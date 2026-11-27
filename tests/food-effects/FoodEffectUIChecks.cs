using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunFoodEffectUIChecks() {
        await TestClean(JamShowsAtMostOneFullHungerBar);
        await TestClean(EatingStewShowsItsEffectsBelowTheNeedsPlate);
    }

    private async Task JamShowsAtMostOneFullHungerBar() {
        Give(Hosted, BerryJam.Bottle);
        Give(Hosted, "canned_jam");
        Hosted.Got(BerryJam.Liquid);
        Hosted.Got("canned_jam");
        await KeyStroke(Key.Tab);
        await Until(() => world.Backpack[BerryJam.Bottle] > 0 && world.Backpack["canned_jam"] > 0 && world.Had("canned_jam"),
            "both jams in the backpack and discovered on the map");
        var canned = BagCellOf("canned_jam");
        Require(BagCellOf(BerryJam.Bottle).TooltipText.Contains("Hunger: +100% per liter\n")
            && canned.TooltipText.Contains("Hunger: +100%\n"), "both jam tooltips should show a full bar, not the unused amount");
        await Click(canned, alt: true);
        await Until(() => recipeMap.Visible && recipeMap.Chosen == "canned_jam", "the canned jam card highlighted");
        Require(recipeMap.FindChild("food_description", true, false) is RichTextLabel cannedDetails
            && cannedDetails.Text.Contains("Hunger: +100%\n"), "the canned jam map entry should show a full bar");
        await Click(recipeMap.NodeOf(BerryJam.Liquid)!);
        await Until(() => recipeMap.Chosen == BerryJam.Liquid, "the berry jam card selected");
        Require(recipeMap.FindChild("food_description", true, false) is RichTextLabel berryDetails
            && berryDetails.Text.Contains("Hunger: +100% per liter\n"), "the berry jam map entry should show a full bar per liter");
        await KeyStroke(Key.Escape);
        Drop(Hosted, BerryJam.Bottle, 1);
        Drop(Hosted, "canned_jam", 1);
        await CloseWithEsc();
    }

    private async Task EatingStewShowsItsEffectsBelowTheNeedsPlate() {
        var (hunger, thirst, health) = (Hosted.Hunger, Hosted.Thirst, Hosted.Health);
        SetNeeds(Hosted, thirst: 100);
        Give(Hosted, "meat_stew");
        await KeyStroke(Key.Tab);
        await Until(() => world.Backpack["meat_stew"] == 1, "the stew in the backpack");
        var stew = BagCellOf("meat_stew");
        Require(stew.TooltipText.Contains("Hunger: fills to 150%\nEffects:\n• Well fed:")
            && stew.TooltipText.Contains("• Protein recovery:") && !stew.TooltipText.Contains("LMB:"),
            "the stew's description should show hunger and both effects with no controls line");
        Hosted.Got("meat_stew");
        await Until(() => world.Had("meat_stew"), "the map knowing the meat stew recipe");
        await Click(stew, alt: true);
        await Until(() => recipeMap.Visible && recipeMap.Chosen == "meat_stew", "the meat stew card highlighted");
        Require(recipeMap.FindChild("food_description", true, false) is RichTextLabel details
            && details.Text.Contains("Hunger: fills to 150%") && details.Text.Contains("• [b]Well fed[/b]:")
            && details.Text.Contains("• [b]Protein recovery[/b]:"), "the map should show hunger and bold only the effect names");
        await KeyStroke(Key.Escape);
        await DoubleClick(stew);
        await CloseWithEsc();
        var recovery = foodEffectRow.GetNode<FoodEffectBadge>("ProteinRecovery");
        var fed = foodEffectRow.GetNode<FoodEffectBadge>("WellFed");
        await Until(() => foodEffectRow.Visible && recovery.Visible && fed.Visible && needsPlate.Hunger.Level > 149,
            "the stew's two badges and the hunger second lap appearing together", 3);
        Require(foodEffectRow.GetRect().Position.Y > needsPlate.GetRect().End.Y
            && fed.Position.X > recovery.Position.X && fed.Position.Y == recovery.Position.Y, "the badges should form one row below the needs plate");
        await Hover(fed);
        Require(GetViewport().GuiGetHoveredControl() == fed, "hovering the Well fed badge should hit its tooltip control");
        Hosted.Tick(150, sleeping: true);
        await Until(() => !recovery.Visible && fed.Visible, "only the timed Protein recovery badge expiring");
        Hosted.Tick(271, sleeping: true);
        await Until(() => !foodEffectRow.Visible, "Well fed and the empty row disappearing below 100% hunger");
        SetNeeds(Hosted, hunger, thirst, health);
        await Hover(needsPlate.Hunger);
    }
}
