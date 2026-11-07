using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Cyber Lord's ranged-mode effects (Core/CyberLordRanged.cs; docs/CYBER-LORD.md "Ranged mode"), part of its world
    /// effects and keyed the same way: every Cyber Lord's view (the mode and its switch, the ranged casts' windows, the
    /// lance's and the beam's lines, the field's throw and life, the swap's path and its mine), the hardlight shots the scene
    /// holds (ICyberShots: replicated shots of Spec cyberlord, or the Dev Sandbox preview's) and the ranged hits and impacts.
    /// - the key-5 reconfiguration: a glitch flash at both fists, the shell's shards scattering out (or snapping in), a hex
    ///   ring, an RGB split of the body;
    /// - Bit Bolt: the emitter's muzzle flash, the bolt (a white head, an RGB-split tail, trailing pixels), its impact;
    /// - Ping Lance: the charge at the cannon emitter with a dashed telegraph down the aim, the instant lance with pings
    ///   running along it, the target's reticle;
    /// - Lag Field: the tumbling data cube on its arc, upright telegraph posts showing the field's edge before it lands, the
    ///   field (a hexagonal hardlight dome of rings over a ring of stuttering posts, lagging packets inside, a pulse per
    ///   tick, the pilots inside glitching), its collapse;
    /// - Proxy Swap: the blink's pixel streak, the RGB-split proxy left at the start that collapses into the hardlight mine,
    ///   the mine's quickening fuse and the burst's edge posts, the burst;
    /// - Root Access: the two-handed charge (a growing core, converging streaks, spinning hex rings) with the danger line down
    ///   the aim from TellFrom, the railbeam (a white core in a cyan sheath with magenta edges and code blocks spiralling
    ///   along it) and its impact.
    /// Nothing lies flat within 12 cm of the floor (the owner's rule): the field's rings float, the telegraphs stand up.
    /// Reduced effects: no flicker or strobe, softer flashes, fewer pieces; the low tier halves the counts. The first-person
    /// pilot sees its own fist effects on the view layer and nothing that would fill the camera.
    /// </summary>
    public sealed partial class NativeCyberLordFx
    {
        /// <summary>The ranged effects' lives (s): the switch, the bolt's muzzle flash, the lance's beam, the beam's, an impact,
        /// the field's collapse, the mine's burst.</summary>
        public const float SwitchLife=.45f,MuzzleLife=.09f,LanceLife=.22f,BeamLife=.4f,ImpactLife=.35f,FieldCollapse=.3f,BurstLife=.45f;
        /// <summary>The emitters' reach ahead of the fist centres (m) and the height floating rings keep over the floor.</summary>
        public const float EmitterAhead=.09f,RingFloor=.2f;
        /// <summary>The first-person pilot's own beams (the lance, the railbeam): the near part on the view layer from its view
        /// emitters (NearBeam long), the rest in the world from FarFrom down the line off the replicated start, so a line aimed
        /// straight away from the camera still reads.</summary>
        public const float NearBeam=.9f,FarFrom=1.2f;
        private Vector3 eye;
        // A beam as a true-perspective ribbon through `from`-`to` turned to face the eye (the axis cards lie in the camera plane,
        // so a beam aimed at or away from the camera, the target's or the shooter's own view, would shrink to a dot).
        private void Beam(ICyberFxSink s,bool view,Vector3 from,Vector3 to,float width,Vector3 color)
        {
            var dir=to-from;float len=dir.Length();if(!(len>1e-3f)||!(width>1e-5f))return;dir/=len;
            var side=Vector3.Cross(dir,eye-(from+to)*.5f);if(side.LengthSquared()<1e-8f)side=Vector3.Cross(dir,Up);if(side.LengthSquared()<1e-8f)side=Vector3.UnitX;
            side=Vector3.Normalize(side)*(width*.5f);s.Band(view,from-side,to-side,to+side,from+side,1,1,color);
        }
        private static Vector3 Aim(Actor a)
        {
            double y=a.Yaw*Math.PI/180,p=MeleeMath.Clamp(a.Pitch,-75,75)*Math.PI/180;return new Vector3((float)(Math.Sin(y)*Math.Cos(p)),(float)Math.Sin(p),(float)(Math.Cos(y)*Math.Cos(p)));
        }
        /// <summary>One pilot's ranged-mode effects (the switch, the casts, the field and the mine).</summary>
        private void Ranged(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,float q,bool low,bool me)
        {
            bool reduced=q<.5f;var feet=scene.Feet(a);var f=Forward(v.Yaw);var r=Right(f);var aim=Aim(a);eye=scene.Eye;
            Vector3 lf,rf;bool hasL=me?scene.ViewFist(true,out lf):scene.Fist(a,true,out lf),hasR=me?scene.ViewFist(false,out rf):scene.Fist(a,false,out rf);
            if(!hasL)lf=feet+Up*1.25f+f*.42f-r*.1f;if(!hasR)rf=feet+Up*1.25f+f*.42f+r*.1f;
            var le=lf+(me?f*.03f:aim*EmitterAhead);var re=rf+(me?f*.03f:aim*EmitterAhead);
            // ---- the reconfiguration ----
            float sw=time-v.ModeAt;
            if(v.ModeAt>-90&&sw>=0&&sw<SwitchLife)
            {
                long seed=a.Id*1931L+(long)Math.Round(v.ModeAt*240);float u=sw/SwitchLife,flash=reduced?.6f*(float)Math.Sin(Math.PI*u):1-Smooth(u/.35f);
                for(int h=0;h<2;h++)
                {
                    var at=h==0?lf:rf;s.Sprite(me,at,Lin(White,2.6f*flash),me?.06f:.22f,Hot);if(!me)s.Glow(at,Lin(Cyan,1.6f*flash),0,.35f+.2f*u,Core,u,seed+h);
                    s.Sprite(me,at,Lin(Cyan,1.8f*(1-u)),(me?.05f:.12f)+(me?.08f:.35f)*Smooth(u),RingSprite);
                    // The shell's shards: out from the fist into the emitter (to ranged), in from around it (to melee).
                    int n=reduced?3:low?4:7;
                    for(int i=0;i<n;i++)
                    {
                        float ang=i*2.39996f+H(seed+i+h*31,3)*1.2f;var dir=Vector3.Normalize(r*(float)Math.Cos(ang)+Up*(float)Math.Sin(ang)+f*(H(seed+i,5)-.3f));
                        float k=v.Ranged?Smooth(u):1-Smooth(u);var p=at+dir*(.04f+.22f*k);float z=.028f*(1-u*.6f);
                        s.Voxel(me,WeaponShape.Cube,p,Quaternion.CreateFromAxisAngle(Up,ang+u*6),new Vector3(z,z,z*.5f),Lin(i%2==0?Cyan:Magenta,2.2f*(1-u)));
                    }
                }
                // The body's RGB split (not on the first-person pilot's own camera).
                if(!me&&ghostCount<MaxGhosts-2&&u<.6f)
                {
                    int step=(int)(time*(reduced?6:24));if(reduced||H(seed+step,7)>.3f)
                    {float off=(reduced?.02f:.03f+.03f*H(seed+step,9))*(1-u/.6f);ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=r*off,Tint=Lin(Magenta,1.4f),Opacity=.4f*(1-u/.6f)};ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=-r*off,Tint=Lin(Cyan,1.4f),Opacity=.4f*(1-u/.6f)};}
                }
            }
            // The cyber mage's hands (NativeCyberLordPlasmaFx.cs): the fireballs held over the palms and the throws' cast flash.
            PlasmaHands(s,scene,a,v,time,q,low,me,aim);
            // ---- the casts ----
            for(int slot=0;slot<CyberRangedSpec.Slots;slot++)
            {
                var p=v.RPhase(slot,time);float rel=v.RRelease[slot],age=time-rel;long seed=a.Id*7919L+(long)Math.Round(v.RAt[slot]*240)+slot*101;
                switch(slot)
                {
                    case CyberRangedSpec.Bolt:break; // the throw's flash is PlasmaHands'
                    case CyberRangedSpec.Lance:
                        if(p.Live&&p.Part==0)
                        {
                            Charge(s,me,re,p.U,time,seed,q,low);
                            // The telegraph: a thin dashed line down the aim, marching out, brighter as the lance is about to fire.
                            if(!me)Dashed(s,re,aim,CyberRangedSpec.LanceRange,time,Lin(Cyan,.5f+1.2f*p.U),.016f,reduced,seed);
                        }
                        if(v.RReleased(slot)&&age>=0&&age<LanceLife)
                        {
                            float k=1-Smooth(age/LanceLife);var from=v.LanceFrom;var to=v.LanceTo;
                            if(me)
                            {
                                var dl=to-from;float l0=dl.Length();var dv=l0>1e-3f?dl/l0:aim;var tip=re+dv*NearBeam;
                                Beam(s,true,re,tip,.012f*(.4f+.6f*k),Lin(White,3.2f*k));Beam(s,true,re,tip,.032f*k,Lin(Cyan,1.6f*k));if(l0>FarFrom)from+=dv*FarFrom;
                            }
                            Beam(s,false,from,to,.022f*(.4f+.6f*k),Lin(White,3.2f*k));Beam(s,false,from,to,.07f*k,Lin(Cyan,1.6f*k));
                            if(!reduced)for(int i=0;i<3;i++){float t=Clamp01(age/.12f+i*.18f);if(t<1)s.Sprite(false,Vector3.Lerp(from,to,t),Lin(Cyan,2*(1-t)*k),.16f,RingSprite);}
                            int bits=reduced?4:low?6:12;float len=Vector3.Distance(from,to);var dir=len>1e-3f?(to-from)/len:aim;
                            for(int i=0;i<bits;i++){float t=H(seed+i,3);var at=from+dir*(len*t)+Up*((H(seed+i,5)-.5f)*.1f)+r*((H(seed+i,7)-.5f)*.1f);float z=.02f*k;s.Voxel(false,WeaponShape.Cube,at,Quaternion.Identity,new Vector3(z,z,z),Lin(i%3==0?Magenta:Cyan,2*k));}
                        }
                        break;
                    case CyberRangedSpec.Field:FieldFx(s,scene,a,v,p,time,le,seed,q,low,me);break;
                    case CyberRangedSpec.Swap:SwapFx(s,scene,a,v,p,time,seed,q,low,me);break;
                    case CyberRangedSpec.Root:RootFx(s,a,v,p,time,le,re,aim,seed,q,low,me);break;
                }
            }
        }
        // A dashed telegraph line from `from` along `dir` for `length` metres, its dashes marching outward.
        private void Dashed(ICyberFxSink s,Vector3 from,Vector3 dir,float length,float time,Vector3 color,float width,bool reduced,long seed)
        {
            const float Dash=.9f,Gap=.6f;float shift=reduced?0:(time*6)%(Dash+Gap);int n=(int)(length/(Dash+Gap))+1;
            for(int i=0;i<n;i++){float a=i*(Dash+Gap)+shift,b=Math.Min(length,a+Dash);if(a>=length)break;float fade=1-a/length*.6f;Beam(s,false,from+dir*a,from+dir*b,width,color*fade);}
        }
        // Lag Field: the cube's arc, the telegraph posts, the dome and its posts, the lagging packets, the ticks, the collapse.
        private void FieldFx(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,CyberPhase p,float time,Vector3 hand,long seed,float q,bool low,bool me)
        {
            bool reduced=q<.5f;float rel=v.RRelease[CyberRangedSpec.Field],land=v.FieldLand,until=v.FieldUntil;if(!(rel>-90)||!(land>rel))return;
            var c=v.FieldPoint;float floor=c.Y,R=CyberRangedSpec.FieldRadius;
            if(p.Live&&p.Part==0)Charge(s,me,hand,p.U,time,seed,q,low);
            // The arc (not in the thrower's own view: it leaves the hand at once).
            if(time>=rel&&time<land)
            {
                // (The thrower's own first-person view sees it from a metre out, not filling the camera off its hand.)
                float u=(time-rel)/(land-rel);var from=v.FieldFrom;var flat=new Vector3(c.X-from.X,0,c.Z-from.Z);float apex=.6f+.25f*flat.Length();
                if(me&&flat.LengthSquared()>1.5f){from+=Vector3.Normalize(flat)*1.1f+Up*.15f;flat=new Vector3(c.X-from.X,0,c.Z-from.Z);}
                var at=Vector3.Lerp(from,new Vector3(c.X,floor+.15f,c.Z),u)+Up*(4*apex*u*(1-u));var spin=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1,.6f,.3f)),time*9);
                s.Voxel(false,WeaponShape.Cube,at,spin,new Vector3(.12f,.12f,.12f),Lin(Cyan,2.2f));s.Voxel(false,WeaponShape.Cube,at,spin,new Vector3(.06f,.06f,.06f),Lin(White,3));
                if(!low)s.Glow(at,Lin(Cyan,.7f),0,.3f,Core,u,seed);
                int trail=reduced?2:5;for(int i=1;i<=trail;i++){float t=Math.Max(0,u-i*.05f);var tp=Vector3.Lerp(from,new Vector3(c.X,floor+.15f,c.Z),t)+Up*(4*apex*t*(1-t));float z=.03f*(1-i/(trail+1f));s.Voxel(false,WeaponShape.Cube,tp,Quaternion.Identity,new Vector3(z,z,z),Lin(i%2==0?Magenta:Cyan,1.6f));}
                // The field's edge shown before it lands: upright posts rising round the landing point.
                int posts=reduced?6:12;float grow=Smooth(u);
                for(int i=0;i<posts;i++){float ang=i*MeleeMath.TAU/posts;var b=c+new Vector3((float)Math.Sin(ang)*R,0,(float)Math.Cos(ang)*R);b.Y=floor+.15f;s.Line(false,b,b+Up*(.1f+.45f*grow),Lin(Magenta,.9f+.6f*grow),.018f,false);}
            }
            if(!(time>=land)||time>=until+FieldCollapse)return;
            float age=time-land,form=Smooth(age/CyberRangedSpec.FieldForm),collapse=Clamp01((time-until)/FieldCollapse),k=form*(1-collapse);if(k<=1e-3f)return;
            // A pulse climbs the dome on each tick.
            float tick=(age%CyberRangedSpec.FieldTick)/CyberRangedSpec.FieldTick,pulse=reduced?.4f:1-Smooth(tick/.35f);
            // The posts round the edge, stuttering on 6 Hz steps (the lag), falling to pixels as it collapses.
            int n=reduced?8:low?10:16;int step=(int)(time*6);
            for(int i=0;i<n;i++)
            {
                float ang=i*MeleeMath.TAU/n+.05f*(float)Math.Sin(i*1.7f),stut=reduced?0:(H(seed+i*7+step,3)-.5f)*.12f;var b=c+new Vector3((float)Math.Sin(ang)*R,0,(float)Math.Cos(ang)*R);b.Y=floor+.14f;
                float hgt=(1.2f+.6f*H(seed+i,5))*form*(1-collapse)+stut;s.Line(false,b,b+Up*Math.Max(.05f,hgt),Lin(i%4==0?Magenta:Cyan,(1.3f+.8f*pulse)*k),.028f,false);
                if(collapse>0&&!low){var fall=b+Up*(hgt*(1-collapse))+Up*(-1.2f*collapse*collapse);fall.Y=Math.Max(fall.Y,floor+.15f);float z=.04f*(1-collapse);s.Voxel(false,WeaponShape.Cube,fall,Quaternion.Identity,new Vector3(z,z,z),Lin(Cyan,2*(1-collapse)));}
            }
            // The dome: hexagonal rings of light shrinking toward the top (floating; the lowest .5 m up), a curtain between posts.
            int rings=reduced?2:4;
            for(int j=0;j<rings;j++)
            {
                float h=.5f+j*(1.5f/rings),rad=R*(float)Math.Sqrt(Math.Max(0,1-(h/2.1f)*(h/2.1f)));float ph=time*(reduced?.15f:.4f)*(j%2==0?1:-1);
                float bright=(1.6f+(j==(int)(tick*rings)?1.4f*pulse:0))*k;
                for(int e=0;e<6;e++){float a0=ph+e*MeleeMath.TAU/6,a1=ph+(e+1)*MeleeMath.TAU/6;var p0=new Vector3(c.X+(float)Math.Sin(a0)*rad,floor+h,c.Z+(float)Math.Cos(a0)*rad);var p1=new Vector3(c.X+(float)Math.Sin(a1)*rad,floor+h,c.Z+(float)Math.Cos(a1)*rad);s.Line(false,p0,p1,Lin(j%2==0?Cyan:White,bright),.03f,false);}
            }
            if(!low)for(int e=0;e<(reduced?3:6);e++){float a0=e*MeleeMath.TAU/6,a1=a0+MeleeMath.TAU/12;var p0=new Vector3(c.X+(float)Math.Sin(a0)*R,floor+.16f,c.Z+(float)Math.Cos(a0)*R);var p1=new Vector3(c.X+(float)Math.Sin(a1)*R,floor+.16f,c.Z+(float)Math.Cos(a1)*R);s.Band(false,p0,p1,p1+Up*1.1f,p0+Up*1.1f,.12f*k,.12f*k,Lin(Cyan,.9f));}
            // Lagging packets inside: drifting slowly, stepping on 6 Hz (frozen between).
            int packets=reduced?3:low?4:8;
            for(int i=0;i<packets;i++)
            {
                float st=reduced?age:(float)Math.Floor(age*6)/6;float ang=H(seed+i,11)*MeleeMath.TAU+st*.3f,rr=R*(.2f+.7f*H(seed+i,13));
                var at=new Vector3(c.X+(float)Math.Sin(ang)*rr,floor+.5f+1.1f*H(seed+i,17)+.15f*(float)Math.Sin(st+i),c.Z+(float)Math.Cos(ang)*rr);float z=.05f+.03f*H(seed+i,19);
                s.Voxel(false,WeaponShape.Cube,at,Quaternion.CreateFromAxisAngle(Up,st*2+i),new Vector3(z,z,z),Lin(i%3==0?Magenta:Cyan,1.7f*k));
            }
            // The pilots caught inside glitch (pixel blocks jittering at their chests on the lag steps).
            var actors=scene.Actors;
            for(int i=0;i<actors.Count;i++)
            {
                var o=actors[i];if(o==null||!o.Alive||o.Id==a.Id||o.Id==scene.Viewer)continue;var of=scene.Feet(o);var d=new Vector3(of.X-c.X,0,of.Z-c.Z);if(d.LengthSquared()>R*R||Math.Abs(of.Y-floor)>2)continue;
                for(int j=0;j<(reduced?2:4);j++){var at=of+Up*(1+.5f*H(o.Id*13+j+step,3))+new Vector3(H(o.Id*7+j+step,5)-.5f,0,H(o.Id*11+j+step,7)-.5f)*.6f;float z=.04f;s.Voxel(false,WeaponShape.Cube,at,Quaternion.Identity,new Vector3(z,z,z*.4f),Lin(j%2==0?Magenta:Cyan,1.8f*k));}
            }
        }
        // Proxy Swap: the blink's streak, the proxy afterimage collapsing into the mine, the fuse, the edge posts, the burst.
        private void SwapFx(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,CyberPhase p,float time,long seed,float q,bool low,bool me)
        {
            bool reduced=q<.5f;float at=v.RAt[CyberRangedSpec.Swap],rel=v.RRelease[CyberRangedSpec.Swap],mine=v.MineAt;if(!(at>-90))return;
            var from=v.SwapFrom;var to=v.SwapTo;float age=time-rel;
            // The blink: a streak of pixels along the path (the body is already there; the host moved it).
            if(v.RReleased(CyberRangedSpec.Swap)&&age>=-.05f&&age<.25f&&!me)
            {
                float k=1-Smooth(Math.Max(0,age)/.25f);int n=reduced?5:low?6:12;
                for(int i=0;i<n;i++){float t=(i+.5f)/n;var pp=Vector3.Lerp(from,to,t)+Up*(.4f+1.1f*H(seed+i,3));float z=.05f*k*(.5f+H(seed+i,5));s.Voxel(false,WeaponShape.Cube,pp,Quaternion.Identity,new Vector3(z*1.6f,z,z),Lin(i%2==0?Magenta:Cyan,2.2f));}
                var dir=to-from;if(dir.LengthSquared()>1e-4f)s.Streak(to+Up*1.1f,dir*8,Lin(Cyan,1.5f*k),.05f,.6f,.04f,.9f,Math.Max(0,age)/.25f,seed);
                s.Sprite(false,to+Up*1.1f,Lin(White,2.4f*k),.35f,Hot);
            }
            if(!(mine>-90))return;float fuse=time-rel,life=mine-rel;
            // The proxy: the caster's RGB-split afterimage left at the start, collapsing toward the mine through the fuse (not
            // for the first-person pilot itself: its own body, hidden from its camera, is what the ghost would draw).
            if(fuse>=0&&fuse<life&&!me&&ghostCount<MaxGhosts-2)
            {
                float u=fuse/life;int step=(int)(time*(reduced?6:24));bool show=reduced||H(seed+step,7)>.2f;
                if(show){var feet=scene.Feet(a);var off=new Vector3(from.X-feet.X,from.Y-feet.Y,from.Z-feet.Z);float op=.45f*(1-Smooth(u));var rr=Right(Forward(v.Yaw))*(reduced?.015f:.02f+.02f*H(seed+step,9));
                    ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=off+rr,Tint=Lin(Magenta,1.5f),Opacity=op};ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=off-rr,Tint=Lin(Cyan,1.5f),Opacity=op};}
            }
            var core=from+Up*.55f;
            // The mine: nested spinning hardlight frames, blinking faster toward the burst; the burst's edge shown by upright posts.
            if(fuse>=0&&fuse<life)
            {
                float u=fuse/life,rate=reduced?2:4+12*u;bool on=reduced||((int)(fuse*rate))%2==0;float g=on?1.6f+1.4f*u:.5f;
                for(int i=0;i<3;i++){float z=.09f-.025f*i;s.Voxel(false,WeaponShape.Cube,core,Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(i==0?1:0,1,i==2?1:0)),time*(3+i*2)),new Vector3(z,z,z),Lin(i==1?Magenta:i==2?White:Cyan,g));}
                if(!low)s.Glow(core,Lin(u>.6f?Magenta:Cyan,.5f+.6f*u),0,.4f,Core,u,seed);
                int posts=reduced?6:10;for(int i=0;i<posts;i++){float ang=i*MeleeMath.TAU/posts;var b=new Vector3(from.X+(float)Math.Sin(ang)*CyberRangedSpec.MineRadius,from.Y+.15f,from.Z+(float)Math.Cos(ang)*CyberRangedSpec.MineRadius);s.Line(false,b,b+Up*(.3f+.3f*u),Lin(Magenta,(.6f+.9f*u)*(on?1:.6f)),.016f,false);}
            }
            // The burst: a flash, a hexagon of light running out to the edge (floating), shards.
            float bu=time-mine;
            if(bu>=0&&bu<BurstLife)
            {
                float u=bu/BurstLife,flash=reduced?.7f*(float)Math.Sin(Math.PI*Math.Min(1,u*2)):1-Smooth(u/.3f);
                s.Glow(core,Lin(White,2.4f*flash),0,1.2f,Core,u,seed+3);if(!low)s.Glow(core,Lin(Magenta,1.2f*(1-u)),0,2.2f,Ring,u,seed+5);
                float rad=CyberRangedSpec.MineRadius*Smooth(u*1.4f);float y=from.Y+Math.Max(RingFloor,.5f-.2f*u);
                for(int e=0;e<6;e++){float a0=e*MeleeMath.TAU/6,a1=a0+MeleeMath.TAU/6;s.Line(false,new Vector3(from.X+(float)Math.Sin(a0)*rad,y,from.Z+(float)Math.Cos(a0)*rad),new Vector3(from.X+(float)Math.Sin(a1)*rad,y,from.Z+(float)Math.Cos(a1)*rad),Lin(Cyan,2.2f*(1-u)),.03f,false);}
                int n=reduced?4:low?6:12;for(int i=0;i<n;i++){float ang=i*2.39996f;var dir=new Vector3((float)Math.Sin(ang),.4f+.6f*H(seed+i,3),(float)Math.Cos(ang));var pp=core+dir*(.2f+3.2f*u)-Up*(2*u*u);pp.Y=Math.Max(pp.Y,from.Y+.15f);float z=.05f*(1-u);s.Voxel(false,WeaponShape.Cube,pp,Quaternion.CreateFromAxisAngle(Up,ang+u*8),new Vector3(z,z,z),Lin(i%2==0?Magenta:Cyan,2.2f*(1-u)));}
            }
        }
        // Root Access: the charge (core, converging streaks, spinning rings), the danger line, the railbeam, its impact.
        private void RootFx(ICyberFxSink s,Actor a,CyberLordView v,CyberPhase p,float time,Vector3 le,Vector3 re,Vector3 aim,long seed,float q,bool low,bool me)
        {
            bool reduced=q<.5f;float at=v.RAt[CyberRangedSpec.Root],rel=v.RRelease[CyberRangedSpec.Root];if(!(at>-90))return;
            var mid=(le+re)*.5f;var core=me?mid+Forward(v.Yaw)*.08f:mid;
            if(p.Live&&p.Part==0)
            {
                float u=p.U;Charge(s,me,core,u,time,seed,q,low);
                s.Sprite(me,core,Lin(White,1+3*u),(me?.04f:.12f)+(me?.05f:.18f)*u,Hot);
                if(!me)
                {
                    s.Glow(mid,Lin(Cyan,.6f+1.6f*u),0,.3f+.5f*u,Core,u,seed);
                    // Two hex rings round the charge, turning and closing in.
                    var f=Vector3.Normalize(new Vector3(aim.X,0,aim.Z));var r=Right(f);
                    for(int j=0;j<(reduced?1:2);j++)
                    {
                        float rad=(.35f-.2f*u)*(j==0?1:.7f),ph=time*(reduced?1:4)*(j==0?1:-1.4f);
                        for(int e=0;e<6;e++){float a0=ph+e*MeleeMath.TAU/6,a1=a0+MeleeMath.TAU/6;var p0=mid+r*((float)Math.Cos(a0)*rad)+Up*((float)Math.Sin(a0)*rad);var p1=mid+r*((float)Math.Cos(a1)*rad)+Up*((float)Math.Sin(a1)*rad);s.Line(false,p0,p1,Lin(j==0?Cyan:Magenta,1.6f+1.2f*u),.014f,false);}
                    }
                    // The danger line down the aim from TellFrom into the charge (everyone sees where it will fire).
                    float tell=at+CyberRangedSpec.TellFrom;if(time>=tell)Dashed(s,mid,aim,CyberRangedSpec.RootRange,time,Lin(Magenta,.6f+1.6f*u)*(reduced||H(seed+(int)(time*20),3)>.15f?1:.4f),.02f+.015f*u,reduced,seed);
                }
            }
            float age=time-rel;
            if(v.RReleased(CyberRangedSpec.Root)&&age>=0&&age<BeamLife)
            {
                // (Reduced effects: a softer flash.)
                float u=age/BeamLife,k=(1-Smooth(u))*(reduced?.65f:1),w=Smooth(Math.Min(1,age/.04f))*k;var from=v.RootFrom;var to=v.RootTo;var len=Vector3.Distance(from,to);var dir=len>1e-3f?(to-from)/len:aim;
                if(me){var tip=core+dir*NearBeam;Beam(s,true,core,tip,.03f*w,Lin(White,4*w));Beam(s,true,core,tip,.07f*w,Lin(Cyan,2*w));if(len>FarFrom){from+=dir*FarFrom;len-=FarFrom;}}
                var side=Vector3.Normalize(Vector3.Cross(dir,Math.Abs(dir.Y)>.9f?Vector3.UnitX:Up));var up2=Vector3.Cross(side,dir);
                Beam(s,false,from,to,.09f*w,Lin(White,4*w));Beam(s,false,from,to,.22f*w,Lin(Cyan,2*w));
                if(!low){s.Line(false,from+side*.07f,to+side*.07f,Lin(Magenta,1.8f*w),.03f*w,false);s.Line(false,from-side*.07f,to-side*.07f,Lin(Magenta,1.8f*w),.03f*w,false);}
                // Code blocks spiralling along the beam.
                int n=reduced?8:low?12:24;
                for(int i=0;i<n;i++){float t=(i+.5f)/n,ang=t*18+age*14;var pp=from+dir*(len*t)+(side*(float)Math.Cos(ang)+up2*(float)Math.Sin(ang))*(.16f*w);float z=.04f*w;s.Voxel(false,WeaponShape.Cube,pp,Quaternion.CreateFromAxisAngle(dir,ang),new Vector3(z,z,z*.4f),Lin(i%3==0?Magenta:i%3==1?White:Cyan,2.4f));}
                // The muzzle and the far end.
                s.Sprite(me,me?core:from,Lin(White,3.5f*k),me?.08f:.5f,Hot);s.Glow(to,Lin(White,2.2f*k),0,1f,Core,u,seed+7);if(!low)s.Glow(to,Lin(Magenta,1.2f*k),0,1.8f,Ring,u,seed+9);
            }
        }
        /// <summary>The hardlight dash sprint's streaks (Core/CyberLordRun.cs CyberLordRunRules.Swept*): from the swept-back fists a cyan
        /// streak and a magenta core trail behind the body, shedding pixels. They grow and fade on the carry's own eased ramp of the
        /// replicated sprint flag (NativeSprintRules, kept here per actor), so they never pop when the sprint toggles; a rewound
        /// clock (the killcam) takes the flag as it stands. Not on the first-person pilot's own view; Reduced: thinner, dimmer,
        /// one pixel; the low tier the streak alone.</summary>
        public const float StreakLength=1.1f;
        public const int MaxSprinters=16;
        private readonly int[] sprintId=new int[MaxSprinters];private readonly float[] sprintRamp=new float[MaxSprinters],sprintAt=new float[MaxSprinters];
        /// <summary>The actor's eased sprint ramp (0..1, shaped), stepped once per frame (no allocation).</summary>
        public float SprintRamp(Actor a,float time)
        {
            float vx=a.Velocity.x,vz=a.Velocity.z;bool on=a.Alive&&NativeSprintRules.Shows(a.Sprinting,a.Grounded,(float)Math.Sqrt(vx*vx+vz*vz));
            int slot=-1,stale=0;
            for(int i=0;i<MaxSprinters;i++){if(sprintId[i]==a.Id+1){slot=i;break;}if(sprintAt[i]<sprintAt[stale])stale=i;}
            if(slot<0){slot=stale;sprintId[slot]=a.Id+1;sprintRamp[slot]=on?1:0;sprintAt[slot]=time;}
            float dt=time-sprintAt[slot];sprintRamp[slot]=dt<0||dt>1?(on?1:0):NativeSprintRules.Step(sprintRamp[slot],on,dt);sprintAt[slot]=time;
            return NativeSprintRules.Shape(sprintRamp[slot]);
        }
        private void Sprint(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,float q,bool low,bool me)
        {
            float w=SprintRamp(a,time);if(me||!a.Alive||w<=1e-3f)return;float vx=a.Velocity.x,vz=a.Velocity.z,speed=(float)Math.Sqrt(vx*vx+vz*vz);if(speed<.5f)return;
            bool reduced=q<.5f;var dir=new Vector3(vx/speed,0,vz/speed);var vel=dir*speed;var r=Right(dir);
            // The ranged glide (Core/CyberLordRangedMotion.cs): energy streaming off the trailing palms instead of the fists' streaks.
            float mode=v.ModeWeight(time);if(mode>1e-3f)Glide(s,scene,a,dir,speed,time,w*mode,reduced,low);if(mode>=.999f)return;w*=1-mode;
            for(int h=0;h<2;h++)
            {
                if(!scene.Fist(a,h==0,out var fist))continue;long seed=a.Id*733L+h*17;float k=w*(reduced?.55f:1);
                s.Streak(fist,vel,Lin(Cyan,2.2f*k),reduced?.03f:.045f,StreakLength*w,.03f,.9f,(time*1.3f+h*.5f)%1f,seed);
                if(!low){s.Line(false,fist-dir*(StreakLength*.75f*w)+r*.012f,fist,Lin(Magenta,1.5f*k),.014f,false);s.Line(false,fist-dir*(StreakLength*.6f*w)-r*.012f,fist,Lin(White,1.6f*k),.008f,false);}
                int bits=reduced?1:low?1:3;int step=(int)(time*(reduced?6:24));
                for(int i=0;i<bits;i++){float t=H(seed+i*7+step,3);var at=fist-dir*(StreakLength*w*(.2f+.8f*t))+Up*((H(seed+i+step,5)-.5f)*.08f)+r*((H(seed+i+step,7)-.5f)*.08f);float z=.025f*(1-t*.6f);s.Voxel(false,WeaponShape.Cube,at,Quaternion.Identity,new Vector3(z,z,z),Lin(i%2==0?Cyan:Magenta,2*k*(1-t*.5f)));}
            }
        }
        /// <summary>The ranged glide's streams: from each trailing palm a magenta flame ribbon with a cyan heart streams back along
        /// the run, shedding pixel embers; a soft glow on the palm. Grows and fades on the sprint's eased ramp (`w`).</summary>
        public const float StreamLength=1.5f;
        private void Glide(ICyberFxSink s,ICyberFxScene scene,Actor a,Vector3 dir,float speed,float time,float w,bool reduced,bool low)
        {
            int step=(int)(time*24);var r=Right(dir);
            for(int h=0;h<2;h++)
            {
                if(!scene.Fist(a,h==0,out var fist))continue;long seed=a.Id*733L+h*17+5;float k=w*(reduced?.6f:1),fl=reduced?1:.85f+.3f*H(seed+step,3);
                var palm=fist-dir*.04f;var lift=Up*(.05f*(float)Math.Sin(time*7+h*3));
                Flame(s,false,palm,palm-dir*(StreamLength*w)+lift+Up*.06f,Lin(Magenta,1.7f*k*fl),.17f*w,reduced?.4f:H(seed+step,5));
                Flame(s,false,palm,palm-dir*(StreamLength*.7f*w)+lift*.5f,Lin(Cyan,2.4f*k),.12f*w,reduced?.6f:H(seed+step,7));Flame(s,false,palm,palm-dir*(StreamLength*.3f*w),Lin(HotWhite,2.2f*k),.05f*w,reduced?.2f:H(seed+step,8));
                s.Sprite(false,palm,Lin(HotWhite,2.4f*k),.08f,Hot);if(!low)s.Glow(palm,Lin(Plasma,.35f*k),0,.35f,Mote,MeleeMath.Frac(time*.9f+h*.5f),seed);
                int bits=reduced?1:low?2:5;
                for(int i=0;i<bits;i++){float u=MeleeMath.Frac(time*2.2f+i/(float)bits+H(seed,9));int gen=(int)Math.Floor(time*2.2f+i/(float)bits+H(seed,9));var at=palm-dir*(StreamLength*w*u)+Up*((H(seed+gen*7+i,11)-.5f)*.25f*u)+r*((H(seed+gen*5+i,13)-.5f)*.3f*u);float z=.035f*(1-u);s.Voxel(false,WeaponShape.Cube,at,Quaternion.CreateFromAxisAngle(Up,u*5+i),new Vector3(z,z,z),Lin(i%2==0?Magenta:Cyan,2.4f*k*(1-u*.5f)));}
            }
        }
        /// <summary>The scene's hardlight shots (the bolts) and the ranged impacts (the preview's, and the ranged hit / impact events).</summary>
        private void RangedShots(ICyberFxSink s,ICyberFxScene scene,float time,float q,bool low)
        {
            bool reduced=q<.5f;eye=scene.Eye;
            if(scene is ICyberShots shots)
            {
                // The fireballs (Bit Bolts today, Kit v2's Plasma Bytes): NativeCyberLordPlasmaFx.cs.
                PlasmaShots(s,scene,shots,time,q,low);
                for(int i=0;i<shots.CyberImpacts;i++)
                {
                    var im=shots.CyberImpactAt(i);long seed=im.Owner*977L+(long)Math.Round(im.At*240);
                    if(im.Slot==CyberRangedSpec.Bolt)PlasmaSplash(s,im.Position,im.Normal,time-im.At,im.Kind,im.Pilot,Splash(im.Kind),seed,reduced,low);
                    else Impact(s,im.Slot,im.Position,im.Normal,time-im.At,im.Pilot,seed,reduced,low);
                }
            }
            for(int i=0;i<records.Count;i++)
            {
                var rec=records[i];if(rec.Kind==EventKind.Heal)continue;
                // The world's normal when the event carries it, else back toward where it came from.
                var n=rec.Normal.LengthSquared()>1e-6f?Vector3.Normalize(rec.Normal):rec.From-rec.Position;n=n.LengthSquared()>1e-6f?Vector3.Normalize(n):Up;
                int kind=rec.Hit==CyberKit2RangedFeed.PlasmaHit?1:rec.Hit==CyberKit2RangedFeed.OverflowHit?2:rec.Hit==CyberHit.Bolt?0:-1;
                if(kind>=0){if(rec.Kind!=EventKind.Absorb)PlasmaSplash(s,rec.Position,n,time-rec.Time,kind,rec.Kind!=EventKind.Impact,Splash(kind),rec.Id*31L,reduced,low);continue;}
                int slot=rec.Hit>=CyberHit.Bolt&&rec.Hit<=CyberHit.Root?rec.Hit-CyberHit.Bolt:-1;if(slot<0)continue;
                Impact(s,slot,rec.Position,n,time-rec.Time,rec.Kind!=EventKind.Impact,rec.Id*31L,reduced,low);
            }
        }
        // A fireball kind's splash radius (Kit v2's; today's Bit Bolt has none).
        private static float Splash(int kind)=>kind==1?CyberKit2Spec.PlasmaSplashRadius:kind==2?CyberKit2Spec.OverflowSplashRadius:0;
        // One ranged impact: a hex ring off the surface, a flash and pixel shards; on a pilot, the ping reticle (the lance) or
        // a bigger hex shatter (the beam).
        private void Impact(ICyberFxSink s,int slot,Vector3 at,Vector3 normal,float age,bool pilot,long seed,bool reduced,bool low)
        {
            // Struck in its own first person (the impact on its own chest under the camera), the pilot gets the flash and the shards, no reticle.
            bool own=Vector3.DistanceSquared(at,eye)<.8f*.8f;
            float life=slot==CyberRangedSpec.Root?ImpactLife*1.6f:slot==CyberRangedSpec.Lance&&pilot?.6f:ImpactLife;if(age<0||age>=life)return;float u=age/life,k=1-Smooth(u);
            float size=slot==CyberRangedSpec.Root?1:slot==CyberRangedSpec.Bolt?.45f:.7f;
            s.Sprite(false,at,Lin(White,(reduced?1.6f:2.8f)*k),.25f*size,Hot);s.Sprite(false,at+normal*.02f,Lin(Cyan,1.8f*k),(.12f+.5f*Smooth(u))*size,RingSprite);
            int n=reduced?3:low?4:(slot==CyberRangedSpec.Root?12:6);
            for(int i=0;i<n;i++){float ang=i*2.39996f+H(seed+i,3);var dir=Vector3.Normalize(normal*.8f+new Vector3((float)Math.Cos(ang),.6f*H(seed+i,5),(float)Math.Sin(ang))*.9f);var pp=at+dir*(.05f+.6f*size*Smooth(u))-Up*(.5f*u*u);float z=.03f*size*k;s.Voxel(false,WeaponShape.Cube,pp,Quaternion.CreateFromAxisAngle(Up,ang+u*6),new Vector3(z,z,z),Lin(i%2==0?Cyan:Magenta,2.2f*k));}
            if(own)return;
            if(pilot&&slot==CyberRangedSpec.Lance)
            {
                // The ping: a hex reticle round the struck pilot, turning, held, fading.
                float rad=.42f,ph=age*2;for(int e=0;e<6;e++){if(e%2==1)continue;float a0=ph+e*MeleeMath.TAU/6,a1=a0+MeleeMath.TAU/6;var axisA=new Vector3((float)Math.Cos(a0),(float)Math.Sin(a0),0)*rad;var axisB=new Vector3((float)Math.Cos(a1),(float)Math.Sin(a1),0)*rad;s.Line(false,at+axisA,at+axisB,Lin(Magenta,2.2f*k),.018f,false);}
            }
        }
    }
}
