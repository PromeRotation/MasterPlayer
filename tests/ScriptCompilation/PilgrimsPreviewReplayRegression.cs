using System.Collections;
using System.Text.Json;

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
                Check(dangers.Length == probe.Count && previews.Length == (probe.Count == 0 ? 0 : 1),
                    $"PT{floor} at {probe.Time}: expected {probe.Count} navigation areas and one next-hit preview; got {dangers.Length}/{previews.Length}.");
                if (probe.Count == 0) continue;
                dynamic next = dangers.MinBy(a => ((IEnumerable)a.DangerWindows).Cast<dynamic>().Min(w => (DateTime)w.DangerFrom))!;
                dynamic preview = previews[0].GetType().GetProperty("Geometry")!.GetValue(previews[0])!;
                Check(preview.GetType() == next.Geometry.GetType() && preview.GetBounds().Equals(next.Geometry.GetBounds()),
                    $"PT{floor} preview geometry differs from the next navigation area.");
                if (floor == 80)
                    Check(preview.GetType().Name == (probe.Count == 1 ? "CircleGeometry" : "RayRectangleGeometry"), "PT80 final circle appeared before the last charge resolved.");
            }
            controller.Seek(probes[0].Time);
            controller.StopSession();
            Check(((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Count() == 0
                && ((IEnumerable)visibilityType.GetMethod("GetPreviews")!.Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().Count() == 0,
                $"PT{floor} retained output after seeking backward and stopping.");
            Console.WriteLine($"PASS production PT{floor} previews: complete sequence navigation, next-hit geometry, duplicate effects, cast replacement, seek and cleanup");
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
