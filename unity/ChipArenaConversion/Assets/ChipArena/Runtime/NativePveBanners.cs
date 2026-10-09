using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // pve/hud.ts bannerFor + pveBanner (pve.css .pve-banner): the most recent stage banner still showing, centred at 24%.
    // The host has exact stage times; a replica times a banner from when it first saw the state (its snapshots carry none).
    public sealed partial class ConversionGame
    {
        private sealed class PveBannerSpec {public string Key,Title,Sub,Tone;public float At,Duration;}
        private readonly Dictionary<string,float> bannerSeen=new Dictionary<string,float>();private int bannerGeneration=-1;private string lastNotice="";
        private float Seen(string key,float exact){if(!bannerSeen.TryGetValue(key,out float at)){at=arena.Replica?arena.Time:exact;bannerSeen[key]=at;}return at;}
        private static string Deny(string notice)
        {
            string n=(notice??"").ToLowerInvariant();
            return n.Contains("money")?"NOT ENOUGH MONEY":n.Contains("chips first")?"TAKE YOUR CHIPS FIRST":n.Contains("full")?"ALREADY AT FULL HEALTH":n.Contains("recharg")||n.Contains("cool")?"STATION COOLING DOWN":n.Contains("seat")?"NO SEATS FOR RECRUITS":n.Contains("limit")?"RECRUIT LIMIT REACHED":notice.ToUpperInvariant();
        }
        private PveBannerSpec PveBannerNow()
        {
            var v=arena.Pve;if(v==null)return null;if(bannerGeneration!=arena.Generation){bannerSeen.Clear();bannerGeneration=arena.Generation;lastNotice="";}
            float t=arena.Time;var list=new List<PveBannerSpec>();
            if(v.Holding&&(v.Cinematic=="victory"||v.Cinematic=="defeat"))
            {   // The outro holds the simulation clock, so it runs on the hold's own clock and stays .5 s past the hold.
                bool defeat=v.Cinematic=="defeat";return new PveBannerSpec{Key="outro",Title=defeat?"PARTY WIPED":"THE SIREN IS SILENCED",Sub=defeat?"The Siren smiles":"Victory",Tone=defeat?"defeat":"victory",At=t-(v.HoldDuration-v.HoldLeft),Duration=v.HoldDuration+.5f};
            }
            if(!v.Holding)
            {
                int waves=v.Content?.waves?.Length??3;
                if(v.Stage=="wave")list.Add(new PveBannerSpec{Key="wave-"+v.Wave,Title="WAVE "+v.Wave,Sub=v.Wave>=waves?"Final wave before the Siren":v.WaveTotal>0?v.WaveTotal+" minions incoming":"Minions incoming",Tone="wave",At=Seen("wave-"+v.Wave,v.StageAt),Duration=2.6f});
                if(v.Stage=="intermission"&&v.Wave>0)list.Add(new PveBannerSpec{Key="clear-"+v.Wave,Title="WAVE CLEARED",Sub="+$"+(100+50*v.Wave)+" · fallen pilots respawn",Tone="clear",At=Seen("clear-"+v.Wave,v.StageAt),Duration=3});
                if(v.Stage=="boss"&&v.Phase==1)list.Add(new PveBannerSpec{Key="spawned",Title="Siren has Spawned",Tone="boss",At=Seen("spawned",v.StageAt),Duration=3.2f});
                if(v.Stage=="boss"&&v.Phase==2&&v.Hidden)
                {
                    float at=Seen("possess",v.PossessionAt);list.Add(new PveBannerSpec{Key="possess",Title="FIND ME...",Sub="The Siren hides inside a minion",Tone="phase",At=at,Duration=3.2f});
                    // Strong hint pings (PVE_SIREN.possession.hints: strong 35 s, every 8 s for 1.2 s).
                    if(t-at>=35){float k=Mathf.Floor((t-at-35)/8),ping=at+35+k*8;list.Add(new PveBannerSpec{Key="whisper-"+k,Title="Find me...",Tone="phase",At=ping,Duration=1.2f});}
                }
            }
            if(!string.IsNullOrEmpty(arena.PveNotice(arena.Local.Seat.id))&&arena.PveNotice(arena.Local.Seat.id)!=lastNotice){lastNotice=arena.PveNotice(arena.Local.Seat.id);bannerSeen["deny"]=t;}else if(string.IsNullOrEmpty(arena.PveNotice(arena.Local.Seat.id)))lastNotice="";
            if(bannerSeen.TryGetValue("deny",out float denied)&&!string.IsNullOrEmpty(lastNotice))list.Add(new PveBannerSpec{Key="deny",Title=Deny(lastNotice),Tone="deny",At=denied,Duration=1.4f});
            var me=arena.Local;if(me!=null&&me.Hp>0&&t-me.RevivedAt<2.2f&&me.RevivedAt>0){bool respawn=v.Stage=="intermission"||v.Stage=="boss"&&t-v.StageAt<3;list.Add(new PveBannerSpec{Key="revive-"+me.RevivedAt,Title=respawn?"BACK IN THE FIGHT":"REVIVED",Sub=respawn?"Free respawn at 50% health":null,Tone="respawn",At=me.RevivedAt,Duration=respawn?2.2f:2});}
            return list.Where(b=>t>=b.At-.05f&&t<b.At+b.Duration).OrderByDescending(b=>b.At).FirstOrDefault();
        }
        private void PveBanner()
        {
            var b=PveBannerNow();if(b==null)return;float W=U.W,H=U.H,age=arena.Time-b.At;if(b.Key=="outro")age=arena.Pve.HoldDuration-arena.Pve.HoldLeft;
            // pve-banner: in from 1.25x over 10%, hold to 82%, then fade while rising 12 px (cubic-bezier(.2,.8,.2,1) per segment).
            float k=Mathf.Clamp01(age/b.Duration),a,s=1,dy=0;Func<float,float> ease=x=>M.Bezier(.2f,.8f,.2f,1,x);
            if(k<.1f){float u=ease(k/.1f);a=u;s=1.25f-.25f*u;}else if(k<.82f)a=1;else{float u=ease((k-.82f)/.18f);a=1-u;dy=-12*u;}
            bool siren=b.Tone=="boss"||b.Tone=="phase"||b.Tone=="found",deny=b.Tone=="deny";
            float F=deny?18:b.Tone=="boss"?Mathf.Clamp(W*.054f,34,76):Mathf.Clamp(W*.046f,30,64),top=deny?H*.5f+86:H*.24f;
            Color c=b.Tone=="clear"||b.Tone=="respawn"?U.Hex(0xb6ffc8):b.Tone=="victory"?U.Hex(0xffe7a6):b.Tone=="defeat"||deny?U.Hex(0xff8a7a):siren?U.Hex(0xf6f3ff):Color.white;
            Color glow=b.Tone=="clear"?U.Hex(0x59dd83,.4f):b.Tone=="victory"?U.Hex(0xffb84a,.6f):b.Tone=="defeat"?U.Hex(0xff3535,.6f):siren?U.Hex(0x9b87ff,.8f):deny?default:U.Hex(0xff6b35,.4f);
            string title=siren?b.Title:b.Title.ToUpperInvariant();float track=deny?.16f:siren?.04f:.06f;
            U.Push(new Vector2(W/2,top+F/2),new Vector2(0,dy),s);
            if(siren&&b.Tone!="found")
            {   // The violet glitch split: red and cyan copies flicker in steps every 1.6 s.
                float g=Mathf.Repeat(age+1,1.6f)/1.6f,h=Mathf.Repeat(age+1.1f,1.6f)/1.6f;var box=new Rect(0,top,W,F);
                U.SplitCopy(box,box,g,false,title,F,a,TextAnchor.MiddleCenter,track);U.SplitCopy(box,box,h,true,title,F,a,TextAnchor.MiddleCenter,track);
            }
            U.Text(new Rect(0,top,W,F),title,F,U.A(c,a),TextAnchor.MiddleCenter,bold:true,tracking:track,glow:U.A(glow,a),shadow:new Color(0,0,0,a));
            if(!string.IsNullOrEmpty(b.Sub))
            {
                string sub=b.Sub.ToUpperInvariant();float sw=U.Measure(sub,13,true,true,.14f).x+24;var r=new Rect(W/2-sw/2,top+F+10,sw,24);
                U.Pill(r,U.Hex(0x0b0c12,.78f*a),Color.clear);U.Text(r,sub,13,U.A(b.Tone=="clear"?U.Hex(0x9ff0b8):siren?U.Hex(0xd9d0ff):U.Hex(0xffcfae),a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.14f);
            }
            U.Pop();
        }
    }
}
