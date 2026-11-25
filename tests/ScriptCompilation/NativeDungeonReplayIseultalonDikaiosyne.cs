using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunIseultalonDikaiosyne(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        string guid = "";
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        object[] Items(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        object Aoe(string id) => Aoes().Single(a => (string)Value(a, "Id") == id);
        string Director(double t, uint command) => JsonSerializer.Serialize(new
        {
            t, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
            data = new { Command = command, Timestamp = Start.AddSeconds(t) },
        });
        string Icon(double t) => JsonSerializer.Serialize(new
        {
            t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = 100ul, TargetId = 2ul, MarkerId = 129u, Timestamp = Start.AddSeconds(t) },
        });
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Native quest pair replay leaked execution.");
        }
        int CircleOracle(Vector3 center, float radius)
        {
            dynamic arena = Value(Encounter(), "Arena");
            var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MasterPlayer.MechanicRuntime");
            var boundsType = assembly.GetType("BossMod.ArenaBoundsCircle")!;
            var bounds = Activator.CreateInstance(boundsType, [radius, .5f, false])!;
            var direction = assembly.GetType("BossMod.WDir")!;
            var compared = 0;
            Check((Vector3)arena.Position == center, "Native quest circle center differs from source.");
            for (var x = -radius - 2.13f; x < radius + 2; x += .7f)
            for (var z = -radius - 2.17f; z < radius + 2; z += .7f)
            {
                var position = center + new Vector3(x, 0, z);
                var expected = (bool)boundsType.GetMethod("Contains")!.Invoke(bounds,
                    [Activator.CreateInstance(direction, [position.X - center.X, position.Z - center.Z])])!;
                Check((bool)arena.Contains(position, 0f) == expected, "Native quest circle oracle mismatch.");
                ++compared;
            }
            return compared;
        }

        guid = "masterplayer.quest.iseultalon";
        var center = new Vector3(-289, 0, -30);
        object Npc(Vector3 position) => Actor(2, 0x2650, position, subKind: 5, isEnemy: false);
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        string I(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(839, t, actors, full: full, removed: removed, party: party);
        load(guid, [Header(839), I(0,
            [Actor(1, 0x252C, center, combat: true, nameId: 7895),
             Actor(10, 0x25B3, center, cast: Cast(14609, 20, center, target: 10, startedAt: 0)),
             Actor(11, 0x273D, center, cast: Cast(14603, 30, center, startedAt: 0)),
             Actor(12, 0x233C, center), Actor(13, 0x233C, center),
             Npc(center + Vector3.UnitX * 14), Actor(100, 0, center + Vector3.UnitX * 10)], true),
            Icon(.1), Icon(.2),
            I(1, [Actor(10, 0x25B3, center + Vector3.UnitZ * 5,
                cast: Cast(14609, 20, center, target: 10, startedAt: 0))]),
            I(2, [], removed: [10]), EffectEvent(3, 12, 15083, 1),
            I(4, [Actor(1, 0x252C, center, combat: true, cast: Cast(14602, 4, center, startedAt: 4))]),
            I(5, [Actor(1, 0x252C, center, combat: true)]),
            I(6, [Actor(1, 0x252C, center + new Vector3(1.12345f, 0, .23456f), combat: true, rotation: MathF.PI / 2)]),
            EffectEvent(7, 12, 14604, 2),
            I(8, [Actor(11, 0x273D, center)]),
            I(9, [Actor(12, 0x233C, center, cast: Cast(14981, 20, center, startedAt: 9)), Npc(center)]),
            I(10, [Actor(13, 0x233C, center, cast: Cast(14981, 20, center + Vector3.UnitZ * 10, startedAt: 10))]),
            I(11, [Actor(12, 0x233C, center)]),
            I(12, [Actor(1, 0x252C, center, dead: true)]),
            I(13, [Actor(1, 0x252C, center, combat: true)]),
            I(14, [], removed: [1]), I(15, [Actor(1, 0x252C, center, combat: true)]),
            Director(16, 0x40000005), I(16.1, []), Director(17, 0x40000011), I(17.1, []), I(20, [])]);
        controller.Seek(.5);
        var oracle = CircleOracle(center, 25);
        Check(Shown("iseultalon/spread/").Length == 2 && Items("ElectrocutionTowers").Length == 1 && dangers() > 0
            && Damage().Length == 1 && (ulong)Value(Damage()[0], "TargetId") == 100
            && (DateTime)Value(Damage()[0], "Activation") == default, "Iseultalon callback actor/default spread clock failed.");
        controller.Seek(2.5);
        Check(Items("ElectrocutionTowers").Length == 1 && (int)Value(Encounter(), "ElectrocutionNumCasts") == 1,
            "Iseultalon moved destroyed caster incorrectly cleared old tutorial tower.");
        controller.Seek(3.5);
        Check(Shown("iseultalon/spread/").Length == 0, "Arbitrary Thunderbolt effect failed all-spread clear.");
        controller.Seek(6.5);
        Check((Vector3)Value(Aoe("iseultalon/death-ray"), "Position") == center + new Vector3(1.12345f, 0, .23456f)
            && (DateTime)Value(Aoe("iseultalon/death-ray"), "Activation") == Start.AddSeconds(10.1d)
            && Shown("iseultalon/narrow-ray/").Length == 1, "Dynamic wide ray raw origin/clock/Finish persistence mismatch.");
        controller.Seek(7.5);
        Check(Shown("iseultalon/death-ray").Length == 0, "Unrelated caster wide-ray effect failed clear.");
        controller.Seek(10.5);
        Check(Items("Electrocution2Towers").Length == 2 && (DateTime)Value(Aoe("iseultalon/tower2/join"), "Activation") == default
            && Damage().Length == 1, "First-only cast tower gained deadline or unexpected damage prediction.");
        controller.Seek(11.5);
        Check(Items("Electrocution2Towers").Length == 1, "Cast tower first source Finish failed promotion.");
        controller.Seek(13.5);
        Check(Value(Encounter(), "Active") == null && previews().Length == 0, "Completed Iseultalon healing repulled.");
        controller.Seek(15.5);
        Check(Value(Encounter(), "Active") != null, "New Iseultalon raw generation failed pull.");
        controller.Seek(16.5);
        Check(Value(Encounter(), "Active") == null, "Iseultalon wipe repulled stale actors.");
        controller.Seek(17.5);
        Check(Value(Encounter(), "Active") != null, "Iseultalon Director unlock failed.");
        controller.Seek(.5);
        Check(Shown("iseultalon/spread/").Length == 2, "Iseultalon reverse Seek failed model rebuild.");
        Isolated();
        manager.SetMechanismEnabled(guid, "Iseultalon", false);
        controller.Seek(.6);
        Check(previews().Length == 0 && dangers() == 0, "Iseultalon mechanism disable retained output.");
        manager.SetMechanismEnabled(guid, "Iseultalon", true);
        stop();
        Check(previews().Length == 0 && dangers() == 0, "Iseultalon owner stop leaked output.");
        Console.WriteLine($"PASS native Iseultalon production default tower clocks raw destruction historical positions duplicate icon actor independent ray first tower NPC capacity source circle oracle {oracle} points reverse Seek wipe and owner cleanup");

        load(guid, [Header(839), I(0, [Actor(1, 0x252C, center, combat: true),
            Actor(10, 0x25B3, center, cast: Cast(14609, 20, center, target: 10, startedAt: 0)),
            Actor(11, 0x25B3, center), Actor(100, 0, center + Vector3.UnitX * 10)], true),
            I(1, [Actor(11, 0x25B3, center, cast: Cast(14609, 20, center, target: 11, startedAt: 1))], removed: [10]),
            I(2, [])]);
        controller.Seek(1.5);
        Check(Items("ElectrocutionTowers").Length == 1
            && (int)Value(Items("ElectrocutionTowers")[0], "ForbiddenSlot") == 0
            && (ulong)Value(Value(Items("ElectrocutionTowers")[0], "Cast"), "Source") == 11,
            "Iseultalon raw same-frame new tower preceded destroyed old caster Finish.");
        controller.Seek(.5);
        Check(Items("ElectrocutionTowers").Length == 1
            && (int)Value(Items("ElectrocutionTowers")[0], "ForbiddenSlot") == -1,
            "Iseultalon reverse Seek retained later tutorial counter.");
        stop();
        Console.WriteLine("PASS native Iseultalon production raw destruction Finish precedes same-frame coincident tower Start captured forbidden slot reverse Seek and owner cleanup");

        guid = "masterplayer.quest.to-have-loved-and-lost";
        center = new(-798.6f, 0, -40.49f);
        string D(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(869, t, actors, full: full, removed: removed, party: party);
        string Correction() => JsonSerializer.Serialize(new
        {
            t = 2.0, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0f,
            data = new { SourceId = 10ul, TargetId = 0ul, ActionId = 15576u, CastTime = 20f,
                EffectPosition = Pose(center + Vector3.UnitZ * 5), Timestamp = Start },
        });
        load(guid, [Header(869), D(0,
            [Actor(1, 0x2927, center, combat: true, nameId: 8922),
             Actor(10, 0x2612, center, cast: Cast(15576, 20, center, startedAt: 0)),
             Actor(11, 0x2612, center, cast: Cast(13207, 20, center, startedAt: 0)),
             Actor(12, 0x233C, center, cast: Cast(16132, 10, center, startedAt: 0)),
             Actor(13, 0x233C, center), Actor(14, 0x233C, center),
             Npc(center), Actor(100, 0, center + Vector3.UnitX * 15)], true),
            D(1, [Actor(11, 0x2612, center)]), Correction(),
            D(3, [Actor(12, 0x233C, center + Vector3.UnitZ * 15, cast: Cast(16132, 10, center, startedAt: 0))]),
            D(4, [Actor(12, 0x233C, center)]),
            D(5, [Actor(13, 0x233C, center, cast: Cast(16134, 20, center, target: 2, startedAt: 5)),
                Actor(14, 0x233C, center, cast: Cast(17431, 20, center, target: 100, startedAt: 5))]),
            D(6, [Actor(2, 0x2650, center, dead: true, subKind: 5)]), D(7, [Npc(center)]),
            D(8, [], removed: [2]), D(9, [Npc(center + Vector3.UnitZ * 12)]),
            D(10, [Actor(1, 0x2927, center, dead: true)]), D(11, []), D(12, [])]);
        controller.Seek(.5);
        oracle = CircleOracle(center, 20);
        Check(Items("FractureTowers").Length == 2 && Shown("dikaiosyne/knockback/").Length == 2
            && Aoes().Where(a => ((string)Value(a, "Id")).StartsWith("dikaiosyne/knockback/"))
                .All(a => !(bool)Value(a, "Navigate")), "Dikaiosyne towers or source display-only knockback failed.");
        var player = ((IDictionary)Value(Encounter(), "Actors"))[100ul]!;
        var landing = (Vector3)Encounter().GetType().GetMethod("DikaiosyneLanding", flags)!.Invoke(Encounter(), [player])!;
        Check(Vector3.Distance(landing, center + Vector3.UnitX * 25) < .001f, "Dikaiosyne incorrectly stopped at arena wall.");
        controller.Seek(1.5);
        Check(Items("FractureTowers").Length == 1 && (ulong)Value(Value(Items("FractureTowers")[0], "Cast"), "Source") == 11,
            "Six-action tower Finish failed shared first-location removal.");
        controller.Seek(2.5);
        Check(Items("FractureTowers").Length == 1, "Consumed fracture resurrected after same generation packet correction.");
        controller.Seek(3.5);
        player = ((IDictionary)Value(Encounter(), "Actors"))[100ul]!;
        landing = (Vector3)Encounter().GetType().GetMethod("DikaiosyneLanding", flags)!.Invoke(Encounter(), [player])!;
        Check(Vector3.Distance(landing, center + Vector3.UnitX * 25) < .001f, "Knockback followed moved caster instead of locked LocXZ.");
        controller.Seek(4.5);
        Check(Shown("dikaiosyne/knockback/").Length == 0, "Observed knockback Finish failed cleanup.");
        controller.Seek(5.5);
        Check(Shown("quest/stack/").Length == 1 && Shown("quest/spread/").Length == 1
            && Damage().Length == 1, "NPC stack/spread display or tower-only prediction failed.");
        controller.Seek(6.5);
        Check(Shown("quest/stack/").Length == 0, "Dead original stack target remained active.");
        controller.Seek(9.5);
        Check(Vector3.Distance((Vector3)Value(Aoe("quest/stack/0/display"), "Position"), center) < .05f,
            "Same ID new NPC stole destroyed stack target.");
        controller.Seek(10.5);
        Check(previews().Length == 0 && dangers() == 0, "Dikaiosyne completion retained outputs.");
        controller.Seek(.5);
        Check(Items("FractureTowers").Length == 2, "Dikaiosyne reverse Seek failed shared tower rebuild.");
        Isolated();
        stop();
        Check(previews().Length == 0 && dangers() == 0, "Dikaiosyne owner stop leaked outputs.");
        Console.WriteLine($"PASS native Dikaiosyne production six shared exact towers no packet resurrection locked ten knockback display-only no wall stop NPC stack spread frozen targets source circle oracle {oracle} points reverse Seek and owner cleanup");
    }
}
