using System.Globalization;
using HannaDemoApp.Core.Constants;
using HannaDemoApp.Core.Localization;
using HannaDemoApp.Models;
using Microsoft.Data.Sqlite;

namespace HNADB;

public static class HNALogDatabase
{
    private static readonly string DatabasePath = Path.Combine(FileSystem.AppDataDirectory, HNAAppConstants.DatabaseFileName);
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        using var connection = new SqliteConnection($"Data Source={DatabasePath};Cache=Shared");
        connection.Open();

        using var createLogFiles = connection.CreateCommand();
        createLogFiles.CommandText = @"
            CREATE TABLE IF NOT EXISTS LogFiles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DeviceModel TEXT NOT NULL,
                DeviceId TEXT NOT NULL,
                DeviceName TEXT NOT NULL,
                FileName TEXT NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT NOT NULL
            );";
        createLogFiles.ExecuteNonQuery();

        using var createLogRecords = connection.CreateCommand();
        createLogRecords.CommandText = @"
            CREATE TABLE IF NOT EXISTS LogRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LogFileId INTEGER NOT NULL,
                Timestamp TEXT NOT NULL,
                Response TEXT NOT NULL,
                FOREIGN KEY (LogFileId) REFERENCES LogFiles(Id) ON DELETE CASCADE
            );";
        createLogRecords.ExecuteNonQuery();

        using var createIndex = connection.CreateCommand();
        createIndex.CommandText = @"
            CREATE INDEX IF NOT EXISTS IX_LogRecords_LogFileId ON LogRecords(LogFileId);";
        createIndex.ExecuteNonQuery();

        MigrateLegacyLogs(connection);
        _initialized = true;
    }

    public static int SaveLogFile(string deviceId, string deviceModel, string deviceName, string fileName, DateTime startTime, DateTime endTime, IReadOnlyCollection<HNAMeasurementLogModel> records)
    {
        Initialize();

        using var connection = new SqliteConnection($"Data Source={DatabasePath};Cache=Shared");
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            using var insertFile = connection.CreateCommand();
            insertFile.Transaction = transaction;
            insertFile.CommandText = @"
                INSERT INTO LogFiles (DeviceId, DeviceModel, DeviceName, FileName, StartTime, EndTime)
                VALUES ($deviceId, $deviceModel, $deviceName, $fileName, $startTime, $endTime);
                SELECT last_insert_rowid();";
            insertFile.Parameters.AddWithValue("$deviceId", deviceId);
            insertFile.Parameters.AddWithValue("$deviceModel", deviceModel);
            insertFile.Parameters.AddWithValue("$deviceName", deviceName);
            insertFile.Parameters.AddWithValue("$fileName", fileName);
            insertFile.Parameters.AddWithValue("$startTime", startTime.ToString("o", CultureInfo.InvariantCulture));
            insertFile.Parameters.AddWithValue("$endTime", endTime.ToString("o", CultureInfo.InvariantCulture));

            var logFileId = Convert.ToInt32(insertFile.ExecuteScalar());

            using var insertRecord = connection.CreateCommand();
            insertRecord.Transaction = transaction;
            insertRecord.CommandText = @"
                INSERT INTO LogRecords (LogFileId, Timestamp, Response)
                VALUES ($logFileId, $timestamp, $response);";
            insertRecord.Parameters.AddWithValue("$logFileId", logFileId);
            var timestampParam = insertRecord.Parameters.Add("$timestamp", SqliteType.Text);
            var responseParam = insertRecord.Parameters.Add("$response", SqliteType.Text);

            foreach (var record in records)
            {
                timestampParam.Value = record.Timestamp;
                responseParam.Value = record.Response;
                insertRecord.ExecuteNonQuery();
            }

            transaction.Commit();
            return logFileId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public static IReadOnlyList<HNALogFileModel> GetLogFiles(string? deviceId = null)
    {
        Initialize();

        var files = new List<HNALogFileModel>();
        using var connection = new SqliteConnection($"Data Source={DatabasePath};Cache=Shared");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, DeviceId, DeviceModel, DeviceName, FileName, StartTime, EndTime
            FROM LogFiles
            " + (string.IsNullOrEmpty(deviceId) ? string.Empty : "WHERE DeviceId = $deviceId ") +
            "ORDER BY StartTime DESC;";
        if (!string.IsNullOrEmpty(deviceId))
        {
            command.Parameters.AddWithValue("$deviceId", deviceId);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            files.Add(new HNALogFileModel
            {
                Id = reader.GetInt32(0),
                DeviceId = reader.GetString(1),
                DeviceModel = reader.GetString(2),
                DeviceName = reader.GetString(3),
                FileName = reader.GetString(4),
                StartTime = DateTime.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind),
                EndTime = DateTime.Parse(reader.GetString(6), null, DateTimeStyles.RoundtripKind)
            });
        }

        return files;
    }

    public static HNALogFileModel? GetLogFile(int logFileId)
    {
        Initialize();

        using var connection = new SqliteConnection($"Data Source={DatabasePath};Cache=Shared");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, DeviceId, DeviceModel, DeviceName, FileName, StartTime, EndTime
            FROM LogFiles
            WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", logFileId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new HNALogFileModel
        {
            Id = reader.GetInt32(0),
            DeviceId = reader.GetString(1),
            DeviceModel = reader.GetString(2),
            DeviceName = reader.GetString(3),
            FileName = reader.GetString(4),
            StartTime = DateTime.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind),
            EndTime = DateTime.Parse(reader.GetString(6), null, DateTimeStyles.RoundtripKind)
        };
    }

    /// <summary>
    /// Deletes the specified log files and their child records.
    /// Child records auto-delete via the ON DELETE CASCADE foreign key.
    /// No-op when the list is empty.
    /// </summary>
    public static void DeleteLogFiles(IReadOnlyList<int> ids)
    {
        if (ids.Count == 0)
            return;

        Initialize();

        using var connection = new SqliteConnection($"Data Source={DatabasePath};Cache=Shared");
        connection.Open();

        // Build a parameterized IN clause to avoid SQL injection.
        // e.g. DELETE FROM LogFiles WHERE Id IN ($id0,$id1,$id2)
        var paramNames = ids.Select((_, i) => $"$id{i}").ToList();

        using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM LogFiles WHERE Id IN ({string.Join(",", paramNames)});";

        for (var i = 0; i < ids.Count; i++)
            command.Parameters.AddWithValue(paramNames[i], ids[i]);

        command.ExecuteNonQuery();
    }

    public static IReadOnlyList<HNAMeasurementDisplayModel> GetLogRecords(int logFileId)
    {
        Initialize();

        var records = new List<HNAMeasurementDisplayModel>();
        using var connection = new SqliteConnection($"Data Source={DatabasePath};Cache=Shared");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Timestamp, Response
            FROM LogRecords
            WHERE LogFileId = $logFileId
            ORDER BY Id ASC;";
        command.Parameters.AddWithValue("$logFileId", logFileId);

        using var reader = command.ExecuteReader();
        int recordSerialNumber = 1;
        while (reader.Read())
        {
            records.Add(new HNAMeasurementDisplayModel(recordSerialNumber++, reader.GetString(0), reader.GetString(1)));
        }

        return records;
    }

    private static void MigrateLegacyLogs(SqliteConnection connection)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM LogFiles;";
        var fileCount = Convert.ToInt32(countCommand.ExecuteScalar());
        if (fileCount > 0)
        {
            return;
        }

        var logsRoot = Path.Combine(FileSystem.AppDataDirectory, "logs");
        if (!Directory.Exists(logsRoot))
        {
            return;
        }

        foreach (var deviceFolder in Directory.GetDirectories(logsRoot))
        {
            var deviceId = Path.GetFileName(deviceFolder);
            var deviceModel = "Unknown Model";
            var deviceName = deviceId;

            foreach (var filePath in Directory.GetFiles(deviceFolder, "*.txt").OrderBy(f => File.GetCreationTime(f)))
            {
                try
                {
                    var fileName = Path.GetFileName(filePath);
                    if (!TryParseFileName(fileName, out var startTime, out var endTime))
                    {
                        continue;
                    }

                    var records = new List<HNAMeasurementLogModel>();
                    foreach (var line in File.ReadAllLines(filePath))
                    {
                        var split = line.Split(' ', 2);
                        if (split.Length == 2)
                        {
                            records.Add(new HNAMeasurementLogModel(split[0], split[1]));
                        }
                    }

                    if (records.Count > 0)
                    {
                        SaveLogFile(deviceId, deviceModel, deviceName, fileName, startTime, endTime, records);
                    }
                }
                catch
                {
                    // Skip files that can't be parsed or saved — they won't block the rest.
                }
            }
        }
    }

    private static bool TryParseFileName(string fileName, out DateTime startTime, out DateTime endTime)
    {
        startTime = DateTime.MinValue;
        endTime = DateTime.MinValue;
        if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fileName = Path.GetFileNameWithoutExtension(fileName);
        var parts = fileName.Split("_to_");
        if (parts.Length != 2)
        {
            return false;
        }

        if (!TimeOnly.TryParse(parts[0].Replace('_', ':'), out var start) || !TimeOnly.TryParse(parts[1].Replace('_', ':'), out var end))
        {
            return false;
        }

        startTime = DateTime.Today.Add(start.ToTimeSpan());
        endTime = DateTime.Today.Add(end.ToTimeSpan());
        return true;
    }
}

public sealed class HNALogFileModel
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string StartDateText => HNADateTimeFormatter.FormatDateTime(StartTime);
    public string EndDateText => HNADateTimeFormatter.FormatDateTime(EndTime);
}
