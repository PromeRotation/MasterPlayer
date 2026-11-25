using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunFates(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string guid = "masterplayer.fate.archaeotania";
        var center = new Vector3(279, 390, 249);
        object Property(object obj, string name) => obj.GetType().GetProperty(name, hidden)!.GetValue(obj)!;
        object Script() => ((IEnumerable)manager.Running.Values).Cast<object>().Select(s => Property(s, "Script"))
            .Single(s => s.GetType().Name == "Archaeotania");
        object Encounter()
        {
            var script = Script();
            return script.GetType().BaseType!.GetField("_encounter", hidden)!.GetValue(script)!;
        }
        bool Active() => (bool)Property(Script(), "EncounterActive");
        int Obstacles() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
        object Boss(bool combat = true, object? cast = null, uint hp = 1000, bool targetable = true)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x27AD, center, combat: combat, cast: cast,
                targetable: targetable, objectIndex: 2, isEnemy: true))!;
            node["Hp"] = hp;
            node["AggroPlayer"] = combat;
            node["FateId"] = 1432;
            return node;
        }
        void Isolated()
        {
            var hints = Property(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", hidden)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", hidden)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", hidden)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Property(Property(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Fate replay published facing, forbidden attacks or movement hold.");
        }
        load(guid,
        [
            Header(818), Frame(818, 0, [Boss(), Actor(100, 0, center + Vector3.UnitX * 30)], full: true),
            Frame(818, 1, [Actor(100, 0, center)]),
            Frame(818, 2, [Boss(false, targetable: false)]),
            Frame(818, 3, [Boss(hp: 0)]), Frame(818, 4, [Boss()]),
            Frame(818, 5, [], removed: [1]), Frame(818, 6, [Boss()]), Frame(818, 7, []),
        ]);
        controller.Seek(.1);
        Check(!Active() && dangers() == 0 && previews().Length == 2
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Fate strict 30y pull or precombat display/navigation differs.");
        controller.Seek(1.2);
        object boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(Active() && (Vector3)Property(boundary, "Position") == center && previews().Length == 1,
            "Fate native square or primary marker missing.");
        controller.Seek(2.2);
        Check(Active() && previews().Length == 0, "Fate deaggro latch or targetability marker filtering differs.");
        controller.Seek(4.2);
        Check(!Active() && previews().Length == 0, "Fate completed old generation revived.");
        controller.Seek(6.2);
        Check(Active(), "Fate actual new generation did not start.");
        manager.SetMechanismEnabled(guid, "Archaeotania", false);
        controller.Seek(6.3);
        Check(!Active() && previews().Length == 0 && dangers() == 0
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Fate disabled retained owner output.");
        manager.SetMechanismEnabled(guid, "Archaeotania", true);
        controller.Seek(1.2);
        Check(Active(), "Fate reverse Seek/re-enable did not rebuild.");
        Isolated();
        stop();
        Console.WriteLine("PASS native fate Archaeotania production territory818 exact square strict pull source precombat display latch HP0 generations mechanism off reverse Seek isolation owner cleanup");

        uint[] actions = [16445, 16448, 16447, 16454, 17089, 16441, 16442, 16455, 16456, 17042];
        var records = new List<string>
        {
            Header(818), Frame(818, 0, [Boss(), Actor(100, 0, center),
                .. actions.Select((_, i) => Actor((ulong)(10 + i), 0x233C, center + Vector3.UnitX * 10))], full: true),
            Frame(818, .1, actions.Select((action, i) => Actor((ulong)(10 + i), 0x233C, center + Vector3.UnitX * 10,
                cast: Cast(action, 3, center, .25f)))),
            Frame(818, 4, []),
            Frame(818, 5, actions.Select((_, i) => Actor((ulong)(10 + i), 0x233C, center))),
            Frame(818, 6, []),
        };
        load(guid, records);
        controller.Seek(4.2);
        Check(dangers() == 10 && previews().Length == 11, "Fate ordinary casts expired before actual finish or lost source geometry.");
        controller.Seek(5.2);
        Check(dangers() == 0 && previews().Length == 1, "Fate ordinary actual finish retained AOE.");
        controller.Seek(.5);
        Check(dangers() == 10 && previews().Length == 11, "Fate ordinary reverse Seek lost casts.");
        Isolated();
        stop();
        Console.WriteLine("PASS native fate Archaeotania production ten ordinary helper geometries locked origins independent cast clocks actual finish dual views navigation reverse Seek owner cleanup");

        object Wind(Vector3 position, bool dead = false) => Actor(10, 0x27B2, position, dead: dead, targetable: false, eventState: 7);
        load(guid,
        [
            Header(818), Frame(818, 0, [Boss(), Actor(100, 0, center), Wind(center)], full: true),
            Frame(818, 1, [Wind(center + Vector3.UnitX * 10)]),
            Frame(818, 2, [Wind(center, true)]), Frame(818, 3, [Wind(center)]), Frame(818, 4, []),
        ]);
        controller.Seek(.2);
        Check(Obstacles() == 1 && dangers() == 0 && previews().Length == 2, "Fate twister not published as hard obstacle and display.");
        controller.Seek(1.2);
        Check(Obstacles() == 1, "Fate moving twister lost hard obstacle.");
        controller.Seek(2.2);
        Check(Obstacles() == 0 && previews().Length == 1, "Dead twister retained display or hard obstacle.");
        controller.Seek(3.2);
        Check(Obstacles() == 1 && previews().Length == 2, "Twister revival stayed hidden.");
        stop();
        Check(Obstacles() == 0, "Fate owner cleanup retained twister obstacle.");
        Console.WriteLine("PASS native fate Archaeotania production twister dynamic R5 hard obstacle state7 untargetable dead-only filtering revival dual views owner cleanup");

        var party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        var members = Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0,
            center + new Vector3(i == 0 ? 10 : 0, 0, 0))).ToArray();
        object Helper(bool dead = false) => Actor(10, 0x2AA0, center, targetId: 101, dead: dead);
        load(guid,
        [
            Header(818), Frame(818, 0, [Boss(cast: Cast(16449, 10, center, startedAt: 0)), .. members], full: true, party: party),
            JsonSerializer.Serialize(new { t = .2, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = 101ul, TargetId = 100ul, MarkerId = 62u, Timestamp = Start.AddSeconds(.2) } }),
            Frame(818, 1, [Helper()], party: party),
            Frame(818, 2, [Boss()], party: party),
            Frame(818, 3, [Actor(101, 0, center, dead: true)], party: party),
            Frame(818, 4, [Actor(101, 0, center)], party: party),
            TreasureNativeEffect(5, 10, 17483, 1, 100, [101]),
            EffectEvent(5, 10, 17483, 1, target: 101),
            TreasureNativeEffect(6, 10, 17483, 2, 101, [100]),
            Frame(818, 7, [], party: party),
        ]);
        controller.Seek(.5);
        Check(dangers() == 1 && previews().Length == 2, "Fate source-icon stack did not publish R6 join/navigation.");
        controller.Seek(2.2);
        Check(dangers() == 1 && previews().Length == 2, "Living helper fallback did not retain stack after primary stopped.");
        controller.Seek(3.2);
        Check(dangers() == 0 && previews().Length == 1, "Dead stack target retained output.");
        controller.Seek(4.2);
        Check(dangers() == 1 && previews().Length == 2, "Stack target revival lost the original mark.");
        controller.Seek(5.2);
        Check(dangers() == 1, "Native main target was confused with a secondary hit or expanded PR target.");
        controller.Seek(6.2);
        Check(dangers() == 0 && previews().Length == 1, "Actual main target effect did not clear stack.");
        controller.Seek(.5);
        Check(dangers() == 1, "Fate stack reverse Seek failed.");
        Isolated();
        stop();
        Console.WriteLine("PASS native fate Archaeotania production Source icon R6 min8 stack helper fallback dead target revival main-target actual effect dual views join navigation reverse Seek isolation");

        object Wave(ulong id, float rotation, bool casting = true) => Actor(id, 0x2AB1, center + Vector3.UnitX * 5,
            cast: casting ? Cast(16452, 8.5, center, rotation) : null);
        load(guid,
        [
            Header(818), Frame(818, 0, [Boss(), Actor(100, 0, center), Wave(10, 0, false), Wave(11, 0, false)], full: true),
            Frame(818, .1, [Wave(10, 0), Wave(11, MathF.PI / 2)]),
            Frame(818, 1, [Actor(100, 0, center, stackedStatus: 1209, statusExpiresAt: Start.AddSeconds(8.9))]),
            Frame(818, 2, [Actor(100, 0, center, stackedStatus: 1209, statusExpiresAt: Start.AddSeconds(9))]),
            Frame(818, 3, [Actor(100, 0, center)]),
            Frame(818, 5, [Wave(10, 0, false)]), Frame(818, 10, [Wave(11, 0, false)]), Frame(818, 11, []),
        ]);
        controller.Seek(.5);
        var both = previews().Length;
        var waves = ((IEnumerable)Encounter().GetType().GetField("fateKnockbacks", hidden)!.GetValue(Encounter())!).Cast<object>().ToArray();
        Check(waves.Length == 2 && waves.All(w => (float)Property(w, "Distance") == 48
            && !(bool)Property(w, "StopAtWall") && !(bool)Property(w, "IgnoreImmunes")),
            "Production TidalWave lost the explicit displacement model.");
        Check(both > 1 && dangers() == 0 && Obstacles() == 0, "Fate TidalWave projected movement became navigation.");
        controller.Seek(1.2);
        Check(previews().Length == both, "TidalWave accepted immunity expiring exactly at activation.");
        controller.Seek(2.2);
        Check(previews().Length == 1 && dangers() == 0, "Strictly longer immunity failed to suppress projection.");
        controller.Seek(5.2);
        Check(previews().Length is > 1 && previews().Length < both && dangers() == 0, "First source finish cleared all waves.");
        controller.Seek(10.2);
        Check(previews().Length == 1 && dangers() == 0, "TidalWave actual finish retained projection.");
        controller.Seek(.5);
        Check(previews().Length == both && dangers() == 0, "TidalWave reverse Seek failed.");
        Isolated();
        stop();
        Console.WriteLine("PASS native fate Archaeotania production ordered forward48 knockback projections independent actual finish strict immunity boundary-only warning no navigation dual views reverse Seek replay isolation");

        load(guid,
        [
            Header(818), Frame(818, 0, [Boss(hp: 0), Actor(100, 0, center, dead: true)], full: true),
            Frame(818, 1, [Boss(), Actor(100, 0, center)]), Frame(818, 2, []),
        ]);
        controller.Seek(1.2);
        Check(!Active() && previews().Length == 0 && dangers() == 0,
            "Production initial dead viewer lost completed primary generation.");
        stop();
        Console.WriteLine("PASS native fate Archaeotania production dead initial perspective HP0 barrier prevents same-generation revival");

        load(guid,
        [
            Header(818), Frame(818, 0, [Boss(), Actor(100, 0, center),
                Actor(10, 0x2995, center, dead: true, cast: Cast(16445, 3, center, .1f, startedAt: 0))], full: true),
            Frame(818, 1, [Actor(10, 0x2995, center + Vector3.UnitX * 20, dead: true,
                cast: Cast(16445, 3, center + Vector3.UnitX * 7, .6f, startedAt: 0))]),
            Frame(818, 2, []),
        ]);
        controller.Seek(1.2);
        var pending = ((IDictionary)Property(Encounter(), "Casts"))[10ul]!;
        Check(dangers() == 1 && (Vector3)Property(pending, "Location") == center + Vector3.UnitX * 7
            && (float)Property(pending, "Rotation") == .6f, "Production dead caster or raw pose correction differs.");
        stop();
        Console.WriteLine("PASS native fate Archaeotania production dead caster activation replay raw CastInfo location rotation relock");
    }
}
