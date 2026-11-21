using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static class Pt99BlazeCaptureRegression
{
    private static readonly DateTime Start = new(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Assembly assembly, dynamic manager, dynamic runtime, dynamic controller, string path)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", "pt99-blaze-215425.json")));
        var round = fixture.RootElement.GetProperty("rounds")[0];
        var directions = round.GetProperty("directionEffects").EnumerateArray().ToArray();
        var crystals = round.GetProperty("crystals").EnumerateArray().ToArray();
        var events = round.GetProperty("events").EnumerateArray().ToArray();
        var offset = 1 - directions[0].GetProperty("t").GetDouble();
        var switchAt = offset + directions[1].GetProperty("t").GetDouble();
        object Pose(JsonElement p) => new { X = p[0].GetSingle(), Y = p[1].GetSingle(), Z = p[2].GetSingle() };
        var visibility = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        visibility = visibility.GetType().GetProperty("AoeVisibility", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(visibility)!;
        int Previews() => ((IEnumerable)visibility.GetType().GetMethod("GetPreviews")!
            .Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().Count();

        foreach (var delayed in new[] { false, true })
        foreach (var complete in new[] { false, true })
        {
            var records = new List<(double Time, object Record)>();
            void Add(double time, object record) => records.Add((time, record));
            Add(0, new { t = 0, kind = "WorldFrame", data = new { Full = true, Territory = 1333, LocalPlayerId = 100,
                Actors = new object[] {
                    new { Id = 1, Oid = 0x48EA, Hp = 1000, MaxHp = 1000, InCombat = true,
                        Position = new { X = -600, Y = 0, Z = -330 } },
                    new { Id = 100, Kind = 1, Position = Pose(round.GetProperty("player")) } } } });
            foreach (var direction in directions)
            {
                var t = offset + direction.GetProperty("t").GetDouble();
                var record = new { t, kind = "ActionEffect", type = "PromeRotation.LogSystem.LogSystemActionEffectEvent",
                    data = new { SourceId = 1, ActionId = direction.GetProperty("action").GetUInt32(),
                        GlobalSequence = direction.GetProperty("sequence").GetUInt32() } };
                Add(t, record);
                Add(t, record);
            }
            foreach (var batch in crystals.GroupBy(c => c.GetProperty("created").GetDouble()))
            {
                var t = offset + batch.Key;
                // Reverse enumeration to ensure crystal ownership does not depend on object-table order.
                Add(t, new { t, kind = "WorldFrame", data = new { Territory = 1333, LocalPlayerId = 100,
                    Actors = batch.Reverse().Select(c => new { Id = c.GetProperty("source").GetUInt64(),
                        Oid = 0x1EBE70, Position = Pose(c.GetProperty("position")) }).ToArray() } });
            }
            foreach (var e in events)
            {
                var t = offset + e.GetProperty("t").GetDouble();
                var cast = e.GetProperty("kind").GetString() == "cast";
                if (!cast && !complete && !e.GetProperty("hit").GetBoolean()) continue;
                var data = new Dictionary<string, object?> {
                    ["SourceId"] = e.GetProperty("source").GetUInt64(),
                    ["ActionId"] = e.GetProperty("action").GetUInt32(),
                    ["EffectPosition"] = cast || e.GetProperty("action").GetUInt32() == 44080
                        ? Pose(e.GetProperty("position")) : new { X = 0f, Y = 0f, Z = 0f },
                    ["SourcePosition"] = Pose(e.GetProperty("sourcePosition")),
                };
                if (cast) data["CastTime"] = e.GetProperty("seconds").GetSingle();
                else
                {
                    data["GlobalSequence"] = e.GetProperty("sequence").GetUInt32();
                    data["SourceRotation"] = e.GetProperty("rotation").GetSingle();
                    data["TargetIndex"] = -1;
                }
                var kind = cast ? "CastStart" : "ActionEffect";
                var record = new { t, kind, type = $"PromeRotation.LogSystem.LogSystem{kind}Event",
                    packetFacing = e.GetProperty("rotation").GetSingle(), data };
                Add(t, record);
                if (!cast) Add(t, record);
            }
            var end = offset + 30;
            Add(end, new { t = end, kind = "WorldFrame", data = new { Territory = 1333, LocalPlayerId = 100, Actors = Array.Empty<object>() } });
            File.WriteAllLines(path, new[] { """{"v":2,"territory":1333,"startedUtc":"2026-11-21T00:00:00Z","pluginVersion":"pt99-capture-regression"}""" }
                .Concat(records.OrderBy(r => r.Time).Select(r => JsonSerializer.Serialize(r.Record))));
            manager.SetScriptEnabled("promeradar.pt99", true);
            Check(controller.Load(path), (string)controller.StatusMessage);
            controller.SetSpeed(1f);
            controller.Play();
            void Advance(double t)
            {
                controller.Update((float)(t - (double)controller.Clock));
                Check(controller.SessionActive && (string)manager.ErrorMessage == "", (string)controller.StatusMessage);
            }
            Advance(1.01);
            object script = manager.Running["promeradar.pt99"].Script;
            var nextScan = script.GetType().BaseType!.GetField("nextScan", BindingFlags.Instance | BindingFlags.NonPublic)!;
            if (delayed) nextScan.SetValue(script, Start.AddYears(1));
            foreach (var t in crystals.Select(c => offset + c.GetProperty("created").GetDouble()).Where(t => t < switchAt).Distinct())
                Advance(t + .001);
            Advance(switchAt + .001);
            nextScan.SetValue(script, DateTime.MinValue);
            Advance(switchAt + .04);
            var checks = 0;
            foreach (var group in events.Where(e => e.GetProperty("kind").GetString() == "effect")
                .GroupBy(e => e.GetProperty("t").GetDouble()))
            {
                var at = offset + group.Key;
                Advance(at - .001);
                var dangers = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<dynamic>().ToArray();
                foreach (var effect in group)
                {
                    var p = effect.GetProperty("position");
                    var center = new Vector3(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle());
                    var hit = Start.AddSeconds(at);
                    var matched = dangers.Where(a => Vector3.Distance((Vector3)a.Geometry.Position, center) < .1f
                        && ((IEnumerable)a.DangerWindows).Cast<dynamic>().Any(w =>
                            (DateTime)w.DangerFrom <= hit && (DateTime?)w.DangerUntil >= hit)).ToArray();
                    Check(matched.Length > 0,
                        $"PT99 capture delayed={delayed}, complete={complete}: missing actual {effect.GetProperty("action")} at {group.Key}, {center}.");
                    Check(matched.All(a => Math.Abs((float)a.Geometry.Radius - 5) < .001f),
                        "PT99 capture changed the physical R5 geometry.");
                    if (effect.GetProperty("hit").GetBoolean())
                    {
                        var pc = effect.GetProperty("recordedPlayer");
                        Check(matched.Any(a => (bool)a.Geometry.Contains(new Vector3(pc[0].GetSingle(), pc[1].GetSingle(), pc[2].GetSingle()), 0f)),
                            "PT99 capture failed to recognize the fatal recorded position as danger.");
                    }
                    ++checks;
                }
                Advance(at + .001);
            }
            Check(checks == 64, "PT99 capture must check all 64 actual effects.");
            Advance(end);
            Check(!((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Any(),
                "PT99 capture retained expired exaflares.");
            controller.Seek(offset + .1);
            Check(((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Any(),
                "PT99 capture failed to rebuild exaflares on backward seek.");
            controller.StopSession();
            Check(!((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Any() && Previews() == 0,
                "PT99 capture retained navigation or previews after stopping.");
            Console.WriteLine($"PASS production PT99 capture: delayed={delayed}, complete={complete}, 64 effects, fatal point, duplicate effects, R5, seek and cleanup");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
