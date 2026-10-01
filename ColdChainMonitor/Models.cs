namespace ColdChainMonitor;

public enum StorageClass { Cold, Frozen }

public sealed record Reading(
    string SensorId,
    DateTimeOffset Timestamp,
    StorageClass StorageClass,
    decimal Temperature);

public sealed record ImportError(int LineNumber, string RawLine, string Message);

public sealed record ImportResult(
    IReadOnlyList<Reading> Readings,
    IReadOnlyList<ImportError> Errors);

public sealed record Alert(
    Reading Reading,
    bool OutsideRange,
    bool AbruptChange,
    decimal? PreviousTemperature);

public sealed record MonitoringArchive(
    IReadOnlyList<Reading> Readings,
    IReadOnlyList<ImportError> Errors,
    IReadOnlyList<Alert> Alerts,
    DateTimeOffset CreatedAtUtc);
