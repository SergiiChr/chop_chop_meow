using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>A vertical hit cylinder in model space; picking and canopy fading use the same volumes.</summary>
public readonly record struct Hit(float Radius, float Bottom, float Top);

/// <summary>A built world object plus the moving parts that <see cref="WorldView"/> animates from simulation state.</summary>
public sealed class Prop {
    public readonly Node3D Root = new();
    public readonly Node3D Body;
    public Node3D? Sway, Stump, Fruit, Lid, Door, Lever, Needle, Flag, Fire, Chisel, Hammer, Mortar;
    /// <summary>A mine's parts, shown stage by stage: the marked outline, the dug hole, the delivered materials, the timber frame, the rails and the cart with its load.</summary>
    public Node3D? Outline, Hole, Supplies, Frame, Rails, Cart, Load;
    /// <summary>A tool station's hanging tools by kind, each shown while the station holds one.</summary>
    public Dictionary<string, Node3D> Racked = new();
    /// <summary>A pile's stacks, 9, 4 and 1 from the ground up, shown one per used slot in the colour of what it holds.</summary>
    public MeshInstance3D[] Stacks = Array.Empty<MeshInstance3D>();
    public string StackKey = "";
    /// <summary>A farm plot's looks by <see cref="World.CropLook"/> from 1: sown soil, small straws, tall green plants and ripe wheat; one shows at a time over the soil.</summary>
    public Node3D[] Crops = Array.Empty<Node3D>();
    public Node3D[] Pleats = Array.Empty<Node3D>();
    public Node3D? WoodPile, CoalPile;
    public MeshInstance3D? Glow, Window, Pool;
    public WorldModels.WolfModel? Wolf;
    public OmniLight3D? Light;
    public Vector3 Vent;
    public Hit[] Hits = Array.Empty<Hit>();
    /// <summary>A tree crown's meshes and their own materials, for fading it without walking its nodes.</summary>
    public (MeshInstance3D Mesh, StandardMaterial3D Material)[] SwayParts = Array.Empty<(MeshInstance3D, StandardMaterial3D)>();
    public string Kind = "";
    public float Fade, Fall = -1, Open, Shake, Puff;
    public int LastCount, Tint = -1;
    public bool Seen;
    /// <summary>Whether the model is drawn, kept here so it is set in Godot only when it changes.</summary>
    public bool Shown = true;
    public Prop() { Body = Pivot(Root, Vector3.Zero); }
}

/// <summary>Code-built low-poly models for every world object: natural, modern and DIY rather than fantasy.</summary>
public static class WorldModels {
    public const int TreeVariants = 3;
    /// <summary>Full deposit sizes from world generation; piles shrink toward half size as they are worked.</summary>
    public static float Fullness(Thing t) => t.Kind switch {
        "stone" => t.Remaining / 64f,
        "iron" or "coal" or "tin" => t.Remaining / 400f,
        "wolf_corpse" => t.Remaining / 4f,
        "leaves" => t.Remaining / (float)World.LeafPatch,
        _ => 1
    };

    /// <summary>Stable pseudo-random value in [0, 1) so a thing looks the same after every load.</summary>
    public static float Jitter(int id, int salt) {
        uint h = (uint)(id * 73856093) ^ (uint)(salt * 19349663);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return (h & 0xffff) / 65536f;
    }

    public static Hit[] Hits(Thing t) => t.Kind switch {
        "tree" => (t.Id % TreeVariants) switch {
            1 => new[] { new Hit(.3f, 0, 1.2f), new Hit(1.25f, 1.2f, 5.6f) },
            2 => new[] { new Hit(.25f, 0, 2.6f), new Hit(1.05f, 2.6f, 5.4f) },
            _ => new[] { new Hit(.35f, 0, 2.5f), new Hit(1.45f, 2.5f, 5.3f) },
        },
        "stone" => new[] { new Hit(.75f, 0, .9f) },
        "iron" or "coal" or "tin" => new[] { new Hit(.8f, 0, 1) },
        "loose_coal" => new[] { new Hit(.45f, 0, .35f) },
        "wolf_corpse" => new[] { new Hit(.65f, 0, .5f) },
        "berries" => new[] { new Hit(.65f, 0, 1) },
        "water" => new[] { new Hit(1.2f, 0, .5f) },
        "furnace" => new[] { new Hit(.72f, 0, 1.15f) },
        "reinforced" => new[] { new Hit(.95f, 0, 1.45f) },
        "hut" => new[] { new Hit(1.6f, 0, 3.1f) },
        "chest" => new[] { new Hit(.5f, 0, .65f) },
        "crate" => new[] { new Hit(.4f, 0, .55f) },
        "bench" => new[] { new Hit(.6f, 0, .95f) },
        "cooktable" => new[] { new Hit(.7f, 0, .9f) },
        "cannery" => new[] { new Hit(.6f, 0, 1f) },
        "toolstation" => new[] { new Hit(.55f, 0, 1.5f) },
        "pile" => new[] { new Hit(1.5f, 0, .5f) },
        "farm" => new[] { new Hit(1.5f, 0, .5f) },
        "leaves" => new[] { new Hit(.6f, 0, .15f) },
        "grave" => new[] { new Hit(.45f, 0, .55f) },
        "anvil" => new[] { new Hit(.45f, 0, .85f) },
        "signpost" => new[] { new Hit(1.02f, 0, 1.98f) },
        "campfire" => new[] { new Hit(.55f, 0, .7f) },
        "mine" => new[] { new Hit(.9f, 0, 1.8f) },
        _ => new[] { new Hit(.5f, 0, .8f) },
    };

    public static Prop Build(Thing t) {
        var prop = new Prop { Kind = t.Kind, Hits = Hits(t) };
        var body = prop.Body;
        switch (t.Kind) {
            case "tree": Tree(prop, t.Id); break;
            case "stone": Rocks(body, t.Kind, t.Id, "8d918e"); break;
            case "iron": Rocks(body, t.Kind, t.Id, "7d5b45", "a8592b", "b4622a"); break;
            case "coal": Rocks(body, t.Kind, t.Id, "505257", "121316", "15161a"); break;
            case "tin": Rocks(body, t.Kind, t.Id, "858882", "b9bcb4", "e6e8e2"); break;
            case "loose_coal": LooseOre(body, t.Kind, t.Id, "2d2f33", "1d1e21"); break;
            case "wolf_corpse": Carcass(prop); break;
            case "berries": Bush(prop, t.Id); break;
            case "water": break; // The lake is the drinking spot; this thing only provides the hit area.
            case "furnace": BlockFurnace(prop); break;
            case "reinforced": DrumForge(prop); break;
            case "hut": Cabin(prop); break;
            case "chest": StorageBox(prop, "c9a46e", "9d7a4c", "2f3133"); break;
            case "grave": StorageBox(prop, "e2701e", "c45b12", "f2f2ea"); break;
            case "anvil": Anvil(prop); break;
            case "crate": Crate(prop); break;
            case "bench": Workbench(prop); break;
            case "cooktable": CookingTable(prop); break;
            case "cannery": Cannery(prop); break;
            case "toolstation": ToolRack(prop); break;
            case "pile": Pile(prop); break;
            case "farm": Farm(prop, t.Id); break;
            case "leaves": LeafLitter(body, t.Id); break;
            case "signpost": Signpost(prop); break;
            case "campfire": FirePit(prop); break;
            case "mine": Mine(prop, World.CliffAt(t.Pos).Ore); break;
        }
        if (t.Kind != "water") body.RotationDegrees = new Vector3(0, t.Kind == "tree" || t.IsResource ? Jitter(t.Id, 1) * 360 : 0, 0);
        if (t.Kind == "furnace") SyncFuel(prop, t);
        if (t.Kind == "mine") body.Rotation = MineYaw(t.Pos);
        return prop;
    }

    /// <summary>
    /// The model an icon shows for an item or building plan, or null when the game has no model of its own for it.
    /// Items borrow the part of a model that shows them in the world; buildings show as placed, with a finished mine, a ripe farm and a pile full of logs.
    /// Berries show as a small heap of the bush's berries; planks, parts and bars as the stock a storage pile shows.
    /// Hammers and chisels share one model between grades, so they keep their drawn icons.
    /// </summary>
    public static Node3D? IconModel(string id, bool building, GameData data) {
        Node3D Part(string kind, Func<Prop, Node3D> part) {
            var prop = Build(new Thing { Kind = kind });
            var node = part(prop);
            node.GetParent().RemoveChild(node);
            node.Visible = true;
            prop.Root.Free();
            return node;
        }
        switch (id) {
            case "steel_axe" or "steel_pick": return ActorRig.Tool(id);
            case "torch": var (torch, _, _) = ActorRig.Torch(); return torch;
            case "wood": return Part("furnace", p => p.WoodPile!);
            case "rake": return Part("toolstation", p => p.Racked["rake"]);
            case "mortar": return Part("cooktable", p => p.Mortar!);
            case "berries": { var heap = new Node3D(); Add(heap, NatureShape.BerryHeap, NatureShape.Glossy, Vector3.Zero); return heap; }
            case "planks" or "wood_parts" or "iron_bar" or "steel_bar" or "tin_bar": {
                    var stock = new Node3D(); stock.AddChild(PileShape.Stock(id, data.Items[id].Color));
                    return stock;
                }
            case "stone" or "leaves" or "crate": return Build(new Thing { Kind = id }).Root;
            // Loose iron left the world, but its lump still makes the iron ore icon.
            case "iron_ore": { var lump = new Node3D(); LooseOre(lump, "loose_iron", 0, "a35a2e", "8b4f2b"); return lump; }
            case "coal" or "tin_ore": return Build(new Thing { Kind = id == "coal" ? "loose_coal" : "tin" }).Root;
            case "mine": {
                    var prop = new Prop { Kind = id };
                    Mine(prop, "iron");
                    prop.Outline!.Visible = prop.Supplies!.Visible = prop.Glow!.Visible = prop.Fire!.Visible = false;
                    return prop.Root;
                }
        }
        if (!building) return null;
        var built = Build(new Thing { Kind = id });
        if (built.Glow != null) built.Glow.Visible = false;
        if (id == "farm") { built.Crops[0].Visible = built.Crops[^1].Visible = true; }
        foreach (var stack in built.Stacks) { stack.Visible = true; PileShape.SetStock(stack, "wood", data.Items["wood"].Color); }
        return built.Root;
    }

    /// <summary>The outdoor pile represents stocked fuel, excluding fuel already burning in a batch.</summary>
    public static void SyncFuel(Prop prop, Thing t) {
        string fuel = World.BurnedFuel(t);
        prop.WoodPile!.Visible = fuel == "wood" && t.Inventory["wood"] > 0;
        prop.CoalPile!.Visible = fuel == "coal" && t.Inventory["coal"] > 0;
    }

    private static void Tree(Prop prop, int id) {
        var sway = Pivot(prop.Body, Vector3.Zero);
        prop.Sway = sway; // the whole tree shakes and falls about its base
        float scale = .9f + Jitter(id, 2) * .22f;
        sway.Scale = Vector3.One * scale;
        int variant = (id / TreeVariants) % NatureShape.Variants;
        Add(sway, (id % TreeVariants) switch { 1 => NatureShape.Spruce(variant), 2 => NatureShape.Birch(variant), _ => NatureShape.Oak(variant) }, NatureShape.Natural, Vector3.Zero);
        var stump = Add(prop.Root, NatureShape.Stump, NatureShape.Natural, Vector3.Zero);
        stump.Visible = false;
        prop.Stump = stump;
    }

    /// <summary>A boulder group in the deposit's rock color; an ore deposit also shows veins across the rock faces and ore chunks set into it.</summary>
    private static void Rocks(Node3D body, string kind, int id, string stone, string? vein = null, string? chunks = null) {
        int variant = id % NatureShape.Variants;
        Add(body, NatureShape.Rocks(kind, new Color(stone), variant, vein == null ? null : false), NatureShape.Natural, Vector3.Zero);
        if (vein == null || chunks == null) return;
        Add(body, NatureShape.Rocks(kind, new Color(vein), variant, true), NatureShape.Mineral, Vector3.Zero);
        Add(body, NatureShape.Insertions(kind, new Color(chunks), variant), NatureShape.Mineral, Vector3.Zero);
    }

    private static void LooseOre(Node3D body, string kind, int id, string light, string dark) =>
        Add(body, NatureShape.Loose(kind, new Color(light), new Color(dark), id % NatureShape.Variants), NatureShape.Natural, Vector3.Zero);

    private static void Carcass(Prop prop) {
        prop.Pool = Cylinder(prop.Body, .55f, .55f, .004f, new Vector3(.1f, .006f, 0), Flat(new Color(.36f, .05f, .05f, .75f), true), null, 14);
        prop.Wolf = new WolfModel();
        prop.Body.AddChild(prop.Wolf.Root);
    }

    private static void Bush(Prop prop, int id) {
        int variant = id % NatureShape.Variants;
        Add(prop.Body, NatureShape.Bush(variant), NatureShape.Natural, Vector3.Zero);
        var fruit = Pivot(prop.Body, Vector3.Zero);
        Add(fruit, NatureShape.Berries(variant), NatureShape.Glossy, Vector3.Zero);
        prop.Fruit = fruit;
    }

    /// <summary>Cinder-block furnace with a steel top plate, stovepipe and stocked fuel pile.</summary>
    private static void BlockFurnace(Prop prop) {
        var body = prop.Body;
        Add(body, FurnaceShape.Masonry, FurnaceShape.Concrete, Vector3.Zero);
        prop.Glow = Add(body, FurnaceShape.Fire, FurnaceShape.Flame, Vector3.Zero);
        Add(body, FurnaceShape.Plate, FurnaceShape.Steel, Vector3.Zero);
        var flue = Pivot(body, new Vector3(-.3f, 0, -.25f));
        var metal = new Color("8e9396");
        ActorShape.Add(flue, new[] {
            new ActorShape.Ring(2.05f, .001f, .001f, metal), new ActorShape.Ring(1.94f, .14f, .14f, metal),
            new ActorShape.Ring(1.92f, .16f, .16f, metal), new ActorShape.Ring(1.90f, .09f, .09f, metal),
            new ActorShape.Ring(.95f, .09f, .09f, metal), new ActorShape.Ring(.9f, .11f, .11f, metal),
            new ActorShape.Ring(.85f, .12f, .12f, metal) }, material: FurnaceShape.Steel);
        prop.Vent = new Vector3(-.3f, 2.05f, -.25f);
        prop.WoodPile = Pivot(body, Vector3.Zero);
        for (int i = 0; i < 3; i++)
            Cylinder(prop.WoodPile, .07f, .065f, .5f, new Vector3(.82f, .08f + (i == 2 ? .13f : 0), -.1f + (i == 2 ? .07f : i * .14f)), Mat(i == 1 ? "7a5235" : "8a5f3c"), new Vector3(90, 0, 0), 10);
        prop.CoalPile = Add(body, FurnaceShape.CoalPile, FurnaceShape.Concrete, new Vector3(.82f, 0, -.03f));
        prop.Light = Light(body, new Vector3(0, .35f, .7f), 2.6f);
    }

    /// <summary>A propane-tank forge on an angle-iron stand, fed by a hinged leather bellows with a heat gauge on top.</summary>
    private static void DrumForge(Prop prop) {
        var body = prop.Body;
        var steel = Mat("3b3f42", .5f, .7f);
        foreach (var (x, z) in new[] { (-.35f, -.3f), (.35f, -.3f), (-.35f, .3f), (.35f, .3f) }) Box(body, new Vector3(.05f, .5f, .05f), new Vector3(x, .25f, z), steel);
        Box(body, new Vector3(.8f, .04f, .7f), new Vector3(0, .5f, 0), steel);
        Add(body, ForgeShape.Shell, FurnaceShape.Steel, Vector3.Zero);
        prop.Glow = Add(body, ForgeShape.Fire, FurnaceShape.Flame, Vector3.Zero);
        // Saddles meet the round shell above the stand instead of balancing it on a flat plate.
        foreach (float z in new[] { -.25f, .2f })
            foreach (float x in new[] { -.25f, .25f })
                Box(body, new Vector3(.12f, .15f, .09f), new Vector3(x, .565f, z), steel, new Vector3(0, 0, x < 0 ? -28 : 28));
        var flue = Pivot(body, new Vector3(0, 0, -.2f));
        var metal = new Color("8e9396");
        ActorShape.Add(flue, new[] {
            new ActorShape.Ring(1.95f, .07f, .07f, metal), new ActorShape.Ring(1.91f, .08f, .08f, metal),
            new ActorShape.Ring(1.88f, .07f, .07f, metal), new ActorShape.Ring(1.34f, .07f, .07f, metal),
            new ActorShape.Ring(1.29f, .10f, .10f, metal), new ActorShape.Ring(1.25f, .115f, .115f, metal) }, material: FurnaceShape.Steel);
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
        Add(rig, ForgeShape.Board, wood, hinge + new Vector3(0, -.03f, 0));
        Cylinder(rig, .07f, .025f, .22f, new Vector3(.55f, .3f, 0), Mat("b8893a", .4f, .8f), new Vector3(0, 0, 90), 10);
        var lever = Pivot(rig, hinge);
        Add(lever, ForgeShape.Board, wood, new Vector3(0, .13f, 0));
        Box(lever, new Vector3(.06f, .62f, .06f), new Vector3(1f, .44f, 0), Mat("8a5a32"));
        Cylinder(lever, .03f, .03f, .34f, new Vector3(1f, .75f, 0), Mat("222222"), new Vector3(90, 0, 0), 8);
        prop.Lever = lever;
        // Each pleat hangs from the hinge and follows a share of the lid's swing, so the leather folds as it closes.
        prop.Pleats = new Node3D[3];
        for (int i = 0; i < 3; i++) {
            prop.Pleats[i] = Pivot(rig, hinge);
            float size = i % 2 == 0 ? .97f : .88f;
            Add(prop.Pleats[i], ForgeShape.Board, leather, new Vector3(0, -.03f + (i + 1) * .04f, 0), scale: new Vector3(size, 1, size));
        }
        prop.Light = Light(body, new Vector3(0, .9f, .9f), 3);
    }

    /// <summary>Small OSB cabin on block piers with a corrugated shed roof, solar panel and rain barrel.</summary>
    private static void Cabin(Prop prop) {
        var body = prop.Body;
        foreach (var (x, z) in new[] { (-1.1f, -1f), (1.1f, -1f), (-1.1f, 1f), (1.1f, 1f) }) Box(body, new Vector3(.3f, .3f, .3f), new Vector3(x, .15f, z), Mat("9a9b97"));
        Box(body, new Vector3(2.6f, .12f, 2.4f), new Vector3(0, .36f, 0), Mat("7d5f43"));
        Add(body, CabinShape.Walls, CabinShape.WallMaterial, Vector3.Zero);
        Add(body, CabinShape.Interior, CabinShape.WallMaterial, Vector3.Zero);
        var trim = Mat("3d4245");
        foreach (var (x, z) in new[] { (-1.2f, -1.1f), (1.2f, -1.1f), (-1.2f, 1f), (1.2f, 1f) }) {
            float height = CabinShape.WallTop(z) - .42f;
            Box(body, new Vector3(.08f, height, .08f), new Vector3(x, .42f + height / 2, z), trim);
        }
        foreach (float x in new[] { -.95f, .15f, 1.03f }) {
            float height = CabinShape.WallTop(1) - .42f;
            Box(body, new Vector3(.025f, height, .015f), new Vector3(x, .42f + height / 2, 1.005f), Mat("b08c55"));
        }
        foreach (float z in new[] { -.8f, .65f }) {
            float height = CabinShape.WallTop(z) - .42f;
            Box(body, new Vector3(.015f, height, .025f), new Vector3(1.205f, .42f + height / 2, z), Mat("b08c55"));
        }
        // Shed roof pitched toward the back, overhanging the door.
        var roof = Pivot(body, new Vector3(0, 2.68f, -.05f), new Vector3(-9, 0, 0));
        Add(roof, CabinShape.Roof, Mat("6f7a80", .45f, .6f), Vector3.Zero);
        Box(roof, new Vector3(1.1f, .05f, .8f), new Vector3(.55f, .085f, -.4f), Mat("1f2c44", .25f, .4f));
        Box(roof, new Vector3(1.14f, .03f, .84f), new Vector3(.55f, .065f, -.4f), Mat("c9ced2", .4f, .7f));
        Post(body, .08f, .08f, 1.1f, new Vector3(-.75f, 2.4f, -.6f), Mat("8e9396", .45f, .7f));
        Post(body, .14f, 0, .12f, new Vector3(-.75f, 3.48f, -.6f), Mat("8e9396", .45f, .7f), 8);
        prop.Vent = new Vector3(-.75f, 3.6f, -.6f);
        // Door hinged on its left edge, painted teal.
        // The hinge axis sits just ahead of the jamb so the open leaf clears the frame.
        var door = Pivot(body, new Vector3(-.75f, .42f, 1.075f));
        Box(door, new Vector3(.82f, 1.86f, .05f), new Vector3(.41f, .93f, -.025f), Mat("3e6e6a", .6f));
        Sphere(door, .035f, new Vector3(.72f, .95f, .025f), Mat("c9ced2", .3f, .8f));
        prop.Door = door;
        foreach (float x in new[] { -.79f, .11f })
            Box(body, new Vector3(.06f, 1.93f, .1f), new Vector3(x, 1.385f, 1.015f), trim);
        Box(body, new Vector3(.96f, .06f, .1f), new Vector3(-.34f, 2.32f, 1.015f), trim);
        Box(body, new Vector3(1, .14f, .45f), new Vector3(-.34f, .07f, 1.375f), Mat("a9824f"));
        Box(body, new Vector3(1, .28f, .26f), new Vector3(-.34f, .14f, 1.25f), Mat("a9824f"));
        var glass = Mat("8fb4c4", .15f, .2f);
        Box(body, new Vector3(.7f, .6f, .06f), new Vector3(.55f, 1.65f, 1.0f), trim);
        prop.Window = Box(body, new Vector3(.6f, .5f, .07f), new Vector3(.55f, 1.65f, 1.0f), glass);
        Box(body, new Vector3(.06f, .7f, .9f), new Vector3(1.2f, 1.6f, 0), trim);
        Box(body, new Vector3(.07f, .6f, .8f), new Vector3(1.2f, 1.6f, 0), glass);
        var barrel = Pivot(body, new Vector3(1.5f, 0, .6f));
        var blue = new Color("2c5f9e"); var rim = new Color("244f85");
        ActorShape.Add(barrel, new[] {
            new ActorShape.Ring(.8f, .23f, .23f, rim), new ActorShape.Ring(.77f, .27f, .27f, rim),
            new ActorShape.Ring(.73f, .28f, .28f, blue), new ActorShape.Ring(.16f, .28f, .28f, blue),
            new ActorShape.Ring(.03f, .255f, .255f, blue), new ActorShape.Ring(0, .23f, .23f, blue) });
        prop.Light = Light(body, new Vector3(.55f, 1.65f, 1.4f), 2.5f);
    }

    private static void StorageBox(Prop prop, string panel, string edge, string fittings) {
        var body = prop.Body;
        if (prop.Kind == "chest") {
            Add(body, ChestShape.Body, ChestShape.Wood, Vector3.Zero);
            Add(body, ChestShape.Frame, ChestShape.Metal, Vector3.Zero);
            var hinge = Pivot(body, new Vector3(0, .49f, -.275f));
            Add(hinge, ChestShape.Lid, ChestShape.Wood, Vector3.Zero);
            Add(hinge, ChestShape.LidFrame, ChestShape.Metal, Vector3.Zero);
            prop.Lid = hinge;
            return;
        }
        bool tote = panel == "e2701e";
        var size = tote ? new Vector3(.75f, .36f, .5f) : new Vector3(.85f, .45f, .55f);
        Box(body, size, new Vector3(0, size.Y / 2 + .04f, 0), Mat(panel, tote ? .5f : .9f));
        if (!tote) {
            foreach (var (x, z) in new[] { (-.4f, -.25f), (.4f, -.25f), (-.4f, .25f), (.4f, .25f) }) Box(body, new Vector3(.07f, .5f, .07f), new Vector3(x, .27f, z), Mat(fittings, .5f, .6f));
            Box(body, new Vector3(.87f, .06f, .57f), new Vector3(0, .07f, 0), Mat(edge));
            Box(body, new Vector3(.2f, .05f, .04f), new Vector3(.25f, .3f, .29f), Mat(fittings, .5f, .6f));
            Box(body, new Vector3(.2f, .05f, .04f), new Vector3(-.25f, .3f, .29f), Mat(fittings, .5f, .6f));
        } else Box(body, new Vector3(.77f, .05f, .52f), new Vector3(0, .25f, 0), Mat(fittings, .4f));
        var lid = Pivot(body, new Vector3(0, size.Y + .04f, -size.Z / 2));
        Box(lid, new Vector3(size.X + .04f, .06f, size.Z + .04f), new Vector3(0, .03f, size.Z / 2), Mat(tote ? edge : edge, tote ? .5f : .85f));
        Box(lid, new Vector3(.12f, .04f, .03f), new Vector3(0, .02f, size.Z + .03f), Mat(fittings, .5f, .6f));
        prop.Lid = lid;
    }

    /// <summary>A hollow slatted crate with a separately hinged lid and shared wood geometry.</summary>
    private static void Crate(Prop prop) {
        Add(prop.Body, CrateShape.Body, CrateShape.Timber, Vector3.Zero);
        var lid = Pivot(prop.Body, new Vector3(0, .42f, -.23f));
        Add(lid, CrateShape.Lid, CrateShape.Timber, Vector3.Zero);
        prop.Lid = lid;
    }

    /// <summary>A plank-topped workbench with a lower shelf and a vise; slotted tools lie on the top.</summary>
    private static void Workbench(Prop prop) {
        var body = prop.Body;
        var wood = Mat("b98552"); var steel = Mat("4a4d50", .4f, .6f);
        Add(body, BenchShape.Wood, BenchShape.Timber, Vector3.Zero);
        Add(body, BenchShape.Vise, BenchShape.Steel, Vector3.Zero);
        Cylinder(body, .014f, .014f, .19f, new Vector3(.35f, .86f, .37f), steel, new Vector3(90, 0, 0), 10);
        Cylinder(body, .012f, .012f, .15f, new Vector3(.35f, .86f, .46f), steel, sides: 10);
        var chisel = Pivot(body, new Vector3(-.25f, .83f, .05f), new Vector3(0, 30, 0));
        Box(chisel, new Vector3(.24f, .03f, .06f), Vector3.Zero, Mat("9aa29f"));
        prop.Chisel = chisel;
        var hammer = Pivot(body, new Vector3(.15f, .83f, -.1f), new Vector3(0, -20, 0));
        Box(hammer, new Vector3(.34f, .03f, .035f), Vector3.Zero, wood);
        Box(hammer, new Vector3(.06f, .06f, .14f), new Vector3(.17f, .02f, 0), steel);
        prop.Hammer = hammer;
    }

    /// <summary>A stone firebox under a steel grill with a stew pot, beside a plank counter where the slotted mortar and pestle sits.</summary>
    private static void CookingTable(Prop prop) {
        var body = prop.Body;
        Add(body, CookShape.Masonry, CookShape.Stone, Vector3.Zero);
        Add(body, CookShape.Timber, CookShape.Wood, Vector3.Zero);
        Add(body, CookShape.Metal, CookShape.Steel, Vector3.Zero);
        prop.Glow = Add(body, CookShape.Fire, CookShape.Flame, Vector3.Zero);
        prop.Vent = new Vector3(-.56f, 1.4f, -.24f);
        var mortar = Pivot(body, new Vector3(.36f, .645f, .06f));
        Add(mortar, CookShape.Mortar, CookShape.Ceramic, Vector3.Zero);
        prop.Mortar = mortar;
        prop.Light = Light(body, new Vector3(-.33f, .3f, .6f), 2.2f);
    }

    /// <summary>A steel stove with a lidded canning pot, beside a plank table with tin cans and a lever press.</summary>
    private static void Cannery(Prop prop) {
        var body = prop.Body;
        Add(body, CanneryShape.Stove, CanneryShape.Iron, Vector3.Zero);
        Add(body, CanneryShape.Timber, CanneryShape.Wood, Vector3.Zero);
        Add(body, CanneryShape.Pot, CanneryShape.Steel, Vector3.Zero);
        Add(body, CanneryShape.Press, CanneryShape.Steel, Vector3.Zero);
        prop.Glow = Add(body, CanneryShape.Fire, CanneryShape.Flame, Vector3.Zero);
        prop.Vent = new Vector3(-.44f, 1.1f, -.18f);
        prop.Light = Light(body, new Vector3(-.27f, .25f, .55f), 2f);
    }

    /// <summary>A plank pegboard on two posts set in a cinder-block footing, with an axe, a pickaxe and a rake hanging on it.</summary>
    private static void ToolRack(Prop prop) {
        // Turned to face down the valley, toward the camera, so the hanging tools show.
        var body = Pivot(prop.Body, Vector3.Zero, new Vector3(0, 45, 0));
        Add(body, RackShape.Feet, RackShape.Concrete, Vector3.Zero);
        Add(body, RackShape.Frame, RackShape.Timber, Vector3.Zero);
        Node3D Hang(float x, Mesh mesh) {
            var hook = Pivot(body, new Vector3(x, 1.3f, .06f));
            Add(hook, mesh, RackShape.Tool, Vector3.Zero);
            return hook;
        }
        prop.Racked = new() { ["axe"] = Hang(-.3f, RackShape.Axe), ["pickaxe"] = Hang(0, RackShape.Pick), ["rake"] = Hang(.3f, RackShape.Rake) };
    }

    /// <summary>A 3×3 yard of packed earth edged with logs, with fourteen stack blocks in a 9-4-1 pyramid that the view shows as the pile fills.</summary>
    private static void Pile(Prop prop) {
        var body = prop.Body;
        Add(body, PileShape.Yard, PileShape.Ground, new Vector3(0, .01f, 0));
        var stacks = new List<MeshInstance3D>();
        foreach (var (count, y) in new[] { (3, .22f), (2, .62f), (1, 1.02f) })
            for (int x = 0; x < count; x++)
                for (int z = 0; z < count; z++) {
                    float step = .82f, offset = (count - 1) * step / 2;
                    var block = Add(body, PileShape.Stack, Mat("b0a58a", .95f), new Vector3(x * step - offset, y, z * step - offset));
                    block.Visible = false; stacks.Add(block);
                }
        prop.Stacks = stacks.ToArray();
    }

    /// <summary>Refresh only visible inventory memory; the view guards this call with the existing sight check.</summary>
    public static void SyncPile(Prop prop, Thing t, GameData data) {
        var stacks = t.Inventory.Stacks().Select(s => s.Item).ToList();
        string key = string.Join(",", stacks);
        if (key == prop.StackKey) return;
        prop.StackKey = key;
        for (int i = 0; i < prop.Stacks.Length; i++) {
            prop.Stacks[i].Visible = i < stacks.Count;
            if (i < stacks.Count) PileShape.SetStock(prop.Stacks[i], stacks[i], data.Items.TryGetValue(stacks[i], out var def) ? def.Color : PileShape.Fallback);
        }
    }

    /// <summary>A farm plot: an empty pile's yard that, once sown, shows dark tilled rows and then the wheat growing on them.</summary>
    private static void Farm(Prop prop, int id) {
        var body = prop.Body;
        Add(body, PileShape.Yard, PileShape.Ground, new Vector3(0, .01f, 0));
        int variant = id % 3;
        var soil = Pivot(body, Vector3.Zero);
        Add(soil, FarmShape.Soil, FarmShape.Earth, Vector3.Zero);
        Node3D Field(int stage) {
            var field = Pivot(body, Vector3.Zero);
            Add(field, FarmShape.Crop(stage, variant), FarmShape.Foliage, Vector3.Zero);
            return field;
        }
        prop.Crops = new[] { soil, Field(0), Field(1), Field(2) };
        foreach (var crop in prop.Crops) crop.Visible = false;
    }

    /// <summary>A loose scatter of fallen oak leaves in autumn colours, flat on the ground.</summary>
    private static void LeafLitter(Node3D body, int id) {
        string[] colors = { "c08a3a", "a8652c", "d0a347", "8e5a2a", "b8753a" };
        for (int i = 0; i < 26; i++) {
            float angle = i * 2.4f + Jitter(id, 80 + i) * 2, r = .15f + Jitter(id, 110 + i) * .55f;
            Box(body, new Vector3(.16f, .012f, .1f), new Vector3(MathF.Cos(angle) * r, .01f + i * .002f, MathF.Sin(angle) * r), Mat(colors[i % colors.Length], .95f), new Vector3(Jitter(id, 140 + i) * 12, Jitter(id, 170 + i) * 360, 0));
        }
    }

    /// <summary>Anvil bolted to a welded square-tube stand.</summary>
    private static void Anvil(Prop prop) {
        var body = prop.Body;
        var tube = Mat("5d6a4f", .6f, .4f);
        foreach (var (x, z) in new[] { (-.2f, -.15f), (.2f, -.15f), (-.2f, .15f), (.2f, .15f) }) Box(body, new Vector3(.06f, .46f, .06f), new Vector3(x * 1.1f, .23f, z * 1.1f), tube);
        Box(body, new Vector3(.52f, .05f, .4f), new Vector3(0, .47f, 0), tube);
        Box(body, new Vector3(.5f, .04f, .05f), new Vector3(0, .12f, .17f), tube);
        Add(body, AnvilShape.Casting, AnvilShape.Iron, Vector3.Zero);
        prop.Vent = new Vector3(0, .86f, 0);
    }

    /// <summary>A village market post with a mailbox, framed chalkboard and shipping storage.</summary>
    private static void Signpost(Prop prop) {
        var body = prop.Body;
        Add(body, SignpostShape.Timber, SignpostShape.Wood, Vector3.Zero);
        Add(body, SignpostShape.Mailbox, SignpostShape.Steel, Vector3.Zero);
        var flag = Pivot(body, new Vector3(-.49f, 1.0f, .1f));
        Add(flag, SignpostShape.Flag, SignpostShape.Steel, Vector3.Zero);
        prop.Flag = flag;
        var crate = Pivot(body, new Vector3(-.63f, .07f, .02f));
        Add(crate, CrateShape.Body, CrateShape.Timber, Vector3.Zero);
        Add(crate, CrateShape.Lid, CrateShape.Timber, new Vector3(0, .42f, -.23f));
        var openCrate = Pivot(body, new Vector3(-.61f, .55f, -.015f), new Vector3(0, -8, 0));
        openCrate.Scale = Vector3.One * .72f;
        Add(openCrate, CrateShape.Body, CrateShape.Timber, Vector3.Zero);
        body.AddChild(new Label3D {
            Text = "PRICES\n$  -  $$\n$$ - $$$\n$  -  $$",
            Position = new Vector3(.43f, .9f, .112f),
            FontSize = 40,
            PixelSize = .0017f,
            Modulate = new Color("e2e5cf"),
            OutlineSize = 0,
            NoDepthTest = false,
            Shaded = true
        });
    }

    /// <summary>Fire pit made from an old steel wheel rim, with a log teepee and flickering flames.</summary>
    private static void FirePit(Prop prop) {
        var body = prop.Body;
        var rim = Mat("3d3f42", .55f, .6f);
        Cylinder(body, .46f, .44f, .22f, new Vector3(0, .11f, 0), rim, null, 16);
        Torus(body, .42f, .5f, new Vector3(0, .22f, 0), rim);
        Cylinder(body, .4f, .4f, .02f, new Vector3(0, .23f, 0), Mat("4d4440", 1), null, 14);
        for (int i = 0; i < 4; i++) {
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

    /// <summary>Needle roll in degrees for a heat fraction: cold points left, full heat points right.</summary>
    public static float Gauge(float heat) => 110 - 220 * Math.Clamp(heat, 0, 1);

    /// <summary>Turns a mine's local +Z along its rails, out of the cliff's end.</summary>
    public static Vector3 MineYaw(Vector2 p) {
        var way = World.MineOut(World.CliffAt(p));
        return new Vector3(0, MathF.Atan2(way.X, way.Y), 0);
    }

    /// <summary>Ore color in a mine cart: rusty iron, black coal, and pale grey for anything else.</summary>
    private static string OreColor(string ore) => ore switch { "iron" => "a35a2e", "coal" => "26282b", _ => "b9bbb4" };

    /// <summary>The portal, this far behind a mine's spot along its rails; the cart stands at the spot (see <see cref="World.MineSpot"/>).</summary>
    public const float MineDoor = -2.2f;
    /// <summary>How far the tunnel runs into the spur before it is black.</summary>
    public const float MineDepth = 3;

    /// <summary>
    /// The rock site beside a cliff, built in that cliff's face-aligned mine frame: rock to both sides of and over a cave mouth, so a mine's tunnel can run into it.
    /// Part of the scenery, so every cliff has one whether or not it is mined.
    /// </summary>
    public static void MineSpur(Node3D parent, Cliff cliff, string[] rock) {
        var spot = World.MineSpot(cliff);
        var spur = Pivot(parent, new Vector3(spot.X, 0, spot.Y));
        spur.Rotation = MineYaw(spot);
        Add(spur, MineRockShape.Mesh(cliff, rock), MineRockShape.Rock, Vector3.Zero);
    }

    /// <summary>A timbered adit into the cliff's spur, local +Z out of the rock, with rails running in and a cart standing on them at the origin.</summary>
    private static void Mine(Prop prop, string ore) {
        var body = prop.Body;
        const float Door = MineDoor;
        // The marking: a see-through green footprint and portal, shown until the mine is finished.
        var outline = Pivot(body, Vector3.Zero);
        var green = Flat(new Color(.45f, .95f, .4f, .45f));
        Add(outline, MineShape.Outline, green, Vector3.Zero);
        prop.Outline = outline;
        // The dug tunnel: unlit floor, walls and roof that darken step by step to black, so the light never reaches its end.
        // It deepens from the portal as the entrance is dug; a spoil patch lies in front.
        var hole = Pivot(body, new Vector3(0, 0, Door));
        Add(hole, MineShape.Tunnel, MineShape.Unlit, Vector3.Zero);
        Add(hole, MineShape.Pad, MineShape.Timber, Vector3.Zero);
        prop.Hole = hole;
        // Delivered materials wait beside the entrance: a log pile, stones and a few iron bars.
        var supplies = Pivot(body, Vector3.Zero);
        for (int i = 0; i < 5; i++) Cylinder(supplies, .11f, .11f, .9f, new Vector3(1.3f + (i % 3) * .23f + (i > 2 ? .11f : 0), .11f + (i > 2 ? .19f : 0), -.6f), Mat(i % 2 == 0 ? "8a5f3c" : "7a5235"), new Vector3(90, 0, 0), 7);
        for (int i = 0; i < 4; i++) Sphere(supplies, .17f, new Vector3(-1.35f + (i % 2) * .26f, .1f + (i / 2) * .18f, -.2f + (i % 2) * .1f), Mat("a3a7a3", .9f), new Vector3(1.2f, .7f, 1), 6);
        for (int i = 0; i < 3; i++) Box(supplies, new Vector3(.12f, .06f, .5f), new Vector3(-1.3f, .03f + i * .06f, -1f), Mat("c2c9c9", .4f, .6f), new Vector3(0, i * 12, 0));
        prop.Supplies = supplies;
        // Rough timber sets: a weathered portal with a plank hood and a warning board, then sets down the tunnel fading into the dark.
        var frame = Pivot(body, new Vector3(0, 0, Door));
        Add(frame, MineShape.Portal, MineShape.Timber, Vector3.Zero);
        Add(frame, MineShape.Supports, MineShape.Unlit, Vector3.Zero);
        // A lantern hangs from the header on a hook; its flame and light burn only while someone works.
        var lantern = Pivot(frame, new Vector3(.55f, 1.46f, .32f));
        var iron = Mat("2f3134", .5f, .6f);
        Box(lantern, new Vector3(.03f, .3f, .03f), new Vector3(0, .3f, -.08f), iron);
        Box(lantern, new Vector3(.03f, .03f, .14f), new Vector3(0, .44f, -.02f), iron);
        Cylinder(lantern, .04f, .1f, .07f, new Vector3(0, .2f, 0), iron, null, 6);
        Cylinder(lantern, .1f, .1f, .04f, new Vector3(0, -.02f, 0), iron, null, 6);
        Box(lantern, new Vector3(.13f, .18f, .13f), new Vector3(0, .08f, 0), Mat("6d6658", .3f));
        var flame = Pivot(lantern, Vector3.Zero);
        Box(flame, new Vector3(.135f, .185f, .135f), new Vector3(0, .08f, 0), Flat(new Color("ffcf6a")));
        prop.Fire = flame;
        prop.Light = Light(lantern, new Vector3(0, .08f, .2f), 5);
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
        Add(load, MineShape.Load, Mat(OreColor(ore), .8f), Vector3.Zero);
        prop.Load = load;
        prop.Cart = cart;
        // Lamplight deep inside while someone works underground: a faint warm haze at the dark end.
        prop.Glow = Box(body, new Vector3(1.4f, 1.7f, .02f), new Vector3(0, .86f, Door - 2.5f), Flat(new Color(1, .55f, .2f, .25f)));
    }

    private static OmniLight3D Light(Node3D parent, Vector3 at, float range) {
        var light = new OmniLight3D { Position = at, OmniRange = range, LightColor = new Color("ff9a45"), LightEnergy = 0, ShadowEnabled = false, OmniAttenuation = 1.4f };
        parent.AddChild(light);
        return light;
    }

    /// <summary>Wolf with four hinged legs, built facing +Z; <see cref="Body"/> lifts and tilts for leaps, feeding and death.</summary>
    public sealed class WolfModel {
        public readonly Node3D Root = new();
        public readonly Node3D Body;
        /// <summary>Shoulder and hip pivots: front left, front right, hind left, hind right.</summary>
        public readonly Node3D[] Legs = new Node3D[4];
        public readonly Node3D Head, Tail;
        public float Stride, Yaw;
        public Vector2 Last;
        // Each leg bends once: the front wrist folds the paw back and the hind hock folds it forward.
        private readonly Skeleton3D[] shins = new Skeleton3D[4];
        private readonly Skeleton3D tailTip;
        private static readonly Vector3[] Hips = { new(.11f, .5f, .32f), new(-.11f, .5f, .32f), new(.11f, .5f, -.32f), new(-.11f, .5f, -.32f) };

        public WolfModel() {
            Body = Pivot(Root, Vector3.Zero);
            var dark = Mat("353c36");
            Add(Body, WolfShape.Body, WolfShape.Fur, Vector3.Zero);
            Head = Pivot(Body, new Vector3(0, .78f, .48f));
            Add(Head, WolfShape.Head, WolfShape.Fur, Vector3.Zero);
            Sphere(Head, .038f, new Vector3(0, -.027f, .366f), dark, new Vector3(1, .7f, .65f), 8);
            foreach (float side in new[] { -1f, 1f }) {
                Sphere(Head, .023f, new Vector3(side * .095f, .05f, .132f), dark, new Vector3(.7f, .75f, 1), 8);
                Sphere(Head, .012f, new Vector3(side * .111f, .05f, .136f), Mat("d49a50", .4f, 0, .5f), null, 8);
                Post(Head, .012f, 0, .045f, new Vector3(side * .035f, -.09f, .31f), Mat("f2efe6", .4f), 4).RotationDegrees = new Vector3(180, 0, 0);
            }
            Tail = Pivot(Body, new Vector3(0, .7f, -.48f));
            tailTip = WolfShape.Tail(Tail);
            for (int i = 0; i < 4; i++) {
                Legs[i] = Pivot(Body, Hips[i]);
                shins[i] = WolfShape.Leg(Legs[i], i >= 2);
            }
        }

        /// <summary>
        /// Front legs swing <paramref name="front"/> degrees and hind legs <paramref name="hind"/>; positive swings a foot back.
        /// The bends fold each front paw back at the wrist and each hind paw forward at the hock.
        /// </summary>
        private void Pose(float front, float hind, float head, float tail, float lift = 0, float pitch = 0, float roll = 0, float shift = 0, float frontBend = 8, float hindBend = 10) {
            Body.Position = new Vector3(shift, lift, 0);
            Body.RotationDegrees = new Vector3(pitch, 0, roll);
            for (int i = 0; i < 4; i++) Leg(i, i < 2 ? front : hind, i < 2 ? frontBend : hindBend);
            Head.RotationDegrees = new Vector3(head, 0, 0);
            TailAt(tail, 0, 0);
        }

        private void Leg(int i, float swing, float bend) {
            Legs[i].Position = Hips[i];
            Legs[i].RotationDegrees = new Vector3(swing, 0, 0);
            float fold = Mathf.DegToRad(i < 2 ? Math.Max(0, bend) : -Math.Max(0, bend));
            shins[i].SetBonePoseRotation(1, new Quaternion(Vector3.Right, fold));
        }

        /// <summary><paramref name="lift"/> is the tail's angle from hanging straight down; the tip curls on by <paramref name="curl"/>.</summary>
        private void TailAt(float lift, float wag, float curl) {
            Tail.RotationDegrees = new Vector3(lift + 180, wag, 0);
            tailTip.SetBonePoseRotation(1, new Quaternion(Vector3.Right, Mathf.DegToRad(curl)));
        }

        /// <summary>Standing: slow breaths, the head looking about and the tail swaying; <paramref name="time"/> is offset per wolf.</summary>
        public void Idle(float time) {
            float breath = MathF.Sin(time * 2.4f);
            Pose(-2, 4, MathF.Sin(time * .55f) * 6 - 4 + breath, -128, .004f * breath, breath * .6f, 0, 0, 6, 12);
            Head.RotationDegrees += new Vector3(0, MathF.Sin(time * .37f) * 22 + MathF.Sin(time * 1.3f) * 4, MathF.Sin(time * .37f) * 4);
            // Weight shifts from one hind leg to the other now and then.
            float settle = MathF.Sin(time * .23f) * 4;
            Leg(2, 4 + settle, 12 + Math.Max(0, settle) * 3); Leg(3, 4 - settle, 12 + Math.Max(0, -settle) * 3);
            TailAt(-128 + MathF.Sin(time * .8f) * 4, MathF.Sin(time * 1.1f) * 9, 14 + MathF.Sin(time * 1.1f + .8f) * 8);
        }

        /// <summary>A trot for stalking and roaming: diagonal pairs swing together and fold their paws as they come forward.</summary>
        public void Trot(float stride) {
            float bob = MathF.Cos(stride * 2);
            Pose(0, 0, 9 + bob * 3, -118, .012f + .012f * bob, bob * 1.5f, MathF.Sin(stride) * 2.5f);
            for (int i = 0; i < 4; i++) {
                // Front left with hind right, front right with hind left.
                float beat = stride + (i is 0 or 3 ? 0 : Mathf.Pi), lift = Math.Max(0, -MathF.Cos(beat));
                Leg(i, MathF.Sin(beat) * 30, i < 2 ? 10 + lift * 70 : 12 + lift * 55);
            }
            // A running wolf carries its head low and level, like it is hunting.
            Head.RotationDegrees += new Vector3(0, MathF.Sin(stride) * 3, 0);
            TailAt(-118 + bob * 4, MathF.Sin(stride) * 12, 18 + MathF.Sin(stride + .8f) * 10);
        }

        /// <summary>A bounding gallop for a chase: forelegs reach as the hind legs drive, the back rocking and the body lifting between strides.</summary>
        public void Gallop(float stride) {
            float rock = MathF.Cos(stride), flight = Math.Max(0, MathF.Sin(stride));
            // The head stays level against the rocking spine.
            Pose(0, 0, 18 - rock * 7, -96 + rock * 10, .03f + flight * .09f, rock * 10, MathF.Sin(stride * .5f) * 1.5f);
            for (int i = 0; i < 4; i++) {
                // Each pair lands a beat apart, left before right.
                float beat = stride + (i % 2) * .5f;
                if (i < 2) Leg(i, -MathF.Sin(beat) * 52, 12 + Math.Max(0, MathF.Cos(beat)) * 95);
                else Leg(i, MathF.Sin(beat + .9f) * 48, 14 + Math.Max(0, -MathF.Cos(beat + .9f)) * 70);
            }
            TailAt(-96 + rock * 10, MathF.Sin(stride * .5f) * 6, 8 - rock * 10);
        }

        /// <summary>A leap at the throat: <paramref name="progress"/> runs 0 to 1 from take-off to landing.</summary>
        public void Leap(float progress) {
            float t = Math.Clamp(progress, 0, 1), arc = MathF.Sin(t * Mathf.Pi);
            // Hind legs drive out straight at take-off, the forelegs tuck, then reach out with the jaws for the landing.
            float reach = Ease(Math.Clamp((t - .15f) / .6f, 0, 1));
            Pose(Mathf.Lerp(-40, -85, reach), Mathf.Lerp(70, 40, t), Mathf.Lerp(-18, 30, Ease(t)), -88 + arc * 10, arc * .75f, Mathf.Lerp(-28, 26, t),
                0, 0, Mathf.Lerp(110, 12, reach), Mathf.Lerp(4, 60, Ease(Math.Clamp((t - .3f) / .7f, 0, 1))));
            TailAt(-88 + arc * 10, 0, -10 + arc * 15);
        }

        /// <summary>Head down and tearing, legs braced; <paramref name="time"/> drives the shaking.</summary>
        public void Feed(float time) {
            // A hard pull back every so often, with quicker shakes of the head between.
            float pull = MathF.Pow(Math.Max(0, MathF.Sin(time * 2.6f)), 3);
            Pose(-14 + pull * 6, 22 - pull * 10, 48 + MathF.Sin(time * 9) * 10 - pull * 14, -150, -.1f, 14 - pull * 6, MathF.Sin(time * 4.5f) * 2, 0, 30, 34 + pull * 16);
            Body.Position += new Vector3(0, 0, -.05f * pull);
            Head.RotationDegrees += new Vector3(0, MathF.Sin(time * 13) * 18 * (1 - pull * .6f), MathF.Sin(time * 13) * 6);
            TailAt(-150 + MathF.Sin(time * 1.7f) * 5, MathF.Sin(time * 2.1f) * 14, 10);
            // Plant each paw while the chest pitches down; the upper profiles stay buried in the shoulders/hips.
            for (int i = 0; i < 4; i++) {
                float lowest = Math.Min(Paw(i, new Vector3(0, -.49f, -.03f)).Y, Paw(i, new Vector3(0, -.49f, .12f)).Y);
                Legs[i].Position += Vector3.Up * ((.01f - lowest) / Body.Basis.Y.Y);
            }
        }

        /// <summary>A point on a leg's paw, in the root's space after the wrist or hock bend.</summary>
        private Vector3 Paw(int i, Vector3 point) {
            float joint = WolfShape.Joint(i >= 2);
            var bone = new Transform3D(new Basis(shins[i].GetBonePoseRotation(1)), new Vector3(0, -joint, 0));
            return Body.Transform * (Legs[i].Transform * (bone * (point + new Vector3(0, joint, 0))));
        }

        /// <summary>A killed wolf buckles, then rolls onto its side with stiff legs as <paramref name="progress"/> runs 0 to 1.</summary>
        public void Fall(float progress) {
            float buckle = Ease(Math.Clamp(progress / .35f, 0, 1)), roll = Ease(Math.Clamp((progress - .25f) / .5f, 0, 1));
            float fold = 45 * buckle * (1 - roll), twitch = progress < 1 ? MathF.Sin(progress * 40) * 6 * roll * (1 - roll) : 0;
            // Rolling about the ground point would sink the body, so it shifts back over its old footprint as it goes.
            Pose(-fold - 22 * roll + twitch, fold + 26 * roll - twitch, 35 * buckle - 15 * roll, -150 + 40 * roll, -.22f * buckle * (1 - roll) + .2f * roll, 0, 90 * roll, .62f * roll,
                8 + 70 * buckle * (1 - roll), 10 + 60 * buckle * (1 - roll));
            TailAt(-150 + 40 * roll, 0, 10 + 20 * buckle * (1 - roll));
        }

        private static float Ease(float t) => t * t * (3 - 2 * t);
    }

    /// <summary>Courier pulling a two-wheel garden cart; the cart load shows its crates, loose goods or the payment cash box.</summary>
    public sealed class CartModel {
        public readonly Node3D Root = new();
        public readonly ActorRig Courier = new(ActorSkin.Courier);
        public readonly Node3D Goods, Cash;
        /// <summary>One crate model per market slot: four on the bed and two stacked on top.</summary>
        public readonly Node3D[] Crates = new Node3D[World.MarketSlots];
        private readonly Node3D[] wheels = new Node3D[2];
        public float Distance, Yaw;
        public Vector2 Last;

        public CartModel() {
            Root.AddChild(Courier.Root);
            Courier.Root.Position = new Vector3(0, 0, .55f);
            var cart = Pivot(Root, new Vector3(0, 0, -.45f));
            var green = Mat("3f6b3a", .55f, .3f);
            Box(cart, new Vector3(.7f, .3f, .85f), new Vector3(0, .5f, 0), green);
            Box(cart, new Vector3(.62f, .02f, .78f), new Vector3(0, .66f, 0), Mat("2d4a2a", .7f));
            for (int i = 0; i < 2; i++) {
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
            for (int i = 0; i < Crates.Length; i++) {
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
        public void Load(int crates, bool goods, bool cash) {
            for (int i = 0; i < Crates.Length; i++) Crates[i].Visible = !cash && i < crates;
            Goods.Visible = goods && !cash && crates == 0; Cash.Visible = cash;
        }

        public void Roll(float distance) {
            foreach (var wheel in wheels) wheel.RotationDegrees = new Vector3(Mathf.RadToDeg(distance / .26f), 0, 0);
        }
    }

}
