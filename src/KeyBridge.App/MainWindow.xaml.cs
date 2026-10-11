using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeyBridge.Core;
using KeyBridge.App.Platform;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace KeyBridge.App;

public partial class MainWindow : Window, IDisposable
{
    private readonly bool smoke;
    private readonly ProfileStore store;
    private readonly KeyboardService? keyboard;
    private AppProfile profile = AppProfile.CreateDefault();
    private Forms.NotifyIcon? tray;
    private bool quitting;
    private bool disposed;
    private bool initialized;
    private bool capturingRole;
    private bool trialDisplayActive;
    private bool storageBlocked;
    private AppProfile lastSavedProfile = AppProfile.CreateDefault();
    private readonly StartupController startupController = new();
    private TextBlock? roleReadout;
    private TextBlock? trialReadout;
    private TextBox? trialEditor;
    private readonly System.Windows.Threading.DispatcherTimer statusTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow(bool smokeTest = false)
    {
        smoke = smokeTest;
        InitializeComponent();
        Feedback.Text = "Version " + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.1.0") + " • Development build • English interface";
        SidebarColumn.Width = new GridLength(220 * Math.Min(App.TextScale, 1.8));
        MinWidth = Math.Min(SystemParameters.WorkArea.Width, 760 * Math.Min(App.TextScale, 1.5));
        store = new ProfileStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyBridge"));
        if (!smoke)
        {
            keyboard = new KeyboardService();
            keyboard.Changed += () => Dispatcher.BeginInvoke(new Action(UpdateStatus));
            keyboard.EmergencyPauseTriggered += () => Dispatcher.BeginInvoke(new Action(async () =>
            {
                profile = profile with { Enabled = false, RecoveryTested = true };
                await SaveAsync();
                Feedback.Text = "Emergency pause tested successfully. Mapping is paused; resume only when you are ready.";
                RenderPage();
            }));
            CreateTray();
            statusTimer.Tick += (_, _) => UpdateStatus();
            statusTimer.Start();
        }
        Loaded += async (_, _) => await InitializeAsync();
        Closing += OnClosing;
        PreviewKeyDown += OnTrialKeyDown;
        if (!smoke)
        {
            Microsoft.Win32.SystemEvents.SessionSwitch += SessionChanged;
            Microsoft.Win32.SystemEvents.PowerModeChanged += PowerChanged;
        }
    }

    private async Task InitializeAsync()
    {
        if (!smoke)
        {
            try
            {
                var loaded = await store.LoadAsync();
                profile = loaded.Profile;
                lastSavedProfile = profile;
                storageBlocked = !loaded.Loaded && loaded.RecoveryRequired;
                if (!profile.PreviousSessionClean) Feedback.Text = "The previous session did not close cleanly. Release and press any affected modifier, then test before resuming.";
                else if (!string.IsNullOrEmpty(loaded.Message)) Feedback.Text = loaded.Message;
                profile = profile with { Enabled = false, PreviousSessionClean = false };
                var startupState = await startupController.GetStateAsync();
                profile = profile with { StartAtLogin = startupState.IsEnabled };
                if (!storageBlocked) { await store.SaveAsync(profile); lastSavedProfile = profile; }
                keyboard!.Configure(profile);
            }
            catch (Exception) { Feedback.Text = "Configuration could not be loaded. Mapping remains paused; fix file access before enabling."; }
        }
        initialized = true;
        Navigation.SelectedIndex = 0;
        RenderPage();
        UpdateStatus();
    }

    private void CreateTray()
    {
        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Warning, Visible = true, Text = "KeyBridge — paused" };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open KeyBridge", null, (_, _) => Dispatcher.Invoke(OpenWindow));
        menu.Items.Add("Pause", null, (_, _) => Dispatcher.InvokeAsync(async () => await PauseAsync("Paused from the notification area.")));
        menu.Items.Add("Resume", null, (_, _) => Dispatcher.InvokeAsync(async () => await EnableAsync()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.InvokeAsync(async () => await QuitAsync()));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(OpenWindow);
    }

    private void OpenWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void NavigationChanged(object sender, SelectionChangedEventArgs e) { if (initialized) RenderPage(); }
    private void RenderPage()
    {
        if (keyboard?.IsTrial == true && Navigation.SelectedIndex != 4) keyboard.Pause("The trial ended. Mapping is paused.");
        capturingRole = false;
        trialDisplayActive = false;
        trialReadout = null;
        trialEditor = null;
        roleReadout = null;
        PageContent.Children.Clear();
        PageScroll.ScrollToTop();
        switch (Navigation.SelectedIndex)
        {
            case 1: RenderShortcuts(); break;
            case 2: RenderApplications(); break;
            case 3: RenderIme(); break;
            case 4: RenderTrial(); break;
            case 5: RenderSettings(); break;
            case 6: RenderAbout(); break;
            default: RenderHome(); break;
        }
    }

    private void RenderHome()
    {
        PageTitle.Text = "Make Windows feel familiar";
        Paragraph("Map familiar Command shortcuts to native Windows actions, one application at a time. You choose the role key and can pause at any moment.");
        Heading("1. Understand the change");
        Paragraph("KeyBridge processes keyboard events locally in real time. It does not save or upload input content, read your clipboard, install a driver, or request administrator access. A mapped shortcut replaces its original behavior in the selected application.");
        var consent = Check("I understand and agree to enable shortcut conversion when setup is complete.", profile.ConsentGranted);
        consent.Click += async (_, _) => { keyboard?.Pause("Consent changed. Mapping is paused."); profile = profile with { ConsentGranted = consent.IsChecked == true, Enabled = false }; await SaveAsync(); };
        ActionButton("Read the full privacy policy", () => Navigate(6));
        Heading("2. Choose your Command role");
        Paragraph("Left Alt is a starting point for a Windows keyboard and will replace matching Alt shortcuts. Apple Command often appears as Windows; Windows + L always stays protected. All physical keyboards share this profile. Right Alt is unavailable to protect AltGr.");
        var role = new ComboBox { ItemsSource = new[] { KeyCode.LeftAlt, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftWindows, KeyCode.RightWindows }, SelectedItem = profile.CommandKey };
        AccessibleName(role, "Command role key");
        role.SelectionChanged += async (_, _) =>
        {
            if (role.SelectedItem is not KeyCode key) return;
            await PauseAsync("Role key changed. Confirm the new key before resuming.");
            var rules = profile.Rules;
            if (key is KeyCode.LeftWindows or KeyCode.RightWindows)
            {
                rules = rules.Where(r => !(r.Source.Modifiers.HasFlag(KeyModifiers.Command) && r.Source.Key == KeyCode.L)).ToList();
                Feedback.Text = "Command + L rules were removed because Windows + L is system reserved. Use native Ctrl + L for the address bar.";
            }
            profile = ProfileChanges.WithCommandKey(profile with { Rules = rules }, key);
            await SaveAsync();
            if (roleReadout != null) roleReadout.Text = "Selected " + key + ". Press Confirm role, then press that physical key.";
        };
        PageContent.Children.Add(role);
        roleReadout = Paragraph(profile.RoleKeyConfirmed ? "Physical role confirmed: " + profile.CommandKey : "Physical key confirmation is required.");
        ActionButton("Confirm role with a physical key", () => { capturingRole = true; roleReadout.Text = "Press the selected modifier now. Capture ends after this key; nothing is stored as input history."; });
        Heading("3. Review applications and test");
        Paragraph("Presets are suggestions, disabled until you record an explicit local test for each rule. No external application or IME compatibility is certified in this development build. Start with the isolated test on Try it, then verify your chosen app before recording evidence.");
        ActionButton("Review shortcuts & local verification", () => Navigate(1));
        ActionButton("Open the local test area", () => Navigate(4));
        Heading("4. Rehearse recovery");
        Paragraph("Press " + profile.EmergencyPause + " now to test the registered emergency pause. A successful test is required before mapping starts. The notification area and Pause now button remain available. Closing this window hides it there; Exit stops the app.");
        Paragraph(profile.RecoveryTested ? "Recovery test completed. Emergency registration is checked again on resume." : "Emergency pause has not been tested yet.");
        ActionButton("Enable mapping", async () => await EnableAsync());
    }

    private void RenderShortcuts()
    {
        PageTitle.Text = "Shortcuts";
        Paragraph("Every rule has a source, a target, and an application scope. Verification applies only to the exact local context you tested. Global conversion and custom targets beyond Ctrl + letters are unavailable in this prototype. Existing native Ctrl shortcuts are preserved unless you deliberately select Ctrl as your Command role.");
        ActionButton("Add an application rule", () => OpenRuleEditor(null));
        ActionButton("Restore suggested presets…", async () =>
        {
            if (MessageBox.Show("Export a backup from Settings first if needed. Replace preset-origin rules with disabled suggestions? Your custom rules stay in place.", "Restore presets", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            await PauseAsync("Presets restored. Review and test before resuming.");
            var overrides = profile.Rules.Where(r => r.Origin != RuleOrigin.ApplicationPreset).ToList();
            var overrideIds = overrides.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
            var suggested = PresetCatalog.CreateSuggestedRules().Where(r => !overrideIds.Contains(r.Id) && !(profile.CommandKey is KeyCode.LeftWindows or KeyCode.RightWindows && r.Source.Key == KeyCode.L));
            profile = profile with { Rules = overrides.Concat(suggested).ToList() };
            await SaveAsync(); RenderPage();
        });
        foreach (var rule in profile.Rules)
        {
            var panel = Card();
            var toggle = new CheckBox { Content = new TextBlock { Text = rule.Intent + "  ·  " + (string.IsNullOrEmpty(rule.Scope.ProcessName) ? "All applications (opt in)" : rule.Scope.ProcessName), TextWrapping = TextWrapping.Wrap }, IsChecked = rule.Enabled, FontWeight = FontWeights.SemiBold };
            toggle.IsEnabled = ShortcutSupport.IsSupportedTarget(rule.Target);
            AccessibleName(toggle, "Enable " + rule.Intent + " for " + rule.Scope.ProcessName);
            toggle.Click += async (_, _) =>
            {
                var next = rule with { Enabled = toggle.IsChecked == true };
                if (!await ReplaceRuleAsync(next)) toggle.IsChecked = rule.Enabled;
                RenderPage();
            };
            panel.Children.Add(toggle);
            panel.Children.Add(Text(rule.Source + "  →  " + rule.Target));
            panel.Children.Add(Text(!ShortcutSupport.IsSupportedTarget(rule.Target)
                ? "Unsupported target · kept disabled. This version converts Ctrl + letters with optional Shift only."
                : rule.Verification.VerifiedContexts.Count == 0 ? "Unverified · disabled until a local test is recorded" : "Local evidence: " + string.Join(", ", rule.Verification.VerifiedContexts) + " · " + rule.Verification.ApplicationVersion));
            var buttons = new WrapPanel();
            buttons.Children.Add(Button("Edit & record verification", () => OpenRuleEditor(rule)));
            buttons.Children.Add(Button("Remove", async () => { await PauseAsync("Rules changed."); profile = profile with { Rules = profile.Rules.Where(r => r.Id != rule.Id).ToList() }; await SaveAsync(); RenderPage(); }));
            panel.Children.Add(buttons);
        }
    }

    private void OpenRuleEditor(RuleDefinition? existing)
    {
        var dialog = new Window { Owner = this, Title = existing == null ? "Add rule" : "Edit rule & local evidence", Width = 600, Height = 720, MinWidth = 420, MinHeight = 500, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(24) };
        dialog.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        TextBox Field(string label, string value) { panel.Children.Add(Text(label)); var input = new TextBox { Text = value }; AccessibleName(input, label); panel.Children.Add(input); return input; }
        var intent = Field("Action name", existing?.Intent ?? "Copy");
        var process = Field("Process name (for example msedge.exe; leave blank only for an opt-in global rule)", existing?.Scope.ProcessName ?? "keybridge.exe");
        var source = Field("Source (for example Command + Shift + Z)", existing?.Source.ToString() ?? "Command + C");
        var target = Field("Target (for example LeftControl + C)", existing?.Target.ToString() ?? "LeftControl + C");
        panel.Children.Add(Text("Both combinations are checked for protected system, input-method and recovery shortcuts. Navigation, single keys and IME switching remain unavailable until separately validated."));
        var ownVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).FileVersion ?? "";
        var version = Field("Tested application file version", existing?.Verification.ApplicationVersion.Length > 0 ? existing.Verification.ApplicationVersion : process.Text.Equals("keybridge.exe", StringComparison.OrdinalIgnoreCase) ? ownVersion : "");
        panel.Children.Add(Button("Select the tested application executable…", () =>
        {
            var chooseApp = new OpenFileDialog { Filter = "Windows application (*.exe)|*.exe" };
            if (chooseApp.ShowDialog(dialog) != true) return;
            process.Text = Path.GetFileName(chooseApp.FileName);
            version.Text = FileVersionInfo.GetVersionInfo(chooseApp.FileName).FileVersion ?? "";
        }));
        var build = Field("Tested Windows build", existing?.Verification.WindowsBuild ?? Environment.OSVersion.Version.ToString());
        var notes = Field("Test notes (no typed text or input history)", existing?.Verification.Notes ?? "");
        var verified = new CheckBox { Content = new TextBlock { Text = "I tested this exact rule with ENG US in this application, including modifier release and native shortcuts. Reconfirm after edits.", TextWrapping = TextWrapping.Wrap }, IsChecked = false, Margin = new Thickness(0, 12, 0, 12) };
        AccessibleName(verified, "Record local English US keyboard verification"); panel.Children.Add(verified);
        var global = new CheckBox { Content = new TextBlock { Text = "Save a disabled global proposal (conversion is unavailable pending separate validation).", TextWrapping = TextWrapping.Wrap }, IsChecked = existing?.Origin == RuleOrigin.UserGlobal }; panel.Children.Add(global);
        var errors = Text(""); panel.Children.Add(errors);
        var ruleId = existing?.Id ?? "user-" + Guid.NewGuid().ToString("N");
        RuleDefinition ReadRule(bool recordEvidence)
        {
            if (string.IsNullOrWhiteSpace(process.Text) && global.IsChecked != true) throw new InvalidOperationException("Choose an application scope or explicitly opt into a disabled global proposal.");
            var rule = new RuleDefinition
            {
                Id = ruleId, Intent = intent.Text.Trim(), Source = ParseChord(source.Text), Target = ParseChord(target.Text),
                Scope = new RuleScope { ProcessName = process.Text.Trim().ToLowerInvariant() },
                Origin = string.IsNullOrWhiteSpace(process.Text) ? RuleOrigin.UserGlobal : RuleOrigin.UserApplication,
                Enabled = existing?.Enabled == true && recordEvidence,
                Verification = new VerificationEvidence { ApplicationVersion = version.Text.Trim(), WindowsBuild = build.Text.Trim(), Notes = notes.Text.Trim(), VerifiedContexts = recordEvidence ? [VerifiedContext.EnglishKeyboard] : [] }
            };
            if (!ShortcutSupport.IsSupportedTarget(rule.Target))
                throw new InvalidOperationException("This prototype supports Ctrl + letter targets with optional Shift only. Navigation and switching targets are unavailable pending validation.");
            return rule;
        }
        panel.Children.Add(Button("Try this rule temporarily in the selected app", async () =>
        {
            try
            {
                if (smoke || storageBlocked || !profile.ConsentGranted || !profile.RoleKeyConfirmed || !profile.RecoveryTested)
                    throw new InvalidOperationException("Complete valid configuration, consent, physical role confirmation and emergency recovery on Home first.");
                var rule = ReadRule(false);
                if (rule.Origin == RuleOrigin.UserGlobal) throw new InvalidOperationException("Temporary trials require one application scope.");
                if (!await ReplaceRuleAsync(rule)) { errors.Text = "The proposal was rejected. Check the main window message."; return; }
                if (rule.Scope.ProcessName.Equals("keybridge.exe", StringComparison.OrdinalIgnoreCase))
                { dialog.Close(); Navigate(4); Feedback.Text = "Begin the isolated editor trial on this page."; return; }
                if (keyboard?.BeginRuleTrial(profile, rule.Id) == true)
                {
                    dialog.Close(); Navigation.SelectedIndex = 4; RenderTrialForApplication(rule);
                    Feedback.Text = "Temporary trial started. Switch to " + rule.Scope.ProcessName + ", test this operation, then pause and record the actual result. No evidence was created.";
                }
                else errors.Text = keyboard?.Status ?? "The trial could not start.";
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { errors.Text = ex.Message; }
        }));
        panel.Children.Add(Button("Save rule", async () =>
        {
            try
            {
                var rule = ReadRule(verified.IsChecked == true);
                if (await ReplaceRuleAsync(rule)) { dialog.Close(); RenderPage(); }
                else errors.Text = "The rule was rejected. See the message on the main window; the previous configuration is preserved.";
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { errors.Text = ex.Message; }
        }));
        dialog.ShowDialog();
    }

    private async Task<bool> ReplaceRuleAsync(RuleDefinition rule)
    {
        var rules = profile.Rules.Where(r => r.Id != rule.Id).Append(rule).ToList();
        var next = profile with { Rules = rules, Enabled = false };
        var compiled = RuleCompiler.Compile(next);
        if (!compiled.Success) { Feedback.Text = string.Join("; ", compiled.Issues.Select(i => i.Message)); return false; }
        await PauseAsync("Rules changed. Review before resuming.");
        profile = next;
        return await SaveAsync();
    }

    private void RenderApplications()
    {
        PageTitle.Text = "Applications";
        Paragraph("Suggested presets cover Microsoft Edge, Google Chrome, Visual Studio Code and the local test area. Browser redo uses Ctrl + Y; Visual Studio Code redo uses Ctrl + Shift + Z. Targets can differ by app and custom settings. Terminal presets are unavailable in this build.");
        foreach (var group in profile.Rules.GroupBy(r => r.Scope.ProcessName))
        {
            var card = Card();
            card.Children.Add(Text(string.IsNullOrEmpty(group.Key) ? "All applications · explicit opt in" : group.Key, 18, true));
            card.Children.Add(Text(group.Count(r => r.Enabled) + " enabled of " + group.Count() + " rules · " + group.Count(r => r.Verification.VerifiedContexts.Count > 0) + " with local evidence"));
            card.Children.Add(Button("Review rules", () => Navigate(1)));
        }
        Heading("Excluded applications");
        Paragraph("Exclusions take priority over every rule. Process names are matched exactly; this is a scope filter, not a security boundary.");
        var excluded = new TextBox { Text = string.Join(Environment.NewLine, profile.ExcludedApps), AcceptsReturn = true, MinHeight = 90 };
        AccessibleName(excluded, "Excluded process names, one per line"); PageContent.Children.Add(excluded);
        ActionButton("Save exclusions", async () =>
        {
            var next = profile with { ExcludedApps = excluded.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(), Enabled = false };
            if (!Accept(next)) return;
            await PauseAsync("Application exclusions changed."); profile = next; await SaveAsync(); RenderPage();
        });
    }

    private void RenderIme()
    {
        PageTitle.Text = "Input methods";
        Paragraph("Input source, internal mode and composition are separate states. ENG is different from Microsoft Pinyin in English mode. This build never infers Pinyin mode or reads composition text through another process.");
        var card = Card();
        var context = keyboard?.CurrentContext;
        card.Children.Add(Text("Foreground input context", 18, true));
        card.Children.Add(Text(context == null ? "Unknown (mapping paused)" : "Source: " + context.Ime.Source + "\nMode: " + context.Ime.Mode + "\nComposition: " + context.Ime.Composition + "\nWidth: " + context.Ime.Width + "\nPunctuation: " + context.Ime.Punctuation));
        Heading("Preserve your existing switching shortcuts");
        Paragraph("Shift, Windows + Space, Ctrl + Space, Shift + Space, Ctrl + period, Alt + Shift and Ctrl + Shift are protected. Source and target combinations are both checked. Additional protected shortcuts can be added below when you change input-method settings.");
        var protectedKeys = new TextBox { Text = string.Join(Environment.NewLine, profile.ImePolicy.AdditionalProtectedShortcuts), AcceptsReturn = true, MinHeight = 75 };
        AccessibleName(protectedKeys, "Additional protected input method shortcuts"); PageContent.Children.Add(protectedKeys);
        ActionButton("Save protected shortcuts", async () =>
        {
            try
            {
                var chords = protectedKeys.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ParseChord).ToList();
                var next = profile with { ImePolicy = profile.ImePolicy with { AdditionalProtectedShortcuts = chords }, Enabled = false };
                if (!Accept(next)) return;
                await PauseAsync("Input-method protection changed. Recheck conflicts and try your shortcuts."); profile = next; await SaveAsync();
            }
            catch (ArgumentException ex) { Feedback.Text = ex.Message; }
        });
        Heading("Microsoft Pinyin internal mode");
        Paragraph("Custom mode switching is unavailable until real Microsoft Pinyin tests pass. It would toggle the current mode; it could not guarantee Chinese or English. Existing native Shift behavior is preserved.");
        Heading("System input-source switching");
        Paragraph("Custom input-source cycling is unavailable pending validation. Windows + Space continues to cycle installed input sources in the system order. With three or more sources it is not a fixed Chinese / English toggle.");
        Heading("Conservative fallback");
        Paragraph("Only locally verified ENG US basic shortcuts are eligible in this prototype. Microsoft Pinyin, other layouts, unknown context and optional navigation pass through unchanged. This restriction is visible and is a release validation item.");
        ActionButton("Open the local test area", () => Navigate(4));
    }

    private void RenderTrial()
    {
        PageTitle.Text = "Try it safely";
        Paragraph("This editor is local and its text is never saved. Complete consent, physical role confirmation and emergency recovery setup on Home, then select ENG US and begin an isolated trial. Trial conversion runs only in this process and never records compatibility evidence automatically.");
        ActionButton("Begin isolated local trial", () =>
        {
            if (smoke) return;
            if (storageBlocked) { Feedback.Text = "Recover the invalid configuration in Settings before beginning a trial."; return; }
            if (!profile.ConsentGranted || !profile.RoleKeyConfirmed || !profile.RecoveryTested) { Navigate(0); Feedback.Text = "Complete consent, physical role confirmation and emergency recovery first."; return; }
            trialDisplayActive = keyboard?.BeginLocalTrial(profile) == true;
            Feedback.Text = keyboard?.Status ?? "The trial could not start.";
            trialEditor?.Focus(); UpdateStatus();
        });
        ActionButton("End local trial", async () => await PauseAsync("The local trial ended. Mapping is paused."));
        trialReadout = Paragraph("Begin an explicit trial and focus the editor to inspect the key it receives. Readout is cleared when focus leaves.");
        trialEditor = new TextBox { Text = "Select this sample, copy it, paste it, and undo.\nTry a Shift shortcut and release modifiers in both orders.", AcceptsReturn = true, AcceptsTab = false, TextWrapping = TextWrapping.Wrap, MinHeight = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        trialEditor.LostKeyboardFocus += (_, _) =>
        {
            trialDisplayActive = false;
            if (keyboard?.IsLocalTrial == true) keyboard.Pause("The editor lost focus. The isolated trial has ended.");
            if (trialReadout != null) trialReadout.Text = "Begin an explicit trial and focus the editor to inspect the key it receives.";
            UpdateStatus();
        };
        AccessibleName(trialEditor, "Local shortcut test editor; content is not saved"); PageContent.Children.Add(trialEditor);
        ActionButton("Clear test text", () => { trialEditor.Clear(); trialReadout.Text = "Test text cleared."; });
        Heading("Recovery rehearsal");
        Paragraph("Press " + profile.EmergencyPause + ". Mapping must pause. Release all held keys, verify native typing, then resume from Home or the notification area. After a crash, release and press any affected modifier once before continuing.");
        ActionButton("Pause & verify native input", async () => await PauseAsync("Paused for the recovery rehearsal."));
        Heading("External app checklist");
        Paragraph("Verify copy, paste, cut, select all, undo, save, find, app-specific redo and tab operations in each target version. Test both release orders, repeated keys, focus changes, the Windows role, Alt menus, Pinyin candidates, elevated windows, sleep and emergency pause. Do not record a passed context before testing it.");
    }
    private void RenderTrialForApplication(RuleDefinition rule)
    {
        PageContent.Children.Clear();
        PageTitle.Text = "Temporary application trial";
        Paragraph("Selected application: " + rule.Scope.ProcessName + "\nOperation: " + rule.Intent + "\n" + rule.Source + " → " + rule.Target);
        Paragraph("Only this one rule is temporarily active in the selected ordinary-permission application with ENG US. Switching away preserves native input. Trial evidence is never created automatically. Press the emergency pause at any point.");
        ActionButton("End trial & record the actual result", () => { keyboard?.Pause("The application trial ended."); Navigate(1); OpenRuleEditor(profile.Rules.First(r => r.Id == rule.Id)); });
        ActionButton("End trial without recording a result", async () => { await PauseAsync("The application trial ended without a recorded result."); RenderPage(); });
        UpdateStatus();
    }

    private void RenderSettings()
    {
        PageTitle.Text = "Settings";
        Heading("Profile & recovery");
        var profileName = new TextBox { Text = profile.ProfileName }; AccessibleName(profileName, "Profile name"); PageContent.Children.Add(profileName);
        var emergency = new TextBox { Text = profile.EmergencyPause.ToString() }; AccessibleName(emergency, "Emergency pause shortcut"); PageContent.Children.Add(emergency);
        ActionButton("Save profile & recovery shortcut", async () =>
        {
            try
            {
                var chord = ParseChord(emergency.Text);
                var next = profile with { ProfileName = profileName.Text.Trim(), EmergencyPause = chord, RecoveryTested = chord == profile.EmergencyPause && profile.RecoveryTested, Enabled = false };
                if (!Accept(next)) return;
                await PauseAsync("Recovery settings changed. Test the registered shortcut before resuming."); profile = next; await SaveAsync(); RenderPage();
            }
            catch (ArgumentException ex) { Feedback.Text = ex.Message; }
        });
        Heading("Start at login");
        Paragraph("Off by default. Packaged builds use the Windows startup task. Windows can disable it; KeyBridge respects that setting. Startup always opens in a safe paused state, including after an explicit pause or an interrupted session.");
        var startup = Check("Start KeyBridge when I sign in (mapping still starts paused)", profile.StartAtLogin);
        if (!smoke) _ = RefreshStartupAsync(startup);
        startup.Click += async (_, _) =>
        {
            if (smoke) return;
            try
            {
                var result = await startupController.SetEnabledAsync(startup.IsChecked == true);
                Feedback.Text = result.Message;
                profile = profile with { StartAtLogin = result.IsEnabled }; startup.IsChecked = profile.StartAtLogin; await SaveAsync();
            }
            catch (Exception) { startup.IsChecked = false; Feedback.Text = "Startup is unavailable in this unpackaged build. Install an MSIX test package to test it."; }
        };
        Heading("Import & export");
        Paragraph("Versioned JSON stores rules, process scopes and local test notes. Exports may contain application information. Imports are size-limited and strictly validated; a rejected file leaves the current profile intact. Importing never enables mapping or grants first-use consent.");
        ActionButton("Export configuration…", async () =>
        {
            var dialog = new SaveFileDialog { Filter = "KeyBridge JSON (*.json)|*.json", FileName = "KeyBridge-profile.json" };
            if (dialog.ShowDialog(this) != true) return;
            try { await store.ExportAsync(dialog.FileName, profile); Feedback.Text = "Configuration exported."; } catch (Exception) { Feedback.Text = "Export failed. Check the selected location."; }
        });
        ActionButton("Import configuration…", async () =>
        {
            var dialog = new OpenFileDialog { Filter = "KeyBridge JSON (*.json)|*.json" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                var imported = await store.ImportAsync(dialog.FileName);
                var next = imported with { Enabled = false, ConsentGranted = profile.ConsentGranted, RoleKeyConfirmed = false, RecoveryTested = false, StartAtLogin = profile.StartAtLogin, PreviousSessionClean = false };
                if (!Accept(next)) return;
                if (storageBlocked) { await store.PreserveRejectedAsync(); storageBlocked = false; }
                await PauseAsync("Imported configuration. Confirm the physical role and rehearse recovery."); profile = next; await SaveAsync(); RenderPage();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException or ProfileValidationException) { Feedback.Text = "Import rejected: " + ex.Message; }
        });
        ActionButton("Reset to safe defaults…", async () =>
        {
            if (MessageBox.Show("Export your configuration first if you need a backup. Reset all rules and setup confirmations?", "Reset KeyBridge", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try { if (storageBlocked) { await store.PreserveRejectedAsync(); storageBlocked = false; } }
            catch (Exception) { Feedback.Text = "The rejected configuration could not be preserved. Reset was cancelled; the original file remains intact."; return; }
            await PauseAsync("Reset to safe defaults.");
            profile = AppProfile.CreateDefault() with { PreviousSessionClean = false, StartAtLogin = profile.StartAtLogin };
            await SaveAsync(); RenderPage();
        });
        Heading("Language & diagnostics");
        Paragraph("English is the interface language for this build. Diagnostics contain only the app version, current engine state and Windows build. No key sequences, test text, clipboard, window titles or foreground history are exported.");
        ActionButton("Preview diagnostics", () => MessageBox.Show(Diagnostics(), "Diagnostics preview"));
        ActionButton("Export diagnostics…", () =>
        {
            var dialog = new SaveFileDialog { Filter = "Text (*.txt)|*.txt", FileName = "KeyBridge-diagnostics.txt" };
            if (dialog.ShowDialog(this) != true) return;
            try { File.WriteAllText(dialog.FileName, Diagnostics()); Feedback.Text = "Diagnostics exported."; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Feedback.Text = "Diagnostics export failed. Check access to the selected location."; }
        });
    }

    private void RenderAbout()
    {
        PageTitle.Text = "About & privacy";
        Heading("KeyBridge " + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.1.0"));
        Paragraph("A local Windows desktop shortcut bridge for people who use Mac keyboard habits. Development build for Windows 11 x64. External application compatibility and Microsoft Store certification are pending. No affiliation with Apple or Microsoft is implied.");
        Heading("Privacy policy · October 11, 2026");
        Paragraph("KeyBridge processes keyboard events and modifier state transiently on your computer to match configured shortcuts. It does not save or upload input content, accumulate ordinary key sequences as text, read clipboard content, read document or password text, read window titles, or inspect IME candidate or composition strings.");
        Paragraph("The application stores your preferences, declarative rules, chosen application process names, local verification notes and a clean-shutdown marker under your current-user app data folder. The test editor and shortcut readout exist only while the test page is open. A configuration export includes your selected applications and test notes. Import/export locations are selected by you.");
        Paragraph("Core operation makes no network requests and uses no analytics, ads, account or cloud AI service. Microsoft Store installation, update and any future purchase use Microsoft platform services independently. The application uses public Windows APIs in your user session and never requests elevation or installs a driver or service.");
        Paragraph("Pausing stops conversion; exiting releases owned input and removes the hook. Uninstalling an MSIX package removes its managed data. For an unpackaged development build, you may remove the KeyBridge folder in LocalAppData after exiting. Your manually exported files remain at the locations you selected.");
        Paragraph("Diagnostics are optional and previewable; they contain version, Windows build and engine state without input content or activity history. Publisher: Zhiqi Zhang. Privacy and support contact: zhiqizhangg@gmail.com. Contacting support is your choice; KeyBridge does not send messages automatically.");
        Paragraph("Full privacy policy: https://github.com/shoutoutuoadi325/keybridge/blob/main/docs/PRIVACY.md. A copy is included with the application as PRIVACY.md.");
        Heading("Known limits & licenses");
        Paragraph("Ordinary permission windows only. No secure desktop, Ctrl + Alt + Delete interception, Windows + L override, hardware Fn conversion, per-device rules or terminal guarantee. Modern external Pinyin mode/composition remains unknown and is passed through. Hook health is not proven by installation alone; silent timeout removal requires real Windows validation.");
        Paragraph("KeyBridge source license: GNU GPL v3 (see LICENSE). WPF and .NET are Microsoft open-source components; framework notices travel with self-contained publishing. Public API references and release checks are in the repository's docs folder.");
    }

    private async void OnTrialKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var code = (KeyCode)KeyInterop.VirtualKeyFromKey(key);
        if (capturingRole)
        {
            capturingRole = false;
            var confirmed = code == profile.CommandKey;
            profile = profile with { RoleKeyConfirmed = confirmed };
            if (roleReadout != null) roleReadout.Text = "Windows reports: " + code + (confirmed ? " · Selected role confirmed." : " · Does not match the selected role. Choose again.");
            await SaveAsync();
            e.Handled = true;
        }
        else if (trialDisplayActive && trialEditor?.IsKeyboardFocusWithin == true && trialReadout != null)
        {
            trialReadout.Text = "Key received by test editor: " + Keyboard.Modifiers + " + " + code + " · Role: " + profile.CommandKey;
        }
    }

    private bool Accept(AppProfile next)
    {
        var result = RuleCompiler.Compile(next);
        if (result.Success) return true;
        Feedback.Text = "Configuration rejected: " + string.Join("; ", result.Issues.Select(i => i.Message)); return false;
    }
    private async Task<bool> SaveAsync()
    {
        if (smoke) return true;
        if (storageBlocked) { keyboard?.Pause("The saved configuration is invalid. Explicitly import or reset in Settings to recover."); Feedback.Text = "The original invalid configuration is preserved. Import a valid file or explicitly reset in Settings before saving or enabling."; return false; }
        var candidate = profile;
        try { await store.SaveAsync(candidate); lastSavedProfile = candidate; keyboard?.Configure(candidate); UpdateStatus(); return true; }
        catch (Exception) { profile = lastSavedProfile with { Enabled = false }; keyboard?.Pause("Settings could not be saved. Mapping stopped."); Feedback.Text = "Settings could not be saved. Previous settings are preserved and mapping is paused."; return false; }
    }
    private async Task EnableAsync()
    {
        if (smoke) return;
        if (!profile.ConsentGranted || !profile.RoleKeyConfirmed || !profile.RecoveryTested)
        { OpenWindow(); Navigate(0); Feedback.Text = "Complete consent, physical role confirmation and the emergency pause test first."; return; }
        if (!profile.Rules.Any(r => r.Enabled)) { OpenWindow(); Navigate(1); Feedback.Text = "Enable a rule with recorded local verification first."; return; }
        profile = profile with { Enabled = true };
        if (!await SaveAsync()) return;
        if (!profile.Enabled) return;
        if (keyboard?.Resume() != true) { profile = profile with { Enabled = false }; await SaveAsync(); Feedback.Text = keyboard?.Status ?? "Mapping could not start."; }
        UpdateStatus();
    }
    private async Task PauseAsync(string reason)
    {
        trialDisplayActive = false;
        keyboard?.Pause(reason);
        profile = profile with { Enabled = false };
        await SaveAsync(); keyboard?.Pause(reason); UpdateStatus();
    }
    public void EmergencyStop(string reason) { keyboard?.Pause(reason); profile = profile with { Enabled = false }; }
    private async void PauseClick(object sender, RoutedEventArgs e) => await PauseAsync("Paused by you.");
    private async void ExitClick(object sender, RoutedEventArgs e) => await QuitAsync();
    private async Task QuitAsync()
    {
        if (quitting) return;
        quitting = true;
        keyboard?.Pause("Exiting KeyBridge.");
        profile = profile with { Enabled = false, PreviousSessionClean = true };
        await SaveAsync();
        Dispose(); Application.Current.Shutdown();
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (quitting || smoke) return;
        e.Cancel = true;
        if (MessageBox.Show("Closing this window keeps KeyBridge in the notification area. Use Exit KeyBridge to stop it completely. Hide the window now?", "Keep in notification area", MessageBoxButton.YesNo) == MessageBoxResult.Yes) Hide();
    }
    private void UpdateStatus()
    {
        if (disposed) return;
        var active = keyboard?.IsRunning == true;
        var context = keyboard?.CurrentContext;
        StatusText.Text = (active ? "Enabled" : "Paused") + " · " + (keyboard?.Status ?? "Mapping is off.") +
            (context == null ? "" : "\nCurrent app: " + (string.IsNullOrEmpty(context.ProcessName) ? "Unknown" : context.ProcessName) + " · Input source: " + context.Ime.Source + " · Mode: " + context.Ime.Mode + " · Composition: " + context.Ime.Composition);
        if (!smoke && keyboard?.EmergencyAvailable != true)
            StatusText.Text += "\nEmergency pause is unavailable (Windows error " + keyboard?.EmergencyNativeError + "). Choose another shortcut in Settings and test it before enabling.";
        if (active && context != null)
        {
            string scopeReason;
            if (context.Permission != PermissionState.Allowed) scopeReason = "Permission unavailable or elevated window: native input is preserved.";
            else if (context.Ime.Source != InputSource.EnglishKeyboard) scopeReason = "Input source or composition cannot be confirmed: native input is preserved.";
            else if (profile.ExcludedApps.Contains(context.ProcessName, StringComparer.OrdinalIgnoreCase)) scopeReason = "This application is excluded: native input is preserved.";
            else if (keyboard?.IsLocalTrial == true) scopeReason = "Temporary trial is limited to this KeyBridge process; no compatibility evidence is recorded.";
            else if (keyboard?.IsTrial == true) scopeReason = "Only the explicitly selected application rule is on trial; no compatibility evidence is recorded.";
            else
            {
                var effective = profile.Rules.Count(r => r.Enabled && r.Scope.ProcessName.Equals(context.ProcessName, StringComparison.OrdinalIgnoreCase)
                    && r.Verification.ApplicationVersion == context.VersionToken && r.Verification.WindowsBuild == context.WindowsBuild
                    && r.Verification.VerifiedContexts.Contains(VerifiedContext.EnglishKeyboard));
                scopeReason = effective == 0 ? "No locally verified rules match this application version and Windows build: native input is preserved." : effective + " locally verified rules are eligible in this context.";
            }
            StatusText.Text += "\n" + scopeReason;
        }
        if (tray != null) { tray.Text = active ? "KeyBridge — enabled" : "KeyBridge — paused"; tray.Icon = active ? System.Drawing.SystemIcons.Information : System.Drawing.SystemIcons.Warning; }
    }
    private async Task RefreshStartupAsync(CheckBox startup)
    {
        try
        {
            var state = await startupController.GetStateAsync();
            startup.IsChecked = state.IsEnabled;
            startup.IsEnabled = state.CanEnable;
            AutomationProperties.SetHelpText(startup, state.Message);
            startup.ToolTip = state.Message;
        }
        catch (Exception) { startup.IsChecked = false; startup.IsEnabled = false; startup.ToolTip = "Startup requires an installed MSIX package."; }
    }
    private void SessionChanged(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.SessionLogoff) Dispatcher.BeginInvoke(new Action(async () => await PauseAsync("Session locked. Resume manually when ready.")));
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend || e.Mode == PowerModes.Resume) Dispatcher.BeginInvoke(new Action(async () => await PauseAsync("Power state changed. Mapping is paused for recovery.")));
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; statusTimer.Stop(); keyboard?.Dispose();
        if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
        SystemEvents.SessionSwitch -= SessionChanged; SystemEvents.PowerModeChanged -= PowerChanged;
    }

    public async Task RenderSmokeAsync(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        // Render the real UI at desktop Store screenshot dimensions without scaling a bitmap.
        Width = 1400;
        Height = 900;
        UpdateLayout();
        for (var page = 0; page < 7; page++)
        {
            Navigation.SelectedIndex = page; RenderPage(); UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var surface = (FrameworkElement)Content;
            var dpi = VisualTreeHelper.GetDpi(surface);
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(surface.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32); bitmap.Render(surface);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(outputDirectory, "page-" + page + ".png")); png.Save(stream);
        }
        File.WriteAllText(Path.Combine(outputDirectory, "result.txt"), "PASS: all seven pages constructed and rendered without installing a hook, registering shortcuts, touching profile storage or processing global input.");
    }
    private string Diagnostics() => "KeyBridge " + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.1.0") + "\nWindows build: " + Environment.OSVersion.Version + "\nMapping enabled: " + (keyboard?.IsRunning == true) + "\nEmergency registered: " + (keyboard?.EmergencyAvailable == true) + "\nHook native error: " + (keyboard?.LastNativeError ?? 0) + "\nEmergency native error: " + (keyboard?.EmergencyNativeError ?? 0) + "\nNo input content, key sequences or application history included.\n";
    private void Navigate(int page) { Navigation.SelectedIndex = page; RenderPage(); }
    private TextBlock Paragraph(string content) { var text = Text(content); text.Margin = new Thickness(0, 0, 0, 14); PageContent.Children.Add(text); return text; }
    private void Heading(string title) { var text = Text(title, 20, true); text.Margin = new Thickness(0, 20, 0, 12); PageContent.Children.Add(text); }
    private static TextBlock Text(string content, double size = 14, bool bold = false) => new() { Text = content, TextWrapping = TextWrapping.Wrap, FontSize = size * App.TextScale, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, 0, 0, 8) };
    private StackPanel Card() { var panel = new StackPanel(); PageContent.Children.Add(new Border { Child = panel, Padding = new Thickness(18), Margin = new Thickness(0, 10, 0, 8), BorderThickness = new Thickness(1), BorderBrush = SystemColors.ControlDarkBrush }); return panel; }
    private CheckBox Check(string content, bool selected) { var box = new CheckBox { Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap }, IsChecked = selected }; AccessibleName(box, content); PageContent.Children.Add(box); return box; }
    private static Button Button(string title, Action action) { var button = new Button { Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap } }; AccessibleName(button, title); button.Click += (_, _) => action(); return button; }
    private void ActionButton(string title, Action action) => PageContent.Children.Add(Button(title, action));
    private static void AccessibleName(DependencyObject control, string name) => AutomationProperties.SetName(control, name);
    private static KeyChord ParseChord(string value)
    {
        var tokens = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) throw new ArgumentException("Enter a keyboard combination.");
        var modifiers = KeyModifiers.None;
        for (var i = 0; i < tokens.Length - 1; i++)
        {
            if (!Enum.TryParse<KeyModifiers>(tokens[i], true, out var modifier) || !Enum.IsDefined(modifier) || modifier == KeyModifiers.None) throw new ArgumentException("Unknown modifier: " + tokens[i]);
            modifiers |= modifier;
        }
        if (!Enum.TryParse<KeyCode>(tokens[^1], true, out var key) || !Enum.IsDefined(key)) throw new ArgumentException("Unknown key: " + tokens[^1]);
        return new KeyChord(modifiers, key);
    }
}
