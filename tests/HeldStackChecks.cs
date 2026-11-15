using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static PlayerSteps;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks for held stacks: units a player picks up stay in their cell, nobody else takes them, and they land by the rules of where they are put down.</summary>
public static class HeldStackChecks {
    public static void Run(GameData data) {
        CheckAHeldStackStaysInItsCellUntilLetGo(data);
        CheckPuttingDownFillsTheCellAndKeepsTheRestHeld(data);
        CheckPuttingDownOnAnotherItemInTheBackpackSwapsThem(data);
        CheckAnotherPlayerCannotTakeAHeldStackAndIsToldWhy(data);
        CheckAStationStartsNoBatchWhileAStackInItIsHeld(data);
        CheckAStationInputTakesOnlyWhatItsRecipeUses(data);
        CheckAHeldStackIsLetGoOutOfReachAndOnLeaving(data);
        CheckAHeldBottleStaysOutOfGatheringWater(data);
        CheckAnEmptyCratePacksOnlyTheHeldUnits(data);
        CheckAHeldStackIsLetGoOnceItsCrateIsPutDown(data);
    }

    private static void CheckAHeldBottleStaysOutOfGatheringWater(GameData data) {
        var world = EmptyWorld(data);
        var lake = new Thing { Kind = ThingKind.Water };
        Give(world, "empty_bottle", 1);
        world.Hold(world.Backpack, CellOf(world.Backpack, "empty_bottle"), 1);
        SetNeeds(world, thirst: 25);
        Require(Water.CollectWater(world, lake) == WaterCollectResult.SourceSip && world.Held is { Item: "empty_bottle" },
            "with the only bottle held, gathering water should drink from the source and leave the bottle held");
    }

    private static void CheckAnEmptyCratePacksOnlyTheHeldUnits(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "crate", 1); Give(world, "wood", 10);
        world.Hold(world.Backpack, CellOf(world.Backpack, "wood"), 5);
        world.MoveInCrate(0, "wood", int.MaxValue, true);
        string packed = world.Backpack.Items.Keys.Single(id => id.StartsWith(World.PackedPrefix));
        Require(world.Packed(packed)!["wood"] == 5 && world.Backpack["wood"] == 5 && world.Held == null,
            "five held wood put on an empty crate should pack just those five");
    }

    private static void CheckAHeldStackIsLetGoOnceItsCrateIsPutDown(GameData data) {
        var world = EmptyWorld(data);
        string packed = PackCrate(world, ("berries", 4));
        var contents = world.Packed(packed)!;
        world.Hold(contents, 0, 4);
        var crate = world.PlaceCrate(packed, world.Player + new Vector2(2, 0))!;
        world.Tick(.1f);
        Require(world.Held == null && crate.Inventory.Layout[0].Held == 0, "putting the crate down should let go of the stack held from its contents");
    }

    private static void CheckAHeldStackStaysInItsCellUntilLetGo(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        Give(world, "wood", 10);
        int cell = CellOf(world.Backpack, "wood");
        world.Hold(world.Backpack, cell, 6);
        Require(world.Held is { Item: "wood", Count: 6 } && world.Backpack.Layout[cell].Count == 10, "picking up 6 of 10 wood should hold them in their cell");
        Require(world.Transfer(world.Backpack, chest.Inventory, "wood", 10) == 4 && world.Backpack.Layout[cell].Count == 6, "only the 4 wood nobody holds should move out");
        world.Release();
        Require(world.Held == null && world.Backpack.Layout[cell].Held == 0, "letting go should leave the wood in the same cell, held by nobody");
        Require(world.Transfer(world.Backpack, chest.Inventory, "wood", 6) == 6, "once let go of, the wood should move out");
    }

    private static void CheckPuttingDownFillsTheCellAndKeepsTheRestHeld(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        Give(world, "wood", 10);
        world.Transfer(world.Backpack, chest.Inventory, "wood", 10);
        Give(world, "wood", 60);
        world.Hold(world.Backpack, CellOf(world.Backpack, "wood"), 60);
        world.PutDown(chest.Inventory, 0, 60);
        Require(chest.Inventory.Layout[0].Count == 64 && world.Held is { Count: 6 } && world.Backpack["wood"] == 6,
            "a held 60 wood put on a chest cell of 10 should fill it to 64 and keep 6 held");
        world.PutDown(chest.Inventory, 1, 1);
        Require(chest.Inventory.Layout[1] is { Item: "wood", Count: 1 } && world.Held is { Count: 5 }, "putting down one should place one and keep the rest held");
    }

    private static void CheckPuttingDownOnAnotherItemInTheBackpackSwapsThem(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "wood", 5); Give(world, "stone", 3);
        int wood = CellOf(world.Backpack, "wood"), stone = CellOf(world.Backpack, "stone");
        world.Hold(world.Backpack, wood, 5);
        world.PutDown(world.Backpack, stone, 5);
        Require(world.Backpack.Layout[stone].Item == "wood" && world.Backpack.Layout[wood].Item == "stone" && world.Held == null,
            "a whole held stack put on another item's cell should swap the two and hold nothing");
    }

    private static void CheckAnotherPlayerCannotTakeAHeldStackAndIsToldWhy(GameData data) {
        var (world, guest, heard) = Hosted(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        guest.Pos = world.Player;
        Give(world, "coal", 8);
        world.Transfer(world.Backpack, chest.Inventory, "coal", 8);
        world.Hold(chest.Inventory, 0, 8);
        world.Run(guest, new Command(Verb.Transfer, world.Locate(chest.Inventory), 0, "coal", 8, new Vector2(-1, -1)));
        world.Run(guest, new Command(Verb.Hold, world.Locate(chest.Inventory), 8, "coal", 0, default));
        Require(guest.Backpack["coal"] == 0 && guest.HeldStack == null && chest.Inventory.Layout[0].Count == 8,
            "another player should neither take nor pick up a stack someone holds");
        Require(heard.Count(m => m == World.HeldByAnother) == 2, $"the other player should be told why, each time: {string.Join(" | ", heard)}");
    }

    private static void CheckAStationStartsNoBatchWhileAStackInItIsHeld(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        world.Player = furnace.Pos + Vector2.One;
        Give(world, "raw_meat", 4); Give(world, "coal", 4);
        world.QueueBatch(furnace, "burnt_steak", 2);
        // Two batches' meat lies in the station, so what is not held is still enough for one.
        world.Hold(furnace.Inventory, CellOf(furnace.Inventory, "raw_meat"), 1);
        Require(furnace.Inventory["raw_meat"] == 4 && world.Held is { Count: 1 }, "the furnace should hold two batches of meat, one unit of it held");
        world.Tick(.1f);
        Require(!furnace.Processing, "a station should start no batch while a player holds a stack in it");
        world.Release();
        world.Tick(.1f);
        Require(furnace.Processing, "the station should start once the stack is let go");
    }

    private static void CheckAStationInputTakesOnlyWhatItsRecipeUses(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        world.Player = furnace.Pos + Vector2.One;
        Give(world, "iron_ore", 10); Give(world, "stone", 5);
        world.Hold(world.Backpack, CellOf(world.Backpack, "iron_ore"), 10);
        world.PutDown(furnace.Inventory, -1, 10);
        Require(furnace.Inventory["iron_ore"] == 10 && world.Held == null, "held iron ore put on a furnace smelting iron should load its input slot");
        world.Hold(world.Backpack, CellOf(world.Backpack, "stone"), 5);
        world.PutDown(furnace.Inventory, -1, 5);
        Require(furnace.Inventory["stone"] == 0 && world.Held is { Item: "stone", Count: 5 }, "stone the recipe does not use should stay held");
    }

    private static void CheckAHeldStackIsLetGoOutOfReachAndOnLeaving(GameData data) {
        var (world, guest, _) = Hosted(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        guest.Pos = world.Player;
        Give(world, "wood", 12);
        world.Transfer(world.Backpack, chest.Inventory, "wood", 12);
        world.Hold(chest.Inventory, 0, 12);
        world.Player = chest.Pos + new Vector2(World.ItemReach + 5, 0);
        world.Tick(.1f);
        Require(world.Held == null && chest.Inventory["wood"] == 12, "walking out of reach should let go of a stack held from a chest");
        world.Run(guest, new Command(Verb.Hold, world.Locate(chest.Inventory), 12, "wood", 0, default));
        Require(guest.HeldStack is { Count: 12 }, "the guest should hold the chest's wood");
        world.Depart(GuestId);
        Require(chest.Inventory["wood"] == 12, "a player who leaves should let go of what they held");
    }
}
