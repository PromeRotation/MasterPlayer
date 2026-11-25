using System.Numerics;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed record PalaceKnockbackProjection(ulong Source, Vector3 From, Vector3 To, bool Unsafe);
