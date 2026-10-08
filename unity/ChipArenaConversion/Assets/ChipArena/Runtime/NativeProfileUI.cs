using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public static class NativeCosmeticRules
    {
        public static readonly string[] Hats={"none","cap","beanie","top","wizard","crown","cone","antenna","halo","cowboy","auditor-halo"};
        public static readonly string[] Kills={"impact-ragdoll","ragdoll","ragdoll-ember","ragdoll-frost","ragdoll-static","ragdoll-gravity","ragdoll-pinball","ragdoll-fireworks","ragdoll-soul","ragdoll-petals","voxel-burst","confetti","ascension","shrink","launch","ghost","dev-gibber","dev-singularity","dev-raging-demon","dev-clown","dev-clown-love","dev-bass-van"};
        public static readonly string[] Intros={"neon-drop","vector-slide","signal-spin","arc-salute","pulse-wave","weapon-sword","weapon-daggers","weapon-shield","weapon-greatsword","weapon-pistol","weapon-rifle","weapon-machinegun","weapon-fire","weapon-water","weapon-lightning","weapon-darkness","dev-emperor","dev-ansatsuken","dev-matrix","dev-siren"};
        // Display names from the source (shared/cosmetics.ts HATS / KILL_STYLES with ragdoll-styles.ts, shared/intros.ts with
        // weapon-intros.ts, shared/access.ts GRANTABLE_CONTENT / ACCOUNT_FLAGS); ids stay the saved and sent values.
        private static readonly System.Collections.Generic.Dictionary<string,string> Labels=new System.Collections.Generic.Dictionary<string,string>
        {
            {"auditor-halo","Auditor Halo · DEV ONLY"},{"none","No hat"},{"cap","Ball cap"},{"beanie","Beanie"},{"top","Top hat"},{"wizard","Wizard hat"},{"crown","Crown"},{"cone","Traffic cone"},{"antenna","Antenna"},{"halo","Halo"},{"cowboy","Cowboy hat"},
            {"impact-ragdoll","Impact ragdoll & gibs"},{"dev-gibber","Gibber · DEV ONLY"},{"ragdoll","Ragdoll"},{"ragdoll-ember","Emberfall"},{"ragdoll-frost","Frostbreak"},{"ragdoll-static","Static Aftershock"},{"ragdoll-gravity","Gravity Collapse"},{"ragdoll-pinball","Pinball"},{"ragdoll-fireworks","Firework Finale"},{"ragdoll-soul","Soul Drift"},{"ragdoll-petals","Petal Scatter"},{"dev-singularity","Singularity Override · DEV ONLY"},
            {"dev-raging-demon","Raging Demon · DEV ONLY"},{"voxel-burst","Voxel breakup"},{"confetti","Confetti cannon"},{"ascension","Beam me up"},{"shrink","Pocket-sized"},{"launch","Team Rocket"},{"ghost","Ghost escape"},{"dev-clown","Clown getaway · DEV ONLY"},{"dev-clown-love","Clown Love · DEV ONLY"},{"dev-bass-van","Bass Van · DEV ONLY"},
            {"dev-siren","DEV · Black Siren Protocol"},{"dev-emperor","DEV · Emperor’s Oath"},{"dev-ansatsuken","DEV · Rain Warrior"},{"dev-matrix","DEV · L33T Throne"},
            {"weapon-sword","Flash Draw"},{"weapon-daggers","Twin Phantom"},{"weapon-greatsword","Colossus Oath"},{"weapon-shield","Bastion Drop"},{"weapon-pistol","Deadeye"},{"weapon-rifle","Longwatch"},{"weapon-machinegun","Belt Rattle"},{"weapon-fire","Cinder Crown"},{"weapon-water","Tidal Serpent"},{"weapon-lightning","Thunderstep"},{"weapon-darkness","Eventide Gate"},
            {"neon-drop","Weapon Signature"},{"vector-slide","Vector Slide"},{"signal-spin","Signal Spin"},{"arc-salute","Arc Salute"},{"pulse-wave","Pulse Wave"},
            {"weapon:siren","Siren DEV boss"},{"intro:dev-siren","Siren protocol intro"},{"weapon:emperor","Emperor Style + Mishima Chips"},{"intro:dev-emperor","Emperor storm intro"},{"weapon:leet","L33T H4x staff + L44T Chips"},{"weapon:ansatsuken","Ansatsuken + arcade Chips"},
            {"intro:dev-matrix","Matrix throne intro"},{"intro:dev-ansatsuken","Rain warrior intro"},{"hat:auditor-halo","Auditor Halo"},{"effect:dev-singularity","Singularity Override"},{"effect:dev-raging-demon","Raging Demon"},{"effect:dev-clown","Clown getaway"},{"effect:dev-clown-love","Clown Love"},{"effect:dev-bass-van","Bass Van"},
            {"playtester","Playtester"},{"vip","VIP"},
        };
        public static string Label(string id)=>id!=null&&Labels.TryGetValue(id,out var name)?name:(id??"").Replace("-"," ");
        public static string[] LabelsOf(string[] ids)=>ids.Select(Label).ToArray();
        public static string Name(string name){name=Regex.Replace(name??"Pilot","[<>\\p{Cc}\\p{Cf}]"," ").Trim();return name.Length==0?"Pilot":name.Substring(0,Math.Min(20,name.Length));}
        public static string IntroText(string value){value=Regex.Replace(value??"","[<>\\p{Cc}\\p{Cf}]"," ").Trim();return value.Substring(0,Math.Min(64,value.Length));}
        public static string Color(string value,string fallback)=>value!=null&&Regex.IsMatch(value,"^#[0-9a-fA-F]{6}$")?value:fallback;
        public static bool Allowed(NativeAccess a,string type,string id)=>id!=null&&(id!="dev-gibber"||(a??NativeAccess.Guest).Developer)&&( !id.StartsWith("dev-")&&id!="auditor-halo"||(a??NativeAccess.Guest).Allows(type+":"+id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        public static string Hat(string value,NativeAccess a)=>Hats.Contains(value)&&Allowed(a,"hat",value)?value:"none";
        public static string Kill(string value,NativeAccess a)=>Kills.Contains(value)&&Allowed(a,"effect",value)?value:"impact-ragdoll";
        public static string Intro(string value,NativeAccess a)=>Intros.Contains(value)&&Allowed(a,"intro",value)?value:"neon-drop";
    }
    public sealed partial class ConversionGame
    {
        private NativeAccount account;private bool rememberAccount=true;private string accountUser="",accountPassword="",accountRecovery="",accountAction="login",accountNotice="";
        private bool AllowedWeapon(ContentWeapon w)=>!w.devOnly||(account?.Access??NativeAccess.Guest).Allows("weapon:"+w.id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        private void InitializeAccount()
        {
            account=gameObject.AddComponent<NativeAccount>();account.Accepted=(data,load)=>{if(arena?.Local!=null)arena.Local.Seat.access=account.Access;if(server!=null)SendServer(new NativeNetMessage{type="auth-refresh",token=account.Token});if(!load)return;var p=data.profile;if(p==null)return;prefs.name=NativeCosmeticRules.Name(p.name);prefs.accentColor=NativeCosmeticRules.Color(p.color,"#70e1d0");var c=p.cosmetics??new NativeCosmetics();prefs.bodyColor=NativeCosmeticRules.Color(c.bodyColor,"#e5e7eb");prefs.hat=NativeCosmeticRules.Hat(c.hat,account.Access);prefs.killEffect=NativeCosmeticRules.Kill(c.killEffect,account.Access);prefs.intro=NativeCosmeticRules.Intro(c.intro,account.Access);prefs.introText=c.introText;prefs.overrideIntro=c.overrideWeaponIntro;if(content.weapons.Any(w=>w.id==c.preferredSpec&&AllowedWeapon(w)))prefs.weapon=c.preferredSpec;accountPassword=accountRecovery="";Save();};
            try{account.Configure(prefs.accountOrigin,true);}catch(ArgumentException){prefs.accountOrigin="";}
        }
        // Customize (main.ts customize screen): name, preferred weapon, colours, headwear, kill effect, intro and its text.
        private void CustomizeBody()
        {
            Field("Player name");prefs.name=GUILayout.TextField(prefs.name??"",20);
            Field("Preferred weapon");WeaponGrid();Fine("Your preferred weapon is your pilot's default and is sent to rooms you join.");
            Field("Colours");GUILayout.BeginHorizontal();ColourField("Body",ref prefs.bodyColor,"#e5e7eb");GUILayout.Space(12);ColourField("Accent",ref prefs.accentColor,"#70e1d0");GUILayout.EndHorizontal();
            var hats=NativeCosmeticRules.Hats.Where(id=>NativeCosmeticRules.Allowed(account.Access,"hat",id)).ToArray();var kills=NativeCosmeticRules.Kills.Where(id=>NativeCosmeticRules.Allowed(account.Access,"effect",id)).ToArray();
            Field("Headwear");prefs.hat=ChoiceGrid(hats,prefs.hat,NativeCosmeticRules.LabelsOf(hats));
            Field("Kill effect");prefs.killEffect=ChoiceGrid(kills,prefs.killEffect,NativeCosmeticRules.LabelsOf(kills));
            Field("Match intro");prefs.overrideIntro=GUILayout.Toggle(prefs.overrideIntro,"Use a custom entrance instead of this weapon’s signature");
            if(prefs.overrideIntro){var intros=NativeCosmeticRules.Intros.Where(id=>NativeCosmeticRules.Allowed(account.Access,"intro",id)).ToArray();prefs.intro=ChoiceGrid(intros,prefs.intro,NativeCosmeticRules.LabelsOf(intros));}
            Field("Intro text");prefs.introText=GUILayout.TextField(prefs.introText??"Ready to make an impact",64);Fine("Shown under your name while your entrance plays. Preview Intro plays it on its own.");
        }
        // A #rrggbb field with a live swatch of the colour it will save as, and a row of preset swatches standing in for the
        // source's colour picker (input type=color): clicking one sets the field.
        private static readonly string[] BodyPresets={"#e5e7eb","#9ca3af","#4b5563","#1f2937","#fca5a5","#fdba74","#fde68a","#86efac","#93c5fd","#c4b5fd","#f9a8d4","#f4f4f5"};
        private static readonly string[] AccentPresets={"#70e1d0","#ff6b35","#ffd27a","#7ed78a","#69a6ff","#ba94f5","#ff6575","#65dce0","#f18dcc","#ffa565","#b8aabb","#ffffff"};
        private void ColourField(string name,ref string value,string fallback)
        {
            GUILayout.BeginVertical();GUILayout.Label(name,small);GUILayout.BeginHorizontal();var swatch=GUILayoutUtility.GetRect(30,30,GUILayout.Width(30),GUILayout.Height(30));
            if(Event.current.type==EventType.Repaint){var c=GUI.color;GUI.color=HexColor(NativeCosmeticRules.Color(value,fallback),0xffffff);GUI.DrawTexture(swatch,Texture2D.whiteTexture);GUI.color=c;}
            value=GUILayout.TextField(value??fallback,7);GUILayout.EndHorizontal();
            var row=GUILayoutUtility.GetRect(0,62,GUILayout.ExpandWidth(true));var presets=fallback=="#e5e7eb"?BodyPresets:AccentPresets;float size=Mathf.Min(22,(row.width-5*6)/6);
            for(int i=0;i<presets.Length;i++)
            {
                var r=new Rect(row.x+2+(i%6)*(size+6),row.y+6+(i/6)*(size+6),size,size);bool on=string.Equals(value,presets[i],StringComparison.OrdinalIgnoreCase);float h=NativeUi.HoverAmount(name.GetHashCode()^i*977,r);
                NativeUi.Round(new Rect(r.x-2*h,r.y-2*h,r.width+4*h,r.height+4*h),HexColor(presets[i],0xffffff),5,on?NativeUi.Hex(0xff6b35):NativeUi.Mix(new Color(1,1,1,.18f),NativeUi.Hex(0xff784b),h),on?2:1);
                if(NativeUi.Button(r)){value=presets[i];Click();}
            }
            GUILayout.EndVertical();
        }
        private void SavePilot()
        {
            prefs.name=NativeCosmeticRules.Name(prefs.name);prefs.introText=NativeCosmeticRules.IntroText(prefs.introText);prefs.bodyColor=NativeCosmeticRules.Color(prefs.bodyColor,"#e5e7eb");prefs.accentColor=NativeCosmeticRules.Color(prefs.accentColor,"#70e1d0");
            Save();if(account.Account!=null)account.Save(Profile());
        }
        // Account (main.ts login / account screens): the Log in / Register / Recovery form, or the signed-in account with its
        // one-time recovery code; the service URL stays editable at the foot. Submit, save, DEV Menu and Log out are actions.
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
                Fine(account.Developer?"DEV access":"Registered player");
                if(account.RecoveryCode.Length>0){Field("Recovery code");GUILayout.Label("Save this recovery code somewhere private. It is shown once.",label);GUILayout.TextField(account.RecoveryCode);}
                Fine("Items and flags refresh here when the account service grants them. Save Current Pilot uploads your Customize choices.");
            }
            Field("Account service URL");prefs.accountOrigin=GUILayout.TextField(prefs.accountOrigin??"",240);
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
