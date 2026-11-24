using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static CabinSteps;
using static SetupSteps;
using static WorldSteps;
using static EconomySteps;
using static PlayerSteps;
using static WorkerSteps;

/// <summary>
/// Checks of wolves at night: their dens, patrols and howls, whom they go for and leave alone, and what a bite or a
/// leap does.
/// </summary>
public static class WolfChecks {
    public static void Run(GameData data) {
        Test(CheckWolvesHurtUnprotectedPlayerAtNightAndAvoidFire, data);
        Test(CheckWolvesHuntAndCarcassesYieldMeat, data);
        Test(CheckWolvesLeaveHealthyOrFirelitPlayersAlone, data);
        Test(CheckWolfLeapPinsAndKillsAPlayerOnHalfHealth, data);
        Test(CheckWolfThatBitesAPlayerToDeathEatsTheirFood, data);
        Test(CheckAWolfThatFindsAPlayerInSightGoesForThemWithoutAHowl, data);
        Test(CheckSprintOutrunsWolf, data);
        Test(CheckDensLetWolvesOutAtDusk, data);
        Test(CheckARandomDenLetsOneMoreWolfOutEveryHour, data);
        Test(CheckWolvesAreNoStrongerOnLaterNights, data);
        Test(CheckPatrollingWolfWalksAtHalfItsTrot, data);
        Test(CheckWolvesKeepToGroundAPlayerCanWalk, data);
        Test(CheckSmelledPlayerDrawsAHowlAndThePack, data);
        Test(CheckWolvesGoHomeBeforeDawn, data);
        Test(CheckDensStayOpen, data);
        Test(CheckWolvesLetGoCleanly, data);
        Test(CheckFullPackIgnoresASwing, data);
        Test(CheckEachPlayerHasTheirOwnPack, data);
    }

    private static void CheckWolvesHurtUnprotectedPlayerAtNightAndAvoidFire(GameData data) {
        var dark = EmptyWorld(data);
        dark.Player = new Vector2(10, 10);
        RunUntilNight(dark);
        SetNeeds(dark, health: 100);
        PlaceWolf(dark, new Wolf { Pos = dark.Player + new Vector2(.8f, 0) });
        WolfSteps.SnapTick(dark, .9f);
        Require(dark.Health < 100, "an unprotected player near a wolf should take damage at night");

        var lit = EmptyWorld(data);
        lit.Player = new Vector2(10, 10);
        RunUntilNight(lit);
        SetNeeds(lit, health: 100);
        var fire = lit.Add(ThingKind.Campfire, lit.Player + new Vector2(3, 0));
        fire.Burn = 90;
        var wolf = new Wolf { Pos = lit.Player + new Vector2(1.3f, 0) };
        PlaceWolf(lit, wolf);
        SafeTick(lit, .2f);
        Require(lit.Health == 100 && wolf.Pos.DistanceTo(lit.Player) > 1.3f,
                "active campfire light should repel a nearby wolf without harming the player");
        RunUntilPhase(lit, .2f);
        SafeTick(lit, .1f);
        Require(lit.Wolves.Count == 0,
                "the day cycle should clear wolves when daylight returns");

        foreach (var cause in new[] { "hunger", "thirst" }) {
            var world = EmptyWorld(data);
            world.Player = new Vector2(12, 13);
            SetNeeds(world, health: 1);
            if (cause == "hunger") SetNeeds(world, hunger: 0); else SetNeeds(world, thirst: 0);
            Give(world, "berries", 2);
            Give(world, "water", 1);
            world.Tick(1);
            var recovery = world.Things.LastOrDefault(t => t.Kind == ThingKind.Grave);
            Require(recovery != null, $"{cause} death should leave a recovery box");
            Require(recovery.Pos == new Vector2(12, 13), $"{cause} death should leave the recovery box at the collapse location");
            Require(recovery.Inventory["berries"] == 2, $"{cause} death should recover the player's carried berries");
            Require(recovery.Inventory["water"] == 1, $"{cause} death should recover the player's carried water");
            SafeTick(world, data.RespawnSeconds + .1f);
            Require(world.Player == Terrain.Spawn && world.Health == 100,
                    $"{cause} death should respawn the player in a healthy state");
        }
    }

    private static void CheckWolvesHuntAndCarcassesYieldMeat(GameData data) {
        var chase = NightWorld(data);
        var runner = new Wolf { Pos = chase.Player + new Vector2(8, 0) };
        PlaceWolf(chase, runner);
        SafeTick(chase, 1);
        Require(runner.Pos.DistanceTo(chase.Player) < 5, "a wolf should close at least three tiles per second");

        var hunt = NightWorld(data);
        Give(hunt, "steel_axe", 1);
        Give(hunt, "wood", 1);
        var prey = new Wolf { Pos = hunt.Player + new Vector2(1.5f, 0), Health = 1 };
        PlaceWolf(hunt, prey);
        ActorAction swung = ActorAction.Idle;
        hunt.PlayerActed += (action, _) => swung = action;
        hunt.Attack(Vector2.Right);
        hunt.Tick(.45f);
        Require(swung == ActorAction.SteelAxeSwing, "a swing with a steel axe carried should be the overhead axe swing, not the felling stroke");
        var carcass = hunt.Things.SingleOrDefault(t => t.Kind == ThingKind.WolfCorpse);
        Require(!hunt.Wolves.Contains(prey), "a killed wolf should not stay among the wolves");
        Require(carcass != null, "a killed wolf should leave a carcass");
        Require(carcass.Remaining == 4, "the carcass should hold the wolf's meat");
        RunUntilPhase(hunt, .2f);
        SafeTick(hunt, .5f);
        Require(hunt.Things.Contains(carcass!) && hunt.Wolves.Count == 0, "a carcass should outlast the night");

        hunt.Player = carcass!.Pos + new Vector2(1, 0);
        while (hunt.GatherBlock(carcass) == null) hunt.Gather(carcass, data.GatherSeconds);
        Require(hunt.Backpack["raw_meat"] == 4, "butchering a carcass should yield all of its raw meat");
        SafeTick(hunt, .1f);
        Require(!hunt.Things.Contains(carcass), "a fully butchered carcass should disappear");

        SetNeeds(hunt, hunger: 40);
        hunt.Consume("raw_meat");
        Require(hunt.Backpack["raw_meat"] == 4 && hunt.Hunger == 40, "raw meat must be cooked before it can be eaten");
        var furnace = hunt.Add(ThingKind.Furnace, hunt.Player + new Vector2(1, 1));
        hunt.SetRecipe(furnace, "burnt_steak");
        hunt.LoadBatch(furnace);
        for (int i = 0; i < 28; i++) hunt.Tick(Step);
        hunt.Collect(furnace);
        Require(hunt.Backpack["burnt_steak"] == 2, "the stone furnace should cook two steaks");
        Require(hunt.Backpack["raw_meat"] == 2, "the stone furnace should use two raw meat");
        Require(hunt.Backpack["wood"] == 0, "the stone furnace should use one fuel for two raw meat");
        SetNeeds(hunt, hunger: 40);
        hunt.Consume("burnt_steak");
        Require(hunt.Backpack["burnt_steak"] == 1 && hunt.Hunger == 80, "cooked meat should be edible and filling");
    }

    private static void CheckWolvesLeaveHealthyOrFirelitPlayersAlone(GameData data) {
        var healthy = NightWorld(data);
        SetNeeds(healthy, health: 80);
        var stalker = new Wolf { Pos = healthy.Player + new Vector2(3, 0), Attack = 5 };
        PlaceWolf(healthy, stalker);
        healthy.Tick(.2f);
        Require(stalker.Leap == 0 && !healthy.Mauled, "wolves should not leap at a player above half health");

        var sheltered = NightWorld(data);
        SetNeeds(sheltered, health: 40);
        sheltered.Add(ThingKind.Campfire, sheltered.Player + new Vector2(2, 0)).Burn = 90;
        var circling = new Wolf { Pos = sheltered.Player + new Vector2(-3, 0), Attack = 5 };
        PlaceWolf(sheltered, circling);
        sheltered.Tick(.2f);
        Require(circling.Leap == 0 && !sheltered.Mauled, "campfire light should prevent the throat leap");
    }

    private static void CheckWolfLeapPinsAndKillsAPlayerOnHalfHealth(GameData data) {
        var maul = NightWorld(data);
        SetNeeds(maul, health: 49);
        foreach (var (item, count) in new[] { ("berries", 3), ("burnt_steak", 2), ("raw_meat", 1), ("water", 2), ("steel_axe", 1), ("wood", 4) })
            Give(maul, item, count);
        var leaper = new Wolf { Pos = maul.Player + new Vector2(3, 0) };
        var packmate = new Wolf { Pos = maul.Player + new Vector2(-9, 4), Attack = 5 };
        PlaceWolf(maul, leaper); PlaceWolf(maul, packmate);
        var heard = Listen(maul);
        var said = new List<string>();
        maul.Notify += said.Add;
        maul.Tick(.1f);
        Require(leaper.Leap > 0 && !maul.Mauled, "a nearby wolf should leap at a player on half health");
        WolfSteps.SnapTick(maul, .64f);
        Require(maul.Mauled, "the leap should pin the player");
        Require(said.Contains("You are pinned and can't move."), $"a pinned player should be told they cannot move: told {string.Join(" | ", said)}");
        Require(heard.Count(name => name == "takedown_scream") == 1, $"a player pinned by a leap should scream once: heard {string.Join(", ", heard)}");
        Require(leaper.Feeding, "the leap should make the wolf feed");
        Require(leaper.Pinning && leaper.LungeFrom == Vector2.Zero, "the leap should make the wolf pin, over the player");
        Require(leaper.Pos.DistanceTo(maul.Player) < 1.6f, "the leap should land at the throat");
        var pinned = maul.Player;
        maul.MovePlayer(Vector2.Right);
        maul.Attack(Vector2.Right);
        maul.Tick(.5f);
        Require(maul.Player == pinned && leaper.Health == data.WolfHealth, "a pinned player can neither move nor fight back");
        for (int i = 0; i < 40 && !maul.Dead; i++) maul.Tick(.1f);
        Require(maul.Dead, "a pinned player should be killed within seconds");
        Require(said.Contains("Wolves ate food in your backpack."), $"a player wolves killed should be told only that they ate the food: told {string.Join(" | ", said)}");
        for (int i = 0; i < 20; i++) maul.Tick(.1f);
        Require(packmate.Feeding, "the rest of the pack should stay to feed");
        Require(packmate.Pos.DistanceTo(pinned) < 1.6f, "the rest of the pack should run in to the pinned player");
        Require(maul.Wolves.Contains(leaper), "the leaping wolf should stay on the kill");
        var box = maul.Things.Single(t => t.Kind == ThingKind.Grave);
        Require(box.Inventory["berries"] == 0, "a wolf kill should leave no berries in the recovery box");
        Require(box.Inventory["burnt_steak"] == 0, "a wolf kill should leave no steak in the recovery box");
        Require(box.Inventory["raw_meat"] == 0, "a wolf kill should leave no raw meat in the recovery box");
        Require(box.Inventory["water"] == 2, "a wolf kill should keep water in the recovery box");
        Require(box.Inventory["steel_axe"] == 1, "a wolf kill should keep tools in the recovery box");
        Require(box.Inventory["wood"] == 4, "a wolf kill should keep wood in the recovery box");
        SafeTick(maul, data.RespawnSeconds);
        Require(!maul.Dead, "respawning should revive the player");
        Require(!maul.Mauled, "respawning should end the mauling");
        Require(!maul.Wolves.Contains(leaper), "respawning should send the leaping wolf away");
        Require(!maul.Wolves.Contains(packmate), "respawning should send its packmate away");
    }

    private static void CheckWolfThatBitesAPlayerToDeathEatsTheirFood(GameData data) {
        // Behind a lit torch a lone wolf creeps in and bites rather than leaping, so the bite is what kills.
        var world = TorchNight(data);
        SetNeeds(world, health: 5);
        Give(world, "berries", 3); Give(world, "wood", 4);
        PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(.8f, 0) });
        for (int i = 0; i < 30 && !world.Dead; i++) world.Tick(.1f);
        Require(world.Dead && !world.Mauled, "a wolf's bite should kill a player on 5 health without pinning them");
        var box = world.Things.Single(t => t.Kind == ThingKind.Grave);
        Require(box.Inventory["berries"] == 0, "a wolf that bites a player to death should eat their berries");
        Require(box.Inventory["wood"] == 4, "a wolf that bites a player to death should leave the rest in the recovery box");
    }

    private static void CheckAWolfThatFindsAPlayerInSightGoesForThemWithoutAHowl(GameData data) {
        var world = NightWorld(data);
        var heard = Listen(world);
        var wolf = PatrolWolf(world, new Wolf { Pos = world.Player + new Vector2(4, 0) });
        world.Tick(.1f);
        Require(wolf.Target == world.Me.Id && wolf.Howl == 0 && !heard.Any(name => name.StartsWith("wolf_howl", StringComparison.Ordinal)), "a wolf that finds a player within its sight should go for them without a howl");
    }

    private static void CheckSprintOutrunsWolf(GameData data) {
        // Fleeing straight away from a wolf four tiles behind for two seconds: walking loses ground, sprinting slowly gains it.
        float Gap(bool sprinting) {
            var world = NightWorld(data);
            var wolf = new Wolf { Pos = world.Player + new Vector2(-4, 0) };
            PlaceWolf(world, wolf);
            world.Me.Sprinting = sprinting;
            var start = world.Player;
            float closest = float.MaxValue;
            for (int i = 0; i < 40; i++) {
                world.MovePlayer(Vector2.Right * .05f * data.MoveSpeed(sprinting));
                world.Tick(.05f);
                closest = Math.Min(closest, wolf.Pos.DistanceTo(world.Player));
            }
            Require(world.Player.DistanceTo(start) > 7, "the open ground east of the test spot must let the player flee");
            return sprinting ? wolf.Pos.DistanceTo(world.Player) : closest;
        }
        Require(Gap(false) < 3, "a chasing wolf must be faster than a walking player");
        Require(Gap(true) > 4.1f, "a sprinting player must outrun a chasing wolf");
    }

    private static void CheckDensLetWolvesOutAtDusk(GameData data) {
        var world = new World(data);
        RunUntil(world, data.DuskStarts * data.DaySeconds - 1);
        SafeTick(world, .9f);
        Require(world.Wolves.Count == 0, "no wolf should come out before dusk");
        SafeTick(world, 1);
        Require(world.Wolves.Count == 1 && world.Wolves.All(w => w.Pos.DistanceTo(WolfPatrols.Dens[0].Door) < 4),
                "the first den should let its one wolf out as soon as dusk starts, and the others in turn after it");
        SafeTick(world, WolfPatrols.Dens.Length * WolfPatrols.DenGapSeconds);
        Require(!world.Night && world.Wolves.Count == WolfPatrols.Dens.Length, "every den should have let its one wolf out well before night");
        Require(world.Wolves.All(w => w.Target == 0 && w.Mode == WolfMode.Patrol), "wolves out of their dens should patrol until they smell someone");
    }

    private static void CheckARandomDenLetsOneMoreWolfOutEveryHour(GameData data) {
        // Behind a locked cabin door the player is never reached, so every wolf let out stays out until the retreat.
        var world = EmptyWorld(data);
        StepInside(world, world.Add(ThingKind.Hut, world.Player + new Vector2(2, 0)));
        world.Doorways.TurnKey();
        RunUntilDusk(world);
        SafeTick(world, data.HourSeconds - 1);
        Require(world.Wolves.Count == WolfPatrols.Dens.Length, "only the wolves of dusk should be out through the first hour");
        SafeTick(world, 2);
        Require(world.Wolves.Count == WolfPatrols.Dens.Length + 1 && WolfPatrols.Dens.Any(den => world.Wolves[^1].Pos.DistanceTo(den.Door) < 4),
                "one den should let one more wolf out an hour after dusk");
        SafeTick(world, data.HourSeconds);
        Require(world.Wolves.Count == WolfPatrols.Dens.Length + 2, "a den should let one more wolf out after each further hour");
        // From dusk at 20:24 to the retreat at 05:00 eight whole hours pass.
        SafeTick(world, data.WolfRetreatStarts * data.DaySeconds - world.Time - 1);
        Require(world.Wolves.Count == WolfPatrols.Dens.Length + 8, $"a night should let out one wolf an hour until the retreat, not {world.Wolves.Count - WolfPatrols.Dens.Length}");
    }

    private static void CheckWolvesAreNoStrongerOnLaterNights(GameData data) {
        (float Health, float Pace, float Bite) Wolf(int night) {
            var world = EmptyWorld(data);
            world.Player = Terrain.Point(120, 0);
            RunUntil(world, (night - 1) * data.DaySeconds);
            RunUntilDusk(world);
            SafeTick(world, .5f);
            var wolf = world.Wolves.Single();
            var from = wolf.Pos;
            SafeTick(world, 1);
            float pace = wolf.Pos.DistanceTo(from);
            SetNeeds(world, health: 100);
            PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(.8f, 0) });
            WolfSteps.SnapTick(world, .85f);
            return (wolf.Health, pace, 100 - world.Health);
        }
        var (first, later) = (Wolf(1), Wolf(30));
        Require(later.Health == first.Health, $"a wolf should take as many blows a month in as on the first night: {first.Health} and {later.Health}");
        Require(later.Pace == first.Pace, $"a wolf should walk as fast a month in as on the first night: {first.Pace} and {later.Pace}");
        foreach (float bite in new[] { first.Bite, later.Bite })
            Require(bite * 3 < 50 && bite * 4 >= 50, $"the fourth bite, not the third, should take a healthy player to half health on any night; one takes {bite}");
    }

    private static void CheckPatrollingWolfWalksAtHalfItsTrot(GameData data) {
        var world = NightWorld(data);
        world.Player = Terrain.Point(30, 0);
        var start = Terrain.Point(120, 0);
        var wolf = PatrolWolf(world, new Wolf { Pos = start, Heading = Terrain.Sideways.Angle() });
        SafeTick(world, 1);
        // A wolf trots at 3 m/s, so it patrols at 1.5 m/s.
        Require(Math.Abs(wolf.Pos.DistanceTo(start) - 1.5f) < .02f, $"a patrolling wolf should walk at half its trot, not {wolf.Pos.DistanceTo(start):0.00} m/s");
    }

    private static void CheckWolvesKeepToGroundAPlayerCanWalk(GameData data) {
        var world = new World(data);
        RunUntil(world, data.DuskStarts * data.DaySeconds);
        for (float t = 0; t < 120; t += .1f) {
            SafeTick(world, .1f); SetNeeds(world, health: 100);
            Require(world.Wolves.All(w => world.CanStand(w.Pos)), $"wolves should only walk where a player can, at {t:0.0}s: {string.Join(", ", world.Wolves.Where(w => !world.CanStand(w.Pos)).Select(w => w.Pos))}");
        }
        // Across the lake from its prey a wolf goes round the shore rather than through the water.
        var lake = NightWorld(data);
        lake.Player = Terrain.Lake - Terrain.Seaward * 18;
        var hunter = PlaceWolf(lake, new Wolf { Pos = Terrain.Lake + Terrain.Seaward * 18 });
        for (int i = 0; i < 300 && hunter.Pos.DistanceTo(lake.Player) > 2; i++) {
            SafeTick(lake, .1f); SetNeeds(lake, health: 100);
            Require(lake.CanStand(hunter.Pos), $"a hunting wolf should go round the lake, not through it: {hunter.Pos}");
        }
        Require(hunter.Pos.DistanceTo(lake.Player) <= 2, "a hunting wolf should find its way round the lake to the player");
        // Behind a row of cabins a wolf goes round them rather than through a wall.
        var camp = NightWorld(data);
        camp.Player = Terrain.Point(120, 0);
        for (int i = -2; i <= 2; i++) camp.Add(ThingKind.Hut, camp.Player + Terrain.Seaward * 3 + Terrain.Sideways * i * 1.5f);
        var stalker = PlaceWolf(camp, new Wolf { Pos = camp.Player + Terrain.Seaward * 8 + Terrain.Sideways * .4f });
        for (int i = 0; i < 300 && stalker.Pos.DistanceTo(camp.Player) > 2; i++) {
            SafeTick(camp, .1f); SetNeeds(camp, health: 100);
            Require(camp.CanStand(stalker.Pos), $"a hunting wolf should go round cabins, not through them: {stalker.Pos}");
        }
        Require(stalker.Pos.DistanceTo(camp.Player) <= 2, "a hunting wolf should find its way round cabins to the player");
        // Patrolling wolves keep out of firelight.
        var lit = NightWorld(data);
        lit.Player = Terrain.Point(30, 0);
        var fire = lit.Add(ThingKind.Campfire, Terrain.Point(120, 0)); fire.Burn = 9999;
        var patrol = PatrolWolf(lit, new Wolf { Pos = fire.Pos + Terrain.Seaward * (World.FireReach + 3), Heading = (-Terrain.Seaward).Angle() });
        for (int i = 0; i < 200; i++) {
            SafeTick(lit, .1f);
            Require(!lit.Protected(patrol.Pos), $"a patrolling wolf should keep out of firelight: {patrol.Pos}");
        }
    }

    private static void CheckSmelledPlayerDrawsAHowlAndThePack(GameData data) {
        var world = NightWorld(data);
        world.Player = Terrain.Point(120, 0);
        // The smelling wolf walks straight at the player, so it has no turn to make before it howls.
        var smeller = new Wolf { Pos = world.Player + Terrain.Seaward * (data.SmellRadius - 5), Heading = (-Terrain.Seaward).Angle() };
        var called = new Wolf { Pos = smeller.Pos + Terrain.Seaward * (data.HowlRadius - 10) };
        var deaf = new Wolf { Pos = smeller.Pos - Terrain.Seaward * (data.HowlRadius + 10) };
        var heard = Listen(world);
        foreach (var wolf in new[] { smeller, called, deaf }) PatrolWolf(world, wolf);
        SafeTick(world, .1f);
        Require(smeller.Target == world.Me.Id && smeller.Howl > 0, "a wolf within smell of the player should howl and take them as its prey");
        Require(heard.Contains("wolf_howl") && smeller.HowlLength == data.LoneHowlSeconds, "the night's first howl should be a lone wolf, held as long as its recording");
        Require(called.Mode == WolfMode.Called && called.Call.DistanceTo(smeller.Pos) < .5f && deaf.Mode == WolfMode.Patrol,
                "a howl should call the patrolling wolves within its reach, and only them");
        var at = smeller.Pos;
        SafeTick(world, smeller.HowlLength - .3f);
        Require(smeller.Pos == at, "a howling wolf should stand still");
        SafeTick(world, .5f);
        Require(smeller.Pos.DistanceTo(world.Player) < at.DistanceTo(world.Player) && smeller.Chase <= 0, "after its howl a wolf should close in without sprinting while the player is out of its sight");
        for (int i = 0; i < 100 && smeller.Pos.DistanceTo(world.Player) > data.WolfSightRange - .5f; i++) { SafeTick(world, .1f); SetNeeds(world, health: 100); }
        SafeTick(world, .1f);
        Require(smeller.Chase > 0, "a wolf should sprint once it can see the player");
        Require(heard.Count(name => name == "wolf_growl_distance") == 1, "a wolf should growl once as it breaks into a sprint");
        for (int i = 0; i < 400 && called.Target == 0; i++) { SafeTick(world, .1f); SetNeeds(world, health: 100); }
        Require(called.Target == world.Me.Id, $"a wolf answering a howl should smell the player on its way and go after them too");
    }

    private static void CheckWolvesGoHomeBeforeDawn(GameData data) {
        var world = NightWorld(data);
        world.Player = Terrain.Point(120, 0);
        var hunter = PlaceWolf(world, new Wolf { Pos = world.Player + Terrain.Seaward * 5 });
        RunUntil(world, (world.Day - 1 + data.WolfRetreatStarts) * data.DaySeconds + .1f);
        SafeTick(world, .1f);
        Require(hunter.Target == 0 && hunter.Mode == WolfMode.Home, "an hour before dawn every wolf should give up its hunt and head home");
        for (int i = 0; i < 600 && world.Wolves.Contains(hunter); i++) SafeTick(world, .1f);
        Require(!world.Wolves.Contains(hunter) && world.Dusk, "a wolf should go back into the nearest den before dawn");
    }

    private static void CheckDensStayOpen(GameData data) {
        var world = new World(data);
        foreach (var den in WolfPatrols.Dens) {
            Require(world.CanStand(den.Door) && !world.CanStand(den.Mouth - den.Face * .5f), "a den's rock should be solid and the ground at its door open");
            world.Player = den.Door + den.Face * 3;
            Require(world.Construction.PlacementBlock(ThingKind.Hut, den.Door + den.Face * 2) != null, "nothing should be built over a den's mouth");
        }
    }

    private static void CheckWolvesLetGoCleanly(GameData data) {
        // A player stepping into a cabin mid-howl leaves the wolf patrolling, not howling or fleeing as it walks off.
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(2, 0));
        var howler = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(-8, 0), Howl = 5, HowlLength = 5, Flee = 2, Stagger = 1 });
        StepInside(world, hut);
        SafeTick(world, .1f);
        Require(howler.Target == 0 && howler.Mode == WolfMode.Patrol && howler.Howl == 0 && howler.Flee == 0 && howler.Stagger == 0,
                "a wolf whose player took shelter should drop its howl and flight and patrol");
        // A wolf whose player left the game lets go of them; out of sight of the one still playing, it howls before it hunts them.
        var gone = NightWorld(data);
        var feeder = PlaceWolf(gone, new Wolf { Pos = gone.Player + new Vector2(20, 0), Target = 999, Feeding = true, Pinning = true, Leap = 1, Chase = 3 });
        SafeTick(gone, .1f);
        Require(feeder.Target != 999 && !feeder.Feeding && !feeder.Pinning && feeder.Leap == 0 && feeder.Chase == 0,
                "a wolf whose player left should stop feeding, leaping and chasing");
        // A pinned player goes free when the wolf heads home before dawn.
        var dawn = NightWorld(data);
        dawn.Player = Terrain.Point(120, 0);
        PlaceWolf(dawn, new Wolf { Pos = dawn.Player + Terrain.Seaward * .8f, Feeding = true, Pinning = true });
        SetMauled(dawn, true);
        RunUntil(dawn, (dawn.Day - 1 + data.WolfRetreatStarts) * data.DaySeconds + .1f);
        SafeTick(dawn, .2f);
        Require(!dawn.Mauled, "a pinned player should go free once the wolves head home");
    }

    private static void CheckFullPackIgnoresASwing(GameData data) {
        var pack = TorchNight(data);
        var wolves = Enumerable.Range(0, World.PackSize).Select(i => new Wolf { Pos = pack.Player + Vector2.Right.Rotated(i * Mathf.Tau / World.PackSize) * World.StalkDistance, Health = 4 }).ToArray();
        foreach (var w in wolves) PlaceWolf(pack, w);
        pack.UseTorch(Vector2.Right);
        Require(wolves.All(w => !w.Following), "a complete pack should ignore a swing");
        WolfTick(pack, 4, wolves);
        Require(pack.Health < 100, $"{World.PackSize} wolves around a torch should attack together");
    }

    private static void CheckEachPlayerHasTheirOwnPack(GameData data) {
        var (world, guest, _) = Hosted(data);
        world.Player = Terrain.Spawn;
        var far = Enumerable.Range(0, 63).Select(i => Terrain.Spawn + Vector2.Right.Rotated(i * .1f) * 35).First(world.CanStand);
        world.As(guest, () => world.MoveTo(far));
        RunUntilNight(world);
        ShutDens(world);
        // A patrolling wolf near each player smells the nearer one.
        Vector2 Near(Vector2 at, Vector2 other) => Enumerable.Range(0, 63).Select(i => at + Vector2.Right.Rotated(i * .1f) * 10).First(p => world.CanStand(p) && p.DistanceTo(other) > 30);
        var hostWolf = new Wolf { Pos = Near(world.Player, guest.Pos) }; var guestWolf = new Wolf { Pos = Near(guest.Pos, world.Player) };
        PatrolWolf(world, hostWolf); PatrolWolf(world, guestWolf);
        for (int i = 0; i < 3; i++) { world.Tick(.25f); SetNeeds(world, health: 100); SetNeeds(guest, health: 100); }
        Require(hostWolf.Target == world.Me.Id && guestWolf.Target == GuestId, "each player out at night should draw the wolves that smell them");

        Give(guest, "stone", 2); SetNeeds(guest, health: -1);
        world.Tick(.25f);
        Require(guest.RespawnIn > 0 && world.RespawnIn == 0, "only the guest should die");
        Require(world.Wolves.All(w => w.Target != GuestId) && world.Wolves.Any(w => w.Target == world.Me.Id), "a guest's collapse should send off only their own pack");
        Require(world.Things.Any(t => t.Kind == ThingKind.Grave && t.Inventory["stone"] == 2 && t.Pos.DistanceTo(far) < 2) && guest.Backpack.Count == 0, "a guest's belongings should stay where they fell");
        int warp = guest.Warp;
        for (float t = 0; t <= data.RespawnSeconds + .5f; t += .25f) { world.Tick(.25f); SetNeeds(world, health: 100); }
        Require(guest.RespawnIn == 0, "a guest should finish respawning");
        Require(guest.Pos == Terrain.Spawn, "a guest should respawn at the clearing");
        Require(guest.Warp == warp + 1, "the respawn move should tell the guest's machine");
    }
}
