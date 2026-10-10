using System;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The sky death broadcast (client/death-announcements.ts at 05950ea) as pure rules in source (three.js) axes: a small
    /// rocket (SphereGeometry(.27, 8, 5), 0xf0fbff) with a line trail climbs for DEATH_ANNOUNCEMENT.launch, bursts into 72
    /// point-sprite stars (0xa6f4ff, size .18 x layout scale, size-attenuated, fading over 2.2 s), and the pilot's cyan
    /// hologram (0x62dcff, .55 opacity) unfolds with its part-box edges, 31 scanlines, the corner/tick frame and the nameplate.
    /// Everything is in the stage root's local units (skyBroadcastFrame: identity rotation, scale 5.5); the hologram's
    /// children (rig, scanlines, frame, nameplate) are additionally scaled (1, reveal, 1). Presentation only.
    /// </summary>
    public static class NativeBroadcastFx
    {
        public const float Launch=1.05f,Duration=5.6f,Spacing=1.5f,Scale=5.5f,Fade=.8f,TextHold=.45f;
        public const int Visible=3;
        // skyBroadcastFrame: [0,-43,43][slot%3], 72, -115 (source axes).
        public static readonly float[] SlotX={0,-43,43};public const float FrameY=72,FrameZ=-115;
        public const float RocketRadius=.27f;public const int RocketWidth=8,RocketHeight=5;
        public const int Stars=72;public const float StarSize=.18f,StarLife=2.2f,StarDrop=.4f;
        public const int HoloColor=0x62dcff,LineColor=0x8eedff,RocketColor=0xf0fbff,StarColor=0xa6f4ff,NameColor=0xb8f4ff;
        public const float HoloOpacity=.55f,EdgeOpacity=.36f,ScanOpacity=.12f,FrameOpacity=.7f,TrailOpacity=.6f;
        public const float ScanHalfWidth=2.8f,ScanZ=1.4f,ScanBob=.09f,ScanBobRate=1.7f;
        // The nameplate: PlaneGeometry(6.2, 1.356) at (0, -4.26, 1.5) carrying a 1024 x 224 canvas (system-ui, #b8f4ff, centred,
        // textBaseline middle, maxWidth 940): the victim upper-cased (500 52px at y 48), the verb (400 36px at y 118) and the
        // killer (500 44px at y 178).
        public const float NameY=-4.26f,NameZ=1.5f,NameWidth=6.2f,NameHeight=1.356f,CanvasWidth=1024,CanvasHeight=224,MaxTextWidth=940;
        public static readonly float[] NameRows={48,118,178},NameSizes={52,36,44};public static readonly int[] NameWeights={500,400,500};
        public static string Verb(bool killer)=>killer?"was killed by":"made an oopsie.";
        public static float RevealEase(float x){x=Math.Min(1,Math.Max(0,x));return x*x*(3-2*x);}
        public static float RevealBack(float x){x=Math.Min(1,Math.Max(0,x))-1;return 1+2.3f*x*x*x+1.3f*x*x;}
        public struct State
        {
            public bool Rocket,Stars,Holo,Text;public float Burst,RocketX,RocketY,TrailX,TrailY,TrailEndY,StarOpacity,Show,FadeOut,Flicker,ScaleY,HeadX,HeadY,ScanOffset;
            public float HologramAlpha,EdgeAlpha,ScanAlpha,FrameAlpha,NameAlpha;
        }
        /// <summary>DeathAnnouncements.update for one stage at <paramref name="age"/> = time - announcement.at (age &gt;= 0).</summary>
        public static State Sample(float age)
        {
            var s=new State();float u=Math.Min(1,age/Launch),burst=age-Launch,fade=Math.Min(1,Math.Max(0,(Duration-age)/Fade));s.Burst=burst;s.FadeOut=fade;
            s.Rocket=burst<0;float y=-25*(1-u)*(1-u);s.RocketX=(float)Math.Sin(u*8)*.12f;s.RocketY=y;s.TrailX=s.RocketX;s.TrailY=y;s.TrailEndY=y-6*(1-u*.5f);
            s.Stars=burst>=0&&burst<StarLife;s.StarOpacity=Math.Max(0,1-burst/StarLife);
            s.Holo=burst>=0;s.Show=RevealEase(burst/.26f);s.Flicker=.96f+.04f*(float)Math.Sin(age*29);s.ScaleY=Math.Max(.01f,RevealBack(burst/.32f));
            s.HeadY=(float)Math.Sin(age*.6f)*.045f;s.HeadX=(float)Math.Sin(age*.85f)*.018f;s.ScanOffset=(float)Math.Sin(age*ScanBobRate)*ScanBob;
            s.HologramAlpha=HoloOpacity*fade*s.Show*s.Flicker;s.EdgeAlpha=EdgeOpacity*fade*s.Show;s.ScanAlpha=ScanOpacity*fade*s.Show;s.FrameAlpha=FrameOpacity*fade*s.Show;s.NameAlpha=fade*s.Show;
            s.Text=s.Holo&&age<Duration-TextHold;
            if(!s.Holo)s.HologramAlpha=s.EdgeAlpha=s.ScanAlpha=s.FrameAlpha=s.NameAlpha=0;
            return s;
        }
        /// <summary>The 72 star directions (Fibonacci sphere, lengths 2 + (i % 7) * .22).</summary>
        public static readonly float[] StarDirections=MakeStars();
        private static float[] MakeStars()
        {
            var d=new float[Stars*3];
            for(int i=0;i<Stars;i++){double y=1-2*(i+.5)/Stars,phi=i*2.399963229728653,r=Math.Sqrt(1-y*y),k=2+(i%7)*.22;d[i*3]=(float)(Math.Cos(phi)*r*k);d[i*3+1]=(float)(y*k);d[i*3+2]=(float)(Math.Sin(phi)*r*k);}
            return d;
        }
        public static void Star(int i,float burst,out float x,out float y,out float z){x=StarDirections[i*3]*burst;y=StarDirections[i*3+1]*burst-burst*burst*StarDrop;z=StarDirections[i*3+2]*burst;}
        /// <summary>The scanline heights: for(let y=-2.85; y&lt;=3.85; y+=.22) in doubles (31 lines).</summary>
        public static readonly float[] ScanLines=MakeScan();
        private static float[] MakeScan(){var list=new System.Collections.Generic.List<float>();for(double y=-2.85;y<=3.85;y+=.22)list.Add((float)y);return list.ToArray();}
        /// <summary>The nameplate's canvas pixel (x, y) in hologram units: PlaneGeometry(6.2, 1.356) at (0, -4.26, 1.5), the canvas
        /// mapped over it with its top row at the top (CanvasTexture flipY).</summary>
        public static void NamePixel(float px,float py,out float x,out float y){x=-NameWidth*.5f+px/CanvasWidth*NameWidth;y=NameY+NameHeight*.5f-py/CanvasHeight*NameHeight;}
        /// <summary>The portrait frame as segments (x0,y0,z0,x1,y1,z1): four corner brackets, nine ticks a side, the base line.</summary>
        public static readonly float[] FrameSegments=MakeFrame();
        private static float[] MakeFrame()
        {
            var s=new System.Collections.Generic.List<float>();void Seg(float ax,float ay,float az,float bx,float by,float bz){s.Add(ax);s.Add(ay);s.Add(az);s.Add(bx);s.Add(by);s.Add(bz);}
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1}){float cx=x*3,cy=y*3.65f+.25f,cz=1.45f;Seg(cx-x*.65f,cy,cz,cx,cy,cz);Seg(cx,cy,cz,cx,cy-y*.9f,cz);}
            foreach(int x in new[]{-1,1})for(int i=0;i<9;i++)Seg(x*3.12f,-2.8f+i*.75f,1.45f,x*3.3f,-2.8f+i*.75f,1.45f);
            Seg(-2.5f,-3.55f,1.45f,2.5f,-3.55f,1.45f);return s.ToArray();
        }
        /// <summary>The stage root's position (source axes) for a slot.</summary>
        public static void Frame(int slot,out float x,out float y,out float z){x=SlotX[((slot%3)+3)%3];y=FrameY;z=FrameZ;}
        /// <summary>The portrait rig's fit (create(): crop to the visible head, hat and shoulders, scale min(5.1 / width,
        /// 6.4 / height), centred, raised .48) for a hat (NativeBroadcastEdges), in hologram units.</summary>
        public static void RigFit(string hat,out float fit,out float ox,out float oy,out float oz)
        {int h=NativeBroadcastEdges.HatIndex(hat);fit=NativeBroadcastEdges.Fits[h];ox=NativeBroadcastEdges.Offsets[h*3];oy=NativeBroadcastEdges.Offsets[h*3+1];oz=NativeBroadcastEdges.Offsets[h*3+2];}
        /// <summary>canvas fillText's maxWidth: a line wider than 940 px is squeezed horizontally to fit.</summary>
        public static float Squeeze(float width)=>width>MaxTextWidth&&width>0?MaxTextWidth/width:1;
        /// <summary>The bones the portrait shows (legs, arms, hip and abdomen parts hidden): chest, neck, head, clavicles.</summary>
        public static bool PortraitBone(int bone)=>bone==CapturedRig.Chest||bone==CapturedRig.Neck||bone==CapturedRig.Head||bone==CapturedRig.LClav||bone==CapturedRig.RClav;
    }
    public static partial class NativeBroadcastEdges
    {
        /// <summary>The hat's row (cosmetics(): an unknown or missing hat is 'none', row 0).</summary>
        public static int HatIndex(string hat){if(hat!=null)for(int i=0;i<Hats.Length;i++)if(Hats[i]==hat)return i;return 0;}
    }
}
