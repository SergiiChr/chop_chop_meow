using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunHotbarUIChecks() {
        await TestClean(CheckHotbarRightClickAndDoubleClickUseItsItem);
        await TestClean(CheckDoubleClickTakesAToolOffTheHotbarInHand);
        await TestClean(CheckRightClickOnOpenGroundUsesTheSelectedHotbarItem);
    }

    /// <summary>Number keys and slot clicks select items, and right-clicking open ground eats or drinks the selection.</summary>
    private async Task CheckRightClickOnOpenGroundUsesTheSelectedHotbarItem() {
        Require(hotbar.Buttons.Count == 9 && world.HotbarSlots.Count == 9, "The bottom toolbar must show nine saved hotbar references.");
        // Earlier checks drop and give berries, which empties a slot bound to a dropped stack, so the slot waits for berries again here.
        SetHotbarSlot(Hosted, 7, "berries");
        await Until(() => world.HotbarStack(7) >= 0, "the hotbar's eighth slot binding the berries carried", 2);
        int berries = world.Backpack["berries"];
        await KeyStroke(Key.Key3);
        Require(player.Mode.Placing == ThingKind.Chest, "A building-plan hotkey must enter placement mode.");
        await KeyStroke(Key.Key8);
        Require(world.ActiveHotbar == 7, "Key 8 must make the eighth hotbar slot active.");
        Require(world.Backpack["berries"] == berries, "Key 8 must not eat the berries.");
        Require(hotbar.Buttons[7].GetThemeStylebox("normal") == Style.SlotWanted, "Key 8 must outline the selected berries.");
        Require(hotbar.Buttons[2].GetThemeStylebox("normal") != Style.SlotWanted, "Key 8 must outline only the selected slot.");
        Require(player.Mode.Placing == null, "Selecting an item must clear any previously selected building plan.");
        int played = sounds.Played;
        await ClickGround();
        Require(world.Backpack["berries"] == berries, "A left click on open ground must leave the selected berries in the backpack.");
        await ClickGround(MouseButton.Right);
        Require(world.Backpack["berries"] == berries - 1, "A right click on open ground must eat one selected berry.");
        Require(sounds.Played == played + 1 && sounds.Last == "eat_berry", "Eating must play only the eating sound, not an inventory sound as well.");
        // The ninth slot drinks only from the bottle bound to it, so the bottles carried go aside and the slot waits for the full one given here.
        var bottles = Enumerable.Range(0, Water.Capacity + 1).Select(liters => (liters, DropVessels(Hosted, Water.Empty, Water.Liquid, liters))).ToArray();
        SetHotbarSlot(Hosted, 8, Water.Full); GiveWater(Hosted); SetNeeds(Hosted, thirst: 20);
        await Until(() => !hotbar.Buttons[8].Disabled && world.HotbarStack(8) >= 0, "the hotbar showing the water", 2);
        await Click(hotbar.Buttons[8]);
        await Until(() => world.ActiveHotbar == 8, "a click selecting the hotbar slot once no second press follows", 2);
        Require(Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 1, "Clicking a hotbar slot must only select it.");
        await ClickGround(MouseButton.Right);
        Require(Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 0 && world.Thirst > 20, "A right click on open ground must drink the selected water.");
        foreach (var (liters, count) in bottles) if (count > 0) GiveVessel(Hosted, Water.Empty, Water.Liquid, liters, count);
        await Frames();
    }

    /// <summary>A double click on a tool in the backpack that no hotbar slot holds takes it in hand with no slot selected, until a number key selects a slot.</summary>
    private async Task CheckDoubleClickTakesAToolOffTheHotbarInHand() {
        await CloseWithEsc();
        int active = world.ActiveHotbar;
        await KeyStroke(Key.Key3);
        await CloseWithEsc();
        Give(Hosted, "steel_shovel", 1);
        Require(Enumerable.Range(0, 9).All(i => world.HotbarItem(i) != "steel_shovel"), "The default hotbar must not hold a shovel for this check.");
        await KeyStroke(Key.Tab);
        await DoubleClick(BagCell(c => c.TooltipText.StartsWith("Steel shovel")));
        await Until(() => world.InHand == "steel_shovel" && hotbar.ActiveItem() == "steel_shovel", "a double click on the shovel taking it in hand", 2);
        Require(Enumerable.Range(0, 9).All(i => world.HotbarItem(i) != "steel_shovel"), "Taking a tool in hand must not put it on the hotbar.");
        await CloseWithEsc();
        Drop(Hosted, "steel_shovel");
        await Until(() => world.InHand == "" && hotbar.ActiveItem() == null, "the hand emptying once the shovel leaves the backpack", 2);
        Give(Hosted, "steel_shovel", 1); await Frames();
        Require(world.InHand == "", "A shovel coming back must not go back in hand by itself.");
        await KeyStroke(Key.Key2);
        await Until(() => world.InHand == "" && world.ActiveHotbar == 1 && hotbar.ActiveItem() == world.HotbarItem(1), "key 2 selecting its slot instead of the shovel in hand", 2);
        Drop(Hosted, "steel_shovel"); world.SelectHotbar(active); await Frames();
    }

    /// <summary>Both right-click and double left-click use a hotbar item without clearing the slot.</summary>
    private async Task CheckHotbarRightClickAndDoubleClickUseItsItem() {
        string before = world.HotbarItem(7);
        int carried = Drop(Hosted, "berries");
        Give(Hosted, "berries", 2); SetHotbarSlot(Hosted, 7, "berries"); SetNeeds(Hosted, hunger: 50);
        await Until(() => world.Backpack["berries"] == 2 && !hotbar.Buttons[7].Disabled, "the hotbar showing the two berries", 2);
        await RightClick(hotbar.Buttons[7]);
        await Until(() => world.Backpack["berries"] == 1, "a right-click on the berries' slot eating one", 2);
        Require(world.HotbarItem(7) == "berries", "Using the slot must leave the remaining berries bound.");
        await DoubleClick(hotbar.Buttons[7]);
        await Until(() => world.Backpack["berries"] == 0, "a double left-click on the berries' slot eating the other berry", 2);
        Require(world.HotbarItem(7) == "berries", "An empty stack must leave its hotbar slot waiting for berries.");
        world.SetHotbar(7, before); Give(Hosted, "berries", carried); await Frames();
    }
}
