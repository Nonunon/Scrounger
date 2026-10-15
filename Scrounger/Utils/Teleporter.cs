using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Scrounger.Utils;

public static unsafe class Teleporter
{
    public static bool IsAttuned(uint aetheryte)
    {
        var teleport = Telepo.Instance();
        if (teleport == null)
        {
            Scrounger.Log.Error("Could not check attunement: Telepo is missing.");
            return false;
        }

        if (Svc.Objects.LocalPlayer == null)
            return true;
        teleport->UpdateAetheryteList();

        var endPtr = teleport->TeleportList.Last;
        for (var it = teleport->TeleportList.First; it != endPtr; ++it)
        {
            if (it->AetheryteId == aetheryte)
                return true;
        }

        return false;
    }

    public static bool Teleport(uint aetheryte)
    {
        if (IsAttuned(aetheryte))
        {
            Telepo.Instance()->Teleport(aetheryte, 0);
            return true;
        }
        ChatPrinter.Print("You must be attuned to the aetheryte to teleport there.");
        return false;
    }

    // Teleport without checking for attunement. Use at own risk.
    public static void TeleportUnchecked(uint aetheryte)
    {
        Telepo.Instance()->Teleport(aetheryte, 0);
    }
}