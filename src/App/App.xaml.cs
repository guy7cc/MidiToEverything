using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MidiToEverything.Core;
using Serilog;

namespace MidiToEverything.App;

/// <summary>
/// Application entry point. Builds the generic host (DI + logging) that will,
/// over later milestones, own the MIDI pipeline, window watcher and input sink.
/// For M0 it only wires DI/Serilog and shows the main window.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    public IServiceProvider Services =>
        _host?.Services ?? throw new InvalidOperationException("Host not started.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(AppInfo.DataDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
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
        logger.LogInformation("{Name} {Version} started.", AppInfo.Name, AppInfo.Version);

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // Views / ViewModels. Composition root grows here as milestones land.
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
