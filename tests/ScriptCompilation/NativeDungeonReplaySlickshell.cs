using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunSlickshell(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.quest.his-forgotten-home";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Aoes(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix)).ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        var center = new Vector3(470, 0, 302);
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(634, t, actors, full: full, removed: removed, party: party);
        string Icon(double t, ulong target, ulong source = 100) => JsonSerializer.Serialize(new
        {
            t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, TargetId = target, MarkerId = 26u, Timestamp = Start.AddSeconds(t) },
        });
        string BattleEnd(double t) => JsonSerializer.Serialize(new
        {
            t, kind = "BattleEnded", type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent",
            data = new { Timestamp = Start.AddSeconds(t) },
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
                "Slickshell replay leaked facing/action/movement execution.");
        }
        object[] Initial(bool combat = true) => [Actor(1, 0x213A, center, combat: combat),
            Actor(100, 0, center - Vector3.UnitX * 6), Actor(101, 0, center - Vector3.UnitX * 4)];
        object PrimaryZero(bool dead = false) => new { Id = 1ul, Oid = 0x213Au, Kind = 2, Position = Pose(center),
            Hp = 0u, MaxHp = 1000u, Dead = dead, Targetable = true, InCombat = true };

        load(guid, [Header(634), At(0, Initial(false), true),
            At(.1, [PrimaryZero()]),
            At(.2, [Actor(10, 0x213B, center, cast: Cast(8585, 20, center, rotation: .4f, startedAt: .2)),
                Actor(11, 0x213C, center, cast: Cast(5831, 30, center, startedAt: .2))]),
            At(1, [PrimaryZero(true)]), BattleEnd(1.1),
            At(2, [], removed: [1]), BattleEnd(2.1),
            At(2.2, [Actor(12, 0x213D, center, cast: Cast(5831, 30, center, startedAt: 2.2))]),
            At(3, [Actor(10, 0x213B, center)]),
            At(4, [Actor(11, 0x213C, center), Actor(12, 0x213D, center)]), At(5, [])]);
        controller.Seek(.05);
        Check(dangers() == 0, "Slickshell idle primary pulled.");
        controller.Seek(.5);
        Check(dangers() == 2 && Shown("cast/").Length == 2, "Slickshell HP0 pull or ordinary dual-view output failed.");
        dynamic arena = Value(Encounter(), "Arena");
        var sourceType = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MasterPlayer.MechanicRuntime")
            .GetType("BossMod.Stormblood.Quest.MSQ.HisForgottenHome.SlickshellCaptain")!;
        dynamic oracle = sourceType.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var sourceCenter = new Vector3((float)oracle.Center.X, 0, (float)oracle.Center.Z);
        var direction = sourceType.Assembly.GetType("BossMod.WDir")!;
        Check(Vector3.Distance((Vector3)arena.Position, sourceCenter) < .0001f, "Slickshell source AABB arena center changed.");
        var compared = 0;
        for (var x = 441.13f; x < 500; x += .8f)
        for (var z = 279.17f; z < 325; z += .8f)
        {
            var expected = (bool)((object)oracle).GetType().GetMethod("Contains")!.Invoke((object)oracle,
                [Activator.CreateInstance(direction, [x - sourceCenter.X, z - sourceCenter.Z])])!;
            Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, $"Slickshell source arena mismatch at {x},{z}.");
            ++compared;
        }
        controller.Seek(2.5);
        Check(dangers() == 3 && Value(Encounter(), "Active") != null, "Slickshell primary destroy/BattleEnded prematurely ended phase or rejected later cast.");
        controller.Seek(3.5);
        Check(dangers() == 2, "Slickshell real cancel removed unrelated cast.");
        controller.Seek(4.5);
        Check(dangers() == 0, "Slickshell ordinary cancel failed cleanup.");
        controller.Seek(.5);
        manager.SetMechanismEnabled(guid, "Slickshell Captain", false);
        controller.Seek(.6);
        Check(dangers() == 0 && previews().Length == 0, "Slickshell disabled mechanism retained outputs.");
        manager.SetMechanismEnabled(guid, "Slickshell Captain", true);
        controller.Seek(.5);
        Isolated();
        stop();
        Console.WriteLine($"PASS native Slickshell production HP0 pull primary death and destruction preserve phase source arena oracle {compared} points ordinary dual-view navigation cancellation mechanism switch reverse Seek owner cleanup");

        load(guid, [Header(634), At(0, Initial(), true), Icon(.1, 101),
            At(.2, [Actor(101, 0, center - Vector3.UnitX * 4 + Vector3.UnitZ * 3)]),
            At(1, [Actor(10, 0x213B, center, cast: Cast(1087, 10, center, target: 101, startedAt: 1))]),
            Icon(1.1, 100, 101), At(2, [Actor(10, 0x213B, center)]),
            TreasureNativeEffect(2.1, 10, 1087, 1, 101, [100], center + Vector3.UnitX * 20),
            EffectEvent(2.1, 10, 1087, 1, center + Vector3.UnitX * 20, target: 100),
            At(3, [Actor(20, 0x1E8D9C, center - Vector3.UnitX * 4 + Vector3.UnitZ * 3, dead: true, targetable: false)]),
            At(4, [Actor(20, 0x1E8D9C, center, eventState: 7)]), At(5, [])]);
        controller.Seek(.5);
        Check(Shown("slickshell/bait/").Length == 1 && dangers() == 1
            && (Vector3)Value(Aoes("slickshell/bait/").Single(a => ((string)Value(a, "Id")).EndsWith("/display")), "Position")
                == center - Vector3.UnitX * 4 + Vector3.UnitZ * 3,
            "Slickshell icon failed packet target dynamic follow or other bait navigation.");
        controller.Seek(1.05);
        Check(Shown("slickshell/bait/").Length == 0 && Aoes("trial/voidzone/cast/").Length == 1, "Any-source Blizzard start did not hand off icon to cast.");
        controller.Seek(2.5);
        Check(Shown("slickshell/bait/").Length == 1 && Aoes("trial/voidzone/event/").Length == 1
            && (Vector3)Value(Aoes("trial/voidzone/event/").Single(), "Position") == center - Vector3.UnitX * 4 + Vector3.UnitZ * 3,
            "Slickshell native main target was expanded/duplicated or consumed new icon.");
        controller.Seek(3.5);
        Check(Aoes("trial/voidzone/event/").Length == 0 && Aoes("trial/voidzone/live/").Length == 1,
            "Slickshell dead untargetable live pool failed event handoff.");
        controller.Seek(4.5);
        Check(Aoes("trial/voidzone/").Length == 0, "Slickshell EventState7 pool remained active.");
        controller.Seek(.5);
        Check(Shown("slickshell/bait/").Length == 1, "Slickshell icon/voidzone reverse Seek failed.");
        Isolated();
        stop();
        Console.WriteLine("PASS native Slickshell production packet target bait any source start handoff native main target dedup dynamic cast pool EventState and reverse Seek");

        load(guid, [Header(634), At(0, Initial(), true), Icon(.1, 101),
            At(1, [Actor(101, 0, center - Vector3.UnitX * 4, dead: true)]),
            At(2, [Actor(101, 0, center - Vector3.UnitX * 4)]), Icon(2.1, 101),
            At(3, [], removed: [101]), At(4, [Actor(101, 0, center + Vector3.UnitX * 20)]),
            At(5, [], removed: [1]), At(6, [])]);
        controller.Seek(1.5);
        Check(Shown("slickshell/bait/").Length == 0, "Slickshell dead target bait not removed.");
        controller.Seek(2.05);
        Check(Shown("slickshell/bait/").Length == 0, "Slickshell target resurrection revived old icon.");
        controller.Seek(4.5);
        Check(Shown("slickshell/bait/").Length == 1
            && (Vector3)Value(Aoes("slickshell/bait/").Single(a => ((string)Value(a, "Id")).EndsWith("/display")), "Position") == center - Vector3.UnitX * 4,
            "Slickshell same-ID target rebound frozen reference.");
        controller.Seek(5.5);
        Check(Shown("slickshell/bait/").Length == 0 && Value(Encounter(), "Active") != null,
            "Slickshell primary removal failed bait-only cleanup.");
        stop();
        Check(dangers() == 0 && previews().Length == 0, "Slickshell stopped owner retained output.");
        Console.WriteLine("PASS native Slickshell production permanent dead target cleanup frozen destroyed target same ID generation primary bait cleanup and owner isolation");

        load(guid, [Header(634), At(0, Initial(), true),
            At(.1, [PrimaryZero(true)]), At(.2, [], removed: [1]), Icon(.3, 100),
            At(1, [Actor(1, 0x213A, center, combat: true)]), Icon(1.1, 101),
            TreasureNativeEffect(1.2, 1, 1087, 1, 101, []), Director(2, 0x40000005),
            At(2.1, [Actor(1, 0x213A, center, combat: true)]),
            Director(3, 0x40000011), At(3.1, [Actor(1, 0x213A, center, combat: true)]), At(4, [])]);
        controller.Seek(.5);
        Check(Shown("slickshell/bait/").Length == 0, "Old dead primary supplied active bait.");
        controller.Seek(1.5);
        Check(Shown("slickshell/bait/").Length == 1 && Aoes("trial/voidzone/event/").Length == 1,
            "New primary failed to supply future baits or rebound old source.");
        controller.Seek(2.5);
        Check(dangers() == 0 && previews().Length == 0 && Value(Encounter(), "Active") == null,
            "Director wipe failed unload or allowed stale combat pull.");
        controller.Seek(3.5);
        Check(Value(Encounter(), "Active") != null && Shown("slickshell/bait/").Length == 0
            && Aoes("trial/voidzone/").Length == 0, "Director reopen failed fresh phase or revived old output.");
        controller.Seek(1.5);
        Check(Shown("slickshell/bait/").Length == 1, "Reverse Seek across wipe failed primary generation rebuild.");
        Isolated();
        stop();
        Console.WriteLine("PASS native Slickshell production same ID primary replacement freezes old source future icons rebind Director wipe unload stale pull barrier reopen reverse Seek and replay execution isolation");
    }
}
