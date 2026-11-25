using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunPortrait(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Aoes(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix)).ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        const string id = "masterplayer.quest.somewhere-only-she-knows";
        var center = new Vector3(0, 0, -340);
        string Create(double t, ulong source, Vector3 position, float rotation) => JsonSerializer.Serialize(new
        {
            t, kind = "ObjectCreate", type = "PromeRotation.LogSystem.LogSystemObjectCreateEvent",
            data = new { SourceId = source, DataId = 0x4299u, ObjectKind = 2, SourcePosition = Pose(position), SourceRotation = rotation, Timestamp = Start.AddSeconds(t) },
        });
        string WaterEffect(double t, uint sequence, Vector3 position) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "PromeRotation.LogSystem.LogSystemActionEffectEvent",
            data = new { SourceId = 4ul, ActionId = 37536u, GlobalSequence = sequence, EffectPosition = Pose(center + Vector3.UnitX * 100),
                SourcePosition = Pose(position), SourceRotation = 2f, Timestamp = Start.AddSeconds(t) },
        });
        string CorrectedCast(double t, uint action, Vector3 location, Vector3 sourcePosition, float rotation) => JsonSerializer.Serialize(new
        {
            t, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = rotation,
            data = new { SourceId = 2ul, ActionId = action, CastTime = 5f, EffectPosition = Pose(location),
                SourcePosition = Pose(sourcePosition), Timestamp = Start.AddSeconds(t) },
        });
        string[] Initial() => [Header(1234), Frame(1234, 0, [Actor(1, 0x4298, center, combat: true), Actor(100, 0, center)], full: true, party: party)];
        load(id,
        [
            .. Initial(),
            Create(1, 2, center + Vector3.UnitX * 5, .5f),
            Frame(1234, 1.1, [Actor(2, 0x4299, center + Vector3.UnitX * 10, rotation: 2)], party: party),
            Create(3, 3, center - Vector3.UnitX * 5, 1),
            Frame(1234, 3.1, [Actor(3, 0x4299, center - Vector3.UnitX * 10, rotation: 2)], party: party),
            EffectEvent(4, 2, 37527, 1),
            Frame(1234, 5, [Actor(2, 0x4299, center), Actor(3, 0x4299, center)], party: party),
            Frame(1234, 6, [], party: party),
        ]);
        controller.Seek(2d);
        var flower = Aoes("portrait/flower").Single();
        Check((Vector3)Value(flower, "Position") == center + Vector3.UnitX * 5 && (float)Value(flower, "Rotation") == .5f
            && (DateTime)Value(flower, "Activation") == Start.AddSeconds(10.8)
            && dangers() == 1, "Portrait creation event pose/timing was replaced by later object observation.");
        controller.Seek(3.5);
        Check((Vector3)Value(Aoes("portrait/flower").Single(), "Position") == center - Vector3.UnitX * 5 && dangers() == 1,
            "Second Portrait flower did not replace singleton prediction.");
        controller.Seek(5.5);
        Check(dangers() == 0 && Aoes("portrait/flower").Length == 0, "Old flower effect did not clear current flower or stale frame resurrected it.");
        controller.Seek(2d);
        Check(dangers() == 1, "Portrait flower reverse Seek reconstruction.");
        stop();

        load(id,
        [
            .. Initial(),
            Frame(1234, 10, [Actor(4, 0x233C, center, cast: Cast(37535, 5, center + Vector3.UnitZ * 2, startedAt: 10))], party: party),
            CastEvent(10, 4, 37535, 5, center + Vector3.UnitZ * 2),
            WaterEffect(11, 1, center - Vector3.UnitX * 5),
            WaterEffect(11.01, 1, center - Vector3.UnitX * 12.5f),
            Frame(1234, 12, [Actor(4, 0x233C, center)], party: party),
            Frame(1234, 30, [], party: party),
        ]);
        controller.Seek(10.5);
        Check(dangers() == 5 && Aoes("portrait/water/").Length == 5, "Portrait water first/current/future navigation or previews.");
        Check(Aoes("portrait/water/").Count(a => Value(a, "Color") is Vector4 c && c == new Vector4(1, 1, 0, 1)) == 3,
            "Portrait first and imminent water danger colors.");
        controller.Seek(11.5);
        var current = Aoes("portrait/water/0/current").Single();
        Check(Vector3.Distance((Vector3)Value(current, "Position"), center - Vector3.UnitX * 12.5f) < .045f
            && (DateTime)Value(current, "Activation") == Start.AddSeconds(13), "Water effect-caster correction or duplicate effect isolation.");
        var futureTime = (DateTime)Value(Aoes("portrait/water/0/next").Single(), "Activation");
        controller.Seek(20d);
        Check(dangers() == 4 && Aoes("portrait/water/").Length == 4
            && (DateTime)Value(Aoes("portrait/water/0/next").Single(), "Activation") == futureTime,
            "Water first finish or passing time erased/slid persistent lines.");
        controller.Seek(10.5);
        Check(dangers() == 5, "Portrait water reverse Seek.");
        stop();

        load(id,
        [
            .. Initial(),
            Frame(1234, 1, [
                Actor(10, 0x233C, center, cast: Cast(37531, 5, center, startedAt: 1)),
                Actor(11, 0x233C, center, cast: Cast(37532, 7, center + new Vector3(.9f, 0, .9f), startedAt: 1)),
                Actor(12, 0x233C, center, cast: Cast(37533, 9, center, startedAt: 1))], party: party),
            Frame(1234, 2, [Actor(10, 0x233C, center)], party: party),
            Frame(1234, 3, [], removed: [11], party: party),
            Frame(1234, 20, [Actor(12, 0x233C, center)], party: party),
            Frame(1234, 21, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(dangers() == 1 && Aoes("portrait/ring/").Single() is { } first
            && Value(first, "Shape").GetType().Name == "CircleTemplate", "Portrait earthquake first ring only.");
        controller.Seek(2.5);
        Check(dangers() == 1 && (DateTime)Value(Aoes("portrait/ring/").Single(), "Activation") == Start.AddSeconds(4),
            "Portrait earthquake short-cancel progression.");
        controller.Seek(3.5);
        var ring = Aoes("portrait/ring/").Single();
        Check((float)Value(Value(ring, "Shape"), "InnerRadius") == 20
            && (DateTime)Value(ring, "Activation") == Start.AddSeconds(5), "Earthquake destruction/per-axis diagonal progression.");
        controller.Seek(19d);
        Check(dangers() == 1, "Portrait earthquake ended by predicted clock.");
        controller.Seek(20.5);
        Check(dangers() == 0 && Aoes("portrait/ring/").Length == 0, "Portrait third ring cleanup.");
        stop();

        var records = new List<string>(Initial());
        records.Add(Frame(1234, 1, Enumerable.Range(2, 10).Select(i => Actor((ulong)i, 0x233C, center,
            cast: Cast(37529, 3, center, startedAt: 1))), party: party));
        records.Add(Frame(1234, 2, [Actor(2, 0x233C, center)], party: party));
        records.Add(Frame(1234, 3, [Actor(20, 0x233C, center, cast: Cast(37529, 3, center, startedAt: 3))], party: party));
        records.Add(Frame(1234, 3.1, [Actor(4, 0x233C, center, cast: Cast(37529, 3.9, center, startedAt: 1.1))], party: party));
        records.Add(Frame(1234, 4, Enumerable.Range(3, 9).Select(i => Actor((ulong)i, 0x233C, center))
            .Append(Actor(20, 0x233C, center)), party: party));
        records.Add(Frame(1234, 5, [Actor(1, 0x4298, center, combat: true, cast: Cast(37537, 3, center + Vector3.UnitZ * 120, startedAt: 5))], party: party));
        records.Add(Frame(1234, 9, [Actor(1, 0x4298, center, combat: true, cast: Cast(37539, 6, center, startedAt: 9))], party: party));
        records.Add(Frame(1234, 16, [Actor(1, 0x4298, center, dead: true), Actor(30, 0x4299, center)], party: party));
        records.Add(Frame(1234, 17, [], party: party));
        load(id, records);
        controller.Seek(1.5);
        Check(dangers() == 9, "Nine Ivies first-nine cap.");
        controller.Seek(3.05);
        var castSources = ((IEnumerable)Value(Encounter(), "OutputCasts")).Cast<object>().Select(c => Value(c, "Value")).ToArray();
        Check(dangers() == 9 && castSources.Any(c => (ulong)Value(c, "Source") == 11ul)
            && castSources.All(c => (ulong)Value(c, "Source") != 20ul), "Nine Ivies insertion ordering after dictionary slot reuse.");
        controller.Seek(3.5);
        castSources = ((IEnumerable)Value(Encounter(), "OutputCasts")).Cast<object>().Select(c => Value(c, "Value")).ToArray();
        Check(dangers() == 9 && castSources.Select(c => (ulong)Value(c, "Source"))
            .SequenceEqual(new ulong[] { 3, 5, 6, 7, 8, 9, 10, 11, 20 }), "Ivy accepted replacement generation lost first-nine membership or order.");
        controller.Seek(7.6);
        Check(dangers() == 0 && Aoes("portrait/gaze/").Length == 3
            && Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [center, 0f]) is float, "Portrait gaze/facing connection.");
        controller.Seek(10d);
        Check(dangers() == 0 && ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().Count() == 1,
            "Portrait raidwide damage/non-spatial handling.");
        controller.Seek(16.5);
        Check(previews().Length == 0 && dangers() == 0, "Portrait primary-only death failed to clear flower/output.");
        stop();
        load(id,
        [
            .. Initial(),
            Frame(1234, 1, [Actor(2, 0x233C, center, cast: Cast(37535, 5, center, startedAt: 1))], party: party),
            CorrectedCast(1, 37535, center, center + Vector3.UnitX * 10, MathF.PI / 2),
            Frame(1234, 3, [Actor(2, 0x233C, center, cast: Cast(37531, 5, center, startedAt: 3))], party: party),
            CorrectedCast(3, 37531, center + Vector3.UnitX * 2, center, 0),
            Frame(1234, 4, [Actor(2, 0x233C, center)], party: party),
            Frame(1234, 5, [Actor(1, 0x4298, center, dead: true)], party: party),
            Frame(1234, 6, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(Aoes("portrait/water/").Where(a => ((string)Value(a, "Id")).EndsWith("/current"))
            .All(a => MathF.Abs(((Vector3)Value(a, "Position")).X - 10) < .045f), "Production water packet source-position/rotation correction.");
        controller.Seek(3.5);
        Check((Vector3)Value(Aoes("portrait/ring/").Single(), "Position") == center + Vector3.UnitX * 2,
            "Production earthquake authoritative packet origin correction.");
        controller.Seek(4.5);
        Check((float)Value(Value(Aoes("portrait/ring/").Single(), "Shape"), "InnerRadius") == 10,
            "Production corrected earthquake failed to advance.");
        stop();
        Console.WriteLine("PASS production Portrait: creation-pose singleton flower, old-flower effect, cast-finish earthquake and diagonal/destroy matching, two independently corrected water lines with cached times and colors, first-nine insertion order, 10000-range gaze, raidwide, reverse Seek and cleanup");
    }
}
