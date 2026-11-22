using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The buried pipes seen through the surface. Its camera shares the world's framing and zoom.</summary>
internal partial class PipeLayerView : Node3D {
    private readonly MeshInstance3D composite;
    private readonly SubViewport picture = new() { OwnWorld3D = true, TransparentBg = true, Msaa3D = Viewport.Msaa.Msaa2X, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
    private readonly Camera3D camera = new() { Projection = Camera3D.ProjectionType.Orthogonal, Near = 1, Far = 200, Current = true };
    private readonly PipeGround ground = new();
    private readonly Node3D pipes = new() { Position = Vector3.Up * PipeGround.FloorY };
    private readonly Dictionary<int, MeshInstance3D> meshes = new();
    private readonly List<int> gone = new();
    private readonly Dictionary<Vector2I, MeshInstance3D> risers = new();
    private readonly HashSet<Vector2I> connected = new();
    private readonly List<Vector2I> disconnected = new();
    private readonly MeshInstance3D ghost;
    private static readonly Material PreviewTint = ModelKit.Flat(new Color("87edff"));
    private static readonly Material Blocked = ModelKit.Flat(new Color("ff7467"));
    private static readonly Material RemovalTint = ModelKit.Flat(new Color("f25043"));
    private int revision = -1, explored = -1;
    private int? marked;
    private (int Ports, int Links, bool Ready)? preview;

    public PipeLayerView() {
        Name = "Underground"; Visible = false;
        AddChild(picture);
        picture.AddChild(ground);
        picture.AddChild(pipes);
        picture.AddChild(camera);
        picture.AddChild(new WorldEnvironment {
            Environment = new Godot.Environment {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Colors.Black,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("a9d5fa"),
                AmbientLightEnergy = .8f
            }
        });
        picture.AddChild(new DirectionalLight3D {
            LightColor = new Color("d2ecff"), LightEnergy = 1.5f, RotationDegrees = new Vector3(-48, -32, 0),
            ShadowEnabled = true, DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal, ShadowBias = .015f
        });
        ghost = SteelPipeShape.Build(pipes);
        ghost.Name = "PipePreview";
        ghost.Visible = false;
        // Draw after the surface and before the fog of war. Its depth keeps the player and buildings legible.
        var blend = new ShaderMaterial { Shader = new Shader { Code = CutawayShader }, RenderPriority = (int)Material.RenderPriorityMax - 1 };
        blend.SetShaderParameter("buried", picture.GetTexture());
        composite = new MeshInstance3D {
            MaterialOverride = blend, Mesh = new QuadMesh { Size = new Vector2(2, 2) },
            ExtraCullMargin = 16384, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(composite);
    }

    public void Update(World world, List<Thing> known, Camera3D surface, bool active, Vector2I? placing, float rotation,
        PipeShape shape, Vector2I? removing) {
        if (active != Visible) {
            Visible = active;
            picture.RenderTargetUpdateMode = active ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        }
        if (!active) return;
        var size = (Vector2I)GetViewport().GetVisibleRect().Size;
        composite.Transform = surface.GlobalTransform.TranslatedLocal(Vector3.Forward * 2);
        if (picture.Size != size) picture.Size = size;
        camera.Transform = surface.GlobalTransform;
        camera.Size = surface.Size;
        camera.HOffset = surface.HOffset;
        camera.VOffset = surface.VOffset;
        bool changed = revision != world.SteelPipes.Revision || explored != world.ExploredRevision;
        ground.Update(world, known, placing, changed);
        if (changed) {
            Sync(world);
            revision = world.SteelPipes.Revision;
            explored = world.ExploredRevision;
        }
        UpdateRisers(world, known);
        int? next = removing is Vector2I tile ? Terrain.TileIndex(tile) : null;
        if (next != marked) {
            if (marked is int was && meshes.TryGetValue(was, out var old)) old.MaterialOverlay = null;
            if (next is int now && meshes.TryGetValue(now, out var selected)) selected.MaterialOverlay = RemovalTint;
            marked = next;
        }
        ghost.Visible = placing.HasValue;
        if (placing is not Vector2I at) return;
        var state = (Ports: SteelPipes.PortMask(rotation, shape), Links: world.SteelPipes.PreviewMask(at, rotation, shape),
            Ready: world.Construction.PlacementBlock(ThingKind.SteelPiping, at, rotation: rotation, pipeShape: shape) == null);
        if (preview != state) {
            SteelPipeShape.SetConnections(ghost, state.Ports, state.Links);
            ghost.MaterialOverride = state.Ready ? PreviewTint : Blocked;
            preview = state;
        }
        ghost.Position = WorldView.To3(at, .015f);
    }

    /// <summary>Join each installed fixture to the pipe immediately under its base.</summary>
    private void UpdateRisers(World world, List<Thing> known) {
        connected.Clear();
        foreach (var thing in known) {
            if (thing.Kind != ThingKind.Pump && !thing.PlumbingInstalled) continue;
            var at = Terrain.TileAt(thing.Pos);
            if (!world.SteelPipes.Laid(at) || !connected.Add(at) || risers.ContainsKey(at)) continue;
            const float bottom = PipeGround.FloorY + SteelPipeShape.CenterHeight;
            risers[at] = ModelKit.Post(pipes, SteelPipeShape.OuterRadius, SteelPipeShape.OuterRadius, -bottom,
                WorldView.To3(at, SteelPipeShape.CenterHeight), ModelKit.Metal("93a6af", .55f, .38f), 8);
        }
        disconnected.Clear();
        foreach (var at in risers.Keys) if (!connected.Contains(at)) disconnected.Add(at);
        foreach (var at in disconnected) { risers[at].QueueFree(); risers.Remove(at); }
    }

    private const string CutawayShader = """
        shader_type spatial;
        render_mode unshaded, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled;
        uniform sampler2D depth : hint_depth_texture, filter_nearest;
        uniform sampler2D buried : source_color, filter_nearest, repeat_disable;

        void vertex() { POSITION = vec4(VERTEX.xy, 1.0, 1.0); }

        void fragment() {
            float d = texture(depth, SCREEN_UV).r;
            #if CURRENT_RENDERER == RENDERER_COMPATIBILITY
            vec3 ndc = vec3(SCREEN_UV, d) * 2.0 - 1.0;
            #else
            vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, d);
            #endif
            vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
            float height = (INV_VIEW_MATRIX * vec4(view.xyz / view.w, 1.0)).y;
            float above_ground = smoothstep(0.06, 0.29, height);
            vec4 trench = texture(buried, SCREEN_UV);
            // Steel is opaque in the trench viewport. Earth is translucent so a wall above it can show through.
            float steel = smoothstep(0.87, 0.99, trench.a);
            float show = trench.a * mix(1.0 - 0.88 * above_ground, 1.0, steel);
            float opacity = mix(0.64, 1.0, show);
            ALBEDO = (trench.rgb / max(0.01, trench.a)) * show / opacity;
            ALPHA = opacity;
        }
        """;

    private void Sync(World world) {
        gone.Clear();
        foreach (int index in meshes.Keys) {
            var at = new Vector2I(index % Terrain.Size, index / Terrain.Size);
            if (!world.SteelPipes.Laid(at) || !world.Explored(at)) gone.Add(index);
        }
        foreach (int index in gone) { meshes[index].QueueFree(); meshes.Remove(index); }
        foreach (int index in world.SteelPipes.Tiles) {
            var at = new Vector2I(index % Terrain.Size, index / Terrain.Size);
            if (!world.Explored(at)) continue;
            int ports = SteelPipes.PortMask(world.SteelPipes.RotationAt(at)!.Value, world.SteelPipes.ShapeAt(at)!.Value);
            int links = world.SteelPipes.ConnectionMask(at);
            if (meshes.TryGetValue(index, out var mesh)) SteelPipeShape.SetConnections(mesh, ports, links);
            else {
                mesh = SteelPipeShape.Build(pipes, ports, links);
                mesh.Position = WorldView.To3(at);
                meshes.Add(index, mesh);
            }
        }
    }
}
