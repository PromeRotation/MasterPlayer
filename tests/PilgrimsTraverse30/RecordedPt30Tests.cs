using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.PilgrimsTraverse;

internal static class RecordedPt30Tests
{
    public static void Run(string path)
    {
        using var input = File.OpenText(path);
        using var header = JsonDocument.Parse(input.ReadLine()!);
        var started = header.RootElement.GetProperty("startedUtc").GetDateTime().ToUniversalTime();
        var encounter = new Pt30Encounter();
        var actors = new Dictionary<ulong, Pt30Encounter.Actor>();
        ulong playerId = 0;
        var player = Vector3.Zero;
        var classifiedHits = 0;
        var chasePulses = 0;
        var maxDangers = 0;
        var selfHits = new Dictionary<uint, int>();
        string? line;
        while ((line = input.ReadLine()) != null)
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var now = started.AddSeconds(root.GetProperty("t").GetDouble());
            var data = root.GetProperty("data");
            switch (root.GetProperty("kind").GetString())
            {
                case "WorldFrame":
                    if (data.GetProperty("Full").GetBoolean()) actors.Clear();
                    playerId = data.GetProperty("LocalPlayerId").GetUInt64();
                    foreach (var id in data.GetProperty("Removed").EnumerateArray()) actors.Remove(id.GetUInt64());
                    foreach (var actor in data.GetProperty("Actors").EnumerateArray())
                    {
                        var id = actor.GetProperty("Id").GetUInt64();
                        var position = Position(actor.GetProperty("Position"));
                        if (id == playerId) player = position;
                        var oid = actor.GetProperty("Oid").GetUInt32();
                        if (oid is Pt30Encounter.BOSS_OID or Pt30Encounter.BALL_OID)
                            actors[id] = new(id, oid, position, actor.GetProperty("Rotation").GetSingle(),
                                actor.GetProperty("Dead").GetBoolean(), actor.GetProperty("InCombat").GetBoolean());
                    }
                    encounter.Synchronize(actors.Values, now);
                    maxDangers = Math.Max(maxDangers, encounter.Hazards(now, playerId, player).Count);
                    break;
                case "TargetIcon":
                    encounter.Icon(data.GetProperty("TargetId").GetUInt64(), data.GetProperty("MarkerId").GetUInt32(), now);
                    break;
                case "PlayActionTimeline":
                    encounter.Timeline(data.GetProperty("SourceId").GetUInt64(), (ushort)data.GetProperty("TimelineId").GetUInt32());
                    break;
                case "CastStart":
                    var action = data.GetProperty("ActionId").GetUInt32();
                    if (!Pt30Encounter.Actions.Contains(action)) break;
                    var rotation = root.GetProperty("packetFacing").GetSingle();
                    encounter.Cast(data.GetProperty("SourceId").GetUInt64(), action,
                        Position(data.GetProperty("EffectPosition")), rotation, now,
                        now.AddSeconds(data.GetProperty("CastTime").GetDouble() + .3));
                    break;
                case "ActionEffect":
                    action = data.GetProperty("ActionId").GetUInt32();
                    if (!Pt30Encounter.Actions.Contains(action)) break;
                    var source = data.GetProperty("SourceId").GetUInt64();
                    if (data.GetProperty("TargetId").GetUInt64() == playerId)
                    {
                        selfHits[action] = selfHits.GetValueOrDefault(action) + 1;
                        if (action is 39642 or 39643 or 39674 or 39743 or 40551 or 44927 or 40552 or 40553)
                        {
                            var targetPosition = Position(data.GetProperty("TargetPosition"));
                            var hazard = encounter.Hazards(now, playerId, player).Single(h => h.Key == $"cast/{source}/{action}");
                            if (!hazard.Shape.Resolve(hazard.Position, hazard.Rotation, null).Contains(targetPosition))
                                throw new InvalidOperationException($"Recorded hit {action} at {root.GetProperty("t")} was not classified as dangerous.");
                            classifiedHits++;
                        }
                    }
                    if (action is 44918 or 44919) chasePulses++;
                    encounter.Effect(source, data.GetProperty("GlobalSequence").GetUInt32(), action,
                        Position(data.GetProperty("EffectPosition")), now);
                    break;
            }
        }
        if (classifiedHits == 0 || chasePulses == 0 || encounter.Remaining != 0)
            throw new InvalidOperationException("Recording lacks resolved mechanisms or leaves a stale chase.");
        Console.WriteLine($"PASS actual PT30 recording: {classifiedHits} observed ring/Grip/Reach hits correctly classified; {chasePulses} chase effects, max {maxDangers} native hazards; final chase cleared. Original movement is recorded, not resimulated.");
        Console.WriteLine("Recorded target-self effects: " + JsonSerializer.Serialize(selfHits));
    }

    private static Vector3 Position(JsonElement value) => new(value.GetProperty("X").GetSingle(), value.GetProperty("Y").GetSingle(), value.GetProperty("Z").GetSingle());
}
