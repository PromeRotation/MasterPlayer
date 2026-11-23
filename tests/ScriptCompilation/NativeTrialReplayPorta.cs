using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunPorta(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.trial.porta-decumana";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object Value(object a, string name) => a.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(a)!;
        bool Flag(string name) => (bool)Value(Encounter(), name);
        ulong[] Forbidden() => (ulong[])Value(Encounter(), "ForbiddenTargets");
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("porta/" + prefix)).ToArray();
        void Isolated()
        {
            var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
            var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
                "Porta replay issued PR facing/action execution.");
        }
        var p1 = new Vector3(-772, 0, -600);
        var p2 = new Vector3(-704, 0, 480);
        var party = Enumerable.Range(0, 4).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        load(id,
        [
            Header(1048),
            Frame(1048, 0, [Actor(1, 0x38FB, p1, combat: true, stackedStatus: 325), Actor(100, 0, p1 + Vector3.UnitX * 10),
                Actor(101, 0, p1), Actor(102, 0, p1), Actor(103, 0, p1), Actor(10, 0x38FF, p1),
                Actor(11, 0x233C, p1), Actor(12, 0x233C, p1)], full: true, party: party),
            Frame(1048, .1, [Actor(10, 0x38FF, p1, cast: Cast(29003, 4, p1)),
                Actor(11, 0x233C, p1, cast: Cast(28980, 3, p1)), Actor(12, 0x233C, p1, cast: Cast(29001, 3, p1 + Vector3.UnitX * 5))], party: party),
            CastEvent(.1, 10, 29003, 4, p1), CastEvent(.1, 11, 28980, 3, p1), CastEvent(.1, 12, 29001, 3, p1 + Vector3.UnitX * 5),
            StatusEvent(1, 1, 325, 0, false), StatusEvent(1.1, 1, 3012, 0, true),
            Frame(1048, 1.2, [Actor(1, 0x38FB, p1, combat: true, stackedStatus: 3012)], party: party),
            StatusEvent(2, 1, 3012, 0, false),
            Frame(1048, 2.1, [Actor(1, 0x38FB, p1, combat: true)], party: party),
            Frame(1048, 3.2, [Actor(11, 0x233C, p1), Actor(12, 0x233C, p1)], party: party),
            Frame(1048, 4.2, [Actor(10, 0x38FF, p1)], party: party),
            Frame(1048, 5, [Actor(1, 0x38FB, p1, dead: true), Actor(2, 0x3900, p2, combat: true),
                Actor(100, 0, p2), Actor(101, 0, p2), Actor(102, 0, p2), Actor(103, 0, p2)], party: party),
        ]);
        controller.Seek(.5);
        Check(Forbidden().SequenceEqual([1UL]) && !Flag("RequireAllStop") && dangers() == 3 && previews().Length == 4,
            "Porta first phase shield/center constraint/parallel AOE missing."); Isolated();
        controller.Seek(1.5); Check(Forbidden().SequenceEqual([1UL]), "Porta VortexBarrier event not routed.");
        controller.Seek(2.05); Check(Forbidden().Length == 0, "Porta barrier removal did not override the preceding snapshot.");
        controller.Seek(2.7); Check(Forbidden().Length == 0, "Porta updated snapshot resurrected a removed barrier.");
        controller.Seek(3.5); Check(dangers() == 1 && Group("vulcan").Length == 1 && previews().Length == 2, "Porta puddle/donut endings removed knockback.");
        controller.Seek(4.5); Check(dangers() == 0 && previews().Length == 0, "Porta first phase ended cast leaked.");
        controller.Seek(5.1);
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check((bool)boundary.Contains(p2, 0f) && !(bool)boundary.Contains(p1, 0f) && Forbidden().Length == 0, "Porta phase transition retained old arena or shield.");
        controller.Seek(.5); Check(Forbidden().SequenceEqual([1UL]) && dangers() == 3, "Porta backward phase Seek lost first-phase state."); stop();

        load(id,
        [
            Header(1048),
            Frame(1048, 0, [Actor(1, 0x3900, p2, combat: true), Actor(100, 0, p2),
                Actor(101, 0, p2 + Vector3.UnitX * 3), Actor(102, 0, p2 + Vector3.UnitX * 2), Actor(103, 0, p2 + Vector3.UnitX * 1),
                Actor(10, 0x233C, p2), Actor(11, 0x3901, p2), Actor(12, 0x233C, p2), Actor(13, 0x233C, p2),
                Actor(20, 0x3902, p2 + Vector3.UnitZ * 10)], full: true, party: party),
            Frame(1048, .1, [Actor(10, 0x233C, p2, cast: Cast(29021, 7, p2)),
                Actor(11, 0x3901, p2, cast: Cast(29019, 4, p2)),
                Actor(12, 0x233C, p2, cast: Cast(29014, 5, p2, target: 100)),
                Actor(13, 0x233C, p2, cast: Cast(29012, 5, p2, target: 101))], party: party),
            Frame(1048, 1, [Actor(100, 0, p2 + Vector3.UnitZ * 5), Actor(101, 0, p2 + Vector3.UnitX * 6)], party: party),
            Frame(1048, 2, [Actor(1, 0x3900, p2, combat: true, cast: Cast(29015, 4, p2, startedAt: 2)),
                Actor(20, 0x3902, p2 + Vector3.UnitZ * 10, rotation: MathF.PI / 2)], party: party),
            Frame(1048, 4.2, [Actor(11, 0x3901, p2)], party: party),
            Frame(1048, 5.2, [Actor(12, 0x233C, p2), Actor(13, 0x233C, p2)], party: party),
            Frame(1048, 6.2, [Actor(1, 0x3900, p2, combat: true)], removed: [20], party: party),
            Frame(1048, 7.2, [Actor(10, 0x233C, p2)], party: party),
            Frame(1048, 8, [Actor(1, 0x3900, p2, combat: true, cast: Cast(29024, 71, p2, startedAt: 8))], party: party),
            Frame(1048, 9, [], party: party),
        ]);
        controller.Seek(.5);
        Check(previews().Length == 5 && Group("explosion").Single() is { } blast && !(bool)Value(blast, "Navigate")
            && !Flag("WantsPortaSprint"), "Porta cannon/Explosion visual or independent party mechanics differ.");
        controller.Seek(1.5);
        Check((Vector3)Value(Group("stack").Single(a => (bool)Value(a, "Display")), "Position") == p2 + Vector3.UnitZ * 5
            && (Vector3)Value(Group("spread").Single(a => (bool)Value(a, "Display")), "Position") == p2 + Vector3.UnitX * 6,
            "Porta stack/spread did not follow targets.");
        controller.Seek(2.5);
        Check(Flag("WantsPortaSprint") && Flag("WantsPortaKnockbackImmunity") && Group("orb/join").Length == 1
            && ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Any(), "Porta orb goal/Sprint/immunity requests missing."); Isolated();
        controller.Seek(4.5); Check((bool)Value(Group("explosion").Single(), "Navigate") && Group("stack").Length > 0, "Porta cannon finish failed to restore explosion.");
        controller.Seek(5.5); Check(Group("stack").Length == 0 && Group("spread").Length == 0, "Porta target cast endings leaked.");
        controller.Seek(6.5); Check(!Flag("WantsPortaSprint") && !Flag("WantsPortaKnockbackImmunity")
            && Group("orb").Length == 0 && Group("knockback").Length == 0, "Porta source removal or AethericBoom end leaked control.");
        controller.Seek(7.5); Check(dangers() == 0 && previews().Length == 0, "Porta explosion finish leaked.");
        controller.Seek(8.2);
        var enrage = Value(Encounter(), "PortaCountdown").ToString()!;
        Check(enrage.Contains("71.1"), $"Porta enrage hint differs: {enrage}, replay clock {controller.Clock}.");
        controller.Seek(.5); Check(!Flag("WantsPortaSprint") && !Flag("WantsPortaKnockbackImmunity") && Group("stack").Length > 0, "Porta Seek retained future orb/control state.");
        manager.SetScriptEnabled(id, false);
        Check(dangers() == 0 && previews().Length == 0 && !((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Any(), "Porta disable retained output.");
        stop();
        Console.WriteLine("PASS Porta native two phases actual DLL replay barriers, parallel primals, knockback, phase switch, cannon/explosion order, moving party targets, orb goals, action isolation, enrage and Seek");
    }
}
