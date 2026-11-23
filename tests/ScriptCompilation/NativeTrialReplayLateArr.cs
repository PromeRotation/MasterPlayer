using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunLateArrTrials(object managerObject, object runtimeObject, object controllerObject,
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
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        ulong Priority(ulong current = 1) => (ulong)Encounter().GetType().GetMethod("PriorityTarget")!.Invoke(Encounter(), [current, Vector3.Zero])!;
        void Isolated()
        {
            var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
            var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
                "Late ARR trial replay issued PR execution control.");
        }
        string Map(double t, byte index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        var party = new object[] { new { Slot = 0, Role = "Healer", EntityId = 100UL } };
        const string thornId = "masterplayer.trial.thornmarch-hard";
        load(thornId,
        [
            Header(1067), Frame(1067, 0, [Actor(1, 0x391C, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero, classJob: 24),
                Actor(2, 0x3923, Vector3.Zero, targetable: false), Actor(20, 0x233C, Vector3.Zero),
                Actor(21, 0x233C, Vector3.Zero), Actor(22, 0x233C, Vector3.Zero)], full: true, party: party),
            Map(.1, 8, 0x00020001), Map(.2, 12, 0x00020001), Map(.3, 8, 0x00200010),
            Frame(1067, 1, [Actor(1, 0x391C, Vector3.Zero, dead: true, targetable: false),
                Actor(2, 0x3923, Vector3.Zero, combat: true)], party: party),
            JsonSerializer.Serialize(new { t = 1.1, kind = "BattleEnded", type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent",
                data = new { Timestamp = Start.AddSeconds(1.1) } }),
            Frame(1067, 2, [Actor(20, 0x233C, Vector3.Zero, cast: Cast(29201, 3, Vector3.Zero, startedAt: 2))], party: party),
            Frame(1067, 3, [Actor(21, 0x233C, new(-15, 0, 0), cast: Cast(29197, 9, Vector3.Zero, startedAt: 3)),
                Actor(22, 0x233C, new(15, 0, 0), cast: Cast(29196, 10, Vector3.Zero, startedAt: 3))], party: party),
            Frame(1067, 5.4, [Actor(20, 0x233C, Vector3.Zero)], party: party),
            Frame(1067, 6, [Actor(20, 0x233C, Vector3.Zero, cast: Cast(29202, 1, Vector3.Zero, startedAt: 6))], party: party),
            Frame(1067, 7.4, [Actor(20, 0x233C, Vector3.Zero)], party: party),
            Frame(1067, 8, [Actor(20, 0x233C, Vector3.Zero, cast: Cast(29619, 1, Vector3.Zero, startedAt: 8))], party: party),
            Frame(1067, 9.4, [Actor(20, 0x233C, Vector3.Zero)], party: party),
            Frame(1067, 10, [], removed: [1], party: party), Frame(1067, 11, [], party: party),
        ]);
        controller.Seek(.5);
        Check(Group("thorn/tower").Length == 4 && Group("thorn/tower").Where(a => (bool)Value(a, "Navigate"))
            .All(a => (DateTime)Value(a, "Activation") == Start.AddSeconds(.2).AddSeconds(8)),
            "Tower shared map clock missing: " + string.Join("; ", Group("thorn/tower").Select(a => $"{Value(a, "Id")} {Value(a, "Activation"):O}"))); Isolated();
        var descriptor = ((IEnumerable)manager.Scripts).Cast<object>().Single(d => (string)Value(Value(d, "Metadata"), "Id") == thornId);
        dynamic store = managerObject.GetType().GetMethod("GetScriptSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(managerObject, [descriptor])!;
        var assignmentType = Script().GetType().GetProperty("TowerAssignment")!.PropertyType;
        store.SetPropertyValue("TowerAssignment", Enum.Parse(assignmentType, "H1"));
        controller.Seek(1.5);
        Check(Value(Encounter(), "Active") != null && Priority() == 2 && Group("thorn/tower/join").Length == 1
            && Group("thorn/tower").Count(a => (bool)Value(a, "Navigate")) == 1,
            "Dead first moogle reset phase or explicit setting failed.");
        controller.Seek(3.5);
        Check(Group("thorn/stone").Length == 1 && Group("thorn/round").Length == 2, "King phase continuous outputs missing.");
        controller.Seek(5.7);
        dynamic stone = Value(Group("thorn/stone").Single(), "Shape");
        Check((float)stone.InnerRadius == 10 && (float)stone.OuterRadius == 20
            && (DateTime)Value(Group("thorn/stone").Single(), "Activation") == Start.AddSeconds(7.4), "First ring finish plus2 differs.");
        controller.Seek(7.7);
        stone = Value(Group("thorn/stone").Single(), "Shape");
        Check((float)stone.InnerRadius == 20 && (DateTime)Value(Group("thorn/stone").Single(), "Activation") == Start.AddSeconds(9.4), "Second ring finish differs.");
        controller.Seek(9.7); Check(Group("thorn/stone").Length == 0, "Final ring leaked.");
        controller.Seek(10.5); Check(dangers() == 0 && previews().Length == 0, "Destroyed primary retained king phase.");
        controller.Seek(.5); Check(Group("thorn/tower/join").Length == 1 && Group("thorn/stone").Length == 0, "Reverse Seek lost settings/tower state.");
        store.SetPropertyValue("TowerAssignment", Enum.Parse(assignmentType, "Unassigned"));
        manager.SetScriptEnabled(thornId, false);
        Check(dangers() == 0 && previews().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Thornmarch disable leaked."); stop();

        const string whorlId = "masterplayer.trial.whorleater-hard";
        load(whorlId,
        [
            Header(281), Frame(281, 0, [Actor(1, 0xA67, new(0, 0, -25), combat: true),
                Actor(100, 0, new(4, 0, 5), classJob: 24, targetId: 2), Actor(2, 0xA86, Vector3.Zero, stackedStatus: 477),
                Actor(3, 0x1E922A, Vector3.UnitX * 10), Actor(4, 0xA85, Vector3.Zero)], full: true, party: party),
            Frame(281, 1, [Actor(1, 0xA67, new(0, 0, 25), combat: true, rotation: MathF.PI),
                Actor(10, 0xA87, Vector3.Zero)], party: party),
            Frame(281, 2, [Actor(100, 0, new(4, 0, 5), classJob: 24, targetId: 2, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(3)),
                Actor(11, 0x1E9231, new(10, 0, 13), dead: true)], party: party),
            Frame(281, 2.5, [Actor(100, 0, new(4, 0, 5), classJob: 23, targetId: 1)], party: party),
            EffectEvent(3, 1, 1860, 1), EffectEvent(3.5, 10, 1869, 2),
            EffectEvent(4, 10, 1861, 3), EffectEvent(5, 1, 1938, 4),
            Frame(281, 6, [Actor(4, 0xA85, Vector3.Zero, dead: true), Actor(11, 0x1E9231, new(10, 0, 13), eventState: 7)], party: party),
            Frame(281, 7, [], party: party),
        ]);
        controller.Seek(.5);
        Check(Group("whorl/body").Length == 0 && Priority() == 4 && Group("whorl/converter").Length == 1, "Whorl baseline/targets/converter missing.");
        controller.Seek(1.5);
        Check(Group("whorl/body/aoe").Length == 1 && Group("whorl/dive/aoe").Length == 1
            && Group("whorl/body/knockback").Length == 2 && Group("whorl/dive/knockback").Length == 2
            && dangers() == 2, "Whorl independent body/dive missing."); Isolated();
        controller.Seek(2.2);
        Check(Group("whorl/dive/knockback").Length == 0 && Group("whorl/body/knockback").Length == 2,
            "Whorl activation-specific immunity differs.");
        controller.Seek(2.7);
        Check(Group("whorl/dive/knockback").Length == 2 && dangers() == 3, "Whorl poison/live immunity state missing.");
        controller.Seek(3.2);
        Check(Group("whorl/body/aoe").Length == 0 && Group("whorl/body/knockback").Length == 2
            && Group("whorl/dive/aoe").Length == 1, "Whorl body damage effect cleared other state.");
        controller.Seek(3.7);
        Check(Group("whorl/dive/aoe").Length == 0 && Group("whorl/dive/knockback").Length == 2, "Dive snapshot cleared KB.");
        controller.Seek(4.2); Check(Group("whorl/dive/knockback").Length == 0 && Group("whorl/body/knockback").Length == 2, "Dive effect cleared body.");
        controller.Seek(5.2); Check(!Group("whorl/body").Any() && dangers() == 1, "Body KB effect or poison navigation differs.");
        controller.Seek(6.5); Check(dangers() == 0 && Priority(1) == 2, "Physical ranged tail priority/state7 cleanup missing.");
        controller.Seek(1.5); Check(Group("whorl/dive/knockback").Length == 2 && dangers() == 2, "Whorl reverse Seek retained future poison/immunity.");
        manager.SetScriptEnabled(whorlId, false);
        Check(dangers() == 0 && previews().Length == 0, "Whorleater disable leaked."); stop();
        Console.WriteLine("PASS ARR late trials actual DLL replay dead-primary phase, explicit tower settings, ring event clock, independent AOE/KB, poison, class targets, immunity, disable, Seek and PR isolation");
    }
}
