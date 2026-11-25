using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunPalaceSerpents(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Encounter()
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(r => Value(r, "Script").GetType().BaseType?.Name == "DungeonScript"), "Script");
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object[] Ambient() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        object[] Puddles() => Ambient().Where(a => ((string)Value(a, "Id")).StartsWith("trial/voidzone/")).ToArray();
        string[] Hints() => ((IEnumerable<string>)Encounter().GetType().GetMethod("Hints")!
            .Invoke(Encounter(), [new Vector3(-287, 0, -234)])!).ToArray();
        foreach (var (territory, oid, fire, ice, fear, id) in new[]
        {
            (563u, 0x16ACu, 6427u, 6428u, 6429u, "masterplayer.palace.ningishzida"),
            (600u, 0x181Au, 7139u, 7140u, 7141u, "masterplayer.palace.alfard"),
        })
        {
            var center = new Vector3(-300, 0, -236);
            var target = center + Vector3.UnitX * 12;
            var moved = center + new Vector3(13, 0, 2);
            object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
            var records = new List<string>
            {
                Header(territory),
                Frame(territory, 0, [Actor(1, oid, center, combat: true, targetId: 100),
                    Actor(100, 0, target), Actor(101, 0, center + Vector3.UnitZ * 5)], full: true, party: party),
                Frame(territory, 1, [Actor(1, oid, center, combat: true, targetId: 100,
                    cast: Cast(fire, 3, center, target: 100, startedAt: 1))], party: party),
                CastEvent(1, 1, fire, 3, center, target: 100),
                Frame(territory, 2, [Actor(100, 0, moved)], party: party),
                Frame(territory, 3, [Actor(1, oid, center, combat: true, targetId: 100)], party: party),
            };
            for (uint i = 0; i < 4; ++i)
            {
                var action = i < 2 ? fire : ice;
                records.Add(EffectEvent(4 + i, 1, action, i + 1, center, target: 100));
                records.Add(EffectEvent(4 + i, 1, action, i + 1, center, target: 100));
            }
            records.AddRange([
                Frame(territory, 8, [Actor(2, 0x1E8D9B, moved, targetable: false)], party: party),
                Frame(territory, 9, [], party: party),
                Frame(territory, 10, [Actor(2, 0x1E8D9B, moved, eventState: 7, targetable: false)], party: party),
                EffectEvent(31, 1, fear, 5),
                Frame(territory, 32, [Actor(1, oid, center, combat: true, targetId: 100,
                    cast: Cast(fear, territory == 563 ? 6 : 2, center, startedAt: 32))], party: party),
                CastEvent(32, 1, fear, territory == 563 ? 6 : 2, center),
                Frame(territory, 61, [Actor(1, oid, center, dead: true)], party: party),
                Frame(territory, 62, [], party: party),
            ]);
            load(id, records);
            controller.Seek(.5);
            Check(dangers() == 2 && Ambient().Any(a => ((string)Value(a, "Id")).EndsWith("/avoid/101")),
                $"Palace serpent {territory} bait and target-specific cleave navigation.");
            Check(Ambient().Single(a => ((string)Value(a, "Id")).EndsWith("/display")) is { } cleave
                && (bool)Value(cleave, "OutlineOnly") && !(bool)Value(cleave, "Navigate"), "Cleave outline polluted navigation.");
            var previewBaseline = previews().Length;
            controller.Seek(2.5);
            var castPuddle = Puddles().Single();
            Check((Vector3)Value(castPuddle, "Position") == moved
                && (DateTime)Value(castPuddle, "Activation") == Start.AddSeconds(4.3)
                && dangers() == 2 && previews().Length == previewBaseline,
                $"Palace serpent {territory} dynamic cast target {Value(castPuddle, "Position")}, finish {Value(castPuddle, "Activation"):O}, dangers {dangers()}, previews {previews().Length}/{previewBaseline}.");
            controller.Seek(3.5);
            Check(Puddles().Length == 0 && dangers() == 2, "Ended cast retained predicted puddle.");
            controller.Seek(7.5);
            Check(Puddles().Length == 4 && Hints().Any(h => h.StartsWith("Run to the middle")),
                "Fourth effect, event deduplication or phase hint.");
            Check(Puddles().All(a => (Vector3)Value(a, "Position") == moved)
                && Math.Abs(((DateTime)Value(Puddles()[0], "Activation") - Start.AddSeconds(6.1)).TotalSeconds) < .000001,
                "Event predictions lost fixed target or +2.1 clock.");
            Check(dangers() == 6 && previews().Length == previewBaseline + 4,
                "Event puddles were not published to both drawing and navigation.");
            controller.Seek(10.5);
            Check(Puddles().Length == 2 && Puddles().All(a => ((string)Value(a, "Id")).Contains("/event/")),
                "Typed live handoff or EventState7 filtering.");
            controller.Seek(30);
            Check(Puddles().Length == 2, "Persistent ice event predictions acquired a timeout.");
            controller.Seek(31.5);
            Check(Hints().Any(h => h.Contains("x2 Fire")), "Fifth event did not restart bait cycle.");
            controller.Seek(60);
            Check(dangers() == 4 && Puddles().Length == 2
                && !Ambient().Any(a => ((string)Value(a, "Id")).Contains("/cleave/")),
                "Observed Fear cast expired by clock or failed to hide cleave.");
            controller.Seek(61.1);
            Check(dangers() == 0 && previews().Length == 0, "Serpent death leaked output.");
            controller.Seek(7.5);
            Check(Puddles().Length == 4 && dangers() == 6, "Serpent reverse Seek did not reconstruct effects.");
            stop();
        }
        Console.WriteLine("PASS production Palace serpents: two exact arenas, dynamic cleave, four-to-five event cycle, cast/event/live puddles, typed handoff, both drawing and navigation, observed endings, Seek and cleanup");
    }
}
