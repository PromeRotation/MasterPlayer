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



// Each search layer holds candidate positions at one or more near-simultaneous snapshots.
// Edges are walked at the actual movement speed, including the occupied end of the previous telegraph.
internal static class ArkveldWalking
{
    private static readonly Vector2[] Field=(from z in Enumerable.Range(-10,21) from x in Enumerable.Range(-10,21)
        let p=new Vector2(2*x,2*z) where p.LengthSquared()<=Radius*Radius
        select p+Arena.Center).ToArray();
    public const float Radius=19.5f;
    public static Vector2 Clamp(Vector2 point,float radius=Radius)
    {
        var offset=point-Arena.Center;
        return offset.LengthSquared()>radius*radius?Arena.Center+Vector2.Normalize(offset)*radius:point;
    }
    private sealed record Node(Vector2 P,double Cost,Node? Parent,double Depart);
    private sealed class Wave(double start,double end)
    {
        public double Start=start,End=end;
        public readonly List<Zone> Areas=[];
    }
    public static WalkPlan Solve(Vector2 current,Role role,double time,Destination goal,IReadOnlyList<Zone> zones,float speed,double sprintUntil=double.PositiveInfinity,bool avoidPersistentCircles=false,Vector2? meleeBoss=null,float meleeRadius=0,double reactUntil=0,Vector2 footHint=default,Vector2[]? party=null,float freedom=0,double finishCastAt=0,bool overlappingRings=false)
    {
        float DistanceUntil(double start,double end)
        {
            double elapsed=Math.Max(0,end-start);
            double boosted=Math.Clamp(sprintUntil-start,0,elapsed);
            return (float)(boosted*speed+(elapsed-boosted)*Walking.Speed);
        }
        double Arrival(double depart,float distance)
        {
            double boosted=Math.Max(0,sprintUntil-depart);
            if(distance<=boosted*speed)return depart+distance/speed;
            return depart+boosted+(distance-boosted*speed)/Walking.Speed;
        }
        var target=Clamp(goal.Point,goal.FieldRadius);
        var constraints=goal.Required is {} required?zones.Append(required):zones;
        var incoming=constraints.Where(z=>z.Until>=time && z.From<=time+7 && (z.Roles & (1<<(int)role))!=0)
            .Select(z=>avoidPersistentCircles && z.From<time?z with {From=time}:z).OrderBy(z=>z.From).ToArray();
        bool onTime=!goal.Unavailable && Vector2.Distance(current,target)<=DistanceUntil(time,goal.Deadline)+goal.Radius;
        string name=onTime || goal.Unavailable?goal.Name:goal.Name+" · 预计迟到";
        bool leavingMelee=goal.Room!=ArkveldRoom.Blade && meleeBoss is {} centerNow && Vector2.Distance(current,centerNow)<=meleeRadius &&
            Vector2.Distance(target,centerNow)>meleeRadius;
        double directDeparture=leavingMelee?Math.Max(time,goal.Deadline-Vector2.Distance(current,target)/Walking.Speed-.12):time;
        if(reactUntil>time)
        {
            double lastSafe=goal.Deadline-Vector2.Distance(current,target)/Walking.Speed-.45;
            directDeparture=Math.Max(directDeparture,Math.Min(reactUntil,lastSafe));
        }

        var waves=new List<Wave>();
        foreach(var z in incoming)
        {
            if(waves.Count==0 || z.From>waves[^1].Start+.18)waves.Add(new(z.From,z.Until));
            var w=waves[^1];w.End=Math.Max(w.End,z.Until);w.Areas.Add(z);
        }
        bool SafeSegment(Vector2 start,Vector2 end,double depart,double until)
        {
            float distance=Vector2.Distance(start,end);
            foreach(var area in incoming)
                if(!Clear(area,start,end,depart,until,distance))return false;
            return true;
        }
        bool Clear(Zone area,Vector2 start,Vector2 end,double depart,double until,float distance)
        {
            double begin=Math.Max(depart,area.From),finish=Math.Min(until,area.Until);
            if(begin>finish)return true;
            if(distance<.0001)return !area.Contains(start);
            float f=Math.Clamp(DistanceUntil(depart,begin)/distance,0,1),l=Math.Clamp(DistanceUntil(depart,finish)/distance,0,1);
            if(area.Shape==Shape.Circle)
            {
                var path=end-start;
                float closest=Math.Clamp(Vector2.Dot(area.Origin-start,path)/(distance*distance),f,l);
                return !area.Contains(start+path*closest);
            }
            int steps=Math.Max(1,(int)Math.Ceiling((l-f)*distance/.24));
            for(int j=0;j<=steps;j++)
                if(area.Contains(Vector2.Lerp(start,end,(float)(f+(l-f)*j/steps))))return false;
            return true;
        }
        double horizon=Math.Max(time+2.7,Math.Min(time+7,goal.Deadline));
        if(incoming.Any(z=>z.Snapshot.HasValue))horizon=Math.Max(horizon,incoming.Max(z=>z.Until));
        // A pickup is consumed on contact. The next leg can leave before a later
        // wave crosses it; holding there until every future snapshot invents a detour.
        double directHorizon=goal.ContactOnly?Arrival(time,Vector2.Distance(current,target))+.65:horizon;
        float Crowding(Vector2 p)
        {
            float cost=0;if(party!=null)foreach(var ally in party)
            {
                float gap=Vector2.DistanceSquared(ally,p);
                if(gap<5.29f){float shortfall=2.3f-MathF.Sqrt(gap);cost+=shortfall*shortfall;}
            }
            return cost;
        }
        if(freedom>0 && party is {Length:>0} && Crowding(target)>.18f)
        {
            Vector2 original=target;
            float score=Crowding(original)*1.6f;
            float seed=footHint==Vector2.Zero?(int)role*.71f:MathF.Atan2(footHint.X,footHint.Y);
            foreach(float len in (ReadOnlySpan<float>)[.48f,.79f,1.0f])
            for(int i=0;i<12;i++)
            {
                var p=Clamp(original+Arena.Direction(seed+i*MathF.Tau/12)*(freedom*len),goal.FieldRadius);
                float extra=Vector2.Distance(current,p)-Vector2.Distance(current,original);
                if(extra>.1f && Vector2.Distance(current,p)>DistanceUntil(time,goal.Deadline-.28)+.2f)continue;
                float cost=1.6f*Crowding(p)+.42f*Vector2.Distance(original,p)+.14f*Math.Max(0,extra);
                if(cost>=score || !SafeSegment(current,p,time,directHorizon))continue;
                score=cost;target=p;
            }
            if(target!=original)
            {
                onTime=!goal.Unavailable && Vector2.Distance(current,target)<=DistanceUntil(time,goal.Deadline)+goal.Radius;
                directDeparture=Math.Min(directDeparture,Math.Max(time,goal.Deadline-Vector2.Distance(current,target)/Walking.Speed-.45));
            }
        }
        // Protect a cast only if the entire route stays safe and the duty's timing reserve remains.
        double AfterCast(Vector2 destination,double departure,double horizonEnd)
        {
            if(!onTime || goal.ContactOnly || finishCastAt<=time || finishCastAt>time+8)return departure;
            double snapshot=incoming.Where(z=>z.Snapshot>=time).Select(z=>z.Snapshot!.Value)
                .DefaultIfEmpty(double.NegativeInfinity).Min();
            double after=Math.Max(departure,finishCastAt);
            double arrive=Arrival(after,Vector2.Distance(current,destination));
            return arrive<=Math.Min(goal.Deadline,snapshot)-Arkveld.PlanLead &&
                   SafeSegment(current,current,time,after) && SafeSegment(current,destination,after,Math.Max(horizonEnd,arrive+.35))
                ? after : departure;
        }
        // Find how many successive ring impacts one reachable pocket covers. A later wave
        // must not leave a player parked on the boundary of an earlier one.
        if(overlappingRings)
        {
            var impacts=incoming.Where(z=>z.Shape==Shape.Ring && z.Snapshot>=time+.04 && z.Snapshot<=time+5)
                .Select(z=>z.Snapshot!.Value).Order().ToArray();
            var volleys=new List<double>();
            foreach(double hit in impacts)
                if(volleys.Count==0 || hit-volleys[^1]>.38)volleys.Add(hit);else volleys[^1]=hit;
            for(int w=volleys.Count-1;w>=0;w--)
            {
                double until=volleys[w]+.27;float best=float.PositiveInfinity;Vector2? pocket=null;
                foreach(var p in Field.Append(target).Append(current))
                {
                    float distance=Vector2.Distance(current,p);
                    if(Arrival(time,distance)>until-.48)continue;
                    float cost=distance*.64f+Vector2.Distance(p,target)*.42f;
                    if(cost>=best || !SafeSegment(current,p,time,until))continue;
                    if(incoming.Any(z=>z.Snapshot is {} shot && shot>=time && shot<=until &&
                        Arrival(time,distance)<=shot-.20 && (z.Shape==Shape.Circle || z.Shape==Shape.Ring) &&
                        (z.Contains(p+Vector2.UnitX*.40f) || z.Contains(p-Vector2.UnitX*.40f) ||
                         z.Contains(p+Vector2.UnitY*.40f) || z.Contains(p-Vector2.UnitY*.40f))))continue;
                    best=cost;pocket=p;
                }
                if(pocket is {} safe)return new(safe,[safe],true,name,time);
            }
        }
        if(goal.Room==ArkveldRoom.Rush && incoming.Any(z=>z.Shape==Shape.Ring && z.Contains(current) && z.From<time+3.5) &&
            !SafeSegment(current,target,time,directHorizon))
        {
            var end=incoming.Where(z=>z.From<=time+3.55).Max(z=>z.Until);
            float best=float.PositiveInfinity;Vector2? shelter=null;double leave=time;
            foreach(var p in Field)
            {
                float dist=Vector2.Distance(current,p);
                if(dist>17 || Vector2.DistanceSquared(p,target)>155)continue;
                foreach(double delay in (ReadOnlySpan<double>)[0,.25,.55,.85,1.15])
                {
                    double start=time+delay,arrival=Arrival(start,dist);
                    float cost=Vector2.Distance(p,target)+.32f*dist+.6f*(float)delay;
                    if(arrival>end || cost>=best || !SafeSegment(current,current,time,start) || !SafeSegment(current,p,start,end))continue;
                    if(incoming.Any(z=>z.From<=end && z.Until>=arrival && z.Shape is Shape.Ring or Shape.Circle &&
                        (z.Contains(p+Vector2.UnitX*.45f) || z.Contains(p-Vector2.UnitX*.45f) ||
                         z.Contains(p+Vector2.UnitY*.45f) || z.Contains(p-Vector2.UnitY*.45f))))continue;
                    best=cost;shelter=p;leave=start;break;
                }
            }
            if(shelter is {} refuge)return new(refuge,[refuge],true,name,leave);
        }
        if(goal.Room==ArkveldRoom.Blade && goal.Deadline>=time && incoming.Length>0)
        {
            // Crossing for the second cleave must keep making progress. A later narrow blade
            // can require a different exit along the safe half, while the named anchor is blocked.
            double until=goal.Deadline+.25;
            var intended=target;float best=float.PositiveInfinity;Vector2? exit=null;
            void TryExit(Vector2 p)
            {
                float d=Vector2.Distance(current,p);
                double arrival=Arrival(time,d);
                if(arrival>goal.Deadline-.26 || !SafeSegment(current,p,time,until))return;
                if(incoming.Any(z=>z.Snapshot is {} hit && Math.Abs(hit-goal.Deadline)<1 &&
                    (z.Contains(p) || z.Contains(p+Vector2.UnitX*.45f) || z.Contains(p-Vector2.UnitX*.45f) ||
                    z.Contains(p+Vector2.UnitY*.45f) || z.Contains(p-Vector2.UnitY*.45f))))return;
                float score=Vector2.Distance(p,intended)+.06f*d;
                if(score<best){best=score;exit=p;}
            }
            TryExit(intended);
            if(!exit.HasValue)foreach(var p in Field)if(Vector2.DistanceSquared(p,intended)<92)TryExit(p);
            if(exit is {} point)
            {
                double depart=directDeparture;
                if(depart>time && (!SafeSegment(current,current,time,depart) || !SafeSegment(current,point,depart,until)))depart=time;
                return new(point,[point],true,name,AfterCast(point,depart,until));
            }
        }
        // Only a proven direct route may extend the wait for a real cast. Include the
        // whole crossing, both snapshots, expiring Sprint and one live planning tick.
        if(finishCastAt>time)
        {
            double depart=AfterCast(target,directDeparture,directHorizon);
            if(depart>=finishCastAt && depart>directDeparture)return new(target,[target],true,name,depart);
        }
        if(incoming.Length==0)return new(target,[target],onTime,name,directDeparture);
        if(SafeSegment(current,target,time,directHorizon))
        {
            double departure=time;
            if(directDeparture>time && SafeSegment(current,current,time,directDeparture) &&
                SafeSegment(current,target,directDeparture,Math.Max(directDeparture,horizon)))departure=directDeparture;
            return new(target,[target],onTime,name,departure);
        }
        // With a circle already active the first snapshot has zero travel time.
        // Try a bend around that circle now, while there is still time to cross.
        bool NeedsBend(Zone z)=>z.Owner==Arkveld.GroundBait || avoidPersistentCircles && z.Shape==Shape.Circle && z.Until-time>1;
        if(onTime && incoming.Any(NeedsBend))
        {
            Vector2? bend=null;
            double fastest=double.PositiveInfinity;
            foreach(var circle in incoming.Where(NeedsBend))
            {
                for(int i=0;i<24;i++)
                {
                    var p=Clamp(circle.Origin+Arena.Direction(i*MathF.Tau/24)*(circle.Length+1.25f));
                    double turnAt=Arrival(time,Vector2.Distance(current,p));
                    double finish=Arrival(turnAt,Vector2.Distance(p,target));
                    if(finish>=fastest || finish>goal.Deadline+.03 || !SafeSegment(current,p,time,turnAt) ||
                        !SafeSegment(p,target,turnAt,Math.Max(horizon,finish)))continue;
                    fastest=finish;bend=p;
                }
            }
            if(bend is {} via)return new(target,[via,target],true,name);
        }
        var points=new List<Vector2>(Field.Length+24){target,current};
        if(incoming.Any(z=>z.Snapshot.HasValue && z.Shape==Shape.Line))
        {
            var mid=Vector2.Lerp(current,target,.5f);
            for(int i=1;i<16;i++)points.Add(Vector2.Lerp(current,target,i/16f));
            foreach(var z in incoming.Where(z=>z.Shape==Shape.Line))
            {
                var axis=Arena.Direction(z.Angle);var normal=new Vector2(axis.Y,-axis.X);
                foreach(var origin in new[]{current,mid,target})
                {
                    var line=origin-normal*Vector2.Dot(origin-z.Origin,normal);
                    points.Add(Clamp(line+normal*(z.Width+.35f)));
                    points.Add(Clamp(line-normal*(z.Width+.35f)));
                }
            }
        }
        if(meleeBoss is {} center)
            for(int i=0;i<32;i++)points.Add(Clamp(center+Arena.Direction(i*MathF.Tau/32)*(meleeRadius-.35f)));
        if(Vector2.DistanceSquared(target,current)>.05f)
        {
            var towards=Vector2.Normalize(target-current);
            foreach(var w in waves)
            {
                var length=Math.Min(Vector2.Distance(current,target),DistanceUntil(time,w.Start-.035));
                points.Add(current+towards*length);
            }
        }
        // Short exact steps beside a narrow lane or the arena edge can be lost to a 2y grid.
        for(int i=0;i<8;i++)points.Add(Clamp(current+Arena.Direction(i*MathF.PI/4)*.9f));
        foreach(var z in incoming)
        {
            if(z.Shape==Shape.Cone && z.Contains(current))
            {
                var q=current-z.Origin;
                foreach(int side in new[]{-1,1})
                {
                    var ray=Arena.Direction(z.Angle+side*(z.Width+.025f));
                    float along=Math.Max(0,Vector2.Dot(q,ray));
                    var edge=z.Origin+ray*along;
                    points.Add(Clamp(edge+(edge-current)/Math.Max(.01f,Vector2.Distance(edge,current))*.55f));
                }
            }
            else if(z.Shape==Shape.Circle && z.Contains(current))
            {
                var offset=current-z.Origin;
                if(offset.LengthSquared()>.01f)points.Add(Clamp(z.Origin+Vector2.Normalize(offset)*(z.Length+.45f)));
            }
        }
        points.AddRange(Field);
        HashSet<Vector2>? pockets=null;
        if(footHint!=Vector2.Zero)
        {
            pockets=new HashSet<Vector2>();
            var alternatives=new List<Vector2>(points.Count);
            foreach(var p in points)
            {
                if(p==current)continue;
                var q=Clamp(p+footHint);
                if(pockets.Add(q))alternatives.Add(q);
            }
            points.InsertRange(0,alternatives);
            pockets.Add(target);
            pockets.Add(current);
        }
        var states=new List<Node>{new(current,0,null,time)};
        double previous=time;
        static bool Safe(Vector2 p,List<Zone> areas)=>!areas.Any(z=>z.Contains(p));
        int limit=Math.Min(8,waves.Count);
        for(int k=0;k<limit;k++)
        {
            var w=waves[k];float travel=DistanceUntil(previous,w.Start);
            var next=new List<Node>();
            foreach(var p in points)
            {
                if(Vector2.DistanceSquared(p,Arena.Center)>Radius*Radius+.01f || !Safe(p,w.Areas))continue;
                float localCrowd=Crowding(p);
                Node? best=null;double least=double.PositiveInfinity;
                foreach(var n in states)
                {
                    float distance=Vector2.Distance(p,n.P);
                    if(distance>travel+.035f)continue;
                    // Try departure at the previous snapshot, then after it finishes.
                    double cost=n.Cost+distance*.17+Vector2.Distance(p,target)*(k==0?1:.65)+.82f*localCrowd+(pockets!=null && !pockets.Contains(p)?1.65:0);
                    if(footHint!=Vector2.Zero && goal.Room is (ArkveldRoom.Cross or ArkveldRoom.Rush or ArkveldRoom.Tail or ArkveldRoom.Flight) && distance is >.05f and <5.18f)
                        cost+=k==0?2.2f:1.0f;
                    if(cost>=least)continue;
                    double depart=previous;
                    if(!SafeSegment(n.P,p,depart,Math.Max(depart,w.Start)))
                    {
                        if(k==0)continue;
                        depart=Math.Max(depart,waves[k-1].End+.015);
                        // A known fire step stops blocking immediately after its own resolution.
                        // A wait is valid only if both the waiting point and the entire next leg are clear.
                        if(Arrival(depart,distance)>w.Start+.005 || !SafeSegment(n.P,n.P,previous,depart) ||
                            !SafeSegment(n.P,p,depart,w.Start))continue;
                    }
                    least=cost;best=new(p,cost,n,depart);
                }
                if(best is {} found)next.Add(found);
            }
            if(next.Count==0)
            {
                if(k>0)break;
                // Late packet / already in damage. Minimize remaining time exposed and keep walking.
                var clear=points.Where(p=>Safe(p,w.Areas)).ToArray();
                double EmergencyCost(Vector2 p)
                {
                    float d=Vector2.Distance(p,current);
                    // Prefer an exit which does not introduce another hit on the way.
                    int entered=incoming.Count(a=>!a.Contains(current) && !Clear(a,current,p,time,Arrival(time,d),d));
                    return entered*1000+d+(avoidPersistentCircles?.85f:.16f)*Vector2.Distance(p,target)+(pockets!=null && !pockets.Contains(p)?.60:0);
                }
                var safe=clear.Length==0?current:clear.MinBy(EmergencyCost);
                return new(target,[safe],false,goal.Name+" · 紧急避险");
            }
            next.Sort((a,b)=>a.Cost.CompareTo(b.Cost));
            states=next.Take(40).ToList();
            previous=Math.Max(previous,w.Start);
        }
        var path=new List<Vector2>();var departures=new List<double>();
        for(var n=states.MinBy(n=>n.Cost);n?.Parent!=null;n=n.Parent)
        {
            if(Vector2.DistanceSquared(n.P,n.Parent.P)<.0001f)continue;
            path.Add(n.P);departures.Add(n.Depart);
        }
        path.Reverse();departures.Reverse();
        if(leavingMelee && path.Count>0 && limit>0)
        {
            // Only delay the first leg. Later steps already have their own safe departure
            // windows. Check the wait and this segment against every intervening hit.
            double latest=waves[0].Start-Vector2.Distance(current,path[0])/Walking.Speed-.12;
            if(latest>time && latest>departures[0] && SafeSegment(current,current,time,latest) &&
                SafeSegment(current,path[0],latest,waves[0].Start))departures[0]=latest;
        }
        return new(target,path.ToArray(),onTime,name){Departures=departures.ToArray()};
    }
}

}
