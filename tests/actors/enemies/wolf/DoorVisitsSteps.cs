using System.Collections.Generic;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static CabinSteps;

/// <summary>Steps the checks of wolves at cabin doors share: a player inside a cabin at night, a wolf brought to its door, and time passed in small ticks.</summary>
public static class DoorVisitsSteps {
    /// <summary>A night with the player inside a cabin four metres up from where they stood, its door locked or not, and the sounds played from then on.</summary>
    public static (World World, Thing Hut, List<string> Heard) ShelteredNight(GameData data, bool locked) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        StepInside(world, hut);
        if (locked) world.Doorways.TurnKey();
        return (world, hut, Listen(world));
    }

    /// <summary>How many times a sound was heard.</summary>
    public static int Heard(List<string> heard, string sound) {
        int times = 0;
        foreach (string name in heard) if (name == sound) times++;
        return times;
    }

    /// <summary>Runs a night for a time in ticks of a twentieth of a second, with every player kept fed and watered.</summary>
    public static void VisitTick(World world, float seconds) {
        for (int i = 0; i < Mathf.RoundToInt(seconds / .05f); i++) { NourishEveryone(world); world.Tick(.05f); }
    }

    /// <summary>A wolf out on patrol 3 metres in front of a cabin's door, run until it starts scratching at the door.</summary>
    public static Wolf ScratchingWolf(World world, Thing hut) {
        var wolf = PatrolWolf(world, new Wolf { Pos = Doorways.DoorPoint(hut) + Doorways.OutOfDoor * 3 });
        for (int i = 0; i < 100 && wolf.Visit != WolfVisit.Scratching; i++) { NourishEveryone(world); world.Tick(.05f); }
        Require(wolf.Visit == WolfVisit.Scratching, $"a wolf passing a cabin with someone inside should come to its door and scratch, not {wolf.Visit}");
        return wolf;
    }
}
