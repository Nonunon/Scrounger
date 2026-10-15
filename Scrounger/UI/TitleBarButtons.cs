using System.Diagnostics;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.ImGuiMethods;
using Dalamud.Bindings.ImGui;
using OtterGui;

namespace Scrounger.UI;

public class TitleBarButtons
{
    public TitleBarButton DonationButton => new TitleBarButton()
    {
        Icon = FontAwesomeIcon.Heart,
        ShowTooltip = () =>
        {
            ImGui.BeginTooltip();
            ImGui.Text("Support NostraThomas on Ko-fi");
            ImGui.EndTooltip();
        },
        Priority = 0,
        Click = _ =>
        {
            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = "https://ko-fi.com/nostrathomas",
                    UseShellExecute = true,
                    Verb = String.Empty
                });
            }
            catch
            {
                // ignored
            }
        },
        AvailableClickthrough = true
    };

    public TitleBarButton DiscordButton => new TitleBarButton()
    {
        Icon = FontAwesomeIcon.At,
        ShowTooltip = () =>
        {
            ImGui.BeginTooltip();
            ImGui.Text("Join the NostraThomas Industries Discord for support and updates");
            ImGui.EndTooltip();
        },
        Priority = 1,
        Click = _ =>
        {
            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = "https://discord.gg/56nXcvE7nX",
                    UseShellExecute = true,
                    Verb = String.Empty
                });
            }
            catch
            {
                // ignored
            }
        },
        AvailableClickthrough = true
    };
}