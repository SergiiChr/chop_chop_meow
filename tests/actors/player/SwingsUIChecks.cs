using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static ModelSteps;
using static PlayerSteps;
using static CabinSteps;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The UI checks of a player's swings: what Space throws and how it sounds.</summary>
    private async Task RunSwingsUIChecks() {
        await TestClean(CheckSpaceWithoutAnAxeThrowsAPunchTheWayThePlayerLastWalked);
    }

    /// <summary>
    /// Space without an axe throws a punch the way the player last walked, which whooshes and then thuds on a wolf
    /// there.
    /// </summary>
    private async Task CheckSpaceWithoutAnAxeThrowsAPunchTheWayThePlayerLastWalked() {
        var home = Hosted.Player;
        await CloseWithEsc();
        Drop(Hosted, "steel_axe");
        // By day wolves are calmed and sent home at once, which would clear the blow before the window shows it.
        float health = world.Health; RunUntilNight(Hosted);
        // Space swings the way the player last walked: a step to the right on screen aims the punch at a wolf there.
        await Holding(Key.D, () => Seconds(.1));
        await Frames();
        var wolf = PlaceWolf(Hosted, new Wolf { Pos = Hosted.Player + PlayerControl.KeyDirection(1, 0) * 1.2f, Health = 5 });
        int soundsBefore = sounds.Played;
        await KeyStroke(Key.Space);
        Require(view.PlayerAnimation == ActorAction.Attack, "Space without an axe must trigger the dedicated unarmed attack.");
        Require(sounds.Heard("tool_air_swing", soundsBefore) && !sounds.Heard("wolf_hit", soundsBefore), "A punch must whoosh before it lands.");
        await Until(() => sounds.Heard("wolf_hit", soundsBefore), "the punch thudding as it lands on the wolf", 2);

        var aim = PlayerControl.KeyDirection(1, 0);
        await Holding(Key.D, () => Seconds(.07));
        var turn = world.Player;
        await Holding(Key.A, () => Seconds(.07));
        Require((world.Player - turn).Dot(aim) < -.05f, "the player can immediately reverse direction during a fight");
        var facing = view.PlayerRig.Root.Basis.Z;
        Require(view.PlayerAnimation == ActorAction.Attack && new Vector2(facing.X, facing.Z).Dot(aim) > .99f,
            "the punch should keep facing its original aim while retreating");

        RemoveWolves(Hosted, w => w == wolf); RunUntilMorning(Hosted); SetNeeds(Hosted, health: health);
        await Until(() => view.PlayerAnimation != ActorAction.Attack, "the unarmed attack finishing", 3);
        Warp(Hosted, home); await Frames(4);
    }
}
