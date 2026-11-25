using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleGoldor(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object? Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj);
        object Encounter()
        {
            Check(manager.Running.ContainsKey(guid), $"Goldor script stopped: {manager.ErrorMessage}");
            object script = Value(manager.Running[guid], "Script")!;
            return script.GetType().BaseType!.GetField("_encounter", flags)!.GetValue(script)!;
        }
        object State() => Value(Encounter(), "CarnivaleGoldor")!;
        object[] Values(string property) => ((IEnumerable)Value(Encounter(), property)!).Cast<object>().ToArray();
        object[] List(string property) => ((IEnumerable)Value(State(), property)!).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")!).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: [new { Slot = 0, EntityId = 100ul }]);
        string Cancel(double t, ulong source, uint action) => JsonSerializer.Serialize(new { t, kind = "CancelCast",
            type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(uint oid = 0x3FA7, bool combat = true) => [Actor(1, oid, center, combat: combat),
            Actor(2, 0x233C, center), Actor(100, 0, center + Vector3.UnitZ * 5)];

        foreach (var (oid, name) in new[] { (0x3FA5u, "黄金斗士——金德尔·第一幕"), (0x3FA7u, "黄金斗士——金德尔·第二幕") })
        {
            load(guid, [Header(796), At(0, Initial(oid, false), true),
                At(.1, [Actor(1, oid, center, combat: true), Actor(10, 0x3FA6, center), Actor(11, 0x3FAB, center),
                    Actor(12, 0x3FAC, center), Actor(13, 0x3FA8, center), Actor(14, 0x3FA9, center)]),
                At(1, [Actor(1, oid, center, dead: true)]), At(2, [])]);
            controller.Seek(.01);
            Check(Value(Encounter(), "Active") == null && Shown("carnivale/precombat/arena").Length == 1
                && (Value(Encounter(), "CarnivalePrecombatHint") != null) == (oid == 0x3FA5), "Goldor preparation/arena.");
            controller.Seek(.5);
            Check(Shown("carnivale/enemy/").Length == (oid == 0x3FA5 ? 3 : 1), "Goldor markers.");
            var source = Assembly.Load("MasterPlayer.MechanicRuntime");
            var bounds = source.GetType("BossMod.Global.MaskedCarnivale.Layouts")!.GetField("CircleSmall")!.GetValue(null)!;
            var wdir = source.GetType("BossMod.WDir")!;
            dynamic arena = Value(Encounter(), "Arena")!;
            var count = 0;
            for (var x = 74.13f; x < 126; x += .67f)
                for (var z = 74.17f; z < 126; z += .71f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(wdir, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Goldor source arena.");
                    ++count;
                }
            controller.Seek(1.5);
            Check(Value(Encounter(), "Active") == null, "Goldor primary lifecycle.");
            controller.Seek(.5);
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(.6);
            Check(previews().Length == 0 && dangers() == 0, "Goldor disabled owner.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage32 {oid:X} source arena oracle {count} points first preparation orange objects second primary only default lifecycle disabled owner reverse Seek");
        }

        foreach (var (oid, action, hazard) in new[] { (0x3FA5u, 34448u, true), (0x3FA5u, 34449u, true),
            (0x3FA5u, 34450u, true), (0x3FA5u, 34446u, true), (0x3FA5u, 34455u, true),
            (0x3FA5u, 34462u, false), (0x3FA5u, 34463u, false), (0x3FA5u, 34589u, false),
            (0x3FA7u, 34449u, true), (0x3FA7u, 34465u, true), (0x3FA7u, 34467u, true) })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, rotation: .7f))]),
                EffectEvent(1, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(.5);
            Check(dangers() == (hazard ? 1 : 0) && Values("PredictedDamage").Length == 0, "Goldor ordinary scope.");
            controller.Seek(8.5);
            Check(dangers() == (hazard ? 1 : 0), "Goldor effect/timeout.");
            controller.Seek(9.5);
            Check(dangers() == 0, "Goldor real Finish.");
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage32 ordinary sixty ray Burn10 Thunder6 Cross100 half3point5 Swing12 shared Fire8 first simple Fire8 second source Finish summon Rupture interrupt pure hints");

        load(guid, [Header(796), At(0, Initial(0x3FA5), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(34463, 10, center))]),
            At(.2, [Actor(3, 0x233C, center, cast: Cast(34463, 2, center, startedAt: .2))]),
            Cancel(1, 2, 34463), Cancel(2, 3, 34463), At(3, [])]);
        controller.Seek(.5);
        Check(Hints().Single(h => h.StartsWith("Kill slime ASAP!")).EndsWith("9.9s left"), "First Rupture current NPC clock.");
        controller.Seek(1.5);
        Check(Hints().Single(h => h.StartsWith("Kill slime ASAP!")).EndsWith("1.0s left"), "Rupture second insertion promotes.");
        controller.Seek(2.5);
        Check(!Hints().Any(h => h.StartsWith("Kill slime ASAP!")), "Rupture last Finish clears.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage32 Rupture first accepted caster current NPC remaining countdown later earlier deadline cannot reorder Finish promotion reverse Seek");

        load(guid, [Header(796), At(0, Initial().Append(Actor(10, 0x3FA8, center, dead: true)).ToArray(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(34464, 4, center, rotation: .7f))]),
            Cancel(1, 2, 34464), At(2, [])]);
        controller.Seek(.5);
        dynamic singleBeam = Value(Shown("carnivale/goldor/beam").Single(), "Geometry")!;
        Check(List("BeamOutput").Length == 1 && dangers() == 1
            && (bool)singleBeam.Contains(center + new Vector3(MathF.Sin(.7f), 0, MathF.Cos(.7f)) * 39, 0f)
            && !(bool)singleBeam.Contains(center - Vector3.UnitZ * 30, 0f), "Exactly one dead Marionette analytic cone.");
        controller.Seek(1.5);
        Check(dangers() == 0, "Single Beam real Finish.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage32 exactly one raw Marionette including dead analytic R40 total120 locked direction and real Finish");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(34457, 4, center))]), Cancel(1, 2, 34457),
            At(2, [Actor(3, 0x233C, center, cast: Cast(34458, 4, center, startedAt: 2))]), Cancel(3, 3, 34458),
            At(4, [Actor(4, 0x233C, center, cast: Cast(34459, 4, center, startedAt: 4))]), Cancel(5, 4, 34459), At(6, [])]);
        controller.Seek(.5);
        Check(List("Quakes").Length == 1 && (int)Value(List("Quakes").Single(), "Order")! == 0 && dangers() == 1, "Quake first only.");
        controller.Seek(1.5);
        Check((int)Value(List("Quakes").Single(), "Order")! == 1
            && (DateTime)Value(List("Quakes").Single(), "Activation")! == Start.AddSeconds(2.5), "Quake cancel processing clock.");
        controller.Seek(3.5);
        Check((int)Value(List("Quakes").Single(), "Order")! == 2 && dangers() == 1, "Quake next independent source.");
        controller.Seek(5.5);
        Check(List("Quakes").Length == 0 && dangers() == 0, "Quake third.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage32 Quake current R10 ring10to20 ring20to30 source independent matching Finish cancellation advances processing1point5 native windows reverse Seek");

        foreach (var oid in new uint[] { 0x3FA5, 0x3FA7 })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(34451, 4, center)),
                    Actor(3, 0x233C, center, cast: Cast(34453, 4, center))]),
                Cancel(1, 2, 34451), Cancel(2, 3, 34453), TreasureNativeEffect(3, 999, 34452, 1, 0, []),
                At(20, []), TreasureNativeEffect(21, 3, 34452, 2, 0, []),
                TreasureNativeEffect(22, 2, 34454, 3, 0, []), At(23, [])]);
            controller.Seek(20.5);
            var delay = oid == 0x3FA5 ? (double).8f : .8d;
            Check(Values("PredictedDamage").Length == 2 && dangers() == 0
                && Values("PredictedDamage").All(d => (DateTime)Value(d, "Activation")! ==
                    ((DateTime)Value(Value(State(), "Gravity")!, "FinishAt")!).AddSeconds(delay)), "Global delayed clocks.");
            controller.Seek(21.5);
            Check(Value(State(), "Gravity") == null && Value(State(), "Thunder") != null && Values("PredictedDamage").Length == 1, "Effect scope.");
            controller.Seek(22.5);
            Check(Values("PredictedDamage").Length == 0, "Known source zero target clears.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage32 independent global Gravity Thunder delay exact float first double second cancel expiry unknown source retained zero target native effects clear reverse Seek");

        var arrangements = new (Vector3 Position, float Rotation)[][]
        {
            [(center, 0), (center, 0), (center + Vector3.UnitX * 40, .3f), (center - Vector3.UnitX * 40, -.3f)],
            [(new(90, 0, 90), .6f), (new(110, 0, 90), 2.2f), (new(110, 0, 110), 3.7f), (new(90, 0, 110), 5.3f)],
            [(center, .37f), (center, .37f), (center, .37f), (center, .37f)],
        };
        foreach (var arrangement in arrangements)
        {
            var initial = Initial().Concat(Enumerable.Range(0, 4).Select(i => Actor((ulong)(10 + i), 0x3FA8, arrangement[i].Position))).ToArray();
            var casts = Enumerable.Range(0, 4).Select(i => Actor((ulong)(10 + i), 0x3FA8, arrangement[i].Position,
                cast: Cast(34464, 7 + i, arrangement[i].Position, rotation: arrangement[i].Rotation))).ToArray();
            load(guid, [Header(796), At(0, initial, true), At(.1, casts), Cancel(20, 10, 34464), At(21, [])]);
            controller.Seek(.5);
            var accepted = ((IEnumerable)Value(State(), "BeamOutput")!).Cast<object>().ToArray();
            Check(accepted.Length == 4 && accepted.Select(c => (float)Value(c, "Rotation")!).SequenceEqual(arrangement.Select(a => a.Rotation))
                && Shown("carnivale/goldor/beam").Length == 1 && dangers() == 1, "Four source model and native outputs.");
            dynamic geometry = Value(Shown("carnivale/goldor/beam").Single(), "Geometry")!;
            var expected = GoldenBeamOracle(accepted.Select(c => ((Vector3)Value(c, "Location")!, (float)Value(c, "Rotation")!)).ToArray(),
                out var sourceContours);
            var fills = ((IEnumerable)Value(geometry, "FillPolygons")!).Cast<IEnumerable>()
                .Select(p => p.Cast<Vector3>().ToArray()).ToArray();
            bool Filled(Vector3 point) => fills.Any(t => TriangleContains(point, t));
            var count = 0;
            for (var x = 68.13f; x < 132; x += .61f)
                for (var z = 68.17f; z < 132; z += .67f)
                {
                    Check((bool)geometry.Contains(new Vector3(x, 0, z), 0f) == expected(new Vector3(x, 0, z)), $"GoldenBeam oracle {x}/{z}.");
                    Check(Filled(new(x, 0, z)) == expected(new(x, 0, z)), $"GoldenBeam fill oracle {x}/{z}.");
                    ++count;
                }
            var boundaryChecks = 0;
            foreach (var contour in sourceContours)
                for (var i = 0; i < contour.Length; ++i)
                {
                    var a = contour[i];
                    var b = contour[(i + 1) % contour.Length];
                    var edge = b - a;
                    if (edge.LengthSquared() < .000001f) continue;
                    var normal = Vector3.Normalize(new Vector3(-edge.Z, 0, edge.X)) * .001f;
                    foreach (var t in new[] { .01f, .5f, .99f })
                        foreach (var side in new[] { -1, 1 })
                        {
                            var point = Vector3.Lerp(a, b, t) + normal * side;
                            Check((bool)geometry.Contains(point, 0f) == expected(point), $"GoldenBeam boundary side {point}.");
                            Check(Filled(point) == expected(point), $"GoldenBeam boundary fill {point}.");
                            ++boundaryChecks;
                        }
                }
            controller.Seek(20.5);
            Check(Shown("carnivale/goldor/beam").Length == 0 && dangers() == 0, "Any Beam Finish clears all.");
            controller.Seek(.5);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage32 GoldenBeam upstream folded edge2 polygons Check and fill oracle {count} points {boundaryChecks} boundary sides holes touching vertices parity native preview navigation any Finish reverse Seek");
        }

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(34460, 4, center)),
                Actor(3, 0x233C, center, cast: Cast(34468, 4, center)),
                Actor(4, 0x233C, center, cast: Cast(34466, 4, center))]),
            Cancel(1, 2, 34460), Cancel(2, 3, 34468), Cancel(3, 4, 34466), At(4, [])]);
        controller.Seek(.5);
        Check(Values("CarnivaleCastKnockbacks").Length == 3 && Shown("carnivale/goldor/kb/").Length > 0
            && Values("PredictedDamage").Length == 2 && dangers() == 0, "Independent KB and raidwide components.");
        controller.Seek(3.5);
        Check(Shown("carnivale/goldor/kb/").Length == 0 && Values("PredictedDamage").Length == 0, "Finish KB/raidwide.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage32 three independent Away10 Aero Rush Pull30 clamped component models projections strict immunity adapter Finish no extra KB navigation reverse Seek");
    }

    private static bool TriangleContains(Vector3 point, Vector3[] triangle)
    {
        static double Cross(Vector3 a, Vector3 b, Vector3 p) =>
            ((double)b.X - a.X) * ((double)p.Z - a.Z) - ((double)b.Z - a.Z) * ((double)p.X - a.X);
        var a = Cross(triangle[0], triangle[1], point);
        var b = Cross(triangle[1], triangle[2], point);
        var c = Cross(triangle[2], triangle[0], point);
        return a >= 0 && b >= 0 && c >= 0 || a <= 0 && b <= 0 && c <= 0;
    }

    private static Func<Vector3, bool> GoldenBeamOracle((Vector3 Position, float Rotation)[] casts, out Vector3[][] contours)
    {
        var source = Assembly.Load("MasterPlayer.MechanicRuntime");
        Type T(string name) => source.GetType("BossMod." + name, true)!;
        object Pos(float x, float z) => Activator.CreateInstance(T("WPos"), [x, z])!;
        object Angle(float value) => Activator.CreateInstance(T("Angle"), [value])!;
        var center = Pos(100, 100);
        Array Shapes(params object[] values)
        {
            var result = Array.CreateInstance(T("Shape"), values.Length);
            for (var i = 0; i < values.Length; ++i) result.SetValue(values[i], i);
            return result;
        }
        var cones = casts.Select(c => Activator.CreateInstance(T("ConeV"),
            [Pos(c.Position.X, c.Position.Z), 40f, Angle(c.Rotation), Angle(60f * (MathF.PI / 180f)), 2])!).ToArray();
        object Custom(string operand) => T("AOEShapeCustom").GetConstructors().Single().Invoke(
            [Shapes(cones[0]), null, Shapes(cones[1], cones[2], cones[3]), Enum.Parse(T("OperandType"), operand),
                Activator.CreateInstance(T("WPos")), false]);
        var intersection = Custom("Intersection");
        var xor = Custom("Xor");
        var combined = T("AOEShapeCustom").GetMethod("GetCombinedPolygon")!;
        object Operand(object polygon) => Activator.CreateInstance(T("PolygonClipper+Operand"), [polygon])!;
        var clipper = Activator.CreateInstance(T("PolygonClipper"))!;
        var result = T("PolygonClipper").GetMethod("Union")!.Invoke(clipper,
            [Operand(combined.Invoke(intersection, [center])!), Operand(combined.Invoke(xor, [center])!),
                Enum.Parse(source.GetType("Clipper2Lib.FillRule", true)!, "NonZero")])!;
        T("AOEShapeCustom").GetField("Polygon")!.SetValue(intersection, result);
        var contourList = new List<Vector3[]>();
        foreach (var part in ((IEnumerable)result.GetType().GetField("Parts")!.GetValue(result)!).Cast<object>())
        {
            var vertices = ((IEnumerable)part.GetType().GetField("Vertices")!.GetValue(part)!).Cast<object>().Select(p =>
                new Vector3(100 + (float)p.GetType().GetField("X")!.GetValue(p)!, 0,
                    100 + (float)p.GetType().GetField("Z")!.GetValue(p)!)).ToArray();
            var starts = ((IEnumerable)part.GetType().GetField("HoleStarts")!.GetValue(part)!).Cast<int>().Prepend(0).Append(vertices.Length).ToArray();
            for (var i = 1; i < starts.Length; ++i) contourList.Add(vertices[starts[i - 1]..starts[i]]);
        }
        contours = contourList.ToArray();
        var check = T("AOEShapeCustom").GetMethod("Check", [T("WPos"), T("WPos"), T("Angle")])!;
        return p => (bool)check.Invoke(intersection, [Pos(p.X, p.Z), center, Angle(0)])!;
    }
}
