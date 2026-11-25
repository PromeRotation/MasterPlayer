using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using MasterPlayer.Scripting;

internal static class MainScenarioSleepOtisRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        run("MSQ Sleep Otis exact catalog arenas active components and unclaimed visuals", Catalog);
        run("MSQ Sleep Otis distinct pull completion and same actor revival", Pull);
        run("MSQ Sleep Otis casts survive effects deadlines and dead helpers", CastLifetime);
        run("MSQ Sleep Otis separate raidwide and single target prediction clocks", Damage);
        run("MSQ Sleep two tether types dynamic raw geometry and own directional navigation", BeamGeometry);
        run("MSQ Sleep tether raw typed receipts repeated pairs independent first clocks", BeamReceipts);
        run("MSQ Sleep tether death resurrection missing target and object generations", BeamLifetime);
        run("MSQ Sleep tether strict Player Buddy classification and reverse direction", BeamClassification);
        run("MSQ Sleep teardown bypasses old raw receipts typed absence and pending member prediction", BeamTeardown);
        run("MSQ Sleep simultaneous missing actors preserve historical source target destruction order", BeamSimultaneousRemoval);
        run("MSQ Sleep Otis creation generation bypasses old completion without helper reset", CreatedGeneration);
        run("MSQ Otis grouped circles arbitrary action finish and no packet resurrection", Circles);
        run("MSQ Otis rush first four previews first two navigation and promotion colors", Rush);
        run("MSQ Otis raw source charge length versus quantized locked origin", RushGeometry);
        run("MSQ Otis six cones stable insertion promotion and actual raw requeue", Cones);
        run("MSQ Otis grouped 99 caster exact source sort thresholds", SortThreshold);
        run("MSQ Sleep Otis NPC stacks own partner avoid other marks and spreads", PartyMarks);
        run("MSQ Sleep Otis primary only versus six OID markers and priority zero", Markers);
        run("MSQ Sleep Otis real native output dual lines and solver beam escape stack join", NativeOutputSolver);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E;
        internal readonly List<DungeonActor> Actors = [];
        internal DateTime Now = Start;
        internal Fixture(bool otis = false)
        {
            E = new(otis ? 1213u : 914u);
            var center = new Vector3(otis ? 349 : 100, 0, otis ? -14 : 100);
            Actors.Add(new(1, otis ? 0x4342u : 0x2D3Cu, center, IsEnemy: true, ObjectIndex: 2, Hp: 1000));
            Actors.Add(new(100, 0, center + Vector3.UnitZ * 8, PartySlot: 0, Player: true, InCombat: otis));
            Actors.Add(new(101, 0, center + new Vector3(12, 0, 8), PartySlot: 1, Player: true));
            Sync();
        }
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == 100).Position, Now, playerId: 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = change(Actors[i]);
        }
        internal DungeonPendingCast Cast(ulong id, uint action, Vector3? location = null, double finish = 10, ulong target = 100)
        {
            if (Actors.All(a => a.Id != id))
            {
                Actors.Add(new(id, 0x233C, Actors[0].Position));
                Sync();
            }
            E.StartCast(id, new(action, location ?? Actors[0].Position, .7f, Now, Start.AddSeconds(finish), target), Now);
            return E.Casts[id];
        }
        internal DungeonAmbientAoe[] Aoes(string prefix) => E.AmbientAoes().Where(a => a.Id.StartsWith(prefix)).ToArray();
        internal void Tether(uint type, bool present = true, ulong source = 10, ulong target = 100)
            => E.Tether(target, type, present, Now, source);
        internal void BeamSource()
        {
            Actors.Add(new(10, 0x2D42, Actors[0].Position + new Vector3(.123f, 0, .231f)));
            Sync();
        }
    }

    private static void Catalog()
    {
        var sleep = new Fixture();
        var otis = new Fixture(true);
        Check(sleep.E.Active!.Casts.Length == 6 && otis.E.Active!.Casts.Length == 10, "Activated action set mismatch.");
        Check(!sleep.E.Definitions.SelectMany(d => d.Casts).Any(c => c.Action == 19616)
            && !otis.E.Definitions.SelectMany(d => d.Casts).Any(c => c.Action == 38206), "Unactivated visual/self destruct claimed.");
        Check(sleep.E.Arena is RectangleGeometry && sleep.E.Arena.Contains(new(119.5f, 0, 119.5f)), "Sleep square lost corner.");
        var polygon = (WorldPolygonGeometry)otis.E.Arena!;
        Check(polygon.WorldPoints.Count == 20 && polygon.Position == new Vector3(349, 0, -14), "Otis real polygon missing.");
        for (var i = 0; i < 20; ++i)
        {
            var (sin, cos) = Math.SinCos(i * (Math.PI * 2 / 20));
            Check(polygon.WorldPoints[i] == new Vector3(349 + 19.5f * (float)sin, 0, -14 + 19.5f * (float)cos), "Source float vertex changed.");
        }
    }

    private static void Pull()
    {
        var sleep = new Fixture();
        Check(sleep.E.Active != null, "Targetable precombat Sleep did not pull.");
        var otis = new Fixture(true);
        otis.E.ResetFight();
        otis.Set(1, a => a with { Targetable = false });
        otis.Sync();
        Check(otis.E.Active != null, "Player combat with untargetable idle primary did not pull.");
        otis.Set(100, a => a with { InCombat = false });
        otis.Sync();
        Check(otis.E.Active != null, "Combat loss prematurely reset Otis.");
        foreach (var f in new[] { sleep, otis })
        {
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active == null, "Primary HP0 failed end.");
            f.Set(1, a => a with { Hp = 1000, Targetable = true });
            f.Set(100, a => a with { InCombat = true });
            f.Sync();
            Check(f.E.Active == null, "Completed actor restarted on healing.");
            f.E.RemoveActor(1);
            f.Sync();
            Check(f.E.Active != null, "New object with reused ID could not pull.");
        }
    }

    private static void CastLifetime()
    {
        foreach (var (otis, action) in new[] { (false, 19618u), (true, 38207u) })
        {
            var f = new Fixture(otis);
            f.Cast(10, action);
            f.E.Effect(10, action, 1, Start.AddSeconds(3));
            f.E.Tick(Start.AddSeconds(50));
            Check(f.E.OutputCasts.Count(c => c.Value.Rule.Shape != null) == 1, "Effect or timeout consumed ongoing cast.");
            f.Set(10, a => a with { Dead = true, Cast = new(action, a.Position, .7f, Start, Start.AddSeconds(10)) });
            f.Now = Start.AddSeconds(51);
            f.Sync();
            Check(f.E.OutputCasts.Any(), "Dead helper was mistaken for cast end.");
            f.Set(10, a => a with { Cast = null });
            f.Sync();
            Check(!f.E.OutputCasts.Any(), "Actual cancel retained ordinary AOE.");
        }
    }

    private static void Damage()
    {
        foreach (var (otis, actions) in new[] { (false, new uint[] { 19617 }), (true, new uint[] { 38202, 38203 }) })
        {
            var f = new Fixture(otis);
            foreach (var (action, i) in actions.Select((a, i) => (a, i))) f.Cast((ulong)(10 + i), action, finish: 10 + i);
            Check(f.E.PredictedDamage.Count() == actions.Length * 2, "Independent raidwide mask missing.");
            f.E.Finish(10, actions[0], Start.AddSeconds(1));
            Check(f.E.PredictedDamage.Count() == (actions.Length - 1) * 2, "Raidwide finish removed wrong component.");
            Check(!f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null), "Raidwide invented danger geometry.");
        }
        var single = new Fixture(true);
        single.Cast(10, 38201, target: 101);
        Check(single.E.PredictedDamage.Single().TargetId == 101, "SingleTargetCast lost cast target.");
    }

    private static void BeamGeometry()
    {
        var f = new Fixture();
        f.BeamSource();
        f.Tether(1);
        var own = f.Aoes("sleep/beam/").Single(a => a.Id.EndsWith("/display"));
        Check(own.OutlineOnly && !own.Navigate && own.Position == f.Actors.Single(a => a.Id == 10).Position
            && own.Shape is RayRectangleTemplate { MinLocalX: -2, MaxLocalX: 2, Length: 40 }, "Own beam raw rectangle contract.");
        Check(f.E.SleepBeamLines().Count() == 1
            && f.Aoes("sleep/beam/").Single(a => a.Id.Contains("/away/")).Shape is FanTemplate { Radius: 100 }, "Tether/away cone missing.");
        f.Set(100, a => a with { Position = a.Position + Vector3.UnitX * 3 });
        f.Sync();
        Check(f.Aoes("sleep/beam/").Single(a => a.Id.EndsWith("/display")).Rotation != own.Rotation, "Beam did not follow target.");
        f.Tether(84, target: 101);
        Check(f.Aoes("sleep/beam/").Count(a => a.Id.EndsWith("/display") && a.Navigate) == 1, "Other beam not navigable.");
    }

    private static void BeamReceipts()
    {
        var f = new Fixture();
        f.BeamSource();
        f.Set(10, a => a with { Tethers = new Dictionary<uint, ulong> { [1] = 100 } });
        f.Sync();
        f.Tether(1);
        Check(f.Aoes("sleep/beam/").Count(a => a.Id.EndsWith("/display")) == 1, "Raw then typed duplicated tether.");
        f.Tether(1);
        Check(f.Aoes("sleep/beam/").Count(a => a.Id.EndsWith("/display")) == 1, "OpTether identical replacement appended another bait.");
        f.Tether(1, false);
        Check(!f.Aoes("sleep/beam/").Any(), "Untether failed pair cleanup.");
        f.Now = Start.AddSeconds(20);
        f.Actors.Add(new(11, 0x2D42, f.Actors[0].Position));
        f.Sync();
        f.Tether(1);
        f.Tether(84, source: 11);
        var times = f.Aoes("sleep/beam/").Where(a => a.Id.EndsWith("/display")).Select(a => a.Activation).ToArray();
        Check(times.SequenceEqual(new DateTime?[] { Start, f.Now }), "Tether types shared/reset first clock.");
        f.E.Effect(10, 19623, 1, f.Now);
        f.E.Effect(10, 19624, 2, f.Now);
        Check(f.Aoes("sleep/beam/").Count(a => a.Id.EndsWith("/display")) == 2, "Nonwatched effects cleared bait.");
        var swapped = new Fixture();
        swapped.BeamSource();
        swapped.Set(10, a => a with { Tethers = new Dictionary<uint, ulong> { [1] = 100 } });
        swapped.Sync();
        swapped.Tether(1);
        swapped.Set(10, a => a with { Tethers = new Dictionary<uint, ulong> { [1] = 101 } });
        swapped.Sync();
        swapped.Tether(1, false, target: 100);
        swapped.Tether(1, target: 101);
        Check(swapped.Aoes("sleep/beam/").Count(a => a.Id.EndsWith("/display")) == 1
            && swapped.E.SleepBeamLines().Count() == 1, "Raw replacement lost one echo receipt and duplicated new target.");
        swapped.E.RemoveActor(10);
        Check(!swapped.E.SleepBeamLines().Any(), "Raw replacement left a duplicate beam after source destruction.");
    }

    private static void BeamLifetime()
    {
        var f = new Fixture();
        f.BeamSource();
        f.Tether(1);
        f.Set(100, a => a with { Dead = true });
        f.Sync();
        Check(f.Aoes("sleep/beam/").Any(), "Dead target removed beam.");
        f.Set(10, a => a with { Dead = true });
        f.Sync();
        Check(!f.Aoes("sleep/beam/").Any(), "Dead source beam visible.");
        f.Set(10, a => a with { Dead = false });
        f.Sync();
        Check(f.Aoes("sleep/beam/").Any(), "Same source revival lost old bait.");
        f.E.RemoveActor(100);
        f.Tether(1, false);
        Check(f.Aoes("sleep/beam/").Any(), "Missing target incorrectly resolved Untether.");
        var old = f.Aoes("sleep/beam/").Single(a => a.Id.EndsWith("/display"));
        f.Set(100, a => a with { Position = a.Position + Vector3.UnitX * 20, Dead = false });
        f.Sync();
        Check(f.Aoes("sleep/beam/").Single(a => a.Id.EndsWith("/display")).Rotation == old.Rotation, "New target rebound old reference.");
        f.E.RemoveActor(10);
        Check(f.Aoes("sleep/beam/").Any(), "Previously unresolved Untether was incorrectly replayed on destruction.");
        f.E.ResetFight();
        Check(!f.Aoes("sleep/beam/").Any(), "Owner reset retained frozen bait.");
        var outgoing = new Fixture();
        outgoing.BeamSource();
        outgoing.Tether(1);
        outgoing.E.RemoveActor(10);
        Check(!outgoing.Aoes("sleep/beam/").Any(), "Resolvable outgoing Untether failed source destruction cleanup.");
    }

    private static void BeamClassification()
    {
        var f = new Fixture();
        f.BeamSource();
        f.Actors.Add(new(102, 0x2D3D, f.Actors[1].Position, FriendlyNpc: true));
        f.Sync();
        f.Tether(1, target: 102);
        Check(!f.Aoes("sleep/beam/").Any(), "Friendly NPC was implicitly Buddy.");
        f.Set(102, a => a with { Buddy = true });
        f.Sync();
        f.Tether(84, source: 102, target: 10);
        Check(f.Aoes("sleep/beam/").Any(), "Reverse Buddy tether rejected.");
    }

    private static void Circles()
    {
        var f = new Fixture(true);
        var first = f.Cast(10, 38197, finish: 20);
        f.Cast(11, 38205, finish: 5);
        Check(f.Aoes("otis/circle/").Select(a => a.Activation).SequenceEqual(new DateTime?[] { Start.AddSeconds(20), Start.AddSeconds(5) }),
            "Circle group sorted below expected99.");
        f.E.Finish(10, 38197, Start.AddSeconds(1));
        f.Now = Start.AddSeconds(2);
        f.Cast(11, 49999);
        Check(!f.Aoes("otis/circle/").Any(), "Other action start/old finish failed group source removal.");
        f.E.Finish(11, 49999, Start.AddSeconds(3));
        Check(!f.E.Definitions.SelectMany(d => d.Casts).Any(c => c.Action == 49999), "Observed arbitrary action expanded generic ownership.");
        f.E.StartCast(10, new(38197, first.Location + Vector3.UnitX, .7f, Start, first.FinishAt), f.Now);
        Check(!f.Aoes("otis/circle/").Any(), "Consumed old packet resurrected group.");
    }

    private static void Rush()
    {
        var f = new Fixture(true);
        foreach (var (end, i) in new double[] { 20, 10, 30, 5, 2 }.Select((t, i) => (t, i)))
            f.Cast((ulong)(10 + i), 38209, f.Actors[0].Position + Vector3.UnitZ * (i + 1), end);
        var aoes = f.Aoes("otis/rush/");
        Check(aoes.Length == 4 && aoes.Count(a => a.Navigate) == 2 && aoes.Take(2).All(a => a.Color == new Vector4(1, 1, 0, 1))
            && aoes.Select(a => a.Activation).SequenceEqual(new DateTime?[] { Start.AddSeconds(20), Start.AddSeconds(10), Start.AddSeconds(30), Start.AddSeconds(5) }),
            "Rush preview risk/order contract.");
        f.E.Finish(10, 38209, Start.AddSeconds(1));
        Check(f.Aoes("otis/rush/").Last().Activation == Start.AddSeconds(2), "Hidden rush not promoted.");
        f.E.Finish(11, 38209, Start.AddSeconds(1));
        f.E.Finish(12, 38209, Start.AddSeconds(1));
        Check(f.Aoes("otis/rush/").All(a => a.Navigate && a.Color == null), "Two remaining rushes kept danger override.");
        f.E.Tick(Start.AddSeconds(100));
        Check(f.Aoes("otis/rush/").Length == 2, "Rush expired without real finish.");
    }

    private static void RushGeometry()
    {
        var f = new Fixture(true);
        var origin = f.Actors[0].Position + new Vector3(.12345f, 0, .23456f);
        f.Actors.Add(new(10, 0x4347, origin));
        f.Sync();
        var target = origin + new Vector3(3, 0, 4);
        f.Cast(10, 38209, target);
        var aoe = f.Aoes("otis/rush/").Single();
        Check(aoe.Shape is RayRectangleTemplate { MinLocalX: -2.5f, MaxLocalX: 2.5f, Length: 5 }
            && MathF.Abs(aoe.Rotation - MathF.Atan2(3, 4)) < .00001f && aoe.Position != origin, "Raw vector/quantized origin mismatch.");
        f.Set(10, a => a with { Position = a.Position + Vector3.UnitX * 10 });
        f.Sync();
        Check(f.Aoes("otis/rush/").Single() == aoe, "Locked rush followed actor.");
        f.E.StartCast(10, new(38209, target + Vector3.UnitZ, .7f, Start, Start.AddSeconds(10)), Start.AddSeconds(1));
        Check(f.Aoes("otis/rush/").Single().Position == aoe.Position, "Same cast packet correction relocked moved source.");
        var originA = new Vector3(349, 0, -14);
        var originB = originA + new Vector3(1f / 512, 0, 0);
        var a = new DungeonCastObservation(38209, originA + new Vector3(3, 0, 4), .7f, Start, Start.AddSeconds(10), SourcePosition: originA);
        f.E.StartCast(10, a, Start.AddSeconds(1.1));
        f.E.StartCast(10, a with { SourcePosition = originB, Location = originB + new Vector3(3, 0, 4) }, Start.AddSeconds(1.2));
        Check(f.E.Casts[10].SourcePosition == originB, "Invisible raw-origin correction was discarded.");
        f.E.StartCast(10, a with { SourcePosition = null, Location = originB + new Vector3(4, 0, 4) }, Start.AddSeconds(1.3));
        Check(MathF.Abs(f.Aoes("otis/rush/").Single().Rotation - MathF.PI / 4) < .000001f, "Next correction used stale raw origin.");
    }

    private static void BeamTeardown()
    {
        var f = new Fixture();
        f.BeamSource();
        void Raw(bool present)
        {
            f.Set(10, a => a with { Tethers = present ? new Dictionary<uint, ulong> { [1] = 100 } : null });
            f.Sync();
        }
        Raw(true);
        Raw(false);
        Raw(true);
        f.E.RemoveActor(10);
        Check(!f.Aoes("sleep/beam/").Any(), "Old raw cancel receipt swallowed destruction cleanup.");
        var missing = new Fixture();
        missing.BeamSource();
        missing.Tether(1);
        missing.Actors.RemoveAll(a => a.Id == 10);
        missing.Sync();
        Check(!missing.Aoes("sleep/beam/").Any(), "Packet-only source disappearance lacked Untether.");
        var prediction = new Fixture();
        prediction.BeamSource();
        prediction.E.CurrentPartyMembers = [new(0, "", 100)];
        prediction.Tether(1);
        prediction.E.RemoveActor(100);
        Check(prediction.E.PredictedDamage.Any(p => p.TargetId == 100 && p.DamageType == "Tankbuster"),
            "Incoming target removal erased still-present party member prediction.");
    }

    private static void BeamSimultaneousRemoval()
    {
        foreach (var sourceFirst in new[] { true, false })
        foreach (var reverse in new[] { true, false })
        foreach (var type in new uint[] { 1, 84 })
        {
            var f = new Fixture();
            var source = new DungeonActor(10, 0x2D42, f.Actors[0].Position);
            var target = new DungeonActor(102, 0, f.Actors[1].Position, Buddy: true);
            var rawSource = reverse ? target : source;
            var rawTarget = reverse ? source : target;
            f.Actors.AddRange(sourceFirst ? [rawSource, rawTarget] : [rawTarget, rawSource]);
            f.Sync();
            f.Tether(type, source: rawSource.Id, target: rawTarget.Id);
            // Later frame order cannot replace the historical tracking order used for destruction.
            f.Actors.Reverse();
            f.Sync();
            f.Actors.RemoveAll(a => a.Id is 10 or 102);
            f.Sync();
            Check(f.E.SleepBeamLines().Count() == (sourceFirst ? 0 : 1),
                "Simultaneous missing actors changed the source feed's ordered Untether target resolution.");
        }
        var staggered = new Fixture();
        staggered.Now = Start.AddMilliseconds(20);
        staggered.BeamSource();
        staggered.Now = Start.AddMilliseconds(40);
        staggered.Actors.Insert(0, new(102, 0, staggered.Actors.Single(a => a.Id == 100).Position, Buddy: true));
        staggered.Sync();
        staggered.Now = Start.AddMilliseconds(100);
        staggered.Tether(1, source: 10, target: 102);
        staggered.Sync();
        staggered.Actors.RemoveAll(a => a.Id is 10 or 102);
        staggered.Sync();
        Check(!staggered.E.SleepBeamLines().Any(), "Staggered frame observations lost historical creation order to later object indices.");
    }

    private static void CreatedGeneration()
    {
        foreach (var otis in new[] { false, true })
        {
            var f = new Fixture(otis);
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            f.E.ObjectCreated(50, f.Now, 0x233C);
            f.Set(1, a => a with { Hp = 1000 });
            f.Sync();
            Check(f.E.Active == null, "Helper creation unlocked completed primary.");
            f.E.ObjectCreated(1, f.Now, otis ? 0x4342u : 0x2D3Cu);
            f.Sync();
            Check(f.E.Active != null, "New primary creation could not unlock old completion.");
        }
    }

    private static void Cones()
    {
        var f = new Fixture(true);
        for (var i = 0; i < 7; ++i) f.Cast((ulong)(10 + i), 38200, finish: 20 - i);
        Check(f.E.OutputCasts.Select(c => c.Value.Source).SequenceEqual(new ulong[] { 10, 11, 12, 13, 14, 15 }), "Six cone stable order.");
        f.E.Finish(10, 38200, Start.AddSeconds(1));
        Check(f.E.OutputCasts.Last().Value.Source == 16 && f.E.OutputCasts.Last().Value.FinishAt == Start.AddSeconds(14), "Cone hidden promotion lost original clock.");
        foreach (var cast in f.E.Casts.Values.ToArray())
            f.Set(cast.Source, a => a with { Cast = new(cast.Rule.Action, cast.Location, cast.Rotation, cast.StartedAt, cast.FinishAt) });
        f.Now = Start.AddSeconds(2);
        f.Sync();
        f.Set(11, a => a with { Cast = a.Cast! with { Rotation = 1 } });
        f.Now = Start.AddSeconds(2.1);
        f.Sync();
        Check(f.E.OutputCasts.Select(c => c.Value.Source).SequenceEqual(new ulong[] { 12, 13, 14, 15, 16, 11 }),
            "Real raw cone correction failed Finish/Start requeue.");
    }

    private static void SortThreshold()
    {
        foreach (var action in new uint[] { 38197, 38209 })
        {
            var f = new Fixture(true);
            for (var i = 0; i < 99; ++i)
            {
                f.Actors.Add(new((ulong)(1000 + i), 0x233C, f.Actors[0].Position));
            }
            f.Sync();
            for (var i = 0; i < 99; ++i) f.Cast((ulong)(1000 + i), action, f.Actors[0].Position + Vector3.UnitZ, 200 - i);
            Check(f.Aoes(action == 38197 ? "otis/circle/" : "otis/rush/").First().Activation == Start.AddSeconds(102), "99 threshold source sort omitted.");
        }
    }

    private static void PartyMarks()
    {
        foreach (var otis in new[] { false, true })
        {
            var f = new Fixture(otis);
            f.Actors.Add(new(102, 0x2D3D, f.Actors[1].Position + Vector3.UnitX * 2, FriendlyNpc: true));
            f.Sync();
            f.Cast(10, otis ? 38199u : 19615u, target: 100);
            f.Cast(11, otis ? 38199u : 19615u, target: 101);
            var aoes = f.Aoes("quest/stack/");
            Check(aoes.Any(a => a.Id.Contains("/buddy")) && aoes.Any(a => a.Id.Contains("/avoid") && a.Shape is CircleTemplate { Radius: 12 })
                && aoes.All(a => !a.Id.EndsWith("/join")), "Own stack joined other marked player or missed partner/avoid.");
            f.E.Finish(10, otis ? 38199u : 19615u, Start.AddSeconds(1));
            Check(f.Aoes("quest/stack/").Any(a => a.Id.EndsWith("/join")), "Unmarked viewer failed union join.");
            f.Cast(12, otis ? 38198u : 19613u, target: 101);
            Check(f.Aoes("quest/spread/").Any(a => a.Id.EndsWith("/display") && a.Shape is CircleTemplate c && c.Radius == (otis ? 6 : 8)),
                "Spread radius/NPC roster mismatch.");
        }
    }

    private static void Markers()
    {
        var sleep = new Fixture();
        sleep.Set(1, a => a with { AggroPlayer = false, InCombat = false });
        sleep.Sync();
        Check(sleep.E.PriorityTarget(0, sleep.Actors[1].Position) == 1, "Boss priority0 setter lost.");
        var otis = new Fixture(true);
        foreach (var (oid, i) in new uint[] { 0x4344, 0x4345, 0x4346, 0x4343, 0x4347, 0x4349 }.Select((o, i) => (o, i)))
            otis.Actors.Add(new((ulong)(10 + i), oid, otis.Actors[0].Position));
        otis.Sync();
        Check(otis.Aoes("job-quest/enemy/").Length == 6, "Otis markers omitted active add or included other Boss.");
        Check(!MainScenarioQuestCatalog.Owns(1213, 0x4349), "Other same-territory Boss incorrectly owned.");
    }

    private static void NativeOutputSolver()
    {
        foreach (var otis in new[] { false, true })
        {
            var f = new Fixture(otis);
            Func<Vector3, bool> accept;
            if (otis)
            {
                f.Cast(10, 38199, target: 101, finish: 4);
                var target = f.Actors.Single(a => a.Id == 101).Position;
                accept = p => Vector2.Distance(new(p.X, p.Z), new(target.X, target.Z)) <= 6;
            }
            else
            {
                f.BeamSource();
                f.Set(100, a => a with { Position = f.Actors[0].Position + new Vector3(6, 0, 4) });
                f.Sync();
                f.Tether(1, target: 101);
                var a = f.Aoes("sleep/beam/").Single();
                var geometry = a.Shape.Resolve(a.Position, a.Rotation, null);
                accept = p => !geometry.Contains(p);
            }
            using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
            var draw = new ScriptDrawApi();
            var output = new DungeonOutput(safe, draw, safe.Manager);
            output.Apply(f.E, f.Now, false);
            Check(otis || draw.Lines.Count == 1 && draw.Lines.Values.Single().Safe == false, "Native beam line lost danger style.");
            var snapshot = safe.Manager.BuildSnapshot(f.Now);
            var player = f.Actors.Single(a => a.Id == 100).Position;
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback
                && accept(path.Count > 0 ? path[^1].Position : player), "Real native solver did not resolve beam/stack geometry.");
            output.Clear();
            Check(draw.Lines.Count == 0, "Native output clear leaked line.");
        }
    }
}
