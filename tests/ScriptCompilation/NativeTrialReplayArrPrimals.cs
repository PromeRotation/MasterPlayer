using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunArrPrimals(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object Value(object a, string name) => a.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(a)!;
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("arr-trial/" + key)).ToArray();
        ulong Priority() => (ulong)Encounter().GetType().GetMethod("PriorityTarget")!.Invoke(Encounter(), [1UL, Vector3.Zero])!;
        string[] Text() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [Vector3.Zero])!)
            .Cast<string>().Where(s => s.Contains("狂暴")).ToArray();
        bool SafeAt(Vector3 p)
        {
            dynamic geometry = Value(previews().Single(a => (bool)Value(a, "Safe")), "Geometry");
            return (bool)geometry.Contains(p, 0f);
        }
        void Isolated()
        {
            var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
            var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
                "ARR trial replay issued PR control.");
        }
        load("masterplayer.trial.ifrit-normal",
        [
            Header(1045),
            Frame(1045, 0, [Actor(1, 0xCF, Vector3.Zero, combat: true), Actor(100, 0, Vector3.UnitX * 15),
                Actor(10, 0xD0, Vector3.Zero, targetable: false), Actor(11, 0xFFFF, Vector3.Zero)], full: true),
            Frame(1045, 1, [Actor(10, 0xD0, Vector3.Zero)]),
            Frame(1045, 1.1, [Actor(11, 0xFFFF, Vector3.Zero, cast: Cast(733, 2, Vector3.Zero, startedAt: 1.1))]),
            CastEvent(1.1, 11, 733, 2, Vector3.Zero),
            Frame(1045, 3.2, [Actor(11, 0xFFFF, Vector3.Zero, cast: Cast(734, 2, Vector3.UnitZ * 5, startedAt: 3.2))]),
            CastEvent(3.2, 11, 734, 2, Vector3.UnitZ * 5),
            Frame(1045, 4, [Actor(11, 0xFFFF, Vector3.Zero)]),
            Frame(1045, 5, [Actor(10, 0xD0, Vector3.Zero, dead: true)]),
        ]);
        controller.Seek(.2); Check(!Text().Any() && dangers() == 0, "Ifrit untargetable nail started countdown.");
        controller.Seek(1.5); Check(Text().Single().Contains("34.5") && Priority() == 10 && dangers() == 1, "Ifrit countdown or Eruption missing."); Isolated();
        controller.Seek(3.5); Check(dangers() == 1 && previews().Length == 2, "Ifrit replacement duplicated old cast.");
        dynamic plume = Value(previews().Single(a => !(bool)Value(a, "OutlineOnly")), "Geometry");
        Check((bool)plume.Contains(Vector3.UnitZ * 12, 0f) && !(bool)plume.Contains(-Vector3.UnitZ * 4, 0f), "Ifrit cast replacement did not lock new location.");
        controller.Seek(4.2); Check(dangers() == 0 && previews().Length == 1, "Ifrit cancellation leaked cast.");
        controller.Seek(5.2); Check(!Text().Any(), "Ifrit dead nail retained countdown.");
        controller.Seek(1.5); Check(dangers() == 1 && Text().Single().Contains("34.5"), "Ifrit Seek did not restore clock.");
        manager.SetScriptEnabled("masterplayer.trial.ifrit-normal", false);
        Check(dangers() == 0 && previews().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Ifrit disable retained output."); stop();

        load("masterplayer.trial.titan-normal",
        [
            Header(1046), Frame(1046, 0, [Actor(1, 0xF6, Vector3.Zero, combat: true),
                Actor(100, 0, Vector3.UnitX * 15), Actor(10, 0x5E3, Vector3.Zero)], full: true),
            Frame(1046, 1, [Actor(11, 0x5A4, Vector3.Zero)]),
            CastEvent(1.1, 12, 650, 2, Vector3.Zero, MathF.PI / 2),
            Frame(1046, 1.2, [Actor(12, 0xFFFF, Vector3.Zero, cast: Cast(650, 2, Vector3.Zero, MathF.PI / 2, startedAt: 1.1))]),
            Frame(1046, 3.2, [Actor(12, 0xFFFF, Vector3.Zero)]),
            Frame(1046, 4, [Actor(11, 0x5A4, Vector3.Zero, dead: true), Actor(10, 0x5E3, Vector3.Zero, dead: true)]),
        ]);
        controller.Seek(.5); Check(Text().Single().Contains("61.5") && Priority() == 10, "Titan heart clock missing.");
        controller.Seek(1.5); Check(Priority() == 11 && dangers() == 1 && previews().Length == 1, "Titan gaol priority or Landslide missing."); Isolated();
        controller.Seek(3.5); Check(dangers() == 0 && previews().Length == 0, "Titan Landslide end leaked.");
        controller.Seek(4.2); Check(Priority() == 1 && !Text().Any(), "Titan dead gaol or heart retained output.");
        controller.Seek(1.5); Check(Priority() == 11 && dangers() == 1, "Titan Seek failed."); stop();

        var origin = new Vector3(0, 0, -15);
        load("masterplayer.trial.garuda-normal",
        [
            Header(1047),
            Frame(1047, 0, [Actor(1, 0xEF, origin, combat: true), Actor(100, 0, new(0, 0, 5)),
                Actor(10, 0xED, Vector3.Zero, hitboxRadius: 2, dead: true, targetable: false),
                Actor(11, 0x622, Vector3.Zero), Actor(12, 0xEE, Vector3.Zero)], full: true),
            Frame(1047, .1, [Actor(1, 0xEF, origin, combat: true, cast: Cast(667, 4, origin))]),
            CastEvent(.1, 1, 667, 4, origin),
            Frame(1047, 1, [Actor(10, 0xED, Vector3.UnitX * 15, hitboxRadius: 10)]),
            Frame(1047, 4.2, [Actor(1, 0xEF, origin, combat: true)]),
            Frame(1047, 5, [Actor(1, 0xEF, origin, combat: true, cast: Cast(662, 4, origin, startedAt: 5))]),
            CastEvent(5, 1, 662, 4, origin),
            EffectEvent(9, 1, 662, 1),
            Frame(1047, 9.1, [Actor(1, 0xEF, origin, combat: true)]),
            Frame(1047, 10, [], removed: [11]),
            Frame(1047, 11, [], removed: [10]),
        ]);
        controller.Seek(.01); Check(dangers() == 1 && previews().Length == 1 && Priority() == 12, "Garuda Friction or feather priority missing.");
        controller.Seek(.5); Check(SafeAt(new(0, 0, 5)) && !SafeAt(Vector3.Zero) && !SafeAt(new(5, 0, 5))
            && dangers() == 1 && previews().Length == 2, "Garuda LOS shadow or hitbox differs."); Isolated();
        controller.Seek(1.5); Check(SafeAt(new(0, 0, 5)), "Garuda LOS moved with later pillar snapshot.");
        controller.Seek(4.5); Check(Group("los").Length == 0 && dangers() == 1 && previews().Length == 1, "Garuda real LOS finish failed.");
        controller.Seek(5.5); Check(Group("eye").Length == 0 && dangers() == 0, "Garuda eye activated before actual AerialBlast effect.");
        controller.Seek(9.2); Check(Group("eye").Length == 1 && dangers() == 2 && previews().Length == 2, "Garuda eye/Friction continuous combination missing.");
        controller.Seek(10.2); Check(Group("eye").Length == 0 && dangers() == 1, "Garuda helper removal retained eye.");
        controller.Seek(11.2); Check(dangers() == 0 && previews().Length == 0, "Garuda pillar removal retained Friction.");
        controller.Seek(.5); Check(SafeAt(new(0, 0, 5)) && Group("eye").Length == 0, "Garuda backward Seek retained future eye or pillar.");
        stop();
        Console.WriteLine("PASS ARR native trials actual DLL replay Ifrit nail/Eruption/Plume, Titan heart/gaol/Landslide, Garuda LOS/Friction/Eye, real endings, disable, Seek and PR isolation");
    }
}
