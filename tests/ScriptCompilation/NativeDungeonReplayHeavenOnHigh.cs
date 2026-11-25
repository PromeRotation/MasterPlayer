using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using ECommons.DalamudServices;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHeavenOnHigh(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(-300, 0, -300);
        object Read(object obj, string name)
        {
            var property = obj.GetType().GetProperty(name, flags);
            return property != null ? property.GetValue(obj)! : obj.GetType().GetField(name, flags)!.GetValue(obj)!;
        }
        var bmr = Assembly.Load("MasterPlayer.MechanicRuntime");
        var bosses = new[]
        {
            (Floor: 10, Territory: 770u, Cfc: 540, Oid: 0x23E1u, Name: "亡者船", Actions: new uint[] { 11878, 11877, 11876 }, Dangers: 2),
            (Floor: 20, Territory: 771u, Cfc: 541, Oid: 0x23E7u, Name: "别蝶", Actions: new uint[] { 11899, 11900, 11901 }, Dangers: 2),
            (Floor: 30, Territory: 772u, Cfc: 542, Oid: 0x2251u, Name: "蝶化身", Actions: new uint[] { 11293, 11291, 11292, 11294 }, Dangers: 4),
            (Floor: 40, Territory: 782u, Cfc: 543, Oid: 0x23E2u, Name: "怖军", Actions: new uint[] { 11905, 11903, 11904, 11902 }, Dangers: 2),
            (Floor: 50, Territory: 773u, Cfc: 544, Oid: 0x23E9u, Name: "牛头", Actions: new uint[] { 11922, 11919, 11921, 11920 }, Dangers: 3),
            (Floor: 60, Territory: 783u, Cfc: 545, Oid: 0x23E4u, Name: "忍冬", Actions: new uint[] { 11925, 11930, 11927, 11926, 11929, 11928 }, Dangers: 4),
            (Floor: 70, Territory: 774u, Cfc: 546, Oid: 0x23EBu, Name: "犬蛊", Actions: new uint[] { 12205, 12203, 12207, 12208, 12206 }, Dangers: 2),
            (Floor: 80, Territory: 784u, Cfc: 547, Oid: 0x23ECu, Name: "锻冶媪", Actions: new uint[] { 11985, 11990, 11989, 11987 }, Dangers: 3),
            (Floor: 90, Territory: 775u, Cfc: 548, Oid: 0x23EFu, Name: "温罗", Actions: new uint[] { 12210, 12213, 12214, 12212, 12216, 12215 }, Dangers: 4),
        };
        object Encounter(string id)
        {
            Check(manager.Running.ContainsKey(id), $"HoH script missing: {id} / {manager.ErrorMessage}");
            object script = Read(manager.Running[id], "Script");
            return script.GetType().BaseType!.GetField("_encounter", flags)!.GetValue(script)!;
        }
        object[] Values(string id, string property) => ((IEnumerable)Read(Encounter(id), property)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(p => ((string)Read(p, "Id")).Contains(prefix)).ToArray();
        object[] Snapshot(string property) => ((IEnumerable)Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), property)).Cast<object>().ToArray();
        void Isolated(string id)
        {
            var hints = Read(Read(manager.Running[id], "Script"), "Hints");
            Check(!(bool)Read(Read(hints, "_facing"), "_hasRequest")
                && !(bool)Read(Read(runtimeObject, "Setting"), "RuntimeMovementHeld")
                && !((IEnumerable)Read(hints, "forbiddenTargets")).Cast<object>().Any(), "HoH replay sent native control.");
        }
        foreach (var boss in bosses)
        {
            object[] party = [new { Slot = 0, EntityId = 100ul }];
            var id = $"masterplayer.hoh.{boss.Floor}";
            var casts = boss.Actions.Select((action, i) => Actor((ulong)(10 + i), 0x233C, center,
                cast: Cast(action, 2, center, MathF.PI / 2, target: 100))).ToArray();
            load(id, [Header(boss.Territory), Frame(boss.Territory, 0,
                [Actor(1, boss.Oid, center), Actor(2, 0x233C, center), Actor(100, 0, center + Vector3.UnitZ * 5, rotation: MathF.PI)], full: true, party: party),
                Frame(boss.Territory, .1, casts.Prepend(Actor(1, boss.Oid, center, combat: true)).ToArray(), party: party),
                System.Text.Json.JsonSerializer.Serialize(new { t = .8, kind = "BattleEnded",
                    type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent", data = new { Timestamp = Start.AddSeconds(.8) } }),
                Frame(boss.Territory, 3, [], party: party),
                Frame(boss.Territory, 4, boss.Actions.Select((_, i) => Actor((ulong)(10 + i), 0x233C, center)).ToArray(), party: party),
                Frame(boss.Territory, 5, [Actor(1, boss.Oid, center, dead: true)], party: party), Frame(boss.Territory, 6, [], party: party)]);
            controller.Seek(.01);
            Check(Read(Encounter(id), "Active") == null && dangers() == 0
                && Shown("hoh/precombat/arena").Length == 1 && Shown("hoh/enemy/").Length == 1,
                "HoH precombat display/navigation scope.");
            controller.Seek(.5);
            var hintDetails = string.Join(",", ((IDictionary)Read(Encounter(id), "_casts")).Values.Cast<object>()
                .Select(c => $"{Read(Read(c, "Rule"), "Action")}:{Read(Read(c, "Rule"), "Hint")}"));
            Check((string)Read(Read(Encounter(id), "Active"), "Name") == boss.Name
                && dangers() == boss.Dangers && Values(id, "PredictedDamage").Length == (boss.Floor == 70 ? 2 : boss.Floor >= 60 ? 1 : 0),
                $"HoH{boss.Floor} ordinary rules/source damage: dangers={dangers()} expected={boss.Dangers}, predictions={Values(id, "PredictedDamage").Length}, casts={hintDetails}.");
            dynamic arena = Read(Encounter(id), "Arena");
            var boundsType = boss.Floor == 90 ? "BossMod.Stormblood.DeepDungeon.HeavenOnHigh.DD90Onra.DD90Onra"
                : $"BossMod.Stormblood.DeepDungeon.HeavenOnHigh.HoHArena{(boss.Floor >= 50 ? 2 : 1)}";
            var bounds = bmr.GetType(boundsType)!
                .GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            var direction = bmr.GetType("BossMod.WDir")!;
            var sourceCenter = Read(bounds, "Center");
            var sourceX = (float)Read(sourceCenter, "X");
            var sourceZ = (float)Read(sourceCenter, "Z");
            Check((Vector3)Read((object)arena, "Position") == new Vector3(sourceX, 0, sourceZ), "HoH source AABB center lost.");
            var points = 0;
            for (var x = -326.137f; x < -274; x += .67f)
                for (var z = -326.173f; z < -274; z += .71f)
                {
                    var point = new Vector3(x, 0, z);
                    var relative = Activator.CreateInstance(direction, [x - sourceX, z - sourceZ])!;
                    Check((bool)arena.Contains(point, 0f) == (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds, [relative])!,
                        "HoH resolved source polygon mismatch.");
                    ++points;
                }
            controller.Seek(3.5);
            Check(dangers() == boss.Dangers && Read(Encounter(id), "Active") != null,
                "HoH battle end or projected deadline cleared observed cast.");
            controller.Seek(4.5);
            Check(dangers() == 0 && !Values(id, "Gazes").Any() && !Values(id, "HeavenOnHighKnockbacks").Any(), "HoH true cast end leaked.");
            controller.Seek(.5);
            manager.SetMechanismEnabled(id, boss.Name, false);
            controller.Seek(.6);
            Check(dangers() == 0 && previews().Length == 0, "HoH disabled owner retained precombat display.");
            manager.SetMechanismEnabled(id, boss.Name, true);
            Isolated(id);
            stop();
            Console.WriteLine($"PASS native HoH{boss.Floor} true territory{boss.Territory} source polygon center {points} points precombat exact rules helper sources observed end source damage only reverse Seek owner replay isolation");
        }
        foreach (var (floor, territory, oid, action) in new[] { (10, 770u, 0x23E1u, 11877u), (40, 782u, 0x23E2u, 11904u) })
        {
            var id = $"masterplayer.hoh.{floor}";
            var origin = center - Vector3.UnitZ * 3;
            load(id, [Header(territory), Frame(territory, 0, [Actor(1, oid, center, combat: true),
                Actor(100, 0, center + Vector3.UnitZ * 5, stackedStatus: 160, statusExpiresAt: Start.AddHours(1))], full: true),
                Frame(territory, .1, [Actor(2, 0x233C, center + Vector3.UnitX * 10, cast: Cast(action, 3, origin)),
                    Actor(3, 0x233C, center, cast: Cast(action, 3, center + Vector3.UnitZ * 40))]),
                Frame(territory, 1, [Actor(2, 0x233C, center - Vector3.UnitX * 10, cast: Cast(action, 3, origin))]),
                EffectEvent(4, 2, action, 1), Frame(territory, 5, []),
                Frame(territory, 6, [Actor(2, 0x233C, center), Actor(3, 0x233C, center)]), Frame(territory, 7, [])]);
            controller.Seek(.5);
            Check(Values(id, "HeavenOnHighKnockbacks").Length == 2 && Shown("hoh/kb/").Length == 4 && dangers() == 0,
                $"HoH immune KB scope: models={Values(id, "HeavenOnHighKnockbacks").Length}, previews={Shown("hoh/kb/").Length}, dangers={dangers()}.");
            var first = Values(id, "HeavenOnHighKnockbacks").First();
            Check((bool)Read(first, "IgnoreImmunes") && (bool)Read(first, "StopAtWall")
                && (float)Read(first, "Distance") == 30 && (Vector3)Read(first, "Origin") == origin, "HoH KB source model.");
            var player = ((IDictionary)Read(Encounter(id), "_actors"))[100ul]!;
            var movements = ((IEnumerable)Encounter(id).GetType().GetMethod("HeavenOnHighMovements", flags)!
                .Invoke(Encounter(id), [player])!).Cast<object>().ToArray();
            var bounds = bmr.GetType("BossMod.Stormblood.DeepDungeon.HeavenOnHigh.HoHArena1")!
                .GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            var sourceCenter = Read(bounds, "Center");
            var directionType = bmr.GetType("BossMod.WDir")!;
            foreach (var movement in movements)
            {
                var from = (Vector3)Read(movement, "Item2");
                var to = (Vector3)Read(movement, "Item3");
                var source = Read(movement, "Item1");
                var locked = (Vector3)Read(source, "Origin");
                var offset = from - locked;
                offset.Y = 0;
                var direction = Vector3.Normalize(offset);
                var sourceOffset = Activator.CreateInstance(directionType,
                    [from.X - (float)Read(sourceCenter, "X"), from.Z - (float)Read(sourceCenter, "Z")])!;
                var sourceDirection = Activator.CreateInstance(directionType, [direction.X, direction.Z])!;
                var ray = (float)bounds.GetType().GetMethod("IntersectRay")!.Invoke(bounds, [sourceOffset, sourceDirection])!;
                var hitbox = (float)Read(player, "HitboxRadius");
                var expected = from + direction * MathF.Min(30, ray - Math.Clamp(hitbox - .499f, .5f - .499f, hitbox - .499f));
                Check(Vector3.Distance(to, expected) < .002f, "HoH wall landing differs from source ray oracle.");
            }
            controller.Seek(5.5);
            Check(Shown("hoh/kb/").Length == 4 && (Vector3)Read(Values(id, "HeavenOnHighKnockbacks").First(), "Origin") == origin,
                "HoH KB followed source or expired.");
            controller.Seek(6.5);
            Check(Shown("hoh/kb/").Length == 0, "HoH KB real Finish retained.");
            Isolated(id);
            stop();
            Console.WriteLine($"PASS native HoH{floor} 30y away ignore immunity lock spell origin not actor sequential wall projections display only effect deadline real Finish");
        }
        foreach (var (floor, territory, oid, action) in new[] { (20, 771u, 0x23E7u, 11900u), (50, 773u, 0x23E9u, 11922u) })
        {
            var id = $"masterplayer.hoh.{floor}";
            load(id, [Header(territory), Frame(territory, 0, [Actor(1, oid, center, combat: true),
                Actor(100, 0, center + Vector3.UnitZ * 60, rotation: MathF.PI)], full: true),
                Frame(territory, .1, [Actor(2, 0x233C, center, cast: Cast(action, 2, center))]),
                EffectEvent(2.2, 2, 11923, 1), Frame(territory, 3, []),
                Frame(territory, 4, [Actor(2, 0x233C, center)]), Frame(territory, 5, [])]);
            controller.Seek(2);
            Check(Shown("hoh/gaze/").Length == 3 && dangers() == 0
                && Encounter(id).GetType().GetMethod("GazeFacing")!.Invoke(Encounter(id), [center + Vector3.UnitZ * 60, MathF.PI]) != null,
                "HoH gaze default range/PR model.");
            controller.Seek(3.5);
            Check(Values(id, "Gazes").Length == 1, "HoH gaze visual or timeout cleared.");
            controller.Seek(4.5);
            Check(Shown("hoh/gaze/").Length == 0, "HoH gaze real Finish retained.");
            Isolated(id);
            stop();
            Console.WriteLine($"PASS native HoH{floor} default10000 gaze eye pupil view arc PR facing model60y visual effect ignored cancel replay isolation");
        }
        foreach (var (floor, territory, oid, zoneOid, radius, countAtSeven) in new[]
        { (40, 782u, 0x23E2u, 0x23E3u, 6f, 0), (50, 773u, 0x23E9u, 0x23EAu, 8f, 1) })
        {
            var id = $"masterplayer.hoh.{floor}";
            var position = center + new Vector3(.123f, 0, .317f);
            load(id, [Header(territory), Frame(territory, 0, [Actor(1, oid, center, combat: true),
                Actor(100, 0, center + Vector3.UnitZ * 5), Actor(3, zoneOid, position, dead: true, eventState: 1)], full: true),
                Frame(territory, 1, [Actor(3, zoneOid, position, dead: true, eventState: 7)]), Frame(territory, 2, [])]);
            controller.Seek(.5);
            Check(Shown("hoh/voidzone/").Length == 1 && Snapshot("HardObstacles").Length == 1 && dangers() == 0
                && (float)Read(Read(Shown("hoh/voidzone/").Single(), "Geometry"), "Radius") == radius, "HoH voidzone hard obstacle.");
            controller.Seek(1.5);
            Check(Shown("hoh/voidzone/").Length == countAtSeven && Snapshot("HardObstacles").Length == countAtSeven,
                "HoH voidzone source state filter.");
            stop();
            Console.WriteLine($"PASS native HoH{floor} persistent voidzone radius{radius} dead actor original position hard obstacle independent eventstate source filter");
        }
        load("masterplayer.hoh.40", [Header(782), Frame(782, 0, [Actor(1, 0x23E2, center, combat: true),
            Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 5), Actor(102, 0, center - Vector3.UnitX * 5)], full: true,
            party: [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 2, EntityId = 102ul }]),
            Frame(782, .1, [Actor(2, 0x233C, center, cast: Cast(11902, 3, center, target: 101)),
                Actor(3, 0x233C, center, cast: Cast(11902, 3, center, target: 101))],
                party: [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 2, EntityId = 102ul }]),
            CancelHoH(.7, 3, 11902), Frame(782, .9, [], removed: [101],
                party: [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 2, EntityId = 102ul }]),
            Frame(782, 1.1, [Actor(101, 0, center + Vector3.UnitX * 15)],
                party: [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 2, EntityId = 102ul }]),
            CancelHoH(1.3, 2, 11902), Frame(782, 2, [])]);
        controller.Seek(.5);
        Check(Shown("quest/spread/").Length == 2 && dangers() == 2
            && Shown("quest/spread/").All(p => (float)Read(Read(p, "Geometry"), "Radius") == 6),
            "HoH spread display6/nav7.");
        controller.Seek(1.2);
        Check(Shown("quest/spread/").Length == 1 && dangers() == 1
            && MathF.Abs(((Vector3)Read(Read(Shown("quest/spread/").Single(), "Geometry"), "Position")).X + 295) < .04f,
            "HoH spread target FIFO or destroyed reference rebound.");
        controller.Seek(1.5);
        Check(Shown("quest/spread/").Length == 0 && dangers() == 0, "HoH cross-source spread Finish retained.");
        stop();
        Console.WriteLine("PASS native HoH40 spread R6 display R7 navigation target FIFO destroy freezes original target same ID new actor real Finish");
        foreach (var (floor, territory, oid, action) in new[]
        { (10, 770u, 0x23E1u, 11877u), (20, 771u, 0x23E7u, 11900u), (40, 782u, 0x23E2u, 11904u), (50, 773u, 0x23E9u, 11922u) })
        {
            var id = $"masterplayer.hoh.{floor}";
            load(id, [Header(territory), Frame(territory, 0, [Actor(1, oid, center, combat: true),
                Actor(2, 0x233C, center), Actor(100, 0, center + Vector3.UnitZ * 5)], full: true),
                CastEvent(.1, 2, action, 3, center), CastEvent(.15, 2, action, 3, center + Vector3.UnitX * 4),
                Frame(territory, .6, [Actor(2, 0x233C, center,
                    cast: Cast(action, 3, center + Vector3.UnitZ * 4))]),
                CancelHoH(.8, 2, action, .5), Frame(territory, 1, []),
                CancelHoH(1.4, 2, action), Frame(territory, 2, [])]);
            controller.Seek(.2);
            var pending = ((IDictionary)Read(Encounter(id), "_casts"))[2ul]!;
            Check((Vector3)Read(pending, "Location") == center + Vector3.UnitX * 4, "HoH packet correction lost.");
            if (action is 11877 or 11904)
                Check((Vector3)Read(Values(id, "HeavenOnHighKnockbacks").Single(), "Origin") == center + Vector3.UnitX * 4,
                    "HoH packet corrected KB kept old origin.");
            controller.Seek(1.1);
            Check(((IDictionary)Read(Encounter(id), "_casts")).Count == 1
                && (DateTime)Read(((IDictionary)Read(Encounter(id), "_casts"))[2ul]!, "FinishEventBoundary") == Start.AddSeconds(.6),
                "HoH queued stale cancel cleared raw replacement.");
            controller.Seek(1.5);
            Check(((IDictionary)Read(Encounter(id), "_casts")).Count == 0
                && Values(id, "HeavenOnHighKnockbacks").Length == 0 && Values(id, "Gazes").Length == 0, "HoH real cancel not consumed.");
            stop();
            Console.WriteLine($"PASS native HoH{floor} packet corrected origin raw replacement generation boundary queued stale cancel current cancel");
        }
        load("masterplayer.hoh.10", [Header(770), Frame(770, 0, [Actor(1, 0x23E1, center, combat: true),
            Actor(2, 0x233C, center, dead: true, cast: Cast(11877, 4, center, startedAt: -10)),
            Actor(100, 0, center + Vector3.UnitZ * 5)], full: true),
            System.Text.Json.JsonSerializer.Serialize(new { t = .05, kind = "CastStart",
                type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0f,
                data = new { SourceId = 2ul, TargetId = 0ul, ActionId = 11877, CastTime = 4f, EffectPosition = Pose(center),
                    Timestamp = Start.AddSeconds(-10) } }), Frame(770, 2, [])]);
        controller.Seek(.01);
        var bootstrapEnd = (DateTime)Read(Read(Values("masterplayer.hoh.10", "HeavenOnHighKnockbacks").Single(), "Cast"), "FinishAt");
        Check(Math.Abs((bootstrapEnd - Start).TotalSeconds - ((4f + .3f) - 4f)) < .00001, "HoH late bootstrap source clock.");
        controller.Seek(1);
        Check((DateTime)Read(Read(Values("masterplayer.hoh.10", "HeavenOnHighKnockbacks").Single(), "Cast"), "FinishAt") == bootstrapEnd,
            "HoH bootstrap activation slid.");
        stop();
        Console.WriteLine("PASS native HoH10 cold overdue dead helper source NPC elapsed cap once stable observation retains activation");
        RunHeavenOnHighSpawns(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunHeavenOnHighKenko(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        RunHeavenOnHighFloorContinuity(managerObject, controllerObject, load, stop);
    }

    private static string CancelHoH(double t, ulong source, uint action, double? eventTime = null) => System.Text.Json.JsonSerializer.Serialize(new
    {
        t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
        data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(eventTime ?? t) },
    });

    private static void RunHeavenOnHighFloorContinuity(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop)
    {
        dynamic controller = controllerObject;
        var assembly = ((IEnumerable)((dynamic)managerObject).Scripts).Cast<object>().First().GetType().Assembly;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var hostType = assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        object Read(object obj, string name)
        {
            var property = obj.GetType().GetProperty(name, flags | BindingFlags.Public);
            return property != null ? property.GetValue(obj)! : obj.GetType().GetField(name, flags | BindingFlags.Public)!.GetValue(obj)!;
        }
        var center = new Vector3(-300, 0, -300);
        foreach (var (floor, territory, oid) in new[]
        { (10, (ushort)770, 0x23E1u), (20, (ushort)771, 0x23E7u),
            (30, (ushort)772, 0x2251u), (40, (ushort)782, 0x23E2u), (50, (ushort)773, 0x23E9u),
            (60, (ushort)783, 0x23E4u), (70, (ushort)774, 0x23EBu),
            (80, (ushort)784, 0x23ECu), (90, (ushort)775, 0x23EFu) })
        {
            load($"masterplayer.hoh.{floor}", [Header(territory), Frame(territory, 0,
                [Actor(1, oid, center), Actor(100, 0, center)], full: true),
                Frame(territory, 1, [Actor(1, oid, center, combat: true)]),
                Frame(territory, 2, [Actor(1, oid, center, dead: true)]), Frame(territory, 3, [])]);
            var host = RuntimeHelpers.GetUninitializedObject(hostType);
            void Set(string name, object value) => hostType.GetField(name, flags)!.SetValue(host, value);
            Set("_replayTerritory", territory);
            // ModulesEnabled selects the replay territory when a session exists.
            // No source session is ticked or consumed by the native scripts.
            var sessionField = hostType.GetField("_replaySession", flags)!;
            var session = RuntimeHelpers.GetUninitializedObject(sessionField.FieldType);
            Set("_replaySession", session);
            try
            {
                foreach (var t in new[] { 0d, 1.1, 2.1 })
                {
                    controller.Seek(t);
                    Check(!(bool)Read(host, "ModulesEnabled")
                        && (bool)hostType.GetMethod("IsReplaySuppressed", flags)!.Invoke(host, null)!,
                        "Native HoH region left BMR output enabled.");
                    Check(!(bool)Read(host, "HasActiveModule"), "Disabled bridge suppressed native generic dodge.");
                }
            }
            finally
            {
                Set("_replaySession", null!);
                stop();
            }
            Console.WriteLine($"PASS native HoH{floor} bridge disabled before fight during Boss after death native region ownership");
        }
    }
}
