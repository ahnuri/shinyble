namespace HannaDemoApp.Services.Dialog;

// Defines user-dialog interactions used by view models.
public interface IHNADialogService
{
    //Shows a native action sheet. Returns the tapped button label, or null if cancelled.
    Task<string?> ShowActionSheetAsync(string title, string cancel, string? destructionButton, params string[] buttons);

    // Shows a native alert with a single dismiss button.
    Task ShowAlertAsync(string title, string message, string accept);

    //Shows a native alert with accept/cancel buttons. Returns true when the user taps accept.

    Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel);
}
