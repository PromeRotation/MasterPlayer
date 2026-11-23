using System.Numerics;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Replay;
using PromeRotation.LogSystem;

internal static class PalaceNativeParityRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
    private static Vector3 Center => PalaceArena.Center;
    private static ReplayActorState Actor(ulong id, uint oid, Vector3 position, uint kind = 2)
        => new() { Id = id, Oid = oid, EntityId = (uint)id, Position = position, Kind = (byte)kind, Targetable = true,
            InCombat = true, Hostile = kind != 1, Hp = 1000, MaxHp = 1000, HitboxRadius = .5f };
    private static PalaceCast Cast(ulong source, uint action, ulong target, Vector3 sourcePosition)
        => new(source, action, target, sourcePosition, sourcePosition, 0, Start, Start.AddSeconds(4));

    public static void Run(Action<string, Action> run)
    {
        run("Palace parity own Valfodr shows charge outline avoids allies and preserves landing constraint", Charge);
        run("Palace parity simultaneous opposing gazes select one direction safe against both", Gazes);
        run("Palace parity Yulunggu water pull represents entire expanded union complement", Pull);
        run("Palace parity Godfather small bombs lock birth positions and resolve as one wave", Bombs);
    }

    private static void Charge()
    {
        var e = new PalaceEncounter();
        e.Synchronize([Actor(10, 0x181D, Center) with { TargetId = 100 }, Actor(100, 0, Center + Vector3.UnitZ * 10, 1),
            Actor(101, 0, Center + Vector3.UnitZ * 5, 1)], Start);
        e.StartCast(Cast(10, 7156, 100, Center));
        var areas = e.Collect(Start, 100);
        Check(areas.Any(a => a.Action == 7156 && a.PreviewOnly && a.OutlineOnly), "Own charge outline missing.");
        var bait = areas.Single(a => a.Key == "charge-bait/10/101");
        Check(bait.Geometry.Contains(Center + Vector3.UnitZ * 10) && !bait.Geometry.Contains(Center + Vector3.UnitX * 10),
            "Charge bait sector did not exclude the ally direction.");
        Check(areas.Any(a => a.Key == "landing/10" && a.NavigationOnly), "Adding ally avoidance removed landing safety.");
        e.Cancel(10);
        Check(!e.Collect(Start, 100).Any(a => a.Action == 7156), "Cancelled charge retained personal regions or outline.");
        e.Synchronize([Actor(10, 0x181D, Center) with { TargetId = 100 }, Actor(100, 0, Center - Vector3.UnitZ * 10, 1),
            Actor(101, 0, Center + Vector3.UnitZ * 3, 1)], Start);
        e.StartCast(Cast(10, 7156, 100, Center));
        bait = e.Collect(Start, 100).Single(a => a.Key == "charge-bait/10/101");
        Check(!bait.Geometry.Contains(Center - Vector3.UnitZ * 10) && bait.Geometry.Contains(Center + Vector3.UnitZ * 10),
            "An ally exactly three yalms away incorrectly forbade the full circle.");
        e.Synchronize([Actor(10, 0x181D, Center + Vector3.UnitX * 10) with { TargetId = 100 },
            Actor(100, 0, Center + Vector3.UnitZ * 5, 1), Actor(101, 0, Center + new Vector3(5, 0, 12), 1)], Start.AddSeconds(1));
        var landing = e.Collect(Start.AddSeconds(1), 100).Single(a => a.Key == "landing/10");
        Check(!landing.Geometry.Contains(Center + Vector3.UnitZ * 5), "Bait direction was incorrectly treated as damage at the landing point.");
    }

    private static void Gazes()
    {
        var e = new PalaceEncounter();
        var north = Center + Vector3.UnitZ * 10;
        var south = Center - Vector3.UnitZ * 10;
        e.Synchronize([Actor(10, 0x1890, north), Actor(11, 0x1890, south), Actor(100, 0, Center, 1)], Start);
        e.StartCast(Cast(10, 6953, 100, north));
        e.StartCast(Cast(11, 6953, 100, south));
        var facing = e.Facing(Start.AddSeconds(3.5), 100);
        Check(facing != null && new[] { 0f, MathF.PI }.All(a =>
            MathF.Abs(MathF.Atan2(MathF.Sin(facing.Value - a), MathF.Cos(facing.Value - a))) > MathF.PI / 4),
            "Facing avoided only the first gaze.");
        Check(e.Facing(Start.AddSeconds(3), 100) == null, "Gaze turned earlier than its active window.");
        e.Cancel(10);
        e.Cancel(11);
        Check(e.Facing(Start.AddSeconds(3.5), 100) == null, "Cancelled gazes retained facing.");
        var directions = new[] { 0f, 1.5957963268f, 3.1582593203f, 4.7207223137f };
        var sources = directions.Select((d, i) => Actor((ulong)(20 + i), 0x1890,
            Center + new Vector3(MathF.Sin(d), 0, MathF.Cos(d)) * 10)).ToArray();
        e.Synchronize(sources.Append(Actor(100, 0, Center, 1)), Start);
        foreach (var source in sources) e.StartCast(Cast(source.Id, 6953, 100, source.Position));
        facing = e.Facing(Start.AddSeconds(3.5), 100);
        Check(facing != null && directions.All(d => MathF.Abs(MathF.Atan2(MathF.Sin(facing.Value - d),
            MathF.Cos(facing.Value - d))) > MathF.PI / 4 + .01f), "A narrow valid gap between four gazes was missed.");
    }

    private static void Pull()
    {
        var e = new PalaceEncounter();
        e.Synchronize([Actor(10, 0x181E, Center) with { TargetId = 100, HitboxRadius = 5.75f },
            Actor(100, 0, Center - Vector3.UnitZ * 15, 1), Actor(20, 0x1E9998, Center)], Start);
        var areas = e.Collect(Start, 100);
        var goal = areas.Single(a => a.Key == "pull/170");
        const float radius = 8 + 2 * 5.75f + 3;
        for (var x = -25; x <= 25; ++x)
            for (var z = -25; z <= 25; ++z)
            {
                var point = Center + new Vector3(x, 0, z);
                Check(goal.Geometry.Contains(point) == (PalaceArena.Boundary.Contains(point) && x * x + z * z > radius * radius),
                    "Pull region differs from the source union complement.");
            }
        Check(areas.Any(a => a.Key == "puddle-boss/10" && a.PreviewOnly && a.OutlineOnly)
            && e.Hints(Start).Contains("把 Boss 拉出水圈"), "Boss-in-water drawing or hint missing.");
    }

    private static void Bombs()
    {
        var e = new PalaceEncounter();
        var first = Actor(20, 0x18F3, Center);
        var second = Actor(21, 0x18F3, Center + Vector3.UnitX * 10);
        e.Synchronize([Actor(10, 0x1820, Center), first, second], Start);
        e.Synchronize([Actor(10, 0x1820, Center), first with { Position = Center + Vector3.UnitZ * 10 }, second], Start.AddSeconds(5));
        Check(e.Collect(Start.AddSeconds(5), 100).Single(a => a.Key == "bomb/20").Position == Center, "Bomb prediction followed movement after birth.");
        e.Apply(new LogSystemActionEffectEvent { SourceId = 20, ActionId = 7172, GlobalSequence = 1 }, Start.AddSeconds(10));
        Check(!e.Collect(Start.AddSeconds(10), 100).Any(a => a.Key.StartsWith("bomb/")), "Finishing a small bomb retained part of its source wave.");
        e.Reset();
        e.Synchronize([Actor(10, 0x1820, Center), first, second], Start);
        e.StartCast(Cast(20, 7172, 0, first.Position));
        e.StartCast(Cast(21, 7172, 0, second.Position));
        e.Apply(new LogSystemActionEffectEvent { SourceId = 20, ActionId = 7172, GlobalSequence = 2 }, Start.AddSeconds(4));
        Check(!e.Collect(Start.AddSeconds(4), 100).Any(a => a.Action == 7172), "Resolving the bomb wave retained another bomb's cast region.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
