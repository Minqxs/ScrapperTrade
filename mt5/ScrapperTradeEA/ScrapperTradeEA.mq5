#property copyright "ScrapperTrade"
#property version   "1.00"
#property strict

#include <Trade/Trade.mqh>

input string QueueName = "ScrapperTrade";
input int PollIntervalMilliseconds = 250;
input int MaximumCommandAgeSeconds = 15;
input bool EmergencyLocked = true;

CTrade trade;
string processed_ids[];
ulong sequence = 0;

string QueuePath(const string leaf) { return QueueName + "\\" + leaf; }

bool IsDemoAccount()
{
   if(!TerminalInfoInteger(TERMINAL_CONNECTED)) return false;
   return (ENUM_ACCOUNT_TRADE_MODE)AccountInfoInteger(ACCOUNT_TRADE_MODE) == ACCOUNT_TRADE_MODE_DEMO;
}

bool WasProcessed(const string id)
{
   for(int i = 0; i < ArraySize(processed_ids); i++)
      if(processed_ids[i] == id) return true;
   return false;
}

void Remember(const string id)
{
   int count = ArraySize(processed_ids);
   if(count >= 2048)
   {
      for(int i = 1; i < count; i++) processed_ids[i - 1] = processed_ids[i];
      ArrayResize(processed_ids, count - 1);
      count--;
   }
   ArrayResize(processed_ids, count + 1);
   processed_ids[count] = id;
}

void WriteAtomic(const string relative, const string payload)
{
   string temp = relative + ".tmp";
   int handle = FileOpen(temp, FILE_WRITE | FILE_TXT | FILE_ANSI | FILE_COMMON);
   if(handle == INVALID_HANDLE) return;
   FileWriteString(handle, payload);
   FileFlush(handle);
   FileClose(handle);
   FileDelete(relative, FILE_COMMON);
   FileMove(temp, FILE_COMMON, relative, FILE_COMMON);
}

void WriteHeartbeat()
{
   sequence++;
   string mode = IsDemoAccount() ? "DEMO" : "UNSAFE";
   string payload = StringFormat("{\"sequence\":%I64u,\"time\":%I64d,\"accountMode\":\"%s\",\"connected\":%s,\"emergencyLocked\":%s}",
      sequence, (long)TimeTradeServer(), mode,
      TerminalInfoInteger(TERMINAL_CONNECTED) ? "true" : "false",
      EmergencyLocked ? "true" : "false");
   WriteAtomic(QueuePath("heartbeat.json"), payload);
}

void Reject(const string id, const string reason)
{
   string safe_reason = reason;
   StringReplace(safe_reason, "\"", "'");
   WriteAtomic(QueuePath("results\\" + id + ".json"),
      StringFormat("{\"commandId\":\"%s\",\"accepted\":false,\"reason\":\"%s\",\"time\":%I64d}", id, safe_reason, (long)TimeTradeServer()));
   Remember(id);
}

// Command files use one pipe-delimited line:
// id|unix-created-at|action|symbol|volume|price|stop-loss|take-profit|ticket
void ProcessCommand(const string filename)
{
   int handle = FileOpen(QueuePath("commands\\" + filename), FILE_READ | FILE_TXT | FILE_ANSI | FILE_COMMON);
   if(handle == INVALID_HANDLE) return;
   string line = FileReadString(handle);
   FileClose(handle);

   string fields[];
   if(StringSplit(line, '|', fields) < 3) return;
   string id = fields[0];
   if(WasProcessed(id)) { Reject(id, "duplicate-command"); return; }
   long created = (long)StringToInteger(fields[1]);
   if(created <= 0 || (long)TimeTradeServer() - created > MaximumCommandAgeSeconds) { Reject(id, "stale-command"); return; }
   if(!IsDemoAccount()) { Reject(id, "account-is-not-positively-verified-demo"); return; }
   if(EmergencyLocked) { Reject(id, "ea-emergency-lock-is-enabled"); return; }

   string action = fields[2];
   bool ok = false;
   if(action == "CLOSE" && ArraySize(fields) >= 9)
      ok = trade.PositionClose((ulong)StringToInteger(fields[8]));
   else if((action == "BUY" || action == "SELL") && ArraySize(fields) >= 8)
   {
      string symbol = fields[3];
      double volume = StringToDouble(fields[4]);
      double price = StringToDouble(fields[5]);
      double stop = StringToDouble(fields[6]);
      double target = StringToDouble(fields[7]);
      if(volume <= 0 || stop <= 0 || target <= 0) { Reject(id, "invalid-protective-order"); return; }
      ok = action == "BUY" ? trade.Buy(volume, symbol, price, stop, target, id)
                           : trade.Sell(volume, symbol, price, stop, target, id);
   }
   else { Reject(id, "unsupported-command"); return; }

   string reason = ok ? "executed" : trade.ResultRetcodeDescription();
   WriteAtomic(QueuePath("results\\" + id + ".json"),
      StringFormat("{\"commandId\":\"%s\",\"accepted\":%s,\"reason\":\"%s\",\"brokerOrder\":%I64u}",
         id, ok ? "true" : "false", reason, trade.ResultOrder()));
   Remember(id);
}

void PollCommands()
{
   string filename;
   long search = FileFindFirst(QueuePath("commands\\*.cmd"), filename, FILE_COMMON);
   if(search == INVALID_HANDLE) return;
   do { ProcessCommand(filename); FileDelete(QueuePath("commands\\" + filename), FILE_COMMON); }
   while(FileFindNext(search, filename));
   FileFindClose(search);
}

int OnInit()
{
   trade.SetAsyncMode(false);
   EventSetMillisecondTimer(MathMax(100, PollIntervalMilliseconds));
   WriteHeartbeat();
   return INIT_SUCCEEDED;
}

void OnDeinit(const int reason) { EventKillTimer(); }
void OnTimer() { WriteHeartbeat(); PollCommands(); }
