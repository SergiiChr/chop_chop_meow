using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The review fixtures behind <c>--showcase</c> and its flags: settled scenes for screenshots, separate from a playable game.</summary>
public sealed class Showcase(World world, WorldView view, SidePanel panel, Control ui, BlackHole blackHole, PauseMenu menu, UiKit kit) {
    /// <summary>
    /// Diary review: the player on the lake's shore by its floating page, with a page lying on the ground beside them and three finds in their diary, two of whose days are unread.
    /// <c>--diary-fall</c> drops that page off the oak beside them a second in instead, since a player has one fallen page at a time.
    /// <c>--panel</c> opens the diary, <c>--diary-day N</c> opens day N large and <c>--diary-find</c> shows the pop-up of a page found in leaves.
    /// <c>--diary-first</c> leaves the diary empty and no page on the ground, so the lake's page is the player's first find.
    /// </summary>
    private void DiaryReview() {
        var me = world.Me;
        world.S.Time = world.Data.DaySeconds * Launch.Number("--day-phase", .2f);
        var page = world.ThingsOf(ThingKind.FloatingPage)[0];
        var floating = Diary.FloatAt(page, world.Time);
        var shore = Terrain.WaterPoint(floating);
        var inland = (shore - Terrain.Lake).Normalized();
        world.S.Things.RemoveAll(t => !t.IsPage && t.Pos.DistanceTo(shore) < 8 && t.Kind != ThingKind.Water);
        world.S.Workers.Clear(); world.S.Orders.Clear(); world.S.Remembered.Clear(); world.S.Wolves.Clear();
        world.Player = shore + inland * .6f;
        var tree = world.Add(ThingKind.Tree, shore + inland * 4.5f + inland.Orthogonal() * 2, World.TreeWood);
        if (!Launch.Has("--diary-first")) {
            me.DiaryFinds.AddRange([DiaryFind.Sand, DiaryFind.Berries, DiaryFind.Tree]);
            me.DiaryRead.AddRange([6, 36]);
            if (Launch.Has("--diary-fall")) view.GetTree().CreateTimer(1).Timeout += () => world.Diary.Drop(me, tree.Pos - inland * 1.2f);
            else world.Diary.Drop(me, world.Player + inland.Orthogonal() * 1.4f + inland * .8f);
        }
        if (Launch.Has("--diary-find")) me.DiaryFinding = DiaryFind.Leaves;
        world.Tick(0); view.Zoom = Launch.Number("--zoom", 1.8f);
        if (Launch.Has("--panel")) panel.Open(Page.Diary); else panel.Close();
        if (Launch.Value("--diary-day") is string day) panel.ReadingSheet.Open(int.Parse(day), quiet: true);
        if (Launch.Has("--hide-ui")) ui.Visible = false;
    }

    /// <summary>Mine review at the near cliff of <c>--mine-ore</c>, iron by default: <c>--mine-stage N</c> sets the finished stages, and a finished mine gets a crew underground and some output.</summary>
    private void MineReview(Thing sign) {
        var stage = int.TryParse(Launch.Value("--mine-stage"), out int n) ? (MineStage)n : MineStage.Open;
        var cliff = Cliff.Of(Kinds.Parse(Launch.Value("--mine-ore")) ?? ThingKind.Iron);
        world.Player = cliff.MineSpot + cliff.Face * 3;
        var mine = world.Construction.Build(ThingKind.Mine, cliff.MineSpot)!;
        mine.MineStage = stage; mine.Progress = stage is MineStage.Marked or MineStage.Supplied ? world.Mines.StageSeconds / 2 : 0;
        if (stage == MineStage.Dug) mine.Inventory.Add("wood", 20);
        // The scene's storage takes what the crew brings, as a player would set it.
        world.SetFilter(world.Add(ThingKind.Chest, mine.Pos + cliff.Face * 3 + cliff.Along * 2), Inventory.Everything, false, true);
        foreach (var w in world.Workers.ToArray()) world.Assign(w, null);
        foreach (var w in world.Workers.Take(stage == MineStage.Open ? 3 : 1)) { w.Pos = world.Player; world.Assign(w, mine); }
        if (stage == MineStage.Open && !Launch.Has("--idle")) mine.Output!.Add(Mines.Ore(mine), 20);
        if (Launch.Has("--idle")) { foreach (var w in world.Workers) world.Assign(w, null); mine.Output!.Add(Mines.Ore(mine), 12); mine.Output.Add("stone", 12); }
        world.S.Time = world.Data.DaySeconds * (Launch.Has("--night") ? .55f : .2f);
        for (int i = 0; i < 80; i++) world.Tick(.25f);
        if (Launch.Has("--panel")) panel.Select(mine); else panel.Close();
        // Review from between the cart and portal, so either cliff-facing orientation fits in view.
        world.Player = mine.Pos - cliff.Face * .8f;
        view.Zoom = 1.25f;
    }
    private void Press(string button) => panel.FindChild(button, true, false).EmitSignal(BaseButton.SignalName.Pressed);

    /// <summary>Sets up the scene the flags ask for; --zoom then overrides its camera zoom, for looking closely at a model.</summary>
    public void Run() {
        if (Launch.Value("--straw-bed") is string bedPose) { StrawBedScene(bedPose); return; }
        Place();
        view.Zoom = Launch.Number("--zoom", view.Zoom);
        if (Launch.Value("--doorway") is string scene) {
            var cabin = world.Things.First(t => t.Kind == ThingKind.Hut);
            view.AddChild(new DoorwayReview { World = world, View = view, Cabin = cabin, Scene = scene });
            // With --panel the cabin's page shows beside the scene, for a look at its key.
            if (Launch.Has("--panel")) panel.Select(cabin);
        }
        if (Launch.Value("--wolf-scene") is string wolves) view.AddChild(new WolfReview { World = world, View = view, Cabin = world.Things.First(t => t.Kind == ThingKind.Hut), Scene = wolves });
    }

    /// <summary>A straw bed at the game camera, with a model or sleep pose to inspect.</summary>
    private void StrawBedScene(string pose) {
        var at = Terrain.Spawn + new Vector2(-1, 2);
        world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 4f ||
            t.Kind is ThingKind.Tree or ThingKind.Bush or ThingKind.Berries && t.Pos.DistanceTo(at) < 14f);
        world.S.Time = world.Data.DaySeconds * (Launch.Has("--night") ? world.Data.NightStarts + .02f : .28f);
        world.S.Backpack.Remove("steel_axe", world.S.Backpack["steel_axe"]);
        var bed = world.Add(ThingKind.StrawBed, at);
        world.ClearGround(bed);
        world.Player = at + Vector2.Left * 1.6f;
        world.S.Health = 60;
        world.S.Hunger = world.S.Thirst = 100;
        if (Launch.Has("--torch")) {
            world.S.Backpack.Add("torch", 1);
            world.UseTorch(Vector2.Zero);
            world.Tick(World.KindleSeconds);
        }
        if (pose is "sleep" or "wake" or "wake-walk") {
            // The other awake player keeps the clock from skipping to dawn.
            world.Arrive("Straw bed review", 2).Pos = at + Vector2.Right * 35;
            world.Sleep.Start(bed);
            if (pose is "wake" or "wake-walk") {
                var timer = new Timer { WaitTime = StrawBed.LieDownSeconds + .60, OneShot = true, Autostart = true };
                timer.Timeout += () => {
                    world.Sleep.Wake();
                    if (pose != "wake-walk") return;
                    Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Pressed = true });
                    var release = new Timer { WaitTime = StrawBed.LieDownSeconds + .45, OneShot = true, Autostart = true };
                    release.Timeout += () => Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Pressed = false });
                    view.AddChild(release);
                };
                view.AddChild(timer);
            }
        }
        view.Zoom = Launch.Number("--zoom", 3.2f);
        view.CameraTurn = Launch.Number("--camera-turn", 0);
        view.CameraPitch = Launch.Number("--camera-pitch", 30);
        if (Launch.Has("--hide-ui")) ui.Visible = false;
        world.Tick(0);
        if (Launch.Has("--panel")) panel.Select(bed); else panel.Close();
    }

    private void VesselReview() {
        var at = Terrain.Spawn;
        foreach (var t in world.Things.Where(t => t.Pos.DistanceTo(at) < 9).ToArray()) world.S.Things.Remove(t);
        var cabin = world.Add(ThingKind.Hut, at);
        cabin.Inventory.Add(Barrel.AtFill(20), 1);
        var rack = world.Add(ThingKind.Toolstation, at + new Vector2(3.1f, -.8f));
        rack.Inventory.Add(Bucket.Item, 2);
        rack.Inventory.Add("steel_axe", 1);
        rack.Inventory.Add(Water.Full, 1);
        var barrel = world.Add(ThingKind.Barrel, at + new Vector2(.8f, 2.35f));
        barrel.Inventory.Add(Water.Liquid, 25);
        var pile = world.Add(ThingKind.Pile, at + new Vector2(-2.7f, 1.8f));
        pile.Inventory.Add(Barrel.Item, pile.Inventory.Slots * pile.Inventory.StackOf(Barrel.Item));
        world.Player = at + new Vector2(-2f, -2f);
        world.S.Time = world.Data.DaySeconds * .2f;
        world.Tick(0);
        view.CameraOn = at + new Vector2(-.48f, -.86f);
        view.CameraTurn = Launch.Number("--camera-turn", 0);
        view.CameraPitch = Launch.Number("--camera-pitch", 30);
        view.Zoom = 2.15f;
        panel.Close();
        ui.Visible = false;
    }

    private void FarmWateringReview() {
        var at = Terrain.Spawn + new Vector2(2, -4);
        world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 13);
        world.S.Time = world.Data.DaySeconds * .16f;
        var farm = world.Add(ThingKind.Farm, at);
        farm.FarmStage = FarmStage.Growing;
        farm.Progress = world.Farms.GrowSeconds * .5f;
        var barrel = world.Add(ThingKind.Barrel, at + new Vector2(-3.8f, -2));
        barrel.Inventory.Add(Water.Liquid, 50);
        var sign = world.Add(ThingKind.Signpost, at + new Vector2(-4, 1.5f));
        world.Player = sign.Pos;
        world.S.Backpack.Add(Inventory.Coins, 100);
        if (world.Hire(sign, world.Data.HireOffers[1]) is Worker worker) {
            worker.Pos = barrel.Pos + BarrelHaul.SpoutApproach;
            worker.Tool = Bucket.Item;
            world.SetDuty(worker, farm, JobKind.Haul, true);
        }
        world.Player = at + new Vector2(4.8f, 3);
        world.Tick(0);
        AimAt(at);
        view.Zoom = 1.3f;
        if (Launch.Has("--panel")) panel.Select(farm); else panel.Close();
        if (Launch.Has("--hide-ui")) ui.Visible = false;
    }

    /// <summary>Keeps the camera on a spot, turned and tilted as --camera-turn and --camera-pitch say.</summary>
    private void AimAt(Vector2 at) {
        view.CameraOn = at;
        view.CameraTurn = Launch.Number("--camera-turn", view.CameraTurn);
        view.CameraPitch = Launch.Number("--camera-pitch", view.CameraPitch ?? 30);
    }

    /// <summary>Stages a real glass batch just before the part of the motion to record.</summary>
    private void GlassReview(string scene) {
        bool manual = scene == "pump";
        var at = Terrain.Spawn + new Vector2(5, 3);
        world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 28);
        world.S.Time = world.Data.DaySeconds * .2f;
        var forge = world.Add(ThingKind.Reinforced, at);
        var post = world.Add(ThingKind.Signpost, at + new Vector2(-9, 0));
        world.Player = post.Pos;
        world.S.Backpack.Add(Inventory.Coins, 100);
        if (world.Hire(post, world.Data.HireOffers[1]) is Worker worker) {
            worker.Pos = forge.ToWorld(manual ? GlassBlowing.BlowStand : World.BellowsStand);
            world.SetDuty(worker, forge, manual ? JobKind.BlowGlass : JobKind.Bellows, true);
        }
        world.Player = forge.ToWorld(manual ? World.BellowsStand : GlassBlowing.BlowStand);
        world.SetRecipe(forge, GlassBlowing.Item);
        forge.Tools!.Add("steel_pipe", 1);
        forge.Inventory.Add(Beach.Sand, 5);
        if (Launch.Has("--full-output")) forge.Inventory.Add(GlassBlowing.Item, forge.Inventory.SlotRoom(GlassBlowing.Item));
        forge.Burner!.Add("coal", 1);
        forge.Heat = manual ? GlassBlowing.MinHeat : World.SteelHeat;
        float start = scene switch { "success" => 9.35f, "failure" => 8, "work" => 3, _ => 0 };
        for (int i = 0; i < 660 && forge.Progress < start; i++) world.Tick(1f / 60);
        if (scene == "failure") forge.Heat = GlassBlowing.MaxHeat + 4;
        AimAt(at);
        view.Zoom = 3.5f;
        if (Launch.Has("--panel")) panel.Select(forge); else panel.Close();
        if (Launch.Has("--hide-ui")) ui.Visible = false;
    }

    /// <summary>Reviews the same lantern models drawn on the ground, in the hand or on the belt during play.</summary>
    private void LanternReview() {
        var at = Terrain.Spawn + new Vector2(5, 3);
        world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 12);
        bool night = Launch.Has("--night");
        world.S.Time = world.Data.DaySeconds * (night ? .76f : .2f);
        world.S.DensOut = (1 << WolfPatrols.Dens.Length) - 1; world.S.DenHours = 24; world.Firebugs.KeepIn();
        world.SelectHotbar(1);
        string GiveLantern(int count) {
            var lantern = world.Lanterns.NewEmpty();
            world.Lanterns.FillForReview(lantern, count);
            world.S.Backpack.Add(lantern);
            return lantern.Key;
        }
        string? scene = Launch.Value("--lantern");
        bool carried = scene is "held" or "belt" or "full-wear" or "backpack" or "torch";
        world.Player = carried ? at : at + new Vector2(-2.2f, -2.2f);
        if (scene is "belt" or "full-wear") {
            for (int i = 0; i < (scene == "belt" ? 2 : 9); i++) world.SetHotbar(i, GiveLantern(i == 0 ? 10 : 4));
            if (Launch.Has("--lantern-motion")) world.Lanterns.ToggleInHand(Lanterns.Id(GiveLantern(10)));
        } else if (scene == "held") world.Lanterns.ToggleInHand(Lanterns.Id(GiveLantern(10)));
        else if (scene == "backpack") {
            GiveLantern(0);
            world.SetHotbar(0, GiveLantern(4));
            world.Firebugs.Catch(world.S.Backpack, Lanterns.Capacity);
        } else if (scene == "torch") { world.S.Backpack.Add("torch", 1); world.UseTorch(Vector2.Zero); } else _ = world.Lanterns.Place(GiveLantern(scene == "partial" ? 4 : Launch.Has("--dark") ? 0 : 10), at) ?? throw new InvalidOperationException("The lantern review needs open ground.");
        world.Tick(scene == "torch" ? World.KindleSeconds : 0);
        AimAt(at);
        if (carried && Launch.Has("--lantern-motion")) ReviewLanternMotion();
        view.Zoom = carried ? 3.25f : night ? 1.6f : 4;
        if (scene == "backpack") panel.Open(Page.Backpack); else panel.Close();
        ui.Visible = scene == "backpack" && !Launch.Has("--hide-ui");
    }

    /// <summary>Records the held and worn lanterns from standing through a jog, a sprint and a stop.</summary>
    private void ReviewLanternMotion() {
        view.CameraOn = null;
        var motion = view.CreateTween();
        motion.TweenInterval(.30);
        motion.TweenCallback(Callable.From(() => KeyPress(Key.D, true)));
        motion.TweenInterval(.85);
        motion.TweenCallback(Callable.From(() => KeyPress(Key.Shift, true)));
        motion.TweenInterval(.85);
        motion.TweenCallback(Callable.From(() => { KeyPress(Key.D, false); KeyPress(Key.Shift, false); }));

        static void KeyPress(Key key, bool down) => Input.ParseInputEvent(
            new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = down });
    }

    private void Place() {
        if (Launch.Has("--lantern")) { LanternReview(); return; }
        if (Launch.Value("--glassblowing") is string glass) { GlassReview(glass); return; }
        if (Launch.Has("--farm-watering")) { FarmWateringReview(); return; }
        if (Launch.Has("--steel-vessels")) { VesselReview(); return; }
        if (Launch.Has("--environment-review")) {
            string location = Launch.Value("--environment-review") ?? "forest";
            float coastAcross = Terrain.Across(Terrain.Spawn);
            var coastCenter = Terrain.Point(Terrain.Coast(coastAcross) - 2, coastAcross);
            var reviewCliff = Cliff.Of(Kinds.Parse(Launch.Value("--mine-ore")) ?? ThingKind.Coal);
            var mineCenter = reviewCliff.MineSpot - reviewCliff.Face * 1.4f;
            world.Player = location switch {
                "lake" => Terrain.WaterPoint(Terrain.Lake + Terrain.Seaward) + Terrain.Seaward,
                "coast" => coastCenter - Terrain.Seaward * 3,
                "mine" => reviewCliff.MineSpot + reviewCliff.Face,
                "terrain" => Terrain.Point(125, -18),
                "den" => WolfPatrols.Dens[3].Door + WolfPatrols.Dens[3].Face * 2.5f,
                _ => Terrain.Spawn,
            };
            world.S.Time = world.Data.DaySeconds * .2f;
            world.S.Workers.Clear(); world.S.Orders.Clear();
            world.Tick(0);
            view.EnvironmentReviewFog = Launch.Has("--review-fog");
            view.EnvironmentCenter = location switch { "lake" => Terrain.Lake + Terrain.Seaward * 3, "terrain" => Terrain.Point(125, -18), "coast" => coastCenter, "mine" => mineCenter, _ => null };
            if (location == "mine") {
                var mine = world.Construction.Build(ThingKind.Mine, reviewCliff.MineSpot)!;
                mine.MineStage = MineStage.Open;
                mine.Output!.Add(Mines.Ore(mine), 12);
            }
            // The wide terrain camera must sit above the peaks rather than inside a near mountain face.
            if (location is "terrain" or "coast" or "mine") view.EnvironmentCameraDistance = 140;
            view.Zoom = location switch { "den" => 1.6f, "lake" => .44f, "terrain" => .24f, "coast" => .36f, "mine" => .85f, _ => .75f };
            panel.Close(); ui.Visible = false; kit.HoldWhile(() => true);
            return;
        }
        if (Launch.Has("--diary")) { DiaryReview(); return; }
        if (Launch.Has("--firebugs")) {
            // A night meadow: three nettles in a row, one of each look, a tool station holding a bug net, and firebugs hovering at several distances from a player who carries a net.
            // The last firebug starts near enough to the player to dart off as the scene opens.
            var at = Terrain.Spawn + new Vector2(9, 9);
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 15);
            world.S.NextId += (3 - world.S.NextId % 3) % 3;
            for (int i = 0; i < 3; i++) world.Add(ThingKind.Nettle, at + new Vector2(-2.6f + i * 1.3f, 2.6f - i * 1.3f) + new Vector2(1, 1) * 1.6f, 1);
            // By day, with --day-phase, only the nettles stand.
            world.S.Time = world.Data.DaySeconds * Launch.Number("--day-phase", world.Data.NightStarts + .1f);
            // No wolves and no more firebugs than these come out.
            world.S.DensOut = (1 << WolfPatrols.Dens.Length) - 1; world.S.DenHours = 24; world.Firebugs.KeepIn();
            foreach (var off in new Vector2[] { new(-4.5f, -1), new(-2.5f, -4.5f), new(1, -5.5f), new(4.6f, -2), new(5, 2.5f), new(-5.5f, 3), new(-1, 5.5f), new(2.4f, 1.6f) })
                world.Add(ThingKind.Firebug, at + off, 1);
            world.S.Backpack.Add("bug_net", 1);
            world.S.HotbarSlots[0] = "bug_net";
            // A tool station with a bug net on its first peg.
            world.Add(ThingKind.Toolstation, at + new Vector2(-3.2f, -3.2f)).Inventory.Add("bug_net", 1);
            world.S.Workers.Clear(); world.S.Orders.Clear(); world.S.Remembered.Clear(); world.S.Wolves.Clear();
            world.Player = at;
            world.Tick(0); view.Zoom = Launch.Number("--zoom", 1.8f);
            if (Launch.Has("--hide-ui")) ui.Visible = false;
            if (Launch.Has("--chase")) view.AddChild(new FirebugChase { World = world, View = view });
            panel.Close(); return;
        }
        if (Launch.Has("--bushes")) {
            // A row of berry bushes in their five stages, from full to picked clean, with all three variants.
            var at = Terrain.Spawn + new Vector2(9, 9);
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 15);
            // Bush variants are selected from the id; start the row at variant zero and include all three.
            world.S.NextId += (3 - world.S.NextId % 3) % 3;
            for (int stage = WorldModels.BerryStages; stage >= 0; stage--)
                world.Add(ThingKind.Berries, at + new Vector2(-1.5f, 1.5f) * (stage - 2), stage * World.BushBerries / WorldModels.BerryStages);
            // In front of them, three plain bushes, the last half dug out, and a stump beside one half dug out.
            for (int i = 0; i < 3; i++) world.Add(ThingKind.Bush, at + new Vector2(-.5f + i * 1.6f, 3.4f - i * 1.6f), World.BushLeaves).Progress = i == 2 ? GatherRules.DigSeconds * .7f : 0;
            for (int i = 0; i < 2; i++) world.Add(ThingKind.Stump, at + new Vector2(3.4f + i * 1.2f, -.4f - i * 1.2f), World.StumpWood).Progress = i * GatherRules.DigSeconds * .7f;
            // A tree two strokes from falling and a shovel to dig with, to see a tree leave its stump and a stump or bush come out.
            world.Add(ThingKind.Tree, at + new Vector2(-4, -1), 8);
            world.S.Backpack.Add("steel_shovel", 1);
            world.S.Time = world.Data.DaySeconds * .2f;
            world.S.Workers.Clear(); world.S.Orders.Clear(); world.S.Remembered.Clear();
            world.Player = at + new Vector2(-1.5f, -1.2f);
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
        var chest = world.Add(ThingKind.Chest, Terrain.Spawn + new Vector2(2, 3)); world.SetFilter(chest, Inventory.Everything, false, true); chest.Inventory.Add("wood", 35); chest.Inventory.Add("coal", 30); chest.Inventory.Add("iron_bar", 15);
        var anvil = world.Add(ThingKind.Anvil, Terrain.Spawn + new Vector2(4, 4)); anvil.Tools!.Add("steel_hammer", 1); var sign = world.Add(ThingKind.Signpost, Terrain.Spawn + new Vector2(-1, 4));
        world.S.Backpack.Add(Inventory.Coins, 140); world.S.Hunger = 55; world.S.Thirst = 30; world.S.Backpack.Add(Water.Full, 4); world.S.Backpack.Add("steel_pick", 1);
        var bench = world.Add(ThingKind.Bench, Terrain.Spawn + new Vector2(-4, 1.5f)); world.SetRecipe(bench, "crate"); bench.Inventory.Add("planks", 12); bench.Tools!.Add("stone_chisel", 1); bench.Tools.Add("steel_hammer", 1);
        var rack = world.Add(ThingKind.Toolstation, Terrain.Spawn + new Vector2(-5, -1.5f)); world.SetFilter(rack, Inventory.Everything, false, true); rack.Inventory.Add("steel_axe", 1); rack.Inventory.Add("rake", 1); rack.Inventory.Add("steel_shovel", 1);
        world.Add(ThingKind.LeafSpot, Terrain.Spawn + new Vector2(-3.5f, 5), World.LeavesPerSpot);
        var crate = world.Add(ThingKind.Crate, Terrain.Spawn + new Vector2(1.5f, 1.5f)); world.SetFilter(crate, Inventory.Everything, false, true); crate.Inventory.Add("berries", 6); crate.Inventory.Add("steel_axe", 1); crate.Inventory.Add("steel_pick", 1);
        foreach (var (item, count) in new[] { ("steel_axe", 1), ("steel_pick", 1), ("stone_chisel", 1), ("wood", 9), ("planks", 8), ("wood_parts", 3), ("crate", 1), ("torch", 2) }) world.S.Backpack.Add(item, count);
        world.S.Milestones.UnionWith(new[] { "bench", "crate", "sold", "furnace", "iron_bar", "reinforced", "steel_bar", "anvil" });
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
        world.S.Making = "planks"; world.S.Crafting.Add(new Queued { Recipe = "wood_parts", Count = 3 }); world.S.CraftTime = 6;
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
        if (Launch.Has("--anvil")) {
            // The anvil's page with the anvil in the open: the trees and bushes beside it are cleared.
            // The camera turns and tilts by the flags.
            world.S.Things.RemoveAll(t => t.Kind is ThingKind.Tree or ThingKind.Bush or ThingKind.Berries && t.Pos.DistanceTo(anvil.Pos) < 10);
            world.Player = anvil.Pos + new Vector2(-1, 1.2f); panel.Select(anvil);
            // --anvil-work loads it with steel bars and stands the player within reach with nothing crafting by hand, so
            // they hammer with its slotted hammer.
            if (Launch.Has("--anvil-work")) {
                anvil.Inventory.Add("steel_bar", 20); world.Player = anvil.Pos + new Vector2(-.6f, .9f);
                world.S.Making = ""; world.S.Crafting.Clear(); world.S.CraftTime = 0;
            }
            AimAt(anvil.Pos);
        }
        if (Launch.Has("--neighbor")) { world.As(world.Arrive("neighbor", 2), () => world.Player = bench.Pos + Vector2.One); world.Player = bench.Pos + new Vector2(-2, 4); panel.Close(); view.Zoom = 1.6f; }
        if (Launch.Has("--craft")) panel.Open(Page.Craft);
        // --map-item also clicks an item's slot, to show its details.
        if (Launch.Has("--map")) { panel.Map.Open(); if (Launch.Value("--map-item") is string item) panel.Map.NodeOf(item)?.GetNode<Button>("slot").EmitSignal(BaseButton.SignalName.Pressed); }
        if (Launch.Has("--crew")) panel.Open(Page.Crew);
        if (Launch.Has("--worker")) {
            // Worker review: jobs at three stations in order, their hauls in one group, a load in hand and a tool.
            var hand = world.Workers[0];
            world.Assign(hand, anvil); world.SetDuty(hand, bench, JobKind.Craft, true); world.SetDuty(hand, bench, JobKind.Haul, true); world.SetDuty(hand, bellows, JobKind.Haul, true);
            world.AddHaulGroup(hand);
            foreach (var place in new[] { bellows, anvil, bench }) {
                int from = hand.Jobs.FindIndex(e => e is Duty { Job: JobKind.Haul } d && d.Thing == place.Id);
                world.MoveJob(hand, new JobSpot(from), new JobSpot(hand.Jobs.Count - 1, int.MaxValue));
            }
            hand.Bag.Add("steel_parts", 6); hand.Tool = "steel_pick";
            world.Player = hand.Pos + Vector2.One;
            if (Launch.Has("--crew")) panel.Open(Page.Crew); else panel.Select(hand);
        }
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
        if (Launch.Has("--goods")) {
            // A pile of every good, eight to a row and --goods-share full (six tenths unless given), then a compost pile.
            // --goods-pile N looks at the Nth pile, counting from 0.
            const int perRow = 8;
            var at = Terrain.Spawn + new Vector2(-3, 9);
            string[] goods = world.Data.Items.Keys.Where(item => item != Inventory.Coins
                && ContainerItem.Base(item) == item).ToArray();
            float share = Launch.Number("--goods-share", .6f); int look = (int)Launch.Number("--goods-pile", 0);
            Vector2 Spot(int pile) => at + new Vector2(pile % perRow * 3.2f, pile / perRow * 3.2f);
            world.S.Things.RemoveAll(t => t.Pos.X > at.X - 4 && t.Pos.X < at.X + perRow * 3.2f + 4 && t.Pos.Y > at.Y - 4 && t.Pos.Y < at.Y + (goods.Length / perRow + 1) * 3.2f + 4);
            world.S.Workers.Clear(); world.S.Orders.Clear();
            for (int i = 0; i < goods.Length; i++) {
                var pile = world.Add(ThingKind.Pile, Spot(i));
                int amount = Math.Max(1, (int)(pile.Inventory.Slots * pile.Inventory.StackOf(goods[i]) * share));
                if (goods[i] == Firebugs.Item) world.Firebugs.Catch(pile.Inventory, amount);
                else if (goods[i] == Lanterns.Item) for (int n = 0; n < amount; n++) pile.Inventory.Add(world.Lanterns.NewEmpty());
                else pile.Inventory.Add(goods[i], amount);
            }
            var compost = world.Add(ThingKind.Pile, Spot(goods.Length)); compost.Compost = true;
            compost.Inventory.Add("leaves", (int)(300 * share)); compost.Inventory.Add("grass", (int)(300 * share)); compost.Inventory.Add("fertilizer", (int)(200 * share));
            // The player crouches in front of the pile looked at, clear of it.
            world.Player = Spot(look) + new Vector2(1.9f, 1.9f); world.Tick(0); view.Zoom = 2.2f; panel.Close();
        }
        if (Launch.Has("--paths")) {
            // Gravel paths in each shape tiles join into: one tile, two and three in a row, two corner to corner, a turn and a
            // square of four; then a path that runs under a chest, past a pile and off at a slant, and two staircases, one a
            // tile wide and one two.
            // --top looks straight down with the tiles square on screen, as on squared paper.
            var at = Terrain.TileAt(Terrain.Spawn) + new Vector2I(-4, 12);
            world.S.Things.RemoveAll(t => t.Pos.X > at.X - 6 && t.Pos.X < at.X + 20 && t.Pos.Y > at.Y - 6 && t.Pos.Y < at.Y + 16);
            (int X, int Y)[] tiles = {
                (0, 0), (0, 2), (1, 2), (6, 0), (6, 1), (6, 2), (3, 4), (4, 3), (3, 7), (4, 7), (4, 6), (3, 9), (4, 9), (3, 10), (4, 10),
                (9, 0), (10, 0), (11, 0), (12, 0), (12, 1), (12, 2), (12, 3), (13, 4), (14, 5), (15, 6), (15, 7), (14, 8), (13, 9), (12, 9), (11, 9), (10, 9),
                (6, 11), (7, 11), (7, 12), (8, 12), (8, 13), (9, 13),
                (11, 11), (12, 11), (11, 12), (12, 12), (13, 12), (12, 13), (13, 13), (14, 13),
            };
            foreach (var (x, y) in tiles) world.GravelPaths.Lay(at + new Vector2I(x, y));
            world.SetFilter(world.Add(ThingKind.Chest, at + new Vector2(12, 2)), Inventory.Everything, false, true);
            var pile = world.Add(ThingKind.Pile, at + new Vector2(9.5f, 5)); pile.Inventory.Add("gravel", 5 * pile.Inventory.StackSize);
            world.Player = at + new Vector2(7.5f, 6.5f); world.Tick(0); view.Zoom = .7f; panel.Close();
            if (Launch.Has("--top")) { view.CameraTurn = -45; view.CameraPitch = 89; }
        }
        if (Launch.Has("--farm")) {
            // A row of plots, one per look: bare, sown soil, straws, green plants and ripe wheat half harvested, beside a pile of wheat.
            var at = Terrain.Spawn + new Vector2(-6, 9);
            for (int i = 0; i < 5; i++) {
                var plot = world.Add(ThingKind.Farm, at + new Vector2(i * Farms.HalfSize * 2, 0));
                plot.FarmStage = i switch { 0 => FarmStage.Fallow, 4 => FarmStage.Ripe, _ => FarmStage.Growing };
                plot.Progress = i == 4 ? world.Farms.HarvestSeconds / 2 : (i - 1) * world.Farms.GrowSeconds / 3 + 1;
            }
            var wheat = world.Add(ThingKind.Pile, at + new Vector2(0, Farms.HalfSize + Construction.ZoneSize / 2)); wheat.Inventory.Add("wheat", 300);
            world.Player = at + new Vector2(12, 2.5f); world.Tick(0); view.Zoom = .7f;
            if (Launch.Has("--panel")) panel.Select(world.Things.Last(t => t.Kind == ThingKind.Farm)); else panel.Close();
        }
        if (Launch.Has("--cabin")) {
            // The cabin's page at bedtime, wounded but fed: what a night's sleep would leave.
            world.S.Time = world.Data.DaySeconds * .69f; world.S.Health = 40; world.S.Hunger = 80; world.S.Thirst = 70;
            world.Player = hut.Pos + new Vector2(1, 2); world.Tick(0); view.Zoom = 1.7f; panel.Select(hut);
        }
        if (Launch.Has("--kitchen")) {
            // The cooking table mid-stew beside a canning station sealing cans, with the mortar slotted.
            // --grind has the player at the table instead, grinding wheat with its mortar.
            bool grind = Launch.Has("--grind");
            var at = Terrain.Spawn + new Vector2(-3, 8);
            world.S.Things.RemoveAll(t => t.Pos.DistanceTo(at) < 4 && !t.IsResource);
            var table = world.Add(ThingKind.Cooktable, at); table.Tools!.Add("mortar", 1); world.SetRecipe(table, grind ? "flour" : "meat_stew");
            if (grind) {
                table.Inventory.Add("wheat", 20); world.S.Making = ""; world.S.Crafting.Clear();
            } else {
                table.Inventory.Add(Water.Liquid, 2); table.Inventory.Add("raw_meat", 2); table.Burner!.Add("wood", 2);
            }
            var cannery = world.Add(ThingKind.Cannery, at + new Vector2(2.2f, 0)); world.SetRecipe(cannery, "canned_meat_stew");
            cannery.Inventory.Add("meat_stew", 2); cannery.Inventory.Add("tin_can", 2); cannery.Burner!.Add("wood", 2);
            world.Player = at + (grind ? new Vector2(.4f, -1.1f) : new Vector2(1, 2.5f)); world.Tick(.5f); view.Zoom = grind ? 3.4f : 1.7f;
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
                world.SetDuty(packing, packer, JobKind.Craft, true); packing.Pos = at + World.PackStand;
                world.Player = at + new Vector2(.4f, -2.6f); view.Zoom = 3.4f; world.Tick(.1f);
            }
            world.SetFilter(sign, World.CrateOf("bread"), false, true); world.SetFilter(sign, World.CrateOf("berries"), false, true); world.SetAutoSell(sign, true);
            if (Launch.Has("--post")) { world.Player = sign.Pos + Vector2.One; panel.Select(sign); Press("filters"); } else if (Launch.Has("--panel")) { panel.Select(packer); if (Launch.Has("--pick")) Press("pack_good"); } else panel.Close();
        }
        if (Launch.Has("--tools")) { world.Player = rack.Pos + new Vector2(2.5f, 2.5f); view.Zoom = 1.6f; if (Launch.Has("--panel")) panel.Select(rack); else panel.Close(); if (Launch.Has("--filters")) { panel.Select(rack); Press("filters"); } }
        if (Launch.Has("--tin")) { var cliff = Cliff.Of(ThingKind.Tin); world.Player = cliff.Center + cliff.Face * 5.5f; world.Tick(0); panel.Close(); }
        if (Launch.Has("--pantry")) {
            foreach (var id in new[] { "tin_ore", "tin_bar", "tin_can", "steel_chisel", "rake", "mortar", "leaves", "grass", "fertilizer", "wheat_seeds", "wheat", "flour", "dough", "bread", "crushed_berries", BerryJam.Bottle, "meat_stew", "burnt_steak", "canned_jam", "canned_meat_stew" }) world.S.Backpack.Add(id, 3);
            panel.Open(Page.Backpack);
        }
        // Hovering a recipe shows its description below the grids and as a tooltip at the pointer.
        if (Launch.Has("--hover")) panel.GetTree().CreateTimer(.5).Timeout += () => {
            if (panel.FindChild("craft_torch", true, false) is not Button cell) return;
            cell.EmitSignal(Control.SignalName.MouseEntered);
            var at = Game.WindowPoint(cell);
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
        if (Launch.Has("--idle")) {
            // Idle review: the crew's jobs are taken off and they sit at the campfire, which has no wood and burns while they do.
            foreach (var w in world.Workers) world.Assign(w, null);
            fire.Burn = 0;
            for (int i = 0; i < 60; i++) world.Tick(.25f);
            world.Player = fire.Pos + new Vector2(3, 3); panel.Close(); view.Zoom = 1.6f;
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
        foreach (var offset in new[] { new Vector2(2.5f, -1), new Vector2(-6, 3), new Vector2(5, 5) }) world.S.Wolves.Add(new Wolf { Pos = spot + offset, Target = world.Me.Id });
        world.Add(ThingKind.WolfCorpse, spot + new Vector2(-2, -2), 4);
        panel.Close(); view.Zoom = 1.6f;
    }
}
