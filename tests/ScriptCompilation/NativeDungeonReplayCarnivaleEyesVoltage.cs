using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleEyesVoltage(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter()
        {
            Check(manager.Running.ContainsKey(guid), $"Eyes/Voltage stopped: {manager.ErrorMessage}");
            return Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        }
        object[] Values(string property) => ((IEnumerable)Value(Encounter(), property)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center + Vector3.UnitZ * 3])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null, object[]? party = null)
            => Frame(796, t, actors, full: full, removed: removed, party: party ?? [new { Slot = 0, EntityId = 100ul }]);
        string Cancel(double t, ulong source, uint action) => JsonSerializer.Serialize(new { t, kind = "CancelCast",
            type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(uint oid, bool combat = false) => [Actor(1, oid, center, combat: combat, objectIndex: 2, isEnemy: true),
            Actor(2, 0x233C, center), Actor(100, 0, center + Vector3.UnitZ * 3, rotation: MathF.PI)];
        float? Facing() => (float?)Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [center + Vector3.UnitZ * 3, MathF.PI]);
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"), "Eye/Voltage replay leaked controls.");
        }
        Vector3[] WorldFill(object geometry)
        {
            var assembly = geometry.GetType().Assembly;
            var host = assembly.GetType("MasterPlayer.Radar.HostDrawingGeometry")!.GetMethod("Convert")!.Invoke(null, [geometry])!;
            var batches = Activator.CreateInstance(typeof(List<>).MakeGenericType(assembly.GetType("MasterPlayer.Drawing.RenderBatch")!))!;
            var item = new PromeRotation.Spatial.Drawing.DrawRenderItem("last-song",
                (PromeRotation.Spatial.Geometry.IGeometryShape)host, new PromeRotation.Spatial.Drawing.DrawStyle(Vector4.One, null));
            assembly.GetType("MasterPlayer.Drawing.MeshBuilder")!.GetMethod("AddGeometry", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [batches, item, Vector4.Zero, 0f]);
            return ((IEnumerable)batches).Cast<object>().SelectMany(b =>
                ((IEnumerable)Value(b, "Vertices")).Cast<object>().Select(v => (Vector3)v.GetType().GetField("Position", flags)!.GetValue(v)!)).ToArray();
        }
        bool Filled(Vector3[] triangles, Vector3 p)
        {
            float Cross(Vector3 a, Vector3 b, Vector3 c) => (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i]; var b = triangles[i + 1]; var c = triangles[i + 2];
                var winding = MathF.Sign(Cross(a, b, c));
                if (winding != 0 && winding * Cross(a, b, p) >= 0 && winding * Cross(b, c, p) >= 0 && winding * Cross(c, a, p) >= 0)
                    return true;
            }
            return false;
        }
        var sourceAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var layouts = sourceAssembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!;
        var directionType = sourceAssembly.GetType("BossMod.WDir")!;
        var sourceCenter = Activator.CreateInstance(sourceAssembly.GetType("BossMod.WPos")!, [100f, 100f])!;
        object Direction(Vector3 point) => Activator.CreateInstance(directionType, [point.X - 100, point.Z - 100])!;
        object Oracle(string layout, Vector3 origin)
        {
            var blockers = layouts.GetField(layout + "Blockers", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
            var vertices = blockers.GetType().GetMethod("Visibility")!.Invoke(blockers, [Direction(origin)])!;
            var polygonShape = Activator.CreateInstance(sourceAssembly.GetType("BossMod.PolygonCustomRel")!, [vertices])!;
            var shapes = Array.CreateInstance(sourceAssembly.GetType("BossMod.Shape")!, 1);
            shapes.SetValue(polygonShape, 0);
            var constructor = sourceAssembly.GetType("BossMod.AOEShapeCustom")!.GetConstructors().Single();
            var args = constructor.GetParameters().Select(p => p.DefaultValue).ToArray();
            args[0] = shapes;
            args[4] = sourceCenter;
            var shape = constructor.Invoke(args);
            return shape.GetType().GetMethod("GetCombinedPolygon")!.Invoke(shape, [sourceCenter])!;
        }


        foreach (var (oid, name, layout) in new[] { (0x25CDu, "铁鹰战士·第一幕", "LayoutBigQuad"),
            (0x26FFu, "铁鹰战士·第二幕", "CircleBig"), (0x2706u, "粘液怪合唱团·第三幕", "Layout2Corners") })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(1, oid, center, combat: true)]), At(1, [Actor(1, oid, center, dead: true)]), At(2, [])]);
            controller.Seek(.01);
            Check(Value(Encounter(), "Active") == null && Shown("carnivale/precombat/arena").Length == 1 && dangers() == 0,
                "Precombat navigation.");
            controller.Seek(.5);
            dynamic arena = Value(Encounter(), "Arena");
            var bounds = layouts.GetField(layout)!.GetValue(null)!;
            var points = 0;
            for (var x = 74.137f; x < 126; x += .67f)
                for (var z = 74.173f; z < 126; z += .71f)
                {
                    var p = new Vector3(x, 0, z);
                    Check((bool)arena.Contains(p, 0f) == (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds, [Direction(p)])!,
                        "Eyes/Voltage source arena mismatch.");
                    ++points;
                }
            controller.Seek(1.5);
            Check(Value(Encounter(), "Active") == null, "Group completion.");
            controller.Seek(.5);
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(.6);
            Check(previews().Length == 0 && dangers() == 0, "Disabled owner.");
            manager.SetMechanismEnabled(guid, name, true);
            Isolated();
            stop();
            Console.WriteLine($"PASS native Carnivale Eyes Voltage {oid:X} exact source {layout} {points} points precombat display group lifecycle reverse Seek disabled owner");
        }

        foreach (var oid in new uint[] { 0x25CD, 0x26FF })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(14691, 4, center)),
                    Actor(3, oid == 0x25CD ? 0x2700u : 0x2701u, center, cast: Cast(14693, 3, center, target: 3))]),
                At(4, [Actor(1, oid, center, combat: true)]), Cancel(5, 2, 14691), Cancel(5.1, 3, 14693), At(6, [])]);
            controller.Seek(3.8);
            Check(Values("Gazes").Length == 1 && Values("CarnivaleEyeStacks").Length == 1
                && Shown("carnivale/gaze/").Length == 3 && Shown("carnivale/eyes/stack/").Length == 1
                && dangers() == 0 && Facing() == null && Hints().Contains("Stack to get blinded!") && !Hints().Contains("Stack!"),
                "Constructor components or solo semantics.");
            controller.Seek(4.1);
            Check(Values("Gazes").Length == 1 && Values("CarnivaleEyeStacks").Length == 1
                && Facing() != null && dangers() == 0 && Values("PredictedDamage").Length == 0, "Pull duplicated or expanded solo stack navigation.");
            Isolated();
            controller.Seek(5.5);
            Check(Values("Gazes").Length == 0 && Values("CarnivaleEyeStacks").Length == 0, "Real Finish.");
            controller.Seek(3.8);
            stop();
            Console.WriteLine($"PASS native Carnivale Eyes {oid:X} constructor gaze stack true eye pupil view arc solo radius6point3 no movement no generic Stack combat PR facing model cancel reverse Seek replay isolation");
        }

        load(guid, [Header(796), At(0, Initial(0x26FF, true), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14691, 5, center)),
                Actor(3, 0x2701, center, cast: Cast(14693, 5, center, target: 3)),
                Actor(4, 0x233C, center, cast: Cast(14692, 5, center, MathF.PI / 2)),
                Actor(5, 0x25CE, center, cast: Cast(14694, 5, center, MathF.PI / 2))]),
            At(1, [Actor(100, 0, center + Vector3.UnitZ * 3, stackedStatus: 571, statusExpiresAt: Start.AddSeconds(-1))]),
            At(2, [Actor(100, 0, center + Vector3.UnitZ * 3)]), At(3, [])]);
        controller.Seek(.5);
        Check(Shown("carnivale/eyes/cone/").Length == 2 && dangers() == 2 && Values("Gazes").Length == 1, "Ordinary cones.");
        foreach (var preview in Shown("carnivale/eyes/cone/"))
        {
            dynamic shape = Value(preview, "Geometry");
            Check((bool)shape.Contains(center + Vector3.UnitX * 5, 0f)
                && !(bool)shape.Contains(center + Vector3.UnitZ * 5, 0f), "Cold/Dread lost locked spell rotation.");
        }
        controller.Seek(1.5);
        Check(Shown("carnivale/eyes/cone/").Length == 0 && dangers() == 0 && Values("Gazes").Length == 0
            && Shown("carnivale/eyes/stack/").Length == 1 && Facing() == null
            && Hints().Contains("Kill mandragoras last incase you need to get blinded again."), "Blind expiry or scope.");
        controller.Seek(2.5);
        Check(Shown("carnivale/eyes/cone/").Length == 2 && Values("Gazes").Length == 1, "Blind actual Lose.");
        stop();
        Console.WriteLine("PASS native Carnivale Eyes separate Blind571 masks expiry ignored hide true gaze ordinary cones but retain stack restored by actual Lose no fabricated damage");

        load(guid, [Header(796), At(0, Initial(0x26FF, true), true, party: [new { Slot = 1, EntityId = 100ul }]),
            At(.05, [Actor(100, 0, center + Vector3.UnitZ * 3, stackedStatus: 571)],
                party: [new { Slot = 1, EntityId = 100ul }]),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14691, 5, center))],
                party: [new { Slot = 1, EntityId = 100ul }]),
            At(1, [Actor(100, 0, center + Vector3.UnitZ * 3, stackedStatus: 571)]),
            At(2, [Actor(100, 0, center + Vector3.UnitZ * 3)]), At(3, [])]);
        controller.Seek(.5);
        Check(Values("Gazes").Length == 0, "Source player PR slot1 was not Raid0.");
        controller.Seek(1.5);
        Check(Values("Gazes").Length == 0 && Hints().Contains("Kill mandragoras last incase you need to get blinded again."),
            "PR priority reorder changed source Blind bit.");
        controller.Seek(2.5);
        Check(Values("Gazes").Length == 1, "True Lose after PR reorder.");
        stop();
        Console.WriteLine("PASS native Carnivale Eyes source solo Raid0 PR priority reorder stable Blind actual Lose");

        var far = center + Vector3.UnitX * 600;
        load(guid, [Header(796), At(0, [Actor(1, 0x25CD, far), Actor(2, 0x233C, far),
                Actor(100, 0, center)], true),
            At(.1, [Actor(100, 0, center, stackedStatus: 571),
                Actor(2, 0x233C, far, cast: Cast(14691, 5, far))]),
            At(2, [Actor(100, 0, far, stackedStatus: 571)]), At(3, [])]);
        controller.Seek(.5);
        var pending = ((IEnumerable)Encounter().GetType().GetField("carnivalePending", flags)!.GetValue(Encounter())!).Cast<object>().Single();
        var pendingEyes = Value(pending, "Eyes");
        var blind = Value(pendingEyes, "DemonBlindness");
        Check(!(bool)Value(pendingEyes, "ConstructorNeedsBootstrap")
            && ((IEnumerable)Value(pendingEyes, "Demons")).Cast<object>().Count() == 1
            && (bool)blind.GetType().GetMethod("Blinded", flags)!.Invoke(blind, [0])! && previews().Length == 0,
            "Pending constructor did not retain input history or emitted output.");
        controller.Seek(2.5);
        Check(Values("Gazes").Length == 0 && Hints().Contains("Kill mandragoras last incase you need to get blinded again."),
            "Pending promotion reset Blind.");
        stop();
        Console.WriteLine("PASS native Carnivale Eyes Pending constructor raw gaze and Blind history before promotion no pending output");

        load(guid, [Header(796), At(0, Initial(0x25CD, true).Concat(new[] {
                Actor(3, 0x2700, center + Vector3.UnitX * 5), Actor(4, 0x2700, center + Vector3.UnitX * 10) }).ToArray(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14693, 5, center, target: 3))]),
            CastEvent(.11, 2, 14693, 5, center, target: 4),
            At(.2, [Actor(2, 0x233C, center, cast: Cast(14693, 5, center, target: 3))]),
            Cancel(.3, 2, 14693), At(1, [])]);
        controller.Seek(.25);
        var corrected = Values("CarnivaleEyeStacks").Single();
        Check((ulong)Value(Value(Value(corrected, "Target"), "Actor"), "Id") == 4ul
            && (ulong)Value(Value(corrected, "Cast"), "TargetId") == 4ul, "Packet target identity split or raw reverted correction.");
        controller.Seek(.5);
        Check(Values("CarnivaleEyeStacks").Length == 0 && Shown("carnivale/eyes/stack/").Length == 0, "Corrected Finish leaked stack.");
        controller.Seek(.25);
        stop();
        Console.WriteLine("PASS native Carnivale Eyes Teary same generation packet target correction raw confirmation true Finish reverse Seek");

        load(guid, [Header(796), At(0, [Actor(1, 0x26FF, center, combat: true),
                Actor(2, 0x233C, center, cast: Cast(14691, 5, center, startedAt: 0)),
                Actor(3, 0x233C, center, cast: Cast(14692, 5, center, startedAt: 0)),
                Actor(100, 0, center + Vector3.UnitZ * 3, stackedStatus: 571)], true,
                party: [new { Slot = 1, EntityId = 100ul }]),
            At(1, []), At(2, [])]);
        controller.Seek(.5);
        Check(Values("Gazes").Length == 1 && Shown("carnivale/eyes/cone/").Length == 0
            && Hints().Contains("Stack to get blinded!"), "Cold constructor consumed new roster before source commit.");
        controller.Seek(1.5);
        Check(Values("Gazes").Length == 1, "Unchanged cold-start Blind synthesized Gain.");
        stop();
        Console.WriteLine("PASS native Carnivale Eyes cold start571 constructor old roster combat new roster unchanged status preserves distinct masks");

        load(guid, [Header(796), At(0, [Actor(100, 0, center + Vector3.UnitZ * 3, stackedStatus: 571)], true),
            At(.1, [Actor(1, 0x26FF, center, combat: true),
                Actor(2, 0x233C, center, cast: Cast(14691, 5, center))]), At(1, [])]);
        controller.Seek(.5);
        Check(Values("Gazes").Length == 0 && Hints().Contains("Kill mandragoras last incase you need to get blinded again."),
            "Later constructor lost previous committed source roster.");
        stop();
        Console.WriteLine("PASS native Carnivale Eyes late module constructor replays existing571 with committed source roster");

        load(guid, [Header(796), At(0, Initial(0x25CD, true).Concat(new[] {
                Actor(3, 0x2700, center), Actor(4, 0x233C, center) }).ToArray(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14693, 5, center, target: 3)),
                Actor(4, 0x233C, center, cast: Cast(14693, 5, center, target: 3))]),
            Cancel(.2, 4, 14693), CastEvent(.21, 2, 14693, 5, center + Vector3.UnitX, target: 3),
            Cancel(.3, 2, 14693), At(1, [])]);
        controller.Seek(.25);
        Check(Values("CarnivaleEyeStacks").Length == 1
            && (ulong)Value(Value(Values("CarnivaleEyeStacks").Single(), "Cast"), "Source") == 4ul,
            "Packet replayed consumed target-FIFO Start.");
        controller.Seek(.5);
        Check(Values("CarnivaleEyeStacks").Length == 0 && Shown("carnivale/eyes/stack/").Length == 0,
            "Cross-source Finish left orphan.");
        stop();
        Console.WriteLine("PASS native Carnivale Eyes target FIFO already consumed source packet correction no resurrection real Finish");

        foreach (var origin in new Vector3[] { new(100, 0, 112), new(82, 0, 90), new(90, 0, 100),
            new(100, 0, 82), new(118, 0, 105), new(85, 0, 110), new(100, 0, 125), center })
        {
            load(guid, [Header(796), At(0, Initial(0x2706, true), true),
                At(.1, [Actor(2, 0x233C, origin, cast: Cast(14710, 12, center + Vector3.UnitX * 15))]),
                At(1, [Actor(2, 0x233C, center, cast: Cast(14710, 12, center + Vector3.UnitX * 15))]),
                EffectEvent(13, 2, 14710, 1), At(15, []), Cancel(16, 2, 14710), At(17, [])]);
            controller.Seek(.5);
            var voltage = Values("CarnivaleVoltages").Single();
            dynamic geometry = Value(voltage, "Visibility");
            Check((Vector3)Value(voltage, "Origin") == origin && dangers() == 1 && Shown("carnivale/voltage/").Length == 1,
                "Voltage must lock caster origin and immediately be risky.");
            var oracle = Oracle("Layout2Corners", origin);
            var contains = oracle.GetType().GetMethod("Contains")!;
            var fill = WorldFill((object)geometry);
            var compared = 0;
            for (var x = 74.137f; x < 126; x += .6f)
                for (var z = 74.173f; z < 126; z += .6f)
                {
                    var point = new Vector3(x, 0, z);
                    var expected = (bool)contains.Invoke(oracle, [Direction(point)])!;
                    Check((bool)geometry.Contains(point, 0f) == expected && Filled(fill, point) == expected,
                        $"Voltage source or real world fill differs at {origin} / {point}.");
                    ++compared;
                }
            controller.Seek(15.5);
            Check(Values("CarnivaleVoltages").Length == 1 && (Vector3)Value(Values("CarnivaleVoltages").Single(), "Origin") == origin
                && dangers() == 1, "Movement Effect timeout changed voltage lock.");
            controller.Seek(16.5);
            Check(Values("CarnivaleVoltages").Length == 0 && dangers() == 0, "Voltage cancel retained projection.");
            controller.Seek(.5);
            Isolated();
            stop();
            Console.WriteLine($"PASS native Carnivale Voltage origin{origin} source visibility and actual world fill {compared} points immediate risk actor origin separate spellLoc persistent effect deadline real cancel reverse Seek");
        }

        load(guid, [Header(796), At(0, Initial(0x2706), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14710, 12, center))]), Cancel(.2, 2, 14710),
            At(.3, [Actor(1, 0x2706, center, combat: true)]), At(1, [])]);
        controller.Seek(.5);
        Check(Values("CarnivaleVoltages").Length == 0 && dangers() == 0, "Canceled unclaimed voltage revived on pull.");
        stop();
        Console.WriteLine("PASS native Carnivale Voltage canceled before activation unchanged raw blocked no LastSong independent bool or special completion");

        load(guid, [Header(796), At(0, Initial(0x2706).Append(Actor(3, 0x2707, center)).ToArray(), true),
            At(.5, [], removed: [3]), At(1, [Actor(3, 0x2707, center + Vector3.UnitX * 10)]),
            At(2, [Actor(3, 0x2707, center + Vector3.UnitX * 10, dead: true)]),
            At(3, [Actor(3, 0x2707, center + Vector3.UnitX * 10)]), At(4, [])]);
        controller.Seek(.1);
        Check(Shown("carnivale/explosion/").Length == 1 && dangers() == 0 && Hints().Contains("In slime explosion radius!"),
            "Slime outline no navigation.");
        controller.Seek(1.5);
        Check(Shown("carnivale/explosion/").Length == 2 && Shown("carnivale/explosion/").All(a =>
            (float)Value(Value(a, "Geometry"), "Radius") == 7.6f), "Destroy froze old generation and new generation.");
        controller.Seek(3.5);
        Check(Shown("carnivale/explosion/").Length == 1 && dangers() == 0, "Slime death/ revive history.");
        stop();
        Console.WriteLine("PASS native Carnivale Voltage slime original reference radius7point6 outline text no navigation Destroy freeze same ID new generation Death removes revival not readds");
    }
}
