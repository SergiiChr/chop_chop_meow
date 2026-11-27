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
    /// <summary>The steel furnace shows one slot per input and output, loads held stacks put on its input slots, and Space pumps its bellows. Returns the furnace.</summary>
    private async Task<Thing> CheckSteelFurnaceSlotsLoadHeldStacksThenPump() {
        var furnace = Hosted.Add(ThingKind.Reinforced, world.Player + new Vector2(1, 0));
        Give(Hosted, "iron_bar", 5); Give(Hosted, "coal", 100);
        await ClickOn(furnace);
        Require(Named("input_iron_bar") != null, "The steel furnace must show a slot for iron bars.");
        Require(Named("input_coal") != null, "The steel furnace must show a slot for coal.");
        Require(Named("output_steel_bar") != null, "The steel furnace must show a slot for steel bars.");
        await ShowBackpack();
        await Carry(BagCell(c => c.TooltipText.StartsWith("Iron bar")), Named("input_iron_bar")!);
        await Until(() => furnace.Inventory["iron_bar"] == 5 && world.Backpack["iron_bar"] == 0 && cursor.Empty, "held bars put on the matching input slot loading", 2);
        await Carry(BagCell(c => c.TooltipText.StartsWith("Coal")), Named("input_iron_bar")!);
        await Frames(4);
        Require(furnace.Inventory["coal"] == 0 && !cursor.Empty, "An input slot must refuse a different item and leave it held.");
        Require(((ItemSlot)Named("input_iron_bar")!).Shaking, "An input slot that refuses a held stack must shake.");
        await RightClick(Named("input_coal")!);
        await Until(() => furnace.Inventory["coal"] == 1, "a right click with coal held putting one in its input slot", 2);
        await KeyStroke(Key.Escape);
        await PickUpHalf(Named("input_iron_bar")!);
        await Click(BagCell(c => c.TooltipText == ""));
        await Until(() => furnace.Inventory["iron_bar"] == 2 && world.Backpack["iron_bar"] == 3, "half the input slot's bars going back to the backpack", 2);
        await KeyStroke(Key.Space);
        Require(view.PlayerAnimation == ActorAction.Pump, "Manual bellows input must trigger its dedicated cycle.");
        await Until(() => view.PlayerAnimation != ActorAction.Pump, "the bellows action pulse finishing before pause checks", 3);
        return furnace;
    }
}
