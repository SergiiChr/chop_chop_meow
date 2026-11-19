using Godot;

namespace ChopChopMeow;

/// <summary>
/// A swing a player has started that has yet to land: what they swing, the way they aim it, the damage it does give or
/// take <paramref name="Spread"/>, and the seconds until it lands.
/// </summary>
public readonly record struct Swing(ActorAction Action, Vector2 Aim, float Damage, float Spread, float In);
