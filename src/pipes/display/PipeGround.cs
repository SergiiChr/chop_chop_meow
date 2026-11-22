using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The soil and survey marks under the work area, with exposed earth between its floor and the surface.</summary>
internal partial class PipeGround : MeshInstance3D {
    public const float FloorY = -.9f;
    private readonly HashSet<Vector2I> tiles = new(), sockets = new(), nextSockets = new();
    private readonly HashSet<Vector2I> previewTiles = new();
    private readonly MeshInstance3D previewSoil = new() { CastShadow = ShadowCastingSetting.Off };
    private Vector2I? lastPlayer, lastPreview;
    private readonly Image openings = Image.CreateEmpty(Terrain.Size, Terrain.Size, false, Image.Format.L8);
    private readonly ImageTexture openingMap;

    public PipeGround() {
        Name = "ExcavatedSoil";
        var soil = new ShaderMaterial { Shader = new Shader { Code = SoilShader } };
        openingMap = ImageTexture.CreateFromImage(openings);
        soil.SetShaderParameter("openings", openingMap);
        soil.SetShaderParameter("stone", ModelKit.ScanMap(Scan.Gravel, "albedo"));
        soil.SetShaderParameter("soil_height", ModelKit.ScanMap(Scan.Soil, "height"));
        MaterialOverride = previewSoil.MaterialOverride = soil;
        AddChild(previewSoil);
        CastShadow = ShadowCastingSetting.Off;
    }

    /// <summary>Cut a work area around the player, and a narrower bed along each pipe and around each inlet.</summary>
    public void Update(World world, List<Thing> known, Vector2I? preview, bool layoutChanged) {
        nextSockets.Clear();
        foreach (var thing in known)
            if (thing.Kind == ThingKind.Pump || Plumbing.HasPlumbing(thing.Kind)) nextSockets.Add(Terrain.TileAt(thing.Pos));
        Vector2I? player = world.SteelPipes.Tiles.Count == 0 ? Terrain.TileAt(world.Player) : null;
        layoutChanged |= player != lastPlayer || !sockets.SetEquals(nextSockets);
        if (!layoutChanged && preview == lastPreview) return;
        if (layoutChanged) {
            lastPlayer = player;
            sockets.Clear(); sockets.UnionWith(nextSockets);
            tiles.Clear();
            if (player is Vector2I start) AddArea(tiles, start);
            foreach (int index in world.SteelPipes.Tiles) AddArea(tiles, new Vector2I(index % Terrain.Size, index / Terrain.Size));
            foreach (var at in sockets) AddArea(tiles, at);
            openings.Fill(Colors.Black);
            foreach (var at in tiles) openings.SetPixel(at.X, at.Y, Colors.White);
            Mesh = Cut(tiles);
        } else {
            foreach (var at in previewTiles) openings.SetPixel(at.X, at.Y, Colors.Black);
        }
        // Moving the cursor changes at most nine floor tiles, even beside a large network.
        lastPreview = preview;
        previewTiles.Clear();
        if (preview is Vector2I cursor) AddArea(previewTiles, cursor);
        previewTiles.ExceptWith(tiles);
        foreach (var at in previewTiles) openings.SetPixel(at.X, at.Y, Colors.White);
        previewSoil.Mesh = Cut(previewTiles);
        openingMap.Update(openings);

        void AddArea(HashSet<Vector2I> area, Vector2I center) {
            if (!Terrain.OnMap(center) || !world.Explored(center)) return;
            for (int z = -1; z <= 1; z++)
                for (int x = -1; x <= 1; x++) {
                    var at = center + new Vector2I(x, z);
                    if (Terrain.OnMap(at) && world.Explored(at)) area.Add(at);
                }
        }
    }

    private static ArrayMesh? Cut(HashSet<Vector2I> tiles) {
        if (tiles.Count == 0) return null;
        var mesh = new SurfaceTool();
        mesh.Begin(Godot.Mesh.PrimitiveType.Triangles);
        foreach (var at in tiles) {
            var nw = new Vector3(at.X - .5f, FloorY, at.Y - .5f);
            var ne = nw + Vector3.Right; var sw = nw + Vector3.Back; var se = ne + Vector3.Back;
            Face(nw, ne, se, sw, Vector3.Up);
            // Only the inward faces show, so a near bank never hides the work behind it.
            Wall(at + Vector2I.Up, nw, ne, Vector3.Back);
            Wall(at + Vector2I.Right, ne, se, Vector3.Left);
            Wall(at + Vector2I.Down, se, sw, Vector3.Forward);
            Wall(at + Vector2I.Left, sw, nw, Vector3.Right);
        }
        mesh.Index();
        return mesh.Commit();

        void Wall(Vector2I next, Vector3 from, Vector3 to, Vector3 inward) {
            if (!tiles.Contains(next)) Face(from, from - Vector3.Up * FloorY, to - Vector3.Up * FloorY, to, inward);
        }

        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal) {
            mesh.SetNormal(normal);
            mesh.AddVertex(a); mesh.AddVertex(b); mesh.AddVertex(c);
            mesh.AddVertex(a); mesh.AddVertex(c); mesh.AddVertex(d);
        }
    }

    // The same soil relief as the farm. World-space sampling keeps its grain continuous across tiles.
    private const string SoilShader = """
        shader_type spatial;
        uniform sampler2D openings : repeat_disable, filter_nearest;
        uniform sampler2D stone : repeat_enable, filter_linear_mipmap_anisotropic;
        uniform sampler2D soil_height : repeat_enable, filter_linear_mipmap_anisotropic;
        uniform vec4 earth : source_color = vec4(0.32, 0.28, 0.23, 1.0);
        uniform vec4 subsoil : source_color = vec4(0.21, 0.16, 0.12, 1.0);
        uniform vec4 ink : source_color = vec4(0.28, 0.60, 0.99, 1.0);
        varying vec3 pos;
        varying vec3 face;

        void vertex() {
            pos = VERTEX;
            face = NORMAL;
        }

        void fragment() {
            // Clip to the opening at grade. The near bank hides the floor behind its unbroken surface.
            vec3 eye = INV_VIEW_MATRIX[2].xyz;
            vec2 grade = pos.xz - pos.y * eye.xz / max(0.05, eye.y);
            float edge = texture(soil_height, grade / 2.3).r;
            if (texture(openings, (grade + 0.5 + 0.16 * (edge - 0.3)) / vec2(textureSize(openings, 0))).r < 0.5) discard;
            bool floor_face = face.y > 0.5;
            // Where the cursor opens the next tile, the old bank becomes part of the floor.
            if (!floor_face && texture(openings, (pos.xz - 0.1 * face.xz + 0.5) / vec2(textureSize(openings, 0))).r > 0.5) discard;
            vec2 uv = floor_face ? pos.xz : vec2(pos.x + pos.z, pos.y);
            float grit = texture(soil_height, uv / 1.65).r;
            float coarse = texture(soil_height, uv / 6.7).r;
            float rock = texture(stone, uv / 6.5).g;
            float stroke;
            if (floor_face) {
                vec2 tile = floor(pos.xz + 0.5);
                vec2 local = pos.xz - tile;
                vec2 gap = 0.5 - abs(local);
                vec2 grid = 1.0 - smoothstep(vec2(0.0), max(1.3 * fwidth(pos.xz), vec2(0.01)), gap);
                vec2 side = gap.x < gap.y ? vec2(sign(local.x), 0.0) : vec2(0.0, sign(local.y));
                float neighbor = texture(openings, (tile + side + 0.5) / vec2(textureSize(openings, 0))).r;
                float ticks = max(grid.x * (1.0 - smoothstep(0.04, 0.12, gap.y)),
                                  grid.y * (1.0 - smoothstep(0.04, 0.12, gap.x)));
                stroke = max(0.66 * ticks, (1.0 - neighbor) * max(grid.x, grid.y));
                ALBEDO = earth.rgb;
                ALPHA = 0.64;
            } else {
                float depth = -pos.y;
                float band = depth + (coarse - 0.29) * 1.3 + (rock - 0.5) * 0.17;
                vec3 dirt = mix(vec3(0.043, 0.034, 0.023), subsoil.rgb, smoothstep(0.01, 0.35, band));
                ALBEDO = mix(dirt, earth.rgb, smoothstep(0.28, 0.88, band));
                stroke = 1.0 - smoothstep(0.0, max(1.3 * fwidth(pos.y), 0.008), depth);
                ALBEDO *= 1.0 - 0.40 * (1.0 - smoothstep(0.0, 0.035, abs(band - 0.45)));
                ALPHA = 0.83;
            }
            ALBEDO *= 0.35 + 1.33 * rock * rock + 1.03 * grit + 0.5 * coarse;
            ALBEDO = mix(ALBEDO, ink.rgb, stroke);
            EMISSION = 0.18 * ink.rgb * stroke;
            ROUGHNESS = 1.0;
        }
        """;
}
