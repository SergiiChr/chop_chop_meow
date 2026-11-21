using System;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The UI checks of mines: digging a marked mine's entrance by hand and its page through the stages.</summary>
    private async Task RunMineUIChecks() {
        await TestClean(CheckRightMouseOnAMarkedMineDigsItWithAPickaxe);
        await TestClean(CheckMinePageFollowsTheMineThroughItsStages);
    }

    /// <summary>
    /// Holding the right mouse button on a marked mine walks the player up to the rock face beside its cave mouth and digs
    /// the entrance with the pickaxe, which lands on the rock.
    /// </summary>
    private async Task CheckRightMouseOnAMarkedMineDigsItWithAPickaxe() {
        // The server's position, since the window's copy may not have caught up with the last check's warp.
        var start = Hosted.Player;
        var cliff = await StandAtIronCliff();
        var mine = Hosted.Construction.Build(ThingKind.Mine, cliff.MineSpot)!;
        Give(Hosted, "steel_pick", 1);
        await Frames(2);
        var point = RightPress(mine.Pos);
        await Until(() => mine.Progress > 0 && view.PlayerAnimation == ActorAction.SteelMine, "holding RMB on a marked mine walking to its entrance and digging with the pickaxe", 6);
        // The pick lands on the cliff's rock face beside the cave mouth, which is 1.44 m wide in a face 2.28 m behind the mine's spot.
        int soundsBefore = sounds.Played;
        await Until(() => sounds.Heard("mine_ore", soundsBefore), "a pick's blow heard at the mine's entrance", 4);
        var end = view.PlayerRig.Root.Transform * view.PlayerRig.Holds(ActorRig.PickPoint);
        var flat = new Vector2(end.X, end.Z) - (mine.Pos - cliff.Face * 2.28f);
        float behind = -flat.Dot(cliff.Face), aside = Math.Abs(flat.Dot(new Vector2(-cliff.Face.Y, cliff.Face.X)));
        Require(Math.Abs(behind) < .06f && aside > .72f && aside < 1.2f && end.Y is > .5f and < 1.4f,
            $"A pick digging a mine's entrance must land on the rock face beside its mouth, not {behind:0.00} m into the face, {aside:0.00} m aside and {end.Y:0.00} m up.");
        RightRelease(point);
        await Frames();
        Drop(Hosted, "steel_pick", 1);
        RemoveThings(Hosted, t => t == mine); Warp(Hosted, start); await CloseWithEsc(); await Frames(4);
    }

    /// <summary>A mine's page left open follows the mine from stage to stage and on to the open mine.</summary>
    private async Task CheckMinePageFollowsTheMineThroughItsStages() {
        var start = Hosted.Player;
        var cliff = await StandAtIronCliff();
        var mine = Hosted.Construction.Build(ThingKind.Mine, cliff.MineSpot)!;
        await ClickOn(mine);
        await Until(() => HasLabel(panel, "Stage 1 · Dig the entrance"), "the mine's page opening on its first stage", 3);
        mine.MineStage = MineStage.Dug;
        await Until(() => HasLabel(panel, "Stage 2 · Shore it up") && ShowsChild(panel, "supply_wood"), "the open mine page moving on to the supplies", 3);
        mine.MineStage = MineStage.Supplied;
        await Until(() => HasLabel(panel, "Stage 3 · Fit the frame and rails"), "the open mine page moving on to the fitting", 3);
        mine.MineStage = MineStage.Open;
        await Until(() => ShowsChild(panel, "output_stone") && !HasLabel(panel, "Stage 3"), "the open mine page showing the mine's outputs once it opens", 3);
        RemoveThings(Hosted, t => t == mine); Warp(Hosted, start); await CloseWithEsc(); await Frames(4);
    }
}
