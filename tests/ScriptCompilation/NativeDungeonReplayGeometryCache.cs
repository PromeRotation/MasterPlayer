using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunGeometryCache(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(-600, 0, -300);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object Value(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, flags) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Script(string id) => Value(manager.Running[id], "Script");
        object Output(string id) => Value(Script(id), "_output");
        object Encounter(string id) => Value(Script(id), "_encounter");
        void Apply(string id) => Output(id).GetType().GetMethod("Apply")!
            .Invoke(Output(id), [Encounter(id), (DateTime)runtime.CurrentTime, true]);
        Dictionary<string, (object Aoe, object Handle)> Areas(string id, string prefix)
        {
            var result = new Dictionary<string, (object, object)>();
            foreach (DictionaryEntry entry in (IDictionary)Value(Output(id), "ambientAreas"))
                if (((string)entry.Key).StartsWith(prefix, StringComparison.Ordinal))
                    result.Add((string)entry.Key, (Value(entry.Value!, "Item1"), Value(entry.Value!, "Item2")));
            return result;
        }
        object Shape(string id, string prefix) => Value(Areas(id, prefix).First().Value.Aoe, "Shape");
        void Stable(string id, string prefix)
        {
            var before = Areas(id, prefix);
            Check(before.Count > 0, $"{prefix}: fixture has no navigation areas.");
            var version = (int)runtime.Manager.Version;
            for (var i = 0; i < 20; ++i) Apply(id);
            var after = Areas(id, prefix);
            Check((int)runtime.Manager.Version == version, $"{prefix}: unchanged input churned registry version.");
            Check(before.Count == after.Count && before.All(pair => after.TryGetValue(pair.Key, out var current)
                && Equals(pair.Value.Handle, current.Handle)
                && ReferenceEquals(Value(pair.Value.Aoe, "Shape"), Value(current.Aoe, "Shape"))),
                $"{prefix}: unchanged input recreated geometry or handles.");
        }
        void Set(object obj, string property, object value)
            => obj.GetType().GetProperty(property, flags)!.SetValue(obj, value);
        void Changed(string id, string prefix, Action change)
        {
            var shape = Shape(id, prefix);
            var version = (int)runtime.Manager.Version;
            change();
            Apply(id);
            Check(!ReferenceEquals(shape, Shape(id, prefix)) && (int)runtime.Manager.Version > version,
                $"{prefix}: geometry change retained stale navigation.");
            Stable(id, prefix);
        }
        void EmptyStable(string id, string key)
        {
            var cache = (IDictionary)Value(Output(id), "adaptedGeometry");
            Check(cache.Contains(key) && ((Array)Value(cache[key]!, "Item2")).Length == 0,
                $"{key}: fixture did not exercise an empty contour.");
            var inputs = Value(cache[key]!, "Item1");
            var version = (int)runtime.Manager.Version;
            for (var i = 0; i < 20; ++i) Apply(id);
            Check(ReferenceEquals(inputs, Value(cache[key]!, "Item1")) && (int)runtime.Manager.Version == version,
                $"{key}: unchanged empty contour was resampled.");
        }
        void EmptyChanged(string id, string key, Action change)
        {
            var cache = (IDictionary)Value(Output(id), "adaptedGeometry");
            var inputs = Value(cache[key]!, "Item1");
            change();
            Apply(id);
            Check(!ReferenceEquals(inputs, Value(cache[key]!, "Item1")), $"{key}: geometry change was ignored.");
            EmptyStable(id, key);
        }
        void Cleanup(string id)
        {
            var output = Output(id);
            output.GetType().GetMethod("Clear")!.Invoke(output, null);
            Check(((IDictionary)Value(output, "adaptedGeometry")).Count == 0
                && ((IDictionary)Value(output, "ambientAreas")).Count == 0, "Output clear retained cached geometry.");
            stop();
        }

        const string excalibur = "masterplayer.eureka.99";
        load(excalibur,
        [
            Header(1108),
            Frame(1108, 0, [Actor(1, 0x3CFC, center, combat: true), Actor(2, 0x233C, center),
                Actor(100, 0, center + Vector3.UnitX * 8), Actor(101, 0, center - Vector3.UnitX * 8)], full: true, party: party),
            Frame(1108, .1, [Actor(2, 0x233C, center, cast: Cast(31350, 5, center, startedAt: .1))], party: party),
        ]);
        controller.Seek(.15);
        const string thermalPrefix = "eureka/excalibur/thermal/inverse/";
        Stable(excalibur, thermalPrefix);
        var thermal = Value(Encounter(excalibur), "EurekaThermalPattern");
        Changed(excalibur, thermalPrefix, () => Set(thermal, "Rotation", .4f));
        var thermalShape = Shape(excalibur, thermalPrefix);
        var activation = ((DateTime)Value(thermal, "Activation")).AddSeconds(1);
        Set(thermal, "Activation", activation);
        Apply(excalibur);
        Check(ReferenceEquals(thermalShape, Shape(excalibur, thermalPrefix))
            && Areas(excalibur, thermalPrefix).Values.All(a => (DateTime)Value(a.Aoe, "Activation") == activation),
            "Thermal timing update rebuilt geometry or retained its old activation.");
        var windows = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>()
            .SelectMany(a => ((IEnumerable)Value(a, "DangerWindows")).Cast<object>()).ToArray();
        Check(windows.Any(w => (DateTime)Value(w, "DangerFrom") == activation),
            "Thermal timing update did not reach the solver snapshot.");
        Encounter(excalibur).GetType().GetField("eurekaThermalPattern", flags)!.SetValue(Encounter(excalibur), null);
        Apply(excalibur);
        Check(Areas(excalibur, thermalPrefix).Count == 0
            && ((IDictionary)Value(Output(excalibur), "adaptedGeometry")).Count == 0,
            "Finished thermal retained navigation or cache.");
        Encounter(excalibur).GetType().GetField("eurekaThermalPattern", flags)!.SetValue(Encounter(excalibur), thermal);
        Apply(excalibur);
        Check(!ReferenceEquals(thermalShape, Shape(excalibur, thermalPrefix)), "Reappearing thermal reused removed geometry.");
        Cleanup(excalibur);
        Console.WriteLine("PASS geometry cache Thermal stable versions handles geometry rotation timing expiry reappearance clear");

        load(excalibur,
        [
            Header(1108),
            Frame(1108, 0, [Actor(1, 0x3CFC, center, combat: true), Actor(2, 0x233C, center),
                Actor(100, 0, center, stackedStatus: 3411), Actor(101, 0, center - Vector3.UnitX * 8)], full: true, party: party),
            Frame(1108, .1, [Actor(1, 0x3CFC, center, combat: true, cast: Cast(31333, 5, center, startedAt: .1))], party: party),
            Frame(1108, .2, Enumerable.Range(0, 10).Select(i => Actor((ulong)(10 + i), 0x3D01, center,
                rotation: (-45 + i * 10) * (MathF.PI / 180), stackedStatus: 2552,
                statusParam: (ushort)(i < 5 ? 0x219 : 0x21A))), party: party),
            Frame(1108, .3, [Actor(2, 0x233C, center, cast: Cast(31330, 5, center, startedAt: .3))], party: party),
        ]);
        controller.Seek(.35);
        const string steelPrefix = "eureka/excalibur/steel/safe-union/";
        Stable(excalibur, steelPrefix);
        var sword = ((IEnumerable)Value(Encounter(excalibur), "EurekaFireSwords")).Cast<object>().First();
        Changed(excalibur, steelPrefix, () => Set(sword, "Rotation", .2f));
        Cleanup(excalibur);
        Console.WriteLine("PASS geometry cache Steel stable versions handles geometry blade rotation clear");

        const string kaliya = "masterplayer.eureka.80";
        load(kaliya,
        [
            Header(1106),
            Frame(1106, 0, [Actor(1, 0x3D18, center, combat: true), Actor(2, 0x233C, center),
                Actor(3, 0x3D19, center + Vector3.UnitX * 15, stackedStatus: 3417),
                Actor(100, 0, center - Vector3.UnitX * 6, stackedStatus: 3418),
                Actor(101, 0, center + Vector3.UnitX * 10)], full: true, party: party),
            Frame(1106, .1, [Actor(3, 0x3D19, center + Vector3.UnitX * 15, stackedStatus: 3417,
                tethers: [new { Id = 38u, TargetId = 100ul }])], party: party),
            Frame(1106, .2, [Actor(2, 0x233C, center, cast: Cast(32930, 7.2, center, startedAt: .2))], party: party),
        ]);
        controller.Seek(.25);
        // Preserve the existing fill-ratio guard; its empty result must also be cached.
        const string magnetKey = "eureka/kaliya/magnetism";
        EmptyStable(kaliya, magnetKey);
        var droneState = ((IDictionary)Value(Encounter(kaliya), "eurekaActors"))[3ul]!;
        var drone = Value(droneState, "Actor");
        EmptyChanged(kaliya, magnetKey, () => Set(drone, "Position", center + Vector3.UnitX * 14));
        EmptyChanged(kaliya, magnetKey, () => Set(drone, "Rotation", .6f));
        Cleanup(kaliya);
        Console.WriteLine("PASS geometry cache Magnetism empty contour cached drone position rotation invalidation clear");

        const string twintania = "masterplayer.eureka.40";
        load(twintania,
        [
            Header(1102),
            Frame(1102, 0, [Actor(1, 0x3D1D, center, combat: true), Actor(2, 0x233C, center),
                Actor(20, 0x3D1E, center + Vector3.UnitZ * 10), Actor(100, 0, center + Vector3.UnitX * 8)],
                full: true, party: party),
            Frame(1102, .1, [Actor(2, 0x233C, center, cast: Cast(31467, 6, center, startedAt: .1))], party: party),
        ]);
        controller.Seek(.15);
        const string turbinePrefix = "eureka/turbine/navigation/";
        Stable(twintania, turbinePrefix);
        var wind = ((IDictionary)Value(Encounter(twintania), "_actors"))[20ul]!;
        Changed(twintania, turbinePrefix, () => Set(wind, "Position", center + new Vector3(2, 0, 10)));
        Cleanup(twintania);
        Console.WriteLine("PASS geometry cache Turbine stable versions handles geometry wind position clear");

        const string wicked = "masterplayer.raid.wickedthunder.normal";
        center = new(100, 0, 100);
        load(wicked,
        [
            Header(1231),
            Frame(1231, 0, [Actor(1, 0x4263, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 8)], full: true, party: party),
            JsonSerializer.Serialize(new { t = .1, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = 100ul, TargetId = 100ul, MarkerId = 316u, Timestamp = Start.AddSeconds(.1) } }),
        ]);
        controller.Seek(.15);
        var cache = (IDictionary)Value(Output(wicked), "adaptedGeometry");
        Check(cache.Count == 1, "Buddy fixture did not exercise the sampler.");
        var buddyKey = cache.Keys.Cast<string>().Single();
        EmptyStable(wicked, buddyKey);
        var model = Value(Encounter(wicked), "WickedThunder");
        var buddy = Value(((IDictionary)Value(model, "Actors"))[101ul]!, "Actor");
        EmptyChanged(wicked, buddyKey, () => Set(buddy, "Position", center + Vector3.UnitX * 6));
        Cleanup(wicked);
        Console.WriteLine("PASS geometry cache Buddy empty contour cached moving party invalidation clear");
    }
}
