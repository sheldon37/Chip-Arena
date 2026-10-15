using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expeditions screen's rules and layout (owner 2026-10-15: "Redesign the "Start Expedition" Screen ... Lets also make
    /// this Multiplayer capable ... a "Play Wave Defense" button for individual waves ... We will show players / bots in the
    /// lobby all in the lobby, in the top. We have a "Select Expedition" screen"). Engine-free, so the managed checks hold the
    /// mode and wave mapping, the launch gates and the fit at every canvas. Drawn by Runtime/NativeExpeditionsMenu.cs, which is
    /// presentation only: every launch goes through the calls the old boss-test screen and room lobby made.
    /// <list type="bullet">
    /// <item>START EXPEDITION is the shared run for 1–4 signed-in human players. Runtime validates identities and client support.</item>
    /// <item>PLAY WAVE DEFENSE is the boss-test match (Ward Zero's three waves, then the Siren), already co-op through the room.
    /// The room carries only <c>skipWaves</c>: Wave 1 is a normal start and the Siren is skipWaves. Waves 2 and 3 are shown and
    /// locked until rooms carry a start wave (Astra).</item>
    /// </list>
    /// Lengths are design units: the canvas (NativeUiMotion.Measure) divided by <see cref="Layout.Scale"/>, which only drops
    /// under 1 below <see cref="MinWidth"/> x <see cref="MinHeight"/> (a large interface scale on a small window).
    /// </summary>
    public static class NativeExpeditionsMenu
    {
        public enum Mode{Expedition,Waves}
        /// <summary>Who is looking: offline (Solo), the room's host, or a guest (read-only).</summary>
        public enum Seat{Solo,Host,Guest}
        public enum LaunchKind{None,ExpeditionM1,WaveMatch,Ready}

        // ---- Wave Defense starts ------------------------------------------------------------------------------------------
        /// <summary>The start choices: Wave 1, Wave 2, Wave 3 and the Siren (index <see cref="Siren"/>).</summary>
        public const int WaveStarts=4,Siren=3;
        public static readonly string[] WaveLabels={"WAVE 1","WAVE 2","WAVE 3","SIREN"};
        public const string WaveLockNote="NEEDS START-WAVE SUPPORT";
        /// <summary>A room's options hold one boolean (skipWaves), so only Wave 1 and the Siren can be started today.</summary>
        public static bool WaveSupported(int start)=>start==0||start==Siren;
        /// <summary>The skipWaves a start maps to (only meaningful for a supported start).</summary>
        public static bool SkipWaves(int start)=>start==Siren;
        /// <summary>The start the room's skipWaves shows.</summary>
        public static int StartOf(bool skipWaves)=>skipWaves?Siren:0;

        // ---- Launch ---------------------------------------------------------------------------------------------------------
        public struct Launch
        {
            public LaunchKind Kind;public bool Enabled;
            /// <summary>The button's label and its detail line.</summary>
            public string Label,Detail;
            /// <summary>Why it is disabled (null when enabled).</summary>
            public string Reason;
        }
        public const string ExpeditionLabel="START EXPEDITION",WavesLabel="PLAY WAVE DEFENSE";
        public const string Placeholder="Coming soon: this expedition is a placeholder.";
        public const string WaveLocked="Waves 2 and 3 start once rooms carry a start wave.";
        public const string GuestsWaiting="Waiting for every pilot to ready up.";
        public const string HostStarts="The host picks the run and starts it. Ready up when your loadout is set.";
        /// <summary>
        /// What the launch button does for this viewer and pick. `busy`: a connection is open that is not this host's room
        /// (StartMatch refuses then). `guestsReady`: every guest has readied (the room's GuestsReady). `guests`: connected guests.
        /// </summary>
        public static Launch Resolve(NativeExpeditionCatalogue.Expedition pick,Mode mode,int start,Seat seat,bool developer,bool busy,bool guestsReady,int guests)
        {
            var l=new Launch{Kind=LaunchKind.None,Label=mode==Mode.Expedition?ExpeditionLabel:WavesLabel};
            if(seat==Seat.Guest){l.Kind=LaunchKind.Ready;l.Label="READY";l.Detail="THE HOST STARTS";l.Enabled=!busy;l.Reason=busy?null:HostStarts;return l;}
            if(pick==null||pick.Locked){l.Detail=NativeExpeditionCatalogue.ComingSoon;l.Reason=Placeholder;return l;}
            if(mode==Mode.Expedition)
            {
                l.Detail="1–4 PLAYERS · SHARED RUN";
                if(seat==Seat.Host&&!guestsReady){l.Reason=GuestsWaiting;return l;}
                l.Kind=LaunchKind.ExpeditionM1;l.Enabled=!busy;return l;
            }
            if(!pick.WaveDefense){l.Reason=Placeholder;return l;}
            start=Math.Max(0,Math.Min(WaveStarts-1,start));l.Detail=pick.Areas[0].Name+" · "+WaveLabels[start];
            if(!WaveSupported(start)){l.Reason=WaveLocked;return l;}
            l.Kind=LaunchKind.WaveMatch;
            if(seat==Seat.Host&&!guestsReady){l.Reason=GuestsWaiting;return l;}
            l.Enabled=!busy;return l;
        }
        /// <summary>Whether this viewer may change the pick, the mode, the wave and the party bots (a guest sees the host's).</summary>
        public static bool Editable(Seat seat)=>seat!=Seat.Guest;
        /// <summary>The mode the screen opens on: the full run where it can start (a developer, offline), else Wave Defense.</summary>
        public static Mode DefaultMode(Seat seat,bool developer)=>seat==Seat.Solo&&developer?Mode.Expedition:Mode.Waves;

        // ---- Layout ---------------------------------------------------------------------------------------------------------
        public struct Box
        {
            public float X,Y,W,H;public Box(float x,float y,float w,float h){X=x;Y=y;W=w;H=h;}
            public float Right=>X+W;public float Bottom=>Y+H;
            public bool Contains(Box b,float slack=.01f)=>b.X>=X-slack&&b.Y>=Y-slack&&b.Right<=Right+slack&&b.Bottom<=Bottom+slack;
            public bool Overlaps(Box b)=>b.X<Right-.01f&&b.Right>X+.01f&&b.Y<Bottom-.01f&&b.Bottom>Y+.01f;
        }
        public const float MinWidth=1240,MinHeight=720,ContentMax=1760,Top=70,HeaderH=56,PartyGap=14,MainGap=18,BarGap=14;
        /// <summary>The menus' footer: its rule sits FooterRule canvas units above the canvas foot (NativeHomeMenu Footer).</summary>
        public const float FooterRule=54;
        public struct Layout
        {
            /// <summary>Design units per canvas unit is 1/Scale; Width and Height are the design canvas.</summary>
            public float Scale,Width,Height;
            /// <summary>Short design canvases tighten the card, the mode panel and the bar.</summary>
            public bool Compact;
            public Box Content,Header,Party,Browser,Mode,Bar;
            public int Slots;public float SlotGap;
            public Box Slot(int i){float w=(Party.W-SlotGap*(Slots-1))/Slots;return new Box(Party.X+i*(w+SlotGap),Party.Y+PartyLabel,w,Party.H-PartyLabel);}
            /// <summary>The party strip's label row above the cards.</summary>
            public float PartyLabel=>Compact?20:24;
        }
        /// <summary>The screen's regions on a canvas of <paramref name="canvasW"/> x <paramref name="canvasH"/> (NativeUi.W, H).</summary>
        public static Layout Measure(float canvasW,float canvasH,int slots=NativeExpeditionCatalogue.PartyCap)
        {
            float s=Math.Min(1,Math.Min(canvasH/MinHeight,canvasW/MinWidth));if(!(s>0))s=1;float W=canvasW/s,H=canvasH/s;
            // The left margin of the home column and the footer (NativeHomeMenu HomeX: at least 64, 4.5% of the canvas), in design units.
            float mx=Math.Max(64,canvasW*.045f)/s,cw=Math.Min(W-2*mx,ContentMax),x0=(W-cw)/2;
            var l=new Layout{Scale=s,Width=W,Height=H,Slots=Math.Max(1,slots),SlotGap=14};
            l.Header=new Box(x0,Top,cw,HeaderH);
            float partyH=Math.Max(146,Math.Min(208,H*.215f));l.Party=new Box(x0,l.Header.Bottom+PartyGap,cw,partyH);
            float barBottom=H-FooterRule/s-BarGap;float main0=l.Party.Bottom+MainGap;
            l.Compact=barBottom-main0<420;float barH=l.Compact?56:64;l.Bar=new Box(x0,barBottom-barH,cw,barH);
            float mainH=l.Bar.Y-BarGap-main0;float modeW=Math.Max(400,Math.Min(540,cw*.36f)),gap=20;
            l.Browser=new Box(x0,main0,cw-modeW-gap,mainH);l.Mode=new Box(l.Browser.Right+gap,main0,modeW,mainH);
            l.Content=new Box(x0,Top,cw,l.Bar.Bottom-Top);return l;
        }

        // The carousel inside the browser: the label row, the selected card between the neighbours' spines, and the dots.
        public struct Browser{public Box Label,Card,Prev,Next,Dots;}
        public const float BrowserLabel=30,BrowserDots=22;
        public static Browser Carousel(Box b,bool compact)
        {
            float label=compact?26:BrowserLabel,dots=compact?18:BrowserDots,spine=Math.Max(40,Math.Min(64,b.W*.06f)),gap=10;
            var r=new Browser{Label=new Box(b.X,b.Y,b.W,label),Dots=new Box(b.X,b.Bottom-dots,b.W,dots)};
            float y=b.Y+label,h=b.H-label-dots;r.Prev=new Box(b.X,y+h*.06f,spine,h*.88f);r.Next=new Box(b.Right-spine,y+h*.06f,spine,h*.88f);
            r.Card=new Box(b.X+spine+gap,y,b.W-2*(spine+gap),h);return r;
        }
        // The selected card: the key art at the left, the details at the right; route rows from RouteTop down.
        public struct Card{public Box Art,Info;public float Title,Tagline,RouteRow;public int TaglineLines;}
        public static Card CardParts(Box c,bool compact)
        {
            float pad=compact?12:16,artW=Math.Max(200,Math.Min(c.W*.44f,c.H*1.45f));var k=new Card{Art=new Box(c.X+pad,c.Y+pad,artW,c.H-2*pad)};
            float ix=k.Art.Right+(compact?16:22);k.Info=new Box(ix,c.Y+pad,c.Right-pad-ix,c.H-2*pad);
            k.Title=compact?26:34;k.Tagline=compact?12.5f:13.5f;k.TaglineLines=compact?1:2;k.RouteRow=compact?19:28;return k;
        }
        /// <summary>The details column's stack height: kicker, title, tagline, facts, then the route (rows: the hub only when not compact).</summary>
        public static float InfoHeight(Card k,bool compact,int areas)
        {
            int rows=areas+(compact?0:1);
            return (compact?14:18)+k.Title*1.2f+k.TaglineLines*k.Tagline*1.4f+(compact?4:8)+(compact?22:26)+(compact?6:12)+(compact?0:16)+rows*k.RouteRow;
        }
        // The mode panel: its label, the two mode tiles, the area under them (the wave row or the run notes) and the loadout row.
        public struct ModePanel{public Box Label,Expedition,Waves,Detail,Loadout;}
        public static ModePanel Panel(Box m,bool compact)
        {
            float label=compact?22:28,tile=compact?52:74,gap=compact?6:10,load=compact?44:56;var p=new ModePanel{Label=new Box(m.X,m.Y,m.W,label)};
            float y=m.Y+label+(compact?2:6);p.Expedition=new Box(m.X,y,m.W,tile);y+=tile+gap;p.Waves=new Box(m.X,y,m.W,tile);y+=tile+gap+(compact?2:6);
            p.Loadout=new Box(m.X,m.Bottom-load,m.W,load);p.Detail=new Box(m.X,y,m.W,Math.Max(0,p.Loadout.Y-(compact?6:10)-y));return p;
        }
        /// <summary>The wave row's height inside the detail area (its label and the four start tiles).</summary>
        public static float WaveRow(bool compact)=>compact?18+44:22+64;
        // A party card: the portrait stage at the left, the details beside it.
        public struct PartyCard{public Box Stage,Info;}
        public static PartyCard Party(Box slot)
        {
            float sw=Math.Max(64,Math.Min(slot.H*.9f,slot.W*.44f));var p=new PartyCard{Stage=new Box(slot.X+1,slot.Y+1,sw,slot.H-2)};
            float ix=p.Stage.Right+10;p.Info=new Box(ix,slot.Y+10,Math.Max(10,slot.Right-12-ix),slot.H-20);return p;
        }
    }
}
