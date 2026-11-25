using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleBombPlantRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);

    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale bomb plant exact four ownerships actual component sets and ignored cast scope", Ownership);
        run("Carnivale Stage11 both acts group pull all dead or destroyed HP0 and all enemy markers", BombGroups);
        run("Carnivale Stage12 Act1 default primary completion independent of same OID and all enemy display", SmallPlants);
        run("Carnivale Stage12 Act2 two exact cones lock cast location rotation and actual finish", Cones);
        run("Carnivale Stage12 Act2 spore sac is a pure observed cast hint with no AOE or damage", Spore);
        run("Carnivale Stage12 Act2 fumes carry only stun intent independent of enemy projection and real finish", Stun);
        run("Carnivale Stage12 Act2 roselet object colour all candidates priority and pointless preservation", MarkersAndPriority);
        run("Carnivale bomb plant production adapter separates previews arena cones and stun owner cleanup", Output);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(uint oid, bool combat = false)
        {
            Actors =
            [
                new(1, oid, Center, InCombat: combat, Name: "Named Plant", Hp: 1000, IsEnemy: true, ObjectIndex: 2),
                new(100, 0, Center, PartySlot: 0, Player: true),
            ];
            Sync();
        }
        internal void Sync() => E.Synchronize(Actors, Center, Now, playerId: 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = change(Actors[i]);
        }
        internal void Raw(uint action, Vector3? position = null, float rotation = 0)
        {
            Set(1, a => a with { Cast = new(action, position ?? Center, rotation, Start, Start.AddSeconds(15), 100) });
            Sync();
        }
    }

    private static void Ownership()
    {
        foreach (var oid in new uint[] { 0x2718, 0x2719, 0x271A, 0x271B })
        {
            var f = new Fixture(oid);
            Check(CarnivaleCatalog.Owns(796, oid) && !CarnivaleCatalog.Owns(795, oid)
                && f.E.CarnivalePrecombatHint != null && f.E.Active == null, "Ownership/preparation.");
            f.Set(1, a => a with { InCombat = true });
            f.Sync();
            Check(f.E.CarnivalePrecombatHint == null && !f.E.Hints(Center).Any(), "Preparation hint survived activation.");
            if (oid != 0x271B)
            {
                f.Raw(oid == 0x271A ? 14750u : 14583u);
                Check(f.E.Active!.Casts.Length == 0 && f.E.Casts.Count == 0 && !f.E.CarnivaleInterrupts.Any()
                    && !f.E.PredictedDamage.Any(), "Unactivated enumeration became a component.");
            }
            else
                Check(f.E.Active!.Casts.Select(c => c.Action).Order().SequenceEqual(new uint[] { 14751, 14752, 14753, 14754 }),
                    "Stage12 Act2 actual component set.");
        }
        Check(CarnivaleCatalog.IgnoredCast(0x2718, 14583) && CarnivaleCatalog.IgnoredCast(0x2719, 14583)
            && !CarnivaleCatalog.IgnoredCast(0x271A, 14583) && !CarnivaleCatalog.IgnoredCast(0x2718, 60000),
            "Ignored cast interception escaped its source modules.");
    }

    private static void BombGroups()
    {
        foreach (var oid in new uint[] { 0x2718, 0x2719 })
        {
            var f = new Fixture(oid);
            f.Set(1, a => a with { Targetable = false, Hp = 0 });
            f.Actors.Add(new(2, oid, Center + Vector3.UnitZ * 5, InCombat: true, Hp: 1000));
            f.Sync();
            Check(f.E.Active?.BossOid == oid && f.E.PrimaryId == 1, "Group pull or fixed primary.");
            f.Set(1, a => a with { Targetable = true });
            f.Sync();
            Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == 2, "All bomb markers.");
            f.Set(1, a => a with { Dead = true });
            f.Set(2, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active != null, "HP0 replaced the group Dead predicate.");
            f.Actors.RemoveAll(a => a.Id == 1);
            f.Sync();
            Check(f.E.Active != null, "Destroyed primary ended living sibling group.");
            f.Set(2, a => a with { Dead = true });
            f.Sync();
            Check(f.E.Active == null && !f.E.AmbientAoes().Any(), "Group completion leaked components.");
        }
    }

    private static void SmallPlants()
    {
        var f = new Fixture(0x271A);
        f.Actors.Add(new(2, 0x271A, Center + Vector3.UnitZ * 5, InCombat: true, Hp: 1000));
        f.Sync();
        Check(f.E.Active == null && f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == 2,
            "Act1 second plant pulled default primary or was not drawn.");
        f.Set(1, a => a with { InCombat = true, Targetable = false });
        f.Sync();
        Check(f.E.Active == null, "Untargetable default primary pulled.");
        f.Set(1, a => a with { Targetable = true });
        f.Sync();
        f.Set(1, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active == null && f.E.CarnivalePrecombatHint == null, "Default death phase waited for all plants.");
    }

    private static void Cones()
    {
        foreach (var (action, radius, angle) in new[] { (14751u, 16.96f, MathF.PI * 2 / 3), (14754u, 46.96f, MathF.PI / 3) })
        {
            var f = new Fixture(0x271B, true);
            var locked = Center + Vector3.UnitX * 3;
            f.E.StartCast(1, new(action, Center, 0, Start, Start.AddSeconds(15), 100), Start);
            f.Raw(action, locked, MathF.PI / 2);
            var cast = f.E.OutputCasts.Single().Value;
            Check(cast.Rule.Shape is FanTemplate fan && fan.Radius == radius && fan.AngleRadians == angle
                && cast.Location == locked && cast.Rotation == MathF.PI / 2, "Exact locked cone.");
            f.Set(1, a => a with { Position = Center + Vector3.UnitZ * 15, Rotation = -.7f });
            f.Sync();
            Check(f.E.OutputCasts.Single().Value.Location == locked, "Cone followed actor.");
            f.Now = Start.AddSeconds(20);
            f.Sync();
            Check(f.E.OutputCasts.Any(), "Predicted time ended cone.");
            f.Set(1, a => a with { Cast = null });
            f.Sync();
            Check(!f.E.OutputCasts.Any(), "Observed finish did not clear cone.");
        }
    }

    private static void Spore()
    {
        var f = new Fixture(0x271B);
        f.Raw(14752);
        Check(!f.E.Hints(Center).Any(h => h.StartsWith("Calls Roselets")), "Spore active pre-pull.");
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.Hints(Center).Contains("Calls Roselets. Prepare Ice Spikes if available.")
            && !f.E.CarnivaleInterrupts.Any() && !f.E.PredictedDamage.Any()
            && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null), "Spore became AOE, damage or interrupt.");
        f.Now = Start.AddSeconds(20);
        f.Sync();
        Check(f.E.Hints(Center).Any(h => h.StartsWith("Calls Roselets")), "Spore expired by prediction.");
        f.E.Finish(1, 14752, f.Now);
        Check(!f.E.Hints(Center).Any(h => h.StartsWith("Calls Roselets")), "Spore finish leaked.");
    }

    private static void Stun()
    {
        var f = new Fixture(0x271B, true);
        f.Raw(14753);
        var intent = f.E.CarnivaleInterrupts.Single();
        Check(intent.ShouldBeStunned && !intent.ShouldBeInterrupted
            && f.E.Hints(Center).Contains("Stun!") && !f.E.Hints(Center).Contains("Interrupt!"), "Stun became interrupt.");
        f.Set(1, a => a with { Hp = 1 });
        f.Sync();
        Check(f.E.CarnivaleInterrupts.Single().ShouldBeStunned, "Pointless lost stun flag.");
        f.Set(1, a => a with { Targetable = false });
        f.Sync();
        Check(!f.E.CarnivaleInterrupts.Single().ShouldBeStunned && f.E.Hints(Center).Contains("Stun!"),
            "Projection removed underlying caster or retained ineligible enemy flag.");
        f.E.Effect(1, 14753, 1, Start.AddSeconds(15));
        f.Now = Start.AddSeconds(20);
        f.Sync();
        Check(f.E.CarnivaleInterrupts.Any(), "Effect or deadline ended stun.");
        f.Set(1, a => a with { Cast = null });
        f.Sync();
        Check(!f.E.CarnivaleInterrupts.Any() && !f.E.Hints(Center).Contains("Stun!"), "Stun finish leaked.");
    }

    private static void MarkersAndPriority()
    {
        var f = new Fixture(0x271B, true);
        f.Actors.Add(new(10, 0x271C, Center + Vector3.UnitZ * 10, IsEnemy: true, ObjectIndex: 4, Hp: 1000));
        f.Actors.Add(new(11, 0x9999, Center, IsEnemy: true, ObjectIndex: 198, Hp: 1000));
        f.Sync();
        var markers = f.E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/enemy/")).ToArray();
        Check(markers.Length == 2 && markers.Single(a => a.Id.EndsWith("/10")).Color == new Vector4(1, 128f / 255, 0, 1)
            && markers.Single(a => a.Id.EndsWith("/1")).Color == new Vector4(1, 0, 0, 1), "Source object/enemy colours.");
        Check(f.E.PriorityTarget(11, Center) == 10, "Roselet priority.");
        f.Set(10, a => a with { Hp = 1 });
        f.Sync();
        Check(f.E.PriorityTarget(10, Center) != 10, "Pointless overridden.");
        f.Set(1, a => a with { Targetable = false });
        f.Set(10, a => a with { Targetable = false });
        f.Sync();
        Check(f.E.PriorityTarget(0, Center) == 11, "Other valid unenumerated candidates omitted.");
    }

    private static void Output()
    {
        foreach (var oid in new uint[] { 0x2718, 0x2719, 0x271A, 0x271B })
        {
            var f = new Fixture(oid);
            using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
            var draw = new ScriptDrawApi();
            var output = new DungeonOutput(safe, draw, safe.Manager);
            output.Apply(f.E, Start, false);
            Check(safe.Manager.BuildSnapshot(Start).ArenaBoundary == null
                && safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0, "Pre-pull navigation.");
            f.Set(1, a => a with { InCombat = true });
            f.Sync();
            if (oid == 0x271B) f.Raw(14751);
            output.Apply(f.E, Start, false);
            var snap = safe.Manager.BuildSnapshot(Start);
            Check(snap.ArenaBoundary != null && snap.DangerAreas.Count == (oid == 0x271B ? 1 : 0)
                && snap.GoalAreas.Count == 0, "Actual adapter source scope.");
            Check(f.E.Arena!.GetContours().Count() == (oid == 0x2719 ? 5 : 1), "Wrong layout selected.");
            output.Clear();
            Check(safe.Manager.BuildSnapshot(Start).ArenaBoundary == null, "Owner cleanup.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
