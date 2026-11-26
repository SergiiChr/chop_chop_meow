using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Lanterns suspended from the player's fist, belt, backpack or shoulder straps.</summary>
internal sealed class LanternWear {
    private const int BeltSlots = 6, HotbarSlots = 9, SpareBeltHook = HotbarSlots;
    private const float WearScale = .8f, BeltRadius = .33f, BeltY = .105f, ShoulderX = .124f;
    private static readonly float[] BeltAngles = [43, -43, 89, -89, 136, -136];
    private static readonly Vector3 Grip = Vector3.Up * LanternShape.GripHeight;
    internal readonly LanternModel Hand;
    internal readonly LanternModel[] Worn = new LanternModel[HotbarSlots + 1];
    private readonly Node3D[] hooks = new Node3D[HotbarSlots + 1];
    private readonly Node3D hips, torso;
    private bool handShown;
    private int hooksShown;

    internal LanternWear(Node3D root, Node3D hips, Node3D torso) {
        this.hips = hips;
        this.torso = torso;
        Hand = new LanternModel(root);
        Suspend(Hand, Vector3.Zero, Basis.Identity);
        Hand.Node.Hide();
        for (int i = 0; i < BeltSlots; i++) MountBelt(i, BeltAngles[i]);
        Mount(BeltSlots, torso, new Vector3(0, .465f, -.397f), 180);
        Mount(BeltSlots + 1, torso, new Vector3(BodySide.Left * ShoulderX, .526f, .271f), 0);
        Mount(BeltSlots + 2, torso, new Vector3(BodySide.Right * ShoulderX, .526f, .271f), 0);
        MountBelt(SpareBeltHook, 180);

        void MountBelt(int index, float yaw) {
            float angle = Mathf.DegToRad(yaw);
            Mount(index, hips, new Vector3(Mathf.Sin(angle) * BeltRadius, BeltY, Mathf.Cos(angle) * BeltRadius), yaw);
        }

        void Mount(int index, Node3D parent, Vector3 at, float yaw) {
            var hook = Hook();
            parent.AddChild(hook);
            hook.Position = at;
            hook.RotationDegrees = Vector3.Up * yaw;
            hooks[index] = hook;
            Worn[index] = new LanternModel(hook, compact: true);
            Suspend(Worn[index], Vector3.Zero, Basis.Identity);
            hook.Hide();
        }
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
        bool hand = equipped != null && fist.HasValue;
        if (handShown != hand) Hand.Node.Visible = handShown = hand;
        Basis? swing = null;
        if (hand) {
            Suspend(Hand, fist.GetValueOrDefault(), GaitSwing());
            Hand.Sync(world.Lanterns.BugCount(equipped), energy);
        }
        int shown = 0;
        if (who.Health > 0 && who.Sheltered == 0) {
            int equippedHook = SpareBeltHook;
            foreach (var cell in who.Backpack.Layout) {
                if (cell.Hotbar is < 1 or > HotbarSlots || !Lanterns.IsItem(cell.Item)) continue;
                if (cell.Item == equipped) {
                    if (!hand && cell.Hotbar <= BeltSlots) equippedHook = cell.Hotbar - 1;
                    continue;
                }
                Hang(cell.Hotbar - 1, cell.Item);
            }
            if (equipped != null && !hand) Hang(equippedHook, equipped);
        }
        int changed = hooksShown ^ shown;
        if (changed != 0) {
            for (int i = 0; i < Worn.Length; i++)
                if ((changed & (1 << i)) != 0) hooks[i].Visible = (shown & (1 << i)) != 0;
            hooksShown = shown;
        }

        Basis GaitSwing() => swing ??= Swing(stride);

        void Hang(int index, string item) {
            shown |= 1 << index;
            var lantern = Worn[index];
            var heading = hooks[index].Basis;
            var body = index < BeltSlots || index == SpareBeltHook ? hips.Basis : hips.Basis * torso.Basis;
            // The hook follows the body, but the lantern hangs down rather than leaning with the torso.
            Suspend(lantern, Vector3.Zero, (body * heading).Transposed() * GaitSwing() * heading);
            lantern.Sync(world.Lanterns.BugCount(item), energy);
        }
    }
}
