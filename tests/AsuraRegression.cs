using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class AsuraRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Asura native identity exact starting polygon reciprocal cosine and map shrink", () =>
        {
            using var f = Scene();
            var source = BossMod.Endwalker.Trial.T08Asura.T08Asura.StartingArena;
            Check(source.Radius > 19.5f && Math.Abs(source.Radius - 19.59436f) < .00001f
                && f.Engine.Arena!.Contains(new(100, 0, 119.59f)) && !f.Engine.Arena.Contains(new(100, 0, 119.60f))
                && ArrDungeonCatalog.IsScript("masterplayer.trial.asura"), "Starting polygon or identity differs.");
            f.Engine.MapEffect(1, 0x00020001, f.Now); f.Render();
            Check(!f.Engine.Arena.Contains(new(100, 0, 119.2f)) && f.Engine.Arena.Contains(new(100, 0, 119.16f)), "Map failed shrink.");
        });
        run("Asura lower realm shrink warning starts finish plus point eight and persists until map", () =>
        {
            using var f = Scene(); Start(f, 10, 36001);
            Check(Group(f, "shrink").Single() is { Shape: DonutTemplate { InnerRadius: 19, OuterRadius: 20 }, Activation: var at }
                && Math.Abs((at!.Value - f.Start).TotalSeconds - 6.1) < .000001, "Shrink warning clock differs.");
            End(f, 10, 36001); At(f, 100); Check(Group(f, "shrink").Length == 1, "Warning consumed on cast/time.");
            f.Engine.MapEffect(1, 0x00020001, f.Now); f.Render(); Check(!Group(f, "shrink").Any(), "Map failed clear warning.");
            Start(f, 10, 36001); Check(!Group(f, "shrink").Any(), "Reduced arena recreated initial warning.");
        });
        run("Asura chakra preserves actual mismatched source action mappings and duplicate outer component", () =>
        {
            using var f = Scene();
            var cases = new[] { (35994u, 0f, 5f), (35995u, 6f, 8f), (35996u, 9f, 11f), (35998u, 12f, 14f), (35997u, 15f, 17f) };
            foreach (var (action, inner, outer) in cases)
            {
                Start(f, 10, action); var cast = f.Engine.Casts[10];
                Check(inner == 0 ? cast.Rule.Shape is CircleTemplate { Radius: 5 }
                    : cast.Rule.Shape is DonutTemplate d && d.InnerRadius == inner && d.OuterRadius == outer, "Chakra actual map changed.");
                if (action == 35997) Check(Group(f, "chakra5").Single().Shape is DonutTemplate { InnerRadius: 18, OuterRadius: 20 }, "Second 35997 component missing.");
                End(f, 10, action);
            }
            Start(f, 10, 35999); Check(!f.Engine.Casts.ContainsKey(10) && !Group(f, "chakra5").Any(), "Comment-only Chakra5 action gained implementation.");
        });
        run("Asura laceration timeline snapshots any actor with queue count delay and no timeout", () =>
        {
            using var f = Scene();
            for (ulong i = 20; i < 25; ++i) f.Engine.Timeline(i, 0x11D6, f.Now);
            f.Render(); var list = Group(f, "laceration");
            Check(list.Length == 5 && list.Select(a => Math.Round((a.Activation!.Value - f.Start).TotalSeconds, 3))
                .SequenceEqual([7.1, 6.6, 6.1, 5.6, 5.1]) && list.All(a => a.Shape is CircleTemplate { Radius: 9 }), "Timeline queue differs.");
            f.Actors[20] = f.Actors[20] with { Position = new(115, 0, 110), Dead = true }; At(f, 100);
            f.Engine.RemoveActor(21); f.Actors.Remove(21); f.Scan();
            Check(Group(f, "laceration").Length == 5 && Group(f, "laceration")[0].Position == list[0].Position, "Timeline snapshot followed/deleted actor.");
            f.Engine.Effect(10, 35991, 1, f.Now); f.Render(); Check(Group(f, "laceration").Length == 5, "Laceration effect cleared queue.");
            Start(f, 10, 35991); End(f, 10, 35991); Check(!Group(f, "laceration").Any(), "Laceration finish failed global clear.");
        });
        run("Asura six bladed telegraphs enqueue at finish plus float eleven point nine and expose two", () =>
        {
            using var f = Scene();
            for (ulong i = 20; i < 26; ++i) Start(f, i, (uint)(36011 + (i - 20) % 3), rotation: (float)(i - 20) * MathF.PI / 2);
            var list = Group(f, "khadga").Where(a => a.Display).ToArray();
            Check(list.Length == 2 && list[0].Navigate && list[1].Navigate
                && list[0].Color == new Vector4(1, 1, 0, 1)
                && Math.Abs((list[0].Activation!.Value - f.Start).TotalSeconds - (5.3 + (double)11.9f)) < .000001,
                "Khadga exposure/time/color differs.");
            Check(Group(f, "khadga/center").Length == 1 && !f.Draw.Previews.ContainsKey("cast/20"), "Khadga center pressure or telegraph visibility differs.");
            At(f, 100); Check(Group(f, "khadga").Count(a => a.Display) == 2, "Khadga timed out.");
        });
        run("Asura opposite second khadga stays visible without navigation then becomes risky when first", () =>
        {
            using var f = Scene(); Start(f, 20, 36011); Start(f, 21, 36012, rotation: MathF.PI);
            var list = Group(f, "khadga").Where(a => a.Display).ToArray();
            Check(list.Length == 2 && !list[1].Navigate && f.Draw.Previews.ContainsKey(list[1].Id), "Opposite preview was removed or risky.");
            f.Engine.Effect(1, 35977, 1, f.Now); f.Render();
            var last = Group(f, "khadga").Single(a => a.Display);
            Check(last.Navigate && !Group(f, "khadga/center").Any(), "New first retained Risky=false or center pressure.");
            f.Engine.Effect(1, 35982, 2, f.Now); f.Render(); Check(!Group(f, "khadga").Any(), "Final FIFO hit failed clear.");
        });
        run("Asura six resolving actions consume one FIFO each independently of source and target", () =>
        {
            using var f = Scene();
            for (ulong i = 20; i < 26; ++i) Start(f, i, 36011);
            uint seq = 1;
            foreach (var action in Enumerable.Range(35977, 6))
            {
                f.Engine.Effect(999, (uint)action, seq++, f.Now, 888); f.Render();
            }
            Check(!Group(f, "khadga").Any(), "Six action IDs failed FIFO.");
        });
        run("Asura many faces fixed cardinal variants overwrite and ignore cast finish", () =>
        {
            using var f = Scene();
            foreach (var (action, angle) in new[] { (35989u, MathF.PI), (35986u, MathF.PI),
                (35987u, -.003f * MathF.PI / 180), (35988u, -.003f * MathF.PI / 180),
                (35985u, 89.999f * MathF.PI / 180), (35984u, 89.999f * MathF.PI / 180) })
            {
                Start(f, 10, action, rotation: 1.1f);
                var face = Group(f, "face").Single();
                Check(Math.Abs(face.Rotation - angle) < .000001 && face.Shape is FanTemplate { Radius: 20 },
                    "Face used cast rotation or wrong cardinal.");
                End(f, 10, action); Check(Group(f, "face").Single() == face, "Face cast finish consumed prediction.");
            }
            f.Engine.Effect(11, 36022, 1, f.Now); f.Render(); Check(Group(f, "face").Any(), "Face visual consumed prediction.");
            f.Engine.Effect(11, 36006, 2, f.Now); f.Render(); Check(!Group(f, "face").Any(), "Wrath effect failed clear.");
            At(f, 1); Start(f, 10, 35989); f.Engine.Effect(11, 36007, 3, f.Now); f.Render(); Check(!Group(f, "face").Any(), "Delight effect failed clear.");
        });
        run("Asura myriad sorts only on twelfth caster and exposes earliest six thereafter", () =>
        {
            using var f = Scene();
            for (ulong i = 20; i < 31; ++i) Start(f, i, 36021, duration: 9);
            Check(Group(f, "myriad").Length == 6 && Group(f, "myriad").First().Id.EndsWith("/20"), "Pre-threshold insertion order changed.");
            Start(f, 31, 36020, duration: 1);
            Check(Group(f, "myriad").First().Id.EndsWith("/31") && Group(f, "myriad").Length == 6, "Twelfth caster failed global activation sort.");
            Start(f, 31, 35969); f.Render(); Check(!Group(f, "myriad").Any(a => a.Id.EndsWith("/31")), "Replacement cast failed consume source group.");
            At(f, 100); Check(Group(f, "myriad").Length == 6, "Myriad predictions expired without finish.");
        });
        run("Asura enlarged and normal geometry widths follow enabled component parameters", () =>
        {
            using var f = Scene();
            foreach (var (action, width, length) in new[] { (35973u, 6f, 50f), (35974u, 28f, 100f), (35993u, 6f, 20f) })
            {
                Start(f, 10, action); Check(f.Engine.Casts[10].Rule.Shape is RayRectangleTemplate r && r.Length == length
                    && r.MaxLocalX - r.MinLocalX == width, "Line geometry differs."); End(f, 10, action);
            }
            Start(f, 10, 35970); Check(f.Engine.Casts[10].Rule.Shape is CircleTemplate { Radius: 60 }, "Large pedestal differs."); End(f, 10, 35970);
            Start(f, 10, 35972); Check(f.Engine.Casts[10].Rule.Shape is DonutTemplate { InnerRadius: 48, OuterRadius: 96 }, "Large wheel differs.");
        });
        run("Asura cutting jewel follows target through death until finish with tankbuster prediction", () =>
        {
            using var f = Scene(); Start(f, 10, 36000, target: 101);
            f.Actors[101] = f.Actors[101] with { Position = new(110, 0, 110), Dead = true }; f.Scan();
            Check(Group(f, "bait/36000").Single(a => a.Display).Position == new Vector3(110, 0, 110)
                && f.Engine.PredictedDamage.Single().DamageType == "Tankbuster", "Cast bait lost allow-dead target.");
            End(f, 10, 36000); Check(!Group(f, "bait/36000").Any() && !f.Engine.PredictedDamage.Any(), "Cast finish leaked bait.");
        });
        run("Asura ordered chaos uses radius five display six navigation and no predicted damage", () =>
        {
            using var f = Scene(); Start(f, 10, 36003, target: 101); Start(f, 11, 36003, target: 102);
            Check(Group(f, "bait/36003").Count(a => a.Display && a.OutlineOnly && a.Shape is CircleTemplate { Radius: 5 }) == 2
                && Group(f, "bait/36003").Where(a => !a.Display).All(a => a.Shape is CircleTemplate { Radius: 6 })
                && !f.Engine.PredictedDamage.Any(), "Spread geometry/prediction differs.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
            Check(Group(f, "bait/36003").Count(a => a.Display) == 1, "Dead spread visible.");
            f.Actors[101] = f.Actors[101] with { Dead = false }; f.Scan();
            Check(Group(f, "bait/36003").Count(a => a.Display) == 2, "Revival lost retained spread.");
            End(f, 10, 36003); Check(Group(f, "bait/36003").Count(a => a.Display) == 1, "Target finish removed all spreads.");
        });
        run("Asura raidwides and icon only markers create no invented controls or mechanics", () =>
        {
            using var f = Scene();
            foreach (var action in new[] { 36001u, 35990u })
            {
                Start(f, 10, action); Check(f.Engine.PredictedDamage.Count() == 8, "Raidwide prediction missing.");
                End(f, 10, action); Check(!f.Engine.PredictedDamage.Any(), "Raidwide leaked.");
            }
            foreach (var icon in new[] { 342u, 139u, 454u, 455u, 456u, 457u, 458u, 459u }) f.Engine.Icon(100, icon, f.Now);
            Check(!Group(f, "khadga").Any() && !Group(f, "bait").Any() && !f.Engine.RequireAllStop
                && f.Engine.ForbiddenTargets.Length == 0 && !f.Engine.Gazes.Any(), "Metadata icons invented components or PR controls.");
        });
        run("Asura private death retains phases but boss death clears drawing navigation and arena", () =>
        {
            using var f = Scene(); f.Engine.Timeline(20, 0x11D6, f.Now); Start(f, 21, 36011);
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Engine.Synchronize(f.Actors.Values, null, f.Now, playerId: 100); f.Render();
            Check(f.Engine.Active != null && Group(f, "laceration").Any() && Group(f, "khadga").Any(), "Personal death erased phase.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0
                && f.Snapshot.ArenaBoundary == null, "Primary death leaked output.");
        });
        run("Asura dead spread caster still outputs until accepted finish and stale cancellation has no side effects", () =>
        {
            using var f = Scene(); At(f, 10); Start(f, 10, 36003, target: 101);
            f.Actors[10] = f.Actors[10] with { Dead = true }; f.Scan();
            Check(Group(f, "bait/36003").Any(), "Source death hid target-based spread.");
            f.Engine.Finish(10, 36003, f.Start.AddSeconds(9)); f.Render();
            Check(Group(f, "bait/36003").Any() && f.Engine.Casts.ContainsKey(10), "Rejected old finish consumed current spread.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x409B, 1136); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c };
        for (var i = 0; i < 8; ++i)
            f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * i, PartySlot: i, Hostile: false);
        for (ulong i = 10; i < 42; ++i) f.Actors[i] = new(i, 0x233C, c);
        f.Scan(); return f;
    }
    private static void At(Fight f, double time) { f.Now = f.Start.AddSeconds(time); f.Scan(); }
    private static void Start(Fight f, ulong source, uint action, ulong target = 0, float rotation = 0, double duration = 5)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Tick(f.Now); f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("asura/" + prefix)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
