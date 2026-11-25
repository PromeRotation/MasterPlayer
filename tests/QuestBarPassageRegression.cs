using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;

internal static partial class QuestBarPassageRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(0, 0, 28.5f);

    internal static void Run(Action<string, Action> run)
    {
        RunP3(run);
        run("Quest Bar P2 exact friendly primary ownership five components and three cast rules", Catalog);
        run("Quest Bar P2 raw cast locks true finish cancellation unknown baseline and late packet", Casts);
        run("Quest Bar P2 shield raw pose inverted cone default clock and origin hint predicate", Shield);
        run("Quest Bar P2 exactly fifth shared real effect clears new shield does not reset counter", Hits);
        run("Quest Bar P2 native packet zero targets legacy expansion and current actor sampling", Adapter);
        run("Quest Bar P2 zero sequence distinct packets preserve fifth hit duplicate and shield relock", ZeroSequence);
        run("Quest Bar P2 first raw corrects packet geometry and owner wipe releases on recommence", FirstRawAndWipe);
        run("Quest Bar P2 active map replaces rectangular boundary without preactivation replay", Map);
        run("Quest Bar P2 independent completion CFC targetable death destruction and new generation", Lifecycle);
        run("Quest Bar P2 actual output safe goal solver owner clear and director no invented transition", Output);
        run("Quest Bar P1 exact seven rules ordinary geometry raidwide NPC and fixed primary intercept", P1Catalog);
        run("Quest Bar P1 shared clock first cached target raw tether colour and direct slot clear", P1Intercept);
        run("Quest Bar P1 actual narrow goal solver raw replacement and destruction references", P1Output);
        run("Quest Bar P1 lazy primary list survives dictionary slot reuse and promotes live successor", P1TargetOrder);
        run("Quest Bar P1 full marker set and same frame P2 handoff requires separate P3 mycotender pull", P1Handoff);
        run("Quest Bar source module order current priority reverse handoff and reused actor slots", StageOrder);
        run("Quest Bar background stage retains activation tethers shield map and completion independently", BackgroundStages);
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(1246) { CurrentCfcId = 1016 };
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x4660, Center, InCombat: true, Hp: 1000, FriendlyNpc: true, IsEnemy: false),
            new(10, 0x4669, Center + Vector3.UnitX * 10),
            new(20, 0x4667, Center + Vector3.UnitZ * 10),
            new(100, 0, Center - Vector3.UnitZ * 10, PartySlot: 0, Player: true),
        ];
        internal DateTime Now = Start;
        internal bool Enabled = true;
        internal Fixture(bool p1 = false)
        {
            if (p1)
            {
                Set(1, a => a with { Oid = 0x465D, Position = new(0, 0, -336.3f) });
                Set(10, a => a with { Oid = 0x4664, Position = new(13, 0, -302) });
                Set(20, a => a with { Oid = 0x4661, Position = new(-10, 0, -336.3f) });
                Set(100, a => a with { Position = new(5, 0, -330) });
            }
            Sync();
        }
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == 100).Position, Now, _ => Enabled, 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Cast(ulong source, uint action, float rotation = .12345f)
        {
            Set(source, a => a with { Cast = new(action, a.Position + Vector3.UnitX * .12345f,
                rotation, Now, Now.AddSeconds(10.3), source) });
            Sync();
        }
        internal DungeonAmbientAoe[] Shields() => E.AmbientAoes().Where(a => a.Id == "bar-p2/shield").ToArray();
    }

    private static void Catalog()
    {
        var f = new Fixture();
        Check(JobQuestCatalog.Owns(1246, 0x4660) && JobQuestCatalog.Owns(1246, 0x465D)
            && JobQuestCatalog.Owns(1246, 0x466A) && f.E.Active?.BossOid == 0x4660,
            "P2 friendly primary or P1/P3 ownership changed.");
        Check(f.E.Active!.Casts.Select(c => c.Action).SequenceEqual(new uint[] { 40904, 40905, 40906 }),
            "Enumerated DirtySlash incorrectly became rectangular AOE.");
        f.Cast(10, 40904);
        Check(f.E.Casts[10].Rule.Shape is FanTemplate { Radius: 9 }, "Savage Swipe radius.");
        f.Cast(20, 40905);
        Check(f.E.Casts[20].Rule.Shape is CircleTemplate { Radius: 10 }, "Swing radius.");
        f.Cast(10, 40906);
        var fan = (FanTemplate)f.E.Casts[10].Rule.Shape!;
        Check(fan.Radius == 10 && MathF.Abs(fan.AngleRadians - MathF.PI * 2 / 3) < .00001f
            && !f.E.PredictedDamage.Any(), "Swipe shape or invented damage.");
        Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("job-quest/enemy/")) == 2
            && f.E.AmbientAoes().All(a => a.Id != "job-quest/enemy/1"), "Friendly primary drawn as enemy.");
    }

    private static void Casts()
    {
        var f = new Fixture();
        f.Cast(1, 40904);
        var old = f.Actors[0].Cast!;
        Check(f.E.Casts[1].Location == old.Location && f.E.Casts[1].Rotation == old.Rotation
            && f.E.Casts[1].UntilObservedEnd, "P2 cast quantized or timeout bound.");
        f.Now = Start.AddSeconds(40);
        f.E.Effect(1, 40904, 1, f.Now);
        f.Sync();
        Check(f.E.Casts.ContainsKey(1), "Effect/clock ended a still-observed cast.");
        f.E.Finish(1, endedAt: f.Now);
        f.Sync();
        Check(!f.E.Casts.ContainsKey(1), "Cancelled unchanged raw resurrected.");
        f.Set(1, a => a with { Cast = old with { Action = 60000 } });
        f.Sync();
        f.E.StartCast(1, old, f.Now);
        Check(!f.E.Casts.ContainsKey(1), "Unknown raw erased old cancellation.");
        f.Set(1, a => a with { Cast = old });
        f.Sync();
        Check(f.E.Casts.ContainsKey(1), "Unknown to known actual raw transition blocked.");
        f.E.Finish(1, endedAt: f.Now);
        f.Set(1, a => a with { Cast = old with { Rotation = .73123f } });
        f.Sync();
        Check(f.E.Casts[1].Rotation == .73123f, "True raw replacement failed new lock.");
        f.Set(1, a => a with { Cast = null });
        f.Sync();
        f.E.StartCast(1, old, f.Now);
        Check(!f.E.Casts.ContainsKey(1), "Real Finish allowed late old packet.");
    }

    private static void Shield()
    {
        var f = new Fixture();
        var pose = Center + new Vector3(.12345f, 0, .54321f);
        f.Set(1, a => a with { Position = pose, Rotation = .32123f });
        f.Sync();
        f.E.Effect(1, 40866, 1, Start);
        var shield = f.Shields().Single();
        Check(shield.Position == pose && shield.Rotation == .32123f + MathF.PI && shield.Activation == default(DateTime)
            && shield.Safe && shield.Display && shield.Navigate && shield.Shape is FanTemplate { Radius: 5 },
            "Ice Aegis failed unquantized frozen inverse-facing/default persistent safe cone.");
        var forward = new Vector3(MathF.Sin(shield.Rotation), 0, MathF.Cos(shield.Rotation));
        Check(!f.E.BarP2ShieldUnsafe(pose + forward * 4) && f.E.BarP2ShieldUnsafe(pose)
            && f.E.BarP2ShieldUnsafe(pose - forward) && f.E.BarP2ShieldUnsafe(pose + forward * 5.01f),
            "Source cone Check risk predicate, including zero direction, changed.");
        f.Set(1, a => a with { Position = Center + Vector3.UnitX * 5, Rotation = 2 });
        f.Now = Start.AddMinutes(1);
        f.Sync();
        Check(f.Shields().Single() == shield && f.E.Hints(pose + forward * 4).Contains("Go behind shield!"),
            "Shield followed actor, timed out or lost source neutral text.");
        f.E.StartCast(1, new(40866, pose, 0, f.Now, f.Now.AddSeconds(3), 1), f.Now);
        f.E.Finish(1);
        Check(f.Shields().Single() == shield, "Start/cancel altered effect-only shield.");
    }

    private static void Hits()
    {
        var f = new Fixture();
        for (uint i = 1; i <= 4; ++i) f.E.Effect(20, i % 2 == 0 ? 40907u : 40908u, i, Start);
        Check(f.E.BarP2Hits == 4 && f.Shields().Length == 0, "Hits without a shield did not accumulate.");
        f.E.Effect(1, 40866, 5, Start);
        Check(f.E.BarP2Hits == 4 && f.Shields().Length == 1, "Ice Aegis reset shared hit counter.");
        f.E.Effect(20, 40908, 6, Start);
        f.E.Effect(20, 40908, 6, Start, 100);
        Check(f.E.BarP2Hits == 5 && f.Shields().Length == 0, "Fifth effect or packet dedup wrong.");
        f.E.Effect(1, 40866, 7, Start);
        f.E.Effect(20, 40907, 8, Start);
        Check(f.E.BarP2Hits == 6 && f.Shields().Length == 1, "Sixth effect incorrectly cleared replacement shield.");
        f.E.Effect(999, 40908, 9, Start);
        Check(f.E.BarP2Hits == 6, "Unresolvable native caster counted an event.");
    }

    private static void Adapter()
    {
        var f = new Fixture();
        var scans = 0;
        var adapter = new DungeonEventAdapter(f.E, () => { ++scans; f.Sync(); }, _ => 0, e => throw new Exception(e));
        f.Set(1, a => a with { Position = Center + Vector3.UnitX * .76543f, Rotation = .45678f });
        adapter.Handle(new NativeActionEffectEvent { SourceId = 1, ActionId = 40866, GlobalSequence = 1,
            Targets = [], Timestamp = Start }, Start);
        Check(scans == 1 && f.Shields().Single().Position.X == .76543f,
            "Zero-target native packet did not scan current caster before shield lock.");
        adapter.Handle(new LogSystemActionEffectEvent { SourceId = 20, ActionId = 40908, GlobalSequence = 2,
            TargetId = 100, TargetIndex = 0, Timestamp = Start }, Start);
        Check(f.E.BarP2Hits == 0, "Per-target legacy expansion was counted.");
        var packet = new NativeActionEffectEvent { SourceId = 20, ActionId = 40908, GlobalSequence = 2,
            Targets = [100, 101], Timestamp = Start };
        adapter.Handle(packet, Start);
        adapter.Handle(packet, Start);
        Check(f.E.BarP2Hits == 1 && scans == 3, "Duplicate native packet counted more than once.");
    }

    private static void ZeroSequence()
    {
        var f = new Fixture();
        f.E.Effect(1, 40866, 0, Start);
        for (var i = 1; i <= 5; ++i)
        {
            var at = Start.AddSeconds(i);
            f.E.Effect(20, i == 1 ? 40907u : 40908u, 0, at, occurredAt: at);
            f.E.Effect(20, i == 1 ? 40907u : 40908u, 0, at.AddMilliseconds(1), occurredAt: at);
        }
        Check(f.E.BarP2Hits == 5 && f.Shields().Length == 0, "Distinct zero-sequence packets collapsed or duplicates counted.");
        f.Set(1, a => a with { Position = Center + Vector3.UnitX * .76543f });
        f.Now = Start.AddSeconds(6);
        f.Sync();
        f.E.Effect(1, 40866, 0, f.Now, occurredAt: f.Now);
        Check(f.Shields().Single().Position.X == .76543f, "New zero-sequence shield failed relock.");
    }

    private static void FirstRawAndWipe()
    {
        foreach (var mode in new[] { "position", "rotation", "target", "duration" })
        {
            var fixture = new Fixture();
            var packet = new DungeonCastObservation(40904, Center, 0, Start, Start.AddSeconds(10.3), 1);
            fixture.E.StartCast(10, packet, Start);
            var raw = mode switch
            {
                "position" => packet with { Location = Center + Vector3.UnitX * .12345f },
                "rotation" => packet with { Rotation = .71234f },
                "target" => packet with { TargetId = 100 },
                _ => packet with { FinishAt = Start.AddSeconds(10.7) },
            };
            fixture.Set(10, a => a with { Cast = raw });
            fixture.Sync();
            var current = fixture.E.Casts[10];
            Check(current.Location == raw.Location && current.Rotation == raw.Rotation
                && current.TargetId == raw.TargetId && current.FinishAt == raw.FinishAt, $"First raw {mode} not committed.");
            fixture.Now = Start.AddMilliseconds(10);
            fixture.Sync();
            Check(ReferenceEquals(fixture.E.Casts[10], current), $"Stable raw {mode} restarted a locked cast.");
        }
        var f = new Fixture();
        f.E.StartCast(10, new(40904, Center, 0, Start, Start.AddSeconds(10.3), 1), Start);
        f.Cast(10, 40904, .71234f);
        Check(f.E.Casts[10].Location == f.Actors.Single(a => a.Id == 10).Cast!.Location
            && f.E.Casts[10].Rotation == .71234f && f.E.Casts[10].Observed, "First raw failed packet pose correction.");
        f.E.Effect(1, 40866, 1, Start);
        f.E.DirectorUpdate(0x40000005);
        f.Sync();
        Check(f.E.Active == null && f.Shields().Length == 0, "Manager wipe failed unload and repull barrier.");
        f.E.DirectorUpdate(0x40000011);
        f.Sync();
        Check(f.E.Active == null, "Recommence recreated an unloaded module without new actor birth.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        f.Actors.Add(new(1, 0x4660, Center, InCombat: true));
        f.Sync();
        Check(f.E.Active != null && f.E.BarP2Hits == 0 && !f.E.BarP2SecondArena, "Manager recommence failed new session.");
    }

    private static void Map()
    {
        var f = new Fixture();
        Check(f.E.Arena!.Contains(new(34.59f, 0, 5.01f)) && !f.E.Arena.Contains(new(0, 0, 52.01f), 0),
            "Initial rectangle parameters changed.");
        f.E.MapEffect(3, 0x00020001, Start);
        Check(f.E.BarP2SecondArena && f.E.Arena.Position == new Vector3(0, 0, 57)
            && f.E.Arena.Contains(new(0, 0, 80.99f)) && !f.E.Arena.Contains(new(0, 0, 32.99f), 0),
            "Map failed replacement or retained union of old arena.");
        f.E.MapEffect(3, 0, Start);
        Check(f.E.BarP2SecondArena, "Unknown map restored initial arena.");
        f.E.ResetFight();
        f.Set(1, a => a with { InCombat = false });
        f.Sync();
        f.E.MapEffect(3, 0x00020001, Start);
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(!f.E.BarP2SecondArena && f.E.Arena!.Position == Center, "Preactivation map replayed on activation.");
    }

    private static void Lifecycle()
    {
        var f = new Fixture();
        f.Set(1, a => a with { Dead = true, Hp = 0, InCombat = false });
        f.Sync();
        Check(f.E.Active != null && f.E.PreserveStageAcrossBattleEnd, "Death/HP0/out of combat ended source phase.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        Check(f.E.Active != null, "Primary destruction replaced source custom completion.");
        f.Actors.Add(new(1, 0x4660, Center, Targetable: false, InCombat: true));
        f.Sync();
        Check(f.E.Active == null, "Same-ID rebinding failed targetable completion.");
        f.Set(1, a => a with { Targetable = true });
        f.Sync();
        Check(f.E.Active == null, "Completed same actor immediately repulled.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        f.Actors.Add(new(1, 0x4660, Center, InCombat: true, Dead: true, Hp: 0));
        f.Sync();
        Check(f.E.Active != null, "Real new primary incorrectly required live HP.");
        f.E.CurrentCfcId = 0;
        f.Sync();
        Check(f.E.Active == null, "Independent CFC completion ignored.");
    }

    private static void Output()
    {
        var f = new Fixture();
        f.E.Effect(1, 40866, 1, Start);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        output.Apply(f.E, Start, false);
        var snapshot = safe.Manager.BuildSnapshot(Start);
        Check(snapshot.GoalAreas.Count == 1 && snapshot.DangerAreas.Count == 0, "Existing bridge safe goal semantics changed.");
        var origin = f.Actors.Single(a => a.Id == 100).Position + Vector3.UnitX * 10;
        var solution = new SafeZoneSolver().Solve(snapshot, new(origin, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .2f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, Start);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && path.Count > 0 && !f.E.BarP2ShieldUnsafe(path[^1].Position),
            "Actual solver did not enter safe shield cone.");
        f.E.MapEffect(3, 0x00020001, Start);
        f.E.DirectorUpdate(0x40000011);
        Check(f.E.BarP2SecondArena && f.Shields().Length == 1 && f.E.BarP2Hits == 0,
            "Unconsumed Director11 invented a component reset.");
        f.Enabled = false;
        f.Sync();
        output.Apply(f.E, Start, false);
        Check(safe.Manager.BuildSnapshot(Start).GoalAreas.Count == 0 && f.E.Active == null,
            "Disabled module retained safe goal.");
        output.Clear();
    }

    private static void P1Catalog()
    {
        var f = new Fixture(true);
        Check(f.E.IsBarPassageP1 && !f.E.IsBarPassageP2 && f.E.Active!.Casts.Length == 7
            && f.E.Arena!.Position == new Vector3(0, 0, -336.3f), "P1 definition or P2 isolation failed.");
        f.Cast(20, 40896);
        Check(f.E.Casts[20].Rule.Shape is RayRectangleTemplate { Length: 15, MinLocalX: -2, MaxLocalX: 2 },
            "Nopal Fan exact ray parameters changed.");
        foreach (var (action, radius) in new[] { (40898u, 5f), (40901u, 6f), (41787u, 6f), (40900u, 4f) })
        {
            f.Cast(20, action);
            Check(f.E.Casts[20].Rule.Shape is CircleTemplate circle && circle.Radius == radius, $"P1 action{action} radius.");
        }
        f.Cast(20, 41960);
        Check(f.E.OutputCasts.All(p => p.Value.Rule.Shape == null)
            && f.E.PredictedDamage.Select(p => p.TargetId).Order().SequenceEqual(new ulong[] { 1, 100 })
            && f.E.Actors[1].PartySlot == 24, "Raidwide did not include friendly NPC from first raw frame or drew a circle.");
        f.E.Effect(1, 40866, 1, Start);
        f.E.MapEffect(3, 0x00020001, Start);
        Check(f.Shields().Length == 0 && !f.E.BarP2SecondArena && f.E.Arena!.Position.Z == -336.3f,
            "P2 shield or map leaked into P1.");
    }

    private static void P1Intercept()
    {
        var f = new Fixture(true);
        f.Set(10, a => a with { Tethers = new Dictionary<uint, ulong> { [84] = 100 } });
        f.Sync();
        var goal = f.E.AmbientAoes().Single(a => a.Id == "alpha/intercept");
        Check(goal.Safe && !goal.Display && goal.Navigate && goal.GoalPriority == 1 && goal.Activation == default(DateTime),
            "P1 interception goal/default time or scope.");
        var primary = f.E.Actors[1];
        var source = f.E.Actors[10];
        var direction = Vector3.Normalize(source.Position - primary.Position);
        var expectedFrom = primary.Position + (primary.HitboxRadius + .1f) * direction;
        Check(goal.Position == expectedFrom && f.E.AlphaLines().Single().Safe
            && !f.E.Hints(f.E.Actors[100].Position).Contains("Grab the tether!"),
            "P1 used current tether target instead of fixed first465D, or slot mask missing.");
        f.Set(10, a => a with { Tethers = new Dictionary<uint, ulong> { [17] = 1 } });
        f.Sync();
        Check(!f.E.AlphaLines().Single().Safe && f.E.Hints(f.E.Actors[100].Position).Contains("Grab the tether!"),
            "Excluded friendly primary incorrectly coloured safe or old player bit retained.");
        f.Cast(10, 41125);
        Check(Math.Abs((f.E.AlphaActivation - f.Now.AddSeconds(11.4)).TotalSeconds) < .00001,
            "P1 Toxic Spit omitted Finish+1.1.");
        f.Set(10, a => a with { Cast = null });
        f.Sync();
        f.E.Effect(10, 40902, 1, Start);
        Check(f.E.AlphaTetherCount == 1 && Math.Abs((f.E.AlphaActivation - Start.AddSeconds(11.4)).TotalSeconds) < .00001,
            "P1 Finish/effect cleared shared clock/tether.");
        f.Actors.Add(new(101, 0, f.E.Actors[100].Position + Vector3.UnitX * 2, PartySlot: 1, Player: true));
        f.Set(20, a => a with { Tethers = new Dictionary<uint, ulong> { [84] = 100 } });
        f.Set(10, a => a with { Tethers = new Dictionary<uint, ulong> { [17] = 100 } });
        f.Sync();
        f.Set(20, a => a with { Tethers = null });
        f.Sync();
        Check(f.E.AlphaTetherCount == 1 && f.E.Hints(f.E.Actors[100].Position).Contains("Grab the tether!"),
            "P1 direct Clear(slot) became reference counting.");
        f.Set(1, a => a with { Dead = true, InCombat = false, Hp = 0 });
        f.Sync();
        Check(f.E.Active != null, "P1 default death ended custom stage.");
    }

    private static void P1Output()
    {
        var f = new Fixture(true);
        f.Set(10, a => a with { Tethers = new Dictionary<uint, ulong> { [84] = 100 } });
        f.Sync();
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        output.Apply(f.E, Start, false);
        var snapshot = safe.Manager.BuildSnapshot(Start);
        var origin = f.E.Actors[100].Position;
        var result = new SafeZoneSolver().Solve(snapshot, new(origin, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .2f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, Start);
        var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
        var goal = f.E.AmbientAoes().Single(a => a.Id == "alpha/intercept");
        Check(snapshot.GoalAreas.Count == 1 && snapshot.DangerAreas.Count == 0 && result.Status == SolutionStatus.Success
            && path.Count > 0 && goal.Shape.Resolve(goal.Position, goal.Rotation, null).Contains(path[^1].Position),
            "P1 production solver failed narrow intercept.");
        var oldTarget = f.E.AlphaLines().Single().End;
        // A full absence is required to advance the source world generation.
        f.Actors.RemoveAll(a => a.Id == 100);
        f.E.Synchronize(f.Actors, origin, f.Now, _ => true, 100);
        f.Actors.Add(new(100, 0, oldTarget + Vector3.UnitX * 4, PartySlot: 0, Player: true));
        f.Sync();
        Check(f.E.AlphaLines().Single().End == oldTarget && !f.E.AlphaLines().Single().Safe,
            "P1 destroyed tether target rebound to same-ID new actor.");
        output.Clear();
    }

    private static void P1TargetOrder()
    {
        var f = new Fixture(true);
        f.Actors.Insert(0, new(999, 0, new(0, 0, -320)));
        f.Set(10, a => a with { Position = new(0, 0, -320), Tethers = new Dictionary<uint, ulong> { [84] = 100 } });
        f.Sync();
        Check(!f.E.AmbientAoes().Any(a => a.Id == "alpha/intercept") && f.E.AlphaLines().Count() == 1,
            "Missing middle source invented corridor.");
        f.Actors.RemoveAll(a => a.Id == 999);
        f.Actors.Add(new(2, 0x465D, new(20, 0, -320), InCombat: true, FriendlyNpc: true, IsEnemy: false));
        f.Set(10, a => a with { Position = new(13.8f, 0, -301.2f) });
        f.Sync();
        var zone = f.E.AmbientAoes().Single(a => a.Id == "alpha/intercept");
        var target = f.E.Actors[1];
        var expected = target.Position + .6f * Vector3.Normalize(f.E.Actors[10].Position - target.Position);
        Check(zone.Position == expected, "New actor reused dictionary slot before first cached465D.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        zone = f.E.AmbientAoes().Single(a => a.Id == "alpha/intercept");
        target = f.E.Actors[2];
        expected = target.Position + .6f * Vector3.Normalize(f.E.Actors[10].Position - target.Position);
        Check(f.E.Active != null && zone.Position == expected, "Destroyed first465D target failed live list promotion.");
    }

    private static void P1Handoff()
    {
        var f = new Fixture(true);
        var oids = new uint[] { 0x4664, 0x4666, 0x46B0, 0x4662, 0x46AC, 0x473A, 0x4661, 0x4665, 0x46AF, 0x4663, 0x46AD };
        f.Actors.RemoveAll(a => a.Id is 10 or 20);
        for (var i = 0; i < oids.Length; ++i)
            f.Actors.Add(new((ulong)(10 + i), oids[i], new(0, 0, -330)));
        f.Actors.Add(new(50, 0x46BC, new(0, 0, -330)));
        f.Actors.Add(new(51, 0x233C, new(0, 0, -330)));
        f.Sync();
        Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("job-quest/enemy/")) == 11,
            "P1 eleven exact marker types/helper exclusion failed.");
        f.Set(1, a => a with { Targetable = false });
        f.Actors.Add(new(2, 0x4660, Center, InCombat: true, FriendlyNpc: true, IsEnemy: false));
        f.Sync();
        Check(f.E.IsBarPassageP2 && f.E.PrimaryId == 2 && f.E.AlphaTetherCount == 0 && f.E.Arena!.Position == Center,
            "P1 completion failed same-frame handoff to P2.");
        f.Set(2, a => a with { Targetable = false });
        f.Actors.Add(new(3, 0x466A, new(0, 0, 379.27f), InCombat: true));
        f.Sync();
        Check(f.E.Active == null && JobQuestCatalog.Owns(1246, 0x466A), "P3 pulled without first mycotender.");
    }
    private static void StageOrder()
    {
        var player = new DungeonActor(100, 0, new(0, 0, -330), Player: true, PartySlot: 0);
        var p2 = new DungeonActor(20, 0x4660, Center, InCombat: true);
        var p1 = new DungeonActor(10, 0x465D, new(0, 0, -336.3f), InCombat: true);
        DungeonEncounter New() => new(1246) { CurrentCfcId = 1016 };
        void Sync(DungeonEncounter e, params DungeonActor[] actors) => e.Synchronize(actors, player.Position, Start, _ => true, 100);
        var direct = New();
        Sync(direct, player);
        Sync(direct, p2, p1, player);
        Check(direct.IsBarPassageP2 && direct.PrimaryId == 20, "Nearest P1 replaced first directly loaded active P2.");
        Sync(direct, p1, player, p2);
        Check(direct.IsBarPassageP2, "Raw enumeration reordered equal-priority current module.");
        Sync(direct, p2 with { Targetable = false }, p1, player);
        Check(direct.IsBarPassageP1 && direct.PrimaryId == 10, "P2 completion left an empty frame before ready P1.");

        var cold = New();
        Sync(cold, p2, p1, player with { Position = Center });
        Check(cold.IsBarPassageP1, "Cold first frame failed reverse pending promotion before party creation.");
        var blocked = New();
        Sync(blocked, player);
        Sync(blocked, p1 with { InCombat = false }, player);
        Sync(blocked, p1 with { InCombat = false }, p1 with { Id = 11 }, player);
        Check(blocked.Active == null, "Same OID pending primary bypassed inactive loaded sibling.");
        Sync(blocked, p1 with { Id = 11 }, player);
        Check(blocked.Active == null, "Pending sibling promoted before destroyed inactive module unloaded.");
        Sync(blocked, p1 with { Id = 11 }, player);
        Check(blocked.PrimaryId == 11 && blocked.IsBarPassageP1, "Pending sibling failed promotion after occupancy release.");

        var distant = New();
        player = player with { Position = Vector3.Zero };
        Sync(distant, player);
        var farP2 = p2 with { Position = new(800, 0, 0) };
        var farP1 = p1 with { Position = new(1000, 0, 0) };
        Sync(distant, farP2, farP1, player);
        Check(distant.Active == null, "Out-of-range primary bypassed pending load gate.");
        player = player with { Position = new(800, 0, 0) };
        Sync(distant, farP2, farP1, player);
        Check(distant.IsBarPassageP1, "Late pending promotion used distance instead of reverse order.");
        player = player with { Position = new(2000, 0, 0) };
        Sync(distant, farP2, farP1, player);
        Check(distant.IsBarPassageP1, "Active source module unloaded outside load distance.");
    }
    private static void BackgroundStages()
    {
        var f = new Fixture();
        f.Actors.Add(new(2, 0x465D, new(0, 0, -336.3f), InCombat: true, FriendlyNpc: true, IsEnemy: false));
        f.Actors.Add(new(30, 0x4664, new(13, 0, -302), Tethers: new Dictionary<uint, ulong> { [84] = 100 }));
        f.Sync();
        f.Cast(30, 41125);
        f.Cast(10, 40904);
        Check(f.E.IsBarPassageP2 && !f.E.AlphaLines().Any() && f.E.AlphaTetherCount == 1
            && f.E.OutputCasts.Count() == 1, "Background P1 was not updated independently or leaked display.");
        var activation = f.E.AlphaActivation;
        f.Set(2, a => a with { InCombat = false });
        f.Actors.Add(new(3, 0x465D, new(0, 0, -330), FriendlyNpc: true, IsEnemy: false));
        f.Actors.RemoveAll(a => a.Id == 2);
        f.Set(1, a => a with { Targetable = false });
        f.Sync();
        Check(f.E.IsBarPassageP1 && f.E.AlphaTetherCount == 1 && f.E.AlphaActivation == activation
            && f.E.AlphaLines().Count() == 1 && f.E.AmbientAoes().Any(a => a.Id == "alpha/intercept")
            && f.E.OutputCasts.All(c => c.Value.Rule.Action == 41125), "Background P1 lost history or out-of-combat handoff.");

        f.Sync();
        Check(f.E.IsBarPassageP1 && f.E.PrimaryId == 2 && f.E.AlphaTetherCount == 1,
            "Missing selected primary lost persistent module snapshot.");

        var g = new Fixture(true);
        g.Actors.Add(new(2, 0x4660, Center, InCombat: true, FriendlyNpc: true, IsEnemy: false));
        g.Sync();
        g.E.Effect(2, 40866, 1, Start);
        g.E.Effect(20, 40907, 2, Start);
        g.E.MapEffect(3, 0x00020001, Start);
        Check(g.E.IsBarPassageP1 && g.Shields().Length == 0 && g.E.BarP2Hits == 1
            && g.E.Arena!.Position.Z == -336.3f, "Background P2 did not retain shield/map or leaked output.");
        g.Set(2, a => a with { InCombat = false });
        g.Set(1, a => a with { Targetable = false });
        g.Sync();
        Check(g.E.IsBarPassageP2 && g.Shields().Length == 1 && g.E.BarP2Hits == 1
            && g.E.BarP2SecondArena && g.E.Arena!.Position.Z == 57, "Handoff reset background shield/count/map.");

        var pending = new Fixture(true);
        pending.Actors.Add(new(2, 0x465D, new(0, 0, -330), InCombat: true));
        pending.Sync();
        pending.E.DirectorUpdate(0x40000005);
        pending.Sync();
        pending.E.DirectorUpdate(0x40000011);
        pending.Sync();
        Check(pending.E.PrimaryId == 2 && pending.E.IsBarPassageP1,
            "Wipe removed pending sibling or recommence recreated unloaded old primary.");
    }

}
