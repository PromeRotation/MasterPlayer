using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunNecron(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!
            .Invoke(Encounter(), null)!).Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("necron/" + prefix)).ToArray();
        int Portals() => ((IEnumerable)runtime.Teleporters).Cast<object>().Count();
        string Map(double time, uint index, uint state) => JsonSerializer.Serialize(new
        {
            t = time, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(time) }
        });
        string Director(double time, uint command, uint param1) => JsonSerializer.Serialize(new
        {
            t = time, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
            data = new { Command = command, Param1 = param1, Timestamp = Start.AddSeconds(time) }
        });
        string Icon(double time, uint icon) => JsonSerializer.Serialize(new
        {
            t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = 1UL, TargetId = 1UL, MarkerId = icon, Timestamp = Start.AddSeconds(time) }
        });
        const string id = "masterplayer.trial.necron";
        var c = new Vector3(100, 0, 100);
        object[] party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        var actors = new List<object> { Actor(1, 0x4870, c, combat: true), Actor(30, 0x4903, new(90, 0, 85)) };
        actors.AddRange(Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, c + Vector3.UnitX * i, classJob: i < 2 ? 19u : 20u)));
        actors.AddRange(Enumerable.Range(10, 20).Select(i => Actor((ulong)i, 0x233C, c)));
        load(id,
        [
            Header(1295), Frame(1295, 0, actors, full: true, party: party),
            Frame(1295, .1, [Actor(1, 0x4870, c, combat: true, cast: Cast(44521, 5, c)),
                Actor(10, 0x233C, c, cast: Cast(44611, 5, c)), Actor(11, 0x233C, c, cast: Cast(44611, 6, c)),
                Actor(12, 0x233C, c, cast: Cast(44541, 4, c)), Actor(13, 0x233C, c, cast: Cast(44538, 4, c))], party: party),
            Frame(1295, .2, [Actor(1, 0x4870, c, combat: true, cast: Cast(44525, 5, c, MathF.PI / 2, startedAt: .2))], party: party),
            Icon(.3, 604), Icon(.4, 605), Map(.5, 0x1A, 0x00020001),
            Frame(1295, 1, [Actor(100, 0, new(100, -101, -100), classJob: 19)], party: party),
            Map(1.1, 0, 0x00020001),
            Frame(1295, 2, [], removed: [1], party: party),
            Frame(1295, 3, [Actor(100, 0, c, classJob: 19)], party: party),
            Director(3.1, 0x8000000D, 2),
            Frame(1295, 5.5, [Actor(10, 0x233C, c), Actor(12, 0x233C, c), Actor(13, 0x233C, c)], party: party),
            Frame(1295, 6.5, [Actor(11, 0x233C, c)], party: party),
            EffectEvent(7, 15, 44542, 1), EffectEvent(7.1, 15, 44539, 2),
            Map(7.2, 0x1A, 0x00080004), Director(8, 0x80000029, 0),
            Frame(1295, 9, [], party: party)
        ]);
        controller.Seek(.8);
        Check(Group("hand").Length == 1 && Group("fear").Length == 1 && Group("cold").Length == 3
            && (float)Value(Group("cold")[2], "Rotation") == MathF.PI / 2 && Group("blight").Any()
            && Group("tower").Any() && dangers() > 0 && previews().Length > 0, "Necron initial native outputs.");
        controller.Seek(1.5);
        Check(Portals() == 3 && Group("prison").Count(a => (bool)Value(a, "Display")) == 4, "Necron prison portals/drawing.");
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(boundary.Contains(new Vector3(100, -101, -100)) && !boundary.Contains(new Vector3(100, -101, -115)),
            "Necron prison disconnected islands.");
        Check(((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Any(), "Necron prison soft exit goal.");

        var runtimeType = runtimeObject.GetType();
        var setOwner = runtimeType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(m => m.Name == "SetTeleporters" && m.GetParameters().Length == 2);
        var elementType = runtimeType.GetProperty("Teleporters")!.PropertyType.GetGenericArguments()[0];
        var edges = ((IEnumerable)runtime.Teleporters).Cast<object>().ToArray();
        Array EdgeList(params object[] values)
        {
            var list = Array.CreateInstance(elementType, values.Length);
            for (var i = 0; i < values.Length; ++i) list.SetValue(values[i], i);
            return list;
        }
        var ownerA = new object(); var ownerB = new object();
        setOwner.Invoke(runtimeObject, [ownerA, EdgeList(edges[0])]);
        setOwner.Invoke(runtimeObject, [ownerB, EdgeList(edges[1])]);
        manager.SetScriptEnabled(id, false);
        Check(Portals() == 2 && dangers() == 0 && previews().Length == 0, "Script stop overwrote unrelated portal owners.");
        setOwner.Invoke(runtimeObject, [ownerA, EdgeList()]);
        Check(Portals() == 1, "Clearing one portal owner removed another.");
        var published = (object)runtime.Teleporters;
        setOwner.Invoke(runtimeObject, [ownerB, EdgeList(edges[1])]);
        Check(ReferenceEquals(published, (object)runtime.Teleporters), "Unchanged portal publication rebuilt solver edges.");
        runtimeType.GetMethod("ClearCombatState", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(runtimeObject, null);
        Check(Portals() == 0, "Combat clear leaked native portal owners.");
        manager.SetScriptEnabled(id, true);
        controller.Seek(2.5);
        Check(Portals() == 3 && Group("blight").Any() && Group("cold").Length == 3, "Missing primary erased active Necron phase.");
        controller.Seek(3.5);
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(Portals() == 0 && boundary.Contains(new Vector3(108, 0, 100)) && !boundary.Contains(new Vector3(110, 0, 100)),
            "Director Param1/ground circle restore.");
        controller.Seek(6.8);
        Check(Group("cold").Length == 1 && Group("fear").Any(), "Necron cold independent FIFO stream.");
        controller.Seek(8.5);
        Check(previews().Length == 0 && dangers() == 0 && Portals() == 0, "Necron director wipe cleanup.");
        controller.Seek(1.5);
        Check(Portals() == 3 && Group("prison").Any(), "Necron reverse Seek did not rebuild portal state.");
        manager.SetScriptEnabled(id, false);
        Check(Portals() == 0 && previews().Length == 0 && dangers() == 0
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Necron disabled script leaked outputs.");
        stop();
        Console.WriteLine("PASS Necron actual DLL replay delayed hand streams, cold direction/FIFO, stored blight, towers, disconnected prison, directed portals, owner isolation, director Param1, missing primary, wipe and reverse Seek");
    }
}
