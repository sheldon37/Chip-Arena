using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Meridian's street dress drawn (Core NativeMeridianDress plans it; docs/expedition/MERIDIAN-DRESS.md). With the city bank
    /// (Resources/ChipArenaCityMeridian, from the Drive zip) it:
    ///  - combines the planned modules and the generated surfaces into 32 m chunks at load: every chunk's LOD1 at once, each
    ///    chunk's LOD0 on a worker when the eye comes within reach (High 38 m, Balanced 24 m; Low draws LOD1 only), released again
    ///    past it; chunks past the fog's end plus 25 m are left out; ChipArena/StageLit, two materials shared by every chunk;
    ///  - lights the night (R4, the owner's lamp rule): the nearest working lamps take the eight practical slots (three on Low),
    ///    every working lamp lays its pool on the floor and, near the eye, a wet streak toward it (camera-facing FX quads, about
    ///    sixty, inside the 2,800 budget), flickering lamps' heads draw apart with their glow per frame, the nearest bowls throw
    ///    soft beams on High (NativeVolumetricBeams), and pilots and zombies take the lamps' light (ActorLights);
    ///  - tells the greybox which blocks it replaced (<see cref="Hides"/>).
    /// Without the bank (CI, cloud and public builds, `--ca-no-pack-banks`) nothing is made and the greybox draws as before.
    /// Presentation only. Allocation free per frame once prepared, but for a chunk's LOD0 build (on a worker) and upload.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class NativeMeridianDressDraw : MonoBehaviour
    {
        private static string activeMap;private static HashSet<string> activeCovers;private static int activeFrame=-10;
        /// <summary>Whether the dress drew this map block's place this frame (the greybox leaves its box out).</summary>
        public static bool Hides(string mapId,string blockId)=>blockId!=null&&activeCovers!=null&&mapId==activeMap&&Time.frameCount-activeFrame<=1&&activeCovers.Contains(blockId);

        private sealed class ChunkDraw{public int Index;public Bounds Box;public MeshRenderer Far,Near;public Mesh FarMesh,NearMesh;public Task<NativeMeridianMesh> Building;public int Shown=-1,NearTris,FarTris;}
        private NativeMeridianDress.Plan plan;private NativePackBank.Bank bank;private Dictionary<string,int[]> modules;private List<NativeMeridianDress.DrawChunk> units;
        private readonly List<ChunkDraw> chunks=new List<ChunkDraw>();private GameObject root;private Material solid,glow,poolMat;private Shader shader;private string mapId;private Camera eye;
        private MaterialPropertyBlock block;private Mesh poolMesh;private Vector3[] poolV;private Color32[] poolC;private Vector2[] poolUv;private int[] poolT;
        private readonly float[] slotPos=new float[NativeMapLooks.MaxLights*4],slotRgb=new float[NativeMapLooks.MaxLights*3];private readonly int[] nearest=new int[NativeMapLooks.MaxLights];
        private static readonly int GlowId=Shader.PropertyToID("_StageGlow");
        private NativeVolumetricBeams beams;private readonly NativeBeamList beamList=new NativeBeamList();private int building;
        /// <summary>Last frame: chunks drawn at LOD0 and LOD1, their triangles, lamps lit, pools and streaks drawn.</summary>
        public int NearChunks{get;private set;}public int FarChunks{get;private set;}public long Triangles{get;private set;}public int Pools{get;private set;}public int Streaks{get;private set;}
        public bool Ready=>plan!=null&&root!=null;
        public NativeMeridianDress.Plan Plan=>plan;
        /// <summary>Milliseconds the load spent planning and combining.</summary>
        public float PlanMs{get;private set;}public float CombineMs{get;private set;}

        /// <summary>Plans and combines the city (false without the bank: the greybox draws).</summary>
        public bool Prepare(NativeMeridianDress.Input input,Camera camera)
        {
            Release();if(input?.Map==null)return false;
            try
            {
                bank=NativePackBanks.Bank(NativeMeridianDress.Bank);if(bank==null)return false;
                // the bleed's themed variants (key@theme, in ChipArenaCityBleed) join the module table: ThemeAt picks them, and the plain
                // key draws where a theme has none
                bank=NativeMeridianDress.WithVariants(bank,NativePackBanks.Bank(NativeCityBleedDress.Bank));
                modules=NativeMeridianDress.Modules(bank);if(modules.Count==0)return false;
                shader=Resources.Load<Shader>("NativeStageLit");if(shader==null||!shader.isSupported)throw new InvalidOperationException("ChipArena/StageLit is missing or unsupported");
                var watch=System.Diagnostics.Stopwatch.StartNew();
                plan=NativeMeridianDress.Build(input,modules.ContainsKey);PlanMs=(float)watch.Elapsed.TotalMilliseconds;watch.Restart();
                units=NativeMeridianDress.DrawChunks(plan,bank,modules);
                solid=new Material(shader){name="Meridian dress",enableInstancing=true};solid.SetFloat("_Glow",0);
                glow=new Material(shader){name="Meridian dress glow",enableInstancing=true};glow.SetFloat("_Glow",1);block=new MaterialPropertyBlock();
                var pool=Resources.Load<Shader>("NativeFieldGlow");if(pool!=null){poolMat=new Material(pool){name="Meridian lamp pools",renderQueue=2990};poolMat.SetFloat("_Radial",1.6f);poolMat.SetFloat("_Offset",-2);poolMat.SetFloat("_Cull",0);}
                root=new GameObject("Meridian dress "+input.Map.id);mapId=input.Map.id;eye=camera;
                var data=new NativeMeridianMesh();
                for(int c=0;c<units.Count;c++)
                {
                    var u=units[c];NativeMeridianDress.Combine(plan,u.Cell,2,bank,modules,u.Placements,u.Quads,data);if(data.Vertices==0)continue;
                    var cd=new ChunkDraw{Index=c,FarMesh=Upload(data,"Meridian chunk "+c+" far"),FarTris=data.Triangles};cd.Box=cd.FarMesh.bounds;
                    cd.Far=Renderer(cd.FarMesh,"chunk "+c+" far");chunks.Add(cd);
                }
                CombineMs=(float)watch.Elapsed.TotalMilliseconds;
                activeCovers=plan.Covers;activeMap=mapId;
                Debug.Log(FormattableString.Invariant($"Meridian dress {mapId}: {plan.Placements.Count} modules, {plan.Quads.Count} surfaces, {plan.Lamps.Count} lamps ({plan.Unmounted} route points unmounted), {chunks.Count} chunks, {plan.Covers.Count} blocks covered; plan {PlanMs:F0} ms, LOD1 combine {CombineMs:F0} ms"));
                return true;
            }
            catch(Exception e){Debug.LogWarning("Meridian dress disabled: "+e);Release();return false;}
        }
        private MeshRenderer Renderer(Mesh mesh,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();
            r.sharedMaterials=mesh.subMeshCount>1?new[]{solid,glow}:new[]{solid};r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;r.lightProbeUsage=LightProbeUsage.Off;
            r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;r.enabled=false;return r;
        }
        private static Mesh Upload(NativeMeridianMesh d,string name)
        {
            int n=d.Vertices;var v=new Vector3[n];var nr=new Vector3[n];var a=new Vector2[n];var b=new Vector2[n];var c=new Color32[n];
            for(int i=0;i<n;i++){v[i]=new Vector3(d.P[i*3],d.P[i*3+1],d.P[i*3+2]);nr[i]=new Vector3(d.N[i*3],d.N[i*3+1],d.N[i*3+2]);a[i]=new Vector2(d.Uv0[i*2],d.Uv0[i*2+1]);b[i]=new Vector2(d.Uv1[i*2],d.Uv1[i*2+1]);c[i]=new Color32(d.C[i*4],d.C[i*4+1],d.C[i*4+2],d.C[i*4+3]);}
            var m=new Mesh{name=name};if(n>65535)m.indexFormat=IndexFormat.UInt32;m.vertices=v;m.normals=nr;m.uv=a;m.uv2=b;m.colors32=c;
            m.subMeshCount=d.Glow.Count>0?2:1;m.SetTriangles(d.Solid,0,false);if(d.Glow.Count>0)m.SetTriangles(d.Glow,1,false);
            m.bounds=new Bounds(new Vector3((d.MinX+d.MaxX)*.5f,(d.MinY+d.MaxY)*.5f,(d.MinZ+d.MaxZ)*.5f),new Vector3(d.MaxX-d.MinX,d.MaxY-d.MinY,d.MaxZ-d.MinZ));
            m.UploadMeshData(true);return m;
        }

        private void LateUpdate()
        {
            if(!Ready)return;var cam=eye!=null?eye:Camera.main;if(cam==null)return;var e=cam.transform.position;
            activeMap=mapId;activeCovers=plan.Covers;activeFrame=Time.frameCount;
            var tier=NativeAdvancedLighting.Tier;float reach=NativeMeridianDress.Reach(tier),far=NativeMeridianDress.Far(tier);
            int near=0,farN=0;long tris=0;
            foreach(var c in chunks)
            {
                float d=Mathf.Sqrt(c.Box.SqrDistance(e));int want=d<reach+(c.Shown==0?NativeMeridianDress.Hysteresis:0)?0:d<far?2:-1;
                if(want==0&&c.NearMesh==null)
                {
                    if(c.Building==null&&building<2){building++;var u=units[c.Index];c.Building=Task.Run(()=>{var m=new NativeMeridianMesh();NativeMeridianDress.Combine(plan,u.Cell,0,bank,modules,u.Placements,u.Quads,m);return m;});}
                    if(c.Building!=null&&c.Building.IsCompleted)
                    {
                        building--;var task=c.Building;c.Building=null;
                        if(task.Status==TaskStatus.RanToCompletion&&task.Result.Vertices>0){c.NearMesh=Upload(task.Result,"Meridian chunk "+c.Index+" near");c.NearTris=task.Result.Triangles;c.Near=Renderer(c.NearMesh,"chunk "+c.Index+" near");}
                    }
                    if(c.NearMesh==null)want=2;
                }
                else if(want!=0&&c.NearMesh!=null&&d>reach+30){Destroy(c.Near.gameObject);Destroy(c.NearMesh);c.Near=null;c.NearMesh=null;}
                if(c.Shown!=want){if(c.Far!=null)c.Far.enabled=want==2;if(c.Near!=null)c.Near.enabled=want==0;c.Shown=want;}
                if(want==0){near++;tris+=c.NearTris;}else if(want==2){farN++;tris+=c.FarTris;}
            }
            NearChunks=near;FarChunks=farN;Triangles=tris;
            Lights(e,cam,tier);
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The night's light: slots, pools, streaks, flickering heads, beams
        // ------------------------------------------------------------------------------------------------------------------
        /// <summary>Sodium (bible 3.1): the lamp #FFA14A, a route home's warmer #FF8E3A, linear, times the slot's intensity.</summary>
        private static readonly Color Sodium=new Color(1f,.631f,.29f),RouteSodium=new Color(1f,.557f,.227f);
        public const float SlotIntensity=1.35f,PoolAlpha=.34f,StreakAlpha=.26f,StreakReach=40f;
        /// <summary>A lamp's light this frame (0 dead .. 1): a flickering lamp stutters (at most three times a second, the
        /// photosensitivity rule), dropping out now and then.</summary>
        public static float Level(NativeMeridianDress.Lamp l,float time)
        {
            if(l.State==NativeMeridianDress.Dead)return 0;if(l.State==NativeMeridianDress.Working)return 1;
            float t=time*2.7f+l.Seed*17.3f;int k=(int)Mathf.Floor(t);float h=Mathf.Repeat(Mathf.Sin(k*12.9898f+l.Seed*78.233f)*43758.55f,1f);
            return h<.32f?.08f+.12f*h:h<.45f?.55f:1f;
        }
        private void Lights(Vector3 e,Camera cam,NativeGraphicsTier tier)
        {
            float time=Time.time;var lamps=plan.Lamps;int slots=tier==NativeGraphicsTier.Low?3:NativeMapLooks.MaxLights;
            // the nearest lit lamps take the slots (a lamp behind the eye only when nothing ahead is near)
            for(int s=0;s<nearest.Length;s++)nearest[s]=-1;var fwd=cam.transform.forward;
            for(int i=0;i<lamps.Count;i++)
            {
                var l=lamps[i];if(l.State==NativeMeridianDress.Dead)continue;var d=new Vector3(l.Bowl.x-e.x,l.Bowl.y-e.y,l.Bowl.z-e.z);float score=d.sqrMagnitude*(Vector3.Dot(d,fwd)<-l.Radius?3f:1f);
                for(int s=0;s<slots;s++)
                {
                    if(nearest[s]<0){nearest[s]=i;break;}
                    var m=lamps[nearest[s]];var dm=new Vector3(m.Bowl.x-e.x,m.Bowl.y-e.y,m.Bowl.z-e.z);float ms=dm.sqrMagnitude*(Vector3.Dot(dm,fwd)<-m.Radius?3f:1f);
                    if(score<ms){for(int k=slots-1;k>s;k--)nearest[k]=nearest[k-1];nearest[s]=i;break;}
                }
            }
            for(int s=0;s<NativeMapLooks.MaxLights;s++)
            {
                int i=s<slots?nearest[s]:-1;if(i<0){slotPos[s*4]=0;slotPos[s*4+1]=-10000;slotPos[s*4+2]=0;slotPos[s*4+3]=1;slotRgb[s*3]=slotRgb[s*3+1]=slotRgb[s*3+2]=0;continue;}
                var l=lamps[i];float k=Level(l,time)*SlotIntensity;var c=l.Protected?RouteSodium:Sodium;
                slotPos[s*4]=l.Bowl.x;slotPos[s*4+1]=l.Bowl.y-.2f;slotPos[s*4+2]=l.Bowl.z;slotPos[s*4+3]=l.Radius*1.25f;slotRgb[s*3]=c.r*k;slotRgb[s*3+1]=c.g*k;slotRgb[s*3+2]=c.b*k;
            }
            NativeLighting.PickedLights(slotPos,slotRgb);NativeLighting.ActorLights(true);
            DrawPools(e,time,tier);
            Heads(time);
            if(tier==NativeGraphicsTier.High)Beams(cam,time);
        }
        private void DrawPools(Vector3 e,float time,NativeGraphicsTier tier)
        {
            if(poolMat==null)return;var lamps=plan.Lamps;float far=NativeMeridianDress.Far(tier);int max=lamps.Count*2+8;
            if(poolV==null||poolV.Length<max*4){poolV=new Vector3[max*4];poolC=new Color32[max*4];poolUv=new Vector2[max*4];poolT=new int[max*6];poolMesh=new Mesh{name="Meridian pools"};poolMesh.MarkDynamic();}
            int q=0;Pools=Streaks=0;var right=Vector3.right;
            for(int i=0;i<lamps.Count;i++)
            {
                var l=lamps[i];float level=Level(l,time);if(level<=.01f)continue;var p=new Vector3(l.Pool.x,l.Pool.y+.03f,l.Pool.z);float d=Vector3.Distance(p,e);if(d>far)continue;
                var c=l.Protected?RouteSodium:Sodium;float r=l.Radius*.78f;
                Quad(ref q,p,new Vector3(r,0,0),new Vector3(0,0,r),c,PoolAlpha*level,-1,1,-1,1);Pools++;
                // the wet streak toward the eye: from under the lamp, laid on the street, about six metres long
                if(d<StreakReach&&tier!=NativeGraphicsTier.Low)
                {
                    var to=new Vector3(e.x-p.x,0,e.z-p.z);float tl=to.magnitude;if(tl<1.5f)continue;to/=tl;var across=new Vector3(to.z,0,-to.x);float len=Mathf.Min(6.5f,tl*.6f);
                    var mid=p+to*(len*.5f)+new Vector3(0,.01f,0);Quad(ref q,mid,across*.42f,to*(len*.5f),c,StreakAlpha*level,-1,1,-1,1);Streaks++;
                }
            }
            if(q==0)return;
            for(int k=q*4;k<poolV.Length;k++)poolV[k]=Vector3.zero;
            poolMesh.Clear(false);poolMesh.vertices=poolV;poolMesh.colors32=poolC;poolMesh.uv=poolUv;
            for(int k=0;k<poolT.Length;k++)poolT[k]=0;for(int k=0;k<q;k++){int v=k*4,t=k*6;poolT[t]=v;poolT[t+1]=v+2;poolT[t+2]=v+1;poolT[t+3]=v;poolT[t+4]=v+3;poolT[t+5]=v+2;}
            poolMesh.triangles=poolT;poolMesh.bounds=new Bounds(e,Vector3.one*far*2.2f);
            Graphics.DrawMesh(poolMesh,Matrix4x4.identity,poolMat,0,null,0,null,ShadowCastingMode.Off,false);
        }
        private void Quad(ref int q,Vector3 c,Vector3 a,Vector3 b,Color col,float alpha,float u0,float u1,float v0,float v1)
        {
            if((q+1)*4>poolV.Length)return;int v=q*4;var cc=new Color(col.r,col.g,col.b,alpha);
            Color32 c32=cc;poolV[v]=c-a-b;poolV[v+1]=c+a-b;poolV[v+2]=c+a+b;poolV[v+3]=c-a+b;for(int k=0;k<4;k++)poolC[v+k]=c32;
            poolUv[v]=new Vector2(u0,v0);poolUv[v+1]=new Vector2(u1,v0);poolUv[v+2]=new Vector2(u1,v1);poolUv[v+3]=new Vector2(u0,v1);q++;
        }
        /// <summary>The flickering lamps' heads (left out of the chunks): drawn at their places with their glow this frame.</summary>
        private void Heads(float time)
        {
            var lamps=plan.Lamps;
            for(int i=0;i<lamps.Count;i++)
            {
                var l=lamps[i];if(l.State!=NativeMeridianDress.Flicker||l.Placement<0)continue;var pl=plan.Placements[l.Placement];if(!modules.TryGetValue(pl.Key,out var props))continue;
                var d=new Vector3(l.Bowl.x,l.Bowl.y,l.Bowl.z)-(eye!=null?eye.transform.position:Vector3.zero);int tier=d.sqrMagnitude<38*38?0:2;var prop=bank.Props[props[tier]];
                var m=new Matrix4x4();m.m00=pl.M[0];m.m01=pl.M[1];m.m02=pl.M[2];m.m03=pl.M[3];m.m10=pl.M[4];m.m11=pl.M[5];m.m12=pl.M[6];m.m13=pl.M[7];m.m20=pl.M[8];m.m21=pl.M[9];m.m22=pl.M[10];m.m23=pl.M[11];m.m33=1;
                float k=Level(l,time);block.Clear();block.SetVector(GlowId,new Vector4(k,k,k,1));
                foreach(var part in prop.Parts)
                {
                    var mesh=NativePackBanks.Mesh(NativeMeridianDress.Bank,part.Mesh);if(mesh==null)continue;var w=m*NativePackBanks.Place(part.Place);
                    for(int s=0;s<mesh.subMeshCount;s++)Graphics.DrawMesh(mesh,w,s==0?solid:glow,0,null,s,block,s==0?ShadowCastingMode.On:ShadowCastingMode.Off,true);
                }
            }
        }
        /// <summary>High's lamp beams: the nearest lit bowls' soft cones down to their pools (our NativeVolumetricBeams; VLB only
        /// through the private add-on, which has no Meridian bridge yet).</summary>
        private void Beams(Camera cam,float time)
        {
            if(beams==null){beams=new NativeVolumetricBeams();}if(!beams.Ready)return;var lamps=plan.Lamps;
            for(int s=0;s<nearest.Length;s++)
            {
                int i=nearest[s];if(i<0)continue;var l=lamps[i];float k=Level(l,time);if(k<.05f)continue;var c=l.Protected?RouteSodium:Sodium;
                float h=Mathf.Max(1,l.Bowl.y-l.Pool.y);beamList.Add(l.Bowl.x,l.Bowl.y-.05f,l.Bowl.z,l.Pool.x,l.Pool.y+.1f,l.Pool.z,.16,h*.55,c.r*.22*k,c.g*.22*k,c.b*.22*k,.5,l.Seed);
            }
            beams.Draw(beamList,cam,2,3,time,0);
        }

        private void Release()
        {
            foreach(var c in chunks){if(c.FarMesh!=null)Destroy(c.FarMesh);if(c.NearMesh!=null)Destroy(c.NearMesh);}
            chunks.Clear();if(root!=null)Destroy(root);root=null;plan=null;
            if(solid!=null)Destroy(solid);if(glow!=null)Destroy(glow);if(poolMat!=null)Destroy(poolMat);if(poolMesh!=null)Destroy(poolMesh);solid=glow=poolMat=null;poolMesh=null;poolV=null;
            beams?.Dispose();beams=null;building=0;
            if(activeMap==mapId){activeCovers=null;activeMap=null;}
        }
        private void OnDestroy()=>Release();
    }
}
