using System.Threading.Tasks;
using Godot;
using static Checks;
using static GlassJarSteps;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunGlassJarUIChecks() {
        await TestClean(CheckGlassFurnaceShowsSlotsAndStartsWithASpaceStroke);
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
        Require(Named($"job_{JobKind.BlowGlass.Id()}") != null, "The furnace should offer the separate Blow glass job.");
        await Holding(Key.Shift, () => Click(BagCellOf("steel_pipe")));
        await Until(() => world.Backpack["steel_pipe"] == pipes + 1
            && panel.Aside.FindChild("tool_glass blower", true, false) is ItemSlot { TooltipText: var tip } && tip.StartsWith("Steel pipe"),
            "installing just one steel pipe from the held stack into the glass blower slot", 2);

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
        await ClickOn(furnace);
        await Click((ItemSlot)panel.Aside.FindChild("tool_glass blower", true, false));
        await Click(BagCell(c => c.TooltipText == ""));
        await Until(() => world.Backpack["steel_pipe"] == pipes + 2 && cursor.Empty, "the slotted pipe going back into the backpack", 2);
        Hosted.Fire(blower);
        RemoveThings(Hosted, t => t == furnace);
        Drop(Hosted, "steel_pipe", 2);
        await CloseWithEsc();
        await Frames();
    }
}
