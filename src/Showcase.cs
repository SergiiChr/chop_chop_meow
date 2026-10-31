using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The review fixtures behind <c>--showcase</c> and its flags: settled scenes for screenshots, separate from a playable game.</summary>
public sealed class Showcase(World world, WorldView view, SidePanel panel, Control ui, BlackHole blackHole, PauseMenu menu, UiKit kit) {
    /// <summary>Mine review at the near cliff of <c>--mine-ore</c>, iron by default: <c>--mine-stage N</c> sets the finished stages, and a finished mine gets a crew underground and some output.</summary>
    private void MineReview(Thing sign) {
        var stage = int.TryParse(Launch.Value("--mine-stage"), out int n) ? (MineStage)n : MineStage.Open;
        var cliff = World.CliffOf(Kinds.Parse(Launch.Value("--mine-ore")) ?? ThingKind.Iron);
        world.Player = World.MineSpot(cliff) + cliff.Face * 3;
        var mine = world.Build(ThingKind.Mine, World.MineSpot(cliff))!;
        mine.MineStage = stage; mine.Progress = stage is MineStage.Marked or MineStage.Supplied ? world.MineStageSeconds / 2 : 0;
        if (stage == MineStage.Dug) mine.Inventory.Add("wood", 20);
        world.Add(ThingKind.Chest, mine.Pos + cliff.Face * 3 + cliff.Along * 2);
        foreach (var w in world.Workers.ToArray()) world.Assign(w, null);
        foreach (var w in world.Workers.Take(stage == MineStage.Open ? 3 : 1)) { w.Pos = world.Player; world.Assign(w, mine); }
        if (stage == MineStage.Open && !Launch.Has("--idle")) mine.Output!.Add(World.MineOre(mine), 20);
        if (Launch.Has("--idle")) { foreach (var w in world.Workers) world.Assign(w, null); mine.Output!.Add(World.MineOre(mine), 12); mine.Output.Add("stone", 12); }
        world.S.Time = world.Data.DaySeconds * (Launch.Has("--night") ? .55f : .2f);
        for (int i = 0; i < 80; i++) world.Tick(.25f);
        if (Launch.Has("--panel")) panel.Select(mine); else panel.Close();
        // Review from between the cart and portal, so either cliff-facing orientation fits in view.
        world.Player = mine.Pos - cliff.Face * .8f;
        view.Zoom = 1.25f;
    }
    private void Press(string button) => panel.FindChild(button, true, false).EmitSignal(BaseButton.SignalName.Pressed);

    public void Run() {
        if (Launch.Has("--environment-review")) {
            string location = Launch.Value("--environment-review") ?? "forest";
            float coastAcross = Terrain.Across(Terrain.Spawn);
            var coastCenter = Terrain.Point(Terrain.Coast(coastAcross) - 2, coastAcross);
            var reviewCliff = World.CliffOf(Kinds.Parse(Launch.Value("--mine-ore")) ?? ThingKind.Coal);
            var mineCenter = World.MineSpot(reviewCliff) - reviewCliff.Face * 1.4f;
            world.Player = location switch {
                "lake" => Terrain.WaterPoint(Terrain.Lake + Terrain.Seaward) + Terrain.Seaward,
                "coast" => coastCenter - Terrain.Seaward * 3,
                "mine" => World.MineSpot(reviewCliff) + reviewCliff.Face,
                "terrain" => Terrain.Point(125, -18),
                _ => Terrain.Spawn,
            };
            world.S.Time = world.Data.DaySeconds * .2f;
            world.S.Workers.Clear(); world.S.Orders.Clear();
            world.Tick(0);
            view.EnvironmentReviewFog = Launch.Has("--review-fog");
            view.EnvironmentCenter = location switch { "lake" => Terrain.Lake + Terrain.Seaward * 3, "terrain" => Terrain.Point(125, -18), "coast" => coastCenter, "mine" => mineCenter, _ => null };
            if (location == "mine") {
                var mine = world.Build(ThingKind.Mine, World.MineSpot(reviewCliff))!;
                mine.MineStage = MineStage.Open;
                mine.Output!.Add(World.MineOre(mine), 12);
            }
            // The wide terrain camera must sit above the peaks rather than inside a near mountain face.
            if (location is "terrain" or "coast" or "mine") view.EnvironmentCameraDistance = 140;
            view.Zoom = location switch { "lake" => .44f, "terrain" => .24f, "coast" => .36f, "mine" => .85f, _ => .75f };
            panel.Close(); ui.Visible = false; kit.HoldWhile(() => true);
            return;
        }
        if (Launch.Has("--bushes")) {
            // A clean three-bush comparison row, with one picked-clean bush to show its persistent model.
            var at = Terrain.Spawn + new Vector2(9, 9);
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 15);
            // Bush variants are selected from the id; start the row at variant zero and include all three.
            world.S.NextId += (3 - world.S.NextId % 3) % 3;
            world.Add(ThingKind.Berries, at + new Vector2(-2.25f, 2.25f), 24);
            world.Add(ThingKind.Berries, at + new Vector2(-.75f, .75f), 24);
            world.Add(ThingKind.Berries, at + new Vector2(.75f, -.75f), 24);
            world.Add(ThingKind.Berries, at + new Vector2(2.25f, -2.25f), 0);
            world.S.Time = world.Data.DaySeconds * .2f;
            world.S.Workers.Clear(); world.S.Orders.Clear(); world.S.Remembered.Clear();
            world.Player = at + new Vector2(-1, -.5f);
            world.Tick(0); view.Zoom = 1.8f;
            panel.Close(); return;
        }
        if (Launch.Has("--trees")) {
            // A clean three-tree comparison row, placed beyond the settlement showcase.
            var at = Terrain.Spawn + new Vector2(9, 9);
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 15);
            // Tree variants are selected from the id: align the first id to oak, then add spruce and birch.
            world.S.NextId += (3 - world.S.NextId % 3) % 3;
            world.Add(ThingKind.Tree, at + new Vector2(-3, 3), 42);
            world.Add(ThingKind.Tree, at, 42);
            world.Add(ThingKind.Tree, at + new Vector2(3, -3), 42);
            world.S.Time = world.Data.DaySeconds * .2f;
            world.S.Workers.Clear(); world.S.Orders.Clear(); world.S.Remembered.Clear();
            world.Player = at + new Vector2(1, -1);
            if (Launch.Has("--tree-shadow")) world.Player = at - Vector2.One * 2;
            world.Tick(0); view.Zoom = 1;
            panel.Close(); return;
        }
        foreach (var t in world.Things.Where(t => t.Pos.DistanceTo(Terrain.Spawn) < 8).ToArray()) world.S.Things.Remove(t);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(-2, -2)); var fire = world.Add(ThingKind.Campfire, Terrain.Spawn + new Vector2(1, -3)); fire.Burn = 200;
        var f = world.Add(ThingKind.Furnace, Terrain.Spawn + new Vector2(4, -2)); f.Inventory.Add("iron_ore", 12); f.Inventory.Add("wood", 6);
        var bellows = world.Add(ThingKind.Reinforced, Terrain.Spawn + new Vector2(6, 1)); bellows.Inventory.Add("iron_bar", 12); bellows.Inventory.Add("coal", 20); bellows.Burner!.Add("coal", 10); bellows.Heat = 100;
        var chest = world.Add(ThingKind.Chest, Terrain.Spawn + new Vector2(2, 3)); chest.Inventory.Add("wood", 35); chest.Inventory.Add("coal", 30); chest.Inventory.Add("iron_bar", 15);
        world.Add(ThingKind.Anvil, Terrain.Spawn + new Vector2(4, 4)); var sign = world.Add(ThingKind.Signpost, Terrain.Spawn + new Vector2(-1, 4));
        world.S.Backpack.Add(Inventory.Coins, 140); world.S.Hunger = 55; world.S.Thirst = 30; world.S.Backpack.Add("water", 4); world.S.Backpack.Add("steel_pick", 1);
        var bench = world.Add(ThingKind.Bench, Terrain.Spawn + new Vector2(-4, 1.5f)); bench.Recipe = "crate"; bench.Inventory.Add("planks", 12); bench.Tools!.Add("stone_chisel", 1); bench.Tools.Add("steel_hammer", 1);
        var rack = world.Add(ThingKind.Toolstation, Terrain.Spawn + new Vector2(-5, -1.5f)); rack.Inventory.Add("steel_axe", 1); rack.Inventory.Add("rake", 1);
        world.Add(ThingKind.Leaves, Terrain.Spawn + new Vector2(-3.5f, 5), World.LeafPatch);
        var crate = world.Add(ThingKind.Crate, Terrain.Spawn + new Vector2(1.5f, 1.5f)); crate.Inventory.Add("berries", 6); crate.Inventory.Add("steel_axe", 1); crate.Inventory.Add("steel_pick", 1);
        foreach (var (item, count) in new[] { ("steel_axe", 1), ("steel_pick", 1), ("stone_chisel", 1), ("wood", 9), ("planks", 8), ("wood_parts", 3), ("crate", 1), ("torch", 2) }) world.S.Backpack.Add(item, count);
        world.S.Milestones.UnionWith(new[] { "gather", "bench", "crate", "sold", "pick", "furnace", "iron_bar", "reinforced", "steel_bar", "anvil", "steel_tool" });
        // The review fixture is deliberately separate from a fresh playable game.
        world.Player = sign.Pos + Vector2.One; var offer = world.Data.HireOffers[1];
        var w1 = world.Hire(sign, offer); var w2 = world.Hire(sign, offer); var w3 = world.Hire(sign, offer);
        if (w1 != null) world.Assign(w1, bellows); if (w2 != null) world.Assign(w2, bellows); if (w3 != null) world.Assign(w3, sign);
        // Three packed crates leave on the cart, so it should show three crates.
        foreach (var goods in new[] { "steel_axe", "berries", "raw_meat" }) {
            var box = world.Add(ThingKind.Crate, world.Player); box.Inventory.Add(goods, goods == "steel_axe" ? 2 : 12); world.PickUpCrate(box);
            world.S.Backpack.TransferTo(sign.Inventory, world.Backpack.Items.Keys.First(id => id.StartsWith(World.PackedPrefix)), 1);
        }
        world.Sell(sign); world.Orders[0].Elapsed = 4;
        world.S.Crafting.AddRange(new[] { "planks", "wood_parts" }); world.S.CraftTime = 6;
        var meat = world.Add(ThingKind.Crate, world.Player); meat.Inventory.Add("burnt_steak", 12); world.PickUpCrate(meat);
        world.S.Backpack.TransferTo(sign.Inventory, world.Backpack.Items.Keys.First(id => id.StartsWith(World.PackedPrefix)), 1); sign.Output!.Add(Inventory.Coins, 36);
        if (Launch.Has("--night")) world.S.Time = world.Data.DaySeconds * .78f;
        // --day-phase sets the time of day from 0 at dawn, for comparing the light across the day.
        if (Launch.Value("--day-phase") is string phase) world.S.Time = world.Data.DaySeconds * float.Parse(phase, System.Globalization.CultureInfo.InvariantCulture);
        // A past walk toward the iron cliff leaves explored ground beyond the live sight circle.
        for (float d = 0; d <= 14; d += 2) { world.Player = Terrain.Spawn + new Vector2(d * .5f, -d); world.Tick(0); }
        world.Player = Terrain.Spawn; world.Tick(0); panel.Select(bellows);
        // --hide-ui leaves only the world in review screenshots.
        if (Launch.Has("--hide-ui")) ui.Visible = false;
        if (Launch.Has("--market")) { world.Player = sign.Pos + Vector2.One; panel.Select(sign); }
        if (Launch.Has("--bench")) { world.Player = bench.Pos + Vector2.One; panel.Select(bench); }
        if (Launch.Has("--craft")) panel.Open(Page.Craft);
        if (Launch.Has("--crew")) panel.Open(Page.Crew);
        if (Launch.Has("--worker")) panel.Select(world.Workers[0]);
        if (Launch.Has("--guide")) panel.Open(Page.Guide);
        if (Launch.Has("--menu")) { panel.Close(); menu.Open(); }
        if (Launch.Has("--furnace")) { world.Player = bellows.Pos + Vector2.One; panel.Select(bellows); }
        if (Launch.Has("--piles")) {
            var at = Terrain.Spawn + new Vector2(-3, 9);
            foreach (var (item, stacks, dx) in new[] { ("stone", 14, 0), ("wood", 6, 3), ("leaves", 3, 6) }) {
                var pile = world.Add(ThingKind.Pile, at + new Vector2(dx, 0)); pile.Inventory.Add(item, stacks * pile.Inventory.StackSize);
                if (item == "leaves") { pile.Compost = true; pile.Inventory.Add("fertilizer", 20); }
            }
            world.Player = at + new Vector2(3, -2.5f); world.Tick(0); view.Zoom = 1.4f;
            if (Launch.Has("--panel")) panel.Select(world.Things.Last(t => t.Kind == ThingKind.Pile)); else panel.Close();
        }
        if (Launch.Has("--farm")) {
            // A row of plots, one per look: bare, sown soil, straws, green plants and ripe wheat half harvested, beside a pile of wheat.
            var at = Terrain.Spawn + new Vector2(-6, 9);
            for (int i = 0; i < 5; i++) {
                var plot = world.Add(ThingKind.Farm, at + new Vector2(i * World.ZoneSize, 0));
                plot.FarmStage = i switch { 0 => FarmStage.Fallow, 4 => FarmStage.Ripe, _ => FarmStage.Growing };
                plot.Progress = i == 4 ? world.HarvestSeconds / 2 : (i - 1) * world.GrowSeconds / 3 + 1;
            }
            var wheat = world.Add(ThingKind.Pile, at + new Vector2(0, World.ZoneSize)); wheat.Inventory.Add("wheat", 300);
            world.Player = at + new Vector2(6, 2.5f); world.Tick(0); view.Zoom = 1.2f;
            if (Launch.Has("--panel")) panel.Select(world.Things.Last(t => t.Kind == ThingKind.Farm)); else panel.Close();
        }
        if (Launch.Has("--kitchen")) {
            // The cooking table mid-stew beside a canning station sealing cans, with the mortar slotted.
            var at = Terrain.Spawn + new Vector2(-3, 8);
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 4 && !t.IsResource);
            var table = world.Add(ThingKind.Cooktable, at); table.Tools!.Add("mortar", 1); world.SetRecipe(table, "meat_stew");
            table.Inventory.Add("water", 2); table.Inventory.Add("raw_meat", 2); table.Burner!.Add("wood", 2);
            var cannery = world.Add(ThingKind.Cannery, at + new Vector2(2.2f, 0)); world.SetRecipe(cannery, "canned_meat_stew");
            cannery.Inventory.Add("meat_stew", 2); cannery.Inventory.Add("tin_can", 2); cannery.Burner!.Add("wood", 2);
            world.Player = at + new Vector2(1, 2.5f); world.Tick(.5f); view.Zoom = 1.7f;
            if (Launch.Has("--panel")) panel.Select(table); else panel.Close();
        }
        if (Launch.Has("--packing")) {
            // A packing station part way through a crate of bread, with bread and empty crates waiting, beside the trading post asking for crates of bread and berries.
            var at = Terrain.Spawn + new Vector2(3, 8);
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 4 && !t.IsResource);
            var packer = world.Add(ThingKind.Packer, at); world.SetPacking(packer, "bread");
            packer.Inventory.Add("bread", 20); packer.Inventory.Add("crate", 3);
            world.Player = at + new Vector2(1, 1.5f); view.Zoom = 1.7f;
            for (int i = 0; i < 7; i++) world.Tick(world.Data.PackSeconds);
            // --crates N leaves that many empty crates at the station, and --unpacked takes its bread so packing stops.
            if (Launch.Value("--crates") is string crates) { packer.Inventory.Remove("crate", 99); packer.Inventory.Add("crate", int.Parse(crates, System.Globalization.CultureInfo.InvariantCulture)); }
            if (Launch.Has("--unpacked")) { packer.Inventory.Remove("bread", 99); world.Tick(.1f); }
            if (Launch.Has("--crates") || Launch.Has("--unpacked")) { world.Player = at + new Vector2(-1.4f, -1.4f); view.Zoom = 2.6f; }
            // --packer puts a worker at the table packing, with the player stepped back.
            if (Launch.Has("--packer")) { world.S.Backpack.Add(Inventory.Coins, 500); world.Player = sign.Pos + Vector2.One; }
            if (Launch.Has("--packer") && world.Hire(sign, world.Data.HireOffers[1]) is Worker packing) {
                world.SetDuty(packing, packer, "craft", true); packing.Pos = at + World.PackStand;
                world.Player = at + new Vector2(.4f, -2.6f); view.Zoom = 3.4f; world.Tick(.1f);
            }
            world.SetFilter(sign, World.CrateOf("bread"), false, true); world.SetFilter(sign, World.CrateOf("berries"), false, true); world.SetAutoSell(sign, true);
            if (Launch.Has("--post")) { world.Player = sign.Pos + Vector2.One; panel.Select(sign); Press("filters"); } else if (Launch.Has("--panel")) { panel.Select(packer); if (Launch.Has("--pick")) Press("pack_good"); } else panel.Close();
        }
        if (Launch.Has("--tools")) { world.Player = rack.Pos + new Vector2(2.5f, 2.5f); view.Zoom = 1.6f; if (Launch.Has("--panel")) panel.Select(rack); else panel.Close(); }
        if (Launch.Has("--tin")) { var cliff = World.CliffOf(ThingKind.Tin); world.Player = cliff.Center + cliff.Face * 5.5f; world.Tick(0); panel.Close(); }
        if (Launch.Has("--pantry")) {
            foreach (var id in new[] { "tin_ore", "tin_bar", "tin_can", "steel_chisel", "rake", "mortar", "leaves", "grass", "fertilizer", "wheat_seeds", "wheat", "flour", "dough", "bread", "crushed_berries", "berry_jam", "meat_stew", "burnt_steak", "canned_jam", "canned_meat_stew" }) world.S.Backpack.Add(id, 3);
            panel.Open(Page.Backpack);
        }
        // Hovering a recipe shows its description below the grids and as a tooltip at the pointer.
        if (Launch.Has("--hover")) panel.GetTree().CreateTimer(.5).Timeout += () => {
            if (panel.FindChild("craft_torch", true, false) is not Button cell) return;
            cell.EmitSignal(Control.SignalName.MouseEntered);
            var at = panel.GetViewport().GetFinalTransform() * cell.GetGlobalRect().GetCenter();
            Input.WarpMouse(at); Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        };
        if (Launch.Has("--torch")) { world.Player = Terrain.Spawn + new Vector2(-5, 6); world.S.Backpack.Add("torch", 1); world.UseTorch(Vector2.Zero); if (!Launch.Has("--kindle")) world.Tick(World.KindleSeconds); panel.Close(); view.Zoom = 1.6f; }
        if (Launch.Has("--crate")) { world.Player = crate.Pos + Vector2.One; panel.Select(crate); }
        if (Launch.Has("--anomaly")) { world.Player = crate.Pos + Vector2.One; blackHole.CrateIntoCrate(crate, crate.Inventory, "crate"); }
        if (Launch.Has("--collapse")) { world.Player = crate.Pos + Vector2.One; panel.Close(); blackHole.CrateIntoCrate(crate, crate.Inventory, "crate"); blackHole.Collapse(crate); }
        if (Launch.Has("--lunch")) {
            // Lunch review: the crew has walked over and sat down around the campfire, with the pay overview open.
            world.S.Time = (world.Data.LunchHour - 6) / 24 * world.Data.DaySeconds;
            for (int i = 0; i < 60; i++) world.Tick(.25f);
            panel.Open(Page.Crew);
        }
        if (Launch.Has("--strike")) {
            // Strike review: the whole crew owes wages and stands holding protest signs.
            foreach (var w in world.Workers) w.WageDebt = w.Wage;
            world.Player = sign.Pos + new Vector2(2.5f, 1.5f); world.Tick(.25f); panel.Close(); view.Zoom = 2.2f;
        }
        if (Launch.Has("--mine")) MineReview(sign);
        if (!Launch.Has("--wolves")) return;
        // Night ambush review: a wounded player, one wolf in leaping range, the pack further out and a fresh carcass.
        var spot = Terrain.Spawn + new Vector2(11, 8);
        world.S.Things.RemoveAll(t => t.Pos.DistanceTo(spot) < 6);
        world.S.Time = world.Data.DaySeconds * .78f; world.Player = spot; world.S.Health = 45;
        foreach (var offset in new[] { new Vector2(2.5f, -1), new Vector2(-6, 3), new Vector2(5, 5) }) world.S.Wolves.Add(new Wolf { Pos = spot + offset });
        world.Add(ThingKind.WolfCorpse, spot + new Vector2(-2, -2), 4);
        panel.Close(); view.Zoom = 1.6f;
    }
}
