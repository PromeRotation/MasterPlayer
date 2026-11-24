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



internal sealed partial class Arkveld
{
    private bool firstBeamResolved;
    public override Destination Goal(Role role,double time,Vector2 current)
    {
        var me=Members[(int)role];
        if(time<=numberEnd)return ChaseGoal(role,time,current);
        if(lasers==1 && !firstBeamResolved && time-laserLock is >=0 and <1.10)
        {
            var normal=Right(Arena.Direction(laserAngle));
            if(Vector2.Dot(Center-laserOrigin,normal)<0)normal=-normal;
            float side=Vector2.Dot(current-laserOrigin,normal);
            if(Math.Abs(side)<4.1f)
                return new(ArkveldWalking.Clamp(current+normal*(4.35f-side)),laserLock+.89,"龙闪炮 · 锁定后先横出射线",.20f);
        }
        if(time<=towerEnd+.12)
        {
            Vector2? tower=Tower(role);
            if(tower==null || baits==0)return new(GroupPocket(role,Center,.65f),time+3,"六塔 · 中央集合放第一圈",.35f);
            if(baits<3)
            {
                var radial=Unit(tower.Value-Center,Vector2.UnitY);
                // Bait two between centre and the rim, three outside all tower footprints.
                var outer=ThirdBait(tower.Value);
                var p=baits==1?Center+Unit(outer-Center,radial)*8:outer;
                if(!personal && role is Role.H1 or Role.H2 or Role.D1 or Role.D2)
                {
                    var wing=Right(Unit(p-Center,radial))*(role is Role.H1 or Role.H2?1.12f:-1.12f);
                    var shifted=ArkveldWalking.Clamp(p+wing,19.25f);
                    if(baits==1 || tank.Concat(regular).All(t=>Vector2.Distance(shifted,t)>7.0f))p=shifted;
                }
                return new(p,time+3,baits==1?"六塔 · 放第二圈":"六塔 · 塔外放第三圈",.7f);
            }
            return TowerEntry(role,tower.Value,current,time);
        }
        if(time<=longFireEnd)return TailFireGoal(role,time,current);
        if(waitingStack && (burstOrigin==null || time<=burstEnd+.20))
        {
            var mark=singleImpact??PointOf(singleActor);
            if(me==singleActor && singleImpact==null)
                mark=personal?PartyStackCenter(me,current,time):Center;
            var anchor=mark??Center;
            if(me!=singleActor && me!=0 && singleActor!=0)
                anchor=NearWithin(role,anchor,current,4,time,singleEnd);
            return new(anchor,singleEnd-PlanLead,"八人集合分摊",2);
        }
        if(displacedBlade && !waitingStack && BladeActive(time) && OuterBlade(role,time,current) is {} onSide)
            return new(onSide,Math.Max(time+.20,FirstBladePending(time)?bladeFirst:bladeEnd),"锁刃敲打 · 在两刀都安全的这一侧等结算",.35f);
        if(burstOrigin is {} origin && time<=burstEnd+.20)
        {
            Vector2 towards=BladeActive(time)?Center+(FirstBladePending(time)?bladeSide:-bladeSide)*9:current;
            var direction=Unit(towards-origin,Unit(Center-origin,Vector2.UnitY));
            var exit=ArkveldWalking.Clamp(origin+14*direction);
            if(Vector2.DistanceSquared(exit,origin)<13*13)
                exit=ArkveldWalking.Clamp(origin+Unit(Center-origin,Vector2.UnitY)*14);
            return new(exit,Math.Max(time+.2,burstEnd),"分摊结算 · 撤出落点大圈");
        }
        if(time>=waveStart && time<=waveStart+7.35)
        {
            // Place the two bait waves on successive positions, then cut behind the passing front.
            var side=-Right(waveAxis);
            float across=groundWaves<=1?7.5f:12;
            float ahead=Far(role)?(groundWaves>=2?-6.0f:-3.5f):-12.5f;
            var preferred=ArkveldWalking.Clamp(Center+side*across+waveAxis*ahead);
            return new(personal?FlightGap(preferred,current,time,waveAxis,12):preferred,time+3.2,"诱导圈 · 横移后切到地火后方",1){Room=ArkveldRoom.Bait};
        }
        if(dual.Count==2 && lasers==1 && time-laserStart is >=0 and <24 && DualStackActive(time))
        {
            var staging=LaserPoint(-8.15f,2.8f);
            if(!IsLaserBait(role,time))return new(staging,time+1.5,"龙闪炮 · 等第二束锁定方向再跟组",.45f){Room=ArkveldRoom.Beam};
            // The stack announcement can precede the actual second lock by more than a second.
            // Follow the formal route but cap the bearing change until the game fixes the ray.
            bool far=GroupFor(role,time,current);
            var stack=LaserPoint(far?16:8,far?12:3.8f);
            var along=Unit(stack-staging,Vector2.UnitX);
            float maxStep=Math.Min(Walking.Speed*.45f,Vector2.Distance(staging,laserOrigin)*.30f);
            return new(staging+along*maxStep,time+.7,"龙闪炮 · 点名沿正式路线引导，等锁向后跟组",.18f);
        }
        if(DualStackActive(time) || time<=flightEnd)
        {
            var far=Far(role);
            if(dual.Count>=2)far=GroupFor(role,time,current);
            Vector2 anchor;
            if(time<=flightEnd+.5)
            {
                anchor=Center+(far?Vector2.Zero:flightAxis*12)+flightSide*(narrowFlight?0:11);
                if(narrowFlight && time<firstLane+.18)anchor+=flightSide*6.2f;
            }
            else anchor=LaserPoint(far?16:8,far?12:3.8f);
            bool followReal=false;
            // An unmarked solo player tracks the real marker once it approaches the assigned lane.
            // While both healers are still crossing, use the predicted site to get a head start.
            if(personal && dual.Count==2 && !dual.ContainsKey(me))
            {
                var markers=OrderedMarks();
                ulong id=markers[far?1:0];
                if(PointOf(id) is {} real && (Vector2.Distance(real,anchor)<5 || dualEnd-time<=3.6))
                {anchor=FollowStack(role,id,real,markers[far?0:1],current,time);followReal=true;}
            }
            if(personal && dual.Count==2 && dual.ContainsKey(me) && dualEnd-time<=4.3)
            {
                var marks=OrderedMarks();anchor=PlaceStack(role,anchor,marks[far?0:1],current,time);
            }
            if(personal && time<=flightEnd+.5)
                anchor=FlightGap(anchor,current,time,flightAxis,dual.Count>0?5.5f:12,dual.Count>0);
            if(!IsLaserBait(role,time) && !followReal && dual.Count==2 && !dual.ContainsKey(me) && (!personal || time>firstLane+.2))
            {
                var marks=OrderedMarks();
                ulong other=marks[far?0:1];
                var otherPoint=PointOf(other);
                var site=PointOf(marks[far?1:0]);
                bool close=site is {} marker && (Vector2.DistanceSquared(marker,anchor)<4 ||
                    !personal && dualEnd-time<1.65 && Vector2.DistanceSquared(current,marker)<64);
                if(close || !personal)
                    anchor=NearWithin(role,close?site!.Value:anchor,current,3.5f,time,dualEnd,otherPoint,
                        otherPoint is {} p?Projected(other,p,time,dualEnd):null);
            }
            double appointment=dual.Count>0?dualEnd:time<firstLane?firstLane:flightEnd;
            return new(anchor,appointment-PlanLead,far?"飞翔／龙闪炮 · 远组分摊":"飞翔／龙闪炮 · 近组分摊",1.5f)
                {Room=dual.Count==0?ArkveldRoom.Flight:ArkveldRoom.None};
        }
        if(CrystalPhase(time))return CrystalGoal(role,time,current);
        if(time<=spreadEnd+.33 || time<=spinEnd+.25)
        {
            Vector2 p;
            if(lasers==3 && time>=laserLock && time-laserLock<14)
            {
                // Stay ahead of the third sweeping beam until it locks its last shot.
                // The farthest final position is staged along the rim before the last shot.
                if(time<laserLock+7.9 && !straightLaser && !(personal && time<=spreadEnd && spreadEnd-time<6.8))
                    return new(LaserPoint(-8.15f,2.8f),laserLock+8.0,"龙闪炮 · 等第三轮扫过",.8f){Room=ArkveldRoom.Beam};
                (float x,float z)=role switch {Role.MT=>(-8.7f,3),Role.ST=>(7.3f,2),
                    Role.H1=>(7.8f,15.4f),Role.H2=>(-15.1f,9.7f),Role.D1=>(.04f,1.1f),
                    Role.D2=>(-1.2f,9.2f),Role.D3=>(14.3f,7.2f),_=>(-7,17.35f)};
                p=LaserPoint(x,z);
            }
            else if(Math.Abs(spreadEnd-spinEnd)<2 || time<=spinEnd+.1)
            {
                p=SpinSlot((int)role);
                if(personal && time<=spreadEnd+.28)
                {
                    var site=VacantSpinSlot(role,current,time);
                    if(site is {} open)
                        return new(open,Math.Max(time+.15,spreadEnd-PlanLead),"八方分散 · 按实位进入空位",.45f);
                }
            }
            else
            {
                // Alternate cardinals and intercardinals between the two exaflare patterns.
                float offset=lastExaAngle;
                p=Center+Arena.Direction(offset+MathF.PI/8+MathF.PI/4*(int)role)*17.7f;
            }
            p=ArkveldWalking.Clamp(p);
            if(personal && time<=spreadEnd+.28)
                p=SoloSpread(role,p,current,time);
            return new(p,Math.Max(time+.25,Math.Max(spreadEnd,spinEnd)),"八方分散 · 避开邻位",1.3f);
        }
        if(BladeActive(time))
        {
            bool first=FirstBladePending(time);
            var blade=BladePoint(role,first,first?bladeFirst:bladeEnd,time,current);
            return new(blade,
                Math.Max(time+.25,first?bladeFirst:bladeEnd),first?"锁刃敲打 · 第一侧":"锁刃敲打 · 穿到对侧",2){Room=ArkveldRoom.Blade};
        }
        if(time>=laserStart && time<laserStart+34 && lasers<4)
        {
            if(lasers>0 && straightLaser && time<laserHit+.95)
            {
                var axis=Arena.Direction(laserAngle);
                var normal=Right(axis);
                float side=Vector2.Dot(current-laserOrigin,normal);
                var sidestep=current+normal*((side>=0?1:-1)*Math.Max(0,4.8f-Math.Abs(side)));
                return new(ArkveldWalking.Clamp(sidestep),time+1.2,"龙闪炮 · 避开直射通道",1);
            }
            bool right=lasers is 0 or 2;
            var p=LaserPoint(right?7.0f:-8.15f,right?2.3f:2.8f);
            return new(p,time+5,lasers==0?"龙闪炮 · 提前集合诱导":"龙闪炮 · 穿到下一侧",2){Room=ArkveldRoom.Beam};
        }
        // The cracked crystals appear after the towers, before the next attack reveals
        // which lanes are safe. Hold the tower exit until the following cast supplies a route.
        if(time<towerEnd+7 && Tower(role)!=null && time>longFireEnd)
            return new(current,towerEnd+7,"六塔已结算 · 原地等候下一轮方向",.45f);
        return IdleGoal(role,time,current);
    }
    private Vector2? OuterBlade(Role role,double time,Vector2 current)
    {
        int i=(int)role;var h=Zones.Where(z=>(z.Roles & 1<<i)!=0 && z.Until>=time && z.From<=bladeEnd+.35).ToArray();
        bool Good(Vector2 p)=>HasRoom(p,role,time,bladeEnd+.35,h,.42f);
        if(outerBladePocket[i] is {} known && Good(known))return known;
        if(outerBladeChecked[i] && outerBladePocket[i]==null || !FirstBladePending(time))return null;
        outerBladeChecked[i]=true;
        var outward=-bladeSide;
        float off=Vector2.Dot(current-observedWideOrigin,outward);
        if(off<3.8f)return null;
        // Keep a distant bait on its side when that side lies outside the swept volume of BOTH parallel cleaves.
        float limit=Math.Max(0,(float)(bladeFirst-time-PlanLead))*Walking.Speed;
        float best=float.PositiveInfinity;Vector2? result=null;
        void Check(Vector2 site)
        {
            if(Vector2.Dot(site-observedWideOrigin,outward)<14.80f || !Good(site))return;
            float dist=Vector2.Distance(current,site);if(dist>limit+.1f)return;
            float cost=dist+.18f*MeleePenalty(role,site);
            if(cost<best){best=cost;result=site;}
        }
        Check(current);
        Vector2 edge=current+outward*Math.Max(0,15.0f-off);
        foreach(float push in (ReadOnlySpan<float>)[0,.85f,1.7f,2.5f])
            for(int s=-6;s<=6;s++)Check(ArkveldWalking.Clamp(edge+bladeAlong*(s*.75f)+outward*push,19.16f));
        outerBladePocket[i]=result;
        return result;
    }
    private static readonly Vector2[] spreadOptions=(from z in Enumerable.Range(-12,25) from x in Enumerable.Range(-12,25)
        let d=new Vector2(x*1.6f,z*1.6f) where d.LengthSquared()<19.25f*19.25f select Center+d).ToArray();
    private Vector2 SpinSlot(int i)=>personal || spinRound==0?BaseSpinSlot(i):spinPoints[i];
    private Vector2 BaseSpinSlot(int i)
    {
        float theta=(i<3?(i-1)*64:(i-5)*37)*MathF.PI/180;
        return ArkveldWalking.Clamp(spinOrigin+Arena.Direction(Facing(spreadAxis)+theta)*(i<3?7.3f:16.5f));
    }

    private int localSpinRole=-1,observedSpinSlot=-1;
    private double sampledSpin=-100;
    private void RecordSpinFormation(double hit)
    {
        if(!personal || localSpinRole<0 || Math.Abs(hit-spinEnd)>1.3 || hit-sampledSpin<2)return;
        sampledSpin=hit;observedSpinSlot=-1;
        var used=0;
        for(int i=0;i<8;i++)
        {
            if(i==localSpinRole)continue;
            if(PointOf(Members[i]) is not {} p)return;
            int closest=Enumerable.Range(0,8).MinBy(s=>Vector2.DistanceSquared(p,SpinSlot(s)));
            if((used & 1<<closest)!=0 || Vector2.DistanceSquared(p,SpinSlot(closest))>2.2f*2.2f)return;
            used|=1<<closest;
        }
        observedSpinSlot=Enumerable.Range(0,8).Single(s=>(used & 1<<s)==0);
    }
    private Vector2? VacantSpinSlot(Role role,Vector2 current,double now)
    {
        // Use only the seven allies' previous resolved fan. Their occupied lanes
        // rotate with the next boss facing. A loose or incomplete fan supplies no prior.
        if(observedSpinSlot<0 || sampledSpin>=spinEnd-1.3)return null;
        var expected=SpinSlot(observedSpinSlot);
        if(spreadEnd-now>2.9)return expected;
        var nearby=Members.Where(id=>id!=Members[(int)role] && PointOf(id)!=null).ToArray();
        bool Clear(Vector2 site)=>nearby.All(id=>Vector2.DistanceSquared(positions[id],site)>6.45f*6.45f ||
            Vector2.DistanceSquared(positions[id],site)>4.0f*4.0f && Vector2.DistanceSquared(Projected(id,positions[id],now,spreadEnd),site)>7.25f*7.25f);
        if(Clear(expected))return expected;
        return Enumerable.Range(0,8).Select(SpinSlot).Where(p=>Clear(p) && Vector2.Distance(p,current)<
            Walking.Speed*Math.Max(0,spreadEnd-now-.2)+.35f).OrderBy(p=>Vector2.DistanceSquared(p,current)).Select(p=>(Vector2?)p).FirstOrDefault();
    }
    private Vector2 SoloSpread(Role role,Vector2 intended,Vector2 current,double time)
    {
        ulong me=Members[(int)role];
        bool sweeping=lasers==3 && time-laserLock<14;
        // The other seven players can cross a sector while entering the safe half.
        // Use the same bounded velocity projection as the sweeping beam when choosing a free exit.
        bool halfRoom=spinEnd>=time-.2 && Math.Abs(spinEnd-spreadEnd)<2;
        var neighbours=(spreadMarks.Contains(me)?Members.AsEnumerable():spreadMarks).Where(id=>id!=me)
            .Where(id=>PointOf(id)!=null).SelectMany(id=>
            {
                var p=positions[id];var lead=sweeping || halfRoom?SpreadProjection(id,p,time,spreadEnd):Projected(id,p,time,spreadEnd);
                return Vector2.DistanceSquared(p,lead)>1?new[]{p,Vector2.Lerp(p,lead,.5f),lead}:new[]{p};
            }).ToArray();
        if(neighbours.Length==0)return intended;
        double remaining=Math.Max(.2,spreadEnd-time);
        var fixedAreas=Zones.Where(z=>z.Owner!=ActorHazard && z.From<=spreadEnd+1.4 && z.Until>=Math.Max(time,spreadEnd-.5)).ToArray();
        var runner=lasers==3 && time-laserLock<14 && remaining<2.8
            ?Members.Where(id=>id!=me && (spreadMarks.Contains(me) || spreadMarks.Contains(id)) && PointOf(id)!=null)
                .OrderBy(id=>Vector2.DistanceSquared(current,positions[id])).FirstOrDefault():0;
        var approaching=motions.GetValueOrDefault(runner);
        bool converging=runner!=0 && time-approaching.At<.8 && Vector2.DistanceSquared(current,approaching.Point)<64 &&
            Vector2.Dot(approaching.Velocity,current-approaching.Point)>6;
        Vector2 best=intended;float min=float.MaxValue;
        IEnumerable<Vector2> candidates=spreadOptions.Prepend(intended).Prepend(current);
        // A half-room cut can leave the only free spread slot between bodies and the cut line.
        // Sample along its actual safe edge; the arena grid may skip the entire opening.
        if(fixedAreas.FirstOrDefault(z=>z.Shape==Shape.Cone && z.Width is >1.55f and <1.61f) is {Shape:Shape.Cone} cut)
        {
            var inward=-Arena.Direction(cut.Angle);var across=Right(inward);
            var edge=from n in Enumerable.Range(-19,39) from depth in new[]{.85f,1.85f,3.25f}
                     let point=cut.Origin+across*n+inward*depth
                     where Vector2.DistanceSquared(Center,point)<ArkveldWalking.Radius*ArkveldWalking.Radius
                     select point;
            candidates=candidates.Concat(edge);
        }
        foreach(var p in candidates)
        {
            float near=neighbours.Min(a=>Vector2.Distance(a,p));
            float walk=Vector2.Distance(p,current);
            float cost=.28f*Vector2.Distance(p,intended)+.40f*walk+12*MathF.Max(0,9.5f-near)
                +18*MathF.Max(0,walk-(float)remaining*Walking.Speed)+100*fixedAreas.Count(a=>a.Contains(p));
            if(converging)
            {
                var path=approaching.Velocity*(float)Math.Min(remaining,1.6);
                float fraction=Math.Clamp(Vector2.Dot(p-approaching.Point,path)/Math.Max(.001f,path.LengthSquared()),0,1);
                cost+=10*Math.Max(0,9-Vector2.Distance(p,approaching.Point+path*fraction));
            }
            if(sweeping && remaining<2.1)
                cost+=9*Math.Max(0,walk-(float)Math.Max(0,remaining-PlanLead)*Walking.Speed);
            if(cost<min){min=cost;best=p;}
        }
        return best;
    }
    public Vector2? Tower(Role role)
    {
        if(tank.Count!=2 || regular.Count!=4)return null;
        return role switch {Role.MT=>tank[0],Role.ST=>tank[1],Role.H1 or Role.D1=>regular[0],
            Role.H2 or Role.D2=>regular[1],Role.D3=>regular[2],_=>regular[3]};
    }
    private Vector2 ThirdBait(Vector2 tower)
    {
        var radial=Unit(tower-Center,Vector2.UnitY);
        Vector2 best=Center+radial*19;
        float min=float.MaxValue;
        // Choose a reachable point on the rim which leaves every tower clear at the final snapshot.
        for(int k=-10;k<=10;k++)
        {
            var p=Center+Arena.Direction(Facing(radial)+k*MathF.PI/60)*19;
            float cost=Vector2.DistanceSquared(p,tower);
            foreach(var t in tank.Concat(regular))cost+=100*Math.Max(0,7.35f-Vector2.Distance(p,t));
            if(cost<min){best=p;min=cost;}
        }
        return best;
    }
    private Vector2 LaserPoint(float right,float forward)
    {
        var axis=Unit(Center-laserOrigin,Vector2.UnitY);
        return ArkveldWalking.Clamp(laserOrigin+axis*forward+Right(axis)*right);
    }
    private Vector2 PartyStackCenter(ulong own,Vector2 current,double time)
    {
        var allies=Members.Where(id=>id!=own && !unavailable.Contains(id) && PointOf(id)!=null)
            .Select(id=>Projected(id,PointOf(id)!.Value,time,singleEnd)).ToArray();
        if(allies.Length==0)return current;
        var best=current;float bestCost=float.PositiveInfinity;
        void Consider(Vector2 center)
        {
            center=ArkveldWalking.Clamp(center);
            float far=0,miss=0;
            foreach(var ally in allies)
            {
                float d=Vector2.Distance(ally,center);
                far=Math.Max(far,d);
                if(d>5.8f)miss+=6+Math.Min(4,d-5.8f);
            }
            float travel=Vector2.Distance(current,center);
            float cost=miss+far+.08f*travel;
            if(cost<bestCost){bestCost=cost;best=center;}
        }
        // The smallest circle covering the party is supported by two or three players.
        Consider(current);
        for(int i=0;i<allies.Length;i++)
        {
            var a=allies[i];Consider(a);
            for(int j=0;j<i;j++)
            {
                var b=allies[j]-a;Consider(a+b*.5f);
                for(int k=0;k<j;k++)
                {
                    var c=allies[k]-a;
                    float den=2*(b.X*c.Y-b.Y*c.X);
                    if(Math.Abs(den)<.01f)continue;
                    var offset=(Right(c)*b.LengthSquared()-Right(b)*c.LengthSquared())/den;
                    if(offset.LengthSquared()<=20*20)Consider(a+offset);
                }
            }
        }
        return best;
    }
    private Vector2 PlaceStack(Role role,Vector2 assigned,ulong other,Vector2 current,double time)
    {
        if(PointOf(other) is not {} otherAt)return assigned;
        var opposed=Projected(other,otherAt,time,dualEnd);
        var own=Members[(int)role];
        var allies=Members.Where(id=>id!=own && id!=other && !unavailable.Contains(id) && PointOf(id)!=null)
            .Select(id=>Projected(id,PointOf(id)!.Value,time,dualEnd)).ToArray();
        var danger=Zones.Where(z=>z.Snapshot>=time-.15 && z.Snapshot<=dualEnd+.65 && z.Until>=dualEnd-1.7 && (z.Roles & (1<<(int)role))!=0).ToArray();
        float bestCost=float.PositiveInfinity;var best=assigned;
        void Candidate(Vector2 p)
        {
            p=ArkveldWalking.Clamp(p);
            int available=0,overlap=0;
            foreach(var ally in allies)
                if(Vector2.DistanceSquared(ally,p)<29){if(Vector2.DistanceSquared(ally,opposed)>40)available++;else overlap++;}
            float travel=Vector2.Distance(p,current),apart=Vector2.Distance(p,opposed);
            float cost=20*Math.Max(0,2-available)+8*Math.Max(0,available-4)+16*overlap+
                24*Math.Max(0,7-apart)+3*Math.Max(0,12.6f-apart)+travel*.32f+Vector2.Distance(p,assigned)*.43f;
            float clearance=Math.Min(apart,Vector2.Distance(p,otherAt));
            cost+=85*Math.Max(0,7.5f-clearance);
            cost+=100*Math.Max(0,travel-Walking.Speed*(float)Math.Max(0,dualEnd-time-.45));
            foreach(var zone in danger)if(zone.Contains(p))cost+=90;
            if(cost<bestCost){bestCost=cost;best=p;}
        }
        Candidate(current);Candidate(assigned);
        foreach(var ally in allies)Candidate(ally);
        for(int z=-18;z<=18;z+=2)for(int x=-18;x<=18;x+=2)if(x*x+z*z<=19*19)Candidate(Center+new Vector2(x,z));
        return best;
    }
    private Vector2 FollowStack(Role role,ulong id,Vector2 real,ulong other,Vector2 current,double time)
    {
        var expected=Projected(id,real,time,dualEnd);
        var opposite=PointOf(other) is {} p?Projected(other,p,time,dualEnd):(Vector2?)null;
        var best=expected;float cost=float.PositiveInfinity;
        var lanes=Zones.Where(z=>z.Snapshot>=time-.15 && z.Snapshot<=dualEnd+.65 && z.Until>=dualEnd-1.7 && (z.Roles & (1<<(int)role))!=0).ToArray();
        void Consider(Vector2 site)
        {
            site=ArkveldWalking.Clamp(site);
            float d=Vector2.Distance(site,expected),travel=Vector2.Distance(site,current);
            if(d>5.25f)return;
            float score=travel*.30f+d*.55f+MeleePenalty(role,site)*.20f;
            if(opposite is {} avoid)score+=35*Math.Max(0,6.8f-Vector2.Distance(avoid,site));
            foreach(var z in lanes)if(z.Contains(site))score+=140;
            score+=12*Math.Max(0,travel-Walking.Speed*(float)Math.Max(0,dualEnd-time-.45));
            if(score<cost){cost=score;best=site;}
        }
        Consider(real);Consider(current);Consider(expected);
        for(int k=0;k<20;k++)
        {
            var offset=Arena.Direction(k*MathF.Tau/20);
            Consider(expected+offset*3.2f);Consider(expected+offset*4.95f);
        }
        return best;
    }
    private ulong[] OrderedMarks()=>dual.Keys.OrderBy(id=>
    {
        int index=Array.IndexOf(Members,id);
        return index<0?2:Far((Role)index)?1:0;
    }).ThenBy(id=>id).ToArray();
    private bool GroupFor(Role role,double time,Vector2 current)
    {
        var markers=OrderedMarks();var me=Members[(int)role];
        if(me==markers[0])return false;
        if(me==markers[1])return true;
        bool committed=committedGroups.TryGetValue(role,out var chosen);
        if(!committed)chosen=Far(role);
        if(!personal || dualEnd-time>3.65 || PointOf(markers[0]) is not {} a || PointOf(markers[1]) is not {} b)return chosen;
        var pa=Projected(markers[0],a,time,dualEnd);var pb=Projected(markers[1],b,time,dualEnd);
        if(Vector2.DistanceSquared(pa,pb)<65)return chosen;
        float reach=5.1f+Walking.Speed*(float)Math.Max(0,dualEnd-time-.42);
        float originalDistance=Vector2.Distance(current,chosen?pb:pa),alternateDistance=Vector2.Distance(current,chosen?pa:pb);
        if(originalDistance>reach+1 && alternateDistance<=reach && alternateDistance+4<originalDistance)
            return committedGroups[role]=!chosen;
        if(committed || Vector2.DistanceSquared(a,b)<65)return chosen;
        // Other players' observed positions can already establish a legal 3+5. Join a group
        // which keeps both legal, preferring the closer one if both work.
        int left=0,right=0;
        foreach(ulong id in Members)
        {
            if(id==me || unavailable.Contains(id) || PointOf(id) is not {} p)continue;
            bool l=Vector2.DistanceSquared(p,a)<=36,r=Vector2.DistanceSquared(p,b)<=36;
            if(l && !r)left++;if(r && !l)right++;
        }
        bool canLeft=left is >=2 and <=4 && right is >=3 and <=5;
        bool canRight=right is >=2 and <=4 && left is >=3 and <=5;
        if(canLeft && canRight)
            return committedGroups[role]=Vector2.DistanceSquared(current,b)<Vector2.DistanceSquared(current,a);
        if(canLeft || canRight)
        {
            bool far=canRight;
            // A filling group may be transient. Commit only if it is reachable and the other
            // anchor is already occupied by enough players.
            if(Vector2.Distance(current,far?b:a)<=Walking.Speed*Math.Max(0,dualEnd-time-.2)+4)
                return committedGroups[role]=far;
        }
        return Far(role);
    }
}

}
