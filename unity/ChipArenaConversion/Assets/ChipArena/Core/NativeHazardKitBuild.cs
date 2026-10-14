using System;

namespace ChipArena.UnityConversion
{
    /// <summary>One box the kit draws this frame (world centre, rotation, half extents), as NativeBlockLit instances: linear
    /// colour, the shader look and flags (NativeBlockLooks / NativeCyberCityLooks with its bank flag), and the emissive level
    /// of glowing looks (1 for the others). Shadow: casts; Detail: left out on the low graphics tier.</summary>
    public struct NativeKitPart {public float X,Y,Z,Qx,Qy,Qz,Qw,Hx,Hy,Hz,R,G,B,Level;public int Look,Flags;public bool Shadow,Detail;}
    /// <summary>A practical-light candidate: world place, range, linear colour times intensity.</summary>
    public struct NativeKitLight {public float X,Y,Z,Range,R,G,B;public int Piece;}
    /// <summary>A turret round for the weapon effects renderer (Runtime NativeWeaponFxShapes: the guns' muzzle star, tracer and
    /// impact): its time, the muzzle and its frame (forward x, up y, side z), and the hit point (NaN x: none in reach).</summary>
    public struct NativeKitShot {public double At;public V3 Muzzle,Forward,Up,Side,Hit;public int Piece,Round;}

    /// <summary>
    /// A map's (or the DEV gallery's) hazard-kit pieces in one frame (docs/HAZARD-KIT.md): the trigger ledger, each piece's
    /// phase, and the look: boxes (<see cref="Parts"/>), camera-facing effects (<see cref="Quads"/>, NativeFxDraw's quads),
    /// light candidates (<see cref="Lights"/>, merged into the eight practical lights by <see cref="NativeKitLightPool"/>) and
    /// turret rounds (<see cref="Shots"/>). Presentation only: it reads the authority's events and states and never writes
    /// one. Allocation free after construction (fixed buffers; the build refuses parts past its capacity).
    /// </summary>
    public sealed class NativeKitScene
    {
        public const int PartCapacity=7000,QuadCapacity=2400,LightCapacity=48,ShotCapacity=32;
        public readonly KitRecord[] Records;public readonly V3[] At;public readonly FactoryQ[] Rot;
        public readonly NativeKitPhase[] Phases;public readonly NativeTurretView[] Turrets;public readonly NativeKitSawPose[] Saws;
        public readonly double[] Fired;public readonly bool[] HasFollow;public readonly NativeHazardState[] Followed;
        public readonly float[] Blood;private readonly double[][] hits;private readonly int[] hitCount;
        public readonly NativeKitPart[] Parts=new NativeKitPart[PartCapacity];public int PartCount;
        public readonly NativeFxQuads Quads=new NativeFxQuads(QuadCapacity);
        public readonly NativeKitLight[] Lights=new NativeKitLight[LightCapacity];public int LightCount;
        public readonly NativeKitShot[] Shots=new NativeKitShot[ShotCapacity];public int ShotCount;
        /// <summary>The gallery's test bays are drawn round the pieces (presentation preview).</summary>
        public bool Bays;public double Now=double.NaN;public NativeKitFlash Mode;public NativeKitBeat Beat;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,quality=1;private bool low;

        public NativeKitScene(KitRecord[] records,Func<string,(bool found,V3 pos,FactoryQ rot)> anchor=null)
        {
            Records=records??Array.Empty<KitRecord>();int n=Records.Length;At=new V3[n];Rot=new FactoryQ[n];Phases=new NativeKitPhase[n];Turrets=new NativeTurretView[n];Saws=new NativeKitSawPose[n];
            Fired=new double[n];HasFollow=new bool[n];Followed=new NativeHazardState[n];Blood=new float[n];hits=new double[n][];hitCount=new int[n];
            for(int i=0;i<n;i++)
            {
                var r=Records[i];Fired[i]=double.NaN;hits[i]=new double[8];Turrets[i]=NativeTurretView.Offline;
                V3 ap=default;var aq=FactoryQ.Identity;if(!string.IsNullOrEmpty(r.anchor)&&anchor!=null){var f=anchor(r.anchor);if(f.found){ap=f.pos;aq=f.rot;}}
                NativeHazardKit.Frame(r,ap,aq,out At[i],out Rot[i]);
            }
        }
        /// <summary>Forgets every fire, hit and follow (a new round, a rewound clock).</summary>
        public void Reset(){for(int i=0;i<Records.Length;i++){Fired[i]=double.NaN;hitCount[i]=0;HasFollow[i]=false;}Now=double.NaN;}

        // ---------------- the trigger ledger ----------------
        /// <summary>
        /// An authority event (<see cref="NativeHazardKit.Events"/>) at time `at`, optionally at a place and of a stage hazard:
        /// every event piece that names it (within its radius, of its hazard) fires. Idempotent: the same or an older event
        /// changes nothing, so a caller may report its sources every frame. A piece already active stays active from `at`.
        /// Returns how many pieces fired.
        /// </summary>
        public int Fire(string evt,double at,bool hasWhere=false,V3 where=default,string hazard=null)
        {
            int n=0;if(evt==null||!(at>-1e8))return 0;
            for(int i=0;i<Records.Length;i++)
            {
                var r=Records[i];var t=r.trigger;if(t==null||NativeHazardKit.Mode(r)!="event"||t.events==null)continue;bool named=false;foreach(var e in t.events)if(e==evt){named=true;break;}if(!named)continue;
                if(t.hazard!=null&&hazard!=null&&t.hazard!=hazard)continue;
                if(t.radius>0&&hasWhere){float dx=where.x-At[i].x,dy=where.y-At[i].y,dz=where.z-At[i].z;if(dx*dx+dy*dy+dz*dz>t.radius*t.radius)continue;}
                if(FireAt(i,at))n++;
            }
            return n;
        }
        /// <summary>Fires piece `i` at `at` (the DEV preview's and an adapter's manual trigger). False when nothing changes.</summary>
        public bool FireAt(int i,double at)
        {
            if(i<0||i>=Records.Length||!(at>-1e8))return false;double f=Fired[i];if(!double.IsNaN(f)&&at<=f+1e-6)return false;
            var r=Records[i];var p=NativeHazardKitClock.Phase(r,at,f);
            // Re-armed while active (or arming): it holds active from `at` instead of warning again.
            Fired[i]=p.State==NativeKitState.Active||p.State==NativeKitState.Arming&&!double.IsNaN(f)?Math.Min(at,at-NativeHazardKit.Warn(r)+(p.State==NativeKitState.Arming?p.Since:0)):at;
            if(p.State==NativeKitState.Arming)Fired[i]=f;return true;
        }
        /// <summary>The piece of id `id` (-1 none).</summary>
        public int Find(string id){for(int i=0;i<Records.Length;i++)if(Records[i].id==id)return i;return -1;}
        /// <summary>A saw's hit (from the authority's hit event): blood on its blade.</summary>
        public void Hit(int i,double at){if(i<0||i>=Records.Length)return;var h=hits[i];for(int k=0;k<hitCount[i];k++)if(Math.Abs(h[k]-at)<1e-6)return;if(hitCount[i]<h.Length)h[hitCount[i]++]=at;else{Array.Copy(h,1,h,0,h.Length-1);h[h.Length-1]=at;}}
        /// <summary>Piece `i` follows a stage hazard this frame (its state from the hazard's own pure clock).</summary>
        /// <summary>The latest hit on piece `i` in the clock window (from, to] (a saw's wet chop), false when none.</summary>
        public bool HitIn(int i,double from,double to,out double at){at=double.NaN;if(i<0||i>=Records.Length)return false;var h=hits[i];for(int k=0;k<hitCount[i];k++)if(h[k]>from&&h[k]<=to&&!(h[k]<=at))at=h[k];return !double.IsNaN(at);}
        public void Follow(int i,in NativeHazardState s){if(i<0||i>=Records.Length)return;HasFollow[i]=true;Followed[i]=s;}

        /// <summary>Every piece's phase at `now` (a clock that ran back forgets the fires: the sources fire again). Turrets take
        /// `turret` (the authority's view; null: their preview timeline when `preview`, else offline).</summary>
        public void Step(double now,NativeKitFlash mode,in NativeKitBeat beat,bool preview,NativeTurretView[] turret=null)
        {
            if(!double.IsNaN(Now)&&now<Now-.5)Reset();Now=now;Mode=mode;Beat=beat;
            for(int i=0;i<Records.Length;i++)
            {
                var r=Records[i];Phases[i]=NativeHazardKitClock.Phase(r,now,Fired[i],HasFollow[i],Followed[i]);HasFollow[i]=false;
                if(r.kind==NativeHazardKit.Turret)Turrets[i]=turret!=null&&i<turret.Length?turret[i]:preview?NativeTurretPreview.At(r,now+i*1.7):NativeTurretView.Offline;
                if(r.kind==NativeHazardKit.Saw){Saws[i]=NativeKitSaw.Pose(r,Phases[i],now,At[i],Rot[i]);Blood[i]=NativeKitSaw.Blood(hits[i],hitCount[i],now);}
            }
        }

        // ---------------- the look ----------------
        private const int Bk=NativeCyberCityLooks.Bank,LMetal=NativeCyberCityLooks.Metal,LTube=NativeCyberCityLooks.Tube,LGlass=NativeCyberCityLooks.Glass,LPanel=NativeCyberCityLooks.Panel,LGrate=NativeCyberCityLooks.Grate;
        private const int LPlain=NativeBlockLooks.Plain,LStripe=NativeBlockLooks.Demolition,LSteel=NativeBlockLooks.SteelGreen;
        private const int Dark=0x22272C,Steel=0x5A646A,SteelLight=0x8C969C,Black=0x0E1012,Rubber=0x1A1A1C,Laser=0xFF2E2E,Scan=0x7FFFE8,Track=0xFFB020,Safe=0x3DFF7A;
        private int part;private FactoryQ q;private V3 o;private float k=1;
        private static double Lin(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);
        // A box in the current frame (o, q, scale k): local centre, half extents, its own local turn.
        private void Box(double x,double y,double z,double hx,double hy,double hz,int hex,int look,int flags,float level=1,bool shadow=true,bool detail=false,FactoryQ? turn=null)
        {
            if(PartCount>=Parts.Length||!(hx>0&&hy>0&&hz>0))return;q.Rotate(x*k,y*k,z*k,out double wx,out double wy,out double wz);var rq=turn.HasValue?q*turn.Value:q;
            Parts[PartCount++]=new NativeKitPart{X=(float)(o.x+wx),Y=(float)(o.y+wy),Z=(float)(o.z+wz),Qx=(float)rq.X,Qy=(float)rq.Y,Qz=(float)rq.Z,Qw=(float)rq.W,Hx=(float)(hx*k),Hy=(float)(hy*k),Hz=(float)(hz*k),
                R=(float)Lin(hex,16),G=(float)Lin(hex,8),B=(float)Lin(hex,0),Level=level,Look=look,Flags=flags,Shadow=shadow,Detail=detail};
        }
        private void Lamp(double x,double y,double z,double hx,double hy,double hz,int hex,float level,FactoryQ? turn=null)=>Box(x,y,z,hx,hy,hz,hex,LTube,Bk,Math.Max(.02f,level),false,false,turn);
        private V3 W(double x,double y,double z){q.Rotate(x*k,y*k,z*k,out double wx,out double wy,out double wz);return new V3((float)(o.x+wx),(float)(o.y+wy),(float)(o.z+wz));}
        private V3 D(double x,double y,double z){q.Rotate(x,y,z,out double wx,out double wy,out double wz);return new V3((float)wx,(float)wy,(float)wz);}
        private void Light(V3 p,float range,int hex,float intensity)
        {
            if(LightCount>=Lights.Length||!(intensity>.02f))return;Lights[LightCount++]=new NativeKitLight{X=p.x,Y=p.y,Z=p.z,Range=range,R=(float)Lin(hex,16)*intensity,G=(float)Lin(hex,8)*intensity,B=(float)Lin(hex,0)*intensity,Piece=part};
        }
        private void Sprite(V3 p,double size,int hex,double a,bool additive=true)
        {
            double h=size/2,ax=rx*h,ay=ry*h,az=rz*h,bx=ux*h,by=uy*h,bz=uz*h;double r=Lin(hex,16),g=Lin(hex,8),b=Lin(hex,0);
            Quads.Add(p.x-ax-bx,p.y-ay-by,p.z-az-bz,p.x+ax-bx,p.y+ay-by,p.z+az-bz,p.x+ax+bx,p.y+ay+by,p.z+az+bz,p.x-ax+bx,p.y-ay+by,p.z-az+bz,r,g,b,Math.Min(1,a),1,additive,1);
        }
        private void Sprite(V3 p,double size,double r,double g,double b,double a,bool additive)
        {
            double h=size/2,ax=rx*h,ay=ry*h,az=rz*h,bx=ux*h,by=uy*h,bz=uz*h;
            Quads.Add(p.x-ax-bx,p.y-ay-by,p.z-az-bz,p.x+ax-bx,p.y+ay-by,p.z+az-bz,p.x+ax+bx,p.y+ay+by,p.z+az+bz,p.x-ax+bx,p.y-ay+by,p.z-az+bz,r,g,b,Math.Min(1,a),1,additive,1);
        }
        // A camera-facing ribbon from a to b (mode 0 flat or 1 soft); colours straight (NativeSirenFx multiplies by alpha).
        private void Line(V3 a,V3 b,double width,double r,double g,double bl,double alpha,bool additive=true,int mode=0)
        {
            double dx=b.x-a.x,dy=b.y-a.y,dz=b.z-a.z,mx=(a.x+b.x)/2-ex,my=(a.y+b.y)/2-ey,mz=(a.z+b.z)/2-ez;
            double sx=dy*mz-dz*my,sy=dz*mx-dx*mz,sz=dx*my-dy*mx,sl=Math.Sqrt(sx*sx+sy*sy+sz*sz);if(sl<1e-9)return;sx*=width/2/sl;sy*=width/2/sl;sz*=width/2/sl;
            Quads.Add(a.x-sx,a.y-sy,a.z-sz,b.x-sx,b.y-sy,b.z-sz,b.x+sx,b.y+sy,b.z+sz,a.x+sx,a.y+sy,a.z+sz,r,g,bl,Math.Min(1,alpha),mode,additive,1);
        }
        // A cone of light from p along d (length, widths at both ends) as a camera-facing trapezoid with soft falloff.
        private void Cone(V3 p,V3 d,double length,double w0,double w1,int hex,double a)
        {
            V3 e=p+d*(float)length;double mx=(p.x+e.x)/2-ex,my=(p.y+e.y)/2-ey,mz=(p.z+e.z)/2-ez;double sx=d.y*mz-d.z*my,sy=d.z*mx-d.x*mz,sz=d.x*my-d.y*mx,sl=Math.Sqrt(sx*sx+sy*sy+sz*sz);if(sl<1e-6)return;
            sx/=sl;sy/=sl;sz/=sl;double ml=Math.Sqrt(mx*mx+my*my+mz*mz),face=ml>1e-6?Math.Abs(d.x*mx+d.y*my+d.z*mz)/ml:0;a*=1-.65*face*face;
            Quads.Add(p.x-sx*w0,p.y-sy*w0,p.z-sz*w0,e.x-sx*w1,e.y-sy*w1,e.z-sz*w1,e.x+sx*w1,e.y+sy*w1,e.z+sz*w1,p.x+sx*w0,p.y+sy*w0,p.z+sz*w0,Lin(hex,16),Lin(hex,8),Lin(hex,0),Math.Min(1,a),1,true,1);
        }
        private static double H(int a,int b=0,int c=0){unchecked{uint x=(uint)a*0x9E3779B9U^(uint)b*0x85EBCA6BU^(uint)c*0xC2B2AE35U;x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x/4294967296.0;}}
        private static FactoryQ Turn(double ax,double ay,double az,double rad)=>FactoryQ.Axis(ax,ay,az,rad);

        /// <summary>
        /// Builds the frame's look for a camera at e with right r and up u: every piece by its phase, mode and beat (no
        /// effect quads past `quality` budgets; `low` drops the detail parts and the sign letters beyond 12 m). Call
        /// <see cref="Step"/> first.
        /// </summary>
        public void Build(double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,double quality,bool low)
        {
            PartCount=0;LightCount=0;ShotCount=0;Quads.Clear();ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;this.quality=quality;this.low=low;
            double now=double.IsNaN(Now)?0:Now;
            for(int i=0;i<Records.Length;i++)
            {
                var r=Records[i];part=i;o=At[i];q=Rot[i];k=NativeHazardKit.Scale(r);var p=Phases[i];
                if(Bays)Bay(r);
                switch(r.kind)
                {
                    case NativeHazardKit.Beacon:BeaconLook(r,p,now,i);break;case NativeHazardKit.Strobe:StrobeLook(r,p,now);break;
                    case NativeHazardKit.Klaxon:KlaxonLook(r,p,now);break;case NativeHazardKit.Sign:SignLook(r,p,now);break;
                    case NativeHazardKit.Turret:TurretLook(r,Turrets[i],now,i);break;case NativeHazardKit.Saw:SawLook(r,p,Saws[i],Blood[i],now,i);break;
                }
            }
        }
        private bool Near(V3 p,double range){double dx=p.x-ex,dy=p.y-ey,dz=p.z-ez;return dx*dx+dy*dy+dz*dz<range*range;}
        private int Budget(int full)=>Math.Max(1,(int)Math.Round(full*(low?.35:quality<.5?.5:1)));

        // The gallery's test bay round a piece: a wall slab behind wall pieces, a ceiling slab over ceiling pieces, a plate under floor ones.
        private void Bay(KitRecord r)
        {
            bool ceiling=r.mount=="ceiling"||r.kind==NativeHazardKit.Saw&&(r.variant==NativeHazardKit.SawDrop||r.variant==NativeHazardKit.SawPendulum||r.variant==NativeHazardKit.SawRail);
            bool wall=r.mount=="wall"||r.kind==NativeHazardKit.Saw&&r.variant==NativeHazardKit.SawWall;var keep=o;float kk=k;k=1;
            if(wall){Box(0,0,-.16,1.45,1.25,.12,0x3A4246,LPanel,Bk);Box(0,-1.21,-.12,1.45,.05,.1,0xF4B321,LStripe,0,1,false);}
            if(ceiling){Box(0,.16,0,1.45,.12,1.2,0x3A4246,LPanel,Bk);Box(0,.04,1.16,1.45,.04,.04,0xF4B321,LStripe,0,1,false);}
            if(r.kind==NativeHazardKit.Saw&&r.variant==NativeHazardKit.SawWall){o=keep+new V3(0,-1.1f,0);Box(0,-.04,.6,1.45,.04,1.0,0x2E3236,LGrate,Bk);}
            else if(!ceiling&&!wall||r.kind==NativeHazardKit.Turret&&r.mount=="floor")Box(0,-.04,0,1.45,.04,1.2,0x2E3236,LGrate,Bk);
            o=keep;k=kk;
        }

        // ---------------- alarms ----------------
        // A beacon: plate, collar, dome over a bulb, two reflectors turning about its upright axis, two light beams and the
        // light that sweeps the walls (a practical light carried along the beam). A wall-mounted one stands on a bracket arm
        // off the wall, upright; a ceiling one hangs upside down. When the beam points back at the wall it hangs on, the
        // light sits in the hot spot it makes on that wall.
        private void BeaconLook(KitRecord r,in NativeKitPhase p,double now,int i)
        {
            var pal=NativeHazardKit.Look(r.look);bool locked=NativeHazardKit.BeatLocked(r);float light=NativeHazardKitClock.BeaconLight(p,Mode);
            double angle=NativeHazardKitClock.BeaconAngle(p,now,Mode,Beat,locked,H(i)*.97);var keep=q;bool wall=r.mount=="wall";V3 normal=D(0,0,1);
            if(wall){Box(0,0,.03,.12,.14,.03,Dark,LMetal,Bk);Box(0,-.08,.17,.04,.03,.14,Steel,LMetal,Bk);Box(0,-.04,.3,.03,.06,.03,Steel,LMetal,Bk);o=W(0,-.06,.36);}
            else if(r.mount=="ceiling"){q=q*Turn(1,0,0,Math.PI);}
            // Local +y is the beacon's axis: up on a wall or floor, down from a ceiling.
            Box(0,.03,0,.16,.03,.16,Dark,LMetal,Bk);Box(0,.07,0,.12,.03,.12,Black,LPlain,0,1,false);
            // The dome glows in the lamp's colour (brighter as a reflector turns toward the eye); the reflectors inside show as
            // two dark vanes through it.
            V3 c0=W(0,.18,0);double gx=ex-c0.x,gz=ez-c0.z,gl=Math.Sqrt(gx*gx+gz*gz);V3 b0=D(Math.Sin(angle),0,Math.Cos(angle));double facing=gl>1e-6?Math.Abs(b0.x*gx+b0.z*gz)/gl:0;
            float dome=light>.01f?.25f+.8f*light+1.4f*light*(float)Math.Pow(facing,6):.04f;
            for(int s=0;s<2;s++)Box(0,.18,0,.105,.12,.105,light>.01f?pal.Lamp:pal.Off,LTube,Bk,dome,false,false,Turn(0,1,0,Math.PI/4*s));
            for(int s=0;s<2;s++){var t=Turn(0,1,0,angle+Math.PI*s);Box(Math.Sin(angle+Math.PI*s)*.03,.18,Math.Cos(angle+Math.PI*s)*.03,.07,.09,.006,Black,LPlain,0,1,false,true,t);}
            Box(0,.31,0,.075,.015,.075,Dark,LMetal,Bk);
            if(light>.01f)
            {
                V3 c=W(0,.18,0);double reach=(r.range>0?r.range:6)*.5;
                for(int s=0;s<2;s++)
                {
                    double a=angle+Math.PI*s;V3 d=D(Math.Sin(a),0,Math.Cos(a));
                    Cone(c,d,reach,.05,1.15,pal.Lamp,.22*light);Cone(c,d,reach*.8,.04,.42,pal.Lamp,.2*light);
                    // The glare when a beam sweeps across the eye.
                    double vx=ex-c.x,vy=ey-c.y,vz=ez-c.z,vl=Math.Sqrt(vx*vx+vy*vy+vz*vz);double face=vl>1e-6?(d.x*vx+d.y*vy+d.z*vz)/vl:0;if(face>0)Sprite(c,.35+1.6*Math.Pow(face,8),pal.Lamp,light*(.25+1.2*Math.Pow(face,12)));
                }
                Sprite(c,.42,pal.Lamp,.45*light);
                // The light that spills on the walls: carried out along the brighter beam, or the hot spot on its own wall.
                V3 beam=D(Math.Sin(angle),0,Math.Cos(angle));float back=wall?beam.x*normal.x+beam.y*normal.y+beam.z*normal.z:1;
                V3 spot=back>-.2f?c+beam*1.8f:c+beam*.2f+normal*.12f;Light(spot,(r.range>0?r.range:8)*.6f,pal.Lamp,(back>-.2f?1.9f:1.2f)*light);
            }
            q=keep;o=At[i];
        }
        // A strobe panel: back box, a wide lens with guard bars; the flash lights the room (a practical light) and haloes.
        private void StrobeLook(KitRecord r,in NativeKitPhase p,double now)
        {
            var pal=NativeHazardKit.Look(r.look);float f=NativeHazardKitClock.Strobe(p,now,Mode,Beat,NativeHazardKit.BeatLocked(r));bool ceiling=r.mount=="ceiling";var keep=q;
            if(ceiling)q=q*Turn(1,0,0,Math.PI/2);
            Box(0,0,.05,.32,.12,.05,Dark,LMetal,Bk);Lamp(0,0,.115,.27,.065,.02,pal.Flash,.04f+1.4f*f);
            for(int s=-1;s<=1;s+=2)Box(s*.3,0,.11,.025,.1,.025,Steel,LMetal,Bk);
            for(int s=-2;s<=2;s++)Box(s*.11,0,.14,.006,.075,.006,Black,LPlain,0,1,false,true);
            Box(0,-.085,.11,.3,.012,.03,pal.Lamp,LTube,Bk,.3f+.4f*f,false);
            if(f>.03f){V3 c=W(0,0,.17);Sprite(c,.6+.9*f,pal.Flash,.5*f);Sprite(c,1.8*f,pal.Lamp,.18*f);Light(W(0,0,.6),r.range>0?r.range:8,Mix(pal.Lamp,pal.Flash,.3f),(Mode==NativeKitFlash.Calm?.8f:1.4f)*f);}
            q=keep;
        }
        // A klaxon horn: bracket, motor body, a flared bell of three steps, grille and the red indicator; it shudders as it blasts.
        private void KlaxonLook(KitRecord r,in NativeKitPhase p,double now)
        {
            var pal=NativeHazardKit.Look(r.look);double since=BlastAge(r,p,now);float blast=since>=0&&since<.9?(float)Math.Max(0,1-since/.9):0;var keep=q;
            Box(0,0,.03,.12,.16,.03,Dark,LMetal,Bk);Box(0,-.02,.12,.03,.03,.09,Steel,LMetal,Bk);
            double shake=blast*.006*Math.Sin(now*90);o=W(shake,.03,.2);
            Box(0,0,0,.09,.09,.09,0x7A1E18,LMetal,Bk);Box(0,0,.12,.07,.07,.04,Steel,LMetal,Bk);Box(0,0,.18,.1,.1,.03,Steel,LMetal,Bk);Box(0,0,.23,.15,.15,.025,0xB8352A,LMetal,Bk);
            Box(0,0,.255,.118,.118,.004,SteelLight,LMetal,Bk);Box(0,0,.26,.085,.085,.004,Black,LGrate,Bk,1,false,true);Lamp(0,.11,-.02,.025,.02,.025,0xFF2A1A,.2f+1.6f*blast+(p.State==NativeKitState.Active?.3f:0));
            if(blast>0){V3 c=W(0,.11,-.02);Sprite(c,.25+.25*blast,0xFF2A1A,.6*blast);}
            o=At[part];q=keep;
        }
        // Seconds since the klaxon's last blast (negative: none yet), free-running or on the beat (every two beats).
        private double BlastAge(KitRecord r,in NativeKitPhase p,double now)
        {
            if(p.State!=NativeKitState.Active)return -1;bool locked=NativeHazardKit.BeatLocked(r);
            if(locked&&Beat.At(out int index,out double bt,out double bp)){int per=2;int rel=((index-Beat.FirstDownbeat)%per+per)%per;double since=Beat.Song-bt+rel*bp;return Math.Min(since,p.Since);}
            double period=r.rate>0?r.rate:1.6;return p.Since-Math.Floor(p.Since/period)*period;
        }
        // A lit warning sign: a dark case, hazard-striped rails, a backlit face and the words in light (a 5x7 pixel font).
        private void SignLook(KitRecord r,in NativeKitPhase p,double now)
        {
            var pal=NativeHazardKit.Look(r.look);string text=string.IsNullOrEmpty(r.text)?pal.Text:r.text;float lit=NativeHazardKitClock.SignLight(p,now,Mode);
            int n=text.Length;double cell=.09,width=n*cell*6/5+.2,hw=width/2;
            Box(0,0,.04,hw,.2,.04,Black,LMetal,Bk);Box(0,.205,.06,hw,.018,.03,pal.Stripe,LStripe,0,1,false);Box(0,-.205,.06,hw,.018,.03,pal.Stripe,LStripe,0,1,false);
            Box(0,0,.083,hw-.03,.17,.004,pal.Face,LTube,Bk,.15f+.25f*lit,false);
            bool letters=!low||Near(o,12);
            if(letters)
            {
                double px=cell/5,x0=-(n*cell*6/5-px)/2;
                for(int c=0;c<n;c++)
                {
                    int g=text[c]-'A';if(g<0||g>=26)continue;var rects=NativeKitFont.Rects[g];double gx=x0+c*cell*6/5;
                    for(int j=0;j<rects.Length;j+=4)
                    {
                        double ax=gx+rects[j]*px,bx=gx+(rects[j+1]+1)*px,ay=.13-rects[j+2]*px*1.3,by=.13-(rects[j+3]+1)*px*1.3;
                        // The words run toward local -x: a reader facing the sign (looking along its -z) reads them left to right.
                        Box(-(ax+bx)/2,(ay+by)/2,.09,(bx-ax)/2,(ay-by)/2,.004,pal.Ink,LTube,Bk,.05f+1.3f*lit,false,true);
                    }
                }
            }
            if(lit>.2f){Sprite(W(0,0,.25),width*1.3,pal.Ink,.12*lit);Light(W(0,0,.7),4.5f,pal.Ink,.55f*lit);}
        }

        // ---------------- the sentry turret ----------------
        // Original blocky design: a mount (floor tripod plate, ceiling drop or wall arm), a yaw ring, a yoke, a head with a
        // sensor eye and twin rotary barrel packs, heat fins and a status light. Aim from the view (degrees in the mount frame).
        private void TurretLook(KitRecord r,in NativeTurretView v,double now,int i)
        {
            bool industrial=r.look=="industrial";int shell=industrial?0xC9A227:0xD7DCDA,trim=industrial?0x2A2A2A:0x3D6E7A;var keep=q;var keepO=o;
            bool dead=v.State==NativeTurretState.Destroyed;double hp=v.Health;int body=dead?0x2A2624:shell;
            // The mount: local +y is the yaw axis afterwards.
            if(r.mount=="ceiling"){Box(0,-.03,0,.2,.03,.2,Dark,LMetal,Bk);Box(0,-.18,0,.07,.13,.07,Steel,LMetal,Bk);o=W(0,-.31,0);q=q*Turn(0,0,1,Math.PI);}
            else if(r.mount=="wall"){Box(0,0,.04,.24,.3,.04,Dark,LMetal,Bk);Box(0,-.1,.22,.07,.07,.18,Steel,LMetal,Bk);Box(0,.02,.38,.07,.13,.07,Steel,LMetal,Bk);o=W(0,.15,.38);}
            else{for(int s=0;s<3;s++){double a=s*Math.PI*2/3;Box(Math.Sin(a)*.24,.05,Math.Cos(a)*.24,.05,.05,.2,Dark,LMetal,Bk,1,true,false,Turn(0,1,0,a));}Box(0,.18,0,.11,.16,.11,Steel,LMetal,Bk);o=W(0,.34,0);}
            double yaw=v.Yaw*Math.PI/180,pitch=v.Pitch*Math.PI/180;if(r.mount=="ceiling")pitch=-pitch;
            // Damaged: the servo jitters; destroyed: slumped.
            if(hp<.5&&!dead)yaw+=Math.Sin(now*31+i)*.012*(1-hp);
            var yq=Turn(0,1,0,yaw);var rq=q;q=rq*yq;
            Box(0,.03,0,.16,.03,.16,trim,LMetal,Bk);Box(0,.1,0,.1,.05,.1,Dark,LMetal,Bk);
            for(int s=-1;s<=1;s+=2)Box(s*.2,.2,0,.03,.14,.08,body,LPanel,Bk);
            var pq=Turn(1,0,0,-pitch);var axle=W(0,.24,0);q=q*pq;o=axle;
            // The head (centred on the pitch axle, facing +z).
            Box(0,0,.02,.16,.12,.2,body,LPanel,Bk);Box(0,.125,-.02,.12,.012,.15,trim,LMetal,Bk);Box(0,-.12,.02,.14,.01,.16,Dark,LMetal,Bk);
            if(!dead||H(i,7)<.5)Box(0,0,.225,.13,.09,.012,trim,LMetal,Bk);
            // The sensor eye and the laser emitter.
            int eye=v.State==NativeTurretState.Lock||v.State==NativeTurretState.Fire?Laser:v.State==NativeTurretState.Acquire?Track:v.State==NativeTurretState.Disabled?0x3A6DFF:Scan;
            bool powered=v.State!=NativeTurretState.Offline&&v.State!=NativeTurretState.Destroyed;float eyeLevel=powered?(v.State==NativeTurretState.Disabled?.4f:1.6f):.03f;
            if(hp<.5&&powered&&H(i,(int)(now*12))<.12)eyeLevel*=.2f;
            Lamp(0,.03,.24,.035,.035,.01,eye,eyeLevel);Lamp(.08,-.05,.24,.012,.012,.01,eye,eyeLevel*.8f);
            // Heat fins on top: glowing as the barrels heat.
            float heat=Math.Max(0,Math.Min(1,v.Heat));for(int f=-2;f<=2;f++)Box(f*.045,.155,-.06,.008,.03,.1,heat>.35f?0xFF6A1A:SteelLight,heat>.35f?LTube:LMetal,Bk,heat>.35f?.2f+1.8f*heat*heat:1,false,true);
            // Status light at the back.
            Lamp(0,.07,-.205,.02,.02,.008,v.State==NativeTurretState.Disabled?0x3A6DFF:dead||!powered?0x220808:v.State==NativeTurretState.Overheat?0xFF6A1A:hp<.5?Track:Safe,powered?1.2f:.05f);
            // Twin rotary barrel packs, spun by the state (three barrels each round the pack's axis).
            double spinSince=now-v.StateAt,spin=NativeTurretPreview.SpinRate(v.State,spinSince)*spinSince+now*.0;double angle=spin+(v.State==NativeTurretState.Fire?now*34:0);
            for(int s=-1;s<=1;s+=2)
            {
                double cx=s*.105,cy=-.02;Box(cx,cy,.25,.045,.045,.05,Dark,LMetal,Bk);
                for(int b=0;b<3;b++){double a=angle*s+b*Math.PI*2/3;Box(cx+Math.Cos(a)*.025,cy+Math.Sin(a)*.025,.42,.012,.012,.17,heat>.55f?0x6A2A18:0x30343A,LMetal,Bk,1,true,true);}
                Box(cx,cy,.52,.04,.04,.012,Steel,LMetal,Bk);if(heat>.6f)Lamp(cx,cy,.6,.014,.014,.012,0xFF5A10,heat*1.4f);
            }
            V3 muzzleL=W(-.105,-.02,.6),muzzleR=W(.105,-.02,.6),fwd=D(0,0,1),up=D(0,1,0),side=D(1,0,0);V3 emitter=W(.08,-.05,.25);
            // The laser sight: cyan-white while scanning, amber acquiring, red and pulsing locked and firing; off when down.
            if(powered&&v.State!=NativeTurretState.Disabled&&v.State!=NativeTurretState.Overheat)
            {
                double reach=r.range>0?r.range:18;V3 end=float.IsNaN(v.Hit.x)?emitter+fwd*(float)reach:v.Hit;if(!float.IsNaN(v.Hit.x)&&(v.Hit-emitter).Length>reach)end=emitter+fwd*(float)reach;
                bool hot=v.State==NativeTurretState.Lock||v.State==NativeTurretState.Fire;int c=hot?Laser:v.State==NativeTurretState.Acquire?Track:Scan;double pulse=hot?.75+.25*Math.Sin(now*40):1;
                double lr=Lin(c,16),lg=Lin(c,8),lb=Lin(c,0);Line(emitter,end,hot?.022:.012,lr,lg,lb,(hot?1.1:.55)*pulse);Line(emitter,end,hot?.12:.06,lr,lg,lb,(hot?.22:.12)*pulse);
                Sprite(end,hot?.22:.13,c,(hot?1.2:.7)*pulse);Sprite(emitter,.08,c,.9);
                if(v.State==NativeTurretState.Lock){double u=Math.Min(1,(now-v.StateAt)/NativeTurretPreview.LockTime);Sprite(end,.9-.65*u,Laser,.35+.5*u);}
            }
            // Rounds: the burst's last few (the renderer draws muzzle star, tracer and impact by age).
            if(v.State==NativeTurretState.Fire||v.State==NativeTurretState.Cool||v.State==NativeTurretState.Overheat)
            {
                double rate=r.rate>0?r.rate:NativeTurretPreview.Rate;for(int s=0;s<4&&ShotCount<Shots.Length;s++)
                {
                    double at=v.LastShot-s/rate;if(!(now-at<.7)||now<at)continue;int round=v.Shots-s;V3 hit=v.Hit;
                    if(float.IsNaN(hit.x)){double spread=.03;hit=muzzleL+(fwd+side*(float)((H(i,round,1)-.5)*spread)+up*(float)((H(i,round,2)-.5)*spread))*(float)(r.range>0?r.range:14);}
                    Shots[ShotCount++]=new NativeKitShot{At=at,Muzzle=round%2==0?muzzleL:muzzleR,Forward=fwd,Up=up,Side=side,Hit=hit,Piece=i,Round=round};
                }
                if(now-v.LastShot<.05)Light(W(0,0,.7),6,0xFFC060,1.6f);
            }
            // Overheat: steam from the fin vents; damaged: sparks and smoke; destroyed: a smoke column and rare sparks.
            if(v.State==NativeTurretState.Overheat||heat>.8f){int n=Budget(10);for(int s=0;s<n;s++){double u=H(i,s,3),age=(now*.9+u)%1;V3 sp=W((u-.5)*.18,.17+age*.45,-.06-age*.1);Sprite(sp,.08+.3*age,.82,.84,.86,.35*(1-age),false);}}
            if(hp<.5){int n=Budget(dead?14:8);for(int s=0;s<n;s++){double u=H(i,s,5),age=(now*(dead?.35:.5)+u)%1;V3 sp=W((u-.5)*.25,.15+age*(dead?1.6:.8),(H(i,s,6)-.5)*.2);double g=dead?.08:.18;Sprite(sp,.12+(dead?.7:.35)*age,g,g,g*1.05,(dead?.5:.3)*(1-age),false);}
                int burst=(int)(now*3.1+i);if(H(i,burst,9)<(dead?.25:.55)){double age=now*3.1+i-burst;if(age<.4){int m=Budget(8);V3 c=W(.1,.05,.1);for(int s=0;s<m;s++){V3 d=new V3((float)(H(i,burst*16+s,1)-.5),(float)(H(i,burst*16+s,2)*.8),(float)(H(i,burst*16+s,3)-.5));V3 a=c+d*(float)(age*1.2)+new V3(0,(float)(-2.4*age*age),0);Line(a,a+d*.05f,.012,1.6,.9,.4,1-age/.4);}if(age<.08)Light(c,3.5f,0xFFC27A,1.4f);}}}
            q=keep;o=keepO;
        }

        // ---------------- saws ----------------
        private void SawLook(KitRecord r,in NativeKitPhase p,in NativeKitSawPose s,float blood,double now,int i)
        {
            bool surgical=r.look=="surgical";double radius=(r.radius>0?r.radius:.55)/k;int lampHex=p.State==NativeKitState.Active?0xFF2A1A:p.State==NativeKitState.Arming?(Frac(now*2)<.5?0xFFB020:0x2A1A08):p.State==NativeKitState.Cooldown?0xFFB020:Safe;
            float lampLevel=p.State==NativeKitState.Idle?.5f:1.4f;var keep=q;var keepO=o;V3 A=NativeHazardKit.Point(r.a,At[i],Rot[i]),B=NativeHazardKit.Point(r.b,At[i],Rot[i]);
            switch(r.variant)
            {
                case NativeHazardKit.SawFloor:
                {
                    // The slot: a steel frame flush with the floor, the dark gap, hazard paint either side, a lamp at each end.
                    double half=(B-A).Length/2+radius*.9;o=(A+B)*.5f;Box(0,-.01,0,half+.06,.012,.14,0x2E3236,LMetal,Bk,1,false);Box(0,-.005,0,half,.012,.035,Black,LPlain,0,1,false);
                    for(int e=-1;e<=1;e+=2)Box(0,0,e*.2,half+.06,.004,.06,0xF4B321,LStripe,0,1,false);for(int e=-1;e<=1;e+=2)Lamp(e*(half+.1),.015,0,.04,.015,.08,lampHex,lampLevel);
                    Scrapes(i,half,now,p);break;
                }
                case NativeHazardKit.SawWall:
                {
                    double half=(B-A).Length/2+radius*.9;o=(A+B)*.5f;Box(0,0,-.01,half+.06,.14,.012,0x2E3236,LMetal,Bk,1,false);Box(0,0,-.005,half,.035,.012,Black,LPlain,0,1,false);
                    for(int e=-1;e<=1;e+=2)Box(0,e*.2,0,half+.06,.06,.004,0xF4B321,LStripe,0,1,false);for(int e=-1;e<=1;e+=2)Lamp(e*(half+.1),0,.015,.04,.08,.015,lampHex,lampLevel);break;
                }
                case NativeHazardKit.SawDrop:
                {
                    // The ceiling housing and the telescoping arm down to the blade.
                    Box(0,-.12,0,radius+.18,.12,.16,surgical?0xD7DCDA:Dark,LMetal,Bk);Box(0,-.24,0,radius+.2,.015,.17,0xF4B321,LStripe,0,1,false);Lamp(radius+.12,-.12,.17,.03,.03,.01,lampHex,lampLevel);
                    double drop=(A-s.Hub).Length;if(drop>.25){double l=drop-.1;Box(0,-.24-l/2*.55,0,.05,l*.55/2,.05,SteelLight,LMetal,Bk);Box(0,-.24-l/2,0,.035,l/2,.035,Steel,LMetal,Bk);}
                    break;
                }
                case NativeHazardKit.SawRail:
                {
                    double half=(B-A).Length/2+.3;o=(A+B)*.5f;Box(0,.06,0,half,.06,.1,Steel,LMetal,Bk);Box(0,0,0,half,.012,.13,0xF4B321,LStripe,0,1,false);
                    for(int e=-1;e<=1;e+=2)Box(e*half,.06,0,.06,.14,.14,Dark,LMetal,Bk);
                    o=s.Hub;q=Rot[i];Box(0,radius+.25,0,.14,.1,.16,Dark,LMetal,Bk);Box(0,radius+.12,0,.03,.12,.03,Steel,LMetal,Bk);Lamp(0,radius+.36,.17,.03,.02,.01,lampHex,lampLevel);break;
                }
                default:
                {
                    // The pendulum's pivot housing and the arm to the blade.
                    Box(0,-.1,0,.22,.1,.22,surgical?0xD7DCDA:Dark,LMetal,Bk);Lamp(.23,-.1,0,.01,.03,.03,lampHex,lampLevel);
                    V3 pivot=W(0,-.2,0),hub=s.Hub;V3 arm=hub-pivot;double len=arm.Length;if(len>.05){o=(pivot+hub)*.5f;
                        q=Rot[i]*Turn(0,0,1,s.Swing);Box(0,0,0,.05,len/2,.035,Steel,LMetal,Bk);q=keep;}
                    break;
                }
            }
            q=keep;o=keepO;
            // The blade: hidden in its slot or the drop's housing while it is in (a slot shows only what stands out).
            if(s.Exposure<.02&&(r.variant==NativeHazardKit.SawFloor||r.variant==NativeHazardKit.SawWall||r.variant==NativeHazardKit.SawDrop))return;
            Blade(s,radius,blood,surgical,i);
            // Grinding: sparks fanning back from the contact, the scrape's hot streak, and blood flung off a wet blade.
            if(s.Grinding&&s.Rate>5){int n=Budget(18);for(int e=0;e<n;e++){double u=H(i,e,(int)(now*24)),age=(now*24+u)%1*.3;V3 tang=s.Along*(float)(s.Rate>0?-1:1);V3 d=(tang*(float)(.6+u)+s.Normal*(float)(.5+H(i,e,4)*.9)+Cross(s.Along,s.Normal)*(float)((H(i,e,5)-.5)*.9));
                    V3 a=s.Contact+d*(float)(age*4)+new V3(0,(float)(-4*age*age),0);double fade=1-age/.3;Line(a,a+d*.06f,.014,1.9*fade,1.0*fade,.35*fade,fade);}
                Light(s.Contact+s.Normal*.3f,3,0xFFA040,.8f+.35f*(float)H(i,0,(int)(now*30)));}
            if(blood>.2f&&s.Rate>8){int n=Budget(6);for(int e=0;e<n;e++){double u=H(i,e,77),age=(now*2+u)%1*.5;double a=u*Math.PI*2+now*3;V3 t=Cross(s.Axle,new V3((float)Math.Cos(a),(float)Math.Sin(a),0)).Normalized;
                    V3 pos=s.Hub+new V3((float)Math.Cos(a),(float)Math.Sin(a),0)*(float)radius*k+t*(float)(age*3)+new V3(0,(float)(-4.9*age*age),0);Sprite(pos,.035,.35,.01,.01,.9*(1-age/.5)*blood,false);}}
        }
        // A blade of four square plates turned 22.5 degrees apart (sixteen teeth) round a dark hub, spun about its axle; a
        // translucent blur disc while it runs; blood darkens it.
        private void Blade(in NativeKitSawPose s,double radius,float blood,bool surgical,int i)
        {
            var keep=q;var keepO=o;o=s.Hub;
            // The blade's frame: local z along the axle.
            var axle=s.Axle;var basis=Look(axle,Math.Abs(axle.y)<.9f?new V3(0,1,0):new V3(1,0,0));q=basis*Turn(0,0,1,s.Spin);
            int steel=surgical?0xDCE4E6:0xA8B0B4;int tint=Mix(steel,0x5A0806,blood*.75f);double half=radius/Math.Sqrt(2)/k*k;
            for(int e=0;e<4;e++)Box(0,0,0,half,half,.008,tint,LMetal,Bk,1,true,false,Turn(0,0,1,e*Math.PI/8));
            Box(0,0,0,radius*.32,radius*.32,.02,Dark,LMetal,Bk,1,true,false,Turn(0,0,1,Math.PI/4));Box(0,0,0,radius*.16,radius*.16,.03,Steel,LMetal,Bk);
            q=keep;o=keepO;
            if(s.Rate>12)
            {
                // Motion blur: two crossed translucent squares in the blade's plane, faint.
                V3 a=Perp(axle),b=Cross(axle,a);double rr=radius*k*.98;double al=.16*Math.Min(1,s.Rate/30);
                for(int e=0;e<2;e++){V3 p0=s.Hub+(a*(float)-rr)+(b*(float)-rr),p1=s.Hub+(a*(float)rr)+(b*(float)-rr),p2=s.Hub+(a*(float)rr)+(b*(float)rr),p3=s.Hub+(a*(float)-rr)+(b*(float)rr);
                    if(e==1){var t=a;a=(a+b).Normalized;b=Cross(axle,a);p0=s.Hub+(a*(float)-rr)+(b*(float)-rr);p1=s.Hub+(a*(float)rr)+(b*(float)-rr);p2=s.Hub+(a*(float)rr)+(b*(float)rr);p3=s.Hub+(a*(float)-rr)+(b*(float)rr);a=t;}
                    int tone=Mix(0xB8C0C4,0x6A0A08,blood*.6f);double c=Lin(tone,16),g=Lin(tone,8),bl=Lin(tone,0);
                    Quads.Add(p0.x,p0.y,p0.z,p1.x,p1.y,p1.z,p2.x,p2.y,p2.z,p3.x,p3.y,p3.z,c,g,bl,al,1,false,1);}
            }
            if(s.Rate>4)
            {
                // The running rim's glint: a faint additive ring at the teeth so a spinning blade reads in a dark room (and
                // edge-on), steel-blue clean, red once bloodied, growing with the spin.
                double glow=.5*Math.Min(1,s.Rate/NativeKitSaw.SpinRate);int n=low?8:16;V3 a=Perp(axle),b=Cross(axle,a);double rr=radius*k*.96;
                int tone=Mix(surgical?0xCFE6F0:0xB9C8D2,0xC0160E,blood*.7f);double cr=Lin(tone,16)*glow,cg=Lin(tone,8)*glow,cb=Lin(tone,0)*glow;
                for(int e=0;e<n;e++){double t0=e*Math.PI*2/n,t1=(e+1)*Math.PI*2/n;V3 p0=s.Hub+a*(float)(Math.Cos(t0)*rr)+b*(float)(Math.Sin(t0)*rr),p1=s.Hub+a*(float)(Math.Cos(t1)*rr)+b*(float)(Math.Sin(t1)*rr);Line(p0,p1,.022,cr,cg,cb,.9,true,1);}
            }
        }
        // Wear along a floor slot: dark scuffs both sides (the blade's years of work) and, behind a running blade, a cooling
        // orange streak on the slot lip.
        private void Scrapes(int i,double half,double now,in NativeKitPhase p)
        {
            int n=low?4:10;for(int e=0;e<n;e++){double u=H(i,e,11),x=(u*2-1)*half,len=.12+H(i,e,12)*.35,side=H(i,e,13)<.5?-1:1;V3 a=W(x-len/2,.004,side*(.06+H(i,e,14)*.05)),b=W(x+len/2,.004,side*(.06+H(i,e,14)*.05));FlatLine(a,b,.02+.03*H(i,e,15),.03,.028,.026,.55);}
            if(p.State==NativeKitState.Active||p.State==NativeKitState.Cooldown){var s=Saws[i];V3 c=s.Contact;V3 back=s.Along*(float)-.5;double heat=p.State==NativeKitState.Active?1:Math.Max(0,1-p.Since);FlatLine(c+back,c,.03,1.4*heat,.45*heat,.1*heat,heat,true);}
        }
        // A ribbon lying on the surface (its width along the slot's sideways), premultiplied or additive.
        private void FlatLine(V3 a,V3 b,double width,double r,double g,double bl,double alpha,bool additive=false)
        {
            V3 d=b-a;V3 up=D(0,1,0);V3 s=Cross(d,up);double l=s.Length;if(l<1e-6)return;s=s*(float)(width/2/l);
            Quads.Add(a.x-s.x,a.y-s.y,a.z-s.z,b.x-s.x,b.y-s.y,b.z-s.z,b.x+s.x,b.y+s.y,b.z+s.z,a.x+s.x,a.y+s.y,a.z+s.z,r,g,bl,Math.Min(1,alpha),0,additive,1);
        }
        private static double Frac(double v)=>v-Math.Floor(v);
        private static V3 Cross(V3 a,V3 b)=>new V3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        private static V3 Perp(V3 n){var h=Math.Abs(n.y)<.9f?new V3(0,1,0):new V3(1,0,0);return Cross(h,n).Normalized;}
        private static int Mix(int a,int b,float t){t=t<0?0:t>1?1:t;int Ch(int s)=>(int)Math.Round(((a>>s)&255)*(1-t)+((b>>s)&255)*t);return Ch(16)<<16|Ch(8)<<8|Ch(0);}
        /// <summary>A rotation whose local +z is `forward` and local +y lies toward `up`.</summary>
        public static FactoryQ Look(V3 forward,V3 up)
        {
            var f=forward.Normalized;var r=Cross(up,f).Normalized;var u=Cross(f,r);
            double m00=r.x,m01=u.x,m02=f.x,m10=r.y,m11=u.y,m12=f.y,m20=r.z,m21=u.z,m22=f.z,tr=m00+m11+m22;
            if(tr>0){double s=Math.Sqrt(tr+1)*2;return new FactoryQ{W=.25*s,X=(m21-m12)/s,Y=(m02-m20)/s,Z=(m10-m01)/s};}
            if(m00>m11&&m00>m22){double s=Math.Sqrt(1+m00-m11-m22)*2;return new FactoryQ{W=(m21-m12)/s,X=.25*s,Y=(m01+m10)/s,Z=(m02+m20)/s};}
            if(m11>m22){double s=Math.Sqrt(1+m11-m00-m22)*2;return new FactoryQ{W=(m02-m20)/s,X=(m01+m10)/s,Y=.25*s,Z=(m12+m21)/s};}
            {double s=Math.Sqrt(1+m22-m00-m11)*2;return new FactoryQ{W=(m10-m01)/s,X=(m02+m20)/s,Y=(m12+m21)/s,Z=.25*s};}
        }
    }

    /// <summary>A 5x7 pixel font (A-Z) as greedy rectangles per glyph: x0, x1, y0, y1 cells (y down), built once.</summary>
    public static class NativeKitFont
    {
        private static readonly byte[] Rows={
            0x0E,0x11,0x11,0x1F,0x11,0x11,0x11, 0x1E,0x11,0x11,0x1E,0x11,0x11,0x1E, 0x0E,0x11,0x10,0x10,0x10,0x11,0x0E, 0x1E,0x11,0x11,0x11,0x11,0x11,0x1E,
            0x1F,0x10,0x10,0x1E,0x10,0x10,0x1F, 0x1F,0x10,0x10,0x1E,0x10,0x10,0x10, 0x0E,0x11,0x10,0x17,0x11,0x11,0x0F, 0x11,0x11,0x11,0x1F,0x11,0x11,0x11,
            0x0E,0x04,0x04,0x04,0x04,0x04,0x0E, 0x07,0x02,0x02,0x02,0x02,0x12,0x0C, 0x11,0x12,0x14,0x18,0x14,0x12,0x11, 0x10,0x10,0x10,0x10,0x10,0x10,0x1F,
            0x11,0x1B,0x15,0x15,0x11,0x11,0x11, 0x11,0x11,0x19,0x15,0x13,0x11,0x11, 0x0E,0x11,0x11,0x11,0x11,0x11,0x0E, 0x1E,0x11,0x11,0x1E,0x10,0x10,0x10,
            0x0E,0x11,0x11,0x11,0x15,0x12,0x0D, 0x1E,0x11,0x11,0x1E,0x14,0x12,0x11, 0x0F,0x10,0x10,0x0E,0x01,0x01,0x1E, 0x1F,0x04,0x04,0x04,0x04,0x04,0x04,
            0x11,0x11,0x11,0x11,0x11,0x11,0x0E, 0x11,0x11,0x11,0x11,0x11,0x0A,0x04, 0x11,0x11,0x11,0x15,0x15,0x15,0x0A, 0x11,0x11,0x0A,0x04,0x0A,0x11,0x11,
            0x11,0x11,0x11,0x0A,0x04,0x04,0x04, 0x1F,0x01,0x02,0x04,0x08,0x10,0x1F};
        public static readonly int[][] Rects=Build();
        public static bool Lit(int glyph,int x,int y)=>(Rows[glyph*7+y]>>(4-x)&1)!=0;
        private static int[][] Build()
        {
            var all=new int[26][];
            for(int g=0;g<26;g++)
            {
                var used=new bool[5,7];var list=new System.Collections.Generic.List<int>();
                for(int y=0;y<7;y++)for(int x=0;x<5;x++)
                {
                    if(!Lit(g,x,y)||used[x,y])continue;int x1=x;while(x1+1<5&&Lit(g,x1+1,y)&&!used[x1+1,y])x1++;
                    int y1=y;while(y1+1<7){bool row=true;for(int c=x;c<=x1;c++)if(!Lit(g,c,y1+1)||used[c,y1+1]){row=false;break;}if(!row)break;y1++;}
                    for(int yy=y;yy<=y1;yy++)for(int c=x;c<=x1;c++)used[c,yy]=true;list.Add(x);list.Add(x1);list.Add(y);list.Add(y1);
                }
                all[g]=list.ToArray();
            }
            return all;
        }
    }

    /// <summary>
    /// The kit's share of the eight practical lights (NativeBlockLit's slots; docs/HAZARD-KIT.md "Light"): each frame the
    /// strongest kit lights at the camera (score: intensity times the shader's fall-off at the eye, plus a share for strong
    /// lights further off) take at most <see cref="Budget"/> slots, easing in and out so nothing pops. Budget: 3 on full
    /// effects, 2 reduced, 1 on the low graphics tier. The map's own lights keep the rest (the runtime puts the kit's in the
    /// map's weakest slots at the camera). Allocation free.
    /// </summary>
    public sealed class NativeKitLightPool
    {
        public const int MaxBudget=3;public int Budget=MaxBudget;
        public readonly float[] Pos=new float[MaxBudget*4],Rgb=new float[MaxBudget*3];public int Count;
        private readonly int[] slot={-1,-1,-1};private readonly float[] weight=new float[MaxBudget];private readonly NativeKitLight[] last=new NativeKitLight[MaxBudget];
        private readonly int[] want=new int[MaxBudget];private float[] score=new float[NativeKitScene.LightCapacity];
        /// <summary>A light's worth at the eye.</summary>
        public static float Score(in NativeKitLight l,double x,double y,double z)
        {
            double dx=l.X-x,dy=l.Y-y,dz=l.Z-z,d=Math.Sqrt(dx*dx+dy*dy+dz*dz),u=d/Math.Max(.1,l.Range),near=u<1?(1-u*u)*(1-u*u):0;float lum=l.R*.3f+l.G*.55f+l.B*.15f;
            return lum*(float)(near+.3/(1+d*d/(l.Range*l.Range*.6)));
        }
        /// <summary>Picks and eases this frame's kit lights from the scene's candidates (keyed by piece; dt 0 snaps).</summary>
        public void Update(NativeKitScene scene,double x,double y,double z,float dt,float rate=6)
        {
            int n=scene?.LightCount??0;var lights=scene?.Lights;
            for(int i=0;i<n;i++)score[i]=Score(lights[i],x,y,z);
            for(int k=0;k<MaxBudget;k++){want[k]=-1;if(k>=Budget)continue;int best=-1;float bs=1e-4f;for(int i=0;i<n;i++){if(score[i]<=bs)continue;bool taken=false;for(int j=0;j<k;j++)if(want[j]>=0&&lights[want[j]].Piece==lights[i].Piece){taken=true;break;}if(!taken){bs=score[i];best=i;}}want[k]=best;}
            float step=dt<=0?1:Math.Min(1,dt*rate);
            for(int s=0;s<MaxBudget;s++)
            {
                int piece=slot[s];if(piece<0)continue;int found=-1;for(int k=0;k<MaxBudget;k++)if(want[k]>=0&&lights[want[k]].Piece==piece){found=want[k];want[k]=-1;break;}
                if(found>=0){last[s]=lights[found];weight[s]=Math.Min(1,weight[s]+step);}else{weight[s]-=step;if(weight[s]<=0){weight[s]=0;slot[s]=-1;}}
            }
            for(int k=0;k<MaxBudget;k++){int c=want[k];if(c<0)continue;for(int s=0;s<MaxBudget;s++)if(slot[s]<0){slot[s]=lights[c].Piece;last[s]=lights[c];weight[s]=dt<=0?1:step;break;}}
            Count=0;
            for(int s=0;s<MaxBudget;s++)
            {
                if(slot[s]<0||weight[s]<=0)continue;var l=last[s];float w=weight[s];int o=Count*4,c=Count*3;
                Pos[o]=l.X;Pos[o+1]=l.Y;Pos[o+2]=l.Z;Pos[o+3]=l.Range;Rgb[c]=l.R*w;Rgb[c+1]=l.G*w;Rgb[c+2]=l.B*w;Count++;
            }
        }
        public void Clear(){for(int s=0;s<MaxBudget;s++){slot[s]=-1;weight[s]=0;}Count=0;}
        /// <summary>
        /// Merges the kit's lights into eight practical slots in place (`pos` x, y, z, range; `rgb` linear): a kit light takes
        /// a free slot (range 0 or far below the floor) first, else the slot whose light is worth least at the eye, never one
        /// another kit light took this call. Returns how many slots it took.
        /// </summary>
        public static int Merge(float[] pos,float[] rgb,int slots,float[] kitPos,float[] kitRgb,int kitCount,double x,double y,double z)
        {
            Span<bool> taken=stackalloc bool[16];int took=0;
            for(int k=0;k<kitCount;k++)
            {
                int best=-1;float worst=float.MaxValue;
                for(int s=0;s<slots&&s<16;s++)
                {
                    if(taken[s])continue;bool free=pos[s*4+1]<-5000||rgb[s*3]+rgb[s*3+1]+rgb[s*3+2]<=1e-5f;
                    float v=free?-1:Score(new NativeKitLight{X=pos[s*4],Y=pos[s*4+1],Z=pos[s*4+2],Range=pos[s*4+3],R=rgb[s*3],G=rgb[s*3+1],B=rgb[s*3+2]},x,y,z);
                    if(v<worst){worst=v;best=s;}
                }
                if(best<0)break;taken[best]=true;took++;
                for(int c=0;c<4;c++)pos[best*4+c]=kitPos[k*4+c];for(int c=0;c<3;c++)rgb[best*3+c]=kitRgb[k*3+c];
            }
            return took;
        }
    }
}
