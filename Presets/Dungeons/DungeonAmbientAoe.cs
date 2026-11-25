using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record DungeonAmbientAoe(string Id, string Name, GeometryTemplate Shape, Vector3 Position,
    float Rotation = 0, bool Navigate = true, bool Display = true, bool OutlineOnly = false, bool Safe = false,
    DateTime? Activation = null, bool Obstacle = false, bool Unclipped = false, Vector4? Color = null, int GoalPriority = 100,
    bool OverrideDangerMargin = true, Vector3? PreferredGoalPosition = null,
    DateTime? ExpiresAt = null, bool HasMotionEnvelope = false, bool RequireOutwardExit = false);
