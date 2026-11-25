using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The realistic explosions, fire and blood spray (Claude X1, 2026-11-25; effects plan phase 4, owner: "go with recommended" on
    /// V1's gallery): the licensed fire pack's flipbooks (the fireball burst, the aftermath smoke, the smoke-and-dust burst, the campfire,
    /// the large flame and the campfire smoke) and the licensed blood pack's sprites (three splatters and the mist), rebuilt here
    /// as the recipes V1 tested in its lab (scratch fx-audit, ChipArenaV1Lab.cs), each a pure function of an effect's age and seed,
    /// so host, guests and the killcam draw the same frame. Runtime NativeVfxDraw draws them in its one sorted batch (NativeVfx.shader
    /// modes 13 fire, 14 smoke and dust, 15 blood).
    /// <para>Where the sheets come from: the explosion and fire sheets are the separate effects bank (git-ignored
    /// Assets/LocalAssets/ChipArenaFx/Resources/<see cref="Folder"/>, the folder Claude C2's cache effects load from, shipped by
    /// Astra's effects-bank packer); the blood sheets ride the licensed-assets zip (Assets/LocalAssets/ChipArenaLocal/Resources/
    /// <see cref="BloodFolder"/>, packed with the visual body). Each is read once by name; without them (CI, cloud builds, other
    /// checkouts, the public source) nothing here draws and every effect keeps today's look.</para>
    /// <para>What stays code-drawn (V1's readability guardrails): a blast's shock front (it ends exactly on the gameplay radius), its
    /// lights, its white-hot core and halo glows, sparks and rubble, every telegraph, and the RVFX wound splashes (the spray is an extra
    /// layer over them, never the hit cue). What the pack replaces: a restyled blast's toon puffs, toon flashes and crescent licks
    /// (<see cref="Replaced"/>), the fire fields' toon flame tongues and the slam landing's toon dust puffs. Cosmetic only: no blast
    /// radius, timing, damage, random draw, network message or rule reads anything here.</para>
    /// </summary>
    public static class NativeFxPack
    {
        /// <summary>The effects bank's sheets: Resources/<see cref="Folder"/>/<i>name</i> in Assets/LocalAssets/ChipArenaFx.</summary>
        public const string Folder="ChipArenaBlastFx";
        /// <summary>The blood spray's sheets: Resources/<see cref="BloodFolder"/>/<i>name</i> in Assets/LocalAssets/ChipArenaLocal (the zip).</summary>
        public const string BloodFolder="ChipArenaBloodSpray";
        /// <summary>The sheets (NativeVfx.shader _FxSheet0-7): 8 x 8 flipbooks, row 0 at the image's top, except the splatters (2 x 2 tiles).</summary>
        public const int Burst=0,Smoke=1,Dust=2,Flame=3,Large=4,FlameSmoke=5,Splatters=6,Mist=7,Sheets=8;
        public static readonly string[] Names={"burst","smoke","dust","flame","large","flamesmoke","splatters","mist"};
        /// <summary>The Resources path of a sheet.</summary>
        public static string Path(int sheet)=>(Blood(sheet)?BloodFolder:Folder)+"/"+Names[sheet];
        public static bool Blood(int sheet)=>sheet==Splatters||sheet==Mist;
        public static int Grid(int sheet)=>sheet==Splatters?2:8;
        public static int Frames(int sheet)=>Grid(sheet)*Grid(sheet);
        /// <summary>The splatter atlas's tiles: two round splatters, a directional one (its drops run along +u) and a spare.</summary>
        public const int SplatA=0,SplatB=1,SplatLine=2;
        /// <summary>The layer's switch (a DEV and capture comparison, as NativeCachePackFx.On; the game leaves it on).</summary>
        public static bool On=true;

        // ---- which effects the pack restyles ----
        /// <summary>The blasts that take the fireball (rockets, barrels, mines, meteors: the Fire family, untinted).</summary>
        public static bool Fireball(NativeBlastStyle style,bool tinted)=>!tinted&&(style==NativeBlastStyle.Fire||style==NativeBlastStyle.Mine||style==NativeBlastStyle.Meteor);
        /// <summary>The frag's own tint (NativeBlastSim.TintOf("frag"): its warm greys).</summary>
        public static bool Frag(NativeBlastTint? tint)=>tint.HasValue&&tint.Value.Core==0xfff3e2&&tint.Value.Hot==0xcfc8bc&&tint.Value.Cool==0x676b72&&tint.Value.Look==NativeBlastLook.Default;
        /// <summary>The blasts that take the smoke-and-dust burst: the Kinetic poof (the frag's, and the untinted dash blast, kinetic and
        /// bubble mine; not the Tidal Wave or a tinted pulse) and the Quake slam (quake, aftershock, trampoline).</summary>
        public static bool Dusty(NativeBlastStyle style,NativeBlastTint? tint)=>style==NativeBlastStyle.Kinetic&&(!tint.HasValue||Frag(tint))||style==NativeBlastStyle.Quake&&!tint.HasValue;
        public static bool Restyled(NativeBlastStyle style,NativeBlastTint? tint)=>Fireball(style,tint.HasValue)||Dusty(style,tint);
        /// <summary>A restyled blast's pool particles the pack draws instead: its toon puffs, toon flashes and crescent licks. Its halo and core
        /// glows, sparks, embers, rubble, slabs, shell voxels (never drawn) and its shock front stay code-drawn.</summary>
        public static bool Replaced(NativeFxRole role)=>role==NativeFxRole.Fireball||role==NativeFxRole.Smoke||role==NativeFxRole.Flash||role==NativeFxRole.Lick;

        // ---- the recipes' numbers (metres and seconds at a 3 m blast; a blast scales by NativeBlastSim's own size, clamp(R/3, .45, 2.2)) ----
        /// <summary>How long a restyled blast's record lives (its smoke's longest life plus its delay).</summary>
        public const double BurstLife=4.4;
        /// <summary>Fix 1 (V1: the fireball read smaller than today's flash in its first .12 s): a big flash glow and one bright ignition burst
        /// at full size from the first frame; the burst sheet's fire starts opaque (no fade-in) and near its full size.</summary>
        public const float FlashSize=6.8f,FlashLife=.18f,IgniteSize=3.6f,IgniteLife=.55f;
        /// <summary>Fix 2 (V1: the smoke-and-dust burst hardly read on dark stages): its smoke is a lighter grey, it never goes darker than a
        /// floor of self light (the explosion's own glow lighting it), and a bigger fire core and flash open it.</summary>
        public const float DustSelf=.55f,SmokeSelf=.3f,FireSmokeSelf=.12f;
        public static readonly Vector3 FlashTint=new Vector3(1,.72f,.38f),DustFlashTint=new Vector3(1,.86f,.62f),EmberTint=new Vector3(1,.5f,.15f);
        public const int FireSmokeHex=0x9e9a94,FireballSmokeHex=0x5f5a55,DustHex=0xc7b79e,FragSmokeHex=0xa19b94,SlamDustHex=0xb6a895,FlameSmokeHex=0x5e5b58;
        /// <summary>Blood: the RVFX splash's own tint (NativeBloodSplash.Tint), a little darker for the spray's sheets.</summary>
        public const int BloodHex=0x6c0a10;

        // ---- hashing: stable on every machine ----
        public static float H(uint seed,int i,uint salt){unchecked{uint h=seed*0x9e3779b1u^(uint)i*0x85ebca6bu^salt*0xc2b2ae35u;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;h*=0x297a2d39u;h^=h>>15;return (h&0xffffff)/16777216f;}}
        private static float Mix(float a,float b,float t)=>a+(b-a)*t;
        /// <summary>A curve of evenly spaced keys over 0..1, smooth between keys.</summary>
        public static float Curve(float[] k,float u)
        {
            if(k==null||k.Length==0)return 1;if(k.Length==1||!(u>0))return k[0];if(u>=1)return k[k.Length-1];
            float x=u*(k.Length-1);int i=(int)x;float t=x-i;t=t*t*(3-2*t);return Mix(k[i],k[i+1],t);
        }
        private static Vector3 Dir(uint seed,int i,uint salt)
        {
            float z=H(seed,i,salt)*2-1,a=H(seed,i,salt+1)*6.2831853f,r=(float)Math.Sqrt(Math.Max(0,1-z*z));
            return new Vector3(r*(float)Math.Cos(a),z,r*(float)Math.Sin(a));
        }

        /// <summary>One emitter of a recipe (V1's lab Em): a burst of Count sprites at Delay, or (Rate &gt; 0) a stream of Rate a second.</summary>
        public sealed class Emitter
        {
            public int Sheet=-1;public NativeFxLook Look;public int Count;public float Rate,Delay,Life0=1,Life1=1,Size0=1,Size1=1,Speed0,Speed1,Gravity,Drag,Radius,Rot=360;
            public NativeFxShape Shape;public float Angle=25;public float OffsetY;public float[] Heat,Alpha,Grow;public float HeatGain=1,Gain=1,Self,Occlusion=1;public int Hex;
            public Vector3 Tint=Vector3.One;public bool Loop;public float Stretch=.04f;
        }
        public enum NativeFxShape{Sphere,Hemisphere,Ring,Cone}

        // V1's fireball recipe (the fire pack's burst and aftermath smoke) with fix 1; the embers are the code's HDR streaks.
        public static readonly Emitter[] FireballRecipe=
        {
            new Emitter{Look=NativeFxLook.Glow,Count=1,Life0=FlashLife,Life1=FlashLife,Size0=FlashSize,Size1=FlashSize,OffsetY=1f,Gain=3,Tint=FlashTint,Alpha=new[]{1f,.45f,0}},
            new Emitter{Sheet=Burst,Look=NativeFxLook.Fire,Count=1,Life0=IgniteLife,Life1=IgniteLife,Size0=IgniteSize,Size1=IgniteSize,OffsetY=1f,Rot=40,Gain=2.3f,HeatGain=1.5f,Occlusion=.8f,Hex=FireballSmokeHex,Self=FireSmokeSelf,
                Heat=new[]{1f,1f,.8f},Alpha=new[]{1f,1f,.55f,0},Grow=new[]{1f,1.25f}},
            new Emitter{Sheet=Burst,Look=NativeFxLook.Fire,Count=4,Life0=1.3f,Life1=1.7f,Size0=4.2f,Size1=5.2f,Speed0=.4f,Speed1=1f,Radius=.7f,OffsetY=1.2f,Gain=2,HeatGain=1.35f,Occlusion=.85f,Hex=FireballSmokeHex,Self=FireSmokeSelf,
                Heat=new[]{1f,.85f,.45f,.15f,0},Alpha=new[]{1f,1f,.9f,.6f,0},Grow=new[]{.85f,1.05f,1.25f}},
            new Emitter{Sheet=Smoke,Look=NativeFxLook.Smoke,Count=5,Delay=.15f,Life0=3f,Life1=4f,Size0=4.6f,Size1=6.2f,Speed0=.6f,Speed1=1.3f,Shape=NativeFxShape.Hemisphere,Radius=1f,OffsetY=1.3f,Hex=FireSmokeHex,Self=SmokeSelf,
                Alpha=new[]{0f,.95f,.9f,.6f,0},Grow=new[]{.7f,1.3f}},
            new Emitter{Look=NativeFxLook.Streak,Count=30,Life0=.6f,Life1=1.4f,Size0=.05f,Size1=.1f,Speed0=5,Speed1=11,Gravity=.8f,Drag=1.2f,OffsetY=.8f,Gain=5,Tint=EmberTint,Alpha=new[]{1f,1f,0},Stretch=.03f},
        };
        // V1's smoke-and-dust recipe (a short fire core, dark smoke, a ground dust ring) with fix 2; the shards and sparks are the code's.
        public static readonly Emitter[] DustRecipe=
        {
            new Emitter{Look=NativeFxLook.Glow,Count=1,Life0=.14f,Life1=.14f,Size0=5.6f,Size1=5.6f,OffsetY=.8f,Gain=4,Tint=DustFlashTint,Alpha=new[]{1f,.35f,0}},
            new Emitter{Sheet=Burst,Look=NativeFxLook.Fire,Count=2,Life0=.75f,Life1=.85f,Size0=3.2f,Size1=3.6f,Radius=.3f,OffsetY=.9f,Rot=60,Gain=2.6f,HeatGain=1.6f,Occlusion=.75f,Hex=FragSmokeHex,Self=DustSelf,
                Heat=new[]{1f,.55f,0},Alpha=new[]{1f,1f,.7f,0},Grow=new[]{.9f,1.15f}},
            new Emitter{Sheet=Smoke,Look=NativeFxLook.Smoke,Count=7,Delay=.03f,Life0=2.5f,Life1=3.5f,Size0=3.6f,Size1=5.2f,Speed0=1.2f,Speed1=2.6f,Drag=1.5f,Shape=NativeFxShape.Hemisphere,Radius=.6f,OffsetY=.6f,Hex=FragSmokeHex,Self=DustSelf,
                Alpha=new[]{.8f,1f,.9f,.55f,0},Grow=new[]{.8f,1.4f}},
            new Emitter{Sheet=Dust,Look=NativeFxLook.Smoke,Count=10,Life0=1.4f,Life1=2.2f,Size0=1.4f,Size1=2.4f,Speed0=4,Speed1=7,Drag=2.5f,Shape=NativeFxShape.Ring,Radius=.4f,OffsetY=.25f,Hex=DustHex,Self=DustSelf,
                Alpha=new[]{.8f,1f,.65f,0},Grow=new[]{.6f,1.4f}},
        };
        // The Quake slam: the dust ring wider and lower, the smoke slower; its slam wave, slabs and rubble stay the code's.
        public static readonly Emitter[] QuakeRecipe=
        {
            new Emitter{Sheet=Dust,Look=NativeFxLook.Smoke,Count=14,Life0=1.3f,Life1=2f,Size0=1.8f,Size1=2.8f,Speed0=6.5f,Speed1=9.5f,Drag=3f,Shape=NativeFxShape.Ring,Radius=.45f,OffsetY=.3f,Hex=DustHex,Self=DustSelf,
                Alpha=new[]{.6f,.95f,.6f,0},Grow=new[]{.6f,1.4f}},
            new Emitter{Sheet=Smoke,Look=NativeFxLook.Smoke,Count=4,Delay=.08f,Life0=2.2f,Life1=3f,Size0=3.2f,Size1=4.4f,Speed0=.6f,Speed1=1.2f,Drag=1.2f,Shape=NativeFxShape.Hemisphere,Radius=1.2f,OffsetY=.5f,Hex=DustHex,Self=DustSelf,
                Alpha=new[]{.4f,.85f,.7f,.4f,0},Grow=new[]{.7f,1.3f}},
        };
        // A melee slam's landing (NativeSlamDust's place and clock): a small dust ring and a low puff; its stones stay the code's.
        public static readonly Emitter[] SlamRecipe=
        {
            new Emitter{Sheet=Dust,Look=NativeFxLook.Smoke,Count=10,Life0=.9f,Life1=1.3f,Size0=1.3f,Size1=2.1f,Speed0=3.2f,Speed1=5f,Drag=2.8f,Shape=NativeFxShape.Ring,Radius=.25f,OffsetY=.2f,Hex=SlamDustHex,Self=DustSelf,
                Alpha=new[]{.85f,1f,.6f,0},Grow=new[]{.6f,1.5f}},
            new Emitter{Sheet=Smoke,Look=NativeFxLook.Smoke,Count=3,Delay=.04f,Life0=1.1f,Life1=1.3f,Size0=1.6f,Size1=2.2f,Speed0=.3f,Speed1=.7f,Shape=NativeFxShape.Hemisphere,Radius=.3f,OffsetY=.35f,Hex=SlamDustHex,Self=DustSelf,
                Alpha=new[]{.6f,.85f,.5f,0},Grow=new[]{.7f,1.3f}},
        };
        // V1's campfire recipe (looping streams; size s the flame's width): flame, smoke, glow, embers. The large flame swaps the sheet.
        public static Emitter[] FireRecipe(bool large)=>large?LargeFire:Campfire;
        private static Emitter[] FireStreams(int sheet)=>new[]
        {
            new Emitter{Look=NativeFxLook.Glow,Loop=true,Rate=5,Life0=.8f,Life1=1,Size0=1.3f,Size1=1.5f,OffsetY=.35f,Gain=.12f,Tint=new Vector3(1,.45f,.12f),Alpha=new[]{0f,.7f,.7f,0}},
            new Emitter{Sheet=sheet,Look=NativeFxLook.Fire,Loop=true,Rate=3.2f,Life0=2f,Life1=2.1f,Size0=1,Size1=1.15f,Speed0=.02f,Speed1=.06f,Shape=NativeFxShape.Ring,Radius=.12f,OffsetY=.42f,Rot=12,Gain=1.35f,HeatGain=1.1f,Occlusion=.6f,Hex=FlameSmokeHex,Self=FireSmokeSelf,
                Heat=new[]{1f,1f,.9f},Alpha=new[]{0f,1f,1f,1f,0}},
            new Emitter{Sheet=FlameSmoke,Look=NativeFxLook.Smoke,Loop=true,Rate=1.6f,Life0=3.2f,Life1=3.6f,Size0=1.2f,Size1=1.7f,Speed0=.7f,Speed1=1,Shape=NativeFxShape.Cone,Angle=8,OffsetY=.9f,Hex=FlameSmokeHex,Self=.06f,
                Alpha=new[]{0f,.5f,.4f,0},Grow=new[]{.6f,1.5f}},
            new Emitter{Look=NativeFxLook.Streak,Loop=true,Rate=10,Life0=1,Life1=2.2f,Size0=.025f,Size1=.05f,Speed0=.8f,Speed1=2.2f,Gravity=-.08f,Shape=NativeFxShape.Cone,Angle=18,Radius=.15f,OffsetY=.2f,Gain=3,Tint=new Vector3(1,.55f,.18f),Alpha=new[]{0f,1f,1f,0},Stretch=.03f},
        };
        private static readonly Emitter[] Campfire=FireStreams(Flame),LargeFire=FireStreams(Large);
        // V1's blood pack spray rebuild (along the wound's exit direction): mist, two splatters, stretched drops.
        public static readonly Emitter[] BloodRecipe=
        {
            new Emitter{Sheet=Mist,Look=NativeFxLook.Blood,Count=2,Life0=.55f,Life1=.7f,Size0=.45f,Size1=.7f,Speed0=.6f,Speed1=1.2f,Shape=NativeFxShape.Cone,Angle=25,Gain=1,Hex=BloodHex,Self=.05f,Occlusion=.55f,Alpha=new[]{0f,1f,.5f,0},Grow=new[]{.5f,1.3f}},
            new Emitter{Sheet=Splatters,Look=NativeFxLook.Blood,Count=2,Life0=.3f,Life1=.4f,Size0=.3f,Size1=.45f,Speed0=1.2f,Speed1=2.2f,Shape=NativeFxShape.Cone,Angle=15,Gain=1,Hex=BloodHex,Self=.05f,Alpha=new[]{1f,1f,0},Grow=new[]{.4f,1.2f,1.35f}},
            new Emitter{Sheet=Splatters,Look=NativeFxLook.Blood,Count=2,Delay=.02f,Life0=.35f,Life1=.45f,Size0=.25f,Size1=.4f,Speed0=2,Speed1=3.2f,Shape=NativeFxShape.Cone,Angle=22,Gain=1,Hex=BloodHex,Self=.05f,Alpha=new[]{1f,.9f,0},Grow=new[]{.5f,1.2f}},
            new Emitter{Sheet=Splatters,Look=NativeFxLook.Blood,Count=24,Life0=.5f,Life1=.9f,Size0=.05f,Size1=.12f,Speed0=2.5f,Speed1=5.5f,Gravity=1.4f,Shape=NativeFxShape.Cone,Angle=30,Gain=1,Hex=BloodHex,Self=.05f,Alpha=new[]{1f,1f,0},Stretch=.045f},
        };
        /// <summary>The splatter tile each blood emitter draws (the mist plays its flipbook).</summary>
        private static int BloodTile(int emitter,int i)=>emitter==1?SplatA:emitter==2?SplatB:SplatLine;

        // ---- emission ----
        /// <summary>How many of an emitter's Count a quality keeps (at least one; the low tier halves it).</summary>
        public static int Keep(int count,float quality,bool low){if(count<=1)return count;float q=Math.Max(.25f,Math.Min(1,quality))*(low?.5f:1);return Math.Max(1,(int)Math.Round(count*q));}

        /// <summary>A restyled blast's layer at `age` s after it (its NativeBlastBurst): the fireball or the smoke-and-dust burst, sized by
        /// its scale, round its centre (on the floor when it went off near it).</summary>
        public static void Blast(NativeFxList into,in NativeBlastBurst b,float age,bool low)
        {
            if(into==null||!(age>=0)||age>=BurstLife)return;
            var recipe=Fireball(b.Style,b.Tinted)?FireballRecipe:b.Style==NativeBlastStyle.Quake?QuakeRecipe:DustRecipe;
            float sc=(float)b.Scale;var floor=new Vector3((float)b.X,(float)(b.Near?b.Ground:b.Y),(float)b.Z);float q=(float)b.Quality;
            // In the air the recipe plays round the centre (its offsets are heights above the floor on the ground).
            float lift=b.Near?1:0;uint seed=(uint)b.Seed*2654435761u^0x5eedu;
            for(int e=0;e<recipe.Length;e++)Once(into,recipe[e],e,floor,sc,lift,age,seed,q,low,(float)b.Ground,-1);
        }
        /// <summary>A fireball not made by the blast sim (the Bombadier's charges): the same recipe for a blast of `radius` on the floor at
        /// `at`, at `age`.</summary>
        public static void Fireball(NativeFxList into,Vector3 at,float radius,float age,long seed,float quality,bool low)
        {
            var b=new NativeBlastBurst{Style=NativeBlastStyle.Fire,Near=true,X=at.X,Y=at.Y+.35,Z=at.Z,Ground=at.Y,Radius=Math.Max(.4,radius),Scale=Math.Max(.45,Math.Min(2.2,Math.Max(.4,radius)/3)),Quality=quality,Seed=(int)(seed&NativeBlastPool.SeedMask)};
            Blast(into,b,age,low);
        }
        /// <summary>A melee slam's landing layer at `age` (NativeSlamDust.Life or less), from its floor point.</summary>
        public static void Slam(NativeFxList into,Vector3 at,float age,uint seed,float quality,bool low)
        {
            if(into==null||!(age>=0)||age>1.6f)return;
            for(int e=0;e<SlamRecipe.Length;e++)Once(into,SlamRecipe[e],e,at,1,1,age,seed,quality,low,at.Y,-1);
        }
        /// <summary>A wound's spray at `age` after it (gun hits and kills), from `origin` along `along` (the exit direction).</summary>
        public static void Spray(NativeFxList into,Vector3 origin,Vector3 along,float age,uint seed,bool kill,float quality,bool low)
        {
            if(into==null||!(age>=0)||age>1.2f)return;float len=along.Length();var dir=len>1e-5f?along/len:Vector3.UnitY;float sc=kill?1.25f:1;
            for(int e=0;e<BloodRecipe.Length;e++)Once(into,BloodRecipe[e],e,origin,sc,0,age,seed,quality,low,-1e9f,-1,dir);
        }
        /// <summary>A looping fire (a fire field's flame, a drum, a brazier, a burning body) of flame width `size` m on `at` (its base), at
        /// time `time` on any steady clock; `seed` keeps neighbours apart. large: the large flame (fields, buildings), else the campfire.</summary>
        public static void Fire(NativeFxList into,Vector3 at,float size,float time,uint seed,bool large,float quality,bool low,bool embers=true,bool smoke=true)
        {
            if(into==null||!(size>0)||!float.IsFinite(time))return;var recipe=FireRecipe(large);
            for(int e=0;e<recipe.Length;e++)
            {
                var em=recipe[e];if(!embers&&em.Look==NativeFxLook.Streak||!smoke&&em.Sheet==FlameSmoke)continue;
                Stream(into,em,e,at,size,time,seed,quality,low);
            }
        }

        // A burst emitter: Count sprites born at Delay, each a pure function of its age and index.
        private static void Once(NativeFxList into,Emitter em,int e,Vector3 floor,float sc,float lift,float age,uint seed,float quality,bool low,float ground,int only,Vector3? axis=null)
        {
            int n=Keep(em.Count,quality,low);uint s=seed+(uint)e*7919u;
            for(int i=0;i<n;i++)
            {
                float life=Mix(em.Life0,em.Life1,H(s,i,1)),a=age-em.Delay;if(a<0||a>=life)continue;
                Place(into,em,e,i,s,floor,sc,lift,a,life,ground,axis);
            }
        }
        // A looping stream: particle k is born at k / rate (from the start of the clock), each a pure function of its age.
        private static void Stream(NativeFxList into,Emitter em,int e,Vector3 at,float size,float time,uint seed,float quality,bool low)
        {
            float rate=em.Rate*Math.Max(.25f,Math.Min(1,quality))*(low?.5f:1);if(!(rate>0))return;uint s=seed+(uint)e*7919u;
            long last=(long)Math.Floor(time*rate),first=(long)Math.Floor((time-em.Life1)*rate);
            for(long k=first;k<=last;k++)
            {
                int i=(int)(k&0x7fffffff);float born=(float)(k/(double)rate),life=Mix(em.Life0,em.Life1,H(s,i,1)),a=time-born;if(a<0||a>=life)continue;
                Place(into,em,e,i,s,at,size,1,a,life,-1e9f,null);
            }
        }
        private static void Place(NativeFxList into,Emitter em,int e,int i,uint s,Vector3 origin,float sc,float lift,float a,float life,float ground,Vector3? axis)
        {
            float u=a/life,h2=H(s,i,2),h3=H(s,i,3),h4=H(s,i,4);
            Vector3 d;
            switch(em.Shape)
            {
                case NativeFxShape.Hemisphere:d=Dir(s,i,11);d.Y=Math.Abs(d.Y);break;
                case NativeFxShape.Ring:{float ang=(i+H(s,i,12)*.6f)/Math.Max(1,em.Count)*6.2831853f;d=new Vector3((float)Math.Cos(ang),.05f,(float)Math.Sin(ang));break;}
                case NativeFxShape.Cone:{float ang=H(s,i,13)*6.2831853f,t=em.Angle*.0174533f*(float)Math.Sqrt(H(s,i,14)),st=(float)Math.Sin(t);d=new Vector3((float)Math.Cos(ang)*st,(float)Math.Cos(t),(float)Math.Sin(ang)*st);break;}
                default:d=Dir(s,i,11);break;
            }
            // A cone (blood along the wound) turns its +y onto the given axis.
            if(axis.HasValue&&em.Shape==NativeFxShape.Cone)d=Turn(d,axis.Value);
            float speed=Mix(em.Speed0,em.Speed1,h2)*sc,drag=em.Drag;float travel=drag>0?(1-(float)Math.Exp(-drag*a))/drag:a;
            var start=origin+new Vector3(0,em.OffsetY*sc*lift,0)+d*(em.Radius*sc*(em.Shape==NativeFxShape.Ring?1:(float)Math.Sqrt(H(s,i,15))));
            var v0=d*speed;float g=em.Gravity*9.81f;
            var at=start+v0*travel-new Vector3(0,.5f*g*a*a,0);var vel=v0*(drag>0?(float)Math.Exp(-drag*a):1)-new Vector3(0,g*a,0);
            if(ground>-1e8f&&at.Y<ground+.03f){if(em.Look==NativeFxLook.Streak)return;at.Y=ground+.03f;}
            float opacity=Curve(em.Alpha,u);if(!(opacity>.002f))return;
            float size=Mix(em.Size0,em.Size1,h3)*sc*Curve(em.Grow,u);
            var sp=new NativeFxSprite{Look=em.Look,Sheet=em.Sheet,At=at,Velocity=vel,Size=size,Roll=(h4-.5f)*em.Rot*.0174533f+(H(s,i,5)-.5f)*.35f*u,Opacity=opacity,
                Heat=Curve(em.Heat,u)*em.HeatGain,Gain=em.Gain,Self=em.Self,Occlusion=em.Occlusion,Hex=em.Hex,Tint=em.Tint,Stretch=em.Stretch,U=u,Seed=i+e*1000};
            if(em.Sheet>=0)sp.Frame=em.Sheet==Splatters?BloodTile(e,i):Math.Min(Frames(em.Sheet)-1,u*Frames(em.Sheet));
            // The stretched blood drops lie along their flight (the directional tile).
            if(em.Look==NativeFxLook.Blood&&em.Sheet==Splatters&&BloodTile(e,i)==SplatLine)sp.Segment=vel*em.Stretch;
            into.Add(sp);
        }
        // Rotates d (about +y) so +y lands on axis (unit).
        private static Vector3 Turn(Vector3 d,Vector3 axis)
        {
            var up=Vector3.UnitY;float c=Vector3.Dot(up,axis);if(c>.9999f)return d;if(c<-.9999f)return new Vector3(d.X,-d.Y,-d.Z);
            var k=Vector3.Normalize(Vector3.Cross(up,axis));float s=(float)Math.Sqrt(Math.Max(0,1-c*c));
            return d*c+Vector3.Cross(k,d)*s+k*(Vector3.Dot(k,d)*(1-c));
        }

        // ---- the fire fields and stage fires: where a field's flames stand ----
        /// <summary>How many flames a fire field of radius r burns with (one for a small one, up to seven), and the flames' width.</summary>
        /// <summary>A burning body's flame: the campfire this wide (m), its base this far above the feet.</summary>
        public const float BodyFireSize=1.5f,BodyFireLift=.25f;
        public static int FieldFlames(double r)=>Math.Max(1,Math.Min(7,(int)Math.Round(1+r*r*.45)));
        public static float FieldFlameSize(double r)=>(float)Math.Max(1.8,Math.Min(3,1.2+r*.6));
        /// <summary>Flame j of a field: the centre, then a ring at .55 of its radius, turned by its id.</summary>
        public static Vector3 FieldFlameAt(double x,double ground,double z,double r,long id,int j,int n)
        {
            if(j==0||n<=1)return new Vector3((float)x,(float)ground,(float)z);
            float a=(j-1)/(float)Math.Max(1,n-1)*6.2831853f+H((uint)id,0,77)*6.2831853f,rr=(float)r*.55f*(.85f+.3f*H((uint)id,j,78));
            return new Vector3((float)x+rr*(float)Math.Cos(a),(float)ground,(float)z+rr*(float)Math.Sin(a));
        }
    }

    /// <summary>A fire the effects bank draws for a stage or a body (a camp drum or brazier, a town fire, a burning body): its base,
    /// flame width (m), seed, which flame (the large one for buildings, the campfire otherwise) and whether it smokes.</summary>
    public struct NativePackFire{public float X,Y,Z,Size;public uint Seed;public bool Large,Smoke;}
    /// <summary>A reused list of stage fires (at most <see cref="Max"/> a frame): no allocation once grown.</summary>
    public sealed class NativePackFires
    {
        public const int Max=48;public NativePackFire[] Items=new NativePackFire[16];public int Count;
        public void Clear()=>Count=0;
        public void Add(in NativePackFire f){if(Count>=Max)return;if(Count==Items.Length)Array.Resize(ref Items,Items.Length*2);Items[Count++]=f;}
    }
    /// <summary>A technique sink that can draw the effects bank's fireball (the runtime's, while the bank is loaded): the Bombadier's
    /// blasts ask it first and keep their own fire puffs and smoke only when it says no. Test sinks do not implement it.</summary>
    public interface INativeFxPackSink{bool PackBlast(Vector3 at,float radius,float age,long seed,float quality,bool low);}

    /// <summary>What a pack sprite is drawn as: a fire flipbook (heat ramp, its smoke lit), a lit smoke or dust flipbook, a lit blood
    /// sprite, or one of the code's own HDR glows and streaks (the flash, the embers).</summary>
    public enum NativeFxLook:byte{Fire,Smoke,Blood,Glow,Streak}
    /// <summary>One sprite of a recipe at one moment (Core NativeFxPack): where and how big, its sheet and frame (or splatter tile), how
    /// opaque, its heat (fire), its smoke tint (a packed sRGB hex), its self light and occlusion, and for the code's glows and streaks
    /// their tint, gain and velocity.</summary>
    public struct NativeFxSprite
    {
        public NativeFxLook Look;public int Sheet,Hex,Seed;public Vector3 At,Velocity,Segment,Tint;
        public float Size,Roll,Frame,Opacity,Heat,Gain,Self,Occlusion,Stretch,U;
    }
    /// <summary>A reused list of sprites: no allocation once it has grown.</summary>
    public sealed class NativeFxList
    {
        public NativeFxSprite[] Items=new NativeFxSprite[256];public int Count;
        public void Clear()=>Count=0;
        public void Add(in NativeFxSprite s){if(Count==Items.Length)Array.Resize(ref Items,Items.Length*2);Items[Count++]=s;}
    }
}
