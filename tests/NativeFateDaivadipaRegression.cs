using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;

internal static class NativeFateDaivadipaRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly Vector3 Center = new(-608, 100, 811);
    private static DungeonActor Boss() => new(1, 0x356D, Center, InCombat: true, Hp: 1000);
    private static DungeonActor Player(ulong id = 100) => new(id, 0, Center, Hostile: false, PartySlot: (int)id - 100, Player: true);
    private static DungeonEncounter Encounter(params DungeonActor[] actors)
    {
        var e = new DungeonEncounter(957);
        e.Synchronize([Boss(), Player(), .. actors], Center, Start, playerId: 100);
        return e;
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native fate Daivadipa fixed square strict pull lifecycle and six ordinary geometries", Lifecycle);
        run("native fate Daivadipa LitPath entire reverse strict head prefix frozen raw X direction and finish FIFO", LitPath);
        run("native fate Daivadipa Burn first eight original clocks effect ignored and hidden promotion", Burn);
        run("native fate Daivadipa forced march current heading full hidden Lit danger excludes Burn and no navigation", March);
        run("native fate Daivadipa dynamic single target and delayed raidwide independent finish effect clocks", Damage);
        run("native fate Daivadipa source slot zero pull and existing orb order before first query", SourceIdentity);
        run("native fate Daivadipa packet correction does not repeat sequence side effects raw changes still do", Corrections);
        run("native fate Daivadipa actor raw disappearance same ID new generation restores identical march status", StatusGenerations);
        run("native fate Daivadipa march danger includes hidden Lit excludes Burn and checks first half only", MarchDangerBoundaries);
    }
    private static DungeonActor Orb(ulong id, uint oid, Vector3? position = null)
        => new(id, oid, position ?? Center, Targetable: false, Dead: true);
    private static DungeonAmbientAoe[] Aoes(DungeonEncounter e, string part)
        => e.AmbientAoes().Where(a => a.Id.StartsWith("fate/" + part + "/")).ToArray();
    private static void Lifecycle()
    {
        Check(NativeFateCatalog.Owns(957, 0x356D) && !NativeFateCatalog.Owns(1763, 0x356D), "Fate/territory confused.");
        var e = new DungeonEncounter(957);
        var edge = Center + Vector3.UnitX * 25;
        e.Synchronize([Boss(), Player() with { Position = edge }], edge, Start, playerId: 100);
        Check(e.Active == null && e.AmbientAoes().Count() == 2, "Strict 25y pull differs.");
        e.Synchronize([Boss(), Player()], Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active != null && e.Arena!.Contains(Center + new Vector3(24.5f, 0, 24.5f), 0)
            && !e.Arena.Contains(Center + Vector3.UnitX * 24.51f, 0), "Exact square differs.");
        e.Synchronize([Boss() with { InCombat = false }, Player()], Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active != null, "Daivadipa unexpectedly reset on deaggro.");
        var rules = e.Definitions[0].Casts.Where(c => c.Shape != null).ToArray();
        Check(rules.Length == 6 && rules.Single(c => c.Action == 26516).Shape is MasterPlayer.Spatial.SafeZone.CircleTemplate { Radius: 20 },
            "KarmicFlames used annotation range or missing ordinary AOEs.");
    }
    private static void LitPath()
    {
        var blue = Orb(10, 0x3570, Center with { X = -633 });
        var red = Orb(11, 0x356F, Center with { X = -632 });
        var e = Encounter(blue, red);
        e.StartCast(1, new(26499, Center, 0, Start, Start.AddSeconds(5)), Start);
        var first = Aoes(e, "lit");
        Check(first.Length == 1 && first[0].Activation == Start.AddSeconds(7.2)
            && MathF.Abs(first[0].Rotation - 89.999f * MathF.PI / 180) < 1e-7f, "Lit first prefix/raw-X angle/clock differs.");
        e.StartCast(1, new(26498, Center, 0, Start.AddSeconds(.1), Start.AddSeconds(5.1)), Start.AddSeconds(.1));
        first = Aoes(e, "lit");
        Check(first.Length == 1 && first[0].Rotation == MathF.PI && first[0].Activation == Start.AddSeconds(7.3),
            "Red Start did not reverse the entire existing queue.");
        var trigger = new DungeonActor(20, 0x3573, Center);
        e.Synchronize([Boss(), Player(), blue with { Position = Center }, red, trigger], Center, Start.AddSeconds(.2), playerId: 100);
        e.StartCast(20, new(26501, Center, 0, Start.AddSeconds(.3), Start.AddSeconds(1)), Start.AddSeconds(.3));
        e.Finish(20, 26501, completed: false);
        Check(Aoes(e, "lit").Length == 3, "Cancel did not globally consume head or prefix searched beyond first excluded item.");
        e.RemoveActor(10);
        e.Tick(Start.AddSeconds(30));
        Check(Aoes(e, "lit").Length == 3, "Orb destruction/clock removed already copied predictions.");
        e = Encounter(blue, new DungeonActor(30, 0x3573, Center));
        e.StartCast(1, new(26499, Center, 0, Start, Start.AddSeconds(5)), Start);
        e.StartCast(30, new(26499, Center, 0, Start, Start.AddSeconds(6)), Start);
        Check(Aoes(e, "lit").Length == 1, "Head activation plus exactly one second was included.");
    }
    private static void Burn()
    {
        var orbs = Enumerable.Range(0, 9).Select(i => Orb((ulong)(10 + i), 0x3572, Center + Vector3.UnitX * i)).ToArray();
        var e = Encounter(orbs);
        e.StartCast(1, new(26499, Center, 0, Start, Start.AddSeconds(5)), Start);
        Check(Aoes(e, "burn").Length == 8 && Aoes(e, "burn").All(a => a.Navigate && a.Activation == Start.AddSeconds(7.2)),
            "Burn first-eight/clock differs.");
        e.Effect(10, 26507, 1, Start.AddSeconds(1));
        Check(Aoes(e, "burn").Length == 8, "Effect advanced Finish-only Burn.");
        e.StartCast(10, new(26506, Center, 0, Start.AddSeconds(1), Start.AddSeconds(2)), Start.AddSeconds(1));
        e.Finish(10, 26506);
        Check(Aoes(e, "burn").Length == 8 && Aoes(e, "burn")[^1].Position.X > Center.X + 7.9f,
            "Hidden ninth failed to promote with original position/clock.");
    }
    private static void March()
    {
        var e = Encounter(Orb(10, 0x3570));
        e.StartCast(1, new(26499, Center, 0, Start, Start.AddSeconds(5)), Start);
        e.StatusChanged(100, 1958, true, Start.AddSeconds(.1), expiresAt: Start.AddSeconds(2));
        var projections = Aoes(e, "march");
        Check(projections.Length > 0 && projections.All(a => !a.Navigate) && e.ForbiddenTargets.Length == 0,
            "March became navigation or attack control.");
        e.Synchronize([Boss(), Player() with { Rotation = MathF.PI }], Center, Start.AddSeconds(.2), playerId: 100);
        Check(e.Hints(Center).Contains("Aim into AOEs!"), "March did not use current heading or retained copied Lit prediction.");
        e.StatusChanged(100, 1257, true, Start.AddSeconds(.3), expiresAt: Start.AddSeconds(1.3));
        Check(Aoes(e, "march").Length > projections.Length, "Actual movement displaced pending direction instead of preceding it.");
        e.StatusChanged(100, 1958, false, Start.AddSeconds(.4));
        e.StatusChanged(100, 1257, false, Start.AddSeconds(.5));
        Check(Aoes(e, "march").Length == 0, "Actual/pending Lose did not revoke march.");
    }
    private static void Damage()
    {
        var cast = new DungeonCastObservation(26510, Center, 0, Start, Start.AddSeconds(5), TargetId: 1);
        var e = Encounter(Player(101));
        e.Synchronize([Boss() with { Cast = cast, TargetId = 101 }, Player(), Player(101)], Center, Start, playerId: 100);
        Check(e.PredictedDamage.Single().TargetId == 101, "Drumbeat self-target did not use current actor target.");
        cast = cast with { FinishAt = Start.AddSeconds(5.01) };
        e.Synchronize([Boss() with { Cast = cast, TargetId = 100 }, Player(), Player(101)], Center, Start.AddSeconds(.1), playerId: 100);
        var damage = e.PredictedDamage.Single();
        Check(damage.TargetId == 100 && damage.Activation == cast.FinishAt, "Drumbeat cached initial target/clock.");
        e.StartCast(1, new(26517, Center, 0, Start.AddSeconds(1), Start.AddSeconds(6)), Start.AddSeconds(1));
        e.Finish(1, 26517, completed: false);
        e.Tick(Start.AddSeconds(20));
        Check(e.PredictedDamage.Count() == 2 && e.PredictedDamage.All(d => d.Activation == Start.AddSeconds(7)),
            "Delayed raidwide ended on cancel/time or lost exact +1 clock.");
        e.Effect(20, 26518, 0, Start.AddSeconds(21));
        Check(!e.PredictedDamage.Any(), "Actual redemption effect failed to clear delayed prediction.");
    }
    private static void SourceIdentity()
    {
        var e = new DungeonEncounter(957);
        e.Synchronize([Boss(), Player() with { Position = Center + Vector3.UnitX * 30 }, Player(101)],
            Center, Start, playerId: 101);
        Check(e.Active == null, "Pull followed viewed teammate instead of source slot zero.");
        var first = Orb(10, 0x3570, Center);
        var second = Orb(11, 0x3570, Center + Vector3.UnitX * 2);
        e.Synchronize([Boss(), Player(), first, second], Center, Start.AddSeconds(1), playerId: 100);
        e.Synchronize([Boss() with { Cast = new(26499, Center, 0, Start.AddSeconds(2), Start.AddSeconds(7)) },
            Player(), second, first], Center, Start.AddSeconds(2), playerId: 100);
        Check(Aoes(e, "lit")[0].Position.X < Aoes(e, "lit")[1].Position.X, "First orb query used reordered snapshot enumeration.");
    }
    private static void Corrections()
    {
        var e = Encounter(Orb(10, 0x3570), Orb(11, 0x3570, Center + Vector3.UnitX * 2));
        var cast = new DungeonCastObservation(26498, Center, 0, Start, Start.AddSeconds(5));
        e.StartCast(1, cast, Start);
        var before = Aoes(e, "lit");
        e.StartCast(1, cast with { Location = Center + Vector3.UnitZ }, Start.AddSeconds(.1));
        Check(Aoes(e, "lit").Length == before.Length && Aoes(e, "lit")[0].Position == before[0].Position,
            "Packet pose correction appended or reversed an already started queue.");
        e.StartCast(10, cast with { Action = 26501 }, Start.AddSeconds(.2));
        e.StartCast(10, cast with { Action = 26501, Location = Center + Vector3.UnitZ }, Start.AddSeconds(.3));
        Check(Aoes(e, "lit").Length == before.Length, "Packet correction consumed a real Finish-only sequence head.");
        e.Finish(10, 26501);
        Check(Aoes(e, "lit").Length == before.Length - 1, "Real Finish failed after packet pose correction.");
    }
    private static void StatusGenerations()
    {
        var other = Player(101) with { StatusExpirations = new Dictionary<uint, DateTime> { [1958] = Start.AddSeconds(10) },
            Statuses = [1958] };
        var e = Encounter(other);
        e.Synchronize([Boss(), Player()], Center, Start.AddSeconds(1), playerId: 100);
        e.Synchronize([Boss(), Player(), other], Center, Start.AddSeconds(2), playerId: 101);
        Check(Aoes(e, "march").Length > 0, "New actor with identical status was mistaken for destroyed old raw.");
    }
    private static void MarchDangerBoundaries()
    {
        var e = Encounter(Orb(10, 0x3570, Center + Vector3.UnitX * 20), Orb(11, 0x356F));
        e.StartCast(1, new(26499, Center, 0, Start, Start.AddSeconds(5)), Start);
        Check(Aoes(e, "lit").Length == 1 && Aoes(e, "lit")[0].Position.X > Center.X + 19,
            "Hidden red lane unexpectedly became visible.");
        e.StatusChanged(100, 1959, true, Start.AddSeconds(.1), expiresAt: Start.AddSeconds(2));
        Check(e.Hints(Center).Contains("Aim into AOEs!"), "Hidden Lit lane was omitted from march danger.");
        e = Encounter(Orb(10, 0x3572, Center + Vector3.UnitZ * 13.2f));
        e.StartCast(1, new(26499, Center, 0, Start, Start.AddSeconds(5)), Start);
        e.StatusChanged(100, 1958, true, Start.AddSeconds(.1), expiresAt: Start.AddSeconds(2));
        Check(!e.Hints(Center).Any(h => h.StartsWith("Aim")), "Burn incorrectly entered special march danger.");
        e = Encounter(new DungeonActor(10, 0x3573, Center), new DungeonActor(11, 0x3574, Center));
        e.StartCast(10, new(26508, Center + Vector3.UnitX * 100, 0, Start, Start.AddSeconds(7)), Start);
        e.StartCast(11, new(26509, Center, 0, Start, Start.AddSeconds(7)), Start);
        e.StatusChanged(100, 1958, true, Start.AddSeconds(.1), expiresAt: Start.AddSeconds(2));
        Check(!e.Hints(Center).Contains("Aim for safe spot!"), "Non-first half caster entered march danger.");
        e.Finish(10, 26508);
        Check(e.Hints(Center).Contains("Aim for safe spot!"), "Promoted first half caster missing from march danger.");
    }
}
