using System.Numerics;
using MasterPlayer.Dodge;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record DungeonCastRule(uint Action, uint CasterOid, string Name, GeometryTemplate? Shape, string? Hint = null);
internal sealed record DungeonDefinition(uint BossOid, string Name, Func<Vector3, IGeometryShape> Arena, DungeonCastRule[] Casts, uint[] VisualActions);

internal static class ArrDungeonCatalog
{
    public static DungeonDefinition[] Create(uint territory) => territory switch
    {
        1036 => [
            new(0x19B, "Chopper", p => ArrDungeonArenas.Chopper(p.Y), [
                new(351, 0x19B, "ChargedWhisker", new CircleTemplate(6.15f))], []),
            new(0x1A1, "Denn the Orcatoothed", p => ArrDungeonArenas.Denn(p.Y), [
                new(556, 0x1A1, "Hydroball", new FanTemplate(8, MathF.PI / 2))], [])],
        1037 => [
            new(0x237, "Void Soulcounter", p => p.X > -10 ? ArrDungeonArenas.VoidFirst(p.Y)
                : p.X is > -25 and < -15 ? ArrDungeonArenas.VoidSecond(p.Y) : ArrDungeonArenas.VoidThird(p.Y), [
                new(912, 0x237, "Condemnation", new FanTemplate(7, MathF.PI / 2))], [430]),
            new(0x4C, "Galvanth the Dominator", p => new CircleGeometry(new(-52.765f, p.Y, -12.789f), 18), [
                new(987, 0x4C, "MindBlast", new CircleTemplate(9.95f))], [971])],
        1038 => [
            new(0x387C, "Kottos", p => ArrDungeonArenas.Kottos(p.Y), [
                new(28545, 0x387C, "GrandSlam", null, "Tankbuster"),
                new(28544, 0x233C, "LumberingLeap", new CircleTemplate(12)),
                new(28546, 0x387C, "ColossalSlam", new FanTemplate(30, MathF.PI / 3)),
                new(28547, 0x387C, "Catapult", null, "Single-target damage")], [28543, 28549]),
            new(0x3870, "Ichorous Ire", p => ArrDungeonArenas.Ichorous(p.Y), [
                new(28462, 0x3870, "Syrup", new CircleTemplate(4)),
                new(28461, 0x3870, "FluidSpread", null, "Tankbuster"),
                new(28465, 0x3871, "Divide", new CircleTemplate(8))], [28463, 28464]),
            new(0x38C9, "Gyges the Great", p => new CircleGeometry(new(-100, p.Y, 6), 19.5f), [
                new(28762, 0x38C9, "GiganticSwing", new DonutTemplate(4, 40)),
                new(28760, 0x38C9, "GiganticSmash", new CircleTemplate(10)),
                new(28761, 0x233C, "GiganticBlast", new CircleTemplate(8)),
                new(28764, 0x38C9, "GrandSlam", null, "Tankbuster"),
                new(28763, 0x38C9, "ColossalSlam", new FanTemplate(40, MathF.PI / 3))], [])],
        _ => []
    };

    public static IEnumerable<uint> ClaimedActions(IEnumerable<DungeonDefinition> encounters)
        => encounters.SelectMany(e => e.Casts.Select(c => c.Action).Concat(e.VisualActions)).Distinct();

    public static bool TryGetShape(uint action, out AoeShapeDescriptor? descriptor)
    {
        descriptor = action switch
        {
            351 => Circle(6.15f), 987 => Circle(9.95f), 28544 => Circle(12),
            28462 => Circle(4), 28465 or 28761 => Circle(8), 28760 => Circle(10),
            556 => Cone(8, MathF.PI / 2), 912 => Cone(7, MathF.PI / 2),
            28546 => Cone(30, MathF.PI / 3), 28763 => Cone(40, MathF.PI / 3),
            28762 => new(AoeShapeKind.Donut, 4, 40, false, 0.3f),
            _ => null
        };
        return descriptor != null;
    }

    private static AoeShapeDescriptor Circle(float radius) => new(AoeShapeKind.Circle, radius, 0, false, 0.3f);
    private static AoeShapeDescriptor Cone(float radius, float angle) => new(AoeShapeKind.Fan, radius, angle, false, 0.3f) { UseCastRotation = true };
}
