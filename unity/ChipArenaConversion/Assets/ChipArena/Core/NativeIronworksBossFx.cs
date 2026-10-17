using System;
using System.Numerics;
using M=ChipArena.UnityConversion.NativeIronworksBossModel;
using L=ChipArena.UnityConversion.NativeIronworksBossLook;
using Ch=ChipArena.UnityConversion.IronworksBossCh;
using A=ChipArena.UnityConversion.NativeIronworksBossArena;

namespace ChipArena.UnityConversion
{
    /// <summary>One molten surface the boss adds (drawn through Resources/NativeMolten.shader as the kit's molten pieces are): a box
    /// (centre, rotation, half extents) with the shader's style (kind, signed axis + 1, flow speed scale, gain), heat (start, end,
    /// crust, front) and flow (speed m/s, seed).</summary>
    public struct IronworksBossMolten{public Vector3 At,Half;public Quaternion Rot;public float Kind,Axis,Live,Gain,HeatA,HeatB,Crust,Front,Speed,Seed;}

    /// <summary>
    /// THE CONVERTER's effects for one frame (doc section 9's list), in the Ironworks' roles and the game's danger conventions
    /// (docs/EXPEDITION-CITY-IRONWORKS.md readability rule 4): area telegraphs are hard-edged cream-white floor decals (the pour's
    /// wedge, the rain's circles, the stomp's ring, the slam's preview ring, the deslag's circles), lines are cyan dashes (the grab's
    /// line and ring, the charge's path and edges), fast attacks flare white on the body; the hazard kit's amber stays the hazards'
    /// own (the hall vessels' lanes are Astra's converter-tilt hazard, drawn by the hazard presentation on its schedule, never here).
    /// <list type="bullet">
    /// <item>Quads (<see cref="Quads"/>, one solid and one effects batch): halos, sparks, embers, smoke, steam, dust, shards, the
    /// column's fire, the rain's streaks, the slam's white shock ring, the lamps' soft beams, the telegraphs.</item>
    /// <item>Molten surfaces (<see cref="Molten"/>): the spill strip down the nose, the pour's stream and pool, the rain's splashes,
    /// the slam's wave pool, the deslag's lumps and slag patches, the charge's trail, the pit's cross of cracks in the arrival and its
    /// welling in the break, the death's last pour.</item>
    /// <item>Up to three practical lights (<see cref="LightPos"/>, the runtime merges them into the farthest warm slots) and heat haze
    /// sources (<see cref="Heat"/>).</item>
    /// </list>
    /// Deterministic (the same frames give the same output; the pour's splash point and the charge's start are remembered from the
    /// frames before). Counts scale with the effects quality and the low tier, except the telegraphs and halos. Allocation free.
    /// </summary>
    public sealed class NativeIronworksBossFx
    {
        public const int Capacity=3600,MoltenMax=96,LightMax=3,HeatMax=12;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public readonly IronworksBossMolten[] Molten=new IronworksBossMolten[MoltenMax];public int MoltenCount;
        public readonly float[] LightPos=new float[LightMax*4],LightRgb=new float[LightMax*3],LightBeam=new float[LightMax];public int LightCount;
        public readonly NativeHeatSource[] Heat=new NativeHeatSource[HeatMax];public int HeatCount;
        public int Telegraphs,SparkCount,SmokeCount;
        /// <summary>The cream-white telegraph decals (NativeIronworksSceneFx's: the readability rules' area decal) and the cyan line.</summary>
        public const float CreamR=NativeIronworksSceneFx.DecalR,CreamG=NativeIronworksSceneFx.DecalG,CreamB=NativeIronworksSceneFx.DecalB,CreamGain=NativeIronworksSceneFx.DecalGain,DecalY=.03f,DecalW=.14f;
        public static readonly Vector3 Cyan=Lin(NativeIronworksKinds.Cyan)*2.4f,White=Lin(NativeIronworksKinds.WhiteHot),ColdWhite=Lin(NativeIronworksKinds.ColdWhite),MoltenC=Lin(NativeIronworksKinds.Molten);
        private static Vector3 Lin(int hex)=>WeaponPaint.Linear(hex);
        private float ex,ey,ez,rx,ry,rz,ux,uy,uz;private double quality;private bool low;private uint seed;
        /// <summary>The rain's circles this blow (x, z, radius, landing time from the hit) and the deslag's landings: shared with the cues.</summary>
        public readonly float[] Rain=new float[4*28];public int RainCount;public readonly float[] Lumps=new float[3*3];

        private int N(int full)=>NativeBrickfallAtmosphere.Count(full,quality,low);
        private static float H(uint s,int i)=>BossMath.Hash(s,i);

        public void Build(IronworksBossRig rig,in IronworksBossView v,float t,float camX,float camY,float camZ,float rightX,float rightY,float rightZ,float upX,float upY,float upZ,float effects,bool lowTier)
        {
            Quads.Clear();MoltenCount=0;LightCount=0;HeatCount=0;Telegraphs=SparkCount=SmokeCount=0;RainCount=0;
            ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;quality=effects;low=lowTier;
            if(rig==null||!v.live||!rig.Visible)return;seed=rig.Seed^(uint)(BossAt(v.spawned)?(int)(v.spawned*16):0);
            var ch=rig.Channels;bool dead=!float.IsNaN(rig.DeathAge);
            Always(rig,v,t,ch,dead);
            if(rig.Arriving)Arrival(rig,v,rig.ArrivalAge,t);
            if(rig.ActIndex>=0)Action(rig,v,t);
            PhaseFx(rig,v,t);MomentFx(rig,v,t);Leftovers(rig,v,t);
            if(dead)Death(rig,v,rig.DeathAge,t);
            HallVessels(v,t,rig);
            Lights(rig,v,t,ch,dead);
        }
        private static bool BossAt(float t)=>!float.IsNaN(t);

        // ================================================================================================================
        // primitives
        // ================================================================================================================
        private void Sprite(Vector3 p,float size,Vector3 c,float a,bool additive=true,int layer=1)
        {
            float h=size/2,ax=rx*h,ay=ry*h,az=rz*h,bx=ux*h,by=uy*h,bz=uz*h;
            Quads.Add(p.X-ax-bx,p.Y-ay-by,p.Z-az-bz,p.X+ax-bx,p.Y+ay-by,p.Z+az-bz,p.X+ax+bx,p.Y+ay+by,p.Z+az+bz,p.X-ax+bx,p.Y-ay+by,p.Z-az+bz,c.X,c.Y,c.Z,a,1,additive,layer);
        }
        private void Streak(Vector3 a,Vector3 b,float w,Vector3 c,float al,int mode=1)
        {
            float dx=b.X-a.X,dy=b.Y-a.Y,dz=b.Z-a.Z,cx=(a.X+b.X)/2-ex,cy=(a.Y+b.Y)/2-ey,cz=(a.Z+b.Z)/2-ez;float px=dy*cz-dz*cy,py=dz*cx-dx*cz,pz=dx*cy-dy*cx,l=(float)Math.Sqrt(px*px+py*py+pz*pz);if(l<1e-9f)return;
            px*=w/2/l;py*=w/2/l;pz*=w/2/l;Quads.Add(a.X-px,a.Y-py,a.Z-pz,b.X-px,b.Y-py,b.Z-pz,b.X+px,b.Y+py,b.Z+pz,a.X+px,a.Y+py,a.Z+pz,c.X,c.Y,c.Z,al,mode,true,1);
        }
        // A floor quad (four corners at height y), hard edged (mode 0) or soft (1).
        private void FloorQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 col,float al,int mode=0){Quads.Add(a.X,a.Y,a.Z,b.X,b.Y,b.Z,c.X,c.Y,c.Z,d.X,d.Y,d.Z,col.X,col.Y,col.Z,al,mode,true,1);}
        private Vector3 Cream(float k)=>new Vector3(CreamR,CreamG,CreamB)*(CreamGain*k);
        /// <summary>A hard-edged cream ring on the floor (radius r, width w), dashed when `dash` (segments marching slowly).</summary>
        private void Ring(float cx,float cz,float r,float k,float t,bool dash=false,float w=DecalW,Vector3? color=null)
        {
            if(k<=0||r<=.05f)return;var col=color??Cream(k);int n=Math.Max(12,Math.Min(64,(int)(r*7)));float spin=dash?t*.4f:0;
            for(int i=0;i<n;i++)
            {
                if(dash&&(i&1)==1)continue;float a0=spin+i*BossMath.Tau/n,a1=spin+(i+1)*BossMath.Tau/n+(dash?0:.002f);
                float c0=(float)Math.Cos(a0),s0=(float)Math.Sin(a0),c1=(float)Math.Cos(a1),s1=(float)Math.Sin(a1),ri=r-w;
                FloorQuad(new Vector3(cx+c0*r,DecalY,cz+s0*r),new Vector3(cx+c1*r,DecalY,cz+s1*r),new Vector3(cx+c1*ri,DecalY,cz+s1*ri),new Vector3(cx+c0*ri,DecalY,cz+s0*ri),col,1);Telegraphs++;
            }
        }
        /// <summary>A cream wedge outline: an arc at r0 and r1 over ±half (radians) about yaw, and its two radial edges; a soft fill.</summary>
        private void Wedge(Vector3 o,float yaw,float half,float r0,float r1,float k,float t)
        {
            if(k<=0)return;var col=Cream(k);int n=18;
            for(int e=0;e<2;e++)
            {
                float r=e==0?r0:r1;for(int i=0;i<n;i++)
                {
                    float a0=yaw-half+2*half*i/n,a1=yaw-half+2*half*(i+1)/n;var p0=o+BossMath.Forward(a0)*r;var p1=o+BossMath.Forward(a1)*r;var q0=o+BossMath.Forward(a0)*(r-DecalW);var q1=o+BossMath.Forward(a1)*(r-DecalW);
                    FloorQuad(Y(p0),Y(p1),Y(q1),Y(q0),col,1);Telegraphs++;
                }
            }
            for(int s=-1;s<=1;s+=2){float a=yaw+s*half;var dir=BossMath.Forward(a);var side=BossMath.Right(a)*(DecalW*.5f);var p0=o+dir*r0;var p1=o+dir*r1;FloorQuad(Y(p0-side),Y(p1-side),Y(p1+side),Y(p0+side),col,1);Telegraphs++;}
            // The fill: a faint soft wash inside so the whole area reads, under the edge's brightness.
            for(int i=0;i<6;i++){float a0=yaw-half+2*half*i/6,a1=yaw-half+2*half*(i+1)/6;var p0=o+BossMath.Forward(a0)*r0;var p1=o+BossMath.Forward(a1)*r0;var q1=o+BossMath.Forward(a1)*r1;var q0=o+BossMath.Forward(a0)*r1;FloorQuad(Y(p0),Y(p1),Y(q1),Y(q0),col*.12f,1);}
        }
        private static Vector3 Y(Vector3 p)=>new Vector3(p.X,DecalY,p.Z);
        /// <summary>A dashed cyan line on the floor from a to b (dashes marching toward b), `w` wide.</summary>
        private void Dashes(Vector3 a,Vector3 b,float k,float t,float w=.16f,float dash=.55f)
        {
            if(k<=0)return;var d=new Vector3(b.X-a.X,0,b.Z-a.Z);float len=d.Length();if(len<.1f)return;d/=len;var side=new Vector3(-d.Z,0,d.X)*(w*.5f);var col=Cyan*k;
            float off=BossMath.Frac(t*1.4f)*dash*2;
            for(float s=-off;s<len;s+=dash*2){float s0=Math.Max(0,s),s1=Math.Min(len,s+dash);if(s1<=s0)continue;var p0=a+d*s0;var p1=a+d*s1;FloorQuad(Y(p0-side),Y(p1-side),Y(p1+side),Y(p0+side),col,1);Telegraphs++;}
        }
        private void AddMolten(Vector3 at,Vector3 half,Quaternion rot,byte kind,int axis,int sign,float speed,float heatA,float heatB,float crust,float front=1,float gain=1,float live=1,float seedK=0)
        {
            if(MoltenCount>=MoltenMax||half.X<=0||half.Y<=0||half.Z<=0)return;
            Molten[MoltenCount++]=new IronworksBossMolten{At=at,Half=half,Rot=rot,Kind=kind,Axis=(axis+1f)*(sign<0?-1:1),Live=live,Gain=gain,HeatA=heatA,HeatB=heatB,Crust=crust,Front=front,Speed=speed,Seed=seedK};
        }
        private void AddHeat(Vector3 p,float w,float h,float s){if(HeatCount>=HeatMax||s<=.02f)return;Heat[HeatCount++]=new NativeHeatSource{X=p.X,Y=p.Y,Z=p.Z,W=w,H=h,Strength=s};}
        private void AddLight(Vector3 p,float range,Vector3 rgb,float beam=0)
        {
            if(LightCount>=LightMax||rgb.X+rgb.Y+rgb.Z<.02f)return;int k=LightCount++;LightPos[k*4]=p.X;LightPos[k*4+1]=p.Y;LightPos[k*4+2]=p.Z;LightPos[k*4+3]=range;LightRgb[k*3]=rgb.X;LightRgb[k*3+1]=rgb.Y;LightRgb[k*3+2]=rgb.Z;LightBeam[k]=beam;
        }
        // A spray of sparks from p along `dir` (spread, speed), `count`, each living `life`, over the window [t0, t0 + span).
        private void Sparks(Vector3 p,Vector3 dir,float spread,float speed,int count,float life,float t,float t0,float span,int key,float bright=1)
        {
            int n=N(count);for(int i=0;i<n;i++)
            {
                float born=t0+span*H(seed,key*977+i*7),age=t-born;if(age<0||age>life*(.6f+.4f*H(seed,key*977+i*7+1)))continue;
                var j=new Vector3(H(seed,key*977+i*7+2)-.5f,H(seed,key*977+i*7+3)-.5f,H(seed,key*977+i*7+4)-.5f)*2*spread;var v0=Vector3.Normalize(dir+j)*speed*(.55f+.6f*H(seed,key*977+i*7+5));
                var a=p+v0*age+new Vector3(0,-4.9f*age*age,0);if(a.Y<.02f)continue;var vel=v0+new Vector3(0,-9.8f*age,0);var b=a-vel*.035f;float f=1-age/life;
                Streak(a,b,.035f,new Vector3(3.2f,1.5f,.45f)*bright*f,1,1);SparkCount++;
            }
        }
        // Soft puffs rising from p (smoke dark or steam white), growing and fading.
        private void Puffs(Vector3 p,Vector3 drift,int count,float life,float size,Vector3 col,float alpha,float t,float t0,float span,int key,bool additive=false)
        {
            int n=N(count);for(int i=0;i<n;i++)
            {
                float born=t0+span*H(seed,key*613+i*5),age=t-born;if(age<0||age>life)continue;float u=age/life;
                var j=new Vector3(H(seed,key*613+i*5+1)-.5f,H(seed,key*613+i*5+2)*.3f,H(seed,key*613+i*5+3)-.5f);var q=p+(drift+j*1.2f)*age;
                Sprite(q,size*(.5f+1.6f*u),col,alpha*(1-u)*(1-u)*BossMath.Smooth(u*6),additive,1);SmokeCount++;
            }
        }

        // ================================================================================================================
        // always: the mouth's glow, smoke and drips, the spill strip, the lamps' halos and beams, the visor, the seams' sparks
        // ================================================================================================================
        private void Always(IronworksBossRig rig,in IronworksBossView v,float t,float[] ch,bool dead)
        {
            var P=rig.Shown;float heat=rig.HeatLevel*(1-rig.CoolLevel);var mouth=rig.Mouth;var axis=rig.MouthAxis;
            // The mouth's halo (white at its core, warm round it) and the heat haze over it.
            if(heat>.02f){Sprite(mouth+axis*.25f,2.4f+.9f*heat,White*(.55f*heat),.9f);Sprite(mouth+axis*.6f,4.6f,MoltenC*(.22f*heat),.8f);AddHeat(mouth+axis*1.4f,2.6f,3.4f,.55f*Math.Min(1.3f,heat));}
            // Smoke curling off the mouth (thicker when it is hot), drips and slops off the lip when the metal spills.
            if(!dead||rig.DeathAge<3)Puffs(mouth+axis*.5f,new Vector3(.15f,1.3f,.05f),14,3.2f,1.4f,new Vector3(.07f,.065f,.06f),.42f*Math.Min(1,heat+.2f),t,(float)Math.Floor(t/3.2f)*3.2f,3.2f,1);
            if(rig.Spill>.05f)Sparks(mouth+BossMath.Rotate(P.Rotation[M.Vessel],new Vector3(0,-.15f,.7f)),new Vector3(0,-1,0),.4f,1.2f,10,.9f,t,(float)Math.Floor(t*2)/2,.5f,2,rig.Spill);
            // The spill: a molten strip down the nose (flowing down it), hotter as the metal slops.
            var vr=P.Rotation[M.Vessel];var vp=P.Position[M.Vessel];var path=L.SpillPath;var wid=L.SpillWidth;float spill=Math.Max(.35f,Math.Min(1.2f,.55f+.5f*rig.Spill))*(1-rig.CoolLevel*.9f);
            for(int i=0;i+1<path.Length;i++)
            {
                var a=vp+BossMath.Rotate(vr,path[i]);var b=vp+BossMath.Rotate(vr,path[i+1]);var mid=(a+b)*.5f;var d=b-a;float len=d.Length();if(len<1e-4f)continue;
                var outN=BossMath.Rotate(vr,Vector3.Normalize(new Vector3(path[i].X,0,path[i].Z)+new Vector3(0,0,.001f)));
                // The piece's frame: y down the strip (its flow axis, sign -1: it runs from the lip downward), z out of the shell.
                var yAxis=-d/len;var zAxis=Vector3.Normalize(outN-yAxis*Vector3.Dot(outN,yAxis));var xAxis=Vector3.Cross(yAxis,zAxis);
                var q=BossMath.FromBasis(xAxis,yAxis,zAxis);float w=(wid[i]+wid[i+1])*.25f;float u=i/(float)(path.Length-1);
                AddMolten(mid,new Vector3(w,len*.5f+.02f,.03f),q,NativeIronworksKit.MoltenFlow,1,-1,.35f,.95f*spill-.25f*u,.72f*spill-.2f*u,.2f+.5f*u+.6f*rig.CoolLevel,1,1,1,.3f+i*.1f);
            }
            // The lamps: halos and soft beams (cold white), only while lit.
            float lamp=rig.LampLevel;
            if(lamp>.02f)for(int s=0;s<2;s++)
            {
                var p=s==0?rig.LampAtL:rig.LampAtR;var dir=s==0?rig.BeamL:rig.BeamR;Sprite(p+dir*.15f,.9f,ColdWhite*(1.4f*lamp),.95f);Sprite(p+dir*.2f,2.2f,ColdWhite*(.25f*lamp),.6f);
                // A soft beam: a few widening quads along it (camera facing streaks), fading out over 9 m.
                for(int k=0;k<5;k++){float d0=.4f+k*1.8f,d1=d0+1.8f;Streak(p+dir*d0,p+dir*d1,.4f+.45f*k,ColdWhite*(.07f*lamp*(1-k/5f)),1,1);}
            }
            float visor=rig.VisorLevel;if(visor>.02f&&!rig.Hidden[L.Visor])Sprite(P.Position[L.Visor],.32f,Cyan*(.55f*visor),.8f);
            // The gauges' faint cyan and the tongs' hot tips need no effect (they glow in the model). Phase 3: sparks falling from the seams.
            if(rig.SeamLevel>.05f){for(int k=0;k<3;k++){var sp=vp+BossMath.Rotate(vr,new Vector3((float)Math.Sin(k*2.1f+1)*1.9f,.2f+k*.6f,(float)Math.Cos(k*2.1f+1)*1.9f));Sparks(sp,new Vector3(0,-.2f,0),.6f,1.0f,8,1.1f,t,(float)Math.Floor(t*1.5f)/1.5f,.66f,20+k,.9f*rig.SeamLevel);}
                AddHeat(vp+new Vector3(0,1.2f,0),4.2f,4.6f,.35f*rig.SeamLevel);}
            // The tuyère grate's glow when it blows (white, never orange), its steam jets at the trunnion seals.
            if(rig.BlowLevel>.3f){Sprite(rig.GrateAt+rig.GrateAxis*.15f,1.8f*rig.BlowLevel,White*(.6f*rig.BlowLevel),.9f);}
            float steam=ch[Ch.Steam];if(steam>.05f)for(int s=-1;s<=1;s+=2){var seal=vp+BossMath.Rotate(vr,new Vector3(s*2.2f,0,0));Puffs(seal,BossMath.Rotate(vr,new Vector3(s*3.2f,.6f,0)),12,.8f,.6f,new Vector3(.9f,.92f,.95f),.5f*steam,t,(float)Math.Floor(t/.8f)*.8f,.8f,4+(s+1),true);}
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
            // over once it has climbed out.
            float open=BossMath.Smoother((a-(b.Hit-.04f))/.32f),wide=BossMath.Smoother((a-b.Hit)/Math.Max(.3f,b.Climb-b.Hit)),crust=BossMath.Smooth((a-b.Climb)/2.5f);
            if(open>0)for(int arm=0;arm<4;arm++)
            {
                var dir=arm==0?Vector3.UnitX:arm==1?-Vector3.UnitX:arm==2?Vector3.UnitZ:-Vector3.UnitZ;float len=2.95f*open,w=.1f+.38f*wide;
                var q=BossMath.FromBasis(Vector3.Cross(Vector3.UnitY,dir),Vector3.UnitY,dir);
                AddMolten(pit+dir*(len*.5f)+new Vector3(0,A.PitY+.01f,0),new Vector3(w,.02f,len*.5f),q,NativeIronworksKit.MoltenFlow,2,1,.6f,.86f-.3f*crust,.6f-.25f*crust,.3f+.45f*crust,1,1f,1,arm*.27f);
            }
            if(open>0&&a<b.Hit+.6f){Sprite(pit+new Vector3(0,.4f,0),9*open,White*(1.2f*(1-BossMath.Smooth((a-b.Hit)/.6f))),1);}
            // Steam rings at the rim on the hit, a gush of heat.
            if(a>b.Hit-.05f&&a<b.Hit+2.5f)
            {
                for(int i=0;i<N(20);i++){float ang=i*BossMath.Tau/20,rr=3.2f;var p=pit+new Vector3((float)Math.Cos(ang)*rr,.2f,(float)Math.Sin(ang)*rr);Puffs(p,new Vector3((float)Math.Cos(ang)*.6f,1.4f,(float)Math.Sin(ang)*.6f),1,1.8f,1.4f,new Vector3(.9f,.9f,.92f),.5f,a,b.Hit,.12f,50+i,true);}
                Sparks(pit+new Vector3(0,.1f,0),Vector3.UnitY,.7f,7,80,1.3f,a,b.Hit,.35f,60);
                AddHeat(pit+new Vector3(0,1.6f,0),6,3.4f,.8f*(1-BossMath.Smooth((a-b.Hit)/2.5f)));
            }
            // Slag sheeting off the emerging body: molten drips and dark sheets falling from the shell as it heaves and climbs.
            if(a>b.Heave-.2f&&a<b.Flare){var vp=rig.Shown.Position[M.Vessel];for(int k=0;k<4;k++){float ang=k*1.57f+.8f;Sparks(vp+new Vector3((float)Math.Cos(ang)*1.9f,.6f,(float)Math.Sin(ang)*1.9f),-Vector3.UnitY,.25f,1.2f,12,1.0f,a,b.Heave+k*.3f,b.Flare-b.Heave,70+k,.8f);}
                Puffs(vp+new Vector3(0,-.8f,0),new Vector3(0,1.2f,0),16,2.4f,2.2f,new Vector3(.06f,.055f,.05f),.5f,a,b.Heave,b.Flare-b.Heave,80);}
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
                    if(age<hit){int leg=Math.Max(0,Math.Min(3,v.variant));Sprite(rig.Foot(leg),1.1f,White*.6f*BossMath.Smooth(age/hit),.8f);}
                    float k=age-hit;if(k>=0&&k<1.4f){Sparks(land,Vector3.UnitY,.9f,5,40,.9f,age,hit,.05f,100);Puffs(land,new Vector3(0,.5f,0),18,1.4f,1.6f,new Vector3(.12f,.1f,.08f),.55f,age,hit,.12f,101);
                        for(int i=0;i<N(8);i++){float ang=i*BossMath.Tau/8+.3f;var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));float l=1.4f+.8f*H(seed,i+5);FloorQuad(Y(land+d*.4f-new Vector3(-d.Z,0,d.X)*.04f),Y(land+d*l),Y(land+d*l),Y(land+d*.4f+new Vector3(-d.Z,0,d.X)*.04f),new Vector3(.02f,.015f,.012f),.8f*(1-k/1.4f),0);}}
                    break;}
                case "grab":Grab(rig,v,age,hit,act,target,tk,t);break;
                case "slam":Slam(rig,v,age,hit,act,tk,t);break;
                case "deslag":Deslag(rig,v,age,hit,act,yaw,tk,t);break;
                case "call":{float k=age-hit;if(k>=0&&k<1.2f){float r=1+16*BossMath.Smooth(k/1.1f);
                        // The heat-haze shock rolling out (a faint warm ring of haze and a soft flash at the mouth), no telegraph: it is harmless.
                        for(int i=0;i<8;i++){float ang=i*BossMath.Tau/8;AddHeat(root+new Vector3((float)Math.Cos(ang)*r,1.5f,(float)Math.Sin(ang)*r),3,3,.6f*(1-k/1.2f));}
                        Sprite(rig.Mouth+rig.MouthAxis*.6f,5,White*(.8f*(1-k/1.2f)),.9f);Puffs(rig.Mouth,rig.MouthAxis*2.5f,10,1.4f,2.4f,new Vector3(.08f,.07f,.06f),.5f,age,hit,.3f,110);}
                    break;}
                case "charge":Charge(rig,v,age,hit,act,target,tk,t);break;
            }
        }
        private void Pour(IronworksBossRig rig,in IronworksBossView v,float age,float hit,in NativeIronworksBoss.Action act,float yaw,Vector3 target,float tk,float t)
        {
            var root=rig.Root;float aimYaw=BossMath.YawOf(target-root);if(float.IsNaN(aimYaw))aimYaw=yaw;
            if(age<hit+act.Strike)Wedge(root,aimYaw,26*BossMath.Deg,1.2f,8,tk,t);
            // The bubbling at the lip in the windup; the stream from the strike (the mouth's lip, a ballistic arc to the floor), its
            // sweep following the target with a lag (the rig's yaw: the body turns into it), thinning in the follow-through.
            if(age<hit){float k=BossMath.Smooth(age/hit);Sparks(rig.Mouth,rig.MouthAxis,.5f,2,16,.6f,age,0,hit,120,k);}
            float on=age<hit?0:age<hit+act.Strike?1:Math.Max(0,1-(age-hit-act.Strike)/act.Follow);
            if(on>0)
            {
                var lip=rig.Mouth+rig.MouthAxis*.2f;var dir=rig.MouthAxis;var v0=Vector3.Normalize(new Vector3(dir.X,Math.Max(-.3f,dir.Y),dir.Z))*2.2f;
                float reach=BossMath.Smooth((age-hit)/.25f);Vector3 last=lip;const int seg=10;float total=0;
                for(int i=1;i<=seg;i++)
                {
                    float s=i/(float)seg*1.1f*reach;var p=lip+v0*s+new Vector3(0,-4.9f*s*s,0);if(p.Y<0){float a=v0.Y,bq=-4.9f;float tt=(-a-(float)Math.Sqrt(Math.Max(0,a*a-4*bq*lip.Y)))/(2*bq);p=lip+v0*tt+new Vector3(0,-4.9f*tt*tt,0);p.Y=0;}
                    var mid=(last+p)*.5f;var d=p-last;float len=d.Length();if(len<.02f){last=p;continue;}total+=len;
                    var yAx=-d/len;var toCam=Vector3.Normalize(new Vector3(ex,ey,ez)-mid);var zAx=Vector3.Normalize(toCam-yAx*Vector3.Dot(toCam,yAx));var xAx=Vector3.Cross(yAx,zAx);
                    float w=(.26f-.08f*i/seg)*(.6f+.4f*on);AddMolten(mid,new Vector3(w,len*.5f+.03f,w*.8f),BossMath.FromBasis(xAx,yAx,zAx),NativeIronworksKit.MoltenStream,1,-1,3.2f,1f,.9f,0,1,1.3f*on,1,i*.13f);
                    AddHeat(mid,1.4f,2.2f,.45f*on);last=p;if(p.Y<=0)break;
                }
                // The splash where it lands: sparks, a white flare, the pool spreading.
                if(last.Y<.2f){Sparks(last,Vector3.UnitY,.8f,4.5f,40,.8f,age,hit,act.Strike,121,on);Sprite(last+new Vector3(0,.3f,0),2.4f,White*(.7f*on),.9f);AddLight(last+new Vector3(0,.8f,0),10,new Vector3(3.4f,1.9f,.8f)*on);splashAt=last;}
            }
        }
        private Vector3 splashAt=new Vector3(float.NaN);
        // The pool left in the wedge's far half: spreading through the strike, burning 4 s while it crusts (the leftovers' builder).
        private void Blow(IronworksBossRig rig,in IronworksBossView v,float age,float hit,in NativeIronworksBoss.Action act,float tk,float t)
        {
            RainLayout(rig,v);
            // The circles fade in over the windup, stay until each droplet lands; the clots' circles are bigger.
            float rainStart=hit+2.0f,rainEnd=hit+act.Strike;
            for(int i=0;i<RainCount;i++)
            {
                float x=Rain[i*4],z=Rain[i*4+1],r=Rain[i*4+2],land=rainStart+Rain[i*4+3];if(age>land+.05f)continue;
                float k=age<hit?BossMath.Smooth(age/hit)*tk:.85f;Ring(x,z,r,k,t);
                // The droplet falling from under the roof onto its circle in the last .9 s.
                float f=age-(land-.9f);if(f>=0){var top=new Vector3(x+(H(seed,i*3)-.5f)*1.5f,13.5f,z+(H(seed,i*3+1)-.5f)*1.5f);var bot=new Vector3(x,0,z);float u=BossMath.Clamp01(f/.9f);u=u*u;var p=Vector3.Lerp(top,bot,u);
                    Streak(p,p+new Vector3(0,1.2f,0),.12f,new Vector3(3.4f,2.0f,.7f),1,1);Sprite(p,.35f,White*.8f,.9f);}
            }
            Column(rig,age-hit,age,t,true,v);
        }
        /// <summary>The blow's column for a time `k` from its hit (the inhale before it): fire and sparks to the roof, a smoke ring under
        /// it, sparks off the crane hook; the rain's splashes when `rain`.</summary>
        private void Column(IronworksBossRig rig,float k,float age,float t,bool rain,in IronworksBossView v)
        {
            var mouth=rig.Mouth;var axis=rig.MouthAxis;
            if(k<0){float inh=BossMath.Smooth((k+.75f)/.75f);Sprite(rig.GrateAt,2.2f*inh,White*(.8f*inh),.9f);return;}
            float on=k<.25f?BossMath.Smooth(k/.25f):k<1.75f?1:Math.Max(0,1-(k-1.75f)/.35f);if(on<=0&&!rain)return;
            if(on>0)
            {
                float top=A.Roof-1.2f;int n=N(22);
                for(int i=0;i<n;i++)
                {
                    // Stacked fire sprites rising up the column, white at its root, orange up it, flickering.
                    float u=BossMath.Frac(i/(float)n+t*1.6f+H(seed,i)*.2f);var p=mouth+axis*.4f+Vector3.UnitY*((top-mouth.Y)*u)+new Vector3((H(seed,i+40)-.5f)*1.2f*u,0,(H(seed,i+80)-.5f)*1.2f*u);
                    var c=Vector3.Lerp(White*2.2f,MoltenC*1.6f,BossMath.Smooth(u*1.4f));Sprite(p,(1.4f+3.2f*u)*(.8f+.4f*H(seed,i+120)),c*on*(1-.55f*u),.85f);
                }
                Sprite(mouth+axis*.6f,4.5f,White*(1.6f*on),1);
                for(int i=0;i<N(70);i++)
                {
                    float life=1.1f,born=(float)Math.Floor((t-H(seed,i+300)*life)/life)*life+H(seed,i+300)*life,a2=t-born;if(a2<0||a2>life)continue;
                    var v0=new Vector3((H(seed,i+400)-.5f)*3,9+10*H(seed,i+500),(H(seed,i+600)-.5f)*3);var p=mouth+v0*a2+new Vector3(0,-2.5f*a2*a2,0);if(p.Y>A.Roof-.3f)continue;
                    Streak(p,p-v0*.04f,.04f,new Vector3(3.4f,1.7f,.5f)*on*(1-a2/life),1,1);SparkCount++;
                }
                // The smoke ring rolling under the roof, the sparks thrown off the crane hook.
                Puffs(new Vector3(mouth.X,A.Roof-1.6f,mouth.Z),new Vector3(0,.1f,0),16,2.6f,3.6f,new Vector3(.06f,.055f,.05f),.55f*on,age,age-k,1.8f,140);
                Sparks(new Vector3(A.HookX,A.HookTip,A.HookZ),-Vector3.UnitY,.9f,3,24,1.2f,age,age-k,1.6f,141,on);
                AddHeat(mouth+Vector3.UnitY*4,3.4f,8,.9f*on);AddLight(mouth+Vector3.UnitY*2.5f,16,new Vector3(5.5f,3.6f,1.8f)*on);
            }
            if(rain&&k>2.0f)
            {
                // Each droplet's splash where it landed (1 s): sparks, a small molten splash.
                for(int i=0;i<RainCount;i++)
                {
                    float land=2.0f+Rain[i*4+3],s=k-land;if(s<0||s>1.0f)continue;var p=new Vector3(Rain[i*4],0,Rain[i*4+1]);float r=Rain[i*4+2];
                    if(s<.25f)Sparks(p,Vector3.UnitY,.9f,3.2f,10,.6f,k,land,.05f,160+i);
                    AddMolten(p+new Vector3(0,.02f,0),new Vector3(r*.55f,.02f,r*.55f),BossMath.Yaw(H(seed,i)*6),NativeIronworksKit.MoltenFlow,0,1,.2f,.9f*(1-s),.7f*(1-s),.25f+.6f*s,1,1,1,i*.31f);
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
            if(age<hit+act.Strike){Dashes(tip,target,tk,t);Ring(target.X,target.Z,1.2f,1,t,true,.12f,Cyan*tk);Sprite(tip,.9f,White*(.7f*BossMath.Smooth(age/hit)),.9f);}
            float k=age-hit;if(k>=0&&k<.35f){Sparks(tip,Vector3.UnitY,.8f,3,24,.5f,age,hit,.06f,180);Sprite(tip,1.6f,White*(1.2f*(1-k/.35f)),1);}
            // Held: the hinge glows when allies hit it (the rig's flinch time), sparks as the jaws grind; freed early: the jaws spring.
            bool held=k>=0&&k<act.Strike+act.Follow-.6f&&v.targetSeat>=0;if(held){Sparks(tip,Vector3.UnitY,.6f,1.5f,6,.5f,age,hit,act.Follow,181,.6f);if(BossAt(rig.LastHit)&&t-rig.LastHit<.3f)Sprite(tip,1.0f,White*(1.4f*(1-(t-rig.LastHit)/.3f)),1);}
            // The throw's whoosh trail behind the tongs as they fling.
            float th=age-(hit+act.Strike+act.Follow-.6f);if(th>0&&th<.5f)for(int i=0;i<6;i++){float a2=th-i*.03f;if(a2<0)break;Sprite(tip+new Vector3(0,-.2f*i,0),.6f,Cyan*(.25f*(1-th/.5f)),.5f);}
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
                    Quads.Add(p0.X,p0.Y,p0.Z,p1.X,p1.Y,p1.Z,p1.X,.6f,p1.Z,p0.X,.6f,p0.Z,White.X*2.4f*fade,White.Y*2.4f*fade,White.Z*2.4f*fade,.85f,0,true,1);Telegraphs++;
                    // The dust wall riding behind it.
                    if(i%3==0)Sprite(new Vector3(p0.X,.6f,p0.Z),1.6f+.5f*u,new Vector3(.14f,.12f,.1f),.35f*(1-u),false,1);
                }
            }
            // The tongs' impact: sparks, cracks of dust; the metal wave splashing ahead; the hook's sparks; the hall's dust sifting down.
            for(int s=0;s<2;s++){var f=rig.Foot(s);if(k<1.2f){Sparks(f,Vector3.UnitY,.9f,5,30,.9f,age,hit,.05f,200+s);Puffs(f,new Vector3(0,.5f,0),12,1.4f,1.8f,new Vector3(.12f,.1f,.08f),.55f,age,hit,.1f,202+s);}}
            if(k<1.6f){Sparks(rig.Mouth,BossMath.Forward(rig.Yaw)+Vector3.UnitY*.6f,.5f,5,30,1.1f,age,hit+.15f,.3f,205);Puffs(new Vector3(c.X,A.Roof-2,c.Z),new Vector3(0,-1.2f,0),18,2.8f,2.2f,new Vector3(.18f,.16f,.14f),.25f,age,hit+.1f,.8f,206);}
        }
        private void Deslag(IronworksBossRig rig,in IronworksBossView v,float age,float hit,in NativeIronworksBoss.Action act,float yaw,float tk,float t)
        {
            var c=rig.Root;var back=-BossMath.Forward(yaw);var right=BossMath.Right(yaw);
            for(int i=0;i<3;i++)
            {
                // The three landing points, 7 to 9 m behind (fixed offsets from the locked facing).
                float dist=7+i,side=(i-1)*2.6f;var land=c+back*dist+right*side;land.X=BossMath.Clamp(land.X,A.HallX0+1,A.HallX1-1);land.Z=BossMath.Clamp(land.Z,A.HallZ0+1,A.HallZ1-1);
                Lumps[i*3]=land.X;Lumps[i*3+1]=land.Z;Lumps[i*3+2]=hit+i*.4f+.9f;
                float launch=hit+i*.4f,landAt=launch+.9f;if(age<landAt+.05f)Ring(land.X,land.Z,1.4f,age<hit?tk:.85f,t);
                // The lump: lobbed out of the mouth on a smoke trail, tumbling, landing on its circle.
                float f=age-launch;if(f>=0&&f<.9f)
                {
                    var from=rig.Mouth;float u=f/.9f;var p=Vector3.Lerp(from,land,u)+new Vector3(0,4.2f*4*u*(1-u),0);var rot=BossMath.Euler(f*7,f*4,f*5);
                    AddMolten(p,new Vector3(.32f,.28f,.3f),rot,NativeIronworksKit.MoltenSlag,0,1,.3f,.8f,.6f,.5f,1,1.2f,1,i*.4f);
                    for(int k=1;k<8;k++){float uu=Math.Max(0,(f-k*.05f)/.9f);var q=Vector3.Lerp(from,land,uu)+new Vector3(0,4.2f*4*uu*(1-uu),0);Sprite(q,.6f+.12f*k,new Vector3(.07f,.065f,.06f),.45f*(1-k/8f),false);}
                }
                if(f>=.9f&&f<1.6f){Sparks(land,Vector3.UnitY,.9f,3.6f,24,.7f,age,landAt,.05f,220+i);Puffs(land,new Vector3(0,.8f,0),8,1.3f,1.4f,new Vector3(.07f,.065f,.06f),.5f,age,landAt,.1f,224+i);}
            }
        }
        private void Charge(IronworksBossRig rig,in IronworksBossView v,float age,float hit,in NativeIronworksBoss.Action act,Vector3 target,float tk,float t)
        {
            var start=rig.Root;if(age>=hit&&BossAt(chargeFrom.X))start=chargeFrom;else chargeFrom=rig.Root;
            // The cyan dashed path and its two edge lines over the windup and the run.
            if(age<hit+act.Strike){var d=new Vector3(target.X-start.X,0,target.Z-start.Z);float len=d.Length();if(len>.5f){d/=len;var side=new Vector3(-d.Z,0,d.X)*2.7f;Dashes(start,target,tk,t,.2f);Dashes(start+side,target+side,tk*.75f,t,.1f,.3f);Dashes(start-side,target-side,tk*.75f,t,.1f,.3f);}}
            // The bogies dig back throwing sparks; the gallop's sparks under every foot that lands; the trail; the skid.
            if(age<hit)for(int s=2;s<4;s++)Sparks(rig.Foot(s),-BossMath.Forward(rig.Yaw)+Vector3.UnitY*.5f,.5f,3.5f,14,.5f,age,0,hit,240+s,BossMath.Smooth(age/hit));
            for(int leg=0;leg<4;leg++)if((rig.Landed&(1<<leg))!=0)landSpark[leg]=age;
            for(int leg=0;leg<4;leg++){float k=age-landSpark[leg];if(k>=0&&k<.5f)Sparks(rig.Foot(leg),Vector3.UnitY,.8f,3,10,.5f,age,landSpark[leg],.04f,250+leg);}
            float sk=age-(hit+act.Strike);if(sk>=0&&sk<act.Follow){for(int s=0;s<2;s++)Sparks(rig.Foot(s),BossMath.Forward(rig.Yaw)+Vector3.UnitY*.4f,.4f,4.5f,30,.6f,age,hit+act.Strike,act.Follow,256+s);}
        }
        private Vector3 chargeFrom=new Vector3(float.NaN);private readonly float[] landSpark={-9,-9,-9,-9};

        // ================================================================================================================
        // the leftovers: the pour's pool, the slam's wave pool, the deslag's slag patches, the charge's trail (presentation's
        // timing: from the action's start; Astra's ledger owns the burns)
        // ================================================================================================================
        private void Leftovers(IronworksBossRig rig,in IronworksBossView v,float t)
        {
            if(!BossAt(v.actionAt)||v.action==null)return;var a=NativeIronworksBoss.Find(v.action);float age=t-v.actionAt,hit=a.Windup;
            switch(v.action)
            {
                case "pour":{float k=age-hit;if(k<0||k>a.Strike+a.Follow+4)break;var p=BossAt(splashAt.X)?splashAt:rig.Root+BossMath.Forward(rig.Yaw)*5.5f;float spread=BossMath.Smooth(k/a.Strike),cool=BossMath.Clamp01((k-a.Strike)/4);
                    AddMolten(p+new Vector3(0,.025f,0),new Vector3(1.6f*spread+.2f,.025f,1.25f*spread+.2f),BossMath.Yaw(rig.Yaw),NativeIronworksKit.MoltenFlow,2,1,.25f,.95f-.6f*cool,.75f-.5f*cool,.15f+.75f*cool,1,1,1,.7f);
                    if(cool<1)AddHeat(p+new Vector3(0,.8f,0),3.4f,2,.6f*(1-cool));break;}
                case "slam":{float k=age-hit-a.Strike;if(k<0||k>a.Follow+3)break;var p=rig.Root+BossMath.Forward(rig.Yaw)*5.2f;float spread=BossMath.Smooth(k/.4f),cool=BossMath.Clamp01((k-.4f)/3);
                    AddMolten(p+new Vector3(0,.025f,0),new Vector3(2.4f*spread+.1f,.025f,1.2f*spread+.1f),BossMath.Yaw(rig.Yaw),NativeIronworksKit.MoltenFlow,2,1,.5f,.9f-.6f*cool,.7f-.5f*cool,.2f+.7f*cool,1,1,1,.9f);
                    if(k<.6f){Sparks(p,Vector3.UnitY+BossMath.Forward(rig.Yaw),.7f,6,40,.8f,age,hit+a.Strike,.1f,270);}break;}
                case "deslag":for(int i=0;i<3;i++){float landAt=hit+i*.4f+.9f,k=age-landAt;if(k<0||k>6)continue;float cool=BossMath.Clamp01(k/6);var p=new Vector3(Lumps[i*3],0,Lumps[i*3+1]);if(!(Lumps[i*3]!=0||Lumps[i*3+1]!=0))continue;
                        AddMolten(p+new Vector3(0,.03f,0),new Vector3(1.1f,.03f,1.0f),BossMath.Yaw(i*1.3f),NativeIronworksKit.MoltenSlag,0,1,.15f,.75f-.45f*cool,.55f-.35f*cool,.45f+.45f*cool,1,1,1,i*.5f);}break;
                case "charge":{float k=age-hit;if(k<0||k>a.Strike+a.Follow+3)break;var from=BossAt(chargeFrom.X)?chargeFrom:rig.Root;var to=rig.Root;var d=to-from;d.Y=0;float len=d.Length();if(len<.5f)break;d/=len;int n=Math.Min(8,(int)(len/1.6f));
                        for(int i=0;i<n;i++){float u=(i+.5f)/n,born=hit+a.Strike*u,age2=age-born;if(age2<0)continue;float cool=BossMath.Clamp01(age2/3);var p=from+d*(len*u);
                            AddMolten(p+new Vector3(0,.022f,0),new Vector3(.45f,.022f,len/n*.5f),BossMath.Yaw(BossMath.YawOf(d)),NativeIronworksKit.MoltenFlow,2,1,.3f,.85f-.6f*cool,.65f-.45f*cool,.2f+.7f*cool,1,1,1,i*.2f);}break;}
            }
        }

        // ================================================================================================================
        // phase changes, moments, the death, the hall's vessels, the lights
        // ================================================================================================================
        private void PhaseFx(IronworksBossRig rig,in IronworksBossView v,float t)
        {
            // Crust chunks flying: glassy shards and a cream flash where each leaves, dust where it lands.
            for(int k=0;k<12;k++)
            {
                float at=rig.ChunkAt[k];if(!BossAt(at))continue;float s=t-at;if(s<0||s>2.2f)continue;var j=L.Crust(k/3,k%3);var p=rig.Shown.Position[j]+BossMath.Rotate(rig.Shown.Rotation[j],L.ChunkCentre[k]);
                if(s<.25f)Sprite(p,2.2f,Cream(1.4f*(1-s/.25f)),1);
                for(int i=0;i<N(10);i++){float a2=s-.02f*i;if(a2<0||a2>1.2f)continue;var v0=new Vector3(H(seed,k*31+i)-.5f,.8f+H(seed,k*31+i+1),H(seed,k*31+i+2)-.5f)*4;var q=p+v0*a2+new Vector3(0,-4.9f*a2*a2,0);if(q.Y<.03f)continue;
                    var col=new Vector3(.25f,.24f,.3f);Streak(q,q-v0*.02f,.07f,col,.9f*(1-a2/1.2f),0);}
                if(s>.5f&&s<1.6f&&p.Y<1.2f)Puffs(p,new Vector3(0,.5f,0),5,1.2f,1.4f,new Vector3(.1f,.09f,.08f),.4f,s,.5f,.3f,300+k);
            }
            // The break: the arch cracking (sparks), the pulpit's chains (drawn by the runtime as links), the seams' flash, the pit welling.
            if(BossAt(rig.PulpitAt)){float s=t-rig.PulpitAt;if(s>=0&&s<1.5f)Sparks(rig.TearTop[0]*.5f+rig.TearTop[1]*.5f,Vector3.UnitY,.9f,4,40,.9f,t,rig.PulpitAt,.2f,320);}
            if(v.Phase>=3&&BossAt(v.phaseAt)&&float.IsNaN(rig.DeathAge))
            {
                float s=t-v.phaseAt;if(s>=1.0f&&s<2.0f)Sprite(rig.Shown.Position[M.Vessel]+new Vector3(0,1,0),7,White*(1.1f*(1-(s-1)/1)),1);
                // The pit wells up white and spills to its rim as the break lands (inside the pit: the overflow cycles are the hazard's).
                float well=BossMath.Smooth((s-1.2f)/1.4f)*(1-.5f*BossMath.Smooth((s-6)/4));if(well>0){AddMolten(new Vector3(A.PitX,A.PitY+.02f,A.PitZ),new Vector3(2.95f,.02f+.02f*well,2.95f),Quaternion.Identity,NativeIronworksKit.MoltenFlow,0,1,.4f,.55f+.4f*well,.45f+.35f*well,.6f-.4f*well,1,1,1,.55f);
                    AddLight(new Vector3(A.PitX,.9f,A.PitZ),12,new Vector3(3.2f,1.7f,.6f)*well);if(s<3.6f)Sparks(new Vector3(A.PitX,.1f,A.PitZ),Vector3.UnitY,1.1f,4,30,1,t,v.phaseAt+1.2f,2.2f,330,well);}
            }
        }
        private void MomentFx(IronworksBossRig rig,in IronworksBossView v,float t)
        {
            if(rig.MomentIndex<0)return;var a=NativeIronworksBoss.Actions[rig.MomentIndex];float age=rig.MomentAct.Age;
            switch(a.Id)
            {
                case "choke":Puffs(rig.Mouth,rig.MouthAxis*3+Vector3.UnitY,24,2.4f,2.2f,new Vector3(.03f,.028f,.026f),.75f,age,0,.6f,400);Sprite(rig.GrateAt,1.6f,White*.5f,.7f);break;
                case "kneel":for(int leg=0;leg<2;leg++){var knee=rig.Shown.Position[M.LegJoint(leg,M.Tibia)];Puffs(knee,new Vector3(0,1.2f,0),18,1.4f,1.2f,new Vector3(.9f,.9f,.92f),.6f,age,leg*.15f,1.2f,410+leg,true);}
                    if(age>.4f&&age<2.4f){var p=rig.Mouth;p.Y=0;p+=BossMath.Forward(rig.Yaw)*1.5f;AddMolten(p+new Vector3(0,.02f,0),new Vector3(.8f,.02f,.7f),BossMath.Yaw(rig.Yaw),NativeIronworksKit.MoltenFlow,0,1,.2f,.85f,.6f,.3f,1,1,1,.4f);}break;
                case "crash":if(age<1.2f){Sparks(rig.Mouth,-BossMath.Forward(rig.Yaw)+Vector3.UnitY,.9f,6,60,1.0f,age,0,.15f,420);Puffs(rig.Root+BossMath.Forward(rig.Yaw)*4,new Vector3(0,1,0),20,2,2.4f,new Vector3(.14f,.12f,.1f),.5f,age,0,.4f,421);}break;
            }
        }
        private void Death(IronworksBossRig rig,in IronworksBossView v,float d,float t)
        {
            var vp=rig.Shown.Position[M.Vessel];
            // The last pour into the pit: a thinning stream, then drips (from 1 s), a pool spreading and cooling.
            float pour=d<1.0f?0:d<2.6f?1-.6f*BossMath.Smooth((d-1.0f)/1.6f):Math.Max(0,.4f-(d-2.6f)*.4f);
            if(pour>0)
            {
                var lip=rig.Mouth+rig.MouthAxis*.2f;var land=new Vector3(lip.X,0,lip.Z)+BossMath.Forward(rig.Yaw)*.6f;var mid=(lip+land)*.5f;float len=Vector3.Distance(lip,land);
                var yAx=Vector3.Normalize(land-lip)*-1;var toCam=Vector3.Normalize(new Vector3(ex,ey,ez)-mid);var zAx=Vector3.Normalize(toCam-yAx*Vector3.Dot(toCam,yAx));
                AddMolten(mid,new Vector3(.16f*pour+.03f,len*.5f,.12f*pour+.03f),BossMath.FromBasis(Vector3.Cross(yAx,zAx),yAx,zAx),NativeIronworksKit.MoltenStream,1,-1,2.6f,.95f,.85f,0,1,1.1f*pour,1,.5f);
                AddLight(land+new Vector3(0,.8f,0),9,new Vector3(3,1.6f,.6f)*pour);
            }
            float k=d-1.0f;if(k>0){float cool=BossMath.Clamp01((d-1.6f)/2.6f);var p=new Vector3(rig.Mouth.X,0,rig.Mouth.Z)+BossMath.Forward(rig.Yaw)*.6f;AddMolten(p+new Vector3(0,.025f,0),new Vector3(.4f+1.2f*BossMath.Smooth(k/1.6f),.025f,.4f+1.0f*BossMath.Smooth(k/1.6f)),BossMath.Yaw(rig.Yaw),NativeIronworksKit.MoltenFlow,0,1,.15f,.9f-.7f*cool,.7f-.55f*cool,.2f+.75f*cool,1,1,1,.6f);}
            // Steam columns rising off the cooling shell; the whistle's steam; the hall's vessels exhale (the hall's own builder).
            float steam=BossMath.Smooth((d-1.4f)/1)*(1-BossMath.Smooth((d-9)/4));
            if(steam>0)for(int c=0;c<3;c++)Puffs(vp+new Vector3((c-1)*1.2f,.8f,(c%2)*.6f-.3f),new Vector3(.1f,1.6f,0),10,2.6f,1.8f,new Vector3(.85f,.86f,.88f),.42f*steam,t,(float)Math.Floor(t/2.6f)*2.6f,2.6f,500+c,true);
            var stage=NativeIronworksBossStage.Evaluate(v,t,d,rig);if(stage.Whistle>0){var w=rig.WhistleAt;Puffs(w,new Vector3(.2f,2.4f,-.3f),26,1.6f,.9f,new Vector3(.95f,.95f,.97f),.75f*stage.Whistle,t,(float)Math.Floor(t/1.6f)*1.6f,1.6f,510,true);Sprite(w+new Vector3(0,.3f,0),.8f,new Vector3(1,1,1)*.5f*stage.Whistle,.6f);}
        }
        private void HallVessels(in IronworksBossView v,float t,IronworksBossRig rig)
        {
            var s=NativeIronworksBossStage.Evaluate(v,t,rig.DeathAge,rig);
            for(int i=0;i<2;i++)
            {
                float wake=i==0?s.WakeA:s.WakeB;var mouth=new Vector3(A.VesselX,A.VesselY,A.VesselMouthZ(i));
                if(wake>0){Sprite(mouth,3.2f+2*wake,White*(1.2f*wake),.95f);Sprite(mouth+new Vector3(.6f,.5f,0),6,MoltenC*(.4f*wake),.8f);Sparks(mouth,new Vector3(.7f,1,0),.6f,4,30,1.0f,t,t-.01f-(1-wake)*2.6f,.4f,600+i,wake);
                    Puffs(mouth,new Vector3(.4f,1.4f,0),10,2.2f,2,new Vector3(.08f,.07f,.06f),.5f*wake,t,(float)Math.Floor(t/2.2f)*2.2f,2.2f,610+i);if(LightCount<LightMax&&i==0||wake>.5f)AddLight(mouth+new Vector3(.8f,.4f,0),13,new Vector3(4.2f,2.8f,1.4f)*wake);}
                if(s.Exhale>0)Puffs(mouth,new Vector3(.3f,1.2f,0),14,2.6f,2.2f,new Vector3(.86f,.87f,.9f),.5f*s.Exhale,t,(float)Math.Floor(t/2.6f)*2.6f,2.6f,620+i,true);
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
