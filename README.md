# ColdChainMonitor — Assignment 4

Student: Kurmashev Damir  
Group: IT-2511

## Run

Install the .NET 8 SDK (or a newer SDK that supports net8.0).
Extract the ZIP before running anything. Open the extracted ColdChainMonitorProject folder in VS Code, not only Program.cs.

From the project root:

```bash
dotnet build ColdChainMonitor.sln
dotnet run --project ColdChainMonitor -- ColdChainMonitor/readings.txt ColdChainMonitor/archive.json
dotnet run --project ColdChainMonitor.Tests
```

On Windows, you can also double-click `run.bat` or `test.bat`.
In VS Code install Microsoft's C# extension, open Run and Debug, select **Run ColdChainMonitor**, and press F5 or the green run arrow. Select **Run all automated tests** to run the tests. No keyboard input is required.

The application accepts optional input and output paths:

```bash
dotnet run --project ColdChainMonitor -- path/to/readings.txt path/to/archive.json
```

With no arguments it reads the copied readings.txt next to the executable and writes archive.json beside that file. The explicit command above writes into the source folder, making the output easy to find.

## Expected result

- 6 valid readings in their original input order.
- 4 import errors at lines 7, 8, 9 and 10.
- 2 alerts in sensor and timestamp order.
- S1 at 08:05 UTC: 8.7, outside range and abrupt change from 4.0.
- S2 at 08:06 UTC: -12.0, outside range and abrupt change from -18.0.
- File and MemoryStream round trips both verified.

The generated archive.json is included. Every run replaces it and supplies a new CreatedAtUtc from Program.cs.

## Files and responsibilities

| File | Purpose |
| --- | --- |
| Models.cs | Immutable data records and the StorageClass enumeration |
| ReadingParser.cs | Pure parsing of one line without file access |
| ReadingImporter.cs | Eager stream reading, line numbers and per-sensor class consistency |
| ReadingAnalyzer.cs | Safe limits, grouped timestamp sorting and adjacent-pair alerts |
| ArchiveStorage.cs | JSON read/write and comparison of archive contents |
| Program.cs | File ownership, injected creation time, pipeline orchestration and output |
| ColdChainMonitor.Tests/Program.cs | 28 independently named automated tests |

## Required answers

1. **Who owns each stream?** The caller that creates a stream owns it. Program.cs disposes its FileStream and MemoryStream instances with using. Helpers dispose their reader/writer wrappers with leaveOpen: true, so the caller can continue using the original stream.
2. **Why is a lazy reader sequence unsafe?** An iterator may not read anything until enumeration begins. If its StreamReader was disposed before the method returned, later enumeration uses a closed reader. ReadReadings reads every line and builds lists before returning; the results no longer depend on the reader.
3. **Why reset MemoryStream.Position?** Writing moves the position to the end. Reading starts at the current position, where no JSON remains. Program.cs explicitly sets Position = 0 before deserializing its newly written MemoryStream. The helper does not rewind automatically.
4. **Why group before pairing?** Consecutive readings in a global list may come from different sensors. GroupBy with OrdinalIgnoreCase separates sensors, OrderBy sorts each group by Timestamp, and Zip with Skip(1) creates adjacent pairs within that group.
5. **Why Enum.IsDefined after Enum.TryParse?** TryParse can convert numeric text such as 99 into an enum value even though no member declares that number. IsDefined rejects such values. Named storage classes are parsed case-insensitively. The assignment requires rejecting undeclared numeric values, so declared numeric values 0 and 1 are accepted by this contract.
6. **Which operations are pure?** TryParseReading and Analyze only transform supplied data. HaveSameValues compares data. ReadReadings, WriteArchive and ReadArchive perform stream I/O. Program.cs opens files, writes the console and supplies the current UTC time.
7. **Why compare values instead of references?** Deserialization creates new records and collections. Different object references do not mean the contents differ. SequenceEqual compares reading, error and alert records by value, while CreatedAtUtc is compared separately. Comparing only the outer record would compare collection references instead of their contents.
8. **How do deterministic order and injected time help tests?** Sorting alerts by sensor with OrdinalIgnoreCase and then timestamp makes results predictable. Program.cs supplies CreatedAtUtc; tests supply a fixed value so timing does not change expected results.

## Debugging explanations and fixes

### 4.2 Lazy data backed by a disposed reader

The broken method returns ReadAll(reader), a lazy iterator, while its using statement disposes the reader. Returning the iterator does not execute its reading loop. Enumeration later fails because the wrapper is closed, even if the underlying stream remains open.

A minimal correction is to materialize before leaving the using scope:

```csharp
static IReadOnlyList<string> ReadLines(Stream input)
{
    using var reader = new StreamReader(input, leaveOpen: true);
    var lines = new List<string>();
    string? line;
    while ((line = reader.ReadLine()) != null)
        lines.Add(line);
    return lines.AsReadOnly();
}
```

The actual importer uses this eager-loop approach and immediately parses each line.

### 4.3 Reading from the end of a stream

Serialization advances the stream's Position. Deserializing immediately afterward starts at the end, so there are no bytes to parse. The caller must rewind its MemoryStream:

```csharp
using var stream = new MemoryStream();
ArchiveStorage.WriteArchive(stream, archive);
stream.Position = 0;
var restored = ArchiveStorage.ReadArchive(stream);
```

A separate FileStream opened for reading starts at position zero. The program verifies both the actual file archive and the memory archive.

### 4.4 Comparing unrelated sensors

Sorting all sensors together by time can compare S1 against S2, producing false changes. GroupBy belongs before ordering and pairing:

```csharp
foreach (var sensor in readings.GroupBy(r => r.SensorId, StringComparer.OrdinalIgnoreCase))
{
    var ordered = sensor.OrderBy(r => r.Timestamp).ToArray();
    var pairs = ordered.Zip(ordered.Skip(1),
        (previous, current) => (previous, current));
}
```

The first reading is checked for an unsafe range separately. It cannot be abrupt because it has no previous reading. Each later reading creates at most one alert containing both Boolean reason flags.

## Automated tests

The separate ColdChainMonitor.Tests project uses a small console test runner and no external NuGet packages. Run it using `dotnet run --project ColdChainMonitor.Tests`, not `dotnet test`. It invokes each test independently, prints PASS or FAIL, and returns a nonzero exit code if any test fails.

The 28 tests include all 11 mandatory cases and extra cases for blank lines, exact 4.0 changes, unsafe first readings, malformed fields, bad dates, special decimal strings, case-insensitive sensors, lower boundaries, current stream positions, null archives, reading from EOF, trimming, empty input, alert ordering and invalid initial readings.

Compiler and runtime verification details are included in VALIDATION.txt. The workspace blocked solution-level dotnet build; the SDK compiler successfully compiled both projects and the compiled application and all tests ran successfully.
