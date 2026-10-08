using System;
using System.Collections.Generic;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The loadout pickers of Play vs Bots, Test Boss, the room lobby and Customize: the source's YOUR CLASS row
    // (main.ts classOptions, .class-option in style.css, its lobby.css and Customize variants) and, under it, the weapon
    // cards of that class drawn as the source's specialization chips (.spec-grid .chip in style.css; compact cards in
    // Customize, where the source has a weapon list). The source picks the weapon in a timed in-match phase; the menus pick
    // it here. The class is UI state only: it filters the cards, and a pick sets prefs.weapon, the one loadout field that is
    // saved and sent to rooms. The body reserves each picker's slot with GetRect in every event, so the layout and repaint
    // passes match; the cards are drawn and hit-tested in the body's deferred kit replay (U.Defer), in its physical-pixel
    // clip, at the slot's place in the scroll content.
    public sealed partial class ConversionGame
    {
        private enum ClassCardStyle{Stacked,Lobby,Customize}
        private string menuClass;
        private readonly List<string> menuClasses=new List<string>(4);
        private readonly List<ContentWeapon> menuWeapons=new List<ContentWeapon>(8);
        private readonly List<float> loadoutRows=new List<float>(8);
        // Each picker's slot width from its last repaint, per screen: the layout pass needs the height before IMGUI has
        // given the slot its width (the first frame, still inside the panel's fade-in, uses an estimate).
        private readonly Dictionary<int,float> loadoutWidth=new Dictionary<int,float>();

        private ContentWeapon MenuWeapon(){foreach(var w in content.weapons)if(w.id==prefs.weapon)return w;return null;}
        // The highlighted class: the one last picked while it still holds the weapon, otherwise the weapon's own class (DEV
        // for a developer's restricted weapon).
        private string MenuClass()
        {
            var w=MenuWeapon();bool dev=account.Developer;
            if(menuClass==null||!NativeLoadoutText.Offered(menuClass,dev)||!NativeLoadoutText.InClass(w,menuClass,dev,w!=null&&AllowedWeapon(w)))menuClass=NativeLoadoutText.ClassOf(w,dev);
            return menuClass;
        }
        // The side effect the weapon grid always had: a host's own seat is re-broadcast to the room.
        private void SetWeapon(string id){if(id==null||id==prefs.weapon)return;prefs.weapon=id;if(Hosting&&arena==null)LobbyBroadcast();}
        // In a panel body the slot is inset 4 px at each side, the margin the form's labels and buttons keep, so the cards
        // line up with them; in the room lobby it is flush with the pilot cards above.
        private float LoadoutSlot(int key,float estimate,out Rect slot,float height)
        {
            float inset=menuScreen==MenuScreen.Lobby?0:4;var r=GUILayoutUtility.GetRect(0,height,GUILayout.ExpandWidth(true));slot=new Rect(r.x+inset,r.y,r.width-2*inset,r.height);var ev=Event.current.type;
            if(ev==EventType.Layout)return loadoutWidth.TryGetValue(key,out float known)?known:estimate;
            if(ev==EventType.Repaint)loadoutWidth[key]=slot.width;return slot.width;
        }

        // ---- Class row -------------------------------------------------------------------------------------------------
        // .options: three equal columns (gap 10, 6 under 580 px) with the DEV card (developers) wrapping to a second row;
        // cards in a row share the tallest height. Clicking plays the ui sound; a weapon outside the class gives way to the
        // class default (preferredWeapon).
        private void ClassRow(string label,ClassCardStyle style)
        {
            Field(label);MenuClasses();
            int key=(int)menuScreen*8+(int)style;bool lobby=style==ClassCardStyle.Lobby;float top=lobby?2:style==ClassCardStyle.Customize?4:6,bottom=lobby?0:2,gap=U.W<=580?6:10,e=GUI.color.a;
            float estimate=style==ClassCardStyle.Lobby?860:style==ClassCardStyle.Customize?486:576;float known=loadoutWidth.TryGetValue(key,out float k)?k:estimate;
            float width=LoadoutSlot(key,estimate,out var slot,top+ClassGrid(style,known,gap)+bottom);
            U.Defer(()=>ClassCards(slot,width,top,gap,style,e));
        }
        private void MenuClasses(){bool dev=account.Developer;menuClasses.Clear();foreach(var c in NativeLoadoutText.Classes)if(NativeLoadoutText.Offered(c,dev))menuClasses.Add(c);}
        // The class cards in their slot (`width` wide, the cards `top` below its top edge), with their clicks.
        private void ClassCards(Rect slot,float width,float top,float gap,ClassCardStyle style,float e)
        {
            string active=MenuClass();bool dev=account.Developer,repaint=Event.current.type==EventType.Repaint;MenuClasses();ClassGrid(style,width,gap);
            float cw=(width-2*gap)/3,y=slot.y+top;
            for(int i=0;i<menuClasses.Count;i++)
            {
                if(i>0&&i%3==0)y+=loadoutRows[i/3-1]+gap;string c=menuClasses[i];var r=new Rect(slot.x+(i%3)*(cw+gap),y,cw,loadoutRows[i/3]);
                if(repaint)DrawClassCard(r,c,style,c==active,U.Hover(r),e);
                if(U.Button(r)){Click();menuClass=c;SetWeapon(NativeLoadoutText.Pick(content.weapons,prefs.weapon,c,dev,AllowedWeapon));}
            }
        }
        // Row heights of the class grid at `width` (into loadoutRows); returns the grid's height.
        private float ClassGrid(ClassCardStyle style,float width,float gap)
        {
            float cw=(width-2*gap)/3,total=0;loadoutRows.Clear();
            for(int i=0;i<menuClasses.Count;i++){float h=ClassCard(style,cw,menuClasses[i],out _);if(i%3==0)loadoutRows.Add(h);else loadoutRows[i/3]=Mathf.Max(loadoutRows[i/3],h);}
            for(int i=0;i<loadoutRows.Count;i++)total+=loadoutRows[i]+(i>0?gap:0);return total;
        }
        // The card's metrics: Stacked (.class-option: 16 px padding, 48 px symbol box with a 38 px icon, 17 px name, 12 px
        // tagline at least 2.6em tall), Lobby (lobby.css: 32 px icon beside the two text rows, 16 px side padding; 9 px
        // above and below rather than 11, so the row fits the space the roster keeps under the pilot cards) and
        // Customize (14 px padding, 28 px symbol box with a 26 px icon, 10 px tagline). Under 580 px the names are 14 px and
        // the taglines 10 px (the stacked card also pads 12 / 10 px). Returns the natural height.
        private struct ClassMetrics{public float PadT,PadX,Name,Tag,TagMin;public List<string> Lines;}
        private static float ClassCard(ClassCardStyle style,float w,string c,out ClassMetrics m)
        {
            bool small=U.W<=580;m=new ClassMetrics{Name=small?14:17};
            if(style==ClassCardStyle.Lobby){m.PadT=9;m.PadX=16;m.Tag=small?10:12;m.Lines=U.Wrap(NativeLoadoutText.Tagline(c),m.Tag,w-80);return 2+2*m.PadT+Mathf.Max(40,m.Name*1.21f+3+m.Lines.Count*m.Tag*1.21f);}
            if(style==ClassCardStyle.Customize){m.PadT=m.PadX=14;m.Tag=10;m.TagMin=26;}else{m.PadT=small?12:16;m.PadX=small?10:16;m.Tag=small?10:12;m.TagMin=m.Tag*2.6f;}
            m.Lines=U.Wrap(NativeLoadoutText.Tagline(c),m.Tag,w-2-2*m.PadX);float box=style==ClassCardStyle.Customize?28+8:48+10;
            return 1+m.PadT+box+9+m.Name*1.21f+9+Mathf.Max(m.TagMin,m.Lines.Count*m.Tag*1.21f)+m.PadT+1;
        }
        // Normal #222329 / #484952; hover snaps to #303138 / #ff784b (no transition in the source); active #39251f with the
        // #ff6b35 edge and its 3 px inset bar along the bottom, winning over hover. The icon and tagline stay #ff996f.
        private static void DrawClassCard(Rect r,string c,ClassCardStyle style,bool active,bool hot,float e)
        {
            ClassCard(style,r.width,c,out var m);
            Color fill=active?U.Hex(0x39251f):hot?U.Hex(0x303138):U.Hex(0x222329),edge=active?U.Hex(0xff6b35):hot?U.Hex(0xff784b):U.Hex(0x484952);
            U.Round(r,U.A(fill,e),4,U.A(edge,e));if(active)U.Fill(new Rect(r.x+2,r.yMax-1-3,r.width-4,3),U.Hex(0xff6b35,e));
            Color warm=U.Hex(0xff996f,e),ink=U.Hex(0xf5f5f7,e);float nl=m.Name*1.21f,tl=m.Tag*1.21f;string icon=NativeIcons.Class(c),name=NativeLoadoutText.Name(c);
            if(style==ClassCardStyle.Lobby)
            {
                float tb=nl+3+m.Lines.Count*tl,mid=r.center.y,tx=r.x+63,tw=r.width-80;NativeIcons.Draw(new Rect(r.x+17,mid-16,32,32),icon,warm);
                float y=mid-tb/2;U.Text(new Rect(tx,y,tw,nl),name,m.Name,ink,TextAnchor.MiddleLeft,bold:true);y+=nl+3;
                foreach(var line in m.Lines){U.Text(new Rect(tx,y,tw,tl),line,m.Tag,warm,TextAnchor.MiddleLeft);y+=tl;}
                return;
            }
            bool custom=style==ClassCardStyle.Customize;float size=custom?26:38,top=r.y+1+m.PadT,x=r.x+1+m.PadX,cw=r.width-2-2*m.PadX;
            NativeIcons.Draw(new Rect(r.center.x-size/2,top+(custom?1:5),size,size),icon,warm);
            float ny=top+(custom?36:58)+9;U.Text(new Rect(x,ny,cw,nl),name,m.Name,ink,TextAnchor.MiddleLeft,bold:true);
            float ty=ny+nl+9;foreach(var line in m.Lines){U.Text(new Rect(x,ty,cw,tl),line,m.Tag,warm,TextAnchor.MiddleLeft);ty+=tl;}
        }

        // ---- Weapon cards ----------------------------------------------------------------------------------------------
        // The class's weapons in content (source) order. Full cards: three columns from a 900 px slot, two from 520, else
        // one; 18 px between columns and 25 between rows (the source's single-column row gap, which keeps the pin rows of
        // stacked chips apart); the slot keeps 16 px above and below for the gold pins and the ledge.
        // Compact (Customize): two columns of 72 px cards (one under 440 px), gap 10.
        private void WeaponCards(string label,bool compact=false)
        {
            Field(label);MenuWeapons();
            int key=(int)menuScreen*8+(compact?5:4);float top=compact?6:16,bottom=compact?10:16,e=GUI.color.a,estimate=compact?486:menuScreen==MenuScreen.Lobby?860:576;
            float known=loadoutWidth.TryGetValue(key,out float k)?k:estimate;
            float width=LoadoutSlot(key,estimate,out var slot,top+SpecGrid(known,compact,out _,out _,out _)+bottom);
            U.Defer(()=>SpecCards(slot,width,top,compact,e));
        }
        // The weapons of the highlighted class, in content order (into menuWeapons).
        private void MenuWeapons(){string cls=MenuClass();bool dev=account.Developer;menuWeapons.Clear();foreach(var w in content.weapons)if(NativeLoadoutText.InClass(w,cls,dev,AllowedWeapon(w)))menuWeapons.Add(w);}
        // The weapon cards in their slot, with their clicks; the class is read again here, after the class cards' clicks.
        private void SpecCards(Rect slot,float width,float top,bool compact,float e)
        {
            MenuWeapons();SpecGrid(width,compact,out int cols,out float gap,out float rowGap);bool repaint=Event.current.type==EventType.Repaint;
            float cw=(width-(cols-1)*gap)/cols,y=slot.y+top;bool narrow=cols<=2||U.W<=850;
            for(int i=0;i<menuWeapons.Count;i++)
            {
                if(i>0&&i%cols==0)y+=loadoutRows[i/cols-1]+rowGap;var w=menuWeapons[i];var r=new Rect(slot.x+(i%cols)*(cw+gap),y,cw,loadoutRows[i/cols]);
                bool selected=w.id==prefs.weapon;int index=Array.IndexOf(content.weapons,w);
                if(compact)
                {
                    if(repaint)DrawCompactWeapon(r,w,SpecCard(w,cw,2),selected,U.Hover(r),e);
                    if(U.Button(r)&&!selected){Click();SetWeapon(w.id);}
                }
                else
                {
                    float h=U.HoverAmount(7400+index,r);if(repaint)DrawSpecCard(r,w,SpecCard(w,cw,narrow?1:0),selected,h,U.Hover(r),e);
                    if(U.Button(r)&&!selected){nativeAudio?.Play("install",null,.4f);SetWeapon(w.id);}
                }
            }
        }
        // Row heights of the weapon grid at `width` (into loadoutRows); returns the grid's height.
        private float SpecGrid(float width,bool compact,out int cols,out float gap,out float rowGap)
        {
            if(compact){cols=width<440?1:2;gap=rowGap=10;}else{cols=width>=900?3:width>=520?2:1;gap=18;rowGap=25;}
            float cw=(width-(cols-1)*gap)/cols,total=0;int mode=compact?2:cols<=2||U.W<=850?1:0;loadoutRows.Clear();
            for(int i=0;i<menuWeapons.Count;i++){float h=SpecCard(menuWeapons[i],cw,mode).Height;if(i%cols==0)loadoutRows.Add(h);else loadoutRows[i/cols]=Mathf.Max(loadoutRows[i/cols],h);}
            for(int i=0;i<loadoutRows.Count;i++)total+=loadoutRows[i]+(i>0?rowGap:0);return total;
        }
        // A card's wrapped text and natural height, cached per weapon, width, density and UI scale. Mode 0: the source's
        // 3-column metrics (padding 23 / 18 / 18, 18 px name); 1: its narrow metrics (padding 18 / 13, 16 px name);
        // 2: the compact card (stat line only, at most two lines).
        private sealed class SpecText{public List<string> Name,Desc,Stats;public float Height,PadT,PadX,PadB,NameSize;}
        private static readonly Dictionary<(string,int,int),SpecText> specText=new Dictionary<(string,int,int),SpecText>();
        private static SpecText SpecCard(ContentWeapon w,float width,int mode)
        {
            var key=(w.id,Mathf.RoundToInt(width*4)*4+mode,Mathf.RoundToInt(U.S*1000));if(specText.TryGetValue(key,out var t))return t;if(specText.Count>512)specText.Clear();
            if(mode==2){t=new SpecText{Stats=StatLines(NativeLoadoutText.Stats(w),10,width-78,2),Height=72};specText[key]=t;return t;}
            t=new SpecText{PadT=mode==1?18:23,PadX=mode==1?13:18,PadB=18,NameSize=mode==1?16:18};float tw=width-4-2*t.PadX;
            t.Name=U.Wrap(w.name,t.NameSize,tw,true);t.Desc=U.Wrap(NativeLoadoutText.Description(w),14,tw);t.Stats=StatLines(NativeLoadoutText.Stats(w),11,tw);
            // min-height 265 (border box): 2 + padding + the 50 px icon well and its 20 px margin, the name (line-height 1.2),
            // 10 px, the description (14 px, line-height 1.6), 12 px, then the stat line pinned to the bottom.
            t.Height=Mathf.Max(265,2+t.PadT+50+20+t.Name.Count*t.NameSize*1.2f+10+t.Desc.Count*22.4f+12+t.Stats.Count*13+t.PadB+2);
            specText[key]=t;return t;
        }
        // The stat line (mono, .1em tracking) wrapped between its " · " parts; `max` lines, the last cut with an ellipsis.
        private static List<string> StatLines(string text,float size,float width,int max=int.MaxValue)
        {
            var lines=new List<string>();string line="";
            foreach(var part in text.Split(new[]{" · "},StringSplitOptions.None)){string next=line.Length==0?part:line+" · "+part;if(line.Length>0&&U.Measure(next,size,false,true,.1f).x>width){lines.Add(line);line=part;}else line=next;}
            if(line.Length>0)lines.Add(line);
            if(lines.Count>max){string last=string.Join(" · ",lines.GetRange(max-1,lines.Count-max+1));while(last.Length>1&&U.Measure(last+"…",size,false,true,.1f).x>width)last=last.Substring(0,last.Length-1).TrimEnd();lines.RemoveRange(max-1,lines.Count-max+1);lines.Add(last+"…");}
            return lines;
        }
        // .spec-grid .chip: square #24252c body with a 2 px #585a66 edge, a hard 7 px #081b22 ledge, gold pin rows
        // (#b99b5e, 6 px on / 7 px off, 9 px tall) above and below, the 50 px #17181e icon well (4 px ring, #666874 edge,
        // #ffc1a5 icon), the name, the description in #bfc0c9 and the #ff996f mono stat line pinned to the bottom. Hover
        // lifts the whole chip 5 px and warms the body to #30313a over .15 s (ease); the edge snaps to #ff6b35. The menu's
        // selected card borrows the class card's active look: #39251f, #ff6b35 edge and a 3 px inset bar.
        private static void DrawSpecCard(Rect r,ContentWeapon w,SpecText t,bool selected,float hover,bool hot,float e)
        {
            float k=M.Ease(hover);var d=new Rect(r.x,r.y-5*k,r.width,r.height);
            U.Fill(new Rect(d.x,d.yMax,d.width,7),U.Hex(0x081b22,e));
            U.Fill(d,selected?U.Hex(0x39251f,e):U.A(U.Mix(U.Hex(0x24252c),U.Hex(0x30313a),k),e));
            Color edge=selected||hot?U.Hex(0xff6b35,e):U.Hex(0x585a66,e);
            U.Fill(new Rect(d.x,d.y,d.width,2),edge);U.Fill(new Rect(d.x,d.yMax-2,d.width,2),edge);U.Fill(new Rect(d.x,d.y+2,2,d.height-4),edge);U.Fill(new Rect(d.xMax-2,d.y+2,2,d.height-4),edge);
            if(selected)U.Fill(new Rect(d.x+2,d.yMax-2-3,d.width-4,3),U.Hex(0xff6b35,e));
            Color pin=U.Hex(0xb99b5e,e);for(float px=d.x+12;px<d.xMax-12;px+=13){float pw=Mathf.Min(6,d.xMax-12-px);U.Fill(new Rect(px,d.y-9,pw,9),pin);U.Fill(new Rect(px,d.yMax,pw,9),pin);}
            float cx=d.x+2+t.PadX,cy=d.y+2+t.PadT,tw=d.width-4-2*t.PadX,nl=t.NameSize*1.2f;
            U.Round(new Rect(cx-4,cy-4,58,58),U.Hex(0x17181e,e),14);U.Round(new Rect(cx,cy,50,50),U.Hex(0x17181e,e),10,U.Hex(0x666874,e));
            NativeIcons.Draw(new Rect(cx+8,cy+8,34,34),NativeIcons.Spec(w.id),U.Hex(0xffc1a5,e));
            float y=cy+50+20;foreach(var line in t.Name){U.Text(new Rect(cx,y,tw,nl),line,t.NameSize,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true);y+=nl;}y+=10;
            foreach(var line in t.Desc){U.Text(new Rect(cx,y,tw,22.4f),line,14,U.Hex(0xbfc0c9,e),TextAnchor.MiddleLeft);y+=22.4f;}
            float sy=d.yMax-2-t.PadB-t.Stats.Count*13;foreach(var line in t.Stats){U.Text(new Rect(cx,sy,tw+8,13),line,11,U.Hex(0xff996f,e),TextAnchor.MiddleLeft,mono:true,tracking:.1f);sy+=13;}
        }
        // The compact card in Customize (the source lists names in a select): the class card's palette and states, the
        // spec chip's icon well (40 px, 26 px icon), the name (15 px) and up to two stat lines (10 px mono), centred.
        private static void DrawCompactWeapon(Rect r,ContentWeapon w,SpecText t,bool selected,bool hot,float e)
        {
            Color fill=selected?U.Hex(0x39251f):hot?U.Hex(0x303138):U.Hex(0x222329),edge=selected?U.Hex(0xff6b35):hot?U.Hex(0xff784b):U.Hex(0x484952);
            U.Round(r,U.A(fill,e),4,U.A(edge,e));if(selected)U.Fill(new Rect(r.x+2,r.yMax-1-3,r.width-4,3),U.Hex(0xff6b35,e));
            var well=new Rect(r.x+13,r.center.y-20,40,40);U.Round(well,U.Hex(0x17181e,e),10,U.Hex(0x666874,e));NativeIcons.Draw(new Rect(well.x+7,well.y+7,26,26),NativeIcons.Spec(w.id),U.Hex(0xffc1a5,e));
            float tx=r.x+65,tw=r.width-78,y=r.center.y-(18+4+t.Stats.Count*12)/2;
            U.Text(new Rect(tx,y,tw,18),Fit(w.name,15,tw,true,0),15,U.Hex(0xf5f5f7,e),TextAnchor.MiddleLeft,bold:true);y+=22;
            foreach(var line in t.Stats){U.Text(new Rect(tx,y,tw+8,12),line,10,U.Hex(0xff996f,e),TextAnchor.MiddleLeft,mono:true,tracking:.1f);y+=12;}
        }
    }
}
