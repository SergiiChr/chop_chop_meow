namespace ChopChopMeow;

/// <summary>How far down the arm a sleeve reaches.</summary>
public enum Sleeves { None, Short, Long }

/// <summary>
/// The colours a person is drawn in: skin, hair, the top, the hips,
/// the legs and the shoes, and how long the sleeves are.
/// Legs without a colour are bare.
/// </summary>
public readonly record struct Outfit(string Skin, string Hair, string Top, string Hips, string? Legs, string Shoes, Sleeves Sleeves);
