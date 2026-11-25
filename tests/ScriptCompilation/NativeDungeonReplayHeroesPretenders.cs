using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHeroesPretenders(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.quest.heroes-and-pretenders";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(676, 0, 41);
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(1218, t, actors, full: full, removed: removed, party: party);
        object Npc(Vector3 position, bool dead = false) => Actor(2, 0x42A0, position, dead: dead, subKind: 5, isEnemy: false);
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Heroes/Pretenders replay leaked native execution.");
        }

        var lines = new List<string>
        {
            Header(1218), At(0, [Actor(1, 0x428A, center, combat: true, objectIndex: 2, isEnemy: true),
                Actor(10, 0x233C, center, rotation: .9f,
                    cast: Cast(37467, 7, center + Vector3.UnitZ * 12, rotation: MathF.PI / 2, startedAt: 0)),
                Actor(11, 0x233C, center, cast: Cast(37466, 30, center, startedAt: 0)),
                Actor(12, 0x233C, center), Actor(20, 0x428C, center), Actor(21, 0x4208, center),
                Actor(22, 0x428E, center), Actor(23, 0x448B, center), Actor(24, 0x4207, center),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 12)], true),
            At(1, [Actor(10, 0x233C, center, dead: true)]), At(2, [], removed: [10]),
        };
        var position = center + new Vector3(.8f, 0, .8f);
        for (var i = 0; i < 8; ++i)
        {
            var t = 3 + i;
            lines.Add(At(t, [Actor(12, 0x233C, position)]));
            lines.Add(EffectEvent(t + .01, 12, i == 0 ? 37468u : 37467u, (uint)i + 1,
                position: center + Vector3.UnitZ * 30));
            position += Vector3.UnitX * 5;
        }
        lines.Add(At(15, [Actor(11, 0x233C, center)]));
        lines.Add(At(20, [Actor(1, 0x428A, center, dead: true)]));
        lines.Add(At(21, []));
        load(guid, lines);
        controller.Seek(.5);
        Check(dangers() == 4 && Shown("heroes-pretenders/flurry/").Length == 3
            && Shown("job-quest/enemy/").Length == 6 && !Damage().Any(),
            "Heroes/Pretenders initial three risky flurries R13 or six enemy OIDs incorrect.");
        dynamic arena = Value(Encounter(), "Arena");
        var runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MasterPlayer.MechanicRuntime");
        var sourceType = runtimeAssembly.GetType("BossMod.Dawntrail.Quest.Role.HeroesAndPretenders.HeroesAndPretenders")!;
        dynamic oracle = sourceType.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var sourceCenter = new Vector3((float)oracle.Center.X, 0, (float)oracle.Center.Z);
        var direction = runtimeAssembly.GetType("BossMod.WDir")!;
        var compared = 0;
        Check(Vector3.Distance((Vector3)arena.Position, sourceCenter) < .0001f, "Heroes/Pretenders source center differs.");
        for (var x = 660.13f; x < 692; x += .6f)
        for (var z = 25.17f; z < 57; z += .6f)
        {
            var expected = (bool)((object)oracle).GetType().GetMethod("Contains")!.Invoke((object)oracle,
                [Activator.CreateInstance(direction, [x - sourceCenter.X, z - sourceCenter.Z])])!;
            Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, $"Heroes/Pretenders source polygon mismatch at {x},{z}.");
            ++compared;
        }
        controller.Seek(2.5);
        Check(Shown("heroes-pretenders/flurry/").Length == 3, "Flurry source cancel/death/destroy consumed line.");
        controller.Seek(3.5);
        var flurry = ((IEnumerable)Value(Encounter(), "HeroesFlurries")).Cast<object>().Single();
        Check((int)flurry.GetType().GetField("Item6")!.GetValue(flurry)! == 7,
            "Flurry first axial position match failed from unrelated effect caster.");
        controller.Seek(10.5);
        Check(Shown("heroes-pretenders/flurry/").Length == 0 && dangers() == 1,
            "Eight effects failed line retirement or incorrectly consumed ordinary live cast.");
        controller.Seek(15.5);
        Check(dangers() == 0, "Real ordinary cast ending failed.");
        controller.Seek(20.5);
        Check(Value(Encounter(), "Active") == null && previews().Length == 0, "Primary death failed owner cleanup.");
        controller.Seek(.5);
        Check(Shown("heroes-pretenders/flurry/").Length == 3, "Reverse Seek retained consumed flurry.");
        Isolated();
        stop();
        Console.WriteLine($"PASS native HeroesPretenders production eight flurry effects exact axial caster position three risky cached circles six OID markers R13 source polygon oracle {compared} points observed ends reverse Seek and owner isolation");

        string Director(double t, uint command) => JsonSerializer.Serialize(new
        {
            t, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
            data = new { Command = command, Timestamp = Start.AddSeconds(t) },
        });
        load(guid, [Header(1218), At(0, [Actor(1, 0x428A, center, combat: true),
            Actor(10, 0x428E, center, cast: Cast(37464, 30, center, target: 2, startedAt: 0)),
            Actor(11, 0x233C, center), Actor(12, 0x233C, center),
            Npc(center), Actor(100, 0, center + Vector3.UnitX * 10), Actor(101, 0, center + Vector3.UnitX)], true),
            At(1, [Actor(101, 0, center + Vector3.UnitZ * 12)]),
            At(2, [Actor(11, 0x233C, center, cast: Cast(37463, 30, center, target: 100, startedAt: 2))]),
            At(3, [Npc(center, true)]), At(4, [Npc(center)]), At(5, [], removed: [2]),
            At(5.5, [Npc(center + Vector3.UnitX * 12)]),
            At(6, [Actor(10, 0x428E, center), Actor(11, 0x233C, center),
                Actor(12, 0x233C, center, cast: Cast(37459, 30, center, target: 2, startedAt: 6))]),
            At(6.2, [], removed: [2]),
            JsonSerializer.Serialize(new { t = 6.3, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent",
                packetFacing = 0f, data = new { SourceId = 12ul, TargetId = 2ul, ActionId = 37459, CastTime = 30f,
                    EffectPosition = Pose(center + Vector3.UnitX), Timestamp = Start.AddSeconds(6) } }),
            At(6.4, [Npc(center - Vector3.UnitX * 12)]),
            At(7, [Npc(center - Vector3.UnitX * 12, true)]),
            At(8, [Actor(12, 0x233C, center, dead: true, cast: Cast(37459, 30, center, target: 2, startedAt: 6))]),
            At(9, [Actor(12, 0x233C, center)]), At(10, [Actor(1, 0x428A, center, dead: true)]),
            At(10.5, [Actor(1, 0x428A, center, combat: true)]), At(11, [], removed: [1]),
            At(12, [Actor(1, 0x428A, center, combat: true)]),
            Director(13, 0x40000005), At(13.5, []), Director(14, 0x40000011), At(14.1, []), At(15, [])]);
        controller.Seek(.5);
        Check(Aoes().Any(a => ((string)Value(a, "Id")).EndsWith("/avoid")
            && ((string)Value(a, "Id")).StartsWith("quest/stack/")), "Full two-person NPC stack accepted outside viewer.");
        controller.Seek(2.5);
        Check(Aoes().Any(a => (string)Value(a, "Id") == "quest/stack/join")
            && Aoes().Any(a => ((string)Value(a, "Id")).Contains("quest/spread/0/separate/2"))
            && Damage().Length == 0, "Independent stack/spread conflict lost a constraint or invented prediction.");
        controller.Seek(3.5);
        Check(Shown("quest/stack/").Length == 0, "Dead Gold Dust target still drawn.");
        controller.Seek(5.7);
        var mark = Aoes().Single(a => (string)Value(a, "Id") == "quest/stack/0/display");
        Check(Vector3.Distance((Vector3)Value(mark, "Position"), center) < .05f, "Destroyed Gold Dust target rebound to same ID NPC.");
        controller.Seek(6.35);
        Check(Shown("heroes-pretenders/arrow/").Length == 1 && Damage().Length == 0,
            "Same cast packet correction removed destroyed target bait or invented a missing party slot.");
        controller.Seek(7.5);
        Check(Shown("heroes-pretenders/arrow/").Length == 1 && Damage().Length == 1
            && (int)Value(Damage()[0], "PartySlot") == 24
            && Vector3.Distance((Vector3)Value(Aoes().Single(a => ((string)Value(a, "Id")).StartsWith("heroes-pretenders/arrow/")
                && ((string)Value(a, "Id")).EndsWith("/display")), "Position"), center + Vector3.UnitX * 12) < .05f,
            "Old NPC arrow reference or same ID target-only tankbuster slot lost after replacement/death.");
        controller.Seek(8.5);
        Check(Shown("heroes-pretenders/arrow/").Length == 0 && Damage().Length == 0, "Dead arrow source still active.");
        controller.Seek(10.7);
        Check(Value(Encounter(), "Active") == null, "Completed healed primary repulled.");
        controller.Seek(12.5);
        Check(Value(Encounter(), "Active") != null, "New raw primary failed to pull.");
        controller.Seek(13.7);
        Check(Value(Encounter(), "Active") == null && dangers() == 0, "Wipe repulled stale snapshot.");
        controller.Seek(14.5);
        Check(Value(Encounter(), "Active") != null, "Wipe unlock did not reopen.");
        controller.Seek(2.5);
        manager.SetMechanismEnabled(guid, "Heroes and Pretenders", false);
        controller.Seek(2.6);
        Check(dangers() == 0 && previews().Length == 0, "Disabled Heroes/Pretenders retained output.");
        manager.SetMechanismEnabled(guid, "Heroes and Pretenders", true);
        controller.Seek(2.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native HeroesPretenders production exact two capacity independent R8 R9 stack spread NPC24 dead target frozen reference arrow tankbuster primary completion new raw generation wipe reverse Seek mechanism disable and owner cleanup");
    }
}
