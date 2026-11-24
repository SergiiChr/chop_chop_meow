using Godot;

namespace ChopChopMeow;

/// <summary>Marks open ground for shovel work and leaves an empty hole when digging finishes.</summary>
public sealed class GroundHoles(World world) {
    public bool Mark(Vector2 at) {
        at = at.Round();
        if (DigBlock() is string needsTool) { world.Say(needsTool); return false; }
        if (world.Construction.PlacementBlock(ThingKind.PlantingSpot, at) is string blocked) { world.Say(blocked); return false; }
        if (world.Forwarded(Verb.MarkDig, at: at)) return true;
        world.Add(ThingKind.PlantingSpot, at);
        return true;
    }

    internal string? DigBlock() => world.BestTool("shovel") == null ? "Carry a shovel to dig a hole." : null;

    internal void Dig(Thing marker, float dt) {
        if (!world.Near(marker, 1.35f)) return;
        marker.Progress += dt * world.Speed(world.BestTool("shovel"));
        if (!World.DugOut(marker)) return;
        world.S.Things.Remove(marker);
        world.Add(ThingKind.Hole, marker.Pos);
        world.Play("dig_toss", marker.Pos);
    }

    internal void AtDawn() => world.S.Things.RemoveAll(ThingKind.Hole, static _ => true);

    /// <summary>A hole is saved only while empty, since planting replaces it at once.</summary>
    internal static bool Valid(Thing hole) =>
        hole.Inventory.Slots == 1 && hole.Inventory.StackSize == 1 && hole.Inventory.Capacity == 1 && hole.Inventory.Count == 0;
}
