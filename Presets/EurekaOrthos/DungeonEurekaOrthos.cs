using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsEurekaOrthos => EurekaOrthosCatalog.IsTerritory(territory);
    private bool eurekaEnabled = true;
    private readonly List<ulong> eurekaMandraOrder = [];
    private readonly List<EurekaFlameBreath> eurekaBreaths = [];
    private readonly HashSet<ulong> eurekaCompleted = [];
    private ulong eurekaLoadedPrimary;
    private bool eurekaWiping;
    private long eurekaGeneration;
    internal IReadOnlyList<EurekaFlameBreath> EurekaBreaths => eurekaBreaths;

    private void EurekaObserve(Func<string, bool>? enabled)
    {
        if (!IsEurekaOrthos) return;
        EurekaActorsObserve();
        eurekaEnabled = enabled?.Invoke(Definitions[0].Name) != false;
        eurekaCompleted.RemoveWhere(id => !_actors.ContainsKey(id));
        if (eurekaLoadedPrimary != 0 && !_actors.ContainsKey(eurekaLoadedPrimary))
            eurekaLoadedPrimary = 0;
        if (Active == null && _actors.GetValueOrDefault(eurekaLoadedPrimary) is { } loaded
            && _actors.Values.FirstOrDefault(a => a.Player && a.PartySlot == 0) is { } source
            && Vector3.DistanceSquared(loaded.Position, source.Position) > 500 * 500)
            eurekaLoadedPrimary = 0;
        if (Active != null && (!_actors.TryGetValue(PrimaryId, out var primary) || primary.Dead || primary.Hp == 0))
        {
            if (primary != null) eurekaCompleted.Add(primary.Id);
            eurekaLoadedPrimary = 0;
            ResetFight();
        }
        if (eurekaLoadedPrimary == 0 && _actors.Values.FirstOrDefault(a => a.Player && a.PartySlot == 0) is { } sourcePlayer)
            eurekaLoadedPrimary = _actors.Values.LastOrDefault(a => EurekaOrthosCatalog.Owns(territory, a.Oid)
                && !a.Dead && !eurekaCompleted.Contains(a.Id)
                && Vector3.DistanceSquared(a.Position, sourcePlayer.Position) <= 500 * 500)?.Id ?? 0;
    }

    internal void EurekaDirector(uint update)
    {
        if (!IsEurekaOrthos) return;
        if (update == 0x40000005)
        {
            if (eurekaLoadedPrimary != 0) eurekaCompleted.Add(eurekaLoadedPrimary);
            eurekaLoadedPrimary = 0;
            eurekaWiping = true;
            ResetFight();
        }
        else if (update == 0x40000011) eurekaWiping = false;
    }

    private void EurekaCast(DungeonPendingCast cast)
    {
        EurekaTiamatCast(cast);
        EurekaChaseStart(cast);
        EurekaTwintaniaCast(cast);
        EurekaMinotaurCast(cast);
        EurekaChimeraCast(cast);
        EurekaAeturnaCast(cast);
        EurekaKaliyaCast(cast);
        EurekaAdministratorCast(cast);
        EurekaExcaliburCast(cast);
        if (territory == EurekaOrthosCatalog.GANCANAGH && cast.Rule.Action == 32700
            && !eurekaMandraOrder.Contains(cast.Source)) eurekaMandraOrder.Add(cast.Source);
    }

    private void EurekaFinish(DungeonPendingCast cast, bool completed)
    {
        if (!IsEurekaOrthos) return;
        if (territory == EurekaOrthosCatalog.TIAMAT && !completed)
            eurekaTiamatHits.Remove(cast.Source);
        EurekaTwintaniaFinish(cast);
        EurekaMinotaurFinish(cast);
        EurekaChimeraFinish(cast);
        EurekaAeturnaFinish(cast);
        EurekaKaliyaFinish(cast);
        EurekaAdministratorFinish(cast);
        EurekaExcaliburFinish(cast);
        if (cast.Rule.Action == 32700) eurekaMandraOrder.Remove(cast.Source);
        if (territory == EurekaOrthosCatalog.CLONING_NODE && cast.Rule.Action == 32864 && cast.Observed && eurekaBreaths.Count != 0)
            eurekaBreaths.RemoveAt(0);
    }

    internal bool UsesEurekaNativeEffect(uint action) => territory == EurekaOrthosCatalog.CLONING_NODE && action == 32552
        || territory == EurekaOrthosCatalog.TIAMAT && action is 31847 or 31848
        || territory == EurekaOrthosCatalog.TWINTANIA && action == 31463
        || territory == EurekaOrthosCatalog.CHIMERA && action is 31851 or 31852 or 31853 or 31854 or 32806 or 32807 or 31865
        || territory == EurekaOrthosCatalog.MINOTAUR && action is 31872 or >= 31868 and <= 31871
        || territory == EurekaOrthosCatalog.AETURNA && action == 31445
        || territory == EurekaOrthosCatalog.PROTO_KALIYA && action == 31422
        || territory == EurekaOrthosCatalog.EXCALIBUR && action is 31331 or 31332 or 31351 or 31348;

    private bool EurekaSpellSource(ulong source) => _actors.TryGetValue(source, out var actor)
        && !actor.Player && !actor.Buddy && actor.NativeType is not (0x104 or 0x202 or 0x203 or 0x209);

    private void EurekaEffect(ulong source, uint action, ulong target, Vector3? location, DateTime now)
    {
        if (!EurekaSpellSource(source)) return;
        if (IsEurekaExcalibur)
        {
            if (Active != null && eurekaEnabled) EurekaExcaliburEffect(action);
            return;
        }
        if (IsEurekaKaliya)
        {
            if (Active != null && eurekaEnabled && action == 31422)
            {
                var index = eurekaResonances.FindIndex(b => b.Source.Actor.Id == source);
                if (index >= 0) eurekaResonances.RemoveAt(index);
            }
            return;
        }
        if (territory == EurekaOrthosCatalog.AETURNA)
        {
            if (Active != null && eurekaEnabled) EurekaAeturnaEffect(action, target);
            return;
        }
        if (territory == EurekaOrthosCatalog.CHIMERA)
        {
            if (Active != null && eurekaEnabled) EurekaChimeraEffect(source, action);
            return;
        }
        if (territory == EurekaOrthosCatalog.MINOTAUR)
        {
            if (Active != null && eurekaEnabled) EurekaMinotaurEffect(action);
            return;
        }
        if (territory == EurekaOrthosCatalog.TWINTANIA)
        {
            if (Active != null && eurekaEnabled && _actors.ContainsKey(source))
                TrialVoidzoneEffect(action, target, location, now);
            return;
        }
        if (territory == EurekaOrthosCatalog.TIAMAT)
        {
            if (Active != null && eurekaEnabled && _actors.ContainsKey(source)) EurekaChaseEffect(source, target, location, now);
            return;
        }
        if (Active == null || !eurekaEnabled || !_actors.TryGetValue(source, out var caster)) return;
        var position = caster.Position;
        var (sin, cos) = Math.SinCos(caster.Rotation);
        var direction = new Vector3((float)sin, 0, (float)cos);
        var far = new Vector2(position.X + 300, position.Z + 300).LengthSquared() > 625;
        if (far)
        {
            var angle = caster.Rotation;
            var diagonals = new[] { -45.003f, 44.998f, 134.999f, -135.005f };
            const float degreesToRadians = MathF.PI / 180f;
            var diagonal = diagonals.Any(degrees =>
            {
                var difference = angle - degrees * degreesToRadians;
                while (difference < -MathF.PI) difference += MathF.Tau;
                while (difference > MathF.PI) difference -= MathF.Tau;
                return MathF.Abs(difference) <= degreesToRadians;
            });
            position += direction * (diagonal ? 22.627417f : 22f);
        }
        position.X = MathF.Round(position.X * 2) * .5f;
        position.Z = MathF.Round(position.Z * 2) * .5f;
        if (far && position.X == -315 && position.Z == -315)
        {
            position.X = -315.5f;
            position.Z = -315.5f;
        }
        position = QuantizedDungeonPosition(position);
        eurekaBreaths.Add(new(++eurekaGeneration, source, caster.Position, caster.Rotation,
            now, position, now.AddSeconds(9.6d)));
    }

    private IEnumerable<DungeonAmbientAoe> EurekaAoes()
    {
        if (!IsEurekaOrthos || !eurekaEnabled) yield break;
        var primary = _actors.GetValueOrDefault(eurekaLoadedPrimary);
        if (primary == null || !_actors.ContainsKey(PlayerId)) yield break;
        var boundary = Arena ?? EurekaOrthosCatalog.Arena(territory, primary.Position.Y);
        if (Active == null)
            yield return new("eureka/precombat/arena", "场地", new LocalPolygonTemplate(
                boundary.GetContours()[0].Select(p => GeometryHelper.WorldToLocal(p, boundary.Position, 0))),
                boundary.Position, Navigate: false, OutlineOnly: true);
        if (!primary.Dead && primary.Targetable)
            yield return HeavenswardIntroEnemyMarker(primary, boundary, "eureka/enemy", new(1, 0, 0, 1));
        if (Active == null) yield break;
        foreach (var aoe in EurekaTiamatAoes()) yield return aoe;
        foreach (var aoe in EurekaTwintaniaAoes()) yield return aoe;
        foreach (var aoe in EurekaMinotaurAoes()) yield return aoe;
        foreach (var aoe in EurekaChimeraAoes()) yield return aoe;
        foreach (var aoe in EurekaAeturnaAoes()) yield return aoe;
        foreach (var aoe in EurekaKaliyaAoes()) yield return aoe;
        foreach (var aoe in EurekaAdministratorAoes()) yield return aoe;
        foreach (var aoe in EurekaExcaliburAoes()) yield return aoe;
        if (eurekaBreaths.Count == 0) yield break;
        var deadline = eurekaBreaths[0].Activation.AddSeconds(1d);
        foreach (var breath in eurekaBreaths)
        {
            if (breath.Activation >= deadline) break;
            yield return new($"eureka/breath/{breath.Generation}", "Flame Breath",
                new FanTemplate(50, MathF.PI / 6), breath.Origin, breath.CasterRotation, Activation: breath.Activation);
        }
    }

    private void ResetEurekaOutput()
    {
        eurekaBreaths.Clear();
        eurekaMandraOrder.Clear();
        eurekaChasers.Clear();
        eurekaWheiTargets.Clear();
        eurekaChaseMissingPosition = false;
        eurekaHeadTracks.Clear();
        eurekaTiamatHits.Clear();
        eurekaRecoveries.Clear();
        eurekaGridCastStartedAt = null;
        eurekaGridUntil = default;
        eurekaTwisterCreated.Clear();
        eurekaTwister.Reset();
        eurekaDiveTwister.Reset();
        eurekaTwisterInputs.Clear();
        eurekaTurbines.Clear();
        eurekaDive = null;
        eurekaDivePreparing = false;
        eurekaSwipes.Clear();
        eurekaShocks.Clear();
        eurekaShockCreated.Clear();
        eurekaGroans.Clear();
        ResetEurekaChimera();
        eurekaShatters.Clear();
        eurekaSteelClaws.Clear();
        eurekaAeturnaCrystals.Clear();
        eurekaFerocityCasted = false;
        eurekaAeturnaCrystalsBootstrapped = false;
        ResetEurekaKaliya();
        ResetEurekaAdministrator();
        ResetEurekaExcalibur();
    }

    private bool EurekaBreathDanger(Vector3 player) => EurekaAoes().Where(a => a.Id.StartsWith("eureka/breath/")).Any(a =>
    {
        var x = player.X - a.Position.X;
        var z = player.Z - a.Position.Z;
        var lengthSquared = x * x + z * z;
        if (lengthSquared == 0 || lengthSquared > 2500) return false;
        var (sin, cos) = Math.SinCos(a.Rotation);
        var inverseLength = 1f / MathF.Sqrt(lengthSquared);
        return x * inverseLength * (float)sin + z * inverseLength * (float)cos
            >= (float)Math.Cos(15f * (MathF.PI / 180f));
    });
}
