using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private NetScene FoodEffectNetScene => new("food-effects", null, HostSeesOnlyTheGuestGetFoodEffects, GuestEatsCannedStewAndJam);

    private async Task HostSeesOnlyTheGuestGetFoodEffects(PlayerState guest) {
        Give(guest, "canned_meat_stew"); Give(guest, "canned_jam");
        await Until(() => Hosted.As(guest, () => Hosted.Hunger > 145 && Hosted.FoodEffects.SecondsLeft(FoodEffect.ProteinRecovery) > 0),
            "the server applying both effects to the player who ate the stew");
        await Until(() => Hosted.As(guest, () => Hosted.BonusHealth == 50), "the server filling the guest's bonus health after jam");
        Require(!foodEffectRow.Visible && world.BonusHealth == 0 && world.FoodEffects.SecondsLeft(FoodEffect.ProteinRecovery) == 0,
            "the host's own player should not get the guest's food effects");
    }

    private async Task GuestEatsCannedStewAndJam() {
        await CloseWithEsc();
        await Until(() => world.Backpack["canned_meat_stew"] == 1, "the guest receiving the stew");
        await KeyStroke(Key.Tab);
        await DoubleClick(BagCellOf("canned_meat_stew"));
        await Until(() => world.Hunger > 145 && foodEffectRow.Visible && world.FoodEffects.SecondsLeft(FoodEffect.ProteinRecovery) > 0,
            "both effects showing in the guest's own window");
        double left = world.FoodEffects.SecondsLeft(FoodEffect.ProteinRecovery);
        await Until(() => world.FoodEffects.SecondsLeft(FoodEffect.ProteinRecovery) < left, "the timer counting down after the first update");
        await DoubleClick(BagCellOf("canned_jam"));
        await Until(() => world.BonusHealth == 50 && needsPlate.Health.Bonus == 50,
            "the guest's window drawing the full bonus separately from normal health");
        Require(needsPlate.Health.TooltipText == $"Health: {needsPlate.Health.Level:0}% + 50% bonus.",
            "the health tooltip should show normal health followed by the bonus instead of showing the total");
        await CloseWithEsc();
    }
}
