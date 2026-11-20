using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;

internal static class Pt99TargetRegression
{
    private static readonly DateTime Now = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-600, 0, -300);
    private static ReplayActorState Player(params uint[] buffs) => new()
    {
        Id = 100, Kind = 1, Position = Center, Hp = 1000,
        Statuses = buffs.Select(id => new ReplayStatus(id, 0, 0, Now.AddSeconds(30))).ToArray()
    };
    private static ReplayActorState Enemy(ulong id, uint oid, float x = 0) => new()
    {
        Id = id, EntityId = (uint)id, Oid = oid, Position = Center + new Vector3(x, 0, 0),
        Targetable = true, InCombat = true, Hp = 1000, MaxHp = 1000
    };
    private static Pt99Encounter Encounter(ReplayActorState player, params ReplayActorState[] adds)
    {
        var e = new Pt99Encounter();
        e.Synchronize([Enemy(1, 0x48EA), Enemy(2, 0x48EB), player, .. adds], Now);
        return e;
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void Run(Action<string, Action> run)
    {
        run("PT99 targets follow actual light/dark buffs, never requested drain colour", () =>
        {
            Check(Encounter(Player(4560)).SelectTarget(Now, 100, 2)?.Id == 1, "Light attacks Grief");
            var dark = Encounter(Player(4559));
            dark.StartCast(new(1, 44088, Center, 0, Now, Now.AddSeconds(7), 0));
            Check(dark.SelectTarget(Now, 100, 1)?.Id == 2, "A request to acquire light must not attack Grief before the buff changes");
            Check(Encounter(Player()).SelectTarget(Now, 100, 1) == null, "No colour withholds boss attack");
            Check(Encounter(Player(4559, 4560)).SelectTarget(Now, 100, 1) == null, "Ambiguous simultaneous colours withhold boss attack");
            Check(Encounter(Player(4560)).SelectTarget(Now.AddSeconds(31), 100, 1) == null, "Expired colour withholds boss attack");
        });
        run("PT99 adds override bosses and retain an eligible current add", () =>
        {
            var e = Encounter(Player(4560), Enemy(11, 0x48EC, 10), Enemy(12, 0x48EC, 2), Enemy(10, 0x48ED, 1));
            Check(e.SelectTarget(Now, 100, 1)?.Id == 12, "Choose nearest minion over either boss and fireball helper");
            Check(e.SelectTarget(Now, 100, 11)?.Id == 11, "Keep current valid minion to avoid FSD target churn");
            Check(Encounter(Player(), Enemy(11, 0x48EC)).SelectTarget(Now, 100, 1)?.Id == 11, "Adds remain attackable without a colour");
            Check(Encounter(Player(4560), Enemy(12, 0x48EC, -2), Enemy(11, 0x48EC, 2)).SelectTarget(Now, 100, 0)?.Id == 11,
                "Equal-distance minions use stable ID ordering");
        });
        run("PT99 dead, unselectable and one-HP targets are excluded", () =>
        {
            var e = Encounter(Player(4560), Enemy(11, 0x48EC) with { Dead = true },
                Enemy(12, 0x48EC) with { Targetable = false }, Enemy(13, 0x48EC) with { Hp = 1 });
            Check(e.SelectTarget(Now, 100, 11)?.Id == 1, "Return to matching boss after adds become invalid");
            e.Synchronize([Enemy(1, 0x48EA) with { Hp = 1 }, Enemy(2, 0x48EB), Player(4560)], Now.AddSeconds(1));
            Check(e.SelectTarget(Now.AddSeconds(1), 100, 1) == null, "Do not attack one-HP boss or wrong-colour counterpart");
            e.Synchronize([Enemy(1, 0x48EA), Enemy(2, 0x48EB), Player(4559)], Now.AddSeconds(2));
            Check(e.SelectTarget(Now.AddSeconds(2), 100, 1)?.Id == 2, "Switch after the actual buff changes");
        });
        run("PT99 target ownership stops outside combat, arena, or player lifetime", () =>
        {
            var e = Encounter(Player(4560));
            Check(e.SelectTarget(Now, 999, 1) == null, "Unknown player cannot select targets");
            Check(Encounter(Player(4560) with { Dead = true }).SelectTarget(Now, 100, 1) == null, "Dead player cannot select targets");
            Check(Encounter(Player(4560) with { Position = Center + new Vector3(30, 0, 0) }).SelectTarget(Now, 100, 1) == null,
                "Outside-arena player cannot select targets");
            var chained = Encounter(Player(4560, 4562));
            Check(chained.HoldMovement(Now, 100) && chained.SelectTarget(Now, 100, 2)?.Id == 1, "Chains stop movement but still allow correct targeting");
            e.Synchronize([Enemy(1, 0x48EA) with { InCombat = false }, Enemy(2, 0x48EB), Player(4560)], Now);
            Check(e.Preparing && e.SelectTarget(Now, 100, 1) == null, "Preparation never auto-targets");
            e.Reset();
            Check(e.SelectTarget(Now, 100, 1) == null, "Reset releases target selection");
        });
    }
}
