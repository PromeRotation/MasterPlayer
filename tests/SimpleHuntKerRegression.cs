using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntKerRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static ReplayCast Cast(uint action, double at = 0, Vector3? location = null)
        => new(action, 1, location ?? Vector3.Zero, .3f, Start.AddSeconds(at), Start.AddSeconds(at + 6.3), false, false);
    private static ReplayActorState Boss(ReplayCast? cast = null) => new()
    {
        Id = 1, Oid = 0x35CF, Kind = 2, SubKind = 5, Targetable = true, InCombat = true, Hp = 1000, Cast = cast
    };
    private static ReplayActorState Player(ulong id = 100, bool dead = false) => new() { Id = id, Dead = dead, Position = new(0, 0, -10) };
    private static readonly ReplayPartyMember[] Party = [new(0, "P", 100), new(1, "P2", 101), new(8, "A", 108), new(24, "NPC", 124)];
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Ker exact source geometry groups locked gaze arbitrary finish and no invented predictions", Geometry);
        run("SimpleHunts Ker flare main-party preload activation hold complete-effect rebuild and individual status loss", Flare);
        run("SimpleHunts Ker mirrors event snapshot acknowledgement misdirection and no remembered geometry", Statuses);
        run("SimpleHunts Ker helper casts corrections slot history death and lifecycle release", Helpers);
    }

    private static void Geometry()
    {
        Check(SimpleHuntCatalog.Find(0x35CF)!.Casts.Length == 25, "Ker source cast coverage differs.");
        foreach (var action in new uint[] { 27636, 27637, 27652, 27659, 27646, 27639, 27653,
            27641, 27642, 27643, 27644, 27661, 27662, 27663, 27664 })
        {
            var e = new SimpleHuntEncounter();
            var cast = Cast(action);
            e.Synchronize([Boss(cast)], _ => true, Start);
            var aoe = e.Areas.Single();
            var shapeMatches = aoe.Shape switch
            {
                CircleTemplate circle => circle.Radius == (action == 27636 ? 12 : 20),
                DonutTemplate donut => donut.InnerRadius == 8 && donut.OuterRadius == 40,
                FanTemplate cone => cone.Radius == 40 && cone.AngleRadians == MathF.PI,
                _ => false
            };
            Check(aoe.Rotation == .3f && aoe.Activation == cast.FinishAt && shapeMatches,
                "Ker shape, mirrored rotation or clock differs.");
            e.Finish(1, 99999, Start.AddSeconds(1));
            Check(e.Areas.Any(a => a.Shape != null) == (action == 27636), "Ker group did not clear on any real caster finish.");
            Check(!e.PredictedDamage(Party).Any(), "Ker invented a raidwide prediction.");
        }
        foreach (var action in new uint[] { 27647, 27640, 27654 })
        {
            var e = new SimpleHuntEncounter();
            var player = Player();
            var cast = Cast(action);
            e.Synchronize([Boss(cast), player], _ => true, Start, Party);
            Check(e.Gazes(player).Single().Rule.GazeRange == 10000 && e.Facing(player, cast.FinishAt) != null,
                "Ker gaze lost its source default range or facing plan.");
            e.Finish(1, 99999, Start.AddSeconds(1));
            e.Synchronize([Boss(cast), player], _ => true, Start.AddSeconds(2), Party);
            Check(!e.Gazes(player).Any(), "Ker gaze resurrected after another caster finish.");
            e.Synchronize([Boss(cast with { Location = Vector3.UnitX }), player], _ => true, Start.AddSeconds(3), Party);
            Check(e.Gazes(player).Any(), "Ker real raw restart retained a cleared gaze.");
        }
    }

    private static void Flare()
    {
        foreach (var action in new uint[] { 27704, 27638, 27706 })
        {
            var e = new SimpleHuntEncounter();
            var cast = Cast(action);
            var player = Player();
            var ally = Player(108);
            var npc = Player(124);
            e.Synchronize([Boss(cast), player, Player(101), ally, npc], _ => true, Start, Party);
            Check(!e.StopEverything(player, Party, Start.AddSeconds(6.299))
                && e.StopEverything(player, Party, cast.FinishAt)
                && !e.StopEverything(ally, Party, cast.FinishAt) && !e.StopEverything(npc, Party, cast.FinishAt),
                "Ker preload included alliance/NPC or executed before activation.");
            e.Synchronize([Boss(), player, Player(101), ally, npc], _ => true, Start.AddSeconds(3), Party);
            Check(!e.Hints(player).Contains("Stop everything!"), "Ker text showed earlier than its 2.5-second window.");
            e.Synchronize([Boss(), player, Player(101), ally, npc], _ => true, Start.AddSeconds(3.8), Party);
            Check(e.Hints(player).Contains("Stop everything!") && e.StopEverything(player, Party, Start.AddSeconds(60)),
                "Ker cancel or elapsed time cleared an unresolved Stay.");
            e.KerEffect(1, action, Start.AddSeconds(7), 1, [100, 108, 124]);
            Check(e.StopEverything(player, Party, Start.AddSeconds(7)) && e.StopEverything(ally, Party, Start.AddSeconds(7))
                && e.StopEverything(npc, Party, Start.AddSeconds(7)) && !e.StopEverything(Player(101), Party, Start.AddSeconds(7)),
                "Ker full effect targets did not clear preload and rebuild all hit slots.");
            e.KerStatus(100, 960, Start.AddSeconds(8), null);
            Check(!e.StopEverything(player, Party, Start.AddSeconds(8)) && e.StopEverything(ally, Party, Start.AddSeconds(8)),
                "Ker individual Pyretic loss cleared the wrong slots.");
            e.KerEffect(1, action, Start.AddSeconds(8), 1, [100]);
            Check(!e.StopEverything(player, Party, Start.AddSeconds(8)), "Duplicate full packet revived a removed Stay.");
            e.KerEffect(1, action, Start.AddSeconds(9), 2, []);
            Check(!e.StopEverything(ally, Party, Start.AddSeconds(9)) && !e.StopEverything(npc, Party, Start.AddSeconds(9)),
                "Empty full-effect target array failed to clear all Stay states.");
            e.KerStatus(100, 960, Start.AddSeconds(10), new(960, 0, 1, Start.AddSeconds(30)));
            Check(!e.StopEverything(player, Party, Start.AddSeconds(11)), "SID960 Gain invented Flare history.");
        }
    }

    private static void Statuses()
    {
        var e = new SimpleHuntEncounter();
        var player = Player();
        var mirror = new ReplayStatus(2848, 2, 1, Start.AddSeconds(30));
        e.Synchronize([Boss(Cast(27927)) with { Statuses = [mirror] }, player], _ => true, Start, Party);
        Check(e.Hints(player).Contains("The next 2 interments will be mirrored!"),
            "Ker activation replay did not apply the current cast before the same actor's status.");
        e.Reset();
        e.Synchronize([Boss(), player with { Statuses = [mirror] }], _ => true, Start, Party);
        Check(e.Hints(player).Contains("The next 2 interments will be mirrored!"), "Ker did not replay an existing mirror status.");
        e.Start(1, Cast(27927), true);
        e.Synchronize([Boss(Cast(27927)), player with { Statuses = [mirror] }], _ => true, Start.AddSeconds(1), Party);
        Check(e.Hints(player).Contains("The next 3 interments will be mirrored!"), "Unchanged old status overrode a new mirror cast.");
        e.Synchronize([Boss(Cast(27927)), player with { Statuses = [mirror with { ExpiresAt = mirror.ExpiresAt.AddSeconds(.2) }] }],
            _ => true, Start.AddSeconds(1.1), Party);
        Check(e.Hints(player).Contains("The next 3 interments will be mirrored!"), "Subsecond old status drift replayed a Gain.");
        e.Finish(1, 27927, Start.AddSeconds(2));
        Check(e.Hints(player).Contains("The next 3 interments will be mirrored!"), "Mirror cancel cleared stacks.");
        var statusTime = 3d;
        foreach (var value in new ushort[] { 4, 1, 5 })
        {
            e.KerStatus(100, 2848, Start.AddSeconds(statusTime++), mirror with { Param = value });
            Check(value <= 4 ? e.Hints(player).Contains($"The next {value} interments will be mirrored!")
                : !e.Hints(player).Any(h => h.Contains("interments")), "Ker mirror extra mapping differs.");
        }
        e.KerStatus(100, 2848, Start.AddSeconds(9), null);
        e.Synchronize([Boss(), player with { Statuses = [mirror] }], _ => true, Start.AddSeconds(9.1), Party);
        Check(!e.Hints(player).Any(h => h.Contains("interments")), "Stale mirror snapshot resurrected a removed status.");
        e.Misdirection(100, 1422, true, Party);
        Check(e.Misdirected(player, Party), "Ker misdirection owner gate missing.");
        e.Misdirection(100, 1422, false, Party);
        Check(!e.Misdirected(player, Party), "Ker misdirection Lose retained control.");
        e.Start(1, Cast(27645, 10), true);
        Check(!e.Areas.Any(a => a.Shape != null), "Whispered Incantation invented remembered geometry.");
    }

    private static void Helpers()
    {
        var e = new SimpleHuntEncounter();
        var helper = new ReplayActorState { Id = 9, Oid = 0x233C, Kind = 2, SubKind = 11 };
        var player = Player();
        e.Synchronize([Boss(), helper, player], _ => true, Start, Party);
        var cast = Cast(27704);
        e.Start(9, cast, true);
        var corrected = cast with { FinishAt = cast.FinishAt.AddSeconds(1) };
        e.Start(9, corrected, true);
        Check(!e.StopEverything(player, Party, cast.FinishAt) && e.StopEverything(player, Party, corrected.FinishAt),
            "Ker helper packet correction failed to retime its preload.");
        e.Synchronize([Boss(), helper, player with { Dead = true }], _ => true, Start.AddSeconds(8), Party);
        Check(!e.StopEverything(player with { Dead = true }, Party, Start.AddSeconds(8))
            && !e.StopEverything(player, Party, Start.AddSeconds(9)), "Ker viewed death failed to clear its slot state.");
        e.KerEffect(9, 27704, Start.AddSeconds(10), 1, [100]);
        var swapped = new ReplayPartyMember[] { new(0, "Other", 101), new(1, "P", 100) };
        e.Synchronize([Boss(), helper, player, Player(101)], _ => true, Start.AddSeconds(11), swapped);
        Check(!e.StopEverything(player, swapped, Start.AddSeconds(11)) && e.StopEverything(Player(101), swapped, Start.AddSeconds(11)),
            "Ker source slot history was incorrectly moved with an actor.");
        e.Synchronize([Boss(), helper, player], _ => false, Start.AddSeconds(12), Party);
        Check(!e.StopEverything(player, Party, Start.AddSeconds(12)), "Ker mechanism disable retained Stay.");
    }
}
