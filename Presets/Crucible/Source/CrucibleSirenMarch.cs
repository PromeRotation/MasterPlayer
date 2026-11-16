namespace MasterPlayer.Mechanics.Crucible;

sealed class CrucibleSirenMarch(BossModule module) : BossComponent(module)
{
    private const double MarchSeconds = 3;
    private const double HandoffSeconds = .3;
    private (ulong Actor, uint Order, DateTime Expiry, Angle Facing)? _pending;
    private static ActorStatus? Order(Actor actor)
        => actor.FindStatus(2161) ?? actor.FindStatus(2162) ?? actor.FindStatus(2163) ?? actor.FindStatus(2164);
    private static float Offset(uint status) => status switch {2162=>180,2163=>90,2164=>-90,_=>0};

    // run2005's first march overlaps status50: 6y/s normally, 7.8 while sprint remains.
    // Count only the sprint duration already observed when the march is due.
    internal static float Distance(Actor actor, DateTime start, double seconds, float scale = 1)
    {
        var sprint = actor.FindStatus(50);
        var boosted = sprint is {ExpireAt:var until} && until>start
            ? Math.Clamp((until-start).TotalSeconds,0,seconds) : 0;
        return (float)((6*seconds+1.8*boosted)*scale);
    }

    // run2198 (both tries): 神秘鞋 (arena equipment 12, move speed +50%) was bought in the
    // shop before the siren. The 3s march covered 26.9y / 26.6y (~8.9y/s), not 18y. The
    // facing chosen for 6y/s (135deg / 140deg chords from r=18.7 / 18.5) ran into the
    // fence at 2.8s (status 3077, ~500 per tick) and killed the player twice. run2175 had
    // no shoes and matched 6y/s (16.3y; 22.9y with 3.5s of sprint left).
    // The equipment speed is not readable here, so a facing is accepted only if the
    // whole path stays inside for every plausible speed. From any spot in the R20 ring a
    // heading through the centre gives >=19.5y+r, so a valid facing always exists.
    private static readonly float[] SpeedScales = [1f, 1.25f, 1.55f];

    internal Angle? SafePendingFacing(Actor actor, AIHints hints)
    {
        var now=WorldState.CurrentTime;
        if(actor.IsDeadOrDestroyed || Module.PrimaryActor.IsDeadOrDestroyed
            || actor.FindStatus(1257) is {ExpireAt:var active} && active>now
            || actor.FindStatus(1283) is {ExpireAt:var confused} && confused>now
            || Order(actor) is not {} order) return ClearPending();
        // run2116: the native countdown reached zero before 1257 replaced the order.
        // Keep the already verified heading through that handoff; otherwise attacks
        // can face the boss for one frame and latch an outward march. A newly seen
        // order with unknown expiry never acquires control, and a missing transition
        // cannot hold actions indefinitely.
        if(_pending is {} pending && pending.Actor==actor.InstanceID && pending.Order==order.ID
            && now>=pending.Expiry && now<pending.Expiry.AddSeconds(HandoffSeconds)
            && (order.ExpireAt==DateTime.MaxValue || order.ExpireAt<=now)) return pending.Facing;
        if(order.ExpireAt==DateTime.MaxValue || order.ExpireAt<=now
            || order.ExpireAt>now.AddSeconds(1.2)) return ClearPending();
        Angle? Remember(Angle? facing)
        {
            _pending=facing is {} value ? (actor.InstanceID,order.ID,order.ExpireAt,value) : null;
            return facing;
        }
        var offset=Offset(order.ID);
        float Clearance(Angle facing)
        {
            var direction=(facing+offset.Degrees()).ToDirection();
            var clearance=100f;
            foreach(var scale in SpeedScales)
            for(var i=0;i<=60;i++)
            {
                var seconds=i*.05;
                var point=actor.Position+direction*Distance(actor,order.ExpireAt,seconds,scale);
                if(!Arena.InBounds(point+new WDir(.5f,0)) || !Arena.InBounds(point-new WDir(.5f,0))
                    || !Arena.InBounds(point+new WDir(0,.5f)) || !Arena.InBounds(point-new WDir(0,.5f))) return -1;
                var at=order.ExpireAt.AddSeconds(seconds);
                foreach(var hazard in hints.ForbiddenZones)
                    if((hazard.Activation==default || hazard.Activation<=at.AddSeconds(.25))
                        && (hazard.ExpiresAt==null || hazard.ExpiresAt>=at.AddSeconds(-.25)))
                        clearance=Math.Min(clearance,hazard.Shape.Distance(point)-.5f);
            }
            return clearance;
        }
        if(Clearance(actor.Rotation)>.01f) return Remember(actor.Rotation);
        Angle? best=null;
        var score=.01f;
        for(var i=0;i<72;i++)
        {
            var facing=(i*5f).Degrees();
            var clearance=Clearance(facing);
            if(clearance>score){score=clearance;best=facing;}
        }
        return Remember(best);
    }

    private Angle? ClearPending() { _pending=null; return null; }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if(SafePendingFacing(actor,hints) is not {} facing) return;
        hints.DesiredFacing=facing;
        hints.DesiredFacingExpire=_pending!.Value.Expiry.AddSeconds(HandoffSeconds);
        hints.ForcedMarchImminent=true;
        hints.ForceCancelCast=true;
        hints.MaxCastTime=0;
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if(Order(actor) is {} order && order.ExpireAt>WorldState.CurrentTime && order.ExpireAt!=DateTime.MaxValue)
            hints.Add("诱导之歌：最后一刻调整面向，强制移动期间不能转向");
    }
}
