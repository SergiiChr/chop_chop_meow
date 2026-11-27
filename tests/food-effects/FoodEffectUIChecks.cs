using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunFoodEffectUIChecks() {
        await TestClean(JamShowsAtMostOneFullHungerBar);
        await TestClean(CookingOutputShowsWhatTheFoodRestores);
        await TestClean(AJamBarrelShowsWhatItsJamRestores);
        await TestClean(EatingStewShowsItsEffectsBesideTheLayerSwitch);
    }

    private async Task JamShowsAtMostOneFullHungerBar() {
        Give(Hosted, BerryJam.Bottle);
        Give(Hosted, "canned_jam");
        bool knewJam = Hosted.Had("canned_jam");
        var restoreDiscoveries = TemporarilyDiscover(Hosted, BerryJam.Liquid, "canned_jam");
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
        restoreDiscoveries();
        await Until(() => world.Had("canned_jam") == knewJam, "the jam's original discovery state restored");
    }

    private async Task CookingOutputShowsWhatTheFoodRestores() {
        var table = Hosted.Add(ThingKind.Cooktable, Hosted.Player + new Vector2(1.2f, 0));
        Hosted.SetRecipe(table, "meat_stew");
        await ClickOn(table);
        var output = OnPage("output_meat_stew")!;
        Require(output.TooltipText.Contains("LMB:") && !output.TooltipText.Contains("Effects:"), "an empty output should still explain how to use the slot");
        table.Inventory.Add("meat_stew", 1);
        await Until(() => output.TooltipText.StartsWith("Meat stew · 1/"), "the cooked stew in the output");
        Require(output.TooltipText.Contains("Hunger: fills to 150%\nEffects:") && !output.TooltipText.Contains("LMB:"),
            "a finished stew should show hunger and effects without the controls line");
        RemoveThings(Hosted, t => t == table);
        await CloseWithEsc();
    }

    private async Task AJamBarrelShowsWhatItsJamRestores() {
        var barrel = Hosted.Add(ThingKind.Barrel, Hosted.Player + new Vector2(1.2f, 0));
        barrel.Inventory.Add(BerryJam.Liquid, 2);
        await ClickOn(barrel);
        var contents = OnPage("barrel_water")!;
        Require(contents.TooltipText.StartsWith("Berry jam · 2/") && contents.TooltipText.Contains("Hunger: +100% per liter\nEffects:")
            && !contents.TooltipText.Contains("LMB:"), "jam in a barrel should show what one liter does without the controls line");
        RemoveThings(Hosted, t => t == barrel);
        await CloseWithEsc();
    }

    private async Task EatingStewShowsItsEffectsBesideTheLayerSwitch() {
        var layerPosition = layerButton.GetGlobalRect().Position;
        var (hunger, thirst, health) = (Hosted.Hunger, Hosted.Thirst, Hosted.Health);
        SetNeeds(Hosted, thirst: 100);
        Give(Hosted, "meat_stew");
        await KeyStroke(Key.Tab);
        await Until(() => world.Backpack["meat_stew"] == 1, "the stew in the backpack");
        var stew = BagCellOf("meat_stew");
        Require(stew.TooltipText.Contains("Hunger: fills to 150%\nEffects:\n• Well fed:")
            && stew.TooltipText.Contains("• Protein recovery:") && !stew.TooltipText.Contains("LMB:"),
            "the stew's description should show hunger and both effects with no controls line");
        var restoreDiscoveries = TemporarilyDiscover(Hosted, "meat_stew");
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
        var layerBounds = layerButton.GetGlobalRect();
        var recoveryBounds = recovery.GetGlobalRect();
        var fedBounds = fed.GetGlobalRect();
        Require(layerBounds.Position == layerPosition && layerBounds.Position.Y > needsPlate.GetGlobalRect().End.Y,
            "the Layer switch should stay below the needs plate when food effects appear");
        Require(recoveryBounds.Position.Y == layerBounds.Position.Y && fedBounds.Position.Y == layerBounds.Position.Y
            && recoveryBounds.Position.X > layerBounds.End.X && fedBounds.Position.X > recoveryBounds.End.X,
            "active badges should line up to the right of the Layer switch");
        Require(recoveryBounds.Size == layerBounds.Size && fedBounds.Size == layerBounds.Size,
            "each badge should take up the same space as the Layer switch");
        await Hover(fed);
        Require(GetViewport().GuiGetHoveredControl() == fed, "hovering the Well fed badge should hit its tooltip control");
        Hosted.Tick(150, sleeping: true);
        await Until(() => !recovery.Visible && fed.Visible, "only the timed Protein recovery badge expiring");
        Hosted.Tick(271, sleeping: true);
        await Until(() => !foodEffectRow.Visible, "Well fed and the empty row disappearing below 100% hunger");
        SetNeeds(Hosted, hunger, thirst, health);
        restoreDiscoveries();
        await Hover(needsPlate.Hunger);
    }
}
