using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static partial class QuestBarPassageRegression
{
    private static readonly Vector3 P3Center = new(0, 0, 379.27f);

    private static void RunP3(Action<string, Action> run)
    {
        run("Quest Bar P3 eleven source components exact rules arena and marker ownership", P3Catalog);
        run("Quest Bar P3 lazy first mycotender pull dead targetable absent and default completion", P3Lifecycle);
        run("Quest Bar P3 shrink ring F plus delays map replacement restore and cancellation", P3Arena);
        run("Quest Bar P3 power global FIFO strict prefix sticky risk and yellow colour", P3PowerQueue);
        run("Quest Bar P3 burst nine finish explosions axial tolerance original precision and cached clocks", P3BurstSequence);
        run("Quest Bar P3 burst destroy continues helper cancel and effect separation", P3BurstDestroy);
        run("Quest Bar P3 radial13 sequential model strict immunity no wall stop and independent first goal", P3Knockback);
        run("Quest Bar P3 independent stack spread NPC references death replacement and target finish", P3Party);
        run("Quest Bar P3 raidwide current raw clock enrage first and no rectangle or fake damage", P3Hints);
        run("Quest Bar P3 actual safe goal solver world radar output and owner release", P3Output);
        run("Quest Bar P3 background state handoff map retained CFC independent and director wipe", P3Background);
        run("Quest Bar P3 first raw and packet origin correction then locks ordinary movement", P3Origins);
        run("Quest Bar P3 stacked targets may join each other and buddy sampling preserves consumer limits", P3BuddySampling);
        run("Quest Bar P3 no phantom knockback hint and every actual segment checked", P3KnockbackHints);
    }

    private sealed class P3Fixture
    {
        internal readonly DungeonEncounter E = new(1246) { CurrentCfcId = 1016 };
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x466A, P3Center, InCombat: false, Hp: 1000),
            new(10, 0x4670, P3Center + Vector3.UnitX * 8, InCombat: true),
            new(20, 0x4671, P3Center + Vector3.UnitZ * 8),
            new(30, 0x233C, P3Center - Vector3.UnitX * 8),
            new(100, 0, P3Center + Vector3.UnitX * 2, PartySlot: 0, Player: true),
            new(101, 0, P3Center - Vector3.UnitX * 2, PartySlot: 24, FriendlyNpc: true, IsEnemy: false),
        ];
        internal DateTime Now = Start;
        internal bool Enabled = true;
        internal P3Fixture() => Sync();
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == 100).Position, Now, _ => Enabled, 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Cast(ulong id, uint action, double duration = 10, Vector3? location = null, float rotation = .12345f, ulong target = 100)
        {
            Set(id, a => a with { Cast = new(action, location ?? a.Position, rotation, Now, Now.AddSeconds(duration), target) });
            Sync();
        }
        internal DungeonAmbientAoe[] Aoes(string prefix) => E.AmbientAoes().Where(a => a.Id.StartsWith(prefix)).ToArray();
        internal void End(ulong id)
        {
            Set(id, a => a with { Cast = null });
            Sync();
        }
    }

    private static void P3Catalog()
    {
        var f = new P3Fixture();
        Check(JobQuestCatalog.Owns(1246, 0x466A) && f.E.Active?.BossOid == 0x466A, "P3 not native.");
        Check(f.E.Active!.Casts.Select(c => c.Action).SequenceEqual(new uint[]
            { 40914, 40922, 40916, 40917, 40918, 40909, 40910, 40912, 40920, 40930, 40927, 40932 }), "P3 activated rule set.");
        f.Cast(20, 40914);
        Check(f.E.Casts[20].Rule.Shape is CircleTemplate { Radius: 12 }, "Proximity incorrectly uses comment45.");
        f.Cast(30, 40920);
        Check(f.E.Casts[30].Rule.Shape is DonutTemplate { InnerRadius: 5, OuterRadius: 40 }, "Lake donut.");
        f.Actors.Add(new(40, 0x4672, P3Center));
        f.Actors.Add(new(41, 0x46DA, P3Center));
        f.Sync();
        var markers = f.Aoes("job-quest/enemy/").Select(a => a.Id).ToHashSet();
        Check(markers.SetEquals(new[] { "job-quest/enemy/1", "job-quest/enemy/10", "job-quest/enemy/40" }), "P3 marker scope.");
        f.Cast(30, 40925);
        Check(!f.E.Casts.ContainsKey(30), "Unactivated pull became owned.");
    }

    private static void P3Lifecycle()
    {
        var f = new P3Fixture();
        f.E.ResetFight();
        f.Set(10, a => a with { InCombat = false });
        f.Sync();
        f.Actors.Add(new(11, 0x4670, P3Center, InCombat: true));
        f.Sync();
        Check(f.E.Active == null, "Second mycotender incorrectly pulled.");
        f.Set(10, a => a with { InCombat = true, Dead = true, Targetable = false });
        f.Set(1, a => a with { InCombat = false, Targetable = false });
        f.Sync();
        Check(f.E.Active?.BossOid == 0x466A, "First dead/untargetable mycotender pull or primary selection filtered.");
        f.E.CurrentCfcId = 0;
        f.Set(10, a => a with { InCombat = false });
        f.Sync();
        Check(f.E.Active != null, "P3 incorrectly used P1/P2 CFC or combat completion.");
        f.Set(1, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active == null, "P3 HP0 did not complete.");
        f.Set(1, a => a with { Hp = 1000 });
        f.Sync();
        Check(f.E.Active == null, "Completed same actor resurrected without birth.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        f.Actors.Add(new(1, 0x466A, P3Center, Hp: 1000, Targetable: false));
        f.Set(10, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.Active != null, "New primary generation not loaded.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        Check(f.E.Active == null, "Destroyed primary retained P1/P2 snapshot lifecycle.");
    }

    private static void P3Arena()
    {
        var f = new P3Fixture();
        f.Cast(20, 40914, 7);
        var ring = f.Aoes("bar-p3/arena").Single();
        Check(ring.Activation == Start.AddSeconds(7.5) && ring.Shape is DonutTemplate { InnerRadius: 20, OuterRadius: 25 }, "Flames arena clock.");
        f.End(20);
        Check(f.Aoes("bar-p3/arena").Single() == ring, "Cancellation cleared arena ring.");
        f.Now = Start.AddSeconds(1);
        f.Cast(30, 40920, 8.5);
        Check(f.Aoes("bar-p3/arena").Single().Activation == Start.AddSeconds(11.5), "Lake failed overwrite F+2.");
        f.E.MapEffect(6, 0x00020001);
        Check(f.E.BarP3SmallArena && !f.Aoes("bar-p3/arena").Any()
            && MathF.Abs(f.E.Arena!.Position.Z - 379.241f) < .0001f, "Small arena map.");
        f.E.MapEffect(6, 0x00008004);
        f.Now = Start.AddSeconds(2);
        f.Cast(20, 40914, 7);
        var restored = f.Aoes("bar-p3/arena").Single();
        f.E.MapEffect(6, 0x00008004);
        Check(!f.E.BarP3SmallArena && f.Aoes("bar-p3/arena").Single() == restored, "Restore should not clear ring.");
    }

    private static void P3PowerQueue()
    {
        var f = new P3Fixture();
        f.Actors.Add(new(40, 0x233C, P3Center));
        f.Cast(20, 40910, 10);
        f.Cast(30, 40910, 1);
        f.Cast(40, 40910, 10.5);
        f.Actors.Add(new(50, 0x233C, P3Center));
        f.Cast(50, 40910, 20);
        var output = f.Aoes("bar-p3/power/");
        Check(output.Length == 4 && output.Take(3).All(a => a.Navigate && a.Color == new Vector4(1, 1, 0, 1))
            && !output[3].Navigate, "Unsorted power strict prefix or yellow danger.");
        f.End(30); // Any same action finish removes the global head, not this caster's item.
        var sticky = f.Aoes("bar-p3/power/");
        Check(f.E.BarP3Power.Select(p => p.Cast.Source).SequenceEqual(new ulong[] { 30, 40, 50 })
            && sticky[1].Navigate && sticky[1].Color == new Vector4(1, 1, 0, 1), "FIFO or sticky risk reset.");
        f.E.Effect(50, 40910, 1, f.Now);
        Check(f.E.BarP3Power.Count == 3, "Effect advanced Finish-only component.");
    }

    private static void P3BurstSequence()
    {
        var f = new P3Fixture();
        var origin = P3Center + new Vector3(.12345f, 0, .54321f);
        f.Set(20, a => a with { Position = origin });
        f.Cast(20, 40917, 9, origin, .41321f);
        Check(f.E.BarP3Bursts.Single().Next == origin
            && MathF.Abs(f.E.BarP3Bursts.Single().Advance.Length() - 5) < .00001f, "Burst original precision/rotation.");
        var shown = f.Aoes("bar-p3/burst/");
        Check(shown.Length == 4 && shown[^1].Id.EndsWith("/0") && shown[^1].Color == new Vector4(1, 1, 0, 1), "Burst head order/colour.");
        var frozen = shown.Select(a => a.Activation).ToArray();
        f.Now = Start.AddSeconds(50);
        f.Sync();
        Check(f.Aoes("bar-p3/burst/").Select(a => a.Activation).SequenceEqual(frozen), "Idle future clocks slid.");
        f.End(20);
        var line = f.E.BarP3Bursts.Single();
        Check(line.Remaining == 8 && line.Activation == f.Now.AddSeconds(1.2)
            && Vector3.Distance(line.Next, origin + line.Advance) < .00001f, "Cancelled first finish advance.");
        for (var i = 0; i < 8; ++i)
        {
            line = f.E.BarP3Bursts.Single();
            f.Now = f.Now.AddSeconds(2);
            var hit = line.Next + new Vector3(.9f, 0, -.9f);
            f.Cast(30, 40918, 1, hit);
            f.End(30);
        }
        Check(f.E.BarP3Bursts.Count == 0 && f.Aoes("bar-p3/burst/").Length == 0, "Nine real finishes failed remove line.");
    }

    private static void P3BurstDestroy()
    {
        var f = new P3Fixture();
        f.Cast(20, 40917, 9);
        f.Actors.RemoveAll(a => a.Id == 20);
        f.Now = Start.AddSeconds(2);
        f.Sync();
        var line = f.E.BarP3Bursts.Single();
        Check(line.Remaining == 8 && line.Activation == f.Now.AddSeconds(1.2), "Destroy did not advance with processing clock.");
        f.E.Effect(30, 40918, 1, f.Now);
        Check(f.E.BarP3Bursts.Single() == line, "Effect advanced exaflare.");
        f.Cast(30, 40918, 1, line.Next + new Vector3(1.01f, 0, 0));
        f.End(30);
        Check(f.E.BarP3Bursts.Single() == line, "Finish outside axial tolerance matched.");
        f.Now = f.Now.AddSeconds(1);
        f.Cast(30, 40918, 1, line.Next + new Vector3(1, 0, 1));
        f.End(30);
        Check(f.E.BarP3Bursts.Single().Remaining == 7, "Later helper could not continue destroyed source.");
    }

    private static void P3Knockback()
    {
        var f = new P3Fixture();
        f.Cast(10, 40909, 6, P3Center);
        f.Cast(30, 40909, 8, P3Center - Vector3.UnitX);
        var player = f.Actors.Single(a => a.Id == 100);
        Check(Vector3.Distance(f.E.BarP3Landing(player), player.Position + Vector3.UnitX * 26) < .0001f,
            "Sequential no-wall radial13 model.");
        var goal = f.Aoes("bar-p3/knockback/start").Single();
        Check(goal.Safe && !goal.Display && goal.GoalPriority == 1 && goal.Position == P3Center, "First independent R6 safe goal.");
        var equal = player with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = Start.AddSeconds(6) } };
        Check(f.E.BarP3Landing(equal) == f.E.BarP3Landing(player), "Equal expiry incorrectly immune.");
        var immune = player with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = Start.AddSeconds(7) } };
        Check(Vector3.Distance(f.E.BarP3Landing(immune), player.Position + Vector3.UnitX * 13) < .0001f, "Per-source strict immunity.");
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: Start.AddSeconds(20));
        Check(f.Aoes("bar-p3/knockback/").Length == 1 && f.Aoes("bar-p3/knockback/start").Single() == goal, "Event immunity removed goal or leaked projection.");
        f.End(10);
        Check(f.Aoes("bar-p3/knockback/start").Single().Position == P3Center - Vector3.UnitX, "First-source goal did not promote.");
    }

    private static void P3Party()
    {
        var f = new P3Fixture();
        f.Cast(10, 40930, 5, target: 100);
        f.Cast(30, 40932, 7, target: 101);
        var aoes = f.E.AmbientAoes().ToArray();
        Check(aoes.Single(a => a.Id == "quest/stack/0/display").Shape is CircleTemplate { Radius: 6 }
            && aoes.Any(a => a.Id == "quest/stack/0/buddy")
            && aoes.Single(a => a.Id == "quest/spread/0/avoid").Shape is CircleTemplate { Radius: 7 },
            "Independent components cross-filtered or radii changed.");
        Check(!f.E.PredictedDamage.Any(), "Cast stack/spread invented damage.");
        f.Set(100, a => a with { Dead = true });
        f.Sync();
        Check(!f.Aoes("quest/stack/").Any(), "Dead stack target remained active.");
        f.Set(100, a => a with { Dead = false });
        f.Sync();
        Check(f.Aoes("quest/stack/").Any(), "Revived original stack target lost marker.");
        f.Actors.RemoveAll(a => a.Id == 101);
        f.Sync();
        f.Actors.Add(new(101, 0, P3Center + Vector3.UnitX * 20, PartySlot: 24, FriendlyNpc: true));
        f.Sync();
        Check(f.Aoes("quest/spread/").First().Position != P3Center + Vector3.UnitX * 20, "Old target rebound same-ID birth.");
        f.End(30);
        Check(!f.Aoes("quest/spread/").Any() && f.Aoes("quest/stack/").Any(), "Independent finish removed wrong component.");
    }

    private static void P3Hints()
    {
        var f = new P3Fixture();
        f.Cast(10, 40916, 10);
        Check(f.E.OutputCasts.All(c => c.Value.Rule.Shape == null) && f.E.PredictedDamage.Any()
            && f.E.PredictedDamage.All(p => p.Activation == Start.AddSeconds(10)), "Raidwide rectangle or initial clock.");
        f.Cast(30, 40927, 30);
        Check(f.E.Hints(P3Center).Any(h => h.Contains("Enrage! 30.0")), "First enrage remaining.");
        f.Now = Start.AddSeconds(1);
        f.Set(10, a => a with { Cast = a.Cast! with { FinishAt = Start.AddSeconds(12), TotalTime = 11.7f } });
        f.Sync();
        Check(f.E.PredictedDamage.All(p => p.Activation == Start.AddSeconds(12))
            && f.E.Hints(P3Center).Any(h => h.Contains("Enrage! 29.0")), "Current raw clock not consumed.");
        f.End(30);
        Check(!f.E.Hints(P3Center).Any(h => h.StartsWith("Enrage!")), "Enrage cancellation retained.");
        f.Now = Start.AddSeconds(50);
        f.Sync();
        var remaining = 11.7f + .3f - 11.7f;
        Check(f.E.PredictedDamage.All(p => p.Activation == f.Now.AddSeconds(remaining)),
            "Saturated dynamic NPC clock froze at old raw FinishAt.");
        f.Now = f.Now.AddSeconds(1);
        f.E.Tick(f.Now);
        Check(f.E.PredictedDamage.All(p => p.Activation == f.Now.AddSeconds(remaining)),
            "Unchanged replay world did not advance saturated clock.");
    }

    private static void P3Output()
    {
        var f = new P3Fixture();
        f.Set(100, a => a with { Position = P3Center + Vector3.UnitX * 10 });
        f.Cast(10, 40909, 6, P3Center);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        output.Apply(f.E, f.Now, false);
        var snapshot = safe.Manager.BuildSnapshot(f.Now);
        Check(snapshot.GoalAreas.Count == 1 && snapshot.DangerAreas.Count == 0, "Safe goal publication.");
        var origin = f.Actors.Single(a => a.Id == 100).Position;
        var result = new SafeZoneSolver().Solve(snapshot, new(origin, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .2f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, Start);
        var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
        Check(result.Status == SolutionStatus.Success && path.Count > 0
            && Vector3.Distance(path[^1].Position, P3Center) <= 6.01f, "Production goal solver did not enter first R6.");
        output.Clear();
        Check(safe.Manager.BuildSnapshot(f.Now).GoalAreas.Count == 0, "Owner cleanup.");
    }

    private static void P3Background()
    {
        var f = new P3Fixture();
        f.Actors.Add(new(2, 0x465D, P3Center, InCombat: true, FriendlyNpc: true));
        f.Sync();
        Check(f.E.Active?.BossOid == 0x466A, "Equal priority unexpectedly switched.");
        f.Cast(20, 40917, 9);
        f.E.MapEffect(6, 0x00020001);
        f.Set(1, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active?.BossOid == 0x465D && f.E.BarP3Bursts.Count == 0, "P3 completion failed handoff/teardown.");
        f.E.DirectorUpdate(0x40000005);
        Check(f.E.Active == null && f.E.BarP3Power.Count == 0, "Wipe leaked P3.");
        f.E.DirectorUpdate(0x40000011);
        f.Sync();
        Check(f.E.Active == null, "Director recommence recreated unchanged unloaded modules.");
    }

    private static void P3Origins()
    {
        foreach (uint action in new uint[] { 40922, 40916, 40927 })
        {
            var clock = new P3Fixture();
            var packetClock = new DungeonCastObservation(action, P3Center, 0, Start, Start.AddSeconds(5.3), 100);
            clock.E.StartCast(10, packetClock, Start);
            clock.Now = Start.AddSeconds(1);
            clock.Set(10, a => a with
            {
                Cast = packetClock with { StartedAt = Start.AddSeconds(.1), FinishAt = Start.AddSeconds(5.4), TotalTime = 5 },
            });
            clock.Sync();
            Check(action == 40927
                ? clock.E.Hints(P3Center).Any(h => h.Contains("Enrage! 4.4"))
                : clock.E.PredictedDamage.Any() && clock.E.PredictedDamage.All(p => Math.Abs((p.Activation - Start.AddSeconds(5.4)).TotalSeconds) < .00001),
                "First raw clock retained packet start instead of observed elapsed.");
        }
        var f = new P3Fixture();
        var actor = f.Actors.Single(a => a.Id == 20);
        var packet = new DungeonCastObservation(40917, actor.Position, .41321f, Start, Start.AddSeconds(9), 100,
            SourcePosition: actor.Position);
        f.E.StartCast(20, packet, Start);
        var corrected = actor.Position + Vector3.UnitX * .25f;
        f.Now = Start.AddSeconds(.1);
        f.Set(20, a => a with { Position = corrected, Cast = packet with { SourcePosition = null } });
        f.Sync();
        Check(f.E.BarP3Bursts.Count == 1 && f.E.BarP3Bursts[0].Next == corrected
            && f.E.BarP3Bursts[0].Remaining == 9, "First raw origin did not correct or advanced old line.");
        f.Set(20, a => a with { Position = corrected + Vector3.UnitX * 2 });
        f.Sync();
        Check(f.E.BarP3Bursts[0].Next == corrected, "Ordinary caster movement slid origin.");
        var authoritative = corrected + Vector3.UnitZ * .25f;
        f.E.StartCast(20, packet with { SourcePosition = authoritative }, f.Now);
        Check(f.E.BarP3Bursts[0].Next == authoritative, "Source-only authoritative packet ignored.");
        var (sin, cos) = Math.SinCos(packet.Rotation!.Value);
        Check(f.E.BarP3Bursts[0].Advance == new Vector3((float)sin, 0, (float)cos) * 5, "Source double SinCos direction mismatch.");
    }

    private static void P3BuddySampling()
    {
        var f = new P3Fixture();
        f.Cast(10, 40930, 5, target: 100);
        f.Cast(30, 40930, 5, target: 101);
        var marks = f.E.AmbientAoes().ToArray();
        Check(marks.Any(a => a.Id == "quest/stack/join") && !marks.Any(a => a.Id.StartsWith("quest/stack/") && a.Id.EndsWith("/avoid")),
            "Unbounded stack capacity incorrectly became mutually exclusive.");
        f.End(30);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        output.Apply(f.E, f.Now, false);
        Check(safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0
            && f.E.AmbientAoes().Any(a => a.Shape is DungeonStackBuddyTemplate),
            "Two-player buddy model lost or ignored legacy 65 percent coverage rejection.");
        var count = 0;
        for (var z = -18; z <= 18 && count < 39; z += 6)
            for (var x = -18; x <= 18 && count < 39; x += 6)
            {
                if (x * x + z * z > 22 * 22) continue;
                f.Actors.Add(new((ulong)(200 + count), 0, P3Center + new Vector3(x, 0, z),
                    PartySlot: 25 + count, FriendlyNpc: true, IsEnemy: false));
                ++count;
            }
        f.Sync();
        output.Apply(f.E, f.Now, false);
        Check(safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count > 0, "Rare actual buddy sampling layout lost navigation.");
        output.Clear();
    }

    private static void P3KnockbackHints()
    {
        var f = new P3Fixture();
        f.Set(100, a => a with { Position = P3Center + Vector3.UnitX * 30 });
        f.Sync();
        Check(!f.E.Hints(f.Actors.Single(a => a.Id == 100).Position).Contains("Knockback into danger!"), "No source still warned about knockback.");
        f.Set(100, a => a with { Position = P3Center + Vector3.UnitX * 15 });
        f.Cast(10, 40909, 6, P3Center);
        f.Cast(30, 40909, 8, P3Center + Vector3.UnitX * 40);
        var player = f.Actors.Single(a => a.Id == 100);
        Check(Vector3.Distance(f.E.BarP3Landing(player), player.Position) < .0001f
            && f.E.Hints(player.Position).Contains("Knockback into danger!"), "Unsafe intermediate landing ignored.");
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: Start.AddSeconds(20));
        Check(!f.E.Hints(player.Position).Contains("Knockback into danger!"), "Immune source generated phantom warning.");
    }
}
