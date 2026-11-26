using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The diary in <paramref name="world"/>: each player finds its pages for themselves, and reads them back from their own diary.
/// A find stops its player with a pop-up until they take it.
/// A page lying in the world shows only to the player it is for: the one on the lake to everyone who has not taken it yet, and one fallen off a tree to the player who chopped.
/// </summary>
public sealed class Diary(World world) {
    /// <summary>Chance that one gather of a kind that hides a page turns it up for a player who has not found it yet.</summary>
    public const float FindChance = .05f;
    /// <summary>Metres from where a tree's page lands to its trunk, at least and at most.</summary>
    public const float FallNear = .8f, FallFar = 1.8f;
    /// <summary>Metres from the lake's shore the floating page drifts between, inward, all within reach of a player wading in.</summary>
    public const float FloatsFrom = .6f, FloatsTo = 3;
    private const int Seed = 7351, FloatSeed = 7352;
    // Rolls for finds; seeded, and started again with each world, so a recorded game plays the same every time.
    private Random rolls = new(Seed);

    internal void Reset() => rolls = new Random(Seed);

    /// <summary>
    /// While set, no gather turns up a page.
    /// The checks set it, since a find's pop-up stops its player and a check that gathers would stop with it; the diary's own checks leave it off.
    /// </summary>
    public bool Quiet { get; set; }

    /// <summary>Whether a thing is a page lying in the world: floating on the lake or fallen off a tree.</summary>
    public static bool IsPage(ThingKind kind) => kind is ThingKind.FloatingPage or ThingKind.FallenPage;

    /// <summary>The find that a gather of a kind can turn up, or none.</summary>
    internal static DiaryFind HiddenIn(ThingKind kind) => kind switch {
        ThingKind.Tree => DiaryFind.Tree,
        ThingKind.Berries => DiaryFind.Berries,
        ThingKind.LeafSpot => DiaryFind.Leaves,
        _ => DiaryFind.None,
    };

    /// <summary>Whether a player has found a day.</summary>
    public static bool Has(PlayerState p, int day) {
        foreach (var find in p.DiaryFinds)
            if (Array.IndexOf(DiaryFindDef.Of(find).Days, day) >= 0) return true;
        return false;
    }

    /// <summary>Whether a player has found any part of the diary.</summary>
    public static bool Started(PlayerState p) => p.DiaryFinds.Count > 0;

    /// <summary>How many days a player has found.</summary>
    public static int FoundDays(PlayerState p) {
        int found = 0;
        foreach (var day in DiaryDay.All) if (Has(p, day.Number)) found++;
        return found;
    }

    /// <summary>Whether a player has found a day and not opened it yet.</summary>
    public static bool Unread(PlayerState p, int day) => Has(p, day) && !p.DiaryRead.Contains(day);

    /// <summary>Whether a player has found a day they have not opened yet.</summary>
    public static bool AnyUnread(PlayerState p) {
        foreach (var day in DiaryDay.All) if (Unread(p, day.Number)) return true;
        return false;
    }

    /// <summary>Whether a page lying in the world shows to a player and they can pick it up.</summary>
    public static bool ShowsTo(PlayerState p, Thing t) => t.Kind switch {
        ThingKind.FloatingPage => Missing(p, DiaryFind.Lake),
        ThingKind.FallenPage => p.FallenPage == t.Id,
        _ => true,
    };

    /// <summary>Whether a find is still to come for a player: not taken, not waiting in its pop-up and not lying where it fell.</summary>
    private static bool Missing(PlayerState p, DiaryFind find) =>
        !p.DiaryFinds.Contains(find) && p.DiaryFinding != find && (find != DiaryFind.Tree || p.FallenPage == 0);

    /// <summary>
    /// One gather by the acting player of what hides <paramref name="find"/>, <paramref name="at"/> a spot: one time in twenty, it turns up.
    /// A page off a tree falls beside the trunk for the player to pick up; any other comes up in its pop-up at once.
    /// </summary>
    internal void Roll(DiaryFind find, Vector2 at) {
        var p = world.S.Actor;
        if (Quiet || !Missing(p, find) || rolls.NextDouble() >= FindChance) return;
        if (find != DiaryFind.Tree) { p.DiaryFinding = find; return; }
        for (int tries = 0; tries < 8; tries++) {
            float angle = (float)(rolls.NextDouble() * Math.Tau), distance = FallNear + (FallFar - FallNear) * (float)rolls.NextDouble();
            var spot = at + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            if (!world.CanStand(spot)) continue;
            Drop(p, spot);
            return;
        }
    }

    /// <summary>Lays a page fallen off a tree at a spot for a player, the only one it shows to.</summary>
    public Thing Drop(PlayerState p, Vector2 at) {
        var page = world.Add(ThingKind.FallenPage, at, 1);
        p.FallenPage = page.Id;
        return page;
    }

    /// <summary>Takes away each page fallen off a tree that a new building, yard or crate covers, so a later chop can drop its player's page again.</summary>
    internal void ClearUnder(Thing built) {
        foreach (var p in world.S.Players.Concat(world.S.Absent.Values))
            if (p.FallenPage != 0 && world.Find(p.FallenPage) is Thing page && World.Clears(built, page.Pos)) {
                world.S.Things.Remove(page);
                p.FallenPage = 0;
            }
    }

    /// <summary>Floats the lake's page at a spot of its own in the shallows, as a new world starts.</summary>
    internal void Float() {
        float angle = (float)(new Random(FloatSeed).NextDouble() * Math.Tau);
        world.Add(ThingKind.FloatingPage, Terrain.Lake + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Terrain.Shore(angle), 1);
    }

    /// <summary>
    /// Where the lake's page floats at a time on the world's clock: it drifts to and fro along the shore and in and out from it,
    /// always <see cref="FloatsFrom"/> to <see cref="FloatsTo"/> metres in from the shore.
    /// Its thing lies on the shore where it drifts about.
    /// </summary>
    public static Vector2 FloatAt(Thing page, float time) {
        var from = page.Pos - Terrain.Lake;
        float angle = MathF.Atan2(from.Y, from.X) + .09f * MathF.Sin(time * .011f) + .05f * MathF.Sin(time * .029f + 1.3f);
        float inward = (FloatsFrom + FloatsTo) / 2 + (FloatsTo - FloatsFrom) / 2 * (.6f * MathF.Sin(time * .017f + .4f) + .4f * MathF.Sin(time * .043f + 2.1f));
        return Terrain.Lake + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (Terrain.Shore(angle) - inward);
    }

    /// <summary>Where a page is now: the lake's where it has drifted to, any other where it lies.</summary>
    public Vector2 At(Thing page) => page.Kind == ThingKind.FloatingPage ? FloatAt(page, world.Time) : page.Pos;

    /// <summary>The acting player picks up a page in their reach that shows to them, which brings up its pop-up.</summary>
    internal void PickUp(Thing page) {
        var p = world.S.Actor;
        if (!ShowsTo(p, page)) return;
        if (page.Kind == ThingKind.FloatingPage) p.DiaryFinding = DiaryFind.Lake;
        else {
            p.DiaryFinding = DiaryFind.Tree; p.FallenPage = 0;
            world.S.Things.Remove(page);
        }
        world.PlayerActed?.Invoke(ActorAction.PickUp, At(page));
    }

    /// <summary>The acting player takes the find in their pop-up into their diary, which ends the pop-up.</summary>
    public void Take() {
        if (world.Forwarded(Verb.TakePage)) return;
        var p = world.S.Actor;
        if (p.DiaryFinding == DiaryFind.None) return;
        p.DiaryFinds.Add(p.DiaryFinding);
        p.DiaryFinding = DiaryFind.None;
    }

    /// <summary>
    /// The acting player has opened a day they found to read it.
    /// A guest sends it on whatever their copy says, since the day a find just taken opens on reaches their copy only with a later update.
    /// </summary>
    public void Read(int day) {
        if (world.Forwarded(Verb.ReadDiary, count: day)) return;
        var p = world.S.Actor;
        if (Unread(p, day)) p.DiaryRead.Add(day);
    }
}
