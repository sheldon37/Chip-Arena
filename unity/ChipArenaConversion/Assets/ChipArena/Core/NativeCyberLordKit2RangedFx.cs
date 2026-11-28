using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Kit v2's ranged abilities' effects (Core/CyberKit2Ranged.cs; docs/CYBER-LORD.md "Kit v2"), part of the Cyber Lord's world
    /// effects and keyed the same way: every Cyber Lord's v2 view (CyberLordView.Kit2R, live only on a Version 2 record) and the
    /// pure helpers the host resolves with (CyberKit2Spec: the chain's hop times and falloff, the storm's drops, the orbital
    /// chase), so host, guests, spectators and the killcam draw the same frame from the replicated starts.
    /// - 1 Daisy Chain: the charge crackling round the casting hand, then a jagged lightning bolt from the hand to the first
    ///   pilot and hop after hop to the next ones on the hop delay (each a little dimmer, as the damage falls off), a flash on
    ///   each and the slow's static crawling over the struck for its length.
    /// - 2 Singularity: the dark orb gathered between the hands and thrown; the black hole (an event horizon, a photon ring, light
    ///   bent in arcs round it, a tilted accretion disk of streaks spiralling in, debris pulled in from the pull's reach, an
    ///   inward pulse each tick), the collapse and the outward shock of the implosion.
    /// - 3 Packet Storm: the call beam from the raised hand, a floating hex ring with upright posts at the aimed point, a glitch
    ///   portal overhead, and ten cyber meteors (glowing cubes trailing flame) falling on their replicated drops, each telegraphed
    ///   by brackets and a light column, landing in a burst, a floating ring to its radius, shards and a data pillar.
    /// - 4 Orbital Uplink: the uplink from the raised hand to the sky and the targeting ring closing in through the call-down;
    ///   then the orbital column (a white core in a cyan sheath with magenta edges, code blocks pouring down it) chasing the aim's
    ///   floor point, the ground burning round its foot (upright flame licks, embers, smoke) and along its recent path, a pulse on
    ///   each tick, and its narrowing end.
    /// Nothing lies flat within 12 cm of the floor (the owner's rule): rings float, telegraphs stand up. Reduced effects: no
    /// strobe or flicker, fewer pieces; the low tier halves the counts and drops smoke. Presentation only.
    /// </summary>
    public sealed partial class NativeCyberLordFx
    {
        /// <summary>The v2 effects' timing (s): a chain bolt's linger after the cast, the struck flash; the hole's growth and
        /// the implosion's collapse and shock; the storm's telegraph lead and a drop's burst; the column's fade.</summary>
        public const float ChainLinger=.2f,ChainFlash=.14f,HoleGrow=.25f,Collapse=.12f,ShockLife=.6f,DropLead=.55f,DropBurst=.65f,ColumnFade=.25f;
        public const float RingLift=.22f;

        /// <summary>One pilot's v2 ranged effects.</summary>
        private void Kit2Ranged(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,float q,bool low,bool me)
        {
            var k=v.Kit2R;if(!k.Live)return;eye=scene.Eye;
            Vector3 lf,rf;bool hasL=me?scene.ViewFist(true,out lf):scene.Fist(a,true,out lf),hasR=me?scene.ViewFist(false,out rf):scene.Fist(a,false,out rf);
            var feet=scene.Feet(a);var f=Forward(v.Yaw);var r=Right(f);if(!hasL)lf=feet+Up*1.25f+f*.4f-r*.15f;if(!hasR)rf=feet+Up*1.25f+f*.4f+r*.15f;var aim=Aim(a);
            DaisyChain(s,scene,a,k,time,rf,aim,q,low,me);
            SingularityFx(s,scene,a,k,time,lf,rf,aim,q,low,me);
            PacketStorm(s,a,k,time,rf,q,low,me);
            OrbitalFx(s,a,k,time,lf,rf,q,low,me);
        }

        // ---- 1 Daisy Chain ----
        // A jagged bolt from `a` to `b`: the path displaced across the eye's view (re-seeded on `flick`), three layers (a magenta
        // halo, a cyan body, a white core), and a fork off its middle.
        private void Lightning(ICyberFxSink s,Vector3 a,Vector3 b,long seed,int flick,float gain,bool reduced,bool low)
        {
            var d=b-a;float len=d.Length();if(len<.05f||gain<=1e-3f)return;d/=len;var side=Vector3.Cross(d,eye-(a+b)*.5f);if(side.LengthSquared()<1e-6f)side=Vector3.Cross(d,Up);side=Vector3.Normalize(side);var up=Vector3.Cross(side,d);
            int n=Math.Max(5,Math.Min(low?10:20,(int)(len/.3f)));float amp=Math.Min(.55f,.07f+.045f*len)*(reduced?.6f:1);
            for(int strand=0;strand<(low||reduced?1:2);strand++)
            {
                long ss=seed+strand*7919;float sa=strand==0?1:.55f,g=strand==0?gain:gain*.55f;var prev=a;
                for(int j=1;j<=n;j++)
                {
                    // Two octaves: a coarse kink every third point (eased between), a fine crackle on every one.
                    float t=j/(float)n,env=(float)Math.Sin(Math.PI*t),cu=j/3f;int c0=(int)cu;float cf=cu-c0;cf=cf*cf*(3-2*cf);
                    float hx=MeleeMath.Lerp(H(ss+c0*7+flick*131,3),H(ss+(c0+1)*7+flick*131,3),cf)-.5f+(H(ss+j*13+flick*37,5)-.5f)*.35f,hy=(MeleeMath.Lerp(H(ss+c0*11+flick*131,7),H(ss+(c0+1)*11+flick*131,7),cf)-.5f)*.6f;
                    var p=j==n?b:a+d*(len*t)+(side*hx+up*hy)*(2*amp*sa*env);
                    if(strand==0&&!low)s.Line(false,prev,p,Lin(Cyan,.9f*g),.13f,false);
                    s.Line(false,prev,p,Lin(Cyan,2.6f*g),strand==0?.045f:.025f,false);s.Line(false,prev,p,Lin(HotWhite,3.4f*g),strand==0?.016f:.01f,false);
                    // A fork off the middle, and glitch sparks at a couple of kinks.
                    if(strand==0&&!low&&j==n/2){var fd=Vector3.Normalize(d+side*(H(ss+flick,9)-.5f)*2.4f+up*(H(ss+flick,11)-.5f));var q0=p;for(int m=1;m<=3;m++){var q1=p+fd*(len*.07f*m)+side*((H(ss+m+flick*17,13)-.5f)*.12f);s.Line(false,q0,q1,Lin(Cyan,1.8f*g*(1-m/4f)),.022f,false);q0=q1;}}
                    if(strand==0&&!reduced&&(j==n/3||j==2*n/3))s.Sprite(false,p,Lin(Magenta,1.6f*g),.14f,Hot);
                    prev=p;
                }
            }
        }
        // The pilot struck by hop `i`: its live chest when it is in the scene, else the replicated point.
        private static Vector3 Struck(ICyberFxScene scene,CyberKit2RangedView k,int i)
        {
            int id=k.ChainIds[i];if(id!=0){var list=scene.Actors;for(int j=0;j<list.Count;j++){var o=list[j];if(o!=null&&o.Id==id&&o.Alive&&scene.Chest(o,out var c))return c;}}
            return k.ChainPoints[i];
        }
        private void DaisyChain(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberKit2RangedView k,float time,Vector3 hand,Vector3 aim,float q,bool low,bool me)
        {
            const int C=CyberKit2Ranged.Chain;float at=k.At[C];if(!(at>-90)||time<at)return;bool reduced=q<.5f;long seed=a.Id*6151L+(long)Math.Round(at*240);
            int flick=(int)(time*(reduced?6:30));var tip=hand+aim*(me?.04f:.12f);float rel=k.Release[C],end=k.Until[C]+ChainLinger;
            // The charge: arcs crackling off the casting hand, a glow growing.
            if(time<rel)
            {
                float u=(time-at)/Math.Max(.01f,rel-at);s.Sprite(me,tip,Lin(HotWhite,1.6f+2*u),(me?.05f:.14f)+(me?.04f:.12f)*u,Hot);if(!me)s.Glow(tip,Lin(Cyan,.6f+.8f*u),0,.4f+.3f*u,Core,u,seed);
                if(!me)for(int i=0;i<(low?2:4);i++){var dir=Vector3.Normalize(new Vector3(H(seed+i+flick*5,3)-.5f,H(seed+i+flick*5,5)-.3f,H(seed+i+flick*5,7)-.5f));Lightning(s,tip,tip+dir*(.18f+.2f*u),seed+i*97,flick,.7f*u,reduced,true);}
            }
            if(!k.Released(C)||k.ChainCount<=0)return;
            var from=me?tip:tip;
            for(int i=0;i<k.ChainCount;i++)
            {
                float hop=k.HopAt(i),age=time-hop;if(age<0)break;var to=Struck(scene,k,i);
                float fall=CyberKit2Spec.ChainHopDamage(i)/CyberKit2Spec.ChainDamage,fade=time<end?1:Clamp01(1-(time-end)/.12f),gain=(.55f+.45f*fall)*fade*(age<.05f?1.6f:1);
                if(time<end+.12f)Lightning(s,from,to,seed+i*1013,flick,gain,reduced,low);
                // The strike's flash, then the slow's static crawling over the struck pilot.
                if(age<ChainFlash){float u=age/ChainFlash;s.Sprite(false,to,Lin(HotWhite,(reduced?2:3.6f)*(1-u)),.55f,Hot);s.Sprite(false,to,Lin(Cyan,2*(1-u)),.25f+.7f*u,RingSprite);s.Glow(to,Lin(Cyan,1.1f*(1-u)),0,1.1f,Core,u,seed+i);}
                if(age<CyberKit2Spec.ChainSlowSeconds&&k.ChainIds[i]!=scene.Viewer)
                {
                    float u=age/CyberKit2Spec.ChainSlowSeconds;int crawl=(int)(time*(reduced?5:20));
                    for(int j=0;j<(low?1:3);j++){float ang=H(seed+i*31+j+crawl*7,13)*MeleeMath.TAU;var o1=to+new Vector3((float)Math.Cos(ang)*.28f,(H(seed+j+crawl,17)-.5f)*.7f,(float)Math.Sin(ang)*.28f);var o2=o1+new Vector3(H(seed+j+crawl,19)-.5f,H(seed+j+crawl,23)-.5f,H(seed+j+crawl,29)-.5f)*.35f;Lightning(s,o1,o2,seed+i*7+j,crawl,.8f*(1-u),reduced,true);}
                }
                from=to;
            }
        }

        // ---- 2 Singularity ----
        private static readonly Vector3 Void=new Vector3(0,0,0);
        private void SingularityFx(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberKit2RangedView k,float time,Vector3 lf,Vector3 rf,Vector3 aim,float q,bool low,bool me)
        {
            const int G=CyberKit2Ranged.Singularity;float at=k.At[G];if(!(at>-90)||time<at)return;bool reduced=q<.5f;long seed=a.Id*4241L+(long)Math.Round(at*240);float rel=k.Release[G];int step=(int)(time*24);
            // The orb gathered between the hands.
            if(time<rel)
            {
                float u=(time-at)/Math.Max(.01f,rel-at);var c=(lf+rf)*.5f+aim*(me?.04f:.12f);float rr=(me?.02f:.04f)+(me?.03f:.07f)*u;
                s.Voxel(me,WeaponShape.Sphere,c,Quaternion.Identity,new Vector3(rr,rr,rr),Void);s.Sprite(me,c,Lin(Magenta,1.6f+u),rr*4.5f,RingSprite);
                if(!me){s.Glow(c,Lin(Violet,.7f*u),0,.6f,Core,u,seed);for(int i=0;i<(low?3:6);i++){float ang=i*MeleeMath.TAU/6+time*5;var o=new Vector3((float)Math.Cos(ang),(float)Math.Sin(ang*1.3f)*.6f,(float)Math.Sin(ang))*(.35f*(1-u)+.08f);s.Streak(c+o,-o*5,Lin(i%2==0?Violet:Cyan,1.8f),.02f,.08f,.02f,.9f,u,seed+i);}}
                return;
            }
            if(!(k.OrbLand>-90))return;float land=k.OrbLand,implode=k.Implode;var centre=k.OrbPoint;
            // The orb in flight.
            if(time<land)
            {
                float u=(time-rel)/Math.Max(.01f,land-rel);var p=Vector3.Lerp(k.OrbFrom,centre,u);var dir=centre-k.OrbFrom;dir=dir.LengthSquared()>1e-6f?Vector3.Normalize(dir):aim;
                s.Voxel(false,WeaponShape.Sphere,p,Quaternion.Identity,new Vector3(.13f,.13f,.13f),Void);s.Sprite(false,p,Lin(Magenta,2.2f),.46f,RingSprite);s.Sprite(false,p,Lin(HotWhite,1.6f),.34f,RingSprite);
                s.Glow(p,Lin(Violet,.9f),0,.9f,Core,u,seed);s.Streak(p,dir*CyberKit2Spec.OrbSpeed,Lin(Violet,1.4f),.1f,.4f,.02f,.9f,u,seed);
                for(int i=0;i<(low?1:2);i++){float ang=time*18+i*MeleeMath.PI;var side=Across(dir);var up=Vector3.Cross(dir,side);s.Sprite(false,p+(side*(float)Math.Cos(ang)+up*(float)Math.Sin(ang))*.22f,Lin(Cyan,2.4f),.08f,Hot);}
                return;
            }
            float hole=time-land,life=implode-land,floorY=centre.Y-CyberKit2Ranged.HoleLift;if(time>=implode+ShockLife)return;
            var toEye=eye-centre;float de=toEye.Length();toEye=de>1e-4f?toEye/de:Vector3.UnitZ;var ex=Vector3.Cross(Up,toEye);if(ex.LengthSquared()<1e-6f)ex=Vector3.UnitX;ex=Vector3.Normalize(ex);var ey=Vector3.Cross(toEye,ex);
            if(time<implode)
            {
                float g=Smooth(hole/HoleGrow),c=1-Smooth((time-(implode-Collapse))/Collapse),sz=g*(time>implode-Collapse?c:1),pulse=reduced?1:1+.05f*(float)Math.Sin(time*9);
                // The event horizon, its photon ring and the bent light round it.
                float rh=.38f*sz*pulse;s.Voxel(false,WeaponShape.Sphere,centre,Quaternion.Identity,new Vector3(rh,rh,rh),Void);
                s.Sprite(false,centre,Lin(HotWhite,2.6f),rh*2/.62f*1.12f,RingSprite);s.Sprite(false,centre,Lin(Magenta,2),rh*2/.62f*1.45f,RingSprite);s.Glow(centre,Lin(Violet,.8f*sz),0,3.2f*sz,Ring,MeleeMath.Frac(hole*.4f),seed);
                int arcs=reduced?8:low?8:18;
                for(int i=0;i<arcs;i++)
                {
                    float rr=(.55f+.65f*H(seed+i,3))*sz,a0=H(seed+i,5)*MeleeMath.TAU+time*(reduced?.3f:.9f)*(i%2==0?1:-1),span=.5f+.9f*H(seed+i,7);var col=i%3==0?Lin(Cyan,1.7f):i%3==1?Lin(Magenta,1.6f):Lin(HotWhite,1.5f);
                    var p0=centre+(ex*(float)Math.Cos(a0)+ey*(float)Math.Sin(a0))*rr;for(int m=1;m<=3;m++){float am=a0+span*m/3;var p1=centre+(ex*(float)Math.Cos(am)+ey*(float)Math.Sin(am))*rr;if(Math.Min(p0.Y,p1.Y)>floorY+.15f)s.Line(false,p0,p1,col*(1-rr/(1.4f*Math.Max(.01f,sz)))*sz,.025f+.02f*(1-rr/1.3f),false);p0=p1;}
                }
                // The accretion disk: streaks spiralling in on a tilted plane, hotter toward the horizon.
                var tilt=Vector3.Normalize(Up+ex*.35f+toEye*.15f);var e1=Vector3.Normalize(Vector3.Cross(tilt,ey));var e2=Vector3.Cross(tilt,e1);int disk=reduced?10:low?12:26;
                for(int i=0;i<disk;i++)
                {
                    float ph=MeleeMath.Frac(time*(.55f+.25f*H(seed+i,9))+H(seed+i,11)),rr=MeleeMath.Lerp(1.7f,.42f,ph*ph)*sz,ang=H(seed+i,13)*MeleeMath.TAU+time*(2.2f/Math.Max(.3f,rr));
                    var p=centre+(e1*(float)Math.Cos(ang)+e2*(float)Math.Sin(ang))*rr;var tan=(-e1*(float)Math.Sin(ang)+e2*(float)Math.Cos(ang))*(2.2f/Math.Max(.3f,rr)*rr);
                    var col=ph<.4f?Lin(Violet,1.8f):ph<.75f?Lin(Magenta,2.2f):Lin(HotWhite,2.8f);s.Streak(p,tan,col*sz,.06f,.22f,.03f,.9f,ph,seed+i);
                }
                // Two rings of the disk turning on its plane (dashed), magenta outside, white within.
                for(int ring=0;ring<(low?1:2);ring++)
                {
                    float rr=(ring==0?1.35f:.92f)*sz,spin=time*(reduced?.6f:(ring==0?1.6f:-2.4f));int segs=24;
                    for(int e=0;e<segs;e+=2){float a0=spin+e*MeleeMath.TAU/segs,a1=a0+MeleeMath.TAU/segs;var p0=centre+(e1*(float)Math.Cos(a0)+e2*(float)Math.Sin(a0))*rr;var p1=centre+(e1*(float)Math.Cos(a1)+e2*(float)Math.Sin(a1))*rr;if(Math.Min(p0.Y,p1.Y)>floorY+.15f)s.Line(false,p0,p1,ring==0?Lin(Magenta,1.8f*sz):Lin(HotWhite,2.2f*sz),ring==0?.05f:.035f,false);}
                }
                // Debris pulled in from the pull's reach, spiralling.
                int bits=reduced?5:low?6:14;
                for(int i=0;i<bits;i++)
                {
                    float ph=MeleeMath.Frac(hole*(.45f+.3f*H(seed+i,15))+H(seed+i,17)),ang=H(seed+i,19)*MeleeMath.TAU+ph*2.4f,r0=CyberKit2Spec.PullRadius*(.35f+.65f*H(seed+i,21));
                    var start=new Vector3(centre.X+(float)Math.Cos(ang)*r0,centre.Y-CyberKit2Ranged.HoleLift+.15f+1.6f*H(seed+i,23),centre.Z+(float)Math.Sin(ang)*r0);
                    var p=Vector3.Lerp(start,centre,ph*ph);float z=(.05f+.07f*H(seed+i,25))*(1-ph*.7f)*g;if(z<=.004f)continue;
                    s.Voxel(false,WeaponShape.Cube,p,Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(H(seed+i,27),1,H(seed+i,29))),hole*6+i),new Vector3(z,z,z),i%3==0?Lin(Cyan,2.2f):i%3==1?Lin(0x3a3150):Lin(Violet,1.4f));
                }
                // The pull's reach: faint streaks drawn in at its edge.
                if(!low){int edge=reduced?6:16;for(int i=0;i<edge;i++){float ph=MeleeMath.Frac(hole*.8f+i/(float)edge),ang=i*MeleeMath.TAU/edge+H(seed,31);var dir=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));var p=centre+dir*(CyberKit2Spec.PullRadius*(1-.25f*ph))+Up*((H(seed+i,33)-.5f)*.9f);s.Streak(p,-dir*6,Lin(Violet,.9f*(1-ph)*g),.03f,.4f,.04f,.9f,ph,seed+i);}}
                // A pulse drawn in on each tick.
                float tick=hole%CyberKit2Spec.HoleTick;if(hole>=CyberKit2Spec.HoleTick*.5f&&tick<.25f){float u=tick/.25f;s.Sprite(false,centre,Lin(Magenta,(reduced?1.2f:2.2f)*(1-u)),MeleeMath.Lerp(4.5f,1.1f,Smooth(u)),RingSprite);}
                // The pulled pilots stretched toward it.
                var list=scene.Actors;
                for(int j=0;j<list.Count;j++)
                {
                    var o=list[j];if(o==null||!o.Alive||o.Id==a.Id||o.Id==scene.Viewer||!scene.Chest(o,out var oc))continue;var dd=centre-oc;float dl=dd.Length();if(dl>CyberKit2Spec.PullRadius||dl<.3f)continue;
                    dd/=dl;float pull=CyberKit2Spec.Pull(dl)/(2*CyberKit2Spec.PullSpeed);for(int m=0;m<2;m++){var o0=oc+Across(dd)*((m-.5f)*.25f);s.Streak(o0+dd*(.4f*pull),dd*8*pull,Lin(Violet,1.6f*pull),.03f,.3f+.4f*pull,.03f,.9f,pull,seed+o.Id+m);}
                }
            }
            // The implosion: a white point, then the shock thrown out to its radius.
            float sh=time-implode;
            if(sh>=-Collapse&&sh<ShockLife)
            {
                if(sh<0){float u=1+sh/Collapse;s.Sprite(false,centre,Lin(HotWhite,4*u),.4f+.6f*u,Hot);}
                else
                {
                    float u=sh/ShockLife,k2=1-Smooth(u),R=CyberKit2Spec.ImplodeRadius*Smooth(Math.Min(1,u*1.8f));
                    s.Sprite(false,centre,Lin(HotWhite,(reduced?2.4f:4.5f)*(1-Smooth(u/.3f))),2.2f,Hot);s.Glow(centre,Lin(Magenta,1.6f*k2),0,2+5*u,Core,u,seed+5);s.Sprite(false,centre,Lin(Cyan,2.4f*k2),2*R,RingSprite);
                    int seg=low?14:28;var prev=centre+new Vector3(R,0,0);for(int e=1;e<=seg;e++){float ang=e*MeleeMath.TAU/seg;var p=centre+new Vector3((float)Math.Cos(ang)*R,0,(float)Math.Sin(ang)*R);s.Line(false,prev,p,Lin(e%2==0?Magenta:HotWhite,2.2f*k2),.06f,false);prev=p;}
                    int rays=reduced?8:low?10:20;for(int i=0;i<rays;i++){float ang=i*MeleeMath.TAU/rays+H(seed+i,35)*.3f;var dir=Vector3.Normalize(new Vector3((float)Math.Cos(ang),(H(seed+i,37)-.4f)*.8f,(float)Math.Sin(ang)));s.Streak(centre+dir*(R*.9f),dir*12,Lin(i%2==0?Violet:Cyan,2.2f*k2),.05f,.5f,.03f,.9f,u,seed+i);}
                    int shards=reduced?5:low?6:12;for(int i=0;i<shards;i++){float ang=i*2.39996f;var dir=Vector3.Normalize(new Vector3((float)Math.Cos(ang),.3f+.5f*H(seed+i,39),(float)Math.Sin(ang)));var p=centre+dir*(9*sh)-Up*(6*sh*sh);if(p.Y<centre.Y-CyberKit2Ranged.HoleLift+.15f)continue;float z=.09f*k2;s.Voxel(false,WeaponShape.Cube,p,Quaternion.CreateFromAxisAngle(Up,sh*10+i),new Vector3(z,z,z*.3f),Lin(i%2==0?Magenta:HotWhite,2.4f));}
                    if(!low&&!reduced)for(int i=0;i<6;i++){float ang=i*MeleeMath.TAU/6;s.Smoke(centre+new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*(R*.7f)-Up*.5f,Lin(0x2a2338),.35f*k2,.8f+.8f*u,u,seed+i,centre.Y-CyberKit2Ranged.HoleLift);}
                }
            }
        }

        // ---- 3 Packet Storm ----
        private void PacketStorm(ICyberFxSink s,Actor a,CyberKit2RangedView k,float time,Vector3 hand,float q,bool low,bool me)
        {
            const int T=CyberKit2Ranged.Storm;float at=k.At[T];if(!(at>-90)||time<at)return;bool reduced=q<.5f;long seed=a.Id*3301L+(long)Math.Round(at*240);float rel=k.Release[T];
            bool cast=k.Released(T);float end=cast?k.StormEnd:k.Until[T];if(time>end+DropBurst)return;var c=k.StormCentre;float R=CyberKit2Spec.StormRadius,floor=c.Y;int step=(int)(time*(reduced?6:24));
            // The call: a beam of data from the raised hand to the sky through the windup, flashing at the release.
            if(time<rel+.25f&&!me)
            {
                float u=Clamp01((time-at)/Math.Max(.01f,rel-at)),f=time<rel?u:1-Smooth((time-rel)/.25f);var top=hand+Up*(CyberKit2Ranged.SkyHeight*u);
                Beam(s,false,hand,top,.05f+.06f*f,Lin(HotWhite,2.6f*f));Beam(s,false,hand,top,.16f*f,Lin(Cyan,1.4f*f));s.Sprite(false,hand,Lin(HotWhite,2.4f*f),.3f,Hot);
            }
            if(!cast)return;
            // The ring: a floating hex ring with upright posts at its corners and an inner dashed ring turning the other way.
            float form=Smooth((time-at)/.2f)*(1-Smooth((time-end)/.3f));
            if(form>1e-3f)
            {
                int sides=12;float y=floor+RingLift,turn=time*(reduced?.08f:.25f);
                for(int e=0;e<sides;e++)
                {
                    float a0=turn+e*MeleeMath.TAU/sides,a1=a0+MeleeMath.TAU/sides;var p0=new Vector3(c.X+(float)Math.Sin(a0)*R,y,c.Z+(float)Math.Cos(a0)*R);var p1=new Vector3(c.X+(float)Math.Sin(a1)*R,y,c.Z+(float)Math.Cos(a1)*R);
                    s.Line(false,p0,p1,Lin(Magenta,1.8f*form),.05f,false);float stut=reduced?0:(H(seed+e+step,3)-.5f)*.15f;s.Line(false,p0,p0+Up*((.35f+.25f*H(seed+e,5))*form+stut),Lin(Cyan,2*form),.035f,false);
                }
                if(!low){float ri=R*.88f;for(int e=0;e<24;e+=2){float a0=-turn*1.6f+e*MeleeMath.TAU/24,a1=a0+MeleeMath.TAU/24;s.Line(false,new Vector3(c.X+(float)Math.Sin(a0)*ri,y+.04f,c.Z+(float)Math.Cos(a0)*ri),new Vector3(c.X+(float)Math.Sin(a1)*ri,y+.04f,c.Z+(float)Math.Cos(a1)*ri),Lin(Cyan,1.2f*form),.03f,false);}}
                // The glitch portal overhead.
                var top=c+Up*CyberKit2Ranged.SkyHeight;float pr=3.2f*form;for(int e=0;e<6;e++){float a0=-turn*2+e*MeleeMath.TAU/6,a1=a0+MeleeMath.TAU/6;s.Line(false,top+new Vector3((float)Math.Sin(a0)*pr,0,(float)Math.Cos(a0)*pr),top+new Vector3((float)Math.Sin(a1)*pr,0,(float)Math.Cos(a1)*pr),Lin(e%2==0?Magenta:Cyan,2.4f*form),.12f,false);}
                s.Glow(top,Lin(Violet,1.2f*form),0,7*form,Core,MeleeMath.Frac(time*.3f),seed);
            }
            // The drops.
            for(int i=0;i<CyberKit2Spec.Drops;i++)
            {
                k.Drop(i,out float hit,out var p);float lead=time-(hit-DropLead),fall=time-(hit-CyberKit2Spec.FallSeconds),burst=time-hit;if(lead<0||burst>=DropBurst)continue;long ds=seed*17+i;
                // The telegraph: brackets standing round the point, a light column over it.
                if(burst<0)
                {
                    float u=Clamp01(lead/DropLead);for(int e=0;e<4;e++){float ang=e*MeleeMath.TAU/4+.785f;var b=p+new Vector3((float)Math.Sin(ang),0,(float)Math.Cos(ang))*(.55f-.15f*u);b.Y=floor+.14f;s.Line(false,b,b+Up*(.25f+.25f*u),Lin(Magenta,1.4f+u),.03f,false);}
                    Beam(s,false,new Vector3(p.X,floor+.14f,p.Z),new Vector3(p.X,floor+3.5f,p.Z),.08f+.1f*u,Lin(Cyan,.6f+1.2f*u));
                }
                // The meteor: a glowing cube falling on a slant, trailing flame and pixels.
                if(fall>=0&&burst<0)
                {
                    float u=fall/CyberKit2Spec.FallSeconds;var slant=new Vector3((H(ds,7)-.5f)*5,CyberKit2Ranged.SkyHeight*.7f,(H(ds,9)-.5f)*5);var from=p+slant;var pos=Vector3.Lerp(from,p+Up*.25f,(float)Math.Pow(u,1.5));var vel=(p-from)*(1.5f*(float)Math.Pow(Math.Max(.05f,u),.5)/CyberKit2Spec.FallSeconds);var dir=Vector3.Normalize(p-from);
                    var spin=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(H(ds,11),1,H(ds,13))),time*7+i);s.Voxel(false,WeaponShape.Cube,pos,spin,new Vector3(.5f,.5f,.5f),Lin(Magenta,2.4f));s.Voxel(false,WeaponShape.Cube,pos,spin,new Vector3(.3f,.3f,.3f),Lin(HotWhite,3.2f));
                    Flame(s,false,pos,pos-dir*2.6f+Across(dir)*((H(ds+step,15)-.5f)*.3f),Lin(Magenta,2.2f),.65f,H(ds+step,17));Flame(s,false,pos,pos-dir*1.6f,Lin(Cyan,2.4f),.32f,H(ds+step,19));
                    s.Glow(pos,Lin(Plasma,1.2f),0,2f,Core,u,ds);s.Streak(pos,vel,Lin(Core,1.8f),.16f,2.4f,.02f,.9f,u,ds);s.Line(false,pos-dir*2.4f,pos-dir*7,Lin(Cyan,.9f*(1-u*.5f)),.05f,false);
                    if(!low)for(int m=0;m<(reduced?2:5);m++){float te=Math.Max(0,u-.07f*(m+1));var tp=Vector3.Lerp(from,p+Up*.25f,(float)Math.Pow(te,1.5))+new Vector3(H(ds+m,21)-.5f,0,H(ds+m,23)-.5f)*.3f;float z=.06f*(1-m/6f);s.Voxel(false,WeaponShape.Cube,tp,Quaternion.Identity,new Vector3(z,z,z),Lin(m%2==0?Cyan:Magenta,2.2f));}
                }
                // The landing: a burst, a floating ring out to its radius, shards and a data pillar.
                if(burst>=0)
                {
                    float u=burst/DropBurst,k2=1-Smooth(u),flash=reduced?.7f*(1-Smooth(u/.4f)):1-Smooth(u/.2f),rr=CyberKit2Spec.DropRadius*Smooth(Math.Min(1,u*2.5f));
                    s.Sprite(false,p+Up*.3f,Lin(HotWhite,4*flash),1.2f,Hot);s.Glow(p+Up*.4f,Lin(Plasma,1.4f*k2),0,1.6f+1.6f*u,Core,u,ds);
                    int seg=low?8:16;var prev=new Vector3(p.X+rr,floor+RingLift,p.Z);for(int e=1;e<=seg;e++){float ang=e*MeleeMath.TAU/seg;var pe=new Vector3(p.X+(float)Math.Cos(ang)*rr,floor+RingLift,p.Z+(float)Math.Sin(ang)*rr);s.Line(false,prev,pe,Lin(e%2==0?Cyan:Magenta,2.2f*k2),.05f,false);prev=pe;}
                    int petals=reduced?4:low?4:8;for(int e=0;e<petals;e++){float ang=e*MeleeMath.TAU/petals+H(ds,25);var dir=Vector3.Normalize(new Vector3((float)Math.Cos(ang),1.1f,(float)Math.Sin(ang)));Flame(s,false,p+Up*.15f,p+Up*.15f+dir*(.3f+.7f*Smooth(Math.Min(1,u*3)))*(1-u*.5f),e%2==0?Lin(Magenta,2*k2*k2):Lin(Cyan,2.2f*k2*k2),.2f*(1-u),H(ds+e,27));}
                    int shards=reduced?3:low?4:8;for(int e=0;e<shards;e++){float ang=e*2.39996f+H(ds,29);var dir=Vector3.Normalize(new Vector3((float)Math.Cos(ang),.8f+.6f*H(ds+e,31),(float)Math.Sin(ang)));var sp=p+dir*(5*burst)-Up*(7*burst*burst);if(sp.Y<floor+.14f)continue;float z=.08f*k2;s.Voxel(false,WeaponShape.Cube,sp,Quaternion.CreateFromAxisAngle(Up,burst*12+e),new Vector3(z,z,z*.3f),Lin(e%2==0?HotWhite:Cyan,2.4f));}
                    if(u<.5f){float pu=u/.5f;s.Line(false,new Vector3(p.X,floor+.14f,p.Z),new Vector3(p.X,floor+.14f+2.6f*(1-pu*.4f),p.Z),Lin(Cyan,2.4f*(1-pu)),.12f*(1-pu),false);}
                    if(!low&&!reduced)s.Smoke(p+Up*(.3f+.5f*u),Lin(0x2a2338),.4f*k2,.7f+.7f*u,u,ds,floor);
                }
            }
        }

        // ---- 4 Orbital Uplink ----
        public const int MaxOrbitals=8,OrbitalTrail=12;public const float TrailStep=.08f,TrailLife=.8f;
        private readonly int[] orbitalId=new int[MaxOrbitals];private readonly float[] orbitalNoted=new float[MaxOrbitals],orbitalStart=new float[MaxOrbitals];
        private readonly Vector3[] orbitalAt=new Vector3[MaxOrbitals*OrbitalTrail];private readonly float[] orbitalWhen=new float[MaxOrbitals*OrbitalTrail];private readonly int[] orbitalCount=new int[MaxOrbitals];
        // The column's recent path (a ring per caster, noted every TrailStep while it burns; a new column or a rewound clock clears it).
        private int OrbitalSlot(int id,float start,float time)
        {
            int slot=-1,stale=0;for(int i=0;i<MaxOrbitals;i++){if(orbitalId[i]==id+1){slot=i;break;}if(orbitalNoted[i]<orbitalNoted[stale])stale=i;}
            if(slot<0){slot=stale;orbitalId[slot]=id+1;orbitalCount[slot]=0;orbitalNoted[slot]=-100;orbitalStart[slot]=start;}
            if(Math.Abs(orbitalStart[slot]-start)>1e-3f||time<orbitalNoted[slot]-.05f){orbitalCount[slot]=0;orbitalNoted[slot]=-100;orbitalStart[slot]=start;}
            return slot;
        }
        private void OrbitalFx(ICyberFxSink s,Actor a,CyberKit2RangedView k,float time,Vector3 lf,Vector3 rf,float q,bool low,bool me)
        {
            const int O=CyberKit2Ranged.Orbital;float at=k.At[O];if(!(at>-90)||time<at)return;bool reduced=q<.5f;long seed=a.Id*2203L+(long)Math.Round(at*240);float rel=k.Release[O],until=k.Until[O];
            if(time>=until+ColumnFade)return;int step=(int)(time*(reduced?6:24));
            // The uplink: from the raised left hand to the sky through the call-down and the steering.
            if(!me&&a.Alive)
            {
                float f=Smooth((time-at)/.2f)*(time<until?1:1-Smooth((time-until)/ColumnFade)),hum=reduced?1:.8f+.4f*H(seed+step,3);var top=lf+Up*(CyberKit2Ranged.BeamHeight*Smooth((time-at)/.3f));
                Beam(s,false,lf,top,.035f*f,Lin(HotWhite,2.6f*f*hum));Beam(s,false,lf,top,.11f*f,Lin(Cyan,1.2f*f));s.Sprite(false,lf,Lin(Cyan,2.2f*f),.3f,Hot);
                // The steering link: a faint line from the pointing hand to the column's foot.
                if(time>=rel){var link=k.OrbitalAt(time)+Up*.6f;s.Line(false,rf,link,Lin(Cyan,.7f*f),.02f,false);}
            }
            // The call-down: the targeting ring closing in, brackets turning, a crosshair and the spotter line.
            if(time<rel+.15f)
            {
                float u=Clamp01((time-at)/Math.Max(.01f,rel-at)),f=time<rel?Smooth(u/.3f):1-Smooth((time-rel)/.15f);var c=k.OrbitalCentre;float cy=c.Y+RingLift,R=MeleeMath.Lerp(3.6f,CyberKit2Spec.OrbitalRadius,Smooth(u)),turn=time*(reduced?.5f:2);
                int arcs=low?12:20;for(int e=0;e<arcs;e++){if(e%5==4)continue;float a0=turn+e*MeleeMath.TAU/arcs,a1=a0+MeleeMath.TAU/arcs;s.Line(false,new Vector3(c.X+(float)Math.Sin(a0)*R,cy,c.Z+(float)Math.Cos(a0)*R),new Vector3(c.X+(float)Math.Sin(a1)*R,cy,c.Z+(float)Math.Cos(a1)*R),Lin(Magenta,2*f),.06f,false);}
                for(int e=0;e<4;e++){float ang=-turn*.6f+e*MeleeMath.TAU/4;var b=new Vector3(c.X+(float)Math.Sin(ang)*R*1.12f,cy-.08f,c.Z+(float)Math.Cos(ang)*R*1.12f);s.Line(false,b,b+Up*(.5f+.4f*u),Lin(Cyan,2.2f*f),.05f,false);}
                s.Line(false,new Vector3(c.X-.6f,cy,c.Z),new Vector3(c.X+.6f,cy,c.Z),Lin(HotWhite,1.6f*f),.03f,false);s.Line(false,new Vector3(c.X,cy,c.Z-.6f),new Vector3(c.X,cy,c.Z+.6f),Lin(HotWhite,1.6f*f),.03f,false);
                Beam(s,false,new Vector3(c.X,c.Y+.14f,c.Z),new Vector3(c.X,c.Y+CyberKit2Ranged.BeamHeight,c.Z),(.04f+.2f*u)*f,Lin(Cyan,(.8f+1.4f*u)*f));
            }
            if(!(k.OrbitalPointAt>-90)||time<rel)return;
            // The column: a white core in a cyan sheath with magenta edges, from the sky to the chased floor point.
            var p=k.OrbitalAt(Math.Min(time,until));float out1=time<until?Smooth((time-rel)/.08f):1-Smooth((time-until)/ColumnFade),beat=(time-rel)%CyberKit2Spec.OrbitalTick,pulse=reduced?1:1+.35f*(1-Smooth(beat/.12f));
            var foot=new Vector3(p.X,p.Y+.14f,p.Z);var sky=new Vector3(p.X,p.Y+CyberKit2Ranged.BeamHeight,p.Z);float flick=reduced?1:.92f+.16f*H(seed+step,5);
            // A camera in or near the column (a pilot it chases, or a chase camera it sweeps past) sees it dimmed, not a white screen:
            // 30% within 1 m of its axis, full from 7 m.
            var toEye=eye-foot;toEye.Y=0;float inside=.3f+.7f*Smooth(((float)Math.Sqrt(toEye.LengthSquared())-1)/6),glow=out1*inside;
            Beam(s,false,foot,sky,.85f*out1*flick*pulse,Lin(HotWhite,3.2f*glow));Beam(s,false,foot,sky,2.3f*out1*flick,Lin(Cyan,1.5f*glow*pulse));Beam(s,false,foot,sky,3.6f*out1,Lin(Deep,.8f*glow));
            var side=toEye.LengthSquared()>1e-6f?Vector3.Normalize(Vector3.Cross(Up,toEye)):Vector3.UnitX;
            if(!low){s.Line(false,foot+side*1.15f*out1,sky+side*1.15f*out1,Lin(Magenta,1.8f*out1),.06f,false);s.Line(false,foot-side*1.15f*out1,sky-side*1.15f*out1,Lin(Magenta,1.8f*out1),.06f,false);}
            // Code blocks pouring down the column.
            int blocks=reduced?8:low?10:22;for(int i=0;i<blocks;i++){float ph=MeleeMath.Frac(time*(.7f+.4f*H(seed+i,7))+H(seed+i,9)),hgt=CyberKit2Ranged.BeamHeight*.45f*(1-ph),ang=H(seed+i,11)*MeleeMath.TAU+time*3;var bp=foot+Up*hgt+new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*(.75f*out1);float z=.1f*out1;s.Voxel(false,WeaponShape.Cube,bp,Quaternion.CreateFromAxisAngle(Up,ang),new Vector3(z,z*1.6f,z*.4f),Lin(i%3==0?HotWhite:i%3==1?Cyan:Magenta,2.4f));}
            // The foot: a white-hot splash, the ground burning round it (upright flame licks, embers, smoke), a pulse each tick.
            s.Sprite(false,foot+Up*.2f,Lin(HotWhite,3.4f*glow*pulse),2.2f*out1,Hot);s.Glow(foot+Up*.4f,Lin(Cyan,1.2f*glow),0,4.2f*out1,Core,MeleeMath.Frac(time*.8f),seed);
            int licks=reduced?5:low?6:12;for(int i=0;i<licks;i++){float ang=i*MeleeMath.TAU/licks+H(seed+i,13),rr=CyberKit2Spec.OrbitalRadius*(.55f+.4f*H(seed+i,15));var b=foot+new Vector3((float)Math.Cos(ang)*rr,0,(float)Math.Sin(ang)*rr);float h=(.45f+.6f*H(seed+i+step,17))*out1;Flame(s,false,b,b+Up*h+new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*(.15f*h),i%2==0?Lin(Plasma,1.9f*out1):Lin(Cyan,2.1f*out1),.32f*out1,H(seed+i+step,19));}
            float y=p.Y+RingLift;int seg=low?10:18;float pr=CyberKit2Spec.OrbitalRadius*(1+.08f*(pulse-1));var prev=new Vector3(p.X+pr,y,p.Z);for(int e=1;e<=seg;e++){float ang=e*MeleeMath.TAU/seg;var pe=new Vector3(p.X+(float)Math.Cos(ang)*pr,y,p.Z+(float)Math.Sin(ang)*pr);s.Line(false,prev,pe,Lin(e%2==0?Magenta:Cyan,1.8f*out1*pulse),.05f,false);prev=pe;}
            if(!low){int em=reduced?3:8;for(int i=0;i<em;i++){float u=MeleeMath.Frac(time*1.3f+i/(float)em);int gen=(int)Math.Floor(time*1.3f+i/(float)em);var b=foot+new Vector3(H(seed+gen*7+i,21)-.5f,0,H(seed+gen*5+i,23)-.5f)*(2*CyberKit2Spec.OrbitalRadius)+Up*(2.2f*u);float z=.05f*(1-u);s.Voxel(false,WeaponShape.Cube,b,Quaternion.CreateFromAxisAngle(Up,u*6+i),new Vector3(z,z,z),Lin(i%2==0?Plasma:HotWhite,2.4f*(1-u)*out1));}
                if(!reduced)s.Smoke(foot+Up*.6f,Lin(0x241e30),.35f*out1,1.4f,MeleeMath.Frac(time*.5f),seed,p.Y);}
            // The burn along its recent path.
            int slot=OrbitalSlot(a.Id,rel,time);int n=orbitalCount[slot];
            if(time<until&&time-orbitalNoted[slot]>=TrailStep){int at2=slot*OrbitalTrail;if(n>=OrbitalTrail){Array.Copy(orbitalAt,at2+1,orbitalAt,at2,OrbitalTrail-1);Array.Copy(orbitalWhen,at2+1,orbitalWhen,at2,OrbitalTrail-1);n=OrbitalTrail-1;}orbitalAt[at2+n]=p;orbitalWhen[at2+n]=time;orbitalCount[slot]=++n;orbitalNoted[slot]=time;}
            for(int i=0;i<n;i++)
            {
                float age=time-orbitalWhen[slot*OrbitalTrail+i];if(age<0||age>TrailLife)continue;var tp=orbitalAt[slot*OrbitalTrail+i];if(Vector3.DistanceSquared(tp,p)<.8f*.8f)continue;float u=age/TrailLife;
                var b=new Vector3(tp.X,tp.Y+.14f,tp.Z);Flame(s,false,b,b+Up*(.5f*(1-u)),Lin(Plasma,1.6f*(1-u)),.28f*(1-u),H(seed+i+step,25));if(!low)s.Sprite(false,b+Up*.15f,Lin(Magenta,1.2f*(1-u)),.8f*(1-u*.5f),Soft);
            }
        }
    }
}
