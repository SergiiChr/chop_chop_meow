using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The buried pipes seen through the surface. Its camera shares the world's framing and zoom.</summary>
internal partial class PipeLayerView : Node3D {
    private readonly MeshInstance3D composite;
    private readonly ShaderMaterial blend;
    private readonly Vector3[] feet = new Vector3[World.MaxWorkers + Net.MaxPlayers];
    private static readonly StringName Feet = "feet", FootCount = "foot_count";
    private int footCount;
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
    private static readonly Material PreviewTint = ModelKit.PlacementTint(new Color("81beff"));
    private static readonly Material Blocked = ModelKit.PlacementTint(new Color("ff7467"));
    private static readonly Material RemovalTint = ModelKit.Flat(new Color("f25043"));
    private static readonly Material WetSteel = ModelKit.Vertex(Surface.Metal, .52f, .47f, "356fc6");
    private static readonly Material RiserSteel = ModelKit.Metal("93a6af", .55f, .38f);
    private int revision = -1, explored = -1, waterRevision = -1;
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
                AmbientLightColor = new Color("e1ded7"),
                AmbientLightEnergy = .8f
            }
        });
        picture.AddChild(new DirectionalLight3D {
            LightColor = new Color("fff7e9"),
            LightEnergy = 1.5f,
            RotationDegrees = new Vector3(-48, -32, 0),
            ShadowEnabled = true,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
            ShadowBias = .015f
        });
        ghost = SteelPipeShape.Build(this);
        ghost.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        ghost.Name = "PipePreview";
        ghost.Visible = false;
        // Draw after the surface and before the fog of war. Its depth keeps the player and buildings legible.
        blend = new ShaderMaterial { Shader = new Shader { Code = CutawayShader }, RenderPriority = (int)Material.RenderPriorityMax - 2 };
        blend.SetShaderParameter("buried", picture.GetTexture());
        composite = new MeshInstance3D {
            MaterialOverride = blend,
            Mesh = new QuadMesh { Size = new Vector2(2, 2) },
            ExtraCullMargin = 16384,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(composite);
    }

    public void Update(World world, List<Thing> known, List<Vector3> people, Camera3D surface, bool active, Vector2I? placing, float rotation,
        PipeShape shape, Vector2I? removing) {
        if (active != Visible) {
            Visible = active;
            picture.RenderTargetUpdateMode = active ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        }
        if (!active) return;
        UpdateFooting(people);
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
        int water = world.SteelPipes.WaterRevision;
        bool recolor = changed || water != waterRevision;
        if (recolor) {
            foreach (var (index, pipe) in meshes)
                ShowWater(pipe, world.SteelPipes.WaterAt(index), SteelPipeShape.Finish);
            waterRevision = water;
        }
        UpdateRisers(world, known, recolor);
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
        ghost.Position = WorldView.To3(at, PipeGround.FloorY + .015f);
    }

    private void UpdateFooting(List<Vector3> people) {
        bool changed = footCount != people.Count;
        for (int i = 0; i < people.Count; i++) {
            if (feet[i] == people[i]) continue;
            feet[i] = people[i]; changed = true;
        }
        if (!changed) return;
        footCount = people.Count;
        blend.SetShaderParameter(Feet, feet);
        blend.SetShaderParameter(FootCount, footCount);
    }

    private static void ShowWater(MeshInstance3D pipe, double liters, Material empty) {
        var finish = liters > 0 ? WetSteel : empty;
        if (pipe.MaterialOverride != finish) pipe.MaterialOverride = finish;
    }

    /// <summary>Join each installed fixture to the pipe immediately under its base.</summary>
    private void UpdateRisers(World world, List<Thing> known, bool recolor) {
        connected.Clear();
        foreach (var thing in known) {
            if (thing.Kind != ThingKind.Pump && !thing.ShowsPlumbing) continue;
            var at = Terrain.TileAt(thing.Pos);
            if (!world.SteelPipes.Laid(at) || !connected.Add(at)) continue;
            bool updateFinish = recolor;
            if (!risers.TryGetValue(at, out var riser)) {
                riser = ModelKit.Cylinder(pipes, SteelPipeShape.OuterRadius, SteelPipeShape.OuterRadius, 1,
                    Vector3.Zero, RiserSteel, sides: 8);
                risers.Add(at, riser);
                updateFinish = true;
            }
            if (updateFinish) ShowWater(riser, world.SteelPipes.WaterAt(Terrain.TileIndex(at)), RiserSteel);
            // Expose the foot of the pump's discharge through stones at the waterline.
            float top = thing.Kind == ThingKind.Pump ? .17f : 0;
            float height = top - PipeGround.FloorY - SteelPipeShape.CenterHeight;
            if (Mathf.IsEqualApprox(riser.Scale.Y, height)) continue;
            riser.Scale = new Vector3(1, height, 1);
            riser.Position = WorldView.To3(at, SteelPipeShape.CenterHeight + height * .5f);
        }
        disconnected.Clear();
        foreach (var at in risers.Keys) if (!connected.Contains(at)) disconnected.Add(at);
        foreach (var at in disconnected) { risers[at].QueueFree(); risers.Remove(at); }
    }

    private static readonly string CutawayShader = $$"""
        shader_type spatial;
        render_mode unshaded, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled;
        uniform sampler2D depth : hint_depth_texture, filter_nearest;
        uniform sampler2D buried : source_color, filter_nearest, repeat_disable;
        uniform vec4 ink : source_color = vec4(0.28, 0.60, 0.99, 1.0);
        uniform vec3 feet[{{World.MaxWorkers + Net.MaxPlayers}}];
        uniform int foot_count;

        void vertex() { POSITION = vec4(VERTEX.xy, 1.0, 1.0); }

        void fragment() {
            float d = texture(depth, SCREEN_UV).r;
            #if CURRENT_RENDERER == RENDERER_COMPATIBILITY
            vec3 ndc = vec3(SCREEN_UV, d) * 2.0 - 1.0;
            #else
            vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, d);
            #endif
            vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
            vec3 surface = (INV_VIEW_MATRIX * vec4(view.xyz / view.w, 1.0)).xyz;
            float above_ground = smoothstep(0.025, 0.18, surface.y);
            vec4 trench = texture(buried, SCREEN_UV);
            // Steel is opaque in the trench viewport. Earth is translucent so a wall above it can show through.
            float steel = smoothstep(0.96, 0.995, trench.a);
            float show = trench.a * mix(1.0 - 0.88 * above_ground, 1.0, steel);
            if (steel > 0.01 && trench.r <= 1.45 * trench.g) {
                vec2 px = 1.0 / vec2(textureSize(buried, 0));
                float sides = min(texture(buried, SCREEN_UV + vec2(px.x, 0)).a, texture(buried, SCREEN_UV - vec2(px.x, 0)).a);
                float ends = min(texture(buried, SCREEN_UV + vec2(0, px.y)).a, texture(buried, SCREEN_UV - vec2(0, px.y)).a);
                float outline = smoothstep(0.025, 0.15, trench.a - min(sides, ends));
                trench.rgb = mix(trench.rgb, ink.rgb * trench.a, steel * outline);
            }
            // A boot and the ground it stands on have the same depth at contact.
            // Leave a little of the real surface around each person in front of the cutaway.
            if (show > 0.0) {
                for (int i = 0; i < foot_count; i++) {
                    vec3 offset = surface - feet[i];
                    if (offset.y >= -0.22 && offset.y <= 2.2)
                        show *= smoothstep(0.20, 0.54, dot(offset.xz, offset.xz));
                }
            }
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
