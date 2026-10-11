using System.Diagnostics;
using System.Text.Json;
using KeyBridge.App.Platform;
using KeyBridge.Core;

// Only infrastructure is exercised. The profile has zero rules, so every keyboard
// event passes through and no SendInput invocation or compatibility evidence exists.
var output = Path.GetFullPath(args.Length > 0 ? args[0] : "native-smoke-results.json");
var result = new Dictionary<string, object?>
{
    ["test"] = "Native infrastructure smoke with zero transformation rules",
    ["recordedAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
    ["windowsBuild"] = Environment.OSVersion.Version.ToString(),
    ["architecture"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
    ["rules"] = 0,
    ["sendInputInvoked"] = false,
    ["compatibilityVerified"] = false,
    ["startupStateModified"] = false,
    ["userConfigurationPersisted"] = false
};
var watch = Stopwatch.StartNew();
KeyboardService? service = null;
try
{
    service = new KeyboardService();
    result["keyboardServiceInitialized"] = true;
    // Setup flags are in-memory test prerequisites only. They do not represent a
    // user's consent, physical-key confirmation, or an actual recovery rehearsal.
    var ephemeral = new AppProfile
    {
        ProfileName = "Ephemeral empty native smoke profile", Enabled = true,
        ConsentGranted = true, RoleKeyConfirmed = true, RecoveryTested = true, Rules = []
    };
    service.Configure(ephemeral);
    result["defaultEmergencyRegistered"] = service.EmergencyAvailable;
    result["defaultEmergencyNativeError"] = service.EmergencyNativeError;
    result["fallbackEmergency"] = null;
    if (!service.EmergencyAvailable)
    {
        // A collision belongs to another owner. Try bounded temporary test-only
        // alternatives; Configure unregisters only this service's own registration.
        foreach (var key in new[] { KeyCode.F11, KeyCode.F10, KeyCode.F9 })
        {
            ephemeral = ephemeral with { EmergencyPause = new(KeyModifiers.Control | KeyModifiers.Alt, key) };
            service.Configure(ephemeral);
            if (!service.EmergencyAvailable) continue;
            result["fallbackEmergency"] = ephemeral.EmergencyPause.ToString();
            break;
        }
    }
    result["emergencyRegistered"] = service.EmergencyAvailable;
    result["emergencyNativeError"] = service.EmergencyNativeError;
    result["hookResumeRequested"] = service.EmergencyAvailable;
    var resumed = service.EmergencyAvailable && service.Resume();
    result["hookInstalled"] = resumed && service.IsRunning;
    result["hookNativeError"] = service.LastNativeError;
    // Allow one actual off-hook context poll; never inject keys to probe hook health.
    await Task.Delay(300);
    var context = service.CurrentContext;
    result["foregroundContextKnown"] = context.IsKnown;
    result["foregroundPermission"] = context.Permission.ToString();
    result["inputSource"] = context.Ime.Source.ToString();
    result["inputMode"] = context.Ime.Mode.ToString();
    result["composition"] = context.Ime.Composition.ToString();
    service.Pause("Native smoke finished. No transformation rules were installed.");
    result["paused"] = !service.IsRunning;
    result["status"] = service.Status;
    var startupRead = new StartupController().GetStateAsync();
    if (await Task.WhenAny(startupRead, Task.Delay(300)) == startupRead)
    {
        var startup = await startupRead;
        result["startupReadCompleted"] = true;
        result["startupState"] = startup.Message;
    }
    else result["startupReadCompleted"] = false;
    result["result"] = !service.EmergencyAvailable ? "Emergency shortcut occupied or unavailable; hook install safely skipped."
        : resumed ? "Native initialization, registration, hook installation, context polling, and pause completed."
        : "Hook installation did not complete; see native error and status.";
}
catch (Exception error)
{
    result["result"] = "Failed";
    result["errorType"] = error.GetType().FullName;
    result["errorCode"] = error.HResult;
    Environment.ExitCode = 1;
}
finally
{
    service?.Dispose();
    result["disposed"] = true;
    result["elapsedMilliseconds"] = watch.ElapsedMilliseconds;
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
}
