using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    // A presentation-only scene. It never advances physics or replaces the live match/input state.
    public sealed partial class ConversionGame
    {
        private readonly List<NativeGameFrame> replayFrames=new List<NativeGameFrame>();private int replayGeneration=-1,replayEntities,replaySeen,replayView,spectatorId;private float replayNext,replayDeath=-1,replayStart,replayEnd,replayStarted,replayWait;private bool replayWasAlive,replayRunning,replayBody;
        private NativeArena replayArena;private Scene replayScene;private NativeGameFrame replaySource;private readonly Dictionary<Renderer,bool> liveVisibility=new Dictionary<Renderer,bool>();
        private bool ReplayActive=>replayRunning;
        private NativePilot Spectator=>arena?.Local?.Hp<=0?arena.Pilots.FirstOrDefault(p=>p.Seat.id==spectatorId&&p.Hp>0&&p.Seat.connected):null;
        private void ObserveReplay()
        {
            if(arena?.Local==null)return;if(replayGeneration!=arena.Generation){ClearReplay();replayGeneration=arena.Generation;}
            var p=arena.Local;bool allowed=match.Phase==NativePhase.Fight||match.Phase==NativePhase.Result||match.Phase==NativePhase.Match;if(!allowed){EndReplay();return;}
            if(p.Hp>0){EndReplay();spectatorId=0;}var d=p.Hp<=0?p.Death:null;bool fresh=d!=null&&replaySeen!=d.seed;
            if(fresh){replaySeen=d.seed;if(replayWasAlive&&replayFrames.Count>1&&arena.Time-replayFrames[0].time>=.2f){replayDeath=d.at;replayWait=Time.unscaledTime+1.6f;replayView=d.owner>0?d.owner:p.Seat.id;}}
            replayWasAlive=p.Hp>0;if(arena.Time<replayNext&&!fresh)return;replayNext=arena.Time+.05f;
            // JsonUtility provides a deep copy of mutable authority data; entity and time budgets bound history.
            var frame=JsonUtility.FromJson<NativeGameFrame>(JsonUtility.ToJson(arena.NetworkFrame()));replayFrames.Add(frame);replayEntities+=ReplaySize(frame);
            while(replayFrames.Count>1&&(replayFrames.Count>132||replayEntities>150000||arena.Time-replayFrames[0].time>6)){replayEntities-=ReplaySize(replayFrames[0]);replayFrames.RemoveAt(0);}
        }
        private static int ReplaySize(NativeGameFrame f)=>f.pilots.Length+f.props.Length+f.minions.Length+f.effects.Length+f.snapshot.Shots.Length+f.snapshot.Events.Length;
        private void ClearReplay(){EndReplay();replayFrames.Clear();replayEntities=0;replayNext=0;replaySeen=0;replayWasAlive=false;spectatorId=0;}
        private void EndReplay()
        {
            replayDeath=-1;if(!replayRunning&&replayArena==null)return;if(replayRunning&&arena?.Local?.Hp<=0&&match?.Phase==NativePhase.Fight)replayReturnAt=Time.unscaledTime;replayRunning=false;replaySource=null;replayArena?.Dispose();replayArena=null;if(replayScene.IsValid())SceneManager.UnloadSceneAsync(replayScene);
            foreach(var pair in liveVisibility)if(pair.Key!=null)pair.Key.enabled=pair.Value;liveVisibility.Clear();presentation?.ResetPresentation();nativeAudio?.ResetCursor();
        }
        private void SpectatorControls()
        {
            if(arena?.Local==null||arena.Local.Hp>0||menu||BoardOpen)return;if(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Escape))EndReplay();
            var candidates=arena.Pilots.Where(p=>p!=arena.Local&&p.Hp>0&&p.Seat.connected&&!(p.Seat.AuthorityBoss&&arena.Pve?.Hidden==true)).OrderBy(p=>match.SameSide(arena.Local.Seat,p.Seat)?0:1).ThenBy(p=>p.Seat.id).ToArray();if(candidates.Length==0){spectatorId=0;return;}
            int at=Array.FindIndex(candidates,p=>p.Seat.id==spectatorId);if(at<0)at=0;else if(Input.GetKeyDown(KeyCode.RightArrow))at=(at+1)%candidates.Length;else if(Input.GetKeyDown(KeyCode.LeftArrow))at=(at+candidates.Length-1)%candidates.Length;spectatorId=candidates[at].Seat.id;
            if(!replayRunning){yaw+=Input.GetAxisRaw("Mouse X")*2*prefs.sensitivity;pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*1.5f*prefs.sensitivity,-70,70);}
        }
        private bool RenderReplay()
        {
            if(menu||arena.Local.Hp>0){EndReplay();return false;}
            if(!replayRunning&&replayDeath>=0&&(arena.Time>=replayDeath+.6f||Time.unscaledTime>=replayWait))
            {
                if(replayFrames.Count<2){replayDeath=-1;return false;}replayStart=Math.Max((float)replayFrames[0].time,replayDeath-3);replayEnd=replayDeath+1.25f;replayStarted=Time.unscaledTime;replayRunning=true;replayBody=false;
                replayScene=SceneManager.CreateScene("Killcam presentation",new CreateSceneParameters(LocalPhysicsMode.Physics3D));replayArena=new NativeArena(content,new NativeMatch(content),arena.MapId){Replica=true,LocalId=arena.LocalId};presentation.ResetPresentation();nativeAudio.ResetCursor();

            }
            if(!replayRunning)return false;foreach(var r in arena.Root.GetComponentsInChildren<Renderer>()){if(!liveVisibility.ContainsKey(r))liveVisibility[r]=r.enabled;r.enabled=false;}float time=replayStart+Time.unscaledTime-replayStarted;replayTime=time;if(time>replayEnd||time>replayFrames[replayFrames.Count-1].time+.5){EndReplay();return false;}
            var a=replayFrames[0];var b=a;foreach(var f in replayFrames){if(f.time>time){b=f;break;}a=b=f;}
            if(replaySource!=a){var copy=JsonUtility.FromJson<NativeGameFrame>(JsonUtility.ToJson(a));replayArena.ApplyFrame(copy,arena.LocalId);SceneManager.MoveGameObjectToScene(replayArena.Root,replayScene);replaySource=a;}
            float blend=Mathf.Clamp01((time-(float)a.time)/Math.Max(.00001f,(float)(b.time-a.time)));
            foreach(var p in replayArena.Pilots){var x=a.pilots.First(v=>v.id==p.Seat.id);var y=b.pilots.FirstOrDefault(v=>v.id==p.Seat.id);if(y!=null&&(x.hp>0)==(y.hp>0)){p.Body.position=Vector3.Lerp(x.position,y.position,blend);p.Yaw=Mathf.LerpAngle(x.yaw,y.yaw,blend);p.Pitch=Mathf.Lerp(x.pitch,y.pitch,blend);}}
            foreach(var p in replayArena.Props){var x=a.props.FirstOrDefault(v=>v.id==p.Definition.id);var y=b.props.FirstOrDefault(v=>v.id==p.Definition.id);if(x!=null&&y!=null&&p.Body!=null)p.transform.SetPositionAndRotation(Vector3.Lerp(x.position,y.position,blend),Quaternion.Slerp(x.rotation,y.rotation,blend));}
            var target=replayArena.Pilots.Find(p=>p.Seat.id==replayView);if(time>replayDeath&&(target==null||target.Hp<=0||Vector3.Distance(target.Body.position,replayArena.Local.Body.position)>24))replayBody=true;if(replayBody||target==null)target=replayArena.Local;
            Vector3 focus=target.Body.position+Vector3.up*(NativeViewRules.EyeHeight+.22f)+Quaternion.Euler(0,target.Yaw,0)*Vector3.right*.85f;Quaternion rotation=Quaternion.Euler(-target.Pitch,target.Yaw,0);Vector3 offset=rotation*Vector3.back*NativeViewRules.ThirdDistance+Vector3.up*NativeViewRules.ThirdLift;
            if(replayScene.GetPhysicsScene().SphereCast(focus,.12f,offset.normalized,out var hit,offset.magnitude,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))offset=offset.normalized*Math.Max(.15f,hit.distance-.1f);cameraView.transform.SetPositionAndRotation(focus+offset,rotation);cameraView.fieldOfView=NativeViewRules.ThirdFov;viewModelCamera.enabled=false;presentation.FirstPerson=false;presentation.AimHeld=false;presentation.LocalId=target.Seat.id;
            presentation.Render(replayArena.Snapshot(),time,false);presentation.RenderAbilities(replayArena,null);presentation.RenderGlow(replayArena,cameraView);presentation.FinishSourceFrame();nativeAudio.Render(replayArena,false,cameraView);return true;
        }
        private float replayTime,replayReturnAt=-10;
        // killcam-hud.ts CAUSES for the causes the native death record can name.
        private static string KillCause(NativeDeath d,NativePilot killer)=>killer==null?"Environment":killer.Weapon.@class=="melee"?(d.melee?"Blunt force":"Special attack"):"Direct hit";
        // killcam-hud.ts / killcam.css: a broadcast frame on the screen edges so the fatal hit in the centre stays clear.
        private void ReplayHud(float width,float height)
        {
            float W=NativeUi.W,H=NativeUi.H;
            if(replayRunning)
            {
                var d=arena.Local.Death;var killer=d!=null&&d.owner>0&&d.owner!=arena.Local.Seat.id?arena.Pilots.Find(x=>x.Seat.id==d.owner):null;bool self=killer==null;
                float age=replayTime-replayStart,span=Mathf.Max(.001f,replayEnd-replayStart),progress=Mathf.Clamp01(age/span),mark=Mathf.Clamp01((replayDeath-replayStart)/span),close=replayEnd-.3f;
                float Out(float duration,Func<float,float> ease)=>ease(NativeUiMotion.Progress(replayTime,close,duration));Func<float,float> easeIn=t=>NativeUiMotion.Bezier(.42f,0,1,1,t),bar=t=>NativeUiMotion.Bezier(.5f,0,.8f,.4f,t),pop=t=>NativeUiMotion.Bezier(.2f,.8f,.2f,1,t);
                Color accent=self?NativeUi.Hex(0xff535f):killer!=null?SeatColor(killer.Seat):NativeUi.Hex(0xff6b35);float kb=Mathf.Clamp(H*.072f,34,64),g=24;
                // Letterbox grows in over .34 s and closes over the last .26 s; vignette and the single cut flash.
                float lb=pop(NativeUiMotion.Progress(age,0,.34f))*(1-Out(.26f,bar));NativeUi.Fill(new Rect(0,0,W,kb*lb),NativeUi.Hex(0x050608,.9f));NativeUi.Fill(new Rect(0,H-kb*lb,W,kb*lb),NativeUi.Hex(0x050608,.9f));
                float vig=NativeUiMotion.EaseOut(NativeUiMotion.Progress(age,0,.4f))*(1-Out(.26f,easeIn));NativeUi.RadialStops(new Rect(-W*.25f,-H*.2f,W*1.5f,H*1.4f),NativeUi.Hex(0x0a0204,.66f*vig),new[]{0,.58f,1,1.05f},new[]{0f,0f,1f,1f});
                NativeUi.Round(new Rect(0,0,W,H),Color.clear,4,NativeUi.A(accent,.45f*vig),2);
                float flash=.7f*(1-NativeUiMotion.EaseOut(NativeUiMotion.Progress(age,0,.26f)));NativeUi.RadialStops(new Rect(-W*.2f,-H*.2f,W*1.4f,H*1.4f),new Color(1,1,1,flash),new[]{0,.75f,1},new[]{.5f,.125f,.125f});
                // Tag: blinking REC dot, KILL CAM and the clock relative to the fatal hit.
                float tag=pop(NativeUiMotion.Progress(age,0,.36f)),ty=kb/2;bool blink=Mathf.Repeat(Time.unscaledTime,1.1f)<.55f||replayTime>=close;float tx=g-14*(1-tag),ta=.45f+.55f*tag;
                var dot=new Rect(tx,ty-4.5f,9,9);NativeUi.Glow(dot,NativeUi.Hex(0xff3b47,.5f*ta),6);NativeUi.Round(dot,NativeUi.Hex(0xff3b47,(blink&&replayTime<close?1:.4f)*ta),6);
                NativeUi.Text(new Rect(tx+19,ty-8,120,16),"KILL CAM",13,new Color(1,1,1,ta),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);float rel=replayTime-replayDeath;string clock=rel<-.05f?"−"+Mathf.Abs(rel).ToString("0.0")+"s":rel>.05f?"+"+rel.ToString("0.0")+"s":"0.0s";
                var ck=new Rect(tx+19+NativeUi.Measure("KILL CAM",13,true,true,.2f).x+10,ty-11,66,22);NativeUi.Round(ck,new Color(1,1,1,.08f*ta),4);NativeUi.Text(new Rect(ck.x,ck.y,ck.width-7,ck.height),clock,13,NativeUi.Hex(0xd6d7e1,ta),TextAnchor.MiddleRight,mono:true,tracking:.06f);
                // Lower third: who and how, just above the bottom letterbox.
                float card=pop(NativeUiMotion.Progress(age,.08f,.4f))*(1-Out(.26f,easeIn)),cx=g-22*(1-pop(NativeUiMotion.Progress(age,.08f,.4f)))-16*Out(.26f,easeIn);string who=self?"You were eliminated":"Eliminated by",name=self?KillCause(d,null):killer.Seat.name,how=self?"No killer · replaying your view":(killer.Weapon.name+"  ·  "+KillCause(d,killer));
                float fs=Mathf.Clamp(W*.024f,20,28),cw=Mathf.Min(440,W-2*g),ch=10+14+fs*1.1f+5+(match.Teams&&!self?20:0)+18+11,cy=H-kb-16-ch;
                NativeUi.HGradient(new Rect(cx,cy,cw*.7f,ch),NativeUi.Hex(0x0d0f16,.94f*card),NativeUi.Hex(0x0d0f16,.78f*card));NativeUi.HGradient(new Rect(cx+cw*.7f,cy,cw*.3f,ch),NativeUi.Hex(0x0d0f16,.78f*card),Color.clear);NativeUi.Fill(new Rect(cx,cy,3,ch),NativeUi.A(accent,card));
                float ly=cy+10;NativeUi.Text(new Rect(cx+16,ly,cw-34,14),who.ToUpperInvariant(),10,NativeUi.Hex(0xa6a7ad,card),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);ly+=16;
                NativeUi.Text(new Rect(cx+16,ly,cw-34,fs*1.1f),name,fs,NativeUi.A(accent,card),TextAnchor.MiddleLeft,bold:true,tracking:-.02f,shadow:new Color(0,0,0,card));ly+=fs*1.1f+3;
                if(match.Teams&&!self){var tc=TeamColor(killer.Seat.team);string tn=NativeMatch.TeamNames[TeamIndex(killer.Seat.team)].ToUpperInvariant();var tr=new Rect(cx+16,ly,NativeUi.Measure(tn,10,true,true,.12f).x+12,16);NativeUi.Round(tr,Color.clear,4,NativeUi.A(tc,card));NativeUi.Text(tr,tn,10,NativeUi.A(tc,card),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);ly+=20;}
                NativeUi.Text(new Rect(cx+16,ly,cw-34,18),how,13,NativeUi.Hex(0xd6d7e1,card),TextAnchor.MiddleLeft);
                // Timeline in the bottom letterbox: white up to the fatal-hit diamond, red after it.
                float tl=(.45f+.55f*NativeUiMotion.Progress(age,.1f,.4f))*(1-Out(.24f,easeIn)),tw=Mathf.Min(520,W*.44f),tlx=(W-tw)/2,tly=H-kb/2-8;
                NativeUi.Text(new Rect(tlx,tly-14,tw,12),replayView==d?.owner&&!self?"KILLER’S VIEW":"YOUR FINAL MOMENTS",10,NativeUi.Hex(0xa6a7ad,tl),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.18f);
                var track=new Rect(tlx,tly+4,tw,4);NativeUi.Round(new Rect(track.x,track.y,tw*mark,4),new Color(1,1,1,.165f*tl),4);NativeUi.Round(new Rect(track.x+tw*mark,track.y,tw*(1-mark),4),NativeUi.Hex(0xff535f,.2f*tl),4);
                NativeUi.Round(new Rect(track.x,track.y,tw*Mathf.Min(progress,mark),4),NativeUi.Hex(0xf4f4f5,tl),4);if(progress>mark)NativeUi.Round(new Rect(track.x+tw*mark,track.y,tw*(progress-mark),4),NativeUi.Hex(0xff535f,tl),4);
                float hit=replayTime>=replayDeath?NativeUiMotion.EaseOut(NativeUiMotion.Progress(replayTime,replayDeath,.5f)):1;var mk=new Vector2(track.x+tw*mark,track.y+2);
                if(replayTime>=replayDeath&&hit<1)NativeUi.Glow(new Rect(mk.x-5,mk.y-5,10,10),NativeUi.Hex(0xff535f,.8f*(1-hit)*tl),12*hit);
                NativeUi.Push(mk,Vector2.zero,1,45);NativeUi.Round(new Rect(mk.x-7,mk.y-7,14,14),NativeUi.Hex(0x050608,tl),2);NativeUi.Round(new Rect(mk.x-5,mk.y-5,10,10),replayTime>=replayDeath?NativeUi.Hex(0xff535f,tl):NativeUi.Hex(0x16171c,tl),2,NativeUi.Hex(0xff535f,tl),2);NativeUi.Pop();
                // Skip pill (Space), bottom right in the letterbox.
                float sk=(.45f+.55f*NativeUiMotion.Progress(age,0,.3f))*(1-Out(.2f,easeIn));var sr=new Rect(W-g-150,H-kb/2-17,150,34);float sh=NativeUi.HoverAmount(9137,sr);
                NativeUi.Pill(sr,NativeUi.Mix(NativeUi.Hex(0x16171c,.9f*sk),NativeUi.Hex(0x2a2b33,sk),sh),NativeUi.Mix(new Color(1,1,1,.22f*sk),NativeUi.A(accent,sk),sh));NativeUi.Text(new Rect(sr.x+14,sr.y,90,sr.height),"Skip replay",13,NativeUi.Hex(0xf4f4f5,sk),TextAnchor.MiddleLeft,bold:true);
                var kr=new Rect(sr.xMax-8-48,sr.y+8,48,18);NativeUi.Pill(kr,new Color(1,1,1,.11f*sk),Color.clear);NativeUi.Text(kr,"Space",10,NativeUi.Hex(0xf4f4f5,sk),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.06f);
                if(NativeUi.Button(sr)&&sk>.5f)EndReplay();
                return;
            }
            // Return to live spectating: a .45 s flash over the cut and a 1.6 s LIVE chip.
            float back=Time.unscaledTime-replayReturnAt;if(back>=0&&back<1.6f)
            {
                NativeUi.RadialStops(new Rect(-W*.2f,-H*.2f,W*1.4f,H*1.4f),new Color(1,1,1,.9f*(1-NativeUiMotion.EaseOut(Mathf.Clamp01(back/.45f)))),new[]{0,.7f,1},new[]{.33f,0,0});
                float k=back/1.6f,live=k<.12f?NativeUiMotion.EaseOut(k/.12f):k<.75f?1:1-NativeUiMotion.EaseOut((k-.75f)/.25f),dy=k<.12f?-6*(1-NativeUiMotion.EaseOut(k/.12f)):0;var chip=new Rect(24,22+dy,74,22);
                NativeUi.Pill(chip,NativeUi.Hex(0x0d0f16,.85f*live),NativeUi.Hex(0x59dd83,.4f*live));var ld=new Rect(chip.x+9,chip.y+7,8,8);NativeUi.Glow(ld,NativeUi.Hex(0x59dd83,.6f*live),5);NativeUi.Round(ld,NativeUi.Hex(0x59dd83,live),4);
                NativeUi.Text(new Rect(chip.x+24,chip.y,chip.width-28,chip.height),"LIVE",11,NativeUi.Hex(0xd6f8e1,live),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            }
            if(arena?.Local?.Hp<=0&&match.Phase==NativePhase.Fight)SpectatorLabel();
        }
        // main.ts spectator label: who is being watched, their side and HP, and how to cycle.
        private void SpectatorLabel()
        {
            float W=NativeUi.W,H=NativeUi.H;var p=Spectator;bool pve=arena.Pve!=null;var candidates=arena.Pilots.Count(x=>x!=arena.Local&&x.Hp>0&&x.Seat.connected);
            string title=p!=null?"SPECTATING "+p.Seat.name.ToUpperInvariant():pve?"DOWN":"ELIMINATED",info=p==null?null:(match.Teams&&!pve?NativeMatch.TeamNames[TeamIndex(p.Seat.team)]+" team · ":"")+Mathf.CeilToInt(p.Hp)+" / "+Mathf.RoundToInt((float)p.Stats["hp"])+" HP";
            string hint=candidates>0?"← / → to cycle surviving players":"No surviving players. Waiting for the next round.";
            float w=Mathf.Max(300,Mathf.Max(NativeUi.Measure(title,18,true).x,NativeUi.Measure(hint,14).x)+44),h=12+24+(info!=null?24:0)+(candidates>1?58:0)+20+12,x=(W-w)/2,y=H-32-h;
            NativeUi.Round(new Rect(x,y,w,h),NativeUi.Hex(0x080b16,.87f),12,NativeUi.Hex(0x44567b));float cy=y+12;
            NativeUi.Text(new Rect(x,cy,w,24),title,18,NativeUi.Hex(0xf4f4f5),TextAnchor.MiddleCenter,bold:true);cy+=24;
            if(info!=null){NativeUi.Text(new Rect(x,cy,w,22),info,14,NativeUi.Hex(0xbfc0c9),TextAnchor.MiddleCenter);cy+=24;}
            if(candidates>1){float bw=120;if(NativeUi.Btn(new Rect(W/2-bw-5,cy+5,bw,40),"← Previous",size:13))CycleSpectator(-1);if(NativeUi.Btn(new Rect(W/2+5,cy+5,bw,40),"Next →",size:13))CycleSpectator(1);cy+=58;}
            NativeUi.Text(new Rect(x,cy,w,20),hint,14,NativeUi.Hex(0xa6a7ad),TextAnchor.MiddleCenter);
        }
        private void CycleSpectator(int step)
        {
            var candidates=arena.Pilots.Where(p=>p!=arena.Local&&p.Hp>0&&p.Seat.connected&&!(p.Seat.AuthorityBoss&&arena.Pve?.Hidden==true)).OrderBy(p=>match.SameSide(arena.Local.Seat,p.Seat)?0:1).ThenBy(p=>p.Seat.id).ToArray();if(candidates.Length==0)return;
            int at=Math.Max(0,Array.FindIndex(candidates,p=>p.Seat.id==spectatorId));spectatorId=candidates[(at+step+candidates.Length)%candidates.Length].Seat.id;
        }
    }
}
