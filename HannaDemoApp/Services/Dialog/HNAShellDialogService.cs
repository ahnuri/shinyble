namespace HannaDemoApp.Services.Dialog;

// Uses MAUI Shell dialogs for user prompts on the UI thread.
public class HNAShellDialogService : IHNADialogService
{
    
    public Task<string?> ShowActionSheetAsync(string title, string cancel, string? destructionButton, params string[] buttons)
    {
        return MainThread.InvokeOnMainThreadAsync(
            () => Shell.Current.DisplayActionSheetAsync(title, cancel, destructionButton, buttons));
    }

    public Task ShowAlertAsync(string title, string message, string accept)
    {
        return MainThread.InvokeOnMainThreadAsync(
            () => Shell.Current.DisplayAlertAsync(title, message, accept));
    }

    public Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel)
    {
        return MainThread.InvokeOnMainThreadAsync(
            () => Shell.Current.DisplayAlertAsync(title, message, accept, cancel));
    }


}
