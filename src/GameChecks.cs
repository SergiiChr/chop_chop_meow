using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace ChopChopMeow;

public partial class Game
{
    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async Task Seconds(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task KeyStroke(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        await Frames();
    }
    private Button? FindButton(Node parent, string text)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is Button b && b.Text == text) return b;
            var found = FindButton(child, text); if (found != null) return found;
        }
        return null;
    }
    private Button? Named(string name) => panel.FindChild(name, true, false) as Button;
    private bool HasLabel(Node parent, string text)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is Label label && label.Text.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
            if (HasLabel(child, text)) return true;
        }
        return false;
    }
    /// <summary>Window position of a control's centre, scrolled into view first.</summary>
    private async Task<Vector2> At(Control control)
    {
        for (Node? n = control.GetParent(); n != null; n = n.GetParent()) if (n is ScrollContainer scroll) scroll.EnsureControlVisible(control);
        await Frames();
        return GetViewport().GetFinalTransform() * control.GetGlobalRect().GetCenter();
    }
    private async Task Click(Control control)
    {
        var at = await At(control);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = at, GlobalPosition = at, Pressed = false });
        await Frames();
    }
    /// <summary>A real mouse drag: press, move in steps past the drag threshold, release over the target.</summary>
    private async Task DragTo(Control from, Control to)
    {
        var start = await At(from); var end = await At(to);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = start, GlobalPosition = start, Pressed = true });
        await Frames();
        for (int i = 1; i <= 8; i++)
        {
            var at = start.Lerp(end, i / 8f);
            Input.ParseInputEvent(new InputEventMouseMotion { ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Relative = (end - start) / 8 });
            await Frames(1);
        }
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = end, GlobalPosition = end, Pressed = false });
        await Frames();
    }
    private ItemSlot Cell(int grid, Func<ItemSlot, bool> match) => panel.FindChildren("*", "GridContainer", true, false)[grid].GetChildren().OfType<ItemSlot>().First(match);
    private static void UIRequire(bool okay, string message)
    {
        if (!okay) throw new InvalidOperationException("UI check failed: " + message);
    }
    private static void RemoveCheckFiles()
    {
        foreach (var path in Enumerable.Range(0, SlotCount + 1).Select(SlotPath).Append(SettingsPath)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }
    private async void RunUIChecks()
    {
        try
        {
            // A failed earlier run may have left saves behind, which would enable Continue.
            RemoveCheckFiles(); ShowStart(); await Frames(4);
            UIRequire(backdrop.Visible && paused && FindButton(screen, "New game") != null && FindButton(screen, "Continue")!.Disabled, "Launch must open the start menu, hold the world and offer no save to continue.");
            float launchTime = world.S.Time; await KeyStroke(Key.Escape);
            UIRequire(backdrop.Visible && world.S.Time == launchTime, "Esc must not leave the start menu, and the world must not advance behind it.");
            await Click(FindButton(screen, "Options")!);
            var picker = (OptionButton)screen.FindChild("resolution", true, false);
            UIRequire(picker.Selected == Array.IndexOf(Resolutions, new Vector2I(1440, 900)), "Options must show the default resolution before one is chosen.");
            picker.Select(0); picker.EmitSignal(OptionButton.SignalName.ItemSelected, 0L); await Frames();
            UIRequire(SavedResolution() == Resolutions[0], "Choosing a resolution must store it in the settings file.");
            await KeyStroke(Key.Escape);
            UIRequire(FindButton(screen, "New game") != null, "Esc in options must return to the start menu.");
            await Click(FindButton(screen, "Load game")!);
            UIRequire(Enumerable.Range(0, SlotCount + 1).All(n => FindButton(screen, $"{SlotName(n)} · empty")!.Disabled), "Empty save slots must not be loadable.");
            await Click(FindButton(screen, "Back · Esc")!);
            await Click(FindButton(screen, "New game")!);
            UIRequire(!backdrop.Visible && !paused && page == "welcome", "New game must close the start menu and show the welcome page.");
            ClosePanel();
            await KeyStroke(Key.Escape);
            UIRequire(menu.Visible && paused && HasLabel(menu, World.ObjectiveFor(world.S.Goal).Title), "Esc must open the pause menu with the current goal.");
            await KeyStroke(Key.Escape);
            UIRequire(!menu.Visible && !paused, "Esc must close the pause menu and resume.");
            var start = world.Player;
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
            await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
            UIRequire(view.PlayerAnimation == ActorAction.Walk, "Movement must select the full walking cycle.");
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
            await Frames(); UIRequire(world.Player.DistanceTo(start) > .25f, "WASD must move the player through Godot input.");
            UIRequire(sounds.Last == "footsteps_default", "Walking on grass must play grass footsteps.");
            UIRequire(Sounds.Names.All(name => sounds.VariantCount(name) > 0) && sounds.VariantCount("footsteps_default") == 3 && sounds.VariantCount("chop_tree") == 1, "Every sound must load, with numbered files grouped as variants.");
            var picks = Enumerable.Range(0, 12).Select(_ => { sounds.Play("anvil_work"); return sounds.LastVariant("anvil_work"); }).ToArray();
            UIRequire(picks.Zip(picks.Skip(1)).All(p => p.First != p.Second) && picks.Distinct().Count() == 3, "Repeated sounds must rotate through their variants without repeating the previous one.");
            int heard = sounds.Played; sounds.Play("anvil_work", world.Player + new Vector2(Sounds.Range + 1, 0));
            UIRequire(sounds.Played == heard, "A sound out of range of the player must stay silent.");
            var tree = world.S.Things.First(t => t.Kind == "tree" && t.Pos == new Vector2(28, 24));
            int wood = world.S.Backpack["wood"];
            var point = view.Screen(tree.Pos) - new Vector2(0, 70);
            UIRequire(view.Pick(point)?.Id == tree.Id, "Illustrated tree hit area must select the visible resource.");
            var windowPoint = GetViewport().GetFinalTransform() * point;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
            await Frames(3);
            await ToSignal(GetTree().CreateTimer(4), SceneTreeTimer.SignalName.Timeout);
            UIRequire(view.PlayerAnimation == ActorAction.Chop, "RMB gathering with the starting axe must select the axe stroke.");
            UIRequire(sounds.Last == "chop_tree", "Axe strokes on a tree must play the chop sound.");
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = 0, Position = windowPoint, Pressed = false });
            await Frames(); UIRequire(world.S.Backpack["wood"] > wood, "Holding RMB must approach and gather a real resource.");
            UIRequire(!stats.Text.Contains("Health") && !stats.Text.Contains("Food") && !stats.Text.Contains("Water") && !stats.Text.Contains("coins") && !stats.Text.Contains("Pack"), "The persistent HUD must show only the day and clock beside the brand.");
            UIRequire(hotbarButtons.Count == 9 && world.S.HotbarSlots.Count == 9, "The bottom toolbar must show nine saved hotbar references.");
            int berries = world.S.Backpack["berries"];
            await KeyStroke(Key.Key3);
            UIRequire(view.BuildMode == "chest", "A building-plan hotkey must enter placement mode.");
            int played = sounds.Played;
            await KeyStroke(Key.Key8);
            UIRequire(world.S.Backpack["berries"] == berries - 1, "Key 8 must consume the berries assigned to hotbar slot 8.");
            UIRequire(sounds.Played == played + 1 && sounds.Last == "eat_berry", "Eating must play only the eating sound, not an inventory sound as well.");
            UIRequire(view.BuildMode == "", "Using a consumable must clear any previously selected building plan.");
            world.S.Backpack.Add("water", 1); world.S.Thirst = 20;
            await KeyStroke(Key.Key9);
            UIRequire(world.S.Backpack["water"] == 0 && world.S.Thirst > 20, "Key 9 must consume carried water and restore thirst.");
            await KeyStroke(Key.B); UIRequire(page == "build" && panel.Visible, "B must open the building plans.");
            var plan = Named("build_chest"); UIRequire(plan != null, "Building plans must expose storage construction.");
            plan!.EmitSignal(Control.SignalName.MouseEntered); await Frames();
            UIRequire(HasLabel(panel, "Needs: Wood") && HasLabel(panel, "18 slots"), "Hovering a plan must show its description and required resources.");
            plan.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            UIRequire(view.BuildMode == "chest" && !panel.Visible, "Choosing a plan must enter placement mode.");
            Vector2 place = Vector2.Zero;
            for (int x = -3; x <= 3; x++) for (int y = -3; y <= 3; y++)
            {
                var candidate = world.Player.Round() + new Vector2(x, y);
                if (world.Player.DistanceTo(candidate) <= 3 && world.PlacementBlock("chest", candidate) == null) place = candidate;
            }
            UIRequire(place != Vector2.Zero, "A fresh clearing must offer space for construction.");
            point = view.Screen(place);
            windowPoint = GetViewport().GetFinalTransform() * point;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
            await Frames();
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = windowPoint, Pressed = false });
            await Frames();
            var chest = world.S.Things.LastOrDefault(t => t.Kind == "chest");
            UIRequire(chest != null && view.BuildMode == "" && selected == chest.Id, "A ground click must construct and select the chest.");
            var deposit = FindButton(panel, "Deposit all materials (keep tools)"); UIRequire(deposit != null, "Chest context must expose deposit action.");
            deposit!.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            UIRequire(chest!.Inventory["wood"] > 0 && world.S.Backpack["iron_axe"] == 1, "Manual deposit must move materials and retain equipped tools.");
            int storedWood = chest.Inventory["wood"]; RefreshLive();
            await DragTo(Cell(0, c => c.TooltipText.StartsWith("Wood")), Cell(1, c => c.TooltipText == ""));
            UIRequire(chest.Inventory["wood"] == 0 && world.S.Backpack["wood"] == storedWood, "Dragging a chest stack onto the backpack must move it.");
            RefreshLive();
            await DragTo(Cell(1, c => c.TooltipText.StartsWith("Wood")), hotbarButtons[3]);
            UIRequire(world.S.HotbarSlots[3] == "wood" && world.S.Backpack["wood"] == storedWood, "Dragging an item onto the hotbar must assign a reference without moving items.");
            await DragTo(Cell(1, c => c.TooltipText.StartsWith("Wood")), Cell(0, c => c.TooltipText == ""));
            UIRequire(chest.Inventory["wood"] == storedWood, "Dragging a backpack stack onto the chest must store it.");
            var filters = FindButton(panel, "Input / output filters"); filters!.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            UIRequire(page == "filters", "Chest filters must be reachable from the live context UI.");
            await KeyStroke(Key.I);
            FindButton(panel, "Assign hotbar slots")!.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            UIRequire(page == "hotbar", "Inventory must expose hotbar assignment.");
            FindButton(panel, "1")!.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            int carriedBeforeAssignment = world.S.Backpack.Count;
            Named("assign_hut")!.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            UIRequire(world.S.HotbarSlots[0] == "hut" && world.S.Backpack.Count == carriedBeforeAssignment, "Rebinding a hotbar slot must store a plan reference without creating items.");
            await KeyStroke(Key.Tab); UIRequire(page == "help", "Tab must open the field guide.");
            var savedPosition = world.Player;
            var savedSlots = world.S.HotbarSlots.ToArray(); int savedActiveSlot = world.S.ActiveHotbar;
            await KeyStroke(Key.F5);
            UIRequire(backdrop.Visible && FindButton(screen, "Slot 1 · empty") != null && FindButton(screen, "Autosave · empty") == null, "F5 in a new game must ask for a save slot and leave out the autosave.");
            await Click(FindButton(screen, "Slot 1 · empty")!);
            UIRequire(SlotUsed(1) && !backdrop.Visible && !paused, "Choosing a slot must write the save and resume.");
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
            await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
            await Frames(); UIRequire(world.Player.DistanceTo(savedPosition) > .1f, "The player should move after saving.");
            await KeyStroke(Key.F9);
            UIRequire(world.Player.DistanceTo(savedPosition) < .01f && world.S.Things.Any(t => t.Id == chest.Id && t.Inventory["wood"] > 0), "F9 must restore position and stored items from disk.");
            UIRequire(world.S.HotbarSlots.SequenceEqual(savedSlots) && world.S.ActiveHotbar == savedActiveSlot, "F5 and F9 must preserve hotbar references and selection.");
            world.Player += Vector2.One; ClosePanel();
            await KeyStroke(Key.Escape);
            await Click(FindButton(menu, "Load")!);
            UIRequire(FindButton(screen, "Slot 2 · empty")!.Disabled, "The load screen must disable empty slots.");
            await Click(screen.GetChildren().OfType<Button>().First(b => b.Text.StartsWith("Slot 1 · ")));
            UIRequire(!backdrop.Visible && !menu.Visible && !paused && world.Player.DistanceTo(savedPosition) < .01f, "Loading from the pause menu must restore the save and resume.");
            UIRequire(HasLabel(panel, "Coins") && HasLabel(panel, "Health") && HasLabel(panel, "Capacity") && HasLabel(panel, "Daily wages"), "Inventory must contain coins, health, capacity and wages.");
            UIRequire(!HasLabel(panel, "Hunger") && !HasLabel(panel, "Thirst"), "Hunger and thirst belong to the HUD icons, not the inventory.");
            world.S.Hunger = 40; world.S.Thirst = 70; RefreshLive();
            UIRequire(hunger.Visible && thirst.Visible && hunger.TooltipText == "Hunger 40%" && thirst.TooltipText == "Thirst 70%", "The HUD stomach and droplet must track hunger and thirst.");
            UIRequire(panel.FindChildren("slot_*", "Button", true, false).Count == world.S.Backpack.Slots, "The backpack must show one grid cell per slot.");
            world.S.Backpack.Add("berries", 2); RefreshLive();
            var berryCell = panel.FindChildren("slot_*", "Button", true, false).OfType<Button>().FirstOrDefault(c => c.TooltipText.StartsWith("Berries"));
            int berriesBeforeClick = world.S.Backpack["berries"];
            UIRequire(berryCell != null, "Carried berries must appear as a grid cell.");
            berryCell!.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            UIRequire(world.S.Backpack["berries"] == berriesBeforeClick - 1, "Clicking a berry cell must eat one berry.");

            float dayTime = world.S.Time;
            world.S.Time = world.Data.DaySeconds * (world.Data.NightStarts + .02f); world.S.Mauled = true;
            var pinnedAt = world.Player;
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
            await Seconds(.25);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
            await Frames();
            UIRequire(world.Player == pinnedAt && view.PlayerAnimation == ActorAction.Mauled && deathLabel.Text.Contains("throat"), "A pinned player must not move and must show the mauled struggle.");
            world.S.Mauled = false; world.S.Wolves.Clear(); world.S.Time = dayTime; world.S.Health = 100;
            world.S.Health = -1; await Seconds(.3);
            UIRequire(world.Dead && deathLabel.Text.Contains("Respawning in"), "Death must show the respawn countdown.");
            world.S.RespawnIn = .01f; await Seconds(.3);
            UIRequire(!world.Dead && deathLabel.Text == "", "The countdown must clear once the player respawns.");

            world.S.Milestones.UnionWith(new[] { "gather", "furnace", "iron_bar", "anvil", "pick", "reinforced", "steel_bar", "steel_tools", "sold_steel", "hired", "assigned" });
            await Frames(3);
            UIRequire(ending.Visible && paused, "Completing the goal chain must show the ending and pause the world.");
            ((Button)ending.FindChild("keep_playing", true, false)).EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
            UIRequire(!ending.Visible && !paused && world.S.EndingShown, "Keep playing must close the ending and resume.");
            await Frames(3);
            UIRequire(!ending.Visible, "The ending must not reappear after the player chose to keep playing.");
            ClosePanel();
            var furnace = world.Add("reinforced", world.Player + new Vector2(1, 0));
            Select(furnace);
            UIRequire(Named("input_iron_bar") != null && Named("input_coal") != null && Named("output_steel_bar") != null, "The steel furnace must show one slot per input and output.");
            world.S.Backpack.Add("iron_bar", 5); world.S.Backpack.Add("coal", 100); RefreshLive();
            await DragTo(Cell(0, c => c.TooltipText.StartsWith("Iron bar")), Named("input_iron_bar")!);
            UIRequire(furnace.Inventory["iron_bar"] == 5 && world.S.Backpack["iron_bar"] == 0, "Dragging bars onto the matching input slot must load them.");
            await DragTo(Cell(0, c => c.TooltipText.StartsWith("Mined coal")), Named("input_iron_bar")!);
            UIRequire(furnace.Inventory["coal"] == 0, "An input slot must reject a different item.");
            await Click(Cell(0, c => c.TooltipText.StartsWith("Mined coal")));
            UIRequire(furnace.Inventory["coal"] == furnace.Inventory.StackSize, "Clicking a backpack item must fill its input slot up to one stack.");
            await Click(Named("input_iron_bar")!);
            UIRequire(furnace.Inventory["iron_bar"] == 0 && world.S.Backpack["iron_bar"] == 5, "Clicking an input slot must return its stack.");
            await KeyStroke(Key.Space);
            UIRequire(view.PlayerAnimation == ActorAction.Pump, "Manual bellows input must trigger its dedicated cycle.");
            await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
            await Frames();
            UIRequire(view.PlayerAnimation != ActorAction.Pump, "The bellows action pulse should finish before pause checks.");
            ClosePanel();
            await KeyStroke(Key.Escape);
            UIRequire(paused, "Escape must pause when no panel is open.");
            float heatBeforePausedActions = furnace.Heat;
            var actionBeforePausedActions = view.PlayerAnimation;
            float pausedActionTime = view.PlayerAnimationTime;
            await KeyStroke(Key.Space);
            UIRequire(Math.Abs(furnace.Heat - heatBeforePausedActions) < .001f, "Space must not pump or attack while paused.");
            Select(furnace);
            var pumpButton = FindButton(panel, "Pump bellows · Space");
            UIRequire(pumpButton != null, "The selected furnace should still expose its pump control while paused.");
            pumpButton!.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            UIRequire(Math.Abs(furnace.Heat - heatBeforePausedActions) < .001f, "The pump button must not change heat while paused.");
            UIRequire(view.PlayerAnimation == actionBeforePausedActions && Math.Abs(view.PlayerAnimationTime - pausedActionTime) < .001f, "Paused Space and pump-button input must not start an animation pulse.");
            ClosePanel();
            float animationTime = view.PlayerAnimationTime;
            await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
            UIRequire(Math.Abs(view.PlayerAnimationTime - animationTime) < .001f, "Pausing must freeze actor animation clocks.");
            await KeyStroke(Key.Escape);
            UIRequire(!paused, "Escape must resume after animation pause check.");
            await Frames();
            UIRequire(view.PlayerAnimation != ActorAction.Pump, "Actions rejected during pause must not play delayed pulses after resume.");
            var occluded = new Thing { Kind = "reinforced", Pos = world.Player };
            var foregroundTree = new Thing { Kind = "tree", Pos = world.Player + Vector2.One };
            UIRequire(view.TreeCanopyCoversThing(foregroundTree, occluded), "A foreground canopy overlapping a furnace must fade.");
            foregroundTree.Pos = world.Player - Vector2.One;
            UIRequire(!view.TreeCanopyCoversThing(foregroundTree, occluded), "Trees behind buildings must retain their opacity.");
            foregroundTree.Pos = world.Player + new Vector2(8, -8);
            UIRequire(!view.TreeCanopyCoversThing(foregroundTree, occluded), "A distant tree must not fade for a building.");
            ClosePanel(); selected = 0;
            world.S.Backpack.Remove("iron_axe", 99); world.S.Backpack.Remove("steel_tools", 99);
            await KeyStroke(Key.Space);
            UIRequire(view.PlayerAnimation == ActorAction.Attack, "Space without an axe must trigger the dedicated unarmed attack.");
            await ToSignal(GetTree().CreateTimer(1.1), SceneTreeTimer.SignalName.Timeout);
            var gatheringPosition = new Vector2(8, 8);
            // Full-size tree crowns reach several tiles up the screen, so clear enough room for a clean click.
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(gatheringPosition) < 12);
            world.Player = gatheringPosition;
            // Far enough that the player has to keep walking after the button is released.
            var looseOre = world.Add("loose_iron", gatheringPosition + new Vector2(2, 0), 2);
            int oreBefore = world.S.Backpack["iron_ore"];
            await Frames();
            point = view.Screen(looseOre.Pos) - new Vector2(0, 12) * view.Zoom;
            UIRequire(view.Pick(point)?.Id == looseOre.Id, "The loose ore fixture must be selectable.");
            windowPoint = GetViewport().GetFinalTransform() * point;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
            await Frames();
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = false });
            bool harvestAnimation = false;
            for (int i = 0; i < 120 && world.Find(looseOre.Id) != null; i++) { await Frames(); harvestAnimation |= view.PlayerAnimation == ActorAction.Gather; }
            UIRequire(world.Find(looseOre.Id) == null && world.S.Backpack["iron_ore"] == oreBefore + 2, "One right-click must walk over and pick up the whole loose piece.");
            UIRequire(!harvestAnimation, "Picking up loose ore must not play the gathering animation.");
            RemoveCheckFiles();
            GD.Print("UI-TEST PASS"); Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
