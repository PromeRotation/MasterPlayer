using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class YojimboRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Yojimbo birth explosions keep independent clocks quantization and component-wide actual finish", () =>
        {
            using var f = Scene();
            f.Now = f.Start.AddSeconds(2);
            f.Engine.ObjectCreated(20, f.Start); f.Engine.ObjectCreated(21, f.Start.AddSeconds(.1));
            f.Actors[20] = new(20, 0x25FE, new(100, 3, 100));
            f.Actors[21] = new(21, 0x2603, new(110, 3, 100)); f.Scan();
            var born = Group(f, "yojimbo/birth");
            Check(born.Length == 2 && born.Single(a => a.Id.EndsWith("/20")).Activation == f.Start.AddSeconds(4.85)
                && born.Single(a => a.Id.EndsWith("/21")).Activation == f.Start.AddSeconds(.1).AddSeconds((double)6.7f),
                "Creation clock replaced by discovery time.");
            Check(born.Single(a => a.Id.EndsWith("/21")).Position == f.Actors[21].Position
                && born.Single(a => a.Id.EndsWith("/20")).Position != f.Actors[20].Position, "Birth quantization crossed components.");
            f.Actors.Remove(20); f.Scan();
            Check(Group(f, "yojimbo/birth").Length == 2, "Destroy erased stored explosion.");
            Start(f, 10, 14378, new(100, 0, 100)); End(f, 10, 14378);
            Check(Group(f, "yojimbo/birth").Single().Id.EndsWith("/21"), "Fragility finish crossed electrogenetic component.");
            Start(f, 10, 14400, new(100, 0, 100)); End(f, 10, 14400);
            Check(!Group(f, "yojimbo/birth").Any(), "Actual electrogenetic finish leaked.");
        });
        run("Yojimbo dragon twenty steps use timer sequence not action effects or object lifetime", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x2601, new(100, 0, 80), Rotation: 0); f.Scan();
            Check(Group(f, "yojimbo/dragon").Length == 20, "Full dragon preview missing.");
            var first = Group(f, "yojimbo/dragon").Single(a => a.Id.EndsWith("/0"));
            Check(first.Activation == f.Start.AddSeconds(5.7) && first.Shape is RayRectangleTemplate,
                "First dragon clock/shape differs.");
            f.Engine.Effect(20, 14392, 1, f.Now); f.Render(); Check(Group(f, "yojimbo/dragon").Length == 20, "Raidwide effect advanced timer sequence.");
            f.Now = f.Start.AddSeconds(5.7); f.Scan();
            var next = Group(f, "yojimbo/dragon").Single(a => a.Id.EndsWith("/0"));
            Check(Group(f, "yojimbo/dragon").Length == 19 && MathF.Abs(next.Position.Z - first.Position.Z - 3) < .04f,
                "First timer step length differs.");
            f.Actors.Remove(20); f.Scan();
            Check(Group(f, "yojimbo/dragon").Length == 19, "Destroy erased independent line.");
            f.Now = f.Start.AddSeconds(100); f.Scan();
            Check(Group(f, "yojimbo/dragon").Length == 18, "Late frame caught up multiple steps instead of one source update.");
            for (var i = 0; i < 18; ++i) { f.Now = f.Now.AddSeconds(.6); f.Scan(); }
            Check(!Group(f, "yojimbo/dragon").Any(), "Twentieth explosion retained playable AOE.");
        });
        run("Yojimbo kasha float delay fixed location all clear and no timeout", () =>
        {
            using var f = Scene(); Start(f, 10, 14388, new(100, 0, 100));
            f.Now = f.Start.AddSeconds(1); Start(f, 11, 14388, new(110, 0, 100));
            var first = Group(f, "yojimbo/kasha").First();
            Check(first.Activation == f.Start.AddSeconds((double)4.164f), "Kasha delay lost float conversion.");
            var geometry = first.Shape.Resolve(first.Position, first.Rotation, null);
            Check(!geometry.Contains(new(102.8f, 0, 100)) && geometry.Contains(new(103, 0, 100)), "Kasha inner radius differs.");
            f.Now = f.Start.AddSeconds(100); f.Scan(); Check(Group(f, "yojimbo/kasha").Length == 2, "Prediction expiry cleared Kasha.");
            End(f, 11, 14388); Check(!Group(f, "yojimbo/kasha").Any(), "One actual finish did not clear component.");
        });
        run("Yojimbo gekko dynamic clock overlap gate embodiment loss and actual action clear", () =>
        {
            using var f = Scene();
            f.Actors[101] = f.Actors[101] with { Position = f.Actors[100].Position + Vector3.UnitX }; f.Scan();
            f.Engine.Icon(100, 144, f.Now); f.Engine.Icon(101, 144, f.Now); f.Render();
            Check(Group(f, "yojimbo/gekko").Count(a => a.Navigate) == 2, "Overlapping marked targets failed.");
            var at = Group(f, "yojimbo/gekko").First(a => a.Navigate).Activation;
            f.Now = f.Start.AddSeconds(1); f.Scan();
            Check(Group(f, "yojimbo/gekko").First(a => a.Navigate).Activation == at!.Value.AddSeconds(1), "Rolling Gekko activation became icon-fixed.");
            Start(f, 10, 14386, new(100, 0, 100), seconds: 3);
            Check(Group(f, "yojimbo/gekko").All(a => !a.Navigate), "Yukikaze suppression gate missing.");
            End(f, 10, 14386);
            f.Now = f.Start.AddSeconds(7.79); f.Scan(); Check(Group(f, "yojimbo/gekko").All(a => !a.Navigate), "Yukikaze gate ended before finish+3.5.");
            f.Now = f.Start.AddSeconds(7.81); f.Scan(); Check(Group(f, "yojimbo/gekko").Any(a => a.Navigate), "Yukikaze gate did not release.");
            f.Actors[2] = f.Actors[2] with { Dead = true }; f.Scan(); Check(!Group(f, "yojimbo/gekko").Any(), "All embodiments dead did not clear Gekko.");
        });
        run("Yojimbo tiny marked partners only fixed first clock strict expiry", () =>
        {
            using var f = Scene();
            f.Engine.Icon(100, 55, f.Now); f.Now = f.Start.AddSeconds(1); f.Engine.Icon(101, 55, f.Now); f.Render();
            Check(Group(f, "yojimbo/tiny/join").Single().Activation == f.Start.AddSeconds(5)
                && Group(f, "yojimbo/tiny").Single(a => a.Display).Shape is CircleTemplate { Radius: 1 },
                "Tiny first activation/radius differs.");
            f.PlayerId = 102; f.Scan(); Check(!Group(f, "yojimbo/tiny").Any(), "Unmarked player entered marked partner requirement.");
            f.PlayerId = 100; f.Now = f.Start.AddSeconds(5); f.Scan(); Check(Group(f, "yojimbo/tiny").Any(), "Tiny ended at equality instead of strictly later.");
            f.Now = f.Start.AddSeconds(5.01); f.Scan(); Check(!Group(f, "yojimbo/tiny").Any(), "Tiny estimated icon lifetime failed.");
        });
        run("Yojimbo giga jump independent icon group fixed six radius25 navigation26 real clear", () =>
        {
            using var f = Scene();
            f.Engine.Icon(101, 87, f.Now); f.Render();
            Check(Group(f, "yojimbo/jump").Single(a => a.Display).Shape is CircleTemplate { Radius: 25 }
                && Group(f, "yojimbo/jump").Single(a => a.Navigate) is { Shape: CircleTemplate { Radius: 26 }, Activation: var at }
                && at == f.Start.AddSeconds(6) && f.Engine.PredictedDamage.Single().TargetId == 101, "Jump semantics differ.");
            f.Engine.Icon(100, 55, f.Now); f.Engine.Icon(101, 55, f.Now); f.Render();
            f.Engine.Effect(2, 14396, 1, f.Now); f.Render();
            Check(!Group(f, "yojimbo/jump").Any() && Group(f, "yojimbo/tiny").Any(), "Jump resolution crossed Tiny.");
        });
        run("Yojimbo storm locks actual caster pose side offsets colors and independent fixed expiry", () =>
        {
            using var f = Scene();
            f.Actors[1] = f.Actors[1] with { Rotation = MathF.PI / 2 }; f.Scan();
            Start(f, 1, 14405, new(200, 0, 200), rotation: 0, seconds: 10);
            var center = Group(f, "yojimbo/storm").Single();
            Check(center.Activation == f.Start.AddSeconds(5) && center.Rotation == MathF.PI / 2
                && center.Shape.Resolve(center.Position, center.Rotation, null).Contains(new(139.9f, 0, 100))
                && center.Shape.Resolve(center.Position, center.Rotation, null).Contains(new(99.1f, 0, 100)),
                "Storm followed spell location/rotation or lost length back.");
            f.Now = f.Start.AddSeconds(1); Start(f, 10, 14409, new(0, 0, 0), seconds: 6);
            var sides = Group(f, "yojimbo/storm").Where(a => a.Shape is RayRectangleTemplate { MinLocalX: -10 }).ToArray();
            Check(sides.Length == 2, "Side rectangles missing.");
            Check(sides.All(a => a.Activation == f.Start.AddSeconds(7)), "Side TotalTime clock differs: " + string.Join(",", sides.Select(a => (a.Activation - f.Start)?.TotalSeconds)));
            Check(sides.All(a => a.Color != null) && MathF.Abs(sides[0].Position.Z - sides[1].Position.Z) == 21,
                "Side locked offset/color differs.");
            f.Now = f.Start.AddSeconds(5); f.Scan();
            Check(Group(f, "yojimbo/storm").Length == 2 && Group(f, "yojimbo/storm").All(a => a.Color == null), "Central expiry did not promote sides.");
            f.Now = f.Start.AddSeconds(7); f.Scan(); Check(!Group(f, "yojimbo/storm").Any(), "Side fixed deadline did not clear.");
        });
        run("Yojimbo storm actual finish early only clears corresponding component", () =>
        {
            using var f = Scene(); Start(f, 1, 14405, new(100, 0, 100)); Start(f, 10, 14409, new(100, 0, 100));
            End(f, 1, 14405); Check(Group(f, "yojimbo/storm").Length == 2, "Storm finish cleared side predictions.");
            End(f, 10, 14409); Check(!Group(f, "yojimbo/storm").Any(), "Side finish leaked.");
        });
        run("Yojimbo chain hints source player only pending raw and corpse display cleanup", () =>
        {
            using var f = Scene();
            f.Engine.Tether(1, 57, true, f.Now, 100); f.Render();
            var avoid = Group(f, "yojimbo/enchain").Single();
            Check(avoid.Shape is CircleTemplate { Radius: 20 } && avoid.Activation == DateTime.MaxValue
                && f.Engine.Hints(f.Actors[100].Position).Contains("即将被束缚"), "Enchain pending movement/hint differs.");
            f.Now = f.Start.AddSeconds(3); f.Scan(); Check(Group(f, "yojimbo/enchain").Any(), "Stale raw erased tether.");
            f.Actors[100] = f.Actors[100] with { Tethers = new Dictionary<uint, ulong> { [57] = 1, [81] = 1 } };
            f.Actors[20] = new(20, 0x2602, new(100, 0, 100)); f.Scan();
            f.PlayerId = 101; f.Scan();
            Check(f.Engine.Hints(f.Actors[101].Position).Contains("击破铁锁解救队友") && Group(f, "yojimbo/chain").Length == 2
                && f.Engine.PriorityTarget(1, f.Actors[101].Position) == 20, "Bound rescue, chain drawing or priority missing.");
            f.Actors[20] = f.Actors[20] with { Dead = true }; f.Scan(); Check(Group(f, "yojimbo/chain").Length == 1, "Chain corpse must keep only the range outline.");
            f.Engine.Tether(1, 81, false, f.Now, 100); f.Render(); Check(!Group(f, "yojimbo/chain").Any(), "Untether did not prune dead chain.");
            f.Actors.Remove(100); f.Scan(); f.PlayerId = 100; f.Render();
            Check(!Group(f, "yojimbo/enchain").Any(), "Destroyed player retained enchain.");
        });
        run("Yojimbo destruction waits for raw removal and prunes chain corpses only with bound targets", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x2602, new(100, 0, 100));
            f.Actors[21] = new(21, 0x25FE, new(105, 0, 100)); f.Scan();
            f.Actors[20] = f.Actors[20] with { Dead = true }; f.Scan();
            f.Engine.RemoveActor(21); f.Scan();
            Check(Group(f, "yojimbo/birth").Length == 1 && Group(f, "yojimbo/chain").Length == 1,
                "Stale raw recreated birth or unrelated destruction pruned an unbound corpse.");
            f.Actors.Remove(21); f.Scan();
            f.Actors[21] = new(21, 0x25FE, new(110, 0, 100)); f.Scan();
            Check(Group(f, "yojimbo/birth").Length == 2, "Real new lifecycle was not observed.");
        });
        run("Yojimbo generation keys preserve simultaneous stored explosions in both output views", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x25FE, new(100, 0, 100));
            f.Actors[21] = new(21, 0x2601, new(100, 0, 80)); f.Scan();
            f.Engine.RemoveActor(20); f.Engine.RemoveActor(21);
            f.Actors.Remove(20); f.Actors.Remove(21); f.Scan();
            f.Actors[20] = new(20, 0x25FE, new(110, 0, 100));
            f.Actors[21] = new(21, 0x2601, new(110, 0, 80)); f.Scan();
            var aoes = f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("yojimbo/birth/")
                || a.Id.StartsWith("yojimbo/dragon/")).ToArray();
            Check(aoes.Length == 42 && aoes.Select(a => a.Id).Distinct().Count() == 42,
                "Actor ID reuse collided with stored predictions.");
            Check(f.Draw.Previews.Keys.Count(id => id.Contains("yojimbo/birth/") || id.Contains("yojimbo/dragon/")) == 42
                && f.Snapshot.DangerAreas.Count == 42, "Final drawing or navigation overwrote an older generation.");
        });
        run("Yojimbo destroyed players cannot restore stale raw tethers and player removal cannot prune chains", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Tethers = new Dictionary<uint, ulong> { [57] = 1, [81] = 1 } };
            f.Actors[20] = new(20, 0x2602, new(100, 0, 100)); f.Scan();
            f.Actors[20] = f.Actors[20] with { Dead = true }; f.Scan();
            f.Engine.RemoveActor(101); f.Scan();
            Check(Group(f, "yojimbo/chain").Length == 1, "Unrelated player destruction pruned a corpse.");
            f.Engine.RemoveActor(100); f.Scan();
            Check(!Group(f, "yojimbo/enchain").Any() && !Group(f, "yojimbo/chain").Any(),
                "Stale raw tether was restored or implicit untether failed to prune.");
            f.Actors.Remove(100); f.Scan();
            f.Actors[100] = new(100, 0, new(90, 0, 100), PartySlot: 0, Hostile: false); f.Scan();
            f.Actors[21] = new(21, 0x2602, new(110, 0, 100)); f.Scan();
            f.Actors[21] = f.Actors[21] with { Dead = true }; f.Scan();
            f.Engine.Tether(1, 81, true, f.Now, 100); f.Render();
            f.Engine.RemoveActor(11); f.Render();
            Check(!Group(f, "yojimbo/chain").Any(), "Enemy destruction with a bound player failed to prune.");
        });
        run("Yojimbo destroyed chain snapshots never follow a later actor generation", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x2602, new(100, 0, 100)); f.Scan();
            f.Engine.RemoveActor(20); f.Actors.Remove(20); f.Scan();
            f.Actors[20] = new(20, 0x2602, new(110, 0, 100)); f.Scan();
            var chains = Group(f, "yojimbo/chain");
            Check(chains.Length == 3 && chains.Count(a => a.Position.X == 100) == 1
                && chains.Count(a => a.Position.X == 110) == 2 && chains.Select(a => a.Id).Distinct().Count() == 3,
                "Destroyed chain rebound or output IDs collided.");
            f.Engine.Tether(1, 81, true, f.Now, 100); f.Engine.Tether(1, 81, false, f.Now, 100); f.Render();
            Check(Group(f, "yojimbo/chain").Length == 2, "Untether did not prune only the destroyed generation.");
        });
        run("Yojimbo jump death is permanent while destroyed tiny and jump keep the old actor position", () =>
        {
            using var f = Scene();
            f.Engine.Icon(100, 55, f.Now); f.Engine.Icon(101, 55, f.Now);
            f.Engine.Icon(101, 87, f.Now); f.Render();
            var old = Group(f, "yojimbo/jump").First(a => a.Display).Position;
            f.Engine.RemoveActor(101); f.Actors.Remove(101); f.Scan();
            f.Actors[101] = new(101, 0, new(120, 0, 120), PartySlot: 1, Hostile: false); f.Scan();
            Check(Group(f, "yojimbo/jump").First(a => a.Display).Position == old
                && Group(f, "yojimbo/tiny").First(a => a.Display).Position == new Vector3(110, 0, 100),
                "Old marks rebound to a new actor with the same ID.");
            f.Engine.Effect(2, 14398, 1, f.Now);
            f.Engine.Icon(101, 87, f.Now); f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan();
            Check(!Group(f, "yojimbo/jump").Any() && !f.Engine.PredictedDamage.Any(),
                "Revival restored a removed jump mark.");
        });
        run("Yojimbo restored casts start fixed delay at first observation not historical spell start", () =>
        {
            using var f = Scene(); f.Now = f.Start.AddSeconds(2);
            var kasha = new DungeonCastObservation(14388, new(100, 0, 100), 0, f.Start, f.Start.AddSeconds(5.3));
            f.Actors[10] = f.Actors[10] with { Cast = kasha };
            f.Engine.StartCast(10, kasha, f.Now); f.Render();
            Check(Group(f, "yojimbo/kasha").Single().Activation == f.Now.AddSeconds((double)4.164f),
                "Restored Kasha used historical spell start.");
            var storm = kasha with { Action = 14405 };
            f.Actors[1] = f.Actors[1] with { Cast = storm }; f.Engine.StartCast(1, storm, f.Now); f.Render();
            Check(Group(f, "yojimbo/storm").Single().Activation == f.Now.AddSeconds(5),
                "Restored Storm used historical spell start.");
        });
        run("Yojimbo charge exact dynamic endpoint simple cast dual view raidwide ownership and death", () =>
        {
            using var f = Scene(); Start(f, 10, 14403, new(100, 0, 115));
            var charge = Group(f, "yojimbo/charge").Single();
            var geometry = charge.Shape.Resolve(charge.Position, charge.Rotation, null);
            Check(geometry.Contains(new(103.9f, 0, 114.9f)) && HostDrawingGeometry.Convert(geometry).Contains(new(103.9f, 0, 114.9f))
                && !geometry.Contains(new(104.1f, 0, 114.9f)) && !geometry.Contains(new(100, 0, 115.1f)), "Charge geometry differs.");
            End(f, 10, 14403); Start(f, 10, 14386, new(100, 0, 100));
            Check(f.Draw.Previews["cast/10"].Geometry.Contains(new(101.9f, 0, 144.4f)), "Yukikaze dimension differs.");
            End(f, 10, 14386);
            foreach (var action in new[] { 14376u, 14397u, 14404u })
            {
                Start(f, 10, action, new(100, 0, 100)); Check(f.Engine.PredictedDamage.Count() == 3, "Raidwide omitted.");
                End(f, 10, action);
            }
            Check(ArrDungeonCatalog.IsScript("masterplayer.trial.yojimbo") && f.Engine.ForbiddenTargets.Length == 0, "Ownership or invented shield.");
            f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Now = f.Start.AddSeconds(10001); f.Scan();
            Check(f.Engine.Active != null, "Hidden primary ended trivial phase.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan(); Check(f.Engine.Active == null && f.Draw.Previews.Count == 0, "Death leaked output.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x25FD, 806);
        f.Actors[1] = f.Actors[1] with { Position = new(100, 0, 100) };
        f.Actors[2] = new(2, 0x25FF, new(100, 0, 100));
        f.Actors[100] = new(100, 0, new(90, 0, 100), PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, new(110, 0, 100), PartySlot: 1, Hostile: false);
        f.Actors[102] = new(102, 0, new(100, 0, 110), PartySlot: 2, Hostile: false);
        f.Actors[10] = new(10, 0x233C, new(100, 0, 100)); f.Actors[11] = new(11, 0x233C, new(100, 0, 100));
        f.Scan(); return f;
    }
    private static void Start(Fight f, ulong source, uint action, Vector3 location, float rotation = 0, double seconds = 5)
    {
        var cast = new DungeonCastObservation(action, location, rotation, f.Now, f.Now.AddSeconds(seconds + .3));
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
