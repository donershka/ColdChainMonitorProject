using System.Globalization;
using ColdChainMonitor;

try
{
    string inputPath = args.Length > 0
        ? Path.GetFullPath(args[0])
        : Path.Combine(AppContext.BaseDirectory, "readings.txt");
    string outputPath = args.Length > 1
        ? Path.GetFullPath(args[1])
        : Path.Combine(Path.GetDirectoryName(inputPath)!, "archive.json");

    ImportResult imported;
    using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
    {
        imported = ReadingImporter.ReadReadings(input);
    }

    IReadOnlyList<Alert> alerts = ReadingAnalyzer.Analyze(imported.Readings);
    var archive = new MonitoringArchive(imported.Readings, imported.Errors, alerts,
        DateTimeOffset.UtcNow);

    using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
    {
        ArchiveStorage.WriteArchive(output, archive);
    }

    MonitoringArchive restored;
    using (var input = new FileStream(outputPath, FileMode.Open, FileAccess.Read))
    {
        restored = ArchiveStorage.ReadArchive(input);
    }

    using var memory = new MemoryStream();
    ArchiveStorage.WriteArchive(memory, archive);
    memory.Position = 0;
    MonitoringArchive memoryRestored = ArchiveStorage.ReadArchive(memory);

    if (!ArchiveStorage.HaveSameValues(archive, restored)
        || !ArchiveStorage.HaveSameValues(archive, memoryRestored))
    {
        throw new InvalidDataException("JSON round-trip verification failed.");
    }

    Console.WriteLine("COLD CHAIN MONITOR");
    Console.WriteLine($"Valid readings: {archive.Readings.Count}");
    Console.WriteLine($"Import errors: {archive.Errors.Count}");
    Console.WriteLine($"Alerts: {archive.Alerts.Count}");
    Console.WriteLine("\nIMPORT ERRORS");
    foreach (ImportError error in archive.Errors)
        Console.WriteLine($"Line {error.LineNumber}: {error.Message}");

    Console.WriteLine("\nALERTS");
    foreach (Alert alert in archive.Alerts)
    {
        var reasons = new List<string>();
        if (alert.OutsideRange)
            reasons.Add("Outside range");
        if (alert.AbruptChange)
            reasons.Add("Abrupt change");

        string temperature = alert.Reading.Temperature.ToString("0.0", CultureInfo.InvariantCulture);
        Console.WriteLine($"{alert.Reading.SensorId} | {alert.Reading.Timestamp:HH:mm} UTC | "
            + $"{temperature} C | {string.Join("; ", reasons)}");
    }

    Console.WriteLine("\nFile and MemoryStream round trips: VERIFIED");
    Console.WriteLine($"Archive: {outputPath}");
}
catch (Exception exception) when (exception is IOException
    or UnauthorizedAccessException or System.Text.Json.JsonException)
{
    Console.Error.WriteLine($"Error: {exception.Message}");
    Environment.ExitCode = 1;
}
