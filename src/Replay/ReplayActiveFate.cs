using System.Numerics;

namespace MasterPlayer.Replay;

// Null in older recordings means the original current-FATE input is unknown.
// A present value with Id=0 explicitly records that there is no current FATE.
public sealed record ReplayActiveFate(uint Id, Vector3 Center, float Radius);
