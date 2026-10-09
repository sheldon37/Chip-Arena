using System;
using System.Collections.Generic;
using ChipArena.UnityConversion;

// Engine-free checks for the entrance presentation (Core/NativeIntroRules.cs): which weapon the bare intro rig holds
// (player-intros.ts builds one only for a weapon intro, so the L33T Throne's stage staff is the only staff), the staged
// fallback for a style without an entrance, and the entrance-stage light conversion (NativeEntranceLight) that keeps the
// Siren cell lit as the source lights it once Linear colour space is pinned. Unity's rendering is not exercised.
internal static class IntroPresentationChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Intro presentation: "+what);}
    private static readonly string[] WeaponIntros={"weapon-sword","weapon-daggers","weapon-shield","weapon-greatsword","weapon-pistol","weapon-rifle","weapon-machinegun","weapon-fire","weapon-water","weapon-lightning","weapon-darkness"};
    public static IEnumerable<string> Run()
    {
        var styles=NativeIntroRules.Entrances;Check(styles.Length==20,"twenty entrances");
        var seen=new HashSet<string>();foreach(var s in styles)Check(seen.Add(s),"duplicate entrance "+s);
        // Hands: only a weapon intro holds a weapon, and it is that intro's own weapon model.
        int armed=0;
        foreach(var s in styles)
        {
            string held=NativeIntroRules.IntroWeapon(s);bool weapon=Array.IndexOf(WeaponIntros,s)>=0;
            Check(weapon?held==s:held==null,s+" holds "+(held??"nothing"));if(held!=null)armed++;
        }
        Check(armed==WeaponIntros.Length,"eleven weapon intros are armed");
        foreach(var s in new[]{"dev-matrix","dev-emperor","dev-ansatsuken","dev-siren","neon-drop","pulse-wave"})Check(NativeIntroRules.IntroWeapon(s)==null,s+" must keep its hands empty");
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
        yield return "intro hands: 11 weapon intros hold their own weapon, 9 entrances (L33T Throne, Emperor, Rain Warrior, Siren, poses) hold none";
        yield return "intro staging: 20 entrances stage themselves, unknown styles stage neon-drop with unchanged timing";
        yield return "entrance lights: "+lights+" conversions apply the source's diffuse light in Linear, linear-intensity and Gamma modes; Siren bulb no longer blown out";
    }
    private static void Near(double a,double b,string what){if(!(Math.Abs(a-b)<=1e-9*Math.Max(1,Math.Abs(b))))throw new Exception("Intro presentation: "+what+" "+a+" vs "+b);}
}
