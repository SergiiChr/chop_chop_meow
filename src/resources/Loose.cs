using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Loose pieces: the stones and sticks lying about the valley, and the pieces tools knock off trees, rock and ore.</summary>
public partial class World {
    private readonly Random chipping = new(9127);

    /// <summary>Lays a loose stone or stick on every scenery spot that is one, unless something already stands there.</summary>
    private void ScatterLoose() {
        var points = Scenery.Points;
        for (int i = 0; i < points.Count; i++) {
            if (Scenery.Loose(i) is not ThingKind kind) continue;
            var p = points[i].At;
            if (FreeSpot(p)) Add(kind, p, 1);
        }
    }

    /// <summary>Whether a scenery spot is free for a thing to lie or grow on: ground that scenery may stand on, away from where players start and clear of what is already there.</summary>
    private bool FreeSpot(Vector2 p) => Scenery.Dressable(p) && p.DistanceTo(Terrain.Spawn) >= 4.5f && !S.Things.Any(t => t.Pos.DistanceTo(p) < (t.Kind == ThingKind.Tree ? .95f : t.IsResource ? 1.3f : 2.7f));

    /// <summary>Now and then a hit with the tool a resource needs drops a loose piece on open ground nearby.</summary>
    internal void Chip(Thing source, string? tool) {
        if (tool == null || GatherRules.Of(source.Kind) is not { Chip: ThingKind kind } rule
            || Data.Items[tool].Tool != rule.Needs || chipping.NextDouble() >= GatherRules.ChipChance) return;
        if (StandSpotNear(chipping, source.Pos, GatherRules.ChipReach * .55f, GatherRules.ChipReach) is not Vector2 p) return;
        Add(kind, p, 1); Play("wood_rocks_inventory", p);
    }

    /// <summary>Whether a worker has a tool of a kind in hand or can take one from a tool station.</summary>
    internal bool HasTool(Worker w, string kind) => w.Tool != "" && Data.Items[Item.KindOf(w.Tool)].Tool == kind || StationsHold(kind);

    /// <summary>
    /// Sends a gatherer who cannot work a spot to the nearest known spot of the same job that they can, and tells whether there was one:
    /// a woodcutter with no axe to be had digs up stumps or picks up sticks.
    /// </summary>
    internal bool OtherSpot(Worker w, Thing spot) {
        if (NearestSpot(w, spot.Kind, w.Pos) is not Thing other) return false;
        MoveTo(w, spot.Id, other); w.Missing = "";
        return true;
    }
}
