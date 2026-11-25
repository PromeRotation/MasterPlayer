using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleTyphonRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Typhon three acts primary lifecycle exact scope arena preparation achievement", Lifecycle);
        run("Carnivale Typhon first act locked origin wall hitbox stop no navigation or damage", Wall);
        run("Carnivale Typhon third act independent knockback components chained shape and insertion order", Chains);
        run("Carnivale Typhon role job duty overwrite expiry strict immunity observed raw slots", Immunity);
        run("Carnivale Typhon fireball shared group arbitrary action Finish sorting threshold and raw clock", Fireballs);
        run("Carnivale Typhon interrupt showName single multiple and ordinary source dimensions", Interrupts);
        run("Carnivale Typhon shared action background keeps separate wall behavior and bootstrap order", Background);
    }
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(uint oid = 0x272C, bool combat = true)
        {
            Actors =
            [
                new(1, oid, Center, InCombat: combat, IsEnemy: true, ObjectIndex: 2, Name: "提丰"),
                new(2, 0x233C, Center, Name: "施法者A"),
                new(3, 0x233C, Center, Name: "施法者B"),
                new(100, 0, Center + Vector3.UnitZ * 10, PartySlot: 0, Player: true),
            ];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = fn(Actors[index]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == 100).Position, Now, playerId: 100);
        internal void Cast(uint action, ulong source = 2, Vector3? location = null, float rotation = 0, double duration = 4)
        {
            Now = Now.AddSeconds(.01);
            Set(source, a => a with { Cast = new(action, location ?? Center, rotation, Now, Now.AddSeconds(duration)) });
            Sync();
        }
        internal string[] Hints() => E.Hints(Actors.Single(a => a.Id == 100).Position).ToArray();
        internal (CarnivaleKnockbackCast Source, Vector3 From, Vector3 To)[] Moves(uint action = 14704) =>
            E.CarnivaleCastKnockbackMovements(Actors.Single(a => a.Id == 100), action).ToArray();
    }
    private static void Lifecycle()
    {
        foreach (var oid in new uint[] { 0x272A, 0x272B, 0x272C })
        {
            var f = new Fixture(oid, false);
            f.Actors.Add(new(4, 0x272D, Center, InCombat: true, IsEnemy: true, ObjectIndex: 8));
            f.Actors.Add(new(5, 0x272E, Center, IsEnemy: true, ObjectIndex: 10));
            f.Sync();
            Check(f.E.Active == null && f.E.CarnivalePrecombatHint != null, "Auxiliary pull.");
            Check(f.Hints().Any(h => h.StartsWith("Requirement for achievement:")) == (oid != 0x272B), "Constructor achievement.");
            f.Set(1, a => a with { InCombat = true });
            f.Sync();
            Check(f.E.Active?.Casts.Length == (oid == 0x272A ? 2 : oid == 0x272B ? 5 : 7)
                && !f.Hints().Any(h => h.StartsWith("Requirement for achievement:"))
                && f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == (oid == 0x272C ? 3 : 1),
                "Exact component scope/preparation/markers.");
            Check(f.E.Arena!.Contains(Center + Vector3.UnitX * 20) == (oid == 0x272A), "Source arena size.");
            if (oid == 0x272C) Check(f.E.PriorityTarget(20, Center) == 5, "Tentacle priority.");
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active == null, "Primary completion waited for adds.");
        }
    }
    private static void Wall()
    {
        var f = new Fixture(0x272A);
        f.Cast(14704);
        var kb = f.E.CarnivaleCastKnockbacks.Single();
        Check(kb.StopAtWall && kb.Distance == 30 && kb.EffectShape == null && kb.Cast.Location == Center,
            "Full KB model.");
        var move = f.Moves().Single();
        Check(Math.Abs(move.To.Z - 124.499f) < .0001f && move.From.Z == 110
            && !f.Hints().Contains("About to be knocked into danger!") && f.Hints().Contains("Use Diamondback!"),
            "Wall or source text.");
        f.Set(2, a => a with { Position = Center + Vector3.UnitX * 80, Rotation = MathF.PI });
        f.Now = Start.AddSeconds(10);
        f.Sync();
        Check(f.Moves().Single().To == move.To && f.E.CarnivaleCastKnockbacks.Single().Cast == kb.Cast,
            "Locked location or observed lifetime.");
        Check(f.E.AmbientAoes().Where(a => a.Id.Contains("/typhon/kb/")).All(a => !a.Navigate)
            && !f.E.PredictedDamage.Any(), "KB preview became navigation/damage.");
        f.E.Finish(2, 14704);
        Check(!f.Moves().Any(), "KB Finish.");
    }
    private static void Chains()
    {
        var f = new Fixture();
        f.Cast(14748, 2);
        f.Cast(14704, 3);
        Check(f.Moves(14748).Single().To.Z == 130 && f.Moves().Single().To.Z == 140,
            "Independent components merged into one chain.");
        f.Actors.Add(new(4, 0x233C, Center));
        f.Cast(14748, 4, Center + Vector3.UnitZ * 20);
        var wallops = f.Moves(14748);
        Check(wallops.Length == 2 && wallops[1].From == wallops[0].To && wallops[1].To.Z == 150,
            "Within-component chaining/shape origin.");
        Check(f.Hints().Contains("About to be knocked into danger!"), "No-wall KB danger.");
        f.Set(100, a => a with { Position = Center + Vector3.UnitX * 6 + Vector3.UnitZ * 10 });
        f.Sync();
        Check(!f.Moves(14748).Any() && f.Moves().Single().To.X > 106, "Wallop shape or radial direction.");
        f.E.Finish(3, 14704);
        Check(f.E.CarnivaleCastKnockbacks.Count() == 2, "Finish cleared other component.");
    }
    private static void Immunity()
    {
        var f = new Fixture();
        f.Cast(14704);
        var activation = f.E.CarnivaleCastKnockbacks.Single().Cast.FinishAt;
        f.E.StatusChanged(100, 1722, true, f.Now, expiresAt: activation);
        Check(f.Moves().Length == 1, "Equal expiry immune.");
        f.E.StatusChanged(100, 1722, true, f.Now.AddSeconds(.1), expiresAt: activation.AddSeconds(1));
        Check(!f.Moves().Any() && f.Hints().Contains("Use Diamondback!"), "Diamondback immunity or hint.");
        f.E.StatusChanged(100, 2663, false, f.Now.AddSeconds(.2));
        Check(f.Moves().Length == 1, "Shared category lose.");
        f.Set(100, a => a with { StatusSlots = [new MasterPlayer.Replay.ReplayStatus(2345, 0, 1, activation.AddSeconds(2)) { SlotIndex = 2 }] });
        f.Sync();
        Check(!f.Moves().Any(), "Raw immunity slots.");
        f.Set(100, a => a with { StatusSlots = [] });
        f.Sync();
        Check(f.Moves().Length == 1, "Raw lose.");
    }
    private static void Fireballs()
    {
        var f = new Fixture();
        f.Cast(14706, 2, Center, duration: 9);
        f.Cast(14747, 3, Center + Vector3.UnitZ * 5, duration: 3);
        var aoes = f.E.AmbientAoes().Where(a => a.Id.Contains("/typhon/fireball/")).ToArray();
        Check(aoes.Length == 2 && aoes[0].Position == Center && aoes.All(a => a.Shape is CircleTemplate { Radius: 8 } && a.Navigate),
            "Shared group insertion order or circle.");
        f.Now = Start.AddSeconds(20);
        f.Sync();
        Check(f.E.AmbientAoes().Count(a => a.Id.Contains("/typhon/fireball/")) == 2, "Fireball clock timeout.");
        f.E.Finish(2, 60000, f.Now);
        Check(f.E.AmbientAoes().Single(a => a.Id.Contains("/typhon/fireball/")).Position == Center + Vector3.UnitZ * 5,
            "Shared arbitrary AID Finish.");
        var g = new Fixture();
        for (var i = 0; i < 99; ++i)
            g.Actors.Add(new((ulong)(200 + i), 0x233C, Center, Cast: new(i % 2 == 0 ? 14706u : 14747u,
                Center + Vector3.UnitX * i, 0, Start, Start.AddSeconds(100 - i))));
        g.Sync();
        var sorted = g.E.AmbientAoes().Where(a => a.Id.Contains("/typhon/fireball/")).Select(a => a.Activation).ToArray();
        Check(sorted.Length == 99 && sorted.SequenceEqual(sorted.Order()), "99 source sorting threshold.");
    }
    private static void Interrupts()
    {
        var f = new Fixture();
        f.Cast(14744);
        Check(f.Hints().Contains("Interrupt 施法者A!") && f.E.CarnivaleInterrupts.Count() == 1, "Single named interrupt.");
        f.Set(2, a => a with { Name = "改名A" });
        f.Sync();
        Check(f.Hints().Contains("Interrupt 施法者A!") && !f.Hints().Contains("Interrupt 改名A!"), "Rename refreshed event-cached hint.");
        f.Cast(14744, 3);
        Check(f.Hints().Contains("Interrupt!") && !f.Hints().Contains("Interrupt 施法者A!"), "Multiple interrupt name.");
        f.E.Finish(3, 14744);
        Check(f.Hints().Contains("Interrupt 改名A!"), "Named hint after Finish.");
        var rules = CarnivaleCatalog.Create().Where(d => d.BossOid is 0x272A or 0x272B or 0x272C).SelectMany(d => d.Casts).ToArray();
        Check(rules.Single(r => r.Action == 14713).Shape is FanTemplate { Radius: 13.1f, AngleRadians: MathF.PI / 2 }
            && rules.Single(r => r.Action == 14714).Shape is CircleTemplate { Radius: 11.1f }
            && rules.Single(r => r.Action == 14718).Shape is CircleTemplate { Radius: 4 }
            && rules.Single(r => r.Action == 14717).Shape is CircleTemplate { Radius: 3 }
            && rules.Single(r => r.Action == 14748).Shape is RayRectangleTemplate { Length: 57.2f, MaxLocalX: 5 }
            && rules.All(r => r.Action is not (14705 or 14707 or 14749)), "Ordinary dimensions/inactive scope.");
    }
    private static void Background()
    {
        var f = new Fixture(0x272A);
        f.Actors.Add(new(4, 0x272C, Center, InCombat: true));
        f.Sync();
        f.Cast(14704);
        Check(f.Moves().Single().Source.StopAtWall, "First wall flag.");
        f.Set(1, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active?.BossOid == 0x272C && !f.Moves().Single().Source.StopAtWall && f.Moves().Single().To.Z == 140,
            "Shared input contaminated per-module wall.");
        f.E.Finish(2, 14704);
        Check(!f.E.CarnivaleCastKnockbacks.Any(), "Background Finish.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
