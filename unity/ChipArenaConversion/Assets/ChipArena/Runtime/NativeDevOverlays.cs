using System;
using System.Collections.Generic;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV panel's read-only view tools and the DEV reveal (NativeDevPanel.cs owns the panel). Everything here reads the
    /// match (the authority's own data on the host, the replicated frame on a guest) and draws on the IMGUI canvas: pilot hit
    /// volumes (each pilot's capsule collider and NativeCombatRules' head box), melee hit volumes (the live swing's
    /// NativeMeleeRules sector), map hazard boxes, the nav graph, Expedition anchors, predicted shot paths (Core
    /// NativeSpecialRules.Ballistic on a copy), the event log, the frame / step / round-trip graph and the DEV ABUSE ON tag.
    /// It writes nothing back: no hitbox, timing, damage, cooldown, movement or network value changes. The reveal ("a
    /// developer is here") and the DEV tags on nameplates and kill-feed lines run on every client from the proposed
    /// replicated NativeSeat.devRevealed (Core NativeDevTags, dormant until Astra adds it) or a local sandbox preview.
    /// Repaint only; nothing allocates per frame once warm.
    /// </summary>
    public sealed partial class ConversionGame
    {
        private const int DevLineBudget=900;private int devLines;
        private string devNavMap;private Vector3[] devNavPos=Array.Empty<Vector3>();private int[] devNavA=Array.Empty<int>(),devNavB=Array.Empty<int>();private byte[] devNavKind=Array.Empty<byte>();
        private string devRevealUpper="";private static string[] devBannerTyped;

        // ---- The world (CombatWorldHud, after the overhead bars and chip tags) --------------------------------------------
        private void DevWorld(float scale,float height)
        {
            if(Event.current.type!=EventType.Repaint||arena==null||cameraView==null)return;
            DevTags(height);devLines=0;DevPulseMarks();DevMonsterTags();
            if(!DevPanelAvailable)return;
            if(devOverlayHazards)DevHazardBoxes();if(devOverlayNav)DevNavGraph();if(devOverlayAnchors)DevAnchors();
            if(devOverlayHit)DevHitVolumes();if(devOverlayMelee)DevMeleeVolumes();if(devOverlayShots)DevShotPaths();
        }
        // A world point on the canvas (false behind the camera).
        private bool DevScreen(Vector3 world,out Vector2 at)
        {
            var s=cameraView.WorldToScreenPoint(world);at=new Vector2(s.x/U.S,(Screen.height-s.y)/U.S);return s.z>0;
        }
        /// <summary>A world line on the canvas, clipped at the near plane, as a rotated 1.5-unit bar.</summary>
        private void DevLine(Vector3 a,Vector3 b,Color c,float width=1.5f)
        {
            if(devLines>=DevLineBudget||c.a<=0)return;var t=cameraView.transform;Vector3 eye=t.position,forward=t.forward;float near=cameraView.nearClipPlane+.05f;
            float da=Vector3.Dot(a-eye,forward)-near,db=Vector3.Dot(b-eye,forward)-near;if(da<0&&db<0)return;
            if(da<0)a=Vector3.Lerp(a,b,da/(da-db));else if(db<0)b=Vector3.Lerp(a,b,da/(da-db));
            DevScreen(a,out var pa);DevScreen(b,out var pb);
            if(pa.x<-50&&pb.x<-50||pa.y<-50&&pb.y<-50||pa.x>U.W+50&&pb.x>U.W+50||pa.y>U.H+50&&pb.y>U.H+50)return;
            Vector2 d=pb-pa;float len=d.magnitude;if(len<.5f)return;var mid=(pa+pb)*.5f;
            U.Push(mid,Vector2.zero,1,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);U.Fill(new Rect(mid.x-len*.5f,mid.y-width*.5f,len,width),c);U.Pop();devLines++;
        }
        private void DevRing(Vector3 center,float radius,Color c,int segments=16,float from=0,float to=Mathf.PI*2)
        {
            Vector3 last=center+new Vector3(Mathf.Sin(from),0,Mathf.Cos(from))*radius;
            for(int i=1;i<=segments;i++){float a=from+(to-from)*i/segments;var next=center+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*radius;DevLine(last,next,c);last=next;}
        }
        private static readonly Vector3[] DevSquare={new Vector3(-1,0,-1),new Vector3(1,0,-1),new Vector3(1,0,1),new Vector3(-1,0,1)};
        private void DevBox(Vector3 center,Vector3 half,Quaternion turn,Color c)
        {
            var up=new Vector3(0,half.y,0);
            for(int i=0;i<4;i++)
            {
                Vector3 a=Vector3.Scale(DevSquare[i],half),b=Vector3.Scale(DevSquare[(i+1)%4],half);
                DevLine(center+turn*(a-up),center+turn*(b-up),c);DevLine(center+turn*(a+up),center+turn*(b+up),c);DevLine(center+turn*(a-up),center+turn*(a+up),c);
            }
        }
        private void DevLabel(Vector3 world,string text,Color c)
        {
            if(string.IsNullOrEmpty(text)||!DevScreen(world,out var at)||Vector3.Distance(cameraView.transform.position,world)>40)return;
            U.Text(new Rect(at.x-80,at.y-16,160,14),text,9,c,TextAnchor.MiddleCenter,bold:true,mono:true,shadow:C.Void(.9f));
        }
        private void DevHitVolumes()
        {
            var local=DevLocal();
            foreach(var p in arena.Pilots)
            {
                if(!p.Seat.connected||p.Hp<=0||p.Collider==null||p.Body==null)continue;bool ally=p==local||local!=null&&match.SameSide(p.Seat,local.Seat);Color tone=ally?C.Cyan(.85f):C.Danger(.9f);
                var col=p.Collider;var tr=col.transform;var scale=tr.lossyScale;Vector3 axis=col.direction==0?tr.right:col.direction==2?tr.forward:tr.up;
                float r=col.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z)),half=Mathf.Max(0,col.height*.5f*Mathf.Abs(scale.y)-r);Vector3 c=tr.TransformPoint(col.center),top=c+axis*half,bottom=c-axis*half;
                DevRing(top,r,tone,14);DevRing(bottom,r,tone,14);
                for(int k=0;k<4;k++){float a=k*Mathf.PI*.5f;var o=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*r;DevLine(bottom+o,top+o,tone);}
                for(int k=0;k<2;k++){Vector3 side=k==0?Vector3.right:Vector3.forward;Vector3 last=top+side*r;for(int i=1;i<=6;i++){float a=Mathf.PI*i/6;var next=top+side*(Mathf.Cos(a)*r)+axis*(Mathf.Sin(a)*r);DevLine(last,next,tone);last=next;}last=bottom+side*r;for(int i=1;i<=6;i++){float a=Mathf.PI*i/6;var next=bottom+side*(Mathf.Cos(a)*r)-axis*(Mathf.Sin(a)*r);DevLine(last,next,tone);last=next;}}
                // The head box NativeCombatRules.Headshot tests (no headshot while down or juggled: dim).
                bool headOff=p.Down!=null||p.Juggle!=null;DevBox(p.Body.position+Vector3.up*NativeDevVolumes.HeadY,Vector3.one*NativeDevVolumes.HeadHalf,Quaternion.Euler(0,p.Yaw,0),headOff?C.Warn(.3f):C.Warn(.95f));
            }
        }
        private void DevMeleeVolumes()
        {
            float now=arena.Time;
            foreach(var p in arena.Pilots)
            {
                var s=p.Swing;if(s==null||p.Hp<=0||s.Air||s.Range<=0)continue;bool active=now>=s.HitAt&&now<=s.HitUntil;if(now>s.Until)continue;
                Vector3 origin=p.Body.position+Vector3.up*NativeDevVolumes.MeleeLift;float range=s.Range+NativeDevVolumes.PilotExtra,half=NativeDevVolumes.Cone+Mathf.Asin(Mathf.Min(.95f,NativeDevVolumes.PilotExtra/Mathf.Max(.1f,range)));
                float angle=NativeMeleeRules.Angle(s,Mathf.Clamp(now,s.HitAt,s.HitUntil));Color tone=active?C.Magenta(.95f):C.Magenta(.35f);
                DevRing(origin,range,tone,10,angle-half,angle+half);
                var l=origin+new Vector3(Mathf.Sin(angle-half),0,Mathf.Cos(angle-half))*range;var rr=origin+new Vector3(Mathf.Sin(angle+half),0,Mathf.Cos(angle+half))*range;DevLine(origin,l,tone);DevLine(origin,rr,tone);
                // The sweep across the contact window, and the ±1.6 m height band at the sector's edge.
                float a0=NativeMeleeRules.Angle(s,s.HitAt),a1=NativeMeleeRules.Angle(s,s.HitUntil);DevRing(origin,range,C.Violet(.45f),12,Mathf.Min(a0,a1)-half,Mathf.Max(a0,a1)+half);
                var band=Vector3.up*NativeDevVolumes.MeleeHeight;DevLine(l-band,l+band,U.A(tone,.5f));DevLine(rr-band,rr+band,U.A(tone,.5f));
            }
        }
        private void DevHazardBoxes()
        {
            var hazards=arena.Map.hazards;if(hazards==null)return;
            foreach(var h in hazards)
            {
                if(h==null)continue;Color tone=h.lethal?C.Danger(.85f):C.Warn(.8f);bool drawn=false;
                if(h.size.x>0||h.size.y>0||h.size.z>0){var c=NativeArena.Vec(h.center);DevBox(c,NativeArena.Vec(h.size)*.5f,Quaternion.identity,tone);DevLabel(c+Vector3.up*(h.size.y*.5f+.3f),h.id,tone);drawn=true;}
                if(h.zones!=null)foreach(var z in h.zones){if(z==null)continue;var c=NativeArena.Vec(z.center);DevBox(c,NativeArena.Vec(z.size)*.5f,Quaternion.identity,U.A(tone,.7f));if(!drawn){DevLabel(c+Vector3.up*(z.size.y*.5f+.3f),h.id,tone);drawn=true;}}
                var a=NativeArena.Vec(h.a);var b=NativeArena.Vec(h.b);if((a-b).sqrMagnitude>.01f){DevLine(a,b,tone,2);if(h.radius>0){DevRing(a,h.radius,U.A(tone,.6f),12);DevRing(b,h.radius,U.A(tone,.6f),12);}if(!drawn)DevLabel((a+b)*.5f+Vector3.up*.5f,h.id,tone);}
            }
        }
        private void DevNavGraph()
        {
            if(devNavMap!=arena.MapId)DevNavBuild();if(devNavA.Length==0)return;var eye=cameraView.transform.position;
            for(int i=0;i<devNavA.Length;i++)
            {
                var a=devNavPos[devNavA[i]];var b=devNavPos[devNavB[i]];if((a-eye).sqrMagnitude>45*45&&(b-eye).sqrMagnitude>45*45)continue;
                DevLine(a,b,devNavKind[i]==1?C.Violet(.7f):devNavKind[i]==2?C.Warn(.7f):C.Cyan(.45f),1);
            }
            for(int i=0;i<devNavPos.Length;i++){var p=devNavPos[i];if((p-eye).sqrMagnitude>30*30)continue;DevLine(p-Vector3.right*.12f,p+Vector3.right*.12f,C.CyanSoft(.8f),1);DevLine(p-Vector3.forward*.12f,p+Vector3.forward*.12f,C.CyanSoft(.8f),1);}
        }
        // The map's nav nodes and links by index, once per map (kinds: 0 walk, 1 jump, 2 drop / fall / other).
        private void DevNavBuild()
        {
            devNavMap=arena.MapId;var nav=arena.Map.nav;if(nav?.nodes==null||nav.links==null){devNavPos=Array.Empty<Vector3>();devNavA=devNavB=Array.Empty<int>();devNavKind=Array.Empty<byte>();return;}
            var index=new Dictionary<string,int>(nav.nodes.Length);devNavPos=new Vector3[nav.nodes.Length];for(int i=0;i<nav.nodes.Length;i++){devNavPos[i]=NativeArena.Vec(nav.nodes[i].pos);if(nav.nodes[i].id!=null)index[nav.nodes[i].id]=i;}
            var a=new List<int>(nav.links.Length);var b=new List<int>(nav.links.Length);var k=new List<byte>(nav.links.Length);
            foreach(var l in nav.links){if(l==null||l.a==null||l.b==null||!index.TryGetValue(l.a,out int ia)||!index.TryGetValue(l.b,out int ib))continue;a.Add(ia);b.Add(ib);k.Add((byte)(l.kind=="walk"?0:l.kind=="jump"?1:2));}
            devNavA=a.ToArray();devNavB=b.ToArray();devNavKind=k.ToArray();
        }
        private void DevAnchors()
        {
            var anchors=ExpeditionView?.anchors;if(anchors==null)return;
            foreach(var a in anchors){if(a==null)continue;var p=new Vector3(a.x,a.y,a.z);Color tone=a.kind=="reward"?C.Good(.9f):a.kind=="chest"?C.Warn(.9f):a.kind=="door"?C.Violet(.9f):C.CyanSoft(.9f);DevRing(p,.6f,tone,12);DevLine(p,p+Vector3.up*2.2f,tone);DevLabel(p+Vector3.up*2.5f,a.id,tone);}
        }
        private void DevShotPaths()
        {
            foreach(var s in arena.Bullets)
            {
                if(s==null)continue;var v=NativeArena.Vec(s.Velocity);Vector3 at=s.Position;float life=Mathf.Min(.6f,s.Life);Color tone=C.Good(.85f);
                for(int i=0;i<10&&life>0;i++){float h=Mathf.Min(.06f,life);var d=NativeArena.Vec(NativeSpecialRules.Ballistic(ref v,s.Gravity,h));var next=at+d;DevLine(at,next,U.A(tone,1-i*.08f),1.5f);at=next;life-=h;}
                DevLine(s.Position-Vector3.up*.08f,s.Position+Vector3.up*.08f,C.Ink(),2);
            }
        }

        // A DEV-forced hazard pulse's mark for everyone: a short magenta ring and "DEV" over the pulse for its two seconds.
        private void DevPulseMarks()
        {
            foreach(var h in arena.DevHazardPulses)
            {
                if(h==null)continue;float age=arena.Time-h.at;if(age<0||age>2)continue;float a=1-age/2,r=.8f+age*1.6f;
                DevRing(h.position+Vector3.up*.05f,r,C.Magenta(.85f*a),20);DevLabel(h.position+Vector3.up*2.2f,NativeDevTags.Tag,C.Magenta(a));
            }
        }
        // The room's DEV time scale (NativeGameFrame.devTimeScale) for everyone while it is not 1.
        private void DevTimeTag(float W)
        {
            if(Event.current.type!=EventType.Repaint||arena==null||Mathf.Abs(arena.DevTimeScale-1)<.001f)return;
            string v=NativeDevText.Value(arena.DevTimeScale,2,"×");float vw=U.Measure(v,10,true,true).x,tw=U.Measure("DEV TIME",9,true,true,.14f).x;var r=new Rect(W*.5f-(tw+vw+34)*.5f,52,tw+vw+34,22);
            H.Tag(r,C.Magenta(),1);U.Text(new Rect(r.x+12,r.y,tw+4,r.height),"DEV TIME",9,C.Magenta(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);U.Text(new Rect(r.xMax-vw-12,r.y,vw+4,r.height),v,10,C.Ink(),TextAnchor.MiddleRight,bold:true,mono:true);
        }

        // ---- DEV tags: nameplates and the kill feed (every client; dormant until devRevealed exists) ----------------------
        private NativeSeat DevSeat(int id){if(match==null)return null;foreach(var s in match.Seats)if(s.id==id)return s;return null;}
        private bool DevTagged(NativeSeat seat,float now)=>seat!=null&&(NativeDevTags.Revealed(seat)||devReveal.Previewing(seat.id,now));
        private void DevTags(float height)
        {
            float now=Time.unscaledTime;var physics=arena.Root.scene.GetPhysicsScene();var eye=cameraView.transform.position;
            foreach(var p in arena.Pilots)
            {
                if(!p.Seat.connected||p.Hp<=0||!DevTagged(p.Seat,now)||FirstPersonView&&p==arena.ViewPilot)continue;
                // Where the overhead bar's name sits (NativeCombatHud.cs: the bar at the body + 1.3 m, the name 27 over it), with its range and occlusion.
                var at=p.Body.transform.position+Vector3.up*1.3f*(p.Seat.spec=="siren"?p.Pose.RigScale:p.Seat.devScale);float metres=Vector3.Distance(eye,at);if(metres>32||!DevScreen(at,out var bar))continue;
                Vector3 delta=at-eye;if(physics.Raycast(eye,delta.normalized,out _,delta.magnitude-.08f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))continue;
                float nameW=Mathf.Min(220,U.Measure(p.Seat.name,12).x),w=30,h=15;var r=new Rect(Mathf.Round(bar.x-nameW*.5f-w-5),Mathf.Round(bar.y-27+4),w,h);
                DevDevTag(r,1,now);
            }
        }
        // The DEV tag: a magenta chamfered plate, "DEV" in mono, a soft breathing glow.
        private void DevDevTag(Rect r,float alpha,float now)
        {
            float breath=prefs.reducedEffects?.5f:.35f+.25f*Mathf.Sin(now*3.2f);U.Glow(r,C.Magenta(breath*alpha*.6f),8);
            H.Cut(r,C.Magenta(.88f*alpha),NativeHudCyber.TagCut,H.Corners.Diagonal);H.Outline(r,C.CyanSoft(.6f*alpha),NativeHudCyber.TagCut);
            U.Text(r,NativeDevTags.Tag,9,C.Ink(alpha),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.14f,shadow:C.Void(.6f*alpha));
        }
        /// <summary>DeathLine's hook: a DEV tag beside each kill-feed line whose killer or victim is revealed.</summary>
        private void DevFeedTags(Rect card,int count,float alpha)
        {
            if(Event.current.type!=EventType.Repaint||arena==null)return;float now=Time.unscaledTime;
            for(int i=0;i<count;i++)
            {
                var item=presentation.DeathItemAt(arena.Time,i);if(item==null)continue;
                if(!DevTagged(DevSeat(item.Owner),now)&&!DevTagged(DevSeat(item.Victim),now))continue;
                DevDevTag(new Rect(card.xMax+6,card.y+7+i*13*1.45f+2,30,15),alpha,now);
            }
        }

        // ---- The reveal: "a developer is here" ----------------------------------------------------------------------------
        private void DevRevealStart(int seat,string name,bool preview)
        {
            devReveal.Start(seat,name,Time.unscaledTime,preview);devRevealUpper=(name??"").ToUpperInvariant();
            nativeAudio?.Cue("ui.go","go",null,.55f,mine:true);
        }
        private void DevRevealMoment(float W,float Hh,float now)
        {
            if(!devReveal.Active(now)||Event.current.type!=EventType.Repaint)return;float age=devReveal.Age(now),a=NativeDevReveal.Alpha(age);if(a<=0)return;bool reduced=prefs.reducedEffects;
            if(devBannerTyped==null){var t=NativeDevTags.Banner;devBannerTyped=new string[t.Length+1];for(int i=0;i<=t.Length;i++)devBannerTyped[i]=t.Substring(0,i);}
            float bw=Mathf.Min(980,W*.62f),bh=118,cx=W*.5f,by=Hh*.17f;
            // The scan flash across the whole canvas, and the banner opening from the centre.
            float flash=reduced?0:NativeDevReveal.Flash(age);if(flash>0){U.Fill(new Rect(0,by+bh*.5f-1,W,2),C.CyanSoft(flash));U.Glow(new Rect(0,by+bh*.5f-4,W,8),C.Cyan(.5f*flash),24);}
            float open=NativeDevReveal.Open(age);var r=new Rect(cx-bw*.5f*open,by,Mathf.Max(2,bw*open),bh);
            U.Glow(r,C.Magenta(.35f*a),36);H.Panel(r,a,C.Magenta(),NativeHudCyber.Cut,H.Corners.Diagonal,true,.92f);H.Brackets(new Rect(r.x-6,r.y-6,r.width+12,r.height+12),C.Cyan(.8f*a),14,H.Corners.None);
            // The shards: data fragments flying out of the banner (none with Reduced effects).
            for(int i=0;i<NativeDevReveal.Shards;i++)
            {
                if(!NativeDevReveal.Shard(i,age,reduced,out float sx,out float sy,out float sa,out float size,out int tone))continue;
                Color sc=tone==0?C.Cyan():tone==1?C.Magenta():tone==2?C.Violet():C.Ink();U.Fill(new Rect(cx+sx-size*.5f,by+bh*.5f+sy-size*.25f,size,Mathf.Max(1.5f,size*.5f)),U.A(sc,sa*a));
            }
            if(open<.6f)return;float ta=a*Mathf.Clamp01((open-.6f)/.4f);
            // The eyebrow, typed in; the name rising with a glitch; the DEV badge; the guardrail line.
            // Typed left to right inside the full line's box, so the letters never shift as it types.
            var t2=NativeDevTags.Banner;int typed=NativeDevReveal.Typed(age,t2.Length,reduced);float full=U.Measure(t2,14,true,true,.32f).x;var eyebrow=new Rect(cx-full*.5f,r.y+16,full+8,18);
            U.Text(eyebrow,devBannerTyped[typed],14,C.CyanSoft(ta),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.32f,glow:C.Cyan(.4f*ta));
            float ni=NativeDevReveal.NameIn(age);if(ni>0)
            {
                float na=ta*ni,dy=14*(1-ni);string name=devRevealUpper.Length>0?devRevealUpper:"DEVELOPER";float nw=Mathf.Min(r.width-140,U.Measure(name,36,true,false,.04f).x);var line=new Rect(cx-nw*.5f,r.y+40+dy,nw,44);
                H.Glitch(line,name,36,age-.55f,na,TextAnchor.MiddleCenter,reduced,true,false,.04f);U.Text(line,name,36,C.Ink(na),TextAnchor.MiddleCenter,bold:true,tracking:.04f,glow:C.Magenta(.35f*na));
                DevDevTag(new Rect(line.xMax+12,line.center.y-10,40,20),na,now);
                U.Text(new Rect(r.x,r.yMax-26,r.width,16),"MATCH-ONLY FUN  ·  YOUR PROGRESS IS SAFE",10,C.Muted(na),TextAnchor.MiddleCenter,mono:true,tracking:.16f);
            }
            DevRevealMarker(age,a);
        }
        // Over the revealed pilot: a ring pulsing out round the nameplate and a chevron pointing at it.
        private void DevRevealMarker(float age,float a)
        {
            var p=DevPilot(devReveal.Seat);if(p==null||p.Body==null||cameraView==null||FirstPersonView&&p==arena.ViewPilot)return;
            if(!DevScreen(p.Body.transform.position+Vector3.up*1.9f,out var at))return;float k=NativeDevReveal.Ring(age),r=18+42*k;
            for(int i=0;i<24;i++){float a0=Mathf.PI*2*i/24f,a1=Mathf.PI*2*(i+.6f)/24f;var p0=at+new Vector2(Mathf.Cos(a0),Mathf.Sin(a0)*.55f)*r;var p1=at+new Vector2(Mathf.Cos(a1),Mathf.Sin(a1)*.55f)*r;var d=p1-p0;var mid=(p0+p1)*.5f;U.Push(mid,Vector2.zero,1,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);U.Fill(new Rect(mid.x-d.magnitude*.5f,mid.y-1,d.magnitude,2),C.Magenta((1-k)*a));U.Pop();}
            for(int s=-1;s<=1;s+=2){var tip=new Vector2(at.x,at.y-22-4*Mathf.Sin(age*6));U.Push(tip,Vector2.zero,1,s*45);U.Fill(new Rect(tip.x-1,tip.y-12,2,12),C.CyanSoft(a));U.Pop();}
        }

        // ---- HUD pieces: the DEV ABUSE ON tag, the fly tag, the graph and the event log -----------------------------------
        private void DevIndicator()
        {
            if(Event.current.type!=EventType.Repaint)return;float now=Time.unscaledTime,breath=prefs.reducedEffects?1:.75f+.25f*Mathf.Sin(now*2.4f);
            float tw=U.Measure(NativeDevTags.Indicator,10,true,true,.14f).x,kw=H.KeyWidth(NativeDevKeys.Panel,14);var r=new Rect(24,22,tw+kw+40,24);
            U.Glow(r,C.Magenta(.25f*breath),10);H.Tag(r,C.Magenta(),1);U.Fill(new Rect(r.x+10,r.center.y-3,6,6),C.Magenta(breath));
            U.Text(new Rect(r.x+22,r.y,tw+4,r.height),NativeDevTags.Indicator,10,C.Ink(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);H.Key(new Rect(r.xMax-kw-8,r.center.y-7,kw,14),NativeDevKeys.Panel,1,C.Magenta());
            // My replicated DEV state under it: god, infinite ammo, no cooldowns, scale, and the progression note.
            var me=DevLocal();if(me==null)return;float px=r.x,py=r.yMax+4;
            if(me.DevGod)px=DevHudPill(px,py,"GOD",C.Good());if(me.DevInfiniteAmmo)px=DevHudPill(px,py,"INF AMMO",C.Good());if(me.DevNoCooldowns)px=DevHudPill(px,py,"NO CD",C.Good());
            if(me.Seat.devScale!=1)px=DevHudPill(px,py,me.Seat.devScale<1?"SHRUNK":"GROWN",C.Violet());if(me.Seat.devCommandsRan)DevHudPill(px,py,NativeDevTags.NoProgress,C.Magenta());
        }
        private float DevHudPill(float x,float y,string text,Color tone)
        {float w=U.Measure(text,8,true,true,.1f).x+16;var r=new Rect(x,y,w,17);H.Tag(r,tone,1);U.Text(new Rect(r.x+5,r.y,r.width-6,r.height),text,8,C.Ink(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);return x+w+5;}
        private void DevFlyTag(float W)
        {
            if(Event.current.type!=EventType.Repaint)return;float y=(devGraphOn?150:56)+(Mathf.Abs(arena.DevTimeScale-1)<.001f?0:24);var lines=U.Wrap(NativeDevKeys.FlyHelp,10,560,false,1);float w=Mathf.Min(620,W-40);var r=new Rect(W*.5f-w*.5f,y,w,40);
            H.Panel(r,1,C.Cyan(),NativeHudCyber.TagCut,H.Corners.Diagonal,true,.8f);U.Text(new Rect(r.x+14,r.y+4,140,16),"FLY CAMERA",10,C.Cyan(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            U.Text(new Rect(r.xMax-170,r.y+4,156,16),NativeDevText.Value(devFly.Speed,0," m/s"),10,C.CyanSoft(),TextAnchor.MiddleRight,bold:true,mono:true);if(lines.Count>0)U.Text(new Rect(r.x+14,r.y+20,r.width-28,16),lines[0],10,C.Muted(),TextAnchor.MiddleLeft,mono:true);
        }
        private void DevGraphPanel(float W)
        {
            if(Event.current.type!=EventType.Repaint)return;float w=340,hgt=86,top=Mathf.Abs(arena.DevTimeScale-1)<.001f?56:80;var r=new Rect(W*.5f-w*.5f,top,w,hgt);H.Panel(r,1,C.Cyan(),NativeHudCyber.TagCut,H.Corners.Diagonal,true,.86f);
            float frame=devFrameGraph.Average(),fps=frame>0?1000/frame:0;
            U.Text(new Rect(r.x+12,r.y+6,90,14),NativeDevText.Value(fps,0," fps"),11,C.Ink(),TextAnchor.MiddleLeft,bold:true,mono:true);
            U.Text(new Rect(r.x+96,r.y+6,90,14),NativeDevText.Value(frame,1," ms"),10,C.CyanSoft(),TextAnchor.MiddleLeft,mono:true);
            U.Text(new Rect(r.x+170,r.y+6,80,14),NativeDevText.Value(devStepGraph.Average(),2," ms"),10,C.Violet(),TextAnchor.MiddleLeft,mono:true);
            U.Text(new Rect(r.xMax-92,r.y+6,80,14),Online?NativeDevText.Value(devRttGraph.Last,0," ms"):"OFFLINE",10,C.Good(),TextAnchor.MiddleRight,mono:true);
            // Bars: frame time (cyan, 33 ms tall), the simulation step under it (violet), round trip as a line (green, 200 ms tall).
            var plot=new Rect(r.x+12,r.y+26,r.width-24,hgt-36);U.Fill(new Rect(plot.x,plot.yMax-plot.height*(16.7f/33f),plot.width,H.Px),C.Edge(.9f));
            int n=devFrameGraph.Count;float bw=plot.width/Mathf.Max(1,devFrameGraph.Capacity);
            for(int i=0;i<n;i++){float v=Mathf.Clamp01(devFrameGraph.At(i)/33f),sv=Mathf.Clamp01((i<devStepGraph.Count?devStepGraph.At(i):0)/33f);float x=plot.x+(devFrameGraph.Capacity-n+i)*bw;U.Fill(new Rect(x,plot.yMax-plot.height*v,Mathf.Max(1,bw-.5f),plot.height*v),v>.75f?C.Danger(.8f):C.Cyan(.55f));if(sv>0)U.Fill(new Rect(x,plot.yMax-plot.height*sv,Mathf.Max(1,bw-.5f),plot.height*sv),C.Violet(.8f));}
            int m=devRttGraph.Count;float rw=plot.width/Mathf.Max(1,devRttGraph.Capacity);for(int i=0;i<m;i++){float v=Mathf.Clamp01(devRttGraph.At(i)/200f);U.Fill(new Rect(plot.x+(devRttGraph.Capacity-m+i)*rw,plot.yMax-plot.height*v-1,Mathf.Max(1,rw),2),C.Good(.9f));}
        }
        private void DevLogPanel(float W,float Hh)
        {
            if(Event.current.type!=EventType.Repaint)return;float w=Mathf.Min(460,W*.3f),y=Mathf.Max(150,Hh*.24f),hgt=Mathf.Min(300,Hh*.42f);var r=new Rect(24,y,w,hgt);
            H.Panel(r,1,C.Violet(),NativeHudCyber.TagCut,H.Corners.Diagonal,true,.82f);U.Text(new Rect(r.x+12,r.y+6,120,14),"EVENT LOG",10,C.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            U.Text(new Rect(r.xMax-140,r.y+6,128,14),devLogMine?"MINE · F1":"ALL · F1",9,C.Muted(),TextAnchor.MiddleRight,mono:true);
            float ly=r.y+26;int self=arena.LocalId;
            for(int i=0;i<devLog.Count&&ly+14<=r.yMax-6;i++)
            {
                var e=devLog[i];if(!NativeDevLog.Shows(e,devLogFilter,devLogMine,self))continue;
                Color tone=e.Category==NativeDevLogFilter.Damage?C.Danger(.95f):e.Category==NativeDevLogFilter.Deaths?C.Magenta():e.Category==NativeDevLogFilter.Defense?C.CyanSoft():e.Category==NativeDevLogFilter.Abilities?C.Violet():e.Category==NativeDevLogFilter.Heals?C.Good():e.Category==NativeDevLogFilter.Pve?C.Warn():C.Muted();
                var lines=U.Wrap(e.Text,10,r.width-24,false,1);if(lines.Count>0)U.Text(new Rect(r.x+12,ly,r.width-24,14),lines[0],10,tone,TextAnchor.MiddleLeft,mono:true);ly+=15;
            }
        }
    }

    public sealed partial class ArenaPresentation
    {
        // The forced hazard pulses seen this frame and the frame before (a record lives two seconds; each plays once).
        private HashSet<long> devPulsesNow=new HashSet<long>(),devPulsesBefore=new HashSet<long>();
        /// <summary>Plays each new NativeArena.DevHazardPulses record (host: the authority's list; guest: NativeGameFrame.devHazards)
        /// once through the shared effects renderer with its hazard's cues (Core NativeDevHazardLook), at the record's position.
        /// A record first seen late (a join) is skipped. Presentation only; the scheduled hazard looks are not retimed.</summary>
        public void DevHazardPulses(NativeArena arena)
        {
            var swap=devPulsesBefore;devPulsesBefore=devPulsesNow;devPulsesNow=swap;devPulsesNow.Clear();if(arena==null)return;
            var hazards=arena.Map.hazards;
            foreach(var h in arena.DevHazardPulses)
            {
                if(h?.id==null)continue;long key=((long)h.id.GetHashCode()<<32)^(uint)BitConverter.SingleToInt32Bits(h.at);devPulsesNow.Add(key);
                if(devPulsesBefore.Contains(key)||arena.Time-h.at>.6f||blasts==null)continue;
                MapHazard hz=null;if(hazards!=null)foreach(var x in hazards)if(x!=null&&x.id==h.id){hz=x;break;}
                blasts.DevHazardPulse(h.position,hz?.kind,NativeDevHazardLook.Reach(hz),BlastQuality);
            }
        }
        /// <summary>The `index`th death announcement shown at `time`, in DeathMessages' order (its 1.05-5.15 s window), or null.</summary>
        public NativeBroadcastItem DeathItemAt(float time,int index)
        {
            int n=0;foreach(var i in broadcasts.Items){if(!(time>=i.At+1.05f&&time<i.At+5.15f))continue;if(n==index)return i;n++;}return null;
        }
    }
}
