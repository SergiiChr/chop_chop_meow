using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>A vertical hit cylinder in model space; picking and canopy fading use the same volumes.</summary>
public readonly record struct Hit(float Radius, float Bottom, float Top);

/// <summary>A built world object plus the moving parts that <see cref="WorldView"/> animates from simulation state.</summary>
public sealed class Prop
{
    public readonly Node3D Root = new();
    public readonly Node3D Body;
    public Node3D? Sway, Stump, Fruit, Lid, Door, Lever, Needle, Flag, Fire, Chisel, Hammer;
    public Node3D[] Pleats = Array.Empty<Node3D>();
    public MeshInstance3D? Glow, Window, Pool;
    public WorldModels.WolfModel? Wolf;
    public OmniLight3D? Light;
    public Vector3 Vent;
    public Hit[] Hits = Array.Empty<Hit>();
    public string Kind = "";
    public float Fade, Fall = -1, Open, Shake, Puff;
    public int LastCount, Tint = -1;
    public bool Seen;
    public Prop() { Body = Pivot(Root, Vector3.Zero); }
}

/// <summary>Code-built low-poly models for every world object: natural, modern and DIY rather than fantasy.</summary>
public static class WorldModels
{
    public const int TreeVariants = 3;
    /// <summary>Full deposit sizes from world generation; piles shrink toward half size as they are worked.</summary>
    public static float Fullness(Thing t) => t.Kind switch
    {
        "stone" => t.Remaining / 64f,
        "iron" or "coal" => t.Remaining / 400f,
        "wolf_corpse" => t.Remaining / 4f,
        _ => 1
    };

    /// <summary>Stable pseudo-random value in [0, 1) so a thing looks the same after every load.</summary>
    public static float Jitter(int id, int salt)
    {
        uint h = (uint)(id * 73856093) ^ (uint)(salt * 19349663);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return (h & 0xffff) / 65536f;
    }

    public static Hit[] Hits(Thing t) => t.Kind switch
    {
        "tree" => (t.Id % TreeVariants) switch
        {
            1 => new[] { new Hit(.3f, 0, 1.2f), new Hit(1.25f, 1.2f, 5.6f) },
            2 => new[] { new Hit(.25f, 0, 2.6f), new Hit(1.05f, 2.6f, 5.4f) },
            _ => new[] { new Hit(.35f, 0, 2.5f), new Hit(1.45f, 2.5f, 5.3f) },
        },
        "stone" => new[] { new Hit(.75f, 0, .9f) },
        "iron" or "coal" => new[] { new Hit(.8f, 0, 1) },
        "loose_iron" or "loose_coal" => new[] { new Hit(.45f, 0, .35f) },
        "wolf_corpse" => new[] { new Hit(.65f, 0, .5f) },
        "berries" => new[] { new Hit(.65f, 0, 1) },
        "water" => new[] { new Hit(1.2f, 0, .5f) },
        "furnace" => new[] { new Hit(.72f, 0, 1.15f) },
        "reinforced" => new[] { new Hit(.95f, 0, 1.45f) },
        "hut" => new[] { new Hit(1.6f, 0, 3.1f) },
        "chest" => new[] { new Hit(.5f, 0, .65f) },
        "crate" => new[] { new Hit(.4f, 0, .55f) },
        "bench" => new[] { new Hit(.6f, 0, .95f) },
        "grave" => new[] { new Hit(.45f, 0, .55f) },
        "anvil" => new[] { new Hit(.45f, 0, .85f) },
        "signpost" => new[] { new Hit(.5f, 0, 1.9f) },
        "campfire" => new[] { new Hit(.55f, 0, .7f) },
        _ => new[] { new Hit(.5f, 0, .8f) },
    };

    public static Prop Build(Thing t)
    {
        var prop = new Prop { Kind = t.Kind, Hits = Hits(t) };
        var body = prop.Body;
        switch (t.Kind)
        {
            case "tree": Tree(prop, t.Id); break;
            case "stone": Rocks(body, t.Id, new[] { "8d918e", "a3a7a3", "7c807d" }, null); break;
            case "iron": Rocks(body, t.Id, new[] { "7d5b45", "8f6a50", "6e4f3c" }, "b4622a"); break;
            case "coal": Rocks(body, t.Id, new[] { "2d2f33", "3a3c40", "26282b" }, "15161a"); break;
            case "loose_iron": LooseOre(body, t.Id, "a35a2e", "8b4f2b"); break;
            case "loose_coal": LooseOre(body, t.Id, "2d2f33", "1d1e21"); break;
            case "wolf_corpse": Carcass(prop); break;
            case "berries": Bush(prop, t.Id); break;
            case "water": break; // The pond is the drinking spot; this thing only provides the hit area.
            case "furnace": BlockFurnace(prop); break;
            case "reinforced": DrumForge(prop); break;
            case "hut": Cabin(prop); break;
            case "chest": StorageBox(prop, "c9a46e", "9d7a4c", "2f3133"); break;
            case "grave": StorageBox(prop, "e2701e", "c45b12", "f2f2ea"); break;
            case "anvil": Anvil(prop); break;
            case "crate": Crate(prop); break;
            case "bench": Workbench(prop); break;
            case "signpost": Signpost(prop); break;
            case "campfire": FirePit(prop); break;
        }
        if (t.Kind != "water") body.RotationDegrees = new Vector3(0, t.Kind == "tree" || t.IsResource ? Jitter(t.Id, 1) * 360 : 0, 0);
        return prop;
    }

    private static void Tree(Prop prop, int id)
    {
        var sway = Pivot(prop.Body, Vector3.Zero);
        prop.Sway = sway; // the whole tree shakes and falls about its base
        float scale = .9f + Jitter(id, 2) * .22f;
        sway.Scale = Vector3.One * scale;
        switch (id % TreeVariants)
        {
            case 1: // Norway spruce: stacked dark cones on a short trunk
                Post(sway, .24f, .12f, 1.6f, Vector3.Zero, Mat("5b3f2a"));
                (float Radius, float Height, float Y, string Color)[] tiers = { (1.45f, 2.1f, 1.1f, "2f5a3a"), (1.15f, 1.8f, 2.3f, "35643f"), (.85f, 1.5f, 3.4f, "2f5a3a"), (.5f, 1.2f, 4.4f, "3a6b44") };
                foreach (var tier in tiers) Post(sway, tier.Radius, 0, tier.Height, new Vector3(0, tier.Y, 0), Mat(tier.Color), 9);
                break;
            case 2: // Silver birch: slender pale trunk with an airy crown
                Post(sway, .16f, .09f, 4.4f, Vector3.Zero, Mat("e6e2d6", .9f));
                for (int i = 0; i < 6; i++) Box(sway, new Vector3(.12f, .04f, .02f), new Vector3(0, .5f + i * .55f + Jitter(id, 10 + i) * .2f, .14f - i * .004f), Mat("2b2b2b"), new Vector3(0, i * 57, 0));
                Box(sway, new Vector3(.05f, .9f, .05f), new Vector3(.3f, 3.2f, 0), Mat("d8d2c4"), new Vector3(0, 0, -35));
                (Vector3 At, float Radius)[] leaves = { (new(0, 3.4f, 0), .95f), (new(.6f, 3f, .3f), .7f), (new(-.5f, 3.1f, -.3f), .72f), (new(.2f, 4.2f, -.2f), .8f), (new(-.3f, 4f, .5f), .65f), (new(.5f, 3.8f, -.5f), .6f) };
                for (int i = 0; i < leaves.Length; i++) Sphere(sway, leaves[i].Radius, leaves[i].At, Mat(i % 2 == 0 ? "7a9a42" : "6d8c3c"), new Vector3(1, .85f, 1), 8);
                break;
            default: // Broadleaf oak: thick trunk, two limbs and a broad clustered crown
                Post(sway, .34f, .2f, 2.7f, Vector3.Zero, Mat("6b4a32"));
                Cylinder(sway, .12f, .07f, 1.1f, new Vector3(.35f, 2.4f, 0), Mat("6b4a32"), new Vector3(0, 0, -40), 6);
                Cylinder(sway, .11f, .07f, 1f, new Vector3(-.3f, 2.5f, .1f), Mat("6b4a32"), new Vector3(20, 0, 38), 6);
                (Vector3 At, float Radius, string Color)[] crown = { (new(0, 3.9f, 0), 1.35f, "4a6f2f"), (new(.85f, 3.5f, .2f), 1f, "426528"), (new(-.8f, 3.6f, -.2f), 1.05f, "527d35"), (new(.1f, 3.4f, .85f), .95f, "476b2c"), (new(-.1f, 3.5f, -.85f), .95f, "426528"), (new(.2f, 4.6f, -.1f), .9f, "5a8838") };
                foreach (var c in crown) Sphere(sway, c.Radius, c.At, Mat(c.Color), new Vector3(1, .82f, 1), 10);
                break;
        }
        var stump = Pivot(prop.Root, Vector3.Zero);
        Post(stump, .3f, .26f, .32f, Vector3.Zero, Mat("6b4a32"));
        Post(stump, .25f, .25f, .02f, new Vector3(0, .32f, 0), Mat("c9a477"));
        stump.Visible = false;
        prop.Stump = stump;
    }

    private static void Rocks(Node3D body, int id, string[] colors, string? veins)
    {
        var pile = Pivot(body, Vector3.Zero);
        (Vector3 At, float Radius, Vector3 Scale)[] rocks = { (new(0, .35f, 0), .55f, new(1.1f, .95f, 1)), (new(.5f, .2f, .25f), .36f, new(1, .8f, 1.1f)), (new(-.45f, .2f, .3f), .32f, new(1.2f, .75f, 1)), (new(-.15f, .15f, -.5f), .3f, new(1, .7f, 1.2f)), (new(.35f, .12f, -.4f), .22f, Vector3.One) };
        for (int i = 0; i < rocks.Length; i++)
        {
            var rock = Sphere(pile, rocks[i].Radius, rocks[i].At, Mat(colors[i % colors.Length], .9f), rocks[i].Scale, 6);
            rock.RotationDegrees = new Vector3(Jitter(id, 20 + i) * 30, Jitter(id, 30 + i) * 360, 0);
        }
        if (veins == null) return;
        for (int i = 0; i < 7; i++)
        {
            float angle = i * 1.7f + Jitter(id, 40 + i);
            var at = new Vector3(MathF.Cos(angle) * .45f, .25f + Jitter(id, 50 + i) * .45f, MathF.Sin(angle) * .45f);
            Box(pile, new Vector3(.1f, .08f, .1f), at, Mat(veins, .5f, veins == "15161a" ? .3f : 0), new Vector3(30, angle * 50, 20));
        }
    }

    private static void LooseOre(Node3D body, int id, string light, string dark)
    {
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 1.6f + Jitter(id, 60 + i);
            var at = new Vector3(MathF.Cos(angle) * .22f, .08f, MathF.Sin(angle) * .22f);
            Sphere(body, .13f + Jitter(id, 70 + i) * .06f, at, Mat(i % 2 == 0 ? light : dark, .95f), new Vector3(1, .7f, 1.1f), 6);
        }
    }

    private static void Carcass(Prop prop)
    {
        prop.Pool = Cylinder(prop.Body, .55f, .55f, .004f, new Vector3(.1f, .006f, 0), Flat(new Color(.36f, .05f, .05f, .75f), true), null, 14);
        prop.Wolf = new WolfModel();
        prop.Body.AddChild(prop.Wolf.Root);
    }

    private static void Bush(Prop prop, int id)
    {
        (Vector3 At, float Radius)[] clumps = { (new(0, .5f, 0), .5f), (new(.38f, .35f, .15f), .38f), (new(-.35f, .38f, .1f), .4f), (new(.05f, .35f, .4f), .36f), (new(0, .4f, -.38f), .38f) };
        foreach (var c in clumps) Sphere(prop.Body, c.Radius, c.At, Mat("4c7a33"), new Vector3(1, .9f, 1), 8);
        var fruit = Pivot(prop.Body, Vector3.Zero);
        for (int i = 0; i < 16; i++)
        {
            float angle = i * 2.4f, lift = .25f + Jitter(id, 80 + i) * .55f;
            float reach = .5f - Math.Abs(lift - .5f) * .5f;
            Sphere(fruit, .055f, new Vector3(MathF.Cos(angle) * reach, lift, MathF.Sin(angle) * reach), Mat("c0283a", .4f), null, 6);
        }
        prop.Fruit = fruit;
    }

    /// <summary>Cinder-block furnace with a steel top plate, stovepipe and a firewood stack.</summary>
    private static void BlockFurnace(Prop prop)
    {
        var body = prop.Body;
        Box(body, new Vector3(1f, .8f, .9f), new Vector3(0, .4f, 0), Mat("9b9c98", .95f));
        // Mortar joints on the two faces the camera sees give the stacked-block look.
        var joint = Mat("7b7c78", 1);
        for (int row = 1; row < 4; row++)
        {
            Box(body, new Vector3(1.01f, .015f, .01f), new Vector3(0, row * .2f, .451f), joint);
            Box(body, new Vector3(.01f, .015f, .91f), new Vector3(.501f, row * .2f, 0), joint);
        }
        for (int row = 0; row < 4; row++)
            for (int i = 0; i < 3; i++)
            {
                float offset = row % 2 == 0 ? -.2f : 0;
                Box(body, new Vector3(.01f, .19f, .01f), new Vector3(-.2f + i * .4f + offset, row * .2f + .1f, .451f), joint);
                Box(body, new Vector3(.01f, .19f, .01f), new Vector3(.501f, row * .2f + .1f, -.25f + i * .4f + offset), joint);
            }
        Box(body, new Vector3(.44f, .38f, .03f), new Vector3(0, .21f, .45f), Mat("2b2522"));
        prop.Glow = Box(body, new Vector3(.34f, .26f, .03f), new Vector3(0, .17f, .46f), Flat(new Color("ff7a24")));
        Box(body, new Vector3(1.14f, .05f, 1.0f), new Vector3(0, .82f, 0), Mat("45494c", .5f, .6f));
        Post(body, .09f, .09f, 1.05f, new Vector3(-.3f, .84f, -.25f), Mat("8e9396", .45f, .7f));
        Post(body, .16f, 0, .14f, new Vector3(-.3f, 1.92f, -.25f), Mat("8e9396", .45f, .7f), 8);
        prop.Vent = new Vector3(-.3f, 2.05f, -.25f);
        for (int i = 0; i < 3; i++)
            Cylinder(body, .07f, .07f, .5f, new Vector3(.82f, .08f + (i == 2 ? .13f : 0), -.1f + (i == 2 ? .07f : i * .14f)), Mat(i == 1 ? "7a5235" : "8a5f3c"), new Vector3(90, 0, 0), 7);
        prop.Light = Light(body, new Vector3(0, .35f, .7f), 2.6f);
    }

    /// <summary>A propane-tank forge on an angle-iron stand, fed by a hinged leather bellows with a heat gauge on top.</summary>
    private static void DrumForge(Prop prop)
    {
        var body = prop.Body;
        var steel = Mat("3b3f42", .5f, .7f);
        foreach (var (x, z) in new[] { (-.35f, -.3f), (.35f, -.3f), (-.35f, .3f), (.35f, .3f) }) Box(body, new Vector3(.05f, .5f, .05f), new Vector3(x, .25f, z), steel);
        Box(body, new Vector3(.8f, .04f, .7f), new Vector3(0, .5f, 0), steel);
        var tank = Mat("4d6a7a", .45f, .55f);
        Cylinder(body, .38f, .38f, .9f, new Vector3(0, .9f, 0), tank, new Vector3(90, 0, 0), 14);
        Sphere(body, .38f, new Vector3(0, .9f, -.45f), tank, new Vector3(1, 1, .35f), 14);
        Torus(body, .37f, .42f, new Vector3(0, .9f, .2f), steel).RotationDegrees = new Vector3(90, 0, 0);
        Torus(body, .37f, .42f, new Vector3(0, .9f, -.25f), steel).RotationDegrees = new Vector3(90, 0, 0);
        Cylinder(body, .3f, .3f, .04f, new Vector3(0, .9f, .45f), Mat("d9c7a3", .95f), new Vector3(90, 0, 0), 14);
        prop.Glow = Cylinder(body, .17f, .17f, .05f, new Vector3(0, .9f, .46f), Flat(new Color("ff7a24")), new Vector3(90, 0, 0), 12);
        Post(body, .07f, .07f, .65f, new Vector3(0, 1.22f, -.2f), Mat("8e9396", .45f, .7f));
        prop.Vent = new Vector3(0, 1.95f, -.2f);
        // Heat gauge: the needle sweeps from cold to the 80° steel mark.
        Post(body, .03f, .03f, .18f, new Vector3(.2f, 1.25f, .2f), steel);
        Cylinder(body, .1f, .1f, .03f, new Vector3(.2f, 1.48f, .2f), Mat("f2efe6", .6f), new Vector3(90, 0, 0), 14);
        float mark = Mathf.DegToRad(Gauge(.8f));
        Box(body, new Vector3(.03f, .05f, .01f), new Vector3(.2f - MathF.Sin(mark) * .075f, 1.48f + MathF.Cos(mark) * .075f, .217f), Mat("2f8f4f"), new Vector3(0, 0, Gauge(.8f)));
        var needle = Pivot(body, new Vector3(.2f, 1.48f, .22f));
        Box(needle, new Vector3(.012f, .085f, .01f), new Vector3(0, .04f, 0), Mat("c62828"));
        prop.Needle = needle;
        // A classic hinged bellows behind the forge, built along the rig's +X, which points to the back of the tank.
        // Teardrop boards hinge at the nozzle and are joined by leather pleats.
        var rig = Pivot(body, Vector3.Zero, new Vector3(0, 90, 0));
        Post(rig, .05f, .05f, .5f, new Vector3(.5f, .28f, 0), steel, 8);
        var hinge = new Vector3(.66f, .26f, 0);
        var wood = Mat("8a5f3c");
        var leather = Mat("3b2619", .9f);
        foreach (float x in new[] { .95f, 1.4f }) Box(rig, new Vector3(.08f, .2f, .5f), new Vector3(x, .1f, 0), Mat("6f5a40"));
        Paddle(rig, hinge + new Vector3(0, -.03f, 0), 1, wood);
        Cylinder(rig, .07f, .025f, .22f, new Vector3(.55f, .3f, 0), Mat("b8893a", .4f, .8f), new Vector3(0, 0, 90), 10);
        var lever = Pivot(rig, hinge);
        Paddle(lever, new Vector3(0, .13f, 0), 1, wood);
        Box(lever, new Vector3(.06f, .62f, .06f), new Vector3(1f, .44f, 0), Mat("8a5a32"));
        Cylinder(lever, .03f, .03f, .34f, new Vector3(1f, .75f, 0), Mat("222222"), new Vector3(90, 0, 0), 8);
        prop.Lever = lever;
        // Each pleat hangs from the hinge and follows a share of the lid's swing, so the leather folds as it closes.
        prop.Pleats = new Node3D[3];
        for (int i = 0; i < 3; i++)
        {
            prop.Pleats[i] = Pivot(rig, hinge);
            Paddle(prop.Pleats[i], new Vector3(0, -.03f + (i + 1) * .04f, 0), i % 2 == 0 ? .97f : .88f, leather);
        }
        prop.Light = Light(body, new Vector3(0, .9f, .9f), 3);
    }

    /// <summary>Small OSB cabin on block piers with a corrugated shed roof, solar panel and rain barrel.</summary>
    private static void Cabin(Prop prop)
    {
        var body = prop.Body;
        foreach (var (x, z) in new[] { (-1.1f, -1f), (1.1f, -1f), (-1.1f, 1f), (1.1f, 1f) }) Box(body, new Vector3(.3f, .3f, .3f), new Vector3(x, .15f, z), Mat("9a9b97"));
        Box(body, new Vector3(2.6f, .12f, 2.4f), new Vector3(0, .36f, 0), Mat("7d5f43"));
        var osb = Mat("c8a46a", .95f);
        Box(body, new Vector3(2.4f, 2.1f, 2.1f), new Vector3(0, 1.47f, -.05f), osb);
        var trim = Mat("3d4245");
        foreach (var (x, z) in new[] { (-1.2f, -1.1f), (1.2f, -1.1f), (-1.2f, 1f), (1.2f, 1f) }) Box(body, new Vector3(.08f, 2.2f, .08f), new Vector3(x, 1.47f, z), trim);
        for (int i = 0; i < 4; i++) Box(body, new Vector3(.04f, 2.1f, .02f), new Vector3(-.95f + i * .6f + (i > 1 ? .3f : 0), 1.47f, 1.01f), Mat("b08c55"));
        for (int i = 0; i < 4; i++) Box(body, new Vector3(.02f, 2.1f, .04f), new Vector3(1.21f, 1.47f, -.8f + i * .55f), Mat("b08c55"));
        // Shed roof pitched toward the back, overhanging the door.
        var roof = Pivot(body, new Vector3(0, 2.68f, -.05f), new Vector3(-9, 0, 0));
        Box(roof, new Vector3(2.9f, .06f, 2.8f), Vector3.Zero, Mat("6f7a80", .45f, .6f));
        for (int i = 0; i < 10; i++) Cylinder(roof, .03f, .03f, 2.8f, new Vector3(-1.35f + i * .3f, .04f, 0), Mat("7d878d", .45f, .6f), new Vector3(90, 0, 0), 6);
        Box(roof, new Vector3(1.1f, .05f, .8f), new Vector3(.55f, .1f, -.4f), Mat("1f2c44", .25f, .4f));
        Box(roof, new Vector3(1.14f, .03f, .84f), new Vector3(.55f, .08f, -.4f), Mat("c9ced2", .4f, .7f));
        Post(body, .08f, .08f, 1.1f, new Vector3(-.75f, 2.4f, -.6f), Mat("8e9396", .45f, .7f));
        Post(body, .14f, 0, .12f, new Vector3(-.75f, 3.48f, -.6f), Mat("8e9396", .45f, .7f), 8);
        prop.Vent = new Vector3(-.75f, 3.6f, -.6f);
        // Door hinged on its left edge, painted teal.
        var door = Pivot(body, new Vector3(-.75f, .42f, 1.02f));
        Box(door, new Vector3(.82f, 1.86f, .05f), new Vector3(.41f, .93f, 0), Mat("3e6e6a", .6f));
        Sphere(door, .035f, new Vector3(.72f, .95f, .05f), Mat("c9ced2", .3f, .8f));
        prop.Door = door;
        Box(body, new Vector3(1, .14f, .4f), new Vector3(-.34f, .21f, 1.35f), Mat("a9824f"));
        var glass = Mat("8fb4c4", .15f, .2f);
        Box(body, new Vector3(.7f, .6f, .06f), new Vector3(.55f, 1.65f, 1.0f), trim);
        prop.Window = Box(body, new Vector3(.6f, .5f, .07f), new Vector3(.55f, 1.65f, 1.0f), glass);
        Box(body, new Vector3(.06f, .7f, .9f), new Vector3(1.2f, 1.6f, 0), trim);
        Box(body, new Vector3(.07f, .6f, .8f), new Vector3(1.2f, 1.6f, 0), glass);
        Post(body, .26f, .26f, .8f, new Vector3(1.5f, 0, .6f), Mat("2c5f9e", .5f));
        Cylinder(body, .27f, .27f, .04f, new Vector3(1.5f, .62f, .6f), Mat("244f85", .5f), null, 12);
        prop.Light = Light(body, new Vector3(.55f, 1.65f, 1.4f), 2.5f);
    }

    private static void StorageBox(Prop prop, string panel, string edge, string fittings)
    {
        var body = prop.Body;
        bool tote = panel == "e2701e";
        var size = tote ? new Vector3(.75f, .36f, .5f) : new Vector3(.85f, .45f, .55f);
        Box(body, size, new Vector3(0, size.Y / 2 + .04f, 0), Mat(panel, tote ? .5f : .9f));
        if (!tote)
        {
            foreach (var (x, z) in new[] { (-.4f, -.25f), (.4f, -.25f), (-.4f, .25f), (.4f, .25f) }) Box(body, new Vector3(.07f, .5f, .07f), new Vector3(x, .27f, z), Mat(fittings, .5f, .6f));
            Box(body, new Vector3(.87f, .06f, .57f), new Vector3(0, .07f, 0), Mat(edge));
            Box(body, new Vector3(.2f, .05f, .04f), new Vector3(.25f, .3f, .29f), Mat(fittings, .5f, .6f));
            Box(body, new Vector3(.2f, .05f, .04f), new Vector3(-.25f, .3f, .29f), Mat(fittings, .5f, .6f));
        }
        else Box(body, new Vector3(.77f, .05f, .52f), new Vector3(0, .25f, 0), Mat(fittings, .4f));
        var lid = Pivot(body, new Vector3(0, size.Y + .04f, -size.Z / 2));
        Box(lid, new Vector3(size.X + .04f, .06f, size.Z + .04f), new Vector3(0, .03f, size.Z / 2), Mat(tote ? edge : edge, tote ? .5f : .85f));
        Box(lid, new Vector3(.12f, .04f, .03f), new Vector3(0, .02f, size.Z + .03f), Mat(fittings, .5f, .6f));
        prop.Lid = lid;
    }

    /// <summary>A slatted wood crate with darker corner battens and a slatted lid.</summary>
    private static void Crate(Prop prop)
    {
        var body = prop.Body;
        var size = new Vector3(.6f, .42f, .46f);
        var board = Mat("c9a46e"); var batten = Mat("8f6a3f"); var gap = Mat("4a3420");
        Box(body, size - new Vector3(.02f, .02f, .02f), new Vector3(0, size.Y / 2, 0), gap);
        // Three slats a side leave dark gaps between them.
        for (int i = 0; i < 3; i++)
        {
            float y = .07f + i * .14f;
            Box(body, new Vector3(size.X, .11f, .02f), new Vector3(0, y, size.Z / 2), board);
            Box(body, new Vector3(size.X, .11f, .02f), new Vector3(0, y, -size.Z / 2), board);
            Box(body, new Vector3(.02f, .11f, size.Z), new Vector3(size.X / 2, y, 0), board);
            Box(body, new Vector3(.02f, .11f, size.Z), new Vector3(-size.X / 2, y, 0), board);
        }
        foreach (var (x, z) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }) Box(body, new Vector3(.05f, size.Y, .05f), new Vector3(x * (size.X / 2 - .01f), size.Y / 2, z * (size.Z / 2 - .01f)), batten);
        var lid = Pivot(body, new Vector3(0, size.Y, -size.Z / 2));
        for (int i = 0; i < 3; i++) Box(lid, new Vector3(size.X + .03f, .035f, size.Z / 3 - .015f), new Vector3(0, .018f, size.Z / 6 + i * size.Z / 3), board);
        Box(lid, new Vector3(.05f, .045f, size.Z + .02f), new Vector3(size.X / 2 - .05f, .03f, size.Z / 2), batten);
        Box(lid, new Vector3(.05f, .045f, size.Z + .02f), new Vector3(-size.X / 2 + .05f, .03f, size.Z / 2), batten);
        prop.Lid = lid;
    }

    /// <summary>A plank-topped workbench with a lower shelf and a vise; slotted tools lie on the top.</summary>
    private static void Workbench(Prop prop)
    {
        var body = prop.Body;
        var wood = Mat("b98552"); var dark = Mat("7a5432"); var steel = Mat("4a4d50", .4f, .6f);
        foreach (var (x, z) in new[] { (-.5f, -.25f), (.5f, -.25f), (-.5f, .25f), (.5f, .25f) }) Box(body, new Vector3(.08f, .74f, .08f), new Vector3(x, .37f, z), dark);
        Box(body, new Vector3(1.1f, .07f, .62f), new Vector3(0, .78f, 0), wood);
        for (int i = 0; i < 3; i++) Box(body, new Vector3(1.11f, .005f, .01f), new Vector3(0, .816f, -.2f + i * .2f), dark);
        Box(body, new Vector3(1f, .04f, .5f), new Vector3(0, .2f, 0), dark);
        Box(body, new Vector3(.3f, .12f, .2f), new Vector3(.62f, .78f, .2f), steel);
        Box(body, new Vector3(.04f, .04f, .26f), new Vector3(.62f, .8f, .4f), steel);
        var chisel = Pivot(body, new Vector3(-.25f, .83f, .05f), new Vector3(0, 30, 0));
        Box(chisel, new Vector3(.24f, .03f, .06f), Vector3.Zero, Mat("9aa29f"));
        prop.Chisel = chisel;
        var hammer = Pivot(body, new Vector3(.15f, .83f, -.1f), new Vector3(0, -20, 0));
        Box(hammer, new Vector3(.34f, .03f, .035f), Vector3.Zero, wood);
        Box(hammer, new Vector3(.06f, .06f, .14f), new Vector3(.17f, .02f, 0), steel);
        prop.Hammer = hammer;
    }

    /// <summary>Anvil bolted to a welded square-tube stand.</summary>
    private static void Anvil(Prop prop)
    {
        var body = prop.Body;
        var tube = Mat("5d6a4f", .6f, .4f);
        foreach (var (x, z) in new[] { (-.2f, -.15f), (.2f, -.15f), (-.2f, .15f), (.2f, .15f) }) Box(body, new Vector3(.06f, .46f, .06f), new Vector3(x * 1.1f, .23f, z * 1.1f), tube);
        Box(body, new Vector3(.52f, .05f, .4f), new Vector3(0, .47f, 0), tube);
        Box(body, new Vector3(.5f, .04f, .05f), new Vector3(0, .12f, .17f), tube);
        var iron = Mat("3b3e41", .45f, .7f);
        Box(body, new Vector3(.34f, .1f, .24f), new Vector3(0, .55f, 0), iron);
        Box(body, new Vector3(.18f, .12f, .14f), new Vector3(0, .66f, 0), iron);
        Box(body, new Vector3(.46f, .12f, .2f), new Vector3(-.03f, .78f, 0), iron);
        Cylinder(body, .075f, 0, .26f, new Vector3(.33f, .79f, 0), iron, new Vector3(0, 0, -90), 8);
        prop.Vent = new Vector3(0, .86f, 0);
    }

    /// <summary>A 4x4 post with a painted market sign and a mailbox whose flag rises while an order is out.</summary>
    private static void Signpost(Prop prop)
    {
        var body = prop.Body;
        Box(body, new Vector3(.12f, 1.95f, .12f), new Vector3(0, .975f, 0), Mat("8a6a48"));
        Box(body, new Vector3(.95f, .42f, .04f), new Vector3(.3f, 1.55f, .08f), Mat("2f5d4a", .7f));
        Box(body, new Vector3(.55f, .08f, .01f), new Vector3(.25f, 1.55f, .105f), Mat("f2efe6", .6f));
        Cylinder(body, .1f, 0, .16f, new Vector3(.6f, 1.55f, .105f), Mat("f2efe6", .6f), new Vector3(0, 0, -90), 3);
        Box(body, new Vector3(.45f, .04f, .06f), new Vector3(-.2f, .95f, 0), Mat("8a6a48"));
        Box(body, new Vector3(.24f, .22f, .38f), new Vector3(-.36f, 1.08f, 0), Mat("2b2d30", .5f, .5f));
        var flag = Pivot(body, new Vector3(-.49f, 1.0f, .1f));
        Box(flag, new Vector3(.02f, .2f, .04f), new Vector3(0, .1f, 0), Mat("c62828", .5f));
        Box(flag, new Vector3(.02f, .06f, .1f), new Vector3(0, .18f, .05f), Mat("c62828", .5f));
        prop.Flag = flag;
        Box(body, new Vector3(.3f, .06f, .3f), new Vector3(0, .03f, 0), Mat("9a9b97"));
    }

    /// <summary>Fire pit made from an old steel wheel rim, with a log teepee and flickering flames.</summary>
    private static void FirePit(Prop prop)
    {
        var body = prop.Body;
        var rim = Mat("3d3f42", .55f, .6f);
        Cylinder(body, .46f, .44f, .22f, new Vector3(0, .11f, 0), rim, null, 16);
        Torus(body, .42f, .5f, new Vector3(0, .22f, 0), rim);
        Cylinder(body, .4f, .4f, .02f, new Vector3(0, .23f, 0), Mat("4d4440", 1), null, 14);
        for (int i = 0; i < 4; i++)
        {
            var log = Pivot(body, new Vector3(0, .25f, 0), new Vector3(0, i * 90 + 20, 0));
            Cylinder(log, .05f, .045f, .55f, new Vector3(0, .17f, .14f), Mat(i % 2 == 0 ? "6e4a2e" : "5d3f28"), new Vector3(-28, 0, 0), 6);
        }
        var fire = Pivot(body, new Vector3(0, .26f, 0));
        Post(fire, .2f, 0, .55f, Vector3.Zero, Flat(new Color("ff8a2a", .9f)), 7);
        Post(fire, .13f, 0, .42f, new Vector3(.07f, 0, .05f), Flat(new Color("ffc24a", .95f)), 7);
        Post(fire, .1f, 0, .32f, new Vector3(-.08f, 0, -.04f), Flat(new Color("ffe08a", .95f)), 7);
        prop.Fire = fire;
        prop.Vent = new Vector3(0, .8f, 0);
        prop.Light = Light(body, new Vector3(0, .8f, 0), 7);
    }

    /// <summary>A flat teardrop bellows board pointing its narrow end at <paramref name="tip"/> and widening toward +X.</summary>
    private static void Paddle(Node3D parent, Vector3 tip, float scale, Material material)
    {
        Cylinder(parent, .34f, .34f, .04f, tip + new Vector3(.34f * scale, 0, 0), material, new Vector3(0, -90, 0), 3).Scale = new Vector3(scale, 1, scale);
        Cylinder(parent, .34f, .34f, .04f, tip + new Vector3(.68f * scale, 0, 0), material, null, 16).Scale = new Vector3(scale, 1, scale);
    }

    /// <summary>Needle roll in degrees for a heat fraction: cold points left, full heat points right.</summary>
    public static float Gauge(float heat) => 110 - 220 * Math.Clamp(heat, 0, 1);

    private static OmniLight3D Light(Node3D parent, Vector3 at, float range)
    {
        var light = new OmniLight3D { Position = at, OmniRange = range, LightColor = new Color("ff9a45"), LightEnergy = 0, ShadowEnabled = false, OmniAttenuation = 1.4f };
        parent.AddChild(light);
        return light;
    }

    /// <summary>Wolf with four hinged legs, built facing +Z; <see cref="Body"/> lifts and tilts for leaps, feeding and death.</summary>
    public sealed class WolfModel
    {
        public readonly Node3D Root = new();
        public readonly Node3D Body;
        public readonly Node3D[] Legs = new Node3D[4];
        public readonly Node3D Head, Tail;
        public float Stride, Yaw;
        public Vector2 Last;

        public WolfModel()
        {
            Body = Pivot(Root, Vector3.Zero);
            var fur = Mat("6b6e6a"); var light = Mat("a5a69e"); var dark = Mat("44464a");
            Add(Body, new CapsuleMesh { Radius = .2f, Height = 1f, RadialSegments = 10 }, fur, new Vector3(0, .62f, 0), new Vector3(90, 0, 0), new Vector3(1, 1, 1.05f));
            Add(Body, new CapsuleMesh { Radius = .16f, Height = .7f, RadialSegments = 10 }, light, new Vector3(0, .54f, .02f), new Vector3(90, 0, 0));
            Head = Pivot(Body, new Vector3(0, .78f, .48f));
            Sphere(Head, .16f, new Vector3(0, 0, .04f), fur, new Vector3(1, .95f, 1.1f), 10);
            Cylinder(Head, .09f, .05f, .22f, new Vector3(0, -.04f, .26f), light, new Vector3(90, 0, 0), 8);
            Sphere(Head, .03f, new Vector3(0, -.01f, .37f), dark, null, 6);
            Post(Head, .05f, 0, .13f, new Vector3(.08f, .12f, 0), fur, 4);
            Post(Head, .05f, 0, .13f, new Vector3(-.08f, .12f, 0), fur, 4);
            Sphere(Head, .022f, new Vector3(.07f, .04f, .17f), Mat("e2a570", .3f, 0, 1.5f), null, 6);
            Sphere(Head, .022f, new Vector3(-.07f, .04f, .17f), Mat("e2a570", .3f, 0, 1.5f), null, 6);
            // Bared fangs under the muzzle.
            Post(Head, .012f, 0, .05f, new Vector3(.03f, -.09f, .33f), Mat("f2efe6", .4f), 4).RotationDegrees = new Vector3(180, 0, 0);
            Post(Head, .012f, 0, .05f, new Vector3(-.03f, -.09f, .33f), Mat("f2efe6", .4f), 4).RotationDegrees = new Vector3(180, 0, 0);
            Tail = Pivot(Body, new Vector3(0, .7f, -.52f), new Vector3(-130, 0, 0));
            Add(Tail, new CapsuleMesh { Radius = .07f, Height = .5f, RadialSegments = 8 }, fur, new Vector3(0, .22f, 0));
            int index = 0;
            foreach (var (x, z) in new[] { (.11f, .32f), (-.11f, .32f), (.11f, -.32f), (-.11f, -.32f) })
            {
                Legs[index] = Pivot(Body, new Vector3(x, .5f, z));
                Limb(Legs[index], .045f, .5f, new Vector3(0, .02f, 0), index < 2 ? fur : dark);
                index++;
            }
        }

        /// <summary>Front legs take <paramref name="front"/> degrees and hind legs <paramref name="hind"/>; positive swings a foot back.</summary>
        private void Pose(float front, float hind, float head, float tail, float lift = 0, float pitch = 0, float roll = 0, float shift = 0)
        {
            Body.Position = new Vector3(shift, lift, 0);
            Body.RotationDegrees = new Vector3(pitch, 0, roll);
            for (int i = 0; i < 4; i++) Legs[i].RotationDegrees = new Vector3(i < 2 ? front : hind, 0, 0);
            Head.RotationDegrees = new Vector3(head, 0, 0);
            Tail.RotationDegrees = new Vector3(tail, 0, 0);
        }

        public void Trot(float stride, bool running)
        {
            Pose(0, 0, 0, 0);
            float swing = running ? MathF.Sin(stride) * 34 : 0;
            for (int i = 0; i < 4; i++) Legs[i].RotationDegrees = new Vector3(i is 0 or 3 ? swing : -swing, 0, 0);
            // A running wolf carries its head low and level, like it is hunting.
            Head.RotationDegrees = new Vector3(running ? 10 + MathF.Sin(stride * 2) * 4 : MathF.Sin(stride * .5f) * 6 - 4, 0, 0);
            Tail.RotationDegrees = new Vector3(-130 + MathF.Sin(stride * 1.3f) * 8, MathF.Sin(stride) * 10, 0);
        }

        /// <summary>A leap at the throat: <paramref name="progress"/> runs 0 to 1 from take-off to landing.</summary>
        public void Leap(float progress)
        {
            float t = Math.Clamp(progress, 0, 1);
            Pose(-70 + t * 20, 65 - t * 35, -10 + t * 30, -95, MathF.Sin(t * Mathf.Pi) * .75f, Mathf.Lerp(-28, 26, t));
        }

        /// <summary>Head down and tearing, legs braced; <paramref name="time"/> drives the shaking.</summary>
        public void Feed(float time)
        {
            Pose(-14, 22, 48 + MathF.Sin(time * 9) * 12, -150, -.1f, 14);
            Head.RotationDegrees += new Vector3(0, MathF.Sin(time * 13) * 18, 0);
        }

        /// <summary>A killed wolf buckles, then rolls onto its side with stiff legs as <paramref name="progress"/> runs 0 to 1.</summary>
        public void Fall(float progress)
        {
            float buckle = Ease(Math.Clamp(progress / .35f, 0, 1)), roll = Ease(Math.Clamp((progress - .25f) / .5f, 0, 1));
            float fold = 45 * buckle * (1 - roll);
            // Rolling about the ground point would sink the body, so it shifts back over its old footprint as it goes.
            Pose(-fold - 22 * roll, fold + 26 * roll, 35 * buckle - 15 * roll, -150 + 40 * roll, -.22f * buckle * (1 - roll) + .2f * roll, 0, 90 * roll, .62f * roll);
        }

        private static float Ease(float t) => t * t * (3 - 2 * t);
    }

    /// <summary>Courier pulling a two-wheel garden cart; the cart load shows its crates, loose goods or the payment cash box.</summary>
    public sealed class CartModel
    {
        public readonly Node3D Root = new();
        public readonly ActorRig Courier = new(ActorSkin.Courier);
        public readonly Node3D Goods, Cash;
        /// <summary>One crate model per market slot: four on the bed and two stacked on top.</summary>
        public readonly Node3D[] Crates = new Node3D[World.MarketSlots];
        private readonly Node3D[] wheels = new Node3D[2];
        public float Distance, Yaw;
        public Vector2 Last;

        public CartModel()
        {
            Root.AddChild(Courier.Root);
            Courier.Root.Position = new Vector3(0, 0, .55f);
            var cart = Pivot(Root, new Vector3(0, 0, -.45f));
            var green = Mat("3f6b3a", .55f, .3f);
            Box(cart, new Vector3(.7f, .3f, .85f), new Vector3(0, .5f, 0), green);
            Box(cart, new Vector3(.62f, .02f, .78f), new Vector3(0, .66f, 0), Mat("2d4a2a", .7f));
            for (int i = 0; i < 2; i++)
            {
                wheels[i] = Pivot(cart, new Vector3(i == 0 ? .42f : -.42f, .26f, 0));
                Cylinder(wheels[i], .26f, .26f, .1f, Vector3.Zero, Mat("222222", .9f), new Vector3(0, 0, 90), 12);
                Cylinder(wheels[i], .1f, .1f, .12f, Vector3.Zero, Mat("c9ced2", .4f, .7f), new Vector3(0, 0, 90), 8);
            }
            Cylinder(cart, .02f, .02f, .7f, new Vector3(.2f, .62f, .7f), Mat("c9ced2", .4f, .7f), new Vector3(70, 0, 0), 6);
            Cylinder(cart, .02f, .02f, .7f, new Vector3(-.2f, .62f, .7f), Mat("c9ced2", .4f, .7f), new Vector3(70, 0, 0), 6);
            Cylinder(cart, .025f, .025f, .46f, new Vector3(0, .74f, 1.02f), Mat("222222"), new Vector3(0, 0, 90), 6);
            Goods = Pivot(cart, new Vector3(0, .68f, 0));
            Box(Goods, new Vector3(.4f, .28f, .32f), new Vector3(-.08f, .14f, .18f), Mat("c9a46e"));
            Box(Goods, new Vector3(.36f, .24f, .3f), new Vector3(.1f, .12f, -.2f), Mat("b8925c"));
            Box(Goods, new Vector3(.3f, .22f, .26f), new Vector3(.05f, .39f, .05f), Mat("c9a46e"));
            for (int i = 0; i < Crates.Length; i++)
            {
                // Small gaps and dark corner battens keep each crate countable at a glance.
                var at = i < 4 ? new Vector3(i % 2 == 0 ? -.15f : .15f, 0, i < 2 ? .2f : -.2f) : new Vector3(i == 4 ? -.15f : .15f, .25f, i == 4 ? .08f : -.12f);
                Crates[i] = Pivot(cart, new Vector3(0, .68f, 0) + at, new Vector3(0, i * 23 % 13 - 6, 0));
                Box(Crates[i], new Vector3(.25f, .22f, .32f), new Vector3(0, .11f, 0), Mat("d4b07a"));
                foreach (var (x, z) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }) Box(Crates[i], new Vector3(.035f, .225f, .035f), new Vector3(x * .11f, .112f, z * .15f), Mat("6e4f2c"));
                Box(Crates[i], new Vector3(.255f, .025f, .325f), new Vector3(0, .21f, 0), Mat("8f6a3f"));
            }
            Cash = Pivot(cart, new Vector3(0, .68f, 0));
            Box(Cash, new Vector3(.36f, .2f, .26f), new Vector3(0, .1f, .1f), Mat("4a5258", .4f, .7f));
            Sphere(Cash, .14f, new Vector3(.12f, .12f, -.2f), Mat("c9a227", .5f), new Vector3(1, .9f, 1), 8);
            Sphere(Cash, .12f, new Vector3(-.14f, .1f, -.22f), Mat("b89120", .5f), new Vector3(1, .9f, 1), 8);
        }

        /// <summary>Shows exactly <paramref name="crates"/> crates, or loose goods, or the cash box.</summary>
        public void Load(int crates, bool goods, bool cash)
        {
            for (int i = 0; i < Crates.Length; i++) Crates[i].Visible = !cash && i < crates;
            Goods.Visible = goods && !cash && crates == 0; Cash.Visible = cash;
        }

        public void Roll(float distance)
        {
            foreach (var wheel in wheels) wheel.RotationDegrees = new Vector3(Mathf.RadToDeg(distance / .26f), 0, 0);
        }
    }

}
