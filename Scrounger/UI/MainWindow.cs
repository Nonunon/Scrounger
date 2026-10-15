using System.Reflection;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using OtterGui.Log;
using OtterGui.Raii;

namespace Scrounger.UI;

public partial class MainWindow : Window
{
    private readonly Scrounger _plugin;
    public MainWindow(Scrounger plugin) : base($"Scrounger {Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? ""}", ImGuiWindowFlags.None, false)
    {
        _plugin = plugin;
        Size = new(800, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        TitleBarButtons.Add(new TitleBarButtons().DonationButton);
        TitleBarButtons.Add(new TitleBarButtons().DiscordButton);
        _selector = new GatherListSelector(_plugin);
    }

    public override void PreDraw()
    {
        SetupValues();
        base.PreDraw();
    }

    public override void Draw()
    {
        string buttonText = _plugin.AutoGather.Enabled ? "Stop Gathering" : "Start Gathering";
        if (ImGui.Button(buttonText))
        {
            _plugin.AutoGather.Enabled = !_plugin.AutoGather.Enabled;
        }
        ImGui.SameLine();
        ImGui.Text($"Status: {_plugin.AutoGather.AutoStatus}");
        using var tab = ImRaii.TabBar("ScroungerTabs", ImGuiTabBarFlags.Reorderable);
        if (!tab)
            return;
        DrawGatherablesTab();
        DrawConfigTab();
        DrawConfigPresetsTab();
        DrawDebugTab();
    }
}