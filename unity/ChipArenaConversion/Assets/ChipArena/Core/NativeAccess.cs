using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativePlayerClaim
    {public string kind,user,accountId,role;public bool developer;public double iat,exp;public string[] unlocks,flags;}
    public sealed class NativeAccess
    {
        public static readonly NativeAccess Guest=new NativeAccess();
        public bool Developer{get;private set;}public string AccountId{get;private set;}public double Expires{get;private set;}
        private readonly HashSet<string> grants=new HashSet<string>();
        public bool Allows(string key,double now)=>Expires>now&&(Developer||grants.Contains(key));
        public static NativeAccess ScopedWeapon(NativeAccess owner,string weapon,double now)
        {if(owner==null||!owner.Allows("weapon:"+weapon,now))return Guest;var access=new NativeAccess{Expires=owner.Expires};access.grants.Add("weapon:"+weapon);return access;}
        public static NativeAccess Verify(string token,Func<string,NativePlayerClaim> decode,double now)
        {
            try
            {
                if(token==null||token.Length>3000)return Guest;var parts=token.Split('.');if(parts.Length!=2)return Guest;
                byte[] signature=Base64(parts[1]);if(signature.Length!=64)return Guest;var p=decode(Encoding.UTF8.GetString(Base64(parts[0])));
                if(p==null||double.IsNaN(p.iat)||double.IsInfinity(p.iat)||double.IsNaN(p.exp)||double.IsInfinity(p.exp)||p.iat>now+30000||p.exp<=now||p.exp<=p.iat)return Guest;
                bool account=p.kind=="chip-arena-player";
                if(account){if(string.IsNullOrEmpty(p.accountId)||p.accountId.Length>64||p.user==null||p.exp-p.iat>900000)return Guest;}
                else if(token.Length>1500||p.user!="Sheldon"||p.role!="dev"||p.exp>now+8*3600000)return Guest;
                using(var key=ECDsa.Create(new ECParameters{Curve=ECCurve.NamedCurves.nistP256,Q=new ECPoint{X=Base64("8phe-ghSPLnonQXfeMgAtMeLKCzFEzQNGTt27gFc1eY"),Y=Base64("f6sN-RmLQtVJUGkovXUbb3SdjSb7D6YVJdmyagmNxmM")}}))
                    if(!key.VerifyData(Encoding.UTF8.GetBytes(parts[0]),signature,HashAlgorithmName.SHA256))return Guest;
                var access=new NativeAccess{Developer=!account||p.developer,AccountId=p.accountId,Expires=p.exp};if(account&&p.unlocks!=null)foreach(var grant in p.unlocks)if(grant!=null)access.grants.Add(grant);return access;
            }
            catch(ArgumentException){return Guest;}catch(CryptographicException){return Guest;}catch(FormatException){return Guest;}catch(NotSupportedException){return Guest;}
        }
        private static byte[] Base64(string s){s=s.Replace('-','+').Replace('_','/');return Convert.FromBase64String(s.PadRight((s.Length+3)/4*4,'='));}
    }
}
