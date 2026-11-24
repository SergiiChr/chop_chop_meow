using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Tracks a held dig until the player finishes it and leaves a hole in the ground.</summary>
public sealed class GroundHoles(World world) {
    internal const float Reach = 1.35f;

    private string SelectedTool => world.InHand != "" ? world.InHand : world.HotbarItem(world.ActiveHotbar);
    private bool HasShovel(string item) => world.ToolKind(item) == "shovel" && world.Backpack[item] > 0;

    internal string? Block(Vector2 at) {
        if (!HasShovel(SelectedTool)) return "Select a shovel to dig.";
        if (!float.IsFinite(at.X) || !float.IsFinite(at.Y) || !Terrain.OnMap(Terrain.TileAt(at))) return "Choose dry, open ground.";
        return world.Construction.PlacementBlock(ThingKind.PlantingSpot, at);
    }

    /// <summary>Starts a fresh dig once the player reaches the tile.</summary>
    public bool Begin(Vector2 at, int attempt) {
        at = at.Round();
        if (attempt <= 0 || world.Player.DistanceTo(at) > Reach) return false;
        if (Block(at) is string blocked) { world.Say(blocked); return false; }
        if (world.Forwarded(Verb.BeginDig, count: attempt, at: at)) return true;
        var player = world.S.Actor;
        Clear(player);
        player.DigAttempt = attempt;
        player.DiggingTile = Terrain.TileIndex(Terrain.TileAt(at)) + 1;
        return true;
    }

    internal void Work(float dt) {
        var player = world.S.Actor;
        if (player.DigAttempt == 0) return;
        string tool = SelectedTool;
        if (!HasShovel(tool) || world.Occupied || world.S.Sheltered != 0
            || world.Player.DistanceTo(Position(player)) > Reach) { Clear(player); return; }
        player.Busy = true;
        if (player.DigReady) return;
        player.DigWork += dt * world.Speed(tool);
        player.DigReady = player.DigWork >= GatherRules.DigSeconds;
    }

    public bool Ready(int attempt) => attempt > 0 && world.S.Actor.DigAttempt == attempt && world.S.Actor.DigReady;

    /// <summary>Finishes a dig after the server reports enough work and the player is still holding the mouse.</summary>
    public bool Finish(int attempt) {
        if (!Ready(attempt)) return false;
        if (world.Forwarded(Verb.FinishDig, count: attempt)) return true;
        var player = world.S.Actor;
        var at = Position(player);
        Clear(player);
        if (world.Player.DistanceTo(at) > Reach) return false;
        if (Block(at) is string blocked) { world.Say(blocked); return false; }
        world.Add(ThingKind.Hole, at);
        world.Play("dig_toss", at);
        return true;
    }

    public void Cancel(int attempt) {
        if (attempt <= 0 || world.Forwarded(Verb.CancelDig, count: attempt)) return;
        if (world.S.Actor.DigAttempt == attempt) Clear(world.S.Actor);
    }

    internal static void Clear(PlayerState player) {
        player.DigAttempt = player.DiggingTile = 0;
        player.DigWork = 0;
        player.DigReady = false;
    }

    private static Vector2 Position(PlayerState player) {
        int index = player.DiggingTile - 1;
        return new Vector2(index % Terrain.Size, index / Terrain.Size);
    }

    /// <summary>The point the shovel animation aims at while digging open ground.</summary>
    internal static Thing? AnimationTarget(PlayerState player) {
        if (player.DigAttempt == 0) return null;
        var target = player.DigSubject ??= new Thing { Kind = ThingKind.PlantingSpot };
        target.Pos = Position(player);
        return target;
    }

    internal void Reset() {
        foreach (var player in world.Players) Clear(player);
        foreach (var player in world.S.Absent.Values) Clear(player);
        // A dig outline is a preview, so old outlines and their fog memories disappear when the world opens.
        world.S.Things.RemoveAll(ThingKind.PlantingSpot, static _ => true);
        foreach (int id in world.S.Remembered.Where(pair => pair.Value.Kind == ThingKind.PlantingSpot).Select(pair => pair.Key).ToArray())
            world.S.Remembered.Remove(id);
    }

    internal void AtDawn() => world.S.Things.RemoveAll(ThingKind.Hole, static _ => true);

    /// <summary>A hole is saved only while empty, since planting replaces it at once.</summary>
    internal static bool Valid(Thing hole) =>
        hole.Inventory.Slots == 1 && hole.Inventory.StackSize == 1 && hole.Inventory.Capacity == 1 && hole.Inventory.Count == 0;
}
