using System.Collections.Frozen;
using MasterPlayer.Dodge;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed record BlundervilleRule(string Group, GeometryTemplate? Shape, bool Charge = false);

internal static class BlundervilleCatalog
{
    public const uint Territory = 1165, Helper = 16338, Ball = 16351, BeamActor = 9020, BeamAction = 34808,
        VisualWave = 34803, WaveStart = 34812, WaveStep = 34804;
    public const string Devices = "机关判定范围", Casts = "读条范围预警", Balls = "滚动球", Routes = "往返障碍路线", Waves = "决赛波浪", Wind = "击退范围预警";
    public static IEnumerable<string> Groups => [Devices, Casts, Balls, Routes, Waves, Wind];

    // Action sheet from the user's CN client; see docs/reference/Blunderville.spec.md.
    private static readonly FrozenDictionary<uint, BlundervilleRule> Rules = new Dictionary<uint, BlundervilleRule>
    {
        [29795] = new(Devices, new CircleTemplate(3)),
        [34716] = new(Devices, new RectangleTemplate(2, 3)),
        [34717] = new(Casts, new CircleTemplate(3)),
        [34718] = new(Devices, new RectangleTemplate(10, 4)),
        [34773] = new(Devices, new CircleTemplate(5)),
        [34774] = new(Routes, null, true),
        [34775] = new(Devices, new RectangleTemplate(3, 3)),
        [34776] = new(Balls, new CircleTemplate(2)),
        [34796] = new(Devices, new CircleTemplate(5)),
        [34797] = new(Casts, new FanTemplate(8, MathF.PI)),
        [34798] = new(Casts, new FanTemplate(8, MathF.PI)),
        [34799] = new(Devices, RayRectangleTemplate.Symmetric(4, 4)),
        [34800] = new(Wind, RayRectangleTemplate.Symmetric(12, 31)),
        [34801] = new(Devices, new CircleTemplate(6)),
        [34802] = new(Devices, new CircleTemplate(5)),
        [34805] = new(Casts, new CircleTemplate(6)),
        [BeamAction] = new(Devices, RayRectangleTemplate.Symmetric(8, 100)),
        [35409] = new(Routes, null, true),
        [VisualWave] = new(Waves, null),
        [WaveStep] = new(Waves, RayRectangleTemplate.Symmetric(6, 6)),
        [WaveStart] = new(Waves, RayRectangleTemplate.Symmetric(6, 6)),
    }.ToFrozenDictionary();

    public static IEnumerable<uint> ClaimedActions => Rules.Keys;
    public static bool TryGet(uint action, out BlundervilleRule rule) => Rules.TryGetValue(action, out rule!);
    public static bool IsActor(uint oid) => oid is Helper or 16339 or 16350 or Ball or 16352 or 16353 or BeamActor;

    public static bool TryGetShape(uint action, out AoeShapeDescriptor? descriptor)
    {
        descriptor = null;
        if (!TryGet(action, out var rule)) return false;
        descriptor = rule.Shape switch
        {
            CircleTemplate c => new(AoeShapeKind.Circle, c.Radius, 0, false, 0.3f) { ExactTemplate = c, UseSourcePosition = true },
            FanTemplate f => new(AoeShapeKind.Fan, f.Radius, f.AngleRadians, false, 0.3f)
                { ExactTemplate = f, UseCastRotation = true, UseSourcePosition = true },
            RayRectangleTemplate r => new(AoeShapeKind.RayRectangle, r.MaxLocalX - r.MinLocalX, r.Length, false, 0.3f)
                { ExactTemplate = r, UseCastRotation = true, UseSourcePosition = true },
            RectangleTemplate r => new(AoeShapeKind.Rectangle, r.Width, r.Length, false, 0.3f)
                { ExactTemplate = r, UseCastRotation = true, UseSourcePosition = true },
            _ when rule.Charge => new(AoeShapeKind.RayRectangle, 3, 0, false, 0.3f) { IsCharge = true },
            _ => null,
        };
        return descriptor != null;
    }
}
