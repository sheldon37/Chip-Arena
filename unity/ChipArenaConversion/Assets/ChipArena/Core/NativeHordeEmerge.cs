using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The zombies' emergence (owner 2026-11-21: "rework zombie spawn animation as well. Currently theyre using old drop in effect
    /// or just spawning in. Can we animate them digging out of the ground? Or have them laying there, and just rising? The hit
    /// indicator from the old drop in effect is massive and inaccurate, so I want to remove that drop in animation entirely.").
    /// The shared rules and presentation contract, engine free (docs/ZOMBIE-EMERGE.md):
    /// <list type="bullet">
    /// <item>Which entries emerge (<see cref="Emerges"/>): a map entry of kind "drop" (any kind), a ceiling vent, and a door or
    /// cell with no opening to walk through (its from is its exit: the city's street spawns, the DEV monsters). Doors and cells
    /// with an opening still walk in, floor and wall vents climb out, breaches break through; the boss summons, the Rigger's hang
    /// and the Backrooms entities keep their own entrances.</item>
    /// <item>The clock is the entry's own, replicated as before (SpawnAt and ActiveAt; ExposedAt derived from them on every
    /// peer): a drop entry's old warning-to-touchdown (<see cref="NativeHordeDrop.Duration(float,in NativeHordeDrop.DropProfile)"/>) is
    /// the emergence and its old touchdown (<see cref="NativeHordeDrop.LandAt(float,in NativeHordeDrop.DropProfile)"/>) the moment
    /// its torso is out; a pop-in door's is <see cref="PopSpan"/>, its torso out at <see cref="PopTorso"/>. No wire change.</item>
    /// <item>The phases (<see cref="Marks"/>): the tell (the ground cracks and bulges, or a dark stain seeps out), the break (a
    /// hand claws through, or the body lying there stirs), the torso out at ExposedAt, risen at ActiveAt.</item>
    /// <item>Honesty: nothing hurts anyone while it emerges (no slam: the drop's landing damage and knockback went with it);
    /// before ExposedAt it has no collider and takes no damage; from ExposedAt its capsule rises with the drawn body
    /// (<see cref="Sink"/>: its top over the drawn head) and it can be shot, still unable to move or attack until ActiveAt.</item>
    /// <item>The style (<see cref="Choose"/>): open ground digs out; interiors, the bleed and decor favour lying and rising; the
    /// heavy kinds (brute, Hulk) always dig out, bigger, and leave a scar that stays a while (<see cref="LookOf"/>).</item>
    /// </list>
    /// Pure functions of the replicated clocks and the add's id: host, guests and the killcam draw the same. No allocation.
    /// </summary>
    public static class NativeHordeEmerge
    {
        public enum Style{Dig=0,Rise=1}

        // ================================================================================================================
        // Rules: which entries emerge, the pop-in timeline, the capsule's rise
        // ================================================================================================================
        /// <summary>A door or cell with no opening: its from is its exit (the add used to appear standing there).</summary>
        public static bool PopIn(PveEntry e)=>e!=null&&(e.kind=="door"||e.kind=="cell")&&e.from!=null&&e.@exit!=null&&
            Math.Abs(e.from.x-e.@exit.x)<.05f&&Math.Abs(e.from.y-e.@exit.y)<.05f&&Math.Abs(e.from.z-e.@exit.z)<.05f;
        /// <summary>Kinds with an entrance of their own: the Backrooms entities (their dormancy and lore bodies) and the Rigger
        /// (it hangs from its chain, active at once).</summary>
        public static bool OwnEntrance(string kind)=>kind=="rigger"||NativeBackroomsKinds.Own(kind);
        /// <summary>True when an add of `kind` entering by `e` emerges from the floor (rules and presentation read the same test).</summary>
        public static bool Emerges(PveEntry e,string kind)=>e!=null&&!OwnEntrance(kind)&&(NativeWardEntries.Drop(e)||PopIn(e));
        /// <summary>A pop-in door's emergence: risen <see cref="PopSpan"/> s after SpawnAt (its first walk-in's length, kept for
        /// the pacing), its torso out (ExposedAt, the entry's WalkAt) at <see cref="PopTorso"/>.</summary>
        public const float PopTorso=1f,PopSpan=1.8f;
        /// <summary>How far under its standing height the capsule's centre is when the torso comes out (a share of its height):
        /// its top then sits over the drawn kneel's head.</summary>
        public const float TorsoDepth=.42f;
        // The capsule's rise from the torso out to risen (a share), keyed to the crawl-to-stand take's head over the same time
        // (u = 0, .1, ... 1): the drawn head and the capsule's top climb together (EmergeChecks).
        private static readonly float[] RiseKeys={0,.15f,.35f,.41f,.44f,.53f,.57f,.65f,.80f,.92f,1};
        /// <summary>The capsule's rise (0..1) a share `u` of the way from the torso out to risen.</summary>
        public static float Rise(float u)
        {
            if(!(u>0))return 0;if(u>=1)return 1;float k=u*10;int i=(int)k;float f=k-i;return RiseKeys[i]+(RiseKeys[i+1]-RiseKeys[i])*f;
        }
        /// <summary>The authority's capsule centre under its standing height (m, &lt;= 0) `age` s after SpawnAt, for an add whose
        /// torso is out `torso` s and risen `span` s after it, `height` its capsule's: a whole height under (no collider) until the
        /// torso is out, then rising with the drawn body to 0 at ActiveAt.</summary>
        public static float Sink(float age,float torso,float span,float height)
        {
            if(age>=span)return 0;if(age<torso)return -height;
            float u=(age-torso)/Math.Max(.05f,span-torso);return -TorsoDepth*height*(1-Rise(u));
        }

        // ================================================================================================================
        // The phases
        // ================================================================================================================
        /// <summary>An emergence's marks (s after SpawnAt): the tell's end (<see cref="Break"/>: the first hand through, the
        /// lying body stirring), the torso out (ExposedAt) and risen (ActiveAt).</summary>
        public readonly struct Marks
        {
            public readonly float Break,Torso,Span;
            public Marks(float brk,float torso,float span){Break=brk;Torso=torso;Span=span;}
        }
        /// <summary>The marks from the add's replicated clocks. The tell is a third of the way to the torso (never past a second,
        /// never under a quarter second): the ground cracking a beat before, not a hit indicator.</summary>
        public static Marks Of(float spawnAt,float exposedAt,float activeAt)
        {
            float span=Math.Max(.2f,activeAt-spawnAt),torso=Math.Max(.1f,Math.Min(span-.1f,exposedAt-spawnAt));
            float brk=Math.Max(Math.Min(.25f,torso*.5f),Math.Min(1f,torso*.35f));return new Marks(brk,torso,span);
        }

        // ================================================================================================================
        // The style by context (presentation: every peer picks the same from the same facts)
        // ================================================================================================================
        /// <summary>The maps whose spawns stand on open ground (the city): every other map is an interior.</summary>
        public static bool Outdoor(string map)=>map!=null&&(map==NativeExpeditionGeneratedWorld.MapId||map.StartsWith(NativeExpeditionCityKit.MapId,StringComparison.Ordinal));
        /// <summary>Dig out or lie and rise: the heavy kinds always dig; on open ground most dig, inside (and in the bleed, under a
        /// ceiling vent) most lie and rise; `seed` the add's (so a crowd mixes, and every peer agrees).</summary>
        public static Style Choose(bool interior,bool bleed,bool heavy,uint seed)
        {
            if(heavy)return Style.Dig;float r=NativeHordeKinds.Hash(seed,331);
            return interior||bleed?(r<.82f?Style.Rise:Style.Dig):(r<.84f?Style.Dig:Style.Rise);
        }
        /// <summary>The kinds that dig out bigger (the brute and the Hulk).</summary>
        public static bool Heavy(int kind)=>kind==NativeHordeKinds.Brute||kind==NativeHordeKinds.Hulk;

        // ================================================================================================================
        // The look: how big the ground breaks (world metres; scaled with the kind)
        // ================================================================================================================
        public readonly struct Look
        {
            /// <summary>The crack field's reach, the hole's radius, rubble count and size, dust, the scar's stay (s; 0 none), the shake.</summary>
            public readonly float Reach,Hole,Chunk,Scar,Shake;public readonly int Cracks,Chunks,Plates,Dust;public readonly bool Heavy;
            public Look(float reach,float hole,int cracks,int chunks,float chunk,int plates,int dust,float scar,float shake,bool heavy)
            {Reach=reach;Hole=hole;Cracks=cracks;Chunks=chunks;Chunk=chunk;Plates=plates;Dust=dust;Scar=scar;Shake=shake;Heavy=heavy;}
        }
        public static readonly Look Light=new Look(.95f,.42f,6,9,.05f,5,8,0,0,false);
        public static readonly Look BruteLook=new Look(1.45f,.55f,8,14,.075f,7,12,24,.035f,true);
        public static readonly Look HulkLook=new Look(2.6f,.95f,11,20,.11f,9,18,40,.09f,true);
        public static Look LookOf(int kind)=>kind==NativeHordeKinds.Hulk?HulkLook:kind==NativeHordeKinds.Brute?BruteLook:Light;
        /// <summary>A lie-and-rise stain's reach (m) for a kind of scale `scale`.</summary>
        public static float StainReach(float scale)=>.95f*scale;
        /// <summary>How long a stain stays after it has risen (s), drying.</summary>
        public const float StainStay=7f;

        // ================================================================================================================
        // Effect shapes (pure; the draw is Runtime/NativeZombieEmergeDraw.cs)
        // ================================================================================================================
        private static float H(uint seed,int i,int k)=>(float)NativeGoreRules.Hash01(seed,i*131+k,0x6e7);
        private static float Sm(float x)=>MeleeMath.Smooth(x);
        private const float Tau=(float)(Math.PI*2);
        /// <summary>The tell's growth `age` s after SpawnAt (0..1 by the break, the crack running on a little after it).</summary>
        public static float Tell(float age,in Marks m)=>age<0?0:Math.Min(1.25f,age/Math.Max(.1f,m.Break));
        /// <summary>The ground's bulge over the spot (m, before the kind's scale): it swells through the tell and gives at the break.</summary>
        public static float Bulge(float age,in Marks m)
        {
            if(age<0)return 0;if(age<m.Break){float u=age/m.Break;return .07f*u*u*(1+.15f*(float)Math.Sin(age*38));}
            return .07f*(1-Sm((age-m.Break)/.12f));
        }
        public const int CrackSegments=4;
        /// <summary>Point j (0..<see cref="CrackSegments"/>) of crack i of `count` creeping out of the spot as the tell grows
        /// (`grow` 0..1.25): its offset on the floor (x, z; m) and the crack's width there (m).</summary>
        public static Vector2 CrackPoint(uint seed,int i,int count,int j,float grow,float reach,out float width)
        {
            float ang=Tau*(i+.55f*H(seed,i,1))/Math.Max(1,count),len=reach*(.5f+.5f*H(seed,i,2))*Math.Min(1,grow),s=j/(float)CrackSegments;
            var dir=new Vector2((float)Math.Cos(ang),(float)Math.Sin(ang));var side=new Vector2(-dir.Y,dir.X);
            float jag=j==0||j==CrackSegments?0:(H(seed,i*8+j,3)-.5f)*.4f*len/CrackSegments;
            width=(.02f+.03f*reach)*(1-.75f*s)*(.7f+.3f*H(seed,i,4))*Math.Min(1,grow*1.6f);
            return dir*(.1f*reach+len*.9f*s)+side*jag;
        }
        /// <summary>Floor plate k of `count` round the spot, heaving with the bulge then thrown aside at the break: its offset
        /// (m; y its lift), tilt (rad, outward), size (m) and how much is left (it sinks away into the rim).</summary>
        public static bool Plate(uint seed,int k,int count,float age,in Marks m,float reach,out Vector3 offset,out float tilt,out float size,out float left)
        {
            offset=Vector3.Zero;tilt=size=left=0;if(age<m.Break*.3f)return false;
            float a=Tau*(k+.5f*H(seed,k,11))/Math.Max(1,count),r=reach*(.28f+.12f*H(seed,k,12));size=reach*(.18f+.08f*H(seed,k,13));
            var dir=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));float heave=Bulge(age,m)*(1.1f+.6f*H(seed,k,14));
            float t=age-m.Break;tilt=(.15f+.25f*H(seed,k,15))*Sm(age/Math.Max(.1f,m.Break));
            if(t>0){float fly=Sm(t/.35f);r+=reach*.22f*fly;tilt+=.6f*fly;heave=Math.Max(0,.12f*reach*(float)Math.Sin(Math.Min(1,t/.35f)*Math.PI))+heave;}
            offset=dir*r+new Vector3(0,heave,0);left=1-Sm((age-m.Span-3)/1.5f);return left>0;
        }
        /// <summary>
        /// Clod i of `count` thrown by a breaking-through (the first hand at the break, the head and shoulders at `burst`):
        /// ballistic from the hole's edge, one bounce, at rest by the rim; offset (m; y over the floor), spin and size (m), how
        /// much is left (they crumble a few seconds after it has risen).
        /// </summary>
        public static bool Clod(uint seed,int i,int count,float since,float hole,float chunk,float stay,out Vector3 offset,out float spin,out float size,out float left)
        {
            offset=Vector3.Zero;spin=size=left=0;if(since<0||since>stay)return false;
            float a=Tau*(i+.7f*H(seed,i,21))/Math.Max(1,count),up=2.2f+2.4f*H(seed,i,22),out_=.9f+1.6f*H(seed,i,23);const float g=14;
            var dir=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));float fly=2*up/g,t=Math.Min(since,fly),y=up*t-.5f*g*t*t,r=hole*.7f+out_*t;
            if(since>fly){float b=since-fly,bf=.35f*up*2/g;float tb=Math.Min(b,bf);y=Math.Max(0,.35f*up*tb-.5f*g*tb*tb);r+=out_*.35f*tb+out_*.08f*Math.Min(.4f,Math.Max(0,b-bf));}
            size=chunk*(.6f+.8f*H(seed,i,24));offset=dir*r+new Vector3(0,y+size*.4f,0);spin=since*(6+8*H(seed,i,25));
            left=1-Sm((since-(stay-1.2f))/1.2f);return true;
        }
        /// <summary>Dust puff i of `count` off a breaking-through: offset (m), size (m), opacity and age share.</summary>
        public static bool Dust(uint seed,int i,int count,float since,float hole,out Vector3 offset,out float size,out float alpha,out float u)
        {
            // (Low, earthy and quick: seen in the Editor, puffs the hole's size and more hid the body as it came out.)
            offset=Vector3.Zero;size=alpha=u=0;const float life=1.1f;if(since<0||since>life)return false;u=since/life;
            float a=Tau*(i+.6f*H(seed,i,31))/Math.Max(1,count),r=hole*(.6f+1.5f*(1-(float)Math.Exp(-since*3.6f)))*(.8f+.4f*H(seed,i,32));
            offset=new Vector3((float)Math.Cos(a)*r,.04f+.18f*hole*u+.08f*u*(.5f+H(seed,i,33)),(float)Math.Sin(a)*r);
            size=hole*(.45f+.7f*Sm(u*1.3f))*(.75f+.5f*H(seed,i,34));alpha=.55f*Sm(since/.06f)*(1-Sm((u-.25f)/.75f));return true;
        }
        /// <summary>Grit trickling off the body as it hauls itself up (a falling grain's offset over the hole, m), while it rises.</summary>
        public static bool Grit(uint seed,int i,float age,in Marks m,float hole,out Vector3 offset,out float alpha)
        {
            offset=Vector3.Zero;alpha=0;if(age<m.Break||age>m.Span+.3f)return false;float period=.45f+.3f*H(seed,i,41),t=(age+period*H(seed,i,42))%period,u=t/period;
            float a=Tau*H(seed,i+(int)((age+period*H(seed,i,42))/period)*7,43),r=hole*(.3f+.7f*H(seed,i,44));
            offset=new Vector3((float)Math.Cos(a)*r,.9f*(1-u)*(1-u)+.02f,(float)Math.Sin(a)*r);alpha=.85f*(1-Sm((age-m.Span)/.3f));return true;
        }
        /// <summary>The dark hole the body comes out of: its radius (m) and darkness `age` s after SpawnAt; it stays as a scar
        /// `scar` s after the add has risen (0: it closes up a few seconds after), then fades.</summary>
        public static bool Hole(float age,in Marks m,float hole,float scar,out float radius,out float alpha)
        {
            radius=alpha=0;if(age<m.Break-.05f)return false;float open=Sm((age-m.Break+.05f)/.25f);radius=hole*(.35f+.65f*open)*(1+.25f*Sm((age-m.Torso)/Math.Max(.1f,m.Span-m.Torso)));
            float stay=scar>0?scar:3.5f,end=m.Span+stay;alpha=.9f*open*(1-Sm((age-end+2.5f)/2.5f));return alpha>0;
        }
        /// <summary>The cracks' darkness `age` s after SpawnAt (they stay with the scar, a heavy kind's, else a few seconds).</summary>
        public static float CrackAlpha(float age,in Marks m,float scar)
        {
            if(age<0)return 0;float stay=scar>0?scar:3.5f,end=m.Span+stay;return .9f*Sm(age/Math.Max(.08f,m.Break*.4f))*(1-Sm((age-end+2.5f)/2.5f));
        }
        /// <summary>How long after SpawnAt an emergence's marks are gone (s).</summary>
        public static float Life(in Marks m,in Look l,bool rise)=>m.Span+(rise?StainStay:Math.Max(l.Scar,3.5f))+.1f;
        /// <summary>The lie-and-rise stain: its reach (m) and darkness `age` s after SpawnAt (seeping out through the tell, then
        /// drying and fading <see cref="StainStay"/> s after it has risen); `wet` its sheen.</summary>
        public static bool Stain(float age,in Marks m,float reach,out float radius,out float alpha,out float wet)
        {
            radius=alpha=wet=0;if(age<0)return false;float seep=Sm(age/Math.Max(.1f,m.Break*1.1f));radius=reach*(.25f+.75f*seep);
            alpha=.82f*Sm(age/.15f)*(1-Sm((age-m.Span-StainStay+2.5f)/2.5f));wet=1-Sm((age-m.Span)/3f);return alpha>0;
        }
        /// <summary>The stain's ragged edge at vertex k of <see cref="NativeZombieFx.Edge"/> (a share of its reach).</summary>
        public static float StainEdge(uint seed,int k)=>NativeZombieFx.BlotchEdge(seed^0x5eedu,k);
    }

    // ====================================================================================================================
    // The pose: dig out (a hand, the other, head and shoulders, the haul, a knee) or lie and rise (stir, push up, kneel), both
    // handing over to the pack's crawl-to-stand at the torso out and standing by ActiveAt.
    // ====================================================================================================================
    public sealed partial class NativeHordeGait
    {
        /// <summary>The crawl-to-stand take's kneel (the torso-out pose) and the end of its rise (standing), s into the take.</summary>
        public const float EmergeKneelAt=1.1f,EmergeStandAt=2.62f;
        /// <summary>The root's height under its standing height when the dig-out starts (rig m): the whole body under the floor.</summary>
        public const float DigDeep=-1.6f;
        private static readonly CapturedRig.Pose emergeA=new CapturedRig.Pose(),emergeB=new CapturedRig.Pose();
        private static readonly float[] emergeRow=new float[NativeZombieClips.Channels];
        private static void TakePose(int clip,float t,CapturedRig.Pose into,float shoulders){NativeZombieClips.Sample(clip,t,emergeRow);NativeZombieClips.Pose(emergeRow,into,true,shoulders);}
        /// <summary>into = a slerped toward b by w (every joint), the body's offset lerped.</summary>
        private static void MixPose(CapturedRig.Pose a,CapturedRig.Pose b,float w,CapturedRig.Pose into)
        {
            for(int j=0;j<CapturedRig.Bones.Length;j++){into.Rotation[j]=Quaternion.Normalize(Quaternion.Slerp(a.Rotation[j],b.Rotation[j],w));into.Position[j]=Vector3.Lerp(a.Position[j],b.Position[j],w);}
        }
        private static readonly Quaternion[] emergeArm=new Quaternion[4];
        /// <summary>The arm reached to `target` (chest space) and blended over the pose's own arm by `w` (joint by joint, so a reach
        /// letting go never flips the elbow).</summary>
        private static void ArmBlend(CapturedRig.Pose p,bool left,Vector3 target,Quaternion wrist,Vector3 pole,float w)
        {
            if(w<=0)return;int c=left?CapturedRig.LClav:CapturedRig.RClav;for(int k=0;k<4;k++)emergeArm[k]=p.Rotation[c+k];
            CapturedRig.Arm(p,left,target,wrist,pole);if(w>=1)return;for(int k=0;k<4;k++)p.Rotation[c+k]=Quaternion.Normalize(Quaternion.Slerp(emergeArm[k],p.Rotation[c+k],w));
        }
        private static void CopyPose(CapturedRig.Pose a,CapturedRig.Pose into){Array.Copy(a.Rotation,into.Rotation,a.Rotation.Length);Array.Copy(a.Position,into.Position,a.Position.Length);}
        /// <summary>Keys a value over (time, value) pairs, eased between them.</summary>
        private static float EmergeKey(float t,float[] k)
        {
            if(t<=k[0])return k[1];for(int i=2;i<k.Length;i+=2)if(t<=k[i]){float u=(t-k[i-2])/Math.Max(1e-5f,k[i]-k[i-2]);return k[i-1]+(k[i+1]-k[i-1])*Sm(u);}return k[k.Length-1];
        }
        // The dig-out's root under its standing height (rig m) over the break-to-torso share v: deep, a hand, the other, the head
        // and shoulders through, the haul to the waist, then up onto a knee.
        private static readonly float[] DigRoot={0,DigDeep,.16f,-1.52f,.32f,-1.42f,.5f,-1.2f,.72f,-.8f,.86f,-.46f,1f,0};
        // How far the trunk pitches over the hole's edge (rad) as it hauls.
        private static readonly float[] DigPitch={0,0,.32f,.05f,.5f,.15f,.72f,.42f,.86f,.62f,1f,0};
        // The right hand's claw out of the ground (its height over the floor, m) and when it slams flat; the left's the same, later.
        private static readonly float[] RightUp={0,-.2f,.06f,.42f,.13f,.58f,.2f,.5f,.27f,.05f},LeftUp={0,-.3f,.14f,-.3f,.22f,.5f,.29f,.55f,.38f,.05f};
        /// <summary>
        /// The emergence's pose `age` s after SpawnAt on its marks, in `style`: writes the pose (rig frame, the root at its standing
        /// point) and the root's height under its standing height (rig m, &lt;= 0: the caller scales it with the kind); false while
        /// nothing of it shows (the tell). From the torso out it is the pack's crawl-to-stand rising, stood by ActiveAt (the
        /// capsule's <see cref="NativeHordeEmerge.Sink"/> follows the same rise).
        /// </summary>
        public static bool Emerge(CapturedRig.Pose p,int kind,uint seed,NativeHordeEmerge.Style style,float age,in NativeHordeEmerge.Marks m,out float lift,int look=-1)
        {
            NativeZombieMotion.Warm();lift=0;int cs=NativeZombieMotion.CrawlStand;float sin=ShoulderInset(kind,look);
            if(age>=m.Torso)
            {
                // The rise: the take from its kneel to standing over the torso-out-to-risen span (its head and the capsule climb together).
                float u=Clamp01((age-m.Torso)/Math.Max(.05f,m.Span-m.Torso));TakePose(cs,EmergeKneelAt+(EmergeStandAt-EmergeKneelAt)*u,p,sin);p.World();return true;
            }
            return style==NativeHordeEmerge.Style.Rise?LieRise(p,kind,seed,age,m,sin,out lift):DigOut(p,kind,seed,age,m,sin,out lift);
        }
        private static bool DigOut(CapturedRig.Pose p,int kind,uint seed,float age,in NativeHordeEmerge.Marks m,float sin,out float lift)
        {
            lift=0;if(age<m.Break)return false;float v=Clamp01((age-m.Break)/Math.Max(.05f,m.Torso-m.Break));
            int cs=NativeZombieMotion.CrawlStand;bool mirror=NativeHordeKinds.Hash(seed,332)<.5f;float side=mirror?-1:1;
            // The trunk in the hole: the pack's standing take, upright, pitched over the edge as it hauls, the head searching.
            TakePose(NativeZombieMotion.Wake,.2f,emergeA,sin);
            float pitch=EmergeKey(v,DigPitch),search=(float)Math.Sin(age*5.3f+NativeHordeKinds.Hash(seed,333)*6)*(1-Sm((v-.6f)/.3f));
            Bend(emergeA,CapturedRig.Spine,pitch*.45f,0,0);Bend(emergeA,CapturedRig.Chest,pitch*.55f,.12f*search,0);
            Bend(emergeA,CapturedRig.Neck,-.25f*(1-v),.3f*search,0);Bend(emergeA,CapturedRig.Head,-.2f*(1-v)+.1f*pitch,.25f*search,.1f*search);
            // The legs hang and kick in the dark until the knee comes up.
            for(int i=0;i<2;i++){int hip=i==0?CapturedRig.LHip:CapturedRig.RHip;float k=(float)Math.Sin(age*7+i*2.1f)*.2f;emergeA.Rotation[hip]=CapturedRig.Euler(-.25f+k);emergeA.Rotation[hip+1]=CapturedRig.Euler(.45f-k);}
            float root=EmergeKey(v,DigRoot);
            // Up onto a knee: the last share hands over to the take's kneel (so the rise starts where this ends).
            float knee=Sm((v-.84f)/.16f);
            if(knee>0){TakePose(cs,EmergeKneelAt,emergeB,sin);MixPose(emergeA,emergeB,knee,p);}else CopyPose(emergeA,p);
            var b=p.Position[CapturedRig.Body];b.Y+=root;p.Position[CapturedRig.Body]=b;p.World();
            // The claws: the first up through the ground, clawing at the air, then slammed flat by the hole and pushing; the other
            // after it. Fixed in the world once planted, so the body hauls up past them.
            float floor=Floor,hold=1-knee;
            for(int i=0;i<2;i++)
            {
                bool first=i==0;bool left=first==mirror;float sx=left?1:-1;var keys=first?RightUp:LeftUp;
                float h=EmergeKey(v,keys),plant=Sm((v-(first?.2f:.29f))/.07f),claw=(float)Math.Sin(age*(first?17:15)+i)*.06f*(1-plant);
                var up=new Vector3(sx*(.2f+.05f*i),floor+h,.08f+claw);var down=new Vector3(sx*(.36f-sin),floor+.05f,.18f+(first?.04f:-.02f));
                var target=Vector3.Lerp(up,down,plant);
                var chest=p.WorldRotation[CapturedRig.Chest];var inv=Quaternion.Inverse(chest);var cpos=p.WorldPosition[CapturedRig.Chest];
                var wrist=Quaternion.Normalize(CapturedRig.Euler(-1.3f-.35f*(1-plant),0,0));
                // (Before its moment the hand waits overhead under the floor; the reach lets go into the take's kneel.)
                ArmBlend(p,left,Vector3.Transform(target-cpos,inv),inv*wrist,Vector3.Transform(new Vector3(sx,.05f,-.35f),inv),hold);
            }
            p.World();lift=0;return true;
        }
        // The lying body (the pack's forward death at rest, its joints a hand over the floor), and the stir, the push up and the kneel.
        private const float LieLift=.13f,LieSink=-.42f;
        private static bool LieRise(CapturedRig.Pose p,int kind,uint seed,float age,in NativeHordeEmerge.Marks m,float sin,out float lift)
        {
            lift=0;float surface=m.Break*.45f;if(age<surface)return false;int cs=NativeZombieMotion.CrawlStand;
            // Lying face down, sunk in the stain and surfacing out of it through the end of the tell.
            int death=NativeZombieClips.Find("death-front-2");if(death<0)death=NativeZombieMotion.CrawlStand;
            TakePose(death,NativeZombieClips.Duration(death),emergeA,sin);var b=emergeA.Position[CapturedRig.Body];b.Y+=LieLift;emergeA.Position[CapturedRig.Body]=b;
            float rise=Sm((age-surface)/Math.Max(.05f,m.Break-surface));
            float v=Clamp01((age-m.Break)/Math.Max(.05f,m.Torso-m.Break));
            // The stir: the head lifts and turns, a hand drags forward; the shudder fades as it pushes.
            float stir=Sm(v/.25f)*(1-Sm((v-.3f)/.25f)),shudder=(float)Math.Sin(age*31)*.05f*Sm(v/.1f)*(1-Sm((v-.55f)/.2f));
            Bend(emergeA,CapturedRig.Neck,-.35f*stir,.35f*stir*(NativeHordeKinds.Hash(seed,334)<.5f?-1:1),shudder);Bend(emergeA,CapturedRig.Head,-.3f*stir,0,0);
            Bend(emergeA,CapturedRig.Chest,shudder,0,0);
            // The push up toward the take's first frame (hands and knees under), then its kneel.
            float push=Sm((v-.22f)/.48f),kneel=Sm((v-.62f)/.38f);
            TakePose(cs,EmergeKneelAt*kneel,emergeB,sin);MixPose(emergeA,emergeB,push,p);
            b=p.Position[CapturedRig.Body];b.Y+=LieSink*(1-rise);p.Position[CapturedRig.Body]=b;p.World();
            // The hands: flopped on the floor beside the head while it lies, one dragging forward as it stirs, then planted under
            // the shoulders and pushing (fixed on the floor), let go as it kneels up.
            float plant=Sm((v-.15f)/.2f),hold=1-kneel;bool dragLeft=NativeHordeKinds.Hash(seed,335)<.5f;
            if(hold>0)for(int i=0;i<2;i++)
            {
                bool left=i==0;float sx=left?1:-1;var chest=p.WorldRotation[CapturedRig.Chest];var inv=Quaternion.Inverse(chest);var cpos=p.WorldPosition[CapturedRig.Chest];
                float drag=left==dragLeft?.16f*Sm(v/.2f):0;
                var flop=new Vector3(sx*(.42f-sin),Floor+.04f,.5f+drag+(left?.05f:-.06f));var under=new Vector3(sx*(.3f-sin),Floor+.05f,.42f+(left?.03f:-.04f)+.12f*(1-push));
                var at=Vector3.Lerp(flop,under,plant);var wrist=Quaternion.Normalize(CapturedRig.Euler(-1.35f-.2f*(1-plant),0,0));
                ArmBlend(p,left,Vector3.Transform(at-cpos,inv),inv*wrist,Vector3.Transform(new Vector3(sx*.8f,.2f,-.4f),inv),hold);
            }
            p.World();return true;
        }
    }
}
