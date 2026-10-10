using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private sealed class HealthLag {public float Value,Last,Time=-100,Hold;}
        // combat-numbers.ts CombatNumberTrack (NativeCombatNumbers): every hit (burn/poison ticks included) and heal once; a
        // target's numbers of one kind within .16 s share one, in one of three lanes; numbers live 1.15 s (at most 64).
        private readonly NativeCombatNumbers combatTrack=new NativeCombatNumbers();private NativeCombatNumbers.Anchor combatAnchor;
        private bool CombatAnchor(int id,out V3 at)
        {foreach(var p in arena.Pilots)if(p.Seat.id==id){at=NativeArena.Vec(p.Body.transform.position);return true;}at=default;return false;}
        private List<NativeCombatNumbers.Number> CombatNumbers()
        {combatAnchor??=CombatAnchor;return combatTrack.Update(arena.Events,arena.Time,arena.Generation,arena.ViewPilot?.Seat.id??0,arena.Pve!=null,combatAnchor);}
        // One number centred on `c`: .combat-number translate(-50%,-50%) scale(1 + max(0, .16 - age) x 1.5), fading out over
        // its last .48 s; damage red, heals green, each with its drop shadow and dark outline.
        private static void CombatNumberText(NativeCombatNumbers.Number n,Vector2 c,float size,float age)
        {
            float alpha=NativeCombatNumbers.Alpha(age);U.Push(c,Vector2.zero,NativeCombatNumbers.Scale(age));
            NativeUi.Text(new Rect(c.x-70,c.y-size*.6f,140,size*1.2f),n.Label,size,NativeUi.Hex(n.Heal?NativeCombatNumbers.HealColor:NativeCombatNumbers.DamageColor,alpha),TextAnchor.MiddleCenter,bold:true,
                glow:NativeUi.Hex(n.Heal?NativeCombatNumbers.HealOutline:NativeCombatNumbers.DamageOutline,.9f*alpha),shadow:NativeUi.Hex(n.Heal?NativeCombatNumbers.HealShadow:NativeCombatNumbers.DamageShadow,alpha));U.Pop();
        }
        private readonly Dictionary<int,HealthLag> healthLags=new Dictionary<int,HealthLag>();private int hudGeneration=-1;
        // match-hud.css .death-announcement: top left, a dark card (backdrop blur 4 px) with an orange edge; a new line slides in over .26 s.
        private void DeathLine()
        {
            var lines=presentation.DeathMessages(arena.Time).ToArray();if(lines.Length==0)return;float since=arena.Time-presentation.LatestDeathMessage(arena.Time),e=NativeUiMotion.Bezier(.2f,.8f,.2f,1,Mathf.Clamp01(since/.26f));
            float maxW=Mathf.Min(320,NativeUi.W*.42f),w=Mathf.Min(maxW,lines.Max(l=>NativeUi.Measure(l,13).x)+22),h=lines.Length*13*1.45f+14;var r=new Rect(22-10*(1-e),arena.Pve!=null?96:70,w,h);
            NativeUi.Backdrop(r,6,4,e);NativeUi.Round(r,NativeUi.Hex(0x0d0f16,.72f*e),6);NativeUi.Fill(new Rect(r.x,r.y+3,2,r.height-6),NativeUi.Hex(0xff6b35,e));
            for(int i=0;i<lines.Length;i++)NativeUi.Text(new Rect(r.x+11,r.y+7+i*13*1.45f,w-22,13*1.45f),lines[i],13,NativeUi.Hex(0xd9ecf3,e),TextAnchor.MiddleLeft,shadow:new Color(0,0,0,e));
        }
        // main.ts drawHud .leet-status (rules in NativeLeetStatus), in the fight: the controller's CONTROLLING name with the
        // countdown and "Your body is vulnerable", the possessed pilot's POSSESSED BY name with the countdown, else the Admin
        // Pause countdown. The host knows ControlledBy and ControlUntil; a guest is sent only each pilot's Controlling link,
        // so it finds its possessor by that link and counts the 5 s from when the link first appears.
        private int leetLinkFrom,leetLinkTo;private float leetLinkAt;private string leetTitle,leetTitleName;private bool leetTitleControlling;
        private void LeetStatus()
        {
            if(match.Phase!=NativePhase.Fight||Event.current.type!=EventType.Repaint){if(match.Phase!=NativePhase.Fight)leetLinkFrom=leetLinkTo=0;return;}
            var me=arena.Local;float now=arena.Time;NativePilot controller=null,target=null;bool controlling=me.Controlling!=null;
            if(controlling){controller=me;target=me.Controlling;}
            else{controller=me.ControlledBy;if(controller==null)foreach(var q in arena.Pilots)if(q.Controlling==me){controller=q;break;}if(controller!=null)target=me;}
            if(controller==null){leetLinkFrom=leetLinkTo=0;if(now<me.FrozenUntil)LeetBox(NativeLeetStatus.PauseText,NativeHudText.Seconds(me.FrozenUntil-now));return;}
            float until=me.ControlUntil;
            if(controller.Seat.id!=leetLinkFrom||target.Seat.id!=leetLinkTo){leetLinkFrom=controller.Seat.id;leetLinkTo=target.Seat.id;leetLinkAt=now;}
            if(arena.Replica||!(until>0))until=leetLinkAt+NativeLeetStatus.ControlSeconds;
            string name=controlling?target.Seat.name:controller.Seat.name;
            if(leetTitle==null||controlling!=leetTitleControlling||!ReferenceEquals(name,leetTitleName)){leetTitleControlling=controlling;leetTitleName=name;leetTitle=NativeLeetStatus.Title(controlling,name);}
            LeetBox(leetTitle,controlling?NativeHudText.SecondsVulnerable(until-now):NativeHudText.Seconds(until-now));
        }
        // .leet-status: a grid of the bold, tracked title over the 13 px line, centred, 21% down; square #45ff87 edge.
        private static void LeetBox(string title,string sub)
        {
            float tw=U.Measure(title,NativeLeetStatus.TitleSize,true,true,NativeLeetStatus.TitleTracking).x+NativeLeetStatus.TitleTracking*NativeLeetStatus.TitleSize,sw=U.Measure(sub,NativeLeetStatus.SubSize,false,true).x;
            float inner=Mathf.Max(tw,sw),b=NativeLeetStatus.Border,w=inner+2*NativeLeetStatus.PadX+2*b,h=2*NativeLeetStatus.PadY+NativeLeetStatus.TitleLine+NativeLeetStatus.Gap+NativeLeetStatus.SubLine+2*b;
            var r=new Rect(Mathf.Round(U.W/2-w/2),U.H*NativeLeetStatus.Top,w,h);Color ink=U.Hex(NativeLeetStatus.Ink);
            U.Glow(r,U.Hex(NativeLeetStatus.GlowColor,NativeLeetStatus.GlowAlpha),NativeLeetStatus.GlowSpread);
            U.Fill(r,U.Hex(NativeLeetStatus.Fill,NativeLeetStatus.FillAlpha));var edge=U.Hex(NativeLeetStatus.Edge);
            U.Fill(new Rect(r.x,r.y,r.width,b),edge);U.Fill(new Rect(r.x,r.yMax-b,r.width,b),edge);U.Fill(new Rect(r.x,r.y+b,b,r.height-2*b),edge);U.Fill(new Rect(r.xMax-b,r.y+b,b,r.height-2*b),edge);
            float x=r.x+b+NativeLeetStatus.PadX,y=r.y+b+NativeLeetStatus.PadY;
            // The tracking follows the last glyph too, so the browser's centred title sits half a letter-space left.
            U.Text(new Rect(x-NativeLeetStatus.TitleTracking*NativeLeetStatus.TitleSize/2,y,inner,NativeLeetStatus.TitleLine),title,NativeLeetStatus.TitleSize,ink,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:NativeLeetStatus.TitleTracking);
            U.Text(new Rect(x,y+NativeLeetStatus.TitleLine+NativeLeetStatus.Gap,inner,NativeLeetStatus.SubLine),sub,NativeLeetStatus.SubSize,ink,TextAnchor.MiddleCenter,mono:true);
        }
        private void CombatWorldHud(float scale,float width,float height)
        {
            if(menu||arena?.Local==null||cameraView==null||IntroActive||ReplayActive)return;
            if(hudGeneration!=arena.Generation){healthLags.Clear();hudGeneration=arena.Generation;}
            var physics=arena.Root.scene.GetPhysicsScene();var old=GUI.color;
            bool ScreenPoint(Vector3 p,float range,out Vector3 point){point=cameraView.WorldToScreenPoint(p)/scale;if(point.z<=0||Vector3.Distance(cameraView.transform.position,p)>range)return false;Vector3 delta=p-cameraView.transform.position;return !physics.Raycast(cameraView.transform.position,delta.normalized,out _,delta.magnitude-.08f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);}
            void RectColor(Rect rect,Color color){GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
            void Health(int id,Vector3 pos,float hp,float max,float worldWidth,bool ally,string name,bool lag)
            {
                if(hp<=0||!ScreenPoint(pos,55,out var at))return;float ratio=Mathf.Clamp01(hp/Math.Max(1,max)),pixels=Mathf.Clamp(worldWidth*Screen.height/(2*Mathf.Tan(cameraView.fieldOfView*Mathf.Deg2Rad*.5f)*Math.Max(1,Vector3.Distance(cameraView.transform.position,pos)))/scale,16,85),x=at.x-pixels*.5f,y=height-at.y;
                RectColor(new Rect(x-2,y-2,pixels+4,8),new Color(.03f,.04f,.07f,.8f));
                if(lag){if(!healthLags.TryGetValue(id,out var value)){value=new HealthLag();healthLags[id]=value;}float dt=arena.Time-value.Time;value.Time=arena.Time;if(dt<0||dt>.35f||ratio>=value.Value){value.Value=ratio;value.Hold=0;}else{value.Hold=ratio<value.Last?0:value.Hold+Math.Min(dt,.05f);if(value.Hold>.22f)value.Value=Math.Max(ratio,value.Value-Math.Min(dt,.05f)*.9f);}value.Last=ratio;RectColor(new Rect(x,y,pixels*value.Value,4),new Color(1,.85f,.63f));}
                RectColor(new Rect(x,y,pixels*ratio,4),ratio<.25f?new Color(1,.45f,.5f):ally?new Color(.35f,.87f,.51f):new Color(1,.35f,.28f));if(!string.IsNullOrEmpty(name)&&Vector3.Distance(cameraView.transform.position,pos)<32){var style=new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=12};GUI.Label(new Rect(at.x-110,y-27,220,23),name,style);}
            }
            foreach(var p in arena.Pilots){if(!p.Seat.connected||prefs.firstPerson&&p==arena.ViewPilot||p.Seat.AuthorityBoss&&arena.Pve?.Hidden==true)continue;bool ally=p==arena.Local||match.SameSide(p.Seat,arena.Local.Seat);Health(p.Seat.id,p.Body.transform.position+Vector3.up*1.3f*(p.Seat.spec=="siren"?p.Pose.RigScale:1),p.Hp,(float)p.Stats["hp"],.72f,ally,match.Teams?(ally?"ALLY · ":"ENEMY · ")+p.Seat.name:p.Seat.name,true);}
            foreach(var m in arena.Minions)if(m.Hp<m.MaxHp)Health(m.Id,m.Body.transform.position+Vector3.up*(m.Kind=="slender"?1.52f:m.Kind=="toy"?.85f:1.2f),m.Hp,m.MaxHp,m.Kind=="toy"?.42f:.6f,m.Owner==arena.Local||match.SameSide(m.Owner.Seat,arena.Local.Seat),null,false);
            // combat-numbers.ts FloatingCombatNumbers with lobby.css:92 / match-hud.css:3 .combat-number (clamp(18px,1.65vw,25px),
            // 850): red damage and green heals over every pilot, and in PvE the view pilot's hits on adds; shown only in the fight
            // and result phases, the track cleared otherwise (main.ts combatNumbers.update).
            if(match.Phase==NativePhase.Fight||match.Phase==NativePhase.Result)
            {
                float numberSize=Mathf.Clamp(NativeUi.W*.0165f,18,25);int view=arena.ViewPilot?.Seat.id??0;
                foreach(var n in CombatNumbers())
                {
                    float age=Math.Max(0,arena.Time-n.At);Vector3 at;bool own=prefs.firstPerson&&n.Target==view;
                    // A first-person pilot cannot see their own head: their numbers sit under the crosshair.
                    if(own)at=new Vector3(width*NativeCombatNumbers.OwnX(n.Lane),height*(1-NativeCombatNumbers.OwnY(age)),1);
                    else{at=cameraView.WorldToScreenPoint(NativeArena.Vec(NativeCombatNumbers.World(n,age)))/scale;if(at.z<=0||at.x<-width*.05f||at.x>width*1.05f||at.y<-height*.05f||at.y>height*1.05f)continue;}
                    CombatNumberText(n,new Vector2(at.x,height-at.y),numberSize,age);
                }
            }
            else combatTrack.Clear();
            GUI.color=old;
            DeathLine();
            LeetStatus();var local=arena.Local;
            if(arena.Pve!=null)
            {
                var row=arena.Pve.Rows.TryGetValue(local.Seat.id,out var r)?r:null;
                foreach(var m in arena.Pve.Content.machines)if(ScreenPoint(new Vector3(m.x,2.62f,m.z),24,out var point)){int price=m.kind=="chip"?NativePveRules.ChipPrice(row?.Bought??0):m.kind=="recruit"?300:75;string value=m.kind=="med"&&row?.HealReady>arena.Time?Math.Ceiling(row.HealReady-arena.Time)+"s":"$"+price;var style=new GUIStyle(small){alignment=TextAnchor.MiddleCenter};GUI.Label(new Rect(point.x-80,height-point.y,160,50),m.kind.ToUpperInvariant()+"\n"+value,style);if(Vector3.Distance(local.Body.position,NativeArena.Vec(m.front.Position(.9f)))<2.2f)GUI.Label(new Rect(width/2-170,height-165,340,32),"F · "+(m.kind=="chip"?"Buy Chip":m.kind=="recruit"?"Hire recruit":"Heal")+"  "+value,label);}
                // The owner's pickups wear the glow texture's YOURS (NativePickupGlowPresentation.cs); this flat label stands in only without it.
                if(presentation?.PickupLabelDrawn!=true)foreach(var k in arena.Pve.Pickups)if(k.Owner==local&&arena.Time>=k.Born&&ScreenPoint(k.Position+Vector3.up*.65f,30,out var point))GUI.Label(new Rect(point.x-40,height-point.y,90,25),"YOURS",small);
            }
        }
    }
}
