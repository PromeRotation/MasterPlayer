using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal IEnumerable<CarnivaleLineOfSightCast> CarnivaleLastSongs => IsCarnivale && Active != null
        && carnivaleSelected is { Started: true } module ? module.LastSongs : [];
    internal bool CarnivaleLastSongCasting => IsCarnivale && Active != null && carnivaleSelected?.LastSongCasting == true;

    private void CarnivaleLastSongStarted(DungeonPendingCast cast)
    {
        if (!IsCarnivale || cast.Rule.Action != 14756
            || !carnivaleActors.TryGetValue(cast.Source, out var caster)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.LastSongNeedsBootstrap
            && m.Primary.Actor.Oid is 0x271D or 0x271E))
            CarnivaleLastSongStarted(module, cast, caster);
    }

    private void CarnivaleLastSongBootstrap()
    {
        // Activation replays current state after raw Finish/Start reconciliation. Replaying
        // earlier would let an already ended cast clear the new component's independent bool.
        foreach (var module in carnivaleLoaded.Where(m => m.LastSongNeedsBootstrap))
        {
            module.LastSongNeedsBootstrap = false;
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 14756))
                if (carnivaleActors.TryGetValue(cast.Source, out var caster))
                    CarnivaleLastSongStarted(module, cast, caster);
        }
    }

    private void CarnivaleLastSongStarted(CarnivaleModuleState module, DungeonPendingCast cast, AlphaActorState caster)
    {
        var index = module.LastSongs.FindIndex(c => ReferenceEquals(c.Caster, caster));
        if (index >= 0)
        {
            // Packet corrections preserve the source's cached visibility and independent bool.
            module.LastSongs[index] = module.LastSongs[index] with { Cast = cast };
            return;
        }
        var origin = caster.Actor.Position;
        var center = new Vector3(100, origin.Y, 100);
        var contours = module.Primary.Actor.Oid == 0x271D
            ? CarnivaleArenas.Layout2CornersSightContours : CarnivaleArenas.LayoutBigQuadSightContours;
        module.LastSongs.Add(new(++carnivaleLineOfSightGeneration, caster, cast, origin, contours,
            DungeonPolygonVisibility.Create(center, origin, contours)));
        module.LastSongCasting = true;
    }

    private void CarnivaleLastSongFinished(DungeonPendingCast cast)
    {
        if (cast.Rule.Action != 14756) return;
        foreach (var module in carnivaleModules.Where(m => m.Started && m.Primary.Actor.Oid is 0x271D or 0x271E))
        {
            module.LastSongs.RemoveAll(c => ReferenceEquals(c.Cast, cast));
            // Source LastSongHint is a bool: finishing one caster clears the text even if others remain.
            module.LastSongCasting = false;
        }
    }

    private void CarnivaleLastSongComplete(Func<string, bool>? enabled)
    {
        if (!IsCarnivale || carnivaleWiping) return;
        var changed = false;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Primary.Actor.Oid is 0x271D or 0x271E).ToArray())
        {
            if (module.LastSongCasting || _actors.Values.Any(a => a.Oid == module.Primary.Actor.Oid && !a.Dead)) continue;
            CarnivaleUnload(module);
            changed = true;
            if (ReferenceEquals(module, carnivaleSelected)) CarnivaleUnselect();
        }
        if (!changed) return;
        CarnivaleSelect(enabled);
        if (carnivaleSelected is { Started: true } selected && carnivaleEnabled)
        {
            Active = Definitions.Single(d => d.BossOid == selected.Primary.Actor.Oid);
            PrimaryId = selected.Primary.Actor.Id;
            Arena ??= Active.Arena(selected.Primary.Actor.Position);
        }
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleLastSongAoes()
    {
        foreach (var song in CarnivaleLastSongs)
            yield return new($"carnivale/last-song/{song.Generation}", "The Last Song",
                new DungeonShapeTemplate(song.Visibility), song.Visibility.Position, Activation: song.Cast.FinishAt);
    }

    private IEnumerable<string> CarnivaleLastSongHints(Vector3 player)
    {
        if (CarnivaleLastSongCasting)
            yield return Active?.BossOid == 0x271D ? "Take cover behind a barricade!" : "Use the cube to take cover!";
        if (CarnivaleLastSongs.Any(c => c.Visibility.Contains(player, 0))) yield return "GTFO from aoe!";
    }
}
