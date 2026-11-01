using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
using Cart=ChipArena.UnityConversion.NativeChipCartridge;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class ChipTextEntry {public string id,description;public ChipStatText[] lines;}
    [Serializable] public sealed class ChipTextSet {public string commit;public ChipTextEntry[] chips;public ContentChip[] sirenTrack;}
    // How a card is being shown this frame. Hover is eased 0..1 by the caller. `Plain` drops the clipped card art (the traces,
    // the kits' auras, Mythic's foil) for a card drawn under a rotation, where IMGUI clips would not follow it. The draft tray's
    // highlight (NativeChipArt's holo scan, NativeHoloScan's timeline): `Holo` its eased level (1 under the pointer or the
    // keyboard focus, NativeHoloScan.Held while held across a gap) and `HoloAt` the time it engaged; `Ghost` the keyboard
    // focus's still brackets while the pointer is on another card; `Recede` 0..1 the veil while another card is highlighted;
    // `Charge` extra core charge (the pick's lock); `Calm` Reduced effects (the still variant: no breath, sweep, sheen or
    // particles); `Badge` the Siren pick's number badge on the top-left corner (1-9; 0 none), drawn over the lit edge and under
    // the brackets; `Unbuilt` 1..0 the reveal's assembly still to run (Reveal.Build; 0, the default, a finished card).
    public struct ChipLook {public float Alpha,Hover,Time,SweepAt,Bright,Holo,HoloAt,Recede,Charge,Unbuilt;public int Badge;public bool Focus,Enabled,Chosen,Plain,Calm,Ghost;public static ChipLook Idle(float time)=>new ChipLook{Alpha=1,Time=time,SweepAt=-10,Bright=1,HoloAt=-10};}
    // Card sizes from draft-screen.css (compact tray) and style.css (.selection-focus spotlight card). Radius picks the
    // cartridge's proportions (NativeChipArt: the tray's 13 / 6 px cuts, the spotlight's 18 / 8 at 11 and up); Die is the core's
    // size (the icon projected in it scales with it, about Icon); Pin the edge connector's height under the card and PinInset
    // its inset from the card's sides.
    public struct ChipSize
    {
        public float Pad,PinInset,Pin,Die,Icon,Label,Name,NameLine,Desc,DescLine,Stat,Foot,Radius;public int DescLines,NameLines;
        public static ChipSize Tray(bool large,int descLines)=>new ChipSize{Pad=10,PinInset=16,Pin=9,Die=54,Icon=20,Label=10,Name=large?17:15,NameLine=1.15f,Desc=large?13:12,DescLine=1.38f,Stat=12.5f,Foot=9.5f,Radius=9,DescLines=descLines,NameLines=3};
        public static ChipSize Spotlight=>new ChipSize{Pad=19,PinInset=24,Pin=12,Die=84,Icon=34,Label=12,Name=30,NameLine=1.08f,Desc=17,DescLine=1.55f,Stat=14,Foot=10,Radius=11,DescLines=8,NameLines=3};
    }
    // client/chip-card.ts as an IMGUI drawing, restyled as a neural core cartridge (Core/NativeChipCartridge.cs): the cartridge
    // (the faceted bezel and its inlay, its machined notches and status lamps, the anodised body with the board's circuit
    // under it and traces radiating from the core, the tier's fins, ring, crest, gems and particles, the gold edge connector
    // under it), the header (the core with the chip's icon projected in its
    // crystal; the grade with its rank pips and the name), the source description and stat rows (exported by
    // tools/export_chip_text.mjs), and the footer with the install count and stack slots. The art is NativeChipArt's.
    public static class NativeChipCard
    {
        private static ChipTextSet set;private static Dictionary<string,ChipTextEntry> text;
        private static void Ensure(){if(set!=null)return;var asset=Resources.Load<TextAsset>("ChipText");if(asset==null)throw new InvalidOperationException("ChipText.json is missing");set=JsonUtility.FromJson<ChipTextSet>(asset.text);text=set.chips.ToDictionary(c=>c.id);}
        public static ChipTextSet Text{get{Ensure();return set;}}
        public static string Description(ContentChip c){Ensure();return text.TryGetValue(c.id,out var t)?t.description:c.description+(string.IsNullOrWhiteSpace(c.sourceCard)?"":" Adapted from "+c.sourcePack+" / "+c.sourceCard+".");}
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ContentChip,ChipStatText[]> dynamicLines=new System.Runtime.CompilerServices.ConditionalWeakTable<ContentChip,ChipStatText[]>();
        public static ChipStatText[] Lines(ContentChip c){Ensure();return text.TryGetValue(c.id,out var t)&&t.lines!=null?t.lines:dynamicLines.GetValue(c,NativeChipStatText.Lines);}
        public static Color Rarity(string r)=>U.Hex(Cart.Rarity(r));
        public static string RarityName(string r)=>r=="l44t"?"L44T":string.IsNullOrEmpty(r)?"Common":char.ToUpperInvariant(r[0])+r.Substring(1);
        // The grade's label (constants, so a frame builds no string), its colour, tracking and glow.
        private static string Grade(string r)=>r=="uncommon"?"UNCOMMON":r=="rare"?"RARE":r=="epic"?"EPIC":r=="legendary"?"LEGENDARY":r=="mythic"?"MYTHIC":r=="mishima"?"MISHIMA":r=="ansatsuken"?"ANSATSUKEN":r=="l44t"?"L44T":r=="siren"?"SIREN":"COMMON";
        private static Color LabelOf(string r,Color rare)=>r=="mishima"?U.Hex(0xd6d7df):r=="ansatsuken"?U.Hex(0xff8a97):r=="l44t"?U.Hex(0xa0ffc0):r=="siren"?U.Hex(0xc9bfff):U.Mix(Color.white,rare,.82f);
        private static float Tracking(string r)=>r=="l44t"?.22f:r=="siren"?.18f:r=="mishima"?.14f:.12f;
        private static int Particles(string r)=>r=="mishima"?18:r=="ansatsuken"?20:r=="l44t"?24:r=="siren"?16:0;
        public static bool Exclusive(string r)=>r=="mishima"||r=="ansatsuken"||r=="l44t"||r=="siren";
        private static readonly Dictionary<string,string> tags=new Dictionary<string,string>();
        private static string Tag(ContentChip c)
        {
            string t=c.tags!=null&&c.tags.Length>0?c.tags[0]:"all";if(t=="pve-money")return "BOSS FIGHT ONLY";if(t=="projectile")return "GUNS + MAGIC";if(t=="all")return "ALL WEAPONS";
            if(!tags.TryGetValue(t,out var up))tags[t]=up=t.ToUpperInvariant();return up;
        }
        // INSTALL / ×c → ×c+1 / ×n INSTALLED with the stack's max, made once per value (a frame builds no string).
        private static readonly Dictionary<int,string> counts=new Dictionary<int,string>();
        private static string CountText(int copies,int max,bool chosen)
        {
            int key=(chosen?1<<30:0)|(Mathf.Clamp(copies,0,9999)<<12)|Mathf.Clamp(max,0,4095);if(counts.TryGetValue(key,out var text))return text;if(counts.Count>1024)counts.Clear();
            return counts[key]=(chosen?"×"+(copies+1)+" INSTALLED":copies>0?"×"+copies+" → ×"+(copies+1):"INSTALL")+" / "+max;
        }
        // The core's size on a card `w` wide (narrow trays shrink it) and how far it rises over the top edge.
        private static float CoreSize(float w,ChipSize s)=>Mathf.Min(s.Die,Mathf.Max(34,w*.27f));
        private static float CoreRise(ChipSize s)=>s.Radius>=11?7:5;
        // The layout from the card's top-left corner: the core mounted on the top-left shoulder (its collar's left point 2 px
        // out of the card, its top point CoreRise above it), the title block right of it, the header's divider under both.
        private struct Layout {public float CoreX,CoreY,Core,TitleX,TitleY,Title,Head,DescTop,StatsTop,FootTop,Height;public List<string> Name,Desc;}
        // Layouts by chip, width, size and UI scale (the wrapped lines are lists): made once, so a frame allocates nothing.
        private static readonly Dictionary<(string,int,int),Layout> layouts=new Dictionary<(string,int,int),Layout>();
        private static int SizeKey(ChipSize s)=>unchecked((int)(s.Name*7919+s.Desc*104729+s.Label*523+s.Stat*1009+s.Pad*31+s.Die*131+s.Radius*7+s.Foot*61)*31+s.DescLines*17+s.NameLines*13+Mathf.RoundToInt(U.S*100)*100003);
        private static Layout Measure(ContentChip c,float w,ChipSize s)
        {
            var key=(c.id??c.name,Mathf.RoundToInt(w*8),SizeKey(s));if(layouts.TryGetValue(key,out var cached))return cached;if(layouts.Count>512)layouts.Clear();
            return layouts[key]=Lay(c,w,s);
        }
        private static Layout Lay(ContentChip c,float w,ChipSize s)
        {
            var l=new Layout();float core=CoreSize(w,s),R=core*.48f;l.Core=core;l.CoreX=-2-(core/2-.866f*R);l.CoreY=-CoreRise(s)-(core/2-R);l.TitleX=l.CoreX+core/2+.866f*R+(s.Radius>=11?16:11);
            l.Name=U.Wrap(c.name,s.Name,w-l.TitleX-s.Pad,true,s.NameLines);float title=s.Label+7+l.Name.Count*s.Name*s.NameLine;l.Title=title;
            l.Head=Mathf.Max(l.CoreY+core+4,s.Pad+title+5);l.TitleY=s.Pad+Mathf.Max(0,(l.Head-5-s.Pad-title)/2);
            l.Desc=s.DescLines>0?U.Wrap(Description(c),s.Desc,w-2*s.Pad-6,false,s.DescLines):new List<string>();
            float y=l.Head+2;l.DescTop=y+7;if(l.Desc.Count>0)y+=7+l.Desc.Count*s.Desc*s.DescLine+8;else y+=8;
            var lines=Lines(c);l.StatsTop=y-3;if(lines.Length>0)y+=-3+lines.Length*(s.Stat*1.22f+1)+8;
            l.FootTop=y;l.Height=y+6+1+Mathf.Max(8,s.Foot*1.2f)+s.Pad-2+(NativeMods.Credit(c).Length>0?s.Foot*1.3f+3:0);return l;
        }
        public static float Height(ContentChip c,float w,ChipSize s)=>Measure(c,w,s).Height;
        // Draw a card filling `r` (height from Height(), or the row's tallest card). `copies` already owned.
        public static void Draw(Rect r,ContentChip c,int copies,ChipSize s,ChipLook look)
        {
            if(c==null||look.Alpha<=.002f)return;var l=Measure(c,r.width,s);float a=look.Alpha,t=look.Time,h=look.Hover;string tier=c.rarity;Color rare=Rarity(tier);var cart=Cart.Of(tier);bool calm=look.Calm;
            bool legendary=tier=="legendary",mythic=tier=="mythic",siren=tier=="siren";Color A(Color x,float k=1)=>U.A(x,a*k);float bw=NativeChipArt.BezelOf(s.Radius);
            // The holo scan at `holo` (the level times the alpha), `age` s since it engaged; under a rotation (Plain) only its halo,
            // the core's charge and the caller's lift. The core's charge: the highlight's rise and settle plus the pick's flare.
            float holo=look.Holo*a,age=t-look.HoloAt,charge=Mathf.Max(0,(holo>0?NativeHoloScan.DieCharge(age,look.Calm)*holo:0)+look.Charge);bool scan=holo>0&&!look.Plain;
            // The assembly (the reveal): the shell's pieces snap in, the crest and fins with them; the core ignites on the
            // reveal's flicker (Bright); the connector's pads light left to right as it finishes.
            float built=1-Mathf.Clamp01(look.Unbuilt),spread=Cart.FrameOffset(built),frame=a*Cart.FrameAlpha(built);
            // Shadows: the cartridge seen edge-on (a dark slab 5 px down, 9 px when lifted), the soft drop shadow, the tier's glow
            // (Legendary and up and the kits), the hover or holo scan halo, the chosen card's 60 px halo, a dark keyline.
            NativeChipArt.Package(new Rect(r.x,r.y+5+4*h,r.width,r.height),s.Radius,0,A(U.Hex(siren?0x020203:0x04060b)));U.Glow(new Rect(r.x,r.y+16+10*h,r.width,r.height),A(new Color(0,0,0,(siren?.8f:.6f)+.13f*h)),17+5*h);
            float tierGlow=legendary?.2f:mythic?.26f:Exclusive(tier)?.24f:cart.Rank>=4?.13f:0;if(tierGlow>0)U.Glow(r,A(siren?U.Hex(0x7d5cff):tier=="mishima"?U.Hex(0xff173b):rare,tierGlow),legendary?24:30);
            if(holo>0)NativeChipArt.HoloHalo(r,tier,rare,holo,t,look.Calm);else if(h>0)U.Glow(r,siren?A(U.Hex(0x8f73ff),.4f*h):A(rare,.26f*h),siren?40:28);if(look.Chosen)U.Glow(r,A(rare,.42f),60);
            NativeChipArt.Package(r,s.Radius,1,A(new Color(0,0,0,.67f)));
            // The edge connector under the card and the fins behind its sides, then the shell (bezel, inlay, body) and the crest.
            NativeChipArt.Connector(r,s.Radius,s.Pin,s.PinInset,rare,built,frame);if(scan)NativeChipArt.HoloPads(r,s.Radius,s.Pin,s.PinInset,tier,rare,holo,age,look.Calm);
            if(cart.Fins)NativeChipArt.Fins(r,tier,spread,frame);
            NativeChipArt.Shell(r,tier,s.Radius,spread,frame);NativeChipArt.Hardware(r,s.Radius,rare,t,calm,frame);NativeChipArt.Crest(r,tier,s.Radius,spread*1.4f,frame);
            // The holo scan's lit edge on the outline, under the core mounted over the frame.
            if(scan)NativeChipArt.HoloEdge(r,s.Radius,tier,rare,holo,age,t,look.Calm);
            // The header: the core at the left, its light pooled on the body; the traces radiating from it, the sweep's packets.
            float core=l.Core,jolt=siren&&!look.Plain&&!calm?NativeChipArt.Jolt(t):0;var coreRect=new Rect(r.x+l.CoreX+jolt,r.y+l.CoreY,core,core);var cc=coreRect.center;
            float light=Cart.CoreLight(cart,t,calm,charge)*Mathf.Max(0,look.Bright);
            NativeChipArt.Body(r,cart,rare,s.Radius,cc,core/2,light,a,look.Plain);NativeChipArt.Traces(r,s.Radius,cc,core/2,rare,(siren?.14f:.1f+.025f*cart.Rank)*a,look.Plain);
            if(Cart.Sweep(t,Cart.Phase(c.id),calm,out float sweepU))NativeChipArt.Packets(r,s.Radius,cc,core/2,Color.Lerp(rare,Color.white,.35f),sweepU,a);
            // The board's circuit under the face: faint under the text, clearer between the content and the footer.
            float fy=r.y+Mathf.Max(l.FootTop,r.height-(l.Height-l.FootTop));NativeChipArt.Board(r,s.Radius,rare,a,r.y+l.FootTop-4,fy-2);
            // Kit sparks drifting off the long edges (2.8 s; Siren 3.4 s) in the kits' colours, and the kits' card art.
            int sparks=calm?0:Particles(tier);float period=siren?3.4f:2.8f;Color spark=tier=="mishima"?U.Hex(0xff234a):tier=="ansatsuken"?U.Hex(0xff6071):siren?U.Hex(0xd9d0ff):rare,sparkGlow=tier=="mishima"?U.Hex(0xf30035):tier=="ansatsuken"?U.Hex(0xff294f):siren?U.Hex(0x9b87ff):rare;
            for(int n=0;n<sparks;n++){float k=Mathf.Repeat(t/period+n*.31f/period,1),op=k<.2f?k/.2f*.9f:.9f*(1-(k-.2f)/.8f);bool even=n%2==0;float px=r.x+r.width*((n*37+11)%100)/100f+(n-6)*3*k,py=even?r.y+2+3-35*k:r.yMax-3-3+35*k;
                float sw=3*(1-.6f*k),sh=(siren?5:3)*(1-.6f*k);var sp=new Rect(px,py,sw,sh);bool dark=siren&&n%3==2;U.Glow(sp,A(dark?U.Hex(0xb7a8ff):sparkGlow,.5f*op),3);U.Fill(sp,A(dark?U.Hex(0x050409):spark,op));}
            NativeChipArt.Aura(r,s.Radius,tier,t,a,look.Plain);
            // The footer's well (a darker band inside the bezel) and the etched divider under the header, its glint in the tier.
            float p1=NativeHudChrome.Px;NativeHudChrome.Cut(Rect.MinMaxRect(r.x+bw,fy,r.xMax-bw,r.yMax-bw),A(new Color(0,0,0,siren?.3f:.26f)),NativeChipArt.BottomCut(s.Radius)*.55f,NativeHudChrome.Corners.BL|NativeHudChrome.Corners.BR);
            float dy=r.y+l.Head;U.Fill(new Rect(r.x+bw+2,dy,r.width-2*bw-4,p1),A(new Color(0,0,0,.55f)));U.Fill(new Rect(r.x+bw+2,dy+p1,r.width-2*bw-4,p1),A(new Color(1,1,1,.06f)));
            U.HGradient(new Rect(r.x+bw+2,dy,Mathf.Min(r.width*.45f,core*2.2f),p1),A(U.Mix(rare,Color.white,.2f),.7f),Color.clear);
            // A ruler of ticks under the divider's right end.
            for(int i=0;i<7;i++)U.Fill(new Rect(r.xMax-bw-6-3*i,dy+2*p1,p1,i%2==0?3:1.5f),A(Color.white,.16f));
            // The header band's sheen: Mythic's foil drifting, the hover sweep, Legendary and Mythic's gleam every 4.6 s.
            var head=Rect.MinMaxRect(r.x+bw+1,r.y+NativeChipArt.TopCut(s.Radius)*.6f,r.xMax-bw-1,dy);
            if(mythic&&!look.Plain)NativeChipArt.Holographic(head,t,a);
            float sweep=t-look.SweepAt,gleam=Mathf.Repeat(t+Cart.Phase(c.id)*2,Cart.GleamEvery);if(sweep>=0&&sweep<.75f)NativeChipArt.Sweep(head,M.Bezier(.3f,.7f,.2f,1,sweep/.75f),a);
            else if((legendary||mythic)&&!calm&&gleam>=Cart.GleamEvery*.68f)NativeChipArt.Sweep(head,M.Bezier(.3f,.7f,.2f,1,(gleam-Cart.GleamEvery*.68f)/(Cart.GleamEvery*.32f)),a);
            // The title plate right of the core.
            NativeChipArt.TitlePlate(Rect.MinMaxRect(r.x+l.TitleX-7,r.y+bw+3,r.xMax-bw-3,dy-3),cart,rare,a);
            // The core: the glass sheen on the highlight's engage (and with the gleam), charged toward white.
            float sheen=float.NaN;if(!calm){if(Cart.Sheen(age,out float sx))sheen=sx;else if((legendary||mythic)&&Cart.Sheen(gleam-Cart.GleamEvery*.68f,out sx))sheen=sx;else if(Cart.Sheen(sweep,out sx))sheen=sx;}
            float hot=Mathf.Min(1,charge);NativeChipArt.Core(coreRect,cart,NativeIcons.Chip(c.id),rare,light,.3f*hot,sheen,t,calm,a);
            // The ignition (the reveal): a ring of the core's light bursting out as it lights.
            if(built<1&&built>.38f&&built<.8f){float u=(built-.38f)/.42f,rr=core*(.5f+.7f*u);U.Glow(new Rect(cc.x-rr*.5f,cc.y-rr*.5f,rr,rr),A(U.Hex(cart.CoreHi),.7f*(1-u)),rr*.5f);}
            // The holo scan crosses the face over the core but under every line of text.
            if(scan)NativeChipArt.HoloScan(r,s.Radius,tier,rare,holo,age,look.Calm);
            // The grade (the tier's label in its tracking, L44T's glow, its rank pips) and the name (Legendary's gold and Mythic's
            // foil gradients, Siren's glitch split).
            float tx=r.x+l.TitleX,tw=r.xMax-s.Pad-tx,ty=r.y+l.TitleY;string grade=Grade(tier);float gw=U.Measure(grade,s.Label,true,true,Tracking(tier)).x;
            U.Text(new Rect(tx,ty-1,gw+4,s.Label+2),grade,s.Label,A(LabelOf(tier,rare)),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:Tracking(tier),glow:tier=="l44t"?U.Hex(0x39ff7a,.7f*a):default,shadow:A(Color.black,.5f));
            if(cart.Rank>0&&tw-gw-8>6*5)NativeChipArt.Pips(tx+gw+8,ty+s.Label/2,cart.Rank,rare,Mathf.Round(s.Label*.55f),a);
            float ny=ty+s.Label+7;
            for(int i=0;i<l.Name.Count;i++)
            {
                var nr=new Rect(tx,ny+i*s.Name*s.NameLine,tw+4,s.Name*s.NameLine);
                if(siren&&!look.Plain){float g=Mathf.Repeat(t,4.2f)/4.2f;var block=new Rect(tx,ny,tw+4,l.Name.Count*s.Name*s.NameLine);U.SplitCopy(block,nr,g,false,l.Name[i],s.Name,a,TextAnchor.MiddleLeft);U.SplitCopy(block,nr,g,true,l.Name[i],s.Name,a,TextAnchor.MiddleLeft);}
                if((legendary||mythic)&&!look.Plain)GradientName(nr,l.Name[i],s.Name,legendary,a);
                else U.Text(nr,l.Name[i],s.Name,A(legendary?U.Hex(0xffd28c):siren?U.Hex(0xf6f3ff):U.Hex(0xf3f6fb)),TextAnchor.MiddleLeft,bold:true,glow:legendary?A(U.Hex(0xf39a35),.5f):default,shadow:A(Color.black,.55f));
            }
            // Description and the stat rows on the body: each row a tick in its tone in the padding and a faint wash of it; a line
            // wider than the card (narrow trays) shrinks to fit, down to 80%. Over Siren's flames the text keeps a shadow.
            Color desc=siren?U.Hex(0xdcd7ef):U.Hex(0xc6cedb);float tx0=r.x+s.Pad+3,txw=r.width-2*s.Pad-6;Color textShade=A(Color.black,siren?1:.45f);
            for(int i=0;i<l.Desc.Count;i++)U.Text(new Rect(tx0,r.y+l.DescTop+i*s.Desc*s.DescLine,txw,s.Desc*s.DescLine),l.Desc[i],s.Desc,A(desc),TextAnchor.MiddleLeft,shadow:textShade);
            var stats=Lines(c);float rh=s.Stat*1.22f;
            for(int i=0;i<stats.Length;i++)
            {
                var stl=stats[i];bool neutral=stl.tone!="good"&&stl.tone!="bad";Color sc=stl.tone=="good"?U.Hex(0x5bea86):stl.tone=="bad"?U.Hex(0xff5c69):U.Hex(0xb9bcc8);var row=new Rect(tx0,r.y+l.StatsTop+i*(rh+1),txw,rh);
                U.HGradient(new Rect(row.x-5,row.y+1,txw*.72f,rh-1),A(sc,neutral?.05f:.09f),Color.clear);U.Fill(new Rect(row.x-5,row.y+2.5f,2*p1,rh-4),A(sc,neutral?.5f:.9f));
                float fit=s.Stat*Mathf.Clamp(txw/Mathf.Max(1,U.Measure(stl.text,s.Stat,true).x),.8f,1);U.Text(row,stl.text,fit,A(sc),TextAnchor.MiddleLeft,bold:true,glow:neutral?default:A(sc,.2f),shadow:textShade);
            }
            // Footer (margin-top auto: at the bottom of a card taller than its content): the rule with the tier's accent, the tag on
            // the left; INSTALL / ×c → ×c+1 / ×n INSTALLED and the stack slots on the right.
            U.Fill(new Rect(r.x+bw+1,fy,r.width-2*bw-2,p1),siren?A(U.Hex(0xb7a8ff,.2f)):A(new Color(1,1,1,.09f)));U.Fill(new Rect(r.x+bw+1,fy,Mathf.Min(28,r.width*.12f),p1),A(U.Hex(cart.Accent),.8f));fy+=6;
            var foot=new Rect(tx0,fy,txw,Mathf.Max(8,s.Foot*1.2f)+1);
            int max=Math.Min(12,Math.Max(1,c.max)),have=Math.Min(max,look.Chosen?copies+1:copies);string count=CountText(copies,c.max,look.Chosen);Color footC=siren?U.Hex(0xb9b2d6):U.Hex(0xa3acbd);
            Color footShade=siren?A(Color.black):default;U.Text(foot,Tag(c),s.Foot,A(footC),TextAnchor.MiddleLeft,mono:true,tracking:.06f,shadow:footShade);U.Text(foot,count,s.Foot,A(footC),TextAnchor.MiddleRight,mono:true,tracking:.06f,shadow:footShade);
            if(c.max<=6){float sw=max*7-2,sx=foot.xMax-U.Measure(count,s.Foot,false,true,.06f).x-9-sw,sy=foot.center.y-4;
                for(int i=0;i<max;i++){var slot=new Rect(sx+i*7,sy,5,8);if(i<have){U.Fill(slot,A(rare));U.Fill(new Rect(slot.x,slot.y,slot.width,p1),A(Color.white,.4f));}else{U.Fill(slot,A(new Color(1,1,1,.1f)));U.Fill(new Rect(slot.x,slot.yMax-p1,slot.width,p1),A(new Color(1,1,1,.14f)));}
                    if(i==have&&look.Enabled&&!look.Chosen){float pulse=.5f-.5f*Mathf.Cos(t*2*Mathf.PI/1.4f);U.Fill(slot,A(rare,.55f*pulse));U.Round(slot,Color.clear,1,A(rare),1);}}}
            string credit=NativeMods.Credit(c);if(credit.Length>0)
            {
                var creditRect=new Rect(tx0,foot.yMax+3,txw,Mathf.Max(8,s.Foot*1.3f));
                var wrapped=U.Wrap(credit,s.Foot,txw,false,1);if(wrapped.Count>0)U.Text(creditRect,wrapped[0],s.Foot,A(footC),TextAnchor.MiddleLeft,shadow:footShade);
            }
            // Legendary's embers off the crest and Mythic's shards round the bezel.
            NativeChipArt.Embers(r,cart,t,calm||look.Plain,a);NativeChipArt.Shards(r,s.Radius,cart,t,calm||look.Plain,a);
            // Another card highlighted: this one recedes under a veil (kept under Reduced effects; the tray's badges stay over it).
            if(look.Recede>0)NativeChipArt.Package(r,s.Radius,0,A(U.Hex(0x05060a),NativeHoloScan.Veil*Mathf.Min(1,look.Recede)));
            // The focus ring (the holo scan's edge is under the core, above); the Siren pick's number badge over it; then the
            // scan's brackets outside, or the keyboard focus's still brackets while the pointer is on another card, so the badge
            // never hides the top-right bracket.
            if(!look.Ghost&&look.Focus&&holo<=0)NativeChipArt.PackageEdge(r,s.Radius,7,A(rare),2);
            if(look.Badge>0)Badge(r,look.Badge,a);
            if(scan)NativeChipArt.HoloBrackets(r,tier,rare,look.Holo,a,age,t,look.Calm);
            if(look.Ghost&&!look.Plain)NativeChipArt.HoloGhost(r,tier,rare,a*(1-Mathf.Clamp01(look.Holo)));
            if(look.Chosen)NativeChipArt.PackageEdge(r,s.Radius,6,A(rare),2);
        }
        // The Siren pick's number badge (its 1-9 keys, pve/chip-screen.ts): a light 22 px chamfered badge with the dark number over
        // the card's top-right corner (the core holds the top-left one).
        private static readonly string[] Numbers={"0","1","2","3","4","5","6","7","8","9"};
        private static void Badge(Rect r,int number,float a)
        {
            var b=new Rect(r.xMax-15,r.y-7,22,22);U.Glow(new Rect(b.x,b.y+2,b.width,b.height),new Color(0,0,0,.6f*a),5);NativeHudChrome.Cut(b,U.Hex(0xf1f2f6,a),5);U.Text(b,Numbers[Mathf.Clamp(number,0,9)],12,U.Hex(0x0f1015,a),TextAnchor.MiddleCenter,bold:true,mono:true);
        }
        // Legendary: linear-gradient(180deg, #fff2cb, #ffc96d 55%, #f39a35) clipped to the name (with its warm glow); Mythic:
        // linear-gradient(100deg, #ffb3bd, #ffe39a 30%, #a8ffd0 55%, #9fd8ff 75%, #e2b6ff). Drawn in bands of the gradient.
        private static readonly float[] GoldAt={0,.55f,1},FoilAt={0,.3f,.55f,.75f,1};
        private static readonly Color[] Gold={U.Hex(0xfff2cb),U.Hex(0xffc96d),U.Hex(0xf39a35)},Foil={U.Hex(0xffb3bd),U.Hex(0xffe39a),U.Hex(0xa8ffd0),U.Hex(0x9fd8ff),U.Hex(0xe2b6ff)};
        private static Color Gradient(float u,float[] at,Color[] colors){for(int i=1;i<at.Length;i++)if(u<=at[i])return Color.Lerp(colors[i-1],colors[i],(u-at[i-1])/(at[i]-at[i-1]));return colors[colors.Length-1];}
        private static void GradientName(Rect nr,string str,float size,bool gold,float a)
        {
            const int Bands=5;float w=Mathf.Min(nr.width,U.Measure(str,size,true).x+2);
            U.Text(nr,str,size,U.A(gold?Gold[1]:Foil[1],a),TextAnchor.MiddleLeft,bold:true,glow:gold?U.Hex(0xf39a35,.5f*a):default,shadow:new Color(0,0,0,.55f*a));
            for(int b=0;b<Bands;b++)
            {
                // Gold runs down the glyphs' height (the outer bands reach past it for descenders and accents); the foil across.
                float u=(b+.5f)/Bands,y0=b==0?nr.y-2:nr.y+nr.height*(.12f+.76f*b/Bands),y1=b==Bands-1?nr.yMax+2:nr.y+nr.height*(.12f+.76f*(b+1)/Bands);
                var band=gold?Rect.MinMaxRect(nr.x-2,y0,nr.xMax+2,y1):new Rect(nr.x+w*b/Bands,nr.y-2,w/Bands,nr.height+4);
                U.BeginBand(band);U.Text(nr,str,size,U.A(Gradient(u,gold?GoldAt:FoilAt,gold?Gold:Foil),a),TextAnchor.MiddleLeft,bold:true);U.EndBand();
            }
        }
        // Per-tier power-on reveal (chip-cards.css draft-reveal keyframes) at `t` seconds after the card's own delay (the draft
        // tray and the Siren reveal; CSS 3D turns are drawn as their cos() squash). Build 0..1 is the cartridge's assembly over
        // the same duration (ChipLook.Unbuilt = 1 - Build: the bezel snapping in, the core igniting, the connector lighting).
        public struct Reveal {public float Alpha,Dx,Dy,Scale,ScaleX,ScaleY,Degrees,Bright,Build;}
        public static float RevealSeconds(string tier)=>tier=="epic"?.9f:tier=="legendary"||tier=="mythic"?1:tier=="mishima"?1.2f:tier=="ansatsuken"?1.4f:tier=="l44t"?1.5f:tier=="siren"?1.25f:.62f;
        public static Reveal Entrance(string tier,float t)
        {
            float duration=RevealSeconds(tier);
            var v=new Reveal{Alpha=1,Scale=1,ScaleX=1,ScaleY=1,Bright=1,Build=1};if(t>=duration+1){return v;}float k=M.Clamp01(t/duration),e=M.Bezier(.2f,.8f,.2f,1,k);
            if(t<0){v.Alpha=0;v.Build=0;return v;}v.Build=k;
            // Core flicker over 1 s: dark until 22%, flash 1.7 at 28%, .35 at 33%, 1.35 at 41%, settling to 1.
            v.Bright=M.Keys(M.Clamp01(t),FlickerAt,FlickerValue);
            switch(tier)
            {
                case "epic":{float ang=M.Keys(e,At3a,EpicTurn);v.ScaleX=Mathf.Max(.02f,Mathf.Abs(Mathf.Cos(ang*Mathf.Deg2Rad)));v.Alpha=M.Clamp01(e*2.5f);break;}
                case "legendary":v.Dy=M.Keys(e,At3a,LegendaryDrop);v.Degrees=M.Keys(e,At3a,LegendaryTurn);v.Scale=M.Keys(e,At3a,LegendaryScale);v.Alpha=M.Clamp01(e*2.5f);break;
                case "mythic":{v.Scale=M.Keys(e,At3b,MythicScale);float ang=M.Keys(e,At3b,MythicTurn);v.ScaleX=Mathf.Max(.02f,Mathf.Abs(Mathf.Cos(ang*Mathf.Deg2Rad)));v.Alpha=M.Clamp01(e*3);break;}
                case "mishima":v.Dy=M.Keys(e,At3c,MishimaRise);v.Scale=M.Keys(e,At3c,MishimaScale);v.Alpha=M.Clamp01(e*2.5f);break;
                case "ansatsuken":{v.Dy=40*(1-e);float ang=-30*(1-e);v.ScaleX=Mathf.Abs(Mathf.Cos(ang*Mathf.Deg2Rad));v.Bright*=1+(1-e);v.Alpha=M.Clamp01(e*2.5f);break;}
                case "l44t":v.Dy=M.Keys(e,At3d,LeetRise);v.Scale=M.Keys(e,At3d,LeetScale);v.Bright*=1+2*(1-M.Clamp01(e/.35f));v.Alpha=M.Clamp01(e*3);break;
                case "siren":v.ScaleY=Mathf.Lerp(.02f,1,M.Clamp01(e/.4f));v.ScaleX=Mathf.Lerp(1.12f,1,M.Clamp01(e/.4f));v.Dx=e<.75f&&e>.4f?(Mathf.Repeat(e*40,2)<1?7:-7)*(1-e):0;v.Alpha=M.Clamp01(e*3);break;
                default:v.Dy=34*(1-e);v.Scale=.94f+.06f*e;v.ScaleY=Mathf.Cos(18*(1-e)*Mathf.Deg2Rad);v.Alpha=e;break;
            }
            return v;
        }
        // The keyframes (static: an entrance builds no arrays per frame).
        private static readonly float[] FlickerAt={0,.22f,.28f,.33f,.41f,1},FlickerValue={.12f,.12f,1.7f,.35f,1.35f,1},At3a={0,.6f,1},At3b={0,.5f,1},At3c={0,.55f,1},At3d={0,.35f,1};
        private static readonly float[] EpicTurn={90,-8,0},LegendaryDrop={-70,7,0},LegendaryTurn={-6,0,0},LegendaryScale={.8f,1.035f,1},MythicScale={.15f,1.1f,1},MythicTurn={180,-12,0},MishimaRise={28,-3,0},MishimaScale={.9f,1.02f,1},LeetRise={50,-12,0},LeetScale={.68f,1.09f,1};
    }
}
