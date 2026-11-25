using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunKefka(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string id = "masterplayer.raid.kefka.normal";
        object Value(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, flags) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Script() => Value(manager.Running[id], "Script");
        object Encounter() => Value(Script(), "_encounter");
        object Model() => Value(Encounter(), "Kefka");
        object[] Mechanics() => ((IEnumerable)Value(Model(), "Mechanics")).Cast<object>().ToArray();
        object[] Group(string prefix) => previews().Where(p => ((string)Value(p, "Id")).Contains(prefix)).ToArray();
        object[] roster = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        object[] Initial(bool combat = true) => [Actor(1, 0x2153, default, combat: combat),
            Actor(2, 0x18D6, new(0, 0, 10)), Actor(3, 0x18D6, new(10, 0, 0)),
            .. Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, new(i * .1f, 0, 0)))];
        object Statuses(object actor, params object[] statuses)
        {
            var node = JsonSerializer.SerializeToNode(actor)!;
            node["Statuses"] = JsonSerializer.SerializeToNode(statuses);
            return node;
        }
        object[] Predictions() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        string Effect(double t, uint action) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 3ul, ActionId = action, ActionType = 1, GlobalSequence = 1u, Targets = Array.Empty<ulong>() },
        });
        {
            load(id, [Header(751), Frame(751, 0, Initial(false), full: true, party: roster),
                Frame(751, .1, [Actor(1, 0x2153, default, combat: true)], party: roster),
                Frame(751, .2, [Actor(2, 0x18D6, new(0, 0, 10), cast: Cast(10518, 3, new(0, 0, 10), startedAt: .2))], party: roster),
                CancelHoH(.3, 2, 10518),
                Frame(751, .4, [Actor(2, 0x18D6, new(0, 0, 10))], party: roster),
                Frame(751, .5, [Actor(2, 0x18D6, new(0, 0, 10), cast: Cast(10530, 3, new(0, 0, 10), startedAt: .5))], party: roster),
                Frame(751, .6, [Actor(2, 0x18D6, new(0, 0, 10))], party: roster),
                Frame(751, 15, [], party: roster), Effect(15.1, 10531)]);
            controller.Seek(.05);
            Check(Group("kefka/enemy").Length == 1 && dangers() == 0, "Kefka precombat marker/navigation gate.");
            Check(Value((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), "ArenaBoundary") == null
                && Group("kefka/arena/display").Length == 1, "Kefka precombat boundary navigation gate.");
            controller.Seek(.35);
            Check(Mechanics().Length == 1 && Group("kefka/aoe/").Length == 1 && dangers() > 0,
                "Kefka raw-only cancellation or shared previews.");
            controller.Seek(.45);
            Check(Mechanics().Length == 0 && dangers() == 0, "Kefka true Finish not consumed.");
            controller.Seek(15.05);
            Check(Value(Model(), "Ruin") != null && Group("kefka/ruin").Length == 1 && dangers() > 0,
                "Ruin cleared on Finish/time.");
            controller.Seek(15.2);
            Check(Value(Model(), "Ruin") == null && Group("kefka/ruin").Length == 0, "Ruin effect did not clear global single slot.");
            controller.Seek(.35);
            Check(Mechanics().Length == 1, "Kefka inverse Seek lost original group.");
            stop();
            Console.WriteLine("PASS native Kefka precombat marker real raw Finish packet Cancel isolation Ruin persistent F plus3.1 cross-source effect shared previews inverse Seek owner");
        }
        {
            load(id, [Header(751), Frame(751, 0, Initial(), full: true, party: roster),
                Frame(751, .1, [Actor(100, 0, new(10, 0, 0)),
                    Actor(2, 0x18D6, new(0, 0, 10), rotation: MathF.PI / 2,
                        cast: Cast(10535, 3, default, startedAt: .1, rotation: MathF.PI / 2))], party: roster),
                Frame(751, 4, [], party: roster)]);
            controller.Seek(.2);
            Check(Group("kefka/knockback/").Length >= 2 && dangers() == 1, "Kefka knockback model not actually adapted.");
            var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [new Vector3(10, 0, 0), 0f, 6f, false, false, 0f, true, (int)Value(snapshot, "Version")])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)Value((object)result, "IsLeastDangerFallback"), "Kefka real solver failed knockback safety.");
            var endpoint = (Vector3)Value((object)result, "TargetPosition");
            var landing = endpoint + new Vector3(15, 0, 0);
            Check(landing.X * landing.X + landing.Z * landing.Z <= 361.001f, "Kefka solver ignored fixed-direction landing.");
            stop();
            Console.WriteLine("PASS native Kefka production SafeZoneSolver fixed15 landing inside R19 no least-danger fallback real constraint preview owner cleanup");
        }
        {
            load(id, [Header(751), Frame(751, 0, Initial(), full: true, party: roster),
                Frame(751, .1, [Actor(100, 0, default, rotation: MathF.PI),
                    Actor(2, 0x18D6, new(0, 0, 10), cast: Cast(10539, 3, new(0, 0, 10), startedAt: .1)),
                    Actor(3, 0x18D6, new(10, 0, 0), cast: Cast(10540, 3, new(10, 0, 0), startedAt: .1))], party: roster),
                Frame(751, 3.5, [Actor(2, 0x18D6, new(0, 0, 10)), Actor(3, 0x18D6, new(10, 0, 0))], party: roster)]);
            controller.Seek(2.8);
            var encounter = Encounter();
            var heading = encounter.GetType().GetMethod("GazeFacing")!.Invoke(encounter, [Vector3.Zero, MathF.PI]);
            Check(heading is float value && MathF.Cos(value) >= .707107f && MathF.Sin(value) < .707107f,
                "Kefka reverse/positive gaze failed joint heading.");
            Check(Group("kefka/gaze/").Length == 6 && dangers() == 0, "Kefka eye/pupil/arcs or navigation scope.");
            var hints = Value(Script(), "Hints");
            var control = Value(hints, "_facing");
            Check(!(bool)Value(control, "_hasRequest"), "Replay submitted real PR heading.");
            controller.Seek(3.6);
            Check(Group("kefka/gaze/").Length == 0 && encounter.GetType().GetMethod("GazeFacing")!
                .Invoke(encounter, [Vector3.Zero, 0f]) == null, "Kefka real gaze Finish leaked.");
            stop();
            Console.WriteLine("PASS native Kefka positive reverse gaze source geometry eye pupil 90 and270 arcs joint PR facing planner deadline real Finish replay execution isolation");
        }
        {
            load(id, [Header(751), Frame(751, 0, Initial(), full: true, party: roster),
                Frame(751, .1, [Actor(2, 0x18D6, new(0, 0, 10),
                    cast: Cast(10528, 3, default, target: 100, startedAt: .1))], party: roster),
                Frame(751, .2, [Actor(107, 0, new(15, 0, 0))], party: roster),
                Frame(751, .3, [Actor(2, 0x18D6, new(0, 0, 10),
                    cast: Cast(10527, 3, default, target: 100, startedAt: .3))], party: roster),
                Frame(751, .8, [Actor(3, 0x18D6, new(10, 0, 0),
                    cast: Cast(10527, 3, default, target: 101, startedAt: .8))], party: roster),
                Frame(751, .9, [Actor(101, 0, new(.1f, 0, 0), dead: true)], party: roster),
                Frame(751, 1, [Actor(101, 0, new(.1f, 0, 0))], party: roster),
                Frame(751, 1.1, [Actor(3, 0x18D6, new(10, 0, 0))], party: roster)]);
            controller.Seek(.15);
            var hints = ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [null])!)
                .Cast<string>().ToArray();
            Check(hints.Contains("Stack (8/8)") && Group("kefka/stack/").Length > 0 && dangers() > 0,
                "Kefka exact8 actual stack consumer.");
            controller.Seek(.25);
            hints = ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [null])!).Cast<string>().ToArray();
            Check(hints.Contains("Stack! (7/8)"), "Kefka exact8 underfill not reflected.");
            controller.Seek(.85);
            var spread = Group("kefka/spread/");
            Check(spread.Length == 2 && spread.All(p => (bool)Value(p, "OutlineOnly"))
                && dangers() == 7, "Kefka R5 outlines or independent R6 constraints.");
            Check(Predictions().Length == 0, "Source RaidwideOnResolve=false spread generated production damage predictions.");
            var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            var dangerAreas = ((IEnumerable)Value(snapshot, "DangerAreas")).Cast<object>().ToArray();
            Check(dangerAreas.Length == 7, "Kefka spread target exclusion/clock gate.");
            controller.Seek(.95);
            Check(Mechanics().Length == 2 && Group("kefka/spread/").Length == 1, "Kefka dead spread did not hide without removal.");
            controller.Seek(1.05);
            Check(Group("kefka/spread/").Length == 2, "Kefka revived spread did not restore.");
            controller.Seek(1.15);
            Check(Mechanics().Length == 1 && Group("kefka/spread/").Length == 1, "Kefka Finish failed target consumer.");
            controller.Seek(.85);
            Check(Group("kefka/spread/").Length == 2, "Kefka party inverse Seek.");
            stop();
            Check(Group("kefka/").Length == 0 && dangers() == 0, "Kefka party owner cleanup.");
            Console.WriteLine("PASS native Kefka exact8 underfill spread R5 outlines R6 constraints independent clocks death revival real Finish inverse Seek owner");
        }
        {
            load(id, [Header(751), Frame(751, 0, Initial(), full: true, party: roster),
                Frame(751, .1, [Actor(2, 0x18D6, default, cast: Cast(10532, 3, default, startedAt: .1)),
                    Statuses(Actor(100, 0, new(10, 0, 0)),
                        new { Id = 160u, ExpiresAt = Start.AddSeconds(30), SlotIndex = 4 },
                        new { Id = 1209u, ExpiresAt = Start.AddSeconds(1), SlotIndex = 6 },
                        new { Id = 2663u, ExpiresAt = Start.AddSeconds(30), SlotIndex = 9 })], party: roster),
                Frame(751, .2, [Statuses(Actor(100, 0, new(10, 0, 0)),
                    new { Id = 160u, ExpiresAt = Start.AddSeconds(30), SlotIndex = 4 },
                    new { Id = 1209u, ExpiresAt = Start.AddSeconds(1), SlotIndex = 6 })], party: roster),
                Frame(751, .3, [Statuses(Actor(100, 0, new(10, 0, 0)),
                    new { Id = 160u, ExpiresAt = Start.AddSeconds(30), SlotIndex = 4 })], party: roster),
                Frame(751, .4, [Actor(2, 0x18D6, default,
                    cast: Cast(10542, 3, default, target: 101, startedAt: .4))], removed: [101], party: roster),
                Frame(751, 10, [], party: roster)]);
            controller.Seek(.15);
            Check(dangers() == 0 && Group("kefka/knockback/").Length == 0, "Kefka real job immunity slot absent.");
            controller.Seek(.25);
            Check(dangers() == 1 && Group("kefka/knockback/").Length >= 2, "Kefka real Slot9 Lose or Slot6 overwrite missing.");
            controller.Seek(.35);
            Check(dangers() == 1, "Kefka removing one same-category slot should clear category, not restore old Slot4.");
            controller.Seek(.45);
            Check(Predictions().Length == 1 && (ulong)Value(Predictions()[0], "TargetId") == 101,
                "Kefka missing Actor roster damage lost.");
            controller.Seek(10.05);
            Check(Math.Abs(((DateTime)Value(Predictions()[0], "Activation")
                - ((DateTime)runtime.CurrentTime).AddSeconds(.3)).TotalSeconds) < .00001,
                "Kefka production damage clock stopped following saturated CastInfo.");
            stop();
            Console.WriteLine("PASS native Kefka real60 status slots category overwrite independent job immunity Lose missing Actor roster damage dynamic raw clock replay isolation owner");
        }
        {
            load(id, [Header(751), Frame(751, 0, Initial(), full: true, party: roster),
                Frame(751, .1, [Actor(2, 0x18D6, default, cast: Cast(10532, 3, default, startedAt: .1)),
                    Statuses(Actor(100, 0, new(10, 0, 0)),
                        new { Id = 160u, ExpiresAt = Start.AddSeconds(1), SlotIndex = 4 })], party: roster),
                Frame(751, .2, [Statuses(Actor(100, 0, new(10, 0, 0)),
                    new { Id = 160u, ExpiresAt = DateTime.MaxValue, SlotIndex = 4, ZeroRemainingTime = true })], party: roster),
                Frame(751, .3, [Statuses(Actor(100, 0, new(10, 0, 0)),
                    new { Id = 160u, ExpiresAt = Start.AddSeconds(30), SlotIndex = 4 })], party: roster)]);
            controller.Seek(.15);
            Check(dangers() == 1, "Expired-before-hit immunity incorrectly removed knockback navigation.");
            controller.Seek(.25);
            Check(dangers() == 1, "Native zero remaining became a permanent immunity Gain.");
            controller.Seek(.35);
            Check(dangers() == 0, "A real native duration refresh did not update immunity.");
            controller.Seek(.25);
            Check(dangers() == 1, "Reverse Seek reused later duration refresh.");
            stop();
            Console.WriteLine("PASS native raw status adapter zero remaining is not permanent immunity genuine refresh and reverse Seek preserve real knockback navigation");
        }
        {
            var source = Assembly.Load("MasterPlayer.MechanicRuntime");
            var wpos = source.GetType("BossMod.WPos")!;
            var wdir = source.GetType("BossMod.WDir")!;
            object Pos(float x, float z) => Activator.CreateInstance(wpos, [x, z])!;
            var displacement = assembly.GetType("MasterPlayer.Mechanics.Dungeons.KefkaDisplacement")!;
            var geometry = assembly.GetType("MasterPlayer.Mechanics.Dungeons.KefkaKnockbackGeometry")!;
            var sourceChecks = 0;
            foreach (var forward in new[] { false, true })
                foreach (var rotation in new[] { 0f, .71f, MathF.PI / 2 })
                    foreach (var origin in new[] { Vector3.Zero, new Vector3(3, 0, -2) })
                    {
                        var movement = Activator.CreateInstance(displacement, flags, null,
                            [origin, forward ? 15f : 10f, rotation, Start, forward, false, false], null)!;
                        var native = Activator.CreateInstance(geometry, flags, null, [movement], null)!;
                        var (sin, cos) = Math.SinCos(rotation);
                        var oracle = forward ? Activator.CreateInstance(source.GetType("BossMod.SDKnockbackInCircleFixedDirection")!,
                            [Pos(0, 0), Activator.CreateInstance(wdir, [15 * (float)sin, 15 * (float)cos])!, 19f])!
                            : Activator.CreateInstance(source.GetType("BossMod.SDKnockbackInCircleAwayFromOrigin")!,
                                [Pos(0, 0), Pos(origin.X, origin.Z), 10f, 19f])!;
                        for (var x = -20f; x <= 20; x += .5f)
                            for (var z = -20f; z <= 20; z += .5f)
                            {
                                var expected = (float)oracle.GetType().GetMethod("Distance")!.Invoke(oracle, [Pos(x, z)])! <= 0;
                                var actual = (bool)geometry.GetMethod("Contains")!.Invoke(native, [new Vector3(x, 0, z), 0f])!;
                                Check(expected == actual, $"Kefka actual source 0/1 landing predicate forward={forward} rotation={rotation} point={x},{z}.");
                                ++sourceChecks;
                            }
                    }
            Console.WriteLine($"PASS native Kefka actual source radial10 fixed15 R19 0/1 Distance oracle {sourceChecks} points including zero and boundaries");
        }
        {
            var source = Assembly.Load("MasterPlayer.MechanicRuntime");
            var wpos = source.GetType("BossMod.WPos")!;
            var angle = source.GetType("BossMod.Angle")!;
            object Pos(float x, float z) => Activator.CreateInstance(wpos, [x, z])!;
            object Angle(float value) => Activator.CreateInstance(angle, [value])!;
            object Shape(string name, params object[] args) => Activator.CreateInstance(source.GetType("BossMod." + name)!, args)!;
            var zero = Angle(0);
            var cases = new (uint Action, object Shape)[]
            {
                (10518, Shape("AOEShapeCircle", 10f, false)),
                (10516, Shape("AOEShapeDonut", 10f, 40f, false)),
                (10524, Shape("AOEShapeRect", 40f, 5f, 0f, zero, false)),
                (10536, Shape("AOEShapeRect", 70f, 3f, 0f, zero, false)),
                (10537, Shape("AOEShapeCone", 100f, Angle(MathF.PI / 2), zero, false)),
                (10530, Shape("AOEShapeCone", 102.7f, Angle(MathF.PI / 3), zero, false)),
            };
            var nativeCheck = assembly.GetType("MasterPlayer.Mechanics.Dungeons.DungeonSourceGeometry")!
                .GetMethod("Contains", BindingFlags.Static | BindingFlags.NonPublic)!;
            var count = 0;
            foreach (var item in cases)
                foreach (var rotation in new[] { 0f, .71f, MathF.PI / 2 })
                {
                    var origin = new Vector3(0, 0, 10);
                    load(id, [Header(751), Frame(751, 0, Initial(), full: true, party: roster),
                        Frame(751, .1, [Actor(2, 0x18D6, origin,
                            cast: Cast(item.Action, 3, origin, rotation: rotation, startedAt: .1))], party: roster)]);
                    controller.Seek(.1);
                    var mechanic = item.Action == 10530 ? Value(Model(), "Ruin") : Mechanics().Single();
                    var nativeShape = Value(mechanic, "Shape");
                    var check = item.Shape.GetType().GetMethod("Check", [wpos, wpos, angle])!;
                    for (var x = -24f; x <= 24; x += .5f)
                        for (var z = -24f; z <= 24; z += .5f)
                        {
                            var point = origin + new Vector3(x, 0, z);
                            var expected = (bool)check.Invoke(item.Shape, [Pos(point.X, point.Z), Pos(origin.X, origin.Z), Angle(rotation)])!;
                            var actual = (bool)nativeCheck.Invoke(null, [nativeShape, origin, rotation, point])!;
                            Check(actual == expected,
                                $"Kefka actual source text Check action={item.Action} rotation={rotation} offset={x},{z} expected={expected}.");
                            ++count;
                        }
                    stop();
                }
            Console.WriteLine($"PASS native Kefka actual source ordinary text Check donut inner edge cones apex raw rect circles {count} points three rotations");
        }
    }
}
