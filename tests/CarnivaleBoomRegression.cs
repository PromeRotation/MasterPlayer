using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleBoomRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Boom first act exact XZ movement no cold Y rotation repeat or perspective movement", Movement);
        run("Carnivale Boom targetable second boss blocks even dead group completion not primary health", Lifecycle);
        run("Carnivale Boom second act default primary living bomb hints persistent order colours", Hints);
        run("Carnivale Boom Sap shared insertions raw locks arbitrary Finish no effect or timeout", Sap);
        run("Carnivale Boom Sap threshold99 sorting retained tail and distinct ordinary source shapes", SortAndShapes);
        run("Carnivale Boom Ignition current raw clock raid64 dead exclusion no invented navigation or actions", Ignition);
        run("Carnivale Boom background late activation actor order independent deadline immediate handoff", Background);
    }
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal ulong Viewer = 100;
        internal Fixture(uint oid = 0x26FE)
        {
            Actors = [new(1, oid, Center, InCombat: true), new(2, 0x233C, Center),
                new(100, 0, Center, Player: true, PartySlot: 0)];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = fn(Actors[index]);
        }
        internal void Sync() => E.Synchronize(Actors, Center, Now, playerId: Viewer);
        internal void Cast(uint action, ulong source = 2, double seconds = 4, Vector3? location = null)
        {
            Now = Now.AddSeconds(.01);
            if (Actors.All(a => a.Id != source)) Actors.Add(new(source, 0x233C, Center));
            Set(source, a => a with { Cast = new(action, location ?? Center, .2f, Now, Now.AddSeconds(seconds)) });
            Sync();
        }
        internal DungeonAmbientAoe[] Saps() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/sap/")).ToArray();
    }
    private static void Movement()
    {
        var f = new Fixture(0x26FC);
        Check(f.E.Active == null && f.E.CarnivalePrecombatHint != null, "Cold actor moved.");
        f.Set(100, a => a with { Position = Center + Vector3.UnitY, Rotation = 1 });
        f.Sync();
        Check(f.E.Active == null, "Y/rotation pulled.");
        f.Actors.Add(new(101, 0, Center + Vector3.UnitZ * 10, Player: true, PartySlot: 1));
        f.Viewer = 101;
        f.Sync();
        Check(f.E.Active == null, "New view actor pulled.");
        f.Viewer = 100;
        f.Sync();
        Check(f.E.Active == null, "Perspective swap created movement.");
        f.Set(101, a => a with { Position = a.Position + Vector3.UnitX });
        f.Sync();
        Check(f.E.Active == null, "Non-viewer move pulled.");
        f.Set(100, a => a with { Position = a.Position + Vector3.UnitX * .00001f });
        f.Sync();
        Check(f.E.Active?.BossOid == 0x26FC, "Small nonzero XZ did not pull.");
        var g = new Fixture(0x26FC);
        g.Set(1, a => a with { Targetable = false });
        g.Set(100, a => a with { Position = a.Position + Vector3.UnitX });
        g.Sync();
        g.Set(1, a => a with { Targetable = true });
        g.Sync();
        Check(g.E.Active == null, "Repeated same timestamp/frame retained old movement.");
        g.Actors.RemoveAll(a => a.Id == 100);
        g.Sync();
        g.Actors.Add(new(100, 0, Center + Vector3.UnitZ * 8, Player: true, PartySlot: 0));
        g.Sync();
        Check(g.E.Active == null, "Same ID recreated actor moved.");
    }
    private static void Lifecycle()
    {
        var f = new Fixture(0x26FC);
        f.Actors.Add(new(3, 0x26FE, Center, Dead: true, Targetable: true));
        f.Actors.Add(new(4, 0x26FC, Center, Targetable: false));
        f.Set(100, a => a with { Position = a.Position + Vector3.UnitX });
        f.Sync();
        Check(f.E.Active == null, "Dead but targetable Act2 failed to block.");
        f.Set(3, a => a with { Targetable = false });
        f.Sync();
        Check(f.E.Active == null, "Unblocking retained movement.");
        f.Set(1, a => a with { Targetable = false });
        f.Set(4, a => a with { Targetable = true, Hp = 0 });
        f.Set(100, a => a with { Position = a.Position + Vector3.UnitX });
        f.Sync();
        Check(f.E.Active?.BossOid == 0x26FC, "Any targetable HP0 grenade cannot pull.");
        f.Set(1, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active != null && f.E.Hints(Center).Any(h => h.Contains("543 HP"))
            && f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == 1, "Primary death/HP0 ended group.");
        f.Set(4, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active == null, "AllDeadOrDestroyed did not complete.");
    }
    private static void Hints()
    {
        var f = new Fixture();
        f.Actors.Add(new(4, 0x26FC, Center, Name: "First", Targetable: false, Hp: 0));
        f.Actors.Add(new(5, 0x26FC, Center, Name: "Second"));
        f.Actors.Add(new(6, 0x26FD, Center, Name: "Gas"));
        f.Sync();
        f.Actors.Reverse();
        f.Sync();
        var hints = f.E.Hints(Center).ToArray();
        Check(hints.Contains("First in one hit or it will wipe you! It got 543 HP.")
            && hints.Any(h => h.Contains("pull the Gas to the bos\n")), "Living first-created hint order/filters.");
        Check(f.E.AmbientAoes().Where(a => a.Id is "carnivale/enemy/5" or "carnivale/enemy/6")
            .All(a => a.Color == new Vector4(1, 128f / 255, 0, 1)), "Add colours.");
        f.Set(4, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Hints(Center).Any(h => h.StartsWith("Second in one hit")), "Dead grenade not skipped.");
        f.Set(1, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active == null, "Act2 Primary HP0 failed to complete.");
    }
    private static void Sap()
    {
        var f = new Fixture();
        f.Cast(14906, seconds: 9);
        f.Cast(14907, 3, seconds: 3, location: Center + Vector3.UnitZ * 5);
        var saps = f.Saps();
        Check(saps.Length == 2 && saps[0].Position == Center && saps.All(a => a.Navigate
            && a.Shape is CircleTemplate { Radius: 8 }), "Sap shared insertion or geometry.");
        f.Set(2, a => a with { Position = Center + Vector3.UnitX * 10, Rotation = 2 });
        f.Sync();
        Check(f.Saps()[0].Position == Center, "Sap followed caster.");
        f.E.Effect(2, 14906, 1, f.Now);
        f.Now = Start.AddSeconds(20);
        f.Sync();
        Check(f.Saps().Length == 2, "Effect/deadline cleared Sap.");
        f.E.Finish(2, 60000, f.Now);
        Check(f.Saps().Length == 1 && f.Saps()[0].Position.Z == 105, "Unrelated action Finish did not remove caster.");
        f.E.Finish(3, 14907, f.Now);
        Check(f.Saps().Length == 0, "Sap Finish.");
    }
    private static void SortAndShapes()
    {
        var f = new Fixture();
        for (var i = 0; i < 98; ++i) f.Cast(i % 2 == 0 ? 14906u : 14907u, (ulong)i + 10, 200 - i, Center + Vector3.UnitX * i);
        Check(f.Saps()[0].Position == Center, "Sap sorted below threshold.");
        f.Cast(14907, 108, 1, Center + Vector3.UnitX * 98);
        Check(f.Saps().Length == 99 && f.Saps()[0].Position.X == 198, "Sap threshold99 did not sort.");
        var rules = CarnivaleCatalog.Create().Single(d => d.BossOid == 0x26FE).Casts;
        Check(rules.Single(r => r.Action == 14903).Shape is FanTemplate { Radius: 11.75f }
            && rules.Single(r => r.Action == 14902).Shape is CircleTemplate { Radius: 7.2f }
            && rules.Single(r => r.Action == 15040).Shape == null
            && rules.All(r => r.Action is not (14901 or 14904 or 14905 or 6499)), "Source dimensions/enumeration scope.");
    }
    private static void Ignition()
    {
        var f = new Fixture();
        f.Actors.Add(new(101, 0, Center, PartySlot: 24));
        f.Actors.Add(new(102, 0, Center, PartySlot: 63));
        f.Actors.Add(new(103, 0, Center, PartySlot: 1, Dead: true));
        f.Actors.Add(new(104, 0, Center, PartySlot: 64));
        f.Cast(15040);
        Check(f.E.PredictedDamage.Select(d => d.TargetId).Order().SequenceEqual(new ulong[] { 100, 101, 102 })
            && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null) && !f.E.CarnivaleInterrupts.Any()
            && !f.E.Gazes.Any(), "Ignition raid64 or invented outputs.");
        var later = Start.AddSeconds(40);
        f.Set(2, a => a with { Cast = a.Cast! with { FinishAt = later } });
        f.Sync();
        Check(f.E.PredictedDamage.All(d => d.Activation == later), "Ignition raw dynamic clock.");
        f.E.Finish(2, 15040, f.Now);
        Check(!f.E.PredictedDamage.Any(), "Ignition Finish did not release.");
    }
    private static void Background()
    {
        var f = new Fixture(0x272F);
        f.Actors.Add(new(4, 0x26FE, Center, InCombat: false));
        f.Cast(14907, seconds: 1);
        f.Cast(14902, 5, seconds: 1);
        f.Now = Start.AddSeconds(10);
        f.Set(4, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.Active?.BossOid == 0x272F && !f.Saps().Any(), "Background display leak.");
        f.Set(1, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active?.BossOid == 0x26FE && f.Saps().Length == 1
            && f.Saps()[0].Activation == f.Now.AddSeconds((double).3f),
            $"Late bootstrap deadline or immediate handoff: active={f.E.Active?.BossOid:X}, count={f.Saps().Length}, " +
            $"activation={f.Saps().FirstOrDefault()?.Activation:O}, expected={f.Now.AddSeconds((double).3f):O}.");
        Check(f.E.OutputCasts.Single(c => c.Value.Rule.Action == 14902).Value.FinishAt == f.Now.AddSeconds((double).3f),
            "Late ordinary cast remained unclaimed or lost NPC finish delay.");
        f.E.Finish(2, 60000, f.Now);
        Check(!f.Saps().Any(), "Background arbitrary Finish.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
