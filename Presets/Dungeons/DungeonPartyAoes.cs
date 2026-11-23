using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> PartyAoes()
    {
        var casts = _casts.Values.Where(c => c.Rule.PartyAoe != null && c.Rule.Action != 29283
            && !(territory == TroiaCatalog.TERRITORY && c.Rule.Action is 31208 or 30243 or 30254)
            && territory != AetherfontCatalog.TERRITORY && territory != LunarCatalog.TERRITORY && territory != IhuykatumuCatalog.TERRITORY
            && territory != WorqorCatalog.TERRITORY && territory != SkydeepCatalog.TERRITORY && territory != VanguardCatalog.TERRITORY
            && territory != OrigenicsCatalog.TERRITORY && territory != AlexandriaCatalog.TERRITORY && territory != TenderValleyCatalog.TERRITORY
            && territory != StrayboroughCatalog.TERRITORY && territory != YuweyawataCatalog.TERRITORY && territory != UnderkeepCatalog.TERRITORY && territory != MesoCatalog.TERRITORY && territory != MistwakeCatalog.TERRITORY && territory != ClyteumCatalog.TERRITORY);
        if (akhMorn != null) casts = casts.Append(akhMorn);
        foreach (var cast in casts)
        {
            if (!_actors.TryGetValue(cast.TargetId, out var target) || target.Dead || cast.Rule.Shape is not CircleTemplate shape) continue;
            var stack = cast.Rule.PartyAoe == "分摊";
            yield return new($"party/display/{cast.Source}", cast.Rule.Name, shape, target.Position,
                Navigate: false, Safe: stack, OutlineOnly: territory != TroiaCatalog.TERRITORY || PlayerId == target.Id || stack);
            // Lyssa's support party gathers at a fixed point rather than the marked actor.
            if (territory == KtisisCatalog.TERRITORY && cast.Rule.Action == 25180 && _actors.Values.Any(a => a.Buddy && a.PartySlot >= 0))
                continue;
            if (PlayerId != target.Id)
            {
                yield return new($"party/avoid/{cast.Source}", cast.Rule.Name, shape, target.Position,
                    Display: false, Safe: stack, Activation: cast.FinishAt);
            }
            else if (!stack)
            {
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"party/separate/{cast.Source}/{member.Id}", cast.Rule.Name, shape, member.Position,
                        Display: false, Activation: cast.FinishAt);
            }
        }
    }
}
