using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class HeavenOnHighKenkoRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-300, 0, -300);

    internal static void Run(Action<string, Action> run)
    {
        run("HoH70 dual pools independent captured icon default clock ululation and native effects", Pools);
        run("HoH70 puddle first actor state only created origin freeze and owner reset", PuddleOrder);
        run("HoH70 independent charge Finish and devour effect source target references dead destroyed", Baits);
        run("HoH70 charge whole component target gate prediction first clock and finite party cones", Navigation);
        run("HoH70 first raw replaces packet prediction subsequent raw starts independent devour", Raw);
        run("HoH70 spread R3 display R4 navigation no predicted damage solo", Spread);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(774);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x23EB, Center, InCombat: true),
            new(2, 0x233C, Center),
            new(100, 0, Center + Vector3.UnitZ * 15, PartySlot: 0, Player: true),
            new(101, 0, Center + Vector3.UnitX * 20, PartySlot: 1, Player: true),
        ];
        internal DateTime Now = Start;
        internal ulong Viewer = 100;
        internal Fixture() => Sync();
        internal void Sync() => E.Synchronize(Actors, Actors.First(a => a.Id == Viewer).Position, Now, null, Viewer);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = change(Actors[i]);
        }
        internal void Cast(uint action, ulong source = 2, ulong target = 100)
        {
            Now = Now.AddSeconds(.01);
            Set(source, a => a with { Cast = new(action, Center, 0, Now, Now.AddSeconds(5), target) });
            Sync();
        }
        internal void End(ulong source = 2)
        {
            Now = Now.AddSeconds(.01);
            Set(source, a => a with { Cast = null });
            Sync();
        }
        internal DungeonAmbientAoe[] Aoes(string fragment) => E.AmbientAoes().Where(a => a.Id.Contains(fragment)).ToArray();
        internal void Effect(uint action, ulong target = 100, uint sequence = 0)
            => E.Effect(2, action, sequence, Now, target);
    }

    private static void Pools()
    {
        var f = new Fixture();
        f.E.Icon(100, 1, actorId: 101);
        f.Effect(12207);
        Check(f.E.KenkoTargetSlot == 0 && f.E.KenkoTargetPool == null
            && f.E.KenkoNormalPool!.Activation == f.Now.AddSeconds(7.2d)
            && f.Aoes("kenko/pool").Length == 0, "An early icon retroactively captured a future pool.");
        f.E.Icon(100, 1, actorId: 101);
        var captured = f.E.KenkoTargetPool!;
        Check(captured.Activation == default && f.Aoes("kenko/pool").Single().Safe, "Inverse pool default clock/safe goal.");
        Check(f.E.KenkoPoolHint(f.Actors.First(a => a.Id == 100).Position) is { Text: "Go inside puddle!", IsRisk: false }
            && f.E.Hints(f.Actors.First(a => a.Id == 100).Position).Contains("Go inside puddle!"), "Safe-state target hint lost.");
        f.Set(100, a => a with { Position = Center - Vector3.UnitZ * 10 });
        f.Sync();
        f.Effect(12207);
        Check(f.E.KenkoTargetPool == captured && f.E.KenkoNormalPool!.Origin != captured.Origin,
            "Normal replacement moved the captured inverse pool.");
        f.Cast(12208);
        Check(f.E.KenkoUlulation && !f.Aoes("kenko/pool").Single().Safe
            && f.E.Hints(Center - Vector3.UnitZ * 10).Contains("GTFO from AOE!"), "Ululation did not switch to normal pool.");
        f.Now = f.Now.AddSeconds(20);
        f.Sync();
        Check(f.E.KenkoUlulation, "Deadline expired Ululation.");
        f.End();
        Check(!f.E.KenkoUlulation && f.Aoes("kenko/pool").Single().Position == captured.Origin,
            "Real Finish lost captured inverse pool.");
        f.Effect(12204);
        Check(f.E.KenkoTargetSlot == -1 && f.E.KenkoTargetPool == null && f.E.KenkoNormalPool != null
            && !f.Aoes("kenko/pool").Single().Safe, "Devour cleared the normal pool.");
    }

    private static void PuddleOrder()
    {
        var f = new Fixture();
        f.Actors.Add(new(3, 0x1E9829, Center + Vector3.UnitX, EventState: 1));
        f.Actors.Add(new(4, 0x1E9829, Center + Vector3.UnitZ, EventState: 7));
        f.Sync();
        Check(f.E.KenkoNormalPool is { Activation: var clock } && clock == default
            && f.E.KenkoNormalPool.Origin == Center + Vector3.UnitZ, "Any State7 cleared pool despite first actor state1.");
        f.E.Icon(100, 1);
        var inverse = f.E.KenkoTargetPool;
        f.Set(3, a => a with { Position = Center + Vector3.UnitX * 9, Dead = true });
        f.Set(4, a => a with { Position = Center + Vector3.UnitZ * 9 });
        f.Sync();
        Check(f.E.KenkoNormalPool!.Origin == Center + Vector3.UnitZ, "Creation pool followed or filtered death.");
        f.Set(3, a => a with { EventState = 7 });
        f.Sync();
        Check(f.E.KenkoNormalPool == null && f.E.KenkoTargetPool == inverse
            && !f.E.Hints(Center).Contains("Go inside puddle!"), "First State7 cleared independent inverse or emitted normal-only text.");
        f.Actors.RemoveAll(a => a.Id is 3 or 4);
        f.Sync();
        f.Effect(12207);
        f.Sync();
        Check(f.E.KenkoNormalPool != null, "No puddle entity incorrectly cleared prediction.");
        f.E.ResetFight();
        Check(f.E.KenkoNormalPool == null && f.E.KenkoTargetPool == null, "Owner reset retained pools.");
    }

    private static void Baits()
    {
        var f = new Fixture();
        f.Cast(12206);
        var original = f.E.KenkoDevours.Single();
        Check(f.E.KenkoCharges.Single().Activation == original.Cast.FinishAt
            && original.Activation == original.Cast.FinishAt.AddSeconds((double)3.1f)
            && f.Aoes("kenko/devour/").Single(a => a.Display).OutlineOnly, "Independent clocks/own outline.");
        f.Set(100, a => a with { Position = Center + Vector3.UnitX * 12, Dead = true });
        f.Sync();
        var charge = f.Aoes("kenko/charge/").Single(a => a.Display);
        Check(charge.Shape is RayRectangleTemplate rectangle && rectangle.Length == 12
            && MathF.Abs(charge.Rotation - MathF.PI / 2) < .001f, "Dynamic charge target/dead target lost.");
        f.End();
        Check(f.E.KenkoCharges.Count == 0 && f.E.KenkoDevours.Single() == original, "Finish cleared Devour.");
        f.Actors.RemoveAll(a => a.Id == 100);
        f.Viewer = 101;
        f.Sync();
        f.Actors.Add(new(100, 0, Center - Vector3.UnitZ * 20, PartySlot: 0, Player: true));
        f.Sync();
        Check(ReferenceEquals(f.E.KenkoDevours.Single().Target, original.Target)
            && original.Target.Actor.Position == Center + Vector3.UnitX * 12, "Destroyed target rebound to same ID.");
        f.Set(2, a => a with { Dead = true });
        f.Sync();
        Check(f.Aoes("kenko/devour/").Length == 0 && f.E.KenkoDevours.Count == 1, "Source death deleted instead of hiding.");
        f.Set(2, a => a with { Dead = false });
        f.Sync();
        f.Actors.RemoveAll(a => a.Id == 2);
        f.Sync();
        Check(f.Aoes("kenko/devour/").Any(a => a.Display), "Destroyed living source incorrectly hid Devour.");
        f.Effect(12204);
        Check(f.E.KenkoDevours.Count == 0, "Native Devour did not clear all old references.");
    }

    private static void Navigation()
    {
        var f = new Fixture();
        f.Cast(12206, target: 101);
        Check(f.Aoes("kenko/charge/").All(a => !a.Navigate) && !f.E.PredictedDamage.Any(),
            "Non target viewer received charge navigation/damage.");
        Check(f.Aoes("kenko/devour/").Any(a => a.Navigate && a.Shape is FanTemplate { Radius: 10 }),
            "Non target Devour lost real cone avoidance.");
        f.Actors.Add(new(3, 0x233C, Center - Vector3.UnitZ));
        f.Sync();
        f.Cast(12206, 3, 100);
        var firstClock = f.E.KenkoCharges[0].Activation;
        Check(f.Aoes("kenko/charge/").Count(a => a.Navigate) == 2
            && f.Aoes("kenko/charge/").Any(a => a.Navigate && a.Shape is FanTemplate { Radius: 100 })
            && f.E.PredictedDamage.Count() == 2
            && f.E.PredictedDamage.All(p => p.Activation == firstClock), "Whole-component gate/first prediction clock.");
        f.Set(2, a => a with { Dead = true });
        f.Sync();
        Check(f.E.PredictedDamage.Single().Activation == firstClock, "Dead first source changed component prediction clock.");
        f.Set(101, a => a with { Position = Center - Vector3.UnitZ });
        f.Sync();
        Check(f.Aoes("kenko/charge/").Where(a => a.Navigate).All(a => a.Shape is not FanTemplate),
            "Non finite asin was replaced with an invented angle.");
        Check(f.Aoes("kenko/devour/").Any(a => a.Navigate && a.Shape is FanTemplate { Radius: 100 }),
            "Devour incorrectly clipped target-specific cone to radius10.");
    }

    private static void Raw()
    {
        var f = new Fixture();
        var packet = new DungeonCastObservation(12206, Center, 0, Start, Start.AddSeconds(5), 100);
        f.E.StartCast(2, packet, Start);
        f.Set(2, a => a with { Cast = packet with { TargetId = 101, Rotation = 1 } });
        f.Now = Start.AddSeconds(.5);
        f.Sync();
        Check(f.E.KenkoDevours.Count == 1 && f.E.KenkoCharges.Count == 1
            && f.E.KenkoDevours.Single().Target.Actor.Id == 101, "First raw duplicated packet Devour.");
        f.Set(2, a => a with { Cast = a.Cast! with { Rotation = 2 } });
        f.Now = Start.AddSeconds(.6);
        f.Sync();
        Check(f.E.KenkoDevours.Count == 2 && f.E.KenkoCharges.Count == 1, "Later real raw replacement lost old Devour.");
        f.E.Finish(2, 12206, Start.AddSeconds(.7));
        f.Now = Start.AddSeconds(.8);
        f.Sync();
        Check(f.E.KenkoCharges.Count == 0 && f.E.KenkoDevours.Count == 2, "Stable cancelled raw resurrected bait.");
        f.Effect(12204);
        f.Sync();
        Check(f.E.KenkoDevours.Count == 0, "Consumed Devour resurrected from stable raw.");
        var consumed = new Fixture();
        consumed.E.StartCast(2, packet, Start);
        consumed.Effect(12204);
        consumed.Set(2, a => a with { Cast = packet with { TargetId = 101, Rotation = 1 } });
        consumed.Now = Start.AddSeconds(.5);
        consumed.Sync();
        Check(consumed.E.KenkoDevours.Count == 0 && consumed.E.KenkoCharges.Count == 1,
            "First raw resurrected a Devour already consumed by native effect.");
    }

    private static void Spread()
    {
        var f = new Fixture();
        f.Cast(12207, target: 101);
        var spread = f.Aoes("quest/spread/");
        Check(spread.Single(a => a.Display).Shape == new CircleTemplate(3)
            && spread.Single(a => a.Navigate).Shape == new CircleTemplate(4)
            && !f.E.PredictedDamage.Any(), "Spread radius/extra damage.");
        f.Actors.RemoveAll(a => a.Id == 101);
        f.Sync();
        Check(f.Aoes("quest/spread/").Length == 1 && !f.Aoes("quest/spread/").Single().Navigate,
            "Solo spread added navigation.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
