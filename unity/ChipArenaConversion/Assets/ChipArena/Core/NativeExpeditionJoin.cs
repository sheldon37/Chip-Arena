using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Join circle presentation (Claude, 2026-10-17; docs/EXPEDITION-HUB.md "Join circle presentation"). Engine-free: what each hub
    // join circle shows, where the party list places each member, the deploy and return notes and the sound cue events, read from
    // the public ExpeditionView only: Astra's joins[] (doorId, area, x/y/z/r/h, joinInside, joinStartedAt, duration, the deploy
    // record deployedAt/deployedAccounts, closedReason), members[].area/pendingArea/atJoin/seat, and instances[].cleared only for
    // a snapshot without h or closedReason. Where each member's pilot stands this frame (the caller's arena) only says where to
    // draw a member's marker: who deploys is the authority's atJoin, joinInside and deploy record, never a position read here.
    // Nothing decides a rule, writes the view or touches collision. Allocation-free once warm (labels cached, fixed arrays).

    /// <summary>A circle's state as drawn: at rest, charging toward deploy, the deploy itself, powering down after it emptied,
    /// held at expiry (the authority refused the transfer and restarts the count), or closed (closedReason: cleared, its area is
    /// done; locked; or its route reserved). Only an open circle counts, so a closed one never charges.</summary>
    public enum ExpeditionJoinPhase { Idle, Charging, Deploying, Cancelled, Held, Cleared, Locked, Reserved }
    /// <summary>The local pilot against its nearest circle: away, near (a prompt), counted (deploying with the circle), standing in
    /// it but not counted (the reason says why), at a cleared circle, or at a locked or reserved one.</summary>
    public enum ExpeditionJoinViewer { Away, Near, Deploying, NotCounted, Cleared, Closed }
    public enum ExpeditionJoinCueKind { Charge, Tick, Cancel, Deploy, Held, Arrive }

    /// <summary>A deploy zone as the hub names and colours it: the area id, its name, a short name for tight signs, what it
    /// threatens (presentation copy, not a rule) and its accent (sRGB).</summary>
    public struct ExpeditionJoinZone { public string Area,Name,Short,Threat;public int Accent; }

    /// <summary>The zones the hub deploys to. One shared look, one accent each: Ward Zero's emergency red (its canopy sign),
    /// Central Station's teal (its facade lettering), City Ironworks' molten amber. Unknown areas read as the manifest names them.</summary>
    public static class NativeExpeditionJoinZones
    {
        public const string Hub="HUB";
        /// <summary>The hub's chip in the party list: the sanctuary's ward green.</summary>
        public const int HubHex=0x8AE89A,FallbackHex=0x22E6FF;
        public const string WardZero="exp-ward-zero",Station="exp-transit",Ironworks="exp-foundry";
        private static readonly ExpeditionJoinZone[] Known=
        {
            new ExpeditionJoinZone{Area=WardZero,Name="WARD ZERO",Short="WARD ZERO",Threat="QUARANTINE BREACH",Accent=0xFF4A3A},
            new ExpeditionJoinZone{Area=Station,Name="CENTRAL STATION",Short="STATION",Threat="LIVE RAILS / FLOODED LINES",Accent=0x7AE8D0},
            new ExpeditionJoinZone{Area=Ironworks,Name="CITY IRONWORKS",Short="IRONWORKS",Threat="MOLTEN METAL / FURNACE HEAT",Accent=0xFFA040},
        };
        private static readonly Dictionary<string,ExpeditionJoinZone> other=new Dictionary<string,ExpeditionJoinZone>(StringComparer.Ordinal);
        /// <summary>The zone of an area id (an unknown one: its manifest name, the HUD cyan; cached).</summary>
        public static ExpeditionJoinZone Of(string area)
        {
            for(int i=0;i<Known.Length;i++)if(Known[i].Area==area)return Known[i];
            if(string.IsNullOrEmpty(area))return new ExpeditionJoinZone{Area=area,Name="",Short="",Threat="",Accent=FallbackHex};
            if(!other.TryGetValue(area,out var z)){if(other.Count>64)other.Clear();string n=NativeExpeditionPresentation.Name(area);z=other[area]=new ExpeditionJoinZone{Area=area,Name=n,Short=n,Threat="",Accent=FallbackHex};}
            return z;
        }
        /// <summary>The display name of a known zone, else null (NativeExpeditionPresentation.Name uses it for its ids).</summary>
        public static string KnownName(string area){for(int i=0;i<Known.Length;i++)if(Known[i].Area==area)return Known[i].Name;return null;}
        /// <summary>Where a member is, as the party list says it: HUB for the world area, else the zone's name; "—" when unknown.</summary>
        public static string Place(string area,string world)=>string.IsNullOrEmpty(area)?"—":area==world?Hub:Of(area).Name;
        public static int PlaceAccent(string area,string world)=>string.IsNullOrEmpty(area)?FallbackHex:area==world?HubHex:Of(area).Accent;
    }

    /// <summary>Where a member's pilot stands in the local scene this frame (its feet), from the caller's arena. False when that
    /// pilot is not in this scene (another area, unbound seat, down and gone).</summary>
    public interface IExpeditionJoinPilots
    {
        bool Feet(string account,int seat,out float x,out float y,out float z);
    }

    /// <summary>One hub join circle this frame (NativeExpeditionJoinModel.Build).</summary>
    public struct ExpeditionJoinRing
    {
        public string Door,Area;public ExpeditionJoinZone Zone;
        /// <summary>The circle (its floor centre, radius and counted height: joins[].h, NativeExpeditionJoinModel.FallbackHeight
        /// for a snapshot without it), the unit direction from it toward its door, and toward where the party comes from (the
        /// hub's centre).</summary>
        public float X,Y,Z,R,H,Dx,Dz,Ax,Az;
        public ExpeditionJoinPhase Phase;
        /// <summary>Why it does not count (joins[].closedReason; Open counts).</summary>
        public ExpeditionJoinClosedReason Closure;
        /// <summary>The countdown: its fill (0..1 over the authority's duration), seconds left and the whole seconds shown.</summary>
        public float Charge,Left;public int Seconds;
        /// <summary>The authority's count of pilots in it (joinInside), and the members (bits over the party rows) the view says
        /// are counted in it; at a deploy, the members it took (its deployedAccounts).</summary>
        public int Inside,Pilots,Taken;
        /// <summary>Seconds in a timed phase (deploy, cancel, hold); the charge it held when it emptied or deployed; a refused
        /// deploy's amber flicker (1 at the refusal, 0 after HeldSeconds), shown over a restarted count too.</summary>
        public float Age,Was,Hold;
        /// <summary>How lit the pad is (0..1): idle .4, rising with the charge, full at a deploy, dim when cleared.</summary>
        public float Power;
        /// <summary>Cleared: closed because its area is done. Entered: the party has been in its area.</summary>
        public bool Cleared,Entered,Mine;
        /// <summary>The local pilot's horizontal distance from the centre (metres; +inf without a pilot here).</summary>
        public float Distance;
        public string Title,Status,Detail,Names;public ExpeditionTone Tone;
    }
    /// <summary>One party member's line: who, where (HUB or the zone: where they physically are), what they are doing, and their
    /// pilot here this frame. Loading: the member's transfer is loading (members[].pendingArea, Pending its area and PendingAccent
    /// its accent); Place stays their source until the authority commits it.</summary>
    public struct ExpeditionPartyRow
    {
        public string Account,Name,Place,Status,Pending;public int Accent,PendingAccent,Seat,Ring;public ExpeditionTone Tone;
        public bool You,Host,Hub,Deploying,EnRoute,Loading,Down,Offline,Extracted,Placed;
        /// <summary>Seconds since this member came back to the hub (-1 none recent), and since they deployed (-1).</summary>
        public float Returned,Deployed;public float X,Y,Z;
    }
    public struct ExpeditionJoinToast { public string Text;public int Accent;public float Age; }
    public struct ExpeditionJoinCue { public ExpeditionJoinCueKind Kind;public int Ring;public float X,Y,Z; }

    /// <summary>
    /// The join circles, the party list and their notes from one view (one instance per screen, single thread). The view is the
    /// truth: a circle charges while joins[].joinStartedAt is set with pilots in it. It deployed when its deploy record changes
    /// (joins[].deployedAt, the authority's acceptance of the transfer) and took exactly its deployedAccounts; an empty circle at
    /// the deadline is never read as a deploy. The first record seen is a baseline: a pilot who reaches the hub late (a join, a
    /// return from a zone, a cancelled load) sees no old deployment, and a record already DeploySeconds old on the view's clock is
    /// adopted the same way. A count that stops without a new record was cancelled when it emptied, or held when it is still
    /// occupied after expiry (the authority refused and restarts the count). A closed circle (closedReason) shows its reason and
    /// never counts. A member's members[].pendingArea is their transfer loading (LOADING · the zone) until the authority commits
    /// it and their area changes.
    /// </summary>
    public sealed class NativeExpeditionJoinModel
    {
        public const int MaxRings=8,MaxMembers=8,MaxToasts=4,MaxCues=16;
        /// <summary>Within this far of a circle's edge (m) the HUD prompts; the arrival and pad effects' timings (s).</summary>
        public const float NearMetres=7,CancelSeconds=.9f,DeploySeconds=1.6f,HeldSeconds=.8f,ToastSeconds=4.5f,EnRouteSeconds=15,ReturnSeconds=4,ArriveSeconds=1.4f;
        /// <summary>A circle's counted height (m) for a snapshot without joins[].h (the column presentation assumed before it).</summary>
        public const float FallbackHeight=4.5f;
        /// <summary>My screen whitens over the last FlashLead seconds of my countdown, so the loading screen's cut lands on light.</summary>
        public const float FlashLead=.35f,FlashOut=.25f;
        public const string Down="YOU ARE DOWN",Picking="FINISH YOUR CHIP PICK",Carrying="PUT DOWN WHO YOU CARRY",Loading="LOADING",Extracted="YOU HAVE EXTRACTED",Waiting="WAITING FOR THE HOST";
        /// <summary>The closed circles' words, and a loading member's status prefix (then the zone's name).</summary>
        public const string LockedWord="LOCKED",ReservedWord="ROUTE RESERVED",NoDeploy="NO DEPLOY",LoadingInto="LOADING · ";
        public readonly ExpeditionJoinRing[] Rings=new ExpeditionJoinRing[MaxRings];public int RingCount;
        public readonly ExpeditionPartyRow[] Party=new ExpeditionPartyRow[MaxMembers];public int PartyCount;
        public readonly ExpeditionJoinToast[] Toasts=new ExpeditionJoinToast[MaxToasts];public int ToastCount;
        public readonly ExpeditionJoinCue[] Cues=new ExpeditionJoinCue[MaxCues];public int CueCount;
        public ExpeditionJoinViewer Viewer;public int ViewerRing=-1,ViewerRow=-1;public string ViewerReason;
        /// <summary>The viewer stands in the hub with its circles in the view; the party spans more than one member.</summary>
        public bool InHub;
        /// <summary>My deploy flash this frame (0..1).</summary>
        public float Flash;
        public string World;

        private struct RingMemory{public string Door;public double Started,EventAt,DeployedAt;public int Inside,Seconds,EventMask;public ExpeditionJoinPhase Event;public float Was,Charge;}
        private struct MemberMemory{public string Account,Area,To,From,Pending;public double DeployedAt,ReturnedAt;public bool Used;}
        private readonly RingMemory[] rings=new RingMemory[MaxRings];private int ringMemory;
        private readonly MemberMemory[] members=new MemberMemory[MaxMembers];
        private readonly ExpeditionJoinToast[] toastAt=new ExpeditionJoinToast[MaxToasts];private readonly double[] toastTime=new double[MaxToasts];private int toastCount;
        private string run;private ExpeditionView last;private double lastElapsed=double.NaN;private long lastRevision=long.MinValue;

        public void Reset()
        {
            RingCount=PartyCount=ToastCount=CueCount=0;ringMemory=0;toastCount=0;run=null;last=null;lastElapsed=double.NaN;lastRevision=long.MinValue;
            for(int i=0;i<members.Length;i++)members[i]=default;Viewer=ExpeditionJoinViewer.Away;ViewerRing=ViewerRow=-1;ViewerReason=null;InHub=false;Flash=0;World=null;
        }

        /// <summary>Rebuilds the circles, the party rows, the viewer's relation, the notes and this frame's cues. `t` is the
        /// presentation clock (the authority's elapsed run forward between views, ExpeditionPresentationClock). Allocation-free once
        /// each label has been seen.</summary>
        public void Build(ExpeditionView v,double t,IExpeditionJoinPilots pilots,ExpeditionLabels labels)
        {
            CueCount=0;RingCount=0;PartyCount=0;Viewer=ExpeditionJoinViewer.Away;ViewerRing=ViewerRow=-1;ViewerReason=null;InHub=false;Flash=0;
            if(v==null||labels==null){ToastCount=0;return;}
            if(v.run!=run){Reset();run=v.run;}
            World=v.world;bool changed=!ReferenceEquals(v,last)||!v.elapsed.Equals(lastElapsed)||v.revision!=lastRevision;last=v;lastElapsed=v.elapsed;lastRevision=v.revision;
            Members(v,t,pilots,labels,changed);
            var joins=v.joins;bool hub=v.area!=null&&v.area==v.world;
            if(hub&&joins!=null)
            {
                for(int i=0;i<joins.Length&&RingCount<MaxRings;i++)if(joins[i]?.doorId!=null){source[RingCount]=joins[i];Rings[RingCount]=Place(v,joins[i],RingCount);RingCount++;}
                Unplaced(v);
                for(int i=0;i<RingCount;i++)Ring(v,source[i],i,t,labels,changed);
            }
            InHub=hub&&RingCount>0;
            // Out of the hub (or its circles out of the view) the circles are forgotten: back in it, each deploy record is a fresh
            // baseline, so a deploy that happened while this pilot was away or loading is never replayed.
            if(!InHub)ringMemory=0;
            ViewerState(v,t,labels);
            Age(t);
        }

        // ---- the party ---------------------------------------------------------------------------------------------------------
        private void Members(ExpeditionView v,double t,IExpeditionJoinPilots pilots,ExpeditionLabels labels,bool changed)
        {
            for(int i=0;i<v.members.Length&&PartyCount<MaxMembers;i++)
            {
                var m=v.members[i];if(m?.account==null)continue;int slot=MemberSlot(m.account);ref var mm=ref members[slot];
                string name=labels.Who(string.IsNullOrEmpty(m.name)?m.account:m.name);bool you=m.account==v.viewer;string pending=string.IsNullOrEmpty(m.pendingArea)?null:m.pendingArea;
                if(changed)
                {
                    if(mm.Area!=null&&m.area!=null&&mm.Area!=m.area)
                    {
                        // Came back to the hub, or arrived in a zone (the deploy note already said so for a deploy this client saw).
                        if(m.area==v.world){mm.ReturnedAt=t;mm.From=mm.Area;mm.To=null;mm.DeployedAt=double.NaN;if(!you)Toast(labels.Join(labels.Join(name," RETURNED FROM "),NativeExpeditionJoinZones.Of(mm.Area).Name),NativeExpeditionJoinZones.HubHex,t);}
                        else{if(!you&&mm.To!=m.area)Toast(labels.Join(labels.Join(name," ENTERED "),NativeExpeditionJoinZones.Of(m.area).Name),NativeExpeditionJoinZones.Of(m.area).Accent,t);mm.To=null;mm.DeployedAt=double.NaN;}
                    }
                    // Their load ended without a move (the transfer was cancelled): no longer on the way.
                    else if(mm.To!=null&&mm.Pending!=null&&pending==null){mm.To=null;mm.DeployedAt=double.NaN;}
                    mm.Pending=pending;
                }
                if(m.area!=null)mm.Area=m.area;
                if(mm.To!=null&&!(t-mm.DeployedAt<EnRouteSeconds)){mm.To=null;mm.DeployedAt=double.NaN;}
                var row=new ExpeditionPartyRow{Account=m.account,Name=name,Seat=m.seat,You=you,Host=m.host,Hub=m.area==v.world,Down=!m.alive,Offline=!m.connected,Extracted=m.extracted,Ring=-1,Returned=-1,Deployed=-1,
                    Place=NativeExpeditionJoinZones.Place(m.area,v.world),Accent=NativeExpeditionJoinZones.PlaceAccent(m.area,v.world)};
                if(pending!=null){var zone=NativeExpeditionJoinZones.Of(pending);row.Loading=true;row.Pending=pending;row.PendingAccent=zone.Accent;}
                if(m.area!=null&&m.area==v.area&&pilots!=null&&pilots.Feet(m.account,m.seat,out float x,out float y,out float z)&&Finite(x)&&Finite(y)&&Finite(z)){row.Placed=true;row.X=x;row.Y=y;row.Z=z;}
                if(mm.To!=null){row.EnRoute=true;row.Deployed=(float)(t-mm.DeployedAt);}
                if(t-mm.ReturnedAt<ReturnSeconds&&t>=mm.ReturnedAt)row.Returned=(float)(t-mm.ReturnedAt);
                Party[PartyCount++]=row;
                // Arrival cue where the returning pilot stands (once, the first frame it is placed after the return).
                if(row.Placed&&row.Returned>=0&&row.Returned<ArriveSeconds&&changed&&mm.From!=null){Cue(ExpeditionJoinCueKind.Arrive,-1,row.X,row.Y,row.Z);mm.From=null;}
            }
            // Party rows' status after the rings (deploying needs their counts): set in Status().
        }
        private int MemberSlot(string account)
        {
            int free=-1;for(int i=0;i<members.Length;i++){if(members[i].Used&&members[i].Account==account)return i;if(!members[i].Used&&free<0)free=i;}
            if(free<0){free=0;for(int i=1;i<members.Length;i++)if(members[i].ReturnedAt<members[free].ReturnedAt)free=i;}
            members[free]=new MemberMemory{Account=account,Used=true,DeployedAt=double.NaN,ReturnedAt=double.NegativeInfinity};return free;
        }

        // ---- the circles -------------------------------------------------------------------------------------------------------
        private readonly ExpeditionJoinView[] source=new ExpeditionJoinView[MaxRings];
        /// <summary>A circle's geometry, zone and closure, and the members the view counts (atJoin) whose pilot stands in it (an
        /// open circle only: a closed one counts nobody).</summary>
        private ExpeditionJoinRing Place(ExpeditionView v,ExpeditionJoinView j,int index)
        {
            var zone=NativeExpeditionJoinZones.Of(j.area);bool exact=j.h>0&&!float.IsInfinity(j.h);
            var r=new ExpeditionJoinRing{Door=j.doorId,Area=j.area,Zone=zone,X=j.x,Y=j.y,Z=j.z,R=j.r>0?j.r:2.5f,H=exact?j.h:FallbackHeight,Inside=Math.Max(0,j.joinInside),Distance=float.PositiveInfinity,Tone=ExpeditionTone.Cyan};
            // Toward the door (its anchor), and toward the hub's centre (every route leaves the sanctuary toward its circle).
            float dx=-r.X,dz=-r.Z;foreach(var a in v.anchors)if(a!=null&&a.id==j.doorId){dx=a.x-r.X;dz=a.z-r.Z;break;}
            Unit(ref dx,ref dz,r.X,r.Z);r.Dx=dx;r.Dz=dz;float ax=-r.X,az=-r.Z;Unit(ref ax,ref az,-dx,-dz);r.Ax=ax;r.Az=az;
            bool cleared=false;foreach(var b in v.instances)if(b!=null&&b.id==j.area){cleared=b.cleared;r.Entered=b.entered;break;}
            // The closure is the authority's closedReason; a snapshot without the join fields (no h) still closes a cleared area's
            // circle from instances[].cleared, as before them. An unknown reason does not count either: it reads as locked.
            var closure=j.closedReason;if(closure==ExpeditionJoinClosedReason.Open&&!exact&&cleared)closure=ExpeditionJoinClosedReason.Cleared;
            r.Closure=closure;r.Cleared=closure==ExpeditionJoinClosedReason.Cleared;
            r.Phase=closure==ExpeditionJoinClosedReason.Open?ExpeditionJoinPhase.Idle:closure==ExpeditionJoinClosedReason.Cleared?ExpeditionJoinPhase.Cleared:closure==ExpeditionJoinClosedReason.Reserved?ExpeditionJoinPhase.Reserved:ExpeditionJoinPhase.Locked;
            if(Closed(r.Phase)){r.Inside=0;return r;}
            for(int i=0;i<PartyCount;i++)
            {
                ref var row=ref Party[i];if(row.Ring>=0||!row.Placed)continue;var m=v.Member(row.Account);if(m==null||!m.atJoin)continue;
                if(Within(r,row.X,row.Y,row.Z,.35f)){r.Pilots|=1<<i;row.Ring=index;}
            }
            return r;
        }
        /// <summary>A counted member whose pilot is not placed in any circle (not in this scene yet, or lagging): the only
        /// occupied circle counts them.</summary>
        private void Unplaced(ExpeditionView v)
        {
            int only=-1,n=0;for(int i=0;i<RingCount;i++)if(!Closed(Rings[i].Phase)&&source[i].joinInside>0&&source[i].joinStartedAt>=0){only=i;n++;}if(n!=1)return;
            for(int i=0;i<PartyCount;i++){ref var row=ref Party[i];if(row.Ring>=0)continue;var m=v.Member(row.Account);if(m==null||!m.atJoin)continue;Rings[only].Pilots|=1<<i;row.Ring=only;}
        }
        private void Ring(ExpeditionView v,ExpeditionJoinView j,int index,double t,ExpeditionLabels labels,bool changed)
        {
            var r=Rings[index];double duration=j.duration>0?j.duration:ExpeditionJoinRules.Duration;
            bool closed=Closed(r.Phase);double started=closed?-1:j.joinStartedAt;bool charging=started>=0&&r.Inside>0;
            ref var mem=ref Memory(j.doorId,started,r.Inside,j.deployedAt);
            if(changed)
            {
                // A deploy is a new record: the authority accepted the transfer at deployedAt and took deployedAccounts. A reset
                // (deployedAt back to -1) and a record already old on the view's clock (a pilot back from a menu) are adopted.
                bool record=!j.deployedAt.Equals(mem.DeployedAt);
                if(record){if(j.deployedAt>=0&&v.elapsed-j.deployedAt<=DeploySeconds)Event(ref mem,ExpeditionJoinPhase.Deploying,t,ref r,index,v,labels,j.deployedAccounts);}
                else if(mem.Started>=0&&(started<0||started!=mem.Started))
                {
                    // The count stopped without a deploy: still occupied after its expiry, the authority refused and restarts it;
                    // otherwise it emptied (a step-out at the deadline too).
                    bool expired=v.elapsed>=mem.Started+duration-1e-6;
                    if(r.Inside>0&&expired)Event(ref mem,ExpeditionJoinPhase.Held,t,ref r,index,v,labels,null);
                    else if(mem.Inside>0)Event(ref mem,ExpeditionJoinPhase.Cancelled,t,ref r,index,v,labels,null);
                }
                if(charging&&(mem.Started<0||started!=mem.Started)){Cue(ExpeditionJoinCueKind.Charge,index,r.X,r.Y,r.Z);mem.Seconds=int.MaxValue;}
                mem.Started=started;mem.Inside=r.Inside;mem.DeployedAt=j.deployedAt;
            }
            double since=t-mem.EventAt;if(!closed&&mem.Event==ExpeditionJoinPhase.Held&&since>=0&&since<HeldSeconds)r.Hold=1-(float)(since/HeldSeconds);
            if(closed){r.Power=.3f;mem.Seconds=int.MaxValue;}
            else if(charging)
            {
                r.Phase=ExpeditionJoinPhase.Charging;r.Charge=Clamp01((float)((t-started)/duration));r.Left=(float)Math.Max(0,started+duration-t);r.Seconds=Math.Max(1,(int)Math.Ceiling(r.Left-1e-6));
                if(r.Seconds<mem.Seconds&&mem.Seconds!=int.MaxValue)Cue(ExpeditionJoinCueKind.Tick,index,r.X,r.Y,r.Z);mem.Seconds=r.Seconds;mem.Charge=r.Charge;
                r.Power=.4f+.6f*r.Charge;
            }
            else
            {
                double age=t-mem.EventAt;r.Age=(float)Math.Max(0,age);r.Was=mem.Was;r.Taken=mem.Event==ExpeditionJoinPhase.Deploying?mem.EventMask:0;
                if(mem.Event==ExpeditionJoinPhase.Deploying&&age>=0&&age<DeploySeconds){r.Phase=ExpeditionJoinPhase.Deploying;r.Power=1-.6f*Smooth(r.Age/DeploySeconds);}
                else if(mem.Event==ExpeditionJoinPhase.Held&&age>=0&&age<HeldSeconds){r.Phase=ExpeditionJoinPhase.Held;r.Power=.6f;}
                else if(mem.Event==ExpeditionJoinPhase.Cancelled&&age>=0&&age<CancelSeconds){r.Phase=ExpeditionJoinPhase.Cancelled;r.Power=.4f+.6f*r.Was*(1-Smooth(r.Age/CancelSeconds));}
                else{r.Phase=ExpeditionJoinPhase.Idle;r.Power=.4f;}
                mem.Seconds=int.MaxValue;
            }
            Words(ref r,v,labels);
            Rings[index]=r;
        }
        private ref RingMemory Memory(string door,double started,int inside,double deployedAt)
        {
            for(int i=0;i<ringMemory;i++)if(rings[i].Door==door)return ref rings[i];
            int k=ringMemory<rings.Length?ringMemory++:rings.Length-1;
            // First sight: no transition from nothing (a late joiner sees a running count, never a deploy or a cancel); its deploy
            // record is the baseline the next records are compared with.
            rings[k]=new RingMemory{Door=door,Started=started,Inside=inside,DeployedAt=deployedAt,Seconds=int.MaxValue,EventAt=double.NegativeInfinity,Event=ExpeditionJoinPhase.Idle};return ref rings[k];
        }
        /// <summary>The party rows (bits) of a deploy record's accounts.</summary>
        private int Mask(string[] accounts)
        {
            int mask=0;if(accounts==null)return 0;
            for(int k=0;k<accounts.Length;k++)for(int i=0;i<PartyCount;i++)if(Party[i].Account==accounts[k]){mask|=1<<i;break;}
            return mask;
        }
        private void Event(ref RingMemory mem,ExpeditionJoinPhase kind,double t,ref ExpeditionJoinRing r,int index,ExpeditionView v,ExpeditionLabels labels,string[] took)
        {
            mem.Event=kind;mem.EventAt=t;mem.Was=mem.Charge;mem.EventMask=kind==ExpeditionJoinPhase.Deploying?Mask(took):0;
            if(kind==ExpeditionJoinPhase.Deploying)
            {
                Cue(ExpeditionJoinCueKind.Deploy,index,r.X,r.Y,r.Z);mem.Was=1;
                // Each member it took is on the way until their area changes (or the note lapses).
                for(int i=0;i<PartyCount;i++)if((mem.EventMask&1<<i)!=0){int slot=MemberSlot(Party[i].Account);members[slot].To=r.Area;members[slot].DeployedAt=t;Party[i].EnRoute=true;Party[i].Deployed=0;}
                string names=Names(mem.EventMask,v,labels,true);
                if(names!=null)Toast(labels.Join(labels.Join(names," DEPLOYED TO "),r.Zone.Name),r.Zone.Accent,t);
            }
            else if(kind==ExpeditionJoinPhase.Held)Cue(ExpeditionJoinCueKind.Held,index,r.X,r.Y,r.Z);
            else Cue(ExpeditionJoinCueKind.Cancel,index,r.X,r.Y,r.Z);
        }
        /// <summary>The members of a mask as the circle names them (YOU first), joined by " · "; null for none. `others`: leave me out.</summary>
        private string Names(int mask,ExpeditionView v,ExpeditionLabels labels,bool others)
        {
            string s=null;
            for(int pass=0;pass<2;pass++)for(int i=0;i<PartyCount;i++)
            {
                if((mask&1<<i)==0||Party[i].You!=(pass==0))continue;if(Party[i].You&&others)continue;
                string n=Party[i].You?"YOU":Party[i].Name;s=s==null?n:labels.Join(labels.Join(s," · "),n);
            }
            return s;
        }
        private void Words(ref ExpeditionJoinRing r,ExpeditionView v,ExpeditionLabels labels)
        {
            r.Title=r.Zone.Name;r.Names=Names(r.Phase==ExpeditionJoinPhase.Deploying?r.Taken:r.Pilots,v,labels,false);
            switch(r.Phase)
            {
                case ExpeditionJoinPhase.Charging:r.Status=labels.Int("DEPLOYING IN ",r.Seconds);r.Detail=r.Names??labels.Int("PILOTS IN CIRCLE · ",r.Inside);r.Tone=ExpeditionTone.Good;break;
                case ExpeditionJoinPhase.Deploying:r.Status="DEPLOYING";r.Detail=r.Names??"";r.Tone=ExpeditionTone.Good;break;
                case ExpeditionJoinPhase.Cancelled:r.Status="CANCELLED";r.Detail="THE CIRCLE EMPTIED";r.Tone=ExpeditionTone.Warn;break;
                case ExpeditionJoinPhase.Held:r.Status="HOLDING · RETRYING";r.Detail="AREA NOT READY YET";r.Tone=ExpeditionTone.Warn;break;
                case ExpeditionJoinPhase.Cleared:r.Status="CLEARED";r.Detail="BOSS DOWN · NO DEPLOY";r.Tone=ExpeditionTone.Good;break;
                case ExpeditionJoinPhase.Locked:r.Status=LockedWord;r.Detail=NoDeploy;r.Tone=ExpeditionTone.Danger;break;
                case ExpeditionJoinPhase.Reserved:r.Status=ReservedWord;r.Detail=NoDeploy;r.Tone=ExpeditionTone.Warn;break;
                default:r.Status="STEP IN TO DEPLOY";r.Detail=r.Zone.Threat;r.Tone=ExpeditionTone.Cyan;break;
            }
        }

        // ---- the viewer --------------------------------------------------------------------------------------------------------
        private void ViewerState(ExpeditionView v,double t,ExpeditionLabels labels)
        {
            int me=-1;for(int i=0;i<PartyCount;i++)if(Party[i].You){me=i;break;}ViewerRow=me;
            for(int i=0;i<PartyCount;i++)Status(ref Party[i],v,labels);
            if(me<0||!InHub)return;var row=Party[me];
            float best=float.PositiveInfinity;
            for(int i=0;i<RingCount;i++)
            {
                ref var r=ref Rings[i];if(row.Placed){float dx=row.X-r.X,dz=row.Z-r.Z;r.Distance=(float)Math.Sqrt(dx*dx+dz*dz);}
                r.Mine=(r.Pilots&1<<me)!=0;
                // My circle: the one counting me, or the one whose deploy took me.
                if(r.Mine||r.Phase==ExpeditionJoinPhase.Deploying&&(r.Taken&1<<me)!=0){ViewerRing=i;best=-1;}else if(best>=0&&r.Distance<best){best=r.Distance;if(r.Distance<=r.R+NearMetres)ViewerRing=i;}
            }
            // Counted without a placed pilot (a guest's own pilot lagging the authority): the only charging circle is mine.
            var mine=v.Member(v.viewer);
            if(ViewerRing<0&&mine!=null&&mine.atJoin){int only=-1,n=0;for(int i=0;i<RingCount;i++)if(Rings[i].Phase==ExpeditionJoinPhase.Charging){only=i;n++;}if(n==1){ViewerRing=only;Rings[only].Mine=true;}}
            if(ViewerRing<0)return;ref var ring=ref Rings[ViewerRing];bool inside=row.Placed&&Within(ring,row.X,row.Y,row.Z,0);
            if(ring.Mine&&ring.Phase==ExpeditionJoinPhase.Charging){Viewer=ExpeditionJoinViewer.Deploying;Flash=FlashLead>0?Smooth(1-ring.Left/FlashLead):0;}
            // Taken by a deploy, or my transfer loading, and still held in the hub: no prompt, the loading screen takes over.
            else if(row.EnRoute||row.Loading){}
            else if(Closed(ring.Phase)&&(inside||ring.Distance<=ring.R+NearMetres))Viewer=ring.Phase==ExpeditionJoinPhase.Cleared?ExpeditionJoinViewer.Cleared:ExpeditionJoinViewer.Closed;
            else if(inside){Viewer=ExpeditionJoinViewer.NotCounted;ViewerReason=Reason(v);}
            else if(ring.Distance<=ring.R+NearMetres)Viewer=ExpeditionJoinViewer.Near;
            // The deploy that took me: the light falls away under the loading screen's cut.
            if(ring.Phase==ExpeditionJoinPhase.Deploying&&(ring.Taken&1<<me)!=0&&ring.Age<FlashOut)Flash=Math.Max(Flash,1-ring.Age/FlashOut);
        }
        private static string Reason(ExpeditionView v)
        {
            var me=v.mine;var m=v.Member(v.viewer);
            if(m!=null&&m.extracted||me!=null&&me.extracted)return Extracted;
            if(m!=null&&!m.alive)return Down;
            if(m!=null&&m.choosing)return Picking;
            if(me?.carrying!=null&&me.carrying.Length>0)return Carrying;
            if(v.loading||v.areaBarrier!=null&&v.areaBarrier.active)return Loading;
            return Waiting;
        }
        private void Status(ref ExpeditionPartyRow row,ExpeditionView v,ExpeditionLabels labels)
        {
            var m=v.Member(row.Account);row.Deploying=row.Ring>=0&&row.Ring<RingCount&&Rings[row.Ring].Phase==ExpeditionJoinPhase.Charging;
            if(row.Extracted){row.Status="EXTRACTED";row.Tone=ExpeditionTone.Warn;}
            else if(row.Offline){row.Status="OFFLINE";row.Tone=ExpeditionTone.Muted;}
            else if(row.Down){row.Status=m!=null&&NativeExpeditionPresentation.Has(m.carriedBy)?"CARRIED":"DOWN";row.Tone=ExpeditionTone.Danger;}
            // Their transfer is loading (pendingArea): where to, while Place still says where they are.
            else if(row.Loading){row.Status=labels.Join(LoadingInto,NativeExpeditionJoinZones.Of(row.Pending).Name);row.Tone=ExpeditionTone.Cyan;}
            else if(row.Deploying){row.Status=SecondsWord(Rings[row.Ring].Seconds);row.Tone=ExpeditionTone.Good;}
            else if(row.EnRoute){row.Status="DEPLOYING";row.Tone=ExpeditionTone.Cyan;}
            else if(row.Returned>=0){row.Status="BACK IN THE HUB";row.Tone=ExpeditionTone.Good;}
            else if(m!=null&&m.choosing){row.Status="PICKING A CHIP";row.Tone=ExpeditionTone.Magenta;}
            else if(m!=null&&m.safe){row.Status="SAFE";row.Tone=ExpeditionTone.Good;}
            else{row.Status=row.Hub?"IN THE CITY":"IN THE ZONE";row.Tone=ExpeditionTone.Muted;}
        }
        private static readonly string[] seconds={"DEPLOYING · NOW","DEPLOYING · 1s","DEPLOYING · 2s","DEPLOYING · 3s","DEPLOYING · 4s","DEPLOYING · 5s"};
        private static string SecondsWord(int s)=>seconds[Math.Max(0,Math.Min(seconds.Length-1,s))];

        // ---- notes and cues ----------------------------------------------------------------------------------------------------
        private void Toast(string text,int accent,double t)
        {
            if(text==null)return;for(int i=0;i<toastCount;i++)if(toastAt[i].Text==text&&t-toastTime[i]<1)return;
            if(toastCount==MaxToasts){for(int i=1;i<MaxToasts;i++){toastAt[i-1]=toastAt[i];toastTime[i-1]=toastTime[i];}toastCount--;}
            toastAt[toastCount]=new ExpeditionJoinToast{Text=text,Accent=accent};toastTime[toastCount]=t;toastCount++;
        }
        private void Age(double t)
        {
            int k=0;for(int i=0;i<toastCount;i++){double age=t-toastTime[i];if(age>=0&&age<ToastSeconds){toastAt[k]=toastAt[i];toastTime[k]=toastTime[i];k++;}}toastCount=k;
            // Newest first.
            ToastCount=0;for(int i=toastCount-1;i>=0;i--){var x=toastAt[i];x.Age=(float)(t-toastTime[i]);Toasts[ToastCount++]=x;}
        }
        private void Cue(ExpeditionJoinCueKind kind,int ring,float x,float y,float z){if(CueCount<MaxCues)Cues[CueCount++]=new ExpeditionJoinCue{Kind=kind,Ring=ring,X=x,Y=y,Z=z};}

        // ---- helpers -----------------------------------------------------------------------------------------------------------
        /// <summary>A pilot (its feet) inside a circle's counted column as the authority tests it: the capsule centre (feet +
        /// ExpeditionJoinRules.CapsuleCentreHeight) within the radius and between the floor and the counted height H, each
        /// widened by `pad`.</summary>
        public static bool Within(in ExpeditionJoinRing r,float x,float y,float z,float pad)
        {float dx=x-r.X,dz=z-r.Z,rr=r.R+pad,c=y+ExpeditionJoinRules.CapsuleCentreHeight;return dx*dx+dz*dz<=rr*rr&&c>=r.Y-pad&&c<=r.Y+r.H+pad;}
        /// <summary>A circle that does not count (closedReason other than Open): cleared, locked or reserved.</summary>
        public static bool Closed(ExpeditionJoinPhase p)=>p==ExpeditionJoinPhase.Cleared||p==ExpeditionJoinPhase.Locked||p==ExpeditionJoinPhase.Reserved;
        private static void Unit(ref float x,ref float z,float fx,float fz){float l=(float)Math.Sqrt(x*x+z*z);if(l<1e-4f){x=fx;z=fz;l=(float)Math.Sqrt(x*x+z*z);if(l<1e-4f){x=1;z=0;return;}}x/=l;z/=l;}
        private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        public static float Clamp01(float x)=>x<0?0:x>1?1:x;
        public static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
    }
}
