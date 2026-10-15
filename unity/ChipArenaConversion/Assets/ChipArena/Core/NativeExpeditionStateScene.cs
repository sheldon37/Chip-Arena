using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>One solid kit box of the state pieces, drawn with Resources/NativeBlockLit.shader as the dressing kits are: centre,
    /// half extents and yaw (degrees, the closure volume's), the block look and flags, an sRGB colour with an HDR gain, the light
    /// level (emissive looks), the pattern's rest point (the unmoved position, so a sliding leaf carries its pattern) and seed.</summary>
    public struct ExpeditionKitBox
    {
        public float X,Y,Z,Hx,Hy,Hz,Yaw,Gain,Level,RestX,RestY,RestZ,Seed;public int Look,Flags,Hex;
    }
    /// <summary>The camera the camera-facing quads face (position, right and up, world).</summary>
    public struct ExpeditionEye{public float X,Y,Z,Rx,Ry,Rz,Ux,Uy,Uz;}

    /// <summary>
    /// The state pieces of one frame (Claude, 2026-10-14; docs/EXPEDITION-PRESENTATION.md "Doors, gate, sanctuary, transfer and
    /// boss arrival"), built from the view, the area's layout rig, the cue memory and the presentation clock into reused buffers:
    /// <see cref="Parts"/> (solid kit boxes: NativeBlockLit) and <see cref="Quads"/> (hardlight, glow and dust: NativeSirenFx's
    /// effects batch through NativeFxDraw). Engine-free and allocation-free once warm.
    ///  - Doors (a closure of this area whose id is a door anchor of the rig): the airlock unit at the closure volume, two
    ///    leaves that meet in the doorway plane and slide into the unit's pockets, lock bolts, status lamps and signs in the
    ///    state's colour. Biocontainment (Ward Zero's portico and exit): clinical white leaves with hazard edges and a teal band.
    ///    The hardlight seal covers exactly the closure face for as long as the closure blocks (sealing, sealed, opening), so
    ///    nothing looks open while it blocks; it flashes on at the start of sealing and is gone the moment the state is open.
    ///  - The final gate: a steel portcullis that rises into the gatehouse over the opening second after its lock clamps draw
    ///    back, behind a hardlight lattice that dissolves cell by cell but keeps a fifth of its cells until the state is open.
    ///    A beacon pillar and ground rings play once when this client sees FinalUnlocked open the gate (ExpeditionStateCues).
    ///  - The sanctuary: a soft warm edge on the floor along every volume's footprint, a low shimmering curtain, rising motes and
    ///    a ripple where a pilot crosses; outside the town and victory phases only a dim cool outline.
    ///  - A boss's ground crack and roar rings (<see cref="BossCrack"/>), only where the caller has the boss's position.
    /// </summary>
    public sealed class NativeExpeditionStateScene
    {
        public const int QuadCapacity=4096,PartCapacity=640,MaxDoors=16;
        public const float CullDistance=140,SanctuaryCull=90;public const double CrackSeconds=2.6;
        public readonly NativeFxQuads Quads=new NativeFxQuads(QuadCapacity);
        public readonly List<ExpeditionKitBox> Parts=new List<ExpeditionKitBox>(PartCapacity);
        private readonly bool[] leaf=new bool[MaxDoors];private readonly float[] edges=new float[5];private ExpeditionStateRig built;
        /// <summary>Doors, gates and boss cracks drawn this frame (for checks).</summary>
        public int DoorsDrawn,GatesDrawn,CracksDrawn;

        // ---- Colours (sRGB): Ward Zero's clinical palette and the hazard colours, the HUD's magenta for the final gate ----
        public const int SteelHex=0x2C3140,SteelLightHex=0x6B7385,ClinicalHex=0xC9CFCC,HeavyHex=0x3E6B5A,VoidHex=0x07090D,
            TealHex=NativeWardZeroLooks.TealHex,PortHex=0x16252C,SafeHex=NativeWardZeroLooks.SafeHex,AmberHex=NativeWardZeroLooks.AmberHex,RedHex=NativeWardZeroLooks.EmergencyHex,
            MagentaHex=NativeCyberCityLooks.MagentaHex,WarmHex=0xFFC27A,EmberHex=NativeCyberCityLooks.WarmHex,CoolHex=0x8AA6C4,DustHex=0x2A2630,CrackHex=0x050206;
        private const int Metal=NativeCyberCityLooks.Metal,Tube=NativeCyberCityLooks.Tube,Holo=NativeCyberCityLooks.Holo,Bank=NativeCyberCityLooks.Bank;

        public void Clear(){Quads.Clear();Parts.Clear();Array.Clear(leaf,0,leaf.Length);DoorsDrawn=GatesDrawn=CracksDrawn=0;}
        /// <summary>Whether door anchor `anchor` has 3D leaves this frame (else its HUD hologram stands in).</summary>
        public bool HasLeaf(string anchor){if(built==null)return false;int i=built.Door(anchor);return i>=0&&i<MaxDoors&&leaf[i];}

        /// <summary>Builds the frame. Nothing is drawn without a view, a rig of the view's area or a closure record.</summary>
        public void Build(ExpeditionView v,ExpeditionStateRig rig,ExpeditionStateCues cues,double t,in ExpeditionEye eye,bool reduced,bool low)
        {
            Clear();built=rig;if(v==null||rig==null||v.area!=rig.Area)return;
            var list=v.closures;
            if(list!=null)for(int i=0;i<list.Length;i++)
            {
                var c=list[i];if(c?.id==null||c.area!=rig.Area)continue;
                int d=rig.Door(c.id);if(d>=0&&d<MaxDoors){if(Near(rig.Doors[d].Frame,eye,CullDistance))Door(rig.Doors[d],d,c,t,eye,reduced,low);leaf[d]=true;DoorsDrawn++;continue;}
                int g=rig.Gate(c.id);if(g<0)continue;var f=rig.Gates[g].Frame;GatesDrawn++;
                if(Near(f,eye,CullDistance*1.5f))Gate(f,g,c,t,reduced,low);
                if(cues!=null&&cues.Cue(c.id,t,out double age))Beacon(f,age,eye,reduced);
            }
            for(int s=0;s<rig.Sanctuary.Length;s++)Sanctuary(rig.Sanctuary[s],s,NativeExpeditionStates.SanctuaryLit(v),cues,t,eye,reduced,low);
            // Boss arrivals of this area with an intro (seen within LateJoin of their spawn) and a recorded spawn point: the crack
            // and roar at that point, timed from `spawned`, so it plays once and in step for everyone.
            if(cues!=null)for(int i=0;i<cues.ArrivalCount;i++)
            {
                var a=cues.ArrivalAt(i);if(!a.Intro||!a.Placed||a.Area!=rig.Area)continue;double age=t-a.Spawned;if(age<0||age>CrackSeconds)continue;
                float dx=a.X-eye.X,dz=a.Z-eye.Z;if(dx*dx+dz*dz>CullDistance*CullDistance)continue;
                BossCrack(a.X,a.Y,a.Z,age,a.Seed,eye,reduced);CracksDrawn++;
            }
        }
        private static bool Near(in ExpeditionFrame f,in ExpeditionEye e,float range){float dx=f.Cx-e.X,dz=f.Cz-e.Z;return dx*dx+dz*dz<=range*range;}

        // ---- Primitives --------------------------------------------------------------------------------------------------
        private static float Lin(int hex,int shift){double c=((hex>>shift)&255)/255.0;return (float)(c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4));}
        private void Box(in ExpeditionFrame f,float u0,float u1,float v0,float v1,float d0,float d1,int look,int flags,int hex,float gain,float level,int seed,float du=0,float dv=0)
        {
            if(Parts.Count>=PartCapacity)return;
            float uc=(u0+u1)/2,vc=(v0+v1)/2,dc=(d0+d1)/2;f.Point(uc,vc,dc,out float x,out float y,out float z);f.Point(uc-du,vc-dv,dc,out float rx,out float ry,out float rz);
            Parts.Add(new ExpeditionKitBox{X=x,Y=y,Z=z,Hx=Math.Abs(u1-u0)/2,Hy=Math.Abs(v1-v0)/2,Hz=Math.Abs(d1-d0)/2,Yaw=f.Yaw,Look=look,Flags=flags,Hex=hex,Gain=gain,Level=level,RestX=rx,RestY=ry,RestZ=rz,Seed=NativeExpeditionStates.Hash(seed,31)});
        }
        /// <summary>A quad in the closure plane at depth d (u0..u1 by v0..v1), linear colour of `hex` times `gain`.</summary>
        private void Rect(in ExpeditionFrame f,float u0,float u1,float v0,float v1,float d,int hex,float gain,float alpha,bool additive,int mode=0)
        {
            f.Point(u0,v0,d,out float ax,out float ay,out float az);f.Point(u1,v0,d,out float bx,out float by,out float bz);f.Point(u1,v1,d,out float cx,out float cy,out float cz);f.Point(u0,v1,d,out float dx,out float dy,out float dz);
            Quads.Add(ax,ay,az,bx,by,bz,cx,cy,cz,dx,dy,dz,Lin(hex,16)*gain,Lin(hex,8)*gain,Lin(hex,0)*gain,alpha,mode,additive,1);
        }
        /// <summary>A four-cornered shape in the closure plane at depth d, corners given in (u, v).</summary>
        private void Shape(in ExpeditionFrame f,float u0,float v0,float u1,float v1,float u2,float v2,float u3,float v3,float d,int hex,float gain,float alpha,bool additive)
        {
            f.Point(u0,v0,d,out float ax,out float ay,out float az);f.Point(u1,v1,d,out float bx,out float by,out float bz);f.Point(u2,v2,d,out float cx,out float cy,out float cz);f.Point(u3,v3,d,out float dx,out float dy,out float dz);
            Quads.Add(ax,ay,az,bx,by,bz,cx,cy,cz,dx,dy,dz,Lin(hex,16)*gain,Lin(hex,8)*gain,Lin(hex,0)*gain,alpha,0,additive,1);
        }
        /// <summary>A floor quad in the closure frame (u0..u1 by d0..d1) at height v.</summary>
        private void Floor(in ExpeditionFrame f,float u0,float u1,float d0,float d1,float v,int hex,float gain,float alpha,int mode=0)
        {
            f.Point(u0,v,d0,out float ax,out float ay,out float az);f.Point(u1,v,d0,out float bx,out float by,out float bz);f.Point(u1,v,d1,out float cx,out float cy,out float cz);f.Point(u0,v,d1,out float dx,out float dy,out float dz);
            Quads.Add(ax,ay,az,bx,by,bz,cx,cy,cz,dx,dy,dz,Lin(hex,16)*gain,Lin(hex,8)*gain,Lin(hex,0)*gain,alpha,mode,true,1);
        }
        /// <summary>A camera-facing soft sprite (radial) of half size `size`.</summary>
        private void Sprite(in ExpeditionEye e,float x,float y,float z,float size,int hex,float gain,float alpha,bool additive=true)
        {
            float rx=e.Rx*size,ry=e.Ry*size,rz=e.Rz*size,ux=e.Ux*size,uy=e.Uy*size,uz=e.Uz*size;
            Quads.Add(x-rx-ux,y-ry-uy,z-rz-uz,x+rx-ux,y+ry-uy,z+rz-uz,x+rx+ux,y+ry+uy,z+rz+uz,x-rx+ux,y-ry+uy,z-rz+uz,Lin(hex,16)*gain,Lin(hex,8)*gain,Lin(hex,0)*gain,alpha,1,additive,1);
        }
        /// <summary>A flat floor ring (annulus) of `segments` quads.</summary>
        private void Ring(float cx,float cy,float cz,float r0,float r1,int segments,int hex,float gain,float alpha)
        {
            if(r1<=0||alpha<=0)return;if(r0<0)r0=0;
            for(int i=0;i<segments;i++)
            {
                double a0=i*Math.PI*2/segments,a1=(i+1)*Math.PI*2/segments;float c0=(float)Math.Cos(a0),s0=(float)Math.Sin(a0),c1=(float)Math.Cos(a1),s1=(float)Math.Sin(a1);
                Quads.Add(cx+r0*c0,cy,cz+r0*s0,cx+r1*c0,cy,cz+r1*s0,cx+r1*c1,cy,cz+r1*s1,cx+r0*c1,cy,cz+r0*s1,Lin(hex,16)*gain,Lin(hex,8)*gain,Lin(hex,0)*gain,alpha,0,true,1);
            }
        }
        /// <summary>A thin floor strip from (x0, z0) to (x1, z1), `width` wide, at height y.</summary>
        private void Strip(float x0,float z0,float x1,float z1,float y,float width,int hex,float gain,float alpha,bool additive)
        {
            float dx=x1-x0,dz=z1-z0,l=(float)Math.Sqrt(dx*dx+dz*dz);if(l<1e-4f)return;float nx=-dz/l*width/2,nz=dx/l*width/2;
            Quads.Add(x0-nx,y,z0-nz,x1-nx,y,z1-nz,x1+nx,y,z1+nz,x0+nx,y,z0+nz,Lin(hex,16)*gain,Lin(hex,8)*gain,Lin(hex,0)*gain,alpha,0,additive,1);
        }

        // ---- Doors ---------------------------------------------------------------------------------------------------------
        /// <summary>The leaves' depth band, the seal's depth and the unit's front (closure-relative metres toward the party).</summary>
        public static float SealDepth(in ExpeditionFrame f)=>f.D/2+.1f;
        private void Door(ExpeditionDoorRig door,int index,ExpeditionClosureView c,double t,in ExpeditionEye eye,bool reduced,bool low)
        {
            var f=door.Frame;float W=f.W,H=f.H,D=f.D,half=W/2,P=half+.1f,X=W+.3f,dB=-D/2+.01f,dF=D/2+.07f;int s=index*64;
            var phase=NativeExpeditionStates.Phase(c);float shut=NativeExpeditionStates.Shut(c,t),open=1-shut,bolts=NativeExpeditionStates.Bolts(c,t);
            bool moving=phase==ExpeditionClosurePhase.Sealing||phase==ExpeditionClosurePhase.Opening;
            int state=phase==ExpeditionClosurePhase.Open?SafeHex:phase==ExpeditionClosurePhase.Sealed?RedHex:AmberHex;
            float blink=moving&&!reduced?(Math.Floor(t*8)%2==0?1:.3f):1;bool bio=door.Look==ExpeditionDoorLook.Biocontainment;
            // The airlock unit: pockets either side, the header, the sill, hazard bands, lamps, signs, the dark airlock behind. The
            // pockets and the header frame the doorway from outside: nothing solid stands inside the closure face (|u| < W/2,
            // 0..H), which is the opening the collider frees (the Ward Zero exit's jambs and lintel bound exactly that face).
            for(int k=-1;k<=1;k+=2)
            {
                float a=k*half,b=k*X;Box(f,Math.Min(a,b),Math.Max(a,b),0,H,dB,dF,Metal,Bank,SteelHex,1,1,s+k+1);
                a=k*half;b=k*(half+.32f);Box(f,Math.Min(a,b),Math.Max(a,b),.15f,H-.2f,dF,dF+.015f,NativeBlockLooks.Demolition,0,0xF4B321,1,1,s+4+k);
                a=k*(half+.53f);b=k*(half+.97f);Box(f,Math.Min(a,b),Math.Max(a,b),H+.12f,H+.26f,dF,dF+.03f,Tube,Bank,state,3,blink,s+7+k);
                if(!low){a=k*(half+.6f);b=k*(half+1.2f);Box(f,Math.Min(a,b),Math.Max(a,b),2.05f,2.45f,dF,dF+.012f,Holo,Bank,state,1,.55f,s+10+k);}
            }
            Box(f,-X,X,H,H+.55f,dB,dF,Metal,Bank,SteelHex,1,1,s+13);
            Box(f,-(half+.3f),half+.3f,H+.08f,H+.3f,dF,dF+.015f,NativeBlockLooks.Demolition,0,0xF4B321,1,1,s+14);
            Box(f,-(half-.14f),half-.14f,H+.02f,H+.06f,dF,dF+.03f,Tube,Bank,state,2,.9f*blink,s+15);
            Box(f,-X,X,0,.03f,dB,dF+.3f,NativeBlockLooks.Demolition,0,0xF4B321,1,1,s+16);
            Box(f,-half,half,.002f,H-.002f,dB,dB+.015f,NativeBlockLooks.Plain,0,VoidHex,1,1,s+17);
            if(!low)Box(f,-(half-.35f),half-.35f,H-.42f,H-.36f,dB+.015f,dB+.04f,Tube,Bank,TealHex,1.4f,.7f,s+18);
            // The leaves: shut they meet at the doorway's centre line; open they sit inside the pockets (u beyond W/2).
            for(int k=-1;k<=1;k+=2)
            {
                float shift=k*open*(P+.02f),lo=k<0?-P:0,hi=k<0?0:P;
                Box(f,lo+shift,hi+shift,.005f,H-.02f,-.07f,.07f,bio?NativeWardZeroLooks.Panel:NativeBlockLooks.SteelGreen,bio?Bank:0,bio?ClinicalHex:HeavyHex,1,1,s+20+k,shift);
                float e0=k<0?-.18f:0,e1=k<0?0:.18f;Box(f,e0+shift,e1+shift,.12f,H-.14f,.07f,.085f,NativeBlockLooks.Demolition,0,0xF4B321,1,1,s+23+k,shift);
                if(bio){float b0=k<0?-(half-.14f):.24f,b1=k<0?-.24f:half-.14f;Box(f,b0+shift,b1+shift,1.02f,1.1f,.07f,.08f,Tube,Bank,TealHex,1.6f,phase==ExpeditionClosurePhase.Open?.45f:.95f,s+26+k,shift);}
                if(!low){float p0=k<0?-.98f:.58f,p1=k<0?-.58f:.98f;Box(f,p0+shift,p1+shift,1.5f,2.1f,.07f,.08f,NativeBlockLooks.Plain,0,PortHex,1,1,s+29+k,shift);Box(f,p0+.04f+shift,p1-.04f+shift,2.12f,2.15f,.07f,.085f,Tube,Bank,TealHex,1.2f,.8f,s+31+k,shift);}
            }
            if(bolts>0)for(int i=0;i<3;i++){float v=.7f+1.05f*i,w=.16f*bolts;Box(f,-w,w,v-.06f,v+.06f,.07f,.11f,Metal,Bank,SteelLightHex,1,1,s+33+i);}
            // The hardlight seal: exactly the closure face, while the closure blocks.
            if(NativeExpeditionStates.Blocking(c))Seal(f,index,c,phase,t,reduced);
            else
            {
                double age=t-c.began;
                if(age>=0&&age<.5&&!reduced)for(int i=0;i<14;i++)
                {   // The seal let go: sparks drift off the face for half a second.
                    float u=(NativeExpeditionStates.Hash(s+i,1)-.5f)*W,v=NativeExpeditionStates.Hash(s+i,2)*H+(float)age*1.2f;f.Point(u,v,SealDepth(f),out float x,out float y,out float z);
                    Sprite(eye,x,y,z,.07f,SafeHex,2.5f,.85f*(1-(float)age/.5f));
                }
            }
            // The airlock's light spills out as the leaves part.
            if(open>.01f){Rect(f,-half*.9f,half*.9f,.1f,H*.95f,dB+.03f,TealHex,1.2f,.3f*open,true,1);Floor(f,-half*.8f,half*.8f,dB+.05f,1.3f,.02f,TealHex,1,.08f*open,1);}
        }
        private void Seal(in ExpeditionFrame f,int index,ExpeditionClosureView c,ExpeditionClosurePhase phase,double t,bool reduced)
        {
            float W=f.W,H=f.H,half=W/2,d=SealDepth(f);int hex=phase==ExpeditionClosurePhase.Sealed?RedHex:AmberHex;double age=t-c.began;float I;
            if(phase==ExpeditionClosurePhase.Sealing)I=1+1.6f*(float)Math.Exp(-Math.Max(0,age)*9);
            else if(phase==ExpeditionClosurePhase.Opening)I=reduced?.8f:NativeExpeditionStates.Hash((int)Math.Floor(t*14),index)>.3f?1:.55f;
            else I=reduced?1:.85f+.15f*(float)Math.Sin(t*2.4);
            Rect(f,-half,half,0,H,d,hex,1,.07f*I,false);
            float w=.05f;Rect(f,-half,half,0,w,d,hex,1.6f,.9f*I,true);Rect(f,-half,half,H-w,H,d,hex,1.6f,.9f*I,true);Rect(f,-half,-half+w,w,H-w,d,hex,1.6f,.9f*I,true);Rect(f,half-w,half,w,H-w,d,hex,1.6f,.9f*I,true);
            // Hazard hems top and bottom: slanted stripes, drifting (fast while the leaves move).
            float period=.5f,stripe=.2f,slant=.3f,drift=reduced?0:(float)((t*(phase==ExpeditionClosurePhase.Sealed?.25:1.2))%period);
            for(int band=0;band<2;band++)
            {
                float v0=band==0?.06f:H-.36f,v1=v0+.3f;
                for(float b=-half-period-slant+drift;b<half;b+=period)
                {
                    float a0=Clamp(b,half),a1=Clamp(b+stripe,half),c0=Clamp(b+slant,half),c1=Clamp(b+stripe+slant,half);if(a1-a0<.005f&&c1-c0<.005f)continue;
                    Shape(f,a0,v0,a1,v0,c1,v1,c0,v1,d,hex,1.3f,.32f*I,true);
                }
            }
            for(float v=.5f;v<H-.45f;v+=.14f)Rect(f,-half+w,half-w,v,v+.012f,d,hex,1,.06f*I,true);
            if(!reduced){float sweep=H*(1-(float)(t/1.8%1));Rect(f,-half+w,half-w,sweep-.018f,sweep+.018f,d,hex,1.4f,.5f*I,true);}
            Floor(f,-half,half,d,d+1.1f,.02f,hex,1,.1f*I);
        }
        private static float Clamp(float u,float half)=>Math.Max(-half+.05f,Math.Min(half-.05f,u));

        // ---- The final gate --------------------------------------------------------------------------------------------------
        private void Gate(in ExpeditionFrame f,int index,ExpeditionClosureView c,double t,bool reduced,bool low)
        {
            float W=f.W,H=f.H,half=W/2,rise=NativeExpeditionStates.GateRise(c,t,out float unlock),lift=rise*H,k6=H/6;int s=1000+index*128;
            var phase=NativeExpeditionStates.Phase(c);
            if(rise<.999f)
            {
                int n=Math.Max(2,(int)Math.Round(W/.75f));float pitch=W/n;
                for(int i=0;i<n;i++){float u=-half+pitch*(i+.5f);Box(f,u-.07f,u+.07f,lift,lift+H,-.1f,.1f,Metal,Bank,SteelLightHex,1,1,s+i,0,lift);}
                for(int i=0;i<4;i++){float v=(.55f+1.5f*i)*k6;Box(f,-half+.03f,half-.03f,lift+v-.09f,lift+v+.09f,-.14f,.14f,Metal,Bank,SteelHex,1,1,s+40+i,0,lift);}
                Box(f,-half+.03f,half-.03f,lift,lift+.28f,-.15f,.15f,NativeBlockLooks.Demolition,0,0xF4B321,1,1,s+45,0,lift);
                Box(f,-half+.2f,half-.2f,lift+.29f,lift+.33f,.11f,.15f,Tube,Bank,MagentaHex,2.5f,phase==ExpeditionClosurePhase.Sealed?1:.6f,s+46,0,lift);
            }
            if(unlock<.999f)for(int k=-1;k<=1;k+=2)for(int i=0;i<2;i++)
            {   // The lock clamps: out of the towers across the gate; they draw back fully inside the towers.
                float v=(i==0?1.45f:4.3f)*k6,shift=k*1.0f*unlock,a=k*(half-.95f)+shift,b=k*(half+.35f)+shift;
                Box(f,Math.Min(a,b),Math.Max(a,b),v-.16f,v+.16f,.21f,.39f,Metal,Bank,SteelLightHex,1,1,s+50+i*2+k,shift);
                Box(f,Math.Min(a,b)+.05f,Math.Max(a,b)-.05f,v-.03f,v+.03f,.39f,.405f,Tube,Bank,RedHex,2.5f,1,s+56+i*2+k,shift);
            }
            if(NativeExpeditionStates.Blocking(c))Lattice(f,index,c,phase,t,reduced,low);
        }
        private void Lattice(in ExpeditionFrame f,int index,ExpeditionClosureView c,ExpeditionClosurePhase phase,double t,bool reduced,bool low)
        {
            float W=f.W,H=f.H,half=W/2,d=f.D/2+.15f,p=NativeExpeditionStates.Progress(c,t),e=NativeExpeditionStates.Ease(p);
            float I=phase==ExpeditionClosurePhase.Sealed?(reduced?1:.85f+.15f*(float)Math.Sin(t*2.1)):1;
            Rect(f,-half,half,0,H,d,MagentaHex,1,.06f*I,false);
            float w=.06f;Rect(f,-half,half,0,w,d,MagentaHex,1.8f,.9f,true);Rect(f,-half,half,H-w,H,d,MagentaHex,1.8f,.9f,true);Rect(f,-half,-half+w,w,H-w,d,MagentaHex,1.8f,.9f,true);Rect(f,half-w,half,w,H-w,d,MagentaHex,1.8f,.9f,true);
            float s=low?2.4f:1.2f,lw=.035f;
            for(int family=0;family<2;family++)
            {
                // Family 0: u = c + v (rising); family 1: u = c - v (falling); cut into cells of s/2 height.
                float c0=family==0?-half-H:-half,c1=family==0?half:half+H;int line=0;
                for(float cc=c0+s*.5f;cc<c1;cc+=s,line++)
                {
                    int cell=0;
                    for(float v0=0;v0<H-1e-4f;v0+=s/2,cell++)
                    {
                        float v1=Math.Min(H,v0+s/2),va=v0,vb=v1;
                        if(family==0){va=Math.Max(va,-half-cc);vb=Math.Min(vb,half-cc);}else{va=Math.Max(va,cc-half);vb=Math.Min(vb,cc+half);}
                        if(vb-va<.02f)continue;
                        float h=NativeExpeditionStates.Hash(index*4096+family*2048+line,cell);bool shown;float front;
                        if(phase==ExpeditionClosurePhase.Opening){front=.78f*e;shown=h>front;}
                        else if(phase==ExpeditionClosurePhase.Sealing){front=.15f+.85f*e;shown=h<front;}
                        else{front=-1;shown=true;}
                        if(!shown)continue;
                        bool flash=!reduced&&front>=0&&Math.Abs(h-front)<.06f;
                        float ua=family==0?cc+va:cc-va,ub=family==0?cc+vb:cc-vb,px=lw*.7071f,py=lw*.7071f*(family==0?-1:1);
                        float pulse=reduced||phase!=ExpeditionClosurePhase.Sealed?1:.75f+.25f*(float)Math.Sin((va+vb)*1.2-t*3);
                        Shape(f,ua-px,va-py,ub-px,vb-py,ub+px,vb+py,ua+px,va+py,d,flash?0xFFFFFF:MagentaHex,flash?2.2f:1.5f,(flash?.95f:.45f)*pulse*I,true);
                    }
                }
            }
            Floor(f,-half,half,d,d+1.4f,.02f,MagentaHex,1,.08f*I);
        }
        /// <summary>The final gate's opening cue: a beacon pillar off the gatehouse, ground rings and a face flash.</summary>
        private void Beacon(in ExpeditionFrame f,double age,in ExpeditionEye eye,bool reduced)
        {
            float a=(float)age,fade=a<NativeExpeditionStates.CueSeconds-2.5f?1:Math.Max(0,(float)(NativeExpeditionStates.CueSeconds-a)/2.5f),grow=NativeExpeditionStates.Ease(a/.8f);if(fade<=0)return;
            f.Point(0,f.H,0,out float bx,out float by,out float bz);float top=by+70*grow,dx=eye.X-bx,dz=eye.Z-bz,l=(float)Math.Sqrt(dx*dx+dz*dz);if(l<1e-3f){dx=1;l=1;}
            float rx=dz/l,rz=-dx/l;
            if(top>by+.1f){Pillar(bx,by,bz,top,rx,rz,.45f,0,2.2f,.85f*fade);Pillar(bx,by,bz,top,rx,rz,3,1,1.4f,.45f*fade);}
            for(int k=0;k<3&&!reduced||k<1;k++)
            {
                float ak=a-k*.6f;if(ak<0||ak>1.6f)continue;float r=1.5f+16*NativeExpeditionStates.Ease(ak/1.6f);
                Ring(f.Cx,f.Cy+.03f,f.Cz,r-.35f,r,40,MagentaHex,1.6f,.5f*(1-ak/1.6f)*fade);
            }
            if(a<.6f)Rect(f,-f.W/2,f.W/2,0,f.H,f.D/2+.2f,MagentaHex,2,.5f*(1-a/.6f),true);
        }

        private void Pillar(float bx,float by,float bz,float top,float rx,float rz,float half,int mode,float gain,float alpha)
        {Quads.Add(bx-rx*half,by,bz-rz*half,bx+rx*half,by,bz+rz*half,bx+rx*half,top,bz+rz*half,bx-rx*half,top,bz-rz*half,Lin(MagentaHex,16)*gain,Lin(MagentaHex,8)*gain,Lin(MagentaHex,0)*gain,alpha,mode,true,1);}

        // ---- The sanctuary -------------------------------------------------------------------------------------------------
        private void Sanctuary(ExpeditionVolume v,int vi,bool lit,ExpeditionStateCues cues,double t,in ExpeditionEye eye,bool reduced,bool low)
        {
            float y0=v.y-v.sy/2+.025f,per=ExpeditionStateRig.Perimeter(v),g=lit?1:.35f;int core=lit?WarmHex:CoolHex,glow=lit?EmberHex:CoolHex;
            var edges=this.edges;edges[0]=0;edges[1]=v.sx;edges[2]=v.sx+v.sz;edges[3]=2*v.sx+v.sz;edges[4]=per;
            for(int e=0;e<4;e++)
            {
                ExpeditionStateRig.Along(v,edges[e],out float ax,out float az,out float nx,out float nz);ExpeditionStateRig.Along(v,edges[e+1]-1e-3f,out float bx,out float bz,out _,out _);
                // Floor glow: a bright edge fading inward in three bands.
                Band(ax,az,bx,bz,nx,nz,0,.07f,y0,core,1.6f,.75f*g);Band(ax,az,bx,bz,nx,nz,.07f,.5f,y0,glow,1.2f,.22f*g);Band(ax,az,bx,bz,nx,nz,.5f,1.7f,y0,glow,1,.07f*g);
                if(lit)Floor(ax,y0,az,.9f,glow,.12f);
                if(low)continue;
                // The curtain: a low shimmer standing on the edge, one metre segments, a slow wave along the perimeter.
                float len=edges[e+1]-edges[e];int n=Math.Max(1,(int)Math.Ceiling(len));float seg=len/n;
                for(int i=0;i<n;i++)
                {
                    float s0=edges[e]+i*seg,s1=s0+seg,mid=(s0+s1)/2;
                    ExpeditionStateRig.Along(v,s0,out float x0,out float z0,out _,out _);ExpeditionStateRig.Along(v,s1-1e-3f,out float x1,out float z1,out _,out _);
                    float ddx=(x0+x1)/2-eye.X,ddz=(z0+z1)/2-eye.Z;if(ddx*ddx+ddz*ddz>SanctuaryCull*SanctuaryCull)continue;
                    float wave=reduced||!lit?.5f:.5f+.5f*(float)Math.Sin(mid*.9-t*1.6),boost=0;
                    if(cues!=null)for(int r=0;r<cues.Ripples.Length;r++)
                    {
                        var rp=cues.Ripples[r];if(rp.Volume!=vi)continue;double ra=t-rp.At;if(ra<0||ra>.9)continue;
                        float dist=Math.Abs(mid-rp.Along);dist=Math.Min(dist,per-dist);boost+=.5f*(1-(float)ra/.9f)*Math.Max(0,1-dist/(.6f+2.6f*(float)ra));
                    }
                    float a0=g*(.05f+.08f*wave)+boost*(lit?1:.6f);
                    Curtain(x0,z0,x1,z1,y0,0,.3f,glow,a0);Curtain(x0,z0,x1,z1,y0,.3f,.65f,glow,a0*.55f);Curtain(x0,z0,x1,z1,y0,.65f,1.1f,glow,a0*.22f);
                }
            }
            if(lit&&!reduced&&!low)for(int i=0;i<28;i++)
            {   // Warm motes rising off the edge.
                float along=NativeExpeditionStates.Hash(vi*97+i,5)*per,inward=.15f+NativeExpeditionStates.Hash(i,vi+7)*.5f,cycle=(float)((t/4.8+NativeExpeditionStates.Hash(i,3))%1);
                ExpeditionStateRig.Along(v,along,out float x,out float z,out float nx,out float nz);x+=nx*inward;z+=nz*inward;
                float dx=x-eye.X,dz=z-eye.Z;if(dx*dx+dz*dz>SanctuaryCull*SanctuaryCull)continue;
                Sprite(eye,x,y0+cycle*2.4f,z,.05f+.03f*NativeExpeditionStates.Hash(i,9),WarmHex,2,.55f*(float)Math.Sin(Math.PI*cycle));
            }
            if(cues!=null)for(int r=0;r<cues.Ripples.Length;r++)
            {   // A crossing: a ripple standing at the boundary point and a ring on the floor.
                var rp=cues.Ripples[r];if(rp.Volume!=vi)continue;double ra=t-rp.At;if(ra<0||ra>.9)continue;float k=1-(float)ra/.9f;
                Sprite(eye,rp.X,rp.Y+.7f,rp.Z,.9f+1.4f*(float)ra,glow,lit?1.6f:1,.35f*k);Ring(rp.X,y0+.01f,rp.Z,.1f+1.6f*(float)ra,.25f+1.6f*(float)ra,24,core,1.4f,.3f*k*(lit?1:.6f));
            }
        }
        private void Band(float ax,float az,float bx,float bz,float nx,float nz,float o0,float o1,float y,int hex,float gain,float alpha)
        {
            Quads.Add(ax+nx*o0,y,az+nz*o0,bx+nx*o0,y,bz+nz*o0,bx+nx*o1,y,bz+nz*o1,ax+nx*o1,y,az+nz*o1,Lin(hex,16)*gain,Lin(hex,8)*gain,Lin(hex,0)*gain,alpha,0,true,1);
        }
        private void Curtain(float x0,float z0,float x1,float z1,float y,float h0,float h1,int hex,float alpha)
        {
            if(alpha<=.002f)return;Quads.Add(x0,y+h0,z0,x1,y+h0,z1,x1,y+h1,z1,x0,y+h1,z0,Lin(hex,16),Lin(hex,8),Lin(hex,0),alpha,0,true,1);
        }
        private void Floor(float x,float y,float z,float r,int hex,float alpha)
        {
            Quads.Add(x-r,y,z-r,x+r,y,z-r,x+r,y,z+r,x-r,y,z+r,Lin(hex,16),Lin(hex,8),Lin(hex,0),alpha,1,true,1);
        }

        // ---- A boss's arrival at its position (only where the caller has one) ----------------------------------------------
        /// <summary>Ground cracks, two roar rings, dust and a flash at a boss's feet, `age` seconds after its authority spawn
        /// (nothing outside 0..CrackSeconds). Deterministic per `seed` (the arrival's stable hash). Build draws it at each arrival's
        /// recorded spawn point (bossArrivals[].x/y/z).</summary>
        public void BossCrack(float x,float y,float z,double age,int seed,in ExpeditionEye eye,bool reduced)
        {
            if(!(age>=0)||age>CrackSeconds)return;float a=(float)age,grow=NativeExpeditionStates.Ease(a/.35f),fade=a<1.4f?1:(2.6f-a)/1.2f,fy=y+.045f;
            for(int i=0;i<7;i++)
            {
                double ang=(i+NativeExpeditionStates.Hash(seed,i))*Math.PI*2/7;float len=(2.8f+2*NativeExpeditionStates.Hash(seed,i+20))*grow,px=x,pz=z;
                for(int k=0;k<4;k++)
                {
                    ang+=(NativeExpeditionStates.Hash(seed*13+i,k)-.5f)*.7f;float l=len/4,nx=px+l*(float)Math.Cos(ang),nz=pz+l*(float)Math.Sin(ang),w=.4f*(1-k*.2f);
                    Strip(px,pz,nx,nz,fy,w,CrackHex,1,.7f*fade,false);Strip(px,pz,nx,nz,fy+.004f,w*.3f,k<2?EmberHex:MagentaHex,2.6f,.95f*fade,true);px=nx;pz=nz;
                }
            }
            Quads.Add(x-1.4f,fy-.002f,z-1.4f,x+1.4f,fy-.002f,z-1.4f,x+1.4f,fy-.002f,z+1.4f,x-1.4f,fy-.002f,z+1.4f,0,0,0,.75f*fade,1,false,1);
            for(int k=0;k<2;k++){float ak=a-k*.25f;if(ak<0||ak>.9f)continue;float r=.8f+9*NativeExpeditionStates.Ease(ak/.9f);Ring(x,fy+.01f,z,r-(.3f-.2f*ak/.9f),r,40,EmberHex,1.4f,.3f*(1-ak/.9f));}
            int dust=reduced?4:10;
            for(int i=0;i<dust;i++)
            {
                double ang=i*Math.PI*2/dust+NativeExpeditionStates.Hash(seed,i+40);float r=1+3*NativeExpeditionStates.Ease(a/1.2f);
                Sprite(eye,x+r*(float)Math.Cos(ang),y+.3f+.8f*a,z+r*(float)Math.Sin(ang),.6f+1.2f*a,DustHex,1,.3f*fade,false);
            }
            if(a<.4f)Sprite(eye,x,y+.6f,z,2.5f,EmberHex,2.5f,.8f*(1-a/.4f));
        }
    }
}
