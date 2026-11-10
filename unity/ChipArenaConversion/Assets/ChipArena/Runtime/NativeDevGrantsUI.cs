using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;
namespace ChipArena.UnityConversion
{
    // The main menu's DEV screen (home · DEV; main.ts dev-menu): find registered players and give them DEV classes, their entrances
    // and DEV looks, and set their account labels. Owner 2026-11-09: "Rework the dev menu (Where we can modify players); Make it
    // cleaner and easier to use. Make it look a lot better too. Everytime we make a dev class, add them as an option here."
    //  - Left: the username search (Enter or Find) and the players found, each a card: their pilot's class icon in their colours,
    //    username, pilot and class, DEV / PT / VIP tags and a dot while they have unsaved changes.
    //  - Right: the picked player's card, then DEV CLASSES (every devOnly class in the content, so a new DEV class appears with
    //    nothing to add, each with its own entrance), LOOKS (each DEV kill effect and headwear a grant unlocks, drawn as the Locker
    //    draws them, in the player's colours), any DEV entrance of no class, and ACCOUNT LABELS as switches.
    //  - The bar: the picked player's unsaved changes, Revert and Save (one grant request, as before).
    // A key goes out only where the account server takes it (Core NativeDevGrants.Server, held equal to the server's list by
    // CoreTests --dev-grants); the rest read DEV accounts only. Changes are kept per player against what the server holds.
    public sealed partial class ConversionGame
    {
        private sealed class AdminItem{public string Key,Id,Name,Kind,Sub,Icon;public Color Tone;public bool Server;public AdminItem Entrance;public int Look;}
        private sealed class AdminRow{public NativeAccountData P;public string Line,Icon,Count;public Color Tone,Accent,Body;public bool Tester,Vip;}
        private string adminQuery="",adminPick,adminSearched,adminAsked,adminNote,adminError,adminAction,adminListHead="",adminLeftNote="Type the start of a registered username, then press Enter.";
        private bool adminWaiting,adminOk,adminLeftWarn,adminClassLinks,adminLookLinks,adminIntroLinks;private int adminListedFor=-1;private float adminScrollL,adminScrollR,adminContentL,adminContentR;private GUIStyle adminField;
        private NativeAccountData[] managedPlayers=Array.Empty<NativeAccountData>(),adminRowsFor;
        private readonly Dictionary<string,Dictionary<string,bool>> adminGrants=new Dictionary<string,Dictionary<string,bool>>(),adminFlags=new Dictionary<string,Dictionary<string,bool>>();
        private readonly List<AdminItem> adminClasses=new List<AdminItem>(),adminLooks=new List<AdminItem>(),adminIntros=new List<AdminItem>();
        private readonly List<string> adminClassKeys=new List<string>(),adminLookKeys=new List<string>(),adminIntroKeys=new List<string>();
        private readonly List<AdminRow> adminRows=new List<AdminRow>();
        private readonly Dictionary<(string,int,int,bool),string> adminFits=new Dictionary<(string,int,int,bool),string>();
        private readonly Dictionary<(string,int,int),List<string>> adminWraps=new Dictionary<(string,int,int),List<string>>();
        private (int,string) adminBarFor;private string adminBarText;
        private const int AdminHover=61000;private const float AdminGap=12,AdminClassMin=250,AdminClassH=138,AdminLookMin=168,AdminLookH=148,AdminFlagMin=260,AdminFlagH=64;
        private static readonly float[] AdminDiscAt={0f,.92f,1f},AdminDiscA={1f,1f,0f};
        private static readonly Color AdminDevFill=new Color(.118f,.078f,.251f),AdminDevEdge=new Color(.545f,.361f,1f),AdminDevInk=new Color(.839f,.784f,1f);

        // ---- Screen --------------------------------------------------------------------------------------------------
        private void DevGrantsScreen(float age)
        {
            // The panel takes the screen's height (BeginScreen sizes a body to its content; this one scrolls its own columns).
            float keep=screenContent;screenContent=4000;var l=BeginScreen("CHIP ARENA · DEV","Players",Mathf.Min(1280,U.W-56),age,actions:false);screenContent=keep;
            if(l.Closed||!account.Developer){Go(account.Developer?MenuScreen.Home:MenuScreen.Account);return;}
            AdminLists();AdminRowsSync();AdminSettle();
            float e=l.Enter;var b=l.Body;const float barH=60;
            var bar=new Rect(b.x,b.yMax-barH,b.width,barH);var cols=new Rect(b.x,b.y,b.width,bar.y-16-b.y);
            float lw=b.width>=1100?330:290,gap=28;var left=new Rect(cols.x,cols.y,lw,cols.height);var right=new Rect(left.xMax+gap,cols.y,cols.xMax-left.xMax-gap,cols.height);
            U.Fill(new Rect(left.xMax+gap/2,cols.y+4,1,cols.height-8),C.Edge(.6f*e));
            AdminLeft(left,e,age);AdminRight(right,e);AdminBar(bar,e);
        }
        // A request ends when the account stops being busy: its callback ran (adminOk) or the account's status says why not.
        private void AdminSettle()
        {
            if(!adminWaiting||account.Busy)return;adminWaiting=false;if(adminOk)return;
            string why=string.IsNullOrEmpty(account.Status)?"The account service did not answer. Try again.":account.Status;
            if(adminAction=="players"){adminLeftNote=why;adminLeftWarn=true;}else adminError=why;
        }

        // ---- What can be granted -------------------------------------------------------------------------------------
        // Every devOnly class in the content (the Locker's DEV classes) with its own entrance (LockerSignature), every DEV kill effect
        // and headwear a grant unlocks, and any DEV entrance of no class (NativeDevGrants.Offered: never the owner's own).
        private void AdminLists()
        {
            int n=content?.weapons?.Length??0;if(n==adminListedFor)return;adminListedFor=n;
            adminClasses.Clear();adminLooks.Clear();adminIntros.Clear();adminClassKeys.Clear();adminLookKeys.Clear();adminIntroKeys.Clear();var paired=new HashSet<string>();
            if(content?.weapons!=null)foreach(var w in content.weapons)
            {
                if(w==null||!w.devOnly)continue;string intro=LockerSignature(w.id);
                var item=new AdminItem{Key="weapon:"+w.id,Id=w.id,Name=w.name,Kind=AdminClassKind(w.@class),Icon=NativeIcons.Spec(w.id),Tone=U.Hex((int)w.color),Sub="No entrance of its own"};
                item.Server=NativeDevGrants.Grantable(item.Key);adminClassKeys.Add(item.Key);
                if(NativeDevGrants.Offered(intro)){item.Entrance=AdminIntroItem(intro,item.Tone);item.Sub="Entrance · "+item.Entrance.Name;paired.Add(intro);adminClassKeys.Add(item.Entrance.Key);}
                adminClasses.Add(item);
            }
            foreach(var id in NativeCosmeticRules.Kills)if(NativeDevGrants.Offered(id))adminLooks.Add(AdminLookItem("effect:",id,1,"Kill effect"));
            foreach(var id in NativeCosmeticRules.Hats)if(NativeDevGrants.Offered(id))adminLooks.Add(AdminLookItem("hat:",id,2,"Headwear"));
            foreach(var id in NativeCosmeticRules.Intros)if(NativeDevGrants.Offered(id)&&!paired.Contains(id))adminIntros.Add(AdminIntroItem(id,U.Hex(0xb7a8ff)));
            foreach(var it in adminLooks)adminLookKeys.Add(it.Key);foreach(var it in adminIntros)adminIntroKeys.Add(it.Key);
            adminClassLinks=adminClassKeys.Exists(NativeDevGrants.Grantable);adminLookLinks=adminLookKeys.Exists(NativeDevGrants.Grantable);adminIntroLinks=adminIntroKeys.Exists(NativeDevGrants.Grantable);
        }
        private static string AdminClassKind(string c)=>c=="melee"?"MELEE · DEV CLASS":c=="magic"?"MAGIC · DEV CLASS":c=="ranged"?"RANGED · DEV CLASS":"DEV CLASS";
        // The Locker's labels without their DEV marks ("DEV · Emperor's Oath", "Clown getaway · DEV ONLY").
        private static string AdminName(string label)
        {
            if(label==null)return "";if(label.StartsWith("DEV · ",StringComparison.Ordinal))label=label.Substring(6);
            int i=label.IndexOf(" · DEV ONLY",StringComparison.Ordinal);return i>0?label.Substring(0,i):label;
        }
        private static AdminItem AdminLookItem(string prefix,string id,int look,string kind)=>new AdminItem{Key=prefix+id,Id=id,Name=AdminName(NativeCosmeticRules.Label(id)),Kind=kind,Look=look,Server=NativeDevGrants.Grantable(prefix+id)};
        private static AdminItem AdminIntroItem(string id,Color tone)=>new AdminItem{Key="intro:"+id,Id=id,Name=AdminName(NativeCosmeticRules.Label(id)),Kind="Entrance",Icon=NativeLocker.IntroIcon(id)??"sparkles",Tone=tone,Look=3,Server=NativeDevGrants.Grantable("intro:"+id)};

        // ---- Players found -------------------------------------------------------------------------------------------
        private void AdminRowsSync()
        {
            if(ReferenceEquals(adminRowsFor,managedPlayers))return;adminRowsFor=managedPlayers;adminRows.Clear();
            foreach(var p in managedPlayers)if(p!=null&&!string.IsNullOrEmpty(p.id))adminRows.Add(AdminRowOf(p));
            adminListHead=adminRows.Count==1?"1 PLAYER":adminRows.Count+" PLAYERS";
            if(adminPick!=null&&AdminRowFor(adminPick)==null)adminPick=null;if(adminPick==null&&adminRows.Count>0)adminPick=adminRows[0].P.id;
        }
        private AdminRow AdminRowOf(NativeAccountData p)
        {
            var c=p.profile?.cosmetics;string spec=c?.preferredSpec,fallback=AdminSpecOf(p.profile?.@class);ContentWeapon w=null,f=null;
            if(content?.weapons!=null)foreach(var x in content.weapons){if(x==null)continue;if(x.id==spec)w=x;if(x.id==fallback)f=x;}w=w??f;
            int grants=0;if(p.unlocks!=null)foreach(var k in p.unlocks)if(k!=null)grants++;
            return new AdminRow{P=p,Line=NativeCosmeticRules.Name(p.profile?.name)+" · "+(w?.name??"No class yet"),Icon=w!=null?NativeIcons.Spec(w.id):"users",Tone=w!=null?U.Hex((int)w.color):C.Muted(),
                Accent=HexColor(p.profile?.color,0xff6b35),Body=HexColor(c?.bodyColor,0xe5e7eb),Count=grants==0?"NO GRANTS":grants==1?"1 GRANT":grants+" GRANTS",Tester=NativeDevGrants.Has(p.flags,"playtester"),Vip=NativeDevGrants.Has(p.flags,"vip")};
        }
        private static string AdminSpecOf(string c)=>c=="melee"?"sword":c=="magic"?"fire":"pistol";
        private AdminRow AdminRowFor(string id){if(id!=null)foreach(var r in adminRows)if(r.P.id==id)return r;return null;}
        private static Dictionary<string,bool> AdminChanges(Dictionary<string,Dictionary<string,bool>> all,string id){if(!all.TryGetValue(id,out var d)){d=new Dictionary<string,bool>();all[id]=d;}return d;}
        private int AdminChangeCount(NativeAccountData p){adminGrants.TryGetValue(p.id,out var g);adminFlags.TryGetValue(p.id,out var f);return NativeDevGrants.Changes(p.unlocks,g)+NativeDevGrants.Changes(p.flags,f);}
        private bool AdminUnsaved(out string who){foreach(var r in adminRows)if(AdminChangeCount(r.P)>0){who=r.P.username;return true;}who=null;return false;}
        // Usernames are 2-20 of a-z, 0-9 and _ (the server's query check); typed capitals are lowered.
        private static string AdminClean(string typed)
        {
            if(string.IsNullOrEmpty(typed))return "";var chars=new char[Math.Min(20,typed.Length)];int n=0;
            foreach(char ch in typed){char c=char.ToLowerInvariant(ch);if(n<chars.Length&&(c>='a'&&c<='z'||c>='0'&&c<='9'||c=='_'))chars[n++]=c;}return new string(chars,0,n);
        }

        // ---- Requests (NativeAccount.Manage: one at a time, a DEV's session) -------------------------------------------
        private void AdminSearch()
        {
            string q=AdminClean(adminQuery);adminQuery=q;
            if(q.Length<2){adminLeftNote="Type at least 2 characters of the username.";adminLeftWarn=true;return;}
            if(AdminUnsaved(out string who)){adminLeftNote="Save or revert the changes for "+who+" first.";adminLeftWarn=true;return;}
            if(account.Busy||!account.Developer)return;
            adminAction="players";adminAsked=q;adminWaiting=true;adminOk=false;adminError=null;adminNote=null;adminLeftNote="Searching…";adminLeftWarn=false;
            account.Manage("players",JsonUtility.ToJson(new NativeAdminQuery{query=q}),response=>
            {
                adminOk=true;managedPlayers=(response.players??Array.Empty<NativeAccountData>()).Where(p=>p!=null&&!string.IsNullOrEmpty(p.id)).Take(100).ToArray();
                adminSearched=adminAsked;adminScrollL=adminScrollR=0;adminGrants.Clear();adminFlags.Clear();adminPick=null;
                adminLeftNote=managedPlayers.Length==0?"No registered player starts with “"+adminSearched+"”.":managedPlayers.Length==1?"1 player starts with “"+adminSearched+"”.":managedPlayers.Length+" players start with “"+adminSearched+"”.";adminLeftWarn=managedPlayers.Length==0;
            });
            if(!account.Busy&&!adminOk){adminWaiting=false;adminLeftNote="The account service is busy. Try again.";adminLeftWarn=true;}
        }
        private void AdminSave()
        {
            var row=AdminRowFor(adminPick);if(row==null||account.Busy||!account.Developer)return;var p=row.P;
            adminGrants.TryGetValue(p.id,out var g);adminFlags.TryGetValue(p.id,out var f);if(NativeDevGrants.Changes(p.unlocks,g)+NativeDevGrants.Changes(p.flags,f)==0)return;
            var request=new NativeGrantRequest{accountId=p.id,grantVersion=p.grantVersion,unlocks=NativeDevGrants.Merge(p.unlocks,g,NativeDevGrants.Server),flags=NativeDevGrants.Merge(p.flags,f,NativeDevGrants.Flags)};
            string id=p.id,name=p.username;adminAction="grant";adminWaiting=true;adminOk=false;adminError=null;adminNote=null;
            account.Manage("grant",JsonUtility.ToJson(request),response=>
            {
                adminOk=true;var a=response.account;if(a==null||a.id!=id)return;
                p.grantVersion=a.grantVersion;p.unlocks=a.unlocks;p.flags=a.flags;p.developer=a.developer;adminGrants.Remove(id);adminFlags.Remove(id);adminRowsFor=null;
                adminNote="Saved for "+name+". They get it after refreshing their account and rejoining.";
            });
            if(!account.Busy&&!adminOk){adminWaiting=false;adminError="The account service is busy. Try again.";}
        }

        // ---- Left: search and players ----------------------------------------------------------------------------------
        private void AdminLeft(Rect r,float e,float age)
        {
            var ev=Event.current;bool blocked=MenuBlocked();float y=r.y;
            AdminHeading(r.x,y,r.width,"FIND A PLAYER",null,e);y+=36;
            if(adminField==null){adminField=new GUIStyle(GUI.skin.textField){fontSize=16,alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip,wordWrap=false};adminField.padding=new RectOffset(40,12,10,10);}
            var field=new Rect(r.x,y,r.width-110,46);var find=new Rect(field.xMax+10,y,100,46);
            if(!blocked&&ev.type==EventType.KeyDown&&(ev.keyCode==KeyCode.Return||ev.keyCode==KeyCode.KeypadEnter)&&GUI.GetNameOfFocusedControl()=="dev-player-search"){AdminSearch();ev.Use();}
            GUI.SetNextControlName("dev-player-search");string typed=GUI.TextField(field,adminQuery??"",20,adminField);
            if(typed!=adminQuery){adminQuery=AdminClean(typed);if(adminLeftWarn&&!adminWaiting){adminLeftWarn=false;adminLeftNote="Press Enter or Find to search.";}}
            if(age<.3f&&managedPlayers.Length==0)GUI.FocusControl("dev-player-search");
            NativeIcons.Draw(new Rect(field.x+13,field.center.y-9,18,18),"scan",C.Muted(e));
            if(string.IsNullOrEmpty(adminQuery))U.Text(new Rect(field.x+40,field.y,field.width-48,field.height),"username",15,C.Dim(e),TextAnchor.MiddleLeft);
            bool searching=adminWaiting&&adminAction=="players";
            if(MenuButton(find,null,searching?"…":"Find",null,true,!account.Busy&&(adminQuery??"").Length>=2,e,15,true))AdminSearch();
            y+=56;
            var note=AdminWrap(adminLeftNote,12,r.width,2);for(int i=0;i<note.Count;i++)U.Text(new Rect(r.x,y+i*16,r.width,16),note[i],12,adminLeftWarn?C.Warn(e):C.Muted(e),TextAnchor.MiddleLeft);
            y+=Mathf.Max(1,note.Count)*16+18;
            if(adminRows.Count==0){AdminEmptyList(new Rect(r.x,y,r.width,r.yMax-y),e);return;}
            AdminHeading(r.x,y,r.width,adminListHead,null,e);y+=36;
            var list=new Rect(r.x,y,r.width,r.yMax-y);bool hot=!blocked&&U.Hover(list);
            if(hot&&ev.type==EventType.ScrollWheel){adminScrollL+=ev.delta.y*24;ev.Use();}
            adminScrollL=Mathf.Clamp(adminScrollL,0,Mathf.Max(0,adminContentL-list.height));
            float cw=list.width-(adminContentL>list.height?12:0),cy=list.y+2-adminScrollL,c0=cy;
            NativeControllerPointer.SetClip(list);U.BeginBand(list);
            for(int i=0;i<adminRows.Count;i++){var card=new Rect(list.x,cy,cw,70);if(card.yMax>list.y-4&&card.y<list.yMax+4&&AdminPlayerCard(card,adminRows[i],i,hot,e)&&adminPick!=adminRows[i].P.id){adminPick=adminRows[i].P.id;adminScrollR=0;}cy+=78;}
            U.EndBand();NativeControllerPointer.ClearClip();adminContentL=cy-c0;AdminThumb(list,adminScrollL,adminContentL,e);
        }
        private void AdminEmptyList(Rect r,float e)
        {
            if(r.height<150)return;var c=new Vector2(r.center.x,r.y+70);
            U.Radial(new Rect(c.x-80,c.y-80,160,160),C.Cyan(.06f*e));U.RadialStops(new Rect(c.x-34,c.y-34,68,68),C.Edge(.9f*e),AdminDiscAt,AdminDiscA);U.RadialStops(new Rect(c.x-32,c.y-32,64,64),U.Hex(0x0a0f24,e),AdminDiscAt,AdminDiscA);
            NativeIcons.Draw(new Rect(c.x-16,c.y-16,32,32),"users",C.Muted(e));
            bool none=adminSearched!=null;U.Text(new Rect(r.x,c.y+46,r.width,22),none?"Nobody found":"No players yet",16,C.Ink(e),TextAnchor.MiddleCenter,bold:true);
            var lines=AdminWrap(none?"Check the spelling: usernames are letters, numbers and _.":"Search a registered username to see the player here.",12,r.width-30,3);
            for(int i=0;i<lines.Count;i++)U.Text(new Rect(r.x,c.y+72+i*16,r.width,16),lines[i],12,C.Muted(e),TextAnchor.MiddleCenter);
        }
        private bool AdminPlayerCard(Rect r,AdminRow row,int i,bool listHot,float e)
        {
            var p=row.P;bool on=p.id==adminPick,hot=listHot&&U.Hover(r);float h=U.HoverAmount(AdminHover+i,r,hot);int changes=AdminChangeCount(p);var d=new Rect(r.x,r.y-1.5f*h,r.width,r.height);
            if(on)U.Glow(new Rect(d.x+8,d.y+10,d.width-16,d.height-10),C.Cyan(.13f*e),14);
            U.Round(d,on?U.Hex(0x0f2338,.96f*e):U.A(U.Mix(U.Hex(0x10162f),U.Hex(0x162047),h),.94f*e),10,on?C.Cyan(.9f*e):U.A(U.Mix(C.Edge(),C.Cyan(.7f),h),e),on?1.5f:1);
            if(on)U.Fill(new Rect(d.x+1,d.y+15,3,d.height-30),C.Cyan(e));
            AdminEmblem(new Vector2(d.x+38,d.center.y),22,row,e);
            float tx=d.x+72,tag=d.xMax-12;
            if(p.developer)tag=AdminTag(tag,d.y+15,"DEV",AdminDevFill,AdminDevEdge,AdminDevInk,e);
            if(row.Vip)tag=AdminTag(tag,d.y+15,"VIP",U.Hex(0x3a2a0a),U.Hex(0xffc24d),U.Hex(0xffe2a0),e);
            if(row.Tester)tag=AdminTag(tag,d.y+15,"PT",U.Hex(0x0a2a33),U.Hex(0x22e6ff),U.Hex(0xb8f6ff),e);
            if(changes>0){U.RadialStops(new Rect(tag-10,d.y+19,8,8),C.Warn(e),AdminDiscAt,AdminDiscA);tag-=16;}
            U.Text(new Rect(tx,d.y+13,tag-tx,20),AdminFit(p.username,15,true,tag-tx),15,C.Ink(e),TextAnchor.MiddleLeft,bold:true);
            U.Text(new Rect(tx,d.y+37,d.xMax-12-tx,18),AdminFit(row.Line,12,false,d.xMax-12-tx),12,C.Muted(e),TextAnchor.MiddleLeft);
            return !MenuBlocked()&&hot&&U.Button(r);
        }
        // Their pilot's class icon in its colour on a dark disc ringed in their accent colour.
        private static void AdminEmblem(Vector2 c,float rad,AdminRow row,float e)
        {
            U.Radial(new Rect(c.x-rad*1.7f,c.y-rad*1.7f,rad*3.4f,rad*3.4f),U.A(row.Accent,.18f*e));
            U.RadialStops(new Rect(c.x-rad,c.y-rad,rad*2,rad*2),U.A(row.Accent,.9f*e),AdminDiscAt,AdminDiscA);U.RadialStops(new Rect(c.x-rad+2.5f,c.y-rad+2.5f,rad*2-5,rad*2-5),U.Hex(0x0a0d1c,e),AdminDiscAt,AdminDiscA);
            float s=rad*1.1f;NativeIcons.Draw(new Rect(c.x-s/2,c.y-s/2,s,s),row.Icon,U.A(U.Mix(row.Tone,Color.white,.1f),e));
        }

        // ---- Right: the picked player ----------------------------------------------------------------------------------
        private void AdminRight(Rect r,float e)
        {
            var row=AdminRowFor(adminPick);if(row==null){AdminEmptyEditor(r,e);return;}
            var p=row.P;AdminHeader(new Rect(r.x,r.y,r.width,104),row,e);
            var view=new Rect(r.x,r.y+104+18,r.width,r.height-104-18);var ev=Event.current;bool hot=!MenuBlocked()&&U.Hover(view);
            if(hot&&ev.type==EventType.ScrollWheel){adminScrollR+=ev.delta.y*26;ev.Use();}
            adminScrollR=Mathf.Clamp(adminScrollR,0,Mathf.Max(0,adminContentR-view.height));
            float w=view.width-(adminContentR>view.height?14:0),x=view.x,y=view.y-adminScrollR,y0=y;
            adminGrants.TryGetValue(p.id,out var g);adminFlags.TryGetValue(p.id,out var f);
            NativeControllerPointer.SetClip(view);U.BeginBand(view);
            y=AdminSection(x,y,w,"DEV CLASSES","A class and its own entrance",adminClassKeys,adminClassLinks,p,hot,0,e);
            int cols=AdminCols(w,AdminClassMin);float cw=(w-(cols-1)*AdminGap)/cols;
            for(int i=0;i<adminClasses.Count;i++)AdminClassCard(new Rect(x+(i%cols)*(cw+AdminGap),y+(i/cols)*(AdminClassH+AdminGap),cw,AdminClassH),adminClasses[i],i,p,g,hot,e);
            y+=AdminGridH(adminClasses.Count,cols,AdminClassH)+28;
            if(adminLooks.Count>0)
            {
                y=AdminSection(x,y,w,"LOOKS","DEV kill effects and headwear, in their colours",adminLookKeys,adminLookLinks,p,hot,1,e);cols=AdminCols(w,AdminLookMin);cw=(w-(cols-1)*AdminGap)/cols;
                for(int i=0;i<adminLooks.Count;i++)AdminLookCard(new Rect(x+(i%cols)*(cw+AdminGap),y+(i/cols)*(AdminLookH+AdminGap),cw,AdminLookH),adminLooks[i],i,row,g,hot,e);
                y+=AdminGridH(adminLooks.Count,cols,AdminLookH)+28;
            }
            if(adminIntros.Count>0)
            {
                y=AdminSection(x,y,w,"ENTRANCES","DEV entrances of no class",adminIntroKeys,adminIntroLinks,p,hot,2,e);cols=AdminCols(w,AdminLookMin);cw=(w-(cols-1)*AdminGap)/cols;
                for(int i=0;i<adminIntros.Count;i++)AdminLookCard(new Rect(x+(i%cols)*(cw+AdminGap),y+(i/cols)*(AdminLookH+AdminGap),cw,AdminLookH),adminIntros[i],40+i,row,g,hot,e);
                y+=AdminGridH(adminIntros.Count,cols,AdminLookH)+28;
            }
            y=AdminSection(x,y,w,"ACCOUNT LABELS","Shown on their account",null,false,p,hot,3,e);cols=AdminCols(w,AdminFlagMin);cw=(w-(cols-1)*AdminGap)/cols;
            for(int i=0;i<NativeDevGrants.Flags.Length;i++)AdminFlagRow(new Rect(x+(i%cols)*(cw+AdminGap),y+(i/cols)*(AdminFlagH+AdminGap),cw,AdminFlagH),i,p,f,hot,e);
            y+=AdminGridH(NativeDevGrants.Flags.Length,cols,AdminFlagH)+8;
            U.EndBand();NativeControllerPointer.ClearClip();adminContentR=y-y0;AdminThumb(view,adminScrollR,adminContentR,e);
        }
        private void AdminEmptyEditor(Rect r,float e)
        {
            var c=new Vector2(r.center.x,r.y+Mathf.Min(r.height*.36f,220));
            U.Radial(new Rect(c.x-120,c.y-120,240,240),C.Cyan(.07f*e));U.RadialStops(new Rect(c.x-48,c.y-48,96,96),C.Edge(e),AdminDiscAt,AdminDiscA);U.RadialStops(new Rect(c.x-45.5f,c.y-45.5f,91,91),U.Hex(0x0a0f24,e),AdminDiscAt,AdminDiscA);
            NativeIcons.Draw(new Rect(c.x-22,c.y-22,44,44),"users",C.CyanSoft(.85f*e));
            U.Text(new Rect(r.x,c.y+64,r.width,28),"Pick a player",21,C.Ink(e),TextAnchor.MiddleCenter,bold:true);
            var lines=AdminWrap("Find a registered player on the left. Their DEV classes, looks and account labels open here.",13,Mathf.Min(460,r.width-40),3);
            for(int i=0;i<lines.Count;i++)U.Text(new Rect(r.x,c.y+98+i*18,r.width,18),lines[i],13,C.Muted(e),TextAnchor.MiddleCenter);
        }
        private void AdminHeader(Rect r,AdminRow row,float e)
        {
            var p=row.P;U.Glow(new Rect(r.x+24,r.y+18,r.width-48,r.height-18),U.A(row.Accent,.07f*e),22);
            U.RoundGradient(r,U.A(U.Mix(U.Hex(0x111a3c),row.Accent,.12f),.96f*e),U.Hex(0x0b1027,.96f*e),14,U.A(U.Mix(C.Edge(),row.Accent,.4f),e),1,true);
            AdminEmblem(new Vector2(r.x+56,r.center.y),33,row,e);
            float tx=r.x+108,tw=r.xMax-20-tx;
            U.Text(new Rect(tx,r.y+13,tw,32),AdminFit(p.username,25,true,tw),25,C.Ink(e),TextAnchor.MiddleLeft,bold:true,tracking:-.02f);
            U.Text(new Rect(tx,r.y+45,tw,18),AdminFit(row.Line,13,false,tw),13,C.Muted(e),TextAnchor.MiddleLeft);
            float t=tx;if(p.developer)t=AdminTagL(t,r.y+70,"DEV ACCOUNT",AdminDevFill,AdminDevEdge,AdminDevInk,e);
            if(row.Tester)t=AdminTagL(t,r.y+70,"PLAYTESTER",U.Hex(0x0a2a33),U.Hex(0x22e6ff),U.Hex(0xb8f6ff),e);
            if(row.Vip)t=AdminTagL(t,r.y+70,"VIP",U.Hex(0x3a2a0a),U.Hex(0xffc24d),U.Hex(0xffe2a0),e);
            t=AdminTagL(t,r.y+70,row.Count,U.Hex(0x0b1230),C.Edge(),C.Muted(),e);
            if(p.developer&&r.width>620)U.Text(new Rect(t+8,r.y+70,r.xMax-20-t-8,18),"A DEV account has every class and look already.",12,U.Hex(0xb9a8ff,e),TextAnchor.MiddleRight);
        }
        // A section's heading (the Locker's mono label and hint) with ALL / NONE for its grantable keys, over a rule.
        private float AdminSection(float x,float y,float w,string label,string hint,List<string> keys,bool links,NativeAccountData p,bool hot,int section,float e)
        {
            U.Text(new Rect(x+2,y,w,22),label,11,C.CyanSoft(.95f*e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);float lw=U.Measure(label,11,true,true,.16f).x;
            float room=w-lw-16-(links?130:0);
            if(hint!=null&&room>60)U.Text(new Rect(x+lw+16,y,room,22),AdminFit(hint,12,false,room),12,C.Muted(e),TextAnchor.MiddleLeft);
            if(links)
            {
                var none=new Rect(x+w-56,y,56,22);var all=new Rect(none.x-62,y,56,22);
                if(AdminLink(all,"ALL",AdminHover+900+section*2,hot,e))NativeDevGrants.SetAll(p.unlocks,AdminChanges(adminGrants,p.id),keys,true,NativeDevGrants.Server);
                if(AdminLink(none,"NONE",AdminHover+901+section*2,hot,e))NativeDevGrants.SetAll(p.unlocks,AdminChanges(adminGrants,p.id),keys,false,NativeDevGrants.Server);
            }
            U.Fill(new Rect(x,y+30,w,1),C.Edge(.55f*e));U.Fill(new Rect(x,y+30,26,1),C.Cyan(.8f*e));return y+44;
        }
        private static bool AdminLink(Rect r,string text,int id,bool hot,float e)
        {
            bool over=hot&&U.Hover(r);float h=U.HoverAmount(id,r,over);
            U.Round(r,U.A(U.Mix(U.Hex(0x0d1330),U.Hex(0x15305a),h),.92f*e),5,U.A(U.Mix(C.Edge(),C.Cyan(),h),e));
            U.Text(r,text,10,U.A(U.Mix(C.Muted(),C.Ink(),h),e),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
            return over&&U.Button(r);
        }
        // A DEV class: its icon in its colour, name and kind, its entrance, and a CLASS and an ENTRANCE toggle (a strip saying DEV
        // accounts only where the server grants neither).
        private void AdminClassCard(Rect r,AdminItem it,int i,NativeAccountData p,Dictionary<string,bool> g,bool hot,float e)
        {
            bool over=hot&&U.Hover(r);float h=U.HoverAmount(AdminHover+100+i,r,over);bool on=NativeDevGrants.On(p.unlocks,g,it.Key),locked=!it.Server&&(it.Entrance==null||!it.Entrance.Server);
            var d=new Rect(r.x,r.y-1.5f*h,r.width,r.height);float a=locked?.72f:1;
            if(on)U.Glow(new Rect(d.x+12,d.y+14,d.width-24,d.height-14),U.A(it.Tone,.16f*e),16);
            U.Round(d,U.A(U.Mix(U.Hex(0x0f142b),U.Hex(0x141c3b),h),.95f*e),12,on?U.A(U.Mix(it.Tone,Color.white,.2f),.95f*e):U.A(U.Mix(C.Edge(),locked?AdminDevEdge:C.Cyan(.7f),h*.8f),e),on?1.5f:1);
            var well=new Rect(d.x+14,d.y+14,52,52);U.Radial(new Rect(well.x-10,well.y-10,72,72),U.A(it.Tone,(on?.24f:.1f)*e));
            U.Round(well,U.Hex(0x080b18,e),12,U.A(it.Tone,(on?.95f:.45f)*a*e));NativeIcons.Draw(new Rect(well.center.x-16,well.center.y-16,32,32),it.Icon,U.A(it.Tone,a*e));
            float tx=well.xMax+14,tw=d.xMax-14-tx;
            U.Text(new Rect(tx,d.y+15,tw,24),AdminFit(it.Name,17,true,tw),17,U.A(C.Ink(),a*e),TextAnchor.MiddleLeft,bold:true);
            U.Text(new Rect(tx,d.y+41,tw,16),it.Kind,10,U.A(C.Muted(),a*e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            var line=new Rect(d.x+14,d.y+78,d.width-28,16);NativeIcons.Draw(new Rect(line.x,line.y+1,14,14),"sparkles",U.A(U.Hex(0xb7a8ff),a*e));
            U.Text(new Rect(line.x+20,line.y,line.width-20,line.height),AdminFit(it.Sub,12,false,line.width-20),12,U.A(C.Muted(),a*e),TextAnchor.MiddleLeft);
            float cy=d.yMax-12-32;
            if(locked)
            {
                var strip=new Rect(d.x+14,cy,d.width-28,32);U.Round(strip,U.A(AdminDevFill,.85f*e),8,U.A(AdminDevEdge,.45f*e));
                NativeIcons.Draw(new Rect(strip.x+10,strip.center.y-8,16,16),"shield-alert",U.A(AdminDevInk,e));
                U.Text(new Rect(strip.x+34,strip.y,strip.width-40,strip.height),"DEV ACCOUNTS ONLY",10,U.A(AdminDevInk,e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
                if(strip.width>300)U.Text(new Rect(strip.x,strip.y,strip.width-12,strip.height),"not grantable yet",11,C.Muted(.9f*e),TextAnchor.MiddleRight);
                return;
            }
            float chip=(d.width-28-8)/2;
            AdminChip(new Rect(d.x+14,cy,it.Entrance!=null?chip:d.width-28,32),"CLASS",it,p,g,AdminHover+300+i*2,hot,e);
            if(it.Entrance!=null)AdminChip(new Rect(d.x+14+chip+8,cy,chip,32),"ENTRANCE",it.Entrance,p,g,AdminHover+301+i*2,hot,e);
        }
        private void AdminChip(Rect r,string label,AdminItem it,NativeAccountData p,Dictionary<string,bool> g,int id,bool hot,float e)
        {
            if(!it.Server)
            {
                U.Round(r,U.A(AdminDevFill,.8f*e),8,U.A(AdminDevEdge,.35f*e));NativeIcons.Draw(new Rect(r.x+9,r.center.y-7,14,14),"shield-alert",U.A(AdminDevInk,.9f*e));
                U.Text(new Rect(r.x+28,r.y,r.width-32,r.height),label+" · DEV ONLY",10,U.A(AdminDevInk,.85f*e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);return;
            }
            bool on=NativeDevGrants.On(p.unlocks,g,it.Key),changed=g!=null&&g.ContainsKey(it.Key),over=hot&&U.Hover(r);float h=U.HoverAmount(id,r,over);
            U.Round(r,on?U.A(U.Mix(C.Cyan(),U.Hex(0x06202c),.8f),.96f*e):U.A(U.Mix(U.Hex(0x0a0f22),U.Hex(0x13204a),h),.96f*e),8,on?C.Cyan(.9f*e):U.A(U.Mix(C.Edge(),C.Cyan(.8f),h),e));
            var box=new Rect(r.x+10,r.center.y-8,16,16);U.Round(box,on?C.Cyan(e):U.Hex(0x060914,e),4,on?C.CyanSoft(e):U.A(U.Mix(C.Dim(),C.Cyan(.8f),h),e));
            if(on)NativeIcons.Draw(new Rect(box.x+2,box.y+2,12,12),"check",C.Void(e));
            U.Text(new Rect(r.x+34,r.y,r.width-40,r.height),label,10,on?C.Ink(e):U.A(U.Mix(C.Muted(),C.Ink(),h),e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            if(changed)U.RadialStops(new Rect(r.xMax-13,r.y+6,7,7),C.Warn(e),AdminDiscAt,AdminDiscA);
            if(over&&U.Button(r))NativeDevGrants.Toggle(p.unlocks,AdminChanges(adminGrants,p.id),it.Key,NativeDevGrants.Server);
        }
        // A DEV look as the Locker shows it (its emblem, hat or entrance, in the player's colours), ticked when they have it.
        private void AdminLookCard(Rect r,AdminItem it,int i,AdminRow row,Dictionary<string,bool> g,bool hot,float e)
        {
            var p=row.P;bool over=hot&&U.Hover(r);float h=U.HoverAmount(AdminHover+500+i,r,over);bool on=NativeDevGrants.On(p.unlocks,g,it.Key),changed=g!=null&&g.ContainsKey(it.Key);
            var d=new Rect(r.x,r.y-1.5f*h,r.width,r.height);float a=it.Server?1:.72f;
            if(on)U.Glow(new Rect(d.x+10,d.y+12,d.width-20,d.height-12),C.Cyan(.12f*e),14);
            U.Round(d,U.A(U.Mix(U.Hex(0x0f142b),U.Hex(0x141c3b),h),.95f*e),12,on?C.Cyan(.9f*e):U.A(U.Mix(C.Edge(),it.Server?C.Cyan(.7f):AdminDevEdge,h*.8f),e),on?1.5f:1);
            var v=new Rect(d.x+8,d.y+8,d.width-16,84);U.Round(v,U.Hex(0x070a16,e),9);
            if(Event.current.type==EventType.Repaint)
            {
                float t=Time.unscaledTime;U.BeginBand(v);
                if(it.Look==1)LkKillEmblem(v,it.Id,over?Mathf.Repeat(t/2.8f+i*.13f,1):LkHero(it.Id)+.03f*Mathf.Sin(t*1.3f+i),a*e,row.Body,row.Accent,t);
                else if(it.Look==2)LkHatVisual(v,it.Id,a*e,h,row.Body,row.Accent);
                else LkIntroVisual(v,it.Icon,it.Tone,a*e,h,t,i*.17f,false);
                U.EndBand();
            }
            if(on){var c=new Vector2(v.xMax-14,v.y+14);U.RadialStops(new Rect(c.x-11,c.y-11,22,22),C.Cyan(e),AdminDiscAt,AdminDiscA);NativeIcons.Draw(new Rect(c.x-7,c.y-7,14,14),"check",C.Void(e));}
            if(!it.Server)AdminTagL(v.x+8,v.y+8,"DEV ONLY",AdminDevFill,AdminDevEdge,AdminDevInk,e);
            if(changed)U.RadialStops(new Rect(on?v.xMax-36:v.xMax-16,v.y+10,8,8),C.Warn(e),AdminDiscAt,AdminDiscA);
            float tx=d.x+12,tw=d.width-24;
            U.Text(new Rect(tx,v.yMax+9,tw,20),AdminFit(it.Name,15,true,tw),15,U.A(C.Ink(),a*e),TextAnchor.MiddleLeft,bold:true);
            U.Text(new Rect(tx,v.yMax+30,tw,16),it.Server?it.Kind:"DEV accounts only",12,it.Server?C.Muted(e):U.A(AdminDevInk,.8f*e),TextAnchor.MiddleLeft);
            if(it.Server&&over&&U.Button(r))NativeDevGrants.Toggle(p.unlocks,AdminChanges(adminGrants,p.id),it.Key,NativeDevGrants.Server);
        }
        private void AdminFlagRow(Rect r,int i,NativeAccountData p,Dictionary<string,bool> f,bool hot,float e)
        {
            string key=NativeDevGrants.Flags[i];bool on=NativeDevGrants.On(p.flags,f,key),changed=f!=null&&f.ContainsKey(key),over=hot&&U.Hover(r);float h=U.HoverAmount(AdminHover+700+i,r,over);
            var d=new Rect(r.x,r.y-1.5f*h,r.width,r.height);
            U.Round(d,U.A(U.Mix(U.Hex(0x0f142b),U.Hex(0x141c3b),h),.95f*e),12,on?C.Cyan(.75f*e):U.A(U.Mix(C.Edge(),C.Cyan(.7f),h*.8f),e));
            var well=new Rect(d.x+12,d.center.y-20,40,40);U.Round(well,U.Hex(0x080b18,e),10,U.A(on?C.Cyan():C.Edge(),.7f*e));
            NativeIcons.Draw(new Rect(well.center.x-11,well.center.y-11,22,22),key=="vip"?"gem":key=="playtester"?"flask-conical":"shield-check",on?C.CyanSoft(e):C.Muted(e));
            float tx=well.xMax+12,sw=46,tw=d.xMax-16-sw-12-tx;
            U.Text(new Rect(tx,d.y+12,tw,20),NativeCosmeticRules.Label(key),15,C.Ink(e),TextAnchor.MiddleLeft,bold:true);
            U.Text(new Rect(tx,d.y+33,tw,18),AdminFit(key=="playtester"?"Marks the account as a playtester":key=="vip"?"Marks the account as a VIP":"A label on their account",12,false,tw),12,C.Muted(e),TextAnchor.MiddleLeft);
            var track=new Rect(d.xMax-16-sw,d.center.y-13,sw,26);U.Round(track,on?C.Cyan(.9f*e):U.Hex(0x080c1d,e),13,on?C.CyanSoft(e):U.A(U.Mix(C.Dim(),C.Cyan(.8f),h),e));
            float k=on?track.xMax-23:track.x+3;U.RadialStops(new Rect(k,track.y+3,20,20),on?C.Void(e):U.A(U.Mix(C.Muted(),C.Ink(),h),e),AdminDiscAt,AdminDiscA);
            if(changed)U.RadialStops(new Rect(track.x-14,track.y-2,7,7),C.Warn(e),AdminDiscAt,AdminDiscA);
            if(over&&U.Button(r))NativeDevGrants.Toggle(p.flags,AdminChanges(adminFlags,p.id),key,NativeDevGrants.Flags);
        }

        // ---- The bar: unsaved changes, Revert and Save -----------------------------------------------------------------
        private void AdminBar(Rect r,float e)
        {
            U.Fill(new Rect(r.x,r.y,r.width,1),C.Edge(.9f*e));U.Fill(new Rect(r.x,r.y,28*e,1),C.Cyan(.8f*e));
            var row=AdminRowFor(adminPick);int n=row!=null?AdminChangeCount(row.P):0;bool busy=account.Busy,saving=adminWaiting&&adminAction=="grant";
            var save=new Rect(r.xMax-214,r.y+16,214,44);var revert=new Rect(save.x-12-150,save.y,150,44);
            if(MenuButton(revert,"undo-2","Revert",null,false,n>0&&!busy,e,15,true)&&row!=null){adminGrants.Remove(row.P.id);adminFlags.Remove(row.P.id);adminError=null;}
            if(MenuButton(save,"check",saving?"Saving…":"Save changes",null,true,n>0&&!busy,e,15,true))AdminSave();
            float tx=r.x,tw=revert.x-20-tx,my=save.center.y;
            if(n>0)
            {
                if(adminBarFor.Item1!=n||adminBarFor.Item2!=row.P.id){adminBarFor=(n,row.P.id);adminBarText=(n==1?"1 unsaved change":n+" unsaved changes")+" for "+row.P.username;}
                U.RadialStops(new Rect(tx,my-14,9,9),C.Warn(e),AdminDiscAt,AdminDiscA);U.Text(new Rect(tx+18,my-20,tw-18,20),AdminFit(adminBarText,14,true,tw-18),14,C.Ink(e),TextAnchor.MiddleLeft,bold:true);
                U.Text(new Rect(tx+18,my+1,tw-18,18),AdminFit("Save sends them. The player gets them after refreshing their account and rejoining.",12,false,tw-18),12,C.Muted(e),TextAnchor.MiddleLeft);
            }
            else if(adminError!=null)
            {
                NativeIcons.Draw(new Rect(tx,my-9,18,18),"circle-alert",C.Danger(e));var lines=AdminWrap(adminError,13,tw-28,2);
                for(int i=0;i<lines.Count;i++)U.Text(new Rect(tx+28,my-lines.Count*9+i*18,tw-28,18),lines[i],13,U.Hex(0xff8aa0,e),TextAnchor.MiddleLeft);
            }
            else
            {
                string text=adminNote??"Grants reach a player after they refresh their account and rejoin.";NativeIcons.Draw(new Rect(tx,my-9,18,18),adminNote!=null?"check":"circle-help",adminNote!=null?C.Good(e):C.Dim(e));
                var lines=AdminWrap(text,13,tw-28,2);for(int i=0;i<lines.Count;i++)U.Text(new Rect(tx+28,my-lines.Count*9+i*18,tw-28,18),lines[i],13,C.Muted(e),TextAnchor.MiddleLeft);
            }
        }

        // ---- Small parts --------------------------------------------------------------------------------------------------
        private static void AdminHeading(float x,float y,float w,string label,string hint,float e)
        {
            U.Text(new Rect(x+2,y,w,22),label,11,C.CyanSoft(.95f*e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            if(hint!=null){float lw=U.Measure(label,11,true,true,.16f).x;U.Text(new Rect(x+lw+16,y,w-lw-16,22),hint,12,C.Muted(e),TextAnchor.MiddleLeft);}
            U.Fill(new Rect(x,y+30,w,1),C.Edge(.55f*e));U.Fill(new Rect(x,y+30,26,1),C.Cyan(.8f*e));
        }
        // A tag drawn leftwards from `right` (returns where the next one ends), or rightwards from `left` (returns where the next starts).
        private static float AdminTag(float right,float y,string text,Color fill,Color edge,Color ink,float e){float w=U.Measure(text,9,true,true,.1f).x+12;AdminTagAt(new Rect(right-w,y,w,17),text,fill,edge,ink,e);return right-w-5;}
        private static float AdminTagL(float left,float y,string text,Color fill,Color edge,Color ink,float e){float w=U.Measure(text,9,true,true,.1f).x+14;AdminTagAt(new Rect(left,y,w,18),text,fill,edge,ink,e);return left+w+6;}
        private static void AdminTagAt(Rect r,string text,Color fill,Color edge,Color ink,float e){U.Round(r,U.A(fill,.95f*e),4,U.A(edge,.8f*e));U.Text(r,text,9,U.A(ink,e),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);}
        private static int AdminCols(float w,float min)=>Mathf.Max(1,Mathf.FloorToInt((w+AdminGap)/(min+AdminGap)));
        private static float AdminGridH(int count,int cols,float h){int rows=(count+cols-1)/cols;return rows<=0?0:rows*h+(rows-1)*AdminGap;}
        private static void AdminThumb(Rect view,float scroll,float content,float e)
        {
            if(content<=view.height+1)return;float k=view.height/content,th=Mathf.Max(28,view.height*k),ty=view.y+(view.height-th)*(scroll/Mathf.Max(1,content-view.height));
            U.Fill(new Rect(view.xMax-3,view.y,1,view.height),C.Edge(.45f*e));U.Round(new Rect(view.xMax-5,ty,5,th),C.Cyan(.6f*e),2.5f);
        }
        // Text fitted to a width with an ellipsis, and wrapped lines, both cached (the screen builds no strings per frame).
        private string AdminFit(string text,float size,bool bold,float width)
        {
            if(string.IsNullOrEmpty(text))return text??"";var key=(text,(int)(size*10),Mathf.RoundToInt(width),bold);if(adminFits.TryGetValue(key,out var s))return s;if(adminFits.Count>512)adminFits.Clear();
            s=text;if(U.Measure(text,size,bold).x>width){int n=text.Length;while(n>1&&U.Measure(text.Substring(0,n)+"…",size,bold).x>width)n--;s=text.Substring(0,n).TrimEnd()+"…";}
            adminFits[key]=s;return s;
        }
        private List<string> AdminWrap(string text,float size,float width,int max)
        {
            var key=(text??"",(int)(size*10)+max*1000,Mathf.RoundToInt(width));if(adminWraps.TryGetValue(key,out var lines))return lines;if(adminWraps.Count>128)adminWraps.Clear();
            lines=U.Wrap(text??"",size,width,false,max);adminWraps[key]=lines;return lines;
        }
    }
}
