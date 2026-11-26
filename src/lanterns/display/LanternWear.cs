using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Lanterns hanging from the player's fist or belt, built with the rig and reused as items move.</summary>
internal sealed class LanternWear {
    private const int BeltSlots = 10;
    private const float BeltScale = .52f, HandScale = .8f, BeltY = .105f;
    // One hook per hotbar slot, plus the last for an unbound hand lantern while its hand is busy.
    private static readonly float[] Angles = [36, -36, 72, -72, 108, -108, 144, -144, 180, 0];
    internal readonly LanternModel Hand;
    internal readonly LanternModel[] Belt = new LanternModel[BeltSlots];
    private readonly Node3D[] hooks = new Node3D[BeltSlots];
    private bool handShown;
    private int hooksShown;

    internal LanternWear(Node3D root, Node3D hips) {
        Hand = new LanternModel(root);
        Hand.Node.Scale = Vector3.One * HandScale;
        Hand.Node.Hide();
        for (int i = 0; i < Belt.Length; i++) {
            float angle = Mathf.DegToRad(Angles[i]);
            var hook = Hook();
            hips.AddChild(hook);
            hook.Position = new Vector3(Mathf.Sin(angle) * .25f, BeltY, Mathf.Cos(angle) * .25f);
            hook.RotationDegrees = new Vector3(0, Angles[i], 0);
            var lantern = new LanternModel(hook, compact: true);
            lantern.Node.Position = Vector3.Down * (LanternShape.GripHeight * BeltScale);
            lantern.Node.Scale = Vector3.One * BeltScale;
            hooks[i] = hook;
            hook.Hide();
            Belt[i] = lantern;
        }
    }

    internal static Node3D Hook() {
        var root = new Node3D();
        Box(root, new Vector3(.028f, .037f, .164f), new Vector3(0, .018f, -.079f), Wood("5c3d25", .9f));
        Box(root, new Vector3(.029f, .045f, .018f), Vector3.Up * .018f, Metal("647075", .5f, .7f));
        return root;
    }

    /// <summary>Each hotbar lantern hangs from its own hook, except for the one currently on the fist.</summary>
    internal void Sync(World world, PlayerState who, string? equipped, Vector3? fist, float energy) {
        bool hand = equipped != null && fist.HasValue;
        if (handShown != hand) Hand.Node.Visible = handShown = hand;
        if (hand) {
            // The lantern hangs below its grip instead of turning over with the forearm.
            Hand.Node.Position = fist.GetValueOrDefault() - Vector3.Up * (LanternShape.GripHeight * HandScale);
            Hand.Sync(world.Lanterns.BugCount(equipped), energy);
        }
        int shown = 0;
        if (who.Health > 0 && who.Sheltered == 0) {
            int equippedHook = BeltSlots - 1;
            foreach (var cell in who.Backpack.Layout) {
                if (cell.Hotbar is < 1 or >= BeltSlots || !Lanterns.IsItem(cell.Item)) continue;
                if (cell.Item == equipped) {
                    if (!hand) equippedHook = cell.Hotbar - 1;
                    continue;
                }
                Hang(cell.Hotbar - 1, cell.Item);
            }
            if (equipped != null && !hand) Hang(equippedHook, equipped);
        }
        int changed = hooksShown ^ shown;
        if (changed != 0) {
            for (int i = 0; i < Belt.Length; i++)
                if ((changed & (1 << i)) != 0) hooks[i].Visible = (shown & (1 << i)) != 0;
            hooksShown = shown;
        }

        void Hang(int hook, string item) {
            shown |= 1 << hook;
            Belt[hook].Sync(world.Lanterns.BugCount(item), energy);
        }
    }
}
