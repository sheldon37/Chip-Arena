#nullable disable
using System;
using System.Linq;
using System.Security.Cryptography;
namespace ChipArena.UnityConversion
{
    public static class NativeRoomCodes
    {
        public const int Length=6;
        public static string Online(string code)=>RawOnline(code);
        public static string RawOnline(string code)
        {if(!TryNormalize(code,out var raw))throw new ArgumentException("Enter the six-character room code from your host.");return raw;}
        public static bool Valid(string code)=>code!=null&&(code.Length==Length||code.Length==8)&&code.All(c=>Alphabet.Contains(c));
        public static bool TryNormalize(string code,out string raw)
        {
            raw=null;if(code==null||code.Length>64)return false;string value=code.Trim().ToUpperInvariant();
            // Old CA- invites remain pasteable; a new code beginning CA must not lose its first two letters.
            if(value.StartsWith("CA-",StringComparison.Ordinal)){string rest=Compact(value.Substring(3));if(Valid(rest)){raw=rest;return true;}}
            value=Compact(value);if(!Valid(value))return false;raw=value;return true;
        }
        private static string Compact(string value)=>new string(value.Where(c=>c!='-'&&!char.IsWhiteSpace(c)).ToArray());
        public const string Alphabet="ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public static string Create(int length=Length){if(length!=Length&&length!=8)throw new ArgumentOutOfRangeException(nameof(length));var bytes=new byte[length];using(var random=RandomNumberGenerator.Create())random.GetBytes(bytes);var chars=new char[length];for(int i=0;i<length;i++)chars[i]=Alphabet[bytes[i]&31];return new string(chars);}
        // Reserve atomically before advertising. A collision retries without replacing the existing room.
        public static bool TryReserve(Func<string,bool> reserve,Func<string> generate=null)
        {for(int attempt=0;attempt<32;attempt++)if(reserve(generate==null?Create():generate()))return true;return false;}
        public static string Invite(string code)=>"chiparena://room?code="+RawOnline(code);
        public static bool ParseInvite(string value,out string code)
        {
            code=null;if(value==null||value.Length>128||!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="chiparena"||uri.Host!="room"||!uri.IsDefaultPort||uri.UserInfo.Length>0||uri.Fragment.Length>0||uri.AbsolutePath.Length>1||!uri.Query.StartsWith("?code=",StringComparison.Ordinal)||uri.Query.Contains("&"))return false;
            return TryNormalize(Uri.UnescapeDataString(uri.Query.Substring(6)),out code);
        }
    }
}
