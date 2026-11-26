using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunZadnorTime(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const string mechanism = "复制魔人——第四军团贝利亚斯";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var jsonOptions = (JsonSerializerOptions)assembly.GetType("MasterPlayer.Replay.ReplayJson")!
            .GetField("Options", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var center = new Vector3(-550, 0, 0);
        object[] roster = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        object Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } p) return p.GetValue(obj)!;
                if (type.GetField(name, flags) is { } f) return f.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Script() => Read(manager.Running[id], "Script");
        object Model() => Read(Script(), "encounter");
        int Count(string name) => ((IEnumerable)Read(Model(), name)).Cast<object>().Count();
        object[] Shown(string key) => previews().Where(p => ((string)Read(p, "Id")).Contains(key)).ToArray();
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        string World(double t, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(975, t, actors, full, removed, roster, sourcePlayerId: 100);
        object Spell(uint action, double at, Vector3? origin = null)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 4, origin ?? center, .4f, 100, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 4;
            return node;
        }
        object Primary(object? cast = null, bool targetable = true) =>
            Actor(1, 0x31C8, center, combat: true, cast: cast, isEnemy: true, targetable: targetable);
        object[] PartyActors() => [Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitZ * 20)];
        void FinishExpectedFifoError()
        {
            Check(((string)manager.ErrorMessage).Contains("TimeEruptionBombReproduce")
                && ((string)manager.ErrorMessage).Contains("空队列"),
                "CE52 expected FIFO failure must remain in the actual script manager diagnostic.");
            stop();
            // LogError deliberately persists across sessions. Acknowledge only
            // this asserted failure so later unrelated catalog tests stay isolated.
            manager.SetError("");
        }
        Vector3 ClockPosition(int i) => center + new Vector3((i % 3 - 1) * 20, 0, (i / 3 - 1) * 20);
        object Clock(int i, uint? state = null) => Actor((ulong)(10 + i), 0x1EB17A, ClockPosition(i), eventAnimation: state);
        string Animation(double t, ulong source, uint state) => JsonSerializer.Serialize(new
        {
            t, kind = "EObjAnimation", type = "PromeRotation.LogSystem.LogSystemEObjAnimationEvent",
            data = new { SourceId = source, Param1 = (ushort)(state >> 16), Param2 = (ushort)state, Timestamp = Start.AddSeconds(t) },
        });
        string TimeEffectRecord(double t, ulong source, uint action, uint sequence, Vector3 position) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = source, ActionId = action, ActionType = 1, GlobalSequence = sequence,
                MainTargetId = 100UL, Targets = Array.Empty<ulong>(), TargetPosition = Pose(position), Timestamp = Start.AddSeconds(t) },
        });
        void Seek(double time)
        {
            controller.Seek(time);
            Check((int)controller.RouterErrorCount == 0, "CE52 replay router: " + controller.StatusMessage);
        }
        void ApplyWithoutUpdate(double time, IEnumerable<object> actors)
        {
            var world = Read(Script(), "ReplayWorld");
            using var json = JsonDocument.Parse(World(time, actors));
            var data = json.RootElement.GetProperty("data");
            var frame = JsonSerializer.Deserialize(data.GetRawText(),
                assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!, jsonOptions)!;
            var decoded = ((IEnumerable)Read(frame, "Actors")).Cast<object>().ToArray();
            foreach (var actor in data.GetProperty("Actors").EnumerateArray())
            {
                var actual = decoded.Single(a => (ulong)Read(a, "Id") == actor.GetProperty("Id").GetUInt64());
                Check((Vector3)Read(actual, "Position") == ReadPosition(actor.GetProperty("Position")),
                    "CE52 manual snapshot preserves actor position");
                if (actor.GetProperty("Cast").ValueKind != JsonValueKind.Null)
                    Check((Vector3)Read(Read(actual, "Cast"), "Location")
                        == ReadPosition(actor.GetProperty("Cast").GetProperty("Location")),
                        "CE52 manual snapshot preserves cast location");
            }
            world.GetType().GetMethod("Apply", flags)!.Invoke(world, [frame]);
            world.GetType().GetProperty("CurrentTime")!.SetValue(world, Start.AddSeconds(time));
        }
        Vector3 ReadPosition(JsonElement value) => new(value.GetProperty("X").GetSingle(),
            value.GetProperty("Y").GetSingle(), value.GetProperty("Z").GetSingle());
        void Raise(LogSystemEvent ev)
        {
            var router = Read(Read(manager.Running[id], "Context"), "_router");
            router.GetType().GetMethod("Raise")!.Invoke(router, [ev]);
            Check((int)Read(router, "ErrorCount") == 0, "CE52 fixed-frame router callback.");
        }
        void RaiseNative(string record)
        {
            using var json = JsonDocument.Parse(record);
            var data = json.RootElement.GetProperty("data");
            var ev = (LogSystemEvent)JsonSerializer.Deserialize(data.GetRawText(),
                assembly.GetType("MasterPlayer.Scripting.NativeActionEffectEvent")!, jsonOptions)!;
            Check((Vector3?)Read(ev, "TargetPosition") == ReadPosition(data.GetProperty("TargetPosition")),
                "CE52 manual packet preserves actual target coordinates");
            Raise(ev);
        }
        void Update() => Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.02f]);
        var records = new List<string> { Header(975), World(0, [Primary(), .. PartyActors(),
            Actor(2, 0x233C, center, subKind: 5), Actor(30, 0x233C, center, subKind: 5),
            .. Enumerable.Range(0, 9).Select(i => Clock(i))], true) };
        records.AddRange(Enumerable.Range(0, 9).Select(i => Animation(.1 + i * .01, (ulong)(10 + i),
            i < 7 ? 0x10002u : 0x10020u)));
        records.AddRange([
            World(1, [Actor(2, 0x233C, center, cast: Spell(23955, 1), subKind: 5)]),
            World(2, [Actor(2, 0x233C, center, cast: Spell(23959, 2), subKind: 5),
                Actor(30, 0x233C, center, cast: Spell(23960, 2), subKind: 5)]),
            World(3, [Primary(Spell(23957, 3))]),
            World(4, [Actor(20, 0x1EB17B, center + Vector3.UnitX * 10, rotation: 1.2f),
                Actor(21, 0x1EB1D4, center - Vector3.UnitX * 10)]),
            World(5, [Actor(20, 0x1EB17B, center + Vector3.UnitX * 13.123f, dead: true, rotation: 2.3f)]),
            World(6, [], removed: [20]), TimeEffectRecord(7, 2, 23958, 52, center),
            World(8, [Actor(2, 0x233C, center + Vector3.UnitX * 27, subKind: 5)]),
            TimeEffectRecord(9, 1, 23959, 53, center), TimeEffectRecord(10, 2, 23956, 54, ClockPosition(0)),
            World(11, [Actor(30, 0x233C, center, subKind: 5)]),
            TimeEffectRecord(12, 10, 23956, 55, center + Vector3.UnitZ * 27), World(20, []),
        ]);
        load(id, records);
        Seek(.5);
        Check(Count("timeClocks") == 9 && Shown("bozja/time/safe/").Length == 2 && dangers() == 0 && Goals() == 0,
            "CE52 hot EAnim nine clocks produce safe-only background before StartTime.");
        Check(Shown("bozja/time/safe/").All(p => (bool)Read(p, "Safe")),
            "CE52 safe background tagged safe in actual visibility output.");
        Seek(1.1);
        Check(dangers() == 7 && Shown("bozja/aoe/").Length == 7 && Shown("bozja/time/safe/").Length == 2 && Goals() == 0,
            "CE52 StartTime publishes seven clocks without safe goals.");
        Seek(2.1); Check(dangers() == 2 && Shown("bozja/time/safe/").Length == 2, "CE52 cyclone hides clocks but not standard Eruption.");
        Seek(3.1); Check(dangers() == 1 && Shown("bozja/time/safe/").Length == 2, "CE52 empty bombs suppress cyclone and clocks.");
        Seek(4.1);
        var bomb = Shown("bozja/aoe/").Select(p => Read(p, "Geometry")).Single(g => g.GetType().Name == "FanGeometry");
        Check(dangers() == 2 && (float)Read(bomb, "Radius") == 60 && (float)Read(bomb, "AngleRadians") == MathF.PI / 2,
            "CE52 only Bomb1 yields real cone R60 90 degrees.");
        Seek(5.1);
        bomb = Shown("bozja/aoe/").Select(p => Read(p, "Geometry")).Single(g => g.GetType().Name == "FanGeometry");
        Check((Vector3)Read(bomb, "Position") == center + Vector3.UnitX * 13.123f && dangers() == 2,
            "CE52 dead cached original bomb uses current raw position.");
        Seek(6.1); Check(dangers() == 1, "CE52 empty cache still suppresses other main mechanics.");
        Seek(7.1); Check(dangers() == 2, "CE52 any caster bomb Effect promotes stored cyclone.");
        Seek(8.1); Check(dangers() == 2, "CE52 ordinary cyclone Finish cannot remove.");
        Seek(9.1); Check(dangers() == 8 && Shown("bozja/time/safe/").Length == 2, "CE52 cross-caster FIFO Effect promotes clocks.");
        Seek(10.1);
        Check(Count("timeClocks") == 9 && dangers() == 8 && Shown("bozja/time/safe/").Length == 0,
            "CE52 unmatched Effect caster clears safe background but ignores target position.");
        Seek(12.1); Check(Count("timeClocks") == 8 && dangers() == 6, "CE52 matching Effect removes one clock after standard Finish.");
        Seek(20); Check(dangers() == 6, "CE52 clock predictions have no TTL cleanup.");
        Seek(1.1); Check(dangers() == 7 && Shown("bozja/time/safe/").Length == 2, "CE52 reverse Seek reconstructs animations.");
        manager.SetMechanismEnabled(id, mechanism, false); Seek(1.1);
        Check(dangers() == 0 && previews().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "CE52 mechanism disable releases both drawing and navigation owner.");
        manager.SetMechanismEnabled(id, mechanism, true); Seek(1.1);
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest")
            && (ulong)Read(Read(Script(), "Hints"), "target") == 0
            && !(bool)Read(Read(Script(), "Hints"), "holdActions"), "CE52 replay does not execute.");
        stop();
        Console.WriteLine("PASS native CE52 EAnim nine clock safe-only background tri-state priority empty bomb original cache FIFO Effect real Finish reverse Seek owner replay");

        load(id, [Header(975), World(0, [Primary(), .. PartyActors(),
                Actor(2, 0x233C, center, cast: Spell(23955, 0), subKind: 5), Clock(0, 0x10002)], true),
            World(1, [Clock(0, 0x10002)]), World(2, [Clock(0, 0x10020)]),
            Animation(2, 10, 0x10002), Animation(2.5, 10, 0x10020), World(3, []),
            World(4, [Clock(0)]), World(5, [Clock(0, 0x10020)]), World(6, [])]);
        Seek(1.1);
        Check(Count("timeClocks") == 0 && dangers() == 0, "CE52 initial snapshot animation is lost before activation, unchanged value not replayed.");
        Seek(2.1);
        Check(Count("timeClocks") == 2 && dangers() == 2, "CE52 changed snapshot and real packet both consumed, without shared dedup.");
        Check(((IEnumerable)Read(Model(), "timeClocks")).Cast<object>().Select(c => (double)Read(c, "Item4"))
            .SequenceEqual(new[] { 11.7d, 9.7d }), "CE52 playback updates after each record, so prior world record flushes first.");
        Seek(3.1);
        Check(Count("timeClocks") == 3 && dangers() == 1, "CE52 repeated same animation packet adds, unchanged frame does not.");
        Seek(5.1);
        Check(Count("timeClocks") == 4 && dangers() == 2, "CE52 explicit nullable snapshot reset permits same state again.");
        stop();
        Console.WriteLine("PASS native CE52 actual snapshot and packet EAnim two independent routes cold loss same packet repeats nullable reset");
        load(id, [Header(975), World(0, [Primary(), .. PartyActors(),
                Actor(2, 0x233C, center, cast: Spell(23955, 0), subKind: 5), Clock(0, 0x10002)], true), World(20, [])]);
        Seek(.1);
        ApplyWithoutUpdate(2, [Clock(0, 0x10020)]);
        Raise(new LogSystemEObjAnimationEvent { SourceId = 10, Param1 = 1, Param2 = 2 });
        Check(Count("timeClocks") == 0, "CE52 pending packet and changed snapshot await same Framework pass.");
        Update();
        Check(((IEnumerable)Read(Model(), "timeClocks")).Cast<object>().Select(c => (double)Read(c, "Item4"))
            .SequenceEqual(new[] { 9.7d, 11.7d }) && dangers() == 2,
            "CE52 fixed Framework pass queues real packet before snapshot animation, matching source Feed.");
        stop();
        Console.WriteLine("PASS native CE52 actual fixed Framework batch preserves packet then snapshot animation order without shared dedup");

        load(id, [Header(975), World(0, [Primary(targetable: false), .. PartyActors(),
                Actor(2, 0x233C, center, cast: Spell(23960, 0), subKind: 5)], true),
            World(1, [Primary()]), World(2, [])]);
        Seek(.1);
        var region = Read(Script(), "region");
        Check(((IEnumerable)Read(region, "Loaded")).Cast<object>().Count() == 0 && dangers() == 0,
            "CE52 onlyLoadIfTargetable keeps module pending.");
        Seek(1.1); Check((bool)Read(Script(), "EncounterActive") && dangers() == 1, "CE52 targetable pending module cold activation.");
        Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = 23960 })
            && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = 23960 }),
            "CE52 generic suppression source gate.");
        stop();
        Console.WriteLine("PASS native CE52 true pending onlyLoadIfTargetable source suppression then cold standard activation");

        load(id, [Header(975), World(0, [Primary(), .. PartyActors(),
                Actor(2, 0x233C, center, cast: Spell(23955, 0), subKind: 5),
                Actor(30, 0x233C, center, cast: Spell(23960, 0), subKind: 5), Clock(0)], true),
            World(20, [])]);
        Seek(.1);
        Raise(new LogSystemEObjAnimationEvent { SourceId = 10, Param1 = 1, Param2 = 2 }); Update();
        Check(dangers() == 2, "CE52 failure case initially has full standard and clock navigation.");
        ApplyWithoutUpdate(1, [Clock(0, 0x10020)]);
        RaiseNative(TimeEffectRecord(1, 1, 23959, 56, center));
        Update();
        Check(((string)Read(Model(), "TimeSequenceError")).Contains("空队列") && dangers() == 0 && previews().Length == 0
            && Goals() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null
            && ((IList)Read(Script(), "pendingEvents")).Count == 2 && Count("Casts") == 1 && Count("timeClocks") == 1,
            "CE52 malformed empty FIFO is diagnosed, retained for retry, all output withdrawn without destroying history.");
        ApplyWithoutUpdate(2, [Actor(20, 0x233C, center, cast: Spell(23959, 2), subKind: 5)]);
        Update();
        Check(Read(Model(), "TimeSequenceError") == null && dangers() == 3 && Count("timeClocks") == 2
            && ((IList)Read(Script(), "pendingEvents")).Count == 0,
            "CE52 retained Effect can consume new current cyclone and recover source-like on next frame.");
        FinishExpectedFifoError();
        Console.WriteLine("PASS native CE52 empty FIFO explicit failure withdraws all output retains event history retry recovers on real new cast");

        load(id, [Header(975), World(0, [Primary(), .. PartyActors(),
                Actor(2, 0x233C, center, cast: Spell(23955, 0), subKind: 5),
                .. Enumerable.Range(0, 9).Select(i => Actor((ulong)(10 + i), 0x1EB17A,
                    center + new Vector3(i * 5, 0, 20)))], true), World(20, [])]);
        Seek(.1);
        for (var i = 0; i < 8; ++i)
            Raise(new LogSystemEObjAnimationEvent { SourceId = (ulong)(10 + i), Param1 = 1, Param2 = 2 });
        Update();
        void RaiseEffect(uint action, uint sequence)
        {
            RaiseNative(TimeEffectRecord(1, 1, action, sequence, center));
        }
        RaiseEffect(23959, 57); Update();
        RaiseEffect(23956, 58); Update();
        ApplyWithoutUpdate(2, [Actor(20, 0x233C, center, cast: Spell(23959, 2), subKind: 5),
            Actor(18, 0x1EB17A, center + new Vector3(40, 0, 20), eventAnimation: 0x10002)]);
        Update();
        Check(Count("timeClocks") == 9 && Shown("bozja/time/safe/").Length == 2
            && ((IList)Read(Script(), "pendingEvents")).Count == 0,
            "CE52 retry consumes retained cyclone then clear-safe Effect before ninth snapshot animation.");
        FinishExpectedFifoError();
        Console.WriteLine("PASS native CE52 retry queue retains cross Effect and ninth snapshot ordering with two safe backgrounds");

        var hambi = new Vector3(-390, 0, 230);
        load(id, [Header(975), World(0, [Actor(1, 0x319A, hambi, combat: true, isEnemy: true),
                Actor(100, 0, hambi), Actor(101, 0, hambi + Vector3.UnitZ * 20),
                Actor(2, 0x233C, hambi, cast: Spell(23568, 0, hambi), subKind: 5)], true),
            World(1, [Actor(10, 0x31C8, center, combat: true, isEnemy: true),
                .. PartyActors()]), World(20, [])]);
        Seek(1.1);
        Check((string)Read(Read(Model(), "Definition"), "Key") == "CE51ThereWouldBeBlood"
            && dangers() > 0, "CE52 second active module retains first CE51 selected at equal source priority.");
        RaiseEffect(23959, 59); Update();
        Check(dangers() == 0 && previews().Length == 0
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null
            && ((string)Read(Script(), "MechanicHintsText")).Contains("TimeEruptionBombReproduce")
            && (ulong)Read(Script(), "PlannedTargetId") == 0 && Read(Script(), "PlannedFacing") == null,
            "CE52 error in nonselected module withdraws entire selected region output and controls.");
        FinishExpectedFifoError();
        Console.WriteLine("PASS native CE52 nonselected module FIFO error withdraws selected CE51 drawing navigation boundary control");

        foreach (var action in new uint[] { 23960, 23961, 23962, 23963 })
        {
            load(id, [Header(975), World(0, [Primary(), .. PartyActors(),
                Actor(2, 0x233C, center, cast: Spell(action, 0), subKind: 5)], true),
                World(10, []), World(11, [Actor(2, 0x233C, center, subKind: 5)]), World(12, [])]);
            Seek(10.1);
            if (action == 23960)
            {
                Check(dangers() == 1 && (float)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "Radius") == 8,
                    "CE52 actual R8 circle.");
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                    && Vector3.Distance((Vector3)Read((object)result, "TargetPosition"), center) >= 8,
                    "CE52 actual solver R8 escape in true square.");
            }
            else if (action is 23961 or 23962)
            {
                var damage = ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Single();
                Check((ulong)Read(damage, "Mask") == (action == 23961 ? 1UL : 3UL) && dangers() == 0
                    && (string)Read(Script(), "MechanicHintsText") == (action == 23961 ? "Tankbuster" : "Raidwide"),
                    "CE52 original dynamic damage diagnostic and global text.");
            }
            else Check(dangers() == 0 && Count("Casts") == 0, "CE52 effect-only raidwide cannot fabricate source CastHint.");
            Seek(11.1); Check(dangers() == 0 && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(),
                "CE52 watched Finish releases standard outputs.");
            stop();
            Console.WriteLine($"PASS native CE52 {action} actual standard source geometry solver or damage diagnostic no invented Effect model real Finish owner");
        }
    }
}
