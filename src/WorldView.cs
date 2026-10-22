using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

public partial class WorldView : Node2D
{
    public World World = null!;
    public int Selected;
    public int Hover;
    public int SelectedWorker;
    public string BuildMode = "";
    public Vector2 Mouse;
    public float Zoom = 1;
    public Vector2 MoveDirection;
    public bool Gathering;
    public float Elapsed;
    public bool PanelOpen;
    private Texture2D atlas = null!;
    private Texture2D actors = null!;
    private Texture2D workers = null!;
    private Texture2D? details;
    private Image? detailImage;
    private Image atlasImage = null!;
    private Texture2D? extras;
    private Font font = ThemeDB.FallbackFont;
    private int facing;
    private static readonly Color Ink = new("283d34");
    private readonly Dictionary<string, int> cells = new() { ["tree"] = 0, ["stone"] = 1, ["iron"] = 2, ["loose_iron"] = 2, ["coal"] = 3, ["berries"] = 4, ["furnace"] = 5, ["reinforced"] = 6, ["hut"] = 7, ["chest"] = 8, ["grave"] = 8, ["anvil"] = 9, ["signpost"] = 10, ["campfire"] = 11 };
    public override void _Ready()
    {
        atlas = GD.Load<Texture2D>("res://assets/world-atlas.png");
        actors = GD.Load<Texture2D>("res://assets/player-atlas.png");
        workers = GD.Load<Texture2D>("res://assets/worker-atlas.png");
        if (ResourceLoader.Exists("res://assets/environment-details.png"))
        {
            details = GD.Load<Texture2D>("res://assets/environment-details.png");
            detailImage = details.GetImage();
            if (detailImage.IsCompressed()) detailImage.Decompress();
        }
        atlasImage = atlas.GetImage();
        if (atlasImage.IsCompressed()) atlasImage.Decompress();
        if (ResourceLoader.Exists("res://assets/travel-atlas.png")) extras = GD.Load<Texture2D>("res://assets/travel-atlas.png");
        TextureFilter = TextureFilterEnum.Linear;
    }
    public Vector2 Center => GetViewportRect().Size * new Vector2(PanelOpen ? .38f : .5f, .52f);
    public static Vector2 Iso(Vector2 p) => new((p.X - p.Y) * 44, (p.X + p.Y) * 22);
    public Vector2 Screen(Vector2 p) => (Iso(p) - Iso(World.Player)) * Zoom + Center;
    public Vector2 ToWorld(Vector2 p)
    {
        var q = (p - Center) / Zoom + Iso(World.Player);
        return new Vector2(q.X / 88 + q.Y / 44, q.Y / 44 - q.X / 88);
    }
    // One scale anchor: an adult actor is 85 px. Small props stay below waist height,
    // the hut has an adult-sized doorway, and tree crowns rise well above the roof.
    private static float Height(Thing t) => t.Kind switch
    {
        "tree" => 210 + t.Id % 5 * 7,
        "hut" => 270,
        "reinforced" => 98,
        "furnace" => 86,
        "stone" or "iron" or "coal" => 58,
        "loose_iron" => 27,
        "berries" => 51,
        "signpost" => 87,
        "anvil" => 53,
        "water" => 24,
        "campfire" => 48,
        _ => 46
    };
    private Vector2 ThingSize(Thing t) => new Vector2(Height(t) * (t.Kind == "tree" ? .88f : t.Kind == "reinforced" ? 1.20f : 1.05f), Height(t)) * Zoom;
    private Rect2 Bounds(Thing t)
    {
        var size = ThingSize(t);
        return new Rect2(Screen(t.Pos) - new Vector2(size.X / 2, size.Y * .93f), size);
    }
    private bool Hit(Thing t, Vector2 screen)
    {
        var bounds = Bounds(t);
        if (!bounds.HasPoint(screen)) return false;
        if (t.Kind == "water") return true;
        if (!cells.TryGetValue(t.Kind, out int cell)) return false;
        var uv = (screen - bounds.Position) / bounds.Size;
        bool hutDetail = t.Kind == "hut" && detailImage != null;
        var image = hutDetail ? detailImage! : atlasImage;
        int columns = hutDetail ? 2 : 4, rows = hutDetail ? 1 : 3;
        if (hutDetail) cell = 1;
        int w = image.GetWidth() / columns, h = image.GetHeight() / rows;
        int x = cell % columns * w + Math.Clamp((int)(uv.X * w), 0, w - 1);
        int y = cell / columns * h + Math.Clamp((int)(uv.Y * h), 0, h - 1);
        return image.GetPixel(x, y).A > .20f;
    }
    public Thing? Pick(Vector2 screen) => World.S.Things.Where(t => (t.Remaining > 0 || !t.IsResource || t.Kind == "berries") && Hit(t, screen))
        .OrderByDescending(t => t.X + t.Y).FirstOrDefault();
    public Worker? PickWorker(Vector2 screen) => World.S.Workers.FirstOrDefault(w => new Rect2(Screen(w.Pos) - new Vector2(24, 77) * Zoom, new Vector2(48, 84) * Zoom).HasPoint(screen));
    public override void _Process(double delta)
    {
        Elapsed += (float)delta;
        if (MoveDirection.LengthSquared() > .01f)
        {
            var d = Iso(MoveDirection);
            facing = d.Y >= 0 ? d.X >= 0 ? 0 : 1 : d.X >= 0 ? 2 : 3;
        }
        QueueRedraw();
    }
    private void Diamond(Vector2 p, float size, Color c)
    {
        DrawColoredPolygon(new[] { p + new Vector2(0, -22 * size), p + new Vector2(44 * size, 0), p + new Vector2(0, 22 * size), p + new Vector2(-44 * size, 0) }, c);
    }
    private void Ellipse(Vector2 p, Vector2 radius, Color color)
    {
        var points = new Vector2[40];
        for (int i = 0; i < points.Length; i++) { float a = i * Mathf.Tau / points.Length; points[i] = p + new Vector2(MathF.Cos(a) * radius.X, MathF.Sin(a) * radius.Y); }
        DrawColoredPolygon(points, color);
    }
    private void Text(Vector2 p, string s, int size, Color color) => DrawString(font, p, s, HorizontalAlignment.Center, -1, size, color);
    private void Sprite(Texture2D texture, int cell, int columns, int rows, Vector2 basePoint, Vector2 size, Color tint)
    {
        var region = new Rect2(cell % columns * texture.GetWidth() / (float)columns, cell / columns * texture.GetHeight() / (float)rows, texture.GetWidth() / (float)columns, texture.GetHeight() / (float)rows);
        var rect = new Rect2(basePoint - new Vector2(size.X / 2, size.Y * .93f), size);
        DrawTextureRectRegion(texture, rect, region, tint);
    }
    private void DrawThing(Thing t, Color? overrideTint = null)
    {
        var p = Screen(t.Pos); float h = Height(t) * Zoom;
        var tint = overrideTint ?? Colors.White;
        if (t.Kind == "water")
        {
            DrawCircle(p, 11 * Zoom, new Color("a4d7e2")); Text(p + new Vector2(-15, -18), "Water", 15, Ink); return;
        }
        if (t.IsResource && t.Remaining == 0 && t.Kind != "berries")
        {
            if (t.Kind == "tree") { Ellipse(p, new Vector2(12, 7) * Zoom, new Color("886d45")); }
            return;
        }
        bool selected = t.Id == Selected || t.Id == Hover;
        if (selected) Ellipse(p, new Vector2(36, 17) * Zoom, new Color(.92f, .83f, .46f, .5f));
        if (t.Kind != "hut") Ellipse(p + new Vector2(4, 1) * Zoom, new Vector2(h * .32f, h * .10f), new Color(.14f, .22f, .13f, .18f));
        if (t.Kind == "berries" && t.Remaining == 0) tint = new Color(.6f, .7f, .5f, 1);
        if (t.Kind == "grave") tint = new Color(1, .75f, .5f);
        if (t.Kind == "campfire" && t.Burn == 0) tint = new Color(.38f, .4f, .36f);
        // Canopies fade over people, keeping the player and overall-clad crew readable.
        if (t.Kind == "tree" &&
            (Bounds(t).HasPoint(Screen(World.Player) - new Vector2(0, 40)) && t.X + t.Y > World.Player.X + World.Player.Y ||
             World.S.Workers.Any(w => Bounds(t).HasPoint(Screen(w.Pos) - new Vector2(0, 40)) && t.X + t.Y > w.X + w.Y))) tint.A = .26f;
        if (t.Kind == "hut" && details != null) Sprite(details, 1, 2, 1, p, ThingSize(t), tint);
        else Sprite(atlas, cells.GetValueOrDefault(t.Kind), 4, 3, p, ThingSize(t), tint);
        if (t.Kind is "furnace" or "reinforced" or "campfire" && (t.Processing || t.Burn > 0))
        {
            for (int i = 0; i < 3; i++)
            {
                float age = (Elapsed * .6f + i * .33f) % 1;
                DrawCircle(p + new Vector2(MathF.Sin(age * 8 + i) * 9, -h * .36f - age * 40 * Zoom), (2 - age) * Zoom, new Color(1, .65f, .2f, (1 - age) * .8f));
            }
        }
        if (selected || t.Processing)
        {
            string name = t.Kind == "loose_iron" ? "Loose iron · pick up by hand" : t.Kind == "iron" ? "Iron deposit" : t.Kind == "coal" ? "Mined coal" : World.Data.Name(t.Kind);
            if (t.Kind == "grave") name = "Recovery box";
            var label = p + new Vector2(-Math.Min(100, name.Length * 4), -h - 8);
            DrawStyleBox(new StyleBoxFlat { BgColor = new Color(.12f, .18f, .15f, .86f), CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5 }, new Rect2(label + new Vector2(-5, -17), new Vector2(name.Length * 7.2f + 10, 24)));
            Text(label, name, 14, new Color("f5ecd1"));
            if (t.Processing && World.Data.Recipes.TryGetValue(t.Recipe, out var r))
            {
                DrawRect(new Rect2(p + new Vector2(-28, 10), new Vector2(56, 5)), new Color("2b4135"));
                DrawRect(new Rect2(p + new Vector2(-28, 10), new Vector2(56 * t.Progress / r.Seconds, 5)), new Color("e5bf68"));
            }
        }
    }
    private void DrawActor(Vector2 pos, int direction, bool moving, bool worker = false)
    {
        var p = Screen(pos); float height = 85 * Zoom;
        Ellipse(p, new Vector2(13, 6) * Zoom, new Color(0, 0, 0, .23f));
        int frame = direction + (moving && (int)(Elapsed * 5) % 2 == 1 ? 4 : 0);
        float bob = moving ? MathF.Sin(Elapsed * 12) * 1.2f * Zoom : 0;
        Sprite(worker ? workers : actors, frame, 4, 2, p + new Vector2(0, bob), new Vector2(height * .75f, height), Colors.White);
        if (!worker) { DrawArc(p, 18 * Zoom, 0, Mathf.Tau, 32, new Color(.95f, .88f, .57f, .8f), 1.4f, true); }
    }
    public override void _Draw()
    {
        if (World == null || atlas == null) return;
        DrawRect(GetViewportRect(), new Color("879c69"));
        var viewport = GetViewportRect().Grow(260);
        // Broad, quiet grass patches replace the checkerboard of individually colored tiles.
        for (int x = 0; x < World.MapSize; x += 2) for (int y = 0; y < World.MapSize; y += 2)
        {
            float seed = ((x * 127 + y * 311 + x * y * 7) % 31) / 31f;
            var p = Screen(new Vector2(x + seed * .6f, y - seed * .4f));
            if (!viewport.HasPoint(p)) continue;
            Ellipse(p, new Vector2(108 + seed * 38, 48 + seed * 15) * Zoom, new Color(.35f, .47f, .27f, .025f + seed * .025f));
            if (seed > .7f)
            {
                var q = p + new Vector2(seed * 27, seed * 13) * Zoom;
                DrawLine(q, q + new Vector2(-2, -4) * Zoom, new Color(.39f, .51f, .29f, .28f), 1, true);
                DrawLine(q, q + new Vector2(3, -3) * Zoom, new Color(.39f, .51f, .29f, .28f), 1, true);
                if (seed > .95f) DrawCircle(q + new Vector2(2, -4) * Zoom, 1.5f * Zoom, new Color("d7d49c"));
            }
        }
        for (int ring = 16; ring > 0; ring--)
            Ellipse(Screen(ChopChopMeow.World.Spawn), new Vector2(150, 68) * Zoom * ring / 16f, new Color(.66f, .60f, .41f, .018f));
        // A narrow, worn trail with soft shoulders; the grid remains implicit for building placement.
        for (int layer = 3; layer > 0; layer--)
        {
            float width = .33f + layer * .12f;
            var edges = new List<Vector2>();
            for (int x = 22; x <= 49; x++) edges.Add(Screen(new Vector2(x, 24 + MathF.Sin(x * .6f) * .13f - width)));
            for (int x = 49; x >= 22; x--) edges.Add(Screen(new Vector2(x, 24 + MathF.Sin(x * .6f) * .13f + width)));
            DrawColoredPolygon(edges.ToArray(), new Color(.67f, .60f, .43f, layer == 1 ? .28f : .14f));
        }
        var pond = Screen(ChopChopMeow.World.Pond);
        if (details != null)
        {
            float cell = details.GetWidth() / 2f;
            DrawTextureRectRegion(details, new Rect2(pond - new Vector2(170, 170) * Zoom, new Vector2(340, 340) * Zoom), new Rect2(0, 0, cell, details.GetHeight()));
        }
        else
        {
            Ellipse(pond, new Vector2(150, 73) * Zoom, new Color("b1ae79"));
            Ellipse(pond, new Vector2(130, 63) * Zoom, new Color("6e9b9b"));
        }
        var list = new List<(float Depth, Action Draw)>();
        foreach (var t in World.S.Things)
        {
            var captured = t;
            if (!viewport.HasPoint(Screen(t.Pos))) continue;
            list.Add((t.X + t.Y, () => DrawThing(captured)));
        }
        list.Add((World.Player.X + World.Player.Y, () => DrawActor(World.Player, facing, MoveDirection.LengthSquared() > .01f || Gathering)));
        foreach (var w in World.S.Workers)
        {
            var worker = w;
            list.Add((w.X + w.Y, () =>
            {
                DrawActor(worker.Pos, worker.Destination != 0 ? 2 : 0, worker.Status.StartsWith("Carrying") || worker.Status.StartsWith("Fetching"), true);
                var p = Screen(worker.Pos);
                if (worker.Id == SelectedWorker) Ellipse(p, new Vector2(22, 10) * Zoom, new Color(1, .8f, .3f, .35f));
                Text(p + new Vector2(-20, -80) * Zoom, worker.Name, 13, Ink);
                if (worker.Bag.Count > 0) DrawCircle(p - new Vector2(12, 37) * Zoom, 5 * Zoom, new Color("e1c383"));
            }
            ));
        }
        foreach (var wolf in World.S.Wolves)
        {
            var captured = wolf;
            list.Add((wolf.X + wolf.Y, () =>
            {
                var p = Screen(captured.Pos);
                if (extras != null) Sprite(extras, 0, 3, 1, p, new Vector2(86, 52) * Zoom, Colors.White);
                else { Ellipse(p - new Vector2(0, 12), new Vector2(22, 12) * Zoom, new Color("5a5d58")); DrawCircle(p + new Vector2(19, -18) * Zoom, 9 * Zoom, new Color("6d7168")); }
                DrawCircle(p + new Vector2(20, -20) * Zoom, 2 * Zoom, new Color("e2a570"));
            }
            ));
        }
        foreach (var order in World.S.Orders)
        {
            var sign = World.Find(order.Signpost); if (sign == null) continue;
            float phase = order.Elapsed / World.Data.TradeSeconds;
            Vector2 destination = new(49, 24);
            var pos = sign.Pos.Lerp(destination, phase < .5f ? phase * 2 : (1 - phase) * 2);
            list.Add((pos.X + pos.Y, () =>
            {
                var p = Screen(pos);
                bool cash = !order.Buying && phase >= .5f;
                if (extras != null) Sprite(extras, cash ? 2 : 1, 3, 1, p, new Vector2(108, 72) * Zoom, Colors.White);
                else { DrawRect(new Rect2(p - new Vector2(22, 25), new Vector2(44, 24)), new Color("947342")); DrawCircle(p + new Vector2(-16, 1), 8, Ink); DrawCircle(p + new Vector2(16, 1), 8, Ink); }
                Text(p + new Vector2(-40, -88) * Zoom, cash ? "Payment returning" : order.Buying ? "Village purchase" : "Goods to village", 12, Ink);
            }
            ));
        }
        foreach (var entry in list.OrderBy(v => v.Depth)) entry.Draw();
        if (BuildMode != "")
        {
            var pos = ToWorld(Mouse).Round(); bool valid = World.PlacementBlock(BuildMode, pos) == null;
            Diamond(Screen(pos), Zoom * 1.4f, valid ? new Color(.7f, .9f, .5f, .5f) : new Color(.9f, .3f, .2f, .5f));
            DrawThing(new Thing { Kind = BuildMode, Pos = pos }, valid ? new Color(1, 1, 1, .65f) : new Color(1, .5f, .5f, .6f));
        }
        // Dusk deepens into full night and dawn fades out, so neither boundary jumps in brightness.
        float dayPhase = World.DayPhase, dusk = World.Data.NightStarts - .1f;
        float darkness = World.Night ? .65f : dayPhase > dusk ? .02f + (dayPhase - dusk) / .1f * .63f : dayPhase < .05f ? .65f - dayPhase / .05f * .63f : .02f;
        if (darkness > 0)
        {
            DrawRect(GetViewportRect(), new Color(.05f, .10f, .22f, darkness));
            foreach (var fire in World.S.Things.Where(t => t.Kind == "campfire" && t.Burn > 0 || t.Processing && t.Kind is "furnace" or "reinforced"))
            {
                var p = Screen(fire.Pos); float radius = fire.Kind == "campfire" ? 233 : 65;
                for (int i = 40; i > 0; i--) Ellipse(p - new Vector2(0, 10) * Zoom, new Vector2(radius, radius * .5f) * Zoom * i / 40f, new Color(1, .66f, .24f, .006f));
            }
        }
        // The landmark guide follows the player's position rather than assuming they remain at spawn.
        string Direction(Vector2 target)
        {
            var d = Iso(target - World.Player);
            if (d.Length() < 95) return "HERE";
            if (Math.Abs(d.X) > Math.Abs(d.Y) * 2) return d.X > 0 ? "→" : "←";
            if (Math.Abs(d.Y) > Math.Abs(d.X) * 2) return d.Y > 0 ? "↓" : "↑";
            return d.Y > 0 ? d.X > 0 ? "↘" : "↙" : d.X > 0 ? "↗" : "↖";
        }
        Text(new Vector2(25, GetViewportRect().Size.Y - 210), $"POND {Direction(World.Pond)}    IRON {Direction(new Vector2(30, 18))}    COAL {Direction(new Vector2(37, 20))}", 13, new Color("f4ecd3"));
    }
}
