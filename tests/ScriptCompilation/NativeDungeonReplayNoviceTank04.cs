using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunNoviceTank04(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string guid = "masterplayer.novice.tank04";
        object Property(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => ((IEnumerable)manager.Running.Values).Cast<object>().Select(s => Property(s, "Script"))
            .Single(s => s.GetType().Name == "NoviceTank04");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object? Plan() => Encounter().GetType().GetProperty("NoviceTank04", flags)!.GetValue(Encounter());
        ulong[] Forbidden() => (ulong[])Property(Encounter(), "ForbiddenTargets");
        bool Active() => (bool)Property(Script(), "EncounterActive");
        object Dummy(ulong id, Vector3 position = default, bool targetable = true, bool dead = false, uint? nameId = 541)
            => Actor(id, 0x1564, position, targetable: targetable, dead: dead, isEnemy: true,
                objectIndex: (ushort)(id * 2), nameId: nameId ?? 0);
        void Isolated()
        {
            var hints = Property(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Property(Property(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Tank04 replay leaked execution or expanded intent into facing/movement hold.");
        }
        load(guid,
        [
            Header(540), Frame(540, 0, [Dummy(1, targetable: false), Actor(100, 0, default)], full: true),
            Frame(540, .5, [Dummy(1)]), Frame(540, 1, [Dummy(1, targetable: false)]),
            Frame(540, 2, [], removed: [1]), Frame(540, 3, []),
        ]);
        controller.Seek(.1);
        Check(!Active() && previews().Length == 1 && dangers() == 0, "Tank04 untargetable precombat display differs.");
        controller.Seek(.7);
        Check(Active() && Forbidden().SequenceEqual(new ulong[] { 1 }) && Plan() == null
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary != null,
            "Tank04 targetable-only pull or single dummy forbidden missing.");
        controller.Seek(1.2);
        Check(Active() && Forbidden().Length == 0, "Untargetable training reset or retained invalid forbidden candidate.");
        controller.Seek(2.2);
        Check(!Active() && previews().Length == 0 && Forbidden().Length == 0, "Destroyed training primary leaked output.");
        controller.Seek(.7);
        Isolated();
        stop();
        Console.WriteLine("PASS native novice Tank04 production CFC154 territory540 targetable only pull no combat R20 primary removal untargetable latch replay control isolation Seek owner cleanup");

        load(guid,
        [
            Header(540), Frame(540, 0, [Dummy(1, new(-4, 0, -2)), Dummy(2, new(6, 0, 2)),
                Dummy(3), Actor(100, 0, new(1, 0, 0), classJob: 21)], full: true),
            Frame(540, 1, [Actor(100, 0, new(2, 0, 0), classJob: 21)]),
            Frame(540, 2, [Dummy(4)]), Frame(540, 3, [], removed: [4]),
            Frame(540, 4, [Dummy(2, new(6, 0, 2), dead: true)]), Frame(540, 5, []),
        ]);
        controller.Seek(.2);
        var plan = Plan()!;
        Check((Vector3)Property(plan, "Center") == new Vector3(1, 0, 0)
            && (float)Property(plan, "GoalWeight") == 1 && (float)Property(plan, "GoalRadius") == 1
            && (uint)Property(plan, "Action") == 41 && (bool)Property(plan, "RequestsAction")
            && (bool)Property(plan, "ClearsActionIntents")
            && plan.GetType().GetProperty("ActionTarget", flags)!.GetValue(plan) == null
            && (float)Property(plan, "ActionPriority") == 1 && Forbidden().Length == 3,
            "Tank04 source bbox, weak goal or WAR action intent changed.");
        Check(dangers() == 0 && ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count() == 0
            && previews().Length == 1, "Discarded weak goal or action intent became navigation/extra display.");
        controller.Seek(1.2);
        Check(!(bool)Property(Plan()!, "RequestsAction"), "Tank04 spell intent triggered at exact distance1.");
        controller.Seek(2.2);
        Check(Plan() == null && Forbidden().Length == 4, "Count4 retained weak plan or stopped forbidden output.");
        controller.Seek(4.2);
        Check(Plan() != null && Forbidden().Length == 2, "Dead secondary incorrectly removed from world-count or retained attack restriction.");
        manager.SetMechanismEnabled(guid, "NoviceTank04", false);
        controller.Seek(4.3);
        Check(!Active() && Plan() == null && Forbidden().Length == 0 && previews().Length == 0,
            "Tank04 disabled owner retained controls or plan.");
        manager.SetMechanismEnabled(guid, "NoviceTank04", true);
        controller.Seek(.2);
        Isolated();
        stop();
        Console.WriteLine("PASS native novice Tank04 production all actor exact3 bbox center strict1 weak goal weight1 source null-target WAR action intent priority1 local clear count4 dead secondary no added navigation or auto execution");

        object Lethal(ulong id, uint name)
        {
            var node = JsonSerializer.SerializeToNode(Dummy(id, nameId: name))!;
            node["Hp"] = 100;
            node["IncomingEffects"] = JsonSerializer.SerializeToNode(new[]
            {
                new { Slot = 0, GlobalSequence = 1u, TargetIndex = (byte)0, TargetConfirmed = false,
                    Knockback = false, HpDelta = -100 },
            });
            return node;
        }
        load(guid,
        [
            Header(540), Frame(540, 0, [Dummy(1), Lethal(2, 0), Actor(100, 0, default)], full: true),
            Frame(540, 1, [Lethal(2, 541)]), Frame(540, 2, []),
        ]);
        controller.Seek(.2);
        Check(Forbidden().SequenceEqual(new ulong[] { 1 }), "PendingDead protection was bypassed by forbidden setter.");
        controller.Seek(1.2);
        Check(Forbidden().Order().SequenceEqual(new ulong[] { 1, 2 }), "Dummy NameID541 exception missing in production.");
        Isolated();
        stop();
        Console.WriteLine("PASS native novice Tank04 production forbidden exact legal candidates pending HP Pointless setter and NameID541 dummy exception no replay ACR execution");

        load(guid,
        [
            Header(540), Frame(540, 0, [Dummy(1, targetable: false), Dummy(2), Dummy(3),
                Actor(100, 0, default)], full: true),
            Frame(540, 1, [Dummy(3), Dummy(2), Dummy(1)]),
            Frame(540, 2, [], removed: [1]), Frame(540, 3, []),
        ]);
        controller.Seek(.2);
        Check(!Active() && Forbidden().Length == 0 && Plan() == null,
            "Tank04 skipped its bound untargetable primary.");
        controller.Seek(1.2);
        Check(Active() && (ulong)Property(Encounter(), "PrimaryId") == 1,
            "Tank04 snapshot reorder rebound the primary.");
        controller.Seek(2.2);
        Check(Active() && (ulong)Property(Encounter(), "PrimaryId") == 3,
            "Tank04 did not promote last pending dummy on a later replay tick.");
        controller.Seek(.2);
        Check(!Active() && Forbidden().Length == 0, "Tank04 reverse Seek retained a later primary.");
        Isolated();
        stop();
        Console.WriteLine("PASS native novice Tank04 production bound untargetable primary snapshot reorder reverse pending promotion and reverse Seek");
    }
}
