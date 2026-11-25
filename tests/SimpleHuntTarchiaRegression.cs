using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;

internal static class SimpleHuntTarchiaRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Tarchia constructor preview, initial anchor and early/late Groundstorm endings", () =>
        {
            var encounter = new SimpleHuntEncounter();
            var boss = Boss(1);
            encounter.Synchronize([boss], _ => true, Start);
            var initial = encounter.Areas.Single();
            Check(!encounter.Active && !initial.Risky && initial.Activation == default, "Pre-fight constructor became navigation or disappeared.");
            boss = boss with { InCombat = true, Position = new(20, 0, 10) };
            encounter.Synchronize([boss], _ => true, Start.AddSeconds(.1));
            Check(encounter.Areas.Single() is { Risky: true } original && original.Location == initial.Location,
                "Initial circle moved to the pull position.");
            var cast = Ground(1, boss.Position);
            encounter.Start(1, cast, true, observedAt: Start.AddSeconds(1));
            encounter.Finish(1, 18023, Start.AddSeconds(2));
            var spin = encounter.Areas.Single();
            Check(spin.Location == cast.Location && spin.Rule.Action == 18093
                && Math.Abs((spin.Activation - Start.AddSeconds(10.3)).TotalSeconds) < .00001,
                "Early cancellation used cancel time plus four instead of old NPC remaining time.");
            encounter.Synchronize([boss], _ => true, Start.AddSeconds(30));
            Check(encounter.Areas.Single() == spin, "Follow-up expired by elapsed prediction time.");
            cast = Ground(31, new(4, 0, 7));
            encounter.Start(1, cast, true, observedAt: Start.AddSeconds(31));
            encounter.Finish(1, 18023, Start.AddSeconds(39));
            spin = encounter.Areas.Single();
            Check(Math.Abs((spin.Activation - Start.AddSeconds(43.3)).TotalSeconds) < .00001,
                "Late ending ignored source ElapsedTime clamping.");
        });
        run("SimpleHunts Tarchia arbitrary effects, duplicate barriers, instance isolation and lifecycle", () =>
        {
            var encounter = new SimpleHuntEncounter();
            var a = Boss(1);
            var b = Boss(2) with { Position = new(30, 0, 0) };
            encounter.Synchronize([a, b], _ => true, Start);
            encounter.Effect(1, 999, Start.AddSeconds(.1), 100);
            Check(encounter.Areas.Single().Source == 2, "Unknown pre-fight effect was blocked or cleared another source.");
            a = a with { InCombat = true };
            b = b with { InCombat = true };
            encounter.Synchronize([a, b], _ => true, Start.AddSeconds(1));
            encounter.Start(1, Ground(1, a.Position), true);
            encounter.Finish(1, 18023, Start.AddSeconds(2));
            encounter.Effect(1, 18023, Start.AddSeconds(2.1), 101);
            Check(encounter.Areas.Count() == 2, "Groundstorm effect cleared its follow-up.");
            encounter.Effect(1, 870, Start.AddSeconds(3), 102);
            Check(encounter.Areas.Single().Source == 2, "Auto-attack failed to clear the prediction.");
            encounter.Start(1, Ground(4, a.Position), true);
            encounter.Finish(1, 18023, Start.AddSeconds(5));
            encounter.Effect(1, 870, Start.AddSeconds(6), 102);
            encounter.Effect(1, 18103, Start.AddSeconds(3), 103);
            Check(encounter.Areas.Count() == 2, "Duplicate or old effect consumed a newer follow-up.");
            encounter.Effect(1, 18103, Start.AddSeconds(6), 104);
            Check(encounter.Areas.Single().Source == 2, "WakeUp incorrectly created another prediction.");
            encounter.Synchronize([a, b with { Dead = true }], _ => true, Start.AddSeconds(7));
            Check(!encounter.Areas.Any(), "Dead instance leaked a constructor circle.");
            encounter.Synchronize([a], _ => false, Start.AddSeconds(8));
            Check(!encounter.Areas.Any() && !encounter.DrawingActors.Any(), "Disabled Tarchia leaked output.");
            encounter.Reset();
            Check(!encounter.Areas.Any(), "Reset retained Tarchia state.");
        });
        run("SimpleHunts Tarchia surviving disengage rebuilds constructor circle before reengage", () =>
        {
            var encounter = new SimpleHuntEncounter();
            var boss = Boss(1) with { InCombat = true };
            encounter.Synchronize([boss], _ => true, Start);
            encounter.Effect(1, 870, Start.AddSeconds(1), 1);
            Check(!encounter.Areas.Any(), "Autoattack did not consume initial circle.");
            boss = boss with { InCombat = false, Position = new(30, 0, 10) };
            encounter.Synchronize([boss], _ => true, Start.AddSeconds(2));
            Check(encounter.Areas.Single() is { Risky: false, Activation: var at } initial
                && at == default && Vector3.Distance(initial.Location, boss.Position) < .05f,
                "Surviving disengage did not reconstruct the initial preview at current position.");
            boss = boss with { InCombat = true };
            encounter.Synchronize([boss], _ => true, Start.AddSeconds(3));
            Check(encounter.Areas.Single().Risky, "Reengage failed to restore navigation.");
            encounter.Start(1, Ground(4, boss.Position), true);
            encounter.Finish(1, 18023, Start.AddSeconds(5));
            boss = boss with { InCombat = false, Position = new(40, 0, 20) };
            encounter.Synchronize([boss], _ => true, Start.AddSeconds(6));
            Check(encounter.Areas.Single().Activation == default
                && Vector3.Distance(encounter.Areas.Single().Location, boss.Position) < .05f,
                "Old Groundstorm follow-up survived encounter reset.");
        });
        run("SimpleHunts Tarchia exact seven rules, raidwide and four status-driven misdirection identities", () =>
        {
            var definition = SimpleHuntCatalog.Find(0x2873)!;
            Check(definition.Casts.Length == 7, "Tarchia cast catalog is incomplete.");
            var fire = definition.Casts.Single(c => c.Action == 18030).Shape!.Resolve(Vector3.Zero, 0, null);
            Check(fire.Contains(new(14.99f, 0, 0), 0) && !fire.Contains(new(15.01f, 0, 0), 0), "Falloff substituted the annotated R40.");
            var encounter = new SimpleHuntEncounter();
            var boss = Boss(1) with { InCombat = true };
            ReplayActorState player = new() { Id = 100 };
            ReplayPartyMember[] party = [new(0, "", 100)];
            encounter.Synchronize([boss, player], _ => true, Start, party);
            encounter.Start(1, new(18031, 0, default, 0, Start, Start.AddSeconds(4.3), false, false), true);
            Check(encounter.PredictedDamage(party).Single().TargetId == 100
                && encounter.Casts.Single().Shape == null, "Raidwide lost prediction or invented geometry.");
            foreach (var status in new uint[] { 1422, 2936, 3694, 3909 })
            {
                encounter.Misdirection(100, status, true, party);
                Check(encounter.Misdirected(player, party), "Tarchia status did not authorize existing PR misdirection control.");
                encounter.Finish(1);
                Check(encounter.Misdirected(player, party), "Cast end cancelled a still-present misdirection status.");
                encounter.Misdirection(100, status, false, party);
                Check(!encounter.Misdirected(player, party), "Status loss retained misdirection.");
            }
        });
    }

    private static ReplayActorState Boss(ulong id) => new() { Id = id, Oid = 0x2873, Hp = 1000, MaxHp = 1000, Targetable = true, Position = new(1.2345f, 0, 2.3456f) };
    private static ReplayCast Ground(double t, Vector3 location) => new(18023, 0, location, 0, Start.AddSeconds(t), Start.AddSeconds(t + 5.3), false, false) { TotalTime = 5 };
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
