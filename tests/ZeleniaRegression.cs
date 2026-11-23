using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class ZeleniaRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Zelenia native identity polygon arena hole map restoration and persistent warning", () =>
        {
            using var f = Scene();
            Check(ArrDungeonCatalog.IsScript("masterplayer.trial.zelenia") && f.Engine.Arena!.Contains(new(100, 0, 100)), "Identity/default arena.");
            Start(f, 10, 43092);
            Check(Math.Abs((Group(f, "shrink").Single().Activation!.Value - f.Start).TotalSeconds - 5.4) < .000001, "Shrink timing.");
            End(f, 10, 43092); At(f, 50); Check(Group(f, "shrink").Any(), "Warning timed out.");
            f.Engine.MapEffect(1, 0x00020001, f.Now); f.Render();
            Check(!Group(f, "shrink").Any() && !f.Engine.Arena.Contains(new(100, 0, 100))
                && f.Engine.Arena.Contains(new(100, 0, 105)), "Donut map.");
            Start(f, 10, 43092); f.Engine.MapEffect(1, 0x00080004, f.Now); f.Render();
            Check(Group(f, "shrink").Any() && f.Engine.Arena.Contains(new(100, 0, 100)), "Restoration wrongly clears warning.");
        });
        run("Zelenia thunder IV preserves FIFO rather than finish source and no time expiry", () =>
        {
            using var f = Scene();
            Start(f, 10, 43084, location: new(100.003f, 0, 101.007f));
            Start(f, 11, 43085, duration: 1);
            var first = Group(f, "thunder4").Single();
            Check(first.Shape is CircleTemplate { Radius: 8 } && first.Position.X == 100.003f, "FIFO/precision.");
            End(f, 11, 43085);
            Check(Group(f, "thunder4").Single().Shape is DonutTemplate { InnerRadius: 8, OuterRadius: 24 }, "Finish consumed source instead of FIFO.");
            At(f, 100); Check(Group(f, "thunder4").Length == 1, "FIFO expired.");
            End(f, 10, 43084); Check(!Group(f, "thunder4").Any(), "Final finish.");
        });
        run("Zelenia thunder III conducts only first intersecting connected block and wraps tile five zero", () =>
        {
            using var f = Scene(); Light(f, 0, 1, 3, 5);
            Start(f, 10, 43102, location: new(100, 0, 87));
            var aoes = Group(f, "thunder3");
            Check(aoes.Length == 4 && aoes[0].Shape is CircleTemplate { Radius: 4 }, "Circle/block size.");
            Check(aoes.Skip(1).Select(a => Math.Round(a.Rotation * 180 / MathF.PI)).SequenceEqual([-120d, 180d, 120d]), "Connected ordering or disjoint tile.");
            f.Engine.MapEffect(4, 0x00040020, f.Now); f.Render();
            Check(Group(f, "thunder3").Length == 4, "Map rewrote existing prediction.");
            End(f, 10, 43102); Check(!Group(f, "thunder3").Any(), "Source finish did not clear conduction.");
        });
        run("Zelenia thunder III all lit terminates with six unique sectors", () =>
        {
            using var f = Scene(); Light(f, 0, 1, 2, 3, 4, 5);
            Start(f, 10, 43102, location: new(100, 0, 100));
            Check(Group(f, "thunder3").Length == 7 && Group(f, "thunder3").Count(a => a.Shape is FanTemplate) == 6, "All lit finite expansion.");
        });
        run("Zelenia thunder III sector dedup keeps first insertion activation then later source takes over", () =>
        {
            using var f = Scene(); Light(f, 0);
            Start(f, 10, 43102, duration: 9, location: new(100, 0, 87));
            Start(f, 11, 43439, duration: 1, location: new(100, 0, 86));
            var all = Group(f, "thunder3");
            Check(all.Length == 3 && Math.Abs((all.Single(a => a.Shape is FanTemplate).Activation!.Value - f.Start).TotalSeconds - 9.3) < .000001, "Dedup sorted by time.");
            End(f, 10, 43102);
            Check(Group(f, "thunder3").Length == 2 && Math.Abs((Group(f, "thunder3").Single(a => a.Shape is FanTemplate).Activation!.Value - f.Start).TotalSeconds - 1.3) < .000001, "Later source failed takeover.");
        });
        run("Zelenia second thunder IV empty fallback and independent second finish clear III", () =>
        {
            using var f = Scene(); Light(f, 1, 4);
            Start(f, 10, 43446); Start(f, 11, 43447);
            Check(Group(f, "thunder3").Length == 2 && Group(f, "thunder3").All(a => a.Shape is FanTemplate), "Empty fallback duplicated or added circle.");
            End(f, 10, 43446); Check(Group(f, "thunder3").Length == 2, "First finish cleared III.");
            Start(f, 12, 43102);
            End(f, 11, 43447);
            Check(!Group(f, "thunder3").Any() && !Group(f, "thunder4").Any(), "Second finish did not clear entire III independently.");
        });
        run("Zelenia timeline tile activation uses truncated unnormalized degrees and ignores unknown values", () =>
        {
            using var f = Scene();
            f.Actors[20] = f.Actors[20] with { Rotation = .9f * MathF.PI / 180 }; f.Scan();
            f.Engine.Timeline(20, 0x1E46, f.Now);
            f.Actors[21] = f.Actors[21] with { Rotation = 720 * MathF.PI / 180 }; f.Scan();
            f.Engine.Timeline(21, 0x1E46, f.Now); Start(f, 10, 43446);
            Check(Group(f, "thunder3").Single().Rotation == 0, "Truncation/normalization changed tile.");
        });
        run("Zelenia shock marker follows actor and effects build persistent quantized circles with exact double hit counter", () =>
        {
            using var f = Scene(); f.Engine.Icon(101, 581, f.Now);
            Check(Group(f, "spread").Single(a => a.Display).Shape is CircleTemplate { Radius: 4 }
                && f.Engine.PredictedDamage.Single() is { TargetId: 101, DamageType: "Raidwide" } p
                && p.Activation == f.Start.AddSeconds(8), "Spread prediction.");
            f.Actors[10] = f.Actors[10] with { Position = new(103.004f, 0, 100.004f) }; f.Scan();
            Hit(f, 10, 43057, 1); Hit(f, 11, 43057, 2);
            var expected = new BossMod.WPos(103.004f, 100.004f).Quantized();
            Check(!Group(f, "spread").Any() && Group(f, "shock").Length == 2
                && Group(f, "shock")[0].Position == new Vector3(expected.X, 0, expected.Z), "Effect circle position/clear.");
            for (uint i = 0; i < 3; ++i) Hit(f, 10, 43063, 10 + i);
            Check(Group(f, "shock").Length == 2, "Hits removed individual circles.");
            Hit(f, 10, 43063, 13); Check(!Group(f, "shock").Any(), "Fourth hit failed global clear.");
        });
        run("Zelenia shock counter increments before any circles and exact equality remains authoritative", () =>
        {
            using var f = Scene();
            Hit(f, 10, 43063, 1); Hit(f, 10, 43057, 2); Hit(f, 10, 43063, 3);
            Check(!Group(f, "shock").Any(), "Empty-list hit was discarded.");
        });
        run("Zelenia stock break icon actor target eight person join and only last resolving action clears", () =>
        {
            using var f = Scene(); f.Engine.Icon(101, 590, f.Now, actorId: 102);
            var expected = new BossMod.WPos(102, 100).Quantized();
            Check(Group(f, "stack").Single(a => a.Display).Position == new Vector3(expected.X, 0, expected.Z)
                && Group(f, "stack/join").Any() && !f.Engine.PredictedDamage.Any(), "Icon actor/join or invented shared damage.");
            foreach (var action in new[] { 43087u, 43088u, 43089u }) Hit(f, 10, action, action);
            Check(Group(f, "stack").Any(), "First three hits clear marker.");
            Hit(f, 10, 43090, 4); Check(!Group(f, "stack").Any(), "Final hit failed clear.");
        });
        run("Zelenia ascension only helper timeline snapshots delayed queue and hides last retained entry", () =>
        {
            using var f = Scene(); f.Engine.Timeline(10, 0x11DB, f.Now); Check(!Group(f, "ascension").Any(), "Wrong OID accepted.");
            f.Engine.Timeline(20, 0x11DB, f.Now); Check(!Group(f, "ascension").Any(), "Single entry exposed.");
            f.Engine.Timeline(21, 0x11DB, f.Now); f.Render();
            Check(Group(f, "ascension").Length == 2 && Group(f, "ascension").All(a => a.Activation == f.Start.AddSeconds(11.1)), "Queue/time.");
            Start(f, 10, 43074); End(f, 10, 43074); Check(!Group(f, "ascension").Any(), "Last entry remains visible.");
            f.Engine.Timeline(22, 0x11DB, f.Now); f.Render(); Check(Group(f, "ascension").Length == 2, "Hidden last entry was deleted.");
        });
        run("Zelenia simple cone queues cap previews and danger colors without dropping navigation", () =>
        {
            using var f = Scene();
            for (ulong i = 10; i < 15; ++i) Start(f, i, 43083);
            var slash = Group(f, "slash");
            Check(slash.Length == 4 && slash.All(a => a.Navigate)
                && slash.Count(a => a.Color == new Vector4(1, 1, 0, 1)) == 2
                && !f.Draw.Previews.ContainsKey("cast/10"), "Slash cap/colors/double output.");
            for (ulong i = 20; i < 23; ++i) Start(f, i, 43126);
            Check(Group(f, "holy").Length == 2, "Holy cap.");
            Start(f, 30, 43112); Check(f.Engine.Casts[30].Rule.Shape is RayRectangleTemplate r
                && r.Length == 24 && r.MaxLocalX - r.MinLocalX == 64, "Powerbreak geometry.");
        });
        run("Zelenia specter follows dead target until finish and stale cancellation cannot consume current cast", () =>
        {
            using var f = Scene(); At(f, 10); Start(f, 10, 43129, target: 101);
            f.Actors[101] = f.Actors[101] with { Dead = true, Position = new(110, 0, 100) }; f.Scan();
            Check(Group(f, "specter").Any() && f.Engine.PredictedDamage.Single().DamageType == "Tankbuster", "Allow-dead bait.");
            f.Engine.Finish(10, 43129, f.Start.AddSeconds(9)); f.Render();
            Check(Group(f, "specter").Any(), "Stale finish mutated state.");
            End(f, 10, 43129); Check(!Group(f, "specter").Any(), "Finish left bait.");
        });
        run("Zelenia roseblood ignores immunity joins first caster and predicts damage on actual action", () =>
        {
            using var f = Scene(); f.Actors[100] = f.Actors[100] with { Position = new(108, 0, 100), Statuses = [160] }; f.Scan();
            Start(f, 10, 43479); Start(f, 11, 43479);
            Check(Group(f, "knockback/join").Length == 1 && Group(f, "knockback").Any(a => a.Display)
                && f.Engine.Hints(f.Actors[100].Position).Contains("击退后将出界")
                && f.Engine.PredictedDamage.Count() == 16, "Knockback immunity/join/prediction.");
            Check(Group(f, "knockback").Where(a => a.Navigate).All(a => a.Id.EndsWith("/join")), "Invented landing forbidden zones.");
            End(f, 10, 43479); End(f, 11, 43479);
            Start(f, 12, 43095); Check(!f.Engine.PredictedDamage.Any(), "Comment-only quietus gained prediction.");
        });
        run("Zelenia death preserve personal history boss death cleans outputs and no invented PR restrictions", () =>
        {
            using var f = Scene(); Light(f, 0); Start(f, 10, 43102);
            Check(f.Engine.PreserveFightAcrossPlayerDeath && !f.Engine.RequireAllStop && f.Engine.ForbiddenTargets.Length == 0
                && !f.Engine.Gazes.Any(), "Control contract.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan(); Check(Group(f, "thunder3").Any(), "Personal death erased phase.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0
                && f.Snapshot.ArenaBoundary == null, "Primary death leaked output.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x47C3, 1270); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c };
        for (var i = 0; i < 8; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * i, PartySlot: i, Hostile: false);
        for (ulong i = 10; i < 40; ++i) f.Actors[i] = new(i, i is >= 20 and < 30 ? 0x47C4u : 0x233Cu, c);
        f.Scan(); return f;
    }
    private static void Light(Fight f, params uint[] tiles) { foreach (var i in tiles) f.Engine.MapEffect(4 + i, 0x00400100, f.Now); }
    private static void At(Fight f, double time) { f.Now = f.Start.AddSeconds(time); f.Scan(); }
    private static void Hit(Fight f, ulong source, uint action, uint sequence) { f.Engine.Effect(source, action, sequence, f.Now); f.Render(); }
    private static void Start(Fight f, ulong source, uint action, ulong target = 0, double duration = 5, Vector3? location = null)
    {
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, 0, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Tick(f.Now); f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("zelenia/" + prefix)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
