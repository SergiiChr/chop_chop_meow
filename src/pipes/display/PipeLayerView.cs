using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The buried pipes seen through the surface. Its camera shares the world's framing and zoom.</summary>
internal partial class PipeLayerView : TextureRect {
    private readonly SubViewport picture = new() { OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
    private readonly Camera3D camera = new() { Projection = Camera3D.ProjectionType.Orthogonal, Near = 1, Far = 200, Current = true };
    private readonly Node3D pipes = new();
    private readonly Dictionary<int, MeshInstance3D> meshes = new();
    private readonly List<int> gone = new();
    private readonly MeshInstance3D ghost;
    private readonly Material ready, blocked;
    private static readonly Material RemovalTint = ModelKit.Flat(new Color("f25043"));
    private int revision = -1, explored = -1;
    private int? marked;
    private (int Ports, int Links, bool Ready)? preview;

    public PipeLayerView(Material ready, Material blocked) {
        this.ready = ready;
        this.blocked = blocked;
        Name = "Underground"; Visible = false; MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ExpandMode = ExpandModeEnum.IgnoreSize;
        SelfModulate = new Color(1, 1, 1, .76f);
        AddChild(picture);
        picture.AddChild(pipes);
        picture.AddChild(camera);
        picture.AddChild(new WorldEnvironment {
            Environment = new Godot.Environment {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("342b23"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("d7dbe0"),
                AmbientLightEnergy = .85f
            }
        });
        picture.AddChild(new DirectionalLight3D { LightEnergy = 1.1f, RotationDegrees = new Vector3(-55, -28, 0) });
        ghost = SteelPipeShape.Build(pipes);
        ghost.Name = "PipePreview";
        ghost.Visible = false;
        Texture = picture.GetTexture();
    }

    public void Update(World world, Camera3D surface, bool active, Vector2I? placing, float rotation,
        PipeShape shape, Vector2I? removing) {
        if (active != Visible) {
            Visible = active;
            picture.RenderTargetUpdateMode = active ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        }
        if (!active) return;
        var size = (Vector2I)GetViewportRect().Size;
        if (picture.Size != size) picture.Size = size;
        camera.Transform = surface.GlobalTransform;
        camera.Size = surface.Size;
        camera.HOffset = surface.HOffset;
        camera.VOffset = surface.VOffset;
        if (revision != world.SteelPipes.Revision || explored != world.ExploredRevision) {
            Sync(world);
            revision = world.SteelPipes.Revision;
            explored = world.ExploredRevision;
        }
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
            ghost.MaterialOverride = state.Ready ? ready : blocked;
            preview = state;
        }
        ghost.Position = WorldView.To3(at, .015f);
    }

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
