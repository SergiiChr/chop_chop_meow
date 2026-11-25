using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>The forge's blowpipe and glass, positioned against the hands and mouth of whoever works it.</summary>
internal sealed class GlassBlowingView {
    private const float PipeLength = SteelPipeShape.LooseLength, NearGrip = .18f, FarGrip = .40f;
    private const float LiftSeconds = .32f, DetachAt = .075f, Gravity = 15f, StartSpeed = 1.45f;
    private const float StretchDistance = StartSpeed * DetachAt / 2;
    private static readonly Vector3 Blowing = new Vector3(.48f, 0, .877f).Normalized();
    private static readonly Vector3 Showing = new Vector3(.988f, .03f, .15f).Normalized();
    private static readonly Vector3 Low = new(-.11f, 1.02f, .13f), Ready = new(-.57f, 1.14f, .25f);
    private static readonly Vector3 Lip = new(0, .084f, .111f);
    private static readonly Vector3 ElbowL = new(.55f, -1, 0), ElbowR = new(-.55f, -1, 0);
    private static readonly Color Hot = new("ff941e"), Cold = new("66301e");
    private static readonly SphereMesh Glass = new() { Radius = 1, Height = 2, Rings = 5, RadialSegments = 12 };
    private static readonly SphereMesh Bead = new() { Radius = 1, Height = 2, Rings = 2, RadialSegments = 6 };

    private readonly Node3D parent;
    private readonly MeshInstance3D tube, gather, neck, puddle, jar;
    private readonly MeshInstance3D[] splash = new MeshInstance3D[4];
    private readonly StandardMaterial3D cooling;
    private readonly LocalClock progress = new(.30f), overheat = new(.125f), ending = new(.125f);
    private GlassResult lastResult;
    private int lastBlower;
    private Vector3 fallingFrom;
    private float fallingRadius, lastRadius, lastDroop, fallSeconds;

    public GlassBlowingView(Node3D parent) {
        this.parent = parent;
        var molten = Mat("ffae34", .28f, glow: .85f);
        cooling = (StandardMaterial3D)Mat("ff941e", .34f, glow: .9f).Duplicate();
        tube = Add(parent, SteelPipeShape.Loose, SteelPipeShape.Finish, Vector3.Zero);
        gather = Add(parent, Glass, molten, Vector3.Zero);
        neck = Add(parent, Bead, molten, Vector3.Zero);
        puddle = Add(parent, Glass, cooling, Vector3.Zero);
        jar = Add(parent, GlassJarShape.Mesh, GlassJarShape.Finish, Vector3.Zero);
        for (int i = 0; i < splash.Length; i++) splash[i] = Add(parent, Bead, cooling, Vector3.Zero);
        Hide();
    }

    public void Warm() {
        tube.Show(); gather.Show(); neck.Show(); puddle.Show(); jar.Show();
        foreach (var drop in splash) drop.Show();
    }

    public void Hide() {
        if (lastBlower == 0 && lastResult == GlassResult.None && !tube.Visible) return;
        tube.Hide(); gather.Hide(); neck.Hide(); puddle.Hide(); jar.Hide();
        foreach (var drop in splash) drop.Hide();
        lastBlower = 0;
        lastResult = GlassResult.None;
    }

    public void Show(Thing forge, ActorRig rig, ActorState actor, float time, float dt) {
        if (!rig.Root.Visible || actor.Action != ActorAction.Glassblow) { Unattended(forge, time, dt); return; }
        var result = forge.GlassResult;
        float elapsed = GlassBlowing.ResultSeconds - forge.GlassResultLeft;
        bool reset = forge.GlassBlower != lastBlower || result != lastResult;
        float since = Math.Clamp(Read(ending, elapsed), 0, GlassBlowing.ResultSeconds);
        float batch = Math.Clamp(Read(progress, forge.Progress) / GlassBlowing.Seconds, 0, 1);
        float droop = Mathf.SmoothStep(0, GlassBlowing.OverheatSeconds, Read(overheat, forge.GlassOverheat));
        var pose = actor.Pose;
        float lift = Mathf.SmoothStep(.06f, LiftSeconds, actor.Time);
        if (result != GlassResult.None) lift = 1;
        float showJar = result == GlassResult.Success ? Mathf.SmoothStep(0, .42f, since) : 0;
        float lower = result == GlassResult.Melted ? Mathf.SmoothStep(DetachAt, .44f, since) : 0;
        pose = pose with { Head = Mathf.Lerp(7, pose.Head, lift) + lower * 29 };
        var rotation = Basis.FromEuler(new Vector3(Mathf.DegToRad(pose.Head - pose.Lean * .4f),
            Mathf.DegToRad(-pose.Twist * .5f), Mathf.DegToRad(-pose.Sway * .6f)));
        var mouth = ArmReach.FromTrunk(pose, HumanShape.Standard.HeadOffset + rotation * Lip);
        var tail = Low.Lerp(mouth, lift).Lerp(Ready, showJar).Lerp(Low, lower);
        var along = Blowing.Lerp(Showing, showJar).Lerp(Blowing.Rotated(Vector3.Right, .17f), lower).Normalized();
        var tip = tail + along * PipeLength;
        var left = tail + along * FarGrip;
        float present = Mathf.SmoothStep(.18f, .42f, since);
        if (result == GlassResult.Success) left = left.Lerp(tip + new Vector3(.05f, -.029f, 0), present);
        float weight = Mathf.SmoothStep(0, .12f, actor.Time);
        var (rightPose, rightRoll) = ArmReach.Reaching(pose, tail + along * NearGrip, Transform3D.Identity,
            weight: weight, elbowToward: ElbowR);
        var (held, leftRoll) = ArmReach.Reaching(rightPose, left, Transform3D.Identity, left: true,
            weight: weight, elbowToward: ElbowL);
        rig.Show(held with { ArmRollL = leftRoll, ArmRollR = rightRoll }, null, false);
        if (showJar > 0) rig.Head.RotateY(.34f * showJar);

        // Draw the shaft through the posed fists, including while the hands are moving up to their marks.
        var rightHand = rig.Fist(false);
        if (result != GlassResult.Success || present == 0) along = (rig.Fist(true) - rightHand).Normalized();
        tail = rightHand - along * NearGrip;
        tip = tail + along * PipeLength;
        var toForge = parent.GlobalTransform.AffineInverse() * rig.Root.GlobalTransform;
        var up = Vector3.Up;
        var side = along.Cross(up).Normalized();
        var pipeBasis = new Basis(along, side.Cross(along), side);
        var pipeScale = new Vector3(1, .32f, .32f);
        // The shared tube mesh lies along X with its axis above its base.
        var centre = (tail + tip) * .5f - pipeBasis.Y * (SteelPipeShape.CenterHeight * pipeScale.Y);
        tube.Transform = toForge * new Transform3D(pipeBasis.ScaledLocal(pipeScale), centre);
        tube.Show();
        Vector3? lastGather = lastDroop > .7f && gather.Visible && lastBlower == forge.GlassBlower
            ? gather.Position : null;
        ClearGlass();
        if (result == GlassResult.Melted) {
            float radius = lastBlower == forge.GlassBlower ? lastRadius : .115f;
            if (reset) StartFall(lastGather ?? toForge * DroopedAt(mouth, radius), radius);
            Melt(since);
        } else if (result == GlassResult.Success) {
            float shape = Mathf.SmoothStep(.08f, .34f, since);
            if (shape < 1) Bulb(toForge, tip, along, Math.Max(lastRadius, .13f) * (1 - shape), 0);
            if (shape > 0) {
                var bottom = (tip + new Vector3(.052f, -.055f, 0)).Lerp(rig.Fist(true) + up * .035f, present);
                jar.Position = toForge * bottom;
                jar.Scale = Vector3.One * shape;
                jar.Show();
            }
        } else {
            lastRadius = Mathf.Lerp(.071f, .148f, batch) * (1 + .014f * (pose.Lean - 3));
            Bulb(toForge, tip, along, lastRadius * Mathf.SmoothStep(0, .14f, actor.Time), droop);
        }
        lastDroop = droop;
        lastBlower = forge.GlassBlower;
        lastResult = result;
        return;

        float Read(LocalClock clock, float value) => reset ? clock.Set(value, time) : clock.Follow(value, time, dt);
    }

    /// <summary>The glass already in flight can land after the blower steps away.</summary>
    public void Unattended(Thing forge, float time, float dt) {
        if (forge.GlassResult != GlassResult.Melted) { Hide(); return; }
        float elapsed = GlassBlowing.ResultSeconds - forge.GlassResultLeft;
        if (lastResult != GlassResult.Melted) {
            var at = lastBlower != 0 && gather.Visible ? gather.Position : new Vector3(-.46f, 1.22f, .78f);
            StartFall(at, lastBlower != 0 ? lastRadius : .115f);
            ending.Set(elapsed, time);
        }
        tube.Hide();
        ClearGlass();
        Melt(Math.Clamp(ending.Follow(elapsed, time, dt), 0, GlassBlowing.ResultSeconds));
        lastBlower = 0;
        lastResult = GlassResult.Melted;
    }

    private void ClearGlass() {
        gather.Hide(); neck.Hide(); puddle.Hide(); jar.Hide();
        foreach (var drop in splash) drop.Hide();
    }

    private void Bulb(Transform3D toForge, Vector3 tip, Vector3 along, float radius, float droop) {
        var at = tip + along * (radius * (.73f - droop * .40f)) + Vector3.Down * (.19f * droop);
        gather.Position = toForge * at;
        float width = radius * (1 - .22f * droop);
        gather.Scale = new Vector3(width, radius * (1 + .14f * droop), width);
        gather.Visible = radius > .004f;
        if (droop <= 0) return;
        Stretch(toForge * tip, gather.Position, radius * .32f * droop);
    }

    private void Stretch(Vector3 top, Vector3 bottom, float width) {
        var axis = top - bottom;
        var y = axis.Normalized();
        var x = y.Cross(Vector3.Back).Normalized();
        neck.Transform = new Transform3D(new Basis(x, y, x.Cross(y)).ScaledLocal(
            new Vector3(width, axis.Length() * .55f, width)), (top + bottom) * .5f);
        neck.Visible = width > .002f;
    }

    private static Vector3 DroopedAt(Vector3 mouth, float radius) =>
        mouth + Blowing * (PipeLength + radius * .33f) + Vector3.Down * .19f;

    private void StartFall(Vector3 at, float radius) {
        fallingRadius = Math.Max(radius * .78f, .055f);
        fallingFrom = at + Vector3.Down * StretchDistance;
        float height = Math.Max(0, fallingFrom.Y - fallingRadius);
        fallSeconds = (MathF.Sqrt(StartSpeed * StartSpeed + 2 * Gravity * height) - StartSpeed) / Gravity;
    }

    private void Melt(float since) {
        float flying = Math.Max(0, since - DetachAt);
        if (flying < fallSeconds) {
            float stretch = Math.Min(1, since / DetachAt);
            float y = StretchDistance * (1 - stretch * stretch) - StartSpeed * flying - Gravity * flying * flying / 2;
            gather.Position = fallingFrom + Vector3.Up * y;
            gather.Scale = Vector3.One * fallingRadius;
            gather.Show();
            if (since < DetachAt && tube.Visible) {
                var rim = tube.Transform * new Vector3(PipeLength * .5f, SteelPipeShape.CenterHeight, 0);
                Stretch(rim, gather.Position, fallingRadius * .41f * (1 - since / DetachAt));
            }
            return;
        }
        float landed = flying - fallSeconds;
        float spread = Mathf.Lerp(.65f, 1.65f, Mathf.SmoothStep(0, .17f, landed));
        float height = Mathf.Lerp(.053f, .013f, Mathf.SmoothStep(0, .16f, landed));
        float cool = Mathf.SmoothStep(.03f, .48f, landed);
        cooling.AlbedoColor = Hot.Lerp(Cold, cool);
        cooling.EmissionEnergyMultiplier = .9f * (1 - cool);
        puddle.Position = new Vector3(fallingFrom.X, height, fallingFrom.Z);
        puddle.Scale = new Vector3(fallingRadius * spread * 1.13f, height, fallingRadius * spread * .87f);
        puddle.Show();
        for (int i = 0; i < splash.Length; i++) {
            float y = (1.8f + .16f * i) * landed - Gravity * landed * landed / 2;
            if (y <= .014f) continue;
            var away = Vector3.Right.Rotated(Vector3.Up, i * 1.78f);
            var drop = splash[i];
            drop.Position = new Vector3(fallingFrom.X, y, fallingFrom.Z) + away * (landed * (.7f + .17f * i));
            drop.Scale = Vector3.One * fallingRadius * .22f;
            drop.Show();
        }
    }
}
