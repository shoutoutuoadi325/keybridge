using KeyBridge.Core;

internal static class ProfileOperationChecks
{
    public static void Register(Action<string, Action> test)
    {
        void AsyncTest(string name, Func<Task> run) => test(name, () => run().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());
        AsyncTest("Overlapping profile edits retain both changes", async () =>
        {
            var profile = new AppProfile { Rules = [Rule("A", KeyCode.C), Rule("B", KeyCode.V)] };
            AppProfile? persisted = null;
            var firstSave = NewGate();
            var entered = NewGate();
            var queue = new ProfileOperationQueue(() => Task.CompletedTask);
            var editA = queue.RunAsync(async () =>
            {
                var next = profile with { Rules = profile.Rules.Select(r => r.Id == "A" ? r with { Enabled = true } : r).ToList() };
                entered.SetResult();
                await firstSave.Task;
                profile = next;
                persisted = next;
            });
            await entered.Task;
            var secondEntered = false;
            var editB = queue.RunAsync(() =>
            {
                secondEntered = true;
                profile = profile with { Rules = profile.Rules.Select(r => r.Id == "B" ? r with { Enabled = true } : r).ToList() };
                Check(RuleCompiler.Compile(profile).Success);
                persisted = profile;
                return Task.CompletedTask;
            });
            Check(!secondEntered && !editB.IsCompleted);
            firstSave.SetResult();
            Check(await editA && await editB);
            Check(profile.Rules.All(r => r.Enabled) && persisted!.Rules.All(r => r.Enabled));
        });
        AsyncTest("Exit during load saves loaded rules and skips queued edits", async () =>
        {
            var load = NewGate();
            var profile = AppProfile.CreateDefault();
            AppProfile? persisted = null;
            var queue = new ProfileOperationQueue(async () =>
            {
                await load.Task;
                profile = new AppProfile { ProfileName = "Keep settings", Rules = [Rule("custom-keep", KeyCode.C)] };
            });
            var initialization = queue.InitializeAsync();
            var edited = false;
            var edit = queue.RunAsync(() => { edited = true; return Task.CompletedTask; });
            var close = queue.CloseAsync(() => { persisted = profile with { PreviousSessionClean = true }; return Task.CompletedTask; });
            Check(persisted is null && !close.IsCompleted);
            load.SetResult();
            await Task.WhenAll(initialization, close);
            Check(!await edit && !edited);
            Check(persisted!.ProfileName == "Keep settings" && persisted.Rules.Single().Id == "custom-keep");
        });
        AsyncTest("Initialization is shared by overlapping operations", async () =>
        {
            var load = NewGate();
            var loads = 0;
            var operations = 0;
            var queue = new ProfileOperationQueue(async () => { loads++; await load.Task; });
            var one = queue.RunAsync(() => { operations++; return Task.CompletedTask; });
            var two = queue.RunAsync(() => { operations++; return Task.CompletedTask; });
            Check(loads == 1 && operations == 0);
            load.SetResult();
            await Task.WhenAll(one, two);
            Check(operations == 2 && loads == 1);
        });
        AsyncTest("Operation failure releases queue for subsequent recovery", async () =>
        {
            var queue = new ProfileOperationQueue(() => Task.CompletedTask);
            try { await queue.RunAsync(() => throw new IOException("Failed save")); throw new Exception("Expected failed save."); }
            catch (IOException) { }
            var recovered = false;
            Check(await queue.RunAsync(() => { recovered = true; return Task.CompletedTask; }) && recovered);
        });
        AsyncTest("Shutdown waits for active save and runs exactly once", async () =>
        {
            var save = NewGate();
            var active = false;
            var closed = 0;
            var queue = new ProfileOperationQueue(() => Task.CompletedTask);
            var edit = queue.RunAsync(async () => { active = true; await save.Task; active = false; });
            var close = queue.CloseAsync(() => { Check(!active); closed++; return Task.CompletedTask; });
            var duplicateClose = queue.CloseAsync(() => throw new Exception("Duplicate close"));
            Check(active && !close.IsCompleted && ReferenceEquals(close, duplicateClose));
            var rejected = queue.RunAsync(() => throw new Exception("Edit after close"));
            save.SetResult();
            await Task.WhenAll(edit, close, duplicateClose);
            Check(closed == 1 && !await rejected);
        });
        AsyncTest("Failed initialization never runs edits but allows cleanup", async () =>
        {
            var queue = new ProfileOperationQueue(() => Task.FromException(new IOException("Failed load")));
            try { await queue.RunAsync(() => throw new Exception("Default state must not be saved")); throw new Exception("Expected failed load."); }
            catch (IOException) { }
            var closed = false;
            await queue.CloseAsync(() => { closed = true; return Task.CompletedTask; });
            Check(closed);
        });
    }

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static void Check(bool condition) { if (!condition) throw new InvalidOperationException("Profile workflow regression."); }
    private static RuleDefinition Rule(string id, KeyCode key) => new()
    {
        Id = id, Intent = id, Source = new(KeyModifiers.Command, key), Target = new(KeyModifiers.LeftControl, key),
        Scope = new() { ProcessName = "editor.exe" }, Verification = new()
        {
            ApplicationVersion = "1", WindowsBuild = "10.0.26100.0", VerifiedContexts = [VerifiedContext.EnglishKeyboard]
        }
    };
}
