using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeDirectoryEntry { public string code,origin; public long expires; }
    public sealed class NativeOnlineRoomException:IOException { public NativeOnlineRoomException(string message):base(message){} public NativeOnlineRoomException(string message,Exception inner):base(message,inner){} }

    // Reuse the existing eight-character directory without changing the Electron service or its records.
    // UN is an internal namespace; players see and enter only the six random characters.
    public static class NativeRoomDirectory
    {
        public const string Origin="https://chip-arena-prototype.sheldon37.chatgpt.site";
        public static string Key(string code)
        {
            code=NativeRoomCodes.RawOnline(code);
            if(code.Length!=6)throw new NativeOnlineRoomException("This online room needs its six-character Unity code. Ask the host to update and create a new room.");
            return "UN"+code;
        }
        public static string TunnelOrigin(string value)
        {
            if(value==null||value.Length>200||!Uri.TryCreate(value,UriKind.Absolute,out var uri)||
                uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo.Length!=0||uri.Query.Length!=0||uri.Fragment.Length!=0||
                uri.AbsolutePath!="/"||!Regex.IsMatch(uri.Host,@"\A[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.trycloudflare\.com\z"))
                throw new NativeOnlineRoomException("The host's online address is invalid. Ask them to create a new room.");
            return uri.GetLeftPart(UriPartial.Authority);
        }
        public static HttpClient Client()=>new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan,MaxResponseContentBufferSize=4096};
        public static async Task<string> Lookup(HttpClient http,string code,Func<string,NativeDirectoryEntry> decode,CancellationToken token)
        {
            string key=Key(code);
            using(var request=new HttpRequestMessage(HttpMethod.Get,Origin+"/api/directory/"+key))
            {
                request.Headers.CacheControl=new CacheControlHeaderValue{NoCache=true,NoStore=true};
                var entry=await Exchange(http,request,decode,token).ConfigureAwait(false);
                Validate(entry,key);return TunnelOrigin(entry.origin);
            }
        }
        public static async Task<bool> Register(HttpClient http,string code,string origin,string secret,Func<string,NativeDirectoryEntry> decode,CancellationToken token)
        {
            string key=Key(code);origin=TunnelOrigin(origin);
            if(secret==null||!Regex.IsMatch(secret,@"\A[a-f0-9]{64}\z"))throw new ArgumentException("Invalid directory lease secret.");
            using(var request=new HttpRequestMessage(HttpMethod.Post,Origin+"/api/directory/"+key))
            {
                request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",secret);
                request.Content=new StringContent("{\"origin\":\""+origin+"\"}",Encoding.UTF8,"application/json");
                var entry=await Exchange(http,request,decode,token,true).ConfigureAwait(false);
                if(entry==null)return false;Validate(entry,key);return true;
            }
        }
        private static void Validate(NativeDirectoryEntry entry,string key)
        {if(entry==null||entry.code!=key||entry.expires<=0)throw new NativeOnlineRoomException("Room lookup returned an invalid response. Please try again.");}
        private static async Task<NativeDirectoryEntry> Exchange(HttpClient http,HttpRequestMessage request,Func<string,NativeDirectoryEntry> decode,CancellationToken token,bool registering=false)
        {
            token.ThrowIfCancellationRequested();
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(8000);
                try
                {
                    using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false))
                    {
                        if(registering&&response.StatusCode==HttpStatusCode.Conflict)return null;
                        if(response.StatusCode==HttpStatusCode.NotFound)throw new NativeOnlineRoomException("Room not found. Check the code and ask the host to keep their room open.");
                        if(!response.IsSuccessStatusCode)throw new NativeOnlineRoomException("Room lookup is temporarily unavailable. Please try again.");
                        using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            byte[] bytes=new byte[4097];int total=0,n;
                            while((n=await stream.ReadAsync(bytes,total,bytes.Length-total,timeout.Token).ConfigureAwait(false))>0)
                            {total+=n;if(total>4096)throw new NativeOnlineRoomException("Room lookup returned an invalid response. Please try again.");}
                            try{return decode(new UTF8Encoding(false,true).GetString(bytes,0,total));}
                            catch(Exception e)when(!(e is OperationCanceledException)){throw new NativeOnlineRoomException("Room lookup returned an invalid response. Please try again.");}
                        }
                    }
                }
                catch(OperationCanceledException)when(!token.IsCancellationRequested){throw new NativeOnlineRoomException("Room lookup timed out. Check your internet connection and try again.");}
                catch(HttpRequestException){throw new NativeOnlineRoomException("Room lookup could not connect. Check your internet connection and try again.");}
            }
        }
    }
}
