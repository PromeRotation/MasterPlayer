using System.Collections;
using System.Text.Json;
using System.Numerics;

internal static class PilgrimsPreviewReplayRegression
{
    public static void Run(object controllerObject, object runtimeObject, object visibility, Type visibilityType, string file)
    {
        Pt10ReplayRegression.Run(controllerObject, runtimeObject, visibility, visibilityType, file);
        dynamic controller = controllerObject;
        dynamic runtime = runtimeObject;
        object Pose(float x, float z) => new { X = x, Y = 0f, Z = z };
        object Center(int floor) => Pose(floor == 80 ? -600 : -300, -300);

        foreach (var floor in new[] { 10, 70, 80 })
        {
            var events = new List<(double Time, string Line)>();
            void Add(double time, string kind, object data)
                => events.Add((time, JsonSerializer.Serialize(new { t = time, kind,
                    type = $"PromeRotation.LogSystem.LogSystem{kind}Event", packetFacing = 0f, data })));
            void Cast(double time, ulong source, uint action, object position, double seconds, object? origin = null)
                => Add(time, "CastStart", new { SourceId = source, ActionId = action, CastTime = seconds, EffectPosition = position, SourcePosition = origin });
            void Effect(double time, ulong source, uint action, uint sequence, object? position = null)
            {
                var data = new { SourceId = source, ActionId = action, GlobalSequence = sequence, EffectPosition = position };
                Add(time, "ActionEffect", data);
                Add(time, "ActionEffect", data);
            }
            var probes = new List<(double Time, int Count)>();
            var oid = floor switch { 10 => 0x460D, 70 => 0x484C, _ => 0x485D };
            var actors = new List<object>
            {
                new { Id = 1, Oid = oid, Hp = 1000, MaxHp = 1000, InCombat = true, Position = Center(floor) },
                new { Id = 100, Kind = 1, Position = Center(floor) },
            };
            if (floor == 10)
            {
                probes.Add((4, 4));
                for (var i = 0; i < 4; ++i)
                {
                    var position = Pose(-306 + i * 4, -300);
                    var hit = 9 + .7 * i;
                    Effect(.1 + i, 10, 44058, (uint)(i + 1), position);
                    Cast(hit - 2.2, (ulong)(20 + i), 44057, position, 1.9);
                    probes.Add((hit - .1, 4 - i));
                    Effect(hit, (ulong)(20 + i), 44057, (uint)(10 + i), Pose(0, 0));
                    probes.Add((hit + .001, 3 - i));
                }
            }
            else if (floor == 70)
            {
                probes.Add((5, 8));
                for (var i = 0; i < 8; ++i)
                {
                    Cast(.1 + .6 * i, (ulong)(20 + i), 43437, Center(floor), 1);
                    Effect(9.6 + 2 * i, 1, 43434, (uint)(i + 1));
                    probes.Add((9.601 + 2 * i, 7 - i));
                }
            }
            else
            {
                var balls = Enumerable.Range(0, 9).Select(i => Pose(-604 + i, -305 + i % 2 * 10)).Reverse().ToArray();
                for (var i = 0; i < 9; ++i)
                    actors.Add(new { Id = 28 - i, Oid = 0x485E, Hp = 1000, MaxHp = 1000, Position = balls[i] });
                probes.Add((1, 9));
                for (var i = 0; i < 8; ++i)
                {
                    var hit = 7.5 + .4 * i;
                    Cast(hit - 1.3, (ulong)(40 + i), 43795, balls[i + 1], 1, balls[i]);
                    Effect(hit, (ulong)(40 + i), 43795, (uint)(i + 1));
                    probes.Add((hit + .001, 8 - i));
                }
                Cast(9.4, 60, 44060, balls[^1], 2);
                probes.Add((9.401, 4));
                Effect(11.7, 60, 44060, 9);
                probes.Add((11.701, 0));
            }
            var territory = 1280 + floor / 10;
            var header = JsonSerializer.Serialize(new { v = 2, territory, startedUtc = "2026-11-19T00:00:00Z", pluginVersion = "preview-audit" });
            var frame = JsonSerializer.Serialize(new { t = 0, kind = "WorldFrame", data = new { Full = true, Territory = territory, LocalPlayerId = 100, Actors = actors } });
            var end = JsonSerializer.Serialize(new { t = probes.Max(p => p.Time) + .1, kind = "WorldFrame", data = new { Territory = territory, LocalPlayerId = 100 } });
            File.WriteAllText(file, string.Join('\n', new[] { header, frame }.Concat(events.OrderBy(e => e.Time).Select(e => e.Line)).Append(end)));
            Check(controller.Load(file), (string)controller.StatusMessage);
            controller.Play(); controller.Pause();
            foreach (var probe in probes.OrderBy(p => p.Time))
            {
                controller.Seek(probe.Time);
                var dangers = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<dynamic>().ToArray();
                var previews = ((IEnumerable)visibilityType.GetMethod("GetPreviews")!.Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().ToArray();
                var previewCount = floor switch { 10 => Math.Min(probe.Count, 3), 70 => Math.Min(probe.Count, 2), _ => probe.Count };
                Check(dangers.Length == probe.Count && previews.Length == previewCount,
                    $"PT{floor} at {probe.Time}: expected {probe.Count} navigation areas and {previewCount} previews; got {dangers.Length}/{previews.Length}.");
                if (probe.Count == 0) continue;
                var expectedShapes = dangers.OrderBy(a => ((IEnumerable)a.DangerWindows).Cast<dynamic>().Min(w => (DateTime)w.DangerFrom))
                    .Take(previewCount).Select(a => (Type: ((object)a.Geometry).GetType(), Bounds: (object)a.Geometry.GetBounds())).ToList();
                foreach (var entry in previews)
                {
                    dynamic preview = entry.GetType().GetProperty("Geometry")!.GetValue(entry)!;
                    Check(expectedShapes.Remove((((object)preview).GetType(), (object)preview.GetBounds())),
                        $"PT{floor} preview geometry differs from the visible navigation windows.");
                }
                Check(expectedShapes.Count == 0, $"PT{floor} omitted a visible sequence shape.");
                if (floor == 80)
                    Check(previews.Count(p => p.GetType().GetProperty("Geometry")!.GetValue(p)!.GetType().Name == "CircleGeometry") == 1,
                        "PT80 failed to keep its terminal circle visible with the remaining charges.");
            }
            controller.Seek(probes[0].Time);
            controller.StopSession();
            Check(((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Count() == 0
                && ((IEnumerable)visibilityType.GetMethod("GetPreviews")!.Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().Count() == 0,
                $"PT{floor} retained output after seeking backward and stopping.");
            Console.WriteLine($"PASS production PT{floor} previews: complete sequence navigation and source preview counts, exact geometry, duplicate effects, cast replacement, seek and cleanup");
        }
        Knockbacks(controllerObject, runtimeObject, visibility, visibilityType, file);
    }

    private static void Knockbacks(object controllerObject, object runtimeObject, object visibility, Type visibilityType, string file)
    {
        dynamic controller = controllerObject;
        dynamic runtime = runtimeObject;
        foreach (var (floor, oid, action, distance) in new (int, uint, uint, float)[]
        {
            (20, 0x485C, 43455, 10), (40, 0x460B, 42128, 21), (50, 0x4872, 43538, 20),
            (70, 0x484C, 43431, 7), (90, 0x48F5, 44266, 30),
        })
        {
            var x = floor == 40 ? -600f : -300f;
            object Pose(float px, float pz) => new { X = px, Y = 0f, Z = pz };
            var territory = 1280 + floor / 10;
            var events = new List<(double Time, string Line)>();
            void Event(double time, string kind, object data) => events.Add((time, JsonSerializer.Serialize(new
            {
                t = time, kind, type = $"PromeRotation.LogSystem.LogSystem{kind}Event", packetFacing = 0f, data,
            })));
            Event(.4, "CastStart", new { SourceId = 1, ActionId = action, CastTime = 3f,
                Timestamp = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc).AddSeconds(.1),
                EffectPosition = Pose(x, -300), SourcePosition = Pose(x, -300) });
            if (floor == 40)
                Event(.2, "CastStart", new { SourceId = 2, ActionId = action, CastTime = 4f,
                    EffectPosition = Pose(x + 10, -300), SourcePosition = Pose(x + 10, -300) });
            Event(1, "StatusAdd", new { TargetId = 100, StatusId = 2663, Duration = 10f });
            Event(2, "StatusRemove", new { TargetId = 100, StatusId = 2663 });
            Event(2.5, "CancelCast", new { SourceId = 1, ActionId = action });
            if (floor == 40) Event(2.6, "CancelCast", new { SourceId = 2, ActionId = action });
            var header = JsonSerializer.Serialize(new { v = 2, territory, startedUtc = "2026-11-19T00:00:00Z", pluginVersion = "native-knockbacks" });
            var initial = JsonSerializer.Serialize(new { t = 0, kind = "WorldFrame", data = new
            {
                Full = true, Territory = territory, LocalPlayerId = 100, Actors = new object[]
                {
                    new { Id = 1, Oid = oid, Hp = 1000, MaxHp = 1000, InCombat = true, Position = Pose(x, -300) },
                    new { Id = 100, Kind = 1, Position = Pose(x, -298), Rotation = 1f },
                },
            } });
            var end = JsonSerializer.Serialize(new { t = 3, kind = "WorldFrame", data = new { Territory = territory, LocalPlayerId = 100 } });
            File.WriteAllLines(file, new[] { header, initial }.Concat(events.OrderBy(e => e.Time).Select(e => e.Line)).Append(end));
            object[] Lines() => ((IEnumerable)visibilityType.GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>()
                .Where(l => ((string)l.GetType().GetProperty("Id")!.GetValue(l)!).Contains("knockback/")).ToArray();
            int Markers() => ((IEnumerable)visibilityType.GetMethod("GetPreviews")!.Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>()
                .Count(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("knockback/"));
            Check(controller.Load(file), (string)controller.StatusMessage);
            controller.Play(); controller.Pause(); controller.Seek(.5d);
            var count = floor == 40 ? 2 : 1;
            var lines = Lines();
            Check(lines.Length == count && Markers() == count, $"PT{floor} omitted native projection lines or landing actor markers.");
            var hitClock = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<dynamic>()
                .SelectMany(a => ((IEnumerable)a.DangerWindows).Cast<dynamic>()).Min(w => (DateTime)w.DangerFrom);
            Check(Math.Abs(((DateTime)hitClock - new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds - 3.2) < .000001,
                $"PT{floor} dispatch latency shifted the knockback activation clock.");
            var first = lines[0];
            var start = (Vector3)first.GetType().GetProperty("Start")!.GetValue(first)!;
            var landing = (Vector3)first.GetType().GetProperty("End")!.GetValue(first)!;
            Check(Vector3.Distance(start, new(x, 0, -298)) < .001f && Math.Abs(Vector3.Distance(start, landing) - distance) < .001f,
                $"PT{floor} projection lost its player origin or knockback distance.");
            if (floor == 40)
                Check((Vector3)lines[1].GetType().GetProperty("Start")!.GetValue(lines[1])! == landing,
                    "PT40 projected the second knockback from the original player position.");
            controller.Seek(1.5d);
            Check(Lines().Length == 0 && Markers() == 0, $"PT{floor} retained knockback projections while immune.");
            controller.Seek(2.1d);
            Check(Lines().Length == count && Markers() == count, $"PT{floor} failed to restore projections after immunity ended.");
            controller.Seek(2.7d);
            Check(Lines().Length == 0 && Markers() == 0, $"PT{floor} retained cancelled projection output.");
            controller.Seek(.5d);
            controller.StopSession();
            Check(Lines().Length == 0 && Markers() == 0, $"PT{floor} retained projections after reverse Seek and stop.");
        }
        Console.WriteLine("PASS production PT20/40/50/70/90: native projected movement and landing markers, sequential knockbacks, immunity, cancel, reverse Seek and cleanup");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
