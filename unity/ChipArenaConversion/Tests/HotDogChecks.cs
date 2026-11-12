using System;
using System.IO;
using System.Numerics;
using ChipArena.UnityConversion;
// The Hot Dog's sausage jiggle (Core NativeHotDog; owner 2026-11-08: "give the body physics"): a spring that lags the body's
// lurches and settles, held in its limits, steady at any frame rate, reset by a teleport; and the runtime keeps it on the
// sausage's twins, never the limbs.
internal static class HotDogChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("Hot Dog: "+why);}
        bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
        const float dt=1f/60;
        // Standing still: nothing moves.
        var j=default(NativeHotDog.Jiggle);var at=new Vector3(3,1,-2);
        for(int i=0;i<120;i++)NativeHotDog.Step(ref j,at,dt);
        Check(j.Primed&&j.Offset.Length()<1e-6f,"still at rest");
        // A start to a run (6.8 m/s ahead in a frame): the top lags back, within its lean, then settles while the run goes on.
        float most=0,first=float.NaN;int peak=0;
        for(int i=0;i<300;i++)
        {
            at+=new Vector3(0,0,6.8f*dt);NativeHotDog.Step(ref j,at,dt);
            if(i==2)first=j.Offset.Z;
            float lean=new Vector3(j.Offset.X,0,j.Offset.Z).Length();if(lean>most){most=lean;peak=i;}
        }
        Check(first<-.02f,"a start leans it back ("+first.ToString("0.000")+" m)");
        Check(most<=NativeHotDog.MaxLean+1e-4f&&peak<20,"its lean held to "+NativeHotDog.MaxLean+" m (most "+most.ToString("0.000")+" at frame "+peak+")");
        Check(j.Offset.Length()<.002f,"it settles in a running stride ("+j.Offset.Length().ToString("0.0000")+" m)");
        // A landing (falling at 6 m/s, then stopped): it squashes, within its limit, and springs back.
        j=default;at=new Vector3(0,5,0);NativeHotDog.Step(ref j,at,dt);
        for(int i=0;i<30;i++){at.Y-=6*dt;NativeHotDog.Step(ref j,at,dt);}
        float squash=0;
        for(int i=0;i<240;i++){NativeHotDog.Step(ref j,at,dt);NativeHotDog.Pose(j,out _,out _,out float s);squash=Math.Max(squash,s);}
        Check(squash>.05f&&squash<=NativeHotDog.MaxSquash+1e-4f,"a landing squashes it ("+squash.ToString("0.000")+")");
        Check(j.Offset.Length()<.002f,"and it springs back");
        // The lean's axis tips the top the offset's way (up crossed with it) and the bend grows to the top.
        j=default;j.Offset=new Vector3(0,0,.1f);NativeHotDog.Pose(j,out var axis,out float angle,out float sq);
        Check(Vector3.Distance(axis,Vector3.UnitX)<1e-4f&&Math.Abs(angle-Math.Atan2(.1,NativeHotDog.Height))<1e-4f&&Math.Abs(sq)<1e-6f,"a lean ahead tips about +x");
        Check(NativeHotDog.Bend(0)==0&&Math.Abs(NativeHotDog.Bend(NativeHotDog.Height)-1)<1e-6f&&NativeHotDog.Bend(.3f)<NativeHotDog.Bend(.6f)&&NativeHotDog.Bend(5)==1,"the bend: none at the pelvis, all at the top");
        // A teleport or a long gap starts it at rest.
        j.Primed=true;j.LastAt=Vector3.Zero;j.Velocity=new Vector3(1,1,1);NativeHotDog.Step(ref j,new Vector3(10,0,0),dt);
        Check(j.Offset==Vector3.Zero&&j.Velocity==Vector3.Zero&&j.LastAt==new Vector3(10,0,0),"a teleport resets it");
        // Any frame rate: a weaving, jumping run at random steps stays finite and in its limits.
        var rng=new Random(7);j=default;at=Vector3.Zero;float t=0;
        for(int i=0;i<4000;i++)
        {
            float h=(float)(1/30.0+rng.NextDouble()*(1/30.0-1/240.0))*(rng.Next(9)==0?.1f:1);t+=h;
            at=new Vector3(MathF.Sin(t*3)*4,MathF.Max(0,MathF.Sin(t*5))*1.2f,t*5);NativeHotDog.Step(ref j,at,h);
            var side=new Vector3(j.Offset.X,0,j.Offset.Z);
            Check(Finite(j.Offset)&&Finite(j.Velocity)&&side.Length()<=NativeHotDog.MaxLean+1e-4f&&Math.Abs(j.Offset.Y)<=NativeHotDog.MaxSquash*NativeHotDog.Height+1e-4f,"bounded at any frame rate (step "+i+")");
        }
        // Hats on the crown: every hat the game offers has its fit (scaled about the band, its band round the sausage and lifted so
        // its crown clears the dome), the closed ones hide the sprigs and the open ones show them.
        foreach(var hat in NativeModelHatMeshes.Hats)
        {
            NativeHotDog.HatFit(hat,out float k,out var move);var band=NativeHotDog.HatPivot*k+move;
            Check(k>.8f&&k<1.25f&&band.Y>=NativeModelHatMeshes.BandY+.01f&&band.Z>NativeModelHatMeshes.BandZ,"the "+hat+"'s fit on the crown: its band lifted and ahead");
            Check(NativeHotDog.HidesSprigs(hat)==(hat!="crown"&&hat!="antenna"&&hat!="halo"),"the "+hat+" hides the sprigs only if it closes over the crown");
        }
        NativeHotDog.HatFit("no-such-hat",out float k0,out var m0);NativeHotDog.HatFit("cap",out float k1,out var m1);
        Check(k0==k1&&m0==m1&&NativeHotDog.HidesSprigs(NativeHatLooks.SheldonHead)&&!NativeHotDog.HidesSprigs(NativeAuditorHalo.Hat),"an unlisted hat takes the cap's fit; the Sheldon Head covers the sprigs, the auditor's halo doesn't");
        // The runtime: the live pilot's body jiggles after it is posed, only on the sausage's twins (made under the spine's bones),
        // and a hat rides the head's twin.
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        var body=R("NativeVisualBodyPresentation.cs");var dog=R("NativeHotDogPresentation.cs");
        Check(body.Contains("VisualBigHead(body);\n            HotDogJiggle(body);")||body.Contains("VisualBigHead(body);\r\n            HotDogJiggle(body);"),"the jiggle runs once the body is posed");
        Check(dog.Contains("if(drawn!=null)foreach(var t in drawn.bones)if(t!=null&&t.name.StartsWith(HotDogTwin,StringComparison.Ordinal))")&&!dog.Contains("body.Root.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith(HotDogTwin")&&dog.Contains("bool twin=name.StartsWith(\"~\",StringComparison.Ordinal)"),"the jiggle moves only the twins the sausage is skinned to (not the motion and stride proxies' copies)");
        Check(body.Contains("{var head=HotDogHatOn(hat,VisualHeadOf(body));if(head!=null)return VisualHatFit(head,hat,body.Helm);}")&&body.Contains("var dogHat=HotDogHatOn(hat,c.Bones[c.Head]);if(dogHat!=c.Bones[c.Head])corpseHat=VisualHatFit(dogHat,hat);")&&dog.Contains("if(hotDogBones[i]==\"~head\"){var hat=new GameObject(HotDogHat).transform;hat.SetParent(twin,false);}")&&dog.Contains("var at=m.MultiplyPoint3x4(crownHead+new Vector3(move.X,move.Y,move.Z));")&&body.Contains("HotDogUnderHat(body?.Root,hat);")&&body.Contains("HotDogUnderHat(root,death.actor?.Hat);"),"a hat rides the sausage's crown, alive or dead");
        Check(body.Contains("if(NativeHotDog.Worn(look))simulate=false;")&&R("NativeVisualSkeletonPresentation.cs").Contains("if(HotDogOn(root))return null;"),"no human skeleton in the sausage (and no hair or cloth simulated), alive or dead");
        Check(NativeHotDog.SpineEven>0&&NativeHotDog.SpineEven<=1&&NativeHotDog.SpinePasses>=1&&dog.Contains("eased[k]=Quaternion.Slerp(eased[k],mid,.5f);")&&dog.Contains("Matrix4x4.TRS(bone.position,even,bone.lossyScale)"),"the sausage's bend kept even down its twins (no fold in a deep curl)");
        Check(body.Contains("if(NativeHotDog.Worn(VisualOutfitOf(id)))return; // (None on the Hot Dog")&&R("NativeSheldonHoodPresentation.cs").Contains("if(hoodFailed||HotDogOn(root))return null;")&&NativeRobotSuit.Shown(NativeLook.With("",NativeLook.Upper,NativeHotDog.Id),false)==0&&NativeRobotSuit.Shown("",false)!=0,"no face gear, Sheldon hood or robot plates on it");
            // The face (Core NativeHotDogFace; owner 2026-11-09: "animate eyes and mouth with hotdog character. Give them expressions"): every
        // mood a sane pose; a laugh and a shout open the mouth, anger knits the brows, death crosses the eyes; blinks are brief and come
        // every few seconds; easing heads for the target and settles on it; and the moods follow the pilot and the intro's beats.
        foreach(HotDogMood m in Enum.GetValues(typeof(HotDogMood)))for(float ft=0;ft<3;ft+=.37f)
        {
            var p=NativeHotDogFace.At(m,ft,ft+5,17);
            Check(p.OpenL>=0&&p.OpenL<=1&&p.OpenR>=0&&p.OpenR<=1&&p.Open>=0&&p.Open<=1.01f&&p.Smile>=-1&&p.Smile<=1&&p.Wide>=0&&p.Wide<=1&&p.Pupil>.3f&&p.Pupil<1.5f,"a sane pose for "+m);
            Check(Math.Abs(p.LookX)<=1.3f&&Math.Abs(p.LookY)<=1.3f&&p.BrowL>=-1&&p.BrowL<=1&&p.BrowR>=-1&&p.BrowR<=1,"the eyes and brows in range for "+m);
        }
        var idle=NativeHotDogFace.Target(HotDogMood.Idle,0,0);
        Check(NativeHotDogFace.Target(HotDogMood.Laugh,.3f,0).Open>idle.Open+.3f&&NativeHotDogFace.Target(HotDogMood.Shout,.3f,0).Open>idle.Open+.3f,"a laugh and a shout open the mouth");
        Check(NativeHotDogFace.Target(HotDogMood.Angry,0,0).BrowL<idle.BrowL&&NativeHotDogFace.Target(HotDogMood.Ouch,0,0).BrowL>0,"anger knits the brows, a hit lifts them");
        Check(NativeHotDogFace.Target(HotDogMood.Dead,0,0).Cross&&!idle.Cross&&NativeHotDogFace.Target(HotDogMood.Wink,0,0).OpenL<.1f,"X eyes in death, one eye shut in a wink");
        int blinks=0;float shut=0;for(float ft=0;ft<30;ft+=.01f){float b=NativeHotDogFace.Blink(ft,3);if(b>.5f&&shut<=.5f)blinks++;shut=b;}
        Check(blinks>=6&&blinks<=14,"a blink every few seconds ("+blinks+" in 30 s)");
        int open=0;for(float ft=0;ft<30;ft+=.01f)if(NativeHotDogFace.Blink(ft,3)<.05f)open++;Check(open>2700,"the eyes mostly open");
        var from=NativeHotDogFace.Target(HotDogMood.Idle,0,0);var to=NativeHotDogFace.Target(HotDogMood.Laugh,0,0);var cur=from;
        cur=NativeHotDogFace.Ease(cur,to,1f/60);Check(cur.Open>from.Open&&cur.Open<to.Open,"easing moves part way in a frame");
        for(int i=0;i<60;i++)cur=NativeHotDogFace.Ease(cur,to,1f/60);Check(Math.Abs(cur.Open-to.Open)<.01f&&Math.Abs(cur.Smile-to.Smile)<.01f,"and settles within a second");
        var a=new Actor{Hp=100,FlashUntil=10.14f,AttackAt=-100};
        Check(NativeHotDogFace.Of(a,null,false,10.3f,out _)==HotDogMood.Ouch&&NativeHotDogFace.Of(a,null,false,10.14f+NativeHotDogFace.OuchHold+.01f,out _)==HotDogMood.Idle,"a hit winces past its flash, then lets go");
        Check(NativeHotDogFace.Of(a,null,true,20,out _)==HotDogMood.Dizzy,"a daze rolls the eyes");
        a.AttackAt=30;Check(NativeHotDogFace.Of(a,null,false,30.2f,out _)==HotDogMood.Angry&&NativeHotDogFace.Of(a,null,false,31,out _)==HotDogMood.Idle,"a swing is angry, briefly");
        var kit=new NativeTopDogKit{Feast=new NativeDogFeast{At=40,Catch=41,FeedAt=42,Release=46,Until=48}};
        Check(NativeHotDogFace.Of(a,kit,false,41.5f,out _)==HotDogMood.Grin&&NativeHotDogFace.Of(a,kit,false,43,out _)==HotDogMood.Hungry&&NativeHotDogFace.Of(a,kit,false,47,out _)==HotDogMood.Laugh,"the feast: a grin on the catch, hungry feeding, a laugh at the pop");
        kit.Feast.Snap=44;Check(NativeHotDogFace.Of(a,kit,false,44.5f,out _)==HotDogMood.Angry,"angry when the feast is snapped");
        Check(NativeHotDogFace.Intro(NativeTopDogIntro.ApexAt+.1f,out _)==HotDogMood.Laugh&&NativeHotDogFace.Intro(NativeTopDogIntro.SlamAt,out _)==HotDogMood.Shout&&NativeHotDogFace.Intro(NativeTopDogIntro.WinkAt+.2f,out _)==HotDogMood.Wink,"Order Up!: a laugh at the spring, a shout on the slam, the wink");
        for(float ft=0;ft<NativeTopDogIntro.Total;ft+=.05f){NativeHotDogFace.Intro(ft,out float since);Check(since<=ft+1e-4f,"an intro mood begun no later than now at "+ft.ToString("0.00"));}
        // The runtime: the painted face is blanked at load, the toon face rides the head's twin, and the mood source is set each frame.
        var face=R("NativeHotDogFacePresentation.cs");
        Check(dog.Contains("else{tri=HotDogFaceOff(tex,pos,nor,uv,weights,tri);")&&dog.Contains("if(head>=0)HotDogFaceAttach(bones[head],r);")&&R("NativeTopDogPresentation.cs").Contains("HotDogFaceSource=hotDogFaceSource??=HotDogFaceMood;"),"the face is blanked, attached and driven");
        Check(face.Contains("if(dead)mood=HotDogMood.Dead;")&&face.Contains("if(!host){if(view.enabled){view.enabled=false;hidden=true;}return;}"),"a corpse's face is dead, and it hides with the Hot Dog without overriding the body's own hiding");
        Check(face.Contains("var sr=go.AddComponent<SkinnedMeshRenderer>();")&&face.Contains("sr.bones=hotDog.bones;sr.rootBone=hotDog.rootBone;")&&face.Contains("weights[i]=fit.WeightAt(fit.Theta(q),q.Y);"),"the face is skinned on the Hot Dog's bones, each corner weighted as the skin under it, so it stays on the head (owner 2026-11-09)");
        Check(body.Contains("if(!NativeHotDog.Worn(body.Look)&&!NativeHotDog.Worn(VisualOutfitOf(a.Id)))"),"no chest core on the Hot Dog (owner 2026-11-09)");
        Check(R("NativeWeaponPresentation.cs").Contains("if(!first&&(bone==CapturedRig.RHand||bone==CapturedRig.LHand))m=PalmGrip(rig,a,m,bone==CapturedRig.LHand);")&&R("NativeWeaponPresentation.cs").Contains("PalmGrip(CapturedAnimation.Player rig,Actor a,Matrix4x4 m,bool left)=>a==null?m:PalmGrip(rig,a.SourceSpec,m,left);")&&R("NativeWeaponPresentation.cs").Contains("if(spec!=NativeTopDog.Spec&&spec!=NativeBombs.Spec&&spec!=NativeGlaive.Spec&&!NativeBlades.Is(spec))return m;"),"the Top Dog's mallet and the Bombadier's TNT through the palm, not at the wrist (owner 2026-11-09: \"the hands are off the handle\")");
    }
}
