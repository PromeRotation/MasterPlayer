using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntRecordedTurnsRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Omen all six histories four combos literal angles delays finish FIFO", OmenHistories);
        run("SimpleHunts Omen packet correction observation restart stale snapshots instance lifecycle", OmenEvents);
        run("SimpleHunts Wingblade partial histories effect FIFO first reset and persistent risk color", WingbladeHistories);
        run("SimpleHunts Wingblade packet corrections source cone width effect dedup and cleanup", WingbladeEvents);
        run("SimpleHunts Sugaar actual raw packet catchup appends independent rotating sequences", SugaarObservation);
        run("SimpleHunts historical sequences remain locked across equal packet confirmation and later corrections", HistoricalCorrections);
    }

    private static void OmenHistories()
    {
        float[][] histories =
        [
            [MathF.PI / 2], [-MathF.PI / 2], [MathF.PI / 2, -MathF.PI / 2],
            [MathF.PI / 2, MathF.PI / 2], [-MathF.PI / 2, MathF.PI / 2], [-MathF.PI / 2, -MathF.PI / 2]
        ];
        for (var history = 0; history < histories.Length; ++history)
        foreach (var action in new uint[] { 38632, 38633, 38634, 38635 })
        {
            var state = new HuntWhirlingOmenState();
            state.Start(Pending(0x43DB, (uint)(38626 + history), 0));
            // Omen casts do not overwrite unconsumed history.
            state.Start(Pending(0x43DB, 38631, 6));
            for (var stage = 0; stage < histories[history].Length; ++stage)
            {
                var cast = Pending(0x43DB, action, 10 + 10 * stage);
                state.Start(cast);
                var offset = histories[history][stage];
                float[] expected = action switch
                {
                    38632 => [0, offset + MathF.PI],
                    38633 => [MathF.PI, offset],
                    38634 => [offset, offset + MathF.PI],
                    _ => [offset + MathF.PI, offset]
                };
                var areas = state.Areas.ToArray();
                Check(areas.Length == (stage + 1) * 2 && areas.All(a => a.Risky), "Omen capped or de-risked future items.");
                for (var i = 0; i < 2; ++i)
                {
                    var area = areas[stage * 2 + i];
                    Check(Near(area.Rotation, cast.Rotation + expected[i])
                        && area.Location == cast.Location && area.Activation == cast.Activation.AddSeconds(i == 0 ? 1.4f : 3.8f),
                        "Omen offset matrix or float activation changed.");
                    Check(area.Shape is FanTemplate { Radius: 40 } fan && Near(fan.AngleRadians, 5 * MathF.PI / 6),
                        "Omen did not preserve its 150-degree cone.");
                }
            }
            state.Start(Pending(0x43DB, action, 40));
            Check(state.Areas.Count() == histories[history].Length * 2, "Empty Omen history invented a pair.");
            var count = state.Areas.Count();
            state.Finish(38632);
            Check(state.Areas.Count() == count, "Combo finish consumed a damage cast.");
            for (var i = 0; i < count - 1; ++i)
            {
                _ = state.Areas.ToArray();
                state.Finish((uint)(38636 + i % 6));
            }
            Check(state.Areas.Single().Color == null, "Last Omen item was colored without ever having a successor.");
            state.Finish(38641);
            Check(!state.Areas.Any(), "Final Omen finish retained a queued item.");
        }
    }

    private static void OmenEvents()
    {
        var encounter = new SimpleHuntEncounter();
        var boss = Boss(1, 0x43DB, null);
        encounter.Synchronize([boss, boss with { Id = 2 }], _ => true, Start);
        encounter.Start(1, Pending(0x43DB, 38628, 0).Input, true);
        var cast = Pending(0x43DB, 38632, 6).Input;
        encounter.Start(1, cast, true);
        encounter.Start(1, cast, true);
        var corrected = cast with { Location = new(10, 0, 20), Rotation = .7f, FinishAt = cast.FinishAt.AddSeconds(1) };
        encounter.Start(1, corrected, true);
        encounter.Synchronize([boss with { Cast = cast }, boss with { Id = 2 }], _ => true, Start.AddSeconds(7));
        var areas = Shapes(encounter);
        Check(areas.Length == 2 && areas.All(a => a.Location == corrected.Location)
            && areas[0].Activation == corrected.FinishAt.AddSeconds(1.4f), "Packet correction consumed history or stale frame reverted it.");
        encounter.Effect(1, 38636, Start.AddSeconds(8), 1);
        Check(Shapes(encounter).Length == 2, "Omen advanced on effect.");
        // Actual changed CastInfo is Finish -> Start and consumes the next recorded turn.
        encounter.Synchronize([boss with { Cast = corrected }, boss with { Id = 2 }], _ => true, Start.AddSeconds(8));
        areas = Shapes(encounter);
        Check(areas.Length == 4 && areas.Skip(2).All(a => a.Location == corrected.Location)
            && Near(areas[3].Rotation, corrected.Rotation!.Value + MathF.PI / 2),
            "Raw observation catching up with the packet swallowed Finish -> Start or used the wrong recorded turn.");
        var secondPacket = corrected with { Location = new(18, 0, 29) };
        encounter.Start(1, secondPacket, true);
        Check(Shapes(encounter).Take(2).All(a => a.Location == corrected.Location)
            && Shapes(encounter).Skip(2).All(a => a.Location == secondPacket.Location),
            "Correcting the new actual generation moved previous Omen predictions with an equal Input value.");
        var changed = corrected with { Location = new(12, 0, 23) };
        encounter.Synchronize([boss with { Cast = changed }, boss with { Id = 2 }], _ => true, Start.AddSeconds(9));
        Check(Shapes(encounter).Length == 4 && Shapes(encounter).Skip(2).All(a => a.Location == secondPacket.Location),
            "Exhausted history was consumed again or a later cast dragged an earlier sequence.");
        encounter.Synchronize([boss, boss with { Id = 2 }], _ => true, Start.AddSeconds(100));
        Check(Shapes(encounter).Length == 4, "Predicted time or combo ending removed Omen queue.");
        var damage = Pending(0x43DB, 38636, 101).Input;
        encounter.Start(1, damage, true);
        encounter.Finish(1, 38636, Start.AddSeconds(102));
        encounter.Finish(1, 38636, Start.AddSeconds(102));
        encounter.Start(1, damage, true);
        Check(Shapes(encounter).Length == 3, "Duplicate finish or stale damage start consumed another item.");
        encounter.Start(2, Pending(0x43DB, 38626, 103).Input, true);
        encounter.Start(2, Pending(0x43DB, 38634, 109).Input, true);
        Check(Shapes(encounter).Count(a => a.Source == 2) == 2, "Same-OID bosses shared history.");
        encounter.Remove(1);
        Check(Shapes(encounter).All(a => a.Source == 2), "Removing one hunt cleared the other instance.");
        encounter.Synchronize([boss with { Id = 2 }], _ => false, Start.AddSeconds(110));
        Check(!encounter.Active && !encounter.Areas.Any(), "Disabling the module leaked Omen history.");
        encounter.Reset();
        encounter.Synchronize([boss], _ => true, Start.AddSeconds(120));
        encounter.Start(1, Pending(0x43DB, 38634, 121).Input, true);
        Check(Shapes(encounter).Length == 0, "Reset retained recorded offsets.");
        encounter.Start(1, Pending(0x43DB, 38626, 127).Input, true);
        encounter.Start(1, Pending(0x43DB, 38632, 133).Input, true);
        encounter.Synchronize([boss with { Hp = 0 }], _ => true, Start.AddSeconds(134));
        Check(!encounter.Active && !encounter.Areas.Any(), "HP0 without Dead retained active hunt state.");
        encounter.Reset();
        encounter.Synchronize([boss], _ => true, Start.AddSeconds(140));
        encounter.Start(1, Pending(0x43DB, 38626, 140).Input, true);
        encounter.Start(1, Pending(0x43DB, 38632, 146).Input, true);
        var shortCast = Pending(0x43DB, 38636, 152).Input with { TotalTime = .8f };
        encounter.Synchronize([boss with { Cast = shortCast }], _ => true, Start.AddSeconds(152));
        encounter.Synchronize([boss with { Cast = shortCast with { TotalTime = .8005f } }], _ => true, Start.AddSeconds(153));
        Check(Shapes(encounter).Length == 1, "Short-cast real TotalTime change did not finish a queue head.");
        encounter.Synchronize([boss], _ => true, Start.AddSeconds(154));
        Check(Shapes(encounter).Length == 0, "Short-cast final finish did not consume the remaining head.");
    }

    private static void WingbladeHistories()
    {
        foreach (var count in new[] { 0, 1, 2, 3, 4 })
        {
            var state = new HuntWingbladeState();
            for (var i = 0; i < count; ++i)
            {
                state.Start(Pending(0x4232, i == 0 ? 37164u : i % 2 == 0 ? 37166u : 37167u, i * 4));
                state.Effect(37164, Start.AddSeconds(i * 4 + 4));
            }
            var cast = Pending(0x4232, 37170, 20);
            state.Start(cast);
            for (var i = 0; i < count; ++i)
            {
                var areas = state.Areas.ToArray();
                Check(areas.Length == Math.Min(2, count - i) && areas[0].Risky
                    && areas[0].Activation == cast.Activation.AddSeconds(.4f + i * 2f)
                    && Near(areas[0].Rotation, cast.Rotation + (i % 2 == 0 ? -MathF.PI / 2 : MathF.PI / 2)),
                    "Reflex forced three items or changed source delays/recorded side.");
                if (areas.Length == 2) Check(!areas[1].Risky, "Opposite future Wingblade became a navigation danger.");
                Check(state.Hint == "Sequence: " + string.Join(" -> ", Enumerable.Range(i, count - i)
                    .Select(n => n % 2 == 0 ? "Right" : "Left")), "Wingblade hint did not consume offsets with repeat effects.");
                state.Effect(37171, Start.AddSeconds(30 + i));
            }
            Check(!state.Areas.Any() && state.Hint == null, "Resolved partial history leaked hints or AOE.");
        }

        var persistent = new HuntWingbladeState();
        persistent.Start(Pending(0x4232, 37164, 0));
        var second = Pending(0x4232, 37167, 4) with { Rotation = MathF.PI };
        persistent.Start(second);
        Check(!persistent.Areas.Last().Risky, "Opposite second cone not de-risked.");
        var correction = second with { Input = second.Input with { Rotation = 1 }, Rotation = 1 };
        persistent.Correct(second, correction);
        Check(!persistent.Areas.Last().Risky, "Pose correction restored a persistent false Risky.");
        persistent.Effect(37164, Start.AddSeconds(10));
        Check(persistent.Areas.Single().Risky, "Promoted head failed to restore Risky.");
        persistent.Start(Pending(0x4232, 37165, 11));
        Check(persistent.Areas.Count() == 2 && persistent.Hint == "Sequence: Left",
            "First Wingblade cleared AOEs instead of only offset history.");
        _ = persistent.Areas.ToArray();
        persistent.Effect(37165, Start.AddSeconds(20));
        _ = persistent.Areas.ToArray();
        persistent.Start(Pending(0x4232, 37166, 21));
        _ = persistent.Areas.ToArray();
        persistent.Effect(37166, Start.AddSeconds(30));
        Check(persistent.Areas.Single().Color == null, "A never-colored future item inherited another item's color.");
        foreach (var delta in new[] { .5f, 1f, 1f + .000001f, 1.1f })
        {
            var edge = new HuntWingbladeState();
            var first = Pending(0x4232, 37164, 0) with { Rotation = delta * MathF.PI / 180 };
            edge.Start(first);
            edge.Start(Pending(0x4232, 37167, 4) with { Rotation = -MathF.PI });
            Check(edge.Areas.Last().Risky == (first.Rotation > MathF.PI / 180),
                "Wingblade 1-degree tolerance changed single-precision evaluation order.");
        }
    }

    private static void WingbladeEvents()
    {
        var encounter = new SimpleHuntEncounter();
        var boss = Boss(1, 0x4232, null);
        encounter.Synchronize([boss], _ => true, Start);
        var cast = Pending(0x4232, 37164, 0).Input;
        encounter.Start(1, cast, true);
        encounter.Start(1, cast with { Location = new(3, 0, 5), Rotation = MathF.PI / 2 }, true);
        var area = Shapes(encounter).Single();
        Check(area.Location == new Vector3(3, 0, 5) && Near(area.Rotation, MathF.PI / 2),
            "Wingblade packet correction duplicated or moved the wrong cone.");
        var geometry = area.Shape!.Resolve(area.Location, area.Rotation, null);
        Check(geometry.Contains(area.Location + new Vector3(1, 0, 20), 0)
            && !geometry.Contains(area.Location + new Vector3(-1, 0, 20), 0), "Wingblade used the stale 90-degree comment.");
        encounter.Finish(1);
        Check(Shapes(encounter).Length == 1, "Wingblade advanced on cast finish.");
        encounter.Effect(1, 37164, Start.AddSeconds(6), 10);
        encounter.Start(1, Pending(0x4232, 37167, 7).Input, true);
        encounter.Effect(1, 37164, Start.AddSeconds(8), 10);
        Check(Shapes(encounter).Length == 1, "Duplicate effect consumed the next cast.");
        encounter.Effect(1, 37167, Start.AddSeconds(12), 11);
        encounter.Start(1, Pending(0x4232, 37170, 13).Input, true);
        Check(Shapes(encounter).Length == 2 && encounter.Hints(null).Contains("Sequence: Right -> Left"),
            "Corrections duplicated the side history.");
        encounter.Synchronize([boss with { Targetable = false }], _ => true, Start.AddSeconds(100));
        Check(Shapes(encounter).Length == 2, "Untargetable stage or estimated activation removed Wingblade queue.");
        encounter.Effect(1, 37171, Start.AddSeconds(101), 12);
        Check(Shapes(encounter).Single().Risky && encounter.Hints(null).Contains("Sequence: Left"),
            "Repeat did not promote the second stage or consume history.");
        encounter.Synchronize([boss with { InCombat = false }], _ => true, Start.AddSeconds(102));
        Check(!encounter.Areas.Any() && !encounter.Hints(null).Any(), "Disengagement leaked Wingblade output.");
        encounter.Synchronize([boss], _ => true, Start.AddSeconds(110));
        var observed = Pending(0x4232, 37166, 110).Input with { TotalTime = 5 };
        encounter.Synchronize([boss with { Cast = observed }], _ => true, Start.AddSeconds(110));
        var corrected = observed with { Location = new(8, 0, 9) };
        encounter.Start(1, corrected, true);
        Check(Shapes(encounter).Length == 1, "Packet correction restarted Wingblade.");
        encounter.Synchronize([boss with { Cast = corrected }], _ => true, Start.AddSeconds(111));
        Check(Shapes(encounter).Length == 2 && encounter.Hints(null).Contains("Sequence: Right -> Right"),
            "Wingblade raw packet confirmation swallowed the actual cast restart.");
        encounter.Start(1, corrected with { Location = new(15, 0, 16) }, true);
        Check(Shapes(encounter)[0].Location == corrected.Location && Shapes(encounter)[1].Location == new Vector3(15, 0, 16),
            "Wingblade correction moved a historical item sharing the same Input.");
        encounter.Synchronize([boss with { Cast = corrected with { FinishAt = corrected.FinishAt.AddTicks(1) } }], _ => true, Start.AddSeconds(112));
        Check(Shapes(encounter).Length == 2 && encounter.Hints(null).Contains("Sequence: Right -> Right"),
            "Equal raw TotalTime restarted on reconstructed tick noise.");
        encounter.Remove(1);
        Check(!encounter.Areas.Any(), "Wingblade destruction leaked history.");
    }

    private static void SugaarObservation()
    {
        var encounter = new SimpleHuntEncounter();
        var cast = Pending(0x2875, 18100, 0).Input;
        var boss = Boss(1, 0x2875, cast);
        encounter.Synchronize([boss], _ => true, Start);
        var corrected = cast with { Rotation = .2f };
        encounter.Start(1, corrected, true);
        var firstIds = Shapes(encounter).Select(a => a.AreaId).ToArray();
        Check(firstIds.Length == 2, "Sugaar packet correction appended a real sequence.");
        encounter.Synchronize([boss with { Cast = corrected }], _ => true, Start.AddSeconds(1));
        var appended = Shapes(encounter);
        Check(appended.Length == 4 && appended.Take(2).Select(a => a.AreaId).SequenceEqual(firstIds)
            && appended.Select(a => a.AreaId).Distinct().Count() == 4,
            "Sugaar actual raw change swallowed an appended sequence or replaced previous identities.");
        encounter.Finish(1);
        for (var i = 0; i < 6; ++i)
        {
            var follow = Pending(0x2875, 18098, 10 + i * 4).Input;
            encounter.Start(1, follow, true);
            encounter.Finish(1, 18098, Start.AddSeconds(11 + i * 4));
        }
        Check(!Shapes(encounter).Any(), "Six actual finishes failed to resolve both real Sugaar sequences.");
    }

    private static void HistoricalCorrections()
    {
        foreach (var (oid, action, visible) in new (uint, uint, int)[]
        {
            (0x2854, 17423, 3), (0x28B5, 17495, 4), (0x2875, 18100, 4), (0x35BE, 27472, 4)
        })
        {
            var encounter = new SimpleHuntEncounter();
            var cast = Pending(oid, action, 0).Input;
            var boss = Boss(1, oid, cast);
            encounter.Synchronize([boss], _ => true, Start);
            var originalOrder = encounter.Casts.Single().Order;
            encounter.Start(1, cast, true);
            var corrected = cast with { Location = new(10, 0, 20), Rotation = .2f };
            encounter.Start(1, corrected, true);
            Check(encounter.Casts.Single().Order == originalOrder
                && Shapes(encounter).Any(a => a.Location == corrected.Location), "Equal packet confirmation detached the current generation.");
            encounter.Synchronize([boss with { Cast = corrected }], _ => true, Start.AddSeconds(1));
            Check(Shapes(encounter).Length == visible, "Actual raw restart lost old/new sequence overlap.");
            var historical = Shapes(encounter).Where(a => a.Order == originalOrder).ToDictionary(a => a.AreaId);
            Check(historical.Count != 0, "Fixture did not retain historical predictions.");
            foreach (var location in new[] { new Vector3(20, 0, 30), new Vector3(30, 0, 40) })
            {
                encounter.Start(1, corrected with { Location = location }, true);
                var retained = Shapes(encounter).Where(a => historical.ContainsKey(a.AreaId)).ToArray();
                Check(retained.Length == historical.Count, "A later packet deleted a historical sequence.");
                Check(retained
                    .All(a => a.Location == historical[a.AreaId].Location && a.Rotation == historical[a.AreaId].Rotation
                        && a.Activation == historical[a.AreaId].Activation), "A later packet dragged an actual historical sequence.");
                Check(Shapes(encounter).Any(a => a.Order != originalOrder && a.Location == location),
                    "A second packet lost the current generation's correction association.");
            }
        }

        var sally = new SimpleHuntEncounter();
        var actor = Boss(1, 0x4395, Pending(0x4395, 38454, 0).Input);
        sally.Synchronize([actor], _ => true, Start);
        var execute = Pending(0x4395, 38457, 6).Input;
        sally.Synchronize([actor with { Cast = execute }], _ => true, Start.AddSeconds(6));
        var packet = execute with { Location = new(10, 0, 20) };
        sally.Start(1, packet, true);
        sally.Synchronize([actor with { Cast = packet }], _ => true, Start.AddSeconds(7));
        sally.Start(1, packet with { Location = new(20, 0, 30) }, true);
        Check(Shapes(sally).Single().Location == packet.Location,
            "Empty-history real restart let a packet correct the already-ended Sally execution.");

        var sneeze = new SimpleHuntEncounter();
        var inhale = Pending(0x360A, 27307, 0).Input;
        var burfurlur = Boss(1, 0x360A, inhale);
        sneeze.Synchronize([burfurlur], _ => true, Start);
        inhale = Pending(0x360A, 27308, 6).Input with { Rotation = .2f };
        sneeze.Synchronize([burfurlur with { Cast = inhale }], _ => true, Start.AddSeconds(6));
        var inhalePacket = inhale with { Rotation = .4f };
        sneeze.Start(1, inhalePacket, true);
        sneeze.Synchronize([burfurlur with { Cast = inhalePacket }], _ => true, Start.AddSeconds(7));
        sneeze.Start(1, inhalePacket with { Rotation = .6f }, true);
        sneeze.Start(1, Pending(0x360A, 27309, 12).Input, true);
        sneeze.Finish(1, 27309, Start.AddSeconds(18));
        var preview = Shapes(sneeze);
        Check(preview.Length == 2 && Near(preview[0].Rotation, .4f) && Near(preview[1].Rotation, .6f),
            "Sneeze correction changed both the historical and current recorded inhale.");
    }

    private static ReplayActorState Boss(ulong id, uint oid, ReplayCast? cast)
        => new() { Id = id, Oid = oid, Targetable = true, InCombat = true, Hp = 100, Cast = cast };

    private static HuntPendingCast Pending(uint oid, uint action, double at)
    {
        var definition = SimpleHuntCatalog.Find(oid)!;
        var rule = definition.Casts.Single(c => c.Action == action);
        var cast = new ReplayCast(action, 1, Vector3.Zero, 0, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), false, false);
        return new(1, definition, rule, cast.Location, 0, cast.StartedAt, cast.FinishAt, 1, false, true, null, cast, cast,
            Order: (long)(at * 1000));
    }

    private static HuntPendingCast[] Shapes(SimpleHuntEncounter encounter) => encounter.Areas.Where(a => a.Shape != null).ToArray();
    private static bool Near(float a, float b) => MathF.Abs(MathF.Atan2(MathF.Sin(a - b), MathF.Cos(a - b))) < .00001f;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
