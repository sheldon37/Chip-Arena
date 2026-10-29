using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;
// The zombies on the player's model (owner 2026-10-29: "Implement the attached zombie designs ... using the existing low-poly player
// model as the starting point"; docs/ZOMBIE-MODELS.md): the variant table (Core NativeZombieLooks), its pick, the shape names, the
// shader's zombie look and the runtime's wiring into the horde's draw, death, corpses and wounds. The model itself, its captures and
// the crowd's cost are the owner's PC's (Unity and the licensed bake); these hold the data and the code paths.
internal static class ZombieModelChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie models: "+what);count++;}
    private static readonly int[] Kinds={NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Spitter,NativeHordeKinds.Brute,NativeHordeKinds.Hulk};
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int Picks(){int sink=0;for(int i=0;i<4000;i++){var v=NativeZombieLooks.Of(Kinds[i%Kinds.Length],10000+i);sink+=v.Female?1:0;}return sink;}
    public static void Run(string root)
    {
        Table();Pick();Shapes();Sac();Decay();Mutation();Sources(root);
        Console.WriteLine("PASS zombie models: "+count+" checks (20 variants over five kinds, two male and two female each; wardrobe fallbacks; silhouettes by build; the pick by id; the shape names; the shader's zombie look; the horde's draw, death, corpses and wounds on the model; presentation only)");
    }

    // ---- the variant table ----
    private static void Table()
    {
        var all=NativeZombieLooks.All;
        Check(all.Length==Kinds.Length*NativeZombieLooks.Variants&&all.Select(v=>v.Id).Distinct().Count()==all.Length,"twenty variants, each its own id");
        foreach(int kind in Kinds)
        {
            var mine=all.Where(v=>v.Kind==kind).ToArray();string name=NativeHordeKinds.Ids[kind];
            Check(mine.Length==4&&!mine[0].Female&&!mine[1].Female&&mine[2].Female&&mine[3].Female,name+": male A, male B, female A, female B");
            Check(mine.All(v=>v.Id.StartsWith(name+"-",StringComparison.Ordinal)),name+": ids named by kind");
            Check(mine.All(v=>v.Sac==(kind==NativeHordeKinds.Spitter)),name+": the throat sac is the spitter's alone");
            Check(mine.Where(v=>v.Female).All(f=>mine.Where(v=>!v.Female).All(m=>f.Shapes[0]<m.Shapes[0])),name+": each female body type under each male's (the pack's male/female shape)");
        }
        foreach(var v in all)
        {
            Check(v.Pieces.Length==NativeLook.Slots.Length&&v.Colours.Length==NativeLook.Slots.Length,v.Id+": a piece and a colour per slot");
            for(int s=0;s<v.Pieces.Length;s++)
            {
                var c=NativeZombieLooks.Candidates(v.Pieces[s]);string last=c.Last();
                Check(c.Length>0,v.Id+": a candidate in slot "+NativeLook.Slots[s]);
                Check(last==NativeZombieLooks.None&&(s==0||s==1&&v.Mutant)||NativeLook.Ids[s].Contains(last)&&!NativeLook.DevOnly(last),v.Id+": slot "+NativeLook.Slots[s]+" falls back on one of today's pieces (every bake dresses it): "+last);
                foreach(var id in c.Take(c.Length-1))Check(id.StartsWith(NativeZombieLooks.Own,StringComparison.Ordinal)&&!NativeLook.Ids.Any(ids=>ids.Contains(id)),v.Id+": a preferred piece is the zombies' own, never a Locker id: "+id);
                Check(v.Colours[s]==-1||v.Colours[s]>=0&&v.Colours[s]<=0xffffff,v.Id+": colour in range");
            }
            Check(v.Skin>=0&&v.Skin<=0xffffff&&v.Eyes>=0&&v.Eyes<=0xffffff,v.Id+": skin and eyes in range");
            Check(v.Shapes.Length==NativeZombieLooks.ShapeNames.Length&&v.Shapes.Take(NativeZombieLooks.BodyShapes).All(w=>!float.IsNaN(w))&&v.Shapes.All(w=>float.IsNaN(w)||w>=-100&&w<=100),v.Id+": every body shape set, weights in -100..100");
            var b=v.Build;
            // (A mutant, the Hulk's, swells past a person's: its limbs to twice, its head small; its right arm grown past the left.)
            if(v.Mutant)Check(v.Kind==NativeHordeKinds.Hulk&&new[]{b.Arm,b.Forearm,b.Thigh,b.Calf,b.Hand}.All(g=>g>=1.2f&&g<=2.1f)&&b.Head>=.8f&&b.Head<.9f&&b.Shoulder>=1.2f&&b.Shoulder<=1.5f&&b.Right>1&&b.Right<=1.2f&&b.Neck>=.4f&&b.Neck<=.8f&&b.Reach>1&&b.Reach<=1.35f,v.Id+": a mutant's build, swollen within the rig's limits (the head sunk, the arms long)");
            else Check(new[]{b.Arm,b.Forearm,b.Thigh,b.Calf,b.Hand}.All(g=>g>=.8f&&g<=1.6f)&&b.Head>=.9f&&b.Head<=1.1f&&b.Shoulder>=.9f&&b.Shoulder<=1.2f&&b.Right==1&&b.Neck==1&&b.Reach==1,v.Id+": the build within the rig's limits");
            Check(v.Grime>=0&&v.Grime<=1&&v.Tears>=0&&v.Tears<=.62f&&v.Rot>=0&&v.Rot<=1,v.Id+": wear within 0..1 (torn, not shredded: the owner's \"torn clothes\", 2026-10-29)");
            // Pale skin and blank eyes: every zombie's skin is greyed (low saturation), its eyes near white.
            float Sat(int rgb){float r=(rgb>>16&255)/255f,g=(rgb>>8&255)/255f,bl=(rgb&255)/255f,hi=Math.Max(r,Math.Max(g,bl)),lo=Math.Min(r,Math.Min(g,bl));return hi>0?(hi-lo)/hi:0;}
            Check(Sat(v.Skin)<.22f&&Sat(v.Eyes)<.1f&&(v.Eyes>>16&255)>200,v.Id+": greyed skin, blank eyes");
        }
        // Silhouettes by build, not colour: the kinds' means apart, heavy ones heavier, the spitter the thinnest.
        float[] Sig(int kind){var m=all.Where(v=>v.Kind==kind).ToArray();float Mean(Func<NativeZombieLooks.Variant,float> f)=>m.Average(f);
            return new[]{NativeHordeKinds.Scale[kind],Mean(v=>v.Build.Arm),Mean(v=>v.Build.Forearm),Mean(v=>v.Build.Hand),Mean(v=>v.Build.Thigh),Mean(v=>v.Shapes[2]/100),Mean(v=>v.Shapes[1]/100),m[0].Sac?1:0};}
        float Arms(int kind)=>all.Where(v=>v.Kind==kind).Average(v=>v.Build.Arm+v.Build.Forearm);
        Check(Arms(NativeHordeKinds.Hulk)>Arms(NativeHordeKinds.Brute)&&Arms(NativeHordeKinds.Brute)>Arms(NativeHordeKinds.Shambler)&&Arms(NativeHordeKinds.Shambler)>Arms(NativeHordeKinds.Runner)&&Arms(NativeHordeKinds.Runner)>Arms(NativeHordeKinds.Spitter),"arm bulk: hulk > brute > shambler > runner > spitter");
        Check(all.Where(v=>v.Kind==NativeHordeKinds.Hulk||v.Kind==NativeHordeKinds.Brute).All(v=>v.Shapes[2]>=50&&v.Shapes[2]+v.Shapes[1]>=120)&&all.Where(v=>v.Kind==NativeHordeKinds.Spitter).All(v=>v.Shapes[1]<=5&&v.Shapes[2]==0),"the heavy kinds muscled or heavy-set (the brute's big-bellied male B), the spitter gaunt");
        for(int i=0;i<Kinds.Length;i++)for(int j=i+1;j<Kinds.Length;j++)
        {var a=Sig(Kinds[i]);var c=Sig(Kinds[j]);float d=(float)Math.Sqrt(a.Zip(c,(x,y)=>(x-y)*(x-y)).Sum());Check(d>=.2f,"silhouettes apart: "+NativeHordeKinds.Ids[Kinds[i]]+" and "+NativeHordeKinds.Ids[Kinds[j]]+" "+d.ToString("0.00"));}
    }

    // ---- the pick: by id, stable, every variant and both sexes worn ----
    private static void Pick()
    {
        foreach(int kind in Kinds)
        {
            var seen=new int[4];int women=0;var mine=NativeZombieLooks.All.Where(v=>v.Kind==kind).ToList();
            for(int id=10000;id<14000;id++){var v=NativeZombieLooks.Of(kind,id);Check(v!=null&&v.Kind==kind&&ReferenceEquals(v,NativeZombieLooks.Of(kind,id)),"a variant of its own kind, the same each time");seen[mine.IndexOf(v)]++;if(v.Female)women++;}
            Check(seen.All(n=>n>=800&&n<=1200),NativeHordeKinds.Ids[kind]+": each variant worn by about a quarter ("+string.Join(",",seen)+")");
            Check(women>=1800&&women<=2200,NativeHordeKinds.Ids[kind]+": about half female");
        }
        Check(NativeZombieLooks.Of(NativeHordeKinds.Crawler,1)==null&&NativeZombieLooks.Of(NativeHordeKinds.Bloater,1)==null&&NativeZombieLooks.Of(NativeHordeKinds.Count,1)==null,"kinds without variants keep their voxel model");
        bool was=NativeZombieLooks.Drafts;
        NativeZombieLooks.Drafts=false;
        Check(Kinds.All(NativeZombieLooks.Modelled)&&!NativeZombieLooks.Modelled(NativeHordeKinds.Crawler)&&!NativeZombieLooks.Modelled(NativeHordeKinds.Bloater),"shipped: all five kinds on the model after the PC captures (the shambler first, then the reviewed drafts); never the crawler or bloater");
        NativeZombieLooks.Drafts=true;
        Check(Kinds.All(NativeZombieLooks.Modelled)&&!NativeZombieLooks.Modelled(NativeHordeKinds.Crawler)&&!NativeZombieLooks.Modelled(NativeHordeKinds.Bloater)&&!NativeZombieLooks.Modelled(NativeHordeKinds.Count)&&!NativeZombieLooks.Modelled(-1),"drafts: all five kinds; never the crawler, bloater or a zone kind");
        NativeZombieLooks.Drafts=was;
        Picks();long before=GC.GetAllocatedBytesForCurrentThread();int sink=Picks();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(sink>0&&bytes==0,"picking a variant allocates nothing ("+bytes+" B over 4000)");
    }

    // ---- the shape names the variants set on the pack's meshes ----
    private static void Shapes()
    {
        Check(NativeZombieLooks.ShapeNames.Take(NativeZombieLooks.BodyShapes).SequenceEqual(new[]{"BodyType","Weight","Muscle","Belly","Chest","NeckThickness","Curvy","ButtSize","WaistSize"}),"the pack's nine body shapes first (docs/BOZO-INTEGRATION.md)");
        foreach(var (name,want,ok) in new[]{("Shape_Muscle","Muscle",true),("Body.Shape_BodyType","BodyType",true),("muscle","Muscle",true),("Muscle","Muscle",true),("BSMC Weight","Weight",true),
            ("Tag_Muscle","Muscle",false),("Expression_Weight","Weight",false),("ShapeMuscle","Muscle",false),("Shape_MuscleX","Muscle",false),("NeckThickness","Thickness",false),("","Muscle",false),(null,"Muscle",false)})
            Check(NativeZombieLooks.ShapeMatches(name,want)==ok,"shape name "+(name??"null")+" is "+want+": "+ok);
    }

    // ---- the spitter's throat sac (Core NativeZombieSac) ----
    // ---- the Hulk's mutation (Core NativeZombieMutation): grown flesh, spurs and pustules over its build ----
    private static void Mutation()
    {
        Check(NativeZombieLooks.All.Where(v=>v.Kind==NativeHordeKinds.Hulk).All(v=>v.Mutant)&&NativeZombieLooks.All.Where(v=>v.Kind!=NativeHordeKinds.Hulk).All(v=>!v.Mutant),"every Hulk a mutant, no other kind");
        Check(NativeZombieLooks.All.Where(v=>v.Kind==NativeHordeKinds.Hulk&&!v.Female).All(v=>v.Pieces[1]==NativeZombieLooks.None),"the male Hulks bare-chested (the grown torso shows)");
        Check(NativeZombieLooks.All.Where(v=>v.Kind==NativeHordeKinds.Hulk).Average(v=>v.Build.Arm+v.Build.Forearm)>NativeZombieLooks.All.Where(v=>v.Kind==NativeHordeKinds.Brute).Average(v=>v.Build.Arm+v.Build.Forearm)+.6f,"the Hulk's arms far past the brute's");
        // On a mannequin's rest joints (root space, +z front, the left at -x as the bake has it).
        var j=new System.Collections.Generic.Dictionary<string,System.Numerics.Vector3>{
            {"spine_03",new(0,1.16f,.043f)},{"spine_04",new(0,1.25f,.038f)},{"spine_05",new(0,1.42f,0)},{"neck_01",new(0,1.5f,-.01f)},{"head",new(0,1.6f,-.005f)},
            {"upperarm_l",new(-.13f,1.45f,-.03f)},{"upperarm_r",new(.13f,1.45f,-.03f)},{"lowerarm_l",new(-.29f,1.24f,-.03f)},{"lowerarm_r",new(.29f,1.24f,-.03f)},
            {"hand_l",new(-.43f,1.09f,.06f)},{"hand_r",new(.43f,1.09f,.06f)}};
        System.Numerics.Vector3? Rest(string n)=>j.TryGetValue(n,out var q)?q:(System.Numerics.Vector3?)null;
        foreach(var m in NativeZombieMutation.Masses)
        {
            Check(NativeZombieMutation.Place(m,Rest,out var c,out var x,out var y,out var z,out var radii)&&radii.X>0&&radii.Y>0&&radii.Z>0&&Math.Abs(x.Length()-1)<1e-4f&&Math.Abs(y.Length()-1)<1e-4f&&z.Z>0,m.Name+": placed, its frame facing the front");
            if(m.Name.EndsWith("-r",StringComparison.Ordinal))Check(c.X>.05f,m.Name+": on the right");if(m.Name.EndsWith("-l",StringComparison.Ordinal))Check(c.X<-.05f,m.Name+": on the left");
            if(m.Limb){var axis=System.Numerics.Vector3.Normalize(j[m.From]-j[m.To]);Check(Math.Abs(radii.Y-m.Radii.Y*(j[m.From]-j[m.To]).Length())<1e-4f,m.Name+": as long as its share of the limb");Check(Math.Abs(System.Numerics.Vector3.Dot(axis,y))>.999f&&Math.Abs(System.Numerics.Vector3.Dot(axis,x))<1e-3f,m.Name+": along its limb");}
            Check(j.ContainsKey(m.Bone),m.Name+": rigid on a joint of the model");
        }
        NativeZombieMutation.Place(NativeZombieMutation.Find("mound"),Rest,out var mound,out _,out _,out _,out _);Check(mound.Z<-.1f&&mound.Y>j["spine_05"].Y,"the mound behind the upper back");
        var g=NativeZombieMutation.Build(Rest,0x8e8295,1234u);Check(g!=null,"the mutation builds on the mannequin");
        int n=g.Positions.Count;var watch=System.Diagnostics.Stopwatch.StartNew();NativeZombieMutation.Build(Rest,0x8e8295,77u);double ms=watch.Elapsed.TotalMilliseconds;
        Check(n>2000&&n<16000&&g.Normals.Count==n&&g.Colors.Count==n&&g.Bone.Count==n&&g.Bone2.Count==n&&g.Weight2.Count==n&&g.Weight2.All(w=>w>=0&&w<=.5f)&&g.Indices.Count%3==0&&g.Indices.All(i=>i>=0&&i<n),"every vertex a normal, a colour and its bones ("+n+" vertices, "+g.Indices.Count/3+" faces, built in "+ms.ToString("0")+" ms)");
        Console.WriteLine("  mutation: "+n+" vertices, "+g.Indices.Count/3+" faces, built in "+ms.ToString("0")+" ms");
        Check(g.Normals.All(v=>Math.Abs(v.Length()-1)<1e-3f)&&g.Bone.All(b=>b>=0&&b<g.Bones.Count)&&g.Bone2.All(b=>b>=0&&b<g.Bones.Count)&&g.Bones.All(b=>j.ContainsKey(b)),"unit normals, each vertex on one of its bones");
        Check(g.Colors.All(c=>c.X>=0&&c.X<=1&&c.Y>=0&&c.Y<=1&&c.Z>=0&&c.Z<=1&&(c.W==0||c.W==1))&&g.Colors.Count(c=>c.W==1)>=NativeZombieMutation.Pustules.Length*20,"colours in range; only the pustules glow");
        float top=g.Positions.Max(p=>p.Y);Check(top>j["neck_01"].Y+.05f&&top<j["head"].Y+.25f,"the grown shoulders rise round the neck ("+top.ToString("0.00")+" m), not over the head");
        Check(g.Positions.Max(p=>p.X)>j["upperarm_r"].X+.12f&&g.Positions.Max(p=>p.X)>-g.Positions.Min(p=>p.X),"the right shoulder grown past the left");
        // Faces out: each face's normal points away from its part (the toon culls the back), most vertices' normals agree.
        int agree=0;for(int i=0;i<g.Indices.Count;i+=3){int a=g.Indices[i],b=g.Indices[i+1],c=g.Indices[i+2];var f=System.Numerics.Vector3.Cross(g.Positions[b]-g.Positions[a],g.Positions[c]-g.Positions[a]);if(System.Numerics.Vector3.Dot(f,g.Normals[a]+g.Normals[b]+g.Normals[c])>0)agree++;}
        Check(agree==g.Indices.Count/3,"every face wound to its normals ("+agree+" of "+g.Indices.Count/3+")");
        // The forearms club toward the fists: wider at the wrist end than at the elbow, and raw there.
        foreach(var side in new[]{"l","r"})
        {
            var fm=NativeZombieMutation.Find("forearm-"+side);Check(fm.Taper>.2f&&fm.Raw>.5f&&NativeZombieMutation.Lump(new System.Numerics.Vector3(1,-.6f,0)/1.1662f,fm,0)>NativeZombieMutation.Lump(new System.Numerics.Vector3(1,.6f,0)/1.1662f,fm,0)*1.2f,"forearm-"+side+": a club, wider toward the fist");
        }
        Check(NativeZombieMutation.Masses.Where(m=>!m.Limb).All(m=>m.Lumps<=.08f),"the trunk's masses smooth muscle, not bubbles");
        var again=NativeZombieMutation.Build(Rest,0x8e8295,1234u);Check(again.Positions.Count==n&&again.Positions.Zip(g.Positions,(a,b)=>(a-b).Length()).Max()==0,"the same seed, the same growth");
        var other=NativeZombieMutation.Build(Rest,0x8e8295,99u);Check(other.Positions.Zip(g.Positions,(a,b)=>(a-b).Length()).Max()>.005f,"another seed lumps differently");
        Check(NativeZombieMutation.Build(x=>x=="upperarm_r"?null:Rest(x),0x8e8295,1u)==null,"no growth without its joints");
    }

    private static void Sac()
    {
        var neck=new System.Numerics.Vector3(0,1.45f,0);var head=new System.Numerics.Vector3(0,1.55f,.01f);var fwd=new System.Numerics.Vector3(0,0,1);
        var s=NativeZombieSac.Build(neck,head,fwd,0x8f9a86);
        Check(s!=null&&s.Positions.Count==2+(NativeZombieSac.Rings-1)*NativeZombieSac.Sides&&s.Indices.Count%3==0&&s.Indices.All(i=>i>=0&&i<s.Positions.Count),"the sac builds: a closed ellipsoid of rings");
        Check(s.Positions.Count==s.Normals.Count&&s.Positions.Count==s.Swell.Count&&s.Positions.Count==s.Colors.Count&&s.Positions.Count==s.Head.Count,"every vertex has a normal, a swell, a colour and a head share");
        var c=s.Positions.Aggregate(System.Numerics.Vector3.Zero,(a,p)=>a+p)/s.Positions.Count;
        Check(c.Z>.02f&&c.Y>neck.Y&&c.Y<head.Y,"ahead of the neck, between neck_01 and the head (under the jaw)");
        float back=s.Positions.Min(p=>p.Z),front=s.Positions.Max(p=>p.Z);Check(front-c.Z>(c.Z-back)*1.3f,"its back half flattened into the neck");
        var swollen=s.Positions.Select((p,i)=>p+s.Swell[i]).ToList();Check(swollen.Max(p=>p.Z)>front+.03f&&swollen.Max(p=>p.X)>s.Positions.Max(p=>p.X)+.015f,"swollen, it grows forward and wide");
        Check(s.Head.All(h=>h>=0&&h<=.76f)&&s.Head[0]>.5f&&s.Head[s.Head.Count-1]<.05f,"its top rides the head, its bottom the neck");
        Check(Math.Abs(s.Colors[0].X-0x8f/255f)<.02f&&s.Colors[s.Colors.Count-1].Y>s.Colors[s.Colors.Count-1].Z,"its top blends into the skin, its tip a paler acid green");
        int outward=0,tris=s.Indices.Count/3;for(int t=0;t<tris;t++){var a=s.Positions[s.Indices[t*3]];var b=s.Positions[s.Indices[t*3+1]];var d=s.Positions[s.Indices[t*3+2]];if(System.Numerics.Vector3.Dot(System.Numerics.Vector3.Cross(b-a,d-a),(a+b+d)/3-c)>0)outward++;}
        Check(outward==tris,"every face turned out ("+outward+" of "+tris+")");
        Check(NativeZombieSac.Glow(0)==NativeZombieSac.GlowRest&&NativeZombieSac.Glow(1)==NativeZombieSac.GlowSwollen&&NativeZombieSac.Glow(2)==NativeZombieSac.GlowSwollen,"its glow rises with the swell");
        Check(NativeZombieSac.Build(neck,neck,fwd,0)==null&&NativeZombieSac.Build(neck,head,head-neck,0)==null,"no sac without a neck line or a front");
        Check(NativeZombieLooks.All.Where(v=>v.Sac).All(v=>v.Kind==NativeHordeKinds.Spitter)&&NativeZombieLooks.All.Count(v=>v.Sac)==4,"every spitter wears one, nothing else");
    }

    // ---- the undead look: decay openings onto flesh and bone (Core NativeZombieDecay), wear ----
    private static void Decay()
    {
        foreach(var v in NativeZombieLooks.All)
        {
            Check(v.Decay!=null&&v.Decay.Length>=2&&v.Decay.Length<=NativeZombieDecay.Max&&v.Decay.All(n=>NativeZombieDecay.Find(n)!=null)&&v.Decay.Distinct().Count()==v.Decay.Length,v.Id+": two to four decay openings, each a known site");
            Check(v.Tears>=.35f&&v.Tears<=.62f&&v.Rot>=.35f&&v.Rot<=.75f&&v.Blood>=.4f&&v.Blood<=.6f&&v.Grime>0,v.Id+": torn, rotting and bloodied");
        }
        Check(NativeZombieDecay.All.Select(x=>x.Name).Distinct().Count()==NativeZombieDecay.All.Length&&NativeZombieDecay.All.All(x=>x.Radius>.02f&&x.Radius<.08f&&x.Depth>0&&x.Depth<.06f),"every site named once, a few centimetres wide");
        foreach(var x in NativeZombieDecay.All.Where(x=>x.Name.EndsWith("-l",StringComparison.Ordinal)))Check(NativeZombieDecay.Find(x.Name.Substring(0,x.Name.Length-2)+"-r") is NativeZombieDecay.Site r&&r.Radius==x.Radius&&r.Bone==x.Bone.Replace("_l","_r")&&r.Limb==x.Limb,x.Name+": its right twin");
        // On a mannequin's rest joints (root space, +z front, the left at -x as the bake has it).
        var j=new System.Collections.Generic.Dictionary<string,System.Numerics.Vector3>{
            {"spine_02",new(0,1.10f,.035f)},{"spine_03",new(0,1.16f,.043f)},{"spine_04",new(0,1.25f,.038f)},{"spine_05",new(0,1.42f,0)},{"head",new(0,1.6f,-.005f)},
            {"clavicle_l",new(-.01f,1.48f,-.02f)},{"clavicle_r",new(.01f,1.48f,-.02f)},{"upperarm_l",new(-.13f,1.45f,-.03f)},{"upperarm_r",new(.13f,1.45f,-.03f)},
            {"lowerarm_l",new(-.29f,1.24f,-.03f)},{"lowerarm_r",new(.29f,1.24f,-.03f)},{"hand_l",new(-.43f,1.09f,.06f)},{"hand_r",new(.43f,1.09f,.06f)},
            {"thigh_l",new(-.09f,.95f,0)},{"thigh_r",new(.09f,.95f,0)},{"calf_l",new(-.1f,.5f,.01f)},{"calf_r",new(.1f,.5f,.01f)},{"foot_l",new(-.1f,.08f,-.02f)},{"foot_r",new(.1f,.08f,-.02f)}};
        System.Numerics.Vector3? Rest(string n)=>j.TryGetValue(n,out var p)?p:(System.Numerics.Vector3?)null;
        foreach(var x in NativeZombieDecay.All)
        {
            Check(NativeZombieDecay.Place(x,Rest,out var at,out var dir)&&Math.Abs(dir.Length()-1)<1e-4f,x.Name+": placed on the rest joints");
            if(x.Name.EndsWith("-l",StringComparison.Ordinal))Check(dir.X<-.1f,x.Name+": faces out of the left side");
            if(x.Name.EndsWith("-r",StringComparison.Ordinal))Check(dir.X>.1f,x.Name+": faces out of the right side");
            if(x.Limb){var axis=System.Numerics.Vector3.Normalize(j[x.To]-j[x.From]);Check(Math.Abs(System.Numerics.Vector3.Dot(axis,dir))<1e-3f,x.Name+": square to its limb");}
        }
        NativeZombieDecay.Place(NativeZombieDecay.Find("ribs-l"),Rest,out var ribs,out _);Check(ribs.Y>1.25f&&ribs.Y<1.3f,"the ribs' openings at the ribcage, not the waist");
        NativeZombieDecay.Place(NativeZombieDecay.Find("back"),Rest,out _,out var back);Check(back.Z<-.99f,"the back's opening faces behind");
        Check(!NativeZombieDecay.Place(NativeZombieDecay.Find("shin-l"),n=>n=="foot_l"?null:Rest(n),out _,out _)&&!NativeZombieDecay.Place(null,Rest,out _,out _),"no opening without its joints");
        var shin=NativeZombieDecay.Find("shin-l");var ribsSite=NativeZombieDecay.Find("ribs-l");
        Check(NativeZombieDecay.DepthOf(shin,.05f)==.05f+shin.Depth&&NativeZombieDecay.DepthOf(ribsSite,.15f)==ribsSite.Depth,"a limb's opening reaches past its bone, a trunk's its own depth");
        float lo=9,hi=0;for(int a=0;a<720;a++){float r=NativeZombieDecay.Rag(a*MathF.PI/360,.37f);lo=Math.Min(lo,r);hi=Math.Max(hi,r);}
        Check(lo>.55f&&hi<1.09f&&hi-lo>.2f,"a ragged rim within its pit's 1.12 radius ("+lo.ToString("F2")+" to "+hi.ToString("F2")+")");
    }

    // ---- the shader and the runtime ----
    private static void Sources(string root)
    {
        string A(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
        string fx=A("Resources/NativeVisualBodyFx.cginc"),toon=A("Resources/NativeVisualBodyToon.shader"),zr=A("Runtime/NativeZombieModelPresentation.cs"),kp=A("Runtime/NativeHordeKindPresentation.cs"),
            gp=A("Runtime/NativeHordeGorePresentation.cs"),dp=A("Runtime/NativeDeathPresentation.cs"),core=A("Core/NativeZombieLooks.cs");
        string look=fx.Substring(fx.IndexOf("float3 ZombieLook(float3 c,float2 uv)",StringComparison.Ordinal));look=look.Substring(0,look.IndexOf("\n}\n",StringComparison.Ordinal));
        Check(look.Contains("if(_ZombieEye.w>0.5)return _ZombieEye.rgb;")&&look.Contains("if(_ZombieTint.w<=0.0&&_ZombieWear.x+_ZombieWear.y+_ZombieWear.z<=0.0)return c;"),"the zombie look: blank eyes, and nothing changed with it unset (every pilot)");
        Check(Regex.Matches(toon,@"_Zombie(Tint|Wear|Eye|Skin)\(""[^""]*"",Vector\)=\(0,0,0,0\)").Count==4,"its four properties off by default");
        Check(toon.Contains("if(_VertexAlbedo<=0){o.Albedo=ZombieLook(o.Albedo,i.uv_Texture2D);")&&toon.Contains("albedo=ZombieLook(albedo,i.uv);"),"the surface and its outline take the zombie look (never a cut's cap)");
        Check(look.Contains("edge=1.0-0.45*_ZombieWear.y")&&look.Contains("ZombieNoise(uv*30.0+s*1.7)*0.7+ZombieNoise(uv*90.0+s.yx)*0.3"),"torn patches on the part's own texture coordinates (they stay on the cloth), a few at the table's tears");
        // The horde: the model in place of the voxel model on the full tier; the voxel draw kept as the fallback.
        Check(kp.Contains("if(!ZombieModelPose(rig,m.Id,kind,s.Tier,planted,airborne,a.Crawl,s.Gait,a.Lost,0,flash,glow.x,swell)){HordeQueue(rig,NativeHordeModels.Id(kind,s.Look,s.Tier),m.Id,a.Lost,0,swell,glow,flash);HordeQueueJaw(rig,kind,s.Look,s.Tier,s.Jaw,glow,flash);}"),"a living add: its model, else its voxel model and jaw");
        Check(kp.Contains("bool planted=(mode==0||mode==4||mode==5)&&!a.Crawl,airborne=mode==1||mode==3;"),"feet planted on the gait, a door's wake and a landing; airborne in a dive and a jump-down's fall");
        Check(kp.Contains("if(burn||!ZombieModelDeath(rig,death.id,d.Kind,death.lost,d.Slice)){HordeDeathBody(rig,model,death.id,death.lost,fx,hordeQueueVisible,burn);"),"a death: its model falls, else the voxel body");
        Check(kp.Contains("ClearHordeGore();ClearZombieModels();"),"cleared with the horde");
        Check(gp.Contains("if(ZombieCorpseDrawn(r,sink,alpha))continue;")&&gp.Contains("ZombieCorpsesKept();")&&gp.Contains("if(ZombieModelDrawn(id))return;"),"corpses from the baked model, sinking with the voxel timeline; no voxel-fitted wounds on the model");
        Check(dp.Contains("ZombieModelsShown();")&&dp.IndexOf("ZombieModelsShown();",StringComparison.Ordinal)>dp.IndexOf("finally{DiagnosticDeathWoundMs",StringComparison.Ordinal),"bodies posed this frame show, the rest hide, after the deaths");
        Check(zr.Contains("tier==NativeHordeModels.Full&&NativeZombieLooks.Modelled(kind)&&ZombieModelsOn")&&zr.Contains("VoxelZombiesArgument=\"--ca-voxel-zombies\"")&&zr.Contains("!ClassicBodies&&VisualBodiesOn"),"the model only on the full tier, for modelled kinds, with the bake, not Classic, not --ca-voxel-zombies");
        int build=zr.IndexOf("ZombieBuild(skeleton,v.Build);",StringComparison.Ordinal),binding=zr.IndexOf("Binding=new NativeVisualRigBinding(root.transform,skeleton)",StringComparison.Ordinal);
        Check(build>0&&binding>build,"the build set before the binding reads the rest pose");
        Check(zr.Contains("if(t.childCount==0&&n.IndexOf(\"twist\",StringComparison.OrdinalIgnoreCase)>=0)"),"girth only on the twist leaves (no child moves)");
        Check(zr.Contains("z.Body.Binding.Apply(pose,default(NativeVisualRig.Grasp),planted,airborne);"),"posed from the gait's own pose, no weapon grasp");
        Check(zr.Contains("if(zombieMakes>=ZombieMakesPerFrame||zombiePool.Count==0)return null;")&&zr.Contains("ZombieMakesPerFrame=3,ZombieSparesPerVariant=6")&&zr.Contains("zombieFill=InstantiateAsync(visualBodyPrefab,ZombiePoolBatch,zombieHolder.transform);")&&!zr.Contains("VisualCorpseInstance()")&&!zr.Contains("Instantiate(visualBodyPrefab)"),"a few new bodies a frame from their own pool, made ahead off the main thread (never at once, never the pilots' corpse pool); spares kept per variant");
        Check(kp.Contains("if(!HordeKindsReady)return;ZombieModelsPrewarm(arena);"),"the pool made ahead while a horde is about");
        Check(zr.Contains("Graphics.DrawMesh(mesh,m,mats[sub],0,null,sub,c.Blocks[i],false,true)"),"a corpse drawn from its baked meshes, no shadow");
        Check(zr.Contains("var sac=v.Sac?ZombieSacMake(root,skeleton,v):null;")&&zr.IndexOf("var sac=v.Sac?ZombieSacMake(root,skeleton,v):null;",StringComparison.Ordinal)>zr.IndexOf("ZombieBuild(skeleton,v.Build);",StringComparison.Ordinal),"a spitter's sac made on its built rest");
        Check(zr.Contains("ZombieCollapse(z,lost,hidden);ZombieFlash(z,flash,glow);ZombieSacSwell(z,swell);")&&zr.Contains("z.Sac.SetBlendShapeWeight(0,swell*100);")&&zr.Contains("ZombieModelBind(z,rig,false,false,lost,hidden,1,0,0);"),"the sac swells with the gait's swell, none in a death");
        Check(zr.Contains("ws[i]=new BoneWeight{boneIndex0=0,weight0=1-sac.Head[i],boneIndex1=1,weight1=sac.Head[i]};")&&zr.Contains("renderer.bones=new[]{neck,head};"),"skinned on neck_01 and the head");
        Check(zr.Contains("zombieBlock.SetFloat(UseOutlineId,0);r.SetPropertyBlock(zombieBlock);continue;}"),"blank eyes drawn without an outline (a map's tinted line reads as an iris)");
        // The undead look: the decay openings cut in the meshes' rest space, with pits and bones; the wear's blood and gashes.
        Check(fx.Contains("void DecayClip(float3 rest)")&&toon.Contains("clip(i.cut);DecayClip(i.rest);WoundClip(i.worldPos);")&&toon.Contains("clip(i.cut);DecayClip(i.rest);WoundClip(i.world);")&&Regex.Matches(toon,@"o\.rest=v\.texcoord3\.xyz;").Count==2,"both passes cut the openings in the rest space");
        Check(look.Contains("if(_ZombieSkin.w>0.0)")&&look.Contains("float gash=")&&look.Contains("float3 under="),"dried blood in the cloth, gashes on the skin, raw flesh through the tears");
        Check(zr.Contains("if(v.Decay!=null&&v.Decay.Length>0)body.All=ZombieDecayMake(root,skeleton,v,body.All);")&&zr.Contains("s.sharedMesh.SetUVs(3,rest);")&&zr.Contains("var pit=VisualHolePit(all,bone,h);")&&zr.Contains("var go=new GameObject(\"Bones.Decay\");"),"each opening's rest written once, its pit and its bones made");
        Check(zr.Contains("private static bool ZombieDecayCut(Renderer r)=>r!=null&&!ZombieEyePart(r)&&!r.name.StartsWith(\"Teeth.\",StringComparison.Ordinal)&&r.name!=\"Sac.Spitter\"&&r.name!=\"Growth.Hulk\";"),"eyes, teeth, the sac and a mutant's growth never cut");
        Check(zr.Contains("var growth=v.Mutant?ZombieGrowthMake(root,skeleton,v):null;")&&zr.Contains("ws[i]=new BoneWeight{boneIndex0=g.Bone[i],weight0=1-g.Weight2[i],boneIndex1=g.Bone2[i],weight1=g.Weight2[i]};")&&zr.Contains("float h=n==\"hand_r\"?b.Hand*b.Right:b.Hand;"),"a mutant's growth made on its rest after the build, rigid on its bones; its right arm grown");
        // Flying parts from the model, not the voxel parts; their meshes pooled.
        Check(kp.Contains("p.Pieces=ZombiePartPieces(id,joint,p.Pieces);")&&kp.Contains("if(!ZombiePartDrawn(p,age,sink))sourceModels.Queue(p.Model,")&&kp.Contains("p.Live=false;ZombiePiecesFree(p.Pieces);continue;"),"a modelled zombie's severed, sliced and gibbed parts are its own model's");
        Check(zr.Contains("if((mine[t[i]]?1:0)+(mine[t[i+1]]?1:0)+(mine[t[i+2]]?1:0)<2)continue;")&&zr.Contains("skin.BakeMesh(baked,true);zombieBakedNow[skin]=baked;"),"a part keeps the triangles skinned mostly to its bones, each renderer baked once a frame");
        Check(zr.Contains("if(c.Meshes[i]!=null&&c.Baked[i])zombieCorpseMeshes.Push(c.Meshes[i]);"),"a corpse's pits drawn from their own meshes, never pooled for bakes");
        foreach(var ban in new[]{".Hp=","StrikeAt","LastAttack=","Struck","Damage(","NativeNetwork","PveAddStats",".radius=",".height="})Check(!zr.Contains(ban)&&!core.Contains(ban),"presentation only: never "+ban);
        Check(!core.Contains("UnityEngine"),"the table is Core (no Unity types)");
    }
}
