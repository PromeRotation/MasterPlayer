using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureHexapodRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static DungeonActor Player => new(100, 0, Center, PartySlot: 0, Player: true, Hostile: false);
    private static DungeonEncounter Create(Vector3? player = null, params DungeonActor[] extra)
    {
        var e = new DungeonEncounter(1279) { TreasurePartyInput = [100, 101] };
        e.Synchronize([new(1, 0x48B3, Center, InCombat: true), Player with { Position = player ?? Center },
            Player with { Id = 101, PartySlot = 1, Position = Center + Vector3.UnitZ * 15 },
            .. Enumerable.Range(2, 6).Select(i => new DungeonActor((ulong)i, 0x233C, Center)), .. extra],
            player ?? Center, Start, playerId: 100);
        return e;
    }
    private static DungeonCastObservation Cast(uint action, ulong target = 100, Vector3? p = null, double at = 0)
        => new(action, p ?? Center, .3f, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), target);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static DungeonAmbientAoe[] Aoes(DungeonEncounter e, string key) => e.AmbientAoes().Where(a => a.Id.StartsWith(key)).ToArray();
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure Hexapod fifteen source casts exact geometry and source reward ownership", Rules);
        run("native treasure Hexapod mine locked quantization actor destruction retention native effect first match", Mines);
        run("native treasure Hexapod bomb Stay ExpireAt display threshold2.5 no premature execution and actual Lose", Bomb);
        run("native treasure Hexapod PoleShift slot charges frozen pull priority immune ignored global cancel", Pole);
        run("native treasure Hexapod PoleShift push inverse13 square and pull18 actual navigation", PoleNavigation);
        run("native treasure Hexapod march four directions19.8 wall each segment first activation and real Lose", March);
        run("native treasure Hexapod mine first pending relative forbidden directions and fixed18 ray text", Directions);
        run("native treasure Hexapod target circle5 effect retained Finish clears and target damage clock", Bait);
        run("native treasure Hexapod bootstrap interleaved cast-status actor order and dead Bomb revocation", Bootstrap);
    }
    private static void Rules()
    {
        var e = Create();
        uint[] ids = [43755, 43756, 43769, 43770, 43759, 43768, 43754, 43752, 43727, 43726, 32301, 32302, 32303, 32304, 32305];
        var rules = e.Active!.Casts.ToDictionary(c => c.Action);
        Check(rules.Keys.Order().SequenceEqual(ids.Order()) && rules[43755].Shape is CircleTemplate { Radius: 10 }
            && rules[43756].Shape is RayRectangleTemplate { Length: 40, MaxLocalX: 3 }
            && rules[43769].Shape is FanTemplate { Radius: 30, AngleRadians: 2 * MathF.PI / 3 }
            && rules[43759].Shape is CircleTemplate { Radius: 5 } && rules[43770].Shape == null
            && NativeTreasureCatalog.Owns(0x48B3, 1279) && !NativeTreasureCatalog.Owns(0x48B4, 1279)
            && NativeTreasureCatalog.Priority(0x48B3, 0x48B9) == 6
            && NativeTreasureCatalog.Priority(0x48B3, 0x48BD) == 2
            && NativeTreasureCatalog.Priority(0x48B3, 0x48B8) == 1, "Active rule set/ownership/reward priority differs.");
    }
    private static void Mines()
    {
        var p = Center + new Vector3(.123f, 0, .234f);
        var e = Create(extra: [new(30, 0x48B4, p)]);
        var initial = e.TreasureHexMines.Single();
        Check(initial.Position != p && Aoes(e, "treasure/hex/mine/").Single() is { Obstacle: true, Navigate: true }, "Mine must quantize once and publish obstacle.");
        e.RemoveActor(30);
        e.Tick(Start.AddSeconds(100));
        Check(e.TreasureHexMines.Count == 1, "Mine actor destruction/timeout cleared source AOE.");
        e.ObjectCreated(30, Start.AddSeconds(101), 0x48B4, Center + Vector3.UnitX * 10);
        Check(e.TreasureHexMines.Count == 2, "Same-ID new mine did not append.");
        e.TreasureEffect(30, 43771, 1, 100);
        Check(e.TreasureHexMines.Count == 1 && e.TreasureHexMines[0].Order != initial.Order, "Mine effect must consume first source match.");
        e.TreasureEffect(30, 43771, 1, 100);
        Check(e.TreasureHexMines.Count == 1, "Duplicate Native effect consumed second mine.");
        e.TreasureEffect(30, 43771, 2, 100);
        Check(e.TreasureHexMines.Count == 0, "Second real effect failed.");
    }
    private static void Bomb()
    {
        var e = Create();
        e.StatusChanged(100, 3802, true, Start, expiresAt: Start.AddSeconds(10));
        Check(e.TreasureHexBombs[0] == Start.AddSeconds(10) && !e.Hints(Center).Contains("Stop everything!")
            && e.Hints(Center).Any(h => h.StartsWith("SpecialMode: Pyretic")), "Bomb activation or diagnostic missing.");
        e.Tick(Start.AddSeconds(7.5));
        Check(e.Hints(Center).Contains("Stop everything!") && !e.RequireAllStop,
            "2.5s text window expanded into premature PR/MP execution.");
        e.Tick(Start.AddSeconds(20));
        Check(e.TreasureHexBombs.ContainsKey(0), "ExpireAt synthesized Lose.");
        e.StatusChanged(100, 3802, false, Start.AddSeconds(21));
        Check(e.TreasureHexBombs.Count == 0, "Bomb real Lose retained state.");
        Check(!e.TreasureHexMarchMovements.Any(), "Bomb was interpreted as forward march.");
    }
    private static void Pole()
    {
        var e = Create(Center + Vector3.UnitX * 15);
        e.StatusChanged(100, 4551, true, Start);
        e.StatusChanged(100, 4552, true, Start);
        e.StatusChanged(101, 4552, true, Start);
        e.StartCast(2, Cast(43754), Start);
        Check(e.TreasureHexPoles[0] is { Pull: true, Distance: 7, MinDistance: 1, IgnoreImmunes: true, StopAtWall: true }
            && !e.TreasureHexPoles[1].Pull, "Minus signs, pull precedence or explicit parameters differ.");
        e.StatusChanged(100, 4551, false, Start.AddSeconds(1));
        Check(e.TreasureHexPoles[0].Pull, "Charge loss altered a locked displacement.");
        e.StartCast(3, Cast(43752, at: 1), Start.AddSeconds(1));
        Check(e.TreasureHexPoles[0].Pull && e.TreasureHexPoles[1].Pull, "Plus did not pull negative.");
        e.Finish(2, 43754, Start.AddSeconds(2), completed: false);
        Check(e.TreasureHexPoles.Count == 0, "Any PoleShift Finish/cancel did not clear all slots.");
        Check(!e.TreasureHexMarchMovements.Any(), "Charges were interpreted as forced march.");
    }
    private static void PoleNavigation()
    {
        var e = Create(Center + Vector3.UnitX * 15);
        e.StatusChanged(100, 4551, true, Start);
        e.StartCast(2, Cast(43754), Start);
        var pull = Aoes(e, "treasure/hex/pole/avoid").Single();
        Check(pull.Shape is CircleTemplate { Radius: 18 } && pull is { Navigate: true, Display: false }
            && pull.Activation == Start.AddSeconds(5.3), "Pull explicit circle18 navigation differs.");
        e.StartCast(3, Cast(43752, at: 1), Start.AddSeconds(1));
        var push = Aoes(e, "treasure/hex/pole/avoid").Single();
        var shape = push.Shape.Resolve(push.Position, 0, null);
        Check(shape.Contains(Center + Vector3.UnitX * 7, 0)
            && !shape.Contains(Center + Vector3.UnitX * 5, 0), "Push did not inverse-project seven into half13 square.");
        var markers = Aoes(e, "treasure/hex/pole");
        Check(markers.Where(a => a.Display).All(a => !a.Navigate), "Projection arrows became path constraints.");
    }
    private static void March()
    {
        foreach (var (status, direction) in new[] { (2161u, 0f), (2162u, MathF.PI), (2163u, MathF.PI / 2), (2164u, 3 * MathF.PI / 2) })
        {
            var e = Create();
            e.StatusChanged(100, status, true, Start, expiresAt: Start.AddSeconds(100));
            var move = e.TreasureHexMarchMovements.Single();
            Check(MathF.Abs(move.Rotation - direction) < .00001 && Vector3.Distance(move.From, move.To) < 19.8f
                && Vector3.Distance(move.From, move.To) > 19.4f && e.Arena!.Contains(move.To, 0), "March direction19.8 or wall clamp differs.");
            e.Tick(Start.AddSeconds(150));
            Check(e.TreasureHexMarchMovements.Any(), "Pending march expired without real Lose.");
            e.StatusChanged(100, status, false, Start.AddSeconds(151));
            Check(!e.TreasureHexMarchMovements.Any(), "March real Lose retained pending segment.");
        }
        var chained = Create();
        chained.StatusChanged(100, 2161, true, Start, expiresAt: Start.AddSeconds(10));
        chained.StatusChanged(100, 2163, true, Start, expiresAt: Start.AddSeconds(20));
        var moves = chained.TreasureHexMarchMovements.ToArray();
        Check(moves.Length == 2 && moves[1].From == moves[0].To && chained.Arena!.Contains(moves[1].To, 0),
            "Each march segment was not stopped separately at the real wall.");
    }
    private static void Directions()
    {
        var e = Create();
        e.ObjectCreated(30, Start, 0x48B4, Center + Vector3.UnitZ * 12);
        e.StatusChanged(100, 2161, true, Start, expiresAt: Start.AddSeconds(10));
        var d = e.TreasureHexDirections(Center).Single();
        var mine = e.TreasureHexMines.Single().Position - Center;
        Check(d.Activation == Start.AddSeconds(10) && MathF.Abs(d.Center - MathF.Atan2(mine.X, mine.Z)) < .00001
            && MathF.Abs(d.HalfWidth - MathF.Asin(5 / new Vector2(mine.X, mine.Z).Length())) < .00001,
            "Forbidden direction lost first relative direction, width or activation.");
        e.Tick(Start.AddSeconds(9.5));
        Check(e.GazeFacing(Center, d.Center) is { } safe
            && MathF.Abs(MathF.Atan2(MathF.Sin(safe - d.Center), MathF.Cos(safe - d.Center))) > d.HalfWidth,
            "PR facing did not honor exact forbidden arc.");
        Check(e.Hints(Center).Contains("Aim for safe spot!"), "Fixed18 ray failed mine intersection.");
        var far = Create();
        far.ObjectCreated(30, Start, 0x48B4, Center + Vector3.UnitZ * 23.5f);
        far.StatusChanged(100, 2161, true, Start, expiresAt: Start.AddSeconds(10));
        Check(far.TreasureHexDirections(Center).Any() && !far.Hints(Center).Contains("Aim for safe spot!"),
            "18-ray text incorrectly used 19.8 movement length.");
    }
    private static void Bait()
    {
        var e = Create();
        e.StartCast(1, Cast(43768, target: 101), Start);
        Check(Aoes(e, "treasure/cast-bait/").First().Shape is CircleTemplate { Radius: 5 }
            && e.PredictedDamage.Single() is { TargetId: 101, DamageType: "Tankbuster" }, "Artillery dynamic target circle/damage differs.");
        e.TreasureEffect(1, 43768, 1, 101);
        Check(Aoes(e, "treasure/cast-bait/").Length > 0, "Native effect consumed Finish-only bait.");
        e.Finish(1, 43768, Start.AddSeconds(6));
        Check(Aoes(e, "treasure/cast-bait/").Length == 0, "Artillery Finish failed source FIFO.");
    }
    private static void Bootstrap()
    {
        var boss = new DungeonActor(1, 0x48B3, Center, InCombat: true);
        var helper = new DungeonActor(2, 0x233C, Center, Cast: Cast(43754));
        var player = Player with { Statuses = [4551], StatusExpirations = new Dictionary<uint, DateTime> { [4551] = Start.AddSeconds(30) } };
        foreach (var statusFirst in new[] { false, true })
        {
            var e = new DungeonEncounter(1279) { TreasurePartyInput = [100] };
            e.Synchronize(statusFirst ? [boss, player, helper] : [boss, helper, player], Center, Start, playerId: 100);
            Check(e.TreasureHexPoles.Count == (statusFirst ? 1 : 0), "Bootstrap applied later actor charge before an earlier cast.");
        }
        var bomb = Player with { Statuses = [3802], StatusExpirations = new Dictionary<uint, DateTime> { [3802] = Start.AddSeconds(10) } };
        var encounter = Create(extra: [bomb]);
        Check(encounter.TreasureHexBombs.Count == 1, "Bomb bootstrap failed.");
        encounter.Synchronize([boss, bomb with { Dead = true }], Center, Start.AddSeconds(1), playerId: 100);
        encounter.Synchronize([boss, bomb], Center, Start.AddSeconds(2), playerId: 100);
        Check(encounter.TreasureHexBombs.Count == 0, "Revival restored old Bomb without a fresh Gain.");
    }
}
