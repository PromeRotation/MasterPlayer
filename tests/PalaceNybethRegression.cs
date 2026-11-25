using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class PalaceNybethRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(300, 0, 300);
    internal static void Run(Action<string, Action> run)
    {
        run("Palace Nybeth exact five active actions and no invented adds thresholds", Rules);
        run("Palace Nybeth precombat guide consumed only once reset disable director lifetimes", Guide);
        run("Palace Nybeth target centered dynamic bait party navigation original identities", Baits);
        run("Palace Nybeth dead source first stored clock target mask observed finish not effect", Damage);
        run("Palace Nybeth packet raw confirmation replacement late cancel owner cleanup", Timing);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(597);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x1808, Center), new(3, 0x233C, Center), new(4, 0x233C, Center + Vector3.UnitX),
            new(100, 0, Center + Vector3.UnitX * 4, PartySlot: 0, Player: true),
            new(101, 0, Center + Vector3.UnitZ * 4, PartySlot: 1, Player: true),
        ];
        internal DateTime Now = Start;
        internal ulong Viewer = 100;
        internal Fixture(bool combat = true)
        {
            Set(1, a => a with { InCombat = combat });
            Sync();
        }
        internal void Sync(Func<string, bool>? enabled = null) =>
            E.Synchronize(Actors, Actors.First(a => a.Id == Viewer).Position, Now, enabled, Viewer);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change) =>
            Actors[Actors.FindIndex(a => a.Id == id)] = change(Actors.First(a => a.Id == id));
        internal void Cast(ulong source, ulong target, float duration = 3)
        {
            Now = Now.AddSeconds(.01);
            Set(source, a => a with { Cast = new(6872, Center, 0, Now, Now.AddSeconds(duration), target) });
            Sync();
        }
        internal DungeonAmbientAoe[] Aoes() => E.AmbientAoes().Where(a => a.Id.StartsWith("nybeth/abyss/")).ToArray();
    }

    private static void Rules()
    {
        var f = new Fixture();
        Check(f.E.Definitions.Single().Casts.Select(r => r.Action).Order().SequenceEqual(new uint[] { 6872, 6874, 6875, 6876, 6878 }),
            "Inactive enum actions became components.");
        Check(f.E.Definitions.Single().Casts.Single(r => r.Action == 6875).Shape == new FanTemplate(47.4f, 2 * MathF.PI / 3)
            && f.E.Definitions.Single().Casts.Single(r => r.Action == 6874).Shape == RayRectangleTemplate.Symmetric(8, 52.4f)
            && f.E.Definitions.Single().Casts.Single(r => r.Action == 6872).Shape == null, "Exact shapes / no static Abyss.");
        f.Actors.Add(new(10, 0x180A, Center, InCombat: true, AggroPlayer: true));
        f.Actors.Add(new(11, 0x1809, Center, InCombat: true, AggroPlayer: true));
        f.Actors.Add(new(12, 0x180B, Center, InCombat: true, AggroPlayer: true));
        f.Sync();
        Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("hw-intro/enemy/")) == 4
            && f.E.ForbiddenTargets.Length == 0, "Adds markers became restrictions.");
        f.Set(1, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active == null, "Living adds retained source finished phase.");
    }

    private static void Guide()
    {
        var f = new Fixture(false);
        Check(f.E.PalaceGuidePrecombatHint?.Contains("90%, 65%, 40%") == true && f.E.Hints(Center).Contains(f.E.PalaceGuidePrecombatHint),
            "Constructor guide missing.");
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.PalaceGuidePrecombatHint == null, "Pull retained guide.");
        f.E.ResetFight();
        f.Set(1, a => a with { InCombat = false });
        f.Sync(_ => false);
        f.Sync();
        Check(f.E.PalaceGuidePrecombatHint == null, "Reset/toggle revived consumed guide.");
        f.E.DirectorUpdate(0x40000005);
        f.Sync();
        Check(f.E.Active == null && f.E.PalaceGuidePrecombatHint == null, "Wipe reconstructed too early.");
        f.E.DirectorUpdate(0x40000011);
        f.Sync();
        Check(f.E.PalaceGuidePrecombatHint == null, "Director resume invented owner for existing actor.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        f.Actors.Add(new(1, 0x1808, Center));
        f.Sync();
        Check(f.E.PalaceGuidePrecombatHint != null, "Actual new owner did not restore constructor guide.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        Check(f.E.PalaceGuidePrecombatHint == null, "Removed unpulled owner retained guide.");
        f.Actors.Add(new(1, 0x1808, Center, InCombat: true));
        f.Sync();
        f.Set(1, a => a with { Hp = 0 });
        f.Sync();
        f.Set(1, a => a with { Hp = 1000 });
        f.Sync();
        Check(f.E.Active == null && f.E.PalaceGuidePrecombatHint == null
            && !f.E.AmbientAoes().Any(a => a.Id.StartsWith("nybeth/precombat/")), "Finished old actor recreated module without Added.");
        var distant = new Fixture(false);
        distant.Set(1, a => a with { Position = Center + Vector3.UnitX * 600 });
        distant.Sync();
        Check(distant.E.PalaceGuidePrecombatHint == null, "Unactivated distant owner did not become pending.");
        distant.E.DirectorUpdate(0x40000005);
        distant.Set(1, a => a with { Position = Center });
        distant.Sync();
        Check(distant.E.PalaceGuidePrecombatHint != null, "Wipe incorrectly blocked actual pending promotion.");
    }

    private static void Baits()
    {
        var f = new Fixture();
        f.Cast(3, 100);
        var oldTarget = f.E.NybethBaits.Single().Target;
        Check(f.Aoes().Single(a => a.Display).OutlineOnly
            && f.Aoes().Single(a => a.Display).Position == f.Actors.First(a => a.Id == 100).Position
            && f.Aoes().Single(a => a.Navigate).Position == f.Actors.First(a => a.Id == 101).Position, "Target center / party bait separation.");
        f.Set(100, a => a with { Position = Center + Vector3.UnitZ, Dead = true });
        f.Sync();
        Check(f.Aoes().Single(a => a.Display).Position == Center + Vector3.UnitZ, "Dead target stopped following.");
        f.Viewer = 101;
        f.Sync();
        f.Actors.RemoveAll(a => a.Id == 100);
        f.Sync();
        Check(oldTarget.Removed && f.Aoes().Single(a => a.Navigate).Position == Center + Vector3.UnitZ, "Removed target did not freeze original reference.");
        Check(!f.E.PredictedDamage.Any(), "Frozen departed target retained stale party slot prediction.");
        f.Actors.Add(new(100, 0, Center + Vector3.UnitX * 20, PartySlot: 0, Player: true));
        f.Viewer = 100;
        f.Sync();
        Check(f.Aoes().Single(a => a.Display).OutlineOnly
            && f.Aoes().Single(a => a.Navigate).Position == Center + Vector3.UnitZ
            && ReferenceEquals(oldTarget, f.E.NybethBaits.Single().Target), "Same ID rebound old bait target.");
        var self = new Fixture();
        self.Cast(100, 100);
        Check(self.Aoes().Count(a => a.Navigate) == 0, "Source==target invented target navigation.");
    }

    private static void Damage()
    {
        var f = new Fixture();
        f.Cast(3, 100, 2);
        var firstClock = f.E.NybethBaits[0].Cast.FinishAt;
        f.Set(3, a => a with { Dead = true });
        f.Cast(4, 101, 6);
        var prediction = f.E.PredictedDamage.Single();
        Check(prediction.TargetId == 101 && prediction.DamageType == "Raidwide" && prediction.Activation == firstClock
            && f.E.NybethBaits.Count == 2 && f.Aoes().Count(a => a.Display) == 1, "Active mask / dead source / first stored clock.");
        f.E.Effect(4, 6872, 0, f.Now.AddSeconds(1));
        f.Now = f.Now.AddSeconds(30);
        f.Sync();
        Check(f.E.NybethBaits.Count == 2, "Effect/deadline cleared Finish-ended bait.");
        f.Set(4, a => a with { Cast = null });
        f.Sync();
        Check(f.Aoes().Length == 0 && !f.E.PredictedDamage.Any(), "Actual Finish retained active output.");
    }

    private static void Timing()
    {
        var f = new Fixture();
        var raw = new DungeonCastObservation(6872, Center, 0, f.Now, f.Now.AddSeconds(3), 100);
        f.E.StartCast(3, raw, f.Now, fromPacket: true);
        f.Set(3, a => a with { Cast = raw with { TargetId = 101, Location = Center + Vector3.UnitX } });
        f.Sync();
        Check(f.E.NybethBaits.Count == 1 && f.E.NybethBaits[0].Target.Actor.Id == 101, "First raw confirmation duplicated or retained wrong target.");
        f.Now = f.Now.AddSeconds(1);
        f.Set(3, a => a with { Cast = a.Cast! with { TargetId = 100 } });
        f.Sync();
        f.E.StartCast(3, raw with { TargetId = 101 }, f.Now, fromPacket: true);
        Check(f.E.NybethBaits.Single().Target.Actor.Id == f.E.NybethBaits.Single().Cast.TargetId
            && f.E.NybethBaits.Single().Target.Actor.Id == 100, "Late packet retargeted authoritative generation.");
        f.E.Finish(3, 6872, f.Now.AddSeconds(-.5));
        Check(f.E.NybethBaits.Single().Target.Actor.Id == 100, "Late cancel ended replacement.");
        f.E.ResetFight();
        Check(f.E.NybethBaits.Count == 0 && f.Aoes().Length == 0, "Owner reset retained bait.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
