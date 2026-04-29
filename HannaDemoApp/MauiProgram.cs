using HannaDemoApp.Core.DeviceHandlers;
using HannaDemoApp.Features.Device;
using HannaDemoApp.Features.Device.LiveMeasure;
using HannaDemoApp.Features.Landing;
using HannaDemoApp.Features.LogDetail;
using HannaDemoApp.Features.LogHistory;
using HannaDemoApp.Services.Background;
using HannaDemoApp.Features.UserSettings;
using HannaDemoApp.Services.Ble;
using HannaDemoApp.Services.Database;
using HannaDemoApp.Services.Dialog;
using HannaDemoApp.Services.Navigation;
using Microsoft.Extensions.Logging;
using Shiny;
using HannaDemoApp.Services.Ble.Permission;

namespace HannaDemoApp;

/// <summary>
/// Main MAUI application bootstrap configuration.
/// Configures dependency injection, services, pages, and view models.
/// </summary>
public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{


		// Verbose global exception handling for debugging purposes

		AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
		{
			System.Diagnostics.Debug.WriteLine($"[CRASH] {e.ExceptionObject}");
		};

		TaskScheduler.UnobservedTaskException += (sender, e) =>
		{
			System.Diagnostics.Debug.WriteLine($"[TASK ERROR] {e.Exception}");
		};

#if ANDROID
		Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (sender, e) =>
		{
			System.Diagnostics.Debug.WriteLine($"[ANDROID CRASH] {e.Exception}");
		};
#endif

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseShiny()
		.ConfigureFonts(fonts =>
		{
			fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
		});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Core Services
		builder.Services.AddBluetoothLE();
		builder.Services.AddSingleton<HNADeviceHandlerRegistry>();
		builder.Services.AddSingleton<IHNAProductMatchingService, HNAProductMatchingService>();

		// Application Services
		builder.Services.AddSingleton<IHNADialogService, HNAShellDialogService>();
		builder.Services.AddSingleton<IHNANavigationService, HNAShellNavigationService>();
		builder.Services.AddSingleton<IHNALogRepository, HNASqliteLogRepository>();
		builder.Services.AddSingleton<IBlePermissionService, BlePermissionService>();
#if ANDROID
		builder.Services.AddSingleton<IHNABackgroundService, HNAAndroidBackgroundService>();
#else
			builder.Services.AddSingleton<IHNABackgroundService, HNANoOpBackgroundService>();
#endif
		builder.Services.AddSingleton<IHNABleService, HNABleService>();

		// Features - ViewModels
		builder.Services.AddTransient<HNALandingViewModel>();
		builder.Services.AddTransient<HNADeviceViewModel>();
		builder.Services.AddTransient<LiveDetailsViewModel>();
		builder.Services.AddTransient<HNALogHistoryViewModel>();
		builder.Services.AddTransient<HNALogDetailViewModel>();

		// Features - Pages
		builder.Services.AddTransient<HNALandingPage>();
		builder.Services.AddTransient<HNADevicePage>();
		builder.Services.AddTransient<LiveDetailsPage>();
		builder.Services.AddTransient<HNALogHistoryPage>();
		builder.Services.AddTransient<HNALogDetailPage>();
		builder.Services.AddTransient<HNAUserSettingsPage>();

		builder.Services.AddSingleton<AppShell>();

		return builder.Build();
	}
}
