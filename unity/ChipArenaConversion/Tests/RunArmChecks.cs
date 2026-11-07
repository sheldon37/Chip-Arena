#if ARENA_TESTS
using System;
using System.IO;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The free arm's run on the player's model (owner 2026-10-29: "the other arm not holding the weapon tends to extend outward";
    // Core/NativeRunArm). Synthetic arms in the chest's frame (x the arm's outward side, y up, z forward; a 28 cm upper arm and a
    // 26 cm forearm): one hanging, one at the walk's carry, one flung out at the shoulder's height as the pack's armed run has it,
    // one raised above it, one swung forward, the same arms on the right; a stride's sweep for smoothness. Then which arms run free
    // per set, the running share of the blend, and the runtime's wiring. The pack's clips are measured in docs/BOZO-INTEGRATION.md
    // (scratch tools, not shipped); how it reads on screen is the owner's PC's to judge.
    internal static class RunArmChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Run arm: "+what);count++;}
        private const float Deg=57.29578f,Upper=.28f,Lower=.26f;
        private static readonly Vector3 Out=Vector3.UnitX,Up=Vector3.UnitY;
        // A direction `out` degrees out of the middle plane, `forward` degrees forward of hanging in it (negative: back), and its
        // height: the vector from out/forward angles in the frontal and sagittal planes.
        private static Vector3 Dir(float outward,float forward,float up=0)
        {
            var v=new Vector3((float)Math.Sin(outward/Deg),-(float)Math.Cos(outward/Deg)*(float)Math.Cos(forward/Deg),(float)Math.Cos(outward/Deg)*(float)Math.Sin(forward/Deg));
            if(up!=0){float a=up/Deg;v=new Vector3(v.X*(float)Math.Cos(a)-v.Y*(float)Math.Sin(a),v.Y*(float)Math.Cos(a)+v.X*(float)Math.Sin(a),v.Z);}
            return Vector3.Normalize(v);
        }
        // The drop applied as the runtime applies it: about the forward axis, the outward side toward down.
        private static Vector3 Turn(Vector3 v,float d)=>new Vector3(v.X*(float)Math.Cos(d)+v.Y*(float)Math.Sin(d),v.Y*(float)Math.Cos(d)-v.X*(float)Math.Sin(d),v.Z);
        private static float OutOf(Vector3 v)=>(float)Math.Asin(Math.Max(-1,Math.Min(1,Vector3.Dot(Vector3.Normalize(v),Out))))*Deg;
        private struct Arm { public Vector3 S,E,W; }
        private static Arm Make(Vector3 upper,Vector3 fore)=>new Arm{S=Vector3.Zero,E=upper*Upper,W=upper*Upper+fore*Lower};
        private static float Drop(Arm a)=>NativeRunArm.Drop(a.S,a.E,a.W,Out,Up);
        private static Arm After(Arm a,float d)=>new Arm{S=a.S,E=Turn(a.E,d),W=Turn(a.W,d)};

        public static void Run(string root)
        {
            count=0;
            // The soft cap: kept to `from`, eased past it, never reaching the cap, rising.
            Check(NativeRunArm.Soft(20,26,34)==20&&NativeRunArm.Soft(26,26,34)==26,"the soft cap keeps angles up to its start");
            float lastSoft=26;for(float a=26.5f;a<=120;a+=.5f){float s=NativeRunArm.Soft(a,26,34);Check(s>=lastSoft&&s<34&&s<=a,"the soft cap rises, under the cap and the angle, at "+a);lastSoft=s;}
            Check(Math.Abs(NativeRunArm.Soft(26.01f,26,34)-26.01f)<.001f,"the soft cap's slope is one where it starts (no kink)");

            // Hanging and at the walk's carry: left alone.
            var hang=Make(Dir(6,0),Dir(8,20));Check(Drop(hang)==0,"a hanging arm is left alone");
            var walk=Make(Dir(24,5),Dir(23,25));Check(Drop(walk)*Deg<1.5f,"the walk's carry (24 out) is left nearly alone: "+Drop(walk)*Deg);
            var natural=Make(Dir(25,-30),Dir(10,40));Check(Drop(natural)*Deg<1.5f,"a plain run's swing (the pack's Relax run) is left nearly alone: "+Drop(natural)*Deg);
            // Flung out at the shoulder's height (the armed run's free arm at mid-stride: 70 out, 19 forward, near level; the
            // forearm on out and forward).
            var flung=Make(Dir(70,60),Dir(45,70));float d=Drop(flung);var after=After(flung,d);
            Check(d>20/Deg&&d<=NativeRunArm.MaxDrop/Deg+1e-4f,"a flung arm is lowered: "+d*Deg);
            Check(OutOf(after.W-after.S)<=NativeRunArm.HandCap+1.5f,"its hand line comes in to the cap: "+OutOf(after.W-after.S));
            Check(OutOf(after.E-after.S)>=NativeRunArm.UpperMin-.01f,"its upper arm stays off the chest: "+OutOf(after.E-after.S));
            Check(after.W.Y<flung.W.Y&&after.W.Y<0,"its hand comes under the shoulder's height: "+flung.W.Y+" -> "+after.W.Y);
            Check(Math.Abs(after.W.Z-flung.W.Z)<1e-4f&&Math.Abs(after.E.Z-flung.E.Z)<1e-4f,"its swing forward and back is the clip's (the turn is about the forward axis)");
            Check(Math.Abs((after.W-after.E).Length()-Lower)<1e-4f&&Math.Abs((after.E-after.S).Length()-Upper)<1e-4f
                &&Math.Abs(Vector3.Dot(Vector3.Normalize(after.E-after.S),Vector3.Normalize(after.W-after.E))-Vector3.Dot(Vector3.Normalize(flung.E-flung.S),Vector3.Normalize(flung.W-flung.E)))<1e-4f,"its bend and lengths are the clip's");
            // Raised over the shoulder to the side: lowered by at most MaxDrop, under the shoulder line.
            var raised=Make(Dir(80,10,25),Dir(60,30,25));float dr=Drop(raised);var rAfter=After(raised,dr);
            Check(dr<=NativeRunArm.MaxDrop/Deg+1e-4f&&dr>40/Deg,"an arm raised over the shoulder to the side comes down, at most the cap: "+dr*Deg);
            Check(rAfter.E.Y<raised.E.Y&&OutOf(rAfter.W-rAfter.S)<OutOf(raised.W-raised.S),"its elbow lowers and its hand comes in");
            // Swung forward (out of the frontal plane): little or nothing, however it stands out of the middle plane.
            var ahead=Make(Dir(18,70),Dir(10,90));Check(Drop(ahead)==0,"an arm swung forward close in is left alone");
            var aheadOut=Make(Dir(40,60),Dir(30,80));Check(Drop(aheadOut)*Deg<6,"an arm swung forward, a little out, is barely turned (the turn moves it little): "+Drop(aheadOut)*Deg);
            // The same on the right (mirrored: x negated, outward -x).
            var mirror=new Arm{S=Vector3.Zero,E=new Vector3(-flung.E.X,flung.E.Y,flung.E.Z),W=new Vector3(-flung.W.X,flung.W.Y,flung.W.Z)};
            Check(Math.Abs(NativeRunArm.Drop(mirror.S,mirror.E,mirror.W,-Out,Up)-d)<1e-5f,"the right arm, mirrored, turns the same");
            // An arm across the body (its hand inward) or degenerate is left alone.
            Check(Drop(Make(Dir(-20,30),Dir(-30,60)))==0,"an arm across the body is left alone");
            Check(NativeRunArm.Drop(Vector3.Zero,Vector3.Zero,Vector3.Zero,Out,Up)==0,"a degenerate arm is left alone");
            // A stride's sweep (the armed run's free arm: out 30 to 70 and back, swung 15 to 60 forward, its forearm 40-50 out):
            // smooth from frame to frame (120 per stride), never NaN, every frame inside the caps.
            float last=float.NaN,most=0,worstHand=0;
            for(int i=0;i<=240;i++)
            {
                float t=i/120f*2*(float)Math.PI,o=50-20*(float)Math.Cos(t),f=37-22*(float)Math.Cos(t),fo=45-5*(float)Math.Cos(t),lift=12*(float)Math.Max(0,-Math.Cos(t));
                var a=Make(Dir(o,f,lift),Dir(fo,f+20,lift));float dd=Drop(a);
                Check(!float.IsNaN(dd)&&dd>=0,"the sweep's drop is a number");
                if(!float.IsNaN(last))most=Math.Max(most,Math.Abs(dd-last)*Deg);last=dd;
                var b=After(a,dd);worstHand=Math.Max(worstHand,OutOf(b.W-b.S));
                Check(OutOf(b.E-b.S)>=NativeRunArm.UpperMin-.01f||OutOf(a.E-a.S)<=NativeRunArm.UpperMin,"the sweep never lays the upper arm on the chest");
            }
            Check(most<2.5f,"the sweep's drop changes smoothly: at most "+most+" degrees a frame at 120 a stride");
            Check(worstHand<=NativeRunArm.HandCap+6,"the sweep's hand line stays near the cap: "+worstHand);

            // Which arms run free.
            string[][] free={new[]{"sword","L"},new[]{"pistol","L"},new[]{"daggers","L"},new[]{"shield","L"},new[]{"water","R"},new[]{"fire","R"},new[]{"leet","R"},new[]{"bow","R"},new[]{"emperor","LR"},new[]{"ansatsuken","LR"},new[]{"greatsword",""},new[]{"rifle",""},new[]{"machinegun",""}};
            foreach(var f in free){var set=NativeVisualMotion.For(f[0]);Check(set!=null,"a set for "+f[0]);Check(NativeRunArm.Free(set,true)==f[1].Contains("L")&&NativeRunArm.Free(set,false)==f[1].Contains("R"),"free arms of "+f[0]+": "+f[1]);}
            Check(!NativeRunArm.Free(null,true),"no set, no free arm");
            // The running share: the runs and the sprint, not the walks.
            var w=new float[NativeVisualStride.Clips];w[0]=.5f;w[NativeVisualStride.Walks]=.5f;Check(Math.Abs(NativeRunArm.RunShare(w)-.5f)<1e-6f,"the run's half of a walk-to-run blend");
            w=new float[NativeVisualStride.Clips];w[NativeVisualStride.Walks+3]=1;Check(NativeRunArm.RunShare(w)==0,"a walk alone has no running share");
            w=new float[NativeVisualStride.Clips];w[NativeVisualStride.Sprint]=.7f;w[2]=.3f;Check(Math.Abs(NativeRunArm.RunShare(w)-1)<1e-6f,"a sprint with a side run counts whole");
            Check(NativeRunArm.RunShare(null)==0,"no blend, no share");

            // The runtime: after the feet, before the sword's carry and the hold; by the running share, not under a raised upper
            // layer; both sides tried (Free picks); the turn's sense taken from the outward side.
            string motion=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeVisualMotionPresentation.cs"));
            int feet=motion.IndexOf("VisualMotionFeet(m,body.Root.transform,plant,pick.Locomotion,dt,wgt);"),arm=motion.IndexOf("if(run>0){VisualRunArm(m,true,run);VisualRunArm(m,false,run);}"),trail=motion.IndexOf("if(m.Trail>0)VisualSwordTrail(");
            Check(feet>0&&arm>feet&&trail>arm,"the free arm's run after the feet and before the sword's carry");
            Check(motion.Contains("float run=covered&&pick.Locomotion?NativeRunArm.RunShare(m.LocoWeights)*loco*wgt*(1-m.Upper):0;"),"by the running share and the motion's weight, not under a raised upper layer");
            Check(motion.Contains("if(!NativeRunArm.Free(m.Set,left)")&&motion.Contains("var turn=Quaternion.AngleAxis(drop,forward);if(Vector3.Dot(turn*outward,up)>0)turn=Quaternion.AngleAxis(-drop,forward);")&&motion.Contains("upper.rotation=turn*upper.rotation;"),"only a free arm, out toward down, the whole arm about the shoulder");
            Console.WriteLine($"  flung arm: lowered {d*Deg:0} deg, its hand line {OutOf(flung.W-flung.S):0} -> {OutOf(after.W-after.S):0} out, its upper arm {OutOf(flung.E-flung.S):0} -> {OutOf(after.E-after.S):0}, its hand {flung.W.Y*100:0} -> {after.W.Y*100:0} cm against the shoulder; raised arm lowered {dr*Deg:0} deg; stride: at most {most:0.0} deg a frame, the hand line at most {worstHand:0} out");
            Console.WriteLine("PASS run arm: "+count+" checks (a flung free arm lowered in the frontal plane to the caps, its swing, bend and hand kept; hanging, walking and plain-run arms left alone; smooth over a stride; the free arms per set; the running share; the runtime's place)");
        }
    }
}
#endif
