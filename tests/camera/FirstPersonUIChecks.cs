using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static ModelSteps;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
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
        await ToggleFirstPersonInPauseMenu();
        Require(view.PropOf(distant.Id)?.Root.IsVisibleInTree() == true, "First person should show the distant chest.");
        var camera = GetViewport().GetCamera3D();
        Require(AllMeshes(view.PlayerRig.Torso).Any(part => part.IsVisibleInTree() && (part.Layers & camera.CullMask) != 0),
            "The player's body must be visible from the first-person camera.");
        await ToggleFirstPersonInPauseMenu();
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
        var aim = Vector2.Zero;
        void RecordSwing(ActorAction action, Vector2 target) => aim = target - Hosted.Player;
        Hosted.PlayerActed += RecordSwing;
        try {
            await KeyStroke(Key.Space);
            await Until(() => aim != Vector2.Zero, "a first-person swing", 2);
            Require(aim.X > .5f && aim.Y < -.5f, $"Space should swing toward the bench, but swung along {aim}.");
        } finally { Hosted.PlayerActed -= RecordSwing; }
        await Holding(Key.W, () => Until(() => world.Player.DistanceTo(home) >= .4f, "walking forward in first person", 2));
        var step = world.Player - home;
        Require(step.X > .2f && step.Y < -.2f, $"W should walk toward the bench, but walked along {step}.");
        await ToggleFirstPersonInPauseMenu();
        Require(camera.Projection == Camera3D.ProjectionType.Orthogonal, "Switching off the preview must restore the overhead camera.");
        RemoveThings(Hosted, t => t == bench);
        Warp(Hosted, home);
        await Frames(4);
    }
}
