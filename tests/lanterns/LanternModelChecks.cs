using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;

internal static class LanternModelChecks {
    public static void Run(GameData data) {
        Test(CheckLanternLightComesFromInsideTheJar);
        Test(CheckLanternBrightnessRisesWithLiveFirebugs, data);
        Test(CheckHotbarAndHandLanternsKeepTheirOwnLight, data);
        Test(CheckLanternsSwingFartherWhenRunningWithoutLosingTheirGrip, data);
        Test(CheckAllHotbarLanternsHaveRoomForTheHandLantern, data);
    }

    private static void CheckLanternLightComesFromInsideTheJar() {
        var prop = (LanternProp)WorldModels.Build(new Thing { Kind = ThingKind.Lantern });
        try {
            var parts = ModelSteps.AllMeshes(prop.Model.Node);
            var foot = parts.Single(part => part.Mesh is BoxMesh && Mathf.IsEqualApprox(part.Position.Y, .024f));
            var glass = parts.Single(part => part.Mesh == GlassJarShape.Pile);
            var light = prop.Model.Light;
            var jar = glass.Transform * glass.GetAabb();
            Require(jar.HasPoint(light.Position), "the light should sit inside the glass jar");
            Require((light.ShadowCasterMask & foot.Layers) != 0 && foot.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off,
                "the wood foot should block the lantern's light from shining straight down onto the ground");
            prop.Model.Sync(10, 1.6f);
            Require(glass.MaterialOverride is StandardMaterial3D { EmissionEnabled: true, EmissionEnergyMultiplier: > 0 },
                "a full lantern should light the glass from inside");
            float fullGlow = ((StandardMaterial3D)glass.MaterialOverride).EmissionEnergyMultiplier;
            prop.Model.Sync(4, 1.6f);
            Require(glass.MaterialOverride is StandardMaterial3D { EmissionEnabled: true } partial
                && Mathf.IsEqualApprox(partial.EmissionEnergyMultiplier, .4f * fullGlow),
                "four live firebugs should give two fifths of the full glass glow");
            prop.Model.Sync(0, 1.6f);
            Require(glass.MaterialOverride is StandardMaterial3D { EmissionEnabled: false }, "an empty jar should have no interior glow");
        } finally { prop.Root.Free(); }
    }

    private static void CheckLanternBrightnessRisesWithLiveFirebugs(GameData data) {
        var prop = (LanternProp)WorldModels.Build(new Thing { Kind = ThingKind.Lantern });
        var icon = WorldModels.IconModel(Lanterns.Item, false, data)!;
        var (torch, _, light) = ActorRig.Torch();
        try {
            Require(ModelSteps.AllMeshes(icon).Any(part => part.Mesh == GlassJarShape.Pile),
                "the lantern icon should use the existing glass jar");
            Require(ModelSteps.Triangles(icon) < 1000 && ModelSteps.Triangles(prop.Root) < 1000,
                "the icon and placed lantern should stay under the item triangle ceiling");
            prop.Model.Sync(10, 1.6f);
            var green = prop.Model.Light;
            Require(green.Visible && green.OmniRange == light.OmniRange && green.OmniAttenuation == light.OmniAttenuation
                && Mathf.IsEqualApprox(green.LightEnergy, 1.92f) && green.LightColor.G > green.LightColor.R && green.LightColor.G > green.LightColor.B,
                "a full lantern should shine in green over the same area as a torch, 20% brighter");
            Require(prop.Model.BugsShown == 10, "a full lantern should show ten firebugs inside");
            prop.Model.Sync(4, 1.6f);
            Require(prop.Model.BugsShown == 4 && Mathf.IsEqualApprox(green.LightEnergy, .768f),
                "a lantern with four live firebugs shows and lights with only those four");
            prop.Model.Sync(0, 1.6f);
            Require(!green.Visible && prop.Model.Node.Visible && prop.Model.BugsShown == 0,
                "the light and firebugs should go out without hiding the lantern");
        } finally { prop.Root.Free(); icon.Free(); torch.Free(); }
    }

    private static void CheckHotbarAndHandLanternsKeepTheirOwnLight(GameData data) {
        var world = new World(data, false);
        string first = GiveLantern(world, 10),
            second = GiveLantern(world, 4);
        world.SetHotbar(0, first); world.SetHotbar(1, second); world.Tick(0);
        var rig = new ActorRig(ActorSkin.Player);
        var wear = rig.Lanterns!;
        var stride = new ActorState();
        try {
            wear.Sync(world, world.Me, null, null, 1, stride);
            Require(wear.Worn.Take(2).All(lantern => ((Node3D)lantern!.Node.GetParent()).Visible && lantern.Light.Visible),
                "every equipped hotbar lantern should cast its own light from the belt");
            Require(wear.Worn[0]!.Node.Scale.IsEqualApprox(wear.Hand.Node.Scale),
                "the same lantern should be the same size in hand and on the belt");
            Require(wear.Worn[0]!.BugsShown == 10 && wear.Worn[1]!.BugsShown == 4
                && Mathf.IsEqualApprox(wear.Worn[0]!.Light.LightEnergy, 1.2f) && Mathf.IsEqualApprox(wear.Worn[1]!.Light.LightEnergy, .48f),
                "each belt lantern should only shine with its own firebugs");
            world.Lanterns.ToggleInHand(Lanterns.Id(first));
            rig.Show(new Pose(RaiseL: 30, OutL: 4, ElbowL: 60), null, false);
            Vector3 fist = rig.Fist(left: true);
            wear.Sync(world, world.Me, Lanterns.InHand(world.Me), fist, 1, stride);
            Require(wear.Hand.Node.Visible && wear.Hand.Light.Visible && (wear.Hand.Node.Transform * (Vector3.Up * LanternShape.GripHeight)).IsEqualApprox(fist),
                "the lantern in the left hand should light and hang directly from the fist");
            Require(!((Node3D)wear.Worn[0]!.Node.GetParent()).Visible && ((Node3D)wear.Worn[1]!.Node.GetParent()).Visible,
                "the lantern in hand should no longer hang from its hotbar hook but the other should");
            wear.Sync(world, world.Me, Lanterns.InHand(world.Me), null, 1, stride);
            Require(!wear.Hand.Node.Visible && ((Node3D)wear.Worn[0]!.Node.GetParent()).Visible && wear.Worn[0]!.Light.Visible,
                "when the hand is busy the lantern should keep its light at the belt");
        } finally { rig.Root.Free(); }
    }

    private static void CheckLanternsSwingFartherWhenRunningWithoutLosingTheirGrip(GameData data) {
        var world = new World(data, false);
        string belt = GiveLantern(world, 4);
        string hand = GiveLantern(world, 4);
        world.SetHotbar(0, belt); world.Tick(0);
        world.Lanterns.ToggleInHand(Lanterns.Id(hand));
        var rig = new ActorRig(ActorSkin.Player);
        var wear = rig.Lanterns!;
        var stride = new ActorState { Action = ActorAction.Walk };
        Vector3 grip = Vector3.Up * LanternShape.GripHeight, fist = rig.Fist(left: true);
        try {
            wear.Sync(world, world.Me, hand, fist, 1, stride);
            float walking = wear.Hand.Node.Basis.Y.AngleTo(Vector3.Up);
            Require(walking > .02f && wear.Worn[0]!.Node.Basis.Y.AngleTo(Vector3.Up) > .02f,
                "both lanterns should swing away from vertical in a walking stride");
            stride.Action = ActorAction.Run;
            wear.Sync(world, world.Me, hand, fist, 1, stride);
            float running = wear.Hand.Node.Basis.Y.AngleTo(Vector3.Up);
            Require(running > walking + .05f && wear.Worn[0]!.Node.Basis.Y.AngleTo(Vector3.Up) > walking + .05f,
                "both lanterns should swing farther when running");
            Require((wear.Hand.Node.Transform * grip).IsEqualApprox(fist)
                && (wear.Worn[0]!.Node.Transform * grip).IsZeroApprox(),
                "the lanterns should turn about their grips without sliding off the fist or hook");
            stride.Action = ActorAction.Idle; stride.From = ActorAction.Run; stride.Blend = .5f;
            wear.Sync(world, world.Me, hand, fist, 1, stride);
            float settling = wear.Hand.Node.Basis.Y.AngleTo(Vector3.Up);
            Require(settling > .02f && settling < running, "the swing should ease down as the player stops");
        } finally { rig.Root.Free(); }
    }

    private static void CheckAllHotbarLanternsHaveRoomForTheHandLantern(GameData data) {
        var world = new World(data, false);
        for (int slot = 0; slot < 9; slot++) {
            string item = GiveLantern(world, 10);
            world.SetHotbar(slot, item);
        }
        world.Tick(0);
        string extra = GiveLantern(world, 10);
        world.Lanterns.ToggleInHand(Lanterns.Id(extra));
        var rig = new ActorRig(ActorSkin.Player);
        var wear = rig.Lanterns!;
        var stride = new ActorState();
        try {
            rig.Show(new Pose(), null, false);
            wear.Sync(world, world.Me, Lanterns.InHand(world.Me), null, 1, stride);
            Require(wear.Worn.Count(lantern => ((Node3D)lantern!.Node.GetParent()).Visible && lantern.Light.Visible) == 10,
                "using that hand should hang its lantern next to the nine hotbar lanterns without turning any off");
            var hooks = wear.Worn.Select(lantern => (Node3D)lantern!.Node.GetParent()).ToArray();
            var belt = hooks[0].GetParent();
            Require(hooks[..6].All(hook => hook.GetParent() == belt) && hooks[9].GetParent() == belt,
                "slots one through six and the temporary hook belong on the belt");
            Require(hooks[6].GetParent() != belt && hooks[6].Position.Z < 0
                && hooks[7].GetParent() == hooks[6].GetParent() && hooks[7].Position.X > 0 && hooks[7].Position.Z > 0
                && hooks[8].GetParent() == hooks[6].GetParent() && hooks[8].Position.X < 0 && hooks[8].Position.Z > 0,
                "slot seven hangs behind the player and slots eight and nine hang by opposite shoulders");
            int triangles = LanternModelSteps.VisibleTriangles(rig.Root);
            Require(triangles <= 5000, $"with all lanterns lit the player should stay within 5000 visible triangles, got {triangles}");
            var shoulder = world.HotbarItem(7);
            world.Lanterns.ToggleInHand(Lanterns.Id(shoulder));
            wear.Sync(world, world.Me, shoulder, null, 1, stride);
            Require(!hooks[7].Visible && hooks[9].Visible && wear.Worn[9]!.Light.Visible,
                "a lantern from a shoulder should use the temporary belt hook while the hand is busy");
        } finally { rig.Root.Free(); }
    }

}
