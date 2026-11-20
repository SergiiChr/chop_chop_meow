using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A felled tree going over as a stiff pole hinged at the foot of its trunk, on the side it falls to, so no part of the
/// trunk goes into the ground: slow at first, then faster, a taller tree taking longer and landing faster
/// (θ'' = 3g/2L · sin θ, the falling chimney: https://demoweb.physics.ucla.edu/content/40-falling-chimney ,
/// https://arxiv.org/abs/physics/0210033).
/// It comes to rest where its trunk or a branch first touches the ground; the leaf cards bear no weight, and the foliage
/// shader lays those that reach the ground flat on it (<see cref="Tree3DShape.Foliage"/>).
/// Angles are from upright, in radians, and points are in the tree's model sized as drawn, from its spot.
/// </summary>
public sealed class TreeFall {
    public const float Gravity = 9.81f;
    /// <summary>
    /// The lean the fall is timed from: the tree starts upright at the speed it would have from falling from rest at this
    /// lean, which takes it down in the time a pole released there takes.
    /// </summary>
    public static readonly float StartLean = Mathf.DegToRad(5);
    /// <summary>The longest step the fall is worked out in, so it lands the same at every frame rate.</summary>
    private const float Step = 1 / 240f;

    /// <summary>Where the tree turns about: the foot of its trunk on the side it falls to.</summary>
    public readonly Vector3 Hinge;
    /// <summary>The way it falls across the ground, and the level axis it turns about.</summary>
    public readonly Vector3 Toward, Axis;
    /// <summary>Metres from the hinge to the top of the tree.</summary>
    public readonly float Length;
    /// <summary>The angle at which its lowest part touches the ground, where it lands.</summary>
    public readonly float Lands;
    public float Angle { get; private set; }
    /// <summary>How fast it turns, in radians a second.</summary>
    public float Speed { get; private set; }
    public bool Landed => Angle >= Lands;

    /// <param name="wood">The corners of the tree's trunk and branches in its model, unsized (<see cref="Tree3DShape.Outline"/>).</param>
    /// <param name="top">The height of the top of its model.</param>
    /// <param name="foot">The middle of the trunk's foot in the model and its radius at the ground (<see cref="Tree3DShape.Foot"/>).</param>
    /// <param name="scale">How much bigger than its model the tree is drawn.</param>
    /// <param name="toward">The way it falls across the model's ground, a level unit vector.</param>
    public TreeFall(Vector3[] wood, float top, (Vector2 Center, float Radius) foot, float scale, Vector3 toward) {
        Toward = toward;
        Axis = Vector3.Up.Cross(Toward);
        Hinge = new Vector3(foot.Center.X, 0, foot.Center.Y) * scale + Toward * foot.Radius * scale;
        float lands = MathF.PI / 2;
        foreach (var corner in wood) {
            var at = corner * scale - Hinge;
            float ahead = at.Dot(Toward);
            if (ahead > 0 && at.Y > 0) lands = Math.Min(lands, MathF.Atan2(at.Y, ahead));
        }
        (Length, Lands) = (top * scale, lands);
        Speed = MathF.Sqrt(3 * Gravity / Length * (1 - MathF.Cos(StartLean)));
    }

    /// <summary>The tree turned by the fall, about its hinge, before anything else turns it: a point of the model as drawn goes to <c>Turn * point</c>.</summary>
    public Transform3D Turn => new Transform3D(new Basis(Axis, Angle), Vector3.Zero).TranslatedLocal(-Hinge).Translated(Hinge);

    /// <summary>Moves the fall on by <paramref name="dt"/> seconds; true on the step that lands it.</summary>
    public bool Advance(float dt) {
        if (Landed) return false;
        for (float left = dt; left > 0; left -= Step) {
            float h = Math.Min(Step, left);
            Speed += 1.5f * Gravity / Length * MathF.Sin(Angle) * h;
            Angle += Speed * h;
            if (Angle < Lands) continue;
            (Angle, Speed) = (Lands, 0);
            return true;
        }
        return false;
    }
}
