namespace HannaDemoApp.Services.Ble;

/// <summary>
/// Helper for managing background async operations with proper error handling and tracking.
/// Prevents silent failures from fire-and-forget tasks.
/// </summary>
internal static class AsyncTaskExtensions
{
	/// <summary>
	/// Safely schedules an async operation with error logging.
	/// </summary>
	/// <param name="task">The task to execute.</param>
	/// <param name="operation">Description of the operation for logging.</param>
	public static void SafeFireAndForget(
		this Task task,
		string operation = "Async operation")
	{
		ArgumentNullException.ThrowIfNull(task, nameof(task));

		_ = task.ContinueWith(t =>
		{
			if (t.IsFaulted && t.Exception != null)
			{
				System.Diagnostics.Debug.WriteLine(
					$"[AsyncError] {operation} failed: {t.Exception.InnerException?.Message ?? t.Exception.Message}");
			}
			else if (t.IsCanceled)
			{
				System.Diagnostics.Debug.WriteLine($"[AsyncCanceled] {operation} was cancelled");
			}
		}, TaskScheduler.FromCurrentSynchronizationContext());
	}

	/// <summary>
	/// Safely schedules an async operation with error logging for parameterized tasks.
	/// </summary>
	public static void SafeFireAndForget<T>(
		this Task<T> task,
		string operation = "Async operation")
	{
		ArgumentNullException.ThrowIfNull(task, nameof(task));

		_ = task.ContinueWith(t =>
		{
			if (t.IsFaulted && t.Exception != null)
			{
				System.Diagnostics.Debug.WriteLine(
					$"[AsyncError] {operation} failed: {t.Exception.InnerException?.Message ?? t.Exception.Message}");
			}
			else if (t.IsCanceled)
			{
				System.Diagnostics.Debug.WriteLine($"[AsyncCanceled] {operation} was cancelled");
			}
		}, TaskScheduler.FromCurrentSynchronizationContext());
	}
}
