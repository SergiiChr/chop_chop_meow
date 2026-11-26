using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static ModelSteps;
using static PlayerSteps;
using static CabinSteps;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The UI checks of a player's swings: a punch with Space and a torch with right-click.</summary>
    private async Task RunSwingsUIChecks() {
        await TestClean(CheckSpaceWithoutAnAxeThrowsAPunchTheWayThePlayerLastWalked);
        await TestClean(CheckSpaceSwingsTheSelectedBucket);
        await TestClean(CheckSelectedChiselsAndHammersAreCarriedUpright);
        await TestClean(CheckRightClickLightsAndSwingsATorch);
    }

    /// <summary>
    /// Space without an axe throws a punch the way the player last walked, which whooshes and then thuds on a wolf
    /// there.
    /// </summary>
    private async Task CheckSpaceWithoutAnAxeThrowsAPunchTheWayThePlayerLastWalked() {
        var home = Hosted.Player;
        await CloseWithEsc();
        Drop(Hosted, "steel_axe");
        // By day wolves are calmed and sent home at once, which would clear the blow before the window shows it.
        float health = world.Health; RunUntilNight(Hosted);
        // Space swings the way the player last walked: a step to the right on screen aims the punch at a wolf there.
        await Holding(Key.D, () => Seconds(.1));
        await Frames();
        var wolf = PlaceWolf(Hosted, new Wolf { Pos = Hosted.Player + PlayerControl.KeyDirection(1, 0) * 1.2f, Health = 5 });
        int soundsBefore = sounds.Played;
        await KeyStroke(Key.Space);
        Require(view.PlayerAnimation == ActorAction.Attack, "Space without an axe must trigger the dedicated unarmed attack.");
        Require(sounds.Heard("tool_air_swing", soundsBefore) && !sounds.Heard("wolf_hit", soundsBefore), "A punch must whoosh before it lands.");
        await Until(() => sounds.Heard("wolf_hit", soundsBefore), "the punch thudding as it lands on the wolf", 2);

        var aim = PlayerControl.KeyDirection(1, 0);
        await Holding(Key.D, () => Seconds(.07));
        var turn = world.Player;
        await Holding(Key.A, () => Seconds(.07));
        Require((world.Player - turn).Dot(aim) < -.05f, "the player can immediately reverse direction during a fight");
        var facing = view.PlayerRig.Root.Basis.Z;
        Require(view.PlayerAnimation == ActorAction.Attack && new Vector2(facing.X, facing.Z).Dot(aim) > .99f,
            "the punch should keep facing its original aim while retreating");

        RemoveWolves(Hosted, w => w == wolf); RunUntilMorning(Hosted); SetNeeds(Hosted, health: health);
        await Until(() => view.PlayerAnimation != ActorAction.Attack, "the unarmed attack finishing", 3);
        Warp(Hosted, home); await Frames(4);
    }

    /// <summary>Space uses a bucket selected on the hotbar instead of an axe also carried in the backpack.</summary>
    private async Task CheckSpaceSwingsTheSelectedBucket() {
        int active = world.ActiveHotbar;
        string previous = world.HotbarItem(8);
        Give(Hosted, Bucket.Item); Give(Hosted, "steel_axe");
        SetHotbarSlot(Hosted, 8, Bucket.Item);
        await KeyStroke(Key.Key9);
        await Until(() => world.HotbarItem(8) == Bucket.Item && world.ActiveHotbar == 8 && world.Carried(Bucket.Item) > 0,
            "the bucket selected on the hotbar", 2);
        await KeyStroke(Key.Space);
        await Until(() => view.PlayerAnimation == ActorAction.BucketStrike && view.PlayerRig.ToolShown == Bucket.Item,
            "the player swinging the selected bucket, not their axe", 2);
        await Until(() => view.PlayerAnimation != ActorAction.BucketStrike, "the bucket swing finishing", 3);
        SetHotbarSlot(Hosted, 8, previous);
        Drop(Hosted, Bucket.Item, 1); Drop(Hosted, "steel_axe", 1);
        // Restore the hotbar highlight without arming the previous slot's building plan.
        world.SelectHotbar(active); await Frames();
    }

    /// <summary>The selected chisel or hammer stays visible and points up even before any attack.</summary>
    private async Task CheckSelectedChiselsAndHammersAreCarriedUpright() {
        int active = world.ActiveHotbar;
        string previous = world.HotbarItem(8);
        foreach (var (item, action) in new[] { ("stone_chisel", ActorAction.StoneChiselStrike),
            ("steel_chisel", ActorAction.SteelChiselStrike), ("wood_hammer", ActorAction.MalletStrike),
            ("steel_hammer", ActorAction.SteelHammerStrike) }) {
            Give(Hosted, item);
            SetHotbarSlot(Hosted, 8, item);
            await Until(() => world.HotbarItem(8) == item && world.Carried(item) > 0, $"the carried {item} in slot nine", 2);
            await KeyStroke(Key.Key9);
            await Until(() => view.PlayerRig.ToolShown == item, $"the selected {item} in hand at rest", 2);
            var tool = view.PlayerRig.Root.GetNode<Node3D>($"WeaponModels/{action}");
            Require(tool.Visible && tool.GlobalBasis.Y.Y < -.85f,
                $"The selected {item} must point up at rest without swinging.");
            Drop(Hosted, item, 1);
        }
        SetHotbarSlot(Hosted, 8, previous);
        // Restore the selection without arming the previous slot's building plan.
        world.SelectHotbar(active); await Frames();
    }

    /// <summary>A torch in the backpack is lit by a right-click after the kindling animation, and swung by the next right-click, standing still or walking.</summary>
    private async Task CheckRightClickLightsAndSwingsATorch() {
        var home = Hosted.Player;
        await CloseWithEsc(); Give(Hosted, "torch", 1); SetHotbarSlot(Hosted, 8, "torch");
        await KeyStroke(Key.Key9);
        Require(world.Torch == 0, "Selecting a torch must not light it.");
        await ClickGround(MouseButton.Right);
        Require(world.TorchKindling > 0, "The first right-click must start striking flint and steel.");
        Require(world.Backpack["torch"] == 1, "The first right-click must leave the torch it lights in the backpack.");
        Require(view.PlayerAnimation == ActorAction.Kindle, "The first right-click must play the kindling animation.");
        await Until(() => world.TorchLit, "the torch catching after the lighting animation", World.KindleSeconds + 2);
        await ClickGround(MouseButton.Right);
        Require(world.TorchFlare > 0 && view.PlayerAnimation == ActorAction.TorchSwing, "Right-clicking with a lit torch must swing it.");
        // A swing made on the move plays out in full instead of giving way to the walk.
        await Until(() => view.PlayerAnimation != ActorAction.TorchSwing, "the end of a torch swing made standing still", 2);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
        await Seconds(.15);
        var from = world.Player;
        await ClickGround(MouseButton.Right); await Seconds(.25);
        bool swungWalking = view.PlayerAnimation == ActorAction.TorchSwing && world.Player.DistanceTo(from) > .3f;
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
        Require(swungWalking, "A torch swing made while walking must keep playing as the player walks on.");
        Warp(Hosted, home); await Frames(4);
    }
}
