using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleKegsRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale kegs both acts group pull all dead not hp zero and enemy priority scope", Lifecycle);
        run("Carnivale kegs constructor outline death remove destroy freeze and revival stays removed", ConstructorCache);
        run("Carnivale kegs constructor cache distinct from combat charge cache bootstrap", SeparateCaches);
        run("Carnivale kegs charge override text retains native bait navigation damage without knockback", Charge);
        run("Carnivale kegs all four ordinary AOEs exact dimensions bone shaker inactive", Ordinary);
        run("Carnivale kegs two modules same action scope and shared cache independent owner handoff", SharedModules);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(uint oid = 0x2724, bool combat = true)
        {
            Actors =
            [
                new(1, oid, Center, InCombat: combat, IsEnemy: true, ObjectIndex: 2),
                new(2, 0x233C, Center),
                new(3, 0x2726, Center + Vector3.UnitX * 5, IsEnemy: true, ObjectIndex: 4),
                new(100, 0, Center + Vector3.UnitZ * 10, PartySlot: 0, Player: true),
                new(101, 0, Center + Vector3.UnitZ * 5, PartySlot: 1, Player: true),
            ];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.First(a => a.Id == 100).Position, Now, playerId: 100);
        internal void Cast(uint action = 15055, ulong target = 100)
        {
            Set(2, a => a with { Cast = new(action, Center, .3f, Now, Now.AddSeconds(3.8), target) });
            Sync();
        }
        internal DungeonAmbientAoe[] Kegs() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/explosion/")).ToArray();
        internal DungeonAmbientAoe[] Charges() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/charge/")).ToArray();
        internal string[] Hints() => E.Hints(Actors.First(a => a.Id == 100).Position).ToArray();
    }

    private static void Lifecycle()
    {
        foreach (var oid in new uint[] { 0x2724, 0x2725 })
        {
            var f = new Fixture(oid, false);
            Check(f.E.Active == null && f.E.CarnivalePrecombatHint != null && f.Kegs().Length == 1
                && f.E.AmbientAoes().All(a => !a.Navigate), "Constructor component.");
            f.Set(3, a => a with { InCombat = true, Targetable = false });
            f.Sync();
            Check(f.E.Active?.BossOid == oid && f.E.CarnivalePrecombatHint == null && f.E.Active.Casts.Length == 5,
                "Keg combat did not pull module.");
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active != null, "HP0 replaced Dead.");
            f.Set(1, a => a with { Dead = true });
            f.Sync();
            Check(f.E.Active != null, "Primary death ignored keg.");
            f.Set(3, a => a with { Dead = true });
            f.Sync();
            Check(f.E.Active == null && !f.Kegs().Any(), "All dead completion.");
            var g = new Fixture(oid);
            Check(g.E.PriorityTarget(3, Center) == 1, "Boss priority1 keg0.");
            g.Actors.Add(new(4, oid, Center + Vector3.UnitX, InCombat: true));
            g.Sync();
            var markers = g.E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/enemy/")).ToArray();
            Check(markers.Length == (oid == 0x2725 ? 3 : 2), "Act-specific all boss markers.");
            Check(markers.Single(a => a.Id.EndsWith("/3")).Color == new Vector4(1, 128f / 255, 0, 1), "Keg Object colour.");
        }
    }

    private static void ConstructorCache()
    {
        var f = new Fixture(combat: false);
        var original = f.Kegs().Single();
        Check(original.Shape is CircleTemplate { Radius: 10 } && !original.Navigate && original.OutlineOnly
            && original.Color == null && f.E.Hints(Center).Contains("In keg explosion radius!"), "Keg source outline/text only.");
        f.Actors.RemoveAll(a => a.Id == 3);
        f.Sync();
        Check(f.Kegs().Single().Position == original.Position, "Destroyed cache did not freeze.");
        f.Actors.Add(new(3, 0x2726, Center + Vector3.UnitX * 15));
        f.Sync();
        Check(f.Kegs().Length == 2, "Same ID new barrel replaced old cache.");
        f.Set(3, a => a with { Dead = true });
        f.Sync();
        Check(f.Kegs().Length == 1 && f.Kegs().Single().Position == original.Position, "Death cleared wrong generation.");
        f.Set(3, a => a with { Dead = false });
        f.Sync();
        Check(f.Kegs().Length == 1, "Revival re-entered source cache.");
    }

    private static void SeparateCaches()
    {
        var f = new Fixture(combat: false);
        f.Actors.RemoveAll(a => a.Id == 3);
        f.Sync();
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        f.Cast();
        Check(f.Kegs().Length == 1 && f.E.CarnivaleCharges.Count() == 1
            && !f.Hints().Contains("Aim charge at a keg!"), "WildCharge inherited constructor's destroyed keg.");
        f.Actors.Add(new(4, 0x2726, Center));
        f.Sync();
        Check(f.Hints().Contains("Aim charge at a keg!"), "Created live keg not delivered to active component.");
        f.Actors.RemoveAll(a => a.Id == 4);
        f.Sync();
        Check(f.Hints().Contains("Aim charge at a keg!") && f.Kegs().Length == 2, "Destroy incorrectly invoked OnDeath.");
        var g = new Fixture();
        g.Set(3, a => a with { Dead = true });
        g.Sync();
        g.Cast();
        Check(!g.Hints().Contains("Aim charge at a keg!") && !g.Kegs().Any(), "Dead bootstrap keg retained.");
    }

    private static void Charge()
    {
        foreach (var oid in new uint[] { 0x2724, 0x2725 })
        {
            var f = new Fixture(oid);
            f.Cast();
            Check(f.E.CarnivaleCharges.Count() == 1 && f.E.CarnivaleChargeKnockback == null
                && f.Charges().Single(a => a.Id.EndsWith("/display")).OutlineOnly
                && f.Charges().Any(a => a.Id.Contains("/party/")) && f.E.PredictedDamage.Single().TargetId == 100,
                "Wild charge lost inherited bait output or invented KB.");
            Check(f.Hints().Contains("Aim charge at a keg!") && !f.Hints().Contains("Bait away from raid!")
                && !f.Hints().Contains("GTFO from baited aoe!"), "Custom hint did not replace base.");
            f.Set(100, a => a with { Position = Center + Vector3.UnitX * 10 });
            f.Set(2, a => a with { Position = Center + Vector3.UnitZ });
            f.Sync();
            Check(Math.Abs(((RayRectangleTemplate)f.Charges().Single(a => a.Id.EndsWith("/display")).Shape).Length - MathF.Sqrt(101)) < .0001f,
                "Wild charge did not update dynamic length.");
            f.Set(2, a => a with { Dead = true });
            f.Sync();
            Check(f.Hints().Contains("Aim charge at a keg!") && !f.Charges().Any(), "Hints used ActiveBaits rather than CurrentBaits.");
            f.E.Finish(2, 15055);
            Check(!f.E.CarnivaleCharges.Any() && !f.Hints().Contains("Aim charge at a keg!"), "Charge Finish.");
        }
    }

    private static void Ordinary()
    {
        foreach (var oid in new uint[] { 0x2724, 0x2725 })
        {
            var rules = CarnivaleCatalog.Create().Single(d => d.BossOid == oid).Casts;
            Check(rules.Single(r => r.Action == 15054).Shape is CircleTemplate { Radius: 10 }
                && rules.Single(r => r.Action == 15051).Shape is CircleTemplate { Radius: 6 }
                && rules.Single(r => r.Action == 15050).Shape is FanTemplate { Radius: 8, AngleRadians: MathF.PI / 2 }
                && rules.Single(r => r.Action == 15052).Shape is FanTemplate { Radius: 15, AngleRadians: MathF.PI / 2 }
                && rules.All(r => r.Action != 15053), "Source dimensions/component scope.");
            var f = new Fixture(oid);
            f.Cast(15054);
            Check(f.E.OutputCasts.Count() == 1 && f.Kegs().All(a => !a.Navigate), "Explosion cast confused with constructor outline.");
        }
    }

    private static void SharedModules()
    {
        var f = new Fixture();
        f.Actors.Add(new(4, 0x2725, Center, InCombat: true));
        f.Sync();
        f.Cast();
        f.Set(1, a => a with { Dead = true });
        f.Set(3, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active?.BossOid == 0x2725 && f.E.CarnivaleCharges.Count() == 1
            && f.E.Casts[2].Rule.Action == 15055, "Unloading shared action removed background cast.");
        Check(!f.Hints().Contains("Aim charge at a keg!"), "Keg death not delivered to background cache.");
        f.E.Finish(2, 15055);
        Check(!f.E.CarnivaleCharges.Any(), "Background Finish.");
        var e = new DungeonEncounter(796);
        List<DungeonActor> ordered =
        [
            new(1, 0x2724, Center), new(2, 0x233C, Center), new(3, 0x233C, Center),
            new(100, 0, Center + Vector3.UnitZ * 10, PartySlot: 0, Player: true),
        ];
        e.Synchronize(ordered, Center, Start, playerId: 100);
        ordered[0] = ordered[0] with { InCombat = true };
        ordered[1] = ordered[1] with { Cast = new(15055, Center, 0, Start, Start.AddSeconds(3), 100) };
        ordered[2] = ordered[2] with { Cast = new(15055, Center, 0, Start, Start.AddSeconds(5), 100) };
        ordered.Reverse();
        e.Synchronize(ordered, Center, Start.AddSeconds(.1), playerId: 100);
        Check(e.CarnivaleCharges.Select(c => c.Cast.Source).SequenceEqual(new ulong[] { 2, 3 })
            && e.PredictedDamage.Single().Activation == Start.AddSeconds(3),
            "Bootstrap used reordered snapshot rather than persistent source creation order.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
