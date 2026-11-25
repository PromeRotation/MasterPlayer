using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void CarnivaleSlimeBombBody(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        var center = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: party);
        foreach (var stage in new[]
        {
            (Oid: 0x2703u, Add: 0x2702u, Member: 0x2703u, Name: "粘液合唱团·第一幕", Initial: 1),
            (Oid: 0x2705u, Add: 0x2704u, Member: 0x2705u, Name: "粘液合唱团·第二幕", Initial: 2),
            (Oid: 0x2708u, Add: 0x2709u, Member: 0x270Au, Name: "炸弹怪喜剧·第一幕", Initial: 3),
            (Oid: 0x270Bu, Add: 0x270Cu, Member: 0x270Du, Name: "炸弹怪喜剧·第二幕", Initial: 2),
        })
        {
            load(guid, [Header(796), At(0,
                [Actor(1, stage.Oid, center), Actor(10, stage.Add, center + Vector3.UnitX * 15),
                    Actor(20, stage.Member, center + Vector3.UnitZ * 10), Actor(100, 0, center)], true),
                At(1, [Actor(10, stage.Add, center + Vector3.UnitX * 15, combat: true)]),
                At(2, [Actor(20, stage.Member, center + Vector3.UnitZ * 12)]),
                At(3, [], removed: [20]),
                At(4, [Actor(20, stage.Member, center + Vector3.UnitZ * 20)]),
                At(5, [Actor(20, stage.Member, center + Vector3.UnitZ * 20, dead: true)]),
                At(6, [Actor(20, stage.Member, center + Vector3.UnitZ * 20)]),
                At(7, [Actor(1, stage.Oid, center, dead: true)]),
                At(8, [Actor(10, stage.Add, center + Vector3.UnitX * 15, dead: true),
                    Actor(20, stage.Member, center + Vector3.UnitZ * 20, dead: true)]),
                At(9, [])]);
            controller.Seek(.5);
            Check(Value(Encounter(), "Active") == null && Shown("carnivale/explosion/").Length == stage.Initial
                && Shown("carnivale/precombat/arena").Length == 1 && dangers() == 0 && Goals() == 0,
                $"Carnivale {stage.Oid:X} constructor outputs.");
            foreach (var item in Shown("carnivale/explosion/"))
                Check((bool)Value(item, "OutlineOnly") && (Vector4)Value(item, "Color") == new Vector4(1, 1, 0, 1),
                    "Explosion display changed source outline/colour.");
            controller.Seek(1.5);
            Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == stage.Oid
                && dangers() == 0 && Goals() == 0 && Shown("carnivale/precombat/arena").Length == 0,
                "Explosion preview became navigation or group failed pull.");
            dynamic snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            Check(snapshot.ArenaBoundary != null, "Group pull did not publish actual arena.");
            if (stage.Oid != 0x2703)
            {
                controller.Seek(2.5);
                var frozen = Shown("carnivale/explosion/").Single(a =>
                    Vector3.Distance((Vector3)((dynamic)Value(a, "Geometry")).Position, center + Vector3.UnitZ * 12) < .001f);
                var frozenId = (string)Value(frozen, "Id");
                controller.Seek(4.5);
                Check(Shown("carnivale/explosion/").Length == stage.Initial + 1
                    && Shown("carnivale/explosion/").Any(a => (string)Value(a, "Id") == frozenId),
                    "Destroyed explosion was removed/rebound same-ID birth.");
                controller.Seek(6.5);
                Check(Shown("carnivale/explosion/").Length == stage.Initial
                    && Shown("carnivale/explosion/").Any(a => (string)Value(a, "Id") == frozenId),
                    "Death/revival resurrected component entry or deleted wrong generation.");
            }
            controller.Seek(7.5);
            Check(Value(Encounter(), "Active") != null, "Primary death ended living group.");
            controller.Seek(8.5);
            Check(Value(Encounter(), "Active") == null && previews().Length == 0 && dangers() == 0 && Goals() == 0,
                "AllDeadOrDestroyed leaked reference previews.");
            controller.Seek(4.5);
            Check(Shown("carnivale/explosion/").Length == stage.Initial + (stage.Oid == 0x2703 ? 0 : 1),
                "Reverse Seek did not reconstruct original generation list.");
            var hints = Value(Script(), "Hints");
            Check(!(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld")
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any(),
                "Slime/bomb replay leaked action restrictions or movement.");
            manager.SetMechanismEnabled(guid, stage.Name, false);
            controller.Seek(4.6);
            Check(previews().Length == 0 && dangers() == 0 && Goals() == 0, "Mechanism off leaked outputs.");
            manager.SetMechanismEnabled(guid, stage.Name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale {stage.Oid:X} production constructor yellow outline no navigation group pull original reference destroy death revival same ID generations reverse Seek mechanism off and owner cleanup");
        }

        var sourceAssembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "MasterPlayer.MechanicRuntime");
        var source = sourceAssembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!.GetField("Layout4Quads")!.GetValue(null)!;
        var direction = sourceAssembly.GetType("BossMod.WDir")!;
        var contains = source.GetType().GetMethod("Contains")!;
        load(guid, [Header(796), At(0,
            [Actor(1, 0x2705, center), Actor(10, 0x2704, center, combat: true), Actor(100, 0, center)], true), At(1, [])]);
        controller.Seek(.5);
        dynamic arena = Value(Encounter(), "Arena");
        Check(((IEnumerable)arena.GetContours()).Cast<object>().Count() == 5, "Production four holes missing.");
        var samples = 0;
        for (var ix = -110; ix <= 110; ++ix)
            for (var iz = -110; iz <= 110; ++iz)
            {
                var p = new Vector3(100 + ix * .25f + .137f, 0, 100 + iz * .25f + .271f);
                Check((bool)arena.Contains(p, 0f) == (bool)contains.Invoke(source,
                    [Activator.CreateInstance(direction, p.X - 100, p.Z - 100)])!,
                    $"Production four-quad source oracle {ix},{iz}.");
                ++samples;
            }
        foreach (var p in new Vector3[]
        {
            new(110, 0, 113.18f), new(110, 0, 113.20f), new(113.30f, 0, 110),
            new(111.6f, 0, 111.6f), new(124.4993f, 0, 100), new(124.4995f, 0, 100),
        })
        {
            Check((bool)arena.Contains(p, 0f) == (bool)contains.Invoke(source,
                [Activator.CreateInstance(direction, p.X - 100, p.Z - 100)])!,
                "Production inset/rounded corner boundary source oracle.");
            ++samples;
        }
        stop();
        Console.WriteLine($"PASS native Carnivale Stage07 Act2 production exact four hole inset arena {samples} points source oracle");

        load(guid, [Header(796), At(0,
            [Actor(1, 0x2708, center + Vector3.UnitY * 20, isEnemy: true, objectIndex: 2),
                Actor(10, 0x2709, center + Vector3.UnitX * 15, combat: true),
                Actor(30, 0x9999, center, isEnemy: true, objectIndex: 198),
                Actor(100, 0, center)], true),
            At(1, [Actor(1, 0x2708, center + Vector3.UnitY * 20, isEnemy: true, objectIndex: 2, targetable: false)]),
            At(2, [])]);
        controller.Seek(.5);
        var choose = Encounter().GetType().GetMethod("PriorityTarget")!;
        Check((ulong)choose.Invoke(Encounter(), [30ul, center])! == 1, "Production bomb priority did not override high ground.");
        controller.Seek(1.5);
        Check((ulong)choose.Invoke(Encounter(), [0ul, center])! == 30,
            "Production bomb priority omitted unenumerated valid enemy.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage08 Act1 production primary priority one all other candidates zero and native slot filtering");

        foreach (var pendingDied in new[] { false, true })
        {
            load(guid, [Header(796), At(0, [Actor(100, 0, center)], true),
                At(.1, [Actor(1, 0x2705, center), Actor(10, 0x2704, center + Vector3.UnitX * 15)]),
                At(1, [Actor(2, 0x2705, center + Vector3.UnitZ * 10)]),
                At(2, [Actor(2, 0x2705, center + Vector3.UnitZ * 10, dead: pendingDied)]),
                At(3, [Actor(2, 0x2705, center + Vector3.UnitZ * 10)]),
                At(4, [], removed: [1]),
                At(5, [Actor(100, 0, center + Vector3.UnitX * .1f)]),
                At(6, [Actor(10, 0x2704, center + Vector3.UnitX * 15, combat: true)]),
                At(7, [])]);
            controller.Seek(3.5);
            Check(Shown("carnivale/explosion/").Length == (pendingDied ? 1 : 2), "Pending Death changed selected reference history.");
            if (!pendingDied)
            {
                controller.Seek(4.01);
                Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.016f]);
                Check(Shown("carnivale/explosion/").Length == 2 && Value(Encounter(), "CarnivalePrecombatHint") != null,
                    "Pending handoff stalled between recorded object frames.");
            }
            controller.Seek(5.5);
            if (pendingDied)
                Check(previews().Length == 0 && Value(Encounter(), "Active") == null,
                    "Dead pending owner resurrected without new birth.");
            else
                Check(Shown("carnivale/explosion/").Length == 2
                    && Shown("carnivale/explosion/").Any(a =>
                        Vector3.Distance((Vector3)((dynamic)Value(a, "Geometry")).Position, center) < .001f),
                    "Promoted constructor component lost destroyed predecessor reference.");
            controller.Seek(6.5);
            Check(pendingDied
                ? Value(Encounter(), "Active") == null
                : (ulong)Value(Encounter(), "PrimaryId") == 2, "Pending group pull ignored module lifetime.");
            controller.Seek(3.5);
            Check(Shown("carnivale/explosion/").Length == (pendingDied ? 1 : 2), "Pending history reverse Seek mismatch.");
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage07 Act2 production pending constructor history surviving primary destruction dead pending discard no revival group handoff and reverse Seek");

        load(guid, [Header(796), At(0,
            [Actor(1, 0x2703, center), Actor(2, 0x2703, center + Vector3.UnitX * 10),
                Actor(100, 0, center)], true),
            At(1, []), At(2, [])]);
        controller.Seek(.5);
        dynamic primaryCircle = Value(Shown("carnivale/explosion/primary").Single(), "Geometry");
        Check((Vector3)primaryCircle.Position == center + Vector3.UnitX * 10,
            "Production cold pending reverse order chose first sibling.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage07 Act1 production cold initial party reverse pending promotion preserves exact fixed primary");

        load(guid, [Header(796),
            Frame(796, 0, [Actor(1, 0x2705, center), Actor(100, 0, center)], full: true, party: []),
            Frame(796, 1, [], removed: [1], party: []),
            At(2, [Actor(1, 0x2705, center + Vector3.UnitZ * 10)]),
            At(3, [Actor(1, 0x2705, center + Vector3.UnitZ * 10, dead: true)]),
            At(4, [Actor(10, 0x2704, center, combat: true)]), At(5, [])]);
        controller.Seek(2.5);
        Check(Shown("carnivale/explosion/").Length == 1, "Production no-party pending reused destroyed generation.");
        controller.Seek(4.5);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x2705,
            "Old pending disposal deleted same-ID new loaded module.");
        stop();

        load(guid, [Header(796), At(0,
            [Actor(1, 0x2703, center), Actor(10, 0x2702, center + Vector3.UnitX * 15, combat: true),
                Actor(100, 0, center)], true),
            At(1, [], removed: [1]),
            At(2, [Actor(1, 0x2708, center + Vector3.UnitZ * 10)]),
            At(3, [Actor(10, 0x2702, center + Vector3.UnitX * 15, dead: true)]),
            At(4, [Actor(1, 0x2708, center + Vector3.UnitZ * 10, combat: true)]), At(5, [])]);
        controller.Seek(2.5);
        primaryCircle = Value(Shown("carnivale/explosion/primary").Single(), "Geometry");
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x2703
            && (Vector3)primaryCircle.Position == center && (float)primaryCircle.Radius == 7.6f,
            "Production same-ID different OID birth rebound old started module.");
        controller.Seek(3.5);
        primaryCircle = Value(Shown("carnivale/explosion/primary").Single(), "Geometry");
        Check(Value(Encounter(), "Active") == null && (float)primaryCircle.Radius == 10,
            "Production same-ID module handoff retained old Active or inherited Started.");
        controller.Seek(4.5);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x2708, "Independent new OID could not pull.");
        controller.Seek(2.5);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x2703, "Cross-OID module reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native Carnivale production no-party old pending same ID generations remain distinct and loaded cross OID birth independent activation handoff reverse Seek");
    }
}
