using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using ZeroTranslate.Core;
using ZeroTranslate.Core.Interfaces;
using ZeroTranslate.ViewModels;

namespace ZeroTranslate;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private AppOrchestrator? _orchestrator;

    // Single-instance guard: a named mutex detects an existing instance and a
    // named event lets the second launch ask the first to show its window.
    private const string MutexName = "ZeroTranslate.SingleInstance.Mutex";
    private const string ShowEventName = "ZeroTranslate.SingleInstance.ShowEvent";
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showEvent;
    private Thread? _showSignalThread;
    private bool _ownsMutex;

    /// <summary>
    /// Service provider for resolving dependencies.
    /// Used by Views that need to resolve services (e.g. SettingsWindow).
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // --- Single-instance enforcement ---
        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            // Another instance is running — signal it to surface, then exit.
            try
            {
                if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
                {
                    existing.Set();
                    existing.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"SingleInstance signal: {ex.Message}");
            }

            Shutdown();
            return;
        }

        // We own the instance: create the event and a thread that surfaces the
        // main window whenever a later launch sets it.
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        StartShowSignalListener();

        // Setup DI container
        var services = new ServiceCollection();
        services.AddXTranslateServices();

        // Register factory for transient PopupViewModel
        services.AddTransient<Func<PopupViewModel>>(sp => () => sp.GetRequiredService<PopupViewModel>());

        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        // Load settings
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        settingsService.Load();

        // Apply saved engine preference
        var registry = _serviceProvider.GetRequiredService<Services.TranslationEngineRegistry>();
        registry.ActiveEngineName = settingsService.Settings.ActiveEngineName;

        // Apply engine fallback preference
        var translationService = _serviceProvider.GetRequiredService<Services.TranslationService>();
        translationService.EnableFallback = settingsService.Settings.EnableEngineFallback;

        // Load translation history
        _serviceProvider.GetRequiredService<Services.HistoryService>().Load();

        // Initialize orchestrator
        _orchestrator = _serviceProvider.GetRequiredService<AppOrchestrator>();
        _orchestrator.Initialize();

        Log.Debug("[ZeroTranslate] App startup complete with DI container.");
    }

    private void StartShowSignalListener()
    {
        _showSignalThread = new Thread(() =>
        {
            while (_showEvent != null)
            {
                try
                {
                    if (!_showEvent.WaitOne())
                        break;
                }
                catch
                {
                    break;
                }

                // Marshal to the UI thread to show the main window.
                Dispatcher.BeginInvoke(() => _orchestrator?.ShowMainWindow());
            }
        })
        {
            IsBackground = true,
            Name = "ZeroTranslate-ShowSignalListener"
        };
        _showSignalThread.Start();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        // Persist translation history before teardown.
        try { _serviceProvider?.GetService<Services.HistoryService>()?.Save(); }
        catch { /* non-critical */ }

        _orchestrator?.Shutdown();
        _serviceProvider?.Dispose();

        // Release single-instance handles.
        _showEvent?.Dispose();
        _showEvent = null;
        if (_ownsMutex)
        {
            try { _singleInstanceMutex?.ReleaseMutex(); } catch { /* ignore */ }
        }
        _singleInstanceMutex?.Dispose();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error($"UNHANDLED: {e.Exception}");
        e.Handled = true;
    }
}
