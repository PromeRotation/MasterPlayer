using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDelubrumDahu(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.delubrum.normal";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(82, 0, 137.9855f);
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
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility");
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        string World(double t, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(936, t, actors, full, removed,
                [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        object Spell(uint action, double at = 0, float rotation = 0, Vector3? location = null, ulong target = 100, float total = 5)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, total, location ?? center, rotation, target, at))!;
            node["IsSpell"] = true; node["TotalTime"] = total;
            return node;
        }
        object Primary(object? cast = null, bool dead = false) => Actor(1, 0x30A6, center, combat: true, isEnemy: true, cast: cast, dead: dead);
        object Helper(object? cast = null, ulong actor = 20, Vector3? position = null)
            => Actor(actor, 0x233C, position ?? center + Vector3.UnitX * 10, cast: cast, subKind: 5);
        object[] Initial(object? cast = null) =>
            [Primary(), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitZ * 20), Helper(cast)];
        string Icon(double t, uint icon, ulong source = 100) => JsonSerializer.Serialize(new
        {
            t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, MarkerId = icon, TargetId = 101UL, Timestamp = Start.AddSeconds(t) },
        });
        string Effect(double t, uint action, ulong source = 20) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = source, ActionId = action, ActionType = 1, GlobalSequence = (uint)(1000 * t),
                MainTargetId = 101UL, Targets = Array.Empty<ulong>(), Timestamp = Start.AddSeconds(t) },
        });
        void Seek(double t)
        {
            controller.Seek(t);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "DRN2 actual replay error: " + controller.StatusMessage + " / " + manager.ErrorMessage);
        }
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!
            .GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [936u, 0x30A6u])! && !(bool)owns.Invoke(null, [937u, 0x30A6u])!
            && !(bool)owns.Invoke(null, [936u, 0x30A8u])!, "DRN2 exact ownership keeps savage OID in its own territory");
        foreach (var action in new uint[] { 22364, 22372, 22373, 22358, 22359, 22367, 22365 })
        {
            load(id, [Header(936), World(0, Initial(Spell(action)), true), Effect(.5, action),
                World(10, []), World(11, [Helper()]), World(12, [])]);
            Seek(.1);
            Check((bool)Read(Model(), "Active") && Shown("bozja/aoe/").Length == 1 && dangers() == 1,
                "DRN2 exact standard or charge publication " + action);
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 22260 }),
                "DRN2 source and unconverted action gate");
            dynamic arena = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
            Check(!(bool)arena.Contains(new Vector3(82, 0, 167), 0f)
                && (bool)arena.Contains(center + Vector3.UnitX * 25, 0f), "DRN2 real notches rather than circle");
            if (action == 22367)
            {
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
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                    && Vector3.Distance(center, target) > 10 && (bool)arena.Contains(target, 0f),
                    "DRN2 real solver escapes R10 within notched polygon without fallback");
            }
            Seek(10.1);
            Check(dangers() == 1 && Shown("bozja/aoe/").Length == 1, "DRN2 Effect and clock do not end cast");
            Seek(11.1);
            Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0, "DRN2 ordinary Finish first source consumes");
            Seek(.1); stop();
            Check(previews().Length == 0 && dangers() == 0, "DRN2 stop and reverse Seek owner");
            Console.WriteLine($"PASS native DRN2 Dahu {action} real standard charge geometry notches source gate Effect no TTL ordinary Finish solver owner Seek");
        }
        load(id, [Header(936), World(0, Initial(Spell(22368, rotation: .4f)), true),
            World(1, [Helper(Spell(22371, 1, rotation: 2))]), World(2, [Helper()]), World(3, [])]);
        Seek(.1);
        Check(Shown("bozja/aoe/").Length == 2 && dangers() == 1 && Items("dahuShockwaves")
            .Select(a => ((bool)Read(a, "Danger"), (bool)Read(a, "Risky"))).SequenceEqual(new[] { (true, true), (false, false) }),
            "DRN2 actual shock display both but navigate first");
        Seek(1.1);
        Check(Shown("bozja/aoe/").Length == 1 && dangers() == 1 && !(bool)Read(Items("dahuShockwaves").Single(), "Danger")
            && (bool)Read(Items("dahuShockwaves").Single(), "Risky"), "DRN2 promoted raw tail retains zero color");
        Seek(2.1); Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0, "DRN2 second Finish consumes");
        stop();
        Console.WriteLine("PASS native DRN2 Dahu Shockwave two previews one risk ordinary Finish FIFO zero-color promotion");

        foreach (var iconFirst in new[] { false, true })
        {
            var events = new List<string> { Header(936), World(0, Initial(), true) };
            if (iconFirst) events.Add(Icon(.1, 167));
            events.Add(World(1, [Helper(Spell(22361, 1, rotation: .4f))]));
            if (!iconFirst) events.Add(Icon(1.1, 167));
            events.Add(Effect(1.2, 22362));
            for (var i = 0; i < 5; ++i)
            {
                events.Add(World(2 + i * 2, [Helper(Spell(22362, 2 + i * 2, rotation: 2, total: .3f))]));
                events.Add(World(3 + i * 2, [Helper()]));
            }
            events.Add(World(13, []));
            load(id, events);
            Seek(1.3);
            Check(Shown("bozja/aoe/").Length == 2 && dangers() == 2 && Items("dahuRotatingAoes").Length == 2,
                "DRN2 five-step rotating cache actual display and nav");
            var published = Items("dahuRotatingAoes");
            var callbackPosition = iconFirst ? center + Vector3.UnitX * 10 : center;
            // Source WPos.Quantized grid, independently checked by the model oracle.
            const float grid = 2000f / 65535f;
            const float inverseGrid = 1f / grid;
            var sourceOrigin = new Vector3(((int)MathF.Round(callbackPosition.X * inverseGrid) - .5f) * grid, 0,
                ((int)MathF.Round(callbackPosition.Z * inverseGrid) - .5f) * grid);
            Check((Vector3)Read(Read(published[0], "Cast"), "Origin") == sourceOrigin,
                "DRN2 actual successful callback origin");
            var snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            var goals = ((IEnumerable)Read((object)snapshot, "GoalAreas")).Cast<object>().ToArray();
            Check(goals.Length == 1, "DRN2 source R5 complement survives as actual safe goal");
            Check((int)Read(Model(), "NumDahuRotations") == 0, "DRN2 Effect ignored");
            for (var i = 1; i <= 5; ++i)
            {
                Seek(1.1 + i * 2);
                Check((int)Read(Model(), "NumDahuRotations") == i
                    && Shown("bozja/aoe/").Length == (i < 4 ? 2 : i == 4 ? 1 : 0), "DRN2 actual five Finish progression");
            }
            manager.SetMechanismEnabled(id, "大兀", false); Seek(.1);
            Check(previews().Length == 0 && dangers() == 0 && Lines().Length == 0
                && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "DRN2 mechanism full release");
            manager.SetMechanismEnabled(id, "大兀", true); Seek(1.3); stop();
            Console.WriteLine($"PASS native DRN2 Dahu rotation iconFirst={iconFirst} callback origin R5 goal five actual Finish mechanism owner reverse Seek");
        }
        load(id, [Header(936), World(0, Initial(Spell(22361, rotation: .4f)), true), World(10, [])]);
        Seek(10);
        Check(Items("dahuRotatingAoes").Length == 0 && dangers() == 0, "DRN2 cold cast cannot restore missing icon");
        stop();
        Console.WriteLine("PASS native DRN2 Dahu cold rotation missing history remains absent");

        load(id, [Header(936), World(0, [Primary(Spell(22357)), Actor(100, 0, center + Vector3.UnitZ * 3),
            Actor(101, 0, center + Vector3.UnitX * 20), Helper(Spell(22359, location: center + Vector3.UnitZ * 25),
                position: center + Vector3.UnitZ * 25)], true), World(1, [Primary()]), World(2, [])]);
        Seek(.1);
        var line = Lines().Single();
        var end = (Vector3)Read(line, "End");
        Check(end.Z > 166 && end.Z < 166.5f && dangers() > 0
            && ((string)Read(Script(), "MechanicHintsText")).Contains("About to be knocked into danger!"),
            "DRN2 actual wall stop and HuntersClaw destination unsafe text");
        Seek(1.1); Check(Lines().Length == 0, "DRN2 Howl ordinary Finish removes line"); stop();
        Console.WriteLine("PASS native DRN2 Dahu FeralHowl actual wall-stop line claws union unsafe ordinary Finish");

        var wall = center + new Vector3(0, 0, 28.502502f);
        load(id, [Header(936), World(0, [Primary(), Actor(100, 0, wall),
            Actor(101, 0, center), Helper(Spell(22357, location: wall - Vector3.UnitX),
                position: wall - Vector3.UnitX)], true), World(1, [])]);
        Seek(.1);
        var wallLine = Lines().Single();
        var wallEnd = (Vector3)Read(wallLine, "End");
        Check(Vector3.Distance(wallEnd, wall - Vector3.UnitX * (.5f - .499f)) < .00002f,
            "DRN2 actual polygon ray includes zero collinear wall overlap");
        stop();
        Console.WriteLine("PASS native DRN2 Dahu FeralHowl actual zero-distance collinear wall overlap and hitbox correction");

        load(id, [Header(936), World(0, Initial(Spell(22374)), true), World(1, [Helper()]),
            Effect(2, 22374, 1), Effect(3, 22374), World(4, [])]);
        Seek(1.1);
        Check(Items("DahuBaits").Length == 1 && Shown("dahu/bait/").Length == 1
            && ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 1
            && ((string)Read(Script(), "MechanicHintsText")).Contains("Tankbuster cleave"), "DRN2 actual HeatBreath Finish ignored");
        Seek(2.1); Check(Items("DahuBaits").Length == 1, "DRN2 wrong source actual effect ignored");
        Seek(3.1);
        Check(Items("DahuBaits").Length == 0 && Shown("dahu/bait/").Length == 0
            && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "DRN2 right source actual effect consumes");
        Seek(.1); stop();
        Check(previews().Length == 0 && dangers() == 0 && Lines().Length == 0
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "DRN2 final owner release");
        Console.WriteLine("PASS native DRN2 Dahu HeatBreath original target diagnostic real Effect first source Finish ignored owner replay isolation");
    }
}
