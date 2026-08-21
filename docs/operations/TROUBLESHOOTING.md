# Troubleshooting

- Run `scripts/doctor.ps1` first and address each failed prerequisite.
- In PowerShell environments that block `npm.ps1`, use `npm.cmd`.
- If port 5173 or 5178 is occupied, stop the prior ScrapperTrade process with `scripts/stop.ps1`.
- If the EA reports unsafe, verify MT5 connectivity and that the account type explicitly says DEMO.
- MetaEditor, FFmpeg, and Ollama are optional and are reported as unavailable rather than silently assumed.

