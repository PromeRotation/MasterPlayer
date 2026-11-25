using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Linq.Expressions;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunQuestTargets(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        var assembly = managerObject.GetType().Assembly;
        var settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var movement = settings.GetType().GetField("EnableSmartMovement")!;
        var previous = movement.GetValue(settings);
        var hostType = assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        var bridge = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(hostType);
        object Script() => ((IEnumerable)manager.Running.Values).Cast<object>().Single()
            .GetType().GetProperty("Script")!.GetValue(((IEnumerable)manager.Running.Values).Cast<object>().Single())!;
        ulong Planned() => (ulong)Script().GetType().GetProperty("PlannedTargetId")!.GetValue(Script())!;
        void Update() => managerObject.GetType().GetMethod("UpdateReplayScripts", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(managerObject, [.1f]);
        object Enemy(ulong id, uint oid, bool dead = false, bool targetable = true, bool hostile = true, ushort index = 2, uint hp = 1000)
            => new { Id = id, Oid = oid, Kind = 2, SubKind = 5, ObjectIndex = index, Targetable = targetable, Hostile = !hostile, IsEnemy = hostile,
                Dead = dead, Hp = dead ? 0 : hp, MaxHp = 1000, Position = Pose(Vector3.UnitX * (float)id), InCombat = false };
        var cases = new (ushort Territory, uint Oid, string Id)[]
        {
            (476, 0x1268, "masterplayer.quest.hands-of-fate"), (488, 0x1236, "masterplayer.quest.power-of-a-tourney"),
            (484, 0x11E4, "masterplayer.quest.ninja-assassin"), (466, 0x1231, "masterplayer.quest.quarantine"),
            (273, 0x3BC, "masterplayer.quest.oh-captain-my-captain"),
            (317, 0x6B1, "masterplayer.quest.the-voidgate-breathes-gloomy"),
            (314, 0x815, "masterplayer.quest.brother-from-another-mother"),
            (323, 0x5EC, "masterplayer.quest.austerities-of-flame"),
            (498, 0x11D2, "masterplayer.quest.big-sollerets-to-fill"),
            (267, 0x2A4, "masterplayer.quest.the-threat-of-perplexity"),
        };
        try
        {
            movement.SetValue(settings, true);
            foreach (var (territory, oid, id) in cases)
            {
                load(id,
                [
                    Header(territory),
                    Frame(territory, 0, [Actor(100, 0, default), Enemy(1, 1), Enemy(2, oid)], full: true),
                    Frame(territory, 1, [Enemy(2, oid, targetable: false)]),
                    Frame(territory, 2, [Enemy(2, oid, dead: true)]),
                    Frame(territory, 3, [Enemy(2, oid, hostile: false)]),
                    Frame(territory, 4, [Enemy(2, oid, index: 3)]),
                    Frame(territory, 5, [Enemy(2, oid, index: 198)]),
                    Frame(territory, 6, [], removed: [2]), Frame(territory, 7, []),
                ]);
                controller.Seek(.5);
                Check(Planned() == 2, $"Quest {territory} failed to plan its priority target without a boss or combat.");
                Check(!(bool)hostType.GetMethod("IsReplaySuppressed", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(bridge, null)!,
                    "A target-only quest script suppressed unrelated territory mechanics.");
                var script = Script();
                var hints = script.GetType().GetProperty("Hints", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!.GetValue(script)!;
                var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
                Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
                    "Quest replay published a live PR request with movement enabled.");
                foreach (var time in new[] { 1.5, 2.5, 3.5, 4.5, 6.5 })
                {
                    controller.Seek(time);
                    Check(Planned() == 0, $"Quest {territory} kept an unavailable, allied or illegal target at {time}.");
                }
                controller.Seek(5.5);
                Check(Planned() == 2, "The final legal enemy slot did not restore the planned priority.");
                manager.SetMechanismEnabled(id, "优先目标", false);
                Update();
                Check(Planned() == 0, "Disabling a quest mechanism retained its target.");
                manager.SetMechanismEnabled(id, "优先目标", true);
                Update();
                Check(Planned() == 2, "Re-enabling a quest mechanism did not restore planning.");
                Check(dangers() == 0 && previews().Length == 0, "A target-only quest invented AOE output.");
                script = Script();
                stop();
                Check((ulong)script.GetType().GetProperty("PlannedTargetId")!.GetValue(script)! == 0, "Quest stop retained its target plan.");
                Console.WriteLine($"PASS production quest {territory}: preferred OID, legal enemy slots, no combat prerequisite, availability, reverse Seek, settings, non-suppression and replay control isolation");
            }
            foreach (var (territory, id, firstOid, secondOid) in new (ushort, string, uint, uint)[]
            {
                (233, "masterplayer.quest.chasing-shadows", 0x224, 0x376),
                (252, "masterplayer.quest.lurkers-in-the-grotto", 0x386, 0x386),
            })
            {
                load(id,
                [
                    Header(territory),
                    Frame(territory, 0, [Actor(100, 0, default), Enemy(1, firstOid), Enemy(2, secondOid), Enemy(3, 0x999)], full: true),
                    Frame(territory, 1, [Enemy(3, 0x999, dead: true)]),
                    Frame(territory, 2, [Enemy(2, secondOid, targetable: false)]),
                    Frame(territory, 3, [Enemy(1, firstOid, hostile: false)]),
                    Frame(territory, 4, [Enemy(3, 0x999, index: 198)]),
                    Frame(territory, 5, []),
                ]);
                controller.Seek(.5);
                Check(Planned() == 3, $"ARR quest {territory} ignored its highest priority unlisted OID.");
                controller.Seek(1.5);
                Check(Planned() == (territory == 233 ? 2ul : 1ul), "ARR task priority fallback did not preserve zero as attackable.");
                controller.Seek(2.5);
                Check(Planned() == 1, "Only priority zero target was rejected.");
                controller.Seek(3.5);
                Check(Planned() == 0, "ARR task retained an allied target.");
                controller.Seek(4.5);
                Check(Planned() == 3, "ARR task did not restore its highest priority at the last legal object slot.");
                manager.SetMechanismEnabled(id, "优先目标", false);
                Update();
                Check(Planned() == 0, "ARR task mechanism disable retained target.");
                manager.SetMechanismEnabled(id, "优先目标", true);
                Update();
                Check(Planned() == 3, "ARR task mechanism enable did not restore target.");
                controller.Seek(.5);
                Check(Planned() == 3 && dangers() == 0 && previews().Length == 0, "ARR reverse Seek retained later priorities or invented geometry.");
                var script = Script();
                var hints = script.GetType().GetProperty("Hints", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!.GetValue(script)!;
                var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
                Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
                    "ARR task replay published a live PR control request.");
                stop();
                Check((ulong)script.GetType().GetProperty("PlannedTargetId")!.GetValue(script)! == 0, "ARR task owner cleanup retained target.");
                Console.WriteLine($"PASS production ARR quest {territory}: full priority override, unknown OIDs, zero fallback, legal enemy slots, settings, reverse Seek and replay control isolation");
            }
            foreach (var (territory, id, low, high) in new (ushort, string, uint, uint)[]
            {
                (260, "masterplayer.quest.starcrossed-rivals", 0x2BA, 0x2BD),
                (275, "masterplayer.quest.lord-of-the-inferno", 0x3C7, 0x999),
                (315, "masterplayer.quest.five-easy-pieces", 0x640, 0x999),
                (461, "masterplayer.quest.familiar-faces", 0x999, 0x106C),
                (414, "masterplayer.quest.the-crow-knows", 0xDBE, 0xDC9),
                (669, "masterplayer.quest.shades-of-shatotto", 0x1AE8, 0x999),
                (673, "masterplayer.quest.one-golem-to-rule-them-all", 0x1C81, 0x1EB4),
                (410, "masterplayer.quest.the-reason-roaille", 0x999, 0xDB7),
            })
            {
                load(id,
                [
                    Header(territory),
                    Frame(territory, 0, [Actor(100, 0, default), Enemy(1, low), Enemy(2, high)], full: true),
                    Frame(territory, 1, [Enemy(2, high, hp: 1)]),
                    Frame(territory, 2, [Enemy(2, high, hostile: false)]),
                    Frame(territory, 3, [Enemy(1, low, index: 3)]),
                    Frame(territory, 4, [Enemy(2, high, index: 198)]),
                    Frame(territory, 5, []),
                ]);
                controller.Seek(.5);
                Check(Planned() == 2, $"Quest {territory} failed to apply its exact priority.");
                controller.Seek(1.5);
                Check(Planned() == 1, "Pointless setter protection or priority-zero fallback missing.");
                controller.Seek(2.5);
                Check(Planned() == 1, "Allied high-priority object remained attackable.");
                controller.Seek(3.5);
                Check(Planned() == 0, "Illegal object slot retained target.");
                controller.Seek(4.5);
                Check(Planned() == 2, "Final legal slot failed to restore target.");
                manager.SetMechanismEnabled(id, "优先目标", false);
                Update();
                Check(Planned() == 0, "New zone disable retained target.");
                manager.SetMechanismEnabled(id, "优先目标", true);
                Update();
                Check(Planned() == 2, "New zone enable failed to restore target.");
                controller.Seek(.5);
                Check(Planned() == 2 && dangers() == 0 && previews().Length == 0, "New zone reverse Seek or geometry contract failed.");
                var script = Script();
                stop();
                Check((ulong)script.GetType().GetProperty("PlannedTargetId")!.GetValue(script)! == 0, "New zone stop retained target.");
                Console.WriteLine($"PASS production quest {territory}: exact full priorities, Pointless protection, zero fallback, object slots, no combat prerequisite, settings, reverse Seek and cleanup");
            }
            foreach (var (territory, id) in new (ushort, string)[]
            {
                (251, "masterplayer.quest.return-of-the-holyfist"),
                (304, "masterplayer.quest.notorious-biggs"),
                (309, "masterplayer.quest.every-little-thing-she-does-is-magitek")
            })
            {
                load(id,
                [
                    Header(territory),
                    Frame(territory, 0, [Actor(100, 0, default), Enemy(1, 0x999), Enemy(2, 0xABC)], full: true),
                    Frame(territory, 1, [Enemy(1, 0x999, hp: 1)]),
                    Frame(territory, 2, [Enemy(2, 0xABC, hp: 1)]),
                    Frame(territory, 3, [Enemy(1, 0x999, index: 3)]),
                    Frame(territory, 4, [Enemy(1, 0x999, index: 198)]),
                    Frame(territory, 5, []),
                ]);
                controller.Seek(.5);
                Check(Planned() == 1, "PrioritizeAll did not make passive targets attackable.");
                controller.Seek(1.5);
                Check(Planned() == 2, "PrioritizeAll failed to protect Pointless and choose another zero-priority target.");
                controller.Seek(2.5);
                Check(Planned() == 0, "PrioritizeAll retained only Pointless targets.");
                controller.Seek(3.5);
                Check(Planned() == 0, "PrioritizeAll retained an illegal actor slot.");
                controller.Seek(4.5);
                Check(Planned() == 1, "PrioritizeAll final legal slot did not recover.");
                manager.SetMechanismEnabled(id, "优先目标", false);
                Update();
                Check(Planned() == 0, "PrioritizeAll mechanism disable retained its target.");
                manager.SetMechanismEnabled(id, "优先目标", true);
                Update();
                Check(Planned() == 1, "PrioritizeAll mechanism re-enable failed to recover.");
                controller.Seek(.5);
                Check(Planned() == 1 && dangers() == 0 && previews().Length == 0, "PrioritizeAll reverse Seek or geometry contract failed.");
                var script = Script();
                stop();
                Check((ulong)script.GetType().GetProperty("PlannedTargetId")!.GetValue(script)! == 0, "PrioritizeAll stop retained its target.");
                Console.WriteLine($"PASS production quest {territory}: PrioritizeAll zero-priority passive targets, Pointless and slot protection, settings, reverse Seek and cleanup");
            }
            RunQuestPartialReplay(load, controllerObject, Planned, stop, previews, dangers);
            RunQuestExplicitNegative(load, controllerObject, Planned, stop, previews, dangers);
            RunQuestTargetLive(assembly);
            RunQuestTargetLive(assembly, partial: true);
            RunQuestTargetLive(assembly, explicitNegative: true);
        }
        finally
        {
            movement.SetValue(settings, previous);
        }
    }

    private static void RunQuestPartialReplay(Action<string, IEnumerable<string>> load, object controllerObject,
        Func<ulong> planned, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        const ushort territory = 273;
        object Enemy(ulong id, uint oid, bool aggro = false, bool combat = false, ulong target = 0,
            uint hp = 100, uint fate = 0, bool invincible = false) => new
        {
            Id = id, Oid = oid, Kind = 2, IsEnemy = true, ObjectIndex = 2, Targetable = true, Hp = hp,
            AggroPlayer = aggro, InCombat = combat, TargetId = target, FateId = fate, Position = Pose(default),
            Statuses = invincible ? new[] { new { Id = 671u, ExpiresAt = Start.AddSeconds(30) } } : []
        };
        object Friend(bool targetable) => new
        {
            Id = 700ul, Oid = 0xFFFFu, Kind = 2, SubKind = 5, IsEnemy = false, Targetable = targetable,
            Hp = 0, Dead = true, Position = Pose(default)
        };
        load("masterplayer.quest.oh-captain-my-captain",
        [
            Header(territory),
            Frame(territory, 0, [Actor(100, 0, default), Enemy(1, 0x999), Enemy(2, 0x3BC), Friend(true)], full: true),
            Frame(territory, 1, [Enemy(2, 0x3BC, hp: 1), Enemy(1, 0x999, aggro: true)]),
            Frame(territory, 2, [Enemy(1, 0x999)]),
            Frame(territory, 3, [Enemy(1, 0x999, combat: true, target: 700)]),
            Frame(territory, 4, [Friend(false)]),
            Frame(territory, 5, [Friend(true)]),
            Frame(territory, 6, [Enemy(1, 0x999, aggro: true, fate: 55)], allowedFateId: 55),
            Frame(territory, 7, [], allowedFateId: 0),
            Frame(territory, 8, [Enemy(1, 0x999, aggro: true)], allowedFateId: 0),
            Frame(territory, 9, [Enemy(1, 0x999, aggro: true, invincible: true)], allowedFateId: 0),
            Frame(territory, 10, [Enemy(2, 0x3BC, invincible: true)]),
            Frame(territory, 11, []),
        ]);
        foreach (var (time, expected) in new (double, ulong)[]
        {
            (.5, 2), (1.5, 1), (2.5, 0), (3.5, 1), (4.5, 0), (5.5, 1),
            (6.5, 1), (7.5, 0), (8.5, 1), (9.5, 0), (10.5, 2), (6.5, 1), (4.5, 0)
        })
        {
            controller.Seek(time);
            Check(planned() == expected, $"Captain base priority, NPC or Fate replay differs at {time}: {planned()} != {expected}.");
        }
        Check(previews().Length == 0 && dangers() == 0, "Captain base priority created unrelated geometry.");
        stop();
        Console.WriteLine("PASS production Captain base priorities: enmity without combat, NPC roster and death, passive exclusion, Fate enter/leave, invincibility overrides, sticky Pointless and reverse Seek");
    }

    private static void RunQuestExplicitNegative(Action<string, IEnumerable<string>> load, object controllerObject,
        Func<ulong> planned, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        const ushort territory = 307;
        object Enemy(ulong id, uint oid, bool aggro = false, bool dead = false) => new
        {
            Id = id, Oid = oid, Kind = 2, IsEnemy = true, ObjectIndex = 2, Targetable = true, Hp = dead ? 0 : 100,
            Dead = dead, AggroPlayer = aggro, Position = Pose(default)
        };
        load("masterplayer.quest.the-lominsan-way",
        [
            Header(territory),
            Frame(territory, 0, [Actor(100, 0, default), Enemy(1, 0x6E9, true), Enemy(2, 0x6EA),
                Enemy(3, 0xFFFF, true)], full: true),
            Frame(territory, 1, [Enemy(2, 0x6EA, dead: true)]),
            Frame(territory, 2, [Enemy(3, 0xFFFF)]),
            Frame(territory, 3, [Enemy(1, 0x6E9, true)]),
            Frame(territory, 4, [Enemy(2, 0x6EA)]),
            Frame(territory, 5, []),
        ]);
        foreach (var (time, expected) in new (double, ulong)[] { (.5, 2), (1.5, 3), (2.5, 0), (3.5, 0), (4.5, 2), (.5, 2) })
        {
            controller.Seek(time);
            Check(planned() == expected, $"The Lominsan Way explicit negative/unmatched fallback differs at {time}.");
        }
        Check(dangers() == 0 && previews().Length == 0, "Explicit target priorities invented geometry.");
        stop();
        Console.WriteLine("PASS production Lominsan Way: explicit priority one and minus one, unmatched enmity fallback and passive exclusion, reverse Seek and cleanup");
    }

    private static void RunQuestTargetLive(Assembly assembly, bool partial = false, bool explicitNegative = false)
    {
        const BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;
        var actorType = assembly.GetType("MasterPlayer.Replay.ReplayActorState")!;
        var scriptName = explicitNegative ? "TheLominsanWay" : partial ? "OhCaptainMyCaptain" : "FamiliarFaces";
        var script = Activator.CreateInstance(assembly.GetType("MasterPlayer.Scripts.Quests." + scriptName)!)!;
        var scriptType = script.GetType().BaseType!;
        var baseType = scriptType.BaseType!;
        object Actor(ulong id, uint oid)
        {
            var actor = Activator.CreateInstance(actorType)!;
            foreach (var (name, value) in new (string, object)[]
            {
                ("Id", id), ("Oid", oid), ("Kind", (byte)2), ("Hp", 100u),
                ("IsEnemy", true), ("ObjectIndex", (ushort)2), ("Targetable", true), ("TargetId", 2ul),
                ("AggroPlayer", true), ("Level", (byte)20)
            }) actorType.GetProperty(name)!.SetValue(actor, value);
            return actor;
        }
        var player = Actor(100, 0);
        if (explicitNegative) actorType.GetProperty("TargetId")!.SetValue(player, 1ul);
        var ordinary = Actor(1, explicitNegative ? 0x6E9u : 0x999u);
        var preferredOid = explicitNegative ? 0x6EAu : partial ? 0x3BCu : 0x106Cu;
        var preferred = Actor(2, preferredOid);
        var actors = Array.CreateInstance(actorType, 2);
        actors.SetValue(ordinary, 0);
        actors.SetValue(preferred, 1);
        var scans = 0;
        var capture = typeof(NativeDungeonReplayRegression).GetMethod(nameof(CreateQuestLiveCapture), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(actorType).Invoke(null, [(Func<Array>)(() => actors), (Action)(() => ++scans)])!;
        scriptType.GetProperty("CaptureLiveActors", PRIVATE)!.SetValue(script, capture);
        var allowedFate = 0u;
        if (partial || explicitNegative)
        {
            var partyProperty = scriptType.GetProperty("CaptureLiveParty", PRIVATE)!;
            var emptyParty = Array.CreateInstance(assembly.GetType("MasterPlayer.Replay.ReplayPartyMember")!, 0);
            partyProperty.SetValue(script, Expression.Lambda(partyProperty.PropertyType, Expression.Constant(emptyParty)).Compile());
            scriptType.GetProperty("CaptureLiveAllowedFate", PRIVATE)!.SetValue(script, (Func<byte?, uint>)(_ => allowedFate));
        }
        var controlType = assembly.GetType("MasterPlayer.MechanicHost.MechanicFacingControl")!;
        var updateType = controlType.GetNestedType("UpdateRequest", BindingFlags.NonPublic)!;
        ulong published = 0;
        ulong cleared = 0;
        var requests = 0;
        var parameters = updateType.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
        Action<ulong, ulong, ulong[], bool, bool, bool> record = (target, clear, forbidden, hold, block, cancel) =>
        {
            published = target;
            cleared = clear;
            ++requests;
            Check(forbidden.Length == 0 && !hold && !block && !cancel, "Quest target priorities expanded into action/target prohibitions.");
        };
        var update = Expression.Lambda(updateType, Expression.Invoke(Expression.Constant(record),
            parameters[1], parameters[2], parameters[3], parameters[5], parameters[6], parameters[7]), parameters).Compile();
        using var control = (IDisposable)Activator.CreateInstance(controlType, PRIVATE, null,
            [update, (Action<string>)(_ => { published = 0; cleared = 0; })], null)!;
        var hintsType = assembly.GetType("MasterPlayer.Scripting.ScriptHintsApi")!;
        var hints = Activator.CreateInstance(hintsType, PRIVATE, null,
            [(Func<bool>)(() => false), null, control, (Action<bool, ulong[]>)((_, _) => { })], null)!;
        var now = Start;
        var hostType = assembly.GetType("MasterPlayer.Scripting.IScriptHostContext")!;
        var host = typeof(CatalogServiceProxy).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(hostType).Invoke(null, [(Func<string, object?[], object?>)((name, _) => name switch
            {
                "get_CurrentPlayer" => player, "get_CurrentPlayerId" => 100ul,
                "get_CurrentTime" => now, "get_ReplayWorld" => null, "get_IsReplay" => false,
                "get_Hints" => hints, "IsMechanismEnabled" => true,
                _ => throw new InvalidOperationException("Unexpected live quest host access: " + name)
            })])!;
        baseType.GetProperty("Host")!.SetValue(script, host);
        try
        {
            baseType.GetMethod("StartInternal", PRIVATE)!.Invoke(script, null);
            Check((ulong)scriptType.GetProperty("PlannedTargetId")!.GetValue(script)! == 2 && published == 2 && scans == 1,
                "Live quest did not select and publish its preferred target.");
            Check(cleared == (explicitNegative ? 1ul : 0), "Explicit negative current target was not independently cleared.");
            var effectType = assembly.GetType("MasterPlayer.Replay.ReplayIncomingEffect")!;
            var incoming = Activator.CreateInstance(effectType, [0, 1u, (byte)0, false, false])!;
            effectType.GetProperty("HpDelta")!.SetValue(incoming, -99);
            var effects = Array.CreateInstance(effectType, 1);
            effects.SetValue(incoming, 0);
            actorType.GetProperty("IncomingEffects")!.SetValue(preferred, effects);
            now = now.AddMilliseconds(16);
            baseType.GetMethod("UpdateInternal", PRIVATE)!.Invoke(script, [.016f]);
            var replacement = explicitNegative ? 0ul : 1ul;
            Check((ulong)scriptType.GetProperty("PlannedTargetId")!.GetValue(script)! == replacement && published == replacement
                && scans == 2 && requests == 2 && cleared == (explicitNegative ? 1ul : 0),
                "Live quest retained its cached target after next-frame incoming lethal damage.");
            if (partial)
            {
                actorType.GetProperty("FateId")!.SetValue(ordinary, 55u);
                now = now.AddMilliseconds(16);
                baseType.GetMethod("UpdateInternal", PRIVATE)!.Invoke(script, [.016f]);
                Check(published == 0, "Live partial quest retained an enemy from another Fate.");
                allowedFate = 55;
                now = now.AddMilliseconds(16);
                baseType.GetMethod("UpdateInternal", PRIVATE)!.Invoke(script, [.016f]);
                Check(published == 1, "Live partial quest failed to consume allowed Fate sampling.");
                allowedFate = 0;
                now = now.AddMilliseconds(16);
                baseType.GetMethod("UpdateInternal", PRIVATE)!.Invoke(script, [.016f]);
                Check(published == 0, "Live partial quest kept its target after leaving the Fate.");
            }
            actorType.GetProperty("IncomingEffects")!.SetValue(preferred, Array.CreateInstance(effectType, 0));
            now = now.AddMilliseconds(16);
            baseType.GetMethod("UpdateInternal", PRIVATE)!.Invoke(script, [.016f]);
            Check((ulong)scriptType.GetProperty("PlannedTargetId")!.GetValue(script)! == 2 && published == 2 && scans == (partial ? 6 : 3),
                "Live quest failed to restore its preferred target on the next frame.");
        }
        finally
        {
            baseType.GetMethod("StopInternal", PRIVATE)!.Invoke(script, null);
            hintsType.GetMethod("Cleanup", PRIVATE)!.Invoke(hints, null);
        }
        Check(published == 0 && cleared == 0, "Live quest stop retained its PR target request.");
        Console.WriteLine($"PASS production quest live path {scriptName}: 16ms incoming lethal target replacement and restoration, explicit current-target clearing, actual Hints publication and owner cleanup");
    }

    private static Func<DateTime, IEnumerable<T>> CreateQuestLiveCapture<T>(Func<Array> read, Action sampled)
        => _ => { sampled(); return read().Cast<T>(); };
}
