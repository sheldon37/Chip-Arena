using System;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Presentation only: one entrance-stage point light in the source frame (three's axes, +x the rig's left), as three
    /// lights a Lambert surface: R, G, B are the linear colour times intensity, Distance the cutoff (0 none), Decay the
    /// exponent. The stage shader block (NativeSourceLit / NativeSourceTransparent, `_StageLight*`) evaluates these itself.
    /// </summary>
    public struct NativeStageLight
    {
        public double X,Y,Z,R,G,B,Distance,Decay;
        public NativeStageLight(double x,double y,double z,double r,double g,double b,double distance,double decay){X=x;Y=y;Z=z;R=r;G=g;B=b;Distance=distance;Decay=decay;}
    }
    /// <summary>
    /// The entrance stages' point lights, lit in the source models' shader instead of by Unity lights. The stage is drawn with
    /// Graphics.DrawMeshInstanced, and in the owner's builds Unity's point lights did not light it: the Siren cell, lit only by
    /// a bulb and a fill, stayed black around the emissive bulb and eyes (an offline built-in forward emulation of the port's
    /// inputs shows the source's lit cell when both point lights reach the instanced draw, and exactly that black frame when
    /// they do not). The shader block applies three's own model, so pixel-light limits, light culling, render modes, Unity's
    /// attenuation curve and the light intensity mode no longer decide whether or how the stage is lit. Directional lights stay
    /// Unity lights.
    /// </summary>
    public static class NativeStageLighting
    {
        /// <summary>Shader array size (`_StageLightPos[8]` / `_StageLightColor[8]`).</summary>
        public const int Max=8;
        /// <summary>three r180 getDistanceAttenuation: 1/max(d^decay,.01), times (1-(d/distance)^4)^2 clamped when a distance is set.</summary>
        public static double Falloff(double d,double distance,double decay)
        {
            double f=1/Math.Max(Math.Pow(Math.Max(0,d),decay),.01);
            if(distance>0){double q=d/distance;q*=q;q*=q;double c=Math.Max(0,Math.Min(1,1-q));f*=c*c;}
            return f;
        }
        /// <summary>
        /// The light the shader block adds at a surface point (source frame; unit normal), per channel, before the albedo: the
        /// sum of each light's colour x max(0, N.L) x Falloff, over PI. `StageLight` in the shaders multiplies it by the albedo.
        /// </summary>
        public static void Diffuse(NativeStageLight[] lights,int count,double px,double py,double pz,double nx,double ny,double nz,out double r,out double g,out double b)
        {
            r=g=b=0;
            for(int i=0;i<count&&i<lights.Length;i++)
            {
                var l=lights[i];double dx=l.X-px,dy=l.Y-py,dz=l.Z-pz,d=Math.Sqrt(dx*dx+dy*dy+dz*dz),len=Math.Max(d,1e-4);
                double k=Math.Max(0,(nx*dx+ny*dy+nz*dz)/len)*Falloff(d,l.Distance,l.Decay);r+=l.R*k;g+=l.G*k;b+=l.B*k;
            }
            r/=Math.PI;g/=Math.PI;b/=Math.PI;
        }
        /// <summary>The source renderer's exposure (renderer.ts toneMappingExposure) for StageTone.</summary>
        public const double Exposure=.92;
        /// <summary>
        /// three r180 ACESFilmicToneMapping at Exposure for one grey level (the shaders' StageTone runs it per channel with the
        /// ACES input and output matrices, which keep greys grey). The source tone-maps every lit material; the stage's lit
        /// parts get the same curve so a dim stutter falls into ACES's toe and reads black, as it does in the source.
        /// </summary>
        public static double Aces(double x)
        {
            double v=Math.Max(0,x)*Exposure/.6,a=v*(v+.0245786)-.000090537,b=v*(.983729*v+.4329510)+.238081;
            return Math.Max(0,Math.Min(1,a/b));
        }
        /// <summary>How strongly a light reaches the stage's centre (1 m up at the origin): the order in which Add keeps lights.</summary>
        public static double Reach(in NativeStageLight l){double dx=l.X,dy=l.Y-1,dz=l.Z;return (l.R+l.G+l.B)*Falloff(Math.Sqrt(dx*dx+dy*dy+dz*dz),l.Distance,l.Decay);}
        /// <summary>Adds a light to `list` (holding `count`), keeping the Max brightest by Reach; returns the new count.</summary>
        public static int Add(NativeStageLight[] list,int count,in NativeStageLight light)
        {
            if(!(light.R+light.G+light.B>0))return count;double reach=Reach(light);int at=count;
            while(at>0&&Reach(list[at-1])<reach)at--;
            if(at>=Max||at>=list.Length)return count;
            for(int i=Math.Min(count,Math.Min(Max,list.Length)-1);i>at;i--)list[i]=list[i-1];
            list[at]=light;return Math.Min(count+1,Math.Min(Max,list.Length));
        }
    }
    /// <summary>
    /// Presentation only: the dev-siren entrance's light (client/siren/intro.ts SirenIntroScene.update, at 05950ea). An empty
    /// cell; a bulb buzzes on at the track's beat 1, stutters dark around each off-beat (and a second blink on odd beats) from
    /// beat 3, flashes the Siren into view at beat 6, steadies on it from beat 9 and dies at beat 13. Every value is a pure
    /// function of the intro age, which is the song time of black-siren-protocol (the boss song starts with the Siren's intro),
    /// and the stutter reads the same beat table the music is cued from (PveContent bossMusic.beats, boss-music.ts), so the
    /// flashes land on the music for every viewer. Replaces the scene bank's 30 Hz light samples for this stage.
    /// </summary>
    public static class NativeSirenIntroLight
    {
        /// <summary>SIREN_INTRO cue beats (shared/siren-contract.ts): bulb, flicker, glimpse, reveal, blackout, smile.</summary>
        public const int BulbBeat=1,FlickerBeat=3,GlimpseBeat=6,RevealBeat=9,BlackoutBeat=13,SmileBeat=14;
        /// <summary>intro.ts BEATS: the track's first 18 beats (to SIREN_INTRO.duration, the downbeat of bar 5).</summary>
        public const int BeatCount=18;
        // intro.ts: PointLight(0xe9f0d8,0,6.5,1.6) at the bulb, PointLight(0x6a5f8e,0,5,2) at (-.9,1.2,1.4), AmbientLight(0x2a2638),
        // the bulb glow 0xfff6d8 at (.62,2.02,.32) and its halo sprite 0xfff1cf (scale .9, additive).
        public const int BulbHex=0xe9f0d8,FillHex=0x6a5f8e,AmbientHex=0x2a2638,GlowHex=0xfff6d8,HaloHex=0xfff1cf;
        public const double BulbIntensity=38,FillIntensity=4,BulbDistance=6.5,BulbDecay=1.6,FillDistance=5,FillDecay=2,BulbY=2.02,BulbZ=.32,FillX=-.9,FillY=1.2,FillZ=1.4,HaloScale=.9;
        /// <summary>three's Color.setHex: the sRGB channel `shift` of `hex` as a linear value.</summary>
        public static double Linear(int hex,int shift){double c=((hex>>shift)&255)/255.0;return c<.04045?c*.0773993808:Math.Pow(c*.9478672986+.0521327014,2.4);}
        /// <summary>Cue time: the track's beat `index` (SIREN_INTRO cues are those beats' times).</summary>
        public static double Cue(double[] beats,int index)=>beats!=null&&index>=0&&index<beats.Length?beats[index]:double.PositiveInfinity;
        /// <summary>The bulb's level at `age` (intro.ts `light`): 0 dark, ~.55-.72 lit, .04 in a stutter, 1.15 in the glimpse flash.</summary>
        public static double Level(double age,double[] beats)
        {
            if(beats==null||beats.Length<BeatCount)return 0;
            double bulb=beats[BulbBeat],flicker=beats[FlickerBeat],glimpse=beats[GlimpseBeat],reveal=beats[RevealBeat],blackout=beats[BlackoutBeat];
            if(age>=bulb&&age<flicker)return .55+.08*Math.Sin(age*90)+(age-bulb<.08?-.4:0);
            if(age>=flicker&&age<reveal)
            {
                int b=-1;for(int i=0;i<BeatCount;i++)if(beats[i]<=age)b=i;
                double phase=b>=0&&b<BeatCount-1?(age-beats[b])/(beats[b+1]-beats[b]):0;
                // Off-beat stutters (dark around each half beat), plus the hard flash that shows the Siren.
                bool off=phase>.42&&phase<.62||(b%2==1&&phase>.15&&phase<.24);double light=off?.04:.62+.1*Math.Sin(age*130);
                if(age>=glimpse&&age<glimpse+.11)light=1.15;else if(age>=glimpse+.11&&age<glimpse+.32)light=0;
                return light;
            }
            if(age>=reveal&&age<blackout)return .78+.05*Math.Sin(age*70)-(age-reveal<.05?.5:0);
            return 0;
        }
        /// <summary>
        /// True for a scene-bank track (SceneMotion values: 20 floats per frame, translation at 12-14) whose first frame stands
        /// at the bulb's rest position: in stage-dev-siren the bulb glow mesh, and the halo sprite's empty track.
        /// </summary>
        public static bool AtBulb(float[] values,int frames,int track)
        {
            int o=track*frames*20;if(values==null||track<0||frames<1||o+14>=values.Length)return false;
            return Math.Abs(values[o+12]-BulbX(0))<.002&&Math.Abs(values[o+13]-BulbY)<.002&&Math.Abs(values[o+14]-BulbZ)<.002;
        }
        /// <summary>The bulb (and its glow and halo) swaying on the cord: x = .62 + .025 sin(1.3 age).</summary>
        public static double BulbX(double age)=>.62+.025*Math.Sin(age*1.3);
        /// <summary>The bulb mesh's glow multiplier (glowMaterial uIntensity .15 + 1.1 x level; toneMapped false).</summary>
        public static double Glow(double level)=>.15+level*1.1;
        /// <summary>The bulb halo sprite's opacity (.55 x level).</summary>
        public static double Halo(double level)=>.55*level;
        /// <summary>
        /// The halo's opacity for Unity's linear additive blend. three adds the sprite to the sRGB-encoded canvas (display
        /// values), a Linear project adds it in linear light, where a stutter's .02 opacity would glow visibly round the bulb.
        /// Against the backdrop b behind the bulb (linear) the opacity that adds what the source adds is dec(enc(b) + a) - b;
        /// b is the cell's back wall where the intro camera sees it behind the bulb (about (1.51, 2.88, -2.1), facing +z,
        /// 4.7 m away, so fog keeps .56 of it), lit and tone-mapped as the stage block and StageTone light it.
        /// </summary>
        public static double HaloLinear(double age,double[] beats)
        {
            var lights=haloScratch;int n=Lights(age,beats,lights,out double level);double a=Halo(level);if(!(a>0))return 0;
            NativeStageLighting.Diffuse(lights,n,1.51,2.88,-2.1,0,0,1,out double r,out _,out _);
            double b=.56*NativeStageLighting.Aces(r*Linear(WallHex,16));
            return Math.Max(0,NativeEntranceLight.ToLinear(Math.Min(1,NativeEntranceLight.ToGamma(b)+a))-b);
        }
        // Main-thread scratch for HaloLinear (no per-frame allocation).
        private static readonly NativeStageLight[] haloScratch=new NativeStageLight[2];
        /// <summary>siren/intro.ts: the concrete walls (MeshLambertMaterial 0x4a4a50).</summary>
        public const int WallHex=0x4a4a50;
        /// <summary>The ambient light's intensity (.12 + .35 x level while lit, else 0).</summary>
        public static double Ambient(double level)=>level>0?.12+.35*level:0;
        /// <summary>
        /// Writes the cell's point lights at `age` (the bulb, then the fill; none while dark) into `into` and returns how many;
        /// `level` is Level(age, beats).
        /// </summary>
        public static int Lights(double age,double[] beats,NativeStageLight[] into,out double level)
        {
            level=Level(age,beats);if(!(level>0)||into==null||into.Length<2)return 0;
            double bulb=level*BulbIntensity,fill=level*FillIntensity;
            into[0]=new NativeStageLight(BulbX(age),BulbY,BulbZ,Linear(BulbHex,16)*bulb,Linear(BulbHex,8)*bulb,Linear(BulbHex,0)*bulb,BulbDistance,BulbDecay);
            into[1]=new NativeStageLight(FillX,FillY,FillZ,Linear(FillHex,16)*fill,Linear(FillHex,8)*fill,Linear(FillHex,0)*fill,FillDistance,FillDecay);
            return 2;
        }
    }
}
