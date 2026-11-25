using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class EurekaOrthosRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-300, 0, -300);
    private sealed class Fixture(uint territory = 1100)
    {
        internal readonly DungeonEncounter E = new(territory);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, territory == 1099 ? 0x3D54u : 0x3E77u, Center, InCombat: true, Hp: 100),
            new(2, 0x233C, Center),
            new(100, 0, Center + Vector3.UnitZ * 5, PartySlot: 0, Player: true),
        ];
        internal DateTime Now = Start;
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Sync(Func<string, bool>? enabled = null, bool viewer = true) =>
            E.Synchronize(Actors, viewer ? Actors.First(a => a.Id == 100).Position : null, Now, enabled, viewer ? 100ul : 999ul);
        internal void Cast(ulong source, uint action, double duration = 3)
        {
            Now = Now.AddSeconds(.01);
            Set(source, a => a with { Cast = new(action, a.Position, a.Rotation, Now, Now.AddSeconds(duration + .3), 100, TotalTime: (float)duration) });
            Sync();
        }
        internal void Effect(ulong source = 2, uint sequence = 7) => E.Effect(source, 32552, sequence, Now);
        internal void Add(ulong id, Vector3? position = null) => Actors.Add(new(id, 0x3D55, position ?? Center));
    }
    internal static void Run(Action<string, Action> run)
    {
        run("Eureka exact territories OIDs source geometries precombat scope and inactive enum boundaries", Catalog);
        run("Eureka10 six insertion-order mines hidden tail survives timeout dead source and promotes", Mines);
        run("Eureka10 observed Finish true raw replacement missing source and late old cancellation", Timing);
        run("Eureka first raw authority corrects packet without fake Finish FIFO or mine reorder", FirstRaw);
        run("Eureka20 effect-created locked cone consumption clock repeats zero sequence and no bootstrap guess", Events);
        run("Eureka20 exact source rounding far diagonal special corner strict 25 boundary", Positions);
        run("Eureka20 strict first-clock batch prefix no sorting capacity cap or timeout", Batches);
        run("Eureka20 arbitrary helper actual Finish cancellation replacement and destroy global FIFO", Finishes);
        run("Eureka no disengage or missing-view reset primary completion new generation and owner disable", Lifecycle);
        run("Eureka source party cold activation missing viewer output clear and Director unload identity", Director);
        run("Eureka adapter complete native effect excludes legacy targets and fake teleport mechanism", Adapter);
        run("Eureka production output future danger preview real solver escape and owner cleanup", Navigation);
        run("Eureka Flame Breath warning excludes hidden batch and disabled mechanism", Warning);
    }
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static Vector3 Quantized(Vector3 p)
    {
        const float unit = 2000f / 65535f;
        const float inverse = 1f / unit;
        return new(((int)MathF.Round(p.X * inverse) - .5f) * unit, p.Y,
            ((int)MathF.Round(p.Z * inverse) - .5f) * unit);
    }
    private static void Catalog()
    {
        foreach (var territory in new uint[] { 1099, 1100 })
        {
            var f = new Fixture(territory);
            f.Set(1, a => a with { InCombat = false });
            f.Sync();
            Check(f.E.Active == null && f.E.AmbientAoes().All(a => !a.Navigate)
                && f.E.AmbientAoes().Any(a => a.Id == "eureka/precombat/arena"), "Precombat navigation invented.");
            var oid = territory == 1099 ? 0x3D54u : 0x3E77u;
            Check(ArrDungeonCatalog.Owns(oid, territory) && !EurekaOrthosCatalog.Owns(territory, 0x3E78)
                && !EurekaOrthosCatalog.IsTerritory(897), "CFC/helper ownership expanded.");
            f.Set(1, a => a with { InCombat = true });
            f.Sync();
            foreach (var action in new uint[] { 31477, 32543, 32544, 32545, 32554 })
                f.E.StartCast(2, new(action, Center, 0, Start, Start.AddSeconds(3)), Start);
            Check(f.E.Casts.Count == 0, "Unactivated enum became mechanism.");
        }
        Check(EurekaOrthosCatalog.Create(1099).Single().Casts.Single(c => c.Action == 31479).Shape == new CircleTemplate(20)
            && EurekaOrthosCatalog.Create(1100).Single().Casts.Single(c => c.Action == 32547).Shape == RayRectangleTemplate.Symmetric(5, 40),
            "Source actual dimensions replaced by comments.");
    }
    private static void Mines()
    {
        var f = new Fixture(1099);
        f.Sync();
        for (ulong i = 10; i < 18; ++i) { f.Add(i); f.Cast(i, 32700, duration: 30 - i); }
        Check(f.E.Casts.Count == 8 && f.E.OutputCasts.Select(p => p.Value.Source).SequenceEqual(Enumerable.Range(10, 6).Select(i => (ulong)i)),
            "Six mine display/navigation prefix sorted by finish or capped storage.");
        f.Now = Start.AddSeconds(100);
        f.Set(10, a => a with { Dead = true });
        f.Sync();
        Check(f.E.OutputCasts.Count() == 6, "Death/timeout cleared source SimpleAOEs.");
        f.E.Finish(11, 32700, f.Now);
        Check(f.E.OutputCasts.Select(p => p.Value.Source).SequenceEqual(new ulong[] { 10, 12, 13, 14, 15, 16 }), "Hidden tail failed promotion.");
        f.Cast(11, 32700);
        f.E.Finish(10, 32700, f.Now);
        Check(f.E.OutputCasts.Select(p => p.Value.Source).SequenceEqual(new ulong[] { 12, 13, 14, 15, 16, 17 }),
            "Reused dictionary slot jumped queue.");
        for (ulong i = 30; i < 38; ++i) { f.Add(i); f.Cast(i, 31478); }
        Check(f.E.OutputCasts.Count(p => p.Value.Rule.Action == 31478) == 8, "Unlimited first mine action capped.");
    }
    private static void Timing()
    {
        var f = new Fixture(1099);
        f.Sync();
        f.Cast(2, 31478);
        var old = f.E.Casts[2];
        f.Set(2, a => a with { Cast = a.Cast! with { TargetId = 101 } });
        f.Now = f.Now.AddSeconds(.1);
        f.Sync();
        f.E.Finish(2, 31478, old.StartedAt);
        Check(f.E.Casts.Count == 1 && f.E.Casts[2].TargetId == 101, "Stale cancel ended replacement.");
        f.Set(2, a => a with { Cast = null });
        f.Sync();
        Check(f.E.Casts.Count == 0, "True short cancellation retained cast.");
        f.Cast(2, 31478);
        f.Actors.RemoveAll(a => a.Id == 2);
        f.Sync();
        Check(f.E.Casts.Count == 0, "Missing helper did not Finish.");
    }
    private static void Events()
    {
        var f = new Fixture();
        f.Sync();
        f.Cast(2, 32864);
        Check(f.E.EurekaBreaths.Count == 0, "Raw breath invented event history.");
        f.Now = Start.AddSeconds(5);
        f.Effect(); f.Effect(); f.Effect(sequence: 0); f.Effect(sequence: 0);
        Check(f.E.EurekaBreaths.Count == 4 && f.E.EurekaBreaths.All(b => b.PreparedAt == f.Now
            && b.Activation == f.Now.AddSeconds(9.6)), "Native packets deduplicated or wrong event clock.");
        var first = f.E.EurekaBreaths[0];
        f.Set(2, a => a with { Position = Center + Vector3.UnitX * 20, Rotation = 2 });
        f.Sync();
        Check(f.E.EurekaBreaths[0] == first, "Queued geometry followed actor.");
    }
    private static void FirstRaw()
    {
        var f = new Fixture();
        f.Sync(); f.Effect(); f.Effect();
        var raw = new DungeonCastObservation(32864, Center, 0, Start, Start.AddSeconds(3.8), 100, TotalTime: 3.5f);
        f.E.StartCast(2, raw with { Location = Center + Vector3.UnitX, FinishAt = Start.AddSeconds(8) }, Start, fromPacket: true);
        f.Set(2, a => a with { Cast = raw });
        f.Sync();
        Check(f.E.EurekaBreaths.Count == 2 && f.E.Casts[2].Observed
            && f.E.Casts[2].Location == Center && f.E.Casts[2].FinishAt == raw.FinishAt,
            "First raw authority synthesized a Finish or kept packet fields.");
        f.E.StartCast(2, raw with { Location = Center + Vector3.UnitX, FinishAt = Start.AddSeconds(8) }, Start, fromPacket: true);
        f.Sync();
        Check(f.E.EurekaBreaths.Count == 2 && f.E.Casts[2].Observed && f.E.Casts[2].Location == Center,
            "Delayed packet with large duration difference replaced authoritative raw.");
        f.Set(2, a => a with { Cast = raw with { TargetId = 101 } });
        f.Now = Start.AddSeconds(.1); f.Sync();
        Check(f.E.EurekaBreaths.Count == 1, "Subsequent true raw replacement failed Finish.");
        var m = new Fixture(1099);
        m.Sync();
        for (ulong i = 10; i < 17; ++i) m.Add(i);
        m.Sync();
        for (ulong i = 10; i < 17; ++i)
        {
            var input = new DungeonCastObservation(32700, Center, 0, Start, Start.AddSeconds(3.3), 100);
            m.E.StartCast(i, input with { Location = i == 10 ? Center + Vector3.UnitX : Center }, Start, fromPacket: true);
            m.Set(i, a => a with { Cast = input });
        }
        m.Sync();
        Check(m.E.OutputCasts.Select(p => p.Value.Source).SequenceEqual(new ulong[] { 10, 11, 12, 13, 14, 15 })
            && m.E.Casts.Values.All(c => c.Location == Center && c.Observed), "First raw confirmation reordered mine prefix.");
        var early = new Fixture();
        early.Sync(); early.Effect(); early.Effect();
        early.E.StartCast(2, raw, Start, fromPacket: true);
        early.Set(2, a => a with { Cast = raw with { StartedAt = Start.AddSeconds(-1) } });
        early.Sync();
        Check(early.E.Casts[2].Observed && early.E.Casts[2].StartedAt == Start.AddSeconds(-1)
            && early.E.EurekaBreaths.Count == 2, "First raw earlier inferred start was rejected.");
        var unconfirmed = new Fixture();
        unconfirmed.Sync(); unconfirmed.Effect();
        unconfirmed.E.StartCast(2, raw, Start, fromPacket: true);
        unconfirmed.Now = Start.AddSeconds(1); unconfirmed.Sync();
        Check(unconfirmed.E.EurekaBreaths.Count == 1, "Unconfirmed packet timeout synthesized source Finish.");
    }
    private static void Positions()
    {
        foreach (var (position, rotation, expected) in new[]
        {
            (new Vector3(-331, 0, -331), MathF.PI / 4, new Vector3(-315.5f, 0, -315.5f)),
            (new Vector3(-315, 0, -315), 0f, new Vector3(-315, 0, -315)),
            (new Vector3(-315, 0, -320), 0f, new Vector3(-315, 0, -320)),
            (new Vector3(-330, 0, -300), MathF.PI / 2, new Vector3(-308, 0, -300)),
        })
        {
            var f = new Fixture();
            f.Set(2, a => a with { Position = position, Rotation = rotation });
            f.Sync(); f.Effect();
            Check(f.E.EurekaBreaths.Single().Origin == Quantized(expected), "Exact cast projection/round/quantize boundary differs.");
        }
    }
    private static void Batches()
    {
        var f = new Fixture();
        f.Sync(); f.Effect();
        for (var i = 0; i < 7; ++i) { f.Now = Start.AddSeconds(.99); f.Effect(); }
        f.Now = Start.AddSeconds(1); f.Effect();
        f.Now = Start.AddSeconds(.5); f.Effect();
        Check(f.E.EurekaBreaths.Count == 10 && f.E.AmbientAoes().Count(a => a.Id.StartsWith("eureka/breath")) == 8,
            "Strict one-second prefix was sorted, max5 truncated or equality included.");
        f.Now = Start.AddSeconds(100); f.Sync();
        Check(f.E.EurekaBreaths.Count == 10, "Predictions expired without true Finish.");
        f.Cast(2, 32864); f.E.Finish(2, 32864, f.Now);
        Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("eureka/breath")) == 9, "Next batch did not promote from new head clock.");
    }
    private static void Finishes()
    {
        var f = new Fixture();
        f.Sync(); f.Effect(); f.Effect(); f.Effect();
        f.Add(9); f.Cast(9, 32864);
        f.Set(9, a => a with { Cast = null }); f.Sync();
        Check(f.E.EurekaBreaths.Count == 2, "Other helper cancellation did not pop global head.");
        f.Cast(9, 32864);
        f.Set(9, a => a with { Cast = a.Cast! with { Action = 999999 } }); f.Sync();
        Check(f.E.EurekaBreaths.Count == 1, "Unclaimed raw replacement did not finish old breath.");
        f.Cast(9, 32864);
        f.Actors.RemoveAll(a => a.Id == 9); f.Sync();
        Check(f.E.EurekaBreaths.Count == 0, "Destroy casting helper did not pop head.");
    }
    private static void Lifecycle()
    {
        var f = new Fixture();
        f.Sync(); f.Effect();
        f.Set(1, a => a with { InCombat = false }); f.Sync();
        Check(f.E.Active != null && f.E.EurekaBreaths.Count == 1 && f.E.PreserveStageAcrossBattleEnd, "Disengage reset components.");
        f.Sync(viewer: false);
        Check(f.E.Active != null && f.E.EurekaBreaths.Count == 1, "Missing viewer reset world history.");
        f.Set(1, a => a with { Hp = 0 }); f.Sync();
        f.Set(1, a => a with { Hp = 100, InCombat = true }); f.Sync();
        Check(f.E.Active == null, "Finished original Actor revived module.");
        f.Actors.RemoveAll(a => a.Id == 1); f.Sync();
        f.Actors.Add(new(1, 0x3E77, Center, InCombat: true, Hp: 100)); f.Sync();
        Check(f.E.Active != null, "New same-ID actor cannot start.");
        f.Effect(); f.Sync(_ => false);
        Check(f.E.Active == null && f.E.EurekaBreaths.Count == 0 && !f.E.OutputCasts.Any()
            && !f.E.AmbientAoes().Any(), "Disabled owner retained output.");
    }
    private static void Adapter()
    {
        var f = new Fixture();
        f.Sync();
        var adapter = new DungeonEventAdapter(f.E, () => f.Sync(), _ => 0, _ => { });
        adapter.Handle(new NativeActionEffectEvent { SourceId = 2, ActionId = 32552, Targets = [], Timestamp = Start.AddSeconds(-10) }, Start);
        adapter.Handle(new PromeRotation.LogSystem.LogSystemActionEffectEvent { SourceId = 2, ActionId = 32552, TargetId = 100 }, Start);
        Check(f.E.EurekaBreaths.Count == 1 && f.E.EurekaBreaths[0].PreparedAt == Start,
            "Flattened target duplicated event or used packet timestamp instead of source consumption clock.");
        Check(!f.E.NativeTeleporters.Any(), "NPC Teleport became player transfer.");
    }
    private static void Director()
    {
        var f = new Fixture();
        f.Sync(viewer: false); f.Effect();
        Check(f.E.Active != null && f.E.EurekaBreaths.Count == 1, "Cold missing viewer lost source-party mechanism.");
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        f.Sync(); output.Apply(f.E, Start, false); output.Clear(); output.Apply(f.E, Start, false);
        Check(f.E.EurekaBreaths.Count == 1 && safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 1,
            "Pure output withdrawal lost source history.");
        f.E.DirectorUpdate(0x40000005); f.Sync();
        Check(f.E.Active == null && f.E.EurekaBreaths.Count == 0, "Wipe failed unloading components.");
        f.E.DirectorUpdate(0x40000011); f.Sync();
        Check(f.E.Active == null, "Resume reconstructed the unloaded old Actor.");
        f.Actors.RemoveAll(a => a.Id == 1); f.Sync();
        f.Actors.Add(new(1, 0x3E77, Center, InCombat: true, Hp: 100)); f.Sync();
        Check(f.E.Active != null, "Genuine new generation did not load.");
        var pending = new Fixture();
        pending.Set(1, a => a with { InCombat = false }); pending.Sync();
        pending.E.DirectorUpdate(0x40000005); pending.Set(1, a => a with { InCombat = true });
        pending.E.DirectorUpdate(0x40000011); pending.Sync();
        Check(pending.E.Active == null, "Precombat loaded Actor survived ForceUnload.");
        pending.Actors.RemoveAll(a => a.Id == 1); pending.Sync();
        pending.E.DirectorUpdate(0x40000005);
        pending.Actors.Add(new(3, 0x3E77, Center, InCombat: true, Hp: 100)); pending.Sync();
        Check(pending.E.Active == null, "New Actor updated during wiping.");
        pending.E.DirectorUpdate(0x40000011); pending.Sync();
        Check(pending.E.Active != null && pending.E.PrimaryId == 3, "New Actor created during wiping failed resume.");
        var distant = new Fixture();
        distant.Set(1, a => a with { InCombat = false }); distant.Sync();
        distant.Set(100, a => a with { Position = Center + Vector3.UnitX * 501 }); distant.Sync();
        distant.E.DirectorUpdate(0x40000005); distant.E.DirectorUpdate(0x40000011);
        distant.Set(100, a => a with { Position = Center });
        distant.Set(1, a => a with { InCombat = true }); distant.Sync();
        Check(distant.E.Active != null, "Out-of-range pending Actor was incorrectly wiped as loaded.");
    }
    private static void Navigation()
    {
        var f = new Fixture();
        f.Sync(); f.Effect();
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
        var draw = new ScriptDrawApi();
        var output = new DungeonOutput(safe, draw, safe.Manager);
        output.Apply(f.E, Start, false);
        var snapshot = safe.Manager.BuildSnapshot(Start);
        Check(snapshot.DangerAreas.Count == 1 && !snapshot.GoalAreas.Any() && !snapshot.HardObstacles.Any()
            && draw.Previews.Values.Any(p => p.Id.Contains("eureka/breath")), "Model did not reach production output.");
        Check(f.E.Hints(Center + Vector3.UnitZ * 5).Contains("离开范围！"), "Flame Breath lost source GenericAOEs warning.");
        Check(!f.E.Hints(f.E.EurekaBreaths[0].Origin).Contains("离开范围！")
            && !f.E.Hints(Center - Vector3.UnitZ * 5).Contains("离开范围！"), "Flame Breath warning contains source origin or outside cone.");
        var result = new SafeZoneSolver().Solve(snapshot,
            new PlayerState(Center + Vector3.UnitZ * 5, 0, 6, false, false, 0, true, snapshot.Version),
            new MovementCapability(6, 6, 0, false, 6, 6), new SolveOptions(), Start);
        Check(result.IsSuccess && !result.IsLeastDangerFallback && result.ExecutablePath.Count + result.ContinuationPath.Count > 0,
            "Production solver could not escape future breath.");
        output.Clear();
        Check(!draw.Previews.Any() && safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0, "Owner clear retained output.");
    }
    private static void Warning()
    {
        var f = new Fixture();
        f.Sync(); f.Effect();
        f.Now = Start.AddSeconds(1);
        f.Set(2, a => a with { Rotation = MathF.PI }); f.Sync(); f.Effect();
        var behind = f.E.EurekaBreaths[0].Origin - Vector3.UnitZ * 5;
        Check(!f.E.Hints(behind).Contains("离开范围！"), "Hidden next batch produced warning.");
        f.Cast(2, 32864); f.Set(2, a => a with { Cast = null }); f.Sync();
        Check(f.E.Hints(behind).Contains("离开范围！"), "Promoted batch lost warning.");
        f.Sync(_ => false);
        Check(!f.E.Hints(behind).Contains("离开范围！"), "Disabled mechanism retained warning.");
    }
}
