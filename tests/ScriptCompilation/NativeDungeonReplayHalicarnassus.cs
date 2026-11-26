using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHalicarnassus(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.raid.halicarnassus.normal";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var assembly = runtimeObject.GetType().Assembly;
        object Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, flags) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Script() => Read(manager.Running[id], "Script");
        object Encounter() => Read(Script(), "_encounter");
        object Model() => Read(Encounter(), "Halicarnassus");
        object[] Items(string name) => ((IEnumerable)Read(Model(), name)).Cast<object>().ToArray();
        object[] Shown() => previews().Where(p => ((string)Read(p, "Id")).Contains("halicarnassus/aoe/")).ToArray();
        void Seek(double at)
        {
            Check((int)controller.RouterErrorCount == 0, "Halicarnassus callback failed before Seek.");
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0, "Halicarnassus callback failed during Seek.");
        }
        object[] roster = [new { Slot = 0, EntityId = 100ul, Role = "WrongRole" }];
        object[] Initial(bool combat = true) => [Actor(1, 0x1E14, default, combat: combat),
            Actor(2, 0x186D, new(2, 0, 3)), Actor(100, 0, new(-5, 0, -5), classJob: 19)];
        object Spell(uint action, double start)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 3, new(2, 0, 3), .4f, startedAt: start))!;
            node["IsSpell"] = true; node["TotalTime"] = 3;
            return node;
        }
        string Raw(double t, uint action, ulong source = 2, byte type = 1)
            => JsonSerializer.Serialize(new { t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, MainTargetId = 0ul, ActionId = action, ActionType = type,
                    Timestamp = Start.AddSeconds(t) } });

        var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
        Check(Directory.Exists(sqpack), "Halicarnassus metadata requires FFXIV_SQPACK.");
        using (var game = new Lumina.GameData(sqpack!, new Lumina.LuminaOptions
            { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified }))
        {
            Check(game.GetExcelSheet<Lumina.Excel.Sheets.ContentFinderCondition>()!.GetRow(254).TerritoryType.RowId == 693
                && game.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()!.GetRow(693).ContentFinderCondition.RowId == 254,
                "Halicarnassus CFC254 / Territory693 bidirectional map.");
        }
        Check((bool)manager.IsScriptEnabled(id), "Halicarnassus must default enabled.");
        manager.SetScriptEnabled(id, false); manager.RefreshCatalog();
        Check(!(bool)manager.IsScriptEnabled(id), "Halicarnassus explicit disable lost on refresh.");
        manager.SetScriptEnabled(id, true);
        Console.WriteLine("PASS native Halicarnassus client metadata T693 CFC254 exact source primary default enabled explicit disable refresh");

        foreach (var action in new uint[] { 9318, 9314, 9313, 9312, 9323, 9327 })
        {
            load(id, [Header(693), Frame(693, 0, Initial(false), true, party: roster),
                Frame(693, .1, [Actor(1, 0x1E14, default, combat: true),
                    Actor(2, 0x186D, new(2, 0, 3), cast: Spell(action, .1))], party: roster),
                CancelHoH(.2, 2, action), Raw(.3, action),
                Frame(693, 10, [], party: roster),
                Frame(693, 11, [Actor(2, 0x186D, default)], party: roster)]);
            Seek(.05);
            Check(dangers() == 0 && Shown().Length == 0, "Precombat ordinary output.");
            Seek(10.1);
            Check(Items("Ordinary").Length == 1 && Shown().Length == 1 && dangers() == 1,
                $"Ordinary {action}: packet Cancel/Effect/timeout changed raw lifetime.");
            var oldOrigin = (Vector3)Read(Items("Ordinary")[0], "Origin");
            Check(oldOrigin == new Vector3(2, 0, 3) && (float)Read(Items("Ordinary")[0], "Rotation") == .4f,
                "Locked ordinary position/direction changed.");
            Seek(11.1);
            Check(dangers() == 0 && Shown().Length == 0, "Actual raw Finish did not revoke ordinary output.");
            Seek(.15);
            Check(dangers() == 1 && Shown().Length == 1, "Reverse Seek did not rebuild ordinary.");
            stop();
            Console.WriteLine($"PASS native Halicarnassus ordinary {action} raw clock lock packet cancellation effect timeout watched Finish inverse Seek owner");
        }

        load(id, [Header(693), Frame(693, 0, Initial(), true, party: roster),
            Frame(693, .1, [Actor(2, 0x186D, new(80, 0, 80), cast: Spell(9329, .1))], party: roster),
            Frame(693, .2, [Actor(2, 0x186D, default)], party: roster),
            Frame(693, .3, [Actor(2, 0x186D, default, cast: Spell(9329, .3))], party: roster),
            Frame(693, .4, [Actor(2, 0x186D, default)], party: roster),
            Raw(.5, 9330), Raw(.6, 9329, 100), Raw(.7, 9329, type: 2), Raw(.8, 9329, 1)]);
        Seek(.25);
        Check(Items("Waltz").Length == 12 && Shown().Length == 12 && dangers() == 12,
            "Immediate absolute Waltz board or real Finish persistence.");
        Seek(.45);
        Check(Items("Waltz").Length == 24 && dangers() == 24, "Repeated Waltz Start did not append.");
        Seek(.75);
        Check(dangers() == 24, "Helper wrong action / player / nonSpell consumed board.");
        Seek(.85);
        Check(Items("Waltz").Length == 0 && dangers() == 0, "Cross-source native watched Effect did not clear board.");
        stop();
        Console.WriteLine("PASS native Halicarnassus absolute Waltz12 immediate danger repeated24 Finish retained Native Spell cross-source only owner");

        load(id, [Header(693), Frame(693, 0, Initial(), true, party: roster),
            Frame(693, .1, [Actor(2, 0x186D, default, cast: Spell(9325, .1))], party: roster),
            Frame(693, .2, [Actor(2, 0x186D, default)], party: roster),
            Frame(693, .3, [Actor(100, 0, new(-5, 0, -5), classJob: 24)], party: roster),
            Frame(693, .4, [Actor(100, 0, new(-5, 0, -5), classJob: 41)], party: roster),
            Frame(693, .5, [Actor(100, 0, new(-5, 0, -5), classJob: 8)], party: roster),
            Frame(693, .6, [Actor(100, 0, new(-5, 0, -5), classJob: 19)], party: roster),
            Raw(.8, 9325)]);
        Seek(.25);
        Check(dangers() == 15 && Shown().Length == 15 && Items("Game").Length == 16,
            "Intrinsic Tank ClassJob role failed.");
        Seek(.35); Check(dangers() == 15, "Intrinsic Healer role failed.");
        Seek(.45); Check(dangers() == 14, "Intrinsic DPS role failed.");
        Seek(.55); Check(dangers() == 16, "Crafting role must have no safe tile.");
        Seek(.65);
        Check(dangers() == 15 && Items("Game").Length == 16, "Tank return rebuilt underlying board.");
        var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [new Vector3(-5, 0, -5), 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
        Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback"),
            "Game real solver failed immediate chessboard escape.");
        var end = (Vector3)Read((object)result, "TargetPosition");
        Check(end.X > 0 && end.X < 10 && end.Z > -10 && end.Z < 0,
            $"Game real solver did not enter Tank tile: {end}.");
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest"), "Replay emitted real facing.");
        Seek(.85); Check(dangers() == 0, "Game native clear leaked dangers.");
        stop();
        Console.WriteLine("PASS native Halicarnassus intrinsic role Tank Healer DPS Craft first16 dynamic actual solver immediate escape no fallback replay isolation owner");

        string Director(double t, uint command) => JsonSerializer.Serialize(new
        {
            t, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
            data = new { Command = command, Timestamp = Start.AddSeconds(t) },
        });
        load(id, [Header(693), Frame(693, 0, Initial(), true, party: roster),
            Frame(693, .1, [Actor(2, 0x186D, default, cast: Spell(9325, .1)),
                Actor(3, 0x1E14, default, combat: true)], party: roster),
            Frame(693, .2, [Actor(2, 0x186D, default)], party: roster),
            JsonSerializer.Serialize(new { t = .3, kind = "BattleEnded",
                type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent", data = new { Timestamp = Start.AddSeconds(.3) } }),
            Director(.4, 0x40000005),
            Frame(693, .5, [Actor(2, 0x186D, default, cast: Spell(9325, .5))], party: roster),
            Director(.6, 0x40000006), Director(.8, 0x40000011), Raw(1, 9325)]);
        Seek(.35);
        Check(Items("Game").Length == 16 && dangers() == 15 && !(bool)Read(Script(), "_suspended"),
            "BattleEnded discarded live chessboard or suspended event routing.");
        Seek(.7);
        Check((bool)Read(Model(), "Loaded") && !(bool)Read(Model(), "Active")
            && Items("Game").Length == 0 && dangers() == 0,
            "Director wipe failed to unload or Pending activated before 0011.");
        Seek(.9);
        Check((bool)Read(Model(), "Active") && Items("Game").Length == 16 && dangers() == 15,
            "0011 failed to release wipe barrier and cold-replay current board cast.");
        Seek(1.1);
        Check(dangers() == 0, "Post-wipe actual Effect routing failed.");
        stop();
        Console.WriteLine("PASS native Halicarnassus actual BattleEnded history Pending Director0005 activation barrier 0006 retained 0011 cold restore owner");

        load(id, [Header(693), Frame(693, 0, Initial(), true, party: roster),
            Frame(693, .1, [Actor(2, 0x186D, default, cast: Spell(9325, .1))], party: roster)]);
        Seek(.2);
        const string mechanism = "欧米茄时空狭缝 德尔塔幻境3";
        manager.SetMechanismEnabled(id, mechanism, false); Seek(.21);
        Check(dangers() == 0 && Shown().Length == 0 && !(bool)Read(Model(), "Active"),
            "Mechanism off leaked chessboard owner output.");
        manager.SetMechanismEnabled(id, mechanism, true); Seek(.22);
        Check(dangers() == 15 && Shown().Length == 15, "Mechanism on failed cold restore current cast.");
        stop();
        Check(dangers() == 0 && Shown().Length == 0, "Script stop leaked Halicarnassus owner output.");
        Console.WriteLine("PASS native Halicarnassus actual mechanism off cleanup on cold restore script stop releases owner");
    }
}
