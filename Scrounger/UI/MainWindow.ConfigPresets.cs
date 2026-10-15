using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using OtterGui;
using OtterGui.Raii;
using Scrounger.AutoGather;

namespace Scrounger.UI;

public partial class MainWindow
{
    private readonly ConfigPresetsSelector _presetsSelector = new();

    public ConfigPreset GetPreset(Guid id)
    {
        var preset = _presetsSelector.Presets.FirstOrDefault(x => x.Id == id);
        return preset ?? _presetsSelector.Presets.First();
    }
    public void DrawConfigPresetsTab()
    {
        using var id = ImRaii.PushId("PresetsTab");
        using var tab = ImRaii.TabItem("Gathering Presets");
        if (!tab)
            return;
        ImGuiUtil.HoverTooltip("How to gather things");
        ImGui.BeginChild("PresetsTab", new Vector2(0, 0), false);
        _presetsSelector.Draw(SelectorWidth);
        ImGui.SameLine();
        DrawConfigPreset(_presetsSelector.Current);
        ImGui.EndChild();
    }

    private void DrawConfigPreset(ConfigPreset? presetsSelectorCurrent)
    {
        ImGui.BeginChild("ConfigPreset", new Vector2(0, 0), false);
        if (presetsSelectorCurrent == null)
        {
            ImGui.Text("No preset selected");
        }
        else
        {
            DrawConfigPresetInternal(presetsSelectorCurrent);
        }

        ImGui.EndChild();
    }

    public unsafe int GetInventoryItemCount(uint itemRowId)
    {
        return InventoryManager.Instance()->GetInventoryItemCount(itemRowId < 100000 ? itemRowId : itemRowId - 100000, itemRowId >= 100000);
    }

    private void DrawConsumableConfig(string name, ConfigPreset.ActionConfigConsumable consumable, IEnumerable<Item>? items)
    {
        var list = items
            .SelectMany(item => new[] { (item, rowid: item.RowId), (item, rowid: item.RowId + 100000) })
            .Where(x => x.item.CanBeHq || x.rowid < 100000)
            .Select(x => (name: x.item.Name.ExtractText(), x.rowid, count: GetInventoryItemCount(x.rowid)))
            .OrderBy(x => x.count == 0)
            .ThenBy(x => x.name)
            .Select(x => x with { name = $"{(x.rowid > 100000 ? " " : "")}{x.name} ({x.count})" })
            .ToList();

        var selected = (consumable.ItemId > 0 ? list.FirstOrDefault(x => x.rowid == consumable.ItemId).name : null) ?? string.Empty;
        using var combo = ImRaii.Combo($"Select {name.ToLower()}", selected);
        if (combo)
        {
            if (ImGui.Selectable(string.Empty, consumable.ItemId <= 0))
            {
                consumable.ItemId = 0;
                _presetsSelector.Save();
            }

            bool? separatorState = null;
            foreach (var (itemname, rowid, count) in list)
            {
                if (count != 0) separatorState = true;
                else if (separatorState ?? false)
                {
                    ImGui.Separator();
                    separatorState = false;
                }

                if (ImGui.Selectable(itemname, consumable.ItemId == rowid))
                {
                    consumable.ItemId = rowid;
                    _presetsSelector.Save();
                }
            }
        }

        var minGp = consumable.MinGP;
        DrawPresetInputInt("Min GP", ref minGp, x => consumable.MinGP = x, $"Minimum GP for {name}");
        ImGui.SameLine();
        var maxGp = consumable.MaxGP;
        DrawPresetInputInt("Max GP", ref maxGp, x => consumable.MaxGP = x, $"Maximum GP for {name}");
    }

    private void DrawConfigPresetInternal(ConfigPreset preset)
    {
        var givingLand = preset.UseGivingLandOnCooldown;
        DrawPresetCheckbox("Use Giving Land On Cooldown", ref givingLand, x => preset.UseGivingLandOnCooldown = x,
            "Use giving land for crystals on cooldown when gathering other items.");

        var collectableAlwaysSolidAge = preset.CollectableAlwaysUseSolidAge;
        DrawPresetCheckbox("Always use Solid Words/Ageless Wisdom on Collectibles", ref collectableAlwaysSolidAge,
            x => preset.CollectableAlwaysUseSolidAge = x,
            "Always use Solid Words/Ageless Wisdom on Collectibles.");

        var bestNodes = preset.SpendGPOnBestNodesOnly;
        DrawPresetCheckbox("Spend GP on Best Nodes Only", ref bestNodes, x => preset.SpendGPOnBestNodesOnly = x,
            "Spend GP on nodes with the best gathering bonuses only.");

        var regularMinGp = preset.GatherableMinGP;
        DrawPresetInputInt("Minimum GP for Normal Nodes", ref regularMinGp, x => preset.GatherableMinGP = x,
            "Minimum GP for Normal Nodes.");

        var collectableMinGp = preset.CollectableMinGP;
        DrawPresetInputInt("Minimum GP for Collectibles", ref collectableMinGp, x => preset.CollectableMinGP = x,
            "Minimum GP for Collectibles.");

        var collectibleManualScores = preset.CollectableManualScores;
        DrawPresetCheckbox("Use Manual Scoring for Collectibles", ref collectibleManualScores,
            x => preset.CollectableManualScores = x, "Use Manual Scoring for Collectibles.");

        var targetScore = preset.CollectableTargetScore;
        DrawPresetInputInt("Target Score for Collectibles", ref targetScore, x => preset.CollectableTargetScore = x,
            "Target Score for Collectibles.");

        var minScore = preset.CollectableMinScore;
        DrawPresetInputInt("Minimum Score for Collectibles", ref minScore, x => preset.CollectableMinScore = x,
            "Minimum Score for Collectibles.");

        DrawActionConfigs(preset);
        DrawConsumableConfigs(preset);
    }

    private void DrawConsumableConfigs(ConfigPreset preset)
    {
        if (ImGui.CollapsingHeader("Consumables"))
        {
            DrawConsumableConfig("Cordial", preset.Consumables.Cordial, AutoGather.AutoGather.PossibleCordials);
            DrawConsumableConfig("Food", preset.Consumables.Food, AutoGather.AutoGather.PossibleFoods);
            DrawConsumableConfig("Potion", preset.Consumables.Potion, AutoGather.AutoGather.PossiblePotions);
            DrawConsumableConfig("Manual", preset.Consumables.Manual, AutoGather.AutoGather.PossibleManuals);
            DrawConsumableConfig("Squadron Manual", preset.Consumables.SquadronManual, AutoGather.AutoGather.PossibleSquadronManuals);
            DrawConsumableConfig("Squadron Pass", preset.Consumables.SquadronPass, AutoGather.AutoGather.PossibleSquadronPasses);
        }
    }

    private void DrawActionConfigs(ConfigPreset preset)
    {
        if (ImGui.CollapsingHeader("Action Configuration"))
        {
            ImGui.Indent();
            if (ImGui.CollapsingHeader("Regular Actions"))
            {
                ImGui.Indent();
                if (ImGui.CollapsingHeader("Bountiful"))
                {
                    DrawActionConfigYieldBonus(preset.GatherableActions.Bountiful);
                }

                if (ImGui.CollapsingHeader("Yield 1"))
                {
                    DrawActionConfigIntegrity(preset.GatherableActions.Yield1);
                }

                if (ImGui.CollapsingHeader("Yield 2"))
                {
                    DrawActionConfigIntegrity(preset.GatherableActions.Yield2);
                }

                if (ImGui.CollapsingHeader("Solid Words/Ageless Wisdom"))
                {
                    DrawActionConfigYieldTotal(preset.GatherableActions.SolidAge);
                }

                if (ImGui.CollapsingHeader("Twelves Bounty"))
                {
                    DrawActionConfigIntegrity(preset.GatherableActions.TwelvesBounty);
                }

                if (ImGui.CollapsingHeader("The Giving Land"))
                {
                    DrawActionConfigIntegrity(preset.GatherableActions.GivingLand);
                }

                if (ImGui.CollapsingHeader("Gift 1"))
                {
                    DrawActionConfigIntegrity(preset.GatherableActions.Gift1);
                }

                if (ImGui.CollapsingHeader("Gift 2"))
                {
                    DrawActionConfigIntegrity(preset.GatherableActions.Gift2);
                }

                if (ImGui.CollapsingHeader("Tidings"))
                {
                    DrawActionConfigIntegrity(preset.GatherableActions.Tidings);
                }
                ImGui.Unindent();
            }

            if (ImGui.CollapsingHeader("Collectible Actions"))
            {
                ImGui.Indent();
                if (ImGui.CollapsingHeader("Scrutiny"))
                {
                    DrawActionConfigSingle(preset.CollectableActions.Scrutiny);
                }

                if (ImGui.CollapsingHeader("Scour"))
                {
                    DrawActionConfigSingle(preset.CollectableActions.Scour);
                }

                if (ImGui.CollapsingHeader("Brazen"))
                {
                    DrawActionConfigSingle(preset.CollectableActions.Brazen);
                }

                if (ImGui.CollapsingHeader("Meticulous"))
                {
                    DrawActionConfigSingle(preset.CollectableActions.Meticulous);
                }

                if (ImGui.CollapsingHeader("Solid Words/Ageless Wisdom"))
                {
                    DrawActionConfigSingle(preset.CollectableActions.SolidAge);
                }
                ImGui.Unindent();
            }
            ImGui.Unindent();
        }
    }

    private void DrawActionConfigBoon(ConfigPreset.ActionConfigBoon config)
    {
        var minBoonChance = config.MinBoonChance;
        DrawPresetInputInt("Minimum Boon Chance", ref minBoonChance, x => config.MinBoonChance = x,
            "Minimum Boon Chance.");

        var maxBoonChance = config.MaxBoonChance;
        DrawPresetInputInt("Maximum Boon Chance", ref maxBoonChance, x => config.MaxBoonChance = x,
            "Maximum Boon Chance.");
        DrawActionConfigIntegrity(config);
    }

    private void DrawActionConfigIntegrity(ConfigPreset.ActionConfigIntegrity config)
    {
        var firstStepOnly = config.FirstStepOnly;
        DrawPresetCheckbox("First Step Only", ref firstStepOnly, x => config.FirstStepOnly = x,
            "Only use on first step.");

        var minIntegrity = config.MinIntegrity;
        DrawPresetInputInt("Minimum Integrity", ref minIntegrity, x => config.MinIntegrity = x, "Minimum Integrity.");
        DrawActionConfigSingle(config);
    }

    private void DrawActionConfigYieldTotal(ConfigPreset.ActionConfigYieldTotal config)
    {
        var total = config.MinYieldTotal;
        DrawPresetInputInt("Minimum Yield Total", ref total, x => config.MinYieldTotal = x, "Minimum Yield Total.");
        DrawActionConfigSingle(config);
    }

    private void DrawActionConfigYieldBonus(ConfigPreset.ActionConfigYieldBonus config)
    {
        var bonus = config.MinYieldBonus;
        DrawPresetInputInt("Minimum Yield Bonus", ref bonus, x => config.MinYieldBonus = x, "Minimum Yield Bonus.");
        DrawActionConfigSingle(config);
    }

    private void DrawActionConfigSingle(ConfigPreset.ActionConfig config)
    {
        var enabled = config.Enabled;
        DrawPresetCheckbox("Enabled", ref enabled, x => config.Enabled = x, "Enable this action.");

        var minGp = config.MinGP;
        DrawPresetInputInt("Minimum GP", ref minGp, x => config.MinGP = x, "Minimum GP for this action.");

        var maxGp = config.MaxGP;
        DrawPresetInputInt("Maximum GP", ref maxGp, x => config.MaxGP = x, "Maximum GP for this action.");
    }

    private void DrawPresetInputInt(string label, ref int value, Action<int> setter, string tooltip = "",
        int width = 100)
    {
        ImGui.SetNextItemWidth(width);
        if (ImGui.InputInt(label, ref value))
        {
            setter(value);
            _presetsSelector.Save();
        }

        ImGuiUtil.HoverTooltip(tooltip);
    }

    private void DrawPresetCheckbox(string label, ref bool value, Action<bool> setter, string tooltip = "",
        int width = 100)
    {
        ImGui.SetNextItemWidth(width);
        if (ImGui.Checkbox(label, ref value))
        {
            setter(value);
            _presetsSelector.Save();
        }

        ImGuiUtil.HoverTooltip(tooltip);
    }
}