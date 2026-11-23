using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private void StrayMap(uint index, uint state, DateTime now)
    {
        if (territory != StrayboroughCatalog.TERRITORY || Arena == null) return;
        if (Active?.BossOid == 0x4183 && index == 0 && state == 0x00020001)
        {
            straySmall = true; strayShrink = null;
            Arena = new CircleGeometry(Arena.Position, 14);
        }
        if (Active?.BossOid == 0x421F)
        {
            if (index == 0x0B && state is 0x00800040 or 0x00080004) strayCross = state == 0x00800040;
            if (index == 0x0C && state == 0x00020001) strayPlatforms = true;
            SynchronizeStray();
        }
        if (Active?.BossOid != 0x41CA || index is not (0x23 or 1)) return;
        // Map patterns compare exact current actor positions; neither cast locations nor tether-time snapshots qualify.
        for (var i = 0; i < strayCups.Count; ++i) strayCups[i] = _actors.GetValueOrDefault(strayCups[i].Id) ?? strayCups[i];
        bool Has(Vector2 p) => strayCups.Any(a => a.Position.X == p.X && a.Position.Z == p.Y);
        (Vector2 A, Vector2? B, Vector2[] Outputs)[] patterns = state switch
        {
            0x02000100 =>
            [
                (new(17, -163), new(17, -177), [new(3.5f, -161.5f), new(30.5f, -178.5f)]),
                (new(17, -153), new(10, -170), [new(25.5f, -156.5f), new(20.5f, -178.5f)]),
                (new(17, -153), new(17, -177), [new(20.5f, -178.5f), new(3.5f, -161.5f)]),
                (new(34, -170), null, [new(8.5f, -173.5f)]),
                (new(0, -170), null, [new(25.5f, -166.5f)]),
            ],
            0x10000800 =>
            [
                (new(0, -170), new(34, -170), [new(8.5f, -156.5f), new(25.5f, -183.5f)]),
                (new(0, -170), new(17, -187), [new(3.5f, -178.5f), new(8.5f, -156.5f)]),
                (new(17, -187), new(17, -153), [new(30.5f, -161.5f), new(3.5f, -178.5f)]),
            ],
            0x00400020 =>
            [
                (new(0, -170), new(17, -163), [new(5, -165), new(22, -182)]),
                (new(17, -177), new(17, -153), [new(5, -175), new(29, -175)]),
            ],
            _ => [],
        };
        var delay = state switch { 0x02000100 => 11.5, 0x10000800 => 14.5, 0x00100001 => 16, 0x00400020 => 19, _ => 0 };
        var positions = state == 0x00100001 && strayCups.Count >= 2
            ? strayCups.Take(2).Select(a => new Vector2(a.Position.X, a.Position.Z)).ToArray()
            : patterns.FirstOrDefault(p => Has(p.A) && (p.B == null || Has(p.B.Value))).Outputs ?? [];
        foreach (var p in positions)
            strayTea.Add(new($"stray/tea/{++straySerial}", "附身茶杯爆炸", new CircleTemplate(19),
                new(p.X, Arena.Position.Y, p.Y), Activation: now.AddSeconds(delay)));
    }
}
