using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHydatos(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.hydatos";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var center = new Vector3(100, 0, 100);
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
        object Region() => Read(Script(), "region");
        object Model() => Read(Region(), "Selected");
        object[] Aoes() => ((IEnumerable)Read(Model(), "Aoes")).Cast<object>().ToArray();
        object[] Damage() => ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().ToArray();
        object[] Shown(string key) => previews().Where(p => ((string)Read(p, "Id")).Contains(key)).ToArray();
        object[] Lines()
        {
            var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
            var registry = Read(plugin, "AoeVisibility");
            return ((IEnumerable)registry.GetType().GetMethod("GetLines")!.Invoke(registry, null)!).Cast<object>().ToArray();
        }
        string World(double t, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(827, t, actors, full, removed, roster, sourcePlayerId: 100);
        object Spell(uint action, double at = 0, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 3, center, .4f, target, at))!;
            node["IsSpell"] = true;
            node["TotalTime"] = 3;
            return node;
        }
        string Raw(double t, uint action, ushort? rotation = 12345, ulong target = 100)
            => JsonSerializer.Serialize(new { t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = 20UL, ActionId = action, MainTargetId = target, ActionType = 1,
                    RotationInt = rotation, GlobalSequence = 0, Targets = new[] { target } } });
        string Icon(double t, uint icon, ulong source = 100, ulong target = 101)
            => JsonSerializer.Serialize(new { t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = source, TargetId = target, MarkerId = icon } });
        object[] Initial(uint oid, object? cast = null) =>
            [Actor(1, oid, center, combat: true, isEnemy: true, objectIndex: 2),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 4),
                Actor(20, 0x233C, center, cast: cast, subKind: 5)];
        void Seek(double t)
        {
            controller.Seek(t);
            Check((int)controller.RouterErrorCount == 0, $"Hydatos callback failed: {controller.StatusMessage}");
        }
        var hostType = assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        var bridge = RuntimeHelpers.GetUninitializedObject(hostType);
        var owns = assembly.GetType("MasterPlayer.Mechanics.Hydatos.HydatosCatalog")!.GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [827u, 0x2765u])! && !(bool)owns.Invoke(null, [826u, 0x2765u])!
            && !(bool)owns.Invoke(null, [827u, 0x2658u])!, "Hydatos ownership swallowed an unowned territory/OID");
        foreach (var (name, oid, action) in new (string, uint, uint)[] {
            ("Ceto", 0x2765, 15468), ("Daphne", 0x2744, 15025), ("Molech", 0x275D, 14973),
            ("Ovni", 0x2685, 14783), ("ProvenanceWatcher", 0x2686, 15000),
            ("Phuabo", 0x2741, 14924), ("Tristitia", 0x2740, 14922) })
        {
            load(id, [Header(827), World(0, Initial(oid, Spell(action)), true),
                CancelHoH(.2, 20, action), World(20, []),
                World(21, [Actor(20, 0x233C, center)]), World(22, [])]);
            Seek(.1);
            Check((bool)Read(Script(), "EncounterActive") && dangers() == 1 && Shown("hydatos/aoe/").Length == 1,
                $"Hydatos {name} cold helper/nav/preview");
            Check((string)Read(Read(Model(), "Definition"), "Name") == name, $"Hydatos wrong selection {name}");
            var boundary = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
            Check(boundary.GetType().Name == (name == "ProvenanceWatcher" ? "RectangleGeometry" : "CircleGeometry"),
                $"Hydatos {name} navigation bounds differ");
            Check(!(bool)hostType.GetMethod("IsReplaySuppressed", flags)!.Invoke(bridge, null)!,
                "Hydatos whole territory suppression hid unowned BA modules");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 999, ActionId = (ushort)action }),
                $"Hydatos {name} exact native source suppression");
            Seek(20.1);
            Check(dangers() == 1 && Aoes().Length == 1, $"Hydatos {name} expired on packet cancel/timeout");
            Seek(21.1);
            Check(dangers() == 0 && Aoes().Length == 0, $"Hydatos {name} actual raw Finish not consumed");
            Seek(.1);
            Check(dangers() == 1, $"Hydatos {name} reverse Seek");
            var hints = Read(Script(), "Hints");
            Check(!(bool)Read(Read(hints, "_facing"), "_hasRequest"), "Hydatos replay sent actual facing");
            manager.SetMechanismEnabled(id, name, false);
            Seek(.1);
            Check(dangers() == 0 && previews().Length == 0
                && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, $"Hydatos {name} disabled output leak");
            manager.SetMechanismEnabled(id, name, true);
            Seek(.1);
            stop();
            Console.WriteLine($"PASS native Hydatos {name} cold full world exact geometry actual navigation boundary raw Finish no timeout cancel source gate replay Seek owner");
        }
        load(id, [Header(827), World(0, [
            Actor(1, 0x2765, center, combat: true), Actor(2, 0x2685, center),
            Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 4),
            Actor(20, 0x233C, center, cast: Spell(15468), subKind: 5),
            Actor(21, 0x233C, center, cast: Spell(14783), subKind: 5)], true),
            World(.1, [Actor(2, 0x2685, center, combat: true)]),
            World(.2, [Actor(1, 0x2765, center)]), World(1, [])]);
        Seek(.15);
        Check((string)Read(Read(Model(), "Definition"), "Name") == "Ceto" && dangers() == 1
            && ((IEnumerable)Read(Region(), "Loaded")).Cast<object>().Count() == 2, "Hydatos active tie replaced Ceto/background model lost");
        Seek(.25);
        Check((string)Read(Read(Model(), "Definition"), "Name") == "Ovni" && dangers() == 1
            && (float)Read(Read(Shown("hydatos/aoe/").Single(), "Geometry"), "Radius") == 24,
            "Hydatos one publisher failed selected switch/background cold state");
        stop();
        Console.WriteLine("PASS native Hydatos unified Loaded active tie persistent background mechanism state single boundary and selected output switch exact ownership");

        object WithStatus(object actor, ushort param, bool present = true)
        {
            var node = JsonSerializer.SerializeToNode(actor)!;
            node["Statuses"] = JsonSerializer.SerializeToNode(present ? new[] {
                new { Id = 199, Param = param, SlotIndex = 59, ExpiresAt = Start.AddSeconds(30) } } : []);
            return node;
        }
        load(id, [Header(827), World(0, [
            Actor(1, 0x2740, center, combat: true), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 4),
            WithStatus(Actor(20, 0x233C, center, subKind: 5), 1),
            WithStatus(Actor(21, 0x233C, center, subKind: 2), 1)], true),
            StatusEvent(.1, 20, 199, 1, true),
            World(.2, [WithStatus(Actor(20, 0x233C, center, subKind: 5), 2)]),
            World(.3, [WithStatus(Actor(20, 0x233C, center, subKind: 5), 0, false),
                WithStatus(Actor(21, 0x233C, center, subKind: 2), 0, false)]), World(1, [])]);
        int Dispels() => ((IEnumerable)Read(Model(), "DispelTargets")).Cast<object>().Count();
        Seek(.15); Check(Dispels() == 2 && Shown("hydatos/dispel/").Length == 2 && dangers() == 0,
            "Hydatos cold raw slot59 Pet/expanded Status double route/intent-only outputs");
        Seek(.25); Check(Dispels() == 3, "Hydatos Param refresh did not preserve duplicate raw target intent");
        Seek(.35); Check(Dispels() == 2 && Shown("hydatos/dispel/").Length == 2,
            "Hydatos Lose removed all duplicates or hot Pet callback consumed cold reference");
        stop();
        Console.WriteLine("PASS native Hydatos raw slot59 cold Pet hot filter status events ignored Param duplicate Lose first reference pure dispel diagnostics");

        load(id, [Header(827), World(0, Initial(0x2686), true),
            Raw(.1, 14999, 12345),
            World(.2, [Actor(20, 0x233C, center + Vector3.UnitX * 20, rotation: 2)]),
            EffectEvent(.3, 20, 15007, 0),
            Raw(.4, 14999, null), Raw(.5, 15007),
            Raw(.6, 14999, 54321), Raw(.7, 15007), World(1, [])]);
        Seek(.25);
        var tail = Aoes().Single();
        var expectedRotation = 12345 * (float)(1d / 65535 * 2 * Math.PI) - MathF.PI;
        Check((Vector3)Read(tail, "Origin") == center && (float)Read(tail, "Rotation") == expectedRotation
            && dangers() == 1, "Hydatos true packet Tail lock point/rotation");
        Seek(.35);
        Check(dangers() == 1, "Hydatos expanded PR Effect consumed native component twice");
        Seek(.45);
        Check(dangers() == 0 && Read(Script(), "MechanicHintsText") is string diagnostic && diagnostic.Contains("朝向"),
            "Hydatos old rotation-less raw event guessed direction/lost diagnostic");
        Seek(.65); Check(dangers() == 1, "Hydatos restored raw packet rotation failed");
        Seek(.75); Check(dangers() == 0, "Hydatos actual second Tail not consumed");
        stop();
        Console.WriteLine("PASS native Hydatos true raw RotationInt lock no actor rotation fallback expanded effect ignored missing legacy rotation diagnostic actual clear");

        load(id, [Header(827), World(0, Initial(0x2744), true),
            Icon(.1, 62), Icon(.2, 62), Raw(.3, 15033, target: 100), Raw(.4, 15033, target: 100), World(1, [])]);
        Seek(.15);
        Check(dangers() == 1 && Shown("hydatos/aoe/").Length == 1 && Damage().Length == 0
            && (ulong)Read(Read(Read(Aoes()[0], "Target"), "Actor"), "Id") == 100,
            "Hydatos Daphne icon source identity buddy nav no fabricated Shared prediction");
        object[] Handles() => ((IEnumerable)Read(Script(), "areas")).Cast<object>()
            .Select(entry => Read(Read(entry, "Value"), "Item2")).ToArray();
        var initialHandles = Handles();
        for (var i = 0; i < 5; ++i)
            Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [0f]);
        Check(initialHandles.SequenceEqual(Handles()), "Hydatos unchanged buddy geometry recreated navigation handles");
        Seek(.25); Check(Aoes().Length == 2, "Hydatos duplicate icon deduplicated");
        Seek(.35); Check(Aoes().Length == 1, "Hydatos raw main target FIFO removed wrong duplicate");
        Seek(.45); Check(Aoes().Length == 0 && dangers() == 0, "Hydatos stack actual raw count finish");
        stop();
        Console.WriteLine("PASS native Hydatos Daphne source icon duplicates buddy navigation raw main target FIFO no fabricated Shared prediction");

        load(id, [Header(827), World(0, [
            Actor(1, 0x2765, center, combat: true), Actor(2, 0x2765, center, combat: true),
            Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 4)], true),
            World(.1, [Actor(2, 0x2765, center, combat: true, dead: true)]), World(.3, [])]);
        Seek(.15);
        var ceto = ((IEnumerable)Read(Region(), "Encounters")).Cast<object>()
            .Single(m => (string)Read(Read(m, "Definition"), "Name") == "Ceto");
        Check((bool)Read(ceto, "Completed") && (ulong)Read(Read(Read(ceto, "Primary"), "Actor"), "Id") == 2,
            "Hydatos retirement frame fixture did not retain completed primary");
        for (var i = 0; i < 5; ++i)
            manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = 15468 });
        Check((bool)Read(ceto, "Completed") && (ulong)Read(Read(Read(ceto, "Primary"), "Actor"), "Id") == 2,
            "Hydatos repeated event scan promoted pending during same world frame");
        Seek(.35);
        Check((ulong)Read(Read(Read(Model(), "Primary"), "Actor"), "Id") == 1,
            "Hydatos next frame failed pending promotion");
        stop();
        Console.WriteLine("PASS native Hydatos repeated event scans preserve retirement frame next framework pending promotion stable buddy handles");

        load(id, [Header(827), World(0, [
            Actor(1, 0x2685, center, combat: true),
            Actor(100, 0, center + Vector3.UnitX * 10), Actor(101, 0, center + Vector3.UnitX * 4),
            Actor(20, 0x233C, center, cast: Spell(14782), subKind: 5)], true),
            Icon(.1, 111, 101, 100), Raw(.2, 14788), Raw(.3, 14788), Raw(.4, 14788), World(1, [])]);
        Seek(.15);
        Check(Shown("hydatos/pull/").Length == 1 && Lines().Length == 1
            && (Vector3)Read(Lines()[0], "Start") == center + Vector3.UnitX * 10
            && (Vector3)Read(Lines()[0], "End") == center + Vector3.UnitX
            && dangers() == 0
            && Read(Script(), "MechanicHintsText") is string ion && ion.Contains("二值目标"),
            "Hydatos pull projection extended into movement or own binary goal silently broadened");
        Seek(.35);
        Check(Aoes().Any(a => Read(Read(a, "Rule"), "Kind").ToString() == "IonShower"), "Hydatos Ion cleared before third raw effect");
        Seek(.45);
        Check(!Aoes().Any(a => Read(Read(a, "Rule"), "Kind").ToString() == "IonShower"), "Hydatos Ion third raw effect not cleared");
        stop();
        Check(Lines().Length == 0, "Hydatos stop retained displacement lines");
        Console.WriteLine("PASS native Hydatos Ovni pull display only own binary goal diagnostic third raw zero-sequence effects clear spread");

        load(id, [Header(827), World(0, Initial(0x2740), true),
            World(.1, [Actor(20, 0x233C, center, cast: Spell(15909, .1))]),
            World(.2, [Actor(20, 0x233C, center, cast: Spell(15914, .2))]),
            Raw(.3, 15914), Raw(.4, 15913), World(1, [])]);
        Seek(.25);
        Check(Aoes().Length == 2 && dangers() == 1 && Shown("hydatos/aoe/").Length == 2, "Hydatos DualCast preview tail Risky");
        Seek(.35);
        Check(Aoes().Length == 1 && dangers() == 1 && (bool)Read(Aoes()[0], "Risky"), "Hydatos DualCast hidden tail promotion");
        Seek(.45); Check(Aoes().Length == 0 && dangers() == 0, "Hydatos DualCast real global FIFO");
        stop();
        Console.WriteLine("PASS native Hydatos Tristitia DualCast independent previews risky head hidden tail promotion source global FIFO");

        string perspective = World(3, []);
        var perspectiveNode = JsonNode.Parse(perspective)!;
        perspectiveNode["data"]!["LocalPlayerId"] = 101;
        load(id, [Header(827), World(0, [
            Actor(1, 0x2765, center, combat: true),
            Actor(100, 0, center - Vector3.UnitZ * 10), Actor(101, 0, center - Vector3.UnitZ * 60),
            Actor(20, 0x233C, center, cast: Spell(15469), subKind: 5)], true),
            perspectiveNode.ToJsonString(), World(4, [])]);
        Seek(2.7);
        Check(Read(Script(), "PlannedFacing") is float facing && MathF.Cos(facing) < .707107f
            && Shown("hydatos/eye/").Length == 1 && Shown("hydatos/gaze/view").Length == 1 && dangers() == 0,
            "Hydatos gaze failed actual eye view/facing without fabricated circle navigation");
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest"),
            "Hydatos gaze sent replay facing to PR");
        Seek(3.05);
        Check(Read(Script(), "PlannedFacing") == null && Shown("hydatos/gaze/view").Length == 0,
            "Hydatos out-of-range perspective retained previous personal gaze output");
        stop();
        Console.WriteLine("PASS native Hydatos actual gaze eye view facing range perspective switch replay execution isolation");

        load(id, [Header(827), World(0, [
            Actor(1, 0x2765, center, combat: true), Actor(100, 0, center), Actor(101, 0, center),
            Actor(20, 0x233C, center, cast: Spell(15466, target: 20), targetId: 101, subKind: 5)], true),
            World(.2, [Actor(20, 0x233C, center, cast: Spell(15466, target: 20), targetId: 100, subKind: 5)]),
            World(20, []), World(21, [Actor(20, 0x233C, center)]), World(22, [])]);
        Seek(.1);
        Check((ulong)Read(Damage().Single(), "TargetId") == 101, "Hydatos self-cast single target fallback");
        Seek(20.1);
        var sourceClockType = Assembly.Load("MasterPlayer.MechanicRuntime").GetType("BossMod.ActorCastInfo")!;
        var sourceClock = Activator.CreateInstance(sourceClockType)!;
        sourceClockType.GetField("TotalTime")!.SetValue(sourceClock, 3f);
        sourceClockType.GetField("ElapsedTime")!.SetValue(sourceClock, 3f);
        var hit = ((DateTime)runtime.CurrentTime).AddSeconds((float)sourceClockType.GetProperty("NPCRemainingTime")!.GetValue(sourceClock)!);
        Check((ulong)Read(Damage().Single(), "TargetId") == 100 && (DateTime)Read(Damage().Single(), "Activation") == hit
            && dangers() == 0 && Shown("hydatos/aoe/").Length == 0,
            "Hydatos dynamic damage identity/source float clock or diagnostic boundary");
        Seek(21.1);
        Check(Damage().Length == 0, "Hydatos real Finish retained single-target prediction");
        stop();
        Console.WriteLine("PASS native Hydatos actual single target self cast dynamic target saturated NPC clock diagnostic only raw Finish");

        var enmityActor = JsonSerializer.SerializeToNode(Actor(20, 0x233C, center + Vector3.UnitX * 2,
            combat: true, targetId: 999, isEnemy: true, objectIndex: 4))!;
        enmityActor["AggroPlayer"] = true;
        load(id, [Header(827), World(0, [
            Actor(1, 0x2686, center, combat: true, isEnemy: true, objectIndex: 2),
            Actor(100, 0, center), Actor(101, 0, center),
            Actor(30, 0x233C, center + new Vector3(1, 10, 0), combat: true, targetId: 100,
                stackedStatus: 671, isEnemy: true, objectIndex: 6),
            enmityActor], true), World(.2, [], removed: [30]), World(1, [])]);
        Seek(.1);
        Check((ulong)Read(Script(), "PlannedTargetId") == 30, "Hydatos Watcher active status filter or XZ target ranking");
        Seek(.25);
        Check((ulong)Read(Script(), "PlannedTargetId") == 20, "Hydatos Watcher lost enmity-only candidate targeting outsider");
        stop();
        Console.WriteLine("PASS native Hydatos actual Watcher active module status priority XZ nearest enmity-only target fallback replay planning");

        load(id, [Header(827), World(0, Initial(0x2765, Spell(15468)), true), World(4, [])]);
        Seek(.1);
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        dynamic pc = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        dynamic result = solver.Solve((dynamic)snapshot, pc, capability, options, runtime.CurrentTime, null);
        Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
            && Vector3.Distance((Vector3)Read((object)result, "TargetPosition"), center) > 18,
            "Hydatos actual solver failed R18 escape");
        var foreign = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.Geometry.CircleGeometry")!, [center, 60f])!;
        runtime.Manager.SetArenaBoundary((dynamic)foreign);
        manager.StopScript(id);
        Check(ReferenceEquals((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary, foreign),
            "Hydatos removed another owner's replacement boundary");
        runtime.Manager.ClearArenaBoundary();
        stop();
        Console.WriteLine("PASS native Hydatos actual SafeZoneSolver R18 escape no fallback exact owner boundary replacement cleanup");
    }
}
