using System;
using K=ChipArena.UnityConversion.NativeExpeditionCatalogue;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expeditions screen's rules and layout. Engine-free, so the managed checks hold the modes, the launch gates and the fit
    /// at every canvas. Drawn by Runtime/NativeExpeditionsMenu.cs, which is presentation only: every launch goes through a call
    /// the game already made.
    /// <list type="bullet">
    /// <item>START EXPEDITION (owner 2026-11-06: "The run is now randomized based on the slot machines ... no point in selecting
    /// different expeditions as its a all-in-one thing"; then "we don't roll the machine until we start the expedition ... make
    /// player lobby portraits bigger"): the one run for 1–4 signed-in players, entered through the Deployment Station. Nothing
    /// is rolled or sampled here: the party strip and the browser become THE SQUAD, tall cards with large portraits, and the
    /// run's briefing fills a full-height side panel (<see cref="Layout.Squad"/>, <see cref="Layout.Side"/>).</item>
    /// <item>BOSS BATTLE (owner 2026-11-06: "remove the Wave Defense option, and add boss battle option ... to test bosses"):
    /// a roster of every boss. A Direct boss starts its fight at once: the Siren is the boss-test match started at her
    /// (<c>skipWaves</c>), solo with bots or in a co-op room; the zone bosses are Astra's direct starts (NativeBossBattle), 1–4
    /// pilots and no bots. Anything else says honestly why it cannot start (<see cref="K.FightReason"/>).</item>
    /// </list>
    /// Lengths are design units: the canvas (NativeUiMotion.Measure) divided by <see cref="Layout.Scale"/>, which only drops
    /// under 1 below <see cref="MinWidth"/> x <see cref="MinHeight"/> (a large interface scale on a small window).
    /// </summary>
    public static class NativeExpeditionsMenu
    {
        public enum Mode{Expedition,Boss}
        /// <summary>Who is looking: offline (Solo), the room's host, or a guest (read-only).</summary>
        public enum Seat{Solo,Host,Guest}
        public enum LaunchKind{None,ExpeditionM1,BossFight,Ready}

        // ---- Launch ---------------------------------------------------------------------------------------------------------
        public struct Launch
        {
            public LaunchKind Kind;public bool Enabled;
            /// <summary>The button's label and its detail line.</summary>
            public string Label,Detail;
            /// <summary>Why it is disabled (null when enabled).</summary>
            public string Reason;
        }
        public const string ExpeditionLabel="START EXPEDITION",BossLabel="BOSS BATTLE";
        /// <summary>What each mode's tile says.</summary>
        public const string ExpeditionLine="A new city every run · three areas, their bosses, the last fight",ExpeditionLineShort="A new city every run",
            BossLine="Straight to a boss · solo or co-op, no run",BossLineShort="Straight to a boss";
        public const string Locked="This expedition cannot start in this build.";
        public const string GuestsWaiting="Waiting for every pilot to ready up.";
        public const string HostStarts="The host picks the fight and starts it. Ready up when your loadout is set.";
        /// <summary>The launch label for a boss: FIGHT THE SIREN.</summary>
        public static string FightLabel(K.Boss b)=>"FIGHT "+(b?.Name??"");
        /// <summary>
        /// What the launch button does for this viewer and pick. `busy`: a connection is open that is not this host's room
        /// (StartMatch refuses then). `guestsReady`: every guest has readied (the room's GuestsReady).
        /// </summary>
        public static Launch Resolve(Mode mode,K.Boss boss,Seat seat,bool busy,bool guestsReady)
        {
            var l=new Launch{Kind=LaunchKind.None,Label=mode==Mode.Expedition?ExpeditionLabel:FightLabel(boss)};
            if(seat==Seat.Guest){l.Kind=LaunchKind.Ready;l.Label="READY";l.Detail="THE HOST STARTS";l.Enabled=!busy;l.Reason=busy?null:HostStarts;return l;}
            if(mode==Mode.Expedition)
            {
                l.Detail="1–4 PLAYERS · SHARED RUN";
                if(K.Run.Locked){l.Reason=Locked;return l;}
                if(seat==Seat.Host&&!guestsReady){l.Reason=GuestsWaiting;return l;}
                l.Kind=LaunchKind.ExpeditionM1;l.Enabled=!busy;return l;
            }
            if(boss==null){l.Reason=K.FightReason(K.BossAt(K.DefaultBoss));return l;}
            l.Detail=boss.Fight==K.BossFight.Direct?(K.BotsAllowed(boss)?"STRAIGHT TO THE BOSS · BOTS FILL SEATS":"STRAIGHT TO THE BOSS · 1–4 PILOTS"):boss.Fight==K.BossFight.InRun?"FOUGHT IN RUNS":"IN DEVELOPMENT";
            if(boss.Fight!=K.BossFight.Direct){l.Reason=K.FightReason(boss);return l;}
            l.Kind=LaunchKind.BossFight;
            if(seat==Seat.Host&&!guestsReady){l.Reason=GuestsWaiting;return l;}
            l.Enabled=!busy;return l;
        }
        /// <summary>Whether this viewer may change the mode, the boss and the party bots (a guest sees the host's).</summary>
        public static bool Editable(Seat seat)=>seat!=Seat.Guest;
        /// <summary>The mode a room's options show (a guest reads the host's): the full run, else Boss Battle.</summary>
        public static Mode ModeOf(bool expedition)=>expedition?Mode.Expedition:Mode.Boss;

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
            /// <summary>START EXPEDITION's regions: THE SQUAD where the party strip and the browser are, and the side panel where the
            /// mode panel is, both from the party's top down to the bar.</summary>
            public Box Squad,Side;
            public int Slots;public float SlotGap;
            /// <summary>A tall squad card under the squad's label row.</summary>
            public Box SquadSlot(int i){float w=(Squad.W-SlotGap*(Slots-1))/Slots;return new Box(Squad.X+i*(w+SlotGap),Squad.Y+SquadLabel,w,Squad.H-SquadLabel);}
            public float SquadLabel=>Compact?24:30;
            public Box Slot(int i){float w=(Party.W-SlotGap*(Slots-1))/Slots;return new Box(Party.X+i*(w+SlotGap),Party.Y+PartyLabel,w,Party.H-PartyLabel);}
            /// <summary>The party strip's label row above the cards.</summary>
            public float PartyLabel=>Compact?20:24;
        }
        /// <summary>The screen's regions on a canvas of <paramref name="canvasW"/> x <paramref name="canvasH"/> (NativeUi.W, H).</summary>
        public static Layout Measure(float canvasW,float canvasH,int slots=K.PartyCap)
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
            float sy=l.Party.Y,sh=l.Bar.Y-BarGap-sy;l.Squad=new Box(x0,sy,l.Browser.W,sh);l.Side=new Box(l.Mode.X,sy,modeW,sh);
            l.Content=new Box(x0,Top,cw,l.Bar.Bottom-Top);return l;
        }

        // The browser: its label row, then the card. Boss Battle adds the roster rail under the card.
        public struct Browser{public Box Label,Card,Rail;}
        public const float BrowserLabel=30;
        public static float RailHeight(bool compact)=>compact?44:62;
        /// <summary>Boss Battle: the label row, the boss's card and the roster rail at the foot.</summary>
        public static Browser Roster(Box b,bool compact)
        {
            float label=compact?26:BrowserLabel,rail=RailHeight(compact),gap=compact?8:12;
            return new Browser{Label=new Box(b.X,b.Y,b.W,label),Card=new Box(b.X,b.Y+label,b.W,b.H-label-rail-gap),Rail=new Box(b.X,b.Bottom-rail,b.W,rail)};
        }
        /// <summary>Tile <paramref name="i"/> of <paramref name="n"/> along the roster rail.</summary>
        public static Box RailTile(Box rail,int i,int n,float gap=8){n=Math.Max(1,n);float w=(rail.W-gap*(n-1))/n;return new Box(rail.X+i*(w+gap),rail.Y,w,rail.H);}
        // A card: the art at the left, the details at the right.
        public struct Card{public Box Art,Info;public float Title,Tagline,Row;public int TaglineLines;}
        public static Card CardParts(Box c,bool compact)
        {
            float pad=compact?12:16,artW=Math.Max(200,Math.Min(c.W*.44f,c.H*1.45f));var k=new Card{Art=new Box(c.X+pad,c.Y+pad,artW,c.H-2*pad)};
            float ix=k.Art.Right+(compact?16:22);k.Info=new Box(ix,c.Y+pad,c.Right-pad-ix,c.H-2*pad);
            k.Title=compact?26:34;k.Tagline=compact?12.5f:13.5f;k.TaglineLines=compact?1:2;k.Row=compact?19:30;return k;
        }
        /// <summary>The head of a card's details: kicker, title, tag line, then the fact chips.</summary>
        public static float InfoHead(Card k,bool compact)=>(compact?14:18)+k.Title*1.2f+k.TaglineLines*k.Tagline*1.4f+(compact?4:8)+(compact?22:26)+(compact?6:12);
        /// <summary>A boss's details: the head (its subtitle in place of the kicker's second line), then the moves label and two lines.</summary>
        public static float BossInfoHeight(Card k,bool compact)=>InfoHead(k,compact)+(compact?16:20)+(compact?1:2)*(compact?15:18);

        // The mode panel: its label, the two mode tiles, the area under them (the machine pool or the fight's notes) and the
        // loadout row.
        public struct ModePanel{public Box Label,Expedition,Boss,Detail,Loadout;}
        public static ModePanel Panel(Box m,bool compact)
        {
            float label=compact?22:28,tile=compact?52:74,gap=compact?6:10,load=compact?44:56;var p=new ModePanel{Label=new Box(m.X,m.Y,m.W,label)};
            float y=m.Y+label+(compact?2:6);p.Expedition=new Box(m.X,y,m.W,tile);y+=tile+gap;p.Boss=new Box(m.X,y,m.W,tile);y+=tile+gap+(compact?2:6);
            p.Loadout=new Box(m.X,m.Bottom-load,m.W,load);p.Detail=new Box(m.X,y,m.W,Math.Max(0,p.Loadout.Y-(compact?6:10)-y));return p;
        }
        /// <summary>The Boss Battle notes' least height: their label and the first notes.</summary>
        public static float DetailMin(bool compact)=>compact?16+2*16:20+3*20;
        // A squad card: the portrait stage filling the top, the name plate at the foot.
        public struct SquadCard{public Box Stage,Plate;}
        public static float PlateHeight(bool compact)=>compact?86:108;
        public static SquadCard SquadParts(Box slot,bool compact)
        {
            float plate=PlateHeight(compact);return new SquadCard{Stage=new Box(slot.X+1,slot.Y+1,slot.W-2,slot.H-plate-1),Plate=new Box(slot.X+12,slot.Bottom-plate,slot.W-24,plate-12)};
        }
        /// <summary>The plate's stack: the name, the weapon row and its underline, and the foot's pill (24).</summary>
        public static float PlateStack(bool compact)=>compact?2+17*1.3f+2+18+4+24:4+21*1.3f+4+18+8+24;
        // The run's briefing in the side panel's detail area: its label row, kicker, title, tag line, facts, then how a run goes.
        // It takes the full sizes where they fit and the compact ones otherwise.
        public struct Brief{public bool Tight;public float Title,Tagline,Row;public int TaglineLines;}
        public static Brief BriefFor(Box detail,bool compact)
        {
            var full=new Brief{Title=30,Tagline=13,TaglineLines=2,Row=30};
            return !compact&&BriefHeight(full)<=detail.H?full:new Brief{Tight=true,Title=24,Tagline=12,TaglineLines=2,Row=20};
        }
        public static float BriefHeight(Brief b)=>(b.Tight?18:22)+(b.Tight?14:18)+b.Title*1.2f+b.TaglineLines*b.Tagline*1.4f+(b.Tight?4:8)+(b.Tight?22:26)+(b.Tight?8:14)+(b.Tight?0:18)+K.Steps.Length*b.Row;
        // A party card: the portrait stage at the left, the details beside it.
        public struct PartyCard{public Box Stage,Info;}
        public static PartyCard Party(Box slot)
        {
            float sw=Math.Max(64,Math.Min(slot.H*.9f,slot.W*.44f));var p=new PartyCard{Stage=new Box(slot.X+1,slot.Y+1,sw,slot.H-2)};
            float ix=p.Stage.Right+10;p.Info=new Box(ix,slot.Y+10,Math.Max(10,slot.Right-12-ix),slot.H-20);return p;
        }
    }
}
