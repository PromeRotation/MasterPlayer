using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void CarnivaleBombPlantBody(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Intents() => ((IEnumerable)Value(Encounter(), "CarnivaleInterrupts")).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false) => Frame(796, t, actors, full: full, party: party);
        object Hp(object actor, uint hp)
        {
            var node = JsonNode.Parse(JsonSerializer.Serialize(actor))!;
            node["Hp"] = hp;
            return node;
        }
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Bomb/plant replay leaked controls.");
        }

        foreach (var stage in new[]
        {
            (Oid: 0x2718u, Name: "爆前速攻·第一幕"),
            (Oid: 0x2719u, Name: "爆前速攻·第二幕"),
            (Oid: 0x271Au, Name: "寄生植物·第一幕"),
        })
        {
            var group = stage.Oid != 0x271A;
            load(guid, [Header(796), At(0, [Actor(100, 0, center)], true),
                At(.1, [Actor(1, stage.Oid, center)]),
                At(.2, [Actor(2, stage.Oid, center + Vector3.UnitZ * 10, combat: true)]),
                At(1, [Actor(1, stage.Oid, center, combat: true)]),
                At(2, [Actor(1, stage.Oid, center, combat: true,
                    cast: Cast(group ? 14583u : 14750u, 23, center, startedAt: 2))]),
                At(3, [Actor(1, stage.Oid, center, dead: true)]),
                At(4, [Hp(Actor(2, stage.Oid, center + Vector3.UnitZ * 10, combat: true), 0)]),
                At(5, [Actor(2, stage.Oid, center + Vector3.UnitZ * 10, dead: true)]), At(6, [])]);
            controller.Seek(.15);
            Check(Value(Encounter(), "Active") == null && Value(Encounter(), "CarnivalePrecombatHint") != null
                && Shown("carnivale/precombat/arena").Length == 1 && dangers() == 0, "Preparation contract.");
            controller.Seek(.5);
            Check((Value(Encounter(), "Active") != null) == group && Shown("carnivale/enemy/").Length == 2,
                "Group vs primary pull or all-enemy markers.");
            controller.Seek(2.5);
            Check(Value(Encounter(), "Active") != null && Value(Encounter(), "CarnivalePrecombatHint") == null
                && !Hints().Any() && dangers() == 0 && Shown("cast/").Length == 0 && Intents().Length == 0,
                "Unactivated source enumeration became AOE, intent or combat hint.");
            Check(((IEnumerable)((dynamic)Value(Encounter(), "Arena")).GetContours()).Cast<object>().Count()
                == (stage.Oid == 0x2719 ? 5 : 1), "Stage layout selector.");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 14583 }) == group
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = 14583 })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 60000 }),
                "Inactive Fulmination suppression must be scoped to source act and enemy.");
            controller.Seek(4.5);
            if (group)
                Check((ulong)Value(Encounter(), "PrimaryId") == 1 && Value(Encounter(), "Active") != null,
                    "HP0 sibling ended group after primary death.");
            else
                Check((ulong)Value(Encounter(), "PrimaryId") != 1,
                    "Default primary phase incorrectly waited for all same OID plants.");
            controller.Seek(5.5);
            Check(Value(Encounter(), "Active") == null && previews().Length == 0 && dangers() == 0, "Completion output leak.");
            controller.Seek(2.5);
            Isolated();
            manager.SetMechanismEnabled(guid, stage.Name, false);
            controller.Seek(2.6);
            Check(previews().Length == 0 && dangers() == 0, "Disabled act output.");
            manager.SetMechanismEnabled(guid, stage.Name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale {stage.Oid:X} production actual no AOE source scope generic suppression group or primary phase all markers exact layout HP0 pending handoff reverse Seek disabled owner");
        }

        foreach (var action in new uint[] { 14751, 14754 })
        {
            var locked = center + Vector3.UnitX * 4;
            load(guid, [Header(796), At(0, [Actor(1, 0x271B, center, combat: true), Actor(100, 0, center)], true),
                CastEvent(.1, 1, action, 4, center, rotation: -.4f),
                At(.2, [Actor(1, 0x271B, center, combat: true, cast: Cast(action, 4, locked, rotation: .7f))]),
                At(1, [Actor(1, 0x271B, center + Vector3.UnitZ * 20, combat: true,
                    cast: Cast(action, 4, locked, rotation: .7f), rotation: -1.2f)]),
                At(8, [Actor(1, 0x271B, center, combat: true)]), At(9, [])]);
            controller.Seek(6);
            Check(dangers() == 1 && Shown("cast/").Length == 1
                && (Vector3)((dynamic)Value(Shown("cast/").Single(), "Geometry")).Position == locked
                && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = (ushort)action }),
                "Production cone lock, observed lifecycle or native takeover.");
            controller.Seek(8.5);
            Check(dangers() == 0 && Shown("cast/").Length == 0, "Cone Finish output.");
            controller.Seek(2.5);
            Check(dangers() == 1, "Cone reverse Seek.");
            Isolated();
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage12 Act2 production exact two locked cones packet to first raw correction actor turns observed end double view navigation reverse Seek owner cleanup");

        foreach (var action in new uint[] { 14752, 14753 })
        {
            var cast = Cast(action, 15, center, startedAt: 0);
            load(guid, [Header(796), At(0, [Actor(1, 0x271B, center, cast: cast, isEnemy: true, objectIndex: 2),
                    Actor(100, 0, center)], true),
                At(1, [Actor(1, 0x271B, center, combat: true, cast: cast, isEnemy: true, objectIndex: 2)]),
                At(2, [Actor(1, 0x271B, center, combat: true, cast: cast, targetable: false, isEnemy: true, objectIndex: 2)]),
                EffectEvent(16, 1, action, 1, center),
                At(20, [Actor(1, 0x271B, center, combat: true)]), At(21, [])]);
            controller.Seek(.5);
            Check(Intents().Length == 0 && !Hints().Contains("Stun!")
                && !Hints().Any(h => h.StartsWith("Calls Roselets")), "Cast components active before pull.");
            controller.Seek(1.5);
            if (action == 14753)
                Check(Intents().Length == 1 && (bool)Value(Intents().Single(), "ShouldBeStunned")
                    && !(bool)Value(Intents().Single(), "ShouldBeInterrupted") && Hints().Contains("Stun!"),
                    "Fumes source only stun intent.");
            else
                Check(Intents().Length == 0 && Hints().Contains("Calls Roselets. Prepare Ice Spikes if available."),
                    "Spore plain cast hint.");
            controller.Seek(18);
            Check(dangers() == 0 && Shown("cast/").Length == 0 && !Hints().Contains("Interrupt!")
                && (action != 14753 || Intents().Length == 1 && !(bool)Value(Intents().Single(), "ShouldBeStunned")),
                "Hint effect/deadline or enemy projection lost source contract.");
            controller.Seek(20.5);
            Check(Intents().Length == 0 && !Hints().Contains("Stun!")
                && !Hints().Any(h => h.StartsWith("Calls Roselets")), "Hint Finish.");
            controller.Seek(1.5);
            Isolated();
            manager.SetMechanismEnabled(guid, "寄生植物·第二幕", false);
            controller.Seek(1.6);
            Check(Intents().Length == 0 && dangers() == 0 && previews().Length == 0, "Hint disabled owner.");
            manager.SetMechanismEnabled(guid, "寄生植物·第二幕", true);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage12 Act2 production Spore pure hint Fumes only Stun flags bootstrap projection deadline effect real Finish reverse Seek no invented AOE actions");

        load(guid, [Header(796), At(0,
            [Actor(1, 0x271B, center, combat: true, isEnemy: true, objectIndex: 2),
                Actor(10, 0x271C, center + Vector3.UnitZ * 10, isEnemy: true, objectIndex: 4),
                Actor(11, 0x9999, center, isEnemy: true, objectIndex: 198), Actor(100, 0, center)], true),
            At(1, [Hp(Actor(10, 0x271C, center + Vector3.UnitZ * 10, isEnemy: true, objectIndex: 4), 1)]),
            At(2, [])]);
        controller.Seek(.5);
        Check((Vector4)Value(Shown("carnivale/enemy/10").Single(), "Color") == new Vector4(1, 128f / 255, 0, 1)
            && (Vector4)Value(Shown("carnivale/enemy/").Single(a => ((string)Value(a, "Id")).EndsWith("/1")), "Color")
                == new Vector4(1, 0, 0, 1),
            "Production Roselet object colour.");
        var choose = Encounter().GetType().GetMethod("PriorityTarget")!;
        Check((ulong)choose.Invoke(Encounter(), [11ul, center])! == 10, "Production Roselet priority.");
        controller.Seek(1.5);
        Check((ulong)choose.Invoke(Encounter(), [10ul, center])! != 10, "Production pointless override.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage12 Act2 production object orange Roselets enemy red primary priority one all others zero preserve pointless");
    }
}
