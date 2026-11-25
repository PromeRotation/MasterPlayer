using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class HeavenOnHighRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-300, 0, -300);
    private static readonly (uint Territory, uint Oid)[] Bosses = [(770, 0x23E1), (771, 0x23E7),
        (772, 0x2251), (782, 0x23E2), (773, 0x23E9), (783, 0x23E4), (784, 0x23EC), (775, 0x23EF), (774, 0x23EB)];
    internal static void Run(Action<string, Action> run)
    {
        run("HoH exact territories ownership and precombat no navigation helper markers", Ownership);
        run("HoH actual cast rules exact source geometry and no invented actions", Shapes);
        run("HoH observed Finish cancellation deadline effect and saturated raw clock identity", Timing);
        run("HoH default primary HP0 death destroyed ends disengage retains", Lifecycle);
        run("HoH two knockbacks locked spell origin ignore immunity stop wall continuous sources", Knockbacks);
        run("HoH gaze default10000 locked eye PR facing and real Finish", Gazes);
        run("HoH Bhima spread target FIFO destroyed identity solo no navigation", Spreads);
        run("HoH Bhima and Gozu persistent voidzone original position precise filtering hard obstacle", Voidzones);
        run("HoH disabled owner reset clears casts knockbacks previews", Disabled);
        run("HoH packet origin correction raw replacement and stale cancel generation boundary", Corrections);
        run("HoH destroyed and missing helper reappear late bootstrap dead cast and battle end", Bootstrap);
        run("HoH60 creation donuts no capacity limit locked clock effect destruction and Finish", Cane);
        run("HoH80 creation spheres effect only no capacity limit death destroy timeout and bootstrap", Ice);
        run("HoH90 original activation three Finish counts axis first match and destroy ordering", Sand);
        HeavenOnHighKenkoRegression.Run(run);
    }
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E;
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(int index, bool combat = true)
        {
            var (territory, oid) = Bosses[index];
            E = new(territory);
            Actors = [new(1, oid, Center, InCombat: combat), new(2, 0x233C, Center),
                new(100, 0, Center + Vector3.UnitZ * 5, PartySlot: 0, Rotation: MathF.PI, Player: true)];
            Sync();
        }
        internal void Sync(Func<string, bool>? enabled = null) => E.Synchronize(Actors,
            Actors.First(a => a.Id == 100).Position, Now, enabled, 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Cast(uint action, ulong source = 2, ulong target = 100, Vector3? location = null, float rotation = MathF.PI / 2)
        {
            Now = Now.AddSeconds(.01);
            Set(source, a => a with { Cast = new(action, location ?? Center, rotation, Now, Now.AddSeconds(3), target) });
            Sync();
        }
    }
    private static void Ownership()
    {
        foreach (var (territory, oid) in Bosses)
        {
            var f = new Fixture(Array.FindIndex(Bosses, b => b.Territory == territory), false);
            Check(HeavenOnHighCatalog.Owns(territory, oid)
                && !HeavenOnHighCatalog.Owns(territory == 782 ? 773u : 782u, oid)
                && f.E.Active == null && f.E.AmbientAoes().All(a => !a.Navigate)
                && f.E.AmbientAoes().Count(a => a.Id.StartsWith("hoh/enemy/")) == 1
                && f.E.AmbientAoes().Any(a => a.Id == "hoh/precombat/arena"), "Precombat source ownership.");
        }
        Check(!HeavenOnHighCatalog.IsTerritory(540) && !HeavenOnHighCatalog.Owns(770, 0x23E3), "CFC/helper was claimed.");
    }
    private static void Shapes()
    {
        Check(Bosses.Sum(b => HeavenOnHighCatalog.Create(b.Territory).Single().Casts.Length) == 39, "Cast count.");
        var expected = new Dictionary<uint, GeometryTemplate>
        {
            [11876] = new FanTemplate(27.4f, MathF.PI), [11878] = new CircleTemplate(7),
            [11899] = new FanTemplate(53, MathF.PI / 2), [11901] = new CircleTemplate(8),
            [11293] = RayRectangleTemplate.Symmetric(100, 55.25f), [11291] = new CircleTemplate(6),
            [11292] = RayRectangleTemplate.Symmetric(6, 55.25f), [11294] = new DonutTemplate(1, 8),
            [11905] = RayRectangleTemplate.Symmetric(8, 52.4f), [11903] = new CircleTemplate(6),
            [11919] = new FanTemplate(11.45f, 2 * MathF.PI / 3), [11921] = new CircleTemplate(20),
            [11920] = RayRectangleTemplate.Symmetric(6, 48.45f),
            [11925] = new FanTemplate(10, MathF.PI / 2), [11927] = new CircleTemplate(5),
            [11926] = new CircleTemplate(5.5f), [11929] = RayRectangleTemplate.Symmetric(4, 61),
            [11985] = new FanTemplate(12.5f, 2 * MathF.PI / 3), [11990] = new CircleTemplate(6.5f),
            [11989] = RayRectangleTemplate.Symmetric(4, 82), [12213] = new CircleTemplate(8),
            [12212] = new CircleTemplate(20), [12216] = new CircleTemplate(9),
            [12215] = RayRectangleTemplate.Symmetric(10, 63),
            [12205] = new FanTemplate(15, 2 * MathF.PI / 3), [12203] = new CircleTemplate(8),
        };
        foreach (var (territory, _) in Bosses)
        {
            var f = new Fixture(Array.FindIndex(Bosses, b => b.Territory == territory));
            foreach (var rule in f.E.Definitions.Single().Casts.Where(c => c.Shape != null))
            {
                Check(rule.Shape == expected[rule.Action] && rule.CasterOid == 0, "Exact shape/caster restriction.");
                f.Cast(rule.Action);
                Check(f.E.Casts.Count == 1 && f.E.Casts.Values.Single().Rule.Shape == expected[rule.Action], "Helper cast not accepted.");
                f.E.Finish(2, rule.Action, f.Now.AddSeconds(.1));
            }
            foreach (var action in new uint[] { 11290, 11906, 11923, 11924, 11986, 11988, 12211 })
                f.E.StartCast(2, new(action, Center, 0, Start.AddSeconds(10), Start.AddSeconds(12)), Start.AddSeconds(10));
            Check(f.E.Casts.Count == 0, "Inactive enum action became component.");
        }
    }
    private static void Timing()
    {
        var f = new Fixture(2);
        f.Cast(11292);
        var first = f.E.Casts.Values.Single();
        f.Now = Start.AddSeconds(20);
        f.Set(2, a => a with { Cast = a.Cast! with { StartedAt = a.Cast.StartedAt.AddSeconds(1), FinishAt = a.Cast.FinishAt.AddSeconds(1) } });
        f.Sync();
        f.E.Effect(2, 11292, 1, f.Now);
        Check(f.E.Casts.Values.Single().StartedAt == first.StartedAt && f.E.Casts.Values.Single().FinishAt == first.FinishAt, "Deadline/effect/raw drift reset.");
        f.Set(2, a => a with { Cast = null });
        f.Sync();
        Check(f.E.Casts.Count == 0, "Raw end retained.");
        f.Cast(11292);
        f.E.Finish(2, 11292, f.Now.AddSeconds(.1));
        f.Sync();
        Check(f.E.Casts.Count == 0, "Cancel old raw resurrected.");
    }
    private static void Lifecycle()
    {
        foreach (var i in Enumerable.Range(0, Bosses.Length))
        {
            var f = new Fixture(i);
            f.Set(1, a => a with { InCombat = false });
            f.Sync();
            Check(f.E.Active != null, "Disengage ended default phase.");
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active == null, "HP0 did not end.");
            var dead = new Fixture(i);
            dead.Set(1, a => a with { Dead = true });
            dead.Sync();
            Check(dead.E.Active == null, "Death did not end.");
            var removed = new Fixture(i);
            removed.E.RemoveActor(1, Start.AddSeconds(1));
            Check(removed.E.Active == null, "Primary destroy did not end.");
            var missing = new Fixture(i);
            missing.Actors.RemoveAll(a => a.Id == 1);
            missing.Sync();
            Check(missing.E.Active == null, "Missing primary did not end.");
        }
    }
    private static void Knockbacks()
    {
        foreach (var (index, action) in new[] { (0, 11877u), (3, 11904u) })
        {
            var f = new Fixture(index);
            f.Set(100, a => a with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = Start.AddHours(1) } });
            var origin = Center - Vector3.UnitZ * 3;
            f.Cast(action, location: origin);
            var first = f.E.HeavenOnHighKnockbacks.Single();
            Check(first is { IgnoreImmunes: true, StopAtWall: true, Distance: 30 } && first.Origin == origin, "KB semantic parameters.");
            var before = f.E.HeavenOnHighMovements(f.Actors.Single(a => a.Id == 100)).Single();
            Check(Vector3.Distance(before.From, before.To) < 30 && f.E.Arena!.Contains(before.To, 0)
                && f.E.AmbientAoes().Where(a => a.Id.StartsWith("hoh/kb/")).All(a => !a.Navigate), "Wall/immune/display scope.");
            f.Set(2, a => a with { Position = Center + Vector3.UnitX * 15 });
            f.Sync();
            Check(f.E.HeavenOnHighKnockbacks.Single().Origin == origin, "KB followed caster.");
            f.Actors.Add(new(3, 0x233C, Center));
            f.Cast(action, 3, location: Center + Vector3.UnitX * 3);
            var moves = f.E.HeavenOnHighMovements(f.Actors.Single(a => a.Id == 100)).ToArray();
            Check(moves.Length == 2 && moves[1].From == moves[0].To, "Multiple KB origins not sequential.");
            f.E.Finish(2, action, f.Now.AddSeconds(.1));
            Check(f.E.HeavenOnHighKnockbacks.Count == 1, "Finish source FIFO.");
            f.E.RemoveActor(3, f.Now.AddSeconds(.2));
            Check(f.E.HeavenOnHighKnockbacks.Count == 0, "Destroyed KB retained.");
        }
    }
    private static void Gazes()
    {
        foreach (var (index, action) in new[] { (1, 11900u), (4, 11922u) })
        {
            var f = new Fixture(index);
            f.Cast(action);
            Check(f.E.Gazes.Count() == 1 && f.E.AmbientAoes().Count(a => a.Id.StartsWith("hoh/gaze/")) == 3, "True eye primitives.");
            f.Now = Start.AddSeconds(3);
            f.Sync();
            Check(f.E.GazeFacing(Center + Vector3.UnitZ * 60, MathF.PI) != null, "Gaze clipped to comment range.");
            f.E.Effect(2, 11923, 1, f.Now);
            Check(f.E.Gazes.Count() == 1, "Visual effect ended gaze.");
            f.E.Finish(2, action, f.Now);
            Check(!f.E.Gazes.Any(), "Real gaze Finish.");
        }
    }
    private static void Spreads()
    {
        var f = new Fixture(3);
        f.Cast(11902);
        Check(f.E.AmbientAoes().Single(a => a.Id.StartsWith("quest/spread/")).Shape is CircleTemplate { Radius: 6 }
            && !f.E.AmbientAoes().Any(a => a.Id.StartsWith("quest/spread/") && a.Navigate), "Solo spread.");
        f.Actors.Add(new(101, 0, Center + Vector3.UnitX * 3, PartySlot: 1, Player: true));
        f.Sync();
        Check(f.E.AmbientAoes().Any(a => a.Id.StartsWith("quest/spread/") && a.Shape is CircleTemplate { Radius: 7 } && !a.Display), "Spread nav radius.");
        f.E.Finish(2, 11902, f.Now.AddSeconds(.1));
        Check(!f.E.AmbientAoes().Any(a => a.Id.StartsWith("quest/spread/")), "Spread true Finish.");
        f.Actors.Add(new(3, 0x233C, Center));
        f.Cast(11902, target: 101);
        f.Cast(11902, 3, target: 101);
        f.E.Finish(3, 11902, f.Now.AddSeconds(.1));
        Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("quest/spread/") && a.Display) == 1, "Target FIFO consumed count.");
        f.E.RemoveActor(101, f.Now.AddSeconds(.2));
        f.Actors.RemoveAll(a => a.Id == 101);
        f.Actors.Add(new(101, 0, Center + Vector3.UnitX * 15, PartySlot: 1, Player: true));
        f.Sync();
        Check(MathF.Abs(f.E.AmbientAoes().Single(a => a.Id.StartsWith("quest/spread/") && a.Display).Position.X - Center.X - 3) < .04f,
            "Destroyed target rebound to same ID.");
        f.E.Finish(2, 11902, f.Now.AddSeconds(.3));
        Check(!f.E.AmbientAoes().Any(a => a.Id.StartsWith("quest/spread/")), "Cross source FIFO Finish.");
    }
    private static void Voidzones()
    {
        foreach (var (index, oid, radius) in new[] { (3, 0x23E3u, 6f), (4, 0x23EAu, 8f) })
        {
            var f = new Fixture(index);
            var location = Center + new Vector3(.123f, 0, .317f);
            f.Actors.Add(new(3, oid, location, Dead: true, EventState: 1));
            f.Sync();
            var aoe = f.E.AmbientAoes().Single(a => a.Id == "hoh/voidzone/3");
            Check(aoe.Obstacle && aoe.Position == location && aoe.Shape is CircleTemplate c && c.Radius == radius, "Hard obstacle original position.");
            Check(f.E.Hints(location).Contains("GTFO from voidzone!"), "Voidzone text missing.");
            f.Set(3, a => a with { EventState = 7 });
            f.Sync();
            Check(f.E.AmbientAoes().Count(a => a.Id == "hoh/voidzone/3") == (index == 3 ? 0 : 1), "Source state filter.");
        }
    }
    private static void Disabled()
    {
        var f = new Fixture(0);
        f.Cast(11877);
        f.Sync(_ => false);
        Check(f.E.Active == null && f.E.HeavenOnHighKnockbacks.Count == 0 && !f.E.AmbientAoes().Any(), "Disabled owner retained output.");
    }
    private static void Corrections()
    {
        foreach (var (index, action) in new[] { (0, 11877u), (1, 11900u), (3, 11904u), (4, 11922u) })
        {
            var f = new Fixture(index);
            var observation = new DungeonCastObservation(action, Center, 0, Start, Start.AddSeconds(3), 100);
            f.E.StartCast(2, observation, Start);
            var corrected = observation with { Location = Center + Vector3.UnitX * 4 };
            f.E.StartCast(2, corrected, Start.AddSeconds(.05));
            Check(f.E.Casts.Values.Single().Location == corrected.Location, "Packet correction lost.");
            if (action is 11877 or 11904)
                Check(f.E.HeavenOnHighKnockbacks.Single().Origin == corrected.Location, "Corrected KB origin stale.");
            f.Now = Start.AddSeconds(1);
            f.Set(2, a => a with { Cast = observation with { Location = Center + Vector3.UnitZ * 4 } });
            f.Sync();
            Check(f.E.Casts.Values.Single().FinishEventBoundary == f.Now, "Raw replacement boundary lost.");
            f.E.Finish(2, action, Start.AddSeconds(.8));
            Check(f.E.Casts.Count == 1 && (action is 11877 or 11904
                ? f.E.HeavenOnHighKnockbacks.Count == 1 : f.E.Gazes.Count() == 1), "Stale cancel consumed replacement.");
            f.E.Finish(2, action, Start.AddSeconds(1.1));
            Check(f.E.Casts.Count == 0 && f.E.HeavenOnHighKnockbacks.Count == 0 && !f.E.Gazes.Any(), "Current cancel not consumed.");
        }
    }
    private static void Bootstrap()
    {
        foreach (var explicitDestroy in new[] { false, true })
        {
            foreach (var finishFirst in new[] { false, true })
            {
                var f = new Fixture(1);
                f.Cast(11901);
                if (finishFirst) f.E.Finish(2, 11901, f.Now.AddSeconds(.1));
                if (explicitDestroy) f.E.RemoveActor(2, f.Now.AddSeconds(.2));
                f.Actors.RemoveAll(a => a.Id == 2);
                f.Now = Start.AddSeconds(1);
                f.Sync();
                f.Actors.Add(new(2, 0x233C, Center, Cast: new(11901, Center, MathF.PI / 2,
                    Start.AddSeconds(10), Start.AddSeconds(13), 100)));
                f.Now = Start.AddSeconds(10);
                f.Sync();
                Check(f.E.Casts.Count == 1 && f.E.Casts.Values.Single().StartedAt == Start.AddSeconds(10),
                    "Destroyed/missing helper raw baseline swallowed same ID new generation.");
            }
        }
        var late = new Fixture(0);
        late.Now = Start.AddSeconds(10);
        late.Set(2, a => a with { Dead = true, Cast = new(11877, Center, 0, Start, Start.AddSeconds(4.3), 100, TotalTime: 4) });
        late.Sync();
        var end = late.E.Casts.Values.Single().FinishAt;
        Check(Math.Abs((end - late.Now).TotalSeconds - ((4f + .3f) - 4f)) < .00001
            && late.E.HeavenOnHighKnockbacks.Single().Cast.FinishAt == end, "Late raw NPC elapsed cap or dead helper lost.");
        var originalKb = late.E.HeavenOnHighKnockbacks.Single();
        var originalBoundary = late.E.Casts.Values.Single().FinishEventBoundary;
        late.E.StartCast(2, late.Actors.Single(a => a.Id == 2).Cast!, late.Now.AddSeconds(.05));
        Check(ReferenceEquals(originalKb, late.E.HeavenOnHighKnockbacks.Single())
            && late.E.Casts.Values.Single().FinishAt == end
            && late.E.Casts.Values.Single().FinishEventBoundary == originalBoundary, "Late same generation packet rebuilt capped cast.");
        late.Now = late.Now.AddSeconds(1);
        late.Sync();
        Check(late.E.Casts.Values.Single().FinishAt == end && late.E.PreserveStageAcrossBattleEnd, "Bootstrap slid or battle end clears live boss.");
    }
    private static void Cane()
    {
        var f = new Fixture(5);
        f.Now = Start.AddSeconds(2);
        for (ulong id = 3; id < 14; ++id)
            f.Actors.Add(new(id, 0x23E5, Center + Vector3.UnitX * (id - 3)));
        f.Sync();
        Check(f.E.HeavenOnHighSpawnAoes.Count == 11 && f.E.HeavenOnHighSpawnAoes.All(a =>
            a.CreatedAt == f.Now && a.Activation == f.Now.AddSeconds(7.9)), "Cane capacity/bootstrap clock.");
        var first = f.E.HeavenOnHighSpawnAoes.First();
        f.Now = Start.AddSeconds(30);
        f.Set(3, a => a with { Position = Center + Vector3.UnitZ * 20, Dead = true });
        f.Sync();
        f.E.Effect(3, 11930, 1, f.Now);
        Check(f.E.HeavenOnHighSpawnAoes.First() == first && f.E.HeavenOnHighSpawnAoes.Count == 11, "Cane moved/died/expired/effect consumed.");
        f.E.RemoveActor(3, f.Now);
        f.Actors.RemoveAll(a => a.Id == 3);
        Check(f.E.HeavenOnHighSpawnAoes.Count == 11, "Cane no cast destruction consumed.");
        f.Cast(11930, 4);
        f.E.RemoveActor(4, f.Now);
        f.Actors.RemoveAll(a => a.Id == 4);
        Check(f.E.HeavenOnHighSpawnAoes.Count == 10 && f.E.HeavenOnHighSpawnAoes.All(a => a.ActorId != 4), "Cane cast destruction did not Finish first.");
        f.Actors.Add(new(3, 0x23E5, Center + Vector3.UnitX * 20));
        f.Sync();
        Check(f.E.HeavenOnHighSpawnAoes.Count(a => a.ActorId == 3) == 2, "Cane same ID new creation lost.");
        f.Cast(11930, 3);
        f.E.Finish(3, 11930, f.Now);
        Check(f.E.HeavenOnHighSpawnAoes.Count(a => a.ActorId == 3) == 1
            && f.E.HeavenOnHighSpawnAoes.All(a => a.Generation != first.Generation), "Cane Finish not first matching source.");
        Check(f.E.AmbientAoes().Where(a => a.Id.StartsWith("hoh/spawn/")).All(a => a.Shape is DonutTemplate { InnerRadius: 6, OuterRadius: 12 }),
            "Cane donut source parameters.");
    }
    private static void Ice()
    {
        var f = new Fixture(6, false);
        for (ulong id = 3; id < 15; ++id) f.Actors.Add(new(id, 0x23ED, Center));
        f.Sync();
        Check(f.E.HeavenOnHighSpawnAoes.Count == 0, "Ice precombat prediction.");
        f.Now = Start.AddSeconds(10);
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.HeavenOnHighSpawnAoes.Count == 12 && f.E.HeavenOnHighSpawnAoes.All(a => a.Activation == f.Now.AddSeconds(8.5)), "Ice activation bootstrap/capacity.");
        f.Set(3, a => a with { Dead = true, Position = Center + Vector3.UnitZ * 20 });
        f.Now = Start.AddSeconds(50);
        f.Sync();
        f.E.Finish(3, 11992, f.Now);
        f.E.RemoveActor(3, f.Now);
        f.Actors.RemoveAll(a => a.Id == 3);
        Check(f.E.HeavenOnHighSpawnAoes.Count == 12, "Ice Finish/death/destroy/timeout consumed.");
        f.E.Effect(3, 11992, 1, f.Now);
        Check(f.E.HeavenOnHighSpawnAoes.Count == 11, "Ice actual effect not consumed after destruction.");
        f.E.Effect(3, 11992, 1, f.Now);
        f.Sync();
        Check(f.E.HeavenOnHighSpawnAoes.Count == 11, "Ice effect/raw resurrected.");
        f.E.ResetFight();
        Check(f.E.HeavenOnHighSpawnAoes.Count == 0, "Ice owner reset retained.");
    }
    private static void Sand()
    {
        var f = new Fixture(7);
        var diagonal = Center + new Vector3(.09f, 0, .09f);
        f.Actors.Add(new(3, 0x2413, diagonal));
        f.Sync();
        var created = f.E.HeavenOnHighSpawnAoes.Single();
        f.Cast(12210, 3);
        f.E.Finish(3, 12210, f.Now);
        Check(f.E.HeavenOnHighSpawnAoes.Single() is { Hits: 1 } a && a.Activation == created.Activation, "Sand first Finish clock/count.");
        f.Set(3, actor => actor with { Cast = null });
        f.Sync();
        f.Cast(12210, 3);
        f.E.Finish(3, 12210, f.Now);
        Check(f.E.HeavenOnHighSpawnAoes.Single().Hits == 2, "Sand second Finish.");
        f.Set(3, actor => actor with { Cast = null });
        f.Sync();
        f.Cast(12210, 3);
        f.E.RemoveActor(3, f.Now);
        Check(f.E.HeavenOnHighSpawnAoes.Count == 0, "Sand third Finish/destroy order.");
        var shared = new Fixture(7);
        shared.Actors.AddRange([new(3, 0x2413, diagonal), new(4, 0x2413, diagonal)]);
        shared.Sync();
        shared.Cast(12210, 3);
        shared.E.Finish(3, 12210, shared.Now);
        Check(shared.E.HeavenOnHighSpawnAoes.Select(a => a.Hits).SequenceEqual([1, 0]), "Sand first matching duplicate position.");
        shared.Set(3, actor => actor with { Cast = null });
        shared.Sync();
        shared.Cast(12210, 3);
        shared.E.Finish(3, 12210, shared.Now);
        shared.Set(3, actor => actor with { Cast = null });
        shared.Sync();
        shared.Cast(12210, 3);
        shared.E.RemoveActor(3, shared.Now);
        Check(shared.E.HeavenOnHighSpawnAoes.Count == 0, "Sand Finish third first, Destroy removes next same-position entry.");
        var moved = new Fixture(7);
        moved.Actors.Add(new(3, 0x2413, diagonal));
        moved.Sync();
        moved.Set(3, a => a with { Position = diagonal + Vector3.UnitX * 2 });
        moved.Cast(12210, 3);
        moved.E.Finish(3, 12210, moved.Now);
        moved.E.RemoveActor(3, moved.Now);
        Check(moved.E.HeavenOnHighSpawnAoes.Single().Hits == 0, "Moved Sand incorrectly used ID fallback.");
        var axis = new Fixture(7);
        axis.Actors.Add(new(3, 0x2413, Center));
        axis.Sync();
        axis.Set(3, a => a with { Position = axis.E.HeavenOnHighSpawnAoes.Single().Origin + new Vector3(.09f, 0, .09f) });
        axis.Cast(12210, 3);
        axis.E.Finish(3, 12210, axis.Now);
        Check(axis.E.HeavenOnHighSpawnAoes.Single().Hits == 1, "Sand used Euclidean instead of axis tolerance.");
        var replaced = new Fixture(7);
        replaced.Actors.Add(new(3, 0x2413, Center));
        replaced.Sync();
        for (var i = 0; i < 2; ++i)
        {
            replaced.Cast(12210, 3);
            replaced.Set(3, actor => actor with { Cast = null });
            replaced.Sync();
        }
        replaced.Cast(12210, 3);
        replaced.Actors.RemoveAll(a => a.Id == 3);
        replaced.Actors.Add(new(4, 0x2413, Center));
        replaced.Now = replaced.Now.AddSeconds(1);
        replaced.Sync();
        Check(replaced.E.HeavenOnHighSpawnAoes.Single() is { ActorId: 4, Hits: 0 } replacement
            && replacement.Activation == replaced.Now.AddSeconds(3.2), "Old sphere destroy consumed this frame new sphere.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
