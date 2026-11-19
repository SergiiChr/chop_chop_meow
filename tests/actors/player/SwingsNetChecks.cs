using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The way the guest steps before it swings: left on screen, away from the anvil.</summary>
    private static readonly Vector2 NetSwingAim = PlayerControl.KeyDirection(-1, 0);

    /// <summary>
    /// The network scene of a player's swings: the guest chops at wolves the host sets on it, and the host decides what
    /// the chop and the bites do.
    /// </summary>
    private NetScene SwingsNetScene => new("swings", null, CheckAGuestsFightIsDecidedOnTheHost, CheckAGuestFightsWolvesThroughTheHost);

    /// <summary>
    /// The host's half of the fight: two wolves on the guest, one where the guest's chop is aimed and one to its side.
    /// </summary>
    private async Task CheckAGuestsFightIsDecidedOnTheHost(PlayerState guest) {
        var stood = GD.StrToVar(await Signaled("fight")).AsVector2();
        await Until(() => guest.Pos.DistanceTo(stood) < .05f, "the host hearing where the guest stopped");
        // By day wolves are calmed and sent home at once.
        RunUntilNight(Hosted); ShutDens(Hosted); SetNeeds(guest, 100, 100, 100);
        var ahead = PlaceWolf(Hosted, new Wolf { Pos = guest.Pos + NetSwingAim * 1.2f, Target = guest.Id });
        var beside = PlaceWolf(Hosted, new Wolf { Pos = guest.Pos + PlayerControl.KeyDirection(0, 1) * 1.2f, Target = guest.Id });
        await Until(() => view.OtherAnimation(guest.Id) == ActorAction.SteelAxeSwing, "the host seeing the guest chop", 10);
        await Until(() => ahead.Health < Hosted.Data.WolfHealth, "the guest's chop landing on the wolf it was aimed at", 5);
        // A steel axe takes 2.2 to 3.2 off a wolf's 8.
        Require(ahead.Health is > 4.7f and < 5.9f, $"One chop of a steel axe must take 2.2 to 3.2 off the wolf ahead, which has {ahead.Health} left.");
        Require(beside.Health == 8, "A chop must miss a wolf standing to the side of it.");
        await Until(() => guest.Health <= 85, "a wolf's snap taking 15 health off a guest who stands still", 10);
        await Signaled("fought");
        RemoveWolves(Hosted, wolf => true); SetNeeds(guest, health: 100); RunUntilMorning(Hosted); NourishEveryone(Hosted);
        Signal("fight-over");
    }

    /// <summary>
    /// The guest's half of the fight: Space swings the way the guest last walked, the host decides what the chop touches,
    /// and the guest's window shows the wolf it hurt and the bite it took.
    /// </summary>
    private async Task CheckAGuestFightsWolvesThroughTheHost() {
        // Space swings the way the guest last walked: a step to the left on screen aims the chop at a wolf there.
        await Holding(Key.A, () => Frames(8));
        await Frames();
        Signal("fight", GD.VarToStr(world.Player));
        await Until(() => world.Wolves.Count == 2, "the host's two wolves showing up beside the guest", 30);
        await KeyStroke(Key.Space);
        await Until(() => world.Wolves.Any(wolf => wolf.Health is > 4.7f and < 5.9f), "the guest's window showing the wolf its chop hurt", 10);
        await Until(() => sounds.LastVariant("wolf_hit") >= 0, "the guest hearing its chop land", 5);
        await Until(() => world.Health <= 85, "the guest's window showing the bite it took", 10);
        Signal("fought");
        await Signaled("fight-over");
        await Until(() => world.Wolves.Count == 0 && world.Health == 100, "the guest's window showing the wolves gone and its health back");
    }
}
