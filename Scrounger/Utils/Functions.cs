using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using OtterGui.Classes;

namespace Scrounger.Utils;

public static class Functions
{
    // Clamps invalid indices to valid indices.
    public static bool Swap<T>(IList<T> list, int idx1, int idx2)
    {
        idx1 = Math.Clamp(idx1, 0, list.Count);
        idx2 = Math.Clamp(idx2, 0, list.Count);
        if (idx1 == idx2)
            return false;

        (list[idx1], list[idx2]) = (list[idx2], list[idx1]);
        return true;
    }

    public static FileInfo? ObtainSaveFile(string fileName)
    {
        var dir = new DirectoryInfo(Svc.PluginInterface.GetPluginConfigDirectory());
        if (dir.Exists)
            return new FileInfo(Path.Combine(dir.FullName, fileName));

        try
        {
            dir.Create();
        }
        catch (Exception e)
        {
            Scrounger.Log.Error($"Could not create save directory at {dir.FullName}:\n{e}");
            return null;
        }

        return new FileInfo(Path.Combine(dir.FullName, fileName));
    }

    // Copies a save file that failed to load next to the original, so the defaults saved over it later don't destroy it.
    public static string? BackupUnreadableFile(string path, string description)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var backup = $"{path}.unreadable-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            File.Copy(path, backup, false);
            Scrounger.Log.Warning($"Could not read {description}, a copy was saved to {backup}.");
            ChatPrinter.PrintError($"Could not read your {description}, so defaults are in use. "
              + $"The old file was kept as {Path.GetFileName(backup)}.");
            return backup;
        }
        catch (Exception e)
        {
            Scrounger.Log.Error($"Could not back up {path}:\n{e}");
            return null;
        }
    }

    public static bool CompareCi(string lhs, string rhs)
        => string.Compare(lhs, rhs, StringComparison.InvariantCultureIgnoreCase) == 0;

    public static bool TryParseBoolean(string text, out bool parsed)
    {
        parsed = false;
        if (text.Length == 1)
        {
            if (text[0] != '1')
                return text[0] == '0';

            parsed = true;
            return true;
        }

        if (!CompareCi(text,       "on") && !CompareCi(text, "true"))
            return CompareCi(text, "off") || CompareCi(text, "false");

        parsed = true;
        return true;
    }

    [DllImport("msvcrt.dll")]
    private static extern unsafe int memcmp(byte* b1, byte* b2, int count);

    public static unsafe bool MemCmpUnchecked(byte* ptr1, byte* ptr2, int count)
        => memcmp(ptr1, ptr2, count) == 0;

    public static unsafe bool MemCmp(byte* ptr1, byte* ptr2, int length1, int length2)
    {
        if (length1 != length2)
            return false;

        if (ptr1 == ptr2 || length1 == 0)
            return true;

        if (ptr1 == null || ptr2 == null)
            return false;

        return memcmp(ptr1, ptr2, length1) == 0;
    }

    public static string CompressedBase64(byte[] data)
    {
        using var compressedStream = new MemoryStream();
        using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
        {
            zipStream.Write(data, 0, data.Length);
        }

        return Convert.ToBase64String(compressedStream.ToArray());
    }

    public static byte[] DecompressedBase64(string compressedBase64)
    {
        var       data             = Convert.FromBase64String(compressedBase64);
        using var compressedStream = new MemoryStream(data);
        using var zipStream        = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var resultStream     = new MemoryStream();
        zipStream.CopyTo(resultStream);
        return resultStream.ToArray();
    }

    public static bool BoundByDuty()
        => !InIslandSanctuary() && (Svc.Condition[ConditionFlag.BoundByDuty]
         || Svc.Condition[ConditionFlag.WatchingCutscene]
         || Svc.Condition[ConditionFlag.BoundByDuty56]
         || Svc.Condition[ConditionFlag.BoundByDuty95]
         || Svc.Condition[ConditionFlag.WatchingCutscene78]
         || Svc.Condition[ConditionFlag.InDeepDungeon]);

    public static bool BetweenAreas()
        => Svc.Condition[ConditionFlag.BetweenAreas]
         || Svc.Condition[ConditionFlag.BetweenAreas51];

    public static bool InIslandSanctuary()
        => Svc.Data.GetExcelSheet<TerritoryType>()
            .GetRowOrDefault(Svc.ClientState.TerritoryType)?.TerritoryIntendedUse.RowId == 49;

    public static bool Move<T>(IList<T> list, int idx1, int idx2)
    {
        idx1 = Math.Clamp(idx1, 0, list.Count - 1);
        idx2 = Math.Clamp(idx2, 0, list.Count - 1);
        if (idx1 == idx2)
            return false;

        var tmp = list[idx1];
        // move element down and shift other elements up
        if (idx1 < idx2)
            for (var i = idx1; i < idx2; i++)
                list[i] = list[i + 1];
        // move element up and shift other elements down
        else
            for (var i = idx1; i > idx2; i--)
                list[i] = list[i - 1];
        list[idx2] = tmp;
        return true;
    }

    public static bool CheckModifier(ModifierHotkey key, bool noKey)
        => key.Modifier switch
        {
            VirtualKey.CONTROL => Svc.KeyState[VirtualKey.CONTROL],
            VirtualKey.SHIFT   => Svc.KeyState[VirtualKey.SHIFT],
            VirtualKey.MENU    => Svc.KeyState[VirtualKey.MENU],
            VirtualKey.NO_KEY  => noKey,
            _                  => false,
        };

    public static bool CheckKeyState(ModifiableHotkey key, bool noKey)
    {
        if (key.Hotkey == VirtualKey.NO_KEY)
            return noKey;

        return Svc.KeyState[key.Hotkey] && CheckModifier(key.Modifier1, true) && CheckModifier(key.Modifier2, true);
    }
}
