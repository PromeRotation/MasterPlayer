using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class NoviceTrainingRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static DungeonActor Enemy(ulong id, uint oid = 0x1571, Vector3 position = default, bool combat = true)
        => new(id, oid, position, InCombat: combat, Hp: 1000, IsEnemy: true, ObjectIndex: (ushort)(id * 2), AggroPlayer: combat);
    private static DungeonActor Player(ulong target = 1) => new(100, 0, default, Hostile: false, Player: true, TargetId: target, PartySlot: 0);
    private static DungeonActor Gladiator(ulong target, ulong id = 20)
        => new(id, 0x1574, default, Hostile: false, IsEnemy: false, TargetId: target);
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native novice exact CFC territories ownership arenas and precombat display without navigation", Precombat);
        run("native novice first gladiator target partial setter unresolved target warning no forbidden attacks", Directed);
        run("native novice reinforcements all OIDs baseline fallback pending dead legal candidate bounds", Reinforcements);
        run("native novice final all matching OIDs setter pointless protection height order and baseline fallback", Final);
        run("native novice pull latch out of combat untargetable HP0 death completion and new generation", Lifecycle);
        run("native novice disabled owner and player death preserve encounter without action movement output", Controls);
        run("native novice friendly NPC source baseline and active outside primary exact circle projection", NpcAndProjection);
        run("native novice predicted HP dummy exception source priority and pending healing", PredictedHp);
    }
    private static void PredictedHp()
    {
        var e = new DungeonEncounter(547);
        var boss = Enemy(1, 0x157A);
        var reinforcement = Enemy(2, 0xAAAA) with
        {
            IncomingEffects = [new(0, 1, 0, false, false) { HpDelta = -999 }]
        };
        e.Synchronize([boss, reinforcement, Player()], Vector3.Zero, Start, playerId: 100);
        Check(e.PriorityTarget(2, default) == 1, "Pending lethal damage was overwritten by training setter.");
        e.Synchronize([boss, reinforcement with { NameId = 541 }, Player()], Vector3.Zero, Start.AddSeconds(1), playerId: 100);
        Check(e.PriorityTarget(2, default) == 2, "Dummy exception lost before training setter.");
        reinforcement = reinforcement with { Hp = 1, IncomingEffects = [new(0, 1, 0, false, false) { HpDelta = 10 }] };
        e.Synchronize([boss, reinforcement, Player()], Vector3.Zero, Start.AddSeconds(2), playerId: 100);
        Check(e.PriorityTarget(1, default) == 2, "Pending healing could not restore training preferred target.");
    }
    private static void NpcAndProjection()
    {
        var e = new DungeonEncounter(547);
        var boss = Enemy(1, 0x157A) with { AggroPlayer = false, TargetId = 20 };
        e.Synchronize([boss, Player(), Gladiator(1) with { FriendlyNpc = true }], Vector3.Zero, Start, playerId: 100);
        Check(e.Actors[20].PartySlot == 24 && e.PriorityTarget(1, default) == 1,
            "Boss attacking allied training NPC lost the source baseline priority.");
        e.Synchronize([boss with { Statuses = [325] }, Player(), Gladiator(1) with { FriendlyNpc = true }],
            Vector3.Zero, Start.AddSeconds(1), playerId: 100);
        Check(e.PriorityTarget(1, default) == 1, "Active source module incorrectly inherited automatic invincibility filtering.");
        var direction = new Vector3(MathF.Sin(MathF.PI / 64), 0, MathF.Cos(MathF.PI / 64));
        foreach (var (territory, oid, center) in new[] { (545u, 0x1571u, Vector3.Zero), (547u, 0x157Au, Vector3.Zero),
            (552u, 0x1591u, new Vector3(457.383f, 0, 271.721f)) })
        {
            e = new DungeonEncounter(territory);
            e.Synchronize([Enemy(1, oid, center + direction * 30), Player()], center, Start, playerId: 100);
            var marker = e.AmbientAoes().Single(a => a.Id.Contains("/enemy/"));
            Check(Vector3.Distance(marker.Position, center + direction * 20) < .0001f && marker.OutlineOnly,
                "Active Primary clamped to polygonal contour rather than exact circle.");
        }
    }
    private static void Precombat()
    {
        foreach (var (territory, oid, center) in new[] { (545u, 0x1571u, Vector3.Zero),
            (547u, 0x157Au, Vector3.Zero), (552u, 0x1591u, new Vector3(457.383f, 0, 271.721f)) })
        {
            Check(NoviceCatalog.Owns(territory, oid) && !NoviceCatalog.Owns(territory, 0x1574), "Ownership expanded beyond source primary.");
            var e = new DungeonEncounter(territory);
            e.Synchronize([Enemy(1, oid, center + new Vector3(30, 0, 0), false), Player()], center, Start, playerId: 100);
            var previews = e.AmbientAoes().ToArray();
            Check(e.Active == null && e.Arena == null && previews.Length == 2 && previews.All(a => !a.Navigate)
                && previews[0].Shape is CircleTemplate { Radius: 20 } && previews[0].Position == center
                && previews[1].Position == center + new Vector3(20, 0, 0) && previews[1].OutlineOnly,
                "Precombat arena/primary projection was lost or became navigation.");
            Check(e.PriorityTarget(1, center) == 0, "Precombat priority activated before pull.");
        }
    }
    private static void Directed()
    {
        var e = new DungeonEncounter(545);
        e.Synchronize([Enemy(1), Enemy(2, 0x9999, combat: false), Player(), Gladiator(2), Gladiator(1, 21)], Vector3.Zero, Start, playerId: 100);
        Check(e.PriorityTarget(1, default) == 2 && e.Hints(Vector3.Zero).Contains("Attacking invincible target!")
            && e.ForbiddenTargets.Length == 0, "Directed target setter/text incorrectly became forbidden attacks.");
        e.Synchronize([Enemy(1), Enemy(2), Player(), Gladiator(1, 21), Gladiator(2)],
            Vector3.Zero, Start.AddSeconds(.5), playerId: 100);
        Check(e.PriorityTarget(1, default) == 2 && e.Hints(Vector3.Zero).Contains("Attacking invincible target!"),
            "Snapshot reorder replaced the first persistent gladiator.");
        e.Synchronize([Enemy(1), Enemy(2), Player(), Gladiator(9999), Gladiator(2, 21)], Vector3.Zero, Start.AddSeconds(1), playerId: 100);
        Check(e.PriorityTarget(1, default) == 1 && e.Hints(Vector3.Zero).Contains("Attacking invincible target!"),
            "Unresolved first target chose another gladiator or lost warning.");
        e.Synchronize([Enemy(1), Enemy(2), Player(), Gladiator(0), Gladiator(2, 21)], Vector3.Zero, Start.AddSeconds(2), playerId: 100);
        Check(e.PriorityTarget(1, default) == 1 && !e.Hints(Vector3.Zero).Contains("Attacking invincible target!"), "Zero directed target retained override.");
        e.RemoveActor(21);
        e.RemoveActor(20);
        e.Synchronize([Enemy(1), Enemy(2), Player(), Gladiator(2, 30), Gladiator(1, 31)], Vector3.Zero, Start.AddSeconds(3), playerId: 100);
        Check(e.PriorityTarget(2, default) == 1,
            "Packet removal order changed source frame-order dictionary slot reuse.");
    }
    private static void Reinforcements()
    {
        var e = new DungeonEncounter(547);
        e.Synchronize([Enemy(1, 0x157A), Enemy(2, 0xABCDEF, combat: false), Enemy(3, 0xAAAA) with { IsEnemy = false }, Player()],
            Vector3.Zero, Start, playerId: 100);
        Check(e.PriorityTarget(1, default) == 2, $"Unknown-OID reinforcements lost priority or friend became enemy: active={e.Active?.Name}, selected={e.PriorityTarget(1, default)}.");
        e.Synchronize([Enemy(1, 0x157A), Enemy(2, 0xABCDEF) with { Hp = 1 }, Player()], Vector3.Zero, Start.AddSeconds(1), playerId: 100);
        Check(e.PriorityTarget(2, default) == 1, "Pointless reinforcement revived by setter or baseline boss lost.");
        e.Synchronize([Enemy(1, 0x157A), Enemy(2, 0xABCDEF) with { ObjectIndex = 199 }, Player()], Vector3.Zero, Start.AddSeconds(2), playerId: 100);
        Check(e.PriorityTarget(2, default) == 1, "Illegal native object slot admitted.");
    }
    private static void Final()
    {
        var e = new DungeonEncounter(552);
        var center = new Vector3(457.383f, 0, 271.721f);
        var boss = Enemy(1, 0x1591, center);
        e.Synchronize([boss, Enemy(2, 0x1591, center, false), Enemy(3, 0xAAAA, center), Player()], center, Start, playerId: 100);
        Check(e.PriorityTarget(2, center) == 2 && e.Arena?.Position == center, "All matching OIDs not raised equally or center changed.");
        e.Synchronize([boss, Enemy(2, 0x1591, center) with { Hp = 1 }, Player()], center, Start.AddSeconds(1), playerId: 100);
        Check(e.PriorityTarget(2, center) == 1, "Final setter revived Pointless.");
        e.Synchronize([boss, Enemy(2, 0x1591, center + new Vector3(0, 13, 0)) with { Hp = 1 }, Player()], center, Start.AddSeconds(2), playerId: 100);
        Check(e.PriorityTarget(2, center) == 2, "Source height-before-Pointless setter order differs.");
    }
    private static void Lifecycle()
    {
        var initial = new DungeonEncounter(547);
        var initialBoss = Enemy(1, 0x157A);
        initial.Synchronize([initialBoss with { Hp = 0 }, Player()], Vector3.Zero, Start, playerId: 100);
        initial.Synchronize([initialBoss, Player()], Vector3.Zero, Start.AddSeconds(1), playerId: 100);
        Check(initial.Active == null && !initial.AmbientAoes().Any(), "Initially HP0 pulled generation restarted after healing.");
        foreach (var dead in new[] { false, true })
        {
            var e = new DungeonEncounter(547);
            var boss = Enemy(1, 0x157A);
            e.Synchronize([boss, Player()], Vector3.Zero, Start, playerId: 100);
            e.Synchronize([boss with { InCombat = false, Targetable = false }, Player()], Vector3.Zero, Start.AddSeconds(1), playerId: 100);
            Check(e.Active != null && !e.AmbientAoes().Any(a => a.Id.Contains("/enemy/")), "Unselectable or deaggro prematurely ended phase.");
            e.Synchronize([boss with { Hp = dead ? 1000u : 0u, Dead = dead }, Player()], Vector3.Zero, Start.AddSeconds(2), playerId: 100);
            e.Synchronize([boss, Player()], Vector3.Zero, Start.AddSeconds(3), playerId: 100);
            Check(e.Active == null && !e.AmbientAoes().Any(), "Ended old generation restarted after resurrection.");
            e.RemoveActor(1);
            e.Synchronize([boss, Player()], Vector3.Zero, Start.AddSeconds(4), playerId: 100);
            Check(e.Active != null, "Actual Remove/Create same-ID new generation stayed blocked.");
        }
    }
    private static void Controls()
    {
        var e = new DungeonEncounter(545);
        e.Synchronize([Enemy(1), Enemy(2), Player(), Gladiator(2)], Vector3.Zero, Start, playerId: 100);
        e.Synchronize([Enemy(1), Enemy(2), Player() with { Dead = true }, Gladiator(2)], null, Start.AddSeconds(1), playerId: 100);
        Check(e.Active != null && !e.RequireAllStop && e.ForbiddenTargets.Length == 0, "Player death ended source encounter or invented restrictions.");
        e.Synchronize([Enemy(1), Player()], Vector3.Zero, Start.AddSeconds(2), _ => false, 100);
        Check(e.Active == null && e.Arena == null && !e.AmbientAoes().Any() && e.PriorityTarget(1, default) == 0,
            "Disabled mechanism retained display/navigation/selection.");
    }
}
