using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleDraugarRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Draugar source lifecycle small polygon all adds markers preserves pointless", Lifecycle);
        run("Carnivale Draugar Necrobane raw view quantized navigation dead state7 union inversion", Pools);
        run("Carnivale Draugar MegaDeath single global overwritten locked clock any Finish no effect timeout", Inversion);
        run("Carnivale Draugar shared three action R6 FIFO any cast Finish and 99 threshold sort", Groups);
        run("Carnivale Draugar four directions chain relative dynamic pose expired pending no wall immunity", March);
        run("Carnivale Draugar per slot ID source Param expiry threshold gains first direction losses", Slots);
        run("Carnivale Draugar two forced status categories overwrite current residual and any Lose clears", Forced);
        run("Carnivale Draugar remove emits slot losses same ID generation no death clear perspective", Identity);
        run("Carnivale Draugar final march hazard only Bilrost or bounds no poison intermediate checks", Destination);
        run("Carnivale Draugar pure hints raidwide raw clock real Finish bootstrap canceled raw", Casts);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors = [new(1, 0x2CD2, Center, InCombat: true, Name: "Durinn"),
            new(2, 0x233C, Center), new(100, 0, Center, PartySlot: 0, Player: true),
            new(101, 0, Center + Vector3.UnitX * 3, PartySlot: 1, Player: true)];
        internal DateTime Now = Start;
        internal ulong Player = 100;
        internal Fixture(bool combat = true) { Set(1, a => a with { InCombat = combat }); Sync(); }
        internal CarnivaleDraugarState State => E.CarnivaleDraugar!;
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == Player)?.Position, Now, playerId: Player);
        internal void Cast(uint action, ulong source = 2, double seconds = 4, Vector3? location = null)
        {
            Now = Now.AddSeconds(.01);
            if (!Actors.Any(a => a.Id == source)) Actors.Add(new(source, 0x233C, Center));
            Set(source, a => a with { Cast = new(action, location ?? Center, 0, Now, Now.AddSeconds(seconds), 100) });
            Sync();
        }
        internal void Slots(params ReplayStatus[] statuses) { Set(100, a => a with { StatusSlots = statuses }); Sync(); }
        internal DungeonAmbientAoe[] Pools() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/necrobane/")).ToArray();
        internal (Vector3 From, Vector3 To, float Rotation)[] Moves() => E.CarnivaleDraugarMovements.ToArray();
    }

    private static ReplayStatus Status(uint id, int slot, double expires, ulong source = 1, ushort param = 0)
        => new(id, param, source, Start.AddSeconds(expires)) { SlotIndex = slot };

    private static void Lifecycle()
    {
        var f = new Fixture(false);
        Check(f.E.CarnivalePrecombatHint!.Contains("Mustard Bomb") && !f.E.AmbientAoes().Any(a => a.Navigate), "Preparation.");
        f.Actors.AddRange(new uint[] { 0x2CD4, 0x2CD3, 0x2CD7, 0x2CD5, 0x2CD6, 0x2CEC }.Select((oid, i) =>
            new DungeonActor((ulong)(10 + i), oid, Center, IsEnemy: true, ObjectIndex: (ushort)(10 + i * 2), Hp: 1)));
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.Active?.BossOid == 0x2CD2 && f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == 6
            && !f.E.Arena!.Contains(Center + Vector3.UnitX * 17)
            && f.E.PriorityTarget(0, Center) == 0, "Small arena/5 adds/priority preserves pointless.");
        f.Set(10, a => a with { Hp = 100 });
        f.Sync();
        Check(f.E.PriorityTarget(0, Center) == 10, "Eligible add priority.");
        f.Set(1, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active == null, "Adds kept primary alive.");
    }

    private static void Pools()
    {
        var f = new Fixture();
        var raw = Center + new Vector3(1.12345f, 0, 2.37654f);
        f.Actors.Add(new(3, 0x1EA9FA, raw, Dead: true, Targetable: false));
        f.Actors.Add(new(4, 0x1EA9FA, raw + Vector3.UnitX * 8));
        f.Sync();
        Check(f.Pools().Count(a => !a.Navigate) == 2 && f.Pools().Count(a => !a.Display) == 2
            && f.Pools().Single(a => a.Id.EndsWith("display/3")).Position == raw
            && f.Pools().Single(a => a.Id.EndsWith("nav/3")).Position != raw, "Raw display/quantized constraint.");
        f.Cast(19055);
        var union = f.Pools().Single(a => a.Navigate);
        var shape = union.Shape.Resolve(Vector3.Zero, 0, null);
        Check(!shape.Contains(raw) && !shape.Contains(raw + Vector3.UnitX * 4) && !shape.Contains(raw + Vector3.UnitX * 8)
            && shape.Contains(Center - Vector3.UnitX * 12) && f.Pools().Count(a => a.Safe) == 2,
            "Inverted union is any pool, including overlap.");
        Check(f.E.Hints(raw).Contains("Stay in voidzone") && f.E.Hints(Center - Vector3.UnitX * 12).Contains("Go to voidzone!"), "Inverted text.");
        f.Set(3, a => a with { EventState = 7 });
        f.Set(4, a => a with { EventState = 7 });
        f.Sync();
        Check(f.Pools().Length == 0 && f.E.Hints(Center).Contains("Go to voidzone!"), "Empty inverted set must not publish full arena danger.");
    }

    private static void Inversion()
    {
        var f = new Fixture();
        f.Cast(19055, seconds: 4);
        var old = f.State.Inversion!;
        f.Now = Start.AddSeconds(1);
        var raw = f.Actors.Single(a => a.Id == 2).Cast!;
        f.E.StartCast(2, raw with { Location = Center + Vector3.UnitX }, f.Now, fromPacket: true);
        Check(ReferenceEquals(f.State.Inversion, old), "Packet correction restarted global flip.");
        f.Cast(19055, 3, 8);
        Check(f.State.Inversion!.Source == 3, "Second source did not overwrite.");
        f.Now = Start.AddSeconds(30);
        f.Sync();
        f.E.Effect(3, 19055, 1, f.Now);
        Check(f.State.Inversion != null, "Timeout/effect cleared flip.");
        f.E.Finish(2, 19055, f.Now);
        Check(f.State.Inversion == null, "Any matching real Finish must clear global slot.");
    }

    private static void Groups()
    {
        var f = new Fixture();
        f.Cast(19059, 2, 30);
        f.Cast(19064, 3, 5);
        f.Cast(19067, 4, 10);
        DungeonAmbientAoe[] Saps() => f.E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/sap/")).ToArray();
        Check(Saps().Length == 3 && Saps().All(a => a.Shape is CircleTemplate { Radius: 6 })
            && Saps()[0].Activation > Saps()[1].Activation && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null), "Shared group order/radius.");
        f.E.Effect(2, 19059, 1, f.Now);
        Check(Saps().Length == 3, "Effect finished group.");
        f.E.Finish(2, 60000, f.Now);
        Check(Saps().Length == 2, "First caster real Finish.");
        for (ulong id = 10; id < 107; ++id) f.Cast(19059, id, (double)(110 - id));
        Check(Saps().Length == 99 && Saps().Select(a => a.Activation).SequenceEqual(Saps().Select(a => a.Activation).Order()), "99 threshold sort.");
    }

    private static void March()
    {
        var f = new Fixture();
        f.Slots(Status(1961, 0, 40), Status(1958, 1, 10), Status(1960, 2, 20), Status(1959, 3, 30));
        var moves = f.Moves();
        Check(moves.Length == 4 && Near(moves[0].To, Center + Vector3.UnitZ * 13.2f)
            && Near(moves[1].To, Center + new Vector3(13.2f, 0, 13.2f))
            && Near(moves[2].To, Center + Vector3.UnitZ * 13.2f) && Near(moves[3].To, Center), "Ordered cumulative directions.");
        f.Now = Start.AddSeconds(100);
        f.Set(100, a => a with { Position = Center + Vector3.UnitX, Rotation = MathF.PI / 2, Dead = true });
        f.Sync();
        Check(f.Moves().Length == 4 && Near(f.Moves()[0].To, Center + Vector3.UnitX * 14.2f)
            && f.E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/brainstorm/")).All(a => !a.Navigate), "Dynamic pose expired/dead pending no wall/navigation.");
    }

    private static void Slots()
    {
        var f = new Fixture();
        f.Slots(Status(1958, 0, 10), Status(1958, 1, 20, 2));
        Check(f.Moves().Length == 2 && f.State.StatusSlots[(100, 1)].SourceId == 2, "Same ID different source slots.");
        f.Slots(Status(1958, 0, 11), Status(1958, 1, 20, 2));
        Check(f.Moves().Length == 2, "Exactly one second drift produced Gain.");
        f.Slots(Status(1958, 0, 12.01), Status(1958, 1, 20, 2));
        Check(f.Moves().Length == 3, "Larger extension did not produce extra Gain.");
        f.Slots(Status(1958, 1, 20, 2));
        Check(f.Moves().Length == 2, "Lose removes first direction only.");
        f.Slots(Status(1958, 1, 20, 3));
        Check(f.Moves().Length == 2, "Source change must Lose then Gain once.");
        f.Slots(Status(1958, 1, 20, 3, 1));
        Check(f.Moves().Length == 3, "Param Gain should retain existing pending.");
    }

    private static void Forced()
    {
        var f = new Fixture();
        f.Slots(Status(1257, 0, 5), Status(3629, 1, 8), Status(1960, 2, 20));
        f.Now = Start.AddSeconds(2);
        f.Sync();
        Check(f.Moves().Length == 2 && Near(f.Moves()[0].To, Center + Vector3.UnitZ * 39.6f), "Last forced category deadline/current residual.");
        f.Slots(Status(3629, 1, 8), Status(1960, 2, 20));
        Check(f.Moves().Length == 1, "Any forced Lose must clear despite other occupied forced slot.");
    }

    private static void Identity()
    {
        var f = new Fixture();
        f.Slots(Status(1960, 0, 10), Status(1960, 1, 20));
        f.Set(100, a => a with { Dead = true });
        f.Sync();
        Check(f.Moves().Length == 2, "Death cleared statuses.");
        f.Player = 101;
        f.Sync();
        Check(f.Moves().Length == 0, "Perspective reused another player's march.");
        f.Actors.RemoveAll(a => a.Id == 100);
        f.Sync();
        f.Actors.Add(new(100, 0, Center, Player: true, PartySlot: 0));
        f.Player = 100;
        f.Sync();
        Check(f.Moves().Length == 0 && !f.State.StatusSlots.Keys.Any(k => k.Actor == 100), "Destroy did not emit slot losses.");
    }

    private static void Destination()
    {
        var f = new Fixture();
        f.Slots(Status(1958, 0, 10));
        var to = f.Moves().Single().To;
        f.Actors.Add(new(3, 0x1EA9FA, to));
        f.Sync();
        Check(!f.E.Hints(Center).Contains("Aim for safe spot!"), "Poison must not influence march destination.");
        f.Cast(19082, location: to);
        Check(f.E.Hints(Center).Contains("Aim for safe spot!"), "Bilrost landing missing.");
        f.E.Finish(2, 19082, f.Now);
        f.Set(2, a => a with { Cast = null });
        f.Slots(Status(1958, 0, 10), Status(1958, 1, 20));
        Check(f.E.Hints(Center).Contains("Aim for safe spot!"), "Outside landing ignored.");
    }

    private static void Casts()
    {
        foreach (var (action, hint) in new[] { (19053u, "Interrupt!"), (19057u, "Summons adds! (Kill with fire!)"),
            (19051u, "Heal to full before cast ends!"), (19084u, "Raidwide"), (19056u, "Raidwide") })
        {
            var f = new Fixture();
            f.Cast(action);
            Check(f.E.Hints(Center).Contains(hint) && !f.E.AmbientAoes().Any(a => a.Navigate)
                && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null) && f.E.PredictedDamage.Count() == (action is 19084 or 19056 ? 2 : 0), "Pure cast scope.");
            f.Set(101, a => a with { Dead = true });
            f.Set(2, a => a with { Cast = a.Cast! with { FinishAt = Start.AddSeconds(40) } });
            f.Sync();
            if (action is 19084 or 19056)
                Check(f.E.PredictedDamage.Single().Activation == Start.AddSeconds(40), "Raidwide current raw clock/alive roster.");
            f.E.Finish(2, action, f.Now);
            Check(!f.E.Hints(Center).Contains(hint), "Hint Finish.");
        }
        var late = new Fixture(false);
        late.Cast(19055);
        late.E.Finish(2, 19055, late.Now.AddSeconds(.1));
        late.Now = Start.AddSeconds(10);
        late.Set(1, a => a with { InCombat = true });
        late.Sync();
        Check(late.State.Inversion == null, "Canceled unclaimed raw revived at pull.");
    }

    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
