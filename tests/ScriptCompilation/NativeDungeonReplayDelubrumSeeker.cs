using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDelubrumSeeker(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.delubrum.normal";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(0, 0, 277.925f);
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
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object[] Goals() => ((IEnumerable)Read(Snapshot(), "GoalAreas")).Cast<object>().ToArray();
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility");
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        string World(double at, IEnumerable<object> actors, bool full = false)
            => Frame(936, at, actors, full, party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }],
                sourcePlayerId: 100);
        object Spell(uint action, double at = 0, float rotation = .4f, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, center, rotation, target, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 5;
            return node;
        }
        object Primary(object? cast = null, object[]? statuses = null)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x3131, center, combat: true, isEnemy: true, cast: cast))!;
            if (statuses != null) node["Statuses"] = JsonSerializer.SerializeToNode(statuses);
            return node;
        }
        object Helper(object? cast = null) => Actor(20, 0x233C, center + Vector3.UnitX * 10, cast: cast, subKind: 5);
        object Player(ulong actor, bool tether = false)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actor, 0, center + Vector3.UnitZ * (actor == 100 ? 0 : 12)))!;
            if (tether) node["Tethers"] = JsonSerializer.SerializeToNode(new[] { new { Id = 128, TargetId = 101UL } });
            return node;
        }
        object[] Initial(object? cast = null) => [Primary(), Player(100), Player(101), Helper(cast)];
        string Effect(double at, uint action, ulong target = 100) => JsonSerializer.Serialize(new
        {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 20UL, ActionId = action, ActionType = 1, MainTargetId = target,
                GlobalSequence = (uint)(at * 100), Timestamp = Start.AddSeconds(at) },
        });
        string Icon(double at, uint icon, ulong target) => JsonSerializer.Serialize(new
        {
            t = at, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = 100UL, MarkerId = icon, TargetId = target, Timestamp = Start.AddSeconds(at) },
        });
        string Map(double at) => JsonSerializer.Serialize(new
        {
            t = at, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = 0x1B, Param1 = 1, Param2 = 2, Timestamp = Start.AddSeconds(at) },
        });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "DRN1 actual replay error: " + controller.StatusMessage + " / " + manager.ErrorMessage);
        }
        void Release()
        {
            stop();
            Check(previews().Length == 0 && dangers() == 0 && Goals().Length == 0 && Lines().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "DRN1 complete owner release");
        }
        foreach (var action in new uint[] { 23211, 23213, 23218, 23220 })
        {
            load(id, [Header(936), World(0, Initial(Spell(action)), true), Effect(.5, action), World(20, []),
                World(21, [Helper()]), World(22, [])]);
            Seek(.1);
            Check((bool)Read(Model(), "Active") && Shown("bozja/aoe/").Length == 1
                && dangers() == (action == 23218 ? 2 : 1), "DRN1 actual standard display and converted geometry " + action);
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 23259 }),
                "DRN1 real NPC source and unknown action suppression");
            if (action == 23213)
            {
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                var snapshot = Snapshot();
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!,
                    [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                dynamic boundary = Read(snapshot, "ArenaBoundary");
                var target = (Vector3)Read((object)result, "TargetPosition");
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                    && Vector3.Distance(center, target) > 20 && (bool)boundary.Contains(target, 0f),
                    "DRN1 actual solver escapes true R20 inside exported arena without fallback");
            }
            Seek(20.1); Check(Shown("bozja/aoe/").Length == 1, "DRN1 Effect and clock preserve standard cast");
            Seek(21.1); Check(Shown("bozja/aoe/").Length == 0 && dangers() == 0, "DRN1 ordinary Finish consumes");
            Seek(.1); Release();
            Console.WriteLine($"PASS native DRN1 {action} actual standard geometry source gate Finish owner reverse Seek");
        }
        load(id, [Header(936), World(0, [Primary(Spell(23222)), Player(100), Player(101)], true),
            World(1, [Primary()]), World(20, []), Map(21), World(22, [])]);
        Seek(20.1);
        Check(Shown("bozja/aoe/").Length == 1 && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(),
            "DRN1 shrinking ring not raidwide");
        Seek(21.1);
        Check(Shown("bozja/aoe/").Length == 0 && (Vector3)Read(Model(), "Center") == new Vector3(0, 0, 278),
            "DRN1 map low/high words exact and new center");
        dynamic arena = Read(Snapshot(), "ArenaBoundary");
        Check(!(bool)arena.Contains(center + Vector3.UnitX * 27, 0f), "DRN1 actual reduced host boundary");
        Release();
        Console.WriteLine("PASS native DRN1 actual map shrink word order telegraph no ordinary Finish no fabricated damage");

        foreach (var action in new uint[] { 23201, 23202 })
        {
            load(id, [Header(936), World(0, [Primary(Spell(action)), Player(100), Player(101), Helper()], true),
                World(1, [Primary()]), Map(2), Effect(3, 23336), World(4, [])]);
            Seek(1.1);
            Check(Shown("bozja/aoe/").Length == 1 && dangers() == 0 && Goals().Length == 0
                && ((string)Read(Script(), "MechanicHintsText")).Contains(action == 23201 ? "Hide behind barricade!" : "Stay in front of barricade!"),
                "DRN1 actual Blade safe display special hint sampler rejects");
            Seek(2.1);
            Check(Shown("bozja/aoe/").Length == 1 && (Vector3)Read(Items("Casts").Single(), "Origin") == center,
                "DRN1 Blade raw origin remains before map");
            Seek(3.1); Check(Shown("bozja/aoe/").Length == 0, "DRN1 Blade actual Effect consumes");
            Seek(1.1); Release();
            Console.WriteLine($"PASS native DRN1 Blade {action} safe preview sampler boundary actual Effect no automatic knockback");
        }
        var status = new { Id = 2056, Param = 0xF7, SourceId = 0UL, ExpiresAt = Start.AddSeconds(100), SlotIndex = 59 };
        load(id, [Header(936), World(0, [Primary(statuses: [status]), Player(100), Player(101), Helper()], true),
            Effect(1, 23348), World(2, [])]);
        Seek(.1);
        Check(Items("SeekerMercies").Length == 1 && Shown("bozja/aoe/").Length == 1
            && !Items("SeekerBaits").Any() && !Items("SeekerLines").Any(), "DRN1 cold60 without invented Icon marker");
        Seek(1.1); Check(Items("SeekerMercies").Length == 0, "DRN1 real Mercy FIFO");
        Seek(.1); Release();
        Console.WriteLine("PASS native DRN1 cold status59 Mercy FIFO no invented historical Icon or Effect");

        load(id, [Header(936), World(0, Initial(), true), Icon(.1, 243, 100), Icon(.2, 237, 101),
            Effect(1, 23223, 101), Effect(2, 23216), World(3, [])]);
        Seek(.3);
        Check(Items("SeekerBaits").Length == 2 && Shown("seeker/bait/").Length == 2
            && ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 2, "DRN1 actual both Icon components");
        Seek(1.1); Check(Items("SeekerBaits").Length == 1, "DRN1 independent FIFO component");
        Seek(2.1); Check(Items("SeekerBaits").Length == 0, "DRN1 second independent component");
        Release();
        Console.WriteLine("PASS native DRN1 two Icon components original targets diagnostic damage actual Effect FIFO");

        load(id, [Header(936), World(0, [Primary(), Player(100, true), Player(101), Helper()], true),
            World(10, []), World(11, [Player(100)]), World(12, [])]);
        Seek(.1);
        Check(Lines().Length == 1 && dangers() == 1 && ((string)Read(Script(), "MechanicHintsText")).Contains("Break the chains!"),
            "DRN1 actual cold chain line zone hint");
        Seek(10.1); Check(Lines().Length == 1 && dangers() == 1, "DRN1 chain no TTL");
        Seek(11.1); Check(Lines().Length == 0 && dangers() == 0, "DRN1 snapshot Untether");
        Release();
        Console.WriteLine("PASS native DRN1 actual cold chain foreground dynamic navigation no TTL Untether owner");

        load(id, [Header(936), World(0, Initial(), true), Effect(.1, 14588, 101), Effect(.2, 14588, 101),
            Effect(1, 23221, 101), Effect(2, 23221, 100), World(3, [])]);
        Seek(.3);
        Check(Items("SeekerLines").Length == 2 && Shown("seeker/stack/").Length == 2 && Goals().Length == 2
            && dangers() == 0 && ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 1,
            "DRN1 actual multi line background safe goals Shared diagnostic");
        Seek(1.1); Check(Items("SeekerLines").Length == 1 && Goals().Length == 1, "DRN1 first target ID consumes one");
        Seek(2.1); Check(Items("SeekerLines").Length == 0 && Goals().Length == 0, "DRN1 sole target redirect and consume");
        Seek(.3); Release();
        Console.WriteLine("PASS native DRN1 actual multi line complements Shared prediction first match sole redirect owner reverse Seek");

        load(id, [Header(936), World(0, [Primary(), Player(100), Player(101), Helper(),
            Actor(30, 0x3132, center + Vector3.UnitZ * 10, subKind: 5)], true), Effect(6, 23212), World(7, [])]);
        Seek(5.5);
        Check(Items("Eyes").Length == 1 && Shown("bozja/eye/").Length == 1
            && Read(Script(), "PlannedFacing") != null && (ulong)Read(Script(), "PlannedTargetId") == 0,
            "DRN1 Moon actual eye facing plan without target priority");
        Seek(6.1); Check(Items("Eyes").Length == 0 && Shown("bozja/eye/").Length == 0, "DRN1 actual Moon Effect");
        Release();
        Console.WriteLine("PASS native DRN1 actual Moon eye arc facing plan replay execution isolation no invented target");
    }
}
