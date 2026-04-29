namespace HannaDemoApp.Services.Background;

// Placeholder background service used on platforms that do not require one (iOS, Mac, Windows).
public class HNANoOpBackgroundService : IHNABackgroundService
{
    public void StartService() { }
    public void StopService() { }
}
