using System.Linq;
using System.Threading.Tasks;
using static SetupSteps;

namespace ChopChopMeow;

/// <summary>
/// Every station recipe at the moment it is ready to start: all its items and fuel are in the station, so the next tick starts the batch.
/// Each check loads a fresh station with the player's own controls, double-clicking the recipe's card or dragging from the backpack.
/// </summary>
public partial class Game {
    /// <summary>With every item and the fuel of a batch in the backpack, a double-click on the card leaves each recipe ready to start.</summary>
    private async Task CheckEveryRecipeIsReadyToStartAfterADoubleClickWithAllItsItemsCarried() {
        foreach (var (id, r) in StationRecipes) {
            var station = await OpenStationFor(r);
            GiveInputs(r); if (r.Burns) GiveFuel(r);
            await Frames(2);
            await DoubleClick(Named($"craft_{id}")!);
            await ExpectStarts(station, id, "after a double-click with its items and fuel in the backpack");
            await CloseStation(station);
        }
    }

    /// <summary>
    /// With one unit of its first item, and its fuel, a double-click leaves no recipe ready to start, shakes the cups and names what is missing.
    /// A recipe whose whole batch is a single unit has no such case.
    /// </summary>
    private async Task CheckNoRecipeIsReadyToStartAfterADoubleClickWithOneOfItsItems() {
        foreach (var (id, r) in StationRecipes.Where(p => p.Recipe.Inputs.Values.Sum() > 1)) {
            var station = await OpenStationFor(r);
            GiveInput(r.Inputs.Keys.First(), 1); if (r.Burns) GiveFuel(r);
            await Frames(2);
            // Every item but the one carried is missing, and that one too when a batch takes more of it.
            var missing = r.Inputs.Where((p, i) => i > 0 || p.Value > 1).Select(p => world.Lower(world.Data.Name(p.Key))).ToArray();
            await ExpectRefusedDoubleClick(station, id, r, "with one of its items in the backpack", missing);
            await CloseStation(station);
        }
    }

    /// <summary>With its items but no fuel in the backpack, a double-click leaves no burning recipe ready to start, shakes the cups and says fuel is missing.</summary>
    private async Task CheckNoRecipeIsReadyToStartAfterADoubleClickWithoutFuel() {
        foreach (var (id, r) in StationRecipes.Where(p => p.Recipe.Burns)) {
            var station = await OpenStationFor(r);
            GiveInputs(r);
            await Frames(2);
            await ExpectRefusedDoubleClick(station, id, r, "with its items but no fuel in the backpack", "fuel");
            await CloseStation(station);
        }
    }

    /// <summary>With fuel but none of its items in the backpack, a double-click leaves no burning recipe ready to start, shakes the cups and names every item missing.</summary>
    private async Task CheckNoRecipeIsReadyToStartAfterADoubleClickWithFuelAlone() {
        foreach (var (id, r) in StationRecipes.Where(p => p.Recipe.Burns)) {
            var station = await OpenStationFor(r);
            GiveFuel(r);
            await Frames(2);
            await ExpectRefusedDoubleClick(station, id, r, "with only fuel in the backpack", r.Inputs.Keys.Select(item => world.Lower(world.Data.Name(item))).ToArray());
            await CloseStation(station);
        }
    }

    /// <summary>With its fuel and items dragged onto their cups one by one, each recipe is ready to start.</summary>
    private async Task CheckEveryRecipeIsReadyToStartWhenLoadedByHand() {
        foreach (var (id, r) in StationRecipes) {
            var station = await OpenStationFor(r);
            await LoadByHand(station, id, r);
            await ExpectStarts(station, id, "once its fuel and items are dragged onto their cups");
            await CloseStation(station);
        }
    }
}
