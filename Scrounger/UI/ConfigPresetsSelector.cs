using ECommons.ImGuiMethods;
using GatherBuddy.Classes;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using OtterGui;
using OtterGui.Raii;
using Scrounger.AutoGather;

//Credit: https://github.com/FFXIV-CombatReborn/GatherBuddyReborn/blob/main/GatherBuddy/Gui/Interface.ConfigPresetsTab.cs
namespace Scrounger.UI;

public class ConfigPresetsSelector : ItemSelector<ConfigPreset>
{
    private const string FileName = "actions.json";

    public ConfigPresetsSelector()
        : base([], Flags.All ^ Flags.Drop)
    {
        Load();
    }

    public IReadOnlyCollection<ConfigPreset> Presets => Items.AsReadOnly();

    protected override bool Filtered(int idx)
        => Filter.Length != 0 && !Items[idx].Name.Contains(Filter, StringComparison.InvariantCultureIgnoreCase);

    protected override bool OnDraw(int idx)
    {
        using var id = ImRaii.PushId(idx);
        //using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.DisabledText.Value(), !Items[idx].Enabled);
        return ImGui.Selectable(Items[idx].Name, idx == CurrentIdx);
    }

    protected override bool OnDelete(int idx)
    {
        if (idx == Items.Count - 1) return false;

        Items.RemoveAt(idx);
        Save();
        return true;
    }

    protected override bool OnAdd(string name)
    {
        Items.Insert(Items.Count - 1, new()
        {
            Name = name,
        });
        Save();
        return true;
    }

    protected override bool OnClipboardImport(string name, string data)
    {
        var preset = ConfigPreset.FromBase64String(data);
        if (preset == null)
        {
            Notify.Error("Failed to load config preset from clipboard. Are you sure it's valid?");
            return false;
        }

        preset.Name = name;

        Items.Insert(Items.Count - 1, preset);
        Save();
        Notify.Success($"Imported config preset {preset.Name} from clipboard successfully.");
        return true;
    }

    protected override bool OnDuplicate(string name, int idx)
    {
        var preset = Items[idx] with { Name = name };
        Items.Insert(Math.Min(idx + 1, Items.Count - 1), preset);
        Save();
        return true;
    }

    protected override bool OnMove(int idx1, int idx2)
    {
        idx2 = Math.Min(idx2, Items.Count - 2);
        if (idx1 >= Items.Count - 1) return false;
        if (idx1 < 0 || idx2 < 0) return false;

        Utils.Functions.Move(Items, idx1, idx2);
        Save();
        return true;
    }

    public void Save()
    {
        var file = Utils.Functions.ObtainSaveFile(FileName);
        if (file == null)
            return;

        try
        {
            var text = JsonConvert.SerializeObject(Items, Formatting.Indented);
            File.WriteAllText(file.FullName, text);
        }
        catch (Exception e)
        {
            Scrounger.Log.Error($"Error serializing config presets data:\n{e}");
        }
    }

    private void Load()
    {
        List<ConfigPreset>? items = null;

        var file = Utils.Functions.ObtainSaveFile(FileName);
        if (file != null && file.Exists)
        {
            var text = File.ReadAllText(file.FullName);
            items = JsonConvert.DeserializeObject<List<ConfigPreset>>(text);
        }

        if (items != null && items.Count > 0)
        {
            foreach (var item in items)
            {
                Items.Add(item);
            }
        }
        else
        {
            Items.Add(new ConfigPreset());
        }

        Items[Items.Count - 1] = Items[Items.Count - 1].MakeDefault();

        void fixAction(ConfigPreset.ActionConfig action)
        {
            if (action.MaxGP == 0) action.MaxGP = ConfigPreset.MaxGP;
        }
    }
}