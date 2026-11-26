using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Mechanics.Fates;
using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;

internal static class CrescentFateRegression
{
    private static readonly DateTime START = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
    private static Lumina.GameData? oracleData;
    private sealed class Fixture(CrescentFateDefinition definition)
    {
        internal CrescentFateEncounter Model = new(definition);
        internal List<ReplayActorState> Actors =
        [
            new() { Id = 1, Oid = definition.PrimaryOid, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                Position = new(100, 0, 100), Targetable = true, InCombat = !definition.ProtectedPot },
            new() { Id = 100, Kind = 1, Hp = 100, Position = new(100, 0, 100) },
            new() { Id = 20, Oid = 0x233C, Kind = 2, SubKind = 5, Hp = 100, Position = new(100, 0, 100) },
            new() { Id = 22, Oid = 0x233C, Kind = 2, SubKind = 5, Hp = 100, Position = new(100, 0, 100) },
        ];
        internal ReplayActiveFate? Fate = new(definition.FateId, new(100, 0, 100), 40);
        internal DateTime Now = START;
        internal void Set(ulong id, Func<ReplayActorState, ReplayActorState> change)
        { var i = Actors.FindIndex(a => a.Id == id); Actors[i] = change(Actors[i]); }
        internal void Sync() => Model.Synchronize(Actors, Fate, 100, Now);
        internal void Cast(uint action, ulong source = 20)
        {
            Set(source, a => a with { Cast = new(action, 100, a.Position, .4f, Now, Now.AddSeconds(3.3), false, false)
                { TotalTime = 3, IsSpell = true } });
            Sync();
        }
    }

    internal static void Run(Action<string, Action> run)
    {
        run("Crescent FATE five reviewed manifests thirteen actual component source differential", Differential);
        run("Crescent FATE ordinary pull raw identity cached circle reset rebuild and cold helper", Lifecycle);
        run("Crescent FATE protected pot exact live pull BigFan existence and radius overrides", Pot);
        run("Crescent FATE gaze finite ranges locks joint facing and raw dynamic damage clock", GazeClock);
        run("Crescent FATE active input recording delta apply old unknown and explicit none", FrameInput);
        run("Crescent FATE default priorities preserve passive outside fate height and pending dead", Targets);
        run("Crescent FATE groups 98 99 100 source unstable ties independent starts and Finish source differential", GroupThreshold);
        run("Crescent FATE raw lifecycle total location rotation generation pet and nonspell boundaries", RawBoundaries);
        run("Crescent FATE existing same OID pending reverse promotion death and 500 distance", PendingPrimaries);
        run("Crescent FATE actual source manager initial and recreated Party0 loading differential", ManagerLoading);
    }

    private static void Differential()
    {
        if (!MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment.IsInitialized)
        {
            var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
            Check(Directory.Exists(sqpack), "Source component oracle needs FFXIV_SQPACK.");
            oracleData = new(sqpack!, new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
            MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment.Initialize(oracleData,
                ScriptServiceProxy.Create<Dalamud.Plugin.Services.IPluginLog>((_, _) => null));
        }
        var components = 0;
        var boundaries = 0;
        var points = 0;
        foreach (var definition in CrescentFateCatalog.Definitions.Take(5))
        {
            var ns = "BossMod.Dawntrail.Foray.FATE." + (definition.Name == "DemiMedusa" ? "ScaleModel" : definition.Name);
            var moduleType = typeof(BossMod.BossModule).Assembly.GetType(ns + "." + definition.Name)!;
            var world = new BossMod.WorldState(1, "crescent-standard-oracle");
            var primary = new BossMod.Actor(1, definition.PrimaryOid, 0, 0, "", 0, BossMod.ActorType.Enemy,
                default, 100, new Vector4(100, 0, 100, 0));
            var module = (BossMod.BossModule)RuntimeHelpers.GetUninitializedObject(moduleType);
            typeof(BossMod.BossModule).GetField("WorldState")!.SetValue(module, world);
            module.PrimaryActor = primary;
            foreach (var rule in definition.Rules)
            {
                ++components;
                foreach (var action in rule.Actions)
                {
                    var fixture = new Fixture(definition);
                    fixture.Sync();
                    var source = (BossMod.BossComponent)Activator.CreateInstance(moduleType.Assembly.GetType(ns + "." + rule.Component)!,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [module], null)!;
                    var casters = new ulong[] { 20, 22 }.Select(id => new BossMod.Actor(id, 0x233C, (ushort)id, 0, "", 0,
                        BossMod.ActorType.Helper, default, 100, new Vector4(100, 0, 100, 0))).ToArray();
                    BossMod.ActorCastInfo Spell(uint id) => new()
                    {
                        Action = new(BossMod.ActionType.Spell, id), TotalTime = 3, Location = new(100, 0, 100), Rotation = new(.4f),
                    };
                    void Compare(string at)
                    {
                        var native = fixture.Model.Casts.Where(c => ReferenceEquals(c.Rule, rule)).ToArray();
                        if (source is BossMod.Components.GenericAOEs aoes)
                        {
                            var expected = aoes.ActiveAOEs(0, primary).ToArray();
                            Check(expected.Length == native.Length, $"{definition.Name}/{rule.Component}/{at} list count.");
                            for (var i = 0; i < expected.Length; ++i)
                            {
                                var a = native[i];
                                var e = expected[i];
                                Check(a.Activation == e.Activation && a.Origin.X == e.Origin.X && a.Origin.Z == e.Origin.Z
                                    && a.Rotation == e.Rotation.Rad && e.Risky && e.Color == 0,
                                    $"{definition.Name}/{rule.Component}/{at}[{i}] fields/clock/order.");
                                var parameters = (e.Shape, a.Rule.Shape) switch
                                {
                                    (BossMod.AOEShapeCircle x, CircleTemplate y) => x.Radius == y.Radius,
                                    (BossMod.AOEShapeCone x, FanTemplate y) => x.Radius == y.Radius && x.HalfAngle.Rad * 2 == y.AngleRadians,
                                    (BossMod.AOEShapeRect x, RayRectangleTemplate y) => x.LengthFront == y.Length && x.LengthBack == 0
                                        && x.HalfWidth == y.MaxLocalX && -x.HalfWidth == y.MinLocalX,
                                    _ => false,
                                };
                                Check(parameters, $"{definition.Name}/{rule.Component}/{at} shape parameters.");
                                for (var x = -65; x <= 65; x += 5)
                                    for (var z = -65; z <= 65; z += 5)
                                    {
                                        var point = a.Origin + new Vector3(x + .13f, 0, z + .21f);
                                        Check(e.Check(new(point.X, point.Z)) == DungeonSourceGeometry.Contains(a.Rule.Shape!,
                                            a.Origin, a.Rotation, point), $"{rule.Component}/{at} shape point {point}.");
                                        ++points;
                                    }
                            }
                        }
                        else if (source is BossMod.Components.CastGaze gaze)
                        {
                            var eyes = gaze.Eyes;
                            Check(eyes.Count == native.Length, $"{rule.Component}/{at} gaze list.");
                            for (var i = 0; i < eyes.Count; ++i)
                                Check(eyes[i].Activation == native[i].Activation && eyes[i].Range == rule.GazeRange
                                    && eyes[i].Position.X == native[i].Origin.X && eyes[i].Position.Z == native[i].Origin.Z,
                                    $"{rule.Component}/{at} finite gaze/lock/clock.");
                        }
                        else if (source is BossMod.Components.CastHint hint)
                            Check(hint.Casters.Select(a => a.InstanceID).SequenceEqual(native.Select(a => a.Source.Actor.Id)),
                                $"{rule.Component}/{at} original caster references.");
                        else throw new InvalidOperationException("Unsupported source component " + rule.Component);
                        ++boundaries;
                    }
                    foreach (var caster in casters)
                    {
                        world.Frame.Timestamp = fixture.Now;
                        fixture.Cast(action, caster.InstanceID);
                        source.OnCastStarted(caster, Spell(action));
                        Compare("start/" + caster.InstanceID);
                        fixture.Now = fixture.Now.AddSeconds(.1);
                    }
                    fixture.Model.Finish(fixture.Model.Actors[20], fixture.Model.Actors[20].Cast! with { ActionId = 999 });
                    source.OnCastFinished(casters[0], Spell(999));
                    Compare("unrelated-Finish");
                    foreach (var caster in casters)
                    {
                        fixture.Set(caster.InstanceID, a => a with { Cast = null });
                        fixture.Sync();
                        source.OnCastFinished(caster, Spell(action));
                        Compare("watched-Finish/" + caster.InstanceID);
                    }
                }
            }
        }
        Check(components == 13, "Actual component count changed.");
        foreach (var action in CrescentFateCatalog.Definitions.SelectMany(d => d.Rules).SelectMany(r => r.Actions).Distinct())
            Check(CrescentFateCatalog.ExtraCastTime(action)
                == new BossMod.ActionID(BossMod.ActionType.Spell, action).CastTimeExtra(), $"Client extra cast mismatch {action}.");
        Console.WriteLine("INFO Crescent client ExtraCastTime100ms: " + string.Join(", ",
            CrescentFateCatalog.Definitions.SelectMany(d => d.Rules).SelectMany(r => r.Actions).Distinct()
                .Select(a => $"{a}={new BossMod.ActionID(BossMod.ActionType.Spell, a).CastTimeExtra()}")));
        Console.WriteLine($"INFO Crescent source differential: {components} components, {boundaries} event boundaries, {points} independent shape points.");
    }

    private static void Lifecycle()
    {
        var definition = CrescentFateCatalog.Definitions[0];
        var f = new Fixture(definition) { Fate = new(0, default, 0) };
        f.Set(100, a => a with { Position = new(131, 0, 100) });
        f.Cast(41273);
        Check(!f.Model.Active && f.Model.Radius == 30, "Default R30 pull boundary.");
        f.Set(100, a => a with { Position = new(130, 0, 100) }); f.Sync();
        Check(f.Model.Active && f.Model.Casts.Count == 1, "Cold helper cast at R30 boundary.");
        f.Fate = new(1965, new(120, 0, 100), 50); f.Sync();
        Check(f.Model.Center.X == 120 && f.Model.Radius == 50, "Dynamic real fate circle.");
        f.Fate = f.Fate with { Center = new(120.009f, 0, 100.009f), Radius = 50.009f }; f.Sync();
        Check(f.Model.Center == new Vector3(120, 0, 100) && f.Model.Radius == 50,
            "Source coordinate-wise .01 tolerance was replaced by Euclidean distance.");
        f.Set(1, a => a with { FateId = 777 }); f.Fate = new(777, new(125, 0, 100), 60); f.Sync();
        Check(f.Model.Active && f.Model.Center.X == 125, "Primary FateId precedes metadata NameID.");
        f.Fate = new(1965, new(0, 0, 0), 2); f.Sync();
        Check(!f.Model.Active && f.Model.Casts.Count == 0, "Changed current fate did not reset cached module.");
        f.Set(1, a => a with { FateId = 0 }); f.Fate = new(1965, new(100, 0, 100), 40); f.Sync();
        Check(f.Model.Active, "Reset permanently suppressed the same primary.");
        f.Set(100, a => a with { Position = new(150, 0, 100) }); f.Sync();
        Check(f.Model.Active, "R+10 exact boundary reset.");
        f.Set(100, a => a with { Position = new(150.01f, 0, 100) }); f.Sync();
        Check(!f.Model.Active, "R+10 outside retained module.");
        f.Fate = null; f.Sync();
        Check(!f.Model.Active && f.Model.Diagnostic != null, "Unknown old input invented a circle.");
    }

    private static void Pot()
    {
        var f = new Fixture(CrescentFateCatalog.Definitions.Single(d => d.ProtectedPot));
        f.Sync();
        Check(f.Model.Active, "Protected pot must activate outside combat.");
        f.Cast(50221); f.Set(20, a => a with { Cast = null }); f.Sync();
        Check(f.Model.Active, "Small fan wave ended the protected pot module.");
        f.Actors.Add(new() { Id = 30, Oid = 0x4D8E, Hp = 0, Dead = true }); f.Sync();
        Check(f.Model.Active, "Dead but present BigFan is not disappearance.");
        f.Actors.RemoveAll(a => a.Id == 30); f.Sync();
        Check(!f.Model.Active, "Seen BigFan disappearance failed reset.");
        f.Sync(); Check(f.Model.Active, "Reset must allow a fresh module candidate.");
        f.Set(100, a => a with { Position = new(151, 0, 100) }); f.Sync();
        Check(!f.Model.Active, "Protected pot live R+10 override.");
        f.Set(100, a => a with { Position = new(100, 0, 100) });
        f.Fate = new(0, default, 0); f.Sync(); Check(!f.Model.Active, "Pot ignored exact current Fate gate.");
        f.Set(1, a => a with { Oid = 0x47CB }); f.Fate = new(2073, new(100, 0, 100), 40); f.Sync();
        Check(!f.Model.Active, "Comment-only shared pot acquired source behavior.");
    }

    private static void GazeClock()
    {
        var f = new Fixture(CrescentFateCatalog.Definitions.Single(d => d.Name == "EyeToEye"));
        f.Sync(); f.Cast(47148); f.Cast(47152, 22);
        var player = f.Actors.Single(a => a.Id == 100) with { Position = new(140, 0, 100) };
        Check(f.Model.Gazes(player).Length == 1, "R30/R50 gaze boundaries were merged.");
        f.Set(22, a => a with { Position = new(500, 0, 500) }); f.Sync();
        Check(f.Model.Gazes(player).Single().Origin == new Vector3(100, 0, 100), "Gaze moved with actor.");
        f.Now = f.Now.AddSeconds(3); f.Sync();
        Check(f.Model.Facing(player with { Rotation = -MathF.PI / 2 }) is { } facing
            && MathF.Abs(MathF.Atan2(MathF.Sin(facing + MathF.PI / 2), MathF.Cos(facing + MathF.PI / 2))) > MathF.PI / 4,
            "Finite gaze failed joint facing.");
        f.Cast(47147);
        f.Now = f.Now.AddSeconds(20); f.Sync();
        var source = new BossMod.ActorCastInfo { Action = new(BossMod.ActionType.Spell, 47147), TotalTime = 3 };
        var sourceActor = new BossMod.Actor(20, 0x233C, 20, 0, "", 0, BossMod.ActorType.Helper,
            default, 100, default) { CastInfo = source };
        var sourceActors = new BossMod.ActorState();
        sourceActors.Actors.Add(20, sourceActor);
        sourceActors.Tick(new(f.Now, 0, 0, 20f, 20f, 1));
        Check(source.ElapsedTime == 3.5f, "Source client extra-time saturation branch was not exercised.");
        Check(f.Model.Activation(f.Model.Actors[20]) == f.Now.AddSeconds(source.NPCRemainingTime),
            "Raidwide saturated raw NPC clock differs.");
        f.Set(20, a => a with { Cast = null }); f.Sync();
        Check(f.Model.Casts.All(c => c.Rule.Kind != CrescentFateRuleKind.Raidwide), "Raidwide real Finish.");
    }

    private static void FrameInput()
    {
        var first = new ReplayWorldFrame { Full = true, Territory = 1346, ActiveFate = new(2073, new(3, 4, 5), 45), AllowedFateId = 0 };
        var current = first with { Full = false, ActiveFate = new(2073, new(6, 7, 8), 55) };
        var delta = ReplayWorldFrame.Delta(first, current);
        var options = new JsonSerializerOptions { IncludeFields = true };
        var restored = JsonSerializer.Deserialize<ReplayWorldFrame>(JsonSerializer.Serialize(delta, options), options)!;
        var world = new ReplayWorldState();
        world.Apply(first); world.Apply(restored);
        Check(world.ActiveFate == current.ActiveFate && world.AllowedFateId == 0, "Raw identity confused with attack qualification.");
        world.Apply(first with { ActiveFate = null }); Check(world.ActiveFate == null, "Old missing input retained previous circle.");
        world.Apply(first with { ActiveFate = new(0, default, 0) }); Check(world.ActiveFate is { Id: 0 }, "Known none confused with unknown.");
    }

    private static void Targets()
    {
        foreach (var territory in new uint[] { 1252, 1346 })
        {
            var a = new ReplayActorState { Id = 20, Kind = 2, SubKind = 5, IsEnemy = true, ObjectIndex = 20,
                Targetable = true, Hp = 100, MaxHp = 100 };
            ulong Select(params ReplayActorState[] actors) => QuestTargetPriority.Select(territory, actors, 0, default,
                new HashSet<ulong> { 100 }, 2073);
            Check(Select(a) == 0 && Select(a with { InCombat = true, TargetId = 100 }) == 20,
                "Default passive/combat priority changed.");
            Check(Select(a with { FateId = 999, AggroPlayer = true }) == 0
                && Select(a with { Position = new(0, 12.1f, 0), AggroPlayer = true }) == 0,
                "Wrong fate/height ignored.");
            Check(Select(a with { FateId = 2073, AggroPlayer = true }) == 20
                && Select(a with { Hp = 1, AggroPlayer = true }) == 0, "Fate/PendingDead changed.");
        }
    }

    private static void GroupThreshold()
    {
        var definition = CrescentFateCatalog.Definitions.Single(d => d.Name == "DemiMedusa");
        var rule = definition.Rules[0];
        var f = new Fixture(definition);
        f.Sync();
        var world = new BossMod.WorldState(1, "crescent-group-threshold");
        var module = (BossMod.Dawntrail.Foray.FATE.ScaleModel.DemiMedusa)RuntimeHelpers.GetUninitializedObject(
            typeof(BossMod.Dawntrail.Foray.FATE.ScaleModel.DemiMedusa));
        typeof(BossMod.BossModule).GetField("WorldState")!.SetValue(module, world);
        var sourceType = module.GetType().Assembly.GetType("BossMod.Dawntrail.Foray.FATE.ScaleModel.CursedSight")!;
        var source = (BossMod.Components.SimpleAOEGroups)Activator.CreateInstance(sourceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [module], null)!;
        void Compare()
        {
            var actual = f.Model.Casts.Where(c => ReferenceEquals(c.Rule, rule)).ToArray();
            Check(source.Casters.Select(c => c.ActorID).SequenceEqual(actual.Select(c => c.Source.Actor.Id))
                && source.Casters.Select(c => c.Activation).SequenceEqual(actual.Select(c => c.Activation)),
                $"Group source order/clock differs at {actual.Length} casters.");
        }
        for (var i = 0; i < 100; ++i)
        {
            var id = (ulong)(1000 + i);
            f.Actors.Add(new() { Id = id, Oid = 0x4DD8, Kind = 2, SubKind = 5, Hp = 100,
                Position = new(100, 0, 100) });
            var total = i < 98 ? 1 + i % 3 : 2;
            f.Set(id, a => a with { Cast = new(rule.Actions[i % 2], 100, a.Position, 0, f.Now, f.Now.AddSeconds(total + .3), false, false)
                { TotalTime = total, IsSpell = true } });
            f.Sync();
            world.Frame.Timestamp = f.Now;
            var caster = new BossMod.Actor(id, 0x4DD8, (ushort)id, 0, "", 0, BossMod.ActorType.Helper, default, 100, default);
            source.OnCastStarted(caster, new() { Action = new(BossMod.ActionType.Spell, rule.Actions[i % 2]), TotalTime = total });
            Compare();
        }
        f.Cast(48256); // A different component cannot re-sort equal-clock entries.
        Compare();
        f.Model.Finish(f.Model.Actors[1000], new(999, 0, default, 0, f.Now, f.Now, false, false));
        source.OnCastFinished(new(1000, 0x4DD8, 0, 0, "", 0, BossMod.ActorType.Helper, default, 100, default),
            new() { Action = new(BossMod.ActionType.Spell, 999) });
        Compare();
    }

    private static void RawBoundaries()
    {
        var f = new Fixture(CrescentFateCatalog.Definitions[0]);
        f.Sync();
        foreach (var kind in new byte[] { 2, 3, 9 })
        {
            f.Set(20, a => a with { SubKind = kind });
            f.Cast(41273);
            Check(f.Model.Casts.Count == 0, "Pet/chocobo/buddy cold or hot Spell entered source component.");
        }
        f.Set(20, a => a with { SubKind = 5, Cast = null }); f.Sync();
        f.Cast(41273);
        var old = f.Model.Casts.Single();
        f.Set(20, a => a with { Position = new(120, 0, 120), Rotation = 1,
            Cast = a.Cast! with { StartedAt = f.Now.AddSeconds(-1) } }); f.Sync();
        Check(ReferenceEquals(old, f.Model.Casts.Single()), "Elapsed-only/raw actor motion restarted a locked cast.");
        foreach (var modify in new Func<ReplayCast, ReplayCast>[] {
            c => c with { TotalTime = 4 }, c => c with { Location = new(110, 0, 100) }, c => c with { Rotation = 1 } })
        {
            var previous = f.Model.Casts.Single();
            f.Set(20, a => a with { Cast = modify(a.Cast!) }); f.Sync();
            Check(f.Model.Casts.Count == 1 && !ReferenceEquals(previous, f.Model.Casts.Single()),
                "True raw cast field change failed Finish/Start.");
        }
        f.Set(20, a => a with { Cast = a.Cast! with { IsSpell = false } }); f.Sync();
        Check(f.Model.Casts.Count == 0, "Ability transition failed watched Spell Finish.");
        f.Cast(41273);
        var previousSource = f.Model.Casts.Single().Source;
        f.Set(20, a => a with { Oid = 0x4822 }); f.Sync();
        Check(previousSource.Destroyed && f.Model.Casts.Count == 1
            && !ReferenceEquals(previousSource, f.Model.Casts.Single().Source), "Same ID replacement retained the old actor.");
        f.Set(1, a => a with { Hp = 0 }); f.Sync();
        f.Set(1, a => a with { Hp = 100 }); f.Sync();
        Check(!f.Model.Active && f.Model.Completed, "DeathPhase completed actor restarted without recreation.");
        f.Actors.RemoveAll(a => a.Id == 1); f.Sync();
        f.Actors.Insert(0, new() { Id = 1, Oid = f.Model.Definition.PrimaryOid, Hp = 100,
            Position = new(100, 0, 100), InCombat = true, Targetable = true }); f.Sync();
        Check(f.Model.Active && !f.Model.Completed, "New primary generation did not rebuild source phase.");
    }

    private static void PendingPrimaries()
    {
        var initial = new Fixture(CrescentFateCatalog.Definitions[0]);
        var template = initial.Actors[0];
        initial.Fate = new(0, default, 0);
        initial.Actors[0] = template with { Position = new(601, 0, 100) };
        initial.Actors.Add(template with { Id = 2 });
        initial.Sync();
        Check(initial.Model.Active && initial.Model.Primary!.Actor.Id == 2,
            "First distant candidate blocked initial pending promotion.");
        var reverse = new Fixture(CrescentFateCatalog.Definitions[0]);
        reverse.Actors.Add(reverse.Actors[0] with { Id = 3 });
        reverse.Sync();
        Check(reverse.Model.Primary!.Actor.Id == 3, "First world frame did not promote pending in reverse order.");
        var noPlayer = new Fixture(CrescentFateCatalog.Definitions[0]);
        noPlayer.Actors.RemoveAll(a => a.Id == 100);
        noPlayer.Sync();
        Check(noPlayer.Model.Primary == null && !noPlayer.Model.Active,
            "Candidate loaded without source Party[0].");
        noPlayer.Actors.Add(new() { Id = 100, Kind = 1, Hp = 100, Position = template.Position });
        noPlayer.Sync();
        Check(noPlayer.Model.Primary!.Actor.Id == 1, "Pending lost before Party[0] submission.");
        var removedPlayer = new Fixture(CrescentFateCatalog.Definitions[0]);
        removedPlayer.Actors.RemoveAll(a => a.Oid == template.Oid);
        removedPlayer.Sync();
        removedPlayer.Actors.RemoveAll(a => a.Id == 100);
        removedPlayer.Actors.Add(template);
        removedPlayer.Actors.Add(template with { Id = 2 });
        removedPlayer.Sync();
        Check(removedPlayer.Model.Primary == null, "Destroyed previous Party[0] immediately loaded candidate.");
        removedPlayer.Actors.Add(new() { Id = 100, Kind = 1, Hp = 100, Position = template.Position });
        removedPlayer.Sync();
        Check(removedPlayer.Model.Primary!.Actor.Id == 2, "Restored Party[0] did not reverse-promote pending.");
        var generation = new Fixture(CrescentFateCatalog.Definitions[0]);
        generation.Set(1, a => a with { GameObjectId = 10 });
        generation.Cast(41273);
        var oldPrimary = generation.Model.Primary!;
        var existingCast = generation.Model.Casts.Single();
        generation.Set(1, a => a with { GameObjectId = 11 });
        generation.Sync();
        Check(oldPrimary.Destroyed && !ReferenceEquals(oldPrimary, generation.Model.Primary)
            && generation.Model.Active && !generation.Model.Completed
            && ReferenceEquals(existingCast, generation.Model.Casts.Single()),
            "Same-ID primary generation created a retire gap or rebuilt unrelated component references.");
        var observerGeneration = new Fixture(CrescentFateCatalog.Definitions[0]);
        observerGeneration.Actors.RemoveAll(a => a.Oid == template.Oid);
        observerGeneration.Set(100, a => a with { GameObjectId = 1 });
        observerGeneration.Sync();
        observerGeneration.Set(100, a => a with { GameObjectId = 2 });
        observerGeneration.Actors.Add(template);
        observerGeneration.Actors.Add(template with { Id = 2 });
        observerGeneration.Sync();
        Check(observerGeneration.Model.Primary!.Actor.Id == 1,
            "Recreated source Party[0] did not restore immediate creation-order loading.");
        var distant = new Fixture(CrescentFateCatalog.Definitions[0]);
        distant.Fate = new(0, default, 0);
        distant.Sync();
        distant.Actors.Add(template with { Id = 2, Position = new(900, 0, 100) });
        distant.Set(100, a => a with { Position = new(900, 0, 100) }); distant.Sync();
        Check(distant.Model.Primary == null && !distant.Model.Active, "Reset distant loaded module was not returned to pending.");
        distant.Sync();
        Check(distant.Model.Active && distant.Model.Primary!.Actor.Id == 2, "Near pending remained blocked by distant old module.");
        var f = new Fixture(CrescentFateCatalog.Definitions[0]);
        f.Sync();
        var primary = f.Actors[0];
        f.Actors.Add(primary with { Id = 2 });
        f.Actors.Add(primary with { Id = 3, Position = new(601, 0, 100) });
        f.Actors.Add(primary with { Id = 4 });
        f.Actors.Add(primary with { Id = 5, Dead = true });
        f.Sync();
        Check(f.Model.Primary!.Actor.Id == 1, "Existing same OID stole loaded primary.");
        f.Actors.RemoveAll(a => a.Id == 1); f.Sync();
        Check(!f.Model.Active, "Pending promoted before old loaded module retired.");
        f.Sync();
        Check(f.Model.Active && f.Model.Primary!.Actor.Id == 4,
            "Existing pending did not promote in reverse insertion order, excluding dead and beyond500.");
        f.Set(4, a => a with { Dead = true }); f.Sync();
        Check(!f.Model.Active && f.Model.Completed, "Death did not retire active loaded primary.");
        f.Sync();
        Check(f.Model.Active && f.Model.Primary!.Actor.Id == 2, "Living earlier pending was lost after completed primary.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void ManagerLoading()
    {
        // Initialize the actual source registry and client data; no duplicated
        // pending-selection algorithm is used as an oracle.
        if (!MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment.IsInitialized) Differential();
        foreach (var recreateObserver in new[] { false, true })
        {
            var world = new BossMod.WorldState(1, "crescent-manager-oracle");
            world.Frame.Timestamp = START;
            using var manager = new BossMod.BossModuleManager(world);
            var model = new CrescentFateEncounter(CrescentFateCatalog.Definitions[0]);
            var values = new List<ReplayActorState>();
            void Create(ulong id, uint oid, Vector3 position, uint generation = 0)
            {
                values.Add(new() { Id = id, Oid = oid, Kind = id == 100 ? (byte)1 : (byte)2,
                    Hp = 100, MaxHp = 100, GameObjectId = generation, Position = position });
                world.Execute(new BossMod.ActorState.OpCreate(id, oid, (int)id, 0, "", 0,
                    id == 100 ? BossMod.ActorType.Player : BossMod.ActorType.Enemy, default, 100,
                    new(position, 0), 1, new(100, 100, 0, 100, 100), false, false, 0, 0, 0));
            }
            if (recreateObserver)
            {
                Create(100, 0, new(100, 0, 100), 1);
                world.Execute(new BossMod.PartyState.OpModify(0, new(100, 100, false, "source")));
                model.Synchronize(values, new(0, default, 0), 100, START);
                world.Execute(new BossMod.ActorState.OpDestroy(100));
                values.Clear();
                Create(100, 0, new(100, 0, 100), 2);
            }
            Create(1, 0x46C1, new(recreateObserver ? 100 : 601, 0, 100));
            Create(2, 0x46C1, new(100, 0, 100));
            if (!recreateObserver) Create(100, 0, new(100, 0, 100));
            world.Execute(new BossMod.PartyState.OpModify(0, new(100, 100, false, "source")));
            manager.Update();
            model.Synchronize(values, new(0, default, 0), 100, START);
            var expected = manager.LoadedModules.Single(m => m.PrimaryActor.OID == 0x46C1).PrimaryActor.InstanceID;
            Check(expected == model.Primary?.Actor.Id && expected == (recreateObserver ? 1u : 2u),
                $"Actual Manager initial/recreated Party[0] loading differs ({recreateObserver}): {expected}/{model.Primary?.Actor.Id}");
        }
    }
}
