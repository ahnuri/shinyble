using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HNADB;
using HannaDemoApp.Services.Database;
using HannaDemoApp.Services.Dialog;
using HannaDemoApp.Services.Navigation;

namespace HannaDemoApp.Features.LogHistory;

public partial class HNALogHistoryViewModel : ObservableObject
{
    private readonly IHNALogRepository _logRepository;
    private readonly IHNANavigationService _navigationService;
    private readonly IHNADialogService _dialogService;

    // -------------------------------------------------------------------------
    // [ObservableProperty] fields
    // -------------------------------------------------------------------------

    [ObservableProperty]
    private string? deviceId;

    /// <summary>True while the async log load is in progress.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoLogs))]
    [NotifyPropertyChangedFor(nameof(PlaceholderText))]
    private bool isLoading;

    /// <summary>
    /// True while the user is selecting items for bulk action.
    /// Setting false clears all IsSelected flags and hides the bottom bar.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectModeButtonText))]
    [NotifyPropertyChangedFor(nameof(IsNormalMode))]
    private bool isSelectionMode;

    /// <summary>Cached total item count, invalidated when LogGroups changes.</summary>
    private int _cachedTotalCount;
    private bool _totalCountDirty = true;

    /// <summary>Cached selected item count, invalidated when selection changes.</summary>
    private int _cachedSelectedCount;
    private bool _selectedCountDirty = true;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public HNALogHistoryViewModel(
        IHNANavigationService navigationService,
        IHNALogRepository logRepository,
        IHNADialogService dialogService,
        string? deviceId = null)
    {
        _navigationService = navigationService;
        _logRepository = logRepository;
        _dialogService = dialogService;
        DeviceId = deviceId;
    }

    // -------------------------------------------------------------------------
    // Partial method hooks — CommunityToolkit calls these after property changes
    // -------------------------------------------------------------------------

    partial void OnDeviceIdChanged(string? oldValue, string? newValue)
    {
        // New device filter — discard the old list and selection state.
        UnsubscribeAllItems();
        LogGroups.Clear();
        NotifyLogStateChanged();
    }

    partial void OnIsSelectionModeChanged(bool value)
    {
        if (!value)
        {
            // Exiting selection mode — clear every item's selection flag.
            foreach (var item in LogGroups.SelectMany(g => g))
                item.IsSelected = false;
        }

        NotifySelectionStateChanged();
    }

    // -------------------------------------------------------------------------
    // Nested group type
    // -------------------------------------------------------------------------

    /// <summary>
    /// A named group of selectable log files for the grouped CollectionView.
    /// SectionKey   = device ID (used as the grouping key).
    /// SectionName  = human-readable label shown as the group header.
    /// </summary>
    public sealed class LogFilesGroup : ObservableCollection<SelectableLogFile>
    {
        public string SectionKey { get; }
        public string SectionName { get; }

        public LogFilesGroup(
            string sectionKey,
            string sectionName,
            IEnumerable<SelectableLogFile> logFiles) : base(logFiles)
        {
            SectionKey = sectionKey;
            SectionName = sectionName;
        }
    }

    // -------------------------------------------------------------------------
    // Computed properties
    // -------------------------------------------------------------------------

    public bool HasLogs => LogGroups.Count > 0;
    public bool HasNoLogs => !HasLogs && !IsLoading;

    /// <summary>Inverse of IsSelectionMode — drives Chevron visibility and tap behaviour.</summary>
    public bool IsNormalMode => !IsSelectionMode;

    /// <summary>Toolbar button label: "Select" in normal mode, "Done" in selection mode.</summary>
    public string SelectModeButtonText => IsSelectionMode ? "Done" : "Select";

    public string PlaceholderText => HasNoLogs
        ? "No saved logs found. Logs will appear automatically here."
        : string.Empty;

    /// <summary>Total number of log files across all groups.</summary>
    public int TotalCount
    {
        get
        {
            if (_totalCountDirty)
            {
                _cachedTotalCount = LogGroups.SelectMany(g => g).Count();
                _totalCountDirty = false;
            }
            return _cachedTotalCount;
        }
    }

    /// <summary>Count of currently selected items across all groups.</summary>
    public int SelectedCount
    {
        get
        {
            if (_selectedCountDirty)
            {
                _cachedSelectedCount = LogGroups.SelectMany(g => g).Count(f => f.IsSelected);
                _selectedCountDirty = false;
            }
            return _cachedSelectedCount;
        }
    }

    /// <summary>True when at least one item is selected.</summary>
    public bool HasSelection => SelectedCount > 0;

    /// <summary>Selection copy for the bottom action bar.</summary>
    public string SelectionCountText => SelectedCount switch
    {
        0 => "Select Logs",
        1 => "1 Log Selected",
        _ => $"{SelectedCount} Logs Selected"
    };

    /// <summary>"Deselect All" when every item is checked; "Select All" otherwise.</summary>
    public string SelectAllText => SelectedCount == TotalCount && TotalCount > 0
        ? "Deselect All"
        : "Select All";

    // -------------------------------------------------------------------------
    // Collections
    // -------------------------------------------------------------------------

    /// <summary>Grouped log files bound to the CollectionView. Each item is a SelectableLogFile.</summary>
    public ObservableCollection<LogFilesGroup> LogGroups { get; } = new();

    // -------------------------------------------------------------------------
    // Commands
    // -------------------------------------------------------------------------

    /// <summary>Toggles between normal browsing and multi-selection mode.</summary>
    [RelayCommand]
    private void ToggleSelectionMode()
    {
        IsSelectionMode = !IsSelectionMode;
    }

    /// <summary>
    /// Selects all items when not all are selected; deselects all when all are selected.
    /// Mirrors the iOS Photos "Select All" / "Deselect All" behaviour.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        var allItems = LogGroups.SelectMany(g => g).ToList();
        bool select = SelectedCount < TotalCount;
        foreach (var item in allItems)
            item.IsSelected = select;
    }

    /// <summary>
    /// Unified item tap handler:
    ///   • Normal mode  → navigate to Log Detail page.
    ///   • Select mode  → toggle the item's IsSelected flag.
    /// </summary>
    [RelayCommand]
    private Task TapItem(SelectableLogFile? item)
    {
        if (item == null)
            return Task.CompletedTask;

        if (IsSelectionMode)
        {
            item.IsSelected = !item.IsSelected;
            return Task.CompletedTask;
        }

        return _navigationService.NavigateToLogDetailAsync(item.File.Id);
    }

    /// <summary>
    /// Asks the user to confirm, then deletes all selected log files from the
    /// database and removes them from the UI. Empty groups are also removed.
    /// Exits selection mode when done.
    /// </summary>
    [RelayCommand]
    private async Task DeleteSelected()
    {
        var selected = LogGroups.SelectMany(g => g).Where(f => f.IsSelected).ToList();
        if (selected.Count == 0)
            return;

        var label = selected.Count == 1 ? "1 log file" : $"{selected.Count} log files";
        var confirmed = await _dialogService.ShowConfirmAsync(
            "Delete Logs",
            $"Permanently delete {label}? This cannot be undone.",
            "Delete",
            "Cancel");

        if (!confirmed)
            return;

        // --- Delete from database on a background thread ---
        var ids = selected.Select(f => f.File.Id).ToList();
        await Task.Run(() => _logRepository.DeleteLogFiles(ids));

        // --- Update UI on the main thread ---
        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var item in selected)
            {
                UnsubscribeItem(item);

                foreach (var group in LogGroups)
                    group.Remove(item);
            }

            // Collapse groups that are now empty.
            var emptyGroups = LogGroups.Where(g => g.Count == 0).ToList();
            foreach (var group in emptyGroups)
                LogGroups.Remove(group);

            IsSelectionMode = false;
            NotifyLogStateChanged();
        });
    }

    /// <summary>
    /// Builds a plain-text export of all selected log files (header + records)
    /// and hands it to the OS share sheet via MAUI's Share API.
    /// </summary>
    [RelayCommand]
    private async Task ShareSelected()
    {
        var selected = LogGroups.SelectMany(g => g).Where(f => f.IsSelected).ToList();
        if (selected.Count == 0)
            return;

        var sb = new StringBuilder();

        foreach (var item in selected)
        {
            var file = item.File;

            sb.AppendLine($"Device  : {file.DeviceName}");
            sb.AppendLine($"Model   : {file.DeviceModel}");
            sb.AppendLine($"Start   : {file.StartDateText}");
            sb.AppendLine($"End     : {file.EndDateText}");
            sb.AppendLine(new string('-', 40));

            // Load records on a background thread to avoid blocking the UI.
            var records = await Task.Run(() => _logRepository.GetLogRecords(file.Id));
            foreach (var record in records)
                sb.AppendLine($"{record.Timestamp}  {record.Response}");

            sb.AppendLine();
        }

        var shareTitle = selected.Count == 1
            ? $"Log: {selected[0].File.DeviceName} {selected[0].File.StartDateText}"
            : $"{selected.Count} Log Files";

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = shareTitle,
            Text = sb.ToString()
        });
    }

    [RelayCommand]
    private Task NavigateBack() => _navigationService.NavigateToLandingAsync();

    [RelayCommand]
    private Task NavigateToDevices() => _navigationService.NavigateToDevicesAsync();

    // -------------------------------------------------------------------------
    // Load
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads (or reloads) log files from the repository and rebuilds LogGroups.
    /// Guards against concurrent loads with the IsLoading flag.
    /// </summary>
    public async Task LoadLogsAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            var files = await Task.Run(() => _logRepository.GetLogFiles(DeviceId));

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Clean up old subscriptions before clearing.
                UnsubscribeAllItems();
                LogGroups.Clear();

                var groups = files
                    .GroupBy(file => file.DeviceId)
                    .Select(group =>
                    {
                        var first = group.First();

                        // Build the section header: "Model - Name" or just "Name" when
                        // the name already contains the model string.
                        var sectionName = string.IsNullOrWhiteSpace(first.DeviceModel) ||
                            first.DeviceName?.Contains(first.DeviceModel, StringComparison.OrdinalIgnoreCase) == true
                            ? first.DeviceName ?? "Unknown Device"
                            : $"{first.DeviceModel} - {first.DeviceName ?? "Unknown Device"}";

                        var selectableItems = group
                            .OrderByDescending(file => file.StartTime)
                            .Select(f => new SelectableLogFile(f));

                        return new LogFilesGroup(group.Key, sectionName, selectableItems);
                    });

                foreach (var group in groups)
                {
                    LogGroups.Add(group);
                    SubscribeGroupItems(group);
                }

                NotifyLogStateChanged();
                IsLoading = false;
            });
        }
        catch
        {
            // Reset the loading flag so the spinner doesn't stay visible permanently.
            IsLoading = false;
        }
    }

    // -------------------------------------------------------------------------
    // Selection tracking helpers
    // -------------------------------------------------------------------------

    /// <summary>Subscribe to IsSelected changes for every item in a newly added group.</summary>
    private void SubscribeGroupItems(LogFilesGroup group)
    {
        foreach (var item in group)
            SubscribeItem(item);
    }

    /// <summary>Start listening to an individual item so selection counts stay current.</summary>
    private void SubscribeItem(SelectableLogFile item)
    {
        item.PropertyChanged += OnItemSelectionChanged;
    }

    /// <summary>Stop listening — called before removing an item or clearing all groups.</summary>
    private void UnsubscribeItem(SelectableLogFile item)
    {
        item.PropertyChanged -= OnItemSelectionChanged;
    }

    /// <summary>Bulk unsubscribe called before LogGroups.Clear() to avoid memory leaks.</summary>
    private void UnsubscribeAllItems()
    {
        foreach (var item in LogGroups.SelectMany(g => g))
            UnsubscribeItem(item);
    }

    private void OnItemSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableLogFile.IsSelected))
            NotifySelectionStateChanged();
    }

    private void NotifySelectionStateChanged()
    {
        _selectedCountDirty = true;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionCountText));
        OnPropertyChanged(nameof(SelectAllText));
    }

    private void NotifyLogStateChanged()
    {
        _totalCountDirty = true;
        _selectedCountDirty = true;
        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(HasNoLogs));
        OnPropertyChanged(nameof(PlaceholderText));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(SelectAllText));
    }
}

// =============================================================================
// SelectableLogFile
//
// A lightweight ObservableObject wrapper around HNALogFileModel that adds an
// IsSelected flag for multi-select UI. Kept outside the ViewModel class so
// it can be referenced cleanly from XAML DataTemplates.
//
// How selection tracking works end-to-end:
//   1. User taps a checkbox or the card → IsSelected toggles.
//   2. ObservableObject raises PropertyChanged("IsSelected").
//   3. HNALogHistoryViewModel.OnItemSelectionChanged handles the event.
//   4. ViewModel calls NotifySelectionStateChanged() → INPC for SelectedCount,
//      HasSelection, SelectionCountText → UI updates without any polling.
// =============================================================================
public partial class SelectableLogFile : ObservableObject
{
    /// <summary>Two-way bound to the CheckBox in the item template.</summary>
    [ObservableProperty]
    private bool isSelected;

    /// <summary>The underlying database record — bind to File.DeviceName etc. in XAML.</summary>
    public HNALogFileModel File { get; }

    public SelectableLogFile(HNALogFileModel file)
    {
        File = file;
    }
}

