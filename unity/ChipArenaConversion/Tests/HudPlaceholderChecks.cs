#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Batch 3 of the early-build placeholders (owner 2026-10-10): the hit marker's proc dot and tick elements, the sprint
    // tag, the L33t control box, the DEV fighter and Siren HUDs, replayed through Core/NativeHudRules.cs against what the
    // pinned source itself shows (Tests/hud-placeholder-parity.json, exported by tools/export_hud_fixtures.mjs from
    // client/hitmarker.ts, sprint-hud.ts, siren-hud.ts and main.ts drawHud), the cascaded style.css / match-hud.css values
    // those constants port, and scans of the runtime hooks (fight HUD, combat HUD, home menu, sandbox panel) so none of
    // them can silently drop back to the placeholder.
    internal static class HudPlaceholderChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("HUD placeholder parity: "+what);count++;}
        private static void Near(double a,double b,string what,double tolerance=2e-4){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a.ToString(CultureInfo.InvariantCulture)+" != "+b.ToString(CultureInfo.InvariantCulture));}
        private static string S(JsonElement e,string name)=>e.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
        private static double D(JsonElement e,string name)=>e.GetProperty(name).GetDouble();
        private static bool B(JsonElement e,string name)=>e.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.True;
        private static string Css(JsonElement css,string selector,string property)=>css.GetProperty(selector).TryGetProperty(property,out var v)?v.GetString():null;
        private static string Px(float v)=>v.ToString(CultureInfo.InvariantCulture)+"px";
        private static string Hex(int rgb)=>"#"+rgb.ToString("x6");
        private static string Hex(int rgb,float alpha)=>Hex(rgb)+((int)Math.Round(alpha*255)).ToString("x2");
        private static string Short(int rgb)=>Hex(rgb).Length==7&&rgb==0xffffff?"#fff":Hex(rgb);
        private static string Em(float v)=>v.ToString(CultureInfo.InvariantCulture).TrimStart('0')+"em";

        // The fixture's source ids as Unity seat / minion ids: me 'a', enemy 'b' (blue), ally 'c' (red, as me), a PvE add and a summon.
        private static int Id(string id)=>id=="a"?1:id=="b"?2:id=="c"?3:id!=null&&id.StartsWith("add-",StringComparison.Ordinal)?1000+int.Parse(id.Substring(4),CultureInfo.InvariantCulture):id!=null&&id.StartsWith("summon-",StringComparison.Ordinal)?2000+int.Parse(id.Substring(7),CultureInfo.InvariantCulture):0;
        // A source hit / minionHit as the Unity event the native match emits: a pilot's burn/poison tick is its own kind,
        // a headshot is Headshot, any other damage is Hit with its damage kind in Detail.
        private static ArenaEvent Event(JsonElement e)
        {
            string message=S(e,"message"),type=S(e,"type");bool tick=type=="hit"&&(message=="burn"||message=="poison");
            var kind=tick?(message=="burn"?EventKind.BurnTick:EventKind.PoisonTick):B(e,"headshot")&&type=="hit"?EventKind.Headshot:EventKind.Hit;
            return new ArenaEvent{Id=e.GetProperty("id").GetInt32(),Kind=kind,Source=Id(S(e,"source")),Target=Id(S(e,"target")),Amount=(float)D(e,"amount"),Time=(float)D(e,"time"),Detail=tick?null:message};
        }

        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/hud-placeholder-parity.json")));var json=document.RootElement;var css=json.GetProperty("css");var from=json.GetProperty("cssFrom");
            Check(json.GetProperty("source").EnumerateObject().GetEnumerator().MoveNext(),"pinned source blobs recorded");

            // 1. Hit marker: every source track, frame by frame (shown, red, dot, opacity, scale, element icons in order).
            int frames=0;
            foreach(var track in json.GetProperty("hitmarker").EnumerateArray())
            {
                string name=S(track,"name"),mode=S(track,"mode");bool pve=B(track,"pve");var marker=new NativeHitmarker();var events=new List<ArenaEvent>();
                NativeHitmarker.Target Classify(int id)=>id==1?NativeHitmarker.Target.Ally:id==2?NativeHitmarker.Target.Enemy:id==3?(mode=="teams"?NativeHitmarker.Target.Ally:NativeHitmarker.Target.Enemy):id>=1000&&id<2000?NativeHitmarker.Target.Add:NativeHitmarker.Target.Other;
                var all=new List<(int gen,ArenaEvent e)>();foreach(var e in track.GetProperty("events").EnumerateArray())all.Add((e.GetProperty("gen").GetInt32(),Event(e)));
                foreach(var f in track.GetProperty("frames").EnumerateArray())
                {
                    double time=D(f,"time");int gen=f.GetProperty("gen").GetInt32();
                    events.Clear();foreach(var (g,e) in all)if(g==gen&&e.Time<=time+1e-6)events.Add(e);
                    var m=marker.Update(events,(float)time,gen,1,pve,Classify);string at=name+" @"+time.ToString(CultureInfo.InvariantCulture);
                    Check(m.Shown==!B(f,"hidden"),at+" shown");Check(m.Headshot==B(f,"headshot"),at+" headshot");Check(m.Dot==B(f,"dot"),at+" dot");
                    Near(m.Opacity,D(f,"opacity"),at+" opacity");Near(m.Scale,D(f,"scale"),at+" scale");
                    var icons=f.GetProperty("elements");Check(icons.GetArrayLength()==m.Elements,at+" element count");
                    int k=0;foreach(var icon in icons.EnumerateArray()){int element=k==0?m.First:m.Second;Check(icon.GetString()==(element==NativeHitmarker.Fire?"fire":"poison"),at+" element "+k);k++;}
                    frames++;
                }
            }
            Check(frames>=60,"hit marker frames replayed: "+frames);
            // Unity spells some proc kinds with a hyphen; a pilot hit at or under .05 (the source never emits one) and a
            // non-damage kind mark nothing; my ally's hits and other sources' hits mark nothing.
            foreach(string kind in new[]{"drain-dash","block-pulse","chaos-bomb","corpse-party","dash-blast"})
            {var mk=new NativeHitmarker();var m=mk.Update(new List<ArenaEvent>{new ArenaEvent{Id=1,Kind=EventKind.Hit,Source=1,Target=2,Amount=5,Time=10,Detail=kind}},10.01f,0,1,false,id=>NativeHitmarker.Target.Enemy);Check(m.Visible&&m.Dot,"hyphenated proc "+kind);}
            {var mk=new NativeHitmarker();Check(!mk.Update(new List<ArenaEvent>{new ArenaEvent{Id=1,Kind=EventKind.Hit,Source=1,Target=2,Amount=.05f,Time=10,Detail="direct"}},10.01f,0,1,false,id=>NativeHitmarker.Target.Enemy).Shown,"pilot hit at the .05 floor");}
            {var mk=new NativeHitmarker();Check(!mk.Update(new List<ArenaEvent>{new ArenaEvent{Id=1,Kind=EventKind.Impact,Source=1,Target=2,Amount=9,Time=10}},10.01f,0,1,false,id=>NativeHitmarker.Target.Enemy).Shown,"impact is no hit");}
            Check(NativeArcadeHud.Hit==" "+NativeArcadeHud.HitWord&&NativeArcadeHud.Hits==" "+NativeArcadeHud.HitsWord,"combo words drawn after one space");
            Check(NativeHitmarker.CrispLife==.34f&&NativeHitmarker.DotLife==.20f&&NativeHitmarker.Hold==.22f&&NativeHitmarker.Fresh==.4f&&NativeHitmarker.ElementMerge==.5f&&NativeHitmarker.ElementLife==.7f,"hitmarker.ts windows");

            // The marker's look: 42 px box, white / #ff394d, the stroke and tip spans of the 100-unit viewBox, icons and shadow.
            var svg=json.GetProperty("svg");
            Check(Css(css,".combat-hitmarker","width")==Px(NativeHitmarker.Box)&&Css(css,".combat-hitmarker","height")==Px(NativeHitmarker.Box),"marker box");
            Check(Css(css,".combat-hitmarker","color")==Short(NativeHitmarker.Ink)&&Css(css,".combat-hitmarker.headshot","color")==Hex(NativeHitmarker.Headshot),"marker colours");
            Check(Css(css,".combat-hitmarker","filter")=="drop-shadow(0 "+NativeHitmarker.ShadowY+"px "+NativeHitmarker.ShadowBlur+"px #0009)"&&Math.Abs(NativeHitmarker.ShadowAlpha-0x99/255f)<1e-6,"marker drop shadow");
            Check(S(svg,"cross")=="m34 34-17-17m49 17 17-17M34 66 17 83m49-17 17 17"&&Math.Abs(NativeHitmarker.StrokeIn-(50-34)/100f)<1e-6&&Math.Abs(NativeHitmarker.StrokeOut-(50-17)/100f)<1e-6,"cross strokes 16%..33%");
            Check(S(svg,"tips")=="m11 11-3-3m81 3 3-3M11 89l-3 3m81-3 3 3"&&Math.Abs(NativeHitmarker.TipIn-(50-11)/100f)<1e-6&&Math.Abs(NativeHitmarker.TipOut-(50-8)/100f)<1e-6,"corner tips 39%..42%");
            Check(Math.Abs(D(svg,"stroke")/100-NativeHitmarker.Stroke)<1e-6&&Math.Abs(D(svg,"iconStroke")-NativeHitmarker.IconStroke)<1e-6,"stroke widths");
            Check(S(svg,"flame")==NativeHitmarker.FlamePath&&S(svg,"poison")==NativeHitmarker.PoisonPath,"element icon paths");
            Check(Css(css,".hitmark-elements","top")=="calc(100% + "+Px(NativeHitmarker.IconTop)+")"&&Css(css,".hitmark-elements","gap")==Px(NativeHitmarker.IconGap)&&Css(css,".hitmark-elements svg","width")==Px(NativeHitmarker.Icon),"element row");
            Check(Css(css,".hitmark-fire","color")==Hex(NativeHitmarker.FireColor)&&Css(css,".hitmark-poison","color")==Hex(NativeHitmarker.PoisonColor),"element colours");

            // 2. Sprint tag: every flag combination, then its style.
            foreach(var c in json.GetProperty("sprint").EnumerateArray())
            {
                int state=NativeSprintTag.State(B(c,"active"),(float)D(c,"hp"),B(c,"sprinting"),B(c,"exhausted"));string at="sprint "+c.GetRawText();
                Check((state!=NativeSprintTag.None)==B(c,"shown"),at+" shown");Check(NativeSprintTag.Text(state)==S(c,"text"),at+" text");
                Check((state==NativeSprintTag.Exhausted)==B(c,"exhaustedClass"),at+" exhausted");Check((state==NativeSprintTag.Sprinting)==B(c,"chevron"),at+" chevron");
            }
            Check(Css(css,".sprint-status","color")==Hex(NativeSprintTag.SprintColor)&&Css(css,".sprint-status","font")=="700 "+Px(NativeSprintTag.Size)+"/"+NativeSprintTag.LineHeight.ToString(CultureInfo.InvariantCulture)+" ui-monospace,monospace","sprint tag face");
            Check(Css(css,".sprint-status","letter-spacing")==Em(NativeSprintTag.Tracking)&&Css(css,".sprint-status","gap")==Px(NativeSprintTag.Gap),"sprint tag spacing");
            Check(Css(css,".sprint-status","text-shadow")=="0 0 "+Px(NativeSprintTag.GlowBlur)+" "+Hex(NativeSprintTag.GlowColor,NativeSprintTag.GlowAlpha),"sprint glow");
            Check(Css(css,".sprint-status i","font-size")==Px(NativeSprintTag.ChevronSize)&&Css(css,".sprint-status i","line-height")==Px(NativeSprintTag.ChevronLine)&&Css(css,".sprint-status i","animation")=="sprint-chevron "+NativeSprintTag.ChevronPeriod.ToString(CultureInfo.InvariantCulture).TrimStart('0')+"s ease-in-out infinite alternate","chevron");
            Check(json.GetProperty("keyframes").GetProperty("sprint-chevron").GetString()=="from{transform:translateX(-"+Px(NativeSprintTag.ChevronShift)+");opacity:"+NativeSprintTag.ChevronDim.ToString(CultureInfo.InvariantCulture).TrimStart('0')+"}to{transform:translateX("+Px(NativeSprintTag.ChevronShift)+");opacity:1}","chevron sway");
            Near(NativeSprintTag.ChevronPhase(0),0,"chevron starts left",1e-5);Near(NativeSprintTag.ChevronPhase(NativeSprintTag.ChevronPeriod),1,"chevron right after one period",1e-4);Near(NativeSprintTag.ChevronPhase(2*NativeSprintTag.ChevronPeriod),0,"alternate back",1e-4);Near(NativeSprintTag.ChevronPhase(NativeSprintTag.ChevronPeriod/2),.5,"ease-in-out midpoint",1e-4);
            Check(Css(css,".sprint-status.exhausted","color")==Hex(NativeSprintTag.ExhaustedColor)&&Css(css,".sprint-status.exhausted","font-size")==Px(NativeSprintTag.ExhaustedSize)&&Css(css,".sprint-status.exhausted","letter-spacing")==Em(NativeSprintTag.ExhaustedTracking),"depleted tag");
            Check(Css(css,".status-tags span","padding")==Px(NativeSprintTag.PadY)+" "+Px(NativeSprintTag.PadX)&&Css(css,".status-tags span","background")==Hex(NativeSprintTag.Fill,NativeSprintTag.FillAlpha)&&Css(css,".status-tags span","border")==Px(NativeSprintTag.Border)+" solid currentColor","tag box");
            Check(Css(css,".healthbox .status-tags","bottom")=="calc(100% + "+Px(NativeSprintTag.Above)+")"&&Css(css,".healthbox .status-tags","left")=="0","tag above the health box");

            // 3. L33t status: the source's precedence (control over the Admin Pause), title, countdown and subline.
            var contract=json.GetProperty("contract");
            Check(contract.GetProperty("brainSeconds").GetDouble()==NativeLeetStatus.ControlSeconds&&contract.GetProperty("freezeSeconds").GetDouble()==NativeLeetStatus.FreezeSeconds,"LEET brain / freeze seconds");
            foreach(var c in json.GetProperty("leet").EnumerateArray())
            {
                var l=c.GetProperty("leet");double time=D(c,"time");string controlling=S(l,"controlling"),by=S(l,"controlledBy");bool fight=S(c,"phase")=="fight";string title=null,sub=null;
                string Name(string id)=>id=="a"?"Sheldon":id=="b"?"Astra":null;
                if(fight&&(controlling!=null||by!=null)){double until=l.TryGetProperty("controlUntil",out var u)?u.GetDouble():0;title=NativeLeetStatus.Title(controlling!=null,Name(controlling??by));sub=controlling!=null?NativeHudText.SecondsVulnerable(until-time):NativeHudText.Seconds(until-time);}
                else if(fight&&l.TryGetProperty("frozenUntil",out var fu)&&fu.GetDouble()>time){title=NativeLeetStatus.PauseText;sub=NativeHudText.Seconds(fu.GetDouble()-time);}
                string at="leet "+c.GetRawText();Check((title!=null)==B(c,"shown"),at+" shown");Check(title==S(c,"title"),at+" title: "+title);Check(sub==S(c,"sub"),at+" sub: "+sub);
            }
            Check(Css(css,".leet-status","top")==(NativeLeetStatus.Top*100).ToString(CultureInfo.InvariantCulture)+"%"&&Css(css,".leet-status","gap")==Px(NativeLeetStatus.Gap)&&Css(css,".leet-status","padding")==Px(NativeLeetStatus.PadY)+" "+Px(NativeLeetStatus.PadX),"leet box layout");
            Check(Css(css,".leet-status","color")==Hex(NativeLeetStatus.Ink)&&Css(css,".leet-status","background")==Hex(NativeLeetStatus.Fill,NativeLeetStatus.FillAlpha)&&Css(css,".leet-status","border")==Px(NativeLeetStatus.Border)+" solid "+Hex(NativeLeetStatus.Edge),"leet box colours");
            Check(Css(css,".leet-status","box-shadow")=="0 0 "+Px(NativeLeetStatus.GlowSpread)+" "+Hex(NativeLeetStatus.GlowColor,NativeLeetStatus.GlowAlpha)&&Css(css,".leet-status strong","letter-spacing")==Em(NativeLeetStatus.TitleTracking)&&Css(css,".leet-status span","font-size")==Px(NativeLeetStatus.SubSize),"leet glow and type");

            // 4. DEV fighter HUD: the arcade cards and charge bar, the Emperor card, both combo meters.
            foreach(var group in new[]{"arcade","emperor"})foreach(var c in json.GetProperty(group).EnumerateArray())
            {
                bool emperor=group=="emperor",fight=S(c,"phase")=="fight";double time=D(c,"time");var a=c.GetProperty(emperor?"emperor":"arcade");bool has=a.ValueKind==JsonValueKind.Object;string at=group+" "+c.GetRawText();
                Check(fight==B(c,"shown"),at+" shown");if(!fight)continue;
                var specials=c.GetProperty("specials");
                if(emperor){double cd=D(c,"cooldown");var s0=specials[0];Check(specials.GetArrayLength()==1&&S(s0,"key")=="Q"&&S(s0,"label")==NativeArcadeHud.Electric&&S(s0,"state")==(cd>0?NativeHudText.Seconds(cd):NativeArcadeHud.Ready),at+" ELECTRIC");Check(S(c,"hud")=="arcade-hud emperor-hud",at+" hud class");}
                else
                {
                    double hd=has?D(a,"hadokenCd"):0,up=has?D(a,"uppercutCd"):0;
                    Check(specials.GetArrayLength()==2&&S(specials[0],"key")=="1"&&S(specials[0],"label")==NativeArcadeHud.Hadouken&&S(specials[0],"state")==(hd>0?NativeHudText.Seconds(hd):NativeArcadeHud.Ready),at+" HADOUKEN");
                    Check(S(specials[1],"key")=="2"&&S(specials[1],"label")==NativeArcadeHud.Uppercut&&S(specials[1],"state")==(up>0?NativeHudText.Seconds(up):NativeArcadeHud.Ready),at+" UPPERCUT");
                    bool charging=has&&S(a,"action")=="charge";var charge=c.GetProperty("charge");Check(charging==(charge.ValueKind==JsonValueKind.Object),at+" charge bar");
                    if(charging){float v=(float)D(a,"charge");Check(S(charge,"text")==NativeArcadeHud.ChargeText(v)&&D(charge,"width")==NativeArcadeHud.ChargePercent(v),at+" charge text and width");}
                }
                var combo=c.GetProperty("combo");bool shown=has&&NativeArcadeHud.ComboShown(a.GetProperty("hits").GetInt32(),(float)time,(float)D(a,"lastHit"),emperor);Check(shown==(combo.ValueKind==JsonValueKind.Object),at+" combo shown");
                if(shown)
                {
                    int hits=a.GetProperty("hits").GetInt32();Check(S(combo,"count")==NativeHudText.Int(hits)&&S(combo,"small")==NativeArcadeHud.HitsSuffix(hits,emperor),at+" count");
                    Check(S(combo,"damage")==NativeHudText.Int(NativeArcadeHud.Round(D(a,"comboDamage")))+NativeArcadeHud.DamageText,at+" damage");Near(NativeArcadeHud.ComboFill((float)time,(float)D(a,"lastHit"),emperor)*100,D(combo,"width"),at+" bar",2e-3);
                    Check(S(combo,"cls")==(emperor?"arcade-combo emperor-combo":"arcade-combo"),at+" combo class");
                }
            }
            Check(Css(css,".arcade-hud","right")==Px(NativeArcadeHud.Right)&&Css(css,".arcade-hud","bottom")==Px(NativeArcadeHud.Bottom)&&Css(css,".arcade-hud","gap")==Px(NativeArcadeHud.ItemGap)&&Css(css,".arcade-hud","color")==Hex(NativeArcadeHud.Ink),"arcade HUD placement");
            Check(Css(css,".arcade-hud","flex-wrap")=="wrap"&&Css(css,".arcade-hud","justify-content")=="center","arcade HUD wraps and centres");
            Check(Css(css,".arcade-special","padding")==Px(NativeArcadeHud.SpecialPadY)+" "+Px(NativeArcadeHud.SpecialPadX)&&Css(css,".arcade-special","border-radius")==Px(NativeArcadeHud.SpecialRadius)&&Css(css,".arcade-special","font-size")==Px(NativeArcadeHud.SpecialSize),"arcade card box");
            Check(Css(css,".arcade-special","background")==Hex(NativeArcadeHud.SpecialFill,NativeArcadeHud.SpecialFillAlpha)+"!important"&&Css(css,".arcade-special","border")==Px(NativeArcadeHud.Border)+" solid "+Hex(NativeArcadeHud.SpecialEdge),"arcade card colours (!important fill)");
            Check(Css(css,".emperor-hud .arcade-special","border-color")==Hex(NativeArcadeHud.EmperorEdge),"Emperor card edge (its fill loses to the !important one)");
            Check(Css(css,".arcade-special b","color")==Hex(NativeArcadeHud.StateInk)&&Css(css,".arcade-special b","margin-left")==Px(NativeArcadeHud.StateGap)&&Css(css,".arcade-special b","font-size")==Px(NativeArcadeHud.StateSize),"arcade card state");
            Check(Css(css,".hadoken-charge","margin-top")==Px(NativeArcadeHud.ChargeTop)&&Css(css,".hadoken-charge","padding")==Px(NativeArcadeHud.ChargePadY)+" "+Px(NativeArcadeHud.ChargePadX)&&Css(css,".hadoken-charge","min-width")==Px(NativeArcadeHud.ChargeMin)&&Css(css,".hadoken-charge","flex-basis")=="100%","charge bar layout");
            Check(Css(css,".hadoken-charge","background")==Hex(NativeArcadeHud.ChargeFill,NativeArcadeHud.ChargeFillAlpha)&&Css(css,".hadoken-charge","border")==Px(NativeArcadeHud.ChargeBorder)+" solid "+Hex(NativeArcadeHud.ChargeEdge)&&Css(css,".hadoken-charge","font-size")==Px(NativeArcadeHud.ChargeSize)&&Css(css,".hadoken-charge i","background")==Hex(NativeArcadeHud.ChargeBar,NativeArcadeHud.ChargeBarAlpha),"charge bar colours");
            Check(Css(css,".arcade-combo","top")==(NativeArcadeHud.ComboTop*100).ToString(CultureInfo.InvariantCulture)+"%"&&Css(css,".arcade-combo","right")==(NativeArcadeHud.ComboRight*100).ToString(CultureInfo.InvariantCulture)+"%"&&Css(css,".arcade-combo","transform")=="scale("+NativeArcadeHud.ComboScale.ToString(CultureInfo.InvariantCulture).TrimStart('0')+")"&&Css(css,".arcade-combo","transform-origin")=="left center"&&from.GetProperty(".arcade-combo").GetProperty("transform").GetString()=="match-hud.css","combo placement (match-hud's scale(.8) replaces the skew)");
            Check(Css(css,".arcade-combo","color")==Hex(NativeArcadeHud.ComboInk)&&Css(css,".arcade-combo","text-shadow")==Px(NativeArcadeHud.ShadowOffset)+" "+Px(NativeArcadeHud.ShadowOffset)+" "+Hex(NativeArcadeHud.ComboShadow),"combo colours");
            Check(Css(css,".arcade-combo strong","font-size")=="clamp("+Px(NativeArcadeHud.CountMin)+","+(NativeArcadeHud.CountVw*100).ToString(CultureInfo.InvariantCulture)+"vw,"+Px(NativeArcadeHud.CountMax)+")"&&Css(css,".arcade-combo strong","line-height")=="1"&&Css(css,".arcade-combo small","font-size")==Px(NativeArcadeHud.SmallSize),"combo count");
            Near(NativeArcadeHud.CountSize(1600),70,"count clamp at 1600");Near(NativeArcadeHud.CountSize(600),42,"count clamp at 600");Near(NativeArcadeHud.CountSize(1000),50,"count 5vw");
            Check(Css(css,".arcade-combo span","color")==Hex(NativeArcadeHud.DamageInk)&&Css(css,".arcade-combo span","font-size")==Px(NativeArcadeHud.DamageSize)&&Css(css,".arcade-combo span","margin")==Px(NativeArcadeHud.DamageMargin)+" 0","combo damage line");
            Check(Css(css,".arcade-combo i","height")==Px(NativeArcadeHud.BarHeight)&&Css(css,".arcade-combo i","background")==Hex(NativeArcadeHud.ComboBar)&&Css(css,".arcade-combo i","box-shadow")=="0 0 "+Px(NativeArcadeHud.BarGlow)+" "+Hex(NativeArcadeHud.ComboBarGlow),"combo bar");
            Check(Css(css,".emperor-combo","color")==Hex(NativeArcadeHud.EmperorInk)&&Css(css,".emperor-combo","text-shadow")==Px(NativeArcadeHud.ShadowOffset)+" "+Px(NativeArcadeHud.ShadowOffset)+" "+Hex(NativeArcadeHud.EmperorShadow)&&Css(css,".emperor-combo i","background")==Hex(NativeArcadeHud.EmperorBar),"Emperor combo");

            // 5. Siren HUD: the contract's slots and names, every source case's spans, and its style.
            var slots=contract.GetProperty("slots");var names=contract.GetProperty("names");
            foreach(var mode in new[]{"melee","ranged"}){var moves=NativeSirenHud.Moves(mode=="ranged");var want=slots.GetProperty(mode).GetProperty("abilities");Check(moves.Length==want.GetArrayLength(),"slot count "+mode);for(int i=0;i<moves.Length;i++)Check(moves[i]==want[i].GetString(),"SIREN_SLOTS."+mode+"["+i+"]");}
            foreach(var n in names.EnumerateObject())Check(NativeSirenHud.Name(n.Name)==n.Value.GetString(),"SIREN_MOVE_NAMES."+n.Name);
            foreach(var c in json.GetProperty("siren").EnumerateArray())
            {
                bool ranged=S(c,"mode")=="ranged";string key=NativeSirenHud.Key(S(c,"key").Substring(3));double time=D(c,"time");var ready=c.GetProperty("ready");var spans=c.GetProperty("spans");string at="siren "+c.GetRawText();
                Check(S(c,"container")=="arcade-hud siren-hud"&&spans.GetArrayLength()==5,at+" container");
                Check(S(spans[0],"key")==key&&S(spans[0],"label")==(ranged?NativeSirenHud.RangedMode:NativeSirenHud.MeleeMode)&&S(spans[0],"state")==null,at+" mode");
                var moves=NativeSirenHud.Moves(ranged);
                for(int i=0;i<4;i++)
                {
                    double r=ready.TryGetProperty(moves[i],out var rv)?rv.GetDouble():0;
                    Check(S(spans[i+1],"key")==(i+1).ToString(CultureInfo.InvariantCulture)&&S(spans[i+1],"label")==NativeSirenHud.Name(moves[i])&&S(spans[i+1],"state")==(r>time?NativeHudText.Seconds(r-time):NativeArcadeHud.Ready),at+" move "+(i+1));
                }
            }
            Check(Css(css,".siren-hud","width")=="min("+Px(NativeSirenHud.Width)+","+(NativeSirenHud.WidthVw*100).ToString(CultureInfo.InvariantCulture)+"vw)"&&Css(css,".siren-hud","color")==Hex(NativeSirenHud.Ink)&&Css(css,".siren-hud","font-size")==Px(NativeSirenHud.Size),"Siren HUD row");
            Check(from.GetProperty(".siren-hud").GetProperty("bottom").GetString()=="style.css"&&from.GetProperty(".arcade-hud").GetProperty("bottom").GetString()=="match-hud.css"&&Css(css,".arcade-hud","bottom")==Px(NativeArcadeHud.Bottom),"match-hud.css's .arcade-hud bottom (later, same specificity) wins over .siren-hud's 110 px");
            Check(Css(css,".siren-hud>span","padding")==Px(NativeSirenHud.PadY)+" "+Px(NativeSirenHud.PadX)&&Css(css,".siren-hud>span","background")==Hex(NativeSirenHud.Fill,NativeSirenHud.FillAlpha)&&Css(css,".siren-hud>span","border")==Px(NativeSirenHud.Border)+" solid "+Hex(NativeSirenHud.Edge)&&Css(css,".siren-hud>span","border-radius")==Px(NativeSirenHud.Radius),"Siren span box");
            Check(Css(css,".siren-hud b","margin-left")==Px(NativeSirenHud.StateGap)&&Css(css,".siren-hud b","color")==Hex(NativeSirenHud.StateInk),"Siren state");
            // A guest's cooldowns from its own Siren events (the host does not send the Ready times).
            {
                var cd=new NativeSirenCooldowns();var ev=new List<ArenaEvent>{new ArenaEvent{Id=4,Kind=EventKind.Siren,Source=2,Detail="rend",Time=9},new ArenaEvent{Id=5,Kind=EventKind.Siren,Source=1,Detail="eruption",Time=10},new ArenaEvent{Id=6,Kind=EventKind.Siren,Source=1,Detail="hellfire",Time=10.2f}};
                cd.Update(ev,0,1);Near(cd.Ready("eruption"),10+NativeSirenRules.Rule("eruption").Cooldown,"guest eruption cooldown",1e-5);Check(cd.Ready("rend")==0&&cd.Ready("hellfire")==0&&cd.Ready("mark")==0,"other pilots' moves and the primary start nothing");
                cd.Update(new List<ArenaEvent>(),1,1);Check(cd.Ready("eruption")==0,"a new generation clears the guest cooldowns");
            }

            // 6. Number.prototype.toFixed(1) for the countdowns (exact binary value, ties up), and the cache returns one string.
            foreach(var (x,s) in new[]{(0.0,"0.0s"),(0.04,"0.0s"),(0.05,"0.1s"),(0.15,"0.1s"),(0.25,"0.3s"),(1.25,"1.3s"),(1.05,"1.1s"),(2.3000000000000007,"2.3s"),(29.96,"30.0s"),(34.5,"34.5s"),(-1.0,"0.0s"),(125.04,"125.0s")})
                Check(NativeHudText.Seconds(x)==s,"toFixed(1) "+x.ToString("R",CultureInfo.InvariantCulture)+" -> "+NativeHudText.Seconds(x)+" (want "+s+")");
            Check(ReferenceEquals(NativeHudText.Seconds(3.25),NativeHudText.Seconds(3.25))&&ReferenceEquals(NativeHudText.Int(42),NativeHudText.Int(42)),"HUD strings are cached (no per-frame allocation)");

            // 7. The runtime hooks, so no item silently returns to its placeholder.
            string Runtime(string file)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",file));
            string fightHud=Runtime("NativeFightHud.cs"),combat=Runtime("NativeCombatHud.cs"),home=Runtime("NativeHomeMenu.cs"),sandbox=Runtime("NativeSandbox.cs"),leet=Runtime("NativeLeet.cs");
            Check(fightHud.Contains("Hitmarker();")&&fightHud.Contains("hitmarker.Update(arena.Events,arena.Time,arena.Generation,arena.Local.Seat.id,arena.Pve!=null,hitTarget)")&&!fightHud.Contains("presentation.LastHitUntil"),"fight HUD draws the ported hit marker (not the every-hit X)");
            Check(fightHud.Contains("SprintTag(p,r);")&&fightHud.Contains("p.SprintExhausted||p.Pose.SprintExhausted"),"sprint tag reads the exhausted state");
            Check(fightHud.Contains("FighterHud(p,W,H)")&&fightHud.Contains("p.Seat.spec==\"ansatsuken\"||p.Seat.spec==\"emperor\"")&&fightHud.Contains("SirenHud(p,W)"),"fighter and Siren HUDs drawn");
            Check(combat.Contains("LeetStatus();")&&!combat.Contains("\"CONTROLLING \"+local.Controlling"),"L33t box replaces the plain CONTROLLING label");
            Check(leet.Contains("ControlUntil=owner.ControlUntil=Time+5")&&NativeLeetStatus.ControlSeconds==5,"the guest's 5 s countdown matches the host's possession length");
            Check(home.Contains("if(dev&&SubtleLink(ref lx,y+15,\"book-open\",\"Catalogue\",le))")&&home.Contains("if(l.Closed||!account.Developer){Go(MenuScreen.Home);return;}"),"Catalogue is a DEV-only link and screen");
            foreach(var call in new[]{"SandboxPreview(\"draft\")","SandboxPreview(\"rarity\",previewRarity)","SandboxPreview(\"knockout\")","SandboxPreview(\"my-death\")","SandboxLaunchDebris(debrisMass,debrisSpeed,debrisMaterial,debrisImpact)","SandboxReset()","SandboxTestBlock()"})
                Check(sandbox.Contains(call),"sandbox panel calls "+call);
            Check(sandbox.Contains("debrisMass=8,debrisSpeed=15")&&sandbox.Contains("Slider(\"Mass (kg)\",debrisMass,1,150)"),"debris launcher defaults and range (8 kg, 15 m/s, up to 150 kg)");

            Console.WriteLine("PASS HUD placeholder parity: "+count+" checks ("+frames+" hit marker frames from hitmarker.ts, sprint, L33t, DEV fighter and Siren HUD markup from the pinned source, style values, runtime hooks)");
        }
    }
}
#endif
