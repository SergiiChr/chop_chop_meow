using System;
using ChopChopMeow;
using Godot;
using static Checks;
using static WorldSteps;

/// <summary>Steps the cabin checks share: a player stepped inside a cabin or stepping in, and a shore to build a cabin on.</summary>
public static class CabinSteps {
    /// <summary>A night with the player starting to step, at a walk, into a cabin four metres up from where they stood.</summary>
    public static (World World, Thing Hut) SteppingInAtNight(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        Require(world.Doorways.Enter(hut, false), "a player at the door should step in");
        return (world, hut);
    }

    /// <summary>
    /// Ticks until a wolf has a player, the acting one by default, by the waist at a cabin's door, for 0.7 seconds at most:
    /// a wolf at biting distance gathers itself and leaps before its jaws take hold.
    /// </summary>
    public static void UntilTugged(World world, PlayerState? who = null) {
        who ??= world.Me;
        for (float time = 0; time < .7f && who.Door is not (Doorway.Tugged or Doorway.TuggedOff); time += .05f) world.Tick(.05f);
    }

    /// <summary>Puts a player, the acting one by default, at a cabin's door and steps them in at a run, ticking until they are inside.</summary>
    public static void StepInside(World world, Thing hut, PlayerState? who = null) {
        who ??= world.Me;
        world.As(who, () => {
            world.Player = Doorways.DoorPoint(hut);
            Require(world.Doorways.Enter(hut, true), "a player at the cabin's door should start stepping in");
        });
        for (float time = 0; time < 2 && who.Sheltered != hut.Id; time += .05f) world.Tick(.05f);
        Require(who.Sheltered == hut.Id, $"the player should be inside the cabin, not {who.Door}");
    }

    /// <summary>
    /// A spot on dry ground for a cabin whose door has open ground for <paramref name="open"/> metres straight out from it and
    /// none just past that, such as the lake's water: with no open ground, the door itself is in it.
    /// </summary>
    public static Vector2 ShoreForACabin(World world, float open = 0) {
        for (float x = 0; x < 200; x += .5f)
            for (float y = 0; y < 200; y += .5f) {
                var at = new Vector2(x, y);
                var door = Doorways.DoorPoint(new Thing { Pos = at });
                if (!world.CanStand(at) || Terrain.InLake(at, .8f) || world.CanStand(door + Doorways.OutOfDoor * open)) continue;
                bool clear = true;
                for (float ahead = 0; ahead < open - .05f && clear; ahead += .1f) clear = world.CanStand(door + Doorways.OutOfDoor * ahead);
                if (clear) return at;
            }
        throw new InvalidOperationException($"the valley should have a dry spot whose cabin door has {open} m of open ground in front");
    }
}
