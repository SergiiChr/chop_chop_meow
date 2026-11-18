using ChopChopMeow;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks of wolves out on patrol smelling a player.</summary>
public static class WolfPatrolsChecks {
    public static void Run(GameData data) {
        Test(CheckAWolfTurnsToAPlayerItSmellsBeforeItHowls, data);
    }

    /// <summary>A wolf walking straight away from a player it smells has half a turn to make, which takes it 1.3 seconds.</summary>
    private static void CheckAWolfTurnsToAPlayerItSmellsBeforeItHowls(GameData data) {
        var world = NightWorld(data);
        world.Player = Terrain.Point(120, 0);
        var smeller = PatrolWolf(world, new Wolf { Pos = world.Player + Terrain.Seaward * (data.SmellRadius - 5), Heading = Terrain.Seaward.Angle() });
        var called = PatrolWolf(world, new Wolf { Pos = smeller.Pos + Terrain.Seaward * 20, Heading = Terrain.Seaward.Angle() });
        var heard = Listen(world);
        SafeTick(world, .1f);
        var at = smeller.Pos;
        Require(smeller.Target == world.Me.Id, "a wolf within smell of the player should take them as its prey");
        SafeTick(world, 1);
        Require(smeller.Howl == 0 && !heard.Contains("wolf_howl") && called.Mode == WolfMode.Patrol, $"a wolf facing away from the player it smells should turn to them in silence before it howls: heard {string.Join(", ", heard)}");
        Require(smeller.Pos == at, "a wolf turning to the player it smells should stand where it is");
        SafeTick(world, .4f);
        Require(smeller.Howl > 0 && heard.Contains("wolf_howl") && called.Mode == WolfMode.Called, "a wolf that has turned to the player it smells should howl, calling the wolves around");
        Require(smeller.Howl > smeller.HowlLength - .35f, $"the howl should start once the half turn is made, 1.3 s in, not {smeller.HowlLength - smeller.Howl:0.00} s before now");
    }
}
