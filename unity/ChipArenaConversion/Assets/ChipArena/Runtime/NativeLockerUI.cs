using System;
using System.Collections.Generic;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The Locker: the Customize screen (the home Customize tile and the room lobby's Customize button) as one full-screen
    // view. A category rail at the left (Core/NativeLocker.cs: Appearance, Kill Effects, Entrance, Loadout, Weapon Skins), the
    // live 3D pilot on a turntable in the centre (the menu portrait renderer, NativePortraits.cs: drag to turn, an idle sway,
    // every change and every hovered colour, hat, pose or weapon shown live), the category's item cards at the right, and
    // Save Pilot / Revert / Preview Intro along the foot.
    // What is saved and sent is unchanged: the same prefs fields, SavePilot, the room's "profile" message and the host's
    // LobbyBroadcast; every cosmetic list goes through NativeCosmeticRules.Allowed, so restricted items stay hidden. The
    // Locker adds an unsaved-changes state only: the look on entry is the baseline, Revert restores it, and leaving with
    // changes (Escape, the close button) asks first. Kit-drawn cards sit in the screen's GUILayout scroll view (the text fields
    // are GUILayout) through one cached deferred draw, so a frame allocates no closures; nothing inside the scroll clip is
    // drawn rotated or scaled (a transformed draw would carry the clip with it), so the emblems animate by moving rects.
    public sealed partial class ConversionGame
    {
        private int lockerTab,lockerTabNext=-1,lockerN;private bool lockerConfirm,lockerBlocked,lockerViewHot,lockerTurned,lockerSavedAccount;
        private float lockerFade=1,lockerW=500,lockerYaw,lockerTurnAt=-9,lockerDragX,lockerSavedAt=-9,lockerListsAt=-9,lockerKillAt;
        private readonly Vector2[] lockerScroll=new Vector2[NativeLocker.Count];private readonly Rect[] lockerSlots=new Rect[24];private Action lockerDraw;
        // This repaint's hovered item, shown on the pilot (null: the equipped one).
        private string lockerHat,lockerPose,lockerBody,lockerAccent,lockerWeapon,lockerKill,lockerBack;
        private readonly List<string> lockerHats=new List<string>(16),lockerKills=new List<string>(32),lockerIntros=new List<string>(32),lockerSkins=new List<string>(16);
        private readonly Dictionary<string,ContentWeapon> lockerIntroWeapon=new Dictionary<string,ContentWeapon>();private NativeAccess lockerAccess;private string lockerAbilitySpec,lockerNameRaw,lockerName="Pilot";
        private readonly List<NativeLocker.AbilityOption> lockerQ=new List<NativeLocker.AbilityOption>(4),lockerE=new List<NativeLocker.AbilityOption>(4);
        private readonly Dictionary<(int,string),string> lockerText=new Dictionary<(int,string),string>();private GUIStyle lockerField,lockerHex;private string lockerKillsLabel="",lockerSkinsLabel="",lockerAbilityHint;
        private struct LockerLook{public string Name,Weapon,Pose,Body,Accent,Hat,Kill,Intro,IntroText;public bool Override;public string Back,Skins,HatColor,BackColor;}
        private LockerLook lockerBase;
        private static readonly float[] LkGlowAt={0f,.6f,1f},LkGlowA={1f,.25f,0f},LkRingAt={0f,.9f,.955f,1f},LkRingA={0f,0f,1f,0f},LkDiscAt={0f,.96f,1f},LkDiscA={1f,1f,0f};

        // ---- Baseline, Save, Revert, leaving -------------------------------------------------------------------------
        private LockerLook LockerNow()=>new LockerLook{Name=prefs.name,Weapon=prefs.weapon,Pose=prefs.lobbyPose,Body=prefs.bodyColor,Accent=prefs.accentColor,Hat=prefs.hat,Kill=prefs.killEffect,Intro=prefs.intro,IntroText=prefs.introText,Override=prefs.overrideIntro,Skins=prefs.weaponSkins,Back=prefs.lobbyBack,HatColor=prefs.hatColor,BackColor=prefs.backColor};
        private bool LockerDirty()
        {var b=lockerBase;return b.Name!=prefs.name||b.Weapon!=prefs.weapon||b.Pose!=prefs.lobbyPose||b.Body!=prefs.bodyColor||b.Accent!=prefs.accentColor||b.Hat!=prefs.hat||b.Kill!=prefs.killEffect||b.Intro!=prefs.intro||b.IntroText!=prefs.introText||b.Override!=prefs.overrideIntro||b.Back!=prefs.lobbyBack||b.Skins!=prefs.weaponSkins||b.HatColor!=prefs.hatColor||b.BackColor!=prefs.backColor;}
        // An account profile loaded while the Locker is open becomes its baseline (it is the saved look).
        private void LockerRebase(){if(menuScreen==MenuScreen.Customize)lockerBase=LockerNow();}
        // Go(Customize) from another screen (Preview Intro's way back keeps the session).
        private void LockerEnter()
        {
            lockerBase=LockerNow();lockerConfirm=false;lockerTabNext=-1;lockerSavedAt=-9;lockerYaw=0;lockerTurnAt=-9;lockerTurned=false;lockerListsAt=-9;lockerAbilitySpec=null;
            for(int i=0;i<lockerScroll.Length;i++)lockerScroll[i]=Vector2.zero;
        }
        private void LockerRevert()
        {
            var b=lockerBase;bool weapon=prefs.weapon!=b.Weapon;
            prefs.name=b.Name;prefs.weapon=b.Weapon;prefs.lobbyPose=b.Pose;prefs.bodyColor=b.Body;prefs.accentColor=b.Accent;prefs.hat=b.Hat;prefs.killEffect=b.Kill;prefs.intro=b.Intro;prefs.introText=b.IntroText;prefs.overrideIntro=b.Override;prefs.lobbyBack=b.Back;prefs.weaponSkins=b.Skins;prefs.hatColor=b.HatColor;prefs.backColor=b.BackColor;
            GUIUtility.keyboardControl=0;
            // A host's weapon pick re-broadcasts its seat (SetWeapon); putting the weapon back does the same.
            if(weapon&&Hosting&&arena==null)LobbyBroadcast();
        }
        // Save Pilot: the old Customize action unchanged (SavePilot stores prefs and the account profile; in a room the profile
        // message goes to the host; a host re-broadcasts the lobby). The Locker then stays open on the saved look.
        private void LockerSave()
        {
            SavePilot();if(server!=null&&InRoom)SendServer(new NativeNetMessage{type="profile",profile=Profile(),token=account?.Token});if(Hosting)LobbyBroadcast();
            lockerBase=LockerNow();lockerSavedAt=Time.unscaledTime;lockerSavedAccount=account?.Account!=null;
        }
        // Escape and the close button: close the question, ask when something is unsaved, else leave (to the room in a room).
        private void LockerBack(){if(lockerConfirm){lockerConfirm=false;return;}if(LockerDirty())lockerConfirm=true;else LockerLeave();}
        private void LockerLeave(){lockerConfirm=false;Go(InRoom?MenuScreen.Lobby:MenuScreen.Home);}

        // ---- Lists ---------------------------------------------------------------------------------------------------
        // What this player may pick: rebuilt on the layout pass when the account's access changes and every two seconds (grants
        // can expire), never between a frame's layout and repaint.
        private void LockerLists()
        {
            if(Event.current.type!=EventType.Layout&&lockerHats.Count>0)return;var access=account?.Access;float now=Time.unscaledTime;
            if(lockerHats.Count>0&&ReferenceEquals(access,lockerAccess)&&now>=lockerListsAt&&now-lockerListsAt<2)return;lockerAccess=access;lockerListsAt=now;
            lockerHats.Clear();foreach(var id in NativeCosmeticRules.Hats)if(NativeCosmeticRules.Allowed(access,"hat",id))lockerHats.Add(id);
            lockerKills.Clear();foreach(var id in NativeCosmeticRules.Kills)if(NativeCosmeticRules.Allowed(access,"effect",id))lockerKills.Add(id);
            lockerIntros.Clear();foreach(var id in NativeCosmeticRules.Intros)if(NativeCosmeticRules.Allowed(access,"intro",id))lockerIntros.Add(id);
            foreach(var id in lockerIntros)if(!lockerIntroWeapon.ContainsKey(id)&&id.StartsWith("weapon-",StringComparison.Ordinal)){var w=LockerWeaponById(id.Substring(7));if(w!=null)lockerIntroWeapon[id]=w;}
            // The body card first (Core/NativeBodySkins.cs: the Sheldon Clothes need live DEV access), then the weapons.
            lockerBodySheldon=NativeCosmeticRules.Allowed(access,"body",NativeBodySkins.SheldonClothes);lockerSkins.Clear();lockerSkins.Add(NativeBodySkins.Entry);
            foreach(var w in content.weapons)if(AllowedWeapon(w)&&NativeWeaponSkins.Skinned(w.id))lockerSkins.Add(w.id);
            lockerKillsLabel=lockerKills.Count+" EFFECTS";lockerSkinsLabel="BODY + "+(lockerSkins.Count-1)+" WEAPONS";
        }
        private void LockerAbilities()
        {
            if(Event.current.type!=EventType.Layout&&lockerAbilitySpec!=null)return;var w=MenuWeapon();string id=w?.id??"";if(id==lockerAbilitySpec)return;lockerAbilitySpec=id;
            NativeLocker.SlotOptions(content,w,"q",lockerQ);NativeLocker.SlotOptions(content,w,"e",lockerE);lockerAbilityHint=w==null?null:w.name+" · Q and E";
        }
        // Hook for Astra's loadout field: the option equipped in a weapon slot. Ability choices are not stored anywhere yet (no
        // profile, account or seat field and no host validation), so each slot shows its first content option as equipped and
        // the others cannot be picked. When the field exists, read it here and set LockerAbilityPicks to let a click store it.
        private int LockerEquippedAbility(ContentWeapon w,string slot,List<NativeLocker.AbilityOption> options)=>0;
        private static bool LockerAbilityPicks=>false;
        // Cached display strings, keyed by value (no per-frame concatenation): 0 a cosmetic's name, 1 an entrance's name, 2 a
        // weapon's name in capitals, 3 its stat line, 4 its signature entrance's card text, 5 that entrance's name.
        private string LockerString(int kind,string id)
        {
            var key=(kind,id);if(lockerText.TryGetValue(key,out var s))return s;var w=kind>=2?LockerWeaponById(id):null;
            switch(kind)
            {
                case 0:s=NativeLocker.Clean(NativeCosmeticRules.Label(id));break;
                case 1:s=NativeLocker.IntroName(id,NativeCosmeticRules.Label(id));break;
                case 2:s=w==null?"":w.name.ToUpperInvariant();break;
                case 3:s=w==null?"":NativeLoadoutText.Stats(w);break;
                case 4:s=NativeLocker.Clean(NativeCosmeticRules.Label(LockerSignature(id)))+", your "+(w?.name??"weapon")+"'s own entrance.";break;
                case 5:s=NativeLocker.Clean(NativeCosmeticRules.Label(LockerSignature(id)));break;
                default:s=id??"";break;
            }
            if(lockerText.Count>512)lockerText.Clear();lockerText[key]=s;return s;
        }
        private ContentWeapon LockerWeaponById(string id){if(id!=null)foreach(var w in content.weapons)if(w.id==id)return w;return null;}
        private ContentWeapon LockerShownWeapon()=>LockerWeaponById(lockerWeapon??prefs.weapon)??LockerWeaponById("pistol")??content.weapons[0];
        // #rrggbb to a colour without allocating (HexColor cuts a substring).
        private static Color LkColor(string value,int fallback)=>U.Hex(NativePortraitRules.Parse(value,fallback));
        // The entrance NativeIntroRules.Style plays without an override: the weapon's own (the DEV kits' and the Siren's).
        private static string LockerSignature(string spec)=>spec=="siren"?"dev-siren":spec=="emperor"?"dev-emperor":spec=="ansatsuken"?"dev-ansatsuken":spec=="leet"?"dev-matrix":"weapon-"+spec;
        private void LockerStyles()
        {
            if(lockerField!=null)return;lockerField=new GUIStyle(GUI.skin.textField){fontSize=16,alignment=TextAnchor.MiddleLeft,fixedHeight=40};lockerField.padding=new RectOffset(12,12,9,9);lockerField.margin=new RectOffset(0,0,4,4);
            lockerHex=new GUIStyle(lockerField){fontSize=14,font=U.MonoFont,fixedHeight=34};lockerHex.padding=new RectOffset(10,10,7,7);lockerHex.margin=new RectOffset(0,0,0,0);
        }

        // ---- Screen --------------------------------------------------------------------------------------------------
        private void LockerScreen(float age)
        {
            var ev=Event.current;if(ev.type==EventType.Layout&&lockerTabNext>=0){lockerTab=lockerTabNext;lockerTabNext=-1;}
            LockerStyles();LockerLists();LockerAbilities();if(prefs.name!=lockerNameRaw){lockerNameRaw=prefs.name;lockerName=NativeCosmeticRules.Name(prefs.name);}
            if(ev.type==EventType.Repaint){lockerHat=lockerPose=lockerBody=lockerAccent=lockerWeapon=lockerBack=lockerHatColor=lockerBackColor=null;lockerBodyTry=null;}
            float W=U.W,H=U.H,e=In(age,0,.26f),t=Time.unscaledTime;bool tight=H<720,compact=W<1320;lockerFade=e;
            float total=Mathf.Min(W-56,1760);var shell=new Rect((W-total)/2,80+10*(1-e),total,H-62-80);
            bool dirty=LockerDirty();lockerBlocked=lockerConfirm;if(lockerConfirm)menuBlockedNow=true;
            U.Glow(new Rect(shell.x,shell.y+30,shell.width,shell.height),new Color(0,0,0,.45f*e),60);U.Backdrop(shell,10,16,e);
            U.RoundGradient(shell,U.Hex(0x25212b,.96f*e),U.Hex(0x17191f,.97f*e),10,U.Hex(0x45464e,e),1,true);
            float pad=24,head=tight?76:88;
            U.Text(new Rect(shell.x+pad,shell.y+22,400,16),"CUSTOMIZATION",12,U.Hex(0xc0c1ca,e),TextAnchor.MiddleLeft,bold:true,tracking:.2f);
            U.Text(new Rect(shell.x+pad,shell.y+40,400,tight?28:36),"Locker",tight?24:30,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true,tracking:-.03f);
            var close=new Rect(shell.xMax-pad-40,shell.y+20,40,40);float ch=U.HoverAmount(9104,close,!lockerBlocked);
            U.Round(close,U.A(U.Mix(U.Hex(0x222329),U.Hex(0x303138),ch),e),4,U.A(U.Mix(U.Hex(0x45464e),U.Hex(0xff6b35),ch),e));NativeIcons.Draw(new Rect(close.center.x-10.5f,close.center.y-10.5f,21,21),"x",U.Hex(0xf4f4f5,e));
            if(!lockerBlocked&&U.Button(close)){Click();LockerBack();}
            var bar=new Rect(shell.x+pad,shell.yMax-pad-(tight?42:46),shell.width-2*pad,tight?42:46);
            float top=shell.y+head,bottom=bar.y-(tight?20:26),gap=compact?16:20,railW=compact?84:184,inner=shell.width-2*pad-railW-2*gap,stageW=Mathf.Clamp(inner*.38f,250,540);
            var rail=new Rect(shell.x+pad,top,railW,bottom-top);var column=new Rect(rail.xMax+gap,top,stageW,bottom-top);var right=new Rect(column.xMax+gap,top,shell.xMax-pad-column.xMax-gap,bottom-top);
            LockerRail(rail,e,compact);
            // The category's header, then its cards (scrolling).
            int tab=lockerTab;U.Text(new Rect(right.x,right.y-4,right.width,28),NativeLocker.Labels[tab],tight?18:21,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true);
            U.Text(new Rect(right.x,right.y+(tight?20:24),right.width,18),NativeLocker.Subtitles[tab],13,U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft);
            LockerEquippedTag(new Rect(right.x,right.y-2,right.width,24),tab,e);
            float lead=tight?50:58;LockerBody(new Rect(right.x,right.y+lead,right.width,right.height-lead),e);
            float plate=tight?60:86;LockerStage(new Rect(column.x,column.y,column.width,column.height-plate-8),new Rect(column.x,column.yMax-plate,column.width,plate),e,t);
            LockerBar(bar,e,dirty,compact);
            if(lockerConfirm)LockerConfirmDialog(shell,e);
        }
        // The equipped pick at the right of the category header (kill effect, entrance).
        private void LockerEquippedTag(Rect r,int tab,float e)
        {
            string value=tab==NativeLocker.KillEffects?LockerString(0,prefs.killEffect):tab==NativeLocker.Entrance?(prefs.overrideIntro?LockerString(1,prefs.intro):LockerString(5,prefs.weapon)):null;
            if(value==null)return;float vw=U.Measure(value,13,true).x;if(vw+90>r.width*.55f)return;
            U.Text(r,value,13,U.Hex(0xffc590,e),TextAnchor.MiddleRight,bold:true);U.Text(new Rect(r.x,r.y,r.width-vw-10,r.height),"EQUIPPED",10,U.Hex(0x8f909a,e),TextAnchor.MiddleRight,mono:true,tracking:.14f);
        }

        // ---- Rail ----------------------------------------------------------------------------------------------------
        private void LockerRail(Rect r,float e,bool compact)
        {
            float y=r.y,h=Mathf.Min(compact?64:52,(r.height-6*(NativeLocker.Count-1))/NativeLocker.Count);int current=lockerTabNext>=0?lockerTabNext:lockerTab;
            for(int i=0;i<NativeLocker.Count;i++)
            {
                var b=new Rect(r.x,y,r.width,h);bool on=i==current;float hv=on?0:U.HoverAmount(7950+i,b,!lockerBlocked);
                U.Round(b,on?U.Hex(0x3a2a26,e):U.Hex(0x2a2a32,.9f*hv*e),8,on?U.Hex(0xb07a5c,.85f*e):U.Hex(0x5a5262,hv*e));
                if(on)U.Fill(new Rect(b.x,b.y+12,3,b.height-24),U.Hex(0xff6b35,e));
                Color icon=on?U.Hex(0xff996f,e):U.A(U.Mix(U.Hex(0xa9a3b3),U.Hex(0xe6e1ea),hv),e),ink=on?U.Hex(0xfff1e5,e):U.A(U.Mix(U.Hex(0xc9c3d1),U.Hex(0xf4f4f5),hv),e);
                if(compact)
                {
                    float iy=b.y+Mathf.Max(6,(b.height-42)/2);NativeIcons.Draw(new Rect(b.center.x-10,iy,20,20),NativeLocker.Icons[i],icon);var l=U.Wrap(NativeLocker.Labels[i],10,b.width-6,true,1);
                    if(l.Count>0)U.Text(new Rect(b.x+3,iy+24,b.width-6,16),l[0],10,ink,TextAnchor.MiddleCenter,bold:true);
                    if(NativeLocker.Soon(i))U.Disc(new Rect(b.xMax-13,b.y+8,6,6),U.Hex(0x8f909a,e));
                }
                else
                {
                    NativeIcons.Draw(new Rect(b.x+16,b.center.y-10,20,20),NativeLocker.Icons[i],icon);bool soon=NativeLocker.Soon(i);
                    U.Text(new Rect(b.x+48,b.y+(soon?-8:0),b.width-56,b.height),NativeLocker.Labels[i],14,ink,TextAnchor.MiddleLeft,bold:true);
                    if(soon)U.Text(new Rect(b.x+48,b.center.y+3,b.width-56,14),"COMING SOON",9,U.Hex(0x8f909a,e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
                }
                if(!lockerBlocked&&U.Button(b)&&!on){lockerTabNext=i;Click();}
                y+=h+6;
            }
            if(compact)return;
            // Where Save puts the look.
            bool signedIn=account?.Account!=null;string note=signedIn?"Save Pilot stores your look on your account.":"Save Pilot stores your look on this PC. Log in to keep it everywhere.";
            var lines=U.Wrap(note,12,r.width-30,false,3);float ny=r.yMax-lines.Count*16-2;if(ny<y+20)return;
            U.Fill(new Rect(r.x,ny-14,r.width,1),U.Hex(0x4b414c,.6f*e));NativeIcons.Draw(new Rect(r.x,ny+1,14,14),signedIn?"shield-check":"circle-alert",U.Hex(0x8f909a,e));
            for(int i=0;i<lines.Count;i++)U.Text(new Rect(r.x+22,ny+i*16,r.width-22,16),lines[i],12,U.Hex(0x8f909a,e),TextAnchor.MiddleLeft);
        }

        // ---- Body: GUILayout pass (text fields, reserved slots) then one deferred kit draw --------------------------
        private const float LkGap=12,LkItemMin=168,LkItemH=168,LkVisual=86,LkRowMin=220,LkRowH=78,LkSkinMin=156,LkSkinH=150;
        private static int LkCols(float w,float min)=>Mathf.Max(1,Mathf.FloorToInt((w+LkGap)/(min+LkGap)));
        private static float LkGrid(float w,int count,float min,float h){int cols=LkCols(w,min),rows=(count+cols-1)/cols;return rows<=0?0:rows*h+(rows-1)*LkGap;}
        private Rect LkCell(Rect grid,int i,float min,float h){int cols=LkCols(lockerW,min);float w=(lockerW-(cols-1)*LkGap)/cols;return new Rect(grid.x+(i%cols)*(w+LkGap),grid.y+(i/cols)*(h+LkGap),w,h);}
        private void LkReserve(float h){var r=GUILayoutUtility.GetRect(0,h,GUILayout.ExpandWidth(true));if(lockerN<lockerSlots.Length)lockerSlots[lockerN++]=r;}
        private void LkHead(bool first=false){if(!first)GUILayout.Space(14);LkReserve(24);}
        private void LockerBody(Rect view,float e)
        {
            int tab=lockerTab;lockerViewHot=!lockerBlocked&&U.Hover(view);lockerW=view.width-14;lockerN=0;
            var c=GUI.color;GUI.color=new Color(c.r,c.g,c.b,c.a*e);bool enabled=GUI.enabled;GUI.enabled=!lockerBlocked;
            GUILayout.BeginArea(view);U.BeginDeferred();lockerScroll[tab]=GUILayout.BeginScrollView(lockerScroll[tab],false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(GUILayout.Width(lockerW));
            switch(tab)
            {
                case NativeLocker.Appearance:
                    LkHead(true);prefs.name=GUILayout.TextField(prefs.name??"",20,lockerField);LkHead();LkReserve(LkBodyRowH());
                    LkHead();LkSwatches(ref prefs.bodyColor,"#e5e7eb");LkHead();LkSwatches(ref prefs.accentColor,"#70e1d0");
                    LkHead();LkReserve(LkGrid(lockerW,lockerHats.Count,LkItemMin,LkItemH));LkHead();LkReserve(LkPickerH());LkHead();LkReserve(LkGrid(lockerW,NativeLobbyPoses.Keys.Length,LkRowMin,LkRowH));
                    LkHead();LkReserve(LkGrid(lockerW,NativePortraitBacks.Count,LkItemMin,LkItemH));LkHead();LkReserve(LkPickerH());break;
                case NativeLocker.KillEffects:LkHead(true);LkReserve(LkGrid(lockerW,lockerKills.Count,LkItemMin,LkItemH));break;
                case NativeLocker.Entrance:
                    LkHead(true);prefs.introText=GUILayout.TextField(prefs.introText??"Ready to make an impact",64,lockerField);LkReserve(18);
                    LkHead();LkReserve(LkGrid(lockerW,lockerIntros.Count+1,LkItemMin,LkItemH));break;
                case NativeLocker.Loadout:
                    MenuClasses();MenuWeapons();LkHead(true);LkReserve(LockerClassHeight+6);LkHead();LkReserve(LockerWeaponHeight+6);
                    LkHead();LkReserve(LkAbilityH(lockerQ));GUILayout.Space(LkGap);LkReserve(LkAbilityH(lockerE));LkReserve(40);break;
                default:LkSkinsLayout();break;
            }
            GUILayout.EndVertical();if(lockerDraw==null)lockerDraw=LockerDeferred;U.Defer(lockerDraw);GUILayout.Space(10);
            GUILayout.EndScrollView();GUILayout.EndArea();GUI.enabled=enabled;GUI.color=c;U.EndDeferred(view,lockerScroll[tab]);
        }
        // A colour row: twelve preset swatches and the #rrggbb field (the saved format) beside them.
        private void LkSwatches(ref string value,string fallback)
        {
            float field=106,avail=lockerW-field-24;int per=Mathf.Clamp(Mathf.FloorToInt((avail+8)/38),4,12),rows=(12+per-1)/per;float sw=per*38;
            GUILayout.BeginHorizontal();var r=GUILayoutUtility.GetRect(sw,rows*38-4,GUILayout.Width(sw));if(lockerN<lockerSlots.Length)lockerSlots[lockerN++]=r;GUILayout.Space(12);
            value=GUILayout.TextField(value??fallback,7,lockerHex,GUILayout.Width(field));GUILayout.FlexibleSpace();GUILayout.EndHorizontal();
        }
        private void LockerDeferred()
        {
            float e=lockerFade;int k=0;bool repaint=Event.current.type==EventType.Repaint;
            switch(lockerTab)
            {
                case NativeLocker.Appearance:
                {
                    LkHeading(lockerSlots[k++],"CALLSIGN","Shown over your pilot in rooms and intros");
                    // The body skin (Runtime/NativeLockerSkinsUI.cs: the Skins tab's choices, equipped body and setter).
                    LkHeading(lockerSlots[k++],"BODY SKIN","Hover a body to try it on");LkBodyRow(lockerSlots[k++]);
                    LkHeading(lockerSlots[k++],"BODY COLOUR","Hover a swatch to try it");LkSwatchDraw(lockerSlots[k++],true);
                    LkHeading(lockerSlots[k++],"ACCENT COLOUR","Trims, visor and glow");LkSwatchDraw(lockerSlots[k++],false);
                    LkHeading(lockerSlots[k++],"HEADWEAR",null);var grid=lockerSlots[k++];Color body=LkColor(prefs.bodyColor,0xe5e7eb),accent=LkColor(prefs.accentColor,0x70e1d0);
                    int hatTint=NativeHatLooks.Rgb(lockerHatColor??prefs.hatColor);
                    for(int i=0;i<lockerHats.Count;i++)
                    {
                        string id=lockerHats[i];var r=LkCell(grid,i,LkItemMin,LkItemH);bool on=id==prefs.hat;var v=LkCardBegin(r,8100+i,on,true,out float h,out bool hot);
                        if(repaint){LkHatVisual(v,id,e,h,body,accent,on?hatTint:-1);if(hot)lockerHat=id;}
                        if(LkCardEnd(r,v,h,hot,on,NativeLocker.DevItem(id),LockerString(0,id),NativeLocker.HatText(id),true)&&!on)prefs.hat=id;
                    }
                    // The hat's colour (NativeHatColorUI.cs): AUTO, swatches and a custom colour, tried on by hovering.
                    LkHeading(lockerSlots[k++],"HAT COLOUR",LkPickerHint(true));LkColorPicker(lockerSlots[k++],true);
                    LkHeading(lockerSlots[k++],"IDLE POSE","How your pilot waits in the lobby");grid=lockerSlots[k++];int pose=NativeLobbyPoses.Index(prefs.lobbyPose);
                    for(int i=0;i<NativeLobbyPoses.Keys.Length;i++)
                    {
                        var r=LkCell(grid,i,LkRowMin,LkRowH);bool on=i==pose;
                        if(LkRowCard(r,8200+i,on,NativeLobbyPoses.Icons[i],U.Hex(0xefb587),NativeLobbyPoses.Labels[i],NativeLocker.PoseText(i),null,out bool hot)&&!on)prefs.lobbyPose=NativeLobbyPoses.Keys[i];
                        if(hot&&repaint)lockerPose=NativeLobbyPoses.Keys[i];
                    }
                    // Backdrops (Core/NativePortraitBacks.cs): each card plays its style live behind a bust; hovering one tries it
                    // on the stage, a click equips it (prefs.lobbyBack, sent with the profile by Save Pilot).
                    LkHeading(lockerSlots[k++],"BACKDROP","The effect behind your pilot in lobbies");grid=lockerSlots[k++];int back=NativePortraitBacks.Index(prefs.lobbyBack);float now=Time.unscaledTime;
                    int backTint=NativeHatLooks.Rgb(lockerBackColor??prefs.backColor);Color backdrop=backTint<0?accent:U.Hex(backTint);
                    for(int i=0;i<NativePortraitBacks.Count;i++)
                    {
                        var r=LkCell(grid,i,LkItemMin,LkItemH);bool on=i==back;var v=LkCardBegin(r,8700+i,on,true,out float h,out bool hot);
                        if(repaint){LkBackVisual(v,i,e,h,body,accent,now+i*.37f,backdrop);if(hot)lockerBack=NativePortraitBacks.Keys[i];}
                        if(LkCardEnd(r,v,h,hot,on,false,NativePortraitBacks.Labels[i],NativePortraitBacks.Texts[i],true)&&!on)prefs.lobbyBack=NativePortraitBacks.Keys[i];
                    }
                    LkHeading(lockerSlots[k++],"BACKDROP COLOUR",LkPickerHint(false));LkColorPicker(lockerSlots[k++],false);
                    break;
                }
                case NativeLocker.KillEffects:
                {
                    LkHeading(lockerSlots[k++],lockerKillsLabel,"Hover a card to watch it play");var grid=lockerSlots[k++];
                    Color body=LkColor(prefs.bodyColor,0xe5e7eb),accent=LkColor(prefs.accentColor,0x70e1d0);float t=Time.unscaledTime;string hovered=null;
                    for(int i=0;i<lockerKills.Count;i++)
                    {
                        string id=lockerKills[i];var r=LkCell(grid,i,LkItemMin,LkItemH);bool on=id==prefs.killEffect;var v=LkCardBegin(r,8300+i,on,true,out float h,out bool hot);
                        if(hot){hovered=id;if(lockerKill!=id){lockerKill=id;lockerKillAt=t;}}
                        if(repaint){U.BeginBand(v);LkKillEmblem(v,id,hot?Mathf.Repeat((t-lockerKillAt)/2.8f,1):LkHero(id)+.03f*Mathf.Sin(t*1.3f+i),e,body,accent,t);U.EndBand();}
                        if(LkCardEnd(r,v,h,hot,on,NativeLocker.DevItem(id),LockerString(0,id),NativeLocker.KillText(id),true)&&!on)prefs.killEffect=id;
                    }
                    if(repaint&&hovered==null)lockerKill=null;
                    // Extension point: a Preview action on a hovered card that plays the effect on a dummy beside the pilot. The
                    // death effects are arena presentation (ragdoll bodies in the arena's physics scene, NativeDeathPresentation,
                    // the Raging Demon's cinematic camera), not portrait meshes, so that needs its own preview scene like
                    // StartPreview's intro arena; the emblems stand in until then.
                    break;
                }
                case NativeLocker.Entrance:
                {
                    LkHeading(lockerSlots[k++],"INTRO TEXT","Up to 64 characters");var hint=lockerSlots[k++];U.Text(new Rect(hint.x+2,hint.y,lockerW,hint.height),"Shown under your name while your entrance plays.",12,U.Hex(0x8f909a,e),TextAnchor.MiddleLeft);
                    LkHeading(lockerSlots[k++],"ENTRANCE","Preview Intro plays it on its own");var grid=lockerSlots[k++];float t=Time.unscaledTime;
                    var weapon=MenuWeapon();string spec=weapon?.id??prefs.weapon;
                    {
                        var r=LkCell(grid,0,LkItemMin,LkItemH);bool on=!prefs.overrideIntro;var v=LkCardBegin(r,8400,on,true,out float h,out bool hot);
                        if(repaint)LkIntroVisual(v,weapon!=null?NativeIcons.Spec(weapon.id):"sparkles",weapon!=null?U.Hex((int)weapon.color):U.Hex(0xff996f),e,h,t,0,true);
                        if(LkCardEnd(r,v,h,hot,on,false,"Weapon signature",LockerString(4,spec),true)&&!on)prefs.overrideIntro=false;
                    }
                    for(int i=0;i<lockerIntros.Count;i++)
                    {
                        string id=lockerIntros[i];var r=LkCell(grid,i+1,LkItemMin,LkItemH);bool on=prefs.overrideIntro&&id==prefs.intro;var v=LkCardBegin(r,8401+i,on,true,out float h,out bool hot);
                        if(repaint){string icon=NativeLocker.IntroIcon(id);Color tone=U.Hex(0x9fb4ff);if(icon==null&&lockerIntroWeapon.TryGetValue(id,out var w)){icon=NativeIcons.Spec(w.id);tone=U.Hex((int)w.color);}else if(NativeLocker.DevItem(id))tone=U.Hex(0xb7a8ff);LkIntroVisual(v,icon??"sparkles",tone,e,h,t,i*.17f,false);}
                        if(LkCardEnd(r,v,h,hot,on,NativeLocker.DevItem(id),LockerString(1,id),NativeLocker.IntroText(id),true)&&!on){prefs.overrideIntro=true;prefs.intro=id;}
                    }
                    break;
                }
                case NativeLocker.Loadout:
                {
                    LkHeading(lockerSlots[k++],"CLASS",null);LkClassRow(lockerSlots[k++],e);
                    LkHeading(lockerSlots[k++],"PREFERRED WEAPON","Your default in every match and room");var grid=lockerSlots[k++];
                    // The loadout's weapon tiles (NativeLoadoutUI: hover previews the weapon on the pilot, a pick plays install).
                    LockerWeaponTiles(new Rect(grid.x,grid.y+6,lockerW,grid.height-6),e);
                    var weapon=MenuWeapon();LkHeading(lockerSlots[k++],"ABILITIES",lockerAbilityHint);
                    LkAbilityRow(lockerSlots[k++],"q",lockerQ,weapon,e);LkAbilityRow(lockerSlots[k++],"e",lockerE,weapon,e);
                    var note=lockerSlots[k++];NativeIcons.Draw(new Rect(note.x+2,note.y+13,14,14),"circle-help",U.Hex(0x8f909a,e));
                    U.Text(new Rect(note.x+24,note.y+8,lockerW-24,24),"More abilities are coming for every slot. Your picks will be saved with your pilot.",12,U.Hex(0x8f909a,e),TextAnchor.MiddleLeft);
                    break;
                }
                default:LkSkins(lockerSlots[0],lockerSlots[1],lockerSlots[2],e);break;
            }
        }
        private void LkHeading(Rect r,string label,string hint)
        {
            float e=lockerFade;U.Text(new Rect(r.x+2,r.y,lockerW,r.height),label,11,U.Hex(0xc0c1ca,e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            if(hint==null)return;float lw=U.Measure(label,11,true,true,.14f).x;U.Text(new Rect(r.x+lw+16,r.y,lockerW-lw-16,r.height),hint,12,U.Hex(0x8f909a,e),TextAnchor.MiddleLeft);
        }

        // ---- Cards ---------------------------------------------------------------------------------------------------
        // An item card's frame (lifts on hover, warm double edge when equipped) and its visual well.
        private Rect LkCardBegin(Rect r,int key,bool on,bool interactive,out float hover,out bool hot)
        {
            float e=lockerFade;hot=interactive&&lockerViewHot&&U.Hover(r);hover=interactive?U.HoverAmount(key,r,hot):0;var d=new Rect(r.x,r.y-2*hover,r.width,r.height);
            if(on)U.Glow(new Rect(d.x+10,d.y+12,d.width-20,d.height-12),U.Hex(0xff6b35,.13f*e),14);
            U.Round(d,U.A(U.Mix(U.Hex(0x1d1e24),U.Hex(0x26272e),hover),e),10,on?U.Hex(0xff6b35,e):U.A(U.Mix(U.Hex(0x3b3c45),U.Hex(0x9a8a9e),hover),e),on?2:1);
            var v=new Rect(d.x+8,d.y+8,d.width-16,LkVisual);U.Round(v,U.Hex(0x121318,e),8);return v;
        }
        private bool LkCardEnd(Rect r,Rect v,float hover,bool hot,bool on,bool dev,string name,string text,bool interactive)
        {
            float e=lockerFade,tx=r.x+12,tw=r.width-24,y=v.yMax+10;
            if(dev)LkBadge(new Rect(v.x+8,v.y+8,34,17),"DEV",U.Hex(0x261f3a,e),U.Hex(0x8b7ad8,e),U.Hex(0xd9d2f0,e));
            if(on)LkTick(new Vector2(v.xMax-14,v.y+14),e);
            var n=U.Wrap(name,15,tw,true,1);if(n.Count>0)U.Text(new Rect(tx,y,tw,20),n[0],15,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true);y+=23;
            var lines=U.Wrap(text,12,tw,false,2);for(int i=0;i<lines.Count;i++)U.Text(new Rect(tx,y+i*16,tw,16),lines[i],12,U.A(U.Mix(U.Hex(0x9d9ea7),U.Hex(0xc4c5ce),hover),e),TextAnchor.MiddleLeft);
            bool click=interactive&&!lockerBlocked&&lockerViewHot&&U.Button(r);if(click)Click();return click;
        }
        // A wide row card: icon well, name and one line (a description or a mono stat line), the tick at the right. `body` >= 0
        // draws that body skin's thumbnail in the well instead of the icon (LkBodyThumb); `dev` badges the description line.
        private bool LkRowCard(Rect r,int key,bool on,string icon,Color tint,string name,string text,string stats,out bool hot,int body=-1,bool dev=false)
        {
            float e=lockerFade;hot=lockerViewHot&&U.Hover(r);float h=U.HoverAmount(key,r,hot);var d=new Rect(r.x,r.y-2*h,r.width,r.height);
            if(on)U.Glow(new Rect(d.x+10,d.y+10,d.width-20,d.height-10),U.Hex(0xff6b35,.12f*e),12);
            U.Round(d,on?U.Hex(0x2f2224,e):U.A(U.Mix(U.Hex(0x1d1e24),U.Hex(0x26272e),h),e),10,on?U.Hex(0xff6b35,e):U.A(U.Mix(U.Hex(0x3b3c45),U.Hex(0x9a8a9e),h),e),on?2:1);
            var well=new Rect(d.x+14,d.center.y-22,44,44);U.Round(well,U.Hex(0x15161b,e),10,U.Hex(on?0x8a5a45:0x4c4653,e));
            if(body>=0)LkBodyThumb(well,body,e,h);else NativeIcons.Draw(new Rect(well.x+10,well.y+10,24,24),icon,U.A(on?U.Hex(0xffb38a):tint,e));
            float tx=well.xMax+14,tw=d.xMax-tx-(on?36:14);var n=U.Wrap(name,15,tw,true,1);var sl=stats!=null?LkStatLines(stats,tw):null;bool two=sl!=null&&sl.Count>1;
            U.Text(new Rect(tx,d.center.y-(two?27:19),tw,20),n.Count>0?n[0]:name,15,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true);
            if(sl!=null)for(int i=0;i<sl.Count;i++)U.Text(new Rect(tx,d.center.y+(two?-5:3)+i*14,tw+6,14),sl[i],10,U.Hex(0xff996f,e),TextAnchor.MiddleLeft,mono:true,tracking:.1f);
            else if(text!=null)
            {
                float bx=0;if(dev){LkBadge(new Rect(tx,d.center.y+3,30,15),"DEV",U.Hex(0x261f3a,e),U.Hex(0x8b7ad8,e),U.Hex(0xd9d2f0,e));bx=36;}
                var s=U.Wrap(text,12,tw-bx,false,1);U.Text(new Rect(tx+bx,d.center.y+2,tw-bx,17),s.Count>0?s[0]:text,12,U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft);
            }
            if(on)LkTick(new Vector2(d.xMax-20,d.center.y),e);
            bool click=!lockerBlocked&&lockerViewHot&&U.Button(r);if(click)Click();return click;
        }
        // The stat line broken between its " · " parts (two lines at most) (NativeLoadoutUI StatLines, measured in the mono face), cached.
        private readonly Dictionary<(string,int,int),List<string>> lockerStats=new Dictionary<(string,int,int),List<string>>();
        private List<string> LkStatLines(string stats,float width)
        {
            var key=(stats,Mathf.RoundToInt(width),Mathf.RoundToInt(U.S*100));if(lockerStats.TryGetValue(key,out var lines))return lines;if(lockerStats.Count>256)lockerStats.Clear();
            lines=StatLines(stats,10,width,2);lockerStats[key]=lines;return lines;
        }
        private static void LkBadge(Rect r,string text,Color fill,Color edge,Color ink){U.Round(r,fill,4,edge);U.Text(r,text,9,ink,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);}
        private static void LkTick(Vector2 c,float e){U.RadialStops(new Rect(c.x-11,c.y-11,22,22),U.Hex(0xff6b35,e),LkDiscAt,LkDiscA);NativeIcons.Draw(new Rect(c.x-7,c.y-7,14,14),"check",U.Hex(0x111216,e));}
        private static float LkHash(int i,int salt)=>Mathf.Repeat(Mathf.Sin(i*12.9898f+salt*78.233f)*43758.5453f,1);

        // ---- Appearance visuals ----------------------------------------------------------------------------------------
        private void LkSwatchDraw(Rect r,bool bodySide)
        {
            float e=lockerFade;var presets=bodySide?BodyPresets:AccentPresets;string current=bodySide?prefs.bodyColor:prefs.accentColor;int per=Mathf.Clamp(Mathf.FloorToInt((r.width+8)/38),4,12);
            for(int i=0;i<presets.Length;i++)
            {
                var s=new Rect(r.x+2+(i%per)*38,r.y+(i/per)*38,30,30);bool on=string.Equals(current,presets[i],StringComparison.OrdinalIgnoreCase);bool hot=lockerViewHot&&U.Hover(s);float h=U.HoverAmount((bodySide?8000:8050)+i,s,hot);
                var g=new Rect(s.x-2*h,s.y-2*h,s.width+4*h,s.height+4*h);if(on)U.RadialStops(new Rect(g.x-5,g.y-5,g.width+10,g.height+10),U.Hex(0xff6b35,e),LkRingAt,LkRingA);
                U.RadialStops(g,U.A(LkColor(presets[i],0xffffff),e),LkDiscAt,LkDiscA);U.RadialStops(g,U.A(U.Mix(new Color(1,1,1,.25f),U.Hex(0xffc590),h),e),LkRingAt,LkRingA);
                if(hot&&Event.current.type==EventType.Repaint){if(bodySide)lockerBody=presets[i];else lockerAccent=presets[i];}
                if(!lockerBlocked&&hot&&U.Button(s)&&!on){if(bodySide)prefs.bodyColor=presets[i];else prefs.accentColor=presets[i];GUIUtility.keyboardControl=0;Click();}
            }
        }
        // Hats as line drawings over a voxel head in the pilot's colours (Lucide-style 24-unit strokes).
        private const string LkStroke=" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.7\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>";
        private static string LkSvg(string markup)=>markup.Replace("/>",LkStroke);
        private static readonly Dictionary<string,string> HatSvg=new Dictionary<string,string>
        {
            {"cap",LkSvg("<path d=\"M5 16A7 6.5 0 0 1 19 16Z\"/><path d=\"M14 16H22\"/><circle cx=\"12\" cy=\"9.2\" r=\".8\"/>")},
            {"beanie",LkSvg("<path d=\"M5.5 15A6.5 6.5 0 0 1 18.5 15\"/><rect x=\"4.5\" y=\"15\" width=\"15\" height=\"3.5\" rx=\"1.2\"/><circle cx=\"12\" cy=\"6.2\" r=\"1.6\"/>")},
            {"top",LkSvg("<rect x=\"7.5\" y=\"3.5\" width=\"9\" height=\"12.5\" rx=\"1\"/><path d=\"M7.5 12.8H16.5\"/><path d=\"M3.5 17.5Q12 19.8 20.5 17.5\"/>")},
            {"wizard",LkSvg("<path d=\"M12.8 2.5L6.5 17.5H18Z\"/><path d=\"M3.5 19Q12 21.5 20.5 19\"/><circle cx=\"12.2\" cy=\"11.2\" r=\".9\"/><circle cx=\"10.6\" cy=\"14.4\" r=\".6\"/>")},
            {"crown",LkSvg("<path d=\"M3.5 8.5L7.5 13L12 5.5L16.5 13L20.5 8.5L18.5 18.5H5.5Z\"/><path d=\"M5.5 21H18.5\"/>")},
            {"antenna",LkSvg("<path d=\"M12 20.5V9.5\"/><circle cx=\"12\" cy=\"7\" r=\"2.2\"/><path d=\"M7.5 3.8A5 5 0 0 0 7.5 10.2\"/><path d=\"M16.5 3.8A5 5 0 0 1 16.5 10.2\"/>")},
            {"halo",LkSvg("<ellipse cx=\"12\" cy=\"12\" rx=\"8.5\" ry=\"3.2\"/><path d=\"M12 4V6\"/><path d=\"M5.5 6L6.8 7.3\"/><path d=\"M18.5 6L17.2 7.3\"/>")},
            {"cowboy",LkSvg("<path d=\"M2.5 15.5Q12 21 21.5 15.5\"/><path d=\"M6.5 15.8C6.5 10 7.8 6.5 9.8 6.5C10.8 6.5 11.2 7.5 12 7.5C12.8 7.5 13.2 6.5 14.2 6.5C16.2 6.5 17.5 10 17.5 15.8\"/><path d=\"M6.8 13.2H17.2\"/>")},
            {"sheldon-head",LkSvg("<path d=\"M4 21C4 11.5 7.4 3.5 12 3.5S20 11.5 20 21\"/><path d=\"M7.4 19.5C7.4 12.4 9.4 8 12 8S16.6 12.4 16.6 19.5\"/><path d=\"M9.1 12.4Q10.1 11.3 11.1 12.4\"/><path d=\"M12.9 12.4Q13.9 11.3 14.9 12.4\"/><path d=\"M9 14.8L10 16.2L11 15L12 16.4L13 15L14 16.2L15 14.8\"/>")},
            {"auditor-halo",LkSvg("<path d=\"M4.5 12A7.5 3 0 0 1 10 9.1\"/><path d=\"M14 9.1A7.5 3 0 0 1 19.5 12\"/><path d=\"M18.5 14.2A7.5 3 0 0 1 5.5 14.2\"/><path d=\"M7 9.5Q7.8 7.6 7 6\"/><path d=\"M12 8Q12.9 5.6 12 3.6\"/><path d=\"M17 9.5Q17.8 7.6 17 6\"/>")},
        };
        private static readonly Dictionary<string,string> HatKey=new Dictionary<string,string>{{"cap","locker-hat-cap"},{"beanie","locker-hat-beanie"},{"top","locker-hat-top"},{"wizard","locker-hat-wizard"},{"crown","locker-hat-crown"},{"antenna","locker-hat-antenna"},{"halo","locker-hat-halo"},{"cowboy","locker-hat-cowboy"},{"auditor-halo","locker-hat-auditor"},{"sheldon-head","locker-hat-sheldon"}};
        private static Color HatTint(string id,Color accent)=>id==NativeHatLooks.SheldonHead?U.Hex(NativeHatLooks.SheldonGlow):id=="crown"?U.Hex(0xffd27a):id=="top"?U.Hex(0xe6e7ec):id=="halo"?U.Hex(0xfff1bb):id=="cowboy"?U.Hex(0xe0ac74):id=="antenna"?U.Hex(0xc0c1ca):id=="auditor-halo"?U.Hex(0xff3048):id=="cone"?U.Hex(0xffa565):U.Mix(accent,Color.white,.2f);
        private static void LkHatVisual(Rect v,string id,float e,float hover,Color body,Color accent,int picked=-1)
        {
            Color tint=picked>=0&&NativeHatLooks.Colorable(id)?U.Hex(picked):HatTint(id,accent);U.RadialStops(new Rect(v.x+v.width*.15f,v.y-v.height*.1f,v.width*.7f,v.height*1.1f),U.A(tint,.12f*e+.06f*hover),LkGlowAt,LkGlowA);
            float s=Mathf.Min(v.height*.36f,34),cx=v.center.x;var head=new Rect(cx-s/2,v.yMax-s-8,s,s*.92f);U.Fill(new Rect(cx-s*.24f,head.yMax,s*.48f,8),U.A(body*.55f,e));
            U.Round(head,U.A(body,e),4);U.Fill(new Rect(head.x+s*.16f,head.y+s*.38f,s*.68f,s*.17f),U.A(accent,e));
            float hs=s*1.55f;var hat=new Rect(cx-hs/2,head.y-hs*.72f-2*hover,hs,hs);
            if(id=="none"){NativeIcons.Draw(new Rect(cx-11,head.y-28,22,22),"circle-dashed",U.Hex(0x6d6e78,e));return;}
            if(id=="cone"){NativeIcons.Draw(hat,"traffic-cone",U.A(tint,e));return;}
            if(id=="halo")hat.y-=hs*.12f;
            if(HatSvg.TryGetValue(id,out var svg))NativeIcons.DrawMarkup(hat,HatKey[id],svg,24,U.A(tint,e));
        }

        // ---- Kill effect emblems ---------------------------------------------------------------------------------------
        // A voxel pilot from rects: `fall` 0 standing .. 1 lying (turned a quarter clockwise about the feet), `unit` its scale,
        // `lift` raises it (units). Rects lerp between the two layouts; nothing is drawn rotated (see the class comment).
        private static readonly float[] FigureParts=
        {
            -2f,-.3f,0f,4f,1, .3f,2f,0f,4f,1, -2.4f,2.4f,4f,7.6f,0, -2.4f,2.4f,4f,4.6f,2, -.45f,.45f,5f,7.2f,2, -3.3f,-2.5f,4.4f,7.4f,0, 2.5f,3.3f,4.4f,7.4f,0, -1.6f,1.6f,7.9f,10.2f,0, -1.2f,1.2f,8.7f,9.3f,2,
        };
        private static void Figure(Vector2 feet,float unit,float fall,float alpha,Color body,Color accent,float lift=0,Color tint=default,float tintK=0)
        {
            if(alpha<=.003f||unit<=.05f)return;var dark=new Color(body.r*.6f,body.g*.6f,body.b*.6f,1);
            for(int i=0;i<FigureParts.Length;i+=5)
            {
                float x0=FigureParts[i],x1=FigureParts[i+1],y0=FigureParts[i+2],y1=FigureParts[i+3];int tone=(int)FigureParts[i+4];
                float X0=Mathf.Lerp(x0,y0-5,fall),X1=Mathf.Lerp(x1,y1-5,fall),Y0=Mathf.Lerp(y0,2.4f-x1,fall)+lift,Y1=Mathf.Lerp(y1,2.4f-x0,fall)+lift;
                Color c=tone==0?body:tone==1?dark:accent;if(tintK>0)c=Color.Lerp(c,tint,tintK);
                U.Fill(new Rect(feet.x+X0*unit,feet.y-Y1*unit,(X1-X0)*unit,(Y1-Y0)*unit),U.A(c,alpha));
            }
        }
        // The frame an idle card holds (its effect at full show); a hovered card plays the whole 2.8 s loop.
        private static float LkHero(string id)=>id=="launch"?.42f:id=="ascension"?.5f:id=="shrink"?.6f:id=="dev-clown"||id=="dev-clown-love"||id=="dev-bass-van"?.36f:id=="ragdoll"?.42f:.58f;
        private static float Smooth(float x){x=Mathf.Clamp01(x);return x*x*(3-2*x);}
        private static Color Rainbow(float h){h=Mathf.Repeat(h,1)*6;float x=1-Mathf.Abs(h%2-1);return h<1?new Color(1,x,.35f):h<2?new Color(x,1,.35f):h<3?new Color(.35f,1,x):h<4?new Color(.35f,x,1):h<5?new Color(x,.35f,1):new Color(1,.35f,x);}
        private static void LkKillEmblem(Rect v,string id,float p,float e,Color body,Color accent,float t)
        {
            Color style=U.Hex(NativeLocker.KillColor(id));float ground=v.yMax-14,cx=v.center.x,unit=Mathf.Min(v.height/13f,6.6f);var feet=new Vector2(cx,ground);
            float fade=Smooth(p/.06f)*(1-Smooth((p-.9f)/.08f)),fall=Smooth((p-.14f)/.18f),q=Mathf.Clamp01((p-.2f)/.65f),a=fade*e,tau=Mathf.Max(0,p-.2f)*2.8f;
            U.RadialStops(new Rect(v.x+v.width*.1f,v.y+v.height*.05f,v.width*.8f,v.height*1.05f),U.A(style,(.10f+.08f*q)*e),LkGlowAt,LkGlowA);
            U.Fill(new Rect(v.x+12,ground,v.width-24,1),U.A(style,.22f*e));
            var core=new Vector2(cx,ground-2.4f*unit);
            switch(id)
            {
                case "ragdoll":Figure(feet,unit,fall,a*(1-Smooth((q-.55f)/.35f)),body,accent,-1.6f*Smooth((q-.3f)/.6f));break;
                case "impact-ragdoll":case "dev-gibber":
                {
                    bool gib=id=="dev-gibber";Figure(feet,unit,fall,a,body,accent);int n=gib?14:7;
                    for(int i=0;i<n;i++)
                    {
                        float ang=(LkHash(i,3)-.5f)*2.4f,sp=(.5f+LkHash(i,4))*unit*9,x=core.x+Mathf.Sin(ang)*sp*tau,y=core.y-Mathf.Cos(ang)*sp*tau*1.4f+unit*22*tau*tau;y=Mathf.Min(y,ground-2);float sz=gib?2.6f+LkHash(i,5)*2:3.4f;
                        if(tau>0)U.Fill(new Rect(x-sz/2,y-sz/2,sz,sz),U.A(i%3==0&&!gib?accent:U.Hex(gib?0xb3121f:0xd8323c),a));
                    }
                    if(gib)for(int i=0;i<3;i++){float s=Smooth((q-.15f-i*.12f)/.3f)*(10+i*4);if(s>0)U.RadialStops(new Rect(cx-16+i*16-s/2,ground-s*.18f,s,s*.36f),U.Hex(0x8e0f19,.85f*a),LkDiscAt,LkDiscA);}
                    break;
                }
                case "ragdoll-ember":
                    Figure(feet,unit,fall,a,body,accent,0,U.Hex(0x3a2016),.25f*q);
                    for(int i=0;i<12;i++){float life=Mathf.Repeat(q*1.8f+i/12f,1),x=cx+(LkHash(i,1)-.5f)*v.width*.5f+Mathf.Sin(life*6+i)*4,y=ground-6-life*v.height*.66f;if(q>0)U.Fill(new Rect(x-1.5f,y-1.5f,3,3),U.A(Color.Lerp(style,U.Hex(0xffd27a),life),(1-life)*a));}
                    break;
                case "ragdoll-frost":
                {
                    float ice=Smooth((q-.15f)/.25f),burst=Smooth((q-.5f)/.35f);Figure(feet,unit,fall,a*(1-Smooth((q-.5f)/.08f)),body,accent,0,U.Hex(0xbdefff),ice);
                    if(burst>0){for(int i=0;i<10;i++){float ang=i/10f*6.283f+.3f,r=burst*v.width*.36f,sz=5*(1-burst)+2;U.Fill(new Rect(core.x+Mathf.Cos(ang)*r-sz/2,core.y+Mathf.Sin(ang)*r*.6f-sz/2,sz,sz),U.A(style,(1-burst*.7f)*a));}
                        float ring=burst*v.width*.5f;U.RadialStops(new Rect(core.x-ring,core.y-ring*.42f,ring*2,ring*.84f),U.Hex(0xffffff,.6f*(1-burst)*a),LkRingAt,LkRingA);}
                    break;
                }
                case "ragdoll-static":
                {
                    Figure(feet,unit,fall,a,body,accent,0,U.Hex(0xfff6c2),.15f*q);int step=Mathf.FloorToInt(t*18);
                    for(int i=0;i<12&&q>0;i++){float hx=LkHash(i,step),hy=LkHash(i+40,step);float x=cx+(hx-.5f)*unit*(fall>.5f?10:6),y=ground-unit*(fall>.5f?hy*5:hy*10),len=3+LkHash(i,step+7)*5;bool horiz=LkHash(i,step+3)>.5f;
                        U.Fill(horiz?new Rect(x,y,len,1.5f):new Rect(x,y,1.5f,len),U.A(style,(.5f+.5f*LkHash(i,step+9))*a));}
                    break;
                }
                case "ragdoll-gravity":case "dev-singularity":
                {
                    bool sing=id=="dev-singularity";float shrink=1-Smooth((q-.45f)/.35f),burst=Smooth((q-.8f)/.15f);var hole=new Vector2(cx,ground-v.height*.32f);
                    Figure(Vector2.Lerp(feet,hole+new Vector2(0,1.2f*unit),1-shrink),unit*shrink,fall,a,body,accent);
                    for(int i=0;i<14&&q>0;i++){float ang=i*2.39f+q*10,r=(burst>0?burst*1.5f:1-Mathf.Repeat(q*1.6f+i/14f,1))*v.height*.42f;U.Fill(new Rect(hole.x+Mathf.Cos(ang)*r-1.5f,hole.y+Mathf.Sin(ang)*r*.55f-1.5f,3,3),U.A(style,a*(burst>0?1-burst:1)));}
                    if(sing){float s=Smooth(q/.4f)*(1-burst)*24;if(s>0){U.RadialStops(new Rect(hole.x-s*.95f,hole.y-s*.95f,s*1.9f,s*1.9f),U.A(style,.9f*a),LkRingAt,LkRingA);U.RadialStops(new Rect(hole.x-s/2,hole.y-s/2,s,s),U.Hex(0x080011,a),LkDiscAt,LkDiscA);}}
                    else{float s=(1-burst)*Smooth(q/.3f)*30;if(s>0)U.RadialStops(new Rect(hole.x-s,hole.y-s*.55f,s*2,s*1.1f),U.A(style,.5f*a),LkRingAt,LkRingA);}
                    break;
                }
                case "ragdoll-pinball":
                    Figure(feet,unit,fall,a,body,accent);
                    for(int i=0;i<9&&tau>0;i++){float ang=LkHash(i,2)*6.283f,sp=v.width*(.35f+.3f*LkHash(i,6));float x=LkBounce(core.x+Mathf.Cos(ang)*sp*tau,v.x+6,v.xMax-6),y=LkBounce(core.y-Mathf.Abs(Mathf.Sin(ang))*sp*tau,v.y+6,ground-3);
                        U.RadialStops(new Rect(x-3,y-3,6,6),U.A(Rainbow(i/9f),a*(1-q*.4f)),LkDiscAt,LkDiscA);}
                    break;
                case "ragdoll-fireworks":
                {
                    float burst=Smooth((q-.2f)/.45f),shrink=1-Smooth((q-.2f)/.2f);Figure(feet,unit,fall,a*shrink+.001f,body,accent);var c=new Vector2(cx,v.y+v.height*.42f);
                    for(int i=0;i<16&&burst>0;i++){float ang=i/16f*6.283f,r=burst*v.height*.42f,drop=burst*burst*8;var col=U.A(Rainbow(i/16f),(1-Smooth((q-.6f)/.4f))*a);
                        U.Fill(new Rect(c.x+Mathf.Cos(ang)*r-1.6f,c.y+Mathf.Sin(ang)*r+drop-1.6f,3.2f,3.2f),col);U.Fill(new Rect(c.x+Mathf.Cos(ang)*r*.78f-1,c.y+Mathf.Sin(ang)*r*.78f+drop-1,2,2),U.A(col,.5f));}
                    break;
                }
                case "ragdoll-soul":
                {
                    Figure(feet,unit,fall,a*.55f,body,accent,0,U.Hex(0x91dcec),.7f*Smooth(q/.3f));float rise=Smooth(q/.9f);
                    Figure(feet,unit,0,a*.45f*(1-rise),U.Hex(0xbff3ff),U.Hex(0xe8fbff),rise*v.height*.5f/unit);
                    for(int i=0;i<8&&q>0;i++){float life=Mathf.Repeat(q*1.4f+i/8f,1),ang=i*.8f+life*6;U.Fill(new Rect(cx+Mathf.Cos(ang)*unit*3.5f-1.5f,ground-life*v.height*.7f-1.5f,3,3),U.A(style,(1-life)*a));}
                    break;
                }
                case "ragdoll-petals":
                    Figure(feet,unit,fall,a,body,accent);
                    for(int i=0;i<12;i++){float life=Mathf.Repeat(q*1.1f+LkHash(i,8),1),x=v.x+10+LkHash(i,9)*(v.width-20)+Mathf.Sin(life*7+i)*7,y=v.y+4+life*(v.height-16);if(q>0)U.Fill(new Rect(x,y,i%2==0?5:4,i%2==0?3:4),U.A(i%3==0?U.Hex(0xfff0f6):style,a*Mathf.Min(1,(1-life)*3)));}
                    break;
                case "voxel-burst":case "confetti":
                {
                    bool confetti=id=="confetti";float pop=Smooth((p-.16f)/.06f);Figure(feet,unit,0,a*(1-pop),body,accent);int n=confetti?20:14;var dark=body*.6f;dark.a=1;
                    for(int i=0;i<n&&pop>0;i++)
                    {
                        float ang=(LkHash(i,11)-.5f)*2.6f,sp=(.6f+LkHash(i,12))*unit*(confetti?10:8),x=cx+Mathf.Sin(ang)*sp*tau,y=ground-5*unit-Mathf.Cos(ang)*sp*tau*1.5f+unit*(confetti?9:24)*tau*tau;
                        if(confetti)x+=Mathf.Sin(tau*6+i)*4;y=Mathf.Min(y,ground-2);float sz=confetti?3:4+LkHash(i,13)*2;Color c=confetti?Rainbow(LkHash(i,14)):i%3==0?accent:i%3==1?body:dark;
                        U.Fill(new Rect(x-sz/2,y-sz/2,sz,confetti?sz*.7f:sz),U.A(c,a));
                    }
                    break;
                }
                case "ascension":
                {
                    float beam=Smooth(q/.2f)*(1-Smooth((q-.8f)/.2f)),rise=Smooth((q-.1f)/.75f);
                    if(beam>0){U.HGradient(new Rect(cx-unit*4,v.y,unit*4,ground-v.y),Color.clear,U.A(style,.45f*beam*e));U.HGradient(new Rect(cx,v.y,unit*4,ground-v.y),U.A(style,.45f*beam*e),Color.clear);}
                    Figure(feet,unit,0,a*(1-rise*.8f),body,accent,rise*v.height*.6f/unit,U.Hex(0xfff7d6),.5f*beam);
                    for(int i=0;i<6&&beam>0;i++){float life=Mathf.Repeat(q*2+i/6f,1);U.Fill(new Rect(cx+(LkHash(i,15)-.5f)*unit*6,ground-life*(ground-v.y),2,2),U.A(style,(1-life)*beam*e));}
                    break;
                }
                case "shrink":
                {
                    float s=1-.62f*Smooth((q-.05f)/.3f)+.12f*Mathf.Sin(Mathf.Clamp01((q-.05f)/.3f)*Mathf.PI);Figure(feet,unit*s,0,a,body,accent);
                    for(int i=0;i<4&&q>.1f;i++){float k=Mathf.Repeat(q*1.5f+i*.25f,1),ang=i*1.57f+.6f;U.Fill(new Rect(cx+Mathf.Cos(ang)*(8+k*14)-1,ground-unit*3-Mathf.Sin(ang)*(8+k*14)-1,2,2),U.A(style,(1-k)*a));}
                    break;
                }
                case "launch":
                {
                    float lift=q*q*v.height*1.25f/unit,trail=Mathf.Min(lift*unit,ground-v.y);if(q>0)U.Fade(new Rect(cx-unit*1.4f,ground-trail,unit*2.8f,trail),U.A(style,.5f*a),false);
                    Figure(feet,unit,0,a,body,accent,lift);
                    float star=Smooth((q-.72f)/.08f)*(1-Smooth((q-.85f)/.1f));if(star>0){var c=new Vector2(cx+v.width*.18f,v.y+14);float s=7*star;U.Fill(new Rect(c.x-s,c.y-.75f,s*2,1.5f),U.A(Color.white,e));U.Fill(new Rect(c.x-.75f,c.y-s,1.5f,s*2),U.A(Color.white,e));U.Radial(new Rect(c.x-s,c.y-s,s*2,s*2),U.A(style,.8f*e*star));}
                    break;
                }
                case "ghost":
                {
                    Figure(feet,unit,fall,a,body,accent);float rise=Smooth(q/.9f),s=unit*6;if(q>0)NativeIcons.Draw(new Rect(cx-s/2,ground-2*unit-s-rise*v.height*.48f,s,s),"ghost",U.A(style,.85f*(1-rise*.7f)*a));
                    break;
                }
                case "dev-raging-demon":
                {
                    float down=Smooth((q-.68f)/.12f);Figure(feet,unit,down,a,body,accent,0,U.Hex(0x2a0008),.4f*Smooth(q/.2f));
                    int hit=Mathf.FloorToInt(q*14);if(q<.68f&&q>0){U.Fill(v,U.Hex(0xff2d55,.08f*e*(hit%2==0?1:.3f)));float hy=v.y+10+LkHash(hit,21)*(v.height-30),hw=v.width*(.4f+.4f*LkHash(hit,22));U.Fill(new Rect(cx-hw/2,hy,hw,2),U.A(U.Hex(0xffd1da),a));U.Fill(new Rect(cx-hw*.35f,hy+4,hw*.7f,1),U.A(style,a));}
                    if(q>=.68f)U.Fill(new Rect(v.x,v.y,v.width,v.height),U.Hex(0x14000a,.25f*down*a));
                    break;
                }
                case "dev-clown":case "dev-clown-love":case "dev-bass-van":
                {
                    bool van=id=="dev-bass-van";float inX=Smooth(q/.32f),outX=Smooth((q-.5f)/.35f),dir=van?-1:1,carW=van?unit*13:unit*11,carH=van?unit*7:unit*5;
                    float carX=cx-dir*(1-inX)*(v.width*.5f+carW)+dir*outX*(v.width*.5f+carW)+dir*unit*2;bool aboard=q>.34f;
                    Figure(feet,unit,0,a*(aboard?0:1),body,accent);
                    var car=new Rect(carX-carW/2,ground-carH-unit*1.6f,carW,carH);U.Round(car,U.A(style,a),4);
                    if(van){U.Fill(new Rect(car.x+carW*.08f,car.y+carH*.18f,carW*.3f,carH*.35f),U.Hex(0x0f1630,a));for(int i=0;i<3;i++){float k=Mathf.Repeat(t*1.4f+i/3f,1),s=8+k*30;U.RadialStops(new Rect(car.center.x-s,car.center.y-s,s*2,s*2),U.A(style,.5f*(1-k)*a),LkRingAt,LkRingA);}}
                    else{U.Fill(new Rect(car.x+carW*.25f,car.y-carH*.35f,carW*.45f,carH*.35f),U.Hex(0xffd27a,a));if(aboard)Figure(new Vector2(car.center.x,car.y),unit*.45f,0,a,body,accent);}
                    U.RadialStops(new Rect(car.x+carW*.15f,car.yMax-unit*1.4f,unit*2.8f,unit*2.8f),U.Hex(0x15161b,a),LkDiscAt,LkDiscA);U.RadialStops(new Rect(car.xMax-carW*.15f-unit*2.8f,car.yMax-unit*1.4f,unit*2.8f,unit*2.8f),U.Hex(0x15161b,a),LkDiscAt,LkDiscA);
                    if(id=="dev-clown-love")for(int i=0;i<4&&q>.2f;i++){float k=Mathf.Repeat(q*1.6f+i*.25f,1);NativeIcons.Draw(new Rect(car.center.x-8+(i-1.5f)*10,car.y-12-k*v.height*.5f,12,12),"heart",U.A(style,(1-k)*a));}
                    break;
                }
                default:Figure(feet,unit,fall,a,body,accent);break;
            }
        }
        // A ball bouncing between `lo` and `hi` (a triangle wave of its free path).
        private static float LkBounce(float x,float lo,float hi){float span=Mathf.Max(1,hi-lo),k=Mathf.Repeat(x-lo,2*span);return lo+(k<span?k:2*span-k);}

        // ---- Entrance --------------------------------------------------------------------------------------------------
        private static void LkIntroVisual(Rect v,string icon,Color tone,float e,float hover,float t,float phase,bool signature)
        {
            U.RadialStops(new Rect(v.x+v.width*.12f,v.y,v.width*.76f,v.height*1.1f),U.A(tone,(.14f+.08f*hover)*e),LkGlowAt,LkGlowA);
            // A spotlight sweeping across, as an entrance lights its pilot.
            float k=Mathf.Repeat(t*(.28f+.25f*hover)+phase,1),bw=34,bx=v.x-bw+k*(v.width+bw);var l=Rect.MinMaxRect(Mathf.Max(v.x,bx),v.y,Mathf.Min(v.xMax,bx+bw/2),v.yMax);var r=Rect.MinMaxRect(Mathf.Max(v.x,bx+bw/2),v.y,Mathf.Min(v.xMax,bx+bw),v.yMax);
            var c=new Color(1,1,1,.05f*e);if(l.width>0)U.HGradient(l,Color.clear,c);if(r.width>0)U.HGradient(r,c,Color.clear);
            float s=34+4*hover;NativeIcons.Draw(new Rect(v.center.x-s/2+1,v.center.y-s/2-2,s,s),icon,new Color(0,0,0,.45f*e));NativeIcons.Draw(new Rect(v.center.x-s/2,v.center.y-s/2-3,s,s),icon,U.A(U.Mix(tone,Color.white,.25f),e));
            U.Fill(new Rect(v.x+18,v.yMax-14,v.width-36,1),U.A(tone,.35f*e));
            if(signature)U.Text(new Rect(v.x,v.yMax-30,v.width,14),"YOUR WEAPON",9,U.A(U.Mix(tone,Color.white,.4f),e),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.16f);
        }

        // ---- Loadout ---------------------------------------------------------------------------------------------------
        // The class switcher (MenuClass / NativeLoadoutText.Pick as the class cards do: a weapon outside the class gives way to
        // the class default).
        private void LkClassRow(Rect r,float e)=>LockerClassTiles(new Rect(r.x,r.y+6,lockerW,r.height-6),e);
        // A slot's row: the key, its option cards (several per slot are laid out side by side) and, with a single option, the
        // "more coming" placeholder beside it; an empty slot says why.
        private const float LkKey=56;
        private float LkOptionW(int n){float avail=lockerW-LkKey-LkGap;if(n<=1)return Mathf.Min(avail-(avail>=520?LkGap+170:0),460);return (avail-(n-1)*LkGap)/n;}
        private float LkAbilityH(List<NativeLocker.AbilityOption> options)
        {
            float h=92;float w=LkOptionW(options.Count);foreach(var o in options){int lines=U.Wrap(o.Text,12,w-30,false,3).Count;h=Mathf.Max(h,16+28+8+lines*16+14);}return h;
        }
        private void LkAbilityRow(Rect r,string slot,List<NativeLocker.AbilityOption> options,ContentWeapon w,float e)
        {
            var key=new Rect(r.x,r.y,LkKey,r.height);U.Round(key,U.Hex(0x15161b,e),10,U.Hex(0x45464e,e));
            U.Text(new Rect(key.x,key.center.y-18,key.width,28),slot=="q"?"Q":"E",24,U.Hex(0xff996f,e),TextAnchor.MiddleCenter,bold:true,mono:true);U.Text(new Rect(key.x,key.center.y+10,key.width,14),"SLOT",9,U.Hex(0x8f909a,e),TextAnchor.MiddleCenter,mono:true,tracking:.14f);
            float x=key.xMax+LkGap,avail=lockerW-LkKey-LkGap;
            if(options.Count==0){LkDashed(new Rect(x,r.y,avail,r.height),e);NativeIcons.Draw(new Rect(x+18,r.center.y-10,20,20),"clock-3",U.Hex(0x8f909a,e));
                U.Text(new Rect(x+48,r.center.y-20,avail-60,20),slot=="q"?"No Q ability yet":"No E ability yet",15,U.Hex(0xc0c1ca,e),TextAnchor.MiddleLeft,bold:true);U.Text(new Rect(x+48,r.center.y+2,avail-60,18),NativeLocker.EmptySlot(w,slot),12,U.Hex(0x8f909a,e),TextAnchor.MiddleLeft);return;}
            int equipped=LockerEquippedAbility(w,slot,options);float ow=LkOptionW(options.Count);
            for(int i=0;i<options.Count;i++)LkAbilityCard(new Rect(x+i*(ow+LkGap),r.y,ow,r.height),options[i],i==equipped,9000+(slot=="q"?0:16)+i,e);
            if(options.Count==1&&avail>=520){var more=new Rect(x+ow+LkGap,r.y,avail-ow-LkGap,r.height);LkDashed(more,e);NativeIcons.Draw(new Rect(more.center.x-10,more.center.y-24,20,20),"layers",U.Hex(0x8f909a,e));
                U.Text(new Rect(more.x+8,more.center.y,more.width-16,18),"More abilities coming",12,U.Hex(0xa6a7ad,e),TextAnchor.MiddleCenter,bold:true);}
        }
        private void LkAbilityCard(Rect r,NativeLocker.AbilityOption o,bool on,int key,float e)
        {
            bool pickable=LockerAbilityPicks&&!on;bool hot=pickable&&lockerViewHot&&U.Hover(r);float h=pickable?U.HoverAmount(key,r,hot):0;
            U.Round(r,on?U.Hex(0x2a2124,e):U.A(U.Mix(U.Hex(0x1d1e24),U.Hex(0x26272e),h),e),10,on?U.Hex(0xc2714e,e):U.Hex(0x3b3c45,e),on?2:1);
            var well=new Rect(r.x+14,r.y+16,28,28);U.Round(well,U.Hex(0x15161b,e),8,U.Hex(0x4c4653,e));NativeIcons.Draw(new Rect(well.x+5,well.y+5,18,18),o.Icon,U.Hex(0xffc1a5,e));
            float tx=well.xMax+12,tw=r.xMax-tx-(on?36:14);var on1=U.Wrap(o.Name,15,tw,true,1);U.Text(new Rect(tx,r.y+12,tw,20),on1.Count>0?on1[0]:o.Name,15,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true);
            U.Text(new Rect(tx,r.y+32,tw+20,14),o.Meta,10,U.Hex(0xff996f,e),TextAnchor.MiddleLeft,mono:true);
            var lines=U.Wrap(o.Text,12,r.width-30,false,3);for(int i=0;i<lines.Count;i++)U.Text(new Rect(r.x+16,r.y+56+i*16,r.width-30,16),lines[i],12,U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft);
            if(on)LkTick(new Vector2(r.xMax-20,r.y+24),e);
            if(o.FromWeapon)LkBadge(new Rect(r.xMax-(on?110:84),r.y+15,74,17),"WEAPON E",U.Hex(0x1f2026,e),U.Hex(0x5a5b66,e),U.Hex(0xc0c1ca,e));
            if(!on&&!LockerAbilityPicks){var lk=new Rect(r.xMax-24,r.y+14,14,14);NativeIcons.DrawMarkup(lk,"locker-lock",LockMarkup,24,U.Hex(0x8f909a,e));}
        }
        private static void LkDashed(Rect r,float e)
        {
            Color c=U.Hex(0x5a5b66,.9f*e);U.Round(r,U.Hex(0x15161b,.5f*e),10);
            U.Dashed(new Rect(r.x+10,r.y,r.width-20,1),c);U.Dashed(new Rect(r.x+10,r.yMax-1,r.width-20,1),c);U.Dashed(new Rect(r.x,r.y+10,1,r.height-20),c,false);U.Dashed(new Rect(r.xMax-1,r.y+10,1,r.height-20),c,false);
        }

        // ---- Weapon skins: drawn by Runtime/NativeLockerSkinsUI.cs (LkSkinsLayout / LkSkins) ---------------------------------
        private static readonly string LockMarkup=LkSvg("<rect x=\"5\" y=\"11\" width=\"14\" height=\"9.5\" rx=\"2\"/><path d=\"M8 11V7.5A4 4 0 0 1 16 7.5V11\"/>");

        // ---- Stage: the pilot on its turntable -------------------------------------------------------------------------
        private const int LockerPortraitKey=3;
        private float LockerYawShown(float t){float idle=prefs.reducedEffects?0:Mathf.Clamp01((Time.unscaledTime-lockerTurnAt-2.5f)/1.5f);return lockerYaw+Mathf.Sin(t*.5f)*9*idle;}
        private void LockerStage(Rect r,Rect plate,float e,float t)
        {
            var w=LockerShownWeapon();Color accent=LkColor(lockerAccent??prefs.accentColor,0x70e1d0),body=LkColor(lockerBody??prefs.bodyColor,0xe5e7eb),tone=U.Hex((int)w.color);string pose=lockerPose??prefs.lobbyPose;
            U.Round(r,U.Hex(0x101116,.94f*e),12,U.Hex(0x3a3640,e));U.RadialStops(new Rect(r.x+r.width*.04f,r.y+r.height*.02f,r.width*.92f,r.height*.86f),U.A(accent,.15f*e),LkGlowAt,LkGlowA);
            // The turntable under the podium: its glow, rim and marks turning with the pilot (NativePortraitRules.Frame placement).
            var frame=NativePortraitRules.Frame(NativeLobbyPoses.Effective(w.id,pose),r.width/r.height);var view=frame.Target-frame.Position;float len=view.Length();
            float cos=Mathf.Sqrt(view.X*view.X+view.Z*view.Z)/len,sin=Mathf.Abs(view.Y)/len,units=r.height/(2*frame.Half),fy=r.center.y-(-.03f-frame.Target.Y)*cos*units,rx=Mathf.Min(1.16f*units,r.width*.48f),ry=Mathf.Max(6,rx*sin);
            if(fy+ry<r.yMax)
            {
                U.RadialStops(new Rect(r.center.x-rx*1.2f,fy-ry*1.6f,rx*2.4f,ry*3.2f),U.A(accent,.22f*e),LkGlowAt,LkGlowA);U.RadialStops(new Rect(r.center.x-rx,fy-ry,rx*2,ry*2),U.A(accent,.6f*e),LkRingAt,LkRingA);
                float yaw=LockerYawShown(t)*Mathf.Deg2Rad;for(int i=0;i<8;i++){float a=i*Mathf.PI/4-yaw,depth=Mathf.Cos(a);var p=new Vector2(r.center.x+Mathf.Sin(a)*rx,fy+depth*ry);U.Fill(new Rect(p.x-1.5f,p.y-1.5f,3,3),U.A(accent,(.25f+.6f*Mathf.Clamp01(depth))*e));}
            }
            var look=new PortraitLook{Spec=w.id,Pose=pose,Hat=lockerHat??prefs.hat,Body=HexOf(body),Accent=HexOf(accent),Yaw=LockerYawShown(t),Skin=LockerSkinShown(w.id),BodySkin=BodySkinShown(),HatColor=lockerHatColor??prefs.hatColor};
            float ring=Mathf.Min(86,r.width*.26f);PortraitStage(r,LockerPortraitKey,look,lockerBack??prefs.lobbyBack,NativeHatLooks.Rgb(lockerBackColor??prefs.backColor),accent,body,NativeIcons.Spec(w.id),tone,e,t,0,new Vector2(r.center.x,r.y+r.height*.42f),ring,28,false);
            U.Text(new Rect(r.x+16,r.y+14,200,14),"LIVE PREVIEW",10,U.Hex(0xff996f,e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            if(lockerHat!=null||lockerPose!=null||lockerBody!=null||lockerAccent!=null||lockerWeapon!=null||lockerBack!=null||lockerHatColor!=null||lockerBackColor!=null||lockerBodyTry!=null)U.Text(new Rect(r.x,r.y+14,r.width-16,14),"TRYING ON",10,U.Hex(0xc9bfff,e),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.16f);
            float hint=lockerTurned?Mathf.Clamp01(1-(Time.unscaledTime-lockerTurnAt-1.5f)):1;
            if(hint>0){var hr=new Rect(r.center.x-80,r.yMax-28,160,18);NativeIcons.Draw(new Rect(hr.x+4,hr.y+2,14,14),"rotate-cw",U.Hex(0x8f909a,hint*e));U.Text(new Rect(hr.x+24,hr.y,hr.width-24,hr.height),"DRAG TO ROTATE",10,U.Hex(0x8f909a,hint*e),TextAnchor.MiddleLeft,mono:true,tracking:.14f);}
            LockerTurn(r);
            // The name plate.
            float ns=plate.height>70?24:20,y=plate.y+(plate.height>70?6:2);U.Text(new Rect(plate.x,y,plate.width,ns*1.25f),lockerName,ns,U.Hex(0xf4f4f5,e),TextAnchor.MiddleCenter,bold:true);y+=ns*1.25f+2;
            U.Text(new Rect(plate.x,y,plate.width,16),LockerString(2,w.id),12,U.A(U.Mix(tone,Color.white,.3f),e),TextAnchor.MiddleCenter,bold:true,tracking:.12f);y+=20;
            if(y+14<=plate.yMax)U.Text(new Rect(plate.x,y,plate.width,14),ClassLabel(w.@class),10,U.Hex(0x8f909a,e),TextAnchor.MiddleCenter,mono:true,tracking:.1f);
        }
        // Drag to turn (the pointer is captured while held, so the turn follows it off the stage); a double click faces front.
        private void LockerTurn(Rect r)
        {
            var ev=Event.current;int id=GUIUtility.GetControlID(0x10C4E7,FocusType.Passive);var m=GUI.matrix;GUI.matrix=Matrix4x4.identity;float mx=ev.mousePosition.x;GUI.matrix=m;
            switch(ev.GetTypeForControl(id))
            {
                case EventType.MouseDown:if(ev.button==0&&!lockerBlocked&&U.Hover(r)){if(ev.clickCount==2){lockerYaw=0;lockerTurnAt=Time.unscaledTime;}GUIUtility.hotControl=id;lockerDragX=mx;ev.Use();}break;
                case EventType.MouseDrag:if(GUIUtility.hotControl==id){lockerYaw=Mathf.Repeat(lockerYaw-(mx-lockerDragX)/Mathf.Max(.01f,U.S)*.55f+180,360)-180;lockerDragX=mx;lockerTurnAt=Time.unscaledTime;lockerTurned=true;ev.Use();}break;
                case EventType.MouseUp:if(GUIUtility.hotControl==id){GUIUtility.hotControl=0;ev.Use();}break;
            }
        }

        // ---- Foot: status, Preview Intro, Revert, Save Pilot --------------------------------------------------------------
        private void LockerBar(Rect bar,float e,bool dirty,bool compact)
        {
            float now=Time.unscaledTime,bw=compact?168:200,rw=compact?128:150,pw=compact?168:190,gap=10,x=bar.xMax-bw;U.Fill(new Rect(bar.x,bar.y-14,bar.width,1),U.Hex(0x4b414c,.7f*e));
            var save=new Rect(x,bar.y,bw,bar.height);x-=gap+rw;var revert=new Rect(x,bar.y,rw,bar.height);x-=gap+pw;var preview=new Rect(x,bar.y,pw,bar.height);
            bool entrance=lockerTab==NativeLocker.Entrance;float sw=(entrance?preview.x:revert.x)-bar.x-16,cy=bar.center.y;bool signedIn=account?.Account!=null;
            string title,sub;Color tc;
            if(dirty){float pulse=.5f+.5f*Mathf.Sin(now*4);U.Radial(new Rect(bar.x-6,cy-12,24,24),U.Hex(0xff6b35,.35f*pulse*e));U.RadialStops(new Rect(bar.x+1,cy-5,10,10),U.Hex(0xff6b35,e),LkDiscAt,LkDiscA);
                title="Unsaved changes";sub=InRoom?"Save to update your look in this room.":"Save to keep them, or revert.";tc=U.Hex(0xffc590,e);}
            else if(now-lockerSavedAt<3.5f){NativeIcons.Draw(new Rect(bar.x-1,cy-8,16,16),"check",U.Hex(0x7ed78a,e));title="Saved";sub=lockerSavedAccount?"Your look is stored on your account.":"Your look is stored on this PC.";tc=U.Hex(0xb9f0c1,e);}
            else{NativeIcons.Draw(new Rect(bar.x-1,cy-8,16,16),"shield-check",U.Hex(0x8f909a,e));title="All changes saved";sub=signedIn?"Changes apply live; Save Pilot stores them.":"Log in to keep your pilot across computers.";tc=U.Hex(0xc0c1ca,e);}
            if(sw>60){U.Text(new Rect(bar.x+22,cy-18,sw-22,18),title,14,tc,TextAnchor.MiddleLeft,bold:true);var s=U.Wrap(sub,12,sw-22,false,1);if(s.Count>0)U.Text(new Rect(bar.x+22,cy+1,sw-22,16),s[0],12,U.Hex(0x8f909a,e),TextAnchor.MiddleLeft);}
            if(MenuButton(save,"check","Save Pilot",null,true,dirty,e,15,true))LockerSave();
            if(MenuButton(revert,"undo-2","Revert",null,false,dirty,e,15,true))LockerRevert();
            if(entrance&&MenuButton(preview,"play","Preview Intro",null,false,true,e,15,true))StartPreview();
        }
        // Leaving with unsaved changes: keep editing, discard (back to the look on entry) or save and leave.
        private void LockerConfirmDialog(Rect shell,float e)
        {
            U.Round(shell,U.Hex(0x0b0c0f,.66f*e),10);float w=Mathf.Min(520,shell.width-48),h=214;var d=new Rect(shell.center.x-w/2,shell.center.y-h/2,w,h);
            U.Glow(new Rect(d.x,d.y+20,d.width,d.height),new Color(0,0,0,.5f*e),40);U.Round(d,U.Hex(0x1b1a21,e),12,U.Hex(0x5a5262,e));
            U.RadialStops(new Rect(d.x+28,d.y+33,10,10),U.Hex(0xff6b35,e),LkDiscAt,LkDiscA);U.Text(new Rect(d.x+48,d.y+24,w-72,28),"Discard unsaved changes?",20,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true);
            var lines=U.Wrap("Your pilot has changes that are not saved. Discarding restores the look you had when you opened the Locker.",14,w-56,false,3);
            for(int i=0;i<lines.Count;i++)U.Text(new Rect(d.x+28,d.y+64+i*21,w-56,21),lines[i],14,U.Hex(0xbfc0c9,e),TextAnchor.MiddleLeft);
            float bw=(w-56-20)/3,by=d.yMax-28-46;int k=-1;menuBlockedNow=false;
            if(MenuButton(new Rect(d.x+28,by,bw,46),null,"Keep editing",null,false,true,e,14,true))k=0;
            if(MenuButton(new Rect(d.x+28+bw+10,by,bw,46),"x","Discard",null,false,true,e,14,true))k=1;
            if(MenuButton(new Rect(d.x+28+2*(bw+10),by,bw,46),"check","Save & leave",null,true,true,e,14,true))k=2;
            menuBlockedNow=true;
            if(k==0)lockerConfirm=false;else if(k==1){LockerRevert();LockerLeave();}else if(k==2){LockerSave();LockerLeave();}
        }
    }
}
