using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;

// Engine-free checks for the entrances' closing holds (owner 2026-10-13: "why do we do that little one arm movement up in
// each intro? That seems to be a legacy motion"). The arm raise was the shared hero hold (NativeEntranceKeys.Hero, the
// Electron neon-drop entrance's last key: the right fist lifted to the hip, the elbow bent 1.28 rad) that every procedural
// entrance and the Bow's Arrowfall keyed under the name card, each with a pre-hold key lifting that forearm; and, in the four
// staff schools, the pack's closing two-handed staff idle holding the right fist up in an empty grip in front of the chest.
// Now no entrance raises that arm into the card: the keyed holds hang both arms loose, the staff schools' free hand comes
// down to hang, smoothly, and every other entrance keeps its own clip. The timing marks (hits, the slow-motion beat, the
// shot cuts, the name card and the duration) are pinned to their values before the change.
internal static class IntroHoldChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Intro holds: "+what);}
    /// <summary>The keyed entrances that settled into the shared hero hold.</summary>
    private static readonly string[] Keyed={"orbital-drop","grid-rez","holo-projection","blackout","skyline-zip","hero-landing","overclock",NativeBowEntrance.Id};
    /// <summary>The staff schools whose free hand rose into the pack idle's empty grip.</summary>
    private static readonly string[] Staffs={"weapon-fire","weapon-water","weapon-lightning","weapon-darkness"};
    // The timing marks before the change (2026-10-13, shared head 51f0aa7): hit, slow-motion rate/hold/recover/catch-up, the
    // shot cuts (presentation clock). The DEV Cyber Lord's Hardlight Boot keeps its own marks (its own task) and is pinned only
    // to the standard phase below.
    private static readonly (string style,float hit,float[] slow,float[] cuts)[] Marks=
    {
        ("blackout",1.2f,new[]{.4f,.1f,.22f,.6f},new[]{0,.4f,1.2f,2,2.4f,3.2f}),
        ("grid-rez",2.4f,new[]{.45f,.1f,.25f,.7f},new[]{0,.8f,1.6f,2.4f,3.2f}),
        ("hero-landing",1.2f,new[]{.25f,.16f,.3f,1},new[]{0,.62f,1.2f,2.05f,2.8f,3.2f}),
        ("holo-projection",1.6f,new[]{.35f,.1f,.25f,.85f},new[]{0,.6f,1.6f,2.1f,3.2f}),
        ("orbital-drop",1.6f,new[]{.28f,.14f,.28f,.95f},new[]{0,.72f,1.2f,1.6f,2.4f,3.2f}),
        ("overclock",1.6f,new[]{.28f,.14f,.3f,.95f},new[]{0,.8f,1.2f,1.6f,2.4f,3.2f}),
        ("sheldon-intro",2.38f,new[]{1,0,.1f,.5f},new[]{0,.51f,.86f,1.21f,2.38f,3.32f}),
        ("skyline-zip",1.4f,new[]{.3f,.12f,.28f,.9f},new[]{0,.5f,1.1f,1.4f,2.2f,3.2f}),
        ("weapon-bow",2,new[]{.3f,.14f,.3f,.9f},new[]{0,.42f,1,1.6f,2,2.4305723f,3.2f}),
        ("weapon-daggers",2.12f,new[]{.3f,.12f,.28f,.9f},new[]{0,.78f,1.62f,2.12f,2.8f,3.2f}),
        ("weapon-darkness",2.12f,new[]{.3f,.12f,.28f,.9f},new[]{0,.9f,1.6f,2.12f,2.75f,3.2f}),
        ("weapon-fire",1.8f,new[]{.3f,.12f,.28f,.9f},new[]{0,.8f,1.4f,1.8f,2.6f,3.2f}),
        ("weapon-greatsword",1.8f,new[]{.25f,.12f,.28f,.9f},new[]{0,.9f,1.45f,1.8f,2.6f,3.2f}),
        ("weapon-lightning",1.45f,new[]{.3f,.12f,.28f,.9f},new[]{0,.55f,1.1f,1.45f,2.25f,3.2f}),
        ("weapon-machinegun",2.12f,new[]{.3f,.16f,.28f,.9f},new[]{0,.7f,1.45f,1.93f,2.7f,3.2f}),
        ("weapon-pistol",2.12f,new[]{.22f,.16f,.28f,.9f},new[]{0,.5f,1.15f,1.75f,2.12f,2.75f,3.2f}),
        ("weapon-rifle",1.8f,new[]{.22f,.16f,.28f,.9f},new[]{0,.75f,1.3f,1.8f,2.5f,3.2f}),
        ("weapon-shield",1.45f,new[]{.3f,.12f,.28f,.9f},new[]{0,.6f,1.15f,1.45f,2.25f,3.2f}),
        ("weapon-sword",1.8f,new[]{.3f,.12f,.28f,.9f},new[]{0,.95f,1.42f,1.8f,2.7f,3.2f}),
        ("weapon-water",2.12f,new[]{.3f,.12f,.28f,.9f},new[]{0,.8f,1.6f,2.12f,2.8f,3.2f}),
    };
    // The baked DEV stages' phase and name card (NativeIntroRules; not directed).
    private static readonly (string style,double duration,double nameAt)[] Dev={("dev-siren",9.0678,6.4261),("dev-emperor",7.8,5.95),("dev-matrix",8,6.25),("dev-ansatsuken",7.2,5.45)};
    public static IEnumerable<string> Run(string root)
    {
        var clips=Clips(root);var pose=new CapturedRig.Pose();
        NativeEntranceScript Script(string style){var sc=NativeEntranceDirector.Script(style);if(!sc.Ready&&clips.TryGetValue("intro-"+style,out var c))sc.Bind(c);Check(sc.Ready,style+" is ready");return sc;}
        // The right wrist under its shoulder (m, rig frame), the elbow's bend (rad) and the wrist (rig frame) at presentation time t.
        float Arm(NativeEntranceScript sc,float t,out float bend,out Vector3 wrist)
        {
            sc.Pose(t,pose,out _,out _);pose.World();var s=pose.WorldPosition[CapturedRig.RArm];var e=pose.WorldPosition[CapturedRig.RElbow];wrist=pose.WorldPosition[CapturedRig.RHand];
            bend=(float)Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(e-s),Vector3.Normalize(wrist-e)),-1,1));return wrist.Y-s.Y;
        }
        const float Hang=-.4f,Loose=35*(float)Math.PI/180,Rise=.05f,Speed=1.5f;float name=NativeEntranceScript.NameAt,end=NativeEntranceScript.Duration;
        // 1. The shared hero hold has both arms loose: the right elbow bends as little as the left, the arms mirrored.
        {
            var k=new NativeEntranceKeys().Key(0,NativeEntranceKeys.Hero());int Slot(int bone)=>NativeEntranceKeys.Joints+3*Array.IndexOf(NativeEntranceKeys.Bones,bone);
            float re=k.At(Slot(CapturedRig.RElbow),0),le=k.At(Slot(CapturedRig.LElbow),0);
            Check(re==NativeEntranceKeys.HeroElbow&&le==re&&Math.Abs(re)<.5f,"the hero hold's elbows are loose ("+re+", "+le+")");
            Check(k.At(Slot(CapturedRig.RArm),0)==k.At(Slot(CapturedRig.LArm),0)&&k.At(Slot(CapturedRig.RArm)+2,0)==-k.At(Slot(CapturedRig.LArm)+2,0),"the hero hold's arms are mirrored");
        }
        // 2. The keyed entrances: under the card the right arm hangs loose, and from the last beat before the card it never rises.
        float worstHang=-9,worstBend=0,worstRise=0,worstSpeed=0;string riseAt="",speedAt="";
        foreach(var style in Keyed)
        {
            var sc=Script(style);float from=style==NativeBowEntrance.Id?NativeBowEntrance.LowerTo:name-.6f,low=9;Vector3 last=Vector3.Zero;
            for(float t=from;t<=end+1e-4f;t+=1/120f)
            {
                float h=Arm(sc,t,out float bend,out var wrist);low=Math.Min(low,h);
                if(h-low>worstRise){worstRise=h-low;riseAt=style+" "+t.ToString("0.00");}
                if(t>from){float v=(wrist-last).Length()*120;if(v>worstSpeed){worstSpeed=v;speedAt=style+" "+t.ToString("0.00");}}last=wrist;
                if(t>=name){worstHang=Math.Max(worstHang,h);worstBend=Math.Max(worstBend,bend);Check(h<=Hang&&bend<=Loose,style+" lifts the right arm under the card at "+t.ToString("0.00")+" (wrist "+h.ToString("0.00")+" m under the shoulder, elbow "+(bend*180/Math.PI).ToString("0")+" deg)");}
            }
        }
        Check(worstRise<=Rise,"a keyed hold raises the right arm "+(worstRise*100).ToString("0.0")+" cm ("+riseAt+")");
        yield return $"entrance holds: the hero hold's elbows are loose ({NativeEntranceKeys.HeroElbow} rad, mirrored); under the card the {Keyed.Length} keyed entrances' right wrist hangs at least {-worstHang:0.00} m under the shoulder with the elbow at most {worstBend*180/Math.PI:0} deg, never rising more than {worstRise*100:0.0} cm from the last beat before the card ({riseAt})";
        // 3. The staff schools: the free hand comes down to hang over FreeHandFrom..FreeHandTo and stays there, clear of the body;
        // every other pack entrance is untouched (no rest at any time).
        float staffHang=-9,staffRise=0,staffClear=0,restStep=0;
        foreach(var style in Staffs)
        {
            var sc=Script(style);Check(NativePackIntro.FreeHandRests(style)&&sc is NativeWeaponEntrance w&&w.Pack!=null,style+" rests its free hand (a pack entrance)");
            Check(NativePackIntro.Rest(style,NativePackIntro.FreeHandFrom)==0&&NativePackIntro.Rest(style,NativePackIntro.FreeHandTo)==1,style+" rests over its window");
            float low=9;
            for(float t=NativePackIntro.FreeHandFrom;t<=end+1e-4f;t+=1/120f)
            {
                float h=Arm(sc,t,out float bend,out _);low=Math.Min(low,h);staffRise=Math.Max(staffRise,h-low);
                restStep=Math.Max(restStep,Math.Abs(NativePackIntro.Rest(style,t+1/120f)-NativePackIntro.Rest(style,t)));
                staffClear=Math.Max(staffClear,NativeIntroMotion.Clearance(style,pose,out _,1));
                if(t>=NativePackIntro.FreeHandTo){staffHang=Math.Max(staffHang,h);Check(h<=Hang&&bend<=Loose,style+" holds its free hand up at "+t.ToString("0.00")+" ("+h.ToString("0.00")+" m, "+(bend*180/Math.PI).ToString("0")+" deg)");}
            }
        }
        Check(staffRise<=Rise&&staffClear<=.03f,"a staff school's free hand rises "+(staffRise*100).ToString("0.0")+" cm or sinks "+(staffClear*1000).ToString("0")+" mm into the body as it rests");
        Check(worstSpeed<=Speed,"a keyed hold's right wrist jumps at "+worstSpeed.ToString("0.00")+" m/s ("+speedAt+")");
        Check(restStep<=.03f,"a staff school's free hand snaps down ("+restStep.ToString("0.000")+" of its rest in a 120 Hz frame)");
        int untouched=0;
        foreach(var p in NativePackIntros.All){if(NativePackIntro.FreeHandRests(p.Style))continue;for(float t=0;t<=end;t+=1/60f)Check(NativePackIntro.Rest(p.Style,t)==0,p.Style+" keeps its own clip");untouched++;}
        yield return $"entrance holds: the {Staffs.Length} staff schools' free hand eases down over {NativePackIntro.FreeHandFrom}-{NativePackIntro.FreeHandTo} s and hangs at least {-staffHang:0.00} m under the shoulder to the end, never rising ({staffRise*100:0.0} cm), at most {staffClear*1000:0} mm into the body, easing in by at most {restStep:0.000} a 120 Hz frame; the keyed holds' right wrist moves at most {worstSpeed:0.00} m/s ({speedAt}); the other {untouched} pack entrances keep their clip";
        // 4. The timing marks: every directed entrance keeps its hit, slow-motion beat and shot cuts; every entrance its phase and card.
        int marks=0;
        foreach(var (style,hit,slow,cuts) in Marks)
        {
            var sc=Script(style);
            Check(sc.Hit==hit&&sc.SlowRate==slow[0]&&sc.SlowHold==slow[1]&&sc.SlowRecover==slow[2]&&sc.SlowCatchUp==slow[3],style+" keeps its hit and slow-motion beat ("+sc.Hit+")");
            Check(sc.Shots.Select(s=>s.At).SequenceEqual(cuts),style+" keeps its shot cuts ("+string.Join(",",sc.Shots.Select(s=>s.At))+")");
            if(NativeIntroMotion.WeaponIntro(style)>=0)Check(NativeIntroMotion.Hit(style)==hit,style+" keeps its sting's accent");
            Check(NativeIntroRules.Duration(style)==4.8&&NativeIntroRules.NameAt(style)==3.2&&NativeIntroRules.OverlayDuration(style)==4.8,style+" keeps 4.8 s and the 3.2 s card");marks++;
        }
        Check(Marks.Select(m=>m.style).Concat(new[]{CyberLordSpec.Entrance}).OrderBy(s=>s,StringComparer.Ordinal).SequenceEqual(NativeEntranceDirector.Styles.OrderBy(s=>s,StringComparer.Ordinal)),"every directed entrance is pinned (the Cyber Lord's to its phase)");
        Check(NativeIntroRules.Duration(CyberLordSpec.Entrance)==4.8&&NativeIntroRules.NameAt(CyberLordSpec.Entrance)==3.2,"the Hardlight Boot keeps 4.8 s and the 3.2 s card");
        foreach(var (style,duration,nameAt) in Dev)Check(NativeIntroRules.Duration(style)==duration&&NativeIntroRules.NameAt(style)==nameAt,style+" keeps its phase and card");
        Check(NativeEntranceScript.Duration==4.8f&&NativeEntranceScript.NameAt==3.2f&&NativeEntranceScript.Settle==4.45f,"the directed phase, card and settle");
        yield return $"entrance timing marks unchanged: {marks} directed entrances keep their hit, slow-motion beat and shot cuts (the weapon signatures their sting's accent), all 4.8 s with the 3.2 s card; the Hardlight Boot and the {Dev.Length} DEV stages keep their phase and card";
    }
    private static Dictionary<string,NativeEntranceClip> Clips(string root)
    {
        using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceMotion.json")));
        using var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString()));using var zip=new GZipStream(bytes,CompressionMode.Decompress);using var reader=new StreamReader(zip);
        using var doc=JsonDocument.Parse(reader.ReadToEnd());var map=new Dictionary<string,NativeEntranceClip>();
        foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())
        {
            string key=c.GetProperty("key").GetString();if(!key.StartsWith("intro-weapon-"))continue;
            map[key]=new NativeEntranceClip(c.GetProperty("values").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray(),c.GetProperty("roots").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray(),c.GetProperty("frames").GetInt32(),(float)c.GetProperty("duration").GetDouble());
        }
        return map;
    }
}
