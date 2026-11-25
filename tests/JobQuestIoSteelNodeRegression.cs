using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static partial class JobQuestRegression
{
    private static (DungeonEncounter Encounter, DungeonActor Boss, DungeonActor Player) IoSteelNode(uint territory)
    {
        var definition = JobQuestCatalog.Create(territory).Single();
        var center = definition.Arena(Vector3.Zero).Position;
        DungeonActor boss = new(1, definition.BossOid, center, InCombat: true, IsEnemy: true, ObjectIndex: 2, AggroPlayer: true);
        DungeonActor player = new(100, 0, center, PartySlot: 0, Player: true);
        var encounter = new DungeonEncounter(territory);
        encounter.CurrentPartyMembers = [new(0, "", 100)];
        encounter.Synchronize([boss, player], center, Start, playerId: 100);
        return (encounter, boss, player);
    }

    private static void IoCaution()
    {
        var (e, boss, player) = IoSteelNode(648);
        var expiry = Start.AddSeconds(10);
        player = player with { Statuses = [1269], StatusExpirations = new Dictionary<uint, DateTime> { [1269] = expiry } };
        void Sync(double t) => e.Synchronize([boss, player], player.Position, Start.AddSeconds(t), playerId: 100);
        Sync(0);
        Check(!e.RequireAllStop && e.Hints(player.Position).Contains("Stop everything!"), "Io caution stopped too early or omitted early text.");
        e.Tick(Start.AddSeconds(9));
        Check(!e.RequireAllStop, "Io caution used an inclusive rather than strict one-second threshold.");
        e.Tick(Start.AddSeconds(9.001));
        Check(e.RequireAllStop, "Io caution failed to enter control window.");
        e.Tick(Start.AddSeconds(20));
        Check(e.RequireAllStop, "Io caution timed out before actual status loss.");
        e.StatusChanged(100, 1269, false, Start.AddSeconds(20));
        Check(!e.RequireAllStop, "Io status loss retained hold.");
        e.Tick(Start.AddSeconds(21));
        e.StatusChanged(100, 1269, true, Start.AddSeconds(21), expiresAt: Start.AddSeconds(21.5));
        Check(e.RequireAllStop, "Io Gain failed to restore caution.");
        player = player with { Dead = true };
        Sync(21.1);
        Check(!e.RequireAllStop, "Dead viewing character retained Io hold.");
        player = player with { Dead = false };
        Sync(21.2);
        Check(!e.RequireAllStop, "Revived actor's unchanged status recreated a cleared StayMove state.");
        e.RemoveActor(100);
        Sync(21.3);
        Check(e.RequireAllStop, "Destroyed and recreated player inherited the previous generation's death suppression.");
        player = player with { Dead = true };
        Sync(21.4);
        e.Synchronize([boss], player.Position, Start.AddSeconds(21.5), playerId: 100);
        player = player with { Dead = false };
        Sync(21.6);
        Check(e.RequireAllStop, "Missing actor retained previous generation's death suppression.");
        e.StatusChanged(100, 1269, true, Start.AddSeconds(22), expiresAt: Start.AddSeconds(22.1));
        e.Synchronize([boss, player, new(101, 0, player.Position, PartySlot: 1, Player: true)],
            player.Position, Start.AddSeconds(22), playerId: 101);
        Check(!e.RequireAllStop, "Viewing another actor inherited old caution.");
        e.ResetFight();
        Check(!e.RequireAllStop, "Io reset retained control.");
    }

    private static void IoFire()
    {
        var (e, boss, player) = IoSteelNode(648);
        DungeonActor helper = new(2, 0x1C00, boss.Position,
            Cast: new(2175, boss.Position + Vector3.UnitX * 8, 0, Start, Start.AddSeconds(3)));
        e.Synchronize([boss, player, helper], player.Position, Start, playerId: 100);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("trial/voidzone/")).Shape is CircleTemplate { Radius: 5 },
            "Io Fire II cast prediction missing.");
        var corrected = boss.Position - Vector3.UnitX * 8;
        e.StartCast(2, new(2175, corrected, 0, Start, Start.AddSeconds(3)), Start, fromPacket: true);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("trial/voidzone/")).Position == corrected,
            "Io packet correction was overwritten by the previous observed cast.");
        e.StartCast(2, new(2175, corrected, 0, Start.AddSeconds(1), Start.AddSeconds(4)), Start.AddSeconds(1), fromPacket: true);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("trial/voidzone/")).Position == corrected,
            "Io new packet generation was overwritten by the previous observed cast.");
        e.Effect(2, 2175, 1, Start.AddSeconds(3), location: boss.Position + Vector3.UnitX * 8);
        e.Effect(2, 2175, 2, Start.AddSeconds(3), location: boss.Position + Vector3.UnitX * 8);
        e.Finish(2, 2175, Start.AddSeconds(3));
        DungeonActor fire = new(3, 0x1EA4C6, boss.Position + Vector3.UnitX * 8, Dead: true, Targetable: false);
        e.Synchronize([boss, player, fire], player.Position, Start.AddSeconds(50), playerId: 100);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("trial/voidzone/event/")) == 1
            && e.AmbientAoes().Single(a => a.Id.StartsWith("trial/voidzone/live/")).Shape is CircleTemplate { Radius: 5 },
            "Io live fire failed one-per-update transfer or incorrectly filtered death/targetable.");
        Check(e.Hints(fire.Position).Contains("GTFO from voidzone!"), "Io persistent fire omitted its danger hint.");
        e.Synchronize([boss, player, fire], player.Position, Start.AddSeconds(51), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("trial/voidzone/event/")), "Io second prediction failed transfer.");
        e.Synchronize([boss, player, fire with { EventState = 7 }], player.Position, Start.AddSeconds(52), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("trial/voidzone/")), "Io Fire EventState7 not cleared.");
    }

    private static void NodeExplosives()
    {
        var (e, boss, player) = IoSteelNode(702);
        DungeonActor indicator = new(2, 0x1CD7, boss.Position + Vector3.UnitX * 8, Dead: true, Targetable: false);
        void Sync(double t, params DungeonActor[] extra) => e.Synchronize(new[] { boss, player }.Concat(extra), player.Position, Start.AddSeconds(t), playerId: 100);
        Sync(1, indicator);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("node/")).Activation == Start.AddSeconds(4),
            "Indicator dead/targetable filter or creation clock.");
        indicator = indicator with { Position = boss.Position + Vector3.UnitX * 10 };
        Sync(2, indicator);
        DungeonActor bomb = new(3, 0x1CD5, indicator.Position + new Vector3(1, 99, 1));
        Sync(3, indicator, bomb);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("node/")) is { } primed && primed.Position == bomb.Position
            && primed.Activation == Start.AddSeconds(4), "Bomb per-axis exchange lost indicator activation.");
        Sync(4, indicator, bomb with { Dead = true });
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("node/")), "Dead primed bomb remained a usable AOE.");
        Sync(50, indicator, bomb);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("node/")) == 1, "Primed death deleted history or time expired it.");
        e.RemoveActor(3);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("node/")), "Bomb destruction did not remove first position match.");
        Sync(51, indicator);
        e.ObjectCreated(4, Start.AddSeconds(51), 0x1CD7, indicator.Position);
        e.RemoveActor(4);
        e.ObjectCreated(4, Start.AddSeconds(51), 0x1CD7, boss.Position - Vector3.UnitX * 8);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("node/")) == 2,
            "Destroyed indicator reference was dropped or rebound on same-ID recreation.");
        var (reverse, reverseBoss, reversePlayer) = IoSteelNode(702);
        reverse.Synchronize([reverseBoss, reversePlayer, bomb, indicator], reversePlayer.Position, Start, playerId: 100);
        Check(reverse.AmbientAoes().Single(a => a.Id.StartsWith("node/")).Position == indicator.Position,
            "Bootstrap reordered bomb before indicator into an invented primed record.");
        var (moving, movingBoss, movingPlayer) = IoSteelNode(702);
        indicator = indicator with { Position = movingBoss.Position };
        bomb = bomb with { Position = movingBoss.Position + Vector3.UnitX * 2 };
        moving.Synchronize([movingBoss, movingPlayer, indicator, bomb], movingPlayer.Position, Start, playerId: 100);
        moving.Synchronize([movingBoss, movingPlayer, indicator with { Position = bomb.Position }], movingPlayer.Position,
            Start.AddSeconds(1), playerId: 100);
        Check(!moving.AmbientAoes().Any(a => a.Id.StartsWith("node/")),
            "Node bomb destruction matched before updating surviving indicator position.");
    }

    private static void NodeTargets()
    {
        var (e, boss, player) = IoSteelNode(702);
        boss = boss with { Statuses = [325] };
        DungeonActor arbitrary = new(2, 0xFFFF, boss.Position, IsEnemy: true, ObjectIndex: 4);
        e.Synchronize([boss, player, arbitrary], player.Position, Start, playerId: 100);
        Check(e.PriorityTarget(1, player.Position) == 2 && e.ForbiddenTargets.Length == 0, "Node only prioritized its four Adds or forbade AOE.");
        e.StatusChanged(2, 325, true, Start);
        Check(e.PriorityTarget(1, player.Position) == 0 && e.TargetToClear(0) == 1 && !e.RequireAllStop, "Node empty target released selector or held healing.");
        e.StatusChanged(2, 325, false, Start);
        e.Synchronize([boss, player, arbitrary with { Hp = 1 }], player.Position, Start, playerId: 100);
        Check(e.PriorityTarget(1, player.Position) == 0, "Node override replaced source PendingDead -1.");
        e.Synchronize([boss, player, arbitrary with { Hp = 1, NameId = 541 }], player.Position, Start, playerId: 100);
        Check(e.PriorityTarget(1, player.Position) == 2, "Node striking dummy exception.");
    }

    private static void SteelSwords()
    {
        var (e, boss, player) = IoSteelNode(866);
        DungeonActor sword = new(2, 0x2A46, boss.Position + Vector3.UnitX * 10, Rotation: .7f);
        var order = new DungeonCastObservation(17568, boss.Position, 0, Start, Start.AddSeconds(4));
        e.Synchronize([boss with { Cast = order }, player, sword], player.Position, Start, playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("steel/sword/")), "Steel bootstrap moved later sword creation ahead of the primary's existing cast.");
        e.Finish(1, 17568, Start);
        order = order with { StartedAt = Start.AddSeconds(1), FinishAt = Start.AddSeconds(5) };
        e.Synchronize([boss with { Cast = order }, player, sword], player.Position, Start.AddSeconds(1), playerId: 100);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("steel/sword/")).Activation == order.FinishAt,
            "Steel next Order failed to use the current primary cast clock.");
        sword = sword with { Position = sword.Position + Vector3.UnitZ * 2, Dead = true, Targetable = false };
        e.Synchronize([boss, player, sword], player.Position, Start.AddSeconds(5), playerId: 100);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("steel/sword/")) is { } aoe
            && aoe.Position == sword.Position && aoe.Rotation == .7f && aoe.Activation == default(DateTime),
            "Steel cancellation/death/targetability or dynamic clock changed sword contract.");
        e.RemoveActor(2);
        e.Synchronize([boss, player, sword with { Position = boss.Position - Vector3.UnitX * 10, Dead = false }],
            player.Position, Start.AddSeconds(6), playerId: 100);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("steel/sword/")) == 2, "Steel old same-ID sword reference was rebound.");
        e.Effect(2, 17567, 1, Start.AddSeconds(6));
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("steel/sword/")) == 1, "Steel effect cleared frozen old generation.");
        e.Effect(999, 17567, 2, Start.AddSeconds(6));
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("steel/sword/")) == 1, "Unknown caster effect removed an unrelated cached sword.");
        e.Synchronize([boss with { Dead = true }, player], player.Position, Start.AddSeconds(7), playerId: 100);
        Check(!e.AmbientAoes().Any() && e.Active == null, "Steel primary death waited for destroyed swords.");
        var (packet, packetBoss, packetPlayer) = IoSteelNode(866);
        packet.Synchronize([packetBoss, packetPlayer, sword], packetPlayer.Position, Start, playerId: 100);
        packet.StartCast(1, order, Start.AddSeconds(1), fromPacket: true);
        Check(packet.AmbientAoes().Single(a => a.Id.StartsWith("steel/sword/")).Activation == order.FinishAt,
            "Steel packet start activated swords without updating the primary cast clock.");
        packet.Finish(1, 17568, Start.AddSeconds(2));
        Check(packet.AmbientAoes().Single(a => a.Id.StartsWith("steel/sword/")).Activation == default(DateTime),
            "Steel packet cancellation retained the primary's obsolete cast clock.");
        packet.StartCast(1, new(99999, packetBoss.Position, 0, Start.AddSeconds(3), Start.AddSeconds(8)), Start.AddSeconds(3));
        packet.Finish(1, 17568, Start.AddSeconds(4));
        Check(packet.AmbientAoes().Single(a => a.Id.StartsWith("steel/sword/")).Activation == Start.AddSeconds(8),
            "Steel ignored an unclaimed primary cast or stale finish cleared its clock.");
        packet.Finish(1, 99999, Start.AddSeconds(5));
        Check(packet.AmbientAoes().Single(a => a.Id.StartsWith("steel/sword/")).Activation == default(DateTime),
            "Steel unclaimed current cast failed packet cancellation.");
    }

    private static void SteelBolts()
    {
        var (e, boss, player) = IoSteelNode(866);
        for (ulong i = 2; i < 7; ++i)
        {
            e.Synchronize(new[] { boss, player }.Concat(Enumerable.Range(2, 5).Select(j => new DungeonActor((ulong)j, 0x233C, boss.Position))),
                player.Position, Start, playerId: 100);
            e.StartCast(i, new(17561, boss.Position, .8f, Start, Start.AddSeconds(10 - (int)i)), Start);
        }
        var bolts = e.AmbientAoes().Where(a => a.Id.StartsWith("steel/bolt/")).ToArray();
        Check(bolts.Length == 4 && bolts.All(a => a.Rotation == .8f), "Steel CaptiveBolt count or facing.");
        e.Finish(2, 17561, Start.AddSeconds(1), completed: false);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("steel/bolt/")) == 4, "Fifth CaptiveBolt did not promote after cancellation.");
        e.StartCast(2, new(17563, boss.Position, .9f, Start.AddSeconds(2), Start.AddSeconds(7)), Start.AddSeconds(2));
        Check(e.OutputCasts.Single(c => c.Value.Rule.Action == 17563).Value is { Rotation: .9f, Rule.Shape: MasterPlayer.Mechanics.PilgrimsTraverse.PtDonutSectorTemplate },
            "Steel independent half ring lost orientation or shape.");
        Check(!e.Casts.Values.Any(c => c.Rule.Action == 17567), "No-cast sword effect was invented as cast.");
    }
}
