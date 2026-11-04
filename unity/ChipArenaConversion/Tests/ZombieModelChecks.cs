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
    // The Blender script that makes the gear bank (its props by id).
    private static string GearScript="";
    private static readonly int[] Kinds={NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Spitter,NativeHordeKinds.Brute,NativeHordeKinds.Hulk};
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int Picks(){int sink=0;for(int i=0;i<4000;i++){var v=NativeZombieLooks.Of(Kinds[i%Kinds.Length],10000+i);sink+=v.Female?1:0;}return sink;}
    public static void Run(string root)
    {
        GearScript=File.ReadAllText(Path.Combine(root,"tools/gear/station_gear.py"))+File.ReadAllText(Path.Combine(root,"tools/gear/backrooms_gear.py"))+File.ReadAllText(Path.Combine(root,"tools/gear/hound_gear.py"))+File.ReadAllText(Path.Combine(root,"tools/gear/ironworks_gear.py"));
        Table();Pick();Dressed(root);Shapes();Sac();Decay();Mutation();Sources(root);
        Console.WriteLine("PASS zombie models: "+count+" checks (20 variants over five kinds, two male and two female each; wardrobe fallbacks; silhouettes by build; the pick by id; the shape names; the shader's zombie look; the horde's draw, death, corpses and wounds on the model; presentation only)");
    }

    // ---- the variant table ----
    private static void Table()
    {
        var all=NativeZombieLooks.All;
        Check(all.Count(v=>v.Kind<NativeHordeKinds.Count)==Kinds.Length*NativeZombieLooks.Variants&&all.Select(v=>v.Id).Distinct().Count()==all.Length,"twenty zombie variants, every variant its own id");
        // The Central Station's kinds (docs/EXPEDITION-ENEMY-REWORK.md): four each, two male and two female, live since their review
        // (2026-11-03), each with its kit: the gear bank's props (tools/gear/station_gear.py makes every one), the Linesman's tape,
        // the Pileup's fused commuter.
        for(int local=0;local<NativeStationKinds.KindCount;local++)
        {
            int kind=NativeStationKinds.Kind(local);var mine=all.Where(v=>v.Kind==kind).ToArray();
            Check(NativeHordeZones.Of(kind)!=null&&mine.Length==NativeZombieLooks.Variants&&mine.Count(v=>v.Female)==2&&mine.All(v=>v.Id.StartsWith(NativeHordeZones.Of(kind).Id+"-",StringComparison.Ordinal)),"station kind "+local+": four variants, two male and two female");
            Check(NativeZombieLooks.ZoneLive.Contains(kind)&&NativeZombieLooks.Modelled(kind),"station kind "+local+": on the model in shipped builds after its review");
            Check(mine.All(v=>v.Gear.Length>0&&v.Gear.All(g=>GearScript.Contains("('"+g+"',")))&&mine.All(v=>v.Tape==(local==NativeStationKinds.Linesman)&&v.Fused==(local==NativeStationKinds.Pileup)),
                "station kind "+local+": its kit (gear the Blender script builds, the Linesman's tape, the Pileup's fused commuter)");
        }
        // The Backrooms' entities (docs/EXPEDITION-ENEMY-REWORK.md section 4): the four on the model live with their kit, the Hound a
        // beast on all fours (owner 2026-11-03: its own doglike skull and jaw, a starved body); the Tiler and the Smiler have no
        // variants (not people: the Smiler's apparition, owner 2026-11-03: below).
        string[] kit={"faceling","hound","drowned","lifeguard"};
        foreach(var name in kit)
        {
            int kind=NativeHordeKinds.Resolve(name);var mine=all.Where(v=>v.Kind==kind).ToArray();
            Check(NativeHordeZones.Of(kind)?.Id==name&&mine.Length==NativeZombieLooks.Variants&&mine.Count(v=>v.Female)==2&&mine.All(v=>v.Id.StartsWith(name+"-",StringComparison.Ordinal)),name+": four variants, two male and two female");
            Check(NativeZombieLooks.ZoneLive.Contains(kind),name+": on the model in shipped builds after its review");
            Check(mine.All(v=>v.Gear.All(g=>GearScript.Contains("('"+g+"',"))&&(v.Carry==null||GearScript.Contains("('"+v.Carry+"',"))),name+": its gear is the Blender scripts'");
            Check(mine.All(v=>v.Faceless==(name=="faceling")&&(v.Stripes>=0)==(name=="drowned")&&(v.Carry!=null)==(name=="lifeguard")&&(v.Upright>0)==(name!="hound")
                &&v.Beast==(name=="hound")&&(v.Ribs>0)==(name=="hound")),name+": its kit (the blank face, the stripes, the hook, its stance, the beast's head and ribs)");
        }
        // The City Ironworks' five on the model on every tier (docs/ZOMBIE-MODELS.md 4n): their variants, their kit from the Blender script.
        foreach(var name in new[]{"cinderling","stoker","slagback","ladleman","rigger"})
        {
            int kind=NativeHordeKinds.Resolve(name);var mine=all.Where(v=>v.Kind==kind).ToArray();
            Check(NativeHordeZones.Of(kind)?.Id==name&&mine.Length==NativeZombieLooks.Variants&&mine.Count(v=>v.Female)==2&&mine.All(v=>v.Id.StartsWith(name+"-",StringComparison.Ordinal)),name+": four variants, two male and two female");
            Check(NativeZombieLooks.ZoneLive.Contains(kind)&&NativeZombieLooks.Modelled(kind),name+": on the model in shipped builds after its review");
            Check(mine.All(v=>v.Gear.Length>0&&v.Gear.All(g=>GearScript.Contains("('"+g+"',"))),name+": its gear is the Blender script's");
            Check(mine.All(v=>(v.Char>0)==(name=="cinderling"||name=="slagback")&&(v.Foil>0)==(name=="stoker")&&v.Crust==(name=="slagback")),name+": its look (the char and the crust, the foil)");
        }
        Check(NativeZombieLooks.All.Where(v=>v.Kind!=NativeHordeKinds.Resolve("cinderling")&&v.Kind!=NativeHordeKinds.Resolve("slagback")).All(v=>v.Char==0)&&NativeZombieLooks.All.Where(v=>v.Kind!=NativeHordeKinds.Resolve("stoker")).All(v=>v.Foil==0),"no other kind is charred or foiled");
        Check(NativeZombieLooks.Of(NativeHordeKinds.Resolve("tiler"),1)==null,"the Tiler keeps its own body (no variants)");
        Check(!NativeZombieLooks.Has(NativeSmilerLook.Kind)&&!NativeZombieLooks.Modelled(NativeSmilerLook.Kind)&&!GearScript.Contains("'smiler-"),"the Smiler is no person: no variants, no gear (its own apparition)");
        Check(NativeZombieLooks.Kitted&&all.Where(v=>v.Kind<NativeHordeKinds.Count).All(v=>v.Gear.Length==0&&!v.Tape&&!v.Fused&&!v.Faceless&&v.Stripes<0&&v.Carry==null&&v.Upright==0&&!v.Beast&&v.Ribs==0),"the base kinds wear no kit");
        // The Hound (owner 2026-11-03, a reference, then "more monster like", "more doglike"): bare, a beast's head (the gear's skull
        // and jaw), long arms; on all fours (the gait's quadruped, its own gallop), its pounce's crouch, leap and landing on the
        // authority's .35 s and .85 s, its pin's bites on its .3 s beats, its jaw hanging and snapping.
        {
            int houndKind=NativeHordeKinds.Resolve("hound");var hv=all.Where(v=>v.Kind==houndKind).ToArray();
            Check(hv.All(v=>v.Pieces[0]==NativeZombieLooks.None&&v.Pieces[1]==NativeZombieLooks.None&&v.Pieces[2]==NativeZombieLooks.None&&v.Pieces[4]==NativeZombieLooks.None&&v.Gear.SequenceEqual(new[]{"hound-skull","hound-jaw"})&&v.Build.Reach>1.4f),
                "the Hound: bare, its skull and jaw the gear's, its arms near half again as long");
            Check(NativeBackroomsMotion.Quad(houndKind)&&!NativeBackroomsMotion.Quad(NativeHordeKinds.Runner)&&!NativeBackroomsMotion.Quad(NativeHordeKinds.Resolve("faceling")),"only the Hound runs on all fours");
            NativeHordeGait.HoundPounce(.2f,out float c0,out float a0,out float l0);NativeHordeGait.HoundPounce(.6f,out float c1,out float a1,out float l1);NativeHordeGait.HoundPounce(1.0f,out float c2,out float a2,out float l2);NativeHordeGait.HoundPounce(2.5f,out float c3,out float a3,out float l3);
            Check(c0>.5f&&a0==0&&a1==1&&c1==0&&a2==0&&l2>.9f&&c3+a3+l3==0&&NativeHordeGait.HoundCrouch==.35f&&NativeHordeGait.HoundLand==.85f,"the pounce: coiled through the crouch, in the air through the leap, landed low after contact, then gone");
            float j0=NativeBackroomsMotion.HoundJaw(1,0,float.NaN),j1=NativeBackroomsMotion.HoundJaw(1,0,.28f),j2=NativeBackroomsMotion.HoundJaw(1,0,.32f);
            Check(j0>.2f&&j0<.3f&&j1>.9f&&j2<.2f,"its jaw hangs open, wide before a bite and snapped shut on it");
            var pp=new CapturedRig.Pose();pp.World();float pin=NativeBackroomsMotion.HoundPin(pp,.3f,new System.Numerics.Vector3(0,1.1f,.9f));pp.World();
            Check(pin>.9f&&pp.WorldPosition.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))&&NativeBackroomsMotion.HoundPin(pp,NativeBackroomsMotion.PinHold+.4f,System.Numerics.Vector3.UnitZ)==0,"its pin rears onto the pilot, finite, and hands back after the hold");
        }
        // Their moves over the gait (Core NativeBackroomsMotion): each holds the pose finite and hands it back after its time.
        var pose=new CapturedRig.Pose();pose.World();
        NativeBackroomsMotion.Dormant(pose,7,1.3f);float w=NativeBackroomsMotion.Wake(pose,.1f,new System.Numerics.Vector3(1,0,.2f));NativeBackroomsMotion.HowlPose(pose,.4f);
        float wh=NativeBackroomsMotion.WhistlePose(pose,.5f),hk=NativeBackroomsMotion.HookPose(pose,.7f,new System.Numerics.Vector3(0,1.2f,4));pose.World();
        Check(w==1&&wh==1&&hk==1&&pose.WorldPosition.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z)),"the Backrooms' moves pose the rig, finite");
        Check(NativeBackroomsMotion.Wake(pose,NativeBackroomsMotion.WakeTell+NativeBackroomsMotion.WakeSettle+.01f,System.Numerics.Vector3.UnitX)==0&&NativeBackroomsMotion.WhistlePose(pose,NativeBackroomsMotion.Whistle+.7f)==0
            &&NativeBackroomsMotion.HookPose(pose,NativeBackroomsMotion.HookWindup+NativeBackroomsMotion.HookDrag+.5f,System.Numerics.Vector3.UnitZ)==0,"each move hands the pose back after its time");
        Check(NativeBackroomsMotion.Sink(true,float.NaN)==NativeBackroomsMotion.DrownedSink&&NativeBackroomsMotion.Sink(false,NativeBackroomsMotion.Surface*.5f)>0&&NativeBackroomsMotion.Sink(false,NativeBackroomsMotion.Surface+.1f)==0&&NativeBackroomsMotion.Sink(false,float.NaN)==0,
            "the Drowned sinks while submerged and rises after surfacing");
        // The Smiler's apparition (Core NativeSmilerLook; owner 2026-11-03, "the smiler shouldn't be a player model"): at rest it is
        // whole; the blink empties it by the move and gathers it again; the lunge gathers, throws its lobe and hauls it back; a
        // spawn and a death fade it in and out (the eyes first out, the grin last); the billows' noise tiles.
        uint ss=NativeHordeKinds.Seed(10077);bool Fin(NativeSmilerLook.Pose q)=>new[]{q.Presence,q.Spread,q.Rise,q.Gather,q.Surge,q.Churn,q.Bob,q.Eyes,q.Squint,q.Grin,q.Open,q.Face,q.Glow}.All(float.IsFinite);
        var rest=NativeSmilerLook.Living(ss,40,null,float.NaN,-1,float.NaN);
        Check(NativeSmilerLook.Kind>=0&&Fin(rest)&&rest.Presence==1&&rest.Face==1&&rest.Surge==0&&rest.Gather==0&&Math.Abs(rest.Bob)<.07f,"the Smiler at rest: whole, its face on, floating");
        var gone=NativeSmilerLook.Living(ss,40,"blink",NativeSmilerLook.BlinkWind,-1,float.NaN);var back=NativeSmilerLook.Living(ss,40,"blink",NativeSmilerLook.BlinkWind+NativeSmilerLook.Reform+.3f,-1,float.NaN);
        Check(Fin(gone)&&gone.Presence<.02f&&gone.Eyes<.02f&&gone.Face<.02f&&back.Presence>.99f&&back.Face>.99f&&back.Spread<.01f,"the blink: gone by the move, whole again after its reform");
        var wind=NativeSmilerLook.Living(ss,40,"lunge",NativeSmilerLook.LungeWind*.8f,-1,float.NaN);var peak=NativeSmilerLook.Living(ss,40,"lunge",NativeSmilerLook.LungeWind+.12f,-1,float.NaN);var after=NativeSmilerLook.Living(ss,40,"lunge",NativeSmilerLook.LungeWind+.85f,-1,float.NaN);
        Check(wind.Gather>.5f&&wind.Surge==0&&wind.Grin>rest.Grin&&peak.Surge>.9f&&peak.Open>.9f&&after.Surge<.01f&&after.Gather==0&&after.Open<.01f,"the lunge: gathered through the wind-up, thrown at the strike, hauled back");
        var hit=NativeSmilerLook.Living(ss,40,null,float.NaN,-1,.05f);var born=NativeSmilerLook.Living(ss,40,null,float.NaN,0,float.NaN);
        Check(hit.Churn>2&&born.Presence==0&&born.Face==0&&NativeSmilerLook.Living(ss,40,null,float.NaN,NativeSmilerLook.Appear+.5f,float.NaN).Presence==1,"a hit roils it; it appears from nothing");
        var d0=NativeSmilerLook.Dying(ss,40,0);var d1=NativeSmilerLook.Dying(ss,40,.5f);var d2=NativeSmilerLook.Dying(ss,40,NativeSmilerLook.Dissipate*.9f);
        Check(Fin(d0)&&Fin(d2)&&d0.Presence>.99f&&d1.Eyes<.01f&&d1.Face>.9f&&d2.Presence<.01f&&d2.Face<.05f&&d2.Rise>.4f&&!NativeSmilerLook.Gone(1)&&NativeSmilerLook.Gone(NativeSmilerLook.Dissipate),"its death: the eyes out first, the grin last, the smoke risen away");
        var s0=NativeSmilerLook.Sample(.13f,.71f,.42f);var s1=NativeSmilerLook.Sample(1.13f,.71f,-.58f);var tex=NativeSmilerLook.Noise(8);
        Check((s0-s1).Length()<1e-4f&&tex.Length==8*8*8*4&&Enumerable.Range(0,3).All(c=>Enumerable.Range(0,512).Min(i=>tex[i*4+c])==0&&Enumerable.Range(0,512).Max(i=>tex[i*4+c])==255)&&Enumerable.Range(0,512).All(i=>tex[i*4+3]==255),
            "the billows' noise tiles and spans each channel");
        foreach(int kind in Kinds)
        {
            var mine=all.Where(v=>v.Kind==kind).ToArray();string name=NativeHordeKinds.Ids[kind];
            Check(mine.Length==4&&!mine[0].Female&&!mine[1].Female&&mine[2].Female&&mine[3].Female,name+": male A, male B, female A, female B");
            Check(mine.All(v=>v.Id.StartsWith(name+"-",StringComparison.Ordinal)),name+": ids named by kind");
            Check(mine.All(v=>v.Sac==(kind==NativeHordeKinds.Spitter)),name+": the throat sac is the spitter's alone");
            Check(mine.Where(v=>v.Female).All(f=>mine.Where(v=>!v.Female).All(m=>f.Shapes[0]<m.Shapes[0])),name+": each female body type under each male's (the pack's male/female shape)");
        }
        // (The looks dressed for an area and the fallen pilots are held to the same rules: Dressed.)
        foreach(var v in all.Concat(NativeZombieLooks.Dressed))
        {
            Check(v.Pieces.Length==NativeLook.Slots.Length&&v.Colours.Length==NativeLook.Slots.Length,v.Id+": a piece and a colour per slot");
            for(int s=0;s<v.Pieces.Length;s++)
            {
                var c=NativeZombieLooks.Candidates(v.Pieces[s]);string last=c.Last();
                Check(c.Length>0,v.Id+": a candidate in slot "+NativeLook.Slots[s]);
                // (Bare: the hair, a mutant's torn top, a crusted Slagback's chest under its slag, and a beast's every slot but its hands: the Hound is naked, the body's own skin.)
                Check(last==NativeZombieLooks.None&&(s==0||s==1&&(v.Mutant||v.Crust)||v.Beast&&s!=3)||NativeLook.Ids[s].Contains(last)&&!NativeLook.DevOnly(last),v.Id+": slot "+NativeLook.Slots[s]+" falls back on one of today's pieces (every bake dresses it): "+last);
                foreach(var id in c.Take(c.Length-1))Check(id.StartsWith(NativeZombieLooks.Own,StringComparison.Ordinal)&&!NativeLook.Ids.Any(ids=>ids.Contains(id)),v.Id+": a preferred piece is the zombies' own, never a Locker id: "+id);
                Check(v.Colours[s]==-1||v.Colours[s]>=0&&v.Colours[s]<=0xffffff,v.Id+": colour in range");
            }
            Check(v.Skin>=0&&v.Skin<=0xffffff&&v.Eyes>=0&&v.Eyes<=0xffffff,v.Id+": skin and eyes in range");
            Check(v.Shapes.Length==NativeZombieLooks.ShapeNames.Length&&v.Shapes.Take(NativeZombieLooks.BodyShapes).All(w=>!float.IsNaN(w))&&v.Shapes.All(w=>float.IsNaN(w)||w>=-100&&w<=100),v.Id+": every body shape set, weights in -100..100");
            var b=v.Build;
            // (A mutant, the Hulk's, swells past a person's: its limbs to twice, its head small; its right arm grown past the left.)
            if(v.Mutant)Check(v.Kind==NativeHordeKinds.Hulk&&new[]{b.Arm,b.Forearm,b.Thigh,b.Calf,b.Hand}.All(g=>g>=1.2f&&g<=2.1f)&&b.Head>=.8f&&b.Head<.9f&&b.Shoulder>=1.2f&&b.Shoulder<=1.5f&&b.Right>1&&b.Right<=1.2f&&b.Neck>=.4f&&b.Neck<=.8f&&b.Reach>1&&b.Reach<=1.35f,v.Id+": a mutant's build, swollen within the rig's limits (the head sunk, the arms long)");
            // (A beast, the Hound, starves within them: thin limbs, big hands, a long neck and arms near half again as long.)
            else if(v.Beast)Check(new[]{b.Arm,b.Forearm,b.Thigh,b.Calf}.All(g=>g>=.6f&&g<=.9f)&&b.Hand>=1.1f&&b.Hand<=1.4f&&b.Head>=.9f&&b.Head<=1.1f&&b.Shoulder>=.85f&&b.Shoulder<=1&&b.Right==1&&b.Neck>=1.1f&&b.Neck<=1.4f&&b.Reach>=1.3f&&b.Reach<=1.5f,v.Id+": a beast's starved build within the rig's limits");
            else Check(new[]{b.Arm,b.Forearm,b.Thigh,b.Calf,b.Hand}.All(g=>g>=.8f&&g<=1.6f)&&b.Head>=.9f&&b.Head<=1.1f&&b.Shoulder>=.9f&&b.Shoulder<=1.2f&&b.Right==1&&b.Neck==1&&b.Reach==1,v.Id+": the build within the rig's limits");
            Check(v.Grime>=0&&v.Grime<=1&&v.Tears>=0&&v.Tears<=.62f&&v.Rot>=0&&v.Rot<=1,v.Id+": wear within 0..1 (torn, not shredded: the owner's \"torn clothes\", 2026-10-29)");
            // Pale skin and blank eyes: every zombie's skin is greyed (low saturation), its eyes near white.
            float Sat(int rgb){float r=(rgb>>16&255)/255f,g=(rgb>>8&255)/255f,bl=(rgb&255)/255f,hi=Math.Max(r,Math.Max(g,bl)),lo=Math.Min(r,Math.Min(g,bl));return hi>0?(hi-lo)/hi:0;}
            // (A zone kind's eyes are its identity light: the Linesman's blue, the Undertow's aqua.)
            Check(Sat(v.Skin)<.22f&&(v.Kind>=NativeHordeKinds.Count||Sat(v.Eyes)<.1f&&(v.Eyes>>16&255)>200),v.Id+": greyed skin, blank eyes");
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

    // ---- areas and fallen pilots (owner 2026-11-03: "lets theme the zombie clothes for the areas they are in! Like in ward zero,
    // give them lab clothes, patient clothes"; "zombies to use the player default suit ... broken up and damaged full helmet +
    // suit") ----
    private static void Dressed(string root)
    {
        Check(NativeZombieLooks.Theme("expedition-ward-zero")==NativeZombieLooks.Ward&&NativeZombieLooks.Theme("expedition-transit")==NativeZombieLooks.Station&&NativeZombieLooks.Theme("expedition-foundry")==NativeZombieLooks.Foundry
            &&NativeZombieLooks.Theme(NativeBackroomsGenerator.InstanceMap)==NativeZombieLooks.Office&&NativeZombieLooks.Theme(NativePoolroomsGenerator.InstanceMap)==NativeZombieLooks.Pool
            &&NativeZombieLooks.Theme("brickfall")==null&&NativeZombieLooks.Theme("expedition-city")==null&&NativeZombieLooks.Theme(null)==null,"each area's map dresses for it; the city and the arenas keep the everyday clothes");
        var dressed=NativeZombieLooks.Dressed;
        Check(dressed.Select(v=>v.Id).Distinct().Count()==dressed.Length&&dressed.All(v=>!NativeZombieLooks.All.Any(b=>b.Id==v.Id)),"every dressed look its own id");
        // A dressed look is its base look in other clothes: the same body, face, skin, eyes, decay and build.
        foreach(var v in dressed)
        {
            var b=NativeZombieLooks.All.Single(x=>x.Id==v.Id.Split('@')[0]);
            Check(v.Kind==b.Kind&&v.Female==b.Female&&v.Skin==b.Skin&&v.Eyes==b.Eyes&&v.Shapes==b.Shapes&&v.Build==b.Build&&v.Decay==b.Decay&&v.Sac==b.Sac&&v.Mutant==b.Mutant,v.Id+": its base look's body, face, decay and build");
            Check(v.Kind<NativeHordeKinds.Count&&v.Gear.All(g=>GearScript.Contains("('"+g+"',"))&&v.Lift>=1&&v.Lift<=3,v.Id+": a base kind; its gear the Blender scripts'; its pale pieces lifted at most threefold");
        }
        // The pick: about one in eight of the four common kinds a fallen pilot, everywhere; the rest dressed for the area.
        int[] common={NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Spitter,NativeHordeKinds.Brute};
        foreach(string theme in new string[]{null}.Concat(NativeZombieLooks.Themes))
        {
            int pilots=0,adds=0,themed=0,hulkPilots=0;
            foreach(int kind in common)for(int id=20000;id<22000;id++){var v=NativeZombieLooks.Of(kind,id,theme);adds++;if(v.SuitShown!=0)pilots++;else if(v.Theme!=null){themed++;Check(v.Theme==theme,v.Id+": dressed for its own area");}
                Check(ReferenceEquals(v,NativeZombieLooks.Of(kind,id,theme))&&v.Kind==kind,"the same look each time");}
            for(int id=20000;id<22000;id++)if(NativeZombieLooks.Of(NativeHordeKinds.Hulk,id,theme).SuitShown!=0)hulkPilots++;
            Check(pilots>adds*.09f&&pilots<adds*.15f&&hulkPilots==0,(theme??"everyday")+": about one in eight a fallen pilot ("+pilots+" of "+adds+"), never a Hulk");
            Check(theme==null?themed==0:themed>adds*.4f,(theme??"everyday")+": the area's clothes on most of the rest ("+themed+")");
        }
        // Ward Zero: patients in gowns, nurses and orderlies in scrubs, doctors in white coats with their lanyards, security in caps.
        var ward=dressed.Where(v=>v.Theme==NativeZombieLooks.Ward).ToArray();
        Check(ward.Where(v=>v.Pieces[1].StartsWith("z-gown")).All(v=>v.Lift>=2)&&ward.Count(v=>v.Pieces[1].StartsWith("z-gown"))>=7&&ward.Count(v=>v.Pieces[1].StartsWith("z-scrubs-top"))>=4&&ward.Count(v=>v.Pieces[1].StartsWith("z-suit-jacket")&&v.Gear.Contains("faceling-lanyard"))>=2
            &&ward.Count(v=>v.Pieces[0].StartsWith("z-ball-cap"))>=2&&ward.Where(v=>v.Pieces[1].StartsWith("z-gown")).All(v=>v.Pieces[4].StartsWith("z-bare")||v.Pieces[4].StartsWith("z-socks")),
            "Ward Zero: patients in gowns (barefoot or in socks), nurses in scrubs, doctors in white coats with their lanyards, security in caps");
        Check(dressed.Where(v=>v.Theme==NativeZombieLooks.Foundry).Count(v=>v.Pieces[1].StartsWith("z-coveralls"))>=4&&dressed.Any(v=>v.Theme==NativeZombieLooks.Foundry&&v.Tape&&v.Gear.Any(g=>g.Contains("hardhat")))
            &&dressed.Where(v=>v.Theme==NativeZombieLooks.Pool).All(v=>v.Mutant||v.Pieces[2].StartsWith("z-shorts")||v.Pieces[2].StartsWith("z-dolphin-shorts"))
            &&dressed.Where(v=>v.Theme==NativeZombieLooks.Station).Count(v=>v.Pieces[1].StartsWith("z-suit-jacket"))>=2,"the foundry's overalls and hard hats, the pool's swimwear, the station's suits");
        Check(dressed.Where(v=>v.Mutant).All(v=>v.Pieces[1]==NativeZombieLooks.None),"a dressed Hulk still wears no top");
        // The bespoke garments (tools/gear/garments.py; owner 2026-11-03: "May have to create the outfits"): Ward Zero's patients in
        // the gown, its doctors in the lab coat, its nurses and orderlies in the scrubs; each a garment the Blender script makes,
        // its pack piece kept as the fallback; nowhere else.
        string garmentScript=File.ReadAllText(Path.Combine(root,"tools/gear/garments.py"));
        string[] garmentIds=NativeZombieLooks.AllGarments;
        Check(garmentIds.All(g=>garmentScript.Contains("'"+g+"': make_")||garmentScript.Contains("'"+g+"': "))&&dressed.All(v=>v.Garments.All(g=>garmentIds.Contains(g))),"every garment one the Blender script makes");
        Check(ward.Where(v=>v.Pieces[1].StartsWith("z-gown")).All(v=>v.Garments.SequenceEqual(new[]{NativeZombieLooks.GownGarment}))&&ward.Where(v=>v.Pieces[1].StartsWith("z-suit-jacket")).All(v=>v.Garments.SequenceEqual(new[]{NativeZombieLooks.CoatGarment}))
            &&ward.Where(v=>v.Pieces[1].StartsWith("z-scrubs-top")).All(v=>v.Garments.SequenceEqual(new[]{NativeZombieLooks.ScrubsTop,NativeZombieLooks.ScrubsPants}))&&NativeZombieLooks.All.Where(v=>!NativeIronworksKinds.Is(v.Kind)).All(v=>v.Garments.Length==0)&&dressed.All(v=>v.GarmentColours.Length==v.Garments.Length),
            "Ward Zero's gowns, lab coats and scrubs are the bespoke garments (their pack pieces the fallback); none on the everyday looks");
        Check(dressed.Count(v=>v.Theme==NativeZombieLooks.Station&&v.Garments.Contains(NativeZombieLooks.TrenchGarment))>=3&&dressed.Count(v=>v.Theme==NativeZombieLooks.Foundry&&v.Garments.Contains(NativeZombieLooks.CoverallsGarment))>=5
            &&dressed.Count(v=>v.Theme==NativeZombieLooks.Foundry&&v.Garments.Contains(NativeZombieLooks.ApronGarment)&&v.GarmentColours[0]>=0)>=2&&dressed.Count(v=>v.Theme==NativeZombieLooks.Pool&&v.Garments.Contains(NativeZombieLooks.BathrobeGarment))>=3
            &&dressed.Where(v=>v.Theme==NativeZombieLooks.Office).All(v=>v.Garments.Length==0),
            "the station's commuters in trench coats, the foundry's coveralls and leather aprons (their own colour), the pool's bathrobes");
        {
            string gz=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeZombieGarments.cs")),toon=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeVisualBodyToon.shader"));
            string zr=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeZombieModelPresentation.cs"));
            Check(zr.Contains("var worn=ZombieDress(root,v);ZombieGarmentsMake(root,v);")&&zr.Contains("if((garmentSlots>>s&1)==0)foreach(var id in NativeZombieLooks.Candidates(v.Pieces[s]))")&&zr.Contains("ZombieGarmentCloth(z,distance);")
                &&gz.Contains("Resources.Load<TextAsset>(ZombieGarmentBank)")&&gz.Contains("ZombieShapes(r,v.Shapes);")&&gz.Contains("NativeVisualCloth.PaintMaps[\"Garment.\"+id]=paint;")&&gz.Contains("ZombieClothNear=8f")&&gz.Contains("ZombieClothMost=4")
                &&toon.Contains("if(_VertexAlbedo>0&&_ZombieTint.w>0)o.Albedo=ZombieLook(o.Albedo,i.uv_Texture2D,0,0);"),
                "the garments: their slot's pack piece left off, skinned on the model with its shapes, tint and wear, Magica Cloth on the nearest four within 8 m, the pack's pieces without the bank");
        }
        // The fallen pilots: the robot suit's garments and its plates (three torn off each, the ear modules the helmet's), no hair,
        // a smashed helmet, the shell grimy and the light dead.
        var pilotsAll=dressed.Where(v=>v.SuitShown!=0).ToArray();int all16=(1<<NativeRobotSuit.Bones.Length)-1;
        Check(pilotsAll.Length==16&&pilotsAll.All(v=>v.Pieces[0]==NativeZombieLooks.None&&v.Pieces.Skip(1).SequenceEqual(NativeRobotSuit.Under.Skip(1))&&(v.SuitShown&1<<NativeRobotSuit.Head)==0
            &&System.Numerics.BitOperations.PopCount((uint)(all16&~v.SuitShown))==4&&v.Helm>=0&&v.Helm<NativeRobotHelm.BrokenCount&&v.SuitShell>=0&&(v.SuitAccent>>16&255)<80&&v.Gear.Length==0),
            "the fallen pilots: the suit's garments and plates, three plates torn off, no hair, a smashed helmet, grimy, the light dead");
        Check(pilotsAll.Select(v=>v.Helm).Distinct().Count()==NativeRobotHelm.BrokenCount&&pilotsAll.Select(v=>v.SuitShown).Distinct().Count()==4,"every smashed helmet and every torn suit worn");
        // The smashed helmets: the whole helmet less its holes (no stray hole), the edges sooted, every light dead.
        var whole=NativeRobotHelm.Helm();int wholeTris=whole.Sum(s=>s.Indices.Count/3);
        for(int i=0;i<NativeRobotHelm.BrokenCount;i++)
        {
            var b=NativeRobotHelm.Broken(i);int tris=b.Sum(s=>s.Indices.Count/3);
            Check(tris<wholeTris*.95f&&tris>wholeTris*.6f&&b.All(s=>s.Emission.All(e=>e==System.Numerics.Vector4.Zero))&&b.SelectMany(s=>s.Colors).Count(c=>c.X+c.Y+c.Z<.35f)>b.SelectMany(s=>s.Colors).Count()*.02f,
                "smashed helmet "+i+": a hole through it ("+(wholeTris-tris)+" of "+wholeTris+" triangles gone), its edges sooted, its light dead");
        }
        // The face plate's gone on helmet 1: the eyes and mouth show (no triangle ahead of the face between the brow and the chin).
        Check(NativeRobotHelm.Broken(1).All(s=>Enumerable.Range(0,s.Indices.Count/3).All(t=>{var c=(s.Positions[s.Indices[t*3]]+s.Positions[s.Indices[t*3+1]]+s.Positions[s.Indices[t*3+2]])/3;return !(c.Z>.09f&&c.Y>.0f&&c.Y<.075f&&Math.Abs(c.X)<.045f);})),
            "smashed helmet 1: the face open from the brow to the chin");
        // The runtime: the area each frame, the dressed look by it, the suit and helmet on the model's bones with the bank's models.
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));string zm=R("NativeZombieModelPresentation.cs"),kp=R("NativeHordeKindPresentation.cs"),hp=R("NativeRobotHelmPresentation.cs");
        Check(zm.Contains("var v=NativeZombieLooks.Of(kind,id,zombieTheme);")&&kp.Contains("ZombieThemeOf(arena);ZombieModelsPrewarm(arena);")&&zm.Contains("ZombieCarry(z,t,flash);ZombieSuit(z,flash);return;}")
            &&zm.Contains("ZombieCarry(z,t,flash);ZombieSuit(z,flash);\n            DiagnosticZombiePoseMs")&&zm.Contains("sourceModels.Queue(NativeSourceModels.Suit,Matrix4x4.identity,zombieSuitBone,zombieSuitVisible,")&&hp.Contains("models[BrokenHelm+b]=HelmParts(NativeRobotHelm.Broken(b),"),
            "wired: the area each frame, the look by it, the suit's plates and the smashed helmet from the bank on the model's bones");
    }

    // ---- the pick: by id, stable, every variant and both sexes worn ----
    private static void Pick()
    {
        foreach(int kind in Kinds)
        {
            var seen=new int[4];int women=0;var mine=NativeZombieLooks.All.Where(v=>v.Kind==kind).ToList();
            for(int id=10000;id<14000;id++){var v=NativeZombieLooks.Of(kind,id);Check(v!=null&&v.Kind==kind&&ReferenceEquals(v,NativeZombieLooks.Of(kind,id)),"a variant of its own kind, the same each time");seen[mine.FindIndex(b=>b.Id==v.Id.Split('@')[0])]++;if(v.Female)women++;}
            Check(seen.All(n=>n>=800&&n<=1200),NativeHordeKinds.Ids[kind]+": each variant worn by about a quarter ("+string.Join(",",seen)+")");
            Check(women>=1800&&women<=2200,NativeHordeKinds.Ids[kind]+": about half female");
        }
        Check(NativeZombieLooks.Of(NativeHordeKinds.Crawler,1)==null&&NativeZombieLooks.Of(NativeHordeKinds.Bloater,1)==null&&NativeZombieLooks.Of(NativeHordeKinds.Resolve("tiler"),1)==null,"kinds without variants keep their voxel model");
        bool was=NativeZombieLooks.Drafts;
        NativeZombieLooks.Drafts=false;
        Check(Kinds.All(NativeZombieLooks.Modelled)&&!NativeZombieLooks.Modelled(NativeHordeKinds.Crawler)&&!NativeZombieLooks.Modelled(NativeHordeKinds.Bloater),"shipped: all five kinds on the model after the PC captures (the shambler first, then the reviewed drafts); never the crawler or bloater");
        NativeZombieLooks.Drafts=true;
        Check(Kinds.All(NativeZombieLooks.Modelled)&&!NativeZombieLooks.Modelled(NativeHordeKinds.Crawler)&&!NativeZombieLooks.Modelled(NativeHordeKinds.Bloater)&&!NativeZombieLooks.Modelled(NativeHordeKinds.Resolve("tiler"))&&!NativeZombieLooks.Modelled(-1),"drafts: all five kinds; never the crawler, bloater or a zone kind");
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
            // (The Ironworks' suits and burned kinds show few or no raw openings: the char, the crust and the leather and foil are what you see.)
            Check(v.Decay!=null&&v.Decay.Length>=(NativeIronworksKinds.Is(v.Kind)?0:2)&&v.Decay.Length<=NativeZombieDecay.Max&&v.Decay.All(n=>NativeZombieDecay.Find(n)!=null)&&v.Decay.Distinct().Count()==v.Decay.Length,v.Id+": its decay openings, each a known site");
            // (A zone kind keeps the torn-not-shredded limit; its rot and blood follow its story: the drowned rot more and bleed less. Its clothes
            // are its read, so it may be hardly torn: on the Central Station's dark coats the holes' raw red read as camouflage, seen in Unity on 2026-11-04.)
            bool zone=v.Kind>=NativeHordeKinds.Count;
            Check(v.Char>0||NativeIronworksKinds.Is(v.Kind)&&v.Grime>0||v.Tears>=(zone?.1f:.35f)&&v.Tears<=.62f&&v.Rot>=(zone?.3f:.35f)&&v.Rot<=(zone?.9f:.75f)&&v.Blood>=(zone?.1f:.4f)&&v.Blood<=(zone?.8f:.6f)&&v.Grime>0,v.Id+": torn, rotting and bloodied (burned kinds: their char, not rot)");
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
        string look=fx.Substring(fx.IndexOf("float3 ZombieLook(float3 c,float2 uv,float band,float stripe)",StringComparison.Ordinal));look=look.Substring(0,look.IndexOf("\n}\n",StringComparison.Ordinal));
        Check(look.Contains("if(_ZombieEye.w>0.5)return _ZombieEye.rgb;")&&look.Contains("if(_ZombieTint.w<=0.0&&_ZombieWear.x+_ZombieWear.y+_ZombieWear.z<=0.0)return c;"),"the zombie look: blank eyes, and nothing changed with it unset (every pilot)");
        Check(Regex.Matches(toon,@"_Zombie(Tint|Wear|Eye|Skin)\(""[^""]*"",Vector\)=\(0,0,0,0\)").Count==4,"its four properties off by default");
        Check(toon.Contains("if(_VertexAlbedo<=0){float band=ZombieBand(i.rest);o.Albedo=ZombieLook(o.Albedo,i.uv_Texture2D,band,ZombieStripe(i.rest))*ZombieLean(i.rest,i.uv_Texture2D);")&&toon.Contains("albedo=_VertexAlbedo>0?(_ZombieTint.w>0?ZombieLook(i.color.rgb,i.uv,0,0):i.color.rgb):ZombieLook(albedo,i.uv,ZombieBand(i.rest),ZombieStripe(i.rest))*ZombieLean(i.rest,i.uv);"),
            "the surface and its outline take the zombie look with its tape and a starved body's ribs (a cut's cap and the gear, untinted, keep their vertex colours; a garment, tinted, takes the look)");
        Check(fx.Contains("float ZombieLean(float3 rest,float2 uv)")&&fx.Contains("if(_ZombieRibs.x>0.0)")&&fx.Contains("if(_ZombieRibs.y>0.0)")&&toon.Contains("_ZombieRibs(\"A starved zombie's creases, ribs and spine: age, ribs, the ribs' rest heights\",Vector)=(0,0,0,0)"),
            "a starved body's creases, ribs and spine: off unless set (every pilot and every other zombie)");
        Check(fx.Contains("float ZombieBand(float3 rest)")&&fx.Contains("if(band>0.0)c=lerp(c,ZombieTapeColour(k),band);")&&fx.Contains("float3 ZombieLook(float3 c,float2 uv){return ZombieLook(c,uv,0.0,0.0);}")&&fx.Contains("if(stripe>0.0)c=lerp(c,_ZombieStripeTint.rgb*lerp(0.72,1.05,saturate(k)),stripe);"),
            "the tape: rings in the rest space, silver over the piece's colour under its wear; other callers untaped");
        Check(look.Contains("edge=1.0-0.45*_ZombieWear.y")&&look.Contains("ZombieNoise(uv*30.0+s*1.7)*0.7+ZombieNoise(uv*90.0+s.yx)*0.3"),"torn patches on the part's own texture coordinates (they stay on the cloth), a few at the table's tears");
        // The horde: the model in place of the voxel model on the full tier; the voxel draw kept as the fallback.
        Check(kp.Contains("if(!ZombieModelPose(rig,m.Id,kind,s.Tier,planted,airborne,a.Crawl,s.Gait,a.Lost,0,flash,glow.x,swell,s.Jaw)){if(!HordeNoVoxel(kind)){HordeQueue(rig,NativeHordeModels.Id(kind,s.Look,s.Tier),m.Id,a.Lost,0,swell,glow,flash);HordeQueueJaw(rig,kind,s.Look,s.Tier,s.Jaw,glow,flash);}}"),"a living add: its model (its face's jaw the voxel jaw's), else its voxel model and jaw");
        Check(kp.Contains("bool planted=(mode==0||mode==4||mode==5)&&!a.Crawl,airborne=mode==1||mode==3;"),"feet planted on the gait, a door's wake and a landing; airborne in a dive and a jump-down's fall");
        Check(kp.Contains("if(burn||!ZombieModelDeath(rig,death.id,d.Kind,death.lost,d.Slice)&&!HordeNoVoxel(d.Kind)){HordeDeathBody(rig,model,death.id,death.lost,fx,hordeQueueVisible,burn);"),"a death: its model falls, else the voxel body");
        Check(kp.Contains("ClearHordeGore();ClearZombieModels();"),"cleared with the horde");
        Check(gp.Contains("if(ZombieCorpseDrawn(r,sink,alpha)||HordeNoVoxel(r.Kind))continue;")&&gp.Contains("ZombieCorpsesKept();")&&gp.Contains("if(ZombieModelDrawn(id))return;"),"corpses from the baked model, sinking with the voxel timeline; no voxel-fitted wounds on the model");
        Check(dp.Contains("ZombieModelsShown();")&&dp.IndexOf("ZombieModelsShown();",StringComparison.Ordinal)>dp.IndexOf("finally{DiagnosticDeathWoundMs",StringComparison.Ordinal),"bodies posed this frame show, the rest hide, after the deaths");
        Check(zr.Contains("tier==NativeHordeModels.Full&&NativeZombieLooks.Modelled(kind)&&ZombieModelsOn")&&zr.Contains("VoxelZombiesArgument=\"--ca-voxel-zombies\"")&&zr.Contains("!ClassicBodies&&VisualBodiesOn"),"the model only on the full tier, for modelled kinds, with the bake, not Classic, not --ca-voxel-zombies");
        int build=zr.IndexOf("ZombieBuild(skeleton,v.Build);",StringComparison.Ordinal),binding=zr.IndexOf("Binding=new NativeVisualRigBinding(root.transform,skeleton)",StringComparison.Ordinal);
        Check(build>0&&binding>build,"the build set before the binding reads the rest pose");
        Check(zr.Contains("if(t.childCount==0&&n.IndexOf(\"twist\",StringComparison.OrdinalIgnoreCase)>=0)"),"girth only on the twist leaves (no child moves)");
        Check(zr.Contains("z.Body.Binding.Apply(pose,ZombieGrasp(z),planted,airborne);")&&!zr.Contains("Right=NativeVisualRig."),"posed from the gait's own pose, no weapon grasp (the Hulk's fists closed on nothing, a station kind's hands on its prop)");
        Check(zr.Contains("if(zombieMakes>=ZombieMakesPerFrame&&!NativeStationKinds.Is(kind)||zombiePool.Count==0)return null;")&&zr.Contains("ZombieMakesPerFrame=3,ZombieSparesPerVariant=6")&&zr.Contains("zombieFill=InstantiateAsync(visualBodyPrefab,ZombiePoolBatch,zombieHolder.transform);")&&!zr.Contains("VisualCorpseInstance()")&&!zr.Contains("Instantiate(visualBodyPrefab)"),"a few new bodies a frame from their own pool, made ahead off the main thread (never at once, never the pilots' corpse pool); spares kept per variant");
        Check(kp.Contains("if(!HordeKindsReady)return;ZombieThemeOf(arena);ZombieModelsPrewarm(arena);"),"the pool made ahead while a horde is about (its area known first)");
        Check(zr.Contains("Graphics.DrawMesh(mesh,m,mats[sub],0,null,sub,c.Blocks[i],false,true)"),"a corpse drawn from its baked meshes, no shadow");
        Check(zr.Contains("var sac=v.Sac?ZombieSacMake(root,skeleton,v):null;")&&zr.IndexOf("var sac=v.Sac?ZombieSacMake(root,skeleton,v):null;",StringComparison.Ordinal)>zr.IndexOf("ZombieBuild(skeleton,v.Build);",StringComparison.Ordinal),"a spitter's sac made on its built rest");
        Check(zr.Contains("ZombieCollapse(z,lost,hidden);ZombieBigHead(z);ZombieFlash(z,flash,glow);ZombieSacSwell(z,swell);")&&zr.Contains("z.Sac.SetBlendShapeWeight(0,swell*100);")&&zr.Contains("ZombieModelBind(z,rig,false,false,lost,hidden,1,0,0);"),"the sac swells with the gait's swell, none in a death");
        Check(zr.Contains("ws[i]=new BoneWeight{boneIndex0=0,weight0=1-sac.Head[i],boneIndex1=1,weight1=sac.Head[i]};")&&zr.Contains("renderer.bones=new[]{neck,head};"),"skinned on neck_01 and the head");
        Check(zr.Contains("zombieBlock.SetFloat(UseOutlineId,0);r.SetPropertyBlock(zombieBlock);continue;}"),"blank eyes drawn without an outline (a map's tinted line reads as an iris)");
        // The undead look: the decay openings cut in the meshes' rest space, with pits and bones; the wear's blood and gashes.
        Check(fx.Contains("void DecayClip(float3 rest)")&&toon.Contains("clip(i.cut);DecayClip(i.rest);WoundClip(i.worldPos);")&&toon.Contains("clip(i.cut);DecayClip(i.rest);WoundClip(i.world);")&&Regex.Matches(toon,@"o\.rest=v\.texcoord3\.xyz;").Count==2,"both passes cut the openings in the rest space");
        Check(look.Contains("if(_ZombieSkin.w>0.0)")&&look.Contains("float gash=")&&look.Contains("float3 under="),"dried blood in the cloth, gashes on the skin, raw flesh through the tears");
        Check(zr.Contains("if(v.Decay!=null&&v.Decay.Length>0)body.All=ZombieDecayMake(root,skeleton,v,body.All);")&&zr.Contains("s.sharedMesh.SetUVs(3,rest);")&&zr.Contains("var pit=VisualHolePit(all,bone,h);")&&zr.Contains("var go=new GameObject(\"Bones.Decay\");"),"each opening's rest written once, its pit and its bones made");
        Check(zr.Contains("private static bool ZombieDecayCut(Renderer r)=>r!=null&&!ZombieEyePart(r)&&!r.name.StartsWith(\"Teeth.\",StringComparison.Ordinal)&&r.name!=\"Sac.Spitter\"&&r.name!=\"Growth.Hulk\";"),"eyes, teeth, the sac and a mutant's growth never cut");
        Check(zr.Contains("if(v.Mutant)ZombieGrowthStart(root,skeleton,v,body.All);")&&zr.Contains("e.Build=System.Threading.Tasks.Task.Run(()=>NativeZombieMutation.Build(")&&zr.Contains("boneIndex3=g.Bone4[i],weight3=g.Weight4[i]")&&zr.Contains("float h=n==\"hand_r\"?b.Hand*b.Right:b.Hand;"),"a mutant's growth built off the main thread on its rest after the build, on four bones; its right arm grown");
        Check(zr.Contains("if(z.Growing&&!ZombieGrowthAttach(z)){z.SeenAt=Time.unscaledTime;return false;}")&&zr.Contains("if(covered[t[i]]&&covered[t[i+1]]&&covered[t[i+2]]){dropped++;any=true;continue;}")&&zr.Contains("covered[i]=g.Covers("),"a growing body draws voxel until its growth is on; the worn triangles the flesh covers are dropped");
        Check(zr.Contains("if(z.Kind==NativeHordeKinds.Hulk)return new NativeVisualRig.Grasp{RightGrip=1,LeftGrip=1,Clench=ZombieClench+(1-ZombieClench)*z.Busy};"),"the Hulk's hands are fists, clenched hardest through a blow");
        Check(zr.Contains("go.transform.SetParent(bone,false);")&&zr.Contains("go.AddComponent<MeshFilter>().sharedMesh=mesh;var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterials=use;")&&!zr.Contains("skin.bones=new[]{bone}"),
            "the gear rides its bone as a rigid renderer (the project skins on the CPU)");
        // Flying parts from the model, not the voxel parts; their meshes pooled.
        Check(kp.Contains("p.Pieces=ZombiePartPieces(id,joint,p.Pieces);")&&kp.Contains("if(!ZombiePartDrawn(p,age,sink)&&!HordeNoVoxel(p.Kind))sourceModels.Queue(p.Model,")&&kp.Contains("p.Live=false;ZombiePiecesFree(p.Pieces);continue;"),"a modelled zombie's severed, sliced and gibbed parts are its own model's");
        Check(zr.Contains("if((mine[t[i]]?1:0)+(mine[t[i+1]]?1:0)+(mine[t[i+2]]?1:0)<2)continue;")&&zr.Contains("skin.BakeMesh(baked,true);zombieBakedNow[skin]=baked;"),"a part keeps the triangles skinned mostly to its bones, each renderer baked once a frame");
        Check(zr.Contains("if(c.Meshes[i]!=null&&c.Baked[i])zombieCorpseMeshes.Push(c.Meshes[i]);"),"a corpse's pits drawn from their own meshes, never pooled for bakes");
        foreach(var ban in new[]{".Hp=","StrikeAt","LastAttack=","Struck","Damage(","NativeNetwork","PveAddStats",".radius=",".height="})Check(!zr.Contains(ban)&&!core.Contains(ban),"presentation only: never "+ban);
        Check(!core.Contains("UnityEngine"),"the table is Core (no Unity types)");
    }
}
