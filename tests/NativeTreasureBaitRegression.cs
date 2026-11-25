using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureBaitRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static readonly (uint Oid, uint Action, uint Live, float Radius, float Delay, uint Add)[] Cases =
    [
        (0x2539, 13396, 0x1E8D9C, 6, .9f, 0x256A),
        (0x253A, 13219, 0x1EA140, 6, .7f, 0x256F),
        (0x2534, 13452, 0x1EA8BB, 10, 1.2f, 0x2566),
    ];

    internal static void Run(Action<string, Action> run)
    {
        run("native treasure altar bait source geometry and All lifetime", Geometry);
        run("native treasure altar icon source duplicate baits and per-view navigation", Icons);
        run("native treasure altar dead target and frozen generation differ from ID display", TargetGeneration);
        run("native treasure altar dead source hides base but retains first-target semantics", SourceGeneration);
        run("native treasure altar three real starts clear baits without icon reset or finish count", Starts);
        run("native treasure altar voidzone current cast and native event clocks stay separate", Voidzones);
        run("native treasure altar one live entity consumes one prediction without TTL", Handoff);
        run("native treasure Chimera interrupt intent and ordinary Gaze geometry", Interrupts);
        run("native treasure altar new icons retain destroyed source generation", NewIconsAfterDestroy);
        run("native treasure altar raw third start and packet correction preserve new bait", RawThirdStart);
    }

    private static DungeonActor Enemy(ulong id, uint oid) => new(id, oid, Center, InCombat: true, IsEnemy: true,
        ObjectIndex: (ushort)(id * 2), Hp: 100, MaxHp: 100);
    private static DungeonActor Player(ulong id = 100, Vector3? position = null, bool dead = false) =>
        new(id, 0, position ?? Center, Dead: dead, Hostile: false, Player: true);
    private static DungeonEncounter New(uint oid) => Create(oid, [Enemy(1, oid), Enemy(2, 0x233C), Enemy(3, 0x2545),
        Player(), Player(101, Center + Vector3.UnitX * 2), Player(102, Center + Vector3.UnitX * 30)]);
    private static DungeonEncounter Create(uint oid, DungeonActor[] actors)
    {
        var e = new DungeonEncounter(794) { TreasurePartyInput = [100, 101, 102] };
        e.Synchronize(actors, Center, Start, playerId: 100);
        Check(e.Active?.BossOid == oid, "Wrong activated primary.");
        return e;
    }
    private static DungeonAmbientAoe[] Baits(DungeonEncounter e) => e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/bait/")).ToArray();
    private static DungeonAmbientAoe[] Water(DungeonEncounter e) => e.AmbientAoes().Where(a => a.Id.StartsWith("trial/voidzone/")).ToArray();
    private static void Sync(DungeonEncounter e, double t, ulong view = 100, params DungeonActor[] actors) =>
        e.Synchronize(actors, actors.FirstOrDefault(a => a.Id == view)?.Position ?? Center, Start.AddSeconds(t), playerId: view);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Geometry()
    {
        foreach (var (oid, action, live, radius, _, add) in Cases)
        {
            var e = New(oid);
            var rules = e.Active!.Casts.ToDictionary(c => c.Action);
            Check(rules.Count == (oid == 0x2539 ? 8 : oid == 0x253A ? 9 : 7)
                && rules[action].Shape == null && rules[5352].Shape is CircleTemplate { Radius: 6 }
                && NativeTreasureCatalog.HasMatangaCleave(oid), "Component count or shared cleave differs.");
            Check(NativeTreasureCatalog.Priority(oid, 0x2545) == 2 && NativeTreasureCatalog.Priority(oid, add) == 1
                && NativeTreasureCatalog.Priority(oid, 0x9999) == 0
                && NativeTreasureCatalog.Vulnerable(oid, 0x2545) && !NativeTreasureCatalog.Vulnerable(oid, add),
                "Source rewards or ordinary add semantics changed.");
            if (oid == 0x2539)
                Check(rules[13393].Shape is FanTemplate { Radius: 11.92f } && rules[13394].Shape is CircleTemplate { Radius: 9.92f }
                    && rules[13395].Shape is DonutTemplate { InnerRadius: 8, OuterRadius: 30 }
                    && rules[13392].Shape is FanTemplate { Radius: 11.92f } && rules[13742].Shape is FanTemplate { Radius: 8.07f },
                    "Chimera exact geometry differs.");
            if (oid == 0x253A)
                Check(rules[13218].Shape is FanTemplate { Radius: 14.6f } && rules[13220].Shape is FanTemplate { Radius: 26.6f }
                    && rules[13283].Shape == null && rules[13835].Shape is CircleTemplate { Radius: 4 }
                    && rules[13743].Shape is CircleTemplate { Radius: 10 }
                    && NativeTreasureCatalog.Priority(oid, 0x2544) == 3, "Diresaur source geometry differs.");
            if (oid == 0x2534)
                Check(rules[13451].Shape is FanTemplate { Radius: 13.06f } && rules[13454].Shape is CircleTemplate { Radius: 5 }
                    && rules[13453].Shape is CircleTemplate { Radius: 11.06f }
                    && rules[13739].Shape is RayRectangleTemplate { Length: 45.06f, MinLocalX: -2, MaxLocalX: 2 },
                    "Totem used helper hitbox for rectangle.");
            e.Icon(102, 23, Start, 101);
            Check(Baits(e).Where(a => a.Display).All(a => a.Shape is CircleTemplate circle && circle.Radius == radius),
                "Bait radius differs.");
            Sync(e, 1, 100, Enemy(1, oid) with { Dead = true }, Enemy(4, add), Player());
            Check(e.Active != null, "Ordinary All add failed to keep phase.");
            Sync(e, 2, 100, Enemy(5, live), Enemy(2, 0x233C), Player());
            Check(e.Active == null && !e.AmbientAoes().Any(), "Voidzone/helper extended All lifetime.");
        }
    }

    private static void Icons()
    {
        foreach (var (oid, _, _, radius, _, _) in Cases)
        {
            var e = New(oid);
            e.Icon(102, 23, Start, 100);
            e.Icon(102, 23, Start, 101);
            e.Icon(102, 23, Start, 101);
            Check(Baits(e).Count(a => a.Display) == 3 && Baits(e).Count(a => a.Id.EndsWith("/edge")) == 1
                && Baits(e).Count(a => a.Navigate) == 5
                && e.Hints(Center).Contains(oid == 0x2539 ? "Bait away!" : "Bait away! (3 times)")
                && !e.PredictedDamage.Any(), "Icon source, duplicates, edge or no-damage contract differs.");
            Sync(e, 1, 101, Enemy(1, oid), Enemy(3, 0x2545), Player(), Player(101), Player(102));
            Check(Baits(e).Count(a => a.Display && a.OutlineOnly) == 2 && !Baits(e).Any(a => a.Id.EndsWith("/edge"))
                && Baits(e).Count(a => a.Navigate) == 5 && e.Hints(Center).Contains("Bait away from raid!")
                && e.Hints(Center).Contains("GTFO from baited aoe!"), "Second target lost base hints or acquired first-target edge.");
            Sync(e, 2, 102, Enemy(1, oid), Player(), Player(101), Player(102, Center + Vector3.UnitZ * 30));
            Check(Baits(e).Count(a => a.Display && !a.OutlineOnly) == 3 && Baits(e).Count(a => a.Navigate) == 3,
                "Distant observer lost filled baits.");
            e.Tick(Start.AddSeconds(300));
            Check(Baits(e).Length == 6, "Default activation was turned into icon TTL.");
            e.ResetFight();
            Check(!Baits(e).Any(), "Reset retained baits.");
        }
    }

    private static void TargetGeneration()
    {
        foreach (var (oid, _, _, _, _, _) in Cases)
        {
            var e = New(oid);
            e.Icon(101, 23, Start, 100);
            var old = Center + Vector3.UnitX * 3;
            Sync(e, 1, 100, Enemy(1, oid), Player(100, old, dead: true), Player(101));
            Check(Baits(e).Single(a => a.Display).Position == old && Baits(e).Any(a => a.Id.EndsWith("/edge")),
                "Dead target was hidden or removed.");
            e.RemoveActor(100, Start.AddSeconds(2));
            Sync(e, 3, 100, Enemy(1, oid), Player(100, Center + Vector3.UnitZ * 12), Player(101));
            var bait = Baits(e).Single(a => a.Display);
            Check(bait.Position == old && bait.OutlineOnly && Baits(e).Single(a => a.Navigate).Id.EndsWith("/avoid")
                && !e.Hints(Center).Contains(oid == 0x2539 ? "Bait away!" : "Bait away! (3 times)"),
                "Recreated ID replaced old target reference or got old first-target edge.");
        }
    }

    private static void SourceGeneration()
    {
        foreach (var (oid, _, _, _, _, _) in Cases)
        {
            var e = New(oid);
            e.Icon(101, 23, Start, 100);
            Sync(e, 1, 100, Enemy(1, oid) with { Dead = true }, Enemy(3, 0x2545), Player(), Player(101));
            Check(Baits(e).Length == 1 && Baits(e)[0].Id.EndsWith("/edge")
                && e.Hints(Center).Any(s => s.StartsWith("Bait away!")), "Dead source filtered derived CurrentBaits semantics.");
            Sync(e, 2, 100, Enemy(1, oid), Enemy(3, 0x2545), Player(), Player(101));
            Check(Baits(e).Any(a => a.Display), "Same source revival did not restore base bait.");
            e.RemoveActor(1, Start.AddSeconds(3));
            Sync(e, 4, 100, Enemy(3, 0x2545), Player(), Player(101));
            Check(Baits(e).Any(a => a.Display), "Undead destroyed source removed frozen bait.");
            Sync(e, 5, 100, Enemy(1, oid) with { Dead = true }, Enemy(3, 0x2545), Player(), Player(101));
            Check(Baits(e).Any(a => a.Display), "Recreated source ID mutated old source.");
        }
    }

    private static void Starts()
    {
        foreach (var (oid, action, _, _, _, _) in Cases)
        {
            var e = New(oid);
            var total = oid == 0x2539 ? 1 : 3;
            for (var i = 0; i < total; ++i)
            {
                if (i == total - 1) e.Icon(101, 23, Start.AddSeconds(i * 4), 100);
                var time = Start.AddSeconds(i * 4);
                e.StartCast(2, new(action, Center, 0, time, time.AddSeconds(3.3)), time);
                if (i == 0 && total == 3)
                {
                    e.Icon(101, 23, time, 100);
                    e.StartCast(2, new(action, Center + Vector3.UnitX, 0, time, time.AddSeconds(3.3)), time.AddSeconds(.1));
                    Check(Baits(e).Any(), "Same-generation correction counted another Start.");
                }
                e.Finish(2, action, time.AddSeconds(1));
            }
            Check(Baits(e).Length == 0, "Nth real Start did not clear all baits.");
            e.Icon(101, 23, Start.AddSeconds(13), 100);
            e.TreasureEffect(2, action, 1, 0, Start.AddSeconds(14), Center);
            Check(Baits(e).Any(), "Effect advanced CastStarted counter.");
        }
    }

    private static void Voidzones()
    {
        foreach (var (oid, action, _, radius, delay, _) in Cases)
        {
            var target = Player(101, Center + Vector3.UnitX * 2);
            var cast = new DungeonCastObservation(action, Center - Vector3.UnitX * 3, 0, Start, Start.AddSeconds(3.3), 101);
            var e = New(oid);
            Sync(e, .1, 100, Enemy(1, oid) with { Cast = cast }, Player(), target);
            Check(Water(e).Single().Position == target.Position && Water(e).Single().Activation == cast.FinishAt,
                "Voidzone cast did not read live target or original activation.");
            target = target with { Position = Center + Vector3.UnitZ * 9 };
            Sync(e, 1, 100, Enemy(1, oid) with { Cast = cast }, Player(), target);
            Check(Water(e).Single().Position == target.Position, "Target circle stopped following cast target.");
            e.Finish(1, action, Start.AddSeconds(2));
            Check(Water(e).Length == 0, "Finish synthesized effect prediction.");
            e.TreasureEffect(2, action, 1, 101, Start.AddSeconds(3), Center);
            e.TreasureEffect(2, action, 1, 101, Start.AddSeconds(3), Center);
            e.TreasureEffect(2, action, 2, 999, Start.AddSeconds(4), Center);
            e.Effect(2, action, 3, Start.AddSeconds(4), 101, Center);
            e.Tick(Start.AddSeconds(100));
            Check(Water(e).Length == 2 && Water(e)[0].Position == target.Position && Water(e)[1].Position == Center
                && Water(e)[0].Activation == Start.AddSeconds(3).AddSeconds((double)delay)
                && Water(e).All(a => a.Navigate && a.Display && !a.Obstacle && a.Shape is CircleTemplate c && c.Radius == radius),
                "Native main target/fallback, dedup, float delay or no-TTL semantics changed.");
            Check(e.Hints(target.Position).Contains("GTFO from voidzone!"), "Missing voidzone warning.");
        }
    }

    private static void Handoff()
    {
        foreach (var (oid, action, live, _, _, _) in Cases)
        {
            var e = New(oid);
            for (uint i = 0; i < 3; ++i) e.TreasureEffect(1, action, i, 0, Start, Center);
            var water = Enemy(4, live) with { Dead = true, Targetable = false };
            Sync(e, 1, 100, Enemy(1, oid), Player(), water with { EventState = 7 });
            Check(Water(e).Length == 3, "EventState7 consumed prediction.");
            Sync(e, 2, 100, Enemy(1, oid), Player(), water);
            Check(Water(e).Count(a => a.Id.Contains("/event/")) == 2 && Water(e).Count(a => a.Id.Contains("/live/")) == 1,
                "One dead/untargetable live entity must consume only first prediction.");
            Sync(e, 3, 100, Enemy(1, oid), Player(), water);
            Check(Water(e).Count(a => a.Id.Contains("/event/")) == 1, "Second update did not consume exactly one more.");
            e.RemoveActor(4, Start.AddSeconds(4));
            Check(Water(e).Length == 1, "Destroyed live zone removed unrelated event prediction.");
        }
    }

    private static void Interrupts()
    {
        var e = New(0x2539);
        foreach (var action in new[] { 13394u, 13395u })
        {
            var time = action == 13394 ? Start : Start.AddSeconds(20);
            e.StartCast(2, new(action, Center, 0, time, time.AddSeconds(5.3)), time);
            e.Tick(time.AddSeconds(9));
            Check(e.TreasureInterruptTargets.SequenceEqual([2ul]) && e.Hints(Center).Contains("Interrupt!")
                && e.Casts[2].Rule.Shape != null, "Interrupt hint used predicted expiry or replaced independent AOE.");
            e.Finish(2, action, time.AddSeconds(10));
            Check(!e.TreasureInterruptTargets.Any() && !e.Hints(Center).Contains("Interrupt!"), "Interrupt intent leaked after actual finish.");
        }
        e.StartCast(2, new(13742, Center, 0, Start.AddSeconds(31), Start.AddSeconds(34.3)), Start.AddSeconds(31));
        Check(!e.Gazes.Any() && e.GazeFacing(Center, 0) == null && !e.TreasureInterruptTargets.Any(),
            "Ordinary Languorous Gaze became gaze control or interrupt.");
    }

    private static void NewIconsAfterDestroy()
    {
        foreach (var (oid, _, _, _, _, _) in Cases)
        {
            var e = New(oid);
            e.RemoveActor(1, Start.AddSeconds(1));
            Sync(e, 2, 100, Enemy(1, oid) with { Dead = true }, Enemy(3, 0x2545), Player(), Player(101));
            e.Icon(101, 23, Start.AddSeconds(2), 100);
            Check(Baits(e).Count(a => a.Display) == 1 && Baits(e).Count(a => a.Navigate) == 2,
                "Icon after source destruction failed to use frozen undead primary.");
            e = New(oid);
            e.RemoveActor(1, Start.AddSeconds(1));
            Sync(e, 2, 1, Enemy(1, oid) with { Dead = true }, Enemy(3, 0x2545), Player(), Player(101));
            e.Icon(100, 23, Start.AddSeconds(2), 1);
            Check(Baits(e).Count(a => a.Id.Contains("/party/")) == 2 && Baits(e).Any(a => a.Id.EndsWith("/edge")),
                "New primary ID target incorrectly matched old source reference.");
        }
    }

    private static void RawThirdStart()
    {
        foreach (var (oid, action) in new[] { (0x253Au, 13219u), (0x2534u, 13452u) })
        {
            var e = New(oid);
            foreach (var seconds in new[] { 0, 4 })
            {
                var time = Start.AddSeconds(seconds);
                e.StartCast(2, new(action, Center, 0, time, time.AddSeconds(3.3)), time);
                e.Finish(2, action, time.AddSeconds(1));
            }
            e.Icon(101, 23, Start.AddSeconds(9), 100);
            var raw = new DungeonCastObservation(action, Center, 0, Start.AddSeconds(10), Start.AddSeconds(13.3));
            Sync(e, 10.1, 100, Enemy(1, oid), Enemy(2, 0x233C) with { Cast = raw }, Player(), Player(101));
            Check(!Baits(e).Any(), "Third raw observation did not clear baits.");
            e.Icon(101, 23, Start.AddSeconds(10.2), 100);
            var corrected = raw with { Location = Center + Vector3.UnitX };
            e.StartCast(2, corrected, Start.AddSeconds(10.3));
            Check(Baits(e).Any(), "Same raw generation packet correction cleared new icon.");
            Sync(e, 11, 100, Enemy(1, oid), Enemy(2, 0x233C) with { Cast = corrected }, Player(), Player(101));
            foreach (var seconds in new[] { 20, 30 })
            {
                var time = Start.AddSeconds(seconds);
                e.StartCast(2, new(action, Center, 0, time, time.AddSeconds(3.3)), time);
                Check(Baits(e).Any() == (seconds == 20), "Raw field replacement or packet correction Start count changed.");
                e.Finish(2, action, time.AddSeconds(1));
            }
        }
    }
}
