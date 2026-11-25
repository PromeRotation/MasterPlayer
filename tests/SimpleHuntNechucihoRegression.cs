using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntNechucihoRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly ReplayPartyMember[] Party = [new(0, "P", 100)];
    private static ReplayActorState Boss(ulong id = 1) => new()
        { Id = id, Oid = 0x452C, Kind = 2, Hp = 1000, InCombat = true, Targetable = true };
    private static ReplayActorState Player() => new() { Id = 100, Hp = 1000 };
    private static ReplayActorState Helper(ReplayCast? cast = null) => new()
        { Id = 999, Oid = 0x233C, Kind = 2, Cast = cast };
    private static ReplayCast Cast(uint action, double at = 0, float rotation = .2f)
        => new(action, 100, new(10, 0, 20), rotation, Start.AddSeconds(at), Start.AddSeconds(at + 3.3), false, false);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static SimpleHuntEncounter Create()
    {
        var e = new SimpleHuntEncounter();
        e.Synchronize([Boss(), Helper(), Player()], _ => true, Start, Party);
        return e;
    }
    private static HuntPendingCast[] Queue(SimpleHuntEncounter e)
        => e.Areas.Where(a => a.SequenceId?.Contains("/nechuciho/") == true).ToArray();

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Nechuciho three recursive whispers exact float deadlines all twelve effects and first two views", Whispers);
        run("SimpleHunts Nechuciho opposite risk promotion persistent clearing and packet correction", Risk);
        run("SimpleHunts Nechuciho overlapping global indexes raw generations and effect-only lifetime", Overlap);
        run("SimpleHunts Nechuciho helper broadcast current owner replay same frame finish ordinary hints and cleanup", Lifecycle);
    }

    private static void Whispers()
    {
        (uint Action, float[] Offsets)[] cases =
        [
            (39493, [0, -MathF.PI / 2, MathF.PI / 2, MathF.PI]),
            (39494, [MathF.PI / 2, 0, MathF.PI, -MathF.PI / 2]),
            (39495, [MathF.PI, MathF.PI / 2, -MathF.PI / 2, 0])
        ];
        uint[] effects = [39785, 39786, 39787, 39788, 42164, 42165, 42166, 42167, 42168, 42169, 42170, 42171];
        foreach (var (action, offsets) in cases)
        foreach (var effect in effects)
        {
            var e = Create();
            var cast = Cast(action);
            e.Start(999, cast, false);
            var angle = cast.Rotation!.Value;
            for (var i = 0; i < 4; ++i)
            {
                angle += offsets[i];
                var head = Queue(e)[0];
                Check(head.Stage == i && head.Location == cast.Location && head.Rotation == angle
                    && head.Activation == cast.FinishAt.AddSeconds(9.3f + 2f * i)
                    && head.Shape is FanTemplate { Radius: 30, AngleRadians: MathF.PI },
                    "Whisper changed its recursive direction, locked origin, geometry or exact float deadline.");
                Check(Queue(e).Length == Math.Min(2, 4 - i) && head.Risky, "Whisper exposed more than the first two or failed to promote head.");
                e.Effect(1, effect, Start.AddSeconds(10 + i), (uint)(i + 1));
                e.Effect(1, effect, Start.AddSeconds(10.01 + i), (uint)(i + 1));
            }
            Check(Queue(e).Length == 0, "One of twelve effect AIDs failed FIFO/deduplication.");
        }
    }

    private static void Risk()
    {
        var e = Create();
        var cast = Cast(39493);
        e.Start(999, cast, false);
        Check(Queue(e).All(a => a.Risky), "Nonopposite second Whisper was suppressed.");
        e.Effect(1, 39785, Start.AddSeconds(1), 1);
        e.Effect(1, 39785, Start.AddSeconds(2), 2);
        var visible = Queue(e);
        Check(visible[0].Risky && !visible[1].Risky, "Opposite second Whisper should be visible without navigation.");
        e.Start(999, cast with { Rotation = 1, FinishAt = Start.AddSeconds(30) }, true);
        Check(Queue(e).Length == 2 && Queue(e)[0].Stage == 2 && !Queue(e)[1].Risky,
            "Packet correction restored consumed stages or lost persistent risk clearing.");
        e.Effect(1, 42171, Start.AddSeconds(3), 3);
        Check(Queue(e).Single().Risky, "Promoted opposite tail was not restored to risky.");
    }

    private static void Overlap()
    {
        var e = Create();
        e.Start(999, Cast(39493, rotation: 0), false);
        var second = Cast(39494, 1, MathF.PI / 2);
        e.Start(1, second, false);
        var expected = new[] { MathF.PI, 0, MathF.PI / 2, -MathF.PI / 2 };
        for (var i = 0; i < 4; ++i) e.Effect(1, 39785, Start.AddSeconds(5 + i), (uint)(i + 1));
        var corrected = second with { Rotation = MathF.PI, Location = new(30, 0, 40), FinishAt = Start.AddSeconds(25) };
        e.Start(1, corrected, true);
        for (var i = 0; i < 4; ++i)
        {
            var head = Queue(e)[0];
            Check(MathF.Abs(head.Rotation - expected[i] - (i == 0 ? MathF.PI / 2 : 0)) < .000001f
                && head.Location == corrected.Location && head.Activation == corrected.FinishAt.AddSeconds(9.3f + 2f * i),
                "Overlapping Whisper used a local chain or corrected a dependency belonging to an older generation.");
            e.Effect(999, 42164, Start.AddSeconds(10 + i), (uint)(10 + i));
        }
        e.Start(1, Cast(39493, 20), false);
        e.Finish(1, 39493, Start.AddSeconds(21));
        e.Start(1, Cast(39496, 22), false);
        e.Effect(999, 39872, Start.AddSeconds(23), 20);
        e.Remove(999, Start.AddSeconds(24));
        e.Synchronize([Boss(), Player()], _ => true, Start.AddSeconds(200), Party);
        Check(Queue(e).Length == 2 && Queue(e)[0].Stage == 0, "Visual/Finish/caster removal/clock expiry consumed Whisper history.");
        e.Reset();
        e.Synchronize([Boss(), Helper(), Player()], _ => true, Start, Party);
        e.Start(999, Cast(39496), false);
        e.Effect(999, 39785, Start, 1);
        Check(Queue(e).Length == 0, "Missing history was invented from visual/effect.");
    }

    private static void Lifecycle()
    {
        var e = Create();
        var helper = Helper(Cast(39493));
        e.Synchronize([Boss(), helper, Player()], _ => true, Start, Party);
        e.Synchronize([Boss(), Boss(2), helper, Player()], _ => true, Start.AddSeconds(1), Party);
        Check(Queue(e).Length == 4, "New owner did not replay current helper cast.");
        e.Remove(1);
        Check(Queue(e).Length == 2 && Queue(e).All(a => a.Source == 2), "Removing one owner lost another's queue.");
        e.Synchronize([Boss(2), Boss(3), Helper(), Player()], _ => true, Start.AddSeconds(2), Party);
        Check(Queue(e).All(a => a.Source == 2), "New owner replayed a helper cast finished in this frame.");
        e.Remove(2);
        e.Start(999, Cast(39492, 3), false);
        Check(e.Hints(Player()).Contains("Interrupt! (Applies Doom!)") && !e.PredictedDamage(Party).Any()
            && !e.Areas.Any(a => a.Shape != null), "Doom hint became damage or an invented R30 circle.");
        e.Finish(999, 39492, Start.AddSeconds(4));
        e.Start(999, Cast(39491, 5), false);
        Check(e.PredictedDamage(Party).Single().DamageType == "Raidwide", "SentinelRoar lost raidwide prediction.");
        e.Remove(3);
        Check(!e.Casts.Any() && !e.Areas.Any() && !e.PredictedDamage(Party).Any(), "Final owner removal retained helper output.");
        e.Reset();
        Check(!e.Active && !Queue(e).Any(), "Reset retained Nechuciho state.");
    }
}
