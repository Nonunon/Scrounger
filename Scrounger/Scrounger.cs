using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Commands;
using ECommons.DalamudServices;
using OtterGui.Log;
using Scrounger.AutoGather;
using Scrounger.AutoGather.Lists;
using Scrounger.UI;
using Scrounger.Utils;

namespace Scrounger;

public class Scrounger :IDalamudPlugin
{
    public string Name => "Scrounger";

    private readonly WindowSystem _windowSystem;

    private readonly MainWindow _mainWindow;

    public static Config Config = null!;
    public static Logger Log = new();
    public static WorldData WorldData = null!;
    public static SeTime Time = null!;

    public readonly AutoGatherListsManager AutoGatherListsManager;
    public readonly AutoGather.AutoGather AutoGather;

    public Scrounger(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);

        // These depend on Svc, so they must be created after ECommons is initialized
        WorldData = new(Svc.Data, Log);
        Time = new();

        Config = Config.Load();
        AutoGatherListsManager = AutoGatherListsManager.Load();
        AutoGather = new(this);

        _windowSystem = new WindowSystem(Name);
        _mainWindow = new MainWindow(this);

        _windowSystem.AddWindow(_mainWindow);

        Svc.PluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        Svc.Framework.Update += AutoGather.DoAutoGather;
    }

    public ConfigPreset GetPreset(Guid id)
    {
        return _mainWindow.GetPreset(id);
    }

    public void OpenMainUi()
    {
        _mainWindow.Toggle();
    }

    [Cmd("/scrounger", "Open main window")]
    public void OnCommand(string command, string args)
    {
        OpenMainUi();
    }

    public void Dispose()
    {
        Svc.Framework.Update -= AutoGather.DoAutoGather;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        Svc.PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        _windowSystem.RemoveAllWindows();

        // Stops navigation and pending tasks so nothing keeps running after unload
        AutoGather.Enabled = false;
        AutoGather.Dispose();
        Time.Dispose();

        ECommonsMain.Dispose();
    }
}