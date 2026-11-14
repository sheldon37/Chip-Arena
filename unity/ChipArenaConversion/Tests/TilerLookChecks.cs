using System;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

// The Tiler as a crab of tiles (Core NativeTilerLook, Runtime NativeTilerPresentation.cs; docs/ZOMBIE-MODELS.md 4k): its body
// made here (closed, outward-facing, inside its footprint), its scuttle's planted feet, the dormant patch flat in its wall and the
// peel onto the deck on the wake's .6 s, the bite's leap and snap on the authority's contact, its death's plates settling on the
// floor, determinism, no allocation, and the runtime's wiring into the horde's draw, gore, voice and clear. Its looks on screen
// are the owner's PC's.
internal static class TilerLookChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Tiler: "+what);count++;}
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
    private static bool Finite(Matrix4x4 m)=>new[]{m.M11,m.M12,m.M13,m.M21,m.M22,m.M23,m.M31,m.M32,m.M33,m.M41,m.M42,m.M43}.All(float.IsFinite);
    private static void Sound(NativeTilerLook.Shape s,string what)
    {
        Check(s.Pos.Length>0&&s.Nor.Length==s.Pos.Length&&s.Col.Length==s.Pos.Length&&s.Tri.Length%3==0&&s.Tri.All(t=>t>=0&&t<s.Pos.Length),what+": its arrays agree");
        Check(s.Pos.All(Finite)&&s.Nor.All(n=>Math.Abs(n.Length()-1)<1e-3f),what+": finite, unit normals");
        int back=0;string first="";for(int i=0;i<s.Tri.Length;i+=3){var a=s.Pos[s.Tri[i]];var f=Vector3.Cross(s.Pos[s.Tri[i+1]]-a,s.Pos[s.Tri[i+2]]-a);if(f.LengthSquared()<1e-12f)continue;if(Vector3.Dot(f,s.Nor[s.Tri[i]]+s.Nor[s.Tri[i+1]]+s.Nor[s.Tri[i+2]])<0){if(back==0)first=" (first at "+a+" face "+Vector3.Normalize(f)+" normal "+s.Nor[s.Tri[i]]+" area "+(f.Length()*.5f)+")";back++;}}
        Check(back==0,what+": every face wound to face its normals (Unity's winding; "+back+" not"+first+")");
    }
    private static Vector3 Foot(NativeTilerLook.Pose p,int leg)=>Vector3.Transform(Vector3.Transform(new Vector3(0,0,NativeTilerLook.Tibia),p.Part[leg*3+2]),p.Body);
    public static void Run(string root)
    {
        Check(NativeTilerLook.Kind>=0&&NativeTilerLook.Kind==NativeHordeKinds.Resolve("tiler")&&!NativeZombieLooks.Has(NativeTilerLook.Kind)&&!NativeZombieLooks.Modelled(NativeTilerLook.Kind),
            "the Tiler is no person: its own body, no variants on the model");
        // ---- the body ----
        for(int v=0;v<NativeTilerLook.Variants;v++)
        {
            var c=NativeTilerLook.Shell(v);Sound(c.Living,"carapace "+v);Sound(c.Dome,"grout "+v);for(int i=0;i<c.Plate.Length;i++)Sound(c.Plate[i],"plate "+v+"."+i);
            Check(c.Plate.Length==NativeTilerLook.Plates&&NativeTilerLook.Plates>=28&&NativeTilerLook.Plates<=48&&c.Living.Pos.All(p=>Math.Abs(p.X)<NativeTilerLook.A+.05f&&Math.Abs(p.Z)<NativeTilerLook.B+.05f&&p.Y>NativeTilerLook.Belly-.012f&&p.Y<NativeTilerLook.Crown+.03f),
                "carapace "+v+": "+NativeTilerLook.Plates+" plates wrapping a dome inside its half metre");
            int markers=c.Plate.Count(s=>s.Col.Count(k=>Vector4.Distance(k,NativeTilerLook.Marker)<.01f)>s.Col.Length/3),chipped=c.Plate.Count(s=>s.Col.Any(k=>Vector4.Distance(k,NativeTilerLook.Biscuit)<.01f));
            Check(markers<=1&&chipped>=3,"carapace "+v+": a few chipped to the biscuit ("+chipped+"), at most one dark marker ("+markers+")");
        }
        Check(NativeTilerLook.Shell(0)!=NativeTilerLook.Shell(1)&&NativeTilerLook.Shell(0).Living.Col.Length>0,"three carapaces");
        var limbs=NativeTilerLook.Limbs();Check(limbs.Length==NativeTilerLook.Parts,"six legs of three segments and two mandibles");foreach(var s in limbs.Distinct())Sound(s,"limb");
        Sound(NativeTilerLook.Chip(),"chip");Sound(NativeTilerLook.Scar(),"scar");
        Check(NativeTilerLook.Shell(0).Dome.Col.Any(k=>k.W==0)&&NativeTilerLook.Shell(0).Living.Col.Count(k=>k.W==0)==NativeTilerLook.Shell(0).Dome.Col.Count(k=>k.W==0),"its glass eyes unlined");

        // ---- the scuttle: tripods, feet planted on the floor through their stance ----
        uint seed=NativeHordeKinds.Seed(40077);var pose=new NativeTilerLook.Pose();
        foreach(float speed in new[]{1.5f,5.5f})
        {
            var g=new NativeTilerLook.Gait();var m=new NativeTilerLook.Moment{WakeAge=float.NaN,BiteAge=float.NaN,HitAge=float.NaN};const float dt=1f/144;float slip=0,floorWorst=0;int stances=0;
            var last=new Vector3[6];var had=new bool[6];
            for(int f=0;f<720;f++)
            {
                float t=f*dt;NativeTilerLook.Step(ref g,new Vector2(0,speed),0,dt);NativeTilerLook.Living(pose,g,seed,t,m);var origin=new Vector3(0,0,speed*t);
                if(f<300){Array.Clear(had,0,6);continue;}
                for(int i=0;i<6;i++)
                {
                    float q=g.Phase+NativeTilerLook.Offset(i);q-=(float)Math.Floor(q);var w=origin+Foot(pose,i);bool stance=q>.04f&&q<.46f;
                    if(stance){if(had[i]){slip=Math.Max(slip,(w-last[i]).Length());}floorWorst=Math.Max(floorWorst,Math.Abs(w.Y));stances++;}
                    had[i]=stance;last[i]=w;
                }
            }
            Check(stances>500&&slip<.004f&&floorWorst<.004f,"at "+speed+" m/s its planted feet hold (worst "+(slip*1000).ToString("0.0")+" mm a frame) on the floor (worst "+(floorWorst*1000).ToString("0.0")+" mm)");
        }
        // ---- dormant in its wall, the peel onto the deck ----
        {
            var g=new NativeTilerLook.Gait();var m=new NativeTilerLook.Moment{Dormant=true,WakeAge=float.NaN,BiteAge=float.NaN,HitAge=float.NaN,Wall=new Vector3(0,0,-1),WallDistance=.55f};
            NativeTilerLook.Living(pose,g,seed,1,m);var shell=NativeTilerLook.Shell(NativeTilerLook.Variant(seed)).Living;
            float proud=shell.Pos.Max(p=>Vector3.Transform(p,pose.Body).Z+.55f);var lp=new Vector3[NativeTilerLook.LimbVertices];var ln=new Vector3[lp.Length];NativeTilerLook.WriteLimbs(pose,lp,ln);
            float legs=lp.Max(p=>Vector3.Transform(p,pose.Body).Z+.55f);float low=shell.Pos.Min(p=>Vector3.Transform(p,pose.Body).Y);
            Check(proud<.025f&&proud>.005f&&legs<.012f&&low>.15f,"dormant: a patch of tiles "+(proud*100).ToString("0.0")+" cm proud of its wall, its legs folded inside it, off the floor");
            m.Dormant=false;Vector3 prev=default;float jump=0;
            for(int f=0;f<=100;f++){m.WakeAge=f*.0075f;NativeTilerLook.Living(pose,g,seed,1,m);var at=pose.Body.Translation;if(f>0)jump=Math.Max(jump,(at-prev).Length());prev=at;}
            m.WakeAge=NativeTilerLook.Peel+.01f;NativeTilerLook.Living(pose,g,seed,1,m);
            Check(jump<.04f&&Math.Abs(pose.Body.Translation.Y)<.03f&&Math.Abs(pose.Body.M22-1)<.05f,"the peel: out of the wall onto the deck in .6 s without a jump (worst "+(jump*100).ToString("0.0")+" cm a 7.5 ms step), standing at its end");
            var deck=new NativeTilerLook.Moment{Dormant=true,WakeAge=float.NaN,BiteAge=float.NaN,HitAge=float.NaN};NativeTilerLook.Living(pose,g,seed,1,deck);
            Check(shell.Pos.Max(p=>Vector3.Transform(p,pose.Body).Y)<.03f,"without a wall it lies flat in the deck");
        }
        // ---- the bite: crouch, the leap at the pilot, the snap on contact, back ----
        {
            var g=new NativeTilerLook.Gait();var target=new Vector3(0,.3f,2);float strike=.4f,recover=1.2f;
            NativeTilerLook.Moment M(float a)=>new NativeTilerLook.Moment{WakeAge=float.NaN,BiteAge=a,Strike=strike,Recover=recover,HasTarget=true,Target=target,HitAge=float.NaN};
            NativeTilerLook.Living(pose,g,seed,0,M(.15f));float crouch=pose.Body.Translation.Y,openWind=Mandibles(pose);
            NativeTilerLook.Living(pose,g,seed,0,M(strike));var at=pose.Body.Translation;float shut=Mandibles(pose);
            NativeTilerLook.Living(pose,g,seed,0,M(recover+.31f));var back=pose.Body.Translation;
            Check(crouch<-.02f&&openWind>shut+.3f&&(new Vector3(at.X,0,at.Z)-new Vector3(0,0,2)).Length()<.6f&&new Vector2(back.X,back.Z).Length()<.02f,
                "the bite: crouched with its mandibles wide, at the pilot with them snapped shut on the authority's contact, back where it stands after");
        }
        // ---- the death: the plates fly, bounce and settle on the floor, then sink away ----
        {
            var plates=new Matrix4x4[NativeTilerLook.Plates];bool alive=NativeTilerLook.Dying(pose,seed,.2f,plates);bool flying=plates.Any(p=>p.Translation.Y>.25f);
            NativeTilerLook.Dying(pose,seed,NativeTilerLook.Settle-.05f,plates);
            Check(alive&&flying&&plates.All(Finite)&&plates.All(p=>Math.Abs(Vector3.Transform(new Vector3(0,NativeTilerLook.Thick*.5f,0),p).Y-NativeTilerLook.Thick*.5f)<.003f&&new Vector2(p.Translation.X,p.Translation.Z).Length()<2.5f)&&Finite(pose.Body),
                "its death: the plates burst up and come to rest on the floor within 2.5 m");
            Check(NativeTilerLook.Dying(pose,seed,1.5f,plates)&&pose.Scale<.05f&&plates.All(Finite)&&!NativeTilerLook.Dying(pose,seed,NativeTilerLook.GoneAt,plates),"the grout crumbled by 1.5 s, all of it gone at "+NativeTilerLook.GoneAt+" s");
        }
        // ---- determinism, no allocation ----
        {
            var g=new NativeTilerLook.Gait();var a=new NativeTilerLook.Pose();var b=new NativeTilerLook.Pose();var m=new NativeTilerLook.Moment{WakeAge=float.NaN,BiteAge=.3f,Strike=.4f,Recover=1.2f,HitAge=.05f,HitPush=Vector2.UnitX};
            NativeTilerLook.Step(ref g,new Vector2(.3f,4),.5f,.016f);NativeTilerLook.Living(a,g,seed,3.2f,m);NativeTilerLook.Living(b,g,seed,3.2f,m);
            Check(a.Body==b.Body&&a.Part.SequenceEqual(b.Part)&&Finite(a.Body)&&a.Part.All(Finite),"two peers pose it the same, finite");
            var lp=new Vector3[NativeTilerLook.LimbVertices];var ln=new Vector3[lp.Length];var plates=new Matrix4x4[NativeTilerLook.Plates];
            for(int i=0;i<50;i++){NativeTilerLook.Step(ref g,new Vector2(0,5),0,.007f);NativeTilerLook.Living(a,g,seed,i*.007f,m);NativeTilerLook.WriteLimbs(a,lp,ln);NativeTilerLook.Dying(b,seed,i*.05f,plates);}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<1000;i++){NativeTilerLook.Step(ref g,new Vector2(0,5),.2f,.007f);NativeTilerLook.Living(a,g,seed,i*.007f,m);NativeTilerLook.WriteLimbs(a,lp,ln);NativeTilerLook.Dying(b,seed,(i%60)*.05f,plates);}
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"no allocation per frame");
        }
        // ---- the runtime's wiring ----
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            string kp=R("NativeHordeKindPresentation.cs"),gore=R("NativeHordeGorePresentation.cs"),audio=R("NativeZombieAudio.cs"),tp=R("NativeTilerPresentation.cs");
            Check(kp.Contains("else if(TilerIs(kind))TilerAdd(arena,m,s,pos,yaw,velocity,ground,now,flash);")&&kp.Contains("if(TilerIs(d.Kind)){TilerDeath(death,hordeNow-death.at);return true;}")
                &&kp.Contains("!SmilerIs(kind)&&!TilerIs(kind))HordeSever(")&&kp.Contains("SmilerFx(now);TilerFx(now);")&&kp.Contains("ClearSmilers();ClearTilers();"),"drawn as itself, its death its own, no severed limbs, its effects, cleared with the arena");
            Check(gore.Contains("if(TilerHit(m,s,e,at,dir))return;")&&audio.Contains("m.Pve.Stats?.id==\"tiler\")continue;")&&kp.Contains("bool smiler=kind==NativeSmilerLook.Kind||kind==NativeTilerLook.Kind;"),"hits chip it (no blood), no zombie voice");
            Check(!tp.Contains("NativePackBanks")&&!tp.Contains("Resources.Load")&&tp.Contains("ZombieGearMaterials()"),"it needs no local assets: its body is made in code, drawn with the committed toon shader");
        }
        Console.WriteLine("PASS tiler look ("+count+" checks: three carapaces of tiles wrapping a dome, closed and outward; six legs and two mandibles; planted feet hold at 1.5 and 5.5 m/s; dormant flat in its wall, the peel in .6 s; the bite's leap and snap on contact; the death's plates settle and sink away; deterministic; no allocation; wired into the horde)");
    }
    private static float Mandibles(NativeTilerLook.Pose p)
    {
        // The angle between the two mandibles' ahead axes (their tips apart as they open).
        var l=new Vector3(p.Part[NativeTilerLook.Legs*3].M31,0,p.Part[NativeTilerLook.Legs*3].M33);var r=new Vector3(p.Part[NativeTilerLook.Legs*3+1].M31,0,p.Part[NativeTilerLook.Legs*3+1].M33);
        return (float)Math.Acos(Math.Max(-1,Math.Min(1,Vector3.Dot(Vector3.Normalize(l),Vector3.Normalize(r)))));
    }
}
