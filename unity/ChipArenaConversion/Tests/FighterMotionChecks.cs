#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The DEV fighting styles' presentation (Core/NativeFighterPose.cs): each style walks and runs on the gait with its own
    // stance and guard (never the sword set), its Shift shows (the Emperor's dodge roll, the Ansatsuken's Demon Step), every
    // clip it can route to is one tools/export_motion.mjs exported, a juggled pilot shows animateJuggle, and the Emperor's
    // electric bolts fire only on the source's triggers (client/emperor-effects.ts) for each fighter move.
    internal static class FighterMotionChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Fighter motion: "+what);count++;}
        private static void Near(float a,float b,string what,float tol=1e-4f)=>Check(Math.Abs(a-b)<=tol&&!float.IsNaN(a),what+": "+a+" != "+b);
        private static float Angle(Quaternion a,Quaternion b)=>2*(float)Math.Acos(Math.Min(1,Math.Abs(Quaternion.Dot(Quaternion.Normalize(a),Quaternion.Normalize(b)))));
        public static void Run(string root)
        {
            Stances();Routing(root);Locomotion();Juggle();Bolts(root);
            Console.WriteLine("PASS DEV fighter motion ("+count+" checks: Emperor/Ansatsuken stances, own guard on the gait at rest and in travel, Shift, exported clip routing, juggle pose, bolt trigger table per move)");
        }
        // stances.ts EMPEROR_STANCE and KARATE_STANCE (lead x,z,toeOut; rear x,z,toeOut; crouch, pelvisYaw, lean all 0).
        private static void Stances()
        {
            void Is(string spec,float lx,float lz,float lt,float rx,float rz,float rt)
            {
                var s=MeleeStance.For(spec);Near(s.LeadX,lx,spec+" lead x");Near(s.LeadZ,lz,spec+" lead z");Near(s.LeadToe,lt,spec+" lead toe");
                Near(s.RearX,rx,spec+" rear x");Near(s.RearZ,rz,spec+" rear z");Near(s.RearToe,rt,spec+" rear toe");Check(s.Crouch==0&&s.PelvisYaw==0&&s.Lean==0&&s.KneeSoft==0,spec+" guard owns hips");
            }
            Is("emperor",.17f,.08f,-.3f,-.13f,-.23f,.62f);Is("ansatsuken",.17f,.13f,-.15f,-.15f,-.21f,.55f);
            Check(MeleeStance.For("sword")==MeleeStance.Melee&&MeleeStance.For("pistol")==MeleeStance.Pistol&&MeleeStance.For("siren")==MeleeStance.Neutral,"other stances unchanged");
            Check(NativeFighterPose.Handles("emperor")&&NativeFighterPose.Handles("ansatsuken")&&!NativeFighterPose.Handles("sword")&&!NativeFighterPose.Handles("siren"),"fighter specs");
            Check(!MeleeMotion.Handles("emperor")&&!MeleeMotion.Handles("ansatsuken"),"fighters are not on a captured sword set");
        }
        // Every key the routing produces is an exported clip of the same style; standing and travelling both play the guard.
        private static void Routing(string root)
        {
            var exported=new HashSet<string>();
            using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/motion-export.json"))))foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())exported.Add(c.GetProperty("key").GetString());
            void Key(string spec,string action,int total,int index,bool roll,bool block,string want)
            {
                string key=NativeFighterPose.Clip(spec,action,total,index,roll,block);Check(key==want,spec+" "+(action??"idle")+" routes to "+(key??"null")+", not "+(want??"null"));
                if(key!=null){Check(exported.Contains(key),key+" is exported");Check(key.StartsWith(spec+"-",StringComparison.Ordinal),key+" belongs to "+spec);}
                Check(ReferenceEquals(key,NativeFighterPose.Clip(spec,action,total,index,roll,block)),"cached key "+key);
            }
            foreach(var spec in new[]{"emperor","ansatsuken"}){Key(spec,null,0,0,false,false,spec+"-idle");Key(spec,"",5,3,false,false,spec+"-idle");Key(spec,null,0,0,false,true,spec+"-block");}
            Key("emperor",null,0,0,true,false,null);Key("emperor","jab",2,1,true,false,null);Key("ansatsuken",null,0,0,true,false,"ansatsuken-step");Key("ansatsuken","tatsu",4,2,true,true,"ansatsuken-step");
            Key("emperor","electric",1,0,false,false,"emperor-electric-1-0");Key("emperor","wave",1,0,false,false,"emperor-wave-1-0");
            for(int total=2;total<=3;total++)for(int i=0;i<total;i++)Key("emperor","jab",total,i,false,false,"emperor-jab-"+total+"-"+i);
            foreach(int total in new[]{3,10})for(int i=0;i<total;i++)Key("ansatsuken","combo",total,i,false,false,"ansatsuken-combo-"+total+"-"+i);
            foreach(var (kind,total) in new[]{("tatsu",4),("tatsu",7),("shoryuken",1),("shoryuken",3)})Key("ansatsuken",kind,total,total-1,false,false,"ansatsuken-"+kind+"-"+total+"-0");
            foreach(var kind in new[]{"charge","hadoken","shinku"})Key("ansatsuken",kind,0,0,false,false,"ansatsuken-"+kind+"-1-0");
            Key("sword",null,0,0,false,false,null);Key("pistol","jab",2,0,false,false,null);
            Check(NativeFighterPose.Acting("jab",1,.5f)&&!NativeFighterPose.Acting("jab",1,1)&&NativeFighterPose.Acting("charge",1,9)&&!NativeFighterPose.Acting(null,9,1)&&!NativeFighterPose.Acting("",9,1),"action lifetime");
        }
        // The gait under the style: at rest the feet stand on the style's stance and the upper body is its guard; running at
        // full speed the feet stride and the upper body carries back to the gait's arm pump; an action keeps the guard clip.
        private static void Locomotion()
        {
            // A guard stand-in: arms raised, legs unauthored (identity), the body bobbed down as emperorGuard does.
            var guard=new CapturedRig.Pose();for(int b=0;b<MeleeRig.N;b++){guard.Rotation[b]=Quaternion.Identity;guard.Position[b]=CapturedRig.Bones[b].Position;}
            guard.Rotation[CapturedRig.LArm]=MeleeMath.Euler(-.8f,-.15f,.08f);guard.Rotation[CapturedRig.RArm]=MeleeMath.Euler(-.55f,.12f,-.18f);guard.Rotation[CapturedRig.RElbow]=MeleeMath.Euler(-1.95f,0,0);
            guard.Position[CapturedRig.Body]=new Vector3(0,-.05f,0);Check(!NativeFighterPose.AuthorsLegs(guard),"guard leaves the legs to the gait");
            foreach(var spec in new[]{"emperor","ansatsuken"})
            {
                var motion=new MeleeMotion();var gaitOnly=new MeleeMotion();var v=new MeleeView{Id="7",Spec=spec,Grounded=true};var st=MeleeStance.For(spec);
                float t=10,z=0;
                for(int f=0;f<90;f++,t+=1/60f){Step(motion,v,t,z,guard,spec,false);Step(gaitOnly,v,t,z,null,spec,false);}
                st.Foot(true,out float lx,out float lz,out _);st.Foot(false,out float rx,out float rz,out _);
                var la=motion.Rig.AnkleInGroup(true);var ra=motion.Rig.AnkleInGroup(false);
                Near(la.X,lx,spec+" idle lead ankle x",.02f);Near(la.Z,lz,spec+" idle lead ankle z",.02f);Near(ra.X,rx,spec+" idle rear ankle x",.02f);Near(ra.Z,rz,spec+" idle rear ankle z",.02f);
                Check(Angle(motion.Rig.Q[CapturedRig.LArm],guard.Rotation[CapturedRig.LArm])<.02f&&Angle(motion.Rig.Q[CapturedRig.RElbow],guard.Rotation[CapturedRig.RElbow])<.02f,spec+" idle shows its own guard");
                // Run at 6.8 m/s for 1.5 s: the feet stride, the arms are the gait's (carry 1), not the guard's.
                v.Vel=new Vector3(0,0,6.8f);float minZ=float.MaxValue,maxZ=float.MinValue,away=0;
                for(int f=0;f<90;f++,t+=1/60f){z+=6.8f/60;Step(motion,v,t,z,guard,spec,false);Step(gaitOnly,v,t,z,null,spec,false);if(f>45){float az=motion.Rig.AnkleInGroup(true).Z;minZ=Math.Min(minZ,az);maxZ=Math.Max(maxZ,az);away=Math.Max(away,Angle(motion.Rig.Q[CapturedRig.LArm],guard.Rotation[CapturedRig.LArm]));}}
                Check(maxZ-minZ>.3f,spec+" running feet stride ("+(maxZ-minZ)+" m)");
                Check(Angle(motion.Rig.Q[CapturedRig.LArm],gaitOnly.Rig.Q[CapturedRig.LArm])<.03f&&Angle(motion.Rig.Q[CapturedRig.RElbow],gaitOnly.Rig.Q[CapturedRig.RElbow])<.03f,spec+" full run pumps the gait's arms");
                Check(away>.3f,spec+" running is not held in the guard ("+away+" rad)");
                // A strike while travelling keeps the style clip (no carry).
                Step(motion,v,t,z,guard,spec,true);Check(Angle(motion.Rig.Q[CapturedRig.LArm],guard.Rotation[CapturedRig.LArm])<.02f,spec+" an action keeps its clip in travel");
                foreach(var q in motion.Rig.Q)Check(!float.IsNaN(q.W)&&Math.Abs(q.Length()-1)<1e-3f,spec+" finite rotations");
            }
        }
        private static void Step(MeleeMotion m,MeleeView v,float t,float z,CapturedRig.Pose clip,string spec,bool acting)
        {
            m.Rig.GroupPosition=new Vector3(0,.865f,z);m.Rig.GroupYaw=0;m.FighterBase(v,t,1/60f);
            if(clip!=null)NativeFighterPose.Layer(m.Rig,clip,spec=="emperor",true,false,acting,false);
            m.FighterFinish(v,t,1/60f);
        }
        private static void Juggle()
        {
            var j=new NativeJuggleTrack();
            Check(!j.Observe(0,1),"no juggle");Check(j.Observe(2.25f,1.01f)&&Math.Abs(j.At-1)<1e-4f&&Math.Abs(j.LastHit-1)<1e-4f&&j.Hits==1,"launch observed");
            Check(j.Observe(2.25f,1.2f)&&j.Hits==1&&Math.Abs(j.LastHit-1)<1e-4f,"no new hit while unchanged");
            j.Observe(2.75f,1.51f);Check(Math.Abs(j.LastHit-1.5f)<1e-4f&&Math.Abs(j.Previous-1)<1e-4f&&j.Hits==2&&Math.Abs(j.At-1)<1e-4f,"juggle hit + 1.25");
            j.Observe(2.86f,2.01f);Check(Math.Abs(j.LastHit-2.01f)<1e-4f&&j.Hits==3,"bound splat + .85");
            Check(!j.Observe(0,2.5f)&&j.Until==0,"landed");j.Observe(7,5);Check(j.LastHit<=5&&Math.Abs(j.At-5)<1e-4f,"Siren launch + 2 starts now");
            j.Observe(7,3);Check(Math.Abs(j.At-3)<1e-4f,"a replay step back starts over");
            Check(NativeJugglePose.Active(1,3.5f)&&!NativeJugglePose.Active(0,1)&&!NativeJugglePose.Active(1,4.5f),"juggle lasts until landing (capped)");
            // animateJuggle: at the hit the pose is the one the hit caught; after the .16 s launch the body is pitched back by
            // -.7 - .45*kick with its y at .03.
            var p=new CapturedRig.Pose();for(int b=0;b<MeleeRig.N;b++){p.Rotation[b]=MeleeMath.Euler(.1f,.2f,0);p.Position[b]=CapturedRig.Bones[b].Position;}
            var k=new NativeJuggleTrack();k.Observe(11.25f,10);NativeJugglePose.Apply(p,10,k,4);
            Check(Angle(p.Rotation[CapturedRig.Body],MeleeMath.Euler(.1f,.2f,0))<1e-3f&&Angle(p.Rotation[CapturedRig.LKnee],MeleeMath.Euler(.1f,.2f,0))<1e-3f,"launch starts from the caught pose");
            float time=10.4f,age=.4f,kick=age/.065f*(float)Math.Exp(1-age/.065f);
            for(int b=0;b<MeleeRig.N;b++)p.Rotation[b]=MeleeMath.Euler(.1f,.2f,0);NativeJugglePose.Apply(p,time,k,-2);
            Near(p.Position[CapturedRig.Body].Y,.03f,"juggled body height");var e=MeleeMath.ToEuler(p.Rotation[CapturedRig.Body]);
            Near(e.X,-.7f-kick*.45f,"juggled body pitch",1e-3f);Near(e.Y,(float)Math.Sin(.4*4)*.18f,"juggled body sway",1e-3f);
            var knee=MeleeMath.ToEuler(p.Rotation[CapturedRig.RKnee]);Near(knee.X,.85f+.2f*(float)Math.Sin(time*9)+.25f*.12f,"right knee x",1e-3f);Near(knee.Y,.2f,"knee keeps its y",1e-3f);
        }
        // emperor-effects.ts per fighter and move.
        private static void Bolts(string root)
        {
            foreach(var move in new[]{"electric"})Check(NativeEmperorTriggers.Fist("emperor",move),"Emperor "+move+" fist bolt");
            foreach(var move in new[]{"jab","wave",null,""})Check(!NativeEmperorTriggers.Fist("emperor",move),"Emperor "+(move??"idle")+" no fist bolt");
            foreach(var move in new[]{"combo","tatsu","shoryuken","charge","hadoken","shinku","step","electric",null})Check(!NativeEmperorTriggers.Fist("ansatsuken",move),"Ansatsuken "+(move??"idle")+" no fist bolt");
            foreach(var spec in new[]{"ansatsuken","sword","siren","lightning"})foreach(var detail in new[]{"direct","secondary"})foreach(bool punch in new[]{false,true})
                Check(NativeEmperorTriggers.Impact(spec,detail,punch,punch,.13f,!punch,true)==0,spec+" "+detail+" hit spawns no Emperor impact");
            // Owner direction (2026-10-09): basic attacks show no lightning (the source's .65 jab impact and jab juggle bolt are dropped).
            Check(NativeEmperorTriggers.Impact("emperor","direct",true,false,9,false,false)==0,"Emperor jab: no impact");
            Check(!NativeEmperorTriggers.JuggleBolt(4,4.01f,-100)&&!NativeEmperorTriggers.JuggleBolt(4,3.5f,3.99f),"a jab's juggle hit draws no bolt");
            Check(NativeEmperorTriggers.JuggleBolt(4,3.2f,-100)&&NativeEmperorTriggers.JuggleBolt(4,-100,-100),"an Electric Hook, echo or Siren launch juggle hit keeps its bolt");
            string fighters=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeFighters.cs"));
            Check(!fighters.Contains("Fx("),"fighter swings and echoes add no ability blast (the old stand-in drew the lightning blast on basic hits)");
            Check(NativeEmperorTriggers.Impact("emperor","direct",true,true,0,false,false)==1.3,"Electric Hook impact 1.3");
            Check(NativeEmperorTriggers.Impact("emperor","direct",false,false,.13f,false,false)==1.3,"Twin echo impact 1.3");
            Check(NativeEmperorTriggers.Impact("emperor","direct",false,false,9,true,false)==NativeEmperorBolts.DefaultRadius,"storm SHOCK impact .75");
            Check(NativeEmperorTriggers.Impact("emperor","direct",false,false,9,false,false)==0,"other Emperor direct hit: no impact");
            Check(NativeEmperorTriggers.Impact("emperor","secondary",false,false,9,false,true)==1.8,"bound or wall splat 1.8");
            Check(NativeEmperorTriggers.Impact("emperor","secondary",false,false,9,false,false)==0,"other Emperor splash: no impact");
            Check(NativeEmperorTriggers.Impact("emperor","fire",true,true,.13f,true,true)==0,"damage over time: no impact");
        }
    }
}
#endif
