using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleLastSong(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Songs() => ((IEnumerable)Value(Encounter(), "CarnivaleLastSongs")).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        object[] BackgroundSongs(uint oid)
        {
            var modules = ((IEnumerable)Encounter().GetType().GetField("carnivaleLoaded", flags)!.GetValue(Encounter())!).Cast<object>();
            var module = modules.Single(m => (uint)Value(Value(Value(m, "Primary"), "Actor"), "Oid") == oid);
            return ((IEnumerable)Value(module, "LastSongs")).Cast<object>().ToArray();
        }
        Vector3[] WorldFill(object geometry)
        {
            var assembly = geometry.GetType().Assembly;
            var host = assembly.GetType("MasterPlayer.Radar.HostDrawingGeometry")!.GetMethod("Convert")!.Invoke(null, [geometry])!;
            var batches = Activator.CreateInstance(typeof(List<>).MakeGenericType(assembly.GetType("MasterPlayer.Drawing.RenderBatch")!))!;
            var item = new PromeRotation.Spatial.Drawing.DrawRenderItem("last-song",
                (PromeRotation.Spatial.Geometry.IGeometryShape)host, new PromeRotation.Spatial.Drawing.DrawStyle(Vector4.One, null));
            assembly.GetType("MasterPlayer.Drawing.MeshBuilder")!.GetMethod("AddGeometry", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [batches, item, Vector4.Zero, 0f]);
            return ((IEnumerable)batches).Cast<object>().SelectMany(b =>
                ((IEnumerable)Value(b, "Vertices")).Cast<object>().Select(v => (Vector3)v.GetType().GetField("Position", flags)!.GetValue(v)!)).ToArray();
        }
        bool Filled(Vector3[] triangles, Vector3 p)
        {
            float Cross(Vector3 a, Vector3 b, Vector3 c) => (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i]; var b = triangles[i + 1]; var c = triangles[i + 2];
                var winding = MathF.Sign(Cross(a, b, c));
                if (winding != 0 && winding * Cross(a, b, p) >= 0 && winding * Cross(b, c, p) >= 0 && winding * Cross(c, a, p) >= 0)
                    return true;
            }
            return false;
        }
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: party);
        string Cancel(double t, ulong source, uint action, double? eventTime = null)
            => JsonSerializer.Serialize(new
            {
                t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(eventTime ?? t) },
            });
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"), "LastSong replay leaked controls.");
        }
        var sourceAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var layouts = sourceAssembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!;
        var directionType = sourceAssembly.GetType("BossMod.WDir")!;
        var sourceCenter = Activator.CreateInstance(sourceAssembly.GetType("BossMod.WPos")!, [100f, 100f])!;
        object Direction(Vector3 point) => Activator.CreateInstance(directionType, [point.X - 100, point.Z - 100])!;
        object Oracle(string layout, Vector3 origin)
        {
            var blockers = layouts.GetField(layout + "Blockers", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
            var vertices = blockers.GetType().GetMethod("Visibility")!.Invoke(blockers, [Direction(origin)])!;
            var polygonShape = Activator.CreateInstance(sourceAssembly.GetType("BossMod.PolygonCustomRel")!, [vertices])!;
            var shapes = Array.CreateInstance(sourceAssembly.GetType("BossMod.Shape")!, 1);
            shapes.SetValue(polygonShape, 0);
            var constructor = sourceAssembly.GetType("BossMod.AOEShapeCustom")!.GetConstructors().Single();
            var args = constructor.GetParameters().Select(p => p.DefaultValue).ToArray();
            args[0] = shapes;
            args[4] = sourceCenter;
            var shape = constructor.Invoke(args);
            return shape.GetType().GetMethod("GetCombinedPolygon")!.Invoke(shape, [sourceCenter])!;
        }

        foreach (var stage in new[]
        {
            (Oid: 0x271Du, Name: "陪葬·第一幕", Layout: "Layout2Corners", Text: "Take cover behind a barricade!"),
            (Oid: 0x271Eu, Name: "陪葬·第二幕", Layout: "LayoutBigQuad", Text: "Use the cube to take cover!"),
        })
        {
            var compared = 0;
            foreach (var origin in new Vector3[]
            {
                new(100, 0, 112), new(82, 0, 90), new(90, 0, 100), new(100, 0, 82),
                new(118, 0, 105), new(85, 0, 110), new(100, 0, 125), new(100, 0, 100),
            })
            {
                var cast = Cast(14756, 6, center + Vector3.UnitX * 15, startedAt: 0);
                load(guid, [Header(796), At(0, [Actor(1, stage.Oid, origin, combat: true), Actor(100, 0, center)], true),
                    At(.1, [Actor(1, stage.Oid, origin, combat: true, cast: cast, dead: true)]),
                    At(1, [Actor(1, stage.Oid, center, combat: true, cast: cast, dead: true, rotation: 2)]),
                    EffectEvent(7, 1, 14756, 1, center),
                    At(9, [Actor(1, stage.Oid, center, dead: true)]), At(10, [])]);
                controller.Seek(.5);
                Check(Value(Encounter(), "Active") != null && Songs().Length == 1 && dangers() == 1
                    && (Vector3)Value(Songs().Single(), "Origin") == origin && Shown("carnivale/last-song/").Length == 1,
                    "Death+Start prematurely ended Last Song or origin became spell location.");
                var oracle = Oracle(stage.Layout, origin);
                var contains = oracle.GetType().GetMethod("Contains")!;
                dynamic geometry = Value(Songs().Single(), "Visibility");
                var worldFill = WorldFill((object)geometry);
                Check(worldFill.Length != 0, "LastSong missing world fill.");
                for (var x = 74.137f; x < 126; x += .6f)
                    for (var z = 74.173f; z < 126; z += .6f)
                    {
                        var point = new Vector3(x, 0, z);
                        var expected = (bool)contains.Invoke(oracle, [Direction(point)])!;
                        Check((bool)geometry.Contains(point, 0f) == expected,
                            $"LastSong source visibility mismatch {stage.Oid:X}, origin={origin}, point={point}.");
                        Check(Filled(worldFill, point) == expected,
                            $"LastSong world mesh mismatch {stage.Oid:X}, origin={origin}, point={point}.");
                        ++compared;
                    }
                Vector3[] Contours(object shape) => ((IEnumerable)((dynamic)shape).GetContours()).Cast<IEnumerable>()
                    .SelectMany(c => c.Cast<Vector3>()).ToArray();
                var locked = Contours(Value(Songs().Single(), "Visibility"));
                controller.Seek(8);
                Check(Songs().Length == 1 && dangers() == 1 && Hints().Contains(stage.Text)
                    && (Vector3)Value(Songs().Single(), "Origin") == origin
                    && Contours(Value(Songs().Single(), "Visibility")).SequenceEqual(locked),
                    "LastSong origin/polygon changed after actor motion, predicted deadline or Effect.");
                Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 14756 })
                    && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 14757 }),
                    "LastSong takeover added Syrup component.");
                controller.Seek(9.5);
                Check(Value(Encounter(), "Active") == null && dangers() == 0 && previews().Length == 0, "Dead group Finish cleanup.");
                controller.Seek(.5);
                Check(dangers() == 1 && Songs().Length == 1, "LastSong reverse Seek.");
                Isolated();
                stop();
            }
            Console.WriteLine($"PASS native Carnivale {stage.Oid:X} production full visibility source oracle {compared} points eight caster origins separate spell location dead start actor moves effect deadline observed Finish reverse Seek");

            var p1 = new Vector3(100, 0, 112);
            var p2 = new Vector3(90, 0, 110);
            load(guid, [Header(796), At(0,
                    [Actor(1, stage.Oid, p1, combat: true), Actor(2, stage.Oid, p2), Actor(100, 0, center)], true),
                At(.1, [Actor(1, stage.Oid, p1, combat: true, cast: Cast(14756, 20, center)),
                    Actor(2, stage.Oid, p2, cast: Cast(14756, 20, center))]),
                Cancel(1, 1, 14756), At(2, []),
                At(3, [Actor(1, stage.Oid, p1, dead: true, cast: Cast(14756, 20, center)),
                    Actor(2, stage.Oid, p2, dead: true, cast: Cast(14756, 20, center))]), At(4, [])]);
            controller.Seek(.5);
            Check(Songs().Length == 2 && dangers() == 2 && Hints().Contains(stage.Text), "LastSong two casters.");
            controller.Seek(2.5);
            Check(Songs().Length == 1 && dangers() == 1 && !Hints().Contains(stage.Text)
                && !(bool)Value(Encounter(), "CarnivaleLastSongCasting"), "LastSong bool became nonempty caster count.");
            controller.Seek(3.5);
            Check(Value(Encounter(), "Active") == null && previews().Length == 0 && dangers() == 0,
                "All dead plus false source bool waited for remaining caster.");
            controller.Seek(.5);
            manager.SetMechanismEnabled(guid, stage.Name, false);
            controller.Seek(.6);
            Check(previews().Length == 0 && dangers() == 0 && Songs().Length == 0, "LastSong disabled owner.");
            manager.SetMechanismEnabled(guid, stage.Name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale {stage.Oid:X} production concurrent visibility independent hint bool first Finish clears only caster unchanged raw all dead completion disabled owner");
        }

        load(guid, [Header(796), At(0,
                [Actor(1, 0x271E, new(100, 0, 112), combat: true, cast: Cast(14756, 20, center, startedAt: 0)),
                    Actor(100, 0, center)], true),
            Cancel(1, 1, 14756), At(2, []),
            At(3, [Actor(1, 0x271E, new(90, 0, 110), combat: true,
                cast: Cast(14756, 20, center + Vector3.UnitX * 2, startedAt: 0))]),
            Cancel(3.1, 1, 14756, eventTime: 2.5), Cancel(4, 1, 14757), Cancel(5, 1, 14756), At(6, [])]);
        controller.Seek(.5);
        var generation = (long)Value(Songs().Single(), "Generation");
        controller.Seek(2.5);
        Check(dangers() == 0 && Songs().Length == 0, "LastSong cancelled raw revived.");
        controller.Seek(4.5);
        Check(dangers() == 1 && Songs().Length == 1 && (long)Value(Songs().Single(), "Generation") > generation
            && (Vector3)Value(Songs().Single(), "Origin") == new Vector3(90, 0, 110), "LastSong generation late/foreign Finish.");
        controller.Seek(5.5);
        Check(dangers() == 0 && Songs().Length == 0, "LastSong current cancel.");
        controller.Seek(4.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native Carnivale Stage14 production cancelled unchanged raw true new generation caster origin old and foreign Finish reverse Seek no actions");

        var a = new Vector3(100, 0, 112);
        var b = new Vector3(82, 0, 90);
        load(guid, [Header(796), At(0,
                [Actor(1, 0x271D, a, combat: true, cast: Cast(14756, 20, center)),
                    Actor(2, 0x271E, center), Actor(100, 0, center)], true),
            At(1, [Actor(1, 0x271D, b, combat: true, cast: Cast(14756, 20, center)), Actor(2, 0x271E, center, combat: true)]),
            At(2, [Actor(3, 0x271E, a, cast: Cast(14756, 20, center))]),
            Cancel(3, 3, 14756), At(4, [Actor(1, 0x271D, b, dead: true, cast: Cast(14756, 20, center))]),
            At(5, []), At(6, [])]);
        controller.Seek(1.5);
        Check((Vector3)Value(Songs().Single(), "Origin") == a
            && (Vector3)Value(BackgroundSongs(0x271E).Single(), "Origin") == b, "Background activation copied old lock or missed raw.");
        controller.Seek(4.001);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x271E && Songs().Length == 1
            && dangers() == 1 && (ulong)Value(Value(Songs().Single(), "Cast"), "Source") == 1
            && !(bool)Value(Encounter(), "CarnivaleLastSongCasting"), "Unload dropped shared accepted cast or reset independent bool.");
        controller.Seek(1.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native Carnivale Stage14 production activation replays accepted shared cast current actor lock background unload preserves cast and hint bool reverse Seek");

        foreach (var cancellation in new[] { 0, 1, 2 })
        {
            var records = new List<string>
            {
                Header(796), At(0, [Actor(1, 0x271E, a, combat: true), Actor(100, 0, center)], true),
                JsonSerializer.Serialize(new { t = .1, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent",
                    data = new { SourceId = 1ul, ActionId = 14756u, CastTime = 6f, EffectPosition = Pose(center),
                        SourcePosition = Pose(b), Timestamp = Start.AddSeconds(.1) } }),
                At(.35, []),
            };
            if (cancellation == 1) records.Add(Cancel(.37, 1, 14756));
            records.Add(At(.4, [Actor(1, 0x271E, a, combat: true, cast: Cast(14756, 6, center))]));
            if (cancellation == 2) records.Add(Cancel(.47, 1, 14756, eventTime: .37));
            records.Add(At(.5, [], removed: [100]));
            records.Add(At(.6, [Actor(100, 0, center)]));
            records.Add(At(1, []));
            load(guid, records);
            controller.Seek(.15);
            Check((Vector3)Value(Songs().Single(), "Origin") == a, "Packet SourcePosition replaced source actor origin.");
            controller.Seek(.36);
            Check(dangers() == 0 && Songs().Length == 0, "Missing raw grace did not close packet.");
            controller.Seek(.45);
            Check(Songs().Length == (cancellation == 1 ? 0 : 1) && dangers() == (cancellation == 1 ? 0 : 1), "Late first raw recovery/explicit cancel.");
            controller.Seek(.65);
            Check(Songs().Length == (cancellation != 0 ? 0 : 1) && dangers() == (cancellation != 0 ? 0 : 1),
                "Missing observer discarded generation receipt.");
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage14 production packet SourcePosition differs from actor late first raw inferred Finish recovery explicit cancellation final observer missing and restored receipts");

        load(guid, [Header(796), At(0, [Actor(1, 0x271E, a, combat: true, cast: Cast(14756, 20, center)),
                Actor(2, 0x271E, b), Actor(100, 0, center)], true),
            At(1, [Actor(2, 0x271E, b, cast: Cast(14756, 20, center, startedAt: 1))], removed: [100]),
            At(1.5, [Actor(2, 0x271E, b)]),
            At(2, [Actor(1, 0x271E, a, dead: true, cast: Cast(14756, 20, center)),
                Actor(2, 0x271E, b, dead: true), Actor(100, 0, center)]), At(3, [])]);
        controller.Seek(2.1);
        Check(Value(Encounter(), "Active") == null && dangers() == 0 && previews().Length == 0,
            "Observer absence skipped full background cast lifetime and changed independent bool.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage14 production observer absent full other caster Start Finish consumed independent bool and phase completion");
    }
}
