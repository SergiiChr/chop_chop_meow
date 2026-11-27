using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static EconomySteps;
using static PlayerSteps;

/// <summary>Checks of how inventories stack items and how far the player reaches to move them.</summary>
public static class InventoryChecks {
    public static void Run(GameData data) {
        Test(CheckOnlyCoinsUseTheLargerStack, data);
        Test(CheckEachToolTakesItsOwnSlot, data);
        Test(CheckPlayerMovesItemsWithinFifteenMetres, data);
    }

    private static void CheckOnlyCoinsUseTheLargerStack(GameData data) {
        var bag = new Inventory { Slots = 2 };
        Require(bag.Add(Inventory.Coins, 1500) == 1500, "every coin should fit in the bag");
        Require(bag.UsedSlots == 2, "coins should stack to 1000 per slot, so 1500 need two slots");
        Require(bag.Stacks().First().Count == 1000, "the first coin stack should hold 1000");
        Require(bag.Room(Inventory.Coins) == 500 && bag.Room("wood") == 0, "only coins should use the larger stack");

        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        SetCoins(world, 5);
        world.Transfer(world.Backpack, sign.Inventory, PackCrate(world, ("steel_axe", 1)), 1);
        Require(world.Sell(sign), "a loaded crate should sell");
        SafeTick(world, data.TradeSeconds + .1f);
        Require(world.Coins == 5 && sign.Wages![Inventory.Coins] == 42, "sale proceeds for the goods and their crate should arrive as whole coins in the wage slot");
        sign.Inventory.Add(Inventory.Coins, 1);
        Require(!world.Sell(sign) && sign.Inventory[Inventory.Coins] == 1, "coins themselves cannot be sold");
        Require(!world.Buy(sign, Inventory.Coins, 1), "coins themselves cannot be bought");
    }

    private static void CheckEachToolTakesItsOwnSlot(GameData data) {
        var world = EmptyWorld(data);
        GiveVessel(world, Water.Empty, Water.Liquid, 3, 3); Give(world, "steel_axe", 2);
        Require(world.Backpack.UsedSlots == 5, "each refillable bottle and each tool should take its own slot");
        var crate = world.NewCrate();
        Require(crate.Add("steel_pick", data.CrateCapacity) == data.CrateCapacity && crate.UsedSlots == data.CrateCapacity, "a crate should hold a full crate of single tools");
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(10, 10)));
        Require(crate.TransferTo(chest.Inventory, "steel_pick", 8) == World.ChestStacks, "a chest should take one tool per slot");
        var hands = new Worker().Bag;
        Require(hands.Add("steel_axe", 20) == 12 && hands.Add("wood", 1) == 0, "a worker should carry twelve tools, one per slot");
        var mixed = new Worker().Bag;
        Require(mixed.Add("wood", 10) == 10, "a worker should carry ten wood");
        Require(mixed.Add("stone", 1) == 1, "a worker should carry a stone beside the wood");
        Require(mixed.Add("steel_pick", 1) == 1, "a worker should carry a tool beside the other things");
        Require(mixed.Add("coal", 1) == 0, "a worker should carry no more than twelve things in all");
    }

    private static void CheckPlayerMovesItemsWithinFifteenMetres(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(20, 20);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, world.Player + new Vector2(14, 0)));
        var furnace = world.Add(ThingKind.Furnace, world.Player + new Vector2(0, 14));
        var far = TakeEverything(world, world.Add(ThingKind.Chest, world.Player + new Vector2(-17, 0)));
        Give(world, "wood", 6); Give(world, "iron_ore", 4);
        Require(world.Transfer(world.Backpack, chest.Inventory, "wood", 2) == 2, "a chest 14 m away should take items from the backpack");
        Require(world.LoadInput(furnace, "iron_ore", 4) == 4, "a station 14 m away should take its inputs from the backpack");
        Require(world.Transfer(world.Backpack, far.Inventory, "wood", 2) == 0, "a chest 17 m away should be out of reach");
    }
}
