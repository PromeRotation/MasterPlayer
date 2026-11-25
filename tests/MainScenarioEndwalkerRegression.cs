using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static partial class JobQuestRegression
{
    private static void EndwalkerQuestArenasAndPull()
    {
        var p1 = EndwalkerQuestArenas.Scarmiglione(7);
        Check(p1.Contains(new(3.1f, 7, -128.3f)) && !p1.Contains(new(3.21f, 7, -128.45f))
            && !p1.Contains(new(0, 7, -128.15f)) && p1.GetContours().Count == 1,
            "Scarmiglione exact union, concavity or elevation.");
        var frosty = EndwalkerQuestArenas.Vergilia(0);
        Check(frosty.Contains(new(-.07f, 0, -59.51f))
            && !frosty.Contains(new(-.07f + 19.4f * MathF.Sin(MathF.PI / 20), 0, -79 + 19.4f * MathF.Cos(MathF.PI / 20))),
            "Frosty arena replaced by circle or rotated polygon.");
        var colibri = EndwalkerQuestArenas.Colibri(0);
        Check(colibri.Contains(new(429, 0, -425.7f)) && !colibri.Contains(new(429.12f, 0, -425.7f))
            && colibri.Contains(new(420.9f, 0, -454.3f)) && !colibri.Contains(new(420.75f, 0, -454.3f)),
            "Colibri asymmetric rectangle union mirrored or convexified.");
        var e = new DungeonEncounter(1091);
        DungeonActor boss = new(1, 0x39B3, new(0, 0, -148), Targetable: false, Hp: 1000);
        DungeonActor player = new(100, 0, boss.Position, PartySlot: 0, Player: true);
        e.Synchronize([boss with { InCombat = true }, player], player.Position, Start, playerId: 100);
        Check(e.Active == null, "Scarmiglione used primary combat instead of trash pull.");
        DungeonActor trash = new(2, 0x39B5, boss.Position, InCombat: true, Targetable: false, Dead: true);
        e.Synchronize([boss, player, trash], player.Position, Start.AddSeconds(1), playerId: 100);
        Check(e.Active?.BossOid == 0x39B3, "Dead untargetable combat trash must trigger pull with passive primary.");
        trash = trash with { Dead = false, InCombat = false, Targetable = true };
        e.Synchronize([boss, player, trash], player.Position, Start.AddSeconds(2), playerId: 100);
        Check(e.Active != null && e.AmbientAoes().Count(a => a.Id.StartsWith("job-quest/enemy/")) == 1
            && !e.AmbientAoes().Any(a => a.Id == "job-quest/enemy/1"),
            "Scarmiglione despawned on trash combat end or drew untargetable primary.");
        e.Synchronize([boss with { Hp = 0 }, player, trash with { InCombat = true }], player.Position, Start.AddSeconds(3), playerId: 100);
        Check(e.Active == null, "Scarmiglione primary zero HP did not finish.");
        var (markers, mb, mp) = MainScenario(1010, 0x3646);
        markers.Synchronize([mb, mp,
            new(2, 0x777, mb.Position, IsEnemy: false),
            new(3, 0x778, mb.Position, IsEnemy: true),
            new(4, 0x779, mb.Position, IsEnemy: true, Targetable: false),
            new(5, 0x780, mb.Position, IsEnemy: true, Dead: true)],
            mp.Position, Start, playerId: 100);
        Check(markers.AmbientAoes().Where(a => a.Id.StartsWith("job-quest/enemy/")).Select(a => a.Id)
            .Order().SequenceEqual(new[] { "job-quest/enemy/1", "job-quest/enemy/3" }),
            "Frosty marker must use exact enemy classification and filter dead/untargetable.");
        var (outside, ob, op) = MainScenario(1177, 0x4037);
        ob = ob with { Position = new(425, 0, -400) };
        outside.Synchronize([ob, op], op.Position, Start, playerId: 100);
        Check(outside.AmbientAoes().Single(a => a.Id == "job-quest/enemy/1") is { OutlineOnly: true } marker
            && marker.Position == ob.Position, "Custom arena axis marker was clamped to boundary.");
    }

    private static void EndwalkerQuestFifo()
    {
        foreach (var (territory, oid, action, max) in new (uint, uint, uint, int)[]
        {
            (1091, 0x39B3, 30021, 5), (1177, 0x4037, 34888, 8),
        })
        {
            var (e, boss, player) = MainScenario(territory, oid);
            var actors = new List<DungeonActor> { boss, player, new(3, 0x39B5, boss.Position, InCombat: true) };
            for (var i = 0; i <= max; ++i)
                actors.Add(new((ulong)(10 + i), 0x233C, boss.Position,
                    Cast: new(action, boss.Position + Vector3.UnitX * i, 0, Start, Start.AddSeconds(20 - i))));
            e.Synchronize(actors, player.Position, Start, playerId: 100);
            Check(e.OutputCasts.Select(c => c.Value.Source).SequenceEqual(Enumerable.Range(10, max).Select(i => (ulong)i)),
                $"{action}: insertion cap sorted by deadline.");
            e.Effect(10, action, 1, Start.AddSeconds(1));
            e.Finish(10, action, Start.AddSeconds(2));
            var newHelper = new DungeonActor(30, 0x233C, boss.Position,
                Cast: new(action, boss.Position, 0, Start.AddSeconds(3), Start.AddSeconds(5)));
            e.Synchronize(actors.Append(newHelper), player.Position, Start.AddSeconds(3), playerId: 100);
            Check(e.OutputCasts.Select(c => c.Value.Source).SequenceEqual(Enumerable.Range(11, max).Select(i => (ulong)i)),
                $"{action}: hidden promotion or vacant-slot insertion.");
            e.Tick(Start.AddSeconds(60));
            Check(e.OutputCasts.Count() == max, $"{action}: prediction timeout deleted live component.");
        }
    }

    private static void EndwalkerQuestParty()
    {
        foreach (var (territory, oid, stack, spread, radius) in new (uint, uint, uint, uint, float)[]
        {
            (1091, 0x39B3, 30025, 30023, 6), (1177, 0x4037, 34890, 34893, 8),
        })
        {
            var (p, pb, pp) = MainScenario(territory, oid);
            DungeonActor companion = new(101, 0x888, pb.Position + Vector3.UnitX * 12, FriendlyNpc: true);
            DungeonActor trigger = new(4, 0x39B4, pb.Position, InCombat: true);
            DungeonActor partyHelper = new(2, 0x233C, pb.Position, Cast: new(stack, pb.Position, 0, Start, Start.AddSeconds(5), 101));
            p.Synchronize([pb, pp, trigger, companion, partyHelper], pp.Position, Start, playerId: 100);
            Check(p.AmbientAoes().Any(x => x.Id == "quest/stack/join" && x.Navigate && !x.Display)
                && p.AmbientAoes().Any(x => x.Id.StartsWith("quest/stack/") && x.Shape is CircleTemplate { Radius: 6 })
                && !p.PredictedDamage.Any(), $"{stack}: NPC stack hard join or default no-damage contract.");
            p.Finish(2, stack, Start.AddSeconds(1));
            partyHelper = partyHelper with { Cast = new(spread, pb.Position, 0, Start.AddSeconds(2), Start.AddSeconds(7), 101) };
            p.Synchronize([pb, pp, trigger, companion, partyHelper], pp.Position, Start.AddSeconds(2), playerId: 100);
            Check(p.AmbientAoes().Any(x => x.Id.EndsWith("/avoid") && x.Shape is CircleTemplate c && c.Radius == radius + 1)
                && p.AmbientAoes().Any(x => x.Id.EndsWith("/display") && x.Shape is CircleTemplate c && c.Radius == radius),
                $"{spread}: exact display/nav radii.");
        }
        var (e, boss, player) = MainScenario(1010, 0x3646);
        DungeonActor npc = new(101, 0x888, player.Position + Vector3.UnitX * 10, FriendlyNpc: true);
        DungeonActor a = new(2, 0x233C, boss.Position, Cast: new(27449, boss.Position, 0, Start, Start.AddSeconds(5), 101));
        DungeonActor b = new(3, 0x233C, boss.Position, Cast: new(27457, boss.Position, 0, Start, Start.AddSeconds(5), 100));
        e.Synchronize([boss, player, npc, a, b], player.Position, Start, playerId: 100);
        var aoes = e.AmbientAoes().Where(x => x.Id.StartsWith("quest/spread/")).ToArray();
        Check(aoes.Count(x => x.Id.EndsWith("/display")) == 2
            && aoes.Any(x => x.Id.EndsWith("/separate/101") && x.Shape is CircleTemplate { Radius: 7 }),
            "Independent spread components skipped another component's marked teammate.");
        Check(e.Hints(player.Position).Contains("Spread!") && !e.PredictedDamage.Any(),
            "Safe spread neutral hint missing or invented damage prediction.");
        e.Finish(3, 27457, Start.AddSeconds(1));
        Check(e.AmbientAoes().Where(x => x.Id.EndsWith("/display") && x.Id.StartsWith("quest/spread/"))
            .Single().Shape is CircleTemplate { Radius: 5 }, "Magitek Cannon finish consumed Resin Bomb.");

        // Both components can mark the same actor; finish must be target FIFO within its action.
        e.StartCast(3, b.Cast! with { StartedAt = Start.AddSeconds(2), FinishAt = Start.AddSeconds(7), TargetId = 101 }, Start.AddSeconds(2));
        e.Finish(3, 27457, Start.AddSeconds(3));
        Check(e.AmbientAoes().Where(x => x.Id.EndsWith("/display") && x.Id.StartsWith("quest/spread/"))
            .Single().Shape is CircleTemplate { Radius: 5 }, "Cross-action same-target finish removed wrong spread.");
        npc = npc with { Dead = true };
        e.Synchronize([boss, player, npc, a], player.Position, Start.AddSeconds(3.1), playerId: 100);
        Check(!e.AmbientAoes().Any(x => x.Id.StartsWith("quest/spread/")), "Dead target retained visible spread.");
        npc = npc with { Dead = false, Targetable = false };
        e.Synchronize([boss, player, npc, a], player.Position, Start.AddSeconds(3.2), playerId: 100);
        Check(e.AmbientAoes().Any(x => x.Id.EndsWith("/display") && x.Id.StartsWith("quest/spread/")),
            "Live untargetable target lost retained component reference.");

        var (frozen, fb, fp) = MainScenario(1177, 0x4037);
        DungeonActor helper = new(2, 0x233C, fb.Position, Cast: new(34893, fb.Position, 0, Start, Start.AddSeconds(5), 100));
        frozen.Synchronize([fb, fp, helper], fp.Position, Start, playerId: 100);
        frozen.RemoveActor(100);
        fp = fp with { Position = fp.Position + Vector3.UnitX * 12 };
        frozen.Synchronize([fb, fp, helper, new(101, 0, fp.Position, PartySlot: 1)], fp.Position, Start.AddSeconds(1), playerId: 100);
        Check(frozen.AmbientAoes().Any(x => x.Id.EndsWith("/avoid") && x.Id.StartsWith("quest/spread/")
            && Vector3.Distance(x.Position, fb.Position) < .04f && x.Shape is CircleTemplate { Radius: 9 }),
            "Recreated viewer ID rebound old frozen actor reference.");
        var (stackFrozen, sb, sp) = MainScenario(1177, 0x4037);
        var origin = sb.Position;
        sp = sp with { Position = origin + Vector3.UnitX * 12 };
        DungeonActor oldNpc = new(101, 0x888, origin, FriendlyNpc: true);
        helper = helper with { Cast = new(34890, origin, 0, Start, Start.AddSeconds(5), 101) };
        stackFrozen.Synchronize([sb, sp, oldNpc, helper], sp.Position, Start, playerId: 100);
        stackFrozen.RemoveActor(101);
        stackFrozen.Synchronize([sb, sp, oldNpc, helper], sp.Position, Start.AddSeconds(1), playerId: 100);
        Check(!stackFrozen.Hints(sp.Position).Contains("Stack!"),
            "Recreated NPC sharing frozen stack target ID must count as a distinct buddy.");
    }

    private static void FrostyLockOn()
    {
        var (e, boss, player) = MainScenario(1010, 0x3646);
        var center = boss.Position;
        DungeonActor a = new(2, 0x3648, center, Dead: true, Targetable: false);
        DungeonActor b = new(3, 0x3648, center + Vector3.UnitX * 3);
        e.Synchronize([boss, player, a, b], player.Position, Start, playerId: 100);
        Check(e.AmbientAoes().Count(x => x.Id.StartsWith("frosty/lock/")) == 2
            && e.AmbientAoes().Where(x => x.Id.StartsWith("frosty/lock/")).All(x => x.Activation == Start.AddSeconds(6.6)),
            "Lock On create prediction filtered dead/untargetable or used wrong clock.");
        a = a with { Position = center + Vector3.UnitX * 7 };
        e.Synchronize([boss, player, a, b], player.Position, Start.AddSeconds(1), playerId: 100);
        e.RemoveActor(2);
        var old = e.AmbientAoes().Single(x => x.Id == "frosty/lock/prediction/1");
        a = a with { Position = center - Vector3.UnitX * 9 };
        e.Synchronize([boss, player, a, b], player.Position, Start.AddSeconds(2), playerId: 100);
        Check(e.AmbientAoes().Count(x => x.Id.StartsWith("frosty/lock/prediction/")) == 3
            && e.AmbientAoes().Single(x => x.Id == old.Id).Position == old.Position,
            "Lock On destruction removed frozen reference or rebound same ID generation.");
        var ca = new DungeonCastObservation(27461, center - Vector3.UnitZ * 6, 0, Start.AddSeconds(3), Start.AddSeconds(4.3));
        var cb = ca with { Action = 27463, Location = center + Vector3.UnitZ * 8 };
        e.StartCast(2, ca, Start.AddSeconds(3));
        e.StartCast(3, cb, Start.AddSeconds(3));
        var output = e.AmbientAoes().Where(x => x.Id.StartsWith("frosty/lock/")).ToArray();
        Check(output.Length == 3 && output[0].Id == old.Id && output[1].Position == ca.Location && output[2].Position == cb.Location,
            "Lock On handoff did not preserve predictions-first/actual-FIFO ordering.");
        e.Finish(3, 27463, Start.AddSeconds(3.5));
        Check(e.AmbientAoes().Single(x => x.Id.StartsWith("frosty/lock/cast/")).Position == cb.Location,
            "Lock On finish removed by caster instead of global FIFO.");
        e.Tick(Start.AddSeconds(100));
        Check(e.AmbientAoes().Count(x => x.Id.StartsWith("frosty/lock/")) == 2,
            "Lock On expired without actual finish.");
        e.Finish(2, 27461, Start.AddSeconds(101));
        Check(!e.AmbientAoes().Any(x => x.Id.StartsWith("frosty/lock/cast/")), "Lock On final finish leaked actual circle.");
    }

    private static void ColibriCharges()
    {
        var (e, boss, player) = MainScenario(1177, 0x4037);
        var center = boss.Position;
        var endpoint = center + Vector3.UnitX * 20;
        var cast = new DungeonCastObservation(35888, endpoint, null, Start, Start.AddSeconds(30));
        boss = boss with { Cast = cast };
        DungeonActor npc = new(101, 9, center, FriendlyNpc: true);
        DungeonActor ally = new(102, 0, center, PartySlot: 8, Dead: true, Targetable: false);
        void Sync(DateTime time) => e.Synchronize([boss, player, npc, ally], player.Position, time, playerId: 100);
        Sync(Start);
        var charge = e.AmbientAoes().Single(x => x.Id == "colibri/charge/1");
        Check(charge.Navigate && charge.Shape is RayRectangleTemplate { MinLocalX: -8, MaxLocalX: 8, Length: 20 }
            && Vector3.Distance(charge.Position, center) < .04f && MathF.Abs(charge.Rotation - MathF.PI / 2) < .001f,
            "Charge did not use full width16, endpoint heading, locked origin or dead/alliance-inclusive count.");
        boss = boss with { Position = center + Vector3.UnitZ * 9 };
        Sync(Start.AddSeconds(1));
        Check(Vector3.Distance(e.AmbientAoes().Single(x => x.Id == charge.Id).Position, charge.Position) < .04f, "Charge followed moving caster.");
        e.Synchronize([boss, player, npc], player.Position, Start.AddSeconds(2), playerId: 100);
        Check(!e.AmbientAoes().Single(x => x.Id == charge.Id).Navigate
            && e.Hints(player.Position).Contains("GTFO from aoe!"),
            "Two party members should disable only Deadly Swoop navigation.");
        e.Effect(1, 35888, 1, Start.AddSeconds(3));
        e.Tick(Start.AddSeconds(90));
        Check(e.AmbientAoes().Any(x => x.Id == charge.Id), "Charge effect/timeout ended component.");
        e.Finish(1, 35888, Start.AddSeconds(91));
        Check(!e.AmbientAoes().Any(x => x.Id.StartsWith("colibri/")), "Charge observed finish leaked.");
    }
}
