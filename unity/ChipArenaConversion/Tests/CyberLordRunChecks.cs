using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChipArena.UnityConversion;

// The DEV Cyber Lord's run, sprint and second jump (Claude; docs/CYBER-LORD.md "Run and sprint", "Double jump"), and the
// chained Data Dash trails:
//  1. the Runner pack takes are the exporter's (tools/pack-cyberlord-run-provenance.json), in their slot order;
//  2. the carry on the fighter route: the contact foot never slips at the replicated run and sprint speeds in every
//     direction, the takes follow the travel direction, the sprint ramps on the replicated flag without a pop, the run's fists
//     pump and the hardlight dash sprint's sweep back past the hips,
//     the chest stays on the aim, the actor is never written;
//  3. the boost: keyed only on the replicated air jump (Actor.AirJumps / AirJumpAt, as the authority writes the daggers'), on
//     a host's and a guest's (delayed) timeline alike; the style and the jump layer start on the same frame; the timing;
//  4. its effect (NativeCyberLordFx): one per air jump, its lifetime, the first-person and reduced versions, never a disc on
//     the floor, the Dev Sandbox preview, the audio routing on existing keys;
//  5. chained Data Dashes: two .7 s apart both draw and the first finishes its own fade; six .2 s apart (no cooldown) are
//     all held, each fades on its own, the oldest first, the older ones on the low tier; past the cap the oldest retires;
//  6. no allocation per frame.
// Pixels are checked by the offline renders; nothing here runs Unity.
internal static class CyberLordRunChecks
{
    private static int count;
    private const float Dt=1/60f,D2R=(float)Math.PI/180;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Cyber Lord run/jump: "+what);count++;}

    // ---- a Cyber Lord on the fighter route, as CapturedAnimation.Player.FighterPose drives it ----
    private sealed class Pilot
    {
        public readonly Actor A;public readonly MeleeMotion M=new MeleeMotion{JumpLayer=new NativeJumpMotion()};public readonly CyberLordMotion C=new CyberLordMotion();
        public readonly MeleeView V=new MeleeView();public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public Vector3 Root;public string State;
        public Pilot(int id=7){A=CyberLordFixture.Pilot(id,Vector3.Zero,0);V.Id=id.ToString();V.Spec=CyberLordSpec.Id;}
        public void Step(float t,float dt=Dt)
        {
            Root=new Vector3(A.Position.x,A.Position.y,A.Position.z);
            V.Vel=new Vector3(-A.Velocity.x,A.Velocity.y,A.Velocity.z);V.Grounded=A.Grounded;V.Hp=A.Hp;V.Sprinting=A.Sprinting;V.Yaw=-A.Yaw*D2R;
            V.AirJumpAt=A.AirJumps>0?A.AirJumpAt:C.PreviewAirJumpAt;
            M.Rig.GroupPosition=new Vector3(-Root.X,.865f+Root.Y,Root.Z);M.Rig.GroupYaw=-A.Yaw*D2R;
            M.FighterBase(V,t,dt);State=C.Style(M,A,t,A.StunUntil>t);M.FighterFinish(V,t,dt);M.Rig.WritePose(Pose);Pose.World();
        }
        public Quaternion Group=>MeleeMath.AxisAngle(MeleeMath.Y,M.Rig.GroupYaw);
        public Vector3 World(int bone)=>M.Rig.GroupPosition+Vector3.Transform(Pose.WorldPosition[bone],Group);
        public Vector3 InChest(int bone)=>Vector3.Transform(Pose.WorldPosition[bone]-Pose.WorldPosition[CapturedRig.Chest],Quaternion.Inverse(Pose.WorldRotation[CapturedRig.Chest]));
    }
    // dir: radians from the facing, + to the rig's left (the source frame); Unity x is mirrored.
    private static V3 Velocity(float speed,float dir)=>new V3(-(float)Math.Sin(dir)*speed,0,(float)Math.Cos(dir)*speed);
    private static float Yaw(Quaternion q){var f=Vector3.Transform(Vector3.UnitZ,q);return (float)Math.Atan2(f.X,f.Z);}

    // ---- the effects' recording sink and a fixed scene ----
    private sealed class Sink:ICyberFxSink
    {
        public int Count,View,Lines,Voxels,Sprites,Glows,Flat;public float Gain;
        private void Floor(float bottom,float thick,float across){if(bottom<.12f&&thick<.03f&&across>.1f)Flat++;}
        private void G(Vector3 c){Gain=Math.Max(Gain,Math.Max(c.X,Math.Max(c.Y,c.Z)));}
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Count++;Glows++;G(color);}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Count++;G(color);}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Count++;}
        public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){Count++;Sprites++;if(view)View++;G(color);}
        public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){Count++;Lines++;if(view)View++;else Floor(Math.Max(tail.Y,head.Y),Math.Abs(tail.Y-head.Y)+width,new Vector2(head.X-tail.X,head.Z-tail.Z).Length());G(color);}
        public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){Count++;if(view)View++;G(color);}
        public void Voxel(bool view,WeaponShape shape,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){Count++;Voxels++;if(view)View++;else Floor(at.Y+scale.Y*.5f,scale.Y,Math.Max(scale.X,scale.Z));}
        public void Reset(){Count=View=Lines=Voxels=Sprites=Glows=0;Gain=0;}
    }
    private sealed class Scene:ICyberFxScene
    {
        public readonly List<Actor> List=new List<Actor>();public int ViewerId,LocalId;
        public IReadOnlyList<Actor> Actors=>List;
        public Vector3 Feet(Actor a)=>new Vector3(a.Position.x,a.Position.y,a.Position.z);
        public bool Chest(Actor a,out Vector3 chest){chest=Feet(a)+new Vector3(0,1.3f,0);return true;}
        public bool Fist(Actor a,bool left,out Vector3 fist){fist=Feet(a)+new Vector3(left?-.25f:.25f,1.2f,.3f);return true;}
        public int Viewer=>ViewerId;public int Local=>LocalId;
        public bool ViewFist(bool left,out Vector3 fist){fist=new Vector3(left?-.2f:.2f,1.4f,.4f);return ViewerId!=0;}
        public Vector3 Eye=>new Vector3(0,1.62f,0);
        public float Floor(Vector3 at)=>0;
    }

    // A jump at `j` and the air jump at `aj` as the authority's NativeJumpSimulation writes them (jump 8.5, the air jump .9 of
    // it, gravity 23; AirJumps 1 and AirJumpAt from the air jump to the landing), on the 60 Hz tick.
    private struct Arc{public float J,AJ,Land,Y1,V1;}
    private static Arc Jump(float j,float aj)
    {
        float v0=Rules.JumpSpeed,v1=Rules.JumpSpeed*NativeJumpSimulation.AirMultiplier,g=Rules.Gravity,u=aj-j,y1=v0*u-.5f*g*u*u;
        return new Arc{J=j,AJ=aj,Y1=y1,V1=v1,Land=aj+(v1+(float)Math.Sqrt(v1*v1+2*g*y1))/g};
    }
    private static void Place(Actor a,in Arc c,float t,bool grant,float z=0)
    {
        float y=0,vy=0;bool air=t>=c.J&&t<c.Land;
        if(air){if(t<c.AJ||!grant){float u=t-c.J;y=Rules.JumpSpeed*u-.5f*Rules.Gravity*u*u;vy=Rules.JumpSpeed-Rules.Gravity*u;if(!grant&&y<=0){air=false;y=0;vy=0;}}else{float u=t-c.AJ;y=c.Y1+c.V1*u-.5f*Rules.Gravity*u*u;vy=c.V1-Rules.Gravity*u;}}
        a.Grounded=!air;a.AirJumps=grant&&air&&t>=c.AJ?1:0;a.AirJumpAt=a.AirJumps>0?c.AJ:-100;a.Position=new V3(0,Math.Max(0,y),z);a.Velocity=new V3(0,air?vy:0,a.Velocity.z);
    }

    public static IEnumerable<string> Run(string root)
    {
        // ------------------------------------------------------------------------------------------------------------------
        // 1. The takes
        // ------------------------------------------------------------------------------------------------------------------
        int c0=count;
        string[] names={"run-forward","run-forward-left","run-left","run-back-left","run-back","run-back-right","run-right","run-forward-right","sprint-forward","sprint-forward-left","sprint-forward-right","boost"};
        string[] sources={"Mvm_Dash","Mvm_Dash_Front_L45","Mvm_Dash_Front_L","Mvm_Dash_Back_L45","Mvm_Dash_Back","Mvm_Dash_Back_R45","Mvm_Dash_Front_R","Mvm_Dash_Front_R45","Mvm_ChargeBoost","Mvm_ChargeBoost_Front_L45","Mvm_ChargeBoost_Front_R45","Esc_BoostDash_Up_Fwd"};
        Check(NativePackCyberLordRun.Names.SequenceEqual(names)&&NativePackCyberLordRun.Clips.Length==CyberLordRunRules.Clips&&CyberLordRunRules.Sprint==8&&CyberLordRunRules.Boost==11,"the takes in slot order (the run in eight directions, the sprint in three, the boost)");
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack-cyberlord-run-provenance.json"))))
        {
            var prov=doc.RootElement.GetProperty("clips").EnumerateArray().ToArray();Check(prov.Length==names.Length&&doc.RootElement.GetProperty("channels").GetInt32()==CapturedClip.Channels,"the provenance lists every take");
            for(int i=0;i<prov.Length;i++)
            {
                var c=CyberLordRunRules.Clip(i);var e=prov[i];Check(c.Name=="cyberlord-run/"+names[i]&&e.GetProperty("clip").GetString()==c.Name&&c.Source==sources[i]&&e.GetProperty("source").GetString()==sources[i],c.Name+" from "+sources[i]);
                string sha=Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(c.Data))).ToLowerInvariant();Check(sha==e.GetProperty("sha256").GetString(),c.Name+": the exporter's output ("+sha.Substring(0,12)+")");
                Check(c.Fps==e.GetProperty("fps").GetInt32()&&c.Frames==e.GetProperty("frames").GetInt32()&&c.Loop==e.GetProperty("loop").GetBoolean()&&c.Marks.SequenceEqual(e.GetProperty("marks").EnumerateArray().Select(x=>(float)x.GetDouble())),c.Name+": timing and marks");
                var rows=c.Decode();Check(rows.Length==c.Frames*CapturedClip.Channels&&rows.All(v=>float.IsFinite(v)&&Math.Abs(v)<4),c.Name+": decodes finite and bounded");
                if(c.Loop)Check(c.Marks.Length==1&&c.Marks[0]>=0&&c.Marks[0]<=c.Duration+1e-4f&&e.TryGetProperty("strikeFrom",out _),c.Name+": its left-foot strike (from the root-motion twin) lies in the cycle");
                else Check(c.Marks.Length==3&&c.Marks[0]==0&&c.Marks[1]>0&&c.Marks[1]<c.Marks[2]&&Math.Abs(c.Marks[2]-c.Duration)<1e-3f,c.Name+": marks [start, push-off, end]");
            }
        }
        foreach(var f in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))
        {
            string rel=f.Substring(root.Length).Replace('\\','/');if(rel.Contains("/bin/")||rel.Contains("/obj/")||rel.Contains("/Library/"))continue;
            Check(!rel.Contains("CLazy",StringComparison.OrdinalIgnoreCase)||rel.EndsWith(".cs")||rel.EndsWith(".json")||rel.EndsWith(".mjs"),"no Runner pack file in the project ("+rel+")");
        }
        Check(CyberLordRunRules.SprintFor(0)==CyberLordRunRules.Sprint&&CyberLordRunRules.SprintFor(1)==CyberLordRunRules.SprintLeft&&CyberLordRunRules.SprintFor(7)==CyberLordRunRules.SprintRight&&CyberLordRunRules.SprintFor(2)<0&&CyberLordRunRules.SprintFor(4)<0,"the sprint ahead and on the forward diagonals only");
        yield return $"Cyber Lord run takes: {count-c0} checks ({names.Length} Runner pack takes match the exporter's provenance: Mvm_Dash in eight directions, Mvm_ChargeBoost in three, Esc_BoostDash_Up_Fwd; no pack file in the project)";

        // ------------------------------------------------------------------------------------------------------------------
        // 2. The carry
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        float worst=0;string worstAt="";
        var cases=new List<(string name,float speed,float dir,bool sprint)>();float sweptZ=0,rampSpike=0;
        for(int k=0;k<8;k++)cases.Add(("run "+NativePackCyberLordRun.Names[k].Substring(4),Rules.Speed,k*MeleeMath.TAU/8,false));
        cases.Add(("sprint",Rules.Speed*Rules.SprintMultiplier,0,true));cases.Add(("sprint forward-left",Rules.Speed*Rules.SprintMultiplier,MeleeMath.TAU/8,true));cases.Add(("sprint forward-right",Rules.Speed*Rules.SprintMultiplier,-MeleeMath.TAU/8,true));
        cases.Add(("block walk",Rules.Speed*.45f,0,false));
        foreach(var c in cases)
        {
            var p=new Pilot();p.A.Velocity=Velocity(c.speed,c.dir);p.A.Sprinting=c.sprint;string before=JsonSerializer.Serialize(p.A.ViewCopy());
            float slip=0,zlo=9,zhi=-9;double chestYaw=0;int n=0;var prev=new Vector3[2];var had=new bool[2];var fistMean=Vector3.Zero;
            for(int f=0;f<300;f++)
            {
                float t=1+f*Dt;var v=p.A.Velocity;p.A.Position=new V3(v.x*(t-1),0,v.z*(t-1));p.Step(t);if(f<90)continue;
                for(int i=0;i<2;i++){bool lf=i==0,on=p.M.Gait.Planted(lf);var at=p.World(lf?CapturedRig.LFoot:CapturedRig.RFoot);if(on&&had[i]){var d=at-prev[i];d.Y=0;slip=Math.Max(slip,d.Length()/Dt);}had[i]=on;prev[i]=at;}
                var fist=p.InChest(CapturedRig.RHand);zlo=Math.Min(zlo,fist.Z);zhi=Math.Max(zhi,fist.Z);fistMean+=fist+p.InChest(CapturedRig.LHand);chestYaw+=Yaw(p.Pose.WorldRotation[CapturedRig.Chest]);n++;
                foreach(var q in p.M.Rig.Q)Check(float.IsFinite(q.W)&&Math.Abs(q.Length()-1)<1e-3f,c.name+": finite rotations");
            }
            Check(slip<.03f,c.name+": the contact foot slips "+(slip*100).ToString("0.00")+" cm/s (under 3 cm/s)");if(slip>worst){worst=slip;worstAt=c.name;}
            if(c.name!="block walk")
            {
                var cr=p.C.Carry;float dir=cr.Direction;Check(Math.Abs(MeleeMath.Wrap(dir-c.dir))<.05f,c.name+": the carry follows the travel ("+dir.ToString("0.00")+" rad)");
                int want=(int)Math.Round(MeleeMath.Wrap(c.dir)/(MeleeMath.TAU/8));want=((want%8)+8)%8;Check(cr.First==want&&cr.Mix<.05f||cr.Second==want&&cr.Mix>.95f,c.name+": its own take ("+cr.First+"/"+cr.Second+" at "+cr.Mix.ToString("0.00")+")");
                Check(c.sprint?cr.Sprint>.999f:cr.Sprint<1e-3f,c.name+": the sprint weight follows the replicated flag ("+cr.Sprint+")");
                Check(p.State==(c.sprint?"cyber-sprint":"cyber-run"),c.name+": presented as "+p.State);
                // The run pumps its fists; the hardlight dash sprint sweeps them back past the hips (a different silhouette) and
                // swings them a little at the stride.
                if(!c.sprint)Check(zhi-zlo>.08f,c.name+": the fist pumps ("+((zhi-zlo)*100).ToString("0")+" cm in the chest)");
                else{var mean=fistMean/(2*n);Check(mean.Z<-.2f&&mean.Y<-.2f&&zhi-zlo>.04f,c.name+": the fists swept back past the hips (mean "+mean.ToString("0.00")+" m in the chest), swinging "+((zhi-zlo)*100).ToString("0")+" cm");sweptZ=Math.Min(sweptZ,mean.Z);}
                Check(Math.Abs(chestYaw/n)<8*D2R,c.name+": the chest stays on the aim (mean "+(chestYaw/n/D2R).ToString("0")+" degrees)");
            }
            Check(JsonSerializer.Serialize(p.A.ViewCopy())==before,c.name+": the actor is never written");
        }
        {
            // The sprint ramps in and out on the replicated flag without a pop: the fists sweep back and forward over the ramp
            // smoothly (no frame's step stands out over both its neighbours'), and no step outruns the steady run's own swing by a quarter.
            var p=new Pilot();float steady=0,ramp=0,fast=0,spike=0;Vector3? last=null;bool sprint=false;float d1=-1,d2=-1;
            for(int f=0;f<420;f++)
            {
                float t=1+f*Dt;sprint=f>=200&&f<320;float sp=sprint?Rules.Speed*Rules.SprintMultiplier:Rules.Speed;p.A.Sprinting=sprint;p.A.Velocity=new V3(0,0,sp);p.A.Position=new V3(0,0,sp*(t-1));p.Step(t);
                var h=p.InChest(CapturedRig.RHand);
                if(last.HasValue&&f>90)
                {
                    float d=(h-last.Value).Length();if(f<190)steady=Math.Max(steady,d);else if(f>=240&&f<310)fast=Math.Max(fast,d);else if(f>=195&&f<240||f>=315&&f<350)ramp=Math.Max(ramp,d);
                    if(d1>=0&&d2>=0&&(f>=196&&f<241||f>=316&&f<351))spike=Math.Max(spike,d1-Math.Max(d2,d));d2=d1;d1=d;
                }
                last=h;
            }
            Check(spike<.02f&&ramp<Math.Max(steady,fast)*1.25f+.005f,"the sprint ramps in and out without a pop (largest fist step "+(ramp*100).ToString("0.0")+" cm, largest spike "+(spike*100).ToString("0.0")+" cm; the steady run's "+(steady*100).ToString("0.0")+", the sprint's "+(fast*100).ToString("0.0")+")");rampSpike=spike;
        }
        yield return $"Cyber Lord run and sprint: {count-c0} checks ({cases.Count} cases: the run in eight directions at {Rules.Speed} m/s, the sprint ahead and on the diagonals at {Rules.Speed*Rules.SprintMultiplier:0.00}, the block walk; the contact foot slips at most {(worst*100):0.000} cm/s ({worstAt}); the takes follow the travel, the sprint the replicated flag without a pop (spike {(rampSpike*100):0.0} cm), the run's fists pump and the sprint's sweep back to {sweptZ:0.00} m behind the chest, the chest on the aim, the actor untouched)";

        // ------------------------------------------------------------------------------------------------------------------
        // 3. The boost (the second jump's pose)
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        {
            var bc=CyberLordAirBoost.Clip;float prev=-1;
            for(float age=0;age<CyberLordAirBoost.Seconds+.1f;age+=1/240f){float tt=CyberLordAirBoost.TakeTime(age);Check(tt>=prev-1e-5f&&tt>=0&&tt<=bc.Duration+1e-4f,"the take runs forward within itself");prev=tt;}
            Check(Math.Abs(CyberLordAirBoost.TakeTime(CyberLordAirBoost.Push)-bc.Marks[1])<1e-4f&&Math.Abs(CyberLordAirBoost.TakeTime(CyberLordAirBoost.Tuck)-bc.Marks[2])<1e-4f,"the push-off at Push, the full tuck at Tuck");
            Check(CyberLordAirBoost.Weight(-.01f)==0&&CyberLordAirBoost.Weight(CyberLordAirBoost.Seconds)==0&&CyberLordAirBoost.Weight(CyberLordAirBoost.In)>.999f&&CyberLordAirBoost.Weight(CyberLordAirBoost.Hold)>.999f,"the weight: in over In, held, out by Seconds");
            float wprev=0,wstep=0;for(float age=0;age<CyberLordAirBoost.Seconds;age+=Dt){float w=CyberLordAirBoost.Weight(age);wstep=Math.Max(wstep,Math.Abs(w-wprev));wprev=w;}Check(wstep<.4f,"the weight never jumps (largest 60 Hz step "+wstep.ToString("0.00")+")");
            Check(CyberLordAirBoost.Handles(CyberLordSpec.Id)&&!CyberLordAirBoost.Handles("daggers")&&NativeJumpRules.SetFor(CyberLordSpec.Id)==NativeJumpRules.Unarmed,"the Cyber Lord boosts over the Unarmed jump and fall");
            // The flip's three paths skip the Cyber Lord even once Flips follows the air jump grant (NativeJumpSimulation.CanAirJump).
            string Src(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));
            Check(Src("Core/NativeJumpMotion.cs").Contains("NativeJumpRules.Flips(spec)&&!CyberLordAirBoost.Handles(spec)")&&Src("Runtime/NativeJumpFxPresentation.cs").Contains("NativeJumpRules.Flips(p.Seat.spec)&&!CyberLordAirBoost.Handles(p.Seat.spec)")
                &&Src("Runtime/NativeMoveFeelAudio.cs").Contains("NativeJumpRules.Flips(p.Seat.spec)&&!CyberLordAirBoost.Handles(p.Seat.spec)"),"the flip pose, its air burst and its whoosh never take the Cyber Lord (it boosts instead)");
        }
        foreach(bool guest in new[]{false,true})
        {
            // A host draws the newest frame; a guest's remote pilot about .1 s behind (the same arc, presented later).
            var arc=Jump(1,1.3f);float lag=guest?.1f:0;var p=new Pilot();string before=null;float styleAt=-1,layerAt=-1,peak=0,layerPeak=0;string state="";
            for(int f=0;f<150;f++)
            {
                float t=.5f+f*Dt;Place(p.A,arc,t-lag,true);if(guest&&p.A.AirJumps>0)p.A.AirJumpAt+=lag;
                before??=JsonSerializer.Serialize(p.A.ViewCopy());if(t<.6f)before=JsonSerializer.Serialize(p.A.ViewCopy());
                string b=JsonSerializer.Serialize(p.A.ViewCopy());p.Step(t);Check(JsonSerializer.Serialize(p.A.ViewCopy())==b,"the boost never writes the actor");
                if(styleAt<0&&p.C.BoostWeight>.01f)styleAt=t;if(layerAt<0&&p.M.JumpLayer.BoostWeight>.01f)layerAt=t;peak=Math.Max(peak,p.C.BoostWeight);layerPeak=Math.Max(layerPeak,p.M.JumpLayer.BoostWeight);if(p.State=="cyber-air-boost")state=p.State;
                if(t<arc.AJ+lag-1e-3f)Check(p.C.BoostWeight==0&&p.M.JumpLayer.BoostWeight==0,"no boost before the replicated air jump");
                Check(p.M.JumpLayer.FlipAngle==0,"the Cyber Lord never flips");
            }
            string who=guest?"a guest (.1 s behind)":"the host";
            Check(styleAt>=arc.AJ+lag-1e-3f&&styleAt<arc.AJ+lag+.04f,who+": the boost starts on the air jump ("+(styleAt-arc.AJ-lag).ToString("0.000")+" s)");
            Check(Math.Abs(styleAt-layerAt)<1e-4f,who+": the torso and the legs start on the same frame");
            Check(peak>.99f&&layerPeak>.99f&&state=="cyber-air-boost",who+": the boost plays in full and is presented as cyber-air-boost");
        }
        {
            // Without the authority's grant (today's rules: the air jump is the daggers') the same jump shows no boost.
            var arc=Jump(1,1.3f);var p=new Pilot();float any=0;
            for(int f=0;f<150;f++){float t=.5f+f*Dt;Place(p.A,arc,t,false);p.Step(t);any=Math.Max(any,p.C.BoostWeight+p.M.JumpLayer.BoostWeight);}
            Check(any==0,"no grant, no boost: it is keyed only on the replicated AirJumps / AirJumpAt");
            // The Dev Sandbox preview drives the same pose on a single jump (CyberLordAirBoost.PreviewAt on the rig's track).
            p=new Pilot();float pw=0;
            for(int f=0;f<150;f++){float t=.5f+f*Dt;Place(p.A,arc,t,false);p.C.PreviewAirJumpAt=CyberLordAirBoost.PreviewAt(p.M.JumpLayer.Track,t);p.Step(t);pw=Math.Max(pw,p.C.BoostWeight);}
            Check(pw>.99f,"the Dev Sandbox preview shows the boost on the pilot's own jump");
        }
        {
            // A dagger's air jump still flips (the boost is the Cyber Lord's alone).
            var arc=Jump(1,1.3f);var jm=new NativeJumpMotion();var rig=new MeleeRig();var mv=new MeleeView{Id="9",Spec="daggers"};float flip=0,boost=0;
            for(int f=0;f<120;f++){float t=.5f+f*Dt;var a=new Actor();Place(a,arc,t,true);mv.Grounded=a.Grounded;mv.AirJumpAt=a.AirJumps>0?a.AirJumpAt:-100;rig.Reset();rig.GroupPosition=new Vector3(0,.865f+a.Position.y,0);jm.Melee(rig,mv,t,Dt);flip=Math.Max(flip,jm.FlipAngle);boost=Math.Max(boost,jm.BoostWeight);}
            Check(flip>1&&boost==0,"the daggers still flip on their air jump, with no boost");
        }
        yield return $"Cyber Lord boost: {count-c0} checks (the Runner take from its push-off to the tuck in {CyberLordAirBoost.Tuck} s, held to {CyberLordAirBoost.Hold} s, out by {CyberLordAirBoost.Seconds} s; keyed only on the replicated air jump, on the host's and a delayed guest's timeline alike; torso and legs on the same frame; no grant no boost; the Dev Sandbox preview; the daggers still flip; the actor untouched)";

        // ------------------------------------------------------------------------------------------------------------------
        // 4. The effect
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        (int boosts,int pieces,int view,int ghosts,int flat,float firstAt) Effect(bool guest,int viewer,float q,bool low,bool grant=true,bool preview=false)
        {
            var arc=Jump(1,1.3f);var a=CyberLordFixture.Pilot(7,Vector3.Zero,0);var fx=new NativeCyberLordFx();var sc=new Scene{ViewerId=viewer,LocalId=7};sc.List.Add(a);var sink=new Sink();
            float lag=guest?.1f:0;int most=0,pcs=0,vw=0,gh=0,flats=0;float first=-1;var track=new NativeJumpTrack();
            for(int f=0;f<150;f++)
            {
                float t=.5f+f*Dt;Place(a,arc,t-lag,grant);if(guest&&a.AirJumps>0)a.AirJumpAt+=lag;
                if(preview){track.Explicit=true;track.Observe(t,a.Grounded,a.Position.y,false);float pa=CyberLordAirBoost.PreviewAt(track,t);track.AirJumpSignal(pa);fx.PreviewAirJump(7,pa);}
                sink.Reset();fx.Draw(sink,sc,t,q,low);fx.Draw(new Sink(),sc,t,q,low);
                most=Math.Max(most,fx.Boosts);if(fx.Boosts>0&&first<0)first=t;if(fx.Boosts>0){pcs=Math.Max(pcs,sink.Count);vw=Math.Max(vw,sink.View);int g=0;for(int i=0;i<fx.Ghosts;i++)if(fx.Ghost(i).Actor==7)g++;gh=Math.Max(gh,g);}flats+=sink.Flat;
                if(fx.Boosts>0)Check(t-fx.BoostOf(0).At<NativeCyberLordFx.BoostLife,"a boost lives BoostLife");
            }
            return (most,pcs,vw,gh,flats,first);
        }
        var host=Effect(false,0,1,false);var guestFx=Effect(true,0,1,false);var fp=Effect(false,7,1,false);var red=Effect(false,0,.3f,false);var low=Effect(false,0,1,true);var none=Effect(false,0,1,false,grant:false);var pre=Effect(false,0,1,false,grant:false,preview:true);
        Check(host.boosts==1&&guestFx.boosts==1,"one boost per air jump (drawn twice a frame), on the host and on a delayed guest");
        Check(Math.Abs(host.firstAt-1.3f)<.02f&&Math.Abs(guestFx.firstAt-1.4f)<.02f,"it appears on the presented air jump (host "+host.firstAt.ToString("0.000")+" s, guest "+guestFx.firstAt.ToString("0.000")+" s)");
        Check(none.boosts==0,"no grant, no effect");Check(pre.boosts==1,"the Dev Sandbox preview shows the effect once");
        Check(host.ghosts==2&&fp.ghosts==0&&host.view==0,"the afterimage: two RGB-split ghosts in third person, none for the first-person pilot");
        Check(fp.view>0&&fp.pieces<host.pieces,"first person: a gentler version with its fist flare on the view layer ("+fp.pieces+" pieces, third person "+host.pieces+")");
        Check(red.pieces<host.pieces&&low.pieces<host.pieces,"reduced effects and the low tier thin it ("+red.pieces+" and "+low.pieces+" of "+host.pieces+")");
        Check(host.flat+red.flat+fp.flat+guestFx.flat==0,"no glowing piece lies flat on the floor (owner rule)");
        {
            // Reduced effects never strobe: the ghosts' strength only eases down, the gains stay below full effects'.
            var arc=Jump(1,1.3f);var a=CyberLordFixture.Pilot(7,Vector3.Zero,0);var fx=new NativeCyberLordFx();var sc=new Scene();sc.List.Add(a);var s1=new Sink();float last=9,full=0,soft=0;var fx2=new NativeCyberLordFx();var s2=new Sink();
            for(int f=0;f<120;f++)
            {
                float t=.5f+f*Dt;Place(a,arc,t,true);s1.Reset();s2.Reset();fx.Draw(s1,sc,t,.3f,false);fx2.Draw(s2,sc,t,1,false);soft=Math.Max(soft,s1.Gain);full=Math.Max(full,s2.Gain);
                float op=0;for(int i=0;i<fx.Ghosts;i++)op=Math.Max(op,fx.Ghost(i).Opacity);if(fx.Boosts>0&&op>0){Check(op<=last+1e-5f,"reduced effects: the afterimage only fades");last=op;}
            }
            Check(soft<full,"reduced effects: softer light ("+soft.ToString("0.0")+" against "+full.ToString("0.0")+")");
        }
        {
            var o=new NativeKitSound[NativeCyberLordSounds.Max];int n=NativeCyberLordSounds.AirBoost(o);
            Check(n==2&&Enumerable.Range(0,n).All(i=>NativeCyberLordSounds.Keys.Contains(o[i].Key)&&NativeCyberLordSounds.Clips.Contains(o[i].Clip)),"the boost's cue plays existing bank keys with shipped fallbacks");
        }
        yield return $"Cyber Lord boost effect: {count-c0} checks (the hex platform, shards, afterimage and scanline trail once per seen air jump, on the host and a delayed guest; {host.pieces} pieces at full, {red.pieces} reduced, {low.pieces} low, {fp.pieces} in first person with no afterimage; never flat on the floor; the preview; existing audio keys)";

        // ------------------------------------------------------------------------------------------------------------------
        // 5. Chained Data Dashes
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;int spamPeak=0;
        {
            var a=CyberLordFixture.Pilot(3,Vector3.Zero,0);var fx=new NativeCyberLordFx();var sc=new Scene();sc.List.Add(a);var sink=new Sink();
            float d1=1,d2=1.7f;CyberLordFixture.DataDash(a,d1);var first=a.Cyber.TrailUntil;int both=0,single=0,most=0;float firstGone=-1;
            for(int f=0;f<240;f++)
            {
                float t=.9f+f*Dt;
                if(t>=d2&&a.Cyber.DashAt<d2){a.Position=new V3(0,0,CyberLordSpec.DashDistance);CyberLordFixture.DataDash(a,d2);}
                sink.Reset();fx.Draw(sink,sc,t,1,false);most=Math.Max(most,fx.DashTrails);
                bool has1=false,has2=false;for(int i=0;i<fx.DashTrails;i++){var d=fx.DashTrailOf(i);if(Math.Abs(d.At-d1)<1e-4f){has1=true;Check(Math.Abs(d.Until-first)<1e-4f,"the first trail keeps its own end after the second dash");}if(Math.Abs(d.At-d2)<1e-4f)has2=true;}
                if(t>=d2+.02f&&t<first)Check(has1&&has2,"both trails draw while the first fades ("+t.ToString("0.00")+" s)");
                if(has1&&has2)both=Math.Max(both,sink.Voxels);if(!has1&&has2)single=Math.Max(single,sink.Voxels);
                if(!has1&&firstGone<0&&t>d1)firstGone=t;
            }
            Check(most==2,"two remembered trails, no more");
            Check(firstGone>=first&&firstGone<=first+.25f,"the first trail finishes its own fade (gone at "+firstGone.ToString("0.00")+" s, its end "+first.ToString("0.00")+" s)");
            Check(both>single,"both trails' blocks draw together ("+both+" blocks, then "+single+")");
        }
        {
            // Dash spam (Data Dash has no cooldown: only the .2 s dash and the mana gate it): six dashes .2 s apart all keep their
            // trails, each finishes its own fade, the oldest first, and the older ones draw on the low tier.
            const int N=6;const float Start=1,Gap=.2f;
            var a=CyberLordFixture.Pilot(3,Vector3.Zero,0);var fx=new NativeCyberLordFx();var sc=new Scene();sc.List.Add(a);var sink=new Sink();
            var ends=new float[N];var gone=new float[N];for(int k=0;k<N;k++)gone[k]=-1;int next=0,most=0,lowSeen=0,peak=0;var seen=new bool[N];
            for(int f=0;f<200;f++)
            {
                float t=.95f+f*Dt;
                while(next<N&&t>=Start+next*Gap){a.Position=new V3(0,0,next*CyberLordSpec.DashDistance);CyberLordFixture.DataDash(a,Start+next*Gap);ends[next]=a.Cyber.TrailUntil;next++;}
                sink.Reset();fx.Draw(sink,sc,t,1,false);most=Math.Max(most,fx.DashTrails);peak=Math.Max(peak,sink.Voxels);Array.Clear(seen,0,N);
                for(int i=0;i<fx.DashTrails;i++)
                {
                    var d=fx.DashTrailOf(i);int k=(int)Math.Round((d.At-Start)/Gap);if(k<0||k>=N||Math.Abs(d.At-(Start+k*Gap))>1e-4f)continue;seen[k]=true;
                    Check(Math.Abs(d.Until-ends[k])<1e-4f,"spam: trail "+k+" keeps its own end");if(fx.TrailLow(i)){lowSeen++;Check(k<next-NativeCyberLordFx.FullTrails,"spam: only the older trails go low");}
                }
                for(int k=0;k<next;k++){if(t<ends[k])Check(seen[k],"spam: trail "+k+" draws while it fades ("+t.ToString("0.00")+" s)");if(!seen[k]&&gone[k]<0)gone[k]=t;}
            }
            Check(most==N,"spam: all "+N+" trails held at once ("+most+")");
            for(int k=0;k<N;k++)Check(gone[k]>=ends[k]&&gone[k]<=ends[k]+.25f,"spam: trail "+k+" finishes its own fade (gone at "+gone[k].ToString("0.00")+" s, its end "+ends[k].ToString("0.00")+" s)");
            for(int k=1;k<N;k++)Check(gone[k]>=gone[k-1],"spam: the oldest trail fades first");
            Check(lowSeen>0,"spam: the older trails draw on the low tier");spamPeak=peak;
            // Faster than the authority allows (.1 s apart, a dozen): the ring holds TrailsPerPilot and retires the oldest first.
            var b=CyberLordFixture.Pilot(4,Vector3.Zero,0);var fx2=new NativeCyberLordFx();var sc2=new Scene();sc2.List.Add(b);int held=0;bool oldestFirst=true;
            for(int k=0;k<12;k++)
            {
                float t=1+k*.1f;CyberLordFixture.DataDash(b,t);fx2.Draw(new Sink(),sc2,t+.01f,1,false);held=Math.Max(held,fx2.DashTrails);
                float min=float.MaxValue;for(int i=0;i<fx2.DashTrails;i++)min=Math.Min(min,fx2.DashTrailOf(i).At);
                if(k>=NativeCyberLordFx.TrailsPerPilot)oldestFirst&=Math.Abs(min-(1+(k-NativeCyberLordFx.TrailsPerPilot+1)*.1f))<1e-4f;
            }
            Check(held==NativeCyberLordFx.TrailsPerPilot&&oldestFirst,"spam past the cap: "+held+" trails held, the oldest retired first");
        }
        {
            // A late joiner (a fresh effects state) sees the current trail from the snapshot alone.
            var a=CyberLordFixture.Pilot(3,Vector3.Zero,0);CyberLordFixture.DataDash(a,1);var fx=new NativeCyberLordFx();var sc=new Scene();sc.List.Add(a);fx.Draw(new Sink(),sc,1.6f,1,false);
            Check(fx.DashTrails==1,"a late joiner draws the current trail");
        }
        yield return $"Cyber Lord chained dashes: {count-c0} checks (two dashes .7 s apart: both trails draw, the first finishes its own 1.2 s fade; six dashes .2 s apart: all six held, each finishing its own fade, the oldest first, the older ones on the low tier, {spamPeak} blocks at the peak; past the cap the oldest retires first; a late joiner sees the current one)";

        // ------------------------------------------------------------------------------------------------------------------
        // 6. No per-frame allocation
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        {
            var runner=new Pilot();var jumper=new Pilot(8);var arc=Jump(1,1.3f);var fx=new NativeCyberLordFx();var sc=new Scene{ViewerId=8,LocalId=8};sc.List.Add(jumper.A);var nul=new Sink();
            runner.A.Velocity=Velocity(Rules.Speed,.6f);
            // A Cyber Lord spamming Data Dash (no cooldown: one every .2 s) on its own effects state, the ring full of trails.
            var dasher=CyberLordFixture.Pilot(5,Vector3.Zero,0);var dfx=new NativeCyberLordFx();var dsc=new Scene();dsc.List.Add(dasher);float nextDash=1;
            void Frame(float t)
            {
                runner.A.Sprinting=t%2>1;runner.A.Position=new V3(runner.A.Velocity.x*t,0,runner.A.Velocity.z*t);runner.Step(t);
                float jt=1+(t%1.6f);Place(jumper.A,arc,jt,true);jumper.Step(t);fx.Draw(nul,sc,jt,1,false);fx.Draw(nul,sc,jt,.3f,true);
                if(t>=nextDash){CyberLordFixture.DataDash(dasher,nextDash);nextDash+=.2f;}dfx.Draw(nul,dsc,t,1,false);dfx.Draw(nul,dsc,t,.3f,true);
            }
            for(float t=1;t<4;t+=Dt)Frame(t);
            long before=GC.GetAllocatedBytesForCurrentThread();for(float t=4;t<9;t+=Dt)Frame(t);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"300 frames of the run, the sprint ramp, the double jump and its effect, and a Data Dash spam's trail ring allocate "+bytes+" bytes");Check(dfx.DashTrails>=6,"the spam fills the ring ("+dfx.DashTrails+" trails)");
        }
        yield return $"Cyber Lord run/jump allocation: 0 bytes over 300 frames; {count} checks in all";
    }
}
