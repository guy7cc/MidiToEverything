using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MidiToEverything.App.ViewModels;
using MidiToEverything.Core;
using MidiToEverything.Core.Application;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Infrastructure;
using MidiToEverything.Infrastructure.Input;
using MidiToEverything.Infrastructure.Startup;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Application = System.Windows.Application;

namespace MidiToEverything.App;

/// <summary>
/// Composition root and lifecycle (docs/02_Architecture.md §2). Builds the generic host,
/// starts the engine, shows the main window, and keeps a tray icon for background residency
/// (FR-7.3). Closing the window hides to tray; exit is via the tray menu.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// True only for CI release builds (the release workflow passes <c>-p:OfficialBuild=true</c>,
    /// which defines OFFICIAL_BUILD). Local/source builds are false, so they skip the automatic
    /// update check — avoiding a false "update available" and a dev build trying to MSI-update itself.
    /// </summary>
#if OFFICIAL_BUILD
    public static readonly bool IsOfficialBuild = true;
#else
    public static readonly bool IsOfficialBuild = false;
#endif

    /// <summary>Runtime-adjustable Serilog level so the Settings window can change it live.</summary>
    public static readonly LoggingLevelSwitch LogLevelSwitch = new(LogEventLevel.Debug);

    /// <summary>Parse a settings level string to a Serilog level (defaults to Debug).</summary>
    public static LogEventLevel ParseLogLevel(string? level) =>
        Enum.TryParse<LogEventLevel>(level, ignoreCase: true, out var parsed) ? parsed : LogEventLevel.Debug;

    private IHost? _host;
    private AppConfig? _bootConfig;
    private EngineCoordinator? _engine;
    private NotifyIcon? _tray;
    private ToolStripMenuItem? _showItem;
    private ToolStripMenuItem? _toggleItem;
    private ToolStripMenuItem? _startupItem;
    private ToolStripMenuItem? _exitItem;
    private MainWindow? _window;
    private bool _exiting;
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showSignal;
    private const string InstanceKey = "MidiToEverything.SingleInstance.v1";

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Install crash handling first so even startup failures are logged and surfaced.
        CrashReporter.Install(this);

        // Single instance: if one is already running, tell it to surface its window and exit.
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceKey, out var isFirstInstance);
        if (!isFirstInstance)
        {
            try { EventWaitHandle.OpenExisting(InstanceKey + ".show").Set(); } catch { /* ignore */ }
            Shutdown();
            return;
        }

        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceKey + ".show");
        var showWaiter = new Thread(() =>
        {
            while (_showSignal is { } sig)
            {
                try { sig.WaitOne(); } catch { return; }
                Dispatcher.BeginInvoke(() =>
                {
                    ReloadConfigFromDisk();
                    ShowWindow();
                });
            }
        }) { IsBackground = true, Name = "single-instance-waiter" };
        showWaiter.Start();

        Directory.CreateDirectory(AppInfo.DataDirectory);

        // Read persisted diagnostics settings before the logger exists (the host loads its own
        // copy later). Level changes apply live via LogLevelSwitch; retention is fixed here.
        _bootConfig = new Core.Persistence.JsonProfileRepository().LoadOrCreateDefault();
        var bootSettings = _bootConfig.Settings;
        LogLevelSwitch.MinimumLevel = ParseLogLevel(bootSettings.LogLevel);
        CrashReporter.AutoRestart = bootSettings.CrashAutoRestart;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LogLevelSwitch)
            .WriteTo.File(
                Path.Combine(AppInfo.DataDirectory, "logs", "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: Math.Max(1, bootSettings.LogRetentionDays))
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("{Name} {Version} starting.", AppInfo.Name, AppInfo.Version);

        var settings = _host.Services.GetRequiredService<AppConfig>().Settings;

        // Apply the persisted UI language and theme/accent before any window is created.
        Localization.Loc.Instance.SetLanguage(settings.Language);
        ThemeManager.Apply(settings.Theme, settings.AccentColor);

        // Honor the saved startup emission state before the view model reads the gate.
        _host.Services.GetRequiredService<GatedInputSink>().Enabled = settings.StartEmissionEnabled;

        _window = _host.Services.GetRequiredService<MainWindow>();
        _window.Closing += OnWindowClosing;

        // The view model subscribes in its constructor (above), so start the engine after.
        _engine = _host.Services.GetRequiredService<EngineCoordinator>();
        _engine.Start();

        SetupTray();
        SyncAutoStart();
        if (!settings.StartMinimized)
        {
            _window.Show();
        }

        // If the previous run crashed and restarted us, tell the user now that the UI is up.
        CrashReporter.ShowPendingCrashNotice();
    }

    private void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddInfrastructure();

        // Persistence + configuration.
        services.AddSingleton<IProfileRepository>(sp =>
            new Core.Persistence.JsonProfileRepository(
                logger: sp.GetService<ILogger<Core.Persistence.JsonProfileRepository>>()));
        // Reuse the config already read at boot (for the logger) instead of parsing config.json twice.
        services.AddSingleton(_bootConfig ?? throw new InvalidOperationException("Boot config not loaded."));

        // Core engine.
        services.AddSingleton(sp => new ProfileManager(
            sp.GetRequiredService<AppConfig>(),
            sp.GetRequiredService<IWindowWatcher>(),
            sp.GetService<ILogger<ProfileManager>>()));
        services.AddSingleton<IMappingContext>(sp => sp.GetRequiredService<ProfileManager>());
        // UI Automation driver + element picker (Phase 2; WPF-coupled, so implemented in App).
        services.AddSingleton<IUiaDriver>(sp =>
            new Automation.UiaDriver(sp.GetService<ILogger<Automation.UiaDriver>>()));
        services.AddSingleton<IUiaElementPicker>(sp =>
            new Automation.UiaElementPicker(sp.GetService<ILogger<Automation.UiaElementPicker>>()));

        // External-launch opt-in (Q5), initialized from settings; toggled at runtime by the UI.
        services.AddSingleton(sp =>
            new LaunchPolicy(sp.GetRequiredService<AppConfig>().Settings.AllowExternalLaunch));

        // Auto-update: check GitHub Releases, download + run the MSI on the user's confirmation.
        services.AddSingleton<IUpdateChecker>(sp => new Infrastructure.Update.GitHubUpdateChecker(
            logger: sp.GetService<ILogger<Infrastructure.Update.GitHubUpdateChecker>>()));
        services.AddSingleton<IUpdateInstaller>(sp => new Infrastructure.Update.MsiUpdateInstaller(
            logger: sp.GetService<ILogger<Infrastructure.Update.MsiUpdateInstaller>>()));

        // Action plugins (Phase 4): load from the app's "plugins" folder at startup.
        services.AddSingleton(sp =>
        {
            var registry = new PluginRegistry();
            var loader = new Infrastructure.Plugins.PluginLoader(
                sp.GetService<ILogger<Infrastructure.Plugins.PluginLoader>>());
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "plugins");
            foreach (var plugin in loader.LoadFromDirectory(dir))
            {
                registry.Register(plugin);
            }

            return registry;
        });

        // OBS client reads the live connection settings (host/port/password) from the running config.
        services.AddSingleton<IObsClient>(sp => new Infrastructure.Obs.ObsWebSocketClient(
            () =>
            {
                var s = sp.GetRequiredService<ProfileManager>().CurrentConfig.Settings;
                return new ObsConnection(s.ObsHost, s.ObsPort, s.ObsPassword);
            },
            sp.GetService<ILogger<Infrastructure.Obs.ObsWebSocketClient>>()));
        services.AddSingleton(sp => new ActionExecutor(
            ActionExecutor.DefaultHandlers(sp.GetRequiredService<IInputSink>())
                .Append(new Core.Application.Handlers.WindowControlActionHandler(
                    sp.GetRequiredService<IWindowController>()))
                .Append(new Core.Application.Handlers.MediaKeyActionHandler(
                    sp.GetRequiredService<IInputSink>()))
                .Append(new Core.Application.Handlers.TypeTextActionHandler(
                    sp.GetRequiredService<IInputSink>()))
                .Append(new Core.Application.Handlers.LaunchActionHandler(
                    sp.GetRequiredService<IShellLauncher>(), sp.GetRequiredService<LaunchPolicy>()))
                .Append(new Core.Application.Handlers.SetVolumeActionHandler(
                    sp.GetRequiredService<ISystemAudio>()))
                .Append(new Core.Application.Handlers.UiaActionHandler(
                    sp.GetRequiredService<IUiaDriver>()))
                .Append(new Core.Application.Handlers.VirtualDesktopActionHandler(
                    sp.GetRequiredService<IInputSink>()))
                .Append(new Core.Application.Handlers.WindowsToggleActionHandler(
                    sp.GetRequiredService<ISystemToggle>()))
                .Append(new Core.Application.Handlers.BrightnessActionHandler(
                    sp.GetRequiredService<IDisplayBrightness>()))
                .Append(new Core.Application.Handlers.HttpActionHandler(
                    sp.GetRequiredService<IHttpSender>()))
                .Append(new Core.Application.Handlers.OscActionHandler(
                    sp.GetRequiredService<IOscSender>()))
                .Append(new Core.Application.Handlers.ObsActionHandler(
                    sp.GetRequiredService<IObsClient>()))
                .Append(new Core.Application.Handlers.MidiOutActionHandler(
                    sp.GetRequiredService<IMidiOutput>()))
                .Append(new Core.Application.Handlers.MacroActionHandler(
                    sp.GetRequiredService<IInputSink>()))
                .Append(new Core.Application.Handlers.ToggleActionHandler(
                    sp.GetRequiredService<IInputSink>(), sp.GetRequiredService<IMidiOutput>()))
                .Append(new Core.Application.Handlers.PluginActionHandler(
                    sp.GetRequiredService<PluginRegistry>()))));
        services.AddSingleton(sp => new MidiEventPipeline(
            sp.GetRequiredService<IMidiSource>(),
            sp.GetRequiredService<IMappingContext>(),
            sp.GetRequiredService<ActionExecutor>(),
            logger: sp.GetService<ILogger<MidiEventPipeline>>()));
        services.AddSingleton<EngineCoordinator>();

        // UI.
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<ViewModels.Editing.ProfileEditorViewModel>();
        services.AddTransient<ProfileEditorWindow>();
        services.AddSingleton<Func<ProfileEditorWindow>>(sp =>
            () => sp.GetRequiredService<ProfileEditorWindow>());
    }

    /// <summary>Load the bundled app icon for the tray (falls back to the system icon).</summary>
    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var info = Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
            if (info?.Stream is { } stream)
            {
                using (stream)
                {
                    return new System.Drawing.Icon(stream, 32, 32);
                }
            }
        }
        catch
        {
            // fall through to the system icon
        }

        return SystemIcons.Application;
    }

    private void SetupTray()
    {
        _tray = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Visible = true,
            Text = AppInfo.Name,
        };

        var menu = new ContextMenuStrip();
        _showItem = new ToolStripMenuItem(Localization.Loc.T("tray.show"), null, (_, _) => ShowWindow());
        _toggleItem = new ToolStripMenuItem(Localization.Loc.T("common.emergencyToggle"), null, (_, _) =>
            _host!.Services.GetRequiredService<GatedInputSink>().Toggle());
        _startupItem = new ToolStripMenuItem(Localization.Loc.T("tray.startup"), null, (_, _) => ToggleAutoStart())
        {
            CheckOnClick = false,
        };
        _exitItem = new ToolStripMenuItem(Localization.Loc.T("tray.exit"), null, (_, _) => ExitApp());
        menu.Items.Add(_showItem);
        menu.Items.Add(_toggleItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowWindow();

        // Remove the tray icon if the app is torn down by the crash handler (which bypasses OnExit).
        CrashReporter.Cleanup = () =>
        {
            try { if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); } } catch { /* ignore */ }
        };

        // Reflect the current auto-start state each time the menu opens (it may have been
        // changed from the main window's checkbox).
        menu.Opening += (_, _) =>
        {
            if (_startupItem is not null)
            {
                _startupItem.Checked = _host!.Services.GetRequiredService<ProfileManager>()
                    .CurrentConfig.Settings.StartWithWindows;
            }
        };

        // Re-translate the menu when the UI language changes at runtime.
        Localization.Loc.Instance.LanguageChanged += (_, _) => Dispatcher.BeginInvoke(RetranslateTray);

        // Reflect the active profile in the tray tooltip (FR-5.6).
        var profiles = _host!.Services.GetRequiredService<ProfileManager>();
        profiles.Changed += (_, state) => Dispatcher.BeginInvoke(() =>
        {
            if (_tray is not null)
            {
                var text = $"{AppInfo.Name} — {state.Effective.Name}";
                _tray.Text = text.Length > 63 ? text[..63] : text;
            }
        });
    }

    /// <summary>Show a tray balloon (no-op if the tray icon is unavailable). Caller gates on the setting.</summary>
    public static void NotifyTray(string title, string text)
    {
        if (Current is App app && app._tray is { Visible: true } tray)
        {
            try { tray.ShowBalloonTip(4000, title, text, ToolTipIcon.Info); } catch { /* best effort */ }
        }
    }

    // Update tray menu item captions to the current language.
    private void RetranslateTray()
    {
        if (_showItem is not null) _showItem.Text = Localization.Loc.T("tray.show");
        if (_toggleItem is not null) _toggleItem.Text = Localization.Loc.T("common.emergencyToggle");
        if (_startupItem is not null) _startupItem.Text = Localization.Loc.T("tray.startup");
        if (_exitItem is not null) _exitItem.Text = Localization.Loc.T("tray.exit");
    }

    // Apply the persisted "start with Windows" setting to the registry on launch (FR-7.6).
    private void SyncAutoStart()
    {
        var enabled = _host!.Services.GetRequiredService<ProfileManager>().CurrentConfig.Settings.StartWithWindows;
        TrySetAutoStart(enabled);
        if (_startupItem is not null)
        {
            _startupItem.Checked = enabled;
        }
    }

    private void ToggleAutoStart()
    {
        var profiles = _host!.Services.GetRequiredService<ProfileManager>();
        var repository = _host.Services.GetRequiredService<IProfileRepository>();
        var config = profiles.CurrentConfig;
        var enabled = !config.Settings.StartWithWindows;

        TrySetAutoStart(enabled);

        var updated = config with { Settings = config.Settings with { StartWithWindows = enabled } };
        try
        {
            repository.Save(updated);
        }
        catch (Exception ex)
        {
            _host.Services.GetRequiredService<ILogger<App>>().LogWarning(ex, "Failed to persist auto-start setting.");
        }

        profiles.Reload(updated);
        if (_startupItem is not null)
        {
            _startupItem.Checked = enabled;
        }
    }

    private void TrySetAutoStart(bool enabled)
    {
        try
        {
            WindowsStartup.SetEnabled(enabled, Environment.ProcessPath ?? string.Empty);
        }
        catch (Exception ex)
        {
            _host!.Services.GetRequiredService<ILogger<App>>().LogWarning(ex, "Failed to update Windows startup entry.");
        }
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    /// <summary>
    /// A second launch surfaces this already-running instance (single instance) rather than starting
    /// its own process, so it never loads config itself. Re-read config.json from disk here so the
    /// active profile and bindings reflect any changes made since this instance started.
    /// In-memory settings auto-save, so this is a no-op when nothing changed on disk.
    /// </summary>
    private void ReloadConfigFromDisk()
    {
        if (_host is null)
        {
            return;
        }

        // The profile editor is the authoritative in-progress editor (and auto-saves); don't reload
        // underneath an open edit, which would diverge from what the editor is showing.
        foreach (Window window in Windows)
        {
            if (window is ProfileEditorWindow)
            {
                return;
            }
        }

        try
        {
            var repository = _host.Services.GetRequiredService<IProfileRepository>();
            var manager = _host.Services.GetRequiredService<ProfileManager>();
            manager.Reload(repository.LoadOrCreateDefault());
        }
        catch (Exception ex)
        {
            _host.Services.GetService<ILogger<App>>()?.LogWarning(ex, "Failed to reload config on activation.");
        }
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_exiting)
        {
            return;
        }

        // Close button: hide to the tray (FR-7.3) or exit, per the user's setting.
        var closeToTray = _host?.Services.GetRequiredService<ProfileManager>().CurrentConfig.Settings.CloseToTray ?? true;
        if (closeToTray)
        {
            e.Cancel = true;
            _window?.Hide();
        }
        else
        {
            ExitApp();
        }
    }

    private void ExitApp()
    {
        _exiting = true;
        Shutdown();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        if (_engine is not null)
        {
            await _engine.DisposeAsync();
        }

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        var signal = _showSignal;
        _showSignal = null;
        signal?.Dispose();
        _instanceMutex?.Dispose();

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
