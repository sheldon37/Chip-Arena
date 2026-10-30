using System;
using System.Collections.Generic;
using UnityEngine;
using G=ChipArena.UnityConversion.NativeLoadoutGrid;
using T=ChipArena.UnityConversion.NativeCyberTiles;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The loadout pickers of Play vs Bots, Test Boss, Create / Join Room, the room lobby and the Locker's Loadout tab, as compact
    // cyber tiles (owner 2026-10-12: "a animated cyber effect over the choices and just the icon + name of class under it",
    // cyan instead of orange): YOUR CLASS as one row of class tiles (the source's classOptions) and, under it, the class's
    // weapons in content order as a grid of square tiles, with the info strip that shows the hovered weapon's role and key
    // stats (the equipped one's, dimmed, otherwise). The class is UI state only: it filters the tiles, and a pick sets
    // prefs.weapon, the one loadout field that is saved and sent to rooms. The body reserves each picker's slot with GetRect in
    // every event, so the layout and repaint passes match; the tiles are drawn and hit-tested in the body's deferred kit replay
    // (U.Defer), in its physical-pixel clip, at the slot's place in the scroll content. Geometry and the motion timeline:
    // Core/NativeLoadoutGrid; the drawing: NativeCyberTiles.
    public sealed partial class ConversionGame
    {
        // Stacked: a panel body (Play vs Bots, Test Boss, Create / Join Room); Lobby: the room lobby's roster column;
        // Customize: the Locker's Loadout tab (its own hover and click gates).
        private enum ClassCardStyle{Stacked,Lobby,Customize}
        private string menuClass;
        private readonly List<string> menuClasses=new List<string>(4);
        private readonly List<ContentWeapon> menuWeapons=new List<ContentWeapon>(8);
        // Each picker's slot width from its last repaint, per screen: the layout pass needs the height before IMGUI has
        // given the slot its width (the first frame, still inside the panel's fade-in, uses an estimate).
        private readonly Dictionary<int,float> loadoutWidth=new Dictionary<int,float>();
        // Per picker: what it shows (a hash of the class and its tiles), when it last painted and when its entrance began. Per
        // tile (by its hover id): its last hover level and paint, when the pointer arrived and when it was selected.
        private sealed class PickerFx{public int Sig;public float Seen=-10,Start;}
        private sealed class TileFx{public float Hover,Seen=-10,HoverAt=-10,SelectAt=-10;public bool Selected;}
        private readonly Dictionary<int,PickerFx> pickerFx=new Dictionary<int,PickerFx>();
        private readonly Dictionary<int,TileFx> tileFx=new Dictionary<int,TileFx>();
        // The info strip: the weapon it shows and since when, how lit it is (a hovered weapon) and the notch's eased x.
        private string stripWeapon;private float stripAt=-10,stripLive,stripNotch=-1,stripSeen=-10;
        private static readonly Dictionary<string,string> classLabels=new Dictionary<string,string>();
        private readonly Dictionary<(string,int,int),string> stripStats=new Dictionary<(string,int,int),string>();

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
        // In a panel body the slot is inset 4 px at each side, the margin the form's labels and buttons keep, so the tiles
        // line up with them; in the room lobby (and the Locker) LoadoutEdge, which keeps the selection's outer brackets inside
        // the scroll clip.
        private const float LoadoutEdge=5;
        private float LoadoutSlot(int key,float estimate,out Rect slot,float height)
        {
            float inset=menuScreen==MenuScreen.Lobby?LoadoutEdge:4;var r=GUILayoutUtility.GetRect(0,height,GUILayout.ExpandWidth(true));slot=new Rect(r.x+inset,r.y,r.width-2*inset,r.height);var ev=Event.current.type;
            if(ev==EventType.Layout)return loadoutWidth.TryGetValue(key,out float known)?known:estimate;
            if(ev==EventType.Repaint)loadoutWidth[key]=slot.width;return slot.width;
        }
        private static G.Row RowOf(ClassCardStyle s)=>s==ClassCardStyle.Lobby?G.Row.Lobby:s==ClassCardStyle.Customize?G.Row.Locker:G.Row.Panel;
        private static bool LoadoutNarrow=>U.W<=580;
        // The picker's entrance age: it restarts when the picker was not painted for a moment (its screen opened again) or
        // shows another set of tiles (a class switch).
        private float PickerAge(int key,int sig,float now,bool repaint)
        {
            if(!pickerFx.TryGetValue(key,out var p)){p=new PickerFx();pickerFx[key]=p;}
            if(repaint){if(now-p.Seen>.3f||p.Sig!=sig){p.Start=now;p.Sig=sig;}p.Seen=now;}
            return now-p.Start;
        }
        // A tile's timeline: the pointer's arrival (from fully off, or a tile not painted for a moment) and the pick; a tile
        // found selected as its picker enters locks once its entrance is mostly up (`lockAt`).
        private TileFx TileState(int id,bool selected,bool hot,float level,float now,float lockAt,bool repaint)
        {
            if(!tileFx.TryGetValue(id,out var f)){f=new TileFx();tileFx[id]=f;}
            if(!repaint)return f;bool fresh=now-f.Seen>.3f;f.Seen=now;
            if(hot&&(f.Hover<=.02f||fresh))f.HoverAt=now;f.Hover=level;
            if(selected&&(!f.Selected||fresh))f.SelectAt=fresh?lockAt:now;f.Selected=selected;return f;
        }
        private T.Look TileLook(TileFx f,float alpha,float hover,bool selected,float now,int col,int row,Color accent,bool mystery,bool dev)
            =>new T.Look{Alpha=alpha,Hover=hover,HoverAge=now-f.HoverAt,Selected=selected?1:0,SelectAge=now-f.SelectAt,Time=now,Col=col,Row=row,Calm=prefs.reducedEffects,Mystery=mystery,Dev=dev,Accent=accent};
        private static string ClassTileLabel(string c){if(!classLabels.TryGetValue(c??"",out var s)){s=NativeLoadoutText.Name(c).ToUpperInvariant();classLabels[c??""]=s;}return s;}

        // ---- Class row -------------------------------------------------------------------------------------------------
        // One row of equal class tiles (two columns on a very narrow slot), the DEV class (developers) last. Clicking plays
        // the ui sound; a weapon outside the class gives way to the class default (preferredWeapon).
        private void ClassRow(string label,ClassCardStyle style)
        {
            Field(label);MenuClasses();
            int key=(int)menuScreen*8+(int)style;bool lobby=style==ClassCardStyle.Lobby;float top=lobby?4:6,bottom=lobby?4:10,e=GUI.color.a,estimate=lobby?860:576;
            float known=loadoutWidth.TryGetValue(key,out float k)?k:estimate;
            float width=LoadoutSlot(key,estimate,out var slot,top+G.Classes(known,menuClasses.Count,RowOf(style),LoadoutNarrow).Height+bottom);
            U.Defer(()=>ClassTiles(new Rect(slot.x,slot.y+top,slot.width,slot.height-top-bottom),width,style,key,e));
        }
        private void MenuClasses(){bool dev=account.Developer;menuClasses.Clear();foreach(var c in NativeLoadoutText.Classes)if(NativeLoadoutText.Offered(c,dev))menuClasses.Add(c);}
        // The Locker's class row and weapon tiles: their reserved heights (at the Locker's width less LoadoutEdge each side) and
        // their draws into those slots, with the Locker's gates.
        private float LockerClassHeight=>G.Classes(lockerW-2*LoadoutEdge,menuClasses.Count,G.Row.Locker,LoadoutNarrow).Height;
        private float LockerWeaponHeight=>G.WeaponsHeight(lockerW-2*LoadoutEdge,menuWeapons.Count,LoadoutNarrow);
        private void LockerClassTiles(Rect slot,float e)=>ClassTiles(new Rect(slot.x+LoadoutEdge,slot.y,lockerW-2*LoadoutEdge,slot.height),lockerW-2*LoadoutEdge,ClassCardStyle.Customize,(int)MenuScreen.Customize*8+(int)ClassCardStyle.Customize,e);
        private void LockerWeaponTiles(Rect slot,float e)=>SpecCards(new Rect(slot.x+LoadoutEdge,slot.y,lockerW-2*LoadoutEdge,slot.height),lockerW-2*LoadoutEdge,(int)MenuScreen.Customize*8+4,ClassCardStyle.Customize,e);
        // The class tiles from `r`'s top-left, `width` wide, with their clicks. In the Locker the pointer must be over its view
        // and no confirmation open; there a click on the shown class does nothing (the menus' always re-applies it).
        private void ClassTiles(Rect r,float width,ClassCardStyle style,int key,float e)
        {
            string active=MenuClass();bool dev=account.Developer,repaint=Event.current.type==EventType.Repaint,locker=style==ClassCardStyle.Customize,calm=prefs.reducedEffects;MenuClasses();
            var row=RowOf(style);var g=G.Classes(width,menuClasses.Count,row,LoadoutNarrow);float now=Time.unscaledTime;int sig=menuClasses.Count;
            float age=PickerAge(key,sig,now,repaint);
            for(int i=0;i<menuClasses.Count;i++)
            {
                string c=menuClasses[i];var cell=G.At(g,i);var tile=new Rect(r.x+cell.X,r.y+cell.Y,cell.W,cell.H);bool on=c==active,hot=(!locker||lockerViewHot)&&U.Hover(tile);
                int id=(locker?8600:7420)+i;float h=U.HoverAmount(id,tile,hot),enter=G.Enter(age,i,calm);var f=TileState(id,on,hot,h,now,now-age+G.LockAfterEnter(i,calm),repaint);
                if(repaint&&enter>0)
                {
                    bool mystery=c=="dev";var look=TileLook(f,e*enter,h,on,now,G.Col(g,i),G.RowOf(g,i),mystery?NativeCyberTheme.Violet():NativeCyberTheme.Cyan(),mystery,false);
                    T.Class(new Rect(tile.x,tile.y+G.Rise*(1-enter),tile.width,tile.height),look,row,NativeIcons.Class(c),ClassTileLabel(c));
                }
                if(locker){if(!lockerBlocked&&hot&&U.Button(tile)&&!on){Click();menuClass=c;SetWeapon(NativeLoadoutText.Pick(content.weapons,prefs.weapon,c,dev,AllowedWeapon));}}
                else if(U.Button(tile)){Click();menuClass=c;SetWeapon(NativeLoadoutText.Pick(content.weapons,prefs.weapon,c,dev,AllowedWeapon));}
            }
        }

        // ---- Weapon tiles ----------------------------------------------------------------------------------------------
        // The class's weapons in content order as square tiles (NativeLoadoutGrid.Weapons: the same tile size for every class
        // at a given width), the info strip under them; the slot keeps 8 px above for the selection's brackets and glow.
        private void WeaponCards(string label)
        {
            Field(label);MenuWeapons();
            int key=(int)menuScreen*8+4;float top=8,bottom=6,e=GUI.color.a,estimate=menuScreen==MenuScreen.Lobby?860:576;
            float known=loadoutWidth.TryGetValue(key,out float k)?k:estimate;
            float width=LoadoutSlot(key,estimate,out var slot,top+G.WeaponsHeight(known,menuWeapons.Count,LoadoutNarrow)+bottom);
            U.Defer(()=>SpecCards(new Rect(slot.x,slot.y+top,slot.width,slot.height-top-bottom),width,key,ClassCardStyle.Stacked,e));
        }
        // The weapons of the highlighted class, in content order (into menuWeapons).
        private void MenuWeapons(){string cls=MenuClass();bool dev=account.Developer;menuWeapons.Clear();foreach(var w in content.weapons)if(NativeLoadoutText.InClass(w,cls,dev,AllowedWeapon(w)))menuWeapons.Add(w);}
        // The weapon tiles from `r`'s top-left, `width` wide, with their clicks, then the info strip; the class is read again
        // here, after the class tiles' clicks. In the Locker the pointer must be over its view, no confirmation open, and the
        // hovered weapon previews on the pilot (lockerWeapon).
        private void SpecCards(Rect r,float width,int key,ClassCardStyle style,float e)
        {
            MenuWeapons();bool repaint=Event.current.type==EventType.Repaint,locker=style==ClassCardStyle.Customize,calm=prefs.reducedEffects;var g=G.Weapons(width,menuWeapons.Count,LoadoutNarrow);
            float now=Time.unscaledTime;int sig=menuWeapons.Count;foreach(var w in menuWeapons)sig=sig*31+(w.id?.GetHashCode()??0);float age=PickerAge(key,sig,now,repaint);
            ContentWeapon hovered=null,equipped=null;float hoveredX=-1,equippedX=-1;
            for(int i=0;i<menuWeapons.Count;i++)
            {
                var w=menuWeapons[i];var cell=G.At(g,i);var tile=new Rect(r.x+cell.X,r.y+cell.Y,cell.W,cell.H);bool selected=w.id==prefs.weapon,hot=(!locker||lockerViewHot)&&U.Hover(tile);
                int index=Array.IndexOf(content.weapons,w),id=(locker?8500:7400)+index;float h=U.HoverAmount(id,tile,hot),enter=G.Enter(age,i,calm);var f=TileState(id,selected,hot,h,now,now-age+G.LockAfterEnter(i,calm),repaint);
                if(repaint&&enter>0)T.Weapon(new Rect(tile.x,tile.y+G.Rise*(1-enter),tile.width,tile.height),TileLook(f,e*enter,h,selected,now,G.Col(g,i),G.RowOf(g,i),U.Hex((int)w.color),false,w.devOnly),NativeIcons.Spec(w.id),w.name);
                if(hot){hovered=w;hoveredX=tile.center.x;}if(selected){equipped=w;equippedX=tile.center.x;}
                if(locker){if(hot&&repaint)lockerWeapon=w.id;if(!lockerBlocked&&lockerViewHot&&U.Button(tile)){Click();if(!selected){nativeAudio?.Play("install",null,.4f);SetWeapon(w.id);}}}
                else if(U.Button(tile)&&!selected){nativeAudio?.UiCue("ui.loadout.select","install",.4f);SetWeapon(w.id);}
            }
            if(repaint)WeaponStrip(new Rect(r.x,r.y+g.Height+G.StripGap,width,G.StripHeight),hovered??equipped,hovered!=null,hovered!=null?hoveredX:equippedX,equipped,e*G.Enter(age,0,calm),now);
        }
        // The info strip (repaint): the shown weapon cross-fades in when it changes; the strip lights and its notch follows the
        // hovered tile, resting dimmed on the equipped weapon when nothing is hovered.
        private void WeaponStrip(Rect s,ContentWeapon w,bool live,float x,ContentWeapon equipped,float a,float now)
        {
            float dt=Mathf.Clamp(now-stripSeen,0,.1f);if(now-stripSeen>.3f){stripLive=live?1:0;stripNotch=-1;stripWeapon=null;}stripSeen=now;
            string id=w?.id;if(id!=stripWeapon){stripWeapon=id;stripAt=now;}
            stripLive=prefs.reducedEffects?(live?1:0):G.StripStep(stripLive,live,dt);if(live)stripNotch=prefs.reducedEffects?x:G.Follow(stripNotch,x,dt);
            float fade=prefs.reducedEffects?1:.35f+.65f*Mathf.Clamp01((now-stripAt)/G.StripFade);if(w==null){T.Strip(s,null,null,null,false,stripLive,0,stripNotch,a);return;}
            var key=(w.id,Mathf.RoundToInt(s.width*4),Mathf.RoundToInt(U.S*1000));if(!stripStats.TryGetValue(key,out string stats)){if(stripStats.Count>64)stripStats.Clear();var l=StatLines(NativeLoadoutText.Stats(w),10.5f,s.width-24,1);stats=l.Count>0?l[0]:"";stripStats[key]=stats;}
            T.Strip(s,w.name,NativeLoadoutText.Role(w),stats,w==equipped,stripLive,fade,stripNotch,a);
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
    }
}
