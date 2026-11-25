using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunJobQuests(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        object[] MechanicPreviews() => previews().Where(p => !((string)Value(p, "Id")).Contains("job-quest/enemy/")).ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        foreach (var (territory, oid, id, center, actions) in new (uint, uint, string, Vector3, uint[])[]
        {
            (708, 0x1BED, "blood-on-the-deck", new(0, 0, 15.8f), [8407, 8405, 8408, 8411]),
            (1233, 0x4292, "mind-over-manor", Vector3.Zero, [37516, 37512, 37513, 37515]),
            (1234, 0x4293, "somewhere-only-she-knows", new(54.745f, 0, -219.305f), [37517, 37518]),
            (1234, 0x4296, "somewhere-only-she-knows", new(.17f, 0, -250.525f), [37519, 37522, 37520, 37521]),
            (1234, 0x4298, "somewhere-only-she-knows", new(0, 0, -340), [37540, 37529, 37538]),
            (1235, 0x429F, "fangs-of-the-viper", new(264, 0, 480), [37703, 39263, 37704, 37700, 37701, 37702]),
            (709, 0x1BEE, "the-face-of-true-evil", new(-217.27f, 0, -158.31f), [8415, 8412, 8417, 8421]),
            (1022, 0x3587, "sages-focus", new(0, 0, -82.146f), [26537, 26543, 26542, 26546, 26539, 26550, 26554]),
            (715, 0x1C5E, "the-orphans-and-the-broken-blade", new(-166.8f, 0, 290), [8459, 8453]),
            (711, 0x1BF8, "the-battle-on-bekko", new(808.8f, 0, 69.5f), [8433, 9145, 8437, 8435, 8446, 8448]),
            (1021, 0x3664, "the-killing-art", new(-69.569f, 0, -388), [27591, 27595, 27597, 27599, 27605]),
            (722, 0x1CAF, "our-unsung-heroes", new(232.3f, 0, 407.7f), [5604, 8635, 4366]),
            (1023, 0x35C5, "life-ephemeral-path-eternal", new(224.8f, 0, -855.8f), [26837, 26836, 26849, 26850]),
            (1023, 0x35C6, "life-ephemeral-path-eternal", new(224.8f, 0, -855.8f), [26854, 26855, 26856, 26862, 26863, 26864, 26865, 26857, 26858]),
            (648, 0x1BCB, "the-power-to-protect", new(76.285f, 0, -659.475f), [1003, 720, 1135, 8348]),
            (702, 0x1CBA, "an-art-for-the-living", new(-111, 0, -295.51f), [8683, 8707, 8708, 8709, 8697, 8699]),
            (866, 0x2A45, "steel-against-steel", new(-266, 0, -507), [17553, 17575, 17563, 17564, 17565, 17574, 17569, 17559]),
            (706, 0x1B51, "raising-the-sword", new(-89.3f, 0, 0), [8134, 8137, 8141]),
            (867, 0x29D2, "gamboling-for-gil", new(520.47f, 0, 124.99f), [17197, 17204, 17214, 15646]),
            (687, 0x1C48, "the-measure-of-his-reach", new(504, 0, -133), [8397, 8398, 8450, 8399]),
            (932, 0x2F1E, "faded-memories", new(-143, 0, 357), [21094, 21096]),
            (932, 0x2F21, "faded-memories", new(-242, 0, 436.5f), [21299, 21098]),
            (977, 0x3202, "death-unto-dawn", Vector3.Zero, [24057, 24054, 24046]),
            (861, 0x299D, "the-oracle-of-light", new(126.75f, 0, -311.25f), [17622, 17621, 17823, 18035]),
            (863, 0x295A, "a-feast-of-lies", new(0, 0, 18), [16274, 16275, 16283, 16284, 16298, 16297, 16277, 16285, 16276, 16296, 16290, 16286]),
            (671, 0x1A88, "rhalgrs-beacon", new(-195.25f, 0, 147.5f), [8340]),
            (1010, 0x3646, "a-frosty-reception", new(-.07f, 0, -79), [27443, 27444, 27459]),
        })
        {
            var records = new List<string>
            {
                Header(territory),
                Frame(territory, 0, [Actor(1, oid, center, combat: true), Actor(100, 0, center, combat: true, objectIndex: 0)], full: true, party: party),
            };
            foreach (var (action, index) in actions.Select((a, i) => (a, i)))
            {
                var t = 1 + index * 7;
                records.Add(Frame(territory, t, [Actor(2, 0x233C, center,
                    cast: Cast(action, 2, center, MathF.PI / 2, startedAt: t))], party: party));
                records.Add(CastEvent(t, 2, action, 2, center, MathF.PI / 2));
                records.Add(EffectEvent(t + 1, 2, action, (uint)(index + 1)));
                records.Add(Frame(territory, t + 5, [Actor(2, 0x233C, center)], party: party));
            }
            var deathTime = Math.Max(40, actions.Length * 7 + 3);
            records.Add(Frame(territory, deathTime, [Actor(1, oid, center, dead: true)], party: party));
            records.Add(Frame(territory, deathTime + 1, [], party: party));
            load("masterplayer.quest." + id, records);
            foreach (var index in Enumerable.Range(0, actions.Length))
            {
                controller.Seek(1.5 + index * 7);
                Check(dangers() == 1 && MechanicPreviews().Length == 1, $"{oid:X} action {actions[index]} navigation/drawing.");
                controller.Seek(5.5 + index * 7);
                Check(dangers() == 1, $"{oid:X} action {actions[index]} expired before observed ending.");
                controller.Seek(6.5 + index * 7);
                Check(dangers() == 0 && MechanicPreviews().Length == 0, $"{oid:X} action {actions[index]} observed ending leak.");
            }
            controller.Seek(deathTime + .5);
            Check(dangers() == 0 && previews().Length == 0, $"{oid:X} death cleanup.");
            controller.Seek(1.5);
            Check(dangers() == 1, $"{oid:X} reverse Seek reconstruction.");
            stop();
            Console.WriteLine($"PASS production job quest {oid:X}: all AOE shapes, independent observed endings beyond prediction, effect retention, navigation and dual-view previews, death, reverse Seek and owner cleanup");
        }

        load("masterplayer.quest.mind-over-manor",
        [
            Header(1233), Frame(1233, 0, [Actor(1, 0x4292, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true, party: party),
            Frame(1233, 1, [Actor(1, 0x4292, Vector3.Zero, combat: true, cast: Cast(37511, 3, Vector3.Zero, startedAt: 1))], party: party),
            CastEvent(1, 1, 37511, 3, Vector3.Zero),
            Frame(1233, 5, [Actor(1, 0x4292, Vector3.Zero, combat: true)], party: party),
            CastEvent(6, 1, 37514, 3, Vector3.Zero), Frame(1233, 7, [], party: party),
        ]);
        controller.Seek(2d);
        Check(dangers() == 0 && Damage().Length == 1 && (string)Value(Damage()[0], "DamageType") == "Raidwide",
            "Mind Over Manor raidwide prediction or non-spatial handling.");
        controller.Seek(6.5);
        Check(dangers() == 0 && Damage().Length == 0
            && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 37514 }),
            "Unimplemented source enum 37514 was claimed.");
        stop();

        foreach (var (oid, addOid, action, center) in new (uint, uint, uint, Vector3)[]
        {
            (0x4293, 0x4294, 37517, new(54.745f, 0, -219.305f)), (0x4296, 0x4297, 37522, new(.17f, 0, -250.525f)),
        })
        {
            load("masterplayer.quest.somewhere-only-she-knows",
            [
                Header(1234), Frame(1234, 0, [Actor(1, oid, center, combat: true), Actor(2, addOid, center), Actor(100, 0, center)], full: true, party: party),
                Frame(1234, 1, [Actor(1, oid, center, dead: true)], party: party),
                JsonSerializer.Serialize(new { t = 1.1, kind = "BattleEnded", type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent",
                    data = new { Timestamp = Start.AddSeconds(1.1) } }),
                Frame(1234, 2, [], removed: [1], party: party),
                Frame(1234, 3, [Actor(2, addOid, center, cast: Cast(action, 3, center, startedAt: 3))], party: party),
                CastEvent(3, 2, action, 3, center),
                Frame(1234, 7, [Actor(2, addOid, center)], party: party),
                Frame(1234, 8, [Actor(2, addOid, center, dead: true)], party: party),
                Frame(1234, 9, [], party: party),
            ]);
            controller.Seek(4d);
            Check((bool)Script().GetType().GetProperty("EncounterActive")!.GetValue(Script())! && dangers() == 1,
                $"{oid:X} surviving add after primary death/destruction/battle end.");
            controller.Seek(8.5);
            Check(!(bool)Script().GetType().GetProperty("EncounterActive")!.GetValue(Script())! && previews().Length == 0,
                $"{oid:X} final group death cleanup.");
            stop();
        }
        var griffin = new Vector3(.17f, 0, -250.525f);
        var eye = griffin + Vector3.UnitZ * 120;
        load("masterplayer.quest.somewhere-only-she-knows",
        [
            Header(1234), Frame(1234, 0, [Actor(1, 0x4296, griffin, combat: true), Actor(100, 0, griffin)], full: true, party: party),
            Frame(1234, 1, [Actor(1, 0x4296, griffin, combat: true, cast: Cast(37523, 3, eye, startedAt: 1))], party: party),
            CastEvent(1, 1, 37523, 3, eye),
            Frame(1234, 4, [Actor(100, 0, griffin, rotation: MathF.PI)], party: party),
            Frame(1234, 4.1, [Actor(1, 0x4296, griffin, combat: true)], party: party),
            Frame(1234, 5, [Actor(3, 0x4298, new(0, 0, -340), combat: true)], party: party),
            Frame(1234, 6, [], party: party),
        ]);
        controller.Seek(2d);
        Check(dangers() == 0 && MechanicPreviews().Length == 3, "Griffin gaze eye/pupil/arc previews.");
        controller.Seek(3.6);
        Check(Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [griffin, 0f]) is float,
            "Griffin long range gaze did not plan PR facing.");
        var hints = Value(Script(), "Hints");
        var facingControl = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facingControl.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facingControl)!,
            "Offline Griffin executed PR facing.");
        controller.Seek(4.2);
        Check(MechanicPreviews().Length == 0 && Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [griffin, 0f]) == null,
            "Griffin observed gaze ending retained control/drawing.");
        controller.Seek(1.5);
        Check(MechanicPreviews().Length == 3, "Griffin gaze Seek reconstruction.");
        stop();
        Console.WriteLine("PASS production job quest boundaries: raidwide-only damage, unknown action fallback, both grouped lifecycles, Griffin 10000-range gaze, facing plan, replay isolation, observed ending and cleanup");
    }
}
