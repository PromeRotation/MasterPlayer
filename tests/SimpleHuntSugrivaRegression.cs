using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntSugrivaRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly ReplayPartyMember[] Party = [new(0, "P", 100), new(1, "T", 101), new(2, "A", 102)];
    private static ReplayActorState Boss(ulong id = 1) => new()
        { Id = id, Oid = 0x35FC, Kind = 2, Hp = 1000, InCombat = true, Targetable = true };
    private static ReplayActorState Helper(ulong id = 999, ReplayCast? cast = null) => new()
        { Id = id, Oid = 0x233C, Kind = 2, Hp = 1000, Cast = cast };
    private static ReplayActorState Player(ulong id = 100, Vector3 position = default, ReplayStatus? status = null) => new()
        { Id = id, Kind = 1, Hp = 1000, Position = position, Statuses = status == null ? [] : [status] };
    private static ReplayCast Cast(uint action, double at = 0, ulong target = 101, Vector3 location = default)
        => new(action, target, location, 0, Start.AddSeconds(at), Start.AddSeconds(at + 5), false, false) { TotalTime = 5 };
    private static SimpleHuntEncounter Create()
    {
        var e = new SimpleHuntEncounter();
        e.Synchronize([Boss(), Helper(), Helper(998), Helper(997), Player(), Player(101, new(0, 0, 5)), Player(102, new(10, 0, 0))],
            _ => true, Start, Party);
        return e;
    }
    private static HuntPendingCast[] Baits(SimpleHuntEncounter e, uint action, ReplayActorState? player = null)
        => e.TargetPreviews(player ?? Player()).Where(c => c.Rule.Action == action).ToArray();
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Near(float value, float expected, string message)
        => Check(MathF.Abs(value - expected) < .00001f, message);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Sugriva exact ordinary rules grouped Finish and no invented BarrelingSmash", Ordinary);
        run("SimpleHunts Sugriva Twister single slot independent stacks strict immunity and frozen target", Twister);
        run("SimpleHunts Sugriva Butcher dynamic target Finish preservation source FIFO and real death cleanup", Butcher);
        run("SimpleHunts Sugriva Prey accumulating baits exact second Start Chocobo and prediction isolation", Prey);
        run("SimpleHunts Sugriva Prey raw event loss receipts never clear a future bait twice", PreyLoss);
        run("SimpleHunts Sugriva Prey destruction and raw status changes preserve same-frame source input order", PreyOrdering);
        run("SimpleHunts Sugriva helper replay per-owner correction repeated raw and active cleanup", Broadcast);
    }

    private static void Ordinary()
    {
        var e = Create();
        Check(!e.Handles(1, 27220, _ => true), "An unimplemented BarrelingSmash component was invented.");
        var rules = SimpleHuntCatalog.Find(0x35FC)!.Casts;
        Check(rules.Length == 9 && rules.Single(r => r.Action == 27221).Shape is DonutTemplate { InnerRadius: 14, OuterRadius: 30 }
            && rules.Single(r => r.Action == 27222).Shape is CircleTemplate { Radius: 17 }
            && rules.Single(r => r.Action == 27224).Shape is FanTemplate { Radius: 8 } rip
            && rip.AngleRadians == 2 * MathF.PI / 3
            && rules.Where(r => r.Action is 27225 or 27226).All(r => r.Shape is CircleTemplate { Radius: 6 }),
            "Sugriva ordinary geometry differs from activated source constructors.");
        e.Start(999, Cast(27225), true);
        e.Finish(999, 27224, Start.AddSeconds(1));
        Check(!e.Areas.Any(c => c.Shape != null), "RockThrow grouped AOE did not accept another action's Finish.");
        e.Start(998, Cast(27224) with { Rotation = null }, true);
        Check(!e.Areas.Any(c => c.Shape != null), "Rip guessed missing packet facing.");
        e.Start(999, Cast(27227, 2), true);
        Check(!e.Areas.Any(c => c.Shape != null) && e.PredictedDamage(Party).Count(p => p.DamageType == "Raidwide") == 3,
            "Crosswind lost live party prediction or fabricated a radius danger.");
    }

    private static void Twister()
    {
        var e = Create();
        e.Start(999, Cast(27219), true);
        var player = Player(100, new(0, 0, 13));
        var movement = e.Knockbacks(player).Single();
        Check(movement.From == player.Position && movement.To == new Vector3(0, 0, 33)
            && !e.Knockbacks(Player(101, new(0, 0, 5))).Any()
            && !e.Knockbacks(Player(100, new(0, 0, 13.001f))).Any()
            && !e.Knockbacks(Player(100, new(0, 0, 5))).Any(), "Twister target exclusion or inclusive R8/20 rule differs.");
        foreach (var sid in new uint[] { 3054, 160, 1209, 1722, 2663, 2345 })
        {
            var at = Cast(27219).FinishAt;
            Check(e.Knockbacks(player with { Statuses = [new(sid, 0, 1, at)] }).Any()
                && !e.Knockbacks(player with { Statuses = [new(sid, 0, 1, at.AddTicks(1))] }).Any(),
                "Twister changed the strict immunity deadline.");
        }
        Check(!e.PredictedDamage(Party).Any(), "TwisterStack incorrectly generated Shared damage.");
        e.Start(998, Cast(27219, 1, target: 102), true);
        Check(e.TargetPreviews(Player()).Count(c => c.Rule.Action == 27219) == 2,
            "Twister overwrote its stack list with the independent knockback slot.");
        e.Start(997, Cast(27219, 2, target: 500), true);
        Check(e.Knockbacks(Player(100, new(12, 0, 0))).Single().To == new Vector3(32, 0, 0),
            "Unresolved Twister Start erased the previous knockback target.");
        e.Remove(102);
        e.Synchronize([Boss(), Helper(), Helper(998), Helper(997), Player(), Player(101), Player(102, new(100, 0, 100))],
            _ => true, Start.AddSeconds(3), Party);
        Check(e.Knockbacks(Player(100, new(12, 0, 0))).Single().To == new Vector3(32, 0, 0),
            "Destroyed target rebound to a same-ID replacement.");
        e.Finish(999, 27219, Start.AddSeconds(4), targetId: 101);
        Check(!e.Knockbacks(player).Any() && e.TargetPreviews(Player()).Count(c => c.Rule.Action == 27219) == 1,
            "Any Twister Finish must clear the latest KB slot but only the matching target stack.");
    }

    private static void Butcher()
    {
        var e = Create();
        e.Start(999, Cast(27223), false);
        e.Finish(999, 27223, Start.AddSeconds(1));
        e.Start(999, Cast(27223, 2, 102), false);
        e.Start(998, Cast(27223, 3), false);
        Check(Baits(e, 27223).Length == 3 && e.TargetHints(Player(), Party).Contains("Tankbuster cleave")
            && e.PredictedDamage(Party).All(p => p.Activation == Cast(27223).FinishAt),
            "Butcher Finish removed a bait or prediction stopped using the original first activation.");
        e.Synchronize([Boss(), Helper() with { TargetId = 102, Position = new(2, 0, 0) }, Helper(998),
            Player(), Player(101, new(12, 0, 0)), Player(102, new(0, 0, 10))], _ => true, Start.AddSeconds(4), Party);
        var first = Baits(e, 27223).First();
        Check(first.Location == new Vector3(2, 0, 0) && first.Shape is FanTemplate { Radius: 8 },
            "Butcher did not follow its original source and target.");
        Near(first.Rotation, MathF.PI / 2, "Butcher incorrectly used packet facing or the source's new TargetId.");
        var own = e.TargetDangers(Player(101, new(12, 0, 0)), Party)
            .Where(c => c.Rule.Action == 27223 && c.Order == first.Order).ToArray();
        Check(own.Length == 2 && own.All(c => c.Shape is FanTemplate { Radius: 100 }),
            "Butcher target navigation lost the R100 party-avoid directions.");
        e.Effect(998, 27223, Start.AddSeconds(4.1), 1);
        e.Effect(998, 27223, Start.AddSeconds(4.11), 1);
        Check(Baits(e, 27223).Length == 2, "Butcher duplicate Effect or foreign source cleared more than its own first bait.");
        e.Remove(101);
        Check(Baits(e, 27223).Length == 2, "Target destruction was mistaken for target death.");
        e.Remove(999);
        Check(Baits(e, 27223).Length == 0, "Butcher source destruction did not revoke its baits.");
    }

    private static void Prey()
    {
        var e = Create();
        e.Start(999, Cast(27229), false);
        e.Finish(999, 27229, Start.AddSeconds(1));
        e.Start(998, Cast(27229, 2, 102), false);
        Check(Baits(e, 27229).Length == 2 && Baits(e, 27229)[0].Activation == Cast(27229).FinishAt.AddSeconds(1.1d)
            && !e.PredictedDamage(Party).Any(), "Prey reset cleared old baits, altered its delay or invented predicted damage.");
        e.Start(999, Cast(27226, 3), false);
        Check(Baits(e, 27229).Length == 2, "First RockThrowRest Start cleared baits.");
        e.Finish(999, 27226, Start.AddSeconds(4));
        Check(Baits(e, 27229).Length == 2, "RockThrow Finish cleared its independent Prey bait list.");
        e.Start(999, Cast(27226, 5), false);
        Check(Baits(e, 27229).Length == 0, "Exactly the second RockThrowRest Start did not clear all baits.");
        e.Start(999, Cast(27229, 6), false);
        e.Synchronize([Boss(), Helper(), Helper(998), Player(), Player(101) with { Kind = 2, SubKind = 3 }, Player(102)],
            _ => true, Start.AddSeconds(7), Party);
        e.Start(998, Cast(27229, 8), false);
        Check(Baits(e, 27229).Length == 1, "Chocobo generated a Prey bait.");
        e.Start(999, Cast(27226, 9), false);
        Check(Baits(e, 27229).Length == 1, "Chocobo ApplyPrey failed to reset the rock counter.");
        e.Start(999, Cast(27226, 10), false);
        Check(Baits(e, 27229).Length == 0, "Post-Chocobo counter failed to clear on its second Start.");
    }

    private static void PreyLoss()
    {
        var e = Create();
        var a = new ReplayStatus(2939, 0, 1, Start.AddSeconds(30));
        e.SugrivaStatus(101, a, 1, Start);
        e.Synchronize([Boss(), Helper(), Helper(998), Player(), Player(101, status: a), Player(102)],
            _ => true, Start.AddSeconds(.1), Party);
        e.Start(999, Cast(27229, 1), true);
        e.Synchronize([Boss(), Helper(), Helper(998), Player(), Player(101), Player(102)], _ => true, Start.AddSeconds(2), Party);
        Check(Baits(e, 27229).Length == 0, "Real raw Prey loss did not clear baits.");
        e.Start(999, Cast(27229, 3), true);
        e.SugrivaStatus(101, null, 1, Start.AddSeconds(2));
        Check(Baits(e, 27229).Length == 1, "Late confirmation of the old raw Lose cleared a future bait.");
        e.SugrivaStatus(102, a with { SourceId = 2 }, 2, Start.AddSeconds(4));
        e.SugrivaStatus(102, null, 2, Start.AddSeconds(5));
        Check(Baits(e, 27229).Length == 0, "Another actor/source's real Prey Lose failed to clear the component.");
        e.Start(999, Cast(27229, 6), true);
        e.Synchronize([Boss(), Helper(), Helper(998), Player(), Player(101), Player(102)], _ => true, Start.AddSeconds(7), Party);
        Check(Baits(e, 27229).Length == 1, "An unchanged raw absence repeated the previous Prey loss.");
        e.SugrivaStatus(101, a, 1, Start.AddSeconds(8));
        e.SugrivaStatus(101, null, 1, Start.AddSeconds(9));
        e.Start(999, Cast(27229, 10), true);
        e.Synchronize([Boss(), Helper(), Helper(998), Player(), Player(101, status: a), Player(102)],
            _ => true, Start.AddSeconds(11), Party);
        e.Synchronize([Boss(), Helper(), Helper(998), Player(), Player(101), Player(102)], _ => true, Start.AddSeconds(12), Party);
        Check(Baits(e, 27229).Length == 1, "Event-first Gain/Lose raw confirmation cleared the later bait.");
    }

    private static void PreyOrdering()
    {
        var status = new ReplayStatus(2939, 0, 1, Start.AddSeconds(30));
        foreach (var order in new[] { 0, 1, 2 })
        {
            var e = Create();
            e.Synchronize([Boss(), Player(101, status: status), Helper(), Player(), Player(102)], _ => true, Start, Party);
            var caster = Helper(cast: Cast(27229, 1, 102));
            ReplayActorState[] frame = order switch
            {
                0 => [Boss(), caster, Player(), Player(102)],
                1 => [Boss(), Player(101), caster, Player(), Player(102)],
                _ => [Boss(), caster, Player(101), Player(), Player(102)]
            };
            e.Synchronize(frame, _ => true, Start.AddSeconds(1), Party);
            Check(Baits(e, 27229).Length == (order == 2 ? 0 : 1),
                "Prey loss/destroy was deferred past every cast instead of respecting source actor order.");
        }
    }

    private static void Broadcast()
    {
        var e = Create();
        var raw = Cast(27229);
        e.Synchronize([Boss(), Helper(cast: raw), Player(), Player(101)], _ => true, Start, Party);
        e.Synchronize([Boss(), Boss(2), Helper(cast: raw), Player(), Player(101)], _ => true, Start.AddSeconds(1), Party);
        Check(Baits(e, 27229).Length == 2, "Current helper cast was duplicated or not replayed to a new owner.");
        e.Start(999, raw with { TargetId = 100 }, true);
        Check(Baits(e, 27229).Length == 2 && Baits(e, 27229).All(c => c.TargetId == 100),
            "Same-generation correction reset the Prey counter or appended baits.");
        e.Remove(1);
        Check(Baits(e, 27229).Length == 1, "Removing one owner deleted a surviving owner's baits.");
        e.Remove(2);
        Check(!e.TargetPreviews(Player()).Any() && !e.Knockbacks(Player()).Any(), "Last owner retained target or movement output.");
        e.Reset();
        Check(!e.Active && !e.Areas.Any() && !e.TargetDangers(Player(), Party).Any(), "Reset retained Sugriva output.");
    }
}
