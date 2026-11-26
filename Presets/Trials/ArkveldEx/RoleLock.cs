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
using System.Reflection;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.Geometry;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Arkveld;

// This is a USER confirmation, not an entitlement gate. No inferred job order
// becomes authoritative until all eight distinct members have been confirmed.
internal sealed class RoleLock
{
    public string[] Seats {get;set;}=new string[8];
    public bool Locked {get;set;}
    public string Error {get;private set;}="请分配并确认八人职能";
    public string Encode()=>"v1|"+(Locked?"1":"0")+"|"+string.Join(",",Seats);
    public static RoleLock Decode(string text)
    {
        var fields=text.Split('|');if(fields.Length!=3 || fields[0]!="v1")return new();
        var seats=fields[2].Split(',');return seats.Length==8?new(){Seats=seats,Locked=fields[1]=="1"}:new();
    }
    public bool Confirm(IReadOnlyList<PartyEntry> party,ulong self,int own,bool replay)
    {
        if(Seats.Length!=8 || party.Count!=8 || party.Select(p=>p.Key).Distinct().Count()!=8 ||
           Seats.Any(string.IsNullOrEmpty) || Seats.Distinct().Count()!=8 || Seats.Any(k=>party.All(p=>p.Key!=k)))
        {Locked=false;Error="需要完整八人，每个人只能占一个职能";return false;}
        if(!replay && (own is <0 or >7 || party.Single(p=>p.Key==Seats[own]).Actor!=self))
        {Locked=false;Error="本人职能与名单不一致，请把自己分配到所选职能";return false;}
        Locked=true;Error="已确认并锁定";return true;
    }
    public bool Resolve(IReadOnlyList<PartyEntry> party,ulong self,int own,bool replay,out ulong[] roles)
    {
        roles=new ulong[8];
        if(!Locked)return false;
        if(party.Count!=8){Error="名单暂未读齐，保留锁定，等待八人恢复";return false;}
        if(Seats.Length!=8 || Seats.Distinct().Count()!=8 || Seats.Any(k=>party.All(p=>p.Key!=k)))
        {Locked=false;Error="队伍已换人，请重新分配并确认";return false;}
        for(int i=0;i<8;i++)roles[i]=party.Single(p=>p.Key==Seats[i]).Actor;
        if(roles.Any(id=>id is 0 or 0xe0000000) || roles.Distinct().Count()!=8)
        {Error="等待八人对象可用，不按职业猜职能";return false;}
        if(!replay && (own is <0 or >7 || roles[own]!=self))
        {Locked=false;Error="本人职能已改变，请重新确认名单";return false;}
        Error="已确认并锁定";return true;
    }
}
