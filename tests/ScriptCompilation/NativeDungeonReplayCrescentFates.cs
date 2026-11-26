using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCrescentFates(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var center = new Vector3(100, 10, 100);
        var id = "";
        object Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } p) return p.GetValue(obj)!;
                if (type.GetField(name, flags) is { } f) return f.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Script() => Read(manager.Running[id], "Script");
        object Model() => Read(Script(), "encounter");
        object[] Casts() => ((IEnumerable)Read(Model(), "Casts")).Cast<object>().ToArray();
        object[] Damage() => ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().ToArray();
        object[] Shown(string key) => previews().Where(p => ((string)Read(p, "Id")).Contains(key)).ToArray();
        bool Active() => (bool)Read(Script(), "EncounterActive");
        void Seek(double t)
        {
            Check((int)controller.RouterErrorCount == 0, "Crescent callback error before Seek.");
            controller.Seek(t);
            Check((int)controller.RouterErrorCount == 0, "Crescent callback error during Seek.");
        }
        object Spell(uint action, double start = .1)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 3, center, .4f, 100, start))!;
            node["TotalTime"] = 3f;
            node["IsSpell"] = true;
            return node;
        }
        string World(uint territory, double t, uint fate, IEnumerable<object> actors, bool full = false,
            ulong[]? removed = null, Vector3? origin = null, float radius = 40, object[]? party = null, bool known = true)
        {
            var node = JsonSerializer.SerializeToNode(new { t, kind = "WorldFrame", data = new {
                Full = full, Territory = territory, LocalPlayerId = 100, AllowedFateId = fate,
                Actors = actors.ToArray(), Removed = removed ?? [], Party = party ?? [] } })!;
            if (known) node["data"]!["ActiveFate"] = JsonSerializer.SerializeToNode(new {
                Id = fate, Center = Pose(origin ?? center), Radius = radius });
            return node.ToJsonString();
        }
        object[] Initial(uint oid, bool combat = true, object? helperCast = null, byte helperKind = 5) =>
            [Actor(1, oid, center, combat: combat), Actor(100, 0, center),
                Actor(20, 0x233C, center, cast: helperCast, subKind: helperKind)];
        (string Name, uint Territory, uint Cfc, uint Fate, uint Oid, uint Action)[] definitions = [
            ("thewingedterror", 1252u, 1018u, 1965u, 0x46C1u, 41273u),
            ("crescentgremlin", 1346u, 1093u, 2072u, 0x4D8Au, 50226u),
            ("crescentfan", 1346u, 1093u, 2073u, 0x4D8Cu, 50221u),
            ("eyetoeye", 1346u, 1093u, 2075u, 0x4BA7u, 47151u),
            ("demimedusa", 1346u, 1093u, 2083u, 0x4C6Au, 48256u),
            ("arottenaffair", 1346u, 1093u, 2081u, 0x4D61u, 49915u),
            ("adelicatebalance", 1252u, 1018u, 1968u, 0x46B8u, 30340u),
            ("kingofthecrescent", 1252u, 1018u, 1964u, 0x46D7u, 41390u),
            ("madmudarch", 1252u, 1018u, 1969u, 0x46BDu, 29826u),
            ("allureoftheoccult", 1346u, 1093u, 2078u, 0x4D56u, 48944u),
            ("anunendingduty", 1252u, 1018u, 1966u, 0x4735u, 41988u),
            ("galeforceencounter", 1346u, 1093u, 2082u, 0x4BECu, 47583u),
            ("relicicewolf", 1346u, 1093u, 2080u, 0x4D5Eu, 49770u),
            ("thunderregnum", 1346u, 1093u, 2084u, 0x4D63u, 49500u),
            ("apryingeye", 1252u, 1018u, 1970u, 0x47DCu, 43045u),
            ("servingdarkness", 1252u, 1018u, 1972u, 0x4772u, 42183u),
        ];
        var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
        Check(Directory.Exists(sqpack), "Crescent metadata needs FFXIV_SQPACK.");
        using (var data = new Lumina.GameData(sqpack!, new Lumina.LuminaOptions {
            DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified }))
            foreach (var d in definitions)
            {
                var cfc = data.GetExcelSheet<Lumina.Excel.Sheets.ContentFinderCondition>()!.GetRow(d.Cfc);
                var territory = data.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()!.GetRow(d.Territory);
                var fate = data.GetExcelSheet<Lumina.Excel.Sheets.Fate>()!.GetRow(d.Fate);
                Check(cfc.TerritoryType.RowId == d.Territory && territory.ContentFinderCondition.RowId == d.Cfc
                    && fate.RowId == d.Fate && fate.Name.ToString().Length > 0, $"Crescent client metadata {d.Name}.");
                Console.WriteLine($"PASS native Crescent client metadata {d.Name} CFC{d.Cfc} Territory{d.Territory} Fate{d.Fate}: {fate.Name}");
            }

        foreach (var d in definitions)
        {
            id = "masterplayer.fate." + d.Name;
            load(id, [Header(d.Territory),
                World(d.Territory, 0, d.Fate, Initial(d.Oid, d.Name != "crescentfan", Spell(d.Action, 0)), true),
                CancelHoH(.2, 20, d.Action),
                World(d.Territory, 20, d.Fate, [Actor(20, 0x233C, center + Vector3.UnitX * 20, cast: Spell(d.Action, 0))]),
                World(d.Territory, 21, d.Fate, [Actor(20, 0x233C, center)]),
                World(d.Territory, 22, d.Fate, [])]);
            Seek(.1);
            Check(Active() && dangers() == 1 && Shown("crescent/aoe/").Length == 1,
                $"Crescent cold helper failed {d.Name}.");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)d.Action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)d.Action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 999, ActionId = (ushort)d.Action }),
                $"Crescent exact native caster suppression {d.Name}.");
            var boundary = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
            var navigationRadius = d.Name == "adelicatebalance" ? 30f : 40f;
            Check((Vector3)Read(boundary, "Position") == center && (float)Read(boundary, "Radius") == navigationRadius,
                $"Crescent dynamic circle {d.Name}.");
            var hostType = assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
            var bridge = RuntimeHelpers.GetUninitializedObject(hostType);
            Check(!(bool)hostType.GetMethod("IsReplaySuppressed", flags)!.Invoke(bridge, null)!,
                "Crescent suppressed unrelated island modules.");
            Seek(20.1);
            Check(Casts().Length == 1 && dangers() == 1 && (Vector3)Read(Casts()[0], "Origin") == center,
                $"Crescent packet cancel/timeout/locked point {d.Name}.");
            var danger = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Single();
            Check(((IEnumerable)Read(danger, "DangerWindows")).Cast<object>()
                .Any(w => (bool)w.GetType().GetMethod("IsActiveAt")!.Invoke(w, [(object)runtime.CurrentTime])!),
                "Crescent retained model but silently expired navigation before real Finish.");
            Seek(21.1);
            Check(Casts().Length == 0 && dangers() == 0, $"Crescent real raw Finish {d.Name}.");
            Seek(.1);
            Check(dangers() == 1, $"Crescent inverse Seek {d.Name}.");
            var hints = Read(Script(), "Hints");
            Check(!(bool)Read(Read(hints, "_facing"), "_hasRequest"), "Crescent replay sent real facing.");
            var mechanism = (string)Read(Read(Model(), "Definition"), "DisplayName");
            manager.SetMechanismEnabled(id, mechanism, false);
            Seek(.1);
            Check(dangers() == 0 && previews().Length == 0
                && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
                "Crescent mechanism off retained navigation, drawing or boundary.");
            manager.SetMechanismEnabled(id, mechanism, true);
            Seek(.1);
            var foreign = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.Geometry.CircleGeometry")!,
                [center + Vector3.UnitX * 5, 60f])!;
            runtime.Manager.SetArenaBoundary((dynamic)foreign);
            manager.StopScript(id);
            Check(ReferenceEquals((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary, foreign),
                "Crescent owner cleanup removed another owner's replacement boundary.");
            runtime.Manager.ClearArenaBoundary();
            stop();
            Console.WriteLine($"PASS native Crescent {d.Name} cold raw helper locked origin circle packet cancel no timeout real Finish reverse Seek bridge coexist replay isolation owner");
        }

        id = "masterplayer.fate.crescentfan";
        load(id, [Header(1346), World(1346, 0, 2073, Initial(0x4D8C, false), true),
            World(1346, .1, 2073, [Actor(30, 0x4D8E, center, dead: true)]),
            World(1346, .2, 2073, [], removed: [30]),
            World(1346, .3, 2073, [], origin: center + Vector3.UnitX * 100),
            World(1346, .4, 2073, []), World(1346, .5, 0, []),
            World(1346, .6, 2073, []), World(1346, 1, 2073, [])]);
        Seek(.15); Check(Active(), "Dead BigFan treated as disappeared.");
        Seek(.25); Check(!Active(), "BigFan disappearance retained module.");
        Seek(.35); Check(!Active(), "Pot used fallback primary circle outside real Fate.");
        Seek(.45); Check(Active(), "Pot failed fresh reset/pull.");
        Seek(.55); Check(!Active(), "Pot active without current Fate2073.");
        stop();
        Console.WriteLine("PASS native Crescent protected pot no-combat pull dead-present BigFan disappearance exact Fate radius leave reset rebuild owner");

        id = "masterplayer.fate.eyetoeye";
        object[] roster = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        load(id, [Header(1346), World(1346, 0, 2075,
            [.. Initial(0x4BA7), .. Enumerable.Range(1, 7).Select(i => Actor((ulong)(100 + i), 0, center, dead: i == 1)),
                Actor(22, 0x233C, center)], true, party: roster),
            World(1346, .1, 2075, [Actor(20, 0x233C, center, cast: Spell(47147))], party: roster),
            World(1346, 20, 2075, [], party: roster),
            World(1346, 21, 2075, [Actor(20, 0x233C, center, cast: Spell(47148, 21)),
                Actor(22, 0x233C, center, cast: Spell(47152, 21)),
                Actor(100, 0, center + Vector3.UnitX * 40, rotation: -MathF.PI / 2)], party: roster),
            World(1346, 25, 2075, [], party: roster)]);
        Seek(20.1);
        var clockType = Assembly.Load("MasterPlayer.MechanicRuntime").GetType("BossMod.ActorCastInfo")!;
        var clock = Activator.CreateInstance(clockType)!;
        clockType.GetField("TotalTime")!.SetValue(clock, 3f);
        clockType.GetField("ElapsedTime")!.SetValue(clock, 3.5f);
        var expected = ((DateTime)runtime.CurrentTime).AddSeconds((float)clockType.GetProperty("NPCRemainingTime")!.GetValue(clock)!);
        Check(Damage().Select(p => (ulong)Read(p, "TargetId")).SequenceEqual(new ulong[] { 100, 102, 103, 104, 105, 106, 107 })
            && Damage().All(p => (DateTime)Read(p, "Activation") == expected)
            && dangers() == 0 && Shown("crescent/aoe/").Length == 0, "Crescent raidwide clock/identities/diagnostic scope.");
        Seek(24);
        Check(Damage().Length == 0 && Shown("crescent/eye/").Length == 2
            && Read(Script(), "PlannedFacing") is float, "Crescent finite gaze ranges/planned facing.");
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest"), "Crescent gaze executed in replay.");
        stop();
        Console.WriteLine("PASS native Crescent living party raw dynamic source NPC clock diagnostic only finite30 and50 gaze actual planned facing replay isolation owner");

        id = "masterplayer.fate.thewingedterror";
        load(id, [Header(1252), World(1252, 0, 1965, Initial(0x46C1, helperCast: Spell(41273, 0)), true, known: false),
            World(1252, 1, 1965, []), World(1252, 2, 1965, [])]);
        Seek(.1); Check(!Active() && dangers() == 0 && Read(Script(), "MechanicHintsText") is string,
            "Legacy missing input guessed current Fate.");
        Seek(1.1); Check(Active() && dangers() == 1, "Recorded optional current Fate did not enable cold activation.");
        var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
        Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
            && Vector3.Distance((Vector3)Read((object)result, "TargetPosition"), center) > 7,
            "Crescent actual solver failed R7 escape.");
        stop();
        Console.WriteLine("PASS native Crescent legacy unknown diagnostic optional input restore real SafeZoneSolver R7 escape no fallback owner");

        load(id, [Header(1252), World(1252, 0, 1965,
            Initial(0x46C1, helperCast: Spell(41273, 0)), true),
            World(1252, .5, 1965, [Actor(2, 0x46C1, center, combat: true), Actor(3, 0x46C1, center, combat: true)]),
            World(1252, 1, 1965, [], removed: [1]), World(1252, 2, 1965, [])]);
        Seek(.6);
        Check((ulong)Read(Read(Read(Model(), "Primary"), "Actor"), "Id") == 1, "Pending actor stole active primary.");
        Seek(1.1);
        Check(Active() && (ulong)Read(Read(Read(Model(), "Primary"), "Actor"), "Id") == 3 && dangers() == 1,
            "Existing reverse-order pending primary failed cold restoration.");
        stop();
        Console.WriteLine("PASS native Crescent existing same OID reverse pending successor after loaded removal cold helper exact owner");

        var settingsType = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!;
        var settings = settingsType.GetProperty("Instance")!.GetValue(null)!;
        var marginField = settingsType.GetField("DodgeDangerMargin")!;
        var previousMargin = marginField.GetValue(settings);
        try
        {
            marginField.SetValue(settings, 1f);
            load(id, [Header(1252),
                World(1252, 0, 1965, Initial(0x46C1, helperCast: Spell(41273, 0)), true),
                World(1252, 4, 1965, [])]);
            Seek(.1);
            var exactSnapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            var danger = ((IEnumerable)Read(exactSnapshot, "DangerAreas")).Cast<object>().Single();
            Check((float)Read(Read(danger, "Geometry"), "Radius") == 7,
                "Crescent inflated raw R7 geometry before applying the owner solver margin.");
            var optionsType = assembly.GetType(ns + "SolveOptions")!;
            var oneMargin = Activator.CreateInstance(optionsType)!;
            optionsType.GetProperty("HardSafetyMargin")!.SetValue(oneMargin, 1f);
            dynamic marginResult = solver.Solve((dynamic)exactSnapshot, player, capability, (dynamic)oneMargin,
                runtime.CurrentTime, null);
            var distance = Vector3.Distance((Vector3)Read((object)marginResult, "TargetPosition"), center);
            Check((bool)marginResult.IsSuccess && !(bool)Read((object)marginResult, "IsLeastDangerFallback")
                && distance > 8 && distance < 9, $"Crescent one-margin escape differs: {distance}.");
            stop();
        }
        finally { marginField.SetValue(settings, previousMargin); }
        Console.WriteLine("PASS native Crescent raw R7 plus single 1y solver margin exact geometry and bounded actual escape");
    }

    private static void RunCrescentVariants(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var center = new Vector3(100, 10, 100);
        var id = "";
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
        object Model() => Read(Script(), "encounter");
        object[] Casts() => ((IEnumerable)Read(Model(), "Casts")).Cast<object>().ToArray();
        object[] Shown() => previews().Where(p => ((string)Read(p, "Id")).Contains("crescent/aoe/")).ToArray();
        object[] Areas() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().ToArray();
        object[] Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().ToArray();
        void Seek(double t)
        {
            Check((int)controller.RouterErrorCount == 0, "Crescent variant error before Seek.");
            controller.Seek(t);
            Check((int)controller.RouterErrorCount == 0 && (string)manager.ErrorMessage == "",
                "Crescent variant isolated callback error: " + controller.StatusMessage);
        }
        object Spell(uint action, double t, float total = 3, bool happened = false, Vector3? location = null)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, total, location ?? center, .4f, 100, t))!;
            node["IsSpell"] = true; node["TotalTime"] = total; node["EffectSeen"] = happened;
            return node;
        }
        string World(uint territory, double t, uint fate, IEnumerable<object> actors, bool full = false,
            ulong[]? removed = null, Vector3? fateCenter = null)
            => JsonSerializer.Serialize(new { t, kind = "WorldFrame", data = new {
                Full = full, Territory = territory, LocalPlayerId = 100, SourcePlayerId = 100, AllowedFateId = fate,
                ActiveFate = new { Id = fate, Center = Pose(fateCenter ?? center), Radius = 40f },
                Actors = actors.ToArray(), Removed = removed ?? [], Party = new[] { new { Slot = 0, EntityId = 100UL } } } });
        object[] Initial(uint oid, object? cast = null)
            => [Actor(1, oid, center, combat: true, cast: cast), Actor(100, 0, center),
                Actor(20, 0x233C, center), Actor(22, 0x233C, center)];
        string Raw(double t, uint action, ulong source = 20, uint sequence = 0)
            => JsonSerializer.Serialize(new { t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = action, MainTargetId = 100UL, ActionType = 1,
                    GlobalSequence = sequence, Targets = new[] { 100UL } } });
        DateTime Activation(object cast) => (DateTime)Read(cast, "Activation");
        void Isolation()
        {
            var hints = Read(Script(), "Hints");
            Check(!(bool)Read(Read(hints, "_facing"), "_hasRequest"), "Crescent variant executed replay facing.");
            Check((ulong)Read(hints, "target") == 0 && (ulong)Read(hints, "clearTarget") == 0,
                "Crescent variant executed replay target.");
        }

        id = "masterplayer.fate.allureoftheoccult";
        load(id, [Header(1346), World(1346, 0, 2078, Initial(0x4D56), true),
            World(1346, .1, 2078, [Actor(20, 0x233C, center, cast: Spell(48945, .1)),
                Actor(22, 0x4D57, center, cast: Spell(48950, .1, 5))]),
            World(1346, .2, 2078, [Actor(20, 0x233C, center)]),
            World(1346, .3, 2078, [Actor(22, 0x4D57, center)]),
            World(1346, 1, 2078, [])]);
        Seek(.15);
        Check(Shown().Length == 2 && dangers() == 1 && Casts().Any(c => !(bool)Read(c, "Risky")),
            "Burst non-risky circle hidden or navigated.");
        Check(Shown().Any(p => Read(p, "Color") is Vector4 color
                && color == new Vector4(128f / 255, 128f / 255, 0, 128f / 255)),
            "Burst explicit AOE color merged with default theme.");
        Seek(.25);
        Check(Shown().Length == 1 && dangers() == 1 && (float)Read(Read(Areas()[0], "Geometry"), "Radius") == 10,
            "Burst risk recovery failed to restore actual circle navigation.");
        Seek(.35); Check(dangers() == 0 && Shown().Length == 0, "Burst actual Finish leaked.");
        Seek(.15); Isolation(); stop();
        Console.WriteLine("PASS native Crescent variants Burst actual cross context visible non-risky circle risk restore navigation inverse Seek owner");

        id = "masterplayer.fate.galeforceencounter";
        load(id, [Header(1346), World(1346, 0, 2082, Initial(0x4BEC), true),
            World(1346, .1, 2082, [Actor(1, 0x4BEC, center, combat: true, cast: Spell(47598, .1))]),
            Raw(.2, 47598, 1), World(1346, .3, 2082, [Actor(1, 0x4BEC, center, combat: true)]),
            Raw(.4, 47584, sequence: 17), Raw(.5, 47584, sequence: 17),
            Raw(.6, 47584), Raw(.7, 47584), World(1346, 1, 2082, [])]);
        Seek(.15);
        var first = Activation(Casts()[0]);
        Check(Casts().Length == 3 && Shown().Length == 1 && dangers() == 0
            && first == Start.AddSeconds(.1).AddSeconds((3f + .3f) + .23d),
            "Freefall precise first clock/non-risky navigation failed.");
        Check(Shown().All(p => Read(p, "Color") == null), "Freefall default color acquired explicit AOE color.");
        Seek(.35); Check(Casts().Length == 3, "Freefall normal Finish consumed queue.");
        Seek(.45); Check(Casts().Length == 2 && Activation(Casts()[0]) > first, "Freefall first actual Effect.");
        Seek(.55); Check(Casts().Length == 2, "Freefall nonzero duplicate consumed twice.");
        Seek(.65); Check(Casts().Length == 1 && dangers() == 0 && Shown().Length == 1, "Freefall first zero package.");
        Seek(.75); Check(Casts().Length == 0 && Shown().Length == 0, "Freefall second zero package.");
        Seek(.15); Isolation(); stop();
        Console.WriteLine("PASS native Crescent variants Freefall exact float clock three model first visible zero navigation native sequence dedup two zero packages replay owner");

        load(id, [Header(1346), World(1346, 0, 2082,
            [Actor(1, 0x4BEC, center, combat: false, cast: Spell(47598, 0)), Actor(100, 0, center)], true),
            World(1346, .1, 2082, [Actor(1, 0x4BEC, center, combat: false, cast: Spell(47598, 0, happened: true))]),
            World(1346, .2, 2082, [Actor(1, 0x4BEC, center, combat: true, cast: Spell(47598, 0, happened: true))]),
            World(1346, .3, 2082, [Actor(1, 0x4BEC, center, combat: true)]), World(1346, 1, 2082, [])]);
        Seek(.25);
        Check((bool)Read(Script(), "EncounterActive") && Casts().Length == 0 && dangers() == 0,
            "Freefall later response snapshot manufactured cold queue.");
        Seek(.35); Check(Casts().Length == 0, "Freefall cold happened Finish manufactured queue.");
        stop();
        Console.WriteLine("PASS native Crescent variants Freefall later same-cast response snapshot before cold activation no synthetic queue owner");

        foreach (var d in new[] {
            (Name: "anunendingduty", Territory: 1252u, Fate: 1966u, Oid: 0x4735u, Actions: new uint[] { 41984, 41985, 41986 }),
            (Name: "galeforceencounter", Territory: 1346u, Fate: 2082u, Oid: 0x4BECu, Actions: new uint[] { 47587, 47586, 47585 }),
            (Name: "galeforceencounter", Territory: 1346u, Fate: 2082u, Oid: 0x4BECu, Actions: new uint[] { 47594, 47593, 47592 }),
            (Name: "galeforceencounter", Territory: 1346u, Fate: 2082u, Oid: 0x4BECu, Actions: new uint[] { 47597, 47596, 47595 }),
        })
        {
            id = "masterplayer.fate." + d.Name;
            load(id, [Header(d.Territory), World(d.Territory, 0, d.Fate, Initial(d.Oid), true),
                World(d.Territory, .1, d.Fate, [Actor(20, 0x233C, center, cast: Spell(d.Actions[0], .1))]),
                World(d.Territory, .2, d.Fate, [Actor(20, 0x233C, center)]),
                World(d.Territory, .3, d.Fate, [Actor(22, 0x233C, center, cast: Spell(d.Actions[1], .3))]),
                World(d.Territory, .4, d.Fate, [Actor(22, 0x233C, center)]),
                World(d.Territory, .5, d.Fate, [Actor(22, 0x233C, center, cast: Spell(d.Actions[2], .5))]),
                World(d.Territory, .6, d.Fate, [Actor(22, 0x233C, center)]), World(d.Territory, 1, d.Fate, [])]);
            Seek(.15);
            Check(dangers() == 1 && Shown().Length == 1
                && (float)Read(Read(Areas()[0], "Geometry"), "Radius") == 10, "Concentric initial actual circle.");
            object[] Handles() => ((IEnumerable)Read(Script(), "areas")).Cast<object>()
                .Select(entry => Read(Read(entry, "Value"), "Item2")).ToArray();
            var handles = Handles();
            for (var i = 0; i < 3; ++i)
                Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [0f]);
            Check(handles.SequenceEqual(Handles()),
                "Concentric unchanged output recreated navigation.");
            Seek(.25);
            Check((float)Read(Read(Areas()[0], "Geometry"), "InnerRadius") == 10
                && Activation(Casts().Single()) == Start.AddSeconds(.2).AddSeconds(2d), "Concentric Finish next clock.");
            Seek(.45);
            Check((float)Read(Read(Areas()[0], "Geometry"), "InnerRadius") == 20
                && Activation(Casts().Single()) == Start.AddSeconds(.4).AddSeconds(2d), "Concentric cross-source next Finish.");
            Seek(.65); Check(dangers() == 0 && Shown().Length == 0, "Concentric final Finish retained navigation.");
            Seek(.25); Check(dangers() == 1, "Concentric reverse Seek failed.");
            Isolation(); stop();
            Console.WriteLine($"PASS native Crescent variants Concentric {d.Name} {d.Actions[0]} three true Finish cross-source exact plus2 clock stable navigation inverse Seek owner");
        }

        id = "masterplayer.fate.relicicewolf";
        load(id, [Header(1346), World(1346, 0, 2080, Initial(0x4D5E), true),
            World(1346, .1, 2080, [Actor(1, 0x4D5E, center, combat: true, cast: Spell(49756, .1))]),
            Raw(.2, 49756, 1), World(1346, .3, 2080, [Actor(1, 0x4D5E, center, combat: true)]),
            Raw(.4, 49767), Raw(.5, 49766),
            World(1346, .6, 2080, [Actor(1, 0x4D5E, center, combat: true, cast: Spell(49757, .6))]),
            World(1346, .7, 2080, [Actor(1, 0x4D5E, center, combat: true)]),
            World(1346, 1, 2080, [])]);
        Seek(.35);
        Check(dangers() == 1 && Casts().Length == 1 && Activation(Casts()[0])
            == Start.AddSeconds(.1).AddSeconds((3f + .3f) + 1d), "Storm real normal Finish/precise delayed clock.");
        Seek(.45); Check(dangers() == 1, "Wrong Storm helper cleared prediction.");
        Seek(.55); Check(dangers() == 0, "Matching Storm helper did not clear.");
        Seek(.65); Check(dangers() == 1 && (float)Read(Read(Areas()[0], "Geometry"), "InnerRadius") == 10,
            "Storm donut new Start missing.");
        Seek(.75); Check(dangers() == 0, "Canceled Storm without effect retained danger.");
        stop();
        Console.WriteLine("PASS native Crescent variants Storm complete native EffectHappened normal Finish wrong correct helper cancel delayed circle donut owner");
        load(id, [Header(1346), World(1346, 0, 2080, Initial(0x4D5E), true),
            World(1346, .1, 2080, [Actor(1, 0x4D5E, center, combat: true, cast: Spell(49756, .1))]),
            World(1346, .2, 2080, [Actor(1, 0x4D5E, center, combat: true, cast: Spell(49756, .1, happened: true))]),
            World(1346, .3, 2080, [Actor(1, 0x4D5E, center, combat: true)]), Raw(.4, 49766),
            World(1346, 1, 2080, [])]);
        Seek(.35); Check(dangers() == 1, "Storm same-cast response snapshot lost normal Finish.");
        Seek(.45); Check(dangers() == 0, "Storm snapshot normal Finish blocked helper resolution.");
        stop();
        Console.WriteLine("PASS native Crescent variants Storm later same-cast response snapshot preserves real normal Finish independent helper resolution owner");

        id = "masterplayer.fate.thunderregnum";
        var puddles = new uint[] { 49502, 49919, 49920, 49921, 49922, 49923, 49924, 49925, 49926 };
        load(id, [Header(1346), World(1346, 0, 2084, Initial(0x4D63), true),
            World(1346, .1, 2084, puddles.Select((action, i) => Actor((ulong)(30 + i), 0x4D65,
                center + Vector3.UnitX * i, cast: Spell(action, .1, 5 + i * .5f)))),
            Raw(.2, 49502, 30, 77), Raw(.3, 49919, 31, 77), Raw(.4, 49919, 31),
            World(1346, .5, 2084, [Actor(32, 0x4D65, center + Vector3.UnitX * 2)]),
            Raw(.6, 49920, 32), World(1346, 1, 2084, [])]);
        Seek(.15);
        Check(Casts().Length == 9 && Shown().Length == 9 && dangers() == 1
            && Activation(Casts().Single(c => (uint)Read(c, "Action") == 49502))
                == Start.AddSeconds(.1).AddSeconds((5f + .3f) - 1f), "Puddle early clock/full visible route/first risk.");
        Seek(.25); Check(Casts().Length == 8 && dangers() == 1, "Puddle first source actual effect.");
        Seek(.35); Check(Casts().Length == 8, "Puddle component-wide nonzero sequence failed.");
        Seek(.45); Check(Casts().Length == 7, "Puddle zero packet ignored.");
        Seek(.55); Check(Casts().Length == 7, "Puddle ordinary Finish cleared persistent prediction.");
        Seek(.65); Check(Casts().Length == 6 && dangers() == 1 && Shown().Length == 6, "Puddle effect tail promotion.");
        stop();
        Console.WriteLine("PASS native Crescent variants nine puddles precise early clock all visible first-risk actual Effect source sequence zero ordinary Finish promotion owner");

        id = "masterplayer.fate.relicicewolf";
        load(id, [Header(1346), World(1346, 0, 2080, Initial(0x4D5E), true),
            World(1346, .1, 2080, [Actor(20, 0x4DA0, center, cast: Spell(49761, .1)),
                Actor(22, 0x4DA0, center, cast: Spell(49761, .1, 4))]),
            World(1346, .2, 2080, [Actor(20, 0x4DA0, center)]), World(1346, 1, 2080, [])]);
        Seek(.15); Check(Shown().Length == 2 && dangers() == 1, "Frost future wave became simultaneous full danger.");
        Check(Shown().Count(p => Read(p, "Color") is Vector4 color && color == new Vector4(1, 1, 0, 1)) == 1,
            "Frost source Danger ABGR yellow was not preserved in actual preview.");
        Seek(.25); Check(Shown().Length == 1 && dangers() == 1, "Frost hidden tail failed risk promotion.");
        stop();
        Console.WriteLine("PASS native Crescent variants Frost full visible two wave first risky sorted danger color tail promotion navigation owner");

        id = "masterplayer.fate.kingofthecrescent";
        load(id, [Header(1252), World(1252, 0, 1964,
            [.. Initial(0x46D7), Actor(30, 0x46D8, center + new Vector3(.123f, 0, .456f))], true),
            World(1252, 20, 1964, [], removed: [30]),
            World(1252, 21, 1964, [Actor(30, 0x46D8, center, cast: Spell(41386, 21))]),
            World(1252, 22, 1964, [Actor(30, 0x46D8, center)]), World(1252, 23, 1964, [])]);
        Seek(.1); Check(Casts().Length == 1 && dangers() == 1 && Activation(Casts()[0]) == Start.AddSeconds(9.8d),
            "Cold Airburst creation clock.");
        Seek(20.1); Check(Casts().Length == 1 && dangers() == 1, "Airburst destroyed actor/timeout auto-deleted.");
        Seek(21.1); Check(Casts().Length == 2 && dangers() == 2, "Airburst new same-ID actor lost original generation.");
        Seek(22.1); Check(Casts().Length == 1 && dangers() == 1, "Airburst Finish removed every same-ID generation.");
        stop();
        Console.WriteLine("PASS native Crescent variants Airburst cold Created precise nine point eight clock destroyed reference new same ID first-only Finish owner");

        id = "masterplayer.fate.madmudarch";
        object Growth(ushort param, double expires, int slot = 59, byte kind = 5)
        {
            var node = JsonSerializer.SerializeToNode(Actor(20, 0x46BE, center, subKind: kind))!;
            node["Statuses"] = JsonSerializer.SerializeToNode(new[] { new {
                Id = 4221, Param = param, SourceId = 100UL, SlotIndex = slot, ExpiresAt = Start.AddSeconds(expires) } });
            return node;
        }
        load(id, [Header(1252), World(1252, 0, 1969, [.. Initial(0x46BD).Where(a =>
            JsonSerializer.SerializeToNode(a)!["Id"]!.GetValue<ulong>() != 20), Growth(2, 30)], true),
            StatusEvent(.1, 20, 4221, 2, true), World(1252, .2, 1969, [Growth(2, 33)]),
            World(1252, .3, 1969, [Actor(20, 0x46BE, center)]), World(1252, 1, 1969, [])]);
        Seek(.15); Check(Casts().Length == 1 && dangers() == 1, "Growth event duplicated cold real slot59 Gain.");
        Seek(.25); Check(Casts().Length == 2 && dangers() == 2, "Growth raw refresh did not append source duplicate.");
        Seek(.35); Check(Casts().Length == 2 && dangers() == 2, "Growth Lose cleared predicted list.");
        stop();
        Console.WriteLine("PASS native Crescent variants Rupture real slot59 cold Gain raw expiry refresh duplicate no event double-feed Lose retained owner");

        id = "masterplayer.fate.apryingeye";
        load(id, [Header(1252), World(1252, 0, 1970,
            [.. Initial(0x47DC), Actor(30, 0x47DD, center)], true),
            World(1252, .2, 1970, [Actor(30, 0x47DD, center + Vector3.UnitX * 3, rotation: 1)]),
            World(1252, .3, 1970, [Actor(30, 0x47DD, center, dead: true)]), World(1252, 1, 1970, [])]);
        Seek(.1); Check(Shown().Length == 1 && dangers() == 2, "Observer display and dual navigation cones missing.");
        var shapes = Areas().Select(a => Read(a, "Geometry")).ToArray();
        Check(shapes.Select(s => (float)Read(s, "Radius")).Order().SequenceEqual(new[] { 6f, 9f }),
            "Observer extra R6 cone replaced source R9 cone.");
        var assembly = runtimeObject.GetType().Assembly;
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        dynamic pc = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        dynamic solution = solver.Solve((dynamic)snapshot, pc, capability, options, runtime.CurrentTime, null);
        var target = (Vector3)Read((object)solution, "TargetPosition");
        Check((bool)solution.IsSuccess && !(bool)Read((object)solution, "IsLeastDangerFallback")
            && shapes.All(s => !(bool)s.GetType().GetMethod("Contains")!.Invoke(s, [target, 1e-5f])!),
            "Observer actual solver did not escape both navigation cones.");
        Seek(.25); Check(Areas().All(a => (Vector3)Read(Read(a, "Geometry"), "Position") == center + Vector3.UnitX * 3),
            "Observer live movement did not update actual navigation.");
        Seek(.35); Check(dangers() == 0 && Shown().Length == 0, "Dead observer retained live navigation.");
        stop();
        Console.WriteLine("PASS native Crescent variants dynamic Observer real position rotation R9 delayed and R6 immediate two constraints death owner");

        id = "masterplayer.fate.servingdarkness";
        load(id, [Header(1252), World(1252, 0, 1972, Initial(0x4772), true),
            World(1252, .1, 1972, [Actor(1, 0x4772, center, combat: true,
                cast: Spell(42178, .1, location: center + Vector3.UnitX * 15))]),
            World(1252, .2, 1972, [Actor(1, 0x4772, center, combat: true,
                cast: Spell(42179, .2, location: center + Vector3.UnitX * 10))]),
            World(1252, .3, 1972, [Actor(1, 0x4772, center, combat: true)]),
            World(1252, .4, 1972, [Actor(20, 0x4773, center, cast: Spell(42180, .4))]),
            World(1252, .5, 1972, [Actor(20, 0x4773, center)]), World(1252, 1, 1972, [])]);
        Seek(.15);
        Check(dangers() == 1 && Goals().Length == 0 && Read(Script(), "PlannedChargeGoal") is Vector3
            && ((string)Read(Script(), "MechanicHintsText")).Contains("Follow the charge!"),
            "Sweeping charge geometry/weak intent/global text consumption.");
        Seek(.25); Check(dangers() == 2 && Read(Script(), "PlannedChargeGoal") == null, "Menacing charge dual geometry.");
        Seek(.35); Check(dangers() == 1, "Own charge Finish removed the delayed Menace circle.");
        Seek(.55); Check(dangers() == 0, "Different helper42180 Finish did not clear Menace circle.");
        stop();
        Console.WriteLine("PASS native Crescent variants locked charge geometry weak R5 weight5 intent no new automatic goal global text distinct Menace Finish owner");

        id = "masterplayer.fate.adelicatebalance";
        load(id, [Header(1252), World(1252, 0, 1968,
            [Actor(1, 0x46B8, center, combat: true, objectIndex: 2, isEnemy: true),
                Actor(100, 0, center + Vector3.UnitX * 5), Actor(30, 0x4770, center)], true),
            World(1252, .2, 1968, [Actor(100, 0, center + Vector3.UnitX * 8)]), World(1252, 1, 1968, [])]);
        Seek(.1);
        var boundary = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check((Vector3)Read(boundary, "Position") == center + Vector3.UnitX * 5 && (float)Read(boundary, "Radius") == 30
            && (ulong)Read(Script(), "PlannedTargetId") == 1
            && previews().Any(p => ((string)Read(p, "Id")).Contains("crescent/enemy/30")),
            "Delicate override navigation/target/extra enemy marker.");
        var arena = previews().Single(p => ((string)Read(p, "Id")).Contains("crescent/arena"));
        Check((float)Read(Read(arena, "Geometry"), "Radius") == 40, "Delicate real Fate display acquired nav R30.");
        Seek(.25);
        boundary = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check((Vector3)Read(boundary, "Position") == center + Vector3.UnitX * 8, "Delicate default player center did not update.");
        Isolation(); stop();
        Console.WriteLine("PASS native Crescent variants Delicate real Fate R40 display versus player R30 navigation extra enemy target priority replay isolation owner");
    }
}
