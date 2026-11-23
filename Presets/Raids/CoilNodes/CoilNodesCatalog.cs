using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class CoilNodesCatalog
{
    internal const uint FIRST = 241;
    internal const uint SECOND = 242;

    internal static DungeonDefinition[] Create(uint territory)
    {
        if (territory == FIRST)
            return [new(0x887, "T01 ADS", p => Rect(p.Y, -3, 27, 7, 28),
            [
                new(1447, 0, "HighVoltage", null, "高压电流：打断"),
                new(1448, 0, "RepellingCannons", new CircleTemplate(8.3f)),
                new(1450, 0, "PiercingLaser", RayRectangleTemplate.Symmetric(6, 32.3f)),
                new(1378, 0, "DirtyCannons", new CircleTemplate(5.15f)),
                new(1220, 0, "GravityField", null),
            ], [1215, 1225, 1449, 1228, 1229]), CaduceusCatalog.Create()];
        return
        [
            Node(0x7D9, "T02 ADS", 0, 77, 18, 13, laser: true, fire: true, ballast: true, gravity: true, rot: true),
            Node(0x7DA, "Quarantine Node", 0, 112, 14, 13, rot: true),
            Node(0x7DB, "Attack Node", -44, 94, 17, 17, gravity: true),
            Node(0x7DC, "Sanitary Node", -43, 52, 18, 15, ballast: true),
            Node(0x7DD, "Monitoring Node", 0, 39, 17, 15),
            Node(0x7DE, "Defense Node", 46, 52, 17, 14),
            Node(0x7DF, "Disposal Node", 41, 94, 14, 20, fire: true),
        ];
    }

    private static DungeonDefinition Node(uint oid, string name, float x, float z, float halfWidth, float halfLength,
        bool laser = false, bool fire = false, bool ballast = false, bool gravity = false, bool rot = false)
    {
        List<DungeonCastRule> rules =
        [
            new(1216, 0, "HighVoltage", null, "高压电流：打断"),
            new(1217, 0, "RepellingCannons", new CircleTemplate(8.3f)),
        ];
        if (laser) rules.Add(new(1227, 0, "PiercingLaser", RayRectangleTemplate.Symmetric(6, 32.3f)));
        if (fire) rules.Add(new(675, 0, "Firestream", RayRectangleTemplate.Symmetric(6, 35.5f)));
        if (ballast)
        {
            rules.Add(new(1343, 0, "Ballast1", new FanTemplate(5.5f, MathF.PI * 1.5f)));
            rules.Add(new(1222, 0, "Ballast2", new PtDonutSectorTemplate(5.5f, 10.5f, MathF.PI * 1.5f)));
            rules.Add(new(1223, 0, "Ballast3", new PtDonutSectorTemplate(10.5f, 15.5f, MathF.PI * 1.5f)));
        }
        if (gravity) rules.Add(new(1220, 0, "GravityField", null));
        if (rot) rules.Add(new(1218, 0, "AllaganRot", null));
        return new(oid, name, p => Rect(p.Y, x, z, halfWidth, halfLength), rules.ToArray(),
            [1215, 1412, 1415, 1224, 1225, 1315, 1226, 1221, 1219, 1228, 1229]);
    }

    private static RectangleGeometry Rect(float y, float x, float z, float halfWidth, float halfLength)
        => new(new Vector3(x, y, z), 0, halfWidth * 2, halfLength * 2);
}
