using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class NativeFateCatalog
{
    internal const uint ARCHAEOTANIA = 818;
    internal const uint DAIVADIPA = 957;
    internal const uint FORMIDABLE = 814;
    internal const uint CHI = 960;
    internal const uint MICA = 1192;
    internal const uint TTOKRRONE = 1190;
    internal static readonly Vector3 ArchaeotaniaCenter = new(279, 0, 249);
    internal static readonly Vector3 DaivadipaCenter = new(-608, 0, 811);
    internal static readonly Vector3 ChiCenter = new(650, 0, 0);
    internal static readonly Vector3 MicaCenter = new(791, 0, 593);
    internal static readonly Vector3 TtokrroneCenter = new(53, 0, -820);
    internal static Vector3 Center(uint territory) => territory switch
    {
        CHI => ChiCenter,
        MICA => MicaCenter,
        TTOKRRONE => TtokrroneCenter,
        DAIVADIPA => DaivadipaCenter,
        _ => ArchaeotaniaCenter,
    };
    internal static Vector2 HalfSize(uint territory) => territory switch
    {
        CHI => new(29.5f),
        MICA => new(20.5f, 19.5f),
        DAIVADIPA => new(24.5f),
        _ => new(29),
    };
    internal static float PullDistanceSquared(uint territory) => territory switch
    {
        MICA => 420,
        DAIVADIPA => 625,
        _ => 900,
    };

    internal static bool IsTerritory(uint territory) => territory is ARCHAEOTANIA or DAIVADIPA or FORMIDABLE or CHI or MICA or TTOKRRONE;
    internal static bool Owns(uint territory, uint oid) => territory == ARCHAEOTANIA && oid == 0x27AD
        || territory == DAIVADIPA && oid == 0x356D
        || territory == FORMIDABLE && oid == 0x294D
        || territory == CHI && oid == 0x34CB
        || territory == MICA && oid == 0x43EB
        || territory == TTOKRRONE && oid == 0x41DF;
    internal static DungeonDefinition[] Create(uint territory) => territory == ARCHAEOTANIA
        ? [new(0x27AD, "Archaeotania", p => new RectangleGeometry(ArchaeotaniaCenter with { Y = p.Y }, 0, 58, 58),
        [
            new(16445, 0, "Blizzard Breath", new FanTemplate(60, MathF.PI / 4)),
            new(16448, 0, "Flames of the Apocalypse", new DonutTemplate(15, 30)),
            new(16447, 0, "Mind Blast", new CircleTemplate(21)),
            new(16454, 0, "Wind Slash", new CircleTemplate(8)),
            new(17089, 0, "Civilization Buster I", RayRectangleTemplate.Symmetric(15, 62)),
            new(16441, 0, "Civilization Buster II", RayRectangleTemplate.Symmetric(15, 62)),
            new(16442, 0, "Touchdown", new CircleTemplate(30)),
            new(16455, 0, "Pillar Impact", new CircleTemplate(7)),
            new(16456, 0, "Pillar Pierce", RayRectangleTemplate.Symmetric(8, 65)),
            new(17042, 0, "Headlong Rush", RayRectangleTemplate.Symmetric(10, 13.2f)),
            new(16452, 0, "Tidal Wave", null),
            new(16449, 0, "Megaflare", null),
        ], [])]
        : territory == DAIVADIPA
        ? [new(0x356D, "Daivadipa", p => new RectangleGeometry(DaivadipaCenter with { Y = p.Y }, 0, 49, 49),
        [
            new(26508, 0, "Leftward Trisula", new FanTemplate(65, MathF.PI)),
            new(26509, 0, "Rightward Parasu", new FanTemplate(65, MathF.PI)),
            new(26514, 0, "Errant Akasa", new FanTemplate(60, MathF.PI / 2)),
            new(26513, 0, "Cosmic Weave", new CircleTemplate(18)),
            new(26516, 0, "Karmic Flames", new CircleTemplate(20)),
            new(26512, 0, "Yawning Hells", new CircleTemplate(8)),
            new(26510, 0, "Drumbeat", null, "单体伤害"),
            new(26517, 0, "Infernal Redemption", null),
            new(26499, 0, "Loyal Flame Blue", null),
            new(26498, 0, "Loyal Flame Red", null),
            new(26501, 0, "Lit Path Blue", null),
            new(26500, 0, "Lit Path Red", null),
            new(26507, 0, "Burn Blue", null),
            new(26506, 0, "Burn Red", null),
            new(27080, 0, "Divine Call Backwards", null),
            new(26520, 0, "Divine Call Right", null),
            new(27079, 0, "Divine Call Forwards", null),
            new(26519, 0, "Divine Call Left", null),
        ], [])]
        : territory == FORMIDABLE
        ? [new(0x294D, "Formidable", p => new FormidableArenaBoundary(default, p),
        [
            new(17408, 0, "Spincrush", new FanTemplate(15, 2 * MathF.PI / 3)),
            new(17397, 0, "Fire Shot", new CircleTemplate(7)),
            new(17411, 0, "Explosion Grenade", new CircleTemplate(12)),
            new(17395, 0, "Fires of Mt. Gulg", null),
            new(17393, 0, "Barrage Fire", null, "Raidwide + 7 repeats after"),
            new(17394, 0, "Steam Dome", null),
            new(17401, 0, "Drill Shot", null),
        ], [])]
        : territory == CHI
        ? [new(0x34CB, "Chi", p => new RectangleGeometry(ChiCenter with { Y = p.Y }, 0, 59, 59),
        [
            new(25954, 0, "Assault Carapace", RayRectangleTemplate.Symmetric(32, 120)),
            new(25173, 0, "Assault Carapace", RayRectangleTemplate.Symmetric(32, 120)),
            new(25953, 0, "Assault Carapace", new DonutTemplate(16, 60)),
            new(25959, 0, "Fore Arms", new FanTemplate(45, MathF.PI)),
            new(26523, 0, "Fore Arms", new FanTemplate(45, MathF.PI)),
            new(25962, 0, "Rear Guns", new FanTemplate(45, MathF.PI)),
            new(26524, 0, "Rear Guns", new FanTemplate(45, MathF.PI)),
            new(25968, 0, "Free-fall Bombs", new CircleTemplate(6)),
            new(25966, 0, "Thermobaric Explosive", new CircleTemplate(25)),
            new(25972, 0, "Hellburner", null),
            new(25969, 0, "Missile Shower", null, "Raidwide x2"),
            new(25957, 0, "Carapace Fore Arms 2.0", null),
            new(25955, 0, "Carapace Fore Arms 2.0", null),
            new(25958, 0, "Carapace Rear Guns 2.0", null),
            new(25956, 0, "Carapace Rear Guns 2.0", null),
            new(25963, 0, "Rear Guns Fore Arms 2.0", null),
            new(25960, 0, "Fore Arms Rear Guns 2.0", null),
            new(25101, 0, "Bunkerbuster", null),
            new(25976, 0, "Bunkerbuster", null),
            new(27485, 0, "Bouncing Bomb", null),
            new(27486, 0, "Bouncing Bomb", null),
        ], [])]
        : territory == MICA
        ? [new(0x43EB, "Mica the Magical Mu", p => new RectangleGeometry(MicaCenter with { Y = p.Y }, 0, 41, 39),
            MicaRules(), [])]
        : territory == TTOKRRONE
        ? [new(0x41DF, "Ttokrrone", p => TtokrroneArenas.Ttokrrone(p.Y), TtokrroneRules(), [])]
        : [];

    private static DungeonCastRule[] TtokrroneRules()
    {
        var rules = new List<DungeonCastRule>
        {
            new(37327, 0, "Devour", new CircleTemplate(8)),
            new(37339, 0, "Touchdown", null, "Raidwide"),
        };
        foreach (var action in new uint[] { 37328, 37329, 37335, 37336, 37337, 37338 })
            rules.Add(new(action, 0, "Desert Tempest hit", null));
        for (uint action = 37331; action <= 37334; ++action) rules.Add(new(action, 0, "Desert Tempest", null));
        for (uint action = 37313; action <= 37316; ++action) rules.Add(new(action, 0, "Sandspout", null));
        for (uint action = 37578; action <= 37583; ++action) rules.Add(new(action, 0, "Landswallow", null));
        foreach (var action in new uint[] { 38647, 39245, 39246 }) rules.Add(new(action, 0, "Sandspheres", null));
        foreach (var action in new uint[] { 37317, 37321, 37318, 37322 }) rules.Add(new(action, 0, "Desert Dustdevil", null));
        foreach (var action in new uint[] { 39818, 39817, 39819, 39820 }) rules.Add(new(action, 0, "Desert Dustdevil hit", null));
        return [.. rules];
    }

    private static DungeonCastRule[] MicaRules()
    {
        var rules = new List<DungeonCastRule>
        {
            new(38703, 0, "Shimmerstorm", new CircleTemplate(6)),
            new(38705, 0, "Shimmerstrike", null),
            new(38707, 0, "Spark of Imagination", null, "Raidwide"),
            new(38678, 0, "Card Trick", null),
            new(39156, 0, "Card Trick Fake", null),
            new(38697, 0, "Twinkling Flourish Long", null),
            new(38698, 0, "Twinkling Flourish Short", null),
            new(38700, 0, "Twinkling Ring Long", null),
            new(38701, 0, "Twinkling Ring Short", null),
            new(38694, 0, "Double Misdirect Long", null),
            new(38695, 0, "Double Misdirect Short", null),
            new(38692, 0, "Twinkle Toss", null),
        };
        for (uint action = 38666; action <= 38677; ++action) rules.Add(new(action, 0, "Draw", null));
        for (uint action = 38996; action <= 38999; ++action) rules.Add(new(action, 0, "Rolling Starlight", null));
        return [.. rules];
    }
}
