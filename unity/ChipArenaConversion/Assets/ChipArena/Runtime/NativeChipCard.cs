using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class ChipStatText {public string text,tone;}
    [Serializable] public sealed class ChipTextEntry {public string id,description;public ChipStatText[] lines;}
    [Serializable] public sealed class ChipTextSet {public string commit;public ChipTextEntry[] chips;public ContentChip[] sirenTrack;}
    // How a card is being shown this frame. Hover and Lift are eased 0..1 by the caller. `Plain` drops the clipped card art
    // (the kits' auras, Mythic's foil) for a card drawn under a rotation, where IMGUI clips would not follow it.
    public struct ChipLook {public float Alpha,Hover,Time,SweepAt,Bright;public bool Focus,Enabled,Chosen,Plain;public static ChipLook Idle(float time)=>new ChipLook{Alpha=1,Time=time,SweepAt=-10,Bright=1};}
    // Card sizes from draft-screen.css (compact tray) and style.css (.selection-focus spotlight card).
    public struct ChipSize
    {
        public float Pad,PinInset,Pin,Die,Icon,Label,Name,NameLine,Desc,DescLine,Stat,Foot,Radius;public int DescLines,NameLines;
        public static ChipSize Tray(bool large,int descLines)=>new ChipSize{Pad=8,PinInset=16,Pin=7,Die=36,Icon=20,Label=10,Name=large?17:15,NameLine=1.15f,Desc=large?13:12,DescLine=1.38f,Stat=12.5f,Foot=9.5f,Radius=9,DescLines=descLines,NameLines=3};
        public static ChipSize Spotlight=>new ChipSize{Pad=18,PinInset=24,Pin=10,Die=58,Icon=34,Label=12,Name=30,NameLine=1.08f,Desc=17,DescLine=1.55f,Stat=14,Foot=10,Radius=9,DescLines=8,NameLines=3};
    }
    // client/chip-card.ts as an IMGUI drawing: the processor package (metal legs, heat-spreader plate with the lit die and
    // its line icon, grade pips and name), the source description and stat lines (exported by tools/export_chip_text.mjs),
    // and the footer with the install count and stack slots. Tier materials, traces, glass and the exclusive kits' card art
    // follow chip-cards.css and the kits' card modules (NativeChipArt).
    public static class NativeChipCard
    {
        private static ChipTextSet set;private static Dictionary<string,ChipTextEntry> text;
        private static void Ensure(){if(set!=null)return;var asset=Resources.Load<TextAsset>("ChipText");if(asset==null)throw new InvalidOperationException("ChipText.json is missing");set=JsonUtility.FromJson<ChipTextSet>(asset.text);text=set.chips.ToDictionary(c=>c.id);}
        public static ChipTextSet Text{get{Ensure();return set;}}
        public static string Description(ContentChip c){Ensure();return text.TryGetValue(c.id,out var t)?t.description:c.description;}
        public static ChipStatText[] Lines(ContentChip c){Ensure();return text.TryGetValue(c.id,out var t)&&t.lines!=null?t.lines:Array.Empty<ChipStatText>();}
        public static Color Rarity(string r)=>U.Hex(r=="mishima"?0xb3b5c2:r=="ansatsuken"?0xff354e:r=="l44t"?0x4dff83:r=="siren"?0xb7a8ff:r=="uncommon"?0x79e59a:r=="rare"?0x70b5ff:r=="epic"?0xce8aff:r=="legendary"?0xffae55:r=="mythic"?0xff6475:0xf1f5f9);
        public static string RarityName(string r)=>r=="l44t"?"L44T":string.IsNullOrEmpty(r)?"Common":char.ToUpperInvariant(r[0])+r.Substring(1);
        private static int Rank(string r)=>r=="common"?1:r=="uncommon"?2:r=="rare"?3:r=="epic"?4:r=="legendary"?5:r=="mythic"?6:0;
        private static int Particles(string r)=>r=="mishima"?18:r=="ansatsuken"?20:r=="l44t"?24:r=="siren"?16:r=="mythic"?14:r=="legendary"?6:0;
        public static bool Exclusive(string r)=>r=="mishima"||r=="ansatsuken"||r=="l44t"||r=="siren";
        private static string Tag(ContentChip c){string t=c.tags!=null&&c.tags.Length>0?c.tags[0]:"all";return t=="pve-money"?"BOSS FIGHT ONLY":t=="projectile"?"GUNS + MAGIC":t=="all"?"ALL WEAPONS":t.ToUpperInvariant();}
        private struct Layout {public float Plate,Title,DescTop,StatsTop,FootTop,Height;public List<string> Name,Desc;}
        private static Layout Measure(ContentChip c,float w,ChipSize s)
        {
            var l=new Layout();float plateW=w-2*s.Pad,titleW=plateW-15-s.Die-9;
            l.Name=U.Wrap(c.name,s.Name,titleW,true,s.NameLines);float title=s.Label+6+l.Name.Count*s.Name*s.NameLine;l.Title=title;l.Plate=14+Mathf.Max(s.Die,title);
            l.Desc=s.DescLines>0?U.Wrap(Description(c),s.Desc,w-2*s.Pad-6,false,s.DescLines):new List<string>();
            float y=s.Pad+l.Plate;l.DescTop=y+7;if(l.Desc.Count>0)y+=7+l.Desc.Count*s.Desc*s.DescLine+8;else y+=8;
            var lines=Lines(c);l.StatsTop=y-3;if(lines.Length>0)y+=-3+lines.Length*(s.Stat*1.22f+1)+8;
            l.FootTop=y;l.Height=y+6+1+Mathf.Max(8,s.Foot*1.2f)+s.Pad;return l;
        }
        public static float Height(ContentChip c,float w,ChipSize s)=>Measure(c,w,s).Height;
        // Draw a card filling `r` (height from Height(), or the row's tallest card). `copies` already owned.
        public static void Draw(Rect r,ContentChip c,int copies,ChipSize s,ChipLook look)
        {
            if(c==null||look.Alpha<=.002f)return;var l=Measure(c,r.width,s);float a=look.Alpha,t=look.Time,h=look.Hover;string tier=c.rarity;Color rare=Rarity(tier);var st=NativeChipArt.For(tier,rare);
            bool legendary=tier=="legendary",mythic=tier=="mythic",siren=tier=="siren";Color A(Color x,float k=1)=>U.A(x,a*k);
            // Shadows: 0 0 0 1px #000a, 0 8px 0 #050608, 0 16px 34px #0009 and the tier's glow (hover: 0 14px 0, 0 26px 44px #000b
            // and 0 0 28px rarity 26%; Siren 0 0 40px #8f73ff66); the chosen package's 60 px halo.
            U.Round(new Rect(r.x,r.y+8+6*h,r.width,r.height),A(U.Hex(siren?0x020203:0x050608)),s.Radius);U.Glow(new Rect(r.x,r.y+16+10*h,r.width,r.height),A(new Color(0,0,0,(siren?.8f:.6f)+.13f*h)),17+5*h);
            if(st.Glow.a>0)U.Glow(r,A(st.Glow),st.GlowSpread);if(h>0)U.Glow(r,siren?A(U.Hex(0x8f73ff),.4f*h):A(rare,.26f*h),siren?40:28);if(look.Chosen)U.Glow(r,A(rare,.42f),60);
            U.Round(new Rect(r.x-1,r.y-1,r.width+2,r.height+2),A(new Color(0,0,0,.67f)),s.Radius+1);
            // Legs: the tier's metal (pin-a → pin-b → pin-c), 7 px of every 15, inset from the corners; the bottom row mirrored.
            NativeChipArt.Legs(r,st,s.PinInset,s.Pin,a);
            // Substrate (sub-a → sub-b; Siren's void glass) under the edge (rarity 52% into #34363f or the kit's own; hover: rarity
            // 72% into white), then the circuit traces.
            NativeChipArt.Substrate(r,st,s.Radius,Color.Lerp(st.Edge,U.Mix(Color.white,rare,.72f),h),a);NativeChipArt.Traces(r,rare,st,a);
            // Particles (Legendary and up): sparks drifting off the long edges, 2.8 s (Mythic 1.7 s, Siren 3.4 s) in the kit colours.
            int sparks=Particles(tier);float period=mythic?1.7f:siren?3.4f:2.8f,size=mythic?4:3;
            for(int n=0;n<sparks;n++){float k=Mathf.Repeat(t/period+n*.31f/period,1),op=k<.2f?k/.2f*.9f:.9f*(1-(k-.2f)/.8f);bool even=n%2==0;float px=r.x+r.width*((n*37+11)%100)/100f+(n-6)*3*k,py=even?r.y+2+3-35*k:r.yMax-3-3+35*k;
                float sw=size*(1-.6f*k),sh=(siren?5:size)*(1-.6f*k);var sp=new Rect(px,py,sw,sh);bool dark=siren&&n%3==2;U.Glow(sp,A(dark?U.Hex(0xb7a8ff):st.SparkGlow,.5f*op),3);U.Fill(sp,A(dark?U.Hex(0x050409):st.Spark,op));}
            U.Round(new Rect(r.x+4,r.y+4,5,5),A(U.Hex(0x050608)),4);
            // The exclusive kits' card art over the substrate (Emperor, Ansatsuken, L44T, Siren).
            NativeChipArt.Aura(r,tier,t,a,look.Plain);
            // Heat spreader: drop shadow, linear-gradient(162deg, plate-a, plate-b 72%), Mythic's drifting foil, brushed lines,
            // the bevel (top: rarity 45% into #ffffff1f; bottom #000a; inner ring #ffffff0b) and the light sweep (hover, or the
            // Legendary/Mythic gleam every 4.6 s). Siren's spreader jolts on the glitch beat.
            var plate=new Rect(r.x+s.Pad+(siren?NativeChipArt.Jolt(t):0),r.y+s.Pad,r.width-2*s.Pad,l.Plate);
            U.Round(new Rect(plate.x,plate.y+3,plate.width,plate.height),A(new Color(0,0,0,siren?1:.53f)),6);U.Round(plate,A(st.PlateB),6);U.Fade(new Rect(plate.x+1,plate.y+1,plate.width-2,plate.height*.72f),A(st.PlateA));
            if(mythic&&!look.Plain)NativeChipArt.Holographic(plate,t,a);NativeChipArt.Brushed(plate,a);
            U.Fill(new Rect(plate.x+3,plate.y,plate.width-6,1),siren?A(U.Hex(0xb7a8ff,.18f)):A(U.Mix(U.Hex(0xffffff,.12f),rare,.45f)));U.Fill(new Rect(plate.x+3,plate.yMax-1,plate.width-6,1),A(new Color(0,0,0,siren?1:.66f)));
            U.Round(plate,Color.clear,6,siren?A(U.Hex(0xb7a8ff,.11f)):A(new Color(1,1,1,.043f)));
            float sweep=t-look.SweepAt;if(sweep>=0&&sweep<.75f)NativeChipArt.Sweep(plate,M.Bezier(.3f,.7f,.2f,1,sweep/.75f),a);
            else if(legendary||mythic){float g=Mathf.Repeat(t,4.6f)/4.6f;if(g>=.68f)NativeChipArt.Sweep(plate,M.Bezier(.3f,.7f,.2f,1,(g-.68f)/.32f),a);}
            // The die: silicon under glass lit in the kit colour (glow 20 px at 30%, hover 32 px at 55%; Siren 22 px violet), its
            // rings, the radial light (die 26% into #0a0b0e at 50% 46%), the inner shadow, grid and gloss; the chip's line icon.
            float bright=Mathf.Max(0,look.Bright);var die=new Rect(plate.x+7,plate.y+(plate.height-s.Die)/2,s.Die,s.Die);Color dieC=st.Die,ground=U.Hex(siren?0x050409:0x0a0b0e);
            U.Glow(die,siren?A(U.Hex(0x9b87ff),Mathf.Min(1,(.3f+.25f*h)*bright)):A(dieC,Mathf.Min(1,(.3f+.25f*h)*bright)),(siren?22:20)+12*h);
            U.Round(new Rect(die.x-4,die.y-4,die.width+8,die.height+8),siren?A(U.Hex(0xb7a8ff,.15f)):A(new Color(1,1,1,.08f+.04f*h)),6);U.Round(new Rect(die.x-3,die.y-3,die.width+6,die.height+6),A(ground),6);
            U.Round(die,A(ground),5,siren?A(U.Hex(0xb7a8ff,.6f)):A(dieC,.55f));
            float glowR=die.width*1.47f;var light=new Rect(die.center.x-glowR/2,die.y+die.height*.46f-glowR/2,glowR,glowR);
            if(siren)U.RadialStops(light,A(U.Hex(0x2d2244)),DieStopsSiren,DieAlpha);else U.RadialStops(light,A(dieC,Mathf.Min(1,.26f*bright)),DieStops,DieAlpha);
            U.RadialStops(die,A(Color.black),InnerAt,InnerAlpha);NativeChipArt.DieGlass(die,dieC,a);
            var icon=new Rect(die.center.x-s.Icon/2,die.center.y-s.Icon/2,s.Icon,s.Icon);U.Glow(icon,A(dieC,.35f*Mathf.Min(1,bright)),5);
            NativeIcons.Draw(icon,NativeIcons.Chip(c.id),A(Color.Lerp(Color.black,dieC,Mathf.Min(1,bright))));
            // Grade label with rank pips (the kits' own colour and tracking; L44T glows), then the name (Legendary's gold and
            // Mythic's foil gradients, Siren's glitch split).
            float tx=die.xMax+9,tw=plate.xMax-8-tx,ty=plate.y+(plate.height-l.Title)/2;string grade=RarityName(tier);
            U.Text(new Rect(tx,ty,tw,s.Label),grade,s.Label,A(st.Label),TextAnchor.MiddleLeft,bold:true,tracking:st.Tracking,glow:st.LabelGlow?A(U.Hex(0x39ff7a),.7f):default);int rank=Rank(tier);
            if(rank>0){float px=tx+U.Measure(grade,s.Label,true,false,st.Tracking).x+9;for(int i=0;i<6;i++){var dot=new Rect(px+i*8+1,ty+s.Label/2-2,4,4);if(i<rank){U.Glow(dot,A(rare,.5f),2);U.Round(dot,A(rare),4);}else U.Round(dot,A(new Color(1,1,1,.18f)),4);}}
            float ny=ty+s.Label+6;
            for(int i=0;i<l.Name.Count;i++)
            {
                var nr=new Rect(tx,ny+i*s.Name*s.NameLine,tw+4,s.Name*s.NameLine);
                if(siren&&!look.Plain){float g=Mathf.Repeat(t,4.2f)/4.2f;var block=new Rect(tx,ny,tw+4,l.Name.Count*s.Name*s.NameLine);U.SplitCopy(block,nr,g,false,l.Name[i],s.Name,a,TextAnchor.MiddleLeft);U.SplitCopy(block,nr,g,true,l.Name[i],s.Name,a,TextAnchor.MiddleLeft);}
                if((legendary||mythic)&&!look.Plain)GradientName(nr,l.Name[i],s.Name,legendary,a);
                else U.Text(nr,l.Name[i],s.Name,A(st.Name),TextAnchor.MiddleLeft,bold:true,tracking:.01f,glow:legendary?A(U.Hex(0xf39a35),.5f):default);
            }
            // Description and stat lines on the substrate.
            Color desc=siren?U.Hex(0xdcd7ef):U.Hex(0xc8cad3);
            for(int i=0;i<l.Desc.Count;i++)U.Text(new Rect(r.x+s.Pad+3,r.y+l.DescTop+i*s.Desc*s.DescLine,r.width-2*s.Pad-6,s.Desc*s.DescLine),l.Desc[i],s.Desc,A(desc),TextAnchor.MiddleLeft,shadow:siren?A(Color.black):default);
            var stats=Lines(c);for(int i=0;i<stats.Length;i++){var stl=stats[i];Color sc=stl.tone=="good"?U.Hex(0x5bea86):stl.tone=="bad"?U.Hex(0xff5c69):U.Hex(0xb9bcc8);
                U.Text(new Rect(r.x+s.Pad+3,r.y+l.StatsTop+i*(s.Stat*1.22f+1),r.width-2*s.Pad-6,s.Stat*1.22f),stl.text,s.Stat,A(sc),TextAnchor.MiddleLeft,bold:true,glow:stl.tone=="neutral"?default:A(sc,.2f));}
            // Footer (margin-top auto: at the bottom of a card taller than its content): tag on the left; INSTALL / ×c → ×c+1 /
            // ×n INSTALLED and the stack slots on the right.
            float fy=r.y+Mathf.Max(l.FootTop,r.height-(l.Height-l.FootTop));U.Fill(new Rect(r.x+s.Pad+3,fy,r.width-2*s.Pad-6,1),siren?A(U.Hex(0xb7a8ff,.165f)):A(new Color(1,1,1,.08f)));fy+=6;var foot=new Rect(r.x+s.Pad+3,fy,r.width-2*s.Pad-6,Mathf.Max(8,s.Foot*1.2f)+1);
            int max=Math.Min(12,Math.Max(1,c.max)),have=Math.Min(max,look.Chosen?copies+1:copies);string count=(look.Chosen?"×"+(copies+1)+" INSTALLED":copies>0?"×"+copies+" → ×"+(copies+1):"INSTALL")+" / "+c.max;Color footC=siren?U.Hex(0xb9b2d6):U.Hex(0xa5a7b2);
            U.Text(foot,Tag(c),s.Foot,A(footC),TextAnchor.MiddleLeft,mono:true,tracking:.06f);U.Text(foot,count,s.Foot,A(footC),TextAnchor.MiddleRight,mono:true,tracking:.06f);
            if(c.max<=6){float sw=max*7-2,sx=foot.xMax-U.Measure(count,s.Foot,false,true,.06f).x-9-sw,sy=foot.center.y-4;
                for(int i=0;i<max;i++){var slot=new Rect(sx+i*7,sy,5,8);U.Fill(slot,i<have?A(rare):A(new Color(1,1,1,.15f)));
                    if(i==have&&look.Enabled&&!look.Chosen){float pulse=.5f-.5f*Mathf.Cos(t*2*Mathf.PI/1.4f);U.Fill(slot,A(rare,.55f*pulse));U.Round(slot,Color.clear,1,A(rare),1);}}}
            if(look.Focus)U.Round(new Rect(r.x-7,r.y-7,r.width+14,r.height+14),Color.clear,s.Radius+4,A(rare),2);
            if(look.Chosen)U.Round(new Rect(r.x-6,r.y-6,r.width+12,r.height+12),Color.clear,s.Radius+4,A(rare),2);
        }
        private static readonly float[] DieStops={0,.72f,1},DieStopsSiren={0,.7f,1},DieAlpha={1,0,0},InnerAt={0,.55f,1},InnerAlpha={0,0,.7f};
        // Legendary: linear-gradient(180deg, #fff2cb, #ffc96d 55%, #f39a35) clipped to the name (with its warm glow); Mythic:
        // linear-gradient(100deg, #ffb3bd, #ffe39a 30%, #a8ffd0 55%, #9fd8ff 75%, #e2b6ff). Drawn in bands of the gradient.
        private static readonly float[] GoldAt={0,.55f,1},FoilAt={0,.3f,.55f,.75f,1};
        private static readonly Color[] Gold={U.Hex(0xfff2cb),U.Hex(0xffc96d),U.Hex(0xf39a35)},Foil={U.Hex(0xffb3bd),U.Hex(0xffe39a),U.Hex(0xa8ffd0),U.Hex(0x9fd8ff),U.Hex(0xe2b6ff)};
        private static Color Gradient(float u,float[] at,Color[] colors){for(int i=1;i<at.Length;i++)if(u<=at[i])return Color.Lerp(colors[i-1],colors[i],(u-at[i-1])/(at[i]-at[i-1]));return colors[colors.Length-1];}
        private static void GradientName(Rect nr,string str,float size,bool gold,float a)
        {
            const int Bands=5;float w=Mathf.Min(nr.width,U.Measure(str,size,true,false,.01f).x+2);
            U.Text(nr,str,size,U.A(gold?Gold[1]:Foil[1],a),TextAnchor.MiddleLeft,bold:true,tracking:.01f,glow:gold?U.Hex(0xf39a35,.5f*a):default);
            for(int b=0;b<Bands;b++)
            {
                // Gold runs down the glyphs' height (the outer bands reach past it for descenders and accents); the foil across.
                float u=(b+.5f)/Bands,y0=b==0?nr.y-2:nr.y+nr.height*(.12f+.76f*b/Bands),y1=b==Bands-1?nr.yMax+2:nr.y+nr.height*(.12f+.76f*(b+1)/Bands);
                var band=gold?Rect.MinMaxRect(nr.x-2,y0,nr.xMax+2,y1):new Rect(nr.x+w*b/Bands,nr.y-2,w/Bands,nr.height+4);
                U.BeginBand(band);U.Text(nr,str,size,U.A(Gradient(u,gold?GoldAt:FoilAt,gold?Gold:Foil),a),TextAnchor.MiddleLeft,bold:true,tracking:.01f);U.EndBand();
            }
        }
        // Per-tier power-on reveal (chip-cards.css draft-reveal keyframes) at `t` seconds after the card's own delay (the draft
        // tray and the Siren reveal; CSS 3D turns are drawn as their cos() squash).
        public struct Reveal {public float Alpha,Dx,Dy,Scale,ScaleX,ScaleY,Degrees,Bright;}
        public static Reveal Entrance(string tier,float t)
        {
            float duration=tier=="epic"?.9f:tier=="legendary"||tier=="mythic"?1:tier=="mishima"?1.2f:tier=="ansatsuken"?1.4f:tier=="l44t"?1.5f:tier=="siren"?1.25f:.62f;
            var v=new Reveal{Alpha=1,Scale=1,ScaleX=1,ScaleY=1,Bright=1};if(t>=duration+1){return v;}float k=M.Clamp01(t/duration),e=M.Bezier(.2f,.8f,.2f,1,k);
            if(t<0){v.Alpha=0;return v;}
            // Die flicker over 1 s: dark until 22%, flash 1.7 at 28%, .35 at 33%, 1.35 at 41%, settling to 1.
            v.Bright=M.Keys(M.Clamp01(t),new[]{0,.22f,.28f,.33f,.41f,1},new[]{.12f,.12f,1.7f,.35f,1.35f,1});
            switch(tier)
            {
                case "epic":{float ang=M.Keys(e,new[]{0,.6f,1},new float[]{90,-8,0});v.ScaleX=Mathf.Max(.02f,Mathf.Abs(Mathf.Cos(ang*Mathf.Deg2Rad)));v.Alpha=M.Clamp01(e*2.5f);break;}
                case "legendary":v.Dy=M.Keys(e,new[]{0,.6f,1},new float[]{-70,7,0});v.Degrees=M.Keys(e,new[]{0,.6f,1},new float[]{-6,0,0});v.Scale=M.Keys(e,new[]{0,.6f,1},new[]{.8f,1.035f,1});v.Alpha=M.Clamp01(e*2.5f);break;
                case "mythic":{v.Scale=M.Keys(e,new[]{0,.5f,1},new[]{.15f,1.1f,1});float ang=M.Keys(e,new[]{0,.5f,1},new float[]{180,-12,0});v.ScaleX=Mathf.Max(.02f,Mathf.Abs(Mathf.Cos(ang*Mathf.Deg2Rad)));v.Alpha=M.Clamp01(e*3);break;}
                case "mishima":v.Dy=M.Keys(e,new[]{0,.55f,1},new float[]{28,-3,0});v.Scale=M.Keys(e,new[]{0,.55f,1},new[]{.9f,1.02f,1});v.Alpha=M.Clamp01(e*2.5f);break;
                case "ansatsuken":{v.Dy=40*(1-e);float ang=-30*(1-e);v.ScaleX=Mathf.Abs(Mathf.Cos(ang*Mathf.Deg2Rad));v.Bright*=1+(1-e);v.Alpha=M.Clamp01(e*2.5f);break;}
                case "l44t":v.Dy=M.Keys(e,new[]{0,.35f,1},new float[]{50,-12,0});v.Scale=M.Keys(e,new[]{0,.35f,1},new[]{.68f,1.09f,1});v.Bright*=1+2*(1-M.Clamp01(e/.35f));v.Alpha=M.Clamp01(e*3);break;
                case "siren":v.ScaleY=Mathf.Lerp(.02f,1,M.Clamp01(e/.4f));v.ScaleX=Mathf.Lerp(1.12f,1,M.Clamp01(e/.4f));v.Dx=e<.75f&&e>.4f?(Mathf.Repeat(e*40,2)<1?7:-7)*(1-e):0;v.Alpha=M.Clamp01(e*3);break;
                default:v.Dy=34*(1-e);v.Scale=.94f+.06f*e;v.ScaleY=Mathf.Cos(18*(1-e)*Mathf.Deg2Rad);v.Alpha=e;break;
            }
            return v;
        }
    }
}
