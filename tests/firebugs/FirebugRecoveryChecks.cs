using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static ReplantingSteps;
using static SetupSteps;
using static WorldSteps;

internal static class FirebugRecoveryChecks {
    public static void Run(GameData data) {
        Test(CheckPackingAndUnpackingDoesNotRefreshAFirebugsLife, data);
        Test(CheckTheLastFirebugDyingLeavesAnEmptyCrate, data);
        Test(CheckDismantlingKeepsTheLifetimeOfFirebugsInTheRecoveryBox, data);
        Test(CheckAChestCrushedByATreeLeavesFirebugsWithTheirLifetime, data);
    }

    private static void CheckPackingAndUnpackingDoesNotRefreshAFirebugsLife(GameData data) {
        var world = EmptyWorld(data);
        float caught = world.Time;
        Require(GiveFirebugs(world, 1) == 1, "one firebug should fit in the backpack");
        RunUntil(world, caught + data.DaySeconds);
        var packer = world.Add(ThingKind.Packer, world.Player + Vector2.Down);
        world.SetPacking(packer, Firebugs.Item);
        Give(world, "crate");
        Require(world.LoadInput(packer, "crate", 1) == 1 && world.LoadInput(packer, Firebugs.Item, 1) == 1,
            "the packing station should take a firebug and an empty crate");
        var output = packer.Output!;
        Work(world, 10, () => output.First() != null);
        string packed = output.First() ?? "";
        Require(World.PackedId(packed) > 0 && world.Transfer(output, world.Backpack, packed, 1) == 1,
            "the packed firebug should come out in a crate");
        Require(world.MoveInCrate(World.PackedId(packed), Firebugs.Item, 1, false) == 1, "the firebug should unpack into the backpack");
        RunUntil(world, caught + 3 * data.DaySeconds - 1);
        Require(world.Backpack[Firebugs.Item] == 1, "the unpacked firebug should stay alive for the rest of its three days");
        world.Tick(1, true);
        Require(world.Backpack[Firebugs.Item] == 0, "packing and unpacking should not reset the firebug's lifetime");
    }

    private static void CheckTheLastFirebugDyingLeavesAnEmptyCrate(GameData data) {
        var world = new World(data, false) { QuietNights = true };
        float caught = world.Time;
        GiveFirebugs(world, 1); Give(world, "crate");
        Require(world.MoveInCrate(0, Firebugs.Item, 1, true) == 1, "the firebug should go into the crate");
        string packed = world.Backpack.Items.Keys.Single(World.IsPacked);
        RunUntil(world, caught + 2519);
        Require(world.Backpack[packed] == 1, "the crate should still hold the living firebug");
        world.Tick(1, true);
        Require(world.Backpack[packed] == 0 && world.Backpack["crate"] == 1 && world.Packed(packed) == null,
            "the firebug should disappear and leave an empty crate at the end of its third day");
    }

    private static void CheckDismantlingKeepsTheLifetimeOfFirebugsInTheRecoveryBox(GameData data) {
        var world = EmptyWorld(data);
        float caught = world.Time;
        Require(GiveFirebugs(world, 1) == 1, "one firebug should fit in the backpack");
        RunUntil(world, caught + data.DaySeconds);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.Right);
        Require(world.Transfer(world.Backpack, chest.Inventory, Firebugs.Item, 1) == 1, "the old firebug should go into the chest");
        SetBackpackSlots(world, 1);
        Give(world, "wood", 64);
        Require(world.Construction.Dismantle(chest), "the chest should come down even with a full backpack");
        var recovery = world.ThingsOf(ThingKind.Grave).Single();
        RunUntil(world, caught + 3 * data.DaySeconds - 1);
        Require(recovery.Inventory[Firebugs.Item] == 1, "the recovered firebug should survive until its original expiry");
        world.Tick(1, true);
        Require(recovery.Inventory[Firebugs.Item] == 0, "recovering the firebug from a dismantled chest should not reset its lifetime");
    }

    private static void CheckAChestCrushedByATreeLeavesFirebugsWithTheirLifetime(GameData data) {
        var (world, chest) = TreeReadyToGrowBeside(data, ThingKind.Chest);
        world.Player = chest.Pos + Vector2.Right;
        float caught = world.Time;
        Require(GiveFirebugs(world, 1) == 1 && world.Transfer(world.Backpack, chest.Inventory, Firebugs.Item, 1) == 1,
            "the chest beside the growing tree should take a firebug");
        world.Tick(2, true);
        var recovery = world.ThingsOf(ThingKind.Grave).Single();
        Require(world.Find(chest.Id) == null && recovery.Inventory[Firebugs.Item] == 1, "the crushed chest should leave the firebug in a recovery box");
        RunUntil(world, caught + 3 * data.DaySeconds);
        Require(recovery.Inventory[Firebugs.Item] == 0, "leaving the crushed chest should not reset the firebug's lifetime");
    }
}
