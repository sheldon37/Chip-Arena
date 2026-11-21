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
    ///    past it; a 32 m cell's chunks switch together, on the cell's bounds, LOD0 once all of them have it (the surfaces' seams
    ///    conform within a cell at one tier); chunks past the fog's end plus 25 m are left out; ChipArena/StageLit, two materials
    ///    shared by every chunk;
    ///  - lights the night (R4, the owner's lamp rule; owner 2026-11-20: "the light doesn't seem to be on the material properly? (Like
    ///    this seems to be flat, in the air light)"): every working lamp lights the walls and floors round it through the city's lamp
    ///    grid (NativeCityLampLight: ChipArena/StageLit per pixel, on every tier; until then only the eight practical slots did, and every
    ///    other lamp showed a flat pool disc laid over the floor and floating over a sloped street); the nearest lamps take the slots for
    ///    the pilots and zombies (ActorLights), marked so StageLit takes them from the grid; near the eye a soft wet glint toward it on
    ///    level floors only (none on Low); flickering lamps' heads draw apart with their glow per frame; on High, where the camera draws
    ///    its depth, faint beams under the nearest swan-necks and pendants that fade into the floor and walls and are gone 30 m off;
    ///    DARKNESS puts the street lamps out (their light, glints, beams and glass);
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
        private readonly List<ChunkDraw> chunks=new List<ChunkDraw>();private GameObject root;private Material solid,glow,glintMat;private Shader shader;private string mapId;private Camera eye;
        private MaterialPropertyBlock block,darkBlock;private Mesh glintMesh;private Vector3[] glintV,glintUv;private Color[] glintC;private int[] glintT;private bool darkShown;
        private readonly float[] slotPos=new float[NativeMapLooks.MaxLights*4],slotRgb=new float[NativeMapLooks.MaxLights*3],slotBeam=new float[NativeMapLooks.MaxLights];private readonly int[] nearest=new int[NativeMapLooks.MaxLights];
        // the lamps' light (Core NativeMeridianDress.LightOf, in the plan's order) as the city's lamp grid holds it, and the floors under
        // the glints
        private NativeCityLamps.Lamp[] lampLight;private NativeCityLampLight.Block lampBlock;private NativeMeridianDress.Solids solids;
        private static readonly int GlowId=Shader.PropertyToID("_StageGlow");
        private NativeVolumetricBeams beams;private readonly NativeBeamList beamList=new NativeBeamList();private int building;
        // a 32 m cell's chunks (DrawChunks cuts a heavy cell for the caps) switch between LOD0 and LOD1 together, on the cell's whole
        // bounds, and LOD0 shows only once every one of them has its LOD0: the surfaces' seams conform within a cell at one tier
        // (NativeMeridianDress.SeamTable), so two chunks of one cell never draw at two tiers
        private Bounds[] cellBox;private float[] cellDist;private int[] cellWant,cellShown;private bool[] cellReady;
        /// <summary>Last frame: chunks drawn at LOD0 and LOD1, their triangles, lamps lit, the wet glints and the beams drawn.</summary>
        public int NearChunks{get;private set;}public int FarChunks{get;private set;}public long Triangles{get;private set;}public int LampsLit{get;private set;}public int Streaks{get;private set;}public int BeamsDrawn{get;private set;}
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
                var glint=Resources.Load<Shader>("NativeGlow");if(glint!=null)glintMat=new Material(glint){name="Meridian lamp glints"};
                root=new GameObject("Meridian dress "+input.Map.id);mapId=input.Map.id;eye=camera;
                // every lamp's light on the walls and floors round it (the city's lamp grid), and the floors the glints lie on
                solids=new NativeMeridianDress.Solids(input.Map.blocks);lampLight=NativeMeridianDress.LightsOf(plan);lampBlock=NativeCityLampLight.Add(mapId,lampLight);
                // every chunk's LOD1, combined a batch in parallel at a time and uploaded here in order
                NativeMeridianDress.CombineUnits(plan,units,2,bank,modules,(c,data)=>
                {
                    if(data.Vertices==0)return;
                    var cd=new ChunkDraw{Index=c,FarMesh=Upload(data,"Meridian chunk "+c+" far"),FarTris=data.Triangles};cd.Box=cd.FarMesh.bounds;
                    cd.Far=Renderer(cd.FarMesh,"chunk "+c+" far");chunks.Add(cd);
                });
                CombineMs=(float)watch.Elapsed.TotalMilliseconds;
                int cells=plan.Nx*plan.Nz;cellBox=new Bounds[cells];cellDist=new float[cells];cellWant=new int[cells];cellShown=new int[cells];cellReady=new bool[cells];var boxed=new bool[cells];
                foreach(var cd in chunks){int cell=units[cd.Index].Cell;if(boxed[cell])cellBox[cell].Encapsulate(cd.Box);else{cellBox[cell]=cd.Box;boxed[cell]=true;}}
                for(int i=0;i<cells;i++)cellShown[i]=-1;
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
            r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;r.enabled=false;if(darkShown)r.SetPropertyBlock(darkBlock);return r;
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
            // each cell's wish (on its whole bounds), then its chunks' LOD0 builds: the cell draws LOD0 once all of them have it
            foreach(var c in chunks)
            {
                int cell=units[c.Index].Cell;float d=Mathf.Sqrt(cellBox[cell].SqrDistance(e));cellDist[cell]=d;
                cellWant[cell]=d<reach+(cellShown[cell]==0?NativeMeridianDress.Hysteresis:0)?0:d<far?2:-1;cellReady[cell]=true;
            }
            foreach(var c in chunks)
            {
                int cell=units[c.Index].Cell;if(cellWant[cell]!=0||c.NearMesh!=null)continue;
                if(c.Building==null&&building<2){building++;var u=units[c.Index];c.Building=Task.Run(()=>{var m=new NativeMeridianMesh();NativeMeridianDress.Combine(plan,u.Cell,0,bank,modules,u.Placements,u.Quads,m);return m;});}
                if(c.Building!=null&&c.Building.IsCompleted)
                {
                    building--;var task=c.Building;c.Building=null;
                    if(task.Status==TaskStatus.RanToCompletion&&task.Result.Vertices>0){c.NearMesh=Upload(task.Result,"Meridian chunk "+c.Index+" near");c.NearTris=task.Result.Triangles;c.Near=Renderer(c.NearMesh,"chunk "+c.Index+" near");}
                }
                if(c.NearMesh==null)cellReady[cell]=false;
            }
            foreach(var c in chunks)
            {
                int cell=units[c.Index].Cell;int want=cellWant[cell];if(want==0&&!cellReady[cell])want=2;
                if(want!=0&&c.NearMesh!=null&&cellDist[cell]>reach+30){Destroy(c.Near.gameObject);Destroy(c.NearMesh);c.Near=null;c.NearMesh=null;}
                if(c.Shown!=want){if(c.Far!=null)c.Far.enabled=want==2;if(c.Near!=null)c.Near.enabled=want==0;c.Shown=want;}
                if(want==0){near++;tris+=c.NearTris;}else if(want==2){farN++;tris+=c.FarTris;}
                cellShown[cell]=want;
            }
            NearChunks=near;FarChunks=farN;Triangles=tris;
            Lights(e,cam,tier);
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The night's light: the lamps on the walls and floors (the city's lamp grid), the slots for the pilots and zombies, the wet
        // glints, the flickering heads, the beams, DARKNESS
        // ------------------------------------------------------------------------------------------------------------------
        /// <summary>Sodium (bible 3.1): the lamp #FFA14A, a route home's warmer #FF8E3A (linear; Core NativeMeridianDress.Sodium).</summary>
        private static readonly Color Sodium=new Color(1f,.631f,.29f),RouteSodium=new Color(1f,.557f,.227f);
        /// <summary>A slot that carries a city lamp to the pilots and zombies: StageLit takes it from the grid (NativeCityLamps.cginc).</summary>
        public const float CitySlot=-1f;
        /// <summary>The wet glint's opacity and reach; a beam's light (times the sodium) and where it fades out (m from the eye).</summary>
        public const float GlintAlpha=.2f,GlintReach=40f,BeamGain=.075f,BeamNear=16f,BeamFar=30f;
        /// <summary>A lamp's light this frame (0 dead .. 1): Core NativeMeridianDress.LampLevel (under 3 Hz; with Reduced effects a slow
        /// breath, never dark).</summary>
        public static float Level(NativeMeridianDress.Lamp l,float time)=>NativeMeridianDress.LampLevel(l,time,NativeAdvancedLighting.Reduced);
        private void Lights(Vector3 e,Camera cam,NativeGraphicsTier tier)
        {
            float time=Time.time;bool reduced=NativeAdvancedLighting.Reduced,dark=NativeLighting.MutatorDark;var lamps=plan.Lamps;int slots=tier==NativeGraphicsTier.Low?3:NativeMapLooks.MaxLights;
            // every lamp lights the walls and floors round it through the grid (NativeCityLampLight uploads after every drawer); DARKNESS
            // puts the street lamps out (R4: the grid failing)
            int lit=0;
            if(lampBlock!=null)for(int i=0;i<lamps.Count;i++){float k=dark?0:NativeMeridianDress.LampLevel(lamps[i],time,reduced);lampBlock.Level[i]=k;if(k>0)lit++;}
            LampsLit=lit;
            // the nearest lit lamps take the slots for the pilots and zombies (a lamp behind the eye only when nothing ahead is near),
            // marked so StageLit takes their light from the grid, never twice
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
                int i=s<slots?nearest[s]:-1;slotBeam[s]=0;if(i<0){slotPos[s*4]=0;slotPos[s*4+1]=-10000;slotPos[s*4+2]=0;slotPos[s*4+3]=1;slotRgb[s*3]=slotRgb[s*3+1]=slotRgb[s*3+2]=0;continue;}
                var g=lampLight[i];float k=dark?0:NativeMeridianDress.LampLevel(lamps[i],time,reduced);
                slotPos[s*4]=g.At.x;slotPos[s*4+1]=g.At.y;slotPos[s*4+2]=g.At.z;slotPos[s*4+3]=g.Range;slotRgb[s*3]=g.R*k;slotRgb[s*3+1]=g.G*k;slotRgb[s*3+2]=g.B*k;slotBeam[s]=CitySlot;
            }
            NativeLighting.PickedLights(slotPos,slotRgb,slotBeam);NativeLighting.ActorLights(true);
            Glints(e,time,tier,dark,reduced);
            Heads(time,reduced,dark);
            BeamsDrawn=0;if(tier==NativeGraphicsTier.High&&!dark)Beams(cam,time,reduced,e);
            Darkness(dark);
        }
        /// <summary>The wet street's glint toward the eye under each lamp within GlintReach (none on Low or in DARKNESS): an additive soft
        /// streak about six metres long, laid only where the floor under it is level (on a slope or a step a flat streak would stand in
        /// the air).</summary>
        private void Glints(Vector3 e,float time,NativeGraphicsTier tier,bool dark,bool reduced)
        {
            Streaks=0;if(glintMat==null||tier==NativeGraphicsTier.Low||dark)return;var lamps=plan.Lamps;int max=lamps.Count+4;
            if(glintV==null||glintV.Length<max*4)
            {
                glintV=new Vector3[max*4];glintC=new Color[max*4];glintUv=new Vector3[max*4];glintT=new int[max*6];
                for(int k=0;k<max;k++){int v=k*4,t=k*6;glintT[t]=v;glintT[t+1]=v+2;glintT[t+2]=v+1;glintT[t+3]=v;glintT[t+4]=v+3;glintT[t+5]=v+2;glintUv[v]=new Vector3(-1,-1,0);glintUv[v+1]=new Vector3(1,-1,0);glintUv[v+2]=new Vector3(1,1,0);glintUv[v+3]=new Vector3(-1,1,0);}
                if(glintMesh!=null)Destroy(glintMesh);glintMesh=new Mesh{name="Meridian lamp glints",indexFormat=max*4>65000?IndexFormat.UInt32:IndexFormat.UInt16};glintMesh.MarkDynamic();
            }
            int q=0;
            for(int i=0;i<lamps.Count;i++)
            {
                var l=lamps[i];if(l.State==NativeMeridianDress.Dead||l.Kind==NativeMeridianDress.Flush)continue;var p=new Vector3(l.Pool.x,l.Pool.y,l.Pool.z);
                var to=new Vector3(e.x-p.x,0,e.z-p.z);float tl=to.magnitude;if(tl>GlintReach||tl<1.5f)continue;
                float level=NativeMeridianDress.LampLevel(l,time,reduced);if(level<=.01f)continue;to/=tl;float len=Mathf.Min(6.5f,tl*.6f);
                var end=p+to*len;var half=p+to*(len*.5f);
                if(Mathf.Abs(solids.Floor(end.x,end.z,p.y+.3f)-p.y)>.03f||Mathf.Abs(solids.Floor(half.x,half.z,p.y+.3f)-p.y)>.03f)continue;
                var c=l.Protected?RouteSodium:Sodium;float a=GlintAlpha*level*(1-Mathf.SmoothStep(0,1,(tl-GlintReach*.6f)/(GlintReach*.4f)));
                var across=new Vector3(to.z,0,-to.x)*.42f;var along=to*(len*.5f);var mid=half+new Vector3(0,.02f,0);int v=q*4;var col=new Color(c.r,c.g,c.b,a);
                glintV[v]=mid-across-along;glintV[v+1]=mid+across-along;glintV[v+2]=mid+across+along;glintV[v+3]=mid-across+along;glintC[v]=glintC[v+1]=glintC[v+2]=glintC[v+3]=col;q++;
            }
            Streaks=q;if(q==0)return;
            for(int k=q*4;k<glintV.Length;k++){glintV[k]=Vector3.zero;glintC[k]=Color.clear;}
            glintMesh.vertices=glintV;glintMesh.colors=glintC;glintMesh.SetUVs(0,glintUv);if(glintMesh.GetIndexCount(0)==0)glintMesh.triangles=glintT;glintMesh.bounds=new Bounds(e,Vector3.one*GlintReach*2.4f);
            Graphics.DrawMesh(glintMesh,Matrix4x4.identity,glintMat,0,null,0,null,ShadowCastingMode.Off,false);
        }
        /// <summary>The flickering lamps' heads (left out of the chunks): drawn at their places with their glow this frame (dark glass in
        /// DARKNESS).</summary>
        private void Heads(float time,bool reduced,bool dark)
        {
            var lamps=plan.Lamps;
            for(int i=0;i<lamps.Count;i++)
            {
                var l=lamps[i];if(l.State!=NativeMeridianDress.Flicker||l.Placement<0)continue;var pl=plan.Placements[l.Placement];if(!modules.TryGetValue(pl.Key,out var props))continue;
                var d=new Vector3(l.Bowl.x,l.Bowl.y,l.Bowl.z)-(eye!=null?eye.transform.position:Vector3.zero);int tier=d.sqrMagnitude<38*38?0:2;var prop=bank.Props[props[tier]];
                var m=new Matrix4x4();m.m00=pl.M[0];m.m01=pl.M[1];m.m02=pl.M[2];m.m03=pl.M[3];m.m10=pl.M[4];m.m11=pl.M[5];m.m12=pl.M[6];m.m13=pl.M[7];m.m20=pl.M[8];m.m21=pl.M[9];m.m22=pl.M[10];m.m23=pl.M[11];m.m33=1;
                float k=dark?0:NativeMeridianDress.LampLevel(l,time,reduced);block.Clear();block.SetVector(GlowId,new Vector4(k,k,k,1));
                foreach(var part in prop.Parts)
                {
                    var mesh=NativePackBanks.Mesh(NativeMeridianDress.Bank,part.Mesh);if(mesh==null)continue;var w=m*NativePackBanks.Place(part.Place);
                    for(int s=0;s<mesh.subMeshCount;s++)Graphics.DrawMesh(mesh,w,s==0?solid:glow,0,null,s,block,s==0?ShadowCastingMode.On:ShadowCastingMode.Off,true);
                }
            }
        }
        /// <summary>High's lamp beams, only where the camera draws its depth (a beam then fades into the floor and walls it meets;
        /// without it the cone would cut them as a flat sheet): the nearest lit swan-necks' and pendants' faint cones down to their
        /// pools (none from a flush lamp low on its wall), a little fainter seen side on, gone BeamFar from the eye (our
        /// NativeVolumetricBeams; VLB only through the private add-on, which has no Meridian bridge yet).</summary>
        private void Beams(Camera cam,float time,bool reduced,Vector3 e)
        {
            if((cam.depthTextureMode&DepthTextureMode.Depth)==0)return;
            if(beams==null){beams=new NativeVolumetricBeams();}if(!beams.Ready)return;var lamps=plan.Lamps;
            for(int s=0;s<nearest.Length;s++)
            {
                int i=nearest[s];if(i<0)continue;var l=lamps[i];if(l.Kind==NativeMeridianDress.Flush)continue;float k=NativeMeridianDress.LampLevel(l,time,reduced);if(k<.05f)continue;
                var top=new Vector3(l.Bowl.x,l.Bowl.y-.05f,l.Bowl.z);var foot=new Vector3(l.Pool.x,l.Pool.y+.1f,l.Pool.z);var mid=(top+foot)*.5f;
                float fade=1-Mathf.SmoothStep(0,1,(Vector3.Distance(mid,e)-BeamNear)/(BeamFar-BeamNear));if(fade<=.01f)continue;
                var axis=(foot-top).normalized;var view=(mid-e).normalized;fade*=Mathf.Lerp(.55f,1f,Mathf.Abs(Vector3.Dot(axis,view)));
                var c=l.Protected?RouteSodium:Sodium;float h=Mathf.Max(1,l.Bowl.y-l.Pool.y),g=BeamGain*k*fade;
                if(beamList.Add(top.x,top.y,top.z,foot.x,foot.y,foot.z,.2,h*.4,c.r*g,c.g*g,c.b*g,.35,l.Seed))BeamsDrawn++;
            }
            beams.Draw(beamList,cam,3,3,time,0);
        }
        /// <summary>DARKNESS: the lamps' glass and the lit windows down to NativeMutatorLook.DarkGlow (their light is out through the grid
        /// and the slots); back as baked when it lifts. Written only when it changes.</summary>
        private void Darkness(bool dark)
        {
            if(dark==darkShown)return;darkShown=dark;darkBlock??=new MaterialPropertyBlock();darkBlock.Clear();
            if(dark){float g=NativeMutatorLook.DarkGlow;darkBlock.SetVector(GlowId,new Vector4(g,g,g,1));}
            foreach(var c in chunks){if(c.Far!=null)c.Far.SetPropertyBlock(dark?darkBlock:null);if(c.Near!=null)c.Near.SetPropertyBlock(dark?darkBlock:null);}
        }

        private void Release()
        {
            foreach(var c in chunks){if(c.FarMesh!=null)Destroy(c.FarMesh);if(c.NearMesh!=null)Destroy(c.NearMesh);}
            chunks.Clear();if(root!=null)Destroy(root);root=null;plan=null;
            NativeCityLampLight.Remove(lampBlock);lampBlock=null;lampLight=null;solids=null;darkShown=false;
            if(solid!=null)Destroy(solid);if(glow!=null)Destroy(glow);if(glintMat!=null)Destroy(glintMat);if(glintMesh!=null)Destroy(glintMesh);solid=glow=glintMat=null;glintMesh=null;glintV=null;
            beams?.Dispose();beams=null;building=0;
            if(activeMap==mapId){activeCovers=null;activeMap=null;}
        }
        private void OnDestroy()=>Release();
    }
}
