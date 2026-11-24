using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>F5 saves into a slot, F9 and the pause menu load it, restoring position, stored items and the hotbar.</summary>
    private async Task CheckQuickSaveAndLoadRestorePlayerAndChest(Thing chest) {
        await KeyStroke(Key.C);
        Require(ShownPage() == "Crafting", "The crafting panel must be open before quick save.");
        var savedPosition = world.Player;
        var savedSlots = Enumerable.Range(0, 9).Select(world.HotbarItem).ToArray(); int savedActiveSlot = world.ActiveHotbar;
        await KeyStroke(Key.F5);
        Require(startMenu.Visible, "F5 in a new game must open the save slots.");
        Require(FindButton(startMenu.Screen, "Slot 1 · empty") != null,
            "F5 in a new game must offer the empty first slot.");
        Require(FindButton(startMenu.Screen, "Autosave · empty") == null,
            "F5 in a new game must leave out the autosave.");
        await Click(FindButton(startMenu.Screen, "Slot 1 · empty")!);
        Require(Saves.SlotUsed(1), "Choosing a slot must write the save.");
        Require(!startMenu.Visible, "Choosing a slot must close the slot list.");
        Require(!view.Paused, "Choosing a slot must resume the game.");
        int savedWood = chest.Inventory["wood"];
        Require(savedWood > 0 && Hosted.Find(chest.Id)!.Inventory.Remove("wood", 1) == 1, "The chest must hold wood to take out after saving.");
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
        await Seconds(.25);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
        await Frames(); Require(world.Player.DistanceTo(savedPosition) > .1f, "The player should move after saving.");
        Require(view.Footpaths.WornGround > 0, "Walking about must have worn the ground.");
        await KeyStroke(Key.F9);
        // Half a second of play passes before the first footfall wears the loaded world's ground.
        Require(view.Footpaths.WornGround == 0, "A loaded game must start on unworn ground, not on the footpaths of the game before.");
        // Two footfalls' time spent standing still.
        await Seconds(1.2);
        Require(view.Footpaths.WornAt(world.Player) == 0, "Standing still must not wear the ground underfoot.");
        Require(world.Player.DistanceTo(savedPosition) < .01f && world.Things.Any(t => t.Id == chest.Id && t.Inventory["wood"] == savedWood), "F9 must restore position and stored items from disk.");
        Require(Enumerable.Range(0, 9).Select(world.HotbarItem).SequenceEqual(savedSlots) && world.ActiveHotbar == savedActiveSlot, "F5 and F9 must preserve hotbar references and selection.");
        Warp(Hosted, world.Player + Vector2.One); await CloseWithEsc();
        await KeyStroke(Key.Escape);
        await Click(FindButton(menu, "Load")!);
        Require(FindButton(startMenu.Screen, "Slot 2 · empty")!.Disabled, "The load screen must disable empty slots.");
        await Click(startMenu.Screen.GetChildren().OfType<Button>().First(b => b.Text.StartsWith("Slot 1 · ")));
        Require(!startMenu.Visible, "Loading from the pause menu must close the start menu.");
        Require(!menu.Visible, "Loading from the pause menu must close the pause menu.");
        Require(!view.Paused, "Loading from the pause menu must resume the world.");
        Require(world.Player.DistanceTo(savedPosition) < .01f, "Loading from the pause menu must restore the saved position.");
    }
}
