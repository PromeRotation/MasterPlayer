using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntStoredRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Starcrier real status single bit no timer and foreign context isolation", Misdirection);
        run("SimpleHunts Storsie all aspects survive prepare and use live pose until actual finish", Storsie);
        run("SimpleHunts MoussePrincess status order locked direction live origin and actual resolve finish", Mousse);
        run("SimpleHunts Ophioneus jump prediction exact source geometry and final cast correction", Ophioneus);
        run("SimpleHunts Ruminator record reverse dynamic head actual effects and missing history", Ruminator);
        run("SimpleHunts Sphatika UI status order literal inverted queued finishes and no guessed clocks", Sphatika);
        run("SimpleHunts Gurangatch all eight rotations real finish clocks two previews and double strike", Gurangatch);
        run("SimpleHunts Burfurlur inhale direction memory sneeze reference and five real finishes", Burfurlur);
        run("SimpleHunts Minerva live target warning finish lock missile location and no guessed shape", Minerva);
        run("SimpleHunts Armstrong ten actual effects fifth-hit reversal and exact cross geometry", Armstrong);
    }

    private static void Misdirection()
    {
        ReplayPartyMember[] party = [new(0, "Tank", 100), new(8, "Alliance", 101), new(24, "NPC", 102)];
        foreach (var sid in new uint[] { 1422, 2936, 3694, 3909 })
        {
            var encounter = new SimpleHuntEncounter();
            var boss = Boss(1, 0x41FC, Cast(37040));
            var player = new ReplayActorState { Id = 100 };
            encounter.Synchronize([boss, player], _ => true);
            encounter.ObserveMisdirection(party);
            Check(!encounter.Misdirected(player, party) && !encounter.Areas.Any(a => a.Shape != null),
                "Dirge cast alone enabled input control or invented R40 damage.");
            encounter.Misdirection(100, sid, true, party);
            encounter.Synchronize([boss with { Cast = null }, player], _ => true);
            encounter.ObserveMisdirection(party);
            Check(encounter.Misdirected(player, party), "A pre-snapshot gain or ended cast lost misdirection.");
            player = player with { Statuses = [new(sid, 0, 1, Start.AddSeconds(-10))], Dead = true };
            encounter.Synchronize([boss with { Cast = null }, player], _ => true);
            encounter.ObserveMisdirection(party);
            Check(encounter.Misdirected(player, party), "A timer or personal death cleared the source mask.");
            encounter.Misdirection(100, 2936, true, party);
            encounter.Misdirection(100, sid, false, party);
            Check(!encounter.Misdirected(player, party), "Overlapping misdirection used a refcount instead of one source bit.");
            encounter.Misdirection(102, sid, true, party);
            Check(encounter.Misdirected(new() { Id = 102 }, party), "The source's valid NPC party slot lost its status bit.");
            encounter.Synchronize([boss with { InCombat = false }, player], _ => true);
            Check(!encounter.Misdirected(player, party), "Disengagement retained the mask.");
        }
        var foreign = new SimpleHuntEncounter();
        var otherPlayer = new ReplayActorState { Id = 100, Statuses = [new(1422, 0, 1, Start.AddSeconds(10))] };
        foreign.Synchronize([Boss(1, 0x3672, null), otherPlayer], _ => true);
        foreign.ObserveMisdirection(party);
        foreign.Misdirection(100, 1422, true, party);
        Check(!foreign.Misdirected(otherPlayer, party), "An unrelated hunt enabled misdirection.");
        var state = new HuntMisdirectionState();
        state.Status(100, 1422, true, []);
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = otherPlayer }, party);
        Check(state.Active(100, party), "Status preceding the party mapping was never acquired.");
        ReplayPartyMember[] swapped = [new(2, "Tank", 100), new(0, "Healer", 101)];
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = otherPlayer, [101] = new() { Id = 101 } }, swapped);
        Check(state.Active(100, swapped) && !state.Active(101, swapped), "PR slot reordering transferred misdirection to another actor.");
        state.Status(100, 1422, false, [new(0, "Healer", 101)]);
        Check(!state.Active(101, [new(0, "Healer", 101)]), "A departed member left a bit on the replacement member.");
    }

    private static void Storsie()
    {
        foreach (var (action, expected) in new[] { (27354u, typeof(FanTemplate)), (27355u, typeof(DonutTemplate)), (27356u, typeof(CircleTemplate)) })
        {
            var encounter = new SimpleHuntEncounter();
            var boss = Boss(1, 0x35DE, Cast(action));
            encounter.Synchronize([boss], _ => true);
            var area = Area(encounter);
            Check(area.Shape!.GetType() == expected && area.Activation == Start.AddSeconds(10.4), "Aspect geometry/10.4s clock changed.");
            var moved = boss with { Position = new(20, 0, 30), Rotation = 2, Cast = null };
            encounter.Synchronize([moved], _ => true);
            Check(Area(encounter).Location == moved.Position && Area(encounter).Rotation == 2, "Aspect did not survive prepare or follow current pose.");
            var final = Cast(27358, 12, 1) with { Rotation = .4f };
            encounter.Synchronize([moved with { Cast = final }], _ => true);
            Check(Area(encounter).Activation == area.Activation && Area(encounter).Rotation == .4f, "Actual Aspect attack reset prediction time or ignored cast rotation.");
            encounter.Finish(1, 27358);
            Check(!encounter.Areas.Any(a => a.Shape != null), "An actual attack finish did not clear a different selected aspect.");
        }
        var late = new SimpleHuntEncounter();
        late.Synchronize([Boss(1, 0x35DE, Cast(27354))], _ => true, Start.AddSeconds(1));
        Check(Area(late).Activation == Start.AddSeconds(1).AddSeconds(10.4), "Aspect was backdated instead of using first observed start.");
    }

    private static void Mousse()
    {
        foreach (var (sid, offset) in new[] { (2840u, -MathF.PI / 2), (2841u, MathF.PI / 2), (2842u, MathF.PI), (2958u, 0f) })
        {
            var encounter = new SimpleHuntEncounter();
            var boss = Boss(1, 0x360B, null);
            encounter.Synchronize([boss], _ => true);
            encounter.BossStatus(1, sid, Start, new(sid, 0, 1, Start.AddSeconds(20)));
            boss = boss with { Cast = Cast(27318) };
            encounter.Synchronize([boss], _ => true);
            Check(MathF.Abs(Area(encounter).Rotation - offset) < .0001f
                && Area(encounter).Activation == Start.AddSeconds(5.3), "Whimsy gain ahead of snapshot or prepare+2 clock was lost.");
            encounter.Start(1, boss.Cast! with { Rotation = .2f }, true);
            var moved = boss with { Cast = null, Position = new(100, 0, 100), Rotation = 3 };
            encounter.Synchronize([moved], _ => true);
            Check(MathF.Abs(Area(encounter).Rotation - offset - .2f) < .0001f && Area(encounter).Location == moved.Position,
                "Whimsy packet correction relocked the status or did not retain fixed direction/live position.");
            encounter.Start(1, Cast(27319, 10, 1), true);
            encounter.Finish(1, 27319);
            Check(!encounter.Areas.Any(a => a.Shape != null), "Resolve finish retained the predicted cone.");
        }
        var order = new SimpleHuntEncounter();
        order.Synchronize([Boss(1, 0x360B, Cast(27318)) with { Statuses = [new(2842, 0, 1, Start), new(2840, 0, 1, Start)] }], _ => true);
        Check(Area(order).Rotation == MathF.PI, "Whimsy selected numeric SID instead of first raw status.");
        var early = new SimpleHuntEncounter();
        var earlyBoss = Boss(1, 0x360B, null);
        early.Synchronize([earlyBoss], _ => true);
        early.Start(1, Cast(27318), true);
        early.Synchronize([earlyBoss with { Statuses = [new(2841, 0, 1, Start.AddSeconds(20))] }], _ => true);
        early.Synchronize([earlyBoss with { Cast = Cast(27318), Statuses = [new(2841, 0, 1, Start.AddSeconds(20))] }], _ => true);
        Check(Area(early).Rotation == MathF.PI / 2, "Packet before the first Whimsy observation froze a default direction.");
        var missing = new SimpleHuntEncounter();
        var directed = Cast(27318) with { Rotation = 1.2f };
        missing.Synchronize([Boss(1, 0x360B, directed) with { Statuses = [new(2840, 0, 1, Start.AddSeconds(20))] }], _ => true);
        missing.Start(1, directed with { Rotation = null }, true);
        Check(MathF.Abs(Area(missing).Rotation - (1.2f - MathF.PI / 2)) < .0001f, "A missing packet facing replaced known Whimsy direction.");
    }

    private static void Ophioneus()
    {
        foreach (var (jump, final) in new[] { (27341u, 27346u), (27342u, 27347u) })
        {
            var encounter = new SimpleHuntEncounter();
            var boss = Boss(1, 0x35DC, Cast(jump, 0, 6) with { Location = new(10, 0, 20) });
            encounter.Synchronize([boss], _ => true);
            var area = Area(encounter);
            Check(area.Activation == boss.Cast!.FinishAt.AddSeconds(5), "Jump prediction lost the extra5s.");
            encounter.Synchronize([boss with { Cast = null, Position = new(100, 0, 100) }], _ => true);
            Check(Area(encounter) == area, "Jump finish or boss movement erased/dragged the locked prediction.");
            encounter.Start(1, Cast(final, 10, 1) with { Location = new(30, 0, 40) }, true);
            area = Area(encounter);
            Check(area.Location == new Vector3(30, 0, 40) && area.Activation == Cast(final, 10, 1).FinishAt,
                "Final cast did not replace the single jump prediction.");
            var shape = area.Shape!.Resolve(area.Location, 0, null);
            Check(shape.Contains(area.Location + Vector3.UnitZ * 9.99f, 0)
                && (jump == 27341 ? shape.Contains(area.Location + Vector3.UnitZ * 39.99f, 0)
                    : !shape.Contains(area.Location + Vector3.UnitZ * 10.01f, 0)), "Source donut5-40/R10 changed.");
            encounter.Finish(1, final);
            Check(!encounter.Areas.Any(a => a.Shape != null), "Final Pyric finish leaked a predicted area.");
        }
        var missing = new SimpleHuntEncounter();
        var directed = Cast(27350) with { Rotation = 1.2f };
        missing.Synchronize([Boss(1, 0x35DC, directed)], _ => true);
        missing.Start(1, directed with { Rotation = null }, true);
        Check(Area(missing).Rotation == 1.2f, "A missing packet facing replaced a known Maw direction.");
    }

    private static void Ruminator()
    {
        var encounter = new SimpleHuntEncounter();
        var boss = Boss(1, 0x35BD, null);
        encounter.Synchronize([boss], _ => true);
        encounter.Start(1, Cast(26874), true);
        encounter.Start(1, Cast(26876, 5), true);
        encounter.Start(1, Cast(26877, 10), true);
        Check(!encounter.Areas.Any(a => a.Shape != null), "Trace recording leaked a visible damage area.");
        encounter.Start(1, Cast(26915, 15), true);
        Check(Area(encounter).Shape is DonutTemplate && Area(encounter).Activation == default, "Reversal did not reverse the recorded shapes or invented a clock.");
        encounter.Finish(1, 26915);
        Check(Area(encounter).Shape is DonutTemplate, "CastFinished consumed an effect-driven trace.");
        encounter.Synchronize([boss with { Position = new(30, 0, 40) }], _ => true);
        Check(Area(encounter).Location == new Vector3(30, 0, 40), "Ruminator head did not follow the boss.");
        encounter.Effect(1, 26915, Start.AddSeconds(19), 1);
        encounter.Effect(1, 26915, Start.AddSeconds(19), 1);
        Check(Area(encounter).Shape is CircleTemplate, "Duplicate targets consumed more than one trace.");
        encounter.Effect(1, 26167, Start.AddSeconds(20), 2);
        Check(!encounter.Areas.Any(a => a.Shape != null), "Last reversal effect retained the head.");
        encounter.Reset();
        encounter.Synchronize([boss with { Cast = Cast(26878) }], _ => true);
        Check(!encounter.Areas.Any(a => a.Shape != null), "A late join guessed missing trace history.");
        encounter.Effect(1, 26878, Start.AddSeconds(4), 3);
        encounter.Start(1, Cast(26876, 5), true);
        Check(Area(encounter).Shape is CircleTemplate, "An empty effect incorrectly cleared the source active latch.");
    }

    private static void Sphatika()
    {
        foreach (var (action, inverted) in new[] { (27624u, 0f), (27625u, MathF.PI) })
        {
            var encounter = new SimpleHuntEncounter();
            var boss = Boss(1, 0x3670, Cast(action)) with
            {
                Statuses = [new(2838, 0, 1, Start), new(2835, 0, 1, Start), new(2837, 0, 1, Start), new(2836, 0, 1, Start)]
            };
            encounter.Synchronize([boss], _ => true);
            Check(Area(encounter).Activation == default, "Sphatika invented an activation timeline.");
            encounter.Finish(1, action);
            var expected = new[] { 0f, MathF.PI, MathF.PI, 2 * MathF.PI, MathF.PI / 2, 3 * MathF.PI / 2, -MathF.PI / 2, MathF.PI / 2 };
            for (var i = 0; i < expected.Length; ++i)
            {
                Check(MathF.Abs(Area(encounter).Rotation - expected[i] - inverted) < .0001f, $"Sphatika UI order/direction changed at{i}.");
                encounter.Start(1, Cast(i % 2 == 0 ? 27626u : 27715u, 10 + i * 2, 1), true);
                encounter.Finish(1);
            }
            Check(!encounter.Areas.Any(a => a.Shape != null), "Sphatika retained its eighth cleave.");
            encounter.Synchronize([boss], _ => false);
            Check(!encounter.Areas.Any(a => a.Shape != null), "Disabled stance retained its queue.");
        }
        var early = new SimpleHuntEncounter();
        var initial = Boss(1, 0x3670, null);
        early.Synchronize([initial], _ => true);
        early.Start(1, Cast(27624), true);
        var gained = initial with { Statuses = [new(2835, 0, 1, Start.AddSeconds(20))] };
        early.Synchronize([gained], _ => true);
        early.Synchronize([gained with { Cast = Cast(27624) }], _ => true);
        Check(Area(early).Rotation == 0, "Packet before the first bearing observation froze an empty stance.");
        var expired = new SimpleHuntEncounter();
        expired.Synchronize([initial], _ => true);
        expired.BossStatus(1, 2835, Start, new(2835, 0, 1, Start.AddSeconds(2)));
        expired.Synchronize([initial], _ => true, Start.AddSeconds(3));
        expired.Start(1, Cast(27624, 4), true);
        Check(!expired.Areas.Any(a => a.Shape != null), "An unacknowledged expired bearing was retained for a later stance.");
    }

    private static HuntPendingCast Area(SimpleHuntEncounter encounter) => encounter.Areas.Single(a => a.Shape != null);

    private static void Gurangatch()
    {
        foreach (var (action, count, increment) in new[]
        {
            (27493u, 2, MathF.PI), (27494u, 2, MathF.PI),
            (27497u, 8, MathF.PI / 2), (27498u, 8, MathF.PI / 2),
            (27521u, 8, -MathF.PI / 2), (27522u, 8, -MathF.PI / 2)
        })
        {
            var encounter = new SimpleHuntEncounter();
            var cast = Cast(action, 0, 9);
            var boss = Boss(1, 0x361B, cast);
            encounter.Synchronize([boss], _ => true);
            for (var i = 0; i < count; ++i)
            {
                var areas = encounter.Areas.Where(a => a.Shape != null).ToArray();
                Check(areas.Length == Math.Min(count - i, count == 2 ? 1 : 2) && areas.All(a => a.Risky),
                    "Gurangatch lost visible next-stage navigation or double-strike display limit.");
                Check(MathF.Abs(areas[0].Rotation - increment * i) < .0001f && areas[0].Location == Vector3.Zero,
                    "Gurangatch changed its locked origin or rotation increment.");
                Check(areas[0].Color == (count > 2 && i < count - 1 ? new Vector4(1, 1, 0, 1) : null),
                    "Gurangatch current/final color changed.");
                var finished = Start.AddSeconds(10 + i * 5);
                if (i > 0)
                {
                    encounter.Start(1, Cast(count == 2 ? 27495u : 27499u, 10 + i * 5 - 1, 1) with { Rotation = null }, true);
                    Check(encounter.Areas.Count(a => a.Shape != null) == areas.Length, "A followup cast created another rotation sequence.");
                }
                encounter.Finish(1, at: finished);
                if (i < count - 1)
                    Check(encounter.Areas.First(a => a.Shape != null).Activation == finished.AddSeconds(3.6f),
                        "Next slammer was not retimed from the actual finished event.");
            }
            Check(!encounter.Areas.Any(a => a.Shape != null), "Final slammer retained a queued half-plane.");
        }
        var corrected = new SimpleHuntEncounter();
        var initial = Cast(27497, 0, 9);
        corrected.Synchronize([Boss(1, 0x361B, initial)], _ => true);
        var packet = initial with { TotalTime = 10, FinishAt = initial.FinishAt.AddSeconds(1), Rotation = .2f };
        corrected.Start(1, packet, true);
        var visible = corrected.Areas.Where(a => a.Shape != null).ToArray();
        Check(visible[0].Activation == packet.FinishAt && visible[1].Activation == packet.FinishAt.AddSeconds(3.6f),
            "A first-cast clock correction failed to shift its visible next stage.");
        corrected.Finish(1, at: Start.AddSeconds(12));
        var advanced = corrected.Areas.First(a => a.Shape != null);
        corrected.Start(1, initial with { StartedAt = Start.AddSeconds(15), FinishAt = Start.AddSeconds(24.3) }, true);
        Check(corrected.Areas.First(a => a.Shape != null).Activation == advanced.Activation,
            "A later independent rotation reset an already advanced sequence clock.");
        var raidwide = new SimpleHuntEncounter();
        raidwide.Synchronize([Boss(1, 0x361B, Cast(27512)), new() { Id = 100 }], _ => true);
        Check(!raidwide.Areas.Any(a => a.Shape != null)
            && raidwide.PredictedDamage([new(0, "Tank", 100)]).Single().DamageType == "Raidwide",
            "BoneShaker became a dodging circle or lost unavoidable damage diagnostics.");
    }

    private static void Burfurlur()
    {
        var encounter = new SimpleHuntEncounter();
        var boss = Boss(1, 0x360A, null);
        encounter.Synchronize([boss], _ => true);
        var angles = new[] { .4f, .8f, -.2f, 1.6f, -1f };
        for (var i = 0; i < angles.Length; ++i)
            encounter.Start(1, Cast(i == 0 ? 27307u : i % 2 == 0 ? 27692u : 27308u, i * 4) with { Rotation = angles[i] }, true);
        Check(!encounter.Areas.Any(a => a.Shape != null), "Inhale was incorrectly shown as an attack.");
        encounter.Start(1, Cast(27309, 30) with { Rotation = 2 }, true);
        for (var i = 0; i < angles.Length; ++i)
        {
            var areas = encounter.Areas.Where(a => a.Shape != null).ToArray();
            Check(areas.Length == Math.Min(2, angles.Length - i) && areas.All(a => a.Risky)
                && MathF.Abs(areas[0].Rotation - (2 + angles[i] - angles[0])) < .0001f,
                "Sneeze direction memory or new reference/display limit changed.");
            var finish = Start.AddSeconds(34 + i * 4);
            encounter.Finish(1, at: finish);
            if (i < angles.Length - 1)
            {
                encounter.Synchronize([boss with { Position = new(20, 0, 30) }], _ => true);
                Check(encounter.Areas.First(a => a.Shape != null).Location == new Vector3(20, 0, 30)
                    && encounter.Areas.First(a => a.Shape != null).Activation == finish.AddSeconds(2.2),
                    "Sneeze failed to follow the actor or actual-finish+2.2 clock.");
                encounter.Start(1, Cast(i % 2 == 0 ? 27310u : 27693u, 36 + i * 4), true);
            }
        }
        Check(!encounter.Areas.Any(a => a.Shape != null), "Fifth real sneeze finish retained an attack.");
        encounter.Reset();
        encounter.Synchronize([boss with { Cast = Cast(27309) }], _ => true);
        Check(!encounter.Areas.Any(a => a.Shape != null), "A late join guessed five missing inhale directions.");
        encounter.Reset();
        encounter.Synchronize([boss], _ => true);
        var inhale = Cast(27307) with { Rotation = .4f };
        encounter.Start(1, inhale, true);
        encounter.Start(1, Cast(27308, 5) with { Rotation = .8f }, true);
        encounter.Start(1, Cast(27310, 10), true);
        encounter.Finish(1, at: Start.AddSeconds(12));
        Check(MathF.Abs(Area(encounter).Rotation - .8f) < .0001f
            && Area(encounter).Activation == Start.AddSeconds(12).AddSeconds(2.2),
            "A later sneeze finish failed to activate remaining known offsets without a first-sneeze observation.");
        encounter.Reset();
        encounter.Synchronize([boss], _ => true);
        encounter.Start(1, inhale, true);
        var secondInhale = Cast(27308, 5) with { Rotation = .8f };
        encounter.Start(1, secondInhale, true);
        var firstSneeze = Cast(27309, 10) with { Rotation = 2 };
        encounter.Start(1, firstSneeze, true);
        encounter.Start(1, firstSneeze with { Rotation = 2.2f }, true);
        var remembered = encounter.Areas.Where(a => a.Shape != null).ToArray();
        Check(MathF.Abs(remembered[0].Rotation - 2.2f) < .0001f
            && MathF.Abs(remembered[1].Rotation - 2.6f) < .0001f,
            "Sneeze packet correction changed the remembered inhale reference.");
        encounter.Finish(1, at: Start.AddSeconds(14));
        encounter.Start(1, Cast(27692, 15) with { Rotation = 3 }, true);
        Check(MathF.Abs(encounter.Areas.Last(a => a.Shape != null).Rotation - 3) < .0001f,
            "An inhale after sneeze did not use the current reference.");
        encounter.Reset();
        encounter.Synchronize([boss], _ => true);
        encounter.Start(1, Cast(27314, 5), true);
        var uppercut = Area(encounter).Shape!.Resolve(Vector3.Zero, 0, null);
        Vector3 At(float degrees) => new(MathF.Sin(degrees * MathF.PI / 180) * 10, 0, MathF.Cos(degrees * MathF.PI / 180) * 10);
        Check(uppercut.Contains(Vector3.UnitZ * 14.99f, 0) && !uppercut.Contains(Vector3.UnitZ * 15.01f, 0)
            && uppercut.Contains(At(59.9f), 0) && !uppercut.Contains(At(60.1f), 0), "Uppercut R15/120-degree shape changed.");
        encounter.Start(1, Cast(27313, 10), true);
        var spores = Area(encounter).Shape!.Resolve(Vector3.Zero, 0, null);
        Check(spores.Contains(Vector3.UnitZ * 5.99f, 0) && !spores.Contains(Vector3.UnitZ * 6.01f, 0), "RottenSpores R6 changed.");
    }
    private static void Minerva()
    {
        foreach (var (warning, actual) in new[] { (27517u, 27299u), (27518u, 27300u) })
        {
            var encounter = new SimpleHuntEncounter();
            var boss = Boss(1, 0x3609, null);
            var target = new ReplayActorState { Id = 100, Position = new(10, 0, 20) };
            encounter.Synchronize([boss, target], _ => true);
            encounter.Start(1, Cast(27297), true);
            Check(!encounter.Areas.Any(a => a.Shape != null), "Build guessed a missile before target selection.");
            encounter.Start(1, Cast(warning, 6), true);
            Check(Area(encounter).Location == target.Position && Area(encounter).Activation == default,
                "Missile warning lost its immediately effective shape at the selected target.");
            var shapeType = Area(encounter).Shape!.GetType();
            target = target with { Position = new(30, 0, 40), Dead = true };
            encounter.Synchronize([boss, target], _ => true);
            Check(Area(encounter).Location == target.Position, "Missile warning stopped following a target that moved/died.");
            encounter.Finish(1, warning);
            target = target with { Position = new(50, 0, 60) };
            encounter.Synchronize([boss, target], _ => true);
            Check(Area(encounter).Location == new Vector3(30, 0, 40), "Warning finish failed to lock the last target position.");
            var missile = Cast(actual, 15) with { Location = new(70, 0, 80) };
            encounter.Start(1, missile, true);
            Check(Area(encounter).Shape!.GetType() == shapeType && Area(encounter).Location == missile.Location,
                "Actual missile changed the selected shape or failed to set its fixed landing.");
            encounter.Start(1, missile with { Location = new(90, 0, 100) }, true);
            Check(Area(encounter).Location == new Vector3(90, 0, 100), "Actual missile packet correction did not move the landing.");
            encounter.Finish(1, actual);
            Check(!encounter.Areas.Any(a => a.Shape != null), "Actual missile finish retained its warning shape.");
            encounter.Reset();
            encounter.Synchronize([boss with { Cast = missile }], _ => true);
            Check(!encounter.Areas.Any(a => a.Shape != null), "A late actual missile guessed the missing selected shape.");
            encounter.Start(1, Cast(warning, 30) with { TargetId = 999 }, true);
            Check(Area(encounter).Location == missile.Location, "An unresolved warning target discarded the existing fixed location.");
            encounter.Finish(1, warning);
            Check(Area(encounter).Location == Vector3.Zero, "An unresolved warning finish did not use the source default location.");
        }
        var removed = new SimpleHuntEncounter();
        var actor = Boss(1, 0x3609, null);
        var selected = new ReplayActorState { Id = 100, Position = new(30, 0, 40) };
        var selection = Cast(27517);
        removed.Synchronize([actor, selected], _ => true);
        removed.Start(1, selection, true);
        removed.Remove(100);
        removed.Start(1, selection with { Rotation = .2f }, true);
        Check(Area(removed).Location == selected.Position, "Warning correction lost the last removed target position.");
        removed.Synchronize([actor, selected with { Position = new(70, 0, 80) }], _ => true);
        Check(Area(removed).Location == selected.Position, "Warning rebound to a new actor using the removed target ID.");
        removed.Finish(1, 27517);
        removed.Start(1, Cast(27300, 10) with { Location = new(50, 0, 60) }, true);
        Check(Area(removed).Shape is CircleTemplate, "A mismatched actual missile replaced the selected warning shape.");
    }

    private static void Armstrong()
    {
        foreach (var (direction, increment) in new[] { (27470u, -MathF.PI / 6), (27471u, MathF.PI / 6), (0u, 0f) })
        {
            var encounter = new SimpleHuntEncounter();
            var boss = Boss(1, 0x35BE, null);
            encounter.Synchronize([boss], _ => true);
            if (direction != 0) encounter.Effect(1, direction, Start, 1);
            encounter.Start(1, Cast(27472, 1, 6), true);
            encounter.Finish(1, 27472);
            var rotation = 0f;
            for (var i = 0; i < 10; ++i)
            {
                var areas = encounter.Areas.Where(a => a.Shape != null).ToArray();
                Check(areas.Length == Math.Min(2, 10 - i) && areas.All(a => a.Risky)
                    && MathF.Abs(areas[0].Rotation - rotation) < .0001f, $"Compressor sequence direction/count changed at{i}.");
                Check(areas[0].Color == (i < 9 ? new Vector4(1, 1, 0, 1) : null), "Compressor current/final color changed.");
                if (i == 0)
                {
                    var shape = areas[0].Shape!.Resolve(Vector3.Zero, 0, null);
                    Check(shape.Contains(new(3.49f, 0, 49.99f), 0) && shape.Contains(new(-49.99f, 0, -3.49f), 0)
                        && !shape.Contains(new(3.51f, 0, 10), 0) && !shape.Contains(new(0, 0, 50.01f), 0),
                        "Compressor source cross50/half-width3.5 changed.");
                }
                var at = Start.AddSeconds(10 + i * 3);
                var action = i == 0 ? 27472u : i == 5 ? 27473u : 27474u;
                encounter.Effect(1, action, at, (uint)(10 + i));
                encounter.Effect(1, action, at, (uint)(10 + i));
                rotation += i < 5 ? increment : -increment;
                if (i < 9)
                    Check(encounter.Areas.First(a => a.Shape != null).Activation == at.AddSeconds(i == 4 ? 3.6 : 2.1f),
                        "Compressor lost actual effect time or the fifth-hit3.6s pause.");
            }
            Check(!encounter.Areas.Any(a => a.Shape != null), "Compressor tenth effect retained a cross.");
        }
        var corrected = new SimpleHuntEncounter();
        var initial = Cast(27472, 0, 6);
        corrected.Synchronize([Boss(1, 0x35BE, initial)], _ => true);
        corrected.Start(1, initial with { Rotation = .2f, FinishAt = initial.FinishAt.AddSeconds(1), TotalTime = 7 }, true);
        var first = corrected.Areas.First(a => a.Shape != null);
        Check(first.Rotation == .2f && first.Activation == initial.FinishAt.AddSeconds(1), "Compressor packet correction appended or missed the first cross.");
        corrected.Effect(1, 27472, Start.AddSeconds(10), 1);
        corrected.Start(1, initial with { Rotation = .3f, FinishAt = initial.FinishAt.AddSeconds(2), TotalTime = 8 }, true);
        Check(corrected.Areas.Count(a => a.Shape != null) == 2
            && corrected.Areas.First(a => a.Shape != null).Activation == Start.AddSeconds(10).AddSeconds(2.1f),
            "Compressor packet correction reset an effect-advanced clock.");
        corrected.Synchronize([Boss(1, 0x35BE, null)], _ => false);
        Check(!corrected.Areas.Any(), "Disabled Armstrong retained its rotation state.");
        var overlap = new SimpleHuntEncounter();
        overlap.Synchronize([Boss(1, 0x35BE, null)], _ => true);
        overlap.Effect(1, 27471, Start, 1);
        overlap.Start(1, Cast(27472), true);
        for (var i = 0; i < 5; ++i) overlap.Effect(1, 27474, Start.AddSeconds(10 + i), (uint)(10 + i));
        overlap.Start(1, Cast(27472, 20), true);
        for (var i = 0; i < 5; ++i) overlap.Effect(1, 27474, Start.AddSeconds(30 + i), (uint)(20 + i));
        var head = overlap.Areas.Where(a => a.Shape != null).ToArray();
        Check(head.Length == 2 && head[0].Rotation == 0 && head[1].Rotation == -MathF.PI / 6
            && head[0].Activation == Start.AddSeconds(34).AddSeconds(3.6),
            "The global fifth compressor effect failed to reverse a newly promoted overlapping sequence.");
        var instances = new SimpleHuntEncounter();
        instances.Synchronize([Boss(1, 0x35BE, Cast(27472)), Boss(2, 0x35BE, Cast(27472))], _ => true);
        instances.Effect(1, 27472, Start.AddSeconds(5), 0);
        instances.Effect(1, 27472, Start.AddSeconds(5), 0);
        instances.Effect(2, 27472, Start.AddSeconds(5), 0);
        instances.Remove(1);
        var instanceAreas = instances.Areas.Where(a => a.Shape != null).ToArray();
        Check(instanceAreas.Length == 2 && instanceAreas.All(a => a.Source == 2 && a.Stage == 1),
            "Zero-sequence duplicate effects or removal crossed compressor instances.");
    }

    private static ReplayCast Cast(uint action, double start = 0, double duration = 3)
        => new(action, 100, Vector3.Zero, 0, Start.AddSeconds(start), Start.AddSeconds(start + duration + .3), false, false) { TotalTime = (float)duration };
    private static ReplayActorState Boss(ulong id, uint oid, ReplayCast? cast)
        => new() { Id = id, Oid = oid, Targetable = true, InCombat = true, Cast = cast };
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
