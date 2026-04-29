using HannaDemoApp.Models;
using HNADB;

namespace HannaDemoApp.Services.Database;

// Defines persisted log access used by view models and device logging.
public interface IHNALogRepository
{
    int SaveLogFile(string deviceId, string deviceModel, string deviceName, string fileName, DateTime startTime, DateTime endTime, IReadOnlyCollection<HNAMeasurementLogModel> records);
    IReadOnlyList<HNALogFileModel> GetLogFiles(string? deviceId = null);
    HNALogFileModel? GetLogFile(int logFileId);
    IReadOnlyList<HNAMeasurementDisplayModel> GetLogRecords(int logFileId);

    //Permanently deletes the specified log files and all their records.
    void DeleteLogFiles(IReadOnlyList<int> logFileIds);
}
