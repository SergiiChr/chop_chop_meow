using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The pack hunting a player in <paramref name="world"/>: each wolf howls, closes in, leaps, bites or pins, and backs off from firelight and torchlight.</summary>
public sealed class WolfHunt(World world) {
    /// <summary>How far the player a wolf runs at may move before it looks for a new way around what blocks it.</summary>
    private const float RouteSlack = 2;
    private const float FeastSpeed = 5.5f;
    private const float ThreatenSeconds = .48f;
    private const float CircleSeconds = .75f, CircleClearance = .65f;

    /// <summary>The share of its trot a wolf creeps in at behind torchlight.</summary>
    private const float CreepPace = .35f;
    /// <summary>
    /// Metres a second above which a wolf's body faces the way it goes, closing in and following; slower, as when it backs
    /// off, it faces its player.
    /// </summary>
    private const float FacesItsWayFrom = 1.5f, FollowsItsWayFrom = .3f;
    /// <summary>How far from a downed player's tile each feeding wolf stands.</summary>
    private const float FeastSeat = 1.1f;

    /// <summary>Whether a saved wolf's snarl and circling clocks are within the times used in a fight.</summary>
    internal static bool ValidSavedReaction(Wolf wolf) =>
        wolf.Threaten is >= 0 and <= ThreatenSeconds && wolf.Circle is >= -CircleSeconds and <= CircleSeconds &&
        wolf.Recover is >= 0 and <= WolfSnap.MissRecovery;

    internal float RunSpeed => world.Data.WalkSpeed * world.Data.WolfChase;

    /// <summary>The first hit turns a surviving wolf on its attacker, if it hasn't got prey to let go of.</summary>
    internal bool ReactToHit(Wolf wolf, bool burnt) {
        if (wolf.Aggressive) return false;
        wolf.Aggressive = true;
        wolf.Threaten = burnt ? 0 : ThreatenSeconds;
        wolf.Howl = wolf.HowlIn = 0;
        if (wolf.Follow == FollowStage.Routed) wolf.Flee = 0;
        wolf.Follow = FollowStage.Bold;
        if (wolf.Mode != WolfMode.Home && !world.Downed && !Doorways.Indoors(world.S.Actor) &&
            !wolf.Tugging && !wolf.Pinning && wolf.Side == 0) {
            wolf.Target = world.S.Actor.Id;
            bool outside = wolf.DoorCheck == DoorCheckStage.None && world.S.Actor.Door == Doorway.None;
            wolf.EndDoorCheck();
            if (outside && !burnt && world.WolfCounter.Start(wolf, world.Player)) {
                wolf.Threaten = 0;
                return true;
            }
        }
        return false;
    }

    /// <summary>Holds a wolf in place through the stagger and the stance that follows the first hit.</summary>
    internal bool RecoverFromHit(Wolf wolf, float dt) {
        if (wolf.Stagger > 0) {
            wolf.Stagger = Math.Max(0, wolf.Stagger - dt); wolf.Galloping = false;
            if (wolf.Stagger == 0 && wolf.Threaten > 0) {
                wolf.Growled = wolf.Sprinted = true;
                world.Play("wolf_growl_approaching", wolf.Pos);
            }
            return true;
        }
        if (wolf.Threaten <= 0) return false;
        wolf.Threaten = Math.Max(0, wolf.Threaten - dt);
        return true;
    }

    /// <summary>Picks the side to run around the player after landing short, giving way to a nearby obstacle.</summary>
    internal void CircleOnMiss(Wolf wolf) {
        var away = AwayFromPlayer(wolf);
        int way = (wolf.Id & 1) == 0 ? 1 : -1;
        if (!world.CanStand(wolf.Pos + away.Rotated(way * Mathf.Pi / 4) * CircleClearance)) way = -way;
        wolf.Circle = way * CircleSeconds;
        wolf.Route.Clear();
    }

    /// <summary>
    /// Runs diagonally out from the player and curves around them before coming back in for the next bite.
    /// </summary>
    private void Circle(Wolf wolf, float dt, float speed) {
        float left = Math.Abs(wolf.Circle), way = Math.Sign(wolf.Circle);
        float turn = Mathf.Lerp(Mathf.Pi / 4, Mathf.Pi * .63f, 1 - left / CircleSeconds);
        var delta = AwayFromPlayer(wolf).Rotated(way * turn) * (dt * speed);
        wolf.Pos = world.Slide(wolf.Pos, delta);
        wolf.Circle = way * Math.Max(0, left - dt);
    }
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
        var fire = world.FireLighting(world.Player);
        bool torch = world.Torchlit, pack = (fire != null || torch) && world.FullPackAt(fire?.Pos ?? world.Player);
        var packHold = fire != null ? pack ? LightHold.ChargesThrough : LightHold.CirclesFire
            : torch && !pack ? LightHold.HeldByTorch : LightHold.None;
        if (pack && !world.S.Actor.PackAttack) world.Say("The pack has you surrounded. They attack!");
        world.S.Actor.PackAttack = pack;
        if (packHold is LightHold.CirclesFire or LightHold.HeldByTorch)
            world.WolfFollow.Arrange(hunters, packHold == LightHold.CirclesFire ? fire : null);
        // A wounded player invites a leap for the throat. While light holds the pack back, only a wolf that turned on them leaps.
        if (!world.Downed && world.S.Health <= World.TakedownHealth && !world.DoorTug.Tugged && !AnyLeaping(hunters)) {
            Wolf? hunter = null;
            var mark = world.WolfSnap.BitePosition();
            float closest = World.LeapRange * World.LeapRange;
            foreach (var wolf in hunters) {
                float distance = wolf.Pos.DistanceSquaredTo(mark);
                if (distance >= closest || WolfSnap.Active(wolf) || wolf.Attack > 0 || wolf.Follow == FollowStage.Routed ||
                    HoldOf(wolf, packHold) is var hold && (hold is LightHold.CirclesFire or LightHold.HeldByTorch
                        || hold == LightHold.None && world.Protected(wolf.Pos)) ||
                    wolf.Howl > 0 || wolf.HowlIn > 0 || wolf.Flee > 0 || wolf.Stagger > 0 ||
                    wolf.Threaten > 0 || wolf.Recover > 0 || wolf.Circle != 0 || WolfCounter.Active(wolf)) continue;
                hunter = wolf; closest = distance;
            }
            if (hunter != null && world.WolfSnap.Commit(hunter, mark, throat: true))
                world.Say("A wolf gathers to leap for your throat!");
        }
        // A chase always outpaces a walk but not a sprint.
        float speed = world.Data.WolfTrot, chase = RunSpeed;
        foreach (var wolf in hunters) {
            var from = wolf.Pos;
            wolf.Attack = Math.Max(0, wolf.Attack - dt);
            var hold = HoldOf(wolf, packHold);
            Hunt(wolf, dt, hold, speed, chase);
            Face(wolf, from, dt, KeepsRound(wolf, hold));
        }
        world.WolfBodies.KeepApart(hunters, dt, packHold == LightHold.CirclesFire);
        if (world.S.Mauled && !world.Dead) {
            int feeding = 0;
            foreach (var w in hunters) if (w.Feeding) feeding++;
            world.S.Health -= dt * (16 + 8 * feeding);
        }
    }
    /// <summary>What a torch or a lit campfire by its player makes a hunting wolf do this tick.</summary>
    private enum LightHold {
        /// <summary>No light holds it back, though it still goes round a campfire in its way.</summary>
        None,
        /// <summary>It circles the campfire's light at its edge.</summary>
        CirclesFire,
        /// <summary>The torch keeps it in follow mode or creeping in until a swing scares it off.</summary>
        HeldByTorch,
        /// <summary>A full pack, or a wolf that turned on its player, goes straight through the light.</summary>
        ChargesThrough
    }

    /// <summary>
    /// A wolf that turned on its player charges through any light.
    /// The rest do what the light at their player makes the pack do.
    /// </summary>
    private static LightHold HoldOf(Wolf wolf, LightHold packHold) => wolf.IgnoresFire ? LightHold.ChargesThrough : packHold;

    /// <summary>Whether the wolf goes round in follow mode: round a campfire, or round its player behind a torch.</summary>
    private static bool KeepsRound(Wolf wolf, LightHold hold) =>
        hold == LightHold.CirclesFire || hold == LightHold.HeldByTorch && wolf.Following;

    /// <summary>
    /// Moves one wolf hunting the acting player through this tick: at a doorway, howling, in the air, feeding, reeling,
    /// snapping, running off, or closing in as the light and the torch let it.
    /// </summary>
    private void Hunt(Wolf wolf, float dt, LightHold hold, float speed, float chase) {
        // A wolf stands braced only for as long as follow mode keeps finding its player close.
        bool braced = wolf.Follow == FollowStage.Braced;
        if (braced) wolf.Follow = FollowStage.Circling;
        if (wolf.Tugging && world.DoorTug.Tug(wolf) || world.DoorTug.Hold(wolf)) return;
        if (WolfCounter.Active(wolf)) {
            if (world.Downed || world.DoorTug.Tugged) WolfCounter.Clear(wolf);
            else world.WolfCounter.Tick(wolf, dt);
            return;
        }
        if (RecoverFromHit(wolf, dt)) return;
        if (wolf.HowlIn > 0) {
            wolf.HowlIn = Math.Max(0, wolf.HowlIn - dt);
            if (wolf.HowlIn == 0) world.WolfPatrols.Howl(wolf);
            return;
        }
        if (wolf.Howl > 0) { wolf.Howl = Math.Max(0, wolf.Howl - dt); return; }
        if (hold == LightHold.CirclesFire && WolfSnap.Winding(wolf)) wolf.Snap = wolf.Leap = 0;
        if (world.S.Mauled) {
            wolf.Snap = wolf.Leap = wolf.Recover = wolf.Circle = 0;
            if (wolf.Feeding) return;
            var seat = world.Player + (wolf.Pos - world.Player).Normalized() * FeastSeat;
            wolf.Pos = wolf.Pos.MoveToward(seat, dt * FeastSpeed);
            wolf.Feeding = wolf.Pos.DistanceTo(seat) < .05f;
        } else if (WolfSnap.Active(wolf)) {
            world.WolfSnap.Tick(wolf, dt);
        } else if (wolf.Recover > 0) {
            wolf.Recover = Math.Max(0, wolf.Recover - dt);
        } else if (wolf.Flee > 0) {
            // A burnt wolf runs off for a moment; one routed gallops until the player has lost sight of it.
            wolf.Flee -= dt; wolf.Galloping = false;
            float pace = wolf.Aggressive || wolf.Follow == FollowStage.Routed ? chase : speed;
            wolf.Pos = world.Slide(wolf.Pos, AwayFromPlayer(wolf) * dt * pace);
            if (wolf.Follow == FollowStage.Routed && (wolf.Flee <= 0 || !world.Sees(wolf.Pos))) world.WolfFollow.GiveUp(wolf);
        } else if (KeepsRound(wolf, hold)) {
            wolf.Circle = 0;
            world.WolfFollow.Follow(wolf, braced, dt, speed * WolfPatrols.PatrolPace);
            if (hold != LightHold.CirclesFire) Growl(wolf);
        } else if (hold != LightHold.ChargesThrough && world.WolfFollow.SkirtFire(wolf, world.WolfSnap.BitePosition(),
            dt * (hold == LightHold.HeldByTorch ? speed * CreepPace : wolf.Aggressive || wolf.Galloping ? chase : speed))) {
            wolf.Circle = 0;
        } else if (hold == LightHold.HeldByTorch) {
            wolf.Galloping = false;
            Close(wolf, dt * speed * CreepPace, dt);
            Growl(wolf);
            world.WolfSnap.Start(wolf);
        } else if (wolf.Circle != 0) {
            wolf.Galloping = true;
            Circle(wolf, dt, chase);
        } else if (!wolf.Aggressive && !wolf.Galloping
            && wolf.Pos.DistanceTo(world.Player) > world.Data.WolfSightRange) {
            // Out of the light a wolf closes in unseen, then sprints once it is near enough to see the player.
            world.Travel(wolf, world.Player, dt * speed, dt * speed, RouteSlack);
        } else {
            if (!wolf.Sprinted) { wolf.Sprinted = true; world.Play("wolf_growl_distance", wolf.Pos); }
            wolf.Galloping = true;
            Close(wolf, dt * chase, dt);
            Growl(wolf);
            world.WolfSnap.Start(wolf);
        }
    }

    /// <summary>Takes a wolf up to <paramref name="step"/> metres toward biting distance of the acting player, round what is in its way.</summary>
    private void Close(Wolf wolf, float step, float dt) {
        var at = world.DoorTug.Tugged ? world.DoorTug.TuggedTo : world.WolfSnap.BitePosition();
        float direct = WolfBodies.Closing(wolf, at, step, dt);
        if (wolf.Aggressive && direct < 0 && !world.DoorTug.Tugged) {
            // Make room to leap at a run when the player comes inside reach.
            var from = wolf.Pos;
            var away = from == at ? Vector2.Right : (from - at).Normalized();
            var retreat = away * Math.Min(step, WolfBodies.StandOff - from.DistanceTo(at));
            wolf.Pos = world.Slide(from, retreat);
            if (wolf.Pos == from) {
                // Something is behind it. Run outward along the first side that has room.
                float turn = (wolf.Id & 1) == 0 ? Mathf.Pi / 3 : -Mathf.Pi / 3;
                wolf.Pos = world.Slide(from, retreat.Rotated(turn));
                if (wolf.Pos == from) wolf.Pos = world.Slide(from, retreat.Rotated(-turn));
            }
        } else world.Travel(wolf, at, direct, step, RouteSlack);
    }

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
        if (WolfCounter.Active(wolf) || wolf.Stagger > 0 || wolf.Threaten > 0 || WolfSnap.Active(wolf)
            || wolf.Recover > 0 || wolf.Pinning) return;
        var went = wolf.Pos - from;
        // A following wolf walks round its player, slower than that, and still faces its way.
        float pace = following ? FollowsItsWayFrom : FacesItsWayFrom;
        var toward = went.LengthSquared() > dt * dt * pace * pace ? went : world.Player - wolf.Pos;
        if (toward.LengthSquared() > 1e-8f) wolf.Heading = toward.Angle();
    }

    /// <summary>Pins the acting player under a wolf, standing over them, or lunging onto their throat from <paramref name="lungeFrom"/> of it once it has tugged them back out of a doorway.</summary>
    internal void Pin(Wolf wolf, Vector2 lungeFrom = default) {
        wolf.Leap = wolf.Snap = wolf.Recover = 0; wolf.LungeFrom = lungeFrom;
        wolf.Feeding = wolf.Pinning = true; world.S.Mauled = true;
        if (lungeFrom != Vector2.Zero)
            wolf.Pos = world.Player + world.Doorways.OutOfDoorAt(world.S.Actor.DoorCabin) * DoorTug.FallenThroat
                + lungeFrom * DoorTug.AsideReach;
        world.Say("You are pinned and can't move."); world.Play("wolf_bite", world.Player); world.Play("takedown_scream", world.Player);
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
