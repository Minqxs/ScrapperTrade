using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScrapperTrade.Application;

namespace ScrapperTrade.Infrastructure;

public sealed class Mt5CommonFilesHeartbeatReader
{
    private readonly string heartbeatPath;
    private readonly TimeSpan maximumAge;

    public Mt5CommonFilesHeartbeatReader(string commonFilesRoot, string queueName = "ScrapperTrade", TimeSpan? maximumAge = null)
    {
        heartbeatPath = Path.Combine(commonFilesRoot, queueName, "heartbeat.json");
        this.maximumAge = maximumAge ?? TimeSpan.FromSeconds(5);
    }

    public Mt5ConnectionSnapshot Read(DateTimeOffset now)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(heartbeatPath));
            var root = document.RootElement;
            var sequence = root.GetProperty("sequence").GetInt64();
            var observed = DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("time").GetInt64());
            var connected = root.GetProperty("connected").GetBoolean();
            var locked = root.GetProperty("emergencyLocked").GetBoolean();
            var account = ParseAccount(root.GetProperty("accountMode").GetString());
            var position = root.TryGetProperty("positionMode", out var value) ? ParsePosition(value.GetString()) : Mt5PositionMode.Unknown;
            return new(sequence, observed, now, connected, account, position, locked, maximumAge);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or ArgumentOutOfRangeException)
        {
            return new(0, DateTimeOffset.MinValue, now, false, Mt5AccountSafetyMode.Unknown, Mt5PositionMode.Unknown, true, maximumAge);
        }
    }

    private static Mt5AccountSafetyMode ParseAccount(string? value) => value?.ToUpperInvariant() switch
    {
        "DEMO" => Mt5AccountSafetyMode.Demo,
        "REAL" => Mt5AccountSafetyMode.Real,
        "CONTEST" => Mt5AccountSafetyMode.Contest,
        _ => Mt5AccountSafetyMode.Unknown
    };

    private static Mt5PositionMode ParsePosition(string? value) => value?.ToUpperInvariant() switch
    {
        "HEDGING" => Mt5PositionMode.Hedging,
        "NETTING" => Mt5PositionMode.Netting,
        _ => Mt5PositionMode.Unknown
    };
}

public sealed class Mt5CommonFilesCommandQueue
{
    private readonly string commandsDirectory;
    private readonly string resultsDirectory;
    private readonly Mt5CommonFilesHeartbeatReader heartbeatReader;

    public Mt5CommonFilesCommandQueue(string commonFilesRoot, string queueName = "ScrapperTrade", TimeSpan? maximumHeartbeatAge = null)
    {
        var root = Path.Combine(commonFilesRoot, queueName);
        commandsDirectory = Path.Combine(root, "commands");
        resultsDirectory = Path.Combine(root, "results");
        heartbeatReader = new Mt5CommonFilesHeartbeatReader(commonFilesRoot, queueName, maximumHeartbeatAge);
    }

    public string Enqueue(Mt5Command command, DateTimeOffset now)
    {
        var safety = heartbeatReader.Read(now);
        if (!safety.AllowsOrderTransmission) throw new InvalidOperationException("MT5 command transmission requires fresh, connected, positive-DEMO evidence and an unlocked EA.");
        if (command.CommandId == Guid.Empty) throw new ArgumentException("Command ID is required.", nameof(command));
        if (command.CreatedAt > now.AddSeconds(1) || command.ExpiresAt <= now) throw new InvalidOperationException("Stale or future-dated commands cannot be queued.");
        if (command.Sequence <= 0) throw new ArgumentOutOfRangeException(nameof(command), "Sequence must be positive.");
        if (command.Action is Mt5CommandAction.Buy or Mt5CommandAction.Sell && (string.IsNullOrWhiteSpace(command.Symbol) || command.Volume <= 0 || command.StopLoss <= 0 || command.TakeProfit <= 0))
            throw new InvalidOperationException("Entry commands require symbol, volume, stop loss, and take profit.");
        if (command.Action is Mt5CommandAction.Close or Mt5CommandAction.Cancel && command.Ticket == 0) throw new InvalidOperationException("Close and cancel commands require a ticket.");

        Directory.CreateDirectory(commandsDirectory);
        Directory.CreateDirectory(resultsDirectory);
        var name = command.CommandId.ToString("D", CultureInfo.InvariantCulture);
        var finalPath = Path.Combine(commandsDirectory, name + ".cmd");
        if (File.Exists(finalPath) || File.Exists(Path.Combine(resultsDirectory, name + ".json")))
            throw new InvalidOperationException("The command ID has already been queued or completed.");

        var fields = new[]
        {
            name,
            command.CreatedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            command.Action.ToString().ToUpperInvariant(),
            command.Symbol,
            command.Volume.ToString(CultureInfo.InvariantCulture),
            command.Price.ToString(CultureInfo.InvariantCulture),
            command.StopLoss.ToString(CultureInfo.InvariantCulture),
            command.TakeProfit.ToString(CultureInfo.InvariantCulture),
            command.Ticket.ToString(CultureInfo.InvariantCulture),
            command.Sequence.ToString(CultureInfo.InvariantCulture),
            command.ExpiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
        };
        if (fields.Any(field => field.Contains('|') || field.Contains('\r') || field.Contains('\n')))
            throw new InvalidOperationException("Command fields contain protocol delimiters.");
        WriteAtomic(finalPath, string.Join('|', fields));
        return finalPath;
    }

    public Mt5CommandResult? TryReadResult(Guid commandId)
    {
        var path = Path.Combine(resultsDirectory, commandId.ToString("D", CultureInfo.InvariantCulture) + ".json");
        if (!File.Exists(path)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        DateTimeOffset? completed = root.TryGetProperty("time", out var time) ? DateTimeOffset.FromUnixTimeSeconds(time.GetInt64()) : null;
        ulong? brokerOrder = root.TryGetProperty("brokerOrder", out var order) ? order.GetUInt64() : null;
        var resultId = Guid.Parse(root.GetProperty("commandId").GetString()!);
        if (resultId != commandId) throw new InvalidDataException("Result command ID does not match the requested command.");
        return new(resultId, root.GetProperty("accepted").GetBoolean(), root.GetProperty("reason").GetString() ?? "unknown", completed, brokerOrder);
    }

    private static void WriteAtomic(string finalPath, string payload)
    {
        var temporaryPath = finalPath + ".tmp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        File.WriteAllText(temporaryPath, payload, new UTF8Encoding(false));
        File.Move(temporaryPath, finalPath, false);
    }
}

public sealed class Mt5CommonFilesExecutionSnapshotReader
{
    private readonly string root;
    private readonly JsonSerializerOptions options = CreateOptions();

    public Mt5CommonFilesExecutionSnapshotReader(string commonFilesRoot, string queueName = "ScrapperTrade") => root = Path.Combine(commonFilesRoot, queueName);

    public Mt5ExecutionSnapshot<Mt5BrokerPosition>? ReadPositions() => Read<Mt5BrokerPosition>("positions.json");
    public Mt5ExecutionSnapshot<Mt5BrokerOrder>? ReadOrders() => Read<Mt5BrokerOrder>("orders.json");

    private Mt5ExecutionSnapshot<T>? Read<T>(string name)
    {
        try { return JsonSerializer.Deserialize<Mt5ExecutionSnapshot<T>>(File.ReadAllText(Path.Combine(root, name)), options); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }


    private static JsonSerializerOptions CreateOptions()
    {
        var result = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        result.Converters.Add(new JsonStringEnumConverter());
        return result;
    }
}

public sealed class Mt5CommonFilesSymbolReader
{
    private readonly string path;
    public Mt5CommonFilesSymbolReader(string commonFilesRoot, string queueName = "ScrapperTrade") => path = Path.Combine(commonFilesRoot, queueName, "symbols.json");

    public IReadOnlyList<Mt5BrokerSymbol> Read()
    {
        try
        {
            return JsonSerializer.Deserialize<List<Mt5BrokerSymbol>>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { return []; }
    }
}
