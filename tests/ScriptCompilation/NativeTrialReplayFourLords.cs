using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunFourLords(object managerObject, object runtimeObject, object controllerObject,
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
        object Value(object a, string name) => a.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(a)!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        bool ArenaAt(Vector3 p)
        {
            dynamic arena = Value(Encounter(), "Arena");
            return arena.Contains(p, 0f);
        }
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        const string byakko = "masterplayer.trial.byakko";
        load(byakko,
        [
            Header(746), Frame(746, 0, [Actor(1, 0x20F7, Vector3.Zero, combat: true),
                Actor(100, 0, Vector3.UnitX * 5), Actor(101, 0, -Vector3.UnitX * 5),
                Actor(10, 0x18D6, Vector3.Zero), Actor(11, 0x18D6, Vector3.Zero),
                Actor(20, 0x20F9, Vector3.Zero)], full: true, party: party),
            Frame(746, .1, [Actor(10, 0x18D6, Vector3.Zero, cast: Cast(10797, 4, Vector3.Zero, target: 101)),
                Actor(11, 0x18D6, Vector3.Zero, cast: Cast(10798, 4, Vector3.Zero))], party: party),
            Frame(746, 1, [Actor(10, 0x18D6, Vector3.Zero),
                Actor(101, 0, -Vector3.UnitX * 10), Actor(20, 0x20F9, Vector3.UnitZ * 5)], party: party),
            Frame(746, 2, [Actor(11, 0x18D6, Vector3.Zero), Actor(20, 0x20F9, Vector3.UnitZ * 5, dead: true)], party: party),
            Frame(746, 3, [Actor(1, 0x20F7, Vector3.Zero, dead: true)], party: party), Frame(746, 4, [], party: party),
        ]);
        controller.Seek(.5);
        Check(Group("byakko/spread").Any() && Group("byakko/orb").Length == 6 && dangers() > 0,
            "Byakko actual DLL lost spread or moving orb navigation.");
        controller.Seek(1.5);
        Check(!Group("byakko/spread").Any() && Group("byakko/orb").Length == 6 && previews().Length == 2,
            "Byakko independent cast finish crossed sweep/orb.");
        controller.Seek(2.5); Check(previews().Length == 0 && dangers() == 0, "Byakko actual end/death leaked navigation.");
        controller.Seek(3.5); Check(previews().Length == 0, "Byakko primary death leaked.");
        controller.Seek(.5); Check(Group("byakko/spread").Any(), "Byakko reverse Seek lost targets.");
        manager.SetScriptEnabled(byakko, false); Check(previews().Length == 0 && dangers() == 0, "Byakko disable leaked."); stop();

        const string suzaku = "masterplayer.trial.suzaku";
        var c = new Vector3(100, 0, 100);
        load(suzaku,
        [
            Header(810), Frame(810, 0, [Actor(1, 0x2459, c, combat: true), Actor(100, 0, c + Vector3.UnitX * 15),
                Actor(101, 0, c + Vector3.UnitZ * 10), Actor(10, 0x233C, c), Actor(11, 0x233C, c)], full: true, party: party),
            Frame(810, .1, [Actor(10, 0x233C, c, cast: Cast(12844, 7, c)),
                Actor(11, 0x233C, c, cast: Cast(12848, 5, c))], party: party),
            EffectEvent(1, 10, 12844, 1), Frame(810, 2, [Actor(10, 0x233C, c)], party: party),
            Frame(810, 3, [Actor(100, 0, c + Vector3.UnitX * 15, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(20))], party: party),
            Frame(810, 4, [Actor(11, 0x233C, c)], party: party),
            Frame(810, 5, [Actor(1, 0x2459, c, dead: true)], party: party), Frame(810, 6, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(ArenaAt(c) && Group("suzaku/knockback").Any() && dangers() == 0, "Suzaku premature ring or extra knockback AI.");
        controller.Seek(2.5);
        Check(!ArenaAt(c) && ArenaAt(c + Vector3.UnitX * 5) && Group("suzaku/knockback").Any(),
            "Suzaku actual finish did not create holed arena.");
        controller.Seek(3.5); Check(!Group("suzaku/knockback").Any(), "Suzaku raw immunity failed.");
        controller.Seek(5.5); Check(previews().Length == 0 && dangers() == 0, "Suzaku death leaked.");
        controller.Seek(1.5); Check(ArenaAt(c), "Suzaku reverse Seek retained phase2 hole.");
        manager.SetScriptEnabled(suzaku, false);
        Check(previews().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Suzaku disable leaked boundary.");
        stop();
        Console.WriteLine("PASS FourLords Byakko/Suzaku actual DLL replay spreads, moving capsules, independent cast ends, real arena transition, KB immunity, Seek, death and isolation");
    }
}
