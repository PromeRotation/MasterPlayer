using System.Collections;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntNechuciho(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        object[] Whispers() => previews().Where(p => ((string)Value(p, "Id")).Contains("/nechuciho/")).ToArray();
        DateTime[] Deadlines()
        {
            dynamic snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            return ((IEnumerable)snapshot.DangerAreas).Cast<object>()
                .SelectMany(a => ((IEnumerable)Value(a, "DangerWindows")).Cast<object>())
                .Select(w => (DateTime)Value(w, "DangerFrom")).Order().ToArray();
        }
        const uint territory = 1187;
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        foreach (var action in new uint[] { 39493, 39494, 39495 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x452C, default, combat: true), Actor(999, 0x233C, default),
                    Actor(100, 0, default)], full: true, party: party),
                Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(action, 3, default))], party: party),
                Frame(territory, 4, [Actor(999, 0x233C, default, cast: Cast(39496, 7, default, startedAt: 4))], party: party),
                EffectEvent(5, 1, 39872, 1),
                EffectEvent(6, 999, 39785, 2), EffectEvent(6.01, 999, 39785, 2),
                EffectEvent(7, 1, 42164, 3), EffectEvent(8, 999, 42171, 4), EffectEvent(9, 1, 39788, 5),
                Frame(territory, 10, [], removed: [1], party: party),
            ]);
            controller.Seek(1d);
            var finish = Start.AddSeconds(.1 + 3 + .3);
            Check(Whispers().Length == 2 && dangers() == 2 && Deadlines().SequenceEqual(
                new[] { finish.AddSeconds(9.3f), finish.AddSeconds(9.3f + 2f) }),
                "Nechuciho lost first two whispers or exact float deadlines.");
            controller.Seek(5.5);
            Check(Whispers().Length == 2 && dangers() == 2, "Visual Start/Finish or ordinary effect changed Whisper history.");
            controller.Seek(6.5);
            Check(Whispers().Length == 2 && dangers() == (action == 39494 ? 1 : 2),
                "Whisper helper effect/deduplication or opposite preview risk differs.");
            controller.Seek(7.5);
            Check(Whispers().Length == 2 && dangers() == (action == 39493 ? 1 : 2),
                "Whisper second effect failed to promote the hidden pair.");
            controller.Seek(8.5);
            Check(Whispers().Length == 1 && dangers() == 1 && Deadlines().Single() == finish.AddSeconds(9.3f + 6f),
                "Whisper tail failed risk promotion or its original activation was retimed.");
            controller.Seek(9.5);
            Check(Whispers().Length == 0 && dangers() == 0, "Whisper final effect retained output.");
            controller.Seek(1d);
            Check(Whispers().Length == 2 && dangers() == 2, "Whisper reverse Seek retained future consumption.");
            controller.Seek(10.1);
            Check(previews().Length == 0 && dangers() == 0, "Nechuciho final owner removal retained helper output.");
            stop();
        }
        foreach (var action in new uint[] { 39872, 39491, 39492 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x452C, default, combat: true), Actor(999, 0x233C, default),
                    Actor(100, 0, default)], full: true, party: party),
                Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(action, 5, default))], party: party),
                Frame(territory, 2, [], removed: [1], party: party),
                Frame(territory, 3, [], party: party),
            ]);
            controller.Seek(1d);
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            Check(dangers() == (action == 39872 ? 1 : 0), "Nechuciho ordinary cast classification differs.");
            if (action == 39491) Check((string?)Value(script, "PredictedDamageText") == "Raidwide：1人/1次",
                "SentinelRoar lost its raidwide prediction.");
            if (action == 39492) Check(((string?)Value(script, "MechanicHintsText"))?.Contains("Interrupt! (Applies Doom!)") == true
                && Value(script, "PredictedDamageText") == null, "DeathSentence lost its interrupt hint or invented damage.");
            controller.Seek(2.1);
            Check(previews().Length == 0 && dangers() == 0 && Value(script, "PredictedDamageText") == null,
                "Nechuciho owner removal retained ordinary helper output.");
            stop();
        }
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x452C, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, default)], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(39493, 3, default))], party: party),
            Frame(territory, 1, [Actor(2, 0x452C, default, combat: true), Actor(999, 0x233C, default)], party: party),
            Frame(territory, 2, [], removed: [1], party: party),
            Frame(territory, 3, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(Whispers().Length == 2 && dangers() == 2, "New Nechuciho owner replayed a helper cast finished in the same frame.");
        controller.Seek(2.1);
        Check(Whispers().Length == 0 && dangers() == 0, "New owner retained invented completed-helper history.");
        stop();
        Console.WriteLine("PASS Nechuciho actual DLL three Whispers, first-two views, opposite risk promotion, float deadlines, effect-only FIFO, helper hints and raidwide, same-frame owner activation, reverse Seek and cleanup");
    }
}
