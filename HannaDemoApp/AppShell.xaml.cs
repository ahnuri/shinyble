using HannaDemoApp.Core.Constants;
using HannaDemoApp.Features.Device.LiveMeasure;
using HannaDemoApp.Features.LogDetail;
using HannaDemoApp.Features.Device;
using HannaDemoApp.Features.UserSettings;

namespace HannaDemoApp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(HNAAppConstants.Routes.LiveDetails, typeof(LiveDetailsPage));
		Routing.RegisterRoute(HNAAppConstants.Routes.LogDetail, typeof(HNALogDetailPage));
		Routing.RegisterRoute(nameof(HNADevicePage), typeof(HNADevicePage));
		Routing.RegisterRoute(HNAAppConstants.Routes.UserSettings, typeof(HNAUserSettingsPage));
	}
}
