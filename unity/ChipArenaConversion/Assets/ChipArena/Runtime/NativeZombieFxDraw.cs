using System;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NVector2=System.Numerics.Vector2;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The spitter's acid and the heavy kinds' impacts, high-quality pass (owner 2026-10-14: "Improve visual fx from spitter/hulk
    /// zombie (make them high quality) acid needs to look better. Slam/landing/wall break [effects] also needs improvements from
    /// hulk/brute"). Draws Core/NativeZombieFx's pure shapes through the shared effects renderer (NativeVfxDraw) and its own decal
    /// layer; the callers (NativeHordeAcidPresentation, NativeHordeKindPresentation, NativeHordeZombiePresentation,
    /// NativeHordeBreachPresentation) hand it the authority's records, times and radii and the presentation clock, so host, guests
    /// and the killcam draw the same frame. Nothing is read back into the match.
    /// <list type="bullet">
    /// <item>Acid: a glossy viscous core (a lit blob stretched along its flight and wobbling, a hot fresnel rim and an inner glow),
    /// a translucent membrane (a toon water puff), an emissive halo and glowing fumes shed behind it, a stringy tail of teardrops
    /// and drips falling off it; the clot gathering in the mouth with strings across the unhinging jaw; at the landing a flash, a
    /// crown of liquid, drops thrown on arcs that land inside the pool's radius and leave spatter; the pool's lobed liquid film
    /// (natural pass, owner 2026-10-14: a lobed body, its own per record seed, whose mean extent is the authority's radius, with
    /// no ring at it; dark and thick in its core, lighter and translucent at its thin lip, a soft view-dependent wet sheen;
    /// satellite splats outside it) creeping out viscously, a bubble swelling and popping now and then, glints and faint fumes,
    /// drying into patches and receding a little, then a residue stain fading; corroded faces eaten in a ragged blotch with a cooling acid rim, glowing pits, drips running down and
    /// sizzle; a splashed pilot's sheen, drops, drips and sizzle; the interrupted spit's dribble and fizzle.</item>
    /// <item>Impacts (smash, jump-in landing, a Hulk's crash): a flash, the upright slam wave (never a flat ring) reaching the
    /// authority's radius, a crushed patch and jagged branching cracks with a hot seam, toon dust rolling out low over the floor
    /// to the radius, a dust column, lit rubble thrown on ballistic arcs that bounces twice and slides to rest inside the radius,
    /// sparks; the Hulk's landing adds a second wave and the crater's lit rubble rim.</item>
    /// <item>The wall breach: chips spalling off each blow and toon dust off the struck face; at the break toon dust bursting out,
    /// lit grit thrown out and bouncing, sparks from the struck spot, a dust curtain pouring off the gap's top edge; while it
    /// waits, the stage's hardlight glitching along the gap's outline; in the rebuild, hardlight motes riding the pieces home.</item>
    /// </list>
    /// Tiers: <see cref="NativeZombieFx.Tier"/> (Reduced effects and the low graphics tier halve every stream and drop the
    /// extras; the renderer's low tier also thins the toon puffs). The decals (pool film, spatter, etch, cracks, crush, corroded
    /// blotches, a wall's cracks) are one premultiplied mesh in <see cref="DecalQueue"/>, before the effect billboards, so smoke
    /// and fumes over them stay in front. No allocation per frame: fixed scratch rings, the renderers' reused buffers.
    /// </summary>
    public sealed class NativeZombieFxDraw : IDisposable
    {
        /// <summary>The decals' queue: after the opaque scene (depth tested against it), before the effect billboards and the
        /// Tidal Wave's body (Transparent - 1).</summary>
        public const int DecalQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent-2;
        private readonly NativeFxDraw decals=new NativeFxDraw(DecalQueue);
        private NativeVfxDraw vfx;private Vector3 eye,right=Vector3.right,up=Vector3.up;
        /// <summary>This frame's tier (NativeZombieFx.Full, Reduced, Low).</summary>
        public int Tier{get;private set;}
        private bool extras;
        private readonly Vector3[] ringA=new Vector3[NativeZombieFx.Edge],ringB=new Vector3[NativeZombieFx.Edge],ringC=new Vector3[NativeZombieFx.Edge];
        /// <summary>The decal layer (checks and the harness read what a frame queued).</summary>
        public NativeFxDraw Decals=>decals;
        private const float Authored=NativeFxDraw.Authored,Glowing=NativeFxDraw.Authored+NativeFxDraw.Additive;

        // ---- colours: linear for the renderer (HDR where they glow), sRGB for the decals ----
        private static Color L(int hex,float gain=1){var c=NativeVfxDraw.Linear(hex)*gain;c.a=0;return c;}
        private static Color S(int hex,float a)=>new Color(((hex>>16)&255)/255f,((hex>>8)&255)/255f,(hex&255)/255f,a);
        private static Color A(Color c,float a){c.a=a;return c;}
        private static readonly Color AcidGlow=L(NativeZombieFx.AcidHex,1.6f),AcidHot=L(NativeZombieFx.AcidHotHex,2.2f),CoreAlbedo=L(NativeZombieFx.AcidCoreHex),CoreEmit=L(NativeZombieFx.AcidHex,.38f),CoreRim=L(NativeZombieFx.AcidHotHex,1.7f);
        private static readonly Color MembraneLit=L(0x9cff5c),DropColor=A(L(0x63d42a),.92f),DropDeep=A(L(0x2f7a10),.9f),FumeAlbedo=L(NativeZombieFx.AcidFumeHex),Sizzle=L(0xb8c9a4),Dust=L(NativeZombieFx.DustHex),DustDark=L(NativeZombieFx.DustDarkHex);
        private static readonly Color Rock=L(0x4b4654),Warm=new Color(1.5f,1.22f,.92f,0),SparkHot=new Color(2.6f,1.7f,.8f,0),Hardlight=new Color(.38f*2.4f,.92f*2.4f,1f*2.4f,0);

        private int baseTier;
        public void Begin(NativeVfxDraw vfx,Vector3 eye,Vector3 right,Vector3 up,int tier){this.vfx=vfx;this.eye=eye;this.right=right;this.up=up;baseTier=Tier=tier;extras=NativeZombieFx.Extras(tier);decals.Clear();}
        /// <summary>Sets the tier for an effect `distance` m from the camera, as the minion effects band theirs (NativeMinionFx.Band):
        /// false past 40 m (a heavy one 80 m), halved past 24 m (a heavy one 48 m).</summary>
        public bool Within(float distance,bool heavy=false)
        {
            float far=heavy?80:(float)NativeMinionFx.FarBand,full=heavy?48:(float)NativeMinionFx.FullBand;if(!(distance<=far))return false;
            Tier=distance>full?Math.Max(baseTier,NativeZombieFx.Reduced):baseTier;extras=NativeZombieFx.Extras(Tier);return true;
        }
        /// <summary>The camera this frame's effects face (the callers' distance for <see cref="Within"/>).</summary>
        public Vector3 Eye=>eye;
        /// <summary>Draws this frame's decals to the camera (the billboards and lit pieces go with the shared renderer's Submit).</summary>
        public void Submit(Camera camera,float time){decals.Draw(camera,null,time);}
        public void Clear()=>decals.Clear();
        public void Dispose()=>decals.Dispose();
        private int N(int n)=>NativeZombieFx.Count(n,Tier);
        private static Vector3 U(NVector v)=>new Vector3(v.X,v.Y,v.Z);private static NVector V(Vector3 v)=>new NVector(v.x,v.y,v.z);
        private static float Sm(float x)=>MeleeMath.Smooth(x);
        // The renderer's seeds are floats (its shaders pick a puff's tile and turn from them): kept small and exact.
        private static float F(uint seed,long k)=>(float)((seed%8191u+(ulong)(k&0xffff))%65536u);

        // ---- decal primitives (one layer, premultiplied; flags: Authored or Glowing) ----
        private void Fan(Vector3 c,Vector3[] pts,int n,Color inner,Color outer,float flags)
        {
            var layer=decals.Effects;var uv=new Vector4(0,0,NativeFxDraw.Flat,flags);int s=layer.Vertex(c,Vector3.up,inner,uv,Vector4.zero,Vector4.zero);
            for(int k=0;k<n;k++)layer.Vertex(pts[k],Vector3.up,outer,uv,Vector4.zero,Vector4.zero);
            for(int k=0;k<n;k++)layer.Triangle(s,s+1+k,s+1+(k+1)%n);
        }
        private void Band(Vector3[] a,Vector3[] b,int n,Color ca,Color cb,float flags)
        {
            var layer=decals.Effects;var uv=new Vector4(0,0,NativeFxDraw.Flat,flags);int s=layer.Count;
            for(int k=0;k<n;k++){layer.Vertex(a[k],Vector3.up,ca,uv,Vector4.zero,Vector4.zero);layer.Vertex(b[k],Vector3.up,cb,uv,Vector4.zero,Vector4.zero);}
            for(int k=0;k<n;k++){int i=s+k*2,j=s+((k+1)%n)*2;layer.Triangle(i,i+1,j+1);layer.Triangle(i,j+1,j);}
        }
        // A tapered strip from a to b of width wa..wb on the plane with normal n (cracks, runs).
        private void Strip(Vector3 a,Vector3 b,float wa,float wb,Vector3 n,Color color,float flags)
        {
            var d=b-a;var side=Vector3.Cross(n,d);float l=side.magnitude;if(l<1e-6f)return;side/=l;decals.Quad(decals.Effects,a-side*wa,b-side*wb,b+side*wb,a+side*wa,color,flags);
        }

        // ================================================================================================================
        // The spitter's acid
        // ================================================================================================================
        /// <summary>The glob in flight on the authority's arc (`since` s after its launch at the mouth `from`, landing at `to` after
        /// `flight` s): its glossy core, membrane, halo, stringy tail, drips and fumes. False once it has landed.</summary>
        public bool Glob(Vector3 from,Vector3 to,float flight,float since,uint seed,float floor)
        {
            if(vfx==null)return false;var nf=V(from);var nt=V(to);if(!NativeHordeFx.Glob(nf,nt,flight,since,out var at))return false;
            var p=U(at);var vel=U(NativeZombieFx.GlobVelocity(nf,nt,flight,since));float speed=vel.magnitude;var dir=speed>1e-3f?vel/speed:Vector3.down;
            NativeZombieFx.GlobShape(seed,since,speed,out float along,out float across);var turn=Quaternion.FromToRotation(Vector3.up,dir);float R=NativeZombieFx.GlobRadius;
            vfx.Blob(p,turn,new Vector3(across*R,along*R,across*R),CoreAlbedo,1.5f,.94f,CoreEmit,CoreRim);
            vfx.Glow(p,AcidGlow*.9f,R*6.5f,NativeVfxDraw.Mote,0,F(seed,0));
            if(extras)vfx.Water(p-dir*R*.3f,MembraneLit,NativeZombieFx.AcidShadeHex,.26f,R*NativeZombieFx.MembraneScale*2.1f,vel*.016f,.1f,F(seed,0),NativeVfxDraw.NoFloor);
            // The tail: teardrop strings from bead to bead, thinning to a thread.
            Vector3 prev=p;float prevShare=.8f;
            for(int k=0;k<N(NativeZombieFx.TailBeads);k++)
            {
                if(!NativeZombieFx.TailBead(nf,nt,flight,since,k,out var b,out float share))break;var bp=U(b);
                vfx.Drop(prev,prev-bp,DropColor,R*2*(share+prevShare)*.5f,.1f,F(seed,k));prev=bp;prevShare=share;
            }
            for(int i=0;i<N(NativeZombieFx.Drips);i++)if(NativeZombieFx.Drip(nf,nt,flight,seed,i,since,out var dp,out var dv))vfx.Drop(U(dp),U(dv)*.03f,DropColor,.028f,.2f,F(seed,40+i));
            if(extras)
            {
                long k0=(long)Math.Floor((since-NativeZombieFx.FumeLife)*NativeZombieFx.FumeRate),k1=(long)Math.Floor(Math.Min(since,flight)*NativeZombieFx.FumeRate);
                for(long k=Math.Max(0,k0);k<=k1;k++)if(NativeZombieFx.GlobFume(nf,nt,flight,seed,k,since,out var fp,out float fs,out float fa,out float fu))vfx.Vapor(U(fp),FumeAlbedo,fa*.8f,fs*1.6f,fu,F(seed,k),floor,.4f);
            }
            return true;
        }
        /// <summary>The wind-up at the mouth socket (`age` from LastAttack, the release at `strike`; `fwd`/`headUp` the head's frame,
        /// `jaw` its opening): the clot gathering, strings across the jaw, the throat's glow (`swell`).</summary>
        public void Windup(Vector3 mouth,Vector3 throat,Vector3 fwd,Vector3 headUp,float age,float strike,float jaw,float swell,uint seed)
        {
            if(vfx==null)return;
            if(swell>0)vfx.Glow(throat,AcidGlow*(.4f*swell),.16f+.22f*swell,NativeVfxDraw.CoreGlow,0,F(seed,0));
            if(NativeZombieFx.MouthGlob(seed,age,strike,out float r,out float bulge,out float glow))
            {
                var c=mouth+fwd*bulge-headUp*bulge*.35f;vfx.Blob(c,Quaternion.identity,new Vector3(r,r*1.12f,r),CoreAlbedo,1.5f,.94f,CoreEmit*(.6f+.8f*glow),CoreRim);
                vfx.Glow(c,AcidGlow*(.5f+.9f*glow),.14f+.3f*glow,NativeVfxDraw.Mote,0,F(seed,0));
            }
            var side=Vector3.Cross(headUp,fwd).normalized;
            for(int k=0;k<N(NativeZombieFx.MouthStrings);k++)if(NativeZombieFx.MouthString(seed,k,age,strike,jaw,out var t,out var b,out float sag,out float w))
            {
                Vector3 top=mouth+side*t.X+headUp*t.Y+fwd*t.Z,bottom=mouth+side*b.X+headUp*b.Y+fwd*b.Z,mid=(top+bottom)*.5f-headUp*sag*.4f+fwd*sag;
                vfx.Drop(mid,mid-top,DropColor,w*3f,.1f,F(seed,k));vfx.Drop(bottom,bottom-mid,DropColor,w*3.6f,.1f,F(seed,8+k));
            }
            for(int i=0;i<N(NativeHordeFx.DroolDrops);i++)if(NativeHordeFx.Drool(seed,i,age,strike,out var o,out float sz,out float da))
                vfx.Drop(mouth+side*o.X+Vector3.up*o.Y+fwd*o.Z*.5f,Vector3.down*(.03f+.4f*(-o.Y)),A(DropColor,DropColor.a*da),sz*1.8f,.1f,F(seed,20+i));
        }
        /// <summary>The landing `since` s after the glob touched down at `c` (floor height `floor`), its pool of `radius` (the
        /// authority's) for `duration` s: the flash, crown, drops (those landing inside the body leave a spatter mark until the
        /// spreading liquid takes it in; the satellites' drops fly past its edge), the sizzle.</summary>
        public void Splash(Vector3 c,float floor,float since,float radius,float duration,uint seed)
        {
            if(vfx==null||since<0)return;var g=new Vector3(c.x,floor,c.z);
            if(NativeZombieFx.SplashFlash(since,out float fs,out float fa)){vfx.Glow(g+Vector3.up*.12f,AcidHot*(.5f*fa),fs*.85f,NativeVfxDraw.CoreGlow,0,F(seed,0));}
            for(int k=0;k<N(NativeZombieFx.CrownLobes);k++)if(NativeZombieFx.Crown(seed,k,since,out var o,out float s,out float a))
            {var off=U(o);var flat=new Vector3(off.x,0,off.z).normalized;vfx.Water(g+off,MembraneLit,NativeZombieFx.AcidShadeHex,a,s*2.2f,(flat*.4f+Vector3.up)*.06f,since/NativeZombieFx.CrownSeconds*.5f,F(seed,k),floor);}
            for(int i=0;i<N(NativeZombieFx.SplashDrops);i++)if(NativeZombieFx.SplashDrop(seed,i,since,radius,out var o,out var v,out float s))vfx.Drop(g+U(o),U(v)*.035f,DropColor,s*1.6f,.15f,F(seed,60+i));
            for(int i=0;i<N(NativeZombieFx.Satellites);i++)if(NativeZombieFx.SatelliteDrop(seed,i,since,radius,out var o,out var v,out float s))vfx.Drop(g+U(o),U(v)*.035f,DropColor,s*1.6f,.15f,F(seed,80+i));
            // Spatter: each drop's wet mark where it landed, until the creeping liquid reaches it and takes it in.
            NativeZombieFx.Pool(Math.Min(since,duration),duration,out _,out float wet);float spatterAlpha=since>duration?0:.8f*wet;
            if(spatterAlpha>0)for(int i=0;i<N(NativeZombieFx.SplashDrops);i++)
            {
                float land=NativeZombieFx.SplashLand(seed,i,radius);if(since<land)continue;var at=NativeZombieFx.SpatterAt(seed,i,radius);float size=NativeZombieFx.SpatterSize(seed,i,out float turn)*Sm((since-land)/.08f);
                float dist=at.Length(),body=radius*NativeZombieFx.EdgeAt(seed,Mathf.Atan2(at.Y,at.X),since,duration),shown=spatterAlpha*(1-Sm((body-dist+size)/(size*2+1e-3f)));if(shown<=.01f)continue;
                var pc=new Vector3(g.x+at.X,floor+.0075f,g.z+at.Y);int n=8;for(int k=0;k<n;k++){float ang=turn+k*Mathf.PI*2/n,rr=size*(.65f+.35f*NativeHordeFx.Hash(seed,i*8+k,157));ringA[k]=pc+new Vector3(Mathf.Cos(ang)*rr,0,Mathf.Sin(ang)*rr);}
                Fan(pc,ringA,n,Shade(.62f,shown),Shade(.2f,shown*.85f),Authored);
            }
            // The sizzle where it struck.
            for(int k=0;k<N(4);k++)if(NativeHordeFx.Sizzle(seed^0x51u,k,since,.25f,out var so,out float ss,out float sa)&&since<1.2f)vfx.Smoke(g+U(so)+Vector3.up*.04f,Sizzle,sa*(1-since/1.2f),ss*2.4f,((since*1.4f)%1),F(seed,90+k),floor);
        }

        // ---- the liquid body (a decal on the floor: no ring, no band at the radius) ----
        // Rings of the body, a share of its edge at each angle: the deep core, the mottled middle, the thinning lip, the edge, and a
        // feather just past it (transparent) so the boundary is soft but defined.
        private static readonly float[] BodyRings={.14f,.3f,.46f,.6f,.72f,.82f,.9f,.96f,1f,1.02f},SheenRings={.3f,.6f,.82f,.93f,1f},StainRings={.3f,.6f,.85f,.97f,1.04f};
        /// <summary>The thickness of the lip at the body's very edge (a thin wet meniscus: defined, lighter, translucent).</summary>
        private const float LipThickness=.17f;
        private readonly float[] bodyEdge=new float[NativeZombieFx.PoolEdge];private int bodyN;
        // The liquid's colour for its thickness (0 its thin lip .. 1 the deep core) at `alpha`: dark and nearly opaque where deep, a
        // lighter, yellower green and translucent where thin (the floor shows through), gone where dried to nothing.
        private static Color Shade(float thick,float alpha)
        {
            float t=Mathf.Clamp01(thick);Color c;float o;
            if(t<.3f){float k=t/.3f;c=Color.Lerp(LipTone,ThinTone,k);o=Mathf.Lerp(.6f,.8f,k);}
            else if(t<.62f){float k=(t-.3f)/.32f;c=Color.Lerp(ThinTone,MidTone,k);o=Mathf.Lerp(.8f,.92f,k);}
            else{float k=(t-.62f)/.38f;c=Color.Lerp(MidTone,DeepTone,k);o=Mathf.Lerp(.92f,.96f,k);}
            c.a=o*alpha*Sm(t/.3f);return c;
        }
        private static readonly Color LipTone=S(0x6c9f2b,1),ThinTone=S(0x357212,1),MidTone=S(0x1c500a,1),DeepTone=S(0x0c2a04,1),SheenTone=S(0xc4f08a,1),StainTone=S(0x2f3410,1),ResidueTone=S(0x444a17,1);
        // The outline (radius times NativeZombieFx.EdgeAt round the circle) into bodyEdge, on the tier's vertex count.
        private void BodyOutline(float radius,uint seed,float since,float duration,bool spreading)
        {
            bodyN=Tier==NativeZombieFx.Low?NativeZombieFx.PoolEdgeLow:NativeZombieFx.PoolEdge;
            for(int k=0;k<bodyN;k++){float a=k*Mathf.PI*2/bodyN;bodyEdge[k]=radius*(spreading?NativeZombieFx.EdgeAt(seed,a,since,duration):NativeZombieFx.Outline(seed,a,since));}
        }
        // The body over bodyEdge at `g`: its film (thickness-shaded, mottled, patchy as it dries) and its sheen (additive, the view's
        // Fresnel, broken round the edge). `film` fades the whole.
        private void LiquidBody(Vector3 g,uint seed,float since,float wet,float film,float lift)
        {
            var layer=decals.Effects;int n=bodyN,rings=BodyRings.Length;var uv=new Vector4(0,0,NativeFxDraw.Flat,Authored);
            // The centre takes the innermost ring's mean, so the fan round it never shows spokes.
            float centre=0;for(int k=0;k<n;k++){float a=k*Mathf.PI*2/n,r=bodyEdge[k]*BodyRings[0];centre+=NativeZombieFx.Thickness(seed,BodyRings[0],Mathf.Cos(a)*r,Mathf.Sin(a)*r,since,wet);}
            int s=layer.Vertex(g+Vector3.up*lift,Vector3.up,Shade(centre/n,film),uv,Vector4.zero,Vector4.zero);
            film*=.4f+.6f*wet;
            for(int k=0;k<n;k++)
            {
                float a=k*Mathf.PI*2/n,ca=Mathf.Cos(a),sa=Mathf.Sin(a),e=bodyEdge[k],inner=0;
                for(int j=0;j<rings;j++)
                {
                    float rho=BodyRings[j],x=ca*e*rho,z=sa*e*rho,th=NativeZombieFx.Thickness(seed,rho,x,z,since,wet);
                    // The edge: a thin lip, never more than the liquid just inside it (so drying never leaves an outline); then the feather.
                    if(j==rings-2)th=Mathf.Min(LipThickness,inner);inner=th;
                    layer.Vertex(g+new Vector3(x,lift,z),Vector3.up,j==rings-1?A(LipTone,0):Shade(th,film),uv,Vector4.zero,Vector4.zero);
                }
            }
            Grid(layer,s,n,rings);
            // The sheen: the sky's reflection off the wet surface (stronger at grazing views), never a clean outline.
            if(!(wet>.02f))return;var gl=new Vector4(0,0,NativeFxDraw.Flat,Glowing);int sr=SheenRings.Length;
            s=layer.Vertex(g+Vector3.up*(lift+.001f),Vector3.up,Sheened(g,0,0,seed,0,0,wet,film),gl,Vector4.zero,Vector4.zero);
            for(int k=0;k<n;k++)
            {
                float a=k*Mathf.PI*2/n,ca=Mathf.Cos(a),sa=Mathf.Sin(a),e=bodyEdge[k];
                for(int j=0;j<sr;j++){float rho=SheenRings[j];layer.Vertex(g+new Vector3(ca*e*rho,lift+.001f,sa*e*rho),Vector3.up,Sheened(g,ca*e*rho,sa*e*rho,seed,rho,a,wet,film),gl,Vector4.zero,Vector4.zero);}
            }
            Grid(layer,s,n,sr);
        }
        private Color Sheened(Vector3 g,float x,float z,uint seed,float rho,float angle,float wet,float film)
        {
            var to=eye-(g+new Vector3(x,0,z));float l=to.magnitude;float k=NativeZombieFx.Sheen(seed,rho,angle,wet)*NativeZombieFx.Fresnel(l>1e-4f?to.y/l:1)*.42f*film;
            return new Color(SheenTone.r,SheenTone.g,SheenTone.b,k);
        }
        // A polar grid: the centre vertex `s`, then `n` spokes of `rings` vertices each (spoke-major).
        private static void Grid(NativeFxDraw.Layer layer,int s,int n,int rings)
        {
            for(int k=0;k<n;k++)
            {
                int a0=s+1+k*rings,b0=s+1+((k+1)%n)*rings;layer.Triangle(s,a0,b0);
                for(int j=0;j<rings-1;j++){layer.Triangle(a0+j,a0+j+1,b0+j+1);layer.Triangle(a0+j,b0+j+1,b0+j);}
            }
        }
        // The residue stain where the pool dried (its outline pinned where it began to dry): a faint mottled floor stain, a little
        // heavier and yellower toward its edge, unevenly, feathered out.
        private void Stain(Vector3 g,float radius,uint seed,float duration,float alpha)
        {
            var layer=decals.Effects;int n=Tier==NativeZombieFx.Low?NativeZombieFx.PoolEdgeLow:NativeZombieFx.PoolEdge,rings=StainRings.Length;var uv=new Vector4(0,0,NativeFxDraw.Flat,Authored);
            int s=layer.Vertex(g+Vector3.up*.004f,Vector3.up,A(StainTone,alpha*.5f),uv,Vector4.zero,Vector4.zero);
            for(int k=0;k<n;k++)
            {
                float a=k*Mathf.PI*2/n,ca=Mathf.Cos(a),sa=Mathf.Sin(a),e=radius*NativeZombieFx.EtchEdge(seed,a,duration),res=NativeZombieFx.Residue(seed,a);
                for(int j=0;j<rings;j++)
                {
                    float rho=StainRings[j],x=ca*e*rho,z=sa*e*rho,m=NativeZombieFx.Mottle(seed^0x5eedu,x,z,0);
                    Color col=j==rings-1?A(StainTone,0):j==rings-2?A(Color.Lerp(StainTone,ResidueTone,res*m),alpha*(.4f+.35f*res*m)):A(StainTone,alpha*(.3f+.4f*m));
                    layer.Vertex(g+new Vector3(x,.004f,z),Vector3.up,col,uv,Vector4.zero,Vector4.zero);
                }
            }
            Grid(layer,s,n,rings);
        }
        // Satellite splat i outside the body: stretched outward, wet (the liquid's colour) drying early into a stain.
        private void SatelliteSplat(Vector3 g,uint seed,int i,float radius,float since,float duration)
        {
            NativeZombieFx.Satellite(seed,i,radius,out var at,out var along,out float length,out float width,out float land);if(since<land)return;
            float grow=Sm((since-land)/.07f),w=NativeZombieFx.SatelliteWet(since,duration),fade=1-Sm((since-duration)/NativeZombieFx.EtchSeconds);if(fade<=0)return;
            Vector3 c=g+new Vector3(at.X,.007f,at.Y),f=new Vector3(along.X,0,along.Y),side=new Vector3(-along.Y,0,along.X);int n=10;
            for(int k=0;k<n;k++)
            {
                float ph=k*Mathf.PI*2/n,cp=Mathf.Cos(ph),lump=.82f+.3f*NativeHordeFx.Hash(seed,i*16+k,277),tail=1+.32f*Mathf.Pow(Mathf.Max(0,cp),3);
                ringA[k]=c+f*(cp*length*.5f*tail*lump*grow)+side*(Mathf.Sin(ph)*width*.5f*lump*grow);
            }
            Color inner=Color.Lerp(A(StainTone,.5f*NativeZombieFx.StainMax),Shade(.5f,1),w),outer=Color.Lerp(A(ResidueTone,.4f*NativeZombieFx.StainMax),Shade(.12f,1),w);inner.a*=fade;outer.a*=fade;
            Fan(c,ringA,n,inner,outer,Authored);
        }
        /// <summary>The pool of `radius` (the authority's) at `c` on the floor `floor`, `since` s after the landing, for `duration` s:
        /// the residue stain as it dries, the lobed liquid body (mean extent the radius; no ring) and its sheen, the satellite
        /// splats, bubbles now and then, glints and faint fumes.</summary>
        public void Pool(Vector3 c,float floor,float since,float radius,float duration,uint seed)
        {
            if(vfx==null||since<0)return;var g=new Vector3(c.x,floor,c.z);
            if(NativeZombieFx.Etch(since,duration,out float etch))Stain(g,radius,seed,duration,etch);
            if(!NativeZombieFx.Pool(since,duration,out float spread,out float wet))return;
            BodyOutline(radius,seed,since,duration,true);LiquidBody(g,seed,since,wet,Sm(since/.05f),.008f);
            for(int i=0;i<N(NativeZombieFx.Satellites);i++)SatelliteSplat(g,seed,i,radius,since,duration);
            // Bubbles: one swells now and then and pops, a ripple spreading (glossy domes on the full tier).
            for(int i=0;i<N(NativeZombieFx.PoolBubbles);i++)if(NativeZombieFx.PoolBubble(seed,i,since,duration,radius,out var o,out float s,out float pop))
            {
                var p=g+U(o)+Vector3.up*.009f;
                if(pop<=0){if(extras)vfx.Blob(p,Quaternion.identity,new Vector3(s,s*.7f,s),CoreAlbedo*1.3f,1.6f,.95f,CoreEmit*.25f,CoreRim*.5f);continue;}
                Ripple(p+Vector3.up*.002f,s*(1+3.2f*pop),.3f*(1-pop)*(1-pop)*wet);
                if(extras&&pop<.4f)for(int m=0;m<2;m++){float ang=(i*2+m)*2.4f;var dir=new Vector3(Mathf.Cos(ang)*.35f,1,Mathf.Sin(ang)*.35f);vfx.Drop(p+dir*(.02f+.18f*pop),dir*.04f,DropColor,.012f,pop,seed+i*3+m);}
            }
            if(extras)for(int i=0;i<NativeZombieFx.Glints;i++)if(NativeZombieFx.Glint(seed,i,since,duration,radius,out var o,out float ga))vfx.Glow(g+U(o),new Color(1.1f,1.5f,.9f,0)*ga,.07f,NativeVfxDraw.Mote,0,F(seed,i));
            for(int i=0;i<N(NativeZombieFx.PoolFumes);i++)if(NativeZombieFx.PoolFume(seed,i,since,duration,radius,out var o,out float s,out float a,out float u))vfx.Vapor(g+U(o),FumeAlbedo,a,s*1.7f,u,F(seed,200+i),floor,.45f);
        }
        // A bubble's ripple: a thin, faint, lighter ring a few centimetres across (12 segments).
        private void Ripple(Vector3 c,float r,float alpha)
        {
            if(alpha<=.005f)return;int n=12;for(int k=0;k<n;k++){float a=k*Mathf.PI*2/n;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));ringB[k]=c+d*(r*.72f);ringC[k]=c+d*r;}
            Band(ringB,ringC,n,A(SheenTone,0),A(SheenTone,alpha),Glowing);
        }
        /// <summary>After the pool: its residue stain and the satellites' stains fading, the last fumes clearing.</summary>
        public void PoolAfter(Vector3 c,float floor,float since,float radius,float duration,uint seed)
        {
            if(vfx==null||since<=duration)return;var g=new Vector3(c.x,floor,c.z);
            if(NativeZombieFx.Etch(since,duration,out float etch))Stain(g,radius,seed,duration,etch);
            for(int i=0;i<N(NativeZombieFx.Satellites);i++)SatelliteSplat(g,seed,i,radius,since,duration);
            for(int i=0;i<N(NativeZombieFx.PoolFumes);i++)if(NativeZombieFx.PoolFume(seed,i,since,duration,radius,out var o,out float s,out float a,out float u))vfx.Vapor(g+U(o),FumeAlbedo,a,s*1.7f,u,F(seed,200+i),floor,.45f);
        }
        /// <summary>A face the acid eats (`at` the contact, `n` its normal, `tu`/`tv` along it, `down` down it): the ragged blotch
        /// and its cooling acid rim, glowing pits, drips running down, sizzle; `since` s after contact, for `duration`.</summary>
        public void Corrode(Vector3 at,Vector3 n,Vector3 tu,Vector3 tv,Vector3 down,float since,float duration,uint seed)
        {
            if(vfx==null)return;const float spread=.32f;if(!NativeZombieFx.Blotch(since,duration,spread,out float reach,out float heat,out float alpha))return;
            var o=at+n*.006f;int e=NativeZombieFx.Edge;
            for(int k=0;k<e;k++){float a=k*Mathf.PI*2/e,r=reach*NativeZombieFx.BlotchEdge(seed,k);var d=tu*Mathf.Cos(a)+tv*Mathf.Sin(a);ringA[k]=o+d*r*.82f;ringB[k]=o+d*r;}
            Fan(o,ringA,e,S(0x16190a,alpha),S(0x34420e,alpha*.9f),Authored);Band(ringA,ringB,e,S(0x34420e,alpha*.9f),S(0x4a6a14,alpha*.4f),Authored);
            if(heat>.02f)Band(ringA,ringB,e,S(NativeZombieFx.AcidHex,.1f*heat*alpha),S(NativeZombieFx.AcidHex,.75f*heat*alpha),Glowing);
            for(int k=0;k<N(NativeHordeFx.Pits);k++)if(NativeHordeFx.Pit(seed,k,since,duration,spread*.75f,out var pa,out float ps,out float pal))
            {
                Vector3 pc=o+n*.002f+tu*pa.X+tv*pa.Y;decals.Quad(decals.Effects,pc-tu*ps-tv*ps,pc+tu*ps-tv*ps,pc+tu*ps+tv*ps,pc-tu*ps+tv*ps,S(0x080a04,pal),Authored);
                float flick=.55f+.45f*Mathf.Sin(since*(9+k)+k*1.7f);vfx.Glow(pc+n*.01f,AcidGlow*(.55f*pal*heat*flick),ps*2.4f,NativeVfxDraw.Mote,0,F(seed,k));
            }
            for(int k=0;k<N(NativeHordeFx.CorrodeDrips);k++)if(NativeZombieFx.FaceDrip(seed,k,since,duration,spread,out float x,out float run,out float speed,out float da))
            {
                Vector3 d0=o+n*.003f+tu*x,d1=d0+down*run;if(run>.01f)Strip(d0,d1,.012f,.006f,n,S(0x2c4a0c,da*.85f),Authored);
                if(speed>0)vfx.Drop(d1+n*.01f,down*(.03f+.06f*speed),A(DropColor,DropColor.a*da),.022f,.2f,F(seed,30+k));
            }
            for(int k=0;k<N(NativeHordeFx.Sizzles);k++)if(NativeHordeFx.Sizzle(seed,k,since,spread,out var so,out float ss,out float sa))
                vfx.Smoke(at+n*(.05f+so.Y*.3f)+tu*so.X+tv*so.Z+Vector3.up*so.Y,Sizzle,sa*alpha*.8f,ss*2.2f,(since*.9f+k*.37f)%1,F(seed,50+k),NativeVfxDraw.NoFloor);
        }
        /// <summary>A splashed pilot (`chest` the body's chest, `feet` its floor height), `since` s after the splash for `seconds`.</summary>
        public void Body(Vector3 chest,float feet,float since,float seconds,uint seed)
        {
            if(vfx==null||since<0||since>seconds)return;float fade=1-Sm((since-(seconds-.7f))/.7f);
            if(NativeZombieFx.SplashFlash(since,out float fs,out float fa))vfx.Glow(chest,AcidHot*(.8f*fa),fs*.8f,NativeVfxDraw.CoreGlow,0,F(seed,0));
            for(int i=0;i<N(NativeZombieFx.BodyDrops);i++)if(NativeZombieFx.BodyDrop(seed,i,since,chest.y-feet,out var o,out var v,out float s))vfx.Drop(chest+U(o),U(v)*.035f,DropColor,s*1.6f,.2f,F(seed,i));
            // The sheen on the chest and head (and the shoulders on the full tier), sizzling on the full tier.
            for(int b=0;b<(extras?4:2);b++)
            {
                Vector3 at=b==0?chest:b==1?chest+Vector3.up*.42f:chest+right*(b==2?.24f:-.24f)+Vector3.up*.18f;float k2=.5f+.5f*Mathf.Sin(since*9+b*1.7f);
                vfx.Glow(at,AcidGlow*(.5f*fade*k2),.34f+.08f*k2,NativeVfxDraw.Mote,0,F(seed,b));
                if(extras&&NativeHordeFx.Sizzle(seed,b,since,.12f,out var so,out float ss,out float sa))vfx.Smoke(at+U(so),Sizzle,sa*fade*.9f,ss*2f,(since*1.1f+b*.25f)%1,F(seed,10+b),NativeVfxDraw.NoFloor);
            }
            for(int i=0;i<N(NativeZombieFx.BodyDrips);i++)if(NativeZombieFx.BodyDrip(seed,i,since,seconds,out float down,out float ang,out float da))
            {var start=chest+new Vector3(Mathf.Cos(ang)*.27f,.25f,Mathf.Sin(ang)*.27f);if(start.y-down>feet+.05f)vfx.Drop(start-Vector3.up*down,Vector3.down*.06f,A(DropColor,DropColor.a*da*fade),.022f,.2f,F(seed,30+i));}
        }
        /// <summary>The interrupted spit's acid dribbling from the mouth to the floor and fizzling there (`since` s after the cancel).</summary>
        public void Fizzle(Vector3 mouth,float floor,float since,uint seed)
        {
            if(vfx==null||since<0||since>NativeHordeFx.FizzleSeconds)return;float drop=Mathf.Max(.1f,mouth.y-floor);
            for(int k=0;k<N(NativeHordeFx.FizzleDrops);k++)if(NativeHordeFx.Fizzle(seed,k,since,drop,out var o,out float sz,out float al))vfx.Drop(mouth+U(o),Vector3.down*(.03f+.08f*Mathf.Sqrt(-o.Y)),A(DropColor,DropColor.a*al),sz*2f,.1f,F(seed,k));
            var c=new Vector3(mouth.x,floor+.008f,mouth.z);
            if(NativeHordeFx.FizzlePool(since,out float r,out float pa))
            {
                // A small lobed puddle like the spit's own (no ring), thinning as it fizzles away.
                BodyOutline(r,seed,since,NativeHordeFx.FizzleSeconds,false);LiquidBody(new Vector3(c.x,floor,c.z),seed,since,Mathf.Clamp01(pa/.7f),Mathf.Clamp01(pa/.7f),.008f);
                for(int k=0;k<N(3);k++)if(NativeHordeFx.Sizzle(seed,k,since,r,out var so,out float ss,out float sa))vfx.Smoke(c+U(so)*.8f,Sizzle,sa*pa,ss*1.8f,(since+k*.3f)%1,F(seed,20+k),floor);
            }
        }

        // ================================================================================================================
        // Impacts
        // ================================================================================================================
        /// <summary>One impact at `c` (its centre on the floor) `since` s after the strike or the touchdown, on its profile (the
        /// authority's radius); `ink` the kind's seam colour (sRGB hex) for the hot seams and the wave's cool end.</summary>
        public void Impact(Vector3 c,float since,uint seed,in NativeZombieFx.Impact p,int ink)
        {
            if(vfx==null||since<0)return;var inkL=L(ink);float floor=c.y;
            if(NativeZombieFx.ImpactFlash(since,p.Power,out float fs,out float fa)){vfx.Glow(c+Vector3.up*.3f,Warm*(1.1f*fa),fs,NativeVfxDraw.CoreGlow,0,F(seed,0));vfx.Glow(c+Vector3.up*.15f,inkL*(1.6f*fa),fs*1.4f,NativeVfxDraw.Mote,0,F(seed,1));}
            for(int w=0;w<2;w++)if(NativeZombieFx.Wave(p,w,since,out float wr,out float wh,out float wp,out float wf))vfx.SlamWave(c+Vector3.up*.01f,wr,wh,wp,wf,Warm*3f,inkL*3.2f,F(seed,w));
            ImpactDecals(c,since,seed,p,ink);
            // Dust: light where it is kicked up fresh, settling darker as it rolls out (fewer, larger puffs on the thinner tiers).
            float grow=Tier==NativeZombieFx.Full?1:1.35f;
            for(int i=0;i<N(p.Dust);i++)if(NativeZombieFx.HugDust(seed,i,N(p.Dust),since,p,out var o,out float s,out float a,out float u))vfx.Smoke(c+U(o),Color.Lerp(Dust,DustDark,u*.8f+(i%3==0?.2f:0)),a,s*grow,u,F(seed,100+i),floor);
            for(int i=0;i<N(p.Column);i++)if(NativeZombieFx.ColumnDust(seed,i,since,p,out var o,out float s,out float a,out float u))vfx.Smoke(c+U(o),Color.Lerp(Dust,DustDark,u*.6f),a,s*grow,u,F(seed,160+i),floor);
            for(int i=0;i<N(p.Debris);i++)if(NativeZombieFx.Chunk(seed,i,N(p.Debris),since,p,out var o,out var axis,out float ang,out float s,out float left))
                vfx.Rubble(c+U(o),Quaternion.AngleAxis(ang*Mathf.Rad2Deg,U(axis)),s*left,(int)(seed%997)+i,Rock,inkL*.05f);
            if(extras)for(int i=0;i<p.Sparks;i++)if(NativeZombieFx.Spark(seed,i,since,p,out var o,out var v,out float heat))vfx.Streak(c+U(o),U(v),SparkHot*(heat*heat),.022f,.05f,.03f,.85f,1-heat,F(seed,300+i));
            if(p.Crater)for(int k=0;k<N(NativeHordeFx.CraterChunks);k++)if(NativeHordeFx.CraterChunk(seed,k,since,p.Radius*.32f,out var co,out float cs,out float tilt,out float cal))
                vfx.Rubble(c+U(co)+Vector3.up*cs*.2f,Quaternion.Euler(tilt*57.3f,k*37,tilt*30),cs*1.25f*cal,(int)(seed%991)+k,Rock*.85f,inkL*.03f);
        }
        // The impact's floor marks: the crater, the crushed patch, the cracks with their hot seams.
        private void ImpactDecals(Vector3 c,float since,uint seed,in NativeZombieFx.Impact p,int ink)
        {
            var g=c+Vector3.up*.006f;int n=NativeZombieFx.Edge;
            if(p.Crater&&NativeHordeFx.Crater(since,p.Radius*.32f,out float cr,out float ca))
            {for(int k=0;k<n;k++){float a=k*Mathf.PI*2/n,r=cr*NativeZombieFx.BlotchEdge(seed^0x3cu,k);ringA[k]=g+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);}Fan(g,ringA,n,S(0x050407,ca),S(0x16121c,ca*.8f),Authored);}
            if(NativeZombieFx.Crush(since,p,out float reach,out float crush))
            {
                for(int k=0;k<n;k++){float a=k*Mathf.PI*2/n,r=reach*NativeZombieFx.BlotchEdge(seed,k);var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));ringA[k]=g+d*r*.6f+Vector3.up*.001f;ringB[k]=g+d*r+Vector3.up*.001f;}
                Fan(g+Vector3.up*.001f,ringA,n,S(0x0a080e,crush),S(0x18141e,crush*.85f),Authored);Band(ringA,ringB,n,S(0x18141e,crush*.85f),S(0x18141e,0),Authored);
            }
            float alpha=NativeZombieFx.CrackAlpha(since,p.CrackStay),hot=NativeZombieFx.CrackGlow(since);if(alpha<=0)return;int count=N(p.Cracks);
            for(int i=0;i<count;i++)
            {
                var prev=NativeZombieFx.CrackPoint(seed,i,count,0,since,p,out float pw);
                for(int j=1;j<=NativeZombieFx.CrackSegments;j++)
                {
                    var next=NativeZombieFx.CrackPoint(seed,i,count,j,since,p,out float nw);Vector3 a=g+new Vector3(prev.X,.002f,prev.Y),b=g+new Vector3(next.X,.002f,next.Y);
                    Strip(a,b,pw,nw,Vector3.up,S(0x07060a,alpha),Authored);if(hot>0)Strip(a+Vector3.up*.001f,b+Vector3.up*.001f,pw*.45f,nw*.4f,Vector3.up,S(ink,hot*alpha*.9f),Glowing);
                    prev=next;pw=nw;
                }
                for(int b=0;b<(extras?NativeZombieFx.Branches:1);b++)
                {
                    NativeZombieFx.CrackBranch(seed,i,count,b,since,p,out var ba,out var bc,out float bw);Vector3 a0=g+new Vector3(ba.X,.002f,ba.Y),c0=g+new Vector3(bc.X,.002f,bc.Y);
                    Strip(a0,c0,bw,bw*.15f,Vector3.up,S(0x07060a,alpha*.9f),Authored);if(hot>0)Strip(a0+Vector3.up*.001f,c0+Vector3.up*.001f,bw*.4f,bw*.06f,Vector3.up,S(ink,hot*alpha*.7f),Glowing);
                }
            }
        }
        /// <summary>A Hulk's footfall cracks round the foot (decals), `since` s after it landed.</summary>
        public void StompCracks(Vector3 at,float since,uint seed)
        {
            for(int k=0;k<N(NativeHordeFx.StompCracks);k++)if(NativeHordeFx.StompCrack(seed,k,since,out float ang,out float len,out float a))
            {var dir=new Vector3(Mathf.Cos(ang),0,Mathf.Sin(ang));var p0=at+dir*.18f+Vector3.up*.006f;Strip(p0,p0+dir*len,.032f,.004f,Vector3.up,S(0x07060a,a),Authored);}
        }
        /// <summary>A Hulk's footfall: toon dust kicked round the foot (`power` the kind's), `since` s after it landed.</summary>
        public void Footfall(Vector3 at,float since,float power,uint seed)
        {
            if(vfx==null)return;for(int k=0;k<N(NativeHordeFx.DustCount);k++)if(NativeHordeFx.Dust(seed,k,since,power,out var o,out float sz,out float al))vfx.Smoke(at+U(o),Dust,Math.Min(1,al*1.8f),sz*2.6f,Mathf.Clamp01(since/.5f),F(seed,k),at.y);
        }

        // ================================================================================================================
        // The wall breach (the wall's frame: `w` from Core NativeBreach.Wall; `face` the struck face's offset along the normal)
        // ================================================================================================================
        private static Vector3 W(in NativeBreach.Wall w,float x,float y,float z){var p=w.ToWorld(new NVector(x,y,z));return new Vector3(p.X,p.Y,p.Z);}
        private static Vector3 Dir(in NativeBreach.Wall w,Vector3 local)=>new Vector3(w.Right.X*local.x+w.Normal.X*local.z,local.y,w.Right.Z*local.x+w.Normal.Z*local.z);
        /// <summary>A crack line on a wall's face (local xy, `face` the face's offset along the normal), dark with a hot ink seam
        /// while `hot` (into the decal layer, so dust in front of it stays in front).</summary>
        public void WallCrack(in NativeBreach.Wall w,NVector2 a,NVector2 b,float width,float face,float alpha,float hot,int ink)
        {
            Vector3 p0=W(w,a.X,a.Y,face),p1=W(w,b.X,b.Y,face),n=new Vector3(w.Normal.X,w.Normal.Y,w.Normal.Z)*(face<0?-1:1);
            Strip(p0,p1,width,width*.2f,n,S(0x07060a,alpha),Authored);if(hot>0)Strip(p0+n*.002f,p1+n*.002f,width*.4f,width*.08f,n,S(ink,.55f*hot*alpha),Glowing);
        }
        /// <summary>Blow `blow` on a wall, `since` s ago, struck at the wall-local point (x, y) from the side `side` (+1 the arena
        /// face, -1 the far one): a flash, toon dust off the struck face and grit out of the far one, chips spalling to the floor.</summary>
        public void Knock(in NativeBreach.Wall w,int blow,float since,float x,float y,float floorLocal,int side,bool both,uint seed)
        {
            if(vfx==null||since<0||since>1.6f)return;float face=w.Thickness*.5f+.02f;var n=new Vector3(w.Normal.X,0,w.Normal.Z)*side;
            if(since<.1f)vfx.Glow(W(w,x,y,face*side)+n*.05f,Warm*(1.2f*(1-since/.1f)),.9f,NativeVfxDraw.CoreGlow,0,F(seed,blow));
            if(since<=1.1f)for(int d=0;d<N(6);d++)
            {
                float hx=NativeHordeFx.Hash(seed,blow*16+d,341)-.5f,hy=NativeHordeFx.Hash(seed,blow*16+d,342)-.5f,u=Mathf.Clamp01(since/1.1f);
                vfx.Vapor(W(w,x+hx*(.6f+1.2f*u),y+hy*.6f-u*u*1.2f,(face+.1f+.6f*u)*side),Dust,.7f*(1-Sm((u-.3f)/.7f)),.6f+1.1f*u,u,F(seed,blow*16+d),w.Bottom,0);
                if(both)vfx.Vapor(W(w,x+hx*1.1f,y+hy*.5f-u*u*1.8f,-(face+.06f+.35f*u)*side),DustDark,.55f*(1-Sm((u-.25f)/.75f)),.45f+.7f*u,u,F(seed,blow*16+d+8),w.Bottom,0);
            }
            float drop=Mathf.Max(.05f,y-floorLocal);var along=new Vector3(w.Right.X,0,w.Right.Z);
            for(int i=0;i<N(NativeZombieFx.SpallChips);i++)if(NativeZombieFx.Spall(seed,blow,i,since,drop,out var l,out float spin,out float size,out float left))
                vfx.Rubble(W(w,x,y,face*side)+along*l.X+Vector3.up*l.Y+n*l.Z,Quaternion.Euler(spin*40,spin*57,spin*23),size*left,(int)(seed%983)+blow*7+i,Rock,Color.clear);
        }
        /// <summary>The break, `since` s after it, struck at the wall-local point (x, y) on the arena face (+normal): the flash and dust
        /// burst out of the gap, grit thrown out and bouncing to the floor, sparks, the dust curtain off the gap's top edge (`top` the
        /// edge's local y, `width` the gap's), the dust rising off the foot (`footWidth` along it).</summary>
        public void Burst(in NativeBreach.Wall w,float since,float x,float y,float top,float width,float floorLocal,float footWidth,uint seed,bool block)
        {
            if(vfx==null||since<0)return;float face=w.Thickness*.5f+.02f;var n=new Vector3(w.Normal.X,0,w.Normal.Z);
            if(since<.16f){float k=1-since/.16f;vfx.Glow(W(w,x,y,face+.1f),Warm*(1.6f*k),3.2f*k+.6f,NativeVfxDraw.CoreGlow,0,F(seed,0));}
            if(since<1.6f)for(int d=0;d<N(16);d++)
            {
                float ang=d*2.39996f,rr=.3f+.7f*((d*37)%10)/10f,u=since/1.6f,out_=since*(3f+2f*rr)/(1+since*.8f);
                var p=W(w,x+Mathf.Cos(ang)*1.5f*rr*(block?1.2f:1)*(1+since*.6f),Mathf.Max(floorLocal+.35f,y+Mathf.Sin(ang)*1.1f*rr-since*.4f),face+.2f+out_);vfx.Smoke(p,Color.Lerp(Dust,DustDark,u*.7f+(d%3==0?.25f:0)),.92f*(1-Sm((u-.4f)/.6f)),(.9f+2.1f*Sm(u*1.4f))*(.75f+.25f*rr),u,F(seed,d),w.Bottom);
            }
            float drop=Mathf.Max(.1f,y-floorLocal);
            for(int i=0;i<N(NativeZombieFx.BreakChips);i++)if(NativeZombieFx.BreakChip(seed,i,since,drop,block?1.15f:1,out var l,out var axis,out float ang,out float size,out float left))
                vfx.Rubble(W(w,x,y,face)+Dir(w,U(l)),Quaternion.AngleAxis(ang*Mathf.Rad2Deg,Dir(w,U(axis))),size*left,(int)(seed%977)+i,Rock,Color.clear);
            if(extras)for(int i=0;i<NativeZombieFx.BreakSparks;i++)if(NativeZombieFx.BreakSpark(seed,i,since,out var l,out var v,out float heat))vfx.Streak(W(w,x,y,face)+Dir(w,U(l)),Dir(w,U(v)),SparkHot*(heat*heat),.02f,.05f,.03f,.85f,1-heat,F(seed,300+i));
            for(int i=0;i<N(NativeZombieFx.CurtainCount);i++)if(NativeZombieFx.Curtain(seed,i,since,width,top-floorLocal,out var l,out float s,out float a,out float u))
                vfx.Vapor(W(w,x+l.X,floorLocal+l.Y,face+l.Z),Dust,a*.7f,s*2.6f,u,F(seed,60+i),w.Bottom,0,2.2f);
            float rise=Mathf.Clamp01((since-.25f)/1.1f);int foot=Mathf.Clamp(Mathf.RoundToInt(footWidth/1.2f),2,12);
            if(rise>0&&rise<1)for(int d=0;d<N(foot);d++){float u=(d+.5f)/N(foot)*2-1,rr=(d*53)%10/10f;vfx.Vapor(W(w,u*footWidth*.5f*.95f,floorLocal+.3f+rise*.9f,face+rise*(1f+1.6f*rr)),Dust,.75f*(1-rise),(1.2f+2.4f*rise)*(.8f+.4f*rr),rise,F(seed,120+d),w.Bottom,0);}
        }
        /// <summary>The hardlight still tracing the gap while it waits to restore: `outline` (wall-local xy points round the gap,
        /// offsets from `origin`; both faces when `both`), glitching fragments along it, waking toward the restore.</summary>
        public void Remnants(in NativeBreach.Wall w,NVector2 origin,NVector2[] outline,int count,float floorLocal,float face,bool both,float sinceBreak,float untilRestore,uint seed)
        {
            if(vfx==null||count<2)return;float total=0;for(int i=0;i<count;i++)total+=(outline[(i+1)%count]-outline[i]).Length();if(total<1e-3f)return;
            for(int k=0;k<N(NativeZombieFx.Remnants);k++)
            {
                float on=NativeZombieFx.Remnant(seed,k,sinceBreak,untilRestore,out float start,out float length);if(on<=0)continue;
                NVector2 a=origin+Along(outline,count,total,start),b=origin+Along(outline,count,total,Mathf.Min(start+length,.9999f));a.Y=Mathf.Max(a.Y,floorLocal+.02f);b.Y=Mathf.Max(b.Y,floorLocal+.02f);
                for(int f=both?-1:1;f<=1;f+=2){Vector3 p0=W(w,a.X,a.Y,face*f),p1=W(w,b.X,b.Y,face*f);vfx.Streak(p1,p1-p0,Hardlight*(.8f*on),.024f,0,1,.15f,0,F(seed,k));vfx.Glow(p0,Hardlight*(.5f*on),.1f,NativeVfxDraw.Mote,0,F(seed,k));vfx.Glow(p1,Hardlight*(.35f*on),.08f,NativeVfxDraw.Mote,0,F(seed,k+20));}
            }
        }
        private static NVector2 Along(NVector2[] pts,int count,float total,float share)
        {
            float want=(share-Mathf.Floor(share))*total;for(int i=0;i<count;i++){var a=pts[i];var b=pts[(i+1)%count];float l=(b-a).Length();if(want<=l||i==count-1)return l>1e-5f?a+(b-a)*(Mathf.Min(want,l)/l):a;want-=l;}return pts[0];
        }
        /// <summary>Hardlight motes riding a piece home in the rebuild (`at` its world centre): every other piece on the full tier,
        /// every fourth on the others (a whole block has up to 96).</summary>
        public void Motes(Vector3 at,int piece,float back,float solid,float into,uint seed)
        {
            if(vfx==null||piece%(extras?2:4)!=0)return;for(int m=0;m<NativeZombieFx.Motes;m++)if(NativeZombieFx.Mote(seed,piece,m,back,solid,into,out var o,out float a))vfx.Glow(at+U(o),Hardlight*(.7f*a),.07f,NativeVfxDraw.Mote,0,F(seed,piece*4+m));
        }
    }
}
