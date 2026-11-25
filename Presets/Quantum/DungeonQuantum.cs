using System.Numerics;
using MasterPlayer.Scripting;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsQuantum => territory == QuantumCatalog.TERRITORY;
    internal QuantumEncounter Quantum { get; } = new();
    internal bool QuantumInputScan { get; set; }

    private void QuantumSynchronize(IEnumerable<DungeonActor> actors, DateTime now, Func<string, bool>? enabled, ulong playerId)
    {
        var input = actors.ToArray();
        _now = now;
        PlayerId = playerId;
        _actors.Clear();
        foreach (var actor in input) _actors[actor.Id] = actor;
        Quantum.Synchronize(input, CurrentPartyMembers, playerId, now, enabled?.Invoke(Definitions[0].Name) != false,
            update: !QuantumInputScan);
        QuantumPublishState();
    }

    private void QuantumPublishState()
    {
        Active = Quantum.Active ? Definitions[0] : null;
        PrimaryId = Quantum.Primary?.Actor.Id ?? 0;
        Arena = Quantum.Loaded ? Quantum.Arena : null;
    }

    internal void QuantumEvent(LogSystemEvent ev, DateTime now)
    {
        switch (ev)
        {
            case NativeActionEffectEvent effect when effect.ActionType is null or 1:
                Quantum.Effect(effect.SourceId, effect.ActionId, effect.TargetPosition, now);
                break;
            case LogSystemTargetIconEvent icon:
                // The original callback receives the actor carrying the icon.
                Quantum.Icon(icon.SourceId, icon.MarkerId, now);
                break;
            case LogSystemTargetVfxEvent vfx: Quantum.Vfx(vfx.VfxId); break;
            case LogSystemMapEffectEvent map:
                Quantum.Map(map.Position, ((uint)map.Param2 << 16) | map.Param1, now);
                break;
            case LogSystemDirectorUpdateEvent director when director.Command == 0x40000005:
                Quantum.EndModule();
                break;
        }
        QuantumPublishState();
    }

    private ulong QuantumPriorityTarget(ulong current, Vector3 player)
    {
        if (!Quantum.Active || Quantum.CurrentSlot < 0) return 0;
        var targets = _actors.Values.Where(a => a.Hostile && a.Targetable && !a.Dead)
            .Select(a => (Actor: a, Priority: Quantum.TargetPriority(a))).Where(p => p.Priority >= 0)
            .OrderByDescending(p => p.Priority).ThenBy(p => p.Actor.Id == current ? 0 : 1)
            .ThenBy(p => Vector3.DistanceSquared(p.Actor.Position, player)).ToArray();
        return targets.FirstOrDefault().Actor?.Id ?? 0;
    }
}
