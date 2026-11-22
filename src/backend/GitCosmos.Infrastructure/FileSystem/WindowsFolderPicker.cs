using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.FileSystem;
using Microsoft.Extensions.Logging;

namespace GitCosmos.Infrastructure.FileSystem;

public class WindowsFolderPicker(IWindowsFolderDialogFactory factory, ILogger<WindowsFolderPicker> logger) : ILocalFolderPicker
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<string?> PickAsync(string title, string confirmLabel, string? initialPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows() || !factory.IsSupported)
            throw new OperationFailedAppException(LocalizedText.Get("folder.unavailable"));
        if (!await _gate.WaitAsync(0, cancellationToken))
            throw new ConflictAppException(LocalizedText.Get("folder.busy"));
        // Capture request-local text before crossing to a native UI thread.
        var failureMessage = LocalizedText.Get("folder.error");
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() =>
        {
            string? result = null;
            Exception? failure = null;
            try
            {
                using var dialog = factory.Create();
                // WM_CLOSE is posted to this worker's dialog, without cross-apartment COM calls.
                using var closeTimer = new Timer(_ =>
                {
                    try { dialog.Close(); }
                    catch (Exception exception) { logger.LogDebug(exception, "Native folder dialog close was not available."); }
                }, null, Timeout.Infinite, Timeout.Infinite);
                using var registration = cancellationToken.Register(() =>
                {
                    completion.TrySetCanceled(cancellationToken);
                    closeTimer.Change(0, 50);
                });
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result = dialog.Show(title, confirmLabel, initialPath);
                }
                finally
                {
                    // Wait for queued close callbacks before releasing the native dialog/thread.
                    registration.Dispose();
                    closeTimer.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Native folder selection failed.");
                failure = new OperationFailedAppException(failureMessage);
            }
            finally { _gate.Release(); }

            if (cancellationToken.IsCancellationRequested) completion.TrySetCanceled(cancellationToken);
            else if (failure != null) completion.TrySetException(failure);
            else completion.TrySetResult(result);
        }) { IsBackground = true, Name = "GitCosmos folder selection" };
        try
        {
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }
        catch
        {
            _gate.Release();
            throw new OperationFailedAppException(failureMessage);
        }
        return await completion.Task;
    }
}
