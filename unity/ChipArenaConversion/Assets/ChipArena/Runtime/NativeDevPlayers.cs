using System;
using System.Collections.Generic;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The DEV panel's PLAYERS tab (owner 2026-11-09: "Rework the dev menu (Where we can modify players); Make it cleaner and easier to use.
    // Make it look a lot better too. Everytime we make a dev class, add them as an option here."): the old Pilot and Interact tabs in one
    // place. The roster of every connected pilot and bot (you first) picks whom the tab works on; under it that player's card (their class,
    // health and the DEV flags the authority replicated), their class as icon tiles grouped GUNS, MELEE, MAGIC and DEV CLASSES (every devOnly
    // weapon in the content the account may play, so a new DEV class shows here with nothing to add) with one button to swap to the pick,
    // the actions that fit them as icon tiles (yours: restore, down, hit, teleport, clear summons, reveal; anyone's: launch, freeze, bring,
    // send, shrink, grow, debris, a hazard, dummies), your cheats as switches, the actions' tuning, and their chips. Every button still
    // resolves through Core NativeDevActions as before (the Pilot actions on you, the Interact ones on the picked player, devTarget), so the
    // sandbox and Astra's DEV authority see exactly the commands they did. Repaint draws allocate nothing once warm (labels made once per
    // class, no LINQ, no closures).
    public sealed partial class ConversionGame
    {
        // ---- The classes (made once, DevClassLists) ----------------------------------------------------------------------------------
        private static readonly string[] DevClassGroupNames={"GUNS","MELEE","MAGIC","DEV CLASSES"};
        private const int DevClassDev=3;
        private string[] devClassIds,devClassFull,devClassName1,devClassName2,devClassIcon,devClassDesc,devClassGroupTitle,devSwapYou,devSwapYouReset,devSwapThem;
        private int[] devClassGroup;private Color[] devClassTint;
        // The class picked to swap to (null: none), and whose it is.
        private string devClassPick;private int devClassPickFor=-1;
        private const string DevPickHint="Pick a class above to swap to it",DevPickWhy="Their class now is marked NOW",DevNow="NOW",DevClassDevTag="DEV",DevYou="YOU",DevBot="BOT",DevPilotTag="PILOT",DevOut="OUT";
        private const string DevMatchOnly="Match-only fun: every action ends with the match and never reduces anyone's progression, purchases or rewards. Shrink and grow are looks only.";
        /// <summary>Every class this account may play (the Locker's own filter, AllowedWeapon), grouped: guns, melee and magic, and every devOnly
        /// one under DEV CLASSES, so a DEV class added to the content shows here with nothing more to do.</summary>
        private void DevClassLists()
        {
            if(devClassIds!=null)return;var picked=new List<ContentWeapon>();
            for(int g=0;g<DevClassGroupNames.Length;g++)foreach(var w in content.weapons)if(w!=null&&AllowedWeapon(w)&&DevClassGroupOf(w)==g)picked.Add(w);
            int n=picked.Count;var counts=new int[DevClassGroupNames.Length];
            devClassIds=new string[n];devClassFull=new string[n];devClassName1=new string[n];devClassName2=new string[n];devClassIcon=new string[n];devClassDesc=new string[n];
            devClassGroup=new int[n];devClassTint=new Color[n];devSwapYou=new string[n];devSwapYouReset=new string[n];devSwapThem=new string[n];
            for(int i=0;i<n;i++)
            {
                var w=picked[i];string name=(string.IsNullOrEmpty(w.name)?w.id:w.name).ToUpperInvariant();
                devClassIds[i]=w.id;devClassFull[i]=name;DevSplit(name,out devClassName1[i],out devClassName2[i]);devClassIcon[i]=NativeIcons.Spec(w.id);
                string desc=NativeLoadoutText.Text(w);devClassDesc[i]=desc.Length==0?name:desc;devClassGroup[i]=DevClassGroupOf(w);counts[devClassGroup[i]]++;
                var tint=U.Hex((int)w.color);devClassTint[i]=tint.maxColorComponent<.3f?C.CyanSoft():tint;
                devSwapYou[i]="Become "+name;devSwapYouReset[i]="Become "+name+" · resets the arena";devSwapThem[i]="Make them "+name;
            }
            devClassGroupTitle=new string[DevClassGroupNames.Length];for(int g=0;g<counts.Length;g++)devClassGroupTitle[g]=DevClassGroupNames[g]+" · "+counts[g];
        }
        private static int DevClassGroupOf(ContentWeapon w)=>w.devOnly?DevClassDev:w.@class=="melee"?1:w.@class=="magic"?2:0;
        // A tile's name on one line, or on two split at the space nearest its middle.
        private static void DevSplit(string name,out string a,out string b)
        {
            a=name;b=null;if(name.Length<=10)return;int best=-1,mid=name.Length/2;
            for(int i=0;i<name.Length;i++)if(name[i]==' '&&(best<0||Math.Abs(i-mid)<Math.Abs(best-mid)))best=i;
            if(best>0){a=name.Substring(0,best);b=name.Substring(best+1);}
        }
        private int DevClassIndex(string id){if(devClassIds!=null&&id!=null)for(int i=0;i<devClassIds.Length;i++)if(devClassIds[i]==id)return i;return -1;}
        private Color DevClassTint(string spec){int k=DevClassIndex(spec);return k>=0?devClassTint[k]:C.CyanSoft();}

        // ---- The tab --------------------------------------------------------------------------------------------------------------
        private static readonly string[] DevYouActs={"me-restore","me-down","me-hit","me-teleport","me-summons","me-reveal","launch","freeze","shrink","grow","debris-rain","hazard-at"};
        private static readonly string[] DevThemActs={"target-restore","target-down","launch","freeze","bring","send","shrink","grow","debris-rain","target-debris","hazard-at","spawn-dummies-around"};
        private static readonly string[] DevCheatIcons={"shield-check","layers","hourglass"};
        private float DevPlayersTab(float x,float y,float w)
        {
            DevLists();DevClassLists();var me=DevLocal();
            // Whom the tab works on: the picked pilot, else you.
            var p=DevPilot(devTarget);if(p==null||!p.Seat.connected||p.Seat.AuthorityBoss){devTarget=arena.LocalId;p=me;}
            if(p==null)return DevNote(x,y,w,"No pilot to work on yet.",C.Muted());
            bool you=p.Seat.id==arena.LocalId;if(devClassPickFor!=p.Seat.id){devClassPick=null;devClassPickFor=p.Seat.id;}
            y=DevSection(x,y,w,"PLAYERS","users");y=DevRoster(x,y,w);
            y=DevPlayerCard(x,y+2,w,p);
            if(you&&me.Seat.devCommandsRan)y=DevNote(x,y,w,"No progression this match: DEV commands ran for your account (NativeMatch.CanEarnProgression).",C.Magenta());
            // Their class: the tiles, then one button to swap to the pick.
            y=DevSection(x,y,w,you?"YOUR CLASS":"THEIR CLASS","swords");
            string current=p.Seat.spec;y=DevClassGrid(x,y,w,current,ref devClassPick);
            {
                int k=DevClassIndex(devClassPick);bool change=k>=0&&devClassPick!=current;string id=you?"me-weapon":"swap-weapon";
                if(you)devWeapon=change?devClassPick:current;else devTargetWeapon=change?devClassPick:current;
                var r=new Rect(x,y,w,36);
                if(!change){if(Event.current.type==EventType.Repaint&&DevVisible(r)){H.Cut(r,C.Indigo(.45f),6,H.Corners.Diagonal);U.Text(new Rect(r.x+14,r.y,r.width-28,r.height),DevPickHint,11,C.Muted(),TextAnchor.MiddleCenter);}}
                else{var a=NativeDevActions.Find(id);var state=NativeDevActions.Resolve(a,devCtx,out string reason);if(DevButton(r,you?(devMode==NativeDevMode.Sandbox?devSwapYouReset[k]:devSwapYou[k]):devSwapThem[k],state,reason,true)){DevInvoke(id);devClassPick=null;}}
                y+=44;
            }
            // What to do to them.
            y=DevSection(x,y,w,"ACTIONS","wand-sparkles");y=DevActionTiles(x,y,w,you?DevYouActs:DevThemActs);
            if(you&&me.Seat.devRevealed)y=DevNote(x,y,w,"Revealed for this match: everyone sees your DEV tag.",C.Magenta());
            else if(!you)y=DevNote(x,y,w,DevMatchOnly,C.Muted());
            // Your cheats, as switches (DEV commands: the switch shows what the authority replicated).
            if(you)
            {
                y=DevSection(x,y,w,"CHEATS","shield-check");
                for(int i=0;i<DevPersonalToggles.Length;i++)
                {
                    string id=DevPersonalToggles[i];var a=NativeDevActions.Find(id);var state=NativeDevActions.Resolve(a,devCtx,out string reason);
                    bool now=id=="me-god"?me.DevGod:id=="me-ammo"?me.DevInfiniteAmmo:me.DevNoCooldowns;
                    if(DevSwitch(new Rect(x,y,w,38),a.Label,DevCheatIcons[i],now,state,reason))DevSend(id,now?"off":"on");y+=44;
                }
            }
            // The actions' tuning.
            y=DevSection(x,y,w,"TUNING","settings");
            {
                NativeDevActions.Resolve(NativeDevActions.Find("launch"),devCtx,out string why);float half=(w-14)/2;
                devLaunchForce=DevSlider(new Rect(x,y,half,34),"Launch speed",devLaunchForce,NativeDevContract.LaunchMin,NativeDevContract.LaunchMax,0," m/s",NativeDevState.Live,why,out _);
                devFreezeSeconds=DevSlider(new Rect(x+half+14,y,half,34),"Freeze",devFreezeSeconds,0,NativeDevContract.FreezeMax,1," s",NativeDevState.Live,why,out _);y+=44;
                devAroundCount=Mathf.RoundToInt(DevSlider(new Rect(x,y,half,34),"Dummies around",devAroundCount,NativeDevContract.AroundMin,NativeDevContract.AroundMax,0,"",NativeDevState.Live,why,out _));
                float my=y;if(Event.current.type==EventType.Repaint&&DevVisible(new Rect(x+half+14,my,half,18)))U.Text(new Rect(x+half+14,my,half,18),"Debris",12,C.Ink(),TextAnchor.MiddleLeft);my+=20;
                devRainMaterial=DebrisMaterials[DevChoice(x+half+14,ref my,half,DebrisMaterials,DevIndex(DebrisMaterials,devRainMaterial),NativeDevState.Live,why,3,"Debris rain material")];
                y=Mathf.Max(y+44,my);
            }
            // Their chips.
            y=DevSection(x,y,w,"CHIPS","cpu");
            if(DevButton(new Rect(x,y,w,34),you?"Open my chips":"Open their chips",NativeDevState.Live,"The Chips tab, on this player")){devChipSeat=p.Seat.id;devTab=NativeDevTab.Chips;}y+=42;
            return y;
        }

        // ---- Pieces ---------------------------------------------------------------------------------------------------------------
        /// <summary>A section's head: its icon, its title and a rule.</summary>
        private float DevSection(float x,float y,float w,string title,string icon)
        {
            y+=8;
            if(Event.current.type==EventType.Repaint&&DevVisible(new Rect(x,y,w,18)))
            {
                NativeIcons.Draw(new Rect(x,y+1,14,14),icon,C.Cyan());float tw=U.Measure(title,11,true,true,.14f).x;
                U.Text(new Rect(x+21,y,tw+4,16),title,11,C.Ink(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
                U.Fill(new Rect(x+29+tw,y+8,Mathf.Max(0,w-29-tw),H.Px),C.Edge(.9f));
            }
            return y+26;
        }
        private float DevSubHead(float x,float y,float w,string title,Color tone)
        {
            if(Event.current.type==EventType.Repaint&&DevVisible(new Rect(x,y,w,16))){U.Fill(new Rect(x,y+4,3,9),tone);U.Text(new Rect(x+9,y,w-9,16),title,9,tone,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);}
            return y+20;
        }
        /// <summary>Every connected pilot and bot as a card, you first (DEV DUMMY labelled); clicking one picks it (devTarget, shared with
        /// the Monsters tab).</summary>
        private float DevRoster(float x,float y,float w)
        {
            int col=0;float gap=6,cw=(w-gap)/2,ch=48;
            for(int pass=0;pass<2;pass++)
                foreach(var p in arena.Pilots)
                {
                    if(!p.Seat.connected||p.Seat.AuthorityBoss)continue;bool you=p.Seat.id==arena.LocalId;if(you!=(pass==0))continue;
                    if(DevRosterCard(new Rect(x+col*(cw+gap),y,cw,ch),p,you))devTarget=p.Seat.id;
                    if(++col==2){col=0;y+=ch+gap;}
                }
            if(col!=0)y+=ch+gap;return y;
        }
        private bool DevRosterCard(Rect r,NativePilot p,bool you)
        {
            int id=9000+devWid++;bool on=p.Seat.id==devTarget;float h=devBodyHot&&DevVisible(r)?U.HoverAmount(id,r):0;
            if(Event.current.type==EventType.Repaint&&DevVisible(r))
            {
                var tint=DevClassTint(p.Seat.spec);
                H.Cut(r,on?C.Cyan(.16f):U.Mix(C.Indigo(.55f),C.Indigo(.9f),h),6,H.Corners.Diagonal);H.Outline(r,on?C.Cyan(.85f):C.Edge(.45f+.45f*h),6);
                U.Fill(new Rect(r.x+5,r.y+9,2,r.height-18),match.Teams?TeamColor(p.Seat.team):SeatColor(p.Seat));
                NativeIcons.Draw(new Rect(r.x+14,r.y+13,22,22),p.Seat.bot?"bot":NativeIcons.Spec(p.Seat.spec),on?tint:U.A(tint,.7f+.3f*h));
                float tx=r.x+44,right=r.xMax-74;
                U.Text(new Rect(tx,r.y+6,right-tx,18),p.Seat.name,12,C.Ink(),TextAnchor.MiddleLeft,bold:on);
                U.Text(new Rect(tx,r.y+24,right-tx,14),p.Weapon.name,9,C.Muted(),TextAnchor.MiddleLeft,mono:true);
                string tag=you?DevYou:p.Seat.devDummy?NativeDevTags.DevDummy:p.Seat.bot?DevBot:DevPilotTag;
                U.Text(new Rect(r.xMax-72,r.y+6,64,14),tag,8,you?C.CyanSoft():p.Seat.devDummy?C.Magenta():C.Muted(),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.1f);
                float max=Mathf.Max(1,(float)p.Stats["hp"]);H.Bar(new Rect(r.xMax-72,r.y+24,64,5),Mathf.Max(0,p.Hp),max,10,p.Hp>0?C.Good():C.Danger(),C.Edge(.6f));
                U.Text(new Rect(r.xMax-72,r.y+30,64,14),p.Hp>0?NativeDevText.Int(Mathf.CeilToInt(p.Hp)):DevOut,8,p.Hp>0?C.Ink(.7f):C.Danger(),TextAnchor.MiddleRight,mono:true);
            }
            return DevClick(r);
        }
        /// <summary>The picked player's card: their class's badge in its colour, their name, class and health, and the DEV flags the
        /// authority replicated (devGod / devInfiniteAmmo / devNoCooldowns, devRevealed, devScale, devDummy).</summary>
        private float DevPlayerCard(float x,float y,float w,NativePilot p)
        {
            var card=new Rect(x,y,w,84);
            if(Event.current.type==EventType.Repaint&&DevVisible(card))
            {
                var tint=DevClassTint(p.Seat.spec);H.Cut(card,C.Indigo(.82f),8,H.Corners.Diagonal);H.Outline(card,U.A(tint,.5f),8);
                var badge=new Rect(card.x+12,card.y+12,60,60);H.Cut(badge,U.A(tint,.16f),8,H.Corners.Diagonal);H.Outline(badge,U.A(tint,.85f),8,H.Corners.Diagonal,1.5f);
                NativeIcons.Draw(new Rect(badge.x+13,badge.y+13,34,34),p.Seat.bot?"bot":NativeIcons.Spec(p.Seat.spec),tint);
                float tx=badge.xMax+14,tw=card.xMax-12-tx;
                U.Text(new Rect(tx,card.y+10,tw,22),p.Seat.name,17,C.Ink(),TextAnchor.MiddleLeft,bold:true);
                U.Text(new Rect(tx,card.y+32,tw,14),p.Weapon.name,10,tint,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.06f);
                float max=Mathf.Max(1,(float)p.Stats["hp"]);var bar=new Rect(tx,card.y+56,Mathf.Max(20,tw-84),6);H.Bar(bar,Mathf.Max(0,p.Hp),max,16,p.Hp>0?C.Good():C.Danger(),C.Edge(.6f));
                U.Text(new Rect(bar.xMax+6,card.y+48,78,20),NativeDevText.Count(Mathf.CeilToInt(Mathf.Max(0,p.Hp)),Mathf.RoundToInt(max)),11,p.Hp>0?C.Good():C.Danger(),TextAnchor.MiddleRight,bold:true,mono:true);
            }
            y+=92;float px=x;
            px=DevPill(px,y,"GOD",p.DevGod,C.Good());px=DevPill(px,y,"INF AMMO",p.DevInfiniteAmmo,C.Good());px=DevPill(px,y,"NO CD",p.DevNoCooldowns,C.Good());
            px=DevPill(px,y,"REVEALED",p.Seat.devRevealed,C.Magenta());px=DevPill(px,y,p.Seat.devScale<1?"SHRUNK":p.Seat.devScale>1?"GROWN":"SCALED",p.Seat.devScale!=1,C.Magenta());
            if(p.Seat.devDummy)DevPill(px,y,NativeDevTags.DevDummy,true,C.Magenta());
            return y+26;
        }
        /// <summary>The class tiles, grouped (guns, melee, magic, DEV classes): `current` marked NOW; a click sets `pick`. Returns y.</summary>
        private float DevClassGrid(float x,float y,float w,string current,ref string pick)
        {
            DevClassLists();int columns=Mathf.Clamp(Mathf.FloorToInt((w+6)/92),3,7);float gap=6,tw=(w-gap*(columns-1))/columns,th=62;
            for(int g=0;g<DevClassGroupNames.Length;g++)
            {
                int c=0;bool any=false;
                for(int i=0;i<devClassIds.Length;i++)
                {
                    if(devClassGroup[i]!=g)continue;
                    if(!any){y=DevSubHead(x,y,w,devClassGroupTitle[g],g==DevClassDev?C.Magenta():C.Muted());any=true;}
                    string cid=devClassIds[i];if(DevClassTile(new Rect(x+c*(tw+gap),y,tw,th),i,cid==current,cid==pick&&cid!=current))pick=cid;
                    if(++c==columns){c=0;y+=th+gap;}
                }
                if(any&&c!=0)y+=th+gap;
                if(any)y+=4;
            }
            return y;
        }
        /// <summary>A class tile: its icon in its colour over its name; NOW on the class they have, lit when picked, DEV on a DEV class.</summary>
        private bool DevClassTile(Rect r,int i,bool current,bool picked)
        {
            int id=9000+devWid++;bool dev=devClassGroup[i]==DevClassDev;float h=devBodyHot&&DevVisible(r)?U.HoverAmount(id,r):0;
            if(h>.5f){devHoverLabel=devClassFull[i];devHoverReason=devClassDesc[i];}
            if(Event.current.type==EventType.Repaint&&DevVisible(r))
            {
                var tint=devClassTint[i];var d=new Rect(r.x,r.y-h*1.5f,r.width,r.height);
                H.Cut(d,picked?C.Cyan(.22f):current?U.A(tint,.13f):U.Mix(C.Indigo(.7f),C.Indigo(.95f),h),6,H.Corners.Diagonal);
                H.Outline(d,picked?C.Cyan():current?U.A(tint,.9f):dev?C.Magenta(.4f+.45f*h):C.Edge(.55f+.4f*h),6,H.Corners.Diagonal,picked||current?1.5f:1);
                float icon=Mathf.Min(24,d.height*.4f);NativeIcons.Draw(new Rect(d.center.x-icon/2,d.y+8,icon,icon),devClassIcon[i],U.A(tint,picked||current?1:.72f+.28f*h));
                float ty=d.y+12+icon;Color ink=picked||current?C.Ink():C.Ink(.85f);
                if(devClassName2[i]==null)U.Text(new Rect(d.x+2,ty,d.width-4,16),devClassName1[i],9,ink,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.04f);
                else{U.Text(new Rect(d.x+2,ty-2,d.width-4,12),devClassName1[i],8,ink,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.02f);U.Text(new Rect(d.x+2,ty+9,d.width-4,12),devClassName2[i],8,ink,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.02f);}
                if(current){var tag=new Rect(d.xMax-33,d.y+4,29,12);H.Tag(tag,tint,.9f,false);U.Text(tag,DevNow,7,tint,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);}
                else if(dev)U.Text(new Rect(d.xMax-30,d.y+3,26,12),DevClassDevTag,7,C.Magenta(.95f),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.08f);
            }
            return DevClick(r);
        }
        /// <summary>Actions as icon tiles in rows (four on a wide panel, else three).</summary>
        private float DevActionTiles(float x,float y,float w,string[] ids)
        {
            int columns=w>=470?4:3;float gap=6,tw=(w-gap*(columns-1))/columns,th=52;
            for(int i=0;i<ids.Length;i++){int c=i%columns;DevActTile(new Rect(x+c*(tw+gap),y,tw,th),ids[i]);if(c==columns-1||i==ids.Length-1)y+=th+gap;}
            return y;
        }
        private bool DevActTile(Rect r,string id)
        {
            var a=NativeDevActions.Find(id);var state=NativeDevActions.Resolve(a,devCtx,out string reason);bool live=state==NativeDevState.Live;
            int wid=9000+devWid++;float h=devBodyHot&&DevVisible(r)?U.HoverAmount(wid,r):0;if(h>.5f){devHoverLabel=a?.Label??id;devHoverReason=reason;}
            if(Event.current.type==EventType.Repaint&&DevVisible(r))
            {
                Color tone=live?C.Cyan():state==NativeDevState.SandboxOnly||state==NativeDevState.Unavailable?C.Dim():C.Warn();var d=new Rect(r.x,r.y-h*1.5f,r.width,r.height);
                H.Cut(d,U.Mix(C.Indigo(.82f),C.Indigo(),h),6,H.Corners.Diagonal);H.Outline(d,U.A(tone,live?.32f+.58f*h:.3f+.25f*h),6);
                NativeIcons.Draw(new Rect(d.center.x-9,d.y+8,18,18),DevActIcon(id),live?U.Mix(C.CyanSoft(),C.Ink(),h):C.Ink(.38f));
                U.Text(new Rect(d.x+4,d.y+29,d.width-8,16),DevActShort(id,a),10,live?C.Ink():C.Ink(.5f),TextAnchor.MiddleCenter,bold:true);
                if(!live){string badge=NativeDevActions.Badge(state);float bw=U.Measure(badge,7,true,true,.06f).x+8;var b=new Rect(d.xMax-bw-4,d.y+4,bw,12);H.Tag(b,tone,.9f,false);U.Text(b,badge,7,tone,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.06f);}
                else if(a?.Key!=null){float kw=H.KeyWidth(a.Key,13);H.Key(new Rect(d.xMax-kw-4,d.y+4,kw,13),a.Key);}
            }
            if(DevClick(r)){DevInvoke(id);return true;}return false;
        }
        private static string DevActIcon(string id)
        {
            switch(id)
            {
                case "me-restore":case "target-restore":return "heart-plus";case "me-down":case "target-down":return "skull";case "me-hit":return "zap";
                case "me-teleport":return "corner-up-right";case "me-summons":return "x";case "me-reveal":return "scan-line";case "launch":return "rocket";
                case "freeze":return "snowflake";case "bring":return "magnet";case "send":return "move-right";case "shrink":return "shrink";case "grow":return "expand";
                case "debris-rain":return "boxes";case "target-debris":return "brick-wall";case "hazard-at":return "radiation";case "spawn-dummies-around":return "bot";
                default:return "sparkles";
            }
        }
        private static string DevActShort(string id,NativeDevAction a)
        {
            switch(id)
            {
                case "me-restore":case "target-restore":return "Restore";case "me-down":case "target-down":return "Down";case "me-hit":return "Hit (18)";
                case "me-teleport":return "Teleport";case "me-summons":return "Clear summons";case "me-reveal":return "Reveal me";case "launch":return "Launch";
                case "freeze":return "Freeze";case "bring":return "Bring here";case "send":return "Send to aim";case "shrink":return "Shrink";case "grow":return "Grow";
                case "debris-rain":return "Debris rain";case "target-debris":return "Debris";case "hazard-at":return "Hazard";case "spawn-dummies-around":return "Dummies";
                default:return a?.Label??id;
            }
        }
        /// <summary>A switch row: its icon, its label and a switch showing `value`; its state's badge when it needs the authority. True when
        /// clicked.</summary>
        private bool DevSwitch(Rect r,string label,string icon,bool value,NativeDevState state,string reason)
        {
            int id=9000+devWid++;bool live=state==NativeDevState.Live;float h=devBodyHot&&DevVisible(r)?U.HoverAmount(id,r):0;if(h>.5f){devHoverLabel=label;devHoverReason=reason;}
            if(Event.current.type==EventType.Repaint&&DevVisible(r))
            {
                H.Cut(r,value?C.Cyan(.12f):U.Mix(C.Indigo(.6f),C.Indigo(.95f),h),6,H.Corners.Diagonal);H.Outline(r,value?C.Cyan(.6f):C.Edge(.45f+.45f*h),6);
                NativeIcons.Draw(new Rect(r.x+12,r.center.y-8,16,16),icon,value?C.Cyan():live?C.Ink(.75f):C.Dim());
                var sw=new Rect(r.xMax-52,r.center.y-9,40,18);H.Cut(sw,value?C.Cyan(live?.9f:.4f):C.Edge(.8f),4,H.Corners.All);
                U.Fill(new Rect(value?sw.xMax-16:sw.x+2,sw.y+2,14,14),value?C.Void():live?C.Ink(.85f):C.Dim());
                float right=sw.x-10;
                if(!live){string badge=NativeDevActions.Badge(state);float bw=U.Measure(badge,8,true,true,.08f).x+10;var b=new Rect(right-bw,r.center.y-7,bw,14);Color t=state==NativeDevState.SandboxOnly||state==NativeDevState.Unavailable?C.Dim():C.Warn();H.Tag(b,t,.9f,false);U.Text(b,badge,8,t,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);right=b.x-8;}
                U.Text(new Rect(r.x+38,r.y,right-r.x-38,r.height),label,12,live?C.Ink():C.Ink(.55f),TextAnchor.MiddleLeft,bold:value);
            }
            return DevClick(r);
        }
    }
}
