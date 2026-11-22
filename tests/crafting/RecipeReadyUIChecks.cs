using System.Linq;
using System.Threading.Tasks;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

/// <summary>
/// Every station recipe at the moment it is ready to start: all its items and fuel are in the station, so the next tick starts the batch.
/// Each check takes one recipe and loads a fresh station with the player's own controls, double-clicking the recipe's card or carrying from the backpack.
/// </summary>
public partial class Game {
    /// <summary>
    /// Runs each recipe check for every station recipe it applies to, as a check of its own named after the recipe,
    /// so each has its own time limit while all share the one window and world.
    /// </summary>
    private async Task RunRecipeReadyChecks() {
        foreach (var (id, r) in StationRecipes) {
            await Test(() => CheckRecipeIsReadyToStartAfterADoubleClickWithAllItsItemsCarried(id, r), name: $"{nameof(CheckRecipeIsReadyToStartAfterADoubleClickWithAllItsItemsCarried)}({id})");
            if (r.Inputs.Values.Sum() > 1)
                await Test(() => CheckRecipeIsNotReadyToStartAfterADoubleClickWithOneOfItsItems(id, r), name: $"{nameof(CheckRecipeIsNotReadyToStartAfterADoubleClickWithOneOfItsItems)}({id})");
            if (r.Burns) {
                await Test(() => CheckRecipeIsNotReadyToStartAfterADoubleClickWithoutFuel(id, r), name: $"{nameof(CheckRecipeIsNotReadyToStartAfterADoubleClickWithoutFuel)}({id})");
                await Test(() => CheckRecipeIsNotReadyToStartAfterADoubleClickWithFuelAlone(id, r), name: $"{nameof(CheckRecipeIsNotReadyToStartAfterADoubleClickWithFuelAlone)}({id})");
            }
            await Test(() => CheckRecipeIsReadyToStartWhenLoadedByHand(id, r), name: $"{nameof(CheckRecipeIsReadyToStartWhenLoadedByHand)}({id})");
        }
    }

    /// <summary>With every item and the fuel of a batch in the backpack, a double-click on the card leaves the recipe ready to start.</summary>
    private async Task CheckRecipeIsReadyToStartAfterADoubleClickWithAllItsItemsCarried(string id, RecipeDef r) {
        var station = await OpenStationFor(r);
        GiveInputs(r); if (r.Burns) GiveFuel(r);
        await Frames(2);
        await DoubleClick(Named($"craft_{id}")!);
        await ExpectStarts(station, id, "after a double-click with its items and fuel in the backpack");
        // Water poured from bottles leaves them in the backpack, empty.
        foreach (var (item, amount) in r.Inputs)
            if (world.Data.VesselFor(item) is VesselDef v)
                Require(world.Backpack[v.Empty] == (amount + v.Capacity - 1) / v.Capacity && world.Carried(item) == 0, $"The bottles that poured {id}'s {item} must stay in the backpack, empty.");
        await CloseStation(station);
    }

    /// <summary>
    /// With one unit of its first item, and its fuel, a double-click leaves the recipe not ready to start, shakes the slots and names what is missing.
    /// Runs only for a recipe whose whole batch is more than a single unit.
    /// </summary>
    private async Task CheckRecipeIsNotReadyToStartAfterADoubleClickWithOneOfItsItems(string id, RecipeDef r) {
        var station = await OpenStationFor(r);
        GiveInput(r.Inputs.Keys.First(), 1); if (r.Burns) GiveFuel(r);
        await Frames(2);
        // Every item but the one carried is missing, and that one too when a batch takes more of it.
        var missing = r.Inputs.Where((p, i) => i > 0 || p.Value > 1).Select(p => world.Lower(world.Data.Name(p.Key))).ToArray();
        await ExpectRefusedDoubleClick(station, id, "with one of its items in the backpack", missing);
        await CloseStation(station);
    }

    /// <summary>With its items but no fuel in the backpack, a double-click leaves a burning recipe not ready to start, shakes the slots and says fuel is missing.</summary>
    private async Task CheckRecipeIsNotReadyToStartAfterADoubleClickWithoutFuel(string id, RecipeDef r) {
        var station = await OpenStationFor(r);
        GiveInputs(r);
        await Frames(2);
        await ExpectRefusedDoubleClick(station, id, "with its items but no fuel in the backpack", "fuel");
        await CloseStation(station);
    }

    /// <summary>With fuel but none of its items in the backpack, a double-click leaves a burning recipe not ready to start, shakes the slots and names every item missing.</summary>
    private async Task CheckRecipeIsNotReadyToStartAfterADoubleClickWithFuelAlone(string id, RecipeDef r) {
        var station = await OpenStationFor(r);
        GiveFuel(r);
        await Frames(2);
        await ExpectRefusedDoubleClick(station, id, "with only fuel in the backpack", r.Inputs.Keys.Select(item => world.Lower(world.Data.Name(item))).ToArray());
        await CloseStation(station);
    }

    /// <summary>With its fuel and items put on their slots one by one, the recipe is ready to start.</summary>
    private async Task CheckRecipeIsReadyToStartWhenLoadedByHand(string id, RecipeDef r) {
        var station = await OpenStationFor(r);
        await LoadByHand(station, id, r);
        await ExpectStarts(station, id, "once its fuel and items are put on their slots");
        await CloseStation(station);
    }
}
