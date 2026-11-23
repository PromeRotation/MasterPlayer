using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DungeonPendingCast> anamnesisBusters = [];
    private readonly Dictionary<ulong, uint> anamnesisAnimations = [];
    private readonly HashSet<(ulong Source, uint State, DateTime Time)> anamnesisAnimationEvents = [];
    private readonly Dictionary<ulong, DungeonActor> anamnesisLineTargets = [];
    private DungeonAmbientAoe? anamnesisReflection;
    private readonly List<(uint Resolve, ulong Source, ulong Target, DateTime Activation)> anamnesisLines = [];
    private readonly List<DungeonAmbientAoe> anamnesisTiles = [];
    private readonly List<DungeonAmbientAoe> anamnesisWaves = [];
    private readonly HashSet<int> anamnesisActiveDrains = [];
    private readonly HashSet<int> anamnesisSolvedDrains = [];
    private readonly HashSet<ulong> anamnesisHarpooners = [];
    private DateTime? anamnesisDrainActivation;
    private int anamnesisArenaMode;
    private long anamnesisSerial;
    private bool IsAnamnesisPair => territory == AnamnesisCatalog.TERRITORY && Active?.BossOid == 0x2CD9;
    private static bool AnamnesisPairAlive(DungeonActor a) => !a.Dead && a.Oid is 0x2CD9 or 0x2CDA;

    private void AnamnesisSelectPrimary()
    {
        if (!IsAnamnesisPair) return;
        if (!_actors.Values.Any(AnamnesisPairAlive)) { ResetFight(); return; }
        if (!_actors.TryGetValue(PrimaryId, out var p) || !AnamnesisPairAlive(p))
            PrimaryId = _actors.Values.First(AnamnesisPairAlive).Id;
    }

    private void SynchronizeAnamnesis()
    {
        if (territory != AnamnesisCatalog.TERRITORY) return;
        if (anamnesisHarpooners.Any(id => !_actors.TryGetValue(id, out var a) || a.Dead))
            anamnesisDrainActivation = null;
        anamnesisHarpooners.RemoveWhere(id => !_actors.TryGetValue(id, out var a) || a.Dead);
        foreach (var a in _actors.Values.Where(a => a.Oid == 0x2D01 && !a.Dead)) anamnesisHarpooners.Add(a.Id);
        anamnesisLines.RemoveAll(l => !_actors.TryGetValue(l.Source, out var s) || s.Dead);
        foreach (var line in anamnesisLines)
            if (_actors.TryGetValue(line.Target, out var target)) anamnesisLineTargets[target.Id] = target;
    }

    private void AnamnesisCast(DungeonPendingCast cast)
    {
        if (territory != AnamnesisCatalog.TERRITORY || cast.Rule.Action is not (19305 or 19314 or 19340)) return;
        anamnesisBusters[cast.Source] = cast with
        {
            TargetId = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId,
        };
    }

    private void AnamnesisFinish(DungeonPendingCast cast, bool completed)
    {
        if (territory == AnamnesisCatalog.TERRITORY && !completed) anamnesisBusters.Remove(cast.Source);
    }

    private void AnamnesisAnimation(ulong source, uint state, DateTime time, bool actualEvent)
    {
        if (!IsAnamnesisPair || !_actors.TryGetValue(source, out var actor)) return;
        if (actualEvent ? !anamnesisAnimationEvents.Add((source, state, time))
            : anamnesisAnimations.GetValueOrDefault(source, uint.MaxValue) == state) return;
        anamnesisAnimations[source] = state;
        var degrees = state switch { 0x00041000 => -135.005f, 0x00042000 => 134.999f, 0x00044000 => -.003f, _ => float.NaN };
        if (float.IsFinite(degrees))
            anamnesisReflection = new("anamnesis/reflection", "时钟反射", new FanTemplate(40, MathF.PI / 4),
                QuantizedDungeonPosition(actor.Position), degrees * MathF.PI / 180, Activation: time.AddSeconds(14.4));
    }

    private void AnamnesisMapEffect(uint index, uint state, DateTime time)
    {
        if (territory != AnamnesisCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x2CFE && index is 0x0D or 0x0E && anamnesisTiles.Count == 0)
        {
            Vector2[] x = [new(10, -70), new(30, -70), new(20, -80), new(10, -90), new(30, -90)];
            Vector2[] cross = [new(20, -70), new(10, -80), new(30, -80), new(20, -90)];
            if (state is 0x08000400 or 0x02000100) { Add(x, 16); Add(cross, 18.2); }
            else if (state is 0x20001000 or 0x00800040) { Add(cross, 16); Add(x, 18.2); }
            void Add(Vector2[] positions, double delay)
            {
                foreach (var p in positions)
                    anamnesisTiles.Add(new($"anamnesis/tile/{++anamnesisSerial}", "锤刃九宫格", new RectangleTemplate(10, 10),
                        new(p.X, Arena?.Position.Y ?? 0, p.Y), -.003f * MathF.PI / 180, Activation: time.AddSeconds(delay)));
            }
        }
        if (Active?.BossOid != 0x2CFF) return;
        if (index is 0x17 or 0x18)
        {
            var mode = -1;
            if (state == 0x00020001) mode = index == 0x17 ? 1 : 2;
            else if (state == 0x00080004) mode = 0;
            if (mode >= 0 && mode != anamnesisArenaMode)
            {
                anamnesisArenaMode = mode;
                var y = Arena?.Position.Y ?? 0;
                Arena = mode switch { 1 => AnamnesisCatalog.Narrow(y), 2 => AnamnesisArenas.Split(y), _ => AnamnesisCatalog.Default(y) };
            }
        }
        if (index is < 0x0F or > 0x16) return;
        var drain = (int)index - 0x0F;
        switch (state)
        {
            case 0x00020001:
                anamnesisActiveDrains.Add(drain); anamnesisSolvedDrains.Remove(drain);
                anamnesisDrainActivation ??= time.AddSeconds(12);
                break;
            case 0x00080004:
                anamnesisSolvedDrains.Add(drain); anamnesisActiveDrains.Remove(drain);
                break;
            case 0x00200004:
                anamnesisActiveDrains.Clear(); anamnesisSolvedDrains.Clear(); anamnesisDrainActivation = null;
                break;
        }
    }

    private void AnamnesisEffect(ulong source, uint action, ulong target, DateTime time)
    {
        if (territory != AnamnesisCatalog.TERRITORY) return;
        if (action is 19305 or 19314 or 19340) anamnesisBusters.Remove(source);
        if (IsAnamnesisPair)
        {
            if (action == 19311) anamnesisReflection = null;
            if (action is 19319 or 19312 && _actors.ContainsKey(source) && _actors.ContainsKey(target))
            {
                anamnesisLineTargets[target] = _actors[target];
                anamnesisLines.Add((action == 19319 ? 19320u : 19313u, source, target, time.AddSeconds(5.2)));
            }
            if (action is 19320 or 19313)
            {
                var i = anamnesisLines.FindIndex(l => l.Resolve == action);
                if (i >= 0) anamnesisLines.RemoveAt(i);
            }
        }
        if (Active?.BossOid == 0x2CFE && action is 19293 or 19294 && anamnesisTiles.Count > 0) anamnesisTiles.RemoveAt(0);
        if (Active?.BossOid != 0x2CFF) return;
        if (action == 19333 && _actors.TryGetValue(source, out var caster))
        {
            var delay = anamnesisArenaMode switch { 1 => anamnesisWaves.Count > 3 ? 11.6 : 8, 2 => 7.6, _ => 9.8 };
            anamnesisWaves.Add(new($"anamnesis/wave/{++anamnesisSerial}", "逐次海浪",
                RayRectangleTemplate.Symmetric(anamnesisArenaMode == 0 ? 10 : 8, anamnesisArenaMode == 0 ? 21 : 36),
                QuantizedDungeonPosition(caster.Position), caster.Rotation, Activation: time.AddSeconds(delay)));
        }
        if (action is 13268 or 13269 && anamnesisWaves.Count > 0) anamnesisWaves.RemoveAt(0);
    }

    private IEnumerable<DungeonDamagePrediction> AnamnesisDamage()
    {
        foreach (var cast in anamnesisBusters.Values)
            if (_actors.TryGetValue(cast.TargetId, out var a) && !a.Dead && a.PartySlot >= 0)
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, cast.FinishAt, "Tankbuster");
    }

    private void AnamnesisRemoved(ulong source)
    {
        anamnesisBusters.Remove(source);
        // Dead marker targets remain valid until the line resolves; only its
        // source lifetime ends the pending bait.
        anamnesisLines.RemoveAll(l => l.Source == source);
        if (anamnesisHarpooners.Remove(source)) anamnesisDrainActivation = null;
    }

    private void ResetAnamnesis()
    {
        anamnesisBusters.Clear(); anamnesisAnimations.Clear(); anamnesisReflection = null; anamnesisLines.Clear();
        anamnesisAnimationEvents.Clear(); anamnesisLineTargets.Clear();
        anamnesisTiles.Clear(); anamnesisWaves.Clear(); anamnesisActiveDrains.Clear(); anamnesisSolvedDrains.Clear();
        anamnesisHarpooners.Clear(); anamnesisDrainActivation = null; anamnesisArenaMode = 0; anamnesisSerial = 0;
    }
}
