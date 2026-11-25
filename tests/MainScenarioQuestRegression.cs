using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static partial class JobQuestRegression
{
    private static (DungeonEncounter Encounter, DungeonActor Boss, DungeonActor Player) MainScenario(uint territory, uint oid)
    {
        var definition = MainScenarioQuestCatalog.Create(territory).Single(d => d.BossOid == oid);
        var center = definition.Arena(Vector3.Zero).Position;
        DungeonActor boss = new(1, oid, center, InCombat: true, IsEnemy: true, ObjectIndex: 2, AggroPlayer: true, Hp: 1000);
        DungeonActor player = new(100, 0, center, PartySlot: 0, Player: true);
        var e = new DungeonEncounter(territory);
        e.Synchronize([boss, player], center, Start, playerId: 100);
        return (e, boss, player);
    }

    private static void MainScenarioBasics()
    {
        foreach (var (territory, oid) in new (uint, uint)[] { (687, 0x1C48), (932, 0x2F1E), (932, 0x2F21), (977, 0x3202), (861, 0x299D) })
        {
            var (e, boss, player) = MainScenario(territory, oid);
            Check(e.Active?.BossOid == oid && ArrDungeonCatalog.Owns(oid, territory), "MSQ ownership/start missing.");
            var rule = e.Active!.Casts.First(c => c.Shape != null);
            DungeonActor helper = new(2, oid == 0x2F21 ? 0x2F22u : 0x233Cu, boss.Position,
                Cast: new(rule.Action, boss.Position + Vector3.UnitX, .7f, Start, Start.AddSeconds(3)));
            e.Synchronize([boss, player, helper], player.Position, Start, playerId: 100);
            e.Effect(2, rule.Action, 1, Start.AddSeconds(4));
            e.Tick(Start.AddSeconds(40));
            Check(e.OutputCasts.Single().Value.FinishAt == Start.AddSeconds(3), "MSQ actual lifetime replaced by effect/timeout.");
            e.Synchronize([boss, player, helper with { Dead = true }], player.Position, Start.AddSeconds(41), playerId: 100);
            Check(e.OutputCasts.Count() == 1, "Dead helper with ongoing cast prematurely removed.");
            e.Synchronize([boss, player, helper with { Cast = null }], player.Position, Start.AddSeconds(42), playerId: 100);
            Check(!e.OutputCasts.Any(), "MSQ observed Finish failed cleanup.");
            e.Synchronize([boss with { Hp = 0 }, player, helper], player.Position, Start.AddSeconds(43), playerId: 100);
            Check(e.Active == null, "Primary HP zero waited for surviving adds.");
        }
        var measure = MainScenarioQuestCatalog.Create(687).Single();
        Check(measure.Casts.Single(c => c.Action == 8398).Shape is CircleTemplate { Radius: 10 },
            "Moonlight used stale source comment instead of implemented radius.");
        var (moon, mb, mp) = MainScenario(687, 0x1C48);
        moon.Synchronize([mb, mp, new(101, 0, mp.Position, PartySlot: 8),
            new(102, 0, mp.Position, PartySlot: 1, Dead: true),
            new(103, 0x123, mp.Position, FriendlyNpc: true)], mp.Position, Start, playerId: 100);
        moon.StartCast(1, new(8398, mb.Position, 0, Start, Start.AddSeconds(7)), Start);
        Check(moon.PredictedDamage.Select(d => d.TargetId).Order().SequenceEqual(new ulong[] { 100, 103 }),
            "Moonlight omitted NPC or included alliance/dead member.");
        var (dragon, db, dp) = MainScenario(932, 0x2F21);
        dragon.Synchronize([db, dp, new(2, 0x2F22, db.Position, Cast: new(21098, db.Position, 1, Start, Start.AddSeconds(3)))],
            dp.Position, Start, playerId: 100);
        Check(dragon.OutputCasts.Single().Value.Rule.Shape is RayRectangleTemplate { MinLocalX: -4, MaxLocalX: 4, Length: 62 }
            && !dragon.AmbientAoes().Any(a => a.Id == "job-quest/enemy/2"), "Clone cast or primary-only enemy rendering.");
    }

    private static void LunarIfritFifo()
    {
        foreach (var (action, max) in new (uint, int)[] { (24054, 3), (24046, 2) })
        {
            var (e, boss, player) = MainScenario(977, 0x3202);
            var actors = new List<DungeonActor> { boss, player };
            for (var i = 0; i <= max; ++i)
                actors.Add(new((ulong)(10 + i), 0x233C, new(i, 0, 0),
                    Cast: new(action, new(i, 0, 0), i * .1f, Start.AddSeconds(i * .1), Start.AddSeconds(9 - i))));
            e.Synchronize(actors, player.Position, Start.AddSeconds(1), playerId: 100);
            Check(e.OutputCasts.Select(c => c.Value.Source).SequenceEqual(Enumerable.Range(10, max).Select(i => (ulong)i)),
                "Ifrit cap sorted by deadline instead of insertion order.");
            var next = actors.Last().Cast!;
            e.Finish(10, action, Start.AddSeconds(2));
            var promoted = e.OutputCasts.Last().Value;
            Check(e.OutputCasts.Count() == max && promoted.Source == (ulong)(10 + max)
                && promoted.FinishAt == next.FinishAt && promoted.Rotation == next.Rotation,
                "Ifrit promotion lost original clock or locked facing.");
            e.StartCast(30, next, Start.AddSeconds(2.1));
            e.Synchronize(actors.Concat([new DungeonActor(30, 0x233C, Vector3.Zero)]),
                player.Position, Start.AddSeconds(2.2), playerId: 100);
            e.StartCast(30, next, Start.AddSeconds(2.3));
            Check(e.Casts.ContainsKey(30) && e.OutputCasts.All(c => c.Value.Source != 30),
                "Dictionary slot reuse promoted a newly inserted caster before existing FIFO entries.");
            e.Finish(11, action, Start.AddSeconds(2.4));
            Check(e.OutputCasts.Any(c => c.Value.Source == 30 && c.Value.FinishAt == next.FinishAt
                && c.Value.Rotation == next.Rotation), "Accepted hidden caster failed later FIFO promotion.");
        }
        var (raid, rb, rp) = MainScenario(977, 0x3202);
        raid.Synchronize([rb, rp, new(101, 0, rp.Position, PartySlot: 8), new(102, 1, rp.Position, FriendlyNpc: true)],
            rp.Position, Start, playerId: 100);
        foreach (var (action, index) in new uint[] { 24058, 24059, 24062 }.Select((a, i) => (a, i)))
        {
            var time = Start.AddSeconds(index * 10);
            raid.StartCast(1, new(action, rb.Position, 0, time, time.AddSeconds(3)), time);
            Check(raid.PredictedDamage.Count() == 3 && raid.OutputCasts.All(c => c.Value.Rule.Shape == null),
                $"Ifrit {action} raidwide count={raid.PredictedDamage.Count()} active={raid.Active?.BossOid:X} lost alliance/NPC damage or invented geometry.");
            raid.Finish(1, action, time.AddSeconds(1));
            Check(!raid.PredictedDamage.Any(), "Ifrit observed finish retained damage prediction.");
        }
    }

    private static void LunarIfritPriorities()
    {
        var (e, boss, player) = MainScenario(977, 0x3202);
        DungeonActor nail = new(2, 0x3205, player.Position + Vector3.UnitX, IsEnemy: true, ObjectIndex: 4, Hp: 100);
        void Sync() => e.Synchronize([boss, player, nail], player.Position, Start, playerId: 100);
        Sync();
        Check(e.PriorityTarget(1, player.Position) == 2 && !e.ForbiddenTargets.Contains(2ul), "Nail priority or incidental attack permission.");
        nail = nail with { Hp = 1 };
        Sync();
        Check(e.PriorityTarget(2, player.Position) == 1, "PendingDead nail incorrectly regained Adds priority.");
        nail = nail with { Position = nail.Position + Vector3.UnitY * 13 };
        Sync();
        Check(e.PriorityTarget(1, player.Position) == 2, "Height branch did not precede PendingDead as source specifies.");
        nail = nail with { Dead = true };
        boss = boss with { AggroPlayer = false, TargetId = 0 };
        Sync();
        Check(e.PriorityTarget(1, player.Position) == 0 && e.TargetToClear(0) == 1, "No legal target failed selector hold.");
    }

    private static void OracleFifoAndKnockback()
    {
        var (e, boss, player) = MainScenario(861, 0x299D);
        var center = boss.Position;
        var actors = new List<DungeonActor> { boss, player };
        for (var i = 1; i <= 9; ++i)
            actors.Add(new((ulong)(10 + i), 0x2BE6, center,
                Cast: new(18035, center + Vector3.UnitX * i, 0, Start, Start.AddSeconds(20 - i), TotalTime: 4.5f)));
        e.Synchronize(actors, player.Position, Start, playerId: 100);
        Check(e.OutputCasts.Select(c => c.Value.Source).SequenceEqual(Enumerable.Range(11, 8).Select(i => (ulong)i)),
            "Oracle Burn first eight were not in start order.");
        e.Finish(12, 18035, Start.AddSeconds(1));
        actors.Add(new(30, 0x2BE6, center, Cast: new(18035, center, 0, Start.AddSeconds(2), Start.AddSeconds(3))));
        e.Synchronize(actors, player.Position, Start.AddSeconds(2), playerId: 100);
        Check(e.OutputCasts.Select(c => c.Value.Source).SequenceEqual(new ulong[] { 11, 13, 14, 15, 16, 17, 18, 19 }),
            "Oracle dictionary slot reuse broke FIFO.");
        var changed = actors[2] with { Cast = actors[2].Cast! with { Location = center - Vector3.UnitX } };
        actors[2] = changed;
        e.Synchronize(actors, player.Position, Start.AddSeconds(2.1), playerId: 100);
        Check(e.OutputCasts.Select(c => c.Value.Source).SequenceEqual(new ulong[] { 13, 14, 15, 16, 17, 18, 19, 30 }),
            "Real CastInfo change failed Finish/Start requeue.");
        e.Tick(Start.AddSeconds(90));
        Check(e.OutputCasts.Count() == 8, "Oracle Burn expired at predicted deadline.");

        var (kb, b, p) = MainScenario(861, 0x299D);
        p = p with { Position = center + Vector3.UnitX * 10, Rotation = .7f };
        var cast = new DungeonCastObservation(18036, center, 0, Start, Start.AddSeconds(5));
        DungeonActor source = new(2, 0x299E, center, Cast: cast);
        kb.Synchronize([b, p, source], p.Position, Start, playerId: 100);
        var marker = kb.AmbientAoes().Single(a => a.Id == "oracle/kb/2/player");
        Check(MathF.Abs(marker.Position.X - p.Position.X) < .001f
            && MathF.Abs(marker.Position.Z - (center.Z + MathF.Sqrt(300) - .001f)) < .001f
            && marker.Rotation == p.Rotation && !marker.Navigate && !marker.OutlineOnly
            && kb.AmbientAoes().Where(a => a.Id.StartsWith("oracle/")).All(a => !a.Navigate),
            "Oracle directional stopwall ray, actor heading or navigation contract.");
        kb.Synchronize([b, p, source with { Position = center + Vector3.UnitX * 3, Rotation = 2 }],
            p.Position, Start.AddSeconds(1), playerId: 100);
        Check(kb.AmbientAoes().Single(a => a.Id == marker.Id).Position == marker.Position,
            "Oracle actor movement rebound locked cast heading.");
        p = p with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = cast.FinishAt } };
        kb.Synchronize([b, p, source], p.Position, Start.AddSeconds(1.1), playerId: 100);
        Check(kb.AmbientAoes().Any(a => a.Id == marker.Id), "Oracle equal-expiry immunity accepted.");
        kb.StatusChanged(100, 160, true, Start.AddSeconds(1.2), expiresAt: cast.FinishAt.AddTicks(1));
        Check(!kb.AmbientAoes().Any(a => a.Id.StartsWith("oracle/")), "Oracle event immunity overlay ignored.");
        kb.StatusChanged(100, 160, false, Start.AddSeconds(1.3));
        DungeonActor second = new(3, 0x299E, center, Cast: cast with { Rotation = MathF.PI / 2 });
        kb.Synchronize([b, p, source, second], p.Position, Start.AddSeconds(1.4), playerId: 100);
        var secondMarker = kb.AmbientAoes().Single(a => a.Id == "oracle/kb/3/player");
        Check(secondMarker.Position.Z == marker.Position.Z && secondMarker.Position.X > marker.Position.X,
            "Oracle second KB did not continue from first landing.");
        kb.Finish(2, 18036, Start.AddSeconds(2));
        Check(!kb.AmbientAoes().Any(a => a.Id.StartsWith("oracle/kb/2/"))
            && MathF.Abs(kb.AmbientAoes().Single(a => a.Id == "oracle/kb/3/player").Position.Z - p.Position.Z) < .001f,
            "Oracle independent finish retained first KB contribution.");
        var (raw, rawBoss, rawPlayer) = MainScenario(861, 0x299D);
        var rawCast = cast with { TotalTime = 5.5f, FinishAt = Start.AddSeconds(5.8) };
        source = source with { Cast = rawCast };
        raw.Synchronize([rawBoss, rawPlayer, source], rawPlayer.Position, Start, playerId: 100);
        source = source with { Cast = rawCast with { TotalTime = 7.5f, FinishAt = Start.AddSeconds(7.8) } };
        raw.Synchronize([rawBoss, rawPlayer, source], rawPlayer.Position, Start.AddSeconds(1), playerId: 100);
        source = source with { Cast = source.Cast! with { Rotation = MathF.PI / 2 } };
        raw.Synchronize([rawBoss, rawPlayer, source], rawPlayer.Position, Start.AddSeconds(2), playerId: 100);
        var rawMarker = raw.AmbientAoes().Single(a => a.Id == "oracle/kb/2/player").Position;
        Check(rawMarker.X > center.X + 19 && MathF.Abs(rawMarker.Z - center.Z) < .001f,
            "Large raw duration replacement blocked subsequent real facing correction.");
        raw.StartCast(2, rawCast with { StartedAt = Start.AddSeconds(-1) }, Start.AddSeconds(2.1));
        Check(raw.AmbientAoes().Single(a => a.Id == "oracle/kb/2/player").Position == rawMarker,
            "Old packet undid accepted raw replacement.");
        foreach (var packetReplacement in new[] { false, true })
        {
            var (replacement, replacementBoss, replacementPlayer) = MainScenario(861, 0x299D);
            var original = rawCast;
            var revised = original with { TotalTime = 7.5f, FinishAt = Start.AddSeconds(7.8) };
            replacement.Synchronize([replacementBoss, replacementPlayer, source with { Cast = original }],
                replacementPlayer.Position, Start, playerId: 100);
            replacement.StartCast(2, revised, Start.AddSeconds(1), fromPacket: packetReplacement);
            replacement.Synchronize([replacementBoss, replacementPlayer, source with { Cast = revised }],
                replacementPlayer.Position, Start.AddSeconds(1.1), playerId: 100);
            var turned = revised with { Rotation = MathF.PI / 2 };
            replacement.Synchronize([replacementBoss, replacementPlayer, source with { Cast = turned }],
                replacementPlayer.Position, Start.AddSeconds(2), playerId: 100);
            Check(replacement.AmbientAoes().Single(a => a.Id == "oracle/kb/2/player").Position.X > center.X + 19,
                "Committed duration replacement left a tombstone blocking later real facing.");
            replacement.StartCast(2, turned with { Action = 9999, StartedAt = Start.AddSeconds(3) },
                Start.AddSeconds(3), fromPacket: false);
            replacement.StartCast(2, original, Start.AddSeconds(3.1));
            Check(!replacement.AmbientAoes().Any(a => a.Id.StartsWith("oracle/")),
                "Unconfigured action replacement removed tombstone and revived old KB.");
        }
    }
}
