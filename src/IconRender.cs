using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Renders every item and building that has an in-game model to a transparent PNG icon with <c>--render-icons DIR</c>.
/// It needs a real window: the headless renderer draws nothing.
/// </summary>
public partial class IconRender : Node {
    public const int Size = 128;
    public GameData Data = null!;
    public string Folder = "";
    private const float GamePitch = 30;
    private readonly SubViewport viewport = new() { Size = new Vector2I(Size, Size), TransparentBg = true, OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa8X, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
    // Icons show the models alone, without ground marks.
    private readonly Camera3D camera = new() { Projection = Camera3D.ProjectionType.Orthogonal, Near = .1f, Far = 100, Current = true, CullMask = 0xFFFFF & ~WorldModels.MarkLayer };
    private readonly DirectionalLight3D sun = new() { ShadowEnabled = true, DirectionalShadowMaxDistance = 30, LightEnergy = .8f, LightColor = new Color("fff1dc") };

    public override void _Ready() {
        AddChild(viewport);
        viewport.AddChild(camera);
        viewport.AddChild(sun);
        var environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("c7d3dc"),
            AmbientLightEnergy = .32f,
        };
        viewport.AddChild(new WorldEnvironment { Environment = environment });
        PileShape.Warm(Data);
        Render();
    }

    private async void Render() {
        DirAccess.MakeDirRecursiveAbsolute(Folder);
        var ids = new List<(string Id, bool Building)>();
        foreach (var id in Data.Items.Keys) ids.Add((id, false));
        foreach (var id in Data.Buildings.Keys) ids.Add((id, true));
        int count = 0;
        foreach (var (id, building) in ids) {
            if (WorldModels.IconModel(id, building, Data) is not Node3D model) continue;
            var holder = new Node3D { Basis = Pose(id) };
            holder.AddChild(model);
            viewport.AddChild(holder);
            Frame(holder, id);
            for (int i = 0; i < 3; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var error = viewport.GetTexture().GetImage().SavePng($"{Folder}/{id}.png");
            if (error != Error.Ok) GD.PushError($"Icon {id}: {error}");
            holder.QueueFree();
            count++;
        }
        GD.Print($"ICONS RENDERED {count}");
        GetTree().Quit();
    }

    /// <summary>Hand tools stand on their sides, head to the upper right with the blade toward the upper left; everything else sits as placed.</summary>
    private static Basis Pose(string id) => id switch {
        // Tools are built along -Y toward the head with the blade facing -Z; the rake's head spans X, so it turns to face the camera first.
        "steel_axe" or "steel_pick" or "wood_hammer" or "steel_hammer" or "stone_chisel" or "steel_chisel" => new Basis(Vector3.Right, Mathf.DegToRad(135)),
        "rake" => new Basis(Vector3.Right, Mathf.DegToRad(135)) * new Basis(Vector3.Up, Mathf.DegToRad(90)),
        // The torch is built along +Z toward its flame.
        "torch" => new Basis(Vector3.Right, Mathf.DegToRad(-135)),
        _ => Basis.Identity,
    };

    /// <summary>Looks at tools from the side and at everything else from the game camera's angle, lit the way the game lights it, and fits the model.</summary>
    private void Frame(Node3D holder, string id) {
        bool tool = id is "steel_axe" or "steel_pick" or "rake" or "torch" or "wood_hammer" or "steel_hammer" or "stone_chisel" or "steel_chisel";
        // Fallen leaves lie flat, so they are seen from higher up.
        var back = tool ? Direction(15, 70) : Direction(id == "leaves" ? 60 : GamePitch, 45);
        camera.Basis = Basis.LookingAt(-back, Vector3.Up);
        // The sun keeps the place it has relative to the game camera: high on the left.
        var gameCamera = Basis.LookingAt(-Direction(GamePitch, 45), Vector3.Up);
        var light = gameCamera.Inverse() * new Vector3(1, -1.25f, -.45f).Normalized();
        sun.Basis = Basis.LookingAt(camera.Basis * light, Vector3.Up);
        var (min, max) = (new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), new Vector3(float.MinValue, float.MinValue, float.MinValue));
        Bounds(holder, Transform3D.Identity, camera.Basis.Inverse(), ref min, ref max);
        var center = camera.Basis * ((min + max) / 2);
        camera.Size = Math.Max(max.X - min.X, max.Y - min.Y) * 1.08f;
        camera.Position = center + back * 40;
    }

    /// <summary>Camera-space bounds of every visible mesh's box corners.</summary>
    private static void Bounds(Node3D node, Transform3D parent, Basis toCamera, ref Vector3 min, ref Vector3 max) {
        if (!node.Visible) return;
        var transform = parent * node.Transform;
        if (node is MeshInstance3D { Mesh: not null } mesh) {
            var box = mesh.Mesh.GetAabb();
            for (int i = 0; i < 8; i++) {
                var corner = toCamera * (transform * box.GetEndpoint(i));
                min = min.Min(corner); max = max.Max(corner);
            }
        }
        for (int i = 0; i < node.GetChildCount(); i++)
            if (node.GetChild(i) is Node3D child) Bounds(child, transform, toCamera, ref min, ref max);
    }

    /// <summary>The unit vector toward a camera raised by <paramref name="pitch"/> and turned by <paramref name="yaw"/> degrees from +Z toward +X.</summary>
    private static Vector3 Direction(float pitch, float yaw) {
        float p = Mathf.DegToRad(pitch), y = Mathf.DegToRad(yaw);
        return new Vector3(MathF.Cos(p) * MathF.Sin(y), MathF.Sin(p), MathF.Cos(p) * MathF.Cos(y));
    }
}
