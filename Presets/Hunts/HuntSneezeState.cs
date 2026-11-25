using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntSneezeState
{
    private readonly List<(HuntPendingCast Cast, float Offset, HuntPendingCast? Reference)> offsets = [];
    private readonly List<HuntPendingCast> areas = [];
    private HuntPendingCast? sneeze;
    private HuntPendingCast? referenceCast;
    private float reference;
    private DateTime nextActivation;
    private int consumed;
    internal IEnumerable<HuntPendingCast> Areas => areas;

    internal void Start(HuntPendingCast cast, ReplayActorState actor)
    {
        switch (cast.Rule.Action)
        {
            case 27307:
                reference = cast.Rotation;
                referenceCast = cast;
                offsets.Clear();
                offsets.Add((cast, 0, cast));
                sneeze = null;
                break;
            case 27308:
            case 27692:
                offsets.Add((cast, cast.Rotation - reference, referenceCast));
                break;
            case 27309:
                reference = cast.Rotation;
                referenceCast = cast;
                nextActivation = cast.Activation;
                sneeze = cast;
                consumed = 0;
                break;
        }
        Update(actor);
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, ReplayActorState actor)
    {
        if (referenceCast?.Order == previous.Order)
        {
            referenceCast = updated;
            reference = updated.Rotation;
        }
        for (var i = 0; i < offsets.Count; ++i)
        {
            var item = offsets[i];
            if (item.Cast.Order == previous.Order || item.Reference?.Order == previous.Order)
            {
                if (item.Cast.Order == previous.Order) item.Cast = updated;
                if (item.Reference?.Order == previous.Order) item.Reference = updated;
                item.Offset = item.Cast.Rotation - (item.Reference?.Rotation ?? 0);
            }
            offsets[i] = item;
        }
        if (sneeze?.Order == previous.Order)
        {
            sneeze = updated;
            reference = updated.Rotation;
            if (consumed == 0) nextActivation = updated.Activation;
        }
        Update(actor);
    }

    internal void Finish(uint action, DateTime at, ReplayActorState actor)
    {
        if (action is not (27309 or 27310 or 27693) || offsets.Count == 0) return;
        sneeze ??= offsets[0].Cast;
        offsets.RemoveAt(0);
        ++consumed;
        nextActivation = at.AddSeconds(2.2);
        Update(actor);
    }

    internal void Update(ReplayActorState actor)
    {
        HuntPendingCast[] wanted = sneeze == null ? [] : offsets.Take(2).Select((item, i) => sneeze with
        {
            Shape = new FanTemplate(40, 2 * MathF.PI / 3), Location = actor.Position, Rotation = reference + item.Offset,
            Activation = nextActivation.AddSeconds(2.2 * i), SequenceId = $"{sneeze.Source}/sneeze/{item.Cast.Order}",
            Color = i == 0 && offsets.Count > 1 ? new Vector4(1, 1, 0, 1) : null
        }).ToArray();
        if (areas.SequenceEqual(wanted)) return;
        areas.Clear();
        areas.AddRange(wanted);
    }
}
