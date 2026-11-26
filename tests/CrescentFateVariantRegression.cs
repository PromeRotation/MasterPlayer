using System.Numerics;
using System.Reflection;
using BossMod;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Mechanics.Fates;
using MasterPlayer.Mechanics.Quests;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;

internal static class CrescentFateVariantRegression
{
    private static readonly DateTime START = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
    private static Lumina.GameData? data;
    private static int boundaries;
    private static int points;

    private sealed class Fixture : IDisposable
    {
        internal readonly CrescentFateEncounter Model;
        internal readonly BossMod.WorldState World;
        internal readonly BossMod.BossModule Module;
        internal readonly List<ReplayActorState> Actors = [];
        internal DateTime Now = START;
        internal readonly ReplayActiveFate Fate;

        internal Fixture(string name, bool activate = true)
        {
            Initialize();
            var definition = CrescentFateCatalog.Definitions.Single(d => d.Name == name);
            Model = new(definition);
            World = new(1, "crescent-variants-source");
            World.Frame.Timestamp = Now;
            Fate = new(definition.FateId, new(100, 0, 100), 40);
            Add(new() { Id = 1, Oid = definition.PrimaryOid, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                Targetable = true, Position = new(100, 0, 100) });
            Add(new() { Id = 100, Kind = 1, Hp = 100, MaxHp = 100, Position = new(100, 0, 100) });
            Add(new() { Id = 2, Oid = 0x233C, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                Position = new(101.123f, 0, 98.456f) });
            Add(new() { Id = 3, Oid = 0x233C, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                Position = new(105.25f, 0, 95.125f) });
            World.Execute(new BossMod.PartyState.OpModify(0, new(100, 100, false, "source")));
            var ns = name switch
            {
                "MadMudarch" => "SwornToSoil",
                "GaleForceEncounter" => "NH104Stormcaller",
                "RelicIcewolf" => "NH110RelicIcewolf",
                _ => name,
            };
            var type = typeof(BossMod.BossModule).Assembly.GetType($"BossMod.Dawntrail.Foray.FATE.{ns}.{name}")!;
            Module = (BossMod.BossModule)Activator.CreateInstance(type, World, Actor(1))!;
            Sync();
            if (activate) Activate();
        }

        internal BossMod.Actor Actor(ulong id) => World.Actors.Find(id)!;
        internal ReplayActorState Value(ulong id) => Actors.Single(a => a.Id == id);
        internal void Set(ulong id, Func<ReplayActorState, ReplayActorState> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Add(ReplayActorState actor)
        {
            Actors.Add(actor);
            var type = actor.Kind == 1 ? BossMod.ActorType.Player
                : (BossMod.ActorType)((actor.Kind << 8) | actor.SubKind);
            World.Execute(new BossMod.ActorState.OpCreate(actor.Id, actor.Oid, (int)actor.Id, 0, "", 0,
                type, default, 100, new(actor.Position, actor.Rotation), 1,
                new(actor.Hp, actor.MaxHp, 0, 100, 100), actor.Targetable, false, 0, 0, 0));
        }
        internal void Activate()
        {
            foreach (var rule in Model.Definition.Rules)
            {
                var type = Module.GetType().Assembly.GetType(Module.GetType().Namespace + "." + rule.Component)!;
                typeof(BossMod.BossModule).GetMethod("ActivateComponent")!.MakeGenericMethod(type).Invoke(Module, null);
            }
            Set(1, a => a with { InCombat = true });
            Sync();
            Check(Module.Components.Count == Model.Definition.Rules.Length, "Actual activation list differs.");
        }
        internal void Sync()
        {
            Model.Synchronize(Actors, Fate, 100, Now);
            foreach (var component in Module.Components) component.Update();
        }
        internal void Advance(double seconds, bool update = true)
        {
            Now = Now.AddSeconds(seconds);
            World.Actors.Tick(new(Now, 0, 0, (float)seconds, (float)seconds, 1));
            World.Frame.Timestamp = Now;
            Model.Advance(Now);
            if (update) Update();
        }
        internal void Update()
        {
            Model.Update();
            foreach (var component in Module.Components) component.Update();
        }
        internal void Cast(uint action, ulong source = 2, float total = 3, Vector3? location = null, bool happened = false)
        {
            var loc = location ?? new Vector3(103.125f, 0, 97.25f);
            World.Execute(new BossMod.ActorState.OpCastInfo(source, new()
            {
                Action = new(BossMod.ActionType.Spell, action), TargetID = 100,
                Location = loc, Rotation = new(.4f), TotalTime = total, EventHappened = happened,
            }));
            Set(source, a => a with { Cast = new(action, 100, loc, .4f, Now, Now.AddSeconds(total + .3d), false, happened)
                { TotalTime = total, IsSpell = true } });
            Sync();
        }
        internal void End(ulong source = 2)
        {
            World.Execute(new BossMod.ActorState.OpCastInfo(source, null));
            Set(source, a => a with { Cast = null });
            Sync();
        }
        internal void Effect(uint action, ulong source = 3, uint sequence = 0)
        {
            World.Execute(new BossMod.ActorState.OpCastEvent(source,
                new(new(BossMod.ActionType.Spell, action), 100, 0, 0, default, sequence, 0, default)));
            Model.Effect(source, action, sequence);
        }
        internal void FinishAs(uint action, ulong source, Vector3 location)
        {
            var cast = new BossMod.ActorCastInfo { Action = new(BossMod.ActionType.Spell, action), Location = location };
            foreach (var component in Module.Components) component.OnCastFinished(Actor(source), cast);
            Model.Finish(Model.Actors[source], new(action, 0, location, 0, Now, Now, false, false));
        }
        internal CrescentFateCast[] Native(string component)
            => Model.VisibleAoes().Where(c => c.Rule.Component == component).ToArray();
        internal void Compare(string label)
        {
            foreach (var component in Module.Components)
            {
                var name = component.GetType().Name;
                if (component is BossMod.Components.GenericAOEs aoes)
                {
                    var expected = aoes.ActiveAOEs(0, Actor(100)).ToArray();
                    var actual = Native(name);
                    Check(expected.Length == actual.Length,
                        $"{Model.Definition.Name}/{name}/{label}: count {expected.Length}/{actual.Length}");
                    for (var i = 0; i < expected.Length; ++i)
                    {
                        var e = expected[i];
                        var a = actual[i];
                        var color = a.Danger ? BossMod.Colors.Danger : a.AoeColor ? BossMod.Colors.AOE : 0;
                        Check(e.Activation == a.Activation && e.Origin.X == a.Origin.X && e.Origin.Z == a.Origin.Z
                            && e.Rotation.Rad == a.Rotation && e.Risky == a.Risky && e.Color == color
                            && (e.ActorID == 0 || e.ActorID == a.Source.Actor.Id),
                            $"{Model.Definition.Name}/{name}/{label}[{i}]: clock/origin/rotation/risk/color/identity");
                        Shape(e.Shape, a.Shape!, name + "/" + label);
                        for (var x = -61; x <= 61; x += 7)
                            for (var z = -61; z <= 61; z += 7)
                            {
                                var p = a.Origin + new Vector3(x + .137f, 0, z + .251f);
                                Check(e.Check(new(p.X, p.Z)) == DungeonSourceGeometry.Contains(a.Shape!, a.Origin, a.Rotation, p),
                                    $"{name}/{label}: shape point {p}");
                                ++points;
                            }
                    }
                }
                else if (component is BossMod.Components.CastGaze gaze)
                {
                    var actual = Model.Casts.Where(c => c.Rule.Component == name).ToArray();
                    Check(gaze.Eyes.Count == actual.Length, name + "/" + label + " eye count");
                    for (var i = 0; i < actual.Length; ++i)
                        Check(gaze.Eyes[i].Activation == actual[i].Activation
                            && gaze.Eyes[i].Position.X == actual[i].Origin.X && gaze.Eyes[i].Position.Z == actual[i].Origin.Z
                            && gaze.Eyes[i].Range == actual[i].Rule.GazeRange, name + "/" + label + " eye fields");
                }
                else if (component is BossMod.Components.CastHint damage)
                {
                    var actual = Model.Casts.Where(c => c.Rule.Component == name).ToArray();
                    Check(damage.Casters.Select(c => c.InstanceID).SequenceEqual(actual.Select(c => c.Source.Actor.Id)),
                        name + "/" + label + " damage references/order");
                    for (var i = 0; i < actual.Length; ++i)
                        Check(Model.Activation(actual[i].Source) == World.CurrentTime.AddSeconds(
                            damage.Casters[i].CastInfo?.NPCRemainingTime ?? 0), name + "/" + label + " dynamic damage clock");
                }
                else throw new InvalidOperationException("Uncovered component: " + name);
                ++boundaries;
            }
        }
        public void Dispose() => Module.Dispose();
    }

    internal static void Run(Action<string, Action> run)
    {
        run("Crescent FATE variants eleven actual modules forty four standard components source differential", Standard);
        run("Crescent FATE variants Created cold hot quantization original references no timeout and Finish", Created);
        run("Crescent FATE variants Growth real slots cold pet hot status refresh source differential", Growth);
        run("Crescent FATE variants Growth actual Feed sixty slots negative zero capped lifetime strict refresh", GrowthFeed);
        run("Crescent FATE variants Burst actual cross component first item strict clock and risk recovery", Burst);
        run("Crescent FATE variants Concentric cross source coordinate matching independent sequences and cached Update", Concentric);
        run("Crescent FATE variants Freefall cold EventHappened independent nonzero sequence zero FIFO strict timeout", Freefall);
        run("Crescent FATE variants Storm actual cast effect history replacement cancel wrong helper strict timeout", Storm);
        run("Crescent FATE variants Frost all sorted first point two inclusive window promotion and ordinary Finish", Frost);
        run("Crescent FATE variants Puddles nine actions source uniqueness early clock sequence effect no Finish no timeout", Puddles);
        run("Crescent FATE variants Observer moving two OID groups dead filter two distinct navigation cones", Observer);
        run("Crescent FATE variants Charge length locked quantized original and separate five point two circle Finish", Charge);
        run("Crescent FATE variants Delicate actual module navigation display split full target priorities", Delicate);
        run("Crescent FATE variants same cast sampled EffectSeen cold restore and event scans do not promote Pending", InputHistory);
    }

    private static void Initialize()
    {
        if (MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment.IsInitialized) return;
        var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
        Check(Directory.Exists(sqpack), "Crescent oracle requires actual client data.");
        data = new(sqpack!, new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
        MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment.Initialize(data,
            ScriptServiceProxy.Create<Dalamud.Plugin.Services.IPluginLog>((_, _) => null));
    }

    private static void Shape(AOEShape expected, GeometryTemplate actual, string label)
    {
        var equal = (expected, actual) switch
        {
            (BossMod.AOEShapeCircle e, CircleTemplate a) => e.Radius == a.Radius,
            (BossMod.AOEShapeDonut e, DonutTemplate a) => e.InnerRadius == a.InnerRadius && e.OuterRadius == a.OuterRadius,
            (BossMod.AOEShapeCone e, FanTemplate a) => e.Radius == a.Radius && e.HalfAngle.Rad * 2 == a.AngleRadians,
            (BossMod.AOEShapeRect e, RayRectangleTemplate a) => e.LengthFront == a.Length && e.LengthBack == 0
                && e.HalfWidth == a.MaxLocalX && -e.HalfWidth == a.MinLocalX,
            (BossMod.AOEShapeCross e, DungeonCrossTemplate a) => e.Length == a.Length && e.HalfWidth == a.HalfWidth,
            _ => false,
        };
        Check(equal, label + ": exact shape parameters differ");
    }

    private static void Standard()
    {
        var count = 0;
        foreach (var definition in CrescentFateCatalog.Definitions.Skip(5))
            foreach (var rule in definition.Rules.Where(r => r.Kind is CrescentFateRuleKind.Aoe
                or CrescentFateRuleKind.Gaze or CrescentFateRuleKind.Raidwide))
            {
                ++count;
                foreach (var action in rule.Actions)
                {
                    using var f = new Fixture(definition.Name);
                    f.Cast(action); f.Compare("first Start");
                    f.Advance(.1);
                    f.Cast(action, 3, 4); f.Compare("second Start");
                    f.Effect(action, 2, 45); f.Compare("Effect does not Finish");
                    f.FinishAs(999, 2, f.Value(2).Cast!.Location); f.Compare("unrelated Finish");
                    f.End(); f.Compare("first real Finish");
                    f.Advance(20); f.Compare("capped NPC clock");
                    f.End(3); f.Compare("second real Finish");
                }
            }
        Check(count == 44, "Reviewed standard component count differs.");
        Console.WriteLine($"INFO Crescent new standard: {count} components; {boundaries} component event boundaries; {points} shape points.");
    }

    private static void Created()
    {
        using var f = new Fixture("KingOfTheCrescent", false);
        f.Add(new() { Id = 4, Oid = 0x46D8, Kind = 2, SubKind = 5, Hp = 100, Position = new(101.123f, 0, 102.456f) });
        f.Sync(); f.Activate(); f.Compare("cold sphere");
        f.Add(f.Value(4) with { Id = 5 }); f.Sync(); f.Compare("hot sphere");
        f.Advance(30); f.Compare("no timer");
        f.World.Execute(new BossMod.ActorState.OpDestroy(4)); f.Actors.RemoveAll(a => a.Id == 4);
        f.Sync(); f.Compare("destroy retains old prediction");
        Check(f.Native("Airburst").Any(c => c.Source.Destroyed), "Original destroyed actor reference lost.");
        f.Add(new() { Id = 4, Oid = 0x46D8, Kind = 2, SubKind = 5, Hp = 100, Position = new(110, 0, 110) });
        f.Sync(); f.Compare("same ID new sphere");
        f.Cast(41386, 4); f.End(4); f.Compare("Finish removes only first original ID");
        Check(f.Native("Airburst").Count(c => c.Source.Actor.Id == 4) == 1, "Finish removed all same-ID predictions.");
    }

    private static void Growth()
    {
        using var f = new Fixture("MadMudarch", false);
        f.Add(new() { Id = 4, Kind = 2, SubKind = 2, Hp = 100, Position = new(101.123f, 0, 102.456f) });
        void Status(ulong id, int slot, ushort extra, double lifetime = 30, uint statusId = 4221)
        {
            var value = new ReplayStatus(statusId, extra, 100, f.Now.AddSeconds(lifetime)) { SlotIndex = slot };
            f.Set(id, a => a with { Statuses = a.Statuses.Where(s => s.SlotIndex != slot)
                .Append(value).Where(s => s.Id != 0).ToArray() });
            f.World.Execute(new BossMod.ActorState.OpStatus(id, slot,
                new(statusId, extra, f.Now.AddSeconds(lifetime), 100)));
            f.Sync();
        }
        Status(4, 59, 2);
        Status(2, 0, 1); Status(2, 59, 2);
        f.Activate(); f.Compare("cold sixty slots include pet");
        Status(2, 0, 2); f.Compare("Extra changed Gain");
        Status(2, 0, 2, 33); f.Compare("expiry extension Gain duplicate");
        Status(2, 59, 0, statusId: 0); f.Compare("Lose does not clear");
        Status(4, 0, 2); f.Compare("hot pet excluded");
        f.Add(new() { Id = 5, Kind = 2, SubKind = 3, Hp = 100, Position = new(100, 0, 100) });
        Status(5, 59, 2); f.Compare("hot chocobo status included");
        Status(100, 59, 2); f.Compare("hot player status included");
        // Exceed the 7.1s prediction while the supplied raw statuses still have
        // positive remaining times; negative-time normalization is tested via Feed.
        f.Advance(20); f.Compare("no timeout");
        var before = f.Native("Rupture").Length;
        f.Cast(29808); f.End(); f.Compare("Finish one item");
        Check(f.Native("Rupture").Length == before - 1, "Rupture Finish did not consume exactly one.");
    }

    private static void Burst()
    {
        using var f = new Fixture("AllureOfTheOccult");
        f.Cast(48945, 2, 5); f.Cast(48946, 3, 1);
        f.Add(new() { Id = 4, Oid = 0x4D57, Kind = 2, SubKind = 5, Hp = 100, Position = new(100, 0, 100) });
        f.Cast(48950, 4, 3); f.Compare("first cross later than circle, second earlier");
        Check(f.Native("Burst").Single().Risky, "Burst used minimum cross time instead of first item.");
        f.End(); f.Compare("first cross removed, second suppresses");
        Check(!f.Native("Burst").Single().Risky, "Earlier cross did not suppress circle.");
        f.Compare("repeat query same non-danger");
        f.End(3); f.Compare("all crosses gone restores base");
        Check(f.Native("Burst").Single().Risky && !f.Native("Burst").Single().AoeColor, "Burst risk/color stayed suppressed.");
        f.Cast(48945, 2, 3); f.Compare("equal activation is risky");
    }

    private static void GrowthFeed()
    {
        Initialize();
        using var session = new MechanicSession("crescent-growth-actual-feed");
        var feed = new MechanicWorldFeed(session);
        var model = new CrescentFateEncounter(CrescentFateCatalog.Definitions.Single(d => d.Name == "MadMudarch"));
        var now = START;
        ReplayActorState[] actors =
        [
            new() { Id = 1, Oid = 0x46BD, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                Targetable = true, InCombat = true, Position = new(100, 0, 100) },
            new() { Id = 100, Kind = 1, Hp = 100, Position = new(100, 0, 100) },
            new() { Id = 2, Kind = 2, SubKind = 5, Hp = 100, Position = new(101.123f, 0, 102.456f) },
            new() { Id = 3, Kind = 2, SubKind = 2, Hp = 100 },
            new() { Id = 4, Kind = 2, SubKind = 3, Hp = 100 },
        ];
        var raw = new Dictionary<ulong, MechanicStatusDto[]>
        {
            [2] = [new(59, 4221, 2, 0xE0000000, 0), new(0, 4221, 2, 100, -5)],
            [3] = [new(59, 4221, 2, 100, 200000)],
            [4] = [],
            [100] = [],
        };
        BossMod.BossModule? module = null;
        try
        {
            void Apply(string label)
            {
                session.BeginFrame(now);
                feed.ApplyFrame(new(1252, 1018, 100, 0, [new(0, 100, 100, "")], [],
                    actors.Select(a => new MechanicActorSnapshot(a.Id, a.Oid, (int)a.Id, 0, "", 0,
                        a.Kind, a.SubKind, 0, 100, a.Position, a.Rotation, 1, a.Hp, a.MaxHp, 0, 0, 0,
                        a.Targetable, false, a.Dead, a.InCombat, 0, a.TargetId, 0, 0, 0, 0, 0, 0, 0, 0,
                        null, raw.GetValueOrDefault(a.Id) ?? [])).ToArray()));
                if (module == null)
                {
                    var type = typeof(BossMod.BossModule).Assembly.GetType(
                        "BossMod.Dawntrail.Foray.FATE.SwornToSoil.MadMudarch")!;
                    module = (BossMod.BossModule)Activator.CreateInstance(type, session.World, session.World.Actors.Find(1)!)!;
                    var component = type.Assembly.GetType(type.Namespace + ".Rupture")!;
                    typeof(BossMod.BossModule).GetMethod("ActivateComponent")!.MakeGenericMethod(component).Invoke(module, null);
                }
                for (var i = 0; i < actors.Length; ++i)
                    actors[i] = actors[i] with { Statuses = (raw.GetValueOrDefault(actors[i].Id) ?? []).Select(s =>
                        new ReplayStatus(s.Id, s.Extra, s.SourceId, ReplayStatus.Expiration(now, s.RemainingSeconds))
                        { SlotIndex = s.Slot, ZeroRemainingTime = s.RemainingSeconds == 0 }).ToArray() };
                model.Synchronize(actors, new(1969, new(100, 0, 100), 40), 100, now);
                var expected = ((BossMod.Components.GenericAOEs)module.Components.Single())
                    .ActiveAOEs(0, session.World.Actors.Find(100)!).ToArray();
                var actual = model.VisibleAoes();
                Check(expected.Length == actual.Length, $"{label}: Feed/native count {expected.Length}/{actual.Length}");
                for (var i = 0; i < expected.Length; ++i)
                    Check(expected[i].Activation == actual[i].Activation && expected[i].Origin.X == actual[i].Origin.X
                        && expected[i].Origin.Z == actual[i].Origin.Z, label + ": Feed origin/clock/order");
                foreach (var actor in actors)
                    for (var slot = 0; slot < 60; ++slot)
                    {
                        var source = session.World.Actors.Find(actor.Id)!.Statuses[slot];
                        var native = model.Actors[actor.Id].Statuses[slot];
                        Check(source.ID == (native?.Id ?? 0) && (source.ID == 0
                            || source.Extra == native!.Param && source.SourceID == native.SourceId
                                && source.ExpireAt == native.ExpiresAt), $"{label}: raw slot {actor.Id}/{slot}");
                    }
            }
            Apply("cold Pet slots59/0 zero negative and100000 cap");
            Check(model.Casts.Count == 3 && model.Actors[2].Statuses[59]!.ExpiresAt == now
                && model.Actors[3].Statuses[59]!.ExpiresAt == now.AddSeconds(100000), "Raw status normalization missing.");
            raw[2] = [new(59, 4221, 2, 0, 1), new(0, 4221, 2, 100, 4)];
            Apply("exactly one second extension and shorten suppress Gain");
            Check(model.Casts.Count == 3, "One-second expiry refresh incorrectly generated Gain.");
            raw[2] = [new(59, 4221, 2, 0, 2.001f), new(0, 4221, 1, 100, 4)];
            Apply("greater than one second and Param change");
            Check(model.Casts.Count == 4, "Strict expiry Gain missing.");
            raw[2] = [new(59, 4221, 2, 100, 2.001f), new(0, 4221, 2, 100, 4)];
            raw[3] = [new(59, 4221, 2, 1, 30)];
            raw[4] = [new(59, 4221, 2, 100, -9)];
            raw[100] = [new(59, 4221, 2, 100, 9)];
            Apply("source identity Gain hot Pet excluded Chocobo and Player included");
            Check(model.Casts.Count == 8, "Hot type/source callbacks differ.");
            actors[2] = actors[2] with { SubKind = 2 };
            raw[2] = [new(59, 4221, 2, 1, 2.001f)];
            Apply("same generation captured ActorType retained");
            Check(model.Casts.Count == 9, "Snapshot type change rewrote native original ActorType.");
            foreach (var id in raw.Keys.ToArray()) raw[id] = [];
            now = now.AddSeconds(50);
            Apply("Lose and no prediction timeout");
            Check(model.Casts.Count == 9, "Lose or timeout removed source predictions.");
        }
        finally { module?.Dispose(); }
    }

    private static void Concentric()
    {
        foreach (var name in new[] { "AnUnendingDuty", "GaleForceEncounter" })
        {
            var rule = CrescentFateCatalog.Definitions.Single(d => d.Name == name).Rules
                .Single(r => r.Kind == CrescentFateRuleKind.Concentric);
            foreach (var start in rule.Actions.Where((_, i) => i % 3 == 0))
            {
                using var f = new Fixture(name);
                f.Cast(start, 2); f.Cast(start, 3);
                f.Compare("two independent origins");
                var origin = f.Value(2).Cast!.Location;
                f.FinishAs(rule.Actions[Array.IndexOf(rule.Actions, start) + 1], 1, origin);
                f.Update(); f.Compare("wrong order no advance");
                f.FinishAs(start, 1, origin + new Vector3(1.001f, 0, 0));
                f.Update(); f.Compare("outside per-axis epsilon");
                var old = f.Native(rule.Component).ToArray();
                f.FinishAs(start, 1, origin + new Vector3(1, 0, 1));
                Check(f.Native(rule.Component).SequenceEqual(old), "Concentric cache refreshed before Update.");
                f.Update(); f.Compare("cross source within both axes");
                Check(f.Native(rule.Component)[0].Shape is DonutTemplate { InnerRadius: 10, OuterRadius: 20 },
                    "First matching sequence did not advance.");
                f.Advance(10);
                f.FinishAs(rule.Actions[Array.IndexOf(rule.Actions, start) + 1], 1, origin);
                f.Update(); f.Compare("next clock is consumer now plus two");
                Check(f.Native(rule.Component)[0].Activation == f.Now.AddSeconds(2d), "Concentric next clock reused old prediction.");
                f.FinishAs(rule.Actions[Array.IndexOf(rule.Actions, start) + 2], 1, origin);
                f.Update(); f.Compare("only first matching sequence removed");
                f.End(); f.End(3); f.Compare("remaining real source Finish");
            }
        }
    }

    private static void Freefall()
    {
        using var f = new Fixture("GaleForceEncounter", false);
        f.Cast(47598, 2, happened: true); f.Activate(); f.Compare("cold event already happened");
        Check(f.Native("FreefallSequence").Length == 0, "Cold happened cast manufactured three hits.");
        f.End(); f.Cast(47598); f.Compare("three cached hits first only");
        Check(f.Model.Casts.Count(c => c.Rule.Kind == CrescentFateRuleKind.Freefall) == 3
            && !f.Native("FreefallSequence").Single().Risky, "Freefall did not preserve non-risky three-item model.");
        f.Effect(47584, sequence: 50); f.Compare("first effect");
        f.Effect(47584, sequence: 50); f.Compare("duplicate ignored");
        f.Effect(47584); f.Compare("zero consumes");
        f.Effect(47584); f.Compare("second zero consumes");
        f.Cast(47598, 3); f.Effect(47584, sequence: 50); f.Compare("new Start resets component sequence set");
        var next = f.Native("FreefallSequence").Single().Activation;
        f.Advance((next.AddSeconds(1d) - f.Now).TotalSeconds, false);
        f.Update(); f.Compare("equal timeout retains");
        f.Advance(.0000001, false);
        Check(f.Native("FreefallSequence").Length == 1, "Advance applied Update cleanup.");
        f.Update(); f.Compare("strict timeout promotes");
        f.Advance(20); f.Compare("multiple expired items");
    }

    private static void Storm()
    {
        using var f = new Fixture("RelicIcewolf");
        f.Cast(49756); f.Compare("circle caster position");
        f.Effect(49756, 2, 1); f.End(); f.Compare("matching actual Effect marks normal Finish");
        Check(f.Native("StormWithinWithout").Length == 1, "Normal Finish cleared delayed circle.");
        f.Effect(49767); f.Compare("wrong helper retained");
        f.Effect(49766); f.Compare("other source expected helper clears");
        f.Cast(49757); f.Compare("donut replacement");
        f.End(); f.Compare("cancel without effect clears");
        f.Cast(49756); f.Cast(49757, 3); f.Compare("second source replacement");
        f.Effect(49757, 3); f.End(3); f.Compare("normal donut retained");
        var deadline = f.Native("StormWithinWithout").Single().Activation.AddSeconds(1d);
        f.Advance((deadline - f.Now).TotalSeconds); f.Compare("exact timeout retained");
        f.Advance(.0000001); f.Compare("strict timeout removed");
    }

    private static void Frost()
    {
        using var f = new Fixture("RelicIcewolf");
        f.Cast(49761, 2, 4); f.Cast(49761, 3, 3);
        f.Add(new() { Id = 4, Oid = 0x4DA0, Kind = 2, SubKind = 5, Hp = 100 });
        f.Cast(49761, 4, 3.2f); f.Compare("float NPC clocks before precise boundary");
        // 3.2f + .3f is not exactly (3f + .3f) + .2d. Set the
        // independent source/native lists to exact DateTime boundary ticks.
        var cone = (BossMod.Components.SimpleAOEs)f.Module.Components.Single(c => c.GetType().Name == "AgeOfEndlessFrost");
        var deadline = cone.Casters.Single(c => c.ActorID == 3).Activation.AddSeconds(.2d);
        var sourceIndex = cone.Casters.FindIndex(c => c.ActorID == 4);
        var sourceValue = cone.Casters[sourceIndex];
        sourceValue.Activation = deadline;
        cone.Casters[sourceIndex] = sourceValue;
        var nativeIndex = f.Model.Casts.FindIndex(c => c.Source.Actor.Id == 4);
        f.Model.Casts[nativeIndex] = f.Model.Casts[nativeIndex] with { Activation = deadline };
        f.Compare("exact inclusive risk window");
        Check(f.Native("AgeOfEndlessFrost").Count(c => c.Risky) == 2, "Inclusive first-wave deadline wrong.");
        sourceValue.Activation = deadline.AddTicks(1);
        cone.Casters[cone.Casters.FindIndex(c => c.ActorID == 4)] = sourceValue;
        f.Model.Casts[nativeIndex] = f.Model.Casts[nativeIndex] with { Activation = deadline.AddTicks(1) };
        f.Compare("one tick outside risk window");
        Check(f.Native("AgeOfEndlessFrost").Count(c => c.Risky) == 1, "Risk window extended beyond inclusive deadline.");
        f.End(3); f.End(4); f.Compare("tail promotes danger");
        Check(f.Native("AgeOfEndlessFrost").Single().Danger, "Last cone did not promote.");
        f.FinishAs(999, 2, default); f.Compare("ordinary watched-only Finish");
        f.End(); f.Compare("watched Finish");
    }

    private static void Puddles()
    {
        using var f = new Fixture("Thunderregnum");
        var rule = f.Model.Definition.Rules.Single(r => r.Kind == CrescentFateRuleKind.Puddle);
        for (var i = 0; i < 9; ++i)
        {
            var id = (ulong)(10 + i);
            f.Add(new() { Id = id, Oid = 0x4D65, Kind = 2, SubKind = 5, Hp = 100 });
            f.Cast(rule.Actions[i], id, 5 + i * .5f);
        }
        f.Compare("all nine visible first only dangerous early clock");
        Check(f.Native("ThunderboltPuddle").Length == 9 && f.Native("ThunderboltPuddle").Count(c => c.Risky) == 1,
            "Puddle full route/risk window wrong.");
        f.Cast(rule.Actions[1], 10, 20); f.Compare("same ActorID cannot add");
        f.End(10); f.Compare("ordinary Finish retains");
        f.Advance(40); f.Compare("no timeout");
        f.Effect(rule.Actions[0], 10, 123); f.Compare("actual effect by source");
        f.Effect(rule.Actions[0], 11, 123); f.Compare("nonzero sequence is component-wide");
        f.Effect(rule.Actions[0], 11); f.Compare("zero source consumes");
        f.Effect(rule.Actions[0], 12); f.Compare("another zero source consumes");
        f.Compare("repeat query preserves sorted fields");
    }

    private static void Observer()
    {
        using var f = new Fixture("APryingEye");
        foreach (var (id, oid) in new[] { (10UL, 0x4818u), (11UL, 0x47DDu), (12UL, 0x4818u), (13UL, 0x47DDu) })
            f.Add(new() { Id = id, Oid = oid, Kind = 2, SubKind = 5, Hp = 100, Position = new((float)id + 100, 0, 100) });
        f.Sync(); f.Compare("OID grouped order");
        Check(f.Native("SearchMarkOfDeath").Select(c => c.Source.Actor.Id).SequenceEqual(new ulong[] { 11, 13, 10, 12 }),
            "Observer lost concatenated OID order.");
        f.Set(11, a => a with { Position = a.Position + Vector3.UnitZ * 3, Rotation = 1 });
        f.Actor(11).PosRot = new(f.Value(11).Position, 1);
        f.Set(13, a => a with { Dead = true });
        f.World.Execute(new BossMod.ActorState.OpDead(13, true));
        f.Sync(); f.Advance(.4); f.Compare("dynamic position rotation dead filter clock");
        var extras = f.Model.ObserverExtras();
        Check(extras.Length == 3 && extras.All(c => c.Shape is FanTemplate { Radius: 6, AngleRadians: MathF.PI / 2 }
            && c.Activation == default), "Observer extra immediate R6/90 navigation missing.");
        var source = (BossMod.Components.GenericAOEs)f.Module.Components.Single(c => c.GetType().Name == "SearchMarkOfDeath");
        var hints = new BossMod.AIHints();
        source.AddAIHints(0, f.Actor(100), default, hints);
        Check(hints.ForbiddenZones.Count == 6, "Source both navigation sets not activated.");
        f.Cast(43043); f.Compare("Oogle default range 10000");
    }

    private static void Charge()
    {
        using var f = new Fixture("ServingDarkness");
        f.Cast(42178, 1, location: new(112, 0, 103)); f.Compare("quantized charge real length");
        Check(f.Model.ChargeGoal == new Vector3(112, 0, 103), "Sweeping charge goal intent lost.");
        var hints = new BossMod.AIHints();
        var charge = f.Module.Components.Single(c => c.GetType().Name == "SweepingMenacingCharge");
        charge.AddAIHints(0, f.Actor(100), default, hints);
        Check(hints.GoalZones.Count == 1
            && MasterPlayer.MechanicHost.MechanicHintsConvert.ConvertGoalZones(hints.GoalZones,
                f.Actor(100).Position, 40, 0).Count == 0, "Weak charge goal changed existing bridge consumption.");
        for (var x = -6f; x <= 6; x += .5f)
        {
            var p = new Vector3(112 + x, 0, 103);
            Check(hints.GoalZones[0](new(p.X, p.Z)) == f.Model.ChargeGoalScore(p),
                "Weak charge goal weight/closed radius differs.");
        }
        f.Cast(42179, 1, location: new(110, 0, 110)); f.Compare("menacing charge and delayed circle");
        Check(f.Model.ChargeGoal == null, "Menacing charge inherited Sweeping goal.");
        f.End(1); f.Compare("own Finish leaves circle");
        f.Advance(50); f.Compare("delayed circle has no timeout");
        f.FinishAs(42180, 3, default); f.Compare("different caster later Finish clears circle");
    }

    private static void Delicate()
    {
        using var f = new Fixture("ADelicateBalance");
        var hints = new BossMod.AIHints();
        hints.Clear();
        hints.PathfindMapCenter = f.Actor(100).Position;
        foreach (var oid in new[] { f.Model.Definition.PrimaryOid, 0x4770u, 0x1234u })
            foreach (var combat in new[] { false, true })
            {
                var actor = new BossMod.Actor((ulong)(hints.PotentialTargets.Count + 20), oid, 20, 0, "", 0,
                    BossMod.ActorType.Enemy, default, 100, default) { InCombat = combat };
                hints.PotentialTargets.Add(new(actor, 0, false));
            }
        var pending = new BossMod.Actor(50, f.Model.Definition.PrimaryOid, 50, 0, "", 0, BossMod.ActorType.Enemy,
            default, 100, default);
        hints.PotentialTargets.Add(new(pending, -1, false));
        f.Module.CalculateAIHints(0, f.Actor(100), default, hints);
        foreach (var enemy in hints.PotentialTargets)
        {
            var native = f.Model.TargetPriority(new() { Oid = enemy.Actor.OID, InCombat = enemy.Actor.InCombat });
            Check(enemy.Priority == (enemy.Actor.InstanceID == 50 ? -1 : native), "Full module target setter priority differs.");
        }
        var nav = (MasterPlayer.Spatial.Geometry.CircleGeometry)f.Model.NavigationBoundary(f.Value(100));
        Check(nav.Position == f.Value(100).Position && nav.Radius == 30
            && f.Model.Radius == 40 && hints.PathfindMapBounds is BossMod.ArenaBoundsCircle { Radius: 30 },
            "Module override improperly acquired OpenWorldFate navigation circle.");
        Check(QuestTargetPriority.Select(1252,
            [new() { Id = 20, Oid = f.Model.Definition.PrimaryOid, Kind = 2, IsEnemy = true,
                Targetable = true, ObjectIndex = 20, Hp = 100 }], 0, default, assignedPriority: f.Model.TargetPriority) == 20,
            "Native model target override not consumable by shared selector.");
    }

    private static void InputHistory()
    {
        using (var f = new Fixture("GaleForceEncounter", false))
        {
            f.Cast(47598);
            f.Set(2, a => a with { Cast = a.Cast! with { EffectSeen = true } });
            f.World.Execute(new BossMod.ActorState.OpCastEvent(2,
                new(new(BossMod.ActionType.Spell, 47598), 100, 0, 0, default, 0, 0, default)));
            // The source observed the effect before activation; MP restores the
            // same fact from the native response snapshot, without a second Effect.
            f.Sync(); f.Activate(); f.Compare("later same-cast response before cold activate");
            Check(f.Model.Casts.Count == 0, "Sampled EffectSeen history manufactured a cold Freefall.");
        }
        using (var f = new Fixture("RelicIcewolf"))
        {
            f.Cast(49756);
            var activation = f.Native("StormWithinWithout").Single().Activation;
            f.Set(2, a => a with { Cast = a.Cast! with { EffectSeen = true } });
            f.World.Execute(new BossMod.ActorState.OpCastEvent(2,
                new(new(BossMod.ActionType.Spell, 49756), 100, 0, 0, default, 0, 0, default)));
            f.Sync(); f.End(); f.Compare("later sampled response preserves normal Storm Finish");
            Check(f.Native("StormWithinWithout").Single().Activation == activation, "Sampled history restarted Storm clock.");
        }
        var definition = CrescentFateCatalog.Definitions.Single(d => d.Name == "KingOfTheCrescent");
        var model = new CrescentFateEncounter(definition);
        ReplayActorState[] world =
        [
            new() { Id = 1, Oid = definition.PrimaryOid, Kind = 2, SubKind = 5, Hp = 100,
                Targetable = true, InCombat = true, Position = new(100, 0, 100) },
            new() { Id = 100, Kind = 1, Hp = 100, Position = new(100, 0, 100) },
        ];
        var fate = new ReplayActiveFate(definition.FateId, new(100, 0, 100), 40);
        model.Synchronize(world, fate, 100, START);
        world = [.. world, world[0] with { Id = 3 }];
        model.Synchronize(world, fate, 100, START.AddSeconds(.1));
        world = world.Where(a => a.Id != 1).ToArray();
        for (var i = 0; i < 3; ++i)
            model.Synchronize(world, fate, 100, START.AddSeconds(.2), updateModules: false);
        Check(model.Primary?.Actor.Id == 1 && model.Primary.Destroyed, "Event scan promoted Pending before manager update.");
        model.Synchronize(world, fate, 100, START.AddSeconds(.3));
        Check(model.Primary?.Actor.Id == 1 && model.Completed, "Manager did not retire destroyed loaded module first.");
        model.Synchronize(world, fate, 100, START.AddSeconds(.4));
        Check(model.Active && model.Primary?.Actor.Id == 3, "Next manager update did not promote retired primary.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
