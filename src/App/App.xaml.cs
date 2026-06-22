using System.Drawing;
using System.IO;
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
using Application = System.Windows.Application;

namespace MidiToEverything.App;

/// <summary>
/// Composition root and lifecycle (docs/02_Architecture.md §2). Builds the generic host,
/// starts the engine, shows the main window, and keeps a tray icon for background residency
/// (FR-7.3). Closing the window hides to tray; exit is via the tray menu.
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private EngineCoordinator? _engine;
    private NotifyIcon? _tray;
    private ToolStripMenuItem? _startupItem;
    private MainWindow? _window;
    private bool _exiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(AppInfo.DataDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(AppInfo.DataDirectory, "logs", "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("{Name} {Version} starting.", AppInfo.Name, AppInfo.Version);

        _window = _host.Services.GetRequiredService<MainWindow>();
        _window.Closing += OnWindowClosing;

        // The view model subscribes in its constructor (above), so start the engine after.
        _engine = _host.Services.GetRequiredService<EngineCoordinator>();
        _engine.Start();

        SetupTray();
        SyncAutoStart();
        _window.Show();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddInfrastructure();

        // Persistence + configuration.
        services.AddSingleton<IProfileRepository>(sp =>
            new Core.Persistence.JsonProfileRepository(
                logger: sp.GetService<ILogger<Core.Persistence.JsonProfileRepository>>()));
        services.AddSingleton(sp => sp.GetRequiredService<IProfileRepository>().LoadOrCreateDefault());

        // Core engine.
        services.AddSingleton(sp => new ProfileManager(
            sp.GetRequiredService<AppConfig>(),
            sp.GetRequiredService<IWindowWatcher>(),
            sp.GetService<ILogger<ProfileManager>>()));
        services.AddSingleton<IMappingContext>(sp => sp.GetRequiredService<ProfileManager>());
        services.AddSingleton(sp => new ActionExecutor(sp.GetRequiredService<IInputSink>()));
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

    private void SetupTray()
    {
        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = AppInfo.Name,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("表示", null, (_, _) => ShowWindow());
        menu.Items.Add("緊急停止 切替", null, (_, _) =>
            _host!.Services.GetRequiredService<GatedInputSink>().Toggle());

        _startupItem = new ToolStripMenuItem("Windows起動時に実行", null, (_, _) => ToggleAutoStart())
        {
            CheckOnClick = false,
        };
        menu.Items.Add(_startupItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowWindow();

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

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_exiting)
        {
            return;
        }

        // Hide to tray instead of exiting (FR-7.3).
        e.Cancel = true;
        _window?.Hide();
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

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
