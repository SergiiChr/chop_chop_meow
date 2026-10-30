using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

public partial class Game {
    private async Task SurvivalReviewShot(string name) {
        if (LaunchValue("--review-captures") is not string folder) return;
        System.IO.Directory.CreateDirectory(folder);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(folder, name + ".png")) == Error.Ok,
            "The rendered survival review capture must save successfully.");
    }

    /// <summary>Exercises survival interactions through the same UI and input path a player uses.</summary>
    private async Task CheckSurvivalInventoryUI() {
        string savedWorld = world.Serialize();
        bool wasPaused = paused;
        string oldPage = page;
        var oldBagVisible = bag.Visible;
        try {
            paused = false;
            ClosePanel();
            gathering = null;
            world.S.Backpack.Clear();
            world.S.HotbarSlots[8] = "water";

            // A real source-ring pick and a held right mouse button refill the starter bottle.
            world.S.Things.RemoveAll(t => t.Kind == "water");
            world.Add("water", world.Player + Vector2.Right * 20, 9999);
            world.S.Backpack.Remove("water", 99);
            world.S.Backpack.Remove("water_1", 99);
            world.S.Backpack.Remove("water_2", 99);
            world.S.Backpack.Add("empty_bottle", 1);
            var shore = Terrain.WaterPoint(world.Player);
            world.Player = shore + (shore - Terrain.Lake).Normalized() * 1.8f;
            await Frames(4);
            var shorePoint = view.Screen(shore, .8f);
            Require(view.Pick(shorePoint)?.Kind == "water", "The visible shoreline ring must pick the water source.");
            var windowPoint = GetViewport().GetFinalTransform() * shorePoint;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
            await Until(() => world.S.Backpack["water"] == 1 && world.S.Backpack["empty_bottle"] == 0, "holding right mouse on the shoreline filling the retained bottle", world.Data.GatherSeconds + 3);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = false });
            await Frames(3);

            OpenPage("inventory"); await Frames(3); await SurvivalReviewShot("filled-bottle"); ClosePanel();

            // Drinking through hotbar slot nine consumes one sip at a time and keeps the empty bottle.
            for (int expected = 3; expected >= 1; expected--) {
                Require(hotbarCounts[8].Text == $"{expected}/3", $"The bottle badge must show {expected}/3 before a sip.");
                await Click(hotbarButtons[8]);
                await ClickGround();
                RefreshHotbar();
                Require(hotbarCounts[8].Text == $"{expected - 1}/3", "A hotbar drink must remove exactly one sip.");
            }
            Require(world.S.Backpack["empty_bottle"] == 1, "Three hotbar sips must leave the empty bottle in inventory.");

            // Seed distinct stacks so the slot drag can prove both the exact target and untouched cells.
            ClosePanel();
            var bagItems = world.S.Backpack;
            bagItems.Clear();
            bagItems.Add("wood", 7); bagItems.Add("stone", 3); bagItems.Add("berries", 5);
            OpenPage("inventory"); await Frames(3);
            ItemSlot wood = Cell(0, c => c.TooltipText.StartsWith("Wood ·", StringComparison.Ordinal));
            var woodIndex = Array.IndexOf(bag.FindChildren("*", "Button", true, false).OfType<ItemSlot>().ToArray(), wood);
            ItemSlot target = bag.FindChildren("*", "Button", true, false).OfType<ItemSlot>().First(c => c.TooltipText == "");
            var targetIndex = Array.IndexOf(bag.FindChildren("*", "Button", true, false).OfType<ItemSlot>().ToArray(), target);
            Require(woodIndex >= 0 && targetIndex >= 0 && targetIndex != woodIndex, "The inventory must expose source and empty destination slots.");
            await DragTo(wood, target);
            Require(bagItems.Layout[targetIndex].Item == "wood" && bagItems.Layout[targetIndex].Count == 7 && bagItems["stone"] == 3 && bagItems["berries"] == 5,
                "Dragging a stack into an exact empty backpack slot must preserve its quantity and the other stacks.");
            await Click((Control)bag.FindChild("sort_bag", true, false));
            var ordered = bagItems.Layout.Where(s => s.Item != "").Select(s => s.Item).ToArray();
            Require(ordered.SequenceEqual(ordered.OrderBy(id => id, StringComparer.Ordinal)), "The backpack Sort button must order stacks by item name.");

            await SurvivalReviewShot("sorted-inventory");

            // A normal click moves one unit; Ctrl-click moves the entire remaining stack.
            ClosePanel();
            world.S.Backpack.Add("coal", 5);
            var chest = world.Add("chest", world.Player + Vector2.Right);
            chest.Inventory.Add("coal", 5);
            Select(chest); await Frames(3);
            await Click(Cell(0, c => c.TooltipText.StartsWith("Mined coal ·", StringComparison.Ordinal)));
            Require(chest.Inventory["coal"] == 4 && world.S.Backpack["coal"] == 6,
                "Clicking a storage stack must move exactly one item from storage to the backpack.");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = true });
            try { await Click(Cell(0, c => c.TooltipText.StartsWith("Mined coal ·", StringComparison.Ordinal))); } finally { Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = false }); }
            Require(chest.Inventory["coal"] == 0 && world.S.Backpack["coal"] == 10,
                "Ctrl-clicking a storage stack must take all remaining units.");
            await Click(Cell(1, c => c.TooltipText.StartsWith("Mined coal ·", StringComparison.Ordinal)));
            Require(chest.Inventory["coal"] == 1 && world.S.Backpack["coal"] == 9,
                "Clicking one unit in the backpack must store exactly one unit.");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = true });
            try { await Click(Cell(1, c => c.TooltipText.StartsWith("Mined coal ·", StringComparison.Ordinal))); } finally { Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = false }); }
            Require(chest.Inventory["coal"] == 10 && world.S.Backpack["coal"] == 0,
                "Ctrl-clicking a backpack stack must store all remaining units.");

            ClosePanel();
            world.S.Backpack.Clear();
            world.S.Backpack.Add("water_2", 1);
            var table = world.Add("cooktable", world.Player + Vector2.Right);
            world.SetRecipe(table, "dough");
            Select(table); await Frames(3);
            await Click(Cell(1, c => c.TooltipText.StartsWith(world.Data.Name("water_2") + " ·", StringComparison.Ordinal)));
            Require(waterWarning.Text == Water.FullBottleWarning && waterWarning.GetGlobalRect().Position.X < 50 && waterWarning.Size.X > 300 && waterWarning.Size.Y < 50 && waterWarning.GetGlobalRect().End.Y < bag.GetGlobalRect().Position.Y,
                "Trying a partial bottle must show the full-bottle warning at the bottom left.");
            var partial = Cell(1, c => c.TooltipText.StartsWith(world.Data.Name("water_2") + " ·", StringComparison.Ordinal));
            await DragTo(partial, (Control)ui.FindChild("input_water", true, false));
            Require(table.Inventory["water_2"] == 0 && world.S.Backpack["water_2"] == 1 && waterWarning.Text == Water.FullBottleWarning,
                "Dragging a partial bottle onto a recipe water slot must warn without using it.");
            await SurvivalReviewShot("water-warning");

            // A downed player without a cabin may choose dawn; any cabin restores normal respawning.
            ClosePanel();
            world.S.Things.RemoveAll(t => t.Kind == "hut");
            world.S.Time = world.Data.DaySeconds * .9f;
            world.S.RespawnIn = 30;
            RefreshRecoveryPrompt(); RefreshLive(); await Frames(2);
            Require(recoveryPrompt is { Visible: true } && skipMorningButton is { Disabled: false }, "A downed player without a cabin must see an enabled Skip to morning prompt.");
            await SurvivalReviewShot("recovery-prompt");
            float previousDay = world.Day;
            await Click(skipMorningButton!);
            Require(world.S.RespawnIn == 0 && world.Day > previousDay && world.S.Time % world.Data.DaySeconds < world.Data.DaySeconds * .1f,
                "Skipping morning must advance the simulated world to dawn and complete respawn.");
            world.Add("hut", world.Player + Vector2.Right);
            world.S.RespawnIn = 30;
            RefreshRecoveryPrompt(); RefreshLive(); await Frames(2);
            Require(recoveryPrompt is { Visible: false }, "A downed player with a cabin must not be offered Skip to morning.");
        } finally {
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = 0, Pressed = false });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = false });
            world.Deserialize(savedWorld);
            gathering = null;
            ClosePanel();
            if (oldPage != "") OpenPage(oldPage);
            bag.Visible = oldBagVisible;
            paused = wasPaused;
            RefreshRecoveryPrompt();
            await Frames(2);
        }
    }
}
