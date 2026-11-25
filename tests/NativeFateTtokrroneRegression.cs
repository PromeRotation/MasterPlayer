using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeFateTtokrroneRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly Vector3 Center = new(53, 0, -820);
    private static DungeonActor Boss => new(1, 0x41DF, Center, InCombat: true, Hp: 1000);
    private static DungeonActor Player => new(100, 0, Center, Hostile: false, PartySlot: 0, Player: true);
    private static DungeonEncounter Encounter(params DungeonActor[] helpers)
    {
        var e = new DungeonEncounter(1190);
        e.Synchronize([Boss, Player, .. helpers], Center, Start, playerId: 100);
        return e;
    }
    private static DungeonAmbientAoe[] Aoes(DungeonEncounter e) =>
        e.AmbientAoes().Where(a => a.Id.StartsWith("ttokrrone/aoe/")).ToArray();
    private static void Cast(DungeonEncounter e, uint action, double at = 0, double finish = 8,
        ulong source = 1, Vector3? position = null, float rotation = 0) =>
        e.StartCast(source, new(action, position ?? Center, rotation, Start.AddSeconds(at), Start.AddSeconds(finish)), Start.AddSeconds(at));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void Run(Action<string, Action> run)
    {
        run("native fate Ttokrrone exact 48 point arena strict slot0 pull death deaggro and precombat display", Lifecycle);
        run("native fate Ttokrrone four Tempest visual shapes two sides plus1 and six real Finish global clears", Tempest);
        run("native fate Ttokrrone four Sandspout directions float6.1 clocks both risky and Native-only clear", Sandspout);
        run("native fate Ttokrrone Landswallow six threshold source sort all risky retained colors effect38643 FIFO", Swallow);
        run("native fate Ttokrrone Sandspheres strict4.5 continuous prefix original FIFO source matching Finish", Spheres);
        run("native fate Ttokrrone four Dustdevil seven actual finishes float interval fixed cone origin helper circle and final cleanup", Rotation);
        run("native fate Ttokrrone same count Dustdevil restart four retains old cached cones until actual Finish", RotationCache);
        run("native fate Ttokrrone ordinary Devour exact8 raidwide diagnostics and copied sequences survive destruction", Ordinary);
    }

    private static void Lifecycle()
    {
        var e = new DungeonEncounter(1190);
        e.Synchronize([Boss, Player with { Position = Center + new Vector3(30, 0, 0) }], Center, Start, playerId: 100);
        Check(e.Active == null && e.AmbientAoes().All(a => !a.Navigate), "Strict source slot0 pull replaced by viewer.");
        e.Synchronize([Boss, Player], Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active != null && e.Arena!.GetContours()[0].Count == 48 && e.Arena.Position == Center,
            "Actual polygon or center was replaced.");
        var circleOnly = Center + new Vector3(29.48f * MathF.Sin(MathF.PI / 48), 0, 29.48f * MathF.Cos(MathF.PI / 48));
        Check(!e.Arena.Contains(circleOnly, 0), "48-gon was approximated by its enclosing circle.");
        e.Synchronize([Boss with { InCombat = false, Targetable = false }, Player with { Dead = true }],
            Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active != null, "Deaggro or viewer death reset BossModule.");
        e.Synchronize([Boss with { Hp = 0 }, Player], Center, Start.AddSeconds(3), playerId: 100);
        Check(e.Active == null && Aoes(e).Length == 0, "HP0 did not end the original actor generation.");
    }

    private static void Tempest()
    {
        foreach (var visual in new uint[] { 37331, 37332, 37333, 37334 })
            foreach (var finish in new uint[] { 37328, 37329, 37335, 37336, 37337, 37338 })
            {
                var e = Encounter(new DungeonActor(10, 0x4225, Center));
                Cast(e, visual, rotation: .3f);
                var aoes = Aoes(e);
                Check(aoes.Length == (visual is 37331 or 37332 ? 1 : 2)
                    && aoes.All(a => a.Navigate && a.Activation == Start.AddSeconds(9))
                    && aoes[0].Rotation == .3f + MathF.PI / 2, "Tempest stages/rotation/clock differ.");
                if (aoes.Length == 2) Check(aoes[1].Rotation == .3f - MathF.PI / 2, "Second side is not minus90.");
                Check(visual != 37331 || aoes[0].Shape is CircleTemplate { Radius: 19 }, "Circle differs.");
                Check(visual != 37332 || aoes[0].Shape is DonutTemplate { InnerRadius: 14, OuterRadius: 60 }, "Donut differs.");
                e.Finish(1, visual, completed: false);
                Check(Aoes(e).Length == aoes.Length, "Visual cancellation cleared Tempest.");
                Cast(e, finish, at: 1, finish: 2, source: 10);
                e.Effect(10, finish, 1, Start.AddSeconds(1));
                Check(Aoes(e).Length == aoes.Length, "Effect cleared Finish-only Tempest.");
                e.Finish(10, finish, completed: false);
                Check(Aoes(e).Length == 0, "Helper finish did not clear every Tempest item.");
            }
    }

    private static void Sandspout()
    {
        foreach (var (action, offset, effect) in new[] { (37313u, 0f, 39813u), (37314u, MathF.PI, 39814u),
            (37315u, -MathF.PI / 2, 39843u), (37316u, MathF.PI / 2, 39816u) })
        {
            var e = Encounter();
            Cast(e, action, rotation: .2f);
            var aoes = Aoes(e);
            Check(aoes.Length == 2 && aoes[0].Shape is FanTemplate { Radius: 60 }
                && aoes[1].Shape is CircleTemplate { Radius: 13 } && aoes[0].Rotation == .2f + offset
                && aoes.All(a => a.Navigate && a.Activation == Start.AddSeconds(8).AddSeconds((double)6.1f)),
                "Sandspout direction/float delay or paired circle differs.");
            e.Finish(1, action, completed: false);
            Check(Aoes(e).Length == 2, "Visual finish consumed Sandspout.");
            e.Effect(10, effect, 1, Start.AddSeconds(9));
            Check(Aoes(e).Length == 0, "Actual cone Native effect failed global clear.");
        }
    }

    private static void Swallow()
    {
        var helpers = Enumerable.Range(10, 6).Select(id => new DungeonActor((ulong)id, 0x4225, Center)).ToArray();
        var e = Encounter(helpers);
        for (var i = 0; i < 5; ++i) Cast(e, (uint)(37578 + i), source: (ulong)(10 + i), finish: 20 - i);
        Check(Aoes(e).Select(a => a.Activation).SequenceEqual(new[] { 20, 19, 18, 17, 16 }.Select(i => (DateTime?)Start.AddSeconds(i).AddSeconds(.4))),
            "Landswallow sorted before exactly6.");
        Cast(e, 37583, source: 15, finish: 15);
        var aoes = Aoes(e);
        Check(aoes.Length == 6 && aoes.All(a => a.Navigate && a.Shape is RayRectangleTemplate { Length: 68, MaxLocalX: 13.5f }),
            "Landswallow geometry/all risky differs.");
        Check(aoes[0].Activation == Start.AddSeconds(15).AddSeconds(.4) && aoes[0].Color == new Vector4(1, 1, 0, 1),
            "Exactly6 sorting or source Danger differs.");
        e.Effect(1, 38643, 1, Start.AddSeconds(1));
        Check(Aoes(e).Length == 5 && Aoes(e)[0].Activation == Start.AddSeconds(16).AddSeconds(.4),
            "Unnamed effect38643 must be included in the numeric range FIFO.");
    }

    private static void Spheres()
    {
        var e = Encounter(new(10, 0x425E, Center), new(11, 0x425E, Center), new(12, 0x425E, Center));
        Cast(e, 38647, source: 10, finish: 10);
        Cast(e, 39245, source: 11, finish: 14.5);
        Cast(e, 39246, source: 12, finish: 11);
        Check(Aoes(e).Length == 1 && Aoes(e)[0].Shape is CircleTemplate { Radius: 6 },
            "Strict +4.5 boundary or contiguous prefix was ignored.");
        e.Finish(10, 38647, completed: false);
        Check(Aoes(e).Length == 2 && Aoes(e).All(a => a.Shape is CircleTemplate { Radius: 12 }), "Hidden sphere failed original-clock promotion.");
        e.Finish(12, 39246);
        Check(Aoes(e).Single().Activation == Start.AddSeconds(14.5), "Finish removed global head instead of matching source.");
    }

    private static void Rotation()
    {
        foreach (var action in new uint[] { 37317, 37321, 37318, 37322 })
        {
            var e = Encounter(new DungeonActor(10, 0x4225, Center + Vector3.UnitX * 5));
            Cast(e, action, rotation: .2f);
            var startRotation = .2f + (action is 37318 or 37322 ? MathF.PI : 0);
            var increment = action is 37317 or 37318 ? -MathF.PI / 2 : MathF.PI / 2;
            var aoes = Aoes(e);
            Check(aoes.Length == 3 && aoes.All(a => a.Navigate)
                && aoes[1].Rotation == startRotation + increment && aoes[2].Rotation == startRotation
                && aoes[1].Activation == Start.AddSeconds(8).AddSeconds(.9).AddSeconds((double)2.6f),
                "Rotating future-before-current source output differs.");
            e.Finish(1, action);
            e.Effect(10, 39817, 1, Start.AddSeconds(9));
            Check(Aoes(e).Length == 3, "Native effect incorrectly consumed Finish-only rotation.");
            for (var i = 0; i < 7; ++i)
            {
                var at = 10 + i * 4;
                var helperPos = Center + Vector3.UnitX * (i + 1);
                Cast(e, (uint)(i % 2 == 0 ? 39817 : 39820), at, at + 1, 10, helperPos);
                e.Finish(10, i % 2 == 0 ? 39817u : 39820u, completed: false);
                aoes = Aoes(e);
                if (i == 6) Check(aoes.Length == 0, "Seventh helper Finish retained cone/circle.");
                else
                {
                    var circle = aoes.Single(a => a.Shape is CircleTemplate);
                    var current = aoes.Last();
                    Check(circle.Position == helperPos && circle.Activation == Start.AddSeconds(at).AddSeconds(2.6)
                        && current.Position == Center && current.Activation == Start.AddSeconds(at).AddSeconds((double)2.6f),
                        "Helper circle and locked cone origins or distinct double/float consumption clocks differ.");
                }
            }
        }
    }

    private static void RotationCache()
    {
        var e = Encounter(new DungeonActor(10, 0x4225, Center));
        Cast(e, 37317);
        var first = Aoes(e).Where(a => a.Shape is FanTemplate).ToArray();
        Cast(e, 37317, position: Center + Vector3.UnitX * 2, rotation: .2f);
        Check(Aoes(e).Where(a => a.Shape is FanTemplate).SequenceEqual(first),
            "Same-generation packet correction changed published cones before count/consumption cache invalidation.");
        e.Finish(1, 37317);
        Cast(e, 37321, at: 1, finish: 9, position: Center + Vector3.UnitX * 3, rotation: .4f);
        e.Tick(Start.AddSeconds(20));
        var restarted = Aoes(e);
        Check(restarted.Where(a => a.Shape is FanTemplate).SequenceEqual(first)
            && restarted.Single(a => a.Shape is CircleTemplate).Position == Center + Vector3.UnitX * 3,
            "Same-count restart rebuilt cached old cones or failed immediate circle replacement.");
        for (var i = 0; i < 4; ++i)
        {
            Cast(e, 39818, at: 21 + i, finish: 22 + i, source: 10);
            e.Finish(10, 39818);
            var aoes = Aoes(e);
            if (i == 0)
                Check(aoes.Last().Position == Center + Vector3.UnitX * 3 && aoes.Last().Rotation == .4f + MathF.PI / 2,
                    "Next helper finish did not switch to new CCW sequence.");
            if (i == 3) Check(aoes.Length == 0, "Existing-sequence restart must be exactly4.");
        }
    }

    private static void Ordinary()
    {
        var e = Encounter();
        Cast(e, 37327);
        Check(e.OutputCasts.Single().Value.Rule.Shape is CircleTemplate { Radius: 8 }, "Devour radius differs.");
        e.Finish(1, 37327);
        Cast(e, 37339, at: 1, finish: 6);
        Check(e.PredictedDamage.Single().Activation == Start.AddSeconds(6)
            && !e.OutputCasts.Any(c => c.Value.Rule.Shape != null), "Raidwide became an invented danger.");
        e.Finish(1, 37339);
        Cast(e, 37314, at: 2, finish: 8);
        e.RemoveActor(1);
        Check(Aoes(e).Length == 0, "Primary destruction did not end module.");
    }
}
