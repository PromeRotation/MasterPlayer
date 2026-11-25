using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunThordan(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string id = "masterplayer.trial.thordan.extreme";
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
        object Model() => Value(Encounter(), "Thordan");
        object[] Items(object obj, string name) => ((IEnumerable)Value(obj, name)).Cast<object>().ToArray();
        object[] Group(string prefix) => previews().Where(p => ((string)Value(p, "Id")).Contains(prefix)).ToArray();
        object[] Lines(string prefix)
        {
            var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
            var registry = Value(plugin, "AoeVisibility");
            return ((IEnumerable)registry.GetType().GetMethod("GetLines")!.Invoke(registry, null)!)
                .Cast<object>().Where(p => ((string)Value(p, "Id")).Contains(prefix)).ToArray();
        }
        void Activate(string component)
        {
            var type = assembly.GetType("MasterPlayer.Mechanics.Dungeons.ThordanComponent")!;
            Model().GetType().GetMethod("Activate", flags)!.Invoke(Model(), [Enum.Parse(type, component)]);
        }
        void Advance(double time)
        {
            controller.Play();
            controller.Update((float)(time - (double)controller.Clock));
            controller.Pause();
        }
        uint State() => (uint)Value(Value(Model(), "Timeline"), "StateId");
        object Spell(uint action, double at, Vector3 position, float rotation = 0, ulong target = 0, double duration = 3)
            => new { ActionId = action, TargetId = target, Location = Pose(position), Rotation = rotation,
                StartedAt = Start.AddSeconds(at), FinishAt = Start.AddSeconds(at + duration + .3),
                TotalTime = (float)duration, IsSpell = true };
        string Effect(double t, uint action, ulong source = 3, int actionType = 1)
            => JsonSerializer.Serialize(new { t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = action, ActionType = actionType, GlobalSequence = 1u, Targets = Array.Empty<ulong>() } });
        string Icon(double t, ulong actor, uint marker)
            => JsonSerializer.Serialize(new { t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = actor, TargetId = 3ul, MarkerId = marker } });
        string View(string frame, ulong player)
        {
            var node = JsonNode.Parse(frame)!;
            node["data"]!["LocalPlayerId"] = player;
            return node.ToJsonString();
        }
        object[] roster = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        void LoadComponentFixture(IEnumerable<string> lines)
        {
            load(id, lines.Select(line =>
            {
                var node = JsonNode.Parse(line)!;
                if (node["kind"]?.GetValue<string>() == "WorldFrame")
                    node["data"]!["Party"] = JsonSerializer.SerializeToNode(roster);
                return node.ToJsonString();
            }));
        }
        var bossPosition = new Vector3(0, 0, 10);
        object Boss(bool combat = true, bool targetable = true, object? cast = null)
            => Actor(1, 0x1453, bossPosition, combat: combat, targetable: targetable, targetId: 100, cast: cast, subKind: 5);
        object[] Initial(bool combat = true) => [Boss(combat), Actor(2, 0x1463, new(0, 0, 10), subKind: 11),
            Actor(3, 0x1463, new(10, 0, 0), subKind: 11),
            .. Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, new(i * .1f, 0, 0), classJob: i < 2 ? 19u : 25u))];
        {
            load(id, [Header(448), Frame(448, 0, Initial(false), full: true, party: roster),
                Frame(448, .05, [Boss()], party: roster), Effect(.1, 5246),
                Frame(448, .2, [Boss(cast: Spell(5251, .2, bossPosition))], party: roster),
                Frame(448, .3, [Boss()], party: roster), Effect(.4, 5252),
                Frame(448, .5, [Boss(cast: Spell(5247, .5, bossPosition))], party: roster),
                Frame(448, .6, [Boss()], party: roster), Effect(.7, 5246),
                Frame(448, .8, [Boss(cast: Spell(5256, .8, bossPosition))], party: roster),
                Frame(448, .9, [Boss()], party: roster),
                Frame(448, 1, [Boss(cast: Spell(5257, 1, bossPosition))], party: roster),
                Frame(448, 1.1, [Actor(3, 0x1463, new(10, 0, 0),
                    cast: Spell(5258, 1.1, new(10, 0, 0)), subKind: 11)], party: roster),
                CancelHoH(1.2, 1, 5257),
                Frame(448, 4.4, [Boss(), Actor(3, 0x1463, new(10, 0, 0), subKind: 11)], party: roster)]);
            controller.Seek(.02);
            Check(Group("thordan/marker/primary").Length == 1 && dangers() == 0
                && Value((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), "ArenaBoundary") == null,
                "Thordan precombat display became navigation.");
            controller.Seek(.35);
            Check(State() == 0x10002, "Thordan actual timeline did not wait for meteor Effect.");
            var timeline = (object)manager.CurrentTimeline;
            var timelineView = assembly.GetType("MasterPlayer.MechanicHost.MechanicTimelineView")!;
            var rows = ((IEnumerable)timelineView.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == "BuildRows" && m.GetParameters()[0].ParameterType.Name == "ScriptTimeline")
                .Invoke(null, [timeline, 3])!).Cast<object>().ToArray();
            Check((string)Value(rows[0], "Name") == "Puddles resolve" && (bool)Value(rows[0], "IsCurrent"),
                "Actual script timeline is absent from the production UI row builder.");
            controller.Seek(3.6);
            Check(State() == 0x40011 && Group("thordan/gaze/").Length == 6, "Timeline did not actually activate both gaze sources.");
            var heading = Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [Vector3.Zero, 0f]);
            Check(heading is float angle && MathF.Cos(angle) < .707107f && MathF.Sin(angle) < .707107f,
                "Thordan joint PR facing planner failed.");
            Check(!(bool)Value(Value(Value(Script(), "Hints"), "_facing"), "_hasRequest"),
                "Thordan replay sent a real PR facing request.");
            controller.Seek(4.5);
            Check(Group("thordan/gaze/").Length == 0, "Real Finish did not release gaze.");
            controller.Seek(3.6);
            Check(Group("thordan/gaze/").Length == 6, "Reverse Seek reused later gaze cleanup.");
            stop();
            Check(manager.CurrentTimeline == null, "Native timeline survived owner stop.");
            Console.WriteLine("PASS native Thordan real phase0 timeline meteor Effect Mercy Might KnightsEye gaze raw cancellation joint facing shared previews reverse Seek owner replay isolation");
        }
        {
            var grinnaux = Actor(20, 0x145A, new(4, 0, 0), subKind: 5);
            load(id, [Header(448), Frame(448, 0, [.. Initial(), grinnaux], full: true, party: roster),
                Frame(448, .1, [Boss(targetable: false)], party: roster),
                Frame(448, .2, [Actor(10, 0x1455, default, dead: true, subKind: 5),
                    Actor(11, 0x1456, default, dead: true, subKind: 5)], party: roster),
                Frame(448, .25, [], party: roster),
                Frame(448, .3, [Actor(2, 0x1463, new(0, 0, 10), subKind: 11,
                    tethers: [new { Id = 5, TargetId = 100ul, SlotIndex = 0 }])], party: roster),
                Frame(448, .4, [Actor(3, 0x1463, new(10, 0, 0), subKind: 11,
                    cast: Spell(5274, .4, new(10, 0, 0)))], party: roster),
                Icon(.5, 100, 29), Effect(.6, 5287), Effect(.7, 5274),
                Frame(448, .8, [Actor(20, 0x145A, new(20, 0, 0), subKind: 5)], party: roster),
                Effect(.9, 5275, actionType: 3), Effect(1, 5275)]);
            controller.Seek(.27);
            Check((int)Value(Value(Model(), "Timeline"), "Phase") == 2 && State() == 0x02000000,
                "Cached dead knight phase transition failed.");
            controller.Seek(.85);
            Check(State() == 0x02000050 && Value(Model(), "Faith") != null
                && (Vector3)Value(Value(Model(), "Faith"), "Origin") == new Vector3(4, 0, 0)
                && Group("thordan/faith/").Length >= 2, "Faith cold source snapshot or actual activation missing.");
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [new Vector3(-10, 0, 0), 0f, 6f, false, false, 0f, true, (int)Value(snapshot, "Version")])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)Value((object)result, "IsLeastDangerFallback"),
                "Faith production solver failed.");
            var destination = (Vector3)Value((object)result, "TargetPosition");
            var movement = Value(Model(), "Faith");
            var landing = (Vector3)movement.GetType().GetMethod("Landing", flags)!.Invoke(movement, [destination])!;
            Check(landing.X * landing.X + landing.Z * landing.Z <= 324.001f, "Faith solver ignored R18 landing constraint.");
            controller.Seek(.95);
            Check(State() == 0x02000050, "Ability effect incorrectly consumed a Spell-only component.");
            controller.Seek(1.05);
            Check(Value(Model(), "Faith") == null, "True Spell Effect failed Faith deactivation.");
            controller.Seek(.85);
            Check(Value(Model(), "Faith") != null, "Faith reverse Seek failed.");
            stop();
            Console.WriteLine("PASS native Thordan actual three-phase gates cached knights real tether Icon Effect Faith source lock radial16 R18 production solver Ability exclusion reverse Seek owner");
        }
        {
            LoadComponentFixture([Header(448), Frame(448, 0, Initial(), full: true, party: roster),
                Frame(448, .2, [Actor(2, 0x1463, default, cast: Spell(5280, .2, default, .4f), subKind: 11)], party: roster),
                Effect(.25, 5280),
                Frame(448, .3, [Actor(2, 0x1463, default, subKind: 11)], party: roster),
                Frame(448, .4, [Actor(3, 0x1463, new(1, 0, -1), cast: Spell(5281, .4, new(1, 0, -1), .4f), subKind: 11)]),
                Frame(448, .5, [Actor(3, 0x1463, new(1, 0, -1), subKind: 11)]),
                Frame(448, .6, [Actor(3, 0x1463, default, cast: Spell(5282, .6, default, .4f), subKind: 11)]),
                Frame(448, .7, [Actor(3, 0x1463, default, subKind: 11)]),
                Frame(448, .8, [Actor(3, 0x1463, default, cast: Spell(5283, .8, default, .4f), subKind: 11)]),
                Frame(448, .9, [Actor(3, 0x1463, default, subKind: 11)])]);
            controller.Seek(.1); Activate("HeavyImpact");
            Advance(.26);
            Check(Items(Model(), "Heavy").Length == 1 && (int)Value(Items(Model(), "Heavy")[0], "Order") == 0
                && Group("thordan/heavy/").Length == 1 && dangers() > 0, "Heavy Effect consumed a raw Finish sequence.");
            foreach (var (at, order) in new[] { (.35, 1), (.55, 2), (.75, 3) })
            {
                Advance(at);
                var heavy = Items(Model(), "Heavy");
                Check(heavy.Length == 1 && (int)Value(heavy[0], "Order") == order
                    && Group("thordan/heavy/").Length == 1 && dangers() > 0,
                    $"Heavy production stage {order} missing: clock={controller.Clock}, orders={string.Join(',', heavy.Select(h => Value(h, "Order")))}, previews={Group("thordan/heavy/").Length}, dangers={dangers()}.");
            }
            Advance(.95);
            Check(Items(Model(), "Heavy").Length == 0 && Group("thordan/heavy/").Length == 0, "Heavy fourth Finish retained old shape.");
            stop();
            Console.WriteLine("PASS native Thordan component fixture production Heavy four Finish stages cross-source axis match Effect non-consumption shared previews navigation cleanup");
        }
        {
            LoadComponentFixture([Header(448), Frame(448, 0, Initial(), full: true, party: roster),
                Frame(448, .2, [Actor(2, 0x1457, new(30, 0, 0), subKind: 5)]),
                Effect(.3, 4120, 2),
                Frame(448, .4, [Actor(2, 0x1457, new(30, 0, 0), cast: Spell(5286, .4, new(30, 0, 0), .7f), subKind: 5)]),
                Frame(448, .5, [Actor(2, 0x1457, new(30, 0, 0), subKind: 5)]),
                Frame(448, .6, [Actor(2, 0x1457, new(25, 0, 0), subKind: 5,
                    tethers: [new { Id = 5, TargetId = 100ul, SlotIndex = 0 }])]),
                Frame(448, .7, [Actor(2, 0x1457, new(24, 0, 1), subKind: 5,
                    tethers: [new { Id = 5, TargetId = 100ul, SlotIndex = 0 }])])]);
            controller.Seek(.1); Activate("SpiralThrust2"); Activate("SpiralPierce");
            Advance(.35);
            var preview = Group("thordan/aoe/").Single();
            Check(Items(Model(), "Aoes").Any(a => !(bool)Value(a, "Risky"))
                && (bool)Value(preview, "OutlineOnly") == false, "Thrust future prediction was not displayed.");
            Advance(.55);
            Check(Items(Model(), "Aoes").Count(a => (uint)Value(a, "Action") == 5286) == 1
                && Items(Model(), "Aoes").All(a => (bool)Value(a, "Risky")) && dangers() > 0,
                "First true thrust failed to replace predictions or Finish deleted persistent line.");
            Advance(.75);
            Check(Lines("thordan/pierce/line/").Length == 1 && Group("thordan/bait/").Length == 1,
                "Pierce dynamic source line missing from shared preview output.");
            Check((Vector3)Value(Lines("thordan/pierce/line/")[0], "Start") == new Vector3(24, 0, 1),
                "Pierce line failed to follow the original source.");
            stop();
            Check(Lines("thordan/pierce/line/").Length == 0, "Pierce owner retained line.");
            Console.WriteLine("PASS native Thordan component fixture production future Thrust real persistent line dynamic Pierce source tether line owner cleanup");
        }
        {
            LoadComponentFixture([Header(448), Frame(448, 0, Initial(), full: true, party: roster),
                Frame(448, .2, [Actor(10, 0x1455, new(-5, 0, 0), stackedStatus: 944, targetId: 101, subKind: 5),
                    Actor(11, 0x1456, new(5, 0, 0), stackedStatus: 945, targetId: 100, subKind: 5)]),
                Frame(448, .3, [Actor(2, 0x1463, new(0, 0, -10), cast: Spell(5271, .3, new(0, 0, -10), target: 102), subKind: 11)]),
                Frame(448, .4, [Actor(3, 0x1463, new(0, 0, -10), cast: Spell(5266, .4, new(0, 0, -10)), subKind: 11)]),
                Frame(448, .45, [Actor(3, 0x1463, new(0, 0, -10), subKind: 11)]),
                Frame(448, .5, [Actor(4, 0x1463, new(10, 0, 0), cast: Spell(5277, .5, new(10, 0, 0)), subKind: 11),
                    Actor(5, 0x1463, new(-10, 0, 0), cast: Spell(5277, .5, new(-10, 0, 0)), subKind: 11),
                    Actor(103, 0, new(10, 0, 0), classJob: 25)]),
                Icon(.55, 104, 29),
                View(Frame(448, .6, []), 101),
                Frame(448, .7, [Actor(10, 0x1455, new(-5, 0, 0), dead: true, stackedStatus: 944, subKind: 5)], removed: [11]),
                Effect(.8, 5295), Effect(.9, 5271), Effect(1, 5266)]);
            controller.Seek(.1);
            Activate("SwordShieldOfTheHeavens"); Activate("HolyShieldBash"); Activate("Conviction"); Activate("HiemalStormSpread");
            Advance(.25);
            Check(Group("thordan/marker/sword/").Length == 2
                && Items(Encounter(), "ForbiddenTargets").Length == 0, "Shield generated extra forbidden targets or missing source markers.");
            Advance(.48);
            Check(Value(Model(), "WildChargeSource") != null && Group("thordan/wild/").Length == 1,
                "Holy charge raw Finish removed source/shape.");
            Advance(.58);
            Check(Group("thordan/tower/").Length == 2
                && ((string?)Value(Script(), "PredictedDamageText"))?.Contains("Raidwide") == true,
                "Tower or Hiemal production damage diagnostic missing.");
            Advance(.65);
            Check((ulong)Value(Model(), "PlayerId") == 101 && Group("thordan/wild/").Length == 1,
                "Current-player change lost global Holy sequence.");
            var ambient = ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
            Check(ambient.Any(a => ((string)Value(a, "Id")).StartsWith("thordan/wild/join/") && (bool)Value(a, "Safe")),
                "Share role did not publish the existing priority-one target consumer.");
            Advance(.75);
            Check(Group("thordan/marker/sword/").Length == 0, "Dead/destroyed sword references leaked source markers.");
            Advance(1.05);
            Check(Value(Model(), "WildChargeSource") == null && !Items(Model(), "Baits").Any(b => Value(b, "Component").ToString() == "HiemalStormSpread"),
                "Real effects failed Holy/Hiemal independent cleanup.");
            stop();
            Check(previews().Length == 0 && dangers() == 0, "Thordan component owner cleanup leaked output.");
            Console.WriteLine("PASS native Thordan component fixture production Sword Shield markers no widened restrictions Holy Avoid and Share targets tower Hiemal diagnostic view switch true Effect owner");
        }
        {
            var source = Assembly.Load("MasterPlayer.MechanicRuntime");
            var environment = source.GetType("MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment")!;
            if (!(bool)environment.GetProperty("IsInitialized")!.GetValue(null)!)
            {
                var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
                Check(Directory.Exists(sqpack), "Thordan source oracle requires FFXIV_SQPACK.");
                floorGameData = new(sqpack!, new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
                environment.GetMethod("Initialize")!.Invoke(null, [floorGameData, ECommons.DalamudServices.Svc.Log]);
            }
            var ws = Activator.CreateInstance(source.GetType("BossMod.WorldState")!, [1ul, "native-thordan-oracle"])!;
            var actorType = source.GetType("BossMod.Actor")!;
            var constructor = actorType.GetConstructors().Single();
            var args = constructor.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue
                : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : "").ToArray();
            args[0] = 1ul; args[1] = 0x1453u;
            var primary = constructor.Invoke(args);
            var module = Activator.CreateInstance(source.GetType("BossMod.Heavensward.Extreme.Ex3Thordan.Ex3Thordan")!, [ws, primary])!;
            try
            {
                var builder = Activator.CreateInstance(source.GetType("BossMod.Heavensward.Extreme.Ex3Thordan.Ex3ThordanStates")!,
                    flags, null, [module], null)!;
                var machine = builder.GetType().GetMethod("Build")!.Invoke(builder, null)!;
                var sourcePhases = Items(machine, "Phases");
                load(id, [Header(448), Frame(448, 0, Initial(), full: true, party: roster)]);
                controller.Seek(.01);
                var model = Model();
                var nativeTimeline = Value(model, "Timeline");
                var enumType = assembly.GetType("MasterPlayer.Mechanics.Dungeons.ThordanComponent")!;
                var count = 0;
                var metadata = new List<object>();
                for (var phase = 0; phase < 3; ++phase)
                {
                    var nativeSteps = ((IEnumerable)nativeTimeline.GetType().GetMethod("Steps", flags)!.Invoke(nativeTimeline, [phase])!).Cast<object>().ToArray();
                    model.GetType().GetMethod("ClearComponents", flags)!.Invoke(model, null);
                    ((IList)Value(module, "Components")).Clear();
                    var state = Value(sourcePhases[phase], "InitialState");
                    for (var i = 0; i < nativeSteps.Length; ++i)
                    {
                        var step = nativeSteps[i];
                        metadata.Add(new { Phase = phase, PhaseName = (string)Value(sourcePhases[phase], "Name"),
                            Id = (uint)Value(state, "ID"), Name = (string)Value(state, "Name"),
                            Comment = (string)Value(state, "Comment"), Hints = Convert.ToInt32(Value(state, "EndHint")) });
                        Check((uint)Value(state, "ID") == (uint)Value(step, "Id")
                            && (float)Value(state, "Duration") == (float)Value(step, "Expected"),
                            $"Thordan source graph state/duration mismatch phase={phase} index={i}.");
                        Check((string)Value(step, "Name") == (string)Value(state, "Name")
                            && (string)Value(step, "Comment") == (string)Value(state, "Comment")
                            && Convert.ToInt32(Value(step, "Hints")) == Convert.ToInt32(Value(state, "EndHint")),
                            $"Thordan source timeline presentation mismatch state={Value(state, "ID"):X}.");
                        var comment = (string)Value(state, "Comment");
                        var expectedAction = comment.StartsWith("Cast start: ") ? uint.Parse(comment["Cast start: ".Length..]) : 0;
                        Check((uint)Value(step, "ExpectedAction") == expectedAction,
                            $"Thordan expected primary cast action missing state={Value(state, "ID"):X}.");
                        foreach (var boundary in new[] { "Enter", "Exit" })
                        {
                            (Value(state, boundary) as Action)?.Invoke();
                            foreach (var change in Items(step, boundary))
                            {
                                var activate = (bool)Value(change, "Item2");
                                var component = Enum.Parse(enumType, Value(change, "Item1").ToString()!);
                                model.GetType().GetMethod(activate ? "Activate" : "Deactivate", flags)!.Invoke(model, [component]);
                            }
                            var expected = Items(module, "Components").Select(c => c.GetType().Name).ToArray();
                            var actual = Items(model, "ActiveComponents").Select(c => c.ToString()!).ToArray();
                            Check(expected.SequenceEqual(actual),
                                $"Thordan source component activation order phase={phase} state={Value(step, "Id"):X} {boundary}: {string.Join(',', expected)}/{string.Join(',', actual)}.");
                        }
                        ++count;
                        var next = Value(state, "NextStates") as Array;
                        if (i + 1 == nativeSteps.Length) Check(next == null || next.Length == 0, "Source has an omitted terminal successor.");
                        else
                        {
                            Check(next is { Length: 1 }, "Source graph is not the migrated linear path.");
                            state = next!.GetValue(0)!;
                        }
                    }
                }
                stop();
                if (Environment.GetEnvironmentVariable("THORDAN_METADATA_EXPORT") is { Length: > 0 } export)
                    File.WriteAllText(export, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"PASS native Thordan complete source state graph {count} states three phases five trios 100 enrage exact durations names comments end hints expected casts ordered activation deactivation callbacks");
            }
            finally { ((IDisposable)module).Dispose(); }
        }
        {
            var source = Assembly.Load("MasterPlayer.MechanicRuntime");
            var wpos = source.GetType("BossMod.WPos")!;
            var wdir = source.GetType("BossMod.WDir")!;
            object Pos(float x, float z) => Activator.CreateInstance(wpos, [x, z])!;
            var movementType = assembly.GetType("MasterPlayer.Mechanics.Dungeons.ThordanDisplacement")!;
            var geometryType = assembly.GetType("MasterPlayer.Mechanics.Dungeons.ThordanFaithGeometry")!;
            var checkedPoints = 0;
            foreach (var origin in new[] { Vector3.Zero, new Vector3(4, 0, 0), new Vector3(-3.7f, 0, 4.2f) })
            {
                var movement = Activator.CreateInstance(movementType, flags, null, [origin, 16f, default(DateTime)], null)!;
                var geometry = Activator.CreateInstance(geometryType, flags, null, [movement], null)!;
                var oracle = Activator.CreateInstance(source.GetType("BossMod.SDKnockbackInCircleAwayFromOrigin")!,
                    [Pos(0, 0), Pos(origin.X, origin.Z), 16f, 18f])!;
                for (var x = -24f; x <= 24; x += .5f)
                    for (var z = -24f; z <= 24; z += .5f)
                    {
                        var expected = (float)oracle.GetType().GetMethod("Distance")!.Invoke(oracle, [Pos(x, z)])! <= 0;
                        var actual = (bool)geometryType.GetMethod("Contains")!.Invoke(geometry, [new Vector3(x, 0, z), 0f])!;
                        Check(expected == actual, $"Thordan source Faith Distance at {origin}/{x},{z} expected={expected}.");
                        ++checkedPoints;
                    }
            }
            Console.WriteLine($"PASS native Thordan actual source radial16 R18 Distance oracle {checkedPoints} points including zero and edge");
            var sourceType = source.GetType("BossMod.Heavensward.Extreme.Ex3Thordan.Ex3Thordan")!;
            var nativeCatalog = assembly.GetType("MasterPlayer.Mechanics.Dungeons.ThordanCatalog")!;
            foreach (var (sourceName, nativeName) in new[] { ("startingArena", "STARTING_ARENA"), ("DefaultBounds", "REDUCED_ARENA") })
            {
                var bounds = sourceType.GetField(sourceName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
                var center = Value(bounds, "Center");
                var cx = (float)Value(center, "X");
                var cz = (float)Value(center, "Z");
                dynamic native = nativeCatalog.GetField(nativeName, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var count = 0;
                for (var x = -25.1f; x <= 25; x += .31f)
                    for (var z = -25.1f; z <= 25; z += .31f)
                    {
                        var expected = (bool)bounds.GetType().GetMethod("Contains", flags)!.Invoke(bounds,
                            [Activator.CreateInstance(wdir, [x - cx, z - cz])!])!;
                        Check((bool)native.Contains(new Vector3(x, 0, z), 0f) == expected,
                            $"Thordan source {sourceName} arena at {x},{z} expected={expected}.");
                        ++count;
                    }
                Console.WriteLine($"PASS native Thordan source {sourceName} {count} point arena oracle");
            }
        }
    }
}
