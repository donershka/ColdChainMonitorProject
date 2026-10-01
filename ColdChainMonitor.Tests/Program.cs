using System.Text;
using System.Text.Json;
using ColdChainMonitor;

var tests = new (string Name, Action Test)[]
{
    ("Supplied input produces 6 readings, 4 errors and 2 alerts", SuppliedInput),
    ("Undeclared numeric enum 99 is rejected", NumericEnum),
    ("Comma decimal is rejected", CommaDecimal),
    ("Sensor storage class mismatch becomes an import error", ClassMismatch),
    ("Cold 8.0 and Frozen -15.0 are safe", UpperBoundaries),
    ("Adjacent comparisons stay within each sensor", SensorBoundaries),
    ("Timestamp sorting makes alert results independent of input order", TimestampOrder),
    ("ReadReadings leaves the stream open", ImportLeavesOpen),
    ("WriteArchive leaves the stream open", WriteLeavesOpen),
    ("Memory round trip preserves values and string enum names", RoundTrip),
    ("Imported results remain usable after stream disposal", MaterializedResults),
    ("Blank lines preserve original error line numbers", BlankLines),
    ("Exactly 4.0 change is not abrupt", ExactChange),
    ("First unsafe reading is not abrupt", FirstUnsafe),
    ("Malformed field count and empty sensor are rejected", MalformedFields),
    ("Invalid timestamp formats are rejected", BadTimestamp),
    ("Temperature rejects NaN, infinity and exponent notation", BadTemperature),
    ("Sensor matching and storage class parsing ignore case", CaseInsensitive),
    ("Lower safe boundaries are inclusive", LowerBoundaries),
    ("Importer reads from current stream position", ImportPosition),
    ("Archive writer and reader honor current position", ArchivePosition),
    ("ReadArchive leaves the caller stream open", ReadLeavesOpen),
    ("Null JSON archive is rejected", NullArchive),
    ("Reading from the end fails until the caller rewinds", EndOfStream),
    ("Fields are trimmed and UTC is normalized", TrimFields),
    ("Empty input has no readings, errors or alerts", EmptyInput),
    ("Single alert holds both reasons in deterministic sensor order", AlertOrder),
    ("Invalid initial reading does not establish sensor class", InvalidFirst)
};

int failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Test();
        Console.WriteLine($"PASS: {test.Name}");
    }
    catch (Exception error)
    {
        failed++;
        Console.WriteLine($"FAIL: {test.Name}: {error.Message}");
    }
}
Console.WriteLine($"\n{tests.Length - failed}/{tests.Length} tests passed.");
Environment.ExitCode = failed == 0 ? 0 : 1;

static void Check(bool condition, string message = "Assertion failed")
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}");
}

static MemoryStream TextStream(string text) => new(Encoding.UTF8.GetBytes(text));

static ImportResult Import(string text)
{
    using var stream = TextStream(text);
    return ReadingImporter.ReadReadings(stream);
}

static Reading R(string sensor, int minute, decimal temperature, StorageClass storage = StorageClass.Cold)
    => new(sensor, new DateTimeOffset(2026, 9, 22, 8, minute, 0, TimeSpan.Zero), storage, temperature);

static MonitoringArchive SampleArchive()
{
    using var stream = new FileStream(Path.Combine(AppContext.BaseDirectory, "readings.txt"),
        FileMode.Open, FileAccess.Read);
    ImportResult result = ReadingImporter.ReadReadings(stream);
    return new MonitoringArchive(result.Readings, result.Errors, ReadingAnalyzer.Analyze(result.Readings),
        new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.Zero));
}

static void SuppliedInput()
{
    var archive = SampleArchive();
    Check(archive.Readings.Count == 6 && archive.Errors.Count == 4 && archive.Alerts.Count == 2);
    Check(archive.Errors.Select(e => e.LineNumber).SequenceEqual(new[] { 7, 8, 9, 10 }));
    Check(archive.Readings.Select(r => r.Timestamp.Minute).SequenceEqual(new[] { 10, 1, 0, 5, 6, 15 }));
}

static void NumericEnum()
{
    Check(Enum.TryParse("99", out StorageClass value) && (int)value == 99);
    Check(!ReadingParser.TryParseReading("S1|2026-09-22T08:00:00Z|99|5.0", out _, out _));
}

static void CommaDecimal()
    => Check(!ReadingParser.TryParseReading("S1|2026-09-22T08:00:00Z|Cold|5,5", out _, out _));

static void ClassMismatch()
{
    var result = Import("S1|2026-09-22T08:00:00Z|Cold|5.0\ns1|2026-09-22T08:01:00Z|Frozen|-18.0");
    Check(result.Readings.Count == 1 && result.Errors.Count == 1 && result.Errors[0].LineNumber == 2);
}

static void UpperBoundaries()
    => Check(ReadingAnalyzer.Analyze(new[] { R("S1", 0, 8.0m), R("S2", 0, -15.0m, StorageClass.Frozen) }).Count == 0);

static void SensorBoundaries()
    => Check(ReadingAnalyzer.Analyze(new[] { R("S1", 0, 4m), R("S2", 1, -18m, StorageClass.Frozen), R("S1", 2, 5m) }).Count == 0);

static void TimestampOrder()
{
    var readings = SampleArchive().Readings;
    Check(ReadingAnalyzer.Analyze(readings).SequenceEqual(ReadingAnalyzer.Analyze(readings.Reverse())));
}

static void ImportLeavesOpen()
{
    using var stream = TextStream("S1|2026-09-22T08:00:00Z|Cold|4");
    ReadingImporter.ReadReadings(stream);
    Check(stream.CanRead);
    stream.Position = 0;
    Check(stream.ReadByte() == 'S');
}

static void WriteLeavesOpen()
{
    using var stream = new MemoryStream();
    ArchiveStorage.WriteArchive(stream, SampleArchive());
    Check(stream.CanWrite && stream.Length > 0);
    stream.WriteByte(32);
}

static void RoundTrip()
{
    var original = SampleArchive();
    using var stream = new MemoryStream();
    ArchiveStorage.WriteArchive(stream, original);
    string json = Encoding.UTF8.GetString(stream.ToArray());
    Check(json.Contains("\"storageClass\": \"Cold\"") && json.Contains("\"storageClass\": \"Frozen\""));
    Check(json.Contains("\"createdAtUtc\"") && json.Contains('\n'));
    stream.Position = 0;
    var restored = ArchiveStorage.ReadArchive(stream);
    Check(!ReferenceEquals(original, restored) && ArchiveStorage.HaveSameValues(original, restored));
    Check(!ArchiveStorage.HaveSameValues(original, restored with { CreatedAtUtc = restored.CreatedAtUtc.AddSeconds(1) }));
    Check(!ArchiveStorage.HaveSameValues(original, restored with { Readings = Array.Empty<Reading>() }));
    Check(!ArchiveStorage.HaveSameValues(original, restored with { Errors = Array.Empty<ImportError>() }));
    Check(!ArchiveStorage.HaveSameValues(original, restored with { Alerts = Array.Empty<Alert>() }));
}

static void MaterializedResults()
{
    ImportResult result;
    using (var stream = TextStream("S1|2026-09-22T08:00:00Z|Cold|4\nbad"))
        result = ReadingImporter.ReadReadings(stream);
    Check(result.Readings.Single().Temperature == 4m && result.Errors.Single().LineNumber == 2);
}

static void BlankLines()
{
    var result = Import("\n  \nS1|2026-09-22T08:00:00Z|Cold|4\nbad");
    Check(result.Readings.Count == 1 && result.Errors.Single().LineNumber == 4);
}

static void ExactChange()
    => Check(ReadingAnalyzer.Analyze(new[] { R("S1", 0, 4m), R("S1", 1, 8m) }).Count == 0);

static void FirstUnsafe()
{
    var alert = ReadingAnalyzer.Analyze(new[] { R("S1", 0, 9m) }).Single();
    Check(alert.OutsideRange && !alert.AbruptChange && alert.PreviousTemperature == null);
}

static void MalformedFields()
{
    foreach (string line in new[] { "S1|date|Cold", "S1|date|Cold|5|extra", " |2026-09-22T08:00:00Z|Cold|5" })
        Check(!ReadingParser.TryParseReading(line, out _, out _));
}

static void BadTimestamp()
{
    foreach (string date in new[] { "bad-date", "2026-09-22T08:00:00+00:00", "2026-09-22T08:00:00", "2026-02-30T08:00:00Z" })
        Check(!ReadingParser.TryParseReading($"S1|{date}|Cold|5", out _, out _));
}

static void BadTemperature()
{
    foreach (string value in new[] { "NaN", "Infinity", "-Infinity", "1e2", "1,000", "999999999999999999999999999999999" })
        Check(!ReadingParser.TryParseReading($"S1|2026-09-22T08:00:00Z|Cold|{value}", out _, out _));
}

static void CaseInsensitive()
{
    var result = Import("s1|2026-09-22T08:00:00Z|cold|4\nS1|2026-09-22T08:01:00Z|COLD|8.7");
    var alert = ReadingAnalyzer.Analyze(result.Readings).Single();
    Check(result.Errors.Count == 0 && alert.OutsideRange && alert.AbruptChange);
}

static void LowerBoundaries()
{
    Check(!ReadingAnalyzer.IsOutsideRange(R("S1", 0, 2m)));
    Check(!ReadingAnalyzer.IsOutsideRange(R("S2", 0, -22m, StorageClass.Frozen)));
    Check(ReadingAnalyzer.IsOutsideRange(R("S1", 0, 1.9m)));
    Check(ReadingAnalyzer.IsOutsideRange(R("S2", 0, -22.1m, StorageClass.Frozen)));
}

static void ImportPosition()
{
    const string prefix = "skip this\n";
    using var stream = TextStream(prefix + "S1|2026-09-22T08:00:00Z|Cold|5");
    stream.Position = Encoding.UTF8.GetByteCount(prefix);
    var result = ReadingImporter.ReadReadings(stream);
    Check(result.Readings.Count == 1 && result.Errors.Count == 0 && stream.Position == stream.Length);
}

static void ArchivePosition()
{
    var archive = SampleArchive();
    using var stream = new MemoryStream();
    byte[] prefix = Encoding.UTF8.GetBytes("prefix");
    stream.Write(prefix);
    long start = stream.Position;
    ArchiveStorage.WriteArchive(stream, archive);
    Check(stream.ToArray().Take(prefix.Length).SequenceEqual(prefix));
    stream.Position = start;
    Check(ArchiveStorage.HaveSameValues(archive, ArchiveStorage.ReadArchive(stream)));
}

static void ReadLeavesOpen()
{
    using var stream = new MemoryStream();
    ArchiveStorage.WriteArchive(stream, SampleArchive());
    stream.Position = 0;
    ArchiveStorage.ReadArchive(stream);
    Check(stream.CanRead);
    stream.Position = 0;
    Check(stream.ReadByte() == '{');
}

static void NullArchive()
{
    using var stream = TextStream("null");
    Throws<InvalidDataException>(() => ArchiveStorage.ReadArchive(stream));
    Check(stream.CanRead);
}

static void EndOfStream()
{
    using var stream = new MemoryStream();
    var archive = SampleArchive();
    ArchiveStorage.WriteArchive(stream, archive);
    Throws<JsonException>(() => ArchiveStorage.ReadArchive(stream));
    stream.Position = 0;
    Check(ArchiveStorage.HaveSameValues(archive, ArchiveStorage.ReadArchive(stream)));
}

static void TrimFields()
{
    Check(ReadingParser.TryParseReading(" S1 | 2026-09-22T08:00:00Z | Cold | +5.5 ", out var reading, out _));
    Check(reading!.SensorId == "S1" && reading.Temperature == 5.5m && reading.Timestamp.Offset == TimeSpan.Zero);
}

static void EmptyInput()
{
    var result = Import("");
    Check(result.Readings.Count == 0 && result.Errors.Count == 0 && ReadingAnalyzer.Analyze(result.Readings).Count == 0);
}

static void AlertOrder()
{
    var alerts = SampleArchive().Alerts;
    Check(alerts.Select(a => a.Reading.SensorId).SequenceEqual(new[] { "S1", "S2" }));
    Check(alerts.All(a => a.OutsideRange && a.AbruptChange));
    Check(alerts[0].Reading.Timestamp.Minute == 5 && alerts[1].Reading.Timestamp.Minute == 6);
    Check(alerts[0].PreviousTemperature == 4m && alerts[1].PreviousTemperature == -18m);
}

static void InvalidFirst()
{
    var result = Import("S1|bad-date|Cold|4\ns1|2026-09-22T08:00:00Z|Frozen|-18");
    Check(result.Errors.Count == 1 && result.Readings.Single().StorageClass == StorageClass.Frozen);
}
