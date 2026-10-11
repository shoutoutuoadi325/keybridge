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
    private readonly ProfileOperationQueue profileOperations;
    private long pauseGeneration;
    private long trialGeneration;
    private TextBlock? roleReadout;
    private TextBlock? trialReadout;
    private TextBox? trialEditor;
    private readonly System.Windows.Threading.DispatcherTimer statusTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow(bool smokeTest = false)
    {
        smoke = smokeTest;
        InitializeComponent();
        Feedback.Text = "Version " + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.1.0") + " • English";
        SidebarColumn.Width = new GridLength(220 * Math.Min(App.TextScale, 1.8));
        MinWidth = Math.Min(SystemParameters.WorkArea.Width, 760 * Math.Min(App.TextScale, 1.5));
        store = new ProfileStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyBridge"));
        profileOperations = new ProfileOperationQueue(InitializeAsync);
        if (!smoke)
        {
            keyboard = new KeyboardService();
            keyboard.Changed += () => Dispatcher.BeginInvoke(new Action(UpdateStatus));
            keyboard.EmergencyPauseTriggered += registration => Dispatcher.BeginInvoke(new Action(async () =>
            {
                RequestPause("Emergency pause. Resume must be chosen explicitly.");
                await RunProfileOperationAsync(async () =>
                {
                    if (registration.Chord != profile.EmergencyPause || !keyboard.IsCurrentEmergencyRegistration(registration)) return;
                    profile = profile with { Enabled = false, RecoveryTested = true };
                    if (await SaveAsync()) Feedback.Text = "Emergency pause tested successfully. Mapping is paused; resume only when you are ready.";
                    RenderPage();
                });
            }));
            CreateTray();
            statusTimer.Tick += (_, _) => UpdateStatus();
            statusTimer.Start();
        }
        Loaded += async (_, _) => await profileOperations.InitializeAsync();
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
                if (!string.IsNullOrEmpty(loaded.Message)) Feedback.Text = loaded.Message;
                profile = profile with { Enabled = false, PreviousSessionClean = false };
                var startupState = await startupController.GetStateAsync();
                profile = profile with { StartAtLogin = startupState.IsEnabled };
                if (!storageBlocked) { await store.SaveAsync(profile); lastSavedProfile = profile; }
                keyboard!.Configure(profile);
            }
            catch (Exception) { storageBlocked = true; Feedback.Text = "Configuration could not be loaded. Mapping remains paused; import or reset in Settings to recover."; }
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
        trialGeneration++;
        if (keyboard?.IsTrial == true && Navigation.SelectedIndex != 4) RequestPause("The trial ended. Mapping is paused.");
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
        consent.Click += async (_, _) =>
        {
            var granted = consent.IsChecked == true;
            RequestPause("Consent changed. Mapping is paused.");
            await RunProfileOperationAsync(async () => { profile = profile with { ConsentGranted = granted, Enabled = false }; await SaveAsync(); });
        };
        ActionButton("Read the full privacy policy", () => Navigate(6));
        Heading("2. Choose your Command role");
        Paragraph("Left Alt is a starting point for a Windows keyboard and will replace matching Alt shortcuts. Apple Command often appears as Windows; Windows + L always stays protected. All physical keyboards share this profile. Right Alt is unavailable to protect AltGr.");
        var role = new ComboBox { ItemsSource = new[] { KeyCode.LeftAlt, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftWindows, KeyCode.RightWindows }, SelectedItem = profile.CommandKey };
        AccessibleName(role, "Command role key");
        role.SelectionChanged += async (_, _) =>
        {
            if (role.SelectedItem is not KeyCode key) return;
            RequestPause("Role key changed. Confirm the new key before resuming.");
            await RunProfileOperationAsync(async () =>
            {
                var rules = profile.Rules;
                if (key is KeyCode.LeftWindows or KeyCode.RightWindows)
                {
                    rules = rules.Where(r => !(r.Source.Modifiers.HasFlag(KeyModifiers.Command) && r.Source.Key == KeyCode.L)).ToList();
                    Feedback.Text = "Command + L rules were removed because Windows + L is system reserved. Use native Ctrl + L for the address bar.";
                }
                profile = ProfileChanges.WithCommandKey(profile with { Rules = rules }, key);
                await SaveAsync();
                if (roleReadout != null) roleReadout.Text = "Selected " + key + ". Press Confirm role, then press that physical key.";
            });
        };
        PageContent.Children.Add(role);
        roleReadout = Paragraph(profile.RoleKeyConfirmed ? "Physical role confirmed: " + profile.CommandKey : "Physical key confirmation is required.");
        ActionButton("Confirm role with a physical key", () => { capturingRole = true; roleReadout.Text = "Press the selected modifier now. Capture ends after this key; nothing is stored as input history."; });
        Heading("3. Set up your shortcuts");
        Paragraph("Choose shortcuts for each application. Use Try it to preview familiar actions, then confirm your shortcut settings before enabling them.");
        ActionButton("Review & confirm shortcuts", () => Navigate(1));
        ActionButton("Open the shortcut editor", () => Navigate(4));
        Heading("4. Rehearse recovery");
        Paragraph("Press " + profile.EmergencyPause + " now to test the registered emergency pause. A successful test is required before mapping starts. The notification area and Pause now button remain available. Closing this window hides it there; Exit stops the app.");
        Paragraph(profile.RecoveryTested ? "Emergency pause confirmed. KeyBridge checks its registration whenever you resume." : "Press your emergency pause shortcut to complete setup.");
        ActionButton("Enable mapping", async () => await EnableAsync());
    }

    private void RenderShortcuts()
    {
        PageTitle.Text = "Shortcuts";
        Paragraph("Each shortcut connects a source combination to a Ctrl + letter action, optionally with Shift, in your selected application. Confirm the shortcut for your application and Windows versions. Existing Ctrl shortcuts remain available unless you select Ctrl as your Command role.");
        ActionButton("Add an application rule", () => OpenRuleEditor(null));
        ActionButton("Restore suggested presets…", async () =>
        {
            if (MessageBox.Show("Export a backup from Settings first if needed. Replace preset-origin rules with disabled suggestions? Your custom rules stay in place.", "Restore presets", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            RequestPause("Presets restored. Review and test before resuming.");
            await RunProfileOperationAsync(async () =>
            {
                var overrides = profile.Rules.Where(r => r.Origin != RuleOrigin.ApplicationPreset).ToList();
                var overrideIds = overrides.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
                var suggested = PresetCatalog.CreateSuggestedRules().Where(r => !overrideIds.Contains(r.Id) && !(profile.CommandKey is KeyCode.LeftWindows or KeyCode.RightWindows && r.Source.Key == KeyCode.L));
                profile = profile with { Rules = overrides.Concat(suggested).ToList(), Enabled = false };
                await SaveAsync(); RenderPage();
            });
        });
        foreach (var rule in profile.Rules)
        {
            var panel = Card();
            var toggle = new CheckBox { Content = new TextBlock { Text = rule.Intent + "  ·  " + (string.IsNullOrEmpty(rule.Scope.ProcessName) ? "All applications (opt in)" : rule.Scope.ProcessName), TextWrapping = TextWrapping.Wrap }, IsChecked = rule.Enabled, FontWeight = FontWeights.SemiBold };
            toggle.IsEnabled = ShortcutSupport.IsSupportedTarget(rule.Target);
            AccessibleName(toggle, "Enable " + rule.Intent + " for " + rule.Scope.ProcessName);
            toggle.Click += async (_, _) =>
            {
                if (!await SetRuleEnabledAsync(rule.Id, toggle.IsChecked == true)) toggle.IsChecked = rule.Enabled;
                RenderPage();
            };
            panel.Children.Add(toggle);
            panel.Children.Add(Text(rule.Source + "  →  " + rule.Target));
            panel.Children.Add(Text(!ShortcutSupport.IsSupportedTarget(rule.Target)
                ? "Choose a Ctrl + letter target, optionally with Shift, to enable this shortcut."
                : rule.Verification.VerifiedContexts.Count == 0 ? "Confirm this shortcut before enabling" : "Confirmed settings: " + string.Join(", ", rule.Verification.VerifiedContexts) + " · " + rule.Verification.ApplicationVersion));
            var buttons = new WrapPanel();
            buttons.Children.Add(Button("Edit & confirm shortcut", () => OpenRuleEditor(rule)));
            buttons.Children.Add(Button("Remove", async () =>
            {
                RequestPause("Rules changed.");
                await RunProfileOperationAsync(async () => { profile = profile with { Rules = profile.Rules.Where(r => r.Id != rule.Id).ToList(), Enabled = false }; await SaveAsync(); RenderPage(); });
            }));
            panel.Children.Add(buttons);
        }
    }

    private void OpenRuleEditor(RuleDefinition? existing)
    {
        var dialog = new Window { Owner = this, Title = existing == null ? "Add rule" : "Edit shortcut", Width = 600, Height = 720, MinWidth = 420, MinHeight = 500, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var editorOpen = true;
        dialog.Closed += (_, _) => editorOpen = false;
        var panel = new StackPanel { Margin = new Thickness(24) };
        dialog.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        TextBox Field(string label, string value) { panel.Children.Add(Text(label)); var input = new TextBox { Text = value }; AccessibleName(input, label); panel.Children.Add(input); return input; }
        var intent = Field("Action name", existing?.Intent ?? "Copy");
        var process = Field("Application process name (for example msedge.exe)", existing?.Scope.ProcessName ?? "keybridge.exe");
        var source = Field("Source (for example Command + Shift + Z)", existing?.Source.ToString() ?? "Command + C");
        var target = Field("Target (for example LeftControl + C)", existing?.Target.ToString() ?? "LeftControl + C");
        panel.Children.Add(Text("KeyBridge checks both combinations for conflicts with protected system, input-method and emergency pause shortcuts. Choose Ctrl + letter actions, optionally with Shift."));
        var ownVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).FileVersion ?? "";
        var version = Field("Application file version", existing?.Verification.ApplicationVersion.Length > 0 ? existing.Verification.ApplicationVersion : process.Text.Equals("keybridge.exe", StringComparison.OrdinalIgnoreCase) ? ownVersion : "");
        var chooseApplication = Button("Select the application executable…", () =>
        {
            var chooseApp = new OpenFileDialog { Filter = "Windows application (*.exe)|*.exe" };
            if (chooseApp.ShowDialog(dialog) != true) return;
            process.Text = Path.GetFileName(chooseApp.FileName);
            version.Text = FileVersionInfo.GetVersionInfo(chooseApp.FileName).FileVersion ?? "";
        });
        panel.Children.Add(chooseApplication);
        var build = Field("Windows build", RuleEditorChanges.InitialWindowsBuild(existing?.Verification, Environment.OSVersion.Version.ToString()));
        var notes = Field("Shortcut notes (no typed text or input history)", existing?.Verification.Notes ?? "");
        var verified = new CheckBox { Content = new TextBlock { Text = "I confirm this shortcut works as intended with ENG US in the selected application. Confirm again after changing these settings.", TextWrapping = TextWrapping.Wrap }, IsChecked = false, Margin = new Thickness(0, 12, 0, 12) };
        AccessibleName(verified, "Confirm this application's ENG US shortcut"); panel.Children.Add(verified);
        foreach (var field in new[] { process, source, target, version, build })
            field.TextChanged += (_, _) => verified.IsChecked = false;
        var global = new CheckBox { Content = new TextBlock { Text = "Save for all applications (kept disabled; choose an application to enable).", TextWrapping = TextWrapping.Wrap }, IsChecked = existing?.Origin == RuleOrigin.UserGlobal }; panel.Children.Add(global);
        AccessibleName(global, "Save a disabled rule for all applications");
        void UpdateScopeFields()
        {
            var applicationScope = global.IsChecked != true;
            process.IsEnabled = applicationScope;
            version.IsEnabled = applicationScope;
            chooseApplication.IsEnabled = applicationScope;
            verified.IsEnabled = applicationScope;
            verified.IsChecked = false;
        }
        global.Checked += (_, _) => UpdateScopeFields();
        global.Unchecked += (_, _) => UpdateScopeFields();
        UpdateScopeFields();
        var errors = Text(""); panel.Children.Add(errors);
        var ruleId = existing?.Id ?? "user-" + Guid.NewGuid().ToString("N");
        RuleDefinition ReadRule(bool recordEvidence)
        {
            var proposal = new RuleDefinition
            {
                Id = ruleId, Intent = intent.Text.Trim(), Source = ParseChord(source.Text), Target = ParseChord(target.Text),
                Scope = new RuleScope { ProcessName = process.Text.Trim().ToLowerInvariant() },
                Verification = new VerificationEvidence { ApplicationVersion = version.Text.Trim(), WindowsBuild = build.Text.Trim(), Notes = notes.Text.Trim() }
            };
            var rule = RuleEditorChanges.FromProposal(proposal, global.IsChecked == true, existing?.Enabled == true, recordEvidence, existing: existing);
            if (!ShortcutSupport.IsSupportedTarget(rule.Target))
                throw new InvalidOperationException("Choose a Ctrl + letter target, optionally with Shift.");
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
                if (!await ReplaceRuleAsync(rule, startApplicationTrial: () => editorOpen)) { errors.Text = "The proposal was rejected. Check the main window message."; return; }
                if (!editorOpen || quitting) return;
                if (rule.Scope.ProcessName.Equals("keybridge.exe", StringComparison.OrdinalIgnoreCase))
                { dialog.Close(); Navigate(4); Feedback.Text = "Begin the isolated editor trial on this page."; return; }
                if (keyboard?.IsTrial == true)
                {
                    dialog.Close(); Navigation.SelectedIndex = 4; RenderTrialForApplication(rule);
                    Feedback.Text = "Shortcut preview started. Switch to " + rule.Scope.ProcessName + ", try the shortcut, then pause and confirm your settings.";
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

    private async Task<bool> ReplaceRuleAsync(RuleDefinition rule, Func<bool>? startApplicationTrial = null)
    {
        RequestPause("Rules changed. Review before resuming.");
        var requestGeneration = pauseGeneration;
        var safetyGeneration = keyboard?.SafetyPauseGeneration ?? 0;
        var saved = false;
        await RunProfileOperationAsync(async () =>
        {
            saved = await PersistRuleAsync(rule);
            if (saved && startApplicationTrial?.Invoke() == true && requestGeneration == pauseGeneration && !quitting
                && !rule.Scope.ProcessName.Equals("keybridge.exe", StringComparison.OrdinalIgnoreCase))
                keyboard?.BeginRuleTrial(profile, rule.Id, safetyGeneration);
        });
        return saved;
    }

    private async Task<bool> SetRuleEnabledAsync(string id, bool enabled)
    {
        RequestPause("Rules changed. Review before resuming.");
        var saved = false;
        await RunProfileOperationAsync(async () =>
        {
            var current = profile.Rules.FirstOrDefault(rule => rule.Id == id);
            if (current != null) saved = await PersistRuleAsync(current with { Enabled = enabled });
        });
        return saved;
    }

    // Called only while profileOperations owns the complete mutation/save workflow.
    private async Task<bool> PersistRuleAsync(RuleDefinition rule)
    {
        var rules = profile.Rules.Where(r => r.Id != rule.Id).Append(rule).ToList();
        var next = profile with { Rules = rules, Enabled = false };
        if (!Accept(next)) return false;
        profile = next;
        return await SaveAsync();
    }

    private void RenderApplications()
    {
        PageTitle.Text = "Applications";
        Paragraph("Presets include Microsoft Edge, Google Chrome, Visual Studio Code and the KeyBridge editor. Browser redo uses Ctrl + Y; Visual Studio Code redo uses Ctrl + Shift + Z. Customize targets to match each application's settings.");
        foreach (var group in profile.Rules.GroupBy(r => r.Scope.ProcessName))
        {
            var card = Card();
            card.Children.Add(Text(string.IsNullOrEmpty(group.Key) ? "All applications · explicit opt in" : group.Key, 18, true));
            card.Children.Add(Text(group.Count(r => r.Enabled) + " enabled of " + group.Count() + " rules · " + group.Count(r => r.Verification.VerifiedContexts.Count > 0) + " confirmed"));
            card.Children.Add(Button("Review rules", () => Navigate(1)));
        }
        Heading("Excluded applications");
        Paragraph("Exclusions take priority over every rule. Process names are matched exactly; this is a scope filter, not a security boundary.");
        var excluded = new TextBox { Text = string.Join(Environment.NewLine, profile.ExcludedApps), AcceptsReturn = true, MinHeight = 90 };
        AccessibleName(excluded, "Excluded process names, one per line"); PageContent.Children.Add(excluded);
        ActionButton("Save exclusions", async () =>
        {
            var processes = excluded.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            RequestPause("Application exclusions changed.");
            await RunProfileOperationAsync(async () =>
            {
                var next = profile with { ExcludedApps = processes, Enabled = false };
                if (!Accept(next)) return;
                profile = next; await SaveAsync(); RenderPage();
            });
        });
    }

    private void RenderIme()
    {
        PageTitle.Text = "Input methods";
        Paragraph("KeyBridge works with the ENG US keyboard layout. Microsoft Pinyin keeps its native input behavior, including when Pinyin is in English mode. KeyBridge does not read candidate or composition text.");
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
                RequestPause("Input-method protection changed. Recheck conflicts and try your shortcuts.");
                await RunProfileOperationAsync(async () =>
                {
                    var next = profile with { ImePolicy = profile.ImePolicy with { AdditionalProtectedShortcuts = chords }, Enabled = false };
                    if (!Accept(next)) return;
                    profile = next; await SaveAsync();
                });
            }
            catch (ArgumentException ex) { Feedback.Text = ex.Message; }
        });
        Heading("Microsoft Pinyin internal mode");
        Paragraph("Use Microsoft Pinyin's native controls to switch between Chinese and English. Your existing Shift behavior is preserved.");
        Heading("System input-source switching");
        Paragraph("Use Windows + Space to cycle through installed input sources in the order configured in Windows.");
        Heading("Native input behavior");
        Paragraph("Confirmed shortcuts apply with ENG US in their selected applications. Microsoft Pinyin, other layouts and contexts outside those settings retain native input behavior.");
        ActionButton("Open the shortcut editor", () => Navigate(4));
    }

    private void RenderTrial()
    {
        PageTitle.Text = "Try it safely";
        Paragraph("Preview shortcuts in this local editor; its text stays in memory. Complete setup on Home, select ENG US, then begin a preview. Shortcut conversion stays within this editor.");
        ActionButton("Begin isolated local trial", async () =>
        {
            if (smoke) return;
            var requestGeneration = pauseGeneration;
            var safetyGeneration = keyboard?.SafetyPauseGeneration ?? 0;
            var requestTrialGeneration = trialGeneration;
            var editor = trialEditor;
            await RunProfileOperationAsync(() =>
            {
                if (requestGeneration != pauseGeneration || requestTrialGeneration != trialGeneration
                    || Navigation.SelectedIndex != 4 || editor == null || !ReferenceEquals(editor, trialEditor)) return Task.CompletedTask;
                if (storageBlocked) { Feedback.Text = "Recover the invalid configuration in Settings before beginning a trial."; return Task.CompletedTask; }
                if (!profile.ConsentGranted || !profile.RoleKeyConfirmed || !profile.RecoveryTested) { Navigate(0); Feedback.Text = "Complete consent, physical role confirmation and emergency recovery first."; return Task.CompletedTask; }
                trialDisplayActive = keyboard?.BeginLocalTrial(profile, safetyGeneration) == true;
                Feedback.Text = keyboard?.Status ?? "The trial could not start.";
                trialEditor?.Focus(); UpdateStatus();
                return Task.CompletedTask;
            });
        });
        ActionButton("End local trial", async () => await PauseAsync("The local trial ended. Mapping is paused."));
        trialReadout = Paragraph("Begin an explicit trial and focus the editor to inspect the key it receives. Readout is cleared when focus leaves.");
        trialEditor = new TextBox { Text = "Select this sample, copy it, paste it, and undo.\nTry a Shift shortcut and release modifiers in both orders.", AcceptsReturn = true, AcceptsTab = false, TextWrapping = TextWrapping.Wrap, MinHeight = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        trialEditor.LostKeyboardFocus += (_, _) =>
        {
            trialDisplayActive = false;
            if (keyboard?.IsLocalTrial == true) RequestPause("The editor lost focus. The isolated trial has ended.");
            if (trialReadout != null) trialReadout.Text = "Begin an explicit trial and focus the editor to inspect the key it receives.";
            UpdateStatus();
        };
        AccessibleName(trialEditor, "Local shortcut test editor; content is not saved"); PageContent.Children.Add(trialEditor);
        ActionButton("Clear test text", () => { trialEditor.Clear(); trialReadout.Text = "Test text cleared."; });
        Heading("Recovery rehearsal");
        Paragraph("Press " + profile.EmergencyPause + ". Mapping must pause. Release all held keys, verify native typing, then resume from Home or the notification area. After a crash, release and press any affected modifier once before continuing.");
        ActionButton("Pause & verify native input", async () => await PauseAsync("Paused for the recovery rehearsal."));
        Heading("Application shortcuts");
        Paragraph("Choose a target application in Shortcuts and preview your selected action. Match its keyboard settings, then finish the preview and confirm the shortcut. Pause is available from the window, notification area or your emergency shortcut.");
    }
    private void RenderTrialForApplication(RuleDefinition rule)
    {
        PageContent.Children.Clear();
        PageTitle.Text = "Temporary application trial";
        Paragraph("Selected application: " + rule.Scope.ProcessName + "\nOperation: " + rule.Intent + "\n" + rule.Source + " → " + rule.Target);
        Paragraph("This preview activates one shortcut in the selected ordinary-permission application with ENG US. Switching away preserves native input. Use your emergency pause shortcut at any time.");
        ActionButton("Finish & confirm shortcut", () =>
        {
            RequestPause("The application trial ended."); Navigate(1);
            var current = profile.Rules.FirstOrDefault(r => r.Id == rule.Id);
            if (current != null) OpenRuleEditor(current);
        });
        ActionButton("End preview", async () => { await PauseAsync("The shortcut preview ended."); RenderPage(); });
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
                var name = profileName.Text.Trim();
                RequestPause("Recovery settings changed. Test the registered shortcut before resuming.");
                await RunProfileOperationAsync(async () =>
                {
                    var next = profile with { ProfileName = name, EmergencyPause = chord, RecoveryTested = chord == profile.EmergencyPause && profile.RecoveryTested, Enabled = false };
                    if (!Accept(next)) return;
                    profile = next; await SaveAsync(); RenderPage();
                });
            }
            catch (ArgumentException ex) { Feedback.Text = ex.Message; }
        });
        Heading("Start at login");
        Paragraph("Off by default. Login startup uses the Windows startup task and respects your Windows settings. KeyBridge starts paused so you choose when conversion resumes.");
        var startup = Check("Start KeyBridge when I sign in (mapping still starts paused)", profile.StartAtLogin);
        if (!smoke) _ = RefreshStartupAsync(startup);
        startup.Click += async (_, _) =>
        {
            if (smoke) return;
            var enabled = startup.IsChecked == true;
            await RunProfileOperationAsync(async () =>
            {
                try
                {
                    var result = await startupController.SetEnabledAsync(enabled);
                    Feedback.Text = result.Message;
                    profile = profile with { StartAtLogin = result.IsEnabled }; startup.IsChecked = profile.StartAtLogin; await SaveAsync();
                }
                catch (Exception) { startup.IsChecked = false; Feedback.Text = "Install KeyBridge through Microsoft Store to use login startup."; }
            });
        };
        Heading("Import & export");
        Paragraph("Versioned JSON stores shortcuts, application scopes and your notes. Exports may contain application information. Imports are size-limited and strictly validated; a rejected file leaves the current profile intact. Importing never enables mapping or grants first-use consent.");
        ActionButton("Export configuration…", async () =>
        {
            var dialog = new SaveFileDialog { Filter = "KeyBridge JSON (*.json)|*.json", FileName = "KeyBridge-profile.json" };
            if (dialog.ShowDialog(this) != true) return;
            await RunProfileOperationAsync(async () =>
            {
                try { await store.ExportAsync(dialog.FileName, profile); Feedback.Text = "Configuration exported."; } catch (Exception) { Feedback.Text = "Export failed. Check the selected location."; }
            });
        });
        ActionButton("Import configuration…", async () =>
        {
            var dialog = new OpenFileDialog { Filter = "KeyBridge JSON (*.json)|*.json" };
            if (dialog.ShowDialog(this) != true) return;
            RequestPause("Imported configuration. Confirm the physical role and rehearse recovery.");
            await RunProfileOperationAsync(async () =>
            {
                try
                {
                    var imported = await store.ImportAsync(dialog.FileName);
                    var next = imported with { Enabled = false, ConsentGranted = profile.ConsentGranted, RoleKeyConfirmed = false, RecoveryTested = false, StartAtLogin = profile.StartAtLogin, PreviousSessionClean = false };
                    if (!Accept(next)) return;
                    await ReplaceStoredProfileAsync(next); RenderPage();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException or ProfileValidationException) { Feedback.Text = "Import rejected: " + ex.Message; }
            });
        });
        ActionButton("Reset to safe defaults…", async () =>
        {
            if (MessageBox.Show("Export your configuration first if you need a backup. Reset all rules and setup confirmations?", "Reset KeyBridge", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            RequestPause("Reset to safe defaults.");
            await RunProfileOperationAsync(async () =>
            {
                try { await ReplaceStoredProfileAsync(AppProfile.CreateDefault() with { PreviousSessionClean = false, StartAtLogin = profile.StartAtLogin }); RenderPage(); }
                catch (Exception) { Feedback.Text = "The rejected configuration could not be preserved. Reset was cancelled; the original file remains intact."; }
            });
        });
        Heading("Language & diagnostics");
        Paragraph("KeyBridge's interface is in English. Diagnostics contain only the app version, current engine state and Windows build. No key sequences, test text, clipboard, window titles or foreground history are exported.");
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
        Paragraph("Familiar shortcuts for your Windows desktop. KeyBridge lets you personalize application shortcuts on Windows 11 x64, with local processing and convenient pause controls. No affiliation with Apple or Microsoft is implied.");
        Heading("Privacy policy · October 11, 2026");
        Paragraph("KeyBridge processes keyboard events and modifier state transiently on your computer to match configured shortcuts. It does not save or upload input content, accumulate ordinary key sequences as text, read clipboard content, read document or password text, read window titles, or inspect IME candidate or composition strings.");
        Paragraph("The application stores your preferences, declarative rules, chosen application process names, shortcut confirmation notes and a clean-shutdown marker under your current-user app data folder. The test editor and shortcut readout exist only while the test page is open. A configuration export includes your selected applications and test notes. Import/export locations are selected by you.");
        Paragraph("Core operation makes no network requests and uses no analytics, ads, account or cloud AI service. Microsoft Store installation, update and any future purchase use Microsoft platform services independently. The application uses public Windows APIs in your user session and never requests elevation or installs a driver or service.");
        Paragraph("Pausing stops conversion. KeyBridge releases its synthesized input and restores held modifiers; if Windows cannot complete recovery, mapping stays paused and the status identifies the keys to press and release. Exiting removes the hook. Uninstall KeyBridge through Windows Settings to remove the application and its managed data. Your manually exported files remain at the locations you selected.");
        Paragraph("Diagnostics are optional and previewable; they contain version, Windows build and engine state without input content or activity history. Publisher: Zhiqi Zhang. Privacy and support contact: zhiqizhangg@gmail.com. Contacting support is your choice; KeyBridge does not send messages automatically.");
        Paragraph("Full privacy policy: https://github.com/shoutoutuoadi325/keybridge/blob/main/docs/PRIVACY.md. A copy is included with the application as PRIVACY.md.");
        Heading("Supported operation & licenses");
        Paragraph("Application shortcuts use Ctrl + letter targets, optionally with Shift, with ENG US in ordinary-permission windows. Windows security shortcuts and input-method controls keep their native behavior. Your keyboard role and application settings are shared across connected keyboards.");
        Paragraph("KeyBridge source license: GNU GPL v3 (see LICENSE). WPF and .NET are Microsoft open-source components; framework notices travel with self-contained publishing. Public API references and release checks are in the repository's docs folder.");
    }

    private async void OnTrialKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var code = (KeyCode)KeyInterop.VirtualKeyFromKey(key);
        if (capturingRole)
        {
            capturingRole = false;
            e.Handled = true;
            var selectedRole = profile.CommandKey;
            RequestPause("Physical role confirmation. Mapping is paused.");
            await RunProfileOperationAsync(async () =>
            {
                var confirmed = selectedRole == profile.CommandKey && code == profile.CommandKey;
                profile = profile with { RoleKeyConfirmed = confirmed, Enabled = false };
                if (roleReadout != null) roleReadout.Text = "Windows reports: " + code + (confirmed ? " · Selected role confirmed." : " · Does not match the selected role. Choose again.");
                await SaveAsync();
            });
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
    private async Task<bool> SaveAsync(bool configureEngine = true)
    {
        if (smoke) return true;
        if (!initialized || disposed) return false;
        if (storageBlocked) { profile = profile with { Enabled = false }; keyboard?.Pause("The saved configuration is invalid. Explicitly import or reset in Settings to recover."); Feedback.Text = "The original invalid configuration is preserved. Import a valid file or explicitly reset in Settings before saving or enabling."; return false; }
        var candidate = profile;
        try { await store.SaveAsync(candidate); lastSavedProfile = candidate; if (configureEngine) keyboard?.Configure(candidate); UpdateStatus(); return true; }
        catch (Exception) { profile = lastSavedProfile with { Enabled = false }; if (configureEngine) keyboard?.Pause("Settings could not be saved. Mapping stopped."); Feedback.Text = "Settings could not be saved. Previous settings are preserved and mapping is paused."; return false; }
    }
    private async Task EnableAsync()
    {
        if (smoke) return;
        var requestGeneration = pauseGeneration;
        var safetyGeneration = keyboard?.SafetyPauseGeneration ?? 0;
        await RunProfileOperationAsync(async () =>
        {
            if (requestGeneration != pauseGeneration || safetyGeneration != keyboard?.SafetyPauseGeneration) return;
            if (!profile.ConsentGranted || !profile.RoleKeyConfirmed || !profile.RecoveryTested)
            { OpenWindow(); Navigate(0); Feedback.Text = "Complete consent, physical role confirmation and the emergency pause test first."; return; }
            if (!profile.Rules.Any(r => r.Enabled)) { OpenWindow(); Navigate(1); Feedback.Text = "Choose and confirm a shortcut before enabling it."; return; }
            profile = profile with { Enabled = true };
            if (!await SaveAsync()) return;
            if (requestGeneration != pauseGeneration || safetyGeneration != keyboard?.SafetyPauseGeneration || quitting) { profile = profile with { Enabled = false }; await SaveAsync(); return; }
            if (keyboard?.Resume(safetyGeneration) != true) { profile = profile with { Enabled = false }; await SaveAsync(); Feedback.Text = keyboard?.Status ?? "Mapping could not start."; }
            UpdateStatus();
        });
    }
    private async Task PauseAsync(string reason)
    {
        RequestPause(reason);
        await RunProfileOperationAsync(async () =>
        {
            profile = profile with { Enabled = false };
            await SaveAsync(); keyboard?.Pause(reason); UpdateStatus();
        });
    }
    private void RequestPause(string reason) { pauseGeneration++; trialDisplayActive = false; keyboard?.Pause(reason); }
    private Task<bool> RunProfileOperationAsync(Func<Task> operation) => profileOperations.RunAsync(operation);
    private async Task ReplaceStoredProfileAsync(AppProfile next)
    {
        var wasBlocked = storageBlocked;
        if (wasBlocked) await store.PreserveRejectedAsync();
        storageBlocked = false;
        profile = next;
        if (!await SaveAsync()) storageBlocked = wasBlocked;
    }
    public void EmergencyStop(string reason) { RequestPause(reason); profile = profile with { Enabled = false }; }
    private async void PauseClick(object sender, RoutedEventArgs e) => await PauseAsync("Paused by you.");
    private async void ExitClick(object sender, RoutedEventArgs e) => await QuitAsync();
    private async Task QuitAsync()
    {
        if (quitting) return;
        quitting = true;
        RequestPause("Exiting KeyBridge.");
        await profileOperations.CloseAsync(async () =>
        {
            var recovered = keyboard?.PrepareForExit() ?? true;
            if (initialized && !storageBlocked)
            {
                profile = profile with { Enabled = false, PreviousSessionClean = recovered };
                await SaveAsync(configureEngine: false);
            }
            Dispose(); Application.Current.Shutdown();
        });
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
            else if (keyboard?.IsLocalTrial == true) scopeReason = "Shortcut preview is active only in the KeyBridge editor.";
            else if (keyboard?.IsTrial == true) scopeReason = "Shortcut preview is active only in the selected application.";
            else
            {
                var effective = profile.Rules.Count(r => r.Enabled && r.Scope.ProcessName.Equals(context.ProcessName, StringComparison.OrdinalIgnoreCase)
                    && r.Verification.ApplicationVersion == context.VersionToken && r.Verification.WindowsBuild == context.WindowsBuild
                    && r.Verification.VerifiedContexts.Contains(VerifiedContext.EnglishKeyboard));
                scopeReason = effective == 0 ? "No confirmed shortcuts match this application version and Windows build: native input is preserved." : effective + " confirmed shortcuts apply in this context.";
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
        if (!KeyChordParser.TryParse(value, out var chord))
            throw new ArgumentException("Enter a valid keyboard combination, for example Ctrl + C or Command + Shift + Z. Use each modifier once.");
        return chord;
    }
}
