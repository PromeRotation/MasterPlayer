using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureNarkissosRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static DungeonActor Boss => new(1, 0x3D48, Center, InCombat: true);
    private static DungeonActor Player(ulong id = 100, uint[]? statuses = null,
        IReadOnlyDictionary<uint, DateTime>? expirations = null, float rotation = 0) =>
        new(id, 0, Center, PartySlot: (int)id - 100, Hostile: false, Player: true,
            Statuses: statuses, StatusExpirations: expirations, Rotation: rotation);
    private static DungeonEncounter Create()
    {
        var e = new DungeonEncounter(1123) { TreasurePartyInput = [100, 101] };
        e.Synchronize([Boss, Player(), Player(101), new(2, 0x233C, Center), new(3, 0x233C, Center)],
            Center, Start, playerId: 100);
        return e;
    }
    private static DungeonCastObservation Cast(uint action, double started = 0, Vector3? location = null) =>
        new(action, location ?? Center, .7f, Start.AddSeconds(started), Start.AddSeconds(started + 5), 100);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure Narkissos full active components and exact rules", Rules);
        run("native treasure Narkissos Sap cumulative5 correction and any Finish clears active", Sap);
        run("native treasure Narkissos all four march directions ordered13.2 without navigation", Directions);
        run("native treasure Narkissos march checks only final Sap not cross boundary or clocks", Destination);
        run("native treasure Narkissos status event overlay unknown actor and same timestamp replacement", Overlay);
        run("native treasure Narkissos raw occupied expired slot and small expiry corrections", Raw);
        run("native treasure Narkissos forced1257 and3629 remaining6.6 and real Lose", Ongoing);
        run("native treasure Narkissos unknown expiry waits for raw and destroy blocks old events", Destroy);
        run("native treasure Narkissos destroyed occupied raw state cannot revive old generation", OldRawAfterDestroy);
        run("native treasure Narkissos raw-first source replacement reconciles same-time Lose Gain", RawFirstReplacement);
        run("native treasure Narkissos raw unknown clock cannot create movement and later finite resolves", UnknownRaw);
        run("native treasure Narkissos unknown event clock still confirms source and param identity", UnknownIdentity);
        run("native treasure Narkissos skipped event cycle retains old Lose but accepts distinct new raw Gain", SkippedCycle);
        run("native treasure Narkissos repeated late Remove cannot lower destruction watermark", DestroyWatermark);
        run("native treasure Narkissos historical receipt pair cannot roll back newer raw forced end", HistoricalReceipt);
        run("native treasure Narkissos raw-first Lose establishes old generation barrier", RawFirstLoseBarrier);
        run("native treasure Narkissos unknown clock Lose barrier survives empty acknowledgement", UnknownLoseBarrier);
        run("native treasure Narkissos distinct raw generation releases old event barrier", DistinctRawBarrier);
        run("native treasure Narkissos misdirection any Lose mask retains until real transition", Misdirection);
        run("native treasure Narkissos reward lifecycle spread6 and7 no forecast", Rewards);
    }
    private static void Rules()
    {
        var e = Create();
        Check(e.Active!.Casts.Length == 9 && NativeTreasureCatalog.Owns(0x3D48, 1123)
            && !NativeTreasureCatalog.Owns(0x3D48, 924), "Narkissos ownership/rules differ.");
        var r = e.Active.Casts.ToDictionary(c => c.Action);
        Check(r[32336].Shape is CircleTemplate { Radius: 8 }
            && r[32333].Shape is CircleTemplate { Radius: 8 }
            && r[32338].Shape is FanTemplate { Radius: 25, AngleRadians: var angle } && angle == MathF.PI / 2
            && r[32339].Shape is DungeonCrossTemplate { Length: 25, HalfWidth: 3 }
            && !r.ContainsKey(32334) && !r.ContainsKey(32335), "Inactive skills or source shape differ.");
        e.StartCast(2, Cast(32332, location: Center + Vector3.UnitZ * 100), Start);
        Check(e.Gazes.Single().Location.Z == 200 && e.AmbientAoes().Any(a => a.Id == "treasure/gaze/view"),
            "Default10000 gaze/double view drawing missing.");
    }
    private static void Sap()
    {
        var e = Create();
        for (var i = 0; i < 5; ++i)
        {
            var cast = Cast(32336, i * 10);
            e.StartCast(2, cast, Start.AddSeconds(i * 10));
            e.StartCast(2, cast with { Location = Center + Vector3.UnitX }, Start.AddSeconds(i * 10 + .1));
            var hint = e.Hints(Center).Single(h => h.StartsWith("Circles resolve"));
            Check(hint.Contains("aim forced march") == (i == 4), "Sap count reset or correction counted as new cast.");
            Check(!e.OutputCasts.Any(c => c.Value.Rule.Action == 32336)
                && e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/sap/")).Color == new Vector4(1, 0, 0, 1),
                "Sap danger color or duplicate ordinary output differs.");
            e.Finish(2, 32336);
            Check(!e.Hints(Center).Any(h => h.StartsWith("Circles resolve")), "Sap Finish did not clear active.");
        }
        e.StartCast(2, Cast(32336, 60), Start.AddSeconds(60));
        e.StartCast(3, Cast(32336, 61), Start.AddSeconds(61));
        e.Finish(2, 32336);
        Check(!e.Hints(Center).Any(h => h.StartsWith("Circles resolve"))
            && e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/sap/")) == 1,
            "Sap active must clear on any Finish even while another source remains.");
    }
    private static void Directions()
    {
        foreach (var (status, direction) in new[] { (1958u, 0f), (1960u, MathF.PI / 2), (1959u, MathF.PI), (1961u, 3 * MathF.PI / 2) })
        {
            var e = Create();
            e.StatusChanged(100, status, true, Start, expiresAt: Start.AddSeconds(100));
            var move = e.TreasureMarchMovements.Single();
            Check(Vector3.Distance(move.To, Center + new Vector3(MathF.Sin(direction), 0, MathF.Cos(direction)) * 13.2f) < .00001f
                && move.Rotation == direction, "Two-second6.6 march direction/distance differs.");
            Check(e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/march/")).All(a => !a.Navigate),
                "Source projection expanded into navigation.");
        }
        var chained = Create();
        chained.StatusChanged(100, 1960, true, Start, expiresAt: Start.AddSeconds(20));
        chained.StatusChanged(100, 1958, true, Start, expiresAt: Start.AddSeconds(10));
        Check(chained.TreasureMarchMovements.First().Rotation == 0
            && chained.TreasureMarchMovements.Last().Rotation == MathF.PI / 2, "March not sorted by activation.");
    }
    private static void Destination()
    {
        var e = Create();
        e.StatusChanged(100, 1958, true, Start, expiresAt: Start.AddSeconds(100));
        e.StartCast(2, Cast(32339), Start);
        Check(!e.Hints(Center).Contains("Aim for safe spot!"), "Cross/arena added to march destination check.");
        e.StartCast(3, Cast(32336, location: Center + Vector3.UnitZ * 13), Start);
        Check(e.Hints(Center).Contains("Aim for safe spot!"), "Sap late clock must still mark unsafe landing.");
        e.Finish(3, 32336);
        e.StatusChanged(100, 1958, true, Start.AddSeconds(1), expiresAt: Start.AddSeconds(200));
        Check(e.TreasureMarchMovements.Last().To.Z > 119 && !e.Hints(Center).Contains("Aim for safe spot!"),
            "Out-of-arena landing was clamped or warned by unrelated boundary.");
    }
    private static void Overlay()
    {
        var e = Create();
        e.StatusChanged(100, 1958, true, Start, expiresAt: Start.AddSeconds(10));
        e.Synchronize([Boss, Player(), Player(101)], Center, Start.AddSeconds(2), playerId: 100);
        Check(e.TreasureMarchMovements.Count() == 1, "Old empty frame revoked network Gain after500ms.");
        e.StatusChanged(100, 1958, false, Start.AddSeconds(3));
        e.StatusChanged(100, 1958, true, Start.AddSeconds(3), expiresAt: Start.AddSeconds(20));
        Check(e.TreasureMarchMovements.Count() == 1, "Same timestamp Lose/Gain suppressed replacement.");
        e.StatusChanged(102, 1960, true, Start.AddSeconds(4), expiresAt: Start.AddSeconds(30));
        e.Synchronize([Boss, Player(), Player(101)], Center, Start.AddSeconds(5), playerId: 100);
        e.Synchronize([Boss, Player(), Player(101), Player(102)], Center, Start.AddSeconds(6), playerId: 102);
        Check(e.TreasureMarchMovements.Count() == 1, "Event for not-yet-observed actor was discarded.");
    }
    private static void Raw()
    {
        var e = Create();
        e.Synchronize([Boss, Player(statuses: [1958], expirations: new Dictionary<uint, DateTime> { [1958] = Start.AddSeconds(10) })],
            Center, Start.AddSeconds(1), playerId: 100);
        e.Synchronize([Boss, Player(expirations: new Dictionary<uint, DateTime> { [1958] = Start.AddSeconds(10.2) })],
            Center, Start.AddSeconds(20), playerId: 100);
        Check(e.TreasureMarchMovements.Count() == 1, "Expired raw slot or small correction appended/removed march.");
        e.Synchronize([Boss, Player()], Center, Start.AddSeconds(21), playerId: 100);
        Check(!e.TreasureMarchMovements.Any(), "Real raw Lose did not clear pending movement.");
    }
    private static void Ongoing()
    {
        foreach (var status in new uint[] { 1257, 3629 })
        {
            var e = Create();
            e.StatusChanged(100, status, true, Start, expiresAt: Start.AddSeconds(2));
            e.Tick(Start.AddSeconds(1));
            Check(MathF.Abs(e.TreasureMarchMovements.Single().To.Z - 106.6f) < .00001f,
                "Ongoing forced movement does not use remaining time6.6.");
            e.StatusChanged(100, status, false, Start.AddSeconds(1));
            Check(!e.TreasureMarchMovements.Any(), "Ongoing real Lose retained projection.");
        }
    }
    private static void Destroy()
    {
        var e = Create();
        e.StatusChanged(100, 1958, true, Start);
        Check(!e.TreasureMarchMovements.Any(), "Missing expiry fabricated a movement clock.");
        e.Synchronize([Boss, Player(statuses: [1958], expirations: new Dictionary<uint, DateTime> { [1958] = Start.AddSeconds(10) })],
            Center, Start.AddSeconds(1), playerId: 100);
        Check(e.TreasureMarchMovements.Count() == 1, "Raw did not resolve unknown network duration.");
        e.RemoveActor(100, Start.AddSeconds(2));
        e.StatusChanged(100, 1958, true, Start.AddSeconds(1), expiresAt: Start.AddSeconds(20));
        e.Synchronize([Boss, Player()], Center, Start.AddSeconds(3), playerId: 100);
        Check(!e.TreasureMarchMovements.Any(), "Late old event crossed destruction.");
        e.StatusChanged(100, 1958, true, Start.AddSeconds(4), expiresAt: Start.AddSeconds(30));
        Check(e.TreasureMarchMovements.Count() == 1, "New generation event rejected after destruction.");
    }
    private static void Misdirection()
    {
        var e = Create();
        foreach (var status in new uint[] { 1422, 2936, 3694, 3909 })
        {
            e.StatusChanged(100, status, true, Start, param: 0xffff, expiresAt: Start.AddSeconds(1));
            e.Tick(Start.AddSeconds(100));
            Check(e.TreasureMisdirectionActive, "Misdirection expired by timer or ignored valid ID/param.");
        }
        e.StatusChanged(100, 1422, false, Start.AddSeconds(101));
        Check(!e.TreasureMisdirectionActive, "Any Lose must clear bit even with other status IDs held.");
        e.Synchronize([Boss, Player()], Center, Start.AddSeconds(102), playerId: 101);
        Check(!e.TreasureMisdirectionActive, "Perspective change retained previous actor diagnostic.");
    }
    private static ReplayActorState StatusActor(ulong source, DateTime expiration, ushort param = 0) => new()
    {
        Id = 100, Position = Center, Statuses = [new(1958, param, source, expiration)],
    };
    private static void OldRawAfterDestroy()
    {
        var state = new TreasurePlayerStatusState();
        var actor = StatusActor(1, Start.AddSeconds(10));
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = actor }, Start);
        state.Remove(100, Start.AddSeconds(1));
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = actor }, Start.AddSeconds(1.1));
        Check(!state.Movements(actor, Start).Any(), "Destroyed status re-created by identical stale raw frame.");
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = actor with { Statuses = [] } }, Start.AddSeconds(2));
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = actor }, Start.AddSeconds(3));
        Check(state.Movements(actor, Start).Count() == 1, "Confirmed new raw state rejected after old occupied slot disappeared.");
    }
    private static void RawFirstReplacement()
    {
        var state = new TreasurePlayerStatusState();
        var a = StatusActor(1, Start.AddSeconds(10));
        var b = StatusActor(2, Start.AddSeconds(20));
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = a }, Start);
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = b }, Start.AddSeconds(1));
        state.Status(100, 1958, false, Start.AddSeconds(1), 0, null, 1);
        state.Status(100, 1958, true, Start.AddSeconds(1), 0, Start.AddSeconds(20), 2);
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = b }, Start.AddSeconds(2));
        Check(state.Movements(b, Start).Count() == 1, "Raw-first replacement swallowed matching network Gain.");
    }
    private static void UnknownRaw()
    {
        foreach (var status in new uint[] { 1958, 1257, 3629 })
        {
            var state = new TreasurePlayerStatusState();
            var unknown = new ReplayActorState { Id = 100, Position = Center, Statuses = [new(status, 0, 1, DateTime.MaxValue)] };
            state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = unknown }, Start);
            Check(!state.Movements(unknown, Start).Any(), "Unknown raw expiry fabricated forced movement.");
            var known = unknown with { Statuses = [new(status, 0, 1, Start.AddSeconds(10))] };
            state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = known }, Start.AddSeconds(1));
            Check(state.Movements(known, Start).Count() == 1, "Finite raw clock did not resolve previously unknown expiry.");
        }
    }
    private static void UnknownIdentity()
    {
        var state = new TreasurePlayerStatusState();
        state.Status(100, 1958, true, Start, 2, null, 2);
        var old = StatusActor(1, Start.AddSeconds(10));
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = old }, Start.AddSeconds(1));
        Check(!state.Movements(old, Start).Any(), "Unknown event expiry discarded source/param identity.");
        var accepted = StatusActor(2, Start.AddSeconds(20), 2);
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = accepted }, Start.AddSeconds(2));
        Check(state.Movements(accepted, Start).Count() == 1, "Matching raw failed to provide unknown event's clock.");
    }
    private static void SkippedCycle()
    {
        var state = new TreasurePlayerStatusState();
        var empty = new ReplayActorState { Id = 100, Position = Center };
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = empty }, Start);
        state.Status(100, 1958, true, Start.AddSeconds(1), 0, Start.AddSeconds(10), 1);
        state.Status(100, 1958, false, Start.AddSeconds(2), 0, null, 1);
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = empty }, Start.AddSeconds(2.1));
        var old = StatusActor(1, Start.AddSeconds(10));
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = old }, Start.AddSeconds(3));
        Check(!state.Movements(old, Start).Any(), "Old skipped Gain raw frame revoked real Lose.");
        var next = StatusActor(1, Start.AddSeconds(30));
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = next }, Start.AddSeconds(4));
        Check(state.Movements(next, Start).Count() == 1, "Old pending Lose swallowed distinct new raw Gain.");
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = next }, Start.AddSeconds(5));
        Check(state.Movements(next, Start).Count() == 1, "Stable new raw duplicated pending march.");
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = empty }, Start.AddSeconds(6));
        Check(!state.Movements(next, Start).Any(), "New raw generation true Lose was not consumed.");
    }
    private static void DestroyWatermark()
    {
        var state = new TreasurePlayerStatusState();
        var actor = new ReplayActorState { Id = 100, Position = Center };
        state.Remove(100, Start.AddSeconds(2));
        state.Remove(100, Start.AddSeconds(1));
        state.Status(100, 1257, true, Start.AddSeconds(1.5), 0, Start.AddSeconds(10), 1);
        Check(!state.Movements(actor, Start).Any(), "Late duplicate Remove lowered destruction watermark.");
    }
    private static void HistoricalReceipt()
    {
        var state = new TreasurePlayerStatusState();
        ReplayActorState Actor(ulong source, double expiration) => new()
        {
            Id = 100, Position = Center, Statuses = [new(1257, 0, source, Start.AddSeconds(expiration))],
        };
        var a = Actor(1, 10);
        var b = Actor(2, 20);
        var c = Actor(3, 30);
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = a }, Start);
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = b }, Start.AddSeconds(1));
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = c }, Start.AddSeconds(2));
        state.Status(100, 1257, false, Start.AddSeconds(1), 0, null, 1);
        state.Status(100, 1257, true, Start.AddSeconds(1), 0, Start.AddSeconds(20), 2);
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = c }, Start.AddSeconds(3));
        Check(MathF.Abs(state.Movements(c, Start.AddSeconds(3)).Single().To.Z - (100 + 6.6f * 27)) < .0001f,
            "Historical receipt pair rolled newer forced end back to old source.");
    }
    private static void Rewards()
    {
        var e = Create();
        e.StartCast(2, Cast(32340), Start);
        var aoes = e.AmbientAoes().Where(a => a.Id.StartsWith("quest/spread/")).ToArray();
        Check(aoes.Single(a => a.Display).Shape is CircleTemplate { Radius: 6 }
            && aoes.Single(a => a.Navigate).Shape is CircleTemplate { Radius: 7 }
            && !e.PredictedDamage.Any(), "Narkissos spread6/7/no forecast differs.");
        e.Synchronize([Boss with { Dead = true }, new(4, 0x3D4D, Center), Player()], Center,
            Start.AddSeconds(1), playerId: 100);
        Check(e.Active != null && NativeTreasureCatalog.Priority(0x3D48, 0x3D4D) == 2
            && NativeTreasureCatalog.Vulnerable(0x3D48, 0x3D4E), "Reward phase/priority/color differs.");
        e.Synchronize([new(4, 0x233C, Center), Player()], Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active == null && !e.TreasureMarchMovements.Any(), "Helper prolonged source lifecycle.");
    }

    private static void RawFirstLoseBarrier()
    {
        var state = new TreasurePlayerStatusState();
        var a = StatusActor(1, Start.AddSeconds(10));
        var empty = a with { Statuses = [] };
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = a }, Start);
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = empty }, Start.AddSeconds(2));
        state.Status(100, 1958, false, Start.AddSeconds(1), 0, null, 1);
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = a }, Start.AddSeconds(3));
        Check(!state.Movements(a, Start).Any(), "Raw-first Lose receipt failed to block old occupied frame.");
        state.Status(100, 1958, true, Start.AddSeconds(4), 0, Start.AddSeconds(10), 1);
        Check(state.Movements(a, Start).Count() == 1, "A real matching Gain failed to release old barrier.");
    }

    private static void UnknownLoseBarrier()
    {
        var state = new TreasurePlayerStatusState();
        var a = StatusActor(1, Start.AddSeconds(10));
        var empty = a with { Statuses = [] };
        state.Status(100, 1958, true, Start, 0, null, 1);
        state.Status(100, 1958, false, Start.AddSeconds(1), 0, null, 1);
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = empty }, Start.AddSeconds(2));
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = a }, Start.AddSeconds(3));
        Check(!state.Movements(a, Start).Any(), "Finite old raw revived unknown-clock removed generation.");
    }

    private static void DistinctRawBarrier()
    {
        var state = new TreasurePlayerStatusState();
        var a = StatusActor(1, Start.AddSeconds(30));
        var b = StatusActor(1, Start.AddSeconds(50));
        var empty = a with { Statuses = [] };
        state.Status(100, 1958, true, Start, 0, Start.AddSeconds(30), 1);
        state.Status(100, 1958, false, Start.AddSeconds(1), 0, null, 1);
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = b }, Start.AddSeconds(2));
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = empty }, Start.AddSeconds(3));
        state.Observe(new Dictionary<ulong, ReplayActorState> { [100] = a }, Start.AddSeconds(4));
        Check(state.Movements(a, Start).Count() == 1, "Accepted distinct raw left the old event generation barrier behind.");
    }
}
