namespace ColdChainMonitor;

public static class ReadingAnalyzer
{
    public static bool IsOutsideRange(Reading reading)
    {
        return reading.StorageClass switch
        {
            StorageClass.Cold => reading.Temperature < 2.0m || reading.Temperature > 8.0m,
            StorageClass.Frozen => reading.Temperature < -22.0m || reading.Temperature > -15.0m,
            _ => throw new ArgumentOutOfRangeException(nameof(reading))
        };
    }

    public static IReadOnlyList<Alert> Analyze(IEnumerable<Reading> readings)
    {
        var alerts = new List<Alert>();

        foreach (var sensor in readings.GroupBy(r => r.SensorId, StringComparer.OrdinalIgnoreCase))
        {
            Reading[] ordered = sensor.OrderBy(r => r.Timestamp).ToArray();
            if (IsOutsideRange(ordered[0]))
                alerts.Add(new Alert(ordered[0], true, false, null));

            var pairs = ordered.Zip(ordered.Skip(1),
                (previous, current) => (Previous: previous, Current: current));

            foreach (var pair in pairs)
            {
                bool outsideRange = IsOutsideRange(pair.Current);
                bool abruptChange = Math.Abs(pair.Current.Temperature - pair.Previous.Temperature) > 4.0m;
                if (outsideRange || abruptChange)
                {
                    alerts.Add(new Alert(pair.Current, outsideRange, abruptChange,
                        pair.Previous.Temperature));
                }
            }
        }

        return alerts.OrderBy(a => a.Reading.SensorId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Reading.Timestamp).ToList().AsReadOnly();
    }
}
