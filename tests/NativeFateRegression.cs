using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeFateRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly Vector3 Center = new(279, 390, 249);
    private static DungeonActor Boss(bool combat = true) => new(1, 0x27AD, Center, InCombat: combat, Hp: 1000,
        IsEnemy: true, ObjectIndex: 2, AggroPlayer: combat, FateId: 1432);
    private static DungeonActor Player(int slot = 0, Vector3? position = null) => new((ulong)(100 + slot), 0, position ?? Center,
        Hostile: false, PartySlot: slot, Player: true);
    private static DungeonEncounter Encounter(params DungeonActor[] other)
    {
        var e = new DungeonEncounter(818) { AllowedFateId = 1432 };
        e.Synchronize([Boss(), Player(), .. other], Center, Start, playerId: 100);
        return e;
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native fate Archaeotania exact territory ownership square strict pull latch HP0 generations and disabled", Lifecycle);
        run("native fate Archaeotania ten source shapes helper cast clocks real finish cancellation and stale generations", Casts);
        run("native fate Archaeotania live twister R5 hard obstacle dead only filtering and motion", Twisters);
        run("native fate Archaeotania icon Source identity duplicate stack original time helper fallback and main target effect", Stacks);
        run("native fate Archaeotania minimum eight unlimited maximum self buddy joins display colors and no added prediction", StackNavigation);
        run("native fate Archaeotania TidalWave48 forward ordered locked model immunity actual finish display only", Knockbacks);
        run("native fate Archaeotania source adds priority zero pending dead FATE metadata and inactive action boundaries", Priority);
        run("native fate Archaeotania raw cast fields relock while actor motion preserves locked geometry", RawCorrections);
        run("native fate Archaeotania zero sequence main targets independent receipts and destroyed target generation", StackGenerations);
        run("native fate Archaeotania packet first raw unknown action handoffs retain baseline without stale resurrection", RawHandoffs);
    }
    private static void Lifecycle()
    {
        Check(NativeFateCatalog.Owns(818, 0x27AD) && !NativeFateCatalog.Owns(1432, 0x27AD)
            && !NativeFateCatalog.Owns(818, 0x2AA0), "Fate ID confused with territory or helper ownership.");
        var e = new DungeonEncounter(818);
        e.Synchronize([Boss(), Player(position: Center + new Vector3(30, 0, 0))],
            Center + new Vector3(30, 0, 0), Start, playerId: 100);
        Check(e.Active == null && e.Arena == null && e.AmbientAoes().All(a => !a.Navigate), "Strict 30y pull activated at equality.");
        e.Synchronize([Boss(), Player()], Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active != null && e.Arena!.Contains(Center + new Vector3(29, 0, 29))
            && !e.Arena.Contains(Center + new Vector3(29.01f, 0, 0)), "Exact 29y square changed.");
        e.Synchronize([Boss(false) with { Targetable = false }, Player()], Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active != null, "Deaggro or untargetable reset the source encounter.");
        e.Synchronize([Boss() with { Hp = 0 }, Player()], Center, Start.AddSeconds(3), playerId: 100);
        e.Synchronize([Boss(), Player()], Center, Start.AddSeconds(4), playerId: 100);
        Check(e.Active == null && !e.AmbientAoes().Any(), "Completed actor resurrected without a new generation.");
        e.RemoveActor(1);
        e.Synchronize([Boss(), Player()], Center, Start.AddSeconds(5), playerId: 100);
        Check(e.Active != null, "Actual same-ID new generation stayed blocked.");
        e.Synchronize([Boss(), Player()], Center, Start.AddSeconds(6), _ => false, 100);
        Check(e.Active == null && e.Arena == null && !e.AmbientAoes().Any(), "Disabled Fate retained outputs.");
        e = new DungeonEncounter(818);
        e.Synchronize([Boss() with { Hp = 0 }, Player() with { Dead = true }], Center, Start, playerId: 100);
        e.Synchronize([Boss(), Player()], Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active == null, "Initial dead viewer lost primary HP0 completion barrier.");
    }
    private static void Casts()
    {
        var shapes = NativeFateCatalog.Create(818)[0].Casts.Where(c => c.Shape != null).ToArray();
        Check(shapes.Length == 10 && shapes.Single(c => c.Action == 16442).Shape is CircleTemplate { Radius: 30 }
            && shapes.Single(c => c.Action == 17042).Shape is RayRectangleTemplate { Length: 13.2f }
            && !shapes.Any(c => c.Action is 16446 or 16451 or 18121 or 18122 or 18123 or 16775), "Active rule set differs from source.");
        foreach (var rule in shapes)
        {
            var helper = new DungeonActor(10, 0x233C, Center + new Vector3(10, 0, 0));
            var e = Encounter(helper);
            var cast = new DungeonCastObservation(rule.Action, Center, .25f, Start, Start.AddSeconds(3));
            e.StartCast(10, cast, Start);
            e.Tick(Start.AddSeconds(20));
            Check(e.Casts.Count == 1 && e.Casts[10].Location == Center && e.Casts[10].FinishAt == Start.AddSeconds(3),
                "Generic Fate cast expired by clock or followed helper position.");
            e.Finish(10, rule.Action, completed: false);
            e.StartCast(10, cast, Start.AddSeconds(20));
            Check(e.Casts.Count == 0, "Cancelled generation resurrected from old packet.");
            var next = cast with { StartedAt = Start.AddSeconds(21), FinishAt = Start.AddSeconds(24) };
            e.StartCast(10, next, Start.AddSeconds(21));
            Check(e.Casts.Count == 1, "Real next generation was rejected.");
            e.Finish(10, rule.Action);
            Check(e.Casts.Count == 0, "True Finish retained Fate AOE.");
        }
    }
    private static void Twisters()
    {
        var wind = new DungeonActor(10, 0x27B2, Center, Targetable: false, EventState: 7);
        var e = Encounter(wind);
        Check(e.Hints(Center).Contains("GTFO from voidzone!"), "Twister omitted source voidzone warning.");
        var aoe = e.AmbientAoes().Single(a => a.Id.StartsWith("fate/twister/"));
        Check(aoe.Obstacle && aoe.Navigate && aoe.Shape is CircleTemplate { Radius: 5 }, "Twister became a timed danger or filtered state7.");
        e.Synchronize([Boss(), Player(), wind with { Position = Center + Vector3.UnitX * 4 }], Center, Start.AddSeconds(1), playerId: 100);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("fate/twister/")).Position == Center + Vector3.UnitX * 4, "Twister stopped following live position.");
        e.Synchronize([Boss(), Player(), wind with { Dead = true }], Center, Start.AddSeconds(2), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("fate/twister/")), "Dead twister retained obstacle.");
        e.Synchronize([Boss(), Player(), wind], Center, Start.AddSeconds(3), playerId: 100);
        Check(e.AmbientAoes().Any(a => a.Id.StartsWith("fate/twister/")), "Revived twister stayed hidden.");
    }
    private static void Stacks()
    {
        var primaryCast = new DungeonCastObservation(99999, Center, 0, Start, Start.AddSeconds(1));
        var target = Player(1, Center + Vector3.UnitX * 10);
        var boss = Boss() with { Cast = primaryCast };
        var e = Encounter(target);
        e.Synchronize([boss, Player(), target], Center, Start, playerId: 100);
        e.Icon(100, 62, Start, actorId: 101);
        e.Icon(100, 62, Start.AddSeconds(1), actorId: 101);
        e.Synchronize([boss, Player(), target], Center, Start.AddSeconds(20), playerId: 100);
        var displays = e.AmbientAoes().Where(a => a.Id.StartsWith("fate/stack/") && a.Display).ToArray();
        const float grid = 2000f / 65535f;
        var quantized = new Vector3(((int)MathF.Round(target.Position.X * (1f / grid)) - .5f) * grid, target.Position.Y,
            ((int)MathF.Round(target.Position.Z * (1f / grid)) - .5f) * grid);
        Check(displays.Length == 2 && displays[0].Position == quantized && displays[0].Activation == Start.AddSeconds(5)
            && displays[1].Activation == Start.AddSeconds(6), "Stack target changed to icon Target or timed out during another boss cast.");
        var helper = new DungeonActor(20, 0x2AA0, Center, TargetId: 101);
        e.Synchronize([Boss(), Player(), target, helper], Center, Start.AddSeconds(21), playerId: 100);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("fate/stack/") && a.Display) == 2, "Living helper failed fallback retention.");
        e.Effect(20, 17483, 1, Start.AddSeconds(22), target: 100);
        Check(e.AmbientAoes().Any(a => a.Id.StartsWith("fate/stack/")), "Wrong main target cleared stacks.");
        e.Effect(20, 17483, 2, Start.AddSeconds(23), target: 101);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("fate/stack/")), "Matching main target did not remove every matching stack.");
        e.Icon(100, 62, Start.AddSeconds(24), actorId: 101);
        e.Synchronize([Boss(), Player(), target, helper with { Dead = true }], Center, Start.AddSeconds(25), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("fate/stack/")), "Dead last helper did not clear stack after primary stopped casting.");
    }
    private static void StackNavigation()
    {
        var boss = Boss() with { Cast = new(99999, Center, 0, Start, Start.AddSeconds(1)) };
        var party = Enumerable.Range(0, 8).Select(i => Player(i)).ToArray();
        var e = Encounter(party[1..]);
        e.Synchronize([boss, .. party], Center, Start, playerId: 100);
        e.Icon(101, 62, Start, actorId: 100);
        Check(e.Hints(Center).Contains("Stack (8/8+)") && e.AmbientAoes().Any(a => a.Id.EndsWith("/buddy"))
            && !e.PredictedDamage.Any(), "Minimum-eight stack became exact-eight or added default shared prediction.");
        e.Synchronize([boss, .. party, Player(24)], Center, Start.AddSeconds(1), playerId: 100);
        Check(e.AmbientAoes().Single(a => a.Id.EndsWith("/display")).Safe, "Own stack became dangerous due to external party slot.");
        var follower = Player(0, Center + Vector3.UnitX * 10);
        e.Synchronize([boss, follower, .. party[1..]], follower.Position, Start.AddSeconds(2), playerId: 101);
        Check(e.AmbientAoes().Any(a => a.Id.EndsWith("/join")), "Unmarked viewer could not join unlimited stack.");
    }
    private static void Knockbacks()
    {
        var e = Encounter(new DungeonActor(10, 0x2AB1, Center), new DungeonActor(11, 0x2AB1, Center));
        var cast = new DungeonCastObservation(16452, Center + Vector3.UnitX * 5, 0, Start, Start.AddSeconds(8.5));
        e.StartCast(10, cast, Start);
        e.StartCast(11, cast with { Rotation = MathF.PI / 2, StartedAt = Start.AddSeconds(.1) }, Start.AddSeconds(.1));
        var waves = e.AmbientAoes().Where(a => a.Id.StartsWith("fate/tidal/")).ToArray();
        Check(waves.Length > 0 && waves.All(a => !a.Navigate), "TidalWave projection became navigation.");
        var model = new ArchaeotaniaTidalWave(e.Casts[10]);
        Check(model.Distance == 48 && !model.StopAtWall && !model.IgnoreImmunes
            && model.Destination(Center) == Center + Vector3.UnitZ * 48
            && model.Cast.Location == cast.Location, "TidalWave lost explicit movement model.");
        e.Tick(Start.AddSeconds(20));
        Check(e.AmbientAoes().Any(a => a.Id.StartsWith("fate/tidal/")), "Prediction clock ended TidalWave.");
        e.Finish(10, 16452);
        Check(e.AmbientAoes().Any(a => a.Id.StartsWith("fate/tidal/11")), "First source Finish cleared another source.");
        e.Finish(11, 16452, completed: false);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("fate/tidal/")), "Cancel retained TidalWave.");
    }
    private static void Priority()
    {
        var add = new DungeonActor(10, 0x2A8E, Center, Hp: 1000, IsEnemy: true, ObjectIndex: 20, FateId: 1432);
        var e = Encounter(add);
        Check(e.PriorityTarget(10, Center) == 10 && e.ForbiddenTargets.Length == 0, "Adds source priority0 lost or became forbidden.");
        e.Synchronize([Boss(), Player(), add with { Hp = 1 }], Center, Start.AddSeconds(1), playerId: 100);
        Check(e.PriorityTarget(10, Center) == 1, "Adds bypassed Enemy.Priority's Pointless setter protection.");
        e.Synchronize([Boss() with { Statuses = [325] }, Player()], Center, Start.AddSeconds(2), playerId: 100);
        Check(e.PriorityTarget(1, Center) == 1, "Active Fate inherited auto invincibility or required Fate metadata to emit.");
    }

    private static void RawCorrections()
    {
        var raw = new DungeonCastObservation(16454, Center, 0, Start, Start.AddSeconds(3), TotalTime: 3);
        var helper = new DungeonActor(10, 0x2995, Center, Cast: raw, Dead: true);
        var e = Encounter(helper);
        Check(e.Casts.Count == 1, "Dead caster was excluded from component activation replay.");
        e.Synchronize([Boss(), Player(), helper with { Position = Center + Vector3.UnitX * 15 }], Center,
            Start.AddSeconds(.1), playerId: 100);
        Check(e.Casts[10].Location == Center, "Actor motion changed an unchanged CastInfo lock.");
        var corrected = raw with { Location = Center + Vector3.UnitX * 7, Rotation = .3f,
            TotalTime = 3.1f, FinishAt = Start.AddSeconds(3.1) };
        e.Synchronize([Boss(), Player(), helper with { Cast = corrected }], Center, Start.AddSeconds(.2), playerId: 100);
        Check(e.Casts[10].Location == corrected.Location && e.Casts[10].Rotation == corrected.Rotation
            && e.Casts[10].FinishAt == corrected.FinishAt,
            $"Real raw fields did not rebuild the source component lock: {e.Casts[10].Location}, {e.Casts[10].Rotation}, {e.Casts[10].FinishAt:O} expected {corrected.Location}, {corrected.Rotation}, {corrected.FinishAt:O}.");
        corrected = corrected with { Rotation = .6f };
        e.Synchronize([Boss(), Player(), helper with { Cast = corrected }], Center, Start.AddSeconds(.3), playerId: 100);
        Check(e.Casts[10].Rotation == .6f, "Replacement tombstone rejected the next same-generation correction.");
    }

    private static void StackGenerations()
    {
        var boss = Boss() with { Cast = new(99999, Center, 0, Start, Start.AddSeconds(10)) };
        var old = Player(1);
        var other = Player(2);
        var e = Encounter(old, other);
        e.Synchronize([boss, Player(), old, other], Center, Start, playerId: 100);
        e.Icon(0, 62, Start, actorId: 101);
        e.Icon(0, 62, Start, actorId: 102);
        e.Effect(20, 17483, 0, Start.AddSeconds(1), target: 101);
        e.Effect(20, 17483, 0, Start.AddSeconds(1), target: 102);
        Check(!e.AmbientAoes().Any(a => a.Id.Contains("/stack/")), "Zero sequence collapsed distinct main targets.");
        e.Icon(0, 62, Start.AddSeconds(2), actorId: 101);
        e.Effect(20, 17483, 0, Start.AddSeconds(1), target: 101);
        Check(e.AmbientAoes().Any(a => a.Id.Contains("/stack/")), "Duplicate old receipt cleared a new mark.");
        e.RemoveActor(101);
        e.Synchronize([boss, Player(), old with { Position = Center + Vector3.UnitX * 20 }, other], Center,
            Start.AddSeconds(3), playerId: 100);
        var display = e.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
        Check(Vector3.Distance(display.Position, Center) < .1f, "Same-ID replacement rebound an old stack target.");
        e.Effect(20, 17483, 0, Start.AddSeconds(4), target: 101);
        Check(!e.AmbientAoes().Any(a => a.Id.Contains("/stack/")), "Later zero-sequence effect was lost.");
    }

    private static void RawHandoffs()
    {
        var helper = new DungeonActor(10, 0x2995, Center);
        var e = Encounter(helper);
        var packet = new DungeonCastObservation(16454, Center, 0, Start, Start.AddSeconds(3));
        e.StartCast(10, packet, Start);
        var raw = packet with { Action = 16455, Location = Center + Vector3.UnitX, StartedAt = Start.AddSeconds(.1) };
        e.Synchronize([Boss(), Player(), helper with { Cast = raw }], Center, Start.AddSeconds(.1), playerId: 100);
        raw = raw with { Location = Center + Vector3.UnitX * 2 };
        e.Synchronize([Boss(), Player(), helper with { Cast = raw }], Center, Start.AddSeconds(.2), playerId: 100);
        Check(e.Casts[10].Location == raw.Location, "First raw replacement erased its observation baseline.");
        var unknown = raw with { Action = 16446 };
        e.Synchronize([Boss(), Player(), helper with { Cast = unknown }], Center, Start.AddSeconds(.3), playerId: 100);
        Check(e.Casts.Count == 0, "Unactivated visual created an AOE.");
        raw = raw with { Location = Center + Vector3.UnitX * 3 };
        e.Synchronize([Boss(), Player(), helper with { Cast = raw }], Center, Start.AddSeconds(.4), playerId: 100);
        Check(e.Casts.Count == 1 && e.Casts[10].Location == raw.Location, "Unknown action tombstone blocked current changed raw.");
        e.Finish(10, raw.Action, Start.AddSeconds(.5), completed: false);
        e.Synchronize([Boss(), Player(), helper with { Cast = raw }], Center, Start.AddSeconds(.6), playerId: 100);
        e.StartCast(10, raw, Start.AddSeconds(.6));
        Check(e.Casts.Count == 0, "Unchanged raw or old packet resurrected cancelled generation.");
    }
}
