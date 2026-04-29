namespace HannaDemoApp.Features.Landing;

public partial class HNALandingPage : ContentPage
{
    public HNALandingPage(HNALandingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

            // 🔥 Fix: explicitly wire command
        ToolbarItems.Add(new ToolbarItem
        {
            IconImageSource = "cloud_upload",
            Order = ToolbarItemOrder.Primary,
            Priority = 0,
            Command = viewModel.ToolbarClickedCommand
        });
    }
}
