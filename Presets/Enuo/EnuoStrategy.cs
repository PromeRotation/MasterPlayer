using System.Numerics;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Enuo;

internal static class EnuoStrategy
{
    public static Vector3 Center => new(100, 0, 100);
    public static string[] Roles => ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
    public static Vector3[] Towers => [new(109.54f, 0, 76.89f), new(123.09f, 0, 90.41f),
        new(123.09f, 0, 109.54f), new(109.54f, 0, 123.09f), new(90.41f, 0, 123.09f),
        new(76.89f, 0, 109.54f), new(76.89f, 0, 90.41f), new(90.41f, 0, 76.89f)];

    public static ulong[] ResolveParty(IEnumerable<ReplayPartyMember> party)
    {
        var result = new ulong[8];
        foreach (var p in party)
        {
            var slot = Array.IndexOf(Roles, p.Role.ToUpperInvariant());
            if (slot < 0 || result[slot] != 0 || p.EntityId is 0 or 0xE0000000) return [];
            result[slot] = p.EntityId;
        }
        return result.All(id => id != 0) && result.Distinct().Count() == 8 ? result : [];
    }

    public static Vector3 Polar(float clockwiseFromNorth, float radius)
        => Center + new Vector3(MathF.Sin(clockwiseFromNorth) * radius, 0, -MathF.Cos(clockwiseFromNorth) * radius);
    public static float Angle(Vector3 p) => MathF.Atan2(p.X - 100, 100 - p.Z);
    public static float Normalize(float a) => (a % MathF.Tau + MathF.Tau) % MathF.Tau;
    // Video 03:21 / 03:35 / 03:45 uses fixed arena waymarks (A north, B east, C south, D west).
    // These assignments do not rotate with boss facing or the wild-charge safe sector.
    public static Vector3 Spread(int role, float radius = 13)
        => Polar(new float[] { 0, 4, 6, 2, 5, 3, 7, 1 }[role] * MathF.PI / 4, radius);
    public static Vector3 Pairs(int role, float radius = 10)
        => Polar(new float[] { 7, 3, 5, 1, 5, 3, 7, 1 }[role] * MathF.PI / 4, radius);
    public static Vector3 LightParty(int role, float radius = 10) => Polar(role % 2 == 0 ? -MathF.PI / 2 : MathF.PI / 2, radius);

    // BV1uKRuBkEg7 09:01–09:24: west/east; T and D1/D2 take the higher available spot.
    public static int[] AssignTowers(IReadOnlySet<int> towers, IReadOnlySet<int> marked)
    {
        if (towers.Count != 4 || marked.Count != 4 || towers.Any(i => i is < 0 or > 7)
            || marked.Any(i => i is < 0 or > 7)) return [];
        var result = Enumerable.Repeat(-1, 8).ToArray();
        for (var side = 0; side < 2; side++)
            foreach (var bait in new[] { false, true })
            {
                var players = Enumerable.Range(0, 8).Where(i => i % 2 == side && marked.Contains(i) == bait)
                    .OrderBy(i => i is 0 or 1 or 4 or 5 ? 0 : 1).ToArray();
                var spots = Enumerable.Range(0, 8).Where(i => (Towers[i].X > 100 ? 1 : 0) == side && towers.Contains(i) != bait)
                    .OrderBy(i => Towers[i].Z).ToArray();
                if (players.Length != 2 || spots.Length != 2) return [];
                for (var i = 0; i < 2; i++) result[players[i]] = spots[i];
            }
        return result;
    }

    public static ulong[] AssignOrbs(IEnumerable<ReplayActorState> orbs, float origin)
    {
        var group = orbs.ToArray();
        if (group.Length != 4 || group.Count(a => a.Oid == 0x4DC6) != 1) return [];
        return [group.Single(a => a.Oid == 0x4DC6).Id, .. group.Where(a => a.Oid == 0x4DC5)
            .OrderBy(a => Normalize(Angle(a.Position) - origin)).Select(a => a.Id)];
    }
}
