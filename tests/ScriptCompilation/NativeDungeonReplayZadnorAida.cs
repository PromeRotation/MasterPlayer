using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunZadnorAida(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const string mechanism = "光辉虹蛇——阿依达";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var jsonOptions = (JsonSerializerOptions)assembly.GetType("MasterPlayer.Replay.ReplayJson")!
            .GetField("Options", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var center = new Vector3(-200, 0, -580);
        object[] roster = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        object Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, flags) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Script() => Read(manager.Running[id], "Script");
        object Model() => Read(Script(), "encounter");
        object[] Items(string name) => ((IEnumerable)Read(Model(), name)).Cast<object>().ToArray();
        object[] Shown(string key) => previews().Where(p => ((string)Read(p, "Id")).Contains(key)).ToArray();
        string World(double t, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(975, t, actors, full, removed, roster, sourcePlayerId: 100);
        object Spell(uint action, double at, Vector3? origin = null, ulong target = 100, float rotation = 0)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 4, origin ?? center, rotation, target, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 4;
            return node;
        }
        object Helper(object? cast = null, Vector3? p = null)
            => Actor(20, 0x233C, p ?? center, cast: cast, subKind: 5);
        object Primary(Vector3? p = null) => Actor(1, 0x31C9, p ?? center, combat: true, isEnemy: true);
        object[] Initial(object? cast = null) =>
            [Primary(), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 18), Helper(cast)];
        object Fireball(byte model = 0, byte first = 0, byte second = 0, Vector3? p = null)
        {
            var node = JsonSerializer.SerializeToNode(Actor(30, 0x31CB, p ?? center + Vector3.UnitZ * 4))!;
            node["ModelState"] = model; node["AnimationState1"] = first; node["AnimationState2"] = second;
            return node;
        }
        string Effect(double t, uint action, uint sequence, Vector3? position = null, ulong source = 1)
            => JsonSerializer.Serialize(new
            {
                t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = action, ActionType = 1, GlobalSequence = sequence,
                    MainTargetId = 100UL, Targets = Array.Empty<ulong>(),
                    TargetPosition = position.HasValue ? Pose(position.Value) : null, Timestamp = Start.AddSeconds(t) },
            });
        void Seek(double time)
        {
            controller.Seek(time);
            Check((int)controller.RouterErrorCount == 0 && (string)manager.ErrorMessage == "",
                "CE62 actual replay callback: " + controller.StatusMessage + "; " + manager.ErrorMessage);
        }
        void ApplyWithoutUpdate(double time, IEnumerable<object> actors, ulong[]? removed = null)
        {
            var world = Read(Script(), "ReplayWorld");
            using var json = JsonDocument.Parse(World(time, actors, removed: removed));
            var data = json.RootElement.GetProperty("data");
            var frame = JsonSerializer.Deserialize(data.GetRawText(),
                assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!, jsonOptions)!;
            var decoded = ((IEnumerable)Read(frame, "Actors")).Cast<object>().ToArray();
            foreach (var actor in data.GetProperty("Actors").EnumerateArray())
            {
                var actual = decoded.Single(a => (ulong)Read(a, "Id") == actor.GetProperty("Id").GetUInt64());
                var position = actor.GetProperty("Position");
                Check((Vector3)Read(actual, "Position") == new Vector3(position.GetProperty("X").GetSingle(),
                    position.GetProperty("Y").GetSingle(), position.GetProperty("Z").GetSingle()),
                    "CE62 manual snapshot preserves actor coordinates");
            }
            world.GetType().GetMethod("Apply", flags)!.Invoke(world, [frame]);
            world.GetType().GetProperty("CurrentTime")!.SetValue(world, Start.AddSeconds(time));
        }
        void Route(LogSystemEvent ev)
        {
            var router = Read(Read(manager.Running[id], "Context"), "_router");
            router.GetType().GetMethod("Raise")!.Invoke(router, [ev]);
            Check((int)Read(router, "ErrorCount") == 0, "CE62 fixed Framework router");
        }
        void Raise(string record)
        {
            using var json = JsonDocument.Parse(record);
            var ev = (LogSystemEvent)JsonSerializer.Deserialize(json.RootElement.GetProperty("data").GetRawText(),
                assembly.GetType("MasterPlayer.Scripting.NativeActionEffectEvent")!, jsonOptions)!;
            var data = json.RootElement.GetProperty("data");
            if (data.TryGetProperty("TargetPosition", out var position) && position.ValueKind != JsonValueKind.Null)
                Check((Vector3?)Read(ev, "TargetPosition") == new Vector3(position.GetProperty("X").GetSingle(),
                    position.GetProperty("Y").GetSingle(), position.GetProperty("Z").GetSingle()),
                    "CE62 manual native packet preserves actual coordinates");
            Route(ev);
        }
        void Update() => Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.02f]);
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!
            .GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [975u, 0x31C9u])! && !(bool)owns.Invoke(null, [920u, 0x31C9u])!
            && !(bool)owns.Invoke(null, [975u, 0x233Cu])!, "CE62 exact ownership");
        load(id, [Header(975), World(0, [Actor(1, 0x31C9, center, isEnemy: true),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 15), Helper(), Fireball()], true), World(20, [])]);
        Seek(.01);
        Check(!(bool)Read(Model(), "Active"), "CE62 preactivation control");
        ApplyWithoutUpdate(.05, [Primary(), Fireball(first: 1)]);
        Route(new LogSystemCastStartEvent { SourceId = 20, ActionId = 6499, Timestamp = Start.AddSeconds(.05) });
        Check(Items("Burns").Length == 0, "CE62 preScan before activation");
        Update();
        Check((bool)Read(Model(), "Active") && Items("Burns").Length == 0,
            "CE62 preScan cannot activate before formal MDLS baseline");
        ApplyWithoutUpdate(.08, [Fireball(first: 1)]); Update();
        Check(Items("Burns").Length == 0, "CE62 identical tuple after activation no fabricated Burn");
        stop();
        Console.WriteLine("PASS native CE62 log preScan then Framework formal snapshot precedes cold activation no later synthetic MDLS replay");
        foreach (var action in new uint[] { 23970, 23977, 23978, 23964, 23974, 23979, 23981, 23985 })
        {
            load(id, [Header(975), World(0, Initial(Spell(action, 0)), true), World(10, []),
                World(11, [Helper()]), World(12, [])]);
            Seek(10.1);
            if (action is 23970 or 23977 or 23978)
            {
                var shape = Read(Shown("bozja/aoe/").Single(), "Geometry");
                Check(dangers() == 1 && (action == 23970
                    ? (float)Read(shape, "Width") == 10 && (float)Read(shape, "Length") == 80
                    : (float)Read(shape, "Radius") == 40), "CE62 true standard shape");
            }
            else if (action == 23979)
                Check(dangers() == 0 && (string)Read(Script(), "MechanicHintsText") == "Set hp to 1"
                    && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "CE62 hint only");
            else
            {
                var damage = ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Single();
                Check((ulong)Read(damage, "Mask") == (action == 23985 ? 1UL : 3UL) && dangers() == 0,
                    "CE62 actual raw dynamic diagnostic");
            }
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "CE62 true source gate");
            if (action == 23970)
            {
                Seek(.1);
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!,
                    [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                var target = (Vector3)Read((object)result, "TargetPosition");
                var shape = Read(Shown("bozja/aoe/").Single(), "Geometry");
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                    && !(bool)shape.GetType().GetMethod("Contains")!.Invoke(shape, [target, 0f])!
                    && Vector3.Distance(center, target) <= 20, "CE62 actual solver leaves forward ray inside circle no fallback");
            }
            Seek(11.1); Check(dangers() == 0 && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(),
                "CE62 watched raw Finish");
            stop();
            Console.WriteLine($"PASS native CE62 {action} actual DLL standard source geometry or raw dynamic diagnostic exact Finish source gate no TTL owner");
        }

        load(id, [Header(975), World(0, Initial(Spell(23984, 0)), true),
            World(1, [Actor(100, 0, center, dead: true)]), World(2, [Helper()]), World(3, [])]);
        Seek(.1);
        Check(Shown("bozja/spread/").Length == 1 && dangers() == 1
            && (float)Read(Read(Shown("bozja/spread/").Single(), "Geometry"), "Radius") == 6
            && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "CE62 ordinary R6 spread");
        Seek(1.1); Check(Shown("bozja/spread/").Length == 0 && Items("Casts").Length == 1,
            "CE62 dead raw spread retained");
        Seek(2.1); Check(Items("Casts").Length == 0, "CE62 targetID Finish");
        stop();
        Console.WriteLine("PASS native CE62 Levinbolt true R6 outline R7 navigation dead raw targetID Finish no invented spread damage");

        load(id, [Header(975), World(0, [.. Initial(Spell(23968, 0)),
                Actor(21, 0x233C, center, cast: Spell(23968, 0), subKind: 5)], true),
            Effect(1, 23968, 101, center), Effect(2, 23968, 102, center),
            World(3, [Primary(center + Vector3.UnitX * 15)]), Effect(3.1, 23969, 103, center),
            World(4, [Primary()]), Effect(4.1, 23969, 104, new(999, 0, 999)), World(5, [])]);
        Seek(2.1);
        var raw = Items("Casts");
        Check(raw.Length == 2 && Read(Read(raw[0], "Shape"), "InnerRadius") is 4f
            && Read(raw[1], "Shape").GetType().Name == "CircleTemplate"
            && (DateTime)Read(raw[0], "Activation") == Start.AddSeconds(2 + 6.1d), "CE62 first donut repeat resets actual clock");
        Seek(3.2); Check(Items("Casts").Length == 2, "CE62 correct TargetPos wrong caster not consumed");
        Seek(4.2); Check(Items("Casts").Length == 1, "CE62 actual caster consumes first irrespective TargetPos");
        stop();
        Console.WriteLine("PASS native CE62 Lightning duplicate raw origins repeat first donut clock reset Clap consumes actual caster position not area target");

        load(id, [Header(975), World(0, Initial(Spell(23972, 0)), true),
            Effect(1, 23972, 110, new(999, 0, 999)),
            World(2, [Primary(center + Vector3.UnitZ * 5)]), Effect(2.1, 23973, 111, new(999, 0, 999)),
            World(10, [])]);
        Seek(.1); Check(dangers() == 2 && Shown("bozja/aoe/").Length == 2, "CE62 two exaflare outputs");
        Seek(2.2);
        Check((int)Read(Items("Exaflares").Single(), "Left") == 2 && dangers() == 2,
            "CE62 both actual caster Effects ignore wrong TargetPos");
        Seek(10.1); Check(dangers() == 2, "CE62 no TTL");
        Seek(.1); stop();
        Console.WriteLine("PASS native CE62 Cloud two cached outputs step5 interval1point1 both actual caster Effects wrong area ignored reverse Seek");

        load(id, [Header(975), World(0, [.. Initial(), Fireball(first: 1)], true),
            World(1, [Fireball(model: 1, first: 1)]), World(1.999, [Fireball(model: 1, first: 1, second: 1)]),
            World(2, [Fireball(model: 2, first: 1, second: 1)]), World(3, [], removed: [30]),
            Effect(4, 23976, 120), World(5, [])]);
        Seek(.1); Check(Items("Burns").Length == 0 && dangers() == 0, "CE62 cold current animation has no MDLS replay");
        Seek(1.9995); Check(Items("Burns").Length == 2 && dangers() == 2, "CE62 any tuple change retriggers");
        Seek(2.1); Check(Items("Burns").Length == 3 && dangers() == 2, "CE62 exact one second hidden");
        Seek(3.1); Check(Items("Burns").Length == 3 && dangers() == 2, "CE62 original fireball destruction keeps queue");
        Seek(4.1); Check(Items("Burns").Length == 2 && dangers() == 2, "CE62 head FIFO hidden promotion");
        Seek(1.1);
        manager.SetMechanismEnabled(id, mechanism, false); Seek(1.1);
        Check(previews().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "CE62 mechanism owner release");
        manager.SetMechanismEnabled(id, mechanism, true); Seek(1.1); stop();
        Console.WriteLine("PASS native CE62 Burn snapshot full tuple no cold replay prefix strict1 second source destruction FIFO promotion mechanism owner Seek");

        load(id, [Header(975), World(0, [.. Initial(),
                Actor(21, 0x233C, center, cast: Spell(23970, 0), subKind: 5)], true), World(20, [])]);
        Seek(.1);
        Raise(Effect(.1, 23976, 125, source: 20)); Update();
        Check(Read(Model(), "BurnSequenceError") != null && dangers() == 0
            && ((IList)Read(Script(), "pendingEvents")).Count == 1, "CE62 missing source recovery begins failed");
        ApplyWithoutUpdate(1, [], [20]); Update();
        Check(Read(Model(), "BurnSequenceError") == null && dangers() == 1
            && ((IList)Read(Script(), "pendingEvents")).Count == 0,
            "CE62 vanished Effect source skips retry callback clears transient barrier and restores region");
        Check(((string)manager.ErrorMessage).Contains("Burn 空队列"), "CE62 expected disappearing-source FIFO diagnostic");
        stop(); manager.SetError("");
        Console.WriteLine("PASS native CE62 failed Effect source disappears next snapshot entire batch succeeds transient error clears actual output restored");

        load(id, [Header(975), World(0, [.. Initial(Spell(23968, 0)), Fireball()], true), World(20, [])]);
        Seek(.1);
        ApplyWithoutUpdate(1, []);
        Raise(Effect(1, 23968, 130, center));
        Raise(Effect(1, 23976, 131));
        Update();
        Check(((string)Read(Model(), "BurnSequenceError")).Contains("空队列") && dangers() == 0 && previews().Length == 0
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null
            && ((IList)Read(Script(), "pendingEvents")).Count == 2 && Items("Casts").Length == 1,
            "CE62 empty Burn keeps entire batch and model history while withdrawing outputs");
        ApplyWithoutUpdate(2, [Fireball()]); Update();
        Check(((IList)Read(Script(), "pendingEvents")).Count == 2 && Items("Burns").Length == 0,
            "CE62 unchanged tuple cannot unlock failure");
        ApplyWithoutUpdate(3, [Fireball(first: 1)]);
        // A log callback can Scan(false) before Framework without consuming MDLS.
        Raise(Effect(3, 6499, 132));
        Check(Items("Burns").Length == 0, "CE62 event preScan does not dispatch MDLS early");
        Update();
        Check(Read(Model(), "BurnSequenceError") == null && ((IList)Read(Script(), "pendingEvents")).Count == 0
            && Items("Burns").Length == 0 && dangers() == 1
            && (DateTime)Read(Items("Casts").Single(), "Activation") == Start.AddSeconds(3 + 6.1d),
            $"CE62 real snapshot MDLS precedes retry, successful earlier Lightning repeats at new current clock: error={Read(Model(), "BurnSequenceError")}, pending={((IList)Read(Script(), "pendingEvents")).Count}, burns={Items("Burns").Length}, dangers={dangers()}, clock={(DateTime)Read(Items("Casts").Single(), "Activation"):O}, now={Read(Model(), "Now")}");
        Check(((string)manager.ErrorMessage).Contains("Burn 空队列"), "CE62 asserted expected FIFO diagnostic");
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest")
            && (ulong)Read(Read(Script(), "Hints"), "target") == 0 && !(bool)Read(Read(Script(), "Hints"), "holdActions"),
            "CE62 replay emits no control");
        stop(); manager.SetError("");
        Console.WriteLine("PASS native CE62 empty FIFO real Framework entire packet batch retained unchanged tuple stays failed new MDLS before retry repeats earlier event recovery replay isolation");
    }
}
