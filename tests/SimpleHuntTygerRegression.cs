using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;

internal static class SimpleHuntTygerRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static ReplayCast Cast(uint action, double at = 0) => new(action, 100, new(10, 0, 20), 0,
        Start.AddSeconds(at), Start.AddSeconds(at + 4.3), false, false);
    private static ReplayActorState Boss(ReplayCast? cast = null, ulong id = 1) => new()
    {
        Id = id, Oid = 0x288E, Hp = 1000, Targetable = true, InCombat = true, Position = new(10, 0, 20), Cast = cast
    };
    private static HuntPendingCast[] Tails(SimpleHuntEncounter encounter) => encounter.Areas.Where(a => a.AreaId.EndsWith("/scorpions-sting")).ToArray();

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Tyger both voices tail persists after finish and follows exact source drawing navigation split", Tail);
        run("SimpleHunts Tyger corrections raw restart effect dedup source instances and lifecycle", Events);
        run("SimpleHunts Tyger raw restart resets followed pose and cleared tails reject corrections until actual restart", Restart);
    }

    private static void Tail()
    {
        foreach (var action in new uint[] { 16962, 16963 })
        {
            var encounter = new SimpleHuntEncounter();
            var cast = Cast(action);
            var boss = Boss(cast) with { Position = new(13, 0, 25) };
            encounter.Synchronize([boss], _ => true, Start);
            var tail = Tails(encounter).Single();
            Check(tail.Activation == cast.FinishAt.AddSeconds(2.3d) && tail.NavigationRotation == null
                && tail.Location == cast.Location,
                "Initial Tyger prediction did not use double 2.3 seconds and rear distance.");
            var rear = tail.Shape!.Resolve(tail.Location, tail.Rotation, null);
            Check(rear.Contains(tail.Location - Vector3.UnitZ * 17.9f, 0)
                && !rear.Contains(tail.Location + Vector3.UnitX * 17.9f, 0), "Tyger tail cone dimensions differ.");
            encounter.Finish(1, action, Start.AddSeconds(1));
            Check(Tails(encounter).Length == 1, "Voice cancellation consumed the tail prediction.");
            encounter.Synchronize([boss with { Cast = null }], _ => true, Start.AddSeconds(30));
            Check(Tails(encounter).Single() == tail, "Predicted time or unchanged actor changed Tyger's locked initial tail.");
            encounter.Synchronize([boss with { Cast = null, Position = new(13, 1, 25) }], _ => true, Start.AddSeconds(30.5));
            Check(Tails(encounter).Single() == tail, "A height-only change quantized Tyger's initial tail or flipped navigation.");
            var moved = boss with { Cast = null, Position = new(30, 0, 40), Rotation = MathF.PI / 2 };
            encounter.Synchronize([moved], _ => true, Start.AddSeconds(31));
            tail = Tails(encounter).Single();
            const float grid = 2000f / 65535f;
            const float inverse = 1f / grid;
            var quantized = new Vector3(((int)MathF.Round(moved.Position.X * inverse) - .5f) * grid, moved.Position.Y,
                ((int)MathF.Round(moved.Position.Z * inverse) - .5f) * grid);
            Check(tail.Location == quantized && tail.NavigationRotation == moved.Rotation,
                "Tyger moved tail lost quantized origin or source's forward distance rotation.");
            var drawing = tail.Shape!.Resolve(tail.Location, tail.Rotation, null);
            var navigation = tail.Shape.Resolve(tail.Location, tail.NavigationRotation!.Value, null);
            Check(drawing.Contains(tail.Location - Vector3.UnitX * 10, 0) && !drawing.Contains(tail.Location + Vector3.UnitX * 10, 0)
                && navigation.Contains(tail.Location + Vector3.UnitX * 10, 0) && !navigation.Contains(tail.Location - Vector3.UnitX * 10, 0),
                "Tyger rear outline and explicitly forward AI distance were merged.");
            var player = new ReplayActorState { Id = 100, Position = tail.Location - Vector3.UnitX * 10 };
            Check(encounter.Hints(player).Contains("GTFO from aoe!"), "Tyger source text warning stopped using the rear outline.");
            encounter.Effect(1, 16961, Start.AddSeconds(32), 1);
            encounter.Effect(1, 16961, Start.AddSeconds(32), 1);
            Check(!Tails(encounter).Any(), "Tyger actual tail effect did not clear prediction.");
        }
    }

    private static void Events()
    {
        var encounter = new SimpleHuntEncounter();
        var cast = Cast(16963);
        var boss = Boss(cast);
        encounter.Synchronize([boss, boss with { Id = 2 }], _ => true, Start);
        encounter.Start(1, cast, true);
        var packet = cast with { Location = new(20, 0, 30), Rotation = 1, FinishAt = cast.FinishAt.AddMilliseconds(1) };
        encounter.Start(1, packet, true);
        var tail = Tails(encounter).Single(a => a.Source == 1);
        Check(tail.Location == packet.Location && tail.Rotation == packet.Rotation + MathF.PI
            && tail.Activation == packet.FinishAt.AddSeconds(2.3d), "Tyger same-generation correction did not update initial tail.");
        encounter.Synchronize([boss with { Cast = packet, Rotation = 1 }, boss with { Id = 2 }], _ => true, Start.AddSeconds(1));
        Check(Tails(encounter).Count(a => a.Source == 1) == 1, "A true raw voice restart appended instead of replacing its single prediction.");
        encounter.Effect(1, 16961, Start.AddSeconds(2), 1);
        encounter.Synchronize([boss with { Cast = packet, Rotation = 1 }, boss with { Id = 2 }], _ => true, Start.AddSeconds(3));
        Check(Tails(encounter).Length == 1 && Tails(encounter).Single().Source == 2, "A stale observation revived cleared tail or effect crossed source instances.");
        encounter.Synchronize([boss with { Id = 2, Hp = 0 }], _ => true, Start.AddSeconds(4));
        Check(!encounter.Areas.Any(), "Tyger HP0 retained its tail.");
        encounter.Reset();
        encounter.Synchronize([Boss(Cast(16962) with { Rotation = null })], _ => true, Start);
        Check(!encounter.Casts.Any() && !Tails(encounter).Any(), "A circle Voice guessed the required later cone facing.");
        foreach (var action in new uint[] { 16957, 16958, 16959 })
        {
            encounter.Reset();
            encounter.Synchronize([Boss(Cast(action))], _ => true, Start);
            encounter.Finish(1, 999999, Start.AddSeconds(1));
            Check(!encounter.Areas.Any(a => a.Shape != null), "Tyger grouped breath rejected another actual cast finish.");
        }
        encounter.Reset();
        encounter.Synchronize([Boss(Cast(16963)), new ReplayActorState { Id = 100 }], _ => true, Start);
        Check(encounter.Hints(null).Contains("Interrupt! (Donut Raidwide)")
            && !encounter.PredictedDamage([new(0, "Player", 100)]).Any(), "Tyger interrupt hint invented a raidwide damage prediction.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Restart()
    {
        var encounter = new SimpleHuntEncounter();
        var cast = Cast(16962);
        var boss = Boss(cast);
        encounter.Synchronize([boss], _ => true, Start);
        var moved = boss with { Position = new(30, 0, 40), Rotation = .3f };
        encounter.Synchronize([moved], _ => true, Start.AddSeconds(1));
        var followed = Tails(encounter).Single();
        Check(followed.NavigationRotation == .3f, "Fixture did not enter Tyger's followed distance state.");
        var raw = cast with { Location = moved.Position, Rotation = .3f };
        encounter.Synchronize([moved with { Cast = raw }], _ => true, Start.AddSeconds(2));
        var restarted = Tails(encounter).Single();
        Check(restarted.Order != followed.Order && restarted.NavigationRotation == null
            && restarted.Location == raw.Location && restarted.Rotation == .3f + MathF.PI,
            "A true Tyger raw restart did not replace the followed generation with an initial rear prediction.");
        encounter.Effect(1, 16961, Start.AddSeconds(3), 1);
        var corrected = raw with { Location = new(50, 0, 60) };
        encounter.Start(1, corrected, true);
        Check(!Tails(encounter).Any(), "A packet correction recreated an effect-consumed tail.");
        encounter.Synchronize([moved with { Cast = corrected }], _ => true, Start.AddSeconds(4));
        var next = Tails(encounter).Single();
        Check(next.Order != restarted.Order && next.Location == corrected.Location && next.NavigationRotation == null,
            "A new actual raw change did not recreate the cleared tail with its own initial pose.");
    }
}
