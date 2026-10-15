using System.Numerics;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using OtterGui;
using OtterGui.Raii;
using Scrounger.Data;
using Scrounger.Utils.Extensions;

namespace Scrounger.UI;

public partial class MainWindow
{
    public void DrawConfigTab()
    {
        using var id = ImRaii.PushId("ConfigTab");
        using var tab = ImRaii.TabItem("Configuration");
        if (!tab)
            return;
        ImGuiUtil.HoverTooltip("Settings 'n' such");
        ImGui.BeginChild("ConfigTab" , new Vector2(0,0), false);
        DrawGeneralSettings();
        DrawTimerSettings();
        DrawFunctionSettings();
        DrawAdvancedSettings();
        ImGui.EndChild();
    }

    private void DrawAdvancedSettings()
    {
        if (ImGui.CollapsingHeader("Advanced Settings"))
        {
            ImGui.Text("Don't mess with these unless you know what you're doing!");
            ImGui.Separator();

            var flag = Scrounger.Config.FlagPathing;
            DrawCheckbox("Use Flag Pathing", ref flag, x => Scrounger.Config.FlagPathing = x,
                "Use map flags to path to nodes.");

            var walk = Scrounger.Config.UseFlying;
            DrawCheckbox("Use flying navigation", ref walk, x => Scrounger.Config.UseFlying = x,
                "Use flying navigation instead of walking. (Disabling this can be useful for certain zones with poor mesh generation)");

            var gathering = Scrounger.Config.DoGathering;
            DrawCheckbox("Gather From Nodes", ref gathering, x => Scrounger.Config.DoGathering = x,
                "Enable gathering from nodes. Disabling this puts Scrounger in 'Nav Only' mode.");

            var debug = Scrounger.Config.DrawDebugTab;
            DrawCheckbox("Draw Debug Tab", ref debug, x => Scrounger.Config.DrawDebugTab = x,
                "Draw debug tab for additional debugging information (Advanced users only!)");
        }
    }

    private void DrawFunctionSettings()
    {
        if (ImGui.CollapsingHeader("Additional Functions"))
        {
            var reduce = Scrounger.Config.DoReduce;
            DrawCheckbox("Aetherial Reduction", ref reduce, x => Scrounger.Config.DoReduce = x,
                "Do aetherial reduction automatically when gathering Ephemeral nodes.");

            var materialize = Scrounger.Config.DoMaterialize;
            DrawCheckbox("Materia Extraction", ref materialize, x => Scrounger.Config.DoMaterialize = x,
                "Extract materia from your gear automatically.");

            var givingLand = Scrounger.Config.UseGivingLandOnCooldown;
            DrawCheckbox("Use Giving Land On Cooldown", ref givingLand, x => Scrounger.Config.UseGivingLandOnCooldown = x,
                "Use giving land for crystals on cooldown when gathering other items.");

            var repair = Scrounger.Config.DoRepair;
            DrawCheckbox("Repair Gear", ref repair, x => Scrounger.Config.DoRepair = x,
                "Repair gear automatically.");

            var repairThreshold = Scrounger.Config.RepairThreshold;
            DrawIntInput("Repair Threshold", ref repairThreshold, x => Scrounger.Config.RepairThreshold = x,
                "Repair gear when it's below this percentage of its max health.");
        }
    }

    private void DrawTimerSettings()
    {
        if (ImGui.CollapsingHeader("Timer Settings"))
        {
            var delay = Scrounger.Config.ExecutionDelay;
            DrawIntInput("Execution Delay", ref delay, x => Scrounger.Config.ExecutionDelay = x,
                "Delay between each action. (Milliseconds)");

            var precog = Scrounger.Config.TimedNodePrecog;
            DrawIntInput("Timed Node Precognition", ref precog, x => Scrounger.Config.TimedNodePrecog = x,
                "Time before (or after) a timed is available to start moving towards it. (Seconds)");

            var navReset = (int)Scrounger.Config.NavResetCooldown;
            DrawIntInput("Navigation Reset Cooldown", ref navReset, x => Scrounger.Config.NavResetCooldown = x,
                "Cooldown between navigation resets when stuck. (Seconds)");

            var navThreshold = (int)Scrounger.Config.NavResetThreshold;
            DrawIntInput("Navigation Reset Threshold", ref navThreshold, x => Scrounger.Config.NavResetThreshold = x,
                "How long your character needs to be stuck before unstuck methods trigger. (Seconds)");
        }
    }

    private void DrawGeneralSettings()
    {
        if (ImGui.CollapsingHeader("General Settings"))
        {
            DrawMountSelector();

            var mountDistance = Scrounger.Config.MountUpDistance;
            DrawIntInput("Mount Up Distance", ref mountDistance, x => Scrounger.Config.MountUpDistance = x,
                "Minimum Distance between nodes before using your mount.");

            var abandon = Scrounger.Config.AbandonNodes;
            DrawCheckbox("Abandon Nodes When Done Gathering", ref abandon, x => Scrounger.Config.AbandonNodes = x,
                "Abandon nodes when you're done gathering instead of finishing them.");

            var fallbackSkill = Scrounger.Config.UseSkillsForFallbackItems;
            DrawCheckbox("Use Skills For Fallback Items", ref fallbackSkill,
                x => Scrounger.Config.UseSkillsForFallbackItems = x,
                "Use skills for gathering fallback items.");

            var goHomeDone = Scrounger.Config.GoHomeWhenDone;
            DrawCheckbox("Go Home When Done", ref goHomeDone, x => Scrounger.Config.GoHomeWhenDone = x,
                "Go home when you're done gathering. (Requires Lifestream plugin)");

            var goHomeIdle = Scrounger.Config.GoHomeWhenIdle;
            DrawCheckbox("Go Home When Idle", ref goHomeIdle, x => Scrounger.Config.GoHomeWhenIdle = x,
                "Go home when you're idle. (Requires Lifestream plugin)");

            var honkMode = Scrounger.Config.HonkMode;
            DrawCheckbox("Enabled Audio Alert when Gathering is Finished", ref honkMode, b => Scrounger.Config.HonkMode = b, "" +
                "Play a sound when gathering is finished. (Requires sense of humor)");

            var minerName = Scrounger.Config.MinerSetName;
            DrawInputString("Miner Set Name", ref minerName, x => Scrounger.Config.MinerSetName = x,
                "Name of your gearset for Miner.");

            var botName = Scrounger.Config.BotanistSetName;
            DrawInputString("Botanist Set Name", ref botName, x => Scrounger.Config.BotanistSetName = x,
                "Name of your gearset for Botanist.");

            var preferredGatheringType = Scrounger.Config.PreferredGatheringType;
            if (ImGuiUtil.GenericEnumCombo<GatherBuddy.Enums.GatheringType>("Preferred Gathering Type", 100, preferredGatheringType, out var newVal1))
            {
                Scrounger.Config.PreferredGatheringType = newVal1;
                Scrounger.Config.Save();
            }
            ImGuiUtil.HoverTooltip("Preferred gathering type for auto-gather when an item can be gathered by both jobs.");

            var aetherytePreference = Scrounger.Config.AetherytePreference;
            if (ImGuiUtil.GenericEnumCombo<ConfigTypes.AetherytePreference>("Aetheryte Preference", 100, aetherytePreference, out var newVal2))
            {
                Scrounger.Config.AetherytePreference = newVal2;
                Scrounger.Config.Save();
            }
            ImGuiUtil.HoverTooltip("Whether to prefer aetherytes based on distance or cost when teleporting.");

            var sortingType = Scrounger.Config.SortingMethod;
            if (ImGuiUtil.GenericEnumCombo<ConfigTypes.SortingType>("Item Sorting Method", 100, sortingType, out var newVal3))
            {
                Scrounger.Config.SortingMethod = newVal3;
                Scrounger.Config.Save();
            }
            ImGuiUtil.HoverTooltip("How items on your gather lists should be sorted.");
        }
    }

    private void DrawInputString(string label, ref string value, Action<string> setter, string tooltip = "",
        int width = 100)
    {
        ImGui.SetNextItemWidth(width);
        if (ImGui.InputText(label, ref value, 100))
        {
            setter(value);
            Scrounger.Config.Save();
        }
        ImGuiUtil.HoverTooltip(tooltip);
    }

    private void DrawIntInput(string label, ref int value, Action<int> setter, string tooltip = "",
        int width = 100)
    {
        ImGui.SetNextItemWidth(width);
        if (ImGui.InputInt(label, ref value))
        {
            setter(value);
            Scrounger.Config.Save();
        }
        ImGuiUtil.HoverTooltip(tooltip);
    }

    private void DrawCheckbox(string label, ref bool value, Action<bool> setter, string tooltip = "")
    {
        if (ImGui.Checkbox(label, ref value))
        {
            setter(value);
            Scrounger.Config.Save();
        }
        ImGuiUtil.HoverTooltip(tooltip);
    }

    private unsafe void DrawMountSelector()
    {
        ImGui.PushItemWidth(300);
        var ps = PlayerState.Instance();
        var preview = Svc.Data.GetExcelSheet<Mount>().First(x => x.RowId == Scrounger.Config.AutoGatherMountId)
            .Singular.ToString().ToProperCase();
        if (string.IsNullOrEmpty(preview))
            preview = "Mount Roulette";
        if (ImGui.BeginCombo("Select Mount", preview))
        {
            if (ImGui.Selectable("Mount Roulette", Scrounger.Config.AutoGatherMountId == 0))
            {
                Scrounger.Config.AutoGatherMountId = 0;
                Scrounger.Config.Save();
            }

            foreach (var mount in Svc.Data.GetExcelSheet<Mount>().OrderBy(x => x.Singular.ToString().ToProperCase()))
            {
                if (ps->IsMountUnlocked(mount.RowId))
                {
                    var selected = ImGui.Selectable(mount.Singular.ToString().ToProperCase(),
                        Scrounger.Config.AutoGatherMountId == mount.RowId);

                    if (selected)
                    {
                        Scrounger.Config.AutoGatherMountId = mount.RowId;
                        Scrounger.Config.Save();
                    }
                }
            }

            ImGui.EndCombo();
        }
    }
}