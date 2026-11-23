using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> AlzadaalAoes()
    {
        if (territory != AlzadaalCatalog.TERRITORY || Arena == null) yield break;
        foreach (var aoe in alzadaalTentacles) yield return aoe;
        foreach (var id in alzadaalFountains.Take(10))
            if (_casts.TryGetValue(id, out var cast))
                yield return new($"alzadaal/fountain/{id}", "连续喷泉", cast.Rule.Shape!, cast.Location, Activation: cast.FinishAt);
        if (alzadaalVoid != null) yield return alzadaalVoid;
        foreach (var aoe in alzadaalCones.Take(alzadaalWaves == 2 ? 2 : int.MaxValue)) yield return aoe;
        foreach (var aoe in alzadaalRects.Take(alzadaalWaves == 2 ? 2 : int.MaxValue)) yield return aoe;
        foreach (var aoe in alzadaalMana) yield return aoe;
        foreach (var aoe in alzadaalTraps.Values) yield return aoe;
        if (Active?.BossOid == 0x36C1 && _actors.TryGetValue(PlayerId, out var player) && !HasStatus(player, 2973))
        {
            if (alzadaalBlades == 0)
            {
                var i = 0;
                foreach (var offset in new[] { new Vector3(14, 0, -5), new Vector3(-14, 0, -5), new Vector3(14, 0, 14), new Vector3(-14, 0, 14) })
                    yield return new($"alzadaal/staging/{i++}", "旋转前等待点", new RectangleTemplate(2, 2), Arena.Position + offset,
                        Navigate: true, Safe: true, OutlineOnly: true);
            }
            else yield return new("alzadaal/staging/blade", "直线后的等待点", new CircleTemplate(1), Arena.Position - Vector3.UnitZ * 10,
                Safe: true, OutlineOnly: true);
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 28527))
        {
            IGeometryShape[] regions = [];
            if (cast.TargetId == PlayerId)
                regions = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId)
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
            else if (_actors.TryGetValue(cast.TargetId, out var target)) regions = [new CircleGeometry(target.Position, 6)];
            if (regions.Length > 0)
                yield return new($"alzadaal/stack/{cast.Source}", "四人分摊", new SohrKhaiWallHintTemplate(Arena, regions),
                    Arena.Position, Display: false, Activation: cast.FinishAt);
        }
    }
}
