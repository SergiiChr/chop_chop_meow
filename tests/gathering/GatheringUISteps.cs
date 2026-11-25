using System;
using System.Threading.Tasks;
using Godot;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>
    /// Puts a resource of a kind at an offset from where the player starts, holds right mouse on it until its blow is
    /// heard, and returns it with the point held and where the end of the player's tool, as <paramref name="toolEnd"/>
    /// finds it on the rig, is at that moment.
    /// A stump is of the tree <paramref name="ofTree"/> when one is named, so its trunk does not go by its own id.
    /// </summary>
    private async Task<(Thing Thing, Vector2 Press, Vector3 End)> WorkUntilBlowHeard(Vector2 start, ThingKind kind, Vector2 offset, string sound,
        Func<ActorRig, Vector3> toolEnd, int ofTree = 0) {
        Warp(Hosted, start); await Frames(3);
        var thing = Hosted.Add(kind, start + offset, kind == ThingKind.Stone ? World.StonePile : 40);
        thing.OfTree = ofTree;
        await Frames(2);
        int soundsBefore = sounds.Played;
        var press = RightPress(thing.Pos);
        await Until(() => sounds.Heard(sound, soundsBefore) && view.PlayerMark != null, $"the player working a {kind} {offset} away and its blow heard", 8);
        return (thing, press, view.PlayerRig.Root.Transform * toolEnd(view.PlayerRig));
    }

    /// <summary>
    /// Lets go of a held press, then takes away a worked resource with what is left of it and the chips it shed, and the
    /// wood and stone gathered from it beyond what the backpack held before.
    /// </summary>
    private async Task ClearWorked(Thing thing, Vector2? press, int wood, int stone) {
        if (press is Vector2 held) RightRelease(held);
        await Frames(2);
        // A rock sheds chips as it is mined, which land round it.
        RemoveThings(Hosted, t => t == thing || t.Kind == ThingKind.Stump && t.Pos == thing.Pos || t.IsLoose && t.Pos.DistanceTo(thing.Pos) < 4);
        Drop(Hosted, "wood", world.Backpack["wood"] - wood); Drop(Hosted, "stone", world.Backpack["stone"] - stone);
    }
}
