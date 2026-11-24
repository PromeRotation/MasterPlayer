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



internal enum Role { MT, ST, H1, H2, D1, D2, D3, D4 }
internal enum Element { Opening, Fire, Ice, Thunder, Enrage }
internal enum Input { Cast, Hit, Icon, Map, Spawn, Gone, Status, StatusEnd, Position, Player, Reset, Complete, Death, ObjectState, Ballot, Raise, Recommence, Tether, Animation, Combat, PartySign, Targetable, TargetSeen, CastEnd }
internal readonly record struct Signal(double Time, Input Kind, uint Id, ulong Source = 0, ulong Target = 0,
    Vector2 Point = default, float Angle = 0, float Duration = 0, uint Value = 0, int Count = 0,float Height=0, ulong[]? Targets=null, Vector2? SourcePoint=null);
internal enum Shape { Circle, Ring, Cone, Line, Cross, Platform, Push, OutsideCircle }
internal readonly record struct Zone(Shape Shape, Vector2 Origin, float Angle, float Length, float Width,
    double From, double Until, int Roles = 255, ulong Owner = 0)
{
    public double? Snapshot {get;init;}
    public bool AwaitEffect {get;init;}
    public bool Active(Role slot, double time) => (Roles & 1 << (int)slot) != 0 && time >= From && time <= Until;
    public bool Contains(Vector2 p)
    {
        var d = p - Origin;
        float distance = d.LengthSquared();
        if (Shape == Shape.Circle) return distance < Length * Length;
        if (Shape == Shape.OutsideCircle) return distance > Length * Length;
        if (Shape == Shape.Ring) return distance < Length * Length && distance > Width * Width;
        if (Shape == Shape.Platform) return Arena.Levitating(p) != (Width > 0) || Arena.TileEdge(p) < .35f;
        var dir = Arena.Direction(Angle);
        float ahead = Vector2.Dot(d, dir);
        float side = Math.Abs(d.X * dir.Y - d.Y * dir.X);
        return Shape switch {
            Shape.Line => ahead >= -.3f && ahead <= Length && side < Width,
            Shape.Cross => (Math.Abs(ahead) < Width && side <= Length) || (side < Width && Math.Abs(ahead) <= Length),
            Shape.Cone => distance <= Length*Length && ahead >= MathF.Sqrt(distance) * MathF.Cos(Width),
            Shape.Push => !Arena.Inside(p + dir * Length),
            _ => false };
    }
}

internal static class Arena
{
    public static readonly Vector2 Center = new(100,100);
    public static Vector2 Direction(float radians) => new(MathF.Sin(radians),MathF.Cos(radians));
    public static bool Inside(Vector2 p) => p.X >= 80.5f && p.X <= 119.5f && p.Y >= 85.5f && p.Y <= 114.5f;
    public static Vector2 Clamp(Vector2 p) => Vector2.Clamp(p,new(80.6f,85.6f),new(119.4f,114.4f));
    public static bool Levitating(Vector2 p)
    {
        var column = Math.Min(1,(int)(Math.Abs(p.X-100)/10));
        var row = Math.Clamp((int)((p.Y-85)/10),0,2);
        return ((column ^ row) & 1) != 0;
    }
    public static float TileEdge(Vector2 p)
    {
        float x = Math.Abs(p.X-80) % 10, z = Math.Abs(p.Y-85) % 10;
        return Math.Min(Math.Min(x,10-x),Math.Min(z,10-z));
    }
    public static int Group(Role r) => r is Role.MT or Role.H1 or Role.D1 or Role.D3 ? 0 : 1;
    public static Vector2 Start(Role r) => new(96 + Group(r)*8, 100 + ((int)r/2)*1.2f);
}

internal sealed record Destination(Vector2 Point, double Deadline, string Name, float Radius = 1)
{
    public ArkveldRoom Room {get;init;}
    public bool Discretionary {get;init;}
    public bool Unavailable {get;init;}
    public Zone? Required {get;init;}
    public bool ContactOnly {get;init;}
    public float FieldRadius {get;init;}=ArkveldWalking.Radius;
}
internal sealed record WalkPlan(Vector2 Goal, Vector2[] Steps, bool Feasible, string Name, double WaitUntil = 0)
{
    public bool OptionalStride {get;init;}
    public bool RestJump {get;init;}
    public WalkAppointment? Appointment {get;init;}
    public double[]? Departures {get;init;}
    private int step;
    public Vector2 Advance(Vector2 current,double now,double end,float speed)
    {
        double at=Math.Max(now,WaitUntil);
        while(step<Steps.Length && at<end)
        {
            if(Departures!=null)at=Math.Max(at,Departures[step]);
            if(at>=end)break;
            var d=Steps[step]-current;
            float distance=d.Length();
            if(distance>.0001f && distance>(end-at)*speed)
                return current+d*((float)(end-at)*speed/distance);
            current=Steps[step];at+=distance/speed;
            if(step==Steps.Length-1)break;
            step++;
        }
        return current;
    }
    public Vector2 Next(Vector2 current,double now)
    {
        if(now<WaitUntil || Steps.Length==0) return current;
        while(step<Steps.Length-1 && Vector2.DistanceSquared(current,Steps[step])<.12f) step++;
        return Departures!=null && now<Departures[step] ? current : Steps[step];
    }
}

}
