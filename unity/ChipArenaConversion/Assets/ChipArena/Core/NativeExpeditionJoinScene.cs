using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The hub's join pads in 3D (Claude, 2026-10-17; docs/EXPEDITION-HUB.md "Join circle presentation"), built each frame from
    /// NativeExpeditionJoinModel into reused buffers: <see cref="Parts"/> (solid kit boxes, NativeBlockLit, lit by the town like the
    /// airlocks) and <see cref="Quads"/> (hardlight, glow and lettering, one NativeSirenFx effects batch). One look for every pad,
    /// the zone's accent on its light:
    ///  - a deploy pad that reads from across the hub: six waist-high hardlight emitter posts round the circle (just outside its
    ///    radius, clear of the door and approach lines), a low emitter plate at its centre, a soft energy pool on the floor (a filled
    ///    glow that fades out toward the edge, never a painted outline: owner 2026-10-15), a low light curtain standing exactly on the
    ///    counted radius, a floating hologram with the zone's name and threat facing the camera, and a thin sky beacon over the
    ///    rooftops (the approaches carry rubble heaps, so nothing is painted on them; the town kit's light at each pad leads there);
    ///  - charging: the curtain rises with the countdown and whitens, every post's charge bar fills, the hologram shows the seconds,
    ///    twelve segments filling and a pip per pilot counted; each counted pilot stands in a soft lock column under a chevron, so
    ///    a pilot outside it can see at a glance who will be taken;
    ///  - a deploy: the curtain flashes to full height, a beam takes each pilot the circle took, a shock ring runs out (an event,
    ///    not an outline), the posts flash; pilots still held in the hub while their area loads keep a soft hold column;
    ///  - cancelled: the curtain drops and flickers out, the bars drain; held (the authority refused the transfer): an amber
    ///    flicker over the restarted count; cleared: the posts turn green, no curtain, beacon or chevrons, the hologram says CLEARED;
    ///  - a member returning to the hub: a beam-in where their pilot appears.
    /// Low graphics: half the curtain, no motes or beacon glow, plainer posts. Reduced effects: no shimmer, flicker or
    /// bob; the countdown still fills (it is information). Engine-free and allocation-free once warm.
    /// </summary>
    public sealed class NativeExpeditionJoinScene
    {
        public const int QuadCapacity=4096,PartCapacity=384,Posts=6;
        /// <summary>Pads are drawn within CullDistance of the eye (horizontal), their hologram within TitleDistance, the sky
        /// beacon within BeaconDistance.</summary>
        public const float CullDistance=140,TitleDistance=85,BeaconDistance=230;
        /// <summary>The posts stand PostOut beyond the radius; the curtain's idle and full heights; the beacon's top; the
        /// hologram's base over the floor and its title cell (block glyphs are 3 x 5 cells).</summary>
        public const float PostOut=.3f,PostTop=1.24f,CurtainIdle=.32f,CurtainFull=2.6f,CurtainFlash=3.6f,BeaconBase=4.2f,BeaconTop=44,TitleBase=3.05f,TitleCell=.075f,LineCell=.03f;
        public List<ExpeditionKitBox> Parts{get;private set;}
        public NativeFxQuads Quads{get;private set;}
        /// <summary>This frame: pads drawn, pilots marked counted, deploy and arrival beams, glyph quads (for checks and budgets).</summary>
        public int PadsDrawn,PilotsMarked,Beams,Glyphs;
        public NativeExpeditionJoinScene(){Parts=new List<ExpeditionKitBox>(PartCapacity);Quads=new NativeFxQuads(QuadCapacity);}
        /// <summary>Builds into another scene's buffers (the runtime reuses a state scene's for its draw path).</summary>
        public void Target(List<ExpeditionKitBox> parts,NativeFxQuads quads){Parts=parts??throw new ArgumentNullException(nameof(parts));Quads=quads??throw new ArgumentNullException(nameof(quads));}
        public void Clear(){Parts.Clear();Quads.Clear();PadsDrawn=PilotsMarked=Beams=Glyphs=0;}

        // ---- colours (sRGB) ------------------------------------------------------------------------------------------------------
        public const int SteelHex=0x2C3140,SteelLightHex=0x6B7385,DarkHex=0x0B0E14,WhiteHex=0xF2FBFF,AmberHex=0xFFB000,GreenHex=0x5CFF9A,PlateHex=0x1A1F2A,HazardHex=0xF4B321;
        private const int Metal=NativeCyberCityLooks.Metal,Tube=NativeCyberCityLooks.Tube,Bank=NativeCyberCityLooks.Bank;

        private float ex,ey,ez,rx,rz,cx,cy,cz;private bool reduced,low;private double t;

        /// <summary>The frame's pads from the model. `hideRow`: the party row whose own column and chevron are not drawn (my pilot in
        /// first person: they would fill the camera); -1 none.</summary>
        public void Build(NativeExpeditionJoinModel m,double t,in ExpeditionEye eye,bool reduced,bool low,int hideRow=-1)
        {
            Clear();if(m==null||!m.InHub)return;this.t=t;this.reduced=reduced;this.low=low;ex=eye.X;ey=eye.Y;ez=eye.Z;
            float l=(float)Math.Sqrt(eye.Rx*eye.Rx+eye.Rz*eye.Rz);if(l<1e-4f){rx=1;rz=0;}else{rx=eye.Rx/l;rz=eye.Rz/l;}
            for(int i=0;i<m.RingCount;i++)
            {
                var r=m.Rings[i];float dx=r.X-ex,dz=r.Z-ez,d=(float)Math.Sqrt(dx*dx+dz*dz);
                if(d<=BeaconDistance&&r.Phase!=ExpeditionJoinPhase.Cleared)Beacon(r,d);
                if(d>CullDistance)continue;PadsDrawn++;
                Pad(r,i);Curtain(r);if(d<=TitleDistance)Title(r,m);
            }
            Pilots(m,hideRow);
        }

        // ---- primitives ------------------------------------------------------------------------------------------------------------
        private static float Lin(int hex,int shift){double c=((hex>>shift)&255)/255.0;return (float)(c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4));}
        /// <summary>sRGB hex a mixed toward b by k (0..1).</summary>
        public static int Mix(int a,int b,float k)
        {
            k=k<0?0:k>1?1:k;int r=(int)Math.Round(((a>>16)&255)+(((b>>16)&255)-((a>>16)&255))*k),g=(int)Math.Round(((a>>8)&255)+(((b>>8)&255)-((a>>8)&255))*k),bl=(int)Math.Round((a&255)+((b&255)-(a&255))*k);
            return (r<<16)|(g<<8)|bl;
        }
        private void Box(float x,float y,float z,float hx,float hy,float hz,float yaw,int look,int flags,int hex,float gain,float level,int seed)
        {
            if(Parts.Count>=PartCapacity)return;
            Parts.Add(new ExpeditionKitBox{X=x,Y=y,Z=z,Hx=hx,Hy=hy,Hz=hz,Yaw=yaw,Look=look,Flags=flags,Hex=hex,Gain=gain,Level=level,RestX=x,RestY=y,RestZ=z,Seed=NativeExpeditionStates.Hash(seed,31)});
        }
        private void Q(float ax,float ay,float az,float bx,float by,float bz,float qx,float qy,float qz,float dx,float dy,float dz,int hex,float gain,float alpha,int mode=0,bool additive=true)
        {
            if(alpha<=.002f)return;Quads.Add(ax,ay,az,bx,by,bz,qx,qy,qz,dx,dy,dz,Lin(hex,16)*gain,Lin(hex,8)*gain,Lin(hex,0)*gain,alpha,mode,additive,1);
        }
        /// <summary>A camera-facing soft sprite (radial), half sizes w and h along the camera's horizontal right and world up.</summary>
        private void Sprite(float x,float y,float z,float w,float h,int hex,float gain,float alpha)=>Q(x-rx*w,y-h,z-rz*w,x+rx*w,y-h,z+rz*w,x+rx*w,y+h,z+rz*w,x-rx*w,y+h,z-rz*w,hex,gain,alpha,1);
        /// <summary>A flat floor quad round (x, z) of half size s at height y (radial: a soft pool).</summary>
        private void Pool(float x,float y,float z,float s,int hex,float gain,float alpha)=>Q(x-s,y,z-s,x+s,y,z-s,x+s,y,z+s,x-s,y,z+s,hex,gain,alpha,1);
        /// <summary>A vertical ribbon from (x, y0, z) to (x, y1, z), w wide, turned to face the eye about the vertical.</summary>
        private void Ribbon(float x,float z,float y0,float y1,float w,int hex,float gain,float alpha,int mode)
        {
            float dx=x-ex,dz=z-ez,l=(float)Math.Sqrt(dx*dx+dz*dz);float sx,sz;if(l<1e-3f){sx=rx;sz=rz;}else{sx=dz/l;sz=-dx/l;}sx*=w*.5f;sz*=w*.5f;
            Q(x-sx,y0,z-sz,x+sx,y0,z+sz,x+sx,y1,z+sz,x-sx,y1,z-sz,hex,gain,alpha,mode);
        }
        /// <summary>A circle's wall band from y0 to y1 at radius r, `n` segments, each segment's alpha shaped by the posts.</summary>
        private void Band(float cx,float cz,float r,float y0,float y1,int n,float startAngle,int hex,float gain,float alpha,bool posts)
        {
            if(alpha<=.002f||y1-y0<.005f)return;
            for(int k=0;k<n;k++)
            {
                double a0=startAngle+k*Math.PI*2/n,a1=startAngle+(k+1)*Math.PI*2/n;float c0=(float)Math.Cos(a0),s0=(float)Math.Sin(a0),c1=(float)Math.Cos(a1),s1=(float)Math.Sin(a1);
                float w=1;if(posts){double mid=(a0+a1)/2-startAngle,phase=(mid/(Math.PI*2/Posts))%1;float near=(float)Math.Abs(phase-.5)*2;w=.5f+.5f*near*near;}
                float sh=reduced?1:.88f+.12f*(float)Math.Sin(t*3.1+k*1.7);
                Q(cx+r*c0,y0,cz+r*s0,cx+r*c1,y0,cz+r*s1,cx+r*c1,y1,cz+r*s1,cx+r*c0,y1,cz+r*s0,hex,gain,alpha*w*sh);
            }
        }
        private void Annulus(float cx,float cy,float cz,float r0,float r1,int n,int hex,float gain,float alpha)
        {
            if(alpha<=.002f||r1<=r0)return;
            for(int k=0;k<n;k++)
            {
                double a0=k*Math.PI*2/n,a1=(k+1)*Math.PI*2/n;float c0=(float)Math.Cos(a0),s0=(float)Math.Sin(a0),c1=(float)Math.Cos(a1),s1=(float)Math.Sin(a1);
                Q(cx+r0*c0,cy,cz+r0*s0,cx+r1*c0,cy,cz+r1*s0,cx+r1*c1,cy,cz+r1*s1,cx+r0*c1,cy,cz+r0*s1,hex,gain,alpha);
            }
        }

        // ---- the pad: emitter plate, posts, energy pool -----------------------------------------------------------------------
        /// <summary>Where post k stands (world x, z) and its angle: the door direction plus (k + .5) sixths of a turn, so no post
        /// stands on the door's line or the approach's.</summary>
        public static void Post(in ExpeditionJoinRing r,int k,out float x,out float z,out double angle)
        {
            angle=Math.Atan2(r.Dz,r.Dx)+(k+.5)*Math.PI*2/Posts;float rr=r.R+PostOut;x=r.X+rr*(float)Math.Cos(angle);z=r.Z+rr*(float)Math.Sin(angle);
        }
        /// <summary>The pad's light colour this frame: the zone's accent, toward white with the charge and the deploy flash, amber
        /// while cancelled or held, green when cleared.</summary>
        public static int Light(in ExpeditionJoinRing r)
        {
            switch(r.Phase)
            {
                case ExpeditionJoinPhase.Cleared:return GreenHex;
                case ExpeditionJoinPhase.Charging:return Mix(r.Zone.Accent,WhiteHex,.55f*r.Charge*r.Charge);
                case ExpeditionJoinPhase.Deploying:return Mix(r.Zone.Accent,WhiteHex,.85f*(1-NativeExpeditionJoinModel.Smooth(r.Age/.6f)));
                case ExpeditionJoinPhase.Cancelled:return Mix(r.Zone.Accent,AmberHex,.6f*(1-NativeExpeditionJoinModel.Smooth(r.Age/NativeExpeditionJoinModel.CancelSeconds)));
                case ExpeditionJoinPhase.Held:return AmberHex;
                default:return r.Hold>0?Mix(r.Zone.Accent,AmberHex,r.Hold):r.Zone.Accent;
            }
        }
        private void Pad(in ExpeditionJoinRing r,int index)
        {
            int s=index*97+11,light=Light(r);float power=r.Power,flash=r.Phase==ExpeditionJoinPhase.Deploying&&r.Age<.3f?1-r.Age/.3f:0;
            float flick=r.Phase==ExpeditionJoinPhase.Cancelled&&!reduced?(Math.Floor(r.Age*16)%2==0?1:.35f):1;if(r.Hold>0&&!reduced)flick*=Math.Floor(t*14)%2==0?1:.5f;
            float yaw=(float)(Math.Atan2(r.Dx,r.Dz)*180/Math.PI);
            // The emitter plate: two square plates turned 45 degrees (an octagonal star), the lit core, four feed lines.
            Box(r.X,r.Y+.02f,r.Z,.62f,.02f,.62f,yaw,Metal,Bank,PlateHex,1,1,s);Box(r.X,r.Y+.021f,r.Z,.62f,.02f,.62f,yaw+45,Metal,Bank,PlateHex,1,1,s+1);
            Box(r.X,r.Y+.045f,r.Z,.24f,.006f,.24f,yaw+45,Tube,Bank,light,1.2f+2.2f*power+3*flash,flick,s+2);
            if(!low)for(int k=0;k<4;k++){double a=Math.Atan2(r.Dz,r.Dx)+k*Math.PI/2;float fx=(float)Math.Cos(a),fz=(float)Math.Sin(a);
                Box(r.X+fx*.48f,r.Y+.043f,r.Z+fz*.48f,k%2==0?.17f:.022f,.004f,k%2==0?.022f:.17f,yaw,Tube,Bank,light,.8f+1.2f*power,flick,s+3+k);}
            // The posts: base, hazard collar, shaft, head, a lens on top and one facing in, the charge bar on the inner face.
            float fill=r.Phase==ExpeditionJoinPhase.Charging?r.Charge:r.Phase==ExpeditionJoinPhase.Cancelled?r.Was*(1-NativeExpeditionJoinModel.Smooth(r.Age/.5f)):r.Phase==ExpeditionJoinPhase.Deploying?1-NativeExpeditionJoinModel.Smooth(r.Age/.9f):0;
            int bar=Mix(light,WhiteHex,.35f);
            for(int k=0;k<Posts;k++)
            {
                Post(r,k,out float x,out float z,out double angle);float ix=-(float)Math.Cos(angle),iz=-(float)Math.Sin(angle);float py=(float)(Math.Atan2(ix,iz)*180/Math.PI);int ps=s+20+k*10;
                Box(x,r.Y+.045f,z,.2f,.045f,.2f,py,Metal,Bank,SteelHex,1,1,ps);
                if(!low)Box(x,r.Y+.15f,z,.11f,.06f,.11f,py,NativeBlockLooks.Demolition,0,HazardHex,1,1,ps+1);
                Box(x,r.Y+.62f,z,.06f,.42f,.06f,py,Metal,Bank,SteelLightHex,1,1,ps+2);
                Box(x,r.Y+1.13f,z,.13f,.09f,.13f,py,Metal,Bank,SteelHex,1,1,ps+3);
                Box(x,r.Y+PostTop-.006f,z,.1f,.006f,.1f,py,Tube,Bank,light,1.5f+2.5f*power+4*flash,flick,ps+4);
                Box(x+ix*.135f,r.Y+1.13f,z+iz*.135f,.08f,.05f,.006f,py,Tube,Bank,light,1.2f+2.2f*power+4*flash,flick,ps+5);
                if(!low)Box(x+ix*.064f,r.Y+.62f,z+iz*.064f,.022f,.42f,.004f,py,NativeBlockLooks.Plain,0,DarkHex,1,1,ps+6);
                if(fill>.01f){float h=.84f*fill;Box(x+ix*.068f,r.Y+.2f+h/2,z+iz*.068f,.016f,h/2,.004f,py,Tube,Bank,bar,2.4f,flick,ps+7);}
            }
            // The energy pool: a filled glow fading out toward the edge (radial), and the core's brighter heart.
            if(r.Phase==ExpeditionJoinPhase.Cleared)Pool(r.X,r.Y+.012f,r.Z,r.R*1.2f,GreenHex,1,.07f);
            else{Pool(r.X,r.Y+.012f,r.Z,r.R*1.25f,light,1,(.16f+.2f*power+.4f*flash)*flick);Pool(r.X,r.Y+.016f,r.Z,.95f,Mix(light,WhiteHex,.4f),1.2f,(.18f+.3f*power+.5f*flash)*flick);}
            // Light over each post's head (a soft halo, so the ring of posts reads at a distance).
            if(!low)for(int k=0;k<Posts;k++){Post(r,k,out float x,out float z,out _);Sprite(x,r.Y+PostTop+.02f,z,.16f+.12f*power,.16f+.12f*power,light,1.5f,(.35f+.4f*power+.5f*flash)*flick*(r.Phase==ExpeditionJoinPhase.Cleared?.4f:1));}
            // The deploy's shock ring (an event: it runs out and is gone in .55 s).
            if(r.Phase==ExpeditionJoinPhase.Deploying&&r.Age<.55f){float u=r.Age/.55f,rr=r.R+.2f+2.8f*(1-(1-u)*(1-u));Annulus(r.X,r.Y+.02f,r.Z,rr-.07f,rr+.07f,low?24:40,WhiteHex,1.6f,.75f*(1-u)*(1-u));}
        }

        // ---- the curtain: a low light wall exactly on the counted radius ---------------------------------------------------------
        /// <summary>The curtain's height and opacity this frame (0 opacity: none).</summary>
        public static void CurtainOf(in ExpeditionJoinRing r,out float height,out float alpha)
        {
            switch(r.Phase)
            {
                case ExpeditionJoinPhase.Cleared:height=0;alpha=0;return;
                case ExpeditionJoinPhase.Charging:height=CurtainIdle+(CurtainFull-CurtainIdle)*NativeExpeditionJoinModel.Smooth(r.Charge);alpha=.12f+.24f*r.Charge;return;
                case ExpeditionJoinPhase.Deploying:
                    if(r.Age<.7f){height=CurtainFlash;alpha=.9f*(1-NativeExpeditionJoinModel.Smooth(r.Age/.7f));}
                    else{float k=NativeExpeditionJoinModel.Smooth((r.Age-.7f)/(NativeExpeditionJoinModel.DeploySeconds-.7f));height=CurtainIdle;alpha=.1f*k;}
                    return;
                case ExpeditionJoinPhase.Cancelled:
                {
                    float k=NativeExpeditionJoinModel.Smooth(r.Age/.6f),was=CurtainIdle+(CurtainFull-CurtainIdle)*NativeExpeditionJoinModel.Smooth(r.Was);
                    height=was+(CurtainIdle-was)*k;alpha=(.12f+.24f*r.Was)*(1-k)+.1f*k;return;
                }
                case ExpeditionJoinPhase.Held:height=CurtainIdle+.3f;alpha=.22f;return;
                default:height=CurtainIdle;alpha=.1f;return;
            }
        }
        private void Curtain(in ExpeditionJoinRing r)
        {
            CurtainOf(r,out float h,out float a);if(a<=0)return;int light=Light(r),n=low?16:28;float start=(float)Math.Atan2(r.Dz,r.Dx);
            float flick=r.Phase==ExpeditionJoinPhase.Cancelled&&!reduced?(Math.Floor(r.Age*16)%2==0?1:.3f):1;if(r.Hold>0&&!reduced)flick*=Math.Floor(t*14)%2==0?1:.45f;a*=flick;
            float y=r.Y+.02f;
            if(low)Band(r.X,r.Z,r.R,y,y+h,n,start,light,1,a*.8f,true);
            else{Band(r.X,r.Z,r.R,y,y+h*.45f,n,start,light,1,a,true);Band(r.X,r.Z,r.R,y+h*.45f,y+h,n,start,light,1,a*.42f,true);}
            // The bright rising edge at its top, and a faint hem at the floor so the radius reads from any side.
            Band(r.X,r.Z,r.R,y+h-.035f,y+h,n,start,Mix(light,WhiteHex,.4f),1.6f,Math.Min(1,a*2.6f),false);
            Band(r.X,r.Z,r.R,y,y+.05f,n,start,light,1.3f,Math.Min(1,a*1.8f),false);
            // Motes rising inside the curtain while it charges.
            if(!low&&r.Phase==ExpeditionJoinPhase.Charging)for(int k=0;k<14;k++)
            {
                float h1=NativeExpeditionStates.Hash(k,3),h2=NativeExpeditionStates.Hash(k,4),h3=NativeExpeditionStates.Hash(k,5);double climb=reduced?h2:(h2+t*(.35+.4*h1))%1;
                double ang=start+h3*Math.PI*2+(reduced?0:t*.4);float rad=r.R*(.25f+.7f*(float)Math.Sqrt(h1));
                Sprite(r.X+rad*(float)Math.Cos(ang),y+(float)climb*h,r.Z+rad*(float)Math.Sin(ang),.035f,.035f,Mix(light,WhiteHex,.5f),1.8f,.8f*r.Charge*(1-(float)climb));
            }
        }

        // ---- the beacon and the approach ----------------------------------------------------------------------------------------
        private void Beacon(in ExpeditionJoinRing r,float d)
        {
            int light=Light(r);float flash=r.Phase==ExpeditionJoinPhase.Deploying&&r.Age<.5f?1-r.Age/.5f:0,charge=r.Phase==ExpeditionJoinPhase.Charging?r.Charge:0;
            float core=Math.Max(.05f,d*.0016f),glow=Math.Max(.5f,d*.012f),a=(.32f+.35f*charge+.6f*flash)*(r.Hold>0&&!reduced&&Math.Floor(t*14)%2==1?.5f:1);
            // A thin core fading upward in four steps, a soft glow round its lower half (none on the low tier).
            float y0=r.Y+BeaconBase,span=BeaconTop-BeaconBase;
            for(int k=0;k<4;k++){float a0=y0+span*k/4f,a1=y0+span*(k+1)/4f;Ribbon(r.X,r.Z,a0,a1,core,Mix(light,WhiteHex,.3f),1.5f,a*(1-k*.24f),0);}
            if(!low){Ribbon(r.X,r.Z,y0-1,y0+span*.55f,glow,light,1,.07f+.08f*charge+.25f*flash,1);Sprite(r.X,y0,r.Z,glow*.6f,glow*.35f,light,1.2f,.12f+.2f*flash);}
        }
        // ---- the hologram ------------------------------------------------------------------------------------------------------
        private static readonly int[][][] glyphs=BuildGlyphs();
        private static int[][][] BuildGlyphs(){var g=new int[128][][];for(int c=32;c<128;c++){var boxes=NativeExpeditionCityKit.GlyphBoxes((char)c);g[c]=boxes.ToArray();}return g;}
        /// <summary>A line of block letters in the hologram's plane: centred on (cx, cz) at height y (its base), cells `cell` m.
        /// Returns its width.</summary>
        private float Text(string s,float y,float cell,int hex,float gain,float alpha,float offset=0)
        {
            if(string.IsNullOrEmpty(s))return 0;float width=s.Length*4*cell-cell,u0=-width/2+offset;
            for(int i=0;i<s.Length;i++)
            {
                char ch=s[i];if(ch>=128)continue;var boxes=glyphs[ch>='a'&&ch<='z'?ch-32:ch];if(boxes==null)continue;float lu=u0+i*4*cell;
                for(int b=0;b<boxes.Length;b++){var g=boxes[b];float a=lu+g[0]*cell,c=a+g[2]*cell,top=y+(5-g[1])*cell,bottom=top-g[3]*cell;Plane(a,c,bottom,top,hex,gain,alpha);Glyphs++;}
            }
            return width;
        }
        public static float TextWidth(string s,float cell)=>string.IsNullOrEmpty(s)?0:s.Length*4*cell-cell;
        /// <summary>A rectangle in the hologram's plane (u along the camera's horizontal right from the pad's centre, v up).</summary>
        private void Plane(float u0,float u1,float v0,float v1,int hex,float gain,float alpha,bool additive=true,int mode=0)
        =>Q(cx+rx*u0,cy+v0,cz+rz*u0,cx+rx*u1,cy+v0,cz+rz*u1,cx+rx*u1,cy+v1,cz+rz*u1,cx+rx*u0,cy+v1,cz+rz*u0,hex,gain,alpha,mode,additive);
        private void Title(in ExpeditionJoinRing r,NativeExpeditionJoinModel m)
        {
            cx=r.X;cz=r.Z;cy=r.Y+TitleBase+(reduced?0:.04f*(float)Math.Sin(t*1.3+r.X*.1));
            int light=Light(r),title=r.Phase==ExpeditionJoinPhase.Cleared?GreenHex:r.Zone.Accent,ink=Mix(light,WhiteHex,.6f);
            float flash=r.Phase==ExpeditionJoinPhase.Deploying&&r.Age<.5f?1-r.Age/.5f:0,fade=r.Phase==ExpeditionJoinPhase.Deploying?1-.5f*NativeExpeditionJoinModel.Smooth((r.Age-.5f)/.8f):1;
            float tw=TextWidth(r.Title,TitleCell),w=Math.Max(tw,1.9f)+.36f;
            // The backing: a dark glass plate behind the letters (premultiplied, not additive) so they read over a bright sky, and
            // corner brackets in the pad's light.
            Plane(-w/2,w/2,-.1f,.98f,DarkHex,1,.42f*fade,false);
            if(!low){float bl=.18f,bt=.018f;for(int sx=-1;sx<=1;sx+=2){float x0=sx*w/2,x1=sx*(w/2-bl);Plane(Math.Min(x0,x1),Math.Max(x0,x1),.98f-bt,.98f,light,1.6f,.8f*fade);Plane(Math.Min(x0,x1),Math.Max(x0,x1),-.1f,-.1f+bt,light,1.6f,.8f*fade);
                float c0=sx*w/2,c1=c0-sx*bt;Plane(Math.Min(c0,c1),Math.Max(c0,c1),.98f-bl,.98f,light,1.6f,.8f*fade);Plane(Math.Min(c0,c1),Math.Max(c0,c1),-.1f,-.1f+bl,light,1.6f,.8f*fade);}}
            Text(r.Title,.52f,TitleCell,flash>0?Mix(title,WhiteHex,flash):title,1.7f+1.5f*flash,.95f*fade);
            switch(r.Phase)
            {
                case ExpeditionJoinPhase.Charging:
                {
                    // Twelve segments filling over the duration, a pip per pilot counted, and the seconds.
                    float bw=Math.Max(tw,1.9f)-.5f,gap=.025f,seg=(bw-gap*11)/12,u=-bw/2-.22f;int lit=(int)Math.Floor(r.Charge*12+1e-4f);
                    for(int k=0;k<12;k++){float a=u+k*(seg+gap);bool on=k<lit;Plane(a,a+seg,.33f,.39f,on?ink:light,on?2.2f:1,on?.95f:.22f);}
                    int pips=Math.Max(1,Math.Min(NativeExpeditionParty.Capacity,m.PartyCount));float pu=bw/2-.1f;
                    for(int k=0;k<pips;k++){float a=pu+k*.09f;bool on=k<r.Inside;Plane(a,a+.06f,.33f,.39f,on?WhiteHex:light,on?2.4f:1,on?.95f:.25f);}
                    float sw=Text("DEPLOYING IN",.06f,LineCell,ink,1.3f,.9f,-.18f);Text(Digit(r.Seconds),.0f,.065f,WhiteHex,2.6f,.95f,sw/2+.05f);
                    break;
                }
                case ExpeditionJoinPhase.Deploying:Text("DEPLOYING",.12f,.045f,WhiteHex,2.4f+2*flash,.95f*fade);break;
                case ExpeditionJoinPhase.Cancelled:Text("CANCELLED",.12f,.045f,AmberHex,1.8f,reduced?.9f:(Math.Floor(r.Age*10)%2==0?.95f:.5f));break;
                case ExpeditionJoinPhase.Held:Text("HOLD - RETRYING",.12f,.04f,AmberHex,1.8f,.9f);break;
                case ExpeditionJoinPhase.Cleared:Text("CLEARED",.24f,.045f,GreenHex,1.8f,.9f);Text("BOSS DOWN",.0f,LineCell,Mix(GreenHex,WhiteHex,.4f),1.1f,.7f);break;
                default:
                    Text(r.Zone.Threat,.27f,LineCell,Mix(r.Zone.Accent,WhiteHex,.25f),1.1f,.75f);
                    Text("STEP IN TO DEPLOY",.0f,LineCell,ink,1.4f,reduced?.9f:.65f+.3f*(float)(.5+.5*Math.Sin(t*2.4)));
                    break;
            }
        }
        private static readonly string[] digits={"0","1","2","3","4","5","6","7","8","9"};
        private static string Digit(int s)=>digits[Math.Max(0,Math.Min(9,s))];

        // ---- the pilots: lock columns, deploy beams, hold columns, arrivals ---------------------------------------------------------
        private void Pilots(NativeExpeditionJoinModel m,int hideRow)
        {
            for(int i=0;i<m.PartyCount;i++)
            {
                var p=m.Party[i];if(!p.Placed)continue;float dx=p.X-ex,dz=p.Z-ez;if(dx*dx+dz*dz>CullDistance*CullDistance)continue;bool hidden=i==hideRow;
                if(p.Ring>=0&&p.Ring<m.RingCount&&m.Rings[p.Ring].Phase==ExpeditionJoinPhase.Charging){var r=m.Rings[p.Ring];Lock(p,r,hidden);PilotsMarked++;continue;}
                // Taken by a deploy this client saw: the beam, then a hold column while the pilot waits in the hub for its area.
                if(p.EnRoute&&p.Hub){int ring=-1;for(int k=0;k<m.RingCount;k++)if((m.Rings[k].Taken&1<<i)!=0&&m.Rings[k].Phase==ExpeditionJoinPhase.Deploying)ring=k;
                    int light=ring>=0?Light(m.Rings[ring]):WhiteHex;if(p.Deployed<1.2f)Beam(p.X,p.Y,p.Z,p.Deployed,light,false,hidden);else Hold(p,hidden,ring>=0?m.Rings[ring].Zone.Accent:WhiteHex);continue;}
                if(p.Returned>=0&&p.Returned<NativeExpeditionJoinModel.ArriveSeconds&&p.Hub)Beam(p.X,p.Y,p.Z,p.Returned,NativeExpeditionJoinZones.HubHex,true,hidden);
            }
        }
        private void Lock(in ExpeditionPartyRow p,in ExpeditionJoinRing r,bool hidden)
        {
            int light=Light(r);float k=r.Charge,h=2.3f*(.35f+.65f*k);
            if(!hidden){Band(p.X,p.Z,.5f,p.Y+.02f,p.Y+h,low?8:12,0,light,1,.05f+.15f*k,false);Band(p.X,p.Z,.5f,p.Y+h-.03f,p.Y+h,low?8:12,0,Mix(light,WhiteHex,.5f),1.5f,.3f+.5f*k,false);}
            // The chevron over the head, pointing down at the pilot: "this one is going".
            float y=p.Y+2.42f+(reduced?0:.05f*(float)Math.Sin(t*4)),w=.16f,th=.045f;int ink=Mix(light,WhiteHex,.5f);
            for(int side=-1;side<=1;side+=2)
            {
                float ax=p.X,az=p.Z,ay=y,bx=p.X+rx*side*w,bz=p.Z+rz*side*w,by=y+.14f;
                Q(ax,ay-th,az,bx,by-th,bz,bx,by+th,bz,ax,ay+th,az,ink,2.2f,.95f);
            }
            if(hidden)return;
            Sprite(p.X,p.Y+.03f,p.Z,.55f,.08f,light,1.4f,.25f+.3f*k);
        }
        private void Beam(float x,float y,float z,float age,int light,bool arrive,bool hidden)
        {
            Beams++;float u=age/1.2f;if(u>=1)return;int hot=Mix(light,WhiteHex,.6f);
            float a=arrive?(u<.15f?u/.15f:1-NativeExpeditionJoinModel.Smooth((u-.15f)/.85f)):1-NativeExpeditionJoinModel.Smooth(u),width=arrive?.25f+.55f*(1-u):.9f*(1-u)+.15f;
            float top=y+9,bottom=arrive?y+(1-NativeExpeditionJoinModel.Smooth(Math.Min(1,u*3)))*6:y;
            if(!hidden){Ribbon(x,z,bottom,top,width,light,1.6f,.55f*a,1);Ribbon(x,z,bottom,top,.08f,hot,2.4f,.9f*a,0);}
            Pool(x,y+.02f,z,1.3f*(1-.4f*u),hot,1.8f,.8f*a);
            if(!low&&!hidden)for(int k=0;k<10;k++){float h1=NativeExpeditionStates.Hash(k,7),h2=NativeExpeditionStates.Hash(k,8);double ang=h1*Math.PI*2;float rad=.15f+.35f*h2,climb=(age*(4+3*h2)+h1*1.5f)%3f;
                Sprite(x+rad*(float)Math.Cos(ang),y+.1f+climb,z+rad*(float)Math.Sin(ang),.04f,.04f,hot,2,.85f*a);}
        }
        private void Hold(in ExpeditionPartyRow p,bool hidden,int accent)
        {
            if(hidden)return;float pulse=reduced?1:.8f+.2f*(float)Math.Sin(t*5);
            Band(p.X,p.Z,.42f,p.Y+.02f,p.Y+2.2f,low?8:12,0,accent,1.2f,.12f*pulse,false);Ribbon(p.X,p.Z,p.Y,p.Y+6,.06f,Mix(accent,WhiteHex,.5f),2,.5f*pulse,0);
        }
    }
}
