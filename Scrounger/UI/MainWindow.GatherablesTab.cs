using System.Numerics;
using System.Text.RegularExpressions;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using GatherBuddy.Classes;
using GatherBuddy.Time;
using Dalamud.Bindings.ImGui;
using OtterGui;
using OtterGui.Raii;
using Scrounger.AutoGather.Lists;
using Scrounger.Utils;
using Scrounger.Utils.Extensions;

namespace Scrounger.UI;

public partial class MainWindow
{
    private GatherListSelector _selector;

    public void DrawGatherablesTab()
    {
        using var id = ImRaii.PushId("AutoGatherTab");
        using var tab = ImRaii.TabItem("Auto-Gather");
        if (!tab)
            return;
        ImGuiUtil.HoverTooltip("Get dem goodies");
        ImGui.BeginChild("GatherTab", new Vector2(0, 0), false);
        _selector.Draw(SelectorWidth);
        ImGui.SameLine();
        DrawGatherList(_selector.Current);
        ImGui.EndChild();
    }

    private void DrawGatherList(AutoGatherList? selectorCurrent)
    {
        using var id = ImRaii.PushId("GatherList");
        ImGui.BeginChild("GatherList", new Vector2(0, 0), true);
        if (selectorCurrent == null)
        {
            ImGui.Text("No gather list selected");
        }
        else
        {
            DrawGatherListInternal(selectorCurrent);
        }

        ImGui.EndChild();
    }

    private void DrawGatherListInternal(AutoGatherList selectorCurrent)
    {
        DrawItemAdd(selectorCurrent);
        ImGui.Separator();
        ImGui.BeginGroup();
        var enabled = selectorCurrent.Enabled;
        if (ImGui.Checkbox($"Enabled##list", ref enabled))
        {
            _plugin.AutoGatherListsManager.ToggleList(selectorCurrent);
        }

        ImGui.SameLine();
        var fallback = selectorCurrent.Fallback;
        if (ImGui.Checkbox($"Fallback##list", ref fallback))
        {
            selectorCurrent.Fallback = fallback;
        }
        DrawPresetSelector(selectorCurrent);
        ImGui.EndGroup();
        ImGui.Separator();
        ImGui.BeginGroup();

        if (!ImGui.BeginTable("##gatherablesTable", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.EndGroup();
            return;
        }
        ImGui.TableSetupColumn("Item");
        ImGui.TableSetupColumn("Availability");
        ImGui.TableSetupColumn("Preferred Location");
        ImGui.TableSetupColumn("Inventory Amount");
        ImGui.TableSetupColumn("Desired Quantity");
        ImGui.TableSetupColumn("Remove");

        ImGui.TableHeadersRow();
        foreach (var item in selectorCurrent.Items.ToList())
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text(item.Name.ToString());
            ImGui.TableSetColumnIndex(1);
            ImGui.Text(GetUptimeString(item));
            ImGui.TableSetColumnIndex(2);
            ImGui.Text("(Coming soon...)");
            ImGui.TableSetColumnIndex(3);
            ImGui.Text(item.GetInventoryCount().ToString());
            ImGui.TableSetColumnIndex(4);
            ImGui.SetNextItemWidth(100);
            var quantity = (int)selectorCurrent.Quantities[item];
            if (ImGui.InputInt($"##quantity{item.ItemId}", ref quantity))
            {
                _plugin.AutoGatherListsManager.ChangeQuantity(selectorCurrent, item, (uint)quantity);
            }

            ImGui.TableSetColumnIndex(5);
            if (ImGui.Button($"Remove##{item.ItemId}"))
            {
                _plugin.AutoGatherListsManager.RemoveItem(selectorCurrent, item);
            }
        }

        ImGui.EndTable();

        ImGui.EndGroup();
    }

    private void DrawPresetSelector(AutoGatherList selectorCurrent)
    {
        var selected = _presetsSelector.Presets.FirstOrDefault(p => p.Id == selectorCurrent.PresetId)?.Name ??
                       _presetsSelector.Presets.First().Name;
        using var combo = ImRaii.Combo("Gathering Preset", selected);
        if (combo)
        {
            var presets = _presetsSelector.Presets;
            foreach (var preset in presets)
            {
                if (ImGui.Selectable(preset.Name))
                {
                    selectorCurrent.PresetId = preset.Id;
                    _plugin.AutoGatherListsManager.Save();
                }
            }
        }
    }

    private string GetUptimeString(Gatherable item)
    {
        var time = (uint)Scrounger.Time.ServerTime.Time;
        foreach (var node in item.NodeList)
        {
            if (node.Times.AlwaysUp())
                return "Always Available";

            return node.Times.PrintHours();
        }

        return "Never Available";
    }

    private string _searchTerm = string.Empty;

    private void DrawItemAdd(AutoGatherList selectorCurrent)
    {
        ImGui.Text("Item Search");
        ImGui.SameLine();
        DrawImportButton(selectorCurrent);
        ImGui.InputText("##searchBar", ref _searchTerm, 100);

        ImGui.BeginChild($"ItemList", new Vector2(0, 100), true);
        if (!string.IsNullOrEmpty(_searchTerm))
        {
            var matchingItems = Scrounger.WorldData.Gatherables.Where(item =>
                item.Value.Name.ToString().Contains(_searchTerm, StringComparison.OrdinalIgnoreCase));

            if (matchingItems.Any())
            {
                foreach (var item in matchingItems)
                {
                    if (ImGui.Selectable(item.Value.Name.ToString()))
                    {
                        selectorCurrent.Add(item.Value, 1);
                        _plugin.AutoGatherListsManager.Save();
                        Svc.Log.Debug($"Added shopping list item: {item.Value.Name}");
                        _searchTerm = string.Empty;
                    }
                }
            }
        }
        else
        {
            ImGui.Text("Items will appear here when you enter a search term.");
        }

        ImGui.EndChild();
    }

    private void DrawImportButton(AutoGatherList list)
    {
        if (ImGui.Button("Populate Items from Clipboard"))
        {
            var clipboardText = ImGuiUtil.GetClipboardText();
            if (!string.IsNullOrEmpty(clipboardText))
            {
                try
                {
                    Dictionary<string, int> items = new Dictionary<string, int>();

                    // Regex pattern
                    var pattern = @"\b(\d+)x\s(.+)\b";
                    var matches = Regex.Matches(clipboardText, pattern);

                    // Loop through matches and add them to dictionary
                    foreach (Match match in matches)
                    {
                        var quantity = int.Parse(match.Groups[1].Value);
                        var itemName = match.Groups[2].Value;
                        items[itemName] = quantity;
                    }

                    foreach (var (itemName, quantity) in items)
                    {
                        var gatherable =
                            Scrounger.WorldData.Gatherables.Values.FirstOrDefault(g => g.Name[Svc.ClientState.ClientLanguage] == itemName);
                        if (gatherable == null || gatherable.NodeList.Count == 0)
                            continue;

                        list.Add(gatherable, (uint)quantity);
                    }

                    _plugin.AutoGatherListsManager.Save();

                    if (list.Enabled)
                        _plugin.AutoGatherListsManager.SetActiveItems();
                }
                catch (Exception e)
                {
                    ChatPrinter.PrintError("Error importing auto-gather list");
                }
            }
        }
        ImGuiUtil.HoverTooltip("Populate your list with items from the clipboard. Format: 10x Item Name. Uses the same format as Teamcraft, Artisan, Workshoppa, etc.");
    }
}