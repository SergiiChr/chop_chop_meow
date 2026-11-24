using static Checks;

namespace ChopChopMeow;

public partial class Game {
    private void CheckCompactHud() {
        Require(!HasLabel(corner, "Health"), "The persistent HUD must not show health.");
        Require(!HasLabel(corner, "Food"), "The persistent HUD must not show food.");
        Require(!HasLabel(corner, "Water"), "The persistent HUD must not show water.");
        Require(!HasLabel(corner, "coins"), "The persistent HUD must not show coins.");
        Require(!HasLabel(corner, "Pack"), "The persistent HUD must not show the pack.");
    }
}
