using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool vanaspatiSmall;
    private DungeonAmbientAoe? vanaspatiRing;
    private readonly HashSet<ulong> vanaspatiBubbleSeen = [];
    private readonly HashSet<ulong> vanaspatiBubbles = [];
    private uint vanaspatiPattern;
    private readonly List<DungeonAmbientAoe> vanaspatiStripes = [];
    private readonly List<DungeonAmbientAoe> vanaspatiCosmic = [];
    private long vanaspatiSerial;

    public bool RequireVanaspatiMisdirection => territory == VanaspatiCatalog.TERRITORY && Active?.BossOid == 0x33E8
        && _actors.TryGetValue(PlayerId, out var player) && new uint[] { 1422, 2936, 3694, 3909 }.Any(s => HasStatus(player, s));

    private void SynchronizeVanaspati()
    {
        if (territory != VanaspatiCatalog.TERRITORY || Active?.BossOid != 0x33E9) return;
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x3731))
            if (vanaspatiBubbleSeen.Add(actor.Id)) vanaspatiBubbles.Add(actor.Id);
        vanaspatiBubbles.RemoveWhere(id => !_actors.ContainsKey(id));
    }

    private void VanaspatiCast(DungeonPendingCast cast)
    {
        if (territory != VanaspatiCatalog.TERRITORY || Arena == null) return;
        if (cast.Rule.Action == 25153 && !vanaspatiSmall)
            vanaspatiRing = new("vanaspati/ring", "场地即将缩小", new DonutTemplate(20, 30),
                Arena.Position, Activation: cast.FinishAt.AddSeconds(.7));
        if (cast.Rule.Action == 27847) vanaspatiBubbles.Remove(cast.Source);
    }

    private void VanaspatiFinish(DungeonPendingCast cast)
    {
        if (territory == VanaspatiCatalog.TERRITORY && cast.Rule.Action == 25170 && !vanaspatiSmall && Arena != null)
        {
            Vector2[][] strips =
            [
                [new(-25, -25), new(25, -25), new(25, -20), new(-25, -20)],
                [new(-25, 20), new(25, 20), new(25, 25), new(-25, 25)],
                [new(-25, -20), new(-20, -20), new(-20, 20), new(-25, 20)],
                [new(20, -20), new(25, -20), new(25, 20), new(20, 20)],
            ];
            vanaspatiRing = new("vanaspati/ring", "场地即将缩小", new DungeonShapeTemplate(new PolygonRegionGeometry(Arena.Position, strips, [], strips)),
                Arena.Position, Activation: cast.FinishAt.AddSeconds(8.9));
        }
    }

    private void VanaspatiMap(uint index, uint state, DateTime time)
    {
        if (territory != VanaspatiCatalog.TERRITORY || Arena == null) return;
        if (index == 6 && state == 0x00020001 && Active?.BossOid == 0x33E9)
        {
            vanaspatiSmall = true; vanaspatiRing = null; Arena = VanaspatiArenas.WreckerSmall(Arena.Position.Y);
        }
        if (Active?.BossOid != 0x33EB) return;
        if (index == 7 && state == 0x00020001)
        {
            vanaspatiSmall = true; vanaspatiRing = null; Arena = new RectangleGeometry(Arena.Position, 0, 40, 40);
        }
        if (index == 8) vanaspatiPattern = state;
        if (state != 0x00020001 || vanaspatiCosmic.Count != 0 || index is not (0x0A or 0x0B)) return;
        int[][] sets = index == 0x0A ? [[0, 2, 3], [0, 1, 3], [1, 2, 3]] : [[0, 1, 3], [0, 2, 3], [0, 1, 2]];
        double[] delays = [4.3, 9.5, 14.6];
        for (var wave = 0; wave < 3; ++wave)
            foreach (var lane in sets[wave])
                vanaspatiCosmic.Add(new($"vanaspati/cosmic/{++vanaspatiSerial}", "连续天体直线",
                    RayRectangleTemplate.Symmetric(10, 50), QuantizedDungeonPosition(new(320, Arena.Position.Y, -142 - lane * 10)),
                    VanaspatiCatalog.WEST, Activation: time.AddSeconds(delays[wave])));
    }

    private void VanaspatiEffect(uint action, DateTime time)
    {
        if (territory != VanaspatiCatalog.TERRITORY || Active?.BossOid != 0x33EB || Arena == null) return;
        if (action == 25374 && vanaspatiCosmic.Count > 0) vanaspatiCosmic.RemoveAt(0);
        if (action is 25162 or 25163) { vanaspatiStripes.Clear(); vanaspatiPattern = 0; }
        if (action is not (25164 or 25165)) return;
        var blue = action == 25165;
        int[] lanes = vanaspatiPattern switch
        {
            0x00400020 => blue ? [1, 2] : [0, 3],
            0x00020001 => blue ? [2, 3] : [0, 1],
            0x00100008 => blue ? [0, 3] : [1, 2],
            0x01000080 => blue ? [0, 1] : [2, 3],
            _ => [],
        };
        foreach (var lane in lanes)
            vanaspatiStripes.Add(new($"vanaspati/stripe/{++vanaspatiSerial}", "红蓝条带",
                RayRectangleTemplate.Symmetric(10, 40), QuantizedDungeonPosition(new(280, Arena.Position.Y, -142 - lane * 10)),
                VanaspatiCatalog.EAST, Activation: time.AddSeconds(7.7)));
    }

    private void ResetVanaspati()
    {
        vanaspatiSmall = false; vanaspatiRing = null; vanaspatiBubbleSeen.Clear(); vanaspatiBubbles.Clear();
        vanaspatiPattern = 0; vanaspatiStripes.Clear(); vanaspatiCosmic.Clear(); vanaspatiSerial = 0;
    }
}
