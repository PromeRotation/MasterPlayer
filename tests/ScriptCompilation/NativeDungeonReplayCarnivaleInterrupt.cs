using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void CarnivaleInterruptBody(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Intents() => ((IEnumerable)Value(Encounter(), "CarnivaleInterrupts")).Cast<object>().ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: party);
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Carnivale interrupt replay leaked controls.");
        }

        foreach (var stage in new[]
        {
            (Oid: 0x25D5u, Name: "怪力铁巨人·第二幕", Actions: new uint[] { 14369 }),
            (Oid: 0x270Bu, Name: "炸弹怪喜剧·第二幕", Actions: new uint[] { 14680 }),
            (Oid: 0x2717u, Name: "王者的意志", Actions: new uint[] { 14719, 14720, 14721, 14724 }),
        })
        {
            foreach (var action in stage.Actions)
            {
                var raw = Cast(action, 6, center, target: 100, startedAt: 0);
                load(guid, [Header(796),
                    At(0, [Actor(1, stage.Oid, center, cast: raw, isEnemy: true, objectIndex: 2), Actor(100, 0, center)], true),
                    At(1, [Actor(1, stage.Oid, center, combat: true, cast: raw, isEnemy: true, objectIndex: 2)]),
                    At(2, [Actor(20, 0x9999, center, cast: raw, isEnemy: true, objectIndex: 4)]),
                    At(3, [Actor(20, 0x9999, center, cast: raw, dead: true, isEnemy: true, objectIndex: 4)]),
                    At(4, [Actor(1, stage.Oid, center, combat: true, cast: raw, targetable: false, isEnemy: true, objectIndex: 2)]),
                    EffectEvent(6.5, 1, action, 1, center, target: 100),
                    At(8, [Actor(1, stage.Oid, center, combat: true)]),
                    At(9, [], removed: [20]), At(10, [])]);
                controller.Seek(.5);
                Check(Value(Encounter(), "Active") == null && Intents().Length == 0 && dangers() == 0 && Goals() == 0,
                    "Pre-pull interrupt component active.");
                controller.Seek(1.5);
                var original = Intents().Single();
                Check((bool)Value(original, "ShouldBeInterrupted") && !(bool)Value(original, "ShouldBeStunned")
                    && (uint)Value(Value(Value(original, "Cast"), "Rule"), "Action") == action
                    && (DateTime)Value(Value(original, "Cast"), "StartedAt") == Start,
                    "Production interrupt source, flags, start or bootstrap lost.");
                controller.Seek(3.5);
                Check(Intents().Length == 2
                    && !Intents().Where(c => (ulong)Value(Value(c, "Cast"), "Source") == 20)
                        .Any(c => (bool)Value(c, "ShouldBeInterrupted")),
                    "Dead caster did not preserve list independently of enemy projection.");
                Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                    && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 60000 }),
                    "Dead source component leaked into generic AOE inference or suppressed unknown actions.");
                controller.Seek(7.5);
                Check(Intents().Length == 2 && Hints().Contains("Interrupt!")
                    && !Intents().Any(c => (bool)Value(c, "ShouldBeInterrupted"))
                    && dangers() == 0 && Shown("cast/").Length == 0 && Goals() == 0,
                    "Interrupt effect/deadline/targetability changed source component or invented navigation.");
                controller.Seek(8.5);
                Check(Intents().Length == 1 && Hints().Contains("Interrupt!"), "Finish consumed concurrent caster.");
                controller.Seek(9.5);
                Check(Intents().Length == 0 && !Hints().Contains("Interrupt!"), "Destroyed caster leaked.");
                controller.Seek(2.5);
                Check(Intents().Length == 2 && Intents().All(c => (bool)Value(c, "ShouldBeInterrupted")),
                    "Reverse Seek failed caster intent reconstruction.");
                Isolated();
                manager.SetMechanismEnabled(guid, stage.Name, false);
                controller.Seek(2.6);
                Check(Intents().Length == 0 && dangers() == 0 && previews().Length == 0, "Mechanism off leaked.");
                manager.SetMechanismEnabled(guid, stage.Name, true);
                stop();
            }
            Console.WriteLine($"PASS native Carnivale {stage.Oid:X} production all interrupt components bootstrap identity flags independent enemy projection concurrent casters observed finish deadlines reverse Seek replay isolation mechanism off owner");
        }

        foreach (var stage in new[]
        {
            (Oid: 0x25D5u, Actions: new uint[] { 14366, 14368 }),
            (Oid: 0x270Bu, Actions: new uint[] { 14708 }),
            (Oid: 0x2717u, Actions: new uint[] { 14725, 14726, 14727, 14728, 14722 }),
        })
        {
            foreach (var action in stage.Actions)
            {
                var locked = center + Vector3.UnitX * 3;
                var changed = locked + Vector3.UnitZ * 2;
                load(guid, [Header(796),
                    At(0, [Actor(1, stage.Oid, center, combat: true), Actor(100, 0, center)], true),
                    At(.1, [Actor(1, stage.Oid, center, combat: true,
                        cast: Cast(action, 3, locked, rotation: MathF.PI / 2))]),
                    At(1, [Actor(1, stage.Oid, center - Vector3.UnitZ * 10, combat: true,
                        cast: Cast(action, 3, locked, rotation: MathF.PI / 2), rotation: 2.8f)]),
                    At(2, [Actor(1, stage.Oid, center, combat: true,
                        cast: Cast(action, 3, changed, rotation: -.4f))]),
                    At(5, [Actor(1, stage.Oid, center, combat: true)]), At(6, [])]);
                controller.Seek(1.5);
                Check(dangers() == 1 && Shown("cast/").Length == 1 && Goals() == 0, "Actual AOE missing in dual output.");
                dynamic geometry = Value(Shown("cast/").Single(), "Geometry");
                Check((Vector3)geometry.Position == locked, "AOE followed actor position instead of cast lock.");
                var pending = Value(((IEnumerable)Value(Encounter(), "Casts")).Cast<object>().Single(), "Value");
                var directional = action == 14366 || action is >= 14725 and <= 14728;
                Check(!directional || (float)Value(pending, "Rotation") == MathF.PI / 2, "Cast rotation lock lost.");
                controller.Seek(2.5);
                geometry = Value(Shown("cast/").Single(), "Geometry");
                Check(dangers() == 1 && (Vector3)geometry.Position == changed, "True raw replacement lost relock.");
                controller.Seek(5.5);
                Check(dangers() == 0 && Shown("cast/").Length == 0, "Observed finish leaked actual AOE.");
                controller.Seek(1.5);
                Check(dangers() == 1, "Reverse Seek did not rebuild AOE.");
                Isolated();
                stop();
            }
            Console.WriteLine($"PASS native Carnivale {stage.Oid:X} production every simple AOE exact cast location rotation actor movement raw replacement double view navigation observed finish reverse Seek cleanup");
        }

        string King(double t, uint action, Vector3 location, bool full = false) => At(t,
            [Actor(1, 0x2717, center, combat: true, cast: Cast(action, 20, location, target: 100, startedAt: 0),
                isEnemy: true, objectIndex: 2), Actor(100, 0, center)], full);
        var cancel = JsonSerializer.Serialize(new
        {
            t = 2, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = 1ul, ActionId = 14719u, Timestamp = Start.AddSeconds(2) },
        });
        var stale = JsonSerializer.Serialize(new
        {
            t = 4.1, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0f,
            data = new { SourceId = 1ul, TargetId = 100ul, ActionId = 14719u, CastTime = 20f,
                EffectPosition = Pose(center), Timestamp = Start },
        });
        load(guid, [Header(796), King(0, 14719, center, true),
            CastEvent(.1, 1, 14719, 20, center + Vector3.UnitX, target: 100),
            King(1, 14719, center + Vector3.UnitZ), cancel,
            King(3, 14719, center + Vector3.UnitZ), King(4, 60000, center), stale,
            King(5, 14720, center), At(6, [])]);
        controller.Seek(.05);
        var initialGeneration = (long)Value(Intents().Single(), "Generation");
        controller.Seek(.5);
        Check((long)Value(Intents().Single(), "Generation") == initialGeneration
            && (Vector3)Value(Value(Intents().Single(), "Cast"), "Location") == center + Vector3.UnitX,
            "Production same generation packet correction changed identity.");
        controller.Seek(1.5);
        Check((long)Value(Intents().Single(), "Generation") > initialGeneration,
            "Production same time raw replacement reused cast generation.");
        controller.Seek(3.5);
        Check(Intents().Length == 0, "Cancelled unchanged raw resurrected interrupt.");
        controller.Seek(4.5);
        Check(Intents().Length == 0, "Unclaimed raw admitted late cancelled packet.");
        controller.Seek(5.5);
        Check((uint)Value(Value(Value(Intents().Single(), "Cast"), "Rule"), "Action") == 14720,
            "True raw from unknown to claimed did not rebuild interrupt.");
        controller.Seek(3.5);
        Check(Intents().Length == 0, "Reverse Seek lost cancel tombstone.");
        stop();
        Console.WriteLine("PASS native Carnivale production interrupt packet correction stable generation raw same time replacement fresh generation cancel unchanged raw unknown late packet restart reverse Seek");

        foreach (var cancelled in new[] { false, true })
        {
            var receipt = JsonSerializer.Serialize(new
            {
                t = 1, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = 2ul, ActionId = 14719u, Timestamp = Start.AddSeconds(1) },
            });
            var events = new List<string>
            {
                Header(796), At(0, [Actor(100, 0, center)], true),
                At(.1, [Actor(1, 0x25D5, center, combat: true)]),
                At(.2, [Actor(2, 0x2717, center, combat: true, isEnemy: true, objectIndex: 4,
                    cast: cancelled ? Cast(14719, 6, center, startedAt: .2) : null)]),
                cancelled ? receipt : CastEvent(1, 2, 14719, 6, center),
                At(1.05, [Actor(1, 0x25D5, center, dead: true)]), At(2, []),
            };
            load(guid, events);
            controller.Seek(1.1);
            Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x2717
                && (Intents().Length == 0) == cancelled,
                "Production display handoff lost packet grace or revived cancelled background raw.");
            if (!cancelled)
                Check(!(bool)Value(Value(Intents().Single(), "Cast"), "Observed"), "Handoff fabricated a raw cast.");
            controller.Seek(.5);
            Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x25D5 && Intents().Length == 0,
                "Reverse Seek mixed selected module output.");
            stop();
        }
        Console.WriteLine("PASS native Carnivale production display selection preserves cancelled background raw and unobserved packet grace across primary completion reverse Seek");

        foreach (var action in new uint[] { 14719, 14725 })
        {
            string FinishEvent(double at, double timestamp) => JsonSerializer.Serialize(new
            {
                t = at, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = 1ul, ActionId = action, Timestamp = Start.AddSeconds(timestamp) },
            });
            load(guid, [Header(796), King(0, action, center, true), King(1, action, center + Vector3.UnitZ),
                FinishEvent(1.1, .5), At(2, []), FinishEvent(2.1, 2.1), At(3, [])]);
            controller.Seek(1.5);
            Check(((IEnumerable)Value(Encounter(), "Casts")).Cast<object>().Count() == 1,
                "Queued old Finish consumed newer observed raw generation.");
            controller.Seek(2.5);
            Check(!((IEnumerable)Value(Encounter(), "Casts")).Cast<object>().Any(),
                "Current generation Finish was rejected.");
            stop();
        }
        var delayedStart = JsonSerializer.Serialize(new
        {
            t = 2, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0f,
            data = new { SourceId = 1ul, ActionId = 14719u, CastTime = 6f, EffectPosition = Pose(center), Timestamp = Start },
        });
        var delayedCancel = JsonSerializer.Serialize(new
        {
            t = 2.1, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = 1ul, ActionId = 14719u, Timestamp = Start.AddSeconds(1) },
        });
        load(guid, [Header(796), At(0, [Actor(1, 0x2717, center, combat: true), Actor(100, 0, center)], true),
            At(2, [Actor(1, 0x2717, center, combat: true, cast: Cast(14719, 6, center, startedAt: 0))]),
            delayedStart, delayedCancel,
            At(3, [Actor(1, 0x2717, center, combat: true, cast: Cast(14719, 6, center, startedAt: 0))]), At(4, [])]);
        controller.Seek(2.05);
        Check(Intents().Length == 1, "Delayed initial packet not accepted.");
        controller.Seek(2.5);
        Check(Intents().Length == 0, "Processing clock rejected genuine same-generation Cancel.");
        controller.Seek(3.5);
        Check(Intents().Length == 0, "Delayed Cancel failed to suppress old raw.");
        stop();
        Console.WriteLine("PASS native Carnivale production queued old finish cannot consume same time raw replacement for interrupt or AOE current finish still clears");

        var runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "MasterPlayer.MechanicRuntime");
        var sourceBounds = runtimeAssembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!.GetField("Layout2Corners")!.GetValue(null)!;
        var direction = runtimeAssembly.GetType("BossMod.WDir")!;
        load(guid, [Header(796), At(0, [Actor(1, 0x270B, center, combat: true), Actor(100, 0, center)], true), At(1, [])]);
        controller.Seek(.5);
        dynamic arena = Value(Encounter(), "Arena");
        Check(((IEnumerable)arena.GetContours()).Cast<object>().Count() == 3, "Corner holes lost.");
        var probes = new List<Vector3>();
        for (var ix = -110; ix <= 110; ++ix)
            for (var iz = -110; iz <= 110; ++iz)
                probes.Add(new(100 + ix * .25f + .137f, 0, 100 + iz * .25f + .271f));
        probes.AddRange([
            new(90, 0, 95.49f), new(90, 0, 95.51f), new(94.01f, 0, 92), new(93.99f, 0, 92),
            new(95.5003f, 0, 90), new(95.501f, 0, 90), new(95.4997f, 0, 94), new(95.499f, 0, 94),
            new(124.4993f, 0, 100), new(124.4995f, 0, 100)]);
        foreach (var p in probes)
            Check((bool)arena.Contains(p, 0f) == (bool)sourceBounds.GetType().GetMethod("Contains")!.Invoke(sourceBounds,
                [Activator.CreateInstance(direction, p.X - 100, p.Z - 100)])!, $"Corner exact source oracle at {p}.");
        stop();
        Console.WriteLine($"PASS native Carnivale Stage08 Act2 production two holes exact boolean inset and 89.98 degree thin wall {probes.Count} points source oracle");

        foreach (var oid in new uint[] { 0x25D5, 0x2717, 0x270B })
        {
            object Zero(object actor)
            {
                var node = JsonNode.Parse(JsonSerializer.Serialize(actor))!;
                node["Hp"] = 0;
                return node;
            }
            var add = oid == 0x270B ? 0x270Cu : 0x25D6u;
            load(guid, [Header(796), At(0, [Actor(1, oid, center), Actor(10, add, center, combat: true), Actor(100, 0, center)], true),
                At(1, [Actor(1, oid, center, combat: true, targetable: false)]),
                At(2, [Actor(1, oid, center, combat: true)]),
                At(3, [Zero(Actor(1, oid, center, combat: true))]),
                At(4, [Actor(1, oid, center, dead: true)]), At(5, [], removed: [10]), At(6, [])]);
            controller.Seek(.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x270B), "Primary-only vs trash pull.");
            controller.Seek(1.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x270B), "Untargetable primary-only pull.");
            controller.Seek(2.5);
            Check(Value(Encounter(), "Active") != null, "Primary failed pull.");
            controller.Seek(3.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x270B), "HP0 default death vs trash completion.");
            controller.Seek(4.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x270B), "Group ended with living bomb.");
            controller.Seek(5.5);
            Check(Value(Encounter(), "Active") == null && previews().Length == 0 && dangers() == 0, "Completion leaked.");
            controller.Seek(2.5);
            Check(Value(Encounter(), "Active") != null, "Lifecycle reverse Seek.");
            stop();
        }
        Console.WriteLine("PASS native Carnivale production primary targetability pull HP0 death and group trash surviving primary dead distinct lifecycle reverse Seek");
    }
}
