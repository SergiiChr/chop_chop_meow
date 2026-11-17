namespace ChopChopMeow;

/// <summary>
/// A stack a player holds on the cursor.
/// Its units stay in their cell, <see cref="Slot"/> of the inventory that <see cref="Box"/> names as <see cref="World.Locate"/> does, and nobody else can take them.
/// </summary>
public readonly record struct HeldStack(int Box, int Slot, string Item, int Count);
