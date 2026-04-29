using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HannaDemoApp.Models;
using HannaDemoApp.Services.Database;

namespace HannaDemoApp.Features.LogDetail;

public partial class HNALogDetailViewModel : ObservableObject
{
    private readonly IHNALogRepository _logRepository;
    private bool _hasLoaded;

    [ObservableProperty]
    private int? logFileId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoLogs))]
    private bool isLoading;

    [ObservableProperty]
    private string deviceName = string.Empty;

    [ObservableProperty]
    private string fileName = string.Empty;

    [ObservableProperty]
    private string startDateText = string.Empty;

    [ObservableProperty]
    private string endDateText = string.Empty;

    [ObservableProperty]
    private string placeholderText = "Loading log details...";

    public HNALogDetailViewModel(IHNALogRepository logRepository)
    {
        _logRepository = logRepository;
    }

    public bool HasLogs => LogRecords.Count > 0;
    public bool HasNoLogs => !HasLogs && !IsLoading;

    public ObservableCollection<HNAMeasurementDisplayModel> LogRecords { get; } = new();

    public void SetLogFileId(int logFileId)
    {
        if (LogFileId == logFileId)
        {
            return;
        }

        LogFileId = logFileId;
        _hasLoaded = false;
        LogRecords.Clear();
    }

    public async Task LoadLogsAsync()
    {
        if (_hasLoaded)
        {
            return;
        }

        if (!LogFileId.HasValue)
        {
            PlaceholderText = "No log file selected.";
            return;
        }

        _hasLoaded = true;
        IsLoading = true;
        PlaceholderText = "Loading log details...";

        var logFile = await Task.Run(() => _logRepository.GetLogFile(LogFileId.Value));
        var records = await Task.Run(() => _logRepository.GetLogRecords(LogFileId.Value));

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LogRecords.Clear();

            if (logFile != null)
            {
                DeviceName = logFile.DeviceName;
                FileName = logFile.FileName;
                StartDateText = logFile.StartDateText;
                EndDateText = logFile.EndDateText;
            }
            else
            {
                DeviceName = "Unknown device";
                FileName = string.Empty;
                StartDateText = string.Empty;
                EndDateText = string.Empty;
            }

            foreach (var record in records)
            {
                LogRecords.Add(record);
            }

            if (LogRecords.Count == 0)
            {
                PlaceholderText = "No log records found for this file.";
            }

            OnPropertyChanged(nameof(HasLogs));
            OnPropertyChanged(nameof(HasNoLogs));
            OnPropertyChanged(nameof(PlaceholderText));
            IsLoading = false;
        });
    }
}
