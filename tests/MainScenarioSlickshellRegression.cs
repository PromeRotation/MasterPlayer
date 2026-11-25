using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using MasterPlayer.Scripting;

internal static class MainScenarioSlickshellRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new((445f + 495.55f) * .5f, 0, (283.56f + 320.19f) * .5f);

    public static void Run(Action<string, Action> run)
    {
        run("MSQ Slickshell exact seven vertices center actions and source scoped ownership", Catalog);
        run("MSQ Slickshell default pull permits HP0 and dead actor without ending phase", Pull);
        run("MSQ Slickshell primary destruction retains other casts and voidzones until owner stop", PrimaryLifetime);
        run("MSQ Slickshell same ID primary generation renews future baits and freezes old references", PrimaryGeneration);
        run("MSQ Slickshell Director wipe unloads phase blocks stale pull and explicitly reopens", Wipe);
        run("MSQ Slickshell icon resolves packet target fallback and independent processing clocks", Icons);
        run("MSQ Slickshell any Blizzard start clears baits corrections finishes effects do not", ClearBaits);
        run("MSQ Slickshell bait death removal missing target freeze and same ID generation", BaitLifetime);
        run("MSQ Slickshell own outline companion avoidance and first bait damage clock", BaitOutput);
        run("MSQ Slickshell voidzone cast native main target event live handoff quantization", Voidzone);
        run("MSQ Slickshell real observed casts cancel and stale generations do not restart", CastLifetime);
        run("MSQ Slickshell full priority setter follows Isse target with pending dead protection", Priority);
        run("MSQ Slickshell real output solver escapes bait and owner cleanup releases all areas", NativeOutput);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(634);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x213A, Center, InCombat: true, Hp: 1000, IsEnemy: true, ObjectIndex: 2),
            new(100, 0, Center + new Vector3(-6, 0, 0), PartySlot: 0, Player: true),
            new(101, 0, Center + new Vector3(-4, 0, 0), PartySlot: 1, Player: true),
        ];
        internal DateTime Now = Start;
        internal Fixture() => Sync();
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == 100).Position, Now, playerId: 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = change(Actors[i]);
        }
        internal DungeonAmbientAoe[] Baits() => E.AmbientAoes().Where(a => a.Id.StartsWith("slickshell/bait/")).ToArray();
        internal DungeonAmbientAoe[] Pools() => E.AmbientAoes().Where(a => a.Id.StartsWith("trial/voidzone/")).ToArray();
        internal void Icon(ulong target = 100, ulong source = 101) => E.Icon(target, 26, Start.AddSeconds(-5), source);
        internal void Cast(ulong source = 1, uint action = 1087, ulong target = 101)
        {
            if (Actors.All(a => a.Id != source))
            {
                Actors.Add(new(source, 0x213B, Center, InCombat: true));
                Sync();
            }
            E.StartCast(source, new(action, Center, .3f, Now, Now.AddSeconds(3), target), Now);
        }
    }

    private static void Catalog()
    {
        var f = new Fixture();
        var polygon = (WorldPolygonGeometry)f.E.Arena!;
        Vector3[] expected = [new(464.25f, 0, 320.19f), new(455.65f, 0, 313.35f), new(457.72f, 0, 308.20f),
            new(445, 0, 292.92f), new(468.13f, 0, 283.56f), new(495.55f, 0, 299.63f), new(487.19f, 0, 313.73f)];
        Check(polygon.WorldPoints.SequenceEqual(expected) && polygon.Position == Center, "Source contour/AABB center changed.");
        Check(f.E.Active!.Casts.Select(c => c.Action).SequenceEqual(new uint[] { 5831, 8585, 1087 })
            && MainScenarioQuestCatalog.Owns(634, 0x213A) && !MainScenarioQuestCatalog.Owns(634, 0x213B),
            "Active action set or primary-only bridge ownership changed.");
    }

    private static void Pull()
    {
        var f = new Fixture();
        f.E.ResetFight();
        f.Set(1, a => a with { InCombat = false });
        f.Sync();
        Check(f.E.Active == null, "Idle primary pulled.");
        f.Set(1, a => a with { InCombat = true, Hp = 0, Dead = true });
        f.Sync();
        Check(f.E.Active != null, "Source targetable combat pull incorrectly required HP/death gate.");
        f.Set(1, a => a with { InCombat = false, Targetable = false });
        f.Sync();
        Check(f.E.Active != null, "Primary death/combat loss ended CFC-owned phase.");
    }

    private static void PrimaryLifetime()
    {
        var f = new Fixture();
        f.Cast(10, 5831);
        f.E.Effect(1, 1087, 1, f.Now, 101);
        f.Icon();
        f.E.RemoveActor(1);
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        Check(f.E.Active != null && f.E.PrimaryId == 1 && f.E.OutputCasts.Any(c => c.Value.Rule.Action == 5831)
            && f.E.PreserveStageAcrossBattleEnd && f.Pools().Any() && !f.Baits().Any(), "Primary destroy ended phase or failed bait-only cleanup.");
        f.Actors.Add(new(1, 0x213A, Center, InCombat: true));
        f.Sync();
        Check(f.E.Active != null && f.E.OutputCasts.Any(), "Same-ID replacement reset old phase.");
        f.E.ResetFight();
        Check(f.E.Arena == null && !f.Pools().Any() && !f.E.OutputCasts.Any(), "Owner stop retained phase outputs.");
    }

    private static void PrimaryGeneration()
    {
        var f = new Fixture();
        f.Set(1, a => a with { Dead = true });
        f.Sync();
        f.E.RemoveActor(1);
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        f.Icon();
        Check(!f.Baits().Any(), "Old dead source incorrectly remained active.");
        f.Actors.Add(new(1, 0x213A, Center, InCombat: true));
        f.Sync();
        Check(!f.Baits().Any(), "New source rebound bait created against old dead source.");
        f.Icon(101);
        Check(f.Baits().Count(a => a.Id.EndsWith("/display")) == 1, "New primary failed to supply future Icon source.");
        f.E.ObjectCreated(101, f.Now, 0);
        var old = f.Actors.Single(a => a.Id == 101).Position;
        f.Set(101, a => a with { Position = old + Vector3.UnitX * 10 });
        f.Sync();
        Check(f.Baits().Single(a => a.Id.EndsWith("/display")).Position == old,
            "Target creation with reused ID rebound an old bait.");
    }

    private static void Wipe()
    {
        var f = new Fixture();
        f.Icon();
        f.E.Effect(1, 1087, 1, f.Now, 101);
        f.E.DirectorUpdate(0x40000005);
        f.Sync();
        Check(f.E.Active == null && !f.Pools().Any() && !f.Baits().Any(), "Wipe left active state or repulled stale combat snapshot.");
        f.E.DirectorUpdate(0x40000011);
        Check(f.E.Active == null, "Wipe release invented a pull.");
        f.Sync();
        Check(f.E.Active != null && !f.Pools().Any() && !f.Baits().Any(), "Explicit reopen failed fresh empty phase.");
    }

    private static void Icons()
    {
        var f = new Fixture();
        f.Icon(101, 100);
        f.Now = Start.AddSeconds(2);
        f.Sync();
        f.Icon(999, 100);
        var baits = f.Baits().Where(a => a.Id.EndsWith("/display")).ToArray();
        Check(baits.Length == 2 && baits[0].Position == f.Actors[2].Position && baits[1].Position == f.Actors[1].Position
            && baits[0].Activation == Start.AddSeconds(5.1) && baits[1].Activation == Start.AddSeconds(7.1),
            "Icon source/target fallback or component processing time changed.");
        f.E.Tick(Start.AddSeconds(100));
        Check(f.Baits().Count(a => a.Id.EndsWith("/display")) == 2, "Icon bait timed out without source event.");
    }

    private static void ClearBaits()
    {
        var f = new Fixture();
        f.Icon();
        f.Cast(10, 5831);
        Check(f.Baits().Any(), "Unrelated start cleared icon bait.");
        f.Cast(11);
        Check(!f.Baits().Any(), "Nonprimary Blizzard start did not clear all baits.");
        f.Icon();
        var cast = f.E.Casts[11];
        f.E.StartCast(11, new(1087, Center + Vector3.UnitX, .3f, cast.StartedAt, cast.FinishAt, 100), f.Now);
        Check(f.Baits().Any(), "Same-generation packet correction repeated Start cleanup.");
        f.E.Finish(11, 1087, f.Now.AddSeconds(.1));
        f.E.Effect(11, 1087, 1, f.Now.AddSeconds(.2), 100);
        Check(f.Baits().Any(), "Blizzard finish/effect incorrectly cleared a later icon.");
    }

    private static void BaitLifetime()
    {
        var f = new Fixture();
        f.Icon(101);
        f.Set(101, a => a with { Dead = true });
        f.Sync();
        f.Set(101, a => a with { Dead = false });
        f.Sync();
        Check(!f.Baits().Any(), "Dead target icon resurrected.");
        f.Icon(101);
        var position = f.Actors[2].Position;
        f.Actors.RemoveAll(a => a.Id == 101);
        f.Sync();
        f.Actors.Add(new(101, 0, position + Vector3.UnitX * 20, PartySlot: 1, Player: true));
        f.Sync();
        Check(f.Baits().Single(a => a.Id.EndsWith("/display")).Position == position, "Same-ID target rebound old bait.");
        f.Set(1, a => a with { Dead = true });
        f.Sync();
        Check(!f.Baits().Any(), "Dead primary bait remained active.");
        f.Set(1, a => a with { Dead = false });
        f.Sync();
        Check(f.Baits().Any(), "Primary revival erased existing bait.");
    }

    private static void BaitOutput()
    {
        var f = new Fixture();
        f.Icon();
        f.Now = Start.AddSeconds(1);
        f.Sync();
        f.Icon(101);
        Check(f.Baits().Any(a => a.Id.EndsWith("/display") && a.OutlineOnly && !a.Navigate)
            && f.Baits().Any(a => a.Id.Contains("/away/101") && !a.Display && a.Navigate),
            "Own bait outline or companion avoidance missing.");
        Check(f.E.PredictedDamage.Count() == 2
            && f.E.PredictedDamage.All(p => p.Activation == Start.AddSeconds(5.1) && p.DamageType == "Raidwide"),
            "Bait damage mask lost first list clock.");
        f.E.ResetFight();
        f.Sync();
        f.E.Icon(1, 26, f.Now, 1);
        Check(!f.Baits().Any(a => a.Id.Contains("/away/")), "Source=target generated self-bait companion avoidance.");
    }

    private static void Voidzone()
    {
        var f = new Fixture();
        f.Cast();
        f.Set(101, a => a with { Position = a.Position + Vector3.UnitZ * 2 });
        f.Sync();
        Check(f.Pools().Single().Position == f.Actors[2].Position && f.Pools().Single().Shape is CircleTemplate { Radius: 6 },
            "Cast prediction lost dynamic target or actual source R6.");
        f.E.Finish(1, 1087, f.Now.AddSeconds(.1));
        f.E.Effect(1, 1087, 1, f.Now.AddSeconds(.2), 101, Center + Vector3.UnitX * 20);
        f.E.Effect(1, 1087, 2, f.Now.AddSeconds(.3), 101);
        Check(f.Pools().Length == 2 && f.Pools().All(a => a.Position == f.Actors[2].Position), "Effect ignored main target or failed append.");
        var raw = f.Actors[2].Position + new Vector3(.123f, 0, .234f);
        f.Actors.Add(new(20, 0x1E8D9C, raw, Dead: true, Targetable: false));
        f.Sync();
        Check(f.Pools().Count(a => a.Id.Contains("/event/")) == 1
            && f.Pools().Single(a => a.Id.Contains("/live/")).Position != raw, "Pool must consume one prediction per update and use quantized position.");
        f.Sync();
        Check(f.Pools().Count(a => a.Id.Contains("/event/")) == 0, "Second update failed next FIFO handoff.");
        f.Set(20, a => a with { EventState = 7 });
        f.Sync();
        Check(!f.Pools().Any(), "EventState7 pool still active.");
    }

    private static void CastLifetime()
    {
        var f = new Fixture();
        f.Cast(10, 8585);
        var cast = f.E.Casts[10];
        f.Set(10, a => a with { Cast = new(8585, cast.Location, cast.Rotation, cast.StartedAt, cast.FinishAt) });
        f.Sync();
        f.E.Effect(10, 8585, 1, f.Now);
        f.E.Tick(Start.AddSeconds(100));
        Check(f.E.OutputCasts.Single().Value.Rule.Shape is FanTemplate { Radius: 7.6f }, "Kasaya expired on effect/deadline.");
        f.Set(10, a => a with { Cast = null });
        f.Sync();
        Check(!f.E.OutputCasts.Any(), "Real cancel left Kasaya.");
        f.E.StartCast(10, new(8585, cast.Location, cast.Rotation, cast.StartedAt, cast.FinishAt), f.Now);
        Check(!f.E.OutputCasts.Any(), "Old packet resurrected cancelled cast.");
    }

    private static void Priority()
    {
        var f = new Fixture();
        f.Actors.Add(new(20, 0x2138, Center, FriendlyNpc: true));
        f.Actors.Add(new(10, 0x213C, Center, IsEnemy: true, ObjectIndex: 4, TargetId: 20, Hp: 100));
        f.Sync();
        Check(f.E.PriorityTarget(1, f.Actors[1].Position) == 10, "Enemy targeting Isse lacked priority1.");
        f.Set(10, a => a with { TargetId = 999 });
        f.Sync();
        Check(f.E.PriorityTarget(1, f.Actors[1].Position) == 1, "Missing target failed priority0 fallback/current preference.");
        f.Set(10, a => a with { TargetId = 20, Hp = 1 });
        f.Sync();
        Check(f.E.PriorityTarget(10, f.Actors[1].Position) == 1, "Priority setter overrode Pointless protection.");
        f.Set(10, a => a with { FateId = 99 });
        f.Sync();
        Check(f.E.PriorityTarget(1, f.Actors[1].Position) == 10, "FATE mismatch must precede pending-dead before full source setter.");
    }

    private static void NativeOutput()
    {
        var f = new Fixture();
        f.Icon(101);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
        var draw = new ScriptDrawApi();
        var output = new DungeonOutput(safe, draw, safe.Manager);
        output.Apply(f.E, Start, false);
        var snapshot = safe.Manager.BuildSnapshot(Start);
        var player = f.Actors[1].Position;
        var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, Start);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        var final = path.Count > 0 ? path[^1].Position : player;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback
            && Vector2.Distance(new(final.X, final.Z), new(f.Actors[2].Position.X, f.Actors[2].Position.Z)) >= 5,
            "Real solver did not escape other player's bait.");
        output.Clear();
        Check(safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0, "Owner clear leaked native danger.");
    }
}
