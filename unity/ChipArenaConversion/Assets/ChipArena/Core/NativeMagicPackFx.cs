using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The magic schools with a pack look (NativeMagicPackFx). Darkness keeps NativeMagicFx's void orb.</summary>
    public enum NativeMagicSchool{None=-1,Fire=0,Water=1,Lightning=2,Leet=3}

    /// <summary>One camera-facing sprite of the magic sheet: its centre, size (m), roll (radians), tile, colour (linear, HDR;
    /// alpha its coverage, 0 additive) and white-hot share (Hot: how much of the art's own white stays white; 0 tints it all).
    /// The shared effects renderer draws it (NativeVfxDraw.Pack, NativeVfx.shader mode 12).</summary>
    public struct NativePackSprite{public Vector3 At;public float Size,Roll,Hot;public int Tile;public FxColor Color;}

    /// <summary>
    /// The magic bolts' pack look (owner 2026-10-28: "Lets do all of those", the in-place upgrades from the licensed packs):
    /// each school's primary bolt draws a textured head and trail, and a textured burst where it hits, from one sprite sheet
    /// composed by the pack bank tool (tools/pack_bank/selections/fx-magic.json, id <see cref="SheetId"/>: the effect packs'
    /// missile and explosion sprites, 8 x 4 tiles in the order below). The sprites are tinted with the shot's own halo colour
    /// (NativeShotRules.HaloColor: the weapon's), so the game's palette holds; the sheet gives their shapes and where they run
    /// white-hot (NativeVfx.shader VfxPackSprite). Fire: a flame orb in a flickering ring of fire, fire puffs and molten
    /// embers behind it, a ring flash and flung embers where it hits (the fire staff's blast stays). Water: a swirling drop,
    /// splashes, droplets and bubbles behind it, a splash, spray and bubbles on a hit. Lightning: a crackling ball with arcs
    /// round it, small balls and arcs behind it, a flash, a starburst and arc bolts on a hit. L33t H4x: a glowing ball in a
    /// spinning hexagon, data bits behind it, a cyber flare, an expanding hexagon and flung bits on a hit.
    /// The head replaces the energy core (NativeTrailFx) and the shot's model (NativeShotPresentation hides it); the trail
    /// sprites keep NativeTrailFx's spacing, life, size curve, offsets and drift; the ribbon grows out of the head; the halo,
    /// the spill light and the 120-trail budget are unchanged. Every value is a pure function of the shot key, the sprite index, the
    /// replicated event id and the presentation clock, so host, guests and the killcam draw the same frames. Cosmetic only:
    /// nothing here reaches the authority, hit timing, hitboxes, damage or the network frame. Without the sheet (CI, cloud
    /// builds, the public source) nothing here is drawn and the bolts keep their energy core, model and soft sprites.
    /// </summary>
    public static class NativeMagicPackFx
    {
        public const string SheetId="fx-magic-sheet";public const int Columns=8,Rows=4;
        // ---- the sheet's tiles (fx-magic.json's order) ----
        public const int FireOrb=0,FireRing=1,FireFlame=2,FireSmoke=3,WaterDrop=4,WaterSplash=5,WaterDroplets=6,WaterBubble=7,
            LightningBall=8,LightningArcs=9,ArcFrames=4,LightningArc=13,LeetBall=14,LeetRing=15,LeetBits=16,BitFrames=4,
            Burst=20,BurstFrames=8,Glint=28,Glow=29,LightningFlash=30,Tiles=31;
        /// <summary>Most sprites a head and a hit write.</summary>
        public const int HeadMax=4,ImpactMax=24;
        /// <summary>A hit's burst lasts this long (s).</summary>
        public const double ImpactLife=.5;

        public static NativeMagicSchool School(string spec)=>spec=="fire"?NativeMagicSchool.Fire:spec=="water"?NativeMagicSchool.Water:spec=="lightning"?NativeMagicSchool.Lightning:spec=="leet"?NativeMagicSchool.Leet:NativeMagicSchool.None;
        /// <summary>The school of a shot this look draws: a primary bolt of a magic weapon (as NativeTrailFx.Energy's magic
        /// bullets: not a sword beam, an arcade orb or the Siren's) of the four schools; darkness is NativeMagicFx's.</summary>
        public static NativeMagicSchool Bolt(in NativeShotView v)=>v.Kind==NativeShotKind.Bullet&&v.Class=="magic"&&!v.SwordBeam&&!v.Siren&&v.Arcade==0?School(v.Spec):NativeMagicSchool.None;

        private static float H(long seed,long k)=>(float)NativeWeaponFx.Hash(seed,k);
        private static FxColor White(float k)=>new FxColor(k,k,k);
        // The fire blasts' flame yellow (NativeBlastSim.FireYellow): the fire school's sprites run hot through it, as its blast
        // does, rather than through white (the weapon's orange whitened reads pink).
        private static readonly FxColor Flame=NativeWeaponFx.Linear(NativeBlastSim.FireYellow);
        private static FxColor Add(FxColor c){c.A=0;return c;}
        private static void Put(NativePackSprite[] o,ref int n,Vector3 at,float size,float roll,int tile,FxColor color)
        {if(n<o.Length&&size>1e-4f&&color.Max>1e-4f)o[n++]=new NativePackSprite{At=at,Size=size,Roll=roll,Hot=HotOf(tile),Tile=tile,Color=Add(color)};}
        /// <summary>A tile's white-hot share: the coloured sprites keep their art's white cores; the white masks (the hexagon,
        /// the data bits, the flare flipbook) take the shot's colour nearly all through, a faint white core left.</summary>
        public static float HotOf(int tile)=>tile==LeetRing||tile>=LeetBits&&tile<Burst+BurstFrames?.15f:1f;

        /// <summary>
        /// The bolt's head at `time` (the presentation clock): its sprites, `radius` the visual radius (NativeTrailFx.Radius),
        /// `halo` the shot's colour (linear), `seed` the emitter's (the shot key). Returns how many were written to `o`
        /// (at most HeadMax).
        /// </summary>
        public static int Head(NativeMagicSchool school,Vector3 at,float radius,FxColor halo,long seed,double time,NativePackSprite[] o)
        {
            int n=0;float r=Math.Max(.05f,radius),t=(float)time,spin=H(seed,1)*6.2831853f;
            switch(school)
            {
                case NativeMagicSchool.Fire:
                {
                    // A flame orb, white-hot in the middle, in two rings of fire turning opposite ways and flickering.
                    float pulse=.94f+.06f*(float)Math.Sin(t*23+spin);
                    Put(o,ref n,at,r*6.4f*pulse,spin+t*.8f,FireOrb,FxColor.Lerp(halo,Flame,.5f)*1.6f);
                    for(int k=0;k<2;k++){float f=.9f+.1f*(float)Math.Sin(t*(31+k*7)+k*2.1f+spin);Put(o,ref n,at,r*(5.2f+k*1.2f)*f,spin+k*2.4f+(k==0?1:-1)*t*5.5f,FireRing,FxColor.Lerp(halo,Flame,.25f)*(1.25f-.25f*k));}
                    break;
                }
                case NativeMagicSchool.Water:
                    // A swirling drop with a faint bubble round it.
                    Put(o,ref n,at,r*6.2f,spin+t*1.6f,WaterDrop,halo*1.3f);
                    Put(o,ref n,at,r*6.8f,spin-t*.9f,WaterBubble,halo*.45f);
                    break;
                case NativeMagicSchool.Lightning:
                {
                    // A ball that flickers at 15 Hz with three arcs round it, each re-struck every 50 ms.
                    long beat=(long)Math.Floor(t*15);float flicker=H(seed,1000+beat);
                    Put(o,ref n,at,r*(5.6f+2.4f*flicker),spin,LightningBall,halo*1.5f);
                    for(int k=0;k<3;k++)
                    {
                        long slot=(long)Math.Floor(t*20+k*.33f);
                        Put(o,ref n,at,r*(6.2f+1.6f*H(seed,2000+slot*3+k)),H(seed,3000+slot*3+k)*6.2831853f,LightningArcs+(int)(H(seed,4000+slot*3+k)*ArcFrames)%ArcFrames,FxColor.Lerp(White(1.6f),halo*1.6f,.55f));
                    }
                    break;
                }
                case NativeMagicSchool.Leet:
                {
                    // A glowing ball in a spinning hexagon, a smaller one turning the other way.
                    float pulse=.92f+.08f*(float)Math.Sin(t*17+spin);
                    Put(o,ref n,at,r*5.6f*pulse,0,LeetBall,halo*1.5f);
                    Put(o,ref n,at,r*6.8f,spin+t*2.6f,LeetRing,halo*1.3f);
                    Put(o,ref n,at,r*4.4f,spin-t*3.8f,LeetRing,halo*1f);
                    break;
                }
            }
            return n;
        }

        /// <summary>
        /// Trail sprite `index` of a bolt (NativeTrailFx.Sprite gives its life, size, offset and drift): its tile, a size factor
        /// on NativeTrailFx's, its roll at birth and its spin (radians a second).
        /// </summary>
        public static void Trail(NativeMagicSchool school,long seed,long index,out int tile,out float scale,out float roll,out float spin)
        {
            float h=H(seed,index*4+9),pick=H(seed,index*4+10);roll=H(seed,index*4+11)*6.2831853f;spin=(H(seed,index*4+12)-.5f)*7f;
            switch(school)
            {
                // Fire: puffs of flame (rings and tongues), every third a molten ember cloud.
                case NativeMagicSchool.Fire:if(index%3==2){tile=FireSmoke;scale=1.25f;}else{tile=pick<.55f?FireRing:FireFlame;scale=1f+.15f*h;}break;
                // Water: splashes, droplets, bubbles.
                case NativeMagicSchool.Water:{long k=index%3;tile=k==0?WaterSplash:k==1?WaterDroplets:WaterBubble;scale=k==0?1.15f:k==1?.8f:.45f;spin*=.4f;break;}
                // Lightning: small balls and arcs.
                case NativeMagicSchool.Lightning:if(index%2==0){tile=LightningBall;scale=.55f+.2f*h;}else{tile=LightningArcs+(int)(pick*ArcFrames)%ArcFrames;scale=1.05f;}spin=0;break;
                // L33t: data bits (squares and hexagons), every fourth a small hexagon ring.
                case NativeMagicSchool.Leet:if(index%4==3){tile=LeetRing;scale=.8f;}else{tile=LeetBits+(int)(pick*BitFrames)%BitFrames;scale=.5f+.15f*h;}break;
                default:tile=-1;scale=1;break;
            }
        }
        /// <summary>A trail sprite's colour at u (0..1) of its life: white-hot at birth into the halo, deepening as it fades
        /// (NativeTrailFx.SpriteAlpha's in and out on top).</summary>
        public static FxColor TrailColor(NativeMagicSchool school,FxColor halo,double u,int tile)
        {
            float x=NativeWeaponFx.Clamp01(u),a=NativeTrailFx.SpriteAlpha(u),gain=school==NativeMagicSchool.Lightning?1.6f:school==NativeMagicSchool.Water?1.1f:1.3f;
            if(tile==FireSmoke)gain*=.6f;
            // White-hot at birth, except the white masks (L33t's data bits), which would read as white specks: they start in the halo.
            var hot=FxColor.Lerp(HotOf(tile)<1?halo*1.4f:school==NativeMagicSchool.Fire?Flame*1.4f:White(1.3f),halo*1.2f,NativeWeaponFx.Smooth(x/.35));var c=FxColor.Lerp(hot,halo*.6f,NativeWeaponFx.Smooth((x-.35)/.65));
            return c*(gain*a);
        }

        /// <summary>
        /// A bolt's hit at `age` seconds: its burst sprites. `at` the hit point; `normal` the struck surface's (zero on a pilot:
        /// the burst opens all round); `seed` from the replicated event id; `tint` the bolt's colour (linear); `scale` its size;
        /// `quality` the effects quality (Reduced .3: fewer pieces); `low` the low graphics tier (fewer). Returns the count
        /// written to `o` (at most ImpactMax), none once ImpactLife has passed.
        /// </summary>
        public static int Impact(NativeMagicSchool school,Vector3 at,Vector3 normal,long seed,double age,float scale,FxColor tint,float quality,bool low,NativePackSprite[] o)
        {
            int n=0;if(!(age>=0&&age<ImpactLife)||school==NativeMagicSchool.None)return 0;
            float t=(float)age,s=Math.Max(.1f,scale);bool surface=normal.LengthSquared()>.25f;var nrm=surface?Vector3.Normalize(normal):Vector3.UnitY;
            Vector3 lift=surface?nrm*(.06f*s):Vector3.Zero;int pieces=quality<.5f?3:low?5:8;
            // A quick glint at the point, white into the tint.
            {float u=t/.12f;if(u<1)Put(o,ref n,at+lift,s*.8f*(1-.4f*u),H(seed,1)*6.2831853f,Glint,FxColor.Lerp(White(2f),tint*2f,u)*(1-u));}
            switch(school)
            {
                case NativeMagicSchool.Fire:
                    // Two ring flashes (a fast and a slower one), flung embers, one molten ember cloud.
                    for(int k=0;k<2;k++){float life=.24f+.1f*k,u=t/life;if(u<1){float grow=u<.1f?.6f+4*u:1-.85f*(u-.1f)/.9f;Put(o,ref n,at+lift,s*(1.25f+.35f*k)*grow,H(seed,2+k)*6.2831853f+t*(k==0?4:-3),FireRing,FxColor.Lerp(Flame*1.8f,tint*1.6f,Math.Min(1,u*2.5f))*(u<.6f?1:(1-u)/.4f));}}
                    Pieces(o,ref n,at+lift,nrm,surface,seed,t,pieces,3,7,6,.35f,.5f,.15f*s,.24f*s,FireOrb,FxColor.Lerp(tint,Flame,.4f),1.8f);
                    {float u=t/.48f;if(u<1)Put(o,ref n,at+lift+new Vector3(0,.25f*s*u,0),s*(.8f+.4f*u),H(seed,5)*6.2831853f+t,FireSmoke,tint*(1.1f*(u<.2f?u/.2f:(1-u)/.8f)));}
                    break;
                case NativeMagicSchool.Water:
                    // A drop flash, two splashes growing out, a spray of droplets, bubbles drifting up.
                    {float u=t/.2f;if(u<1)Put(o,ref n,at+lift,s*(.8f+.5f*u),H(seed,2)*6.2831853f,WaterDrop,tint*(1.4f*(1-u)));}
                    for(int k=0;k<2;k++){float life=.34f+.1f*k,u=t/life;if(u<1)Put(o,ref n,at+lift,s*(.9f+.4f*k)*(.55f+.45f*(float)Math.Sqrt(u)),H(seed,3+k)*6.2831853f,WaterSplash,tint*(1.25f*(u<.15f?u/.15f:(1-u)/.85f)));}
                    {float u=t/.3f;if(u<1)Put(o,ref n,at+lift,s*1.2f*(.6f+.4f*u),H(seed,6)*6.2831853f,WaterDroplets,tint*(1.1f*(1-u*u)));}
                    for(int k=0;k<Math.Min(pieces,5);k++)
                    {
                        float life=.35f+.15f*H(seed,40+k),u=t/life;if(u>=1)continue;var d=Dir(seed,50+k,nrm,surface);
                        Put(o,ref n,at+lift+d*(.35f*s*(float)Math.Sqrt(u))+new Vector3(0,.3f*s*u,0),s*(.12f+.08f*H(seed,60+k))*(1-.3f*u),0,WaterBubble,tint*(1.1f*(1-u)));
                    }
                    break;
                case NativeMagicSchool.Lightning:
                {
                    // A flash and a starburst, then three arc bolts struck afresh every 40 ms and small arcs round them.
                    {float u=t/.16f;if(u<1)Put(o,ref n,at+lift,s*1.4f*(1-.3f*u),0,LightningBall,tint*(2f*(1-u)));}
                    {float u=t/.22f;if(u<1)Put(o,ref n,at+lift,s*1.7f*(.7f+.3f*u),H(seed,2)*6.2831853f,LightningFlash,tint*(1.6f*(1-u)*(1-u)));}
                    long beat=(long)Math.Floor(t/.04f);
                    for(int k=0;k<3;k++){float u=t/.3f;if(u<1&&H(seed,100+beat*3+k)>.2f)Put(o,ref n,at+lift,s*(1.1f+.5f*H(seed,200+beat*3+k)),H(seed,300+beat*3+k)*6.2831853f,LightningArc,FxColor.Lerp(White(2f),tint*2f,.5f)*(1-u));}
                    for(int k=0;k<Math.Min(pieces,4);k++){float u=t/.26f;if(u<1)Put(o,ref n,at+lift+Dir(seed,400+k,nrm,surface)*(.35f*s),s*.7f,H(seed,500+beat*4+k)*6.2831853f,LightningArcs+(int)(H(seed,600+beat*4+k)*ArcFrames)%ArcFrames,tint*(1.6f*(1-u)));}
                    break;
                }
                case NativeMagicSchool.Leet:
                {
                    // A cyber flare (the flipbook), a hexagon expanding out, data bits flung.
                    {float u=t/.36f;if(u<1)Put(o,ref n,at+lift,s*1.35f*(.75f+.25f*u),H(seed,2)*6.2831853f,Burst+Math.Min(BurstFrames-1,(int)(u*BurstFrames)),tint*(1.6f*(u<.7f?1:(1-u)/.3f)));}
                    {float u=t/.3f;if(u<1)Put(o,ref n,at+lift,s*(.25f+1.5f*(float)Math.Sqrt(u)),H(seed,3)*6.2831853f+t*2,LeetRing,tint*(1.6f*(1-u)));}
                    Pieces(o,ref n,at+lift,nrm,surface,seed,t,pieces,2,4.5f,3,.35f,.45f,.12f*s,.2f*s,-1,tint,1.6f);
                    break;
                }
            }
            return n;
        }
        // A direction for piece k: over the hemisphere about a surface's normal (biased toward it), else the sphere.
        private static Vector3 Dir(long seed,long k,Vector3 normal,bool surface)
        {
            var d=NativeWeaponFx.Sphere(NativeWeaponFx.Hash(seed,k*2+700),NativeWeaponFx.Hash(seed,k*2+701));
            if(surface){if(Vector3.Dot(d,normal)<0)d-=2*Vector3.Dot(d,normal)*normal;d=Vector3.Normalize(d+normal*.4f);}
            return d;
        }
        // Flung pieces: count of them out at vmin..vmax m/s under gravity (m/s²) with drag, life lmin..lmax s, shrinking from
        // size smin..smax, tile (-1: a data bit, the L33t symbols by seed), white-hot into the tint.
        private static void Pieces(NativePackSprite[] o,ref int n,Vector3 at,Vector3 normal,bool surface,long seed,float t,int count,float vmin,float vmax,float gravity,float lmin,float lmax,float smin,float smax,int tile,FxColor tint,float gain)
        {
            for(int k=0;k<count;k++)
            {
                float life=lmin+(lmax-lmin)*H(seed,800+k),u=t/life;if(u>=1)continue;var d=Dir(seed,900+k,normal,surface);float v=vmin+(vmax-vmin)*H(seed,1000+k);
                float travel=v*(1-(float)Math.Exp(-3*t))/3;var p=at+d*travel+new Vector3(0,-.5f*gravity*t*t,0);
                int tl=tile>=0?tile:LeetBits+(int)(H(seed,1100+k)*BitFrames)%BitFrames;
                Put(o,ref n,p,(smin+(smax-smin)*H(seed,1200+k))*(1-.6f*u),H(seed,1300+k)*6.2831853f+t*(H(seed,1400+k)-.5f)*8,tl,FxColor.Lerp(White(gain),tint*gain,Math.Min(1,u*3))*(u<.8f?1:(1-u)/.2f));
            }
        }
    }
}
