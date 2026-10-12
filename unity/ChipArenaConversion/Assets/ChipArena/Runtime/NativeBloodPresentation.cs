using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Blood that falls and stays (Core/NativeBlood), presentation only. Each replicated wound on a presented pilot sprays its
    /// droplets once (LivingWounds; a death's fatal wound from the corpse and the melee slice's extra blood; the kill
    /// effects' stump fountains add theirs where their drops land). The drops fly on the field clock, drawn as the toon
    /// sheet's blended teardrops in the shared effects renderer, and are swept against the arena's own colliders (floors,
    /// walls, props, Brickfall blocks; never pilots, and a dynamic crate or fracture fragment takes no stain). Splats are one
    /// premultiplied mesh on NativeSirenFx (flat, authored colour), lifted <see cref="NativeBloodRules.Lift"/> off their
    /// surface (no z-fighting, depth write off so splats never fight each other either), lit by the scene's ambient and sun
    /// like the destruction decals, rebuilt only when the set changes, while one spreads or fades, and every half second as
    /// they dry. A splat on a collider that is destroyed, disabled or moved fades with it. They stay through the killcam
    /// (ResetPresentation(true)) and clear with the round: a new generation or full reset, and the moment the corpse
    /// lifecycle wipes the bodies (NativeCorpseRules.RoundEnded), which this does not change.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        private readonly NativeBloodSim blood=new NativeBloodSim();
        // Wounds whose drops may stain (once per round, across the killcam's replay of them).
        private readonly HashSet<long> stainedWounds=new HashSet<long>();
        private struct BloodSurface{public Collider Collider;public Vector3 Position;public Quaternion Rotation;}
        private readonly Dictionary<int,BloodSurface> bloodSurfaces=new Dictionary<int,BloodSurface>();private readonly List<int> staleSurfaces=new List<int>();
        private NativeBloodSim.Cast bloodCast;private NativeArena bloodArena;private float bloodBuilt=-100;private int bloodVersion=-1;
        private Mesh splatMesh;private Material splatMaterial;private bool splatFailed;
        private readonly List<Vector3> splatPoints=new List<Vector3>(),splatNormals=new List<Vector3>();private readonly List<Color> splatColors=new List<Color>();
        private readonly List<Vector4> splatUv=new List<Vector4>();private readonly List<int> splatIndices=new List<int>();
        private readonly Vector2[] splatRing=new Vector2[12];
        private static readonly RaycastHit[] bloodHits=new RaycastHit[8];
        /// <summary>Live droplets and splats (the checks and the dev overlay).</summary>
        public int BloodDrops=>blood.Flying;public int BloodSplats=>blood.Splats.Live;

        private static long WoundKey(int pilot,NativeWound w)=>((long)(pilot&0xffff)<<40)^((long)(w.id&0xffffff)<<12)^(long)(BitConverter.SingleToInt32Bits(w.at)&0xfff);
        private float BloodQuality=>Mathf.Clamp01(EffectsQuality);
        /// <summary>Sprays a wound's blood (Core/NativeBloodRules.Count by kind) from `origin` out of `normal` along `along`
        /// (Unity world). `extra`: a slice's added gush. Drops of a wound already stained this round fly but leave no splat.</summary>
        private void Bleed(int pilot,NativeWound w,Vector3 origin,Vector3 normal,Vector3 along,float time,int extra=0,bool kill=false,string kind=null)
        {
            // A death's own gush stains apart from the same wound's spray on the living rig in its last frame.
            if(w==null)return;long key=WoundKey(pilot,w)^(kill?1L<<61:0);bool stain=stainedWounds.Add(key);
            uint seed=NativeWoundCarve.Seed(w.id.ToString(System.Globalization.CultureInfo.InvariantCulture))^0xb100du;
            blood.Spray(kind??w.kind,w.severity,seed,key,new NVector(origin.x,origin.y,origin.z),new NVector(normal.x,normal.y,normal.z),new NVector(along.x,along.y,along.z),time,BloodQuality,LowGraphics,stain,extra,pilot,kill);
        }
        private void ClearBlood(bool splats)
        {
            blood.Clear(splats);if(splats){stainedWounds.Clear();bloodSurfaces.Clear();bloodVersion=-1;}
        }
        // The arena sweep: the first world collider between two points; a dynamic crate or fragment (NativeStructureFxPresentation
        // Movable) takes no stain.
        private bool BloodCast(NVector from,NVector to,out NVector point,out NVector normal,out int surface)
        {
            point=normal=default;surface=0;var arena=bloodArena;if(arena?.Root==null)return false;
            Vector3 a=new Vector3(from.X,from.Y,from.Z),b=new Vector3(to.X,to.Y,to.Z),d=b-a;float l=d.magnitude;if(l<1e-5f)return false;
            int n=arena.Root.scene.GetPhysicsScene().Raycast(a,d/l,bloodHits,l,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);if(n<=0)return false;
            int best=0;for(int i=1;i<n;i++)if(bloodHits[i].distance<bloodHits[best].distance)best=i;var hit=bloodHits[best];
            point=new NVector(hit.point.x,hit.point.y,hit.point.z);normal=new NVector(hit.normal.x,hit.normal.y,hit.normal.z);
            var prop=hit.collider.GetComponent<NativePropBody>();if(prop!=null&&(prop.Fragment||prop.Definition!=null&&prop.Definition.dynamic))return true;
            // A splat near the surface's edge would hang over it: it needs support under its centre and a little round it.
            if(!Supported(arena,hit.point,hit.normal,hit.collider))return true;
            surface=hit.collider.GetInstanceID();if(surface==0)surface=1;
            if(!bloodSurfaces.ContainsKey(surface))bloodSurfaces[surface]=new BloodSurface{Collider=hit.collider,Position=hit.collider.transform.position,Rotation=hit.collider.transform.rotation};
            return true;
        }
        private static bool Supported(NativeArena arena,Vector3 at,Vector3 normal,Collider on)
        {
            Vector3 u=Vector3.Cross(normal,Mathf.Abs(normal.y)<.9f?Vector3.up:Vector3.right).normalized,v=Vector3.Cross(normal,u);var scene=arena.Root.scene.GetPhysicsScene();
            for(int k=0;k<4;k++)
            {
                Vector3 o=at+(k<2?u:v)*((k&1)==0?.04f:-.04f)+normal*.05f;
                if(!scene.Raycast(o,-normal,out var hit,.1f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))return false;
            }
            return true;
        }
        /// <summary>RenderGlow, after the other effects: steps the drops, fades splats whose surface went, queues the drops into
        /// the shared effects renderer and draws the splats.</summary>
        private void BloodFx(NativeArena arena,Camera camera,float now)
        {
            if(arena==null)return;
            // A new round reaches here through ResetPresentation (ClearBlood); the killcam's replay arena sweeps its own copy of
            // the map while the live round's splats stay.
            bloodArena=arena;bloodCast??=BloodCast;float q=BloodQuality;
            blood.Splats.Limit=LowGraphics?160:q<1?240:NativeBloodSplats.Kept;
            blood.Step(now,bloodCast,-60);blood.Splats.Update(now);
            // Surfaces that broke, were switched off or moved take their splats with them.
            staleSurfaces.Clear();
            foreach(var pair in bloodSurfaces)
            {
                var s=pair.Value;bool gone=s.Collider==null||!s.Collider.enabled;
                bool moved=!gone&&((s.Collider.transform.position-s.Position).sqrMagnitude>1e-6f||Quaternion.Angle(s.Collider.transform.rotation,s.Rotation)>.1f);
                if(gone||moved){blood.Splats.FadeSurface(pair.Key,now);staleSurfaces.Add(pair.Key);}
            }
            foreach(int id in staleSurfaces)bloodSurfaces.Remove(id);
            if(vfx!=null)
            {
                var drops=blood.Drops;
                for(int i=0;i<drops.Length;i++)
                {
                    var d=drops[i];if(!d.Live||now-d.Born<d.Delay)continue;float shade=NativeBloodRules.Hash(d.Seed,0,3);
                    var c=NativeVfxDraw.Linear(d.Ink?(shade<.6f?NativeBloodRules.InkDrop:NativeBloodRules.InkDeep):shade<.6f?NativeBloodRules.DropColor:NativeBloodRules.DropDeep);c.a=.92f;
                    vfx.Blood(new Vector3(d.Position.X,d.Position.Y,d.Position.Z),new Vector3(d.Velocity.X,d.Velocity.Y,d.Velocity.Z),c,d.Size,d.Seed&0xffff);
                }
            }
            DrawSplats(camera,now);
        }
        private Color splatUp,splatSide,splatDown;
        private void SplatLight()
        {
            var sun=RenderSettings.sun;Vector3 l=sun!=null?-sun.transform.forward:new Vector3(.35f,.85f,.55f).normalized;Color key=sun!=null&&sun.isActiveAndEnabled?sun.color.linear*sun.intensity:Color.white*.8f;
            bool tri=RenderSettings.ambientMode==AmbientMode.Trilight;Color sky=(tri?RenderSettings.ambientSkyColor:RenderSettings.ambientLight).linear,mid=(tri?RenderSettings.ambientEquatorColor:RenderSettings.ambientLight).linear,low=(tri?RenderSettings.ambientGroundColor:RenderSettings.ambientLight).linear;
            Color Lit(Vector3 n,Color amb){var c=amb+key*Mathf.Max(0,Vector3.Dot(n,l));return new Color(Mathf.Clamp01(Mathf.LinearToGammaSpace(c.r)),Mathf.Clamp01(Mathf.LinearToGammaSpace(c.g)),Mathf.Clamp01(Mathf.LinearToGammaSpace(c.b)),1);}
            Vector3 flat=new Vector3(l.x,0,l.z);splatUp=Lit(Vector3.up,sky);splatSide=Lit(flat.sqrMagnitude>1e-4f?flat.normalized*.7f:Vector3.forward,mid);splatDown=Lit(Vector3.down,low);
        }
        private bool EnsureSplatMaterial()
        {
            if(splatMaterial!=null)return true;if(splatFailed)return false;var shader=Resources.Load<Shader>("NativeSirenFx");if(shader==null||!shader.isSupported){splatFailed=true;return false;}
            // Premultiplied, no depth write, before the other transparent effects (smoke and glows draw over the floor's blood).
            splatMaterial=new Material(shader){name="Blood splats",renderQueue=(int)RenderQueue.Transparent-60};splatMaterial.SetOverrideTag("RenderType","Transparent");
            splatMaterial.SetFloat("_Src",(float)BlendMode.One);splatMaterial.SetFloat("_Dst",(float)BlendMode.OneMinusSrcAlpha);splatMaterial.SetFloat("_ZWrite",0);
            splatMesh=new Mesh{name="Blood splats",indexFormat=IndexFormat.UInt32};splatMesh.MarkDynamic();return true;
        }
        private void DrawSplats(Camera camera,float now)
        {
            var pool=blood.Splats;if(pool.Live==0){if(splatMesh!=null&&splatMesh.vertexCount>0)splatMesh.Clear();return;}if(!EnsureSplatMaterial())return;
            // Rebuild while any splat spreads in or fades, when the set changed, and every half second as they dry.
            bool animating=false;for(int i=0;i<NativeBloodSplats.Capacity&&!animating;i++){var s=pool.Items[i];animating=s.Live&&(now-s.Born<.08f||s.FadeAt>=0);}
            if(animating||pool.Version!=bloodVersion||now-bloodBuilt>.5f||now<bloodBuilt)
            {
                bloodVersion=pool.Version;bloodBuilt=now;SplatLight();splatPoints.Clear();splatNormals.Clear();splatColors.Clear();splatUv.Clear();splatIndices.Clear();bool low=LowGraphics;
                for(int i=0;i<NativeBloodSplats.Capacity;i++){var s=pool.Items[i];if(!s.Live)continue;float a=pool.Opacity(i,now);if(a>.002f)Splat(s,a,now,low);}
                splatMesh.Clear();if(splatPoints.Count>0){splatMesh.SetVertices(splatPoints);splatMesh.SetNormals(splatNormals);splatMesh.SetColors(splatColors);splatMesh.SetUVs(0,splatUv);splatMesh.SetTriangles(splatIndices,0,false);splatMesh.bounds=new Bounds(Vector3.zero,Vector3.one*4000);}
            }
            if(splatMesh.vertexCount>0)Graphics.DrawMesh(splatMesh,Matrix4x4.identity,splatMaterial,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        private static readonly Vector4 SplatUv=new Vector4(0,0,NativeFxDraw.Flat,NativeFxDraw.Authored);
        // One splat: an irregular toon blob stretched along its travel (a dark rim, a wetter core pushed toward where it slid),
        // a few satellite droplets thrown ahead and a thin tail; the low tier draws the blob alone.
        private void Splat(in NativeBloodSplats.Splat s,float alpha,float now,bool low)
        {
            Vector3 n=new Vector3(s.Normal.X,s.Normal.Y,s.Normal.Z),o=new Vector3(s.Center.X,s.Center.Y,s.Center.Z),u=new Vector3(s.Along.X,s.Along.Y,s.Along.Z);
            u=(u-n*Vector3.Dot(u,n)).normalized;if(u.sqrMagnitude<.5f)u=Vector3.Cross(n,Vector3.right).normalized;Vector3 v=Vector3.Cross(n,u);
            float dry=NativeBloodRules.Darken(now-s.Born);Color light=n.y>.5f?splatUp:n.y<-.5f?splatDown:splatSide;
            Color Tone(int hex,float k){var c=NativeArena.ColorOf(hex);return new Color(c.r*k*light.r,c.g*k*light.g,c.b*k*light.b,alpha);}
            Color rim=Tone(s.Ink?NativeBloodRules.InkRim:NativeBloodRules.Rim,dry*.95f),core=Tone(s.Ink?NativeBloodRules.InkCore:NativeBloodRules.Core,dry*(.75f+.25f*dry));
            float L=s.Length,W=s.Width;uint seed=s.Seed;float H(int k)=>NativeBloodRules.Hash(seed,k,47);
            // The blob: 12 lobes, its front (along u) pushed out into a teardrop.
            for(int k=0;k<splatRing.Length;k++)
            {
                float ang=k*Mathf.PI*2/splatRing.Length,c=Mathf.Cos(ang),sn=Mathf.Sin(ang),r=.82f+.3f*H(k)+(k%3==0?.1f:0);
                splatRing[k]=new Vector2(c*L*r*(c>0?1.12f:.92f),sn*W*r);
            }
            Fan(o,u,v,n,Vector2.zero,1,rim);
            if(!low)Fan(o+n*.0005f,u,v,n,new Vector2(L*.12f,0),.58f,core);
            if(low)return;
            // Satellites ahead and to the sides, and a thin tail behind.
            int sats=2+(int)(H(20)*3);
            for(int k=0;k<sats;k++)
            {
                float along=L*(1.15f+.9f*H(30+k)),side=W*(H(40+k)-.5f)*1.4f,r=W*(.12f+.16f*H(50+k));
                Blob(o+n*.0003f,u,v,n,new Vector2(along,side),r,r*(1+.6f*H(60+k)),rim,6);
            }
            if(L>W*1.3f)Blob(o+n*.0002f,u,v,n,new Vector2(-L*.95f,W*(H(70)-.5f)*.3f),W*.12f,L*.35f,rim,6,true);
        }
        private void Fan(Vector3 o,Vector3 u,Vector3 v,Vector3 n,Vector2 offset,float scale,Color c)
        {
            int start=splatPoints.Count;Vector3 centre=o+u*offset.x+v*offset.y;Add(centre,n,c);
            for(int k=0;k<splatRing.Length;k++){var p=splatRing[k]*scale;Add(centre+u*p.x+v*p.y,n,c);}
            for(int k=0;k<splatRing.Length;k++){splatIndices.Add(start);splatIndices.Add(start+1+k);splatIndices.Add(start+1+(k+1)%splatRing.Length);}
        }
        private void Blob(Vector3 o,Vector3 u,Vector3 v,Vector3 n,Vector2 at,float rv,float ru,Color c,int sides,bool tail=false)
        {
            int start=splatPoints.Count;Vector3 centre=o+u*at.x+v*at.y;Add(centre,n,c);
            for(int k=0;k<sides;k++){float ang=k*Mathf.PI*2/sides;Add(centre+u*(Mathf.Cos(ang)*ru)+v*(Mathf.Sin(ang)*rv*(tail&&Mathf.Cos(ang)<0?.4f:1)),n,c);}
            for(int k=0;k<sides;k++){splatIndices.Add(start);splatIndices.Add(start+1+k);splatIndices.Add(start+1+(k+1)%sides);}
        }
        private void Add(Vector3 p,Vector3 n,Color c){splatPoints.Add(p);splatNormals.Add(n);splatColors.Add(c);splatUv.Add(SplatUv);}
        // A corpse's stump fountain (NativeKillSpray) splats where its drops landed: every third landing stays as a splat.
        private void KillSplats(NativeCorpse corpse,NativeKillSpray spray,int index,float now)
        {
            if(spray==null||spray.Pools<=0||bloodArena==null)return;int seen=corpse.SplatsSeen(index),landed=spray.Splatted;
            for(int k=Math.Max(seen,landed-spray.Pools);k<landed;k++)
            {
                if(k%3!=0)continue;int p=k%spray.Pools;Vector3 at=new Vector3(-(float)spray.PoolPos[p*3],(float)spray.PoolPos[p*3+1],(float)spray.PoolPos[p*3+2]);
                if(!BloodCast(new NVector(at.x,at.y+.3f,at.z),new NVector(at.x,at.y-.4f,at.z),out var point,out var normal,out int surface)||surface==0)continue;
                float w=Mathf.Clamp((float)spray.PoolScale[p*2]*.42f,.05f,NativeBloodRules.MaxWidth),l=Mathf.Clamp((float)spray.PoolScale[p*2+1]*.5f+w*.3f,w,NativeBloodRules.MaxLength);
                float rz=(float)spray.PoolRz[p];long key=((long)(corpse.Death.id&0xffff)<<40)^((long)(corpse.Death.seed&0xffff)<<20)^((long)index<<16)^k^(1L<<62);
                blood.Splats.Add(key,point+normal*NativeBloodRules.Lift,normal,new NVector(Mathf.Cos(rz),0,Mathf.Sin(rz)),l,w,(uint)(corpse.Death.seed*31+k),surface,now);
            }
            corpse.SawSplats(index,landed);
        }
    }
}
