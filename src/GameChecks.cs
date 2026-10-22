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
    private bool HasLabel(Node parent, string text)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is Label label && label.Text.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
            if (HasLabel(child, text)) return true;
        }
        return false;
    }
    private static void UIRequire(bool okay, string message)
    {
        if (!okay) throw new InvalidOperationException("UI check failed: " + message);
    }
    private async void RunUIChecks()
    {
        try
        {
            await Frames(4); ClosePanel();
            var start = world.Player;
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
            await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
            UIRequire(view.PlayerAnimation == ActorAction.Walk, "Movement must select the full walking cycle.");
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
            await Frames(); UIRequire(world.Player.DistanceTo(start) > .25f, "WASD must move the player through Godot input.");
            var tree = world.S.Things.First(t => t.Kind == "tree" && t.Pos == new Vector2(28, 24));
            int wood = world.S.Backpack["wood"];
            var point = view.Screen(tree.Pos) - new Vector2(0, 70);
            UIRequire(view.Pick(point)?.Id == tree.Id, "Illustrated tree hit area must select the visible resource.");
            var windowPoint = GetViewport().GetFinalTransform() * point;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
            await Frames(3);
            await ToSignal(GetTree().CreateTimer(4), SceneTreeTimer.SignalName.Timeout);
            UIRequire(view.PlayerAnimation == ActorAction.Chop, "RMB gathering with the starting axe must select the axe stroke.");
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = 0, Position = windowPoint, Pressed = false });
            await Frames(); UIRequire(world.S.Backpack["wood"] > wood, "Holding RMB must approach and gather a real resource.");
            UIRequire(!stats.Text.Contains("Health") && !stats.Text.Contains("Food") && !stats.Text.Contains("Water") && !stats.Text.Contains("coins") && !stats.Text.Contains("Pack"), "The persistent HUD must show only the day and clock beside the brand.");
            UIRequire(hotbarButtons.Count == 9 && world.S.HotbarSlots.Count == 9, "The bottom toolbar must show nine saved hotbar references.");
            int berries = world.S.Backpack["berries"];
            await KeyStroke(Key.Key3);
            UIRequire(view.BuildMode == "chest", "A building-plan hotkey must enter placement mode.");
            await KeyStroke(Key.Key8);
            UIRequire(world.S.Backpack["berries"] == berries - 1, "Key 8 must consume the berries assigned to hotbar slot 8.");
            UIRequire(view.BuildMode == "", "Using a consumable must clear any previously selected building plan.");
            world.S.Backpack.Add("water", 1); world.S.Thirst = 20;
            await KeyStroke(Key.Key9);
            UIRequire(world.S.Backpack["water"] == 0 && world.S.Thirst > 20, "Key 9 must consume carried water and restore thirst.");
            await KeyStroke(Key.B); UIRequire(page == "build" && panel.Visible, "B must open the building plans.");
            var plan = FindButton(panel, "Storage chest"); UIRequire(plan != null, "Building plans must expose storage construction.");
            plan!.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
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
            var filters = FindButton(panel, "Input / output filters"); filters!.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            UIRequire(page == "filters", "Chest filters must be reachable from the live context UI.");
            await KeyStroke(Key.I);
            FindButton(panel, "Assign hotbar slots")!.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            UIRequire(page == "hotbar", "Inventory must expose hotbar assignment.");
            FindButton(panel, "1")!.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            int carriedBeforeAssignment = world.S.Backpack.Count;
            FindButton(panel, "Plan: Thatched hut")!.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            UIRequire(world.S.HotbarSlots[0] == "hut" && world.S.Backpack.Count == carriedBeforeAssignment, "Rebinding a hotbar slot must store a plan reference without creating items.");
            await KeyStroke(Key.Tab); UIRequire(page == "help", "Tab must open the field guide.");
            var savedPosition = world.Player;
            var savedSlots = world.S.HotbarSlots.ToArray(); int savedActiveSlot = world.S.ActiveHotbar;
            await KeyStroke(Key.F5);
            UIRequire(Godot.FileAccess.FileExists(SavePath), "F5 must write a settlement save.");
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
            await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
            await Frames(); UIRequire(world.Player.DistanceTo(savedPosition) > .1f, "The player should move after saving.");
            await KeyStroke(Key.F9);
            UIRequire(world.Player.DistanceTo(savedPosition) < .01f && world.S.Things.Any(t => t.Id == chest.Id && t.Inventory["wood"] > 0), "F9 must restore position and stored items from disk.");
            UIRequire(world.S.HotbarSlots.SequenceEqual(savedSlots) && world.S.ActiveHotbar == savedActiveSlot, "F5 and F9 must preserve hotbar references and selection.");
            UIRequire(HasLabel(panel, "Coins") && HasLabel(panel, "Health") && HasLabel(panel, "Hunger") && HasLabel(panel, "Thirst") && HasLabel(panel, "Capacity") && HasLabel(panel, "Daily wages"), "Inventory must contain all player stats, capacity and wages.");
            ClosePanel();
            var furnace = world.Add("reinforced", world.Player + new Vector2(1, 0));
            Select(furnace);
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
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(gatheringPosition) < 4);
            world.Player = gatheringPosition;
            var looseOre = world.Add("loose_iron", gatheringPosition + new Vector2(1, 0), 4);
            await Frames();
            point = view.Screen(looseOre.Pos) - new Vector2(0, 12) * view.Zoom;
            UIRequire(view.Pick(point)?.Id == looseOre.Id, "The loose ore fixture must be selectable.");
            windowPoint = GetViewport().GetFinalTransform() * point;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
            await Frames(3);
            UIRequire(view.PlayerAnimation == ActorAction.Gather, "Hand-picking loose ore must trigger gathering rather than pumping.");
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = windowPoint, Pressed = false });
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath));
            GD.Print("UI-TEST PASS"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
