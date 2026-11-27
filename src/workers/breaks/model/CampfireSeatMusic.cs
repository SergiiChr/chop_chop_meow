namespace ChopChopMeow;

/// <summary>An idle worker's campfire, instrument and current solo clip, if any.</summary>
public readonly record struct CampfireSeatMusic(int Fire, CampfireInstrument Instrument, int Solo = 0, float SoloAt = 0);
