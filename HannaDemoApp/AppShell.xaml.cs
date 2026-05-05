using HannaDemoApp.Core.Constants;
using HannaDemoApp.Features.Device;
using HannaDemoApp.Features.Device.Halo;
using HannaDemoApp.Features.Device.MultiMeter;
using HannaDemoApp.Features.LogHistory;
using HannaDemoApp.Features.Photometer;
using HannaDemoApp.Features.UserSettings;

namespace HannaDemoApp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(HNAAppConstants.Routes.LiveDetails, typeof(HNAHaloMeasurePage));
		Routing.RegisterRoute(HNAAppConstants.Routes.ConnectedPhotometerDetails, typeof(HNAPMConnectionDetails));
		Routing.RegisterRoute(HNAAppConstants.Routes.ConnectedMultiMeterDetails, typeof(HNAMultiMeterConnectionDetails));
		Routing.RegisterRoute(HNAAppConstants.Routes.LogDetail, typeof(HNALogDetailPage));
		Routing.RegisterRoute(nameof(HNAAllDeviceConnectionPage), typeof(HNAAllDeviceConnectionPage));
		Routing.RegisterRoute(HNAAppConstants.Routes.UserSettings, typeof(HNAUserSettingsPage));
	}
}
