using ChopChopMeow;
using Godot;
using static Checks;
using static WorldSteps;

/// <summary>Checks of the sky: the sun and moon.</summary>
public static class SkyChecks {
    public static void Run(GameData data) {
        Test(CheckSunAndMoonFollowTheirArcs, data);
    }

    /// <summary>The sun crosses the sky from dawn to sunset, the moon from sunset to dawn, and the moon's light comes and goes over its cycle.</summary>
    private static void CheckSunAndMoonFollowTheirArcs(GameData data) {
        var sky = EmptyWorld(data);
        float day = data.DaySeconds;
        Require(!sky.MoonUp, "a new game should start with the sun, not the moon, in the sky");
        Require(sky.SkyArc < .15f, "a new game should start just after sunrise");
        Require(sky.Moonlight > .99f, "a new game should start under a full moon");
        RunUntil(sky, day * data.Sunset - .1f);
        Require(!sky.MoonUp && sky.SkyArc > .99f, "the sun should reach the far end of its arc just before sunset");
        RunUntil(sky, day * data.Sunset + .1f);
        Require(sky.MoonUp && sky.SkyArc < .01f, "the moon should start its arc at sunset");
        RunUntil(sky, day - .1f);
        Require(sky.MoonUp && sky.SkyArc > .99f, "the moon should reach the far end of its arc just before dawn");
        RunUntil(sky, day * data.MoonCycleDays / 2);
        Require(sky.Moonlight < .01f, "halfway through its cycle the moon should be new and give no light");
        RunUntil(sky, day * data.MoonCycleDays);
        Require(!sky.MoonUp && sky.SkyArc == 0, "a whole cycle after the first sunrise, the sun should be rising");
        Require(sky.Moonlight > .99f, "the moon should be full again after a whole cycle");
    }
}
