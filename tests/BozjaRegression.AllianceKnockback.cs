using System.Numerics;
using BossMod;
using BossMod.Components;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunAllianceKnockbackPilot(Action<string, Action> run)
    {
        foreach (var definition in AllianceKnockbackCatalog.Definitions)
        {
            run($"Alliance knockback source {definition.Key} manifest client exact arena lifecycle", () => AlliancePilotManifest(definition));
            foreach (var rule in definition.Rules)
                run($"Alliance knockback source {definition.Key} {rule.Component} hot cold duplicate Finish", () =>
                    AllianceStandard(definition, rule, rule.Actions.Single()));
            var knockback = definition.Rules.Single(r => r.Kind == BozjaRuleKind.AllianceKnockback);
            run($"Alliance knockback source {definition.Key} exact sequential endpoints wall hitbox no extra navigation", () =>
                AllianceKnockbackEndpoints(definition, knockback));
            run($"Alliance knockback source {definition.Key} immunity all sixty slots strict expiry cold and Lose", () =>
                AllianceKnockbackImmunity(definition, knockback));
            run($"Alliance knockback source {definition.Key} cold Pet immunity then hot Lose filtered", () =>
                AllianceKnockbackPet(definition, knockback));
            run($"Alliance knockback source {definition.Key} same ID foreign Actor Finish unrelated AID retains", () =>
                AllianceKnockbackFinishIdentity(definition, knockback));
            run($"Alliance knockback source {definition.Key} same-frame Party old slot and same-class status overwrite Lose", () =>
                AllianceKnockbackSlots(definition, knockback));
            foreach (var rule in definition.Rules.Where(r => r.Kind is BozjaRuleKind.Raidwide or BozjaRuleKind.Tankbuster))
            {
                run($"Alliance knockback source {definition.Key} {rule.Component} duplicate original raw NPC clock", () =>
                    AlliancePilotDamage(definition, rule));
                run($"Alliance knockback source {definition.Key} {rule.Component} multiple caster dynamic NPC clocks", () =>
                    AllianceKnockbackDamage(definition, rule));
            }
        }
    }

    private static void AllianceKnockbackEndpoints(BozjaDefinition definition, BozjaRule rule)
    {
        using var f = new CastrumFixture(alliance: definition);
        var origin = f.Center + new Vector3(.137f, 0, .271f);
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0], point: origin) });
        f.Actors.Add(Cll1Fixture.Actor(22, 0x233C, f.Center + Vector3.UnitZ * 10) with
        {
            Cast = f.Spell(rule.Actions[0], total: 1, point: f.Center - Vector3.UnitX * 5)
        });
        f.Frame(0);
        var source = (SimpleKnockbacks)f.Source!.Components.Single(c => c.GetType().Name == rule.Component);
        var casts = f.Native.Casts.Where(c => c.Rule == rule).ToArray();
        Check(casts.Length == 2 && casts[0].Activation > casts[1].Activation, "Simple knockback preserves insertion order without sorting");
        for (var i = 0; i < casts.Length; ++i)
            Check(casts[i].Origin == source.Casters[i].Origin.ToVec3()
                && casts[i].Activation == source.Casters[i].Activation
                && casts[i].Source.Actor.Id == source.Casters[i].ActorID,
                "Simple knockback locked source Loc clock and caster ID");
        f.Change(20, a => a with { Position = f.Center + Vector3.UnitX * 20 }); f.Frame(.1);
        Check(f.Native.Casts.First(c => c.Rule == rule).Origin == origin, "Simple knockback follows neither moving caster nor target");
        foreach (var hitbox in new[] { .5f, 1f, 3f })
            for (var i = 0; i < 64; ++i)
            {
                var angle = i * MathF.Tau / 64;
                f.Change(100, a => a with { HitboxRadius = hitbox,
                    Position = f.Center + new Vector3(MathF.Sin(angle) * 28, 0, MathF.Cos(angle) * 28) });
                f.Frame(.2);
                Check(!f.Native.NavigationForHost(f.NativeViewer).Any(z => z.Component == rule.Component),
                    "Simple knockback does not fabricate navigation absent in source");
            }
        f.Change(100, a => a with { Position = origin }); f.Frame(.3);
        Check(f.Native.AllianceSimpleMovements(f.NativeViewer, rule).Count() == (rule.KnockbackSide == 0 ? 1 : 2),
            "Simple knockback radial zero direction skips source while fixed side direction remains valid");
        f.Effect(.4, rule.Actions[0]);
        Check(source.NumCasts == 1 && f.Native.Casts.Count(c => c.Rule == rule) == 2,
            "Simple knockback Effect counts on source but does not consume either locked record");
        f.Frame(50);
        Check(f.Native.Casts.Count(c => c.Rule == rule) == 2, "Simple knockback no TTL");
        f.Change(20, a => a with { Cast = null }); f.Frame(51);
        Check(f.Native.Casts.Count(c => c.Rule == rule) == 1
            && f.Native.Casts.Single(c => c.Rule == rule).Source.Actor.Id == 22, "Simple knockback ordinary Finish removes caster ID first");
    }

    private static void AllianceKnockbackPet(BozjaDefinition definition, BozjaRule rule)
    {
        using var f = new CastrumFixture(false, alliance: definition);
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0]) });
        f.Change(100, a => a with { Kind = 2, SubKind = 2,
            Statuses = [new(1209, 0, 100, START.AddSeconds(100)) { SlotIndex = 59 }] });
        f.Frame(0);
        f.Change(1, a => a with { InCombat = true }); f.Frame(.1);
        Check(!f.Native.AllianceSimpleMovements(f.NativeViewer, rule).Any(), "Simple knockback cold Pet immunity admitted");
        f.Change(100, a => a with { Statuses = [] }); f.Frame(.2);
        Check(!f.Native.AllianceSimpleMovements(f.NativeViewer, rule).Any(), "Simple knockback hot Pet Lose remains filtered");
    }

    private static void AllianceKnockbackFinishIdentity(BozjaDefinition definition, BozjaRule rule)
    {
        using var f = new CastrumFixture(alliance: definition);
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0]) }); f.Frame(0);
        var original = f.Native.Casts.Single(c => c.Rule == rule);
        var caster = f.Session.World.Actors.Find(20)!;
        var clone = (Actor)typeof(object).GetMethod("MemberwiseClone",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(caster, null)!;
        var foreign = new BozjaActor(f.Native.Actors[20].Actor);
        Check(!ReferenceEquals(caster, clone) && !ReferenceEquals(f.Native.Actors[20], foreign), "Finish fixture really uses different actor objects");
        foreach (var component in f.Source!.Components)
            component.OnCastFinished(clone, new ActorCastInfo { Action = new(ActionType.Spell, 999) });
        f.Native.Finish(foreign, f.Spell(999)); f.Compare();
        Check(f.Native.Casts.Contains(original), "Unrelated Finish cannot consume same ID knockback");
        foreach (var component in f.Source.Components)
            component.OnCastFinished(clone, new ActorCastInfo { Action = new(ActionType.Spell, rule.Actions[0]) });
        f.Native.Finish(foreign, f.Spell(rule.Actions[0])); f.Compare();
        Check(!f.Native.Casts.Contains(original), "Watched Finish consumes original record by ID despite foreign object");
    }

    private static void AllianceKnockbackSlots(BozjaDefinition definition, BozjaRule rule)
    {
        using var f = new CastrumFixture(alliance: definition);
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0]) }); f.Frame(0);
        var expiry = START.AddSeconds(100);
        var status = new ReplayStatus(1209, 0, 100, expiry) { SlotIndex = 59 };
        f.Party[0] = f.Party[0] with { EntityId = 101 };
        f.Party[1] = f.Party[1] with { EntityId = 100 };
        f.Change(100, a => a with { Statuses = [status] }); f.Frame(.1);
        var source = (SimpleKnockbacks)f.Source!.Components.Single(c => c.GetType().Name == rule.Component);
        var activation = f.Native.Casts.Single(c => c.Rule == rule).Activation;
        Check(source.IsImmune(0, activation) && !source.IsImmune(1, activation)
            && f.Native.ImmuneAt(0, activation) && !f.Native.ImmuneAt(1, activation)
            && f.Native.AllianceSimpleMovements(f.NativeViewer, rule).Any(), "Gain binds to old Party slot before same-frame new Party submission");
        f.Change(100, a => a with { Statuses = [] }); f.Frame(.2);
        Check(source.IsImmune(0, activation) && f.Native.ImmuneAt(0, activation),
            "Lose on current slot does not rewrite historical old slot");
        f.Party[0] = f.Party[0] with { EntityId = 100 };
        f.Party[1] = f.Party[1] with { EntityId = 101 };
        f.Frame(.3);
        Check(!f.Native.AllianceSimpleMovements(f.NativeViewer, rule).Any(), "Restored old slot observes historical immunity");
        f.Change(100, a => a with { Statuses = [status] }); f.Frame(.4);
        f.Change(100, a => a with { Statuses = [status with { SlotIndex = 0 }] }); f.Frame(.5);
        Check(f.Viewer.Statuses[0].ID == 1209 && f.Viewer.Statuses[59].ID == 0
            && !source.IsImmune(0, activation) && f.Native.AllianceSimpleMovements(f.NativeViewer, rule).Any(),
            "Gain low then Lose high clears same class despite physically retained low-slot status");
    }

    private static void AllianceKnockbackDamage(BozjaDefinition definition, BozjaRule rule)
    {
        using var f = new CastrumFixture(alliance: definition);
        f.Party.Reverse();
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0], total: 10) }); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(22, 0x233C, f.Center) with { Cast = f.Spell(rule.Actions[0], .1, total: .02f, target: 101) });
        f.Frame(.1); f.Frame(2);
        var damage = f.Native.DamageForHost(f.NativeViewer);
        Check(damage.Length == 2 && damage[0].Activation < damage[1].Activation,
            "Distinct original sources retain dynamically normalized raw clocks beyond nominal finish");
        f.Change(20, a => a with { Cast = null }); f.Frame(3);
        Check(f.Native.DamageForHost(f.NativeViewer).Length == 1, "First original source Finish preserves second damage clock");
    }

    private static void AllianceKnockbackImmunity(BozjaDefinition definition, BozjaRule rule)
    {
        using var f = new CastrumFixture(false, alliance: definition);
        f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0]) });
        f.Change(100, a => a with { Statuses = [new(1209, 0, 100, START.AddSeconds(100)) { SlotIndex = 59 }] });
        f.Frame(0); f.Change(1, a => a with { InCombat = true }); f.Frame(.1);
        Check(!f.Native.AllianceSimpleMovements(f.NativeViewer, rule).Any(), "Simple knockback cold slot59 immune");
        var activation = f.Native.Casts.Single(c => c.Rule == rule).Activation;
        var time = .2d;
        foreach (var id in new uint[] { 3054, 160, 1209, 1722, 2663, 2345 })
            for (var slot = 0; slot < 60; ++slot)
            {
                f.Change(100, a => a with { Statuses = [] }); f.Frame(time += .001d);
                f.Change(100, a => a with { Statuses = [new(id, 0, 100, activation.AddSeconds(1)) { SlotIndex = slot }] });
                f.Frame(time += .001d);
                Check(!f.Native.AllianceSimpleMovements(f.NativeViewer, rule).Any(), "Simple knockback exact source immune class and physical slot");
                f.Change(100, a => a with { Statuses = [] }); f.Frame(time += .001d);
                Check(f.Native.AllianceSimpleMovements(f.NativeViewer, rule).Count() == 1, "Simple knockback real Lose restores line");
            }
        f.Change(100, a => a with { Statuses = [new(1209, 0, 100, activation) { SlotIndex = 59 }] });
        f.Frame(time += .001d);
        Console.WriteLine($"INFO Simple KB physical expiry input={activation:O} "
            + $"normalized={f.Native.Actors[100].Statuses[59]!.ExpiresAt:O}");
        // Physical status input round-trips a float remaining duration. Test
        // exact equality separately at the actual status callback boundary.
        var source = (SimpleKnockbacks)f.Source!.Components.Single(c => c.GetType().Name == rule.Component);
        var exact = new ActorStatus(1209, 0, activation, 100);
        source.OnStatusGain(f.Viewer, ref exact);
        typeof(BozjaEncounter).GetMethod("ImmuneStatus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(f.Native, [f.NativeViewer, new ReplayStatus(1209, 0, 100, activation), true]);
        f.Compare();
        Check(source.CalculateMovements(0, f.Viewer).Count == 1, "Source knockback expiry equal activation is not immune");
        Check(f.Native.AllianceSimpleMovements(f.NativeViewer, rule).Count() == 1, "Simple knockback expiry equal activation is not immune");
    }
}
