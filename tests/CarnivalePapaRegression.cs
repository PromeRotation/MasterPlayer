using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivalePapaRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Papa default lifecycles exact arenas primary only markers no icon history bootstrap", Lifecycle);
        run("Carnivale Papa ordinary R3 R4 actual Finish cast hints independent interrupt intent no invented casts", Casts);
        run("Carnivale Papa Thunderhead raw R8 hard obstacles dead untargetable state7 filtering actual destroy", Thunderhead);
        run("Carnivale Papa icon23 global overwrite processing five seconds no timeout Finish or target dependency", Icon);
        run("Carnivale Papa dynamic forward15 coincident plusZ current primary and viewer independent immunity", Movement);
        run("Carnivale Papa strict three category immunity expiry current live pool landing hazards no KB navigation", Immunity);
        run("Carnivale Papa dynamic raw raid64 prediction single target self fallback missing dead roster", Damage);
        run("Carnivale Papa occupied expired status slots perspective and no automatic dispel or restrictions", Hints);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal ulong Player = 100;
        internal Fixture(uint oid = 0x2C58, bool combat = true)
        {
            Actors = [new(1, oid, Center, InCombat: combat, Name: "Papa"), new(2, 0x233C, Center),
                new(100, 0, Center + Vector3.UnitZ * 2, PartySlot: 0, Player: true),
                new(101, 0, Center - Vector3.UnitZ * 2, PartySlot: 1, Player: true)];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.First(a => a.Id == Player).Position, Now, playerId: Player);
        internal void Cast(uint action, ulong target = 100)
        {
            Now = Now.AddSeconds(.01);
            Set(2, a => a with { Cast = new(action, Center, 0, Now, Now.AddSeconds(4), target) });
            Sync();
        }
        internal DungeonAmbientAoe[] Pools() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/thunderhead/")).ToArray();
        internal Vector3? Landing() => E.CarnivalePapaLanding(Actors.Single(a => a.Id == Player));
    }

    private static void Lifecycle()
    {
        foreach (var oid in new uint[] { 0x2C84, 0x2C58 })
        {
            var f = new Fixture(oid, false);
            Check(f.E.Active == null && f.E.CarnivalePrecombatHint != null, "Preparation.");
            f.E.Icon(100, 23, Start);
            f.Set(1, a => a with { InCombat = true });
            f.Sync();
            Check(f.E.Active?.BossOid == oid && f.E.CarnivaleDadJoke == null
                && f.E.Arena!.Contains(Center + Vector3.UnitX * 20) == (oid == 0x2C84)
                && f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == 1, "Lifecycle/arena/icon history.");
            f.Set(1, a => a with { InCombat = false });
            f.Sync();
            Check(f.E.Active != null, "Disengage reset.");
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active == null && f.E.CarnivaleDadJoke == null, "Completion.");
        }
    }

    private static void Casts()
    {
        foreach (var (oid, action, radius) in new[] { (0x2C84u, 18687u, 3f), (0x2C58u, 18602u, 4f) })
        {
            var f = new Fixture(oid);
            f.Cast(action);
            var cast = f.E.OutputCasts.Single().Value;
            f.Now = Start.AddSeconds(12);
            f.Sync();
            f.E.Effect(2, action, 1, f.Now);
            Check(f.E.OutputCasts.Single().Value == cast && cast.Rule.Shape is CircleTemplate c && c.Radius == radius, "Ordinary clock/shape.");
            f.E.Finish(2, action, f.Now);
            Check(!f.E.OutputCasts.Any(), "Ordinary Finish.");
        }
        var interrupt = new Fixture(0x2C84, false);
        interrupt.Cast(18688);
        interrupt.Now = Start.AddSeconds(10);
        interrupt.Set(1, a => a with { InCombat = true });
        interrupt.Sync();
        Check(interrupt.E.CarnivaleInterrupts.Single().Cast.Rule.Action == 18688
            && interrupt.E.Hints(Center).Contains("Interrupt!") && !interrupt.E.PredictedDamage.Any(), "Interrupt late bootstrap.");
        foreach (var (oid, action) in new[] { (0x2C84u, 18686u), (0x2C58u, 18604u) })
        {
            var f = new Fixture(oid);
            f.Cast(action);
            Check(f.E.Hints(Center).Contains("Prepare to dispel buff") && !f.E.PredictedDamage.Any()
                && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null), "CastHint scope.");
            f.E.Finish(2, action, f.Now);
            Check(!f.E.Hints(Center).Contains("Prepare to dispel buff"), "CastHint Finish.");
        }
    }

    private static void Thunderhead()
    {
        var f = new Fixture();
        var pos = Center + new Vector3(1.12345f, 0, 2.37654f);
        f.Actors.Add(new(4, 0x2C59, pos, Rotation: .7f, Dead: true, Targetable: false));
        f.Sync();
        Check(f.Pools().Single() is { Obstacle: true, Shape: CircleTemplate { Radius: 8 } } pool
            && pool.Position == pos && pool.Rotation == .7f
            && f.E.Hints(Center).Contains("GTFO from voidzone!") && CarnivaleCatalog.IgnoredCast(0x2C58, 18606), "Pool filters/raw geometry.");
        f.Set(4, a => a with { EventState = 7 });
        f.Sync();
        Check(!f.Pools().Any(), "State7 retained.");
        f.Set(4, a => a with { EventState = 0 });
        f.Sync();
        Check(f.Pools().Length == 1, "Restored pool missing.");
        f.Actors.RemoveAll(a => a.Id == 4);
        f.Sync();
        Check(!f.Pools().Any(), "Destroyed pool retained.");
    }

    private static void Icon()
    {
        var f = new Fixture();
        f.Now = Start.AddSeconds(10);
        f.Sync();
        f.E.Icon(999, 23, Start, 888);
        Check(f.E.CarnivaleDadJoke == null, "Unknown icon source accepted.");
        f.E.Icon(999, 23, Start, 2);
        var kb = f.E.CarnivaleDadJoke!;
        Check(kb.Source == 2 && kb.Target == 999 && kb.OccurredAt == Start && kb.ReceivedAt == f.Now
            && kb.Activation == f.Now.AddSeconds(5d), "Icon source/processing clock.");
        f.Now = Start.AddSeconds(11);
        f.Sync();
        f.E.Icon(101, 23, Start, 101);
        Check(f.E.CarnivaleDadJoke!.Generation != kb.Generation && f.E.CarnivaleDadJoke.Activation == f.Now.AddSeconds(5d),
            "Icon did not replace clock.");
        f.Now = Start.AddSeconds(100);
        f.Sync();
        f.E.Finish(1, 18605, f.Now);
        Check(f.Landing() != null, "Timeout/Finish cleared icon KB.");
        f.E.Effect(999, 18605, 1, f.Now);
        Check(f.E.CarnivaleDadJoke != null, "Unknown effect source cleared.");
        f.E.Effect(2, 18605, 1, f.Now);
        Check(f.E.CarnivaleDadJoke == null && f.Landing() == null, "NonPrimary effect did not clear.");
    }

    private static void Movement()
    {
        var f = new Fixture();
        f.Set(100, a => a with { Position = Center });
        f.Sync();
        f.E.Icon(101, 23, actorId: 100);
        Check(f.Landing() == Center + Vector3.UnitZ * 15, "Coincident DirForward not +Z.");
        f.Set(1, a => a with { Position = Center + Vector3.UnitX * 3, Rotation = 2 });
        f.Sync();
        Check(Vector3.Distance(f.Landing()!.Value, Center - Vector3.UnitX * 15) < .001f, "Primary origin did not update.");
        f.Player = 101;
        f.Sync();
        Check(f.Landing() != null && f.E.CarnivaleDadJoke!.Origin.Actor.Id == 1
            && f.E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/papa/kb/")).All(a => !a.Navigate), "Viewer/KB display navigation scope.");
        var original = f.E.CarnivaleDadJoke!;
        f.Actors.RemoveAll(a => a.Id is 1 or 100);
        f.E.Synchronize(f.Actors, null, f.Now, playerId: 100);
        f.Actors.Add(new(1, 0x2C58, Center + Vector3.UnitX * 3, InCombat: true));
        f.Actors.Add(new(100, 0, Center, PartySlot: 0, Player: true));
        f.Player = 100;
        f.Sync();
        Check(f.E.CarnivaleDadJoke!.Activation == original.Activation
            && !ReferenceEquals(f.E.CarnivaleDadJoke.Origin, original.Origin)
            && Vector3.Distance(f.Landing()!.Value, Center - Vector3.UnitX * 15) < .001f, "Primary rebound left stale origin.");
    }

    private static void Immunity()
    {
        var f = new Fixture();
        f.Set(100, a => a with { Position = Center });
        f.Sync();
        f.E.Icon(100, 23, actorId: 100);
        var activation = f.E.CarnivaleDadJoke!.Activation;
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: activation);
        Check(f.Landing() != null, "Equal expiry incorrectly immune.");
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: activation.AddTicks(1));
        Check(f.Landing() == null, "Strictly later expiry not immune.");
        f.E.StatusChanged(100, 1209, true, f.Now, expiresAt: Start);
        Check(f.Landing() != null, "Role category did not overwrite.");
        f.Actors.Add(new(4, 0x2C59, Center + Vector3.UnitZ * 15, Dead: true, Targetable: false));
        f.Sync();
        Check(f.E.Hints(Center).Contains("About to be knocked into danger!"), "Current dead-but-active pool danger.");
        f.Set(4, a => a with { EventState = 7 });
        f.Sync();
        Check(!f.E.Hints(Center).Contains("About to be knocked into danger!"), "Inactive pool retained danger.");
    }

    private static void Damage()
    {
        var f = new Fixture();
        f.Actors.Add(new(102, 0, Center, PartySlot: 63));
        f.Set(101, a => a with { Dead = true });
        f.Sync();
        f.Cast(18603);
        Check(f.E.PredictedDamage.Select(d => d.TargetId).Order().SequenceEqual(new ulong[] { 100, 102 })
            && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null), "Raidwide mask/scope.");
        f.Set(2, a => a with { Cast = a.Cast! with { FinishAt = Start.AddSeconds(30) } });
        f.Sync();
        Check(f.E.PredictedDamage.All(d => d.Activation == Start.AddSeconds(30)), "Raidwide dynamic clock.");
        f.E.Finish(2, 18603, f.Now);
        f.E.CurrentPartyMembers = [new(0, "DPS", 100), new(1, "DPS", 101), new(63, "DPS", 999)];
        f.Cast(18601, 2);
        f.Set(2, a => a with { TargetId = 999 });
        f.Sync();
        Check(f.E.PredictedDamage.Single().TargetId == 999 && f.E.PredictedDamage.Single().DamageType == "Tankbuster", "Self target fallback missing actor.");
        f.Set(2, a => a with { Cast = a.Cast! with { TargetId = 101 } });
        f.Sync();
        Check(f.E.PredictedDamage.Single().TargetId == 101, "SingleTarget dead roster filtered.");
    }

    private static void Hints()
    {
        foreach (var (oid, bossStatus, playerStatus, text) in new[] { (0x2C84u, 63u, 269u, "Windburn"), (0x2C58u, 1797u, 271u, "Electrocution") })
        {
            var f = new Fixture(oid);
            f.Set(1, a => a with { StatusSlots = [new(bossStatus, 0, 0, Start)] });
            f.Set(100, a => a with { StatusSlots = [new(playerStatus, 0, 0, Start)] });
            f.Now = Start.AddSeconds(20);
            f.Sync();
            Check(f.E.Hints(Center).Contains("Dispel Papa with Eerie Soundwave!")
                && f.E.Hints(Center).Any(h => h.StartsWith(text + " on you!")), "Occupied expired status hints.");
            f.Player = 101;
            f.Sync();
            Check(!f.E.Hints(Center).Any(h => h.StartsWith(text + " on you!")), "Status perspective leaked.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
