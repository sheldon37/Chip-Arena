using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The presentation side of the Cyber Lord's second kit, ranged half (docs/CYBER-LORD.md "Kit v2"; the numbers and the shared
    /// helpers are CyberKit2Spec's): the cyber mage's Plasma Byte fireballs (every third an Overflow), 1 Daisy Chain, 2 Singularity,
    /// 3 Packet Storm and 4 Orbital Uplink. Astra builds the rules and the wire record; until it replicates, the Dev Sandbox preview
    /// and the fixtures write the proposed record below (CyberKit2RangedRecord) and CyberLordFeed maps it into the view's one field
    /// (CyberLordView.Kit2R) only when its Version is 2, so v1's path is untouched otherwise. Presentation only.
    /// </summary>
    public static class CyberKit2Ranged
    {
        /// <summary>The view's ranged slots (R): the LMB fireballs, then 1-4 (CyberKit2Spec.RangedLmb + R).</summary>
        public const int Plasma=0,Chain=1,Singularity=2,Storm=3,Orbital=4,Slots=5;
        public static int Spec(int r)=>CyberKit2Spec.RangedLmb+r;
        /// <summary>The chain's pilots (the first strike and every hop).</summary>
        public const int MaxChain=CyberKit2Spec.Hops+1;
        /// <summary>The fireballs' projectile Details (Spec cyberlord): a Plasma Byte, an Overflow.</summary>
        public const string PlasmaDetail="plasma-byte",OverflowDetail="plasma-overflow";
        /// <summary>The proposed event Details of the ranged set (Hit / AbilityHit on a pilot, Impact on the world).</summary>
        public static readonly string[] EventDetails={"plasma-hit","plasma-overflow-hit","chain-hop","singularity-tick","singularity-implode","storm-drop","orbital-tick"};
        /// <summary>Where the hole's centre sits over a floor the orb stopped on (the proposal: the host lifts it so the pull and
        /// the drawn hole surround the pilots, not the floor).</summary>
        public const float HoleLift=.9f;
        /// <summary>The height the storm's meteors and the orbital column fall from (m over their floor point).</summary>
        public const float SkyHeight=16,BeamHeight=34;
    }

    /// <summary>
    /// The PROPOSED wire record of the v2 ranged set (docs/CYBER-LORD.md "Kit v2": Astra's record replaces it; only
    /// CyberLordFeed reads it, through CyberKit2RangedFeed.Map). Unity-serializable (V3, fixed arrays) so it can be embedded in
    /// Astra's kit record as is. Times are the match clock (-100 never); points world metres.
    /// - Version: CyberKit2Spec.Version (2) when the v2 kit is live; anything else is ignored (v1 draws).
    /// - Per slot (CyberKit2Ranged order: Plasma, Chain, Singularity, Storm, Orbital): At, Release (= At + windup), Until
    ///   (= At + length, or the cut), Ready, CutAt (an interrupt's time, else -100; a cut before Release sets Release there).
    /// - PlasmaShot: the newest throw's index since the attack chain began (0, 1, 2 ...; Overflow every third).
    /// - Chain: the struck pilots in order (ids, 0 for none; up to six) and their body centres when struck; ChainFrom the
    ///   casting hand's point at the release. Hop i lands at Release + i x HopDelay.
    /// - Singularity: OrbFrom (the hand), OrbPoint (the hole's centre: where the orb stopped, lifted HoleLift off a floor),
    ///   OrbLand (the hole forms), Implode (the implosion; OrbLand + HoleSeconds unless it ends early).
    /// - Storm: the ring's centre (the floor point) and the seed; drops from CyberKit2Spec.StormDrop after Release.
    /// - Orbital: Release is the column's start (the call-down's end), Until its end; OrbitalCentre the call-down's floor point,
    ///   OrbitalPoint the column's floor point at the host tick OrbitalPointAt and OrbitalAim the aim's floor point there
    ///   (clamped to OrbitalRange), so a client advances the column with CyberKit2Spec.OrbitalStep between snapshots.
    /// </summary>
    [Serializable] public sealed class CyberKit2RangedRecord
    {
        public int Version;
        public float[] At=New(),Release=New(),Until=New(),Ready=New(),CutAt=New();
        public int PlasmaShot=-1;
        public int ChainCount;public int[] ChainIds=new int[CyberKit2Ranged.MaxChain];public V3[] ChainPoints=new V3[CyberKit2Ranged.MaxChain];public V3 ChainFrom;
        public V3 OrbFrom,OrbPoint;public float OrbLand=-100,Implode=-100;
        public V3 StormCentre;public int StormSeed;
        public V3 OrbitalCentre,OrbitalPoint,OrbitalAim;public float OrbitalPointAt=-100;
        private static float[] New(){var a=new float[CyberKit2Ranged.Slots];for(int i=0;i<a.Length;i++)a[i]=-100;return a;}
        public CyberKit2RangedRecord ViewCopy()
        {
            var c=(CyberKit2RangedRecord)MemberwiseClone();c.At=(float[])At.Clone();c.Release=(float[])Release.Clone();c.Until=(float[])Until.Clone();c.Ready=(float[])Ready.Clone();c.CutAt=(float[])CutAt.Clone();
            c.ChainIds=(int[])ChainIds.Clone();c.ChainPoints=(V3[])ChainPoints.Clone();return c;
        }
        /// <summary>The bounds a network frame would check (finite, five per array, six chain entries, ordered windows).</summary>
        public bool Valid()
        {
            bool F(float n)=>MathUtil.Finite(n)&&Math.Abs(n)<1e8f;bool P(V3 p)=>F(p.x)&&F(p.y)&&F(p.z)&&Math.Abs(p.x)<1e4f&&Math.Abs(p.y)<1e4f&&Math.Abs(p.z)<1e4f;
            foreach(var a in new[]{At,Release,Until,Ready,CutAt}){if(a==null||a.Length!=CyberKit2Ranged.Slots)return false;foreach(var n in a)if(!F(n))return false;}
            for(int i=0;i<CyberKit2Ranged.Slots;i++)if(Release[i]<At[i]||Until[i]<Release[i])return false;
            if(ChainIds==null||ChainPoints==null||ChainIds.Length!=CyberKit2Ranged.MaxChain||ChainPoints.Length!=CyberKit2Ranged.MaxChain||ChainCount<0||ChainCount>CyberKit2Ranged.MaxChain)return false;
            foreach(var p in ChainPoints)if(!P(p))return false;
            return PlasmaShot>=-1&&F(OrbLand)&&F(Implode)&&F(OrbitalPointAt)&&P(ChainFrom)&&P(OrbFrom)&&P(OrbPoint)&&P(StormCentre)&&P(OrbitalCentre)&&P(OrbitalPoint)&&P(OrbitalAim);
        }
    }

    /// <summary>Where the v2 ranged record comes from before the rules replicate it (the Dev Sandbox preview; the fixtures).</summary>
    public interface ICyberKit2RangedSource{CyberKit2RangedRecord Of(int actor);}

    /// <summary>
    /// The presentation's copy of a pilot's v2 ranged state for a frame (CyberKit2RangedFeed.Map fills it; no allocation once
    /// made). Live only when the record's Version is 2. Every v2 ranged pose, effect, HUD card and sound reads this.
    /// </summary>
    public sealed class CyberKit2RangedView
    {
        public bool Live;
        public readonly float[] At=new float[CyberKit2Ranged.Slots],Release=new float[CyberKit2Ranged.Slots],Until=new float[CyberKit2Ranged.Slots],Recover=new float[CyberKit2Ranged.Slots],Ready=new float[CyberKit2Ranged.Slots],CutAt=new float[CyberKit2Ranged.Slots];
        public int Shot=-1;
        public int ChainCount;public readonly int[] ChainIds=new int[CyberKit2Ranged.MaxChain];public readonly Vector3[] ChainPoints=new Vector3[CyberKit2Ranged.MaxChain];public Vector3 ChainFrom;
        public Vector3 OrbFrom,OrbPoint;public float OrbLand=-100,Implode=-100;
        public Vector3 StormCentre;public int StormSeed;
        public Vector3 OrbitalCentre,OrbitalPoint,OrbitalAim;public float OrbitalPointAt=-100;
        /// <summary>
        /// The Plasma Byte throw before the newest (its At, Release, Until, Ready, CutAt and Shot; At -100: none known). The record keeps
        /// only the newest throw, so the view remembers the one it replaced (CyberKit2RangedFeed.Map notices each new throw for the
        /// same pilot): the body's follow-through of one throw then carries on into the next however long the gap
        /// (Core/CyberLordRangedMotion.cs). Presentation history only, kept across Clear; it is forgotten when the record stops
        /// being live, the pilot changes or the clock runs back.
        /// </summary>
        public float PrevAt=-100,PrevRelease=-100,PrevUntil=-100,PrevReady=-100,PrevCutAt=-100;public int PrevShot=-1;
        private float seenAt=-100,seenRelease=-100,seenUntil=-100,seenReady=-100,seenCutAt=-100;private int seenShot=-1,seenOwner=int.MinValue;
        /// <summary>Notes this frame's newest throw for pilot `owner` (after the record is mapped): a new one moves the last into
        /// Prev*. The same throw re-sent (a cut, or a prediction nudging its start under .05 s) only refreshes it.</summary>
        internal void Observe(int owner)
        {
            const int P=CyberKit2Ranged.Plasma;float at=At[P];
            if(!Live||owner!=seenOwner||!(at>-90)){Forget();seenOwner=owner;if(Live&&at>-90)Seen();return;}
            bool same=Shot==seenShot&&Math.Abs(at-seenAt)<.05f;
            if(!same)
            {
                if(seenAt>-90&&at>seenAt+1e-4f){PrevAt=seenAt;PrevRelease=seenRelease;PrevUntil=Math.Min(seenUntil,at);PrevReady=seenReady;PrevCutAt=seenCutAt;PrevShot=seenShot;}
                else Forget();
            }
            Seen();
        }
        private void Seen(){const int P=CyberKit2Ranged.Plasma;seenAt=At[P];seenRelease=Release[P];seenUntil=Until[P];seenReady=Ready[P];seenCutAt=CutAt[P];seenShot=Shot;}
        private void Forget(){PrevAt=PrevRelease=PrevUntil=PrevReady=PrevCutAt=-100;PrevShot=-1;seenAt=seenRelease=seenUntil=seenReady=seenCutAt=-100;seenShot=-1;}
        public void Clear()
        {
            Live=false;for(int i=0;i<CyberKit2Ranged.Slots;i++)At[i]=Release[i]=Until[i]=Recover[i]=Ready[i]=CutAt[i]=-100;Shot=-1;ChainCount=0;
            for(int i=0;i<CyberKit2Ranged.MaxChain;i++){ChainIds[i]=0;ChainPoints[i]=default;}ChainFrom=OrbFrom=OrbPoint=StormCentre=OrbitalCentre=OrbitalPoint=OrbitalAim=default;
            OrbLand=Implode=OrbitalPointAt=-100;StormSeed=0;
        }
        /// <summary>Slot `slot`'s window at `time` (Live from its start to the end of its settle).</summary>
        public CyberPhase Phase(int slot,float time)
        {
            var p=new CyberPhase{At=At[slot],Release=Release[slot],Until=Until[slot]};p.End=Math.Max(p.Until,Recover[slot]);
            if(!Live||!(p.At>-90)||time<p.At||time>=p.End)return p;p.Live=true;p.Age=time-p.At;float rel=p.Release>p.At?p.Release:p.At;
            if(time<rel){p.Part=0;p.U=Clamp01((time-p.At)/Math.Max(1e-4f,rel-p.At));}
            else if(time<p.Until){p.Part=1;p.U=Clamp01((time-rel)/Math.Max(1e-4f,p.Until-rel));}
            else{p.Part=2;p.U=Clamp01((time-p.Until)/Math.Max(1e-4f,p.End-p.Until));}
            return p;
        }
        /// <summary>Whether slot `slot`'s cast reached its release (a cast cut before it never fires: no shot, no cue).</summary>
        public bool Released(int slot)=>Live&&At[slot]>-90&&(CutAt[slot]<-90||CutAt[slot]>Release[slot]+1e-4f)&&Until[slot]>=Release[slot];
        /// <summary>The newest cast live at `time` (to the end of its settle), or -1.</summary>
        public int Newest(float time)
        {
            if(!Live)return -1;int newest=-1;float at=-100;
            for(int s=0;s<CyberKit2Ranged.Slots;s++){float start=At[s],end=Math.Max(Until[s],Recover[s]);if(start>-90&&time>=start&&time<end&&start>at){newest=s;at=start;}}
            return newest;
        }
        /// <summary>Whether a cast window (to its end, not its settle) is live at `time`.</summary>
        public bool Casting(float time){if(!Live)return false;for(int s=0;s<CyberKit2Ranged.Slots;s++)if(At[s]>-90&&time>=At[s]&&time<Until[s])return true;return false;}
        /// <summary>The newest throw's hands (0 right, 1 left, 2 both: the Overflow).</summary>
        public int Hand=>CyberKit2Spec.PlasmaHand(Math.Max(0,Shot));
        public bool Overflow=>CyberKit2Spec.Overflow(Shot);
        /// <summary>The next throw's hands as of `time`: the chain's next, or the right hand once the chain has reset (a pause over
        /// ComboReset after the newest throw ended).</summary>
        public int NextHand(float time)=>Shot<0||!(At[CyberKit2Ranged.Plasma]>-90)||time>=Until[CyberKit2Ranged.Plasma]+CyberKit2Spec.ComboReset?CyberKit2Spec.PlasmaHand(0):CyberKit2Spec.PlasmaHand(Shot+1);
        /// <summary>When chain hop `i` (0 the first strike) lands.</summary>
        public float HopAt(int i)=>Release[CyberKit2Ranged.Chain]+i*CyberKit2Spec.HopDelay;
        /// <summary>Storm drop `i`: its impact time and floor point (CyberKit2Spec.StormDrop from the replicated centre and seed).</summary>
        public void Drop(int i,out float at,out Vector3 point)
        {
            CyberKit2Spec.StormDrop(StormSeed,i,out float t,out float x,out float z);at=Release[CyberKit2Ranged.Storm]+t;point=new Vector3(StormCentre.X+x,StormCentre.Y,StormCentre.Z+z);
        }
        /// <summary>The storm's last impact.</summary>
        public float StormEnd{get{float end=-100;for(int i=0;i<CyberKit2Spec.Drops;i++){Drop(i,out float at,out _);end=Math.Max(end,at);}return end;}}
        /// <summary>The orbital column's floor point at `time`: the replicated point advanced toward the replicated aim at the
        /// chase speed (CyberKit2Spec.OrbitalStep), so the column moves smoothly between snapshots.</summary>
        public Vector3 OrbitalAt(float time)
        {
            if(!(OrbitalPointAt>-90))return OrbitalCentre;float x=OrbitalPoint.X,z=OrbitalPoint.Z;CyberKit2Spec.OrbitalStep(ref x,ref z,OrbitalAim.X,OrbitalAim.Z,Math.Min(.5f,time-OrbitalPointAt));
            float u=Math.Abs(OrbitalAim.X-OrbitalPoint.X)+Math.Abs(OrbitalAim.Z-OrbitalPoint.Z)>1e-4f?Clamp01(((x-OrbitalPoint.X)*(OrbitalAim.X-OrbitalPoint.X)+(z-OrbitalPoint.Z)*(OrbitalAim.Z-OrbitalPoint.Z))/((OrbitalAim.X-OrbitalPoint.X)*(OrbitalAim.X-OrbitalPoint.X)+(OrbitalAim.Z-OrbitalPoint.Z)*(OrbitalAim.Z-OrbitalPoint.Z))):0;
            return new Vector3(x,OrbitalPoint.Y+(OrbitalAim.Y-OrbitalPoint.Y)*u,z);
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
    }

    /// <summary>
    /// The v2 ranged mapping (called only by CyberLordFeed, which owns every replicated name): the record into the view, the
    /// projectile and event Details into what they are. Allocation-free.
    /// </summary>
    public static class CyberKit2RangedFeed
    {
        /// <summary>Fills `v` from `r` when it is a live v2 record (Version 2), else clears it; `owner` is the pilot the record
        /// belongs to (the view then remembers the throw before the newest: CyberKit2RangedView.Prev*).</summary>
        public static void Map(CyberKit2RangedRecord r,CyberKit2RangedView v,int owner){Fill(r,v);v.Observe(owner);}
        public static void Map(CyberKit2RangedRecord r,CyberKit2RangedView v)=>Map(r,v,0);
        private static void Fill(CyberKit2RangedRecord r,CyberKit2RangedView v)
        {
            v.Clear();if(r==null||r.Version!=CyberKit2Spec.Version||r.At==null)return;v.Live=true;
            for(int i=0;i<CyberKit2Ranged.Slots;i++)
            {
                float at=r.At[i],until=Math.Max(at,r.Until[i]),cut=r.CutAt[i];v.At[i]=at;v.Until[i]=until;v.Release[i]=Math.Max(at,Math.Min(r.Release[i],until));v.Ready[i]=r.Ready[i];v.CutAt[i]=cut>=at&&at>-90?cut:-100;
                // A cut cast has no settle; a natural end eases back over the spec's settle.
                v.Recover[i]=at>-90?until+(v.CutAt[i]>-90?0:CyberKit2Spec.Settle[CyberKit2Ranged.Spec(i)]):-100;
            }
            v.Shot=r.PlasmaShot;v.ChainCount=Math.Max(0,Math.Min(CyberKit2Ranged.MaxChain,r.ChainCount));
            for(int i=0;i<CyberKit2Ranged.MaxChain;i++){v.ChainIds[i]=r.ChainIds!=null&&i<r.ChainIds.Length?r.ChainIds[i]:0;v.ChainPoints[i]=r.ChainPoints!=null&&i<r.ChainPoints.Length?N(r.ChainPoints[i]):default;}
            v.ChainFrom=N(r.ChainFrom);v.OrbFrom=N(r.OrbFrom);v.OrbPoint=N(r.OrbPoint);v.OrbLand=r.OrbLand;v.Implode=r.Implode;v.StormCentre=N(r.StormCentre);v.StormSeed=r.StormSeed;
            v.OrbitalCentre=N(r.OrbitalCentre);v.OrbitalPoint=N(r.OrbitalPoint);v.OrbitalAim=N(r.OrbitalAim);v.OrbitalPointAt=r.OrbitalPointAt;
        }
        private static Vector3 N(V3 p)=>new Vector3(p.x,p.y,p.z);
        /// <summary>A fireball's kind from its projectile Detail: 1 a Plasma Byte, 2 an Overflow, 0 anything else.</summary>
        public static int ShotKind(string detail)=>detail==CyberKit2Ranged.PlasmaDetail?1:detail==CyberKit2Ranged.OverflowDetail?2:0;
        /// <summary>The ranged set's event kinds (values past v1's CyberHit list, so the shared enum stays as it is).</summary>
        public const CyberHit PlasmaHit=(CyberHit)64,OverflowHit=(CyberHit)65,ChainHop=(CyberHit)66,SingularityTick=(CyberHit)67,SingularityImplode=(CyberHit)68,StormDrop=(CyberHit)69,OrbitalTick=(CyberHit)70;
        public static CyberHit Hit(string detail)
        {
            if(detail==null)return CyberHit.None;var d=CyberKit2Ranged.EventDetails;
            for(int i=0;i<d.Length;i++)if(detail==d[i])return PlasmaHit+i;
            // The fireballs' own projectile Details on an Impact (the world) read as their hits.
            return detail==CyberKit2Ranged.PlasmaDetail?PlasmaHit:detail==CyberKit2Ranged.OverflowDetail?OverflowHit:CyberHit.None;
        }
        public static bool Is(CyberHit h)=>h>=PlasmaHit&&h<=OrbitalTick;
    }

    /// <summary>
    /// The v2 ranged stand-in source: the Dev Sandbox preview (the local pilot's own screen) and the checks' and render harness's
    /// fixture write CyberKit2RangedRecords exactly as the proposed host would (CyberKit2Spec's windows, numbers and helpers, at
    /// attack rate 1), and keep the fireballs in flight and their landings for the effects (ICyberShots). Nothing reaches the
    /// authority, the actor or the network frame. Allocation-free once warm (fixed rings).
    /// </summary>
    public sealed class CyberKit2RangedPreview:ICyberKit2RangedSource,ICyberShots
    {
        private readonly Dictionary<int,CyberKit2RangedRecord> records=new Dictionary<int,CyberKit2RangedRecord>();
        public CyberKit2RangedRecord Of(int actor)=>records.TryGetValue(actor,out var r)?r:null;
        public CyberKit2RangedRecord For(int actor){if(!records.TryGetValue(actor,out var r)){r=new CyberKit2RangedRecord{Version=CyberKit2Spec.Version};records[actor]=r;}return r;}
        public void Clear(){records.Clear();flightCount=0;impactCount=0;shown=0;}
        public bool Any=>records.Count>0;

        // ---- the fireballs in flight and their landings ----
        public struct Flight{public int Id,Owner,Kind;public Vector3 From,Dir;public float At,EndAt;}
        public const int MaxFlights=24,MaxImpacts=24;
        private readonly Flight[] flights=new Flight[MaxFlights];private int flightCount,flightId;
        private readonly CyberImpact[] impacts=new CyberImpact[MaxImpacts];private int impactCount,shown;private float clock;
        public int CyberShots=>shown;public int CyberImpacts=>impactCount;public CyberImpact CyberImpactAt(int i)=>impacts[i];
        private void AddImpact(int owner,int kind,Vector3 at,Vector3 normal,float time,bool pilot)
        {if(impactCount>=MaxImpacts){Array.Copy(impacts,1,impacts,0,MaxImpacts-1);impactCount--;}impacts[impactCount++]=new CyberImpact{Owner=owner,Slot=CyberRangedSpec.Bolt,Kind=kind,Position=at,Normal=normal,At=time,Pilot=pilot};}
        /// <summary>The fireballs alive at `time` (a rewound clock starts over).</summary>
        public void Advance(float time)
        {
            if(time<clock-.25f){flightCount=0;impactCount=0;}clock=time;
            for(int i=flightCount-1;i>=0;i--)if(time>=flights[i].EndAt+.05f||time<flights[i].At-.25f){Array.Copy(flights,i+1,flights,i,flightCount-i-1);flightCount--;}
            for(int i=impactCount-1;i>=0;i--)if(time>impacts[i].At+2||time<impacts[i].At-.25f){Array.Copy(impacts,i+1,impacts,i,impactCount-i-1);impactCount--;}
            shown=0;for(int i=0;i<flightCount;i++)if(time>=flights[i].At&&time<flights[i].EndAt)shown++;
        }
        public CyberShot CyberShotAt(int i)
        {
            int k=-1;
            for(int j=0;j<flightCount;j++)if(clock>=flights[j].At&&clock<flights[j].EndAt&&++k==i)
            {var b=flights[j];return new CyberShot{Id=b.Id,Owner=b.Owner,Slot=CyberRangedSpec.Bolt,Kind=b.Kind,Position=b.From+b.Dir*(CyberKit2Spec.PlasmaSpeed*(clock-b.At)),Velocity=b.Dir*CyberKit2Spec.PlasmaSpeed,Born=b.At};}
            return default;
        }

        // ---- the writers (the proposed host's casts, attack rate and cooldown scale 1) ----
        private static void Window(CyberKit2RangedRecord r,int slot,float at)
        {
            int s=CyberKit2Ranged.Spec(slot);r.At[slot]=at;r.Release[slot]=at+CyberKit2Spec.Windup[s];r.Until[slot]=at+CyberKit2Spec.Seconds[s];r.CutAt[slot]=-100;
            r.Ready[slot]=slot==CyberKit2Ranged.Plasma?r.Until[slot]:at+CyberKit2Spec.Cooldowns[s];
        }
        /// <summary>A Plasma Byte throw at `time` from `from` along `dir` (unit), ending `end` m out (a pilot when `pilot`, the world
        /// short of the range); the chain counts on while the previous throw ended within ComboReset. Returns the throw's index.</summary>
        public int Plasma(int actor,float time,Vector3 from,Vector3 dir,float end=CyberKit2Spec.PlasmaRange,bool pilot=false)
        {
            var r=For(actor);bool chained=r.PlasmaShot>=0&&r.At[CyberKit2Ranged.Plasma]>-90&&time-r.Until[CyberKit2Ranged.Plasma]<=CyberKit2Spec.ComboReset&&time>=r.At[CyberKit2Ranged.Plasma];
            r.PlasmaShot=chained?r.PlasmaShot+1:0;Window(r,CyberKit2Ranged.Plasma,time);float rel=r.Release[CyberKit2Ranged.Plasma];int kind=CyberKit2Spec.Overflow(r.PlasmaShot)?2:1;
            end=MeleeMath.Clamp(end,.2f,CyberKit2Spec.PlasmaRange);float fly=end/CyberKit2Spec.PlasmaSpeed;
            if(flightCount>=MaxFlights){Array.Copy(flights,1,flights,0,MaxFlights-1);flightCount--;}
            flights[flightCount++]=new Flight{Id=++flightId,Owner=actor,Kind=kind,From=from,Dir=dir,At=rel,EndAt=rel+fly};
            if(end<CyberKit2Spec.PlasmaRange-1e-3f||pilot)AddImpact(actor,kind,from+dir*end,-dir,rel+fly,pilot);
            return r.PlasmaShot;
        }
        /// <summary>Daisy Chain at `time`: the casting hand `from`, the struck pilots in order (ids, their body centres; up to six).</summary>
        public void Chain(int actor,float time,Vector3 from,int[] ids,Vector3[] points,int count)
        {
            var r=For(actor);Window(r,CyberKit2Ranged.Chain,time);r.ChainFrom=V(from);r.ChainCount=Math.Min(count,CyberKit2Ranged.MaxChain);
            for(int i=0;i<CyberKit2Ranged.MaxChain;i++){r.ChainIds[i]=i<r.ChainCount&&ids!=null?ids[i]:0;r.ChainPoints[i]=i<r.ChainCount?V(points[i]):default;}
        }
        /// <summary>Singularity at `time`: the orb from `from` stops at `stop` (a floor point lifts the hole by HoleLift).</summary>
        public void Singularity(int actor,float time,Vector3 from,Vector3 stop,bool floor=true)
        {
            var r=For(actor);Window(r,CyberKit2Ranged.Singularity,time);var centre=floor?stop+Vector3.UnitY*CyberKit2Ranged.HoleLift:stop;
            r.OrbFrom=V(from);r.OrbPoint=V(centre);r.OrbLand=r.Release[CyberKit2Ranged.Singularity]+Vector3.Distance(from,stop)/CyberKit2Spec.OrbSpeed;r.Implode=r.OrbLand+CyberKit2Spec.HoleSeconds;
        }
        /// <summary>Packet Storm at `time` on the floor point `centre` with `seed`.</summary>
        public void Storm(int actor,float time,Vector3 centre,int seed)
        {var r=For(actor);Window(r,CyberKit2Ranged.Storm,time);r.StormCentre=V(centre);r.StormSeed=seed;}
        /// <summary>Orbital Uplink called at `time` on the floor point `centre`: the column starts there at Release.</summary>
        public void Orbital(int actor,float time,Vector3 centre)
        {
            var r=For(actor);Window(r,CyberKit2Ranged.Orbital,time);r.OrbitalCentre=r.OrbitalPoint=r.OrbitalAim=V(centre);r.OrbitalPointAt=r.Release[CyberKit2Ranged.Orbital];
        }
        /// <summary>A host tick of the column at `time`: the point chases the last aim (OrbitalStep), then the aim is `aim` (its floor
        /// point, kept within OrbitalRange of the caster at `caster`).</summary>
        public void Steer(int actor,float time,Vector3 aim,Vector3 caster)
        {
            var r=Of(actor);if(r==null||!(r.OrbitalPointAt>-90))return;int o=CyberKit2Ranged.Orbital;if(time<r.Release[o]||time>=r.Until[o])return;
            float x=r.OrbitalPoint.x,z=r.OrbitalPoint.z;CyberKit2Spec.OrbitalStep(ref x,ref z,r.OrbitalAim.x,r.OrbitalAim.z,time-r.OrbitalPointAt);r.OrbitalPoint=new V3(x,aim.Y,z);
            var d=new Vector3(aim.X-caster.X,0,aim.Z-caster.Z);float l=d.Length();if(l>CyberKit2Spec.OrbitalRange)aim=new Vector3(caster.X,aim.Y,caster.Z)+d*(CyberKit2Spec.OrbitalRange/l);
            r.OrbitalAim=V(aim);r.OrbitalPointAt=time;
        }
        /// <summary>An interrupt at `time` (a block, roll or stun): a live cast ends there; one cut before its release never fires.</summary>
        public void Interrupt(int actor,float time)
        {
            var r=Of(actor);if(r==null)return;
            for(int s=0;s<CyberKit2Ranged.Slots;s++)if(r.At[s]>-90&&time>=r.At[s]&&time<r.Until[s])
            {
                bool before=time<r.Release[s];r.Until[s]=time;r.CutAt[s]=time;if(!before)continue;r.Release[s]=time;
                if(s==CyberKit2Ranged.Singularity)r.OrbLand=r.Implode=-100;else if(s==CyberKit2Ranged.Chain)r.ChainCount=0;else if(s==CyberKit2Ranged.Orbital)r.OrbitalPointAt=-100;
            }
        }
        private static V3 V(Vector3 p)=>new V3(p.X,p.Y,p.Z);
    }
}
