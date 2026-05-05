namespace HannaDemoApp.Services.Ota;

/// <summary>OTA progress for UI (title + percent), similar to native HUD.</summary>
public readonly record struct HNAOtaUiProgress(string Title, int PercentComplete);
