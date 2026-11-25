using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntRebellionRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly ReplayPartyMember[] Party = [new(0, "P", 100)];
    private static ReplayActorState Boss(ulong id = 1) => new()
        { Id = id, Oid = 0x28B6, Kind = 2, Hp = 1000, InCombat = true, Targetable = true };
    private static ReplayActorState Helper(ulong id = 999, ReplayCast? cast = null, Vector3? position = null) => new()
        { Id = id, Oid = 0x233C, Kind = 2, Cast = cast, Position = position ?? Vector3.Zero };
    private static ReplayActorState Player(ReplayStatus? status = null) => new()
        { Id = 100, Hp = 1000, Statuses = status == null ? [] : [status] };
    private static ReplayCast Cast(uint action, double at = 0, float rotation = 0, double duration = 5)
        => new(action, 999, new(100, 0, 100), rotation, Start.AddSeconds(at), Start.AddSeconds(at + duration), false, false)
            { TotalTime = (float)duration };
    private static SimpleHuntEncounter Create()
    {
        var e = new SimpleHuntEncounter();
        e.Synchronize([Boss(), Helper(), Helper(998), Player()], _ => true, Start, Party);
        return e;
    }
    private static HuntPendingCast[] Areas(SimpleHuntEncounter e, string group)
        => e.Areas.Where(c => c.SequenceId?.Contains("/rebellion/" + group) == true).ToArray();
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Near(float actual, float expected, string message) => Check(MathF.Abs(actual - expected) < .00001f, message);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Rebellion Blizzard candidates actual Finish seven steps exact interval and cached future", Blizzard);
        run("SimpleHunts Rebellion shared candidate cache overlap and no-sequence Finish invalidation", SharedCache);
        run("SimpleHunts Rebellion Cyclone icon ordering quantized completing actor zero sentinel and effect-only FIFO", Cyclone);
        run("SimpleHunts Rebellion per-owner march history first ordinary AOE landing and no invented control", March);
        run("SimpleHunts Rebellion helper raw replay correction missing-facing ordinary hints prediction and cleanup", Lifecycle);
        run("SimpleHunts Rebellion missing helper Finish and late pose correction preserve cache time and last Start identity", Corrections);
    }

    private static void Blizzard()
    {
        var e = Create();
        e.Start(999, Cast(17628), false);
        var candidates = Areas(e, "candidate");
        Check(candidates.Length == 3 && candidates.All(c => c.Risky) && candidates[0].Color != null
            && candidates.Skip(1).All(c => c.Color == null), "Unknown-direction Blizzard lost risky candidates or persistent color.");
        e.Finish(999, 17628, Start.AddSeconds(2));
        Check(Areas(e, "candidate").Length == 2 && Areas(e, "candidate").All(c => c.Color != null),
            "Blizzard first Finish did not delete exactly the shared cache head or color promoted candidates.");
        e.Start(999, Cast(17629, 3, MathF.PI / 4, .5), false);
        e.UpdateComponents(Start.AddSeconds(10));
        var rotation = Areas(e, "blizzard");
        Check(rotation.Length == 2 && !Areas(e, "candidate").Any() && rotation.All(c => c.Risky)
            && rotation[0].Activation == Start.AddSeconds(11.3d) && rotation[1].Activation == Start.AddSeconds(3.5),
            "Blizzard sequence lost future-first order, full risk or rebuild-time clock.");
        Near(rotation[0].Rotation, MathF.PI / 2, "Blizzard inferred wrong positive increment.");
        e.UpdateComponents(Start.AddSeconds(50));
        Check(Areas(e, "blizzard")[0].Activation == Start.AddSeconds(11.3d), "Clock-only Update slid cached future AOE.");
        e.Effect(999, 17629, Start.AddSeconds(51), 1);
        Check(Areas(e, "blizzard")[1].Stage == 0, "Blizzard incorrectly advanced on an effect.");
        for (var i = 1; i <= 7; ++i)
        {
            e.Finish(999, i % 2 == 0 ? 18080u : 17629u, Start.AddSeconds(52 + i));
            e.UpdateComponents(Start.AddSeconds(52 + i));
            rotation = Areas(e, "blizzard");
            if (i == 7) Check(rotation.Length == 0, "Blizzard retained its eighth sequence stage.");
            else
            {
                Check(rotation.Last().Activation == Start.AddSeconds(52 + i).AddSeconds(1.3d), "Blizzard did not use actual Finish clock.");
                Check(i == 6 ? rotation.Length == 1 && rotation[0].Color == null : rotation.Length == 2,
                    "Blizzard last stage color/count differs.");
            }
        }
        foreach (var (angle, sign) in new[] { (-MathF.PI / 4, -1), (0f, 1), (-MathF.PI, -1), (MathF.PI, 1) })
        {
            e = Create();
            e.Start(999, Cast(17628), false);
            e.Finish(999, 17628, Start.AddSeconds(1));
            e.Start(999, Cast(17629, 2, angle), false);
            e.UpdateComponents(Start.AddSeconds(2));
            Near(Areas(e, "blizzard")[0].Rotation, angle + sign * MathF.PI / 4, "Blizzard normalized sign or ±pi endpoint differs.");
        }
    }

    private static void SharedCache()
    {
        var e = Create();
        e.Start(999, Cast(17628), false);
        e.Finish(999, 17628, Start.AddSeconds(1));
        e.Finish(998, 17629, Start.AddSeconds(2));
        e.UpdateComponents(Start.AddSeconds(2));
        Check(!Areas(e, "candidate").Any(), "No-sequence Advance failed to invalidate the shared candidate cache.");
        e.Start(999, Cast(17629, 3, .3f), false);
        e.UpdateComponents(Start.AddSeconds(3));
        var head = Areas(e, "blizzard")[0].AreaId;
        e.Start(998, Cast(17628, 4), false);
        e.UpdateComponents(Start.AddSeconds(4));
        Check(Areas(e, "blizzard").Length == 2 && Areas(e, "candidate").Length == 3,
            "A same-count Update incorrectly rebuilt away newly appended candidates.");
        e.Finish(998, 17628, Start.AddSeconds(5));
        Check(!e.Areas.Any(c => c.AreaId == head) && Areas(e, "candidate").Length == 3,
            "Candidate Finish deleted from a separate candidate list instead of the shared cache head.");
    }

    private static void Cyclone()
    {
        var e = Create();
        e.Start(999, Cast(18078, rotation: .4f), false);
        e.Finish(999, 18078, Start.AddSeconds(1));
        e.Synchronize([Boss(), Helper(position: new(50, 0, 50)), Helper(998, position: new(10, 0, 20)), Player()],
            _ => true, Start.AddSeconds(2), Party);
        e.Icon(998, 168, Start.AddSeconds(2));
        e.UpdateComponents(Start.AddSeconds(2));
        var queue = Areas(e, "cyclone");
        var expected = new BossMod.WPos(10, 20).Quantized();
        Check(queue.Length == 2 && queue.All(c => c.Risky) && queue.Last().Activation == Cast(18078).FinishAt.AddSeconds(5.2d)
            && queue.All(c => c.Location == new Vector3(expected.X, 0, expected.Z)),
            "Cyclone did not use completing icon actor, quantized origin or F+5.2d.");
        Near(queue[0].Rotation, .4f + MathF.PI / 2, "Cyclone took direction from action name instead of icon.");
        e.Start(999, Cast(18126, 3), false);
        e.Finish(999, 18126, Start.AddSeconds(4));
        Check(Areas(e, "cyclone").Last().Stage == 0, "Cyclone Start/Finish advanced sequence.");
        for (uint i = 1; i <= 6; ++i)
        {
            e.Effect(998, i % 2 == 0 ? 18127u : 18128u, Start.AddSeconds(20 + i), i);
            e.Effect(998, i % 2 == 0 ? 18127u : 18128u, Start.AddSeconds(20.01 + i), i);
            e.UpdateComponents(Start.AddSeconds(20 + i));
            queue = Areas(e, "cyclone");
            if (i < 6) Check(queue.Last().Activation == Start.AddSeconds(20 + i).AddSeconds(1.7d),
                "Cyclone effect duplicate or wrong real-event delay.");
            else Check(queue.Length == 0, "Cyclone failed six-stage FIFO.");
        }
        e = Create();
        e.Icon(998, 167, Start);
        e.Start(999, Cast(18079, rotation: 0), false);
        e.UpdateComponents(Start);
        Check(!Areas(e, "cyclone").Any(), "Cyclone lost source zero-rotation sentinel.");
        e.Start(999, Cast(18079, rotation: .5f), true);
        e.UpdateComponents(Start);
        Near(Areas(e, "cyclone")[0].Rotation, .5f - MathF.PI / 2, "Icon-first Cyclone direction or correction was lost.");
    }

    private static void March()
    {
        var e = Create();
        var status = new ReplayStatus(1958, 0, 1, Start.AddSeconds(100));
        e.MarchStatus(100, 1958, Start, status);
        e.MarchStatus(100, 1958, Start.AddSeconds(.1), status with { ExpiresAt = Start.AddSeconds(101) });
        e.Synchronize([Boss(), Boss(2), Helper(), Helper(998), Player(status with { ExpiresAt = Start.AddSeconds(101) })],
            _ => true, Start.AddSeconds(1), Party);
        Check(e.MarchMovements(Player()).Count() == 3, "New Rebellion owner inherited old owner's two-Gain march history.");
        e.Remove(1);
        var move = e.MarchMovements(Player()).Single();
        Check(move.To == new Vector3(0, 0, 13.2f), "Mindjack did not retain two-second unlimited-window projection.");
        e.Start(999, Cast(17602, 2) with { Location = new(100, 0, 100) }, false);
        e.Start(998, Cast(17602, 2) with { Location = Vector3.Zero }, false);
        Check(!e.Hints(Player()).Contains("Aim for safe spot!"), "Mindjack checked the second Interference caster.");
        e.Finish(999, 17602, Start.AddSeconds(3));
        Check(e.Hints(Player()).Contains("Aim for safe spot!"), "Mindjack failed to promote first Interference caster.");
        Check(e.Facing(Player(), Start) == null && !e.StopEverything(Player(), Party, Start)
            && !e.TargetDangers(Player(), Party).Any(), "Mindjack invented facing, hold or automatic forced-march navigation.");
        e.Remove(2);
        Check(!e.MarchMovements(Player()).Any(), "Last owner left march history.");
    }

    private static void Lifecycle()
    {
        var e = Create();
        e.Start(999, Cast(17628) with { Rotation = null }, true);
        Check(!Areas(e, "candidate").Any(), "Directional packet without facing was silently treated as zero.");
        var raw = Cast(17628);
        e.Synchronize([Boss(), Helper(cast: raw), Player()], _ => true, Start, Party);
        e.Synchronize([Boss(), Boss(2), Helper(cast: raw), Player()], _ => true, Start.AddSeconds(1), Party);
        Check(Areas(e, "candidate").Length == 6 && e.PredictedDamage(Party).Count() == 1,
            "Raw helper replay duplicated old owner or failed new owner/prediction.");
        e.Start(999, raw with { Location = new(20, 0, 30), Rotation = .3f }, true);
        Check(Areas(e, "candidate").Length == 6 && Areas(e, "candidate").All(c => c.Location == new Vector3(20, 0, 30)),
            "Same-order packet correction added candidates or lost per-owner delivered baseline.");
        e.Remove(999, Start.AddSeconds(2));
        Check(Areas(e, "candidate").Length == 4 && !e.PredictedDamage(Party).Any(), "Destroyed raw caster did not Finish each owner once.");
        e.Reset();
        Check(!e.Areas.Any() && !e.Casts.Any() && !e.MarchMovements(Player()).Any(), "Reset retained Rebellion outputs.");
        e.Synchronize([Boss(), Helper(), Player()], _ => true, Start, Party);
        var ordinaryIndex = 0;
        foreach (var action in new uint[] { 17600, 17601, 17602, 17598 })
        {
            e.Start(999, Cast(action, ++ordinaryIndex), false);
            var shape = e.Areas.Single(c => c.Shape != null).Shape;
            Check(action switch
            {
                17600 => shape is CircleTemplate { Radius: 10 },
                17601 => shape is DonutTemplate { InnerRadius: 5, OuterRadius: 40 },
                17602 => shape is FanTemplate { Radius: 28 } cone && cone.AngleRadians == MathF.PI,
                _ => shape is FanTemplate { Radius: 40 } cone && cone.AngleRadians == MathF.PI / 4
            }, $"Rebellion ordinary {action} geometry parameters differ from the source: {shape}.");
        }
        e.Start(999, Cast(17597, 20000), false);
        Check(!e.Areas.Any(c => c.Shape != null) && e.PredictedDamage(Party).Single().DamageType == "Raidwide",
            "RoyalDecree fabricated a danger circle or lost prediction.");
    }

    private static void Corrections()
    {
        var e = Create();
        var raw = Cast(17628);
        e.Synchronize([Boss(), Helper(cast: raw), Player()], _ => true, Start, Party);
        e.Synchronize([Boss(), Player()], _ => true, Start.AddSeconds(1), Party);
        Check(Areas(e, "candidate").Length == 2, "Missing helper snapshot skipped Blizzard Finish.");
        e.Reset();
        var status = new ReplayStatus(1958, 0, 1, Start.AddSeconds(100));
        e.Synchronize([Boss(), Helper(cast: Cast(17602) with { Location = Vector3.Zero }), Player(status)],
            _ => true, Start, Party);
        Check(e.Hints(Player()).Contains("Aim for safe spot!"), "Missing-source test did not initialize ordinary first-caster hint.");
        e.Synchronize([Boss(), Player(status)], _ => true, Start.AddSeconds(1), Party);
        Check(!e.Hints(Player()).Contains("Aim for safe spot!"), "Missing helper left an ordinary first-caster landing hazard.");
        foreach (var explicitRemove in new[] { false, true })
        {
            e = Create();
            e.MarchStatus(100, 1958, Start, status);
            e.Start(999, Cast(17602) with { Location = Vector3.Zero }, true);
            Check(e.Hints(Player()).Contains("Aim for safe spot!"), "Packet-only test did not initialize landing hazard.");
            if (explicitRemove) e.Remove(999, Start.AddSeconds(1));
            else e.Synchronize([Boss(), Player(status)], _ => true, Start.AddSeconds(1), Party);
            Check(!e.Hints(Player()).Contains("Aim for safe spot!") && !e.Areas.Any(c => c.Shape != null),
                "Packet-only caster destruction kept its ordinary drawing or Mindjack landing hazard.");
        }

        e = Create();
        var second = Cast(17629, rotation: .2f);
        e.Start(999, second, false);
        e.UpdateComponents(Start);
        var future = Areas(e, "blizzard")[0].Activation;
        e.UpdateComponents(Start.AddSeconds(10));
        e.Start(999, second with { Location = new(20, 0, 30), Rotation = -.2f }, true);
        e.UpdateComponents(Start.AddSeconds(10));
        var queue = Areas(e, "blizzard");
        Check(queue.Length == 2 && queue[0].Activation == future && queue[1].Activation == second.FinishAt
            && queue.All(c => c.Location == new Vector3(20, 0, 30)),
            "Late pose-only correction slid the cached future clock or lost corrected geometry.");
        Near(queue[0].Rotation, -.2f - MathF.PI / 4, "Corrected second-cast pose did not correct inferred increment.");
        e = Create();
        var first = Cast(17628);
        e.Start(999, first, false);
        e.Start(998, first with { Rotation = 1 }, false);
        e.Start(999, first with { Rotation = .2f }, true);
        e.Start(1, Cast(17629, 1, .5f), false);
        e.UpdateComponents(Start.AddSeconds(1));
        Near(Areas(e, "blizzard")[0].Rotation, .5f - MathF.PI / 4,
            "An older first-cast correction replaced the last Start's rotation identity.");
    }
}
