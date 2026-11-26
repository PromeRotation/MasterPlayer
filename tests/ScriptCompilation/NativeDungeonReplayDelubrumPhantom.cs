using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDelubrumPhantom(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.delubrum.normal";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(202, 0, -374);
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
        object[] Items(string name) => ((IEnumerable)Read(Model(), name)).Cast<object>().ToArray();
        object[] Shown() => previews().Where(p => ((string)Read(p, "Id")).Contains("bozja/aoe/")).ToArray();
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility");
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        string World(double t, IEnumerable<object> actors, bool full = false)
            => Frame(936, t, actors, full, party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        object Spell(uint action, double at = 0, Vector3? location = null, float rotation = 0, float total = 8)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, total, location ?? center, rotation, 100, at))!;
            node["IsSpell"] = true; node["TotalTime"] = total;
            return node;
        }
        object Primary() => Actor(1, 0x30AC, center, combat: true, isEnemy: true);
        object Helper(object? cast = null) => Actor(20, 0x233C, center, cast: cast, subKind: 5);
        object Feature()
        {
            var node = JsonSerializer.SerializeToNode(Actor(40, 0x1EA1A1, center))!;
            node["Kind"] = 7;
            return node;
        }
        object[] Initial(object? cast = null) =>
            [Primary(), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 15), Helper(cast),
                Feature()];
        string Animation(double t, uint state, ulong source = 40) => JsonSerializer.Serialize(new
        {
            t, kind = "EObjAnimation", type = "PromeRotation.LogSystem.LogSystemEObjAnimationEvent",
            data = new { SourceId = source, Param1 = (ushort)(state >> 16), Param2 = (ushort)state, Timestamp = Start.AddSeconds(t) },
        });
        string Effect(double t, uint action) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 20UL, ActionId = action, ActionType = 1, GlobalSequence = (uint)(t * 1000),
                MainTargetId = 100UL, Targets = Array.Empty<ulong>(), Timestamp = Start.AddSeconds(t) },
        });
        void Seek(double t)
        {
            controller.Seek(t);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "DRN4 actual replay error: " + controller.StatusMessage + " / " + manager.ErrorMessage);
        }
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!.GetMethod("Owns", BindingFlags.NonPublic | BindingFlags.Static)!;
        Check((bool)owns.Invoke(null, [936u, 0x30ACu])! && !(bool)owns.Invoke(null, [937u, 0x30ACu])!
            && !(bool)owns.Invoke(null, [936u, 0x30AEu])! && (bool)owns.Invoke(null, [936u, 0x3131u])!
            && !(bool)owns.Invoke(null, [937u, 0x3131u])!,
            "DRN4 and DRN1 precise normal ownership keeps savage identities separate");
        foreach (var action in new uint[] { 22449, 22437, 22447 })
        {
            load(id, [Header(936), World(0, Initial(Spell(action)), true), Effect(.5, action), World(10, []),
                World(11, [Helper()]), World(12, [])]);
            Seek(.1);
            Check((string)Read(Read(Model(), "Definition"), "Key") == "DRN4Phantom", "DRN4 actual selected model");
            dynamic snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            dynamic arena = snapshot.ArenaBoundary;
            Check((bool)arena.Contains(center + new Vector3(23.4f, 0, 29.4f), 0f)
                && !(bool)arena.Contains(center + new Vector3(23.6f, 0, 0), 0f), "DRN4 real initial rectangle");
            if (action == 22447)
                Check(Shown().Length == 0 && dangers() == 0
                    && ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 1
                    && ((string)Read(Script(), "MechanicHintsText")).Contains("Raidwide"), "DRN4 diagnostic only Raidwide");
            else
                Check(Shown().Length == 1 && dangers() == 1, "DRN4 standard actual geometry");
            Seek(10.1);
            Check(Items("Casts").Length == 1, "DRN4 no Effect TTL removal");
            Seek(11.1);
            Check(Items("Casts").Length == 0 && Shown().Length == 0 && dangers() == 0, "DRN4 ordinary Finish");
            Seek(.1); stop();
            Check(previews().Length == 0 && dangers() == 0, "DRN4 reverse Seek and owner");
            Console.WriteLine($"PASS native DRN4 Phantom {action} actual standard or diagnostic initial bounds Effect no TTL ordinary Finish owner Seek");
        }
        load(id, [Header(936), World(0, Initial(), true), Animation(.1, 0x1234),
            Animation(.2, 0x10002, 20), Animation(.3, 0x10002), World(20, []),
            Animation(21, 0x80010), Animation(22, 0x10002), World(23, [])]);
        Seek(.25); Check(Shown().Length == 0 && dangers() == 0, "DRN4 unknown state and wrong OID ignored");
        Seek(.4); Check(Shown().Length == 1 && dangers() == 2, "DRN4 one multipart preview two real navigation islands");
        Seek(20.1); Check(Shown().Length == 1 && dangers() == 2, "DRN4 animation prediction does not auto shrink");
        Seek(21.1);
        Check(Shown().Length == 0 && dangers() == 0
            && (Vector3)Read(Model(), "Center") == new Vector3(202, 0, -370), "DRN4 actual shrink event");
        dynamic reduced = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!(bool)reduced.Contains(new Vector3(202, 0, -395), 0f)
            && (bool)reduced.Contains(new Vector3(202, 0, -393), 0f), "DRN4 reduced bounds exact");
        Seek(22.1);
        dynamic shiftedGeometry = Read(Shown().Single(), "Geometry");
        Check((bool)shiftedGeometry.Contains(new Vector3(202, 0, -392), 0f) && dangers() == 2,
            "DRN4 second animation shifts display and keeps first navigation cache");
        manager.SetMechanismEnabled(id, "博兹雅幻灵", false);
        Seek(.4);
        Check(previews().Length == 0 && dangers() == 0
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "DRN4 mechanism releases all output");
        manager.SetMechanismEnabled(id, "博兹雅幻灵", true);
        Seek(.4); stop();
        Console.WriteLine("PASS native DRN4 Phantom actual EAnim source gate multipart preview navigation cached second shrink mechanism owner reverse Seek");
        var events = new List<string> { Header(936), World(0, Initial(Spell(22441)), true), Effect(.5, 22441), World(1, [Helper()]) };
        for (var i = 1; i < 8; ++i)
        {
            events.Add(World(i * 2, [Helper(Spell(22442, i * 2, center + Vector3.UnitZ * (i * 6), total: 1))]));
            events.Add(World(i * 2 + 1, [Helper()]));
        }
        events.Add(World(16, []));
        load(id, events);
        Seek(.1);
        Check(Items("Exaflares").Length == 1 && Shown().Length == 2 && dangers() == 2, "DRN4 actual future and current both risky");
        Seek(.6);
        Check((int)Read(Items("Exaflares").Single(), "Left") == 8, "DRN4 actual Effect does not advance");
        Seek(1.1);
        Check((int)Read(Items("Exaflares").Single(), "Left") == 7, "DRN4 first ordinary Finish");
        for (var i = 1; i < 8; ++i)
        {
            Seek(i * 2 + 1.1);
            Check(Items("Exaflares").Length == (i < 7 ? 1 : 0)
                && Shown().Length == (i < 6 ? 2 : i == 6 ? 1 : 0), "DRN4 actual eight Finish progression " + i);
        }
        Check((int)Read(Model(), "NumPhantomLinesFinished") == 1, "DRN4 actual line finished counter");
        Seek(.1); stop();
        Console.WriteLine("PASS native DRN4 Phantom actual Swirling future current both Risky Effect ignored eight ordinary Finish one final count owner reverse Seek");
        foreach (var withLine in new[] { false, true })
        {
            var initial = new List<object>(Initial(Spell(22444, rotation: 0, total: 6)));
            if (withLine) initial.Add(Actor(21, 0x233C, center, cast: Spell(22441), subKind: 5));
            load(id, [Header(936), World(0, initial, true), World(1, [Helper()]), World(2, [])]);
            Seek(.1);
            var line = Lines().Single();
            var from = (Vector3)Read(line, "Start");
            var to = (Vector3)Read(line, "End");
            Check(to == from + Vector3.UnitZ * 30
                && ((string)Read(Script(), "MechanicHintsText")).Contains("About to be knocked into danger!"),
                "DRN4 actual forward30 not truncated at wall and dangerous");
            Check(dangers() == (withLine ? 2 : 1)
                && ((IEnumerable)Read(runtime.Manager.BuildSnapshot(runtime.CurrentTime), "GoalAreas")).Cast<object>().Count() == 0,
                "DRN4 real rectangle versus intersection sampler rejection no invented safe goals");
            if (!withLine)
            {
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                object snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!,
                    [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                var target = (Vector3)Read((object)result, "TargetPosition");
                dynamic arena = Read(snapshot, "ArenaBoundary");
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                    && target.Z < center.Z - 16 && (bool)arena.Contains(target, 0f),
                    "DRN4 real solver avoids forbidden rectangle inside initial arena without fallback");
            }
            Seek(1.1); Check(Lines().Length == 0, "DRN4 ordinary Finish clears movement line");
            Seek(.1); stop();
            Console.WriteLine($"PASS native DRN4 Phantom actual Undying withLine={withLine} forward30 no wall clamp raw navigation accepted or rejected no safe goal ordinary Finish");
        }
    }
}
