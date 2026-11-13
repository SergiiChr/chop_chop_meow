using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>A vertical hit cylinder in model space; picking and canopy fading use the same volumes.</summary>
public readonly record struct Hit(float Radius, float Bottom, float Top);

/// <summary>A built world object; subtypes hold the moving parts of each kind that <see cref="WorldView"/> animates from simulation state.</summary>
public class Prop {
    public readonly Node3D Root = new();
    public readonly Node3D Body;
    public Hit[] Hits = Array.Empty<Hit>();
    public ThingKind Kind;
    public float Shake;
    /// <summary>Whether a build-mode ghost is tinted as placeable, or null before its first tint.</summary>
    public bool? Tint;
    public bool Seen;
    /// <summary>What the model was last drawn from: where its thing stood, what of it shows, and whether it was in sight.</summary>
    public (Vector2 Pos, int Remaining, float Progress, MineStage Mine, FarmStage Farm, bool Night, bool Live) Look;
    /// <summary>Whether the model is drawn, kept here so it is set in Godot only when it changes.</summary>
    public bool Shown = true;
    public Prop() { Body = Pivot(Root, Vector3.Zero); }
}

public sealed class TreeProp : Prop {
    /// <summary>The trunk and crown, which shake and fall about the base.</summary>
    public Node3D Sway = null!;
    /// <summary>The crown's meshes and their own materials, for fading it without walking its nodes.</summary>
    public (MeshInstance3D Mesh, Material Material)[] SwayParts = Array.Empty<(MeshInstance3D, Material)>();
    /// <summary>Seconds since the tree was felled, or below zero while it stands; its stump is a thing of its own.</summary>
    public float Fall = -1;
    /// <summary>How tall the tree stands among trees, from 0 to 1 (<see cref="WorldModels.TreeTall"/>).</summary>
    public float Tall;
    /// <summary>How see-through the crown is while it hides something behind it.</summary>
    public float Fade;
}

/// <summary>A loose stone, stick or chip; a chip knocked off rock flies there from where it was hit.</summary>
public sealed class LooseProp : Prop {
    /// <summary>Where a chip flew from, relative to where it lands.</summary>
    public Vector3 From;
    /// <summary>Seconds into the chip's flight, or past <see cref="WorldModels.ChipFlight"/> once it lies still.</summary>
    public float Flight = float.MaxValue;
}

/// <summary>A stump or a bush, which leans further out of the ground the more it is dug at, then comes out and is gone.</summary>
public sealed class DugProp : Prop {
    /// <summary>The turn about the vertical it was built with, in degrees.</summary>
    public float Yaw;
    /// <summary>Seconds since it came out of the ground, or below zero while it is rooted.</summary>
    public float Out = -1;
    /// <summary>Where it stands from its thing's spot: a stump stands where its tree's trunk did, which is off the middle of the tree.</summary>
    public Vector3 Seat;
}

public sealed class BushProp : Prop {
    public Node3D Fruit = null!;
}

public sealed class CarcassProp : Prop {
    public WolfModel Wolf = null!;
    public MeshInstance3D Pool = null!;
    /// <summary>Where the wolf stood when it was killed, from the carcass, and how long after the carcass appeared the killing blow landed.</summary>
    public Vector3 From;
    public float HitIn;
    /// <summary>Whether the killing blow has landed, so its thud plays once.</summary>
    public bool Landed;
}

/// <summary>A chest, crate or dropped backpack, whose lid opens while it is used.</summary>
public sealed class BoxProp : Prop {
    public Node3D Lid = null!;
    /// <summary>The bobbing marker over a dropped backpack.</summary>
    public Node3D? Marker;
    public float Open;
    /// <summary>Seconds the lid stays open after the contents changed.</summary>
    public float Puff;
    public int LastCount;
}

/// <summary>A station with a fire, whose smoke rises from <see cref="Vent"/>.</summary>
public abstract class HearthProp : Prop {
    public OmniLight3D Light = null!;
    public Vector3 Vent;
    /// <summary>Seconds until the next puff of smoke.</summary>
    public float Smoke;
}

public sealed class CampfireProp : HearthProp {
    public Node3D Fire = null!;
}

public sealed class FurnaceProp : HearthProp {
    public MeshInstance3D Glow = null!;
    /// <summary>The stocked fuel beside the furnace.</summary>
    public StockPile Stock = null!;
}

/// <summary>A cooking table or canning station.</summary>
public sealed class StoveProp : HearthProp {
    public MeshInstance3D Glow = null!;
    /// <summary>The cooking table's mortar, shown while one is slotted and nobody grinds with it.</summary>
    public Node3D? Mortar;
}

public sealed class ForgeProp : HearthProp {
    public MeshInstance3D Glow = null!;
    public Node3D Needle = null!, Lever = null!;
    public Node3D[] Pleats = Array.Empty<Node3D>();
}

public sealed class AnvilProp : Prop {
    /// <summary>Where sparks fly from a hammer blow.</summary>
    public Vector3 Vent;
}

public sealed class HutProp : Prop {
    public Node3D Door = null!;
    public MeshInstance3D Window = null!;
    public OmniLight3D Light = null!;
    public float Open;
    /// <summary>Whether the hut shows its night window glow and lamp.</summary>
    public bool Lit;
}

public sealed class BenchProp : Prop {
    public Node3D Chisel = null!, Hammer = null!;
}

public sealed class PileProp : Prop {
    /// <summary>The heap of the good the pile holds.</summary>
    public StockPile Stock = null!;
}

public sealed class FarmProp : Prop {
    /// <summary>The plot's looks by <see cref="World.CropLook"/> from 1: sown soil, small straws, tall green plants and ripe wheat; one shows at a time over the soil.</summary>
    public Node3D[] Crops = Array.Empty<Node3D>();
}

public sealed class PackerProp : Prop {
    public Node3D[] Crates = Array.Empty<Node3D>();
    public Node3D Packing = null!;
    public StockPile Stock = null!;
}

public sealed class RackProp : Prop {
    /// <summary>One peg per tool slot, each with a hanging tool of every kind it can show; the view shows the one its slot holds.</summary>
    public readonly Dictionary<string, Node3D>[] Pegs = new Dictionary<string, Node3D>[World.ToolSlots];
}

public sealed class SignpostProp : Prop {
    public Node3D Flag = null!;
    public float Open;
}

/// <summary>A mine's parts, shown stage by stage: the marked outline, the dug hole, the delivered materials, the timber frame, the rails and the cart with its load.</summary>
public sealed class MineProp : Prop {
    public Node3D Outline = null!, Hole = null!, Supplies = null!, Frame = null!, Rails = null!, Cart = null!, Load = null!, Fire = null!;
    public MeshInstance3D Glow = null!;
    public OmniLight3D Light = null!;
}

/// <summary>Code-built low-poly models for every world object: natural, modern and DIY rather than fantasy.</summary>
public static class WorldModels {
    public const int TreeVariants = 3;
    /// <summary>The render layer of ground marks, which icon renders leave out.</summary>
    public const uint MarkLayer = 1 << 12;

    /// <summary>
    /// Builds one model of every kind in each of its looks, a mine at every ore and a wolf under <paramref name="stage"/> while the world loads, so their meshes, materials, shaders and faded copies are made.
    /// The view draws the stage once, which builds what drawing needs, so a building, crate, carcass, dropped backpack or wolf that first appears mid-game only adds nodes over what is ready.
    /// </summary>
    public static void Warm(Node3D stage) {
        foreach (var kind in Kinds.All) {
            if (kind == ThingKind.Mine) continue;
            // Natural things and farm plots look different by id; buildings, crates and backpacks look the same.
            int looks = kind == ThingKind.Tree ? TreeLooks : new Thing { Kind = kind }.IsResource || kind == ThingKind.Farm ? NatureShape.Variants : 1;
            for (int id = 0; id < looks; id++) Stage(Build(new Thing { Id = id, Kind = kind }).Root);
        }
        foreach (var ore in World.MineOres) Stage(Build(new Thing { Kind = ThingKind.Mine, Pos = World.MineSpot(World.CliffOf(ore)) }).Root);
        Stage(new WolfModel().Root);
        void Stage(Node3D root) { ModelKit.BuildShaders(root); root.Position = Vector3.Zero; stage.AddChild(root); }
        _ = BackpackShape.Details; _ = BackpackShape.Flap;
        for (int variant = 0; variant < LeafShape.Variants; variant++) LeafShape.For(variant);
        scorchTexture ??= ScorchTexture();
    }
    /// <summary>How full a resource is against its size from world generation; the view shrinks deposits toward 55% as they are worked.</summary>
    public static float Fullness(Thing t) => t.Kind switch {
        ThingKind.Stone => t.Remaining / (float)World.StonePile,
        ThingKind.Iron or ThingKind.Coal or ThingKind.Tin => t.Remaining / (float)World.DepositOre,
        ThingKind.WolfCorpse => t.Remaining / (float)World.WolfMeat,
        ThingKind.Leaves => t.Remaining / (float)World.LeafPatch,
        _ => 1
    };

    /// <summary>Stable pseudo-random value in [0, 1) so a thing looks the same after every load.</summary>
    public static float Jitter(int id, int salt) {
        uint h = (uint)(id * 73856093) ^ (uint)(salt * 19349663);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return (h & 0xffff) / 65536f;
    }

    /// <summary>How many looks a tree has: every species in every variant.</summary>
    public const int TreeLooks = TreeVariants * NatureShape.Variants;
    /// <summary>A tree's species and its variant of that species, by its id.</summary>
    public static (int Species, int Variant) TreeLook(int id) => (id % TreeVariants, id / TreeVariants % NatureShape.Variants);
    /// <summary>The turn about the vertical, in degrees, a tree or another resource is drawn with, by its id.</summary>
    private static float Turn(int id) => Jitter(id, 1) * 360;
    /// <summary>How tall a tree stands among trees by its id, from 0 for the shortest to 1 for the tallest.</summary>
    public static float TreeTall(int id) => Jitter(id, 2);
    /// <summary>A tree's size against its baked model: the tallest stand two thirds taller than the shortest.</summary>
    public static float TreeScale(int id) => .9f + TreeTall(id) * .6f;

    /// <summary>A tree's trunk and crown, sized with the tree.</summary>
    private static Hit[] TreeHits(int id) {
        var (trunk, crown, fork, top) = TreeLook(id).Species switch {
            1 => (.3f, 1.25f, 1.2f, 5.6f),
            2 => (.25f, 1.05f, 2.6f, 5.4f),
            _ => (.35f, 1.8f, 2.5f, 5.3f),
        };
        float scale = TreeScale(id);
        return new[] { new Hit(trunk * scale, 0, fork * scale), new Hit(crown * scale, fork * scale, top * scale) };
    }

    public static Hit[] Hits(Thing t) => t.Kind switch {
        ThingKind.Tree => TreeHits(t.Id),
        ThingKind.Stone => new[] { new Hit(.75f, 0, .9f) },
        ThingKind.Iron or ThingKind.Coal or ThingKind.Tin => new[] { new Hit(.8f, 0, 1) },
        ThingKind.LooseCoal or ThingKind.LooseStone or ThingKind.IronChip or ThingKind.TinChip => new[] { new Hit(.45f, 0, .35f) },
        ThingKind.Stick => new[] { new Hit(.5f, 0, .3f) },
        ThingKind.WolfCorpse => new[] { new Hit(.65f, 0, .5f) },
        ThingKind.Berries => new[] { new Hit(.65f, 0, 1) },
        ThingKind.Stump => new[] { new Hit(.4f, 0, .4f) },
        ThingKind.Bush => new[] { new Hit(.6f, 0, .6f) },
        ThingKind.Water => new[] { new Hit(1.2f, 0, .5f) },
        ThingKind.Furnace => new[] { new Hit(.72f, 0, 1.15f) },
        ThingKind.Reinforced => new[] { new Hit(.95f, 0, 1.45f) },
        ThingKind.Hut => new[] { new Hit(1.6f, 0, 3.1f) },
        ThingKind.Chest => new[] { new Hit(.5f, 0, .65f) },
        ThingKind.Crate => new[] { new Hit(.4f, 0, .55f) },
        ThingKind.Bench => new[] { new Hit(.6f, 0, .95f) },
        ThingKind.Cooktable => new[] { new Hit(.7f, 0, .9f) },
        ThingKind.Cannery => new[] { new Hit(.6f, 0, 1f) },
        ThingKind.Packer => new[] { new Hit(1, 0, 1.1f) },
        ThingKind.Toolstation => new[] { new Hit(.55f, 0, 1.5f) },
        ThingKind.Pile => new[] { new Hit(1.5f, 0, .5f) },
        ThingKind.Farm => new[] { new Hit(1.5f, 0, .5f) },
        ThingKind.Leaves => new[] { new Hit(.6f, 0, .15f) },
        ThingKind.Grave => new[] { new Hit(.65f, 0, 1.5f) },
        ThingKind.Anvil => new[] { new Hit(.45f, 0, .85f) },
        ThingKind.Signpost => new[] { new Hit(1.02f, 0, 1.98f) },
        ThingKind.Campfire => new[] { new Hit(.55f, 0, .7f) },
        ThingKind.Mine => new[] { new Hit(.9f, 0, 1.8f) },
        _ => new[] { new Hit(.5f, 0, .8f) },
    };

    public static Prop Build(Thing t) {
        Prop prop;
        switch (t.Kind) {
            case ThingKind.Tree: prop = Tree(t.Id); break;
            case ThingKind.Stump: prop = Stump(t.OfTree != 0 ? t.OfTree : t.Id); break;
            case ThingKind.Bush: prop = new DugProp(); Shrub(prop.Body, t.Id); break;
            case ThingKind.Stone: prop = new Prop(); Rocks(prop.Body, t.Kind, t.Id, "8d918e"); break;
            case ThingKind.Iron: prop = new Prop(); Rocks(prop.Body, t.Kind, t.Id, "7d5b45", "a8592b", "b4622a"); break;
            case ThingKind.Coal: prop = new Prop(); Rocks(prop.Body, t.Kind, t.Id, "50504b", "191d21", "0c0e10"); break;
            case ThingKind.Tin: prop = new Prop(); Rocks(prop.Body, t.Kind, t.Id, "858882", "b9bcb4", "e6e8e2"); break;
            case ThingKind.LooseCoal: prop = new LooseProp(); LooseOre(prop.Body, t.Kind.Id(), t.Id, "2d2f33", "1d1e21"); break;
            case ThingKind.LooseStone: prop = new LooseProp(); Add(prop.Body, NatureShape.Pebble(new Color("939184"), t.Id % NatureShape.Variants), NatureShape.Rock, Vector3.Zero, new Vector3(0, t.Id * 47 % 360, 0)); break;
            case ThingKind.IronChip: prop = new LooseProp(); LooseOre(prop.Body, "loose_iron", t.Id, "a35a2e", "8b4f2b"); break;
            case ThingKind.TinChip: prop = new LooseProp(); LooseOre(prop.Body, t.Kind.Id(), t.Id, "b9bcb4", "8f928b"); break;
            case ThingKind.Stick: prop = new LooseProp(); Stick(prop.Body, t.Id); break;
            case ThingKind.Leaves: prop = new Prop(); LeafLitter(prop.Body, t.Id); break;
            case ThingKind.WolfCorpse: prop = Carcass(); break;
            case ThingKind.Berries: prop = Bush(t.Id); break;
            // The lake is the drinking spot; this thing only provides the hit area.
            case ThingKind.Water: prop = new Prop(); break;
            case ThingKind.Furnace: prop = BlockFurnace(); break;
            case ThingKind.Reinforced: prop = DrumForge(); break;
            case ThingKind.Hut: prop = Cabin(); break;
            case ThingKind.Chest: prop = Chest(); break;
            case ThingKind.Grave: prop = DroppedBackpack(); break;
            case ThingKind.Anvil: prop = Anvil(); break;
            case ThingKind.Crate: prop = Crate(); break;
            case ThingKind.Bench: prop = Workbench(); break;
            case ThingKind.Cooktable: prop = CookingTable(); break;
            case ThingKind.Cannery: prop = Cannery(); break;
            case ThingKind.Packer: prop = PackingStation(); break;
            case ThingKind.Toolstation: prop = ToolRack(); break;
            case ThingKind.Pile: prop = Pile(); break;
            case ThingKind.Farm: prop = Farm(t.Id); break;
            case ThingKind.Signpost: prop = Signpost(); break;
            case ThingKind.Campfire: prop = FirePit(); break;
            case ThingKind.Mine: prop = Mine(World.CliffAt(t.Pos).Ore); break;
            default: throw new ArgumentOutOfRangeException(nameof(t), t.Kind, "No model for this kind.");
        }
        prop.Kind = t.Kind; prop.Hits = Hits(t);
        var body = prop.Body;
        if (t.Kind != ThingKind.Water) body.RotationDegrees = new Vector3(0, t.Kind == ThingKind.Tree || t.IsResource ? Turn(t.Id) : 0, 0);
        if (t.Kind == ThingKind.Mine) body.Rotation = MineYaw(t.Pos);
        if (prop is DugProp dug) dug.Yaw = body.RotationDegrees.Y;
        return prop;
    }

    /// <summary>
    /// The model an icon shows for an item or building plan, or null when the game has no model of its own for it.
    /// Items borrow the part of a model that shows them in the world; buildings show as placed, with a finished mine, a ripe farm and a pile full of logs.
    /// Berries show as a small heap of the bush's berries; planks, parts, bars and goods the world does not show as the stock a storage pile shows.
    /// The hard hat is the one a worker wears.
    /// </summary>
    public static Node3D? IconModel(string id, bool building, GameData data) {
        Node3D Part(ThingKind kind, Func<Prop, Node3D> part) {
            var prop = Build(new Thing { Kind = kind });
            var node = part(prop);
            node.GetParent().RemoveChild(node);
            node.Visible = true;
            prop.Root.Free();
            return node;
        }
        switch (id) {
            case "hardhat": { var hat = new Node3D(); ActorRig.HardHat(hat); return hat; }
            case "steel_axe" or "steel_pick" or "steel_shovel" or "stone_chisel" or "steel_chisel" or "wood_hammer" or "steel_hammer": return ActorRig.Tool(id);
            case "torch": var (torch, _, _) = ActorRig.Torch(); return torch;
            case "rake": return Part(ThingKind.Toolstation, p => ((RackProp)p).Pegs[0]["rake"]);
            case "mortar": return Part(ThingKind.Cooktable, p => ((StoveProp)p).Mortar!);
            case "berries": { var heap = new Node3D(); Add(heap, NatureShape.BerryHeap, NatureShape.Glossy, Vector3.Zero); return heap; }
            case "water" or "water_2" or "water_1" or "empty_bottle": return BottleShape.Create(Water.BottleSips(id));
            case "iron_bar" or "steel_bar" or "tin_bar": return IngotShape.Create(id, data.Items[id].Color);
            case "stone": return Build(new Thing { Kind = ThingKind.Stone }).Root;
            case "leaves": return Build(new Thing { Kind = ThingKind.Leaves }).Root;
            case "crate": return Build(new Thing { Kind = ThingKind.Crate }).Root;
            // Loose iron left the world, but its lump still makes the iron ore icon.
            case "iron_ore": { var lump = new Node3D(); LooseOre(lump, "loose_iron", 0, "a35a2e", "8b4f2b"); return lump; }
            case "charcoal": { var heap = new Node3D(); Add(heap, CharcoalShape.Heap, CharcoalShape.Material, Vector3.Zero); return heap; }
            case "coal" or "tin_ore": return Build(new Thing { Kind = id == "coal" ? ThingKind.LooseCoal : ThingKind.Tin }).Root;
            case "mine": {
                    var prop = Mine(ThingKind.Iron);
                    prop.Outline.Visible = prop.Supplies.Visible = prop.Glow.Visible = prop.Fire.Visible = false;
                    return prop.Root;
                }
        }
        // Everything else an item shows as is the stock a storage pile holds of it.
        if (id is "wood" or "planks" or "wood_parts" || !building && ItemShape.Has(id)) {
            var stock = new Node3D(); stock.AddChild(PileShape.Stock(id, data.Items[id].Color));
            return stock;
        }
        if (!building || Kinds.Parse(id) is not ThingKind kind) return null;
        var built = Build(new Thing { Kind = kind });
        // A pile and a furnace show logs, a packing station a heap of bread, and a farm ripe wheat over sown soil.
        switch (built) {
            case FurnaceProp furnace: furnace.Glow.Visible = false; furnace.Stock.Fill("wood", 1, data); break;
            case StoveProp stove: stove.Glow.Visible = false; break;
            case ForgeProp forge: forge.Glow.Visible = false; break;
            case FarmProp farm: farm.Crops[0].Visible = farm.Crops[^1].Visible = true; break;
            case PileProp pile: pile.Stock.Fill("wood", 1, data); break;
            case PackerProp packer: packer.Stock.Fill("bread", 1, data); break;
        }
        return built.Root;
    }

    /// <summary>The outdoor heap shows stocked fuel, excluding fuel already burning in a batch: a piece for each one, as many as the heap holds.</summary>
    public static void SyncFuel(FurnaceProp prop, Thing t, GameData data) {
        string fuel = World.ShownFuel(t, data);
        prop.Stock.Show(fuel, t.Burner?[fuel] ?? 0, data);
    }

    private static TreeProp Tree(int id) {
        var prop = new TreeProp();
        var sway = Pivot(prop.Body, Vector3.Zero);
        prop.Sway = sway; // the whole tree shakes and falls about its base
        prop.Tall = TreeTall(id);
        float scale = TreeScale(id);
        sway.Scale = Vector3.One * scale;
        var (species, variant) = TreeLook(id);
        var leaves = Tree3DShape.Add(sway, species, variant);
        PreserveShadows(sway);
        Tree3DShape.AddShadowCore(sway, species, leaves);
        prop.SwayParts = FadeParts(sway);
        return prop;
    }

    /// <summary>
    /// The stump a felled tree leaves: as thick as that tree's trunk was at the ground, and standing where the trunk stood.
    /// A stump with no tree on record is shaped as if a tree with the stump's own id had left it.
    /// </summary>
    private static DugProp Stump(int tree) {
        var prop = new DugProp();
        var stump = Add(prop.Body, NatureShape.Stump, NatureShape.Timber, Vector3.Zero);
        float scale = TreeScale(tree);
        var (species, variant) = TreeLook(tree);
        var (foot, radius) = Tree3DShape.Foot(species, variant);
        float width = radius * scale / NatureShape.StumpRadius;
        stump.Scale = new Vector3(width, scale, width);
        // The tree's model was turned by its id, and its trunk with it.
        prop.Seat = new Vector3(foot.X, 0, foot.Y).Rotated(Vector3.Up, Mathf.DegToRad(Turn(tree))) * scale;
        return prop;
    }

    /// <summary>A plain bush of three lobes around its middle, each a little different in size and green by its id; it casts a shadow.</summary>
    private static void Shrub(Node3D body, int id) {
        float size = .85f + Jitter(id, 3) * .4f;
        for (int lobe = 0; lobe < 3; lobe++) {
            float angle = lobe * Mathf.Tau / 3, radius = (.35f + (id + lobe * 3) % 7 * .05f) * size;
            Add(body, NatureShape.Scrub, NatureShape.ScrubMaterials[(id + lobe) % NatureShape.ScrubHues], new Vector3(MathF.Cos(angle) * .23f, radius * .35f, MathF.Sin(angle) * .23f),
                new Vector3(0, Mathf.RadToDeg(angle), 0), new Vector3(radius, radius * .75f, radius));
        }
    }

    /// <summary>A boulder group in the deposit's rock color; an ore deposit also shows veins across the rock faces and ore chunks set into it.</summary>
    private static void Rocks(Node3D body, ThingKind kind, int id, string stone, string? vein = null, string? chunks = null) {
        int variant = id % NatureShape.Variants;
        if (vein == null || chunks == null) {
            Add(body, NatureShape.Rocks(kind.Id(), new Color(stone), variant), NatureShape.Rock, Vector3.Zero);
            return;
        }
        Add(body, NatureShape.Ore(variant, false), NatureShape.OreMaterial(stone, .92f, 0), Vector3.Zero);
        float roughness = kind == ThingKind.Coal ? .58f : .4f, metallic = kind == ThingKind.Coal ? 0 : .3f;
        Add(body, NatureShape.Ore(variant, true), NatureShape.OreMaterial(vein, roughness, metallic), Vector3.Zero);
        Add(body, NatureShape.OreChips(variant), NatureShape.OreMaterial(chunks, roughness, metallic), Vector3.Zero);
    }

    /// <summary>How long a chip takes to fly from the rock it came off to where it lands.</summary>
    public const float ChipFlight = .55f;

    /// <summary>A fallen stick lying on the ground, turned by its id.</summary>
    private static void Stick(Node3D body, int id) {
        var stick = Pivot(body, new Vector3(0, .05f, 0), new Vector3(0, Jitter(id, 5) * 360, 0));
        Cylinder(stick, .045f, .03f, .8f, Vector3.Zero, Wood("78644a", .95f), new Vector3(0, 0, 90), 5);
        Cylinder(stick, .025f, .015f, .25f, new Vector3(.12f, .06f, .05f), Wood("6e5b43", .95f), new Vector3(30, 0, 60), 4);
    }

    private static void LooseOre(Node3D body, string kind, int id, string light, string dark) =>
        Add(body, NatureShape.Loose(kind, new Color(light), new Color(dark), id % NatureShape.Variants), NatureShape.Rock, Vector3.Zero);

    private static CarcassProp Carcass() {
        var prop = new CarcassProp();
        prop.Pool = Cylinder(prop.Body, .55f, .55f, .004f, new Vector3(.1f, .006f, 0), Flat(new Color(.36f, .05f, .05f, .75f), true), null, 14);
        prop.Wolf = new WolfModel();
        prop.Body.AddChild(prop.Wolf.Root);
        return prop;
    }

    private static BushProp Bush(int id) {
        var prop = new BushProp();
        int variant = id % NatureShape.Variants;
        var leaves = Tree3DShape.Add(prop.Body, Tree3DShape.Bush, variant);
        var fruit = Pivot(prop.Body, Vector3.Zero);
        Add(fruit, NatureShape.CardBerries(leaves, variant), NatureShape.Glossy, Vector3.Zero);
        prop.Fruit = fruit;
        return prop;
    }

    /// <summary>Cinder-block furnace with a steel top plate, stovepipe and stocked fuel pile.</summary>
    private static FurnaceProp BlockFurnace() {
        var prop = new FurnaceProp();
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
        // Stocked fuel piles beside the firebox: pile stock is centred and .16 tall at this scale.
        prop.Stock = new StockPile(body, new Vector3(.82f, 0, .01f), new Vector2(.46f, .62f), 300, .7f);
        prop.Light = Light(body, new Vector3(0, .35f, .7f), 2);
        Scorch(body, 1.9f);
        return prop;
    }

    /// <summary>A propane-tank forge on an angle-iron stand, fed by a hinged leather bellows with a heat gauge on top.</summary>
    private static ForgeProp DrumForge() {
        var prop = new ForgeProp();
        var body = prop.Body;
        var steel = Metal("3b3f42", .5f, .7f);
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
        var wood = Wood("8a5f3c");
        var leather = Mat("3b2619", .9f);
        foreach (float x in new[] { .95f, 1.4f }) Box(rig, new Vector3(.08f, .2f, .5f), new Vector3(x, .1f, 0), Wood("6f5a40"));
        Add(rig, ForgeShape.Board, wood, hinge + new Vector3(0, -.03f, 0));
        Cylinder(rig, .07f, .025f, .22f, new Vector3(.55f, .3f, 0), Metal("b8893a", .4f, .8f), new Vector3(0, 0, 90), 10);
        var lever = Pivot(rig, hinge);
        Add(lever, ForgeShape.Board, wood, new Vector3(0, .13f, 0));
        Box(lever, new Vector3(.06f, .62f, .06f), new Vector3(1f, .44f, 0), Wood("8a5a32"));
        Cylinder(lever, .03f, .03f, .34f, new Vector3(1f, .75f, 0), Mat("222222"), new Vector3(90, 0, 0), 8);
        prop.Lever = lever;
        // Each pleat hangs from the hinge and follows a share of the lid's swing, so the leather folds as it closes.
        prop.Pleats = new Node3D[3];
        for (int i = 0; i < 3; i++) {
            prop.Pleats[i] = Pivot(rig, hinge);
            float size = i % 2 == 0 ? .97f : .88f;
            Add(prop.Pleats[i], ForgeShape.Board, leather, new Vector3(0, -.03f + (i + 1) * .04f, 0), scale: new Vector3(size, 1, size));
        }
        prop.Light = Light(body, new Vector3(0, .9f, .9f), 2.2f);
        Scorch(body, 1.6f);
        return prop;
    }

    /// <summary>Small OSB cabin on block piers with a corrugated shed roof, solar panel and rain barrel.</summary>
    private static HutProp Cabin() {
        var prop = new HutProp();
        var body = prop.Body;
        foreach (var (x, z) in new[] { (-1.1f, -1f), (1.1f, -1f), (-1.1f, 1f), (1.1f, 1f) }) Box(body, new Vector3(.3f, .3f, .3f), new Vector3(x, .15f, z), Stone("9a9b97", .85f));
        Box(body, new Vector3(2.6f, .12f, 2.4f), new Vector3(0, .36f, 0), Wood("7d5f43"));
        Add(body, CabinShape.Walls, CabinShape.WallMaterial, Vector3.Zero);
        Add(body, CabinShape.Interior, CabinShape.WallMaterial, Vector3.Zero);
        var trim = Mat("3d4245");
        foreach (var (x, z) in new[] { (-1.2f, -1.1f), (1.2f, -1.1f), (-1.2f, 1f), (1.2f, 1f) }) {
            float height = CabinShape.WallTop(z) - .42f;
            Box(body, new Vector3(.08f, height, .08f), new Vector3(x, .42f + height / 2, z), trim);
        }
        foreach (float x in new[] { -.95f, .15f, 1.03f }) {
            float height = CabinShape.WallTop(1) - .42f;
            Box(body, new Vector3(.025f, height, .015f), new Vector3(x, .42f + height / 2, 1.005f), Wood("b08c55"));
        }
        foreach (float z in new[] { -.8f, .65f }) {
            float height = CabinShape.WallTop(z) - .42f;
            Box(body, new Vector3(.015f, height, .025f), new Vector3(1.205f, .42f + height / 2, z), Wood("b08c55"));
        }
        // Shed roof pitched toward the back, overhanging the door.
        var roof = Pivot(body, new Vector3(0, 2.68f, -.05f), new Vector3(-9, 0, 0));
        Add(roof, CabinShape.Roof, Metal("6f7a80", .45f, .6f), Vector3.Zero);
        Box(roof, new Vector3(1.1f, .05f, .8f), new Vector3(.55f, .085f, -.4f), Mat("1f2c44", .25f, .4f));
        Box(roof, new Vector3(1.14f, .03f, .84f), new Vector3(.55f, .065f, -.4f), Metal("c9ced2", .4f, .7f));
        Post(body, .08f, .08f, 1.1f, new Vector3(-.75f, 2.4f, -.6f), Metal("8e9396", .45f, .7f));
        Post(body, .14f, 0, .12f, new Vector3(-.75f, 3.48f, -.6f), Metal("8e9396", .45f, .7f), 8);
        // Door hinged on its left edge, painted teal.
        // The hinge axis sits just ahead of the jamb so the open leaf clears the frame.
        var door = Pivot(body, new Vector3(-.75f, .42f, 1.075f));
        Box(door, new Vector3(.82f, 1.86f, .05f), new Vector3(.41f, .93f, -.025f), Wood("3e6e6a", .6f));
        Sphere(door, .035f, new Vector3(.72f, .95f, .025f), Metal("c9ced2", .3f, .8f));
        prop.Door = door;
        foreach (float x in new[] { -.79f, .11f })
            Box(body, new Vector3(.06f, 1.93f, .1f), new Vector3(x, 1.385f, 1.015f), trim);
        Box(body, new Vector3(.96f, .06f, .1f), new Vector3(-.34f, 2.32f, 1.015f), trim);
        Box(body, new Vector3(1, .14f, .45f), new Vector3(-.34f, .07f, 1.375f), Wood("a9824f"));
        Box(body, new Vector3(1, .28f, .26f), new Vector3(-.34f, .14f, 1.25f), Wood("a9824f"));
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
        return prop;
    }

    private static BoxProp Chest() {
        var prop = new BoxProp();
        var body = prop.Body;
        Add(body, ChestShape.Body, ChestShape.Wood, Vector3.Zero);
        Add(body, ChestShape.Frame, ChestShape.Metal, Vector3.Zero);
        var hinge = Pivot(body, new Vector3(0, .49f, -.275f));
        Add(hinge, ChestShape.Lid, ChestShape.Wood, Vector3.Zero);
        Add(hinge, ChestShape.LidFrame, ChestShape.Metal, Vector3.Zero);
        prop.Lid = hinge;
        return prop;
    }

    /// <summary>
    /// The player's shared canvas pack laid on its straps, with an animated access flap.
    /// It is drawn larger than the worn pack, and a glowing gem bobs above it, so it stands out from grass and shows at night.
    /// </summary>
    private static BoxProp DroppedBackpack() {
        var prop = new BoxProp();
        var dropped = Pivot(prop.Body, Vector3.Zero, new Vector3(0, -18, 0));
        dropped.Scale = Vector3.One * 1.4f;
        prop.Lid = LaidBackpack(dropped);
        var marker = Pivot(prop.Root, new Vector3(0, 1.2f, 0));
        var gold = Mat("f2b33d", .4f, 0, 1.4f);
        Cylinder(marker, .13f, 0, .09f, new Vector3(0, .045f, 0), gold, sides: 4);
        Cylinder(marker, 0, .13f, .24f, new Vector3(0, -.12f, 0), gold, sides: 4);
        prop.Marker = marker;
        return prop;
    }

    /// <summary>The canvas pack laid on its straps under <paramref name="parent"/>, half as large again as worn; returns its access flap, hinged at the back.</summary>
    public static Node3D LaidBackpack(Node3D parent) {
        var pack = Pivot(parent, new Vector3(0, .145f, 0), new Vector3(90, 0, 0));
        pack.Scale = Vector3.One * 1.5f;
        ActorShape.Backpack(pack);
        Add(parent, BackpackShape.Details, BackpackShape.Canvas, Vector3.Zero);
        var flap = Pivot(parent, new Vector3(0, .235f, -.15f));
        Add(flap, BackpackShape.Flap, BackpackShape.Canvas, Vector3.Zero);
        return flap;
    }

    /// <summary>A hollow slatted crate with a separately hinged lid and shared wood geometry.</summary>
    private static BoxProp Crate() {
        var prop = new BoxProp();
        Add(prop.Body, CrateShape.Body, CrateShape.Timber, Vector3.Zero);
        var lid = Pivot(prop.Body, new Vector3(0, .42f, -.23f));
        Add(lid, CrateShape.Lid, CrateShape.Timber, Vector3.Zero);
        prop.Lid = lid;
        return prop;
    }

    /// <summary>A plank-topped workbench with a lower shelf and a vise; slotted tools lie on the top.</summary>
    private static BenchProp Workbench() {
        var prop = new BenchProp();
        var body = prop.Body;
        var steel = Metal("4a4d50", .4f, .6f);
        Add(body, BenchShape.Wood, BenchShape.Timber, Vector3.Zero);
        Add(body, BenchShape.Vise, BenchShape.Steel, Vector3.Zero);
        Cylinder(body, .014f, .014f, .19f, new Vector3(.35f, .86f, .37f), steel, new Vector3(90, 0, 0), 10);
        Cylinder(body, .012f, .012f, .15f, new Vector3(.35f, .86f, .46f), steel, sides: 10);
        // Each slot holds both grades of its tool turned onto its side, the lower grade first; the view shows the one slotted.
        Node3D Slot(Vector3 at, float yaw, params (string Tool, float Lift)[] grades) {
            var slot = Pivot(body, at, new Vector3(0, yaw, 0));
            foreach (var (tool, lift) in grades) {
                var model = ActorRig.Tool(tool);
                model.Position = new Vector3(0, lift, 0); model.RotationDegrees = new Vector3(0, 0, 90);
                slot.AddChild(model);
            }
            return slot;
        }
        prop.Chisel = Slot(new Vector3(-.3f, .815f, .05f), 30, ("stone_chisel", .02f), ("steel_chisel", .02f));
        prop.Hammer = Slot(new Vector3(.05f, .815f, -.1f), -20, ("wood_hammer", .052f), ("steel_hammer", .034f));
        return prop;
    }

    /// <summary>
    /// A long painted packing table with the crate being packed open on its top beside a heap of the good being packed,
    /// and up to three empty crates stacked beside it, lids shut.
    /// </summary>
    private static PackerProp PackingStation() {
        var prop = new PackerProp();
        var body = prop.Body;
        Add(body, BenchShape.Packing, BenchShape.Timber, new Vector3(-.25f, 0, 0));
        prop.Packing = Pivot(body, new Vector3(-.6f, .835f, 0));
        Add(prop.Packing, CrateShape.Body, CrateShape.Timber, Vector3.Zero);
        // The goods waiting to be packed heap on the table top at half the size a storage pile shows them.
        prop.Stock = new StockPile(body, new Vector3(.09f, .9f, 0), new Vector2(.46f, .32f), 430, .5f);
        prop.Crates = new Node3D[3];
        for (int i = 0; i < prop.Crates.Length; i++) {
            var crate = prop.Crates[i] = Pivot(body, new Vector3(.85f, i * .44f, .05f));
            Add(crate, CrateShape.Body, CrateShape.Timber, Vector3.Zero);
            Add(Pivot(crate, new Vector3(0, .42f, -.23f)), CrateShape.Lid, CrateShape.Timber, Vector3.Zero);
        }
        return prop;
    }

    /// <summary>A stone firebox under a steel grill with a stew pot, beside a plank counter where the slotted mortar and pestle sits.</summary>
    private static StoveProp CookingTable() {
        var prop = new StoveProp();
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
        Scorch(body, 1.3f);
        return prop;
    }

    /// <summary>A steel stove with a lidded canning pot, beside a plank table with tin cans and a lever press.</summary>
    private static StoveProp Cannery() {
        var prop = new StoveProp();
        var body = prop.Body;
        Add(body, CanneryShape.Stove, CanneryShape.Iron, Vector3.Zero);
        Add(body, CanneryShape.Timber, CanneryShape.Wood, Vector3.Zero);
        Add(body, CanneryShape.Pot, CanneryShape.Steel, Vector3.Zero);
        Add(body, CanneryShape.Press, CanneryShape.Steel, Vector3.Zero);
        prop.Glow = Add(body, CanneryShape.Fire, CanneryShape.Flame, Vector3.Zero);
        prop.Vent = new Vector3(-.44f, 1.1f, -.18f);
        prop.Light = Light(body, new Vector3(-.27f, .25f, .55f), 2f);
        Scorch(body, 1.3f);
        return prop;
    }

    /// <summary>A plank pegboard on two posts set in a cinder-block footing, with four pegs for whatever tools the station holds.</summary>
    private static RackProp ToolRack() {
        var prop = new RackProp();
        // Turned to face down the valley, toward the camera, so the hanging tools show.
        var body = Pivot(prop.Body, Vector3.Zero, new Vector3(0, 45, 0));
        Add(body, RackShape.Feet, RackShape.Concrete, Vector3.Zero);
        Add(body, RackShape.Frame, RackShape.Timber, Vector3.Zero);
        for (int slot = 0; slot < World.ToolSlots; slot++) {
            var peg = prop.Pegs[slot] = new();
            float x = RackShape.PegX(slot);
            foreach (var (kind, mesh) in RackShape.Tools) {
                var hook = Pivot(body, new Vector3(x, 1.3f, .06f));
                Add(hook, mesh, RackShape.Tool, Vector3.Zero);
                hook.Visible = false;
                peg[kind] = hook;
            }
        }
        return prop;
    }

    /// <summary>A 3×3 yard of packed earth edged with logs, with a heap of the good the pile holds, which the view fills from its inventory.</summary>
    private static PileProp Pile() {
        var prop = new PileProp();
        var body = prop.Body;
        Yard(body);
        // The pad lies inside the log frame; with it the pile's ground and frame take 108 of a building's 2,500 triangles.
        prop.Stock = new StockPile(body, new Vector3(0, .02f, 0), new Vector2(2.4f, 2.4f), 2390);
        return prop;
    }

    /// <summary>
    /// Refresh only visible inventory memory; the view guards this call with the existing sight check.
    /// A pile heaps up the good it holds, more of it the fuller the pile is; a compost pile shows one mound of soil that swells with all it holds.
    /// </summary>
    public static void SyncPile(PileProp prop, Thing t, GameData data) {
        string item = t.Compost ? ItemShape.Compost : ""; int count = 0, room = 0;
        foreach (var (id, held) in t.Inventory.Items) {
            if (t.Compost) { count += held; room = t.Inventory.Slots * t.Inventory.StackSize; }
            // A pile holds one good; should it ever hold more, the most plentiful shows.
            else if (held > count) { item = id; count = held; room = t.Inventory.Slots * t.Inventory.StackOf(id); }
        }
        prop.Stock.Fill(item, room > 0 ? count / (float)room : 0, data);
    }

    /// <summary>The packed-earth yard with its log frame that a pile or a farm plot stands on.</summary>
    private static void Yard(Node3D body) {
        Add(body, PileShape.Yard, PileShape.Ground, new Vector3(0, .01f, 0));
        Add(body, PileShape.Frame, PileShape.Timber, new Vector3(0, .01f, 0));
    }

    /// <summary>A farm plot: an empty pile's yard that, once sown, shows dark tilled rows and then the wheat growing on them.</summary>
    private static FarmProp Farm(int id) {
        var prop = new FarmProp();
        var body = prop.Body;
        Yard(body);
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
        return prop;
    }

    /// <summary>A loose scatter of fallen oak leaves in autumn colours, flat on the ground.</summary>
    private static void LeafLitter(Node3D body, int id) {
        Add(body, LeafShape.For(id), NatureShape.Natural, Vector3.Zero);
    }

    /// <summary>Anvil bolted to a welded square-tube stand.</summary>
    private static AnvilProp Anvil() {
        var prop = new AnvilProp();
        var body = prop.Body;
        var tube = Metal("5d6a4f", .6f, .4f);
        foreach (var (x, z) in new[] { (-.2f, -.15f), (.2f, -.15f), (-.2f, .15f), (.2f, .15f) }) Box(body, new Vector3(.06f, .46f, .06f), new Vector3(x * 1.1f, .23f, z * 1.1f), tube);
        Box(body, new Vector3(.52f, .05f, .4f), new Vector3(0, .47f, 0), tube);
        Box(body, new Vector3(.5f, .04f, .05f), new Vector3(0, .12f, .17f), tube);
        Add(body, AnvilShape.Casting, AnvilShape.Iron, Vector3.Zero);
        prop.Vent = new Vector3(0, .86f, 0);
        return prop;
    }

    /// <summary>A village market post with a mailbox, framed chalkboard and shipping storage.</summary>
    private static SignpostProp Signpost() {
        var prop = new SignpostProp();
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
        return prop;
    }

    /// <summary>Campfire enclosed by hand-packed fieldstones, with split firewood and flickering flames.</summary>
    private static CampfireProp FirePit() {
        var prop = new CampfireProp();
        var body = prop.Body;
        Add(body, FirePitShape.Stones, FirePitShape.Stone, Vector3.Zero);
        Add(body, FirePitShape.Fuel, FirePitShape.Wood, Vector3.Zero);
        var fire = Pivot(body, new Vector3(0, .26f, 0));
        Add(fire, FirePitShape.Flames, FirePitShape.Flame, Vector3.Zero).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        prop.Fire = fire;
        prop.Vent = new Vector3(0, .8f, 0);
        // The firelight ends on the ground where the fire's safety from wolves ends, with a flat falloff so the edge reads; other lights stay small pools.
        prop.Light = Light(body, new Vector3(0, .8f, 0), new Vector2(World.FireReach, .8f).Length());
        prop.Light.OmniAttenuation = .5f;
        prop.Light.ShadowEnabled = true;
        Scorch(body, 2.6f);
        return prop;
    }

    /// <summary>Needle roll in degrees for a heat fraction: cold points left, full heat points right.</summary>
    public static float Gauge(float heat) => 110 - 220 * Math.Clamp(heat, 0, 1);

    /// <summary>Turns a mine's local +Z along its rails, out of the cliff's end.</summary>
    public static Vector3 MineYaw(Vector2 p) {
        var way = World.CliffAt(p).Face;
        return new Vector3(0, MathF.Atan2(way.X, way.Y), 0);
    }

    /// <summary>Ore color in a mine cart: rusty iron, black coal, and pale grey for anything else.</summary>
    private static string OreColor(ThingKind ore) => ore switch { ThingKind.Iron => "a35a2e", ThingKind.Coal => "26282b", _ => "b9bbb4" };

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
    private static MineProp Mine(ThingKind ore) {
        var prop = new MineProp();
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
        for (int i = 0; i < 5; i++) Cylinder(supplies, .11f, .11f, .9f, new Vector3(1.3f + (i % 3) * .23f + (i > 2 ? .11f : 0), .11f + (i > 2 ? .19f : 0), -.6f), Wood(i % 2 == 0 ? "8a5f3c" : "7a5235"), new Vector3(90, 0, 0), 7);
        for (int i = 0; i < 4; i++) Sphere(supplies, .17f, new Vector3(-1.35f + (i % 2) * .26f, .1f + (i / 2) * .18f, -.2f + (i % 2) * .1f), Stone("a3a7a3", .9f), new Vector3(1.2f, .7f, 1), 6);
        for (int i = 0; i < 3; i++) Box(supplies, new Vector3(.12f, .06f, .5f), new Vector3(-1.3f, .03f + i * .06f, -1f), Metal("c2c9c9", .4f, .6f), new Vector3(0, i * 12, 0));
        prop.Supplies = supplies;
        // Rough timber sets: a weathered portal with a plank hood and a warning board, then sets down the tunnel fading into the dark.
        var frame = Pivot(body, new Vector3(0, 0, Door));
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
        Add(load, MineShape.Load, Stone(OreColor(ore), .8f), Vector3.Zero);
        prop.Load = load;
        prop.Cart = cart;
        // Lamplight deep inside while someone works underground: a faint warm haze at the dark end.
        prop.Glow = Box(body, new Vector3(1.4f, 1.7f, .02f), new Vector3(0, .86f, Door - 2.5f), Flat(new Color(1, .55f, .2f, .25f)));
        return prop;
    }

    private static OmniLight3D Light(Node3D parent, Vector3 at, float range) {
        var light = new OmniLight3D { Position = at, OmniRange = range, LightColor = new Color("ff9a45"), LightEnergy = 0, ShadowEnabled = false, OmniAttenuation = 1.4f };
        parent.AddChild(light);
        return light;
    }

    private static Texture2D? scorchTexture;
    /// <summary>A soot stain on the ground under a fire, projected as a decal so it follows whatever lies beneath.</summary>
    public static void Scorch(Node3D parent, float size) {
        scorchTexture ??= ScorchTexture();
        parent.AddChild(new Decal { Size = new Vector3(size, .5f, size), TextureAlbedo = scorchTexture, Modulate = new Color(1, 1, 1, .8f), Layers = MarkLayer });
    }

    /// <summary>A ragged dark blot that fades out toward its edge.</summary>
    private static ImageTexture ScorchTexture() {
        const int size = 128;
        var noise = new FastNoiseLite { Seed = 5, Frequency = .045f, FractalOctaves = 3 };
        var image = Image.CreateEmpty(size, size, true, Image.Format.Rgba8);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float distance = new Vector2(x - size / 2f, y - size / 2f).Length() / (size / 2f);
                float grain = noise.GetNoise2D(x, y);
                float alpha = Mathf.Clamp((1 - distance + grain * .35f) * 1.8f - .2f, 0, 1);
                image.SetPixel(x, y, new Color(.07f, .06f, .05f, alpha * (.75f + grain * .25f)));
            }
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
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
        /// <summary>The meshes and their own materials, whose faded copies are made with the cart, for fading it as it leaves view.</summary>
        public readonly (MeshInstance3D Mesh, Material Material)[] Parts;

        public CartModel() {
            Root.AddChild(Courier.Root);
            Courier.Root.Position = new Vector3(0, 0, .55f);
            var cart = Pivot(Root, new Vector3(0, 0, -.45f));
            Add(cart, CartShape.Frame, CartShape.Steel, Vector3.Zero);
            for (int i = 0; i < 2; i++) {
                wheels[i] = Pivot(cart, new Vector3(i == 0 ? .42f : -.42f, .26f, 0));
                Add(wheels[i], CartShape.Wheel, CartShape.WheelSurface, Vector3.Zero);
            }
            Goods = Pivot(cart, new Vector3(0, .415f, 0));
            foreach (var (position, scale) in new[] {
                (new Vector3(-.08f,0,.18f),new Vector3(.63f,.6f,.7f)),
                (new Vector3(.1f,0,-.19f),new Vector3(.55f,.52f,.65f)),
                (new Vector3(.05f,.27f,.05f),new Vector3(.45f,.46f,.58f)) })
                Add(Goods, CrateShape.Cargo, CrateShape.Timber, position, scale: scale);
            for (int i = 0; i < Crates.Length; i++) {
                var at = i < 4 ? new Vector3(i % 2 == 0 ? -.15f : .15f, 0, i < 2 ? .17f : -.17f) : new Vector3(i == 4 ? -.15f : .15f, .225f, i == 4 ? .08f : -.12f);
                Crates[i] = Pivot(cart, new Vector3(0, .415f, 0) + at);
                Add(Crates[i], CrateShape.Cargo, CrateShape.Timber, Vector3.Zero, scale: new Vector3(.4f, .5f, .727f));
            }
            Cash = Pivot(cart, new Vector3(0, .4f, 0));
            Add(Cash, CartShape.Payment, CartShape.Steel, Vector3.Zero);
            Parts = FadeParts(Root);
            BuildShaders(Root);
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
