using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>The eyes and brows for the film's close-ups, with the look and expression set per pose.</summary>
internal sealed class FilmFace {
    private readonly Node3D[] eyes = new Node3D[2], pupils = new Node3D[2], brows = new Node3D[2];
    private readonly Node3D? mouth;
    private const float MouthRadius = .029f;
    private static readonly Vector3 MouthCenter = new(0, .073f, .104f);

    /// <summary>The visible front of the mouth in its current pose.</summary>
    public Vector3 MouthPosition => mouth!.ToGlobal(Vector3.Back * MouthRadius);

    public FilmFace(ActorRig rig, ActorSkin skin, bool showMouth) {
        rig.FaceMarks.Hide();
        var head = rig.Head;
        var hair = Mat(ActorRig.OutfitOf(skin).Hair, .82f);
        var white = Mat("daceb9", .62f);
        var ink = Mat("322826", .61f);
        for (int i = 0; i < eyes.Length; i++) {
            float side = i == 0 ? -1 : 1;
            var eye = eyes[i] = Pivot(head, new Vector3(side * .041f, .146f, .108f), new Vector3(0, side * 16, 0));
            Sphere(eye, .019f, Vector3.Zero, white, new Vector3(1, .71f, .32f), 12);
            var pupil = pupils[i] = Pivot(eye, new Vector3(0, 0, .0046f));
            Sphere(pupil, .011f, Vector3.Zero, ink, new Vector3(.88f, 1, .34f), 12);
            Sphere(pupil, .003f, new Vector3(-.003f, .0038f, .003f), white, new Vector3(1, 1, .4f), 8);
            var brow = brows[i] = Sphere(head, .022f, new Vector3(side * .046f, .175f, .108f), hair, new Vector3(1, .24f, .38f), 12);
            brow.RotationDegrees = new Vector3(0, side * 17, 0);
        }
        if (showMouth)
            mouth = Sphere(head, MouthRadius, MouthCenter, ink, new Vector3(1, .12f, .31f), 16);
    }

    /// <summary>Openness from shut to wide, gaze left or right, and brow tilt from anger to worry.</summary>
    public void Look(float open, float across = 0, float worry = 0) {
        for (int i = 0; i < eyes.Length; i++) {
            float side = i == 0 ? -1 : 1;
            eyes[i].Scale = new Vector3(1, Mathf.Clamp(open, .06f, 1.3f), 1);
            pupils[i].Position = new Vector3(across * .009f, -.001f, .0046f - Mathf.Abs(across) * .001f);
            brows[i].Position = new Vector3(side * .046f, .177f + .004f * (open - 1) + .004f * worry, .108f);
            brows[i].RotationDegrees = new Vector3(0, side * 17, -side * (6 + 16 * worry));
        }
        if (mouth is not null) {
            mouth.Position = MouthCenter;
            mouth.Scale = new Vector3(1 - .16f * worry, .11f + .18f * Mathf.Max(0, worry), .29f);
            mouth.RotationDegrees = Vector3.Back * (worry * 7);
        }
    }

    /// <summary>Opens the mouth past its resting line while keeping it against the face.</summary>
    public void OpenMouth(float opening) {
        if (mouth is null) return;
        mouth.Position = MouthCenter + Vector3.Back * (.008f * opening);
        mouth.Scale = new Vector3(.94f, .11f + .78f * opening, .29f);
    }
}
