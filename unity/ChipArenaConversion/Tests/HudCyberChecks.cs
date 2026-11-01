#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The cyber HUD restyle (owner 2026-10-12: "Also make the Hud match the cyber theme~", and "Show other resources under
    // health too above player (only visible to player, other players can still see health)"): Core/NativeHudCyber's rules
    // against the shared palette (Runtime/NativeCyberTheme.cs, read from its source so no colour is restated here): every
    // HUD text tone keeps 4.5:1 over the holographic panel above any scene, ally and enemy and the semantic damage colours
    // stay apart, the segment math covers every value exactly, the event envelopes start and end where the HUD needs them
    // (and stop with Reduced effects), the own-pilot rows, fades and clocks follow the replicated state, and the bottom row
    // fits every window from 1280x720 to 3440x1440 at every interface scale. Source scans pin the Runtime hooks.
    internal static class HudCyberChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("HUD cyber: "+what);count++;}
        private static bool Near(double a,double b,double e=1e-4)=>Math.Abs(a-b)<=e;
        private static double Hue(int rgb)
        {
            double r=((rgb>>16)&255)/255.0,g=((rgb>>8)&255)/255.0,b=(rgb&255)/255.0,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min;if(d<=0)return 0;
            double h=max==r?(g-b)/d%6:max==g?(b-r)/d+2:(r-g)/d+4;h*=60;return h<0?h+360:h;
        }
        private static double HueGap(int a,int b){double d=Math.Abs(Hue(a)-Hue(b))%360;return Math.Min(d,360-d);}
        public static void Run(string root,ConversionContent data)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            // 1. The palette, read from its one definition.
            var theme=R("NativeCyberTheme.cs");var hex=new Dictionary<string,int>();
            foreach(Match m in Regex.Matches(theme,@"(\w+)Hex=0x([0-9a-fA-F]{6})"))hex[m.Groups[1].Value]=Convert.ToInt32(m.Groups[2].Value,16);
            foreach(var k in new[]{"Cyan","CyanSoft","CyanDeep","Magenta","Violet","Void","Navy","Indigo","Panel","Edge","Ink","Muted","Dim","Danger","Warn","Good"})Check(hex.ContainsKey(k),"palette has "+k);
            Check(NativeHudCyber.Contrast(0xffffff,0x000000)>20.9&&Near(NativeHudCyber.Contrast(0x777777,0x777777),1)&&NativeHudCyber.Over(0xffffff,.5f,0x000000)==0x808080,"WCAG contrast and sRGB compositing");

            // 2. Every text tone the HUD draws on its panels keeps AA contrast over navy glass (and the indigo rows/keycaps)
            // at the panel opacity, whatever the scene behind is (pure black to pure white).
            // Navy carries every tone; the indigo rows and keycaps carry ink, muted, the cyans and good only.
            var onFill=new Dictionary<string,string[]>{{"Navy",new[]{"Ink","Muted","Cyan","CyanSoft","Magenta","Danger","Warn","Good"}},{"Indigo",new[]{"Ink","Muted","Cyan","CyanSoft","Good"}}};
            foreach(var (fill,tones) in onFill.Select(k=>(k.Key,k.Value)))foreach(var t in tones)
            {
                double c=NativeHudCyber.PanelContrast(hex[t],hex[fill],NativeHudCyber.PanelAlpha);
                Check(c>=NativeHudCyber.TextContrast,t+" on "+fill+" glass keeps 4.5:1 over any scene ("+c.ToString("0.00",CultureInfo.InvariantCulture)+")");
            }
            // Decoration-only tones really are below the text bar (so the scan below matters).
            foreach(var t in new[]{"Violet","Dim","Edge","CyanDeep"})Check(NativeHudCyber.PanelContrast(hex[t],hex["Navy"],NativeHudCyber.PanelAlpha)<NativeHudCyber.TextContrast,t+" is decoration only");
            string[] hudFiles={"NativeFightHud.cs","NativeCombatHud.cs","NativePveUI.cs","NativePveBanners.cs","NativeMatchOverlays.cs","NativeReplay.cs","NativeHudChrome.cs"};
            foreach(var f in hudFiles)foreach(var statement in R(f).Split(';'))
                if(statement.Contains("Text("))foreach(var t in new[]{"Violet","Dim","Edge","CyanDeep"})Check(!statement.Contains("NativeCyberTheme."+t+"(")&&!statement.Contains("C."+t+"("),"no "+t+" text in "+f);

            // 3. Sides and semantics stay apart: my side cyan against enemy danger (hue and brightness), and the meaning-bearing
            // colours the restyle keeps (damage / heal numbers, the hit marker's headshot, fire and poison) far from each other
            // and from the cyan / magenta accents.
            Check(HueGap(hex["Cyan"],hex["Danger"])>=120&&NativeHudCyber.Contrast(hex["Cyan"],hex["Danger"])>=1.4,"ally cyan and enemy danger read apart");
            Check(HueGap(NativeCombatNumbers.DamageColor,NativeCombatNumbers.HealColor)>=90,"damage and heal numbers");
            int[] marker={NativeHitmarker.Headshot,NativeHitmarker.FireColor,NativeHitmarker.PoisonColor};
            for(int i=0;i<marker.Length;i++)for(int j=i+1;j<marker.Length;j++)Check(HueGap(marker[i],marker[j])>=20,"hit marker colours "+i+"/"+j);
            foreach(int c in new[]{NativeCombatNumbers.DamageColor,NativeCombatNumbers.HealColor,NativeHitmarker.FireColor,NativeHitmarker.PoisonColor})
                Check(HueGap(c,hex["Cyan"])>=30&&HueGap(c,hex["Magenta"])>=30,"semantic colour #"+c.ToString("x6")+" is not a theme accent");
            var overlays=R("NativeMatchOverlays.cs");Check(overlays.Contains("TeamHex={0xff6575,0x69a6ff,0x7ed78a,0xf5db70,0xba94f5,0xffa565,0x65dce0,0xf18dcc}"),"team colours unchanged");

            // 4. Segments: nice steps within the limit, exact spans and fills (the fills' weighted sum is the value).
            Check(NativeHudCyber.SegmentStep(100,10)==10&&NativeHudCyber.SegmentCount(100,10)==10,"100 HP in tens");
            Check(NativeHudCyber.SegmentStep(7,16)==1&&NativeHudCyber.SegmentStep(48,16)==4&&NativeHudCyber.SegmentCount(48,4)==12,"magazines: 7 rounds, 48 in fours");
            Check(NativeHudCyber.SegmentStep(150,10)==15&&NativeHudCyber.SegmentStep(220,10)==25&&NativeHudCyber.SegmentCount(220,25)==9,"chipped HP keeps a partial last cell");
            Check(NativeHudCyber.SegmentStep(0,10)==1&&NativeHudCyber.SegmentCount(0,1)==1&&NativeHudCyber.SegmentStep(float.NaN,10)==1,"degenerate bars");
            var rng=new Random(17);
            for(int n=0;n<400;n++)
            {
                float max=(float)(rng.NextDouble()*3000+1),value=(float)(rng.NextDouble()*max);int limit=1+rng.Next(20);float step=NativeHudCyber.SegmentStep(max,limit);int cells=NativeHudCyber.SegmentCount(max,step);
                Check(cells<=Math.Max(limit,(int)Math.Ceiling(max/step)),"count within the limit");double sum=0,covered=0;float last=0;
                for(int i=0;i<cells;i++){NativeHudCyber.SegmentSpan(i,step,max,out float a,out float b);Check(Near(a,last,1e-5)&&b>=a,"spans tile the bar");last=b;float f=NativeHudCyber.SegmentFill(i,step,max,value);Check(f>=0&&f<=1,"fill in range");sum+=f*(b-a)*max;covered+=(b-a);}
                Check(Near(last,1,1e-4)&&Near(covered,1,1e-4)&&Math.Abs(sum-value)<=max*1e-4,"segment fills add up to the value");
            }
            Check(NativeHudCyber.Low(.3f)&&NativeHudCyber.Low(.01f)&&!NativeHudCyber.Low(.31f)&&!NativeHudCyber.Low(0),"low health from 30% (alive)");

            // 5. Event envelopes: the ready flare, the glitch burst on the Siren split's banded window, the low-health breath
            // (constant with Reduced effects).
            Check(Near(NativeHudCyber.Flare(0),1)&&NativeHudCyber.Flare(NativeHudCyber.FlareLife)==0&&NativeHudCyber.Flare(-.1f)==0,"flare starts full, ends at its life");
            float prev=2;for(float a=0;a<NativeHudCyber.FlareLife;a+=.01f){float f=NativeHudCyber.Flare(a);Check(f<=prev,"flare only fades");prev=f;}
            Check(NativeHudCyber.Glitching(0)&&!NativeHudCyber.Glitching(NativeHudCyber.GlitchLife)&&!NativeHudCyber.Glitching(-.01f),"glitch burst window");
            for(float a=0;a<NativeHudCyber.GlitchLife;a+=.005f){float k=NativeHudCyber.GlitchCycle(a);Check(k>=.65f&&k<.72f,"burst maps into the split's steps");}
            for(float t=0;t<5;t+=.05f){float b=NativeHudCyber.Breath(t,false);Check(b>=.55f-1e-4&&b<=1+1e-4&&NativeHudCyber.Breath(t,true)==1,"breath range; still with Reduced effects");}
            var chrome=R("NativeHudChrome.cs");
            Check(Regex.IsMatch(chrome,@"public static void Glitch\([^)]*\)\s*\{\s*if\(reduced\|\|")&&Regex.IsMatch(chrome,@"public static void Slice\([^)]*\)\s*\{\s*if\(reduced\|\|")&&chrome.Contains("float f=reduced?0:R.Flare(age);if(f<=0)return;"),"glitch, slice and flare draw nothing with Reduced effects");
            var fight=R("NativeFightHud.cs");
            Check(fight.Contains("bool low=NativeHudCyber.Low(hp),reduced=prefs.reducedEffects;")&&fight.Contains("NativeHudChrome.Slice(r,hit,1,reduced,tone);")&&fight.Contains("bool reduced=prefs.reducedEffects;")&&fight.Contains("NativeHudChrome.Flare(r,since,1,reduced,"),"the fight HUD passes Reduced effects to every accent");

            // 6. The trail and the event latches.
            var trail=new NativeHudTrail();Check(Near(trail.Step(10,.8f),.8f),"trail starts at the value");
            float v=trail.Step(10.01f,.5f);Check(Near(v,.8f),"a loss leaves the trail behind");
            float now=10.01f;for(int i=0;i<12;i++){now+=.016f;v=trail.Step(now,.5f);}Check(Near(v,.8f),"held for .22 s");
            for(int i=0;i<10;i++){now+=.016f;v=trail.Step(now,.5f);}Check(v<.8f&&v>.5f,"then drains");
            for(int i=0;i<60;i++){now+=.016f;v=trail.Step(now,.5f);}Check(Near(v,.5f),"down to the value");Check(Near(trail.Step(now+.016f,.9f),.9f),"a gain snaps it up");
            Check(Near(trail.Step(1,.3f),.3f),"a clock running back restarts it");
            var ev=new NativeHudEvents();Check(ev.Drop(0,1,80)>=100&&ev.Drop(0,1.1f,90)>=100,"first sample and gains are no hit");
            Check(Near(ev.Drop(0,1.2f,70),0)&&Near(ev.Drop(0,1.5f,70),.3f),"a drop is a hit, aged since");
            Check(ev.Rise(2,1,true)>=100&&ev.Rise(2,1.1f,false)>=100&&Near(ev.Rise(2,1.2f,true),0)&&Near(ev.Rise(2,1.4f,true),.2f),"ready latches on the rising edge only");
            Check(ev.Drop(0,.5f,10)>=100,"a new match clock starts the slot over");

            // 7. The own pilot's overhead rows follow the weapon class and the replicated clocks; fades.
            Check(NativeHudCyber.SelfRows("melee",false,false,false,false)==NativeHudCyber.Rows.Stamina&&NativeHudCyber.SelfRows("magic",false,false,false,false)==NativeHudCyber.Rows.Mana,"melee stamina, magic mana");
            Check(NativeHudCyber.SelfRows("ranged",false,false,false,false)==NativeHudCyber.Rows.Ammo&&NativeHudCyber.SelfRows("ranged",true,false,false,false)==(NativeHudCyber.Rows.Ammo|NativeHudCyber.Rows.Reload),"ranged magazine, reload sweep");
            Check((NativeHudCyber.SelfRows("ranged",false,true,false,false)&NativeHudCyber.Rows.Heat)!=0&&(NativeHudCyber.SelfRows("ranged",false,false,true,false)&NativeHudCyber.Rows.Vent)!=0&&(NativeHudCyber.SelfRows("melee",false,false,false,true)&NativeHudCyber.Rows.Charge)!=0,"overdrive heat, vent, DEV charge");
            float at=20,until=at+NativeRangedRules.OverdriveDuration,vent=until+NativeRangedRules.VentDuration;
            Check(Near(NativeHudCyber.Heat(at,at,until),0)&&Near(NativeHudCyber.Heat(at+2,at,until),.5f)&&Near(NativeHudCyber.Heat(until+1,at,until),1)&&NativeHudCyber.Heat(5,0,0)==0,"heat over the 4 s buff");
            Check(Near(NativeHudCyber.Cooling(until,until,vent),1)&&Near(NativeHudCyber.Cooling(until+.4f,until,vent),.5f)&&Near(NativeHudCyber.Cooling(vent,until,vent),0)&&NativeHudCyber.Cooling(5,0,0)==0,"vent cools over .8 s");
            Check(Near(NativeHudCyber.Reload(1.5f,1,2),.5f)&&NativeHudCyber.Reload(3,1,2)==1&&NativeHudCyber.Reload(3,0,0)==0,"reload sweep");
            Check(NativeHudCyber.IdleFade(10,false)==1&&NativeHudCyber.IdleFade(NativeHudCyber.IdleAfter*.9f,true)==1&&Near(NativeHudCyber.IdleFade(NativeHudCyber.IdleAfter+NativeHudCyber.IdleFadeTime+1,true),NativeHudCyber.IdleAlpha),"idle rows dim only when full and still");
            Check(Near(NativeHudCyber.AimFade(-10,-4,20,8,0,0),NativeHudCyber.AimAlpha)&&Near(NativeHudCyber.AimFade(200,200,20,8,0,0),1),"the bar fades over the aim point, not away from it");
            prev=0;for(float d=0;d<80;d+=2){float f=NativeHudCyber.AimFade(d,-4,30,8,0,0);Check(f>=prev-1e-5,"fade eases with distance");prev=f;}
            var combat=R("NativeCombatHud.cs");
            Check(combat.Contains("p==arena.Local?p:null);}")&&combat.Contains("(prefs.firstPerson||presentation.FirstPerson)&&p==arena.ViewPilot")&&combat.Contains("NativeHudCyber.AimFade(")&&combat.Contains("U.W/2,U.H/2"),"resources only over this client's own pilot, never in first person, fading at the aim point");
            Check(combat.Contains("NativeRangedRules.Overdrive(t,p.OverdriveAt,p.OverdriveUntil)")&&combat.Contains("NativeRangedRules.Vent(t,p.OverdriveUntil,p.VentUntil)")&&combat.Contains("NativeHudCyber.Reload(t,p.ReloadAt,p.ReloadEnd)"),"the rows read the replicated Overdrive and reload clocks");

            // 8. The bottom row fits every window and interface scale: the vitals (256) and the widest card row any weapon
            // shows (Consolas / Liberation Mono advance .6 em, as the cards' mono text; a 5% margin for the font).
            float Mono(string s,float size,float tracking=0)=>s.Length*.6f*size+tracking*size*Math.Max(0,s.Length-1);
            float Card(string key,string state)
            {
                int dot=key.IndexOf(" · ",StringComparison.Ordinal);string cap=dot>0?key.Substring(0,dot):null,name=dot>0?key.Substring(dot+3):key;
                float kw=cap!=null?Math.Max(14,Mono(cap,8.68f)+8):0,w=Math.Max(kw+(kw>0?6:0)+Mono(name,10,.06f),Mono(state,14,.1f))+22+14;return Math.Max(84,w)*1.05f;
            }
            float widest=0;string widestSpec=null;
            foreach(var w in data.weapons)
            {
                float row=Card("RMB · "+(w.id=="ansatsuken"?"PARRY":"BLOCK"),"SHIELD OUT")+Card("SHIFT · "+(w.@class=="melee"?"ROLL":"DODGE"),"ROLLING")+12;
                foreach(var slot in new[]{"q","e"}){var d=data.Ability(w.id,slot);if(d!=null)row+=Card(slot.ToUpperInvariant()+" · "+d.name.ToUpperInvariant(),"LOW STAMINA")+6;}
                if(row>widest){widest=row;widestSpec=w.id;}
            }
            foreach(var (sw,sh) in new[]{(1280,720),(1600,900),(1920,1080),(2560,1440),(3440,1440)})foreach(float scale in new[]{.75f,1,1.25f,1.5f})
            {
                var canvas=NativeUiMotion.Measure(sw,sh,scale);
                Check(NativeHudCyber.RowFits(canvas.Width,256,widest),"bottom row fits "+sw+"x"+sh+" at "+scale+" ("+widestSpec+" "+widest.ToString("0",CultureInfo.InvariantCulture)+" of "+canvas.Width.ToString("0",CultureInfo.InvariantCulture)+")");
                Check(Math.Min(560,canvas.Width*.4f)+24<canvas.Width-2*(24+220),"the Siren bar clears the money panel at "+sw+"x"+sh+" x"+scale);
            }

            // 9. The Runtime draws the cyber HUD everywhere it was asked to: palette in, the old orange out of the HUD files,
            // the cards' flags generic for new ability states.
            foreach(var f in new[]{"NativeFightHud.cs","NativePveUI.cs","NativePveBanners.cs","NativeReplay.cs"})Check(!R(f).Contains("0xff6b35")&&R(f).Contains("NativeCyberTheme."),f+" is on the cyber palette");
            Check(fight.Contains("private struct Card {public string Key,State,Sub,Icon;public bool Ready,Active,Recast,Low;public float Dial;}")&&fight.Contains("private float CardAt(Card c,float right,float bottom,float since)"),"one generic card for every ability state");
            Check(fight.Contains("NativeHudChrome.Bar(bar,hp*maxHp,maxHp,10,tone,")&&fight.Contains("float trail=DamageTrail(p,hp);"),"segmented HP with its damage trail");
            Check(!Regex.IsMatch(chrome,@"0x[0-9a-fA-F]{6}")&&Regex.Matches(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeHudCyber.cs")),@"0x[0-9a-fA-F]{6}\b").All(m=>m.Value=="0x000000"||m.Value=="0xffffff"),"the chrome kit and its rules restate no colour (all from NativeCyberTheme)");
            Console.WriteLine("PASS HUD cyber: "+count+" checks (palette contrast over glass, sides and semantic colours, segment math, envelopes and Reduced effects, trail and event latches, own-pilot rows and fades, bottom-row fit at 20 window/scale cases, Runtime hooks)");
        }
    }
}
#endif
