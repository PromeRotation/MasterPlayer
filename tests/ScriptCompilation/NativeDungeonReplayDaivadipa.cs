using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDaivadipa(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string guid = "masterplayer.fate.daivadipa";
        var center = new Vector3(-608, 100, 811);
        object Property(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => ((IEnumerable)manager.Running.Values).Cast<object>().Select(s => Property(s, "Script"))
            .Single(s => s.GetType().Name == "Daivadipa");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Ambient(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Property(a, "Id")).StartsWith("fate/" + prefix + "/")).ToArray();
        bool Active() => (bool)Property(Script(), "EncounterActive");
        object Boss(object? cast = null, bool combat = true, uint hp = 1000, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x356D, center, combat: combat, cast: cast, targetId: target))!;
            node["Hp"] = hp;
            return node;
        }
        string Status(double t, uint status, bool present, double duration = 2) => JsonSerializer.Serialize(new
        {
            t, kind = present ? "StatusAdd" : "StatusRemove",
            type = "PromeRotation.LogSystem.LogSystemStatus" + (present ? "Add" : "Remove") + "Event",
            data = new { TargetId = 100ul, SourceId = 1ul, StatusId = status, Duration = duration, Timestamp = Start.AddSeconds(t) },
        });
        void Isolated()
        {
            var hints = Property(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Property(Property(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Daivadipa projection/diagnostic issued forbidden attack, facing or movement hold.");
        }
        load(guid,
        [
            Header(957), Frame(957, 0, [Boss(), Actor(100, 0, center + Vector3.UnitX * 25)], full: true),
            Frame(957, 1, [Actor(100, 0, center)]),
            Frame(957, 2, [Boss(combat: false)]),
            Frame(957, 3, [Boss(hp: 0)]), Frame(957, 4, [Boss()]), Frame(957, 5, []),
        ]);
        controller.Seek(.1);
        Check(!Active() && previews().Length == 2 && dangers() == 0, "Daivadipa pull equality/precombat display differs.");
        controller.Seek(1.2);
        object boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(Active() && (Vector3)Property(boundary, "Position") == center
            && (float)Property(boundary, "Width") == 49, "Daivadipa exact native square missing.");
        controller.Seek(2.2);
        Check(Active(), "Daivadipa deaggro reset encounter.");
        controller.Seek(4.2);
        Check(!Active() && previews().Length == 0, "Daivadipa completed primary generation revived.");
        controller.Seek(1.2);
        manager.SetMechanismEnabled(guid, "Daivadipa", false);
        controller.Seek(1.3);
        Check(!Active() && dangers() == 0 && previews().Length == 0, "Disabled Daivadipa retained outputs.");
        manager.SetMechanismEnabled(guid, "Daivadipa", true);
        controller.Seek(.5);
        controller.Seek(1.2);
        Isolated();
        stop();
        Console.WriteLine("PASS native fate Daivadipa production exact49 square strict25 pull precombat display latch HP0 generation off reverse Seek owner cleanup isolation");

        object[] orbs =
        [
            Actor(10, 0x3570, center with { X = -633 }, dead: true, targetable: false),
            Actor(11, 0x3570, center with { X = -632 }, targetable: false),
            Actor(12, 0x356F, center + Vector3.UnitX * 10),
            .. Enumerable.Range(20, 9).Select(i => Actor((ulong)i, 0x3572, center + Vector3.UnitX * (i - 20))),
        ];
        load(guid,
        [
            Header(957), Frame(957, 0, [Boss(), Actor(100, 0, center), .. orbs, Actor(40, 0x3573, center)], full: true),
            Frame(957, .1, [Boss(Cast(26499, 5, center))]),
            Frame(957, 1, [Boss(Cast(26498, 5, center, startedAt: 1))]),
            Frame(957, 2, [], removed: [10, 11, 12, .. Enumerable.Range(20, 9).Select(i => (ulong)i)]),
            Frame(957, 3, [Actor(40, 0x3573, center, cast: Cast(26501, 1, center, startedAt: 3))]),
            Frame(957, 4, [Actor(40, 0x3573, center)]),
            Frame(957, 5, [Actor(40, 0x3573, center, cast: Cast(26506, 1, center, startedAt: 5))]),
            Frame(957, 6, [Actor(40, 0x3573, center)]), Frame(957, 20, []),
        ]);
        controller.Seek(.5);
        Check(Ambient("lit").Length == 2 && Ambient("burn").Length == 8 && dangers() == 10 && previews().Length == 11,
            "Daivadipa initial prefix, first-eight, source dead orb or dual navigation/display missing.");
        var original = Ambient("lit")[0];
        Check(MathF.Abs((float)Property(original, "Rotation") - 89.999f * MathF.PI / 180) < 1e-7f,
            "Daivadipa raw-X exact cardinal angle lost.");
        controller.Seek(2.2);
        Check(Ambient("lit").Length == 1 && (float)Property(Ambient("lit")[0], "Rotation") == MathF.PI,
            "Red start failed whole-queue reversal or orb destruction cleared copies.");
        controller.Seek(4.2);
        Check(Ambient("lit").Length == 5 && dangers() == 13, "Lit true Finish did not globally consume/reveal original-clock prefix.");
        controller.Seek(6.2);
        Check(Ambient("burn").Length == 8, "Burn head Finish lost hidden original-clock promotion.");
        controller.Seek(20);
        Check(Ambient("lit").Length == 5 && dangers() == 13, "Copied orb sequence expired by prediction clock.");
        controller.Seek(.5);
        Check(dangers() == 10, "Daivadipa reverse Seek did not rebuild both queues.");
        Isolated();
        stop();
        Console.WriteLine("PASS native fate Daivadipa production Lit rawX quantized lock whole list reversal strict prefix Burn first8 hidden promotion global actual Finish dead orb destruction clock retention dual views navigation Seek");

        load(guid,
        [
            Header(957), Frame(957, 0, [Boss(), Actor(100, 0, center)], full: true,
                party: [new { Slot = 0, EntityId = 100ul }]),
            Frame(957, .1, [Boss(Cast(26517, 5, center))], party: [new { Slot = 0, EntityId = 100ul }]),
            Frame(957, 1, [Boss()], party: [new { Slot = 0, EntityId = 100ul }]),
            Frame(957, 10, [], party: [new { Slot = 0, EntityId = 100ul }]),
            EffectEvent(11, 10, 26518, 1),
            TreasureNativeEffect(12, 10, 26518, 1, 100, []), Frame(957, 13, [], party: [new { Slot = 0, EntityId = 100ul }]),
        ]);
        controller.Seek(10.2);
        Check(((IEnumerable)Property(Encounter(), "PredictedDamage")).Cast<object>().Count() == 1
            && dangers() == 0 && previews().Length == 1, "Delayed raidwide expired/cancelled or became danger geometry.");
        controller.Seek(11.2);
        Check(((IEnumerable)Property(Encounter(), "PredictedDamage")).Cast<object>().Count() == 1,
            "Expanded PR event consumed native raidwide receipt.");
        controller.Seek(12.2);
        Check(!((IEnumerable)Property(Encounter(), "PredictedDamage")).Cast<object>().Any(), "Native redemption effect did not clear prediction.");
        stop();
        Console.WriteLine("PASS native fate Daivadipa production delayed raidwide visual cancel clock retention native effect only no fabricated geometry");

        load(guid,
        [
            Header(957), Frame(957, 0, [Boss(), Actor(100, 0, center),
                Actor(10, 0x3570, center)], full: true),
            Frame(957, .1, [Boss(Cast(26499, 5, center))]),
            Status(.2, 1958, true),
            Frame(957, 1, [Actor(100, 0, center, rotation: MathF.PI)]),
            Status(2, 1257, true),
            Status(3, 1958, false), Status(4, 1257, false),
            Frame(957, 5, []),
        ]);
        controller.Seek(.5);
        var baseDangers = dangers();
        var baseMarch = Ambient("march").Length;
        Check(Ambient("march").Length > 0 && Ambient("march").All(a => !(bool)Property(a, "Navigate")),
            "Forced march model did not project or created navigation.");
        controller.Seek(1.2);
        var hints = ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        Check(hints.Contains("Aim into AOEs!"), "Current heading march danger failed.");
        controller.Seek(2.2);
        Check(Ambient("march").Length > baseMarch && dangers() == baseDangers,
            "Actual march replaced pending move or changed ordinary navigation.");
        controller.Seek(4.2);
        Check(Ambient("march").Length == 0 && dangers() == baseDangers, "Forced march Lose did not revoke projection independently.");
        controller.Seek(.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native fate Daivadipa production forced march ordered pending actual status events current heading final destination warning dual projections no special navigation PR execution Seek owner cleanup");

        uint[] actions = [26508, 26509, 26514, 26513, 26516, 26512];
        load(guid,
        [
            Header(957), Frame(957, 0, [Boss(), Actor(100, 0, center),
                .. actions.Select((_, i) => Actor((ulong)(10 + i), 0x3573, center))], full: true),
            Frame(957, .1, actions.Select((action, i) => Actor((ulong)(10 + i), 0x3573, center + Vector3.UnitX * 10,
                cast: Cast(action, 3, center, .25f)))),
            Frame(957, 5, []),
            Frame(957, 6, actions.Select((_, i) => Actor((ulong)(10 + i), 0x3573, center))), Frame(957, 7, []),
        ]);
        controller.Seek(5.2);
        Check(dangers() == 6 && previews().Length == 7, "Daivadipa ordinary AOEs lost a shape or expired before actual finish.");
        controller.Seek(6.2);
        Check(dangers() == 0 && previews().Length == 1, "Daivadipa ordinary true finish retained output.");
        controller.Seek(.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native fate Daivadipa production six ordinary geometries all sources actual finish clocks locked origins R20 falloff dual views navigation Seek owner cleanup");
    }
}
