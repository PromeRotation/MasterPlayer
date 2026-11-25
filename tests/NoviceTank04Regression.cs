using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;

internal static class NoviceTank04Regression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static DungeonActor Dummy(ulong id, Vector3 position = default) => new(id, 0x1564, position, Hp: 1000,
        IsEnemy: true, ObjectIndex: (ushort)(id * 2), NameId: 541);
    private static DungeonActor Player(Vector3 position = default, uint job = 21) => new(100, 0, position,
        Hostile: false, Player: true, PartySlot: 0, ClassJob: job);
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native novice Tank04 exact territory targetable-only pull primary completion and owner boundaries", Lifecycle);
        run("native novice Tank04 all world dummies exact three bbox center weak goal and no navigation", Goal);
        run("native novice Tank04 strict XZ range null target class spell priority and local action intent clear only", Actions);
        run("native novice Tank04 forbidden legal targets pointless setter dummy exception no full action hold", Forbidden);
        run("native novice Tank04 bound primary cannot skip untargetable first reverse pending promotion next tick", Primary);
    }
    private static void Lifecycle()
    {
        var e = new DungeonEncounter(540);
        e.Synchronize([Dummy(1) with { Targetable = false }, Player()], default(Vector3), Start, playerId: 100);
        Check(e.Active == null && e.AmbientAoes().All(a => !a.Navigate), "Untargetable primary pulled or precombat navigated.");
        e.Synchronize([Dummy(1), Player()], default(Vector3), Start.AddSeconds(1), playerId: 100);
        Check(e.Active?.Name == "NoviceTank04" && !e.Actors[1].InCombat
            && e.Arena!.Contains(new(20, 0, 0), 0) && !e.Arena.Contains(new(20.001f, 0, 0), 0),
            "Targetable-only pull or R20 arena differs.");
        e.Synchronize([Dummy(1) with { Hp = 0 }, Player()], default(Vector3), Start.AddSeconds(2), playerId: 100);
        e.Synchronize([Dummy(1), Player()], default(Vector3), Start.AddSeconds(3), playerId: 100);
        Check(e.Active == null, "Completed primary revived as same generation.");
        e.RemoveActor(1);
        e.Synchronize([Dummy(1), Player()], default(Vector3), Start.AddSeconds(4), playerId: 100);
        Check(e.Active != null, "New dummy generation stayed blocked.");
        e.Synchronize([Dummy(1), Player()], default(Vector3), Start.AddSeconds(5), _ => false, 100);
        Check(e.Active == null && e.ForbiddenTargets.Length == 0 && e.NoviceTank04 == null, "Disabled training retained model/controls.");
    }
    private static void Goal()
    {
        var e = new DungeonEncounter(540);
        var dummies = new[] { Dummy(1, new(-4, 10, -3)), Dummy(2, new(6, 50, 7)) with { Dead = true },
            Dummy(3, new(2, -10, 1)) with { Targetable = false } };
        e.Synchronize([.. dummies, Player(new(5, 200, 5))], new Vector3(5, 200, 5), Start, playerId: 100);
        var plan = e.NoviceTank04;
        Check(plan != null && plan.Dummies.Count == 3 && plan.Center == new Vector3(1, 200, 2)
            && plan.GoalRadius == 1 && plan.GoalWeight == 1 && !plan.RequestsAction,
            "All-actor bbox, exact count, current height or weak goal weight differs.");
        Check(e.AmbientAoes().All(a => !a.Navigate) && !e.Casts.Any(), "Discarded weight1 goal became navigation or an AOE.");
        e.Synchronize([.. dummies, Dummy(4), Player()], default(Vector3), Start.AddSeconds(1), playerId: 100);
        Check(e.NoviceTank04 == null && e.ForbiddenTargets.Length == 2, "Count4 retained goal or stopped forbidding eligible dummies.");
    }
    private static void Actions()
    {
        var e = new DungeonEncounter(540);
        var dummies = new[] { Dummy(1, new(-2, 0, 0)), Dummy(2), Dummy(3, new(2, 0, 0)) };
        e.Synchronize([.. dummies, Player(new(1, 500, 0))], new Vector3(1, 500, 0), Start, playerId: 100);
        Check(e.NoviceTank04 is { RequestsAction: false, ClearsActionIntents: false }, "Action triggered at XZ distance equality1.");
        var step = 0;
        foreach (var (job, action) in new (uint, uint)[] { (3, 41), (21, 41), (1, 7381), (19, 7381), (32, 3621), (40, 11) })
        {
            e.Synchronize([.. dummies, Player(new(.999f, 500, 0), job)], new Vector3(.999f, 500, 0),
                Start.AddSeconds(++step), playerId: 100);
            var plan = e.NoviceTank04!;
            Check(plan.RequestsAction && plan.ClearsActionIntents && plan.Action == action
                && plan.ActionTarget == null && plan.ActionPriority == 1,
                "Source class action/target/priority or local intent Clear differs.");
        }
    }
    private static void Forbidden()
    {
        var e = new DungeonEncounter(540);
        var secondary = Dummy(2) with { NameId = 0, Hp = 100,
            IncomingEffects = [new ReplayIncomingEffect(0, 1, 0, false, false) { HpDelta = -100 }] };
        var other = Dummy(3) with { Oid = 0xAAAA, AggroPlayer = true };
        e.Synchronize([Dummy(1), secondary, other, Player()], default(Vector3), Start, playerId: 100);
        Check(e.ForbiddenTargets.SequenceEqual(new ulong[] { 1 }) && e.PriorityTarget(1, default) == 3,
            "Forbidden setter bypassed Pointless or forbade unrelated target.");
        e.Synchronize([Dummy(1), secondary with { NameId = 541 }, other, Player()], default(Vector3),
            Start.AddSeconds(1), playerId: 100);
        Check(e.ForbiddenTargets.Order().SequenceEqual(new ulong[] { 1, 2 }), "Dummy NameID541 exception lost before forbidden setter.");
    }
    private static void Primary()
    {
        var e = new DungeonEncounter(540);
        e.Synchronize([Dummy(1) with { Targetable = false }, Dummy(2), Dummy(3), Player()], default(Vector3), Start, playerId: 100);
        Check(e.Active == null && e.ForbiddenTargets.Length == 0 && e.NoviceTank04 == null,
            "Skipped the loaded untargetable first dummy.");
        e.Synchronize([Dummy(3), Dummy(2), Dummy(1), Player()], default(Vector3), Start.AddSeconds(1), playerId: 100);
        Check(e.Active != null && e.PrimaryId == 1, "Snapshot reorder rebound the loaded primary.");
        e.RemoveActor(1);
        e.Synchronize([Dummy(2), Dummy(3), Player()], default(Vector3), Start.AddSeconds(2), playerId: 100);
        Check(e.Active == null && e.ForbiddenTargets.Length == 0, "Promoted pending before destroyed primary was unloaded.");
        e.Synchronize([Dummy(2), Dummy(3), Player()], default(Vector3), Start.AddSeconds(3), playerId: 100);
        Check(e.Active != null && e.PrimaryId == 3, "Pending promotion did not use reverse insertion order.");
        e.Synchronize([Dummy(2), Dummy(3) with { Hp = 0 }, Player()], default(Vector3), Start.AddSeconds(4), playerId: 100);
        Check(e.Active == null, "Completed primary promoted in the same update.");
        e.Synchronize([Dummy(2), Dummy(3) with { Hp = 0 }, Player()], default(Vector3), Start.AddSeconds(5), playerId: 100);
        Check(e.Active != null && e.PrimaryId == 2, "Remaining pending module did not activate.");
        e.Synchronize([Dummy(2), Dummy(3) with { Hp = 0 }, Dummy(4), Player()], default(Vector3), Start.AddSeconds(6), playerId: 100);
        e.RemoveActor(2);
        e.Synchronize([Dummy(2) with { Oid = 0x233C }, Dummy(4), Player()], default(Vector3), Start.AddSeconds(7), playerId: 100);
        Check(e.Active == null, "Cross-OID reused primary was not unloaded.");
        e.Synchronize([Dummy(2) with { Oid = 0x233C }, Dummy(4), Player()], default(Vector3), Start.AddSeconds(8), playerId: 100);
        Check(e.Active != null && e.PrimaryId == 4, "Cross-OID ID reuse blocked pending primary.");
    }
}
