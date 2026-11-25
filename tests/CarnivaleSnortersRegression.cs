using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleSnortersRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Snorters default primary lifecycle source markers priority no invented raidwide", Lifecycle);
        run("Carnivale Snorters bomb model events not create duplicate references shared processing clock", Model);
        run("Carnivale Snorters Explosion first reference removal destroyed old generation frozen", Identity);
        run("Carnivale Snorters eight bomb create threshold special first explosive shared Fungah state", Fungah);
        run("Carnivale Snorters Snort locked player origin independent dynamic bomb projection cancel equality boundary", Snort);
        run("Carnivale Snorters player Fungah shape wall immunity danger checks only current explosions", Landing);
        run("Carnivale Snorters explosive first text latest create countdown dead targetable precedence", Hints);
        run("Carnivale Snorters ordinary R8 source lock real Finish late bootstrap canceled raw barrier", Ordinary);
        run("Carnivale Snorters raw immunity callbacks use previous roster while activation uses current", Roster);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors = [new(1, 0x2CF8, Center, InCombat: true, Name: "Typhon"),
            new(2, 0x233C, Center), new(100, 0, Center + Vector3.UnitZ * 5, PartySlot: 0, Player: true)];
        internal DateTime Now = Start;
        internal Fixture(bool combat = true)
        {
            Set(1, a => a with { InCombat = combat });
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == 100).Position, Now, playerId: 100);
        internal void Bomb(ulong id, Vector3? position = null, byte animation = 0)
        {
            Actors.Add(new(id, 0x2CF9, position ?? Center + Vector3.UnitX * 15, AnimationState1: animation));
            Sync();
        }
        internal void Cast(uint action)
        {
            Now = Now.AddSeconds(.01);
            Set(2, a => a with { Cast = new(action, Center, 0, Now, Now.AddSeconds(4), 100) });
            Sync();
        }
        internal DungeonAmbientAoe[] Bombs() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/snorter/bomb/")).ToArray();
    }

    private static void Lifecycle()
    {
        var f = new Fixture(false);
        Check(f.E.CarnivalePrecombatHint!.Contains("35s"), "Preparation.");
        f.Set(1, a => a with { InCombat = true });
        f.Actors.Add(new(3, 0x2CF9, Center, IsEnemy: true, ObjectIndex: 4, Hp: 100));
        f.Actors.Add(new(4, 0x2CEC, Center, IsEnemy: true, ObjectIndex: 6, Hp: 100));
        f.Actors.Add(new(5, 0x9999, Center, IsEnemy: true, ObjectIndex: 8, Hp: 100));
        f.Sync();
        Check(f.E.Active?.BossOid == 0x2CF8 && f.E.Arena!.Contains(Center + Vector3.UnitX * 20)
            && f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == 3
            && f.E.PriorityTarget(3, Center) == 4 && !f.E.PredictedDamage.Any(), "Default lifecycle/markers/priority.");
        f.Set(4, a => a with { Hp = 1 });
        f.Sync();
        Check(f.E.PriorityTarget(0, Center) == 0, "Pointless ME resurrected/other candidates accepted.");
        f.Set(1, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active == null, "Adds kept Primary alive.");
    }

    private static void Model()
    {
        var f = new Fixture();
        var liveCreate = new Fixture();
        liveCreate.Bomb(3, animation: 1);
        Check(liveCreate.Bombs().Length == 1, "Live Create must be followed by nondefault ModelState.");
        var precombat = new Fixture(false);
        precombat.Bomb(3, animation: 1);
        precombat.Set(1, a => a with { InCombat = true });
        precombat.Sync();
        Check(precombat.Bombs().Length == 0, "Activation must not replay historical ModelState.");
        f.Bomb(3);
        Check(!f.Bombs().Any(), "Create replayed model change.");
        f.Now = Start.AddSeconds(2);
        f.Set(3, a => a with { ModelState = 1, AnimationState1 = 1 });
        f.Sync();
        var original = f.E.CarnivaleSnorter!.Bombs.Single().Actor;
        Check(f.Bombs().Single().Activation == f.Now.AddSeconds(6d), "Model trigger clock.");
        f.Now = Start.AddSeconds(3);
        f.Set(3, a => a with { AnimationState2 = 1 });
        f.Sync();
        Check(f.E.CarnivaleSnorter!.Bombs.Count == 2 && f.Bombs().All(a => a.Activation == f.Now.AddSeconds(6d))
            && f.E.CarnivaleSnorter.Bombs.All(b => ReferenceEquals(b.Actor, original)), "Duplicate reference/shared deadline.");
        f.Sync();
        Check(f.Bombs().Length == 2, "Unchanged model generated event.");
        f.E.Effect(3, 19259, 1, f.Now);
        Check(f.Bombs().Length == 1, "Explosion did not remove first reference.");
    }

    private static void Identity()
    {
        var f = new Fixture();
        f.Bomb(3);
        f.Set(3, a => a with { AnimationState1 = 1 });
        f.Sync();
        var old = f.E.CarnivaleSnorter!.Bombs.Single().Actor;
        f.Set(3, a => a with { Dead = true, Targetable = false });
        f.Sync();
        Check(f.Bombs().Length == 1, "Dead bomb removed.");
        f.Actors.RemoveAll(a => a.Id == 3);
        f.Sync();
        Check(old.Removed && f.Bombs().Length == 1, "Destroy removed frozen reference.");
        f.Bomb(3, Center - Vector3.UnitX * 8);
        f.E.Effect(3, 19259, 2, f.Now);
        Check(f.Bombs().Single().Position == old.Actor.Position, "New same-ID bomb consumed old identity.");
    }

    private static void Fungah()
    {
        var f = new Fixture();
        f.Actors.Add(new(4, 0x2CEC, new(96, 0, 94)));
        f.Sync();
        for (ulong id = 10; id < 17; ++id) f.Bomb(id);
        Check(f.E.CarnivaleSnorter!.FungahActivation == default, "Seven bombs activated.");
        f.Bomb(17);
        var state = f.E.CarnivaleSnorter!;
        Check(state.FungahActivation == f.Now.AddSeconds(5.3d) && state.OtherPatterns, "Eighth/special first explosive.");
        f.Bomb(18);
        Check(state.FungahActivation == f.Now.AddSeconds(5.3d), "Ninth changed state.");
        f.E.Effect(2, 19259, 1, f.Now);
        Check(state.FungahActivation == default && state.OtherPatterns, "Explosion incorrectly cleared both slots.");
        f.E.Effect(2, 19256, 2, f.Now);
        Check(!state.OtherPatterns, "Fungah effect did not clear pattern.");
        var plain = new Fixture();
        for (ulong id = 10; id < 18; ++id) plain.Bomb(id);
        Check(plain.E.CarnivaleSnorter!.FungahActivation == plain.Now.AddSeconds(5d)
            && !plain.E.CarnivaleSnorter.OtherPatterns, "Plain eight-bomb clock.");
    }

    private static void Snort()
    {
        var f = new Fixture();
        f.Bomb(3);
        f.Set(3, a => a with { AnimationState1 = 1 });
        f.Sync();
        f.Cast(19266);
        var state = f.E.CarnivaleSnorter!;
        var knockback = f.E.CarnivaleCastKnockbacks.Single();
        Check(knockback is { Distance: 15, StopAtWall: true, Forward: false, EffectShape: null }
            && MathF.Abs(f.Bombs().Single().Position.X - 124.1f) < .001f
            && state.SnortEffectEnds == knockback.Cast.FinishAt.AddSeconds(2.5d), "Snort type/projection fixed .4 margin.");
        f.Set(1, a => a with { Position = Center + Vector3.UnitZ * 5 });
        f.Sync();
        Check(f.Bombs().Single().Position.Z < 100 && f.E.CarnivaleCastKnockbacks.Single().Cast.Location == Center,
            "Dynamic bomb origin or locked player origin.");
        f.E.Finish(2, 19266, f.Now);
        Check(!f.E.CarnivaleCastKnockbacks.Any() && state.SnortEffectEnds != default, "Cancel erased projection window.");
        f.Now = state.SnortEffectEnds;
        f.Sync();
        Check(!f.Bombs().Any(), "Exact window end must be empty.");
        f.Now = f.Now.AddTicks(1);
        f.Sync();
        Check(f.Bombs().Single().Position == Center + Vector3.UnitX * 15, "After window end did not restore raw bomb.");
    }

    private static void Landing()
    {
        var f = new Fixture();
        for (ulong id = 10; id < 18; ++id) f.Bomb(id, Center + Vector3.UnitZ * 20);
        var player = f.Actors.Single(a => a.Id == 100);
        Check(f.E.CarnivaleFungahLanding(player) is { } to && to.Z == 120, "Fungah within R12.5/wall.");
        f.Set(10, a => a with { AnimationState1 = 1 });
        f.Sync();
        Check(f.E.Hints(player.Position).Contains("About to be knocked into danger!"), "Fungah destination bombs.");
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: f.E.CarnivaleSnorter!.FungahActivation);
        Check(f.E.CarnivaleFungahLanding(player) != null, "Equal expiry immune.");
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: f.E.CarnivaleSnorter.FungahActivation.AddTicks(1));
        Check(f.E.CarnivaleFungahLanding(player) == null && f.Bombs().Length == 1, "Immunity removed bomb danger.");
        Check(f.E.CarnivaleFungahLanding(player with { Position = Center + Vector3.UnitZ * 13, PartySlot = -1, Id = 999 }) == null,
            "Outside cone radius moved.");
    }

    private static void Hints()
    {
        var f = new Fixture();
        f.Now = Start.AddSeconds(5);
        f.Actors.Add(new(4, 0x2CEC, Center, Name: "First"));
        f.Sync();
        f.Now = Start.AddSeconds(10);
        f.Actors.Add(new(5, 0x2CEC, Center, Name: "Second"));
        f.Sync();
        Check(f.E.Hints(Center).Contains("A First spawned, destroy it asap.")
            && f.E.Hints(Center).Any(h => h.Contains("35.0s")), "First text/latest create clock.");
        f.Set(4, a => a with { Dead = true });
        f.Sync();
        Check(!f.E.Hints(Center).Any(h => h.StartsWith("A "))
            && f.E.Hints(Center).Any(h => h.StartsWith("Explosion in ca.")), "Dead first fallback/countdown wrong.");
    }

    private static void Ordinary()
    {
        var f = new Fixture();
        f.Cast(19258);
        var cast = f.E.OutputCasts.Single().Value;
        f.Now = Start.AddSeconds(20);
        f.Sync();
        f.E.Effect(2, 19258, 1, f.Now);
        Check(f.E.OutputCasts.Single().Value == cast && cast.Rule.Shape is CircleTemplate { Radius: 8 }, "Fireball clock.");
        f.E.Finish(2, 19258, f.Now);
        Check(!f.E.OutputCasts.Any(), "Fireball Finish.");
        var late = new Fixture(false);
        late.Cast(19266);
        late.E.Finish(2, 19266, late.Now.AddSeconds(.1));
        late.Now = Start.AddSeconds(10);
        late.Set(1, a => a with { InCombat = true });
        late.Sync();
        Check(!late.E.CarnivaleCastKnockbacks.Any() && late.E.CarnivaleSnorter!.SnortEffectEnds == default, "Pre-pull cancel revived.");
    }

    private static void Roster()
    {
        var immunity = new CarnivaleKnockbackImmunity();
        var expiry = Start.AddSeconds(30);
        immunity.Observe([new(100, 0, Center, PartySlot: 0,
            StatusSlots: [new(160, 0, 100, expiry) { SlotIndex = 0 }])]);
        Check(immunity.ImmuneAt(0, Start), "Activation roster.");
        immunity.Observe([new(100, 0, Center, PartySlot: 1, StatusSlots: []),
            new(101, 0, Center, PartySlot: 0, StatusSlots: [])]);
        Check(!immunity.ImmuneAt(0, Start) && !immunity.ImmuneAt(1, Start), "Loss cleared current rather than callback-time old slot.");
        immunity.Observe([new(100, 0, Center, PartySlot: 2,
            StatusSlots: [new(160, 0, 100, expiry) { SlotIndex = 0 }]), new(101, 0, Center, PartySlot: 0, StatusSlots: [])]);
        Check(immunity.ImmuneAt(1, Start) && !immunity.ImmuneAt(2, Start), "Gain must use previous roster.");
        var fullRoster = new Dictionary<ulong, int> { [100] = 0 };
        var absent = new CarnivaleKnockbackImmunity();
        var actor = new DungeonActor(100, 0, Center, PartySlot: 0,
            StatusSlots: [new(160, 0, 100, expiry) { SlotIndex = 0 }]);
        absent.Observe([actor], fullRoster);
        absent.Removed(100);
        absent.Observe([], fullRoster);
        Check(!absent.ImmuneAt(0, Start), "Destroy must Lose old status.");
        absent.Observe([actor], fullRoster);
        Check(absent.ImmuneAt(0, Start), "Recreated actor lost unchanged roster membership.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
