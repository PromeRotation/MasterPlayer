using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class DoomtrainCatalog
{
    internal const uint TERRITORY = 1307;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x4A30, "Doomtrain", p => new RectangleGeometry(new(100, p.Y, 100), 0, 20, 30),
        [
            new(45618, 0, "LightningExpress", null),
            new(45619, 0, "PlasmaBeamLower", null),
            new(45620, 0, "PlasmaBeamUpper", null),
            new(45621, 0, "PlasmaBeamMedium", null),
            new(45622, 0, "PlasmaBeamShort", null),
            new(45623, 0, "UnlimitedExpressVisual", null),
            new(45624, 0, "UnlimitedExpress", null),
            new(45625, 0, "WindpipeVisual", null),
            new(45627, 0, "Blastpipe", RayRectangleTemplate.Symmetric(20, 10)),
            new(45628, 0, "TurretCrossing", null),
            new(45629, 0, "ElectrayLong", RayRectangleTemplate.Symmetric(5, 25)),
            new(45630, 0, "ElectrayUpper", RayRectangleTemplate.Symmetric(5, 10)),
            new(45631, 0, "Electray3", RayRectangleTemplate.Symmetric(5, 20)),
            new(45632, 0, "ElectrayMedium", RayRectangleTemplate.Symmetric(5, 15)),
            new(45633, 0, "ElectrayShort", RayRectangleTemplate.Symmetric(5, 10)),
            new(45634, 0, "HeadOnEmissionVisual", null),
            new(45635, 0, "ThunderousBreath", null),
            new(45636, 0, "HeadlightVisual", null),
            new(45637, 0, "Headlight", null),
            new(45638, 0, "RunawayTrain", null),
            new(45642, 0, "AetherSurgeVisual", null),
            new(45643, 0, "AetherSurge", new FanTemplate(15, MathF.PI / 4)),
            new(45645, 0, "RunawayTrainRaidwide", null),
            new(45647, 0, "Shockwave", null),
            new(45648, 0, "DerailmentSiege", null),
            new(45649, 0, "DerailmentSiegeCircle", null),
            new(45653, 0, "DerailVisual", null),
            new(45654, 0, "Derail", null),
            new(45659, 0, "HailOfThunder", new CircleTemplate(16)),
            new(45660, 0, "LightningBurstVisual", null),
            new(45661, 0, "LightningBurst", null),
            new(45667, 0, "WindpipeDrawIn", null),
            new(47527, 0, "ArcaneRevelationVisual", null),
            new(47529, 0, "BatteringArmsVisual", null),
        ],
        [45662, 45626, 45641, 45639, 45640, 45644, 45646, 45656, 45657, 45650, 45651, 45655, 47236, 47237]),
    ];
}
