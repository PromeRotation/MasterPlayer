using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTime(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.quest.time-between-the-seconds";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Flames() => ((IEnumerable)Value(Encounter(), "TimeBetweenFlames")).Cast<object>().ToArray();
        Vector3 Origin(object flame) => (Vector3)flame.GetType().GetField("Item3")!.GetValue(flame)!;
        DateTime Activation(object flame) => (DateTime)flame.GetType().GetField("Item4")!.GetValue(flame)!;
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        var center = new Vector3(-247, 0, 540);
        var fire = center + new Vector3(1.23456f, 0, 2.34567f);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(685, t, actors, full: full, removed: removed, party: party);
        string Create(double t, ulong id, uint oid, Vector3 position, double? createdAt = null) => JsonSerializer.Serialize(new
        {
            t, kind = "ObjectCreate", type = "PromeRotation.LogSystem.LogSystemObjectCreateEvent",
            data = new { SourceId = id, DataId = oid, SourcePosition = Pose(position), Timestamp = Start.AddSeconds(createdAt ?? t) },
        });
        string Director(double t, uint command) => JsonSerializer.Serialize(new
        {
            t, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
            data = new { Command = command, Timestamp = Start.AddSeconds(t) },
        });
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "TimeBetween replay leaked native execution.");
        }

        load(guid, [Header(685), At(0, [Actor(1, 0x1A36, center, combat: true, cast: Cast(8987, 20, center, startedAt: 0)),
            Actor(9, 0x1CED, fire, targetable: false), Actor(10, 0x1CEE, center, cast: Cast(8993, 30, center, startedAt: 0)),
            Actor(13, 0x233C, center, cast: Cast(8985, 30, center, rotation: .4f, startedAt: 0)),
            Actor(14, 0x233C, center, cast: Cast(8986, 30, center, startedAt: 0)),
            Actor(15, 0x233C, center, cast: Cast(8987, 20, center, startedAt: 0)),
            Actor(100, 0, center + Vector3.UnitZ), Actor(101, 0, center + Vector3.UnitX * 12)], true),
            At(.5, [Actor(9, 0x1CED, center + Vector3.UnitX * 15, targetable: false,
                cast: Cast(8988, .5, center, startedAt: .5))]), EffectEvent(.7, 9, 8988, 1),
            At(1, [Actor(9, 0x1CED, center + Vector3.UnitX * 15), Actor(15, 0x233C, center)]),
            JsonSerializer.Serialize(new { t = 1.1, kind = "ObjectRemove", type = "PromeRotation.LogSystem.LogSystemObjectRemoveEvent",
                data = new { SourceId = 1ul, DataId = 0x1A36u, Timestamp = Start.AddSeconds(1.1) } }),
            At(1.2, [], removed: [9]),
            At(2, [Actor(1, 0x1A36, center, combat: true, targetable: false), Actor(11, 0x1EBC, center)]),
            JsonSerializer.Serialize(new { t = 2.15, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent",
                packetFacing = 0f, data = new { SourceId = 15ul, ActionId = 8987, CastTime = 20f, EffectPosition = Pose(center), Timestamp = Start } }),
            At(2.2, [Actor(12, 0x1CED, center + Vector3.UnitZ * 4, dead: true, targetable: false)]),
            At(3, [Actor(11, 0x1EBC, center, dead: true, targetable: false)]),
            At(4, [], removed: [11]), At(4.1, [Actor(1, 0x1A36, center, combat: true)]),
            Create(4.2, 1, 0x1A36, center), At(4.25, [], removed: [1]),
            At(4.3, [Actor(1, 0x1A36, center, combat: true)]), At(5, [])]);
        controller.Seek(.3);
        Check(dangers() == 4 && Shown("cast/").Length == 3 && Shown("time/flame/").Length == 1
            && (int)Value(Encounter(), "TimeBetweenPhase") == 0,
            "TimeBetween P1 components included sword or lost flame/ordinary geometry.");
        Check(((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().Count() == 2,
            "TimeBetween real raidwide party prediction missing.");
        var runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MasterPlayer.MechanicRuntime");
        var positionType = runtimeAssembly.GetType("BossMod.WPos")!;
        var sourcePosition = Activator.CreateInstance(positionType, [fire.X, fire.Z])!;
        var quantized = positionType.GetMethod("Quantized")!.Invoke(sourcePosition, null)!;
        var expectedFire = new Vector3((float)positionType.GetField("X")!.GetValue(quantized)!, 0,
            (float)positionType.GetField("Z")!.GetValue(quantized)!);
        Check(Origin(Flames().Single()) == expectedFire && Activation(Flames().Single()) == Start.AddSeconds(7),
            "TimeBetween creation origin differs from actual source quantization or activation.");
        dynamic arena = Value(Encounter(), "Arena");
        var sourceType = runtimeAssembly.GetType("BossMod.Stormblood.Quest.MSQ.TheTimeBetweenTheSeconds.ZenosYaeGalvus")!;
        dynamic oracle = sourceType.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var sourceCenter = new Vector3((float)oracle.Center.X, 0, (float)oracle.Center.Z);
        var direction = runtimeAssembly.GetType("BossMod.WDir")!;
        Check(Vector3.Distance((Vector3)arena.Position, sourceCenter) < .0001f, "TimeBetween source AABB center changed.");
        var compared = 0;
        for (var x = -274.13f; x < -222; x += .8f)
        for (var z = 518.17f; z < 571; z += .8f)
        {
            var expected = (bool)((object)oracle).GetType().GetMethod("Contains")!.Invoke((object)oracle,
                [Activator.CreateInstance(direction, [x - sourceCenter.X, z - sourceCenter.Z])])!;
            Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, $"TimeBetween arena source mismatch at {x},{z}.");
            ++compared;
        }
        controller.Seek(1.5);
        Check(Flames().Length == 1 && Origin(Flames()[0]) == expectedFire && Activation(Flames()[0]) == Start.AddSeconds(7),
            "TimeBetween normal source ID changed/cleared source ActorID0 flame on cast/effect/cancel/destroy.");
        controller.Seek(2.1);
        Check(Flames().Length == 0 && dangers() == 2 && (int)Value(Encounter(), "TimeBetweenPhase") == 1,
            "TimeBetween phase transition did not clear old components and replay current sword/cone.");
        controller.Seek(3.5);
        Check(Value(Encounter(), "Active") != null && Flames().Length == 1 && dangers() == 3
            && Activation(Flames()[0]) == Start.AddSeconds(2.2).AddSeconds(7),
            $"TimeBetween dead P2 actor or dead/untargetable flame changed: active={Value(Encounter(), "Active") != null}, phase={Value(Encounter(), "TimeBetweenPhase")}, flames={Flames().Length}, activations={string.Join(",", Flames().Select(a => Activation(a).ToString("O")))}.");
        controller.Seek(4.15);
        Check(Value(Encounter(), "Active") == null && dangers() == 0 && previews().Length == 0,
            "TimeBetween P2 removal failed completion latch.");
        controller.Seek(4.5);
        Check(Value(Encounter(), "Active") != null && (int)Value(Encounter(), "TimeBetweenPhase") == 0
            && Activation(Flames().Single()) == Start.AddSeconds(4.3).AddSeconds(7),
            $"TimeBetween new primary Create: active={Value(Encounter(), "Active") != null}, phase={Value(Encounter(), "TimeBetweenPhase")}, flames={Flames().Length}, activations={string.Join(",", Flames().Select(a => Activation(a).ToString("O")))}.");
        controller.Seek(.3);
        Check(Origin(Flames().Single()) == expectedFire && Activation(Flames().Single()) == Start.AddSeconds(7),
            "TimeBetween reverse Seek retained future phase flame.");
        manager.SetMechanismEnabled(guid, "Zenos yae Galvus", false);
        controller.Seek(.4);
        Check(dangers() == 0 && previews().Length == 0, "Disabled TimeBetween retained output.");
        manager.SetMechanismEnabled(guid, "Zenos yae Galvus", true);
        controller.Seek(.3);
        Isolated();
        stop();
        Console.WriteLine($"PASS native TimeBetween production exact P1 P2 components source ActorID0 frozen flames native quantization source arena oracle {compared} points raidwide prediction dead actor retention completion new Create reverse Seek and owner cleanup");

        string BattleEnd(double t) => JsonSerializer.Serialize(new
        {
            t, kind = "BattleEnded", type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent",
            data = new { Timestamp = Start.AddSeconds(t) },
        });
        object PrimaryZero() => new { Id = 1ul, Oid = 0x1A36u, Kind = 2, Position = Pose(center),
            Hp = 0u, MaxHp = 1000u, Dead = true, Targetable = true, InCombat = true };
        load(guid, [Header(685), At(0, [PrimaryZero(), Actor(100, 0, center)], true),
            At(1, [], removed: [1]), BattleEnd(1.1), Create(2, 1, 0x1A36, center),
            At(2.1, [Actor(1, 0x1A36, center, targetable: false), Actor(11, 0x1EBC, center)]),
            Director(3, 0x40000005), At(3.1, [Actor(1, 0x1A36, center, combat: true)]),
            Director(4, 0x40000011), At(4.1, [Actor(1, 0x1A36, center, combat: true)], removed: [11]),
            At(5, [Actor(1, 0x1A36, center, combat: true, targetable: false)]), At(6, [])]);
        controller.Seek(.5);
        Check(Value(Encounter(), "Active") != null, "TimeBetween source default HP0/dead primary pull failed.");
        controller.Seek(2.5);
        Check(Value(Encounter(), "Active") != null && (int)Value(Encounter(), "TimeBetweenPhase") == 1
            && !Shown("job-quest/enemy/1").Any(a => ((string)Value(a, "Id")).EndsWith("/1", StringComparison.Ordinal)),
            "TimeBetween still-loaded module failed same ID/OID primary rebind or BattleEnded reset it.");
        controller.Seek(3.5);
        Check(Value(Encounter(), "Active") == null && dangers() == 0, "TimeBetween wipe repulled stale snapshot.");
        controller.Seek(4.5);
        Check(Value(Encounter(), "Active") != null, "TimeBetween wipe unlock failed valid P1.");
        controller.Seek(5.5);
        Check(Value(Encounter(), "Active") == null && dangers() == 0 && previews().Length == 0,
            "TimeBetween empty P2 failed immediate same-update completion.");
        controller.Seek(.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native TimeBetween production default HP0 dead pull primary absence and same ID OID rebind BattleEnded persistence Director wipe reopen empty P2 immediate completion reverse Seek execution isolation");

        load(guid, [Header(685), At(0, [Actor(1, 0x1A36, center, combat: true),
            Actor(9, 0x1CED, fire), Actor(100, 0, center)], true),
            Create(.1, 1, 0x1A36, center, .01), Create(.1, 9, 0x1CED, fire, .01),
            At(.2, [Actor(1, 0x1A36, center, combat: true, targetable: false), Actor(11, 0x1EBC, center)]),
            Create(.3, 20, 0x1CED, center + Vector3.UnitX * 3), At(.4, []),
            At(.5, [Actor(20, 0x1CED, center + Vector3.UnitX * 3)]),
            Create(.6, 20, 0x1CED, center - Vector3.UnitX * 3), At(.7, [], removed: [20]),
            At(.8, [Actor(20, 0x1CED, center - Vector3.UnitX * 3)]),
            Create(.9, 21, 0x1CED, center), At(.9, [Actor(21, 0x1CED, center)]),
            At(1, [], removed: [21]), At(1.1, [Actor(21, 0x1CED, center + Vector3.UnitX)]), At(1.5, [])]);
        controller.Seek(.15);
        Check(Flames().Length == 1 && Shown("job-quest/enemy/1").Length == 1,
            "TimeBetween raw-first Create confirmation duplicated flame or froze original primary.");
        controller.Seek(.55);
        Check((int)Value(Encounter(), "TimeBetweenPhase") == 1 && Flames().Length == 2
            && Flames().Any(a => Activation(a) == Start.AddSeconds(.5).AddSeconds(7)),
            "TimeBetween event-before-raw created or retimed a flame before source frame presence.");
        controller.Seek(.85);
        Check(Flames().Length == 3 && Flames().Any(a => Activation(a) == Start.AddSeconds(.8).AddSeconds(7)),
            "TimeBetween duplicate Create or real raw absence mishandled a generation.");
        controller.Seek(1.2);
        Check(Flames().Length == 5 && Flames().Any(a => Activation(a) == Start.AddSeconds(1.1).AddSeconds(7)),
            "TimeBetween same-clock Create/raw then brief absence lost the next real generation.");
        controller.Seek(.15);
        Check((int)Value(Encounter(), "TimeBetweenPhase") == 0 && Flames().Length == 1,
            "TimeBetween creation receipt state survived reverse Seek.");
        Isolated();
        stop();
        Console.WriteLine("PASS native TimeBetween production raw lifecycle late and duplicate PR Create event-before-raw same-clock raw confirmation short absence new generation phase transition reverse Seek and owner isolation");
    }
}
