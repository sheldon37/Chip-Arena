using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private float dedicatedRetryAt;private int dedicatedAttempts;
        private bool dedicated;private int dedicatedMinimum=2;private float dedicatedEmptyAt=-1;private string dedicatedPrinted="";
        private bool InitializeDedicated()
        {
            string[] args=Environment.GetCommandLineArgs();if(!args.Contains("--ca-server"))return false;dedicated=true;
            string Option(string name,string fallback){int at=Array.IndexOf(args,name);return at>=0&&at+1<args.Length?args[at+1]:fallback;}
            prefs=new ConversionPreferences{name="Dedicated server",pve=args.Contains("--ca-pve"),map=Option("--ca-map","foundry"),bots=0};if(prefs.pve)prefs.map="siren";content.Map(prefs.map);
            if(!int.TryParse(Option("--ca-bots","0"),out prefs.bots)||prefs.bots<0||prefs.bots>7)throw new ArgumentException("--ca-bots must be 0–7");if(!int.TryParse(Option("--ca-min-players",prefs.pve||prefs.bots>0?"1":"2"),out dedicatedMinimum)||dedicatedMinimum<1||dedicatedMinimum>(prefs.pve?4:8))throw new ArgumentException("Invalid --ca-min-players");
            if(!prefs.pve&&prefs.bots==0&&dedicatedMinimum<2)throw new ArgumentException("Versus needs at least two players or a bot.");
            account=gameObject.AddComponent<NativeAccount>();Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Time.fixedDeltaTime=1f/60;Time.maximumDeltaTime=.1f;previousSimulation=Physics.simulationMode;previousGravity=Physics.gravity;ownsPhysics=true;Physics.simulationMode=SimulationMode.Script;Physics.IgnoreLayerCollision(8,11,true);Physics.IgnoreLayerCollision(11,11,true);
            portText=Option("--ca-port","29941");prefs.relayOrigin=Option("--ca-relay","");listedRoom=args.Contains("--ca-public");if(prefs.relayOrigin.Length>0){internetRooms=true;HostRelay();}else HostRoom();menu=false;Debug.Log("Native dedicated authority starting. Players choose Ready; match starts when the minimum party is ready.");return true;
        }
        private void DedicatedTick()
        {
            NetworkTick();if(!Hosting&&relayConnecting==null&&Time.unscaledTime>=dedicatedRetryAt){if(!internetRooms){Debug.LogError("Dedicated LAN room could not bind. Exiting for supervisor restart.");Application.Quit(2);return;}dedicatedRetryAt=Time.unscaledTime+(float)Math.Min(30,Math.Pow(2,Math.Min(5,dedicatedAttempts++)));HostRelay();}else if(Hosting)dedicatedAttempts=0;
            if(networkStatus!=dedicatedPrinted){Debug.Log(networkStatus);dedicatedPrinted=networkStatus;}var active=peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false).ToArray();
            if(active.Length==0&&arena!=null){if(dedicatedEmptyAt<0)dedicatedEmptyAt=Time.unscaledTime;if(Time.unscaledTime-dedicatedEmptyAt>=90){arena.Dispose();arena=null;match=null;networkEpoch++;foreach(var p in peers)p.Ready=false;LobbyBroadcast();}}else dedicatedEmptyAt=-1;
            if(Hosting&&active.Length>=dedicatedMinimum&&active.All(p=>p.Ready)&&(arena==null||match.Phase==NativePhase.Match))StartMatch();
        }
    }
}
