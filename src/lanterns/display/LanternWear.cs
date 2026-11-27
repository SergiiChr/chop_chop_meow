using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Lanterns suspended from the player's fist, belt, backpack or shoulder straps.</summary>
internal sealed class LanternWear {
    private const int BeltSlots = 6, HotbarSlots = 9, SpareBeltHook = HotbarSlots;
    private const float WearScale = .8f, BeltRadius = .33f, BeltY = .105f, ShoulderX = .124f;
    private static readonly float[] BeltAngles = [43, -43, 89, -89, 136, -136];
    private static readonly Vector3 Grip = Vector3.Up * LanternShape.GripHeight;
    /// <summary>The lantern in the fist, built the first time the player holds one.</summary>
    internal LanternModel Hand => hand ??= HandModel();
    /// <summary>The lantern on each hook, built with its hook the first time a lantern hangs there, so a player who carries none costs nothing.</summary>
    internal readonly LanternModel?[] Worn = new LanternModel?[HotbarSlots + 1];
    private readonly Node3D?[] hooks = new Node3D?[HotbarSlots + 1];
    private readonly (Node3D Parent, Vector3 At, float Yaw)[] mounts = new (Node3D, Vector3, float)[HotbarSlots + 1];
    private readonly Node3D root, hips, torso;
    private LanternModel? hand;
    private bool handShown;
    private int hooksShown;

    internal LanternWear(Node3D root, Node3D hips, Node3D torso) {
        this.root = root;
        this.hips = hips;
        this.torso = torso;
        for (int i = 0; i < BeltSlots; i++) MountBelt(i, BeltAngles[i]);
        mounts[BeltSlots] = (torso, new Vector3(0, .465f, -.397f), 180);
        mounts[BeltSlots + 1] = (torso, new Vector3(BodySide.Left * ShoulderX, .526f, .271f), 0);
        mounts[BeltSlots + 2] = (torso, new Vector3(BodySide.Right * ShoulderX, .526f, .271f), 0);
        MountBelt(SpareBeltHook, 180);

        void MountBelt(int index, float yaw) {
            float angle = Mathf.DegToRad(yaw);
            mounts[index] = (hips, new Vector3(Mathf.Sin(angle) * BeltRadius, BeltY, Mathf.Cos(angle) * BeltRadius), yaw);
        }
    }

    private LanternModel HandModel() {
        var model = new LanternModel(root);
        Suspend(model, Vector3.Zero, Basis.Identity);
        model.Node.Hide();
        return model;
    }

    /// <summary>The lantern on a hook, building the hook and its lantern the first time.</summary>
    private LanternModel WornModel(int index) {
        if (Worn[index] is LanternModel built) return built;
        var (parent, at, yaw) = mounts[index];
        var hook = Hook();
        parent.AddChild(hook);
        hook.Position = at;
        hook.RotationDegrees = Vector3.Up * yaw;
        hook.Hide();
        hooks[index] = hook;
        var model = new LanternModel(hook, compact: true);
        Suspend(model, Vector3.Zero, Basis.Identity);
        return Worn[index] = model;
    }

    internal static Node3D Hook() {
        var root = new Node3D();
        Box(root, new Vector3(.028f, .037f, .191f), new Vector3(0, .018f, -.0925f), Wood("5c3d25", .9f));
        Box(root, new Vector3(.029f, .045f, .018f), Vector3.Up * .018f, Metal("647075", .5f, .7f));
        return root;
    }

    /// <summary>Turns the lantern about the grip so it stays in contact with the fist or hook.</summary>
    private static void Suspend(LanternModel lantern, Vector3 grip, Basis rotation) {
        Basis scaled = rotation.Scaled(Vector3.One * WearScale);
        lantern.Node.Transform = new Transform3D(scaled, grip - scaled * Grip);
    }

    /// <summary>The swing lags the stride and settles through the same transition as the player's gait.</summary>
    private static Basis Swing(ActorState stride) {
        Vector2 tilt = At(stride.Action, stride.Time);
        if (stride.Blend < 1)
            tilt = At(stride.From, stride.FromTime).Lerp(tilt, Mathf.SmoothStep(0, 1, stride.Blend));
        return Basis.FromEuler(new Vector3(tilt.X, 0, tilt.Y));

        static Vector2 At(ActorAction gait, float time) {
            float amplitude = gait switch { ActorAction.Walk => .085f, ActorAction.Jog => .125f, ActorAction.Run => .25f, _ => 0 };
            if (amplitude == 0) return Vector2.Zero;
            float step = ActorAnimation.Phase(gait, time) * Mathf.Tau - .8f;
            return new Vector2(Mathf.Cos(step), Mathf.Sin(step) * .3f) * amplitude;
        }
    }

    /// <summary>Shows each hotbar lantern once and keeps an occupied hand's lantern lit on the belt.</summary>
    internal void Sync(World world, PlayerState who, string? equipped, Vector3? fist, float energy, ActorState stride) {
        bool held = equipped != null && fist.HasValue;
        if (handShown != held) Hand.Node.Visible = handShown = held;
        Basis? swing = null;
        if (held) {
            Suspend(Hand, fist.GetValueOrDefault(), GaitSwing());
            Hand.Sync(Lanterns.BugCount(who.Backpack, equipped), energy);
        }
        int shown = 0;
        if (who.Health > 0 && who.Sheltered == 0) {
            int equippedHook = SpareBeltHook;
            foreach (var cell in who.Backpack.Layout) {
                if (cell.Hotbar is < 1 or > HotbarSlots || !Lanterns.IsItem(cell.Item)) continue;
                if (cell.Item == equipped) {
                    if (!held && cell.Hotbar <= BeltSlots) equippedHook = cell.Hotbar - 1;
                    continue;
                }
                Hang(cell.Hotbar - 1, cell.Item);
            }
            if (equipped != null && !held) Hang(equippedHook, equipped);
        }
        int changed = hooksShown ^ shown;
        if (changed != 0) {
            for (int i = 0; i < Worn.Length; i++)
                if ((changed & (1 << i)) != 0 && hooks[i] is Node3D hook) hook.Visible = (shown & (1 << i)) != 0;
            hooksShown = shown;
        }

        Basis GaitSwing() => swing ??= Swing(stride);

        void Hang(int index, string item) {
            shown |= 1 << index;
            var lantern = WornModel(index);
            var heading = hooks[index]!.Basis;
            var body = index < BeltSlots || index == SpareBeltHook ? hips.Basis : hips.Basis * torso.Basis;
            // The hook follows the body, but the lantern hangs down rather than leaning with the torso.
            Suspend(lantern, Vector3.Zero, (body * heading).Transposed() * GaitSwing() * heading);
            lantern.Sync(Lanterns.BugCount(who.Backpack, item), energy);
        }
    }
}
