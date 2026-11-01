using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The save slots: slot 0 is the autosave, the rest are the player's.</summary>
public sealed class Saves(UiKit kit, Net net, IGameFlow flow) {
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

    /// <summary>F5: until a numbered slot is chosen, asks which slot to use; only the player who started the game saves it.</summary>
    public void QuickSave(StartMenu menu) {
        if (!flow.Owns) kit.Notify("The host saves the world, and your player with it."); else if (Slot > 0) Save(Slot); else menu.ShowSlots(true, menu.Close);
    }

    /// <summary>F9: until a numbered slot is chosen, asks which slot to use; a guest has nothing of its own to load.</summary>
    public void QuickLoad(StartMenu menu) {
        if (!flow.Owns) kit.Notify("Leave the host's game to load your own."); else if (Slot > 0) flow.LoadSlot(Slot); else menu.ShowSlots(false, menu.Close);
    }

    /// <summary>Asks the server to save the game to a slot, and it says how that went; the UI checks' world, which runs here, is saved here.</summary>
    public void Save(int n) {
        if (kit.World.Remote) net.Save(n); else kit.Notify(Write(kit.World, n));
        if (n > 0) Slot = n;
    }

    /// <summary>Server: writes the world to a slot and returns what to tell the player who asked.</summary>
    public static string Write(World world, int n) {
        try {
            using var file = Godot.FileAccess.Open(SlotPath(n), Godot.FileAccess.ModeFlags.Write); if (file == null) throw new InvalidOperationException("Cannot open save file");
            file.StoreString(world.Serialize());
            return $"Settlement saved · {SlotName(n)}.";
        } catch (Exception e) { return $"Save failed: {e.Message}"; }
    }

    /// <summary>The world in a slot, or why it cannot be loaded.</summary>
    public static (World? World, string? Error) Read(GameData data, int n) {
        try {
            if (!SlotUsed(n)) return (null, $"{SlotName(n)} is empty.");
            var world = new World(data, false);
            world.Deserialize(Godot.FileAccess.GetFileAsString(SlotPath(n)));
            return (world, null);
        } catch (Exception e) { return (null, $"Load failed: {e.Message}"); }
    }
}
