using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks of torches: lighting one, how long it burns, and putting it out.</summary>
public static class TorchChecks {
    public static void Run(GameData data) {
        Test(CheckALitTorchMovedOutOfTheBackpackGoesOut, data);
        Test(CheckTorchLightsAndBurnsOut, data);
    }

    private static void CheckALitTorchMovedOutOfTheBackpackGoesOut(GameData data) {
        var world = EmptyWorld(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, world.Player + Vector2.One));
        Give(world, "torch", 1); world.UseTorch(Vector2.Zero); SafeTick(world, World.KindleSeconds + .1f);
        Require(world.TorchLit, "the torch should light");
        world.Transfer(world.Backpack, chest.Inventory, "torch", 1);
        SafeTick(world, .1f);
        Require(!world.TorchLit && world.Torch == 0 && chest.Inventory["torch"] == 1, "a lit torch moved into a chest should go out and lie there as a torch");
    }

    private static void CheckTorchLightsAndBurnsOut(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "wood", 3);
        world.UseTorch(Vector2.Zero);
        Require(!world.Craft("torch"), "a torch should need a chisel to make");
        Require(world.Torch == 0, "using no torch should not put one in hand");
        Require(world.TorchKindling == 0, "using no torch should not start kindling");
        Give(world, "stone_chisel", 1);
        Require(world.Craft("torch") && world.Backpack["wood"] == 0, "a torch should take 3 wood and nothing else");
        SafeTick(world, data.Recipes["torch"].Seconds + .25f);
        Give(world, "torch", 1);
        world.UseTorch(Vector2.Zero);
        Require(world.Backpack["torch"] == 2, "the first use should leave the torch it lights in the backpack");
        Require(world.Torch == data.CampfireSeconds, "the first use should start a full torch");
        Require(!world.TorchLit, "the first use should not light the torch before the flint strikes");
        SafeTick(world, World.KindleSeconds - .2f);
        Require(!world.TorchLit, "lighting should take its time");
        SafeTick(world, .3f);
        Require(world.TorchLit && world.Backpack["torch"] == 2, "the torch should catch after the lighting animation and stay in the backpack");

        // Steady swings keep it burning and flare the light; four in two seconds put it out.
        for (int i = 0; i < 4; i++) { world.UseTorch(Vector2.Right); Require(world.TorchFlare > 0, "a swing should flare the light"); SafeTick(world, .7f); }
        Require(world.TorchLit, "swinging at a steady pace should keep the torch burning");
        SafeTick(world, World.WildWindow);
        string said = "";
        world.Notify += message => said = message;
        var heard = Listen(world);
        for (int i = 0; i < World.WildSwings; i++) { world.UseTorch(Vector2.Right); SafeTick(world, .65f); }
        float left = world.Torch;
        Require(!world.TorchLit && said == "Torch went off due to wild swinging.", "wild swinging should put the torch out with a message");
        Require(heard.Count(name => name == "torch_out") == 1, "a torch going out should fizzle once");
        SafeTick(world, 5);
        Require(world.Torch == left, "an unlit torch should keep its burn time");
        world.UseTorch(Vector2.Zero); SafeTick(world, World.KindleSeconds + .1f);
        Require(world.TorchLit, "relighting should light the torch");
        Require(world.Backpack["torch"] == 2, "relighting should not use a new torch");
        Require(world.Torch < left, "relighting should not reset the torch's time");
        Require(world.Torch > left - 1, "relighting should not add time to the torch");
        SafeTick(world, world.Torch + .25f);
        Require(world.Torch == 0, "a torch should burn out completely");
        Require(!world.TorchLit, "a burnt out torch should not stay lit");
        Require(heard.Count(name => name == "torch_out") == 2, "a torch burning out should fizzle");
        Require(world.Backpack["torch"] == 1, "a burnt out torch should be used up, leaving the spare torch in the backpack");
        world.UseTorch(Vector2.Zero); SafeTick(world, World.KindleSeconds + .1f);
        Require(world.TorchLit, "a fresh torch should light");
        world.Die();
        Require(heard.Count(name => name == "torch_out") == 3, "a lit torch should fizzle out when the player falls");
        SafeTick(world, data.RespawnSeconds + .1f);
        Give(world, "torch", 1); world.UseTorch(Vector2.Zero); world.Die();
        Require(world.Torch == 0, "a torch should burn out when the player falls");
        Require(!world.TorchLit, "a torch should not stay lit when the player falls");
        Require(world.TorchKindling == 0, "a torch should stop kindling when the player falls");
        Require(heard.Count(name => name == "torch_out") == 3, "a torch still being lit when the player falls should not fizzle");
    }
}
