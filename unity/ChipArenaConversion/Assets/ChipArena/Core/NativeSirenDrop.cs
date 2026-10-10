using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Presentation of a Last Laugh death after the crush (client/siren/kills.ts sirenCrush, drawn from the baked
    /// death-siren-crush clip): the headless body hangs limp in the claw, then is dropped. The drop is a physics ragdoll
    /// (Runtime NativeCorpse) handed the exact pose the clip shows at the release and the clip's own motion there, plus a
    /// small seeded tumble, instead of the clip's scripted crumple (which the native root kept at claw height, so the body
    /// lay in mid-air). Everything here is a pure function of the replicated death (position, yaw, time, seed) and the clip,
    /// in the source's axes (three.js, x mirrored against Unity); matrices are column-major float[16] like the clip's
    /// values. The release, the clip timeline before it, damage, hitboxes and the grab authority are untouched.
    /// </summary>
    public static class NativeSirenDrop
    {
        /// <summary>kills.ts: the limp hang before the drop while the Siren still holds the body.</summary>
        public const float Hang=.45f;
        /// <summary>The claw has already let go (the native simulation ends the Siren's hold at the crush): one limp beat,
        /// after the clip's .08 s carry, so the handed-over pose is the clip's authored hang and never its first-frame rest.</summary>
        public const float Limp=.1f;
        /// <summary>kills.ts DeathPoseCarry window (the clip's own, and SourceDeathScene's carry from the live rig).</summary>
        public const float Carry=.08f;
        /// <summary>kills.ts SIREN_CRUSH_KEEP: a kept (PvE downed) body's clip age.</summary>
        public const float Keep=2.6f;
        public const float ApartAt=3.2f,ApartSeconds=1.2f;
        /// <summary>The finite-difference step for the release motion: one baked frame (the clip is sampled at 30 fps).</summary>
        public const float Step=1/30f;
        /// <summary>The release tumble: a backward tip (rad/s, about the body's right axis) and a backward push (m/s). The hanging
        /// body slumps forward; the tip has it leaning back when its feet reach the floor, so it folds over backward as it lands
        /// instead of standing on its feet, or squatting on its heels, for a beat before it topples.</summary>
        public const float Tip=3.2f,Push=.5f;
        /// <summary>The knees give at the release (rad/s, seeded ±30 % per leg), so the legs fold on impact. A much larger bend (or a
        /// hip flex) leaves the body sitting on its heels.</summary>
        public const float KneeGive=.6f;
        /// <summary>kills.ts burnRig joints: head, chest, spine, pelvis, arms, elbows, legs, knees.</summary>
        public static readonly int[] BurnJoints={CapturedRig.Head,CapturedRig.Chest,CapturedRig.Spine,CapturedRig.Pelvis,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,CapturedRig.RElbow,CapturedRig.LHip,CapturedRig.RHip,CapturedRig.LKnee,CapturedRig.RKnee};

        /// <summary>When the body leaves the claw: the source's hang while the Siren still shows the hold, else at once.</summary>
        public static float Release(bool holding)=>holding?Hang:Limp;
        /// <summary>kills.ts: the body comes apart into black flame (shrinks to nothing) from 3.2 to 4.4 s.</summary>
        public static float Gone(float age)=>MathUtil.Smooth((age-ApartAt)/ApartSeconds);
        /// <summary>kills.ts burnRig rate (cubes per second) at body age <paramref name="age"/>; <paramref name="keeping"/>: a
        /// kept body past its held age smoulders at 30 %.</summary>
        public static double BurnRate(float age,double quality,bool keeping)
        {double gone=Gone(age);return ((quality<.5?30:70)*(age<3.6f?1:1-gone)+(age>ApartAt&&age<ApartAt+ApartSeconds?120*(1-gone):0))*(keeping?.3:1);}

        /// <summary>The baked track's matrix at <paramref name="age"/> (NativeSceneMotion.Matrix before its axis flip).</summary>
        public static void Sample(float[] values,int frames,int track,float duration,float age,bool voxel,float[] o,int at=0)
        {float blend=NativeIntroRules.StageBlend(values,frames,track,duration,age,voxel,out int a,out int b);for(int i=0;i<16;i++)o[at+i]=values[a+i]+(values[b+i]-values[a+i])*blend;}
        /// <summary>The clip root for a death at Unity position (x, y, z) facing Unity yaw (degrees), in source axes.</summary>
        public static void Root(float x,float y,float z,float yaw,float[] o)
        {
            double r=-yaw*Math.PI/180;float c=(float)Math.Cos(r),s=(float)Math.Sin(r);
            Array.Clear(o,0,16);o[0]=c;o[2]=-s;o[5]=1;o[8]=s;o[10]=c;o[12]=-x;o[13]=y-.865f;o[14]=z;o[15]=1;
        }
        /// <summary>o = a·b (column-major).</summary>
        public static void Mul(float[] a,int ai,float[] b,int bi,float[] o,int oi)
        {
            for(int c=0;c<4;c++)for(int r=0;r<4;r++){float v=0;for(int k=0;k<4;k++)v+=a[ai+k*4+r]*b[bi+c*4+k];o[oi+c*4+r]=v;}
        }

        /// <summary>
        /// The handover: <paramref name="bind"/> (22×16) is the clip's victim pose at <paramref name="release"/> under
        /// <paramref name="root"/>, exactly what is drawn there; <paramref name="linear"/> and <paramref name="angular"/> (22×3)
        /// are each bone's motion over one baked frame (the last one, or the next one when the last still falls inside the clip's
        /// own .08 s carry from its rest pose, which is not motion) plus the seeded tumble (a rigid spin about the pelvis and a push)
        /// and the knees giving (<see cref="Give"/>).
        /// </summary>
        public static void Handoff(float[] values,int frames,float duration,int[] victim,bool[] voxel,float[] root,float release,int seed,float[] bind,float[] linear,float[] angular)
        {
            var m=new float[16];var before=new float[22*16];var after=new float[22*16];float from=release-Step,to=release;if(from<Carry){from=release;to=release+Step;}
            for(int b=0;b<22;b++)
            {
                Sample(values,frames,victim[b],duration,release,voxel[victim[b]],m);Mul(root,0,m,0,bind,b*16);
                Sample(values,frames,victim[b],duration,from,voxel[victim[b]],m);Mul(root,0,m,0,before,b*16);
                Sample(values,frames,victim[b],duration,to,voxel[victim[b]],m);Mul(root,0,m,0,after,b*16);
            }
            var spin=new float[3];var push=new float[3];Tumble(seed,root,spin,push);
            int p=CapturedRig.Pelvis*16;float px=bind[p+12],py=bind[p+13],pz=bind[p+14];
            for(int b=0;b<22;b++)
            {
                int i=b*16;float rx=bind[i+12]-px,ry=bind[i+13]-py,rz=bind[i+14]-pz;
                linear[b*3]=(after[i+12]-before[i+12])/Step+push[0]+spin[1]*rz-spin[2]*ry;
                linear[b*3+1]=(after[i+13]-before[i+13])/Step+push[1]+spin[2]*rx-spin[0]*rz;
                linear[b*3+2]=(after[i+14]-before[i+14])/Step+push[2]+spin[0]*ry-spin[1]*rx;
                Turn(before,i,after,i,out float wx,out float wy,out float wz);
                angular[b*3]=wx/Step+spin[0];angular[b*3+1]=wy/Step+spin[1];angular[b*3+2]=wz/Step+spin[2];
            }
            Give(seed,bind,linear,angular);
        }
        /// <summary>
        /// The knees give (<see cref="KneeGive"/>): per leg, a seeded bend (knee rotation.x above 0, the only way the knee hinge
        /// opens) as a rigid turn of the shin and foot about the knee's own x axis, pivoting on the knee, added to the handed-over
        /// motion. The pose itself is untouched (the handoff stays exactly the drawn pose).
        /// </summary>
        public static void Give(int seed,float[] bind,float[] linear,float[] angular)
        {
            uint s=unchecked((uint)seed*2246822519u^0x85ebca6bu)|1u;
            double Next(){s^=s<<13;s^=s>>17;s^=s<<5;return s/4294967296.0;}
            foreach(int knee in new[]{CapturedRig.LKnee,CapturedRig.RKnee})
            {
                int j=knee*16;float ax=bind[j],ay=bind[j+1],az=bind[j+2],l=(float)Math.Sqrt(ax*ax+ay*ay+az*az),rate=KneeGive*(float)(.7+.6*Next());if(!(l>1e-6f))continue;
                float wx=ax/l*rate,wy=ay/l*rate,wz=az/l*rate;
                for(int b=knee;b<=knee+2;b++)
                {
                    int i=b*16;float rx=bind[i+12]-bind[j+12],ry=bind[i+13]-bind[j+13],rz=bind[i+14]-bind[j+14];
                    angular[b*3]+=wx;angular[b*3+1]+=wy;angular[b*3+2]+=wz;
                    linear[b*3]+=wy*rz-wz*ry;linear[b*3+1]+=wz*rx-wx*rz;linear[b*3+2]+=wx*ry-wy*rx;
                }
            }
        }
        /// <summary>The seeded release tumble: a backward tip about the body's right axis, a little roll and twist, and a
        /// backward push, from the death seed and the root's facing (source axes).</summary>
        public static void Tumble(int seed,float[] root,float[] spin,float[] push)
        {
            uint s=unchecked((uint)seed*2654435761u^0x9e3779b9u)|1u;
            double Next(){s^=s<<13;s^=s>>17;s^=s<<5;return s/4294967296.0;}
            float fx=root[8],fz=root[10],l=(float)Math.Sqrt(fx*fx+fz*fz);if(l<1e-6f){fx=0;fz=1;}else{fx/=l;fz/=l;}
            // forward × up = the axis that tips the head backward (away from the face).
            float tip=Tip*(float)(.8+.4*Next()),roll=(float)((Next()-.5)*.9),twist=(float)((Next()-.5)*1.2),back=Push*(float)(.6+.8*Next());
            spin[0]=-fz*tip+fx*roll;spin[1]=twist;spin[2]=fx*tip+fz*roll;
            push[0]=-fx*back;push[1]=0;push[2]=-fz*back;
        }
        /// <summary>The rotation vector (axis × angle) taking matrix a's rotation to matrix b's (b·aᵀ, columns normalised).</summary>
        public static void Turn(float[] a,int ai,float[] b,int bi,out float x,out float y,out float z)
        {
            double[] ra=new double[9],rb=new double[9],r=new double[9];Basis(a,ai,ra);Basis(b,bi,rb);
            // r = rb · raᵀ (row-major 3×3 from column-major bases).
            for(int i=0;i<3;i++)for(int j=0;j<3;j++){double v=0;for(int n=0;n<3;n++)v+=rb[n*3+i]*ra[n*3+j];r[i*3+j]=v;}
            double qw,qx,qy,qz,tr=r[0]+r[4]+r[8];
            if(tr>0){double s=Math.Sqrt(tr+1)*2;qw=.25*s;qx=(r[7]-r[5])/s;qy=(r[2]-r[6])/s;qz=(r[3]-r[1])/s;}
            else if(r[0]>r[4]&&r[0]>r[8]){double s=Math.Sqrt(1+r[0]-r[4]-r[8])*2;qw=(r[7]-r[5])/s;qx=.25*s;qy=(r[1]+r[3])/s;qz=(r[2]+r[6])/s;}
            else if(r[4]>r[8]){double s=Math.Sqrt(1+r[4]-r[0]-r[8])*2;qw=(r[2]-r[6])/s;qx=(r[1]+r[3])/s;qy=.25*s;qz=(r[5]+r[7])/s;}
            else{double s=Math.Sqrt(1+r[8]-r[0]-r[4])*2;qw=(r[3]-r[1])/s;qx=(r[2]+r[6])/s;qy=(r[5]+r[7])/s;qz=.25*s;}
            if(qw<0){qw=-qw;qx=-qx;qy=-qy;qz=-qz;}double sn=Math.Sqrt(qx*qx+qy*qy+qz*qz),angle=2*Math.Atan2(sn,qw),k=sn>1e-9?angle/sn:2;
            x=(float)(qx*k);y=(float)(qy*k);z=(float)(qz*k);
        }
        private static void Basis(float[] m,int at,double[] o)
        {for(int c=0;c<3;c++){double x=m[at+c*4],y=m[at+c*4+1],z=m[at+c*4+2],l=Math.Sqrt(x*x+y*y+z*z);if(l<1e-9)l=1;o[c*3]=x/l;o[c*3+1]=y/l;o[c*3+2]=z/l;}}

        /// <summary>
        /// A head gib of the clip (kills.ts: static colour, never the victim, at rest on the bake floor y .04 by the end):
        /// <paramref name="launch"/> gets its start (0-2) and launch velocity (3-5) from the first two baked frames.
        /// </summary>
        public static bool Gib(float[] values,int frames,int track,float duration,bool voxel,bool victim,float[] launch)
        {
            if(victim||voxel||frames<2)return false;int s=track*frames*20;if(!(values[s+16]>.001f)||values[s+13]<.5f)return false;
            float lo=float.PositiveInfinity;for(int f=0;f<frames;f++){int i=s+f*20;if(values[i+16]>.001f)lo=Math.Min(lo,values[i+13]);}
            if(!(lo>.035f&&lo<.045f))return false;float dt=duration/(frames-1);
            launch[0]=values[s+12];launch[1]=values[s+13];launch[2]=values[s+14];
            launch[3]=(values[s+32]-launch[0])/dt;launch[4]=(values[s+33]-launch[1]+6*dt*dt)/dt;launch[5]=(values[s+34]-launch[2])/dt;return true;
        }
        /// <summary>kills.ts gib flight (12 m/s² fall, floor .04, a .6 slide once down) with the floor lowered by
        /// <paramref name="drop"/>: the clip's floor stands at the claw-height root, the arena floor below it.</summary>
        public static void GibAt(float[] launch,int at,float age,float drop,out float x,out float y,out float z)
        {
            float floor=.04f-Math.Max(0,drop);y=launch[at+1]+launch[at+4]*age-6*age*age;bool down=y<floor;if(down)y=floor;float k=down?.6f:1;
            x=launch[at]+launch[at+3]*age*k;z=launch[at+2]+launch[at+5]*age*k;
        }

        /// <summary>The baked frame StageBlend samples at <paramref name="age"/>.</summary>
        public static int Frame(int frames,float duration,float age){float u=age/duration;if(!(u>0))u=0;else if(u>1)u=1;return Math.Min(frames-1,(int)(u*(frames-1)));}
        /// <summary>For every track and frame, the frame its current particle appeared on (-1: nothing shown).</summary>
        public static short[] Births(float[] values,int frames,int tracks,bool[] voxel)
        {
            var o=new short[tracks*frames];
            for(int t=0;t<tracks;t++)
            {
                short born=-1;
                for(int k=0;k<frames;k++)
                {
                    int i=(t*frames+k)*20;bool live=values[i+16]>.001f&&values[i]*values[i]+values[i+1]*values[i+1]+values[i+2]*values[i+2]>1e-12f;
                    if(!live)born=-1;else if(born<0||!NativeIntroRules.StageContinuous(values,i-20,i,voxel[t]))born=(short)k;
                    o[t*frames+k]=born;
                }
            }
            return o;
        }
        /// <summary>
        /// A baked flame cube is drawn while the body is in the clip; after the release only the tongues already burning
        /// then play out (they were emitted on the hanging body). Later ones followed the clip's scripted body, so the
        /// runtime emits them off the ragdoll instead (<see cref="Burn"/>).
        /// </summary>
        public static bool FlameShown(short[] births,int frames,float duration,int track,float age,float release)
        {
            if(age<release)return true;int born=births[track*frames+Frame(frames,duration,age)];return born>=0&&born<=Frame(frames,duration,release);
        }
        /// <summary>
        /// kills.ts burnRig off posed bones (<paramref name="joints"/>: 22 source-axis world matrices, column-major):
        /// <paramref name="rate"/> cubes per second over <paramref name="dt"/>, carried in <paramref name="acc"/>.
        /// </summary>
        public static double Burn(NativeFlameField flames,float[] joints,double dt,double rate,double acc,double size=1)
        {
            acc+=rate*dt;if(acc<1)return acc;var random=flames.Random;
            while(acc>=1)
            {
                acc-=1;int j=BurnJoints[Math.Min(BurnJoints.Length-1,(int)(random()*BurnJoints.Length))]*16;
                double lx=(random()-.5)*.22,ly=(random()-.5)*.22,lz=(random()-.5)*.22;
                double x=joints[j]*lx+joints[j+4]*ly+joints[j+8]*lz+joints[j+12],y=joints[j+1]*lx+joints[j+5]*ly+joints[j+9]*lz+joints[j+13],z=joints[j+2]*lx+joints[j+6]*ly+joints[j+10]*lz+joints[j+14];
                double vx=(random()-.5)*.3,vy=1+random()*.9,vz=(random()-.5)*.3,life=.5+random()*.4,edge=(.07+random()*.07)*size;
                var tone=random()<.05?NativeFlameTone.Ember:random()<.15?NativeFlameTone.Smoke:NativeFlameTone.Black;
                flames.Emit(x,y,z,vx,vy,vz,life,edge,tone,2.6,2);
            }
            return acc;
        }
        /// <summary>Seconds to fall <paramref name="height"/> metres under the cosmetic bodies' 18 m/s² gravity.</summary>
        public static float FallSeconds(float height)=>(float)Math.Sqrt(2*Math.Max(0,height)/18.0);
    }
}
