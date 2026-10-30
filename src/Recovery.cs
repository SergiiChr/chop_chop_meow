using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Recovery controls for a fallen player without a cabin to wake in.</summary>
public sealed partial class World {
    /// <summary>Whether the local downed player may advance this shared world to dawn.</summary>
    public bool CanSkipMorning {
        get {
            if (S.Actor.RespawnIn > 0) return !HasCabin() && NoLivingNeighbors();
            return false;
        }
    }

    internal bool HasCabin() {
        foreach (var thing in S.Things) if (thing.Kind == ThingKind.Hut) return true;
        return false;
    }

    private bool NoLivingNeighbors() {
        foreach (var player in S.Players) if (player != S.Actor && player.RespawnIn <= 0) return false;
        return true;
    }

    /// <summary>Advance normal world simulation to dawn while the local player is waiting to respawn.</summary>
    public void SkipMorning() {
        if (Forwarded(Verb.SkipMorning) || !CanSkipMorning) return;
        PassNight();
    }
}
