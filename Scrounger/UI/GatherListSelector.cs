using Dalamud.Bindings.ImGui;
using OtterGui;
using OtterGui.Raii;
using Scrounger.AutoGather.Lists;

namespace Scrounger.UI;

public class GatherListSelector : ItemSelector<AutoGatherList>
{
    private readonly Scrounger _plugin;

    public GatherListSelector(Scrounger plugin) : base(plugin.AutoGatherListsManager.Lists, Flags.All ^ Flags.Drop)
    {
        _plugin = plugin;
    }

    protected override bool OnDraw(int idx)
    {
        using var id = ImRaii.PushId(idx);
        return ImGui.Selectable(Items[idx].Name, idx == CurrentIdx);
    }

    protected override bool Filtered(int idx) => Filter.Length != 0 && Items[idx].Name.Contains(Filter, StringComparison.InvariantCultureIgnoreCase);

    protected override bool OnDelete(int idx)
    {
        _plugin.AutoGatherListsManager.DeleteList(idx);
        return true;
    }

    protected override bool OnAdd(string name)
    {
        var list = new AutoGatherList();
        list.Name = name;
        _plugin.AutoGatherListsManager.AddList(list);
        return true;
    }

    protected override bool OnMove(int idx1, int idx2)
    {
        _plugin.AutoGatherListsManager.MoveList(idx1, idx2);
        return true;
    }

    protected override bool OnClipboardImport(string name, string data)
    {
        return false;
    }

    protected override bool OnDuplicate(string name, int idx)
    {
        var list = Items[idx];
        list.Name = name;
        _plugin.AutoGatherListsManager.AddList(list);
        return true;
    }
}