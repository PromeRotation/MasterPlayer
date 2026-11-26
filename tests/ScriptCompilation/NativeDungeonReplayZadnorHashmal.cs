using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunZadnorHashmal(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const string mechanism = "复制统治者——第四军团哈修马利姆";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(330, 0, 390);
        object[] roster = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
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
        object[] Items(string name) => ((IEnumerable)Read(Model(), name)).Cast<object>().ToArray();
        object[] Shown(string key) => previews().Where(p => ((string)Read(p, "Id")).Contains(key)).ToArray();
        string World(double t, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(975, t, actors, full, removed, roster, sourcePlayerId: 100);
        object Spell(uint action, double at, Vector3? origin = null, ulong target = 100, float rotation = 0)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 4, origin ?? center, rotation, target, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 4;
            return node;
        }
        object Helper(object? cast = null, Vector3? p = null)
            => Actor(20, 0x233C, p ?? center, cast: cast, subKind: 5);
        object[] Initial(object? cast = null) =>
            [Actor(1, 0x2DD2, center, combat: true, isEnemy: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 18), Helper(cast)];
        string Effect(double t, uint action, uint sequence, Vector3? position = null, ulong target = 100)
            => JsonSerializer.Serialize(new
            {
                t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = 1UL, ActionId = action, ActionType = 1, GlobalSequence = sequence,
                    MainTargetId = target, Targets = Array.Empty<ulong>(),
                    TargetPosition = position.HasValue ? Pose(position.Value) : null, Timestamp = Start.AddSeconds(t) },
            });
        void Seek(double time)
        {
            controller.Seek(time);
            Check((int)controller.RouterErrorCount == 0 && (string)manager.ErrorMessage == "",
                "CE44 actual replay callback: " + controller.StatusMessage + "; " + manager.ErrorMessage);
        }
        bool Contains(object shape, Vector3 p)
            => (bool)shape.GetType().GetMethod("Contains")!.Invoke(shape, [p, 0f])!;
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!
            .GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [975u, 0x2DD2u])! && !(bool)owns.Invoke(null, [920u, 0x2DD2u])!
            && !(bool)owns.Invoke(null, [975u, 0x233Cu])! && !(bool)owns.Invoke(null, [975u, 0x31B0u])!,
            "CE44 exact native ownership retains unrelated bridge modules");
        foreach (var (action, name) in new[] { (23826u, "CircleGeometry"), (23818u, "RayRectangleGeometry"),
                     (23821u, "RayRectangleGeometry"), (23822u, "RayRectangleGeometry") })
        {
            load(id, [Header(975), World(0, Initial(Spell(action, 0)), true), World(10, []),
                World(11, [Helper()]), World(12, [])]);
            Seek(10.1);
            var shape = Read(Shown("bozja/aoe/").Single(), "Geometry");
            Check(shape.GetType().Name == name && dangers() == 1, "CE44 actual standard " + action);
            Check(action == 23826 ? (float)Read(shape, "Radius") == 6
                : (float)Read(shape, "Width") == (action == 23818 ? 10 : 36)
                    && (float)Read(shape, "Length") == (action == 23818 ? 40 : 60),
                "CE44 actual dimensions " + action);
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "CE44 native source-specific suppression");
            Seek(11.1); Check(dangers() == 0, "CE44 real Finish consumes");
            Seek(.1);
            if (action == 23826)
            {
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!,
                    [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                    && Vector3.Distance((Vector3)Read((object)result, "TargetPosition"), center) >= 6,
                    "CE44 actual solver escapes R6 within polygon");
            }
            stop();
            Console.WriteLine($"PASS native CE44 {action} actual DLL locked shape raw Finish no TTL source gate reverse Seek owner");
        }

        load(id, [Header(975), World(0, Initial(Spell(23827, 0)), true),
            World(1, [Actor(100, 0, center, dead: true), Helper()]),
            World(2, [], removed: [100]), World(3, [Actor(100, 0, center + Vector3.UnitZ * 15)]),
            Effect(4, 23827, 71, target: 100), World(5, [])]);
        Seek(3.1);
        var damage = ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Single();
        Check((ulong)Read(damage, "Mask") == 1 && Items("DelayedHits").Length == 1 && dangers() == 0,
            "CE44 delayable target death destroyed new ID retains old slot and no geometry");
        Seek(4.1);
        Check(Items("DelayedHits").Length == 0 && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(),
            "CE44 cross source real MainTarget consumes");
        stop();
        Console.WriteLine("PASS native CE44 RockCutter old target slot death destruction generation Finish retains actual Effect consumes diagnostics only");

        load(id, [Header(975), World(0, Initial(Spell(23828, 0)), true), World(1, [Helper()]),
            Effect(2, 23832, 72), Effect(3, 23829, 73), Effect(4, 23829, 74), World(5, [])]);
        Seek(2.1);
        damage = ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Single();
        Check((ulong)Read(damage, "Mask") == 3 && dangers() == 0
            && (DateTime)Read(damage, "Activation") == Start.AddSeconds(4 + .3f + (double).8f),
            "CE44 visual raw float delay exact clock only real Quake");
        Seek(3.1); Check(!((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(),
            "CE44 first AOE clears all");
        stop();
        Console.WriteLine("PASS native CE44 AncientQuake float delay fixed clock alive party raw Finish and unrelated Sanction retain first Effect clears");

        load(id, [Header(975), World(0, [.. Initial(Spell(23827, 0)),
                Actor(21, 0x233C, center, cast: Spell(23828, -3), subKind: 5)], true), World(1, [])]);
        Seek(.1);
        var combined = ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().ToArray();
        Check(combined.Length == 2 && (string)Read(combined[0], "Type") == "Raidwide"
            && (DateTime)Read(combined[0], "Activation") < (DateTime)Read(combined[1], "Activation"),
            "CE44 actual host sorts earlier Quake ahead of catalog Rock");
        stop();
        Console.WriteLine("PASS native CE44 concurrent delayed damage real DLL old Normalize order earlier Quake before Rock");

        load(id, [Header(975), World(0, Initial(Spell(23823, 0)), true),
            World(1, [Actor(100, 0, center, dead: true)]), World(2, [Helper()]), World(3, [])]);
        Seek(.1);
        Check(Shown("bozja/spread/").Length == 1 && dangers() == 1
            && (float)Read(Read(Shown("bozja/spread/").Single(), "Geometry"), "Radius") == 6
            && ((string)Read(Script(), "MechanicHintsText")).Contains("Spread!")
            && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "CE44 ordinary spread");
        Seek(1.1); Check(Shown("bozja/spread/").Length == 0 && Items("Casts").Length == 1,
            "CE44 dead hides raw remains");
        Seek(2.1); Check(Items("Casts").Length == 0, "CE44 Finish targetID clears");
        stop();
        Console.WriteLine("PASS native CE44 Roxxor own R6 outline ordinary hints separate R7 navigation dead raw targetID Finish no damage");

        var tower = center + Vector3.UnitX * 8;
        load(id, [Header(975), World(0, Initial(Spell(23830, 0, tower)), true),
            World(1, [Helper(null, tower)]),
            World(2, [Helper(Spell(23818, 2, tower), tower)]),
            World(3, [Helper(null, tower)]), World(4, [])]);
        Seek(.1); Check(dangers() == 1, "CE44 pending tower R6");
        Seek(1.1);
        var modelArena = Read(Model(), "Arena");
        var hostArena = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(Items("ControlTowers").Length == 1 && !Contains(modelArena, tower)
            && Contains((object)hostArena, tower) && dangers() == 0
            && Shown("bozja/arena").Length == 1, "CE44 full model hole separate real host exterior");
        Seek(3.1);
        Check(Items("ControlTowers").Length == 0 && dangers() == 0, "CE44 actual Towerfall Finish");
        Seek(1.1);
        manager.SetMechanismEnabled(id, mechanism, false); Seek(1.1);
        Check(previews().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "CE44 mechanism owner release");
        manager.SetMechanismEnabled(id, mechanism, true); Seek(1.1); stop();
        Console.WriteLine("PASS native CE44 tower true Finish Loc versus actor hole no obstacle old Nav exterior restore owner mechanism Seek");

        var edge = center + Vector3.UnitX * 28;
        load(id, [Header(975), World(0, Initial(Spell(23830, 0, edge)), true),
            World(1, [Helper(null, edge)]), World(2, [])]);
        Seek(1.1);
        Check(!Contains((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary, edge),
            "CE44 real host boundary keeps exterior notch");
        stop();
        Console.WriteLine("PASS native CE44 edge tower notch real host boundary excludes notch without inventing hole obstacle");

        load(id, [Header(975), World(0, Initial(Spell(23819, 0)), true),
            Effect(1, 23819, 80), Effect(2, 23820, 81, center + Vector3.UnitZ * 8),
            Effect(3, 23820, 82), World(10, [])]);
        Seek(.1);
        Check(dangers() == 4 && Shown("bozja/aoe/").Length == 4, "CE44 four exaflare clocks");
        var initialClock = (DateTime)Read(Read(Items("Exaflares").Single(), "Cast"), "Activation");
        Seek(2.1);
        Check((int)Read(Items("Exaflares").Single(), "Left") == 6 && dangers() == 4,
            "CE44 actual first actor origin and next area position effects");
        Seek(3.1);
        Check((int)Read(Items("Exaflares").Single(), "Left") == 6
            && (int)Read(Model(), "MissingExaflarePosition") == 1, "CE44 missing target coordinate diagnoses without guessing");
        Seek(10.1); Check(dangers() == 4, "CE44 no exaflare TTL");
        Seek(.1);
        Check((DateTime)Read(Read(Items("Exaflares").Single(), "Cast"), "Activation") == initialClock,
            "CE44 Seek reconstruct clocks");
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest")
            && (ulong)Read(Read(Script(), "Hints"), "target") == 0
            && !(bool)Read(Read(Script(), "Hints"), "holdActions"), "CE44 replay no execution");
        stop();
        Console.WriteLine("PASS native CE44 cached exaflare four outputs raw NativeEffect actor and area clocks missing coordinate diagnostic no TTL reverse Seek replay isolation");

        load(id, [Header(975), World(0, [.. Initial(),
                Actor(30, 0x1EB17F, center + Vector3.UnitZ * 25),
                Actor(31, 0x1EB17F, center - Vector3.UnitZ * 25),
                Actor(32, 0x1EB17F, center + Vector3.UnitX * 25)], true),
            World(1, [], removed: [30]), World(2, [Helper(Spell(23825, 2))]),
            World(3, [Helper()]), World(4, [])]);
        Seek(1.1); Check(Items("Hammers").Length == 3 && dangers() == 2 && Shown("bozja/aoe/").Length == 2,
            "CE44 raw three hammers publish two after destroy");
        Seek(3.1); Check(Items("Hammers").Length == 2 && dangers() == 2, "CE44 arbitrary watched Finish pops FIFO");
        stop();
        Console.WriteLine("PASS native CE44 Hammer Created predicts raw3 shown2 all risky destroyed no cast retained arbitrary actual Finish FIFO owner");
    }
}
