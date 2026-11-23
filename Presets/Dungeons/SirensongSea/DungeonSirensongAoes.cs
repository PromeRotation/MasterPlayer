using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> SirenAoes()
    {
        if (territory != SirensongCatalog.TERRITORY || Arena == null) yield break;
        foreach (var (id, activation) in sirenStacks)
            if (_actors.TryGetValue(id, out var target))
                yield return new($"siren/stack/{id}", "Hydroball四人分摊", new CircleTemplate(5), target.Position,
                    Safe: true, Activation: activation);
        if (sirenShadowCones >= 6)
            foreach (var aoe in sirenShadow) yield return aoe;
        if (Active?.BossOid == 0x1AFC && _actors.TryGetValue(PrimaryId, out var boss))
        {
            if (sirenTethers.TryGetValue(PlayerId, out var activation))
            {
                yield return new("siren/tether/avoid", "远离首领16码拉断连线", new CircleTemplate(16), boss.Position, Display: false, Activation: activation);
                if (_actors.TryGetValue(PlayerId, out var player))
                {
                    foreach (var line in DungeonMovementPreview("siren/tether", "拉断连线", boss.Position, player.Position)) yield return line;
                }
            }
            if (sirenPullTarget == PlayerId && _actors.TryGetValue(PlayerId, out var pulled) && !pulled.Dead && !SirenImmune(pulled, sirenPullAt))
            {
                var dangers = sirenShadowCones >= 6 ? sirenShadow.Where(a => a.Activation <= sirenPullAt)
                    .Select(a => a.Shape.Resolve(a.Position, a.Rotation, null)).ToArray() : [];
                var template = new SirensongDisplacementTemplate(Arena, boss.Position, null, 40, dangers);
                yield return new("siren/pull/avoid", "拉近落点危险", template, Arena.Position, Display: false, Activation: sirenPullAt);
                foreach (var preview in DungeonMovementPreview("siren/pull", "拉近40码", pulled.Position, template.Landing(pulled.Position))) yield return preview;
            }
        }
        if (Active?.BossOid != 0x1AFE) yield break;
        if (sirenRing != null) yield return sirenRing;
        foreach (var mine in SirenMines)
            yield return new($"siren/mine/{mine.Id}", "污水", new CircleTemplate(7), mine.Position);
        if (_actors.TryGetValue(PlayerId, out var current) && SirenMarchTemplate(current, current.Rotation) is { } march)
        {
            if (!RequireSirenMovementHold)
            {
                var ring = new DonutTemplate(3, 5).Resolve(SirenLoreleiCenter(Arena.Position.Y), 0, null);
                yield return new("siren/march/staging", "强制行进准备环3至5码", new SohrKhaiWallHintTemplate(Arena, [ring]), Arena.Position, Display: false);
                yield return new("siren/march/ring", "强制行进准备环", new DonutTemplate(3, 5), SirenLoreleiCenter(Arena.Position.Y),
                    Navigate: false, Safe: true, OutlineOnly: true);
                yield return new("siren/march/avoid", "强制行进落点及沿途危险", march, Arena.Position, Display: false,
                    Activation: sirenMarch!.FinishAt.AddSeconds(.7));
            }
            foreach (var preview in DungeonMovementPreview("siren/march", "强制行进路径", current.Position, march.Landing(current.Position))) yield return preview;
        }
    }
}
