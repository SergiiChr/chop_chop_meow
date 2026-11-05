using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Loose pieces: the stones and sticks lying about the valley, and the chips a pickaxe knocks off rock and ore.</summary>
public partial class World {
    /// <summary>Chance that one pickaxe hit on a boulder or an ore deposit knocks a loose piece off it.</summary>
    public const float ChipChance = .05f;
    /// <summary>Farthest a chip lands from the middle of what it came off.</summary>
    public const float ChipReach = 2;
    private readonly Random chipping = new(9127);
    // The kinds whose spots need a tool, so a gatherer knows which tool a resource takes.
    private static readonly ThingKind[] Sources = [ThingKind.Tree, ThingKind.Iron, ThingKind.Coal, ThingKind.Tin];

    /// <summary>The loose piece a pickaxe knocks off a kind of rock, or null for anything else.</summary>
    public static ThingKind? ChipOf(ThingKind kind) => kind switch {
        ThingKind.Stone => ThingKind.LooseStone,
        ThingKind.Iron => ThingKind.IronChip,
        ThingKind.Coal => ThingKind.LooseCoal,
        ThingKind.Tin => ThingKind.TinChip,
        _ => null
    };

    /// <summary>Lays a loose stone or stick on every scenery spot that is one, unless something already stands there.</summary>
    private void ScatterLoose() {
        var points = Scenery.Points;
        for (int i = 0; i < points.Count; i++) {
            if (Scenery.Loose(i) is not ThingKind kind) continue;
            var p = points[i].At;
            if (!Scenery.Dressable(p) || S.Things.Any(t => t.Pos.DistanceTo(p) < (t.Kind == ThingKind.Tree ? .95f : t.IsResource ? 1.3f : 2.7f))) continue;
            Add(kind, p, 1);
        }
    }

    /// <summary>After a hit with a pickaxe on rock or ore, now and then drops a loose piece of it on open ground nearby.</summary>
    private void Chip(Thing rock, string? tool) {
        if (tool == null || Data.Items[tool].Tool != "pickaxe" || ChipOf(rock.Kind) is not ThingKind kind || chipping.NextDouble() >= ChipChance) return;
        for (int tries = 0; tries < 8; tries++) {
            float angle = (float)(chipping.NextDouble() * Math.Tau), distance = ChipReach * (.55f + .45f * (float)chipping.NextDouble());
            var p = rock.Pos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            if (!CanStand(p)) continue;
            Add(kind, p, 1); Play("wood_rocks_inventory", p);
            return;
        }
    }

    /// <summary>The tool spots yielding an item need, such as an axe for wood, or null when every one can be gathered by hand.</summary>
    private static string? ToolFor(string item) {
        foreach (var kind in Sources) if (ResourceItem(kind) == item) return ToolKind(kind);
        return null;
    }

    /// <summary>Whether a worker has a tool of a kind in hand or can take one from a tool station.</summary>
    private bool HasTool(Worker w, string kind) => w.Tool != "" && Data.Items[w.Tool].Tool == kind || ToolStationWith(kind, w.Pos) != null;

    /// <summary>Sends a gatherer who cannot work a spot to the nearest known loose piece of the same resource, and tells whether there was one.</summary>
    private bool LooseSpot(Worker w, Thing spot) {
        if (NearestSpot(w, ResourceItem(spot), w.Pos, false) is not Thing loose) return false;
        MoveTo(w, spot.Id, loose); w.Missing = "";
        return true;
    }
}
