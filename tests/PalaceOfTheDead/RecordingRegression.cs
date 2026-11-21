using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

internal static class RecordingRegression
{
    internal static void Run()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "recordings.json")));
        var root = document.RootElement;
        var actions = root.GetProperty("actions").EnumerateArray().ToDictionary(a => a.GetProperty("id").GetUInt32());
        var total = 0;
        var coverage = new List<int>();
        foreach (var recording in root.GetProperty("recordings").EnumerateArray())
        {
            var encounter = new PalaceEncounter();
            var actors = new Dictionary<ulong, ReplayActorState>();
            var casts = new Dictionary<ulong, uint>();
            var allCasts = 0;
            var areas = 0;
            var responses = 0;
            var ignored = 0;
            var canceled = 0;
            var effects = 0;
            var maxOriginDelta = 0f;
            var source = recording.GetProperty("source");
            Program.Check(source.GetProperty("territory").GetUInt32() == source.GetProperty("cfc").GetUInt32() + 389, "CFC/territory mismatch");
            foreach (var ev in recording.GetProperty("events").EnumerateArray())
            {
                var now = Program.Epoch.AddSeconds(ev.GetProperty("t").GetDouble());
                var id = ev.GetProperty("entity").GetUInt64();
                var kind = ev.GetProperty("kind").GetString();
                if (kind is "spawn" or "objectSpawn")
                {
                    actors[id] = Program.Actor(id, ev.GetProperty("oid").GetUInt32(), Vector(ev.GetProperty("pos"))) with
                    {
                        // ARR NPC spawn does not encode authoritative hitbox size. Reference floor inference uses zero.
                        HitboxRadius = 0,
                        Rotation = ev.GetProperty("rot").GetSingle(),
                        InCombat = true,
                    };
                }
                else if (kind == "despawn") actors.Remove(id);
                else if (kind == "death" && actors.TryGetValue(id, out var dead)) actors[id] = dead with { Dead = true };
                else if (kind == "objectState" && actors.TryGetValue(id, out var obj)) actors[id] = obj with { EventState = ev.GetProperty("state").GetByte() };
                if (ev.TryGetProperty("sourcePos", out var position) && position.ValueKind == JsonValueKind.Array && actors.TryGetValue(id, out var actor))
                    actors[id] = actor with { Position = Vector(position), Rotation = ev.GetProperty("sourceRot").GetSingle() };
                encounter.Synchronize(actors.Values, now);
                if (kind == "cast")
                {
                    ++allCasts;
                    var action = ev.GetProperty("action").GetUInt32();
                    Program.Check(actions.TryGetValue(action, out var data), "Missing Action export " + action);
                    var shape = PalaceCatalog.SpecialShape(action);
                    if (!PalaceCatalog.SpecialActions.Contains(action)) shape = ReferenceShape(data);
                    if (action == 7156) shape = RayRectangleTemplate.Symmetric(6, 1);
                    var loc = Vector(ev.GetProperty("loc"));
                    var origin = actors[id].Position;
                    maxOriginDelta = MathF.Max(maxOriginDelta, Vector3.Distance(origin, loc));
                    var rotation = ev.GetProperty("rotation").GetSingle();
                    var cast = new PalaceCast(id, action, ev.GetProperty("target").GetUInt64(), loc, origin, rotation,
                        now, now.AddSeconds(ev.GetProperty("seconds").GetDouble() + .3), shape, data.GetProperty("name").GetString()!);
                    encounter.StartCast(cast);
                    casts[id] = action;
                    var result = encounter.Collect(now, 0).Where(a => a.Owner == id && a.Action == action && a.Key.StartsWith("cast/")).ToArray();
                    if (shape != null && action != 7156 && !PalaceCatalog.IsGaze(action) && action is not (6365 or 7103 or 7166 or 7171))
                    {
                        ++areas;
                        Program.Check(result.Length == 1, "Missing recorded geometry " + action);
                        Program.Check(result[0].Position == loc && result[0].Rotation == rotation, "Recorded locked pose changed " + action);
                        Program.Check(Math.Abs((result[0].From - cast.Hit).TotalSeconds + .2) < .001, "Danger hit window changed");
                    }
                    else if (encounter.HasResponse(action, now)) ++responses;
                    else
                    {
                        ++ignored;
                        Program.Check(data.GetProperty("castType").GetInt32() == 1
                            || data.GetProperty("range").GetInt32() >= 30, "Unclassified recorded cast " + action);
                    }
                }
                else if (kind == "effect")
                {
                    ++effects;
                    var action = ev.GetProperty("action").GetUInt32();
                    var targets = ev.GetProperty("targets").EnumerateArray().ToArray();
                    var effect = new LogSystemActionEffectEvent
                    {
                        SourceId = id, ActionId = action, GlobalSequence = ev.GetProperty("seq").GetUInt32(),
                        TargetId = targets.Length == 0 ? 0 : targets[0].GetUInt64(), SourcePosition = actors.GetValueOrDefault(id)?.Position,
                    };
                    encounter.Apply(effect, now);
                    if (casts.GetValueOrDefault(id) == action)
                    {
                        Program.Check(!encounter.Collect(now, 0).Any(a => a.Owner == id && a.Action == action && a.Key.StartsWith("cast/")), "Effect retained recorded cast " + action);
                        casts.Remove(id);
                    }
                }
                else if (kind == "cancel")
                {
                    var action = ev.GetProperty("action").GetUInt32();
                    encounter.Apply(new LogSystemCancelCastEvent { SourceId = id, ActionId = action }, now);
                    if (casts.GetValueOrDefault(id) == action)
                    {
                        ++canceled;
                        Program.Check(!encounter.Collect(now, 0).Any(a => a.Owner == id && a.Action == action && a.Key.StartsWith("cast/")), "Recorded cancel retained cast");
                        casts.Remove(id);
                    }
                }
                else if (kind == "statusGain") encounter.Apply(new LogSystemStatusAddEvent { TargetId = id, StatusId = ev.GetProperty("status").GetUInt32() }, now);
                else if (kind == "statusLose") encounter.Apply(new LogSystemStatusRemoveEvent { TargetId = id, StatusId = ev.GetProperty("status").GetUInt32() }, now);
            }
            Program.Check(allCasts == recording.GetProperty("coverage").EnumerateArray().Sum(c => c.GetProperty("casts").GetInt32()), "Fixture coverage count mismatch");
            coverage.Add(allCasts);
            total += allCasts;
            encounter.Reset();
            Program.Check(encounter.Actors.Count == 0 && encounter.Collect(Program.Epoch, 0).Count == 0, "Recording cleanup retained state");
            Console.WriteLine($"  ARR CFC{source.GetProperty("cfc")}/territory{source.GetProperty("territory")}: casts={allCasts}; exactPoseAreas={areas}; specialResponses={responses}; singleOrRaidwide={ignored}; effects={effects}; canceled={canceled}; maxLockedOriginDelta={maxOriginDelta:F3}");
        }
        Program.Check(total == 161 && coverage.SequenceEqual(new[] { 15, 5, 99, 7, 35 }), "Five-recording cast coverage changed");
    }

    private static Vector3 Vector(JsonElement array) => new(array[0].GetSingle(), array[1].GetSingle(), array[2].GetSingle());

    // Independent reference dimensions from the captured Lumina rows; hitbox is intentionally unknown/zero.
    private static GeometryTemplate? ReferenceShape(JsonElement action)
    {
        var type = action.GetProperty("castType").GetInt32();
        var radius = action.GetProperty("range").GetSingle();
        if (action.GetProperty("vfx").GetInt32() == 25 || type is 2 or 5 && radius >= 30) return null;
        if (type is 2 or 5 && radius > 0) return new CircleTemplate(radius);
        if (type is 3 or 13)
        {
            var omen = action.GetProperty("omen").GetString()!;
            var at = omen.IndexOf("fan", StringComparison.Ordinal);
            var degrees = at >= 0 && at + 6 <= omen.Length && int.TryParse(omen.AsSpan(at + 3, 3), out var parsed) ? parsed : 180;
            return new FanTemplate(radius, degrees * MathF.PI / 180);
        }
        return null;
    }
}
