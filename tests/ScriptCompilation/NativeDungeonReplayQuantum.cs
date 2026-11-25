using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunQuantum(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string id = "masterplayer.quantum.final-verse";
        var center = new Vector3(-600, 0, -300);
        object Value(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, flags) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Encounter() => Value(Value(manager.Running[id], "Script"), "_encounter");
        object Model() => Value(Encounter(), "Quantum");
        object[] Items(object obj, string name) => ((IEnumerable)Value(obj, name)).Cast<object>().ToArray();
        int Stage() => (int)Value(Value(Model(), "Timeline"), "Stage");
        object[] Group(string prefix) => previews().Where(p => ((string)Value(p, "Id")).Contains(prefix)).ToArray();
        object WithStatuses(object actor, params object[] statuses)
        {
            var node = JsonSerializer.SerializeToNode(actor)!;
            node["Statuses"] = JsonSerializer.SerializeToNode(statuses);
            return node;
        }
        object Status(uint value, int slot, ushort param = 0)
            => new { Id = value, SlotIndex = slot, Param = param, ExpiresAt = Start.AddHours(1) };
        object Primary(bool combat = true) => WithStatuses(Actor(1, 0x48EE, center, combat: combat),
            Status(4571, 2, 8), Status(1018, 4, 8), Status(4570, 6, 8), Status(586, 9, 8));
        object Eater() => WithStatuses(Actor(2, 0x48EF, center), Status(4572, 5, 8));
        object[] roster = Enumerable.Range(0, 4).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        object[] Initial() => [Primary(), Eater(), Actor(3, 0x233C, center + Vector3.UnitX * 7),
            Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX),
            Actor(102, 0, center - Vector3.UnitX), Actor(103, 0, center - Vector3.UnitZ)];
        string Effect(double t, uint action, Vector3? target = null) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 3ul, ActionId = action, ActionType = 1,
                TargetPosition = target is { } p ? new { p.X, p.Y, p.Z } : null,
                GlobalSequence = 7u, Targets = Array.Empty<ulong>() },
        });
        string Map(double t, byte index, uint state) => JsonSerializer.Serialize(new
        {
            t, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16) },
        });
        string Icon(double t) => JsonSerializer.Serialize(new
        {
            t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = 100ul, TargetId = 101ul, MarkerId = 78u },
        });
        string Vfx(double t) => JsonSerializer.Serialize(new
        {
            t, kind = "TargetVfx", type = "PromeRotation.LogSystem.LogSystemTargetVfxEvent",
            data = new { SourceId = 100ul, TargetId = 101ul, VfxId = 40u },
        });
        {
            var before = Initial();
            before[0] = Primary(false);
            load(id, [Header(1311), Frame(1311, 0, before, full: true, party: roster),
                Frame(1311, .2, [Primary()], party: roster),
                Frame(1311, .3, [], party: [new { Slot = 0, EntityId = 101ul }]),
                Frame(1311, .4, [], party: roster)]);
            controller.Seek(.1);
            Check(Stage() == -1 && Group("quantum/").Length >= 2 && dangers() == 0,
                "Quantum precombat display expanded into navigation.");
            Check(Value((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), "ArenaBoundary") == null
                && Group("quantum/arena/display").Length == 1, "Quantum precombat boundary acquired navigation or lost display.");
            controller.Seek(.25);
            Check(Stage() >= 0 && dangers() > 0, "Quantum active roster did not acquire color navigation.");
            controller.Seek(.35);
            Check(Group("quantum/").Length >= 2 && dangers() == 0,
                "Quantum non-roster perspective retained personal navigation.");
            Check(Value((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), "ArenaBoundary") == null
                && Group("quantum/arena/display").Length == 1, "Quantum non-roster boundary retained navigation.");
            controller.Seek(.45);
            Check(dangers() > 0, "Quantum restored roster failed to resume native navigation.");
            stop();
            Console.WriteLine("PASS native Quantum precombat display active-only navigation non-roster perspective withdrawal roster restore owner isolation");
        }
        {
            load(id,
            [
                Header(1311), Frame(1311, 0, Initial(), full: true, party: roster),
                Frame(1311, .1, [], party: roster),
                Frame(1311, .2, [Actor(3, 0x233C, center + Vector3.UnitX * 7,
                    cast: Cast(45117, 3, center + Vector3.UnitX * 7, startedAt: .2))], party: roster),
                CancelHoH(.3, 3, 45117),
                Frame(1311, .4, [Actor(3, 0x233C, center + Vector3.UnitX * 7)], party: roster),
                Frame(1311, .5, [Actor(3, 0x233C, center + Vector3.UnitX * 7,
                    cast: Cast(44098, 3, center + Vector3.UnitX * 7, startedAt: .5))], party: roster),
                Frame(1311, .6, [], removed: [3], party: roster),
                Frame(1311, .7, [Actor(3, 0x233C, center,
                    cast: Cast(45200, 3, center, startedAt: .7))], party: roster),
                Frame(1311, 8, [], party: roster),
            ]);
            controller.Seek(.25);
            Check(Stage() == 1 && Items(Model(), "Ordinary").Length == 1
                && Group("quantum/light/display").Length == 1 && Group("quantum/dark/display").Length == 1
                && Group("quantum/aoe/").Length == 1 && dangers() > 0, "Quantum native identity/raw source/dual view output.");
            controller.Seek(.35);
            Check(Items(Model(), "Ordinary").Length == 1, "Raw-only component accepted unconfirmed Cancel.");
            controller.Seek(.45);
            Check(Items(Model(), "Ordinary").Length == 0, "Observed Finish did not consume ordinary group.");
            controller.Seek(.65);
            Check(Items(Model(), "Ordinary").Length == 0, "Destroyed casting actor did not Finish.");
            controller.Seek(8.1);
            Check(Items(Model(), "Ordinary").Length == 1, "Predicted clock expired real raw cast.");
            controller.Seek(.25);
            Check(Items(Model(), "Ordinary").Length == 1, "Quantum reverse Seek lost source history.");
            stop();
            Console.WriteLine("PASS native Quantum real status slots quantum fork raw-only ordinary true Finish cancellation destroy same-ID no timeout shared views inverse navigation Seek owner cleanup");
        }
        {
            var records = new List<string>
            {
                Header(1311), Frame(1311, 0, Initial(), full: true, party: roster),
                Frame(1311, .1, [], party: roster),
                Effect(.2, 44797),
                Frame(1311, .3, [Actor(10, 0x1EBE70, center + Vector3.UnitX * 7)], party: roster),
                Effect(.4, 44798),
                Frame(1311, .5, [], party: roster),
                Frame(1311, .51, [Actor(3, 0x233C, center + Vector3.UnitX * 7,
                    cast: Cast(44118, 5, center + Vector3.UnitX * 7, startedAt: .51))], party: roster),
                Map(.6, 0, 0x00020001),
                Map(.7, 0x17, 0x00020001),
                Map(.8, 0x1B, 0x00020001),
                Effect(.9, 44121),
                Frame(1311, 1, [Actor(3, 0x233C, center + Vector3.UnitX * 7,
                    rotation: -MathF.PI / 2, cast: Cast(44122, 3, center + Vector3.UnitX * 7, startedAt: 1))], party: roster),
                Icon(1.05), Icon(1.06),
                Frame(1311, 1.1, [Actor(3, 0x233C, center + Vector3.UnitX * 7, rotation: -MathF.PI / 2)], party: roster),
            };
            for (var i = 0; i < 12; ++i) records.Add(Effect(1.2 + i * .01, 44122));
            records.AddRange([Effect(1.4, 44123), Map(1.5, 0x0C, 0x00080004),
                Vfx(1.6), Map(1.7, 0x1B, 0x00080004),
                Frame(1311, 1.8, [], party: roster), Effect(1.9, 44118),
                Frame(1311, 2, [Actor(3, 0x233C, center + Vector3.UnitX * 7, rotation: MathF.PI / 2)], party: roster),
                Effect(2.1, 44119, center + Vector3.UnitX * 11)]);
            load(id, records);
            controller.Seek(.85);
            Check(Stage() == 3 && Items(Model(), "Pillars").Length == 2 && Items(Model(), "Towers").Length == 4
                && Group("quantum/tower/").Length == 4, "Quantum two direction state/Map towers/real floor holes.");
            dynamic arena = Value(Encounter(), "Arena");
            Check(!(bool)arena.Contains(new Vector3(-600, 0, -307), 0f), "Pillar hole was filled.");
            controller.Seek(1.08);
            Check(Stage() == 4 && Items(Model(), "Stacks").Length == 2 && Group("quantum/stack/").Length >= 2,
                "Quantum Source icon duplicate stack identity lost.");
            controller.Seek(1.15);
            Check(Value(Model(), "EndAoe") != null && Group("quantum/aoe/").Length == 1,
                "Actual small Finish failed independent processing+6 end prediction.");
            controller.Seek(1.55);
            Check(Stage() == 7 && Items(Model(), "Stacks").Length == 2 && Items(Model(), "Pillars").Length == 0,
                "Exact twelve effect count, end effect and restore transition differ.");
            controller.Seek(1.85);
            Check(Stage() == 9 && Items(Model(), "Stacks").Length == 0 && Items(Model(), "Towers").Length == 0,
                "VFX-only count or tower Map count failed component deactivation.");
            Check(Group("quantum/blaze/").Length == 10, "Known crystal direction lost complete visible exaflare queue.");
            controller.Seek(2.15);
            var blaze = Value(Model(), "Blaze");
            var lines = Items(blaze, "Lines");
            Check(Stage() == 10 && (int)Value(blaze, "NumCasts") == 1
                && (Vector3)Value(lines[0], "Next") == center + Vector3.UnitX * 15
                && (Vector3)Value(lines[1], "Next") == center + Vector3.UnitX * 3
                && Group("quantum/blaze/").Length == 7,
                $"Quantum first/rest branches: stage={Stage()}, casts={Value(blaze, "NumCasts")}, "
                + $"next={string.Join(';', lines.Select(line => Value(line, "Next")))}, previews={Group("quantum/blaze/").Length}.");
            controller.Seek(.85);
            Check(Stage() == 3 && Items(Model(), "Pillars").Length == 2 && Items(Model(), "Stacks").Length == 0,
                "Quantum reverse Seek leaked future Map/icon/VFX history.");
            stop();
            Console.WriteLine("PASS native Quantum Q40 sequential event state gates exact12 repeated effects Map pillar holes Source duplicate icons real Finish end clock VFX-only stack and tower exit reverse Seek owner clear");
        }
        {
            load(id, [Header(1311), Frame(1311, 0, Initial(), full: true, party: roster),
                Frame(1311, .1, [], party: roster), Frame(1311, .2, [], party: roster)]);
            controller.Seek(.15);
            var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [center, 0f, 6f, false, false, 0f, true, (int)Value(snapshot, "Version")])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)Value((object)result, "IsLeastDangerFallback"),
                "Quantum production solver failed color acquisition.");
            var endpoint = (Vector3)Value((object)result, "TargetPosition");
            dynamic light = assembly.GetType("MasterPlayer.Mechanics.Dungeons.QuantumGeometry")!
                .GetProperty("Light", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            Check((bool)light.Contains(endpoint, 0f), "Quantum solver destination did not acquire actual light pool.");
            stop();
            Console.WriteLine("PASS native Quantum production solver enters a real light island without least-danger fallback and clears owner");
        }
        {
            var source = Assembly.Load("MasterPlayer.MechanicRuntime");
            var wpos = source.GetType("BossMod.WPos")!;
            var wdir = source.GetType("BossMod.WDir")!;
            var angle = Activator.CreateInstance(source.GetType("BossMod.Angle")!)!;
            object Point(float x, float z) => Activator.CreateInstance(wpos, [x, z])!;
            var rectangle = Activator.CreateInstance(source.GetType("BossMod.Rectangle")!,
                [Point(-600, -300), 20f, 15f, angle])!;
            Array Shapes(IEnumerable<object> items)
            {
                var values = items.ToArray();
                var array = Array.CreateInstance(source.GetType("BossMod.Shape")!, values.Length);
                for (var i = 0; i < values.Length; ++i) array.SetValue(values[i], i);
                return array;
            }
            var component = Activator.CreateInstance(source.GetType("BossMod.Dawntrail.Quantum.Q1FinalVerse.BoundsOfSinEnd")!,
                flags, null, [null], null)!;
            var pillars = Items(component, "pillarPolygons");
            var boundsCtor = source.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
            var parameters = boundsCtor.GetParameters();
            var geometry = assembly.GetType("MasterPlayer.Mechanics.Dungeons.QuantumGeometry")!;
            var checks = 0;
            foreach (var indices in Enumerable.Range(0, 24).Select(i => new[] { i })
                .Concat([Array.Empty<int>(), Enumerable.Range(0, 12).ToArray(), Enumerable.Range(12, 12).ToArray()]))
            {
                var args = parameters.Select(p => p.DefaultValue).ToArray();
                args[0] = Shapes([rectangle]);
                args[1] = Shapes(indices.Select(i => pillars[i]));
                args[Array.FindIndex(parameters, p => p.Name == "AdjustForHitboxOutwards")] = indices.Length == 0;
                var bounds = boundsCtor.Invoke(args);
                var sourceCenter = Value(bounds, "Center");
                var sx = (float)Value(sourceCenter, "X");
                var sz = (float)Value(sourceCenter, "Z");
                dynamic native = indices.Length == 0
                    ? geometry.GetProperty("RestoredFloor", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!
                    : geometry.GetMethod("Arena", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [indices])!;
                var contains = bounds.GetType().GetMethod("Contains", flags)!;
                var fill = indices.Length == 0 ? null : Items((object)native, "FillPolygons")
                    .Select(p => ((IEnumerable)p).Cast<Vector3>().ToArray()).ToArray();
                for (var x = -20.4f; x <= 20.4f; x += .41f)
                    for (var z = -15.4f; z <= 15.4f; z += .41f)
                    {
                        var point = center + new Vector3(x, 0, z);
                        var expected = (bool)contains.Invoke(bounds,
                            [Activator.CreateInstance(wdir, [point.X - sx, point.Z - sz])!])!;
                        var actual = (bool)native.Contains(point, 0f);
                        Check(expected == actual, $"Quantum source pillar arena {string.Join(',', indices)} at {point}: {expected}/{actual}.");
                        if (fill != null)
                            Check(fill.Any(t => InTriangle(point, t)) == expected,
                                $"Quantum fill triangles cover a source hole or leave a gap at {point}.");
                        ++checks;
                    }
            }
            Console.WriteLine($"PASS native Quantum real source 24 pillar initial Round-outward restore and both full rings {checks} point arena oracle");

            var lightComponent = Activator.CreateInstance(source.GetType("BossMod.Dawntrail.Quantum.Q1FinalVerse.LightAndDark")!,
                flags, null, [null], null)!;
            foreach (var name in new[] { "Light", "Dark" })
            {
                var pieces = Enumerable.Range(1, 5).Select(i => Activator.CreateInstance(
                    source.GetType("BossMod.PolygonCustom")!, [Value(lightComponent, name.ToLowerInvariant() + i)])!).ToArray();
                var ctor = source.GetType("BossMod.AOEShapeCustom")!.GetConstructors().Single();
                var args = ctor.GetParameters().Select(p => p.DefaultValue).ToArray();
                args[0] = Shapes(pieces);
                var shape = ctor.Invoke(args);
                var polygon = shape.GetType().GetMethod("GetCombinedPolygon")!.Invoke(shape, [Point(-600, -300)])!;
                var contains = polygon.GetType().GetMethod("Contains", flags)!;
                dynamic native = geometry.GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                dynamic inverse = geometry.GetProperty(name + "Inverse", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                dynamic restored = geometry.GetProperty("RestoredFloor", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var count = 0;
                for (var x = -20.4f; x <= 20.4f; x += .23f)
                    for (var z = -15.4f; z <= 15.4f; z += .23f)
                    {
                        var point = center + new Vector3(x, 0, z);
                        var expected = (bool)contains.Invoke(polygon, [Activator.CreateInstance(wdir, [point.X + 600, point.Z + 300])!])!;
                        Check((bool)native.Contains(point, 0f) == expected, $"Quantum {name} source material pool at {point}.");
                        Check((bool)inverse.Contains(point, 0f) == ((bool)restored.Contains(point, 0f) && !expected),
                            $"Quantum {name} inverse complement escaped restored floor at {point}.");
                        ++count;
                    }
                Console.WriteLine($"PASS native Quantum {name} actual source five material islands and inverse navigation {count} point oracle");
            }

            static bool InTriangle(Vector3 p, Vector3[] triangle)
            {
                static double Cross(Vector3 a, Vector3 b, Vector3 c)
                    => ((double)b.X - a.X) * ((double)c.Z - a.Z) - ((double)b.Z - a.Z) * ((double)c.X - a.X);
                if (Cross(triangle[0], triangle[1], triangle[2]) == 0) return false;
                var a = Cross(triangle[0], triangle[1], p);
                var b = Cross(triangle[1], triangle[2], p);
                var c = Cross(triangle[2], triangle[0], p);
                return a >= 0 && b >= 0 && c >= 0 || a <= 0 && b <= 0 && c <= 0;
            }
        }
    }
}
