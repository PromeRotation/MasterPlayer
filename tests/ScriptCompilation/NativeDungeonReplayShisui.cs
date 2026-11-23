using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunShisui(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.shisui";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object aoe) => (string)aoe.GetType().GetProperty("Id")!.GetValue(aoe)!;
        var center = new Vector3(0, 0, 69.221f);
        load(id,
        [
            Header(1367), Frame(1367, 0, [Actor(1, 0x1B0C, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x18D6, center), Actor(11, 0x1B0D, center)], full: true),
            ResearchIconEvent(.1, 100, 14),
            EffectEvent(1, 10, 8057, 1, target: 100),
            Frame(1367, 2, [Actor(20, 0x1E950D, center)]),
            Frame(1367, 3, [Actor(20, 0x1E950D, center, eventState: 7)]),
        ]);
        controller.Seek(.2); Check(dangers() == 1 && previews().Length == 1, "Production Shisui digest bait missing.");
        controller.Seek(1.1); Check(dangers() == 1 && previews().Length == 1 && Aoes().Any(a => AoeId(a).Contains("predicted")),
            "Production Shisui digest prediction missing.");
        controller.Seek(2.1); Check(dangers() == 1 && previews().Length == 1 && Aoes().All(a => !AoeId(a).Contains("predicted")),
            "Production Shisui pool handoff duplicated output.");
        controller.Seek(3.1); Check(dangers() == 0 && previews().Length == 0, "Production Shisui pool state removal failed.");
        controller.Seek(1.1); Check(dangers() == 1, "Production Shisui digest seek failed."); stop();

        center = new(-.046f, 0, -208.362f);
        var chest = center + Vector3.UnitX * 16;
        foreach (var open in new[] { false, true })
        {
            load(id,
            [
                Header(1367), Frame(1367, 0, [Actor(1, 0x1B0E, center, combat: true), Actor(100, 0, center),
                    Actor(10, 0x18D6, chest, nameId: 6274, eventAnimation: open ? 0x00040008u : null),
                    Actor(11, 0x18D6, center, nameId: 999)], full: true),
                Frame(1367, .1, [Actor(1, 0x1B0E, center, combat: true, cast: Cast(8058, 7, center))]),
                Frame(1367, 7.1, [Actor(1, 0x1B0E, center, combat: true)]),
                Frame(1367, 9, [Actor(1, 0x1B0E, center, combat: true, cast: Cast(8059, 5, center, startedAt: 9))]),
                Frame(1367, 14, [Actor(1, 0x1B0E, center, combat: true)]),
            ]);
            controller.Seek(.2);
            Check(previews().Length == 2 && dangers() == 2 && Aoes().Any(a => AoeId(a) == "shisui/chest/enter") == !open,
                "Production Shisui NameId or restored chest animation failed.");
            controller.Seek(7.2); Check(previews().Length == 2 && dangers() == 2, "Production Shisui natural Seduce finish lost kick.");
            controller.Seek(14.1); Check(previews().Length == 1 && dangers() == 1, "Production Shisui real kick finish leaked.");
            controller.Seek(.2); Check(previews().Length == 2, "Production Shisui chest seek failed."); stop();
        }
        List<string> records =
        [
            Header(1367), Frame(1367, 0, [Actor(1, 0x1B0E, center, combat: true),
                Actor(100, 0, center + Vector3.UnitX * 10), Actor(10, 0x18D6, center)], full: true),
            ResearchIconEvent(.05, 100, 1),
            Frame(1367, .1, [Actor(10, 0x18D6, center, cast: Cast(9431, 3.8, center))]),
        ];
        for (uint i = 0; i < 10; ++i)
        {
            records.Add(EffectEvent(3.9 + i * .8, 10, i == 0 ? 9431u : 8061u, i + 1, center, target: 10));
            if (i == 0) records.Add(Frame(1367, 3.91, [Actor(10, 0x18D6, center)]));
        }
        load(id, records);
        controller.Seek(.2); Check(dangers() == 1 && previews().Length == 2, "Production Shisui initial chase missing.");
        for (var i = 0; i < 10; ++i)
        {
            controller.Seek(4 + i * .8);
            Check(dangers() == (i == 9 ? 0 : 1) && previews().Length == (i == 9 ? 0 : 2),
                $"Production Shisui chase hit {i + 1} differs.");
        }
        controller.Seek(4); Check(dangers() == 1, "Production Shisui chase seek failed."); stop();

        center = new(0, 0, -433);
        records =
        [
            Header(1367), Frame(1367, 0, [Actor(1, 0x1B0F, center, combat: true), Actor(100, 0, center + Vector3.UnitX * 5),
                Actor(10, 0x18D6, center), Actor(11, 0x1B10, center - Vector3.UnitX * 10)], full: true),
            .. Enumerable.Range(1, 10).Select(i => EffectEvent(i * .1, 10, 8067, (uint)i)),
            Frame(1367, 2, [Actor(1, 0x1B0F, center, combat: true, cast: Cast(8066, 5, center, startedAt: 2)),
                Actor(11, 0x1B10, center - Vector3.UnitX * 10, cast: Cast(8069, 30, center, target: 100, startedAt: 2))]),
            EffectEvent(5, 1, 8065, 11),
            Frame(1367, 7, [Actor(1, 0x1B0F, center, combat: true)]),
            Frame(1367, 32, [Actor(11, 0x1B10, center - Vector3.UnitX * 10)]),
        ];
        load(id, records);
        controller.Seek(.95); Check(dangers() == 0 && previews().Length == 0, "Production Shisui early tide.");
        controller.Seek(1.1); Check(dangers() == 1 && previews().Length == 1, "Production Shisui tenth splash missing.");
        controller.Seek(6.8); Check(previews().Length == 2 && dangers() == 0, "Production Shisui gaze/own charge output differs.");
        var runningScript = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
        var scriptObject = runningScript.GetType().GetProperty("Script")!.GetValue(runningScript)!;
        var hints = scriptObject.GetType().GetProperty("Hints", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(scriptObject)!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!,
            "Production Shisui replay published PR facing.");
        controller.Seek(32.1); Check(dangers() == 0 && previews().Length == 0, "Production Shisui charge finish leaked.");
        controller.Seek(1.1); Check(dangers() == 1, "Production Shisui tide seek failed."); stop();
        Console.WriteLine("PASS native Shisui three bosses, NameId/chest animations, Seduce/kick, ten-hit chase, pool handoff, ten splashes, charge, gaze, seek and replay isolation");
    }
}
