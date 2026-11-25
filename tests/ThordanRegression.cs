using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Scripting;
using MasterPlayer.MechanicHost;
using C = MasterPlayer.Mechanics.Dungeons.ThordanComponent;

internal static class ThordanRegression
{
    private static readonly DateTime START = new(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(448);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x1453, default, InCombat: true, TargetId: 100, Hp: 1000, MaxHp: 1000, NativeType: 0x205),
            new(2, 0x1463, new(0, 0, 10), NativeType: 0x20B),
            new(3, 0x1463, new(10, 0, 0), NativeType: 0x20B),
        ];
        internal DateTime Now = START;
        internal ThordanEncounter T => E.Thordan;
        internal Fixture()
        {
            for (var i = 0; i < 8; ++i)
                Actors.Add(new((ulong)(100 + i), 0, new(i * .1f, 0, 0), PartySlot: i,
                    PartyRole: i < 2 ? "Tank" : "Dps", Player: true, Hostile: false, NativeType: 0x104));
            E.CurrentPartyMembers = Enumerable.Range(0, 8).Select(i => new ReplayPartyMember(i, "", (ulong)(100 + i))).ToArray();
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        { var index = Actors.FindIndex(a => a.Id == id); Actors[index] = change(Actors[index]); }
        internal void Sync(ulong viewer = 100) => E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == viewer)?.Position, Now, playerId: viewer);
        internal void Step(double seconds) { Now = Now.AddSeconds(seconds); Sync(); }
        internal void Cast(uint action, ulong source = 2, ulong target = 0, float rotation = 0, double duration = 3, bool spell = true)
        {
            Set(source, a => a with { Cast = new(action, a.Position, rotation, Now, Now.AddSeconds(duration + .3d),
                target, TotalTime: (float)duration, IsSpell: spell) });
            Sync();
        }
        internal void End(ulong source = 2) { Set(source, a => a with { Cast = null }); Sync(); }
        internal void Component(C component) { T.ClearComponents(); T.Activate(component); }
    }

    internal static void Run(Action<string, Action> run)
    {
        run("Thordan native actual34 components real territory source shapes and precombat gates", Catalog);
        run("Thordan native timeline actual Effect raw Ability CastEnd phase priority cached knight delay terminal", Timeline);
        run("Thordan native ordinary spells locked Loc real Finish no effect expiry and source identity replacement", Ordinary);
        run("Thordan native Heavy four Finish axes angles cross source cancellation and hidden order", Heavy);
        run("Thordan native SpiralThrust prediction 10 12.1 first true Start clear and persistent real line", Thrust);
        run("Thordan native Sword Shield cold slots hot Pet exclusion target hints no forbidden actions", Swords);
        run("Thordan native Source Icon FIFO Skyward death Hiemal 3 seconds clear all", Icons);
        run("Thordan native two directed tether baits repeated shared clock original refs old Party chains", Tethers);
        run("Thordan native dynamic cleaves and independently locked spreads stacks exact8", Party);
        run("Thordan native Faith origin snapshot ignores immunity landing hard constraint Light seven FIFO", Displacements);
        run("Thordan native gaze global Finish FIFO joint PR direction Eye animation no navigation", Gaze);
        run("Thordan native Holy captured roles source Finish survives Effect stages dynamic target slot", Wild);
        run("Thordan native tower source fill conditional navigation first clock union damage and quantization edge", Towers);
        run("Thordan native timeline production rows clamped clock mismatch overdue missing component continue", TimelinePresentation);
    }

    private static void Catalog()
    {
        var f = new Fixture();
        Check(Enum.GetValues<C>().Length == 34 && f.E.IsThordan && f.E.Definitions[0].BossOid == 0x1453
            && f.E.Definitions[0].Casts.All(c => c.Shape == null), "Wrong actual component/catalog contract.");
        f.T.End();
        Check(!f.T.Active && !f.E.AmbientAoes().Any(a => a.Navigate), "Ended model leaked navigation.");
    }
    private static void Timeline()
    {
        var f = new Fixture();
        Check(f.T.Timeline.StateId == 0, "Initial timeline state.");
        f.T.Effect(1, 5246, f.Now); f.Sync();
        Check(f.T.Timeline.StateId == 0x10000, "Actual cleave did not advance immediately.");
        f.Step(50);
        Check(f.T.Timeline.StateId == 0x10000, "Estimated CastStart timeout advanced raw wait.");
        f.Cast(999, 1, spell: false);
        Check(f.T.Timeline.StateId == 0x10001 && f.T.Has(C.Meteorain), "Raw Ability ignored by timeline.");
        f.Step(10);
        Check(f.T.Timeline.StateId == 0x10001, "Estimated CastEnd advanced nonempty raw.");
        f.End(1);
        Check(f.T.Timeline.StateId == 0x10002, "Real raw empty failed.");
        f.Set(1, a => a with { Targetable = false }); f.Sync();
        Check(f.T.Timeline.Phase == 1 && f.T.ReducedArena && f.T.Timeline.StateId == 0x01000000, "Phase did not preempt current state.");
        f.Actors.Add(new(10, 0x1455, default, Dead: true));
        f.Actors.Add(new(11, 0x1456, default, Dead: true));
        f.Sync();
        Check(f.T.Timeline.Phase == 1 && f.T.Adelphel != null, "Knights cached before phase check.");
        f.Sync();
        Check(f.T.Timeline.Phase == 2 && f.T.Has(C.SpiralPierce), "Cached dead knights failed next-update transition.");
        f.Set(1, a => a with { Hp = 0 }); f.Sync();
        Check(f.T.Completed && !f.T.Active && !f.T.ActiveComponents.Any(), "Death phase failed cleanup.");
        f = new Fixture();
        Check(f.T.Timeline.Steps(2).Count(s => s.Id >= 0x020F0000) == 200, "Source 100 repeat enrage Cast pairs missing.");
    }
    private static void Ordinary()
    {
        var f = new Fixture(); f.Component(C.Meteorain);
        f.Cast(5252, rotation: .3f);
        var first = f.T.Aoes.Single();
        f.Set(2, a => a with { Position = new(20, 0, 20), Rotation = 2 }); f.Step(15);
        Check(f.T.Aoes.Single() == first, "Locked AoE moved/expired.");
        f.T.Effect(2, 5252, f.Now);
        Check(f.T.Aoes.Count == 1 && f.T.Count(C.Meteorain) == 1, "Effect removed raw AoE.");
        f.Cast(999, spell: false);
        Check(f.T.Aoes.Count == 0, "Spell-to-Ability failed old Finish.");
        f.Cast(5252);
        var old = f.T.Aoes.Single().Source;
        f.Set(2, a => a with { GameObjectId = 999 }); f.Sync();
        f.Set(2, a => a with { GameObjectId = 1000 }); f.Sync();
        Check(old.Destroyed && !ReferenceEquals(old, f.T.Actors[2]) && f.T.Aoes.Count == 1, "Actor rebuild merged generations.");
    }
    private static void Heavy()
    {
        var f = new Fixture(); f.Component(C.HeavyImpact);
        f.Cast(5280, rotation: .4f);
        var sequence = f.T.Heavy.Single();
        f.End();
        Check(sequence.Order == 1 && sequence.Activation == f.Now.AddSeconds(2d) && f.T.Count(C.HeavyImpact) == 1,
            "Cancel Finish failed to advance first shape.");
        f.Cast(5282, 3, rotation: .4f); f.End(3);
        Check(sequence.Order == 1 && f.T.Count(C.HeavyImpact) == 2, "Unmatched Finish lost count or advanced wrong order.");
        f.Set(3, a => a with { Position = sequence.Origin + new Vector3(1, 0, -1) });
        for (var order = 1; order < 4; ++order)
        {
            f.Cast((uint)(5280 + order), 3, rotation: .4f); f.Step(.1); f.End(3);
        }
        Check(f.T.Heavy.Count == 0 && f.T.Count(C.HeavyImpact) == 5, "Four ring progression/cross-source matching.");
    }
    private static void Thrust()
    {
        var f = new Fixture();
        f.Set(2, a => a with { Oid = 0x1457, Position = new(30, 0, 0) }); f.Sync();
        foreach (var (component, delay) in new[] { (C.SpiralThrust1, 10d), (C.SpiralThrust2, (double)12.1f) })
        {
            f.Component(component); f.T.Effect(2, 4120, f.Now);
            Check(f.T.Aoes.Single() is { Risky: false } prediction && prediction.Activation == f.Now.AddSeconds(delay),
                "Prediction delay/risk changed.");
            f.Cast(5286, rotation: .7f);
            Check(f.T.Aoes.Count == 1 && f.T.Aoes[0].Risky && f.T.Aoes[0].Origin == new Vector3(30, 0, 0), "First Start did not replace prediction.");
            f.End(); f.Step(20);
            Check(f.T.Aoes.Count == 1, "Finish/time incorrectly cleared line.");
        }
    }
    private static void Swords()
    {
        var f = new Fixture();
        f.Actors.Add(new(10, 0x1455, default, TargetId: 101, StatusSlots: [new(944, 0, 0, f.Now.AddSeconds(50)) { SlotIndex = 4 }]));
        f.Sync(); f.Component(C.SwordShieldOfTheHeavens);
        Check(f.T.Swords.Single().Buff == 944, "Cold activation lost real status slot.");
        f.Set(10, a => a with { StatusSlots = [new(945, 0, 0, f.Now.AddSeconds(50)) { SlotIndex = 7 }] }); f.Sync();
        f.Set(100, a => a with { TargetId = 10 }); f.Sync();
        Check(f.T.Swords.Single().Buff == 945 && f.T.Hints().Contains("Swap target!") && f.E.ForbiddenTargets.Length == 0,
            "Shield widened to automatic forbidden target or hint absent.");
        f.Set(10, a => a with { Dead = true }); f.Sync();
        Check(f.T.ComponentActive(C.SwordShieldOfTheHeavens), "Source Active must not filter dead.");
        f.Set(10, a => a with { NativeType = 0x202, StatusSlots = [new(944, 0, 0, f.Now.AddSeconds(50)) { SlotIndex = 7 }] }); f.Sync();
        Check(f.T.Swords.Single().Buff == 945, "Hot Pet status entered component.");
        f.T.Activate(C.SwordShieldOfTheHeavens);
        Check(f.T.Swords.Count == 0, "Pet should not receive Created during cold activation.");
        f.Actors.Add(new(11, 0x1456, default, NativeType: 0x202));
        f.Sync();
        Check(f.T.Swords.Count == 0, "Hot Pet Created entered component.");
    }
    private static void Icons()
    {
        var f = new Fixture(); f.Component(C.SkywardLeap);
        f.T.Icon(100, 14, f.Now); f.T.Icon(101, 14, f.Now.AddSeconds(.1));
        f.Set(100, a => a with { Dead = true }); f.Sync();
        Check(f.T.Baits.Count == 2 && f.E.AmbientAoes().Any(a => a.Id.StartsWith("thordan/bait/")), "Skyward target death deleted bait.");
        f.T.Effect(3, 5289, f.Now);
        Check(f.T.Baits.Single().Target.Actor.Id == 101, "Skyward cross-source FIFO.");
        f.Component(C.HiemalStormSpread); f.T.Icon(100, 29, f.Now);
        f.T.Icon(101, 29, f.Now.AddSeconds(.1));
        Check(f.E.PredictedDamage.Single() is { TargetId: 101, DamageType: "Raidwide" } prediction
            && prediction.Activation == f.Now.AddSeconds(3.1d), "Active Hiemal spread prediction lost.");
        f.T.Effect(3, 5295, f.Now);
        f.T.Icon(100, 29, f.Now);
        var bait = f.T.Baits.Single();
        Check(bait.Activation == f.Now.AddSeconds(3d), "Hiemal consuming clock.");
        f.Step(20);
        Check(f.T.Baits.Count == 1 && !f.E.AmbientAoes().Any(a => a.Id.StartsWith("thordan/spread/")), "Dead Hiemal hidden/storage or timeout.");
        f.T.Effect(3, 5295, f.Now); Check(f.T.Baits.Count == 0, "Hiemal effect did not clear all.");
    }
    private static void Tethers()
    {
        var f = new Fixture(); f.Component(C.SpiralPierce);
        f.Set(2, a => a with { FirstTether = new(5, 100) { SlotIndex = 0 } });
        f.Set(100, a => a with { FirstTether = new(5, 2) { SlotIndex = 0 } }); f.Sync();
        Check(f.T.Baits.Count == 2 && f.T.Baits.All(b => b.Activation == f.Now), "Bidirectional duplicate pair lost.");
        Check(f.E.ThordanLines().Count() == 2, "SpiralPierce source lines missing without BurningChains.");
        f.Set(2, a => a with { FirstTether = new(5, 100, 3) { SlotIndex = 0 } }); f.Sync();
        Check(f.T.Baits.Count == 2, "Progress-only update recreated pair.");
        f.Set(2, a => a with { FirstTether = null }); f.Sync();
        Check(f.T.Baits.Count == 1, "Untether must remove only first pair.");
        f.T.Effect(3, 5287, f.Now); Check(f.T.Baits.Count == 1, "Effect removed live tether.");
        f.Component(C.BurningChains);
        f.Set(100, a => a with { FirstTether = new(9, 101) }); f.Sync();
        var partner = f.T.ChainPartners[0];
        f.E.CurrentPartyMembers = f.E.CurrentPartyMembers.Select(p => p with { Slot = p.Slot == 0 ? 1 : p.Slot == 1 ? 0 : p.Slot }).ToArray();
        f.Sync();
        Check(ReferenceEquals(f.T.ChainPartners[0], partner) && f.T.Slot == 1, "Chain recaptured on roster reassignment.");
    }
    private static void Party()
    {
        var f = new Fixture(); f.Component(C.AscalonsMight);
        Check(f.E.AmbientAoes().Any(a => a.Id.Contains("/bait/")) && f.T.Cleaves(C.AscalonsMight).Count() == 1, "Cleave target navigation absent.");
        f.Component(C.DragonsRage); f.Cast(5259, target: 100);
        Check(f.T.Hints().Contains("Stack (8/8)"), "Source exact8 stack contract.");
        var original = f.T.Baits[0].Target;
        f.Actors.RemoveAll(a => a.Id == 100); f.Sync(101);
        f.Actors.Add(new(100, 0, new(15, 0, 0), Player: true)); f.Sync();
        Check(!ReferenceEquals(original, f.T.Actors[100]) && ReferenceEquals(f.T.Baits[0].Target, original), "Same-ID target rebound.");
        f.End(); f.Component(C.LightningStorm); f.Cast(5250, target: 101);
        Check(f.E.AmbientAoes().Any(a => a.Shape is CircleTemplate { Radius: 5 } && a.OutlineOnly)
            && f.E.AmbientAoes().Any(a => a.Shape is CircleTemplate { Radius: 6 } && !a.Display),
            "Spread display/navigation radii lost.");
    }
    private static void Displacements()
    {
        var f = new Fixture();
        f.Actors.Add(new(10, 0x145A, new(4, 0, 0))); f.Sync();
        f.Component(C.FaithUnmoving);
        var movement = f.T.Faith!;
        f.Set(10, a => a with { Position = new(20, 0, 0) }); f.Sync();
        Check(movement.Origin == new Vector3(4, 0, 0) && movement.Distance == 16 && movement.Activation == default
            && f.T.FaithNavigationAt == f.Now.AddSeconds(4.5d), "Faith model/independent navigation clock.");
        var hard = f.E.AmbientAoes().Single(a => a.Id == "thordan/faith/navigation").Shape.Resolve(default, 0, null);
        for (var x = -20; x <= 20; ++x)
            for (var z = -20; z <= 20; ++z)
            {
                var p = new Vector3(x, 0, z);
                var landing = movement.Landing(p);
                Check(hard.Contains(p, 0) == (landing.X * landing.X + landing.Z * landing.Z > 324), "Faith actual hard adapter.");
            }
        f.Component(C.LightOfAscalon); f.T.Effect(1, 4187, f.Now);
        Check(f.T.Light.Count == 7 && f.T.Light[6].Activation == f.Now.AddSeconds(10.7d + 6 * 1.3d), "Seven exact clocks.");
        f.T.Effect(3, 5263, f.Now); Check(f.T.Light.Count == 6, "Light FIFO.");
        Check(!f.E.AmbientAoes().Any(a => a.Navigate), "Source weak goal expanded to hard navigation.");
    }
    private static void Gaze()
    {
        var f = new Fixture(); f.Component(C.DragonsGaze);
        f.Actors.Add(new(10, 0x1E9E28, default)); f.Sync(); f.T.Animation(10, 0x00040008);
        Check(f.T.EyePositionHint.LengthSquared() > 360 && !f.E.AmbientAoes().Any(a => a.Navigate), "Eye animation/navigation.");
        f.Cast(5257); f.Cast(5258, 3); f.Step(3);
        Check(f.T.EyePositionHint == default && f.E.AmbientAoes().Count(a => a.Id.Contains("thordan/gaze/")) == 6, "Eyes/arcs/start clears hint.");
        var heading = f.E.GazeFacing(default, 0);
        Check(heading.HasValue && f.T.Aoes.All(a => !ThordanEncounter.EyeDanger(a.Origin, f.T.Actors[100].Actor with { Rotation = heading.Value })),
            "Joint PR gaze direction.");
        f.End(3);
        Check(f.T.Aoes.Single().Source.Actor.Id == 3, "Gaze Finish must pop global FIFO, not source.");
        f.End(); Check(f.E.GazeFacing(default, 0) == null, "Finish leaked facing.");
    }
    private static void Wild()
    {
        var f = new Fixture(); f.Component(C.HolyShieldBash);
        f.Cast(5271, target: 102);
        Check(f.T.WildRoles[0] == ThordanWildRole.Avoid && f.T.WildRoles[1] == ThordanWildRole.Share
            && f.T.WildRoles[2] == ThordanWildRole.Target && f.T.WildRoles[3] == ThordanWildRole.ShareNotFirst, "Captured old Party roles.");
        f.Cast(5266, 3); f.End(3);
        Check(f.T.WildChargeSource != null, "Finish cleared charge Source.");
        f.T.Effect(2, 5271, f.Now); f.T.Effect(2, 5271, f.Now);
        Check(f.T.Count(C.HolyShieldBash) == 1, "Repeated shield hit advanced beyond first stage.");
        f.T.Effect(2, 5266, f.Now);
        Check(f.T.Count(C.HolyShieldBash) == 2 && f.T.WildChargeSource == null, "Spear effect failed to end.");
    }
    private static void Towers()
    {
        var f = new Fixture(); f.Component(C.Conviction);
        f.Set(2, a => a with { Position = new(10, 0, 0) });
        f.Cast(5277);
        f.Set(3, a => a with { Position = new(10, 0, 0) });
        f.Step(.1); f.Cast(5277, 3);
        f.Set(101, a => a with { Position = new(10, 0, 0) }); f.Sync();
        Check(f.E.AmbientAoes().Where(a => a.Display && a.Id.StartsWith("thordan/tower/"))
            .All(a => !a.OutlineOnly && !a.Safe), "Occupied tower must be dangerous fill to outsiders.");
        var damage = f.E.PredictedDamage.ToArray();
        Check(damage.Length == 1 && damage[0].TargetId == 101 && damage[0].DamageType == "Raidwide"
            && damage[0].Activation == f.T.Aoes[0].Activation, "Tower mask union/first clock/default damage lost.");
        f.Set(3, a => a with { Cast = null, Position = new(-10, 0, 0) }); f.Sync(); f.Cast(5277, 3);
        Check(f.E.AmbientAoes().Any(a => a.Id == "thordan/tower/join")
            && !f.E.AmbientAoes().Any(a => a.Id.EndsWith("/avoid")), "Join branch incorrectly retained forbidden tower branch.");
        const float grid = 2000f / 65535f;
        Check(ThordanEncounter.Quantized(new(25.101091384887695f, 0, 0)).X == (823 - .5f) * grid,
            "Quantization divided instead of multiplying the source float inverse.");
    }
    private static void TimelinePresentation()
    {
        var f = new Fixture();
        var initial = f.T.Timeline.Capture(f.Now)!;
        var rows = MechanicTimelineView.BuildRows(initial);
        Check(initial.PhaseName == "Phase 1" && rows[0] is { Name: "Cleave", IsCurrent: true, Tone: MechanicTimelineTone.Tankbuster }
            && rows[0].Seconds == 6.1f && rows.Count == 4, "Native timeline UI metadata/current/future lost.");
        f.T.Effect(1, 5246, f.Now); f.Sync();
        f.Step(50);
        var overdue = f.T.Timeline.Capture(f.Now)!;
        Check(Math.Abs((overdue.Entries[0].ExpectedEnd - f.Now).TotalSeconds) < .00001
            && (overdue.Entries[1].ExpectedEnd - f.Now).TotalSeconds == 2.7,
            "Overdue current state did not preserve source clamped prediction base.");
        f.Cast(999, 1, spell: false);
        Check(f.T.LastTimelineDiagnostic?.Contains("expected 5251") == true && f.T.Timeline.StateId == 0x10001,
            "Unexpected raw Ability must diagnose then continue.");
        f.End(1); f.Step(1.31);
        Check(f.T.LastTimelineDiagnostic?.Contains("because of overdue") == true
            && f.T.Timeline.StateId == 0x20000, "Condition overdue diagnostic/transition missing.");
        f = new Fixture();
        f.T.Deactivate(C.AscalonsMight); f.Sync();
        Check(f.T.LastTimelineDiagnostic?.Contains("component AscalonsMight") == true
            && f.T.Timeline.StateId == 0x10000, "Missing component must diagnose then continue.");
        Check(f.T.Timeline.Steps(0).Single(s => s.Id == 0xA0001).Hints.HasFlag(ScriptTimelineHint.Tankbuster),
            "Cast end lost tankbuster timeline hint.");
        f.T.End();
        Check(f.T.Timeline.Capture(f.Now) == null, "Stopped owner retained native timeline.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
