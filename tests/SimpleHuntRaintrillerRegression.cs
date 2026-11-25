using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntRaintrillerRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static ReplayCast Cast(uint action, double at = 0, Vector3? location = null)
        => new(action, 101, location ?? Vector3.Zero, .2f, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), false, false);
    private static ReplayActorState Boss(ReplayCast? cast = null) => new()
    {
        Id = 1, Oid = 0x457F, Targetable = true, InCombat = true, Hp = 1000, Cast = cast
    };
    private static HuntPendingCast[] Areas(SimpleHuntEncounter e) => e.Areas.Where(a => a.AreaId.Contains("/raintriller/")).ToArray();
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Raintriller all yell and visual combinations exact float timing geometry hints and FIFO", Matrix);
        run("SimpleHunts Raintriller unknown yell clears only cache raw restarts and order corrections preserve history", Corrections);
        run("SimpleHunts Raintriller min two unlimited stack without shared damage and no guessed history", Stack);
        run("SimpleHunts Raintriller existing helper inputs broadcast once and unknown actors are ignored", Helpers);
        run("SimpleHunts Raintriller helper replacement removal disabled foreign modules and source actor types", HelperLifecycle);
    }

    private static void Matrix()
    {
        foreach (var (yell, order) in new (ushort, string)[]
        {
            (17815, "O"), (17719, "OI"), (17725, "OBI"), (17721, "OIB"), (17734, "OBO"),
            (17720, "IOB"), (17723, "IBO"), (17793, "IIO"), (17722, "BIO"), (17724, "BOI")
        })
        foreach (var action in new uint[] { 39758, 39751, 39749 })
        {
            var e = new SimpleHuntEncounter();
            e.Synchronize([Boss()], _ => true, Start);
            e.NpcYell(1, yell, Start);
            var cast = Cast(action, 1);
            e.Start(1, cast, true);
            var hint = string.Join(" -> ", order.Select(c => c == 'O' ? "Out" : c == 'I' ? "In" : "Back"));
            Check(e.Hints(null).Contains(hint), "Raintriller complete global sequence hint differs.");
            for (var i = 0; i < order.Length; ++i)
            {
                var areas = Areas(e);
                Check(areas.Length == Math.Min(2, order.Length - i) && areas[0].Risky
                    && areas[0].Activation == cast.FinishAt.AddSeconds(1.2f + i * 3.2f), "Raintriller preview or float deadline differs.");
                var shape = areas[0].Shape;
                Check(order[i] switch
                {
                    'O' => shape is CircleTemplate { Radius: 12 },
                    'I' => shape is DonutTemplate { InnerRadius: 10, OuterRadius: 40 },
                    _ => shape is FanTemplate fan && fan.Radius == 40 && fan.AngleRadians == 3 * MathF.PI / 2
                }, "Raintriller source half135 cone or other shape differs.");
                if (areas.Length == 2)
                    Check(areas[1].Risky == (order[i + 1] == 'B'), "Raintriller future cone risk was dropped or future circle remained risky.");
                e.Finish(1, 39758, Start.AddSeconds(10 + i));
                e.Effect(1, 39750, Start.AddSeconds(10 + i), (uint)i + 1);
                Check(Areas(e).Length == areas.Length, "Visual finish or effect consumed Raintriller queue.");
                e.Finish(1, new uint[] { 39752, 39753, 39750 }[i % 3], Start.AddSeconds(11 + i));
            }
            Check(Areas(e).Length == 0 && !e.Hints(null).Any(), "Raintriller short finish failed to consume hidden FIFO tail.");
        }
    }

    private static void Corrections()
    {
        var e = new SimpleHuntEncounter();
        e.Synchronize([Boss()], _ => true, Start);
        e.NpcYell(1, 17721, Start);
        e.Start(1, Cast(39749, 1), true);
        var old = Areas(e)[0];
        e.NpcYell(1, 17734, Start.AddSeconds(2));
        e.NpcYell(1, 0, Start.AddSeconds(3));
        e.Start(1, Cast(39751, 4), true);
        Check(Areas(e)[0] == old, "Unknown yell cleared existing AOE or allowed old cache to release.");
        e.NpcYell(1, 17815, Start.AddSeconds(5));
        var cast = Cast(39758, 6);
        e.Start(1, cast, true);
        e.Synchronize([Boss(cast)], _ => true, Start.AddSeconds(6.1));
        var corrected = cast with { Location = new(20, 0, 20), FinishAt = cast.FinishAt.AddSeconds(.1) };
        e.Start(1, corrected, true);
        Check(Areas(e)[0] == old, "Current packet correction moved an older queue generation.");
        e.Synchronize([Boss(corrected)], _ => true, Start.AddSeconds(7));
        for (var i = 0; i < 3; ++i) e.Finish(1, 39752, Start.AddSeconds(8 + i));
        var last = Areas(e).Single();
        Check(last.Location == corrected.Location && last.Activation == corrected.FinishAt.AddSeconds(1.2f),
            "Hidden current prediction missed packet correction or raw restart replayed consumed cache.");
        e.Finish(1, 39750, Start.AddSeconds(12));
        Check(Areas(e).Length == 0, "Raintriller guessed extra history on raw restart.");
    }

    private static void Stack()
    {
        var e = new SimpleHuntEncounter();
        var player = new ReplayActorState { Id = 100, Position = new(20, 0, 0) };
        var target = new ReplayActorState { Id = 101 };
        var buddy = new ReplayActorState { Id = 102 };
        var party = new ReplayPartyMember[] { new(0, "P", 100), new(1, "T", 101), new(2, "B", 102) };
        e.Synchronize([Boss(Cast(39754)), player, target, buddy], _ => true, Start, party);
        Check(e.TargetPreviews(player).Single() is { Shape: CircleTemplate { Radius: 6 }, Safe: true }
            && !e.TargetHints(player, party).Any() && !e.PredictedDamage(party).Any(),
            "Raintriller two-person stack inherited min8 or Shared.");
        e.Synchronize([Boss(Cast(39754)), player, target], _ => true, Start.AddSeconds(1), party);
        Check(e.TargetHints(player, party).Contains("Stack!"), "Raintriller min2 missing participant did not produce a hint.");
        e.Reset();
        e.Synchronize([Boss(Cast(39749))], _ => true, Start);
        Check(!Areas(e).Any(), "Raintriller guessed yell history.");
    }

    private static void Helpers()
    {
        var e = new SimpleHuntEncounter();
        var helper = new ReplayActorState { Id = 9, Oid = 0x233C };
        var second = Boss() with { Id = 2 };
        e.Synchronize([Boss(), second, helper], _ => true, Start);
        e.NpcYell(999, 17815, Start);
        e.Start(9, Cast(39758), true);
        Check(!Areas(e).Any(), "Unknown actor yell reached a Raintriller component.");
        e.NpcYell(9, 17721, Start.AddSeconds(1));
        var cast = Cast(39758, 2);
        e.Start(9, cast, true);
        Check(Areas(e).Length == 4 && Areas(e).Select(a => a.AreaId).Distinct().Count() == 4,
            "Existing helper did not broadcast to both components with unique drawing IDs.");
        e.Synchronize([Boss(), second, helper with { Cast = cast }], _ => true, Start.AddSeconds(2.1));
        e.Start(9, cast, true);
        Check(Areas(e).Length == 4, "Raw confirmation or duplicate packet released the helper cache twice.");
        e.Finish(999, 39750, Start.AddSeconds(8));
        Check(e.Hints(null).Contains("Out -> In -> Back"), "Unknown helper finish consumed history.");
        e.Finish(9, 39750, Start.AddSeconds(9));
        Check(e.Hints(null).Contains("In -> Back"), "Helper finish did not consume each component once.");
        e.Finish(9, 39752, Start.AddSeconds(10));
        e.Finish(9, 39753, Start.AddSeconds(11));
        Check(!Areas(e).Any(), "Helper finishes retained hidden history.");
        e.Reset();
        var target = new ReplayActorState { Id = 101 };
        e.Synchronize([Boss(), helper with { Cast = Cast(39754) }, target], _ => true, Start);
        Check(e.TargetPreviews(target).Single().Shape is CircleTemplate { Radius: 6 },
            "Observed helper Drop of Venom failed to bind its target.");
    }

    private static void HelperLifecycle()
    {
        foreach (var ending in new[] { "replacement", "removed", "missing", "disabled" })
        {
            var e = new SimpleHuntEncounter();
            var helper = new ReplayActorState
            {
                Id = 9, Oid = ending == "disabled" ? 0x288Eu : 0x233Cu,
                Kind = 2, SubKind = 11, Hp = 100, InCombat = true, Targetable = true
            };
            bool Enabled(string name) => name != "Tyger";
            e.Synchronize([Boss(), helper], Enabled, Start);
            e.NpcYell(9, 17721, Start);
            e.Start(9, Cast(39749), true);
            var shortCast = Cast(39750, 8);
            e.Synchronize([Boss(), helper with { Cast = shortCast }], Enabled, Start.AddSeconds(8));
            Check(e.Hints(null).Contains("Out -> In -> Back"), "Foreign module gate prevented helper input.");
            if (ending == "removed") e.Remove(9);
            else if (ending == "missing") e.Synchronize([Boss()], Enabled, Start.AddSeconds(9));
            else e.Synchronize([Boss(), helper with { Cast = ending == "replacement" ? Cast(99999, 9) : null }],
                Enabled, Start.AddSeconds(9));
            Check(e.Hints(null).Contains("In -> Back"), $"Helper {ending} failed to deliver its real short Finish.");
            if (ending == "disabled")
            {
                e.Finish(9, 39750, Start.AddSeconds(9).AddMilliseconds(-20));
                e.Finish(9, 39750, Start.AddSeconds(9).AddMilliseconds(20));
                Check(e.Hints(null).Contains("In -> Back"), "Late ending packets consumed an already observed cast twice.");
            }
        }
        foreach (var (kind, subKind) in new (byte, byte)[] { (1, 4), (2, 2), (2, 3), (2, 9) })
        {
            var e = new SimpleHuntEncounter();
            var excluded = new ReplayActorState { Id = 9, Kind = kind, SubKind = subKind };
            e.Synchronize([Boss(), excluded], _ => true, Start);
            e.NpcYell(9, 17721, Start);
            e.Start(9, Cast(39749), true);
            Check(!Areas(e).Any(), "Excluded actor type cast released the cache.");
            e.Start(1, Cast(39749), true);
            e.Finish(9, 39750, Start.AddSeconds(10));
            Check(e.Hints(null).Contains("Out -> In -> Back"), "Excluded actor type finish consumed history.");
            e.Finish(1, 39750, Start.AddSeconds(11));
            e.Finish(1, 39750, Start.AddSeconds(11));
            Check(e.Hints(null).Contains("In -> Back"), "Duplicate ending consumed the FIFO twice.");
        }
    }
}
