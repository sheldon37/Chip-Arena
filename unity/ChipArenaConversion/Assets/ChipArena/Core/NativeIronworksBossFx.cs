using System;
using System.Numerics;
using M=ChipArena.UnityConversion.NativeIronworksBossModel;
using L=ChipArena.UnityConversion.NativeIronworksBossLook;
using Ch=ChipArena.UnityConversion.IronworksBossCh;
using A=ChipArena.UnityConversion.NativeIronworksBossArena;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE CONVERTER's effects for one frame (doc section 9's list), in the Ironworks' roles and the game's danger conventions
    /// (docs/EXPEDITION-CITY-IRONWORKS.md readability rule 4): area telegraphs are hard-edged cream-white floor decals (the pour's
    /// wedge, the rain's circles, the stomp's ring, the slam's preview ring, the deslag's circles), lines are cyan dashes (the grab's
    /// line and ring, the charge's path and edges), fast attacks flare white on the body; the hazard kit's amber stays the hazards'
    /// own (the hall vessels' lanes are Astra's converter-tilt hazard, drawn by the hazard presentation on its schedule, never here).
    /// The realism pass (owner 2026-10-19: "rework the Ironworks and Metro boss visual effects to look more realistic (When
    /// Ironworks "pours" it looks like minecraft lava right now. I want it to flow realistically."): the telegraphs are unchanged
    /// (same quads, same times; <see cref="TelegraphHash"/> pins them), everything else is rebuilt:
    /// <list type="bullet">
    /// <item><see cref="Melt"/> (Core NativeMoltenFlow, drawn with Resources/NativeMoltenFlow.shader): the pour as one continuous
    /// tube on its ballistic arc from the lip onto the authority's pool point, necking and speeding up as it falls, a splash
    /// crown where it lands; every pool (the authority's hazards, exact centre and radius from their hit: the visible hot area
    /// is the damaging area, a hot lip on the boundary) as a smooth puddle that crusts from its edge into dark plates split by
    /// glowing fissures and bubbles while liquid, then solidifies and sinks away after its `until`; droplets and slag lumps; the
    /// spill down the nose and the pit's cracks as flowing strips.</item>
    /// <item><see cref="Vfx"/> (Core NativeBossVfx, the shared effects renderer): sparks and spatter on gravity and drag with one
    /// floor bounce, motion-stretched and cooling white to red; buoyant soft lit smoke and steam that slows, spreads and
    /// drifts, lit orange from below near the metal; soft fire puffs for the blow's column; lit debris and glassy slag shards;
    /// soft glows for halos and flashes.</item>
    /// <item>Quads (<see cref="Quads"/>, one solid and one effects batch): the telegraphs, the lamps' soft beams, the stomp's
    /// cracks and the pools' floor glow.</item>
    /// <item>Up to three practical lights (<see cref="LightPos"/>, the runtime merges them into the farthest warm slots) and heat
    /// haze sources (<see cref="Heat"/>).</item>
    /// </list>
    /// Deterministic (the same frames give the same output; the pour's splash point, the charge's start, the boss's drift and the
    /// pools that just expired are remembered from the frames before). Particle counts and mesh resolution scale with the effects
    /// quality and the low tier, never the telegraphs. Allocation free.
    /// </summary>
    public sealed class NativeIronworksBossFx
    {
        public const int Capacity=3600,VfxCapacity=3000,LightMax=3,HeatMax=16;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        /// <summary>The particles (sparks, spatter, smoke, steam, fire, debris, glows) for the shared effects renderer.</summary>
        public readonly NativeBossVfx Vfx=new NativeBossVfx(VfxCapacity);
        /// <summary>The molten metal's mesh (stream, pools, crown, droplets, lumps, strips).</summary>
        public readonly NativeMoltenFlow Melt=new NativeMoltenFlow();
        public readonly float[] LightPos=new float[LightMax*4],LightRgb=new float[LightMax*3],LightBeam=new float[LightMax];public int LightCount;
        public readonly NativeHeatSource[] Heat=new NativeHeatSource[HeatMax];public int HeatCount;
        public int Telegraphs,SparkCount,SmokeCount;
        /// <summary>FNV-1a over every telegraph quad this frame (corners, colour, mode, blend, layer; in order): the checks pin it to
        /// the telegraphs the presentation drew before the realism pass.</summary>
        public uint TelegraphHash;
        /// <summary>The cream-white telegraph decals (NativeIronworksSceneFx's: the readability rules' area decal) and the cyan line.</summary>
        public const float CreamR=NativeIronworksSceneFx.DecalR,CreamG=NativeIronworksSceneFx.DecalG,CreamB=NativeIronworksSceneFx.DecalB,CreamGain=NativeIronworksSceneFx.DecalGain,DecalY=.03f,DecalW=.14f;
        public static readonly Vector3 Cyan=Lin(NativeIronworksKinds.Cyan)*2.4f,White=Lin(NativeIronworksKinds.WhiteHot),ColdWhite=Lin(NativeIronworksKinds.ColdWhite),MoltenC=Lin(NativeIronworksKinds.Molten);
        // Smoke and steam albedos (linear) and the orange the metal lights them with from below.
        private static readonly Vector3 Soot=new Vector3(.055f,.05f,.046f),Fume=new Vector3(.11f,.085f,.066f),Steam=new Vector3(.82f,.84f,.87f),Dust=new Vector3(.2f,.18f,.155f),
            Glass=new Vector3(.16f,.16f,.2f),Slag=new Vector3(.09f,.085f,.085f),Underlit=new Vector3(.95f,.42f,.12f),Spatter=new Vector3(3.2f,1.35f,.32f);
        private static Vector3 Lin(int hex)=>WeaponPaint.Linear(hex);
        private float ex,ey,ez,rx,ry,rz,ux,uy,uz;private double quality;private bool low;private uint seed;private Vector3 drift;
        /// <summary>The rain's circles this blow (x, z, radius, landing time from the hit) and the deslag's landings: shared with the cues.</summary>
        public readonly float[] Rain=new float[4*28];public int RainCount;public readonly float[] Lumps=new float[3*3];

        private int N(int full)=>NativeBrickfallAtmosphere.Count(full,quality,low);
        private static float H(uint s,int i)=>BossMath.Hash(s,i);

        public void Build(IronworksBossRig rig,in IronworksBossView v,float t,float camX,float camY,float camZ,float rightX,float rightY,float rightZ,float upX,float upY,float upZ,float effects,bool lowTier)
        {
            Quads.Clear();Vfx.Begin(effects,lowTier);Melt.Clear(lowTier);LightCount=0;HeatCount=0;Telegraphs=SparkCount=SmokeCount=0;RainCount=0;TelegraphHash=2166136261u;
            ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;quality=effects;low=lowTier;
            if(rig==null||!v.live||!rig.Visible){expiredCount=0;return;}seed=rig.Seed^(uint)(BossAt(v.spawned)?(int)(v.spawned*16):0);
            var ch=rig.Channels;bool dead=!float.IsNaN(rig.DeathAge);drift=BossMath.Flat(rig.Follow.Velocity);
            Always(rig,v,t,ch,dead);
            if(rig.Arriving)Arrival(rig,v,rig.ArrivalAge,t);
            if(rig.ActIndex>=0)Action(rig,v,t);
            PhaseFx(rig,v,t);MomentFx(rig,v,t);if(v.hazards==null)Leftovers(rig,v,t);else AuthorityHazards(rig,v,t);
            if(dead)Death(rig,v,rig.DeathAge,t);
            HallVessels(v,t,rig);
            Lights(rig,v,t,ch,dead);
            SparkCount=Vfx.Sparks;SmokeCount=Vfx.Puffs;
        }
        private static bool BossAt(float t)=>!float.IsNaN(t);

        // ================================================================================================================
        // primitives
        // ================================================================================================================
        private void Streak(Vector3 a,Vector3 b,float w,Vector3 c,float al,int mode=1)
        {
            float dx=b.X-a.X,dy=b.Y-a.Y,dz=b.Z-a.Z,cx=(a.X+b.X)/2-ex,cy=(a.Y+b.Y)/2-ey,cz=(a.Z+b.Z)/2-ez;float px=dy*cz-dz*cy,py=dz*cx-dx*cz,pz=dx*cy-dy*cx,l=(float)Math.Sqrt(px*px+py*py+pz*pz);if(l<1e-9f)return;
            px*=w/2/l;py*=w/2/l;pz*=w/2/l;Quads.Add(a.X-px,a.Y-py,a.Z-pz,b.X-px,b.Y-py,b.Z-pz,b.X+px,b.Y+py,b.Z+pz,a.X+px,a.Y+py,a.Z+pz,c.X,c.Y,c.Z,al,mode,true,1);
        }
        // A floor quad (four corners at height y), hard edged (mode 0) or soft (1).
        private void FloorQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 col,float al,int mode=0){Quads.Add(a.X,a.Y,a.Z,b.X,b.Y,b.Z,c.X,c.Y,c.Z,d.X,d.Y,d.Z,col.X,col.Y,col.Z,al,mode,true,1);}
        // A soft square of light on the floor (radial falloff), half size h.
        private void FloorGlow(Vector3 c,float h,Vector3 col){float y=c.Y+.012f;FloorQuad(new Vector3(c.X-h,y,c.Z-h),new Vector3(c.X+h,y,c.Z-h),new Vector3(c.X+h,y,c.Z+h),new Vector3(c.X-h,y,c.Z+h),col,1,1);}
        private Vector3 Cream(float k)=>new Vector3(CreamR,CreamG,CreamB)*(CreamGain*k);
        // The telegraph hash: the quads added since `from` (FNV-1a over their floats' bits, mode, blend and layer).
        private void TeleHash(int from)
        {
            uint h=TelegraphHash;
            for(int q=from;q<Quads.Count;q++)
            {
                for(int k=0;k<12;k++)h=(h^(uint)BitConverter.SingleToInt32Bits(Quads.Corners[q*12+k]))*16777619u;
                for(int k=0;k<4;k++)h=(h^(uint)BitConverter.SingleToInt32Bits(Quads.Colors[q*4+k]))*16777619u;
                h=(h^Quads.Modes[q])*16777619u;h=(h^Quads.Additive[q])*16777619u;h=(h^Quads.Layers[q])*16777619u;
            }
            TelegraphHash=h;
        }
        /// <summary>A hard-edged cream ring on the floor (radius r, width w), dashed when `dash` (segments marching slowly).</summary>
        private void Ring(float cx,float cz,float r,float k,float t,bool dash=false,float w=DecalW,Vector3? color=null)
        {
            if(k<=0||r<=.05f)return;var col=color??Cream(k);int n=Math.Max(12,Math.Min(64,(int)(r*7)));float spin=dash?t*.4f:0;
            for(int i=0;i<n;i++)
            {
                if(dash&&(i&1)==1)continue;float a0=spin+i*BossMath.Tau/n,a1=spin+(i+1)*BossMath.Tau/n+(dash?0:.002f);
                float c0=(float)Math.Cos(a0),s0=(float)Math.Sin(a0),c1=(float)Math.Cos(a1),s1=(float)Math.Sin(a1),ri=r-w;
                int q0=Quads.Count;FloorQuad(new Vector3(cx+c0*r,DecalY,cz+s0*r),new Vector3(cx+c1*r,DecalY,cz+s1*r),new Vector3(cx+c1*ri,DecalY,cz+s1*ri),new Vector3(cx+c0*ri,DecalY,cz+s0*ri),col,1);Telegraphs++;TeleHash(q0);
            }
        }
        /// <summary>A cream wedge outline: an arc at r0 and r1 over ±half (radians) about yaw, and its two radial edges; a soft fill.</summary>
        private void Wedge(Vector3 o,float yaw,float half,float r0,float r1,float k,float t)
        {
            if(k<=0)return;var col=Cream(k);int n=18;int q0=Quads.Count;
            for(int e=0;e<2;e++)
            {
                float r=e==0?r0:r1;for(int i=0;i<n;i++)
                {
                    float a0=yaw-half+2*half*i/n,a1=yaw-half+2*half*(i+1)/n;var p0=o+BossMath.Forward(a0)*r;var p1=o+BossMath.Forward(a1)*r;var q0e=o+BossMath.Forward(a0)*(r-DecalW);var q1=o+BossMath.Forward(a1)*(r-DecalW);
                    FloorQuad(Y(p0),Y(p1),Y(q1),Y(q0e),col,1);Telegraphs++;
                }
            }
            for(int s=-1;s<=1;s+=2){float a=yaw+s*half;var dir=BossMath.Forward(a);var side=BossMath.Right(a)*(DecalW*.5f);var p0=o+dir*r0;var p1=o+dir*r1;FloorQuad(Y(p0-side),Y(p1-side),Y(p1+side),Y(p0+side),col,1);Telegraphs++;}
            // The fill: a faint soft wash inside so the whole area reads, under the edge's brightness.
            for(int i=0;i<6;i++){float a0=yaw-half+2*half*i/6,a1=yaw-half+2*half*(i+1)/6;var p0=o+BossMath.Forward(a0)*r0;var p1=o+BossMath.Forward(a1)*r0;var q1=o+BossMath.Forward(a1)*r1;var qq=o+BossMath.Forward(a0)*r1;FloorQuad(Y(p0),Y(p1),Y(q1),Y(qq),col*.12f,1);}
            TeleHash(q0);
        }
        private static Vector3 Y(Vector3 p)=>new Vector3(p.X,DecalY,p.Z);
        /// <summary>A dashed cyan line on the floor from a to b (dashes marching toward b), `w` wide.</summary>
        private void Dashes(Vector3 a,Vector3 b,float k,float t,float w=.16f,float dash=.55f)
        {
            if(k<=0)return;var d=new Vector3(b.X-a.X,0,b.Z-a.Z);float len=d.Length();if(len<.1f)return;d/=len;var side=new Vector3(-d.Z,0,d.X)*(w*.5f);var col=Cyan*k;
            float off=BossMath.Frac(t*1.4f)*dash*2;int q0=Quads.Count;
            for(float s=-off;s<len;s+=dash*2){float s0=Math.Max(0,s),s1=Math.Min(len,s+dash);if(s1<=s0)continue;var p0=a+d*s0;var p1=a+d*s1;FloorQuad(Y(p0-side),Y(p1-side),Y(p1+side),Y(p0+side),col,1);Telegraphs++;}
            TeleHash(q0);
        }
        private void AddHeat(Vector3 p,float w,float h,float s){if(HeatCount>=HeatMax||s<=.02f)return;Heat[HeatCount++]=new NativeHeatSource{X=p.X,Y=p.Y,Z=p.Z,W=w,H=h,Strength=s};}
        private void AddLight(Vector3 p,float range,Vector3 rgb,float beam=0)
        {
            if(LightCount>=LightMax||rgb.X+rgb.Y+rgb.Z<.02f)return;int k=LightCount++;LightPos[k*4]=p.X;LightPos[k*4+1]=p.Y;LightPos[k*4+2]=p.Z;LightPos[k*4+3]=range;LightRgb[k*3]=rgb.X;LightRgb[k*3+1]=rgb.Y;LightRgb[k*3+2]=rgb.Z;LightBeam[k]=beam;
        }
        // A flicker for the metal's light (0.85..1.05), a pure function of the clock.
        private static float Flicker(float t,float s)=>.95f+.06f*(float)Math.Sin(t*17.3f+s)+.04f*(float)Math.Sin(t*41.7f+s*2.1f);

        // ================================================================================================================
        // always: the mouth's glow, smoke and drips, the spill strip, the lamps' halos and beams, the visor, the seams' sparks
        // ================================================================================================================
        private void Always(IronworksBossRig rig,in IronworksBossView v,float t,float[] ch,bool dead)
        {
            var P=rig.Shown;float heat=rig.HeatLevel*(1-rig.CoolLevel);var mouth=rig.Mouth;var axis=rig.MouthAxis;
            // The mouth's halo (white at its core, warm round it) and the heat haze over it.
            if(heat>.02f){Vfx.Glow(mouth+axis*.25f,White*(.5f*heat),2.2f+.8f*heat,NativeBossVfx.Core);Vfx.Glow(mouth+axis*.6f,MoltenC*(.2f*heat),4.4f,NativeBossVfx.Mote);AddHeat(mouth+axis*1.4f,2.6f,3.4f,.55f*Math.Min(1.3f,heat));}
            // Smoke curling off the mouth: dark fumes leaving along the mouth's axis, lit orange from below as they leave, slowing,
            // rising and spreading; thicker when it is hot. Left behind as the boss walks.
            if(!dead||rig.DeathAge<3)Vfx.SmokeFlow(t,mouth+axis*.5f,axis*.9f+new Vector3(0,.9f,0),.35f,.75f,4.5f,3.6f,.8f,3.4f,Soot,.55f*Math.Min(1,heat+.2f),0,1e7f,seed,1,.22f*Math.Min(1,heat),NativeBossVfx.NoFloor,Underlit*.3f,drift);
            // Drips and slops off the lip when the metal spills: heavy glowing drops falling to the floor.
            if(rig.Spill>.05f){var lip=mouth+BossMath.Rotate(P.Rotation[M.Vessel],new Vector3(0,-.15f,.7f));Vfx.SparkFlow(t,lip,new Vector3(0,-1,0)+axis*.3f,.35f,1.4f,9,1.0f,0,1e7f,seed,2,.85f,.06f,.2f,true,0,drift,BossMath.Clamp01(rig.Spill));
                Vfx.Glow(lip,MoltenC*(.6f*rig.Spill),.7f,NativeBossVfx.Core);}
            // The spill: a molten strip down the nose (flowing down it), hotter as the metal slops.
            var vr=P.Rotation[M.Vessel];var vp=P.Position[M.Vessel];var path=L.SpillPath;var wid=L.SpillWidth;float spill=Math.Max(.35f,Math.Min(1.2f,.55f+.5f*rig.Spill))*(1-rig.CoolLevel*.9f);
            int np=Math.Min(path.Length,Melt.PathP.Length);
            for(int i=0;i<np;i++)
            {
                float u=i/(float)Math.Max(1,np-1);Melt.PathP[i]=vp+BossMath.Rotate(vr,path[i]);Melt.PathN[i]=BossMath.Rotate(vr,Vector3.Normalize(new Vector3(path[i].X,0,path[i].Z)+new Vector3(0,0,.001f)));
                Melt.PathW[i]=wid[i]*.5f;Melt.PathHeat[i]=BossMath.Clamp01(.95f*spill-.25f*u);
            }
            Melt.Ribbon(np,.45f,BossMath.Clamp01(.15f+.35f*rig.CoolLevel),.03f,.3f,1,.012f);
            // The lamps: halos and soft beams (cold white), only while lit.
            float lamp=rig.LampLevel;
            if(lamp>.02f)for(int s=0;s<2;s++)
            {
                var p=s==0?rig.LampAtL:rig.LampAtR;var dir=s==0?rig.BeamL:rig.BeamR;Vfx.Glow(p+dir*.15f,ColdWhite*(1.3f*lamp),.85f,NativeBossVfx.Core);Vfx.Glow(p+dir*.2f,ColdWhite*(.22f*lamp),2.2f,NativeBossVfx.Mote);
                // A soft beam: widening quads along it (camera facing streaks), fading out over 11 m.
                for(int k=0;k<7;k++){float d0=.4f+k*1.55f,d1=d0+1.9f;Streak(p+dir*d0,p+dir*d1,.36f+.42f*k,ColdWhite*(.062f*lamp*(1-k/7f)*(1-k/7f)),1,1);}
            }
            float visor=rig.VisorLevel;if(visor>.02f&&!rig.Hidden[L.Visor])Vfx.Glow(P.Position[L.Visor],Cyan*(.5f*visor),.32f,NativeBossVfx.Core);
            // Phase 3: sparks falling from the seams, bouncing off the floor.
            if(rig.SeamLevel>.05f){for(int k=0;k<3;k++){var sp=vp+BossMath.Rotate(vr,new Vector3((float)Math.Sin(k*2.1f+1)*1.9f,.2f+k*.6f,(float)Math.Cos(k*2.1f+1)*1.9f));Vfx.SparkFlow(t,sp,new Vector3(0,-.2f,0),.6f,1.1f,10,1.2f,0,1e7f,seed,20+k,.9f*rig.SeamLevel,.024f,.8f,true,0,drift);}
                AddHeat(vp+new Vector3(0,1.2f,0),4.2f,4.6f,.35f*rig.SeamLevel);}
            // The tuyère grate's glow when it blows (white, never orange), its steam jets at the trunnion seals.
            if(rig.BlowLevel>.3f){Vfx.Glow(rig.GrateAt+rig.GrateAxis*.15f,White*(.6f*rig.BlowLevel),1.8f*rig.BlowLevel,NativeBossVfx.Core);}
            float steam=ch[Ch.Steam];if(steam>.05f)for(int s=-1;s<=1;s+=2){var seal=vp+BossMath.Rotate(vr,new Vector3(s*2.2f,0,0));
                Vfx.SmokeFlow(t,seal,BossMath.Rotate(vr,new Vector3(s*4.2f,.5f,0)),.35f,.6f,14,1.1f,.25f,1.7f,Steam,.5f*steam,0,1e7f,seed,4+(s+1),0,NativeBossVfx.NoFloor,default,drift);}
        }

        // ================================================================================================================
        // the arrival: the pit's glow, the crust splitting in a cross on the first hit, steam rings, the emerging body's slag
        // sheeting off, the lamps clicking on, the first blow's column on the drop (the action's column, without the rain)
        // ================================================================================================================
        private void Arrival(IronworksBossRig rig,in IronworksBossView v,float a,float t)
        {
            var b=NativeIronworksBossBeats.Current;var pit=new Vector3(A.PitX,0,A.PitZ);
            // The pit glows from below as the song starts, brightening to the hit.
            float glow=BossMath.Smooth(a/b.Hit);AddLight(pit+new Vector3(0,.8f,0),12,new Vector3(3.2f,1.6f,.6f)*(.45f+.95f*glow*(a<b.Climb?1:Math.Max(0,1-(a-b.Climb)/2))),0);
            // The cross of cracks: molten slag opening from the centre along the pit's axes on the hit, widening as it heaves, crusting
            // over once it has climbed out; each arm a jagged running strip.
            float open=BossMath.Smoother((a-(b.Hit-.04f))/.32f),wide=BossMath.Smoother((a-b.Hit)/Math.Max(.3f,b.Climb-b.Hit)),crust=BossMath.Smooth((a-b.Climb)/2.5f);
            if(open>0)for(int arm=0;arm<4;arm++)
            {
                var dir=arm==0?Vector3.UnitX:arm==1?-Vector3.UnitX:arm==2?Vector3.UnitZ:-Vector3.UnitZ;var side=new Vector3(-dir.Z,0,dir.X);float len=2.95f*open,w=.1f+.38f*wide;int n=low?7:12;
                for(int i=0;i<n;i++)
                {
                    float u=i/(float)(n-1);float jag=(H(seed,arm*53+i)-.5f)*(.22f+.3f*wide)*(i==0?0:1);
                    Melt.PathP[i]=pit+dir*(len*u)+side*jag+new Vector3(0,A.PitY+.006f,0);Melt.PathN[i]=Vector3.UnitY;Melt.PathW[i]=w*(1-.7f*u*u)*(.75f+.5f*H(seed,arm*71+i));Melt.PathHeat[i]=BossMath.Clamp01(.88f-.3f*crust-.12f*u);
                }
                Melt.Ribbon(n,.6f,BossMath.Clamp01(.25f+.5f*crust),.02f,arm*.27f,1,.004f);
            }
            if(open>0&&a<b.Hit+.6f){Vfx.Glow(pit+new Vector3(0,.4f,0),White*(1.1f*(1-BossMath.Smooth((a-b.Hit)/.6f))),9*open,NativeBossVfx.Mote);}
            // Steam rings at the rim on the hit, a fountain of sparks, a gush of heat.
            if(a>b.Hit-.05f&&a<b.Hit+3.2f)
            {
                int ring=N(18);for(int i=0;i<ring;i++){float ang=i*BossMath.Tau/ring,rr=3.1f;var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));
                    Vfx.SmokeBurst(a,pit+d*rr+new Vector3(0,.15f,0),d*1.6f+new Vector3(0,1.9f,0),.4f,.55f,2,2.6f,.7f,2.6f,Steam,.5f,b.Hit,.15f,seed,50+i);}
                Vfx.SparkBurst(a,pit+new Vector3(0,.1f,0),Vector3.UnitY,.7f,7.5f,90,1.4f,b.Hit,.35f,seed,60,1,.03f,.6f);
                AddHeat(pit+new Vector3(0,1.6f,0),6,3.4f,.8f*(1-BossMath.Smooth((a-b.Hit)/2.5f)));
            }
            // Slag sheeting off the emerging body: molten drips running off the shell and dark fumes as it heaves and climbs.
            if(a>b.Heave-.2f&&a<b.Flare+1.2f){var vp=rig.Shown.Position[M.Vessel];for(int k=0;k<4;k++){float ang=k*1.57f+.8f;Vfx.SparkFlow(a,vp+new Vector3((float)Math.Cos(ang)*1.9f,.6f,(float)Math.Sin(ang)*1.9f),-Vector3.UnitY,.25f,1.0f,10,1.1f,b.Heave+k*.3f,b.Flare,seed,70+k,.85f,.055f,.2f);}
                Vfx.SmokeBurst(a,vp+new Vector3(0,-.8f,0),new Vector3(0,1.3f,0),.8f,.6f,14,3.0f,1.2f,3.6f,Soot,.5f,b.Heave,b.Flare-b.Heave,seed,80,.5f,NativeBossVfx.NoFloor,Underlit*.6f);}
            // The first blow on the drop: its inhale and its column (the column from the action's builder, from the drop).
            if(a>b.Drop-.75f)Column(rig,a-b.Drop,a,t,false,v);
        }

        // ================================================================================================================
        // the actions
        // ================================================================================================================
        private void Action(IronworksBossRig rig,in IronworksBossView v,float t)
        {
            var act=NativeIronworksBoss.Actions[rig.ActIndex];var s=rig.Act;float age=s.Age,hit=s.HitAt,windup=act.Windup;
            var root=rig.Root;float yaw=BossAt(v.actionYaw)?v.actionYaw*BossMath.Deg:rig.Yaw;var target=BossAt(v.targetX)?new Vector3(v.targetX,0,v.targetZ):root+BossMath.Forward(yaw)*6;
            // Telegraph level: building over the windup (a pulse), steady through the strike, gone after.
            float tk=age<hit?(.55f+.45f*BossMath.Smooth(age/Math.Max(.05f,hit)))*(.8f+.2f*(float)Math.Max(0,Math.Sin(age*Math.PI*4))):age<hit+act.Strike?.8f:0;
            switch(act.Id)
            {
                case "pour":Pour(rig,v,age,hit,act,yaw,target,tk,t);break;
                case "blow":Blow(rig,v,age,hit,act,tk,t);break;
                case "stomp":{var land=BossAt(v.targetX)?target:rig.Foot(Math.Max(0,Math.Min(3,v.variant)));if(age<hit+act.Strike)Ring(land.X,land.Z,1.9f,tk,t);
                    if(age<hit){int leg=Math.Max(0,Math.Min(3,v.variant));Vfx.Glow(rig.Foot(leg),White*.55f*BossMath.Smooth(age/hit),1.1f,NativeBossVfx.Core);}
                    float k=age-hit;if(k>=0&&k<2.4f){Impact(land,age,hit,1,100);
                        for(int i=0;i<N(8);i++){float ang=i*BossMath.Tau/8+.3f;var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));float l=1.4f+.8f*H(seed,i+5);FloorQuad(Y(land+d*.4f-new Vector3(-d.Z,0,d.X)*.04f),Y(land+d*l),Y(land+d*l),Y(land+d*.4f+new Vector3(-d.Z,0,d.X)*.04f),new Vector3(.02f,.015f,.012f),.8f*(1-k/2.4f),0);}}
                    break;}
                case "grab":Grab(rig,v,age,hit,act,target,tk,t);break;
                case "slam":Slam(rig,v,age,hit,act,tk,t);break;
                case "deslag":Deslag(rig,v,age,hit,act,yaw,tk,t);break;
                case "call":{float k=age-hit;if(k>=0&&k<1.6f){float r=1+16*BossMath.Smooth(k/1.1f);
                        // The heat-haze shock rolling out (a faint warm ring of haze and a soft flash at the mouth), no telegraph: it is harmless.
                        if(k<1.2f)for(int i=0;i<8;i++){float ang=i*BossMath.Tau/8;AddHeat(root+new Vector3((float)Math.Cos(ang)*r,1.5f,(float)Math.Sin(ang)*r),3,3,.6f*(1-k/1.2f));}
                        if(k<1.2f)Vfx.Glow(rig.Mouth+rig.MouthAxis*.6f,White*(.8f*(1-k/1.2f)),5,NativeBossVfx.Mote);
                        Vfx.SmokeBurst(age,rig.Mouth,rig.MouthAxis*4.5f+new Vector3(0,1,0),.6f,.5f,14,1.8f,1.0f,3.4f,Soot,.6f,hit,.35f,seed,110,.5f,NativeBossVfx.NoFloor,Underlit*.5f);}
                    break;}
                case "charge":Charge(rig,v,age,hit,act,target,tk,t);break;
            }
        }
        // A heavy foot or tong coming down on the concrete at `p` (the action's clock `age`, its hit `hit`): a white spark spray
        // that skitters off the floor, a dust ring rolling out low and rising, concrete chips.
        private void Impact(Vector3 p,float age,float hit,float k,int key)
        {
            Vfx.SparkBurst(age,p+new Vector3(0,.05f,0),Vector3.UnitY,1.1f,5.5f*k,36,.9f,hit,.05f,seed,key,1,.026f,.9f);
            int n=N(12);for(int i=0;i<n;i++){float ang=i*BossMath.Tau/n+H(seed,key+i)*.4f;var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));
                Vfx.SmokeBurst(age,p+d*.5f+new Vector3(0,.15f,0),d*(3.4f*k)+new Vector3(0,.4f,0),.25f,.22f,1,2.4f,.7f,2.9f*k,Dust,.64f,hit,.06f,seed,key*31+i,0,0);}
            Vfx.RubbleBurst(age,p+new Vector3(0,.1f,0),Vector3.UnitY,1.0f,4.5f*k,6,2.4f,hit,.075f,new Vector3(.3f,.29f,.27f),seed,key);
        }
        // The pour's landing point: the authority's pool for this pour (its hazard, the rule's point 6 m ahead of the boss on the
        // locked facing from the strike), else the same rule from the record.
        private Vector3 PourLanding(in IronworksBossView v,float hit,float yaw)
        {
            if(v.hazards!=null){float want=(BossAt(v.actionAt)?v.actionAt:0)+NativeIronworksBoss.Find("pour").Windup+PourPoolDelay;
                foreach(var h in v.hazards)if(h.kind=="pour"&&Math.Abs(h.hitAt-want)<.35f)return new Vector3(h.x,h.y,h.z);}
            return new Vector3(v.x,v.y,v.z)+BossMath.Forward(yaw)*PourPoolAhead;
        }
        /// <summary>The authority's pour pool (the zone boss authority's rule): 6 m ahead on the locked facing, from the strike's start + 1.6 s
        /// for 4 s, radius 3 (the preview mirrors it).</summary>
        public const float PourPoolAhead=6,PourPoolDelay=1.6f,PourPoolLife=4,PourPoolRadius=3;
        /// <summary>The stream's arc this frame (the checks): the lip, the launch velocity, the landing point and the flight time.</summary>
        public Vector3 StreamLip,StreamLaunch,StreamLand;public float StreamFlight=float.NaN;
        private void Pour(IronworksBossRig rig,in IronworksBossView v,float age,float hit,in NativeIronworksBoss.Action act,float yaw,Vector3 target,float tk,float t)
        {
            var root=rig.Root;float aimYaw=BossMath.YawOf(target-root);if(float.IsNaN(aimYaw))aimYaw=yaw;
            if(age<hit+act.Strike){Wedge(root,aimYaw,26*BossMath.Deg,1.2f,8,tk,t);}
            var lip=rig.Mouth+rig.MouthAxis*.2f;var land=PourLanding(v,hit,yaw);var ax=rig.MouthAxis;float axh=(float)Math.Sqrt(ax.X*ax.X+ax.Z*ax.Z);
            var v0=NativeMoltenFlow.Launch(lip,land,axh>1e-3f?ax.Y/axh:-.6f,out float flight);
            // The metal leaves the lip just before the hit so it lands on it; it stops at the follow-through's end and what is in
            // the air falls on along the arc it left on (latched: the lip swings back up meanwhile). The stream thins in the
            // follow-through and breaks into drops at its end.
            float stopAt=hit+act.Strike+act.Follow*.75f,after=age-stopAt;
            if(after<=0||!pourLatched.Equals(v.actionAt)){latchLip=lip;latchV0=v0;latchFlight=flight;if(after<=0)pourLatched=v.actionAt;}
            else{lip=latchLip;v0=latchV0;flight=latchFlight;}
            StreamLip=lip;StreamLaunch=v0;StreamLand=land;StreamFlight=flight;
            float pourAt=hit-Math.Min(.6f,Math.Max(0,flight-.12f)),since=age-pourAt;
            float on=age<hit?BossMath.Smooth(since/.2f):age<hit+act.Strike?1:Math.Max(.12f,1-(age-hit-act.Strike)/act.Follow);
            // The bubbling at the lip in the windup: boiling glow and a few sparks.
            if(age<pourAt+.1f){float k=BossMath.Smooth(age/Math.Max(.05f,pourAt));Vfx.SparkBurst(age,rig.Mouth,rig.MouthAxis,.5f,2,16,.6f,0,pourAt,seed,120,k);Vfx.Glow(lip,MoltenC*(.9f*k),1.1f,NativeBossVfx.Core);}
            if(since<=0||after>flight+.05f)return;
            float s0=Math.Max(0,after),s1=Math.Min(flight,since);bool landed=since>=flight;
            Melt.Stream(lip,v0,s0,s1,.24f*(.55f+.45f*on),t,.98f,1.1f,.37f,BossMath.Clamp01(1-on)+(after>0?.6f:0),landed);
            for(int i=1;i<=3;i++){var p=NativeMoltenFlow.Arc(lip,v0,(s0+(s1-s0)*i/4f),out _);if(i!=2)AddHeat(p,1.4f,2.2f,.45f*on);Vfx.Glow(p,MoltenC*(.22f*on),1.5f,NativeBossVfx.Mote);}
            Vfx.Glow(lip,White*(.5f*on),1.2f,NativeBossVfx.Core);
            if(!landed)return;
            // The landing: the crown, molten spatter thrown up and falling back, sparks, fumes lit from below, the light on the floor.
            float pour=after>0?Math.Max(0,1-after/flight):on;var vel=NativeMoltenFlow.Arc(lip,v0,flight,out var vl);
            Melt.Crown(land+new Vector3(0,.01f,0),.3f+.12f*pour,.34f*pour+.04f,t,.95f,1,.61f);
            float t0=pourAt+flight,t1=stopAt+flight;
            Vfx.SparkFlow(age,land+new Vector3(0,.08f,0),Vector3.UnitY-vl*.06f,1.0f,3.4f,40,.8f,t0,t1,seed,121,.95f,.05f,.4f,true,land.Y,default,pour);
            Vfx.SparkFlow(age,land+new Vector3(0,.1f,0),Vector3.UnitY,1.1f,6.5f,70,.55f,t0,t1,seed,122,1.1f,.02f,1.2f,true,land.Y,default,pour);
            for(int i=0;i<N(8);i++){float born=t0+(i+H(seed,130+i))*.19f;float k=age-born,life=.55f+.2f*H(seed,140+i);if(k<0||k>life||born>t1)continue;
                var d0=NativeBossVfx.Cone(Vector3.UnitY,.9f,H(seed,150+i),H(seed,160+i),H(seed,170+i))*(2.6f+1.6f*H(seed,180+i));var p=NativeBossVfx.Fly(land+new Vector3(0,.1f,0),d0,.3f,NativeBossVfx.Gravity,k,out var dv);if(p.Y<land.Y)continue;
                Melt.Blob(p,dv,.035f+.02f*H(seed,190+i),1.6f,.95f*(1-.4f*k/life),0,i*.13f,0);}
            Vfx.SmokeFlow(age,land+new Vector3(0,.2f,0),new Vector3(0,1.3f,0),.5f,.85f,8,2.6f,.5f,2.6f,Fume,.36f,t0,t1+.5f,seed,123,.4f,NativeBossVfx.NoFloor,Underlit*.7f,default,.15f+.85f*pour);
            AddHeat(land+new Vector3(0,.9f,0),2.2f,2.4f,.6f*pour);AddLight(land+new Vector3(0,.8f,0),10,new Vector3(2.4f,1.25f,.45f)*(pour*Flicker(t,.7f)));splashAt=land;
        }
        private Vector3 splashAt=new Vector3(float.NaN),latchLip,latchV0;private float latchFlight,pourLatched=float.NaN;
        private void Blow(IronworksBossRig rig,in IronworksBossView v,float age,float hit,in NativeIronworksBoss.Action act,float tk,float t)
        {
            if(v.hazards!=null){Column(rig,age-hit,age,t,false,v);return;}
            RainLayout(rig,v);
            // The circles fade in over the windup, stay until each droplet lands; the clots' circles are bigger.
            float rainStart=hit+2.0f,rainEnd=hit+act.Strike;
            for(int i=0;i<RainCount;i++)
            {
                float x=Rain[i*4],z=Rain[i*4+1],r=Rain[i*4+2],land=rainStart+Rain[i*4+3];if(age>land+.05f)continue;
                float k=age<hit?BossMath.Smooth(age/hit)*tk:.85f;Ring(x,z,r,k,t);
                // The droplet falling from under the roof onto its circle in the last .9 s.
                float f=age-(land-.9f);if(f>=0){var top=new Vector3(x+(H(seed,i*3)-.5f)*1.5f,13.5f,z+(H(seed,i*3+1)-.5f)*1.5f);var bot=new Vector3(x,0,z);float u=BossMath.Clamp01(f/.9f);float uu=u*u;var p=Vector3.Lerp(top,bot,uu);
                    Droplet(p,(bot-top)*(2*u/.9f),r>1.2f?.17f:.12f);}
            }
            Column(rig,age-hit,age,t,true,v);
        }
        // A falling molten droplet (the blow's rain): a stretched glowing blob, a short spark trail, its glow.
        private void Droplet(Vector3 p,Vector3 vel,float r)
        {
            Melt.Blob(p,vel,r,2.2f,.97f,0,p.X*.37f,0,1.1f);Vfx.Spark(p,vel,Spatter*.7f,r*1.1f,.3f,.05f,.9f);Vfx.Glow(p,MoltenC*.5f,r*7,NativeBossVfx.Mote);
        }
        /// <summary>The blow's column for a time `k` from its hit (the inhale before it): fire and sparks to the roof, a smoke ring under
        /// it, sparks off the crane hook; the rain's splashes when `rain`.</summary>
        private void Column(IronworksBossRig rig,float k,float age,float t,bool rain,in IronworksBossView v)
        {
            var mouth=rig.Mouth;var axis=rig.MouthAxis;
            if(k<0){float inh=BossMath.Smooth((k+.75f)/.75f);Vfx.Glow(rig.GrateAt,White*(.8f*inh),2.2f*inh,NativeBossVfx.Core);
                Vfx.SmokeFlow(age,rig.GrateAt+rig.GrateAxis*1.6f,-rig.GrateAxis*2.2f,.3f,.1f,16,.5f,.6f,.25f,Steam,.25f,age-k-.75f,age-k,seed,139,0,NativeBossVfx.NoFloor,default,default,inh);return;}
            float on=k<.25f?BossMath.Smooth(k/.25f):k<1.75f?1:Math.Max(0,1-(k-1.75f)/.35f);if(on<=0&&!rain)return;
            if(on>0)
            {
                float top=A.Roof-1.2f;int n=N(30);
                for(int i=0;i<n;i++)
                {
                    // Fire puffs riding the jet to the roof: white-hot at the root, orange up it, into soot, swirling out as it slows.
                    float u=BossMath.Frac(i/(float)n+t*1.15f+H(seed,i)*.2f);float rise=1-(1-u)*(1-u)*(1-u*.4f);
                    var p=mouth+axis*.4f+Vector3.UnitY*((top-mouth.Y)*rise)+new Vector3((H(seed,i+40)-.5f)*1.6f*u+(float)Math.Sin(t*2.1f+i)*.35f*u,0,(H(seed,i+80)-.5f)*1.6f*u+(float)Math.Cos(t*1.7f+i)*.35f*u);
                    var c=Vector3.Lerp(White*3.0f,MoltenC*3.0f,BossMath.Smooth(u*2.2f));Vfx.Fire(p,c*on,.95f*(1-.3f*u),(1.1f+3.6f*u)*(.8f+.4f*H(seed,i+120)),u*.5f,i*7.13f+seed%97);
                }
                Vfx.Glow(mouth+axis*.6f,White*(1.5f*on),4.5f,NativeBossVfx.Core);Vfx.Glow(mouth+Vector3.UnitY*2.4f,MoltenC*(.6f*on),7.5f,NativeBossVfx.Mote);
                // The jet's luminous core: soft glows stacked up the column, white at its root, orange up it, flickering.
                for(int i=0;i<9;i++){float u=(i+.5f)/9;var p=mouth+Vector3.UnitY*((top-mouth.Y)*u);float fl=.85f+.15f*(float)Math.Sin(t*23+i*1.7f);
                    Vfx.Glow(p,Vector3.Lerp(White*2.4f,MoltenC*2.0f,u)*(on*fl*(1-.4f*u)),3.0f+2.6f*u,NativeBossVfx.Core);}
                // Sparks streaming up the column on drag (they slow and fall back), off the crane hook as the jet hits it.
                Vfx.SparkFlow(age,mouth+axis*.3f,Vector3.UnitY,.22f,15,130,1.3f,age-k,age-k+2.1f,seed,141,1.35f,.034f,.55f,false,0,default,on);
                Vfx.SparkBurst(age,new Vector3(A.HookX,A.HookTip,A.HookZ),-Vector3.UnitY,.9f,3,26,1.3f,age-k,1.6f,seed,142,on);
                // The smoke ring rolling out under the roof.
                Vfx.SmokeFlow(age,new Vector3(mouth.X,A.Roof-1.8f,mouth.Z),new Vector3(0,.2f,0),2.6f,.05f,9,3.2f,2.2f,5.4f,Soot,.55f,age-k,age-k+2.1f,seed,140,.25f,NativeBossVfx.NoFloor,default,default,on);
                AddHeat(mouth+Vector3.UnitY*4,3.4f,8,.9f*on);AddLight(mouth+Vector3.UnitY*2.5f,16,new Vector3(5.5f,3.6f,1.8f)*(on*Flicker(t,1.3f)));
            }
            if(rain&&k>2.0f)
            {
                // Each droplet's splash where it landed (the preview: the authority's pools draw these from v.hazards).
                for(int i=0;i<RainCount;i++)
                {
                    float land=2.0f+Rain[i*4+3],s=k-land;if(s<-.9f||s>2.4f)continue;var p=new Vector3(Rain[i*4],0,Rain[i*4+1]);float r=Rain[i*4+2];
                    PoolFx(900+i,"blow",p,r,s,1,t);
                }
            }
        }
        /// <summary>The rain's circles for the playing blow (14, or 28 in phase 3; 2 or 4 bigger clots), seeded by the arrival and the
        /// blow's start: 4 to 10.5 m out, kept apart, landing staggered over the rain's 1.8 s.</summary>
        public void RainLayout(IronworksBossRig rig,in IronworksBossView v)
        {
            int n=v.Phase>=3?28:14,clots=v.Phase>=3?4:2;uint s=seed^(uint)(BossAt(v.actionAt)?(int)(v.actionAt*8):0);var c=rig.Root;RainCount=0;
            for(int i=0;i<n*3&&RainCount<n;i++)
            {
                float ang=H(s,i*5)*BossMath.Tau,r=4+6.5f*(float)Math.Sqrt(H(s,i*5+1));float x=c.X+(float)Math.Sin(ang)*r,z=c.Z+(float)Math.Cos(ang)*r;
                x=BossMath.Clamp(x,A.HallX0+1,A.HallX1-1);z=BossMath.Clamp(z,A.HallZ0+1,A.HallZ1-1);bool clash=false;
                for(int k=0;k<RainCount;k++){float dx=Rain[k*4]-x,dz=Rain[k*4+1]-z;if(dx*dx+dz*dz<2.6f*2.6f){clash=true;break;}}if(clash)continue;
                int o=RainCount*4;Rain[o]=x;Rain[o+1]=z;Rain[o+2]=RainCount<clots?1.5f:1.0f;Rain[o+3]=1.75f*H(s,i*5+2);RainCount++;
            }
        }
        private void Grab(IronworksBossRig rig,in IronworksBossView v,float age,float hit,in NativeIronworksBoss.Action act,Vector3 target,float tk,float t)
        {
            int leg=rig.ActLeg<0?M.FR:rig.ActLeg;var tip=rig.TongTip[leg<2?leg:0];
            // The cyan line from the tongs to the target and the cyan ring on it through the reach; a glint at the open jaws.
            if(age<hit+act.Strike){Dashes(tip,target,tk,t);Ring(target.X,target.Z,1.2f,1,t,true,.12f,Cyan*tk);Vfx.Glow(tip,White*(.65f*BossMath.Smooth(age/hit)),.9f,NativeBossVfx.Core);}
            float k=age-hit;if(k>=0&&k<.35f){Vfx.Glow(tip,White*(1.1f*(1-k/.35f)),1.6f,NativeBossVfx.Core);}
            if(k>=0&&k<1.2f)Vfx.SparkBurst(age,tip,Vector3.UnitY,.8f,3.2f,26,.6f,hit,.06f,seed,180);
            // Held: the hinge glows when allies hit it (the rig's flinch time), sparks as the jaws grind; freed early: the jaws spring.
            bool held=k>=0&&k<act.Strike+act.Follow-.6f&&v.targetSeat>=0;if(held){Vfx.SparkFlow(age,tip,Vector3.UnitY,.6f,1.6f,12,.5f,hit,hit+act.Strike+act.Follow-.6f,seed,181,.6f);if(BossAt(rig.LastHit)&&t-rig.LastHit<.3f)Vfx.Glow(tip,White*(1.3f*(1-(t-rig.LastHit)/.3f)),1.0f,NativeBossVfx.Core);}
            // The throw's whoosh trail behind the tongs as they fling.
            float th=age-(hit+act.Strike+act.Follow-.6f);if(th>0&&th<.5f)for(int i=0;i<6;i++){float a2=th-i*.03f;if(a2<0)break;Vfx.Glow(tip+new Vector3(0,-.2f*i,0),Cyan*(.25f*(1-th/.5f)),.6f,NativeBossVfx.Mote);}
        }
        private void Slam(IronworksBossRig rig,in IronworksBossView v,float age,float hit,in NativeIronworksBoss.Action act,float tk,float t)
        {
            var c=rig.Root;
            // The preview ring at 9 m through the rear-up; the white shock ring (0.6 m tall: a jump clears it) expanding 0 to 9 m.
            if(age<hit)Ring(c.X,c.Z,9,tk,t);
            float k=age-hit;if(k<0)return;
            if(k<act.Strike)
            {
                float u=k/act.Strike,r=.5f+8.5f*u;int n=Math.Max(24,(int)(r*7));float fade=1-.4f*u;
                for(int i=0;i<n;i++)
                {
                    float a0=i*BossMath.Tau/n,a1=(i+1)*BossMath.Tau/n;var p0=new Vector3(c.X+(float)Math.Cos(a0)*r,.02f,c.Z+(float)Math.Sin(a0)*r);var p1=new Vector3(c.X+(float)Math.Cos(a1)*r,.02f,c.Z+(float)Math.Sin(a1)*r);
                    int q0=Quads.Count;Quads.Add(p0.X,p0.Y,p0.Z,p1.X,p1.Y,p1.Z,p1.X,.6f,p1.Z,p0.X,.6f,p0.Z,White.X*2.4f*fade,White.Y*2.4f*fade,White.Z*2.4f*fade,.85f,0,true,1);Telegraphs++;TeleHash(q0);
                }
            }
            // The dust wall rolling out behind the shock ring: low puffs left in its wake, curling up and spreading.
            if(k<act.Strike+2.4f){int n=N(26);for(int i=0;i<n;i++){float ang=i*BossMath.Tau/n+H(seed,210+i)*.2f;var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));float born=hit+act.Strike*(.15f+.7f*H(seed,240+i));
                    float r=.5f+8.5f*(born-hit)/act.Strike;Vfx.SmokeBurst(age,new Vector3(c.X,0,c.Z)+d*(r-.4f)+new Vector3(0,.25f,0),d*2.2f+new Vector3(0,.7f,0),.3f,.3f,1,2.4f,.9f,2.8f,Dust,.5f,born,.02f,seed,1200+i,0,0);}}
            // The tongs' impact: sparks, dust and chips; the metal wave slopping out of the mouth; the hook's sparks; the hall's dust
            // sifting down from the roof.
            if(k<2.6f)for(int s=0;s<2;s++)Impact(rig.Foot(s),age,hit,1.1f,200+s*3);
            if(k<1.6f)Vfx.SparkBurst(age,rig.Mouth,BossMath.Forward(rig.Yaw)+Vector3.UnitY*.6f,.5f,5,34,1.1f,hit+.15f,.3f,seed,205,1,.045f,.35f);
            if(k<4f)Vfx.SmokeBurst(age,new Vector3(c.X,A.Roof-2,c.Z),new Vector3(0,-.6f,0),2.4f,-.35f,18,4.0f,1.2f,3.2f,Dust*1.3f,.22f,hit+.1f,.9f,seed,206);
        }
        private void Deslag(IronworksBossRig rig,in IronworksBossView v,float age,float hit,in NativeIronworksBoss.Action act,float yaw,float tk,float t)
        {
            if(v.hazards!=null)return;
            var c=rig.Root;var back=-BossMath.Forward(yaw);var right=BossMath.Right(yaw);
            for(int i=0;i<3;i++)
            {
                // The three landing points, 7 to 9 m behind (fixed offsets from the locked facing).
                float dist=7+i,side=(i-1)*2.6f;var land=c+back*dist+right*side;land.X=BossMath.Clamp(land.X,A.HallX0+1,A.HallX1-1);land.Z=BossMath.Clamp(land.Z,A.HallZ0+1,A.HallZ1-1);
                Lumps[i*3]=land.X;Lumps[i*3+1]=land.Z;Lumps[i*3+2]=hit+i*.4f+.9f;
                float launch=hit+i*.4f,landAt=launch+.9f;if(age<landAt+.05f)Ring(land.X,land.Z,1.4f,age<hit?tk:.85f,t);
                // The lump: lobbed out of the mouth on a smoke trail, tumbling, landing on its circle.
                float f=age-launch;if(f>=0&&f<.9f)Lump(rig.Mouth,land,f/.9f,i);
            }
        }
        // A slag lump lobbed from `from` onto `to`, u 0..1 of its .9 s flight: a knobbly crusted lump with glowing cracks, a dark smoke
        // trail behind it and drips of sparks.
        private void Lump(Vector3 from,Vector3 to,float u,int i)
        {
            Vector3 At(float x)=>Vector3.Lerp(from,to,x)+new Vector3(0,4.2f*4*x*(1-x),0);var p=At(u);var vel=(At(Math.Min(1,u+.02f))-At(Math.Max(0,u-.02f)))*(1/(.04f*.9f));
            Melt.Blob(p,vel,.3f,.25f,.85f,.55f,i*.4f+.1f,.55f,1);Vfx.Glow(p,MoltenC*.35f,1.2f,NativeBossVfx.Mote);
            for(int k=1;k<9;k++){float uu=u-k*.045f;if(uu<0)break;Vfx.Smoke(At(uu),Soot,.5f*(1-k/9f),.45f+.14f*k,k/9f,i*31+k,NativeBossVfx.NoFloor,.35f*(1-k/9f));}
        }
        private void Charge(IronworksBossRig rig,in IronworksBossView v,float age,float hit,in NativeIronworksBoss.Action act,Vector3 target,float tk,float t)
        {
            var start=rig.Root;if(age>=hit&&BossAt(chargeFrom.X))start=chargeFrom;else chargeFrom=rig.Root;
            // The cyan dashed path and its two edge lines over the windup and the run.
            if(age<hit+act.Strike){var d=new Vector3(target.X-start.X,0,target.Z-start.Z);float len=d.Length();if(len>.5f){d/=len;var side=new Vector3(-d.Z,0,d.X)*2.7f;Dashes(start,target,tk,t,.2f);Dashes(start+side,target+side,tk*.75f,t,.1f,.3f);Dashes(start-side,target-side,tk*.75f,t,.1f,.3f);}}
            // The bogies dig back throwing sparks; the gallop's sparks under every foot that lands; the skid.
            if(age<hit)for(int s=2;s<4;s++)Vfx.SparkFlow(age,rig.Foot(s),-BossMath.Forward(rig.Yaw)+Vector3.UnitY*.5f,.5f,4.2f,26,.6f,0,hit,seed,240+s,1,.024f,1.1f,true,0,default,BossMath.Smooth(age/hit));
            for(int leg=0;leg<4;leg++)if((rig.Landed&(1<<leg))!=0)landSpark[leg]=age;
            for(int leg=0;leg<4;leg++){float k=age-landSpark[leg];if(k>=0&&k<1.0f){Vfx.SparkBurst(age,rig.Foot(leg),Vector3.UnitY,.8f,3.4f,12,.55f,landSpark[leg],.04f,seed,250+leg*7+(int)(landSpark[leg]*13)%5);
                Vfx.SmokeBurst(age,rig.Foot(leg)+new Vector3(0,.2f,0),Vector3.UnitY*.6f,.9f,.3f,3,1.4f,.5f,1.8f,Dust,.45f,landSpark[leg],.03f,seed,260+leg,0,0);}}
            float sk=age-(hit+act.Strike);if(sk>=0&&sk<act.Follow+1){for(int s=0;s<2;s++)Vfx.SparkFlow(age,rig.Foot(s),BossMath.Forward(rig.Yaw)+Vector3.UnitY*.4f,.4f,5.5f,60,.65f,hit+act.Strike,hit+act.Strike+act.Follow,seed,256+s,1.1f);}
        }
        private Vector3 chargeFrom=new Vector3(float.NaN);private readonly float[] landSpark={-9,-9,-9,-9};

        // ================================================================================================================
        // the pools: the authority's hazards (exact centre and radius), with the preview's mirrors of the same rules
        // ================================================================================================================
        // The pools that expired lately (they solidify and sink after their `until`; the record drops them at once).
        private struct Expired{public int Id;public byte Kind;public float X,Y,Z,R,HitAt,Until;}
        private readonly Expired[] expired=new Expired[48];private int expiredCount;
        private static byte KindCode(string k)=>k=="pour"?(byte)0:k=="blow"?(byte)1:k=="deslag"?(byte)2:(byte)3;
        private static string KindName(byte k)=>k==0?"pour":k==1?"blow":k==2?"deslag":"charge";
        private void Remember(int id,string kind,Vector3 c,float r,float hitAt,float until)
        {
            for(int i=0;i<expiredCount;i++)if(expired[i].Id==id){expired[i]=new Expired{Id=id,Kind=KindCode(kind),X=c.X,Y=c.Y,Z=c.Z,R=r,HitAt=hitAt,Until=until};return;}
            if(expiredCount<expired.Length)expired[expiredCount++]=new Expired{Id=id,Kind=KindCode(kind),X=c.X,Y=c.Y,Z=c.Z,R=r,HitAt=hitAt,Until=until};
            else{int old=0;for(int i=1;i<expired.Length;i++)if(expired[i].Until<expired[old].Until)old=i;expired[old]=new Expired{Id=id,Kind=KindCode(kind),X=c.X,Y=c.Y,Z=c.Z,R=r,HitAt=hitAt,Until=until};}
        }
        /// <summary>How long an expired pool takes to solidify and sink away (s).</summary>
        public const float SolidifySeconds=1.4f;
        private void AuthorityHazards(IronworksBossRig rig,in IronworksBossView v,float t)
        {
            if(!float.IsNaN(rig.DeathAge)){expiredCount=0;return;}
            foreach(var h in v.hazards)
            {
                if(t>h.until)continue;
                var ground=new Vector3(h.x,h.y,h.z);float age=t-h.hitAt;Remember(h.id,h.kind,ground,h.radius,h.hitAt,h.until);
                if(age<0)
                {
                    Ring(h.x,h.z,h.radius,.85f,t);
                    if(age>=-.9f&&(h.kind=="blow"||h.kind=="deslag"))
                    {
                        float u=BossMath.Clamp01(1+age/.9f);
                        if(h.kind=="blow"){var from=ground+Vector3.UnitY*13.5f;var at=Vector3.Lerp(from,ground,u*u);Droplet(at,(ground-from)*(2*u/.9f),.12f);}
                        else Lump(rig.Mouth,ground,u,h.id%3);
                    }
                    if(h.kind=="pour")PoolFx(h.id,h.kind,ground,h.radius,age,h.until-h.hitAt,t);
                    continue;
                }
                PoolFx(h.id,h.kind,ground,h.radius,age,h.until-h.hitAt,t);
                float cool=BossMath.Clamp01(age/Math.Max(.01f,h.until-h.hitAt));
                Ring(h.x,h.z,h.radius,.5f*(1-cool),t);
            }
            // Pools gone from the record (expired): solidifying and sinking away (no longer hot, no telegraph).
            for(int i=0;i<expiredCount;i++)
            {
                var e=expired[i];bool live=false;foreach(var h in v.hazards)if(h.id==e.Id&&t<=h.until){live=true;break;}
                if(live)continue;float f=t-e.Until;if(f<0||f>SolidifySeconds)continue;
                PoolFx(e.Id,KindName(e.Kind),new Vector3(e.X,e.Y,e.Z),e.R,t-e.HitAt,e.Until-e.HitAt,t);
            }
        }
        /// <summary>
        /// One pool from its hazard (kind, id, centre, radius), `age` from its hit and `life` (until - hit): before the hit only the
        /// pour's puddle spreading under the stream; from the hit the full radius (the damaging area), liquid then crusting from the
        /// edge (glowing fissures), bubbling, fuming, shimmering; a splash on landing (rain, slag, the charge's slops); after its
        /// life it solidifies dark and sinks away.
        /// </summary>
        private void PoolFx(int id,string kind,Vector3 c,float R,float age,float life,float t)
        {
            byte k=KindCode(kind);float s=id*.173f+(k*.31f);
            float spread=1;if(age<0){if(k!=0)return;spread=(float)Math.Pow(BossMath.Clamp01((age+PourPoolDelay-.3f)/(PourPoolDelay-.3f)),.55f);if(spread<=.02f)return;}
            float cool=BossMath.Clamp01(age/Math.Max(.05f,life)),gone=age>life?Math.Min(SolidifySeconds,age-life):0;
            float h0=k==2?.82f:.97f,h1=k==2?.6f:k==1?.66f:.68f,c0=k==2?.4f:k==0?.06f:.14f,c1=k==2?.66f:.5f;
            float heat=(h0+(h1-h0)*cool)*(1-BossMath.Smooth(gone/.35f));float crust=c0+(c1-c0)*cool;crust+=(1-crust)*BossMath.Smooth(gone/.3f);
            if(age<0)heat=Math.Max(heat,.97f);
            float lobes=.02f,fingers=k==1?.04f:k==3?.03f:k==2?.03f:0;float sink=.045f*BossMath.Smooth((gone-.3f)/.7f);
            Melt.Pool(id,c-new Vector3(0,sink,0),R,spread,lobes,fingers,heat,crust,Math.Max(0,age),s,1);
            float area=R*R;float hot=heat*(1-.5f*crust);float hitClock=t-age,from=hitClock-(k==0?PourPoolDelay-.3f:0),to=hitClock+life+.2f;
            // Fumes off the hot metal, lit from below; spits of sparks from bursting bubbles (thinning as it crusts; the ones alive
            // when it expires finish their lives).
            int emit=R>2?3:1;
            for(int e=0;e<emit;e++){float ang=e*2.1f+s;var o=emit>1?new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*(R*.45f):Vector3.Zero;
                Vfx.SmokeFlow(t,c+o+new Vector3(0,.15f,0),new Vector3(0,.55f,0),.35f,.65f,1.6f+.5f*area/emit,2.8f,.45f+.15f*R,1.6f+.5f*R,Fume,.24f,from,to,seed,7000+id*7+e,.32f,NativeBossVfx.NoFloor,Underlit*.6f,default,hot*spread);}
            Vfx.SparkFlow(t,c+new Vector3(0,.06f,0),Vector3.UnitY,.9f,2.2f,2.5f+2.2f*area,.45f,from,to,seed,7500+id,.8f,.022f,1.0f,true,c.Y,default,hot*(1-crust*.7f)*spread);
            if(gone>0||heat<.05f)return;
            // The floor's glow round it (a soft warm falloff just past its edge), its shimmer, its light on the hall.
            FloorGlow(c,R*1.45f*spread,MoltenC*(.16f*hot));
            AddHeat(c+new Vector3(0,.5f+R*.25f,0),R*1.7f*spread,1.4f+R*.4f,.38f*hot);
            if(k==0&&LightCount<LightMax)AddLight(c+new Vector3(0,.9f,0),6+R*2.2f,new Vector3(1.6f,.8f,.28f)*(hot*Flicker(t,s)));
            // Landing: the splash of a drop, a lump or a slop; slag throws glassy shards and dark smoke.
            if(age>=0&&age<.6f&&k!=0)
            {
                float sp=1-BossMath.Smooth(age/.35f);if(sp>0)Melt.Crown(c+new Vector3(0,.01f,0),R*.3f,.22f*R*sp+.02f,t,.95f,1,s);
                Vfx.SparkBurst(t,c+new Vector3(0,.08f,0),Vector3.UnitY,1.0f,3.6f+R,k==1?16:22,.7f,t-age,.05f,seed,7600+id,1,.026f);
                Vfx.SparkBurst(t,c+new Vector3(0,.06f,0),Vector3.UnitY,1.1f,2.6f,k==2?6:10,.8f,t-age,.06f,seed,7700+id,.9f,.05f,.35f,true,c.Y);
                if(k==2){Vfx.RubbleBurst(t,c+new Vector3(0,.15f,0),Vector3.UnitY,1.1f,3.2f,5,2.6f,t-age,.09f,Glass,seed,7800+id,1.4f,c.Y);
                    Vfx.SmokeBurst(t,c+new Vector3(0,.3f,0),new Vector3(0,1.3f,0),.9f,.6f,8,2.4f,.8f,2.8f,Soot,.5f,t-age,.1f,seed,7900+id,.5f,NativeBossVfx.NoFloor,Underlit*.6f);}
                else Vfx.SmokeBurst(t,c+new Vector3(0,.2f,0),new Vector3(0,1.0f,0),.6f,.6f,4,1.8f,.4f,1.8f,Fume,.35f,t-age,.08f,seed,7900+id,.6f,NativeBossVfx.NoFloor,Underlit);
            }
        }
        private void Leftovers(IronworksBossRig rig,in IronworksBossView v,float t)
        {
            // The DEV preview (no record): the authority's pools mirrored by the same rules (the zone boss authority's): the pour's
            // pool 6 m ahead on the locked facing from the strike + 1.6 s for 4 s; the deslag's three patches (1.4 m, 6 s); the
            // charge's trail (1.8 m, 3 s, every .2 s of the run). The blow's are drawn by its column.
            if(!BossAt(v.actionAt)||v.action==null)return;var a=NativeIronworksBoss.Find(v.action);float age=t-v.actionAt,hit=a.Windup;
            float yaw=BossAt(v.actionYaw)?v.actionYaw*BossMath.Deg:rig.Yaw;
            switch(v.action)
            {
                case "pour":{float k=age-hit;if(k<0||k>PourPoolDelay+PourPoolLife+SolidifySeconds)break;var p=new Vector3(v.x,v.y,v.z)+BossMath.Forward(yaw)*PourPoolAhead;
                    PoolFx(800,"pour",p,PourPoolRadius,k-PourPoolDelay,PourPoolLife,t);break;}
                case "deslag":for(int i=0;i<3;i++){float landAt=hit+i*.4f+.9f,k=age-landAt;if(k<0||k>6+SolidifySeconds)continue;if(!(Lumps[i*3]!=0||Lumps[i*3+1]!=0))continue;var p=new Vector3(Lumps[i*3],0,Lumps[i*3+1]);
                        PoolFx(810+i,"deslag",p,1.4f,k,6,t);}break;
                case "charge":{float k=age-hit;if(k<0||k>a.Strike+3+SolidifySeconds)break;var from=BossAt(chargeFrom.X)?chargeFrom:rig.Root;var to=rig.Root;var d=to-from;d.Y=0;float len=d.Length();if(len<.5f)break;d/=len;
                        int n=Math.Min(10,(int)(a.Strike/.2f));for(int i=0;i<n;i++){float born=hit+i*.2f,run=Math.Min(1,(born-hit)/a.Strike);if(age<born)continue;var p=from+d*(len*run);PoolFx(820+i,"charge",new Vector3(p.X,0,p.Z),1.8f,age-born,3,t);}break;}
            }
        }

        // ================================================================================================================
        // phase changes, moments, the death, the hall's vessels, the lights
        // ================================================================================================================
        private void PhaseFx(IronworksBossRig rig,in IronworksBossView v,float t)
        {
            // Crust chunks flying: glassy slag shards tumbling off, glowing at first, a cream flash where each leaves, dust where it lands.
            for(int k=0;k<12;k++)
            {
                float at=rig.ChunkAt[k];if(!BossAt(at))continue;float s=t-at;if(s<0||s>3.2f)continue;var j=L.Crust(k/3,k%3);var p=rig.Shown.Position[j]+BossMath.Rotate(rig.Shown.Rotation[j],L.ChunkCentre[k]);
                if(s<.25f)Vfx.Glow(p,Cream(1.3f*(1-s/.25f)),2.2f,NativeBossVfx.Mote);
                var outward=Vector3.Normalize(BossMath.Flat(p-rig.Root)+new Vector3(0,.8f,0)+new Vector3(0,0,.001f));
                Vfx.RubbleBurst(t,p,outward,.7f,4.2f,10,3.2f,at,.13f,Glass,seed,300+k,1.2f);
                if(s<1.4f)Vfx.SparkBurst(t,p,outward,.8f,3.5f,14,.7f,at,.08f,seed,330+k,.9f);
                if(s>.4f)Vfx.SmokeBurst(t,new Vector3(p.X,.3f,p.Z),new Vector3(0,.5f,0),.9f,.3f,5,1.6f,.8f,2.2f,Dust,.4f,at+.45f,.4f,seed,360+k,0,0);
            }
            // The break: the arch cracking (sparks), the pulpit's chains (drawn by the runtime as links), the seams' flash, the pit welling.
            if(BossAt(rig.PulpitAt)){float s=t-rig.PulpitAt;if(s>=0&&s<2.0f)Vfx.SparkBurst(t,rig.TearTop[0]*.5f+rig.TearTop[1]*.5f,Vector3.UnitY,.9f,4.2f,44,1.0f,rig.PulpitAt,.2f,seed,320);}
            if(v.Phase>=3&&BossAt(v.phaseAt)&&float.IsNaN(rig.DeathAge))
            {
                float s=t-v.phaseAt;if(s>=1.0f&&s<2.0f)Vfx.Glow(rig.Shown.Position[M.Vessel]+new Vector3(0,1,0),White*(1.0f*(1-(s-1)/1)),7,NativeBossVfx.Mote);
                // The pit wells up white and spills to its rim as the break lands (inside the pit: the overflow cycles are the hazard's).
                float well=BossMath.Smooth((s-1.2f)/1.4f)*(1-.5f*BossMath.Smooth((s-6)/4));
                if(well>0){Melt.Pool(-3,new Vector3(A.PitX,A.PitY-.004f,A.PitZ),2.95f,BossMath.Clamp01(.3f+.7f*well),.012f,0,.55f+.4f*well,BossMath.Clamp01(.6f-.4f*well),s,.55f,1,1);
                    AddLight(new Vector3(A.PitX,.9f,A.PitZ),12,new Vector3(3.2f,1.7f,.6f)*well);if(s<3.6f)Vfx.SparkBurst(t,new Vector3(A.PitX,.1f,A.PitZ),Vector3.UnitY,1.1f,4,34,1.1f,v.phaseAt+1.2f,2.2f,seed,330,well);
                    Vfx.SmokeFlow(t,new Vector3(A.PitX,.3f,A.PitZ),new Vector3(0,.8f,0),1.2f,.6f,5,2.8f,1.0f,3.6f,Fume,.35f,v.phaseAt+1.2f,v.phaseAt+12,seed,335,.6f,NativeBossVfx.NoFloor,Underlit,default,well);}
            }
        }
        private void MomentFx(IronworksBossRig rig,in IronworksBossView v,float t)
        {
            if(rig.MomentIndex<0)return;var a=NativeIronworksBoss.Actions[rig.MomentIndex];float age=rig.MomentAct.Age;
            switch(a.Id)
            {
                // The choked blow: a thick black cough of soot out of the mouth, rolling and rising.
                case "choke":Vfx.SmokeBurst(age,rig.Mouth,rig.MouthAxis*4.5f+Vector3.UnitY*1.2f,.6f,.5f,28,2.8f,.9f,3.4f,Soot*.6f,.85f,0,.6f,seed,400);Vfx.Glow(rig.GrateAt,White*.5f,1.6f,NativeBossVfx.Core);break;
                case "kneel":for(int leg=0;leg<2;leg++){var knee=rig.Shown.Position[M.LegJoint(leg,M.Tibia)];Vfx.SmokeBurst(age,knee,new Vector3(0,2.2f,0),.7f,.6f,18,1.6f,.4f,1.8f,Steam,.6f,leg*.15f,1.2f,seed,410+leg);}
                    // A slop of metal out of the mouth as it buckles, crusting at once (harmless: not the hazard's).
                    if(age>.4f&&age<3.2f){var p=rig.Mouth;p.Y=0;p+=BossMath.Forward(rig.Yaw)*1.5f;float s=age-.4f;Melt.Pool(-4,p,.8f,BossMath.Smooth(s/.3f),.05f,.06f,.7f*(1-BossMath.Smooth((s-1.6f)/1.2f)),BossMath.Clamp01(.35f+.4f*s),s,.4f,1);}break;
                case "crash":if(age<2.6f){Vfx.SparkBurst(age,rig.Mouth,-BossMath.Forward(rig.Yaw)+Vector3.UnitY,.9f,6,60,1.0f,0,.15f,seed,420);
                    Vfx.SmokeBurst(age,rig.Root+BossMath.Forward(rig.Yaw)*4,new Vector3(0,1,0),1.8f,.45f,22,2.4f,1.2f,3.6f,Dust,.55f,0,.4f,seed,421);
                    Vfx.RubbleBurst(age,rig.Root+BossMath.Forward(rig.Yaw)*4.4f+new Vector3(0,2.2f,0),-BossMath.Forward(rig.Yaw)+Vector3.UnitY*.4f,.8f,4.5f,10,2.6f,.05f,.14f,new Vector3(.35f,.18f,.13f),seed,422);}break;
            }
        }
        private void Death(IronworksBossRig rig,in IronworksBossView v,float d,float t)
        {
            var vp=rig.Shown.Position[M.Vessel];
            // The last pour into the pit: a thinning stream, then drips (from 1 s), a pool spreading and cooling.
            float pour=d<1.0f?0:d<2.6f?1-.6f*BossMath.Smooth((d-1.0f)/1.6f):Math.Max(0,.4f-(d-2.6f)*.4f);
            var lip=rig.Mouth+rig.MouthAxis*.2f;var land=new Vector3(lip.X,0,lip.Z)+BossMath.Forward(rig.Yaw)*.6f;
            if(pour>0)
            {
                var v0=NativeMoltenFlow.Launch(lip,land,-.6f,out float flight);float since=d-1.0f;
                Melt.Stream(lip,v0,0,Math.Min(flight,since),(.03f+.11f*pour),t,.92f,1,.5f,BossMath.Clamp01(1-pour*1.4f),since>=flight);
                if(pour<.45f)Vfx.SparkFlow(t,lip,-Vector3.UnitY,.15f,.6f,4,1.1f,0,1e7f,seed,505,.8f,.05f,.2f);
                AddLight(land+new Vector3(0,.8f,0),9,new Vector3(3,1.6f,.6f)*pour);
            }
            float k=d-1.0f;if(k>0){float cool=BossMath.Clamp01((d-1.6f)/2.6f);Melt.Pool(-5,land,1.6f,BossMath.Smooth(k/1.6f)*.95f+.05f,.05f,0,.9f-.75f*cool,.2f+.75f*cool,k,.6f,1);}
            // Steam columns rising off the cooling shell; the whistle's steam; the hall's vessels exhale (the hall's own builder).
            float steam=BossMath.Smooth((d-1.4f)/1)*(1-BossMath.Smooth((d-9)/4));
            if(steam>0)for(int c=0;c<3;c++)Vfx.SmokeFlow(t,vp+new Vector3((c-1)*1.2f,.8f,(c%2)*.6f-.3f),new Vector3(.1f,1.8f,0),.35f,1.1f,5,3.0f,.6f,2.6f,Steam,.42f,0,1e7f,seed,500+c,0,NativeBossVfx.NoFloor,default,default,steam);
            var stage=NativeIronworksBossStage.Evaluate(v,t,d,rig);if(stage.Whistle>0){var w=rig.WhistleAt;Vfx.SmokeFlow(t,w,new Vector3(.2f,3.2f,-.3f),.3f,.9f,22,1.8f,.2f,1.6f,Steam,.7f,0,1e7f,seed,510,0,NativeBossVfx.NoFloor,default,default,stage.Whistle);Vfx.Glow(w+new Vector3(0,.3f,0),new Vector3(1,1,1)*.45f*stage.Whistle,.8f,NativeBossVfx.Mote);}
        }
        private void HallVessels(in IronworksBossView v,float t,IronworksBossRig rig)
        {
            var s=NativeIronworksBossStage.Evaluate(v,t,rig.DeathAge,rig);
            for(int i=0;i<2;i++)
            {
                float wake=i==0?s.WakeA:s.WakeB;var mouth=new Vector3(A.VesselX,A.VesselY,A.VesselMouthZ(i));
                if(wake>0){Vfx.Glow(mouth,White*(1.1f*wake),3.2f+2*wake,NativeBossVfx.Core);Vfx.Glow(mouth+new Vector3(.6f,.5f,0),MoltenC*(.4f*wake),6,NativeBossVfx.Mote);
                    Vfx.SparkFlow(t,mouth,new Vector3(.7f,1,0),.6f,4.5f,60,1.1f,0,1e7f,seed,600+i,1,.024f,1.1f,true,0,default,wake);
                    Vfx.SmokeFlow(t,mouth,new Vector3(.6f,1.4f,0),.4f,.7f,5,2.6f,1.0f,3.4f,Soot,.5f,0,1e7f,seed,610+i,.5f,NativeBossVfx.NoFloor,Underlit*.6f,default,wake);if(LightCount<LightMax&&i==0||wake>.5f)AddLight(mouth+new Vector3(.8f,.4f,0),13,new Vector3(4.2f,2.8f,1.4f)*wake);}
                if(s.Exhale>0)Vfx.SmokeFlow(t,mouth,new Vector3(.4f,1.4f,0),.4f,.8f,7,2.8f,.9f,3.2f,Steam,.5f,0,1e7f,seed,620+i,0,NativeBossVfx.NoFloor,default,default,s.Exhale);
            }
        }
        private void Lights(IronworksBossRig rig,in IronworksBossView v,float t,float[] ch,bool dead)
        {
            // The mouth's light (white-hot, warm round it), dimming as the metal cools; the lamps' cold light ahead of the yoke.
            float heat=rig.HeatLevel*(1-rig.CoolLevel);if(heat>.02f)AddLight(rig.Mouth+rig.MouthAxis*.8f,11+3*heat,new Vector3(3.2f,2.2f,1.2f)*Math.Min(1.6f,heat));
            if(rig.LampLevel>.02f){var mid=(rig.LampAtL+rig.LampAtR)*.5f+rig.BeamL*2.5f;AddLight(mid,12,new Vector3(1.5f,1.65f,1.9f)*rig.LampLevel);}
        }
    }
}
