using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleBeautyRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);

    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale beauty exact ownership components and two different pull completion predicates", Lifecycle);
        run("Carnivale beauty seven AOE actions source dimensions cast locks and observed end", Shapes);
        run("Carnivale beauty grouped AOE arbitrary finish retains generation guard and caster independence", GroupFinish);
        run("Carnivale beauty gaze packet raw lock corrections generations cancellation and unknown isolation", GazeLocks);
        run("Carnivale beauty gaze full range eye pupil arc facing deadline double eyes and player switch", GazePerspective);
        run("Carnivale beauty succubus interrupt flags identity projection and real finish only", Interrupt);
        run("Carnivale beauty blood rain exact hint party damage deadline no invented AOE", Raidwide);
        run("Carnivale beauty background blood rain hidden until display handoff raw finish survives packet correction", RaidwideHandoff);
        run("Carnivale beauty public output source markers colours priorities previews navigation cleanup", Output);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal ulong Player = 100;
        internal Fixture(uint oid = 0x26F8, bool combat = true)
        {
            Actors =
            [
                new(1, oid, Center, InCombat: combat, Name: "Source Name", Hp: 1000,
                    IsEnemy: true, ObjectIndex: 2, TargetId: 100),
                new(100, 0, Center, PartySlot: 0, Player: true, Hostile: false),
            ];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == Player).Position, Now, playerId: Player);
        internal DungeonCastObservation Raw(uint action, ulong id = 1, Vector3? location = null,
            float rotation = .7f, double start = 0, double finish = 3)
        {
            var cast = new DungeonCastObservation(action, location ?? Center, rotation,
                Start.AddSeconds(start), Start.AddSeconds(finish), 100);
            Set(id, a => a with { Cast = cast });
            Sync();
            return cast;
        }
    }

    private static void Lifecycle()
    {
        foreach (var oid in new uint[] { 0x26F5, 0x26F8 })
        {
            var f = new Fixture(oid, false);
            Check(CarnivaleCatalog.Owns(796, oid) && !CarnivaleCatalog.Owns(795, oid)
                && f.E.CarnivalePrecombatHint != null && f.E.Active == null, "Ownership/preparation.");
            f.Actors.Add(new(2, oid == 0x26F5 ? 0x26F6u : 0x26F7u, Center, InCombat: true));
            f.Set(1, a => a with { Targetable = false });
            f.Sync();
            Check((f.E.Active != null) == (oid == 0x26F5), "Group/default pull mixed.");
            f.Set(1, a => a with { Targetable = true, InCombat = true });
            f.Sync();
            Check(f.E.CarnivalePrecombatHint == null && f.E.Active!.Casts.Length == (oid == 0x26F5 ? 1 : 9),
                "Components/preparation.");
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            if (oid == 0x26F8)
                Check(f.E.Active == null, "Act2 waited for add.");
            else
            {
                Check(f.E.Active != null, "HP0 ended group.");
                f.Set(1, a => a with { Dead = true });
                f.Sync();
                Check(f.E.Active != null, "Living Vodoriga lost.");
                f.Actors.RemoveAll(a => a.Id == 2);
                f.Sync();
                Check(f.E.Active == null, "Group end leaked.");
            }
        }
    }

    private static void Shapes()
    {
        foreach (var (oid, action, inside, outside) in new[]
        {
            (0x26F5u, 14879u, new Vector3(0, 0, 7.39f), new Vector3(0, 0, 7.41f)),
            (0x26F8u, 14880u, new Vector3(4.99f, 0, 0), new Vector3(5.01f, 0, 0)),
            (0x26F8u, 14888u, new Vector3(9.99f, 0, 0), new Vector3(10.01f, 0, 0)),
            (0x26F8u, 14884u, new Vector3(9.99f, 0, 0), new Vector3(10.01f, 0, 0)),
            (0x26F8u, 14889u, new Vector3(5.99f, 0, 0), new Vector3(6.01f, 0, 0)),
            (0x26F8u, 14881u, new Vector3(3.99f, 0, 41.99f), new Vector3(4.01f, 0, 41.99f)),
            (0x26F8u, 14887u, new Vector3(19.99f, 0, 0), new Vector3(9.99f, 0, 0)),
        })
        {
            var f = new Fixture(oid);
            var locked = Center + Vector3.UnitX * 3;
            f.Raw(action, location: locked, rotation: 0);
            var c = f.E.OutputCasts.Single().Value;
            var shape = c.Rule.Shape!.Resolve(c.Location, c.Rotation, null);
            Check(shape.Contains(locked + inside, 0) && !shape.Contains(locked + outside, 0), "Source geometry.");
            if (action == 14879)
                Check(!shape.Contains(locked + Vector3.UnitX * 5, 0), "Mow angle.");
            if (action == 14881)
                Check(!shape.Contains(locked - Vector3.UnitZ, 0) && !shape.Contains(locked + Vector3.UnitZ * 42.01f, 0), "Rect front/back.");
            f.Set(1, a => a with { Position = Center + Vector3.UnitZ * 15, Rotation = -1 });
            f.Now = Start.AddSeconds(20);
            f.Sync();
            Check(f.E.Casts[1].Location == locked && f.E.Casts[1].Rotation == 0, "Lock or predicted expiry.");
            f.Set(1, a => a with { Cast = null });
            f.Sync();
            Check(!f.E.OutputCasts.Any(), "Observed end.");
        }
    }

    private static void GroupFinish()
    {
        var f = new Fixture();
        f.Actors.Add(new(2, 0x233C, Center));
        f.Raw(14888);
        f.Raw(14884, 2);
        f.E.Finish(1, 60000, Start.AddSeconds(.5));
        Check(!f.E.Casts.ContainsKey(1) && f.E.Casts.ContainsKey(2), "Group arbitrary finish cleared wrong caster.");
        f.Sync();
        Check(!f.E.Casts.ContainsKey(1), "Cancelled unchanged raw revived.");
        f.Now = Start.AddSeconds(1);
        f.Raw(14888, location: Center + Vector3.UnitZ * 2);
        f.E.Finish(1, 14884, Start.AddSeconds(.5));
        Check(f.E.Casts.ContainsKey(1), "Old finish consumed replacement.");
        f.E.Finish(1, 60000, f.Now);
        Check(!f.E.Casts.ContainsKey(1), "Current arbitrary finish leaked.");
        f.E.Finish(2, 14888, f.Now);
        Check(f.E.Casts.Count == 0, "Other grouped action did not end.");
    }

    private static void GazeLocks()
    {
        var f = new Fixture();
        var a = Center;
        var b = Center + Vector3.UnitX * 4;
        var c = Center + Vector3.UnitZ * 8;
        f.E.StartCast(1, new(14951, a, 0, Start, Start.AddSeconds(3)), Start);
        f.Raw(14951, location: b);
        Check(f.E.Gazes.Single().Location == b, "First raw did not correct packet lock.");
        f.Set(1, x => x with { Position = c, Rotation = -2 });
        f.Sync();
        Check(f.E.Gazes.Single().Location == b, "Eye followed actor.");
        f.Now = Start.AddSeconds(1);
        f.Raw(14951, location: c);
        f.E.Finish(1, 14951, Start.AddSeconds(.5));
        Check(f.E.Gazes.Single().Location == c, "Late finish consumed raw generation.");
        f.E.Finish(1, 60000, f.Now);
        Check(f.E.Gazes.Any(), "Foreign action cleared gaze.");
        f.E.Effect(1, 14951, 1, Start.AddSeconds(3));
        f.Now = Start.AddSeconds(20);
        f.Sync();
        Check(f.E.Gazes.Any(), "Effect/deadline cleared observed gaze.");
        f.E.Finish(1, 14951, f.Now);
        f.Sync();
        Check(!f.E.Gazes.Any(), "Cancel or unchanged raw.");
        f.Raw(14951, location: a, start: 21, finish: 24);
        Check(f.E.Gazes.Any(), "Real restart missing.");
        f.E.Finish(1, 14951, Start.AddSeconds(22));
        foreach (var action in new uint[] { 15043, 14886, 14885, 14952 })
            f.E.StartCast(1, new(action, a, 0, Start.AddSeconds(25), Start.AddSeconds(28)), Start.AddSeconds(25));
        Check(f.E.Casts.Count == 0 && !f.E.CarnivaleInterrupts.Any(), "Inactive enum became a component.");
    }

    private static void GazePerspective()
    {
        var f = new Fixture();
        f.Set(100, a => a with { Position = Center - Vector3.UnitZ * 120 });
        f.Actors.Add(new(101, 0, Center - Vector3.UnitZ * 120, PartySlot: 1, Player: true, Rotation: MathF.PI));
        f.Raw(14951, rotation: 0);
        var p = f.Actors.Single(a => a.Id == 100).Position;
        Check(f.E.Hints(p).Contains("Turn away from gaze!") && f.E.GazeFacing(p, 0) == null, "Range or lead.");
        var shown = f.E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/gaze/")).ToArray();
        Check(shown.Length == 3 && shown.All(a => !a.Navigate) && !shown.Single(a => a.Id.EndsWith("/eye")).Safe, "Eye/pupil/arc.");
        Check(shown.Single(a => a.Id.EndsWith("/eye")).Position == Center
            && shown.Single(a => a.Id.EndsWith("/pupil")).Position == Center
            && shown.Single(a => a.Id.EndsWith("/view")).Position == p
            && shown.Single(a => a.Id.EndsWith("/view")).Rotation == 0, "Eye and viewer arc anchors.");
        f.Now = Start.AddSeconds(2.3);
        f.Sync();
        Check(f.E.GazeFacing(p, 0) is { } heading && MathF.Cos(heading) < -.9f, "Deadline heading.");
        f.Player = 101;
        f.Sync();
        Check(!f.E.Hints(p).Contains("Turn away from gaze!")
            && f.E.AmbientAoes().Single(a => a.Id.EndsWith("/eye")).Safe, "Viewer switch retained old eye colour.");
        f.Set(101, a => a with { Position = Center - Vector3.UnitZ * 10001, Rotation = 0 });
        f.Sync();
        p = f.Actors.Single(a => a.Id == 101).Position;
        Check(f.E.GazeFacing(p, 0) == null && !f.E.Hints(p).Contains("Turn away from gaze!")
            && f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/gaze/")) == 2, "Default 10000 range.");
        f.Actors.Add(new(2, 0x233C, Center, Cast: new(14951, Center + Vector3.UnitX * 5, 0, Start, Start.AddSeconds(3))));
        f.Sync();
        Check(f.E.Gazes.Count() == 2, "Two casters collapsed.");
        f.E.Finish(1, 14951, f.Now);
        Check(f.E.Gazes.Single().Source == 2, "One finish cleared both eyes.");
    }

    private static void Interrupt()
    {
        var f = new Fixture();
        f.Actors.Add(new(2, 0x26F7, Center, IsEnemy: true, ObjectIndex: 4));
        f.Raw(15045, 2, finish: 7);
        var i = f.E.CarnivaleInterrupts.Single();
        Check(i.ShouldBeInterrupted && !i.ShouldBeStunned && f.E.Hints(Center).Contains("Interrupt!"), "Interrupt flags.");
        f.Set(2, a => a with { Dead = true, Targetable = false });
        f.Now = Start.AddSeconds(12);
        f.Sync();
        Check(f.E.CarnivaleInterrupts.Single().Generation == i.Generation
            && !f.E.CarnivaleInterrupts.Single().ShouldBeInterrupted && f.E.Hints(Center).Contains("Interrupt!"),
            "Caster list confused with eligible enemy projection.");
        f.Set(2, a => a with { Cast = null });
        f.Sync();
        Check(!f.E.CarnivaleInterrupts.Any() && !f.E.PredictedDamage.Any(), "Finish or invented damage.");
    }

    private static void Raidwide()
    {
        var f = new Fixture();
        f.Actors.Add(new(101, 0, Center, PartySlot: 1, Player: true));
        f.Actors.Add(new(102, 0, Center, PartySlot: 2, Player: true, Dead: true));
        f.Raw(14882);
        const string text = "Harmless raidwide unless you failed to kill succubus in time";
        Check(f.E.Hints(Center).Contains(text) && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null)
            && !f.E.CarnivaleInterrupts.Any(), "Raidwide hint became an AOE/interrupt.");
        Check(f.E.PredictedDamage.Select(p => (p.TargetId, p.PartySlot, p.DamageType)).Order()
                .SequenceEqual(new[] { (100ul, 0, "Raidwide"), (101ul, 1, "Raidwide") })
            && f.E.PredictedDamage.All(p => p.Activation == Start.AddSeconds(3)), "Party prediction.");
        f.Now = Start.AddSeconds(10);
        f.Sync();
        Check(f.E.PredictedDamage.Count() == 2, "Predicted expiry.");
        f.E.Finish(1, 14882, f.Now);
        Check(!f.E.PredictedDamage.Any() && !f.E.Hints(Center).Contains(text), "Finish prediction leaked.");
    }

    private static void Output()
    {
        foreach (var oid in new uint[] { 0x26F5, 0x26F8 })
        {
            var f = new Fixture(oid, false);
            f.Actors.Add(new(2, oid == 0x26F5 ? 0x26F6u : 0x26F7u, Center + Vector3.UnitX * 5,
                InCombat: true, IsEnemy: true, ObjectIndex: 4));
            f.Sync();
            using var safe = new ScriptSafeZoneApi { UtcNowForTest = Start, DangerMarginForTest = 0 };
            var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
            output.Apply(f.E, Start, false);
            Check((safe.Manager.BuildSnapshot(Start).ArenaBoundary != null) == (oid == 0x26F5), "Precombat navigation.");
            f.Set(1, a => a with { InCombat = true });
            f.Sync();
            var markers = f.E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/enemy/")).ToArray();
            Check(markers.Length == 2 && markers.Single(a => a.Id.EndsWith("/2")).Color
                == (oid == 0x26F8 ? new Vector4(1, 128f / 255, 0, 1) : new Vector4(1, 0, 0, 1))
                && markers.Single(a => a.Id.EndsWith("/1")).Color == new Vector4(1, 0, 0, 1), "Marker colours.");
            Check(f.E.PriorityTarget(1, Center) == 1, "Invented succubus priority.");
            f.Raw(oid == 0x26F5 ? 14879u : 14880u);
            output.Apply(f.E, Start, false);
            Check(safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 1 && safe.Manager.BuildSnapshot(Start).GoalAreas.Count == 0,
                "Navigation projection.");
            Check(f.E.Arena!.GetContours().Single().Count == (oid == 0x26F5 ? 64 : 32), "Exact polygon.");
            output.Clear();
            Check(safe.Manager.BuildSnapshot(Start).ArenaBoundary == null && safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0,
                "Owner cleanup.");
        }
    }

    private static void RaidwideHandoff()
    {
        var f = new Fixture(0x25D5);
        f.Actors.Add(new(2, 0x26F8, Center, InCombat: true));
        f.Sync();
        f.Raw(14882, 2);
        const string text = "Harmless raidwide unless you failed to kill succubus in time";
        Check(f.E.Active!.BossOid == 0x25D5 && f.E.Casts.ContainsKey(2)
            && !f.E.Hints(Center).Contains(text) && !f.E.PredictedDamage.Any(), "Background raidwide leaked.");
        f.Set(1, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active!.BossOid == 0x26F8 && f.E.Hints(Center).Contains(text)
            && f.E.PredictedDamage.Single().Activation == Start.AddSeconds(3), "Handoff lost background raidwide.");
        f.E.StartCast(2, new(14882, Center, 0, Start.AddSeconds(.1), Start.AddSeconds(3.2)), Start.AddSeconds(.1));
        Check(f.E.PredictedDamage.Single().Activation == Start.AddSeconds(3), "Packet correction overwrote current raw prediction.");
        f.E.Finish(2, 14882, Start.AddSeconds(1));
        f.Sync();
        Check(!f.E.Hints(Center).Contains(text) && !f.E.PredictedDamage.Any(), "Finished background raw revived.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
