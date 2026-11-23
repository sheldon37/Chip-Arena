#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using ChipArena.UnityConversion;

/// <summary>
/// The guns' and casters' grounded stride (Core/NativeGroundedStride, Core/NativeVisualMotion.Stride, Runtime/NativeVisualMotionPresentation.cs;
/// owner 2026-11-23: "Currently, its very 'hoppy' looking. Lets make it look more realistic (Mostly on guns and magic) think melee is fine?";
/// docs/RUN-REALISTIC.md). Held without the local bank (a synthetic book stands in): a bank without the stride's clips keeps every set's own
/// stride; with them the pistol, the rifle and machine gun, the casters and the Bombadier stride on it in all eight ways with its walks and
/// sprint, the casters carrying the staff steady over it, the gun pack's carry still over the guns' legs; melee, the bow, the fighters and
/// the DEV classes keep theirs; the stride's own travel (the bake's, measured on its planted feet) keeps the feet planted at the rules' run
/// and sprint; nothing is written to a rule, and the clips and the bank stay out of git.
/// </summary>
internal static class GroundedStrideChecks
{
    private static int count;
    private static void Check(bool ok,string what){count++;if(!ok)throw new InvalidOperationException("Grounded stride: "+what);}
    private sealed class Book:NativeVisualMotion.IClips
    {
        public bool Stride=true, Carry=true;
        public bool Has(string c)=>c!=null&&(c==NativeGroundedStride.StaffCarry?Carry:Array.IndexOf(NativeGroundedStride.Stride,c)>=0?Stride:true);
        public float Length(string c)=>1.2f;
        public float Hit(string c)=>.6f;
    }
    public static void Run(string root)
    {
        count=0;var book=new Book();
        Sets(book);Travel();Sources(root);
        Console.WriteLine("PASS grounded stride (realistic gun and magic runs): "+count+" checks: the old strides without the bank's clips, the guns, casters and Bombadier on the grounded 8-way stride with them, the staff carried steady, melee unchanged, the feet planted at the rules' run and sprint, presentation only");
    }
    private static readonly string[] Covered={"pistol","rifle","machinegun","fire","water","lightning","darkness","leet",NativeBombs.Spec};
    private static readonly string[] Kept={"sword","greatsword","daggers","shield","bow","emperor","ansatsuken",NativeTopDog.Spec,NativeGlaive.Spec,NativeKatana.Spec,NativeFangs.Spec,NativeDevMotions.Greatsword,NativeDevMotions.Katana};
    private static void Sets(Book book)
    {
        var s=NativeGroundedStride.Stride;
        Check(s.Length==NativeVisualStride.Clips&&s.Distinct().Count()==s.Length&&s.All(c=>c!=null),"the grounded stride names a clip for each of the 8 runs, the 8 walks and the sprint");
        Check(s[0]==NativeGroundedStride.RunAhead&&s[1]==NativeGroundedStride.RunAheadRight&&s[7]==NativeGroundedStride.RunAheadLeft&&s[NativeVisualStride.Sprint]==NativeGroundedStride.Sprint,"mocap ahead and on the diagonals ahead, a sprint of its own (not the run at twice its pace)");
        var rifle0=NativeVisualMotion.For("rifle");for(int i=2;i<=6;i++)Check(s[i]==rifle0.Locomotion[i],"way "+i+": the gun set's own low run (side, back)");
        for(int i=NativeVisualStride.Walks;i<NativeVisualStride.Sprint;i++)Check(s[i]==rifle0.Locomotion[i],"walk "+(i-NativeVisualStride.Walks)+": the gun set's own walk");
        foreach(var spec in Covered)
        {
            var set=NativeVisualMotion.For(spec);
            Check(set.Grounded==NativeGroundedStride.Stride&&set.Locomotion.Length==NativeVisualStride.Clips&&set.Locomotion!=set.Grounded,spec+": the grounded stride, its own kept beside it");
            foreach(var c in set.Grounded)Check(Array.IndexOf(set.All,c)>=0,spec+": the bank's list carries "+c);
            Check(NativeGroundedStride.Covers(set.Spec),spec+": its set is one the stride covers");
            foreach(var c in set.Grounded)Check(NativeVisualMotion.Loops(spec).Contains(c),spec+": "+c+" plays as a loop");
            book.Stride=true;book.Carry=true;
            Check(NativeVisualMotion.Stride(set,book)==set.Grounded&&NativeVisualMotion.Grounds(set,book),spec+": with the bank's clips it strides on the grounded stride");
            book.Stride=false;
            Check(NativeVisualMotion.Stride(set,book)==set.Locomotion&&!NativeVisualMotion.Grounds(set,book),spec+": without them, its own stride (the builder before the bank lands)");
            book.Stride=true;
        }
        // The casters carry the staff over it (the mage's run mirrored into the staff's left fist); without the carry clip, their own stride.
        var staff=NativeVisualMotion.For("fire");
        Check(staff.RunUpper==NativeGroundedStride.StaffCarry&&NativeGroundedStride.StaffCarry.EndsWith(NativeVisualMotion.Mirror,StringComparison.Ordinal)&&Array.IndexOf(staff.All,staff.RunUpper)>=0&&NativeVisualMotion.Loops("fire").Contains(staff.RunUpper)&&staff.LeftShaft,"the casters: the staff carried steady over the stride (mirrored into the left fist), listed and looped");
        book.Carry=false;Check(NativeVisualMotion.Stride(staff,book)==staff.Locomotion,"a caster without the carry clip keeps its own stride whole");
        Check(NativeVisualMotion.Stride(NativeVisualMotion.For("rifle"),book)==NativeGroundedStride.Stride,"the guns need no carry clip (the gun pack carries their upper body)");book.Carry=true;
        foreach(var spec in new[]{"pistol","rifle","machinegun"})Check(NativeVisualMotion.For(spec).RunUpper==null,spec+": the guns' upper body over the stride is the Tactical pack's carry");
        var bomb=NativeVisualMotion.For(NativeBombs.Spec);
        Check(bomb.RunUpper==NativeGroundedStride.FistsCarry&&bomb.RunUpper==NativeVisualMotion.For("emperor").Locomotion[0]&&NativeVisualMotion.Stride(bomb,book)==NativeGroundedStride.Stride,"the Bombadier keeps its own pumping fists (its unarmed run's upper body) over the stride");
        // The pack guns' sets are copies of their own: they keep the stride, and their upper body stays the pack's carry over it.
        foreach(var (spec,gun) in new[]{("pistol",NativeTacticalMotion.Pistol),("rifle",NativeTacticalMotion.Rifle),("machinegun",NativeTacticalMotion.MachineGun)})
        {
            var t=NativeVisualMotion.Tactical(spec,NativeVisualMotion.For(spec),book);
            Check(t.Tactical==gun&&t.Grounded==NativeGroundedStride.Stride&&NativeVisualMotion.Stride(t,book)==NativeGroundedStride.Stride&&t.Grounded.All(c=>Array.IndexOf(t.All,c)>=0),spec+": the pack gun strides on it too, its carry over the legs");
        }
        // Melee ("think melee is fine?"), the bow, the fighters, the Top Dog and the DEV classes: their own strides and loops as before.
        foreach(var spec in Kept)
        {
            var set=NativeVisualMotion.For(spec);
            Check(set.Grounded==null&&set.RunUpper==null&&NativeVisualMotion.Stride(set,book)==set.Locomotion&&set.Locomotion.All(c=>Array.IndexOf(NativeGroundedStride.Added,c)<0)&&set.All.All(c=>Array.IndexOf(NativeGroundedStride.Added,c)<0),spec+": its own stride, untouched");
        }
        Check(NativeVisualMotion.Loops("sword").Length==0&&NativeVisualMotion.Loops("greatsword").Length==0,"other sets' loops unchanged");
        // Their match idles, jumps, falls, landings and reactions are their own sets' (owner 2026-11-05, match-idles-keep).
        foreach(var spec in Covered){var set=NativeVisualMotion.For(spec);Check(Array.IndexOf(NativeGroundedStride.Clips,set.Idle)<0&&Array.IndexOf(NativeGroundedStride.Clips,set.Jump)<0&&Array.IndexOf(NativeGroundedStride.Clips,set.Land)<0,spec+": its match idle, jump and landing unchanged");}
        // The intros' motion is their own (owner): none of the stride's clips is one an intro bank holds (Slipstream's runner clips).
        foreach(var c in new[]{"Mvm_Dash","Mvm_Boost","Mvm_PowerDash_Front","Jmp_FrontAir_A_Start","Jump_Down_A_Loop","Esc_Slide_All"})Check(Array.IndexOf(NativeGroundedStride.Clips,c)<0,"not an intro's clip: "+c);
    }
    // The stride's own travel (m/s, x right and z ahead: the bake's root-motion average speed on the body, which its planted feet match:
    // measured on the posed records, docs/RUN-REALISTIC.md) at the rules' run (6.8 m/s) and sprint (x1.35) in each way: the rate the game
    // plays them at stays within NativeVisualStride's RateMin..RateMax, so the feet are never left behind or dragged.
    // The stride's travel in the bank (x, z m/s, NativeVisualStride's order; the two added Frank runs' set to their planted feet's, the
    // rest as released): docs/RUN-REALISTIC.md "Bank".
    private static readonly float[] TravelX={0,3.042f,3.726f,2.579f,0,-2.613f,-3.722f,-2.964f,0,1.489f,2.369f,1.489f,0,-1.489f,-2.369f,-1.489f,0};
    private static readonly float[] TravelZ={6.0f,3.111f,0,-2.585f,-3.722f,-2.633f,0,3.111f,2.369f,1.489f,0,-1.489f,-2.356f,-1.488f,-.104f,1.489f,6.28f};
    private static void Travel()
    {
        float run=Rules.Speed,sprint=Rules.Speed*Rules.SprintMultiplier;var w=new float[NativeVisualStride.Clips];
        var sx=new float[NativeVisualStride.Clips];var sz=new float[NativeVisualStride.Clips];
        for(int i=0;i<NativeVisualStride.Clips;i++){sx[i]=TravelX[i];sz[i]=TravelZ[i];}
        for(int d=0;d<NativeVisualStride.Directions;d++)
        {
            float a=d*(float)Math.PI/4,vx=(float)Math.Sin(a)*run,vz=(float)Math.Cos(a)*run;
            float rate=NativeVisualStride.Weights(vx,vz,sx,sz,w);
            float along=0;for(int i=0;i<w.Length;i++)along+=w[i]*(sx[i]*vx+sz[i]*vz)/run;
            Check(rate>NativeVisualStride.RateMin&&rate<NativeVisualStride.RateMax&&Math.Abs(rate*along-run)<.05f,"way "+d+" at the run: the clips' travel times the rate is the body's (rate "+rate.ToString("0.00")+")");
        }
        float sr=NativeVisualStride.Weights(0,sprint,sx,sz,w);float sa=0;for(int i=0;i<w.Length;i++)sa+=w[i]*sz[i];
        Check(w[NativeVisualStride.Sprint]>.99f&&sr>NativeVisualStride.RateMin&&sr<NativeVisualStride.RateMax&&Math.Abs(sr*sa-sprint)<.05f,"the sprint: its own clip, the feet planted at the rules' sprint (rate "+sr.ToString("0.00")+")");
        // The old guns' sprint was the run at twice its pace and still short of the rules' sprint (its feet slid): measured 0.30 m/s.
        float oldRate=Math.Min(NativeVisualStride.RateMax,sprint/4.296f);Check(oldRate*4.296f<sprint-.5f,"(the old guns' sprint ran short of the rules' sprint)");
    }
    private static void Sources(string root)
    {
        string core=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeGroundedStride.cs")),visual=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeVisualMotion.cs"));
        string motion=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeVisualMotionPresentation.cs")).Replace("\r\n","\n");
        // Presentation only: nothing here writes a rule, a speed or the pilot.
        foreach(var x in new[]{"Rules.Speed=","SprintMultiplier=","a.Sprinting=","a.Velocity=",".MoveVelocity",".Stats[","Body.position","linearVelocity"})Check(!core.Contains(x)&&!motion.Contains(x),"nothing written: "+x);
        Check(motion.Contains("var stride=NativeVisualMotion.Stride(set,bank);")&&motion.Contains("m.Loco[k]=m.Index.TryGetValue(stride[k],out int i)?i:-1;"),"a body strides on the grounded stride only when the bank carries it");
        int carry=motion.IndexOf("VisualTacticalCarry(m,pick,time,carryWeight);VisualRunUpper(m,runUpper);",StringComparison.Ordinal),upper=motion.IndexOf("System.Array.Clear(m.UpperMix,0,m.UpperMix.Length);",StringComparison.Ordinal);
        Check(carry>0&&upper>carry,"the staff carry goes on before the upper layer (a cast lays over it)");
        Check(motion.Contains("float runUpper=m.RunUpper>=0&&covered?loco:0;bool keepLegs=upper||carryWeight>0||runUpper>0;"),"the stride's legs and hips kept under the carry");
        Check(motion.Contains("m.RunUpperShare=e;m.CarryShare=Mathf.Max(m.CarryShare,e);")&&motion.Contains("NativeRunArm.RunShare(m.LocoWeights)*loco*wgt*(1-m.Upper)*(1-m.CarryShare)"),"the carry holds the arms (the free arm's run lowering stands down under it)");
        Check(motion.Contains("Mathf.Repeat(m.Phase+m.RunUpperOffset,1)")&&motion.Contains("m.RunUpperOffset=Mathf.Repeat(NativeVisualMotionClips.StridePhase(m.Clips[m.RunUpper],lk,rk)-first,1);"),"the carry steps with the stride (its own stride's offset onto the first run's)");
        Check(visual.Contains("public static string[] Stride(Set s,IClips clips)=>Grounds(s,clips)?s.Grounded:s.Locomotion;"),"the stride's choice");
        // The bank and the pack clips are local (LocalAssets is git-ignored): none in the repository.
        var assets=Path.Combine(root,"Assets");
        Check(!Directory.EnumerateFiles(assets,"CLazy@*",SearchOption.AllDirectories).Any(f=>!f.Replace('\\','/').Contains("/LocalAssets/")),"no Runner Action clip outside LocalAssets");
        Check(!Directory.EnumerateFiles(assets,"Frank_RPG_Mage*",SearchOption.AllDirectories).Any(f=>!f.Replace('\\','/').Contains("/LocalAssets/")),"no Frank clip outside LocalAssets");
    }
}
#endif
