namespace HannaDemoApp.Features.LogDetail;

[QueryProperty(nameof(LogFileId), "logFileId")]
public partial class HNALogDetailPage : ContentPage
{
    private readonly HNALogDetailViewModel _viewModel;

    public string? LogFileId
    {
        get => _viewModel.LogFileId?.ToString();
        set
        {
            if (int.TryParse(value, out var id))
            {
                _viewModel.SetLogFileId(id);
            }
        }
    }

    public HNALogDetailPage(HNALogDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadLogsAsync();
    }
}
