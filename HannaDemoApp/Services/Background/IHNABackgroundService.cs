namespace HannaDemoApp.Services.Background;

// Abstracts platform-specific background execution needed during device sessions.
public interface IHNABackgroundService
{
    void StartService();
    void StopService();
}
