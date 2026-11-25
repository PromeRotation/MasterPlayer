using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

internal static class EurekaFinalBossRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E;
        internal readonly Vector3 Center;
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(uint territory)
        {
            Center = territory == 1107 ? new(-300, 0, -300) : new(-600, 0, -300);
            E = new(territory);
            Actors = [new(1, territory == 1107 ? 0x3D23u : 0x3CFCu, Center, InCombat: true),
                new(2, 0x233C, Center), new(3, 0x3D25, Center + Vector3.UnitX * 10),
                new(4, 0x3D26, Center + Vector3.UnitZ * 10),
                new(5, 0x3D24, Center - Vector3.UnitX * 10),
                new(100, 0, Center + Vector3.UnitX * 8, Player: true, PartySlot: 0),
                new(101, 0, Center - Vector3.UnitX * 8, Player: true, PartySlot: 1)];
            E.CurrentPartyMembers = [new(0, "", 100), new(1, "", 101)];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Sync(ulong viewer = 100)
            => E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == viewer)?.Position, Now, playerId: viewer);
        internal void Cast(uint action, ulong source = 2, float rotation = 0, double duration = 5)
        {
            Now = Now.AddMilliseconds(10);
            Set(source, a => a with { Cast = new(action, a.Position, rotation, Now, Now.AddSeconds(duration), TotalTime: (float)duration) });
            Sync();
        }
        internal void End(ulong source = 2) { Set(source, a => a with { Cast = null }); Sync(); }
        internal void Icon(ulong source, uint icon, double offset)
        {
            Now = Start.AddSeconds(offset); E.Tick(Now); E.Icon(100, icon, Now, source);
        }
        internal void Status(ulong source, uint id, ushort param = 0)
        {
            Set(source, a => a with { StatusSlots = [new(id, param, 0, Start.AddSeconds(100)) { SlotIndex = 4 }] });
            Sync();
        }
    }
    internal static void Run(Action<string, Action> run)
    {
        run("Eureka final bosses exact table mapping source ownership and activated rules", Catalog);
        run("Eureka Administrator source icon identity unknown delay exact six unstable sort", IconOrder);
        run("Eureka Administrator prefix one/five second boundaries and sticky combo colors", ComboWindow);
        run("Eureka Administrator hidden ordinary lines advance then regain default colors", Lines);
        run("Eureka Administrator real Finish global FIFO independent other AOEs and current raid mask", Finishes);
        run("Eureka Excalibur first/subsequent Steelstrike angle order and delayed full rectangles", Steel);
        run("Eureka Excalibur status-time sword snapshots exact five sorting safe endpoints and roster masks", SwordStatuses);
        run("Eureka Excalibur cold Cast before status sword capture and no late binding", ColdSword);
        run("Eureka Excalibur two different Steelstrike Finish scopes and native-only raidwide clear", SteelFinish);
        run("Eureka Excalibur ThermalSides two patterns source slots complement clock and effect-only clear", Thermal);
        run("Eureka Excalibur cross unlimited FIFO first six fixed angles effect-only promotion", Crosses);
        run("Eureka Excalibur ordinary source snapshot shared circle arbitrary Finish and no timeout", Ordinary);
        run("Eureka Excalibur public output hard safe side and owner revoke", Output);
        run("Eureka Excalibur safe sword polygons source distance interior boundary and degenerate corners", SteelPolygons);
        run("Eureka final bosses missing viewer keeps sequence history and wipe clears all controls", Lifecycle);
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Catalog()
    {
        var a = new Fixture(1107); var b = new Fixture(1108);
        Check(a.E.Active?.BossOid == 0x3D23 && b.E.Active?.BossOid == 0x3CFC
            && a.E.Arena!.GetBounds().Width == 40 && b.E.Arena!.Position == b.Center, "Mapping/arena.");
        Check(a.E.Definitions[0].Casts.Length == 9 && b.E.Definitions[0].Casts.Length == 20
            && a.E.Definitions[0].Casts.Concat(b.E.Definitions[0].Casts).All(c => c.Shape == null)
            && !EurekaOrthosCatalog.Owns(1108, 0x3CFD), "Source activated rules/ownership.");
    }
    private static void IconOrder()
    {
        var f = new Fixture(1107);
        f.Set(3, a => a with { Position = a.Position + new Vector3(.007f, 0, .009f), Rotation = .7f }); f.Sync();
        f.Icon(3, 999, 0);
        Check(f.E.EurekaAdministratorCombo[0].Activation == Start
            && f.E.EurekaAdministratorCombo[0].Origin == DungeonEncounter.QuantizedDungeonPosition(f.E.Actors[3].Position),
            "Unknown icon or quantization.");
        for (var i = 0; i < 5; ++i) f.Icon(4, 999, 0);
        Check(f.E.EurekaAdministratorCombo.Select(a => a.Generation).SequenceEqual([6L, 1L, 2L, 3L, 4L, 5L]),
            "Six equal-clock items did not use right-pivot sort.");
        f.Icon(5, 390, 0); f.Icon(4, 391, 0); f.Icon(3, 392, 0);
        Check(f.E.EurekaAdministratorCombo[^3].Activation == Start.AddSeconds(7d)
            && f.E.EurekaAdministratorCombo[^2].Rotation == 0
            && f.E.EurekaAdministratorCombo[^1].Activation == Start.AddSeconds(14d), "Icon source/delays.");
        f.Icon(1, 390, 0);
        Check(f.E.EurekaAdministratorCombo.Count == 9, "Boss was treated as interceptor.");
    }
    private static void ComboWindow()
    {
        var f = new Fixture(1107);
        f.Icon(3, 999, 0); f.Icon(4, 999, 1); f.Icon(5, 999, 5); f.Icon(3, 999, 5.01);
        var visible = f.E.AmbientAoes().Where(a => a.Id.StartsWith("eureka/administrator/")).ToArray();
        Check(visible.Length == 3 && visible[0].Navigate && !visible[1].Navigate && !visible[2].Navigate
            && visible[0].Color == new Vector4(1, 1, 0, 1), "Strict one/five boundaries.");
        f.Cast(31451); f.End();
        Check(f.E.EurekaAdministratorCombo.Count == 3, "Global Finish did not pop first.");
        f.Now = Start.AddSeconds(100); f.Sync();
        Check(f.E.EurekaAdministratorCombo.Count == 3, "Clock expired icon FIFO.");
    }
    private static void Lines()
    {
        var f = new Fixture(1107);
        foreach (var source in new ulong[] { 1, 2, 3, 4, 5 }) f.Cast(31452, source);
        var lines = f.E.AmbientAoes().Where(a => a.Id.StartsWith("eureka/administrator/")).ToArray();
        Check(lines.Length == 4 && lines.Count(a => a.Navigate) == 2 && lines.Count(a => a.Color != null) == 2, "Line windows.");
        f.Icon(3, 390, .5);
        Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("eureka/administrator/")) == 1, "Combo did not hide lines.");
        f.End(1);
        Check(f.E.EurekaAdministratorCombo.Count == 0 && f.E.EurekaAdministratorLines.Count == 4, "Hidden line did not consume.");
        f.End(2); f.End(3);
        lines = f.E.AmbientAoes().Where(a => a.Id.StartsWith("eureka/administrator/")).ToArray();
        Check(lines.Length == 2 && lines.All(a => a.Navigate && a.Color == null), "Ordinary colors stuck.");
    }
    private static void Finishes()
    {
        var f = new Fixture(1107); f.Icon(3, 390, 0); f.Cast(31453); f.End();
        Check(f.E.EurekaAdministratorCombo.Count == 0, "Wrong source failed global pop.");
        f.Cast(32832); f.End(); f.Cast(32833); f.Cast(31461, 3); f.Cast(31456, 1);
        Check(f.E.PredictedDamage.Count(p => p.DamageType == "Raidwide") == 2
            && f.E.Hints(f.Center).Contains("Raidwide"), "Raidwide prediction.");
        f.Set(101, a => a with { Dead = true }); f.Sync();
        Check(f.E.PredictedDamage.Count(p => p.DamageType == "Raidwide") == 1, "Current alive mask.");
        f.End(1);
        Check(!f.E.PredictedDamage.Any(), "Raidwide Finish.");
    }
    private static void Steel()
    {
        var f = new Fixture(1108); f.Cast(31333, rotation: .2f);
        var original = f.E.EurekaSteelstrikes.ToArray();
        Check(original.Length == 10 && original.All(a => a.Activation == default)
            && original[0].Rotation == original[0].Cast.Rotation + -45 * (MathF.PI / 180f),
            $"Initial blades: count={original.Length}, rotation={original.FirstOrDefault()?.Rotation:R}, cast={original.FirstOrDefault()?.Cast.Rotation:R}.");
        f.End(); f.Cast(31353);
        Check(f.E.EurekaSteelstrikes.Count == 20
            && f.E.EurekaSteelstrikes.Skip(10).All(a => a.Shape == new RectangleTemplate(4, 40)
                && a.Activation == a.Cast.FinishAt.AddSeconds(3.4d)), "Delayed full strips.");
        f.End(); f.Cast(31335); f.End(); f.Cast(31333, rotation: .3f);
        Check(f.E.EurekaSteelstrikes.Count == 15 && f.E.EurekaSteelAngles.Count == 15
            && f.E.EurekaSteelstrikes[1].Rotation == f.E.EurekaSteelstrikes[0].Rotation + 120 * (MathF.PI / 180f),
            "Subsequent 5x3 angle order.");
    }
    private static void SwordStatuses()
    {
        var f = new Fixture(1108); f.Cast(31333);
        var blades = f.E.EurekaSteelstrikes.ToArray();
        for (var i = 9; i >= 0; --i)
        {
            var id = (ulong)(200 + i);
            f.Actors.Add(new(id, 0x3CFD, f.Center, Rotation: blades[i].Rotation,
                StatusSlots: [new(2552, (ushort)(i % 2 == 0 ? 0x219 : 0x21A), 0, Start.AddSeconds(100)) { SlotIndex = 3 }]));
        }
        f.Sync(); f.Status(100, 3411); f.Cast(31330);
        var safe = f.E.EurekaSteelSafeSwords();
        Check(f.E.EurekaFireSwords.Count == 5 && f.E.EurekaIceSwords.Count == 5
            && safe.Length == 2 && safe[0].Rotation == blades[0].Rotation && safe[1].Rotation == blades[8].Rotation,
            "Exact five sorting / endpoints.");
        Check(safe.All(s => blades.Contains(s)) && f.E.Hints(f.Center).Contains("Get hit by a fire blade!"), "Original sword snapshot/text.");
        f.E.CurrentPartyMembers = [new(1, "", 100), new(0, "", 101)]; f.Sync();
        Check(f.E.EurekaSteelSafeSwords().Length == 0, "Soul state followed identity instead of source slot.");
        f.Sync(101);
        Check(f.E.EurekaSteelSafeSwords().Length == 2, "Stored slot was not selected by new viewer.");
    }
    private static void ColdSword()
    {
        var e = new DungeonEncounter(1108);
        var c = new Vector3(-600, 0, -300);
        e.CurrentPartyMembers = [new(0, "", 100)];
        e.Synchronize([new(1, 0x3CFC, c, InCombat: true, Cast: new(31333, c, 0, Start, Start.AddSeconds(5))),
            new(2, 0x3CFD, c, Rotation: -45 * (MathF.PI / 180f),
                StatusSlots: [new(2552, 0x219, 0, Start.AddSeconds(100))]),
            new(100, 0, c, Player: true, PartySlot: 0)], c, Start, playerId: 100);
        Check(e.EurekaFireSwords.Count == 1, "Cold Cast was not replayed before status.");
        var f = new Fixture(1108); f.Status(3, 2552, 0x219); f.Cast(31333);
        Check(f.E.EurekaFireSwords.Count == 0, "Status retroactively rebound to future sword.");
    }
    private static void SteelFinish()
    {
        var f = new Fixture(1108); f.Cast(31333); f.End(); f.Cast(31330); f.End(); f.Cast(31334); f.End();
        Check(f.E.EurekaSteelstrikes.Count == 0 && f.E.EurekaSteelAngles.Count == 10 && f.E.EurekaNextIce == true,
            "First Steelstrike cleared retained state.");
        f.Cast(31353); f.End();
        Check(f.E.EurekaSteelstrikes.Count == 10, "Retained angles did not build delayed strips.");
        f.E.Effect(2, 31331, 7, f.Now);
        Check(f.E.EurekaNextIce == null && f.E.EurekaSteelstrikes.Count == 10, "Raidwide clear scope.");
        f.Cast(31337); f.End();
        Check(f.E.EurekaSteelAngles.Count == 0 && f.E.EurekaSteelstrikes.Count == 0, "Full Finish scope.");
    }
    private static void Thermal()
    {
        var f = new Fixture(1108); f.Status(100, 3410); f.Cast(31350);
        var pattern = f.E.EurekaThermalPattern!;
        var sides = f.E.EurekaThermalSides()!.Value;
        Check(sides.Danger.X == f.Center.X + 4 && sides.Safe.X == f.Center.X - 4
            && sides.Activation == pattern.Cast.FinishAt.AddSeconds(1.9d), "Pattern/clock/slot.");
        Check(f.E.EurekaThermalHintRisky(f.Center - Vector3.UnitX * 10) == false
            && f.E.EurekaThermalHintRisky(f.Center + Vector3.UnitX * 10) == true,
            "Safe-side neutral hint lost its risk model.");
        f.End(); f.Now = f.Now.AddSeconds(100); f.Sync();
        Check(ReferenceEquals(f.E.EurekaThermalPattern, pattern), "Finish/time cleared pattern.");
        f.E.Effect(2, 31351, 7, f.Now);
        Check(f.E.EurekaThermalPattern == null, "Full thermal effect did not clear.");
        f.Cast(31349);
        Check(f.E.EurekaThermalSides()!.Value.Danger.X == f.Center.X - 4, "Ice pattern used fire soul.");
    }
    private static void Crosses()
    {
        var f = new Fixture(1108);
        foreach (var source in new ulong[] { 1, 2, 3, 4 }) f.Cast(31346, source);
        var crosses = f.E.EurekaIceCrosses.ToArray();
        Check(crosses.Length == 8 && crosses[0].Rotation == 44.998f * (MathF.PI / 180f)
            && crosses[1].Rotation == -.003f * (MathF.PI / 180f)
            && crosses[0].Activation == crosses[0].Cast.FinishAt.AddSeconds(5.2d), "Cross fixed angles/clock.");
        f.End(1); f.Now = f.Now.AddSeconds(100); f.Sync();
        Check(f.E.EurekaIceCrosses.Count == 8
            && f.E.AmbientAoes().Count(a => a.Shape is DungeonCrossTemplate) == 6, "Cross Finish/time/display cap.");
        f.E.Effect(2, 31348, 7, f.Now); f.E.Effect(2, 31348, 7, f.Now);
        Check(ReferenceEquals(f.E.EurekaIceCrosses[0], crosses[2]), "Same-sequence effect deduplicated or wrong FIFO.");
    }
    private static void Ordinary()
    {
        var f = new Fixture(1108); f.Cast(31346); var circle = f.E.AmbientAoes().First(a => a.Shape is CircleTemplate);
        f.Set(2, a => a with { Position = a.Position + Vector3.UnitZ * 12 }); f.Sync();
        Check(f.E.AmbientAoes().Any(a => a.Id == circle.Id && a.Position == f.Center), "Source snapshot moved.");
        f.Cast(999, 2); f.End(2);
        Check(!f.E.AmbientAoes().Any(a => a.Shape is CircleTemplate), "Unknown Finish failed shared circle removal.");
        f.Cast(31341); f.Now = f.Now.AddSeconds(100); f.Sync();
        Check(f.E.AmbientAoes().Any(a => a.Shape is MasterPlayer.Mechanics.PilgrimsTraverse.PtDonutSectorTemplate
            { Inner: 17, Outer: 22 }), "Third sector or no timeout.");
    }
    private static void Output()
    {
        var f = new Fixture(1108); f.Status(100, 3410); f.Cast(31350);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
        var draw = new ScriptDrawApi(); var output = new DungeonOutput(safe, draw, safe.Manager);
        output.Apply(f.E, f.Now, false);
        Check(draw.Previews.Keys.Any(k => k.Contains("/thermal/safe"))
            && safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count >= 2,
            "Green display did not publish a separate inverse hard constraint.");
        output.Clear();
        Check(draw.Previews.Count == 0 && safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0
            && f.E.EurekaThermalPattern != null, "Owner clear/model separation.");
    }
    private static void Lifecycle()
    {
        var f = new Fixture(1108); f.Cast(31346);
        f.Actors.RemoveAll(a => a.Id == 100); f.Sync();
        Check(f.E.EurekaIceCrosses.Count == 2 && f.E.Active != null, "Missing viewer erased global history.");
        f.E.EurekaDirector(0x40000005);
        Check(f.E.EurekaIceCrosses.Count == 0 && f.E.Active == null, "Wipe retained history.");
    }

    private static void SteelPolygons()
    {
        var f = new Fixture(1108);
        f.Cast(31333);
        var blade = f.E.EurekaSteelstrikes[0];
        (float First, float Last)[] angles = [(0, 0), (0, MathF.PI), (0, MathF.PI / 2),
            (0, MathF.PI / 4), (.2f, .2f + MathF.PI), (.3f, 2.7f), (.7f, .70001f),
            (-MathF.PI + .0001f, MathF.PI - .0001f), (.2f, .2f + 80 * (MathF.PI / 180f)),
            (.7128106355667114f, 2.109074115753174f), (0, .20441342890262604f)];
        var comparisons = 0;
        foreach (var (first, last) in angles)
        {
            var polygons = DungeonOutput.EurekaSteelForbiddenPolygons(blade with { Rotation = first },
                    blade with { Rotation = last }, f.Center, 19.5f)
                .Select(p => new WorldPolygonGeometry(p)).ToArray();
            Check(polygons.Length is >= 2 and <= 4, "Safe strip complement lost nondegenerate regions.");
            // Independently evaluate the two source SDInvertedRect distances and
            // their SDIntersection. The full +/-20 strips extend beyond the circle.
            float Rect(float angle, float x, float z)
            {
                var (sin, cos) = Math.SinCos(angle);
                var along = x * (float)sin + z * (float)cos;
                var side = x * (float)cos - z * (float)sin;
                return MathF.Max(MathF.Max(along - 20, -along - 20), MathF.Max(side - 2, -side - 2));
            }
            for (var x = -19.5f; x <= 19.5f; x += .5f)
                for (var z = -19.5f; z <= 19.5f; z += .5f)
                {
                    if (x * x + z * z > 19.5f * 19.5f) continue;
                    var sourceDistance = MathF.Max(-Rect(first, x, z), -Rect(last, x, z));
                    var point = f.Center + new Vector3(x, 0, z);
                    if (MathF.Abs(sourceDistance) > .0001f)
                        Check(polygons.Any(p => p.Contains(point, 0)) == (sourceDistance <= 0),
                            $"Safe strip interior {first:R}/{last:R} at {x}/{z} differs.");
                    ++comparisons;
                }
            if (first == 0 && last == 0)
            {
                Check(polygons.Any(p => p.Contains(f.Center + new Vector3(2, 0, -12), 0)),
                    "Exact axis-aligned source zero boundary was lost.");
                Check(!polygons.Any(p => p.Contains(f.Center - Vector3.UnitZ * 12, 0)),
                    "Rear half of complete safe strip became forbidden.");
            }
        }
        // Sweep the real first-pattern 80-degree pair through every degree.
        // World-coordinate rounding must never create invalid polygons.
        for (var degree = 0; degree < 360; ++degree)
        {
            var first = degree * (MathF.PI / 180f);
            foreach (var polygon in DungeonOutput.EurekaSteelForbiddenPolygons(blade with { Rotation = first },
                blade with { Rotation = first + 80 * (MathF.PI / 180f) }, f.Center, 19.5f))
                _ = new WorldPolygonGeometry(polygon);
        }
        Check(comparisons > 50000, "Insufficient safe-strip oracle coverage.");
    }
}
