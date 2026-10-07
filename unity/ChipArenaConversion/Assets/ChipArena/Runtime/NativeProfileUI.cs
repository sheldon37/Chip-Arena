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
        private NativeAccount account;private bool showProfile,showAccount,rememberAccount=true;private string accountUser="",accountPassword="",accountRecovery="",accountAction="login",accountNotice="";
        private bool AllowedWeapon(ContentWeapon w)=>!w.devOnly||(account?.Access??NativeAccess.Guest).Allows("weapon:"+w.id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        private void InitializeAccount()
        {
            account=gameObject.AddComponent<NativeAccount>();account.Accepted=data=>{var p=data.profile;if(p==null)return;prefs.name=NativeCosmeticRules.Name(p.name);prefs.accentColor=NativeCosmeticRules.Color(p.color,"#70e1d0");var c=p.cosmetics??new NativeCosmetics();prefs.bodyColor=NativeCosmeticRules.Color(c.bodyColor,"#e5e7eb");prefs.hat=NativeCosmeticRules.Hat(c.hat,account.Access);prefs.killEffect=NativeCosmeticRules.Kill(c.killEffect,account.Access);prefs.intro=NativeCosmeticRules.Intro(c.intro,account.Access);prefs.introText=c.introText;prefs.overrideIntro=c.overrideWeaponIntro;if(content.weapons.Any(w=>w.id==c.preferredSpec&&AllowedWeapon(w)))prefs.weapon=c.preferredSpec;accountPassword=accountRecovery="";Save();};
            try{account.Configure(prefs.accountOrigin,true);}catch(ArgumentException){prefs.accountOrigin="";}
        }
        private void ProfilePanel()
        {
            if(GUILayout.Button(showProfile?"Close appearance":"Appearance and entrances",button))showProfile=!showProfile;
            if(showProfile)
            {
                GUILayout.BeginHorizontal();GUILayout.Label("Body colour",small);prefs.bodyColor=GUILayout.TextField(prefs.bodyColor,7);GUILayout.Label("Accent colour",small);prefs.accentColor=GUILayout.TextField(prefs.accentColor,7);GUILayout.EndHorizontal();
                GUILayout.Label("Hat",label);prefs.hat=ChoiceGrid(NativeCosmeticRules.Hats.Where(id=>NativeCosmeticRules.Allowed(account.Access,"hat",id)).ToArray(),prefs.hat);
                GUILayout.Label("Elimination effect",label);prefs.killEffect=ChoiceGrid(NativeCosmeticRules.Kills.Where(id=>NativeCosmeticRules.Allowed(account.Access,"effect",id)).ToArray(),prefs.killEffect);
                prefs.overrideIntro=GUILayout.Toggle(prefs.overrideIntro,"Use a custom entrance instead of this weapon’s signature");
                if(prefs.overrideIntro)prefs.intro=ChoiceGrid(NativeCosmeticRules.Intros.Where(id=>NativeCosmeticRules.Allowed(account.Access,"intro",id)).ToArray(),prefs.intro);
                GUILayout.Label("Entrance message",small);prefs.introText=GUILayout.TextField(prefs.introText??"Ready to make an impact",64);
                if(GUILayout.Button("Save appearance",button)){prefs.bodyColor=NativeCosmeticRules.Color(prefs.bodyColor,"#e5e7eb");prefs.accentColor=NativeCosmeticRules.Color(prefs.accentColor,"#70e1d0");Save();if(account.Account!=null)account.Save(Profile());}
            }
            if(GUILayout.Button(showAccount?"Close account":"Account · "+account.Status,button))showAccount=!showAccount;
            if(showAccount)
            {
                GUILayout.Label("Account service URL",small);prefs.accountOrigin=GUILayout.TextField(prefs.accountOrigin??"",240);GUILayout.Label(account.Status+" "+accountNotice,small);
                if(account.Account==null)
                {
                    accountAction=ChoiceGrid(new[]{"login","register","recover"},accountAction);GUILayout.Label("Username",small);accountUser=GUILayout.TextField(accountUser,32);GUILayout.Label("Password",small);accountPassword=GUILayout.PasswordField(accountPassword,'●',256);
                    if(accountAction=="recover"){GUILayout.Label("Recovery code",small);accountRecovery=GUILayout.TextField(accountRecovery,128);}rememberAccount=GUILayout.Toggle(rememberAccount,"Remember sign-in on this Windows account");
                    GUI.enabled=!account.Busy;if(GUILayout.Button(accountAction=="register"?"Create account":accountAction=="recover"?"Recover account":"Sign in",button))try{account.Configure(prefs.accountOrigin);Save();account.Login(accountAction,accountUser,accountPassword,accountRecovery,rememberAccount);accountNotice="";}catch(ArgumentException e){accountNotice=e.Message;}GUI.enabled=true;
                }
                else
                {
                    if(account.RecoveryCode.Length>0){GUILayout.Label("Save this recovery code somewhere private:",label);GUILayout.TextField(account.RecoveryCode);}
                    GUI.enabled=!account.Busy;if(GUILayout.Button("Save current profile to account",button))account.Save(Profile());if(GUILayout.Button("Sign out",button))account.Logout();GUI.enabled=true;
                }
            }
        }
        private string ChoiceGrid(string[] choices,string selected)
        {int index=0;foreach(string id in choices){if(index%3==0)GUILayout.BeginHorizontal();if(GUILayout.Button((id==selected?"✓ ":"")+id.Replace("-"," "),button))selected=id;index++;if(index%3==0)GUILayout.EndHorizontal();}if(index%3!=0)GUILayout.EndHorizontal();return selected;}
    }
}
