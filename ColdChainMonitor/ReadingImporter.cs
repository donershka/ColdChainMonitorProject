using System.Text;

namespace ColdChainMonitor;

public static class ReadingImporter
{
    public static ImportResult ReadReadings(Stream input)
    {
        using var reader = new StreamReader(input, Encoding.UTF8, true, 1024, leaveOpen: true);
        var readings = new List<Reading>();
        var errors = new List<ImportError>();
        var sensorClasses = new Dictionary<string, StorageClass>(StringComparer.OrdinalIgnoreCase);
        int lineNumber = 0;
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!ReadingParser.TryParseReading(line, out Reading? parsed, out string error))
            {
                errors.Add(new ImportError(lineNumber, line, error));
                continue;
            }

            Reading reading = parsed!;
            if (sensorClasses.TryGetValue(reading.SensorId, out StorageClass previousClass)
                && previousClass != reading.StorageClass)
            {
                errors.Add(new ImportError(lineNumber, line,
                    $"Storage class mismatch for {reading.SensorId}: expected {previousClass}."));
                continue;
            }

            sensorClasses[reading.SensorId] = reading.StorageClass;
            readings.Add(reading);
        }

        return new ImportResult(readings.AsReadOnly(), errors.AsReadOnly());
    }
}
