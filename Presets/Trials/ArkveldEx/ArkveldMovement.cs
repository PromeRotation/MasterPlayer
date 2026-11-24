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
    private static float Bearing(Vector2 p)
    {
        float a=MathF.Atan2(Center.X-p.X,Center.Y-p.Y);
        return a<0?a+MathF.Tau:a;
    }

    private void SortTowers()
    {
        // Measured formations have one ordinary tower per quadrant. Counterclockwise
        // from north gives NW, SW, SE, NE regardless of helper ID or packet order.
        regular.Sort((a,b)=>
        {
            int order=Bearing(a).CompareTo(Bearing(b));
            return order!=0?order:Vector2.DistanceSquared(a,Center).CompareTo(Vector2.DistanceSquared(b,Center));
        });
        // The two tank towers are offset slightly from the cardinal axis.
        // The dominant axis determines whether MT takes west or north.
        tank.Sort((a,b)=>MathF.Abs(a.X-b.X)>MathF.Abs(a.Y-b.Y)?a.X.CompareTo(b.X):a.Y.CompareTo(b.Y));
    }

    private Destination TowerEntry(Role role,Vector2 tower,Vector2 current,double time)
    {
        float radius=role<=Role.ST?4:2;
        string label=role<=Role.ST?"六塔 · 坦克进塔":"六塔 · 进本职能塔";
        var danger=Zones.Where(z=>z.From<=towerEnd+.35 && z.Until>=towerEnd-PlanLead &&
            (z.Roles & 1<<(int)role)!=0).ToArray();
        bool Safe(Vector2 p)=>Vector2.DistanceSquared(p,Center)<=ArkveldWalking.Radius*ArkveldWalking.Radius &&
            !danger.Any(z=>z.Contains(p));
        var entry=tower;
        bool blocked=!Safe(tower);
        if(blocked || WantsMelee(role) && EdgeDistance(tower)>MeleeLimit)
        {
            float best=blocked?float.MaxValue:MeleePenalty(role,tower)*4+Vector2.Distance(current,tower)*.1f;
            for(int ring=1;ring<=4;ring++)
            for(int i=0;i<32;i++)
            {
                var p=tower+Arena.Direction(i*MathF.Tau/32)*((radius-.22f)*ring/4);
                if(!Safe(p))continue;
                float cost=WantsMelee(role)?4*MeleePenalty(role,p)+.1f*Vector2.Distance(current,p)+.15f*Vector2.Distance(tower,p):
                    Vector2.Distance(p,current)+.55f*Vector2.Distance(p,tower);
                if(cost<best){best=cost;entry=p;}
            }
            if(best==float.MaxValue)
            {
                bool covered=danger.Any(z=>z.Owner==GroundBait && Vector2.Distance(z.Origin,tower)+radius<=z.Length-.32f);
                return new(tower,towerEnd,label+(covered?" · 黄圈覆盖全塔，只能避险":" · 塔内未找到安全落点，只能避险")){Unavailable=true};
            }
            if(blocked)label+=" · 从安全侧进塔";
        }
        if(!blocked && entry==tower && role is Role.H1 or Role.H2 or Role.D1 or Role.D2)
        {
            var across=Right(Unit(tower-Center,Vector2.UnitY))*(role is Role.H1 or Role.H2?.65f:-.65f);
            if(Safe(tower+across) && Safe(tower+across*1.4f) && (!WantsMelee(role) || EdgeDistance(tower+across)<=MeleeLimit))entry=tower+across;
        }
        return new(entry,Math.Max(time+.15,towerEnd),label,.22f)
        {
            Required=new(Shape.OutsideCircle,tower,0,radius-.12f,0,towerEnd-PlanLead,towerEnd+.15){Snapshot=towerEnd}
        };
    }

    private Vector2 FlightGap(Vector2 preferred,Vector2 current,double time,Vector2 front,float maxOffset,bool stacking=false)
    {
        // A bait can be crossed before its snapshot. Check its lifetime against the
        // route and the time spent waiting at the next staging point separately.
        double until=time+Math.Clamp(Vector2.Distance(current,preferred)/Walking.Speed+.8,2.2,4.8);
        var circles=Zones.Where(z=>z.Owner==GroundBait && z.Until>=time && z.From<=until).ToArray();
        if(!circles.Any(z=>z.Contains(preferred)))return preferred;
        var best=preferred;float score=float.MaxValue;
        // When a group stack is pending, search its rim as well as the field grid.
        var options=stacking?Enumerable.Range(0,32).Select(i=>preferred+Arena.Direction(i*MathF.Tau/32)*maxOffset).Concat(spreadOptions):spreadOptions;
        foreach(var p in options)
        {
            var d=p-preferred;
            if(d.LengthSquared()>maxOffset*maxOffset+.01f || Vector2.DistanceSquared(p,Center)>ArkveldWalking.Radius*ArkveldWalking.Radius || circles.Any(z=>z.Contains(p)))continue;
            float cost=d.Length()+.25f*Vector2.Distance(current,p)+.6f*Math.Max(0,Vector2.Dot(d,front));
            if(cost<score){score=cost;best=p;}
        }
        return best;
    }

    private Vector2 ForecastLanding(int number,double time,ulong observer)
    {
        var expected=NumberPoint(number);
        double remaining=firstImpact+(number-1)*3.09-time;
        ulong id=numbers[number-1];
        if(remaining>5.5)return expected;
        if(PointOf(id) is not {} p)
        {
            if(id==0 || !unavailable.Contains(id))return expected;
            // A dead number can be redirected to a surviving player. Use the
            // occupied lane as an estimate until the actual jump packet arrives.
            return Members.Where(member=>member!=observer).Select(PointOf).OfType<Vector2>().OrderBy(q=>Vector2.DistanceSquared(q,expected)).FirstOrDefault(expected);
        }
        var radial=p-Center;
        if(remaining<.55)return p;
        if(radial.LengthSquared()<8*8)return expected;
        var direction=Vector2.Normalize(radial);
        if(Vector2.Dot(direction,Unit(expected-Center,direction))>.82f)return expected;
        // Use a committed wrong lane early enough to cross the coming half-room.
        // Movement tangential to the rim should not extrapolate the bait off the field.
        var velocity=motions.GetValueOrDefault(id);
        float outward=time-velocity.At<.8?Math.Max(0,Vector2.Dot(velocity.Velocity,direction)):0;
        float radius=Math.Min(18.8f,radial.Length()+outward*(float)Math.Min(remaining,1.4));
        return Center+direction*radius;
    }

    private Vector2? dispatchedBait;
    private int dispatchedNumber;
    public Vector2 CurrentBaitPoint(int number)=>number==dispatchedNumber && dispatchedBait is {} point?point:NumberPoint(number);

    private Vector2 ChooseBait(int number,Vector2 from,float? priorCleave,double time,Vector2 current)
    {
        var standard=NumberPoint(number);
        if(!personal)return standard;
        ulong actor=numbers[number-1];
        double left=Math.Max(0,firstImpact+(number-1)*3.09-time);
        var others=new List<Vector2>();
        foreach(ulong id in Members)
        {
            if(id==actor || PointOf(id) is not {} point)continue;
            others.Add(point);
            if(motions.TryGetValue(id,out var motion) && time-motion.At<.9)
            {
                float lead=(float)Math.Clamp(left+time-motion.At,0,2.0);
                others.Add(ArkveldWalking.Clamp(point+motion.Velocity*lead));
            }
        }
        var radius=standard-Center;
        bool wrongSide=number>1 && Vector2.DistanceSquared(from,NumberPoint(number-1))>8*8;
        float clearance=dispatchedNumber==number && dispatchedBait is {} currentBait &&
            Vector2.DistanceSquared(currentBait,standard)>9?12:9.5f;
        float best=float.MaxValue;
        var chosen=standard;
        void Consider(Vector2 p)
        {
            var leg=p-from;
            var outwards=Right(Unit(leg,Unit(radius,Vector2.UnitY)))*turn;
            // The previous cleave resolves about one second before our jump.
            float prior=priorCleave is {} a?Vector2.Dot(leg,Arena.Direction(a)):0;
            float intoParty=number==1 && priorCleave==null?-2:Vector2.Dot(Center-p,outwards);
            float score=Vector2.Distance(p,standard)+2*Math.Abs(Vector2.Distance(p,Center)-18)
                +16*Math.Max(0,prior-2)
                +12*Math.Max(0,intoParty+2)
                +12*Math.Max(0,(wrongSide?6.6f:10f)-leg.Length());
            if(others.Count>0)score+=13*Math.Max(0,clearance-others.Min(q=>Vector2.Distance(q,p)));
            score+=18*Math.Max(0,Vector2.Distance(current,p)-(float)Math.Max(0,left-PlanLead)*Walking.Speed-.4f);
            if(dispatchedNumber==number && dispatchedBait is {} last)score+=.85f*Vector2.Distance(p,last);
            if(score<best){best=score;chosen=p;}
        }
        for(int k=0;k<48;k++)
            Consider(k==0?standard:Center+Arena.Direction(Facing(radius)+k*MathF.Tau/48)*18);
        // A late-moving teammate can occupy the chosen edge. Take a reachable
        // step away while keeping our landing on the rim and the cleave facing out.
        if(left<2.6 && Vector2.DistanceSquared(current,Center)>9*9 &&
            (others.Any(p=>Vector2.DistanceSquared(p,chosen)<100) || Vector2.Distance(current,chosen)>Math.Max(0,left-PlanLead)*Walking.Speed))
        {
            float step=(float)Math.Min(6.5,Math.Max(0,left-PlanLead)*Walking.Speed);
            float minimumRadius=Vector2.Distance(chosen,standard)>3?13.5f:15.5f;
            for(int k=0;k<24;k++)
            {
                var p=ArkveldWalking.Clamp(current+Arena.Direction(k*MathF.Tau/24)*step);
                if(Vector2.DistanceSquared(p,Center)>minimumRadius*minimumRadius)Consider(p);
            }
        }
        return chosen;
    }

    private void ChaseForecast(Role role,double time,Vector2 current)
    {
        Vector2 from=lastLanding;
        float? prior=jumped>0?LastCleave:null;
        for(int n=jumped+1;n<=Math.Min(jumped+2,8);n++)
        {
            double at=firstImpact+3.09*(n-1);
            bool mine=Members[(int)role]!=0 && numbers[n-1]==Members[(int)role];
            var p=mine?ChooseBait(n,from,prior,time,current):personal?ForecastLanding(n,time,Members[(int)role]):NumberPoint(n);
            if(!mine)Circle(p,6,at,ActorHazard);
            if(personal && at-time<=5.5 && Vector2.DistanceSquared(p,from)>2)
            {
                float aim=Facing(p-from)+turn*MathF.PI/2;prior=aim;
                Hazard(Shape.Cone,p,aim,60,MathF.PI/2+.05f,at+2.02,ActorHazard);
            }
            from=p;
        }
    }

    private Destination ChaseGoal(Role role,double time,Vector2 current)
    {
        int n=Number(Members[(int)role]);
        if(n<=0 || turn==0)return new(GroupPocket(role,Center,.7f),time+4,"麻将 · 等点名集合");
        if(n>jumped)
        {
            double hit=firstImpact+(n-1)*3.09;
            var from=n==jumped+1?lastLanding:n==jumped+2 && personal?ForecastLanding(n-1,time,Members[(int)role]):NumberPoint(n-1);
            float? prior=n==jumped+1 && jumped>0?LastCleave:n==jumped+2?Facing(from-lastLanding)+turn*MathF.PI/2:null;
            var node=ChooseBait(n,from,prior,time,current);
            bool alternate=Vector2.DistanceSquared(node,NumberPoint(n))>9;
            float distance=Math.Max(18,Vector2.Distance(current,node));
            double travel=distance/Walking.Speed+(alternate?2.0:1.45)+(personal?PlanLead:0);
            if(time>=hit-travel && (n==1 || time>=numberStart+1))
            {
                // The first jump impact can follow the visual cast by several tenths.
                // Hold a reached, clear bait until the real jump, while still reacting
                // if another player actually enters its circle.
                if(personal && n==jumped+1 && dispatchedNumber==n && dispatchedBait is {} locked &&
                    time>=hit-PlanLead && Vector2.DistanceSquared(current,locked)<1 &&
                    !Members.Any(id=>id!=Members[(int)role] && PointOf(id) is {} p &&
                        (Vector2.DistanceSquared(p,locked)<6.6f*6.6f || Vector2.DistanceSquared(Projected(id,p,time,Math.Max(time+.18,hit)),locked)<6.6f*6.6f)))
                    node=locked;
                if(personal){dispatchedNumber=n;dispatchedBait=node;}
                alternate=Vector2.DistanceSquared(node,NumberPoint(n))>9;
                var goal=new Destination(node,Math.Max(hit,time+.2),alternate?$"麻将 {n} 号 · 随前号改侧引导":$"麻将 {n} 号 · 出列引导",.8f);
                if(personal && n==jumped+1 && Vector2.DistanceSquared(current,node)<=1)
                    goal=goal with {Required=new(Shape.OutsideCircle,node,0,.55f,0,hit-SafetyLead,Math.Max(hit+.40,time+.12)){Snapshot=hit}};
                return goal;
            }
        }
        return new(Center,time+4.5,n<=jumped?$"麻将 {n} 号 · 回中":"麻将 · 中央待命",1.5f){Room=ArkveldRoom.Number};
    }
}

}
