using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class DarknessCatalog
{
    internal const uint TERRITORY = 151;
    internal static DungeonDefinition[] Create() =>
    [
        new(0xE00, "Angra Mainyu", p => new CircleGeometry(new Vector3(-145, p.Y, 300), 30),
        [
            new(3272, 0, "DoubleVision", null, "全屏伤害"),
            new(3281, 0, "MortalGaze1", null),
            new(3275, 0, "Level100Flare1", new CircleTemplate(10)),
            new(3277, 0, "Level150Death1", new CircleTemplate(10)),
        ], [3508, 3274, 3273, 3280, 3276, 3499, 3279, 968, 1118, 3278, 3358]),
        new(0xDFC, "Five-headed Dragon", p => new CircleGeometry(new Vector3(200, p.Y, 180), 30),
        [
            new(3295, 0, "WhiteBreath", new FanTemplate(30, MathF.PI * 2 / 3)),
            new(3285, 0, "BreathOfFire", new CircleTemplate(6)),
            new(3284, 0, "BreathOfLight", new CircleTemplate(6)),
            new(3288, 0, "BreathOfPoison", new CircleTemplate(6)),
            new(3286, 0, "BreathOfIce", new CircleTemplate(6)),
            new(3289, 0, "Radiance", null, "全屏伤害"),
            new(3294, 0, "HeatWave", null, "全屏伤害"),
        ], [3355, 3290, 3287, 3282, 872, 3291, 724, 2376, 3283, 725]),
        new(0xDF6, "Cerberus", p => new RectangleGeometry(new Vector3(0, p.Y, -197), 0, 40, 80),
        [
            new(3246, 0, "TailBlow", new FanTemplate(19, MathF.PI / 2)),
            new(3241, 0, "Slabber", new CircleTemplate(8)),
            new(3249, 0, "Mini", new CircleTemplate(9)),
            new(3250, 0, "SulphurousBreath1", RayRectangleTemplate.Symmetric(6, 35)),
            new(3251, 0, "SulphurousBreath2", RayRectangleTemplate.Symmetric(6, 45)),
            new(3253, 0, "LightningBolt2", null),
            new(3247, 0, "HoundOutOfHell", null),
            new(3254, 0, "Ululation", null, "全屏伤害"),
        ], [3509, 3245, 3248, 3376, 3243, 872, 344, 1913, 3244, 3252, 3507, 3510, 3242, 1914]),
        new(0xCFA, "Cloud of Darkness", p => new CircleGeometry(new Vector3(-300, p.Y, -400), 30),
        [
            new(3297, 0, "ZeroFormParticleBeam", RayRectangleTemplate.Symmetric(24, 74)),
            new(3302, 0, "ParticleBeam2", null, "全屏伤害"),
            new(3298, 0, "FeintParticleBeam1", null),
        ], [3306, 3299, 3305, 3301, 3300, 2369]),
    ];
}
