using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntHeshualaRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static ReplayActorState Boss(ReplayCast? cast = null, ReplayStatus[]? statuses = null, ulong id = 1)
        => new() { Id = id, Oid = 0x416E, Kind = 2, Hp = 1000, InCombat = true, Targetable = true,
            Position = new(10, 0, 20), Cast = cast, Statuses = statuses ?? [] };
    private static ReplayCast Cast(uint action, float rotation = .2f, double at = 0)
        => new(action, 100, new(10, 0, 20), rotation, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), false, false);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static HuntPendingCast[] Cones(SimpleHuntEncounter e) => e.Areas.Where(a => a.Shape is FanTemplate).ToArray();
    private static HuntPendingCast? Cross(SimpleHuntEncounter e) => e.Areas.FirstOrDefault(a => a.Shape is LocalPolygonTemplate);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Heshuala counts all source rules double clocks actual Finish matching and final color", Spins);
        run("SimpleHunts Heshuala frozen cross status choice hidden initialization and exact geometry", FrozenCross);
        run("SimpleHunts Heshuala charge fallback source identity no guessed spins and unmatched decrement", Fallback);
        run("SimpleHunts Heshuala helper broadcast raw restart correction cancellation and lifecycle", Lifecycle);
    }

    private static SimpleHuntEncounter Create(ReplayActorState? boss = null)
    {
        var e = new SimpleHuntEncounter();
        e.Synchronize([boss ?? Boss()], _ => true, Start);
        return e;
    }

    private static void Spins()
    {
        foreach (var (power, count) in new (uint, int)[] { (39095, 3), (39096, 4), (39097, 5), (39098, 6) })
        foreach (var firstAction in new uint[] { 39099, 39100 })
        {
            var e = Create();
            e.Start(1, Cast(power), false);
            e.Start(1, Cast(firstAction, at: 1), false, observedAt: Start.AddSeconds(1));
            e.UpdateComponents(Start.AddSeconds(1));
            var cones = Cones(e);
            var increment = firstAction == 39100 ? MathF.PI / 2 : -MathF.PI / 2;
            Check(cones.Length == 2 && cones.All(a => a.Risky) && cones[0].Rotation == .2f + increment
                && cones[1].Rotation == .2f && cones[1].Color == new Vector4(1, 1, 0, 1)
                && cones[0].Activation == Cast(firstAction, at: 1).FinishAt.AddSeconds(2.7d),
                "Heshuala spin count, preview order, risk, direction or double interval differs.");
            e.Effect(1, firstAction, Start.AddSeconds(7), 1);
            Check(Cones(e)[1].Rotation == .2f, "An effect advanced the Finish-only sequence.");
            var at = Start.AddSeconds(9);
            var rotation = .2f;
            for (var i = 0; i < count; ++i)
            {
                if (i != 0) e.Start(1, Cast(39101, rotation, 10 + i * 3), false);
                e.Finish(1, i == 0 ? firstAction : 39101, at.AddSeconds(i * 6));
                e.UpdateComponents(at.AddSeconds(i * 6));
                var remaining = Cones(e);
                if (i == count - 1) Check(remaining.Length == 0, "Heshuala final Finish retained a rotation.");
                else
                {
                    Check(remaining.Last().Activation == at.AddSeconds(i * 6).AddSeconds(2.7d),
                        "Heshuala next activation did not use actual Finish time.");
                    if (i == count - 2) Check(remaining.Length == 1 && remaining[0].Color == null,
                        "Heshuala final cone retained imminent danger color.");
                }
                rotation += increment;
            }
        }
        var ordinary = Create();
        ordinary.Start(1, Cast(39104), false);
        Check(ordinary.Areas.Single().Shape is CircleTemplate { Radius: 5 }, "LightningBolt geometry differs.");
        ordinary.Finish(1, 39104, Start.AddSeconds(1));
        ordinary.Start(1, Cast(39105, at: 2), false);
        Check(ordinary.Hints(new() { Id = 100 }).Contains("Raidwide")
            && !ordinary.Areas.Any(a => a.Shape != null), "ElectricalOverload created an ordinary danger circle.");
    }

    private static void FrozenCross()
    {
        foreach (var cardinal in new[] { false, true })
        {
            var e = Create();
            if (cardinal) e.HeshualaStatus(1, 3977, new(3977, 0, 1, Start.AddSeconds(30)), Start);
            e.Start(1, Cast(39095), false);
            e.Start(1, Cast(39100, at: 1), false);
            e.UpdateComponents(Start.AddSeconds(2));
            Check(Cross(e) == null, "Cross became visible before Spins fell below three.");
            var corrected = Cast(39100, .5f, 1) with { Location = new(14, 0, 20) };
            e.Start(1, corrected, true);
            e.UpdateComponents(Start.AddSeconds(2.5));
            e.HeshualaStatus(1, cardinal ? 3978u : 3977u, new(cardinal ? 3978u : 3977u, 0, 1, Start.AddSeconds(30)), Start.AddSeconds(3));
            e.Finish(1, 39100, Start.AddSeconds(8));
            e.UpdateComponents(Start.AddSeconds(8));
            var cross = Cross(e)!;
            Check(cross != null && cross.Location == new Vector3(10, 0, 20)
                && cross.Rotation == .2f + (cardinal ? 0 : MathF.PI / 4)
                && cross.Activation == Start.AddSeconds(2).AddSeconds(17.5d),
                "Cross did not freeze the original status shape, origin, angle and Update clock.");
            var geometry = cross.Shape!.Resolve(Vector3.Zero, 0, null);
            Check(geometry.Contains(new(49, 0, 4), 0) && geometry.Contains(new(4, 0, -49), 0)
                && !geometry.Contains(new(6, 0, 6), 0), "Cross is not half-arm50 half-width5.");
            e.Finish(1, 39103, Start.AddSeconds(9));
            e.UpdateComponents(Start.AddSeconds(10));
            Check(Cross(e)!.Activation == Start.AddSeconds(10).AddSeconds(15.5d),
                "Cross Finish failed to reset initialization while a rotation remains.");
        }
    }

    private static void Fallback()
    {
        var charge = new ReplayStatus(3979, 5, 1, Start.AddSeconds(30));
        var e = Create(Boss(statuses: [charge]));
        e.Start(1, Cast(39100), false);
        e.UpdateComponents(Start);
        Check(Cones(e).Length == 2 && Cross(e) == null, "Primary electrical charge fallback was lost.");
        // A geometric mismatch does not advance the sequence, but always decrements the independent Spins field.
        e.Synchronize([Boss(Cast(39100), [charge]) with { Position = new(12, 0, 20) }], _ => true, Start.AddSeconds(1));
        e.Finish(1, 39100, Start.AddSeconds(2));
        e.UpdateComponents(Start.AddSeconds(2));
        Check(Cones(e).Last().Rotation == .2f && Cross(e) == null, "Mismatched Finish advanced a sequence.");
        e.Finish(1, 39101, Start.AddSeconds(3));
        e.Finish(1, 39101, Start.AddSeconds(4));
        e.UpdateComponents(Start.AddSeconds(4));
        Check(Cross(e) != null && Cones(e).Last().Rotation == .2f, "Unmatched Finish did not decrement Spins independently.");
        var noHistory = Create();
        noHistory.Start(1, Cast(39100), false);
        noHistory.UpdateComponents(Start);
        Check(Cones(noHistory).Length == 0 && Cross(noHistory) != null, "Missing spin history guessed a default count.");
    }

    private static void Lifecycle()
    {
        var helper = new ReplayActorState { Id = 999, Oid = 0x233C, Kind = 2, Position = new(10, 0, 20) };
        var e = Create();
        e.Synchronize([Boss(), helper], _ => true, Start);
        e.Start(999, Cast(39096), false);
        var cast = Cast(39100, at: 1);
        e.Start(999, cast, false);
        e.UpdateComponents(Start.AddSeconds(1));
        e.Start(999, cast with { FinishAt = Start.AddSeconds(8) }, true);
        e.UpdateComponents(Start.AddSeconds(2));
        Check(Cones(e).Length == 2 && Cones(e).Last().Activation == Start.AddSeconds(8),
            "Packet correction appended a phantom Heshuala sequence.");
        e.Finish(999, 39100, Start.AddSeconds(3));
        e.UpdateComponents(Start.AddSeconds(3));
        Check(Cones(e).Last().Rotation == .2f + MathF.PI / 2,
            "A real cancelled Finish failed to advance Heshuala.");
        e.Synchronize([Boss() with { Dead = true }, helper], _ => true, Start.AddSeconds(4));
        Check(!e.Active && !e.Areas.Any(a => a.Shape != null) && !e.Casts.Any(), "Heshuala owner death retained helper output.");
        e.Reset();
        Check(!e.Areas.Any(), "Heshuala Reset retained history.");
        foreach (var snapshotRemoval in new[] { false, true })
        {
            e.Synchronize([Boss(), helper], _ => true, Start);
            e.Start(999, Cast(39095), true);
            e.Start(999, Cast(39100, at: 1), true);
            e.UpdateComponents(Start.AddSeconds(1));
            Check(Cones(e).Length == 2, "Packet-only helper did not publish its early prediction.");
            if (snapshotRemoval) e.Synchronize([Boss()], _ => true, Start.AddSeconds(2));
            else e.Remove(999, Start.AddSeconds(2));
            e.UpdateComponents(Start.AddSeconds(20));
            Check(Cones(e).Length == 0 && Cross(e) == null,
                "Packet-only caster destruction retained an unconfirmed rotation or derived cross.");
            e.Reset();
        }
    }
}
