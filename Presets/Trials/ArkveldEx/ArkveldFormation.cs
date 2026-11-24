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
    private sealed class Station
    {
        public ArkveldRoom Room;
        public int Window=-1,Part,Leg;
        public Vector2 Anchor,Point,Previous;
        public bool Roaming;
    }
    private readonly Station[] stations=Enumerable.Range(0,8).Select(_=>new Station()).ToArray();
    private double tailUntil=-100;
    private Vector2 tailAxis;
    private readonly Vector2[] spinPoints=new Vector2[8];
    private int spinRound;

    private static bool IsFormation(ArkveldRoom room)=>room is ArkveldRoom.Blade or ArkveldRoom.Cross or
        ArkveldRoom.Rush or ArkveldRoom.Flight or ArkveldRoom.Bait or ArkveldRoom.Beam or ArkveldRoom.Tail;

    // Stable across clients and ARR seeking. Every slot derives the same lane ordering.
    private uint FormationSeed(int part)
    {
        uint h=unchecked((uint)bossId^movementCue*0x85ebca6bu^(uint)movementWindow*0x9e3779b9u^(uint)part*0xc2b2ae35u);
        h^=h>>16;
        return unchecked(h*0x7feb352du)^(h>>7);
    }
    private static bool HasRoom(Vector2 p,Role role,double from,double until,Zone[] danger,float buffer)
    {
        float rim=ArkveldWalking.Radius-buffer*.5f;
        if(Vector2.DistanceSquared(p,Center)>rim*rim)return false;
        foreach(var z in danger)
        {
            if(z.From>until || z.Until<from || (z.Roles & 1<<(int)role)==0)continue;
            if(z.Contains(p) || z.Contains(p+Vector2.UnitX*buffer) || z.Contains(p-Vector2.UnitX*buffer) ||
                z.Contains(p+Vector2.UnitY*buffer) || z.Contains(p-Vector2.UnitY*buffer))return false;
        }
        return true;
    }

    private Destination FormationGoal(Role role,double now,Vector2 current,Destination goal,Zone[] danger,float speed)
    {
        // An aimed beam belongs to the actual marker. Changing its bearing would move everyone else's hazard.
        if(goal.Room==ArkveldRoom.Beam && Members[(int)role]!=0 && Members[(int)role]==laserMarker)return goal;
        var station=stations[(int)role];
        var radial=Unit(goal.Point-Center,Vector2.UnitY);
        var axis=goal.Room switch
        {
            ArkveldRoom.Blade=>bladeAlong,
            ArkveldRoom.Flight=>flightAxis,
            ArkveldRoom.Bait=>waveAxis,
            ArkveldRoom.Beam=>Arena.Direction(laserAngle),
            ArkveldRoom.Tail=>tailAxis,
            _=>Right(radial)
        };
        int part=goal.Room switch
        {
            ArkveldRoom.Blade=>FirstBladePending(now)?1:2,
            ArkveldRoom.Flight=>now<firstLane+.18?1:2,
            ArkveldRoom.Rush=>rushLanes.Count,
            ArkveldRoom.Bait=>groundWaves,
            ArkveldRoom.Beam=>lasers,
            _=>0
        };
        bool blade=goal.Room==ArkveldRoom.Blade;
        bool rings=goal.Room==ArkveldRoom.Rush && danger.Any(z=>z.Shape==Shape.Ring && z.Until>=now && z.From<=now+4);
        float width=goal.Room switch {ArkveldRoom.Blade=>2.85f,ArkveldRoom.Bait=>4.15f,ArkveldRoom.Beam=>4.25f,
            ArkveldRoom.Flight=>5.15f,ArkveldRoom.Rush=>rings?3.1f:5.5f,_=>5.5f};
        // Other clients cannot reserve lanes for a solo player. Keep the conservative
        // staging band through the preceding attacks so the next spread remains reachable.
        if(personal)width=goal.Room switch
        {
            ArkveldRoom.Blade=>2.85f,ArkveldRoom.Bait or ArkveldRoom.Beam=>2.8f,
            ArkveldRoom.Flight=>3.3f,ArkveldRoom.Rush=>rings?2.7f:4.4f,_=>5.2f
        };
        bool crystalBeam=goal.Room==ArkveldRoom.Beam && CrystalSweep(now);
        bool unstableRing=rings && now<irregularUntil;
        if(crystalBeam)width=2.8f;
        if(unstableRing)width=2.7f;
        bool crossingGrid=blade && now-crossStart is >=0 and <40;
        bool openBlade=blade && !crossingGrid;
        if(openBlade)width=4.35f;
        float depth=blade?(openBlade?2.3f:.16f):rings?.8f:goal.Room is ArkveldRoom.Bait or ArkveldRoom.Beam?1.15f:2.2f;
        bool changed=station.Window!=movementWindow || station.Room!=goal.Room || station.Part!=part;
        // In a moving grid the old solver may nominate the current foot position each frame.
        // Re-anchor only when it actually requires a different place.
        bool shifted=Vector2.DistanceSquared(goal.Point,current)>.8f*.8f && Vector2.DistanceSquared(station.Anchor,goal.Point)>2.0f*2.0f;
        if(changed || shifted)
        {
            station.Room=goal.Room;station.Window=movementWindow;station.Part=part;station.Leg=0;
            station.Anchor=goal.Point;station.Previous=current;station.Roaming=false;
        }
        double deadline=goal.Room==ArkveldRoom.Blade?(part==1?bladeFirst:bladeEnd):goal.Deadline;
        double horizon=Math.Clamp(deadline+.22,now+.55,now+4.5);
        if(rings && personal)horizon=Math.Max(horizon,now+3.4);
        else if(rings)
        {
            double firstRing=danger.Where(z=>z.Until>=now && z.Snapshot>=now && z.Shape is Shape.Circle or Shape.Ring)
                .Select(z=>z.Snapshot!.Value).DefaultIfEmpty(now+1.65).Min();
            horizon=Math.Max(now+.38,Math.Min(now+3.4,firstRing+.20));
        }
        if(unstableRing)horizon=Math.Max(horizon,now+3.4);
        bool openLane=!personal && !crystalBeam && goal.Room is (ArkveldRoom.Beam or ArkveldRoom.Flight or ArkveldRoom.Bait);
        if(openLane)
        {
            double next=danger.Where(z=>z.Snapshot>=now && z.Until>=now && (z.Roles & 1<<(int)role)!=0)
                .Select(z=>z.Snapshot!.Value).DefaultIfEmpty(now+2.1).Min();
            horizon=Math.Max(now+.50,Math.Min(horizon,next+.23));
        }
        bool Clear(Vector2 p)
        {
            if(!HasRoom(p,role,Math.Max(now,Math.Min(deadline-.72,now+1.4)),horizon,danger,.47f))return false;
            if(!personal && goal.Room==ArkveldRoom.Beam && lasers<3){
                var origin=lasers>0?laserOrigin:bossPoint;
                if(Math.Abs(Vector2.Dot(p-station.Anchor,Right(Unit(station.Anchor-origin,axis))))>.65f)return false;
            }
            if(!openBlade)return true;
            var flank=part==1?bladeSide:-bladeSide;
            if(Math.Abs(Vector2.Dot(p-goal.Point,flank))>2.35f)return false;
            // Check the actual snapshot even when the first hit is beyond the normal lookahead.
            if(!HasRoom(p,role,deadline-.26,deadline+.12,danger,part==1?.42f:.8f))return false;
            if(part!=1)return true;
            var exit=BladePoint(role,false,bladeEnd,now,p);
            double start=bladeFirst+.25;
            bool Crossable(Vector2 target)
            {
                var delta=target-p;float length=delta.Length();
                if(length>Math.Max(0,bladeEnd-start-.38)*Walking.Speed || length<.01f)return false;
                foreach(var zone in danger){
                    if(zone.Snapshot is not {} at || at<bladeFirst+.55 || at>bladeEnd+.35 || (zone.Roles & 1<<(int)role)==0)continue;
                    double end=Math.Min(zone.Until,at+.25);
                    for(double t=Math.Min(end,zone.From);t<=end+.07;t+=.07){
                        var pos=p+delta*Math.Clamp((float)((Math.Min(t,end)-start)*Walking.Speed)/length,0,1);
                        if(zone.Contains(pos))return false;
                    }
                }
                return true;
            }
            return Crossable(exit) || Crossable(exit+axis*1.8f) || Crossable(exit-axis*1.8f);
        }
        bool hasRoomToMove=(!blade || openBlade && deadline-now>3.6) && !rings && deadline-now>1.65 && goal.Room is not (ArkveldRoom.Bait or ArkveldRoom.Beam);
        bool validStation=!changed && !shifted && Clear(station.Point);
        bool nearStation=validStation && Vector2.DistanceSquared(current,station.Point)<=1.0f;
        // A caster already in a legal pocket may keep this exact position. An assigned
        // bait, a tight grid or a crossing whose second cut is unreachable never qualifies.
        float fromAnchor=Vector2.DistanceSquared(current,goal.Point);
        bool insideLane=nearStation || fromAnchor<(blade?width*width:1.0f) ||
            validStation && station.Roaming && fromAnchor<=width*width;
        bool mayHold=goal.Room is ArkveldRoom.Blade or ArkveldRoom.Cross or ArkveldRoom.Tail or ArkveldRoom.Rush ||
            goal.Room==ArkveldRoom.Flight && validStation ||
            goal.Room is ArkveldRoom.Bait or ArkveldRoom.Beam && fromAnchor<.35f*.35f;
        bool freeHere=mayHold && insideLane && !(blade && !openBlade || rings || TightGrid(now)) && Clear(current);
        if(freeHere && (IsCasting(role,now) || nearStation && !MobileJob(role) && !station.Roaming && !CanRoam(role,now))){
            if(station.Roaming)SkipRoam(role,now);
            station.Roaming=false;station.Point=current;
            return goal with {Point=current,Discretionary=true};
        }
        if(validStation && (!CanRoam(role,now) || !hasRoomToMove || !nearStation))
            return goal with {Point=station.Point,Discretionary=station.Roaming && freeHere};
        bool voluntary=validStation && nearStation && hasRoomToMove;
        bool canStretch=!personal && !blade && !unstableRing && !crystalBeam && goal.Room!=ArkveldRoom.Bait && !TightGrid(now);
        bool canWait=canStretch && Vector2.DistanceSquared(current,goal.Point)<=width*width &&
            HasRoom(current,role,now,Math.Max(now+.65,horizon),danger,.65f);
        if(voluntary){station.Previous=station.Point;station.Leg++;}
        int sequence=(blade?1:part)*31+station.Leg;
        uint seed=Variation(role,sequence+41),order=FormationSeed(blade?1:part);
        int rank=(((int)role*((order&1)==0?3:5))+(int)((order>>3)&7))&7;
        float across=((rank+.50f+(seed%301)/1000f-.15f)/8f*2-1)*width;
        if(openBlade)across=((seed%1009)/504f-1)*width;
        if(station.Leg>0)across=Math.Clamp(across+((seed&1)==0?-1:1)*width*.53f,-width,width);
        float fore=((seed>>10)%1001/500f-1)*depth;
        axis=Unit(axis,Vector2.UnitY);
        if(!personal && goal.Room==ArkveldRoom.Beam && lasers<3){
            // Before the icon arrives any teammate can be next. A radial lane widens the group without turning the beam.
            var emitter=lasers>0?laserOrigin:bossPoint;
            axis=Unit(station.Anchor-emitter,axis);fore=Math.Clamp(fore,-.42f,.42f);
        }
        var normal=Right(axis);
        var desired=ArkveldWalking.Clamp(station.Anchor+axis*across+normal*fore,openLane?19.04f:ArkveldWalking.Radius);
        var best=goal.Point;
        float bestCost=float.PositiveInfinity;
        float budget=Math.Max(Vector2.Distance(current,goal.Point)+.4f,(float)Math.Max(0,deadline-now-.35)*speed+.24f);
        void Consider(Vector2 p)
        {
            if(openLane)p=ArkveldWalking.Clamp(p,19.04f);
            if(!Clear(p) || Vector2.DistanceSquared(p,station.Anchor)>(width+depth)*(width+depth))return;
            float walk=Vector2.Distance(current,p);
            if(walk>budget)return;
            float cost=Vector2.Distance(p,desired)+walk*.14f+MeleePenalty(role,p)*1.9f;
            if(canWait && walk is >.16f and <5.25f)cost+=voluntary?15:6.25f;
            if(canWait && walk<.16f)
            {
                float nearest=8;foreach(var actor in Members)if(actor!=Members[(int)role] && PointOf(actor) is {} pt)nearest=Math.Min(nearest,Vector2.Distance(current,pt));
                if(nearest<.82f)cost+=4.2f*(.82f-nearest);
            }
            if(voluntary && Vector2.DistanceSquared(station.Previous,p)<1.5f*1.5f)cost+=2;
            if(personal && !blade && !rings && deadline-now>1.35)
                foreach(var other in Members)
                    if(other!=Members[(int)role] && PointOf(other) is {} ally)
                        cost+=1.2f*Math.Max(0,2.15f-Vector2.Distance(p,Projected(other,ally,now,now+.6)));
            if(cost<bestCost){bestCost=cost;best=p;}
        }
        Consider(desired);Consider(current);Consider(goal.Point);
        if(canWait && hasRoomToMove)for(int i=0;i<14;i++)
            Consider(current+Arena.Direction((i+(seed%31)*.05f)*MathF.Tau/14)*(5.35f+(seed%140)/100f));
        if(!personal && !unstableRing && !crystalBeam)foreach(float r in (ReadOnlySpan<float>)[.9f,1.8f,2.9f])
            for(int i=0;i<12;i++)Consider(ArkveldWalking.Clamp(desired+Arena.Direction((i+.14f)*MathF.Tau/12)*r));
        foreach(float along in (ReadOnlySpan<float>)[-.94f,-.65f,-.35f,0,.35f,.65f,.94f])
            foreach(float d in (ReadOnlySpan<float>)[-.9f,0,.9f])
                Consider(ArkveldWalking.Clamp(station.Anchor+axis*(along*width)+normal*(d*depth)));
        // If no alternative can satisfy this hit, the mandatory route keeps its original deadline.
        if(openLane && !float.IsFinite(bestCost) && !personal)best=desired;
        station.Point=best;station.Roaming=voluntary;
        if(voluntary)SkipRoam(role,now);
        return goal with {Point=best,Discretionary=voluntary && freeHere};
    }

    private void BuildSpinPositions()
    {
        for(int i=0;i<8;i++)spinPoints[i]=BaseSpinSlot(i);
        int first=(int)(FormationSeed(spinRound)&7);
        for(int k=0;k<8;k++)
        {
            int i=(first+k)&7;
            var start=spinPoints[i]-spinOrigin;
            float bearing=Facing(start),radius=start.Length();
            for(int attempt=0;attempt<10;attempt++)
            {
                uint seed=Variation((Role)i,spinRound*113+attempt+13);
                float yaw=((seed%1001)/500f-1)*.15f;
                float length=radius+(((seed>>12)%1001)/500f-1)*(i<3?1.1f:1.8f);
                var candidate=ArkveldWalking.Clamp(spinOrigin+Arena.Direction(bearing+yaw)*length,19.15f);
                if(Vector2.Dot(candidate-spinOrigin,spreadAxis)<1.3f)continue;
                bool clear=true;
                for(int j=0;j<8;j++)if(j!=i && Vector2.DistanceSquared(candidate,spinPoints[j])<7.25f*7.25f){clear=false;break;}
                if(clear){spinPoints[i]=candidate;break;}
            }
        }
    }
}

}
