using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private Task RunBuildingRemovalUIChecks() => TestClean(CheckRemoveSelectorTakesBuildingsDownAtOnce);
    private Task RunHeldPlanUIChecks() => TestClean(CheckHeldPlanPlacesOnOpenGround);
    private Task<Thing> RunBuildingPlacementUIChecks() => Test(CheckBuildPlanPlacesChest);

    /// <summary>The red cross in the building plans hands over the remove selector, which turns a building in reach red and takes it down on one click, then stays for the next; a building's page has no take-down button.</summary>
    private async Task CheckRemoveSelectorTakesBuildingsDownAtOnce() {
        var chest = Hosted.Add(ThingKind.Chest, world.Player + new Vector2(2, -2)); chest.Inventory.Add("coal", 5);
        var other = Hosted.Add(ThingKind.Chest, world.Player + new Vector2(-2, 2));
        int wood = world.Backpack["wood"], coal = world.Backpack["coal"];
        await ClickOn(chest);
        Require(panel.Selection.Thing == chest.Id && Named("dismantle") == null, "A building's page must have no take-down button.");
        await CloseWithEsc();
        await KeyStroke(Key.B);
        await Click(Named("remove")!);
        Require(player.Mode is ClickMode.Remove && !panel.Visible, "A click on the red cross must close the building plans and hand over the remove selector.");
        await Hover(view.Screen(chest.Pos, .3f));
        Require(view.RemovalShown.Building == chest.Id, "A building in reach under the remove selector must turn red.");
        await ClickOn(chest);
        await Until(() => world.Find(chest.Id) == null, "one click with the remove selector taking the chest down", 2);
        Require(world.Backpack["wood"] == wood + 8 && world.Backpack["coal"] == coal + 5, "Taking the chest down must give back its 8 wood and the coal in it.");
        Require(player.Mode is ClickMode.Remove && !panel.Visible, "The remove selector must stay on after taking a building down, and open no page.");
        await RightClick(world.Player + new Vector2(3, 0));
        await ClickOn(other);
        Require(player.Mode is ClickMode.Remove && !panel.Visible && world.Find(other.Id) != null && view.RemovalShown == Removal.None,
            "Switching underground must keep the selector active and leave the surface chest untouched.");
        await RightClick(world.Player + new Vector2(3, 0));
        await ClickOn(other);
        await Until(() => world.Find(other.Id) == null, "switching back to the surface and taking the next chest down", 2);
        await KeyStroke(Key.Escape);
        Require(player.Mode is ClickMode.Normal, "Esc must put the remove selector away.");
        await KeyStroke(Key.B);
        await PickUp(Named("build_chest")!);
        await Click(Named("remove")!);
        Require(player.Mode is ClickMode.Remove && cursor.Empty && !panel.Visible, "A click on the red cross with a plan held must let go of the plan and hand over the remove selector.");
        await KeyStroke(Key.Escape);
        Drop(Hosted, "wood", 16); Drop(Hosted, "coal", 5); await Frames();
    }

    /// <summary>A held plan keeps its turn over the panel and uses it when placed on open ground.</summary>
    private async Task CheckHeldPlanPlacesOnOpenGround() {
        await CloseWithEsc();
        Give(Hosted, world.Data.Buildings[ThingKind.Chest].Cost);
        await KeyStroke(Key.B);
        await PickUp(Named("build_chest")!);
        Require(cursor.Plan == "chest", "A click on a plan must pick it up.");
        int chests = world.Things.Count(t => t.Kind == ThingKind.Chest);
        var spot = SpotsNearby()
            .First(p => world.Construction.PlacementBlock(ThingKind.Chest, world.Construction.Snap(ThingKind.Chest, p)) == null && Uncovered(view.Screen(p)));
        await Hover(view.Screen(spot)); await KeyStroke(Key.V);
        await Hover(Named("build_chest")!);
        await KeyStroke(Key.V); await KeyStroke(Key.C);
        Require(cursor.Plan == "chest" && panel.Page == Page.Build, "C and V should turn a held plan even while it is over the building plans.");
        await Hover(view.Screen(spot));
        Require(view.PlacementGhost is { Visible: true } ghost && ghost.Basis.Z.X < -.999f, "The preview should face toward the turned chest's front.");
        await Click(view.Screen(spot));
        await Until(() => world.Things.Count(t => t.Kind == ThingKind.Chest) == chests + 1, "a click on open ground placing the held plan", 2);
        var chest = world.Things.Single(t => t.Kind == ThingKind.Chest && t.Pos == spot);
        Require(chest.Rotation == Mathf.Pi / 2, "The held plan should keep its quarter turn across the panel.");
        Require(view.PropOf(chest.Id)?.Root.Basis.Z.X < -.999f, "The placed chest should face the way its preview did.");
        RemoveThings(Hosted, t => t.Kind == ThingKind.Chest && t.Pos == world.Construction.Snap(ThingKind.Chest, spot)); await CloseWithEsc();
    }

    /// <summary>The build page shows plan details and a ground click places a chest. Returns the chest.</summary>
    private async Task<Thing> CheckBuildPlanPlacesChest() {
        await KeyStroke(Key.B); Require(ShownPage() == "Building plans", "B must open the building plans.");
        var plan = Named("build_chest"); Require(plan != null, "Building plans must expose storage construction.");
        await Hover(plan!);
        Require(HasLabel(panel, "Needs: Wood") && HasLabel(panel, "5 slots"), "Hovering a plan must show its description and required resources.");
        await DoubleClick(plan!);
        Require(player.Mode.Placing == ThingKind.Chest && !panel.Visible, "A double click on a plan must enter placement mode.");
        await KeyStroke(Key.C);
        Vector2 place = Vector2.Zero;
        for (int x = -3; x <= 3; x++) for (int y = -3; y <= 3; y++) {
            var candidate = world.Player.Round() + new Vector2(x, y);
            if (world.Player.DistanceTo(candidate) <= 3 && world.Construction.PlacementBlock(ThingKind.Chest, candidate) == null) place = candidate;
        }
        Require(place != Vector2.Zero, "A fresh clearing must offer space for construction.");
        var point = view.Screen(place);
        var windowPoint = GetViewport().GetFinalTransform() * point;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = windowPoint, Pressed = false });
        await Frames();
        var chest = world.Things.LastOrDefault(t => t.Kind == ThingKind.Chest);
        Require(chest != null && chest.Rotation == 3 * Mathf.Pi / 2, "A ground click must construct the chest turned the other way by C.");
        Require(player.Mode.Placing == (world.Backpack.Has(world.Data.Buildings[ThingKind.Chest].Cost) ? ThingKind.Chest : null), "A ground click must stay in placement while another chest can be afforded and leave it otherwise.");
        Require(view.Selection.Thing != chest.Id, "A ground click must not select the new chest.");
        Require(!panel.Visible, "A ground click must not open a panel.");
        return chest!;
    }
}
