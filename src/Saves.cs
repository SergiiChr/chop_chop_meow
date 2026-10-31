using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The save slots: slot 0 is the autosave, the rest are the player's.</summary>
public sealed class Saves(UiKit kit, Net net, Action loaded) {
    public const int SlotCount = 3;

    /// <summary>Numbered slot that F5 and F9 use; 0 until the player saves to or loads one.</summary>
    public int Slot { get; set; }

    // Slot 1 keeps the original single-save file name, so older saves load as slot 1.
    public static string SlotPath(int n) => $"user://{Launch.FilePrefix}{(n == 0 ? "autosave" : n == 1 ? "settlement" : $"settlement-{n}")}.json";
    public static string SlotName(int n) => n == 0 ? "Autosave" : $"Slot {n}";
    public static bool SlotUsed(int n) => Godot.FileAccess.FileExists(SlotPath(n));
    private static long SavedAt(int n) => (long)Godot.FileAccess.GetModifiedTime(SlotPath(n));

    public static string SlotLabel(int n) {
        if (!SlotUsed(n)) return $"{SlotName(n)} · empty";
        long local = SavedAt(n) + Time.GetTimeZoneFromSystem()["bias"].AsInt64() * 60;
        return $"{SlotName(n)} · {Time.GetDatetimeStringFromUnixTime(local, true)[..16]}";
    }

    /// <summary>The most recently written slot, or -1 when there is none.</summary>
    public static int Newest() => Enumerable.Range(0, SlotCount + 1).Where(SlotUsed).OrderByDescending(SavedAt).DefaultIfEmpty(-1).First();

    /// <summary>The save the player was using, else the most recent one, or -1 when there is none.</summary>
    public int Latest() => Slot > 0 && SlotUsed(Slot) ? Slot : Newest();

    /// <summary>F5: until a numbered slot is chosen, asks which slot to use; a guest saves only its own player.</summary>
    public void QuickSave(StartMenu menu) {
        if (kit.World.Remote) { net.SaveGuest(); kit.Notify("Your player is saved on this computer. The host saves the world."); } else if (Slot > 0) Save(Slot); else menu.ShowSlots(true, menu.Close);
    }

    /// <summary>F9: until a numbered slot is chosen, asks which slot to use; a guest has nothing of its own to load.</summary>
    public void QuickLoad(StartMenu menu) {
        if (kit.World.Remote) kit.Notify("Leave the host's game to load your own."); else if (Slot > 0) Load(Slot); else menu.ShowSlots(false, menu.Close);
    }

    public void Save(int n) {
        try {
            using var file = Godot.FileAccess.Open(SlotPath(n), Godot.FileAccess.ModeFlags.Write); if (file == null) throw new InvalidOperationException("Cannot open save file");
            file.StoreString(kit.World.Serialize()); if (n > 0) Slot = n; kit.Notify($"Settlement saved · {SlotName(n)}.");
        } catch (Exception e) { kit.Notify($"Save failed: {e.Message}"); }
    }

    public bool Load(int n) {
        try {
            if (!SlotUsed(n)) { kit.Notify($"{SlotName(n)} is empty."); return false; }
            using var file = Godot.FileAccess.Open(SlotPath(n), Godot.FileAccess.ModeFlags.Read); kit.World.Deserialize(file.GetAsText());
            // Another world ends the game the guests are in.
            net.Leave();
            Slot = n; loaded();
            return true;
        } catch (Exception e) { kit.Notify($"Load failed: {e.Message}"); return false; }
    }
}
