namespace MasterPlayer.Mechanics.Crucible;

// f1x1 boss_b1, enabled CollisionBox/TriggerBoxShape.Box instances. Scale is half-size.
// Physical objects remain hard obstacles; no action timing or arena dimensions change.
// Native Euler composition has not been sampled: include both near-vertical planar
// projections until World transforms establish the sign. 1mm covers <0.00002rad tilt.
sealed class CrucibleB06PhysicalWalls(BossModule module) : BossComponent(module)
{
    internal readonly record struct Wall(uint InstanceId, WPos Center, float Yaw, float HalfX, float HalfZ, bool IncludeMirrored);
    internal static readonly Wall[] Walls =
    [
        new(12192201u, new(537.8406f, -412.4555f), 1.535718f, 2.555853f, 2.173077f, true),
        new(12192202u, new(502.1358f, -423.8582f), 1.5212686f, 2.589571f, 2.240487f, true),
        new(12192204u, new(502.9725f, -396.9528f), 1.570451f, 0.9862114f, 3.009569f, false),
        new(12192205u, new(501.1791f, -400.115f), 1.4770113f, 3.848804f, 1.054422f, false),
        new(12192210u, new(538.4145f, -424.4379f), 1.5357215f, 1.893905f, 1.800704f, true),
        new(12192211u, new(538.5739f, -427.7102f), 1.4327304f, 1.712599f, 1.796325f, false),
        new(12192212u, new(501.5658f, -415.885f), 1.4564799f, 1.654733f, 1.820175f, false),
        new(12192213u, new(501.9724f, -443.015f), 1.4425608f, 1.465953f, 2.265574f, true),
    ];
    private static readonly ShapeDistance[] Obstacles = Walls.SelectMany(w => w.IncludeMirrored ? new ShapeDistance[]
    {
        new SDRect(w.Center, new WDir(MathF.Sin(w.Yaw), MathF.Cos(w.Yaw)), w.HalfZ + .001f, w.HalfZ + .001f, w.HalfX + .001f),
        new SDRect(w.Center, new WDir(MathF.Sin(w.Yaw), -MathF.Cos(w.Yaw)), w.HalfZ + .001f, w.HalfZ + .001f, w.HalfX + .001f),
    } : new ShapeDistance[]
    {
        new SDRect(w.Center, new WDir(MathF.Sin(w.Yaw), MathF.Cos(w.Yaw)), w.HalfZ + .001f, w.HalfZ + .001f, w.HalfX + .001f),
    }).ToArray();
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
        => hints.TemporaryObstacles.AddRange(Obstacles);
}
