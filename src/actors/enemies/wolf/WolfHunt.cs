using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The pack hunting a player in <paramref name="world"/>: each wolf howls, closes in, leaps, bites or pins, and backs off from firelight and torchlight.</summary>
public sealed class WolfHunt(World world) {
    /// <summary>How far the player a wolf runs at may move before it looks for a new way around what blocks it.</summary>
    private const float RouteSlack = 2;
    private const float FeastSpeed = 5.5f, ChaseSeconds = 3;
    /// <summary>The share of its trot a wolf creeps in at behind torchlight.</summary>
    private const float CreepPace = .35f;
    /// <summary>
    /// Metres a second above which a wolf's body faces the way it goes, closing in and following; slower, as when it backs
    /// off, it faces its player.
    /// </summary>
    private const float FacesItsWayFrom = 1.5f, FollowsItsWayFrom = .3f;
    /// <summary>How far from a downed player's tile each feeding wolf stands.</summary>
    private const float FeastSeat = 1.1f;

    /// <summary>Moves the pack hunting the acting player.</summary>
    internal void TickWolves(float dt, List<Wolf> hunters) {
        // A wolf that let go, such as one heading home before dawn, frees the player it pinned.
        if (world.S.Mauled && !world.Dead && !AnyPinning(hunters)) world.S.Mauled = false;
        // A sheltered player is out of reach: the pack goes to the cabin's door, or gives up and patrols on away from a cabin it leaves alone.
        if (Doorways.Indoors(world.S.Actor)) {
            var cabin = world.Find(world.S.Sheltered != 0 ? world.S.Sheltered : world.S.Actor.DoorCabin);
            foreach (var wolf in hunters) if (!world.DoorCheck.Follow(wolf, cabin)) WolfPatrols.Patrol(wolf, AwayFromPlayer(wolf).Angle());
            return;
        }
        bool lit = world.Protected(world.Player);
        // Behind a torch a lone wolf creeps in until a swing scares it off; once the pack is complete they attack together.
        bool torch = world.Torchlit, pack = torch && world.PackAround >= World.PackSize, wary = torch && !pack;
        if (pack && !world.S.Actor.PackAttack) world.Say("The pack has you surrounded. They attack!");
        world.S.Actor.PackAttack = pack;
        if (wary) world.WolfFollow.Arrange(hunters);
        // A wounded player away from fire and shelter invites a leap for the throat, unless a wolf already tugs them out of a doorway.
        // Behind a torch only a wolf that stood its ground leaps.
        if (!world.Downed && world.S.Health <= World.TakedownHealth && !lit && !world.DoorTug.Tugged && !AnyLeaping(hunters)) {
            Wolf? hunter = null;
            foreach (var w in hunters)
                if (!Held(w, wary) && w.Follow != FollowStage.Routed && !world.Protected(w.Pos) && w.Howl <= 0 && w.HowlIn <= 0 && w.Flee <= 0 && w.Stagger <= 0 && w.Pos.DistanceTo(world.Player) < World.LeapRange && (hunter == null || w.Pos.DistanceTo(world.Player) < hunter.Pos.DistanceTo(world.Player))) hunter = w;
            if (hunter != null) { hunter.Leap = World.LeapSeconds; world.Say("A wolf leaps for your throat!"); }
        }
        // A chase always outpaces a walk but not a sprint.
        float speed = world.Data.WolfTrot, chase = world.Data.WalkSpeed * world.Data.WolfChase;
        foreach (var wolf in hunters) {
            var from = wolf.Pos;
            wolf.Attack -= dt;
            bool held = Held(wolf, wary);
            Hunt(wolf, dt, hunters.Count > 1, lit, held, speed, chase);
            Face(wolf, from, dt, held && wolf.Following);
        }
        world.WolfBodies.KeepApart(hunters, dt);
        if (world.S.Mauled && !world.Dead) {
            int feeding = 0;
            foreach (var w in hunters) if (w.Feeding) feeding++;
            world.S.Health -= dt * (16 + 8 * feeding);
        }
    }
    /// <summary>
    /// Moves one wolf hunting the acting player through this tick: at a doorway, howling, in the air, feeding, reeling,
    /// snapping, running off, or closing in as the light and the torch let it.
    /// </summary>
    private void Hunt(Wolf wolf, float dt, bool pack, bool lit, bool held, float speed, float chase) {
        // A wolf stands braced only for as long as follow mode keeps finding its player close.
        bool braced = wolf.Follow == FollowStage.Braced;
        if (braced) wolf.Follow = FollowStage.Circling;
        if (wolf.Tugging && world.DoorTug.Tug(wolf) || world.DoorTug.Hold(wolf)) return;
        if (wolf.HowlIn > 0) {
            wolf.HowlIn = Math.Max(0, wolf.HowlIn - dt);
            if (wolf.HowlIn == 0) world.WolfPatrols.Howl(wolf);
            return;
        }
        if (wolf.Howl > 0) { wolf.Howl = Math.Max(0, wolf.Howl - dt); return; }
        // A leap for the throat, or a player already down, ends a snap the wolf had begun.
        if (wolf.Leap > 0 || world.S.Mauled) wolf.Snap = 0;
        if (wolf.Leap > 0) {
            // Closing the remaining gap evenly makes every leap last LeapSeconds.
            var landing = world.Player + (wolf.Pos - world.Player).Normalized() * World.PinDistance;
            wolf.Pos = wolf.Pos.MoveToward(landing, wolf.Pos.DistanceTo(landing) * Math.Min(1, dt / wolf.Leap));
            wolf.Leap = Math.Max(0, wolf.Leap - dt);
            // A player in a doorway is tugged back out of it first, and pinned once out.
            if (wolf.Leap == 0 && !world.DoorTug.TugOut(wolf)) Pin(wolf, pack);
        } else if (world.S.Mauled) {
            if (wolf.Feeding) return;
            var seat = world.Player + (wolf.Pos - world.Player).Normalized() * FeastSeat;
            wolf.Pos = wolf.Pos.MoveToward(seat, dt * FeastSpeed);
            wolf.Feeding = wolf.Pos.DistanceTo(seat) < .05f;
        } else if (wolf.Stagger > 0) {
            wolf.Stagger = Math.Max(0, wolf.Stagger - dt); wolf.Chase = 0;
        } else if (wolf.Snap > 0) {
            // Gathering itself, a wolf still keeps on its player; in the air it goes where it leapt for.
            if (WolfSnap.Winding(wolf)) Close(wolf, dt * (held ? speed * CreepPace : chase), dt);
            world.WolfSnap.Tick(wolf, dt);
        } else if (wolf.Flee > 0) {
            // A burnt wolf trots off for a moment; one routed gallops until the player has lost sight of it.
            wolf.Flee -= dt; wolf.Chase = 0;
            wolf.Pos = world.Slide(wolf.Pos, AwayFromPlayer(wolf) * dt * (wolf.Follow == FollowStage.Routed ? chase : speed));
            if (wolf.Follow == FollowStage.Routed && (wolf.Flee <= 0 || !world.Sees(wolf.Pos))) world.WolfFollow.GiveUp(wolf);
        } else if (held && wolf.Following) {
            world.WolfFollow.Follow(wolf, braced, dt, speed * WolfPatrols.PatrolPace);
            Growl(wolf);
        } else if (held) {
            wolf.Chase = 0;
            Close(wolf, dt * speed * CreepPace, dt);
            Growl(wolf);
            world.WolfSnap.Start(wolf);
        } else if ((lit || world.Protected(wolf.Pos)) is var safe && safe && wolf.Chase <= 0) {
            // Firelight turns a wolf back, unless it is already on the player's heels: then it follows in briefly.
            wolf.Chase = 0; wolf.Pos = world.Slide(wolf.Pos, AwayFromPlayer(wolf) * dt * 3);
        } else if (wolf.Chase <= 0 && wolf.Pos.DistanceTo(world.Player) > world.Data.WolfSightRange) {
            // Out of the light a wolf closes in unseen, then sprints once it is near enough to see the player.
            world.Travel(wolf, world.Player, dt * speed, dt * speed, RouteSlack);
        } else {
            if (!wolf.Sprinted && !safe) { wolf.Sprinted = true; world.Play("wolf_growl_distance", wolf.Pos); }
            wolf.Chase = safe ? wolf.Chase - dt : ChaseSeconds;
            Close(wolf, dt * chase, dt);
            Growl(wolf);
            world.WolfSnap.Start(wolf);
        }
    }

    /// <summary>Whether a torch that makes the pack <paramref name="wary"/> holds this wolf back: every one but a wolf that turned on its player.</summary>
    private static bool Held(Wolf wolf, bool wary) => wary && wolf.Follow != FollowStage.Bold;

    /// <summary>Takes a wolf up to <paramref name="step"/> metres toward biting distance of the acting player, round what is in its way.</summary>
    private void Close(Wolf wolf, float step, float dt) => world.Travel(wolf, world.DoorTug.TuggedTo, world.WolfBodies.Closing(wolf, step, dt), step, RouteSlack);

    /// <summary>A wolf closing in growls once as it comes near.</summary>
    private void Growl(Wolf wolf) {
        if (!wolf.Growled && wolf.Pos.DistanceTo(world.Player) < 6) { wolf.Growled = true; world.Play("wolf_growl_approaching", wolf.Pos); }
    }

    /// <summary>
    /// Turns a wolf's body the way it went this tick when it ran, and to its player while it stands, creeps or backs off.
    /// One reeling from a blow keeps facing the way it did, and one snapping the way it leapt.
    /// <paramref name="following"/> tells that it is in follow mode: scared, with a torch holding it back.
    /// </summary>
    private void Face(Wolf wolf, Vector2 from, float dt, bool following) {
        if (wolf.Stagger > 0 || WolfSnap.InAir(wolf)) return;
        var went = wolf.Pos - from;
        // A following wolf walks round its player, slower than that, and still faces its way.
        float pace = following ? FollowsItsWayFrom : FacesItsWayFrom;
        var toward = went.LengthSquared() > dt * dt * pace * pace ? went : world.Player - wolf.Pos;
        if (toward.LengthSquared() > 1e-8f) wolf.Heading = toward.Angle();
    }

    /// <summary>Pins the acting player under a wolf, standing over them, or lunging onto their throat from <paramref name="lungeFrom"/> of it once it has tugged them back out of a doorway.</summary>
    internal void Pin(Wolf wolf, bool pack, Vector2 lungeFrom = default) {
        wolf.Leap = 0; wolf.LungeFrom = lungeFrom; wolf.Feeding = wolf.Pinning = true; world.S.Mauled = true;
        wolf.Pos = lungeFrom == Vector2.Zero ? world.Player + AwayFromPlayer(wolf) * World.PinDistance : world.Player + Doorways.OutOfDoor * DoorTug.FallenThroat + lungeFrom * DoorTug.AsideReach;
        world.Say(pack ? "You are pinned. The pack closes in." : "You are pinned."); world.Play("wolf_bite", world.Player); world.Play("takedown_scream", world.Player);
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
