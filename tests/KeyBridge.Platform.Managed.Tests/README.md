# Managed platform regressions

This executable compiles the production KeyboardService, EmergencyHotKey and lifecycle planners against managed-only NativeMethods and foreground-provider replacements. Production P/Invoke code is excluded. It exercises the real service message threads, pause/drain/resume paths, failed and partial recovery, physical state takeover, force-exit diagnostics, hotkey message identity and asynchronous confirmation tokens without installing hooks, injecting input or querying the desktop.

Run with `dotnet run --project tests/KeyBridge.Platform.Managed.Tests/KeyBridge.Platform.Managed.Tests.csproj --configuration Release`.
