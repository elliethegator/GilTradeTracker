using Dalamud.Game.Command;
using ECommons.Configuration;
using ECommons.Schedulers;

namespace GilTradeTracker;

public sealed class Plugin : IDalamudPlugin
{
    public static Plugin P { get; private set; } = null!;
    public static Configuration C { get; private set; } = null!;

    private const string CommandName = "/giltracker";

    public readonly WindowSystem WindowSystem = new("GilTradeTracker");

    private MainWindow _mainWindow = null!;
    private ConfigWindow _configWindow = null!;
    private GilTracker _gilTracker = null!;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        P = this;
        ECommonsMain.Init(pluginInterface, this);

        new TickScheduler(() =>
        {
            C = EzConfig.Init<Configuration>();

            _configWindow = new ConfigWindow();
            _mainWindow = new MainWindow(_configWindow);
            _gilTracker = new GilTracker();
            _gilTracker.TradeDetected += OnTradeDetected;

            WindowSystem.AddWindow(_mainWindow);
            WindowSystem.AddWindow(_configWindow);

            Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the Gil Trade Tracker window."
            });

            Svc.PluginInterface.UiBuilder.Draw += DrawUi;
            Svc.PluginInterface.UiBuilder.OpenMainUi += OpenMain;
            Svc.PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        });
    }

    private void OnTradeDetected(string partnerName, long gilDelta)
    {
        if (gilDelta <= 0) return;

        C.Entries.Add(new GilEntry
        {
            PlayerName = partnerName,
            Amount = gilDelta,
            Date = DateTime.Now,
        });
        EzConfig.Save();
    }

    private void DrawUi()
    {
        ChatQueue.Pump();
        WindowSystem.Draw();
    }

    private void OpenMain() => _mainWindow.IsOpen = true;

    private void OpenConfig() => _configWindow.IsOpen = true;

    private void OnCommand(string command, string args) => _mainWindow.IsOpen ^= true;

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= DrawUi;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= OpenMain;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        Svc.Commands.RemoveHandler(CommandName);
        ChatQueue.Clear();
        if (_gilTracker != null)
        {
            _gilTracker.TradeDetected -= OnTradeDetected;
            _gilTracker.Dispose();
        }
        WindowSystem.RemoveAllWindows();
        _mainWindow?.Dispose();
        _configWindow?.Dispose();
        ECommonsMain.Dispose();
        P = null!;
    }
}
