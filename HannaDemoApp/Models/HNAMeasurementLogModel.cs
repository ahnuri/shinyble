using HannaDemoApp.Core.Localization;

namespace HannaDemoApp.Models;

// Represents a single meter response captured from the live BLE stream.
public class HNAMeasurementLogModel
{
    public HNAMeasurementLogModel(DateTime recordedAt, string response)
    {
        RecordedAt = recordedAt;
        Timestamp = HNADateTimeFormatter.FormatTime(recordedAt);
        Response = response;
    }

    public HNAMeasurementLogModel(string timestamp, string response)
    {
        RecordedAt = DateTime.MinValue;
        Timestamp = HNADateTimeFormatter.FormatStoredTimestamp(timestamp);
        Response = response;
    }

    public string Timestamp { get; }
    public DateTime RecordedAt { get; }
    public string Response { get; }
}

// Display-oriented measurement log record used in detail views.
public class HNAMeasurementDisplayModel
{
    public int SerialNumber { get; set; }
    public string Timestamp { get; set; }
    public string Response { get; set; }

    public HNAMeasurementDisplayModel(int serialNumber, string timestamp, string response)
    {
        SerialNumber = serialNumber;
        Timestamp = HNADateTimeFormatter.FormatStoredTimestamp(timestamp);
        Response = response;
    }
}
