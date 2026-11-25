using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using MasterPlayer.Scripting;

internal static class MainScenarioMaxRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(295, 0, -22);
    public static void Run(Action<string, Action> run)
    {
        run("MSQ MAX exact three activated AOEs and primary scoped ownership in overworld", Catalog);
        run("MSQ MAX precombat native outline and exact colored actor groups without navigation", Precombat);
        run("MSQ MAX targetable combat pull completion healing create and same ID generations", Lifecycle);
        run("MSQ MAX primary HP0 at first pull and Director wipe do not revive old state", FirstAndWipe);
        run("MSQ MAX ordinary casts lock snapshots retain effects and observed cancel barrier", Casts);
        run("MSQ MAX base target priority honors Fate eligibility pending dead and native enemies", Priority);
        run("MSQ MAX mechanism disable clears combat and precombat output", Disabled);
        run("MSQ MAX real public output keeps precombat display separate and solver escapes shock", NativeOutput);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(141);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x252B, Center, InCombat: false, Hp: 1000, IsEnemy: true, ObjectIndex: 2),
            new(100, 0, Center + Vector3.UnitZ, PartySlot: 0, Player: true),
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
        internal DungeonAmbientAoe[] Aoes() => E.AmbientAoes().Where(a => a.Id.StartsWith("max/")).ToArray();
    }

    private static void Catalog()
    {
        var f = new Fixture();
        Check(f.E.Definitions.Single().Casts.Select(c => c.Action).SequenceEqual(new uint[] { 14601, 14600, 14599 })
            && f.E.Definitions.Single().Casts[0].Shape is FanTemplate { Radius: 10 }
            && MainScenarioQuestCatalog.Owns(141, 0x252B) && !MainScenarioQuestCatalog.Owns(141, 0x252A)
            && !MainScenarioQuestCatalog.Owns(140, 0x252B), "Wrong activated actions or overworld ownership.");
    }

    private static void Precombat()
    {
        var f = new Fixture();
        f.Actors.Add(new(2, 0x2529, Center + Vector3.UnitX * 30, FriendlyNpc: true, IsEnemy: false));
        f.Actors.Add(new(3, 0x252A, Center + Vector3.UnitX * 2));
        f.Actors.Add(new(4, 0x252B, Center + Vector3.UnitZ * 10));
        f.Actors.Add(new(5, 0x233C, Center));
        f.Sync();
        var aoes = f.Aoes();
        Check(f.E.Active == null && f.E.Arena == null && aoes.Length == 4
            && aoes.All(a => !a.Navigate), "Precombat markers created navigation or accepted extra Boss/helper.");
        Check(aoes.Single(a => a.Id == "max/enemy/2") is { OutlineOnly: true, Color: { } color }
            && color == new Vector4(1, 0, 1, 1)
            && aoes.Single(a => a.Id == "max/enemy/3").Color == new Vector4(1, 128f / 255, 0, 1),
            "Noctis ally/color, boundary clamp, or Trooper object color changed.");
        f.Set(2, a => a with { Targetable = false });
        f.Set(3, a => a with { Targetable = false });
        f.Sync();
        Check(f.Aoes().Length == 2, "Object color bypassed targetable filter.");
        f.Set(1, a => a with { Dead = true, Targetable = false });
        f.Sync();
        Check(f.Aoes().Any(a => a.Id == "max/enemy/4"),
            "Old corpse hid the later live primary before pull.");
        f.Set(1, a => a with { Dead = false });
        f.Sync();
        Check(f.Aoes().Any(a => a.Id == "max/enemy/4") && !f.Aoes().Any(a => a.Id == "max/enemy/1"),
            "Untargetable old primary displaced the targetable precombat module.");
    }

    private static void Lifecycle()
    {
        var f = new Fixture();
        f.Set(1, a => a with { Targetable = false, InCombat = true });
        f.Sync();
        Check(f.E.Active == null, "Untargetable primary pulled.");
        f.Set(1, a => a with { Targetable = true });
        f.Sync();
        Check(f.E.Active != null && f.E.Arena!.Position == Center, "Combat targetable primary did not pull.");
        f.Set(1, a => a with { InCombat = false });
        f.Sync();
        Check(f.E.Active != null, "Combat loss reset ongoing default quest phase.");
        f.Set(1, a => a with { Hp = 0 });
        f.Sync();
        f.Set(1, a => a with { Hp = 1000, InCombat = true });
        f.Sync();
        Check(f.E.Active == null && !f.Aoes().Any(), "Completed same actor revived by healing.");
        f.E.ObjectCreated(1, f.Now, 0x252B);
        f.Sync();
        Check(f.E.Active != null, "New primary Create failed completed ID unlock.");
        f.E.RemoveActor(1);
        f.Sync();
        Check(f.E.Active != null, "Explicit destroy followed by a new object with same ID could not pull.");
    }

    private static void FirstAndWipe()
    {
        var f = new Fixture();
        f.Set(1, a => a with { InCombat = true, Hp = 0 });
        f.Sync();
        f.Set(1, a => a with { Hp = 1000 });
        f.Sync();
        Check(f.E.Active == null, "First HP0 pull resurrected completed primary.");
        f.E.ObjectCreated(1, f.Now, 0x252B);
        f.Sync();
        f.E.DirectorUpdate(0x40000005);
        f.Sync();
        Check(f.E.Active == null && !f.Aoes().Any(), "Wipe repulled or retained precombat output.");
        f.E.DirectorUpdate(0x40000011);
        f.Sync();
        Check(f.E.Active != null, "Wipe reopen failed fresh valid phase.");
    }

    private static void Casts()
    {
        var f = new Fixture();
        f.Pull();
        f.Actors.Add(new(10, 0x233C, Center));
        f.Sync();
        f.E.StartCast(10, new(14599, Center + Vector3.UnitX * 2, 0, Start, Start.AddSeconds(3)), Start);
        var cast = f.E.Casts[10];
        f.Set(10, a => a with { Position = a.Position + Vector3.UnitZ * 10,
            Cast = new(14599, cast.Location, cast.Rotation, cast.StartedAt, cast.FinishAt), Dead = true });
        f.Sync();
        f.E.Effect(10, 14599, 1, Start.AddSeconds(1));
        f.E.Tick(Start.AddSeconds(20));
        Check(f.E.OutputCasts.Single().Value.Location == cast.Location, "Effect/death/deadline relocked or consumed ordinary cast.");
        f.Now = Start.AddSeconds(20);
        f.Set(10, a => a with { Cast = null });
        f.Sync();
        Check(!f.E.OutputCasts.Any(), "Real observed cancel left AOE.");
        f.E.StartCast(10, new(14599, cast.Location, 0, Start, cast.FinishAt), f.Now);
        Check(!f.E.OutputCasts.Any(), "Stale packet revived cancelled cast.");
        f.E.StartCast(10, new(14598, Center, 0, Start, Start.AddSeconds(3)), f.Now);
        Check(!f.E.OutputCasts.Any(), "Inactive visual action invented danger.");
    }

    private static void Priority()
    {
        var f = new Fixture();
        f.Pull();
        f.E.AllowedFateId = 1409;
        f.Set(1, a => a with { FateId = 1409, AggroPlayer = true });
        f.Actors.Add(new(2, 0x2529, Center, FriendlyNpc: true, IsEnemy: false, ObjectIndex: 4, InCombat: true));
        f.Actors.Add(new(3, 0x252A, Center, IsEnemy: true, ObjectIndex: 6, FateId: 99, AggroPlayer: true, Hp: 100));
        f.Sync();
        Check(f.E.PriorityTarget(3, Center) == 1, "FATE mismatch or friendly marker became attack candidate.");
        Check(f.E.ForbiddenDotTargets.SequenceEqual(new ulong[] { 1 })
            && f.E.Hints(Center).Any(h => h.Contains("持续伤害"))
            && !f.E.ForbiddenTargets.Contains(1ul), "Synced non-boss FATE lost DoT intention or became full target ban.");
        f.E.AllowedFateId = null;
        Check(f.E.ForbiddenDotTargets.Length == 0, "Leaving level sync retained the FATE DoT intention.");
        f.E.AllowedFateId = 1409;
        f.Set(1, a => a with { Hp = 1 });
        f.Sync();
        Check(f.E.PriorityTarget(1, Center) == 0, "Default target rule ignored pending dead.");
    }

    private static void Disabled()
    {
        var f = new Fixture();
        f.Enabled = false;
        f.Sync();
        Check(!f.Aoes().Any(), "Disabled precombat output persisted.");
        f.Enabled = true;
        f.Pull();
        f.Enabled = false;
        f.Sync();
        Check(f.E.Active == null && !f.Aoes().Any(), "Disabled active mechanism retained output.");
    }

    private static void NativeOutput()
    {
        var f = new Fixture();
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
        var draw = new ScriptDrawApi();
        var output = new DungeonOutput(safe, draw, safe.Manager);
        output.Apply(f.E, Start, false);
        var preview = safe.Manager.BuildSnapshot(Start);
        Check(preview.ArenaBoundary == null && preview.DangerAreas.Count == 0, "Precombat display added hard arena/navigation.");
        f.Pull();
        f.E.StartCast(1, new(14600, Center, 0, Start, Start.AddSeconds(3)), Start);
        output.Apply(f.E, Start, false);
        var snapshot = safe.Manager.BuildSnapshot(Start);
        var player = f.Actors[1].Position;
        var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, Start);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        var final = path.Count > 0 ? path[^1].Position : player;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback
            && Vector2.Distance(new(final.X, final.Z), new(Center.X, Center.Z)) >= 10,
            "Real solver failed Shock escape.");
        output.Clear();
        Check(safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0, "Owner cleanup retained danger.");
    }
}
