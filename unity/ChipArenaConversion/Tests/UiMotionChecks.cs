using System;
using System.Collections.Generic;
using ChipArena.UnityConversion;

// Engine-free checks for the overlay timing curves and the shared UI scale (NativeUiMotion). Rendering is not exercised.
internal static class UiMotionChecks
{
    private static void Near(float actual,float expected,float tolerance,string what){if(!(Math.Abs(actual-expected)<=tolerance))throw new Exception(what+": "+actual+" expected "+expected);}
    public static IEnumerable<string> Run(string root)
    {
        // CSS reference values: ease(.5) = .8024, ease-out(.5) = .6847, ease-in-out(.5) = .5; end points are exact.
        Near(NativeUiMotion.Ease(.5f),.8024f,.001f,"CSS ease");Near(NativeUiMotion.EaseOut(.5f),.6847f,.001f,"CSS ease-out");Near(NativeUiMotion.EaseInOut(.5f),.5f,.001f,"CSS ease-in-out");
        Near(NativeUiMotion.Bezier(.16f,.84f,.24f,1,0),0,0,"bezier start");Near(NativeUiMotion.Bezier(.16f,.84f,.24f,1,1),1,0,"bezier end");
        float last=0;for(int i=1;i<=100;i++){float v=NativeUiMotion.Bezier(.2f,.9f,.25f,1,i/100f);if(v<last-1e-5f)throw new Exception("count-tick curve not monotonic");last=v;}
        yield return "CSS cubic-bezier curves match the browser reference values";
        Near(NativeUiMotion.Keys(.5f,new[]{0,.46f,.76f},new[]{.96f,.70f,0}),.70f*(1-(.5f-.46f)/.30f),.0001f,"keyframe interpolation");
        Near(NativeUiMotion.Progress(1.2f,1,.4f),.5f,1e-6f,"animation progress");Near(NativeUiMotion.Progress(0,1,.4f),0,0,"before delay");
        yield return "Keyframe tracks and delayed progress sample the authoritative clock";
        void Layout(int w,int h,float scale,float width,float height,float safe)
        {var l=NativeUiMotion.Measure(w,h);Near(l.Scale,scale,1e-4f,w+"x"+h+" scale");Near(l.Width,width,.01f,w+"x"+h+" width");Near(l.Height,height,.01f,w+"x"+h+" height");Near(l.SafeWidth,safe,.01f,w+"x"+h+" safe band");Near(l.SafeX,(width-safe)/2,.01f,w+"x"+h+" safe x");}
        Layout(1600,900,1,1600,900,1600);Layout(1920,1080,1.2f,1600,900,1600);Layout(2560,1440,1.6f,1600,900,1600);Layout(3840,2160,2.4f,1600,900,1600);
        Layout(3440,1440,1.6f,2150,900,1600);Layout(2560,1080,1.2f,2133.33f,900,1600);Layout(1920,1200,1.2f,1600,1000,1600);Layout(1280,1024,.8f,1600,1280,1600);
        if(NativeUiMotion.Measure(640,480).Scale<.6f-1e-6f)throw new Exception("UI scale must not drop below .6");
        yield return "UI scale keeps a 900-unit canvas from 720p to 4K and widens to 2150 units at 3440x1440 (21:9)";
        Near((float)NativeIntroRules.NameAt("dev-siren"),6.4261f,1e-6f,"Siren name time");Near((float)NativeIntroRules.OverlayDuration("dev-siren"),7.2678f,1e-6f,"Siren overlay length");
        if(NativeIntroRules.StyleName("weapon-daggers")!="Twin Phantom"||NativeIntroRules.StyleName("neon-drop")!="Weapon Signature")throw new Exception("Intro style names");
        yield return "Intro overlay uses the source name card times and style names";
        // Every card the game can show has the source's exported description and stat lines.
        using(var text=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/Conversion/Resources/ChipText.json"))))
        using(var game=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/Conversion/Resources/GameContent.json"))))
        using(var pve=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/Conversion/Resources/PveContent.json"))))
        {
            var ids=new HashSet<string>();foreach(var c in text.RootElement.GetProperty("chips").EnumerateArray()){ids.Add(c.GetProperty("id").GetString());if(string.IsNullOrWhiteSpace(c.GetProperty("description").GetString()))throw new Exception("Empty card text");}
            if(text.RootElement.GetProperty("commit").GetString()!=game.RootElement.GetProperty("sourceCommit").GetString())throw new Exception("Card text is from another source commit");
            int n=0;foreach(var c in game.RootElement.GetProperty("chips").EnumerateArray()){n++;if(!ids.Contains(c.GetProperty("id").GetString()))throw new Exception("Missing card text "+c.GetProperty("id").GetString());}
            foreach(var c in pve.RootElement.GetProperty("moneyChips").EnumerateArray()){n++;if(!ids.Contains(c.GetProperty("id").GetString()))throw new Exception("Missing PvE card text "+c.GetProperty("id").GetString());}
            if(text.RootElement.GetProperty("sirenTrack").GetArrayLength()!=4)throw new Exception("Siren chip track must hold four chips");
            yield return "Card text covers all "+n+" catalog and PvE chips plus the four-chip Siren track";
        }
    }
}
