namespace ChopChopMeow;

/// <summary>How far down the arm a sleeve reaches.</summary>
public enum Sleeves { None, Short, Long }

/// <summary>
/// The colours a person is drawn in, skin, hair, top, hips, legs and shoes, and how long the sleeves are.
/// Legs without a colour are bare above any socks. With thigh-high socks, the leg colour is used for tailored shorts.
/// </summary>
public readonly record struct Outfit(string Skin, string Hair, string Top, string Hips, string? Legs, string Shoes, Sleeves Sleeves, string? Socks = null);
