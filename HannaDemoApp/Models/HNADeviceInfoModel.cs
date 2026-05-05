using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HannaDemoApp.Models;

public class HNADeviceInfoModel : INotifyPropertyChanged
{
    private string _meterModel = string.Empty;
    private string _meterId = string.Empty;
    private string _meterFirmwareVersion = string.Empty;
    private string _bleFirmwareVersion = string.Empty;
    private string _serialNumber = string.Empty;
    private string _recallCount = string.Empty;
    private string _language = string.Empty;
    private string _languageVersion = string.Empty;
    private string _calibrationDate = string.Empty;
    private string _rawDeviceInfo = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string MeterModel
    {
        get => _meterModel;
        set => SetField(ref _meterModel, value);
    }

    public string MeterId
    {
        get => _meterId;
        set => SetField(ref _meterId, value);
    }

    public string MeterFirmwareVersion
    {
        get => _meterFirmwareVersion;
        set => SetField(ref _meterFirmwareVersion, value);
    }

    public string BleFirmwareVersion
    {
        get => _bleFirmwareVersion;
        set => SetField(ref _bleFirmwareVersion, value);
    }

    public string SerialNumber
    {
        get => _serialNumber;
        set => SetField(ref _serialNumber, value);
    }

    public string UserSetName
    {
        get => MeterId;
        set => MeterId = value;
    }

    public string RecallCount
    {
        get => _recallCount;
        set => SetField(ref _recallCount, value);
    }

    public string Language
    {
        get => _language;
        set => SetField(ref _language, value);
    }

    public string LanguageVersion
    {
        get => _languageVersion;
        set => SetField(ref _languageVersion, value);
    }

    public string CalibrationDate
    {
        get => _calibrationDate;
        set => SetField(ref _calibrationDate, value);
    }

    public string RawDeviceInfo
    {
        get => _rawDeviceInfo;
        set => SetField(ref _rawDeviceInfo, value);
    }

    public bool HasValues =>
        !string.IsNullOrWhiteSpace(MeterModel) ||
        !string.IsNullOrWhiteSpace(MeterId) ||
        !string.IsNullOrWhiteSpace(MeterFirmwareVersion) ||
        !string.IsNullOrWhiteSpace(BleFirmwareVersion) ||
        !string.IsNullOrWhiteSpace(SerialNumber) ||
        !string.IsNullOrWhiteSpace(RecallCount) ||
        !string.IsNullOrWhiteSpace(Language) ||
        !string.IsNullOrWhiteSpace(LanguageVersion) ||
        !string.IsNullOrWhiteSpace(CalibrationDate) ||
        !string.IsNullOrWhiteSpace(RawDeviceInfo);

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(HasValues));
        return true;
    }
}
