using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE 6:47 as the game draws it (docs/EXPEDITION-BOSS-CENTRAL-STATION.md "Presentation build"): the concept model's skeleton
    /// and geometry (Core/NativeStationBossModel.cs, its three tiers) with the presentation's own joints after the concept's 68:
    /// <list type="bullet">
    /// <item>the two PA horns on sprung stalks (<see cref="HornL"/>, <see cref="HornR"/>: chains in the rig, so they sway, whip on
    /// the brake and shudder on the horn), split out of the cab;</item>
    /// <item>the Stationmaster's coat tails (<see cref="TailL"/>, <see cref="TailR"/>, three links each: chains draping over the roof
    /// and streaming on the charge), new geometry;</item>
    /// <item>each side window, the cab's lit glass and the marker lamps as their own joints (<see cref="Window"/>, <see cref="CabGlow"/>,
    /// <see cref="Markers"/>), split out of the body so their light is driven per joint (windows dying one by one from the rear,
    /// flickering on hits);</item>
    /// <item>the torn flanks (<see cref="BreachL"/>, <see cref="BreachR"/>), drawn only once the express has broken that side;</item>
    /// <item>a grime pass in the vertex colours (no triangles): dirt rising from the underframe, soot along the roof line and
    /// trailing back from the cab, the nose's lower edge worn dark, the passengers' limbs darker and wetter toward the hands.</item>
    /// </list>
    /// Every added part stays inside the concept's hit volumes (the carriage box, the rider's capsule, the reach). Deterministic,
    /// built once per tier; nothing per frame.
    /// </summary>
    public static class NativeStationBossLook
    {
        public const int HornL=68,HornR=69,TailL=70,TailR=73,TailLinks=3,WindowFirst=76,CabGlow=84,Markers=85,BreachL=86,BreachR=87,WatchAt47=88,WatchAt48=89,Count=90;
        /// <summary>Window k (0..3 in NativeStationBossModel.WindowZ order) on a side (0 left, 1 right).</summary>
        public static int Window(int side,int k)=>WindowFirst+side*4+k;
        /// <summary>Triangle budgets per tier (full, reduced, low): the concept's plus what the presentation adds (the tails, the
        /// stalks, the torn flanks, the lamp's lens and the legs' joint collars at full detail). One boss on screen; about three
        /// Standard pilots at full detail.</summary>
        public static readonly int[] Budget={23000,11200,5000};
        public static readonly int[] Parent=new int[Count];public static readonly Vector3[] Rest=new Vector3[Count];public static readonly string[] Names=new string[Count];
        static NativeStationBossLook()
        {
            for(int i=0;i<NativeStationBossModel.Count;i++){Parent[i]=NativeStationBossModel.Parent[i];Rest[i]=NativeStationBossModel.Rest[i];Names[i]=NativeStationBossModel.Names[i];}
            void B(int i,string name,int parent,float x,float y,float z){Names[i]=name;Parent[i]=parent;Rest[i]=new Vector3(x,y,z);}
            int body=NativeStationBossModel.Body,hips=NativeStationBossModel.SmHips;float r=NativeStationBossModel.Rider;
            B(HornL,"horn-l",body,-.78f,2.62f,3.35f);B(HornR,"horn-r",body,.78f,2.62f,3.35f);
            for(int k=0;k<TailLinks;k++){B(TailL+k,"tail-l-"+k,k==0?hips:TailL+k-1,k==0?-.17f*r:0,k==0?.12f:-TailLength,k==0?-.42f*r:0);B(TailR+k,"tail-r-"+k,k==0?hips:TailR+k-1,k==0?.17f*r:0,k==0?.12f:-TailLength,k==0?-.42f*r:0);}
            for(int side=0;side<2;side++)for(int k=0;k<4;k++)B(Window(side,k),"window-"+(side==0?"l-":"r-")+k,body,(side==0?-1:1)*NativeStationBossModel.HalfWidth,1.55f,NativeStationBossModel.WindowZ[k]);
            B(CabGlow,"cab-glow",body,0,1.45f,4.0f);B(Markers,"markers",body,0,1.3f,5.25f);B(BreachL,"breach-l",body,-1.45f,1.2f,-.7f);B(BreachR,"breach-r",body,1.45f,1.2f,-.7f);
            // The pocket watch's minute hand at 6:47 and at 6:48 (full detail only; its rest is the watch's centre on the hand).
            var watch=WatchPivot(out _,out _);B(WatchAt47,"watch-647",NativeStationBossModel.SmRHand,watch.X,watch.Y,watch.Z);B(WatchAt48,"watch-648",NativeStationBossModel.SmRHand,watch.X,watch.Y,watch.Z);
        }
        /// <summary>A coat tail link's length (m).</summary>
        public const float TailLength=.3f;
        /// <summary>Whether a joint is a glow joint the presentation drives per frame (windows, the cab's glass, the markers).</summary>
        public static bool GlowJoint(int j)=>j>=WindowFirst&&j<=Markers;

        // ---------------- the build ----------------
        private const float Deg=(float)(Math.PI/180);
        public static WeaponMesh Build(int tier)
        {
            tier=Math.Max(0,Math.Min(2,tier));var mesh=NativeStationBossModel.Build(tier);
            Split(mesh);var s=new WeaponSculpt(mesh,tier);Tails(s,tier);Breach(s,tier);Detail(s,tier);if(tier==0)Watch(mesh);Grime(mesh);
            return mesh;
        }

        // ---------------- the pocket watch: its minute hand moves to 6:48 at the death ----------------
        /// <summary>The watch's centre on the Stationmaster's right hand (the hands' meeting end), its face normal (out of the face) and
        /// the turn (radians about the normal) that moves the minute hand one minute on; from the concept's full-detail geometry.</summary>
        public static Vector3 WatchPivot(out Vector3 normal,out float minute)
        {
            if(!watchRead){watchRead=true;var mesh=NativeStationBossModel.Build(0);WatchHands(mesh.Section(NativeStationBossModel.SmRHand,WeaponStretch.None),out watchMinute,out watchPivot,out watchNormal,out watchTurn,out _);}
            normal=watchNormal;minute=watchTurn;return watchPivot;
        }
        private static bool watchRead;private static Vector3 watchPivot,watchNormal;private static float watchTurn;private static List<int> watchMinute;
        // Moves the minute hand's triangles off the hand into the two watch joints (as drawn, and turned on one minute).
        private static void Watch(WeaponMesh mesh)
        {
            var hand=mesh.Section(NativeStationBossModel.SmRHand,WeaponStretch.None);if(!WatchHands(hand,out var tris,out var pivot,out var n,out float turn,out _))return;
            var rot=Quaternion.CreateFromAxisAngle(n,turn);var a=mesh.Section(WatchAt47,WeaponStretch.None);var b=mesh.Section(WatchAt48,WeaponStretch.None);var gone=new HashSet<int>(tris);
            foreach(int t in tris)for(int k=0;k<3;k++)
            {
                int v=hand.Indices[t*3+k];var p=hand.Positions[v]-pivot;
                a.Indices.Add(a.Positions.Count);a.Positions.Add(p);a.Normals.Add(hand.Normals[v]);a.Colors.Add(hand.Colors[v]);a.Emission.Add(hand.Emission[v]);
                b.Indices.Add(b.Positions.Count);b.Positions.Add(Vector3.Transform(p,rot));b.Normals.Add(Vector3.Transform(hand.Normals[v],rot));b.Colors.Add(hand.Colors[v]);b.Emission.Add(hand.Emission[v]);
            }
            var keep=new List<int>();for(int t=0;t*3+2<hand.Indices.Count;t++)if(!gone.Contains(t))for(int k=0;k<3;k++)keep.Add(hand.Indices[t*3+k]);
            hand.Indices.Clear();hand.Indices.AddRange(keep);
        }
        // The watch's two black hands on the hand's section: the triangles fall into two boxes (shared corners); the longer is the
        // minute hand. The pivot is the minute hand's end nearer the hour hand, the normal the hands' broad faces' (out from the
        // amber face), and the turn's sign the one that carries the hour hand's direction onto the minute hand's the short way
        // (clockwise on the face). False if the hands are not there (reduced tiers).
        private static bool WatchHands(WeaponSection hand,out List<int> minute,out Vector3 pivot,out Vector3 normal,out float turn,out float between)
        {
            minute=new List<int>();pivot=Vector3.Zero;normal=Vector3.UnitZ;turn=0;between=0;
            var black=WeaponPaint.Linear(0x050607);var tris=new List<int>();
            for(int t=0;t*3+2<hand.Indices.Count;t++){var c=hand.Colors[hand.Indices[t*3]];if(Math.Abs(c.X-black.X)<1e-4f&&Math.Abs(c.Y-black.Y)<1e-4f&&Math.Abs(c.Z-black.Z)<1e-4f)tris.Add(t);}
            if(tris.Count<8)return false;
            // Union the triangles that share a corner.
            var root=new int[tris.Count];for(int i=0;i<root.Length;i++)root[i]=i;int Find(int i){while(root[i]!=i)i=root[i]=root[root[i]];return i;}
            for(int i=0;i<tris.Count;i++)for(int j=i+1;j<tris.Count;j++)
            {
                bool shared=false;for(int a=0;a<3&&!shared;a++)for(int b=0;b<3&&!shared;b++)shared=Vector3.DistanceSquared(hand.Positions[hand.Indices[tris[i]*3+a]],hand.Positions[hand.Indices[tris[j]*3+b]])<1e-10f;
                if(shared)root[Find(i)]=Find(j);
            }
            var groups=new Dictionary<int,List<int>>();for(int i=0;i<tris.Count;i++){int r=Find(i);if(!groups.TryGetValue(r,out var g))groups[r]=g=new List<int>();g.Add(tris[i]);}
            if(groups.Count!=2)return false;List<int> g0=null,g1=null;foreach(var g in groups.Values){if(g0==null)g0=g;else g1=g;}
            float Span(List<int> g){Vector3 lo=new Vector3(float.MaxValue),hi=new Vector3(float.MinValue);foreach(int t in g)for(int k=0;k<3;k++){var p=hand.Positions[hand.Indices[t*3+k]];lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,p);}return Vector3.Distance(lo,hi);}
            Vector3 Mid(List<int> g){var c=Vector3.Zero;int n=0;foreach(int t in g)for(int k=0;k<3;k++){c+=hand.Positions[hand.Indices[t*3+k]];n++;}return c/n;}
            var hourTris=Span(g0)>=Span(g1)?g1:g0;minute=ReferenceEquals(hourTris,g0)?g1:g0;
            // The broad faces' normal, and the minute hand's long axis and its two ends.
            float best=0;Vector3 axis=Vector3.UnitX;var mc=Mid(minute);float far=0;
            foreach(int t in minute)
            {
                var p0=hand.Positions[hand.Indices[t*3]];var p1=hand.Positions[hand.Indices[t*3+1]];var p2=hand.Positions[hand.Indices[t*3+2]];var cr=Vector3.Cross(p1-p0,p2-p0);float area=cr.Length();
                if(area>best+1e-9f){best=area;normal=Vector3.Normalize(cr);}
                foreach(var p in new[]{p0,p1,p2}){var d=p-mc;if(d.Length()>far){far=d.Length();axis=d;}}
            }
            axis-=normal*Vector3.Dot(axis,normal);axis=Vector3.Normalize(axis);float half=0;foreach(int t in minute)for(int k=0;k<3;k++)half=Math.Max(half,Math.Abs(Vector3.Dot(hand.Positions[hand.Indices[t*3+k]]-mc,axis)));
            var hc=Mid(hourTris);var e0=mc+axis*half;var e1=mc-axis*half;pivot=Vector3.Distance(e0,hc)<Vector3.Distance(e1,hc)?e0:e1;
            // Out of the face: away from the amber dial.
            var dial=Vector3.Zero;int dn=0;for(int t=0;t*3+2<hand.Indices.Count;t++){var e=hand.Emission[hand.Indices[t*3]];if(e.X+e.Y+e.Z>1e-4f){for(int k=0;k<3;k++)dial+=hand.Positions[hand.Indices[t*3+k]];dn+=3;}}
            if(dn>0&&Vector3.Dot(mc-dial/dn,normal)<0)normal=-normal;
            // Clockwise on the face: the way that carries the hour hand onto the minute hand the short way.
            Vector3 H=hc-pivot,M=mc-pivot;H-=normal*Vector3.Dot(H,normal);M-=normal*Vector3.Dot(M,normal);
            between=(float)Math.Atan2(Vector3.Dot(normal,Vector3.Cross(H,M)),Vector3.Dot(H,M));turn=Math.Sign(between)*6*Deg;
            return true;
        }
        /// <summary>Total triangles of a tier.</summary>
        public static int Triangles(int tier)=>Build(tier).Triangles;

        // Moves the body's lit windows, the cab's glass, the marker lamps and the PA horns into their own joints (the triangles
        // as they are, re-expressed in the new joint's rest frame: rest rotations are identity, so that is an offset).
        private static void Split(WeaponMesh mesh)
        {
            var body=mesh.Section(NativeStationBossModel.Body,WeaponStretch.None);var keep=new WeaponSection{Bone=body.Bone};
            var cable=WeaponPaint.Linear(0x121316);
            for(int t=0;t+2<body.Indices.Count;t+=3)
            {
                int a=body.Indices[t],b=body.Indices[t+1],c=body.Indices[t+2];var p=(body.Positions[a]+body.Positions[b]+body.Positions[c])/3;
                var e=body.Emission[a];bool lit=e.X+e.Y+e.Z>1e-4f;var col=body.Colors[a];bool isCable=Math.Abs(col.X-cable.X)<1e-4f&&Math.Abs(col.Y-cable.Y)<1e-4f&&Math.Abs(col.Z-cable.Z)<1e-4f;
                int to=Target(p,lit,isCable);var dst=to<0?keep:mesh.Section(to,WeaponStretch.None);var off=to<0?Vector3.Zero:Rest[to];
                int n=dst.Positions.Count;
                foreach(int v in new[]{a,b,c}){dst.Positions.Add(body.Positions[v]-off);dst.Normals.Add(body.Normals[v]);dst.Colors.Add(body.Colors[v]);dst.Emission.Add(body.Emission[v]);}
                dst.Indices.Add(n);dst.Indices.Add(n+1);dst.Indices.Add(n+2);
            }
            body.Positions.Clear();body.Normals.Clear();body.Colors.Clear();body.Emission.Clear();body.Indices.Clear();
            body.Positions.AddRange(keep.Positions);body.Normals.AddRange(keep.Normals);body.Colors.AddRange(keep.Colors);body.Emission.AddRange(keep.Emission);body.Indices.AddRange(keep.Indices);
        }
        // Which presentation joint a body triangle (centroid p, body frame) belongs to, or -1 (it stays on the body).
        private static int Target(Vector3 p,bool lit,bool cable)
        {
            float ax=Math.Abs(p.X);int side=p.X<0?0:1;
            // A side window: its frame, amber pane, the passengers' silhouettes and hands (the cream band and linings stay).
            if(ax>1.44f&&ax<1.5f&&p.Y>1.17f&&p.Y<1.93f&&p.Z<NativeStationBossModel.CabZ-.2f)
                for(int k=0;k<4;k++)if(Math.Abs(p.Z-NativeStationBossModel.WindowZ[k])<.5f)return Window(side,k);
            // The PA horns and their mounts (not the cable running back to them).
            if(!cable&&Math.Abs(p.X-(side==0?-.78f:.78f))<.34f&&p.Y>2.6f&&p.Y<3.35f&&p.Z>3.12f&&p.Z<3.75f)return side==0?HornL:HornR;
            if(lit)
            {
                if(ax>1.0f&&ax<1.25f&&Math.Abs(p.Y-1.3f)<.12f&&p.Z>5.15f)return Markers;
                if(p.Z>3.0f&&p.Y>.5f&&p.Y<2.6f)return CabGlow;
            }
            return -1;
        }

        // The Stationmaster's frock coat tails, three tapering links each, a gold edge at full detail. Each link hangs down its
        // joint's -Y (a chain segment), its face across Z (the chain's pole is up: it lies flat on the roof at rest).
        private static void Tails(WeaponSculpt s,int tier)
        {
            var coat=WeaponPaint.Solid(NativeStationBossModel.CoatHex,.05f);var dark=WeaponPaint.Solid(0x08090B);var gold=WeaponPaint.Solid(NativeStationBossModel.GoldHex,.3f);
            for(int side=0;side<2;side++)for(int k=0;k<TailLinks;k++)
            {
                s.Into((side==0?TailL:TailR)+k);float w0=.3f-k*.05f,w1=w0-.05f,len=TailLength+.02f,th=.04f;
                if(tier>=2){s.Box(coat,new Vector3(0,-len/2,0),new Vector3((w0+w1)/2,len,th));continue;}
                // A tapered slab (trapezoid across X), the lining darker underneath.
                var top=new[]{new Vector3(-w0/2,0,th/2),new Vector3(w0/2,0,th/2),new Vector3(w1/2,-len,th/2),new Vector3(-w1/2,-len,th/2)};
                var bot=new[]{new Vector3(-w0/2,0,-th/2),new Vector3(w0/2,0,-th/2),new Vector3(w1/2,-len,-th/2),new Vector3(-w1/2,-len,-th/2)};
                s.Quad(coat,top[0],top[3],top[2],top[1]);s.Quad(dark,bot[0],bot[1],bot[2],bot[3]);
                for(int e=0;e<4;e++){int f=(e+1)%4;s.Quad(e==2?coat:dark,top[e],top[f],bot[f],bot[e]);}
                if(tier==0){float sx=side==0?-1:1;s.Box(gold,new Vector3(sx*(w0+w1)/4,-len/2,th/2+.004f),new Vector3(.025f,len*.98f,.006f),0,0,0,sx*(float)Math.Atan2((w0-w1)/2,len));}
                if(k==TailLinks-1&&tier==0)s.Box(gold,new Vector3(0,-len+.02f,th/2+.004f),new Vector3(w1*.9f,.025f,.006f));
            }
        }

        // The torn flank (drawn only once the express has broken it): steel lips bent outward round a gash, the passengers' mass
        // glowing through it, heads and reaching arms; inside the carriage box (x within 1.62).
        private static void Breach(WeaponSculpt s,int tier)
        {
            var steel=WeaponPaint.Solid(NativeStationBossModel.LiveryHex,.02f);var black=WeaponPaint.Solid(0x050607);var flesh=WeaponPaint.Solid(NativeStationBossModel.FleshHex,.03f);
            var dark=WeaponPaint.Solid(NativeStationBossModel.FleshDarkHex,.02f);var glow=WeaponPaint.Glow(NativeStationBossModel.AmberHex,2.4f,.45f);var core=WeaponPaint.Glow(NativeStationBossModel.CoreHex,2.8f,.5f);
            for(int side=0;side<2;side++)
            {
                float sx=side==0?-1:1;s.Into(side==0?BreachL:BreachR);
                s.Box(black,new Vector3(sx*.02f,.15f,0),new Vector3(.03f,1.3f,2.6f));
                int lips=tier==0?9:tier==1?6:3;
                for(int k=0;k<lips;k++){float z=-1.25f+k*2.5f/(lips-1),y=k%2==0?.82f:-.52f,ang=(k*37%50)-25;s.Box(steel,new Vector3(sx*(.02f+.02f*(k%3)),y,z),new Vector3(.04f,.42f,.34f),0,ang*Deg,sx*(10+k*7%16)*Deg,0);}
                s.Sphere(dark,new Vector3(sx*.0f,.1f,-.3f),new Vector3(.12f,.55f,.9f),tier==0?4:3,tier==0?8:5,tier!=0);
                s.Sphere(glow,new Vector3(sx*.03f,.12f,.25f),new Vector3(.08f,.42f,.6f),tier==0?4:3,tier==0?8:5,tier!=0);
                if(tier<=1)
                {
                    s.Box(core,new Vector3(sx*.06f,.3f,-.5f),new Vector3(.02f,.25f,.5f));
                    for(int k=0;k<(tier==0?5:3);k++){float z=-1.0f+k*.5f,y=.35f+(k%2)*.18f;s.Sphere(k%2==0?flesh:dark,new Vector3(sx*.05f,y,z),new Vector3(.1f,.12f,.1f),3,6,true);
                        if(k%2==1)s.Box(flesh,new Vector3(sx*.04f,y+.25f,z+.1f),new Vector3(.06f,.4f,.06f),0,0,0,sx*-12*Deg);}
                }
            }
        }

        // Full-detail refinements inside the concept's volumes: the headlamp's lens dome, collars where the limbs meet the wheels
        // and the hands (the joints read as joints), an iron rim round the hatch.
        private static void Detail(WeaponSculpt s,int tier)
        {
            if(tier!=0)return;
            var lens=WeaponPaint.Glow(NativeStationBossModel.HeadlampHex,5f,.5f);var flesh=WeaponPaint.Solid(NativeStationBossModel.FleshDarkHex,.02f);var iron=WeaponPaint.Solid(NativeStationBossModel.IronHex,.03f);
            var cuff=WeaponPaint.Solid(0x2A1A10,.02f);var brass=WeaponPaint.Solid(NativeStationBossModel.BrassHex,.25f);
            s.Into(NativeStationBossModel.Lamp);s.Sphere(lens,new Vector3(0,0,.2f),new Vector3(.25f,.25f,.08f),4,14);
            for(int i=0;i<NativeStationBossModel.Legs;i++)
            {
                float t=NativeStationBossModel.Row(i)==0?.6f:.56f;
                s.Into(NativeStationBossModel.Hip(i));s.Push(WeaponSculpt.Rx(90*Deg));s.Torus(flesh,new Vector3(0,0,.32f),t*.62f,.09f,12,5);s.Pop();
                s.Box(iron,new Vector3(0,-.12f,t*.5f+.04f),new Vector3(t*.7f,.1f,.08f));
                s.Into(NativeStationBossModel.Foot(i));s.Push(WeaponSculpt.Rx(90*Deg));s.Torus(cuff,new Vector3(0,.0f,-.12f),.2f,.06f,10,4);s.Pop();
                s.Into(NativeStationBossModel.Knee(i));s.Box(brass,new Vector3(0,.02f,t*.66f+.012f),new Vector3(.1f,.1f,.02f));
            }
        }

        // The grime in the vertex colours: no geometry, so every tier has it.
        private static void Grime(WeaponMesh mesh)
        {
            foreach(var sec in mesh.Sections)
            {
                int bone=sec.Bone;
                for(int v=0;v<sec.Positions.Count;v++)
                {
                    var e=sec.Emission[v];if(e.X+e.Y+e.Z>1e-4f)continue;var c=sec.Colors[v];var p=sec.Positions[v];float k=1;
                    if(bone==NativeStationBossModel.Body||bone==NativeStationBossModel.Roof||(bone>=WindowFirst&&bone<=Markers))
                    {
                        var q=p+(bone==NativeStationBossModel.Body?Vector3.Zero:Body(bone));
                        // Dirt rising from the underframe (darkest at the solebars), soot along the roof line, the rear end
                        // scorched where the carriage tore, the nose's plough and lower edge worn black.
                        k*=Lerp(.58f,1f,Smooth((q.Y+.2f)/1.5f));
                        if(q.Y>2.1f)k*=Lerp(1f,.78f,Smooth((q.Y-2.1f)/.6f));
                        if(q.Z<-3.4f)k*=Lerp(1f,.7f,Smooth((-3.4f-q.Z)/1.0f));
                        if(q.Z>4.2f&&q.Y<.6f)k*=.8f;
                        // Soot streaks trailing back along the roof from the horns and the hatch.
                        if(q.Y>2.55f){float band=(float)Math.Abs(Math.Sin(q.X*5.1f+1.3f));k*=Lerp(.82f,1f,band);}
                    }
                    else if(bone>=NativeStationBossModel.HipFirst&&bone<NativeStationBossModel.KneeFirst+NativeStationBossModel.Legs)
                    {
                        // The limbs darker toward the hands and in the creases at the joints.
                        bool shin=bone>=NativeStationBossModel.KneeFirst;int leg=shin?bone-NativeStationBossModel.KneeFirst:bone-NativeStationBossModel.HipFirst;
                        float len=shin?NativeStationBossModel.Shin(leg):NativeStationBossModel.Thigh(leg),u=Math.Max(0,Math.Min(1,-p.Y/len));
                        k*=shin?Lerp(.95f,.66f,u):Lerp(.82f,1f,Smooth(u*4))*Lerp(1f,.86f,Smooth((u-.8f)*5));
                    }
                    else if(bone>=NativeStationBossModel.FootFirst&&bone<NativeStationBossModel.FootFirst+NativeStationBossModel.Legs)k*=Lerp(.62f,.85f,Smooth((p.Y+.15f)/.25f));
                    if(k!=1)sec.Colors[v]=new Vector4(c.X*k,c.Y*k,c.Z*k,c.W);
                }
            }
        }
        // A body-parented joint's rest offset (its geometry back in the body frame for the grime).
        private static Vector3 Body(int bone){var o=Vector3.Zero;int b=bone;while(b>=0&&b!=NativeStationBossModel.Body){o+=Rest[b];b=Parent[b];}return o;}
        private static float Lerp(float a,float b,float t)=>a+(b-a)*Math.Max(0,Math.Min(1,t));
        private static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
    }
}
