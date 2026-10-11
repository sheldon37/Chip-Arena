using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeOnlineConfiguration {public int schema,protocol;public string relayOrigin;}
    public static class NativeOnlineService
    {
        // Owner-operated configuration in the existing test Update channel. Contains no account credentials.
        public const string ConfigurationUrl="https://raw.githubusercontent.com/sheldon37/chip-arena-test-updates/main/native/online.json";
        public const string Unavailable="Online rooms are unavailable. Please try again shortly. Local-network rooms still work.";
        private static readonly HttpClient Client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan,MaxResponseContentBufferSize=4096};
        public static Task<string> Resolve(Func<string,NativeOnlineConfiguration> decode,CancellationToken token)=>Resolve(Client,decode,token);
        public static async Task<string> Resolve(HttpClient client,Func<string,NativeOnlineConfiguration> decode,CancellationToken token)
        {
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(8000);
                using(var request=new HttpRequestMessage(HttpMethod.Get,ConfigurationUrl))
                {
                    request.Headers.CacheControl=new System.Net.Http.Headers.CacheControlHeaderValue{NoCache=true};
                    using(var response=await client.SendAsync(request,timeout.Token).ConfigureAwait(false))
                    {
                        if(!response.IsSuccessStatusCode)throw new InvalidOperationException(Unavailable);
                        string json=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if(json.Length>4096)throw new InvalidOperationException(Unavailable);
                        return Validate(decode(json));
                    }
                }
            }
        }
        public static string Validate(NativeOnlineConfiguration config)
        {
            if(config==null||config.schema!=1||config.protocol!=NativeConnection.Protocol||config.relayOrigin==null||config.relayOrigin.Length>256)throw new InvalidOperationException(Unavailable);
            var uri=NativeWebSocketStream.Endpoint(config.relayOrigin,"");
            // Public service discovery must never direct players to an insecure or loopback endpoint.
            if(uri.Scheme!="wss"||uri.IsLoopback)throw new InvalidOperationException(Unavailable);
            return uri.GetLeftPart(UriPartial.Authority);
        }
    }
}
