using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Each generated map owns its block array. Area and cache IDs are reused by the next run.
    public sealed class NativeCacheBlockIndex
    {
        private const string Suffix="-block";
        private ContentBlock[] indexed;
        private readonly Dictionary<string,ContentBlock> lockers=new Dictionary<string,ContentBlock>(StringComparer.Ordinal);
        public void Bind(ContentBlock[] blocks)
        {
            if(ReferenceEquals(indexed,blocks))return;
            indexed=blocks;lockers.Clear();
            if(blocks==null)return;
            foreach(var block in blocks)
                if(block?.id!=null&&block.id.EndsWith(Suffix,StringComparison.Ordinal))
                    lockers[block.id.Substring(0,block.id.Length-Suffix.Length)]=block;
        }
        public ContentBlock At(ExpeditionWorldAnchor anchor)
        {
            if(anchor?.id==null||!lockers.TryGetValue(anchor.id,out var block))return null;
            float dx=block.pos.x-anchor.x,dz=block.pos.z-anchor.z;
            return dx*dx+dz*dz<.15f*.15f&&Math.Abs(block.pos.y-block.size.y*.5f-anchor.y)<.1f?block:null;
        }
    }

    /// <summary>What a cache shows: for sale, opening (since its open moment, the held shut after it), restocking (shut and red,
    /// a late joiner's cache already opened), sold (bought, waiting for the restock).</summary>
    public enum NativeCacheState { ForSale, Opening, Restocking, Sold }

    /// <summary>One cache (or chip machine) to draw this frame, in world space at its anchor turned by its yaw (degrees).</summary>
    public struct NativeCacheShow
    {
        public string Id;public float X,Y,Z,Yaw;
        /// <summary>The livery shown (the tier that opened, while it opens and restocks), and the tier the next stock holds.</summary>
        public NativeCacheTier Tier,Next;public NativeCacheState State;
        /// <summary>Seconds since the open moment (Opening); the for-sale clock (ForSale); seconds since the livery sweep began
        /// (NaN: none) and the tier it sweeps from.</summary>
        public float Since,Sale,Sweep;public NativeCacheTier SweepFrom;
        /// <summary>This client bought it (its three cartridges, the column's dimmed foot); the viewer is short of its price; a
        /// steel locker stands for it (A2's block); a chip machine's opening (no locker, no lamps: the machine is the cabinet).</summary>
        public bool Buyer,Short,Locker,Machine;
        /// <summary>The opening's seed (the cache's id and the generation it opened); whole seconds left on its restock.</summary>
        public uint Seed;public int Seconds;
    }

    /// <summary>
    /// The caches' open moments, read from the replicated view only (Claude, 2026-11-19; R9 "the loot moment"). The open moment
    /// is the frame this client sees a cache's generation change with its cooldown still ahead (or its stock turn sold before the
    /// restock lands). The burst is public and names no buyer: only the viewer's own purchase (its private reward for the
    /// generation that opened) makes it the buyer's view. A cache first seen already restocking (a late joiner, an area just
    /// entered) is shown opened from that first-seen clock: shut and red with its countdown, no burst. At restock (the cooldown
    /// passing) it clicks, and when the new stock holds another tier its livery sweeps across. Engine-free; once warmed on a
    /// set of caches, a frame allocates nothing.
    /// </summary>
    public sealed class NativeCacheClock
    {
        private sealed class Entry
        {
            public string Generation,OpenGeneration;public bool Sold,Restocking,Buyer;public NativeCacheTier Shown,SweepFrom;
            public double OpenAt=double.NaN,SweepAt=double.NaN,RestockAt=double.NaN;public uint Seed;public int Frame;
        }
        private readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
        private readonly List<string> gone=new List<string>(8);private int frame;
        public readonly List<NativeCacheShow> Items=new List<NativeCacheShow>(16);
        /// <summary>Where the restock clicks this frame (the presentation plays NativeCacheOpenFx.Restock there).</summary>
        public readonly List<NativeCacheShow> Clicks=new List<NativeCacheShow>(4);
        /// <summary>Whether A2's solid chest block stands at an anchor (the runtime looks for it in the map; null: never yet).</summary>
        public Func<ExpeditionWorldAnchor,bool> Solid;
        /// <summary>The livery sweep's length (s).</summary>
        public const float SweepSeconds=.6f;

        /// <summary>Reads the view at presentation time `now` (s); null clears.</summary>
        public void Observe(ExpeditionView v,double now)
        {
            Items.Clear();Clicks.Clear();frame++;
            if(v?.anchors==null||v.chests==null){entries.Clear();return;}
            bool member=NativeExpeditionPresentation.Has(v.mine?.account);
            foreach(var a in v.anchors)
            {
                if(a?.id==null||a.kind!=NativeExpeditionWorld.Chest||a.area!=v.area)continue;
                ExpeditionChestOffer o=null;for(int i=0;i<v.chests.Length;i++)if(v.chests[i]?.id==a.id){o=v.chests[i];break;}if(o==null)continue;
                var tier=NativeCacheTiers.Of(o);bool restocking=NativeExpeditionWorld.Restocking(o,v.elapsed);
                if(!entries.TryGetValue(a.id,out var e))
                {
                    // first seen: what stands is what it is; a cache already opened is shown opened (shut, red), with no burst
                    e=new Entry{Generation=o.generation,Sold=o.sold,Restocking=restocking,Shown=tier};entries[a.id]=e;
                    if(restocking||o.sold){e.OpenAt=double.NegativeInfinity;e.OpenGeneration=o.generation;}
                }
                else
                {
                    if(o.generation!=e.Generation)
                    {
                        if(restocking&&e.OpenGeneration!=e.Generation)Open(e,v,a.id,e.Generation,now);
                        else if(!restocking&&!o.sold&&tier!=e.Shown){e.SweepAt=now;e.SweepFrom=e.Shown;}
                        e.Generation=o.generation;
                    }
                    else if(o.sold&&!e.Sold&&e.OpenGeneration!=o.generation)Open(e,v,a.id,o.generation,now);
                    // the restock: the cooldown passes and the stock is on sale again (a click; the new tier's livery sweeps)
                    if((e.Restocking||e.Sold)&&!restocking&&!o.sold)
                    {
                        e.RestockAt=now;if(tier!=e.Shown){e.SweepAt=now;e.SweepFrom=e.Shown;}
                        Clicks.Add(new NativeCacheShow{Id=a.id,X=a.x,Y=a.y,Z=a.z,Yaw=a.yaw,Tier=tier});
                    }
                }
                e.Sold=o.sold;e.Restocking=restocking;e.Frame=frame;
                var s=new NativeCacheShow{Id=a.id,X=a.x,Y=a.y,Z=a.z,Yaw=a.yaw,Next=tier,Seed=e.Seed,Buyer=e.Buyer,Sweep=float.NaN,SweepFrom=e.SweepFrom,Locker=Solid!=null&&Solid(a)};
                if(restocking||o.sold)
                {
                    s.Tier=e.Shown;s.Seconds=NativeExpeditionPresentation.Left(o.cooldown,v.elapsed);
                    if(double.IsNegativeInfinity(e.OpenAt)){s.State=restocking?NativeCacheState.Restocking:NativeCacheState.Sold;s.Since=NativeCacheOpenFx.End;s.Buyer=false;}
                    else{s.State=NativeCacheState.Opening;s.Since=(float)(now-e.OpenAt);}
                }
                else
                {
                    e.OpenAt=double.NaN;e.Buyer=false;e.Shown=tier;s.Tier=tier;s.State=NativeCacheState.ForSale;s.Buyer=false;s.Sale=(float)(now%1e5);
                    s.Short=member&&v.mine.wallet<o.price;
                    if(!double.IsNaN(e.SweepAt)&&now-e.SweepAt<SweepSeconds)s.Sweep=(float)(now-e.SweepAt);else e.SweepAt=double.NaN;
                }
                Items.Add(s);
            }
            // a cache out of sight (another area) is forgotten: the next sight is a first sight, never a stale open moment
            gone.Clear();foreach(var kv in entries)if(kv.Value.Frame!=frame)gone.Add(kv.Key);foreach(var k in gone)entries.Remove(k);
        }
        private static void Open(Entry e,ExpeditionView v,string id,string generation,double now)
        {
            e.OpenAt=now;e.OpenGeneration=generation;e.Seed=NativeCacheOpenFx.Seed(id,generation);e.Buyer=BoughtGeneration(v,generation);
        }
        /// <summary>The viewer's own purchase of `generation` (its private reward "...:chest:" + generation): the only buyer a view
        /// can know. Another pilot's purchase never reaches the view (NativeExpeditionView.Leaks).</summary>
        public static bool BoughtGeneration(ExpeditionView v,string generation)
        {
            if(generation==null||v?.mine?.rewards==null)return false;
            foreach(var r in v.mine.rewards)
            {
                if(r?.id==null||r.source!="chest")continue;int at=r.id.Length-generation.Length;
                if(at>=7&&string.CompareOrdinal(r.id,at,generation,0,generation.Length)==0&&string.CompareOrdinal(r.id,at-7,":chest:",0,7)==0)return true;
            }
            return false;
        }
        public void Clear(){entries.Clear();Items.Clear();Clicks.Clear();}

        /// <summary>The hologram's opening on the world pieces (until A2's block stands, the live cache keeps its hologram body,
        /// which plays the same opening as a flare): each opening cache's lid angle and flare, and whether a locker stands.</summary>
        public void Stamp(List<ExpeditionWorldPiece> pieces,bool reduced)
        {
            for(int i=0;i<pieces.Count;i++)
            {
                var p=pieces[i];if(p.kind!=ExpeditionPieceKind.Chest)continue;
                for(int k=0;k<Items.Count;k++)
                {
                    var s=Items[k];if(s.Id!=p.anchor)continue;p.locker=s.Locker;
                    if(s.State==NativeCacheState.Opening&&s.Since<NativeCacheOpenFx.End)
                    {
                        var o=NativeCacheOpenFx.Opening(s.Tier,s.Seed,reduced);p.lid=o.Lid(s.Since);
                        p.flare=reduced?0:NativeCacheOpenFx.Env(s.Since,.2f,.32f,o.Length,1,1.2f);
                    }
                    pieces[i]=p;break;
                }
            }
        }
    }

    /// <summary>
    /// The DEV preview of the caches (presentation only; DEV panel · Previews, "Cache openings"): the four CD-647 lockers in a row
    /// in front of where the pilot stood when it was turned on, and today's hologram cache beside them, so the owner sees every
    /// tier before A2's blocks and tiers land. A 9 s cycle: the restock's click and livery sweep, 2.5 s for sale (the lamps
    /// blinking each tier's count), then the opening and the held shut. Each cycle moves every locker one tier on, so the row
    /// always shows the four tiers and each locker shows them all; odd cycles are the buyer's view (the three cartridges dock on
    /// your pilot), even cycles a watcher's. No collider, no rule, no network: it only draws.
    /// </summary>
    public static class NativeCachePreview
    {
        public static bool On;
        /// <summary>Where the row stands: its middle (world), the way its fronts face (degrees); when its first cycle begins (the
        /// presentation clock, a whole number of periods, so every peer and capture can say which tier opens when; NaN: the row
        /// is placed on the next frame, ahead of the camera).</summary>
        public static float X,Y,Z,Yaw;public static double Start=double.NaN;
        public const float Period=9,SaleFor=2.5f,Spacing=2.2f;public const int Lockers=4;
        private static readonly string[] Ids={"dev-cache-0","dev-cache-1","dev-cache-2","dev-cache-3","dev-cache-hologram"};
        public static void Place(float x,float y,float z,float yaw,double now){X=x;Y=y;Z=z;Yaw=yaw;Start=Math.Ceiling(now/Period)*Period;}
        /// <summary>The cycle at `now` (counted from the clock's zero: cycle c holds tier (i + c) % 4 on locker i, odd cycles the
        /// buyer's view) and the time into it; before Start, the first cycle's start (for sale).</summary>
        public static int Cycle(double now,out float into)
        {
            double a=double.IsNaN(Start)||now>=Start?now:Start;int c=(int)Math.Floor(a/Period);into=(float)(a-c*Period);if(!double.IsNaN(Start)&&now<Start)into=0;return c;
        }
        /// <summary>Fills `into` with the row's caches at `now` (the four lockers, then the hologram at the row's end).</summary>
        public static void Fill(List<NativeCacheShow> into,List<NativeCacheShow> clicks,double now,double previous)
        {
            int c=Cycle(now,out float t);bool buyer=c%2==1,started=!double.IsNaN(Start)&&now>=Start;Cycle(previous,out float before);
            int cb=Cycle(previous,out _);bool clicked=started&&(cb!=c||previous<Start);
            for(int i=0;i<=Lockers;i++)
            {
                bool hologram=i==Lockers;float off=(i-Lockers*.5f)*Spacing;NativeCacheOpenFx.ToWorld(Yaw,off,0,out float dx,out float dz);
                var tier=hologram?NativeCacheTier.Standard:(NativeCacheTier)((i+c)%NativeCacheTiers.Count);var from=hologram?tier:(NativeCacheTier)((i+c+3)%NativeCacheTiers.Count);
                uint seed=(uint)(c*8+i+1)*2654435761u;seed^=seed>>15;
                var s=new NativeCacheShow{Id=Ids[i],X=X+dx,Y=Y,Z=Z+dz,Yaw=Yaw,Tier=tier,Next=tier,Locker=!hologram,Seed=seed,Sweep=float.NaN,SweepFrom=from};
                if(!started||t<SaleFor){s.State=NativeCacheState.ForSale;s.Sale=t;s.Short=i==1;if(started&&t<NativeCacheClock.SweepSeconds&&from!=tier)s.Sweep=t;}
                else{s.State=NativeCacheState.Opening;s.Since=t-SaleFor;s.Buyer=buyer;}
                into.Add(s);if(clicked)clicks.Add(s);
            }
        }
    }
}
