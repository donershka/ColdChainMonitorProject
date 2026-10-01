using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColdChainMonitor;

public static class ArchiveStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static void WriteArchive(Stream output, MonitoringArchive archive)
    {
        using var writer = new StreamWriter(output, new UTF8Encoding(false), 1024, leaveOpen: true);
        writer.Write(JsonSerializer.Serialize(archive, Options));
    }

    public static MonitoringArchive ReadArchive(Stream input)
    {
        using var reader = new StreamReader(input, Encoding.UTF8, true, 1024, leaveOpen: true);
        return JsonSerializer.Deserialize<MonitoringArchive>(reader.ReadToEnd(), Options)
            ?? throw new InvalidDataException("The JSON archive must not be null.");
    }

    public static bool HaveSameValues(MonitoringArchive original, MonitoringArchive restored)
    {
        return original.CreatedAtUtc == restored.CreatedAtUtc
            && original.Readings.SequenceEqual(restored.Readings)
            && original.Errors.SequenceEqual(restored.Errors)
            && original.Alerts.SequenceEqual(restored.Alerts);
    }
}
