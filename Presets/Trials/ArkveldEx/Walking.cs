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



// Candidates are evaluated on the walking clock. A path must be safe at every sample
// and its destination must stay usable through the next relevant snapshot.
internal static class Walking
{
    internal const float Speed = 6f;
    internal const float SprintSpeed = Speed*1.3f;
    private static readonly Vector2[] snakeField = (from z in Enumerable.Range(0,15)
                                               from x in Enumerable.Range(0,20)
                                               select new Vector2(81+2*x,86+2*z)).ToArray();

    public static WalkPlan Solve(Vector2 start, Role role, double now, Destination goal, IReadOnlyList<Zone> all, float speed = Speed)
    {
        if (!float.IsFinite(start.X+start.Y) || speed <= 0) return new(start,[],false,"位置或速度不可用");
        var zones = all.Where(z => (z.Roles & (1 << (int)role)) != 0 && z.Until >= now && z.From <= now+12).ToArray();
        var field = snakeField;
        var preferred = Arena.Clamp(goal.Point);
        double horizon = Math.Min(now+8,goal.Deadline);
        bool InTime(double t) => t <= goal.Deadline + .05;
        bool CanHold(Vector2 p,double arrived) => !zones.Any(z => z.Until >= arrived && z.From <= Math.Max(horizon,arrived+.35) && z.Contains(p));
        bool Clear(Vector2 a,Vector2 b,double depart)
        {
            float distance = Vector2.Distance(a,b);
            double duration=distance/speed;
            foreach(var z in zones)
            {
                double first=Math.Max(depart,z.From), last=Math.Min(depart+duration,z.Until);
                if(last<first) continue;
                if(duration<.00001) {if(z.Contains(a)) return false;continue;}
                int samples=Math.Max(1,(int)Math.Ceiling((last-first)*speed/.20));
                for(int i=0;i<=samples;i++)
                {
                    float f=(float)((first-depart+(last-first)*i/samples)/duration);
                    if(z.Contains(Vector2.Lerp(a,b,f))) return false;
                }
            }
            return true;
        }
        double Arrival(Vector2 a,Vector2 b,double depart) => depart+Vector2.Distance(a,b)/speed;
        bool Accept(Vector2 target,double arrive) => InTime(arrive) && CanHold(target,arrive);
        var candidates = new List<Vector2>{ preferred };
        if(Arena.Inside(start) && Vector2.Distance(start,preferred)<=goal.Radius) candidates.Add(start);
        foreach(var p in field)
            if(Vector2.DistanceSquared(p,preferred)<=goal.Radius*goal.Radius) candidates.Add(p);
        candidates.Sort((a,b)=>Cost(a).CompareTo(Cost(b)));
        float Cost(Vector2 p) => Vector2.Distance(p,preferred)+.13f*Vector2.Distance(p,start);
        foreach(var p in candidates)
            if(Accept(p,Arrival(start,p,now)) && Clear(start,p,now)) return new(p,[p],true,goal.Name);

        // Detours and delayed departures allow crossing a telegraph after it resolves.
        var bends = field.OrderBy(p=>Vector2.Distance(p,start)+Vector2.Distance(p,preferred)).ToArray();
        var waits = zones.Where(z=>z.Until>now && z.Until<now+6).Select(z=>z.Until+.08).Distinct().Order().ToArray();
        foreach(var p in candidates)
        {
            foreach(var depart in waits)
                if(!zones.Any(z=>z.From<=depart && z.Until>=now && z.Contains(start)) &&
                    Accept(p,Arrival(start,p,depart)) && Clear(start,p,depart)) return new(p,[p],true,goal.Name,depart);
            foreach(var bend in bends)
            {
                double at=Arrival(start,bend,now), end=Arrival(bend,p,at);
                if(!Accept(p,end) || !Clear(start,bend,now) || !Clear(bend,p,at)) continue;
                return new(p,[bend,p],true,goal.Name);
            }
        }
        // Multiple simultaneous AOEs can require a zigzag. Search adjacent grid cells
        // on a walking clock, including the option to hold at a safe intermediate cell.
        if(zones.Length>0 && InTime(Arrival(start,preferred,now)-goal.Radius/speed))
        {
            var alternate=GridRoute();
            if(alternate!=null) return alternate;
        }
        WalkPlan? GridRoute()
        {
            double end=Math.Min(now+12,goal.Deadline+.05);
            const double tick=1d/3; // cardinal step at base speed
            var open=new PriorityQueue<(int Cell,int Tick),double>();
            var parent=new Dictionary<(int Cell,int Tick),(int Cell,int Tick)>();
            var first=(-1,0);
            open.Enqueue(first,0);
            parent[first]=first;
            int expanded=0;
            while(open.TryDequeue(out var state,out _) && expanded++<3600)
            {
                var point=state.Cell<0?start:field[state.Cell];
                double at=now+state.Tick*tick;
                if(state.Cell>=0)
                {
                    foreach(var target in candidates)
                    {
                        if(Vector2.DistanceSquared(point,target)>16 || !Accept(target,Arrival(point,target,at)) || !Clear(point,target,at)) continue;
                        var reverse=new List<Vector2>{target};
                        var depart=new List<double>{at};
                        var node=state;
                        while(node.Cell>=0)
                        {
                            reverse.Add(field[node.Cell]);
                            node=parent[node];
                            depart.Add(now+node.Tick*tick);
                        }
                        reverse.Reverse();depart.Reverse();
                        return new(target,reverse.ToArray(),true,goal.Name){Departures=depart.ToArray()};
                    }
                }
                int col=state.Cell<0?(int)Math.Round((start.X-81)/2):state.Cell%20;
                int row=state.Cell<0?(int)Math.Round((start.Y-86)/2):state.Cell/20;
                for(int y=Math.Max(0,row-1);y<=Math.Min(14,row+1);y++)
                for(int x=Math.Max(0,col-1);x<=Math.Min(19,col+1);x++)
                {
                    int index=y*20+x;
                    var to=field[index];
                    double travel=Vector2.Distance(point,to)/speed;
                    int steps=state.Cell<0 && travel<.001 ? 0 : Math.Max(1,(int)Math.Ceiling(travel/tick-1e-6));
                    var next=(index,state.Tick+steps);
                    double arrival=now+next.Item2*tick;
                    if(arrival>end || parent.ContainsKey(next) || !Clear(point,to,at)) continue;
                    if(zones.Any(z=>z.From<=arrival && z.Until>=at+travel && z.Contains(to))) continue;
                    parent.Add(next,state);
                    open.Enqueue(next,arrival+(Math.Max(0,Vector2.Distance(to,preferred)-goal.Radius))/speed);
                }
            }
            return null;
        }
        // Report the missed obligation, but move out of imminent damage when possible.
        var escape = field.OrderBy(p=>Vector2.DistanceSquared(p,preferred)+.15f*Vector2.DistanceSquared(p,start));
        foreach(var p in escape)
        {
            double at=Arrival(start,p,now);
            if(CanHold(p,at) && Clear(start,p,now)) return new(p,[p],false,goal.Name+" · 只能避险");
        }
        // A late signal can put the actor inside an active pool. Choose the earliest
        // exit without crossing a different hazard, even if the snapshot is already lost.
        Vector2? exit=null;
        double leastExposure=double.PositiveInfinity;
        foreach(var p in escape)
        {
            double arrive=Arrival(start,p,now);
            if(!CanHold(p,arrive)) continue;
            int samples=Math.Max(1,(int)Math.Ceiling((arrive-now)*speed/.20));
            double exposure=0;
            foreach(var z in zones)
            {
                if(z.Until<now || z.From>arrive) continue;
                bool startedInside=z.Contains(start), left=!startedInside;
                for(int i=1;i<=samples;i++)
                {
                    float f=(float)i/samples;
                    bool inside=z.Contains(Vector2.Lerp(start,p,f));
                    if(!inside) {left=true;continue;}
                    if(!z.Active(role,now+(arrive-now)*f)) continue;
                    if(left) {exposure=double.PositiveInfinity;break;}
                    exposure+=(arrive-now)/samples;
                }
                if(exposure>=leastExposure) break;
            }
            if(exposure<leastExposure) {exit=p;leastExposure=exposure;}
        }
        return exit is {} safe ? new(safe,[safe],false,goal.Name+" · 紧急撤出") : new(start,[],false,goal.Name+" · 暂无可行路线");
    }
}

}
