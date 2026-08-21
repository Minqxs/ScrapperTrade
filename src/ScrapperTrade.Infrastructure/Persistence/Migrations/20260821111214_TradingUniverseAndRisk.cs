using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrapperTrade.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TradingUniverseAndRisk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowLong",
                table: "instruments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowShort",
                table: "instruments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ExposureGroupCode",
                table: "instruments",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MaxConcurrentPositions",
                table: "instruments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TradingSessionsJson",
                table: "instruments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "broker_symbol_metadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BrokerSymbol = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TickSize = table.Column<decimal>(type: "TEXT", nullable: false),
                    TickValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    ContractSize = table.Column<decimal>(type: "TEXT", nullable: false),
                    VolumeMin = table.Column<decimal>(type: "TEXT", nullable: false),
                    VolumeMax = table.Column<decimal>(type: "TEXT", nullable: false),
                    VolumeStep = table.Column<decimal>(type: "TEXT", nullable: false),
                    StopLevel = table.Column<decimal>(type: "TEXT", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_broker_symbol_metadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_broker_symbol_metadata_instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exposure_groups",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MaxRiskFraction = table.Column<decimal>(type: "TEXT", nullable: false),
                    MaxSameDirectionRiskFraction = table.Column<decimal>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exposure_groups", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BrokerPositionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LogicalSymbol = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BrokerSymbol = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    EntryPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    StopPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    Volume = table.Column<decimal>(type: "TEXT", nullable: false),
                    RiskAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    ExposureGroupCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "risk_policy_changes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ToVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ChangeReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_policy_changes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "risk_policy_versions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    PolicyJson = table.Column<string>(type: "TEXT", nullable: false),
                    ChangedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ChangeReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_policy_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trade_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PositionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SignalId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DetailJson = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trade_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_broker_symbol_metadata_InstrumentId_BrokerSymbol",
                table: "broker_symbol_metadata",
                columns: new[] { "InstrumentId", "BrokerSymbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_positions_BrokerPositionId",
                table: "positions",
                column: "BrokerPositionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_positions_LogicalSymbol_Status",
                table: "positions",
                columns: new[] { "LogicalSymbol", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_policy_changes_OccurredAt",
                table: "risk_policy_changes",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_risk_policy_versions_Version",
                table: "risk_policy_versions",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trade_events_OccurredAt",
                table: "trade_events",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_trade_events_PositionId",
                table: "trade_events",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_trade_events_SignalId",
                table: "trade_events",
                column: "SignalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "broker_symbol_metadata");

            migrationBuilder.DropTable(
                name: "exposure_groups");

            migrationBuilder.DropTable(
                name: "positions");

            migrationBuilder.DropTable(
                name: "risk_policy_changes");

            migrationBuilder.DropTable(
                name: "risk_policy_versions");

            migrationBuilder.DropTable(
                name: "trade_events");

            migrationBuilder.DropColumn(
                name: "AllowLong",
                table: "instruments");

            migrationBuilder.DropColumn(
                name: "AllowShort",
                table: "instruments");

            migrationBuilder.DropColumn(
                name: "ExposureGroupCode",
                table: "instruments");

            migrationBuilder.DropColumn(
                name: "MaxConcurrentPositions",
                table: "instruments");

            migrationBuilder.DropColumn(
                name: "TradingSessionsJson",
                table: "instruments");
        }
    }
}
