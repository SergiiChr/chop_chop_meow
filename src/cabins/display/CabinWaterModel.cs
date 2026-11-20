using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Fits the fixed ladder and mounted barrel to the cabin, with contact points for the worker.</summary>
internal static class CabinWaterModel {
    public const float LadderX = 1.58f, LadderWidth = .48f;
    public static readonly float LadderZ = BarrelJobs.LadderApproach.Y;
    public const float FirstRung = .24f, RungSpacing = .28f;
    public const int RungCount = 10;
    public static readonly Vector3 LadderApproach = new(BarrelJobs.LadderApproach.X, 0, LadderZ);
    public static readonly Vector3 RoofEntry = new(1.18f, CabinShape.RoofSurface(LadderZ), LadderZ);
    public static readonly Vector3 PourStand = new(.25f, CabinShape.RoofSurface(.62f), .62f);
    public static readonly Vector3 FillOpening = new(-.31f, 3.30f, .62f);
    private static readonly Vector3 BarrelCenter = new(-.54f, 3.09f, .62f);
    private const float FootLength = .19f, FootWidth = .17f;
    private const float NeckHeight = .061f, NeckOuter = .064f, NeckInner = .043f;
    private static readonly TorusMesh Neck = new() { InnerRadius = NeckInner, OuterRadius = NeckOuter, Rings = 12, RingSegments = 6 };
    private static readonly CylinderMesh Collar = new() {
        BottomRadius = .058f,
        TopRadius = .058f,
        Height = NeckHeight,
        RadialSegments = 12,
        Rings = 0,
        CapTop = false,
        CapBottom = false,
    };
    private static readonly CylinderMesh Rung = new() {
        BottomRadius = .019f,
        TopRadius = .019f,
        Height = LadderWidth,
        RadialSegments = 6,
        Rings = 0,
        CapTop = false,
        CapBottom = false,
    };

    public static void AddLadder(Node3D parent) {
        var rail = Metal("526069", .59f, .41f);
        var rung = Metal("909a9d", .57f, .4f);
        for (int side = -1; side <= 1; side += 2) {
            float z = LadderZ + side * LadderWidth / 2;
            float top = CabinShape.RoofSurface(z) + .41f;
            Box(parent, new Vector3(.055f, top - .08f, .045f), new Vector3(LadderX, (top + .08f) / 2, z), rail);
            // The grab rails turn in over the roof.
            // Brackets carry the ladder back to the wall.
            Box(parent, new Vector3(.19f, .045f, .045f), new Vector3(LadderX - .0675f, top, z), rail);
            foreach (float y in new[] { .49f, 1.4f, 2.5f }) {
                Box(parent, new Vector3(.39f, .043f, .045f), new Vector3(1.391f, y, z), rail);
                Box(parent, new Vector3(.026f, .11f, .089f), new Vector3(1.21f, y, z), rail);
            }
        }
        for (int i = 0; i < RungCount; i++)
            Add(parent, Rung, rung, new Vector3(LadderX, FirstRung + i * RungSpacing, LadderZ), new Vector3(90, 0, 0));
    }

    public static Node3D AddMountedBarrel(Node3D parent) {
        var mount = Pivot(parent, Vector3.Zero);
        var bracket = Metal("5b666c", .56f, .57f);
        var foot = Metal("858f93", .55f, .5f);
        foreach (int along in new[] { -1, 1 }) {
            float x = BarrelCenter.X + along * FootLength;
            foreach (int across in new[] { -1, 1 }) {
                float z = BarrelCenter.Z + across * FootWidth;
                float from = CabinShape.RoofSurface(z) - .016f;
                var bottom = new Vector3(x, from, z);
                var top = new Vector3(x, BarrelCenter.Y - .09f, BarrelCenter.Z + across * .142f);
                var leg = Box(mount, new Vector3(.066f, top.DistanceTo(bottom), .057f), (bottom + top) / 2, bracket);
                leg.Rotation = new Vector3(MathF.Atan2(top.Z - z, top.Y - from), 0, 0);
                Box(mount, new Vector3(.13f, .026f, .11f), bottom, foot, new Vector3(CabinShape.RoofPitchDegrees, 0, 0));
            }
        }
        // The upright barrel's lid faces left once the same shell is set on its side.
        Add(mount, VesselShape.BarrelBody, VesselShape.Finish, BarrelCenter + Vector3.Right * (VesselShape.BarrelHeight / 2),
            new Vector3(0, 0, 90));
        // A short hollow fill neck opens straight up near the worker's side of the barrel.
        Add(mount, Neck, foot, FillOpening - Vector3.Up * ((NeckOuter - NeckInner) / 2));
        Add(mount, Collar, bracket, FillOpening - Vector3.Up * (NeckHeight / 2 + .014f));
        return mount;
    }
}
