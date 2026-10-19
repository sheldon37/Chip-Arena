#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The zombie locomotion pass (owner 2026-10-19: "I think I want to change the zombie animations for their movement, their arms
    // also seem to detach slightly ... it needs to be more fluid. Currently, it's still like rapid back and forth movement that
    // doesn't look realistic."; docs/ZOMBIE-LOCOMOTION.md). Presentation only. Holds:
    //  - the cadence is the body's (Core/NativeHordeLoco.StepRate): every take of a kind steps at the same rate at a speed, the
    //    walkers step under 3.3 times a second at their walk (the shipped mapping: 3.4 to 4.4) and no take runs past 2.2 times its
    //    capture's pace there (shipped: 2 to 2.8);
    //  - fluid: through the real gait at each kind's walk and run, the trunk's and arms' joints turn slower, the trunk swings less
    //    and the hands' quick shiver (energy above two and a half times the stride's frequency) stays small;
    //  - blends are phase-continuous: the phase only moves forward, its rate has no step, the walk-to-run blend moves no faster than
    //    NativeHordeGait.BlendSlew and a jostled speed (the crowd's separation) barely moves it;
    //  - planted feet stay put on the drawn rig at walk, run, turning, setting off and stopping;
    //  - the arms stay attached: on every kind's built mesh, at rest and through its walk and run, the shoulder block touches the
    //    chest (the shipped clavicles left 2 to 10 cm of air on eleven of sixteen kinds) and the upper arm its shoulder block;
    //  - nothing allocates per frame; the authority and the network never read any of it.
    internal static class ZombieLocoChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie locomotion: "+what);count++;}
        private static readonly int[] Walkers={NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Brute,NativeHordeKinds.Spitter,NativeHordeKinds.Bloater};
        public static void Run(string root)
        {
            Cadence(out string cadence);Fluid(out string fluid);Blends(out string blends);Feet(out string feet);Shoulders(out string shoulders);Allocation();Source(root);
            Console.WriteLine("PASS zombie locomotion ("+count+" checks: "+cadence+"; "+fluid+"; "+blends+"; "+feet+"; "+shoulders+"; no allocation; presentation only)");
        }
        private static IEnumerable<int> Captured()=>Enumerable.Range(0,NativeHordeKinds.All).Where(k=>k!=NativeHordeKinds.Crawler&&k!=NativeHordeKinds.Hulk&&NativeZombieMotion.Captured(k));
        private static float Rig(int k,float v)=>v/NativeHordeKinds.Scale[NativeHordeKinds.Index(k)];

        // ---- the body's cadence, whatever take it walks on ----
        private static void Cadence(out string summary)
        {
            float most=0,mostRate=0;
            foreach(int k in Captured())
            {
                var st=NativeHordeKinds.ProposedRow(k);
                foreach(float world in new[]{st.walk,(st.walk+st.run)*.5f,st.run})
                {
                    float v=Rig(k,world),body=NativeHordeGait.StepRate(k,v);
                    foreach(int c in NativeZombieMotion.Walks(k).Concat(NativeZombieMotion.Runs(k)))
                    {
                        float r=NativeZombieMotion.Rate(c,v,k),steps=2*NativeZombieMotion.Cadence(c,v,k);
                        bool clamped=r<=NativeZombieMotion.MinRate+1e-4f||r>=NativeZombieMotion.MaxRate-1e-4f;
                        Check(clamped||Math.Abs(steps-body)<1e-3f,NativeHordeKinds.Ids[k]+" on "+NativeZombieClips.Key(c)+" steps at the body's cadence ("+steps.ToString("0.00")+" against "+body.ToString("0.00")+")");
                    }
                    Check(v/body<=NativeHordeGait.RunStepMost+1e-3f,NativeHordeKinds.Ids[k]+": no step longer than the legs carry ("+(v/body).ToString("0.00")+" m)");
                }
                for(float v=.2f;v<5;v+=.1f)Check(NativeHordeGait.StepRate(k,v+.1f)>NativeHordeGait.StepRate(k,v),"the cadence rises with the speed");
            }
            foreach(int k in Walkers)
            {
                var st=NativeHordeKinds.ProposedRow(k);float v=st.walk,steps=NativeHordeGait.StepRate(k,v);
                if(k!=NativeHordeKinds.Runner){Check(steps<3.3f,NativeHordeKinds.Ids[k]+" walks under 3.3 steps a second ("+steps.ToString("0.00")+"; the shipped mapping took 3.4 to 4.4)");most=Math.Max(most,steps);}
                foreach(int c in NativeZombieMotion.Walks(k)){float r=NativeZombieMotion.Rate(c,v,k);Check(r<=2.2f,NativeHordeKinds.Ids[k]+"'s "+NativeZombieClips.Key(c)+" plays at most 2.2 times its capture's pace at the walk ("+r.ToString("0.00")+")");mostRate=Math.Max(mostRate,r);}
            }
            Check(NativeHordeGait.CalmGain(1)==1&&NativeHordeGait.CalmGain(2)<.7f&&NativeHordeGait.CalmGain(10)>=NativeHordeGait.CalmFloor,"a take at its own pace is drawn as captured; a faster one is calmed, never flattened");
            summary="the walkers step at most "+most.ToString("0.00")+" times a second at their walk, no take past "+mostRate.ToString("0.00")+" times its pace";
        }

        // ---- one add driven like the runtime's presented root (Flow): speed and facing over time ----
        private sealed class Drive
        {
            public readonly NativeHordeGait G=new NativeHordeGait();public readonly CapturedRig.Pose P=new CapturedRig.Pose();public HordeMotionInput F;public Vector3 At;public float Yaw;public int Kind;
            public Drive(int kind,int id,int look=0){Kind=kind;F=new HordeMotionInput{Kind=kind,Look=look,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full};At=new Vector3(0,.85f,0);}
            public void Step(float t,float dt,float speed,float turnRate)
            {
                Yaw+=turnRate*dt;float r=Yaw*(float)Math.PI/180;var v=new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r))*speed;At+=v*dt;
                F.Time=t;F.Root=At;F.Velocity=v;F.Yaw=Yaw;F.YawRate=-turnRate*(float)Math.PI/180;F.Ground=0;G.Pose(F,P);
            }
        }
        private static readonly int[] Upper={CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.RClav,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,CapturedRig.RElbow};
        private static float Ang(Quaternion a,Quaternion b)=>2*(float)Math.Acos(Math.Min(1,Math.Abs(Quaternion.Dot(Quaternion.Normalize(a),Quaternion.Normalize(b)))));
        private static float P95(List<float> a){var o=a.OrderBy(x=>x).ToArray();return o.Length==0?0:o[(int)(.95*(o.Length-1))];}
        /// <summary>A kind at a world speed for 6 s at 60 fps (straight, then turning 20 deg/s): the trunk's and arms' joint speed
        /// (the p95 averaged over the ten upper joints), the chest's yaw rate p95, and the hands' energy above 2.5 times the stride's
        /// frequency (their velocity in the body's frame).</summary>
        private static void Measure(int k,float world,out float joints,out float trunk,out float shiver,out float steps,out bool sprinting)
        {
            var d=new Drive(k,90000+k*7);const float dt=1/60f;int n=360;var q=new Quaternion[n,Upper.Length];var yaw=new float[n];var hand=new Vector3[n,2];float sc=NativeHordeKinds.Scale[NativeHordeKinds.Index(k)];float p0=0;
            for(int i=0;i<n+120;i++)
            {
                float t=i*dt;d.Step(t,dt,Rig(k,world),t>4?20:0);if(i<120){p0=d.G.Phase;continue;}int j=i-120;var p=d.P;p.World();
                for(int u=0;u<Upper.Length;u++)q[j,u]=p.Rotation[Upper[u]];var f=Vector3.Transform(Vector3.UnitZ,p.WorldRotation[CapturedRig.Chest]);yaw[j]=(float)Math.Atan2(f.X,f.Z);
                hand[j,0]=p.WorldPosition[CapturedRig.LHand];hand[j,1]=p.WorldPosition[CapturedRig.RHand];
            }
            steps=(d.G.Phase-p0)/(n*dt)*2;float cycle=steps*.5f;
            // (On a sprint take, the pack's HyperChase, at any share of the blend: its trunk swings as a sprinter's does.)
            sprinting=d.G.RunNow>.05f&&NativeZombieClips.Speed(d.G.RunClip)>1||NativeZombieClips.Speed(d.G.WalkClip)>1;
            var all=new List<float>();for(int u=0;u<Upper.Length;u++){var w=new List<float>();for(int j=1;j<n;j++)w.Add(Ang(q[j,u],q[j-1,u])/dt);all.Add(P95(w));}joints=all.Average();
            var yr=new List<float>();for(int j=1;j<n;j++){float a=yaw[j]-yaw[j-1];if(a>Math.PI)a-=2*(float)Math.PI;if(a<-Math.PI)a+=2*(float)Math.PI;yr.Add(Math.Abs(a)/dt);}trunk=P95(yr);
            double hi=0,tot=0;for(int h=0;h<2;h++)for(int b=1;b<(n-1)/2;b++){double fq=b/((n-1)*dt);if(fq>20)break;double re=0,im=0,re2=0,im2=0,re3=0,im3=0;for(int j=1;j<n;j++){var v=(hand[j,h]-hand[j-1,h])/dt;double a=-2*Math.PI*b*(j-1)/(n-1),c=Math.Cos(a),s=Math.Sin(a);re+=v.X*c;im+=v.X*s;re2+=v.Y*c;im2+=v.Y*s;re3+=v.Z*c;im3+=v.Z*s;}
                double e=re*re+im*im+re2*re2+im2*im2+re3*re3+im3*im3;tot+=e;if(fq>2.5*cycle)hi+=e;}
            shiver=tot>0?(float)(hi/tot):0;
        }
        private static void Fluid(out string summary)
        {
            float worstJ=0,worstT=0,worstS=0;
            foreach(int k in Walkers.Concat(new[]{8,11,12,13,14}))
            {
                var st=NativeHordeKinds.ProposedRow(k);
                foreach(var (gait,world) in new[]{("walk",st.walk),("run",st.run)})
                {
                    Measure(k,world,out float j,out float t,out float s,out float steps,out bool sprint);string what=NativeHordeKinds.Ids[k]+" at its "+gait+" ("+world+" m/s, "+steps.ToString("0.00")+" steps/s"+(sprint?", sprinting":"")+")";
                    // (The shipped gait: the walkers' upper joints turned at 4.2 to 6.7 rad/s p95 at the walk and 4.1 to 8.8 at the run,
                    // the spitter's trunk yawed at 3.7 rad/s walking; a sprint take's trunk swings as a sprinter's, so it has its own bound.)
                    Check(j<(sprint?6f:3f),what+": the trunk and arms turn calmly (upper joints p95 "+j.ToString("0.0")+" rad/s)");
                    Check(t<(sprint?7.5f:2.5f),what+": the trunk does not swing back and forth (chest yaw rate p95 "+t.ToString("0.0")+" rad/s)");
                    Check(s<.35f,what+": the hands carry no quick shiver over the stride ("+(s*100).ToString("0")+"% of their energy above 2.5 times its frequency)");
                    if(!sprint){worstJ=Math.Max(worstJ,j);worstT=Math.Max(worstT,t);}worstS=Math.Max(worstS,s);
                }
            }
            summary="the walkers' upper joints at most "+worstJ.ToString("0.0")+" rad/s p95 and the trunk's yaw "+worstT.ToString("0.0")+" rad/s (sprints apart), the hands' shiver at most "+(worstS*100).ToString("0")+"%";
        }

        // ---- blends: phase forward and continuous, the take blend slewed, a jostle filtered ----
        private static void Blends(out string summary)
        {
            float worstJump=0,worstRateStep=0,blendTravel=0,rawTravel=0;
            foreach(int k in Walkers)
            {
                var st=NativeHordeKinds.ProposedRow(k);var d=new Drive(k,91000+k);const float dt=1/60f;float lastPhase=float.NaN,lastRate=float.NaN,lastRun=float.NaN,lastRaw=float.NaN;var last=new Quaternion[Upper.Length];var rnd=new Random(7+k);
                for(int i=0;i<900;i++)
                {
                    float t=i*dt;
                    // stand, walk, accelerate to the run, hold, ease back to the walk; from 10 s a crowd's jostle (±.45 m/s at 5-8 Hz) on the walk-run boundary
                    float speed=t<1?0:t<3?st.walk:t<4?st.walk+(st.run-st.walk)*(t-3):t<6?st.run:t<7?st.run-(st.run-st.walk)*(t-6):t<10?st.walk:(st.walk+st.run)*.5f+.45f*(float)Math.Sin(t*2*Math.PI*(5+3*rnd.NextDouble()));
                    if(i>=600&&i<900&&t<10)speed=st.walk;
                    d.Step(t,dt,Rig(k,speed),0);var p=d.P;float rate=float.IsNaN(lastPhase)?0:(d.G.Phase-lastPhase)/dt;
                    if(!float.IsNaN(lastPhase))Check(d.G.Phase>=lastPhase-1e-6f,NativeHordeKinds.Ids[k]+": the phase only moves forward");
                    if(i>2&&t>1.5f&&!float.IsNaN(lastRate)){float step=Math.Abs(rate-lastRate);worstRateStep=Math.Max(worstRateStep,step);Check(step<.08f,NativeHordeKinds.Ids[k]+": the cadence never steps ("+step.ToString("0.000")+" cycles/s in a frame at "+t.ToString("0.00")+" s)");}
                    if(!float.IsNaN(lastRun)){float dr=Math.Abs(d.G.RunNow-lastRun);Check(dr<=NativeHordeGait.BlendSlew*dt+1e-4f,NativeHordeKinds.Ids[k]+": the walk-to-run blend moves at most BlendSlew a second");if(t>=10.5f){blendTravel+=dr;rawTravel+=Math.Abs(NativeHordeGait.RunBlend(k,d.G.Speed)-lastRaw);}}
                    if(i>0&&t>1.5f)for(int u=0;u<Upper.Length;u++){float a=Ang(p.Rotation[Upper[u]],last[u]);worstJump=Math.Max(worstJump,a);Check(a<.25f,NativeHordeKinds.Ids[k]+": no joint pops through the blends ("+a.ToString("0.000")+" rad in a frame at "+t.ToString("0.00")+" s)");}
                    for(int u=0;u<Upper.Length;u++)last[u]=p.Rotation[Upper[u]];lastPhase=d.G.Phase;lastRate=rate;lastRun=d.G.RunNow;lastRaw=NativeHordeGait.RunBlend(k,d.G.Speed);
                }
            }
            Check(blendTravel<rawTravel*.35f,"a jostled speed barely moves the take blend ("+blendTravel.ToString("0.00")+" against the unfiltered "+rawTravel.ToString("0.00")+")");
            summary="through walk, run and back the phase rate steps at most "+worstRateStep.ToString("0.000")+" cycles/s a frame and no upper joint turns more than "+worstJump.ToString("0.000")+" rad a frame; a crowd's jostle moves the take blend "+(blendTravel/Math.Max(1e-3f,rawTravel)*100).ToString("0")+"% as far as the raw speed would";
        }

        // ---- planted feet: the drawn ankle (or a rolled foot's ball) stays on its spot ----
        private static void Feet(out string summary)
        {
            float worst=0;
            foreach(int k in Captured())
            {
                var st=NativeHordeKinds.ProposedRow(k);
                foreach(var (name,speedAt,turn) in new (string,Func<float,float>,float)[]{("walk",t=>st.walk,0),("run",t=>st.run,0),("turning walk",t=>st.walk,35),("setting off and stopping",t=>t<1.5f?0:t<4?st.walk*Math.Min(1,(t-1.5f)/.25f):t<5?Math.Max(0,st.walk*(1-(t-4)/.25f)):t<6?st.run*Math.Min(1,(t-5)/.25f):Math.Max(0,st.run*(1-(t-6)/.25f)),0)})
                {
                    var d=new Drive(k,92000+k*13);const float dt=1/60f;var air=new bool[2];var rolled=new bool[2];var ball=new Vector3[2];float bad=0;
                    for(int i=0;i<480;i++)
                    {
                        float t=i*dt;d.Step(t,dt,Rig(k,speedAt(t)),turn);var g=d.G;var p=d.P;
                        for(int j=0;j<2;j++)
                        {
                            if(i>60&&!air[j]&&!g.FootAir[j])
                            {
                                float e;if(g.Heel[j]>0){var b=g.ToWorld(p.WorldPosition[j==0?CapturedRig.LToe:CapturedRig.RToe],d.At);if(!rolled[j]){rolled[j]=true;ball[j]=b;}e=new Vector2(b.X-ball[j].X,b.Z-ball[j].Z).Length();}
                                else{rolled[j]=false;var a=g.ToWorld(p.WorldPosition[j==0?CapturedRig.LFoot:CapturedRig.RFoot],d.At);e=new Vector2(a.X-g.FootWorld[j].X,a.Z-g.FootWorld[j].Z).Length();}
                                bad=Math.Max(bad,e);
                            }
                            else rolled[j]=false;air[j]=g.FootAir[j];
                        }
                    }
                    // (Setting off and stopping inside a quarter second may hold a foot a frame at the hips' deepest crouch.)
                    float most=name.StartsWith("setting")?.02f:.015f;
                    Check(bad<most,NativeHordeKinds.Ids[k]+" "+name+": a planted foot stays on its spot on the drawn rig (worst "+bad.ToString("0.0000")+" m)");worst=Math.Max(worst,bad);
                }
            }
            summary="planted feet within "+(worst*100).ToString("0.0")+" cm of their spot for every captured kind walking, running, turning, setting off and stopping";
        }

        // ---- the arms attached: the shoulder block on the chest, the upper arm in its shoulder block, on the built meshes ----
        private sealed class Body
        {
            public readonly Dictionary<int,(Vector3[] p,Vector3[] n)> Parts=new Dictionary<int,(Vector3[],Vector3[])>();public readonly Dictionary<int,Vector3[]> Verts=new Dictionary<int,Vector3[]>();
            public Body(int kind,int look,int id)
            {
                var m=NativeHordeModels.Build(kind,look,NativeHordeModels.Full);var v=NativeHordeModels.Variation(id);var pr=kind<NativeHordeKinds.Count?NativeHordeModels.Proportions(v.Y-1):Vector3.One;
                var tp=new Dictionary<int,List<Vector3>>();var tn=new Dictionary<int,List<Vector3>>();var vs=new Dictionary<int,List<Vector3>>();
                foreach(var s in m.Mesh.Sections)
                {
                    if(!tp.ContainsKey(s.Bone)){tp[s.Bone]=new List<Vector3>();tn[s.Bone]=new List<Vector3>();vs[s.Bone]=new List<Vector3>();}
                    // (ZombieLit's vertex: the head scaled, the torso widened, the limbs thickened by the add's proportions.)
                    Vector3 P(int i){var p=s.Positions[i];float r=s.Colors[i].W;if(r>.62f&&r<.88f)return p*pr.X;if(r>.37f&&r<.63f)return new Vector3(p.X*pr.Y,p.Y,p.Z);if(r>.12f&&r<.38f)return new Vector3(p.X*pr.Z,p.Y,p.Z*pr.Z);return p;}
                    for(int i=0;i+2<s.Indices.Count;i+=3){tp[s.Bone].Add(P(s.Indices[i]));tp[s.Bone].Add(P(s.Indices[i+1]));tp[s.Bone].Add(P(s.Indices[i+2]));tn[s.Bone].Add(s.Normals[s.Indices[i]]+s.Normals[s.Indices[i+1]]+s.Normals[s.Indices[i+2]]);}
                    for(int i=0;i<s.Positions.Count;i++)vs[s.Bone].Add(P(i));
                }
                foreach(var b in tp.Keys){Parts[b]=(tp[b].ToArray(),tn[b].ToArray());Verts[b]=vs[b].ToArray();}
            }
        }
        // The rig's joints in the model's frame (Unity: x mirrored), row-vector matrices.
        private static readonly Matrix4x4[] world=new Matrix4x4[CapturedRig.Bones.Length];
        private static void Joints(CapturedRig.Pose p)
        {
            for(int i=0;i<world.Length;i++){int parent=CapturedRig.Bones[i].Parent;var l=Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(CapturedRig.Mirror(p.Rotation[i])))*Matrix4x4.CreateTranslation(CapturedRig.Mirror(p.Position[i]));world[i]=parent<0?l:l*world[parent];}
        }
        private static bool Cast(Vector3 o,Vector3 d,(Vector3[] p,Vector3[] n) m,out float best,out bool entering)
        {
            best=float.MaxValue;entering=false;bool any=false;var t=m.p;
            for(int i=0;i+2<t.Length;i+=3)
            {
                var a=t[i];var e1=t[i+1]-a;var e2=t[i+2]-a;var pv=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,pv);if(Math.Abs(det)<1e-9f)continue;float id=1/det;var tv=o-a;float u=Vector3.Dot(tv,pv)*id;if(u<0||u>1)continue;var qv=Vector3.Cross(tv,e1);float vv=Vector3.Dot(d,qv)*id;if(vv<0||u+vv>1)continue;
                float h=Vector3.Dot(e2,qv)*id;if(h<=1e-5f||h>=best)continue;best=h;any=true;entering=Vector3.Dot(m.n[i/3],d)<0;
            }
            return any;
        }
        private static bool Inside(Vector3 p,(Vector3[] p,Vector3[] n) m){foreach(var d in new[]{Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY})if(!Cast(p,d,m,out _,out bool ent)||ent)return false;return true;}
        private static float PointTri(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return ap.Length();
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return bp.Length();
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return (p-(a+d1/(d1-d3)*ab)).Length();
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return cp.Length();
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return (p-(a+d2/(d2-d6)*ac)).Length();
            float va=d3*d6-d5*d4;if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)return (p-(b+(d4-d3)/((d4-d3)+(d5-d6))*(c-b))).Length();
            float den=1/(va+vb+vc);return (p-(a+ab*(vb*den)+ac*(vc*den))).Length();
        }
        /// <summary>How far part `a` (bone) stands off part `b` at the pose: the least distance from a's vertices to b's surface, 0 when one is inside.</summary>
        private static float Apart(Body body,int a,int b)
        {
            if(!body.Verts.ContainsKey(a)||!body.Parts.ContainsKey(b))return 0;var av=body.Verts[a];var m=body.Parts[b];Matrix4x4.Invert(world[b],out var inv);var to=world[a]*inv;
            var pts=new Vector3[av.Length];var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);for(int i=0;i<av.Length;i++){pts[i]=Vector3.Transform(av[i],to);lo=Vector3.Min(lo,pts[i]);hi=Vector3.Max(hi,pts[i]);}
            // (Only b's triangles near a's bounds can be the nearest; the rest are skipped.)
            var near=new List<int>();var c=(lo+hi)*.5f;float rad=(hi-lo).Length()*.5f+.12f;for(int k=0;k+2<m.p.Length;k+=3)if(Vector3.Distance(m.p[k],c)<rad||Vector3.Distance(m.p[k+1],c)<rad||Vector3.Distance(m.p[k+2],c)<rad)near.Add(k);
            float best=float.MaxValue;foreach(var p in pts){float d=float.MaxValue;foreach(int k in near)d=Math.Min(d,PointTri(p,m.p[k],m.p[k+1],m.p[k+2]));best=Math.Min(best,d);if(best<1e-4f)return 0;}
            if(best>1e-3f){int step=Math.Max(1,pts.Length/48);for(int i=0;i<pts.Length;i+=step)if(Inside(pts[i],m))return 0;}
            return best;
        }
        /// <summary>The slit: from the shoulder block's inner face (its vertices nearest the body at its own height), toward the
        /// chest's axis, how far before the chest (0 inside), at the pose `world` holds.</summary>
        private static float Slit(Body body,int clav)
        {
            if(!body.Verts.ContainsKey(clav))return 0;var cv=body.Verts[clav];var rest=new CapturedRig.Pose();rest.Reset();var save=(Matrix4x4[])world.Clone();Joints(rest);
            Matrix4x4.Invert(world[clav],out var ci);var toBody=Vector3.Transform(Vector3.Transform(Vector3.Zero,world[CapturedRig.Chest]),ci);float inward=Math.Sign(toBody.X);Array.Copy(save,world,world.Length);
            var main=cv.Where(p=>Math.Abs(p.Y)<=.062f).ToArray();if(main.Length==0)main=cv;float most=main.Max(p=>p.X*inward);var face=main.Where(p=>p.X*inward>most-.012f).ToArray();var ctr=Vector3.Zero;foreach(var p in face)ctr+=p;ctr/=face.Length;
            Matrix4x4.Invert(world[CapturedRig.Chest],out var chestInv);var c=Vector3.Transform(Vector3.Transform(ctr,world[clav]),chestInv);var dir=new Vector3(-c.X,0,-c.Z);if(dir.LengthSquared()<1e-8f)return 0;dir=Vector3.Normalize(dir);
            var m=body.Parts[CapturedRig.Chest];if(!Cast(c,dir,m,out float h,out bool ent))return Inside(c,m)?0:.6f;return ent?h:0;
        }
        private static void Shoulders(out string summary)
        {
            float worstRest=0,worstMove=0,worstSlit=0,shippedMost=0;int shippedApart=0;
            for(int k=0;k<NativeHordeKinds.All;k++)for(int l=0;l<NativeHordeKinds.Looks[k];l++)
            {
                bool varies=k<NativeHordeKinds.Count;string what=NativeHordeKinds.Ids[k]+" look "+l;float inset=NativeHordeGait.ShoulderInset(k,l),shipped=0;
                foreach(int id in varies?Enumerable.Range(0,12).Select(i=>1000+i*37):new[]{1000})
                {
                    var body=new Body(k,l,id);var pose=new CapturedRig.Pose();pose.Reset();NativeHordeGait.Shoulders(pose,inset);Joints(pose);
                    foreach(int clav in new[]{CapturedRig.LClav,CapturedRig.RClav})
                    {
                        float apart=Apart(body,clav,CapturedRig.Chest),arm=Apart(body,clav+1,clav);worstRest=Math.Max(worstRest,apart);
                        Check(apart<=.002f,what+" (add "+id+"): the shoulder block meets the chest at rest ("+(apart*100).ToString("0.0")+" cm apart)");
                        Check(arm<=.002f,what+" (add "+id+"): the upper arm meets its shoulder block ("+(arm*100).ToString("0.0")+" cm apart)");
                        // (The slit is read where the clavicles are set in: the brute's and the Hulk's broad chests already hold their shoulders.)
                        if(varies&&inset>0&&k!=NativeHordeKinds.Crawler){float s=Slit(body,clav);worstSlit=Math.Max(worstSlit,s);Check(s<=.012f,what+" (add "+id+"): no slit between the shoulder block and the chest ("+(s*100).ToString("0.0")+" cm)");}
                    }
                    // (The shipped clavicles, the rig's own: the gap the owner saw.)
                    pose.Reset();Joints(pose);shipped=Math.Max(shipped,Math.Max(Apart(body,CapturedRig.LClav,CapturedRig.Chest),Apart(body,CapturedRig.RClav,CapturedRig.Chest)));
                    if(varies&&inset>0&&k!=NativeHordeKinds.Crawler)shipped=Math.Max(shipped,Math.Max(Slit(body,CapturedRig.LClav),Slit(body,CapturedRig.RClav)));
                }
                if(inset>0)Check(shipped>.005f||NativeHordeKinds.Base(k)==NativeHordeKinds.Crawler,what+": its inset closes a real gap (the rig's clavicles leave "+(shipped*100).ToString("0.0")+" cm)");
                if(shipped>.015f){shippedApart++;shippedMost=Math.Max(shippedMost,shipped);}
            }
            // In motion: each captured kind's first look walking and running (every 6th frame of 3 s), the clavicles as the capture
            // and the secondary motion move them.
            foreach(int k in Captured())
            {
                var st=NativeHordeKinds.ProposedRow(k);var body=new Body(k,0,1000);
                foreach(float world_ in new[]{st.walk,st.run})
                {
                    var d=new Drive(k,93000+k);const float dt=1/60f;
                    for(int i=0;i<240;i++)
                    {
                        float t=i*dt;d.Step(t,dt,Rig(k,world_),t>2?25:0);if(i<60||i%6!=0)continue;Joints(d.P);
                        foreach(int clav in new[]{CapturedRig.LClav,CapturedRig.RClav}){float a=Apart(body,clav,CapturedRig.Chest);worstMove=Math.Max(worstMove,a);Check(a<=.006f,NativeHordeKinds.Ids[k]+" at "+world_+" m/s: the shoulder block stays on the chest ("+(a*100).ToString("0.0")+" cm apart at "+t.ToString("0.00")+" s)");}
                    }
                }
            }
            Check(shippedApart>=8,"the rig's own clavicles leave a gap on the narrow kinds ("+shippedApart+" looks)");
            summary="every kind's shoulders on its chest at rest (worst "+(worstRest*100).ToString("0.0")+" cm, slit "+(worstSlit*100).ToString("0.0")+" cm; the rig's clavicles left up to "+(shippedMost*100).ToString("0.0")+" cm on "+shippedApart+" looks) and walking and running (worst "+(worstMove*100).ToString("0.0")+" cm)";
        }

        // ---- no allocation per frame ----
        private static void Allocation()
        {
            var adds=new[]{new Drive(0,94001),new Drive(1,94002,1),new Drive(2,94003),new Drive(3,94004,1),new Drive(5,94005),new Drive(7,94006),new Drive(11,94007)};const float dt=1/60f;
            void Frame(int i){float t=i*dt;foreach(var a in adds){var st=NativeHordeKinds.ProposedRow(a.Kind);a.Step(t,dt,Rig(a.Kind,(st.walk+st.run)*.5f+(st.run-st.walk)*.5f*(float)Math.Sin(t*.7f)),i%400<200?0:30);}}
            for(int i=0;i<300;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=300;i<2100;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"the cadence, the calm and the shoulders allocate nothing per frame ("+used+" bytes over 1800 frames of seven adds)");
        }

        // ---- presentation only ----
        private static void Source(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            var loco=R("Core/NativeHordeLoco.cs");
            Check(!loco.Contains("UnityEngine")&&!loco.Contains("System.Linq")&&!loco.Contains("new Random("),"the locomotion pass is pure Core (no engine, no LINQ, no clock-free randomness)");
            foreach(var bad in new[]{"NativePve","Minion","Network","Replica","Body.position","linearVelocity","LastAttack=","StaggerAt=",".Hp=","Damage("})Check(!loco.Contains(bad),"the locomotion pass never reaches the authority or the network (`"+bad+"`)");
            // The authority and the network never read the presentation's gait (its cadence, calm, gait speed or shoulders).
            var authority=new[]{"Runtime/NativePveHorde.cs","Runtime/NativeNetwork.cs","Runtime/NativeReplicaMotion.cs","Core/NativePrediction.cs","Core/NativeHordeKinds.cs","Core/NativeHordeDamage.cs","Core/NativeHordeDrop.cs","Core/NativeHordeRoam.cs"};
            foreach(var f in authority)
            {
                if(!File.Exists(Path.Combine(root,"Assets/ChipArena",f)))continue;var s=R(f);
                foreach(var sym in new[]{"StepRate(","ShoulderInset(","Shoulders(","CalmGain(","GaitSpeed","RunNow","NativeHordeLoco"})Check(!s.Contains(sym),f+" never reads the locomotion pass (`"+sym+"`)");
            }
            var exporter=File.ReadAllText(Path.Combine(root,"tools/export_pack_zombie.mjs"));
            foreach(var key in new[]{"chase-1-full","chase-5-full","walk-6-full","sprint-2"}){Check(exporter.Contains("key:'"+key+"'")&&NativeZombieClips.Find(key)>=0,"the exporter writes "+key+" and the tracks hold it");}
        }
    }
}
#endif
