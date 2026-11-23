using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record MesoSwordLine(ulong Source, long Serial, Vector3 Next, Vector3 Advance, float Rotation, DateTime At, int Left = 51);
