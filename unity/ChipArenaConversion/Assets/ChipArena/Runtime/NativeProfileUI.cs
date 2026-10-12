using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public static class NativeCosmeticRules
    {
        public static readonly string[] Hats={"none","cap","beanie","top","wizard","crown","cone","antenna","halo","cowboy","auditor-halo","sheldon-head"};
        public static readonly string[] Kills={"impact-ragdoll","ragdoll","ragdoll-ember","ragdoll-frost","ragdoll-static","ragdoll-gravity","ragdoll-pinball","ragdoll-fireworks","ragdoll-soul","ragdoll-petals","voxel-burst","confetti","ascension","shrink","launch","ghost","dev-gibber","dev-singularity","dev-raging-demon","dev-clown","dev-clown-love","dev-bass-van"};
        // The pickable entrances (NativeIntroRules.Entrances: procedural, weapon signatures, DEV; and the DEV-only Sheldon Intro,
        // which the Locker lists only with live DEV access, NativeCosmeticGate). The retired generic five are
        // gone (owner 2026-10-11); a saved or received one normalizes to no pick (Intro, OverrideIntro).
        public static readonly string[] Intros={"orbital-drop","grid-rez","holo-projection","blackout","skyline-zip","hero-landing","overclock","weapon-sword","weapon-daggers","weapon-shield","weapon-greatsword","weapon-pistol","weapon-rifle","weapon-machinegun","weapon-fire","weapon-water","weapon-lightning","weapon-darkness","dev-emperor","dev-ansatsuken","dev-matrix","dev-siren","sheldon-intro"};
        // Display names from the source (shared/cosmetics.ts HATS / KILL_STYLES with ragdoll-styles.ts, shared/intros.ts with
        // weapon-intros.ts, shared/access.ts GRANTABLE_CONTENT / ACCOUNT_FLAGS); ids stay the saved and sent values.
        private static readonly System.Collections.Generic.Dictionary<string,string> Labels=new System.Collections.Generic.Dictionary<string,string>
        {
            {"auditor-halo","Auditor Halo · DEV ONLY"},{"sheldon-head","Sheldon Head · DEV ONLY"},{"none","No hat"},{"cap","Ball cap"},{"beanie","Beanie"},{"top","Top hat"},{"wizard","Wizard hat"},{"crown","Crown"},{"cone","Traffic cone"},{"antenna","Antenna"},{"halo","Halo"},{"cowboy","Cowboy hat"},
            {"impact-ragdoll","Impact ragdoll & gibs"},{"dev-gibber","Gibber · DEV ONLY"},{"ragdoll","Ragdoll"},{"ragdoll-ember","Emberfall"},{"ragdoll-frost","Frostbreak"},{"ragdoll-static","Static Aftershock"},{"ragdoll-gravity","Gravity Collapse"},{"ragdoll-pinball","Pinball"},{"ragdoll-fireworks","Firework Finale"},{"ragdoll-soul","Soul Drift"},{"ragdoll-petals","Petal Scatter"},{"dev-singularity","Singularity Override · DEV ONLY"},
            {"dev-raging-demon","Raging Demon · DEV ONLY"},{"voxel-burst","Voxel breakup"},{"confetti","Confetti cannon"},{"ascension","Beam me up"},{"shrink","Pocket-sized"},{"launch","Team Rocket"},{"ghost","Ghost escape"},{"dev-clown","Clown getaway · DEV ONLY"},{"dev-clown-love","Clown Love · DEV ONLY"},{"dev-bass-van","Bass Van · DEV ONLY"},
            {"dev-siren","DEV · Black Siren Protocol"},{"sheldon-intro","DEV · Sheldon Intro"},{"dev-emperor","DEV · Emperor’s Oath"},{"dev-ansatsuken","DEV · Rain Warrior"},{"dev-matrix","DEV · L33T Throne"},
            {"weapon-sword","Flash Draw"},{"weapon-daggers","Twin Phantom"},{"weapon-greatsword","Colossus Oath"},{"weapon-shield","Bastion Drop"},{"weapon-pistol","Deadeye"},{"weapon-rifle","Longwatch"},{"weapon-machinegun","Belt Rattle"},{"weapon-fire","Cinder Crown"},{"weapon-water","Tidal Serpent"},{"weapon-lightning","Thunderstep"},{"weapon-darkness","Eventide Gate"},
            {"weapon-signature","Weapon Signature"},{"orbital-drop","Orbital Drop"},{"grid-rez","Grid Rez"},{"holo-projection","Holo Projection"},{"blackout","Blackout"},{"skyline-zip","Skyline Zip"},{"hero-landing","Hero Landing"},{"overclock","Overclock"},
            {"weapon:siren","Siren DEV boss"},{"intro:dev-siren","Siren protocol intro"},{"weapon:emperor","Emperor Style + Mishima Chips"},{"intro:dev-emperor","Emperor storm intro"},{"weapon:leet","L33T H4x staff + L44T Chips"},{"weapon:ansatsuken","Ansatsuken + arcade Chips"},
            {"intro:dev-matrix","Matrix throne intro"},{"intro:dev-ansatsuken","Rain warrior intro"},{"hat:auditor-halo","Auditor Halo"},{"effect:dev-singularity","Singularity Override"},{"effect:dev-raging-demon","Raging Demon"},{"effect:dev-clown","Clown getaway"},{"effect:dev-clown-love","Clown Love"},{"effect:dev-bass-van","Bass Van"},
            {"playtester","Playtester"},{"vip","VIP"},
        };
        public static string Label(string id)=>id!=null&&Labels.TryGetValue(id,out var name)?name:(id??"").Replace("-"," ");
        public static string[] LabelsOf(string[] ids)=>ids.Select(Label).ToArray();
        public static string Name(string name){name=Regex.Replace(name??"Pilot","[<>\\p{Cc}\\p{Cf}]"," ").Trim();return name.Length==0?"Pilot":name.Substring(0,Math.Min(20,name.Length));}
        public static string IntroText(string value){value=Regex.Replace(value??"","[<>\\p{Cc}\\p{Cf}]"," ").Trim();return value.Substring(0,Math.Min(64,value.Length));}
        public static string Color(string value,string fallback)=>value!=null&&Regex.IsMatch(value,"^#[0-9a-fA-F]{6}$")?value:fallback;
        // The gate (Core/NativeHatLooks.cs NativeCosmeticGate): dev- items and the Auditor Halo need a grant; the Gibber, the
        // Sheldon Head and the Sheldon Intro (the owner's own) need live DEV access itself.
        public static bool Allowed(NativeAccess a,string type,string id)=>NativeCosmeticGate.Allowed(a,type,id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        public static string Hat(string value,NativeAccess a)=>(value=="sheldon-head"||Hats.Contains(value))&&Allowed(a,"hat",value)?value:"none";
        public static string Kill(string value,NativeAccess a)=>Kills.Contains(value)&&Allowed(a,"effect",value)?value:"impact-ragdoll";
        public static string Intro(string value,NativeAccess a)=>(value=="sheldon-intro"||Intros.Contains(value))&&Allowed(a,"intro",value)?value:NativeIntroRules.Default;
        /// <summary>The override flag that goes with a received or loaded pick: off unless the pick is one this client stages and may use.</summary>
        public static bool OverrideIntro(string value,bool overrideIntro,NativeAccess a)=>overrideIntro&&(value=="sheldon-intro"||Intros.Contains(value))&&Allowed(a,"intro",value);
    }
    public sealed partial class ConversionGame
    {
        private NativeAccount account;private bool rememberAccount=true;private string accountUser="",accountPassword="",accountRecovery="",accountAction="login",accountNotice="";
        private bool AllowedWeapon(ContentWeapon w)=>(!prefs.pve||w.id!="siren")&&(!w.devOnly||(account?.Access??NativeAccess.Guest).Allows("weapon:"+w.id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        private void InitializeAccount()
        {
            account=gameObject.AddComponent<NativeAccount>();account.Accepted=(data,load)=>{if(arena?.Local!=null)arena.Local.Seat.access=account.Access;if(server!=null)SendServer(new NativeNetMessage{type="auth-refresh",token=account.Token});if(!load)return;if(!data.hasProfile){account.Save(Profile());return;}var p=data.profile;if(p==null)return;prefs.name=NativeCosmeticRules.Name(p.name);prefs.accentColor=NativeCosmeticRules.Color(p.color,"#70e1d0");var c=p.cosmetics??new NativeCosmetics();prefs.bodyColor=NativeCosmeticRules.Color(c.bodyColor,"#e5e7eb");prefs.hat=NativeCosmeticRules.Hat(c.hat,account.Access);prefs.lobbyPose=c.lobbyPose??"weapon";if(!string.IsNullOrEmpty(c.lobbyBack))prefs.lobbyBack=NativePortraitBacks.Normalize(c.lobbyBack);if(!string.IsNullOrEmpty(c.hatColor))prefs.hatColor=NativeHatLooks.Normalize(c.hatColor);if(!string.IsNullOrEmpty(c.backColor))prefs.backColor=NativeHatLooks.Normalize(c.backColor);if(c.weaponSkins!=null)prefs.weaponSkins=NativeWeaponSkins.Normalize(c.weaponSkins);prefs.killEffect=NativeCosmeticRules.Kill(NativePendingEffect.Restore(prefs.pendingEffects,data.id,c.killEffect,e=>NativeCosmeticRules.Allowed(account.Access,"effect",e)),account.Access);string pick=NativePendingEffect.Restore(prefs.pendingIntros,data.id,c.intro,i=>NativeCosmeticRules.Intros.Contains(i)&&NativeCosmeticRules.Allowed(account.Access,"intro",i));prefs.overrideIntro=NativeCosmeticRules.OverrideIntro(pick,c.overrideWeaponIntro,account.Access);prefs.intro=NativeCosmeticRules.Intro(pick,account.Access);prefs.introText=c.introText;if(content.weapons.Any(w=>w.id==c.preferredSpec&&AllowedWeapon(w)))prefs.weapon=c.preferredSpec;accountPassword=accountRecovery="";Save();LockerRebase();};
            try{account.Configure(prefs.accountOrigin,true);}catch(ArgumentException){prefs.accountOrigin="";}
        }
        // The Customize screen is the Locker (NativeLockerUI.cs). Its colour rows offer these presets beside the #rrggbb field
        // (the source's colour picker, input type=color).
        private static readonly string[] BodyPresets={"#e5e7eb","#9ca3af","#4b5563","#1f2937","#fca5a5","#fdba74","#fde68a","#86efac","#93c5fd","#c4b5fd","#f9a8d4","#f4f4f5"};
        private static readonly string[] AccentPresets={"#70e1d0","#ff6b35","#ffd27a","#7ed78a","#69a6ff","#ba94f5","#ff6575","#65dce0","#f18dcc","#ffa565","#b8aabb","#ffffff"};
        public void ReloadSavedPilot()=>account?.ReloadSavedProfile();
        private void SavePilot()
        {
            prefs.name=NativeCosmeticRules.Name(prefs.name);prefs.introText=NativeCosmeticRules.IntroText(prefs.introText);prefs.bodyColor=NativeCosmeticRules.Color(prefs.bodyColor,"#e5e7eb");prefs.accentColor=NativeCosmeticRules.Color(prefs.accentColor,"#70e1d0");
            if(account.Account!=null)prefs.pendingEffects=NativePendingEffect.Remember(prefs.pendingEffects,account.Account.id,prefs.killEffect,account.Account.profile?.cosmetics?.killEffect,e=>NativeCosmeticRules.Allowed(account.Access,"effect",e));
            // Remember the actual pre-save value on the native-aware service; only an acknowledged or changed remote choice clears a pending pick.
            if(account.Account!=null)prefs.pendingIntros=NativePendingEffect.Remember(prefs.pendingIntros,account.Account.id,prefs.intro,account.Account.profileSchema>=2?account.Account.profile?.cosmetics?.intro:NativeIntroRules.ServiceIntro(prefs.intro),i=>NativeCosmeticRules.Intros.Contains(i)&&NativeCosmeticRules.Allowed(account.Access,"intro",i));
            Save();if(account.Account!=null)account.Save(Profile());
        }
        // Account: the Log in / Register / Recovery form, or the signed-in account and
        // one-time recovery code. The service is configured internally, not by players.
        private void AccountBody()
        {
            if(account.Account==null)
            {
                Fine("Save your pilot, intro text and unlocked items across computers. Guest play is still available.");
                int tab=ChoiceRow(new[]{"Log in","Register","Recovery"},accountAction=="register"?1:accountAction=="recover"?2:0);accountAction=tab==1?"register":tab==2?"recover":"login";
                Field("Username");accountUser=GUILayout.TextField(accountUser,32);Field(accountAction=="login"?"Password":"New password");accountPassword=GUILayout.PasswordField(accountPassword,'●',256);
                if(accountAction=="recover"){Field("Recovery code");accountRecovery=GUILayout.TextField(accountRecovery,128);}
                rememberAccount=GUILayout.Toggle(rememberAccount,"Remember sign-in on this Windows account");
            }
            else
            {
                // main.ts account screen: the one-time recovery code box, the status, the flags, the unlocked items and the
                // refresh note.
                if(account.RecoveryCode.Length>0)RecoveryBox(account.RecoveryCode);
                AccountLine(account.Developer?"DEV access":"Registered player",16,NativeUi.Hex(0xbfc0c9));
                var a=account.Account;var flags=(a.flags??Array.Empty<string>()).Select(NativeCosmeticRules.Label).ToArray();if(flags.Length>0)Fine(string.Join(" · ",flags));
                if(account.Developer)AccountLine("All DEV weapons and intros are available.",16,NativeUi.Hex(0xf4f4f5));
                else if(a.unlocks==null||a.unlocks.Length==0)AccountLine("No restricted items granted yet.",16,NativeUi.Hex(0xf4f4f5));
                else UnlockChips(a.unlocks.Select(NativeCosmeticRules.Label).ToArray());
                AccountLine("Items and flags refresh here. Rejoin a room after a grant changes.",16,NativeUi.Hex(0xbfc0c9));
            }
        }
        private float accountWidth=576;
        // A wrapped paragraph drawn by the kit (.panel p: line-height 1.6, 10 / 22 px margins).
        private void AccountLine(string text,float size,Color c)
        {
            var lines=NativeUi.Wrap(text,size,accountWidth-8);var r=GUILayoutUtility.GetRect(0,10+lines.Count*size*1.6f+12,GUILayout.ExpandWidth(true));if(Event.current.type==EventType.Repaint)accountWidth=r.width;
            NativeUi.Defer(()=>{for(int i=0;i<lines.Count;i++)NativeUi.Text(new Rect(r.x+4,r.y+10+i*size*1.6f,r.width-8,size*1.6f),lines[i],size,c,TextAnchor.MiddleLeft);});
        }
        // .account-unlocks: one square chip per granted item (#0c2425, 1 px #547270, 8 / 12 px padding, 16 px), wrapping.
        private void UnlockChips(string[] names)
        {
            float x=0,y=0;var spots=new System.Collections.Generic.List<Rect>();foreach(var n in names){float w=NativeUi.Measure(n,16).x+26;if(x>0&&x+w>accountWidth-8){x=0;y+=45;}spots.Add(new Rect(x,y,w,37));x+=w+8;}
            var r=GUILayoutUtility.GetRect(0,y+37+14,GUILayout.ExpandWidth(true));
            NativeUi.Defer(()=>{for(int i=0;i<names.Length;i++){var c=new Rect(r.x+4+spots[i].x,r.y+6+spots[i].y,spots[i].width,37);NativeUi.Fill(c,NativeUi.Hex(0x547270));NativeUi.Fill(new Rect(c.x+1,c.y+1,c.width-2,c.height-2),NativeUi.Hex(0x0c2425));NativeUi.Text(new Rect(c.x+13,c.y,c.width-26,c.height),names[i],16,NativeUi.Hex(0xf4f4f5),TextAnchor.MiddleLeft);}});
        }
        // The recovery code box after register / recover: #292516 with a #d7ba72 edge, square, 20 px padding; the code in
        // 15 px mono #fff1bb and a shrink-to-fit "Copy recovery code" button.
        private void RecoveryBox(string code)
        {
            float iw=accountWidth-8-42;var lead=NativeUi.Wrap("Shown once. Use it to reset your password if you forget it.",16,iw);float h=21+20+10+lead.Count*25.6f+22+18+16+48+21;
            var r=GUILayoutUtility.GetRect(0,h+8,GUILayout.ExpandWidth(true));NativeUi.Defer(()=>RecoveryBoxDraw(new Rect(r.x+4,r.y+4,r.width-8,h),code,lead));
        }
        private void RecoveryBoxDraw(Rect b,string code,System.Collections.Generic.List<string> lead)
        {
            NativeUi.Fill(b,NativeUi.Hex(0xd7ba72));NativeUi.Fill(new Rect(b.x+1,b.y+1,b.width-2,b.height-2),NativeUi.Hex(0x292516));float y=b.y+21;
            NativeUi.Text(new Rect(b.x+21,y,b.width-42,20),"Save your recovery code",16,NativeUi.Hex(0xf4f4f5),TextAnchor.MiddleLeft,bold:true);y+=30;
            foreach(var l in lead){NativeUi.Text(new Rect(b.x+21,y,b.width-42,25.6f),l,16,NativeUi.Hex(0xbfc0c9),TextAnchor.MiddleLeft);y+=25.6f;}y+=22;
            NativeUi.Text(new Rect(b.x+21,y,b.width-42,18),code,15,NativeUi.Hex(0xfff1bb),TextAnchor.MiddleLeft,mono:true);y+=34;
            float w=NativeUi.Measure("Copy recovery code",15,true).x+38;if(MenuButton(new Rect(b.x+21,y,w,48),null,"Copy recovery code",null,false,true,1,15,true)){GUIUtility.systemCopyBuffer=code;Notice("Recovery code copied. Store it somewhere safe.");}
        }
        // The signed-in account's actions (main.ts:151): Reload saved pilot | Save current pilot (primary) | DEV Menu (DEV
        // access) | Log out, in sentence case along the panel's foot as ScreenActions lays them out. The source draws no
        // icons; Unity's stay while every label fits beside one, then the row drops them together, then falls back to 13 px,
        // and a label still too long breaks onto two lines inside the 48 px button (the source's button grows instead).
        // Reload loads the account's saved pilot (ReloadSavedPilot) and says "Saved pilot loaded." or the service's error
        // when the request ends (EndFlowNotices); Reload, Save and Log out wait while a request is running.
        private static readonly string[] AccountIcons={"rotate-cw","download","cpu","log-out"};
        private static readonly string[] AccountLabels={"Reload saved pilot","Save current pilot","DEV Menu","Log out"};
        private void AccountActions(ScreenLayout l)
        {
            var p=l.Panel;bool dev=account.Developer;int n=dev?4:3;float y=p.yMax-30-48,x=p.x+32,iw=p.width-64,gap=10,w=(iw-gap*(n-1))/n,size=15;bool icons=true;
            for(int i=0;i<AccountLabels.Length;i++)if(i!=2||dev){float t=NativeUi.Measure(AccountLabels[i],15,true).x;if(19+18+t+36>w)icons=false;if(t+36>w)size=13;}
            NativeUi.Fill(new Rect(x,y-20,iw,1),NativeUi.Hex(0x4b414c,.7f*l.Enter));int hit=-1,k=0;
            for(int i=0;i<AccountLabels.Length;i++)
            {
                if(i==2&&!dev)continue;var r=new Rect(x+k*(w+gap),y,w,48);k++;string label=AccountLabels[i];bool enabled=i==2||!account.Busy;
                if(NativeUi.Measure(label,size,true).x+36<=w){if(MenuButton(r,icons?AccountIcons[i]:null,label,null,i==1,enabled,l.Enter,size,true))hit=i;continue;}
                // Two centred lines over a label-less button, lifting with its hover.
                if(MenuButton(r,null,"",null,i==1,enabled,l.Enter,size,true))hit=i;
                var lines=NativeUi.Wrap(label,size,w-36,true,2);float lift=NativeUi.HoverAmount(label.GetHashCode()^7253,r,enabled&&!MenuBlocked()),lh=size*1.25f,ty=r.center.y-lines.Count*lh/2-lift;
                var ink=NativeUi.A(i==1?NativeUi.Hex(0x111216):NativeUi.Hex(0xf5f5f7),l.Enter*(enabled?1:.45f));
                for(int j=0;j<lines.Count;j++)NativeUi.Text(new Rect(r.x+18,ty+j*lh,r.width-36,lh),lines[j],size,ink,TextAnchor.MiddleCenter,bold:true);
            }
            if(hit==0){ReloadSavedPilot();if(account.Busy)reloadPending=true;else Notice(account.Status);}
            else if(hit==1)SavePilot();else if(hit==2)Go(MenuScreen.Dev);
            else if(hit==3){account.Logout();Go(MenuScreen.Home);Notice("Logged out. Your saved pilot stays on your account.");}
        }
        private void AccountSubmit()
        {try{account.Configure(prefs.accountOrigin);Save();account.Login(accountAction,accountUser,accountPassword,accountRecovery,rememberAccount);accountNotice="";}catch(ArgumentException e){accountNotice=e.Message;}}
        // Choices per row in ChoiceGrid: three, or two while a narrow column (the lobby's room column) is drawn.
        private int choiceColumns=3;
        private string ChoiceGrid(string[] choices,string selected,string[] labels=null)
        {
            int index=0,n=Mathf.Max(1,choiceColumns);foreach(string id in choices)
            {
                if(index%n==0)GUILayout.BeginHorizontal();string text=labels!=null?labels[index]:id.Replace("-"," ");
                if(GUILayout.Toggle(id==selected,(id==selected?"✓ ":"")+text,button)&&id!=selected){selected=id;Click();}index++;if(index%n==0)GUILayout.EndHorizontal();
            }
            if(index%n!=0)GUILayout.EndHorizontal();return selected;
        }
    }
}
