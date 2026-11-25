using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class MainScenarioHopeRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(473.25f, 0, 751.75f);

    public static void Run(Action<string, Action> run)
    {
        run("MSQ Hope exact seven casts six geometries no invented detonate raidwide", Catalog);
        run("MSQ Hope region phase survives primary HP0 death destroy and combat end", Lifecycle);
        run("MSQ Hope Director wipe stale pull barrier and explicit unlock", Wipe);
        run("MSQ Hope real observed end source cancellation packet barriers and helper shapes", Casts);
        run("MSQ Hope first detonate live cast countdown FIFO source reuse and timeout", Detonate);
        run("MSQ Hope first Alphinaud persistent identity current LB and full target setter", Adds);
        run("MSQ Hope markers use native enemy identity and mechanism disable clears owners", Markers);
        run("MSQ Hope public native output real solver escapes missile without scripted movement", NativeOutput);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(757);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x21B1, Center, InCombat: false, Hp: 1000, IsEnemy: true, ObjectIndex: 2),
            new(100, 0, Center + Vector3.UnitZ, PartySlot: 0, Player: true, ClassJob: 19),
            new(101, 0, Center + Vector3.UnitX * 12, PartySlot: 1, Player: true),
        ];
        internal DateTime Now = Start;
        internal bool Enabled = true;
        internal Fixture() => Sync();
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == 100).Position, Now,
            enabled: _ => Enabled, playerId: 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = change(Actors[i]);
        }
        internal void Pull()
        {
            Set(1, a => a with { InCombat = true });
            Sync();
        }
        internal void Cast(ulong source, uint action, double startedAt = 0, double duration = 30, Vector3? location = null)
        {
            var observation = new DungeonCastObservation(action, location ?? Center, .4f,
                Start.AddSeconds(startedAt), Start.AddSeconds(startedAt + duration + .3), TotalTime: (float)duration);
            Set(source, a => a with { Cast = observation });
            E.StartCast(source, observation, Now);
            Sync();
        }
    }

    private static void Catalog()
    {
        var f = new Fixture();
        var rules = f.E.Definitions.Single().Casts;
        Check(rules.Select(c => c.Action).SequenceEqual(new uint[] { 10823, 9428, 10929, 10893, 9425, 10928, 10930 })
            && rules.Count(c => c.Shape != null) == 6 && rules.Single(c => c.Action == 10928).Hint == null,
            "Activated component table changed or detonation became a fake raidwide.");
        Check(rules.Single(c => c.Action == 10893).Shape is CircleTemplate { Radius: 11.75f }
            && rules.Single(c => c.Action == 9428).Shape is CircleTemplate { Radius: 10.24f }
            && MainScenarioQuestCatalog.Owns(757, 0x21B1) && !MainScenarioQuestCatalog.Owns(613, 0x21B1),
            "Source geometry used comments or overworld territory instead of actual definitions.");
        f.Pull();
        Check(f.E.Arena!.GetContours().Single().Count == 50, "Source rotated ellipse lost its 50 vertices.");
    }

    private static void Lifecycle()
    {
        var f = new Fixture();
        f.Set(1, a => a with { InCombat = true, Dead = true, Hp = 0 });
        f.Sync();
        Check(f.E.Active != null && f.E.PreserveStageAcrossBattleEnd, "Default dead/HP0 primary could not pull regional phase.");
        f.E.RemoveActor(1);
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        Check(f.E.Active != null && f.E.PreserveStageAcrossBattleEnd, "Primary destroy ended regional phase.");
        f.Actors.Add(new(9, 0x233C, Center));
        f.Sync();
        f.Cast(9, 10929);
        Check(f.E.OutputCasts.Single().Value.Rule.Action == 10929, "Later helper cast after primary destroy was rejected.");
        f.Enabled = false;
        f.Sync();
        Check(f.E.Active == null && !f.E.OutputCasts.Any(), "Disable did not clear regional phase.");
    }

    private static void Wipe()
    {
        var f = new Fixture();
        f.Pull();
        f.Cast(1, 9425);
        f.E.DirectorUpdate(0x40000005);
        f.Sync();
        Check(f.E.Active == null && !f.E.OutputCasts.Any(), "Wipe restarted from stale combat snapshot.");
        f.E.DirectorUpdate(0x40000011);
        f.Sync();
        Check(f.E.Active != null, "Explicit wipe unlock did not permit the next valid pull.");
    }

    private static void Casts()
    {
        var f = new Fixture();
        f.Pull();
        f.Actors.Add(new(9, 0x233C, Center, Targetable: false));
        f.Sync();
        f.Cast(9, 10893, duration: 3);
        var cast = f.E.Casts[9];
        f.Set(9, a => a with { Dead = true, Position = Center + Vector3.UnitX * 15 });
        f.Sync();
        f.E.Effect(9, 10893, 1, Start.AddSeconds(1));
        f.Now = Start.AddSeconds(60);
        f.E.Tick(f.Now);
        Check(f.E.OutputCasts.Single().Value.Location == Center && !f.E.PredictedDamage.Any(),
            "Death, Effect or prediction timeout consumed/relocked ordinary AOE.");
        f.Set(9, a => a with { Cast = null });
        f.Sync();
        Check(!f.E.OutputCasts.Any(), "Real observed cancellation left danger.");
        f.E.StartCast(9, new(10893, Center, 0, cast.StartedAt, cast.FinishAt), f.Now);
        Check(!f.E.OutputCasts.Any(), "Stale packet revived cancelled ordinary AOE.");
    }

    private static void Detonate()
    {
        var f = new Fixture();
        f.Pull();
        f.Actors.Add(new(9, 0x21B6, Center));
        f.Actors.Add(new(10, 0x21B6, Center));
        f.Sync();
        f.Cast(9, 10928, duration: 30);
        f.Now = Start.AddSeconds(1);
        f.Cast(10, 10928, 1, 50);
        Check(f.E.Hints(Center).Any(h => h == "Kill before detonation! 29.3s left")
            && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null) && !f.E.PredictedDamage.Any(),
            "Hint did not use first caster current CastInfo with NPC delay or invented damage.");
        f.Set(9, a => a with { Cast = null });
        f.Sync();
        Check(f.E.Hints(Center).Any(h => h == "Kill before detonation! 50.3s left"), "Cancelling first detonator did not promote next.");
        f.Now = Start.AddSeconds(2);
        f.Cast(9, 10928, 2, 5);
        Check(f.E.Hints(Center).Any(h => h == "Kill before detonation! 49.3s left"), "Reused source stole first-caster FIFO slot.");
        f.Now = Start.AddSeconds(100);
        f.E.Tick(f.Now);
        Check(f.E.Hints(Center).Any(h => h == "Kill before detonation! 0.3s left"), "Overdue current CastInfo lost source NPC delay floor.");
    }

    private static void Adds()
    {
        var f = new Fixture();
        f.Pull();
        f.Actors.Add(new(7, 0x21AC, Center, FriendlyNpc: true, IsEnemy: false));
        f.Actors.Add(new(8, 0x21AC, Center, FriendlyNpc: true, IsEnemy: false));
        f.Actors.Add(new(9, 0x2114, Center, IsEnemy: true, ObjectIndex: 4, Hp: 100));
        f.Actors.Add(new(10, 0x21B6, Center, IsEnemy: true, ObjectIndex: 6, Hp: 100));
        f.Sync();
        f.Cast(8, 10894, location: Center + Vector3.UnitX * 10);
        Check(f.E.HopeAdds.Count == 0 && f.E.PriorityTarget(9, Center) == 9,
            "Later Alphinaud bypassed first object lookup or neutral priority was dropped.");
        f.Cast(7, 10894, location: Center + Vector3.UnitZ * 5);
        var original = f.Actors.Single(a => a.Id == 7);
        f.Actors.RemoveAll(a => a.Id == 7);
        f.Actors.Add(original);
        f.Sync();
        Check(f.E.HopeAdds.Single() is { ShouldBeTanked: true, PreferProvoking: false } add
            && add.Position == Center + Vector3.UnitZ * 5 && f.E.PriorityTarget(10, Center) == 9,
            "Snapshot reorder changed first Alphinaud or lost LB target/tank intention.");
        f.Set(9, a => a with { Hp = 1 });
        f.Cast(10, 10928);
        Check(f.E.PriorityTarget(9, Center) == 10, "Full setter overwrote Pointless or ignored self-detonator.");
        f.Actors.RemoveAll(a => a.Id == 7);
        f.Sync();
        Check(f.E.HopeAdds.Single().Position == Center + Vector3.UnitX * 10,
            "Destroyed first Alphinaud did not release the next current CastInfo.");
        Check(!f.E.AmbientAoes().Any(a => a.Id.StartsWith("enemy/tank/")) && f.E.EnemyProvokeActions().Count == 0,
            "Source-only tank intention expanded the old bridge execution boundary.");
        var replacement = new Fixture();
        replacement.Pull();
        replacement.Actors.Add(new(7, 0x21AC, Center));
        replacement.Actors.Add(new(8, 0x21AC, Center));
        replacement.Actors.Add(new(9, 0x2114, Center, IsEnemy: true, ObjectIndex: 4));
        replacement.Sync();
        replacement.Actors.RemoveAll(a => a.Id == 7);
        replacement.Actors.Add(new(20, 0x9999, Center));
        replacement.Actors.Add(new(21, 0x21AC, Center, Cast: new(10894, Center + Vector3.UnitX * 10, 0, Start, Start.AddSeconds(10))));
        replacement.Sync();
        Check(replacement.E.HopeAdds.Count == 0,
            "NPC-only dictionary reused a global slot that an unrelated actor already occupied.");
        replacement.Set(8, a => a with { Cast = new(10894, Center + Vector3.UnitZ * 10, 0, Start, Start.AddSeconds(10)) });
        replacement.Sync();
        Check(replacement.E.HopeAdds.Single().Position == Center + Vector3.UnitZ * 10,
            "Global actor slot reuse failed first Alphinaud priority.");
    }

    private static void Markers()
    {
        var f = new Fixture();
        f.Pull();
        f.Actors.Add(new(9, 0x9999, Center + Vector3.UnitX, Hostile: false, IsEnemy: true));
        f.Actors.Add(new(10, 0x9999, Center, Hostile: true, IsEnemy: false));
        f.Sync();
        var markers = f.E.AmbientAoes().Where(a => a.Id.StartsWith("job-quest/enemy/")).ToArray();
        Check(markers.Length == 2 && markers.Any(a => a.Id.EndsWith("/9")) && markers.All(a => !a.Navigate),
            "Enemy markers guessed from hostility or restricted OIDs.");
        f.Enabled = false;
        f.Sync();
        Check(!f.E.AmbientAoes().Any() && f.E.HopeAdds.Count == 0, "Disable retained markers or LB intentions.");
    }

    private static void NativeOutput()
    {
        var f = new Fixture();
        f.Pull();
        f.Cast(1, 10893, duration: 3);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        output.Apply(f.E, Start, false);
        var snapshot = safe.Manager.BuildSnapshot(Start);
        var player = f.Actors[1].Position;
        var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, Start);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0
            && Vector3.Distance(path[^1].Position, Center) >= 11.75f, "Real solver failed native missile escape.");
        output.Clear();
        Check(safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0, "Output owner clear retained danger.");
    }
}
