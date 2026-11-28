using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>
/// The places the intro film plays in before the valley: an office floor, a rented room, an alley and a train car.
/// They are built from the same primitives and surfaces as the game's models, in metres.
/// Each is laid out around the hero at its origin, facing +Z.
/// </summary>
internal static class IntroSets {
    /// <summary>Height of a desk's top and of a seat that puts a sitter's feet flat on the floor.</summary>
    public const float DeskTop = .68f, SeatTop = .36f;

    /// <summary>A material that glows on its own, made fresh so a film can dim or kill it without touching others.</summary>
    private static StandardMaterial3D Glowing(string hex, float energy) =>
        new() { AlbedoColor = new Color(hex), EmissionEnabled = true, Emission = new Color(hex), EmissionEnergyMultiplier = energy, Roughness = .3f };

    private static void Unshadowed(Node3D node) {
        if (node is GeometryInstance3D geometry) geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        for (int i = 0; i < node.GetChildCount(); i++) if (node.GetChild(i) is Node3D child) Unshadowed(child);
    }

    // ---------- Office ----------

    public sealed class Office {
        public readonly Node3D Root = new();
        /// <summary>
        /// The hero's own chair, monitor, keyboard, mug and mouse, and the bat leaning in the corner of his cubicle.
        /// </summary>
        public Node3D Chair = null!, Monitor = null!, Keyboard = null!, Mug = null!, Mouse = null!, LeaningBat = null!;
        /// <summary>The mouse's left button, which flattens into the mouse as it is pressed.</summary>
        public Node3D MouseButton = null!;
        /// <summary>The hero's screen, which dies under the bat; everyone else's keeps glowing.</summary>
        public readonly StandardMaterial3D Screen = Glowing("a9d8ee", 1.3f);
        /// <summary>The web page of dancing catgirls he has up, and the picture of it over his screen.</summary>
        public readonly CatgirlPage Page = new();
        public Node3D PageOnScreen = null!, ScreenIndicators = null!;
        public OmniLight3D Glow = null!;
        /// <summary>The colleagues typing at the other desks.</summary>
        public readonly List<ActorRig> Staff = new();
    }

    /// <summary>
    /// An open-plan floor of grey cubicles under strip lights.
    /// The hero's cubicle is open to the aisle on his left, where the boss walks up.
    /// </summary>
    public static Office BuildOffice() {
        var office = new Office();
        var root = office.Root;
        Box(root, new Vector3(26, .1f, 26), new Vector3(0, -.05f, -3), Cloth("59606b"));
        var wall = Mat("c9c7bd", .9f);
        Box(root, new Vector3(26, 3, .1f), new Vector3(0, 1.5f, -9), wall);
        Box(root, new Vector3(.1f, 3, 26), new Vector3(-7, 1.5f, -3), wall);
        Box(root, new Vector3(26, 3, .1f), new Vector3(0, 1.5f, 5), wall);
        // Shut blinds glow along the far wall; the day outside never reaches the desks.
        var blinds = Glowing("dfe6e2", .9f);
        for (int i = -4; i <= 4; i++) {
            Box(root, new Vector3(1.9f, 1.3f, .04f), new Vector3(i * 2.4f, 1.75f, -8.93f), blinds);
            for (int slat = 0; slat < 9; slat++) Box(root, new Vector3(1.9f, .02f, .05f), new Vector3(i * 2.4f, 1.15f + slat * .15f, -8.92f), Mat("a9aca4", .8f));
        }
        var ceiling = Box(root, new Vector3(26, .1f, 26), new Vector3(0, 2.8f, -3), Mat("d8d6cc", .95f));
        var strips = Glowing("f4f8ff", 2.2f);
        for (int row = 0; row < 5; row++)
            for (int column = -2; column <= 3; column++) {
                var at = new Vector3(column * 2.5f - .6f, 2.74f, 2.5f - row * 2.4f);
                Box(ceiling, new Vector3(1.36f, .035f, .34f), at - ceiling.Position, Metal("8d9293", .64f, .23f));
                foreach (float offset in new[] { -.072f, .072f })
                    Box(ceiling, new Vector3(1.26f, .028f, .081f), at - ceiling.Position + new Vector3(0, -.022f, offset), strips);
            }
        Unshadowed(ceiling);

        var shared = Glowing("a9d8ee", 1.3f);
        root.AddChild(office.Page.Root);
        (office.Chair, office.Monitor, office.Keyboard, office.Mug, office.Mouse, office.MouseButton) = Cubicle(root, Vector3.Zero, office.Screen, 0);
        office.ScreenIndicators = office.Monitor.GetNode<Node3D>("ScreenIndicators");
        // The page covers his screen's own rows of a spreadsheet.
        office.PageOnScreen = Add(office.Monitor, new QuadMesh { Size = ScreenSize }, office.Page.Picture, ScreenMiddle + new Vector3(0, 0, -.012f), new Vector3(0, 180, 0));
        office.Glow = new OmniLight3D { Position = new Vector3(.08f, 1, .36f), LightColor = new Color("8fc4ff"), LightEnergy = .12f, OmniRange = 1.7f, OmniAttenuation = 1.2f };
        root.AddChild(office.Glow);
        office.LeaningBat = Pivot(root, new Vector3(-.84f, .78f, -.42f), new Vector3(4, 0, 8));
        office.LeaningBat.AddChild(Bat());
        // The colleagues' cubicles: one beside him, a row behind and a block across the aisle.
        string[] shirts = { "8b8f96", "a59c8c", "7f8a99", "9a9186", "868d8a" };
        var cells = new[] { new Vector3(-1.9f, 0, 0), new Vector3(0, 0, -2.4f), new Vector3(-1.9f, 0, -2.4f), new Vector3(4.4f, 0, 0), new Vector3(4.4f, 0, -2.4f), new Vector3(6.3f, 0, -2.4f), new Vector3(0, 0, -4.8f), new Vector3(4.4f, 0, -4.8f) };
        for (int i = 0; i < cells.Length; i++) {
            Cubicle(root, cells[i], shared, i);
            if (i is 2 or 5) continue;
            var clerk = new ActorRig(ActorSkin.Player, shirts[i % shirts.Length]);
            clerk.Pack!.Visible = false;
            ActorRig.Tie(clerk.Torso, "39404a");
            clerk.Root.Position = cells[i];
            root.AddChild(clerk.Root); office.Staff.Add(clerk);
        }
        // A water cooler and filing cabinets along the aisle.
        var cooler = Pivot(root, new Vector3(.24f, 0, -6.8f));
        Box(cooler, new Vector3(.34f, .95f, .34f), new Vector3(0, .475f, 0), Mat("e4e2d8", .5f));
        Cylinder(cooler, .14f, .14f, .4f, new Vector3(0, 1.15f, 0), Mat("7fb6dc", .15f), sides: 12);
        for (int i = 0; i < 4; i++) {
            var cabinet = Pivot(root, new Vector3(-6.6f, 0, -1 - i * .62f));
            Box(cabinet, new Vector3(.6f, 1.3f, .6f), new Vector3(0, .65f, 0), Metal("8d9590", .5f, .4f));
            for (int drawer = 0; drawer < 3; drawer++) Box(cabinet, new Vector3(.02f, .03f, .2f), new Vector3(.31f, .3f + drawer * .4f, 0), Metal("c9d3d8", .35f, .8f));
        }
        return office;
    }

    /// <summary>A desk with its computer and chair inside fabric partitions; <paramref name="seed"/> varies the clutter.</summary>
    private static (Node3D Chair, Node3D Monitor, Node3D Keyboard, Node3D Mug, Node3D Mouse, Node3D MouseButton) Cubicle(Node3D parent, Vector3 at, StandardMaterial3D screen, int seed) {
        var cell = Pivot(parent, at);
        var laminate = Mat("cdc4b0", .6f);
        Box(cell, new Vector3(1.7f, .04f, .8f), new Vector3(0, DeskTop - .02f, .62f), laminate);
        foreach (float side in new[] { -1f, 1f }) Box(cell, new Vector3(.04f, DeskTop - .04f, .76f), new Vector3(side * .8f, (DeskTop - .04f) / 2, .62f), laminate);
        Partition(cell, new Vector3(1.9f, 1.2f, .05f), new Vector3(0, .6f, 1.06f));
        Partition(cell, new Vector3(.05f, 1.2f, 1.9f), new Vector3(-.95f, .6f, .13f));
        // Sticky notes and a calendar pinned to the partition.
        string[] notes = { "f1df6b", "f39bb0", "9fd6f0", "f1df6b" };
        for (int i = 0; i < 3; i++) Box(cell, new Vector3(.09f, .09f, .01f), new Vector3(-.62f + i * .13f + seed % 3 * .05f, .98f - i % 2 * .11f, 1.03f), Mat(notes[(i + seed) % notes.Length], .9f), new Vector3(0, 0, (i - 1) * 6));
        Box(cell, new Vector3(.26f, .32f, .01f), new Vector3(.55f, .95f, 1.03f), Mat("f1eee4", .9f));
        Box(cell, new Vector3(.26f, .07f, .012f), new Vector3(.55f, 1.075f, 1.03f), Mat("b8443a", .8f));

        var monitor = Monitor(cell, new Vector3(.08f, DeskTop, .66f), screen);
        var keyboard = Pivot(cell, new Vector3(.02f, DeskTop, .4f));
        Box(keyboard, new Vector3(.44f, .02f, .16f), new Vector3(0, .01f, 0), Mat("cfc8b4", .6f));
        Box(keyboard, new Vector3(.4f, .008f, .12f), new Vector3(0, .024f, 0), Mat("aea793", .7f));
        if (seed == 0) {
            var keyCap = Mat("e4d9c3", .75f);
            for (int row = 0; row < 5; row++)
                for (int key = 0; key < 16; key++)
                    Box(keyboard, new Vector3(.019f, .009f, .015f), new Vector3(-.177f + key * .023f, .031f, -.045f + row * .02f), keyCap);
        }
        Box(cell, new Vector3(.22f, .004f, .2f), MouseSpot - new Vector3(0, .002f, 0), Mat("3d4a63", .9f));
        var (mouse, mouseButton) = Mouse(cell, MouseSpot);
        // The tower, with a floppy slot and a power light.
        var tower = Pivot(cell, new Vector3(-.62f, DeskTop, .76f));
        Box(tower, new Vector3(.19f, .4f, .42f), new Vector3(0, .2f, 0), Mat("d6cfbd", .55f));
        Box(tower, new Vector3(.11f, .012f, .01f), new Vector3(0, .31f, -.212f), Mat("4a4a46", .6f));
        Box(tower, new Vector3(.02f, .02f, .01f), new Vector3(.05f, .08f, -.212f), Mat("57d66b", .4f, 0, 1.5f));
        var mug = Pivot(cell, new Vector3(.36f, DeskTop, .5f));
        Cylinder(mug, .04f, .036f, .09f, new Vector3(0, .045f, 0), Mat(seed % 2 == 0 ? "e9e6dc" : "b5513f", .4f), sides: 10);
        Cylinder(mug, .033f, .033f, .004f, new Vector3(0, .088f, 0), Mat("3a2414", .3f), sides: 10);
        // Paperwork: a stack and a loose sheet.
        Box(cell, new Vector3(.21f, .04f + seed % 3 * .02f, .3f), new Vector3(.6f, DeskTop + .03f, .74f), Mat("f1eee4", .9f), new Vector3(0, 4, 0));
        Box(cell, new Vector3(.21f, .004f, .3f), new Vector3(.66f, DeskTop + .002f, .38f), Mat("f6f3ea", .9f), new Vector3(0, -14, 0));
        return (Chair(cell, new Vector3(0, 0, -.03f)), monitor, keyboard, mug, mouse, mouseButton);
    }

    private static void Partition(Node3D parent, Vector3 size, Vector3 at) {
        Box(parent, size, at, Cloth("8792a0"));
        Box(parent, new Vector3(size.X + .01f, .04f, size.Z + .01f), at + new Vector3(0, size.Y / 2, 0), Mat("5d6470", .5f));
    }

    /// <summary>Where a mouse rests on its pad, to the right of a cubicle's keyboard.</summary>
    public static readonly Vector3 MouseSpot = new(-.36f, DeskTop + .004f, .42f);
    /// <summary>
    /// How far each of a mouse's two buttons is from its middle, and how high its buttons sit on it, in metres.
    /// </summary>
    public const float ButtonSide = .0155f, ButtonTop = .03f;

    /// <summary>
    /// A beige two-button mouse lying at <paramref name="at"/>, its buttons toward +Z, where its cable runs off.
    /// Returned with its left button, which stands on the mouse's top so that squashing it presses it.
    /// </summary>
    private static (Node3D Mouse, Node3D LeftButton) Mouse(Node3D parent, Vector3 at) {
        var mouse = Pivot(parent, at);
        var beige = Mat("d6cfbd", .5f);
        Box(mouse, new Vector3(.06f, ButtonTop, .1f), new Vector3(0, ButtonTop / 2, 0), beige);
        Sphere(mouse, .03f, new Vector3(0, .024f, -.018f), beige, new Vector3(1, .55f, 1.3f), 10);
        Box(mouse, new Vector3(.006f, .006f, .14f), new Vector3(0, .008f, .12f), Mat("b8b1a0", .6f));
        var buttons = Mat("c4bdab", .5f);
        Node3D Button(float side) {
            var button = Pivot(mouse, new Vector3(side * ButtonSide, ButtonTop, .028f));
            Box(button, new Vector3(.027f, .006f, .04f), new Vector3(0, .003f, 0), buttons);
            return button;
        }
        Button(-1);
        // The sitter faces +Z, so his left is +X.
        return (mouse, Button(1));
    }

    /// <summary>The middle of a monitor's screen from its origin, and the screen's size.</summary>
    public static readonly Vector3 ScreenMiddle = new(0, .28f, -.132f);
    public static readonly Vector2 ScreenSize = new(.34f, .26f);
    internal static readonly Vector3 MonitorFrontEdge = new(0, .45f, -.13f);

    /// <summary>A beige tube monitor standing at <paramref name="at"/>, its screen toward -Z and its origin under the stand.</summary>
    private static Node3D Monitor(Node3D parent, Vector3 at, StandardMaterial3D screen) {
        var monitor = Pivot(parent, at);
        var beige = Mat("d6cfbd", .55f);
        Box(monitor, new Vector3(.24f, .03f, .22f), new Vector3(0, .015f, 0), beige);
        Box(monitor, new Vector3(.1f, .06f, .1f), new Vector3(0, .06f, 0), beige);
        Box(monitor, new Vector3(.42f, .36f, .3f), new Vector3(0, .27f, .02f), beige);
        Box(monitor, new Vector3(.3f, .28f, .2f), new Vector3(0, .27f, .25f), Mat("c4bdab", .6f));
        Box(monitor, new Vector3(ScreenSize.X, ScreenSize.Y, .012f), ScreenMiddle, screen);
        var lip = Mat("b4ad99", .68f);
        foreach (float side in new[] { -1f, 1f }) {
            Box(monitor, new Vector3(.021f, .29f, .024f), new Vector3(side * .179f, .28f, -.142f), lip);
            Box(monitor, new Vector3(.371f, .017f, .022f), new Vector3(0, .28f + side * .138f, -.142f), lip);
            for (int vent = 0; vent < 8; vent++)
                Box(monitor, new Vector3(.006f, .052f, .008f), new Vector3(side * .211f, .361f, .015f + vent * .017f), lip);
        }
        // Rows of a spreadsheet and the monitor's power light go out together.
        var indicators = Pivot(monitor, Vector3.Zero);
        indicators.Name = "ScreenIndicators";
        for (int row = 0; row < 6; row++) Box(indicators, new Vector3(row == 0 ? .3f : .26f - row % 3 * .04f, .012f, .004f), new Vector3(row == 0 ? 0 : -.02f - row % 3 * .02f, .375f - row * .036f, -.139f), Mat(row == 0 ? "2f6f8c" : "5c93ab", .4f, 0, .5f));
        Box(indicators, new Vector3(.015f, .015f, .01f), new Vector3(.17f, .115f, -.132f), Mat("57d66b", .4f, 0, 1.5f));
        return monitor;
    }

    /// <summary>A swivel chair with its seat's middle over <paramref name="at"/>, facing +Z.</summary>
    private static Node3D Chair(Node3D parent, Vector3 at) {
        var chair = Pivot(parent, at);
        var fabric = Cloth("3b4252"); var steel = Metal("3a3d42", .5f, .6f);
        Box(chair, new Vector3(.46f, .06f, .44f), new Vector3(0, SeatTop - .03f, 0), fabric);
        Box(chair, new Vector3(.42f, .46f, .05f), new Vector3(0, SeatTop + .36f, -.24f), fabric, new Vector3(-6, 0, 0));
        Box(chair, new Vector3(.06f, .3f, .03f), new Vector3(0, SeatTop + .1f, -.26f), steel);
        Cylinder(chair, .028f, .028f, SeatTop - .1f, new Vector3(0, (SeatTop - .1f) / 2 + .05f, 0), steel);
        for (int leg = 0; leg < 3; leg++) Box(chair, new Vector3(.58f, .035f, .05f), new Vector3(0, .05f, 0), steel, new Vector3(0, leg * 60, 0));
        return chair;
    }

    internal const float BatBarrelStart = .26f, BatBarrelEnd = .72f;
    internal const float BatBarrelNarrowRadius = .021f, BatBarrelWideRadius = .037f;

    /// <summary>A wooden baseball bat gripped at the origin, its barrel along -Y like the game's tools.</summary>
    public static Node3D Bat() {
        var bat = new Node3D();
        var ash = Wood("c9a66c", .6f);
        Sphere(bat, .028f, new Vector3(0, .1f, 0), ash, new Vector3(1, .6f, 1), 8);
        Cylinder(bat, .021f, .017f, .36f, new Vector3(0, -.08f, 0), ash, sides: 8);
        Cylinder(bat, BatBarrelWideRadius, BatBarrelNarrowRadius, BatBarrelEnd - BatBarrelStart, Vector3.Down * ((BatBarrelStart + BatBarrelEnd) / 2), ash, sides: 8);
        Sphere(bat, BatBarrelWideRadius, Vector3.Down * BatBarrelEnd, ash, new Vector3(1, .5f, 1), 8);
        Cylinder(bat, .022f, .022f, .29f, new Vector3(0, -.075f, 0), Cloth("2d2f33"), sides: 8);
        return bat;
    }

    // ---------- Rented room ----------

    public sealed class Room {
        public readonly Node3D Root = new();
        /// <summary>
        /// The pack lying open on the rug, its flap, and the things going into it in the order he takes them, right hand first.
        /// Each has the place it lies and where a hand closes on it, measured from that place.
        /// </summary>
        public Node3D Pack = null!, PackBody = null!, Flap = null!, Zipper = null!, Torch = null!;
        public Transform3D PackOnRug;
        public Basis TorchOnRug;
        public readonly List<(Node3D Item, Vector3 Lies, Vector3 Grip)> Items = new();
        public OmniLight3D Lamp = null!;
    }

    /// <summary>Where the pack lies in front of the kneeling hero, and where things drop into it.</summary>
    public static readonly Vector3 PackSpot = new(0, 0, .62f), PackMouth = new(0, .22f, .58f);

    /// <summary>A small rented room at night: a bed, a wardrobe, a lamp and a window onto the city, with the pack on a rug.</summary>
    public static Room BuildRoom() {
        var room = new Room();
        var root = room.Root;
        Box(root, new Vector3(5, .1f, 5), new Vector3(0, -.05f, 0), Wood("94704c", .7f));
        var plaster = Mat("d9cdb6", .95f);
        // The window faces the street beside the bed, across from the room's door.
        const float wallX = -2.5f, left = -.95f, right = .57f, sill = .82f, head = 2.1f;
        float middle = (left + right) / 2, windowY = (sill + head) / 2;
        Box(root, new Vector3(.12f, 2.7f, 2.5f - right), new Vector3(wallX, 1.35f, (2.5f + right) / 2), plaster);
        Box(root, new Vector3(.12f, 2.7f, left + 2.5f), new Vector3(wallX, 1.35f, (left - 2.5f) / 2), plaster);
        Box(root, new Vector3(.12f, sill, right - left), new Vector3(wallX, sill / 2, middle), plaster);
        Box(root, new Vector3(.12f, 2.7f - head, right - left), new Vector3(wallX, (2.7f + head) / 2, middle), plaster);
        Box(root, new Vector3(5, 2.7f, .12f), new Vector3(0, 1.35f, 2.4f), plaster);
        var frame = Wood("b3a99a", .63f);
        Box(root, new Vector3(.27f, .05f, right - left + .12f), new Vector3(wallX + .08f, sill, middle), frame);
        Box(root, new Vector3(.13f, .04f, right - left), new Vector3(wallX + .06f, windowY, middle), frame);
        foreach (float at in new[] { left, middle, right })
            Box(root, new Vector3(.09f, head - sill, .048f), new Vector3(wallX + .045f, windowY, at), frame);
        for (int pleat = 0; pleat < 7; pleat++) {
            float z = right + .03f + .045f * pleat;
            Box(root, new Vector3(.1f - .04f * (pleat % 2), head - sill + .26f, .062f),
                new Vector3(wallX + .17f, windowY, z), Cloth("544849"));
        }
        Box(root, new Vector3(.12f, 2.7f, 5), new Vector3(2.5f, 1.35f, 0), plaster);
        Box(root, new Vector3(5, 2.7f, .12f), new Vector3(0, 1.35f, -2.4f), plaster);
        Box(root, new Vector3(5.2f, .1f, 5.2f), new Vector3(0, 2.75f, 0), Mat("e3dccb", .95f));
        // The door out, behind him to the right.
        Box(root, new Vector3(.9f, 2.05f, .06f), new Vector3(1.5f, 1.025f, -2.32f), Wood("7a5a3c", .6f));
        var doorTrim = Wood("483326", .68f);
        foreach (float side in new[] { -1f, 1f })
            Box(root, new Vector3(.05f, 2.12f, .07f), new Vector3(1.5f + side * .476f, 1.06f, -2.29f), doorTrim);
        Box(root, new Vector3(1, .05f, .07f), new Vector3(1.5f, 2.08f, -2.29f), doorTrim);
        Sphere(root, .035f, new Vector3(1.15f, 1, -2.27f), Metal("c9b06a", .3f, .8f));
        // Bed along the left wall.
        var bed = Pivot(root, new Vector3(-1.85f, 0, -.5f));
        Box(bed, new Vector3(1.05f, .22f, 2.05f), new Vector3(0, .2f, 0), Wood("6e4a2e", .7f));
        Box(bed, new Vector3(1, .16f, 2), new Vector3(0, .37f, 0), Cloth("e6dfcf"));
        Box(bed, new Vector3(1.02f, .1f, 1.3f), new Vector3(0, .43f, .33f), Cloth("4f6a82"));
        var folds = Cloth("425b75");
        for (int fold = 0; fold < 5; fold++) {
            var crease = Sphere(bed, .3f, new Vector3(.22f - .085f * fold, .487f, .30f), folds, new Vector3(.29f, .055f, 1.6f), 10);
            crease.RotationDegrees = new Vector3(0, fold * 11 - 24, 0);
        }
        Box(bed, new Vector3(.6f, .1f, .36f), new Vector3(0, .5f, -.76f), Cloth("f1ede2"), new Vector3(6, 0, 0));
        Box(bed, new Vector3(1.05f, .7f, .06f), new Vector3(0, .5f, -1.03f), Wood("6e4a2e", .7f));
        // Nightstand and lamp at the head of the bed.
        var stand = Pivot(root, new Vector3(-1, 0, -2));
        Box(stand, new Vector3(.45f, .5f, .4f), new Vector3(0, .25f, 0), Wood("7a5a3c", .7f));
        Cylinder(stand, .07f, .09f, .03f, new Vector3(0, .515f, 0), Metal("8d6f3a", .4f, .7f));
        Cylinder(stand, .015f, .015f, .25f, new Vector3(0, .65f, 0), Metal("8d6f3a", .4f, .7f));
        Cylinder(stand, .15f, .09f, .2f, new Vector3(0, .85f, 0), Glowing("ffd9a0", 1.6f), sides: 12);
        room.Lamp = new OmniLight3D { Position = new Vector3(-1, .95f, -1.9f), LightColor = new Color("ffc27a"), LightEnergy = 1.1f, OmniRange = 4.5f, OmniAttenuation = 1.1f, ShadowEnabled = true };
        root.AddChild(room.Lamp);
        // Cool street spill is strongest by the window sill.
        root.AddChild(new OmniLight3D {
            Position = new Vector3(-1.85f, 1.18f, -.18f),
            LightColor = new Color("83a9ff"),
            LightEnergy = 1.1f,
            OmniRange = 3.48f,
            OmniAttenuation = 1.23f,
        });
        // A muted return from the warm wall across from the lamp.
        root.AddChild(new OmniLight3D {
            Position = new Vector3(.6f, 1.22f, 1.2f),
            LightColor = new Color("e9b58c"),
            LightEnergy = .27f,
            OmniRange = 4.5f,
        });
        // Wardrobe with one door ajar, and a poster of mountains over the bed.
        var wardrobe = Pivot(root, new Vector3(2.05f, 0, -1.4f));
        Box(wardrobe, new Vector3(.6f, 1.9f, 1.1f), new Vector3(0, .95f, 0), Wood("7a5a3c", .7f));
        Box(wardrobe, new Vector3(.03f, 1.8f, .52f), new Vector3(-.4f, .95f, .42f), Wood("86653f", .7f), new Vector3(0, 38, 0));
        Box(root, new Vector3(.01f, .62f, .9f), new Vector3(-2.43f, 1.55f, -1.55f), Mat("9cc3d9", .8f));
        Box(root, new Vector3(.012f, .3f, .9f), new Vector3(-2.428f, 1.39f, -1.55f), Mat("5f8a58", .8f));
        Box(root, new Vector3(.011f, .3f, .3f), new Vector3(-2.429f, 1.45f, -1.70f), Mat("eef2f2", .8f), new Vector3(45, 0, 0));
        Box(root, new Vector3(.011f, .22f, .22f), new Vector3(-2.429f, 1.45f, -1.35f), Mat("d9e0e0", .8f), new Vector3(45, 0, 0));
        Cylinder(root, 1.05f, 1.05f, .012f, new Vector3(0, .006f, .5f), Cloth("7f4a3a"), sides: 24);
        Cylinder(root, .8f, .8f, .014f, new Vector3(0, .007f, .5f), Cloth("b98a5a"), sides: 24);

        room.Pack = Pivot(root, PackSpot, new Vector3(0, 180, 0));
        room.Pack.Scale = Vector3.One * .8f;
        room.PackOnRug = room.Pack.Transform;
        (room.PackBody, room.Flap) = WorldModels.LaidBackpack(room.Pack);
        var zipMetal = Metal("bdb49d", .31f, .6f);
        room.Zipper = Pivot(root, PackMouth);
        Box(room.Zipper, new Vector3(.018f, .009f, .018f), Vector3.Zero, zipMetal);
        var pull = Torus(room.Zipper, .62f, 1, new Vector3(.009f, .003f, .013f), zipMetal);
        pull.Scale = new Vector3(.007f, .006f, .011f);
        // What he takes: the flask, tins of food, a torch and an axe, laid out on the rug within reach of his squat.
        Node3D Lay(Node3D item, Vector3 at, Vector3 turn, float scale, float gripHeight = 0) {
            var holder = Pivot(root, at, turn);
            holder.Scale = Vector3.One * scale; holder.AddChild(item);
            room.Items.Add((holder, at, new Vector3(0, gripHeight, 0)));
            return holder;
        }
        Lay(BottleShape.Create(Water.Full), new Vector3(-.4f, 0, .44f), Vector3.Zero, .3f, .2f);
        var tins = new Node3D();
        Cylinder(tins, .045f, .045f, .11f, new Vector3(0, .055f, 0), Metal("b9c2c4", .35f, .7f), sides: 10);
        Cylinder(tins, .046f, .046f, .06f, new Vector3(0, .055f, 0), Mat("b5513f", .6f), sides: 10);
        Lay(tins, new Vector3(.36f, 0, .36f), Vector3.Zero, 1, .1f);
        var (torch, flame, _) = ActorRig.Torch();
        flame.Visible = false;
        room.Torch = Lay(torch, new Vector3(-.34f, .035f, .56f), new Vector3(0, -150, 0), .70f);
        room.TorchOnRug = room.Torch.Basis.Orthonormalized();
        // A short-handled axe fits diagonally in the pack.
        Lay(ActorRig.Tool("steel_axe"), new Vector3(.34f, .03f, .54f), new Vector3(90, 0, 70), .67f);
        return room;
    }

    /// <summary>The city across the street, seen through the room's window: dark buildings with lit windows.</summary>
    public static Node3D BuildSkyline() {
        var city = new Node3D { RotationDegrees = new Vector3(0, -90, 0) };
        var rng = new Random(5);
        var lit = Glowing("ffd58a", 1.4f);
        for (int i = 0; i < 16; i++) {
            float width = 5 + (float)rng.NextDouble() * 6, height = 8 + (float)rng.NextDouble() * 22, x = -45 + i * 6 + (float)rng.NextDouble() * 2, z = 26 + (float)rng.NextDouble() * 18;
            Box(city, new Vector3(width, height, 6), new Vector3(x, height / 2 - 6, z), Mat(i % 2 == 0 ? "3a3d4a" : "454552", .9f));
            for (int w = 0; w < 10; w++)
                Box(city, new Vector3(.7f, .9f, .1f), new Vector3(x - width / 2 + .8f + rng.Next((int)width - 1), rng.Next((int)height - 2) - 5, z - 3.02f), lit);
        }
        Unshadowed(city);
        return city;
    }

    // ---------- Alley ----------

    public sealed class Alley {
        public readonly Node3D Root = new();
        public OmniLight3D Red = null!, Blue = null!;
        public const float BranchNear = .2f, BranchFar = 3.65f;
    }

    /// <summary>A wet service alley with a dark side passage before the lit street.</summary>
    public static Alley BuildAlley() {
        var alley = new Alley();
        var root = alley.Root;
        // Water gathers in the pavement's hollows. Both coloured lights glint in the same broken surface.
        var pavement = new ShaderMaterial { Shader = new Shader { Code = """
            shader_type spatial;
            uniform sampler2D height_map : repeat_enable, filter_linear_mipmap;
            uniform sampler2D stone_normal : hint_normal, repeat_enable, filter_linear_mipmap;
            varying vec2 lane;
            void vertex() { lane = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz; }
            void fragment() {
                float low = texture(height_map, lane * vec2(0.23, 0.13)).r;
                float grain = texture(height_map, lane * 1.42).r;
                float pool = 1.0 - smoothstep(0.37, 0.58, low + 0.10 * (grain - 0.5));
                ALBEDO = mix(vec3(0.049, 0.053, 0.060), vec3(0.021, 0.030, 0.044), pool) * (0.76 + 0.45 * grain);
                ROUGHNESS = mix(0.48, 0.12, pool);
                SPECULAR = 0.74;
                NORMAL_MAP = texture(stone_normal, lane * 0.9).rgb;
                NORMAL_MAP_DEPTH = mix(0.55, 0.075, pool);
            }
            """ } };
        pavement.SetShaderParameter("height_map", ScanMap(Scan.Gravel, "height"));
        pavement.SetShaderParameter("stone_normal", ScanMap(Scan.Gravel, "bump"));
        Box(root, new Vector3(4.8f, .16f, 29), new Vector3(0, -.08f, 3), pavement);
        const float branchEnd = -9.5f;
        float branchDepth = Alley.BranchFar - Alley.BranchNear;
        Box(root, new Vector3(-2.4f - branchEnd, .16f, branchDepth),
            new Vector3((branchEnd - 2.4f) / 2, -.08f, (Alley.BranchNear + Alley.BranchFar) / 2), pavement);

        var brick = Stone("746267", .83f);
        var mortar = Mat("47444c", .9f);
        var concrete = Stone("565963", .75f);
        var steel = Metal("353e46", .46f, .42f);
        const int courses = 20, joints = 36;
        var brickJoints = new MultiMesh {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new BoxMesh { Size = new Vector3(.007f, .14f, .009f), Material = mortar },
            InstanceCount = 2 * courses * joints,
        };
        root.AddChild(new MultiMeshInstance3D { Multimesh = brickJoints, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        int instance = 0;
        foreach (float side in new[] { -1f, 1f }) {
            void Wall(float from, float to) {
                float z = (from + to) / 2, length = to - from;
                Box(root, new Vector3(.25f, 6, length), new Vector3(side * 2.28f, 3, z), brick);
                Box(root, new Vector3(.31f, .24f, length), new Vector3(side * 2.22f, .12f, z), concrete);
                Box(root, new Vector3(.27f, .14f, length), new Vector3(side * 2.22f, 2.58f, z), concrete);
                for (int row = 0; row < courses; row++)
                    Box(root, new Vector3(.007f, .009f, length), new Vector3(side * 2.15f, (row + 2) * .14f, z), mortar);
            }
            if (side > 0) Wall(-10, 8);
            else { Wall(-10, Alley.BranchNear); Wall(Alley.BranchFar, 8); }
            for (int row = 0; row < courses; row++) {
                float y = (row + 2) * .14f;
                for (int joint = 0; joint < joints; joint++) {
                    float z = -8 + (joint + (row % 2) * .5f) * .44f;
                    if (side < 0 && z > Alley.BranchNear && z < Alley.BranchFar) continue;
                    brickJoints.SetInstanceTransform(instance++, new Transform3D(Basis.Identity, new Vector3(side * 2.15f, y + .07f, z)));
                }
            }

            for (int bay = 0; bay < 4; bay++) {
                float z = -5.8f + bay * 3.7f + side * .32f;
                if (side < 0 && z > Alley.BranchNear - .6f && z < Alley.BranchFar + .6f) continue;
                Box(root, new Vector3(.09f, 1.95f, .88f), new Vector3(side * 2.105f, .995f, z), steel);
                Box(root, new Vector3(.02f, 1.77f, .71f), new Vector3(side * 2.05f, .99f, z), Mat("242730", .64f));
                Box(root, new Vector3(.052f, .055f, .14f), new Vector3(side * 2.015f, .91f, z + .23f), steel);
                Cylinder(root, .037f, .037f, 2.7f, new Vector3(side * 2.06f, 1.38f, z - 1.1f), steel, sides: 8);
                Box(root, new Vector3(.08f, .16f, .34f), new Vector3(side * 2.04f, 2.23f, z), steel);
                Box(root, new Vector3(.085f, .028f, .26f), new Vector3(side * 2.03f, 2.15f, z), Glowing("d39555", .85f));
            }

            var lamp = new OmniLight3D {
                Position = new Vector3(side * 1.91f, 2.05f, -1.95f + side * .32f),
                LightColor = new Color("f6ad67"),
                LightEnergy = .54f,
                OmniRange = 4.7f,
                OmniAttenuation = 1.45f,
            };
            root.AddChild(lamp);
        }

        brickJoints.VisibleInstanceCount = instance;
        foreach (float z in new[] { Alley.BranchNear - .13f, Alley.BranchFar + .13f }) {
            float span = -2.15f - branchEnd;
            Box(root, new Vector3(span, 6, .25f), new Vector3((branchEnd - 2.15f) / 2, 3, z), brick);
            Box(root, new Vector3(span, .24f, .29f), new Vector3((branchEnd - 2.15f) / 2, .12f, z), concrete);
        }
        Box(root, new Vector3(.25f, 6, branchDepth + .5f),
            new Vector3(branchEnd, 3, (Alley.BranchNear + Alley.BranchFar) / 2), brick);
        // A service light catches his pack as he passes the mouth without lighting the street.
        float branchLampZ = Alley.BranchFar - .1f;
        Box(root, new Vector3(.32f, .15f, .19f), new Vector3(-3.25f, 2.36f, branchLampZ), steel);
        Box(root, new Vector3(.27f, .035f, .14f), new Vector3(-3.25f, 2.28f, branchLampZ), Glowing("b6bbaf", 1.05f));
        root.AddChild(new OmniLight3D {
            Position = new Vector3(-3.25f, 2.15f, branchLampZ - .28f),
            LightColor = new Color("bbbead"),
            LightEnergy = 1.15f,
            OmniRange = 4.8f,
            OmniAttenuation = 1.36f,
            ShadowEnabled = true,
        });
        root.AddChild(new OmniLight3D {
            Position = new Vector3(-5.0f, 2.45f, Alley.BranchNear + .53f),
            LightColor = new Color("7e96b7"),
            LightEnergy = .85f,
            OmniRange = 5.4f,
            OmniAttenuation = 1.14f,
        });

        var bin = Pivot(root, new Vector3(1.73f, 0, 5.3f), new Vector3(0, 176, 0));
        Box(bin, new Vector3(.66f, .89f, 1.15f), new Vector3(0, .49f, 0), Metal("344844", .53f, .5f));
        Box(bin, new Vector3(.72f, .075f, 1.21f), new Vector3(0, .96f, 0), steel, new Vector3(0, 0, 4));
        for (int i = 0; i < 5; i++) {
            Box(bin, new Vector3(.028f, .77f, .043f), new Vector3(.34f, .48f, -.44f + i * .22f), steel);
            Box(root, new Vector3(.03f, .008f, .29f), new Vector3(-.32f + i * .065f, .007f, -1.22f), steel);
        }
        // Dark facades across the street close the view beyond the open end of the alley.
        Box(root, new Vector3(15, 7, .4f), new Vector3(0, 3.5f, 13), concrete);
        for (int i = -3; i <= 3; i++)
            Box(root, new Vector3(.72f, 1.15f, .1f), new Vector3(i * 1.82f, 3.44f, 12.75f), Glowing("434b5e", .3f));
        // The street walls catch police lights from around the corner, outside the camera's line of sight.
        OmniLight3D Beacon(string color, Vector3 at) {
            var light = new OmniLight3D {
                Position = at,
                LightColor = new Color(color),
                OmniRange = 15,
                OmniAttenuation = 1.35f,
                LightSpecular = 1,
                ShadowEnabled = true,
            };
            root.AddChild(light);
            return light;
        }
        alley.Red = Beacon("ff242d", new Vector3(1.6f, 1.18f, 8.95f));
        alley.Blue = Beacon("326aff", new Vector3(.44f, 1.38f, 9.4f));
        return alley;
    }

    // ---------- Train ----------

    public sealed class Train {
        public readonly Node3D Root = new();
        /// <summary>Everything outside the windows, which slides past: trees, poles and the ground's sleepers.</summary>
        public readonly List<Node3D> Passing = new();
    }

    /// <summary>How far the scenery outside the train stretches along the track before it comes round again.</summary>
    public const float TrackLoop = 240;

    /// <summary>
    /// A train car's window seats with the hero's bench at the origin and the window on his left.
    /// Outside, the game's trees stand along the track for the film to slide past.
    /// </summary>
    public static Train BuildTrain() {
        var train = new Train();
        var car = Pivot(train.Root, Vector3.Zero);
        const float wallL = .64f, wallR = -2.3f, pitch = 1.7f;
        var panel = Mat("d9d3c2", .7f); var trim = Metal("9aa0a2", .4f, .7f);
        Box(car, new Vector3(wallL - wallR, .1f, 16), new Vector3((wallL + wallR) / 2, -.05f, 0), Mat("5a5148", .8f));
        Box(car, new Vector3(.7f, .012f, 16), new Vector3(-.83f, .006f, 0), Cloth("7c3f3a"));
        var roof = Box(car, new Vector3(wallL - wallR + .2f, .1f, 16), new Vector3((wallL + wallR) / 2, 2.25f, 0), Mat("e6e1d2", .8f));
        Unshadowed(roof);
        foreach (float x in new[] { wallL, wallR }) {
            float side = x > 0 ? 1 : -1;
            Box(car, new Vector3(.1f, .82f, 16), new Vector3(x + side * .05f, .41f, 0), panel);
            Box(car, new Vector3(.1f, .55f, 16), new Vector3(x + side * .05f, 1.975f, 0), panel);
            Box(car, new Vector3(.16f, .04f, 16), new Vector3(x - side * .02f, .84f, 0), trim);
            // Pillars between the windows, one behind each seat back.
            for (int row = -5; row <= 5; row++) Box(car, new Vector3(.1f, .9f, .32f), new Vector3(x + side * .05f, 1.26f, row * pitch - .42f), panel);
            // Luggage racks.
            Box(car, new Vector3(.42f, .025f, 16), new Vector3(x - side * .2f, 1.82f, 0), trim);
        }
        var cloth = Cloth("3f5f7a"); var cover = Cloth("ece8dc");
        for (int row = -4; row <= 4; row++)
            foreach (float x in new[] { .1f, -1.76f }) {
                var bench = Pivot(car, new Vector3(x, 0, row * pitch));
                Box(bench, new Vector3(1, .14f, .5f), new Vector3(0, SeatTop - .07f, 0), cloth);
                Box(bench, new Vector3(1, SeatTop - .14f, .44f), new Vector3(0, (SeatTop - .14f) / 2, 0), Mat("4a463f", .8f));
                Box(bench, new Vector3(1, .85f, .1f), new Vector3(0, SeatTop + .4f, -.3f), cloth, new Vector3(-5, 0, 0));
                foreach (float seat in new[] { -.25f, .25f }) {
                    Box(bench, new Vector3(.36f, .2f, .02f), new Vector3(seat, SeatTop + .7f, -.275f), cover, new Vector3(-5, 0, 0));
                    Box(bench, new Vector3(.425f, .012f, .022f), new Vector3(seat, SeatTop - .013f, .246f), Cloth("789096"));
                }
            }
        // Outside: a meadow, the track bed, poles close by, then the trees and far hills.
        var outside = Pivot(train.Root, new Vector3(0, -1.3f, 0));
        Box(outside, new Vector3(900, .1f, 900), new Vector3(0, -.05f, 0), Mat("7e9563", 1));
        var rng = new Random(11);
        for (int i = 0; i < 130; i++) {
            bool left = i % 4 != 0;
            float away = 8 + (float)rng.NextDouble() * (i % 3 == 0 ? 60 : 24);
            var tree = Pivot(outside, new Vector3(left ? wallL + away : wallR - away, 0, (float)rng.NextDouble() * TrackLoop - TrackLoop / 2), new Vector3(0, rng.Next(360), 0));
            tree.Scale = Vector3.One * (.9f + (float)rng.NextDouble() * .5f);
            Tree3DShape.Add(tree, rng.Next(3), rng.Next(3));
            train.Passing.Add(tree);
        }
        for (int i = 0; i < 8; i++) {
            var pole = Pivot(outside, new Vector3(wallL + 3.4f, 0, i * TrackLoop / 8 - TrackLoop / 2));
            Cylinder(pole, .09f, .07f, 6.5f, new Vector3(0, 3.25f, 0), Wood("5a4636", .9f), sides: 6);
            Box(pole, new Vector3(1.5f, .09f, .09f), new Vector3(0, 6, 0), Wood("5a4636", .9f));
            train.Passing.Add(pole);
        }
        for (int i = 0; i < 12; i++) {
            float height = 10 + (float)rng.NextDouble() * 16;
            Sphere(outside, 1, new Vector3(wallL + 150 + (float)rng.NextDouble() * 60, 0, i * 60 - 330), Mat(i % 2 == 0 ? "5d7a5a" : "67845f", 1), new Vector3(70, height, 55), 12).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        return train;
    }
}
