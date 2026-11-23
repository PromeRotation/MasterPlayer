using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunOrigenics(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.origenics"; const uint territory = 1208;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name)!.GetValue(a)!;
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("origenics/" + key)).ToArray();
        string Map(double t, uint index, uint state) => JsonSerializer.Serialize(new { t, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = index,
                Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(-127.5f, 0, -80);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x41E2, c, combat: true), Actor(2, 0x42BA, c),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
            CastEvent(.1, 2, 38328, 4, c),
            Frame(territory, .2, [Actor(2, 0x42BA, c, cast: Cast(38328, 4, c))], party: party),
            Frame(territory, 1, [Actor(1, 0x41E2, c, dead: true)], party: party),
            Frame(territory, 4.5, [Actor(2, 0x42BA, c)], party: party),
        ]);
        controller.Seek(1.5); Check(previews().Length == 1 && dangers() == 1, "Origenics trash group survived primary death.");
        controller.Seek(4.6); Check(previews().Length == 0, "Origenics trash finish.");
        controller.Seek(1.5); Check(previews().Length == 1, "Origenics trash Seek."); stop();

        c = new(-88, 0, -180);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4185, c, combat: true), Actor(10, 0x233C, c),
                Actor(11, 0x233C, c), Actor(12, 0x233C, c), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
            CastEvent(.1, 10, 36465, 2, c), CastEvent(.1, 11, 36466, 3, c, MathF.PI),
            Frame(territory, .2, [Actor(10, 0x233C, c, cast: Cast(36465, 2, c)),
                Actor(11, 0x233C, c, cast: Cast(36466, 3, c, MathF.PI))], party: party),
            CastEvent(.3, 12, 36460, 1, c + Vector3.UnitX * 3),
            Frame(territory, .4, [Actor(12, 0x233C, c, cast: Cast(36460, 1, c + Vector3.UnitX * 3, startedAt: .3))], party: party),
            EffectEvent(1.7, 12, 36460, 1, c + Vector3.UnitX * 3),
            Frame(territory, 1.8, [Actor(12, 0x233C, c)], party: party),
            Frame(territory, 2, [Actor(20, 0x1E9E3C, c + Vector3.UnitX * 3, dead: true)], party: party),
            Frame(territory, 2.5, [Actor(10, 0x233C, c)], party: party),
            Frame(territory, 3.5, [Actor(11, 0x233C, c)], party: party),
            EffectEvent(4, 1, 36469, 2), EffectEvent(5, 1, 36470, 3),
            Frame(territory, 6, [Actor(20, 0x1E9E3C, c + Vector3.UnitX * 3, eventState: 7)], party: party),
        ]);
        controller.Seek(1); Check(Group("cast").Length == 2 && Group("simple").Length == 1, "Riot/poison inputs.");
        controller.Seek(1.9); Check(Group("poison").Length == 1 && Group("simple").Length == 0, "Poison event handoff.");
        controller.Seek(3.6); Check(Group("cast").Length == 2 && Group("poison").Length == 0 && Group("zone").Length == 1, "Riot survives telegraph, zone replaces prediction.");
        controller.Seek(4.1); Check(Group("cast").Length == 1, "Riot single hit.");
        controller.Seek(6.1); Check(previews().Length == 0, "Riot/zone cleanup.");
        controller.Seek(1); Check(Group("cast").Length == 2, "Riot Seek."); stop();

        c = new(-172, 0, -142);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4170, c, combat: true), Actor(10, 0x233C, c),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
            Map(.1, 0x1B, 0x00020001), CastEvent(.2, 1, 36367, 5, c),
            Frame(territory, .3, [Actor(1, 0x4170, c, combat: true, cast: Cast(36367, 5, c, startedAt: .2))], party: party),
            CastEvent(.4, 10, 38807, 5, c),
            Map(1, 0x99, 0x00080004),
            Frame(territory, 5.6, [Actor(1, 0x4170, c, combat: true)], party: party),
        ]);
        controller.Seek(.8); Check(Group("wall").Length == 4 && Group("kb/nav").Length == 1 && Group("simple").Length == 0, "Surge full map/fake laser.");
        controller.Seek(1.1); Check(Group("wall").Length == 4 && Group("kb/nav").Length == 0, "Surge locked display/current nav.");
        controller.Seek(5.7); Check(previews().Length == 0, "Surge actual finish.");
        controller.Seek(.8); Check(Group("wall").Length == 4, "Surge Seek."); stop();

        c = new(190, 0, 0);
        object Pending(uint sequence, bool confirmed) => new { Id = 100UL, Kind = 1, Position = Pose(c), Hp = 1000, MaxHp = 1000,
            IncomingEffects = new[] { new { Slot = 0, GlobalSequence = sequence, TargetIndex = 0, TargetConfirmed = confirmed, Knockback = true } } };
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x417D, c, combat: true), Actor(10, 0x233C, c),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)], full: true, party: party),
            Map(.1, 0x28, 0x00020001), CastEvent(.2, 1, 39233, 20, c, MathF.PI),
            Frame(territory, .3, [Actor(1, 0x417D, c, combat: true, cast: Cast(39233, 20, c, MathF.PI, startedAt: .2))], party: party),
            CastEvent(.4, 10, 36433, 5, c - Vector3.UnitZ * 19),
            Frame(territory, .5, [Actor(10, 0x233C, c, cast: Cast(36433, 5, c - Vector3.UnitZ * 19, startedAt: .4))], party: party),
            Frame(territory, 5.8, [Actor(10, 0x233C, c), Pending(1, false)], party: party),
            Frame(territory, 6.2, [Pending(1, true)], party: party),
            Frame(territory, 6.5, [Actor(100, 0, c)], party: party),
            Frame(territory, 7, [Pending(2, false)], party: party),
            Frame(territory, 11, [Pending(2, false)], party: party),
        ]);
        controller.Seek(1); Check(Group("charge").Any(a => (bool)Value(a, "Safe")) && Group("charge").Any(a => !(bool)Value(a, "Display")), "Ambrose safe/reverse modes.");
        controller.Seek(6); Check(Group("charge").Length == 0 && Group("kb").Length == 0, "Pending effect failed to hide charge after finish.");
        controller.Seek(6.3); Check(Group("charge").Length == 0, "Target confirmation cleared pending knockback.");
        controller.Seek(6.6); Check(Group("charge").Length == 1, "Incoming slot removal did not restore cone.");
        controller.Seek(7.1); Check(Group("charge").Length == 0, "New incoming sequence.");
        controller.Seek(11.1); Check(Group("charge").Length == 1, "Unchanged pending sequence renewed.");
        controller.Seek(6); Check(Group("charge").Length == 0, "Pending Seek state."); stop();
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x417D, c, combat: true), Actor(10, 0x233C, c),
                Actor(11, 0x233C, c + Vector3.UnitX * 10), Actor(100, 0, c)], full: true, party: party),
            CastEvent(.1, 10, 38953, 2.5, c + Vector3.UnitZ * 10),
            CastEvent(.1, 11, 38953, 2.5, c - Vector3.UnitZ * 10),
            Frame(territory, .2, [Actor(10, 0x233C, c, cast: Cast(38953, 2.5, c + Vector3.UnitZ * 10)),
                Actor(11, 0x233C, c + Vector3.UnitX * 10, cast: Cast(38953, 2.5, c - Vector3.UnitZ * 10))], party: party),
            Frame(territory, 3, [Actor(10, 0x233C, c), Actor(11, 0x233C, c + Vector3.UnitX * 10)], party: party),
            EffectEvent(9.7, 20, 38954, 1), EffectEvent(10.5, 20, 36431, 2),
        ]);
        controller.Seek(3.1); Check(Group("rush").Length == 2 && dangers() == 2, "Rush telegraph lifetime/navigation.");
        controller.Seek(9.8); Check(Group("rush").Length == 1, "Rush FIFO hit.");
        controller.Seek(10.6); Check(previews().Length == 0 && dangers() == 0, "Rush final hit.");
        controller.Seek(3.1); Check(Group("rush").Length == 2, "Rush Seek."); stop();
        Console.WriteLine("PASS: native Origenics synthetic replay trash/riot/poison/maps/surge/charge/pending/rush/Seek");
    }
}
