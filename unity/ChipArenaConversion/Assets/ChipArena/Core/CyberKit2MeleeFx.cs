using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>What a kit v2 melee or shared event is (CyberKit2MeleeFeed.Hit maps its Detail; the contract's Details).</summary>
    public enum Kit2Hit{None,Spin,Rise,UplinkZip,UplinkYank,Overclock,ForkStrike,ForkBurst,GlitchMark,GlitchDetonate,MeteorImpact}

    /// <summary>
    /// Kit v2's melee and shared effects (docs/CYBER-LORD.md "Kit v2"), each a pure function of the v2 view (CyberLordView.Kit2,
    /// the record through CyberLordFeed), the v2 events (their Detail) and the presentation clock, so host, guests, spectators
    /// and the killcam draw the same frame:
    /// - Q Uplink: the cast's flash, the hardlight cable (a taut white core in a cyan sheath, hex links, a glitch jitter) flying
    ///   out at CableSpeed and latching on a hex anchor; through the zip, speed lines and RGB-split afterimages streaming off the
    ///   body, the cable reeling short, and the arrival's burst; the yank's cable hauling the pilot in, and the stunned sparks.
    /// - E Overclock: the power-up's burst ring, then for the power state RGB-split afterimages trailing the pilot, crackling arcs
    ///   on the fists (the look's aura goes white-hot through FlareOf), and the melee hits' chained arcs (overclock events).
    /// - 1 Fork Bomb: three hardlight clones tearing in at their formation, glitch-shimmering, mirroring the caster's swings with
    ///   a short lag (CyberGhost.Fork, posed by their stand-ins), each dashing to its burst and blowing apart.
    /// - 2 Glitch Cut: the coiled hand's glow, the dash-slash's split streak and afterimages along the path, a hex mark flickering
    ///   on every marked pilot and quickening, then every mark detonating together: an X slash, a freeze-frame flash, shards.
    /// - 3 Meteor Byte: the crouch's gathering, the launch ring and the climbing trail, the giant hardlight fist building over the
    ///   raised hand, the reticle (light posts and a floating hex ring over the aimed floor, a beam down to it) following the aim
    ///   through the hover, the dive's streak, the crater's shockwave racing out and the ring of hardlight shards lingering
    ///   CraterSeconds.
    /// Nothing lies flat on the floor (rings float, posts stand). Reduced effects never strobe; the low tier thins. No allocation
    /// per frame once warmed.
    /// </summary>
    public sealed partial class NativeCyberLordFx
    {
        public struct Kit2Record{public int Id,Source,Target;public EventKind Kind;public Kit2Hit Hit;public float Time;public Vector3 Position,From;}
        private readonly List<Kit2Record> kit2Records=new List<Kit2Record>(MaxRecords);private int kit2Cursor;private float kit2Clock=float.NegativeInfinity;
        public IReadOnlyList<Kit2Record> Kit2Records=>kit2Records;
        /// <summary>The v2 events (called by Observe): idempotent per frame; a new arena or a rewound replica starts over.</summary>
        private void ObserveKit2(IReadOnlyList<ArenaEvent> events,float time)
        {
            if(events==null)return;int newest=0;for(int i=0;i<events.Count;i++)if(events[i].Id>newest)newest=events[i].Id;
            if(newest>0&&newest<kit2Cursor||time<kit2Clock-.25f){kit2Records.Clear();kit2Cursor=0;}
            kit2Clock=time;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=kit2Cursor||!CyberLordFeed.Kind(e.Kind))continue;var hit=CyberKit2MeleeFeed.Hit(e.Detail);if(hit==Kit2Hit.None||time-e.Time>RecordSeconds)continue;
                if(kit2Records.Count>=MaxRecords)kit2Records.RemoveAt(0);
                kit2Records.Add(new Kit2Record{Id=e.Id,Source=e.Source,Target=e.Target,Kind=e.Kind,Hit=hit,Time=e.Time,Position=N(e.Position),From=N(e.From)});
            }
            if(newest>kit2Cursor)kit2Cursor=newest;
            for(int i=kit2Records.Count-1;i>=0;i--)if(time-kit2Records[i].Time>RecordSeconds)kit2Records.RemoveAt(i);
        }
        private void ClearKit2(){kit2Records.Clear();kit2Cursor=0;kit2Clock=float.NegativeInfinity;}

        /// <summary>The fists' flare with the Overclock's white heat (the look's aura and gauntlets: Charge and both fists lit).</summary>
        public NativeCyberLordLook.Flare FlareOf(int id,float time,CyberLordView v)
        {
            var f=FlareOf(id,time);float oc=v!=null&&v.Kit2.Live?v.Kit2.Overclock(time):0;if(oc<=0)return f;
            float pulse=.75f+.25f*(float)Math.Sin(time*18+id);f.Charge=Math.Max(f.Charge,oc);f.Left=Math.Max(f.Left,.55f*oc*pulse);f.Right=Math.Max(f.Right,.55f*oc*pulse);return f;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // The per-pilot v2 effects
        // ---------------------------------------------------------------------------------------------------------------
        private void Kit2Melee(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,float q,bool low,bool me)
        {
            var k=v.Kit2;if(!k.Live)return;long seed=a.Id*7349L;
            Crater(s,k,time,seed+(long)Math.Round(k.LandAt*240),q,low);
            if(!a.Alive)return;
            Uplink(s,scene,a,k,time,seed+(long)Math.Round(k.At[CyberKit2Spec.Q]*240),q,low,me);
            Overclock(s,scene,a,v,time,seed+(long)Math.Round(k.OverclockAt*240),q,low,me);
            Fork(s,scene,a,v,time,seed+(long)Math.Round(k.ForkAt*240),q,low,me);
            GlitchCut(s,scene,a,v,time,seed+(long)Math.Round(k.At[CyberKit2Spec.Cut]*240),q,low,me);
            Meteor(s,scene,a,v,time,seed+(long)Math.Round(k.At[CyberKit2Spec.Meteor]*240),q,low,me);
        }
        private static Vector3 Fist(ICyberFxScene scene,Actor a,bool left,bool me,Vector3 fallback){Vector3 f;return (me?scene.ViewFist(left,out f):scene.Fist(a,left,out f))?f:fallback;}
        private static Vector3 Chest(ICyberFxScene scene,Actor a){return scene.Chest(a,out var c)?c:scene.Feet(a)+Up*1.15f;}
        private static Actor ActorOf(ICyberFxScene scene,int id){var l=scene.Actors;for(int i=0;i<l.Count;i++)if(l[i]!=null&&l[i].Id==id)return l[i];return null;}
        // A hardlight cable from `a` to `b`: a white core line, a cyan sheath ribbon, hex links along it, jittered on glitch steps.
        private static void Cable(ICyberFxSink s,Vector3 a,Vector3 b,Vector3 eye,float g,float time,long seed,float q,bool low)
        {
            var d=b-a;float len=d.Length();if(len<.05f||g<=.01f)return;var dir=d/len;bool reduced=q<.5f;int segs=low?4:Math.Min(14,4+(int)(len*.8f));int step=(int)(time*(reduced?6:24));
            var side=Vector3.Cross(dir,eye-(a+b)*.5f);side=side.LengthSquared()>1e-8f?Vector3.Normalize(side):Vector3.UnitX;var prev=a;
            for(int i=1;i<=segs;i++)
            {
                float u=(float)i/segs;var p=a+d*u;if(i<segs&&!reduced)p+=side*((H(seed+i*7+step,3)-.5f)*.035f)+Up*((H(seed+i*7+step,5)-.5f)*.03f);
                s.Line(false,prev,p,Lin(White,2.8f*g),.024f,false);
                s.Band(false,prev-side*.065f,p-side*.065f,p+side*.065f,prev+side*.065f,.8f*g,.8f*g,Lin(Cyan,1.5f));
                prev=p;
            }
            int links=low?3:Math.Min(16,(int)(len*1.6f));
            for(int i=0;i<links;i++){float u=(i+.5f)/links;var p=a+d*u;float z=.035f*g;s.Voxel(false,WeaponShape.Ring,p,Quaternion.CreateFromAxisAngle(dir,time*6+i)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,MeleeMath.PI/2),new Vector3(z,z,z*.4f),Lin(i%3==0?Magenta:Cyan,2.2f));}
        }
        private static void HexAnchor(ICyberFxSink s,Vector3 at,Vector3 normal,float age,float g,long seed,float q,bool low)
        {
            if(g<=.01f)return;float u=Clamp01(age/.25f);var x=Vector3.Normalize(Vector3.Cross(normal,Up)+new Vector3(1e-4f,0,1e-4f));var y=Vector3.Cross(x,normal);float rad=.12f+.12f*Smooth(u);
            for(int i=0;i<6;i++){float a0=i*MeleeMath.TAU/6+age*3,a1=a0+MeleeMath.TAU/6;s.Line(false,at+(x*(float)Math.Cos(a0)+y*(float)Math.Sin(a0))*rad,at+(x*(float)Math.Cos(a1)+y*(float)Math.Sin(a1))*rad,Lin(i%2==0?White:Cyan,2.6f*g),.022f,false);}
            s.Sprite(false,at,Lin(Cyan,2.2f*g*(1.2f-u*.4f)),.25f+.15f*(1-u),Hot);if(age<.2f)s.Sprite(false,at,Lin(White,3*(1-age/.2f)),.45f,RingSprite);
        }

        // ---- Q Uplink ----
        private void Uplink(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberKit2MeleeView k,float time,long seed,float q,bool low,bool me)
        {
            int Q=CyberKit2Spec.Q;float at=k.At[Q],rel=k.Release[Q];if(!(at>-90)||time<at)return;
            float end=k.UplinkOutcome==CyberKit2Melee.Miss?rel+CyberKit2Melee.MissSeconds:k.UplinkOutcome==CyberKit2Melee.Flying?k.Until[Q]+.2f:k.UplinkArriveAt+.12f;if(k.Cut[Q])end=Math.Min(end,k.Until[Q]+.08f);if(time>end+.5f)return;
            var feet=scene.Feet(a);var hand=Fist(scene,a,false,me,feet+Up*1.3f);var eye=scene.Eye;
            // the cast's flash at the fist
            float fa=time-rel;if(fa>=0&&fa<.18f){float u=fa/.18f;s.Sprite(me,hand,Lin(White,3.2f*(1-u)*(1-u)),.18f+.2f*u,Hot);s.Sprite(me,hand,Lin(Cyan,1.8f*(1-u)),.15f+.4f*u,RingSprite);}
            if(time<rel||time>end)return;var emit=hand;var anchor=k.UplinkAnchor;
            // the cable's head: flying out at CableSpeed to the latch (or to full range and back on a miss), then on the anchor
            Vector3 head;
            if(k.UplinkOutcome==CyberKit2Melee.Miss){var dir=Vector3.Normalize(anchor-emit+new Vector3(0,0,1e-5f));float t=time-rel,out1=CyberKit2Spec.UplinkRange/CyberKit2Spec.CableSpeed,dist=t<out1?t*CyberKit2Spec.CableSpeed:Math.Max(0,CyberKit2Spec.UplinkRange-(t-out1)*CyberKit2Spec.CableSpeed);head=emit+dir*dist;}
            else if(time<k.UplinkLatchAt||k.UplinkLatchAt<-90){float total=Math.Max(1e-3f,k.UplinkLatchAt-rel),u=k.UplinkLatchAt>-90?Clamp01((time-rel)/total):Clamp01((time-rel)*CyberKit2Spec.CableSpeed/CyberKit2Spec.UplinkRange);head=Vector3.Lerp(emit,anchor,u);}
            else if(k.UplinkOutcome==CyberKit2Melee.Yank){var victim=ActorOf(scene,k.UplinkTarget);head=victim!=null?Chest(scene,victim):anchor;}
            else head=anchor;
            float g=time>k.UplinkArriveAt&&k.UplinkArriveAt>-90?1-Smooth((time-k.UplinkArriveAt)/.12f):1;if(k.Cut[Q]&&time>k.Until[Q])g=Math.Min(g,1-Smooth((time-k.Until[Q])/.08f));
            Cable(s,emit,head,eye,g,time,seed,q,low);
            s.Sprite(false,head,Lin(White,2.4f*g),.12f,Hot);
            bool latched=k.UplinkLatchAt>-90&&time>=k.UplinkLatchAt;
            if(latched&&k.UplinkOutcome==CyberKit2Melee.Zip)
            {
                HexAnchor(s,anchor,Vector3.Normalize(k.UplinkFrom+Up*CyberLordFeed.BodyCentre-anchor+new Vector3(0,1e-4f,0)),time-k.UplinkLatchAt,g,seed+5,q,low);
                // the zip: speed lines streaming past the body and RGB-split afterimages left behind
                if(time<k.UplinkArriveAt)
                {
                    var dir=Vector3.Normalize(anchor-feet-Up*CyberLordFeed.BodyCentre+new Vector3(0,1e-5f,0));var c=feet+Up*1.0f;int n=Count(10,q,low);float u=Clamp01((time-k.UplinkLatchAt)/Math.Max(.05f,k.UplinkArriveAt-k.UplinkLatchAt));
                    if(!me)for(int i=0;i<n;i++){float h=H(seed+i,7);var o=Vector3.Normalize(Vector3.Cross(dir,new Vector3(H(seed+i,9)-.5f,H(seed+i,11)-.5f,H(seed+i,13)-.5f)+new Vector3(0,1e-4f,0)))*(.35f+.5f*h);
                        float ph=MeleeMath.Frac(time*3+h);s.Streak(c+o-dir*(ph*1.2f),dir*30,Lin(i%3==0?Magenta:Cyan,2f*(1-ph)),.018f,.5f,.04f,.9f,ph,seed+i);}
                    if(!me&&ghostCount<MaxGhosts-3)for(int i=1;i<=3;i++){float back=.6f*i*(.6f+.4f*u);var off=-dir*back;off.Y*=.5f;ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=off,Tint=Lin(i%2==0?Magenta:Cyan,1.4f),Opacity=.2f/i};}
                }
                float aa=time-k.UplinkArriveAt;if(aa>=0&&aa<.35f)ImpactBurst(s,feet+Up*.9f,aa,.8f,seed+17,q,low);
            }
            if(latched&&k.UplinkOutcome==CyberKit2Melee.Yank)
            {
                var victim=ActorOf(scene,k.UplinkTarget);if(victim==null)return;var vc=Chest(scene,victim);float ya=time-k.UplinkLatchAt;
                if(ya<.2f)s.Sprite(false,vc,Lin(White,3*(1-ya/.2f)),.5f,Hot);
                // the stunned sparks: crackling arcs and a hex collar on the pilot through the stun
                float stun=k.UplinkArriveAt+CyberKit2Spec.YankStun;if(time<stun)StunSparks(s,vc,time,stun-time,seed+23,q,low);
            }
        }
        /// <summary>A stunned pilot's crackle (Uplink's yank, a meteor launch): jagged arcs round the head and a spinning hex collar.</summary>
        private static void StunSparks(ICyberFxSink s,Vector3 chest,float time,float left,long seed,float q,bool low)
        {
            bool reduced=q<.5f;float g=Clamp01(left/.15f);int step=(int)(time*(reduced?5:20));var head=chest+Up*.42f;float rad=.28f;
            for(int i=0;i<6;i++){float a0=i*MeleeMath.TAU/6+time*4,a1=a0+MeleeMath.TAU/6;s.Line(false,head+new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*rad,head+new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1))*rad,Lin(i%2==0?Cyan:Magenta,2.2f*g),.016f,false);}
            int arcs=low?2:Count(4,q,low);
            for(int i=0;i<arcs;i++)
            {
                long kk=seed+step*13+i;var p0=chest+new Vector3(H(kk,3)-.5f,H(kk,5)*.9f-.2f,H(kk,7)-.5f)*.6f;var p1=p0;
                for(int j=0;j<3;j++){var p2=p1+new Vector3(H(kk+j,9)-.5f,H(kk+j,11)-.5f,H(kk+j,13)-.5f)*.22f;s.Line(false,p1,p2,Lin(j%2==0?White:Cyan,2.4f*g),.012f,false);p1=p2;}
            }
        }

        // ---- E Overclock ----
        private void Overclock(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,long seed,float q,bool low,bool me)
        {
            var k=v.Kit2;if(!(k.OverclockAt>-90))return;var feet=scene.Feet(a);bool reduced=q<.5f;
            // the power-up's burst: a white flash, an upright ring racing out and sparks flung off the body
            float ba=time-k.OverclockAt;if(ba>=0&&ba<.45f&&!me)
            {
                float u=ba/.45f,fade=1-Smooth(u);var c=feet+Up*1.0f;s.Sprite(false,c,Lin(White,(reduced?2:4)*fade*fade),.9f,Hot);int segs=low?10:20;float rad=.4f+2.2f*Smooth(u);
                for(int i=0;i<segs;i++){float a0=i*MeleeMath.TAU/segs,a1=a0+MeleeMath.TAU/segs;var p0=feet+new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*rad;var p1=feet+new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1))*rad;
                    s.Band(false,p0+Up*.15f,p1+Up*.15f,p1+Up*(.15f+.8f*fade),p0+Up*(.15f+.8f*fade),fade,fade,Lin(i%3==0?Magenta:Cyan,1.8f));}
                int n=Count(12,q,low);for(int i=0;i<n;i++){var d=Vector3.Normalize(new Vector3(H(seed+i,3)-.5f,H(seed+i,5)*.8f,H(seed+i,7)-.5f));float sp=5+5*H(seed+i,9);s.Streak(c+d*(sp*ba),d*sp,Lin(i%3==0?White:Cyan,2.4f*fade),.02f,.15f,.03f,.9f,u,seed+i);}
            }
            float oc=k.Overclock(time);if(oc<=0)return;
            // RGB-split afterimages trailing the pilot (behind its travel; a jitter when still), stepping on 24 Hz (steady reduced)
            if(!me&&ghostCount<MaxGhosts-2)
            {
                int step=(int)(time*(reduced?4:24));var vel=new Vector3(a.Velocity.x,0,a.Velocity.z);float sp=vel.Length();var back=sp>.5f?-vel/sp:-Forward(v.Yaw);float trail=(.08f+.035f*Math.Min(sp,9))*oc;
                var right=Right(Forward(v.Yaw));float j=reduced?0:(H(a.Id*41+step,3)-.5f)*.05f;
                ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=back*trail+right*(.035f+j),Tint=Lin(Magenta,1.5f),Opacity=.34f*oc};
                ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=back*trail*2-right*(.035f-j),Tint=Lin(Cyan,1.5f),Opacity=.26f*oc};
            }
            // crackling arcs on the fists
            int arcs=low?1:Count(3,q,low);int s2=(int)(time*(reduced?5:16));
            for(int h=0;h<2;h++){var fist=Fist(scene,a,h==0,me,feet+Up*1.2f);for(int i=0;i<arcs;i++){long kk=seed+s2*11+i*5+h*97;if(H(kk,3)<.35f)continue;var p1=fist;for(int j=0;j<3;j++){var p2=p1+new Vector3(H(kk+j,5)-.5f,H(kk+j,7)-.3f,H(kk+j,9)-.5f)*.14f;s.Line(me,p1,p2,Lin(j%2==0?White:Cyan,2.6f*oc),.01f,false);p1=p2;}}}
        }
        /// <summary>The Overclock's state for the sibling ranged effects (Plasma Byte's split: two side fireballs at ±SplitAngle
        /// degrees from the shot while this is above 0).</summary>
        public static float OverclockSplit(CyberLordView v,float time)=>v!=null&&v.Kit2.Live?v.Kit2.Overclock(time):0;

        // ---- 1 Fork Bomb ----
        /// <summary>A fork clone's lag behind the caster's swing (s) and its glitch shimmer.</summary>
        public const float ForkLag=.06f,ForkLagStep=.035f;
        private void Fork(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,long seed,float q,bool low,bool me)
        {
            var k=v.Kit2;if(!(k.ForkAt>-90)||time<k.ForkAt-.05f)return;bool reduced=q<.5f;
            for(int i=0;i<CyberKit2Spec.Clones;i++)
            {
                bool bursting=k.BurstAt[i]>-90;float dashAt=bursting?k.BurstAt[i]-CyberKit2Spec.DetonateDash:-100,gone=bursting?k.BurstAt[i]:k.ForkUntil;
                if(time>gone+.6f)continue;var at=k.Clone[i];float yaw=k.CloneYaw[i];
                if(bursting&&time>=dashAt)at=Vector3.Lerp(at,k.Burst[i],Smooth(Clamp01((time-dashAt)/CyberKit2Spec.DetonateDash)));
                Tear(s,at,time-(k.ForkAt-.04f),true,seed+11+i,q,low);
                if(time<gone&&time>=k.ForkAt-.02f)
                {
                    // the clone itself (posed by its stand-in: the caster's swing with a lag), shimmering on glitch steps
                    int step=(int)(time*(reduced?5:24));float shimmer=reduced?.85f:.7f+.3f*H(a.Id*19+i*7+step,3);float op=Smooth((time-k.ForkAt+.02f)/.08f)*shimmer;
                    WriteClone(a.Id,i,k,at,yaw);
                    if(ghostCount<MaxGhosts)ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Fork,Actor=a.Id,Index=i,Position=at,Yaw=yaw,PoseAge=time,Tint=Lin(i==1?Magenta:Cyan,1.8f),Opacity=.55f*op};
                    if(!reduced&&H(a.Id*23+i*11+step,5)<.18f&&ghostCount<MaxGhosts)ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Fork,Actor=a.Id,Index=i,Position=at+Right(Forward(yaw))*(.06f*(H(step,7)-.5f)),Yaw=yaw,PoseAge=time,Tint=Lin(i==1?Cyan:Magenta,1.6f),Opacity=.22f*op};
                    // the dash to the burst
                    if(bursting&&time>=dashAt){var d=k.Burst[i]-k.Clone[i];if(d.LengthSquared()>.01f)s.Streak(at+Up*.9f,Vector3.Normalize(d)*30,Lin(i==1?Magenta:Cyan,2.4f),.05f,.6f,.05f,.9f,Clamp01((time-dashAt)/CyberKit2Spec.DetonateDash),seed+i);}
                }
                if(bursting){float ba=time-k.BurstAt[i];if(ba>=0&&ba<.5f){var c=k.Burst[i]+Up*.9f;ImpactBurst(s,c,ba,CyberKit2Spec.DetonateRadius*.5f,seed+31+i,q,low);Shatter(s,k.Burst[i],yaw,ba,i==1?Magenta:Cyan,seed+41+i,q,low);GlitchRing(s,k.Burst[i],ba,CyberKit2Spec.DetonateRadius,i==1?Magenta:Cyan,seed+51+i,q,low);}}
                else if(time>=k.ForkUntil&&time<k.ForkUntil+.45f)Shatter(s,at,yaw,time-k.ForkUntil,i==1?Magenta:Cyan,seed+61+i,q,low);
            }
        }
        // A clone's stand-in record: the caster's live swing shifted by the clone's lag (and, after its own strike, faced at its target).
        private static void WriteClone(int caster,int i,CyberKit2MeleeView k,Vector3 at,float yaw)
        {
            var r=CyberKit2Melee.Clones.For(CyberKit2Melee.CloneId(caster,i));float lag=ForkLag+ForkLagStep*i;int L=CyberKit2Spec.MeleeLmb;
            r.Version=CyberKit2Spec.Version;r.Combo=k.ComboAt>-90?k.ComboStep:-1;
            if(k.ComboAt>-90){r.At[L]=k.ComboAt+lag;r.Release[L]=k.ComboHit+lag;r.Until[L]=k.ComboUntil+lag;}else r.At[L]=r.Release[L]=r.Until[L]=-100;
            r.CutAt[L]=-100;
        }
        /// <summary>A fork clone's stand-in actor (posed by the runtime on a rig of its own at the ghost's position and clock).</summary>
        public static void ForkActor(Actor clone,Actor caster,in CyberGhost g)
        {
            clone.Id=CyberKit2Melee.CloneId(g.Actor,g.Index);clone.SourceSpec=CyberLordSpec.Id;clone.Weapon=WeaponId.Sword;clone.Grounded=true;clone.Hp=100;clone.CastRate=1;clone.Yaw=g.Yaw;
            clone.Position=new V3(g.Position.X,g.Position.Y,g.Position.Z);clone.Velocity=default;clone.BlockUntil=clone.StunUntil=-100;
            clone.BodyColor=caster?.BodyColor??clone.BodyColor;clone.AccentColor=caster?.AccentColor??clone.AccentColor;clone.Hat=caster?.Hat??"none";
        }
        private static void GlitchRing(ICyberFxSink s,Vector3 feet,float age,float radius,int color,long seed,float q,bool low)
        {
            if(age<0||age>.4f)return;float u=age/.4f,fade=1-Smooth(u),rad=radius*Smooth(u*1.3f);int segs=low?10:18;
            for(int i=0;i<segs;i++){float a0=i*MeleeMath.TAU/segs,a1=a0+MeleeMath.TAU/segs;var p0=feet+new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*rad;var p1=feet+new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1))*rad;
                s.Band(false,p0+Up*.14f,p1+Up*.14f,p1+Up*(.14f+.6f*fade),p0+Up*(.14f+.6f*fade),fade,fade,Lin(i%2==0?color:White,1.7f));}
        }

        // ---- 2 Glitch Cut ----
        private void GlitchCut(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,long seed,float q,bool low,bool me)
        {
            var k=v.Kit2;int C=CyberKit2Spec.Cut;float at=k.At[C];if(!(at>-90)||time<at)return;float rel=k.Release[C],until=k.Until[C],det=k.DetonateAt;
            float end=k.Cut[C]?until+.2f:det+.6f;if(time>end)return;bool reduced=q<.5f;var feet=scene.Feet(a);
            // the coiled hand's glow over the windup
            if(time<rel){float u=Clamp01((time-at)/Math.Max(1e-3f,rel-at));var hand=Fist(scene,a,false,me,feet+Up*.8f);s.Sprite(me,hand,Lin(White,2.4f*u),.12f+.08f*u,Hot);s.Sprite(me,hand,Lin(Magenta,1.4f*u),.25f,RingSprite);return;}
            // the slash's streak along the path: lit as the body passes, held, then relit white as the marks detonate
            var from=k.CutFrom+Up*1.0f;var to=k.CutTo+Up*1.0f;float pass=Clamp01((time-rel)/Math.Max(1e-3f,until-rel));var head=Vector3.Lerp(from,to,1-(1-pass)*(1-pass));
            float hold=time<until?1:1-.6f*Smooth((time-until)/.3f),relit=det>-90&&time>=det?1-Smooth((time-det)/.35f):0,g=Math.Max(hold*(k.Cut[C]?1-Smooth((time-until)/.15f):1),relit);
            if(g>.01f&&!me)
            {
                var d=head-from;if(d.LengthSquared()>1e-4f)
                {
                    var side=Vector3.Normalize(Vector3.Cross(d,scene.Eye-(from+head)*.5f)+new Vector3(0,1e-5f,0));int step=(int)(time*(reduced?5:24));
                    s.Band(false,from-side*.02f,head-side*.02f,head+side*.02f,from+side*.02f,.4f*g,1.2f*g,Lin(White,relit>0?4f:2.6f));
                    s.Band(false,from-side*.09f,head-side*.09f,head+side*.09f,from+side*.09f,.25f*g,.8f*g,Lin(Cyan,1.5f));
                    if(!reduced){var o=Up*(.03f+.02f*H(step,3));s.Line(false,from+o,head+o,Lin(Magenta,1.8f*g),.012f,false);s.Line(false,from-o,head-o,Lin(Cyan,1.8f*g),.012f,false);}
                    int bits=low?3:Count(10,q,low);for(int i=0;i<bits;i++){long kk=seed+i*7+step;float u=H(kk,5);var p=Vector3.Lerp(from,head,u)+new Vector3(H(kk,7)-.5f,H(kk,9)-.5f,H(kk,11)-.5f)*.3f;float z=.03f*g;s.Voxel(false,WeaponShape.Cube,p,Quaternion.Identity,new Vector3(z,z,z*.5f),Lin(i%2==0?Magenta:Cyan,2.2f));}
                }
            }
            // afterimages along the dash
            if(!me&&time<until+.2f&&ghostCount<MaxGhosts-3)for(int i=1;i<=3;i++){float u=Math.Max(0,pass-.25f*i);var p=Vector3.Lerp(k.CutFrom,k.CutTo,1-(1-u)*(1-u));var off=p-feet;off.Y=0;float op=.36f/i*(time<until?1:1-Smooth((time-until)/.2f));if(op>.02f)ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=off,Tint=Lin(i%2==0?Cyan:Magenta,1.5f),Opacity=op};}
            // the marks: a flickering hex and slash on every marked pilot from the moment the dash passed it, quickening to the detonation
            for(int m=0;m<k.MarkedCount;m++)
            {
                var victim=ActorOf(scene,k.Marked[m]);if(victim==null||!victim.Alive&&!(det>-90&&time<det+.4f))continue;var c=Chest(scene,victim);float markAt=MarkAt(k,c);if(time<markAt)continue;
                if(det>-90&&time>=det){DetonateMark(s,c,time-det,seed+101+m,q,low);continue;}if(k.Cut[C]&&det<-90)continue;
                float left=det-time,rate=reduced?2:6+18*Clamp01(1-left/CyberKit2Spec.MarkDelay),blink=reduced?.8f:.55f+.45f*(float)Math.Abs(Math.Sin(time*rate*MeleeMath.PI));float ma=time-markAt;
                if(ma<.12f)s.Sprite(false,c,Lin(White,3*(1-ma/.12f)),.35f,Hot);
                var f=Vector3.Normalize(scene.Eye-c);var x=Vector3.Normalize(Vector3.Cross(Up,f)+new Vector3(1e-4f,0,0));float rad=.22f;
                for(int i=0;i<6;i++){float a0=i*MeleeMath.TAU/6+time*2.5f,a1=a0+MeleeMath.TAU/6;s.Line(false,c+(x*(float)Math.Cos(a0)+Up*(float)Math.Sin(a0))*rad,c+(x*(float)Math.Cos(a1)+Up*(float)Math.Sin(a1))*rad,Lin(i%2==0?Magenta:White,2.4f*blink),.02f,false);}
                s.Line(false,c-x*.16f-Up*.16f,c+x*.16f+Up*.16f,Lin(Magenta,2.6f*blink),.025f,false);
            }
        }
        // When the dash passed a pilot's chest: its projection on the path, timed along the dash's ease.
        private static float MarkAt(CyberKit2MeleeView k,Vector3 chest)
        {
            int C=CyberKit2Spec.Cut;var a=k.CutFrom;var d=k.CutTo-a;float l2=d.LengthSquared();float u=l2<1e-6f?1:Clamp01(Vector3.Dot(chest-Up*1.0f-a,d)/l2);
            float t=1-(float)Math.Sqrt(Math.Max(0,1-u));return k.Release[C]+t*(k.Until[C]-k.Release[C]);
        }
        private static void DetonateMark(ICyberFxSink s,Vector3 c,float age,long seed,float q,bool low)
        {
            if(age<0||age>.5f)return;float u=age/.5f,fade=1-Smooth(u),open=Smooth(age/.06f);
            s.Sprite(false,c,Lin(White,(q<.5f?2.5f:5f)*fade*fade),1.0f,Hot);s.Sprite(false,c,Lin(Magenta,2.2f*fade),.5f+1.1f*u,RingSprite);
            for(int i=0;i<2;i++){var d=Vector3.Normalize(new Vector3(i==0?1:-1,1,.2f));float len=(.45f+.55f*open)*(1-.3f*u);s.Line(false,c-d*len,c+d*len,Lin(i==0?White:Magenta,3.4f*fade),.055f,false);}
            ImpactBurst(s,c,age,.6f,seed,q,low);int n=Count(10,q,low);
            for(int i=0;i<n;i++){var d=Vector3.Normalize(new Vector3(H(seed+i,3)-.5f,H(seed+i,5)-.3f,H(seed+i,7)-.5f));float z=.04f*fade;s.Voxel(false,WeaponShape.Cube,c+d*(2.4f*age),Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1,H(seed+i,9),.3f)),age*12),new Vector3(z,z,z*.4f),Lin(i%2==0?Magenta:White,2.4f));}
        }
        /// <summary>The Glitch Cut's freeze-frame flash for the local pilot's screen (the caster or a marked pilot): 0..1, a hard
        /// white frame at the detonation easing out over .25 s (Reduced: a soft swell at .4).</summary>
        public static float FreezeFlash(CyberLordView v,int local,float time,bool reduced)
        {
            var k=v.Kit2;if(!k.Live||!(k.DetonateAt>-90)||time<k.DetonateAt||time>k.DetonateAt+.3f)return 0;if(v.Id!=local&&!k.MarkedPilot(local))return 0;
            float age=time-k.DetonateAt;return reduced?.4f*(1-Smooth(age/.3f)):age<.05f?1:1-Smooth((age-.05f)/.25f);
        }

        // ---- 3 Meteor Byte ----
        private void Meteor(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,long seed,float q,bool low,bool me)
        {
            var k=v.Kit2;int M=CyberKit2Spec.Meteor;float at=k.At[M];if(!(at>-90)||time<at)return;float leap=CyberKit2Melee.LeapAt(at),hover=CyberKit2Melee.HoverAt(at),dive=CyberKit2Melee.DiveAt(at),land=k.LandAt;
            if(k.Cut[M]&&time>k.Until[M]+.2f)return;if(time>land+.5f)return;var feet=scene.Feet(a);bool reduced=q<.5f;
            // the crouch: energy gathering into both fists
            if(time<leap){float u=Clamp01((time-at)/Math.Max(1e-3f,leap-at));for(int h=0;h<2;h++){var f=Fist(scene,a,h==0,me,feet+Up*.6f);s.Sprite(me,f,Lin(Cyan,2.2f*u),.1f+.12f*u,Hot);}}
            // the launch: a ring at the feet and a climbing trail of pixels
            float la=time-leap;if(la>=0&&la<.4f&&!me){GlitchRing(s,k.MeteorFrom,la,1.6f,Cyan,seed+3,q,low);if(la<.25f)s.Sprite(false,k.MeteorFrom+Up*.3f,Lin(White,3*(1-la/.25f)),.8f,Hot);}
            if(time>=leap&&time<hover&&!me){int n=Count(8,q,low);for(int i=0;i<n;i++){float h=H(seed+i,5),y=CyberKit2Spec.LeapArc(Clamp01((time-leap)/CyberKit2Spec.LeapSeconds))*h;var p=k.MeteorFrom+Up*(.5f+y)+new Vector3(H(seed+i,7)-.5f,0,H(seed+i,9)-.5f)*.4f;float z=.035f*(1-h*.5f);s.Voxel(false,WeaponShape.Cube,p,Quaternion.Identity,new Vector3(z,z,z),Lin(i%2==0?Cyan:Magenta,2));}}
            // the giant hardlight fist building over the raised hand through the hover, leading the dive
            if(time>=hover-.1f&&time<land+.08f)
            {
                var hand=Fist(scene,a,false,me,feet+Up*2f);float build=Smooth(Clamp01((time-(hover-.1f))/.22f)),fade=time>land?1-Smooth((time-land)/.08f):1;var dirDown=time>=dive?Vector3.Normalize(Target(k,a,time)+Up*.3f-hand+new Vector3(0,-1e-4f,0)):Up;
                GiantFist(s,hand+dirDown*(me?.2f:.45f)*build,dirDown,Forward(v.Yaw),build*fade,time,seed+13,q,low,me);
                if(time>=dive&&!me){var tgt=Target(k,a,time);s.Streak(hand,Vector3.Normalize(tgt-hand+new Vector3(0,-1e-4f,0))*28,Lin(White,2.6f*fade),.12f,1.2f,.06f,.9f,Clamp01((time-dive)/CyberKit2Spec.DiveSeconds),seed+17);}
            }
            // the reticle on the aimed floor: following the live aim through the hover, locked from the dive
            if(time>=leap&&time<land&&!(me&&time<hover-.2f))Reticle(s,Target(k,a,time),time,time>=dive?1:Smooth((time-leap)/.2f),time>=dive,seed+19,q,low);
            // the impact: the shockwave and the flash (the crater lingers: Crater)
            float ia=time-land;if(ia>=0&&ia<.5f){var c=k.MeteorTarget;ImpactBurst(s,c+Up*.4f,ia,1.6f,seed+23,q,low);GlitchRing(s,c,ia*.8f,CyberKit2Spec.MeteorRadius,Cyan,seed+29,q,low);if(ia<.08f)s.Sprite(false,c+Up*.8f,Lin(White,(reduced?2.5f:5)*(1-ia/.08f)),2.2f,Hot);}
        }
        // The reticle's floor point at `time`: the record's target from the dive, else the caster's live aim on the start's floor.
        private static Vector3 Target(CyberKit2MeleeView k,Actor a,float time)
        {
            if(time>=CyberKit2Melee.DiveAt(k.At[CyberKit2Spec.Meteor]))return k.MeteorTarget;
            float yaw=a.Yaw*(float)Math.PI/180,pitch=Math.Max(15,Math.Min(85,-a.Pitch))*(float)Math.PI/180;var eye=new Vector3(a.Position.x,a.Position.y+1.5f,a.Position.z);float floor=k.MeteorFrom.Y,drop=Math.Max(.5f,eye.Y-floor);
            float dist=Math.Min(CyberKit2Spec.MeteorRange,drop/(float)Math.Tan(pitch));return new Vector3(eye.X+(float)Math.Sin(yaw)*dist,floor,eye.Z+(float)Math.Cos(yaw)*dist);
        }
        private static void Reticle(ICyberFxSink s,Vector3 c,float time,float g,bool locked,long seed,float q,bool low)
        {
            if(g<=.01f)return;float rad=CyberKit2Spec.MeteorRadius;int posts=low?6:10;float pulse=.75f+.25f*(float)Math.Sin(time*(locked?30:10));
            for(int i=0;i<posts;i++){float a0=i*MeleeMath.TAU/posts+time*.6f;var p=c+new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*rad;s.Line(false,p+Up*.02f,p+Up*(.35f+.25f*pulse),Lin(i%2==0?Cyan:Magenta,2.2f*g),.028f,false);}
            // a floating hex ring over the point (well clear of the floor) and the inner target hex, shrinking as it locks
            float r2=locked?.7f:1.1f;for(int i=0;i<6;i++){float a0=i*MeleeMath.TAU/6-time,a1=a0+MeleeMath.TAU/6;s.Line(false,c+Up*.6f+new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*r2,c+Up*.6f+new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1))*r2,Lin(locked?White:Cyan,2.4f*g*pulse),.03f,false);}
            s.Line(false,c+Up*.15f,c+Up*3.2f,Lin(locked?White:Cyan,1.6f*g),.02f,false);s.Sprite(false,c+Up*.6f,Lin(Cyan,1.4f*g*pulse),.5f,RingSprite);
        }
        // The giant hardlight fist: a knuckle block with four finger bars and a thumb, white-hot core, pixel shed.
        private static void GiantFist(ICyberFxSink s,Vector3 at,Vector3 dir,Vector3 fwd,float g,float time,long seed,float q,bool low,bool view)
        {
            if(g<=.01f)return;var rot=LookRotation(dir,fwd);float k=(view?.7f:1.8f)*g;var col=Lin(Cyan,2.1f);var hot=Lin(White,2.8f);
            s.Voxel(view,WeaponShape.Cube,at,rot,new Vector3(.42f,.3f,.36f)*k,col);s.Voxel(view,WeaponShape.Cube,at,rot,new Vector3(.3f,.2f,.26f)*k,hot);
            for(int i=0;i<4;i++){var o=Vector3.Transform(new Vector3((i-1.5f)*.105f,.02f,.24f)*k,rot);s.Voxel(view,WeaponShape.Cube,at+o,rot,new Vector3(.09f,.16f,.14f)*k,i%2==0?col:Lin(Cyan,2.5f));}
            s.Voxel(view,WeaponShape.Cube,at+Vector3.Transform(new Vector3(.25f,-.06f,.1f)*k,rot),rot,new Vector3(.1f,.12f,.2f)*k,col);
            if(!view){s.Glow(at,Lin(Cyan,1.4f*g),0,.9f*k+.2f,Core,.5f,seed);int bits=low?2:Count(6,q,low);int step=(int)(time*24);for(int i=0;i<bits;i++){long kk=seed+step*5+i;var p=at+new Vector3(H(kk,3)-.5f,H(kk,5)-.5f,H(kk,7)-.5f)*.6f*k;float z=.03f*g;s.Voxel(false,WeaponShape.Cube,p,Quaternion.Identity,new Vector3(z,z,z*.5f),Lin(i%2==0?Magenta:White,2.2f));}}
        }
        private static Quaternion LookRotation(Vector3 z,Vector3 upHint)
        {
            z=Vector3.Normalize(z);var x=Vector3.Cross(Vector3.Normalize(upHint+new Vector3(1e-4f,0,0)),z);if(x.LengthSquared()<1e-6f)x=Vector3.Cross(Up,z);if(x.LengthSquared()<1e-6f)x=Vector3.UnitX;x=Vector3.Normalize(x);var y=Vector3.Cross(z,x);
            var m=new Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1);return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
        }
        /// <summary>The crater: a ring of upright hardlight shards at the impact, lingering CraterSeconds and dissolving into pixels.</summary>
        private static void Crater(ICyberFxSink s,CyberKit2MeleeView k,float time,long seed,float q,bool low)
        {
            if(!(k.LandAt>-90)||k.Cut[CyberKit2Spec.Meteor])return;float age=time-k.LandAt;if(age<0||age>CyberKit2Spec.CraterSeconds)return;
            float rise=Smooth(age/.12f),dissolve=Smooth((age-(CyberKit2Spec.CraterSeconds-.6f))/.6f),g=rise*(1-dissolve);if(g<=.01f)return;int n=low?7:Count(14,q,low);var c=k.MeteorTarget;
            for(int i=0;i<n;i++)
            {
                float a0=i*MeleeMath.TAU/n+H(seed+i,3)*.3f,rad=1.3f+.5f*H(seed+i,5),hgt=(.25f+.45f*H(seed+i,7))*g;var p=c+new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*rad;
                var tilt=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(-(float)Math.Sin(a0),0,(float)Math.Cos(a0))),.45f+.2f*H(seed+i,9));
                s.Voxel(false,WeaponShape.Cube,p+Up*(hgt*.5f),tilt,new Vector3(.09f,hgt,.06f),Lin(i%3==0?Magenta:Cyan,1.5f+.8f*(1-Smooth(age/.5f))));
            }
            s.Glow(c+Up*.3f,Lin(Cyan,.6f*g),0,1.4f,Core,Clamp01(age/CyberKit2Spec.CraterSeconds),seed);
            if(dissolve>0){int bits=Count(8,q,low);for(int i=0;i<bits;i++){var p=c+new Vector3(H(seed+i,11)-.5f,0,H(seed+i,13)-.5f)*3+Up*(.2f+1.2f*dissolve*H(seed+i,15));float z=.03f*(1-dissolve);s.Voxel(false,WeaponShape.Cube,p,Quaternion.Identity,new Vector3(z,z,z),Lin(Cyan,1.8f));}}
        }

        // ---------------------------------------------------------------------------------------------------------------
        // The v2 events: the spin's and rise's hit sparks, the chained arc, the clones' strikes and bursts, the marks, the meteor
        // ---------------------------------------------------------------------------------------------------------------
        private void Kit2Events(ICyberFxSink s,ICyberFxScene scene,float time,float q,bool low)
        {
            for(int i=0;i<kit2Records.Count;i++)
            {
                var r=kit2Records[i];float age=time-r.Time;if(age<0)continue;long seed=r.Id*2654435761L;bool strike=CyberLordFeed.Strike(r.Kind);if(!strike)continue;
                switch(r.Hit)
                {
                    case Kit2Hit.Spin:if(age<SparkSeconds)HitSpark(s,r.Position,age,1.2f,seed,q,low);if(age<ManaFlowSeconds+.05f)ManaFlow(s,scene,new Record{Id=r.Id,Source=r.Source,Target=r.Target,Kind=r.Kind,Time=r.Time,Position=r.Position},age,seed,q,low);break;
                    case Kit2Hit.Rise:if(age<SparkSeconds)HitSpark(s,r.Position,age,1.25f,seed,q,low);if(age<ManaFlowSeconds+.05f)ManaFlow(s,scene,new Record{Id=r.Id,Source=r.Source,Target=r.Target,Kind=r.Kind,Time=r.Time,Position=r.Position},age,seed,q,low);break;
                    case Kit2Hit.Overclock:if(age<.22f)ChainArc(s,r.From,r.Position,age,seed,q,low);if(age<SparkSeconds)GlitchSpark(s,r.Position,age,.8f,seed,q,low);break;
                    case Kit2Hit.ForkStrike:if(age<SparkSeconds)HitSpark(s,r.Position,age,.8f,seed,q,low);break;
                    case Kit2Hit.UplinkZip:case Kit2Hit.UplinkYank:case Kit2Hit.GlitchMark:case Kit2Hit.ForkBurst:case Kit2Hit.GlitchDetonate:if(age<SparkSeconds)GlitchSpark(s,r.Position,age,.9f,seed,q,low);break;
                    case Kit2Hit.MeteorImpact:if(age<SparkSeconds)GlitchSpark(s,r.Position,age,1.2f,seed,q,low);break;
                }
            }
        }
        /// <summary>The Overclock's chained arc: a jagged hardlight bolt from the struck pilot to the next, re-forking on glitch steps.</summary>
        private static void ChainArc(ICyberFxSink s,Vector3 from,Vector3 to,float age,long seed,float q,bool low)
        {
            float u=age/.22f,g=1-Smooth(u);bool reduced=q<.5f;int segs=low?5:8;int step=(int)(age*(reduced?8:40));var d=to-from;var side=Vector3.Normalize(Vector3.Cross(d,Up)+new Vector3(0,0,1e-4f));var prev=from;
            for(int i=1;i<=segs;i++){float k=(float)i/segs;var p=from+d*k;if(i<segs)p+=(side*(H(seed+i*3+step,5)-.5f)+Up*(H(seed+i*3+step,7)-.5f))*.35f*(float)Math.Sin(k*Math.PI);s.Line(false,prev,p,Lin(White,3f*g),.025f,false);s.Line(false,prev,p,Lin(Cyan,1.6f*g),.07f,false);prev=p;}
            s.Sprite(false,to,Lin(White,2.6f*g),.3f,Hot);
        }
    }
}
