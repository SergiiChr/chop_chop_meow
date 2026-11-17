using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The pack hunting a player in <paramref name="world"/>: each wolf howls, closes in, leaps, bites or pins, and backs off from firelight and torchlight.</summary>
public sealed class WolfHunt(World world) {
    /// <summary>How far the player a wolf runs at may move before it looks for a new way around what blocks it.</summary>
    private const float RouteSlack = 2;
    private const float FeastSpeed = 5.5f, ChaseSeconds = 3;
    /// <summary>How far from a downed player's tile each feeding wolf stands.</summary>
    private const float FeastSeat = 1.1f;

    /// <summary>Moves the pack hunting the acting player.</summary>
    internal void TickWolves(float dt, List<Wolf> hunters) {
        // A wolf that let go, such as one heading home before dawn, frees the player it pinned.
        if (world.S.Mauled && !world.Dead && !AnyPinning(hunters)) world.S.Mauled = false;
        // A sheltered player is out of reach: the pack goes to the cabin's door, or gives up and patrols on away from a cabin it leaves alone.
        if (Doorways.Indoors(world.S.Actor)) {
            var cabin = world.Find(world.S.Sheltered != 0 ? world.S.Sheltered : world.S.Actor.DoorCabin);
            foreach (var wolf in hunters) if (!world.DoorVisits.Follow(wolf, cabin)) WolfPatrols.Patrol(wolf, AwayFromPlayer(wolf).Angle());
            return;
        }
        bool lit = world.Protected(world.Player);
        // Behind a torch a lone wolf creeps in until a swing scares it off; once the pack is complete they attack together.
        bool torch = world.Torchlit, pack = torch && world.PackAround >= World.PackSize, wary = torch && !pack;
        if (pack && !world.S.Actor.PackAttack) world.Say("The pack has you surrounded. They attack!");
        world.S.Actor.PackAttack = pack;
        // A wounded player away from fire and shelter invites a leap for the throat, unless a wolf already tugs them out of a doorway.
        if (!world.Downed && world.S.Health <= World.TakedownHealth && !lit && !wary && !world.DoorTug.Tugged && !AnyLeaping(hunters)) {
            Wolf? hunter = null;
            foreach (var w in hunters)
                if (!world.Protected(w.Pos) && w.Howl <= 0 && w.Flee <= 0 && w.Stagger <= 0 && w.Pos.DistanceTo(world.Player) < World.LeapRange && (hunter == null || w.Pos.DistanceTo(world.Player) < hunter.Pos.DistanceTo(world.Player))) hunter = w;
            if (hunter != null) { hunter.Leap = World.LeapSeconds; world.Say("A wolf leaps for your throat!"); }
        }
        // A chase always outpaces a walk but not a sprint.
        float speed = world.Data.WolfTrot, chase = world.Data.WalkSpeed * world.Data.WolfChase;
        foreach (var wolf in hunters) {
            wolf.Attack -= dt;
            if (wolf.Tugging && world.DoorTug.Tug(wolf)) continue;
            if (wolf.Howl > 0) { wolf.Howl = Math.Max(0, wolf.Howl - dt); continue; }
            if (wolf.Leap > 0) {
                // Closing the remaining gap evenly makes every leap last LeapSeconds.
                var landing = world.Player + (wolf.Pos - world.Player).Normalized() * World.PinDistance;
                wolf.Pos = wolf.Pos.MoveToward(landing, wolf.Pos.DistanceTo(landing) * Math.Min(1, dt / wolf.Leap));
                wolf.Leap = Math.Max(0, wolf.Leap - dt);
                // A player in a doorway is tugged back out of it first, and pinned once out.
                if (wolf.Leap == 0 && !world.DoorTug.TugOut(wolf)) Pin(wolf, hunters.Count > 1);
            } else if (world.S.Mauled) {
                if (wolf.Feeding) continue;
                var seat = world.Player + (wolf.Pos - world.Player).Normalized() * FeastSeat;
                wolf.Pos = wolf.Pos.MoveToward(seat, dt * FeastSpeed);
                wolf.Feeding = wolf.Pos.DistanceTo(seat) < .05f;
            } else if (wolf.Stagger > 0) {
                wolf.Stagger = Math.Max(0, wolf.Stagger - dt); wolf.Chase = 0;
            } else if (wolf.Flee > 0) {
                wolf.Flee -= dt; wolf.Chase = 0;
                wolf.Pos = world.Slide(wolf.Pos, AwayFromPlayer(wolf) * dt * speed);
            } else if (wary && wolf.Scared) {
                // Each scared wolf keeps its bearing at the edge of the torchlight, so a growing pack surrounds the player.
                wolf.Chase = 0;
                var away = AwayFromPlayer(wolf);
                wolf.Pos = world.Slide(wolf.Pos, (world.Player + away * World.StalkDistance - wolf.Pos).LimitLength(dt * speed * .6f));
                if (!wolf.Growled && wolf.Pos.DistanceTo(world.Player) < 6) { wolf.Growled = true; world.Play("wolf_growl_approaching", wolf.Pos); }
            } else if (wary) {
                wolf.Chase = 0;
                world.Travel(wolf, world.DoorTug.TuggedTo, world.WolfBodies.Closing(wolf, dt * speed * .35f, dt), dt * speed * .35f, RouteSlack);
                if (!wolf.Growled && wolf.Pos.DistanceTo(world.Player) < 6) { wolf.Growled = true; world.Play("wolf_growl_approaching", wolf.Pos); }
                Bite(wolf);
            } else if ((lit || world.Protected(wolf.Pos)) is var safe && safe && wolf.Chase <= 0) {
                // Firelight turns a wolf back, unless it is already on the player's heels: then it follows in briefly.
                wolf.Chase = 0; wolf.Pos = world.Slide(wolf.Pos, AwayFromPlayer(wolf) * dt * 3);
            } else if (wolf.Chase <= 0 && wolf.Pos.DistanceTo(world.Player) > world.Data.WolfSightRange) {
                // Out of the light a wolf closes in unseen, then sprints once it is near enough to see the player.
                world.Travel(wolf, world.Player, dt * speed, dt * speed, RouteSlack);
            } else {
                if (!wolf.Sprinted && !safe) { wolf.Sprinted = true; world.Play("wolf_growl_distance", wolf.Pos); }
                wolf.Chase = safe ? wolf.Chase - dt : ChaseSeconds;
                world.Travel(wolf, world.DoorTug.TuggedTo, world.WolfBodies.Closing(wolf, dt * chase, dt), dt * chase, RouteSlack);
                if (!wolf.Growled && wolf.Pos.DistanceTo(world.Player) < 6) { wolf.Growled = true; world.Play("wolf_growl_approaching", wolf.Pos); }
                Bite(wolf);
            }
        }
        world.WolfBodies.KeepApart(hunters, dt);
        if (world.S.Mauled && !world.Dead) {
            int feeding = 0;
            foreach (var w in hunters) if (w.Feeding) feeding++;
            world.S.Health -= dt * (16 + 8 * feeding);
        }
    }
    /// <summary>Pins the acting player under a wolf, standing over them, or lunging onto their throat from <paramref name="lungeFrom"/> of it once it has tugged them back out of a doorway.</summary>
    internal void Pin(Wolf wolf, bool pack, Vector2 lungeFrom = default) {
        wolf.Leap = 0; wolf.LungeFrom = lungeFrom; wolf.Feeding = wolf.Pinning = true; world.S.Mauled = true;
        wolf.Pos = lungeFrom == Vector2.Zero ? world.Player + AwayFromPlayer(wolf) * World.PinDistance : world.Player + Doorways.OutOfDoor * DoorTug.FallenThroat + lungeFrom * DoorTug.AsideReach;
        world.Say(pack ? "You are pinned. The pack closes in." : "You are pinned."); world.Play("wolf_bite", world.Player); world.Play("takedown_scream", world.Player);
    }
    /// <summary>A wolf within reach of the acting player whose last bite is behind it bites them, and tugs them out of a doorway they are in.</summary>
    private void Bite(Wolf wolf) {
        if (wolf.Pos.DistanceTo(world.Player) >= 1.1f || wolf.Attack > 0) return;
        wolf.Attack = 1; world.S.Health -= world.Data.WolfBite; world.Play("wolf_bite", world.Player); world.DoorTug.TugOut(wolf);
    }

    /// <summary>The way a wolf backs off; one standing right on the player still has somewhere to go.</summary>
    internal Vector2 AwayFromPlayer(Wolf wolf) => wolf.Pos == world.Player ? Vector2.Right : (wolf.Pos - world.Player).Normalized();
    private static bool AnyLeaping(List<Wolf> wolves) {
        foreach (var w in wolves) if (w.Leap > 0) return true;
        return false;
    }
    private static bool AnyPinning(List<Wolf> wolves) {
        foreach (var w in wolves) if (w.Pinning) return true;
        return false;
    }
}
