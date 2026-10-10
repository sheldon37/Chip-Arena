using System;
namespace ChipArena.UnityConversion
{
    public static class NativeIntroRules
    {
        public static string Style(NativeSeat seat)
        {
            var access=seat.access??NativeAccess.Guest;double now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();if(seat.spec=="siren"&&(seat.AuthorityBoss||access.Allows("weapon:siren",now)))return "dev-siren";
            if(seat.overrideIntro&&!string.IsNullOrEmpty(seat.intro)&&(!seat.intro.StartsWith("dev-")||access.Allows("intro:"+seat.intro,now)))return seat.intro;
            if(seat.spec=="emperor")return "dev-emperor";if(seat.spec=="ansatsuken")return "dev-ansatsuken";if(seat.spec=="leet")return "dev-matrix";return "weapon-"+seat.spec;
        }
        public static double Duration(string style)=>style=="dev-siren"?9.0678:style=="dev-emperor"?7.8:style=="dev-matrix"?8:style=="dev-ansatsuken"?7.2:4.8;
        // Presentation only (the HUD's name card): shared/intros.ts introNameAt, SIREN_INTRO.nameAt 6.4261 for the Siren.
        public static double NameAt(string style)=>style=="dev-siren"?6.4261:style=="dev-emperor"?5.95:style=="dev-matrix"?6.25:style=="dev-ansatsuken"?5.45:3.2;
        // The overlay's own clock length (shared/intros.ts introDuration). The Siren's source intro is 7.2678 s; Duration keeps the
        // native phase length unchanged (9.0678 s for a PvP Siren intro), which is a gameplay-timing difference left for review.
        public static double OverlayDuration(string style)=>style=="dev-siren"?7.2678:Duration(style);
        // Presentation only below: what the client stages for the replicated style; match timing keeps that style.
        // The entrances the client can stage (player-intros.ts; every one has a baked stage and intro clip).
        public static readonly string[] Entrances={"neon-drop","vector-slide","signal-spin","arc-salute","pulse-wave","weapon-sword","weapon-daggers","weapon-shield","weapon-greatsword","weapon-pistol","weapon-rifle","weapon-machinegun","weapon-fire","weapon-water","weapon-lightning","weapon-darkness","dev-emperor","dev-ansatsuken","dev-matrix","dev-siren"};
        // A style with no entrance (a Siren seat without its DEV grant gets "weapon-siren") is staged as the source's own
        // fallback, 'neon-drop' (shared/intros.ts effectiveIntro), instead of failing the intro frame.
        public static string Entrance(string style)=>style!=null&&Array.IndexOf(Entrances,style)>=0?style:"neon-drop";
        // The weapon model the intro rig holds, or null. player-intros.ts builds a weapon into the bare intro rig for a weapon
        // intro (WEAPON_INTROS[style].spec: the model "weapon-"+spec is the style itself; daggers fill both hands) and for the
        // Emperor's Oath (buildWeapon(weapon/offWeapon,'emperor'), shown by poseEmperor; weapon-emperor holds both gauntlets).
        // Every other entrance shows empty hands: the poses, the Rain Warrior and the L33T Throne hide r.weapon and the Siren
        // hides it. The L33T Throne's staff is the stage's own prop, grabbed at 4 s.
        public static string IntroWeapon(string style)=>style=="dev-emperor"?"weapon-emperor":style!=null&&style.StartsWith("weapon-",StringComparison.Ordinal)&&Array.IndexOf(Entrances,style)>=0?style:null;
        // character-rig.ts: the hand socket on the wrist joint, where an ungripped weapon sits (source frame, +x the rig's left).
        public static readonly System.Numerics.Vector3 HandSocket=new System.Numerics.Vector3(0,-.065f,.055f);
        /// <summary>
        /// Where IntroWeapon's model sits on its wrist joint (the bank exports weapons with the hand sockets zeroed, so the
        /// model origin is the wrist). Every weapon intro grips its weapon (rig-ik.ts gripWeapon: the weapon origin on the wrist,
        /// no extra rotation, also for the staffs on the left hand); poseEmperor never grips, so the gauntlets stay on the hand
        /// socket. tools/export_motion.mjs fails the intro bake if any frame holds a weapon anywhere else.
        /// </summary>
        public static System.Numerics.Vector3 IntroGrip(string style)=>style=="dev-emperor"?HandSocket:System.Numerics.Vector3.Zero;
        /// <summary>
        /// Belt Rattle's muzzle flash (weapon-intro-motion.ts: buildWeapon's flash at rest, shown on alternate 1/24 s frames of
        /// the 1.93-2.33 s burst), on the right wrist like the gun; false for every other style and moment.
        /// </summary>
        public static bool IntroMuzzleFlash(string style,double age,out WeaponPiece flash)
        {
            flash=default;if(style!="weapon-machinegun"||!(age>1.93&&age<2.33)||(int)Math.Floor((age-1.93)*24)%2!=0)return false;
            flash=new WeaponPiece{Shape=WeaponShape.Cone,Glow=true,Color=0xffdb90,Position=new System.Numerics.Vector3(0,0,NativeWeaponDressing.MuzzleZ("machinegun")+.14f),Scale=new System.Numerics.Vector3(.11f,.30f,.11f),Rotation=CapturedRig.Euler((float)Math.PI/2),Orbit=-1};
            return true;
        }
        /// <summary>
        /// An entrance-stage track whose tint changes over the clip (SceneMotion values: 20 floats per track frame, tint at
        /// 17-19). On the intro stages these are exactly ChipBlasts' voxel pools; the transparent ones are its glow pools.
        /// </summary>
        public static bool TintVaries(float[] values,int frames,int track)
        {
            int start=track*frames*20;for(int f=1;f<frames;f++)for(int k=17;k<20;k++)if(values[start+f*20+k]!=values[start+k])return true;return false;
        }
        /// <summary>
        /// Where a baked scene track is sampled at `age` (NativeSceneMotion.Matrix): rows `a` and `b` (value offsets) and the
        /// blend between them. The blend is held at 0 (row a as baked) when the next row is hidden: tools/export_cinematics.mjs
        /// fills the rows outside an object's lifetime with an identity matrix, so lerping toward it flew every dying voxel to
        /// the stage origin as a full-size 1 m box for one frame (the boxes flashing at the pilot's feet). It is also held for a
        /// voxel-pool slot (`voxel`: its tint varies) that holds another particle in the next row: ChipBlasts swap-removes spent
        /// voxels, so a slot jumps to a different voxel, and lerping slid a cube across the scene between the two (the trails).
        /// </summary>
        public static float StageBlend(float[] values,int frames,int track,float duration,float age,bool voxel,out int a,out int b)
        {
            float u=age/duration;if(!(u>0))u=0;else if(u>1)u=1;float frame=u*(frames-1);int f=Math.Min(frames-1,(int)frame);
            a=(track*frames+f)*20;b=(track*frames+Math.Min(frames-1,f+1))*20;float blend=frame-f;
            return blend>0&&StageContinuous(values,a,b,voxel)?blend:0;
        }
        /// <summary>Whether a scene track moves smoothly from row offset `a` to `b` (both shown; a voxel slot keeps its particle).</summary>
        public static bool StageContinuous(float[] values,int a,int b,bool voxel)
        {
            if(!(values[b+16]>.001f))return false;if(!voxel)return true;
            float dx=values[b+12]-values[a+12],dy=values[b+13]-values[a+13],dz=values[b+14]-values[a+14];if(dx*dx+dy*dy+dz*dz>.35f*.35f)return false;
            float sa=values[a]*values[a]+values[a+1]*values[a+1]+values[a+2]*values[a+2],sb=values[b]*values[b]+values[b+1]*values[b+1]+values[b+2]*values[b+2];if(sa>sb*2.04f||sb>sa*2.04f)return false;
            for(int k=17;k<20;k++)if(Math.Abs(values[b+k]-values[a+k])>.2f)return false;return true;
        }
        /// <summary>
        /// The alpha of an opaque voxel on a weapon-intro stage. The barrel smoke and floor dust (ChipBlasts' solid pool: grey
        /// voxels that rise, grow and darken from SMOKE to ASH, then shrink away, weapon-intro-effects.ts) are MeshBasicMaterial
        /// in the source, so they read as flat puffs; lit and opaque here they read as hard, shaded grey cubes. Unlit and
        /// alpha-blended (NativeSourceModels.Blended) they read as flat soft puffs, densest at the muzzle (linear tint `max`
        /// .38, alpha .7) and thinning as they darken (.2 spent),
        /// while the bake's own growth and shrink still ends each one. Saturated voxels (brass casings, embers) stay opaque (1).
        /// </summary>
        public static bool SoftSmoke(string style)=>style=="weapon-pistol"||style=="weapon-rifle"||style=="weapon-machinegun";
        public static float SoftVoxelAlpha(float r,float g,float b)
        {
            float max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b));if(!(max>0))return 0;if((max-min)/max>.35f)return 1;
            float t=(max-.02f)/.3f;t=t<0?0:t>1?1:t;return .2f+.5f*t*t*(3-2*t);
        }
        /// <summary>
        /// A glow voxel (ChipBlasts: MeshBasicMaterial, AdditiveBlending, fading by dimming its instance colour to black) drawn
        /// with the alpha-blended source material: the dimming moves into alpha and the colour keeps its hue, so colour x alpha is
        /// the source's additive term and a spent voxel fades out instead of turning into an opaque dark cube. Returns the alpha
        /// factor (0..1) and rewrites the tint; brighter-than-white tints keep alpha 1 and their overbright colour.
        /// </summary>
        public static float GlowAlpha(ref float r,ref float g,ref float b)
        {
            float k=Math.Max(r,Math.Max(g,b));if(!(k>0)){r=g=b=0;return 0;}float a=Math.Min(1,k);r/=a;g/=a;b/=a;return a;
        }
        /// <summary>
        /// intro-audio.ts: the entrance's own sting (Resources/Audio/audio/intros/&lt;style&gt;), or null for dev-siren, which has
        /// none: the boss song plays from the Siren's intro start instead (IntroSong; simulation.ts updateBossSong, audio.ts
        /// previewIntro), and the cell's bulb stutters on that song's beats (NativeSirenIntroLight).
        /// </summary>
        public static string IntroSting(string style)=>style==null||style=="dev-siren"||Array.IndexOf(Entrances,style)<0?null:"audio/intros/"+style;
        /// <summary>The music an entrance plays from its first frame (song time = intro age), or null: black-siren-protocol for dev-siren.</summary>
        public static string IntroSong(string style)=>style=="dev-siren"?"music/black-siren-protocol":null;
        // shared/intros.ts INTRO_STYLES display names.
        public static string StyleName(string style)
        {
            switch(style)
            {
                case "dev-siren":return "DEV · Black Siren Protocol";case "dev-emperor":return "DEV · Emperor’s Oath";case "dev-ansatsuken":return "DEV · Rain Warrior";case "dev-matrix":return "DEV · L33T Throne";
                case "weapon-sword":return "Flash Draw";case "weapon-daggers":return "Twin Phantom";case "weapon-greatsword":return "Colossus Oath";case "weapon-shield":return "Bastion Drop";
                case "weapon-pistol":return "Deadeye";case "weapon-rifle":return "Longwatch";case "weapon-machinegun":return "Belt Rattle";case "weapon-fire":return "Cinder Crown";
                case "weapon-water":return "Tidal Serpent";case "weapon-lightning":return "Thunderstep";case "weapon-darkness":return "Eventide Gate";
                case "vector-slide":return "Vector Slide";case "signal-spin":return "Signal Spin";case "arc-salute":return "Arc Salute";case "pulse-wave":return "Pulse Wave";
                default:return "Weapon Signature";
            }
        }
    }
    /// <summary>
    /// Presentation only: the clock a live entrance is drawn at. The replicated intro age only moves when a host frame lands
    /// (20 Hz on a guest, the host's 60 Hz fixed step), and the dev-siren cell's stutters are 37-83 ms dark windows on the boss
    /// song's beats (NativeSirenIntroLight), so drawing at that age skips or doubles them and lets them drift from the song this
    /// machine plays. This clock runs on the frame time and follows Target: the boss song's own playback time while it plays
    /// near the intro age (the beats as heard; the source's interpolated snapshot time drives its song and its cell alike),
    /// else the replicated age. Small errors (the age's steps, the audio position's mix-buffer steps) are pulled in without
    /// running backwards; a jump past Snap (a new entrance, a song resync, a hitch) is taken at once. Match timing never reads it.
    /// </summary>
    public sealed class NativeIntroClock
    {
        /// <summary>Errors past this (s) are jumped, not pulled; under 1/Pull, so a pull never turns the clock back.</summary>
        public const double Snap=.1;
        /// <summary>How fast (1/s) a small error is pulled in.</summary>
        public const double Pull=6;
        /// <summary>The song only leads the clock while it is this close (s) to the intro age (a second dev-siren entrance in one match meets the song mid-track).</summary>
        public const double SongWindow=.5;
        private double value=double.NaN;
        public void Reset(){value=double.NaN;}
        /// <summary>The time to follow: `song` (playback seconds, negative when it is not playing) near `age`, else `age`.</summary>
        public static double Target(double age,double song)=>song>=0&&Math.Abs(song-age)<SongWindow?song:age;
        /// <summary>Advances by the frame time `dt` (at most .1 s) toward `target` and returns the age to draw (never negative).</summary>
        public double Sample(double target,double dt)
        {
            if(double.IsNaN(target)||double.IsInfinity(target))return double.IsNaN(value)?0:value;
            dt=Math.Max(0,Math.Min(.1,dt));
            if(double.IsNaN(value)||Math.Abs(target-(value+dt))>Snap)value=target;
            else{value+=dt;value+=(target-value)*Math.Min(1,dt*Pull);}
            return Math.Max(0,value);
        }
    }
    /// <summary>
    /// Presentation only: an entrance-stage light from the baked scene bank as a Unity built-in light. The bank holds three's
    /// physically based lights (linear colour, intensity; Lambert diffuse is albedo/PI in three r180, albedo alone in Unity's
    /// built-in Lambert), so the light Unity applies must be the linear colour x intensity / PI. The source's exposure .92 and
    /// ACES toe roughly cancel at the stage's levels and are not applied. Unity applies a light as colour x intensity in Gamma
    /// space, linear(colour) x intensity with linear intensity, and linear(colour x intensity) with the built-in default in
    /// Linear space: the raw values made the Siren cell's bulb (24) about 700 once Linear was pinned, a flat white room.
    /// </summary>
    public static class NativeEntranceLight
    {
        public const double Diffuse=1/Math.PI;
        // Unity's sRGB transfer (Mathf.GammaToLinearSpace), with its power-2.2 branch above 1 for light colour x intensity.
        public static double ToLinear(double c)=>c<=.04045?c/12.92:c<1?Math.Pow((c+.055)/1.055,2.4):Math.Pow(c,2.2);
        public static double ToGamma(double c)=>c<=.0031308?c*12.92:c<1?1.055*Math.Pow(c,1/2.4)-.055:Math.Pow(c,1/2.2);
        /// <summary>The linear light Unity applies for a Light colour channel c and intensity.</summary>
        public static double Applied(double c,double intensity,bool linearSpace,bool linearIntensity)=>!linearSpace?c*intensity:linearIntensity?ToLinear(c)*intensity:ToLinear(c*intensity);
        /// <summary>Light.color (channels 0..1) and Light.intensity for a bank light (linear rgb, intensity).</summary>
        public static void Light(double r,double g,double b,double intensity,bool linearSpace,bool linearIntensity,out double cr,out double cg,out double cb,out double unity)
        {
            double k=Math.Max(0,intensity)*Diffuse,tr=Math.Max(0,r)*k,tg=Math.Max(0,g)*k,tb=Math.Max(0,b)*k;
            if(linearSpace&&!linearIntensity){tr=ToGamma(tr);tg=ToGamma(tg);tb=ToGamma(tb);}
            unity=Math.Max(tr,Math.Max(tg,tb));if(!(unity>0)){cr=cg=cb=unity=0;return;}
            cr=tr/unity;cg=tg/unity;cb=tb/unity;
            if(linearSpace&&linearIntensity){cr=ToGamma(cr);cg=ToGamma(cg);cb=ToGamma(cb);}
        }
        /// <summary>A RenderSettings ambient channel (authored in sRGB, linearised by Unity in Linear space) for a bank ambient sum.</summary>
        public static double Ambient(double linear,bool linearSpace){double t=Math.Max(0,linear)*Diffuse;return linearSpace?ToGamma(t):t;}
    }
}
