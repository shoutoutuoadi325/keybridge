using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace KeyBridge.App.Platform;

public sealed record StartupStatus(bool IsEnabled, bool CanEnable, string Message);

/// <summary>Call SetEnabledAsync(true) only after an explicit user startup choice.</summary>
public sealed class StartupController
{
    public const string TaskId = "KeyBridgeStartup";
    public async Task<StartupStatus> GetStateAsync()
    {
        try { return Describe((await StartupTask.GetAsync(TaskId)).State); }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException)
        { return new(false, false, "Startup is available only in the installed MSIX package."); }
    }
    public async Task<StartupStatus> SetEnabledAsync(bool enabled)
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (!enabled)
            {
                if (task.State is StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy)
                    return Describe(task.State);
                task.Disable();
                return Describe(task.State);
            }
            if (task.State is StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy)
                return Describe(task.State);
            return Describe(task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy
                ? task.State : await task.RequestEnableAsync());
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException)
        { return new(false, false, "Startup is available only in the installed MSIX package."); }
    }
    private static StartupStatus Describe(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => new(true, true, "Login startup is enabled. Saved pause state is preserved."),
        StartupTaskState.EnabledByPolicy => new(true, false, "Login startup is enabled by Windows policy."),
        StartupTaskState.DisabledByUser => new(false, false, "Windows has disabled startup. Re-enable it in Windows Startup settings."),
        StartupTaskState.DisabledByPolicy => new(false, false, "Login startup is disabled by Windows policy."),
        _ => new(false, true, "Login startup is disabled.")
    };
}
