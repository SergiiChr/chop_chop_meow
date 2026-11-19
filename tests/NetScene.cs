using System;
using System.Threading.Tasks;

namespace ChopChopMeow;

/// <summary>
/// One feature's scene in the network session, played by the host and the guest side by side.
/// <paramref name="Prepare"/> sets the host's world up before the guest joins, so that joining has to bring it over.
/// <paramref name="Host"/> is the host's half, given the guest's player, and <paramref name="Guest"/> the guest's.
/// Both sides finish a scene before either starts the next.
/// </summary>
internal sealed record NetScene(string Name, Action? Prepare, Func<PlayerState, Task> Host, Func<Task> Guest);
