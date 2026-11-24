// 护锁刃龙上位狩猎战（Arkveld Extreme）· 个人走位。原单文件脚本 ArkveldExtreme.cs 按段拆分。
/*
BSD 3-Clause License

Copyright (c) 2022-2024, Andrew Gilewsky

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System.Numerics;

namespace MasterPlayer.Mechanics.Arkveld
{
using System.Numerics;



internal readonly record struct CrystalRange(Vector2 Center,float Radius,double? Explosion);

internal sealed partial class Arkveld
{
    private ulong bossId;
    private Vector2 bossPoint;
    private float bossRadius=7.8f;
    private bool bossAvailable,engaged;
    private bool bladeFirstResolved,bladeSecondResolved;
    private bool BladeActive(double now)=>now<=bladeEnd+(bladeSecondResolved?.12:1.65);
    private bool FirstBladePending(double now)=>!bladeFirstResolved && now<=bladeFirst+1.65;
    internal const float MeleeLimit=3;
    // Leave room for the movement controller's stopping tolerance at the edge of the band.
    private const float MeleePreferred=MeleeLimit-.65f;
    private double rushEnd=-100,longFireEnd=-100;

    internal void BeginCombat()=>engaged=true;
    internal bool OpenMovement(bool inCombat,float countdown)
    {
        if(inCombat && !(countdown>0))engaged=true;
        return engaged;
    }
    internal bool Engaged=>engaged;
    internal const double SafetyLead=.5;
    // Allow a live movement tick and the observed cast-to-effect timestamp variation.
    internal const double PlanLead=SafetyLead+.20;
    internal void ObserveBoss(ulong actor,Vector2 point,float radius,bool targetable)
    {
        bossId=actor;bossPoint=point;
        if(radius>0 && float.IsFinite(radius))bossRadius=radius;
        bossAvailable=targetable;
    }
    internal bool WantsMelee(Role role)=>engaged && bossAvailable && IsMelee(role) && OnField(bossPoint);
    internal float EdgeDistance(Vector2 p)=>Math.Max(0,Vector2.Distance(p,bossPoint)-bossRadius);
    private float MeleePenalty(Role role,Vector2 p)=>WantsMelee(role)?Math.Max(0,EdgeDistance(p)-MeleePreferred):0;

    // A flexible stack/tower has an interior. The stack owner stays at its anchor.
    private Vector2 NearWithin(Role role,Vector2 anchor,Vector2 current,float freedom,double time,double deadline,
        Vector2? otherStack=null,Vector2? otherAtHit=null)
    {
        bool ClearOfOther(Vector2 p)=>(otherStack is not {} a || Vector2.DistanceSquared(a,p)>6.7f*6.7f) &&
            (otherAtHit is not {} b || Vector2.DistanceSquared(b,p)>6.7f*6.7f);
        bool alreadyInside=Vector2.DistanceSquared(current,anchor)<=freedom*freedom && ClearOfOther(current);
        uint seed=Variation(role,191),order=FormationSeed(71);
        int rank=((int)role*3+(int)(order&7))&7;
        float angle=(rank+.18f)*(MathF.Tau/8)+((seed%1001)/500f-1)*.24f;
        float radius=Math.Min(freedom-.20f,.95f+((seed>>10)%2101)/1000f);
        var seat=ArkveldWalking.Clamp(anchor+Arena.Direction(angle)*radius);
        Vector2 OpenSeat(Vector2 preferred)
        {
            if(Safe(preferred))return preferred;
            float least=float.PositiveInfinity;
            var best=anchor;
            for(int i=0;i<16;i++)foreach(float f in (ReadOnlySpan<float>)[.40f,.72f,.92f])
            {
                var q=ArkveldWalking.Clamp(anchor+Arena.Direction(angle+i*MathF.Tau/16)*freedom*f);
                if(!Safe(q))continue;
                float cost=Vector2.Distance(q,preferred)+.20f*Vector2.Distance(current,q)+MeleePenalty(role,q);
                if(cost<least){best=q;least=cost;}
            }
            return best;
        }
        if(!WantsMelee(role) || EdgeDistance(seat)<=MeleeLimit)
            return personal?(Safe(seat)?seat:anchor):OpenSeat(seat);
        var offset=bossPoint-anchor;
        var inward=Unit(offset,Vector2.UnitY);
        var tangent=Right(inward);
        float lateral=Vector2.Dot(seat-anchor,tangent)*.75f;
        // Keep distinct feet on the boss-facing arc, including when several melee share a marker.
        // Both sides of this short path stay inside the owner's stack radius.
        float reach=MathF.Sqrt(Math.Max(0,freedom*freedom-lateral*lateral));
        float required=Math.Max(0,offset.Length()-MathF.Sqrt(Math.Max(0,
            (bossRadius+MeleePreferred)*(bossRadius+MeleePreferred)-lateral*lateral)));
        var edgeSeat=ArkveldWalking.Clamp(anchor+inward*Math.Min(reach,required)+tangent*lateral);
        if(Safe(edgeSeat))return edgeSeat;
        var towardsBoss=inward*Math.Min(freedom,Math.Max(0,offset.Length()-bossRadius-MeleePreferred));
        bool Safe(Vector2 p)=>Vector2.DistanceSquared(p,anchor)<=freedom*freedom+.01f && ClearOfOther(p) &&
            !Zones.Any(z=>z.Active(role,deadline) && z.Contains(p)) &&
            (alreadyInside || Vector2.Distance(current,p)<=Walking.Speed*(deadline-time) || Vector2.DistanceSquared(current,p)<=Vector2.DistanceSquared(current,anchor));
        for(int i=4;i>=0;i--)
        {
            var p=ArkveldWalking.Clamp(anchor+towardsBoss*(i*.25f));
            if(Safe(p))return p;
        }
        if(otherStack is {} other)
        {
            var outside=ArkveldWalking.Clamp(anchor+Unit(anchor-other,Vector2.UnitY)*freedom);
            if(Safe(outside))return outside;
        }
        return personal?anchor:OpenSeat(seat);
    }
    private Destination TailFireGoal(Role role,double time,Vector2 current)
    {
        var blasts=Zones.Where(z=>z.Shape==Shape.Circle && z.Snapshot is {} hit && z.Until>=time && hit<longFireEnd+2.4 &&
                                  (z.Roles & (1<<(int)role))!=0).ToArray();
        double first=blasts.Select(z=>z.Snapshot!.Value).DefaultIfEmpty(longFireEnd).Min();
        Vector2 best=current;double cost=double.PositiveInfinity;
        void Consider(Vector2 p)
        {
            float travel=Vector2.Distance(current,p);
            double score=travel+.11*Vector2.Distance(p,Center);
            foreach(var blast in blasts)
            {
                double hit=blast.Snapshot!.Value;
                float clearance=Vector2.Distance(p,blast.Origin)-blast.Length;
                // An early wave must allow the walk to finish. After it goes past, its lane opens.
                float fraction=travel>.01f?Math.Clamp((float)(hit-PlanLead-time)*Walking.Speed/travel,0,1):1;
                float crossing=Vector2.Distance(Vector2.Lerp(current,p,fraction),blast.Origin)-blast.Length;
                double weight=hit-time<4?100:35;
                score+=weight*Math.Max(0,1.1f-Math.Min(clearance,crossing));
                if(score>=cost)return;
            }
            if(score<cost){cost=score;best=p;}
        }
        Consider(current);
        if(cost>3)
            for(int z=-19;z<=19;z+=2)for(int x=-19;x<=19;x+=2)
                if(x*x+z*z<=19*19)Consider(Center+new Vector2(x,z));
        return new(best,Math.Max(time+.15,first-PlanLead),"末段地火 · 避开逐次爆点和相邻结晶",.35f);
    }
    private readonly List<(Vector2 From,Vector2 To,double Hit)> rushLanes=[];
    private readonly Vector2[] rushAnchors=new Vector2[8];
    private readonly int[] rushRevisions=new int[8];
    private bool RingsTrappingAnchor(Vector2 point,double time)=>awaitingArea.Values.Count(z=>z.Shape is Shape.Ring or Shape.Circle && z.Snapshot>=time-.4 && z.Snapshot<=time+4.5)>=3 &&
        awaitingArea.Values.Any(z=>z.Shape==Shape.Ring && z.Snapshot>=time && z.Snapshot<time+4 && z.Contains(point));
    private Destination RushGoal(Role role,double time,Vector2 current)
    {
        int seat=(int)role;var rushAnchor=rushAnchors[seat];
        if((time<irregularUntil || rushLanes.Count>=2 && RingsTrappingAnchor(rushAnchor,time)) && rushLanes.Count>=2)
        {
            // Two outer landings establish a side equidistant from their expanding
            // rings. From there only small radial steps are needed as the fronts pass.
            var chordMid=(rushLanes[0].To+rushLanes[1].To)*.5f-Center;
            if(chordMid.LengthSquared()>12 && Vector2.DistanceSquared(rushLanes[0].To,rushLanes[1].To)>190)
            {
                var end=Center+Vector2.Normalize(chordMid)*18;
                if(time<ringsBegan || time-ringsBegan>12 ||
                    Vector2.DistanceSquared(current,end)<12*12 && !RingsTrappingAnchor(end,time))
                    return new(end,Math.Max(time+.4,rushLanes[^1].Hit-PlanLead),"三连突进 · 在前两处落点的等距侧躲环",.4f){Room=ArkveldRoom.Rush};
            }
        }
        if(rushRevisions[seat]!=rushLanes.Count)
        {
            rushRevisions[seat]=rushLanes.Count;
            float score=float.PositiveInfinity;var bestRush=current;
            // Hold a side with room for actual motor delay. All telegraphed lanes contribute,
            // including the next charge before the previous charge has landed.
            for(int z=-18;z<=18;z++)for(int x=-18;x<=18;x++)
            {
                if(x*x+z*z>18*18)continue;
                var p=Center+new Vector2(x,z);
                float clearance=rushLanes.Min(l=>{
                    var d=l.To-l.From;
                    var along=Math.Clamp(Vector2.Dot(p-l.From,d)/Math.Max(.01f,d.LengthSquared()),0,1);
                    return Vector2.Distance(p,l.From+d*along)-6;
                });
                float cost=12*Math.Max(0,3.6f-clearance)+Vector2.Distance(p,current)*.65f+
                    Vector2.Distance(p,Center)*.30f+(rushLanes.Count>1?Vector2.Distance(p,rushAnchor)*.20f:0);
                if(cost<score){score=cost;bestRush=p;}
            }
            rushAnchor=rushAnchors[seat]=bestRush;
        }
        double next=rushLanes.Where(l=>l.Hit>time).Select(l=>l.Hit).DefaultIfEmpty(rushEnd).Min();
        if((time<irregularUntil || RingsTrappingAnchor(rushAnchor,time)) && time-ringsBegan is >=0 and <12)
        {
            var rings=awaitingArea.Values.Where(z=>z.Shape is Shape.Circle or Shape.Ring &&
                z.Until>=time && z.Snapshot<=time+5).ToArray();
            if(rings.Length>0)
            {
                var best=current;double lowest=double.PositiveInfinity;
                void Consider(Vector2 p)
                {
                    float travel=Vector2.Distance(current,p);double cost=.75*travel+.22*Vector2.Distance(p,rushAnchor);
                    foreach(var ring in rings)
                    {
                        double due=ring.Snapshot!.Value;
                        float d=Vector2.Distance(p,ring.Origin);
                        float clearance=ring.Shape==Shape.Circle?d-ring.Length:Math.Max(d-ring.Length,ring.Width-d);
                        double weight=due<=time+2?38:due<=time+3.5?24:8;
                        cost+=weight*Math.Max(0,.85f-clearance);
                        if(ring.Contains(current))cost+=4*Math.Max(0,travel-Walking.Speed*Math.Max(.1,due-time-.4));
                    }
                    if(cost<lowest){lowest=cost;best=p;}
                }
                Consider(current);Consider(rushAnchor);
                foreach(var p in spreadOptions)Consider(p);
                return new(best,rings.Min(z=>z.Snapshot!.Value)-.45,"环形爆炸 · 在已清空的一侧等下一圈",.3f){Room=ArkveldRoom.Rush};
            }
        }
        return new(rushAnchor,next-PlanLead,"三连突进与环形爆炸 · 保持安全侧",.3f){Room=ArkveldRoom.Rush};
    }
    private Destination IdleGoal(Role role,double time,Vector2 current)
    {
        if(time<=rushEnd && rushLanes.Count>0)return RushGoal(role,time,current);
        if(time>=crossStart && time<=crossStart+39.5)
        {
            // During the moving grid the centre is not an appointment. Stay in a
            // reachable pocket with room on both axes until a real mechanic needs us.
            var strips=Zones.Where(z=>z.Shape==Shape.Line && z.Width<4.5f && z.Until>=time &&
                z.Snapshot is {} hit && hit<=time+3.75).ToArray();
            if(strips.Length>0)
            {
                float close=float.MaxValue;var best=current;
                // A solo player may enter this grid from a completely different laser bait.
                // The first wide blade can be on either side of the boss. Stay within reach of both exits.
                bool approach=time-crossStart<14 && bladeEnd<crossStart && OnField(bossPoint);
                var staging=bossPoint+Unit(Center-bossPoint,Vector2.UnitY)*5.2f;
                void Consider(Vector2 p)
                {
                    float walk=Vector2.Distance(current,p);
                    float cost=walk+3.7f*MeleePenalty(role,p);
                    if(approach)cost+=3.2f*Math.Max(0,Vector2.Distance(p,staging)-4.4f);
                    foreach(var strip in strips)
                    {
                        var dir=Arena.Direction(strip.Angle);var d=p-strip.Origin;
                        if(Vector2.Dot(d,dir)<-.3f || Vector2.Dot(d,dir)>strip.Length)continue;
                        float gap=Math.Abs(Vector2.Dot(d,Right(dir)))-strip.Width;
                        cost+=20*Math.Max(0,1.15f-gap);
                    }
                    if(cost<close){close=cost;best=p;}
                }
                Consider(current);
                if(close>1)foreach(var p in spreadOptions)Consider(p);
                return new(best,time+2.2,"交错直线 · 留在安全缝等下一拍",.3f){Room=ArkveldRoom.Cross};
            }
        }
        if(time<=tailUntil)return new(current,time+2.5,"尾刺 · 就近离开射线",.45f){Room=ArkveldRoom.Tail};
        if(!WantsMelee(role))return new(Center,time+6,Mechanic,2.5f){Room=ArkveldRoom.Output};
        var distance=Vector2.Distance(current,bossPoint)-bossRadius;
        var target=current;
        if(distance>MeleeLimit+.00001f)
        {
            var inward=Unit(Center-bossPoint,Sector(role,false));
            float radius=bossRadius+MeleePreferred;
            target=bossPoint+Unit(current-bossPoint,inward)*radius;
            if(Vector2.DistanceSquared(target,Center)>ArkveldWalking.Radius*ArkveldWalking.Radius)
            {
                // Closest point on the preferred ring that is also inside the arena.
                float separation=Vector2.Distance(bossPoint,Center);
                float along=Math.Clamp((radius*radius+separation*separation-ArkveldWalking.Radius*ArkveldWalking.Radius)/
                    (2*Math.Max(.01f,separation)),0,radius);
                var side=Right(inward);
                if(Vector2.Dot(current-bossPoint,side)<0)side=-side;
                target=ArkveldWalking.Clamp(bossPoint+inward*along+side*MathF.Sqrt(radius*radius-along*along));
            }
        }
        return new(target,time+5,"近战 · 目标圈内及圈外三米",.35f){Room=ArkveldRoom.Output};
    }
    private Vector2 BladePoint(Role role,bool first,double hit,double time,Vector2 current)
    {
        var side=first?bladeSide:-bladeSide;
        var original=Center+side*7.8f+bladeAlong*2;
        if(bossAvailable && OnField(bossPoint) && time-crossStart is >=0 and <40 && hit>=crossStart && hit<crossStart+40)
        {
            // Stage both hits in the same crossing corridor. Sprint changes reachable distance,
            // but gaining or losing it cannot move the exit to the other end of the arena.
            var infield=Unit(Center-bossPoint,-bladeAlong);
            // At the first snapshot only the wide half-room is still active.
            // Stage at its actual safe edge to gain the diagonal-blade crossing time.
            return ArkveldWalking.Clamp(bossPoint+side*(first?7.42f:7.65f)+infield*5.15f);
        }
        if(bossAvailable && OnField(bossPoint))
        {
            // Cross behind the short angled blades when their emitters are on the arena side of the boss.
            var flank=ArkveldWalking.Clamp(bossPoint+side*7.8f);
            if(!Zones.Any(z=>z.Active(role,hit) && z.Contains(flank)))original=flank;
        }
        if(!WantsMelee(role) || EdgeDistance(original)<=MeleeLimit)return original;
        Vector2 inward=Vector2.Dot(Center-bossPoint,bladeAlong)>=0?bladeAlong:-bladeAlong;
        // The sweeping floor lines can cover the rim of the safe half. Test both the
        // usual offset and points closer to the cleave boundary before giving up uptime.
        float travel=(float)Math.Max(0,hit-PlanLead-time)*Walking.Speed+.35f;
        bool CanStage(Vector2 p)=>Vector2.DistanceSquared(current,p)<=travel*travel &&
            Vector2.DistanceSquared(p,Center)<=ArkveldWalking.Radius*ArkveldWalking.Radius &&
            !Zones.Any(z=>z.Active(role,hit) && z.Contains(p));
        foreach(float lateral in (ReadOnlySpan<float>)[8.5f,8f,7.65f])
        {
            for(int i=0;i<2;i++)
            {
                float radius=bossRadius+(i==0?MeleePreferred:MeleeLimit);
                if(radius<lateral)continue;
                var p=bossPoint+side*lateral+inward*MathF.Sqrt(radius*radius-lateral*lateral);
                if(CanStage(p))return p;
            }
        }
        // Stay in the assigned half and advance only as far along it as the snapshot allows.
        var rim=bossPoint+side*8.5f;
        float end=Vector2.Dot(original-rim,inward);
        for(float depth=3;depth<end;depth+=1)
        {
            var p=rim+inward*depth;
            if(CanStage(p))return p;
        }
        return original;
    }

    private bool crystalPhase;
    private readonly Dictionary<ulong,(Vector2 Site,ulong Crystal)> crystalCasts=[];
    private void ConfirmBlast(Signal e)
    {
        if(e.Id is 44809 or 44810)
        {
            if(crystalCasts.Remove(e.Source,out var cast))
            {
                Zones.RemoveAll(z=>z.Owner==(cast.Crystal!=0?cast.Crystal:e.Source) && z.Shape==Shape.Circle && Vector2.DistanceSquared(z.Origin,cast.Site)<.16f &&
                    z.Snapshot is {} at && Math.Abs(at-e.Time)<.55);
                if(cast.Crystal!=0)crystalIgnitions[cast.Crystal]=e.Time;
            }
            return;
        }
        // Retire only this helper's resolved step. The next step and nearby crystals remain armed.
        int index=Zones.FindIndex(z=>z.Owner==e.Source && z.Shape==Shape.Circle && z.Snapshot is {} at &&
            Math.Abs(e.Time-at)<.5 && (!OnField(e.Point) || Vector2.DistanceSquared(e.Point,z.Origin)<1));
        if(index>=0)Zones.RemoveAt(index);
    }
    private int crystalWaves,crystalSpreads;
    private double crystalCast=-100,crystalFirstHit,crystalWaveEnd,lastCrystalSpread=-100;
    private bool cardinalFire;
    private void BeginCrystalWave(double time,double hit,float orientation)
    {
        if(time-crystalCast<1)return; // the four directions belong to one wave
        if(!crystalPhase || time-crystalCast>12)
        {crystalPhase=true;crystalWaves=0;crystalSpreads=0;lastCrystalSpread=-100;}
        crystalWaves++;
        crystalCast=time;crystalFirstHit=hit;
        crystalWaveEnd=hit+2*1.1+.38;
        cardinalFire=orientation<.3f;
    }
    internal bool CrystalPhase(double time)=>crystalPhase &&
        (crystalWaves<4 || crystalSpreads<2 || time<=Math.Max(crystalWaveEnd,lastCrystalSpread+.30));

    internal static Vector2 Sector(Role role,bool diagonalStand)
    {
        Vector2 d=role switch {Role.MT or Role.D3=>-Vector2.UnitY,Role.H1 or Role.D1=>-Vector2.UnitX,
            Role.H2 or Role.D4=>Vector2.UnitY,_=>Vector2.UnitX};
        return diagonalStand?Vector2.Normalize(d+Right(d)):d;
    }
    private static string SectorName(Vector2 d)=>Math.Abs(d.X)<.1f?(d.Y<0?"北":"南"):
        Math.Abs(d.Y)<.1f?(d.X<0?"西":"东"):(d.X<0?"西":"东")+(d.Y<0?"北":"南");

    private readonly (double Wave,float Across)[] crystalApproaches=new (double,float)[8];
    private Destination CrystalGoal(Role role,double time,Vector2 current)
    {
        var direction=Sector(role,cardinalFire);
        bool inner=role is Role.MT or Role.ST or Role.D1 or Role.D4;
        Role partner=role switch {Role.MT=>Role.D3,Role.D3=>Role.MT,Role.ST=>Role.D2,Role.D2=>Role.ST,
            Role.H1=>Role.D1,Role.D1=>Role.H1,Role.H2=>Role.D4,_=>Role.H2};
        if(!personal && IsMelee(role)!=IsMelee(partner))inner=IsMelee(role);
        bool spread=SpreadPending(time);
        // Each sector owns two distinct radii during the two alternating sets of marks.
        float preferredRadius=spread && !personal?(inner?8.8f:17.4f):WantsMelee(role)?bossRadius+1.1f:spread?(inner?8.8f:17.4f):13;
        var intended=Center+direction*preferredRadius;
        bool softPair=!personal && !spread && crystalWaves>1;
        if(softPair)
        {
            float offset=(inner?1:-1)*(1.65f+(Variation(role,212)%450)/1000f);
            intended=ArkveldWalking.Clamp(intended+Right(direction)*offset,19.15f);
        }
        double until=Math.Max(crystalWaveEnd,crystalFirstHit+4.65);
        var dangers=Zones.Where(z=>z.Owner!=ActorHazard && z.Until>=time && z.From<=until && (z.Roles & 1<<(int)role)!=0).ToArray();
        var neighbours=personal && spread
            ?(spreadMarks.Contains(Members[(int)role])?Members.AsEnumerable():spreadMarks)
                .Where(id=>id!=Members[(int)role] && PointOf(id)!=null)
                .SelectMany(id=>new[]{positions[id],SpreadProjection(id,positions[id],time,spreadEnd)}).ToArray()
            :[];
        double next=dangers.Where(z=>z.Snapshot>=time).Select(z=>z.Snapshot!.Value).DefaultIfEmpty(time+3).Min();
        if(spread && spreadEnd>=time)next=Math.Min(next,spreadEnd);
        double deadline=next-PlanLead;
        float walkBudget=(float)Math.Max(0,deadline-time)*Walking.Speed;
        float bestCost=float.MaxValue;var best=intended;
        // A melee bait stays closer to the sector axis to leave a short escape from
        // the next fire. Ranged baits can use the wider arc when a teammate occupies it.
        bool soloSpread=personal && spread,melee=WantsMelee(role);
        ref var approach=ref crystalApproaches[(int)role];
        if(approach.Wave!=crystalFirstHit)
            approach=(crystalFirstHit,Vector2.Dot(current-Center,Right(direction)));
        float side=Math.Abs(approach.Across)>3?Math.Sign(approach.Across):0;
        float minAlignment=soloSpread?(melee?.63f:.25f):.73f;
        void Consider(Vector2 p)
        {
            var delta=p-Center;float r=delta.Length();
            float alignment=Vector2.Dot(delta,direction)/Math.Max(.01f,r);
            if(r<6.9f || r>19.3f || alignment<minAlignment)return;
            float walking=Vector2.Distance(p,current);
            float cost=Vector2.Distance(p,intended)+(personal && spread?.48f:.14f)*walking;
            if(soloSpread)cost+=1.65f*Math.Max(0,-side*Vector2.Dot(delta,Right(direction)))+.30f*walking;
            if(spread && !personal)cost+=4*Math.Abs(r-preferredRadius);
            cost+=MeleePenalty(role,p)*1.8f;
            // If a later circle reaches this point, budget the distance to its nearest exit
            // starting after the previous snapshot. The walker verifies the connecting segment.
            foreach(var z in dangers)
            {
                if(!z.Contains(p))continue;
                double starts=(z.Snapshot??z.From)-PlanLead;
                if(starts<=next+.22 || z.Shape!=Shape.Circle || !WantsMelee(role) && !spread || spread && starts<=spreadEnd+1.7)cost+=180;
                else cost+=16*Math.Max(0,z.Length-Vector2.Distance(p,z.Origin)+.45f-(float)(starts-next-.22)*Walking.Speed);
            }
            if(!spread)
                foreach(var hazard in dangers.Where(z=>z.Shape==Shape.Circle))
                    cost+=95*Math.Max(0,hazard.Length+1.05f-Vector2.Distance(p,hazard.Origin));
            foreach(var n in neighbours)cost+=28*Math.Max(0,9.2f-Vector2.Distance(n,p));
            if(spread && personal)
                foreach(var z in dangers)
                    if(z.Shape==Shape.Circle && z.Snapshot>spreadEnd && z.Snapshot<=spreadEnd+1.35)
                        cost+=14*Math.Max(0,z.Length+.9f-Vector2.Distance(z.Origin,p));
            cost+=15*Math.Max(0,.73f-alignment);
            if(personal && spread && neighbours.Length>0)
                cost-=1.7f*Math.Min(13,neighbours.Min(n=>Vector2.Distance(n,p)));
            // The second spread has no further partner rotation. Allow a full replan
            // interval before its hit instead of budgeting up to the exact snapshot.
            cost+=16*Math.Max(0,walking-walkBudget);
            if(cost<bestCost){best=p;bestCost=cost;}
        }
        Consider(intended);Consider(current);
        int angleLimit=soloSpread?(melee?48:72):40;
        for(float r=7.5f;r<=19.0f;r+=1f)
            for(int degrees=-angleLimit;degrees<=angleLimit;degrees+=8)
                Consider(Center+Arena.Direction(Facing(direction)+degrees*MathF.PI/180)*r);
        if(!spread && dangers.Any(z=>z.Shape==Shape.Circle))
        {
            // At the rim a few decimetres can separate a small crystal from the step beside it.
            // Search the strip the coarse polar grid misses, before either blast resolves.
            foreach(float r in (ReadOnlySpan<float>)[18.85f,19.20f])
                for(int a=-42;a<=42;a+=3)Consider(Center+Arena.Direction(Facing(direction)+a*MathF.PI/180)*r);
            // A new set of flames can already be casting while the previous crystals are live.
            // Keep the present safe pocket until the last overlapping blast actually clears.
            bool pending=dangers.Any(z=>z.AwaitEffect && z.Shape==Shape.Circle && z.Snapshot<=time+1.35);
            if(pending && dangers.Where(z=>z.Shape==Shape.Circle && z.From<=time+1.4)
                .All(z=>Vector2.Distance(current,z.Origin)>=z.Length+.16f))
                return new(current,time+.55,"龙脉爆发 · 留在安全缝等结晶出伤",.20f);
        }
        return new(best,deadline,$"龙脉爆发 · {SectorName(direction)}组{(spread?"分散":"避开地火与结晶")}",.45f);
    }
    private void ConfirmCrystal(Vector2 center,float radius,double hit,ulong source)
    {
        foreach(var (id,c) in crystals)
        {
            if(Vector2.DistanceSquared(center,c.Point)>.16f)continue;
            crystalIgnitions[id]=hit;
            crystalCasts[source]=(center,id);
            Zones.RemoveAll(z=>z.Owner==id);
            Circle(center,radius+.25f,hit,id);
            return;
        }
        crystalCasts[source]=(center,0);Circle(center,radius+.25f,hit,source);
    }
    internal CrystalRange[] CrystalRanges(double time)=>crystals
        .Where(c=>!crystalIgnitions.TryGetValue(c.Key,out var at) || time<=at+.30)
        .Select(c=>new CrystalRange(c.Value.Point,c.Value.Radius,crystalIgnitions.TryGetValue(c.Key,out var at)?at:null)).ToArray();
}

}
