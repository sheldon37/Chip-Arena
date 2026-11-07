#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Faces on the model (Core/NativeFace; owner 2026-10-30: "Characters are currently emotionless"): the shapes it drives are the
    // pack head's own (as Unity listed them on Head_AnimeYoung), every mood in range, a pilot's face following what it does (a set
    // focus in a match, a shout on a blow, a wince on a hit, a grin after a kill, a smile in the menus, closed eyes once dead), the
    // blink on its own clock, easing without jumps, each zombie kind's own face, its jaw where the gait opens it, the spitter's
    // pucker, a dying slack; nothing allocated a frame, the same input the same face; and the runtime's wiring. A synthetic
    // face, not a render: how it reads on screen is the owner's PC's to judge.
    internal static class FaceChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Faces: "+what);count++;}
        private const float Dt=1/60f;
        // The Expression_ shapes on the pack's Head_AnimeYoung (Unity, 2026-10-30; the lashes carry the same 43, the brows the 8
        // brow shapes, the teeth the 23 mouth and jaw shapes).
        private static readonly string[] Head={"Mouth_Grimace_R","Mouth_Grimace_L","Mouth_Pucker_R","Mouth_Pucker_L","Mouth_Wide_R","Mouth_Wide_L","Mouth_Sad_R","Mouth_Sad_L","Mouth_Happy_R","Mouth_Happy_L",
            "LipsI","LipsU","LipsO","LipsE","LipsA","BigSmile","ClentchedTeeth","SurpriesedMouth","TongueExtend","KittyMouth","Jaw_Right","Jaw_Left","Jaw_Open","Brows_Raised_R","Brows_Raised_L",
            "Brows_Lowered_R","Brows_Lowered_L","Brows_Angry_R","Brows_Angry_L","Brows_Sad_R","Brows_Sad_L","Eyes_UpperClosed_R","Eyes_UpperClosed_L","Eyes_LowerClosedUp_R","Eyes_LowerClosedUp_L",
            "Eyes_LowerOpen_R","Eyes_LowerOpen_L","Eyes_UpperOpen_R","Eyes_UpperOpen_L","Eyes_LowerClosed_R","Eyes_LowerClosed_L","BlankEyes","BlankMouth"};
        private static int I(string shape)=>Array.IndexOf(NativeFace.Shapes,shape);
        private static float W(NativeFace.Face f,string shape)=>f.Weight[I(shape)];
        private static NativeFace.Pilot Idle(float t,bool arena=true)=>new NativeFace.Pilot{Time=t,Health=1,Stamina=100,SinceHit=99,SinceAttack=99,SinceSpecial=99,SinceAbility=99,SinceShot=99,SinceBlock=99,SinceRoll=99,Arena=arena,Alive=true};
        public static void Run(string root)
        {
            Check(NativeFace.Shapes.Distinct().Count()==NativeFace.Count&&NativeFace.Shapes.All(s=>Head.Contains(s)),"every shape the face drives is on the pack's head ("+NativeFace.Count+" of "+Head.Length+")");
            Check(NativeFace.Moods.Length==Enum.GetValues(typeof(NativeFace.Mood)).Length&&NativeFace.Moods.All(m=>m.Length==NativeFace.Count&&m.All(w=>w>=0&&w<=100)),"every mood a weight per shape, 0..100");
            // A pilot in a match: a set focus; a blow shouts; a hit winces; back to focus; a kill grins; dead, its eyes close.
            var f=new NativeFace.Face(7);float t=0;
            for(int i=0;i<60;i++){t+=Dt;NativeFace.PilotFace(f,Idle(t));}
            Check(W(f,"Brows_Lowered_L")>30&&W(f,"Mouth_Happy_L")<5,"in a match a set, narrowed focus (brows lowered "+W(f,"Brows_Lowered_L").ToString("0")+")");
            float hit=t;for(int i=0;i<8;i++){t+=Dt;var p=Idle(t);p.SinceHit=t-hit;p.HitStrength=1;NativeFace.PilotFace(f,p);}
            Check(W(f,"Brows_Sad_L")>40&&W(f,"Mouth_Grimace_L")>40&&W(f,"Eyes_UpperClosed_R")>35,"a hit winces within 0.13 s (brows sad "+W(f,"Brows_Sad_L").ToString("0")+", grimace "+W(f,"Mouth_Grimace_L").ToString("0")+")");
            for(int i=0;i<70;i++){t+=Dt;var p=Idle(t);p.SinceHit=t-hit;p.HitStrength=1;NativeFace.PilotFace(f,p);}
            Check(W(f,"Brows_Sad_L")<10&&W(f,"Brows_Lowered_L")>30,"and eases back to focus within 1.3 s");
            float swing=t;for(int i=0;i<8;i++){t+=Dt;var p=Idle(t);p.SinceAttack=t-swing;p.Attacking=true;NativeFace.PilotFace(f,p);}
            Check(W(f,"Jaw_Open")>30&&W(f,"Brows_Angry_L")>50,"a blow shouts (jaw "+W(f,"Jaw_Open").ToString("0")+")");
            for(int i=0;i<60;i++){t+=Dt;var p=Idle(t);p.Kills=0;NativeFace.PilotFace(f,p);}
            for(int i=0;i<20;i++){t+=Dt;var p=Idle(t);p.Kills=1;NativeFace.PilotFace(f,p);}
            Check(W(f,"Mouth_Happy_L")>45,"a kill grins ("+W(f,"Mouth_Happy_L").ToString("0")+")");
            for(int i=0;i<150;i++){t+=Dt;var p=Idle(t);p.Kills=1;NativeFace.PilotFace(f,p);}
            Check(W(f,"Mouth_Happy_L")<10,"and the grin passes");
            for(int i=0;i<40;i++){t+=Dt;var p=Idle(t);p.Alive=false;NativeFace.PilotFace(f,p);}
            Check(W(f,"Eyes_UpperClosed_L")>70&&W(f,"Jaw_Open")>20,"dead, its eyes close and its jaw slackens");
            var menu=new NativeFace.Face(8);for(int i=0;i<60;i++)NativeFace.PilotFace(menu,Idle(i*Dt,false));
            Check(W(menu,"Mouth_Happy_L")>40,"in the menus a smile");
            // The blink: on its own clock, quick, between 2.2 and 5.6 s apart (now and then twice); open between.
            var b=new NativeFace.Face(11);int blinks=0;bool shut=false;float open=0;float worstJump=0;var last=new float[NativeFace.Count];
            for(int i=0;i<1800;i++)
            {
                NativeFace.PilotFace(b,Idle(i*Dt));float eye=W(b,"Eyes_UpperClosed_L");if(eye>80&&!shut){blinks++;shut=true;}if(eye<20)shut=false;if(eye<10)open++;
                if(i>0)for(int k=0;k<NativeFace.Count;k++){if(k==I("Eyes_UpperClosed_L")||k==I("Eyes_UpperClosed_R"))continue;worstJump=Math.Max(worstJump,Math.Abs(b.Weight[k]-last[k]));}
                Array.Copy(b.Weight,last,NativeFace.Count);
            }
            Check(blinks>=5&&blinks<=16&&open>1800*.9f,"it blinks on its own clock ("+blinks+" in 30 s, open "+(open/18).ToString("0")+"% of the time)");
            Check(worstJump<1,"a steady face holds still between blinks");
            // Easing: the hardest switch (a strong hit out of a shout) moves no shape more than 40 points in a 60 Hz frame.
            var e=new NativeFace.Face(3);float jump=0;t=0;
            for(int i=0;i<120;i++)
            {
                t+=Dt;var p=Idle(t);if(i<60){p.Attacking=true;p.SinceAttack=0;}else{p.SinceHit=(i-60)*Dt;p.HitStrength=1;}
                Array.Copy(e.Weight,last,NativeFace.Count);NativeFace.PilotFace(e,p);
                if(i>0)for(int k=0;k<NativeFace.Count;k++){if(k==I("Eyes_UpperClosed_L")||k==I("Eyes_UpperClosed_R"))continue;jump=Math.Max(jump,Math.Abs(e.Weight[k]-last[k]));}
            }
            Check(jump<=40,"moods ease in and out (at most "+jump.ToString("0")+" points a frame)");
            // Zombies: each kind its own resting face; the jaw where the gait opens it, snarling wide; the spitter's pucker; a
            // hit winces; dying, slack.
            var rest=new Dictionary<int,float[]>();
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Spitter,NativeHordeKinds.Brute,NativeHordeKinds.Hulk})
            {
                var z=new NativeFace.Face(kind);for(int i=0;i<60;i++)NativeFace.ZombieFace(z,new NativeFace.Zombie{Time=i*Dt,Kind=kind});rest[kind]=(float[])z.Weight.Clone();
                for(int i=60;i<80;i++)NativeFace.ZombieFace(z,new NativeFace.Zombie{Time=i*Dt,Kind=kind,Jaw=1});
                Check(W(z,"Jaw_Open")>80,NativeHordeKinds.Ids[kind]+": its jaw opens where the gait opens it ("+W(z,"Jaw_Open").ToString("0")+")");
                if(kind!=NativeHordeKinds.Shambler)Check(W(z,"Mouth_Wide_L")>50,NativeHordeKinds.Ids[kind]+": a bite snarls wide");
                for(int i=80;i<120;i++)NativeFace.ZombieFace(z,new NativeFace.Zombie{Time=i*Dt,Kind=kind,Dying=true});
                Check(W(z,"Jaw_Open")>=40&&W(z,"Brows_Angry_L")<10,NativeHordeKinds.Ids[kind]+": dying, slack");
            }
            var kinds=rest.Keys.ToArray();
            for(int a=0;a<kinds.Length;a++)for(int c=a+1;c<kinds.Length;c++)Check(rest[kinds[a]].Zip(rest[kinds[c]],(x,y)=>Math.Abs(x-y)).Sum()>60,NativeHordeKinds.Ids[kinds[a]]+" and "+NativeHordeKinds.Ids[kinds[c]]+" rest with their own faces");
            Check(rest[NativeHordeKinds.Hulk][I("Brows_Angry_L")]>90&&rest[NativeHordeKinds.Shambler][I("LipsO")]>30&&rest[NativeHordeKinds.Runner][I("ClentchedTeeth")]>70,"the Hulk's rage, the shambler's moan, the runner's bared teeth");
            var sp=new NativeFace.Face(5);for(int i=0;i<40;i++)NativeFace.ZombieFace(sp,new NativeFace.Zombie{Time=i*Dt,Kind=NativeHordeKinds.Spitter,Swell=1,Jaw=.5f});
            Check(W(sp,"Mouth_Pucker_L")>50&&W(sp,"Jaw_Open")<40,"the spitter's mouth gathers through a spit");
            var hz=new NativeFace.Face(6);for(int i=0;i<30;i++)NativeFace.ZombieFace(hz,new NativeFace.Zombie{Time=i*Dt,Kind=NativeHordeKinds.Brute,Hurt=true});
            Check(W(hz,"Brows_Sad_L")>30,"a hit winces a zombie too");
            // Nothing allocated a frame; the same input, the same face.
            var g1=new NativeFace.Face(21);var g2=new NativeFace.Face(21);for(int i=0;i<10;i++){NativeFace.PilotFace(g1,Idle(i*Dt));NativeFace.PilotFace(g2,Idle(i*Dt));}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=10;i<600;i++){var p=Idle(i*Dt);p.SinceHit=(i%90)*Dt;p.HitStrength=.8f;NativeFace.PilotFace(g1,p);NativeFace.PilotFace(g2,p);NativeFace.ZombieFace(g1,new NativeFace.Zombie{Time=i*Dt,Kind=NativeHordeKinds.Runner,Jaw=(i%30)/30f});NativeFace.ZombieFace(g2,new NativeFace.Zombie{Time=i*Dt,Kind=NativeHordeKinds.Runner,Jaw=(i%30)/30f});}
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"a face allocates nothing per frame ("+bytes+" bytes)");
            Check(g1.Weight.SequenceEqual(g2.Weight),"the same input, the same face");
            // The runtime's wiring.
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string face=R("Runtime/NativeVisualFacePresentation.cs"),body=R("Runtime/NativeVisualBodyPresentation.cs"),zm=R("Runtime/NativeZombieModelPresentation.cs"),horde=R("Runtime/NativeHordeKindPresentation.cs");
            Check(body.Contains("body.Binding.Apply(pose,grasp,planted,airborne);\n            VisualFacePilot(body,a,time,fresh);"),"every pilot body's face is set where it is posed (the match, the menus, the intros, the draft, a revive)");
            Check(zm.Contains("ZombieFace(z,flash,swell,z.PosedFrame<Time.frameCount-1);")&&horde.Contains("ZombieModelPose(rig,m.Id,kind,s.Tier,planted,airborne,a.Crawl,s.Gait,a.Lost,0,flash,glow.x,swell,s.Jaw)"),"a zombie's face is set where it is posed, its jaw the gait's");
            Check(face.Contains("if(!tail.StartsWith(NativeFace.Prefix,StringComparison.Ordinal))continue;")&&face.Contains("if(b<0||Mathf.Abs(w[k]-shown[k])<.5f)continue;shown[k]=w[k];r.SetBlendShapeWeight(b,w[k]);")&&face.Contains("if(r==null||!r.enabled||!r.gameObject.activeInHierarchy)continue;"),"each face mesh's shapes found by name, written when they move, hidden parts left");
            foreach(string w in new[]{"NativeArena","Authority","Collider","Strike("})Check(!R("Core/NativeFace.cs").Contains(w),"the face reads no rule or authority: "+w);
            Console.WriteLine("PASS faces: "+count+" checks ("+NativeFace.Count+" of the head's expression shapes, "+NativeFace.Moods.Length+" moods: a pilot's focus, shout, wince, grin, smile and death; "+blinks+" blinks in 30 s; at most "+jump.ToString("0")+" points a frame; five zombie kinds' own faces, jaws, the spitter's pucker; nothing allocated a frame; presentation only)");
        }
    }
}
#endif
