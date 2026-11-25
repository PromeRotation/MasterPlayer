using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureFlowersRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static DungeonActor Player => new(100, 0, Center, PartySlot: 0, Player: true, Hostile: false);
    private static DungeonEncounter Create(uint oid)
    {
        var encounter = new DungeonEncounter(1279) { TreasurePartyInput = [100, 101] };
        encounter.Synchronize([new(1, oid, Center, InCombat: true), Player,
            Player with { Id = 101, PartySlot = 1, Position = Center + Vector3.UnitX * 10 },
            .. Enumerable.Range(2, 40).Select(i => new DungeonActor((ulong)i, 0x233C, Center))],
            Center, Start, playerId: 100);
        return encounter;
    }
    private static DungeonCastObservation Cast(uint action, double finish = 5, Vector3? location = null, float rotation = .37f,
        ulong target = 100, double start = 0)
        => new(action, location ?? Center, rotation, Start.AddSeconds(start), Start.AddSeconds(finish), target);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure Paeonia17 Spirit18 active source rules and territory ownership", Rules);
        run("native treasure Paeonia independent opposite concentric sequences source location and consumption clocks", Concentric);
        run("native treasure Paeonia expected8 crossing first4 promotion and locked independent clocks", Crossing);
        run("native treasure Spirit two independent expected groups first4 and first3 danger promotion", Groups);
        run("native treasure Spirit created beams all6 quantized original location7.7 all clear only Finish", Beams);
        run("native treasure Spirit target circle6 Finish only retains dead target and freezes destroyed reference", Bait);
        run("native treasure Paeonia gaze10000 source location PR facing and true Finish", Gaze);
        run("native treasure flower rewards seedlings lifecycle colors priorities and excluded portal", Rewards);
    }
    private static void Rules()
    {
        var p = Create(0x48AE).Active!.Casts.ToDictionary(c => c.Action);
        var s = Create(0x48B5).Active!.Casts.ToDictionary(c => c.Action);
        Check(p.Count == 17 && s.Count == 18 && NativeTreasureCatalog.Owns(0x48AE, 1279)
            && NativeTreasureCatalog.Owns(0x48B5, 1279) && !NativeTreasureCatalog.Owns(0x48B5, 1209)
            && p[43704].Shape is CircleTemplate { Radius: 6 }
            && p[43706].Shape is FanTemplate { Radius: 40, AngleRadians: MathF.PI / 4 }
            && Enumerable.Range(32301, 5).All(a => p[(uint)a].Shape is CircleTemplate { Radius: 7 })
            && s[43773].Shape is RayRectangleTemplate { Length: 20, MinLocalX: -5, MaxLocalX: 5 }
            && s[43778].Shape is RayRectangleTemplate { Length: 20, MinLocalX: -10, MaxLocalX: 10 }
            && s[43776].Shape is FanTemplate { Radius: 30, AngleRadians: MathF.PI / 6 }
            && !p.ContainsKey(43717) && !s.ContainsKey(43787), "Active source shape or ownership differs.");
    }
    private static void Concentric()
    {
        var e = Create(0x48AE);
        var origin = Center + new Vector3(.13f, 0, .17f);
        e.StartCast(2, Cast(43710, 5, origin), Start);
        e.StartCast(3, Cast(43713, 7, origin + Vector3.UnitX * 5), Start);
        Check(e.TreasureIceSequences.Count == 2 && e.TreasureIceSequences[0].Origin == origin
            && e.TreasureIceSequences[0].Activation == Start.AddSeconds(5)
            && e.AmbientAoes().Single(a => a.Id == "treasure/ice/1").Shape is CircleTemplate { Radius: 10 }
            && e.AmbientAoes().Single(a => a.Id == "treasure/ice/2").Shape is DonutTemplate { InnerRadius: 10, OuterRadius: 30 },
            "Initial concentric location, clock or order differs.");
        e.Tick(Start.AddSeconds(20));
        e.Finish(2, 43710, Start.AddSeconds(20));
        Check(e.TreasureIceSequences[0].Order == 1 && e.TreasureIceSequences[0].Activation == Start.AddSeconds(22)
            && e.TreasureIceSequences[1].Order == 0, "First Finish failed independent consumption clock+2.");
        e.StartCast(4, Cast(43712, 23, origin + new Vector3(1, 0, 1), rotation: 2), Start.AddSeconds(21));
        e.Finish(4, 43712, Start.AddSeconds(23));
        Check(e.TreasureIceSequences.Count == 1 && e.TreasureIceSequences[0].Cast.Rule.Action == 43713,
            "Cross-source axis1/default-angle matching failed.");
        e.Tick(Start.AddSeconds(30));
        e.Finish(3, 43713, Start.AddSeconds(30));
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/ice/")).Shape is CircleTemplate { Radius: 10 }
            && e.TreasureIceSequences[0].Activation == Start.AddSeconds(32), "Reverse sequence second circle differs.");
    }
    private static void Crossing()
    {
        var e = Create(0x48AE);
        for (var i = 0; i < 7; ++i) e.StartCast((ulong)i + 2, Cast(i % 2 == 0 ? 43706u : 43707u, 20 - i), Start);
        Check(e.TreasureVaultGroups[0].Source == 2 && e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/vault-group/")) == 4,
            "Crossing sorted before expected8 or showed hidden fifth.");
        e.StartCast(9, Cast(43707, 6), Start);
        Check(e.TreasureVaultGroups[0].Source == 9 && e.TreasureVaultGroups[^1].Source == 2,
            "Crossing expected8 activation ordering differs.");
        e.Finish(9, 43707, Start.AddSeconds(100));
        Check(e.TreasureVaultGroups.Count == 7 && e.TreasureVaultGroups[0].Source == 8
            && e.TreasureVaultGroups[0].FinishAt == Start.AddSeconds(14), "Hidden promotion lost independent clock.");
    }
    private static void Groups()
    {
        var e = Create(0x48B5);
        for (var i = 0; i < 8; ++i) e.StartCast((ulong)i + 2, Cast(i % 2 == 0 ? 43773u : 43774u, 20 - i), Start);
        for (var i = 0; i < 4; ++i) e.StartCast((ulong)i + 20, Cast((uint)(43778 + i), 10 - i), Start);
        var aoes = e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/vault-group/") || a.Id.StartsWith("treasure/high-voltage/")).ToArray();
        Check(e.TreasureVaultGroups.Count == 8 && e.TreasureHighVoltage.Count == 4 && aoes.Length == 7
            && aoes.Count(a => a.Navigate) == 7
            && aoes.Single(a => a.Id == "treasure/high-voltage/23").Color == new Vector4(1, 1, 0, 1)
            && !aoes.Any(a => a.Id == "treasure/high-voltage/20"), "Independent expected groups, all Risky or firstDanger differs.");
        e.Finish(23, 43781, Start.AddSeconds(20));
        Check(e.AmbientAoes().Any(a => a.Id == "treasure/high-voltage/20")
            && e.TreasureVaultGroups.Count == 8, "HighVoltage hidden promotion polluted Electray.");
        e.Finish(22, 43780, Start.AddSeconds(21));
        e.Finish(21, 43779, Start.AddSeconds(22));
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/high-voltage/")).Color == null,
            "Last HighVoltage retained Danger color.");
    }
    private static void Beams()
    {
        var e = Create(0x48B5);
        for (var i = 0; i < 6; ++i) e.ObjectCreated((ulong)i + 50, Start.AddSeconds(i), 0x48B6,
            Center + new Vector3(i + .13f, 0, .17f), .5f + i);
        var beams = e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/beam/")).ToArray();
        Check(beams.Length == 6 && beams.All(a => a.Navigate && a.Shape is RayRectangleTemplate { Length: 40, MinLocalX: -5, MaxLocalX: 5 })
            && e.TreasureBeams[0].Activation == Start.AddSeconds(7.7)
            && MathF.Abs(e.TreasureBeams[0].Origin.Z - 3281.5f * (2000f / 65535f)) < .0001f,
            "Beam count, geometry, original origin or clock differs.");
        e.RemoveActor(50, Start.AddSeconds(10));
        e.TreasureEffect(50, 43788, 1, 0, Start.AddSeconds(11));
        Check(e.TreasureBeams.Count == 6, "Portal Destroy/effect removed source persistent beams.");
        e.StartCast(2, Cast(43788, 30, start: 20), Start.AddSeconds(20));
        e.Finish(2, 43788, Start.AddSeconds(21));
        Check(e.TreasureBeams.Count == 0, "Any PowerLine Finish did not clear all beams.");
    }
    private static void Bait()
    {
        var e = Create(0x48B5);
        e.StartCast(2, Cast(43784), Start);
        e.TreasureEffect(2, 43784, 1, 100);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/cast-bait/") && a.Display).Shape is CircleTemplate { Radius: 6 },
            "Circle6 bait missing or effect consumed Finish-only bait.");
        e.RemoveActor(100, Start.AddSeconds(1));
        Check(e.AmbientAoes().Any(a => a.Id.EndsWith("/avoid") && a.Shape is CircleTemplate { Radius: 6 }),
            "Removed target lost frozen bait reference.");
        e.Finish(2, 43784, Start.AddSeconds(2));
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/cast-bait/")), "Real Finish retained bait.");
    }
    private static void Gaze()
    {
        var e = Create(0x48AE);
        var origin = Center + Vector3.UnitZ * 30;
        e.StartCast(2, Cast(43701, .5, origin), Start);
        Check(e.Gazes.Single().Location == origin && e.GazeFacing(Center, 0) != null,
            "Gaze default10000 or PR facing window differs.");
        e.Finish(2, 43701, Start.AddSeconds(.1));
        Check(!e.Gazes.Any() && e.GazeFacing(Center, 0) == null, "Gaze Finish retained facing.");
    }
    private static void Rewards()
    {
        foreach (var oid in new uint[] { 0x48AE, 0x48B5 })
        {
            var e = Create(oid);
            var keeperOid = oid == 0x48AE ? 0x48AFu : 0x48B8u;
            var reward = new DungeonActor(50, keeperOid, Center);
            e.Synchronize([new(1, oid, Center, Dead: true), reward, Player], Center, Start.AddSeconds(1));
            Check(e.Active != null && NativeTreasureCatalog.Priority(oid, keeperOid) == 1
                && NativeTreasureCatalog.Priority(oid, 0x48B9) == 6
                && NativeTreasureCatalog.Vulnerable(oid, keeperOid) == (oid == 0x48B5), "Reward lifecycle, priority or seed color differs.");
            e.Synchronize([new(1, oid, Center, Dead: true), reward with { Dead = true }, Player, new(51, 0x48B6, Center)],
                Center, Start.AddSeconds(2));
            Check(e.Active == null, "Portal or dead reward retained encounter.");
        }
    }
}
