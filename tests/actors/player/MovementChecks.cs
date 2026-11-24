using ChopChopMeow;
using Godot;
using static Checks;
using static WorldSteps;

/// <summary>Checks of how the player moves.</summary>
public static class MovementChecks {
    public static void Run(GameData data) {
        Test(CheckPressingTheOppositeWayBrakesBeforeRunningBack, data);
    }

    private static void CheckPressingTheOppositeWayBrakesBeforeRunningBack(GameData data) {
        var momentum = new Momentum();
        Require(momentum.Step(Vector2.Right, 1 / 60f) == Vector2.Right, "a player setting off from a stand should run at full pace at once");
        Require(momentum.Step(Vector2.Down, 1 / 60f) == Vector2.Down, "a key at right angles should turn the run at once");
        var step = Vector2.Zero;
        for (int i = 0; i < 6; i++) step = momentum.Step(Vector2.Up, 1 / 60f);
        Require(momentum.Braking && step.Dot(Vector2.Down) is > 0 and < 1, $"0.1 s after pressing the opposite way the player should still slide on, slower, not {step}");
        for (int i = 0; i < 21; i++) step = momentum.Step(Vector2.Up, 1 / 60f);
        Require(!momentum.Braking && step.Dot(Vector2.Up) > 0, $"0.45 s after pressing the opposite way the player should be running back, not {step}");
        for (int i = 0; i < 12; i++) step = momentum.Step(Vector2.Up, 1 / 60f);
        Require(step == Vector2.Up, "the player should be back to full pace soon after turning round");
        Require(momentum.Step(Vector2.Zero, 1 / 60f) == Vector2.Zero && momentum.Step(Vector2.Down, 1 / 60f) == Vector2.Down, "letting go should stop the player at once, and the next key start a fresh run");
    }
}
