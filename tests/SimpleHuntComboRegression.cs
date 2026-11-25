using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;

internal static class SimpleHuntComboRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Baal first risky cone second preview and actual cast finish effect sequence", Baal);
        run("SimpleHunts Huracan both risky stages WinterRain tracking and actual effects", Huracan);
        run("SimpleHunts Grassman exact first subsequent thump counts continuous damage diagnostics", Grassman);
        run("SimpleHunts Maliktender persistent Haste and all four source rules", Maliktender);
        run("SimpleHunts stable raw cast duration and packet corrections never append or count phantom hits", Corrections);
        run("SimpleHunts FanAil dynamic Divebomb rotation fixed origin and no speculative second stage", FanAil);
    }

    private static void Baal()
    {
        foreach (var action in new uint[] { 17423, 17424 })
        {
            var encounter = new SimpleHuntEncounter();
            var boss = Boss(1, 0x2854, Cast(action, 5));
            encounter.Synchronize([boss], _ => true);
            var areas = encounter.Areas.Where(a => a.Shape != null).ToArray();
            Check(areas.Length == 2 && areas[0].Risky && !areas[1].Risky && areas[0].Color == new Vector4(1, 1, 0, 1)
                && areas[0].Activation == Start.AddSeconds(5.3)
                && areas[1].Activation == Start.AddSeconds(5.3).AddSeconds(2.3f), "Baal phase risk/color or clocks changed.");
            Check(Shape(areas[0]).Contains(Vector3.UnitZ * 29.99f, 0)
                && !Shape(areas[0]).Contains(Vector3.UnitZ * 30.01f, 0)
                && Shape(areas[1]).Contains(-Vector3.UnitZ * 29.99f, 0), "Baal R30 opposite semicircle changed.");
            Check(!encounter.Hints(new() { Position = -Vector3.UnitZ * 20 }).Contains("GTFO from aoe!"),
                "Baal preview-only second stage became a current warning.");
            encounter.Synchronize([boss with { Cast = null, Position = new(100, 0, 100), Rotation = 2 }], _ => true);
            areas = encounter.Areas.Where(a => a.Shape != null).ToArray();
            Check(areas.Length == 1 && areas[0].Risky && areas[0].Color == null && areas[0].Location == Vector3.Zero,
                "Baal cast finish failed to promote its locked second stage.");
            encounter.Effect(1, 17422, Start.AddSeconds(8), 10);
            encounter.Effect(1, 17422, Start.AddSeconds(8), 10);
            Check(!encounter.Areas.Any(a => a.Shape != null), "Baal effect did not consume its second stage.");
        }
    }

    private static void Huracan()
    {
        foreach (var action in new uint[] { 17495, 17497, 17498 })
        {
            var encounter = new SimpleHuntEncounter();
            var boss = Boss(1, 0x28B5, Cast(action, 4));
            encounter.Synchronize([boss], _ => true);
            var areas = encounter.Areas.Where(a => a.Shape != null).ToArray();
            Check(areas.Length == 2 && areas.All(a => a.Risky)
                && areas[1].Activation == boss.Cast!.FinishAt.AddSeconds(3.1), "Huracan lost concurrent navigation or the 3.1-second delay.");
            var moved = boss with { Position = new(20, 0, 30), Rotation = MathF.PI / 2 };
            encounter.Synchronize([moved], _ => true);
            var updated = encounter.Areas.Where(a => a.Shape != null).ToArray();
            if (action == 17497)
                Check(updated[1].Rotation == moved.Rotation && Vector3.Distance(updated[1].Location, new(-20, 0, 30)) < .05f
                    && updated[0].Location == Vector3.Zero, "WinterRain did not update only its rear-offset second rectangle.");
            else
                Check(updated[1].Location == areas[1].Location && updated[1].Rotation == areas[1].Rotation,
                    "A non-WinterRain combo incorrectly followed the actor.");
            encounter.Synchronize([moved with { Cast = null }], _ => true);
            Check(encounter.Areas.Count(a => a.Shape != null) == 2, "Huracan cast finish consumed an effect-driven stage.");
            encounter.Effect(1, action, Start.AddSeconds(4.1), 1);
            encounter.Effect(1, action, Start.AddSeconds(4.1), 1);
            var last = encounter.Areas.Single(a => a.Shape != null);
            encounter.Synchronize([moved with { Position = new(500, 0, 500), Rotation = 3, Cast = null }], _ => true);
            Check(encounter.Areas.Single(a => a.Shape != null) == last, "WinterRain kept tracking after its first effect.");
            encounter.Effect(1, 18042, Start.AddSeconds(7.4), 2);
            Check(!encounter.Areas.Any(a => a.Shape != null), "Windburst failed to consume the last stage.");
        }
    }

    private static void Grassman()
    {
        var encounter = new SimpleHuntEncounter();
        var player = new ReplayActorState { Id = 100 };
        var dead = new ReplayActorState { Id = 101, Dead = true };
        var npc = new ReplayActorState { Id = 102 };
        var alliance = new ReplayActorState { Id = 103 };
        ReplayPartyMember[] party = [new(0, "Tank", 100), new(1, "Healer", 101), new(24, "NPC", 102), new(8, "Alliance", 103)];
        var boss = Boss(1, 0x283A, null);
        encounter.Synchronize([boss, player, dead, npc, alliance], _ => true);
        Check(encounter.PredictedDamage(party).All(p => p.Activation == default) && encounter.PredictedDamage(party).Select(p => p.TargetId).Order().SequenceEqual([100UL, 103UL])
            && !encounter.Hints(null).Any(),
            "Grassman initial unknown-time prediction or slot/death filter changed.");
        var first = Cast(17859, 4);
        encounter.Start(1, first, true);
        Check(encounter.Hints(null).Single() == "Raidwide", "Grassman first cast was labeled x5.");
        encounter.Finish(1, 17859);
        Check(!encounter.Hints(null).Any() && encounter.PredictedDamage(party).All(p => p.Activation == first.FinishAt),
            "Grassman first finish failed to end hint or discarded persistent damage diagnostics.");
        var second = first with { StartedAt = Start.AddSeconds(10), FinishAt = Start.AddSeconds(14.3) };
        encounter.Start(1, second, true);
        encounter.Finish(1, 17859);
        Check(encounter.Hints(null).Single() == "Raidwide x5", "Grassman subsequent finish ended before four extra effects.");
        for (uint i = 1; i <= 3; ++i)
        {
            encounter.Effect(1, 17863, Start.AddSeconds(14 + i), i);
            encounter.Effect(1, 17863, Start.AddSeconds(14 + i), i);
            Check(encounter.Hints(null).Single() == "Raidwide x5", "Duplicate/early extra effect ended the thump sequence.");
        }
        encounter.Effect(1, 17863, Start.AddSeconds(18), 4);
        Check(!encounter.Hints(null).Any() && encounter.PredictedDamage(party).Count() == 2,
            "Fourth extra effect failed to end hint or incorrectly erased source persistent prediction.");
        encounter.Synchronize([boss with { InCombat = false }, player], _ => true);
        Check(!encounter.PredictedDamage(party).Any(), "Grassman disengagement retained damage state.");
    }

    private static void Maliktender()
    {
        var encounter = new SimpleHuntEncounter();
        var boss = Boss(1, 0x2874, Cast(18020, 3));
        encounter.Synchronize([boss], _ => true);
        encounter.Finish(1, 18020);
        encounter.Synchronize([boss with { Cast = null, Statuses = [] }], _ => true);
        Check(encounter.Hints(null).Single().Contains("instantly kill"), "Haste warning ended with cast cancellation or absent status.");
        foreach (var (action, inside, outside) in new[]
        {
            (18019u, new Vector3(0, 0, 7.99f), new Vector3(0, 0, 8.01f)),
            (18022u, new Vector3(3.99f, 0, 19.99f), new Vector3(4.01f, 0, 19.99f)),
            (18024u, new Vector3(3.99f, 0, 19.99f), new Vector3(0, 0, 20.01f))
        })
        {
            var cast = Cast(action, 3) with { StartedAt = Start.AddSeconds(action), FinishAt = Start.AddSeconds(action + 3.3) };
            encounter.Start(1, cast, true);
            var shape = Shape(encounter.Areas.Single(a => a.Shape != null));
            Check(shape.Contains(inside, 0) && !shape.Contains(outside, 0), "Maliktender source geometry changed.");
            encounter.Finish(1, action);
        }
        encounter.Synchronize([boss with { Cast = null }], _ => false);
        Check(!encounter.Hints(null).Any(), "Maliktender disable retained Haste.");
    }

    private static void Corrections()
    {
        foreach (var (oid, action) in new[] { (0x2854u, 17423u), (0x28B5u, 17495u) })
        {
            foreach (var total in new float?[] { null, 4 })
            {
                var encounter = new SimpleHuntEncounter();
                var cast = Cast(action, 4) with { TotalTime = total };
                var boss = Boss(1, oid, cast);
                encounter.Synchronize([boss], _ => true);
                encounter.Synchronize([boss with { Cast = cast with { FinishAt = cast.FinishAt.AddTicks(1) } }], _ => true);
                Check(encounter.Areas.Count(a => a.Shape != null) == 2, "Normal sample tick drift appended a phantom combo.");
                encounter.Start(1, cast with { Rotation = .1f }, true);
                Check(encounter.Areas.Count(a => a.Shape != null) == 2
                    && MathF.Abs(encounter.Areas.First(a => a.Shape != null).Rotation - .1f) < .0001f,
                    "Packet pose correction appended stages or failed to correct the current sequence.");
                encounter = new();
                encounter.Synchronize([boss], _ => true);
                var packet = cast with { StartedAt = cast.StartedAt.AddTicks(1), FinishAt = cast.FinishAt.AddTicks(1) };
                encounter.Start(1, packet, true);
                encounter.Start(1, packet with { Rotation = .2f }, true);
                Check(encounter.Areas.Count(a => a.Shape != null) == 2
                    && MathF.Abs(encounter.Areas.First(a => a.Shape != null).Rotation - .2f) < .0001f,
                    "A timestamp-only packet detached later pose corrections from the queued stages.");
                encounter.Effect(1, oid == 0x2854 ? 17422u : action, Start.AddSeconds(5), 1);
                var remaining = encounter.Areas.Single(a => a.Shape != null);
                encounter.Start(1, packet with { Rotation = .3f }, true);
                var corrected = encounter.Areas.Single(a => a.Shape != null);
                Check(corrected.Stage == 1 && corrected.SequenceId == remaining.SequenceId
                    && MathF.Abs(corrected.Rotation - (.3f + (oid == 0x2854 ? MathF.PI : 0))) < .0001f,
                    "A pose correction resurrected the consumed first stage or lost the remaining stage identity.");
            }
        }
        var grassman = new SimpleHuntEncounter();
        var first = Cast(17859, 4);
        grassman.Synchronize([Boss(1, 0x283A, first)], _ => true);
        grassman.Start(1, first with { Rotation = .1f }, true);
        Check(grassman.Hints(null).Single() == "Raidwide", "Packet correction counted a false completed first thump.");
        grassman.Finish(1, 17859);
        Check(!grassman.Hints(null).Any(), "First thump stayed x5 after a packet correction.");
        grassman = new();
        var grassBoss = Boss(1, 0x283A, first);
        grassman.Synchronize([grassBoss], _ => true);
        grassman.Start(1, first with { Rotation = .1f }, true);
        grassman.Synchronize([grassBoss with { Cast = first with { Rotation = .1f } }], _ => true);
        Check(grassman.Hints(null).Single() == "Raidwide x5",
            "Actual changed CastInfo catching up with a packet failed the source Finish -> Start transition.");
        grassman.Finish(1, 17859);
        Check(grassman.Hints(null).Single() == "Raidwide x5", "Restarted thump ended before its four effect hits.");
        for (uint i = 0; i < 4; ++i) grassman.Effect(1, 17863, Start.AddSeconds(10 + i), i + 1);
        Check(!grassman.Hints(null).Any(), "Restarted thump retained its hint after all real effects.");
    }

    private static void FanAil()
    {
        var encounter = new SimpleHuntEncounter();
        var origin = new Vector3(10, 0, 20);
        var cast = Cast(27373, 5) with { Location = origin };
        var boss = Boss(1, 0x35C1, cast);
        encounter.Synchronize([boss], _ => true);
        var moved = boss with { Position = new(100, 0, 100), Rotation = MathF.PI / 2 };
        encounter.Synchronize([moved], _ => true);
        var area = encounter.Areas.Single();
        var shape = Shape(area);
        Check(area.Location == origin && area.Rotation == MathF.PI / 2 && area.Activation == cast.FinishAt
            && shape.Contains(origin + new Vector3(29.99f, 0, 5.49f), 0)
            && !shape.Contains(origin + new Vector3(30.01f, 0, 0), 0)
            && !shape.Contains(origin + new Vector3(10, 0, 5.51f), 0),
            "Divebomb did not update rotation in the same fixed-origin geometry used by preview and navigation.");
        encounter.Effect(1, 27374, Start.AddSeconds(1));
        encounter.Effect(1, 27375, Start.AddSeconds(2));
        Check(encounter.Areas.Single() == area, "Disappear/Reappear invented or removed a Divebomb stage.");
        encounter.Finish(1, 27373);
        Check(!encounter.Areas.Any(), "Divebomb cancellation retained its dynamic rectangle.");
    }

    private static IGeometryShape Shape(HuntPendingCast area) => area.Shape!.Resolve(area.Location, area.Rotation, null);
    private static ReplayCast Cast(uint action, double seconds) => new(action, 0, Vector3.Zero, 0, Start, Start.AddSeconds(seconds + .3), false, false);
    private static ReplayActorState Boss(ulong id, uint oid, ReplayCast? cast)
        => new() { Hp = 1000, Id = id, Oid = oid, InCombat = true, Targetable = true, Cast = cast };
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
