using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

public partial class Game {
    private CheckButton? FirstPersonPreviewOption() =>
        startMenu.Screen.FindChild("first_person_preview", recursive: true, owned: false) as CheckButton;

    private async Task ToggleFirstPersonPreviewInPauseMenu() {
        await KeyStroke(Key.Escape);
        await Click(FindButton(menu, "Options")!);
        await Click(FirstPersonPreviewOption()!);
        await Click(FindButton(startMenu.Screen, "Back · Esc")!);
        await Click(FindButton(menu, "Resume · Esc")!);
    }

    /// <summary>Walks toward the bench and checks that the swing keeps its direction while moving.</summary>
    private async Task SwingWhileWalkingForward(Vector2 home) {
        var aim = Vector2.Zero;
        var started = home;
        void RecordSwing(ActorAction action, Vector2 direction) {
            started = Hosted.Player;
            aim = direction;
        }
        Hosted.PlayerActed += RecordSwing;
        try {
            await Holding(Key.W, async () => {
                await Until(() => world.Player.DistanceTo(home) >= .4f, "walking forward in first person", 2);
                var step = world.Player - home;
                Require(step.X > .2f && step.Y < -.2f, $"W should walk toward the bench, but walked along {step}.");
                await KeyStroke(Key.Space);
                await Until(() => aim != Vector2.Zero, "a first-person swing", 2);
                Require(aim.X > .5f && aim.Y < -.5f, $"Space should swing toward the bench, but swung along {aim}.");
                await Until(() => (world.Player - started).Dot(aim) > 1.1f && ActorAnimation.SwingsOnTheMove(view.PlayerAnimation),
                    "moving more than a metre forward during the swing", 2);
                var facing = ActorAnimation.Forward(view.PlayerRig.Root.Rotation.Y);
                Require(facing.Dot(aim) > .9f, $"The visible swing should continue along {aim}, but faced {facing}.");
            });
        } finally { Hosted.PlayerActed -= RecordSwing; }
    }
}
