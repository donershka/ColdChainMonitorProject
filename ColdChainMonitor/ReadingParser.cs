using System.Globalization;

namespace ColdChainMonitor;

public static class ReadingParser
{
    public static bool TryParseReading(string line, out Reading? reading, out string error)
    {
        reading = null;
        error = string.Empty;
        string[] fields = line.Split('|').Select(field => field.Trim()).ToArray();

        if (fields.Length != 4)
        {
            error = "Expected exactly four pipe-separated fields.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(fields[0]))
        {
            error = "SensorId must not be empty.";
            return false;
        }

        if (!DateTimeOffset.TryParseExact(
            fields[1], "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTimeOffset timestamp))
        {
            error = "Invalid timestamp. Expected yyyy-MM-ddTHH:mm:ssZ.";
            return false;
        }

        if (!Enum.TryParse(fields[2], true, out StorageClass storageClass)
            || !Enum.IsDefined(typeof(StorageClass), storageClass))
        {
            error = "Invalid storage class. Expected Cold or Frozen.";
            return false;
        }

        if (!decimal.TryParse(fields[3],
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out decimal temperature))
        {
            error = "Invalid temperature. Use an invariant decimal such as 5.5.";
            return false;
        }

        reading = new Reading(fields[0], timestamp, storageClass, temperature);
        return true;
    }
}
