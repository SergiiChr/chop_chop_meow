using System;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WolfSteps;
using static WorldSteps;

/// <summary>Checks for a wolf's bite: the windup, leap, bite, and the player's chance to get out of the way.</summary>
public static class WolfSnapChecks {
    public static void Run(GameData data) {
        Test(CheckAWolfGathersItselfBeforeItBites, data);
        Test(CheckAWalkingPlayerCanEvadeAndCounter, data);
        Test(CheckAWoundedPlayerCanEvadeAThroatLeap, data);
        Test(CheckALeapStopsAtABuilding, data);
        Test(CheckSteppingIntoTheDoorwayCanEvadeABite, data);
        Test(CheckABlowThatLandsFirstStopsTheBite, data);
        Test(CheckAFirstHitMakesTheWolfLandFacingItsAttacker, data);
        Test(CheckAStruckWolfRunsAnArcAfterMissingItsBite, data);
    }

    private static void CheckAWolfGathersItselfBeforeItBites(GameData data) {
        var world = NightWorld(data);
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(1.05f, 0) });
        var heard = Listen(world);
        SnapTick(world, .25f);
        Require(world.Health == 100 && WolfSnap.Winding(wolf), "a wolf at biting distance should gather itself for 0.3 seconds before it bites");
        SnapTick(world, .35f);
        Require(world.Health == 100 - data.WolfBite && heard.Contains("wolf_bite"), $"a wolf should bite a player who stands still as it lands, about 0.55 seconds in: health {world.Health}");
        SnapTick(world, .5f);
        Require(world.Health == 100 - data.WolfBite, "a wolf should wait 0.6 seconds after a snap before it starts the next");
        SnapTick(world, .7f);
        Require(world.Health == 100 - 2 * data.WolfBite, "a wolf should bite again about 1.15 seconds after its last bite");
    }

    private static void CheckAWalkingPlayerCanEvadeAndCounter(GameData data) {
        var world = AxeNight(data);
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(1.3f, 0) });
        var heard = Listen(world);
        SnapTick(world, .06f);
        Require(WolfSnap.Winding(wolf), "a wolf should telegraph its snap as it comes into range");
        var planted = wolf.Pos;
        float aim = wolf.Heading;
        WalkThroughSnap(world, Vector2.Up, .24f);
        Require(wolf.Pos == planted && wolf.Heading == aim, "a wolf should plant its paws and lock its aim through the windup");
        SnapTick(world, .28f);
        Require(world.Health == 100 && !heard.Contains("wolf_bite"), "walking sideways through the snap should evade its jaws");
        Require(wolf.Pos.X < planted.X - .12f && MathF.Abs(wolf.Pos.Y - planted.Y) < .01f,
            "the snap should leap forward along its committed line");

        var landing = wolf.Pos;
        SnapTick(world, .1f);
        Require(wolf.Pos == landing && wolf.Heading == aim, "after missing the wolf should stay planted where it landed");
        world.Attack(wolf.Pos - world.Player);
        SnapTick(world, .42f);
        Require(wolf.Health < data.WolfHealth && world.Health == 100, "a player can counter an exposed wolf after evading");
    }

    private static void CheckAWoundedPlayerCanEvadeAThroatLeap(GameData data) {
        var world = NightWorld(data);
        SetNeeds(world, health: 49);
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(3, 0) });
        SnapTick(world, .06f);
        Require(wolf.Leap > 0, "a nearby wolf should commit to a throat leap at half health");
        WalkThroughSnap(world, Vector2.Up, .74f);
        Require(!world.Mauled && world.Health == 49, "a throat leap that misses should leave the wounded player standing");
        var after = world.Player;
        WalkThroughSnap(world, Vector2.Right, .1f);
        Require(world.Player.X > after.X + .3f, "an avoided takedown should leave the player in control");
    }

    private static void CheckALeapStopsAtABuilding(GameData data) {
        var world = NightWorld(data);
        SetNeeds(world, health: 49);
        var wall = world.Add(ThingKind.Chest, world.Player + new Vector2(1.3f, 0));
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(3, 0) });
        SnapTick(world, .78f);
        Require(world.Health == 49 && !world.Mauled && wolf.Pos.X > wall.X + .4f && world.CanStand(wolf.Pos),
            "a throat leap should stop on its side of a solid object and miss the player behind it");
    }

    private static void CheckSteppingIntoTheDoorwayCanEvadeABite(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(1.3f, 0) });
        SnapTick(world, .06f);
        Require(world.Doorways.Enter(hut, false), "the player should start entering during the wolf's windup");
        SnapTick(world, .54f);
        Require(world.Door == Doorway.Entering && world.Health == 100 && wolf.Recover > 0,
            "the player should step beyond the jaws before the bite lands on the empty doorstep");
    }

    /// <summary>A blow knocks a wolf back and leaves it reeling, so one timed to land while the wolf gathers itself costs it the bite.</summary>
    private static void CheckABlowThatLandsFirstStopsTheBite(GameData data) {
        var world = AxeNight(data);
        // The wolf starts its snap 0.15 seconds in, and the axe swung now comes down 0.4 seconds in, before the wolf leaps.
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(1.05f, 0), Attack = .15f });
        world.Attack(Vector2.Right);
        SnapTick(world, .3f);
        Require(WolfSnap.Winding(wolf), "the wolf should be gathering itself as the axe comes down");
        SnapTick(world, .5f);
        Require(world.Health == 100 && wolf.Health < data.WolfHealth, $"a blow that lands while a wolf gathers itself should stop its bite: health {world.Health}");
    }

    private static void CheckAFirstHitMakesTheWolfLandFacingItsAttacker(GameData data) {
        var world = AxeNight(data);
        var wolf = StillWolf(world, Vector2.Right * 1.25f);
        wolf.Heading = 0;
        var before = wolf.Pos;
        world.Attack(Vector2.Right);
        SnapTick(world, .44f);
        Require(wolf.Health < data.WolfHealth && wolf.Pos.X > before.X + .4f, "the first hit should throw the wolf back");
        var landing = wolf.Pos;
        SnapTick(world, .52f);
        Require(wolf.Pos == landing && Vector2.FromAngle(wolf.Heading).Dot(Vector2.Left) > .99f,
            "the wolf should turn to face the attacker and stand its ground after the hop");
        SnapTick(world, .6f);
        Require(wolf.Pos.X < landing.X - .3f && wolf.Chase > 0, "the wolf should run in after standing its ground");
    }

    private static void CheckAStruckWolfRunsAnArcAfterMissingItsBite(GameData data) {
        var world = NightWorld(data);
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + Vector2.Right * 1.2f });
        world.Strike(wolf, 1);
        for (int i = 0; i < 75 && !WolfSnap.Winding(wolf); i++) SnapTick(world, .02f);
        Require(WolfSnap.Winding(wolf), "the struck wolf should come in for another bite");
        WalkThroughSnap(world, Vector2.Up, .55f);
        Require(world.Health == 100 && wolf.Recover > 0, "the aggravated wolf should still miss a player who sidesteps the committed bite");
        var landing = wolf.Pos;
        var landed = landing - world.Player;
        SnapTick(world, .28f);
        Require(wolf.Pos == landing, "the missed bite should still leave the wolf planted for a moment");
        SnapTick(world, .5f);
        var rounded = wolf.Pos - world.Player;
        Require(rounded.Length() > landed.Length() + .45f && MathF.Abs(landed.AngleTo(rounded)) > .25f && wolf.Chase > 0,
            "the wolf should run outward and around the player before closing in again");
        for (int i = 0; i < 100 && !WolfSnap.Winding(wolf); i++) SnapTick(world, .02f);
        Require(WolfSnap.Winding(wolf), "the run around the player should lead to another committed bite");
    }
}
