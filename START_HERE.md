# Start here

## Requirements

- .NET 8 SDK, or a newer SDK with support for net8.0.
- VS Code with the Microsoft C# extension, or Visual Studio.

## Run in VS Code

1. Extract the ZIP archive.
2. Open the entire ColdChainMonitorProject folder in VS Code.
3. Install the recommended Microsoft C# extension.
4. Open Run and Debug.
5. Select Run ColdChainMonitor and press F5 or the green arrow.
6. Select Run all automated tests to execute the separate test project.

The program uses readings.txt automatically. No interactive input is required.
The VS Code launch configuration writes archive.json into the ColdChainMonitor folder.

## Run from a terminal

Open a terminal in the ColdChainMonitorProject folder:

```bash
dotnet build ColdChainMonitor.sln
dotnet run --project ColdChainMonitor -- ColdChainMonitor/readings.txt ColdChainMonitor/archive.json
dotnet run --project ColdChainMonitor.Tests
```

On Windows you can also double-click run.bat or test.bat.

## Expected output

- Valid readings: 6
- Import errors: 4
- Alerts: 2
- S1 at 08:05 UTC: outside range and abrupt change.
- S2 at 08:06 UTC: outside range and abrupt change.
- File and MemoryStream round trips: VERIFIED.
- Automated tests: 28/28 passed.

## Submission contents

The ZIP contains the solution, console application, separate automated test project,
readings.txt, the generated archive.json, README answers and debugging explanations,
startup instructions, and VS Code run configurations.

Read README.md for the required explanations. Read VALIDATION.txt for verification details.
The test project has a dependency-free console runner: run it with dotnet run,
not dotnet test. A failed test produces a nonzero process exit code.
