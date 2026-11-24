using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;

/// <summary>Checks of the valley: what the map starts with, the things on it, and the fog over unseen ground.</summary>
public static class MapChecks {
    public static void Run(GameData data) {
        Test(CheckCliffsHaveDepositsAndLooseCoal, data);
        Test(CheckResourcesScatterAcrossTheOpenValley, data);
        Test(CheckThingsOfAKindStayListedAsTheyComeAndGo, data);
        Test(CheckFogHidesUnseenGroundAndRemembersExploredGround, data);
    }

    private static void CheckCliffsHaveDepositsAndLooseCoal(GameData data) {
        var world = new World(data);
        foreach (var cliff in Cliff.All) {
            Require(!world.CanStand(cliff.Center), $"each {cliff.Ore} cliff should block movement");
            Require(Terrain.FromMountains(cliff.Center) < 0,
                $"each {cliff.Ore} cliff's middle should be off the mountain range");
            Require(Terrain.FromMountains(cliff.Center + cliff.Face * 2.6f) > 0,
                $"each {cliff.Ore} cliff should stand at the foot of a mountain range");
            Require(world.Things.Count(t => t.Kind == cliff.Ore && cliff.DistanceTo(t.Pos) < 3) == 2, $"each {cliff.Ore} cliff should have deposits at its foot");
        }
        foreach (var cliff in new[] { Cliff.Of(ThingKind.Iron), Cliff.Of(ThingKind.Coal) }) {
            Require(world.Things.Where(t => t.Kind == cliff.Ore).All(t => Cliff.All.Any(c => c.Ore == cliff.Ore && c.DistanceTo(t.Pos) < 3)),
                    $"{cliff.Ore} deposits should only spawn at the foot of their cliffs");
        }
        {
            var cliff = Cliff.Of(ThingKind.Coal);
            var loose = world.Things.Where(t => t.Kind == ThingKind.LooseCoal).ToList();
            Require(loose.Count > 0 && loose.All(t => cliff.DistanceTo(t.Pos) is > 3 and < 12 && world.CanStand(t.Pos)),
                    $"loose {cliff.Ore} should spread further than the deposits but stay near the nearer cliff");
            Require(loose.All(a => loose.All(b => a == b || a.Pos.DistanceTo(b.Pos) >= 2)), $"loose {cliff.Ore} pieces should lie apart");
        }
    }

    private static void CheckResourcesScatterAcrossTheOpenValley(GameData data) {
        var world = new World(data);
        foreach (var (kind, spacing) in new[] { (ThingKind.Stone, 15f), (ThingKind.Berries, 14f) }) {
            var found = world.Things.Where(t => t.Kind == kind).ToList();
            Require(found.Count >= 15 && found.All(t => t.Pos.DistanceTo(Terrain.Spawn) >= 9 && found.All(o => o == t || o.Pos.DistanceTo(t.Pos) >= spacing)),
                    $"{kind} should be scattered singly away from the clearing, so more of it takes exploring");
        }
        Require(world.Things.All(t => Terrain.Open(t.Pos) || t.IsDeposit),
                "nothing should be placed in the sea, the lake or the mountains");
        var trees = world.Things.Where(t => t.Kind == ThingKind.Tree).ToList();
        float woods = trees.Count(t => Terrain.FromMountains(t.Pos) < 15), plains = trees.Count(t => Terrain.FromMountains(t.Pos) >= 30);
        Require(trees.Count > 400 && woods > plains * 3, $"trees should thicken toward the mountains ({trees.Count} in all, {woods} near them, {plains} on the plains)");
        Require(world.Things.Count(t => t.Kind == ThingKind.Tree && t.Pos.DistanceTo(Terrain.Spawn) < 9) == 1,
                "the clearing should keep only its one nearby tree");
    }

    /// <summary>Asking for one kind of thing gives every thing of that kind and no other, after placing, picking up, clearing and loading a save.</summary>
    private static void CheckThingsOfAKindStayListedAsTheyComeAndGo(GameData data) {
        var world = EmptyWorld(data);
        var picked = world.Add(ThingKind.LooseStone, new Vector2(20, 20), 1);
        var left = world.Add(ThingKind.LooseStone, new Vector2(24, 20), 1);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(30, 20)));
        PickUp(world, picked);
        RemoveThings(world, t => t == chest);
        var pile = world.Add(ThingKind.Pile, new Vector2(40, 20));
        Require(world.ThingsOf(ThingKind.LooseStone).Count == 1 && world.ThingsOf(ThingKind.LooseStone)[0] == left && world.ThingsOf(ThingKind.Chest).Count == 0,
            "a picked-up stone and a cleared chest should leave the lists of their kinds");
        Require(world.ThingsOf(ThingKind.Pile).Count == 1 && world.ThingsOf(ThingKind.Pile)[0] == pile, "a placed pile should be listed with its kind");
        var loaded = EmptyWorld(data);
        TakeEverything(loaded, loaded.Add(ThingKind.Chest, new Vector2(50, 20)));
        loaded.Deserialize(world.Serialize());
        Require(loaded.ThingsOf(ThingKind.LooseStone).Count == 1 && loaded.ThingsOf(ThingKind.LooseStone)[0].Id == left.Id && loaded.ThingsOf(ThingKind.Chest).Count == 0
            && loaded.ThingsOf(ThingKind.Pile).Count == 1 && loaded.Find(pile.Id)?.Kind == ThingKind.Pile,
            "a loaded save should list its own things by kind and none from before");
    }

    private static void CheckFogHidesUnseenGroundAndRemembersExploredGround(GameData data) {
        var fresh = new World(data);
        var far = Cliff.Of(ThingKind.Coal).Center;
        Require(fresh.Explored(Terrain.Spawn) && !fresh.Explored(far),
                "a new game should reveal only the clearing around the player");
        Require(Known(fresh).All(t => t.Pos.DistanceTo(Terrain.Spawn) <= data.SightRadius),
                "things beyond sight should stay unknown until seen");

        var world = EmptyWorld(data);
        world.Player = new Vector2(10, 10);
        var tree = world.Add(ThingKind.Tree, new Vector2(12, 10), 42);
        var hidden = world.Add(ThingKind.Stone, new Vector2(44, 44), 64);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(9, 12));
        furnace.Processing = true;
        SafeTick(world, Step);
        Require(Known(world).Contains(tree) && !Known(world).Any(t => t.Id == hidden.Id),
                "only things within the sight circle should become known");

        world.Player = new Vector2(40, 10);
        SafeTick(world, Step);
        tree.Remaining = 0;
        var gone = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(11, 11)));
        RemoveThings(world, t => t == gone);
        var memory = Known(world).Single(t => t.Id == tree.Id);
        Require(memory != tree, "explored ground out of sight should show a remembered copy of the tree");
        Require(memory.Remaining == 42, "explored ground out of sight should keep the tree as last seen");
        Require(world.Explored(new Vector2(12, 10)), "explored ground should be marked explored");
        Require(!Known(world).Single(t => t.Id == furnace.Id).Processing, "explored ground out of sight should not show the furnace's live activity");

        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Explored(new Vector2(12, 10)), "save/load should keep explored ground");
        Require(!loaded.Explored(new Vector2(44, 44)), "save/load should not explore new ground");
        Require(Known(loaded).Single(t => t.Id == tree.Id).Remaining == 42, "save/load should keep remembered things");

        world.Player = new Vector2(10, 10);
        SafeTick(world, Step);
        Require(Known(world).Single(t => t.Id == tree.Id).Remaining == 0,
                "looking again should replace the memory with the current state");
    }
}
