using System;
using System.Globalization;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The last enemies of a kill quota, marked so players can find them (owner 2026-10-18: "We need some kind of indicator for the
    /// last enemies of a quota (Like maybe last 5?) In case they get stuck so players know where to find them"). Pure rules, no
    /// UnityEngine; Runtime/NativeQuotaMarksPresentation.cs draws them on the in-scene HUD.
    /// <list type="bullet">
    /// <item>Trigger (<see cref="Of"/>): the viewer's current building or zone (an Expedition instance: Ward Zero, Central Station,
    /// City Ironworks all use instances[].progress / target) has a kill quota running and no boss yet, and 1 to <see cref="Threshold"/>
    /// kills remain. Off when the quota is met, the boss phase starts (bosses[] or a boss arrival in the area), the viewer leaves
    /// the area (phase is no longer Instance), or the view is loading or frozen for a host transfer.</item>
    /// <item>Which (<see cref="QuotaMarkSet"/>): every live hostile horde add in the presented area, the <see cref="Cap"/> nearest the
    /// viewer. Corpses, hub/town zombies, the boss (and its summons), DEV monsters and adds still queued at their entry are never
    /// marked.</item>
    /// <item>World marker (<see cref="Marker"/>): a downward kite <see cref="HeadClear"/> above the kind's head (the Hulk's 3.06 m
    /// too), drawn over the scene so walls never hide it, a gentle <see cref="PulsePeriod"/> pulse (none with Reduced effects),
    /// a distance label in metres, sized by distance between <see cref="SizeFar"/> and <see cref="SizeNear"/> UI px, hidden within
    /// <see cref="CloseHide"/> m while the add is plainly in sight, and faint over the crosshair.</item>
    /// <item>Off screen (<see cref="Arrow"/>): an arrow on a rounded rectangle inset from the screen's edges pointing to each marked
    /// add that is off screen or behind the camera, with its distance; arrows that would sit on top of each other are spread
    /// apart (<see cref="QuotaArrowSet.Spread"/>).</item>
    /// <item>HUD line (<see cref="Tag"/>): "LAST n · MARKED" under the quota while the markers are on. Cue (<see cref="QuotaMarkCue"/>):
    /// one soft ping when they first turn on, once per quota.</item>
    /// </list>
    /// Presentation only: it reads the replicated view and the adds every peer already holds and decides nothing for the
    /// authority (no rule, AI, spawn, director, network or hit change). Fixed buffers, no allocation per frame.
    /// </summary>
    public static class NativeQuotaMarks
    {
        // ---- the trigger ----------------------------------------------------------------------------------------------------
        /// <summary>The markers turn on when this many kills or fewer remain (and at least one).</summary>
        public const int Threshold=5;
        /// <summary>At most this many adds are marked: the nearest.</summary>
        public const int Cap=8;

        /// <summary>The quota the markers follow this frame.</summary>
        public struct Quota
        {
            /// <summary>The markers are on: 1..Threshold kills remain, no boss yet, the viewer inside.</summary>
            public bool Active;
            /// <summary>Kills left (target - progress), the counts, and the quota's identity (run, loop, the instance area).</summary>
            public int Remaining,Progress,Target,Loop;public string Run,Area;
        }
        /// <summary>
        /// The viewer's running kill quota, read from the replicated view only: the current instance (phase Instance, the viewer's own
        /// area), not cleared, no boss started (bosses[] empty and no boss arrival recorded in the area), a positive target. Active
        /// while 1..<see cref="Threshold"/> kills remain. Loading and a host transfer turn it off.
        /// </summary>
        public static Quota Of(ExpeditionView v)
        {
            var q=default(Quota);
            if(v==null||v.loading||v.migrating||v.phase!=ExpeditionPhase.Instance||v.instances==null)return q;
            var b=v.CurrentBuilding();if(b==null||b.cleared||b.target<=0)return q;
            if(!string.IsNullOrEmpty(v.area)&&!string.Equals(b.id,v.area,StringComparison.Ordinal))return q;
            q.Progress=b.progress;q.Target=b.target;q.Remaining=b.target-b.progress;q.Run=v.run;q.Loop=v.loop;q.Area=b.id;
            if(b.bosses!=null&&b.bosses.Length>0)return q;
            if(v.bossArrivals!=null)foreach(var a in v.bossArrivals)if(a!=null&&string.Equals(a.area,b.id,StringComparison.Ordinal))return q;
            q.Active=q.Remaining>=1&&q.Remaining<=Threshold;
            return q;
        }

        // ---- which adds -----------------------------------------------------------------------------------------------------
        /// <summary>Whether a candidate is marked at all: alive, a hostile horde add of the presented area, spawned into the world,
        /// not a hub/town zombie, not the boss or one of its summons.</summary>
        public static bool Eligible(in QuotaCandidate c,int area)=>c.Alive&&c.Hostile&&c.Spawned&&!c.Hub&&!c.Boss&&c.Area==area;

        // ---- the world marker -------------------------------------------------------------------------------------------------
        /// <summary>The marker floats this far over the top of the head (m).</summary>
        public const float HeadClear=.6f;
        /// <summary>How far the top of the head stands above the add's root (the body's centre, where the rig root stands): the
        /// upright kinds' world capsule over their root (the Hulk's 3.06 m capsule: 1.53 m), the crawler's low body (its capsule lies
        /// along its facing), an add crawling on a lost leg its crawl capsule's half height.</summary>
        public static float HeadAbove(int kind,bool crawl)
        {
            if(crawl)return NativeHordeKinds.CrawlHeight*.5f;
            int k=kind<0||kind>=NativeHordeKinds.All?NativeHordeKinds.Shambler:kind;
            if(NativeHordeKinds.Axis[k]==2)return NativeHordeKinds.WorldRadius(k);
            return NativeHordeKinds.WorldHeight(k)-NativeHordeKinds.RootHeight(k,false);
        }
        /// <summary>The marker's anchor (world, y up): <see cref="HeadClear"/> over the top of the head above the add's root.</summary>
        public static Vector3 Anchor(Vector3 root,int kind,bool crawl)=>new Vector3(root.X,root.Y+HeadAbove(kind,crawl)+HeadClear,root.Z);

        /// <summary>The marker's size (UI px of the 900-tall HUD canvas): <see cref="SizeNear"/> within <see cref="NearAt"/> m, easing
        /// down (in log distance) to <see cref="SizeFar"/> by <see cref="FarAt"/> m: readable far away, never huge up close.</summary>
        public const float SizeNear=28,SizeFar=15,NearAt=6,FarAt=48;
        public static float Size(float distance)
        {
            if(!(distance>NearAt))return SizeNear;if(distance>=FarAt)return SizeFar;
            float t=(float)(Math.Log(distance/NearAt)/Math.Log(FarAt/NearAt));return SizeNear+(SizeFar-SizeNear)*Smooth(t);
        }
        /// <summary>The pulse (s per beat) and how much it grows the marker and lifts its alpha.</summary>
        public const float PulsePeriod=1.2f,PulseGrow=.12f,PulseLow=.72f,SteadyAlpha=.92f;
        /// <summary>The pulse wave at time t (0..1, a cosine beat every <see cref="PulsePeriod"/>); 0 with Reduced effects (no pulse).</summary>
        public static float Pulse(double t,bool reduced)
        {
            if(reduced||double.IsNaN(t)||double.IsInfinity(t))return 0;double p=t/PulsePeriod;p-=Math.Floor(p);return (float)(.5-.5*Math.Cos(p*2*Math.PI));
        }
        /// <summary>Close fights stay clear: within <see cref="CloseHide"/> m a marked add in plain sight shows no marker, fading back in
        /// by <see cref="CloseShow"/> m; behind a wall (not visible) it keeps its marker at any range.</summary>
        public const float CloseHide=4,CloseShow=6;
        public static float CloseAlpha(float distance,bool visible)=>!visible?1:Smooth((distance-CloseHide)/(CloseShow-CloseHide));
        /// <summary>Over the crosshair (within <see cref="AimClear"/> of the screen height round the centre) the marker is faint
        /// (<see cref="AimFaint"/>), back to full by <see cref="AimOpen"/>.</summary>
        public const float AimClear=.03f,AimOpen=.075f,AimFaint=.55f;
        public static float AimAlpha(float vx,float vy,float aspect)
        {
            float x=(vx-.5f)*Math.Max(.5f,aspect),y=vy-.5f,r=(float)Math.Sqrt(x*x+y*y);return AimFaint+(1-AimFaint)*Smooth((r-AimClear)/(AimOpen-AimClear));
        }
        /// <summary>The markers fade in over this long when they turn on (s); they leave at once (the quota met, the area left).</summary>
        public const float FadeIn=.3f;

        /// <summary>The on-screen box (viewport fractions in from each edge): a marker outside it, or behind the camera, is shown by
        /// its edge arrow instead.</summary>
        public const float ScreenX=.02f,ScreenY=.03f;
        public static bool OnScreen(float vx,float vy,float vz)=>vz>.05f&&vx>=ScreenX&&vx<=1-ScreenX&&vy>=ScreenY&&vy<=1-ScreenY;

        /// <summary>
        /// One marked add's world marker for this frame, from its anchor's viewport point (vx, vy: 0..1, y up; vz the view depth,
        /// negative behind the camera) on a screen `aspect` wide per its height, its distance from the viewer (m), whether it is in
        /// plain sight, the presentation time, Reduced effects and the set's fade level (0..1).
        /// </summary>
        public static QuotaMarker Marker(float vx,float vy,float vz,float aspect,float distance,bool visible,double time,bool reduced,float level)
        {
            var m=new QuotaMarker{OnScreen=OnScreen(vx,vy,vz),X=vx,Y=vy};
            float wave=Pulse(time,reduced);m.Pulse=wave;
            m.Size=Size(distance)*(1+PulseGrow*wave);
            float beat=reduced?SteadyAlpha:PulseLow+(1-PulseLow)*wave;
            m.Alpha=Clamp01(level)*beat*CloseAlpha(distance,visible)*AimAlpha(vx,vy,aspect);
            m.Label=m.Alpha>.12f;return m;
        }

        /// <summary>A marker's footprint for <see cref="Declutter"/> (UI px): at least this wide (its metres label), and the label's
        /// height and gap above the kite.</summary>
        public const float FootprintWidth=34,LabelRise=17,StackGap=2;
        /// <summary>
        /// Keeps markers that would overlap on screen apart (adds lined up behind each other): nearest first (the order of the marks),
        /// a farther marker whose kite and label would overlap a nearer one's is lifted straight up until it clears (so it still
        /// points along its column). Markers off screen or invisible are left alone. `markers` in viewport units on a W x H UI
        /// canvas; fixed work, no allocation.
        /// </summary>
        public static void Declutter(QuotaMarker[] markers,int count,float W,float H)
        {
            if(markers==null||!(W>0)||!(H>0))return;count=Math.Min(count,markers.Length);
            for(int i=1;i<count;i++)
            {
                var m=markers[i];if(!m.OnScreen||m.Alpha<=.01f)continue;
                float x=m.X*W,bottom=(1-m.Y)*H+m.Size*.5f,w=Math.Max(m.Size,FootprintWidth),h=m.Size+LabelRise;bool lifted=false;
                for(int pass=0;pass<count;pass++)
                {
                    bool moved=false;
                    for(int j=0;j<i;j++)
                    {
                        var n=markers[j];if(!n.OnScreen||n.Alpha<=.01f)continue;
                        float nx=n.X*W,nb=(1-n.Y)*H+n.Size*.5f,nw=Math.Max(n.Size,FootprintWidth),nh=n.Size+LabelRise;
                        if(Math.Abs(x-nx)>=(w+nw)*.5f||bottom<=nb-nh||bottom-h>=nb)continue;
                        bottom=nb-nh-StackGap;moved=lifted=true;
                    }
                    if(!moved)break;
                }
                if(lifted){m.Y=1-(bottom-m.Size*.5f)/H;m.Lifted=true;markers[i]=m;}
            }
        }

        // ---- the edge arrows --------------------------------------------------------------------------------------------------
        /// <summary>The rounded rectangle the arrows ride (fractions of the screen height in from the sides, top and bottom: clear of
        /// the objective tag and LAST tag above and the cooldown cards below), its corner roundness (superellipse exponent), and how
        /// far a target behind the camera is pushed toward the bottom (turn round).</summary>
        public const float InsetSide=.06f,InsetTop=.14f,InsetBottom=.18f,Roundness=4,BehindDrop=.4f;
        /// <summary>Arrows closer than this (radians round the screen centre) are spread apart.</summary>
        public const float MinGap=.16f;
        /// <summary>The arrow's length (UI px): larger for nearer adds.</summary>
        public const float ArrowNear=26,ArrowFar=19,ArrowNearAt=8,ArrowFarAt=45;
        public static float ArrowSize(float distance)=>ArrowNear+(ArrowFar-ArrowNear)*Smooth((distance-ArrowNearAt)/(ArrowFarAt-ArrowNearAt));
        /// <summary>The direction (radians, 0 right, counter-clockwise, y up) from the screen centre toward a target at viewport
        /// (vx, vy, vz) on a screen `aspect` wide: behind the camera the projection is mirrored, so the side is flipped back and the
        /// arrow leans toward the bottom (turn round).</summary>
        public static float Direction(float vx,float vy,float vz,float aspect)
        {
            float ex=(vx-.5f)*Math.Max(.5f,aspect),ey=vy-.5f;if(vz<=0){ex=-ex;ey=-(Math.Abs(ey)+BehindDrop);}
            if(Math.Abs(ex)+Math.Abs(ey)<1e-5f)ey=-1;return (float)Math.Atan2(ey,ex);
        }
        /// <summary>Where an arrow pointing along `angle` sits (viewport, y up) on the inset rounded rectangle of a screen `aspect`
        /// wide: the ray from the rectangle's centre along the angle meets its edge.</summary>
        public static void Place(float angle,float aspect,out float x,out float y)
        {
            aspect=Math.Max(.5f,aspect);float cx=aspect*.5f,cy=(InsetBottom+1-InsetTop)*.5f,rx=Math.Max(.05f,aspect*.5f-InsetSide),ry=Math.Max(.05f,(1-InsetTop-InsetBottom)*.5f);
            double dx=Math.Cos(angle),dy=Math.Sin(angle),k=Math.Pow(Math.Pow(Math.Abs(dx)/rx,Roundness)+Math.Pow(Math.Abs(dy)/ry,Roundness),-1/Roundness);
            x=(float)((cx+dx*k)/aspect);y=(float)(cy+dy*k);
        }
        /// <summary>The edge arrow for a marked add seen at viewport (vx, vy, vz), `distance` m away, with the set's fade level.</summary>
        public static QuotaArrow Arrow(float vx,float vy,float vz,float aspect,float distance,double time,bool reduced,float level,int mark)
        {
            var a=new QuotaArrow{Mark=mark,Angle=Direction(vx,vy,vz,aspect),Size=ArrowSize(distance),Behind=vz<=0};
            float wave=Pulse(time,reduced);a.Alpha=Clamp01(level)*(reduced?SteadyAlpha:.8f+.2f*wave);
            Place(a.Angle,aspect,out a.X,out a.Y);return a;
        }

        // ---- the HUD line, labels and the cue ----------------------------------------------------------------------------------
        private static readonly string[] tags=Tags();
        private static string[] Tags(){var t=new string[Threshold+1];for(int i=1;i<=Threshold;i++)t[i]="LAST "+i.ToString(CultureInfo.InvariantCulture)+" · MARKED";t[0]="";return t;}
        /// <summary>The HUD line under the quota: "LAST n · MARKED" (n the kills left), cached; "" outside 1..Threshold.</summary>
        public static string Tag(int remaining)=>remaining>=1&&remaining<=Threshold?tags[remaining]:"";
        /// <summary>The longest distance label (m); farther reads as this.</summary>
        public const int MaxMetres=999;
        private static readonly string[] metres=new string[MaxMetres+1];
        /// <summary>A distance label ("23 m", whole metres), cached so a frame builds no string.</summary>
        public static string Metres(float distance)
        {
            int m=float.IsNaN(distance)||distance<0?0:distance>=MaxMetres?MaxMetres:(int)Math.Round(distance,MidpointRounding.AwayFromZero);
            return metres[m]??(metres[m]=m.ToString(CultureInfo.InvariantCulture)+" m");
        }
        /// <summary>Fills every distance label once (the first frame then builds none either).</summary>
        public static void WarmLabels(){for(int i=0;i<=MaxMetres;i++)Metres(i);}

        /// <summary>The cue's bank key; without a recorded clip it plays the built-in synthesized ping (<see cref="PingSamples"/>).</summary>
        public const string CueKey="expedition.quota.mark";
        public const int PingRate=48000;public const float PingGain=.42f;
        /// <summary>
        /// The built-in ping (original, synthesized: no LocalAssets clip needed): a soft sonar note at B5 with a quiet fifth, 4 ms in
        /// and a 1.1 s exponential tail, and a fainter echo a sixth higher .16 s later. Peak below .6, click-free ends.
        /// </summary>
        public static float[] PingSamples()
        {
            int count=(int)(PingRate*1.1);var data=new float[count];
            for(int i=0;i<count;i++)
            {
                double t=i/(double)PingRate,tail=Math.Min(1,(count-1-i)/(PingRate*.03));
                double a=Math.Min(1,t/.004)*Math.Exp(-5.2*t),e=t<.16?0:Math.Min(1,(t-.16)/.004)*Math.Exp(-6.5*(t-.16));
                double p=2*Math.PI*987.77*t,q=2*Math.PI*1318.51*t;
                data[i]=(float)(tail*(.42*a*(Math.Sin(p)+.18*Math.Sin(p*1.5))+.16*e*Math.Sin(q)));
            }
            return data;
        }

        // ---- shapes (24-unit SVG markup, rasterized once per size by the HUD's icon cache) ---------------------------------------
        /// <summary>The marker: a downward kite (its tip at the head) as a translucent body, an edge and a solid core.</summary>
        public const string MarkerBody="<path d=\"M12 2.5L20.5 9.2L12 21.5L3.5 9.2Z\" fill=\"#fff\"/>",
            MarkerEdge="<path d=\"M12 2.5L20.5 9.2L12 21.5L3.5 9.2Z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.9\"/>",
            MarkerCore="<path d=\"M12 6.6L16.3 9.6L12 16L7.7 9.6Z\" fill=\"#fff\"/>";
        /// <summary>The edge arrow: a dart pointing right (+x), rotated to its angle.</summary>
        public const string ArrowDart="<path d=\"M21.5 12L5 3.5L9.2 12L5 20.5Z\" fill=\"#fff\"/>";
        /// <summary>The raster box the shapes are drawn in (UI px; a transform scales it to the marker's size).</summary>
        public const float ShapeBox=32;

        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float t){t=Clamp01(t);return t*t*(3-2*t);}

        /// <summary>
        /// A world point's viewport position as Unity's Camera.WorldToViewportPoint gives it (x, y 0..1 bottom-left, z the depth along
        /// the view; behind the camera the x/y are mirrored), for a camera at `eye` looking along `forward` with `up`, a vertical
        /// field of view (degrees) and aspect (width / height). For the checks and offline renders; the game asks its camera.
        /// </summary>
        public static Vector3 Viewport(Vector3 eye,Vector3 forward,Vector3 up,float fovDegrees,float aspect,Vector3 point)
        {
            var f=Vector3.Normalize(forward);var r=Vector3.Normalize(Vector3.Cross(up,f));var u=Vector3.Cross(f,r);var d=point-eye;
            float z=Vector3.Dot(d,f),x=Vector3.Dot(d,r),y=Vector3.Dot(d,u),t=(float)Math.Tan(fovDegrees*.5*Math.PI/180);
            float w=Math.Abs(z)<1e-6f?1e-6f:z;return new Vector3(.5f+.5f*x/(w*t*aspect),.5f+.5f*y/(w*t),z);
        }
    }

    /// <summary>One add the presentation holds this frame (the Runtime fills these from the presented arena's minions).</summary>
    public struct QuotaCandidate
    {
        public int Id,Kind;
        /// <summary>The add's root (world, y up): its presented root where its rig stands (the body's centre).</summary>
        public Vector3 Root;
        /// <summary>The area token it belongs to (the Runtime gives every add of the presented arena that area's token).</summary>
        public int Area;
        public bool Crawl,Alive,Hostile,Spawned,Hub,Boss;
    }
    /// <summary>One marked add: its id, kind, root, the marker's anchor and its distance from the viewer (m).</summary>
    public struct QuotaMark{public int Id,Kind;public Vector3 Root,Anchor;public float Distance;}
    /// <summary>A marker drawn this frame: on screen at viewport (X, Y), `Size` UI px, `Alpha`, the pulse wave and whether its
    /// distance label shows; `Lifted` when <see cref="NativeQuotaMarks.Declutter"/> raised it clear of a nearer one.</summary>
    public struct QuotaMarker{public bool OnScreen,Label,Lifted;public float X,Y,Size,Alpha,Pulse;}
    /// <summary>An edge arrow at viewport (X, Y) pointing along `Angle` (radians, 0 right, counter-clockwise), `Size` UI px long,
    /// for mark index `Mark`; `Behind` when the add is behind the camera.</summary>
    public struct QuotaArrow{public float X,Y,Angle,Size,Alpha;public int Mark;public bool Behind;}

    /// <summary>
    /// The marked adds this frame: the <see cref="NativeQuotaMarks.Cap"/> nearest eligible adds (ties by id), nearest first, and the
    /// set's fade level. Fixed arrays, no allocation.
    /// </summary>
    public sealed class QuotaMarkSet
    {
        public readonly QuotaMark[] Marks=new QuotaMark[NativeQuotaMarks.Cap];
        /// <summary>How many are marked (at most Cap) and how many were eligible before the cap.</summary>
        public int Count{get;private set;}
        public int Eligible{get;private set;}
        /// <summary>The fade level (0..1): rises over <see cref="NativeQuotaMarks.FadeIn"/> while on, 0 at once when off.</summary>
        public float Level{get;private set;}
        public void Clear(){Count=0;Eligible=0;Level=0;}
        /// <summary>Steps the fade: `on` the trigger this frame, `dt` real seconds.</summary>
        public float Fade(bool on,float dt)
        {
            if(!on){Level=0;return 0;}
            if(!(dt>0))return Level;Level=Math.Min(1,Level+dt/NativeQuotaMarks.FadeIn);return Level;
        }
        /// <summary>Picks the nearest eligible candidates of `area` to `from` (the viewer) among the first `count` of `list`.</summary>
        public void Select(QuotaCandidate[] list,int count,int area,Vector3 from)
        {
            Count=0;Eligible=0;if(list==null)return;count=Math.Min(count,list.Length);
            for(int i=0;i<count;i++)
            {
                if(!NativeQuotaMarks.Eligible(list[i],area))continue;Eligible++;
                var c=list[i];float d=Vector3.Distance(c.Root,from);if(float.IsNaN(d))continue;
                int at=Count;while(at>0&&Closer(d,c.Id,Marks[at-1]))at--;
                if(at>=NativeQuotaMarks.Cap)continue;
                for(int k=Math.Min(Count,NativeQuotaMarks.Cap-1);k>at;k--)Marks[k]=Marks[k-1];
                Marks[at]=new QuotaMark{Id=c.Id,Kind=c.Kind,Root=c.Root,Anchor=NativeQuotaMarks.Anchor(c.Root,c.Kind,c.Crawl),Distance=d};
                if(Count<NativeQuotaMarks.Cap)Count++;
            }
        }
        private static bool Closer(float d,int id,in QuotaMark m)=>d<m.Distance||d==m.Distance&&id<m.Id;
    }

    /// <summary>
    /// The edge arrows this frame (one per marked add off screen or behind the camera), spread apart so none sits on another.
    /// Fixed arrays, no allocation.
    /// </summary>
    public sealed class QuotaArrowSet
    {
        public readonly QuotaArrow[] Arrows=new QuotaArrow[NativeQuotaMarks.Cap];
        public int Count{get;private set;}
        private readonly int[] order=new int[NativeQuotaMarks.Cap],first=new int[NativeQuotaMarks.Cap],size=new int[NativeQuotaMarks.Cap];
        private readonly double[] angles=new double[NativeQuotaMarks.Cap],unwrapped=new double[NativeQuotaMarks.Cap],sum=new double[NativeQuotaMarks.Cap];
        public void Clear()=>Count=0;
        public void Add(in QuotaArrow a){if(Count<Arrows.Length)Arrows[Count++]=a;}
        private static double Wrap(double x){const double Tau=Math.PI*2;x%=Tau;if(x>Math.PI)x-=Tau;else if(x<=-Math.PI)x+=Tau;return x;}
        /// <summary>
        /// Spreads arrows closer than <see cref="NativeQuotaMarks.MinGap"/> round the screen centre apart, then places each on the
        /// inset rounded rectangle of a screen `aspect` wide. Exact and deterministic: the circle is cut at its widest gap, and runs of
        /// too-close arrows merge into evenly spaced clusters centred on their members' mean bearing (the least movement).
        /// </summary>
        public void Spread(float aspect)
        {
            int n=Count;if(n==0)return;const double Tau=Math.PI*2;double g=NativeQuotaMarks.MinGap;
            for(int i=0;i<n;i++){order[i]=i;angles[i]=Wrap(Arrows[i].Angle);}
            if(n>1&&n*g<Tau)
            {
                for(int i=1;i<n;i++){int j=i;while(j>0&&angles[order[j-1]]>angles[order[j]]){(order[j-1],order[j])=(order[j],order[j-1]);j--;}}
                // Cut the circle after its widest gap, so the run starts there and increases.
                int start=0;double widest=-1;
                for(int i=0;i<n;i++){double gap=(i+1<n?angles[order[i+1]]:angles[order[0]]+Tau)-angles[order[i]];if(gap>widest){widest=gap;start=(i+1)%n;}}
                for(int k=0;k<n;k++){int i=start+k;unwrapped[k]=angles[order[i%n]]+(i>=n?Tau:0);}
                // Clusters on a stack: each holds its first index, size and the sum of (bearing - offset in it); its first arrow sits at
                // sum / size. A cluster that reaches the next one merges with it.
                int top=0;
                for(int k=0;k<n;k++)
                {
                    first[top]=k;size[top]=1;sum[top]=unwrapped[k];top++;
                    while(top>1&&sum[top-2]/size[top-2]+size[top-2]*g>sum[top-1]/size[top-1]-1e-9)
                    {sum[top-2]+=sum[top-1]-size[top-1]*size[top-2]*g;size[top-2]+=size[top-1];top--;}
                }
                for(int c=0;c<top;c++){double at=sum[c]/size[c];for(int k=0;k<size[c];k++)angles[order[(start+first[c]+k)%n]]=Wrap(at+k*g);}
            }
            for(int i=0;i<n;i++){var a=Arrows[i];a.Angle=(float)angles[i];NativeQuotaMarks.Place(a.Angle,aspect,out a.X,out a.Y);Arrows[i]=a;}
        }
    }

    /// <summary>
    /// The markers' cue: true once when they first turn on for a quota (run, loop, instance area), never on a count change, and not
    /// again when the viewer leaves the area and comes back to the same quota. The last 16 quotas are remembered by reference to
    /// the view's strings (no allocation).
    /// </summary>
    public sealed class QuotaMarkCue
    {
        private readonly string[] runs=new string[16],areas=new string[16];private readonly int[] loops=new int[16];private int heard,next;
        private bool on;private string onRun,onArea;private int onLoop;
        public void Reset(){heard=next=0;on=false;onRun=onArea=null;Array.Clear(runs,0,runs.Length);Array.Clear(areas,0,areas.Length);}
        public bool Step(in NativeQuotaMarks.Quota q)
        {
            if(!q.Active){on=false;return false;}
            if(on&&onLoop==q.Loop&&string.Equals(onRun,q.Run,StringComparison.Ordinal)&&string.Equals(onArea,q.Area,StringComparison.Ordinal))return false;
            on=true;onRun=q.Run;onArea=q.Area;onLoop=q.Loop;
            for(int i=0;i<heard;i++)if(loops[i]==q.Loop&&string.Equals(runs[i],q.Run,StringComparison.Ordinal)&&string.Equals(areas[i],q.Area,StringComparison.Ordinal))return false;
            runs[next]=q.Run;areas[next]=q.Area;loops[next]=q.Loop;next=(next+1)%runs.Length;if(heard<runs.Length)heard++;
            return true;
        }
    }
}
