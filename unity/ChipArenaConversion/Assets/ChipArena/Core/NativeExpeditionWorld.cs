using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChipArena.UnityConversion
{
    // Expedition world pieces (Claude, 2026-10-14; docs/EXPEDITION-PRESENTATION.md "World pieces"). Engine-free: what stands at each
    // real anchor of the current area, read from an ExpeditionView only. Rewards stand where the authority placed them (DropAt,
    // DefeatBossAt, FinishFinalAt, PlaceReward); doors, the reprint machine and chip caches at the scene's SetWorld anchors. A
    // missing anchor, an anchor of another area, or an anchor whose reward, building or cache offer is not in the view draws
    // nothing: there is never a guessed origin. Nothing here claims, buys or decides a rule; claiming goes through the commands.
    // Building, culling and geometry reuse their buffers and cache every label, so a steady view costs no allocation per frame.

    public enum ExpeditionPieceKind { Pickup, Door, Machine, Chest }
    public enum ExpeditionPieceLook { Ordinary, Reserved, NotEligible, DoorSealed, DoorOpen, Machine, Chest }

    /// <summary>One world piece at a real anchor: its look, the three lines its hologram label shows, and its state for the viewer.</summary>
    public struct ExpeditionWorldPiece
    {
        public ExpeditionPieceKind kind;public ExpeditionPieceLook look;public NativeExpeditionPresentation.LootState state;
        /// <summary>The anchor id; the reward, building or cache it stands for; a reward's owner (null while unowned).</summary>
        public string anchor, target, owner;
        public string label, detail, extra;
        /// <summary>The anchor's position, and where the piece is drawn (the anchor, or a small ring round it where several
        /// reserved boss chips share the boss's death point).</summary>
        public float ax, ay, az, x, y, z, yaw;
        public ExpeditionTone tone;
        /// <summary>Yours (your drop, your reserved chip, your cache purchase); something you can act on; settled (taken, claimed,
        /// restocking, not for you), drawn dimmed.</summary>
        public bool mine, actionable, settled;
        /// <summary>Reserved chips sharing one death point (count): one lead carries the cluster's label at the anchor itself
        /// (labelAtAnchor); the others are quiet (glyph only), so a boss's chips never stack unreadable labels.</summary>
        public int group;public bool labelAtAnchor, quiet;
    }

    /// <summary>The geometry sink of the world pieces (the runtime fills reused mesh lists; checks count or rasterise).</summary>
    public interface IExpeditionWorldSink
    {
        /// <summary>A vertex: position, motes uv, flat (1: plain colour, 0: samples the motes map), the palette tone, its
        /// brightness level and opacity. Returns the vertex index.</summary>
        int Vertex(float x,float y,float z,float u,float v,float flat,ExpeditionTone tone,float level,float alpha);
        void Triangle(int a,int b,int c);
    }

    /// <summary>Cached label text, so a steady view never allocates a string per frame. Bounded: cleared past its cap.</summary>
    public sealed class ExpeditionLabels
    {
        private const int Cap=2048;
        private readonly Dictionary<string,string> names=new Dictionary<string,string>(StringComparer.Ordinal),upper=new Dictionary<string,string>(StringComparer.Ordinal);
        private readonly Dictionary<(string,string),string> joins=new Dictionary<(string,string),string>();
        private readonly Dictionary<(string,int,string),string> ints=new Dictionary<(string,int,string),string>();
        private readonly Dictionary<(string,int,int,string),string> pairs=new Dictionary<(string,int,int,string),string>();
        private static void Bound<K>(Dictionary<K,string> d){if(d.Count>Cap)d.Clear();}
        /// <summary>A content id as the screens name it (NativeExpeditionPresentation.Name).</summary>
        public string Name(string id){if(id==null)return "";if(!names.TryGetValue(id,out var s)){Bound(names);s=names[id]=NativeExpeditionPresentation.Name(id);}return s;}
        /// <summary>An account as the world labels name it (upper case; "?" when absent).</summary>
        public string Who(string account){if(string.IsNullOrEmpty(account))return "?";if(!upper.TryGetValue(account,out var s)){Bound(upper);s=upper[account]=account.ToUpperInvariant();}return s;}
        public string Join(string a,string b){var k=(a,b);if(!joins.TryGetValue(k,out var s)){Bound(joins);s=joins[k]=a+b;}return s;}
        public string Int(string prefix,int n,string suffix=null){var k=(prefix,n,suffix);if(!ints.TryGetValue(k,out var s)){Bound(ints);s=ints[k]=prefix+n.ToString(CultureInfo.InvariantCulture)+suffix;}return s;}
        public string Pair(string prefix,int a,int b,string suffix=null)
        {var k=(prefix,a,b,suffix);if(!pairs.TryGetValue(k,out var s)){Bound(pairs);s=pairs[k]=prefix+a.ToString(CultureInfo.InvariantCulture)+" / "+b.ToString(CultureInfo.InvariantCulture)+suffix;}return s;}
        public string Money(int amount){var k=("$",amount,(string)null);if(!ints.TryGetValue(k,out var s)){Bound(ints);s=ints[k]=NativeExpeditionPresentation.Money(amount);}return s;}
    }

    /// <summary>
    /// Builds the world pieces of an ExpeditionView into a reused list. One instance per screen (live game, dev preview); single thread.
    /// </summary>
    public sealed class NativeExpeditionWorld
    {
        /// <summary>Pieces are not drawn past this distance from the eye; labels only within LabelDistance.</summary>
        public const float CullDistance=90, LabelDistance=26;
        public const string Reward="reward", Door="door", Machine="machine", Chest="chest";
        public readonly List<ExpeditionWorldPiece> Pieces=new List<ExpeditionWorldPiece>(64);
        public readonly ExpeditionLabels Labels=new ExpeditionLabels();
        private readonly Dictionary<string,int> loot=new Dictionary<string,int>(StringComparer.Ordinal),buildings=new Dictionary<string,int>(StringComparer.Ordinal),
            caches=new Dictionary<string,int>(StringComparer.Ordinal),dice=new Dictionary<string,int>(StringComparer.Ordinal);
        private readonly Dictionary<(float,float,float),int> shared=new Dictionary<(float,float,float),int>(),placed=new Dictionary<(float,float,float),int>();

        /// <summary>Rebuilds Pieces for this view (cleared when null). Returns the count. Allocation-free once warmed on a view.</summary>
        public int Build(ExpeditionView v)
        {
            Pieces.Clear();if(v==null||v.anchors==null)return 0;
            loot.Clear();buildings.Clear();caches.Clear();dice.Clear();shared.Clear();placed.Clear();
            for(int i=0;i<v.loot.Length;i++)if(v.loot[i]!=null&&v.loot[i].id!=null)loot[v.loot[i].id]=i;
            for(int i=0;i<v.instances.Length;i++)if(v.instances[i]?.id!=null)buildings[v.instances[i].id]=i;
            for(int i=0;i<v.chests.Length;i++)if(v.chests[i]?.id!=null)caches[v.chests[i].id]=i;
            for(int i=0;i<v.dice.Length;i++)if(v.dice[i]?.boss!=null)dice[v.dice[i].boss]=i;
            // Pass 1: how many standing rewards share each point (a boss's reserved chips all stand where it fell).
            foreach(var a in v.anchors)
            {
                if(!Here(v,a)||a.kind!=Reward||!loot.TryGetValue(a.id,out int li)||v.loot[li].consumed)continue;
                var key=(a.x,a.y,a.z);shared.TryGetValue(key,out int n);shared[key]=n+1;
            }
            foreach(var a in v.anchors)
            {
                if(!Here(v,a))continue;
                switch(a.kind)
                {
                    case Reward:RewardPiece(v,a);break;
                    case Door:DoorPiece(v,a);break;
                    case Machine:MachinePiece(v,a);break;
                    case Chest:CachePiece(v,a);break;
                }
            }
            Summarise();
            return Pieces.Count;
        }
        // One label per cluster of reserved chips: yours first (with the others named), else not eligible or reserved, with every
        // owner named once. The lead stands for the cluster at the anchor; the other chips keep their own lines but stay quiet.
        private void Summarise()
        {
            for(int i=0;i<Pieces.Count;i++)
            {
                var p=Pieces[i];if(p.group<2||p.quiet||p.labelAtAnchor)continue;
                int lead=-1,mine=0,opened=0,others=0;bool notEligible=false;string names=null;
                for(int j=i;j<Pieces.Count;j++)
                {
                    var q=Pieces[j];if(q.kind!=ExpeditionPieceKind.Pickup||q.group<2||q.ax!=p.ax||q.ay!=p.ay||q.az!=p.az)continue;
                    if(q.look==ExpeditionPieceLook.NotEligible)notEligible=true;
                    if(q.mine){mine++;if(q.state==NativeExpeditionPresentation.LootState.ClaimedByYou)opened++;if(lead<0)lead=j;continue;}
                    others++;string who=Labels.Who(q.owner);if(names==null)names=who;else if(names.IndexOf(who,StringComparison.Ordinal)<0)names=Labels.Join(Labels.Join(names,", "),who);
                }
                if(lead<0)lead=i;
                for(int j=i;j<Pieces.Count;j++)
                {
                    var q=Pieces[j];if(q.kind!=ExpeditionPieceKind.Pickup||q.group<2||q.ax!=p.ax||q.ay!=p.ay||q.az!=p.az)continue;
                    if(j!=lead){q.quiet=true;Pieces[j]=q;continue;}
                    q.labelAtAnchor=true;
                    if(mine>0){q.label=mine>1?Labels.Int("YOURS × ",mine):"YOURS";q.detail=opened==mine?"OPENED · PICK A CHIP":"RESERVED FOR YOU";q.tone=ExpeditionTone.Magenta;if(others>0)q.extra=Labels.Join("ALSO FOR ",names);}
                    else{q.label=notEligible?"NOT ELIGIBLE":"RESERVED";q.detail=Labels.Join("FOR ",names);q.tone=notEligible?ExpeditionTone.Danger:ExpeditionTone.Muted;}
                    Pieces[j]=q;
                }
            }
        }
        private static bool Here(ExpeditionView v,ExpeditionWorldAnchor a)=>a!=null&&a.id!=null&&a.area!=null&&a.area==v.area&&Finite(a.x)&&Finite(a.y)&&Finite(a.z);
        private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        private static ExpeditionWorldPiece At(ExpeditionWorldAnchor a,ExpeditionPieceKind kind,ExpeditionPieceLook look,string target)
            =>new ExpeditionWorldPiece{kind=kind,look=look,anchor=a.id,target=target,ax=a.x,ay=a.y,az=a.z,x=a.x,y=a.y,z=a.z,yaw=a.yaw};

        // ---- Rewards: ordinary drops (first pickup) and reserved boss chips (one pilot each) ----------------------------------
        private void RewardPiece(ExpeditionView v,ExpeditionWorldAnchor a)
        {
            // Only public loot stands in the world: never a purchase or catch-up reward, and never a reward the view does not hold.
            if(!loot.TryGetValue(a.id,out int li))return;var l=v.loot[li];if(l.consumed)return;
            string me=v.viewer;bool member=NativeExpeditionPresentation.Has(v.mine.account)&&NativeExpeditionPresentation.Has(me);bool mine=member&&l.owner==me;
            ExpeditionWorldPiece p;
            if(l.source=="monster")
            {
                p=At(a,ExpeditionPieceKind.Pickup,ExpeditionPieceLook.Ordinary,l.id);p.mine=mine;
                if(!l.claimed){p.state=NativeExpeditionPresentation.LootState.Open;p.label="FIRST PICKUP";p.detail="ORDINARY DROP · ANYONE";p.tone=ExpeditionTone.Cyan;p.actionable=true;}
                else if(mine){p.state=NativeExpeditionPresentation.LootState.TakenByYou;p.label="YOURS";p.detail="PICK A CHIP · YOUR CHIPS";p.tone=ExpeditionTone.Good;p.settled=true;}
                else{p.state=NativeExpeditionPresentation.LootState.Taken;p.label="TAKEN";p.detail=Labels.Join("BY ",Labels.Who(l.owner));p.tone=ExpeditionTone.Muted;p.settled=true;}
            }
            else if(l.source=="boss")
            {
                p=At(a,ExpeditionPieceKind.Pickup,ExpeditionPieceLook.Reserved,l.id);p.mine=mine;p.extra=Labels.Name(l.boss);
                int di=-1;bool recorded=l.boss!=null&&dice.TryGetValue(l.boss,out di);bool eligible=recorded&&member&&Contains(v.dice[di].eligible,me);
                if(mine)
                {
                    p.state=l.claimed?NativeExpeditionPresentation.LootState.ClaimedByYou:NativeExpeditionPresentation.LootState.ReservedForYou;p.tone=ExpeditionTone.Magenta;
                    p.label="YOURS";p.detail=l.claimed?"OPENED · PICK A CHIP":"RESERVED FOR YOU";p.actionable=!l.claimed;
                }
                else if(member&&recorded&&!eligible)
                {
                    p.look=ExpeditionPieceLook.NotEligible;p.state=NativeExpeditionPresentation.LootState.NotEligible;p.tone=ExpeditionTone.Danger;
                    p.label="NOT ELIGIBLE";p.detail=Labels.Join("FOR ",Labels.Who(l.owner));p.settled=true;
                }
                else
                {
                    p.state=l.claimed?NativeExpeditionPresentation.LootState.ClaimedByOther:NativeExpeditionPresentation.LootState.ReservedForOther;p.tone=ExpeditionTone.Muted;
                    p.label=Labels.Join("FOR ",Labels.Who(l.owner));p.detail=l.claimed?"CLAIMED":"RESERVED";p.settled=true;
                }
            }
            else return;
            p.owner=l.owner;
            // Several reserved chips at one death point stand in a small ring round it, in the order the authority issued them.
            var key=(a.x,a.y,a.z);
            if(shared.TryGetValue(key,out int count)&&count>1)
            {
                placed.TryGetValue(key,out int index);placed[key]=index+1;p.group=count;
                double angle=index*Math.PI*2/count;float radius=.32f+.07f*Math.Min(count,10);
                p.x=a.x+radius*(float)Math.Cos(angle);p.z=a.z+radius*(float)Math.Sin(angle);
            }
            Pieces.Add(p);
        }
        private static bool Contains(string[] list,string value){if(list==null)return false;for(int i=0;i<list.Length;i++)if(list[i]==value)return true;return false;}

        // ---- Doors: the building named by the anchor's area (inside), else by its id (the outdoor entrance) --------------------
        /// <summary>The building a door anchor belongs to (index into instances), or -1: the anchor's area names it (the door
        /// inside that building), else its id (the entrance in the world area). Any other door anchor is not drawn.</summary>
        public int DoorBuilding(ExpeditionWorldAnchor a){if(a==null)return -1;if(!string.IsNullOrEmpty(a.building))return buildings.TryGetValue(a.building,out int explicitBuilding)?explicitBuilding:-1;if(a.area!=null&&buildings.TryGetValue(a.area,out int i))return i;return a.id!=null&&buildings.TryGetValue(a.id,out i)?i:-1;}
        private void DoorPiece(ExpeditionView v,ExpeditionWorldAnchor a)
        {
            int bi=DoorBuilding(a);if(bi<0)return;var b=v.instances[bi];
            var p=At(a,ExpeditionPieceKind.Door,ExpeditionPieceLook.DoorOpen,b.id);p.extra=Labels.Name(b.id);
            if(b.cleared){p.label="OPEN";p.detail="CLEARED · BOSS DOWN";p.tone=ExpeditionTone.Good;p.settled=!b.current;}
            else if(b.current)
            {
                p.look=ExpeditionPieceLook.DoorSealed;p.label="SEALED";
                if(b.bosses.Length>0){p.detail=Labels.Pair("BOSS ",b.defeated.Length,b.bosses.Length," DOWN");p.tone=ExpeditionTone.Magenta;}
                else{p.detail=b.progress>=b.target?"QUOTA MET · BOSS INCOMING":Labels.Pair("QUOTA ",b.progress,b.target);p.tone=ExpeditionTone.Warn;}
            }
            else
            {
                // The entrance outside: open to enter together; it seals behind the party.
                p.label=b.entered?"UNFINISHED":"ENTER TOGETHER";p.detail=b.entered?Labels.Pair("QUOTA ",b.progress,b.target):Labels.Int("QUOTA ",b.target);
                p.tone=b.entered?ExpeditionTone.Warn:ExpeditionTone.Cyan;p.actionable=v.phase==ExpeditionPhase.Town;
            }
            Pieces.Add(p);
        }

        // ---- The reprint machine: who is down and the machine's quote (none: price unavailable, never $0) ----------------------
        private void MachinePiece(ExpeditionView v,ExpeditionWorldAnchor a)
        {
            var p=At(a,ExpeditionPieceKind.Machine,ExpeditionPieceLook.Machine,a.id);p.label="REPRINT MACHINE";
            int down=0;ExpeditionViewMember first=null;
            foreach(var m in v.members)if(m!=null&&!m.alive&&!m.extracted){if(first==null)first=m;down++;}
            if(first==null){p.detail="NOBODY DOWN";p.tone=ExpeditionTone.Muted;p.settled=true;}
            else
            {
                p.detail=Labels.Join(Labels.Join(Labels.Who(first.account)," · "),first.reprintCost>0?Labels.Money(first.reprintCost):"PRICE UNAVAILABLE");
                p.extra=down>1?Labels.Int("+",down-1," MORE DOWN"):Has(first.carriedBy)?Labels.Join("CARRIED BY ",Labels.Who(first.carriedBy)):"NOT CARRIED";
                p.tone=ExpeditionTone.Good;p.actionable=true;
            }
            Pieces.Add(p);
        }
        private static bool Has(string s)=>!string.IsNullOrEmpty(s);

        // ---- Chip caches: the scene's offer (generation, price, cooldown against elapsed), shown before any purchase -----------
        private void CachePiece(ExpeditionView v,ExpeditionWorldAnchor a)
        {
            if(!caches.TryGetValue(a.id,out int ci))return;var o=v.chests[ci];
            var p=At(a,ExpeditionPieceKind.Chest,ExpeditionPieceLook.Chest,o.id);
            string stock=Labels.Join("STOCK ",Labels.Who(o.generation));
            if(Restocking(o,v.elapsed)){p.label="RESTOCKING";p.detail=Labels.Int("",NativeExpeditionPresentation.Left(o.cooldown,v.elapsed),"s");p.extra=stock;p.tone=ExpeditionTone.Muted;p.settled=true;}
            else if(o.sold&&!Bought(v,o)){p.label="SOLD";p.detail="WAITING FOR RESTOCK";p.extra=stock;p.tone=ExpeditionTone.Muted;p.settled=true;}
            else if(Bought(v,o)){p.label="BOUGHT · YOURS";p.detail=Labels.Money(o.price);p.extra=stock;p.tone=ExpeditionTone.Good;p.mine=true;p.settled=true;}
            else
            {
                p.label=Labels.Money(o.price);p.detail=Labels.Join("CHIP CACHE · ",stock);p.tone=ExpeditionTone.Warn;p.actionable=true;
                if(NativeExpeditionPresentation.Has(v.mine.account)&&v.mine.wallet<o.price)p.extra=Labels.Join(Labels.Money(o.price-v.mine.wallet)," SHORT");
            }
            Pieces.Add(p);
        }
        /// <summary>The cache is restocking: its cooldown (an authority-clock deadline) is still ahead of the clock.</summary>
        public static bool Restocking(ExpeditionChestOffer o,double elapsed)=>o!=null&&o.cooldown>elapsed;
        /// <summary>The viewer bought this cache's current generation (its own purchase reward, "...:chest:" + generation). Another
        /// pilot's purchase is private, so it never shows here.</summary>
        public static bool Bought(ExpeditionView v,ExpeditionChestOffer o)
        {
            if(o?.generation==null||v?.mine?.rewards==null)return false;string g=o.generation;
            foreach(var r in v.mine.rewards)
            {
                if(r?.id==null||r.source!="chest")continue;int at=r.id.Length-g.Length;
                if(at>=7&&string.CompareOrdinal(r.id,at,g,0,g.Length)==0&&string.CompareOrdinal(r.id,at-7,":chest:",0,7)==0)return true;
            }
            return false;
        }

        // ---- Culling --------------------------------------------------------------------------------------------------------
        public static float Distance2(in ExpeditionWorldPiece p,float ex,float ey,float ez){float dx=p.x-ex,dy=p.y-ey,dz=p.z-ez;return dx*dx+dy*dy+dz*dz;}
        public static bool Visible(in ExpeditionWorldPiece p,float ex,float ey,float ez)=>Distance2(p,ex,ey,ez)<=CullDistance*CullDistance;
        public static bool Labelled(in ExpeditionWorldPiece p,float ex,float ey,float ez)=>Distance2(p,ex,ey,ez)<=LabelDistance*LabelDistance;

        // ---- Geometry -------------------------------------------------------------------------------------------------------
        /// <summary>Whether a map is an Expedition map (the town and the instances: their ids start "expedition-"). The
        /// Expedition draws no floor ring round an interactable (owner, 2026-10-15); other modes keep theirs.</summary>
        public static bool ExpeditionMap(string mapId)=>mapId!=null&&mapId.StartsWith("expedition-",StringComparison.Ordinal);
        /// <summary>How high the piece's glyph floats over its anchor (metres).</summary>
        public static float GlyphHeight(ExpeditionPieceKind kind)=>kind==ExpeditionPieceKind.Door?1.6f:kind==ExpeditionPieceKind.Machine?1.7f:kind==ExpeditionPieceKind.Chest?1.15f:1.05f;
        /// <summary>A piece's footprint radius (metres): its light column stands inside it. No ring is drawn on the floor (owner,
        /// 2026-10-15: no circle outlines round the Expedition's doors, caches, machines and drops).</summary>
        public static float RingRadius(ExpeditionPieceKind kind)=>kind==ExpeditionPieceKind.Door?1.35f:kind==ExpeditionPieceKind.Machine?.95f:kind==ExpeditionPieceKind.Chest?.75f:.42f;
        /// <summary>The bob of a floating glyph (none with reduced effects).</summary>
        public static float Bob(in ExpeditionWorldPiece p,double t,bool reduced)=>reduced?0:(float)(.05*Math.Sin(t*2.1+(p.x+p.z)*.7));

        /// <summary>
        /// The piece's hologram in the beacon style (Resources/NativeBeacon.shader: additive, double-sided): a light column of rising
        /// motes while it is actionable, and the solid glyph (an ordinary drop's diamond, a reserved chip's tile with its shackle, a
        /// struck tile when not eligible, the machine's pedestal, the cache's crate). Doors draw their column here; their leaves are
        /// the HUD Door hologram at the projected point (anchors carry no facing). No ring on the floor round any piece (owner,
        /// 2026-10-15): the column, the glyph, the label and the interact prompt find it. Static with reduced effects.
        /// </summary>
        public static void Emit(IExpeditionWorldSink sink,in ExpeditionWorldPiece p,double t,bool reduced)
        {
            float fade=p.settled?.42f:1,spin=reduced?0:(float)(t*.9%(Math.PI*2)),r=RingRadius(p.kind);
            if(p.actionable)Column(sink,p.x,p.y,p.z,r*.62f,r*.8f,p.kind==ExpeditionPieceKind.Door?2.9f:p.kind==ExpeditionPieceKind.Machine?2.6f:1.9f,p.tone,.55f);
            float gy=p.y+GlyphHeight(p.kind)+Bob(p,t,reduced),yaw=reduced?0:(float)(t*1.1%(Math.PI*2));
            switch(p.look)
            {
                case ExpeditionPieceLook.Ordinary:Octahedron(sink,p.x,gy,p.z,.26f,yaw,p.tone,fade);break;
                case ExpeditionPieceLook.Reserved:
                    Box(sink,p.x,gy,p.z,.2f,.2f,.05f,yaw,p.tone,.55f,fade);Shackle(sink,p.x,gy+.2f,p.z,.11f,.025f,yaw,p.tone,fade);break;
                case ExpeditionPieceLook.NotEligible:
                    Box(sink,p.x,gy,p.z,.2f,.2f,.05f,yaw,ExpeditionTone.Danger,.35f,fade);Slash(sink,p.x,gy,p.z,.29f,.035f,yaw,fade);break;
                case ExpeditionPieceLook.Machine:
                    Box(sink,p.x,p.y+.55f,p.z,.32f,.55f,.32f,0,p.tone,.32f,fade);Box(sink,p.x,p.y+1.18f,p.z,.4f,.06f,.4f,spin,p.tone,.8f,fade);break;
                case ExpeditionPieceLook.Chest:
                    Box(sink,p.x,p.y+.26f,p.z,.36f,.24f,.26f,0,p.tone,.4f,fade);Box(sink,p.x,p.y+.53f,p.z,.38f,.04f,.28f,0,p.tone,.9f,fade);break;
            }
        }
        /// <summary>A flat ring band on the floor: `count` of `segments` segments from angle `spin` (no longer drawn round the
        /// pieces; kept for callers).</summary>
        public static void Band(IExpeditionWorldSink sink,float cx,float cy,float cz,float r0,float r1,int segments,int count,float spin,ExpeditionTone tone,float level,float alpha)
        {
            for(int i=0;i<count;i++)
            {
                double a0=i*Math.PI*2/segments+spin,a1=(i+1)*Math.PI*2/segments+spin;float c0=(float)Math.Cos(a0),s0=(float)Math.Sin(a0),c1=(float)Math.Cos(a1),s1=(float)Math.Sin(a1);
                int a=sink.Vertex(cx+r0*c0,cy,cz+r0*s0,0,0,1,tone,level,alpha),b=sink.Vertex(cx+r1*c0,cy,cz+r1*s0,0,0,1,tone,level,alpha),
                    c=sink.Vertex(cx+r1*c1,cy,cz+r1*s1,0,0,1,tone,level,alpha),d=sink.Vertex(cx+r0*c1,cy,cz+r0*s1,0,0,1,tone,level,alpha);
                sink.Triangle(a,b,c);sink.Triangle(a,c,d);
            }
        }
        /// <summary>The open light column of the revive beacon (motes uv: u round, v 1 at the top), standing on the floor.</summary>
        public static void Column(IExpeditionWorldSink sink,float cx,float cy,float cz,float top,float bottom,float height,ExpeditionTone tone,float alpha)
        {
            const int n=20;int first=-1;
            for(int row=0;row<=1;row++)for(int i=0;i<=n;i++)
            {
                float u=i/(float)n,radius=row==0?top:bottom;double th=u*Math.PI*2;
                int k=sink.Vertex(cx+radius*(float)Math.Sin(th),cy+(row==0?height:0),cz+radius*(float)Math.Cos(th),u,1-row,0,tone,1,alpha);if(first<0)first=k;
            }
            for(int i=0;i<n;i++){int a=first+i,b=first+n+1+i,c=b+1,d=a+1;sink.Triangle(a,b,d);sink.Triangle(b,c,d);}
        }
        private static void Rotate(float yaw,float x,float z,out float rx,out float rz){float c=(float)Math.Cos(yaw),s=(float)Math.Sin(yaw);rx=x*c-z*s;rz=x*s+z*c;}
        /// <summary>The ordinary drop's diamond (an octahedron, the HUD marker's shape in 3D): lit upper faces, darker lower ones.</summary>
        public static void Octahedron(IExpeditionWorldSink sink,float cx,float cy,float cz,float size,float yaw,ExpeditionTone tone,float alpha)
        {
            float h=size*1.3f;
            for(int i=0;i<4;i++)
            {
                Rotate(yaw+i*(float)Math.PI/2,size,0,out float x0,out float z0);Rotate(yaw+(i+1)*(float)Math.PI/2,size,0,out float x1,out float z1);
                float level=i%2==0?.85f:.6f;
                int a=sink.Vertex(cx,cy+h,cz,0,0,1,tone,level,alpha),b=sink.Vertex(cx+x0,cy,cz+z0,0,0,1,tone,level,alpha),c=sink.Vertex(cx+x1,cy,cz+z1,0,0,1,tone,level,alpha);sink.Triangle(a,b,c);
                a=sink.Vertex(cx,cy-h,cz,0,0,1,tone,level*.55f,alpha);b=sink.Vertex(cx+x1,cy,cz+z1,0,0,1,tone,level*.55f,alpha);c=sink.Vertex(cx+x0,cy,cz+z0,0,0,1,tone,level*.55f,alpha);sink.Triangle(a,b,c);
            }
        }
        /// <summary>A box of half extents (hx, hy, hz) turned by yaw about its centre.</summary>
        public static void Box(IExpeditionWorldSink sink,float cx,float cy,float cz,float hx,float hy,float hz,float yaw,ExpeditionTone tone,float level,float alpha)
        {
            for(int face=0;face<6;face++)
            {
                int axis=face/2;float sign=face%2==0?1:-1;float shade=axis==1?(sign>0?1:.45f):.7f;int first=-1;
                for(int k=0;k<4;k++)
                {
                    float u=k==0||k==3?-1:1,w=k<2?-1:1,x,y,z;
                    if(axis==0){x=sign*hx;y=u*hy;z=w*hz*sign;}else if(axis==1){y=sign*hy;x=u*hx;z=w*hz*sign;}else{z=sign*hz;x=w*hx*sign;y=u*hy;}
                    Rotate(yaw,x,z,out float rx,out float rz);int n=sink.Vertex(cx+rx,cy+y,cz+rz,0,0,1,tone,level*shade,alpha);if(first<0)first=n;
                }
                sink.Triangle(first,first+1,first+2);sink.Triangle(first,first+2,first+3);
            }
        }
        /// <summary>The lock's shackle over a reserved chip: a half ring standing in the tile's plane.</summary>
        public static void Shackle(IExpeditionWorldSink sink,float cx,float cy,float cz,float radius,float thick,float yaw,ExpeditionTone tone,float alpha)
        {
            const int n=10;
            for(int i=0;i<n;i++)
            {
                double a0=Math.PI*i/n,a1=Math.PI*(i+1)/n;
                Rotate(yaw,(radius-thick)*(float)Math.Cos(a0),0,out float ix0,out float iz0);Rotate(yaw,(radius+thick)*(float)Math.Cos(a0),0,out float ox0,out float oz0);
                Rotate(yaw,(radius-thick)*(float)Math.Cos(a1),0,out float ix1,out float iz1);Rotate(yaw,(radius+thick)*(float)Math.Cos(a1),0,out float ox1,out float oz1);
                float iy0=(radius-thick)*(float)Math.Sin(a0),oy0=(radius+thick)*(float)Math.Sin(a0),iy1=(radius-thick)*(float)Math.Sin(a1),oy1=(radius+thick)*(float)Math.Sin(a1);
                int a=sink.Vertex(cx+ix0,cy+iy0,cz+iz0,0,0,1,tone,.9f,alpha),b=sink.Vertex(cx+ox0,cy+oy0,cz+oz0,0,0,1,tone,.9f,alpha),
                    c=sink.Vertex(cx+ox1,cy+oy1,cz+oz1,0,0,1,tone,.9f,alpha),d=sink.Vertex(cx+ix1,cy+iy1,cz+iz1,0,0,1,tone,.9f,alpha);
                sink.Triangle(a,b,c);sink.Triangle(a,c,d);
            }
        }
        /// <summary>The not-eligible strike: a bright bar across the tile, corner to corner.</summary>
        public static void Slash(IExpeditionWorldSink sink,float cx,float cy,float cz,float half,float thick,float yaw,float alpha)
        {
            float d=.7071f;Rotate(yaw,half*d,0,out float x0,out float z0);Rotate(yaw,thick*d,0,out float tx,out float tz);
            int a=sink.Vertex(cx-x0-tx,cy-half*d+thick*d,cz-z0-tz,0,0,1,ExpeditionTone.Danger,1,alpha),b=sink.Vertex(cx-x0+tx,cy-half*d-thick*d,cz-z0+tz,0,0,1,ExpeditionTone.Danger,1,alpha),
                c=sink.Vertex(cx+x0+tx,cy+half*d-thick*d,cz+z0+tz,0,0,1,ExpeditionTone.Danger,1,alpha),e=sink.Vertex(cx+x0-tx,cy+half*d+thick*d,cz+z0-tz,0,0,1,ExpeditionTone.Danger,1,alpha);
            sink.Triangle(a,b,c);sink.Triangle(a,c,e);
        }
    }

    /// <summary>
    /// DEV PREVIEW greybox camera: a fixed perspective look at the current area's anchors, so the dev preview can place the world
    /// pieces where the live camera would (the live game projects with its own camera instead). Screen y grows downwards.
    /// </summary>
    public struct ExpeditionGreyboxCamera
    {
        public float ex, ey, ez, fx, fy, fz, rx, ry, rz, ux, uy, uz, focal, cx, cy;
        public static ExpeditionGreyboxCamera Look(float ex,float ey,float ez,float tx,float ty,float tz,float fovDegrees,float screenX,float screenY,float screenW,float screenH)
        {
            var c=new ExpeditionGreyboxCamera{ex=ex,ey=ey,ez=ez,cx=screenX+screenW/2,cy=screenY+screenH/2};
            float fx=tx-ex,fy=ty-ey,fz=tz-ez,fl=(float)Math.Sqrt(fx*fx+fy*fy+fz*fz);if(fl<1e-5f)fl=1;fx/=fl;fy/=fl;fz/=fl;
            // right = forward x world up (left-handed, as Unity: x right, y up, z forward).
            float rx=fz,ry=0,rz=-fx,rl=(float)Math.Sqrt(rx*rx+rz*rz);if(rl<1e-5f){rx=1;rz=0;rl=1;}rx/=rl;rz/=rl;
            // up = forward x right.
            c.fx=fx;c.fy=fy;c.fz=fz;c.rx=rx;c.ry=ry;c.rz=rz;c.ux=fy*rz-fz*ry;c.uy=fz*rx-fx*rz;c.uz=fx*ry-fy*rx;
            c.focal=screenH*.5f/(float)Math.Tan(fovDegrees*.5*Math.PI/180);return c;
        }
        /// <summary>The screen point of a world point and its depth; false behind the eye.</summary>
        public bool Project(float x,float y,float z,out float sx,out float sy,out float depth)
        {
            float dx=x-ex,dy=y-ey,dz=z-ez;depth=dx*fx+dy*fy+dz*fz;sx=sy=0;if(depth<=.05f)return false;
            sx=cx+(dx*rx+dy*ry+dz*rz)*focal/depth;sy=cy-(dx*ux+dy*uy+dz*uz)*focal/depth;return true;
        }
        /// <summary>Screen units per metre at a depth.</summary>
        public float Scale(float depth)=>depth>.05f?focal/depth:0;
    }
}
