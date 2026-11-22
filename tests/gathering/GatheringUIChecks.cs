using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static ModelSteps;
using static PlayerSteps;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The UI checks of gathering: right-click gathering, loose pieces, berry bushes, and felling and digging up stumps.</summary>
    private async Task RunGatheringUIChecks() {
        await TestClean(CheckEveryGatheringBlowLandsItsToolOnWhatItWorks);
        await TestClean(CheckRightClickGathersTreeAndHarvestsFarm);
        await TestClean(CheckRightClickGathersAndLeavesASelectedWorkerMarked);
        await TestClean(CheckOneRightClickWalksOverAndPicksUpAWholeLoosePiece);
        await TestClean(CheckBerryBushHidesOnlyItsFruitWhenPicked);
        await TestClean(CheckATreeCrownKeepsALoosePieceBehindItClickable);
        await TestClean(CheckFelledTreeTopplesOverItsStumpWhichAShovelDigsUp);
        await TestClean(CheckStumpBuiltOverGoesWithoutBeingThrownUp);
    }

    /// <summary>Holding the right mouse button on a tree or a ripe farm plot walks over and works it.</summary>
    private async Task CheckRightClickGathersTreeAndHarvestsFarm() {
        var home = Hosted.Player;
        var tree = world.Things.First(t => t.Kind == ThingKind.Tree && t.Pos == Terrain.Spawn + new Vector2(5, 0));
        int wood = world.Backpack["wood"];
        var point = view.Screen(tree.Pos) - new Vector2(0, 70);
        Require(view.Pick(point)?.Id == tree.Id, "Illustrated tree hit area must select the visible resource.");
        var windowPoint = GetViewport().GetFinalTransform() * point;
        int soundsBefore = sounds.Played;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
        await Until(() => view.PlayerAnimation == ActorAction.SteelChop && sounds.Heard("chop_tree", soundsBefore) && world.Backpack["wood"] > wood,
            "holding RMB approaching and gathering a tree with the starting axe's steel stroke and chop sound", 8);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = 0, Position = windowPoint, Pressed = false });
        await Frames();
        var farm = Hosted.Add(ThingKind.Farm, world.Player + new Vector2(-3, 3)); farm.FarmStage = FarmStage.Ripe;
        await Frames(2);
        var farmPoint = GetViewport().GetFinalTransform() * view.Screen(farm.Pos);
        Require(view.Pick(view.Screen(farm.Pos))?.Id == farm.Id, "A farm plot must be picked where it is drawn.");
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = farmPoint, GlobalPosition = farmPoint, Pressed = true });
        await Until(() => farm.Progress > 0, "holding RMB on a ripe farm plot walking over and harvesting by hand", 6);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = 0, Position = farmPoint, Pressed = false });
        await Frames(); Require(farm.Progress > 0 && farm.FarmStage == FarmStage.Ripe, "holding RMB on a ripe farm plot must walk over and harvest by hand.");
        RemoveThings(Hosted, t => t == farm);
        Warp(Hosted, home); await Frames(4);
    }

    private async Task CheckRightClickGathersAndLeavesASelectedWorkerMarked() {
        var home = Hosted.Player;
        var tree = world.Things.First(t => t.Kind == ThingKind.Tree && t.Pos == Terrain.Spawn + new Vector2(5, 0));
        var worker = AddWorker(Hosted, world.Player + new Vector2(-2, 2));
        await Frames(); await Click(view.Screen(worker.Pos, 1));
        Require(view.Selection.Worker == worker.Id, "A left click on a worker must select them.");
        var point = view.Screen(tree.Pos) - new Vector2(0, 70);
        Require(Uncovered(point), "The tree must be clear of the worker's page.");
        var windowPoint = GetViewport().GetFinalTransform() * point;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
        await Until(() => view.Gathering, "a right click on a tree starting to gather", 4);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = 0, Position = windowPoint, Pressed = false });
        await Frames();
        Require(view.Selection.Worker == worker.Id, "A right click on a tree must leave a selected worker marked.");
        Require(panel.HeadingTitle.Text == worker.Name, "A right click on a tree must leave the worker's page open.");
        Hosted.Fire(worker); await CloseWithEsc();
        Warp(Hosted, home); await Frames(4);
    }

    /// <summary>One right click walks over to a loose piece and picks it up whole, without the gather animation.</summary>
    private async Task CheckOneRightClickWalksOverAndPicksUpAWholeLoosePiece() {
        var home = Hosted.Player;
        var gatheringPosition = Terrain.Spawn - new Vector2(30, 30);
        // Full-size tree crowns reach several tiles up the screen, so clear enough room for a clean click.
        RemoveThings(Hosted, t => t.Pos.DistanceTo(gatheringPosition) < 12);
        Warp(Hosted, gatheringPosition);
        // Far enough that the player has to keep walking after the button is released.
        var looseOre = Hosted.Add(ThingKind.LooseCoal, gatheringPosition + new Vector2(2, 0), 2);
        int oreBefore = world.Backpack["coal"];
        await Frames();
        var point = view.Screen(looseOre.Pos) - new Vector2(0, 12) * view.Zoom;
        Require(view.Pick(point)?.Id == looseOre.Id, "The loose ore fixture must be selectable.");
        var windowPoint = GetViewport().GetFinalTransform() * point;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = false });
        bool harvestAnimation = false;
        for (int i = 0; i < 120 && world.Find(looseOre.Id) != null; i++) { await Frames(); harvestAnimation |= view.PlayerAnimation == ActorAction.Gather; }
        Require(world.Find(looseOre.Id) == null && world.Backpack["coal"] == oreBefore + 2, "One right-click must walk over and pick up the whole loose piece.");
        Require(!harvestAnimation, "Picking up loose ore must not play the gathering animation.");
        Warp(Hosted, home); await Frames(4);
    }

    /// <summary>Picking a bush clean hides only its fruit, which shows again once it is replenished.</summary>
    private async Task CheckBerryBushHidesOnlyItsFruitWhenPicked() {
        // Exercise the rendered bush's independent fruit handle through real gathering input.
        var home = Hosted.Player;
        var berryBush = Hosted.Add(ThingKind.Berries, world.Player + new Vector2(1, -1), 4);
        Drop(Hosted, "berries");
        await Frames(2);
        var bushProp = (BushProp)view.PropOf(berryBush.Id)!;
        Require(bushProp.Fruit is { Visible: true }, "A ripe berry bush must display its fruit.");
        var point = view.Screen(berryBush.Pos, .35f);
        Require(view.Pick(point)?.Id == berryBush.Id, "The bush foliage must select its berry resource.");
        var windowPoint = GetViewport().GetFinalTransform() * point;
        int soundsBefore = sounds.Played;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
        await Until(() => sounds.Heard("rustle", soundsBefore), "picking a berry bush by hand rustling its leaves", 3);
        await Until(() => berryBush.Remaining == 0, "holding RMB on a berry bush picking it clean", world.Data.GatherSeconds + 3);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = false });
        await Frames(2);
        Require(berryBush.Remaining == 0, "Harvesting must empty the berry bush.");
        Require(world.Backpack["berries"] == 4, "Harvesting must put the berries in the backpack.");
        Require(!bushProp.Fruit.Visible, "Harvesting must hide the fruit.");
        Require(bushProp.Body.Visible, "Harvesting must leave the bush body visible.");
        berryBush.Remaining = 4; await Frames(2);
        Require(bushProp.Fruit.Visible, "A replenished bush must show its fruit again.");
        RemoveThings(Hosted, t => t == berryBush);
        Warp(Hosted, home); await Frames(4);
    }

    /// <summary>A loose piece far enough behind a tree sits under its crown on screen and is still the one picked; a crown alone picks nothing, and the foot of the trunk picks the tree.</summary>
    private async Task CheckATreeCrownKeepsALoosePieceBehindItClickable() {
        var tree = world.Things.First(t => t.Kind == ThingKind.Tree && t.Pos == Terrain.Spawn + new Vector2(5, 0));
        var hidden = Hosted.Add(ThingKind.LooseCoal, tree.Pos - new Vector2(3.3f, 3.3f), 2);
        await Frames();
        var point = view.Screen(hidden.Pos, .15f);
        Require(view.Pick(point)?.Id == hidden.Id, "A tree crown must not steal the click from a loose piece behind it.");
        RemoveThings(Hosted, t => t == hidden); await Frames();
        Require(view.Pick(point) == null, "A crown alone must not select its tree, so a click in a forest swings instead.");
        Require(view.Pick(view.Screen(tree.Pos, .5f))?.Id == tree.Id, "Clicking the foot of a trunk must select its tree.");
    }

    /// <summary>A tree felled in front of the player topples over the stump it leaves, and holding the right mouse button on the stump digs it up with the shovel.</summary>
    private async Task CheckFelledTreeTopplesOverItsStumpWhichAShovelDigsUp() {
        // The server's position, since the window's copy may not have caught up with the last check's warp.
        var start = Hosted.Player;
        var tree = Hosted.Add(ThingKind.Tree, start + new Vector2(2, 2), 4);
        Give(Hosted, "steel_shovel", 1);
        await Frames(2);
        int wood = world.Backpack["wood"];
        var point = RightPress(tree.Pos);
        await Until(() => world.Find(tree.Id) == null && view.Departing == 1, "holding RMB on a tree felling it, its model toppling once the tree is gone", 8);
        RightRelease(point);
        var stump = world.Things.Single(t => t.Kind == ThingKind.Stump && t.Pos == tree.Pos);
        Require(view.PropOf(stump.Id) is DugProp { Shown: true }, "The stump must stand under the toppling tree.");
        await Until(() => view.Departing == 0, "the felled tree fading away over its stump", 4);
        int soundsBefore = sounds.Played;
        point = RightPress(stump.Pos);
        await Until(() => stump.Progress > 0 && view.PlayerAnimation == ActorAction.Dig && (sounds.Heard("dig_earth", soundsBefore) || sounds.Heard("dig_toss", soundsBefore)), "holding RMB on a stump digging at it with the shovel, heard in the soil", 6);
        await Until(() => world.Find(stump.Id) == null && view.Departing == 1, "the stump coming out of the ground, thrown up as it goes", 8);
        // The tree held 4 wood and its stump 3.
        await Until(() => world.Backpack["wood"] == wood + 7 && view.Departing == 0 && view.PlayerAnimation != ActorAction.Dig, "the stump's wood in the backpack, its model gone and the digging done", 3);
        RightRelease(point); await Frames();
        Drop(Hosted, "steel_shovel", 1); Drop(Hosted, "wood", 7);
        Warp(Hosted, start); await CloseWithEsc(); await Frames(4);
    }

    /// <summary>A stump a building goes up over just goes, however far it was dug: only one dug out is thrown up out of the ground.</summary>
    private async Task CheckStumpBuiltOverGoesWithoutBeingThrownUp() {
        var at = Hosted.Player + new Vector2(2, 2);
        var stump = Hosted.Add(ThingKind.Stump, at, World.StumpWood); stump.Progress = GatherRules.DigSeconds * .8f;
        await Frames(2);
        Require(view.PropOf(stump.Id) is DugProp { Shown: true }, "A stump in sight must be drawn.");
        Give(Hosted, world.Data.Buildings[ThingKind.Chest].Cost);
        var chest = Hosted.Construction.Build(ThingKind.Chest, at);
        await Frames(2);
        Require(chest != null && world.Find(stump.Id) == null, "A chest must go up over a stump and clear it away.");
        Require(view.Departing == 0 && view.PropOf(stump.Id) is not { Shown: true }, "A stump built over must just go, not be thrown up through the building.");
        RemoveThings(Hosted, t => t == chest); await Frames(2);
    }

    /// <summary>
    /// Every blow at gathering lands its tool on what is gathered as it is heard, wherever the player stands: the axe's
    /// head on the trunk's side, the pick's point on the rock and the shovel's blade in the ground at a stump's foot.
    /// A rock mined of its last piece crumbles away.
    /// </summary>
    private async Task CheckEveryGatheringBlowLandsItsToolOnWhatItWorks() {
        var start = Hosted.Player;
        var (wood, stone) = (world.Backpack["wood"], world.Backpack["stone"]);
        Give(Hosted, "steel_pick", 1); Give(Hosted, "steel_shovel", 1);
        var rig = view.PlayerRig;
        foreach (var (kind, offset) in new[] {
            (ThingKind.Tree, new Vector2(2, 1)), (ThingKind.Tree, new Vector2(-1.6f, 1.9f)), (ThingKind.Stone, new Vector2(2, -1)), (ThingKind.Stone, new Vector2(-2.1f, .4f)),
            (ThingKind.Stump, new Vector2(1.5f, 1.5f)), (ThingKind.Stump, new Vector2(-2, -.6f)) }) {
            Warp(Hosted, start); await Frames(3);
            var thing = Hosted.Add(kind, start + offset, kind == ThingKind.Stone ? World.StonePile : 40);
            string sound = kind switch { ThingKind.Tree => "chop_tree", ThingKind.Stone => "mine_ore", _ => "dig_earth" };
            await Frames(2);
            int soundsBefore = sounds.Played;
            Vector2? point = RightPress(thing.Pos);
            await Until(() => sounds.Heard(sound, soundsBefore) && view.PlayerMark != null, $"the player working a {kind} {offset} away and its blow heard", 8);
            var root = rig.Root.Transform;
            var end = root * (kind switch { ThingKind.Tree => rig.Holds(ActorRig.AxeHeadPoint), ThingKind.Stone => rig.Holds(ActorRig.PickPoint), _ => rig.ShovelTip });
            var flat = new Vector2(end.X, end.Z);
            switch (kind) {
                case ThingKind.Tree: {
                        var (seat, radius) = WorldModels.Trunk(thing.Id);
                        float gap = flat.DistanceTo(thing.Pos + new Vector2(seat.X, seat.Z)) - radius;
                        Require(gap is > -.08f and < .04f, $"An axe's head must bite the side of the trunk {offset} away as the chop is heard, not {gap:0.00} m off it.");
                        break;
                    }
                case ThingKind.Stump: {
                        var (seat, radius) = WorldModels.Trunk(thing.OfTree != 0 ? thing.OfTree : thing.Id);
                        float gap = flat.DistanceTo(thing.Pos + new Vector2(seat.X, seat.Z)) - radius;
                        Require(gap is > -.02f and < .12f && end.Y < 0, $"A shovel's blade must go into the ground at the foot of the stump {offset} away, not {gap:0.00} m out from it and {end.Y:0.00} m up.");
                        break;
                    }
                case ThingKind.Stone: {
                        float gap = SurfaceGap(view.PropOf(thing.Id)!.Body, end);
                        Require(gap < .04f, $"A pick's point must land on the face of the rock {offset} away, not {gap:0.00} m off it.");
                        // Mined down to its last stones, the rock shrinks, and the pick follows its face in.
                        // Ten stones, at two a blow, leave the shrunken rock blows enough for its swing to come round.
                        thing.Remaining = 10;
                        soundsBefore = sounds.Played;
                        await Until(() => sounds.Heard(sound, soundsBefore), $"the player mining a shrunken rock {offset} away and its blow heard", 8);
                        end = rig.Root.Transform * rig.Holds(ActorRig.PickPoint);
                        gap = SurfaceGap(view.PropOf(thing.Id)!.Body, end);
                        Require(gap < .04f, $"A pick's point must land on the face of the rock {offset} away once it has shrunk, not {gap:0.00} m off it.");
                        // Its last piece mined, the rock breaks apart and crumbles away.
                        thing.Remaining = 1;
                        await Until(() => view.PropOf(thing.Id) is { Crumbled: >= 0, Body.Visible: true }, $"the rock {offset} away mined of its last piece and crumbling", 8);
                        if (point is Vector2 mined) { RightRelease(mined); point = null; }
                        await Until(() => view.PropOf(thing.Id) is { Body.Visible: false }, $"the crumbled rock {offset} away gone", 2);
                        break;
                    }
            }
            if (point is Vector2 held) RightRelease(held);
            await Frames(2);
            // A rock sheds chips as it is mined, which land round it.
            RemoveThings(Hosted, t => t == thing || t.Kind == ThingKind.Stump && t.Pos == thing.Pos || t.IsLoose && t.Pos.DistanceTo(thing.Pos) < 4);
            Drop(Hosted, "wood", world.Backpack["wood"] - wood); Drop(Hosted, "stone", world.Backpack["stone"] - stone);
        }
        Drop(Hosted, "steel_pick", 1); Drop(Hosted, "steel_shovel", 1);
        Warp(Hosted, start); await CloseWithEsc(); await Frames(4);
    }
}
