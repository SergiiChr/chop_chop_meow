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
    private async Task Click(Vector2 point)
    {
        var at = GetViewport().GetFinalTransform() * point;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = at, GlobalPosition = at, Pressed = false });
        await Frames();
    }
    private async Task CheckSignpost()
    {
        ClosePanel(); await Frames();
        var sign = world.Add("signpost", world.Player + new Vector2(2, 0));
        world.S.Backpack.Add("wood", 5); world.S.Backpack.Add(Inventory.Coins, 100);
        var berries = world.Add("crate", world.Player + new Vector2(0, 1)); berries.Inventory.Add("berries", 12); world.PickUpCrate(berries);
        string packed = world.S.Backpack.Items.Keys.Single(id => id.StartsWith(World.PackedPrefix));
        await Click(view.Screen(sign.Pos, .8f));
        UIRequire(selected == sign.Id && Named("sell") != null && Named("coins") != null && Named("hardhat") != null && Named("prices") != null, "A signpost must open the market with its icon buttons.");
        await Click(Cell(2, c => c.TooltipText.StartsWith("Wood ·")));
        UIRequire(sign.Inventory.Count == 0 && world.S.Backpack["wood"] == 5, "Loose goods must stay in the backpack: the market only takes crates.");
        await Click(Cell(2, c => c.TooltipText.StartsWith("Wood crate of 12 Berries")));
        UIRequire(sign.Inventory[packed] == 1 && world.S.Backpack[packed] == 0, "Clicking a packed crate must load it into the market input.");
        await Click(Named("sell")!);
        UIRequire(world.S.Orders.Count == 1 && sign.Inventory.Count == 0, "Sell must send the market input off on a cart.");
        await Click(Named("prices")!);
        var fetch = Named("fetch");
        UIRequire(sheet.Visible && panel.OffsetLeft == PanelLeft - SheetWidth && fetch != null && fetch.Disabled && fetch.TooltipText == "No worker assigned.", "The price sheet must open beside the panel and explain why no one can fetch prices.");
        UIRequire(Named("buy_wood") == null && Named("buy_berries") == null && Named("buy_water") != null, "The price sheet must only offer what the village sells.");
        await Click(Named("buy_water")!);
        UIRequire(world.S.Orders.Count == 2 && world.S.Orders[1].Buying, "Buy on the price sheet must order goods.");
        await Click(Named("hardhat")!);
        var dear = world.Data.HireOffers.OrderBy(o => o.Upfront).Last();
        await Click(Named($"hire_{dear.Upfront:0}")!);
        var hired = world.S.Workers.LastOrDefault();
        UIRequire(hired != null && hired.Wage == dear.Wage, "A hiring offer must set the new worker's wage.");
        world.Assign(hired!, sign); Select(sign); await Frames();
        UIRequire(!Named("fetch")!.Disabled, "With a worker assigned, fresh prices can be fetched.");
        await Click(Named("fetch")!);
        UIRequire(hired!.Away > 0, "Fetching must send the assigned worker to the village.");
        await Click(Named("prices")!);
        UIRequire(!sheet.Visible && panel.OffsetLeft == PanelLeft, "The price sheet icon must close the sheet again.");
        world.S.Workers.Remove(hired); world.S.Orders.Clear(); world.S.Things.Remove(sign); world.S.Backpack.Remove(Inventory.Coins, 999);
        signpostTab = "market"; ClosePanel(); await Frames();
    }
    /// <summary>Marks a mine on every ore through the build plan and a ground click, then checks its stages and the end goals in the pause menu.</summary>
    private async Task CheckMine()
    {
        var home = world.Player;
        foreach (string ore in World.MineOres)
        {
            var cliff = World.CliffOf(ore);
            var spot = World.MineSpot(cliff);
            world.Player = spot + cliff.Face * 3; await Frames(4);
            await KeyStroke(Key.B);
            Named("build_mine")!.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            UIRequire(view.BuildMode == "mine", "The mine plan must enter placement mode.");
            // Off the exact spot, so the click shows the plan snapping to the cliff.
            await Click(view.Screen(spot + cliff.Face * .8f + cliff.Along * .8f));
            var mine = world.S.Things.LastOrDefault(t => t.Kind == "mine");
            UIRequire(mine != null && mine.Pos == spot && selected == mine.Id && view.BuildMode == "", $"A click near the {ore} cliff must mark a mine on its spot and select it.");
            UIRequire(HasLabel(panel, "Stage 1 · Dig the entrance") && HasLabel(panel, "Stage 2 · Shore it up") && HasLabel(panel, "Stage 3 · Fit the frame and rails"), "The mine panel must list its three stages.");
        }
        world.Player = home; ClosePanel();
        await KeyStroke(Key.Escape);
        UIRequire(HasLabel(menu, "END GOALS") && HasLabel(menu, "A working iron mine") && HasLabel(menu, $"{world.Data.Goals.DailyProfit} coins profit a day"), "The pause menu must list the end goals.");
        await KeyStroke(Key.Escape);
    }
    /// <summary>Clicks open ground beside the player, so the click uses the item in hand instead of selecting something.</summary>
    private async Task ClickGround()
    {
        await Frames();
        var open = Enumerable.Range(1, 6).SelectMany(r => Enumerable.Range(0, 8).Select(a => world.Player + Vector2.Right.Rotated(a * Mathf.Tau / 8) * r))
            .Select(p => view.Screen(p)).First(p => view.Pick(p) == null && view.PickWorker(p) == null && GetViewport().GetVisibleRect().HasPoint(p) && p.Y < GetViewport().GetVisibleRect().Size.Y - 120);
        await Click(open);
    }

    private async Task CheckCraftingAndTorch()
    {
        ClosePanel(); world.S.Backpack.Remove("stone_chisel", 99); world.S.Backpack.Add("stone", 2); world.S.Backpack.Add("wood", 3);
        await KeyStroke(Key.C);
        UIRequire(page == "craft", "C must open the crafting menu.");
        var badge = (TextureRect)panel.FindChild("section_chisel", true, false).FindChild("badge", true, false);
        UIRequire(badge.Modulate != Colors.White && badge.TooltipText.Contains("Needs a chisel"), "A missing chisel must show a red badge with a hint.");
        await Click(Named("craft_stone_chisel")!);
        UIRequire(world.S.Crafting.SequenceEqual(new[] { "stone_chisel" }) && world.S.Backpack["stone"] == 0, "Clicking a recipe must queue it and take its materials.");
        world.S.CraftTime = world.Data.Recipes["stone_chisel"].Seconds - .05f; await Frames(10); RefreshLive(); await Frames();
        UIRequire(world.S.Backpack["stone_chisel"] == 1 && badge.Modulate == Colors.White, "The chisel must arrive and turn its badge back to normal.");
        int wood = world.S.Backpack["wood"];
        await Click(Named("craft_torch")!);
        world.S.CraftTime = world.Data.Recipes["torch"].Seconds - .05f; await Frames(10);
        UIRequire(world.S.Backpack["torch"] == 1 && world.S.Backpack["wood"] == wood - 3, "A torch must be made by hand from 3 wood.");
        ClosePanel(); world.S.HotbarSlots[8] = "torch";
        await KeyStroke(Key.Key9);
        UIRequire(world.S.Torch == 0, "Selecting a torch must not light it.");
        await ClickGround();
        UIRequire(world.S.TorchKindling > 0 && world.S.Backpack["torch"] == 0 && view.PlayerAnimation == ActorAction.Kindle, "The first click must strike flint and steel with the torch in hand.");
        await Seconds(World.KindleSeconds + .3);
        UIRequire(world.S.TorchLit, "The torch must catch after the lighting animation.");
        await ClickGround();
        UIRequire(world.TorchFlare > 0 && view.PlayerAnimation == ActorAction.TorchSwing, "Clicking with a lit torch must swing it.");
    }

    private async Task CheckCrateInCrate()
    {
        var crate = world.Add("crate", world.Player + new Vector2(1, 0));
        var box = world.Add("crate", world.Player); box.Inventory.Add("berries", 3); world.PickUpCrate(box);
        string packed = world.S.Backpack.Items.Keys.Single(id => id.StartsWith(World.PackedPrefix));
        Select(crate); await Frames();
        await DragTo(Cell(1, c => c.TooltipText.StartsWith("Wood crate")), Cell(0, c => c.TooltipText == ""));
        UIRequire(anomaly.Visible && anomalyWarning.Visible && world.S.Backpack[packed] == 1 && crate.Inventory.Count == 0, "A crate dragged into a crate must stop at the warning.");
        await Click((Control)anomaly.FindChild("anomaly_no", true, false));
        UIRequire(!anomaly.Visible && world.S.Backpack[packed] == 1 && world.Find(crate.Id) != null, "No must cancel and keep both crates.");
        RefreshLive();
        await DragTo(Cell(1, c => c.TooltipText.StartsWith("Wood crate")), Cell(0, c => c.TooltipText == ""));
        await Click((Control)anomaly.FindChild("anomaly_yes", true, false));
        UIRequire(collapse.Visible && !anomalyWarning.Visible, "Yes must open the black hole.");
        for (int i = 0; i < 40 && !(singularity.Visible && singularity.Modulate.A > .99f); i++) await Seconds(.25);
        UIRequire(singularity.Visible, "The collapse must end on the game over screen.");
        await Click((Control)singularity.FindChild("menu_after_collapse", true, false));
        UIRequire(!anomaly.Visible && backdrop.Visible && FindButton(screen, "New game") != null, "Main menu must leave the black hole for the start menu.");
    }

    private async Task CheckCabinAndCanopy()
    {
        ClosePanel(); await Frames();
        var hut = world.Add("hut", world.Player + new Vector2(0, 4));
        await Click(view.Screen(hut.Pos, 1.5f)); await Seconds(1.5);
        UIRequire(world.S.Sheltered == hut.Id && page == "inspect" && selected == hut.Id, "Clicking a cabin must walk the player inside and open it.");
        float dayTime = world.S.Time;
        world.S.Time = world.Data.DaySeconds * (world.Data.NightStarts + .02f); world.S.Hunger = 5; RefreshLive();
        var sleep = Named("sleep");
        UIRequire(sleep != null && sleep.TooltipText.Contains("too hungry"), "The sleep button must explain that the player is too hungry to sleep.");
        world.S.Time = dayTime; world.S.Hunger = 100;
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.S, Keycode = Key.S, Pressed = true });
        await Seconds(.2);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.S, Keycode = Key.S, Pressed = false });
        await Frames();
        UIRequire(world.S.Sheltered == 0, "Moving must step the player back out of the cabin.");
        world.S.Things.Remove(hut); ClosePanel(); await Frames();

        // A loose piece far enough behind a tree sits under its crown on screen; it must still be the one picked.
        var tree = world.S.Things.First(t => t.Kind == "tree" && t.Pos == World.Spawn + new Vector2(5, 0));
        var hidden = world.Add("loose_iron", tree.Pos - new Vector2(3.3f, 3.3f), 2);
        var point = view.Screen(hidden.Pos, .15f);
        UIRequire(view.Pick(point)?.Id == hidden.Id, "A tree crown must not steal the click from a loose piece behind it.");
        world.S.Things.Remove(hidden);
        UIRequire(view.Pick(point)?.Id == tree.Id, "With nothing behind it, the crown must still select its tree.");
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
            int heard = sounds.Played; sounds.Play("anvil_work", world.Player + Vector2.One * 40);
            UIRequire(view.OffScreen(world.Player) == 0 && view.OffScreen(world.Player + Vector2.One * 40) > Sounds.Reach && sounds.Played == heard, "A sound well beyond the screen edge must stay silent.");
            var tree = world.S.Things.First(t => t.Kind == "tree" && t.Pos == World.Spawn + new Vector2(5, 0));
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
            var farm = world.Add("farm", world.Player + new Vector2(-3, 3)); farm.Stage = World.FarmRipe;
            await Frames(2);
            var farmPoint = GetViewport().GetFinalTransform() * view.Screen(farm.Pos);
            UIRequire(view.Pick(view.Screen(farm.Pos))?.Id == farm.Id, "A farm plot must be picked where it is drawn.");
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = farmPoint, GlobalPosition = farmPoint, Pressed = true });
            await ToSignal(GetTree().CreateTimer(3), SceneTreeTimer.SignalName.Timeout);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = 0, Position = farmPoint, Pressed = false });
            await Frames(); UIRequire(farm.Progress > 0 && farm.Stage == World.FarmRipe, "Holding RMB on a ripe farm plot must walk over and harvest by hand.");
            world.S.Things.Remove(farm);
            UIRequire(!stats.Text.Contains("Health") && !stats.Text.Contains("Food") && !stats.Text.Contains("Water") && !stats.Text.Contains("coins") && !stats.Text.Contains("Pack"), "The persistent HUD must show only the day and clock beside the brand.");
            UIRequire(hotbarButtons.Count == 9 && world.S.HotbarSlots.Count == 9, "The bottom toolbar must show nine saved hotbar references.");
            int berries = world.S.Backpack["berries"];
            await KeyStroke(Key.Key3);
            UIRequire(view.BuildMode == "chest", "A building-plan hotkey must enter placement mode.");
            await KeyStroke(Key.Key8);
            UIRequire(world.S.ActiveHotbar == 7 && world.S.Backpack["berries"] == berries && hotbarButtons[7].HasThemeStyleboxOverride("normal") && !hotbarButtons[2].HasThemeStyleboxOverride("normal"),
                "Key 8 must put the berries in hand with an outline, without eating them.");
            UIRequire(view.BuildMode == "", "Selecting an item must clear any previously selected building plan.");
            int played = sounds.Played;
            await ClickGround();
            UIRequire(world.S.Backpack["berries"] == berries - 1, "A left click on open ground must eat the berries in hand.");
            UIRequire(sounds.Played == played + 1 && sounds.Last == "eat_berry", "Eating must play only the eating sound, not an inventory sound as well.");
            world.S.Backpack.Add("water", 1); world.S.Thirst = 20; RefreshLive();
            await Click(hotbarButtons[8]);
            UIRequire(world.S.ActiveHotbar == 8 && world.S.Backpack["water"] == 1, "Clicking a hotbar slot must only select it.");
            await ClickGround();
            UIRequire(world.S.Backpack["water"] == 0 && world.S.Thirst > 20, "A left click on open ground must drink the water in hand.");
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
            UIRequire(HasLabel(panel, "Health") && HasLabel(panel, "Capacity") && HasLabel(panel, "Daily wages"), "Inventory must contain health, capacity and wages.");
            world.S.Backpack.Add(Inventory.Coins, 1200); RefreshLive();
            UIRequire(panel.FindChildren("slot_*", "Button", true, false).OfType<Button>().Count(c => c.TooltipText.StartsWith("Coins")) == 2, "Carried coins must show as backpack cells stacking to 1000.");
            world.S.Backpack.Remove(Inventory.Coins, 1200);
            float healthBefore = world.S.Health; world.S.Health -= 12; RefreshLive(); DamageFlash(healthBefore - world.S.Health);
            UIRequire(health.TooltipText == $"Health {world.S.Health:0}%" && damageEdges.Strength > 0, "The HUD heart must track health and a hit must flash the screen edges.");
            world.S.Health = healthBefore;
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
            UIRequire(world.Player == pinnedAt && view.PlayerAnimation == ActorAction.Mauled && deathLabel.Text == "", "A pinned player must not move, must show the mauled struggle and leaves the message to the pinned notice.");
            world.S.Mauled = false; world.S.Wolves.Clear(); world.S.Time = dayTime; world.S.Health = 100;
            world.S.Health = -1; await Seconds(.3);
            UIRequire(world.Dead && deathLabel.Text.Contains("Respawning in"), "Death must show the respawn countdown.");
            world.S.RespawnIn = .01f; await Seconds(.3);
            UIRequire(!world.Dead && deathLabel.Text == "", "The countdown must clear once the player respawns.");

            await CheckCabinAndCanopy();
            await CheckSignpost();

            world.S.Milestones.UnionWith(new[] { "gather", "furnace", "iron_bar", "anvil", "bench", "pick", "reinforced", "steel_bar", "steel_tool", "sold_steel", "hired", "assigned" });
            await Frames(3);
            UIRequire(!ending.Visible && !paused, "The starter goal chain alone must not end the game.");
            await CheckMine();
            foreach (var mine in world.S.Things.Where(t => t.Kind == "mine")) mine.Stage = World.MineOpen;
            world.S.Milestones.Add("profit_week");
            await Frames(3);
            UIRequire(ending.Visible && paused, "Reaching the end goals must show the ending and pause the world.");
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
            world.S.Backpack.Remove("iron_axe", 99); world.S.Backpack.Remove("steel_axe", 99);
            await KeyStroke(Key.Space);
            UIRequire(view.PlayerAnimation == ActorAction.Attack, "Space without an axe must trigger the dedicated unarmed attack.");
            await ToSignal(GetTree().CreateTimer(1.1), SceneTreeTimer.SignalName.Timeout);
            var gatheringPosition = World.Spawn - new Vector2(30, 30);
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
            await CheckCraftingAndTorch();
            await CheckCrateInCrate();
            RemoveCheckFiles();
            GD.Print("UI-TEST PASS"); Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
