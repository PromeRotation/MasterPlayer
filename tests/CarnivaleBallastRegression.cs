using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleBallastRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale ballast primary pull complete preparation and exact component scope", Lifecycle);
        run("Carnivale ballast full three remaining sectors share clock all risky and navigation", Outputs);
        run("Carnivale ballast visual cancel and effects persist helper cancel advances consumer clock", Timing);
        run("Carnivale ballast axial and angle first matching sequence independent of source", Matching);
        run("Carnivale ballast corrections cannot resurrect consumed stages true raw changes new generation", Generations);
        run("Carnivale ballast helper destroyed advances but visual destroyed retains sequence", Destroy);
        run("Carnivale ballast simple AOEs dead serpent interrupt intent priorities and colours", SimpleAndTargets);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x26F9, new(100, 0, 112), InCombat: true, IsEnemy: true, ObjectIndex: 2, TargetId: 100),
            new(2, 0x233C, Center), new(3, 0x233C, Center),
            new(100, 0, Center, Player: true, PartySlot: 0),
        ];
        internal DateTime Now = Start;
        internal Fixture() => Sync();
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Center, Now, playerId: 100);
        internal void Cast(ulong source, uint action, Vector3? location = null, float rotation = .3f)
            => E.StartCast(source, new(action, location ?? Center, rotation, Now, Now.AddSeconds(1.3)), Now);
        internal DungeonAmbientAoe[] Ballasts() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/ballast/")).ToArray();
    }

    private static void Lifecycle()
    {
        var f = new Fixture();
        f.Set(1, a => a with { InCombat = false });
        // A fresh module is needed: leaving combat never restarts an already pulled module.
        var e = new DungeonEncounter(796);
        e.Synchronize(f.Actors, Center, Start, playerId: 100);
        Check(e.Active == null && e.CarnivalePrecombatHint!.Contains("Acorn Bomb")
            && e.AmbientAoes().All(a => !a.Navigate), "Preparation or pull.");
        f.Actors.Add(new(4, 0x26FA, Center, InCombat: true));
        e.Synchronize(f.Actors, Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active == null, "Add pulled primary.");
        f.Set(1, a => a with { InCombat = true, Targetable = false });
        e.Synchronize(f.Actors, Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active == null, "Untargetable primary pulled.");
        f.Set(1, a => a with { Targetable = true });
        e.Synchronize(f.Actors, Center, Start.AddSeconds(3), playerId: 100);
        Check(e.Active?.BossOid == 0x26F9 && e.CarnivalePrecombatHint == null
            && e.Active.Casts.Length == 10 && !e.PredictedDamage.Any(), "Component rules.");
        e.StartCast(1, new(14893, Center, .3f, Start.AddSeconds(3), Start.AddSeconds(4.3)), Start.AddSeconds(3));
        f.Set(1, a => a with { Hp = 0 });
        e.Synchronize(f.Actors, Center, Start.AddSeconds(4), playerId: 100);
        Check(e.Active == null && !e.CarnivaleBallasts.Any(), "Primary HP0 waited for adds/ballast.");
        foreach (var action in new uint[] { 6497, 14897, 14955, 14971 })
            Check(!CarnivaleCatalog.Create().Single(d => d.BossOid == 0x26F9).Casts.Any(c => c.Action == action), "Invented component.");
    }

    private static void Outputs()
    {
        var f = new Fixture();
        f.Cast(1, 14893);
        var sequence = f.E.CarnivaleBallasts.Single();
        Check(sequence.Origin == Center && sequence.Rotation == .3f && sequence.VisualCast.Source == 1
            && sequence.Activation == Start.AddSeconds(4.9), "Visual lock/start/deadline.");
        var aoes = f.Ballasts();
        Check(aoes.Length == 3 && aoes.All(a => a.Activation == sequence.Activation && a.Navigate && a.Color == null), "Showall or risk.");
        Check(aoes[0].Shape is FanTemplate { Radius: 5.5f }
            && aoes[1].Shape is MasterPlayer.Mechanics.PilgrimsTraverse.PtDonutSectorTemplate { Inner: 5.5f, Outer: 10.5f }
            && aoes[2].Shape is MasterPlayer.Mechanics.PilgrimsTraverse.PtDonutSectorTemplate { Inner: 10.5f, Outer: 15.5f }, "Exact bands.");
        using var safe = new ScriptSafeZoneApi { UtcNowForTest = Start, DangerMarginForTest = 0 };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        output.Apply(f.E, Start, false);
        Check(safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 3, "Future bands lost navigation.");
        output.Clear();
        Check(safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0, "Owner.");
    }

    private static void Timing()
    {
        var f = new Fixture();
        f.Cast(1, 14893);
        f.E.Finish(1, 14893, Start.AddSeconds(.1), completed: false);
        f.E.Effect(1, 14893, 1, Start.AddSeconds(20));
        Check(f.Ballasts().Length == 3, "Visual cancel/effect removed sequence.");
        f.Now = Start.AddSeconds(20);
        f.Cast(2, 14894);
        f.E.Finish(2, 14894, f.Now, completed: false);
        Check(f.Ballasts().Length == 2 && f.E.CarnivaleBallasts.Single().Order == 1
            && f.Ballasts().All(a => a.Activation == f.Now.AddSeconds(.6)), "Helper cancel/consumer clock.");
        f.Now = Start.AddSeconds(30);
        f.Cast(2, 14896);
        f.E.Finish(2, 14896, f.Now);
        Check(f.Ballasts().Length == 2, "Out of order advanced.");
        f.Now = f.Now.AddSeconds(.1);
        f.Cast(2, 14895);
        f.E.Finish(2, 14895, f.Now);
        Check(f.Ballasts().Length == 1 && f.Ballasts()[0].Activation == f.Now.AddSeconds(.6), "Second stage.");
        f.Now = f.Now.AddSeconds(.1);
        f.Cast(2, 14896);
        f.E.Finish(2, 14896, f.Now);
        Check(f.Ballasts().Length == 0, "Third stage.");
    }

    private static void Matching()
    {
        var f = new Fixture();
        f.Cast(1, 14893, rotation: MathF.Tau - .02f);
        f.Cast(3, 14893, rotation: MathF.Tau - .02f);
        f.Cast(2, 14894, Center + new Vector3(1.01f, 0, 0), .01f);
        f.E.Finish(2, 14894, Start);
        Check(f.E.CarnivaleBallasts.All(s => s.Order == 0), "Euclidean/loose origin match.");
        f.Now = Start.AddSeconds(1);
        f.Cast(2, 14894, Center + new Vector3(.9f, 0, .9f), .01f);
        f.E.Finish(2, 14894, f.Now);
        Check(f.E.CarnivaleBallasts.Select(s => s.Order).SequenceEqual(new[] { 1, 0 }), "Axial first match or wrapped angle.");
        f.Now = Start.AddSeconds(2);
        f.Cast(2, 14894, rotation: .04f);
        f.E.Finish(2, 14894, f.Now);
        Check(f.E.CarnivaleBallasts.Select(s => s.Order).SequenceEqual(new[] { 1, 0 }), "Angle tolerance.");
    }

    private static void Generations()
    {
        var f = new Fixture();
        f.Cast(1, 14893);
        f.Cast(2, 14894);
        f.E.Finish(2, 14894, Start);
        var first = f.E.CarnivaleBallasts.Single().Generation;
        f.E.StartCast(1, new(14893, Center + Vector3.UnitX, .4f, Start, Start.AddSeconds(1.3)), Start.AddSeconds(.1));
        Check(f.E.CarnivaleBallasts.Single().Order == 1 && f.E.CarnivaleBallasts.Single().Generation == first, "Correction reset stage.");
        f.Set(1, a => a with { Cast = new(14893, Center + Vector3.UnitZ, .5f, Start, Start.AddSeconds(1.3)) });
        f.Now = Start.AddSeconds(.2);
        f.Sync();
        Check(f.E.CarnivaleBallasts.Count() == 2 && f.E.CarnivaleBallasts.Last().Generation > first, "Changed raw lost new sequence.");
        foreach (var advance in new[] { false, true })
        {
            var g = new Fixture();
            g.Set(1, a => a with { Cast = new(14893, Center, .3f, Start, Start.AddSeconds(1.3)) });
            g.Sync();
            var second = Center + Vector3.UnitX * 10;
            g.Now = Start.AddSeconds(.1);
            g.Set(1, a => a with { Cast = a.Cast! with { Location = second } });
            g.Sync();
            if (advance)
            {
                g.Now = Start.AddSeconds(.2);
                g.Cast(2, 14894, second);
                g.E.Finish(2, 14894, g.Now);
            }
            g.E.StartCast(1, new(14893, second + Vector3.UnitX, .3f, Start, Start.AddSeconds(1.3)), Start.AddSeconds(.3));
            Check(g.E.CarnivaleBallasts.First().Origin == Center && g.E.CarnivaleBallasts.First().Order == 0
                && g.E.CarnivaleBallasts.Last().Origin == (advance ? second : second + Vector3.UnitX)
                && g.E.CarnivaleBallasts.Last().Order == (advance ? 1 : 0), "Packet correction modified older same-clock visual sequence.");
        }
    }

    private static void Destroy()
    {
        var f = new Fixture();
        f.Set(1, a => a with { Cast = new(14893, Center, .3f, Start, Start.AddSeconds(1.3)) });
        f.Set(2, a => a with { Cast = new(14894, Center, .3f, Start, Start.AddSeconds(3.3)) });
        f.Sync();
        f.Actors.RemoveAll(a => a.Id == 2);
        f.Now = Start.AddSeconds(.5);
        f.Sync();
        Check(f.E.CarnivaleBallasts.Single().Order == 1, "Helper destroy did not Finish.");
        f.Actors.Add(new(4, 0x233C, Center, Cast: new(14893, Center, .3f, f.Now, f.Now.AddSeconds(1.3))));
        f.Sync();
        f.Actors.RemoveAll(a => a.Id == 4);
        f.Sync();
        Check(f.E.CarnivaleBallasts.Count() == 2, "Visual destroy removed sequence.");
    }

    private static void SimpleAndTargets()
    {
        var f = new Fixture();
        f.Actors.Add(new(4, 0x26FA, Center, IsEnemy: true, ObjectIndex: 4, TargetId: 100, InCombat: true));
        f.Actors.Add(new(5, 0x26FB, Center, IsEnemy: true, ObjectIndex: 6, TargetId: 100, InCombat: true));
        f.Sync();
        Check(f.E.PriorityTarget(1, Center) == 4, "Shabti priority.");
        f.Set(4, a => a with { Hp = 1 });
        f.Sync();
        Check(f.E.PriorityTarget(1, Center) == 5, "Pointless or serpent priority.");
        var markers = f.E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/enemy/")).ToArray();
        Check(markers.Length == 3 && markers.Where(a => !a.Id.EndsWith("/1")).All(a => a.Color == new Vector4(1, 128f / 255, 0, 1)), "Object colour.");
        f.Cast(1, 14890);
        Check(f.E.CarnivaleInterruptCasts.Single().Rule.Action == 14890 && f.E.Hints(Center).Contains("Interrupt!")
            && !f.E.PredictedDamage.Any(), "Interrupt only.");
        f.Set(5, a => a with { Dead = true, Cast = new(14899, Center, 0, Start, Start.AddSeconds(2.3)) });
        f.Sync();
        Check(f.E.Casts.Values.Any(c => c.Rule.Action == 14899 && c.Rule.Shape is CircleTemplate { Radius: 7.2f }), "Dead snake cast discarded.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
