using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;

// Engine-free checks for the entrance presentation (Core/NativeIntroRules.cs): which weapon the bare intro rig holds
// (player-intros.ts builds one only for a weapon intro, so the L33T Throne's stage staff is the only staff), the staged
// fallback for a style without an entrance, and the entrance-stage light conversion (NativeEntranceLight) that gives Unity's
// directional stage lights the source's diffuse light once Linear colour space is pinned (point lights and the Siren cell go
// to the shaders' stage block, Tests/SirenIntroChecks.cs). The baked intro clips and weapon models are
// checked too: both arms posed in every armed entrance, the braced weapons' free hand on their support grip, each intro
// weapon exported on its wrist at the grip, and the glow-voxel fade. Unity's rendering is not exercised.
internal static class IntroPresentationChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Intro presentation: "+what);}
    private static readonly string[] WeaponIntros={"weapon-sword","weapon-daggers","weapon-shield","weapon-greatsword","weapon-pistol","weapon-rifle","weapon-machinegun","weapon-fire","weapon-water","weapon-lightning","weapon-darkness"};
    public static IEnumerable<string> Run()
    {
        var styles=NativeIntroRules.Entrances;Check(styles.Length==20,"twenty entrances");
        var seen=new HashSet<string>();foreach(var s in styles)Check(seen.Add(s),"duplicate entrance "+s);
        // Hands: a weapon intro holds its own weapon model and the Emperor's Oath holds the emperor gauntlets.
        int armed=0;
        foreach(var s in styles)
        {
            string held=NativeIntroRules.IntroWeapon(s);bool weapon=Array.IndexOf(WeaponIntros,s)>=0;
            Check(weapon?held==s:s=="dev-emperor"?held=="weapon-emperor":held==null,s+" holds "+(held??"nothing"));if(held!=null)armed++;
        }
        Check(armed==WeaponIntros.Length+1,"eleven weapon intros and the Emperor's Oath are armed");
        foreach(var s in new[]{"dev-matrix","dev-ansatsuken","dev-siren","neon-drop","pulse-wave"})Check(NativeIntroRules.IntroWeapon(s)==null,s+" must keep its hands empty");
        Check(NativeIntroRules.IntroWeapon("weapon-siren")==null&&NativeIntroRules.IntroWeapon(null)==null&&NativeIntroRules.IntroWeapon("weapon-")==null,"unknown weapon intros hold nothing");
        // Staging: every entrance stages itself; anything else (a Siren seat without its grant) stages the source fallback.
        foreach(var s in styles)Check(NativeIntroRules.Entrance(s)==s,s+" stages itself");
        foreach(var s in new[]{"weapon-siren","weapon-leet","",null,"DEV-SIREN"})Check(NativeIntroRules.Entrance(s)=="neon-drop",(s??"null")+" falls back to neon-drop");
        Check(NativeIntroRules.Duration("weapon-siren")==NativeIntroRules.Duration("neon-drop")&&NativeIntroRules.NameAt("weapon-siren")==NativeIntroRules.NameAt("neon-drop"),"fallback keeps the replicated timing");
        // Lights: what Unity applies equals the source's diffuse light (linear colour x intensity / PI) in every mode.
        int lights=0;var rng=new Random(1709);
        for(int i=0;i<400;i++)
        {
            double r=rng.NextDouble(),g=rng.NextDouble(),b=rng.NextDouble(),intensity=i%4==0?rng.NextDouble():rng.NextDouble()*40;
            foreach(var (linear,linearIntensity) in new[]{(true,false),(true,true),(false,false)})
            {
                NativeEntranceLight.Light(r,g,b,intensity,linear,linearIntensity,out double cr,out double cg,out double cb,out double unity);
                Check(cr>=0&&cr<=1+1e-12&&cg>=0&&cg<=1+1e-12&&cb>=0&&cb<=1+1e-12&&unity>=0,"light colour stays in 0..1");
                double k=intensity/Math.PI;
                Near(NativeEntranceLight.Applied(cr,unity,linear,linearIntensity),r*k,"red");Near(NativeEntranceLight.Applied(cg,unity,linear,linearIntensity),g*k,"green");Near(NativeEntranceLight.Applied(cb,unity,linear,linearIntensity),b*k,"blue");lights++;
            }
        }
        NativeEntranceLight.Light(.5,.5,.5,0,true,false,out double zr,out _,out _,out double zero);Check(zr==0&&zero==0,"an unlit bank light stays off");
        // The Siren cell's bulb at the reveal (siren/intro.ts: PointLight 0xe9f0d8, 38 x .63): the raw bank values Unity got
        // before made it ~(.815 x 24)^2.2 in Linear space, hundreds of times the source; converted, it is the source's light.
        double bulb=.815,power=23.9;
        Check(NativeEntranceLight.Applied(bulb,power,true,false)>100*bulb*power/Math.PI,"raw bank light blows out the cell in Linear space");
        NativeEntranceLight.Light(bulb,.871,.687,power,true,false,out double br,out _,out _,out double bulbUnity);
        Near(NativeEntranceLight.Applied(br,bulbUnity,true,false),bulb*power/Math.PI,"Siren bulb");Check(bulbUnity<8,"Siren bulb fits Unity's light intensity range");
        // Ambient goes through sRGB authoring: Unity's linearised value is the source's ambient / PI.
        foreach(double a in new[]{0,.0012,.004,.05,.4,1.3})Near(NativeEntranceLight.ToLinear(NativeEntranceLight.Ambient(a,true)),a/Math.PI,"ambient");
        yield return "intro hands: 11 weapon intros hold their own weapon, the Emperor's Oath holds weapon-emperor, 8 entrances (L33T Throne, Rain Warrior, Siren, poses) hold none";
        yield return "intro staging: 20 entrances stage themselves, unknown styles stage neon-drop with unchanged timing";
        yield return "entrance lights: "+lights+" conversions apply the source's diffuse light in Linear, linear-intensity and Gamma modes; Siren bulb no longer blown out";
        foreach(var result in Baked(Root()))yield return result;
    }
    private static readonly string[] Armed={"weapon-sword","weapon-daggers","weapon-shield","weapon-greatsword","weapon-pistol","weapon-rifle","weapon-machinegun","weapon-fire","weapon-water","weapon-lightning","weapon-darkness","dev-emperor"};
    private static IEnumerable<string> Baked(string root)
    {
        // Grip: the weapon origin on the wrist (rig-ik GRIP) for every weapon intro; the Emperor's ungripped gauntlets on the
        // character-rig.ts hand socket.
        foreach(var s in NativeIntroRules.Entrances)Check(NativeIntroRules.IntroGrip(s)==(s=="dev-emperor"?new Vector3(0,-.065f,.055f):Vector3.Zero),s+" grip");
        // Belt Rattle's muzzle flash: buildWeapon's flash at rest on the muzzle, alternate 1/24 s frames of the 1.93-2.33 s burst.
        Check(NativeIntroRules.IntroMuzzleFlash("weapon-machinegun",1.94,out var flash)&&flash.Glow&&flash.Shape==WeaponShape.Cone&&flash.Color==0xffdb90&&Math.Abs(flash.Position.Z-(NativeWeaponDressing.MuzzleZ("machinegun")+.14f))<1e-6f&&flash.Scale==new Vector3(.11f,.30f,.11f),"machinegun flash on");
        foreach(double t in new[]{1.9,1.98,2.34,3.0})Check(!NativeIntroRules.IntroMuzzleFlash("weapon-machinegun",t,out _),"machinegun flash off at "+t);
        Check(NativeIntroRules.IntroMuzzleFlash("weapon-machinegun",2.03,out _)&&!NativeIntroRules.IntroMuzzleFlash("weapon-rifle",1.94,out _)&&!NativeIntroRules.IntroMuzzleFlash("weapon-pistol",2.13,out _),"only Belt Rattle flashes");
        // Glow voxels: colour x alpha keeps the source's additive term, alpha in 0..1, spent voxels vanish.
        var rng=new Random(4412);int glows=0;
        for(int i=0;i<500;i++)
        {
            float r=(float)rng.NextDouble()*1.15f,g=(float)rng.NextDouble()*1.15f,b=(float)rng.NextDouble()*1.15f;if(i%7==0)r=g=b=(float)rng.NextDouble()*.02f;float r0=r,g0=g,b0=b,a=NativeIntroRules.GlowAlpha(ref r,ref g,ref b);
            Check(a>=0&&a<=1&&Math.Abs(r*a-r0)<1e-5f&&Math.Abs(g*a-g0)<1e-5f&&Math.Abs(b*a-b0)<1e-5f,"glow voxel keeps its additive light");Check(Math.Max(r0,Math.Max(g0,b0))<1||a==1,"bright voxels stay opaque");glows++;
        }
        {float r=0,g=0,b=0;Check(NativeIntroRules.GlowAlpha(ref r,ref g,ref b)==0&&r==0&&g==0&&b==0,"a black voxel is gone");}
        var track=new float[2*3*20];for(int i=0;i<track.Length;i++)track[i]=1;track[(1*3+2)*20+18]=.4f;Check(!NativeIntroRules.TintVaries(track,3,0)&&NativeIntroRules.TintVaries(track,3,1),"tint tracks");
        // Weapon models (Resources/SourceModels.json): every intro weapon is exported on its wrist joint (staffs on the left,
        // where poseWeaponIntro moves them; daggers and gauntlets on both) with the wrist inside its bounds, so the model sits in
        // the hand at the grip, not scattered on other joints or at the rig root. The shield face is strapped .12 m forward.
        int weapons=0;
        using(var bank=Decode(root,"SourceModels"))foreach(var m in bank.RootElement.GetProperty("models").EnumerateArray())
        {
            string id=m.GetProperty("id").GetString(),style=Array.Find(Armed,s=>NativeIntroRules.IntroWeapon(s)==id);if(style==null)continue;weapons++;
            bool staff=style=="weapon-fire"||style=="weapon-water"||style=="weapon-lightning"||style=="weapon-darkness",both=style=="weapon-daggers"||style=="dev-emperor";var bones=new HashSet<int>();
            foreach(var s in m.GetProperty("sections").EnumerateArray())
            {
                int bone=s.GetProperty("bone").GetInt32();bones.Add(bone);Check(bone==CapturedRig.LHand||bone==CapturedRig.RHand,id+" section off the hands");
                var p=s.GetProperty("positions");var min=new Vector3(float.MaxValue);var max=new Vector3(float.MinValue);
                for(int i=0;i+2<p.GetArrayLength();i+=3){var v=new Vector3((float)p[i].GetDouble(),(float)p[i+1].GetDouble(),(float)p[i+2].GetDouble());min=Vector3.Min(min,v);max=Vector3.Max(max,v);}
                float gap=Vector3.Max(Vector3.Max(min,-max),Vector3.Zero).Length();Check(gap<=(style=="weapon-shield"?.15f:1e-4f),id+" is not held at the wrist ("+gap+" m)");
            }
            Check(both?bones.Count==2:bones.Count==1&&bones.Contains(staff?CapturedRig.LHand:CapturedRig.RHand),id+" rides the wrong hand");
        }
        Check(weapons==Armed.Length,"every armed entrance has its weapon model");
        // Intro clips (Resources/SourceMotion.json, baked from the armed rig by tools/export_motion.mjs): no arm of an armed
        // entrance ever hangs at its rest pose (the bare-rig bake left the braced weapons' free arm down), and the greatsword,
        // rifle (after its off-hand phrase hands over at 1.8 s) and machinegun keep the free wrist on the weapon's support-grip
        // (weapon-visuals.ts sockets in the right wrist frame), as far as rig-ik.ts's support reach (.10-.515 m) allows.
        var grips=new Dictionary<string,Vector3>{{"weapon-greatsword",new Vector3(0,0,-.11f)},{"weapon-rifle",new Vector3(.025f,-.03f,.10f)},{"weapon-machinegun",new Vector3(.025f,-.03f,.10f)}};
        int frames=0,braced=0;double worstGrip=0,closest=double.MaxValue;var pos=new Vector3[22];var rot=new Quaternion[22];
        using(var motion=Decode(root,"SourceMotion"))
        {
            var clips=new Dictionary<string,JsonElement>();foreach(var c in motion.RootElement.GetProperty("clips").EnumerateArray())clips[c.GetProperty("key").GetString()]=c;
            foreach(var style in Armed)
            {
                Check(clips.TryGetValue("intro-"+style,out var clip),"intro clip "+style);int count=clip.GetProperty("frames").GetInt32();float duration=(float)clip.GetProperty("duration").GetDouble();var values=clip.GetProperty("values");
                for(int f=0;f<count;f++)
                {
                    float at=f*duration/(count-1);
                    for(int b=0;b<22;b++)
                    {
                        int o=(f*22+b)*7;var p=new Vector3((float)values[o].GetDouble(),(float)values[o+1].GetDouble(),(float)values[o+2].GetDouble());var q=new Quaternion((float)values[o+3].GetDouble(),(float)values[o+4].GetDouble(),(float)values[o+5].GetDouble(),(float)values[o+6].GetDouble());
                        int parent=CapturedRig.Bones[b].Parent;if(parent<0){pos[b]=p;rot[b]=q;}else{pos[b]=pos[parent]+Vector3.Transform(p,rot[parent]);rot[b]=rot[parent]*q;}
                    }
                    foreach(bool left in new[]{true,false})
                    {
                        int clavicle=left?CapturedRig.LClav:CapturedRig.RClav,wrist=left?CapturedRig.LHand:CapturedRig.RHand;
                        var local=Vector3.Transform(pos[wrist]-pos[clavicle],Quaternion.Inverse(rot[clavicle]));double hang=(local-new Vector3(left?.07f:-.07f,-.52f,0)).Length();closest=Math.Min(closest,hang);
                        Check(hang>.03,style+" leaves the "+(left?"left":"right")+" arm hanging at "+at.ToString("0.00")+" s");
                    }
                    if(grips.TryGetValue(style,out var grip)&&(style!="weapon-rifle"||at>=1.8f))
                    {
                        var target=pos[CapturedRig.RHand]+Vector3.Transform(grip,rot[CapturedRig.RHand]);var shoulder=pos[CapturedRig.LArm];float reach=(target-shoulder).Length();
                        var want=shoulder+(target-shoulder)*(Math.Clamp(reach,.10f,.515f)/reach);double error=(pos[CapturedRig.LHand]-want).Length();worstGrip=Math.Max(worstGrip,error);braced++;
                        Check(error<.002,style+" free hand off the support grip at "+at.ToString("0.00")+" s ("+error.ToString("0.000")+" m)");
                    }
                    frames++;
                }
            }
        }
        yield return "intro weapons: "+weapons+" models ride their wrists at the grip (gauntlets on the hand socket); Belt Rattle flashes on its burst frames; "+glows+" glow-voxel fades keep the additive light";
        yield return "intro arms: "+frames+" armed-entrance frames pose both arms (closest to a hanging arm "+closest.ToString("0.000")+" m); "+braced+" braced frames keep the free wrist on the support grip (worst "+(worstGrip*1000).ToString("0.00")+" mm)";
    }
    private static JsonDocument Decode(string root,string name)
    {
        using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/"+name+".json")));
        using var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString()));using var zip=new GZipStream(bytes,CompressionMode.Decompress);using var reader=new StreamReader(zip);
        return JsonDocument.Parse(reader.ReadToEnd());
    }
    private static string Root()
    {
        foreach(string start in new[]{Directory.GetCurrentDirectory(),AppContext.BaseDirectory})
            for(var dir=new DirectoryInfo(start);dir!=null;dir=dir.Parent)
                if(File.Exists(Path.Combine(dir.FullName,"Assets/Conversion/Resources/SourceMotion.json")))return dir.FullName;
        throw new DirectoryNotFoundException("Intro presentation: conversion project not found");
    }
    private static void Near(double a,double b,string what){if(!(Math.Abs(a-b)<=1e-9*Math.Max(1,Math.Abs(b))))throw new Exception("Intro presentation: "+what+" "+a+" vs "+b);}
}
