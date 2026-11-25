using System.Numerics;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class PilgrimsFloorRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static ReplayActorState Player => new() { Id = 100, Kind = 1, Position = new(0, 0, 5) };
    private static ReplayActorState Mob(ReplayCast? cast = null)
        => new() { Id = 1, Oid = 0x4922, Kind = 2, SubKind = 5, Targetable = true, IsEnemy = true, Cast = cast };
    private static ReplayCast Cast(uint action, double start = .1)
        => new(action, 100, Vector3.Zero, .25f, Start.AddSeconds(start), Start.AddSeconds(start + 3.3), false, false)
            { IsSpell = true, TotalTime = 3 };
    private static PalaceCast? Resolve(ReplayActorState actor)
        => actor.Cast is { } c ? new(actor.Id, c.ActionId, c.TargetId, c.Location, actor.Position, c.Rotation,
            c.StartedAt, c.FinishAt, CastExtra: .2f) : null;
    private static void Sync(PilgrimsFloorEncounter m, double t, ReplayActorState[] actors, ReplayPartyMember[]? party = null)
        => m.SynchronizeFloor(actors, Start.AddSeconds(t), true, Resolve, null, null, party ?? [new(0, "", 100)]);
    private static PilgrimsFloorEncounter Fixture()
    {
        var m = new PilgrimsFloorEncounter();
        Sync(m, 0, [Player, Mob()]);
        return m;
    }
    internal static void Run(Action<string, Action> run)
    {
        run("PT floor three source donuts precise 270 sector real Finish", Donuts);
        run("PT floor interrupt stun and large-circle escape source semantics", Intents);
        run("PT floor six complete effects source counter all-clear and actor generation", Effects);
        run("PT floor spikes actual Finish status slot all-clear no ACR expansion", Spikes);
        run("PT floor old party immunity strict expiry early Lose and ClearState", Immunity);
        run("PT floor turtle raw RemainingTime strict one second no targetable/combat filter", Turtle);
        run("PT floor cold source history partial clear reset and native dodge coexistence", Lifecycle);
    }
    private static void Donuts()
    {
        foreach (var action in new uint[] { 42091, 44758, 44732 })
        {
            var m = Fixture();
            Sync(m, .1, [Player, Mob(Cast(action))]);
            var a = m.Collect(Start.AddSeconds(1), Player).Single();
            if (action == 42091)
            {
                Check(a.Shape is PtDonutSectorTemplate { Inner: 3, Outer: 30, Angle: var angle } && angle == 1.5f * MathF.PI,
                    "EarthenAuger lost inner hole or 270-degree sector.");
                var original = new BossMod.SDDonutSector(new(0, 0), 3, 30, new(.25f), new(3 * MathF.PI / 4));
                var count = 0;
                for (var x = -30; x <= 30; ++x)
                    for (var z = -30; z <= 30; ++z)
                    {
                        var p = new Vector3(x + .137f, 0, z + .273f);
                        Check(a.Geometry.Contains(p) == (original.Distance(new(p.X, p.Z)) <= 0), "Source sector classification differs.");
                        ++count;
                    }
                Console.WriteLine($"INFO PT sector real source Distance oracle {count} points.");
            }
            else Check(a.Shape is DonutTemplate { InnerRadius: 5 } d && d.OuterRadius == (action == 44758 ? 60 : 30),
                "Master/Peripheral donut changed.");
            Check(m.Collect(Start.AddSeconds(10), Player).Any(), "Predicted clock cleared source cast list.");
            Sync(m, 11, [Player, Mob()]);
            Check(!m.Collect(Start.AddSeconds(11), Player).Any(), "Raw end failed to clear donut.");
        }
    }
    private static void Intents()
    {
        foreach (var action in new uint[] { 44852, 41512, 44727 })
        {
            var m = Fixture();
            Sync(m, .1, [Player, Mob(Cast(action))]);
            var hints = m.Hints(Start.AddSeconds(1), Player).ToArray();
            Check(hints.Any(h => h.StartsWith("眩晕")) && hints.Any(h => h.StartsWith("打断")) == (action == 44852),
                "PT intent coverage changed.");
        }
        foreach (var action in new uint[] { 42523, 43133 })
        {
            var m = Fixture();
            Sync(m, .1, [Player, Mob(Cast(action))]);
            var a = m.Collect(Start.AddSeconds(1), Player).ToArray();
            Check(a.Any(v => v.Shape is CircleTemplate c && c.Radius == (action == 42523 ? 45 : 46))
                && a.Any(v => v.Safe && v.Priority == 100 && v.Shape is DonutTemplate { InnerRadius: 15 }),
                "Large circle escape goal lost.");
            Sync(m, 2, [Player, Mob(Cast(123, 2))]);
            Check(m.Collect(Start.AddSeconds(3), Player).Any(), "Non-null replacement cleared source circle list.");
        }
    }
    private static void Effects()
    {
        foreach (var (action, end) in new (uint, uint)[] { (42748, 42746), (42578, 42749), (42516, 42517),
            (42214, 42215), (42216, 42217), (44759, 44760) })
        {
            var m = Fixture();
            m.Effect(1, action);
            m.Effect(1, action);
            Sync(m, 1, [Player, Mob() with { Position = new(5, 0, 6), Rotation = 1 }]);
            var a = m.Collect(Start.AddSeconds(1), Player).ToArray();
            Check(a.Length == 2 && a.All(v => v.Position.X > 4.9f), "Complete effects collapsed or source stopped following.");
            if (action is 42214 or 42216)
                Check(a[0].Rotation == 1 + (action == 42214 ? MathF.PI / 2 : -MathF.PI / 2), "Shockwave offset wrong.");
            m.ClearFloor();
            Check(m.Collect(Start.AddSeconds(20), Player).Count() == 2, "ClearState or time expired persistent effect.");
            m.Effect(1, end);
            if (action == 44759)
            {
                Check(m.Collect(Start, Player).Count() == 2, "First heel packet removed whole counter.");
                m.Effect(1, end);
                m.Effect(1, end);
                Check(m.Collect(Start, Player).Count() == 1, "Three same-sequence complete packets did not consume first.");
            }
            else Check(!m.Collect(Start, Player).Any(), "Real follow-up failed to clear all original source entries.");
            Sync(m, 21, [Player]);
            Sync(m, 22, [Player, Mob()]);
            Check(!m.Collect(Start.AddSeconds(22), Player).Any(), "Old source effect rebound to same-ID actor.");
        }
        var mixed = Fixture();
        mixed.Effect(1, 42748);
        mixed.Effect(1, 44759);
        mixed.Effect(1, 44760);
        Check(mixed.Collect(Start, Player).Single().Shape is FanTemplate, "Heel matched action instead of first same-ID entry.");
    }
    private static void Spikes()
    {
        var m = Fixture();
        Sync(m, .1, [Player, Mob(Cast(42212))]);
        var buff = new ReplayStatus(4579, 0, 1, Start.AddSeconds(30)) { SlotIndex = 5 };
        Sync(m, .5, [Player, Mob() with { Statuses = [buff] }]);
        Sync(m, 1, [Player, Mob(Cast(42212, 1)) with { Statuses = [buff] }]);
        Sync(m, 2, [Player, Mob() with { Statuses = [buff] }]);
        Check(m.SpikedTargets(Start.AddSeconds(2)).Length == 2, "Real independent finishes did not append spikes.");
        Sync(m, 3, [Player, Mob()]);
        Check(m.SpikedTargets(Start.AddSeconds(3)).Length == 0, "4579 Lose must clear all original source spikes.");
        Sync(m, 4, [Player, Mob(Cast(42212, 4))]);
        Sync(m, 5, [Player, Mob()]);
        Check(m.SpikedTargets(Start.AddSeconds(15)).Length == 0, "Spikes timeout equality changed.");
    }
    private static void Immunity()
    {
        var m = Fixture();
        var c = Cast(41427);
        var buff = new ReplayStatus(160, 0, 100, c.FinishAt.AddSeconds(1)) { SlotIndex = 4 };
        Sync(m, .1, [Player with { Statuses = [buff] }, Mob(c)]);
        Check(!m.Collect(Start.AddSeconds(1), Player).Any(), "Cached immunity missed.");
        Sync(m, 1.1, [Player, Mob(c)]);
        Check(!m.Collect(Start.AddSeconds(2), Player).Any(), "Early Lose cleared expiry.");
        m.ClearFloor();
        Check(m.Collect(Start.AddSeconds(2), Player).Any(a => a.Shape is CircleTemplate { Radius: 15 }),
            "ClearState retained immunity or cleared KB.");
    }
    private static void Turtle()
    {
        var m = Fixture();
        var turtle = Mob(Cast(1)) with { Oid = 0x491B, TargetId = 100, HitboxRadius = 2, Targetable = false };
        Sync(m, .1, [Player, turtle]);
        Check(!m.Collect(Start.AddSeconds(2.1), Player).Any(), "RemainingTime==1 must not enable turtle danger.");
        Check(m.Collect(Start.AddSeconds(2.101), Player).Single().Shape is CircleTemplate { Radius: 7.5f },
            "Raw RemainingTime wrongly includes NPC delay or targetable/combat gating.");
        Sync(m, 3, [Player, turtle with { Cast = null, TargetId = 101 }]);
        Check(!m.Collect(Start.AddSeconds(3), Player).Any(), "Other target received turtle danger.");
    }
    private static void Lifecycle()
    {
        var m = new PilgrimsFloorEncounter();
        Sync(m, 0, [Player, Mob(Cast(42091, -1))]);
        Check(!m.Collect(Start, Player).Any(), "Cold constructor replayed OnCastStarted.");
        Sync(m, .1, [Player, Mob()]);
        Sync(m, .2, [Player, Mob(Cast(42091, .2))]);
        Check(m.Collect(Start.AddSeconds(.3), Player).Any(), "Hot start lost after cold baseline.");
        foreach (var action in new uint[] { 44690, 44691, 42214, 42216, 42516, 42578, 42748, 43132, 44759 })
            Check(m.PreservesGenericDodge(action), "Existing precise MP first-hit consumer suppressed.");
        m.Reset();
        Check(!m.Collect(Start, Player).Any(), "Owner reset retained output.");
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
