using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>Opens an empty barrel from its backpack cell and waits for its water gauge.</summary>
    private Task OpenEmptyBackpackBarrel() => OpenBackpackBarrel(Barrel.Item);

    /// <summary>Opens the barrel with the given fill from its backpack cell.</summary>
    private async Task OpenBackpackBarrel(string item) {
        if (panel.Page != Page.Backpack) await KeyStroke(Key.Tab);
        await DoubleClick(BagCellOf(item));
        await Until(() => panel.Page == Page.Barrel && Named("barrel_water")?.TooltipText.Contains($"{Barrel.Liters(item)}/{Barrel.Capacity} L") == true,
            $"the {world.Data.Name(item)} opening in the backpack", 2);
    }

    /// <summary>Uses the portable barrel page's placement button and clicks a spot in reach.</summary>
    private async Task<Vector2> PutDownBackpackBarrel(string item, Func<Vector2, bool>? spotFits = null, Key? turn = null) {
        await Click(FindButton(panel, "Put it down")!);
        if (turn is Key key) await KeyStroke(key);
        var spot = SpotsNearby().First(p => world.Construction.PlacementBlock(ThingKind.Barrel, p, item) == null &&
            Uncovered(view.Screen(p)) && (spotFits == null || spotFits(p)));
        await Click(view.Screen(spot));
        return spot;
    }

    /// <summary>A barrel for a pickup check needs a clear sightline and a straight walk within reach.</summary>
    private bool CanApproachBarrel(Vector2 at) {
        var screen = view.Screen(at, .2f);
        if (!Uncovered(screen) || view.Pick(screen) != null) return false;
        var from = world.Player;
        var reach = at.MoveToward(from, 1.4f);
        int steps = Mathf.CeilToInt(from.DistanceTo(reach) / .25f);
        return Enumerable.Range(1, steps).All(i => world.CanStand(from.Lerp(reach, (float)i / steps)));
    }
}
