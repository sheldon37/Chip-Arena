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
    // ---- the Hulk's mutation (Core NativeZombieMutation): its upper body's every muscle, veins, torn skin, spurs and pustules ----
    private static void Mutation()
    {
        Check(NativeZombieLooks.All.Where(v=>v.Kind==NativeHordeKinds.Hulk).All(v=>v.Mutant)&&NativeZombieLooks.All.Where(v=>v.Kind!=NativeHordeKinds.Hulk).All(v=>!v.Mutant),"every Hulk a mutant, no other kind");
        Check(NativeZombieLooks.All.Where(v=>v.Kind==NativeHordeKinds.Hulk).All(v=>v.Pieces[1]==NativeZombieLooks.None),"no Hulk wears a top: the grown flesh is its upper body (owner 2026-10-30: clothes showed inside the muscle)");
        Check(NativeZombieLooks.All.Where(v=>v.Kind==NativeHordeKinds.Hulk&&v.Female).All(v=>v.Colours[1]>=0),"a woman Hulk's top colour is her chest wrap");
        var under=new[]{"ribs-l","ribs-r","back","forearm-l","forearm-r","upperarm-l","upperarm-r"};
        Check(NativeZombieLooks.All.Where(v=>v.Mutant).All(v=>v.Decay.Length>0&&v.Decay.All(d=>!under.Contains(d))),"a Hulk's decay opens where no flesh grows (the face, the legs)");
        Check(NativeZombieLooks.All.Where(v=>v.Kind==NativeHordeKinds.Hulk).Average(v=>v.Build.Arm+v.Build.Forearm)>NativeZombieLooks.All.Where(v=>v.Kind==NativeHordeKinds.Brute).Average(v=>v.Build.Arm+v.Build.Forearm)+.6f,"the Hulk's arms far past the brute's");
        // The male Hulk's rest joints as Unity measured them after its build (root space, +z front, the left at -x).
        var j=new System.Collections.Generic.Dictionary<string,System.Numerics.Vector3>{
            {"pelvis",new(0f,1.022f,0.024f)},{"spine_01",new(0f,1.047f,0.022f)},{"spine_02",new(0f,1.11f,0.036f)},{"spine_03",new(0f,1.171f,0.043f)},{"spine_04",new(0f,1.259f,0.038f)},
            {"spine_05",new(0f,1.43f,0.001f)},{"neck_01",new(0f,1.488f,-0.009f)},{"neck_02",new(0f,1.512f,-0.006f)},{"head",new(0f,1.537f,-0.002f)},
            {"clavicle_l",new(-0.009f,1.485f,-0.021f)},{"clavicle_r",new(0.009f,1.485f,-0.021f)},{"upperarm_l",new(-0.184f,1.45f,-0.03f)},{"upperarm_r",new(0.184f,1.45f,-0.03f)},
            {"lowerarm_l",new(-0.39f,1.177f,-0.033f)},{"lowerarm_r",new(0.39f,1.177f,-0.033f)},{"hand_l",new(-0.567f,0.975f,0.08f)},{"hand_r",new(0.567f,0.975f,0.08f)},
            {"lowerarm_twist_01_l",new(-0.498f,1.054f,0.036f)},{"lowerarm_twist_02_l",new(-0.437f,1.124f,-0.003f)},{"lowerarm_twist_01_r",new(0.498f,1.054f,0.036f)},{"lowerarm_twist_02_r",new(0.437f,1.124f,-0.003f)}};
        System.Numerics.Vector3? Rest(string n)=>j.TryGetValue(n,out var q)?q:(System.Numerics.Vector3?)null;
        // Anatomy: every muscle placed (the left twins mirrored), its frame square.
        var placed=NativeZombieMutation.Place(Rest,1.08f,new NativeZombieMutation.Growth());Check(placed!=null,"the muscles place on the model's rest joints");
        int muscles=placed.Count(p=>!p.Core);Check(muscles==NativeZombieMutation.Muscles.Sum(m=>m.Side?2:1)&&muscles>=55,"every muscle placed, both sides ("+muscles+")");
        foreach(var p in placed.Where(p=>!p.Core))
        {
            Check(Math.Abs(p.U.Length()-1)<1e-4f&&Math.Abs(p.Out.Length()-1)<1e-4f&&Math.Abs(System.Numerics.Vector3.Dot(p.U,p.Out))<1e-3f&&p.Width>0&&p.Thick>0,p.Name+": its frame square, its sizes set");
            if(p.Name.EndsWith("-r",StringComparison.Ordinal)){var twin=placed.First(q=>q.Name==p.Name.Substring(0,p.Name.Length-2)+"-l");Check(Math.Abs((p.A.X+p.B.X)+(twin.A.X+twin.B.X))<1e-4f&&Math.Abs(p.A.Y-twin.A.Y)<1e-4f,p.Name+": its left twin mirrored");}
        }
        Check(new[]{"trap-upper","pec-mid","delt-front","delt-side","delt-back","lat","biceps","triceps-long","triceps-lat","flexors","extensors","abs-1","abs-4","serratus-1","oblique"}.All(n=>placed.Any(p=>p.Name.StartsWith(n,StringComparison.Ordinal))),"the anatomy's named masses (traps, pecs, three deltoid heads, lats, biceps, both triceps heads, the forearm's, the eight-pack, serratus, obliques)");
        Check(placed.First(p=>p.Name=="biceps-r").Thick>placed.First(p=>p.Name=="biceps-l").Thick*1.05f,"the right arm grown past the left");
        var g=NativeZombieMutation.Build(Rest,0xb7a7a2,1234u,1.08f);Check(g!=null,"the mutation builds on the male Hulk's rest");
        int n=g.Positions.Count;var watch=System.Diagnostics.Stopwatch.StartNew();NativeZombieMutation.Build(Rest,0xb7a7a2,77u,1.08f);double ms=watch.Elapsed.TotalMilliseconds;
        Check(n>15000&&n<45000&&g.Normals.Count==n&&g.Colors.Count==n&&new[]{g.Bone,g.Bone2,g.Bone3,g.Bone4}.All(l=>l.Count==n)&&new[]{g.Weight,g.Weight2,g.Weight3,g.Weight4}.All(l=>l.Count==n)&&g.Indices.Count%3==0&&g.Indices.All(i=>i>=0&&i<n),"every vertex a normal, a colour and four bones ("+n+" vertices, "+g.Indices.Count/3+" faces, built in "+ms.ToString("0")+" ms)");
        Console.WriteLine("  mutation: "+n+" vertices, "+g.Indices.Count/3+" faces, built in "+ms.ToString("0")+" ms");
        Check(g.Normals.All(v=>Math.Abs(v.Length()-1)<1e-3f)&&g.Bones.All(b=>j.ContainsKey(b)),"unit normals, its bones the model's");
        bool weighted=true;for(int i=0;i<n;i++){float w0=g.Weight[i],w1=g.Weight2[i],w2=g.Weight3[i],w3=g.Weight4[i];if(Math.Abs(w0+w1+w2+w3-1)>1e-3f||w0<w1-1e-6f||w1<w2-1e-6f||w2<w3-1e-6f||w3<0)weighted=false;}
        Check(weighted,"four weights a vertex, largest first, summing to 1");
        Check(g.Colors.All(c=>c.X>=0&&c.X<=1&&c.Y>=0&&c.Y<=1&&c.Z>=0&&c.Z<=1&&(c.W==0||c.W==1))&&g.Colors.Count(c=>c.W==1)>=NativeZombieMutation.Pustules.Length*40,"colours in range; only the pustules glow");
        // Not bubbles: the surface dips between muscles. Down the belly's middle (the linea alba) the flesh lies behind the
        // eight-pack's blocks, and under the chest's edge behind the pecs.
        float Front(float x,float y){float z=.4f;while(z>-.2f&&g.Distance(new System.Numerics.Vector3(x,y,z))>0)z-=.001f;return z;}
        var s4=j["spine_04"];float alba=Front(0,s4.Y+.0f),block=Front(.041f,s4.Y-.02f);
        Check(block-alba>=.004f,"the eight-pack stands off the linea alba ("+((block-alba)*1000).ToString("0")+" mm)");
        var pecCentre=placed.First(p=>p.Name=="pec-mid-r");var pecAt=(pecCentre.A+pecCentre.B)*.5f;float pec=Front(pecAt.X,pecAt.Y),sternum=Front(0,pecAt.Y);
        Check(pec-sternum>=.008f,"the pecs stand off the sternum ("+((pec-sternum)*1000).ToString("0")+" mm)");
        // Smooth skinning: the weights change little across an edge (by distance alone; a muscle's own bones tore the creases).
        var jumps=new System.Collections.Generic.List<float>();
        float[] W(int i){var w=new float[g.Bones.Count];w[g.Bone[i]]+=g.Weight[i];w[g.Bone2[i]]+=g.Weight2[i];w[g.Bone3[i]]+=g.Weight3[i];w[g.Bone4[i]]+=g.Weight4[i];return w;}
        for(int i=0;i<g.Indices.Count;i+=3){var wa=W(g.Indices[i]);var wb=W(g.Indices[i+1]);float d=0;for(int k=0;k<wa.Length;k++)d+=Math.Abs(wa[k]-wb[k]);jumps.Add(d);}
        jumps.Sort();float p99=jumps[(int)(jumps.Count*.99)];Check(p99<=.4f&&jumps[jumps.Count-1]<1.2f,"skin weights change smoothly across the flesh (p99 "+p99.ToString("0.00")+", max "+jumps[jumps.Count-1].ToString("0.00")+")");
        Check(g.Bones.Contains("lowerarm_twist_01_r")&&g.Bones.Contains("clavicle_l"),"the forearms ride their twist bones, the shoulders their collarbones");
        // Faces out (the toon culls the back): every face wound to its vertices' normals.
        int agree=0;for(int i=0;i<g.Indices.Count;i+=3){int a=g.Indices[i],b=g.Indices[i+1],c=g.Indices[i+2];var f=System.Numerics.Vector3.Cross(g.Positions[b]-g.Positions[a],g.Positions[c]-g.Positions[a]);if(System.Numerics.Vector3.Dot(f,g.Normals[a]+g.Normals[b]+g.Normals[c])>0)agree++;}
        Check(agree==g.Indices.Count/3,"every face wound to its normals ("+agree+" of "+g.Indices.Count/3+")");
        // The head blends: the flesh meets it under the jaw and behind the skull, never over the face.
        var head=j["head"];
        // (The face lies 8 to 12 cm before the head joint from the chin up; the flesh tucked under the jaw, inside the head's own
        // neck, stays nearer.)
        Check(!g.Positions.Any(p=>p.Y>head.Y+.015f&&p.Z>head.Z+.06f&&Math.Abs(p.X)<.07f),"no flesh over the face (the chin and jaw clear)");
        // (The flesh's top: its field, the spurs' tips left out.)
        float crest=0;for(float x=-.3f;x<=.3f;x+=.01f)for(float z=-.25f;z<=.15f;z+=.01f)for(float y=head.Y+.2f;y>head.Y-.1f;y-=.005f)if(g.Distance(new System.Numerics.Vector3(x,y,z))<0){crest=Math.Max(crest,y);break;}
        Check(crest>j["neck_02"].Y+.03f&&crest<head.Y+.1f,"the traps rise round the sunk head ("+crest.ToString("0.00")+" m), not over it");
        Check(g.Positions.Max(p=>p.X)>-g.Positions.Min(p=>p.X),"the right shoulder grown past the left");
        // What it covers: the worn skin over the chest and the arms (and past the flesh by its reach), never the hands or the face.
        Check(g.Covers(new System.Numerics.Vector3(0,s4.Y+.05f,.12f),-.004f)&&g.Covers(new System.Numerics.Vector3(.28f,1.32f,-.03f),-.004f),"the flesh covers the chest and the upper arm");
        Check(!g.Covers(j["hand_r"]+new System.Numerics.Vector3(.02f,-.03f,.02f),.06f)&&!g.Covers(head+new System.Numerics.Vector3(0,.06f,.09f),.06f),"never the hand past the wrist nor the face");
        // The women's wrap: the variant's top colour round the chest, nothing over it.
        var wrap=NativeZombieMutation.Build(Rest,0xb3a4a3,5u,1.06f,0x2b3552);var cloth=new System.Numerics.Vector4(0x2b/255f,0x35/255f,0x52/255f,0);
        int wrapped=wrap.Colors.Count(c=>Math.Abs(c.Z/Math.Max(c.X,1e-3f)-cloth.Z/cloth.X)<.15f&&c.Z>.15f);
        Check(wrapped>1500&&g.Colors.Count(c=>Math.Abs(c.Z/Math.Max(c.X,1e-3f)-cloth.Z/cloth.X)<.15f&&c.Z>.15f)<100,"a woman's chest wrapped in her top's colour ("+wrapped+" vertices), a man's bare");
        Check(wrap.Colors.Count(c=>c.W==1)<g.Colors.Count(c=>c.W==1),"no pustule over a wrap");
        // The tone meets the head's skin (warmer than the colour alone).
        Check(NativeZombieMutation.SkinMatch.X>NativeZombieMutation.SkinMatch.Z,"the flesh warmed to the skin texture's tone");
        var again=NativeZombieMutation.Build(Rest,0xb7a7a2,1234u,1.08f);Check(again.Positions.Count==n&&again.Positions.Zip(g.Positions,(a,b)=>(a-b).Length()).Max()==0&&again.Colors.Zip(g.Colors,(a,b)=>(a-b).Length()).Max()==0,"the same seed, the same growth");
        var other=NativeZombieMutation.Build(Rest,0xb7a7a2,99u,1.08f);Check(other.Colors.Zip(g.Colors,(a,b)=>(a-b).Length()).Max()>.02f,"another seed mottles differently");
        Check(NativeZombieMutation.Build(x=>x=="upperarm_r"?null:Rest(x),0xb7a7a2,1u)==null,"no growth without its joints");
        // The fists: the clench rolls each finger past the handle's grip into the palm.
        for(int f=1;f<=13;f++){if(f>3&&f<11)continue;Check(Math.Abs(NativeVisualRig.Curl(f,1,0)-NativeVisualRig.Curl(f,1))<1e-6f&&NativeVisualRig.Curl(f,1,1)>NativeVisualRig.Curl(f,1)+.15f&&NativeVisualRig.Curl(f,0,1)==NativeVisualRig.Curl(f,0),"finger joint "+f+": a clench closes it past the grip, an open hand stays open");}
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
        Check(zr.Contains("z.Body.Binding.Apply(pose,z.Kind==NativeHordeKinds.Hulk?new NativeVisualRig.Grasp{RightGrip=1,LeftGrip=1,Clench=ZombieClench+(1-ZombieClench)*z.Busy}:default(NativeVisualRig.Grasp),planted,airborne);")&&!zr.Contains("Right=NativeVisualRig."),"posed from the gait's own pose, no weapon grasp (the Hulk's fists closed on nothing)");
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
        Check(zr.Contains("if(v.Mutant)ZombieGrowthStart(root,skeleton,v,body.All);")&&zr.Contains("e.Build=System.Threading.Tasks.Task.Run(()=>NativeZombieMutation.Build(")&&zr.Contains("boneIndex3=g.Bone4[i],weight3=g.Weight4[i]")&&zr.Contains("float h=n==\"hand_r\"?b.Hand*b.Right:b.Hand;"),"a mutant's growth built off the main thread on its rest after the build, on four bones; its right arm grown");
        Check(zr.Contains("if(z.Growing&&!ZombieGrowthAttach(z)){z.SeenAt=Time.unscaledTime;return false;}")&&zr.Contains("if(covered[t[i]]&&covered[t[i+1]]&&covered[t[i+2]]){dropped++;any=true;continue;}")&&zr.Contains("covered[i]=g.Covers("),"a growing body draws voxel until its growth is on; the worn triangles the flesh covers are dropped");
        Check(zr.Contains("z.Kind==NativeHordeKinds.Hulk?new NativeVisualRig.Grasp{RightGrip=1,LeftGrip=1,Clench=ZombieClench+(1-ZombieClench)*z.Busy}:default(NativeVisualRig.Grasp)"),"the Hulk's hands are fists, clenched hardest through a blow");
        // Flying parts from the model, not the voxel parts; their meshes pooled.
        Check(kp.Contains("p.Pieces=ZombiePartPieces(id,joint,p.Pieces);")&&kp.Contains("if(!ZombiePartDrawn(p,age,sink))sourceModels.Queue(p.Model,")&&kp.Contains("p.Live=false;ZombiePiecesFree(p.Pieces);continue;"),"a modelled zombie's severed, sliced and gibbed parts are its own model's");
        Check(zr.Contains("if((mine[t[i]]?1:0)+(mine[t[i+1]]?1:0)+(mine[t[i+2]]?1:0)<2)continue;")&&zr.Contains("skin.BakeMesh(baked,true);zombieBakedNow[skin]=baked;"),"a part keeps the triangles skinned mostly to its bones, each renderer baked once a frame");
        Check(zr.Contains("if(c.Meshes[i]!=null&&c.Baked[i])zombieCorpseMeshes.Push(c.Meshes[i]);"),"a corpse's pits drawn from their own meshes, never pooled for bakes");
        foreach(var ban in new[]{".Hp=","StrikeAt","LastAttack=","Struck","Damage(","NativeNetwork","PveAddStats",".radius=",".height="})Check(!zr.Contains(ban)&&!core.Contains(ban),"presentation only: never "+ban);
        Check(!core.Contains("UnityEngine"),"the table is Core (no Unity types)");
    }
}
