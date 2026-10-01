@echo off
cd /d "%~dp0"
dotnet run --project ColdChainMonitor -- ColdChainMonitor/readings.txt ColdChainMonitor/archive.json
pause
