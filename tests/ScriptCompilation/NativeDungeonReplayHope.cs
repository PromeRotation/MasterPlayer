using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHope(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.quest.hope-on-the-waves";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [null])!).Cast<string>().ToArray();
        object[] Adds() => ((IEnumerable)Value(Encounter(), "HopeAdds")).Cast<object>().ToArray();
        var center = new Vector3(473.25f, 0, 751.75f);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(757, t, actors, full: full, removed: removed, party: party);
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
                "Hope replay leaked facing/action/movement execution.");
        }
        object PrimaryZero() => new { Id = 1ul, Oid = 0x21B1u, Kind = 2, Position = Pose(center),
            Hp = 0u, MaxHp = 1000u, Dead = true, Targetable = true, InCombat = true, IsEnemy = true };
        string BattleEnd(double t) => JsonSerializer.Serialize(new
        {
            t, kind = "BattleEnded", type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent",
            data = new { Timestamp = Start.AddSeconds(t) },
        });

        load(guid, [Header(757), At(0, [Actor(1, 0x21B1, center),
            Actor(100, 0, center + Vector3.UnitZ), Actor(101, 0, center + Vector3.UnitX * 12)], true),
            At(.1, [PrimaryZero()]), At(.2,
            [
                Actor(10, 0x233C, center, targetable: false, cast: Cast(10823, 20, center, rotation: .4f, startedAt: .2)),
                Actor(11, 0x2115, center, cast: Cast(9428, 30, center, startedAt: .2)),
                Actor(12, 0x2115, center, cast: Cast(10929, 30, center, startedAt: .2)),
                Actor(13, 0x21B7, center, cast: Cast(10893, 30, center, startedAt: .2)),
                Actor(14, 0x21B1, center, cast: Cast(9425, 30, center, startedAt: .2)),
                Actor(15, 0x21B6, center, cast: Cast(10930, 30, center, startedAt: .2)),
                Actor(16, 0x21B1, center, cast: Cast(10928, 30, center, startedAt: .2)),
                Actor(18, 0x9999, center + Vector3.UnitX, isEnemy: true),
                Actor(19, 0x9999, center + Vector3.UnitZ, isEnemy: false),
            ]), At(.5, [], removed: [1]), BattleEnd(.6),
            At(1, [Actor(17, 0x233C, center, cast: Cast(10929, 30, center + Vector3.UnitX, startedAt: 1))]),
            EffectEvent(1.2, 10, 10823, 1),
            At(2, [Actor(10, 0x233C, center)]), At(4, [])]);
        controller.Seek(.05);
        Check(dangers() == 0 && Value(Encounter(), "Active") == null, "Idle Hope primary pulled early.");
        controller.Seek(.3);
        Check(dangers() == 6 && Shown("cast/").Length == 6
            && Hints().Any(h => h.StartsWith("Kill before detonation! "))
            && !((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().Any(),
            "Hope six actual AOE components or hint-only detonation failed.");
        Check(Shown("job-quest/enemy/18").Length == 1 && Shown("job-quest/enemy/19").Length == 0,
            "Hope all-enemy marker scope inferred hostility or guessed OIDs.");
        dynamic arena = Value(Encounter(), "Arena");
        var sourceType = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MasterPlayer.MechanicRuntime")
            .GetType("BossMod.Stormblood.Quest.MSQ.HopeOnTheWaves.ImperialCenturion")!;
        dynamic oracle = sourceType.GetField("arena", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
        var sourceCenter = new Vector3((float)oracle.Center.X, 0, (float)oracle.Center.Z);
        var direction = sourceType.Assembly.GetType("BossMod.WDir")!;
        Check(Vector3.Distance((Vector3)arena.Position, sourceCenter) < .0001f, "Hope source rotated ellipse AABB center changed.");
        var compared = 0;
        for (var x = 435.17f; x < 511; x += 1.1f)
        for (var z = 715.13f; z < 788; z += 1.1f)
        {
            var expected = (bool)((object)oracle).GetType().GetMethod("Contains")!.Invoke((object)oracle,
                [Activator.CreateInstance(direction, [x - sourceCenter.X, z - sourceCenter.Z])])!;
            Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, $"Hope source ellipse mismatch at {x},{z}.");
            ++compared;
        }
        controller.Seek(1.5);
        Check(dangers() == 7 && Value(Encounter(), "Active") != null,
            "Hope primary death/destroy/BattleEnded ended regional phase or Effect consumed ordinary cast.");
        controller.Seek(2.5);
        Check(dangers() == 6, "Hope real observed Finish failed to cancel only its source.");
        manager.SetMechanismEnabled(guid, "Imperial Centurion", false);
        controller.Seek(2.6);
        Check(dangers() == 0 && previews().Length == 0, "Disabled Hope retained native output.");
        manager.SetMechanismEnabled(guid, "Imperial Centurion", true);
        controller.Seek(.3);
        Isolated();
        stop();
        Console.WriteLine($"PASS native Hope production regional lifecycle six dual-view AOEs helper and targetable scope hint-only detonate native markers source rotated ellipse oracle {compared} points cancellation reverse Seek and owner cleanup");

        var lb1 = center + Vector3.UnitX * 5;
        var lb2 = center + Vector3.UnitZ * 5;
        object Enemy(ulong id, uint oid, object? cast = null) => new
        {
            Id = id, Oid = oid, Kind = 2, Position = Pose(center), Hp = 100u, MaxHp = 100u,
            Targetable = true, InCombat = true, IsEnemy = true, ObjectIndex = (int)id * 2,
            Hostile = true, TargetId = 100ul, Cast = cast,
        };
        load(guid, [Header(757), At(0, [Actor(1, 0x21B1, center, combat: true),
            Actor(7, 0x21AC, center, isEnemy: false), Actor(8, 0x21AC, center, isEnemy: false,
                cast: Cast(10894, 10, lb2, startedAt: 0)), Enemy(9, 0x2114),
            Enemy(10, 0x21B6, Cast(10928, 30, center, startedAt: 0)),
            Actor(100, 0, center, classJob: 19), Actor(101, 0, center + Vector3.UnitX * 12)], true),
            At(.2, [Actor(7, 0x21AC, center, isEnemy: false, cast: Cast(10894, 10, lb1, startedAt: .2))]),
            At(.4, [], removed: [7]), Director(1, 0x40000005), At(1.1, [Actor(1, 0x21B1, center, combat: true)]),
            Director(2, 0x40000011), At(2.1, [Actor(1, 0x21B1, center, combat: true)]), At(3, [])]);
        controller.Seek(.1);
        Check(Adds().Length == 0, "Hope later Alphinaud bypassed first-actor rule.");
        controller.Seek(.3);
        Check(Adds().Length == 1 && (Vector3)Value(Adds()[0], "Position") == lb1
            && (bool)Value(Adds()[0], "ShouldBeTanked") && !(bool)Value(Adds()[0], "PreferProvoking"),
            "Hope LB intention lost target, desired position or exact tank/provoke flags.");
        var aoes = ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        Check(!aoes.Any(a => ((string)Value(a, "Id")).StartsWith("enemy/tank/")),
            "Hope intention unexpectedly added automatic tank navigation.");
        controller.Seek(.5);
        Check(Adds().Length == 1 && (Vector3)Value(Adds()[0], "Position") == lb2,
            "Hope destroyed first NPC failed next first-actor/current-cast lookup.");
        controller.Seek(1.5);
        Check(Value(Encounter(), "Active") == null && dangers() == 0 && previews().Length == 0,
            "Hope wipe repulled stale actors.");
        controller.Seek(2.5);
        Check(Value(Encounter(), "Active") != null, "Hope Director unlock failed valid pull.");
        controller.Seek(.3);
        Check(Adds().Length == 1 && (Vector3)Value(Adds()[0], "Position") == lb1, "Hope reverse Seek retained future NPC identity.");
        Isolated();
        stop();
        Check(dangers() == 0 && previews().Length == 0, "Hope stopped owner retained outputs.");
        Console.WriteLine("PASS native Hope production first Alphinaud identity current LB tank intent without extra navigation Director wipe stale pull unlock reverse Seek and replay execution isolation");

        load(guid, [Header(757), At(0, [Actor(1, 0x21B1, center, combat: true),
            Actor(7, 0x21AC, center, isEnemy: false), Actor(8, 0x21AC, center, isEnemy: false),
            Enemy(9, 0x2114), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 12)], true),
            At(.2, [Actor(20, 0x9999, center), Actor(21, 0x21AC, center, isEnemy: false,
                cast: Cast(10894, 10, lb1, startedAt: .2))], removed: [7]),
            At(.4, [Actor(8, 0x21AC, center, isEnemy: false, cast: Cast(10894, 10, lb2, startedAt: .4))]),
            At(1, [])]);
        controller.Seek(.3);
        Check(Adds().Length == 0, "Hope NPC-only subset stole global dictionary slot occupied by unrelated actor.");
        controller.Seek(.5);
        Check(Adds().Length == 1 && (Vector3)Value(Adds()[0], "Position") == lb2,
            "Hope global slot reuse failed first persistent Alphinaud selection.");
        controller.Seek(.3);
        Check(Adds().Length == 0, "Hope reverse Seek retained future LB after global actor slot reuse.");
        stop();
        Console.WriteLine("PASS native Hope production unrelated actor reuses destroyed global slot before later Alphinaud first persistent NPC identity and reverse Seek");
    }
}
