using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>The diary in checks: a world where gathering turns up pages, and gathering until one turns up.</summary>
internal static class DiarySteps {
    /// <summary>Gathers this many times at most before a check gives up on a find: at one in twenty, more than enough.</summary>
    private const int Tries = 400;

    /// <summary>An empty map where gathering turns up the diary's pages, with a player who carries an axe.</summary>
    public static World DiaryWorld(GameData data) {
        var world = EmptyWorld(data);
        world.Diary.Quiet = false;
        Give(world, "steel_axe");
        return world;
    }

    /// <summary>The player chops a tree until a page falls off it, and gives the page.</summary>
    public static Thing ChopUntilAPageFalls(World world, Thing tree) {
        world.Player = tree.Pos + Vector2.Right;
        for (int tries = 0; tries < Tries && world.ThingsOf(ThingKind.FallenPage).Count == 0; tries++) {
            int before = tree.Remaining;
            world.Gather(tree, world.Data.GatherSeconds);
            Require(tree.Remaining < before, "each chop should take wood off the tree");
        }
        Require(world.ThingsOf(ThingKind.FallenPage).Count == 1, $"a page should fall off a tree within {Tries} chops");
        return world.ThingsOf(ThingKind.FallenPage)[0];
    }

    /// <summary>The player picks berries off a bush until a page turns up in it, waiting in its pop-up, and gives the bush.</summary>
    public static Thing FindAPageInABush(World world) {
        var bush = world.Add(ThingKind.Berries, Terrain.Spawn + new Vector2(10, -10), 10000);
        GatherUntilFound(world, bush, DiaryFind.Berries);
        return bush;
    }

    /// <summary>The player gathers a resource until a page turns up in it.</summary>
    public static void GatherUntilFound(World world, Thing resource, DiaryFind find) {
        world.Player = world.GatherAt(resource, resource.Pos + Vector2.Right);
        for (int tries = 1; tries <= Tries; tries++) {
            world.Gather(resource, world.Data.GatherSeconds);
            if (world.Me.DiaryFinding == find) return;
            Require(world.Me.DiaryFinding == DiaryFind.None, $"gathering a {resource.Kind} should turn up nothing but its own page: {world.Me.DiaryFinding}");
        }
        Require(false, $"a page should turn up in a {resource.Kind} within {Tries} gathers");
    }

    /// <summary>How far from a page a player stands to pick it up, in <see cref="PickUpPage"/>.</summary>
    public const float PageReach = 1;

    /// <summary>
    /// The player walks within reach of a page lying in the world, from the lake's shore for the one floating there, and picks it up,
    /// which brings up its pop-up.
    /// </summary>
    public static void PickUpPage(World world, Thing page) {
        var at = world.GatherAt(page, world.Player);
        var away = page.Kind == ThingKind.FloatingPage ? at - Terrain.Lake : world.Player - at;
        world.Player = at + away.Normalized() * PageReach;
        Require(world.CanStand(world.Player), $"a player should be able to stand within reach of a page: {world.Player}");
        world.Gather(page, 0);
        var find = page.Kind == ThingKind.FloatingPage ? DiaryFind.Lake : DiaryFind.Tree;
        Require(world.Me.DiaryFinding == find, $"picking up a page should bring up its pop-up, {find}: {world.Me.DiaryFinding}");
    }
}
