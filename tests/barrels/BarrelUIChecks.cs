using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The barrel can be opened, filled and drawn from in the backpack; its water and filter are visible after putting it down.</summary>
    private async Task CheckBarrelOpensInTheBackpackAndCanBePlacedWithWater() {
        int full = Drop(Hosted, Water.Full);
        int empty = Drop(Hosted, Water.Empty);
        int oneLiter = Drop(Hosted, Water.OneLiter);
        int twoLiters = Drop(Hosted, Water.TwoLiters);
        Give(Hosted, Barrel.Item);
        Give(Hosted, Water.Full);
        await KeyStroke(Key.Tab);
        await DoubleClick(BagCell(c => c.TooltipText.StartsWith("Steel barrel")));
        await Until(() => panel.Page == Page.Barrel && Named("barrel_water")?.TooltipText.Contains("0/50 L") == true, "the empty barrel opening in the backpack", 2);
        await Carry(BagCell(c => c.TooltipText.StartsWith(world.Data.Name(Water.Full))), Named("barrel_water")!);
        await Until(() => Named("barrel_water")?.TooltipText.Contains("3/50 L") == true, "the barrel holding three liters after the bottle is poured", 2);

        await Click(Named("barrel_water")!);
        await Until(() => Named("barrel_water")?.TooltipText.Contains("2/50 L") == true && world.Backpack[Water.OneLiter] == 1, "taking one liter of water from the barrel into the bottle", 2);
        await Click(FindButton(panel, "Put it down")!);
        var spot = SpotsNearby().First(p => world.Construction.PlacementBlock(ThingKind.Barrel, p, Barrel.AtFill(2)) == null && Uncovered(view.Screen(p)));
        await Click(view.Screen(spot));
        await Until(() => Hosted.Things.Any(t => t.Kind == ThingKind.Barrel && t.Pos == spot), "putting the barrel down", 2);
        var barrel = Hosted.Things.Single(t => t.Kind == ThingKind.Barrel && t.Pos == spot);
        await ClickOn(barrel);
        await Until(() => Named("barrel_water")?.TooltipText.Contains("2/50 L") == true, "the placed barrel showing its two liters", 2);
        Require(Named("job_haul") != null, "A placed barrel must offer a hauling job.");
        await Click(Named("filters")!);
        string waterFilter = $"filter_in_{Water.Liquid}";
        Require(OnPage("filter_out") == null && Named(waterFilter) != null && Named("filter_add_in") == null,
            "A barrel containing water must show one filter row naming water, with no add button.");

        await Holding(Key.Shift, () => Click(Named("barrel_water")!));
        await Until(() => Named("barrel_water")?.TooltipText.Contains("0/50 L") == true && world.Backpack[Water.Full] == 1, "drawing the remaining water into the bottle", 2);
        await Click(Named(waterFilter)!);
        await Until(() => Named("filter_add_in") != null, "removing the water filter from the empty barrel", 2);
        await Click(Named("filter_add_in")!);
        Require(OnPage("filter_out") == null && Named($"pick_{Water.Liquid}") != null && Named("pick_stone") == null,
            "An empty barrel must offer only water in its filter picker.");
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == barrel);
        Drop(Hosted, Water.Full, 1);
        Give(Hosted, Water.Full, full);
        Give(Hosted, Water.Empty, empty);
        Give(Hosted, Water.OneLiter, oneLiter);
        Give(Hosted, Water.TwoLiters, twoLiters);
        await Frames();
    }
}
