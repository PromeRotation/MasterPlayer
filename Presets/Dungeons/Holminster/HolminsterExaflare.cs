using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record HolminsterExaflare(int Id, ulong Source, Vector3 Next, Vector3 Advance, DateTime Activation, int Remaining);
