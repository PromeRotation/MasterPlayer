using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunSleepOtis(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        object[] Aoes(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix)).ToArray();
        var plugin = runtimeObject.GetType().Assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Value(plugin, "AoeVisibility");
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        object[] Shown(string part) => previews().Concat(Lines()).Where(p => ((string)Value(p, "Id")).Contains(part)).ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        string At(uint territory, double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(territory, t, actors, full: full, removed: removed, party: party);
        string TetherEvent(double t, bool present, uint type, ulong source = 10, ulong target = 100)
            => JsonSerializer.Serialize(new
            {
                t, kind = present ? "Tether" : "TetherCancel",
                type = present ? "PromeRotation.LogSystem.LogSystemTetherEvent" : "PromeRotation.LogSystem.LogSystemTetherCancelEvent",
                data = new { SourceId = source, TargetId = target, TetherId = type, Timestamp = Start.AddSeconds(t) },
            });
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Sleep/Otis replay leaked native facing/action/movement execution.");
        }

        var center = new Vector3(100, 0, 100);
        const string sleep = "masterplayer.quest.a-sleep-disturbed";
        load(sleep, [Header(914), At(914, 0, [Actor(1, 0x2D3C, center), Actor(100, 0, center),
            Actor(101, 0, center + Vector3.UnitX * 10)], true),
            At(914, .1, [Actor(10, 0x233C, center, cast: Cast(19629, 20, center, target: 100)),
                Actor(11, 0x233C, center, cast: Cast(19617, 30, center))]),
            At(914, 1, [Actor(10, 0x233C, center)]), At(914, 2, [Actor(11, 0x233C, center)]), At(914, 3, [])]);
        controller.Seek(.5);
        Check(dangers() == 1 && Shown("cast/").Length == 1 && Damage().Length == 2,
            "Sleep precombat ordinary navigation/dual preview or raidwide mask missing.");
        controller.Seek(1.5);
        Check(dangers() == 0 && Damage().Length == 2, "Sleep cancel also ended independent raidwide.");
        controller.Seek(2.5);
        Check(Damage().Length == 0, "Sleep raidwide actual finish not cleared.");
        controller.Seek(.5);
        manager.SetMechanismEnabled(sleep, "Huaca", false);
        controller.Seek(.6);
        Check(dangers() == 0 && previews().Length == 0, "Sleep mechanism disabled leaked output.");
        manager.SetMechanismEnabled(sleep, "Huaca", true);
        controller.Seek(.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native Sleep production precombat pull ordinary AOE dual preview navigation independent raidwide cancellation mechanism switch reverse Seek owner cleanup");

        var source = center + new Vector3(.123f, 0, .231f);
        object Beam(object[]? tethers = null, bool dead = false) => Actor(10, 0x2D42, source, tethers: tethers, dead: dead);
        object Link(uint type, ulong target) => new { Id = type, TargetId = target };
        party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 2, EntityId = 12ul }];
        load(sleep, [Header(914), At(914, 0, [Actor(1, 0x2D3C, center), Actor(100, 0, center + Vector3.UnitZ * 8),
            Actor(101, 0, center + new Vector3(12, 0, 8)), Beam()], true),
            At(914, .1, [Beam([Link(1, 100)])]), TetherEvent(.1, true, 1),
            At(914, .2, [Actor(12, 0x2D3D, center + new Vector3(5, 0, 8), subKind: 9,
                tethers: [Link(84, 10)])]),
            EffectEvent(.3, 10, 19623, 1, target: 100),
            At(914, 1, [Actor(100, 0, center + new Vector3(3, 0, 8), dead: true)]),
            At(914, 2, [Beam([Link(1, 100)], dead: true)]),
            At(914, 3, [Beam([Link(1, 100)])]), At(914, 4, [Beam()]), At(914, 5, [])]);
        controller.Seek(.5);
        Check(Shown("sleep/beam/").Length == 4 && Aoes("sleep/beam/").Count(a => (bool)Value(a, "Navigate")) == 3
            && Damage().Length == 2, $"Sleep beam previews={Shown("sleep/beam/").Length} navigation={Aoes("sleep/beam/").Count(a => (bool)Value(a, "Navigate"))} damage={Damage().Length}.");
        var rawPosition = (Vector3)Value(Aoes("sleep/beam/").First(a => ((string)Value(a, "Id")).EndsWith("/display")), "Position");
        Check(rawPosition == source, "Sleep dynamic rectangle origin was quantized.");
        controller.Seek(1.5);
        Check(Shown("sleep/beam/").Length == 4, "Dead target cleared beam.");
        controller.Seek(2.5);
        Check(Shown("sleep/beam/").Length == 0, "Dead beam source not hidden.");
        controller.Seek(3.5);
        Check(Shown("sleep/beam/").Length == 4, "Beam source resurrection lost old record.");
        controller.Seek(4.5);
        Check(Shown("sleep/beam/").Length == 2, "Untether removed wrong independent component.");
        controller.Seek(.5);
        Check(Shown("sleep/beam/").Length == 4, "Sleep reverse Seek lost separate tether clocks.");
        Isolated();
        stop();
        Check(Lines().Length == 0, "Sleep owner cleanup retained native line previews.");
        Console.WriteLine("PASS native Sleep production two tether components Player Buddy raw typed receipts dynamic rectangle line own away navigation death revival effect persistence and reverse Seek");

        party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        load(sleep, [Header(914), At(914, 0, [Actor(1, 0x2D3C, center), Actor(100, 0, center + Vector3.UnitZ * 8),
            Actor(101, 0, center + new Vector3(12, 0, 8)), Beam()], true),
            At(914, .1, [Beam([Link(1, 100)])]), TetherEvent(.1, true, 1),
            At(914, .2, [Beam([Link(1, 101)])]), TetherEvent(.2, false, 1),
            TetherEvent(.2, true, 1, target: 101),
            At(914, 1, [], removed: [10]), At(914, 2, [])]);
        controller.Seek(.5);
        Check(Lines().Length == 1 && Aoes("sleep/beam/").Count(a => ((string)Value(a, "Id")).EndsWith("/display")) == 1,
            "Sleep raw target replacement plus typed echoes duplicated beam.");
        controller.Seek(1.5);
        Check(Lines().Length == 0, "Sleep target replacement leaked a duplicate after source destroy.");
        stop();
        Console.WriteLine("PASS native Sleep production raw target replacement retains cancel and gain receipts without duplicate beam or owner leak");

        foreach (var sourceFirst in new[] { true, false })
        foreach (var reverse in new[] { true, false })
        foreach (var type in new uint[] { 1, 84 })
        {
            var enemy = Actor(10, 0x2D42, source);
            var buddy = Actor(12, 0x2D3D, center + Vector3.UnitZ * 8, subKind: 9);
            var rawSource = reverse ? buddy : enemy;
            var rawTarget = reverse ? enemy : buddy;
            var rawSourceId = reverse ? 12ul : 10ul;
            var rawTargetId = reverse ? 10ul : 12ul;
            object[] ordered = sourceFirst ? [rawSource, rawTarget] : [rawTarget, rawSource];
            load(sleep, [Header(914), At(914, 0, new object[] { Actor(1, 0x2D3C, center), Actor(100, 0, center) }
                .Concat(ordered).ToArray(), true),
                TetherEvent(.1, true, type, rawSourceId, rawTargetId),
                At(914, 1, [], removed: [10, 12]), At(914, 2, [])]);
            controller.Seek(.5);
            Check(Lines().Length == 1, "Sleep ordered destruction fixture did not create a beam.");
            controller.Seek(1.5);
            Check(Lines().Length == (sourceFirst ? 0 : 1), "Sleep simultaneous missing actors changed historical Untether lookup order.");
            controller.Seek(.5);
            Check(Lines().Length == 1, "Sleep reverse Seek failed ordered destruction rebuild.");
            stop();
        }
        Console.WriteLine("PASS native Sleep production simultaneous missing actors preserve historical destruction order for both tether types and reverse raw directions");

        party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        center = new(349, 0, -14);
        const string otis = "masterplayer.quest.otis-oathbroken";
        var rows = new List<string>
        {
            Header(1213), At(1213, 0, [Actor(1, 0x4342, center, targetable: false),
                Actor(100, 0, center, combat: true), Actor(101, 0, center + Vector3.UnitX * 10)], true),
        };
        rows.Add(At(1213, .1, Enumerable.Range(10, 5).Select((id, i) =>
            Actor((ulong)id, 0x4347, center + new Vector3(.123f, 0, .234f),
                cast: Cast(38209, 20 - i * 2, center + new Vector3(3, 0, 8), startedAt: .1))).ToArray()));
        rows.Add(At(1213, 1, [Actor(10, 0x4347, center)]));
        rows.Add(At(1213, 2, [Actor(11, 0x4347, center), Actor(12, 0x4347, center)]));
        rows.Add(At(1213, 3, [Actor(13, 0x4347, center), Actor(14, 0x4347, center)]));
        rows.Add(At(1213, 4, []));
        load(otis, rows);
        controller.Seek(.5);
        Check(dangers() == 2 && Shown("otis/rush/").Length == 4, "Otis Rush first four/first two navigation or player pull failed.");
        var firstClock = Aoes("otis/rush/").Select(a => (DateTime)Value(a, "Activation")).ToArray();
        Check(firstClock.Zip(new[] { 20.4, 18.4, 16.4, 14.4 }).All(pair => Math.Abs((pair.First - Start).TotalSeconds - pair.Second) < .00001),
            "Otis insertion order or original clock changed: " + string.Join(",", firstClock.Select(t => (t - Start).TotalSeconds)));
        controller.Seek(1.5);
        Check(dangers() == 2 && Shown("otis/rush/").Length == 4
            && Math.Abs(((DateTime)Value(Aoes("otis/rush/").Last(), "Activation") - Start).TotalSeconds - 12.4) < .00001,
            "Hidden fifth Rush did not promote with original time.");
        controller.Seek(2.5);
        Check(dangers() == 2 && Shown("otis/rush/").Length == 2
            && Aoes("otis/rush/").All(a => a.GetType().GetProperty("Color")!.GetValue(a) == null), "Rush colors failed count-sensitive reset.");
        controller.Seek(3.5);
        Check(dangers() == 0 && Shown("otis/rush/").Length == 0, "Rush true completion left previews/navigation.");
        controller.Seek(.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native Otis production player combat untargetable pull five Rush FIFO four dual previews two navigation original clocks hidden promotion color reset cancel and reverse Seek");

        load(otis, [Header(1213), At(1213, 0, [Actor(1, 0x4342, center), Actor(100, 0, center, combat: true),
            Actor(101, 0, center + Vector3.UnitX * 10)], true),
            At(1213, .1, [Actor(10, 0x233C, center, cast: Cast(38197, 20, center)),
                Actor(11, 0x4345, center, cast: Cast(38205, 10, center))]),
            At(1213, 1, [Actor(10, 0x233C, center, cast: Cast(49999, 2, center, startedAt: 1))]),
            At(1213, 2, [Actor(11, 0x4345, center)]), At(1213, 3, [])]);
        controller.Seek(.5);
        Check(dangers() == 2 && Shown("otis/circle/").Length == 2, "Otis shared circles missing.");
        controller.Seek(1.5);
        Check(dangers() == 1 && Shown("otis/circle/").Length == 1, "Arbitrary replacement Finish did not remove source group.");
        controller.Seek(2.5);
        Check(dangers() == 0, "Shared circles failed second real finish.");
        stop();
        Console.WriteLine("PASS native Otis production grouped circle insertion arbitrary action Finish actual cancel dual preview navigation and independent source cleanup");

        var arena = new Vector3(349, 0, -14);
        load(otis, [Header(1213), At(1213, 0, [Actor(1, 0x4342, arena), Actor(100, 0, arena, combat: true),
            Actor(101, 0, arena + Vector3.UnitX * 10), Actor(102, 0x4348, arena + Vector3.UnitZ * 5, subKind: 5, isEnemy: false)], true),
            At(1213, .1, [Actor(10, 0x233C, arena, cast: Cast(38199, 10, arena, target: 100)),
                Actor(11, 0x233C, arena, cast: Cast(38199, 10, arena, target: 101))]),
            At(1213, 1, [Actor(10, 0x233C, arena)]), At(1213, 2, [])]);
        controller.Seek(.5);
        Check(Aoes("quest/stack/").Any(a => ((string)Value(a, "Id")).EndsWith("/buddy"))
            && Aoes("quest/stack/").Any(a => ((string)Value(a, "Id")).EndsWith("/avoid"))
            && Aoes("quest/stack/").All(a => !((string)Value(a, "Id")).EndsWith("/join"))
            && Shown("quest/stack/").Length == 2 && dangers() == 2,
            "Own stack NPC partner/avoid other mark navigation wrong.");
        controller.Seek(1.5);
        Check(Aoes("quest/stack/").Any(a => ((string)Value(a, "Id")).EndsWith("/join")), "Unmarked stack join missing.");
        stop();
        Console.WriteLine("PASS native Otis production NPC stack own partner radius three avoid other mark radius twelve unmarked union join dual previews and actual navigation");
    }
}
