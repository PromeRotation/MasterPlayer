using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class TitaniaRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Titania water tower creation raw position capacity and mist finish fallback", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x1EABE5, new(85.123f, 3, 100)); f.Scan();
            var tower = Group(f, "titania/tower").Single(a => a.Display);
            Check(tower.Position == f.Actors[20].Position && tower is { Safe: true, OutlineOnly: true },
                "Tower birth lost raw position or unoccupied outline.");
            f.Actors.Remove(20); f.Now = f.Start.AddSeconds(100); f.Scan();
            Check(Group(f, "titania/tower").Any(), "Object lifetime or estimated end erased tower.");
            Start(f, 1, 15685); End(f, 1, 15685);
            Check(!Group(f, "titania/tower").Any(), "Mist finish failed to clear tower.");
        });
        run("Titania burst objects clear towers and take exclusive precedence even when all bursts are dead", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x1EABE5, new(85, 0, 100)); f.Scan();
            f.Actors[21] = new(21, 0x1E9E3C, new(115, 0, 100)); f.Scan();
            Check(!Group(f, "titania/tower").Any(), "Burst birth did not clear all original towers.");
            Start(f, 1, 15687);
            Check(Group(f, "titania/water").Single(a => a.Display).Position.X == 115, "Original water won over burst objects.");
            f.Actors[21] = f.Actors[21] with { Dead = true }; f.Scan();
            Check(!Group(f, "titania/water").Any(), "Dead burst fell back to originals contrary to source.");
        });
        run("Titania fire rune uses callback float delay exact radius display and smaller navigation", () =>
        {
            using var f = Scene(); f.Actors[20] = new(20, 0x1EABE5, new(85, 0, 100)); f.Scan();
            Start(f, 1, 15685); End(f, 1, 15685);
            f.Now = f.Start.AddSeconds(2); Start(f, 1, 15687);
            var display = Group(f, "titania/water").Single(a => a.Display);
            var join = Group(f, "titania/water/join").Single();
            Check(display.Shape is CircleTemplate { Radius: 5 } && join.Activation == f.Now.AddSeconds((double)2.7f)
                && !join.Shape.Resolve(join.Position, 0, null).Contains(new(89.4f, 0, 100))
                && join.Shape.Resolve(join.Position, 0, null).Contains(new(89.7f, 0, 100)), "Fire water geometry or clock differs.");
            f.Now = f.Start.AddSeconds(100); f.Scan(); Check(Group(f, "titania/water").Any(), "Fire ended by prediction.");
            End(f, 1, 15687); Check(!Group(f, "titania/water").Any(), "Fire finish leaked water guidance.");
        });
        run("Titania water tower occupancy supports two and excludes dead damage targets", () =>
        {
            using var f = Scene(); f.Actors[20] = new(20, 0x1EABE5, f.Actors[100].Position); f.Scan();
            f.Actors[101] = f.Actors[101] with { Position = f.Actors[100].Position }; f.Scan();
            Check(Group(f, "titania/tower").Single(a => a.Display).Safe && f.Engine.PredictedDamage.Count() == 2,
                "Two-soaker capacity differs.");
            f.Actors[102] = f.Actors[102] with { Position = f.Actors[100].Position }; f.Scan();
            Check(Group(f, "titania/tower").Single(a => a.Display) is { Safe: false, OutlineOnly: true }
                && f.Engine.Hints(f.Actors[100].Position).Contains("水塔超员"), "Own overfull tower styling/hint differs.");
            f.Actors[102] = f.Actors[102] with { Dead = true }; f.Scan();
            Check(f.Engine.PredictedDamage.Count() == 2, "Corpse counted as a tower hit.");
        });
        run("Titania roots append marker and growth crosses with dynamic first angle fixed second angle", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x1EABE4, new(100, 0, 100), Rotation: .2f); f.Scan();
            f.Engine.Animation(20, 0x00010002, f.Now); f.Render();
            Check(Group(f, "titania/roots").Length == 2 && Group(f, "titania/roots").All(a => a.Shape is DungeonCrossTemplate { Length: 10, HalfWidth: 3 }),
                "Root marker crosses missing.");
            f.Engine.Animation(20, 0x00100020, f.Now); f.Render();
            Check(Group(f, "titania/roots").Length == 4, "Growth replaced rather than appended marker crosses.");
            f.Actors[20] = f.Actors[20] with { Position = new(105, 0, 100), Rotation = .7f }; f.Scan();
            var roots = Group(f, "titania/roots");
            Check(roots.All(a => a.Position.X == 105) && roots.Count(a => a.Rotation == .7f) == 2
                && roots.Count(a => a.Rotation == MathF.PI / 4) == 2, "Root anchor/rotation binding differs.");
            var geometry = roots.First().Shape.Resolve(roots.First().Position, roots.First().Rotation, null);
            Check(HostDrawingGeometry.Convert(geometry).Contains(roots.First().Position), "World cross conversion lost center.");
        });
        run("Titania root death hides one source and all-root death clears component permanently", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x1EABE4, new(100, 0, 100));
            f.Actors[21] = new(21, 0x1EABE4, new(110, 0, 100)); f.Scan();
            f.Engine.Animation(20, 0x00100020, f.Now); f.Engine.Animation(21, 0x00100020, f.Now); f.Render();
            f.Actors[20] = f.Actors[20] with { Dead = true }; f.Scan();
            Check(Group(f, "titania/roots").Length == 2, "Dead root did not hide own active baits.");
            f.Actors[21] = f.Actors[21] with { Dead = true }; f.Scan();
            f.Actors[20] = f.Actors[20] with { Dead = false }; f.Scan();
            Check(!Group(f, "titania/roots").Any(), "Revival restored cleared all-dead root baits.");
        });
        run("Titania root snapshot eanim does not append every frame and destruction freezes old generation", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x1EABE4, new(100, 0, 100), EventAnimation: 0x00010002);
            f.Actors[21] = new(21, 0x1EABE4, new(110, 0, 100)); f.Scan(); f.Scan();
            Check(Group(f, "titania/roots").Length == 2, "Persistent eanim snapshot duplicated crosses.");
            f.Engine.RemoveActor(20); f.Actors.Remove(20); f.Scan();
            f.Actors[20] = new(20, 0x1EABE4, new(115, 0, 100)); f.Scan();
            Check(Group(f, "titania/roots").All(a => a.Position.X == 100), "Destroyed root rebound to new generation.");
        });
        run("Titania divination icon uses targetID full75 cone dynamic baits and FIFO actual effect", () =>
        {
            using var f = Scene();
            f.Engine.Icon(100, 230, f.Now, 101); f.Render();
            var bait = Group(f, "titania/bait/15707").Single(a => a.Display);
            Check(bait.Shape is FanTemplate { Radius: 60 } && bait.Rotation > 1.5f
                && f.Engine.PredictedDamage.Single() is { TargetId: 100, DamageType: "Raidwide", Activation: var at }
                && at == f.Start.AddSeconds(5.1), "Divination source/target, default damage type or clock differs.");
            f.Engine.Icon(101, 230, f.Now); f.Render();
            f.Engine.Effect(1, 15707, 1, f.Now, 101); f.Render();
            Check(Group(f, "titania/bait/15707").Count(a => a.Display) == 1
                && f.Engine.PredictedDamage.Single().TargetId == 101, "Divination failed FIFO consumption.");
        });
        run("Titania icon fire stack has three-second fixed clock radius five finite eight capacity and individual resolution", () =>
        {
            using var f = Scene(); f.Engine.Icon(101, 62, f.Now); f.Engine.Icon(102, 62, f.Now); f.Render();
            Check(Group(f, "titania/mark/17267").Count(a => a.Display) == 2
                && Group(f, "titania/mark/17267/join").Single().Activation == f.Start.AddSeconds(3)
                && !f.Engine.PredictedDamage.Any(), "Flame icon stack radius/clock or source prediction differs.");
            f.Engine.Effect(10, 17267, 1, f.Now, 102); f.Render();
            Check(Group(f, "titania/mark/17267").Count(a => a.Display) == 1, "Flame effect consumed all markers.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            Check(!Group(f, "titania/mark/17267").Any(), "Dead stack target remained active.");
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan();
            Check(Group(f, "titania/mark/17267").Any(), "Cast-independent stack failed to return on revival.");
        });
        run("Titania icon bait death is permanent and one-person tower still asks an outsider to join", () =>
        {
            using var f = Scene();
            f.Engine.Icon(101, 230, f.Now); f.Render();
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan();
            Check(!Group(f, "titania/bait/15707").Any() && !f.Engine.PredictedDamage.Any(),
                "Dead icon target revived a permanently discarded bait.");
            f.Actors[20] = new(20, 0x1EABE5, f.Actors[101].Position); f.Scan();
            Check(f.Engine.Hints(f.Actors[100].Position).Contains("进入水塔"), "One-person tower lost outsider join hint.");
        });
        run("Titania corrected knockback cast returns to FIFO tail", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Position = new(100, 0, 100) }; f.Scan();
            var a = new DungeonCastObservation(15705, new(109, 0, 100), 0, f.Now, f.Now.AddSeconds(5));
            var b = a with { Location = new(100, 0, 110) };
            f.Engine.StartCast(10, a, f.Now); f.Engine.StartCast(11, b, f.Now);
            f.Engine.StartCast(10, a with { Location = new(110, 0, 100) }, f.Now); f.Render();
            var landing = Group(f, "titania/knockback/10").Single(x => x.Id.EndsWith("/landing"));
            Check(Vector3.Distance(landing.Position, new(92.92893f, 0, 82.92893f)) < .01f,
                "Correction kept the old FIFO slot.");
        });
        run("Titania two cast spreads and cast stack remain independent and finish by target", () =>
        {
            using var f = Scene(); Start(f, 10, 16927, target: 101); Start(f, 11, 15668, target: 102);
            Check(Group(f, "titania/mark/16927").Single(a => a.Display).Shape is CircleTemplate { Radius: 8 }
                && Group(f, "titania/mark/16927").Single(a => a.Navigate).Shape is CircleTemplate { Radius: 9 }
                && Group(f, "titania/mark/15668").Single(a => a.Display).Shape is CircleTemplate { Radius: 6 },
                "Separate cast spreads lost exact radii.");
            f.Engine.Effect(10, 16927, 1, f.Now, 101); f.Render();
            Check(Group(f, "titania/mark/16927").Any(), "Cast spread incorrectly ended by effect.");
            End(f, 10, 16927); Check(Group(f, "titania/mark/15668").Any(), "One finish erased independent component.");
            End(f, 11, 15668); Start(f, 10, 15703, target: 101);
            Check(Group(f, "titania/mark/15703/join").Any() && !f.Engine.PredictedDamage.Any(), "Cast stack or source no-prediction differs.");
        });
        run("Titania war pease follows dead targets while allowing only actual source ending", () =>
        {
            using var f = Scene(); Start(f, 10, 15789, target: 101);
            f.Actors[101] = f.Actors[101] with { Dead = true, Position = new(115, 0, 100) }; f.Scan();
            Check(Group(f, "titania/bait/15789").Single(a => a.Display).Position.X == 115
                && f.Engine.PredictedDamage.Single().TargetId == 101, "Dynamic dead bait was hidden.");
            f.Now = f.Start.AddSeconds(100); f.Scan(); Check(Group(f, "titania/bait/15789").Any(), "Timeout erased bait.");
            f.Engine.RemoveActor(10); f.Actors.Remove(10); f.Scan();
            Check(!Group(f, "titania/bait/15789").Any(), "Source destruction retained cast bait.");
        });
        run("Titania ten-yard knockback uses immediate 5.1 to9.8 safe ring immunity and FIFO", () =>
        {
            using var f = Scene(); Start(f, 10, 15705);
            var join = Group(f, "titania/knockback/join").Single();
            var geometry = join.Shape.Resolve(join.Position, 0, null);
            Check(join.Activation == null && !geometry.Contains(new(106, 0, 100)) && geometry.Contains(new(104, 0, 100))
                && geometry.Contains(new(110, 0, 100)) && Group(f, "titania/knockback").Any(a => a.Id.EndsWith("/landing")),
                "Default-time safety donut or trajectory differs.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(30) } }; f.Scan();
            Check(!Group(f, "titania/knockback").Any(), "Immunity failed to gate both custom ring and trajectory.");
        });
        run("Titania simple casts dual view exact widths independent real finishes raidwides and add priority", () =>
        {
            using var f = Scene(); Start(f, 10, 16259); Start(f, 11, 15701);
            var geometry = f.Draw.Previews["cast/10"].Geometry;
            Check(geometry.Contains(new(103.9f, 0, 159)) && HostDrawingGeometry.Convert(geometry).Contains(new(103.9f, 0, 159))
                && !geometry.Contains(new(104.1f, 0, 159)), "GentleBreeze full width eight differs.");
            End(f, 10, 16259); Check(f.Draw.Previews.ContainsKey("cast/11"), "Independent fan finish lost.");
            End(f, 11, 15701);
            foreach (var action in new[] { 15708u, 15702u, 15697u })
            { Start(f, 10, action); Check(f.Engine.PredictedDamage.Count() == 3, "Raidwide missing."); End(f, 10, action); }
            f.Actors[20] = new(20, 0x27F0, new(100, 0, 100)); f.Scan();
            Check(f.Engine.PriorityTarget(1, f.Actors[100].Position) == 20, "Add priority not migrated.");
        });
        run("Titania source placeholders do not invent shields or unsupported icon mechanics and death clears", () =>
        {
            using var f = Scene(); f.Engine.Icon(100, 139, f.Now); f.Engine.Icon(100, 189, f.Now); f.Engine.Icon(100, 161, f.Now); f.Render();
            Check(!Group(f, "titania/mark").Any() && f.Engine.ForbiddenTargets.Length == 0
                && ArrDungeonCatalog.IsScript("masterplayer.trial.titania"), "Invented unsupported mechanics or missing ownership.");
            f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Now = f.Start.AddSeconds(10001); f.Scan();
            Check(f.Engine.Active != null, "Hidden primary or display duration ended trivial phase.");
            Start(f, 10, 15709); f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Death leaked output.");
        });
        run("Titania destroyed raw caster cannot restore bait or knockback before object removal", () =>
        {
            using var f = Scene(); Start(f, 10, 15789, target: 101); Start(f, 11, 15705);
            f.Engine.RemoveActor(10); f.Engine.RemoveActor(11); f.Scan();
            Check(!Group(f, "titania/bait/15789").Any() && !Group(f, "titania/knockback").Any(),
                "Stale raw casts resurrected destroyed generation.");
        });
        run("Titania occupied correct tower suppresses other tower join and stack markers must separate", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x1EABE5, f.Actors[100].Position);
            f.Actors[21] = new(21, 0x1EABE5, new(85, 0, 100)); f.Scan();
            Check(!f.Engine.Hints(f.Actors[100].Position).Contains("进入水塔"), "Already-soaking player was told to leave for another tower.");
            for (var i = 0; i < 8; ++i)
                f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, new(105, 0, 100), PartySlot: i, Hostile: false);
            f.Scan(); f.Engine.Icon(100, 62, f.Now); f.Engine.Icon(101, 62, f.Now); f.Render();
            Check(f.Engine.Hints(f.Actors[100].Position).Contains("八人火焰分摊"), "Overlapping marked stacks escaped source error hint.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x27ED, 845); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c };
        f.Actors[100] = new(100, 0, c + Vector3.UnitX * 5, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitZ * 10, PartySlot: 1, Hostile: false);
        f.Actors[102] = new(102, 0, c - Vector3.UnitX * 10, PartySlot: 2, Hostile: false);
        f.Actors[10] = new(10, 0x233C, c); f.Actors[11] = new(11, 0x233C, c); f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, 0, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
