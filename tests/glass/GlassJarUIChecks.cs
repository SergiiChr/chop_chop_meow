using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static GlassJarSteps;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunGlassJarUIChecks() {
        // Hired workers head home at dusk, which the checks before can reach on a slow machine.
        RunUntilMorning(Hosted);
        await TestClean(CheckGlassFurnaceShowsSlotsAndStartsWithASpaceStroke);
        await TestClean(CheckBellowsControlsWalkAroundTheSelectedFurnace);
        await TestClean(CheckBothGlassBlowersAimAtTheFire);
    }

    private async Task CheckBellowsControlsWalkAroundTheSelectedFurnace() {
        var home = world.Player;
        var forge = Hosted.Add(ThingKind.Reinforced, home + Vector2.Right);
        Hosted.SetRecipe(forge, GlassBlowing.Item);
        var front = forge.ToWorld(GlassBlowing.BlowStand);
        var rear = forge.ToWorld(World.BellowsStand);
        Warp(Hosted, front);
        await ClickOn(forge);
        Require(panel.FindChild("bellows_hint", true, false) is Label { Text: "Stand behind the furnace to pump." },
            "The bellows hint should no longer explain where to stand to blow.");

        await KeyStroke(Key.Space);
        Require(forge.Heat == 0, "Space should not pump until the player has walked to the bellows.");
        await Until(() => world.Player.DistanceTo(rear) < .06f && Shown(forge).Heat > 0,
            "Space walking round the furnace and pumping once from behind", 7);

        forge.Heat = 0;
        Warp(Hosted, front);
        await Until(() => world.Player.DistanceTo(front) < .05f && Shown(forge).Heat == 0,
            "the player back on the far side with the furnace cooled", 2);
        await Click(Named("pump")!);
        await Until(() => world.Player.DistanceTo(rear) < .06f && Shown(forge).Heat > 0,
            "the Pump button walking round the furnace before applying heat", 7);

        Warp(Hosted, home);
        await Until(() => world.Player.DistanceTo(home) < .1f, "returning to the clearing", 2);
        RemoveThings(Hosted, t => t == forge);
        await CloseWithEsc();
        await Frames();
    }

    private async Task CheckBothGlassBlowersAimAtTheFire() {
        var home = world.Player;
        var forge = GlassFurnace(Hosted, home + Vector2.Right, rotation: Mathf.Pi / 2);
        var pumper = HireAtGlassFurnace(Hosted, forge, JobKind.Bellows);
        forge.Heat = 80;
        Warp(Hosted, forge.ToWorld(GlassBlowing.BlowStand));
        await Until(() => world.Find(forge.Id)?.GlassBlower == world.Me.Id && AimsAtFire(),
            "the player's glass blowing pipe pointing into the fire of a turned furnace", 4);

        var blower = HireAtGlassFurnace(Hosted, forge, JobKind.BlowGlass);
        Warp(Hosted, forge.ToWorld(World.BellowsStand));
        await Until(() => world.Find(forge.Id)?.GlassBlower == -blower.Id && AimsAtFire(),
            "the hired blower also aiming at the fire", 4);

        Warp(Hosted, home);
        await Until(() => world.Player.DistanceTo(home) < .1f, "returning to the clearing", 2);
        Hosted.Fire(pumper); Hosted.Fire(blower);
        RemoveThings(Hosted, t => t == forge);
        await Frames();

        bool AimsAtFire() {
            if (view.PropOf(forge.Id) is not ForgeProp drawn) return false;
            var fromTip = ForgeShape.FireCenter - drawn.Blowing.PipeEnd;
            return drawn.Blowing.PipeDirection.Dot(fromTip) > 0
                && drawn.Blowing.PipeDirection.Cross(fromTip).Length() < .04f;
        }
    }

    private async Task CheckGlassFurnaceShowsSlotsAndStartsWithASpaceStroke() {
        var home = world.Player;
        var furnace = Hosted.Add(ThingKind.Reinforced, home + Vector2.Right);
        int pipes = world.Backpack["steel_pipe"];
        Give(Hosted, "steel_pipe", 2); Give(Hosted, Beach.Sand, 5); Give(Hosted, "coal");
        await ClickOn(furnace);
        string cardName = $"craft_{GlassBlowing.Item}";
        if (Named(cardName) == null) await Click(Named($"group_{world.Data.Recipes[GlassBlowing.Item].Group}")!);
        await DoubleClick(Named(cardName)!);
        await Until(() => Named("input_sand") != null && Named("output_glass_jar") != null && Named("fuel") != null
            && Shown(furnace).Inventory[Beach.Sand] == 5 && Shown(furnace).Burner?["coal"] == 1,
            "double-clicking Glass jar loading sand and coal in separate slots", 2);
        Require(panel.FindChild("heat", true, false) is Control { TooltipText: "Glass requires 1,300–1,600 °C" },
            "The glass heat dial should show its lower and upper heat limits.");
        Require(Named($"job_{JobKind.BlowGlass.Id()}") is Button job
            && job.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text == "Glass blower"),
            "The furnace should show the separate Glass blower job.");
        await ShowBackpack();
        await Holding(Key.Shift, () => Click(BagCellOf("steel_pipe")));
        await Until(() => world.Backpack["steel_pipe"] == pipes + 1
            && panel.ToolsWindow.FindChild($"tool_{GlassBlowing.Tool}", true, false) is ItemSlot { TooltipText: var tip } && tip.StartsWith("Steel pipe"),
            "installing just one steel pipe from the held stack into the Glass blowing pipe slot", 2);

        var blocker = Hosted.Add(ThingKind.Hut, furnace.ToWorld(World.BellowsStand) + new Vector2(0, -.8f));
        await Until(() => panel.FindChild("glass_status", true, false) is Label { Text: "Clear ground behind the furnace for the bellows." },
            "showing that access to the bellows is blocked", 2);
        RemoveThings(Hosted, t => t == blocker);

        var blower = HireAtGlassFurnace(Hosted, furnace, JobKind.BlowGlass);
        await CloseWithEsc();
        Warp(Hosted, furnace.ToWorld(World.BellowsStand));
        await Until(() => world.Player.DistanceTo(furnace.ToWorld(World.BellowsStand)) < .1f, "the player reaching the bellows", 2);
        // A single stroke from 1250 °C should enter the working range without waiting through a cold start.
        furnace.Heat = 69;
        await Until(() => Shown(furnace).Heat > 65 && Shown(furnace).Heat < 71, "the heat dial receiving the furnace's warmth", 2);
        await KeyStroke(Key.Space);
        await Until(() => Shown(furnace).GlassBlower == -blower.Id && Shown(furnace).Processing,
            "a Space stroke at the bellows with the page closed letting the hired glass blower begin", 2);

        Warp(Hosted, home);
        await Until(() => world.Player.DistanceTo(home) < .1f, "returning to the clearing", 2);
        await ClickOn(furnace); await UnrollTools(); await ShowBackpack();
        await Click((ItemSlot)panel.ToolsWindow.FindChild($"tool_{GlassBlowing.Tool}", true, false));
        await Click(BagCell(c => c.TooltipText == ""));
        await Until(() => world.Backpack["steel_pipe"] == pipes + 2 && cursor.Empty, "the slotted pipe going back into the backpack", 2);
        Hosted.Fire(blower);
        RemoveThings(Hosted, t => t == furnace);
        Drop(Hosted, "steel_pipe", 2);
        await CloseWithEsc();
        await Frames();
    }
}
