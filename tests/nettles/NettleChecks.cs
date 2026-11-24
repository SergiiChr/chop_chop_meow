using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static GatheringSteps;
using static PlayerSteps;
using static NettleSteps;

/// <summary>Checks of nettles: where and when they come up, picking one by hand and with a shovel carried, and a worker's picking.</summary>
public static class NettleChecks {
    public static void Run(GameData data) {
        Test(CheckThreeNettlesComeUpEachNightUntilTenStand, data);
        Test(CheckNettlesComeUpOutOfSightOnGroundToStandOn, data);
        Test(CheckPickingANettleTakesThreeSecondsAndThreeHealth, data);
        Test(CheckNettleStingCanKill, data);
        Test(CheckCarriedShovelPicksANettleUnharmed, data);
        Test(CheckFullBackpackLeavesANettleUnpickedAndUnstung, data);
        Test(CheckWorkerPicksNettlesOnlyWithAShovelFromTheRack, data);
        Test(CheckNettleBlocksBuildingUntilItIsPicked, data);
    }

    private static void CheckThreeNettlesComeUpEachNightUntilTenStand(GameData data) {
        var world = EmptyWorld(data);
        Require(world.ThingsOf(ThingKind.Nettle).Count == 0, "no nettle should stand before the first night");
        foreach (int standing in new[] { 3, 6, 9, 10, 10 }) {
            RunUntilNight(world);
            Require(world.ThingsOf(ThingKind.Nettle).Count == standing, $"{standing} nettles should stand on night {world.Day}: {world.ThingsOf(ThingKind.Nettle).Count}");
        }
        RunUntilMorning(world);
        Require(world.ThingsOf(ThingKind.Nettle).Count == 10, "nettles should stay through the day");
        Give(world, "steel_shovel");
        foreach (var nettle in world.ThingsOf(ThingKind.Nettle).ToArray()[..2]) HoldGather(world, nettle, 4);
        Require(world.ThingsOf(ThingKind.Nettle).Count == 8 && world.Backpack["nettle"] == 2, $"two picked nettles should be gone for two in the backpack: {world.ThingsOf(ThingKind.Nettle).Count} stand");
        RunUntilNight(world);
        Require(world.ThingsOf(ThingKind.Nettle).Count == 10, $"a night should bring the nettles back up to ten and no further: {world.ThingsOf(ThingKind.Nettle).Count}");
    }

    private static void CheckNettlesComeUpOutOfSightOnGroundToStandOn(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, world.Player + new Vector2(40, 0));
        RunUntilNight(world);
        foreach (var nettle in world.ThingsOf(ThingKind.Nettle)) {
            Require(nettle.Pos.DistanceTo(world.Player) > data.SightRadius, $"a nettle should come up out of the player's sight, not {nettle.Pos.DistanceTo(world.Player):0.0} m off");
            Require(world.CanStand(nettle.Pos), $"a nettle should come up on ground to stand on: {nettle.Pos}");
            Require(nettle.Pos.DistanceTo(chest.Pos) > 2, "a nettle should not come up against a building");
        }
        var places = world.ThingsOf(ThingKind.Nettle).Select(t => t.Pos).ToArray();
        Require(places.Distinct().Count() == 3, "each nettle should come up in a place of its own");
    }

    private static void CheckPickingANettleTakesThreeSecondsAndThreeHealth(GameData data) {
        var (world, nettle) = NettlePatch(data);
        HoldGather(world, nettle, 2.75f);
        Require(world.Find(nettle.Id) != null && world.Backpack["nettle"] == 0, "a nettle should still stand after 2.75 s of picking");
        Require(Mathf.IsEqualApprox(world.Health, 97.25f), $"2.75 s of picking by hand should sting for 2.75 health: {world.Health}");
        HoldGather(world, nettle, Step);
        Require(world.Find(nettle.Id) == null && world.Backpack["nettle"] == 1, "3 s of picking should take the nettle for good, as one nettle in the backpack");
        Require(Mathf.IsEqualApprox(world.Health, 97), $"a nettle picked by hand should cost 3 health: {world.Health}");
        Require(world.Had("nettle"), "the settlement should know nettles once one is picked");
    }

    private static void CheckNettleStingCanKill(GameData data) {
        var (world, nettle) = NettlePatch(data);
        SetNeeds(world, health: 2);
        HoldGather(world, nettle, 3);
        Require(world.Dead, $"a player with 2 health should die 2 s into picking a nettle: {world.Health}");
        Require(world.Find(nettle.Id) != null, "the nettle should stand where it killed its picker");
    }

    private static void CheckCarriedShovelPicksANettleUnharmed(GameData data) {
        var (world, nettle) = NettlePatch(data);
        Give(world, "steel_shovel");
        HoldGather(world, nettle, 2.75f);
        Require(world.Find(nettle.Id) != null, "a shovel should not make picking a nettle any faster than 3 s");
        HoldGather(world, nettle, Step);
        Require(world.Find(nettle.Id) == null && world.Backpack["nettle"] == 1, "a nettle should be picked after 3 s with a shovel carried");
        Require(world.Health == 100, $"a carried shovel should spare the picker the sting: {world.Health}");
    }

    private static void CheckFullBackpackLeavesANettleUnpickedAndUnstung(GameData data) {
        var (world, nettle) = NettlePatch(data);
        SetBackpackSlots(world, 1);
        Give(world, "wood", 64);
        Require(world.GatherBlock(nettle)?.Contains("No room") == true, $"a full backpack should say it has no room for a nettle: {world.GatherBlock(nettle)}");
        HoldGather(world, nettle, 4);
        Require(world.Find(nettle.Id) != null && world.Health == 100, $"a player with no room should neither pick the nettle nor be stung: {world.Health}");
    }

    private static void CheckWorkerPicksNettlesOnlyWithAShovelFromTheRack(GameData data) {
        var (world, nettle, chest, rack, picker) = NettleYard(data);
        Work(world, 20);
        Require(world.Find(nettle.Id) != null && picker.Status.Contains("shovel"), $"a picker with no shovel to take should wait for one: {picker.Status}");
        rack.Inventory.Add("steel_shovel", 1);
        Work(world, 60, () => chest.Inventory["nettle"] > 0);
        Require(world.Find(nettle.Id) == null && chest.Inventory["nettle"] == 1, $"a picker should take a shovel, pick the nettle and store it: {picker.Status}");
        Require(picker.Tool == "steel_shovel", $"the picker should hold the shovel they picked with: {picker.Tool}");
    }

    private static void CheckNettleBlocksBuildingUntilItIsPicked(GameData data) {
        var (world, nettle) = NettlePatch(data);
        Give(world, "wood", 8); Give(world, "gravel", 4); Give(world, "steel_shovel");
        world.Player = nettle.Pos + new Vector2(2, 0);
        Require(world.Construction.Build(ThingKind.Chest, nettle.Pos) == null && world.Find(nettle.Id) != null, "a chest should not go up where a nettle stands");
        Require(world.Construction.Build(ThingKind.GravelPath, nettle.Pos) == null, "a gravel path should not be laid where a nettle stands");
        HoldGather(world, nettle, 4);
        BuildAt(world, ThingKind.Chest, nettle.Pos);
    }
}
