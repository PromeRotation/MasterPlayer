using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NoviceTacticalRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static DungeonActor Boss => new(1, 0x463B, default, Targetable: false, Hp: 1000);
    private static DungeonActor Player => new(100, 0, new(0, 0, 8), Hostile: false, Player: true, PartySlot: 0);
    private static DungeonActor Buddy => new(101, 0, new(10, 0, 8), Hostile: false, Player: true, PartySlot: 1);
    private static DungeonActor Helper(ulong id) => new(id, 0x233C, default);
    private static DungeonEncounter Encounter(uint territory = 1127, params DungeonActor[] extras)
    {
        var encounter = new DungeonEncounter(territory);
        encounter.Synchronize([Boss, Player, Buddy, .. extras], Player.Position, Start, playerId: 100);
        return encounter;
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Cast(DungeonEncounter e, uint action, ulong source = 1, double at = 0, double finish = 8,
        Vector3 position = default, float rotation = 0)
        => e.StartCast(source, new(action, position, rotation, Start.AddSeconds(at), Start.AddSeconds(finish), 100), Start.AddSeconds(at));
    private static DungeonAmbientAoe[] Aoes(DungeonEncounter e, string id)
        => e.AmbientAoes().Where(a => a.Id.StartsWith("tactical/" + id, StringComparison.Ordinal)).ToArray();
    internal static void Run(Action<string, Action> run)
    {
        run("native novice tactical three territories untargetable precombat pull completion and owner", Lifecycle);
        run("native novice tactical starting maps sticky radius complete director tuples", Ready);
        run("native novice tactical nine ordinary shapes true Finish no expiry and unclaimed cast", Ordinary);
        run("native novice tactical four independent marks main target clearing and NPC navigation", Marks);
        run("native novice tactical chasers independent target FIFO first locked hit five real effects", Chasers);
        run("native novice tactical chaser destruction freezes old generation before first effect", ChaserGeneration);
        run("native novice tactical ring consumption clocks global FIFO and fan strict prefix", Waves);
        run("native novice tactical two independent tower unions NPC mask first component activation", Towers);
        run("native novice tactical icon bait raw positions source reference role target death", Bait);
        run("native novice tactical tether source centered dead target shared activation separate buddies", Tethers);
        run("native novice tactical bomb duplicate count untether unresolved frozen identity", Intercept);
        run("native novice tactical knockback independent components strict immunity and gaze", Knockbacks);
        run("native novice tactical raw event tether confirmations preserve real duplicate events", TetherReceipts);
        run("native novice tactical self stack separates other stacks raw buddy radius and spread damage hints", StackParity);
        run("native novice tactical knockback independent safe landings strict expiry gain lose before snapshot", Immunity);
    }
    private static void Lifecycle()
    {
        foreach (var territory in new uint[] { 1127, 1128, 1129 })
        {
            var e = Encounter(territory);
            Check(e.Active?.BossOid == 0x463B && e.Arena!.Contains(new(19, 0, 0), 0), "Tactical source pull requires neither targetability nor combat.");
            e.DirectorUpdate(1);
            Check(e.Active != null, "TrialComplete ended source module.");
            e.Synchronize([Boss with { Hp = 0 }, Player], Player.Position, Start.AddSeconds(1), playerId: 100);
            e.Synchronize([Boss, Player], Player.Position, Start.AddSeconds(2), playerId: 100);
            Check(e.Active == null, "Completed actor restarted without new generation.");
            e.RemoveActor(1);
            e.Synchronize([Boss, Player], Player.Position, Start.AddSeconds(3), playerId: 100);
            Check(e.Active != null, "New actor generation did not reactivate.");
            e.Synchronize([Boss, Player], Player.Position, Start.AddSeconds(4), _ => false, 100);
            Check(e.Active == null && !Aoes(e, "").Any(), "Disabled owner retained outputs.");
        }
    }
    private static void Ready()
    {
        var npc = new DungeonActor(20, 0x100B76, default, Targetable: false);
        var e = Encounter(1128, npc, npc with { Id = 21, Targetable = true });
        e.MapEffect(0x18, 0x20001);
        Check(Aoes(e, "ready/display").Single().Shape is CircleTemplate { Radius: 4 }, "Special radius missing.");
        e.MapEffect(0, 0x20001);
        Check(Aoes(e, "ready/display").Single().Shape is CircleTemplate { Radius: 4 }, "Ordinary map reset sticky radius.");
        e.DirectorUpdate(0x80000015, 5, 0, 1);
        Check(e.TacticalInteractionTarget == 0, "Incomplete director tuple accepted.");
        e.DirectorUpdate(0x80000015, 5);
        Check(e.TacticalInteractionTarget == 0, "Unselectable first NPC incorrectly fell through.");
        e.RemoveActor(20);
        Check(e.TacticalInteractionTarget == 21, "Interaction intention failed after first NPC removed.");
        e.MapEffect(0, 0x80004);
        Check(Aoes(e, "ready").Length == 0, "Clear map retained ready geometry.");
    }
    private static void Ordinary()
    {
        var e = Encounter();
        foreach (var (action, radius) in new[] { (40697u, 15f), (40710u, 5f), (40713u, 18f), (40730u, 20f),
            (40723u, 10f), (40725u, 5f), (40747u, 6f), (40763u, 15f) })
        {
            Cast(e, action);
            e.Tick(Start.AddSeconds(40));
            Check(e.OutputCasts.Single().Value.Rule.Shape is CircleTemplate circle && circle.Radius == radius,
                "Ordinary geometry expired before observed Finish.");
            e.Finish(1, action);
            e.ResetFight();
            e.Synchronize([Boss, Player, Buddy], Player.Position, Start, playerId: 100);
        }
        Cast(e, 40700);
        Check(e.OutputCasts.Single().Value.Rule.Shape is DonutTemplate { InnerRadius: 8, OuterRadius: 25 }, "Donut differs.");
        e.Finish(1, 40700);
        Cast(e, 90000, at: 1, finish: 9);
        Check(!e.Definitions.SelectMany(d => d.Casts).Any(c => c.Action == 90000)
            && e.OutputCasts.All(c => c.Value.Rule.Shape == null), "Observation expanded generic ownership.");
    }
    private static void Marks()
    {
        var e = Encounter();
        foreach (var icon in new uint[] { 139, 375, 318, 317 }) e.Icon(100, icon, Start, 100);
        Check(Aoes(e, "mark").Count(a => a.Display) == 4, "Four mark components conflated.");
        e.Effect(1, 40692, 1, Start.AddSeconds(1), 999);
        Check(Aoes(e, "mark").Count(a => a.Display) == 2, "Independent spreads did not global clear / fallback FIFO.");
        e.Effect(1, 40682, 2, Start.AddSeconds(2), 100);
        Check(Aoes(e, "mark").Length == 0, "Both stack components failed main target clear.");
        e.Icon(100, 139, Start, 100);
        e.Synchronize([Boss, Player with { Dead = true }, Buddy], Player.Position, Start.AddSeconds(3), playerId: 100);
        e.Synchronize([Boss, Player, Buddy], Player.Position, Start.AddSeconds(4), playerId: 100);
        Check(Aoes(e, "mark").Length == 0, "Dead spread resurrected.");
    }
    private static void Chasers()
    {
        var e = Encounter(1127, Helper(10), Helper(11));
        e.Icon(101, 197, Start, 100);
        Cast(e, 40689, 10, position: new(2, 0, 0));
        Cast(e, 40702, 11, position: new(3, 0, 0));
        var shown = Aoes(e, "chaser").Where(a => a.Id.EndsWith("/display")).ToArray();
        Check(shown.Length == 2 && shown.All(a => MathF.Abs(a.Position.Z) < .02f),
            "Independent icon targets were consumed across components or first prediction followed actor.");
        Check(Aoes(e, "chaser").Count(a => a.Shape is CircleTemplate { Radius: 9 }) == 2, "First hit lost self R9.");
        for (var i = 0; i < 5; ++i) e.Effect(99, 40690, (uint)i, Start.AddSeconds(i + 1), 101);
        Check(Aoes(e, "chaser").Count(a => a.Id.EndsWith("/display")) == 1, "Effects did not consume exactly own five hits.");
        e.Effect(99, 40703, 0, Start.AddSeconds(10), 101);
        Check(Aoes(e, "chaser").Single(a => a.Id.EndsWith("/display")).Activation == Start.AddSeconds(12.5), "Continuation did not use consumption clock.");
    }
    private static void ChaserGeneration()
    {
        var e = Encounter(1127, Helper(10));
        e.Icon(100, 197, Start, 100);
        Cast(e, 40689, 10);
        e.RemoveActor(100);
        e.Synchronize([Boss, Player with { Position = new(50, 0, 50) }, Buddy, Helper(10)], Player.Position, Start.AddSeconds(1), playerId: 100);
        Check(Aoes(e, "chaser").Single(a => a.Id.EndsWith("/display")).Position.Length() < .03f, "Destroyed target rebound before first effect.");
        e.Effect(10, 40690, 1, Start.AddSeconds(2), 100);
        e.Synchronize([Boss, Player, Buddy, Helper(10)], Player.Position, Start.AddSeconds(3), playerId: 100);
        Check(Aoes(e, "chaser").Length == 0, "Destroyed chaser survived after its first real effect.");
    }
    private static void Waves()
    {
        var e = Encounter(1127, Helper(10), Helper(11), Helper(12));
        Cast(e, 40760, 10, finish: 1);
        Cast(e, 40694, 11, finish: 20);
        Check(Aoes(e, "aoe").Single().Activation == Start.AddSeconds(7), "Rings used Finish rather than consumption time.");
        e.Finish(11, 40694);
        Check(Aoes(e, "aoe").Length == 1, "Ring cleared on Finish.");
        e.Effect(99, 40759, 1, Start.AddSeconds(2));
        Check(Aoes(e, "aoe").Single().Activation == Start.AddSeconds(13.5), "Ring did not consume global head.");
        Cast(e, 40752, 11, at: 3, finish: 10);
        Cast(e, 40753, 12, at: 3, finish: 11);
        Check(Aoes(e, "aoe").Length == 2, "Fan strict one-second prefix accepted equal boundary.");
        e.Finish(11, 40752);
        Check(Aoes(e, "aoe").Length == 2, "Fan head did not advance.");
    }
    private static void Towers()
    {
        var npc = new DungeonActor(20, 0xAAA, new(2, 0, 0), FriendlyNpc: true, Hostile: false, IsEnemy: false);
        var e = Encounter(1127, Helper(10), Helper(11), Helper(12), npc);
        Cast(e, 40716, 10, finish: 8, position: new(2, 0, 0));
        Cast(e, 40716, 11, finish: 10, position: new(-8, 0, 0));
        Cast(e, 40727, 12, finish: 12, position: new(8, 0, 0));
        Check(Aoes(e, "tower").Count(a => !a.Display) == 2, "Tower components failed separate unions.");
        var prediction = e.PredictedDamage.Single();
        Check(prediction.TargetId == 20 && prediction.Activation == Start.AddSeconds(8), "NPC tower mask/first deadline differs.");
        e.Finish(10, 40716);
        Check(Aoes(e, "tower").Count(a => a.Display) == 2, "Finish failed source-specific tower removal.");
    }
    private static void Bait()
    {
        var e = Encounter();
        e.Icon(101, 412, Start, 100);
        var display = Aoes(e, "bait").Single(a => a.Display);
        Check(display.Activation == Start.AddSeconds(5.1) && display.Position == Boss.Position, "Icon target or default delay differs.");
        e.Effect(99, 40685, 1, Start.AddSeconds(1), 100);
        Check(Aoes(e, "bait").Any(a => a.Display), "Wrong main target removed bait.");
        e.Effect(99, 40685, 2, Start.AddSeconds(2), 101);
        Check(Aoes(e, "bait").Length == 0, "Main target failed remove-all.");
    }
    private static void Tethers()
    {
        var e = Encounter(1127, Helper(10), Helper(11));
        e.Tether(100, 57, true, Start, 10);
        e.Tether(101, 57, true, Start.AddSeconds(2), 11);
        Check(Aoes(e, "tether").Count(a => a.Display && a.Activation == Start) == 2, "Tether activation was not shared.");
        Check(Aoes(e, "tether").Any(a => a.Id.Contains("/separate/101")), "Self circle did not separate buddies.");
        e.Synchronize([Boss, Player with { Dead = true }, Buddy, Helper(10), Helper(11)], Player.Position, Start.AddSeconds(3), playerId: 100);
        Check(Aoes(e, "tether").Count(a => a.Id.EndsWith("/display")) == 2, "Dead target hid allowed tether.");
        e.Tether(100, 57, false, Start.AddSeconds(4), 10);
        Check(Aoes(e, "tether").Count(a => a.Id.EndsWith("/display")) == 1, "Untether failed pair-first removal.");
    }
    private static void Intercept()
    {
        var bomb = new DungeonActor(20, 0x4640, new(10, 0, 0));
        var e = Encounter(1127, bomb);
        e.Tether(20, 84, true, Start, 1);
        Check(Aoes(e, "intercept").Length == 2, "Single bomb failed navigation and display.");
        e.Tether(20, 84, true, Start, 1);
        Check(Aoes(e, "intercept").Length == 0, "Duplicate bomb was deduplicated.");
        e.Tether(20, 84, false, Start, 1);
        Check(Aoes(e, "intercept").Length == 2, "Untether failed remove-first reference.");
        e.RemoveActor(20);
        e.Tether(20, 84, false, Start, 1);
        Check(e.Hints(Player.Position).Contains("Grab the tether!"), "Unresolved untether removed frozen reference.");
    }
    private static void Knockbacks()
    {
        var e = Encounter(1127, Helper(10), Helper(11));
        Cast(e, 40706, 10);
        Cast(e, 40726, 11);
        Check(Aoes(e, "kb").Count(a => a.Id.EndsWith("/enter")) == 2, "Extra positioning disappeared.");
        Check(e.Hints(Player.Position).Contains("About to be knocked into danger!"), "Unsafe knockback hint missing.");
        Cast(e, 40743, at: 1, finish: 8, position: new(0, 0, 500));
        e.Tick(Start.AddSeconds(7.8));
        Check(e.GazeFacing(Player.Position, 0) != null
            && Aoes(e, "gaze").Length == 3, "10000 gaze facing/eye/arc missing.");
    }
    private static void TetherReceipts()
    {
        var bomb = new DungeonActor(20, 0x4640, new(10, 0, 0));
        var boss = Boss with { Tethers = new Dictionary<uint, ulong> { [84] = 20 } };
        var e = new DungeonEncounter(1127);
        e.Synchronize([boss, bomb, Player, Buddy], Player.Position, Start, playerId: 100);
        Check(Aoes(e, "intercept").Length == 2, "Raw tether bootstrap missing.");
        e.Tether(20, 84, true, Start, 1);
        Check(Aoes(e, "intercept").Length == 2, "Raw followed by event duplicated one source transition.");
        e.Tether(20, 84, true, Start.AddSeconds(1), 1);
        Check(Aoes(e, "intercept").Length == 0, "Second real event incorrectly deduplicated.");
        e.Synchronize([Boss, bomb, Player, Buddy], Player.Position, Start.AddSeconds(2), playerId: 100);
        e.Tether(20, 84, false, Start.AddSeconds(2), 1);
        Check(Aoes(e, "intercept").Length == 2, "Raw untether followed by event consumed two references.");
    }
    private static void StackParity()
    {
        var rawBuddy = Buddy with { Position = default };
        var e = Encounter(1127, new DungeonActor(102, 0, new(0, 0, 14), Hostile: false, Player: true, PartySlot: 2));
        e.Synchronize([Boss, Player, rawBuddy], Player.Position, Start, playerId: 100);
        e.Icon(100, 318, Start, 100);
        var enter = Aoes(e, "mark").Single(a => !a.Display);
        Check(enter.Shape.Resolve(enter.Position, 0, null).Contains(new(2.995f, 0, 0), 0) == false,
            "Raw companion R3 region gained forbidden interior at a legal source point.");
        e.Icon(101, 318, Start, 101);
        Check(Aoes(e, "mark").Any(a => a.Shape is CircleTemplate { Radius: 12 } && !a.Display)
            && !Aoes(e, "mark").Any(a => a.Id.EndsWith("/join")), "Self stack incorrectly required joining another stack.");
        e.Icon(100, 139, Start, 100);
        Check(e.PredictedDamage.Any(d => d.TargetId == 100 && d.Activation == Start.AddSeconds(5) && d.DamageType == "Raidwide"),
            "Spread predicted damage omitted.");
        e.Icon(101, 375, Start, 101);
        Check(e.Hints(Vector3.Zero).Contains("GTFO from spreads!"), "Standing in another spread did not warn.");
    }
    private static void Immunity()
    {
        var e = Encounter(1127, Helper(10), Helper(11));
        var player = Player with { Position = new(1, 0, 0) };
        e.Synchronize([Boss, player, Buddy, Helper(10), Helper(11)], player.Position, Start, playerId: 100);
        Cast(e, 40706, 10);
        Cast(e, 40709, 11);
        var projected = Aoes(e, "kb").Where(a => a.Id.EndsWith("/player")).ToArray();
        Check(projected.Select(a => a.Position.X).SequenceEqual(new[] { 16f, 11f })
            && !e.Hints(player.Position).Contains("About to be knocked into danger!"), "Independent knockbacks chained across components.");
        e.StatusChanged(100, 160, true, Start, expiresAt: Start.AddSeconds(8));
        Check(Aoes(e, "kb").Count(a => a.Id.EndsWith("/player")) == 2, "Exact expiry incorrectly grants immunity.");
        e.StatusChanged(100, 160, true, Start, expiresAt: Start.AddSeconds(8).AddTicks(1));
        Check(Aoes(e, "kb").All(a => a.Id.EndsWith("/enter")), "Gain before snapshot did not suppress display-only displacement.");
        e.StatusChanged(100, 160, false, Start);
        Check(Aoes(e, "kb").Count(a => a.Id.EndsWith("/player")) == 2, "Lose before snapshot did not restore displacement.");
    }
}
