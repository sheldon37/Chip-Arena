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
        // target's numbers of one kind within .16 s share one, in one of three lanes; numbers live 1.15 s (at most 64). In
        // first person (the setting, or the rifle scope) the view pilot's own heals feed the quiet total by the vitals
        // (HealReadout) instead, so none sits under the crosshair.
        private readonly NativeCombatNumbers combatTrack=new NativeCombatNumbers();private NativeCombatNumbers.Anchor combatAnchor;
        private bool FirstPersonView=>prefs.firstPerson||presentation.FirstPerson;
        private bool CombatAnchor(int id,out V3 at)
        {foreach(var p in arena.Pilots)if(p.Seat.id==id){at=NativeArena.Vec(p.Body.transform.position);return true;}at=default;return false;}
        private List<NativeCombatNumbers.Number> CombatNumbers()
        {combatAnchor??=CombatAnchor;int view=arena.ViewPilot?.Seat.id??0;return combatTrack.Update(arena.Events,arena.Time,arena.Generation,view,arena.Pve!=null,combatAnchor,FirstPersonView?view:0);}
        // One number centred on `c`: .combat-number translate(-50%,-50%) scale(1 + max(0, .16 - age) x 1.5), fading out over
        // its last .48 s (times `fade`); damage red, heals green, each with its drop shadow and dark outline.
        private static void CombatNumberText(NativeCombatNumbers.Number n,Vector2 c,float size,float age,float fade=1)
        {
            float alpha=NativeCombatNumbers.Alpha(age)*fade;U.Push(c,Vector2.zero,NativeCombatNumbers.Scale(age));
            NativeUi.Text(new Rect(c.x-70,c.y-size*.6f,140,size*1.2f),n.Label,size,NativeUi.Hex(n.Heal?NativeCombatNumbers.HealColor:NativeCombatNumbers.DamageColor,alpha),TextAnchor.MiddleCenter,bold:true,
                glow:NativeUi.Hex(n.Heal?NativeCombatNumbers.HealOutline:NativeCombatNumbers.DamageOutline,.9f*alpha),shadow:NativeUi.Hex(n.Heal?NativeCombatNumbers.HealShadow:NativeCombatNumbers.DamageShadow,alpha));U.Pop();
        }
        private readonly Dictionary<int,HealthLag> healthLags=new Dictionary<int,HealthLag>();private int hudGeneration=-1;
        // match-hud.css .death-announcement: top left, a holographic card (backdrop blur 4 px) with a magenta event edge; a new
        // line slides in over .26 s, the newest one with a .18 s glitch (none with Reduced effects).
        private void DeathLine()
        {
            var lines=presentation.DeathMessages(arena.Time).ToArray();if(lines.Length==0)return;float since=arena.Time-presentation.LatestDeathMessage(arena.Time),e=NativeUiMotion.Bezier(.2f,.8f,.2f,1,Mathf.Clamp01(since/.26f));
            float maxW=Mathf.Min(320,NativeUi.W*.42f),w=Mathf.Min(maxW,lines.Max(l=>NativeUi.Measure(l,13).x)+26),h=lines.Length*13*1.45f+14;var r=new Rect(22-10*(1-e),arena.Pve!=null?96:70,w,h);
            NativeUi.Backdrop(r,0,4,e);NativeHudChrome.Panel(r,e,NativeCyberTheme.Magenta(),NativeHudCyber.TagCut);NativeUi.Fill(new Rect(r.x+NativeHudChrome.Snap(1)+1,r.y+4,NativeHudChrome.Snap(2),r.height-8),NativeCyberTheme.Magenta(e));
            for(int i=0;i<lines.Length;i++)
            {
                var line=new Rect(r.x+13,r.y+7+i*13*1.45f,w-24,13*1.45f);if(i==lines.Length-1)NativeHudChrome.Glitch(line,lines[i],13,since,e,TextAnchor.MiddleLeft,prefs.reducedEffects,false,false);
                NativeUi.Text(line,lines[i],13,NativeCyberTheme.Ink(e),TextAnchor.MiddleLeft,shadow:new Color(0,0,0,e));
            }
            DevFeedTags(r,lines.Length,e);
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
            var r=new Rect(Mathf.Round(U.W/2-w/2),U.H*NativeLeetStatus.Top,w,h);Color ink=NativeCyberTheme.Good();
            // The L33t identity stays hacker green (the palette's good tone) on the cyber chamfer.
            U.Glow(r,NativeCyberTheme.Good(NativeLeetStatus.GlowAlpha),NativeLeetStatus.GlowSpread);
            NativeHudChrome.Cut(r,U.Hex(NativeLeetStatus.Fill,NativeLeetStatus.FillAlpha),NativeHudCyber.Cut);NativeHudChrome.Scan(r,NativeCyberTheme.Good(.05f),NativeHudCyber.Cut);
            NativeHudChrome.Outline(r,NativeCyberTheme.Good(),NativeHudCyber.Cut,NativeHudChrome.Corners.Diagonal,b);NativeHudChrome.Brackets(r,NativeCyberTheme.Good(),10);
            float x=r.x+b+NativeLeetStatus.PadX,y=r.y+b+NativeLeetStatus.PadY;
            // The tracking follows the last glyph too, so the browser's centred title sits half a letter-space left.
            U.Text(new Rect(x-NativeLeetStatus.TitleTracking*NativeLeetStatus.TitleSize/2,y,inner,NativeLeetStatus.TitleLine),title,NativeLeetStatus.TitleSize,ink,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:NativeLeetStatus.TitleTracking);
            U.Text(new Rect(x,y+NativeLeetStatus.TitleLine+NativeLeetStatus.Gap,inner,NativeLeetStatus.SubLine),sub,NativeLeetStatus.SubSize,ink,TextAnchor.MiddleCenter,mono:true);
        }
        // ---- Overhead bars ----------------------------------------------------------------------------------------------
        // One pilot's (or add's) overhead bar at `bar` (canvas units, from the projected head): a navy backing, the segmented
        // health in the side's tone (cyan on my side, danger for enemies; a low bar keeps its side's tone over a danger track)
        // with its damage trail, and the name above. `self` is this client's own pilot in third person: under its health it
        // also carries its resources (NativeHudCyber.SelfRows), fading when all are full and idle and when the bar would
        // cover the aim point at the screen centre.
        private float selfChangedAt=-100,selfSignature=float.NaN;
        private void Overhead(Rect bar,float ratio,float trail,float max,bool ally,string name,NativePilot self)
        {
            if(Event.current.type!=EventType.Repaint)return;bool low=ratio<.25f;Color tone=ally?NativeCyberTheme.Cyan():NativeCyberTheme.Danger();
            NativeHudCyber.Rows rows=self!=null?SelfRowsOf(self):NativeHudCyber.Rows.None;int count=0;for(int k=1;k<=64;k<<=1)if(((int)rows&k)!=0&&k!=(int)NativeHudCyber.Rows.Reload)count++;
            float rowH=2,rowGap=2,blockH=bar.height+count*(rowH+rowGap),alpha=1,rowsAlpha=1;
            if(self!=null){alpha=NativeHudCyber.AimFade(bar.x-2,bar.y-2,bar.width+4,blockH+4,U.W/2,U.H/2);rowsAlpha=SelfIdle(self,rows);}
            var back=new Rect(bar.x-2,bar.y-2,bar.width+4,blockH+4);NativeHudChrome.Cut(back,NativeCyberTheme.Void(.78f*alpha),2,NativeHudChrome.Corners.Diagonal);
            NativeHudChrome.Bar(bar,ratio*max,max,10,U.A(tone,alpha),low?NativeCyberTheme.Danger(.32f*alpha):NativeCyberTheme.Edge(.6f*alpha),trail*max,NativeCyberTheme.Ink(.8f*alpha),1);
            if(name!=null)U.Text(new Rect(bar.center.x-110,bar.y-27,220,23),name,12,NativeCyberTheme.Ink(alpha),TextAnchor.MiddleCenter,shadow:new Color(0,0,0,.85f*alpha));
            if(self!=null&&count>0)SelfRows(self,rows,new Rect(bar.x,bar.yMax+rowGap,bar.width,rowH),rowGap,alpha*rowsAlpha,alpha);
        }
        private NativeHudCyber.Rows SelfRowsOf(NativePilot p)
        {
            float t=arena.Time;bool charging=p.Fighter?.Kind=="charge";
            return NativeHudCyber.SelfRows(p.Weapon.@class,p.ReloadEnd>t,NativeRangedRules.Overdrive(t,p.OverdriveAt,p.OverdriveUntil),NativeRangedRules.Vent(t,p.OverdriveUntil,p.VentUntil),charging,CyberLordFeed.Handles(p.Seat.spec));
        }
        // Idle fade: the rows dim once every resource is full and has held still for NativeHudCyber.IdleAfter (instant with
        // Reduced effects, which skips the fade's motion).
        private float SelfIdle(NativePilot p,NativeHudCyber.Rows rows)
        {
            float t=arena.Time,cap=(float)Math.Ceiling(p.Weapon.ammo*p.Stats["ammoCapacity"]),maxStamina=(float)p.Stats["maxStamina"],maxMana=(float)p.Stats["maxMana"];
            float signature=p.Stamina*7+p.Mana*13+p.Ammo*17+(int)rows*31;if(!(signature==selfSignature)||t<selfChangedAt){selfSignature=signature;selfChangedAt=t;}
            bool full=p.Stamina>=maxStamina-.01f&&(p.Weapon.@class!="magic"&&!CyberLordFeed.Handles(p.Seat.spec)||p.Mana>=maxMana-.01f)&&(p.Weapon.@class!="ranged"||p.Ammo>=cap)&&(rows&(NativeHudCyber.Rows.Reload|NativeHudCyber.Rows.Heat|NativeHudCyber.Rows.Vent|NativeHudCyber.Rows.Charge))==0;
            float idle=t-selfChangedAt;if(prefs.reducedEffects&&idle>NativeHudCyber.IdleAfter)idle=NativeHudCyber.IdleAfter+NativeHudCyber.IdleFadeTime;return NativeHudCyber.IdleFade(idle,full);
        }
        // The rows, top down: the weapon resource (melee stamina, magic mana, ranged magazine pips or the reload sweep), then
        // Overdrive heat or its vent (machine gun), then a DEV fighter's charge; thin segmented bars in the palette's
        // resource tones (stamina good, amber while sprinting; mana violet; magazine amber; reload cyan; heat magenta to
        // danger; vent danger). Heat and vent name themselves in a small tag at the bar's right.
        private void SelfRows(NativePilot p,NativeHudCyber.Rows rows,Rect row,float gap,float a,float tagAlpha)
        {
            float t=arena.Time;var track=NativeCyberTheme.Edge(.65f*a);
            if((rows&NativeHudCyber.Rows.Stamina)!=0){float max=(float)p.Stats["maxStamina"];NativeHudChrome.Bar(row,p.Stamina,max,10,p.Pose.Sprinting?NativeCyberTheme.Warn(a):NativeCyberTheme.Good(a),track,gap:1);row.y+=row.height+gap;}
            if((rows&NativeHudCyber.Rows.Mana)!=0){float max=(float)p.Stats["maxMana"];NativeHudChrome.Bar(row,p.Mana,max,10,NativeCyberTheme.Violet(a),track,gap:1);row.y+=row.height+gap;}
            if((rows&NativeHudCyber.Rows.Ammo)!=0)
            {
                float cap=(float)Math.Ceiling(p.Weapon.ammo*p.Stats["ammoCapacity"]);
                if((rows&NativeHudCyber.Rows.Reload)!=0)NativeHudChrome.Line(row,NativeHudCyber.Reload(t,p.ReloadAt,p.ReloadEnd),NativeCyberTheme.CyanSoft(Mathf.Max(a,.85f*tagAlpha)),track);
                else NativeHudChrome.Bar(row,p.Ammo,cap,16,NativeCyberTheme.Warn(a),track,gap:1);row.y+=row.height+gap;
            }
            if((rows&NativeHudCyber.Rows.Heat)!=0)
            {
                float heat=NativeHudCyber.Heat(t,p.OverdriveAt,p.OverdriveUntil);NativeHudChrome.Line(row,heat,U.A(U.Mix(NativeCyberTheme.Magenta(),NativeCyberTheme.Danger(),heat),tagAlpha),track);
                OverheadTag(row,"OVERDRIVE",NativeCyberTheme.Magenta(tagAlpha));row.y+=row.height+gap;
            }
            if((rows&NativeHudCyber.Rows.Vent)!=0){NativeHudChrome.Line(row,NativeHudCyber.Cooling(t,p.OverdriveUntil,p.VentUntil),NativeCyberTheme.Danger(tagAlpha),track);OverheadTag(row,"VENT",NativeCyberTheme.Danger(tagAlpha));row.y+=row.height+gap;}
            if((rows&NativeHudCyber.Rows.Charge)!=0&&p.Fighter!=null){NativeHudChrome.Line(row,Mathf.Clamp01(p.Fighter.Charge),NativeCyberTheme.CyanSoft(tagAlpha),track);row.y+=row.height+gap;}
        }
        private static void OverheadTag(Rect row,string text,Color c)=>U.Text(new Rect(row.xMax+6,row.center.y-6,90,12),text,9,c,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f,shadow:new Color(0,0,0,.8f*c.a));
        private void CombatWorldHud(float scale,float width,float height)
        {
            if(menu||arena?.Local==null||cameraView==null||IntroActive||ReplayActive)return;
            if(hudGeneration!=arena.Generation){healthLags.Clear();hudGeneration=arena.Generation;}
            var physics=arena.Root.scene.GetPhysicsScene();var old=GUI.color;
            bool ScreenPoint(Vector3 p,float range,out Vector3 point){point=cameraView.WorldToScreenPoint(p)/scale;if(point.z<=0||Vector3.Distance(cameraView.transform.position,p)>range)return false;Vector3 delta=p-cameraView.transform.position;return !physics.Raycast(cameraView.transform.position,delta.normalized,out _,delta.magnitude-.08f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);}
            void Health(int id,Vector3 pos,float hp,float max,float worldWidth,bool ally,string name,bool lag,NativePilot self)
            {
                if(hp<=0||!ScreenPoint(pos,55,out var at))return;float ratio=Mathf.Clamp01(hp/Math.Max(1,max)),pixels=Mathf.Clamp(worldWidth*Screen.height/(2*Mathf.Tan(cameraView.fieldOfView*Mathf.Deg2Rad*.5f)*Math.Max(1,Vector3.Distance(cameraView.transform.position,pos)))/scale,16,85),x=at.x-pixels*.5f,y=height-at.y,trail=ratio;
                if(lag){if(!healthLags.TryGetValue(id,out var value)){value=new HealthLag();healthLags[id]=value;}float dt=arena.Time-value.Time;value.Time=arena.Time;if(dt<0||dt>.35f||ratio>=value.Value){value.Value=ratio;value.Hold=0;}else{value.Hold=ratio<value.Last?0:value.Hold+Math.Min(dt,.05f);if(value.Hold>.22f)value.Value=Math.Max(ratio,value.Value-Math.Min(dt,.05f)*.9f);}value.Last=ratio;trail=value.Value;}
                Overhead(new Rect(x,y,pixels,4),ratio,trail,max,ally,!string.IsNullOrEmpty(name)&&Vector3.Distance(cameraView.transform.position,pos)<32?name:null,self);
            }
            // Every pilot's health; only this client's own pilot (third person: the first-person view has no body to sit on)
            // also carries its resources, so another player's screen never shows mine and mine never shows theirs.
            foreach(var p in arena.Pilots){if(!p.Seat.connected||(prefs.firstPerson||presentation.FirstPerson)&&p==arena.ViewPilot||p.Seat.AuthorityBoss&&arena.Pve?.Hidden==true||presentation.SpawnHidesHud(p.Seat.id))continue;bool ally=p==arena.Local||match.SameSide(p.Seat,arena.Local.Seat);Health(p.Seat.id,p.Body.transform.position+Vector3.up*1.3f*(p.Seat.spec=="siren"?p.Pose.RigScale:p.Seat.devScale),p.Hp,(float)p.Stats["hp"],.72f,ally,match.Teams?(ally?"ALLY · ":"ENEMY · ")+p.Seat.name:p.Seat.name,true,p==arena.Local?p:null);}
            // PvE chip selection: the "SELECTING CHIP" tag over the names (NativeChipSelectHud.cs).
            ChipSelectTags(scale,height);
            // DEV: the DEV tags over revealed pilots (everyone) and the DEV panel's read-only overlays (NativeDevOverlays.cs).
            DevWorld(scale,height);
            foreach(var m in arena.Minions)if(m.Hp<m.MaxHp)Health(m.Id,m.Body.transform.position+Vector3.up*(m.Kind=="slender"?1.52f:m.Kind=="toy"?.85f:1.2f),m.Hp,m.MaxHp,m.Kind=="toy"?.42f:.6f,m.Owner==arena.Local||match.SameSide(m.Owner.Seat,arena.Local.Seat),null,false,null);
            // combat-numbers.ts FloatingCombatNumbers with lobby.css:92 / match-hud.css:3 .combat-number (clamp(18px,1.65vw,25px),
            // 850): red damage and green heals over every pilot, and in PvE the view pilot's hits on adds; shown only in the fight
            // and result phases, the track cleared otherwise (main.ts combatNumbers.update).
            if(match.Phase==NativePhase.Fight||match.Phase==NativePhase.Result)
            {
                float numberSize=Mathf.Clamp(NativeUi.W*.0165f,18,25);int view=arena.ViewPilot?.Seat.id??0;bool first=FirstPersonView;
                foreach(var n in CombatNumbers())
                {
                    // In first person my own heals are the readout by the vitals, never a number (one spawned just before the
                    // switch included).
                    if(n.Readout||first&&n.Heal&&n.Target==view)continue;
                    float age=Math.Max(0,arena.Time-n.At);Vector3 at;bool own=prefs.firstPerson&&n.Target==view;
                    // A first-person pilot cannot see their own head: their damage numbers sit under the crosshair.
                    if(own)at=new Vector3(width*NativeCombatNumbers.OwnX(n.Lane),height*(1-NativeCombatNumbers.OwnY(age)),1);
                    else{at=cameraView.WorldToScreenPoint(NativeArena.Vec(NativeCombatNumbers.World(n,age)))/scale;if(at.z<=0||at.x<-width*.05f||at.x>width*1.05f||at.y<-height*.05f||at.y>height*1.05f)continue;}
                    // Third person: my own heals over my head fade where they would cover the crosshair (Classic spawns them on it).
                    float fade=n.Heal&&n.Target==view?NativeCombatNumbers.AimFade(n.Label,numberSize,at.x,height-at.y,width/2,height/2):1;
                    CombatNumberText(n,new Vector2(at.x,height-at.y),numberSize,age,fade);
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
