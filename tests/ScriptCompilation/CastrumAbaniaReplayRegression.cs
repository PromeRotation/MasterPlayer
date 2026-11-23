using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static class CastrumAbaniaReplayRegression
{
    private const string ScriptId = "masterplayer.dungeon.castrum-abania";
    private static readonly DateTime Start = new(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc);
    private static readonly HashSet<uint> AoeActions =
        [7957, 7960, 8336, 33460, 33461, 33462, 33463, 33464, 7975, 7976, 7977, 8334, 8335, 7979, 8328, 8329];

    public static void Run(Assembly assembly, dynamic manager, dynamic runtime, dynamic controller, string path)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "CastrumAbania", "abania-224618.json")));
        object Pose(JsonElement p) => new { X = p[0].GetSingle(), Y = p[1].GetSingle(), Z = p[2].GetSingle() };
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = plugin.GetType().GetProperty("AoeVisibility", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(plugin)!;
        int Previews() => ((IEnumerable)visibility.GetType().GetMethod("GetPreviews")!
            .Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().Count();
        int Dangers() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Count();
        var checkedCasts = 0;
        foreach (var segment in fixture.RootElement.GetProperty("segments").EnumerateArray())
        foreach (var player in new ulong[] { 100, 101, 102, 103 })
        {
            var offset = segment.GetProperty("start").GetDouble();
            var boss = segment.GetProperty("boss").GetUInt32();
            var events = segment.GetProperty("events").EnumerateArray().ToArray();
            var actors = segment.GetProperty("actors").EnumerateArray().ToDictionary(a => a.GetProperty("id").GetUInt64(),
                a => new Dictionary<string, object?>
                {
                    ["Id"] = a.GetProperty("id").GetUInt64(), ["Oid"] = a.GetProperty("oid").GetUInt32(),
                    ["Kind"] = a.GetProperty("kind").GetByte(), ["Position"] = Pose(a.GetProperty("position")),
                    ["Rotation"] = a.GetProperty("rotation").GetSingle(), ["Hp"] = 1000, ["MaxHp"] = 1000,
                    ["Targetable"] = true, ["InCombat"] = a.GetProperty("oid").GetUInt32() == boss,
                });
            var party = new[] { 100ul, 101ul, 102ul, 103ul }.Select((id, slot) => new { Slot = slot, EntityId = id }).ToArray();
            var activeCasts = new Dictionary<ulong, uint>();
            string Frame(double t, IEnumerable<object> updated) => JsonSerializer.Serialize(new { t, kind = "WorldFrame",
                data = new { Territory = 1145, LocalPlayerId = player, Actors = updated.ToArray(), Party = party } });
            var records = new List<string>
            {
                """{"v":2,"territory":1145,"startedUtc":"2026-11-21T00:00:00Z","pluginVersion":"abania-capture-regression"}""",
                JsonSerializer.Serialize(new { t = 0, kind = "WorldFrame", data = new
                {
                    Full = true, Territory = 1145, LocalPlayerId = player, Actors = actors.Values.ToArray(),
                    Party = party,
                } }),
            };
            foreach (var e in events)
            {
                var t = e.GetProperty("t").GetDouble() - offset;
                var id = e.GetProperty("id").GetUInt64();
                var kind = e.GetProperty("kind").GetString();
                if (kind == "pose")
                {
                    actors[id]["Position"] = Pose(e.GetProperty("position"));
                    actors[id]["Rotation"] = e.GetProperty("rotation").GetSingle();
                    records.Add(Frame(t, [actors[id]]));
                    continue;
                }
                if (kind == "effect")
                {
                    var targets = e.GetProperty("targets").EnumerateArray().Select(a => a.GetUInt64()).DefaultIfEmpty(0ul);
                    foreach (var target in targets)
                    {
                        var record = JsonSerializer.Serialize(new { t, kind = "ActionEffect",
                            type = "PromeRotation.LogSystem.LogSystemActionEffectEvent",
                            data = new { SourceId = id, ActionId = e.GetProperty("action").GetUInt32(),
                                GlobalSequence = e.GetProperty("sequence").GetUInt32(), TargetId = target, TargetIndex = -1 } });
                        records.Add(record);
                        records.Add(record); // The duplicate must not retire another pending mechanic.
                    }
                    if (activeCasts.GetValueOrDefault(id) == e.GetProperty("action").GetUInt32())
                    {
                        activeCasts.Remove(id);
                        actors[id]["Cast"] = null;
                        records.Add(Frame(t, [actors[id]]));
                    }
                }
                else if (kind == "cast")
                {
                    activeCasts[id] = e.GetProperty("action").GetUInt32();
                    actors[id]["Cast"] = new
                    {
                        ActionId = e.GetProperty("action").GetUInt32(), TargetId = e.GetProperty("target").GetUInt64(),
                        Location = Pose(e.GetProperty("position")), Rotation = e.GetProperty("rotation").GetSingle(),
                        StartedAt = Start.AddSeconds(t), FinishAt = Start.AddSeconds(t + e.GetProperty("seconds").GetDouble() + .3),
                    };
                    records.Add(Frame(t, [actors[id]]));
                    records.Add(JsonSerializer.Serialize(new { t, kind = "CastStart",
                        type = "PromeRotation.LogSystem.LogSystemCastStartEvent",
                        packetFacing = e.GetProperty("rotation").GetSingle(),
                        data = new { SourceId = id, ActionId = e.GetProperty("action").GetUInt32(),
                            CastTime = e.GetProperty("seconds").GetSingle(), TargetId = e.GetProperty("target").GetUInt64(),
                            EffectPosition = Pose(e.GetProperty("position")), SourcePosition = Pose(e.GetProperty("sourcePosition")),
                            Timestamp = Start.AddSeconds(t) } }));
                }
                else
                    records.Add(JsonSerializer.Serialize(new { t, kind = "TargetIcon",
                        type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                        data = new { SourceId = id, TargetId = e.GetProperty("target").GetUInt64(), MarkerId = 74 } }));
            }
            var end = segment.GetProperty("end").GetDouble() - offset + 1;
            records.Add(JsonSerializer.Serialize(new { t = end, kind = "WorldFrame",
                data = new { Full = true, Territory = 1145, LocalPlayerId = player, Actors = Array.Empty<object>() } }));
            File.WriteAllLines(path, records);
            manager.SetScriptEnabled(ScriptId, true);
            Check(controller.Load(path), (string)controller.StatusMessage);
            controller.SetSpeed(1f);
            controller.Play();
            void Advance(double time)
            {
                controller.Update((float)(time - (double)controller.Clock));
                Check((string)manager.ErrorMessage == "", (string)manager.ErrorMessage);
            }
            IReadOnlyList<(string Key, object Area)> Output()
            {
                object script = manager.Running[ScriptId].Script;
                var output = (IDictionary)script.GetType().BaseType!.GetField("output", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
                return output.Keys.Cast<string>().Select(k =>
                    (k, output[k]!.GetType().GetField("Item2")!.GetValue(output[k])!)).ToArray();
            }
            bool VisibleCast(ulong source, uint action)
            {
                object script = manager.Running[ScriptId].Script;
                var encounter = script.GetType().BaseType!.GetField("encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
                var casts = (IDictionary)encounter.GetType().GetField("casts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(encounter)!;
                var pending = casts.Values.Cast<object>().Where(c => (uint)c.GetType().GetProperty("Action")!.GetValue(c)! == action).ToArray();
                Check(pending.Any(c => (ulong)c.GetType().GetProperty("Source")!.GetValue(c)! == source),
                    $"Action {action}, source={source}: cast missing from production queue.");
                var cap = action == 33460 ? 10 : action == 33463 ? 3 : int.MaxValue;
                return pending.Take(cap).Any(c => (ulong)c.GetType().GetProperty("Source")!.GetValue(c)! == source);
            }
            double? seekTime = null;
            var solved = false;
            foreach (var group in events.Where(e => e.GetProperty("kind").GetString() is "cast" or "icon" or "effect")
                .GroupBy(e => e.GetProperty("t").GetDouble()))
            {
                var t = group.Key - offset + .02;
                var before = group.Key - offset - .001;
                if (before > (double)controller.Clock)
                {
                    Advance(before);
                    foreach (var effect in group.Where(e => e.GetProperty("kind").GetString() == "effect"))
                    {
                        var action = effect.GetProperty("action").GetUInt32();
                        // Haywire telegraphs are consumed by the independent 8184 hit queue.
                        if (!AoeActions.Contains(action) || action == 7960) continue;
                        var source = effect.GetProperty("id").GetUInt64();
                        if (!events.Any(e => e.GetProperty("kind").GetString() == "cast" && e.GetProperty("id").GetUInt64() == source
                            && e.GetProperty("action").GetUInt32() == action && e.GetProperty("t").GetDouble() < group.Key)) continue;
                        Check(!VisibleCast(source, action) || Output().Any(a => a.Key.StartsWith($"cast/{source}/{action}/", StringComparison.Ordinal)),
                            $"Action {action}, source={source}, t={group.Key}: output disappeared before the recorded effect.");
                    }
                }
                Advance(t);
                foreach (var cast in group)
                {
                    if (cast.GetProperty("kind").GetString() != "cast") continue;
                    var action = cast.GetProperty("action").GetUInt32();
                    if (!AoeActions.Contains(action)) continue;
                    var prefix = $"cast/{cast.GetProperty("id").GetUInt64()}/{action}/";
                    var areas = Output().Where(a => a.Key.StartsWith(prefix, StringComparison.Ordinal)
                        && a.Key.Count(c => c == '/') == 3).ToArray();
                    if (!VisibleCast(cast.GetProperty("id").GetUInt64(), action))
                    {
                        Check(areas.Length == 0, "Capped helper leaked into production output.");
                        ++checkedCasts;
                        continue;
                    }
                    Check(areas.Length == 1, $"Abania boss={boss}, player={player}, action={action}, t={group.Key}: expected one cast output, got {areas.Length}.");
                    var area = areas[0].Area;
                    var previewOnly = (bool)area.GetType().GetProperty("PreviewOnly")!.GetValue(area)!;
                    var actualEffect = events.FirstOrDefault(e => e.GetProperty("kind").GetString() == "effect"
                        && e.GetProperty("id").GetUInt64() == cast.GetProperty("id").GetUInt64()
                        && e.GetProperty("action").GetUInt32() == action
                        && e.GetProperty("t").GetDouble() > group.Key
                        && e.GetProperty("t").GetDouble() < group.Key + cast.GetProperty("seconds").GetDouble() + 1);
                    if (actualEffect.ValueKind != JsonValueKind.Undefined)
                    {
                        var actualHit = Start.AddSeconds(actualEffect.GetProperty("t").GetDouble() - offset);
                        var from = (DateTime)area.GetType().GetProperty("From")!.GetValue(area)!;
                        var until = (DateTime)area.GetType().GetProperty("Until")!.GetValue(area)!;
                        // The source activation is the predicted cast finish. Recorded packets in this
                        // fixture precede it by up to 84ms; this checks prediction error, not damage coverage.
                        Check(from <= actualHit.AddSeconds(.1) && until >= actualHit,
                            $"Action {action}, source={cast.GetProperty("id")}, cast={group.Key}: From={from:O}, Until={until:O}, effect={actualHit:O}, error={(from - actualHit).TotalMilliseconds:F1}ms.");
                    }
                    if (action == 33461)
                        Check(previewOnly == (cast.GetProperty("target").GetUInt64() == player),
                            "Blizzard II must only draw the current player's own spread.");
                    if (action is 7957 or 7960 or 33460 or 7975 or 7976 or 7977)
                    {
                        var from = (DateTime)area.GetType().GetProperty("From")!.GetValue(area)!;
                        var until = (DateTime)area.GetType().GetProperty("Until")!.GetValue(area)!;
                        var hit = Start.AddSeconds(group.Key - offset + cast.GetProperty("seconds").GetDouble()
                            + (action == 7960 ? .1 : .3));
                        Check(from <= hit.AddSeconds(.00001) && hit <= until, "Production danger window lost the packet cast timing.");
                    }
                    if (!solved && player == 100 && action is 7957 or 33460 or 7977)
                    {
                        CheckEscape(assembly, runtime);
                        solved = true;
                    }
                    ++checkedCasts;
                    if (action is 7957 or 33460 or 7977) seekTime ??= t;
                }
                foreach (var marker in group.Where(e => e.GetProperty("kind").GetString() == "icon"))
                {
                    var target = marker.GetProperty("target").GetUInt64();
                    var area = Output().Single(a => a.Key == $"spread/{target}").Area;
                    Check((bool)area.GetType().GetProperty("PreviewOnly")!.GetValue(area)! == (target == player),
                        "Rahu Ray's own circle must be preview only in each recorded perspective.");
                }
                foreach (var effect in group.Where(e => e.GetProperty("kind").GetString() == "effect"))
                {
                    var action = effect.GetProperty("action").GetUInt32();
                    if (action == 33467)
                        Check(Output().Any(a => a.Key == $"bait/{effect.GetProperty("id").GetUInt64()}"),
                            "The recorded Sparking Current marker did not arm an aimed line.");
                    else if (action == 33466)
                        Check(Output().All(a => !a.Key.StartsWith("bait/", StringComparison.Ordinal)),
                            "The recorded Sparking Current hit retained bait navigation.");
                    else if (action == 7978)
                        foreach (var target in effect.GetProperty("targets").EnumerateArray())
                            Check(Output().All(a => a.Key != $"spread/{target.GetUInt64()}"),
                                "The recorded Rahu Ray effect retained its spread.");
                }
            }
            Check(seekTime != null, "Each boss recording must contain a damaging cast.");
            controller.Seek(seekTime!.Value);
            controller.Pause();
            Check(Dangers() > 0 && Previews() > 0, "Backward seek failed to rebuild Abania navigation and previews.");
            var timeBefore = (DateTime)runtime.CurrentTime;
            var dangerBefore = Dangers();
            controller.Update(10f);
            manager.Update(10f);
            Check(runtime.CurrentTime == timeBefore && Dangers() == dangerBefore,
                "Paused Abania replay advanced or removed its output.");
            manager.SetScriptEnabled(ScriptId, false);
            Check(Dangers() == 0 && Previews() == 0, "Disabling Abania retained output.");
            manager.SetScriptEnabled(ScriptId, true);
            controller.Seek(seekTime.Value);
            Check(Dangers() > 0, "Re-enabling Abania did not rebuild output.");
            controller.Seek(end);
            Check(Dangers() == 0 && Previews() == 0, "The final empty frame retained Abania output.");
            controller.StopSession();
            Check(Dangers() == 0 && Previews() == 0, "Stopping Abania retained output.");
        }
        Console.WriteLine($"PASS production Castrum Abania recording: {checkedCasts} cast perspectives, three real solver escapes, duplicate effects, personal spreads, pause, seek, disable and cleanup");
    }

    private static void CheckEscape(Assembly assembly, dynamic runtime)
    {
        dynamic snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        var dangers = ((IEnumerable)snapshot.DangerAreas).Cast<dynamic>().ToArray();
        var center = (Vector3)snapshot.ArenaBoundary.Position;
        Vector3? start = null;
        var candidates = from x in Enumerable.Range(-15, 31)
                         from z in Enumerable.Range(-15, 31)
                         orderby x * x + z * z
                         select center + new Vector3(x, 0, z);
        foreach (var candidate in candidates)
        {
            if (!(bool)snapshot.ArenaBoundary.Contains(candidate, 0f)
                || !dangers.Any(d => (bool)d.Geometry.Contains(candidate, 0f))) continue;
            start = candidate;
            break;
        }
        Check(start != null, "The recording must supply a dangerous starting position inside the arena.");
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        dynamic state = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [start!.Value, 0f, 6f, false, false, 0f, true, (int)snapshot.Version])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        dynamic solution = solver.Solve(snapshot, state, capability, options, runtime.CurrentTime, null);
        var fallback = (bool)solution.GetType().GetProperty("IsLeastDangerFallback", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue((object)solution)!;
        Check((bool)solution.IsSuccess && !fallback,
            $"The real solver failed to escape the recorded AOE: status={solution.Status}, fallback={fallback}, start={start}, arena={center}, dangers={dangers.Length}.");
        dynamic[] path = ((IEnumerable)solution.ExecutablePath).Cast<dynamic>().ToArray();
        Check(path.Length > 0, "A dangerous recorded AOE must produce an escape route.");
        Check(path.All(p => (bool)snapshot.ArenaBoundary.Contains((Vector3)p.Position, 0f)),
            "The recorded escape crossed the arena boundary.");
        var destination = (Vector3)path[^1].Position;
        Check(dangers.All(d => !(bool)d.Geometry.Contains(destination, 0f)),
            "The recorded escape still ends inside an active AOE.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
