namespace ChopChopMeow;

/// <summary>A recording's file name, length and the moments a hand strikes or strums.</summary>
internal readonly record struct CampfireRecording(string Name, float Duration, float[]? Contacts = null);
