using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DungeonPendingCast? aitiascopeBite;
    private readonly List<DungeonPendingCast> aitiascopeShots = [];
    private readonly List<ulong> aitiascopeReturnSources = [];
    private DateTime aitiascopeReturnAt;
    private readonly List<Vector3> aitiascopePockets = [];
    private bool aitiascopeSafespots;
    private DateTime aitiascopeSafespotAt;
    private readonly List<DungeonAmbientAoe> aitiascopeShrapnel = [];
    private readonly List<ulong> aitiascopeCones = [];
    private DungeonPendingCast? aitiascopeCurtain;
    private DungeonActor[] aitiascopeIce = [];
    private bool aitiascopeCurtainArena;

    private void AitiascopeCast(DungeonPendingCast cast)
    {
        if (territory != AitiascopeCatalog.TERRITORY) return;
        if (cast.Rule.Action == 25673) aitiascopeBite = cast;
        if (cast.Rule.Action == 25686)
        {
            var target = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId;
            delayedDamage[cast.Source] = cast with { TargetId = target };
        }
        if (cast.Rule.Action is 25670 or 25671)
        {
            var i = aitiascopeShots.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (i >= 0) aitiascopeShots[i] = cast;
            else aitiascopeShots.Add(cast);
            aitiascopeReturnSources.Clear();
        }
        if (cast.Rule.Action is 25691 or 25692 && !aitiascopeCones.Contains(cast.Source))
        {
            aitiascopeCones.Add(cast.Source);
            if (aitiascopeCones.Count >= 8)
                aitiascopeCones.Sort((a, b) => _casts[a].FinishAt.CompareTo(_casts[b].FinishAt));
        }
        if (cast.Rule.Action == 25702)
        {
            if (aitiascopeCurtain?.Source != cast.Source || aitiascopeCurtain.StartedAt != cast.StartedAt)
                aitiascopeIce = _actors.Values.Where(a => a.Oid == 0x3470).ToArray();
            aitiascopeCurtain = cast;
        }
    }

    private void AitiascopeFinish(DungeonPendingCast cast, DateTime time)
    {
        if (territory != AitiascopeCatalog.TERRITORY) return;
        if (aitiascopeShots.Count > 0 && cast.Rule.Action is 25670 or 25671)
        {
            aitiascopeShots.RemoveAt(0);
            if (cast.Rule.Action == 25670)
            {
                aitiascopeReturnAt = time.AddSeconds(10);
                aitiascopeReturnSources.Add(cast.Source);
            }
        }
        if (cast.Rule.Action == 25684)
        {
            var index = aitiascopeShrapnel.FindIndex(a => Math.Abs(a.Position.X - cast.Location.X) <= 1
                && Math.Abs(a.Position.Z - cast.Location.Z) <= 1);
            if (index >= 0) aitiascopeShrapnel.RemoveAt(index);
        }
        aitiascopeCones.Remove(cast.Source);
        if (cast.Rule.Action == 25702) { aitiascopeCurtain = null; aitiascopeIce = []; }
    }

    private void SynchronizeAitiascope()
    {
        if (territory != AitiascopeCatalog.TERRITORY) return;
        if (aitiascopeBite is { } bait && (!_actors.TryGetValue(bait.Source, out var source) || source.Dead
            || !_actors.TryGetValue(bait.TargetId, out var target) || target.Dead)) aitiascopeBite = null;
        if (Active?.BossOid != 0x346B || aitiascopeShrapnel.Count != 0 || Arena == null) return;
        Vector2[]? positions = null;
        foreach (var marker in _actors.Values.Where(a => a.Oid == 0x346C && a.Position.Z < 144))
        {
            if (marker.Rotation >= .1f * MathF.PI / 180) positions = AitiascopeShrapnelPositions.SWNE;
            else if (marker.Rotation <= -.1f * MathF.PI / 180) positions = AitiascopeShrapnelPositions.NWSE;
            AddShrapnel();
            return;
        }
        foreach (var marker in _actors.Values.Where(a => a.Oid == 0x346D))
        {
            if (marker.Position.X < 11) positions = AitiascopeShrapnelPositions.W;
            else if (marker.Position.X > 11) positions = AitiascopeShrapnelPositions.E;
            if (positions == null) continue;
            AddShrapnel();
            return;
        }
        void AddShrapnel()
        {
            if (positions == null) return;
            for (var i = 0; i < positions.Length; ++i)
                aitiascopeShrapnel.Add(new($"aitiascope/shrapnel/{i}", "连续弹片爆炸", new CircleTemplate(6),
                    new(positions[i].X, Arena.Position.Y, positions[i].Y), Activation: _now.AddSeconds(8)));
        }
    }

    private void AitiascopeMap(uint index, uint state, DateTime time)
    {
        if (territory != AitiascopeCatalog.TERRITORY || Arena == null) return;
        if (Active?.BossOid == 0x346E && index == 5 && state is 0x00020001 or 0x00080004)
        {
            aitiascopeCurtainArena = state == 0x00020001;
            Arena = aitiascopeCurtainArena ? AitiascopeArenas.AmonCurtain(Arena.Position.Y) : AitiascopeArenas.Amon(Arena.Position.Y);
        }
        if (Active?.BossOid != 0x346B) return;
        if (state == 0x00020001 && index == 0)
            Arena = AitiascopeRhitahtynArena.Create(Arena.Position.Y, aitiascopePockets);
        else if (state == 0x00200010)
        {
            if (index == 1) aitiascopePockets.AddRange([new(19.5f, Arena.Position.Y, 152), new(2.5f, Arena.Position.Y, 136)]);
            else if (index == 2) aitiascopePockets.AddRange([new(2.5f, Arena.Position.Y, 152), new(19.5f, Arena.Position.Y, 136)]);
            aitiascopeSafespots = true;
            aitiascopeSafespotAt = time.AddSeconds(7.3);
            Arena = AitiascopeRhitahtynArena.Create(Arena.Position.Y, aitiascopePockets);
        }
        else if (state == 0x00080004)
        {
            if (index == 0) { aitiascopePockets.Clear(); Arena = new RectangleGeometry(new(11, Arena.Position.Y, 144), 0, 39, 39); }
            else if (index is 1 or 2) aitiascopeSafespots = false;
        }
    }

    private void AitiascopeEffect(ulong source, uint action)
    {
        if (territory == AitiascopeCatalog.TERRITORY && action == 25673 && aitiascopeBite?.Source == source)
            aitiascopeBite = null;
    }

    private void ResetAitiascope()
    {
        aitiascopeBite = null; aitiascopeShots.Clear(); aitiascopeReturnSources.Clear(); aitiascopeReturnAt = default;
        aitiascopePockets.Clear(); aitiascopeSafespots = false; aitiascopeSafespotAt = default; aitiascopeShrapnel.Clear();
        aitiascopeCones.Clear(); aitiascopeCurtain = null; aitiascopeIce = []; aitiascopeCurtainArena = false;
    }
}
