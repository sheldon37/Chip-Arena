using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The intro's beats on the boss clock (seconds from the arrival, 0 the music stopping; doc section 3 "It has heard you"): the one table they're timed on. The owner's song will bind them to its accents, as
    /// NativeStationBossBeats.UseSong does; until then these are fixed numbers on the fallback (the Siren's song: its name time 6.4261 and its drop 9.0678). Presentation only: no hit, hold or rule reads these.
    /// </summary>
    public struct HumBossBeats
    {
        /// <summary>The far rows going out, nearer each time (rows 5, 4, 3, 2 of six, counted from the party's side); the two nearest go with the walk.</summary>
        public float RowOut0,RowOut1,RowOut2,RowOut3;
        /// <summary>The paper bulges, the fingers come through, it tears open; the walk out starts (the last lamps die).</summary>
        public float Bulge,Fingers,TearOpen,WalkOut;
        /// <summary>The first tube strikes and the gap to the next (seven, up the spine).</summary>
        public float Tube0,TubeStep;
        /// <summary>The head flickers, catches and steadies; it looks; the diffuser splits; the card rises at <see cref="Name"/>.</summary>
        public float HeadFlicker,HeadCatch,Look,JawSplit,Name;
        /// <summary>It straightens, it stoops; every light in the room slams on and the song drops at the hold's end.</summary>
        public float Straighten,Stoop,Hold;
        public float Row(int i)=>i<=0?RowOut0:i==1?RowOut1:i==2?RowOut2:RowOut3;
        public float Tube(int i)=>Tube0+i*TubeStep;
        public static readonly HumBossBeats Fallback=new HumBossBeats{RowOut0=.5f,RowOut1=.98f,RowOut2=1.42f,RowOut3=1.86f,Bulge=2.35f,Fingers=2.95f,TearOpen=3.3f,WalkOut=4.2f,Tube0=4.5f,TubeStep=.24f,HeadFlicker=6.15f,HeadCatch=6.82f,Look=6.95f,JawSplit=7.35f,Name=NativeBackroomsBossIntro.NameAt,Straighten=8f,Stoop=8.62f,Hold=NativeBackroomsBossIntro.Hold};
        /// <summary>Ordered and inside the hold.</summary>
        public bool Valid=>RowOut0>=0&&RowOut1>RowOut0&&RowOut2>RowOut1&&RowOut3>RowOut2&&Bulge>RowOut3&&Fingers>Bulge&&TearOpen>Fingers&&WalkOut>TearOpen&&Tube0>WalkOut&&Tube(6)<HeadFlicker&&HeadCatch>HeadFlicker&&Look>HeadCatch&&JawSplit>Look&&Straighten>JawSplit&&Stoop>Straighten&&Hold>Stoop;
        private static HumBossBeats bound;private static bool has;
        /// <summary>The beats the intro plays on now.</summary>
        public static HumBossBeats Current=>has?bound:Fallback;
        /// <summary>Binds a set of beats (a song's, later); an out-of-order set is refused.</summary>
        public static bool Use(in HumBossBeats b){if(!b.Valid)return false;bound=b;has=true;return true;}
        public static void Clear(){has=false;}
    }

    /// <summary>
    /// The room for THE HUM at one moment: each lamp's level (its circuit dying, rows flaring and bursting, the ring flaring each as it passes, the lamps dipping along a lunge's lane, the blackout's stray circuits, NOCLIP's wrong
    /// light, the death's return), where the partitions stand, and how bright the air is. A pure function of the record, the clock and the beats (<see cref="Evaluate"/>): Runtime NativeHumBossRoom.cs draws it, and the level's own lights
    /// stay as they are with no boss. Reduced effects: nothing strobes; the row-by-row dimming stays.
    /// </summary>
    public sealed class HumBossStage
    {
        public bool On;
        /// <summary>Each fixture's light (0 dark, 1 as the level has it, more: flaring), and whether it's burst (drawn broken).</summary>
        public readonly float[] Lamp=new float[NativeHumArena.FixtureCount];public readonly bool[] Burst=new bool[NativeHumArena.FixtureCount];
        /// <summary>Which place each partition stands in.</summary>
        public readonly int[] Place=new int[NativeHumArena.Partitions];
        /// <summary>The mean light (the ambient and the haze follow it), the whole room's tint away from the lamps' own (NOCLIP's wrong light), the body's stutter, the shudder, dust from the grid, the hum's level 0..1 and its pitch 0..1, the swim of the textures.</summary>
        public float Air=1,Wrong,Hitch,Shake,Dust,Hum,Pitch,Slip;
        /// <summary>The silence of Lights Out (1: every sound and tube cut).</summary>
        public float Silent;
        private static float Ramp(float x)=>x<=0?0:x>=1?1:x*x*(3-2*x);
        private static float Fade(float t,float len)=>t<0||t>=len?0:1-t/len;
        private static float Impact(float t,float len)=>t<0||t>=len?0:(float)Math.Exp(-t*5/len)*(1-t/len);
        // The buzz of a failing fixture: never faster than 10 Hz (the level's own rule).
        private static float Stutter(int cell,float t,bool reduced)
        {
            if(reduced)return .75f;int k=(int)Math.Floor(t*9);float h=BossMath.Hash((uint)cell*31u+7u,k);return h<.2f?.08f:h<.45f?.5f:1;
        }
        public void Evaluate(in HumBossView v,float t,in HumBossBeats b,HumRoom room,bool reduced)
        {
            On=v.live;Air=1;Wrong=0;Hitch=0;Shake=0;Dust=0;Hum=.2f;Pitch=.2f;Slip=0;Silent=0;
            for(int i=0;i<Lamp.Length;i++){Lamp[i]=1;Burst[i]=false;}
            int from=v.shiftFrom,to=v.shiftMask;float swap=HumBossView.At(v.shiftAt)?v.shiftAt+.07f:float.PositiveInfinity;
            for(int s=0;s<Place.Length;s++)Place[s]=NativeHumArena.PlaceOf(s,t>=swap?to:from);
            if(!v.live||room==null)return;
            float age=HumBossView.At(v.spawned)?t-v.spawned:float.PositiveInfinity;
            // ---- the room's state by phase (a phase's change plays in its own window) ----
            int phase=Math.Max(1,Math.Min(3,v.phase));float pa=HumBossView.At(v.phaseAt)?t-v.phaseAt:float.PositiveInfinity;
            for(int i=0;i<Lamp.Length;i++)
            {
                float lvl=1;int row=i/NativeHumArena.FixtureSide,col=i%NativeHumArena.FixtureSide;
                if(phase==2){lvl=BlackoutLamp(i,t,reduced);if(pa<NativeHumBoss.PhaseChangeSeconds){float mid=2.5f-Math.Abs(row-2.5f);float die=2.25f+mid*.26f+(col%2)*.05f;
                    // The scream: the whole grid climbing, shaking; it stops; the rows die toward the middle.
                    float up=1+1.7f*Ramp(pa/2.1f);float s=reduced?1:(.85f+.15f*(float)Math.Sin(pa*63+i));lvl=pa<die?up*s:(pa<die+.18f?Stutter(i,t,reduced)*.7f:lvl);}}
                else if(phase>=3)
                {
                    lvl=NoclipLamp(i,t,reduced);
                    // Before NOCLIP's lights come back: they stay out till the rear reaches the ceiling; then they stutter in wrong, row by row.
                    if(pa<NativeHumBoss.PhaseChangeSeconds){float come=1.3f+(5-row)*.16f;lvl=pa<come?BlackoutLamp(i,t,reduced):lvl*Ramp((pa-come)/.15f);}
                }
                Lamp[i]=lvl;
            }
            if(phase>=3)Wrong=pa<1.3f?0:Ramp((pa-1.3f)/1.5f);
            // ---- circuits it has killed ----
            if(v.circuitMask!=0)for(int i=0;i<Lamp.Length;i++)if((v.circuitMask>>NativeHumArena.CircuitOf(i)&1)!=0)Lamp[i]=0;
            // ---- the arrival ----
            if(age>=0&&age<b.Hold)Arrival(age,b,reduced);
            else if(age>=b.Hold&&age<b.Hold+1.2f){float k=age-b.Hold;for(int i=0;i<Lamp.Length;i++)Lamp[i]*=1+(reduced?.4f:1.1f)*(float)Math.Exp(-k*3.4f);Shake=Math.Max(Shake,Impact(k,.8f));Dust=Math.Max(Dust,Fade(k,1.2f));}
            // ---- Row Burst: the rows brighten and buzz, flash, burst and stay dark a while (they come back stuttering) ----
            Rows(v.rowAxis,v.rowMask,v.rowAt,t,reduced,in v);Rows(v.row2Axis,v.row2Mask,v.row2At,t,reduced,in v);
            // ---- the lunge: lamps on the lane dip in sequence through the wind-up, as it travels inside the walls ----
            if(v.action=="lunge"&&HumBossView.At(v.actionAt)&&HumBossView.At(v.laneX0))
            {
                float x0=v.laneX0,z0=v.laneZ0,dx=v.laneX1-x0,dz=v.laneZ1-z0,len=Math.Max(.1f,(float)Math.Sqrt(dx*dx+dz*dz));dx/=len;dz/=len;float hit=HumBossView.At(v.hitAt)?v.hitAt:v.actionAt+1.2f;float wind=hit-v.actionAt;
                for(int i=0;i<Lamp.Length;i++)
                {
                    var f=room.FixtureAt[i];float rx=f.X-x0,rz=f.Z-z0,along=rx*dx+rz*dz,off=Math.Abs(rx*dz-rz*dx);if(off>3.4f||along<-2||along>len+2)continue;
                    float at=v.actionAt+wind*BossMath.Clamp01(along/len)*.95f,k=t-at;float dip=k<-.1f||k>.55f?0:1-Math.Abs(k-.2f)/.35f;Lamp[i]*=1-.9f*BossMath.Clamp01(dip)*(1-off/3.4f*.4f);
                    // And it passes under them at speed.
                    float pass=hit+along/len*(NativeHumBoss.Move("lunge").Strike)-t;if(pass<.12f&&pass>-.14f)Lamp[i]*=1+1.2f*(1-Math.Abs(pass-.0f)/.14f);
                }
            }
            // ---- Max Hum: each lamp flares as the ring passes it; everything strains through the charge ----
            if(HumBossView.At(v.ringAt)&&t>=v.ringAt)
            {
                float speed=NativeHumBoss.Move("pulse").Speed;float r=(t-v.ringAt)*speed;
                for(int i=0;i<Lamp.Length;i++){var f=room.FixtureAt[i];float d=(float)Math.Sqrt((f.X-v.ringX)*(f.X-v.ringX)+(f.Z-v.ringZ)*(f.Z-v.ringZ));float k=(r-d)/speed;if(k>-.05f&&k<.32f)Lamp[i]=Math.Max(Lamp[i],(reduced?1.2f:2.6f)*(1-Math.Abs(k-.05f)/.3f));}
            }
            // ---- Lights Out: everything it holds up goes out, and everything is silent ----
            if(v.action=="cut"&&HumBossView.At(v.actionAt)){float k=t-v.actionAt,hit=HumBossView.At(v.hitAt)?v.hitAt-v.actionAt:1.2f;if(k>=0&&k<hit){Silent=1;for(int i=0;i<Lamp.Length;i++)Lamp[i]=0;}}
            // ---- the Peripheral Shift: every lamp dips as the partitions change ----
            if(HumBossView.At(v.shiftAt)){float k=t-v.shiftAt;if(k>-.06f&&k<.26f){float d=1-.9f*(1-Math.Abs(k-.08f)/.2f);for(int i=0;i<Lamp.Length;i++)Lamp[i]*=BossMath.Clamp01(d);Hitch=Math.Max(Hitch,.5f);}}
            // ---- the death: it takes the room down with it, then silence, then every lamp comes back, one by one, ordinary and quiet ----
            if(v.Dead(t))
            {
                float d=t-v.diedAt;Silent=d>=3.2f?1:0;Hum=.2f*(1-BossMath.Clamp01(d/3.1f));Pitch=.2f*(1-BossMath.Clamp01(d/3.1f));Wrong=0;
                for(int i=0;i<Lamp.Length;i++)
                {
                    float back=DeathReturn+BossMath.Hash(0x71u,i)*1.2f;
                    Lamp[i]=d<.4f?Lamp[i]*(1-d/.4f):d<back?0:(d<back+.16f&&!reduced?Stutter(i,t,reduced):1);Burst[i]=false;
                }
            }
            // ---- what the room does with it ----
            float mean=0;for(int i=0;i<Lamp.Length;i++)mean+=Math.Min(Lamp[i],1.6f);mean/=Lamp.Length;Air=mean;
            // The hum: rises through every wind-up, its pitch says how soon.
            if(v.action!=null&&HumBossView.At(v.actionAt)&&HumBossView.At(v.hitAt)&&!v.Dead(t)){float u=(t-v.actionAt)/Math.Max(.05f,v.hitAt-v.actionAt);if(u>=0&&u<1){Hum=Math.Max(Hum,.25f+.75f*u);Pitch=Math.Max(Pitch,.2f+.8f*u*u);}else if(u>=1&&t<v.hitAt+.3f)Pitch=Math.Max(Pitch,1-(t-v.hitAt)/.3f);}
            if(pa<NativeHumBoss.PhaseChangeSeconds&&phase==2){float u=pa/2.1f;if(pa<2.25f){Hum=Math.Max(Hum,Math.Min(1,.3f+.7f*u));Pitch=Math.Max(Pitch,Math.Min(1,.3f+.7f*u));Shake=Math.Max(Shake,.3f*Ramp(u));}else{Silent=Math.Max(Silent,1-Ramp((pa-2.6f)/.5f));Hum*=Ramp((pa-2.6f)/.5f);}}
            if(pa<NativeHumBoss.PhaseChangeSeconds&&phase>=3){if(pa>1.3f&&pa<2.8f){Shake=Math.Max(Shake,.9f*Impact(pa-1.5f,1.2f));Dust=Math.Max(Dust,Fade(pa-1.3f,2.4f));}Slip=Ramp((pa-1.6f)/1.6f);}
            else if(phase>=3)Slip=v.Dead(t)?1-Ramp((t-v.diedAt-2.2f)/1f):1;
            // The flicker hitch when lamps stutter.
            if(!reduced&&!v.Dead(t)&&phase>=2&&age>b.Hold+1){float k=BossMath.Hash(0x51u,(int)Math.Floor(t*5));if(k>.82f)Hitch=Math.Max(Hitch,(k-.82f)*5.5f*(phase>=3?1:.55f));}
            // The impacts it adds to the room.
            if(v.action=="stoop"&&HumBossView.At(v.hitAt)){float k=t-v.hitAt;if(k>=0){Shake=Math.Max(Shake,.8f*Impact(k,.7f));Dust=Math.Max(Dust,.7f*Fade(k,1.2f));}}
            if(HumBossView.At(v.rowAt)){float k=t-v.rowAt;if(k>=0&&k<.9f){Shake=Math.Max(Shake,.5f*Impact(k,.6f));Dust=Math.Max(Dust,.5f*Fade(k,.9f));}}
            if(HumBossView.At(v.staggerAt)&&t>=v.staggerAt)Shake=Math.Max(Shake,(v.staggerCause=="choke"?.8f:.5f)*Impact(t-v.staggerAt,.7f));
            if(v.Dead(t)&&t-v.diedAt<2.4f)Shake=Math.Max(Shake,.4f*Impact(t-v.diedAt-1.9f,.5f));
        }
        /// <summary>When the first lamp returns after the death (s); inside the 4 s hold the room is just lighting up.</summary>
        public const float DeathReturn=3.15f;
        private void Arrival(float a,in HumBossBeats b,bool reduced)
        {
            // Row 5 is the far row (behind the boss's wall side). The far rows go out, nearer and nearer, with a stutter as each dies; a single dying fixture stays over the tear so the wall can be seen; dark from the walk out; all slam on at the drop.
            for(int i=0;i<Lamp.Length;i++)
            {
                int row=i/NativeHumArena.FixtureSide,col=i%NativeHumArena.FixtureSide;float die=row>=2?b.Row(5-row):b.WalkOut+(1-row)*.06f;
                float lvl=a<die-.16f?1:a<die?(reduced?.6f:Stutter(i,a,false)):0;
                // The last lamp over the wall, failing but holding on, until the fingers are through.
                if(row==5&&(col==2||col==3)&&a>=die&&a<b.WalkOut)lvl=col==2?(reduced?.8f:.55f+.45f*Stutter(i,a,false)):.04f;
                Lamp[i]=lvl;
            }
            if(a<b.Hold)
            {
                // The hum: it rises alone over the silence, strains through the head's flicker, peaks as the jaw splits.
                Hum=Math.Min(1,.4f+.6f*BossMath.Clamp01(a/b.Hold));Pitch=Math.Min(1,.15f+.85f*BossMath.Clamp01(a/b.Hold));
                if(a>=b.Fingers&&a<b.TearOpen+.5f)Shake=Math.Max(Shake,.08f);
                if(a>=b.Straighten&&a<b.Stoop+.2f){float k=a-b.Straighten-.22f;Shake=Math.Max(Shake,.45f*Impact(k,.8f));Dust=Math.Max(Dust,.7f*Fade(k,1.3f));}
            }
        }
        private void Rows(int axis,int mask,float at,float t,bool reduced,in HumBossView v)
        {
            if(mask==0||!HumBossView.At(at))return;
            float wind=NativeHumBoss.Move("row").Wind,k=t-at;if(k<-wind||k>RowDark+.5f)return;
            for(int i=0;i<Lamp.Length;i++)
            {
                if((mask>>NativeHumArena.RowOf(i,axis)&1)==0)continue;
                if(k<0){float u=(k+wind)/wind;float buzz=reduced?1:(.92f+.08f*(float)Math.Sin(t*73+i));Lamp[i]=Math.Max(Lamp[i],1+(reduced?.8f:2.1f)*u*u)*buzz;}
                else if(k<.05f)Lamp[i]=reduced?1.8f:4f;
                else if(k<RowDark){Lamp[i]=0;Burst[i]=true;}
                else{float h=(k-RowDark)/.5f;Lamp[i]=reduced?h*Lamp[i]:(Stutter(i,t,false)<.5f?0:Lamp[i]*h);Burst[i]=false;}
            }
        }
        /// <summary>How long a burst row stays dark (s).</summary>
        public const float RowDark=5.5f;
        // BLACKOUT: dark, but for one or two circuits flickering somewhere, wandering over the room.
        private static float BlackoutLamp(int i,float t,bool reduced)
        {
            int c=NativeHumArena.CircuitOf(i);int slot=(int)Math.Floor(t/3.1f);int alive=(int)(BossMath.Hash(0xB1u,slot)*NativeHumArena.Circuits),second=(int)(BossMath.Hash(0xB2u,slot)*NativeHumArena.Circuits);
            if(c!=alive&&c!=second)return 0;float env=BossMath.Clamp01(Math.Min(t/3.1f-slot,slot+1-t/3.1f)*6);return (reduced?.3f:.46f*Stutter(i,t,false))*env*(c==alive?1:.6f);
        }
        // NOCLIP: every lamp back, wrong: slow rolling waves that never agree, hot and dim. Reduced: steady.
        private static float NoclipLamp(int i,float t,bool reduced)
        {
            if(reduced)return .75f;float row=i/NativeHumArena.FixtureSide,col=i%NativeHumArena.FixtureSide;float w=(float)Math.Sin(t*1.7f+row*.9f-col*.6f)*.5f+.5f,u=(float)Math.Sin(t*.61f+col*1.3f+row*.21f)*.5f+.5f;
            int k=(int)Math.Floor(t*7);float dart=BossMath.Hash((uint)i*97u+3u,k);return (.35f+.8f*w*u)*(dart<.1f?.2f:1);
        }

        // ---------------- the room's lights for the level's eight practical slots ----------------
        /// <summary>The boss's own lights first (the fixture, its tubes, the Ballast: up to <paramref name="own"/> entries of pos x y z range, rgb linear), then the brightest near lamps. Slots past the count are dark.</summary>
        public int PickLights(HumRoom room,Vector3 eye,int slots,float[] own,float[] ownRgb,int ownCount,Vector3 lamp,float range,float[] pos,float[] rgb)
        {
            int n=0;
            for(int k=0;k<ownCount&&n<slots;k++)
            {
                pos[n*4]=own[k*4];pos[n*4+1]=own[k*4+1];pos[n*4+2]=own[k*4+2];pos[n*4+3]=own[k*4+3];rgb[n*3]=ownRgb[k*3];rgb[n*3+1]=ownRgb[k*3+1];rgb[n*3+2]=ownRgb[k*3+2];n++;
            }
            int want=slots-n;Span<float> best=stackalloc float[16];Span<int> at=stackalloc int[16];int count=0;want=Math.Min(want,16);
            for(int i=0;i<Lamp.Length;i++)
            {
                float lv=Lamp[i];if(lv<.015f)continue;var f=room.FixtureAt[i];float dx=f.X-eye.X,dz=f.Z-eye.Z,score=(dx*dx+dz*dz+4)/Math.Min(1.6f,lv);
                if(count<want){best[count]=score;at[count]=i;count++;}else{int worst=0;for(int k=1;k<count;k++)if(best[k]>best[worst])worst=k;if(score<best[worst]){best[worst]=score;at[worst]=i;}}
            }
            var tint=Vector3.Lerp(lamp,new Vector3(lamp.X*.62f,lamp.Y*.98f,lamp.Z*1.22f),Wrong);
            for(int k=0;k<count;k++)
            {
                int i=at[k];var f=room.FixtureAt[i];float lv=Lamp[i];pos[n*4]=f.X;pos[n*4+1]=f.Y-.2f;pos[n*4+2]=f.Z;pos[n*4+3]=range*(lv>1?Math.Min(1.5f,.9f+.1f*lv):1);rgb[n*3]=tint.X*lv;rgb[n*3+1]=tint.Y*lv;rgb[n*3+2]=tint.Z*lv;n++;
            }
            for(int k=n;k<slots;k++){pos[k*4]=0;pos[k*4+1]=-10000;pos[k*4+2]=0;pos[k*4+3]=.0001f;rgb[k*3]=rgb[k*3+1]=rgb[k*3+2]=0;}
            return n;
        }
    }
}
