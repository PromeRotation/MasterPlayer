using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record StepsEarthrisingLine(int Id, Vector3 Next, DateTime NextAt, DateTime FutureAt, int Left);
