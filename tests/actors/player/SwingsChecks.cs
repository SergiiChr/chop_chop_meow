using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static PlayerSteps;
using static SetupSteps;
using static WolfSteps;
using static WorldSteps;

/// <summary>Checks for a player's swings at wolves: when a swing lands, what it reaches and how hard it hits.</summary>
public static class SwingsChecks {
    public static void Run(GameData data) {
        Test(CheckAnAxeLandsWhenItComesDownNotWhenItIsSwung, data);
        Test(CheckASwingHitsOnlyWhereItIsAimedAndAsFarAsTheBladeReaches, data);
        Test(CheckAxeSwingsTakeTurnsAndTheSweepCatchesOnlyAWolfInTheAir, data);
        Test(CheckAWolfStandsTwoBlowsOfASteelAxeAndFallsToTheThirdOrFourth, data);
        Test(CheckATorchBurnsOnlyAWolfItsFlameReaches, data);
        Test(CheckAPunchReachesAWolfAtBitingDistance, data);
        Test(CheckABlowThrowsAWolfBackAndLeavesItReeling, data);
        Test(CheckABurntWolfRunsFromThePlayerForAMoment, data);
        Test(CheckSwingShapesFollowThePlayersModel, data);
        Test(CheckTorchSwingScaresOnlyWolvesItIsAimedAt, data);
    }

    /// <summary>The blow counts when the animation brings the axe down, half way through the swing, so a press alone hurts nothing.</summary>
    private static void CheckAnAxeLandsWhenItComesDownNotWhenItIsSwung(GameData data) {
        var world = AxeNight(data);
        var wolf = StillWolf(world, new Vector2(1.5f, 0));
        world.Attack(Vector2.Right);
        Require(wolf.Health == data.WolfHealth && wolf.Stagger == 0, "a swing should not hurt a wolf the moment it starts");
        world.Tick(.3f);
        Require(wolf.Health == data.WolfHealth, "a steel axe should still be on its way down 0.3 seconds into the swing");
        world.Tick(.15f);
        float taken = data.WolfHealth - wolf.Health;
        Require(taken is >= 2.2f and <= 3.2f, $"a steel axe should take 2.2 to 3.2 health as it comes down 0.4 seconds in, not {taken}");
        Require(wolf.Stagger > 0, "a wolf an axe lands on should be knocked back and reel");
    }

    private static void CheckASwingHitsOnlyWhereItIsAimedAndAsFarAsTheBladeReaches(GameData data) {
        Require(TakenByAnAxe(data, new Vector2(1.5f, 0), Vector2.Left) == 0, "a swing aimed away from a wolf should miss it");
        Require(TakenByAnAxe(data, new Vector2(1.5f, 0), Vector2.Up) == 0, "an overhead swing aimed past a wolf's side should miss it");
        // A wolf facing the player reaches 0.76 m toward them with its nose, and the axe head comes down 1.35 m out.
        Require(TakenByAnAxe(data, new Vector2(2, 0), Vector2.Right) > 0, "an axe should reach the nose of a wolf standing 2 m off");
        Require(TakenByAnAxe(data, new Vector2(2.4f, 0), Vector2.Right) == 0, "an axe should fall short of a wolf standing 2.4 m off");
    }

    /// <summary>
    /// The overhead chop comes down on one line. Every second swing sweeps from behind the player's left shoulder round the
    /// front to their right at chest height: it passes over a wolf on the ground and lands on one in the air.
    /// </summary>
    private static void CheckAxeSwingsTakeTurnsAndTheSweepCatchesOnlyAWolfInTheAir(GameData data) {
        var world = AxeNight(data);
        // Aiming east, the player's left is north, which is up the map.
        var ahead = StillWolf(world, new Vector2(1.5f, 0));
        var left = StillWolf(world, new Vector2(1.5f, 0).Rotated(Mathf.DegToRad(-60)));
        var swung = new List<ActorAction>();
        world.PlayerActed += (action, _) => swung.Add(action);
        world.Attack(Vector2.Right);
        world.Tick(.7f);
        Require(ahead.Health < data.WolfHealth && left.Health == data.WolfHealth, "the overhead chop should land only on the wolf straight ahead");
        float after = ahead.Health;
        world.Attack(Vector2.Right);
        world.Tick(.2f);
        // A wolf leaping from the left: half way through its leap as the sweep crosses the front, 0.4 seconds into the swing.
        var leaper = AirborneWolf(world, -60);
        var behind = AirborneWolf(world, 120);
        SnapTick(world, .25f);
        Require(swung.SequenceEqual(new[] { ActorAction.SteelAxeSwing, ActorAction.SteelAxeSweep }), $"axe swings should take turns, overhead then sideways: {string.Join(", ", swung)}");
        Require(ahead.Health == after && left.Health == data.WolfHealth, "the sideways sweep, at chest height, should pass over wolves on the ground");
        Require(leaper.Health < data.WolfHealth, "the sideways sweep should catch a wolf leaping in from the left");
        Require(behind.Health == data.WolfHealth, "the sideways sweep, which ends to the player's right, should miss a wolf leaping in from behind that");
    }

    private static void CheckAWolfStandsTwoBlowsOfASteelAxeAndFallsToTheThirdOrFourth(GameData data) {
        var world = AxeNight(data);
        var blows = new HashSet<int>();
        for (int n = 0; n < 12; n++) {
            var wolf = StillWolf(world, new Vector2(1.2f, 0));
            int count = 0;
            for (int swings = 0; world.Wolves.Contains(wolf) && swings < 12; swings++) {
                // A blow knocks the wolf back; each one is met from the same distance.
                wolf.Pos = world.Player + new Vector2(1.2f, 0);
                float health = wolf.Health;
                world.Attack(Vector2.Right); world.Tick(.7f);
                // Every second swing is the sweep, which passes over it.
                if (!world.Wolves.Contains(wolf) || wolf.Health < health) count++;
            }
            blows.Add(count);
        }
        Require(blows.All(count => count is 3 or 4), $"a wolf should stand two blows of a steel axe and fall to the third or the fourth, not to blow {string.Join(" or ", blows)}");
    }

    /// <summary>A torch is swung bent over, low enough for a wolf: its flame reaches 1 m, so it burns a wolf at biting distance and only scares one further off.</summary>
    private static void CheckATorchBurnsOnlyAWolfItsFlameReaches(GameData data) {
        var far = TorchNight(data);
        var off = StillWolf(far, new Vector2(2, 0));
        far.UseTorch(Vector2.Right);
        far.Tick(.4f);
        Require(off.Health == data.WolfHealth && off.Following, "a torch swung at a wolf 2 m off should scare it without burning it");

        var near = TorchNight(data);
        var close = StillWolf(near, new Vector2(1.05f, 0));
        near.UseTorch(Vector2.Right);
        Require(close.Health == data.WolfHealth, "a torch should not burn a wolf the moment it is swung");
        near.Tick(.3f);
        float taken = data.WolfHealth - close.Health;
        Require(taken is >= .8f and <= 1.2f, $"a torch crossing a wolf's muzzle should take 0.8 to 1.2 health, not {taken}");
        Require(close.Stagger > 0 && close.Flee > 0, "a burnt wolf should be knocked back and run for a moment");
    }

    /// <summary>A blow throws a wolf half a metre back from the player, and it stands reeling there for 0.45 seconds.</summary>
    private static void CheckABlowThrowsAWolfBackAndLeavesItReeling(GameData data) {
        var world = AxeNight(data);
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(1.05f, 0), Attack = .2f });
        world.Attack(Vector2.Right);
        // The axe comes down while the wolf is planted in its windup.
        SnapTick(world, .38f);
        float before = wolf.Pos.DistanceTo(world.Player);
        SnapTick(world, .04f);
        float thrown = wolf.Pos.DistanceTo(world.Player) - before;
        Require(wolf.Health < data.WolfHealth && thrown is > .45f and < .55f, $"a blow should throw a wolf half a metre back, not {thrown:0.00} m");
        var landed = wolf.Pos;
        SnapTick(world, .3f);
        Require(wolf.Pos == landed, "a wolf reeling from a blow should stand where it was thrown");
        SnapTick(world, .5f);
        Require(wolf.Pos != landed, "a wolf should move again once it has reeled for 0.45 seconds");
    }

    /// <summary>A wolf a torch burns runs from the player for a moment, even one that turned on them and that no wave scares.</summary>
    private static void CheckABurntWolfRunsFromThePlayerForAMoment(GameData data) {
        var world = TorchNight(data);
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(1.05f, 0), Follow = FollowStage.Bold });
        world.UseTorch(Vector2.Right);
        SnapTick(world, .2f);
        float burnt = wolf.Pos.DistanceTo(world.Player);
        Require(wolf.Health < data.WolfHealth && wolf.Follow == FollowStage.Bold, "a torch should burn a wolf that turned on the player without scaring it");
        SnapTick(world, 1.5f);
        Require(wolf.Pos.DistanceTo(world.Player) > burnt + 1, $"a burnt wolf should run from the player, not stay {wolf.Pos.DistanceTo(world.Player):0.0} m off");
        Require(world.Health == 100, "a torch that burns a wolf as it gathers itself should stop its bite");
    }

    private static void CheckAPunchReachesAWolfAtBitingDistance(GameData data) {
        var world = NightWorld(data);
        var close = StillWolf(world, new Vector2(1.05f, 0));
        var off = StillWolf(world, new Vector2(0, 2));
        world.Attack(Vector2.Right);
        world.Tick(.4f);
        float taken = data.WolfHealth - close.Health;
        Require(taken is >= .4f and <= .6f, $"a punch should take 0.4 to 0.6 health off a wolf at biting distance, not {taken}");
        world.Tick(.3f);
        world.Attack(Vector2.Down);
        world.Tick(.4f);
        Require(off.Health == data.WolfHealth, "a punch should fall short of a wolf 2 m off");
    }

    /// <summary>
    /// Each swing's shape on the ground is where the player's model carries the axe head, the flame or the fist through its
    /// stroke, from the top of the wind-up to its end: every point of that path lies in the shape, and the shape reaches no
    /// further than the path does, nor lower. The overhead chop counts from where the axe head has come down to head height.
    /// </summary>
    private static void CheckSwingShapesFollowThePlayersModel(GameData data) {
        var rig = new ActorRig(ActorSkin.Player);
        foreach (var (action, windup, end, shape) in new[] {
            (ActorAction.SteelAxeSwing, .46f, .52f, Swings.Chop), (ActorAction.SteelAxeSweep, .34f, .6f, Swings.Sweep),
            (ActorAction.TorchSwing, .2f, .45f, Swings.Torch), (ActorAction.Attack, .18f, .32f, Swings.Punch) }) {
            float lands = ActorAnimation.Contact(action) / ActorAnimation.Duration(action), far = 0, left = -180, right = -180, low = float.MaxValue;
            Require(lands >= windup && lands <= end, $"{action} should land during its stroke, {windup} to {end} through, not {lands:0.00} through");
            for (float p = windup; p <= end + .001f; p += .01f) {
                rig.Show(ActorAnimation.PoseAt(action, p * ActorAnimation.Duration(action), false), ActorAnimation.Tool(action), false, lit: action == ActorAction.TorchSwing);
                var at = action switch { ActorAction.TorchSwing => ModelSteps.InRig(rig, rig.Flame).Origin, ActorAction.Attack => rig.Fist(false), _ => rig.AxeHead };
                // The rig faces +Z with its left hand on +X.
                float distance = new Vector2(at.X, at.Z).Length(), angle = Mathf.RadToDeg(MathF.Atan2(at.X, at.Z));
                Require(distance <= shape.Far + .01f && angle <= shape.Left + 1 && angle >= -shape.Right - 1,
                    $"{action} carries its end to {distance:0.00} m at {angle:0} degrees left, {p:0.00} through, outside its shape {shape}");
                far = Math.Max(far, distance); left = Math.Max(left, angle); right = Math.Max(right, -angle); low = Math.Min(low, at.Y);
            }
            // An axe head is 0.17 m tall, so its edge comes that much lower than its middle at most, and a flame's foot is below its middle; a fist counts at any height.
            Require(shape.Low == 0 || shape.Low <= low && low - shape.Low < .17f, $"{action} should come no lower than its model does, {low:0.00} m at the middle of its head, not {shape.Low} m");
            Require(shape.Far - far < .25f, $"{action} should reach no more than a head's width past the {far:0.00} m its model does, not to {shape.Far} m");
            Require(shape.Left - left < 8 && shape.Right - right < 8, $"{action} should sweep from {left:0} degrees left to {right:0} right as its model does, not {shape.Left} to {shape.Right}");
        }
        rig.Root.Free();
    }

    private static void CheckTorchSwingScaresOnlyWolvesItIsAimedAt(GameData data) {
        var creeping = TorchNight(data);
        var lone = new Wolf { Pos = creeping.Player + new Vector2(8, 0), Health = 4 };
        PlaceWolf(creeping, lone);
        WolfTick(creeping, 3, lone);
        Require(lone.Pos.DistanceTo(creeping.Player) < 6.5f && lone.Pos.DistanceTo(creeping.Player) > 3.5f, "a lone wolf should creep in slowly even with a torch");

        var scared = TorchNight(data);
        var wolf = new Wolf { Pos = scared.Player + new Vector2(6, 0), Health = 4 };
        var behind = new Wolf { Pos = scared.Player + new Vector2(-6, 0), Health = 4 };
        PlaceWolf(scared, wolf); PlaceWolf(scared, behind);
        scared.UseTorch(Vector2.Right);
        Require(wolf.Following && !behind.Following, "a swing should scare only the wolves it is aimed at");
        RemoveWolves(scared, w => w == behind);
        WolfTick(scared, 8, wolf);
        Require(scared.Health == 100 && Math.Abs(wolf.Pos.DistanceTo(scared.Player) - World.StalkDistance) <= WolfFollow.Wander + .1f, "a scared wolf should follow at the edge of the light without biting");
    }
}
