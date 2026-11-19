using System.Threading.Tasks;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The cradle card (Core NativeCradleCard; docs/EXPEDITION-OVERHAUL.md R8 and W3): a pod run's loading screen as the inside of my
    /// pod, in the cyber HUD kit warmed to the cabin's red: the porthole (cloud streaks rushing up past it, lightning now and then),
    /// the run's map in a night palette (the station's holo map, NativeDeployPresentation.Preview, recoloured; each district breathing
    /// at its entrance; each pod's streak drawn in to its landing spot), the altimeter falling with the load, a lamp per party member
    /// (READY, LOADING 64%, LOST), a warning lamp per active mutator and the station's last line. Drawn by the station's console under
    /// its fade (the eject's end, before the load) and by the stage loading screen for a pod run's first load (Runtime
    /// NativeStageLoadingUI.cs), which lifts it away when the hub's countdown starts. Reduced effects: no streak motion, lightning,
    /// breathing or lift. Repaint only; nothing allocates per frame once the map's texture is built (on a worker, once per state).
    /// </summary>
    public static class NativeCradleHud
    {
        private static readonly float[] DiscAt={0f,.965f,1f},DiscAlpha={1f,1f,0f},RimAt={0f,.5f,.82f,1f},RimAlpha={0f,0f,.55f,1f},GlowAt={0f,.45f,1f},GlowAlpha={1f,.35f,0f},
            RingAt={0f,.78f,.86f,.94f,1f},RingAlpha={0f,0f,1f,1f,0f};
        private static readonly string[] tapeLabels=MakeTape();
        private static string[] MakeTape(){var s=new string[26];for(int i=0;i<s.Length;i++)s[i]=(i*500).ToString("N0",System.Globalization.CultureInfo.InvariantCulture);return s;}
        private const int Red=0xff3a30,Amber=0xffb02e,Green=0x6dffa8,Steel=0x2a2f3a,Glass=0x081226;
        private static Color X(int hex,float a)=>U.Hex(hex,a);
        private static void Disc(Rect r,Color c)=>U.RadialStops(r,c,DiscAt,DiscAlpha);
        private static Rect Around(Vector2 c,float r)=>new Rect(c.x-r,c.y-r,2*r,2*r);

        // ---- the night map (built on a worker per state, uploaded on the main thread) ----
        public const int MapWidth=200,MapHeight=152;
        private static Texture2D map;private static Task<int[]> mapJob;private static ulong mapAsked=ulong.MaxValue,mapShown=ulong.MaxValue;
        private static Texture2D MapOf(ExpeditionDeploymentState s)
        {
            if(s!=null&&s.worldRolled)
            {
                ulong key=((ulong)s.worldSeed<<32)|(uint)s.revision;
                if(key!=mapAsked&&mapJob==null){mapAsked=key;var copy=s.Copy();mapJob=Task.Run(()=>NativeCradleCard.NightMap(copy,MapWidth,MapHeight));}
            }
            if(mapJob!=null&&mapJob.IsCompleted)
            {
                if(mapJob.Status==TaskStatus.RanToCompletion&&mapAsked!=mapShown)
                {
                    var px=mapJob.Result;var c=new Color32[MapWidth*MapHeight];
                    // the map's row 0 is the north edge, the texture's row 0 the south
                    for(int j=0;j<MapHeight;j++)for(int i=0;i<MapWidth;i++){int v=px[j*MapWidth+i];c[(MapHeight-1-j)*MapWidth+i]=new Color32((byte)((v>>16)&255),(byte)((v>>8)&255),(byte)(v&255),255);}
                    if(map==null)map=new Texture2D(MapWidth,MapHeight,TextureFormat.RGBA32,false,false){name="Cradle night map",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                    map.SetPixels32(c);map.Apply(false,false);mapShown=mapAsked;
                }
                mapJob=null;
            }
            return mapShown==mapAsked?map:null;
        }

        /// <summary>
        /// The card over a W x Hh canvas at content alpha `a` (the screen's fade or the loading screen's content), lifted by `lift`
        /// (0..1, the reveal: it rises off the screen as it fades). `title` names the run (null: none), `step` the line under it.
        /// </summary>
        public static void Draw(NativeCradleCard card,ExpeditionDeploymentState s,string title,string step,float W,float Hh,float a,float lift,float now,bool reduced)
        {
            if(Event.current.type!=EventType.Repaint||a<=0||card==null)return;var tex=MapOf(s);
            float rise=reduced?0:lift*lift*Hh*.55f;U.PushAffine(new Vector2(0,-rise),Vector2.right,Vector2.up);
            try
            {
                // the cabin: near black, the red of its lamp bleeding in from the edges, a slow throb
                float throb=reduced?.8f:.75f+.25f*Mathf.Sin(now*2.3f);
                U.Fill(new Rect(0,0,W,Hh+rise),X(0x040407,a));U.RadialStops(new Rect(-W*.15f,-Hh*.2f,W*1.3f,Hh*1.4f),X(0x5a0c0a,.85f*a*throb),RimAt,RimAlpha);
                float x0=U.SafeX+Mathf.Max(28,U.SafeW*.05f),x1=U.SafeX+U.SafeW-Mathf.Max(28,U.SafeW*.05f),w=x1-x0;
                // the frame: tick rules along the top and foot, rivets at the corners
                for(int i=0;i<2;i++){float y=i==0?30:Hh-26;U.Fill(new Rect(x0,y,w,H.Snap(1)),X(Red,.35f*a));U.Fill(new Rect(x0,y-H.Px,46,H.Snap(2)),X(Red,.9f*a));U.Fill(new Rect(x1-16,y-H.Px,16,H.Snap(2)),X(Amber,.8f*a));}
                for(int p=0;p<2;p++)for(int q=0;q<2;q++)Disc(Around(new Vector2(p==0?x0+8:x1-8,q==0?44:Hh-40),3.2f),X(0x5a6070,.9f*a));
                // the eyebrow, the run's name and the step
                U.Text(new Rect(x0,44,w*.5f,16),NativeCradleCard.Eyebrow,12,X(Red,.95f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f);
                if(!string.IsNullOrEmpty(title))U.Text(new Rect(x0,62,w*.5f,44),title,36,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:.05f,glow:X(Red,.35f*a),shadow:C.Void(.8f*a));
                if(!string.IsNullOrEmpty(step))U.Text(new Rect(x0,106,w*.5f,18),step,12,X(Amber,.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
                // the porthole
                float pr=Mathf.Min(Hh*.24f,w*.17f);var pc=new Vector2(x0+w*.2f,Hh*.47f);Porthole(pc,pr,a,now,reduced);
                // the map and the altimeter
                float mw=w*.42f,mh=mw*MapHeight/MapWidth;if(mh>Hh*.5f){mh=Hh*.5f;mw=mh*MapWidth/MapHeight;}
                var mr=new Rect(x0+w*.44f,Hh*.47f-mh*.5f-10,mw,mh);Map(mr,card,s,tex,a,now,reduced);
                Altimeter(new Rect(mr.xMax+26,mr.y,56,mh),card.Load,a);
                // the mutators' warning lamps under the map
                Mutators(new Rect(mr.x,mr.yMax+14,mw+82,20),s,a,now,reduced);
                // the squad's lamps, the hold line, the station's last line and the load's bar
                Lamps(new Rect(x0,Hh*.79f-26,w,52),card,a,now,reduced);
                if(!string.IsNullOrEmpty(card.Hold))U.Text(new Rect(x0,Hh*.79f+32,w,18),card.Hold,12,X(Amber,.8f*a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.16f);
                U.Text(new Rect(x0,Hh-62,w,26),NativeCradleCard.LastLine,18,X(0xffd8c8,a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.32f,glow:X(Red,.5f*a));
                var bar=new Rect(x0,Hh-30,w,3);U.Fill(bar,C.Navy(.9f*a));float f=Mathf.Clamp01(card.Load);if(f>0){var fill=new Rect(bar.x,bar.y,bar.width*f,bar.height);U.Glow(fill,X(Red,.3f*a),5);U.Fill(fill,X(0xff6a4a,a));}
            }
            finally{U.Pop();}
        }
        // The porthole: a steel ring with bolts, deep glass, cloud streaks rushing up (the pod falling through the deck), lightning.
        private static void Porthole(Vector2 c,float r,float a,float now,bool reduced)
        {
            Disc(Around(c,r*1.2f),X(Steel,a));U.RadialStops(Around(c,r*1.2f),X(0x0c0e14,.8f*a),RingAt,RingAlpha);
            for(int k=0;k<10;k++){float ang=k*Mathf.PI/5;Disc(Around(c+new Vector2(Mathf.Cos(ang),Mathf.Sin(ang))*r*1.1f,r*.035f),X(0x6a7080,a));}
            Disc(Around(c,r),X(Glass,a));
            // the night below glowing through the cloud, then the streaks clipped to the glass's circle
            U.RadialStops(new Rect(c.x-r,c.y-r*.1f,2*r,r*1.2f),X(0x5a2a1e,.55f*a),GlowAt,GlowAlpha);
            const int n=22;
            for(int k=0;k<n;k++)
            {
                NativeCradleCard.Streak(k,n,now,reduced,out float sx,out float sy,out float len,out float op);
                float half=Mathf.Sqrt(Mathf.Max(0,1-sx*sx));float top=Mathf.Max(-half,sy-len),bottom=Mathf.Min(half,sy);if(bottom<=top)continue;
                // screen y grows down: the streak's head (sy) is its top
                float px=c.x+sx*r*.94f,y0=c.y-bottom*r,y1=c.y-top*r,wd=1.2f+2.4f*(1-Mathf.Abs(sx));
                U.Fade(new Rect(px-wd*.5f,y0,wd,y1-y0),new Color(.75f,.82f,1f,op*a),true);
            }
            float flash=NativeCradleCard.Lightning(now,reduced);if(flash>0){Disc(Around(c,r),new Color(.78f,.85f,1f,.55f*flash*a));U.Glow(Around(c,r*.8f),new Color(.7f,.8f,1f,.25f*flash*a),r*.3f);}
            // the glass's glint and the ring's inner lip
            U.RadialStops(new Rect(c.x-r*.62f,c.y-r*.7f,r*.62f,r*.42f),new Color(1,1,1,.08f*a),GlowAt,GlowAlpha);
            U.RadialStops(Around(c,r*1.02f),X(0x000000,.7f*a),RingAt,RingAlpha);
        }
        // The run's map: the night texture, the districts breathing, each pod's streak drawn in to its landing spot (a crater ring).
        private static void Map(Rect r,NativeCradleCard card,ExpeditionDeploymentState s,Texture2D tex,float a,float now,bool reduced)
        {
            H.Panel(new Rect(r.x-10,r.y-26,r.width+20,r.height+36),a,X(Red,1),10,H.Corners.Diagonal,true,.86f);
            U.Text(new Rect(r.x,r.y-22,r.width,14),NativeCradleCard.SquadDrop,10,X(Red,.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.22f);
            if(tex!=null)U.Image(r,tex,new Color(1,1,1,a));else U.Fill(r,X(0x060a14,a));
            if(s?.districts!=null)for(int d=0;d<3&&d<s.districts.Length;d++)
            {
                var dd=s.districts[d];if(dd==null)continue;var uv=NativeCradleCard.MapUv(dd.x*NativeDeployPresentation.CellMetres,dd.z*NativeDeployPresentation.CellMetres);
                var at=new Vector2(r.x+uv.X*r.width,r.y+uv.Y*r.height);float b=NativeCradleCard.Breath(d,now,reduced),glow=r.width*.12f*(.8f+.4f*b);
                U.RadialStops(Around(at,glow),U.Hex(NativeDeployPresentation.DestinationOf(dd.instance,dd.theme).Color,(.25f+.4f*b)*a),GlowAt,GlowAlpha);
            }
            for(int i=0;i<card.Count;i++)
            {
                var spot=card.Spots[i];if(float.IsNaN(spot.X))continue;var to=NativeCradleCard.MapUv(spot.X,spot.Y);var end=new Vector2(r.x+to.X*r.width,r.y+to.Y*r.height);
                var dir=NativeCradleCard.StreakFrom(i);var from=end+new Vector2(dir.X,-dir.Y)*r.width*.32f;float head=reduced?1:Mathf.Repeat(now*.45f+i*.23f,1.25f);
                var col=card.You[i]?X(0xfff0d8,1):X(0xffb48a,1);
                for(int k=0;k<14;k++){float u=k/13f;if(u>head)break;var p=Vector2.Lerp(from,end,u);float sz=1.2f+1.6f*u;U.Fill(new Rect(p.x-sz/2,p.y-sz/2,sz,sz),U.A(col,(.25f+.65f*u)*a));}
                if(head>=1){U.RadialStops(Around(end,6),U.A(col,.9f*a),RingAt,RingAlpha);U.Fill(new Rect(end.x-1.5f,end.y-1.5f,3,3),U.A(col,a));}
            }
        }
        // The altimeter: a tape scrolling up past a fixed reading as the pod falls (12,000 m to 600 m with the load).
        private static void Altimeter(Rect r,float load,float a)
        {
            U.Fill(r,X(0x07090e,.92f*a));H.Outline(r,X(Red,.6f*a),0,H.Corners.None);
            float alt=NativeCradleCard.AltitudeAt(load),span=2500;
            U.BeginBand(r);
            for(int k=0;k<tapeLabels.Length;k++)
            {
                float v=k*500,y=r.center.y-(v-alt)/span*r.height;if(y<r.y-8||y>r.yMax+8)continue;
                U.Fill(new Rect(r.x,y,k%2==0?14:8,H.Snap(1)),X(0xffd8c8,.7f*a));if(k%2==0)U.Text(new Rect(r.x+16,y-7,r.width-18,14),tapeLabels[k],9,X(0xffd8c8,.75f*a),TextAnchor.MiddleLeft,mono:true);
            }
            U.EndBand();
            var mark=new Rect(r.x-6,r.center.y-1,r.width+12,H.Snap(2));U.Fill(mark,X(Amber,a));
            U.Text(new Rect(r.x-40,r.y-20,r.width+80,14),NativeCradleCard.Altitude,9,X(Red,.9f*a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.2f);
            U.Text(new Rect(r.x-40,r.yMax+4,r.width+80,16),NativeCradleCard.AltitudeLabel(load),12,X(Amber,a),TextAnchor.MiddleCenter,bold:true,mono:true);
        }
        // A warning lamp per active mutator (its polarity's colour), or the calm line when there are none.
        private static void Mutators(Rect r,ExpeditionDeploymentState s,float a,float now,bool reduced)
        {
            int n=s?.mutators?.Length??0;U.Text(new Rect(r.x,r.y,120,r.height),n>0?NativeCradleCard.Hazards:NativeCradleCard.NoHazards,10,X(Red,.85f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            float x=r.x+(n>0?96:0);
            for(int i=0;i<n&&i<4;i++)
            {
                var m=NativeDeployPresentation.MutatorOf(s.mutators[i]);float blink=reduced?1:(Mathf.Repeat(now*1.3f+i*.37f,1)<.7f?1:.35f);
                Disc(Around(new Vector2(x+7,r.center.y),6),U.Hex(m.Color,blink*a));U.Glow(Around(new Vector2(x+7,r.center.y),4),U.Hex(m.Color,.3f*blink*a),6);
                float tw=U.Measure(m.Name,10,true,true,.1f).x;U.Text(new Rect(x+18,r.y,tw+8,r.height),m.Name,10,C.Ink(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);x+=tw+40;
            }
        }
        // The squad's lamps: a lamp, the name and its state (READY green, LOADING n% amber with its bar, LOST red), me first.
        private static void Lamps(Rect r,NativeCradleCard card,float a,float now,bool reduced)
        {
            int n=Mathf.Max(1,card.Count);float cw=Mathf.Min(300,(r.width-(n-1)*14)/n),total=n*cw+(n-1)*14,x=r.x+(r.width-total)*.5f;
            for(int i=0;i<card.Count;i++,x+=cw+14)
            {
                var box=new Rect(x,r.y,cw,r.height);var lamp=card.Lamps[i];int hex=lamp==NativeCradleCard.Lamp.Ready?Green:lamp==NativeCradleCard.Lamp.Lost?Red:lamp==NativeCradleCard.Lamp.Away?0x7a808c:Amber;
                float pulse=lamp==NativeCradleCard.Lamp.Loading&&!reduced?.55f+.45f*Mathf.Abs(Mathf.Sin(now*3.4f+i)):1;
                U.Fill(box,X(0x08090d,.85f*a));H.Outline(box,X(hex,(card.You[i]?.9f:.45f)*a),0,H.Corners.None);
                var lc=new Vector2(box.x+20,box.center.y);U.Glow(Around(lc,7),X(hex,.35f*pulse*a),9);Disc(Around(lc,8),X(hex,pulse*a));Disc(Around(lc,3.5f),new Color(1,1,1,.55f*pulse*a));
                U.Text(new Rect(box.x+38,box.y+6,cw-44,20),card.Names[i]??"",14,C.Ink(a),TextAnchor.MiddleLeft,bold:card.You[i]);
                U.Text(new Rect(box.x+38,box.y+27,cw-44,16),NativeCradleCard.Label(lamp,card.Progress[i]),10,X(hex,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
                float p=card.Progress[i];if(lamp==NativeCradleCard.Lamp.Loading&&p>=0){var b=new Rect(box.x+38,box.yMax-6,cw-50,2);U.Fill(b,C.Navy(.9f*a));U.Fill(new Rect(b.x,b.y,b.width*Mathf.Clamp01(p),b.height),X(Amber,a));}
            }
        }
    }
}
