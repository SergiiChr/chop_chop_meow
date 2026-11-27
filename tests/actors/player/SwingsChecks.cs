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
        Test(CheckAToolAttackSwingsTheSelectedTool, data);
        Test(CheckMortarAndPestleDoesNotAttack, data);
        Test(CheckNewWeaponsReachTheAimedWolfWithTheirWorkingEnd, data);
        Test(CheckOneSwingHitsEveryWolfInItsPath, data);
        Test(CheckBucketOnlyRingsWhenItHits, data);
        Test(CheckRakeAndBucketComeDownFromOverhead);
        Test(CheckHammerAndChiselSwingsKeepTheirWorkingEndsUp);
        Test(CheckSteppingIntoACabinStopsAnUnlandedSwing, data);
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

    /// <summary>A command swings the carried tool it names, even with a steel axe in the same backpack.</summary>
    private static void CheckAToolAttackSwingsTheSelectedTool(GameData data) {
        var world = AxeNight(data);
        ActorAction shown = ActorAction.Idle;
        world.PlayerActed += (action, _) => shown = action;
        foreach (string tool in new[] { "steel_pick", "steel_shovel", "steel_hammer", "wood_hammer", "stone_chisel",
            "steel_chisel", "rake", "bug_net", Bucket.Item }) {
            Give(world, tool);
            world.Run(world.Me, new Command(Verb.Attack, 0, 0, tool, 0, Vector2.Right));
            Require(ActorAnimation.Tool(shown) == tool, $"Selecting {tool} with an axe carried must swing {tool}, not {shown}.");
            SnapTick(world, 1.3f);
            Drop(world, tool);
        }
    }

    /// <summary>A named attack with a mortar and pestle does not swing or hurt the wolf.</summary>
    private static void CheckMortarAndPestleDoesNotAttack(GameData data) {
        var world = AxeNight(data);
        Give(world, "mortar");
        Require(!world.IsWeapon("mortar"), "The Item tree must not classify the mortar and pestle as a weapon.");
        var wolf = StillWolf(world, Vector2.Right * 1.16f);
        bool swung = false;
        world.PlayerActed += (_, _) => swung = true;
        world.Run(world.Me, new Command(Verb.Attack, 0, 0, "mortar", 0, Vector2.Right));
        SnapTick(world, .8f);
        Require(!swung && wolf.Health == data.WolfHealth,
            "A mortar and pestle must not swing or deal damage, even with an axe in the backpack.");
    }

    /// <summary>Each new working end lands in front, but never on a distant wolf or one behind the player.</summary>
    private static void CheckNewWeaponsReachTheAimedWolfWithTheirWorkingEnd(GameData data) {
        foreach (string tool in new[] { "steel_pick", "steel_shovel", "steel_hammer", "wood_hammer", "stone_chisel",
            "steel_chisel", "rake", "bug_net", Bucket.Item }) {
            var world = AxeNight(data);
            Give(world, tool);
            var target = StillWolf(world, Vector2.Right * 1.16f);
            var behind = StillWolf(world, Vector2.Left * 1.16f);
            var far = StillWolf(world, Vector2.Right * 3);
            world.Attack(Vector2.Right, tool);
            SnapTick(world, .22f);
            Require(target.Health == data.WolfHealth, $"{tool} must not hit while being wound up.");
            SnapTick(world, .58f);
            Require(target.Health < data.WolfHealth, $"{tool} must reach the aimed wolf at biting distance.");
            Require(behind.Health == data.WolfHealth && far.Health == data.WolfHealth,
                $"{tool} must miss wolves behind the player and beyond the working end.");
        }
    }

    /// <summary>Even before receiving network IDs, two wolves in the same swing each take one hit.</summary>
    private static void CheckOneSwingHitsEveryWolfInItsPath(GameData data) {
        var world = AxeNight(data);
        Give(world, Bucket.Item);
        var first = StillWolf(world, new Vector2(1.16f, -.08f));
        var second = StillWolf(world, new Vector2(1.16f, .08f));
        int clangs = 0;
        world.Sound += (name, _) => { if (name == "bucket_hit") clangs++; };
        world.Attack(Vector2.Right, Bucket.Item);
        SnapTick(world, 1.3f);
        Require(first.Health < data.WolfHealth && second.Health < data.WolfHealth && clangs == 2,
            $"Each of the two wolves must take one clang. Got {first.Health}, {second.Health} health and {clangs} clangs.");
    }

    /// <summary>The steel bucket makes its own clang exactly once on a hit, never on a swing that misses.</summary>
    private static void CheckBucketOnlyRingsWhenItHits(GameData data) {
        var world = AxeNight(data);
        Give(world, Bucket.Item);
        var wolf = StillWolf(world, Vector2.Right * 1.16f);
        int clangs = 0;
        world.Sound += (name, _) => { if (name == "bucket_hit") clangs++; };
        world.Attack(Vector2.Left, Bucket.Item);
        SnapTick(world, 1.26f);
        Require(clangs == 0 && wolf.Health == data.WolfHealth, "The bucket must not clang on a miss.");
        world.Attack(Vector2.Right, Bucket.Item);
        SnapTick(world, .55f);
        Require(clangs == 0, "The bucket must not clang before it reaches the wolf.");
        SnapTick(world, .23f);
        Require(clangs == 1 && wolf.Health < data.WolfHealth, "A bucket hit must hurt the wolf and play one clang.");
        SnapTick(world, .55f);
        Require(clangs == 1, "Follow-through must not hit the same wolf again.");
    }

    /// <summary>The rake tines and bucket body rise above the head and come down in front.</summary>
    private static void CheckRakeAndBucketComeDownFromOverhead() {
        var rig = new ActorRig(ActorSkin.Player);
        foreach (var (action, end) in new[] {
            (ActorAction.RakeStrike, new Vector3(0, -1.19f, .01f)), (ActorAction.BucketStrike, new Vector3(0, .12f, 0)) }) {
            var weapon = WeaponSwing.Of(action)!;
            var model = rig.Root.GetNode<Node3D>($"WeaponModels/{action}");
            float peak = float.MinValue;
            for (int i = 0; i < 60 * weapon.Duration; i++) {
                float at = i / 60f;
                rig.Show(ActorAnimation.PoseAt(action, at, false), null, false);
                rig.ShowWeapon(weapon, at);
                var working = model.Transform * end;
                if (at < weapon.ActiveFrom) peak = Math.Max(peak, working.Y);
                if (at < weapon.Contact || at >= weapon.Contact + 1 / 60f) continue;
                Require(peak > 1.75f && working.Y < 1 && working.Z > .35f,
                    $"{action} must bring its end down from above the head into the area in front. Peak {peak:0.00} m, contact {working}.");
            }
        }
        rig.Root.Free();
    }

    /// <summary>A chisel or hammer may tip forward into its strike but its working end never hangs below its fist.</summary>
    private static void CheckHammerAndChiselSwingsKeepTheirWorkingEndsUp() {
        var rig = new ActorRig(ActorSkin.Player);
        foreach (var action in new[] { ActorAction.SteelHammerStrike, ActorAction.MalletStrike,
            ActorAction.StoneChiselStrike, ActorAction.SteelChiselStrike }) {
            var weapon = WeaponSwing.Of(action)!;
            var model = rig.Root.GetNode<Node3D>($"WeaponModels/{action}");
            for (int i = 0; i < 60 * weapon.Duration; i++) {
                float at = i / 60f;
                rig.Show(ActorAnimation.PoseAt(action, at, false), null, false);
                rig.ShowWeapon(weapon, at);
                Require(model.Basis.Y.Y <= .01f, $"The working end of {action} must not hang down at {at:0.00} s.");
            }
        }
        rig.Root.Free();
    }

    private static void CheckSteppingIntoACabinStopsAnUnlandedSwing(GameData data) {
        var world = AxeNight(data);
        var hut = world.Add(ThingKind.Hut, world.Player + Vector2.Up * 4);
        world.Player = Doorways.DoorPoint(hut);
        var wolf = StillWolf(world, Vector2.Right * 1.5f);
        world.Attack(Vector2.Right);
        SnapTick(world, .18f);
        Require(world.Doorways.Enter(hut, false), "the player should start stepping in before the axe comes down");
        SnapTick(world, .29f);
        Require(world.Door == Doorway.Entering && wolf.Health == data.WolfHealth,
            "the cut-off swing should not hurt a wolf when the player is already stepping into the cabin");
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

    /// <summary>A further blow throws a wolf half a metre back and leaves it reeling for 0.45 seconds.</summary>
    private static void CheckABlowThrowsAWolfBackAndLeavesItReeling(GameData data) {
        var world = AxeNight(data);
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(1.05f, 0), Attack = .2f, Aggressive = true });
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
        SnapTick(world, .55f);
        Require(wolf.Pos.DistanceTo(world.Player) > burnt + .3f,
            "a burnt wolf should run as soon as it has reeled from the burn");
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
            (ActorAction.TorchSwing, .2f, .45f, Swings.Torch), (ActorAction.Attack, .18f, .32f, Swings.Punch), (ActorAction.NetSwing, ActorAnimation.NetReaches, ActorAnimation.NetPasses, Swings.Net) }) {
            float lands = ActorAnimation.Contact(action) / ActorAnimation.Duration(action), far = 0, left = -180, right = -180, low = float.MaxValue;
            Require(lands >= windup && lands <= end, $"{action} should land during its stroke, {windup} to {end} through, not {lands:0.00} through");
            for (float p = windup; p <= end + .001f; p += .01f) {
                rig.Show(ActorAnimation.PoseAt(action, p * ActorAnimation.Duration(action), false), ActorAnimation.Tool(action), false, lit: action == ActorAction.TorchSwing);
                var at = action switch { ActorAction.TorchSwing => ModelSteps.InRig(rig, rig.Flame).Origin, ActorAction.Attack => rig.Fist(false), ActorAction.NetSwing => rig.Holds(ActorRig.NetMouth), _ => rig.AxeHead };
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
        // A net's hoop is 0.17 m to its rim, and it catches at any height: it has to be at a firebug's as it lands.
        rig.Show(ActorAnimation.PoseAt(ActorAction.NetSwing, ActorAnimation.Contact(ActorAction.NetSwing), false), ActorAnimation.Tool(ActorAction.NetSwing), false);
        float hoop = rig.Holds(ActorRig.NetMouth).Y;
        Require(MathF.Abs(hoop - FirebugShape.Height) < .17f, $"a net's hoop should be at the height a firebug flies at, {FirebugShape.Height} m, as it lands, not at {hoop:0.00} m");
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
