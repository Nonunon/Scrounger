using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using GatherBuddy.Classes;
using GatherBuddy.Interfaces;
using Newtonsoft.Json;
using OtterGui;
using Scrounger.Utils;

//Credit: https://github.com/FFXIV-CombatReborn/GatherBuddyReborn/blob/main/GatherBuddy/AutoGather/Lists/AutoGatherListsManager.cs
namespace Scrounger.AutoGather.Lists;

public partial class AutoGatherListsManager : IDisposable
{
    public event Action? ActiveItemsChanged;

    private const string FileName = "auto_gather_lists.json";
    private const string FileNameFallback = "gather_window.json";

    private readonly List<AutoGatherList> _lists = [];
    private readonly List<(Gatherable Item, uint Quantity, Guid PresetId)> _activeItems = [];
    private readonly List<(Gatherable Item, uint Quantity, Guid PresetId)> _fallbackItems = [];

    public ReadOnlyCollection<AutoGatherList> Lists => _lists.AsReadOnly();
    public ReadOnlyCollection<(Gatherable Item, uint Quantity, Guid PresetId)> ActiveItems => _activeItems.AsReadOnly();
    public ReadOnlyCollection<(Gatherable Item, uint Quantity, Guid PresetId)> FallbackItems => _fallbackItems.AsReadOnly();

    public AutoGatherListsManager() { }

    public void Dispose() { }

    public void SetActiveItems()
    {
        _activeItems.Clear();
        _fallbackItems.Clear();
        var items = _lists
            .Where(l => l.Enabled)
            .SelectMany(l => l.Items.Select(i => (Item: i, Quantity: l.Quantities[i], l.Fallback, l.PresetId)))
            .GroupBy(i => (i.Item, i.Fallback, i.PresetId))
            .Select(x => (x.Key.Item, Quantity: (uint)Math.Min(x.Sum(g => g.Quantity), uint.MaxValue), x.Key.Fallback, x.Key.PresetId));
        
        foreach (var (item, quantity, fallback, presetId) in items)
        {
            if (fallback)
            {
                _fallbackItems.Add((item, quantity, presetId));
            }
            else
            {
                _activeItems.Add((item, quantity, presetId));
            }
        }

        ActiveItemsChanged?.Invoke();
    }

    public void Save()
    {
        var file = Utils.Functions.ObtainSaveFile(FileName);
        if (file == null)
            return;

        try
        {
            var text = JsonConvert.SerializeObject(_lists.Select(p => new AutoGatherList.Config(p)), Formatting.Indented);
            File.WriteAllText(file.FullName, text);
        }
        catch (Exception e)
        {
            Scrounger.Log.Error($"Error serializing auto-gather lists data:\n{e}");
        }
    }

    public static AutoGatherListsManager Load()
    {
        var ret  = new AutoGatherListsManager();
        var file = Utils.Functions.ObtainSaveFile(FileName);
        var change = false;
        if (file is not { Exists: true })
        {
            file = Utils.Functions.ObtainSaveFile(FileNameFallback);
            if (file is not { Exists: true })
            {
                ret.Save();
                return ret;
            }
            change = true;
        }

        try
        {
            var text = File.ReadAllText(file.FullName);
            var data = JsonConvert.DeserializeObject<AutoGatherList.Config[]>(text)!;
            ret._lists.Capacity = data.Length;
            foreach (var cfg in data)
            {
                change |= AutoGatherList.FromConfig(cfg, out var list);
                ret._lists.Add(list);
            }

            if (change)
                ret.Save();
        }
        catch (Exception e)
        {
            Scrounger.Log.Error($"Error deserializing auto gather lists:\n{e}");
            Utils.Functions.BackupUnreadableFile(file.FullName, "gather lists");
            ret._lists.Clear(); // Start clean instead of keeping whatever loaded before the error
            ret.Save();
        }

        ret.SetActiveItems();
        return ret;
    }
}
