using ChopChopMeow;
using Godot;
using static WorldSteps;

internal static class LunchSteps {
    public static World WorldAtLunch(GameData data) {
        var world = FundedWorld(data);
        RunUntil(world, (data.LunchHour - 6) / 24 * data.DaySeconds);
        return world;
    }

    /// <summary>A closed ring of cabin walls to separate the crew from open seats on the other side.</summary>
    public static void EncloseLunchCrew(World world, Vector2 center) {
        for (int i = 0; i < 12; i++) world.Add(ThingKind.Hut, center + Vector2.FromAngle(i * Mathf.Tau / 12) * 5);
    }
}
