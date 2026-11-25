using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunFallenKuribu(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Aoes(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix)).ToArray();
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        ulong Target(ulong current) => (ulong)Encounter().GetType().GetMethod("PriorityTarget")!.Invoke(Encounter(), [current, new Vector3(232.3f, 0, 407.7f)])!;
        string Icon(double t, ulong source, ulong target, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, TargetId = target, MarkerId = icon, Timestamp = Start.AddSeconds(t - 5) } });
        var center = new Vector3(232.3f, 0, 407.7f);
        object Boss(bool shield = true) => Actor(1, 0x1CAF, center, combat: true, objectIndex: 2, isEnemy: true, stackedStatus: shield ? 325u : 0);
        object Npc(bool dead = false) => Actor(2, 0x1CAD, center + Vector3.UnitX * 8, subKind: 5, isEnemy: false, dead: dead);
        object Add(bool shield = false) => Actor(3, 0x18D6, center, objectIndex: 4, isEnemy: true, stackedStatus: shield ? 325u : 0);
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        load("masterplayer.quest.our-unsung-heroes",
        [
            Header(722), Frame(722, 0, [Boss(), Npc(), Add(), Actor(100, 0, center, targetId: 1)], full: true, party: party),
            Icon(1, 2, 100, 71), Icon(1.1, 2, 100, 71), Icon(1.2, 100, 2, 62),
            Frame(722, 2, [Npc(dead: true), Add(shield: true)], party: party),
            Frame(722, 3, [Npc()], party: party),
            Icon(4, 2, 100, 71),
            EffectEvent(10, 999, 8636, 1, target: 999),
            EffectEvent(11, 999, 8637, 2, target: 999),
            Frame(722, 12, [Boss(shield: false)], party: party),
            Frame(722, 13, [Actor(1, 0x1CAF, center, dead: true), Add()], party: party),
            Frame(722, 14, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(Aoes("fallen/spread/").Count(a => (bool)Value(a, "Display")) == 2 && dangers() == 3 && previews().Length == 4,
            "Fallen source-actor icons, repeated spreads, independent stack buddy or default marker drawing.");
        Check(Damage().Single() is { } first && (ulong)Value(first, "TargetId") == 2 && (int)Value(first, "PartySlot") == 24
            && (DateTime)Value(first, "Activation") == Start.AddSeconds(1).AddSeconds(5.15f),
            "Fallen NPC spread prediction or consumed icon clock.");
        Check(Target(1) == 3 && !((IEnumerable)Value(Encounter(), "ForbiddenTargets")).Cast<object>().Any(),
            "Fallen invincibility selection blocked incidental AOE.");
        controller.Seek(2.5);
        Check(dangers() == 0 && previews().Length == 2 && Damage().Length == 0 && Target(1) == 0
            && (ulong)Encounter().GetType().GetMethod("TargetToClear")!.Invoke(Encounter(), [1ul])! == 1,
            "Fallen spread death, stack retention or no-positive-target ownership plan.");
        var hints = Value(Script(), "Hints");
        var control = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)control.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!,
            "Offline Fallen executed PR target controls.");
        controller.Seek(3.5);
        Check(dangers() == 1 && Aoes("fallen/spread/").Length == 0 && previews().Length == 2,
            "Fallen dead spread revived or surviving stack disappeared.");
        controller.Seek(9.5);
        Check(dangers() == 2 && previews().Length == 3, "Fallen delayed icon records expired after activation.");
        controller.Seek(10.5);
        Check(dangers() == 1 && Damage().Length == 0, "Fallen unmatched spread effect failed to consume first record.");
        controller.Seek(11.5);
        Check(dangers() == 0 && previews().Length == 1, "Fallen unmatched stack effect failed to consume first record.");
        controller.Seek(12.5);
        Check(Target(3) == 1 && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 3, ActionId = 8636 }),
            "Fallen invincibility removal or icon-only effect action ownership.");
        controller.Seek(13.5);
        Check(previews().Length == 0 && dangers() == 0, "Fallen primary death waited for adds.");
        controller.Seek(1.5);
        Check(dangers() == 3 && Damage().Length == 1, "Fallen reverse Seek reconstruction.");
        stop();
        Console.WriteLine("PASS production Fallen Kuribu: source-actor/repeated icons, consumed clocks, NPC24 prediction, independent stack/spread, permanent spread death versus temporary stack death, invincible target selection without AOE ban, replay isolation, effects, primary death, reverse Seek and cleanup");
    }
}
