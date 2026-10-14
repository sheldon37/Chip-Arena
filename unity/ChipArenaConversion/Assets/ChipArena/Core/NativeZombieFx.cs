using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The spitter's acid and the heavy kinds' impacts, high-quality pass (owner 2026-10-14: "Improve visual fx from spitter/hulk
    /// zombie (make them high quality) acid needs to look better. Slam/landing/wall break [effects] also needs improvements from
    /// hulk/brute"). Presentation only, engine free and clock free: every shape is a pure function of what every peer holds (the
    /// authority's acid record or spit event, an add's LastAttack and strike, a drop's replicated SpawnAt/ActiveAt, a breach
    /// record), its seed and the presentation clock, so host, guests and the killcam draw the same frame. No allocation.
    /// Runtime/NativeZombieFxDraw.cs draws them through the shared effects renderer (NativeVfxDraw: lit glossy blobs and rubble,
    /// toon puffs, teardrops, glows, the upright slam wave) and a decal layer drawn before the effect billboards.
    /// <list type="bullet">
    /// <item>Sizes and times are the authority's: the glob rides NativeHordeFx.Glob's arc (the server validates that arc; it is
    /// not changed here), the pool's liquid never spreads past NativeSpitterRules.Radius and its ring marks that radius until
    /// NativeSpitterRules.Duration ends, every splash drop lands inside it; an impact's wave, cracks, dust ring and landed debris
    /// stay inside the authority's radius (the smash's NativeHordeKinds.SmashRadiusOf disc, the drop's DropProfile.SlamRadius).</item>
    /// <item>Tiers (<see cref="Tier"/>): Reduced effects and the low graphics tier halve every stream (<see cref="Count"/>) and
    /// drop the extras (<see cref="Extras"/>: sparks, glints, fumes trailing the glob, the membrane); the renderer's low tier also
    /// thins the toon puffs. <see cref="Budget"/> bounds what one effect can queue on each tier.</item>
    /// </list>
    /// </summary>
    public static class NativeZombieFx
    {
        private static float Sm(float x)=>MeleeMath.Smooth(x);
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float H(uint seed,int i,int k)=>NativeHordeFx.Hash(seed,i,k);
        private const float Tau=(float)(Math.PI*2),G=9.8f;

        // ================================================================================================================
        // Tiers and budgets
        // ================================================================================================================
        public const int Full=0,Reduced=1,Low=2;
        /// <summary>The effects tier: the low graphics tier, else Reduced effects (quality under .5), else full.</summary>
        public static int Tier(bool lowGraphics,float quality)=>lowGraphics?Low:quality<.5f?Reduced:Full;
        /// <summary>How many of a stream of `n` a tier draws: all on full, half (at least one) on the others.</summary>
        public static int Count(int n,int tier)=>n<=0?0:tier==Full?n:Math.Max(1,(n+1)/2);
        /// <summary>Whether a tier draws the extras (sparks, glints, the glob's fume trail and membrane, spatter sizzles).</summary>
        public static bool Extras(int tier)=>tier==Full;
        /// <summary>What one effect may queue on a tier (billboards plus lit pieces plus decal quads), its budget: the acid glob in
        /// flight, a landing splash with its pool, a pilot's splash, an impact of each profile, a breach's burst.</summary>
        public enum Effect{Glob,Pool,Body,Impact,HeavyImpact,Breach}
        public static int Budget(Effect e,int tier)
        {
            int full=e==Effect.Glob?40:e==Effect.Pool?130:e==Effect.Body?40:e==Effect.Impact?160:e==Effect.HeavyImpact?240:150;
            return tier==Full?full:(full*3+4)/5;
        }

        // ================================================================================================================
        // The colours (sRGB hexes; the renderer converts them): acid, its deep core, its toon shadow, fumes, the spent etch.
        // ================================================================================================================
        public const int AcidHex=0x8dff4f,AcidHotHex=0xd8ff7a,AcidCoreHex=0x1f4a08,AcidShadeHex=0x2a5c10,AcidFumeHex=0x9ad65a,EtchHex=0x1c1a0c,DustHex=0xaaa09c,DustDarkHex=0x6c6370;

        // ================================================================================================================
        // The glob in flight (NativeHordeFx.Glob's arc: the authority's, unchanged)
        // ================================================================================================================
        /// <summary>The glob's core radius (m): a glossy viscous clot; the membrane round it is <see cref="MembraneScale"/> times wider.</summary>
        public const float GlobRadius=.085f,MembraneScale=2.6f;
        /// <summary>The glob's velocity on its arc `since` s after launch (m/s, the arc's derivative).</summary>
        public static Vector3 GlobVelocity(Vector3 from,Vector3 to,float flight,float since)
        {
            if(!(flight>0))return Vector3.Zero;float u=Clamp01(since/flight),apex=.9f+.08f*Vector2.Distance(new Vector2(from.X,from.Z),new Vector2(to.X,to.Z));
            var v=(to-from)/flight;v.Y+=4*apex*(1-2*u)/flight;return v;
        }
        /// <summary>The glob's viscous shape `since` s after launch at `speed` m/s: its stretch along the flight and squash across
        /// (volume kept: along x across² = 1), a wobble ringing out from the throw.</summary>
        public static void GlobShape(uint seed,float since,float speed,out float along,out float across)
        {
            float stretch=Math.Min(.5f,Math.Max(0,speed)*.042f),ring=(float)(Math.Sin(since*29+H(seed,0,1)*Tau)*.17*Math.Exp(-Math.Max(0,since)*2.4)),drift=.05f*(float)Math.Sin(since*13.5f+H(seed,0,2)*Tau);
            along=Math.Max(.7f,1+stretch+ring+drift);across=1/(float)Math.Sqrt(along);
        }
        public const int TailBeads=5;
        /// <summary>Bead k of the stringy tail the glob drags: where the glob was a moment ago on its arc and its radius share (the
        /// tail thins to a thread); false before it has left the mouth or once the glob has landed.</summary>
        public static bool TailBead(Vector3 from,Vector3 to,float flight,float since,int k,out Vector3 at,out float share)
        {
            at=from;share=0;float lag=.016f*(k+1)+.003f*k*k,back=since-lag;if(back<0||since>flight||!(flight>0))return false;
            NativeHordeFx.Glob(from,to,flight,back,out at);share=.62f*(1-(k+.4f)/(TailBeads+.6f));return true;
        }
        public const int Drips=7;
        /// <summary>Drip i shaken off the glob: it leaves the arc at its own moment with a little of the glob's speed and falls under
        /// gravity; its position and velocity, false before it leaves or once it reaches the landing's height.</summary>
        public static bool Drip(Vector3 from,Vector3 to,float flight,uint seed,int i,float since,out Vector3 at,out Vector3 velocity)
        {
            at=from;velocity=Vector3.Zero;if(!(flight>0))return false;float leave=flight*(.08f+.78f*(i+H(seed,i,111))/Drips),t=since-leave;if(t<0)return false;
            NativeHordeFx.Glob(from,to,flight,leave,out var p);var v0=GlobVelocity(from,to,flight,leave)*.3f;
            at=p+v0*t;at.Y-=.5f*G*t*t;velocity=v0;velocity.Y-=G*t;if(at.Y<Math.Min(from.Y,to.Y)-.02f)return false;return true;
        }
        /// <summary>The acid fumes the glob sheds: one born every 1/<see cref="FumeRate"/> s along the arc while it flies, living
        /// <see cref="FumeLife"/> s, rising and swelling; fume `k` of that stream at `since`: where, its size, opacity and age share.</summary>
        public const float FumeRate=24,FumeLife=.42f;
        public static bool GlobFume(Vector3 from,Vector3 to,float flight,uint seed,long k,float since,out Vector3 at,out float size,out float alpha,out float u)
        {
            at=from;size=alpha=u=0;float born=k/FumeRate,a=since-born;if(k<0||born>flight||a<0||a>=FumeLife)return false;u=a/FumeLife;int i=(int)(k&0xffff);
            NativeHordeFx.Glob(from,to,flight,born,out at);at+=new Vector3((H(seed,i,121)-.5f)*.08f,.05f+.32f*a+.04f*H(seed,i,122),(H(seed,i,123)-.5f)*.08f);
            size=(.07f+.06f*H(seed,i,124))*(.6f+1.4f*u);alpha=.55f*Sm(u/.12f)*(1-u);return true;
        }

        // ================================================================================================================
        // The wind-up: acid gathering in the mouth, strings across the unhinging jaw
        // ================================================================================================================
        /// <summary>The clot gathering in the mouth through the wind-up (`age` from LastAttack, released at `strike`): its radius (m), how
        /// far it bulges out past the lips (m, sagging) and its glow (0..1); false outside the wind-up.</summary>
        public static bool MouthGlob(uint seed,float age,float strike,out float radius,out float bulge,out float glow)
        {
            radius=bulge=glow=0;if(float.IsNaN(age)||age<.06f||age>=strike||!(strike>.1f))return false;float u=(age-.06f)/(strike-.06f);
            radius=.016f+.05f*Sm(u)+.004f*(float)Math.Sin(age*21+H(seed,0,131)*Tau);bulge=.012f+.034f*Sm(u);glow=.25f+.75f*u*u;return true;
        }
        public const int MouthStrings=3;
        /// <summary>String k of acid stretched across the jaw as it unhinges (`jaw` 0 shut .. 1 wide): its upper and lower ends in the
        /// head's frame (x across, y up, z out of the mouth; m from the mouth socket), its sag at the middle (m) and its width (m);
        /// false once it has snapped (they snap one by one as the jaw opens wide) or outside the wind-up.</summary>
        public static bool MouthString(uint seed,int k,float age,float strike,float jaw,out Vector3 top,out Vector3 bottom,out float sag,out float width)
        {
            top=bottom=Vector3.Zero;sag=width=0;if(float.IsNaN(age)||age<.12f||age>=strike+.04f)return false;
            float snap=.62f+.3f*H(seed,k,132);if(jaw>snap)return false;float x=(k-(MouthStrings-1)*.5f)*.05f+(H(seed,k,133)-.5f)*.01f;
            top=new Vector3(x,.018f,.02f);bottom=new Vector3(x*.8f,-.016f-.07f*jaw,.026f);sag=.01f+.03f*jaw*jaw;width=.007f*(1-.55f*jaw/Math.Max(.1f,snap));return true;
        }

        // ================================================================================================================
        // The landing: a crown of acid, drops thrown out (each lands inside the pool's radius and leaves a spatter mark)
        // ================================================================================================================
        public const int SplashDrops=14,CrownLobes=10;public const float CrownSeconds=.32f;
        /// <summary>The flash where the glob lands: its size (m) and strength; false after .14 s.</summary>
        public static bool SplashFlash(float since,out float size,out float alpha)
        {size=alpha=0;if(since<0||since>.14f)return false;float u=since/.14f;size=.45f+.65f*Sm(u*1.6f);alpha=1-u*u;return true;}
        /// <summary>Lobe k of the liquid crown thrown up round the landing: its offset (m), size (m) and opacity.</summary>
        public static bool Crown(uint seed,int k,float since,out Vector3 offset,out float size,out float alpha)
        {
            offset=Vector3.Zero;size=alpha=0;if(since<0||since>CrownSeconds)return false;float u=since/CrownSeconds,a=Tau*(k+.4f*H(seed,k,141))/CrownLobes;
            float r=.1f+.34f*(1-(1-u)*(1-u)),y=.02f+.26f*(float)Math.Sin(Math.PI*Math.Min(1,u*1.12f))*(.7f+.3f*H(seed,k,142));
            offset=new Vector3((float)Math.Cos(a)*r,y,(float)Math.Sin(a)*r);size=(.08f+.05f*H(seed,k,143))*(1-.45f*u);alpha=.95f*(1-Sm((u-.55f)/.45f));return true;
        }
        // A drop's throw: its azimuth, elevation and the range it lands at (a share of the radius, inside it).
        private static void Throw(uint seed,int i,float radius,out float az,out float elev,out float range)
        {az=Tau*(i+.55f*H(seed,i,151))/SplashDrops;elev=.82f+.42f*H(seed,i,152);range=Math.Max(.05f,radius)*(.28f+.62f*H(seed,i,153));}
        /// <summary>When splash drop i lands (s after the glob).</summary>
        public static float SplashLand(uint seed,int i,float radius){Throw(seed,i,radius,out _,out float e,out float r);float v=(float)Math.Sqrt(r*G/Math.Sin(2*e));return 2*v*(float)Math.Sin(e)/G;}
        /// <summary>Splash drop i in flight: its offset from the landing point (m), velocity, size (m); false once it has landed (its
        /// spatter mark takes over, <see cref="SpatterAt"/>).</summary>
        public static bool SplashDrop(uint seed,int i,float since,float radius,out Vector3 offset,out Vector3 velocity,out float size)
        {
            offset=velocity=Vector3.Zero;size=0;if(since<0)return false;Throw(seed,i,radius,out float az,out float e,out float r);
            float v=(float)Math.Sqrt(r*G/Math.Sin(2*e)),vx=v*(float)Math.Cos(e),vy=v*(float)Math.Sin(e);if(since>2*vy/G)return false;
            var d=new Vector3((float)Math.Cos(az),0,(float)Math.Sin(az));offset=d*(vx*since)+new Vector3(0,vy*since-.5f*G*since*since+.02f,0);velocity=d*vx+new Vector3(0,vy-G*since,0);
            size=.022f+.022f*H(seed,i,154);return true;
        }
        /// <summary>Where splash drop i landed (x, z from the landing point, m): always inside `radius`.</summary>
        public static Vector2 SpatterAt(uint seed,int i,float radius){Throw(seed,i,radius,out float az,out _,out float r);return new Vector2((float)Math.Cos(az)*r,(float)Math.Sin(az)*r);}
        /// <summary>The size of drop i's spatter mark (m) and a turn for its blot (rad).</summary>
        public static float SpatterSize(uint seed,int i,out float turn){turn=H(seed,i,155)*Tau;return .05f+.07f*H(seed,i,156);}

        // ================================================================================================================
        // The pool: a lobed liquid film that spreads to the authority's radius (never past it), bubbles, fumes, glints, and
        // evaporates over its last seconds; a faint ring holds the radius until the pool ends; an etched mark stays a while.
        // ================================================================================================================
        public const int Edge=28,PoolBubbles=9,PoolFumes=8,Glints=3;
        public const float SpreadSeconds=.32f,EvaporateSeconds=1.3f,EtchSeconds=5;
        /// <summary>The pool `since` s after the landing, lasting `duration`: how far its liquid has spread (a share of the authority's
        /// radius) and how wet it still is (1 .. 0 as it evaporates over its last <see cref="EvaporateSeconds"/>); false outside it.</summary>
        public static bool Pool(float since,float duration,out float spread,out float wet)
        {
            spread=wet=0;if(since<0||since>duration||!(duration>0))return false;float evap=Math.Min(EvaporateSeconds,duration*.4f);
            wet=1-Sm((since-(duration-evap))/evap);spread=(.3f+.7f*Sm(since/SpreadSeconds))*(.97f+.03f*Sm(since/Math.Max(.4f,duration*.5f)));return true;
        }
        /// <summary>The liquid's edge at vertex k of <see cref="Edge"/> (counter-clockwise from +x): its reach as a share of the
        /// authority's radius, at most `spread` (so never past the radius): slow lobes that creep and wobble, retreating unevenly
        /// as it dries.</summary>
        public static float EdgeAt(uint seed,int k,float since,float spread,float wet)
        {
            float a=Tau*k/Edge,p1=H(seed,1,161)*Tau,p2=H(seed,2,161)*Tau,p3=H(seed,3,161)*Tau,t=Math.Max(0,since);
            float lobe=.5f+.5f*(.55f*(float)Math.Sin(3*a+p1+.55f*t)+.3f*(float)Math.Sin(5*a+p2-.8f*t)+.15f*(float)Math.Sin(9*a+p3+1.6f*t));
            float dry=.5f+.5f*(float)Math.Sin(2*a+p2*1.7f+.3f);float shrink=(1-wet)*(.25f+.55f*dry);
            return Clamp01(spread*(1-.13f*lobe)*(1-shrink));
        }
        /// <summary>The faint ring that marks the authority's radius: on within .2 s of the landing, held, out over the last .3 s.</summary>
        public static float RimAlpha(float since,float duration)=>since<0||since>duration?0:Sm(since/.2f)*(1-Sm((since-(duration-.3f))/.3f));
        /// <summary>Bubble i on the pool: its offset from the centre (m), size (m) and pop (0 swelling .. 1 burst); false while it rests.</summary>
        public static bool PoolBubble(uint seed,int i,float since,float duration,float radius,out Vector3 offset,out float size,out float pop)
        {
            offset=Vector3.Zero;size=pop=0;if(!Pool(since,duration,out float spread,out float wet)||since<.18f||wet<.15f)return false;
            float period=.55f+.7f*H(seed,i,171),u=((since+H(seed,i,172)*period)%period)/period,a=Tau*H(seed,i,173),r=radius*spread*.78f*(float)Math.Sqrt(H(seed,i,174));
            float big=(.03f+.04f*H(seed,i,175))*(.5f+.5f*wet);if(u<.8f){size=big*(.3f+.7f*Sm(u/.8f));pop=0;}else{pop=(u-.8f)/.2f;size=big*(1+.9f*pop);}
            offset=new Vector3((float)Math.Cos(a)*r,u<.8f?size*.35f:.006f,(float)Math.Sin(a)*r);return true;
        }
        /// <summary>Fume i rising off the pool (thicker while it evaporates, clearing after it ends): offset (m), size (m), opacity and
        /// its age share (the toon puff's erosion).</summary>
        public static bool PoolFume(uint seed,int i,float since,float duration,float radius,out Vector3 offset,out float size,out float alpha,out float u)
        {
            offset=Vector3.Zero;size=alpha=u=0;if(since<.1f||since>duration+.7f)return false;Pool(Math.Min(since,duration),duration,out float spread,out float wet);
            float period=1f+.6f*H(seed,i,181);u=((since+H(seed,i,182)*period)%period)/period;float a=Tau*H(seed,i,183),r=radius*Math.Max(.2f,spread)*.62f*(float)Math.Sqrt(H(seed,i,184)),drift=.22f*u;
            offset=new Vector3((float)Math.Cos(a)*r+drift*(H(seed,i,185)-.3f),.06f+.85f*u*(.8f+.4f*H(seed,i,186)),(float)Math.Sin(a)*r+drift*(H(seed,i,187)-.5f));
            float scale=Math.Min(1.3f,radius/1.3f);size=(.24f+.46f*u)*scale;float activity=(.6f+.55f*(1-wet))*(1-Sm((since-duration)/.7f));alpha=Math.Min(.6f,.5f*Sm(u/.15f)*(1-u)*activity);return alpha>.005f;
        }
        /// <summary>A glint sliding over the wet film: offset (m) and strength.</summary>
        public static bool Glint(uint seed,int i,float since,float duration,float radius,out Vector3 offset,out float alpha)
        {
            offset=Vector3.Zero;alpha=0;if(!Pool(since,duration,out float spread,out float wet))return false;float a=Tau*H(seed,i,191)+.35f*since*(H(seed,i,192)<.5f?1:-1),r=radius*spread*(.25f+.45f*H(seed,i,193));
            offset=new Vector3((float)Math.Cos(a)*r,.012f,(float)Math.Sin(a)*r);alpha=wet*Sm(since/.25f)*(.55f+.45f*(float)Math.Sin(since*(2.2f+H(seed,i,194))+i*2.1f));return alpha>.01f;
        }
        /// <summary>The etched mark the acid leaves on the floor: its reach (a share of the radius, the liquid's shape where it began
        /// to dry, inside it) and darkness; it shows as the liquid dries and fades over <see cref="EtchSeconds"/> after the pool.</summary>
        public static bool Etch(float since,float duration,out float alpha)
        {alpha=0;if(since<.3f||since>duration+EtchSeconds)return false;alpha=.5f*Sm((since-.3f)/1.2f)*(1-Sm((since-duration)/EtchSeconds));return alpha>.005f;}
        /// <summary>The etch's edge at vertex k: the liquid's edge as it began to dry, a little inside it.</summary>
        public static float EtchEdge(uint seed,int k,float duration){float at=Math.Max(SpreadSeconds,duration-EvaporateSeconds);Pool(at,duration,out float s,out _);return EdgeAt(seed,k,at,s,1)*.9f;}

        // ================================================================================================================
        // Corrosion on a block's face, and on a splashed pilot
        // ================================================================================================================
        /// <summary>The blotch eaten round the acid's contact on a face: its reach (m, growing), its rim's heat (1 fresh acid .. 0
        /// spent) and its darkness, through `duration` and a fade.</summary>
        public static bool Blotch(float since,float duration,float spread,out float reach,out float heat,out float alpha)
        {
            reach=heat=alpha=0;if(since<0||since>duration+NativeHordeFx.CorrodeFade)return false;reach=spread*(.25f+.75f*Sm(since/.6f));
            heat=1-Sm(since/Math.Max(.5f,duration));alpha=.8f*Sm(since/.25f)*(1-Sm((since-duration)/NativeHordeFx.CorrodeFade));return true;
        }
        /// <summary>The blotch's ragged edge at vertex k of <see cref="Edge"/> (a share of its reach).</summary>
        public static float BlotchEdge(uint seed,int k){float a=Tau*k/Edge;return .72f+.16f*(float)Math.Sin(3*a+H(seed,1,201)*Tau)+.08f*(float)Math.Sin(7*a+H(seed,2,201)*Tau)+.04f*H(seed,k,202);}
        /// <summary>Drip i running down a corroded face: its face-local start (x across, m), how far it has run (m, accelerating as it
        /// thins), its speed (m/s) and opacity.</summary>
        public static bool FaceDrip(uint seed,int i,float since,float duration,float spread,out float x,out float run,out float speed,out float alpha)
        {
            x=run=speed=alpha=0;float start=.12f+.45f*H(seed,i,211),t=since-start;if(t<0||since>duration+NativeHordeFx.CorrodeFade)return false;
            float acc=.22f+.25f*H(seed,i,212),most=.45f+.35f*H(seed,i,213);run=Math.Min(most,.5f*acc*t*t);speed=run<most?acc*t:0;x=(H(seed,i,214)-.5f)*spread*1.5f;
            alpha=.85f*(1-Sm((since-duration)/NativeHordeFx.CorrodeFade));return true;
        }
        public const int BodyDrops=8,BodyDrips=5;
        /// <summary>Drop i thrown off a splashed pilot's body: offset from the chest (m), velocity, size; false once it has fallen past
        /// the feet (`drop` m below the chest).</summary>
        public static bool BodyDrop(uint seed,int i,float since,float drop,out Vector3 offset,out Vector3 velocity,out float size)
        {
            offset=velocity=Vector3.Zero;size=0;if(since<0)return false;float a=Tau*(i+.5f*H(seed,i,221))/BodyDrops,out_=1.2f+1.4f*H(seed,i,222),up=.8f+1.6f*H(seed,i,223);
            var d=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));offset=d*(.18f+out_*since)+new Vector3(0,up*since-.5f*G*since*since,0);if(offset.Y<-drop)return false;
            velocity=d*out_+new Vector3(0,up-G*since,0);size=.02f+.018f*H(seed,i,224);return true;
        }
        /// <summary>Drip i running down a splashed pilot: its height below its start on the body (m, slow at first, then running),
        /// its round offset (rad about the body) and opacity, through `seconds`.</summary>
        public static bool BodyDrip(uint seed,int i,float since,float seconds,out float down,out float angle,out float alpha)
        {
            down=angle=alpha=0;float start=.08f+.5f*H(seed,i,231),t=since-start;if(t<0||since>seconds)return false;down=Math.Min(.9f,.12f*t+.35f*t*t*(.6f+.6f*H(seed,i,232)));
            angle=Tau*H(seed,i,233);alpha=.9f*(1-Sm((since-(seconds-.5f))/.5f));return true;
        }

        // ================================================================================================================
        // Impacts: the smashes, the jump-in landings, a Hulk's death crash
        // ================================================================================================================
        /// <summary>One impact's look (its radius is the authority's): power, the slam wave's length (s) and height (m), its cracks
        /// (count, how long they stay), its floor-hugging dust (count, life), its debris (count, life), sparks, a second wave, a crater.</summary>
        public readonly struct Impact
        {
            public readonly float Radius,Power,WaveSeconds,WaveHeight,CrackStay,DustLife,DebrisLife;public readonly int Cracks,Dust,Debris,Sparks,Column;public readonly bool Second,Crater,Heavy;
            public Impact(float radius,float power,float wave,float height,int cracks,float stay,int dust,float dustLife,int debris,float debrisLife,int sparks,int column,bool second,bool crater,bool heavy)
            {Radius=radius;Power=power;WaveSeconds=wave;WaveHeight=height;Cracks=cracks;CrackStay=stay;Dust=dust;DustLife=dustLife;Debris=debris;DebrisLife=debrisLife;Sparks=sparks;Column=column;Second=second;Crater=crater;Heavy=heavy;}
            public Impact WithRadius(float radius)=>new Impact(radius,Power,WaveSeconds,WaveHeight,Cracks,CrackStay,Dust,DustLife,Debris,DebrisLife,Sparks,Column,Second,Crater,Heavy);
        }
        /// <summary>How long an impact's marks last (its cracks, debris and dust, whichever is longest), s.</summary>
        public static float Life(in Impact p)=>Math.Max(p.CrackStay,Math.Max(p.DebrisLife,p.DustLife*1.1f));
        /// <summary>A smash on the kind's disc (NativeHordeKinds.SmashRadiusOf: the brute's 1.6 m, the Hulk's 2.8 m).</summary>
        public static Impact Smash(int kind)=>kind==NativeHordeKinds.Hulk
            ?new Impact(NativeHordeKinds.SmashRadiusOf(kind),2,.7f,1.0f,12,2.6f,20,1.9f,16,2.4f,10,6,true,false,true)
            :new Impact(NativeHordeKinds.SmashRadiusOf(kind),1,.45f,.5f,7,1.8f,12,1.25f,10,1.8f,6,3,false,false,false);
        /// <summary>A jump-in landing on its profile's slam radius (DropProfile.SlamRadius: the brute's 2.4 m, the Hulk's 5.5 m).</summary>
        public static Impact Landing(in NativeHordeDrop.DropProfile p)=>p.Heavy
            ?new Impact(p.SlamRadius,3,.8f,1.4f,12,NativeHordeFx.CraterSeconds,24,2.4f,18,4f,14,8,true,true,true)
            :new Impact(p.SlamRadius,1.4f,.55f,.65f,8,1.8f,14,1.5f,12,2f,6,4,false,false,false);
        /// <summary>A Hulk's body crashing down (its death): a short heavy thud.</summary>
        public static readonly Impact Crash=new Impact(2.6f,1.3f,.5f,.6f,8,4f,14,1.6f,8,2.2f,0,4,false,false,false);
        /// <summary>The flash at the strike: size (m) and strength; false after .16 s.</summary>
        public static bool ImpactFlash(float since,float power,out float size,out float alpha)
        {size=alpha=0;if(since<0||since>.16f)return false;float u=since/.16f;size=(.8f+.55f*power)*(.5f+.5f*Sm(u*2));alpha=(1-u)*(1-u);return true;}
        /// <summary>The upright slam wave: its radius (m; reaching the authority's radius as it ends, never past it), height (m),
        /// progress (0..1) and fade; the heavy kinds' second, smaller wave follows .16 s later.</summary>
        public static bool Wave(in Impact p,int which,float since,out float radius,out float height,out float progress,out float fade)
        {
            radius=height=progress=fade=0;float t=which==0?since:since-.16f,len=which==0?p.WaveSeconds:p.WaveSeconds*.8f,R=which==0?p.Radius:p.Radius*.72f;if(which!=0&&!p.Second)return false;
            if(t<0||t>len)return false;progress=t/len;radius=R*(.1f+.9f*(1-(1-progress)*(1-progress)*(1-progress)));height=p.WaveHeight*(which==0?1:.7f)*(.6f+.4f*progress);fade=(1-progress)*(float)Math.Sqrt(1-progress);return true;
        }
        public const int CrackSegments=4,Branches=2;
        /// <summary>Point j (0..<see cref="CrackSegments"/>) of main crack i of `count` run out of an impact: its offset on the floor
        /// (x, z from the centre, m; jagged, inside the radius) and the crack's width there (m); the cracks race out in .14 s.</summary>
        public static Vector2 CrackPoint(uint seed,int i,int count,int j,float since,in Impact p,out float width)
        {
            float ang=Tau*(i+.6f*H(seed,i,241))/Math.Max(1,count),len=p.Radius*(.55f+.43f*H(seed,i,242))*Sm(Math.Max(0,since)/.14f),s=j/(float)CrackSegments;
            var dir=new Vector2((float)Math.Cos(ang),(float)Math.Sin(ang));var side=new Vector2(-dir.Y,dir.X);float jag=j==0||j==CrackSegments?0:(H(seed,i*8+j,243)-.5f)*.34f*len/CrackSegments;
            width=(.035f+.035f*(float)Math.Sqrt(p.Power))*(1-.8f*s)*(.7f+.3f*H(seed,i,244));var at=dir*(.06f*p.Radius+len*.94f*s)+side*jag;
            float most=p.Radius*.985f;if(at.Length()>most)at*=most/at.Length();return at;
        }
        /// <summary>Branch b of main crack i: it forks from the crack's point 1+b at an angle and runs a third of the crack's length.</summary>
        public static void CrackBranch(uint seed,int i,int count,int b,float since,in Impact p,out Vector2 a,out Vector2 c,out float width)
        {
            a=CrackPoint(seed,i,count,1+b,since,p,out width);var root=CrackPoint(seed,i,count,0,since,p,out _);var d=a-root;float l=d.Length();var dir=l>1e-5f?d/l:new Vector2(1,0);
            float turn=(H(seed,i*4+b,245)<.5f?-1:1)*(.45f+.45f*H(seed,i*4+b,246)),cs=(float)Math.Cos(turn),sn=(float)Math.Sin(turn);var bd=new Vector2(dir.X*cs-dir.Y*sn,dir.X*sn+dir.Y*cs);
            c=a+bd*(l*(.3f+.25f*H(seed,i*4+b,247)));float most=p.Radius*.985f;if(c.Length()>most)c*=most/c.Length();width*=.6f;
        }
        /// <summary>The cracks' darkness `since` s after the strike (they stay `stay` s) and the hot seam in them (the ink's glow, out by .7 s).</summary>
        public static float CrackAlpha(float since,float stay)=>since<0||since>stay?0:.92f*(1-Sm((since-stay*.6f)/(stay*.4f)));
        public static float CrackGlow(float since)=>since<0?0:1-Sm(since/.7f);
        /// <summary>The crushed patch at the centre: its reach (m) and darkness (it stays with the cracks).</summary>
        public static bool Crush(float since,in Impact p,out float reach,out float alpha)
        {reach=alpha=0;if(since<0||since>p.CrackStay)return false;reach=p.Radius*(p.Heavy?.2f:.24f)*(.6f+.4f*Sm(since/.06f));alpha=.75f*(1-Sm((since-p.CrackStay*.6f)/(p.CrackStay*.4f)));return true;}
        /// <summary>Dust puff i of `count` thrown out along the floor: its offset from the centre (m; rolling out to the radius, never
        /// past it, low over the floor), size (m), opacity and age share.</summary>
        public static bool HugDust(uint seed,int i,int count,float since,in Impact p,out Vector3 offset,out float size,out float alpha,out float u)
        {
            offset=Vector3.Zero;size=alpha=u=0;if(since<0||since>p.DustLife)return false;u=since/p.DustLife;float a=Tau*(i+.5f*H(seed,i,251))/Math.Max(1,count);
            float k=3.4f+1.4f*H(seed,i,252),roll=(1-(float)Math.Exp(-since*k))/(1-(float)Math.Exp(-p.DustLife*k));
            float scale=Math.Min(1.6f,(float)Math.Sqrt(p.Radius/1.6f));size=(.55f+1.25f*Sm(Math.Min(1,since/(p.DustLife*.6f))))*(.65f+.35f*H(seed,i,254))*scale*(.8f+.2f*p.Power);
            // Its dense middle stays inside the radius (the puff's centre plus .4 of its size).
            float r=Math.Max(0,p.Radius-size*.4f)*(.2f+.8f*roll)*(.9f+.1f*H(seed,i,253));
            offset=new Vector3((float)Math.Cos(a)*r,.05f+size*.32f+.2f*u*u*scale,(float)Math.Sin(a)*r);alpha=.92f*Sm(since/.05f)*(1-Sm((u-.45f)/.55f));return true;
        }
        /// <summary>Puff i of the dust column rising out of the strike: offset (m), size (m), opacity, age share.</summary>
        public static bool ColumnDust(uint seed,int i,float since,in Impact p,out Vector3 offset,out float size,out float alpha,out float u)
        {
            offset=Vector3.Zero;size=alpha=u=0;float life=p.DustLife*1.1f;if(since<0||since>life)return false;u=since/life;float a=Tau*H(seed,i,261),r=p.Radius*.12f*H(seed,i,262);
            float scale=(float)Math.Sqrt(p.Radius/1.6f);offset=new Vector3((float)Math.Cos(a)*r,.2f+(1.1f+.6f*H(seed,i,263))*scale*(1-(float)Math.Exp(-since*2.2f)),(float)Math.Sin(a)*r);
            size=(.7f+1.2f*u)*scale*(.7f+.3f*H(seed,i,264));alpha=.85f*Sm(since/.08f)*(1-Sm((u-.35f)/.65f));return true;
        }
        /// <summary>
        /// Chunk i of `count` thrown by an impact: ballistic under gravity from near the centre, bouncing twice (losing most of its
        /// speed) and sliding to rest inside the radius; its offset from the centre (m, its centre's height over the floor), its turn
        /// (axis, angle) and size (m), and how much is left (1 .. 0 as it sinks away at the end of `p.DebrisLife`).
        /// </summary>
        public static bool Chunk(uint seed,int i,int count,float since,in Impact p,out Vector3 offset,out Vector3 axis,out float angle,out float size,out float left)
        {
            offset=Vector3.Zero;axis=Vector3.UnitY;angle=0;size=left=0;if(since<0||since>p.DebrisLife)return false;
            float az=Tau*(i+.7f*H(seed,i,271))/Math.Max(1,count),e=.6f+.45f*H(seed,i,272),r0=p.Radius*.1f*H(seed,i,273);
            size=(.06f+.08f*H(seed,i,274))*(float)Math.Sqrt(p.Power)*(p.Heavy?1.35f:1);
            // Ranges: the first flight, then two bounces (.35 of the vertical, .55 of the horizontal speed each) and a slide; all
            // scaled so it comes to rest inside .92 of the radius.
            const float bounce=.35f,keep=.55f,slide=.05f;float r1=p.Radius*(.22f+.5f*H(seed,i,275));float span=r1*(1+keep*bounce+keep*keep*bounce*bounce+slide);
            float most=p.Radius*.92f-r0;if(span>most)r1*=most/span;
            float v=(float)Math.Sqrt(r1*G/Math.Sin(2*e)),vx=v*(float)Math.Cos(e),vy=v*(float)Math.Sin(e),lift=size*.5f;
            float t=since,x=r0,y=0,spinScale=1;
            for(int leg=0;leg<3;leg++)
            {
                float T=2*vy/G;if(t<T){x+=vx*t;y=vy*t-.5f*G*t*t;t=-1;break;}
                x+=vx*T;t-=T;vy*=bounce;vx*=keep;spinScale*=.5f;
            }
            if(t>=0)x+=r1*slide*(1-(float)Math.Exp(-t*8));
            var d=new Vector3((float)Math.Cos(az),0,(float)Math.Sin(az));offset=d*x+new Vector3(0,y+lift,0);
            axis=Vector3.Normalize(new Vector3(-d.Z,.4f+H(seed,i,276),d.X));float w=(6+9*H(seed,i,277))*(H(seed,i,278)<.5f?-1:1);angle=w*Math.Min(since,.9f)+w*.15f*Math.Min(Math.Max(0,since-.9f),.6f);
            left=1-Sm((since-(p.DebrisLife-.5f))/.5f);return left>0;
        }
        /// <summary>Spark i of an impact: offset (m), velocity and heat (1 white-hot .. 0 out); low, fast, falling.</summary>
        public static bool Spark(uint seed,int i,float since,in Impact p,out Vector3 offset,out Vector3 velocity,out float heat)
        {
            offset=velocity=Vector3.Zero;heat=0;float life=.28f+.18f*H(seed,i,281);if(since<0||since>life)return false;
            float az=Tau*H(seed,i,282),e=.15f+.55f*H(seed,i,283),v=(4.5f+3.5f*H(seed,i,284))*(.8f+.2f*(float)Math.Sqrt(p.Power));var d=new Vector3((float)(Math.Cos(az)*Math.Cos(e)),(float)Math.Sin(e),(float)(Math.Sin(az)*Math.Cos(e)));
            offset=d*(v*since)+new Vector3(0,.08f-.5f*G*since*since,0);if(offset.Y<.01f)offset.Y=.01f;velocity=d*v+new Vector3(0,-G*since,0);heat=1-since/life;return true;
        }

        // ================================================================================================================
        // The wall breach: chips off each blow, the burst's grit and sparks, the dust curtain, the hardlight still tracing the gap
        // ================================================================================================================
        public const int SpallChips=5,BreakChips=14,BreakSparks=12,CurtainCount=18,Remnants=10,Motes=2;public const float CurtainSeconds=2.4f;
        /// <summary>Chip i knocked off the struck face by a blow `since` s ago: its offset in the wall's frame from the blow point (x
        /// along, y up, z out of the struck face, m), its turn about the wall's up (rad), size (m) and what is left; it falls to the
        /// floor `drop` m below the blow and rests.</summary>
        public static bool Spall(uint seed,int blow,int i,float since,float drop,out Vector3 local,out float spin,out float size,out float left)
        {
            local=Vector3.Zero;spin=size=left=0;if(since<0||since>1.6f)return false;int k=blow*SpallChips+i;
            float vx=(H(seed,k,291)-.5f)*1.6f,vz=.9f+1.3f*H(seed,k,292),vy=.4f+1.4f*H(seed,k,293);size=.035f+.045f*H(seed,k,294);
            float land=(vy+(float)Math.Sqrt(vy*vy+2*G*Math.Max(0,drop-size*.5f)))/G,t=Math.Min(since,land),slide=Math.Max(0,since-land);
            local=new Vector3(vx*t+vx*.15f*Math.Min(slide,.4f),vy*t-.5f*G*t*t,vz*t+vz*.15f*Math.Min(slide,.4f));if(local.Y<-(drop-size*.5f))local.Y=-(drop-size*.5f);
            spin=(8+8*H(seed,k,295))*Math.Min(since,land);left=1-Sm((since-1.2f)/.4f);return true;
        }
        /// <summary>Grit chip i burst out of the gap at the break: its offset in the wall's frame from the strike point (m), its turn,
        /// size and what is left; it bounces on the floor `drop` m below the strike and rests.</summary>
        public static bool BreakChip(uint seed,int i,float since,float drop,float power,out Vector3 local,out Vector3 axis,out float angle,out float size,out float left)
        {
            local=Vector3.Zero;axis=Vector3.UnitY;angle=size=left=0;if(since<0||since>3.2f)return false;
            float spread=(H(seed,i,301)-.5f)*1.4f,vz=(2.2f+2.8f*H(seed,i,302))*power,vx=spread*vz*.6f,vy=(.6f+2.2f*H(seed,i,303))*power;size=.05f+.06f*H(seed,i,304);float floor=-(drop-size*.5f);
            float t=since,x=0,z=0,y=0;
            for(int leg=0;leg<3;leg++)
            {
                float c=y-floor,T=(vy+(float)Math.Sqrt(Math.Max(0,vy*vy+2*G*c)))/G;if(t<T){x+=vx*t;z+=vz*t;y+=vy*t-.5f*G*t*t;t=-1;break;}
                x+=vx*T;z+=vz*T;y=floor;t-=T;vy=-(vy-G*T)*.32f;vx*=.5f;vz*=.5f;
            }
            if(t>=0){float f=1-(float)Math.Exp(-t*4);x+=vx*.25f*f;z+=vz*.25f*f;y=floor;}
            local=new Vector3(x,y,z);axis=Vector3.Normalize(new Vector3(H(seed,i,305)-.5f,.6f,H(seed,i,306)-.5f));angle=(7+9*H(seed,i,307))*Math.Min(since,1.1f);left=1-Sm((since-2.6f)/.6f);return true;
        }
        /// <summary>Spark i out of the struck spot at the break (rebar, the wall's wiring): offset in the wall's frame (m), velocity, heat.</summary>
        public static bool BreakSpark(uint seed,int i,float since,out Vector3 local,out Vector3 velocity,out float heat)
        {
            local=velocity=Vector3.Zero;heat=0;float start=i<BreakSparks/2?0:.08f+.5f*H(seed,i,311),life=.3f+.25f*H(seed,i,312),t=since-start;if(t<0||t>life)return false;
            float az=(H(seed,i,313)-.5f)*2.4f,e=(H(seed,i,314)-.25f)*1.6f,v=3.5f+4f*H(seed,i,315);var d=new Vector3((float)(Math.Sin(az)*Math.Cos(e)),(float)Math.Sin(e),(float)(Math.Cos(az)*Math.Cos(e)));
            local=d*(v*t)+new Vector3(0,-.5f*G*t*t,0);velocity=d*v+new Vector3(0,-G*t,0);heat=1-t/life;return true;
        }
        /// <summary>Puff i of the dust curtain pouring off the gap's top edge after the break (`width` m wide, its edge `top` m over the
        /// floor): its offset in the wall's frame (x along from the gap's middle, y up from the floor, z out), size (m), opacity, age share.</summary>
        public static bool Curtain(uint seed,int i,float since,float width,float top,out Vector3 local,out float size,out float alpha,out float u)
        {
            local=Vector3.Zero;size=alpha=u=0;float born=.06f+.9f*(i+H(seed,i,321))/CurtainCount,t=since-born,life=CurtainSeconds-born;if(t<0||t>life)return false;u=t/life;
            // It falls (.35 m/s, gathering) to just over the floor, then billows out along it.
            float F=Math.Max(0,top-.15f),fall=Math.Min(F,.35f*t+.9f*t*t),tf=(-.35f+(float)Math.Sqrt(.1225f+3.6f*F))/1.8f,billow=Sm((t-tf)/.6f);
            local=new Vector3((H(seed,i,322)-.5f)*width*.9f,top-fall,.12f+.35f*t+.25f*billow*H(seed,i,323));size=(.22f+.55f*u+.25f*billow)*(.7f+.3f*H(seed,i,324));alpha=.75f*Sm(t/.1f)*(1-Sm((u-.4f)/.6f));return true;
        }
        /// <summary>The hardlight still tracing the gap while it waits to restore: fragment k of <see cref="Remnants"/> along the
        /// outline (its start and length as shares of the outline) and its flicker (0 off .. 1 on): a deterministic glitch, waking
        /// toward the restore; 0 before .6 s after the break and from the restore on (the scan takes over).</summary>
        public static float Remnant(uint seed,int k,float sinceBreak,float untilRestore,out float start,out float length)
        {
            start=(k+H(seed,k,331)*.5f)/Remnants;length=(.25f+.45f*H(seed,k,332))/Remnants;if(sinceBreak<.6f||untilRestore<=0)return 0;
            float slot=(float)Math.Floor(sinceBreak*9+H(seed,k,333)*9),on=NativeHordeFx.Hash(seed^(uint)slot,k,334),wake=Clamp01(1-untilRestore/6);
            return on<.18f+.5f*wake?(.35f+.65f*NativeHordeFx.Hash(seed^(uint)slot,k,335))*Sm((sinceBreak-.6f)/.5f):0;
        }
        /// <summary>Mote m of a piece flying home in the rebuild: its offset from the piece (m) and strength (they ride in with it and
        /// go as it sets); false while the piece rests or once it is solid.</summary>
        public static bool Mote(uint seed,int piece,int m,float back,float solid,float into,out Vector3 offset,out float alpha)
        {
            offset=Vector3.Zero;alpha=0;if(back<=0||solid>=.98f)return false;float a=Tau*H(seed,piece*Motes+m,341)+into*(5+3*m),r=.12f+.18f*(1-back);
            offset=new Vector3((float)Math.Cos(a)*r,(H(seed,piece*Motes+m,342)-.5f)*.3f*(1-back),(float)Math.Sin(a)*r);alpha=(1-solid)*Sm(back/.2f);return alpha>.01f;
        }
    }
}
