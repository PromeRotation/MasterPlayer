using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

internal static class CastrumAbaniaRegression
{
    private static readonly DateTime Start = new(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("Castrum Abania ownership and arena centers", OwnershipAndArenas);
        run("Castrum Abania Magna multi-helper telegraphs", Magna);
        run("Castrum Abania Subject helper mechanics", Subject);
        run("Castrum Abania Inferno markers and tether", Inferno);
        run("Castrum Abania effect deduplication and reset", Lifecycle);
        run("Castrum Abania personal spread stack towers and raidwides", PersonalMechanics);
        run("Castrum Abania self cast origins and missing facing", CastOrigins);
        run("Castrum Abania knockback landing avoids wave and cutter", Knockbacks);
        CastrumAbaniaParityRegression.Run(run);
    }

    private static ReplayActorState Actor(ulong id, uint oid, Vector3 position, bool combat = false)
        => new()
        {
            Id = id,
            EntityId = (uint)id,
            Oid = oid,
            Position = position,
            InCombat = combat,
            Targetable = true,
            Hp = 1000,
            MaxHp = 1000,
        };

    private static ReplayActorState Player(ulong id, Vector3 position)
        => Actor(id, 0, position) with { Kind = 1, Hostile = false, ClassJob = 31 };

    private static void Begin(CastrumAbaniaEncounter encounter, uint boss, Vector3 center,
        params ReplayActorState[] extras)
        => encounter.Synchronize(
            [Actor(1, boss, center, combat: true), Player(100, center), .. extras],
            Start);

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void OwnershipAndArenas()
    {
        Check(CastrumAbaniaCatalog.Owns(CastrumAbaniaCatalog.MagnaRoader, 1145), "Magna Roader ownership is missing.");
        Check(CastrumAbaniaCatalog.Owns(CastrumAbaniaCatalog.SubjectNumberXXIV, 1145), "Subject ownership is missing.");
        Check(CastrumAbaniaCatalog.Owns(CastrumAbaniaCatalog.Inferno, 1145), "Inferno ownership is missing.");
        Check(!CastrumAbaniaCatalog.Owns(CastrumAbaniaCatalog.MagnaRoader, 242), "CFC ID must not be used as territory ownership.");

        var cases = new[]
        {
            (CastrumAbaniaCatalog.MagnaRoader, new Vector3(-212.99f, 0, 186.99f)),
            (CastrumAbaniaCatalog.SubjectNumberXXIV, new Vector3(10.5f, 0, 186.5f)),
            (CastrumAbaniaCatalog.Inferno, new Vector3(282.5f, 0, -27.25f)),
        };
        foreach (var (boss, center) in cases)
        {
            var encounter = new CastrumAbaniaEncounter();
            Begin(encounter, boss, center);
            Check(encounter.Active && encounter.Boundary!.Contains(center), $"Arena center is outside boss {boss}.");
            Check(!encounter.Boundary!.Contains(center + new Vector3(25, 0, 0)), $"Arena radius is wrong for boss {boss}.");
        }
        Check(!CastrumAbaniaArena.For(CastrumAbaniaCatalog.MagnaRoader, 0).Contains(new(-213, 0, 167.8f)),
            "Magna's south entrance cutout must remain closed.");
        Check(!CastrumAbaniaArena.For(CastrumAbaniaCatalog.SubjectNumberXXIV, 0).Contains(new(29.5f, 0, 186.5f)),
            "Subject's east entrance cutout must remain closed.");
    }

    private static void Magna()
    {
        var center = new Vector3(-212.99f, 0, 186.99f);
        var firstHelper = Actor(10, CastrumAbaniaCatalog.MagnaHelper, center + Vector3.UnitX * 5);
        var secondHelper = Actor(11, CastrumAbaniaCatalog.MagnaHelper, center - Vector3.UnitX * 5);
        var encounter = new CastrumAbaniaEncounter();
        Begin(encounter, CastrumAbaniaCatalog.MagnaRoader, center, firstHelper, secondHelper);

        encounter.StartCast(10, CastrumAbaniaCatalog.MagnaHelper, 7960, firstHelper.Position, 0,
            Start, Start.AddSeconds(6.5), 0);
        encounter.StartCast(11, CastrumAbaniaCatalog.MagnaHelper, 7960, secondHelper.Position, MathF.PI,
            Start, Start.AddSeconds(7), 0);
        var lines = encounter.Collect(Start, 100).Where(a => a.Action == 7960).ToArray();
        Check(lines.Length == 2 && lines.All(a => a.Shape is RayRectangleTemplate),
            "Magna must retain simultaneous helper Haywire rectangles.");

        encounter.StartCast(1, CastrumAbaniaCatalog.MagnaRoader, 7957, center, 0,
            Start, Start.AddSeconds(3), 100);
        var fire = encounter.Collect(Start, 100).Single(a => a.Action == 7957);
        Check(fire.Geometry.Contains(center + Vector3.UnitX * 4.9f)
            && !fire.Geometry.Contains(center + Vector3.UnitX * 5.1f),
            "Magitek Fire II must retain its R5 footprint.");
    }

    private static void Subject()
    {
        var center = new Vector3(10.5f, 0, 186.5f);
        var helper = Actor(20, CastrumAbaniaCatalog.SubjectHelper, center + Vector3.UnitZ * 4);
        var encounter = new CastrumAbaniaEncounter();
        Begin(encounter, CastrumAbaniaCatalog.SubjectNumberXXIV, center, helper);

        encounter.StartCast(20, CastrumAbaniaCatalog.SubjectHelper, 33460, helper.Position, MathF.PI / 2,
            Start, Start.AddSeconds(4), 0);
        var grid = encounter.Collect(Start, 100).Single(a => a.Action == 33460);
        Check(grid.Shape is RayRectangleTemplate, "Subject Ice Grid must accept the actual 0x233C helper.");

        encounter.StartCast(20, CastrumAbaniaCatalog.SubjectHelper, 33461, helper.Position, 0,
            Start, Start.AddSeconds(5), 100);
        var spread = encounter.Collect(Start, 100).Single(a => a.Action == 33461);
        Check(spread.Geometry.Contains(center + Vector3.UnitZ * 4.9f)
            && !spread.Geometry.Contains(center + Vector3.UnitZ * 5.1f),
            "Blizzard II must follow its cast target.");

        encounter.Apply(new LogSystemActionEffectEvent
        {
            SourceId = 20,
            ActionId = 33467,
            TargetId = 100,
            GlobalSequence = 1,
        }, Start);
        Check(encounter.Collect(Start, 100).Any(a => a.Key.StartsWith("bait/")),
            "Sparking Current must publish a live helper-to-target line.");
        Check(encounter.Hints(Start, 100).Any(h => h.Contains("Sparking Current")),
            "Sparking Current must hint the marked target, not its helper source.");
    }

    private static void Inferno()
    {
        var center = new Vector3(282.5f, 0, -27.25f);
        var target = Player(100, center + Vector3.UnitX * 8);
        var claw = Actor(30, 0x1AB0, center - Vector3.UnitX * 8);
        var helper = Actor(31, CastrumAbaniaCatalog.InfernoHelper, center + Vector3.UnitZ * 4);
        var encounter = new CastrumAbaniaEncounter();
        encounter.Synchronize(
            [Actor(1, CastrumAbaniaCatalog.Inferno, center, combat: true), target, claw, helper],
            Start);

        encounter.Icon(new LogSystemTargetIconEvent
        {
            TargetId = 100,
            MarkerId = CastrumAbaniaCatalog.TargetIconSpread,
        }, Start);
        var spread = encounter.Collect(Start, 100).Single(a => a.Action == 7978);
        Check(spread.Geometry.Contains(target.Position + Vector3.UnitX * 9.9f)
            && !spread.Geometry.Contains(target.Position + Vector3.UnitX * 10.1f),
            "Rahu Ray must use the target icon's player.");

        encounter.Tether(new LogSystemTetherEvent
        {
            SourceId = 30,
            TargetId = 100,
            TetherId = CastrumAbaniaCatalog.ClawTether,
        }, Start);
        Check(encounter.ClawTethers.TryGetValue(30, out var tether) && tether.Target == 100,
            "Death Claw tether was not retained.");
        encounter.TetherCancel(new LogSystemTetherCancelEvent { SourceId = 30 });
        Check(!encounter.ClawTethers.ContainsKey(30), "Tether cancellation left stale Death Claw state.");
    }

    private static void Lifecycle()
    {
        var center = new Vector3(-212.99f, 0, 186.99f);
        var encounter = new CastrumAbaniaEncounter();
        Begin(encounter, CastrumAbaniaCatalog.MagnaRoader, center);
        encounter.StartCast(1, CastrumAbaniaCatalog.MagnaRoader, 7957, center, 0,
            Start, Start.AddSeconds(3), 100);
        encounter.Apply(new LogSystemActionEffectEvent
        {
            SourceId = 1,
            ActionId = 7957,
            GlobalSequence = 1,
        }, Start.AddSeconds(2.8));
        encounter.Apply(new LogSystemActionEffectEvent
        {
            SourceId = 1,
            ActionId = 7957,
            GlobalSequence = 1,
        }, Start.AddSeconds(2.8));
        Check(!encounter.Collect(Start.AddSeconds(2.8), 100).Any(),
            "Duplicate ActionEffect must not recreate a consumed cast.");

        var boss = Actor(1, CastrumAbaniaCatalog.MagnaRoader, center, combat: true) with
        {
            Cast = new(7957, 100, center, 0, Start, Start.AddSeconds(3), false, false),
        };
        encounter.Synchronize([boss, Player(100, center)], Start.AddSeconds(2.9));
        Check(encounter.Collect(Start.AddSeconds(2.9), 100).Count == 0,
            "A stale snapshot re-armed a consumed cast.");

        encounter.StartCast(1, CastrumAbaniaCatalog.MagnaRoader, 7957, center, 0,
            Start.AddSeconds(13), Start.AddSeconds(16), 100);
        encounter.Apply(new LogSystemActionEffectEvent
        {
            SourceId = 1,
            ActionId = 7957,
            GlobalSequence = 1,
        }, Start.AddSeconds(15.8));
        Check(encounter.Collect(Start.AddSeconds(15.8), 100).Count == 1,
            "A duplicate old effect consumed a later cast.");
        encounter.Apply(new LogSystemActionEffectEvent
        {
            SourceId = 1, ActionId = 7957, GlobalSequence = 2,
        }, Start.AddSeconds(15.8));
        Check(encounter.Collect(Start.AddSeconds(15.8), 100).Count == 0, "The new effect did not resolve its cast.");

        encounter.Synchronize([], Start.AddSeconds(17));
        Check(!encounter.Active && encounter.Boundary == null && encounter.Collect(Start, 100).Count == 0,
            "A missing boss must reset the encounter completely.");
    }

    private static void PersonalMechanics()
    {
        var center = new Vector3(10.5f, 0, 186.5f);
        var e = new CastrumAbaniaEncounter();
        var boss = Actor(1, CastrumAbaniaCatalog.SubjectNumberXXIV, center, true);
        var helper = Actor(10, CastrumAbaniaCatalog.SubjectHelper, center);
        var left = center - Vector3.UnitX * 8;
        var right = center + Vector3.UnitX * 8;
        e.Synchronize([boss, helper, helper with { Id = 11 }, Player(100, left), Player(101, right)], Start);
        e.StartCast(1, boss.Oid, 33448, center, 0, Start, Start.AddSeconds(5), 0);
        Check(e.Collect(Start, 100).Count == 0 && e.Hints(Start, 100).Any(h => h.Contains("全屏")),
            "Raidwides must hint mitigation without making the arena impassable.");
        e.StartCast(10, helper.Oid, 33461, Vector3.Zero, null, Start, Start.AddSeconds(5), 100);
        var own = e.Collect(Start, 100);
        Check(own.Single(a => a.Key.StartsWith("cast/") && !a.NavigationOnly).PreviewOnly,
            "The marked player's own spread is drawing only.");
        Check(own.Any(a => a.NavigationOnly && a.Geometry.Contains(right) && !a.Geometry.Contains(left)),
            "The marked player must keep the spread away from other party members.");
        Check(!e.Collect(Start, 101).Single(a => a.Action == 33461).PreviewOnly,
            "Other perspectives must avoid the marked player's circle.");
        e.Cancel(10, 33461);
        e.StartCast(10, helper.Oid, 33462, Vector3.Zero, null, Start.AddSeconds(6), Start.AddSeconds(11), 100);
        Check(e.Collect(Start.AddSeconds(6), 101).Single(a => a.Action == 33462 && a.PreviewOnly) is { Safe: true, Position: var p }
            && p == left, "Stack goals must use the live marked player.");
        e.Cancel(10, 33462);
        e.StartCast(10, helper.Oid, 33464, Vector3.Zero, null, Start, Start.AddSeconds(5), 0, left);
        e.StartCast(11, helper.Oid, 33464, Vector3.Zero, null, Start, Start.AddSeconds(5), 0, right);
        var a = e.Collect(Start, 100).Single(t => t.Key == "tower/enter");
        var b = e.Collect(Start, 101).Single(t => t.Key == "tower/enter");
        Check(!a.Geometry.Contains(left) && a.Geometry.Contains(right) && !b.Geometry.Contains(right) && b.Geometry.Contains(left),
            "Each perspective must enter its own or an empty tower and avoid occupied towers.");
    }

    private static void CastOrigins()
    {
        var center = new Vector3(282.5f, 0, -27.25f);
        var e = new CastrumAbaniaEncounter();
        Begin(e, CastrumAbaniaCatalog.Inferno, center, Actor(10, CastrumAbaniaCatalog.InfernoHelper, center));
        e.StartCast(10, CastrumAbaniaCatalog.InfernoHelper, 7975, Vector3.Zero, null, Start, Start.AddSeconds(4), 0);
        Check(e.Collect(Start, 100).Count == 0, "Directional casts must reject missing locked facing.");
        e.StartCast(10, CastrumAbaniaCatalog.InfernoHelper, 7975, Vector3.Zero, 0, Start, Start.AddSeconds(4), 0, center);
        Check(e.Collect(Start, 100).Single().Position == center,
            "Self casts must retain the source pose even when the packet location is zero.");
        e.StartCast(11, CastrumAbaniaCatalog.SubjectHelper, 33460, center, 0, Start, Start.AddSeconds(4), 0);
        Check(e.Collect(Start, 100).Count == 1, "Helpers from a different boss must be rejected.");
    }

    private static void Knockbacks()
    {
        var center = new Vector3(282.5f, 0, -27.25f);
        var arena = CastrumAbaniaArena.For(CastrumAbaniaCatalog.Inferno, 0);
        var wave = new CircleGeometry(center + new Vector3(0, 0, 8), 2);
        var template = new CastrumKnockbackTemplate(arena, center, 5, [wave]);
        Check(template.Landing(center + new Vector3(0, 0, 3)) == center + new Vector3(0, 0, 8),
            "The first comet must push radially by five meters.");
        Check(template.Resolve(center, 0, null).Contains(center + new Vector3(0, 0, 3))
            && !template.Resolve(center, 0, null).Contains(center + new Vector3(3, 0, 0)),
            "Landing checks must reject an overlapping Ketu Wave.");
        var far = template.Landing(center + new Vector3(18, 0, 0));
        Check(arena.Contains(far), "Comet displacement must stop at the arena wall.");
        var e = new CastrumAbaniaEncounter();
        Begin(e, CastrumAbaniaCatalog.Inferno, center,
            Actor(10, CastrumAbaniaCatalog.InfernoHelper, center));
        e.StartCast(10, CastrumAbaniaCatalog.InfernoHelper, 8329, center - Vector3.UnitX * 10, 0,
            Start, Start.AddSeconds(4), 0);
        Check(e.Collect(Start, 100).Single(a => a.Key.StartsWith("knockback/")).Shape
            is CastrumKnockbackTemplate { Distance: 10 }, "The third comet requires ten-meter landing prediction.");
    }
}
