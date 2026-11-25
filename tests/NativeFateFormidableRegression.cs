using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeFateFormidableRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static DungeonActor Boss(bool combat = true) => new(1, 0x294D, default, InCombat: combat,
        Hp: 1000, IsEnemy: true, ObjectIndex: 2, AggroPlayer: combat, FateId: 1464);
    private static DungeonActor Player(ulong id = 100, int slot = 0, Vector3 position = default)
        => new(id, 0, position, Hostile: false, PartySlot: slot, Player: true);
    private static DungeonActor Helper(ulong id, uint oid = 0x233C, Vector3 position = default)
        => new(id, oid, position, Targetable: false);
    private static DungeonEncounter Encounter(params DungeonActor[] others)
    {
        var e = new DungeonEncounter(814) { AllowedFateId = 1464 };
        e.Synchronize([Boss(), Player(), .. others], default(Vector3), Start, playerId: 100);
        return e;
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native fate Formidable exact ownership combat reset and source player display versus cached square", Lifecycle);
        run("native fate Formidable seven real effects retain frozen 10 50 donut processing clock and no finish clear", Fires);
        run("native fate Formidable created charge frozen quantized positions independent delays destruction and real effect", Charges);
        run("native fate Formidable missiles dynamic actor and R6 display only effect receipts and same ID new generation", Missiles);
        run("native fate Formidable duplicate min2 icons finish and native main target each remove first original reference", Stacks);
        run("native fate Formidable three ordinary shapes barrage true clock and no invented pull grenade expansion", Casts);
        run("native fate Formidable SteamDome15 ordered radial model no wall immunity strict expiry and no navigation", Knockback);
        run("native fate Formidable Stay1269 original clock events versus raw death and no expanded action movement control", Stay);
    }
    private static void Lifecycle()
    {
        Check(NativeFateCatalog.Owns(814, 0x294D) && !NativeFateCatalog.Owns(1464, 0x294D), "FATE confused with territory.");
        var e = new DungeonEncounter(814);
        e.Synchronize([Boss(false), Player()], default(Vector3), Start, playerId: 100);
        Check(e.Active == null && e.AmbientAoes().All(a => !a.Navigate), "Precombat started or navigated.");
        e.Synchronize([Boss(), Player(100, 0, new(5, 0, 5)), Player(101, 1, new(200, 0, 200))],
            new Vector3(200, 0, 200), Start.AddSeconds(1), playerId: 101);
        var boundary = (FormidableArenaBoundary)e.Arena!;
        Check(boundary.Position == default && boundary.ViewCenter == new Vector3(5, 0, 5)
            && boundary.ViewRadius == 30 && !boundary.ShowBoundary
            && boundary.Contains(new(30, 0, 30), 0) && boundary.GetContours()[0].Count == 4,
            "Source slot0 view, per-axis strict5 or actual navigation square changed.");
        e.Synchronize([Boss(), Player(100, 0, new(5.1f, 0, 4.5f))], new Vector3(5.1f, 0, 4.5f),
            Start.AddSeconds(2), playerId: 100);
        Check(e.Arena!.Position == new Vector3(5, 0, 4), "Source Round or per-axis cache update changed.");
        var previous = e.Arena;
        e.Synchronize([Boss(), Player(100, 0, new(6, 0, 5))], new Vector3(6, 0, 5), Start.AddSeconds(3), playerId: 100);
        Check(ReferenceEquals(previous, e.Arena) && ((FormidableArenaBoundary)e.Arena!).ViewCenter == new Vector3(6, 0, 5),
            "Display motion rebuilt immutable traversal geometry.");
        e.Synchronize([Boss(false), Player()], default(Vector3), Start.AddSeconds(4), playerId: 100);
        Check(e.Active == null && e.Arena == null, "SimpleBoss deaggro did not reset.");
        e.Synchronize([Boss(), Player()], default(Vector3), Start.AddSeconds(5), playerId: 100);
        Check(e.Active != null, "Reset blocked a new pull on existing living boss.");
        e = new DungeonEncounter(814) { FateSourcePlayerId = 100 };
        e.Synchronize([Boss(), Player(100, 4, new(100, 0, 100)), Player(101, 0)],
            default(Vector3), Start, playerId: 101);
        Check(((FormidableArenaBoundary)e.Arena!).ViewCenter == new Vector3(100, 0, 100)
            && e.Arena.Position == new Vector3(100, 0, 100), "PR MT slot or viewed actor replaced the true source local player.");
    }
    private static void Fires()
    {
        var e = Encounter();
        e.StartCast(1, new(17395, new(2, 0, 3), 0, Start, Start.AddSeconds(4)), Start);
        e.Finish(1, 17395, completed: false);
        Check(e.AmbientAoes().Single(a => a.Id == "formidable/fires").Activation == Start.AddSeconds(4),
            "Cancel cleared the effect-driven ring.");
        for (uint i = 1; i <= 6; ++i)
        {
            var now = Start.AddSeconds(i * 5);
            e.Effect(1, i == 1 ? 17395u : 18002u, i, now);
            var ring = e.AmbientAoes().Single(a => a.Id == "formidable/fires");
            Check(ring.Shape is DonutTemplate { InnerRadius: 10, OuterRadius: 50 }
                && ring.Position == new Vector3(2, 0, 3) && ring.Activation == now.AddSeconds(3.1),
                "Ring moved, changed shape or used initial instead of consumption clock.");
            e.Effect(1, i == 1 ? 17395u : 18002u, i, now);
        }
        e.Effect(1, 18002, 7, Start.AddSeconds(40));
        Check(!e.AmbientAoes().Any(a => a.Id == "formidable/fires"), "Seventh real effect failed to clear.");
        e.StartCast(1, new(17395, default, 0, Start.AddSeconds(41), Start.AddSeconds(45)), Start.AddSeconds(41));
        Check(e.AmbientAoes().Any(a => a.Id == "formidable/fires"), "New start did not reset count.");
    }
    private static void Charges()
    {
        var a = Helper(10, 0x2A9C, new(1.234f, 0, 2.345f));
        var b = Helper(11, 0x2BDC, new(4, 0, 5));
        var e = Encounter(a, b);
        var original = e.AmbientAoes().Where(a => a.Id.StartsWith("formidable/discharge/")).ToArray();
        Check(original.Length == 2 && original[0].Shape is DonutTemplate { InnerRadius: 9, OuterRadius: 60 }
            && original[0].Activation == Start.AddSeconds(9.3)
            && original[1].Shape is CircleTemplate { Radius: 8 } && original[1].Activation == Start.AddSeconds(8.1),
            "Created charges lost independent shapes/clocks.");
        e.Synchronize([Boss(), Player(), a with { Position = new(20, 0, 20), Dead = true }, b],
            default(Vector3), Start.AddSeconds(50), playerId: 100);
        Check(e.AmbientAoes().Single(a => a.Id == original[0].Id).Position == original[0].Position,
            "Created charge followed motion, hid death or expired on predicted time.");
        e.Effect(10, 17404, 1, Start.AddSeconds(51));
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("formidable/discharge/")) == 1, "Discharge real effect failed.");
        e.RemoveActor(11);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("formidable/discharge/")), "Destroyed charge retained prediction.");
    }
    private static void Missiles()
    {
        var missile = Helper(10, 0x2BD5);
        var e = Encounter(missile);
        Check(e.AmbientAoes().Where(a => a.Id.StartsWith("formidable/missile/")).All(a => !a.Navigate)
            && e.AmbientAoes().Any(a => a.Id.EndsWith("/radius")), "Missile became navigation or disappeared when untargetable.");
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("formidable/missile/")) == 2, "Untargetable missile lost its forced Actor marker.");
        e.Synchronize([Boss(), Player(), missile with { Position = new(3, 0, 4), Dead = true }], default(Vector3),
            Start.AddSeconds(1), playerId: 100);
        Check(e.AmbientAoes().Single(a => a.Id.EndsWith("/radius")).Position == new Vector3(3, 0, 4),
            "Dead missile outline failed to follow original actor.");
        e.Synchronize([Boss(), Player(), missile with { Position = new(40, 0, 0), Dead = true }],
            default(Vector3), Start.AddSeconds(1.5), playerId: 100);
        var marker = e.AmbientAoes().Single(a => a.Id == "formidable/missile/10");
        Check(marker.Position == new Vector3(30, 0, 0) && marker.OutlineOnly
            && marker.Color == new Vector4(1, .5f, 0, 1), "Dead offscreen missile lost source circle projection/color.");
        e.Effect(10, 18003, 1, Start.AddSeconds(2));
        e.Synchronize([Boss(), Player(), missile], default(Vector3), Start.AddSeconds(3), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("formidable/missile/")), "Observed same missile resurrected after explosion.");
        e.RemoveActor(10);
        e.Synchronize([Boss(), Player(), missile], default(Vector3), Start.AddSeconds(4), playerId: 100);
        Check(e.AmbientAoes().Any(a => a.Id.EndsWith("/radius")), "Same-ID new missile generation was blocked.");
    }
    private static void Stacks()
    {
        var helper = Helper(10);
        var e = Encounter(Player(101, 1), helper);
        e.Icon(0, 62, Start, actorId: 100);
        e.Icon(0, 62, Start.AddSeconds(1), actorId: 100);
        Check(e.AmbientAoes().Count(a => a.Id.EndsWith("/display") && a.Id.StartsWith("fate/stack/")) == 2
            && e.Hints(default(Vector3)).Any(h => h.Contains("/2+")), "Independent duplicate min2 icon stacks lost.");
        e.StartCast(10, new(17401, default, 0, Start.AddSeconds(2), Start.AddSeconds(7), 100), Start.AddSeconds(2));
        e.Finish(10, 17401, completed: false);
        Check(e.AmbientAoes().Count(a => a.Id.EndsWith("/display") && a.Id.StartsWith("fate/stack/")) == 1,
            "Real cancelled Finish did not consume first matching cast target.");
        e.Effect(10, 17401, 10, Start.AddSeconds(3), 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("fate/stack/")), "MainTarget effect did not consume the next first stack.");
    }
    private static void Casts()
    {
        var e = Encounter(Helper(10));
        var rules = e.Definitions[0].Casts.Where(c => c.Shape != null).ToArray();
        Check(rules.Length == 3 && rules.Single(r => r.Action == 17411).Shape is CircleTemplate { Radius: 12 },
            "Ordinary source shape set or expanded grenade differs.");
        foreach (var rule in rules)
        {
            e = Encounter(Helper(10));
            e.StartCast(10, new(rule.Action, default, .5f, Start, Start.AddSeconds(3)), Start);
            Check(e.Casts[10].Rule.Shape == rule.Shape, "Ordinary source geometry missing.");
            e.Finish(10, rule.Action);
        }
        e.StartCast(1, new(17393, default, 0, Start.AddSeconds(4), Start.AddSeconds(9)), Start.AddSeconds(4));
        Check(e.PredictedDamage.Single().Activation == Start.AddSeconds(9)
            && e.Hints(default(Vector3)).Any(h => h.Contains("7 repeats")), "Barrage source prediction/text changed.");
        e.Finish(1, 17393);
        Check(!e.PredictedDamage.Any(), "Barrage expired by wrong condition.");
    }
    private static void Knockback()
    {
        var e = Encounter();
        e.StartCast(1, new(17394, default, 0, Start, Start.AddSeconds(3)), Start);
        var model = new FormidableSteamDome(e.Casts[1]);
        Check(model.Distance == 15 && !model.StopAtWall && !model.IgnoreImmunes
            && model.Destination(new(2, 0, 0)) == new Vector3(17, 0, 0), "Radial model parameters missing.");
        e.Synchronize([Boss() with { Cast = new(17394, default, 0, Start, Start.AddSeconds(3)) },
            Player(), Player(101, 1, new(15, 0, 15))], new Vector3(15, 0, 15), Start.AddSeconds(.1), playerId: 101);
        var projection = e.AmbientAoes().Where(a => a.Id.StartsWith("formidable/steam/")).ToArray();
        var marker = projection.Single(a => a.Id.EndsWith("/player"));
        Check(projection.Length == 2 && projection.All(a => !a.Navigate)
            && marker.OutlineOnly && Vector3.Distance(marker.Position, new(30 / MathF.Sqrt(2), 0, 30 / MathF.Sqrt(2))) < .001f
            && marker.Color == new Vector4(1, 1, 0, 1),
            $"Steam projection differs: count={projection.Length}, point={marker.Position}, outline={marker.OutlineOnly}, color={marker.Color}.");
    }
    private static void Stay()
    {
        var e = Encounter();
        e.StatusChanged(100, 1269, true, Start, expiresAt: Start.AddSeconds(10), sourceId: 1);
        Check(e.Hints(default(Vector3)).Any(h => h.Contains("Pyretic"))
            && !e.Hints(default(Vector3)).Contains("Stop everything!"), "Stay lost model or early text window.");
        e.Tick(Start.AddSeconds(7));
        Check(e.Hints(default(Vector3)).Contains("Stop everything!"), "Stay inclusive3 hint boundary changed.");
        e.StatusChanged(100, 1269, false, Start.AddSeconds(8), sourceId: 1);
        Check(!e.Hints(default(Vector3)).Any(h => h.Contains("Pyretic")), "Real Lose retained stay intent.");
        Check(e.ForbiddenTargets.Length == 0, "Stay expanded into target prohibitions.");
        e = Encounter();
        e.StatusChanged(100, 1269, true, Start, expiresAt: Start.AddSeconds(10), sourceId: 1);
        e.Synchronize([Boss(), Player(101, 0)], default(Vector3), Start.AddSeconds(8), playerId: 101);
        Check(!e.Hints(default(Vector3)).Any(h => h.Contains("Pyretic")), "Removed actor's state leaked to replacement party slot.");
    }
}
