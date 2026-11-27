using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsOzma => Definition.Key == "A24Ozma";
    internal ushort ContentFinderId { get; set; }
    internal uint[] OzmaArenaStatus { get; } = new uint[24];
    internal DateTime[] OzmaBomb { get; } = new DateTime[64];
    private readonly Dictionary<BozjaActor, Vector3> ozmaPrevious = [];
    private uint ozmaSharedArena;
    internal float OzmaRadius => ozmaSharedArena switch
    {
        1 => OzmaArenas.SplitRadius,
        2 => OzmaArenas.ShadeRadius,
        _ => OzmaArenas.InitialRadius,
    };

    private static bool OzmaRect(Vector3 point, float x, float z, float halfX, float halfZ)
        => MathF.Abs(point.X - x) <= halfX && MathF.Abs(point.Z - z) <= halfZ;

    private bool OzmaComplete()
    {
        var pc = Actors.GetValueOrDefault(Party.FirstOrDefault(p => p.Slot == 0)?.EntityId ?? 0);
        return Primary is { Destroyed: true } && pc != null
            && (ContentFinderId != 168 || !(OzmaRect(pc.Actor.Position, 300, 265, 80, 80)
                || OzmaRect(pc.Actor.Position, 280, -404.5f, 40, 40)));
    }

    private void BeginOzmaFrame()
    {
        if (!IsOzma) return;
        ozmaPrevious.Clear();
        foreach (var actor in Actors.Values) ozmaPrevious.Add(actor, actor.Actor.Position);
    }

    private void ClearOzma()
    {
        Array.Clear(OzmaArenaStatus);
        Array.Clear(OzmaBomb);
        ozmaSharedArena = 0;
    }

    internal void ObserveOzmaDrawing(BozjaActor viewer)
    {
        if (!IsOzma || !Active) return;
        var slot = ViewerSlot(viewer);
        if (slot is < 0 or >= 24) return;
        var status = OzmaArenaStatus[slot];
        var point = viewer.Actor.Position;
        uint? next = null;
        if (status != 1 && OzmaRect(point, 300, 260, 34, 19)) next = 1;
        else if (status != 2 && OzmaRect(point, 301.5f, 205.5f, 31.5f, 31.5f)) next = 2;
        else if (status > 0 && OzmaRect(point, 280, -404.5f, 40, 40)) next = 0;
        if (next is not { } value) return;
        OzmaArenaStatus[slot] = value;
        ozmaSharedArena = value;
        Arena = value switch
        {
            1 => OzmaArenas.Split(0),
            2 => OzmaArenas.Shade(0),
            _ => OzmaArenas.Initial(0),
        };
        Center = Arena.Position;
    }

    private void OzmaStatus(BozjaActor actor, ReplayStatus status, bool gain)
    {
        if (!IsOzma || !Active || status.Id != 1072) return;
        var slot = ViewerSlot(actor);
        if (slot is >= 0 and < 64) OzmaBomb[slot] = gain ? status.ExpiresAt : default;
    }

    internal (string Mode, DateTime Activation)? OzmaSpecialMode(BozjaActor viewer)
    {
        var slot = ViewerSlot(viewer);
        return IsOzma && Active && slot is >= 0 and < 64 && OzmaBomb[slot] != default
            ? ("Pyretic", OzmaBomb[slot]) : null;
    }

    private IEnumerable<BozjaHint> OzmaHints(BozjaActor viewer, BozjaRule rule, bool consumeState)
    {
        if (rule.Kind != BozjaRuleKind.OzmaBomb) yield break;
        var slot = ViewerSlot(viewer);
        if (slot is < 0 or >= 64 || OzmaBomb[slot] == default) yield break;
        if (OzmaBomb[slot] <= Now.AddSeconds(1e3d))
        {
            // Preserve StayMove's previous-position self comparison.
            var previous = ozmaPrevious.GetValueOrDefault(viewer, viewer.Actor.Position);
            yield return new("Stop everything!", float.IsNaN(previous.X) || float.IsNaN(previous.Z)
                || viewer.RawCast != null || viewer.Actor.TargetId != 0);
        }
        if (consumeState && viewer.Actor.Dead) OzmaBomb[slot] = default;
    }
}
