using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static ModelSteps;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task CheckStartMenuPutsCameraSwitchInOptions() {
        Require(startMenu.Screen.FindChild("first_person", true, false) == null, "The main menu should not show the camera switch.");
        await Click(FindButton(startMenu.Screen, "Options")!);
        Require(startMenu.Screen.FindChild("first_person", true, false) is CheckButton { ButtonPressed: false },
            "The first-person preview should be offered in Options and start switched off.");
        await Click(FindButton(startMenu.Screen, "Back · Esc")!);
    }

    private async Task RunFirstPersonUIChecks() {
        await TestClean(CheckFirstPersonDrawsDistantThingsAndTheLocalBody);
        await TestClean(CheckFirstPersonTurnsWalksSwingsAndSelects);
    }

    /// <summary>The preview draws objects outside the explored area and shows the player's body from eye level.</summary>
    private async Task CheckFirstPersonDrawsDistantThingsAndTheLocalBody() {
        var distant = Hosted.Add(ThingKind.Chest, world.Player - new Vector2(25, 25));
        await Frames();
        Require(!world.Explored(distant.Pos), "The distant chest must be outside explored ground.");
        Require(view.PropOf(distant.Id)?.Root.IsVisibleInTree() != true, "Unexplored chests should be hidden in the overhead view.");
        var environment = view.GetWorld3D().Environment;
        var overheadSky = environment.Sky;
        await ToggleFirstPersonInPauseMenu();
        Require(environment.BackgroundMode == Environment.BGMode.Sky && environment.Sky != overheadSky,
            "First person should draw its own sky.");
        Require(view.PropOf(distant.Id)?.Root.IsVisibleInTree() == true, "First person should show the distant chest.");
        var camera = GetViewport().GetCamera3D();
        Require(AllMeshes(view.PlayerRig.Torso).Any(part => part.IsVisibleInTree() && (part.Layers & camera.CullMask) != 0),
            "The player's body must be visible from the first-person camera.");
        await ToggleFirstPersonInPauseMenu();
        Require(environment.BackgroundMode == Environment.BGMode.Color && environment.Sky == overheadSky,
            "Overhead view should restore its original lighting and switch off the visible sky.");
        Require(view.PropOf(distant.Id)?.Root.IsVisibleInTree() != true, "Returning overhead should hide unexplored objects again.");
        RemoveThings(Hosted, t => t == distant);
        await Frames();
    }

    /// <summary>Mouse look steers the player and their swings and points clicks at a station.</summary>
    private async Task CheckFirstPersonTurnsWalksSwingsAndSelects() {
        var home = world.Player;
        var bench = Hosted.Add(ThingKind.Bench, home + new Vector2(2.6f, -2.6f));
        await ToggleFirstPersonInPauseMenu();
        var camera = GetViewport().GetCamera3D();
        Require(camera.Projection == Camera3D.ProjectionType.Perspective, "The preview must switch to the eye-level camera.");
        // Turning 90° right from the starting direction makes the player look toward the bench.
        var center = GetViewport().GetVisibleRect().GetCenter();
        var window = GetViewport().GetFinalTransform();
        Input.ParseInputEvent(new InputEventMouseMotion {
            Position = window * center,
            GlobalPosition = window * center,
            Relative = window.BasisXform(new Vector2(628, 0))
        });
        await Frames();
        var worker = AddWorker(Hosted, home + new Vector2(3.5f, -3.5f));
        await Frames();
        Require(view.PickWorker(center)?.Id == worker.Id, "The worker must be lined up behind the bench for this check.");
        await Click(center);
        Require(Shows(bench), "A first-person click must select the bench in front of the worker.");
        Hosted.Fire(worker);
        await CloseWithEsc();
        await SwingWhileWalkingForward(home);
        await ToggleFirstPersonInPauseMenu();
        Require(camera.Projection == Camera3D.ProjectionType.Orthogonal, "Switching off the preview must restore the overhead camera.");
        RemoveThings(Hosted, t => t == bench);
        Warp(Hosted, home);
        await Frames(4);
    }
}
