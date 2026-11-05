using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Loose pieces: the stones and sticks lying about the valley, and the chips a pickaxe knocks off rock and ore.</summary>
public partial class World {
    private readonly Random chipping = new(9127);

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
        if (tool == null || Data.Items[tool].Tool != "pickaxe" || GatherRules.Of(rock.Kind)?.Chip is not ThingKind kind || chipping.NextDouble() >= GatherRules.ChipChance) return;
        for (int tries = 0; tries < 8; tries++) {
            float angle = (float)(chipping.NextDouble() * Math.Tau), distance = GatherRules.ChipReach * (.55f + .45f * (float)chipping.NextDouble());
            var p = rock.Pos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            if (!CanStand(p)) continue;
            Add(kind, p, 1); Play("wood_rocks_inventory", p);
            return;
        }
    }

    /// <summary>Whether a worker has a tool of a kind in hand or can take one from a tool station.</summary>
    private bool HasTool(Worker w, string kind) => w.Tool != "" && Data.Items[w.Tool].Tool == kind || StationsHold(kind);

    /// <summary>Sends a gatherer who cannot work a spot to the nearest known loose piece of the same resource, and tells whether there was one.</summary>
    private bool LooseSpot(Worker w, Thing spot) {
        if (NearestSpot(w, ResourceItem(spot), w.Pos, false) is not Thing loose) return false;
        MoveTo(w, spot.Id, loose); w.Missing = "";
        return true;
    }
}
