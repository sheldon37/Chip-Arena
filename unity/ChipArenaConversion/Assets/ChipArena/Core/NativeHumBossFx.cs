using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE HUM's effects (docs/EXPEDITION-BOSS-BACKROOMS.md section 7), functions of the record and the clock: camera-facing and floor quads into a <see cref="NativeFxQuads"/> (the telegraphs, the lanes, the scorches, the light on its fixture, the ring), particles into
    /// <see cref="Vfx"/> (the shared effects renderer: glass rain on real arcs, sparks, ceiling dust, drips, tiles), the surfaces' real geometry into <see cref="Surface"/> (the tears, the bulges, Fall Through's hole, the textures' slip), each lit part's level, and the boss's own lights for the room's slots.
    /// Telegraphs follow the game's danger language (amber warning, red strike) on top of the room's own: the rows, the hum, the lamps; they're never thinned by the effects quality. Pure: the same rig, view, clock and camera give the same frame on every peer. Allocation free.
    /// </summary>
    public sealed class NativeHumBossFx
    {
        public const int Capacity=2400,SortMax=320,MaxLights=4;
        public const int WarnHex=0xFFB020,DangerHex=0xFF2A1A;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public readonly NativeBossVfx Vfx=new NativeBossVfx(2600);
        public readonly NativeHumSurface Surface=new NativeHumSurface();
        /// <summary>Each lit part's level: the seven tubes, the head's two tubes, the diffuser's halves, the Ballast (1 as modelled, 0 dark).</summary>
        public readonly float[] Tube=new float[NativeHumBossLook.Tubes];public float HeadTubes,DiffuserU,DiffuserL,Ballast;
        /// <summary>The boss's lights (x, y, z, range; linear r, g, b).</summary>
        public readonly float[] LightPos=new float[MaxLights*4],LightRgb=new float[MaxLights*3];public int LightCount;
        /// <summary>The DEV preview's room (null: the record alone; nothing that needs walls draws).</summary>
        public HumRoom Room;
        /// <summary>This frame's counts (budget lines, checks).</summary>
        public int Telegraph,Bursts,Tears;
        private Vector3 eye,camR,camU;private float quality;private bool low,reduced;private uint vseed;private float floorY;
        private readonly double[] pc=new double[SortMax*12],pk=new double[SortMax];private readonly float[] pcol=new float[SortMax*4];private readonly int[] pidx=new int[SortMax];private readonly byte[] pmode=new byte[SortMax];private int pn;
        // Footprints: where it stepped, ring buffer filled from the rig's landings.
        private const int Prints=28;private readonly Vector3[] printAt=new Vector3[Prints];private readonly float[] printT=new float[Prints],printYaw=new float[Prints];private readonly bool[] printHand=new bool[Prints];private int printNext;
        private static float R(int hex)=>((hex>>16)&255)/255f;private static float G(int hex)=>((hex>>8)&255)/255f;private static float B(int hex)=>(hex&255)/255f;
        private static float H(int a,int b){unchecked{uint x=(uint)a*0x9E3779B9U^(uint)b*0x85EBCA6BU;x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return (x>>8)/16777216f;}}
        private static float Frac(float x)=>x-(float)Math.Floor(x);
        private static readonly Vector3 LampWarm=new Vector3(1.0f,.95f,.72f),GlassC=new Vector3(.72f,.86f,.86f),TileC=new Vector3(.36f,.34f,.28f),DustC=new Vector3(.32f,.3f,.2f),Amber=new Vector3(1f,.55f,.14f),Soot=new Vector3(.02f,.018f,.015f);

        public void Build(HumBossRig rig,in HumBossView v,HumBossStage st,float t,double eyeX,double eyeY,double eyeZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,float effects,bool lowTier,bool reducedEffects)
        {
            Quads.Clear();pn=0;LightCount=0;Telegraph=Bursts=Tears=0;Vfx.Begin(effects,lowTier);Surface.Begin();
            for(int i=0;i<Tube.Length;i++)Tube[i]=1;HeadTubes=DiffuserU=DiffuserL=Ballast=1;
            if(rig==null||!rig.Visible||!v.live)return;
            eye=new Vector3((float)eyeX,(float)eyeY,(float)eyeZ);camR=new Vector3((float)rightX,(float)rightY,(float)rightZ);camU=new Vector3((float)upX,(float)upY,(float)upZ);quality=effects<0?0:effects>1?1:effects;low=lowTier;reduced=reducedEffects;
            vseed=0x4A1u^(uint)(HumBossView.At(v.spawned)?(int)(v.spawned*16):0);floorY=Room!=null?Room.Origin.Y:v.y;
            Levels(rig,v,st,t);
            // Telegraphs first (never thinned), then the rest.
            Rows(v,st,t);Lane(rig,v,t);Stoop(rig,v,t);ReachArc(rig,v,t);CutArc(rig,v,t);FallThrough(rig,v,t);Ring(rig,v,t);
            HeadGlow(rig,v,st,t);TubesFx(rig,v,t);BallastFx(rig,v,t);Arrival(rig,v,st,t);WallHand(rig,v,t);Voices(rig,v,t);PhaseFx(rig,v,st,t);Steps(rig,v,t);Drips(rig,v,t);Slip(rig,v,st,t);Clip(rig,v,t);Death(rig,v,t);Lights(rig,v,st,t);
            Flush();
        }
        // ---------------- the lit parts' levels ----------------
        private void Levels(HumBossRig rig,in HumBossView v,HumBossStage st,float t)
        {
            var beats=HumBossBeats.Current;float a=rig.Arriving?rig.ArrivalAge:float.PositiveInfinity;bool dead=v.Dead(t);float d=dead?t-v.diedAt:-1;
            float hit=HumBossView.At(rig.LastHit)?t-rig.LastHit:99;float flick=hit>=0&&hit<.3f&&!reduced?1-.6f*Math.Abs((float)Math.Sin(hit*43)):1;
            float charge=rig.ActMove>=0?ChargeOf(rig,v):0;bool silent=st.Silent>.5f&&!dead;
            for(int i=0;i<Tube.Length;i++)
            {
                float k;
                // Each tube's own buzz: a slow ripple along the spine and a failing moment now and then.
                float buzz=reduced?1:.84f+.16f*(float)Math.Sin(t*(11+i*1.3f)+i*2.1f);int slot=(int)Math.Floor(t*6.5f);if(!reduced&&H(i+40,slot)<.045f)buzz*=.3f;
                k=buzz*(1+1.1f*charge);
                if(a<beats.Hold){float s=a-beats.Tube(i);k=s<0?0:s<.2f?(reduced?.5f:(Frac(s*17)<.45f?.9f:.04f)):s<.34f?1.6f-(s-.2f)*3f:buzz;}
                if(v.TubeBroken(i))k=0;
                if(silent)k=0;
                if(dead){float pop=.18f+(Tube.Length-1-i)*.17f;k=d<pop?k:0;}
                Tube[i]=k*flick;
            }
            float head=1.3f+1.6f*charge;
            if(a<beats.Hold)
            {
                if(a<beats.HeadFlicker)head=0;
                else if(a<beats.HeadCatch){float s=(a-beats.HeadFlicker)/(beats.HeadCatch-beats.HeadFlicker);head=reduced?.8f*s:(H(77,(int)Math.Floor(a*11))<.3f+.6f*s?.45f+1.1f*s:.03f);}
                else{float s=a-beats.HeadCatch;head=1.3f+1.3f*(float)Math.Exp(-s*2.6f)+.7f*BossMath.Smooth((a-beats.JawSplit)/.8f);}
            }
            if(v.HeadBroken)head*=.25f;if(silent)head=0;
            if(dead){head=d<3.05f?(reduced?1:(.45f+.55f*(H(78,(int)Math.Floor(t*9))<.72f?1:.1f))):0;if(d<1.6f)head=Math.Max(head,1.1f);}
            head*=flick;HeadTubes=head*1.15f;DiffuserU=DiffuserL=head;
            float bal=.85f+.55f*Math.Max(0,BossBreath.Sample(t,v.phase>=3?1.4f:1f,vseed^7u))+3.2f*charge;
            if(a<beats.Tube(0))bal=0;else if(a<beats.Hold)bal=Math.Min(bal,BossMath.Smooth((a-beats.Tube(0))/1.5f)*1.6f);
            if(silent)bal=.05f;if(dead)bal*=1-BossMath.Smooth(d/1.6f);Ballast=bal;
        }
        private static float ChargeOf(HumBossRig rig,in HumBossView v)
        {
            var m=NativeHumBoss.Moves[rig.ActMove];var a=rig.Act;float u=a.HitAt>0?a.Age/a.HitAt:1;
            if(m.Id=="pulse")return u<1?u*u:1-BossMath.Smooth((a.Age-a.HitAt-m.Strike*.5f)/(m.Strike*.8f));
            if(m.Id=="stoop"||m.Id=="reach"||m.Id=="row"||m.Id=="lunge")return (u<1?u*u:1-BossMath.Smooth((a.Age-a.HitAt)/.3f))*.42f;
            return 0;
        }
        // ---------------- Row Burst: a bright lane on the carpet before, a flash, glass rain on real arcs and sparks at the burst, a scorch after ----------------
        private void Rows(in HumBossView v,HumBossStage st,float t)
        {
            if(Room==null)return;RowsOf(v.rowAxis,v.rowMask,v.rowAt,st,t,1);RowsOf(v.row2Axis,v.row2Mask,v.row2At,st,t,2);
        }
        private void RowsOf(int axis,int mask,float at,HumBossStage st,float t,int key)
        {
            if(mask==0||!HumBossView.At(at))return;var m=NativeHumBoss.Move("row");float k=t-at;if(k<-m.Wind||k>HumBossStage.RowDark+1)return;
            float half=m.Width*.5f;
            for(int r=0;r<NativeHumArena.FixtureSide;r++)
            {
                if((mask>>r&1)==0)continue;float line=NativeHumArena.RowLine(Room,axis,r);
                var a=axis==0?new Vector3(Room.MinX+.3f,floorY,line):new Vector3(line,floorY,Room.MinZ+.3f);var b=axis==0?new Vector3(Room.MaxX-.3f,floorY,line):new Vector3(line,floorY,Room.MaxZ-.3f);
                if(k<0)
                {
                    // The lane's light on the carpet: the danger is lit: stand between them.
                    float u=(k+m.Wind)/m.Wind,pulse=reduced?1:.8f+.2f*(float)Math.Sin(t*19);int q0=Quads.Count;
                    StripFlat(a,b,floorY+.02f,half,R(WarnHex)*.32f*u*pulse,G(WarnHex)*.3f*u*pulse,B(WarnHex)*.2f*u*pulse,.85f);
                    StripFlat(a,b,floorY+.024f,half*.42f,.95f*u*u,.9f*u*u,.6f*u*u,.9f);
                    Telegraph+=Quads.Count-q0;
                }
                else if(k<.22f)
                {
                    float f=1-k/.22f;StripFlat(a,b,floorY+.03f,half*1.05f,R(DangerHex)*f,G(DangerHex)*.6f*f,B(DangerHex)*f,.95f);StripFlat(a,b,floorY+.033f,half*.5f,1.4f*f,1.3f*f,1.1f*f,.95f);Telegraph+=2;
                }
                // The scorch that stays till the lamps come back (a dark lane of burnt carpet under each fixture).
                if(k>=0&&k<HumBossStage.RowDark){float fade=(1-BossMath.Smooth((k-HumBossStage.RowDark+1.2f)/1.2f))*BossMath.Smooth(k/.05f);
                    for(int i=0;i<NativeHumArena.FixtureSide;i++){float along=(axis==0?Room.MinX:Room.MinZ)+(i*2+.5f)*BackroomsLayout.Cell;var c=axis==0?new Vector3(along,floorY+.018f,line):new Vector3(line,floorY+.018f,along);Scorch(c,axis==0?1:0,axis==0?1.4f:.5f,axis==0?.5f:1.4f,fade*.75f);}}
                // Each fixture's burst: glass in real arcs, sparks, a flash and dust from the grid.
                for(int i=0;i<NativeHumArena.FixtureSide;i++)
                {
                    int fx=axis==0?r*NativeHumArena.FixtureSide+i:i*NativeHumArena.FixtureSide+r;var f=Room.FixtureAt[fx];int seed=fx*7+key*131;
                    if(k>=-.02f&&k<.2f){float g=1-(k+.02f)/.22f;Vfx.Glow(f-new Vector3(0,.25f,0),LampWarm*(reduced?1.2f:3.4f)*g,3.4f,NativeBossVfx.Core);Bursts++;}
                    if(k>=0&&k<2.4f)
                    {
                        Vfx.SparkBurst(t,f,new Vector3(0,-1,0),.9f,4.2f,14,1.0f,at,.05f,vseed,seed,1.1f,.018f,.7f,true,floorY);
                        GlassRain(f,at,t,seed,i);
                    }
                    if(k>=0&&k<3.2f){Vfx.SmokeBurst(t,f+new Vector3(0,-.1f,0),new Vector3(0,-.7f,0),.7f,.05f,4,2.6f,.25f,.9f,DustC,.34f,at,.15f,vseed,seed+5,0,floorY);}
                    // A burnt out fixture spits: a thin last spark now and then.
                    if(k>.6f&&k<HumBossStage.RowDark){float ck=Frac(k*.8f+H(seed,3));if(ck<.08f){Vfx.Glow(f-new Vector3(0,.04f,0),LampWarm*(.9f*(1-ck/.08f)),.4f,NativeBossVfx.Core);}}
                }
            }
        }
        private void GlassRain(Vector3 f,float at,float t,int seed,int count)
        {
            float age=t-at;if(age<0||age>2.2f)return;int n=Vfx.Scale(18);
            for(int i=0;i<n;i++)
            {
                int k=seed*311+i*5;var dir=new Vector3((H(k,1)-.5f)*3.2f,-(.6f+1.8f*H(k,2)),(H(k,3)-.5f)*3.2f);
                var at2=f+new Vector3((H(k,4)-.5f)*1.5f,-.03f,(H(k,5)-.5f)*.36f);
                var p=NativeBossVfx.Bounce(at2,dir,NativeBossVfx.Gravity,floorY+.02f,.18f,.3f,age,out var vel,out bool landed,out _);
                float life=1.6f+.6f*H(k,6);if(age>life)continue;float fade=1-BossMath.Smooth((age-life*.7f)/(life*.3f));
                float glint=.5f+.5f*(float)Math.Sin(age*38+i);
                Vfx.Spark(p,vel,GlassC*(.7f+1.4f*glint)*fade,.012f+.012f*H(k,7),.05f+.05f*H(k,8),landed?.0f:.02f,.6f,age/life,i);
            }
        }
        // ---------------- Noclip: the lane marked, bulges running along the wallpaper, the grid's dust falling in sequence, tears where it crosses a surface ----------------
        private void Lane(HumBossRig rig,in HumBossView v,float t)
        {
            if(v.action!="lunge"||!HumBossView.At(v.actionAt)||!HumBossView.At(v.laneX0))return;var m=NativeHumBoss.Move("lunge");
            float hit=HumBossView.At(v.hitAt)?v.hitAt:v.actionAt+m.Wind;float a=t-v.actionAt,wind=hit-v.actionAt;
            var p0=new Vector3(v.laneX0,floorY,v.laneZ0);var p1=new Vector3(v.laneX1,floorY,v.laneZ1);var d=p1-p0;float len=d.Length();if(len<.1f)return;var dir=d/len;var side=new Vector3(dir.Z,0,-dir.X);
            float life=a<wind?BossMath.Smooth(a/.2f):1-BossMath.Smooth((a-wind-m.Strike*.6f)/.3f);
            if(life>0)
            {
                // Its width: amber edges filling toward the far end as it comes; red on the strike.
                float u=BossMath.Clamp01(a/wind);int q0=Quads.Count;float c=a<wind?1:.0f;
                float rr=a<wind?R(WarnHex):R(DangerHex),gg=a<wind?G(WarnHex):G(DangerHex),bb=a<wind?B(WarnHex):B(DangerHex);
                StripFlat(p0+side*(m.Width*.5f),p1+side*(m.Width*.5f),floorY+.02f,.05f,rr*life,gg*life,bb*life,.95f);StripFlat(p0-side*(m.Width*.5f),p1-side*(m.Width*.5f),floorY+.02f,.05f,rr*life,gg*life,bb*life,.95f);
                StripFlat(p0,p0+dir*(len*u),floorY+.018f,m.Width*.5f,rr*.16f*life,gg*.16f*life,bb*.1f*life,.85f);
                for(int i=0;i<7;i++){float s=(i+.5f)/7;if(s>u)break;var c0=p0+dir*(len*s);Chevron(c0,dir,side,m.Width*.32f,rr*.5f*life,gg*.5f*life,bb*.5f*life);}
                Telegraph+=Quads.Count-q0;
            }
            // The grid's dust falls ahead of it, down the lane, in sequence.
            for(int i=0;i<6;i++){float s=(i+.5f)/6;var at=p0+dir*(len*s)+new Vector3(0,3.9f,0);float when=v.actionAt+wind*s*.95f;Vfx.SmokeBurst(t,at,new Vector3(0,-.4f,0),.8f,.0f,4,2.2f,.2f,1.0f,DustC,.26f,when,.3f,vseed,500+i,0,floorY);}
            // Walls: the bulge travels inside the nearest wall alongside the lane, then a tear where it comes through.
            if(Room==null)return;
            if(a>=0&&a<wind+.2f){float s=BossMath.Clamp01(a/wind);var at=p0+dir*(len*s*.7f);float w=NativeHumArena.NearestWall(Room,v.shiftMask,at.X,at.Z,out float wx,out float wz,out float nx,out float nz);
                float amp=.16f*BossMath.Smooth(a/.2f)*(1-BossMath.Smooth((a-wind)/.2f));WallPatch(new Vector3(wx,floorY+1.9f,wz),nx,nz,1.3f,0,amp,0,17+((int)(s*3)),t);}
            // Where the lane crosses a partition or the room's edge: the surface tears as it goes through, and stays torn a little.
            float travel=m.Strike;
            for(int s=0;s<NativeHumArena.Partitions;s++)
            {
                Room.Partition(s,NativeHumArena.PlaceOf(s,v.shiftMask),out var c,out bool alongX);float half=NativeHumArena.PartitionCells*BackroomsLayout.Cell*.5f;
                float cross;if(alongX){if(Math.Abs(dir.Z)<.05f)continue;cross=(c.Z-p0.Z)/dir.Z;}else{if(Math.Abs(dir.X)<.05f)continue;cross=(c.X-p0.X)/dir.X;}
                if(cross<0||cross>len)continue;var hitAt=p0+dir*cross;if(alongX?Math.Abs(hitAt.X-c.X)>half:Math.Abs(hitAt.Z-c.Z)>half)continue;
                float when=hit+travel*cross/len;float k=t-when;if(k<-.3f||k>3.2f)continue;
                float nx=alongX?0:1,nz=alongX?1:0;if(nx*dir.X+nz*dir.Z>0){nx=-nx;nz=-nz;}
                float open=k<0?0:Math.Min(1.5f,1.5f*BossMath.Smooth(k/.1f))*(1-BossMath.Smooth((k-2)/1.2f));float bulge=k<0?.3f*(1+k/.3f):.08f;
                WallPatch(new Vector3(hitAt.X,floorY+1.95f,hitAt.Z),nx,nz,2.2f,open,bulge,.08f,70+s,t,default,default,default,default,1.3f,1);WallPatch(new Vector3(hitAt.X,floorY+1.95f,hitAt.Z),-nx,-nz,2.2f,open*.85f,bulge*.5f,.08f,90+s,t,default,default,default,default,1.3f,1);
                if(k>=0&&k<.8f)Vfx.SmokeBurst(t,hitAt+new Vector3(0,1.4f,0),dir*2.4f,.8f,.05f,8,1.2f,.25f,1.1f,DustC,.5f,when,.1f,vseed,540+s,0,floorY);
            }
            // Speed: the air pulled along the lane behind it while it's thrown.
            if(a>=wind&&a<wind+m.Strike&&quality>.2f)
            {
                var r=rig.Root;for(int i=0;i<Budget(12);i++){float k=H(i,3);var c=r+dir*(-1.5f-5*k)+side*((H(i,4)-.5f)*2.8f)+new Vector3(0,.5f+3f*H(i,5),0);Vfx.Spark(c,dir*24,LampWarm*(.25f+.2f*H(i,6)),.008f,.3f,.06f,.2f,0,i);}
            }
        }
        private void Chevron(Vector3 c,Vector3 dir,Vector3 side,float w,float r,float g,float b)
        {
            var tip=c+dir*.3f;StripFlat(c-side*w,tip,floorY+.022f,.05f,r,g,b,.9f);StripFlat(c+side*w,tip,floorY+.022f,.05f,r,g,b,.9f);
        }
        // ---------------- Stoop: a circle at its own feet ----------------
        private void Stoop(HumBossRig rig,in HumBossView v,float t)
        {
            if(rig.ActMove<0||NativeHumBoss.Moves[rig.ActMove].Id!="stoop")return;var m=NativeHumBoss.Moves[rig.ActMove];var a=rig.Act;var c=rig.Root;c.Y=floorY+.02f;
            int q0=Quads.Count;float s=a.HitAt>0?a.Age/a.HitAt:1;
            if(s<1){float pulse=.6f+.4f*(reduced?1:(float)Math.Sin(a.Age*17));RingFlat(c,m.Radius,.09f,R(WarnHex)*pulse,G(WarnHex)*pulse,B(WarnHex)*pulse,.9f);RingFlat(c,Math.Max(.1f,m.Radius*s),.05f,R(WarnHex)*.8f,G(WarnHex)*.8f,B(WarnHex)*.6f,.9f);}
            else{float k=(a.Age-a.HitAt)/.3f;if(k<1){RingFlat(c,m.Radius*(.7f+.32f*k),.2f,R(DangerHex)*(1-k),G(DangerHex)*(1-k),B(DangerHex)*(1-k),.95f);}
                float hit=a.Age-a.HitAt;Vfx.SmokeBurst(t,new Vector3(c.X,floorY+.1f,c.Z),Vector3.Zero,3.2f,.25f,14,1.4f,.35f,1.5f,DustC,.42f,v.hitAt,.08f,vseed,600,0,floorY);if(hit<.9f)for(int i=0;i<2;i++){var h=rig.Shown.Position[i==0?NativeHumBossLook.HandL:NativeHumBossLook.HandR];Vfx.RubbleBurst(t,new Vector3(h.X,floorY+.05f,h.Z),Vector3.UnitY,.7f,3.2f,5,1.4f,v.hitAt,.05f,new Vector3(.06f,.05f,.04f),vseed,610+i,0,floorY);}}
            Telegraph+=Quads.Count-q0;
        }
        // ---------------- Reach: the arc swept ahead of it; a wind trail off the fingers ----------------
        private void ReachArc(HumBossRig rig,in HumBossView v,float t)
        {
            if(rig.ActMove<0||NativeHumBoss.Moves[rig.ActMove].Id!="reach")return;var m=NativeHumBoss.Moves[rig.ActMove];var a=rig.Act;
            int q0=Quads.Count;float s=a.HitAt>0?a.Age/a.HitAt:1;var f=BossMath.Forward(rig.Yaw);
            if(s<1){float u=BossMath.Smooth(s*1.3f);ArcFlat(rig.Root,rig.Yaw,m.Reach,m.Arc,R(WarnHex)*.36f*u,G(WarnHex)*.34f*u,B(WarnHex)*.22f*u,.8f,.07f);}
            else{float k=(a.Age-a.HitAt)/m.Strike;if(k<1.1f)ArcFlat(rig.Root,rig.Yaw,m.Reach,m.Arc*BossMath.Clamp01(k)*rig.ActSide,R(DangerHex)*.45f,G(DangerHex)*.45f,B(DangerHex)*.45f,.8f,.08f);
                // Fingers' trail through the sweep.
                if(k<1.3f){var hand=rig.Shown.Position[rig.ActSide>0?NativeHumBossLook.HandR:NativeHumBossLook.HandL];Vfx.SmokeBurst(t,hand,BossMath.Right(rig.Yaw)*(-rig.ActSide*3f),.3f,.1f,5,.8f,.12f,.45f,DustC,.22f,v.hitAt,m.Strike,vseed,620,0,floorY);}}
            Telegraph+=Quads.Count-q0;
        }
        // ---------------- Lights Out: its 3.5 m arc behind the target, seen only as the tubes return ----------------
        private void CutArc(HumBossRig rig,in HumBossView v,float t)
        {
            if(rig.ActMove<0||NativeHumBoss.Moves[rig.ActMove].Id!="cut")return;var m=NativeHumBoss.Moves[rig.ActMove];var a=rig.Act;float k=a.Age-a.HitAt;if(k<-.25f||k>.5f)return;int q0=Quads.Count;
            float e=k<0?1+k/.25f:1-k/.5f;ArcFlat(rig.Root,rig.Yaw,m.Reach+1,m.Arc,(k<0?R(WarnHex):R(DangerHex))*.6f*e,(k<0?G(WarnHex):G(DangerHex))*.6f*e,(k<0?B(WarnHex):B(DangerHex))*.6f*e,.9f,.1f);Telegraph+=Quads.Count-q0;
        }
        // ---------------- Fall Through: the carpet blackens from the middle, tears inward, and the hole shows nothing at all ----------------
        private void FallThrough(HumBossRig rig,in HumBossView v,float t)
        {
            var m=NativeHumBoss.Move("tear");
            if(HumBossView.At(v.heardX)&&v.heard>.02f){var c=new Vector3(v.heardX,floorY+.015f,v.heardZ);float k=v.heard;Scorch(c,0,1.2f*k,1.2f*k,.4f*k);}
            if(HumBossView.At(v.tearAt)&&HumBossView.At(v.tearX))
            {
                float k=t-v.tearAt;var c=new Vector3(v.tearX,floorY,v.tearZ);
                if(k>-m.Wind&&k<6)
                {
                    int q0=Quads.Count;float u=BossMath.Clamp01((k+m.Wind)/m.Wind);
                    if(k<0){float pulse=.7f+.3f*(reduced?1:(float)Math.Sin(t*11));RingFlat(c+new Vector3(0,.02f,0),m.Radius,.06f,R(WarnHex)*pulse,G(WarnHex)*pulse,B(WarnHex)*pulse,.9f);Scorch(c+new Vector3(0,.016f,0),0,m.Radius*u,m.Radius*u,.7f*u);}
                    Telegraph+=Quads.Count-q0;
                    float open=k<0?(k>-.25f?(k+.25f)/.25f*.22f:0):Math.Min(m.Radius,.22f+m.Radius*BossMath.Smooth(k/.32f));float close=k>3.6f?1-BossMath.Smooth((k-3.6f)/1.6f):1;
                    bool stays=v.phase>=3;if(!stays)open*=close;
                    FloorPatch(c,m.Radius*2.1f,open,k<0?-.08f*u:-.02f,.14f*BossMath.Smooth(k/.2f),401,t);Tears++;
                    if(k>=0&&k<.8f){Vfx.SmokeBurst(t,c+new Vector3(0,.05f,0),new Vector3(0,.5f,0),1.8f,.05f,12,1.5f,.25f,1.1f,new Vector3(.08f,.07f,.05f),.5f,v.tearAt,.1f,vseed,630,0,floorY);Vfx.RubbleBurst(t,c,Vector3.UnitY,.9f,2.4f,8,1.2f,v.tearAt,.05f,new Vector3(.2f,.18f,.08f),vseed,631,0,floorY);}
                }
            }
            // The pit grid: Fall Through's holes stay torn in NOCLIP.
            if(v.torn!=null)for(int i=0;i<v.tornCount;i++){int o=i*3;if(t<v.torn[o+2])continue;FloorPatch(new Vector3(v.torn[o],floorY,v.torn[o+1]),m.Radius*2.1f,m.Radius*.92f,-.02f,.14f,410+i*3,t);Tears++;}
        }
        // ---------------- Max Hum: a ring through the walls, chromatic, the air shaking ----------------
        private void Ring(HumBossRig rig,in HumBossView v,float t)
        {
            var m=NativeHumBoss.Move("pulse");
            if(rig.ActMove>=0&&NativeHumBoss.Moves[rig.ActMove].Id=="pulse"&&!rig.Act.Struck)
            {
                // The charge: light pouring in along the floor toward its chest, the air thick round it.
                var ch=rig.Shown.Position[NativeHumBossLook.Ballast];float u=rig.Act.HitAt>0?rig.Act.Age/rig.Act.HitAt:1;int n=Budget(36);
                for(int i=0;i<n;i++){float ph=Frac(H(i,31)+t*.7f);float ang=H(i,32)*BossMath.Tau;float r=(1-ph)*6.5f;var at=ch+new Vector3((float)Math.Cos(ang)*r,(H(i,33)-.5f)*3*(1-ph),(float)Math.Sin(ang)*r);Vfx.Glow(at,Amber*(.6f*u*ph),.09f,NativeBossVfx.Mote);}
                Vfx.Glow(ch,Amber*(1.2f*u),2.2f+2.8f*u,NativeBossVfx.Mote);
            }
            if(!HumBossView.At(v.ringAt)||t<v.ringAt)return;float k=t-v.ringAt,r0=k*m.Speed;float max=Room!=null?Math.Max(Room.MaxX-Room.MinX,Room.MaxZ-Room.MinZ):40;if(r0>max*1.1f)return;
            var c=new Vector3(v.ringX,floorY,v.ringZ);float fade=(1-BossMath.Smooth((k-1.8f)/.8f))*BossMath.Smooth(k/.05f);int q0=Quads.Count;
            // Three thin coloured rings a hand apart (the chromatic shift), a soft band over them, standing floor to ceiling: jump it or roll through it.
            Band(c,r0-.16f,.9f,1.5f*fade,.25f*fade,.3f*fade);Band(c,r0,.9f,.3f*fade,1.5f*fade,.4f*fade);Band(c,r0+.16f,.9f,.3f*fade,.35f*fade,1.5f*fade);
            RingFlat(c+new Vector3(0,.03f,0),r0,.13f,R(DangerHex)*fade,G(DangerHex)*.8f*fade,B(DangerHex)*fade,.95f);
            Telegraph+=Quads.Count-q0;
            if(k<.4f){Vfx.Glow(c+new Vector3(0,2,0),Amber*(4*(1-k/.4f)),5,NativeBossVfx.RingGlow);}
        }
        private void Band(Vector3 c,float r,float height,float cr,float cg,float cb)
        {
            if(r<=.05f)return;int n=Math.Max(24,Math.Min(64,(int)(r*3)));float y0=c.Y+.05f,y1=c.Y+BackroomsLayout.Height-.05f;
            for(int i=0;i<n;i++){float a0=i*BossMath.Tau/n,a1=(i+1)*BossMath.Tau/n;float c0=(float)Math.Cos(a0),s0=(float)Math.Sin(a0),c1=(float)Math.Cos(a1),s1=(float)Math.Sin(a1);
                Add(c.X+c0*r,y0,c.Z+s0*r,c.X+c1*r,y0,c.Z+s1*r,c.X+c1*r,y1,c.Z+s1*r,c.X+c0*r,y1,c.Z+s0*r,cr*.28f,cg*.28f,cb*.28f,.7f,0,true);}
        }
        // ---------------- its fixture: the bar of light, wide and soft, the streak across it ----------------
        private void HeadGlow(HumBossRig rig,in HumBossView v,HumBossStage st,float t)
        {
            float k=DiffuserU;if(k<=.01f)return;var P=rig.Shown;var c=P.Position[NativeHumBossLook.Head];var q=P.Rotation[NativeHumBossLook.Head];var f=BossMath.Rotate(q,Vector3.UnitZ);var r=BossMath.Rotate(q,Vector3.UnitX);var face=c+f*.18f;
            // How much of the panel faces the eye: it blazes face on, edge on it's a line.
            var to=Vector3.Normalize(eye-face);float facing=Math.Max(0,Vector3.Dot(to,f));float air=Math.Max(.35f,1-.5f*st.Air);
            Vfx.Glow(face,LampWarm*(k*.55f*(.25f+facing)*air),2.6f+1.2f*facing,NativeBossVfx.Mote);
            Vfx.Glow(face,LampWarm*(k*.45f*air*(.3f+facing)),1.1f,NativeBossVfx.Core);
            // A long soft bar over the panel, and the flare streak beyond it, only when you're in front of it.
            int segs=8;for(int i=0;i<segs;i++){float s=(i+.5f)/segs*2-1;Vfx.Glow(face+r*(s*.72f),LampWarm*(k*.32f*air*(.3f+facing)),.62f,NativeBossVfx.Mote);}
            if(facing>.2f){float e=(facing-.2f)/.8f;Strip(face-r*(2.2f+1.6f*e),face+r*(2.2f+1.6f*e),.018f+.02f*e,LampWarm.X*k*.6f*e,LampWarm.Y*k*.6f*e,LampWarm.Z*k*.5f*e,.6f,1,true);}
            // Dust motes hanging in its light.
            int motes=Budget(26);for(int i=0;i<motes;i++){float along=Frac(H(i,201)+t*.03f*(.5f+H(i,202)))*5.5f+.3f,spread=.55f+along*.42f;var at=face+f*along+r*((H(i,203)-.5f)*2*spread)+new Vector3(0,(H(i,204)-.5f)*2*spread*.6f,0);float lit=k*(float)Math.Pow(1-along/6f,1.2f)*(.4f+.6f*(float)Math.Sin(t*1.6f+i));Vfx.Glow(at,LampWarm*(.42f*Math.Max(0,lit)),.035f+.03f*H(i,206),NativeBossVfx.Mote);}
            // The jaw: broken glass catching the light inside it, sparks at its lips while it's open.
            float j=rig.JawOpen;if(j>.15f)
            {
                var inner=c+f*.1f;Vfx.Glow(inner,LampWarm*(k*.9f*j),1.3f,NativeBossVfx.Core);
                float slot=Frac(t*2.3f);if(slot<.14f&&!reduced){var at=c+f*.15f+r*((H((int)(t*2.3f),9)-.5f)*1.3f);Vfx.SparkBurst(t,at,f+new Vector3(0,-.4f,0),.9f,2.4f,6,.5f,(float)Math.Floor(t*2.3f)/2.3f,.04f,vseed,700+(int)(t*2.3f)%37,.7f,.012f,.8f,true,floorY);}
            }
        }
        // ---------------- the tubes on its spine: a soft strip of light along each, starter ticks, sparks when one breaks ----------------
        private void TubesFx(HumBossRig rig,in HumBossView v,float t)
        {
            var P=rig.Shown;var beats=HumBossBeats.Current;
            for(int i=0;i<Tube.Length;i++)
            {
                var c=P.Position[NativeHumBossLook.Tube(i)];var q=P.Rotation[NativeHumBossLook.Tube(i)];float k=Tube[i];
                if(k>.03f){Vfx.Glow(c,LampWarm*(k*.32f),1.25f,NativeBossVfx.Mote);Vfx.Glow(c,LampWarm*(k*.5f),.42f,NativeBossVfx.Core);}
                if(rig.Arriving){float s=rig.ArrivalAge-beats.Tube(i);if(s>=0&&s<.2f)Vfx.SparkBurst(t,c,new Vector3(0,.5f,-.5f),.7f,1.6f,5,.4f,v.spawned+beats.Tube(i),.14f,vseed,720+i,.8f,.01f,.8f,true,floorY);}
                if(v.TubeBroken(i))
                {
                    float at=v.PartAt(i);if(HumBossView.At(at)&&t>=at&&t<at+1.2f){Vfx.SparkBurst(t,c,new Vector3(0,.4f,-.6f),.9f,4,18,.8f,at,.05f,vseed,740+i,1.1f,.014f,.8f,true,floorY);if(t<at+.14f)Vfx.Glow(c,LampWarm*3.2f,1.4f,NativeBossVfx.Core);}
                    // A broken tube stays dark and sparks.
                    float cyc=t*1.1f+i*.37f;float ck=Frac(cyc);if(ck<.22f)Vfx.SparkBurst(t,c,new Vector3(0,.2f,-.4f),.9f,1.8f,5,.45f,(float)Math.Floor(cyc)/1.1f-i*.37f/1.1f,.04f,vseed,760+i+(int)cyc%19,.7f,.01f,.9f,true,floorY);
                }
            }
        }
        // ---------------- the Ballast: amber through the ribs ----------------
        private void BallastFx(HumBossRig rig,in HumBossView v,float t)
        {
            float k=Ballast;if(k<=.02f)return;var c=rig.Shown.Position[NativeHumBossLook.Ballast];var f=BossMath.Rotate(rig.Shown.Rotation[NativeHumBossLook.Chest],Vector3.UnitZ);
            Vfx.Glow(c+f*.22f,Amber*(k*.5f),1.0f+.4f*k,NativeBossVfx.Mote);Vfx.Glow(c+f*.2f,Amber*(k*.7f),.42f,NativeBossVfx.Core);
        }
        // ---------------- the intro: the wall: the bulge, the fingers, the tear; it walks through and the paper stretches to it ----------------
        private void Arrival(HumBossRig rig,in HumBossView v,HumBossStage st,float t)
        {
            if(!rig.Arriving||Room==null)return;var b=HumBossBeats.Current;float a=rig.ArrivalAge;float wz=Room.MaxZ,cx=v.x;
            // Where the hand comes through (its x is the boss's right: world -x here). The wall's face is toward the room: -z.
            var c=new Vector3(cx-HumBossArrival.TearX,floorY+HumBossArrival.TearY,wz);
            if(a<b.Bulge-.1f)return;
            float press=BossMath.Smooth((a-b.Bulge)/(b.Fingers-b.Bulge));float through=BossMath.Smooth((a-b.Fingers)/(b.TearOpen-b.Fingers));float open=BossMath.Smoother((a-b.TearOpen+.08f)/.55f);
            float scratch=reduced?0:.012f*(float)Math.Sin(a*31)*(1-through);
            // Four finger tips under the paper, spreading, before they're through (across the wall in its own axis: -x here).
            var f0=new Vector3(-.5f,-.05f+.02f*(float)Math.Sin(a*5),.1f*press*(1-through));var f1=new Vector3(-.17f,.05f,.13f*press*(1-through));var f2=new Vector3(.15f,.06f+.01f*(float)Math.Sin(a*7),.12f*press*(1-through));var f3=new Vector3(.47f,-.04f,.09f*press*(1-through));
            float holeR=.42f*through+.42f*open;float bulge=.1f*press*(1-.6f*open)+scratch;
            WallPatch(c,0,-1,2.1f,holeR,bulge,.05f*through+.05f*open,9090,t,f0,f1,f2,f3,1.5f,open);Tears++;
            // Plaster dust out of the tear, falling.
            if(a>=b.Fingers){var at0=new Vector3(c.X,c.Y,wz-.65f);Vfx.SmokeBurst(t,at0,new Vector3(0,-.15f,-.5f),.28f,.0f,7,2.3f,.05f,.34f,new Vector3(.5f,.48f,.38f),.14f,v.spawned+b.Fingers,1.1f,vseed,800,0,floorY);Vfx.RubbleBurst(t,new Vector3(c.X,c.Y+.25f,wz-.2f),new Vector3(0,-.2f,-1),.8f,1.3f,8,2.2f,v.spawned+b.TearOpen,.022f,new Vector3(.38f,.38f,.2f),vseed,801,0,floorY);}
            // It walks out through the wall without opening it: the paper reaches for it at each limb crossing the wall. A larger stretch round the torso.
            if(a>=b.WalkOut&&a<b.WalkOut+3.2f)
            {
                var ch=rig.Shown.Position[NativeHumBossLook.Spine1];float k=wz-ch.Z;float near=(float)Math.Exp(-k*k/.6f);
                if(near>.02f){WallPatch(new Vector3(ch.X,floorY+2.05f,wz),0,-1,2.7f,.5f*near,.26f*near,.04f,9091,t,default,default,default,default,1.9f,near);Tears++;}
                for(int limb=0;limb<4;limb++){var h=rig.LimbAt[limb];float dz=wz-h.Z;float nl=(float)Math.Exp(-dz*dz/.25f);if(nl>.05f){float hy=Math.Max(floorY+.5f,h.Y);WallPatch(new Vector3(h.X,hy,wz),0,-1,1.0f,.16f*nl,.12f*nl,.02f,9100+limb,t,default,default,default,default,1.3f,nl*.7f);Tears++;}}
                if(near>.2f&&a<b.WalkOut+2.5f)Vfx.SmokeBurst(t,new Vector3(ch.X,floorY+2.4f,wz-.7f),new Vector3(0,-.25f,-.4f),.9f,.02f,8,2.4f,.14f,.7f,DustC,.16f,v.spawned+b.WalkOut+.4f,1.5f,vseed,810,0,floorY);
            }
            // Through the ceiling as it straightens: dust and tiles around it.
            float st0=a-b.Straighten-.25f;if(st0>=0&&st0<2){var h=rig.Shown.Position[NativeHumBossLook.Chest];CeilingFall(h,v.spawned+b.Straighten+.25f,t,900);}
            if(a>=b.Hold-.25f){}
        }
        // Tiles, dust and rail out of the ceiling grid round a point.
        private void CeilingFall(Vector3 at,float when,float t,int key)
        {
            var top=new Vector3(at.X,floorY+BackroomsLayout.Height-.05f,at.Z);
            Vfx.RubbleBurst(t,top,new Vector3(0,-1,0),1.0f,1.8f,16,2.6f,when,.14f,TileC,vseed,key,0,floorY);
            Vfx.SmokeBurst(t,top,new Vector3(0,-1.2f,0),1.8f,.05f,16,2.4f,.3f,1.5f,DustC,.42f,when,.3f,vseed,key+1,0,floorY);
            Vfx.SparkBurst(t,top,new Vector3(0,-1,0),.9f,3.2f,14,.8f,when,.1f,vseed,key+2,.9f,.012f,.9f,true,floorY);
            CeilingPatch(top,2.4f,1.25f,0,0,key+3,t);Tears++;
        }
        // ---------------- Wall Hand: the tear and the arm in the wall by the pilot ----------------
        private void WallHand(HumBossRig rig,in HumBossView v,float t)
        {
            if(!HumBossView.At(v.handAt)||Room==null)return;var m=NativeHumBoss.Move("hand");float k=t-v.handAt;if(k<-m.Wind||k>m.Strike+m.Follow+m.Recover)return;
            float until=HumBossView.At(v.handUntil)?t-v.handUntil:-1;float grow=k<0?(k+m.Wind)/m.Wind:1;float fade=1-BossMath.Smooth((until-.2f)/.6f);
            var c=new Vector3(v.handX,v.handY,v.handZ);float scratch=k<0&&!reduced?.012f*(float)Math.Sin(t*37):0;
            WallPatch(c,v.handNx,v.handNz,1.5f,k<0?0:.42f*BossMath.Smooth(k/.15f)*fade,.14f*grow+scratch,.05f*BossMath.Smooth(k/.15f),300,t,default,default,default,default,1.2f,1);Tears++;
            // Its warning on the carpet, under where it comes out.
            if(k<0){int q0=Quads.Count;RingFlat(new Vector3(v.handX+v.handNx*1.3f,floorY+.02f,v.handZ+v.handNz*1.3f),m.Grab*.7f,.05f,R(WarnHex)*grow,G(WarnHex)*grow,B(WarnHex)*grow,.85f);Telegraph+=Quads.Count-q0;}
            if(k>=0&&k<.8f)Vfx.SmokeBurst(t,c+new Vector3(v.handNx,0,v.handNz)*.5f,new Vector3(v.handNx,0,v.handNz)*.8f,.35f,.0f,6,1.4f,.05f,.32f,new Vector3(.5f,.48f,.38f),.16f,v.handAt,.1f,vseed,820,0,floorY);
        }
        // ---------------- Familiar Voices: faces pressing out of the paper round each pilot ----------------
        private void Voices(HumBossRig rig,in HumBossView v,float t)
        {
            if(v.voices==null||v.voiceCount<=0||Room==null)return;var m=NativeHumBoss.Move("voices");
            for(int i=0;i<v.voiceCount;i++)
            {
                int o=i*6;float at=v.voices[o+5],k=t-at;if(k<-m.Wind||k>m.Wind+m.Strike+1.5f)continue;var c=new Vector3(v.voices[o],v.voices[o+1],v.voices[o+2]);float nx=v.voices[o+3],nz=v.voices[o+4];
                float rise=BossMath.Smooth((k+m.Wind)/m.Wind);float sink=1-BossMath.Smooth((k-m.Strike)/1f);float open=k<0?0:.5f*BossMath.Smooth(k/.5f);
                // A face under the paper: brow and cheeks standing out, the mouth pulled in. (Offsets across and up the wall; it stretches as it comes.)
                var f0=new Vector3(-.09f,.09f,.08f*rise);var f1=new Vector3(.09f,.09f,.08f*rise);var f2=new Vector3(0,.02f,.06f*rise);var f3=new Vector3(0,-.12f,-.04f*rise);
                WallPatch(c,nx,nz,1.1f,open*sink,.17f*rise*sink,.1f*sink,320+i*7,t,f0,f1,f2,f3);Tears++;
            }
        }
        // ---------------- the phase changes ----------------
        private void PhaseFx(HumBossRig rig,in HumBossView v,HumBossStage st,float t)
        {
            if(!HumBossView.At(v.phaseAt)||v.phase<2)return;float k=t-v.phaseAt;
            if(v.phase==2)
            {
                // The rows dying toward the middle spit sparks; dust shakes off the grid as the scream peaks.
                if(k>0&&k<NativeHumBoss.PhaseChangeSeconds&&Room!=null)for(int i=0;i<NativeHumArena.FixtureCount;i++){int row=i/NativeHumArena.FixtureSide;float mid=2.5f-Math.Abs(row-2.5f);float die=v.phaseAt+2.25f+mid*.26f+(i%NativeHumArena.FixtureSide%2)*.05f;if(i%3==0)Vfx.SparkBurst(t,Room.FixtureAt[i],new Vector3(0,-1,0),.9f,2.8f,6,.8f,die,.05f,vseed,840+i,.8f,.012f,.8f,true,floorY);}
                if(k>.6f&&k<2.3f&&Room!=null)for(int i=0;i<8;i++){var c=new Vector3(Room.MinX+(i*2+1)*Room.MaxX/18-Room.MinX*(i*2+1)/18,floorY+3.9f,Room.MinZ+(((i*5)%8)*2+1)*(Room.MaxZ-Room.MinZ)/16);Vfx.SmokeBurst(t,c,new Vector3(0,-.5f,0),.9f,.02f,4,2.2f,.3f,1.1f,DustC,.22f,v.phaseAt+.8f+i*.12f,.2f,vseed,870+i,0,floorY);}
            }
            else
            {
                // It rears through the ceiling: the tiles rain.
                var ch=rig.Shown.Position[NativeHumBossLook.Chest];if(k>=1.2f&&k<4){float when=v.phaseAt+1.3f;var top=new Vector3(v.x,floorY+BackroomsLayout.Height-.05f,v.z+BossMath.Forward(v.yaw*BossMath.Deg).Z*.2f);CeilingFall(top,when,t,880);for(int i=0;i<5;i++){float ang=i*1.256f,r=2.2f+.5f*i;var at=top+new Vector3((float)Math.Cos(ang)*r,0,(float)Math.Sin(ang)*r);Vfx.RubbleBurst(t,at,new Vector3(0,-1,0),.6f,1.2f,5,2.4f,when+.1f+i*.08f,.16f,TileC,vseed,890+i,0,floorY);CeilingPatch(at,1.4f,.55f,0,0,895+i,t);Tears++;}}
            }
        }
        // ---------------- wet footprints: darkened carpet fading; a hand's arch prints its fingertips ----------------
        private void Steps(HumBossRig rig,in HumBossView v,float t)
        {
            if(rig.Flip>0){return;}
            if(rig.Landed!=0)for(int i=0;i<4;i++)if((rig.Landed&(1<<i))!=0){printAt[printNext]=rig.LimbAt[i];printT[printNext]=t;printYaw[printNext]=rig.Yaw;printHand[printNext]=i>=2;printNext=(printNext+1)%Prints;
                var at=rig.LimbAt[i];at.Y=floorY+.05f;Vfx.SmokeBurst(t,at,new Vector3(0,.2f,0),.5f,.02f,3,.8f,.1f,.5f,DustC,.14f,t,.02f,vseed,950+i,0,floorY);}
            for(int i=0;i<Prints;i++)
            {
                float k=t-printT[i];if(!(k>=0&&k<9)||printT[i]==0)continue;float a=(1-k/9f)*.62f;var at=printAt[i];at.Y=floorY+.012f;float c=(float)Math.Cos(printYaw[i]),s=(float)Math.Sin(printYaw[i]);var f=new Vector3(s,0,c);var r=new Vector3(c,0,-s);
                if(printHand[i]){for(int j=0;j<4;j++){var q=at+f*(.55f+.25f*H(i,j))+r*((j-1.5f)*.22f);Scorch(q,0,.045f,.05f,a);}Scorch(at,0,.14f,.14f,a*.25f);}
                else{Scorch(at+f*.38f,0,.14f,.2f,a);Scorch(at+f*.68f+r*.1f,0,.03f,.08f,a);Scorch(at+f*.72f-r*.1f,0,.03f,.08f,a);}
            }
        }
        public void ClearPrints(){Array.Clear(printT,0,Prints);printNext=0;}
        // ---------------- drips off the hide and the strips ----------------
        private void Drips(HumBossRig rig,in HumBossView v,float t)
        {
            if(quality<.15f||low&&quality<.4f)return;int n=Budget(16);var P=rig.Shown;
            for(int i=0;i<n;i++)
            {
                float period=1.4f+1.3f*H(i,1);float cyc=(t+H(i,2)*7)/period;int c=(int)Math.Floor(cyc);float k=(cyc-c)*period;
                int s=(i+c)%NativeHumBossLook.Strips,link=NativeHumBossLook.StripLinks-1-((i+c)%2);int j=NativeHumBossLook.Strip(s,link);var from=P.Position[j]+BossMath.Rotate(P.Rotation[j],new Vector3(0,-.15f,0));
                var at=from+new Vector3(0,-.5f*NativeBossVfx.Gravity*k*k,0);if(at.Y<floorY+.01f)continue;float sp=NativeBossVfx.Gravity*k;Vfx.Drop(at,new Vector3(0,Math.Min(.12f,.012f*sp+.02f),0),new Vector3(.06f,.055f,.04f),.008f,.8f,k/period,i);
            }
        }
        // ---------------- NOCLIP: the textures slip: blocks of wall and ceiling creeping and swimming ----------------
        private void Slip(HumBossRig rig,in HumBossView v,HumBossStage st,float t)
        {
            if(st.Slip<=.01f||Room==null||reduced)return;float amt=st.Slip;float c=BackroomsLayout.Cell;
            for(int i=0;i<10;i++)
            {
                float x=Room.MinX+(1+H(i,1)*10)*c,z=Room.MinZ+(1+H(i,2)*10)*c;Surface.Slip(NativeHumSurface.CeilingS,new Vector3(x,floorY+BackroomsLayout.Height-.004f,z),-Vector3.UnitY,Vector3.UnitX,Vector3.UnitZ,c*(.8f+.6f*H(i,3)),c*(.8f+.8f*H(i,4)),amt*(.6f+.8f*H(i,5)),t,50+i);
            }
            for(int i=0;i<14;i++)
            {
                int w=i%4;float u=.1f+.8f*H(i,11),half=1.2f+1.6f*H(i,12);float y=floorY+BackroomsMesh.TrimHeight+.05f+half*.6f+H(i,13)*(BackroomsLayout.Height-1.6f-half*1.2f);
                Vector3 p,n,ax;float span=Room.MaxX-Room.MinX,inset=BackroomsLayout.Thickness*.5f;
                if(w==0){p=new Vector3(Room.MinX+u*span,y,Room.MinZ+inset);n=Vector3.UnitZ;ax=Vector3.UnitX;}else if(w==1){p=new Vector3(Room.MinX+u*span,y,Room.MaxZ-inset);n=-Vector3.UnitZ;ax=Vector3.UnitX;}
                else if(w==2){p=new Vector3(Room.MinX+inset,y,Room.MinZ+u*span);n=Vector3.UnitX;ax=Vector3.UnitZ;}else{p=new Vector3(Room.MaxX-inset,y,Room.MinZ+u*span);n=-Vector3.UnitX;ax=Vector3.UnitZ;}
                Surface.Slip(NativeHumSurface.WallS,p,n,ax,Vector3.UnitY,half,Math.Min(half*.7f,1.5f),amt*(.5f+.9f*H(i,14)),t,70+i);
            }
        }
        // ---------------- clipped: half in a surface, glitching ----------------
        private void Clip(HumBossRig rig,in HumBossView v,float t)
        {
            if(!HumBossView.At(v.clipAt)||v.clipSeconds<=0||t<v.clipAt||t>v.clipAt+v.clipSeconds+.4f||Room==null)return;float k=t-v.clipAt;
            var c=rig.Shown.Position[NativeHumBossLook.Chest];float d=NativeHumArena.NearestWall(Room,v.shiftMask,c.X,c.Z,out float wx,out float wz,out float nx,out float nz);
            float open=1.1f*BossMath.Smooth(k/.15f)*(1-BossMath.Smooth((k-v.clipSeconds)/.4f));WallPatch(new Vector3(wx,floorY+2.05f,wz),nx,nz,2.8f,open,.2f,.1f,340,t,default,default,default,default,1.5f,1);Tears++;
            if(!reduced&&k<v.clipSeconds){int slot=(int)Math.Floor(t*7);if(H(slot,5)<.5f)Vfx.SparkBurst(t,new Vector3(wx,floorY+1.5f+2*H(slot,6),wz),new Vector3(nx,-.2f,nz),.9f,2.4f,6,.5f,slot/7f,.03f,vseed,960+slot%23,.8f,.012f,.9f,true,floorY);}
        }
        // ---------------- the death: tubes pop from the top down; glass; then only the fixture ----------------
        private void Death(HumBossRig rig,in HumBossView v,float t)
        {
            if(!v.Dead(t))return;float d=t-v.diedAt;var P=rig.Shown;
            for(int i=0;i<Tube.Length;i++)
            {
                float pop=.18f+(Tube.Length-1-i)*.17f;float k=d-pop;if(k<0||k>1.6f)continue;var c=P.Position[NativeHumBossLook.Tube(i)];
                Vfx.SparkBurst(t,c,new Vector3(0,.5f,-.6f),.9f,3.4f,16,.8f,v.diedAt+pop,.04f,vseed,980+i,1.1f,.014f,.8f,true,floorY);if(k<.1f)Vfx.Glow(c,LampWarm*(3.8f*(1-k/.1f)),1.5f,NativeBossVfx.Core);GlassRain(c,v.diedAt+pop,t,1000+i,i);
            }
            if(d>1.8f&&d<2.5f){var h=rig.Shown.Position[NativeHumBossLook.HandL];Vfx.SmokeBurst(t,new Vector3(v.x,floorY+.1f,v.z+.4f),Vector3.Zero,2.6f,.12f,14,2,.35f,1.5f,DustC,.34f,v.diedAt+1.9f,.12f,vseed,1020,0,floorY);}
        }
        // ---------------- its lights in the room's slots: the fixture, two along the spine, the Ballast ----------------
        private void Lights(HumBossRig rig,in HumBossView v,HumBossStage st,float t)
        {
            var P=rig.Shown;float head=DiffuserU;
            if(head>.02f){var c=P.Position[NativeHumBossLook.Head];var f=BossMath.Rotate(P.Rotation[NativeHumBossLook.Head],Vector3.UnitZ);AddLight(c+f*.7f+new Vector3(0,-.9f,0),8.5f,LampWarm*(.42f*head));}
            float low2=0,hi2=0;for(int i=0;i<3;i++)low2+=Tube[i];for(int i=4;i<7;i++)hi2+=Tube[i];low2/=3;hi2/=3;
            if(hi2>.02f)AddLight(P.Position[NativeHumBossLook.Tube(5)]+new Vector3(0,-.1f,-.15f),7.5f,LampWarm*(.42f*hi2));
            if(low2>.02f)AddLight(P.Position[NativeHumBossLook.Tube(1)]+new Vector3(0,-.25f,-.2f),7.5f,LampWarm*(.42f*low2));
            if(Ballast>.05f)AddLight(P.Position[NativeHumBossLook.Ballast]+BossMath.Rotate(P.Rotation[NativeHumBossLook.Chest],new Vector3(0,0,.5f)),5,Amber*(.5f*Math.Min(3,Ballast)));
        }
        private void AddLight(Vector3 p,float range,Vector3 rgb){if(LightCount>=MaxLights)return;int i=LightCount++;LightPos[i*4]=p.X;LightPos[i*4+1]=p.Y;LightPos[i*4+2]=p.Z;LightPos[i*4+3]=range;LightRgb[i*3]=rgb.X;LightRgb[i*3+1]=rgb.Y;LightRgb[i*3+2]=rgb.Z;}
        // ---------------- the surfaces in the room ----------------
        private void WallPatch(Vector3 c,float nx,float nz,float radius,float open,float bulge,float lift,int seed,float t,Vector3 f0=default,Vector3 f1=default,Vector3 f2=default,Vector3 f3=default,float tall=1,float strips=0)
        {
            var n=new Vector3(nx,0,nz);var a=new Vector3(nz,0,-nx);var at=c+n*(BackroomsLayout.Thickness*.5f);
            float along=Vector3.Dot(c-new Vector3(Room.MinX,0,Room.MinZ),a);
            // (A partition's own ends are not checked: a bulge running past one shows over thin air only a little past the end.)
            float minA=-radius,maxA=radius;
            if(Math.Abs(a.X)>.5f){float lo=(Room.MinX-at.X)*Math.Sign(a.X),hi=(Room.MaxX-at.X)*Math.Sign(a.X);minA=Math.Max(minA,Math.Min(lo,hi));maxA=Math.Min(maxA,Math.Max(lo,hi));}
            else{float lo=(Room.MinZ-at.Z)*Math.Sign(a.Z),hi=(Room.MaxZ-at.Z)*Math.Sign(a.Z);minA=Math.Max(minA,Math.Min(lo,hi));maxA=Math.Min(maxA,Math.Max(lo,hi));}
            float minB=floorY+BackroomsMesh.TrimHeight+.02f-c.Y,maxB=floorY+BackroomsLayout.Height-.02f-c.Y;
            Surface.Tear(NativeHumSurface.WallS,at,n,a,Vector3.UnitY,radius,open,bulge,lift,seed,eye,minA,maxA,minB,maxB,f0,f1,f2,f3,1,tall,strips,t);
        }
        private void FloorPatch(Vector3 c,float radius,float open,float bulge,float lift,int seed,float t)
        {
            float minA=Room!=null?Room.MinX-c.X:-radius,maxA=Room!=null?Room.MaxX-c.X:radius,minB=Room!=null?Room.MinZ-c.Z:-radius,maxB=Room!=null?Room.MaxZ-c.Z:radius;
            Surface.Tear(NativeHumSurface.FloorS,new Vector3(c.X,floorY,c.Z),Vector3.UnitY,Vector3.UnitZ,Vector3.UnitX,radius,open,bulge,lift,seed,eye,minB,maxB,minA,maxA);
        }
        private void CeilingPatch(Vector3 c,float radius,float open,float bulge,float lift,int seed,float t)
        {
            float minA=Room!=null?Room.MinX-c.X:-radius,maxA=Room!=null?Room.MaxX-c.X:radius,minB=Room!=null?Room.MinZ-c.Z:-radius,maxB=Room!=null?Room.MaxZ-c.Z:radius;
            Surface.Tear(NativeHumSurface.CeilingS,new Vector3(c.X,floorY+BackroomsLayout.Height,c.Z),-Vector3.UnitY,Vector3.UnitX,Vector3.UnitZ,radius,open,bulge,lift,seed,eye,minA,maxA,minB,maxB);
        }
        // ---------------- primitives (the 6:47's: premultiplied pieces sorted far to near, additive straight in) ----------------
        private int Budget(int full)=>Math.Max(0,(int)Math.Round(full*(low?.5f:1f)*(.35f+.65f*quality)));
        private void Add(double ax,double ay,double az,double bx,double by,double bz,double cx,double cy,double cz,double dx,double dy,double dz,float r,float g,float b,float a,int mode,bool additive)
        {
            if(additive){Quads.Add(ax,ay,az,bx,by,bz,cx,cy,cz,dx,dy,dz,r,g,b,a,mode,true,1);return;}
            if(pn>=SortMax||!(a>0))return;int o=pn*12;pc[o]=ax;pc[o+1]=ay;pc[o+2]=az;pc[o+3]=bx;pc[o+4]=by;pc[o+5]=bz;pc[o+6]=cx;pc[o+7]=cy;pc[o+8]=cz;pc[o+9]=dx;pc[o+10]=dy;pc[o+11]=dz;
            pcol[pn*4]=r;pcol[pn*4+1]=g;pcol[pn*4+2]=b;pcol[pn*4+3]=a;pmode[pn]=(byte)mode;double mx=(ax+cx)/2-eye.X,my=(ay+cy)/2-eye.Y,mz=(az+cz)/2-eye.Z;pk[pn]=mx*mx+my*my+mz*mz;pidx[pn]=pn;pn++;
        }
        private void Flush()
        {
            for(int i=1;i<pn;i++){int v=pidx[i];double key=pk[v];int j=i-1;while(j>=0&&pk[pidx[j]]<key){pidx[j+1]=pidx[j];j--;}pidx[j+1]=v;}
            for(int i=0;i<pn;i++){int v=pidx[i],o=v*12;Quads.Add(pc[o],pc[o+1],pc[o+2],pc[o+3],pc[o+4],pc[o+5],pc[o+6],pc[o+7],pc[o+8],pc[o+9],pc[o+10],pc[o+11],pcol[v*4],pcol[v*4+1],pcol[v*4+2],pcol[v*4+3],pmode[v],false,1);}
            pn=0;
        }
        // A dark soft patch on the carpet (burnt, wet): premultiplied black with `a` coverage. Round (mode 1).
        private void Scorch(Vector3 c,float yaw,float hx,float hz,float a)
        {
            if(a<=.005f)return;Add(c.X-hx,c.Y,c.Z-hz,c.X+hx,c.Y,c.Z-hz,c.X+hx,c.Y,c.Z+hz,c.X-hx,c.Y,c.Z+hz,Soot.X*a,Soot.Y*a,Soot.Z*a,a,1,false);
        }
        private void Strip(Vector3 a,Vector3 b,float w,float r,float g,float bl,float alpha,int mode,bool additive)
        {
            var mid=(a+b)*.5f;var along=b-a;var toEye=eye-mid;var side=Vector3.Cross(along,toEye);float l=side.Length();if(l<1e-6f)return;side*=w/l;
            Add(a.X-side.X,a.Y-side.Y,a.Z-side.Z,b.X-side.X,b.Y-side.Y,b.Z-side.Z,b.X+side.X,b.Y+side.Y,b.Z+side.Z,a.X+side.X,a.Y+side.Y,a.Z+side.Z,r,g,bl,alpha,mode,additive);
        }
        private void StripFlat(Vector3 a,Vector3 b,float y,float w,float r,float g,float bl,float alpha)
        {
            var d=b-a;d.Y=0;float l=d.Length();if(l<1e-5f)return;var s=new Vector3(-d.Z,0,d.X)*(w/l);
            Add(a.X-s.X,y,a.Z-s.Z,b.X-s.X,y,b.Z-s.Z,b.X+s.X,y,b.Z+s.Z,a.X+s.X,y,a.Z+s.Z,r,g,bl,alpha,0,true);
        }
        private void RingFlat(Vector3 c,float radius,float width,float r,float g,float b,float a)
        {
            int n=Math.Max(16,Math.Min(64,(int)(radius*8)));
            for(int i=0;i<n;i++){double a0=i*Math.PI*2/n,a1=(i+1.04)*Math.PI*2/n;float c0=(float)Math.Cos(a0),s0=(float)Math.Sin(a0),c1=(float)Math.Cos(a1),s1=(float)Math.Sin(a1);float r0=radius-width,r1=radius;
                Add(c.X+c0*r0,c.Y,c.Z+s0*r0,c.X+c1*r0,c.Y,c.Z+s1*r0,c.X+c1*r1,c.Y,c.Z+s1*r1,c.X+c0*r1,c.Y,c.Z+s0*r1,r,g,b,a,0,true);}
        }
        // A wedge on the floor (reach m, arc degrees about the yaw, or a swept share when signed), its edge brighter.
        private void ArcFlat(Vector3 root,float yaw,float reach,float arc,float r,float g,float b,float a,float edge)
        {
            float span=Math.Abs(arc)*BossMath.Deg;if(span<1e-3f)return;float from=arc<0?yaw+span*.5f-span:yaw-span*.5f;int n=Math.Max(6,(int)(span*12));float y=floorY+.02f;
            for(int i=0;i<n;i++)
            {
                float a0=from+span*i/n,a1=from+span*(i+1.02f)/n;var d0=BossMath.Forward(a0);var d1=BossMath.Forward(a1);
                float r0=1.0f,r1=reach;
                Add(root.X+d0.X*r0,y,root.Z+d0.Z*r0,root.X+d1.X*r0,y,root.Z+d1.Z*r0,root.X+d1.X*r1,y,root.Z+d1.Z*r1,root.X+d0.X*r1,y,root.Z+d0.Z*r1,r,g,b,a,0,true);
                Add(root.X+d0.X*(r1-edge),y+.002f,root.Z+d0.Z*(r1-edge),root.X+d1.X*(r1-edge),y+.002f,root.Z+d1.Z*(r1-edge),root.X+d1.X*r1,y+.002f,root.Z+d1.Z*r1,root.X+d0.X*r1,y+.002f,root.Z+d0.Z*r1,r*2.2f,g*2.2f,b*2.2f,a,0,true);
            }
        }
    }
}
