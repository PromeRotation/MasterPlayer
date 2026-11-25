using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleTyphon(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Values(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: party);
        string Cancel(double t, ulong source, uint action)
            => JsonSerializer.Serialize(new { t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(uint oid = 0x272C, bool combat = true) =>
        [
            Actor(1, oid, center, combat: combat, objectIndex: 2, isEnemy: true),
            Actor(2, 0x233C, center), Actor(3, 0x233C, center), Actor(100, 0, center + Vector3.UnitZ * 10),
        ];
        object[] Movements(uint action)
        {
            var actor = ((IDictionary)Value(Encounter(), "Actors"))[100ul]!;
            return ((IEnumerable)Encounter().GetType().GetMethod("CarnivaleCastKnockbackMovements", flags)!
                .Invoke(Encounter(), [actor, action])!).Cast<object>().ToArray();
        }
        Vector3 From(object movement) => (Vector3)movement.GetType().GetField("Item2")!.GetValue(movement)!;
        Vector3 To(object movement) => (Vector3)movement.GetType().GetField("Item3")!.GetValue(movement)!;
        var sourceAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var layouts = sourceAssembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!;
        var wdir = sourceAssembly.GetType("BossMod.WDir")!;
        foreach (var oid in new uint[] { 0x272A, 0x272B, 0x272C })
        {
            load(guid, [Header(796), At(0, Initial(oid, false), true),
                At(.1, [Actor(4, 0x272D, center, combat: true), Actor(5, 0x272E, center)]),
                At(1, [Actor(1, oid, center, combat: true)]),
                At(2, [Actor(1, oid, center, dead: true)]), At(3, [])]);
            controller.Seek(.5);
            Check(Value(Encounter(), "Active") == null && Value(Encounter(), "CarnivalePrecombatHint") != null
                && Hints().Any(h => h.StartsWith("Requirement for achievement:")) == (oid != 0x272B), "Typhon preparation/achievement.");
            controller.Seek(1.5);
            Check(Shown("carnivale/enemy/").Length == (oid == 0x272C ? 3 : 1)
                && !Hints().Any(h => h.StartsWith("Requirement for achievement:")), "Typhon markers/precombat scope.");
            if (oid == 0x272C)
                Check((Vector4)Value(Shown("carnivale/enemy/4").Single(), "Color") == new Vector4(1, 128f / 255, 0, 1)
                    && (Vector4)Value(Shown("carnivale/enemy/5").Single(), "Color") == new Vector4(1, 128f / 255, 0, 1), "Typhon object colours.");
            var bounds = layouts.GetField(oid == 0x272A ? "CircleBig" : "CircleSmall", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
            dynamic arena = Value(Encounter(), "Arena");
            var count = 0;
            for (var x = 74.13f; x < 126; x += .67f)
                for (var z = 74.17f; z < 126; z += .71f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(wdir, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Typhon source arena.");
                    ++count;
                }
            controller.Seek(2.5);
            Check(Value(Encounter(), "Active") == null && previews().Length == 0 && dangers() == 0, "Typhon Primary completion.");
            controller.Seek(1.5);
            var name = "著名组合——奥尔特罗斯&提丰·" + (oid == 0x272A ? "第一幕" : oid == 0x272B ? "第二幕" : "第三幕");
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(1.6);
            Check(previews().Length == 0 && dangers() == 0, "Typhon owner disabled.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage20 {oid:X} source arena oracle {count} points primary lifecycle preparation achievement default enemy colours reverse Seek disabled owner");
        }
        foreach (var (oid, action, count) in new[]
        {
            (0x272Au, 14706u, 1), (0x272Bu, 14713u, 1), (0x272Bu, 14714u, 1),
            (0x272Bu, 14718u, 1), (0x272Bu, 14717u, 1), (0x272Cu, 14745u, 1),
            (0x272Cu, 14746u, 1), (0x272Cu, 14748u, 1),
        })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, .3f))]),
                EffectEvent(7, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(.5);
            Check(dangers() == count && Shown(action == 14706 ? "/typhon/fireball/" : "cast/").Length == 1
                && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action }),
                "Typhon ordinary native dual output.");
            controller.Seek(8.5);
            Check(dangers() == count, "Typhon ordinary predicted deadline/effect lifetime.");
            controller.Seek(9.5);
            Check(dangers() == 0 && Shown("cast/").Length == 0 && Shown("/typhon/fireball/").Length == 0, "Typhon observed Finish.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage20 all eight ordinary component instances native navigation dual view observed lifetime exact catalog dimensions reverse Seek");

        var lines = new List<string>
        {
            Header(796), At(0, Initial(0x272A), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14704, 7, center))]),
        };
        var points = Enumerable.Range(0, 180).Select(i => center +
            new Vector3(MathF.Sin(i * MathF.Tau / 180) * 13, 0, MathF.Cos(i * MathF.Tau / 180) * 13)).ToArray();
        for (var i = 0; i < points.Length; ++i)
            lines.Add(At(i + 1, [Actor(100, 0, points[i])]));
        lines.Add(Cancel(182, 2, 14704));
        lines.Add(At(183, []));
        load(guid, lines);
        var big = layouts.GetField("CircleBig", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
        var polygon = big.GetType().GetField("Polygon")!.GetValue(big)!;
        var parts = ((IEnumerable)polygon.GetType().GetField("Parts")!.GetValue(polygon)!).Cast<object>().ToArray();
        Check(parts.Length == 1, "CircleBig source polygon parts.");
        var vertices = ((IEnumerable)parts[0].GetType().GetField("Vertices")!.GetValue(parts[0])!).Cast<object>().ToArray();
        var raySegment = sourceAssembly.GetType("BossMod.Intersect")!.GetMethods()
            .Single(m => m.Name == "RaySegment" && m.GetParameters()[0].ParameterType.GetElementType() == wdir);
        var optimizedRayMismatches = 0;
        for (var i = 0; i < points.Length; ++i)
        {
            controller.Seek(i + 1.1);
            var direction = Vector3.Normalize(points[i] - center);
            var origin = Activator.CreateInstance(wdir, [points[i].X - 100, points[i].Z - 100])!;
            var dir = Activator.CreateInstance(wdir, [direction.X, direction.Z])!;
            // The source indexed raycaster excludes both edges at the top vertex.
            // Its inclusive RaySegment and the actual source vertices are the independent wall oracle.
            var ray = Enumerable.Range(0, vertices.Length).Select(j => (float)raySegment.Invoke(null,
                [origin, dir, vertices[j], vertices[(j + 1) % vertices.Length]])!).Min();
            var optimized = (float)big.GetType().GetMethod("IntersectRay")!.Invoke(big, [origin, dir])!;
            if (MathF.Abs(optimized - ray) > .003f) ++optimizedRayMismatches;
            if (i == 0)
                Check(optimized == float.MaxValue && MathF.Abs(ray - 11.5f) < .001f,
                    "Source indexed top-vertex regression changed; review oracle discrepancy.");
            var expected = points[i] + direction * Math.Min(30, ray - (.5f - .499f));
            var actual = Movements(14704).Single();
            Check(Vector3.Distance(expected, To(actual)) < .003f
                && From(actual) == points[i] && dangers() == 0 && Shown("/typhon/kb/").Length != 0,
                $"Snort source polygon ray/hitbox oracle i={i} expected={expected} actual={To(actual)} from={From(actual)} wanted={points[i]} dangers={dangers()} previews={Shown("/typhon/kb/").Length}.");
        }
        controller.Seek(182.5);
        Check(Values("CarnivaleCastKnockbacks").Length == 0 && Shown("/typhon/kb/").Length == 0, "Snort Finish.");
        controller.Seek(.5);
        Check(Hints().Contains("Use Diamondback!") && Values("PredictedDamage").Length == 0, "Snort hint versus raidwide scope.");
        stop();
        Console.WriteLine($"PASS native Carnivale Stage20 first Snort source vertices inclusive RaySegment oracle 180 positions indexed source discrepancies={optimizedRayMismatches} hitbox inset locked model wall stop persistent lifetime preview only no damage reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14748, 4, center)),
                Actor(3, 0x233C, center, cast: Cast(14704, 4, center))]),
            At(1, [Actor(4, 0x233C, center, cast: Cast(14748, 4, center + Vector3.UnitZ * 20, startedAt: 1))]),
            At(2, [Actor(100, 0, center + Vector3.UnitX * 6 + Vector3.UnitZ * 10)]),
            Cancel(3, 3, 14704), At(4, [])]);
        controller.Seek(.5);
        Check(To(Movements(14748).Single()).Z == 130 && To(Movements(14704).Single()).Z == 140
            && dangers() == 1 && Hints().Contains("About to be knocked into danger!"), "Independent no-wall chains.");
        controller.Seek(1.5);
        var wallops = Movements(14748);
        Check(wallops.Length == 2 && From(wallops[1]) == To(wallops[0]) && To(wallops[1]).Z == 150, "Wallop chain shape intermediate.");
        controller.Seek(2.5);
        Check(Movements(14748).Length == 0 && Movements(14704).Length == 1, "Wallop rectangle eligibility.");
        controller.Seek(3.5);
        Check(Movements(14704).Length == 0 && Values("CarnivaleCastKnockbacks").Length == 2, "KB Finish isolation.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage20 third independent Snort Wallop chains radial directions intermediate rectangle eligibility no wall stop real Finish reverse Seek");

        var immuneCast = Cast(14704, 4, center);
        var equalExpiry = JsonSerializer.SerializeToElement(immuneCast).GetProperty("FinishAt").GetDateTime();
        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: immuneCast)]),
            At(1, [Actor(100, 0, center + Vector3.UnitZ * 10, stackedStatus: 1722, statusExpiresAt: equalExpiry)]),
            At(2, [Actor(100, 0, center + Vector3.UnitZ * 10, stackedStatus: 1722, statusExpiresAt: Start.AddSeconds(6))]),
            At(3, [Actor(100, 0, center + Vector3.UnitZ * 10)]), At(4, [])]);
        controller.Seek(1.5);
        Check(Movements(14704).Length == 1, "Equal expiry immune.");
        controller.Seek(2.5);
        Check(Movements(14704).Length == 0 && Hints().Contains("Use Diamondback!") && Shown("/typhon/kb/").Length == 0,
            "Diamondback raw state immune.");
        controller.Seek(3.5);
        Check(Movements(14704).Length == 1, "Immunity loss.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage20 real raw Diamondback immunity strict expiry removes projection retains cast hint loss restores no auto action");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14706, 9, center)),
                Actor(3, 0x233C, center, cast: Cast(14747, 3, center + Vector3.UnitZ * 5))]),
            Cancel(1, 2, 60000), At(2, [])]);
        controller.Seek(.5);
        Check(Shown("/typhon/fireball/").Length == 2 && dangers() == 2 && Shown("cast/").Length == 0, "Shared group duplicated/leaked ordinary shape.");
        controller.Seek(1.5);
        Check(Shown("/typhon/fireball/").Length == 1 && dangers() == 1, "Shared group any action Finish.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage20 third shared Fireball Tentacle group single outputs arbitrary action Finish double view navigation reverse Seek");

        load(guid, [Header(796), At(0, Initial(0x272A), true),
            At(.05, [Actor(4, 0x272C, center, combat: true)]),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14704, 7, center)),
                Actor(3, 0x233C, center, cast: Cast(14706, 4, center))]),
            At(.2, [Actor(1, 0x272A, center, dead: true)]), Cancel(.3, 2, 14704), At(1, [])]);
        controller.Seek(.15);
        Check(To(Movements(14704).Single()).Z < 125 && dangers() == 1, "Foreground wall.");
        controller.Seek(.25);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x272C
            && To(Movements(14704).Single()).Z == 140 && dangers() == 1
            && Shown("/typhon/fireball/").Length == 1, "Background wall configuration/shared Fireball input.");
        controller.Seek(.5);
        Check(Movements(14704).Length == 0 && dangers() == 1, "Background Snort Finish removed group.");
        controller.Seek(.15);
        stop();
        Console.WriteLine("PASS native Carnivale Stage20 background shared Snort separate wall policy shared Fireball retained no duplicate rendering real Finish immediate handoff reverse Seek");

        foreach (var (oid, action) in new[] { (0x272Bu, 14712u), (0x272Cu, 14744u) })
        {
            object Named(string name)
            {
                var actor = JsonSerializer.SerializeToNode(Actor(2, 0x233C, center, cast: Cast(action, 6, center)))!;
                actor["Name"] = name;
                return actor;
            }
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Named("A")]), At(.16, [Named("B")]),
                At(.2, [Actor(3, 0x233C, center, cast: Cast(action, 6, center))]),
                Cancel(1, 3, action), Cancel(2, 2, action), At(3, [])]);
            controller.Seek(.15);
            Check(Values("CarnivaleInterrupts").Length == 1 && Hints().Contains(action == 14712 ? "Interrupt!" : "Interrupt A!"), "Single interrupt name scope.");
            controller.Seek(.18);
            Check(Hints().Contains(action == 14712 ? "Interrupt!" : "Interrupt A!"), "Rename changed event-cached interrupt.");
            controller.Seek(.5);
            Check(Values("CarnivaleInterrupts").Length == 2 && Hints().Contains("Interrupt!"), "Multiple interrupts.");
            controller.Seek(1.5);
            Check(Values("CarnivaleInterrupts").Length == 1 && dangers() == 0
                && Hints().Contains(action == 14712 ? "Interrupt!" : "Interrupt B!"), "Interrupt Finish count/name/navigation.");
            controller.Seek(2.5);
            Check(Values("CarnivaleInterrupts").Length == 0, "Interrupt full Finish.");
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage20 ImpSong named single unnamed multiple independent component intent no invented damage automatic interrupt or execution");
    }
}
