using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureHexapod(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object o, string name) => o.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o)!;
        object Encounter()
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object[] Models(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        object[] Aoes(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(key)).ToArray();
        int Obstacles() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
        const string guid = "masterplayer.treasure.vault-oneiron";
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object[] players = [Actor(100, 0, c + Vector3.UnitX * 15), Actor(101, 0, c + Vector3.UnitZ * 15)];
        uint[] actions = [43755, 43756, 43769, 43770, 43759, 43768, 43754, 43752, 43727, 43726, 32301, 32302, 32303, 32304, 32305];
        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48B3, c, combat: true), .. players], full: true, party: party),
            Frame(1279, .1, actions.Select((a, i) => Actor((ulong)i + 2, 0x233C, c,
                cast: Cast(a, 5, c, rotation: .37f, target: 101))), party: party),
            Frame(1279, 50, [], party: party),
            Frame(1279, 51, actions.Select((_, i) => Actor((ulong)i + 2, 0x233C, c)), party: party),
            Frame(1279, 52, [], party: party),
        ]);
        controller.Seek(.2);
        Check(dangers() == 12 && Models("PredictedDamage").Length == 3, "Hexapod all15 rules navigation/damage differs.");
        Check(actions.All(a => (bool)manager.IsGenericDodgeSuppressed(new PromeRotation.LogSystem.LogSystemCastStartEvent
            { SourceId = 2, ActionId = (ushort)a })), "Hexapod active casts not suppressed.");
        controller.Seek(50.1);
        Check(dangers() == 12, "Hexapod source active casts expired by prediction.");
        controller.Seek(51.1);
        Check(dangers() == 0 && Models("PredictedDamage").Length == 0, "Hexapod Finish retained casts/bait.");
        controller.Seek(.2);
        Check(dangers() == 12, "Hexapod reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Hexapod all15 casts exact shapes dual views navigation damage Finish-only artillery retained clock source suppression Seek");

        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48B3, c, combat: true), .. players,
                Actor(30, 0x48B4, c + new Vector3(.13f, 0, 12.17f))], full: true, party: party),
            Frame(1279, 1, [Actor(30, 0x48B4, c + Vector3.UnitX * 10, dead: true)], party: party),
            Frame(1279, 2, [], removed: [30], party: party),
            Frame(1279, 3, [Actor(30, 0x48B4, c + Vector3.UnitX * 10)], party: party),
            TreasureNativeEffect(4, 30, 43771, 1, 100, []),
            TreasureNativeEffect(5, 30, 43771, 2, 100, []), Frame(1279, 6, [], party: party),
        ]);
        controller.Seek(.1);
        var mine = Aoes("treasure/hex/mine/").Single();
        var origin = (Vector3)Value(mine, "Position");
        Check(Obstacles() == 1 && dangers() == 0 && (bool)Value(mine, "Obstacle"), "Mine not published as obstacle-only navigation.");
        controller.Seek(2.1);
        Check(Obstacles() == 1 && (Vector3)Value(Aoes("treasure/hex/mine/").Single(), "Position") == origin,
            "Moving/dead/destroyed mine lost locked AOE.");
        controller.Seek(3.1);
        Check(Obstacles() == 2 && Models("TreasureHexMines").Length == 2, "Same-ID mine new generation failed.");
        controller.Seek(4.1);
        Check(Obstacles() == 1 && (Vector3)Value(Aoes("treasure/hex/mine/").Single(), "Position") != origin,
            "Mine effect failed first-source FIFO.");
        controller.Seek(5.1);
        Check(Obstacles() == 0 && Aoes("treasure/hex/mine/").Length == 0, "Mine second effect retained AOE.");
        controller.Seek(.1);
        Check(Obstacles() == 1, "Mine reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Hexapod mine quantized creation locked pose obstacle-only navigation death Destroy retention source FIFO effects generation Seek owner cleanup");

        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48B3, c, combat: true),
                Actor(100, 0, c + Vector3.UnitX * 15, stackedStatus: 4551),
                Actor(101, 0, c + Vector3.UnitZ * 15, stackedStatus: 4552)], full: true, party: party),
            Frame(1279, .1, [Actor(2, 0x233C, c, cast: Cast(43754, 5, c))], party: party),
            Frame(1279, 1, [Actor(100, 0, c + Vector3.UnitX * 15, stackedStatus: 160)], party: party),
            Frame(1279, 2, [Actor(3, 0x233C, c, cast: Cast(43752, 5, c, startedAt: 2))], party: party),
            Frame(1279, 3, [Actor(2, 0x233C, c)], party: party), Frame(1279, 4, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Models("TreasureHexPoles").Length == 2 && dangers() == 1
            && (float)Value(Value(Aoes("treasure/hex/pole/avoid").Single(), "Shape"), "Radius") == 18,
            "Minus status raw bootstrap or pull18 navigation failed.");
        var poleViewCount = Aoes("treasure/hex/pole").Count(a => (bool)Value(a, "Display"));
        controller.Seek(1.1);
        Check(Aoes("treasure/hex/pole").Count(a => (bool)Value(a, "Display")) == poleViewCount && dangers() == 1,
            "Charge loss/immunity revoked locked ignoreImmunes pole.");
        controller.Seek(2.1);
        Check(dangers() == 1, "Other source pole start revoked unchanged current player's locked slot.");
        controller.Seek(3.1);
        Check(Models("TreasureHexPoles").Length == 0 && dangers() == 0, "Any Minus Finish failed global pole clear.");
        stop();
        Console.WriteLine("PASS native treasure production Hexapod PoleShift signs slot freeze pull18 navigation ignore immunity global any-source cancel dual view projection replay isolation");

        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48B3, c, combat: true),
                Actor(100, 0, c, stackedStatus: 2163, statusExpiresAt: Start.AddSeconds(10)),
                Actor(101, 0, c + Vector3.UnitZ * 15), Actor(30, 0x48B4, c + Vector3.UnitX * 12)], full: true, party: party),
            Frame(1279, 9.5, [], party: party),
            Frame(1279, 11, [Actor(100, 0, c, stackedStatus: 2163, statusExpiresAt: Start.AddSeconds(10))], party: party),
            Frame(1279, 12, [Actor(100, 0, c)], party: party), Frame(1279, 13, [], party: party),
        ]);
        controller.Seek(.1);
        Check(Models("TreasureHexMarchMovements").Length == 1 && Obstacles() == 1 && dangers() == 0
            && Aoes("treasure/hex/march/").Where(a => (bool)Value(a, "Display")).All(a => !(bool)Value(a, "Navigate")),
            "March segment19.8/wall projection boundary failed.");
        controller.Seek(9.6);
        Check(Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [c, MathF.PI]) != null,
            "First pending direction did not reach PR-facing planning.");
        controller.Seek(11.1);
        Check(Models("TreasureHexMarchMovements").Length == 1, "Expired raw slot synthesized march Lose.");
        controller.Seek(12.1);
        Check(Models("TreasureHexMarchMovements").Length == 0, "Real raw Lose retained march.");
        stop();
        Console.WriteLine("PASS native treasure production Hexapod four-direction status march19.8 wall projection first pending facing expired occupied slot and real Lose");

        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48B3, c, combat: true),
                Actor(100, 0, c, stackedStatus: 3802, statusExpiresAt: Start.AddSeconds(10)), players[1]], full: true, party: party),
            Frame(1279, 7.5, [], party: party), Frame(1279, 20, [], party: party),
            Frame(1279, 21, [Actor(100, 0, c)], party: party), Frame(1279, 22, [], party: party),
        ]);
        controller.Seek(.1);
        Check(Models("TreasureHexBombs").Length == 1 && Models("TreasureHexMarchMovements").Length == 0
            && !(bool)Value(Encounter(), "RequireAllStop"), "Bomb wrongly became immediate execution hold or forward march.");
        controller.Seek(20.1);
        Check(Models("TreasureHexBombs").Length == 1 && !(bool)Value(Encounter(), "RequireAllStop"),
            "Bomb expiry synthesized Lose or expanded old diagnostic control scope.");
        controller.Seek(21.1);
        Check(Models("TreasureHexBombs").Length == 0, "Bomb real Lose retained Stay.");
        stop();
        Console.WriteLine("PASS native treasure production Hexapod bomb independent Stay ExpireAt persistent diagnostic actual Lose no unsupported PR all-stop expansion");

        foreach (var statusFirst in new[] { false, true })
        {
            var charged = Actor(100, 0, c + Vector3.UnitX * 15, stackedStatus: 4551);
            var casting = Actor(2, 0x233C, c, cast: Cast(43754, 5, c));
            load(guid,
            [
                Header(1279), Frame(1279, 0, [Actor(1, 0x48B3, c, combat: true),
                    .. statusFirst ? new[] { charged, casting } : new[] { casting, charged }, players[1]], full: true, party: party),
                Frame(1279, 1, [], party: party),
            ]);
            controller.Seek(.1);
            Check(Models("TreasureHexPoles").Length == (statusFirst ? 1 : 0),
                "Bootstrap failed source actor cast/status interleaving.");
            stop();
        }
        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48B3, c, combat: true),
                Actor(100, 0, c + Vector3.UnitX * 15, stackedStatus: 4551), players[1]], full: true, party: party),
            StatusEvent(.1, 100, 4551, 3, false),
            Frame(1279, .2, [Actor(2, 0x233C, c, cast: Cast(43754, 5, c, startedAt: .2))], party: party),
            StatusEvent(1, 100, 4552, 0, true),
            Frame(1279, 1.1, [Actor(3, 0x233C, c, cast: Cast(43752, 5, c, startedAt: 1.1))], party: party),
            Frame(1279, 2, [], party: party),
        ]);
        controller.Seek(.3);
        Check(Models("TreasureHexPoles").Length == 0 && dangers() == 0, "Adapter discarded real charge Lose before an old occupied raw.");
        controller.Seek(1.2);
        Check(Models("TreasureHexPoles").Length == 1 && dangers() == 1, "Adapter discarded event-only charge Gain.");
        stop();
        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48B3, c, combat: true),
                Actor(100, 0, c, stackedStatus: 3802, statusExpiresAt: Start.AddSeconds(10)), players[1]], full: true, party: party),
            Frame(1279, 1, [Actor(100, 0, c, dead: true, stackedStatus: 3802, statusExpiresAt: Start.AddSeconds(10))], party: party),
            Frame(1279, 2, [Actor(100, 0, c, stackedStatus: 3802, statusExpiresAt: Start.AddSeconds(10))], party: party),
            Frame(1279, 3, [], party: party),
        ]);
        controller.Seek(1.2);
        Check(Models("TreasureHexBombs").Length == 0, "Dead current player did not clear Bomb with no Hints enumeration.");
        controller.Seek(2.2);
        Check(Models("TreasureHexBombs").Length == 0, "Revival resurrected unchanged old Bomb.");
        stop();
        Console.WriteLine("PASS native treasure production Hexapod bootstrap world-order cast-status actual adapter charge Gain Lose old raw barrier dead Bomb revival cleanup");
    }
}
