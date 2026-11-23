using System.Collections;
using System.Text.Json;

internal static class Pt10ReplayRegression
{
    public static void Run(object controllerObject, object runtimeObject, object visibility, Type visibilityType, string file)
    {
        dynamic controller = controllerObject;
        dynamic runtime = runtimeObject;
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", "pt10-leafmash-114707.json")));
        object Pose(JsonElement p) => new { X = p[0].GetSingle(), Y = 0f, Z = p[2].GetSingle() };
        var cycles = 0;
        foreach (var cycle in fixture.RootElement.GetProperty("cycles").EnumerateArray())
        {
            var lines = new List<string>
            {
                """{"v":2,"territory":1281,"startedUtc":"2026-11-20T00:00:00Z","pluginVersion":"pt10-leafmash"}""",
                JsonSerializer.Serialize(new { t = 0, kind = "WorldFrame", data = new { Full = true, Territory = 1281, LocalPlayerId = 100,
                    Actors = new object[] {
                        new { Id = 1, Oid = 0x460D, Hp = 1000, MaxHp = 1000, InCombat = true, Position = new { X = -300f, Y = 0f, Z = -300f } },
                        new { Id = 100, Kind = 1, Position = Pose(cycle.GetProperty("initialPlayer")) } } } }),
            };
            var effects = new List<double>();
            foreach (var e in cycle.GetProperty("events").EnumerateArray())
            {
                var cast = e.GetProperty("kind").GetString() == "cast";
                var time = e.GetProperty("t").GetDouble() + .01;
                var data = new Dictionary<string, object?>
                {
                    ["SourceId"] = e.GetProperty("source").GetUInt64(),
                    ["ActionId"] = e.GetProperty("action").GetUInt32(),
                    ["EffectPosition"] = Pose(e.GetProperty("position")),
                    ["SourcePosition"] = Pose(e.GetProperty("sourcePosition")),
                };
                if (cast) data["CastTime"] = e.GetProperty("seconds").GetSingle();
                else
                {
                    data["GlobalSequence"] = e.GetProperty("sequence").GetUInt32();
                    if (e.GetProperty("action").GetUInt32() == 44057) effects.Add(time);
                }
                var kind = cast ? "CastStart" : "ActionEffect";
                var line = JsonSerializer.Serialize(new { t = time, kind, type = $"PromeRotation.LogSystem.LogSystem{kind}Event", packetFacing = 0f, data });
                lines.Add(line);
                if (!cast) lines.Add(line);
            }
            lines.Add(JsonSerializer.Serialize(new { t = effects[^1] + .6, kind = "WorldFrame", data = new { Territory = 1281, LocalPlayerId = 100 } }));
            File.WriteAllText(file, string.Join('\n', lines));
            Check(controller.Load(file), (string)controller.StatusMessage);
            controller.Play(); controller.Pause();
            for (var i = 0; i < effects.Count; ++i)
            {
                controller.Seek(effects[i] + .4);
                var dangers = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<dynamic>().ToArray();
                var previews = ((IEnumerable)visibilityType.GetMethod("GetPreviews")!.Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().ToArray();
                var expected = 3 - i;
                Check(dangers.Length == expected && previews.Length == Math.Min(expected, 3),
                    $"PT10 recorded cycle {cycle.GetProperty("start")} jump {i + 1} lost navigation/preview: {dangers.Length}/{previews.Length}.");
                if (expected == 0) continue;
                var expectedBounds = dangers.OrderBy(a => ((IEnumerable)a.DangerWindows).Cast<dynamic>().Min(w => (DateTime)w.DangerFrom))
                    .Take(3).Select(a => (object)a.Geometry.GetBounds()).ToList();
                foreach (var entry in previews)
                {
                    dynamic preview = entry.GetType().GetProperty("Geometry")!.GetValue(entry)!;
                    Check(expectedBounds.Remove((object)preview.GetBounds()), "PT10 preview differs from the next three navigation circles.");
                }
                Check(expectedBounds.Count == 0, "PT10 omitted a navigation circle from its preview.");
            }
            controller.Seek(effects[0] - .2);
            controller.StopSession();
            Check(!((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Any(), "PT10 replay stop left dangers.");
            Check(!((IEnumerable)visibilityType.GetMethod("GetPreviews")!.Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().Any(), "PT10 replay stop left previews.");
            ++cycles;
        }
        Check(cycles == 5, "PT10 production replay must cover all five recorded cycles.");
        Console.WriteLine("PASS production PT10 latest ARR: five full sequences, zero ground positions, next-cast ordering, duplicate effects, previews, seek and cleanup");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
