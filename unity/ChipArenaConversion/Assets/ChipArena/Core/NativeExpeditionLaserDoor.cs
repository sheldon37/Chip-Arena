using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The instance exit's laser door (Claude, 2026-10-17; docs/EXPEDITION-PRESENTATION.md "The laser door"). Owner, 2026-10-17:
    // "When entering an instance, lets animate the door behind us getting locked (In this case, the laser door powering on) when
    // its unlocked, we can animate that too." Presentation only. What blocks is the exit's closure record (closures[], Astra's
    // director; ExpeditionClosureView.Blocking is the collider's own predicate) and nothing here writes it:
    //  - In the live split-area run the exit of every instance is sealed from the run's start until that instance is cleared
    //    (ExpeditionDirector.Closed: an instance area is always in its Instance phase), so the party arrives behind a door that
    //    is already sealed. The lock is played from this client's arrival in the area (ExpeditionStateCues.LockAt): the beams fire
    //    behind the party ArriveDelay after the arrival, over the director's one-second transition. The field sheet and the floor
    //    beam stand in the doorway from the first frame, so the door never looks passable while it blocks.
    //  - A closure that is sealing (the single-area director, or a future arrival-driven closure) plays the same lock exactly from
    //    its `began`, and an opening plays the unlock exactly from its `began`: the beams shut off top-down, the floor beam last,
    //    at the instant the state turns open and the collider releases.
    // Engine-free and allocation-free (the glyphs are cached once).

    public enum ExpeditionLaserStage { Open, Lock, Sealed, Unlock }

    /// <summary>A zone's laser colours (sRGB hex): the beam, its white-hot core, the glow and haze it throws, the emitters'
    /// cool-down ember and the sealed glyph's ink.</summary>
    public struct ExpeditionLaserPalette { public int Beam,Core,Glow,Ember,Ink; }

    public static class NativeExpeditionLaserDoor
    {
        /// <summary>Beams across the doorway (full and low tier).</summary>
        public const int Beams=9,LowBeams=5;
        /// <summary>The lock and the unlock take the director's one-second transition.</summary>
        public const double LockSeconds=NativeExpeditionStates.TransitionSeconds;
        /// <summary>The emitters' cool-down after their beam lets go; a teammate's arrival pulse; the HUD notice.</summary>
        public const double CoolSeconds=1.6,PulseSeconds=.5,NoticeSeconds=3.2;
        /// <summary>Lock: the charge climbs the emitters over the first ChargeEnd of the second and each beam fires as the charge
        /// reaches it (the floor beam at once); SEALED slams on at SealAt. Unlock: every beam stutters for UnlockStutter, then they
        /// go out top-down, each after BeamStutter of its own stutter, the floor beam exactly at the end of the opening second.</summary>
        public const float ChargeEnd=.78f,SealAt=.84f,UnlockStutter=.16f,BeamStutter=.12f;
        /// <summary>The floor beam's level before the lock starts (the arrival's standby, while the screen fades out) and its least
        /// level while it stutters during the opening: never dark while the closure blocks.</summary>
        public const float StandbyFloor=.55f,StutterFloor=.4f;
        /// <summary>The snap-on flash's length and a fired beam's travel from its emitter across the doorway (seconds).</summary>
        public const float FlashSeconds=.14f,TravelSeconds=.045f;
        public const int OpenHex=0x3DFF7A,AmberHex=0xFFB020,LockRedHex=0xFF3030;
        public const string WardZero="exp-ward-zero",Station="exp-transit",Ironworks="exp-foundry";

        /// <summary>Each live zone's palette: Ward Zero's red containment, Central Station's teal, the Ironworks' amber (the join
        /// circles' accents are the glow); any other instance a cool cyan.</summary>
        public static ExpeditionLaserPalette Palette(string building)
        {
            switch(building)
            {
                case WardZero:return new ExpeditionLaserPalette{Beam=0xFF2E26,Core=0xFFE4DE,Glow=0xFF4A3A,Ember=0xFF8A4A,Ink=0xFFD2CC};
                case Station:return new ExpeditionLaserPalette{Beam=0x2CF2D8,Core=0xE2FFFA,Glow=0x7AE8D0,Ember=0xFFB27A,Ink=0xD8FFF6};
                case Ironworks:return new ExpeditionLaserPalette{Beam=0xFF9A26,Core=0xFFF2D6,Glow=0xFFA040,Ember=0xFF5A18,Ink=0xFFE6C0};
                default:return new ExpeditionLaserPalette{Beam=0x3AD8FF,Core=0xE6FBFF,Glow=0x22E6FF,Ember=0xFFB27A,Ink=0xE6FBFF};
            }
        }
        public static int Count(bool low)=>low?LowBeams:Beams;
        /// <summary>Beam i's height (0 the floor beam) of n across a doorway H tall: the floor beam 12 cm up, the top one 22 cm
        /// under the head, evenly between.</summary>
        public static float Height(int i,int n,float H){float lo=.12f,hi=H-.22f;return n<=1?lo:lo+(hi-lo)*i/(n-1);}
        /// <summary>When beam i fires during the lock (seconds from its start): the floor beam at once, the top one at ChargeEnd.</summary>
        public static double FireAt(int i,int n)=>n<=1?0:LockSeconds*ChargeEnd*i/(n-1);
        /// <summary>When beam i goes out during the unlock (seconds from its start): top-down, the floor beam (i = 0) exactly at the
        /// end of the opening second.</summary>
        public static double OffAt(int i,int n){int k=n-1-i;double s=UnlockStutter;return LockSeconds*(s+(1-s)*(k+1)/n);}

        /// <summary>
        /// The door's stage at presentation time t from its closure and this client's arrival lock (`lockAt`: when the lock starts,
        /// +inf while this client is in the area but not yet presented, NaN without an arrival; `arrivedAt` when it arrived).
        /// `age` is seconds into the stage: into the lock or unlock (negative while the arrival lock waits), since the door opened.
        /// Sealing and opening follow the closure's `began` exactly; a sealed door plays the arrival lock only when it was sealed
        /// before this client arrived (a door this client saw sealing is not locked again).
        /// </summary>
        public static ExpeditionLaserStage Stage(ExpeditionClosureView c,double t,double lockAt,double arrivedAt,out double age)
        {
            age=0;
            switch(NativeExpeditionStates.Phase(c))
            {
                case ExpeditionClosurePhase.Sealing:age=Clamp(t-c.began,0,LockSeconds);return ExpeditionLaserStage.Lock;
                case ExpeditionClosurePhase.Opening:age=Clamp(t-c.began,0,LockSeconds);return ExpeditionLaserStage.Unlock;
                case ExpeditionClosurePhase.Open:age=double.IsNaN(c.began)?double.PositiveInfinity:Math.Max(0,t-c.began);return ExpeditionLaserStage.Open;
            }
            if(!double.IsNaN(lockAt)&&(double.IsPositiveInfinity(lockAt)||!(c.began>arrivedAt)))
            {
                double a=t-lockAt;if(a<LockSeconds){age=a;return ExpeditionLaserStage.Lock;}
            }
            return ExpeditionLaserStage.Sealed;
        }
        private static double Clamp(double x,double lo,double hi)=>double.IsNaN(x)?lo:x<lo?lo:x>hi?hi:x;

        /// <summary>
        /// Beam i's level (0 out .. 1 on) of n at the stage and age, and its snap-on flash (0..1) and drawn length (0..1, from its
        /// emitter). Open: none. Sealed: all. Lock: fired from the floor up; before the lock starts only the floor beam at
        /// StandbyFloor. Unlock: every beam stutters, then they go out top-down; the floor beam never below StutterFloor until the
        /// view says open. Reduced effects: no stutter, flash or travel (fades instead).
        /// </summary>
        public static float Level(ExpeditionLaserStage s,double age,int i,int n,double t,int seed,bool reduced,out float flash,out float reach)
        {
            flash=0;reach=1;
            switch(s)
            {
                case ExpeditionLaserStage.Open:reach=0;return 0;
                case ExpeditionLaserStage.Sealed:return 1;
                case ExpeditionLaserStage.Lock:
                {
                    double a=age-FireAt(i,n);
                    if(a<0||age<0){if(i==0){return StandbyFloor;}reach=0;return 0;}
                    if(reduced){float k=Math.Max(.05f,Math.Min(1,(float)a/.1f));return i==0?Math.Max(StandbyFloor,k):k;}
                    flash=Math.Max(0,1-(float)a/FlashSeconds);reach=i==0?1:Math.Min(1,(float)a/TravelSeconds);return 1;
                }
                default:
                {
                    // The floor beam goes out only when the view says open (the stage leaves Unlock), never on the clock alone.
                    double off=OffAt(i,n);if(age>=off&&i!=0){reach=0;return 0;}
                    float floor=i==0?StutterFloor:0;
                    if(reduced){double k=(off-age)/BeamStutter;return k>=1?1:Math.Max(floor,(float)k);}
                    bool stutter=age<UnlockStutter*LockSeconds||age>=off-BeamStutter;if(!stutter)return 1;
                    // A deterministic flicker per 1/40 s: everyone sees the same stutter.
                    float h=NativeExpeditionStates.Hash((int)Math.Floor(t*40),seed*31+i);return h<.42f?floor:h<.6f?Math.Max(floor,.45f):1;
                }
            }
        }
        /// <summary>The emitters' charge rails (0..1 filled): climbing over the lock, full while sealed, draining top-down as the
        /// unlock puts the beams out, empty open.</summary>
        public static float Charge(ExpeditionLaserStage s,double age)
        {
            switch(s)
            {
                case ExpeditionLaserStage.Sealed:return 1;
                case ExpeditionLaserStage.Open:return 0;
                case ExpeditionLaserStage.Lock:{double k=age/(LockSeconds*ChargeEnd);return k<=0?0:k>=1?1:(float)(1-(1-k)*(1-k));}
                default:{double k=(age-UnlockStutter*LockSeconds)/((1-UnlockStutter)*LockSeconds);return k<=0?1:k>=1?0:(float)(1-k);}
            }
        }
        /// <summary>How hot beam i's emitter still is after its beam let go (1 just out .. 0 cold), from the unlock or the door's
        /// open age; 0 while the beam is lit or long after.</summary>
        public static float Ember(ExpeditionLaserStage s,double age,int i,int n)
        {
            double since;
            if(s==ExpeditionLaserStage.Unlock)since=age-OffAt(i,n);
            else if(s==ExpeditionLaserStage.Open)since=age+LockSeconds-OffAt(i,n);
            else return 0;
            if(!(since>=0)||since>=CoolSeconds)return 0;double k=1-since/CoolSeconds;return (float)(k*k);
        }
        /// <summary>The hum on a lit beam: a fine shimmer, a rare dropout and a slow scan pulse climbing the beams (none with
        /// reduced effects).</summary>
        public static float Hum(int i,int n,double t,int seed,bool reduced)
        {
            if(reduced)return 1;float h=.9f+.1f*NativeExpeditionStates.Hash((int)Math.Floor(t*24),seed*17+i);
            if(NativeExpeditionStates.Hash((int)Math.Floor(t*12),seed*7+i*13)>.988f)h*=.55f;
            double ph=(t/2.6)%1;float x=(float)(ph*1.4-.2)-(n<=1?0:i/(float)(n-1));h+=.55f*(float)Math.Exp(-x*x/.006);return h;
        }
        /// <summary>A teammate's arrival: the beams flash floor-up over PulseSeconds (the door re-seals behind them).</summary>
        public static float Pulse(double age,int i,int n)
        {
            if(!(age>=0)||age>=PulseSeconds)return 0;double x=age/PulseSeconds*1.3-(n<=1?0:i/(double)(n-1));return (float)Math.Exp(-x*x/.012);
        }
        /// <summary>The header's word: LOCKING then SEALED (from SealAt) during the lock, SEALED, UNLOCKING, OPEN.</summary>
        public static int Word(ExpeditionLaserStage s,double age)
        {
            switch(s){case ExpeditionLaserStage.Open:return WordOpen;case ExpeditionLaserStage.Sealed:return WordSealed;case ExpeditionLaserStage.Unlock:return WordUnlocking;}
            return age>=SealAt*LockSeconds?WordSealed:WordLocking;
        }
        public const int WordSealed=0,WordLocking=1,WordUnlocking=2,WordOpen=3;
        public static readonly string[] Words={"SEALED","LOCKING","UNLOCKING","OPEN"};
        /// <summary>The words' glyph cells (x, y from the top, width, height in cells; 4 cells a character), merged once.</summary>
        public static readonly int[][] WordCells=Cells();
        /// <summary>Each word's width in cells.</summary>
        public static int WordWidth(int w)=>Words[w].Length*4-1;
        private static int[][] Cells()
        {
            var all=new int[Words.Length][];
            for(int w=0;w<Words.Length;w++)
            {
                var list=new List<int>();var s=Words[w];
                for(int k=0;k<s.Length;k++)foreach(var b in NativeExpeditionCityKit.GlyphBoxes(s[k])){list.Add(k*4+b[0]);list.Add(b[1]);list.Add(b[2]);list.Add(b[3]);}
                all[w]=list.ToArray();
            }
            return all;
        }
    }

    /// <summary>The DEV panel's laser door preview (presentation only): a stand-in closure for the current instance's exit that
    /// cycles lock (sealing, 1 s), sealed (3 s), unlock (opening, 1 s) and open (2 s) on the presentation clock. The live record
    /// and its collider are untouched; the scene draws the stand-in in its place (NativeExpeditionStateScene.DoorPreview).</summary>
    public static class NativeLaserDoorPreview
    {
        public static bool On;
        public const double Period=7,SealedFor=3,OpenFor=2;
        public static void Fill(ExpeditionClosureView c,string id,string area,double t)
        {
            if(c==null)return;double k=t-Math.Floor(t/Period)*Period,start=t-k,lk=NativeExpeditionLaserDoor.LockSeconds;c.id=id;c.area=area;
            if(k<lk){c.state=NativeExpeditionStates.Sealing;c.began=start;}
            else if(k<lk+SealedFor){c.state=NativeExpeditionStates.Sealed;c.began=start+lk;}
            else if(k<2*lk+SealedFor){c.state=NativeExpeditionStates.Opening;c.began=start+lk+SealedFor;}
            else{c.state=NativeExpeditionStates.Open;c.began=start+2*lk+SealedFor;}
        }
    }
}
