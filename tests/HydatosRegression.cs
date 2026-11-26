using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Mechanics.Hydatos;
using MasterPlayer.Mechanics.Quests;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;

internal static class HydatosRegression
{
    private static readonly DateTime START = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
    private static Lumina.GameData? oracleData;
    private static int comparisons;
    private static int points;

    private sealed class Fixture
    {
        internal readonly HydatosEncounter Model;
        internal readonly BossMod.WorldState World;
        internal readonly BossMod.BossModule Module;
        internal readonly List<ReplayActorState> Actors = [];
        internal readonly List<BossMod.BossComponent> Components = [];
        internal DateTime Now = START;

        internal Fixture(string name)
        {
            Initialize();
            World = new(1, "hydatos-source-oracle");
            var definition = HydatosCatalog.Definitions.Single(d => d.Name == name);
            Model = new(definition);
            Add(new() { Id = 1, Oid = definition.PrimaryOid, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                Targetable = true, InCombat = true, HitboxRadius = 5, Position = new(100, 0, 100) });
            Add(new() { Id = 100, Kind = 1, Hp = 100, MaxHp = 100, HitboxRadius = .5f, Position = new(100, 0, 100) });
            Add(new() { Id = 101, Kind = 1, Hp = 100, MaxHp = 100, HitboxRadius = .5f, Position = new(104, 0, 100) });
            Add(new() { Id = 2, Oid = 0x233C, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                HitboxRadius = 2, Position = new(100.125f, 0, 80.25f) });
            Add(new() { Id = 3, Oid = 0x233C, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                Position = new(105, 0, 85) });
            World.Execute(new BossMod.PartyState.OpModify(0, new(100, 100, false, "source")));
            World.Execute(new BossMod.PartyState.OpModify(1, new(101, 101, false, "other")));
            var ns = "BossMod.Stormblood.Foray.Hydatos." + (name == "Phuabo" ? "Tristitia" : name);
            var type = typeof(BossMod.BossModule).Assembly.GetType(ns + "." + name)!;
            Module = (BossMod.BossModule)RuntimeHelpers.GetUninitializedObject(type);
            typeof(BossMod.BossModule).GetField("WorldState")!.SetValue(Module, World);
            Module.PrimaryActor = Actor(1);
            Sync();
        }

        internal BossMod.Actor Actor(ulong id) => World.Actors.Find(id)!;
        internal void Add(ReplayActorState a)
        {
            Actors.Add(a);
            World.Execute(new BossMod.ActorState.OpCreate(a.Id, a.Oid, (int)a.Id, 0, "", 0,
                a.Kind == 1 ? BossMod.ActorType.Player : BossMod.ActorType.Helper, default, 100,
                new(a.Position, a.Rotation), a.HitboxRadius, new(100, 100, 0, 100, 100), a.Targetable, false, 0, 0, 0));
            foreach (var component in Components) component.OnActorCreated(Actor(a.Id));
        }
        internal T Component<T>(string name) where T : BossMod.BossComponent
        {
            var type = Module.GetType().Assembly.GetType(Module.GetType().Namespace + "." + name)!;
            var c = (T)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [Module], null)!;
            Components.Add(c);
            return c;
        }
        internal void Set(ulong id, Func<ReplayActorState, ReplayActorState> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Sync()
        {
            World.Frame.Timestamp = Now;
            Model.Synchronize(Actors, Now, 100, [new(0, "", 100), new(1, "", 101)]);
        }
        internal void Cast(uint action, ulong source = 2, ulong target = 100, Vector3? location = null)
        {
            End(source);
            var loc = location ?? new Vector3(103.125f, 0, 97.25f);
            var spell = new BossMod.ActorCastInfo { Action = new(BossMod.ActionType.Spell, action),
                TargetID = target, TotalTime = 3, Location = loc, Rotation = new(.4f) };
            Actor(source).CastInfo = spell;
            Set(source, a => a with { Cast = new(action, target, loc, .4f, Now, Now.AddSeconds(3.3), false, false)
                { TotalTime = 3, IsSpell = true } });
            Sync();
            foreach (var c in Components) c.OnCastStarted(Actor(source), spell);
        }
        internal void End(ulong source = 2)
        {
            if (Actor(source).CastInfo is not { } old) return;
            Set(source, a => a with { Cast = null });
            Sync();
            foreach (var c in Components) c.OnCastFinished(Actor(source), old);
            Actor(source).CastInfo = null;
        }
        internal void Effect(uint action, ulong source = 3, ulong target = 0, float rotation = .7f)
        {
            Model.Effect(source, action, rotation, target);
            var spell = new BossMod.ActorCastEvent(new(BossMod.ActionType.Spell, action), target, 0, 0,
                default, 0, 0, new(rotation));
            foreach (var c in Components) c.OnEventCast(Actor(source), spell);
        }
        internal void Icon(uint icon, ulong source = 100, ulong target = 101)
        {
            Model.Icon(source, icon, target);
            foreach (var c in Components) c.OnEventIcon(Actor(source), icon, target);
        }
        internal HydatosAoe[] Native(string component)
            => Model.VisibleAoes().Where(a => a.Rule.Component == component).ToArray();
    }

    internal static void Run(Action<string, Action> run)
    {
        run("Hydatos native standard exact geometry watched Finish gaze charge source differential", Standard);
        run("Hydatos native Watcher Ice Tail Charybdis nineteen event differential", Watcher);
        run("Hydatos native DualCast latch float clock global FIFO sticky color source differential", Dual);
        run("Hydatos native Daphne raw icon target FIFO duplicate empty counter source differential", Stack);
        run("Hydatos native Ovni icon target three effects and independent pull source differential", Ovni);
        run("Hydatos native raw primary pending reference death phase and no fate gating", Lifecycle);
        run("Hydatos native client territory CFC and actual action extra cast manifest", ClientData);
        run("Hydatos native actual Feed sixty slots cold pet hot type refresh dispel source differential", DispelFeed);
        run("Hydatos native actual module double activation final DualCast cold world order", ColdDualFeed);
        run("Hydatos native actual Feed old party immunity callbacks strict expiry classes refresh and slot reuse", ImmunityFeed);
        run("Hydatos native actual manager seven source loaded order active ties pending and reset", RegionFeed);
        run("Hydatos native source AI differential stacks overlap buddy spread diagnostic and bait navigation", Navigation);
        run("Hydatos native actual module navigation circle square per-axis cached center source differential", BoundaryFeed);
        run("Hydatos native source single target self cast dynamic target and raw NPC damage clock", DamageClock);
        run("Hydatos native actual Feed invalid target normalization preserves original cast identity", InvalidTargetFeed);
        run("Hydatos native actual module all target priorities Pointless protection Watcher defaults and XZ selection", TargetPriorities);
    }

    private static void Initialize()
    {
        if (MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment.IsInitialized) return;
        var path = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
        Check(Directory.Exists(path), "Hydatos oracle requires FFXIV_SQPACK, cannot skip.");
        oracleData = new(path!, new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
        MasterPlayer.MechanicRuntime.MechanicRuntimeEnvironment.Initialize(oracleData,
            ScriptServiceProxy.Create<Dalamud.Plugin.Services.IPluginLog>((_, _) => null));
    }

    private static void Compare(Fixture f, string component, BossMod.Components.GenericAOEs oracle, string at)
    {
        var expected = oracle.ActiveAOEs(0, f.Actor(100)).ToArray();
        var actual = f.Native(component);
        Check(expected.Length == actual.Length, $"{component}/{at} count {expected.Length}/{actual.Length}");
        for (var i = 0; i < expected.Length; ++i)
        {
            var e = expected[i];
            var a = actual[i];
            var label = $"{component}/{at}[{i}]";
            Check(e.Origin.X == a.Origin.X && e.Origin.Z == a.Origin.Z && e.Rotation.Rad == a.Rotation,
                $"{label} origin/rotation {e.Origin}/{a.Origin} {e.Rotation.Rad:R}/{a.Rotation:R}");
            Check(e.Activation == a.Activation, $"{label} clock {e.Activation:O}/{a.Activation:O}");
            Check(e.Risky == a.Risky && (e.Color == BossMod.Colors.Danger) == a.Danger, $"{label} risk/color");
            var parameters = (e.Shape, a.Shape) switch
            {
                (BossMod.AOEShapeCircle x, CircleTemplate y) => x.Radius == y.Radius,
                (BossMod.AOEShapeDonut x, DonutTemplate y) => x.InnerRadius == y.InnerRadius && x.OuterRadius == y.OuterRadius,
                (BossMod.AOEShapeCone x, FanTemplate y) => x.Radius == y.Radius && x.HalfAngle.Rad * 2 == y.AngleRadians,
                (BossMod.AOEShapeCross x, DungeonCrossTemplate y) => x.Length == y.Length && x.HalfWidth == y.HalfWidth,
                (BossMod.AOEShapeRect x, RayRectangleTemplate y) => x.LengthFront == y.Length && x.LengthBack == 0
                    && -x.HalfWidth == y.MinLocalX && x.HalfWidth == y.MaxLocalX,
                (BossMod.AOEShapeRect x, RectangleTemplate y) => x.LengthFront * 2 == y.Length
                    && x.LengthBack == x.LengthFront && x.HalfWidth * 2 == y.Width,
                _ => false,
            };
            Check(parameters, $"{label} shape type/parameters");
            for (var x = -81; x <= 81; x += 9)
                for (var z = -81; z <= 81; z += 9)
                {
                    var p = a.Origin + new Vector3(x + .13f, 0, z + .21f);
                    Check(e.Check(new(p.X, p.Z)) == DungeonSourceGeometry.Contains(a.Shape!, a.Origin, a.Rotation, p),
                        $"{label} shape point {p}");
                    ++points;
                }
        }
        ++comparisons;
    }

    private static void Standard()
    {
        comparisons = points = 0;
        var count = 0;
        foreach (var d in HydatosCatalog.Definitions)
            foreach (var rule in d.Rules.Where(r => r.Kind is HydatosRuleKind.Aoe or HydatosRuleKind.Gaze or HydatosRuleKind.Charge))
                foreach (var action in rule.Actions)
                {
                    ++count;
                    var f = new Fixture(d.Name);
                    var c = f.Component<BossMod.BossComponent>(rule.Component);
                    void Equal(string at)
                    {
                        if (c is BossMod.Components.GenericAOEs aoes) Compare(f, rule.Component, aoes, at);
                        else if (c is BossMod.Components.CastGaze gaze)
                        {
                            var e = gaze.ActiveEyes(0, f.Actor(100)).ToArray();
                            var a = f.Native(rule.Component);
                            Check(e.Length == a.Length, $"{rule.Component}/{at} gaze count");
                            for (var i = 0; i < e.Length; ++i)
                                Check(e[i].Position.X == a[i].Origin.X && e[i].Position.Z == a[i].Origin.Z
                                    && e[i].Activation == a[i].Activation && e[i].Range == rule.Range,
                                    $"{rule.Component}/{at} eye origin/clock/range");
                            ++comparisons;
                        }
                        else throw new InvalidOperationException("Unreviewed standard family " + c.GetType());
                    }
                    f.Cast(action); Equal("start1");
                    f.Cast(action, 3); Equal("start2");
                    f.Now = f.Now.AddSeconds(20); f.Sync(); Equal("no-timeout");
                    f.End(); Equal("Finish-first");
                    f.End(3); Equal("Finish-second");
                }
        Console.WriteLine($"Hydatos standard {count} action families {comparisons} boundaries {points} source shape points.");
    }

    private static void Watcher()
    {
        var f = new Fixture("ProvenanceWatcher");
        var ice = f.Component<BossMod.Components.GenericAOEs>("IceAndLevin");
        var tail = f.Component<BossMod.Components.GenericAOEs>("HotTailSecond");
        var pool = f.Component<BossMod.Components.GenericAOEs>("Charybdis");
        f.Cast(14997); Compare(f, "IceAndLevin", ice, "start");
        f.End(); Compare(f, "IceAndLevin", ice, "finish-retains");
        f.Now = f.Now.AddSeconds(10); f.Sync(); Compare(f, "IceAndLevin", ice, "past-clock");
        f.Effect(15006); Compare(f, "IceAndLevin", ice, "other-source-clear");
        f.Cast(14999); f.End(); Compare(f, "HotTailSecond", tail, "finish-does-not-create");
        f.Effect(14999); Compare(f, "HotTailSecond", tail, "effect");
        f.Effect(14999, 2, rotation: -1); Compare(f, "HotTailSecond", tail, "replace");
        f.Effect(15007); Compare(f, "HotTailSecond", tail, "clear");
        f.Add(new() { Id = 40, Oid = 0x2768, Kind = 2, Hp = 1, Position = new(102.123f, 0, 98.321f) });
        f.Add(new() { Id = 41, Oid = 0x2768, Kind = 2, Hp = 1, Position = new(103, 0, 98) });
        f.Sync(); Compare(f, "Charybdis", pool, "created");
        for (var i = 1; i <= 19; ++i)
        { f.Effect(15002, 40); Compare(f, "Charybdis", pool, $"effect-{i}"); }
        Check(f.Native("Charybdis").Single().Source.Actor.Id == 41, "Other source pool was consumed.");
        f.Actors.RemoveAll(a => a.Id == 41);
        f.World.Execute(new BossMod.ActorState.OpDestroy(41));
        f.Sync(); Compare(f, "Charybdis", pool, "destroyed-retains");
    }

    private static void Dual()
    {
        foreach (var action in new uint[] { 15914, 15912 })
        {
            var f = new Fixture("Tristitia");
            var c = f.Component<BossMod.Components.GenericAOEs>("TornadoIIAerogaIVDualCast");
            f.Cast(15909); f.Cast(action); Compare(f, "TornadoIIAerogaIVDualCast", c, "dual");
            f.End(); Compare(f, "TornadoIIAerogaIVDualCast", c, "Finish-retains");
            f.Effect(15913); Compare(f, "TornadoIIAerogaIVDualCast", c, "global-FIFO-promote");
            f.Cast(action); Compare(f, "TornadoIIAerogaIVDualCast", c, "new-second");
            f.Effect(15915, 2); Compare(f, "TornadoIIAerogaIVDualCast", c, "sticky-last-color");
            f.Effect(15912); Compare(f, "TornadoIIAerogaIVDualCast", c, "clear");
        }
    }

    private static void Stack()
    {
        var f = new Fixture("Daphne");
        var c = f.Component<BossMod.Components.GenericStackSpread>("Netherwater");
        void Equal(string at)
        {
            var a = f.Native("Netherwater");
            Check(c.Stacks.Count == a.Length, $"{at} stack count");
            for (var i = 0; i < a.Length; ++i)
                Check(c.Stacks[i].Target.InstanceID == a[i].Target!.Actor.Id && c.Stacks[i].Activation == a[i].Activation
                    && c.Stacks[i].MinSize == 4 && c.Stacks[i].MaxSize == 24, $"{at}[{i}] original icon target/clock/size");
        }
        f.Icon(62); Equal("source-not-target");
        f.Icon(62, 101, 100); f.Icon(62); Equal("duplicates");
        f.Effect(15033, target: 100); Equal("first-target-match");
        f.Effect(15033, target: 900); Equal("fallback-FIFO");
        f.Effect(15033, target: 100); Equal("empty");
        f.Effect(15033); f.Icon(62); f.Effect(15033); Equal("empty-counter-not-reset");
        Check(f.Native("Netherwater").Length == 1, "Empty counter was incorrectly normalized.");
    }

    private static void Ovni()
    {
        var f = new Fixture("Ovni");
        var c = f.Component<BossMod.Components.GenericStackSpread>("IonShower");
        f.Icon(111);
        Check(c.Spreads.Single().Target.InstanceID == 101 && f.Native("IonShower").Single().Target!.Actor.Id == 101,
            "Ion icon did not resolve targetID.");
        for (var i = 1; i <= 3; ++i)
        {
            f.Effect(14788);
            Check(c.Spreads.Count == f.Native("IonShower").Length && c.Spreads.Count == (i < 3 ? 1 : 0),
                $"Ion Effect {i} clear boundary.");
        }
        var kb = f.Component<BossMod.Components.GenericKnockback>("PullOfTheVoid");
        f.Cast(14782);
        var e = kb.ActiveKnockbacks(0, f.Actor(100)).ToArray().Single();
        var a = f.Native("PullOfTheVoid").Single();
        Check(e.Origin.X == a.Origin.X && e.Origin.Z == a.Origin.Z && e.Activation == a.Activation
            && a.Origin != f.Actor(2).PosRot.AsVector3() && e.MinDistance == a.MinDistance && a.MinDistance == 2.5f,
            "Pull origin/clock/frozen Party0 hitbox differs.");
    }

    private static void Lifecycle()
    {
        var f = new Fixture("Ceto");
        f.Cast(15468);
        f.Set(1, a => a with { Targetable = false });
        f.Sync(); Check(f.Model.Active, "Targetable loss reset active phase.");
        f.Set(1, a => a with { InCombat = false });
        f.Sync(); Check(!f.Model.Active && f.Model.Aoes.Count == 0, "SimpleBoss combat reset missing.");
        f.Set(1, a => a with { Targetable = true, InCombat = true });
        f.Sync(); Check(f.Model.Active && f.Model.Aoes.Count == 1, "Cold helper not rebuilt.");
        f.Set(1, a => a with { Dead = true }); f.Sync();
        Check(f.Model.Completed && !f.Model.Active, "Default death phase did not complete.");
        var p = new Fixture("Phuabo");
        p.Set(1, a => a with { Dead = true, Hp = 0 }); p.Sync();
        Check(p.Model.Active && !p.Model.Completed, "Phuabo dead was treated as destroyed.");
        p.Add(new() { Id = 50, Oid = 0x2740, Kind = 2, Hp = 100, Targetable = true }); p.Sync();
        Check(p.Model.Completed && !p.Model.Active, "Phuabo targetable boss transition missing.");
    }

    private static void ClientData()
    {
        Initialize();
        var territory = BossMod.Service.LuminaRow<Lumina.Excel.Sheets.TerritoryType>(HydatosCatalog.TERRITORY);
        Check(territory?.ContentFinderCondition.RowId == HydatosCatalog.CFC, "Hydatos territory->CFC differs.");
        var cfc = BossMod.Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(HydatosCatalog.CFC);
        Check(cfc?.TerritoryType.RowId == HydatosCatalog.TERRITORY, "Hydatos CFC->territory differs.");
        for (var raw = 0; raw <= ushort.MaxValue; ++raw)
            Check(HydatosEncounter.DecodeRotation((ushort)raw) == BossMod.Network.PacketDecoder.IntToFloatAngle((ushort)raw).Rad,
                $"Effect raw rotation float conversion differs at {raw}.");
        foreach (var action in HydatosCatalog.Definitions.SelectMany(d => d.Rules)
            .Where(r => r.Kind != HydatosRuleKind.Dispel).SelectMany(r => r.Actions).Distinct())
        {
            Console.WriteLine($"Hydatos actual ExtraCastTime {action}={new BossMod.ActionID(BossMod.ActionType.Spell, action).CastTimeExtra():R}");
            Check(new BossMod.ActionID(BossMod.ActionType.Spell, action).CastTimeExtra() == 0,
                $"Reviewed Hydatos zero-extra manifest changed at {action}.");
        }
    }

    private static MechanicActorSnapshot Snapshot(ReplayActorState a, MechanicStatusDto[]? statuses = null)
        => new(a.Id, a.Oid, (int)a.Id, 0, a.Name, 0, a.Kind, a.SubKind, 0, 100,
            a.Position, a.Rotation, a.HitboxRadius, a.Hp, a.MaxHp, 0, 0, 0,
            a.Targetable, false, a.Dead, a.InCombat, 0, a.TargetId, 0, 0, 0, 0, 0, 0, 0, 0,
            a.Cast is { } c ? new(c.ActionId, c.IsSpell == true, c.TargetId, c.Rotation ?? a.Rotation,
                c.Location, c.TotalTime ?? 3, false, 0) : null, statuses ?? []);

    private static void DispelFeed()
    {
        Initialize();
        using var session = new MechanicSession("hydatos-status-actual-feed");
        var feed = new MechanicWorldFeed(session);
        var model = new HydatosEncounter(HydatosCatalog.Definitions.Single(d => d.Name == "Tristitia"));
        ReplayActorState[] actors =
        [
            new() { Id = 1, Oid = 0x2740, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                Targetable = true, InCombat = true, Position = new(100, 0, 100) },
            new() { Id = 100, Kind = 1, Hp = 100, Position = new(100, 0, 100) },
            new() { Id = 2, Oid = 0x233C, Kind = 2, SubKind = 5, Hp = 100 },
            new() { Id = 3, Oid = 0x233C, Kind = 2, SubKind = 2, Hp = 100 },
        ];
        var raw = new Dictionary<ulong, MechanicStatusDto[]>
        {
            [2] = [new(59, 199, 1, 0xE0000000, 0), new(1, 1797, 0, 1, -5)],
            [3] = [new(59, 199, 1, 1, 10)],
        };
        var now = START;
        void Apply(string at)
        {
            session.BeginFrame(now);
            feed.ApplyFrame(new(827, 639, 100, 0, [new(0, 100, 100, "")], [],
                actors.Select(a => Snapshot(a, raw.GetValueOrDefault(a.Id))).ToArray()));
            session.Update();
            foreach (var id in raw.Keys)
            {
                var i = Array.FindIndex(actors, a => a.Id == id);
                actors[i] = actors[i] with { Statuses = raw[id].Select(s =>
                    new ReplayStatus(s.Id, s.Extra, s.SourceId, ReplayStatus.Expiration(now, s.RemainingSeconds))
                    { SlotIndex = s.Slot, ZeroRemainingTime = s.RemainingSeconds == 0 }).ToArray() };
            }
            model.Synchronize(actors, now, 100);
            var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x2740);
            foreach (var name in new[] { "ShockSpikes", "MightyStrikes" })
            {
                var component = module.Components.Single(c => c.GetType().Name == name);
                var expected = (List<BossMod.Actor>)typeof(BossMod.Components.Dispel)
                    .GetField("Targets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component)!;
                var actual = model.DispelTargets.Where(p => p.Rule.Component == name).Select(p => p.Actor.Actor.Id);
                Check(expected.Select(a => a.InstanceID).SequenceEqual(actual), $"{at}/{name} original target order/duplicates");
            }
        }
        Apply("cold slot59 includes Pet zero-clock");
        Check(model.DispelTargets.Count(p => p.Rule.Component == "ShockSpikes") == 2
            && model.Actors[2].Statuses[59]!.ExpiresAt == now, "Cold slots/Pet or zero remaining clock missing.");
        raw[2] = [new(59, 199, 1, 0, 1), new(1, 1797, 0, 1, 5)];
        Apply("at-most-one-second-no-refresh");
        raw[2] = [new(59, 199, 1, 0, 2.001f), new(1, 1797, 0, 1, 2)];
        Apply("strict-one-second-refresh-and-shorten");
        raw[2] = [new(59, 199, 2, 0, 2.001f)];
        raw[3] = [new(59, 199, 2, 1, 40)];
        Apply("extra-only-Gain pet-hot-suppressed");
        // Feed keeps the ActorType captured at creation even if the snapshot changes.
        actors[2] = actors[2] with { SubKind = 2 };
        raw[2] = [new(59, 199, 3, 0, 3)];
        Apply("same-generation-type-immutable");
        raw[2] = []; raw[3] = [];
        Apply("Lose-first-reference-pet-ignored");
        now = now.AddSeconds(100);
        Apply("no-expiration-delete");
    }

    private static void ColdDualFeed()
    {
        Initialize();
        foreach (var reverse in new[] { false, true })
        {
            using var session = new MechanicSession("hydatos-cold-double-actual-feed");
            var feed = new MechanicWorldFeed(session);
            var f = new Fixture("Tristitia");
            ReplayCast Spell(uint action) => new(action, 100, new(103, 0, 98), .4f, START, START.AddSeconds(3.3), false, false)
                { TotalTime = 3, IsSpell = true };
            f.Set(2, a => a with { Cast = Spell(15909) });
            f.Set(3, a => a with { Cast = Spell(15914) });
            if (reverse)
            {
                var i = f.Actors.FindIndex(a => a.Id == 2);
                var first = f.Actors[i]; f.Actors.RemoveAt(i); f.Actors.Add(first);
            }
            f.Model.Clear();
            session.BeginFrame(START);
            feed.ApplyFrame(new(827, 639, 100, 0, [new(0, 100, 100, "")], [], f.Actors.Select(a => Snapshot(a)).ToArray()));
            session.Update();
            f.Model.Synchronize(f.Actors, START, 100);
            var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x2740);
            var components = module.Components.Where(c => c.GetType().Name == "TornadoIIAerogaIVDualCast").ToArray();
            Check(components.Length == 1, "Source final duplicate activation did not replace component.");
            Compare(f, "TornadoIIAerogaIVDualCast", (BossMod.Components.GenericAOEs)components[0], $"actual cold reverse={reverse}");
            Check(f.Native("TornadoIIAerogaIVDualCast").Length == (reverse ? 1 : 2),
                "Cold actor order did not change the DualCast latch.");
        }
    }

    private static void ImmunityFeed()
    {
        Initialize();
        using var session = new MechanicSession("hydatos-immunity-actual-feed");
        var feed = new MechanicWorldFeed(session);
        var model = new HydatosEncounter(HydatosCatalog.Definitions.Single(d => d.Name == "Ovni"));
        ReplayActorState[] actors =
        [
            new() { Id = 1, Oid = 0x2685, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                Targetable = true, InCombat = true, Position = new(100, 0, 100) },
            new() { Id = 100, Kind = 1, Hp = 100, Position = new(100, 0, 100) },
            new() { Id = 101, Kind = 1, Hp = 100, Position = new(101, 0, 100) },
        ];
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101)];
        var raw = new Dictionary<ulong, MechanicStatusDto[]>
        {
            [100] = [new(0, 160, 0, 100, 10), new(59, 1209, 0, 100, 4)],
        };
        void Apply(string at)
        {
            session.BeginFrame(START);
            feed.ApplyFrame(new(827, 639, 100, 0, party.Select(p => new MechanicPartySlotDto(
                p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [],
                actors.Select(a => Snapshot(a, raw.GetValueOrDefault(a.Id))).ToArray()));
            session.Update();
            var values = actors.Select(a => a with { Statuses = raw.GetValueOrDefault(a.Id, []).Select(s =>
                new ReplayStatus(s.Id, s.Extra, s.SourceId, ReplayStatus.Expiration(START, s.RemainingSeconds))
                { SlotIndex = s.Slot, ZeroRemainingTime = s.RemainingSeconds == 0 }).ToArray() }).ToArray();
            model.Synchronize(values, START, 100, party);
            var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x2685);
            var source = module.Components.OfType<BossMod.Components.GenericKnockback>().Single();
            foreach (var slot in new[] { 0, 1 })
                foreach (var delay in new[] { 0d, 1d, 4d, 4.0001d, 5d, 10d, 20d })
                    Check(source.IsImmune(slot, START.AddSeconds(delay)) == model.ImmuneAt(slot, START.AddSeconds(delay)),
                        $"{at} immunity slot={slot} activation={delay:R}");
        }
        Apply("cold same-class last callback wins strict boundary");
        Check(!model.ImmuneAt(0, START.AddSeconds(4)) && model.ImmuneAt(0, START.AddSeconds(3)),
            "Cold role callbacks incorrectly used max expiry or >=.");
        raw[100] = [new(0, 160, 0, 100, 1), new(59, 1209, 0, 100, 2)];
        Apply("shortening does not refresh callback history");
        party = [new(0, "", 101), new(1, "", 100)];
        raw[100] = [new(0, 160, 1, 100, 10), new(59, 1209, 0, 100, 2)];
        Apply("status callback uses old Party before reorder");
        raw[100] = [new(0, 160, 1, 100, 10), new(58, 2663, 0, 100, 20)];
        Apply("role loss clears only role in new slot job independent");
        raw[100] = [new(57, 2345, 0, 100, 10)];
        Apply("job loss duty survives independent slot history");
        raw[100] = [];
        Apply("duty loss no expiry inference");
    }

    private static void RegionFeed()
    {
        Initialize();
        using var session = new MechanicSession("hydatos-region-actual-manager");
        var feed = new MechanicWorldFeed(session);
        var model = new HydatosRegion();
        var actors = HydatosCatalog.Definitions.Select((d, i) => new ReplayActorState
        {
            Id = (ulong)(i + 1), Oid = d.PrimaryOid, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
            Targetable = true, Position = new(100 + i, 0, 100),
        }).ToList();
        actors.Add(new() { Id = 100, Kind = 1, Hp = 100, Position = new(100, 0, 100) });
        ReplayPartyMember[] party = [new(0, "", 100)];
        void Apply(string at)
        {
            session.BeginFrame(START);
            feed.ApplyFrame(new(827, 639, 100, 0, [new(0, 100, 100, "")], [],
                actors.Select(a => Snapshot(a)).ToArray()));
            session.Update();
            model.Synchronize(actors.ToArray(), party, 100, START);
            var expected = session.BossModules.LoadedModules.Select(m => m.PrimaryActor.InstanceID).ToArray();
            Check(expected.SequenceEqual(model.Loaded.Select(m => m.Primary!.Actor.Id)),
                $"{at} Loaded order expected={string.Join(',', expected)} actual={string.Join(',', model.Loaded.Select(m => m.Primary!.Actor.Id))}");
            Check(session.BossModules.ActiveModule?.PrimaryActor.InstanceID == model.Selected?.Primary?.Actor.Id,
                $"{at} ActiveModule selection differs");
        }
        Apply("cold seven reverse Pending insertion");
        var index = actors.FindIndex(a => a.Oid == 0x2765);
        actors[index] = actors[index] with { InCombat = true };
        Apply("strictly higher active promotes Ceto");
        var ovni = actors.FindIndex(a => a.Oid == 0x2685);
        actors[ovni] = actors[ovni] with { InCombat = true };
        Apply("equal active keeps Ceto despite existing earlier Ovni");
        actors.Reverse();
        Apply("snapshot reorder preserves Loaded and selection");
        actors.Add(new() { Id = 50, Oid = 0x2765, Kind = 2, SubKind = 5, Hp = 100,
            Targetable = true, InCombat = true, Position = new(100, 0, 100) });
        Apply("same OID candidate remains Pending");
        index = actors.FindIndex(a => a.Id == 1);
        actors[index] = actors[index] with { Dead = true };
        Apply("completed old primary retires before pending vacancy next frame");
        Apply("same OID pending promotes next frame");
        ovni = actors.FindIndex(a => a.Oid == 0x2685);
        actors[ovni] = actors[ovni] with { InCombat = false };
        Apply("reset unload recreate goes to Loaded end");
    }

    private static void Navigation()
    {
        void CompareNav(Fixture f, BossMod.BossComponent source, ulong viewer, string at, int expectedGoals = 0)
        {
            var hints = new BossMod.AIHints();
            var slot = viewer == 100 ? 0 : 1;
            source.AddAIHints(slot, f.Actor(viewer), default, hints);
            var actual = f.Model.Navigation(f.Model.Actors[viewer].Actor).ToArray();
            Check(hints.GoalZones.Count == expectedGoals, $"{at} source goal count changed");
            var geometries = actual.Select(a => a.Shape.Resolve(a.Origin, a.Rotation, null)).ToArray();
            foreach (var (shape, activation, _) in hints.ForbiddenZones)
                Check(actual.Any(a => a.Activation == activation), $"{at} source activation {activation:O} missing");
            for (var x = 78.125f; x < 123; x += .75f)
                for (var z = 78.25f; z < 123; z += .75f)
                {
                    var pos = new BossMod.WPos(x, z);
                    if (hints.ForbiddenZones.Any(v => MathF.Abs(v.shapeDistance.Distance(pos)) < .001f)) continue;
                    var e = hints.ForbiddenZones.Any(v => v.shapeDistance.Distance(pos) < 0);
                    var a = geometries.Any(g => g.Contains(new(x, 0, z), 0));
                    Check(e == a, $"{at} forbidden mismatch ({x:R},{z:R}) source={e} native={a}");
                }
        }
        var d = new Fixture("Daphne");
        var stack = d.Component<BossMod.Components.GenericStackSpread>("Netherwater");
        d.Icon(62);
        CompareNav(d, stack, 100, "stack target buddy half-radius");
        CompareNav(d, stack, 101, "stack non-target enter");
        d.Icon(62, 101, 101);
        CompareNav(d, stack, 100, "two marked targets same component");
        d.Icon(62);
        CompareNav(d, stack, 101, "duplicate target overlap excludes non-exclusive area");
        Check(d.Model.Damage().Count() == 0, "Zero ForbiddenPlayers fabricated Shared damage.");
        var o = new Fixture("Ovni");
        var spread = o.Component<BossMod.Components.GenericStackSpread>("IonShower");
        o.Icon(111, 100, 100);
        CompareNav(o, spread, 100, "own Ion binary goal no forbidden", 1);
        CompareNav(o, spread, 101, "other Ion extra spread threshold");
        var b = new Fixture("ProvenanceWatcher");
        var bait = b.Component<BossMod.Components.GenericBaitAway>("TheScarletPrice");
        b.Cast(15001);
        CompareNav(b, bait, 100, "own bait avoids other party raw positions");
        CompareNav(b, bait, 101, "other bait target-centered R3");
        var sourceHints = new BossMod.AIHints();
        bait.AddAIHints(0, b.Actor(100), default, sourceHints);
        Check(sourceHints.PredictedDamage.Count == 1 && b.Model.Damage().Single().Activation == sourceHints.PredictedDamage[0].Activation
            && b.Model.Damage().Single().DamageType == sourceHints.PredictedDamage[0].Type.ToString(),
            "Bait prediction type/first activation differs.");
    }

    private static void BoundaryFeed()
    {
        Initialize();
        foreach (var definition in HydatosCatalog.Definitions)
        {
            using var session = new MechanicSession("hydatos-boundary-actual-module");
            var feed = new MechanicWorldFeed(session);
            var model = new HydatosEncounter(definition);
            var origin = new Vector3(100.25f, 0, 100.125f);
            ReplayActorState[] actors =
            [
                new() { Id = 1, Oid = definition.PrimaryOid, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                    Targetable = true, InCombat = true, Position = origin },
                new() { Id = 100, Kind = 1, Hp = 100, Position = origin },
            ];
            foreach (var offset in new[] { Vector3.Zero, new Vector3(4, 0, 4), new Vector3(6.01f, 0, 0), new Vector3(6.01f, 0, 5.2f) })
            {
                actors[1] = actors[1] with { Position = origin + offset };
                session.BeginFrame(START);
                feed.ApplyFrame(new(827, 639, 100, 0, [new(0, 100, 100, "")], [], actors.Select(a => Snapshot(a)).ToArray()));
                session.Update();
                model.Synchronize(actors, START, 100, [new(0, "", 100)]);
                var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == definition.PrimaryOid);
                var hints = new BossMod.AIHints();
                source.CalculateAIHints(0, session.World.Actors.Find(100)!, default, hints);
                var native = model.NavigationBoundary(0);
                Check(native.Position.X == hints.PathfindMapCenter.X && native.Position.Z == hints.PathfindMapCenter.Z,
                    $"{definition.Name}/{offset} actual pathfind center");
                for (var x = -35; x < 36; x += 2)
                    for (var z = -35; z < 36; z += 2)
                    {
                        var p = hints.PathfindMapCenter + new BossMod.WDir(x + .125f, z + .25f);
                        Check(hints.PathfindMapBounds.Contains(p - hints.PathfindMapCenter)
                            == native.Contains(new(p.X, 0, p.Z), 0), $"{definition.Name}/{offset} boundary point {p}");
                    }
            }
        }
    }

    private static void DamageClock()
    {
        var f = new Fixture("Ceto");
        var source = f.Component<BossMod.BossComponent>("SickleStrike");
        f.Set(2, a => a with { TargetId = 101 });
        f.Actor(2).TargetID = 101;
        f.Cast(15466, target: 2);
        void CompareDamage(string at)
        {
            var hints = new BossMod.AIHints();
            source.AddAIHints(0, f.Actor(100), default, hints);
            var expected = hints.PredictedDamage.Single();
            var actual = f.Model.Damage().Single();
            Check(actual.TargetId == (expected.Players[1] ? 101UL : 100UL)
                && actual.Activation == expected.Activation && actual.DamageType == expected.Type.ToString(),
                $"{at} exact target/clock/type");
        }
        CompareDamage("self-target fallback");
        f.Set(2, a => a with { TargetId = 100 });
        f.Actor(2).TargetID = 100;
        f.Sync();
        CompareDamage("same cast dynamic main target");
        f.Actor(2).CastInfo!.ElapsedTime = 3;
        f.Model.Actors[2].Elapsed = 3;
        f.Now = f.Now.AddSeconds(20);
        f.Sync();
        f.World.Frame.Timestamp = f.Now;
        CompareDamage("capped real NPC float remainder one DateTime conversion");
    }

    private static void InvalidTargetFeed()
    {
        Initialize();
        using var session = new MechanicSession("hydatos-invalid-target-feed");
        var feed = new MechanicWorldFeed(session);
        var model = new HydatosEncounter(HydatosCatalog.Definitions.Single(d => d.Name == "Ceto"));
        ReplayActorState[] actors =
        [
            new() { Id = 1, Oid = 0x2765, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                Targetable = true, InCombat = true, Position = new(100, 0, 100), TargetId = 0xE0000000,
                Cast = new(15468, 0xE0000000, new(100, 0, 100), 0, START, START.AddSeconds(3.3), false, false)
                { IsSpell = true, TotalTime = 3 } },
            new() { Id = 100, Kind = 1, Hp = 100, Position = new(100, 0, 100) },
        ];
        void Sync()
        {
            session.BeginFrame(START);
            feed.ApplyFrame(new(827, 639, 100, 0, [new(0, 100, 100, "")], [], actors.Select(a => Snapshot(a)).ToArray()));
            session.Update();
            model.Synchronize(actors, START, 100, [new(0, "", 100)]);
        }
        Sync();
        var original = model.Aoes.Single();
        var source = session.World.Actors.Find(1)!;
        var originalCast = source.CastInfo;
        Check(source.TargetID == 0 && source.CastInfo?.TargetID == 0
            && model.Actors[1].Actor.TargetId == 0 && model.Actors[1].Cast?.TargetId == 0,
            "invalid native target not normalized like Feed");
        actors[0] = actors[0] with { TargetId = 0, Cast = actors[0].Cast! with { TargetId = 0 } };
        Sync();
        Check(ReferenceEquals(originalCast, source.CastInfo) && ReferenceEquals(original, model.Aoes.Single()),
            "equivalent invalid-to-zero target synthesized Finish/Start");
    }

    private static void TargetPriorities()
    {
        Initialize();
        foreach (var definition in HydatosCatalog.Definitions)
        {
            using var session = new MechanicSession("hydatos-priority-actual-module");
            var feed = new MechanicWorldFeed(session);
            var model = new HydatosEncounter(definition);
            ReplayActorState[] actors =
            [
                new() { Id = 1, Oid = definition.PrimaryOid, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                    Targetable = true, InCombat = true, Position = new(100, 0, 100) },
                new() { Id = 100, Kind = 1, Hp = 100, Position = new(100, 0, 100) },
            ];
            session.BeginFrame(START);
            feed.ApplyFrame(new(827, 639, 100, 0, [new(0, 100, 100, "")], [], actors.Select(a => Snapshot(a)).ToArray()));
            session.Update();
            var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == definition.PrimaryOid);
            foreach (var oid in new uint[] { definition.PrimaryOid, 0x2767, 0x275E, 0x2740, 0x2741, 0x233C })
                foreach (var combat in new[] { false, true })
                    foreach (var initial in new[] { -3, -2, -1, 0 })
                    {
                        var actor = new BossMod.Actor(2, oid, 2, 0, "", 0, BossMod.ActorType.Enemy, default, 100,
                            new(100, 0, 100, 0), 1);
                        actor.InCombat = combat;
                        var enemy = new BossMod.AIHints.Enemy(actor, initial, false);
                        var hints = new BossMod.AIHints();
                        hints.PotentialTargets.Add(enemy);
                        module.CalculateAIHints(0, session.World.Actors.Find(100)!, default, hints);
                        var assigned = model.TargetPriority(new() { Oid = oid, InCombat = combat });
                        Check(enemy.Priority == (initial == -1 ? -1 : assigned ?? initial),
                            $"{definition.Name}/{oid:X}/{combat}/{initial} full priority setter");
                    }
        }
        var watcher = new HydatosEncounter(HydatosCatalog.Definitions.Single(d => d.Name == "ProvenanceWatcher"));
        ReplayActorState Candidate(ulong id, Vector3 position) => new()
        {
            Id = id, Oid = 0x233C, Kind = 2, SubKind = 5, Hp = 100, Targetable = true,
            IsEnemy = true, ObjectIndex = 2, InCombat = true, TargetId = 100, Position = position,
        };
        var near = Candidate(30, new(1, 10, 0));
        var far = Candidate(20, new(2, 0, 0));
        ulong Select(ReplayActorState[] candidates, ulong current = 0)
            => QuestTargetPriority.Select(827, candidates, current, default, new HashSet<ulong> { 100 }, null, watcher.TargetPriority);
        Check(Select([near, far]) == 30 && Select([near, far], 20) == 20,
            "Hydatos XZ nearest or original highest-priority target lost");
        Check(Select([far with { TargetId = 999, AggroPlayer = true }]) == 20,
            "Watcher discarded enmity-table candidate targeting outsiders");
        var shielded = far with { Statuses = [new(671, 0, 0, START.AddSeconds(10))] };
        Check(Select([shielded]) == 20
            && QuestTargetPriority.Select(476, [shielded], 0, default, new HashSet<ulong> { 100 }) == 0,
            "Watcher active-module status default or unrelated auto-hint filter changed");
        Check(Select([far with { Hp = 1 }]) == 0, "Watcher Pointless protection overwritten");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
