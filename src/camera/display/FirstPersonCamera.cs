using Godot;

namespace ChopChopMeow;

/// <summary>The eye-level view and the ground directions controlled by mouse look.</summary>
internal sealed class FirstPersonCamera {
    public const uint HeadLayer = 1u << 18;
    private const float EyeHeight = 1.63f, EyeForward = .13f, FieldOfView = 65, Near = .05f;
    // Includes the padded terrain beyond the playable valley, even looking across it diagonally.
    private const float Far = 1024;
    private const float Sensitivity = .0025f, PitchLimit = 1.45f;
    private float yaw = Mathf.Pi / 4, pitch = -.25f;

    public Vector2 Forward => GroundDirection(Vector2.Up);
    public Vector2 GroundDirection(Vector2 keys) => keys.Rotated(-yaw).Normalized();

    public void Look(Vector2 motion) {
        yaw = Mathf.Wrap(yaw - motion.X * Sensitivity, -Mathf.Pi, Mathf.Pi);
        pitch = Mathf.Clamp(pitch - motion.Y * Sensitivity, -PitchLimit, PitchLimit);
    }

    public void Frame(Camera3D camera, Vector2 player) {
        if (camera.Projection != Camera3D.ProjectionType.Perspective) {
            camera.HOffset = camera.VOffset = 0;
            camera.SetPerspective(FieldOfView, Near, Far);
            camera.CullMask &= ~HeadLayer;
        }
        camera.Rotation = new Vector3(pitch, yaw, 0);
        camera.Position = WorldView.To3(player + Forward * EyeForward, EyeHeight);
    }

    /// <summary>Hides the meshes around the camera at the player's eyes.</summary>
    public static void SetHeadLayer(Node root) {
        if (root is GeometryInstance3D part) part.Layers = HeadLayer;
        for (int i = 0; i < root.GetChildCount(); i++) SetHeadLayer(root.GetChild(i));
    }
}
