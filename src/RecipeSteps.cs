using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

/// <summary>Steps for driving a station's page through a recipe's batch with the player's own controls.</summary>
public partial class Game {
    /// <summary>Every recipe made at a station, with its id.</summary>
    private IEnumerable<(string Id, RecipeDef Recipe)> StationRecipes => world.Data.Recipes.Where(p => p.Value.Station != null).Select(p => (p.Key, p.Value));

    /// <summary>
    /// Places a fresh station for a recipe beside an empty-handed player, carrying the recipe's tool if it needs one, and opens its page.
    /// A reinforced furnace is pumped hot with its Pump button, so heat is never what holds a batch back.
    /// </summary>
    private async Task<Thing> OpenStationFor(RecipeDef r) {
        await CloseWithEsc();
        EmptyBackpack(Hosted);
        if (r.Tool != "") Give(Hosted, world.Data.PlainestTool(r.Tool, r.Tier, r.Station));
        var station = Hosted.Add(r.Station!.Value, world.Player + new Vector2(1, 0));
        await ClickOn(station); await Frames(3);
        if (station.Kind == ThingKind.Reinforced) {
            while (station.Heat < World.SteelHeat + 12) await Click(Named("pump")!);
        }
        return station;
    }

    /// <summary>Takes a station away with its page, and empties the backpack.</summary>
    private async Task CloseStation(Thing station) {
        RemoveThings(Hosted, t => t == station); EmptyBackpack(Hosted); await CloseWithEsc();
    }

    /// <summary>Puts units of a recipe input in the backpack: a liquid in full vessels, with what is left over in one part-full vessel.</summary>
    private void GiveInput(string item, int amount) {
        if (world.Data.VesselFor(item) is not VesselDef v) { Give(Hosted, item, amount); return; }
        if (amount / v.Capacity > 0) Give(Hosted, v.Full, amount / v.Capacity);
        if (amount % v.Capacity > 0) Give(Hosted, v.States[amount % v.Capacity]);
    }

    /// <summary>Puts one batch of every input of a recipe in the backpack.</summary>
    private void GiveInputs(RecipeDef r) { foreach (var (item, amount) in r.Inputs) GiveInput(item, amount); }

    /// <summary>A fuel a recipe burns, one that is not also among its items where it has one, so fuel never stands in for an item.</summary>
    private static string FuelFor(RecipeDef r) => r.Fuels.FirstOrDefault(f => !r.Inputs.ContainsKey(f)) ?? r.Fuels[0];

    /// <summary>Puts one unit of fuel for a recipe in the backpack.</summary>
    private void GiveFuel(RecipeDef r) => Give(Hosted, FuelFor(r));

    /// <summary>The backpack cell that holds an item, found by the name its tooltip starts with.</summary>
    private ItemSlot BagCellOf(string item) => BagCell(c => c.TooltipText.StartsWith(world.Data.Name(item) + " ·", StringComparison.Ordinal));

    /// <summary>Waits for a station to begin a batch of the recipe: everything it takes was in it, so the next tick started it.</summary>
    private async Task ExpectStarts(Thing station, string recipe, string how) =>
        await Until(() => station.Processing && station.Making == recipe, $"{recipe} starting {how}", 3);

    /// <summary>
    /// Waits for a station loaded for its hard-hat recipe to begin a batch: on its own, or at a station someone works, once a worker
    /// with the recipe's tool slotted comes to it, since the player works only batches they queued.
    /// </summary>
    private async Task ExpectStartsForTheHardHat(Thing station, string recipe, RecipeDef r, string how) {
        if (!Hosted.Worked(station)) { await ExpectStarts(station, recipe, how); return; }
        if (r.Tool != "") Require(Hosted.SlotTool(station, world.Data.PlainestTool(r.Tool, r.Tier, station.Kind)), $"The {r.Tool} {recipe} takes should fit the station's slot.");
        var crafter = AddWorker(Hosted, station.Pos + new Vector2(0, 1.2f)); Hosted.Assign(crafter, station);
        await ExpectStarts(station, recipe, $"{how}, with a worker at it");
        Hosted.Fire(crafter);
    }

    /// <summary>
    /// Requires that a double-click left a station short of a batch: its slots shake at once, a new message names each thing it is missing,
    /// and several ticks later it has begun no batch.
    /// </summary>
    private async Task ExpectRefusedDoubleClick(Thing station, string id, string how, params string[] missing) {
        int posted = messages.Posted;
        await DoubleClick(Named($"craft_{id}")!);
        Require(WorkRow().Shaking, $"A double-click on {id} {how} must shake the slots.");
        await Until(() => messages.Posted > posted && messages.Last.StartsWith("Missing ", StringComparison.Ordinal) && missing.All(messages.Last.Contains),
            $"a message that {id} is missing {string.Join(", ", missing)} {how}", 2);
        await Frames(12);
        Require(!station.Processing && station.Making == "", $"{id} must not start {how}.");
    }

    /// <summary>
    /// Loads one batch of a recipe by hand: its card clicked once when the station is on another recipe, the fuel carried onto the fuel slot first,
    /// then each input carried from the backpack onto its input slot.
    /// </summary>
    private async Task LoadByHand(Thing station, string id, RecipeDef r) {
        if (station.Recipe != id) {
            await Click(Named($"craft_{id}")!);
            await Until(() => station.Recipe == id && Named($"input_{r.Inputs.Keys.First()}") != null, $"a click on {id} turning the station to it", 2);
        }
        if (r.Burns) {
            GiveFuel(r); await Frames(2);
            await Carry(BagCellOf(FuelFor(r)), Named("fuel")!);
            await Until(() => station.Burner![FuelFor(r)] == 1 && cursor.Empty, $"{FuelFor(r)} put on the fuel slot of {id}", 2);
        }
        foreach (var (item, amount) in r.Inputs) {
            GiveInput(item, amount); await Frames(2);
            string carried = world.Data.VesselFor(item) is VesselDef v ? v.States[Math.Min(amount, v.Capacity)] : item;
            await Carry(BagCellOf(carried), Named($"input_{item}")!);
            await Until(() => cursor.Empty, $"{carried} put on the input slot of {id}", 2);
        }
    }
}
