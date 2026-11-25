using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;

internal static class DungeonCastBoundaryRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Native cast boundary allows different action at real Finish timestamp and rejects older observations", () =>
        {
            var (encounter, now, origin) = Fixture();
            var old = new DungeonCastObservation(4261, origin, 0, now, now.AddSeconds(3));
            encounter.StartCast(1, old, now);
            var boundary = now.AddSeconds(3);
            encounter.Finish(1, 4261, boundary);
            var next = new DungeonCastObservation(4259, origin, 0, boundary, boundary.AddSeconds(3));
            encounter.StartCast(1, next, boundary);
            var accepted = encounter.Casts[1];
            encounter.StartCast(1, old, boundary.AddSeconds(.1), fromPacket: false);
            Check(ReferenceEquals(accepted, encounter.Casts[1]), "Old cross-action raw replaced accepted boundary cast.");
            encounter.Finish(1, 4259, boundary);
            encounter.StartCast(1, next, boundary);
            Check(!encounter.Casts.ContainsKey(1), "Closed same action re-entered at equality.");
            encounter.StartCast(1, next with { Action = 4260, StartedAt = boundary.AddTicks(-1) }, boundary);
            Check(!encounter.Casts.ContainsKey(1), "Earlier cross-action start passed watermark.");
        });
        run("Native cast boundary remembers every action closed at the same timestamp across later different actions", () =>
        {
            var (encounter, now, origin) = Fixture();
            DungeonCastObservation Cast(uint action) => new(action, origin, 0, now, now.AddSeconds(3));
            encounter.StartCast(1, Cast(4261), now); encounter.Finish(1, 4261, now);
            encounter.StartCast(1, Cast(4259), now); encounter.Finish(1, 4259, now);
            encounter.StartCast(1, Cast(4260), now);
            var accepted = encounter.Casts[1];
            encounter.StartCast(1, Cast(4261), now.AddSeconds(.1), fromPacket: false);
            Check(ReferenceEquals(accepted, encounter.Casts[1]), "A-to-B-to-C equal-time history allowed old A rollback.");
        });
        run("Native cast boundary old equal-time ADS raw cannot suppress restored cleave", () =>
        {
            var now = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
            var origin = new Vector3(0, 0, 77);
            var encounter = new DungeonEncounter(242);
            DungeonActor[] actors = [new(1, 0x7D9, origin, InCombat: true, TargetId: 101),
                new(100, 0, origin, PartySlot: 0), new(101, 0, origin + Vector3.UnitZ * 5, PartySlot: 1)];
            encounter.Synchronize(actors, origin, now, playerId: 100);
            DungeonCastObservation Cast(uint action) => new(action, origin, 0, now, now.AddSeconds(5));
            encounter.StartCast(1, Cast(1216), now); encounter.Finish(1, 1216, now);
            encounter.StartCast(1, Cast(1217), now); encounter.Finish(1, 1217, now);
            actors[0] = actors[0] with { Cast = Cast(1216) };
            encounter.Synchronize(actors, origin, now.AddSeconds(.1), playerId: 100);
            Check(encounter.Casts.Count == 0 && encounter.AmbientAoes().Any(a => a.Id.StartsWith("coil/cleave/")),
                "Closed raw remained a casting gate after the shared watermark rejected it.");
        });
    }
    private static (DungeonEncounter Encounter, DateTime Now, Vector3 Origin) Fixture()
    {
        var now = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
        var origin = new Vector3(-515, 0, -305);
        var encounter = new DungeonEncounter(477);
        encounter.Synchronize([new(1, 0x10B9, origin, InCombat: true), new(100, 0, origin, PartySlot: 0)],
            origin, now, playerId: 100);
        return (encounter, now, origin);
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
