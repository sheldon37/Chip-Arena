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
        // The runtime: the live pilot's body jiggles after it is posed, only on the sausage's twins (made under the spine's bones),
        // and a hat rides the head's twin.
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        var body=R("NativeVisualBodyPresentation.cs");var dog=R("NativeHotDogPresentation.cs");
        Check(body.Contains("VisualBigHead(body);\n            HotDogJiggle(body);")||body.Contains("VisualBigHead(body);\r\n            HotDogJiggle(body);"),"the jiggle runs once the body is posed");
        Check(dog.Contains("foreach(var t in body.Root.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith(HotDogTwin,StringComparison.Ordinal))")&&dog.Contains("bool twin=name.StartsWith(\"~\",StringComparison.Ordinal)"),"the jiggle moves only the twins");
        Check(body.Contains("{var head=HotDogHatOn(VisualHeadOf(body));if(head!=null)return VisualHatFit(head,hat,body.Helm);}")&&body.Contains("var dogHat=HotDogHatOn(c.Bones[c.Head]);if(dogHat!=c.Bones[c.Head])corpseHat=VisualHatFit(dogHat,hat);")&&dog.Contains("if(hotDogBones[i]==\"~head\"){var hat=new GameObject(HotDogHat).transform;hat.SetParent(twin,false);"),"a hat rides the sausage's crown, alive or dead");
        Check(body.Contains("if(NativeHotDog.Worn(look))simulate=false;")&&body.Contains("if(!NativeHotDog.Worn(look))VisualSkeletonMake(root,skeleton);"),"no human skeleton in the sausage (and no hair or cloth simulated), alive or dead");
        Check(R("NativeSheldonHoodPresentation.cs").Contains("if(hoodFailed||HotDogOn(root))return null;")&&NativeRobotSuit.Shown(NativeLook.With("",NativeLook.Upper,NativeHotDog.Id),false)==0&&NativeRobotSuit.Shown("",false)!=0,"no Sheldon hood or robot plates on it");
    }
}
