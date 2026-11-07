using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// The Expedition drop pods (Core NativePodDrop, NativePodModels; Runtime NativePodPresentation, the spawn-in's pod path, the
// Locker's Pod tab): the drop over the opening countdown (out of the sky, staggered, retros, the slam, the sign, the door into
// a ramp before GO, the pilot hidden until it opens), parked after, the leaving launch; the look map; the LED name sign; the
// models; and the wiring, including that no presentation field initializer makes a Unity object (it broke the component).
internal static class PodDropChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Pod drop: "+what);count++;}
    private sealed class Recorder:INativeEntranceFx
    {
        public readonly List<string> Calls=new List<string>();
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed)=>Calls.Add("g"+at+color+size);
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)=>Calls.Add("s"+head+velocity+color);
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor)=>Calls.Add("m"+at+opacity+size);
    }
    public static void Run(string root)
    {
        count=0;Drop();Launch();Look();Sign();Models();Wiring(root);
        Console.WriteLine("PASS pod drop: "+count+" checks (out of the sky over the countdown, staggered, braking, the slam; the sign, the door into a ramp before GO, the pilot hidden until then; "+
            "parked, then leaving; the look map; the LED sign; the models; the wiring and no Unity object made in a field initializer)");
    }
    private static void Drop()
    {
        const int seed=4242;
        Check(NativePodDrop.Appear==-NativeSpawnIn.Countdown&&NativePodDrop.End<.75f,"the drop spans the opening countdown and ends just after GO");
        Check(NativePodDrop.ImpactOf(7)<NativePodDrop.DoorAt-NativePodDrop.SeamSeconds-.25f,"even the eighth pod lands before its door flares");
        var before=NativePodDrop.Evaluate(NativePodDrop.Appear-.5f,0,seed);Check(!before.Dropping&&before.Offset.Y>NativePodDrop.Altitude*2,"nothing before the countdown");
        var parked=NativePodDrop.Evaluate(float.NaN,0,seed);Check(!parked.Dropping&&parked.Offset==Vector3.Zero&&parked.Door==NativePodModels.OpenAngle&&parked.Shown&&parked.Sign==1,"an unknown time: parked, door open, sign lit");
        for(int order=0;order<4;order++)
        {
            float impact=NativePodDrop.ImpactOf(order),lastY=float.MaxValue,lastReach=float.MaxValue;bool signOn=false,shown=false;float lastDoor=0;
            Check(Math.Abs(impact-(NativePodDrop.Impact+NativePodDrop.Stagger*order))<1e-5f,"pod "+order+" lands in its turn");
            for(float t=NativePodDrop.Appear;t<NativePodDrop.End+1;t+=1/120f)
            {
                var f=NativePodDrop.Evaluate(t,order,seed);
                Check(float.IsFinite(f.Offset.X+f.Offset.Y+f.Offset.Z+f.Door+f.Lift),"finite frames");
                if(t<impact-1e-4f)
                {
                    float reach=new Vector2(f.Offset.X,f.Offset.Z).Length();
                    Check(f.Dropping&&f.Land<0&&f.Offset.Y<lastY&&reach<=lastReach+1e-4f&&f.Door==0&&!f.Shown,"pod "+order+" falls and closes in, door shut, pilot hidden ("+t+")");
                    Check(Math.Abs(f.Lift-(f.Offset.Y+NativePodModels.Floor))<1e-4f,"my view rides the pod's floor down");
                    lastY=f.Offset.Y;lastReach=reach;
                }
                else if(t<NativePodDrop.End)
                {
                    Check(f.Dropping&&f.Land>=-1e-4f&&Math.Abs(f.Offset.X)+Math.Abs(f.Offset.Z)<1e-4f&&Math.Abs(f.Offset.Y)<.1f,"pod "+order+" sits on its spot after the slam");
                    signOn|=f.Sign>.99f;Check(f.Door>=lastDoor-.15f,"the door only opens (a small bounce at rest)");lastDoor=f.Door;
                    if(t<NativePodDrop.DoorAt)Check(f.Door==0&&!f.Shown,"the door stays shut until DoorAt");
                    if(f.Shown){shown=true;Check(t>=NativePodDrop.DoorAt+NativePodDrop.ShowAfter-1e-4f&&f.Lift==0,"the pilot shows once the door is opening, standing on the floor");}
                }
                else Check(!f.Dropping&&f.Door==NativePodModels.OpenAngle&&f.Shown,"parked after the drop");
            }
            Check(signOn&&shown,"pod "+order+"'s sign lights and its pilot steps out");
            var atGo=NativePodDrop.Evaluate(0,order,seed);Check(atGo.Door>.3f*NativePodModels.OpenAngle&&atGo.Shown,"by GO the door is well open and the pilot shows");
        }
        // the approach is shared by the run and comes in from the side, the fall brakes (it lands at about a third of its speed)
        var a=NativePodDrop.Approach(seed);Check(Math.Abs(a.Length()-1)<1e-4f&&a.Y==0&&NativePodDrop.Approach(seed)==a,"one approach for the run");
        float v0=(NativePodDrop.Evaluate(NativePodDrop.Appear,0,seed).Offset.Y-NativePodDrop.Evaluate(NativePodDrop.Appear+.02f,0,seed).Offset.Y)/.02f,v1=(NativePodDrop.Evaluate(NativePodDrop.Impact-.02f,0,seed).Offset.Y-NativePodDrop.Evaluate(NativePodDrop.Impact-.0001f,0,seed).Offset.Y)/.0199f;
        Check(v0>2.5f*v1&&v1>8,"the fall brakes but still slams ("+v0+" to "+v1+" m/s)");
        Check(NativePodDrop.Evaluate(NativePodDrop.Impact-.3f,0,seed).Retro>.9f&&NativePodDrop.Evaluate(NativePodDrop.Impact-1f,0,seed).Retro<.01f,"the retros burn only the last half second");
        // the camera kick: only just after my landing
        Check(NativePodDrop.Kick(NativePodDrop.Evaluate(NativePodDrop.Impact+.06f,0,seed),out float pitch)>0&&pitch>0&&NativePodDrop.Kick(NativePodDrop.Evaluate(NativePodDrop.Impact-.1f,0,seed),out _)==0&&NativePodDrop.Kick(NativePodDrop.Evaluate(-.2f,0,seed),out _)==0,"the kick only just after the landing");
        // effects: deterministic, burning on the way down, dust on landing, none parked
        var r1=new Recorder();var r2=new Recorder();var spot=new Vector3(3,0,-2);var face=new Vector3(0,0,1);
        foreach(float t in new[]{-2.6f,-1.6f,-1.2f,-.5f,-.1f,.2f}){NativePodDrop.Effects(r1,NativePodDrop.Evaluate(t,0,seed),spot,face,7,1,0);NativePodDrop.Effects(r2,NativePodDrop.Evaluate(t,0,seed),spot,face,7,1,0);}
        Check(r1.Calls.Count>40&&r1.Calls.SequenceEqual(r2.Calls),"the effects are the same each time ("+r1.Calls.Count+" calls)");
        var fall=new Recorder();NativePodDrop.Effects(fall,NativePodDrop.Evaluate(-2.4f,0,seed),spot,face,7,1,0);Check(fall.Calls.Count(c=>c[0]=='s')>=6,"a burning trail on the way down");
        var land=new Recorder();NativePodDrop.Effects(land,NativePodDrop.Evaluate(NativePodDrop.Impact+.15f,0,seed),spot,face,7,1,0);Check(land.Calls.Count(c=>c[0]=='m')>=6,"dust and steam on the landing");
        var quiet=new Recorder();NativePodDrop.Effects(quiet,parked,spot,face,7,1,0);Check(quiet.Calls.Count==0,"a parked pod is quiet");
    }
    private static void Launch()
    {
        var at0=NativePodDrop.Launch(0);Check(at0.Dropping&&Math.Abs(at0.Door-NativePodModels.OpenAngle)<1e-4f&&at0.Offset.Y==0,"boarding: the door still open");
        var shut=NativePodDrop.Launch(NativePodDrop.LaunchClose+.01f);Check(shut.Door<1e-3f&&shut.Offset.Y==0,"the door shuts before it lifts");
        float last=-1;for(float t=NativePodDrop.LaunchIgnite;t<NativePodDrop.LaunchGone;t+=.05f){var f=NativePodDrop.Launch(t);Check(f.Offset.Y>=last&&f.Retro>.99f,"it lifts away on its retros");last=f.Offset.Y;}
        Check(last>NativePodDrop.Altitude*.5f&&NativePodDrop.Launch(NativePodDrop.LaunchGone).Offset.Y>NativePodDrop.Altitude*2&&!NativePodDrop.Launch(NativePodDrop.LaunchGone).Dropping,"and is gone");
        Check(!NativePodDrop.Launch(float.NaN).Dropping&&NativePodDrop.Launch(float.NaN).Door==NativePodModels.OpenAngle,"no launch: parked");
    }
    private static void Look()
    {
        Check(NativePodDrop.Normalize("")==""&&NativePodDrop.Normalize(null)=="","no look: the default");
        Check(NativePodDrop.Normalize("light:00FF00, paint:ff0000,bogus:123456,trim:zzzzzz,paint:0000ff")=="paint:0000ff,light:00ff00","the look is canonical: known entries, valid hex, the last wins, in order");
        var l=NativePodDrop.With("","trim",0x112233);Check(l=="trim:112233"&&NativePodDrop.Of(l,"trim")==0x112233&&NativePodDrop.Of(l,"paint")==-1,"With sets one entry");
        Check(NativePodDrop.With(l,"trim",-1)==""&&NativePodDrop.With(l,"nope",5)==l,"With -1 goes back to Auto; an unknown key changes nothing");
        NativePodDrop.Colors("","#123456","#abcdef",out int p,out int t,out int g);Check(p==0x123456&&t==NativePodDrop.DefaultTrim&&g==0xabcdef,"Auto: the body colour, gunmetal, the accent");
        NativePodDrop.Colors("paint:ff0000,light:00ff00","#123456","junk",out p,out t,out g);Check(p==0xff0000&&g==0x00ff00&&t==NativePodDrop.DefaultTrim,"picked colours win");
        NativePodDrop.Colors("",null,null,out p,out t,out g);Check(p==0xe5e7eb&&g==0x70e1d0,"no seat colours: the defaults");
        Check(NativePodDrop.Paints.Length>=12&&NativePodDrop.Paints.Distinct().Count()==NativePodDrop.Paints.Length,"twelve distinct paints");
    }
    private static void Sign()
    {
        Check(NativePodDrop.SignText("  sheldon ")=="SHELDON"&&NativePodDrop.SignText("")=="PILOT"&&NativePodDrop.SignText(null)=="PILOT","upper case, trimmed, PILOT when empty");
        Check(NativePodDrop.SignText("ñame~1")=="?AME?1"&&NativePodDrop.SignText(new string('a',40)).Length==NativePodDrop.MaxName,"letters the font lacks read '?'; at most twenty");
        foreach(char c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.!?'#*+/: ")Check(NativePodDrop.SignText(c.ToString()).Length==1||c==' ',"the font has '"+c+"'");
        Check(NativePodDrop.Lit("A",2,0)&&!NativePodDrop.Lit("A",0,0)&&NativePodDrop.Lit("A",0,3)&&!NativePodDrop.Lit("A",5,3)&&!NativePodDrop.Lit("A",-1,3)&&!NativePodDrop.Lit("A",2,7),"A's dots, the gap column and the edges");
        Check(!NativePodDrop.Scrolls("SHELDON")&&NativePodDrop.Scrolls("ABCDEFGHIJK")&&NativePodDrop.TextColumns("AB")==11,"ten letters fit, eleven scroll");
        var px=NativePodDrop.SignPixels("HI",8,out int w,out int h);Check(w==NativePodDrop.SignRows*8&&h==NativePodDrop.SignColumns*8&&px.Length==w*h*4,"the sign texture: rows across, the sign's length along");
        var lp=NativePodDrop.SignPixels("ABCDEFGHIJKL",8,out _,out int lh);Check(lh==(NativePodDrop.TextColumns("ABCDEFGHIJKL")+12)*8,"a long name's texture is its text and a gap (it scrolls round)");
        var flat=NativePodDrop.SignPixelsFlat("HI",4,out int fw,out int fh);Check(fw==NativePodDrop.SignColumns*4&&fh==NativePodDrop.SignRows*4&&flat.Length==fw*fh*4,"the Locker card's sign lies along x");
        int Bright(byte[] b){int n=0;for(int i=3;i<b.Length;i+=4)if(b[i]>200)n++;return n;}
        Check(Bright(px)>0&&Bright(NativePodDrop.SignPixels("I",8,out _,out _))<Bright(NativePodDrop.SignPixels("M",8,out _,out _)),"lit dots follow the letters");
    }
    private static void Models()
    {
        foreach(var (name,build) in new (string,Func<WeaponMesh>)[]{("pod",NativePodModels.Pod),("door",NativePodModels.Door)})
        {
            var a=build();var b=build();int tris=a.Sections.Sum(s=>s.Indices.Count/3);
            Check(a.Sections.Count==b.Sections.Count&&a.Sections.Zip(b.Sections,(x,y)=>x.Positions.SequenceEqual(y.Positions)&&x.Indices.SequenceEqual(y.Indices)).All(x=>x),name+" builds the same each time");
            Check(tris>50&&tris<9000,name+" stays in budget ("+tris+" triangles)");
            foreach(var s in a.Sections)
            {
                Check(s.Bone>=0&&s.Bone<=NativePodModels.Retro,name+" bone "+s.Bone+" is a pod part");
                Check(s.Positions.Count==s.Normals.Count&&s.Positions.Count==s.Colors.Count&&s.Indices.All(i=>i>=0&&i<s.Positions.Count),name+" section arrays agree");
                if(s.Bone==NativePodModels.Paint)Check(s.Colors.All(c=>c.X>.7f&&c.Y>.7f&&c.Z>.7f),name+"'s paint is near white (the draw tints it)");
                if(s.Bone==NativePodModels.Lights||s.Bone==NativePodModels.Cabin||s.Bone==NativePodModels.Retro)Check(s.Emission.All(e=>e.X+e.Y+e.Z>0),name+"'s lit parts glow");
            }
        }
        var pod=NativePodModels.Pod();var all=pod.Sections.SelectMany(s=>s.Positions).ToArray();
        Check(all.All(p=>new Vector2(p.X,p.Z).Length()<NativePodModels.Radius+.05f&&p.Y>-.08f&&p.Y<NativePodModels.Height+.05f),"the pod stays inside its footprint and height");
        Check(pod.Sections.Any(s=>s.Bone==NativePodModels.Paint)&&pod.Sections.Any(s=>s.Bone==NativePodModels.Trim)&&pod.Sections.Any(s=>s.Bone==NativePodModels.Lights)&&pod.Sections.Any(s=>s.Bone==NativePodModels.Cabin)&&pod.Sections.Any(s=>s.Bone==NativePodModels.Retro),"paint, trim, lights, cabin and retros to tint");
        // the doorway: nothing of the hull in the door's opening (x within the door, from the sill up, toward +z)
        Check(!pod.Sections.Where(s=>s.Bone==NativePodModels.Paint).SelectMany(s=>s.Positions).Any(p=>Math.Abs(p.X)<NativePodModels.DoorWidth/2-.05f&&p.Y>NativePodModels.Hinge.Y+.1f&&p.Y<NativePodModels.Hinge.Y+NativePodModels.DoorHeight-.1f&&p.Z>.5f),"the hull leaves the doorway open");
        var door=NativePodModels.Door().Sections.SelectMany(s=>s.Positions).ToArray();Check(door.All(p=>p.Y>=-.01f&&p.Y<=NativePodModels.DoorHeight+.01f&&Math.Abs(p.X)<=NativePodModels.DoorWidth/2+.01f),"the door stands up from its hinge, its width");
        // opened, the door lies outward as a ramp, its top on the ground
        var top=Vector3.Transform(new Vector3(0,NativePodModels.DoorHeight,0),Matrix4x4.CreateRotationX(NativePodModels.OpenAngle))+NativePodModels.Hinge;Check(top.Z>NativePodModels.Hinge.Z+1.4f&&Math.Abs(top.Y)<.08f,"open, the door rests on the ground as a ramp ("+top.Y+")");
        Check(NativePodModels.SignCentre.Z>.84f&&NativePodModels.SignCentre.Y>NativePodModels.Hinge.Y+NativePodModels.DoorHeight,"the sign sits over the door, on the marquee's face");
    }
    private static void Wiring(string root)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
        var spawn=R("Runtime/NativeSpawnInPresentation.cs");var pods=R("Runtime/NativePodPresentation.cs");
        Check(spawn.Contains("if(PodRun(arena))return PodSpawn(arena,pilot,feet,age,NativeSpawnIn.End(m.Phase,m.PhaseEnd,m.FightAt),out raise);"),"a pod run's pilots take the pod path");
        Check(spawn.Contains("||v.Pod)return;var f=v.Frame;bool slice")&&spawn.Contains("if(!v.Shown||v.Arena!=arena||v.Pod)continue;")&&spawn.Contains("||!v.Shown||v.Pod)return false;")&&spawn.Contains("look.Ghost=holo&&!v.Pod"),"no beam, hologram or veil for a pod");
        Check(spawn.Contains("if(v.Pod){PodCamera(camera,p.Seat.id,reduced);return;}"),"my camera rides my pod (only with camera shake on: SpawnInChecks)");
        Check(R("Runtime/NativeMachineModels.cs").Contains("Add(NativePodModels.PodId,NativePodModels.Pod());Add(NativePodModels.DoorId,NativePodModels.Door());"),"the bank builds the pod and its door");
        Check(R("Runtime/NativeSourcePresentation.cs").Contains("ExpeditionPods(arena);")&&R("Runtime/ArenaPresentation.cs").Contains("SpawnInFx(arena,camera);PodFx(arena,camera);"),"the pods and their effects are drawn");
        Check(R("Runtime/ConversionGame.cs").Contains("presentation.LocalPodLook=prefs.pod;")&&R("Runtime/ConversionGame.cs").Contains("public string pod=\"\";"),"my pod wears the Locker's look (prefs.pod)");
        Check(pods.Contains("podBlock??=new MaterialPropertyBlock();")&&pods.Contains("if(f.Sign<=.01f||pod.Sign==null||opacity<.99f||!EnsurePodSign())return;")&&pods.Contains("ghost?NativePodModels.GhostId:NativePodModels.PodId")&&R("Runtime/NativeMachineModels.cs").Contains("Ghost(NativePodModels.GhostId,NativePodModels.Pod());"),"the sign.s property block is made on first use; my faded pod is the glass copy and hides its sign");
        Check(R("Runtime/NativeLockerUI.cs").Contains("case NativeLocker.Pod:LkPodLayout();break;")&&R("Runtime/NativeLockerUI.cs").Contains("case NativeLocker.Pod:LkPod(k,e);break;"),"the Locker lays out and draws the Pod tab");
        // No field initializer of the presentation component (ArenaPresentation, a MonoBehaviour) makes a Unity object: Unity
        // refuses it in the constructor and leaves the component half built (a MaterialPropertyBlock did, 2026-11-02).
        var made=new Regex(@"^ {8}(private|public|internal|protected)[^;(=]*\b\w+\s*=\s*new (MaterialPropertyBlock|Material|Mesh|Texture2D|RenderTexture|GameObject)\(");
        foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))
        {
            bool inside=false;foreach(var line in File.ReadLines(file))
            {
                if(Regex.IsMatch(line,@"^    \S.*\bclass\s+\w+"))inside=Regex.IsMatch(line,@"\bclass\s+ArenaPresentation\b");
                if(inside)Check(!made.IsMatch(line),Path.GetFileName(file)+": no Unity object made in an ArenaPresentation field initializer ("+line.Trim()+")");
            }
        }
        foreach(var f in new[]{"Core/NativePodDrop.cs.meta","Core/NativePodModels.cs.meta","Runtime/NativePodPresentation.cs.meta","Runtime/NativeLockerPodUI.cs.meta"}){var t=R(f);Check(t.Contains("guid: ")&&!t.Contains(" \n"),"meta "+f);}
    }
}
