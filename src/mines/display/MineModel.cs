using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Builds a mine's model and the rock spur every cliff has for a mine's tunnel to run into.</summary>
internal static class MineModel {
    /// <summary>Turns a mine's local +Z along its rails, out of the cliff's end.</summary>
    public static Vector3 Yaw(Vector2 p) {
        var way = Cliff.Nearest(p).Face;
        return new Vector3(0, MathF.Atan2(way.X, way.Y), 0);
    }

    /// <summary>Ore color in a mine cart: rusty iron, black coal, and pale grey for anything else.</summary>
    private static string OreColor(ThingKind ore) => ore switch { ThingKind.Iron => "a35a2e", ThingKind.Coal => "26282b", _ => "b9bbb4" };

    /// <summary>The portal, this far behind a mine's spot along its rails; the cart stands at the spot (see <see cref="Cliff.MineSpot"/>).</summary>
    public const float PortalDepth = -2.2f;
    /// <summary>How far the tunnel runs into the spur before it is black.</summary>
    public const float TunnelLength = 3;

    /// <summary>
    /// The rock site beside a cliff, built in that cliff's face-aligned mine frame: rock to both sides of and over a cave mouth, so a mine's tunnel can run into it.
    /// Part of the scenery, so every cliff has one whether or not it is mined.
    /// </summary>
    public static void Spur(Node3D parent, Cliff cliff, string[] rock) {
        var spot = cliff.MineSpot;
        var spur = Pivot(parent, new Vector3(spot.X, 0, spot.Y));
        spur.Rotation = Yaw(spot);
        Add(spur, MineRockShape.Mesh(cliff, rock), MineRockShape.Rock, Vector3.Zero);
    }

    /// <summary>A timbered adit into the cliff's spur, local +Z out of the rock, with rails running in and a cart standing on them at the origin.</summary>
    public static MineProp Build(ThingKind ore) {
        var prop = new MineProp();
        var body = prop.Body;
        // The marking: a see-through green footprint and portal, shown until the mine is finished.
        var outline = Pivot(body, Vector3.Zero);
        var green = Flat(new Color(.45f, .95f, .4f, .45f));
        Add(outline, MineShape.Outline, green, Vector3.Zero);
        prop.Outline = outline;
        // The dug tunnel: unlit floor, walls and roof that darken step by step to black, so the light never reaches its end.
        // It deepens from the portal as the entrance is dug; a spoil patch lies in front.
        var hole = Pivot(body, new Vector3(0, 0, PortalDepth));
        Add(hole, MineShape.Tunnel, MineShape.Unlit, Vector3.Zero);
        Add(hole, MineShape.Pad, MineShape.Timber, Vector3.Zero);
        prop.Hole = hole;
        // Delivered materials wait beside the entrance: a log pile, stones and a few iron bars.
        var supplies = Pivot(body, Vector3.Zero);
        for (int i = 0; i < 5; i++) Cylinder(supplies, .11f, .11f, .9f, new Vector3(1.3f + (i % 3) * .23f + (i > 2 ? .11f : 0), .11f + (i > 2 ? .19f : 0), -.6f), Wood(i % 2 == 0 ? "8a5f3c" : "7a5235"), new Vector3(90, 0, 0), 7);
        for (int i = 0; i < 4; i++) Sphere(supplies, .17f, new Vector3(-1.35f + (i % 2) * .26f, .1f + (i / 2) * .18f, -.2f + (i % 2) * .1f), Stone("a3a7a3", .9f), new Vector3(1.2f, .7f, 1), 6);
        for (int i = 0; i < 3; i++) Box(supplies, new Vector3(.12f, .06f, .5f), new Vector3(-1.3f, .03f + i * .06f, -1f), Metal("c2c9c9", .4f, .6f), new Vector3(0, i * 12, 0));
        prop.Supplies = supplies;
        // Rough timber sets: a weathered portal with a plank hood and a warning board, then sets down the tunnel fading into the dark.
        var frame = Pivot(body, new Vector3(0, 0, PortalDepth));
        Add(frame, MineShape.Portal, MineShape.Timber, Vector3.Zero);
        Add(frame, MineShape.Supports, MineShape.Unlit, Vector3.Zero);
        // A lantern hangs from the header on a hook; its flame and light burn only while someone works.
        var lantern = Pivot(frame, new Vector3(.55f, 1.46f, .32f));
        var iron = Metal("2f3134", .5f, .6f);
        Box(lantern, new Vector3(.03f, .3f, .03f), new Vector3(0, .3f, -.08f), iron);
        Box(lantern, new Vector3(.03f, .03f, .14f), new Vector3(0, .44f, -.02f), iron);
        Cylinder(lantern, .04f, .1f, .07f, new Vector3(0, .2f, 0), iron, null, 6);
        Cylinder(lantern, .1f, .1f, .04f, new Vector3(0, -.02f, 0), iron, null, 6);
        Box(lantern, new Vector3(.13f, .18f, .13f), new Vector3(0, .08f, 0), Mat("6d6658", .3f));
        var flame = Pivot(lantern, Vector3.Zero);
        Box(flame, new Vector3(.135f, .185f, .135f), new Vector3(0, .08f, 0), Flat(new Color("ffcf6a")));
        prop.Fire = flame;
        prop.Light = WorldModels.Light(lantern, new Vector3(0, .08f, .2f), 5);
        prop.Frame = frame;
        // Rails from the front of the spoil patch all the way in, the inner lengths darkening with the tunnel.
        var rails = Pivot(body, Vector3.Zero);
        Add(rails, MineShape.Rails, MineShape.Steel, Vector3.Zero);
        Add(rails, MineShape.Sleepers, MineShape.Timber, Vector3.Zero);
        Add(rails, MineShape.DeepRails, MineShape.Unlit, Vector3.Zero);
        prop.Rails = rails;
        // The cart rests at the end of the rails, loaded once there is output; it is underground with the miners while they work.
        var cart = Pivot(body, Vector3.Zero);
        Add(cart, MineShape.Cart, MineShape.Steel, Vector3.Zero);
        var load = Pivot(cart, Vector3.Zero);
        Add(load, MineShape.Load, Stone(OreColor(ore), .8f), Vector3.Zero);
        prop.Load = load;
        prop.Cart = cart;
        // Lamplight deep inside while someone works underground: a faint warm haze at the dark end.
        prop.Glow = Box(body, new Vector3(1.4f, 1.7f, .02f), new Vector3(0, .86f, PortalDepth - 2.5f), Flat(new Color(1, .55f, .2f, .25f)));
        return prop;
    }
}
