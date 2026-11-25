using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunMax(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.quest.ma-x";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("max/")).ToArray();
        object[] Shown(string part) => previews().Where(a => ((string)Value(a, "Id")).Contains(part)).ToArray();
        var center = new Vector3(295, 0, -22);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(141, t, actors, full: full, removed: removed, party: party, allowedFateId: 1409);
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
                "MA-X replay leaked facing/action/movement execution.");
        }

        load(guid, [Header(141), At(0, [Actor(6, 0x252B, center, dead: true, targetable: false),
            Actor(1, 0x252B, center),
            Actor(2, 0x2529, center + Vector3.UnitX * 30, subKind: 5, isEnemy: false),
            Actor(3, 0x252A, center + Vector3.UnitX * 2), Actor(4, 0x252B, center + Vector3.UnitZ * 10),
            Actor(5, 0x233C, center), Actor(100, 0, center + Vector3.UnitZ), Actor(101, 0, center + Vector3.UnitX * 12)], true),
            At(.5, [Actor(6, 0x252B, center, targetable: false),
                Actor(2, 0x2529, center, dead: true, subKind: 5, isEnemy: false),
                Actor(3, 0x252A, center, targetable: false)]),
            At(1, [new { Id = 1ul, Oid = 0x252Bu, Kind = 2, Position = Pose(center),
                    Hp = 1000u, MaxHp = 1000u, Targetable = true, InCombat = true, Hostile = true,
                    IsEnemy = true, ObjectIndex = 2, FateId = 1409u, Cast = Cast(14600, 20, center, startedAt: 1) },
                Actor(10, 0x233C, center, cast: Cast(14599, 30, center + Vector3.UnitX * 5, startedAt: 1)),
                Actor(11, 0x252A, center, targetable: false, cast: Cast(14601, 40, center, rotation: .4f, startedAt: 1))], removed: [4, 6]),
            EffectEvent(1.1, 10, 14599, 1), At(2, [Actor(10, 0x233C, center)]),
            At(3, [Actor(1, 0x252B, center, dead: true)]), At(4, [])]);
        controller.Seek(.2);
        Check(Shown("max/").Length == 4 && dangers() == 0 && Value(Encounter(), "Active") == null,
            "MA-X precombat dual preview scope or no-navigation contract failed.");
        var noctis = Aoes().Single(a => (string)Value(a, "Id") == "max/enemy/2");
        var trooper = Aoes().Single(a => (string)Value(a, "Id") == "max/enemy/3");
        Check((Vector4)Value(noctis, "Color") == new Vector4(1, 0, 1, 1) && (bool)Value(noctis, "OutlineOnly")
            && (Vector4)Value(trooper, "Color") == new Vector4(1, 128f / 255, 0, 1), "MA-X ally Noctis/object Trooper colors or outside clamp changed.");
        controller.Seek(.7);
        Check(Shown("max/").Length == 2, "MA-X death/targetable marker filter failed.");
        controller.Seek(1.5);
        Check(dangers() == 3 && Shown("cast/").Length == 3 && Shown("max/precombat/").Length == 0,
            "MA-X three actual components failed handoff, helper or untargetable caster acceptance.");
        Check(((IEnumerable)Value(Encounter(), "ForbiddenDotTargets")).Cast<ulong>().SequenceEqual(new ulong[] { 1 })
            && !((IEnumerable)Value(Encounter(), "ForbiddenTargets")).Cast<ulong>().Contains(1ul),
            "MA-X synced FATE DoT intention was missing or widened to all actions.");
        controller.Seek(2.5);
        Check(dangers() == 2, "MA-X actual Finish failed independent component cancellation.");
        controller.Seek(3.5);
        Check(dangers() == 0 && Shown("max/").Length == 0, "MA-X primary death failed phase completion.");
        controller.Seek(.2);
        manager.SetMechanismEnabled(guid, "MA-X", false);
        controller.Seek(.3);
        Check(dangers() == 0 && previews().Length == 0, "Disabled MA-X retained precombat previews.");
        manager.SetMechanismEnabled(guid, "MA-X", true);
        controller.Seek(1.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native MA-X production overworld scoped entry native precombat dual previews colored Noctis and Trooper markers three real AOEs helpers cancellation death mechanism switch reverse Seek and owner cleanup");

        string Create(double t) => JsonSerializer.Serialize(new
        {
            t, kind = "ObjectCreate", type = "PromeRotation.LogSystem.LogSystemObjectCreateEvent",
            data = new { SourceId = 1ul, DataId = 0x252Bu, SourcePosition = Pose(center), Timestamp = Start.AddSeconds(t) },
        });
        load(guid, [Header(141), At(0, [Actor(1, 0x252B, center, combat: true), Actor(100, 0, center)], true),
            At(.1, [Actor(1, 0x252B, center, combat: true, cast: Cast(14600, 20, center, startedAt: .1))]),
            At(1, [Actor(1, 0x252B, center, dead: true)]),
            At(2, [Actor(1, 0x252B, center, combat: true)]), Create(2.1),
            At(2.2, [Actor(1, 0x252B, center, combat: true, cast: Cast(14600, 20, center, startedAt: 2.2))]),
            Director(3, 0x40000005), At(3.1, [Actor(1, 0x252B, center, combat: true)]),
            Director(4, 0x40000011), At(4.1, [Actor(1, 0x252B, center, combat: true,
                cast: Cast(14599, 20, center + Vector3.UnitX * 5, startedAt: 4.1))]), At(5, [])]);
        controller.Seek(.5);
        Check(dangers() == 1, "MA-X initial cast missing.");
        controller.Seek(2.05);
        Check(dangers() == 0 && Value(Encounter(), "Active") == null, "MA-X healed completed object repulled.");
        controller.Seek(2.5);
        Check(dangers() == 1, "MA-X new primary Create failed completed-generation unlock.");
        controller.Seek(3.5);
        Check(dangers() == 0 && previews().Length == 0 && Value(Encounter(), "Active") == null,
            "MA-X wipe failed clear or repulled old snapshots.");
        controller.Seek(4.5);
        Check(dangers() == 1 && Value(Encounter(), "Active") != null, "MA-X explicit wipe reopen failed new cast.");
        controller.Seek(.5);
        Check(dangers() == 1, "MA-X reverse Seek across completion/wipe failed.");
        Isolated();
        stop();
        Check(dangers() == 0 && previews().Length == 0, "MA-X owner stopped with output.");
        Console.WriteLine("PASS native MA-X production completed actor healing new Create generation Director wipe stale pull barrier explicit reopen reverse Seek and replay execution isolation");
    }
}
