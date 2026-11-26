using System;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The lake in the window.</summary>
    private async Task RunLakeUIChecks() {
        await TestClean(CheckAStickInTheShallowsRestsOnTheLakeBed);
        await TestClean(CheckAPlayerPinnedInTheShallowsLiesOnTheLakeBedWithTheWolfOverThem);
    }

    private async Task CheckAStickInTheShallowsRestsOnTheLakeBed() {
        await CloseWithEsc();
        var home = Hosted.Player;
        var shore = Terrain.WaterPoint(home);
        var inward = (Terrain.Lake - shore).Normalized();
        var stick = Hosted.Add(ThingKind.Stick, shore + inward * 1.5f, 1);
        Warp(Hosted, shore - inward * 2);
        await Until(() => view.PropOf(stick.Id) is { Shown: true }, "the stick in the shallows showing in the window", 5);
        float stickAt = view.PropOf(stick.Id)!.Root.Position.Y;
        Require(stickAt < -.05f, $"A stick in the shallows must rest on the lake bed under the water, not at {stickAt:0.000}.");
        RemoveThings(Hosted, t => t == stick);
        Warp(Hosted, home); await Frames(4);
    }

    private async Task CheckAPlayerPinnedInTheShallowsLiesOnTheLakeBedWithTheWolfOverThem() {
        await CloseWithEsc();
        var home = Hosted.Player;
        var shore = Terrain.WaterPoint(home);
        var inward = (Terrain.Lake - shore).Normalized();
        RunUntilNight(Hosted);
        var dry = await PinnedAt(shore - inward * 3);
        var wading = shore + inward * 1.5f;
        var wet = await PinnedAt(wading);
        float bed = Terrain.BedDepth(wading), wolfBed = Terrain.BedDepth(wet.WolfAt);
        Require(bed > .05f && wolfBed > .05f, $"The spots must lie over the lake bed: {bed:0.000} and {wolfBed:0.000} m down.");
        Require(Math.Abs(dry.Player - wet.Player - bed) < .01f,
            $"A player pinned in the shallows must lie on the lake bed, {bed:0.000} m under the water, not {dry.Player - wet.Player:0.000} m lower than on dry ground.");
        Require(Math.Abs(dry.Wolf - wet.Wolf - wolfBed) < .01f,
            $"A wolf pinning a player in the shallows must stand on the lake bed, {wolfBed:0.000} m under the water, not {dry.Wolf - wet.Wolf:0.000} m lower than on dry ground.");
        RunUntilMorning(Hosted);
        Warp(Hosted, home); await Frames(4);
    }
}
