using HNADB;
using HannaDemoApp.Models;

namespace HannaDemoApp.Services.Database;

// Adapts the SQLite log database to the repository interface.
public class HNASqliteLogRepository : IHNALogRepository
{
    public int SaveLogFile(string deviceId, string deviceModel, string deviceName, string fileName, DateTime startTime, DateTime endTime, IReadOnlyCollection<HNAMeasurementLogModel> records)
        => HNALogDatabase.SaveLogFile(deviceId, deviceModel, deviceName, fileName, startTime, endTime, records);

    public IReadOnlyList<HNALogFileModel> GetLogFiles(string? deviceId = null)
        => HNALogDatabase.GetLogFiles(deviceId);

    public HNALogFileModel? GetLogFile(int logFileId)
        => HNALogDatabase.GetLogFile(logFileId);

    public IReadOnlyList<HNAMeasurementDisplayModel> GetLogRecords(int logFileId)
        => HNALogDatabase.GetLogRecords(logFileId);

    public void DeleteLogFiles(IReadOnlyList<int> logFileIds)
        => HNALogDatabase.DeleteLogFiles(logFileIds);
}
