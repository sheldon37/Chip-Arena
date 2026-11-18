using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The cradle card: a pod run's loading screen drawn as the inside of my pod (docs/EXPEDITION-OVERHAUL.md R8 and W3: "the loading
    /// screen is the inside of my pod"): a porthole with cloud streaks and lightning, the run's map in a night palette (each district
    /// breathing, each pod's streak drawn to its landing spot), an altimeter falling from 12,000 to 600 m with the load, a lamp per
    /// party member (READY, LOADING 64% or LOST), a warning lamp per active mutator and the station's last line, PODS AWAY · GOOD
    /// HUNTING. Engine-free: the card's rows come from the party loading model (NativePartyLoadModel, which host and guests build
    /// from the same view) or, at the station's fade before the load starts, from the station's party. Runtime/NativeCradleHud.cs
    /// draws it; Runtime/NativeStageLoadingUI.cs shows it for a pod run's first load and holds it until the hub's countdown.
    /// Presentation only: nothing here decides, sends or writes anything. No string is built per frame once a value has been seen.
    /// </summary>
    public sealed class NativeCradleCard
    {
        public const int Max=8;
        /// <summary>A party member's lamp: loading (with its share when known), ready (in its pod), lost (its link dropped) or away
        /// (extracted).</summary>
        public enum Lamp{Loading,Ready,Lost,Away}
        public readonly string[] Names=new string[Max];public readonly Lamp[] Lamps=new Lamp[Max];public readonly float[] Progress=new float[Max];
        public readonly bool[] You=new bool[Max];
        /// <summary>Each member's landing spot (the blueprint's metres, x east, z north; NaN unknown) for its streak on the map.</summary>
        public readonly Vector2[] Spots=new Vector2[Max];
        public int Count;
        /// <summary>This client's load (0..1): the altimeter's fall and the bar.</summary>
        public float Load;
        /// <summary>The hold line under the lamps (the roster's footer, or null).</summary>
        public string Hold;
        public int Ready=>Tally(Lamp.Ready);
        public void Clear(){Count=0;Load=0;Hold=null;for(int i=0;i<Max;i++){Names[i]=null;You[i]=false;Progress[i]=-1;Spots[i]=new Vector2(float.NaN,float.NaN);}}
        public void Add(string name,Lamp lamp,float progress,bool you)
        {
            if(Count>=Max)return;Names[Count]=name;Lamps[Count]=lamp;Progress[Count]=progress;You[Count]=you;Spots[Count]=new Vector2(float.NaN,float.NaN);Count++;
        }
        private int Tally(Lamp l){int n=0;for(int i=0;i<Count;i++)if(Lamps[i]==l)n++;return n;}
        public static Lamp LampOf(PartyLoadState s)=>s==PartyLoadState.Ready||s==PartyLoadState.InWorld?Lamp.Ready:s==PartyLoadState.Dropped?Lamp.Lost:s==PartyLoadState.Extracted?Lamp.Away:Lamp.Loading;
        /// <summary>The card from the party loading model (its rows in order: me first) and this client's load.</summary>
        public void FromRoster(NativePartyLoadModel roster,float load)
        {
            Clear();Load=Clamp01(load);if(roster==null)return;
            for(int i=0;i<roster.Count&&i<Max;i++){var r=roster.Rows[i];Add(r.Name,LampOf(r.State),r.Progress,r.You);}
            Hold=roster.Footer;
        }
        /// <summary>The card at the station's fade, before the load starts: the station's party (`names`, `count`), all loading, me
        /// (`mine`) marked.</summary>
        public void FromParty(string[] names,int count,int mine)
        {
            Clear();if(names==null)return;for(int i=0;i<count&&i<names.Length&&i<Max;i++)Add(names[i]??"",Lamp.Loading,i==mine?0:-1,i==mine);
        }

        // ---- the words (cached) ----
        public const string Eyebrow="DROP POD · CABIN",LastLine="PODS AWAY · GOOD HUNTING",SquadDrop="SQUAD DROP",Hazards="MUTATORS",NoHazards="NO MUTATORS",Altitude="ALT";
        private static readonly string[] loading=MakeLoading();
        private static string[] MakeLoading(){var s=new string[101];for(int i=0;i<=100;i++)s[i]="LOADING "+i+"%";return s;}
        public const string LoadingWord="LOADING",ReadyWord="READY",LostWord="LOST",AwayWord="AWAY";
        /// <summary>The hold line while `n` lamps still load, and once every lamp is in (the party panel's own words).</summary>
        public static string WaitingFor(int n)=>n>0?NativePartyLoadModel.WaitingFor(n):NativePartyLoadModel.Everyone;
        /// <summary>A lamp's label: READY, LOADING n% (LOADING when its share is unknown), LOST or AWAY.</summary>
        public static string Label(Lamp lamp,float progress)
        {
            switch(lamp)
            {
                case Lamp.Ready:return ReadyWord;case Lamp.Lost:return LostWord;case Lamp.Away:return AwayWord;
                default:return progress>=0&&progress<=1?loading[Math.Min(100,(int)Math.Floor(progress*100+1e-4))]:LoadingWord;
            }
        }
        /// <summary>The altimeter: 12,000 m at the load's start falling to 600 m when it is done.</summary>
        public const float AltitudeTop=12000,AltitudeBottom=600;
        public static float AltitudeAt(float load)=>AltitudeTop-(AltitudeTop-AltitudeBottom)*Clamp01(load);
        private static readonly string[] altitudes=new string[(int)((AltitudeTop-AltitudeBottom)/10)+1];
        /// <summary>The altimeter's reading in tens of metres ("11,240 M"), each built once.</summary>
        public static string AltitudeLabel(float load)
        {
            int k=(int)Math.Round((AltitudeAt(load)-AltitudeBottom)/10);k=Math.Max(0,Math.Min(altitudes.Length-1,k));
            return altitudes[k]??=((int)AltitudeBottom+k*10).ToString("N0",System.Globalization.CultureInfo.InvariantCulture)+" M";
        }

        // ---- the porthole and the map ----
        /// <summary>The porthole's lightning: a flash's brightness (0..1) at `now` (a strike now and then, a flicker, decay).</summary>
        public static float Lightning(float now,bool reduced)
        {
            if(reduced)return 0;double slot=Math.Floor(now/1.7);float age=(float)(now-slot*1.7);if(Hash((int)slot,7)<.55f)return 0;
            float at=.2f+Hash((int)slot,9)*1.1f,a=age-at;if(a<0||a>.42f)return 0;return a<.05f?1:a<.09f?.25f:a<.13f?.8f:Math.Max(0,1-(a-.13f)/.29f)*.7f;
        }
        /// <summary>Cloud streak `k` (of `count`) in the porthole at `now`: its x (-1..1 across), its head's height (-1 bottom .. 1 top,
        /// rising: the pod falls through the cloud), its length and opacity.</summary>
        public static void Streak(int k,int count,float now,bool reduced,out float x,out float y,out float length,out float opacity)
        {
            float speed=.55f+1.4f*Hash(k,1),phase=Hash(k,2);x=Hash(k,3)*2-1;length=.18f+.5f*Hash(k,4);opacity=.18f+.5f*Hash(k,5);
            float u=reduced?phase:((now*speed*.6f+phase)%1+1)%1;y=-1.3f+2.6f*u;
        }
        /// <summary>Where a member's pod streak comes from on the map (a unit direction in the blueprint's frame), seat by seat: each
        /// on its own bearing (the drop's own approaches are the pods' presentation, NativePodDrop: this is the card's sketch).</summary>
        public static Vector2 StreakFrom(int seat){double a=.5+seat*2.2+Hash(seat,11)*.7;return new Vector2((float)Math.Sin(a),(float)Math.Cos(a));}
        /// <summary>A district's breath (0..1) at `now`, each on its own phase.</summary>
        public static float Breath(int district,float now,bool reduced)=>reduced?.6f:.5f+.5f*(float)Math.Sin(now*1.9f+district*2.1f);
        /// <summary>
        /// The holo map's pixel (NativeDeployPresentation.Preview, ARGB) in the cabin's night palette: blocks into deep navy, the
        /// lit roads into sodium amber, the entrances' rings keep their colour, the plaza's ring and the boss gate stay bright.
        /// </summary>
        public static int Night(int argb)
        {
            float r=((argb>>16)&255)/255f,g=((argb>>8)&255)/255f,b=(argb&255)/255f;float max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),l=.3f*r+.59f*g+.11f*b,sat=max>1e-4f?(max-min)/max:0;
            float nr,ng,nb;
            if(l>.42f&&sat<.25f){float k=Clamp01((l-.42f)/.4f);nr=.55f+.4f*k;ng=.34f+.3f*k;nb=.1f+.12f*k;}
            else if(max>.55f){nr=r*.95f;ng=g*.95f;nb=b*.95f;}
            else{nr=.02f+r*.28f;ng=.035f+g*.34f;nb=.07f+b*.55f;}
            int R=(int)(Clamp01(nr)*255),G=(int)(Clamp01(ng)*255),B=(int)(Clamp01(nb)*255);return unchecked((int)0xff000000)|(R<<16)|(G<<8)|B;
        }
        /// <summary>The night map (row 0 the north edge) for a state: Preview recoloured by Night.</summary>
        public static int[] NightMap(ExpeditionDeploymentState s,int width,int height)
        {
            var px=NativeDeployPresentation.Preview(s,width,height);for(int i=0;i<px.Length;i++)px[i]=Night(px[i]);return px;
        }
        /// <summary>A blueprint point (metres) on the map's 0..1 square (u east, v south: the texture drawn top down).</summary>
        public static Vector2 MapUv(float wx,float wz)=>new Vector2((wx+NativeDeployPresentation.WorldHalfX)/(2*NativeDeployPresentation.WorldHalfX),(NativeDeployPresentation.WorldHalfZ-wz)/(2*NativeDeployPresentation.WorldHalfZ));

        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Hash(int a,int b){unchecked{uint h=(uint)(a*374761393)^(uint)(b*668265263);h=(h^(h>>13))*1274126177u;h^=h>>16;return (h&0xffffff)/(float)0x1000000;}}
    }
}
