using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class RoleHeroesPretendersRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(676, 0, 41);

    public static void Run(Action<string, Action> run)
    {
        run("Role Heroes exact six components real polygon markers and unclaimed visuals", Catalog);
        run("Role Heroes ordinary R13 R4 observed finish not effects or prediction deadlines", Ordinary);
        run("Role Heroes flurry caster origin spell rotation three risky circles cached clocks", FlurryStart);
        run("Role Heroes flurry first axial match actual effect position and eight advances", FlurryEffects);
        run("Role Heroes flurry survives cancel caster death destruction and timeout", FlurryPersistence);
        run("Role Heroes Gold Dust exact two capacity inside outside and independent Acid Rain", PartyMarks);
        run("Role Heroes target death resurrection destruction frozen reference and missing start", TargetLifetime);
        run("Role Heroes arrow target bait source death retained dead target and tankbuster prediction", Arrow);
        run("Role Heroes primary completion latch raw new generation Director wipe and owner disable", Lifecycle);
        run("Role Heroes real public output solver and complete owner cleanup", NativeOutput);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(1218);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x428A, Center, InCombat: true, Hp: 1000, IsEnemy: true, ObjectIndex: 2),
            new(10, 0x233C, Center, Rotation: .9f),
            new(11, 0x233C, Center),
            new(12, 0x233C, Center),
            new(100, 0, Center + Vector3.UnitX * 10, PartySlot: 0, Player: true),
            new(101, 0, Center, PartySlot: 24, Buddy: true, FriendlyNpc: true),
            new(102, 0, Center + Vector3.UnitX, PartySlot: 1, Player: true),
        ];
        internal DateTime Now = Start;
        internal ulong Player = 100;
        internal bool Enabled = true;
        internal Fixture() => Sync();
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == Player).Position, Now,
            enabled: _ => Enabled, playerId: Player);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Cast(ulong source, uint action, ulong target = 0, double duration = 7, float rotation = 0)
        {
            var cast = new DungeonCastObservation(action, Center + Vector3.UnitZ * 12, rotation, Now,
                Now.AddSeconds(duration + .3), target, Actors.Single(a => a.Id == source).Position);
            Set(source, a => a with { Cast = cast });
            E.StartCast(source, cast, Now);
            Sync();
        }
        internal DungeonAmbientAoe[] Aoes(string prefix) => E.AmbientAoes().Where(a => a.Id.StartsWith(prefix)).ToArray();
    }

    private static void Catalog()
    {
        var f = new Fixture();
        Check(f.E.Active != null && f.E.Arena!.GetContours().Single().Count == 20
            && f.E.Arena.Position == Center && JobQuestCatalog.Owns(1218, 0x428A)
            && !JobQuestCatalog.Owns(1218, 0x428C), "Wrong primary scope or 20 vertex source polygon.");
        Check(f.E.Definitions.Single().Casts.Select(c => c.Action)
            .SequenceEqual(new uint[] { 37466, 37464, 37463, 37459, 37458, 37467 }), "Wrong activated action table.");
        foreach (var (id, oid) in new (ulong, uint)[] { (20, 0x428C), (21, 0x4208), (22, 0x428E), (23, 0x448B), (24, 0x4207) })
            f.Actors.Add(new(id, oid, Center, Rotation: .3f));
        f.Sync();
        Check(f.Aoes("job-quest/enemy/").Length == 6, "All drawing OIDs were confused with completion or primary ownership.");
        f.Cast(10, 37472);
        Check(!f.E.PredictedDamage.Any() && !f.E.OutputCasts.Any(), "Unactivated Apex Judgment gained fake raidwide output.");
    }

    private static void Ordinary()
    {
        var f = new Fixture();
        f.Cast(10, 37466, duration: 2);
        f.Cast(11, 37458, duration: 2);
        Check(f.E.OutputCasts.Count() == 2
            && f.E.OutputCasts.Single(c => c.Value.Rule.Action == 37466).Value.Rule.Shape is CircleTemplate { Radius: 13 },
            "Source R13 falloff threshold was replaced by R35.");
        f.E.Effect(10, 37466, 1, Start.AddSeconds(1));
        f.Now = Start.AddSeconds(20);
        f.Sync();
        Check(f.E.OutputCasts.Count() == 2, "Effects or prediction timeout ended ordinary live CastInfo.");
        f.Set(10, a => a with { Cast = null });
        f.Set(11, a => a with { Cast = null });
        f.Sync();
        Check(!f.E.OutputCasts.Any(), "Real raw observed endings failed.");
    }

    private static void FlurryStart()
    {
        var f = new Fixture();
        f.Cast(10, 37467, rotation: MathF.PI / 2);
        var line = f.E.HeroesFlurries.Single();
        var aoes = f.Aoes("heroes-pretenders/flurry/");
        Check(line.Source == 10 && line.Next == Center && line.Remaining == 8
            && Vector3.Distance(line.Advance, Vector3.UnitX * 5) < .0001f
            && aoes.Length == 3 && aoes.All(a => a.Navigate && a.Display)
            && aoes.Count(a => a.Color == new Vector4(1, 0, 0, 1)) == 1,
            "Flurry lost caster origin/spell rotation, 8 strikes, three risky circles or head color.");
        var clocks = aoes.Select(a => a.Activation).ToArray();
        f.Now = Start.AddSeconds(30);
        f.Sync();
        Check(f.Aoes("heroes-pretenders/flurry/").Select(a => a.Activation).SequenceEqual(clocks),
            "Future previews slide every update instead of source cache invalidation.");
    }

    private static void FlurryEffects()
    {
        var f = new Fixture();
        f.Cast(10, 37467, rotation: MathF.PI / 2);
        f.Cast(11, 37467, rotation: 0);
        f.Set(12, a => a with { Position = Center + new Vector3(.8f, 0, .8f) });
        f.Now = Start.AddSeconds(8);
        f.Sync();
        f.E.Effect(12, 37468, 1, f.Now, location: Center + Vector3.UnitZ * 30);
        var first = f.E.HeroesFlurries[0];
        Check(first.Remaining == 7 && f.E.HeroesFlurries[1].Remaining == 8
            && first.Next == f.Actors.Single(a => a.Id == 12).Position + first.Advance
            && first.Activation == f.Now.AddSeconds(1.1),
            "Flurry matched Euclidean distance/source/target instead of first axial caster position.");
        for (uint i = 2; i <= 8; ++i)
        {
            var next = f.E.HeroesFlurries[0].Next;
            f.Set(12, a => a with { Position = next });
            f.Sync();
            f.Now = f.Now.AddSeconds(2);
            f.E.Effect(12, 37467, i, f.Now);
        }
        Check(f.E.HeroesFlurries.Count == 1 && f.E.HeroesFlurries[0].Source == 11,
            "Eight real effects failed to retire only the matching line.");
    }

    private static void FlurryPersistence()
    {
        var f = new Fixture();
        f.Cast(10, 37467);
        f.Set(10, a => a with { Cast = null, Dead = true });
        f.Sync();
        f.Actors.RemoveAll(a => a.Id == 10);
        f.Now = Start.AddMinutes(5);
        f.Sync();
        Check(f.E.HeroesFlurries.Single().Remaining == 8 && f.Aoes("heroes-pretenders/flurry/").Length == 3,
            "Cancel/death/destruction/timeout consumed source effect-driven line.");
    }

    private static void PartyMarks()
    {
        var f = new Fixture();
        f.Cast(10, 37464, 101);
        Check(f.Aoes("quest/stack/").Any(a => a.Id.EndsWith("/avoid") && a.Shape is CircleTemplate { Radius: 8 }),
            "Outside viewer entered an already full two-person stack.");
        f.Set(102, a => a with { Position = Center + Vector3.UnitZ * 12 });
        f.Sync();
        Check(f.Aoes("quest/stack/").Any(a => a.Id == "quest/stack/join"), "One-person stack failed join navigation.");
        f.Set(100, a => a with { Position = Center + Vector3.UnitZ });
        f.Sync();
        Check(f.Aoes("quest/stack/").Any(a => a.Id == "quest/stack/join"), "Inside viewer was evicted from exact two capacity.");
        f.Set(100, a => a with { Position = Center + Vector3.UnitX * 10 });
        f.Cast(11, 37463, 100);
        Check(f.Aoes("quest/stack/").Any(a => a.Id == "quest/stack/join")
            && f.Aoes("quest/spread/").Any(a => a.Id.Contains("/separate/101") && a.Shape is CircleTemplate { Radius: 9 })
            && !f.E.PredictedDamage.Any(), "Independent Gold Dust/Acid Rain constraints were merged or invented damage prediction.");
        f.Player = 101;
        f.Sync();
        var buddy = f.Aoes("quest/stack/").Single(a => a.Id.EndsWith("/buddy"));
        Check(!buddy.Shape.Resolve(buddy.Position, buddy.Rotation, null).Contains(f.Actors.Single(a => a.Id == 100).Position),
            "Acid Rain target was excluded from independent Gold Dust R4 buddies.");
        var g = new Fixture();
        g.Set(101, a => a with { Position = Center - Vector3.UnitX * 6 });
        g.Set(102, a => a with { Position = Center + Vector3.UnitX * 6 });
        g.Set(100, a => a with { Position = Center + Vector3.UnitZ * 12 });
        g.Cast(10, 37464, 101);
        g.Cast(11, 37464, 102);
        var join = g.Aoes("quest/stack/").Single(a => a.Id == "quest/stack/join");
        var geometry = join.Shape.Resolve(join.Position, join.Rotation, null);
        Check(geometry.Contains(Center) && !geometry.Contains(Center - Vector3.UnitX * 6),
            "Multiple Gold Dust candidates accepted overlap instead of exactly one stack.");
    }

    private static void TargetLifetime()
    {
        var f = new Fixture();
        f.Cast(10, 37464, 101);
        f.Set(101, a => a with { Dead = true });
        f.Sync();
        Check(f.Aoes("quest/stack/").Length == 0, "Dead stack target remained active.");
        f.Set(101, a => a with { Dead = false });
        f.Sync();
        Check(f.Aoes("quest/stack/").Length > 0, "Same actor revival did not restore stored mark.");
        f.Actors.RemoveAll(a => a.Id == 101);
        f.Sync();
        f.Actors.Add(new(101, 0, Center + Vector3.UnitX * 12, PartySlot: 24, Buddy: true));
        f.Sync();
        Check(Vector3.Distance(f.Aoes("quest/stack/").Single(a => a.Id.EndsWith("/display")).Position, Center) < .05,
            "Destroyed old target reference rebound to same ID replacement.");
        f.Cast(11, 37463, 999);
        Check(f.Aoes("quest/spread/").Length == 0, "Missing cast target created an inferred mark.");
    }

    private static void Arrow()
    {
        var f = new Fixture();
        var rawTarget = Center + new Vector3(.12345f, 0, .23456f);
        f.Set(101, a => a with { Position = rawTarget });
        f.Cast(10, 37459, 101);
        Check(f.Aoes("heroes-pretenders/arrow/").Any(a => a.Id.EndsWith("/avoid"))
            && f.E.PredictedDamage.Single().DamageType == "Tankbuster"
            && f.Aoes("heroes-pretenders/arrow/").Single(a => a.Id.EndsWith("/display")).Position == rawTarget,
            "Arrow lost raw target origin, target bait or target-only prediction.");
        f.Player = 101;
        f.Sync();
        Check(!f.Aoes("heroes-pretenders/arrow/").Any(a => a.Id.EndsWith("/avoid"))
            && f.Aoes("heroes-pretenders/arrow/").Any(a => a.Id.Contains("/party/100")), "Own arrow avoided itself or lost party avoidance.");
        f.Set(101, a => a with { Dead = true });
        f.Sync();
        Check(f.Aoes("heroes-pretenders/arrow/").Length > 0 && f.E.PredictedDamage.Any(), "Arrow incorrectly excluded dead target.");
        f.Set(10, a => a with { Dead = true });
        f.Sync();
        Check(f.Aoes("heroes-pretenders/arrow/").Length == 0, "Dead source arrow remained active.");
        f.Set(10, a => a with { Cast = null });
        f.Sync();
        Check(!f.E.PredictedDamage.Any(), "Real arrow finish retained prediction.");
        var g = new Fixture();
        g.Cast(10, 37459, 101, duration: 3);
        var firstClock = g.E.PredictedDamage.Single().Activation;
        g.Now = Start.AddSeconds(1);
        g.Cast(11, 37459, 100, duration: 8);
        g.Set(10, a => a with { Dead = true });
        g.Sync();
        Check(g.E.PredictedDamage.Single().TargetId == 100 && g.E.PredictedDamage.Single().Activation == firstClock,
            "Bait damage mask used per-active-target clocks instead of first stored bait clock.");
        var h = new Fixture();
        h.Cast(10, 37459, 101);
        var cast = h.Actors.Single(a => a.Id == 10).Cast!;
        h.Actors.RemoveAll(a => a.Id == 101);
        h.Sync();
        h.E.StartCast(10, cast with { Location = cast.Location + Vector3.UnitX }, h.Now);
        Check(h.Aoes("heroes-pretenders/arrow/").Length == 2,
            "Same cast packet correction erased a destroyed target's stored bait.");
        h.Actors.Add(new(101, 0, Center, PartySlot: 24, Buddy: true));
        h.Set(100, a => a with { Position = Center + Vector3.UnitZ });
        h.Player = 101;
        h.Sync();
        var hints = h.E.Hints(Center).ToArray();
        Check(hints.Contains("Bait away from raid!") && !hints.Contains("GTFO from baited aoe!")
            && h.Aoes("heroes-pretenders/arrow/").Any(a => a.OutlineOnly && a.Id.EndsWith("/display"))
            && h.Aoes("heroes-pretenders/arrow/").Any(a => a.Id.EndsWith("/avoid")),
            "Same ID replacement conflated ID-based text/drawing with reference-based navigation.");
    }

    private static void Lifecycle()
    {
        var f = new Fixture();
        f.Cast(10, 37467);
        f.Set(1, a => a with { Dead = true, Hp = 0 });
        f.Sync();
        Check(f.E.Active == null && !f.E.HeroesFlurries.Any(), "Primary death failed trivial phase cleanup.");
        f.Set(1, a => a with { Dead = false, Hp = 1000 });
        f.Sync();
        Check(f.E.Active == null, "Healed completed primary repulled without raw new generation.");
        var primary = f.Actors.Single(a => a.Id == 1);
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        f.Actors.Add(primary);
        f.Sync();
        Check(f.E.Active != null, "Real primary new raw generation did not repull.");
        f.E.DirectorUpdate(0x40000005);
        f.Sync();
        Check(f.E.Active == null, "Director wipe repulled stale raw.");
        f.E.DirectorUpdate(0x40000011);
        f.Sync();
        Check(f.E.Active != null, "Director reopen failed valid pull.");
        f.Enabled = false;
        f.Sync();
        Check(f.E.Active == null && !f.E.AmbientAoes().Any(), "Mechanism disabled retained owner output.");
    }

    private static void NativeOutput()
    {
        var f = new Fixture();
        f.Set(100, a => a with { Position = Center + Vector3.UnitZ * 12 });
        f.Cast(10, 37466);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        output.Apply(f.E, Start, false);
        var snapshot = safe.Manager.BuildSnapshot(Start);
        var player = f.Actors.Single(a => a.Id == 100).Position;
        var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, Start);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0
            && Vector3.Distance(path[^1].Position, Center + Vector3.UnitZ * 12) >= 13,
            "Real solver failed R13 source falloff avoidance.");
        output.Clear();
        Check(safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0, "Stopped owner retained danger.");
    }
}
