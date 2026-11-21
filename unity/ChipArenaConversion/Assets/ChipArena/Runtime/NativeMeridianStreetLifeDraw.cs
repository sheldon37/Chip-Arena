using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Meridian's street life drawn (Core NativeMeridianStreetLife plans it; docs/expedition/MERIDIAN-STREETS.md), from its bank
    /// (Resources/ChipArenaCityStreets, from the Drive zip):
    ///  - the pieces: every kind instanced (ChipArena/StageLit, <see cref="Lift"/> layers up, over every layer the street dress uses, as
    ///    the bleed's; the plan keeps its paint off the bleed's and the dress's own), each piece's tier by its distance (NativeStageArt
    ///    .TierAt: LOD0 within 38 m, LOD1 beyond, LOD1 only on the Low tier), culled by 32 m cell and against the eye's frustum, each
    ///    mount out to its own reach (litter is not drawn 100 m away), at most 500 to a draw; each instance's glow (a sign, a signal, a
    ///    fire, a flare, a flood, a festoon at its state and level: NativeMeridianStreetLife.Level) and tint (graffiti, chalk,
    ///    canvas, banners) set per draw;
    ///  - the light: every lit piece lights the walls and floors round it through the city's lamp grid (NativeCityLampLight; a flood
    ///    as a cone down its aim over the plaza, a sign's spill on its wall, a fire's and a flare's glow), on every tier (2026-11-21:
    ///    a flat pool disc under each read as light laid over the floor, not on it); near the eye a halo at its head, flames over the
    ///    fires and the flares; steam rising from the grates. Reduced effects slow every flicker under 3 Hz (the Core's Level) and
    ///    halve the steam and the flames; the Low tier drops halos, flames and steam.
    /// Without the bank (CI, a build without the Drive zip, --ca-no-pack-banks, --ca-no-city-streets) <see cref="Open"/> returns null
    /// and nothing is drawn. Never a collider, a rule or a record. Allocation free per frame once open.
    /// </summary>
    public sealed class NativeMeridianStreetLifeDraw : MonoBehaviour
    {
        public const string OffArgument="--ca-no-city-streets";
        /// <summary>The layers the pieces draw up (ChipArena/StageLit's _StageLift): over the street dress's (up to 8), as the bleed's.</summary>
        public const int Lift=9;
        private const int BatchSize=500;
        /// <summary>How far each mount draws (m, High and Balanced; the Low tier 0.7 of it): litter and paint go first, the lit and the
        /// overhead pieces carry furthest.</summary>
        public static float ReachOf(NativeMeridianStreetLife.Mount m,bool lit)=>lit?175f:m==NativeMeridianStreetLife.Mount.Floor?85f:m==NativeMeridianStreetLife.Mount.Wall||m==NativeMeridianStreetLife.Mount.Low?110f:150f;
        /// <summary>The Low tier's share of a mount's reach (the overhead pieces go first: the fog ends at 100 m there).</summary>
        public static float LowShare(NativeMeridianStreetLife.Mount m,bool lit)=>lit||m==NativeMeridianStreetLife.Mount.Floor||m==NativeMeridianStreetLife.Mount.Wall||m==NativeMeridianStreetLife.Mount.Low?.7f:.55f;
        /// <summary>The light's effects' reach (m): halos, flames and steam (the light itself is the grid's, everywhere).</summary>
        public const float FxReach=70f,SteamReach=45f;

        private sealed class Kind{public Mesh Near,Far;public bool Casts;public float Reach,Low;}
        private NativeMeridianStreetLife.Plan plan;private Camera eye;private Kind[] kinds;
        private Matrix4x4[] place;private Bounds[] box;private Vector4[] tint;private Vector4[] lightOf;
        private Material solid,glow,haloMat,steamMat;private MaterialPropertyBlock block;
        private readonly Plane[] planes=new Plane[6];
        // the per-frame sort: each visible piece's key (kind x 2 + tier) and its slot, counted then placed
        private int[] visible,keyOf,counts,starts,order;private int visibleCount;
        private readonly Matrix4x4[] batchM=new Matrix4x4[BatchSize];private readonly Vector4[] batchGlow=new Vector4[BatchSize],batchTint=new Vector4[BatchSize];
        // the lights: each lit piece's head, its floor, its pool's centre and radius (Core kinds' Head and Pool)
        private int[] lit;private Vector3[] heads,poolAt;private float[] poolR;private Color[] lightColour;
        private int[] steamers;
        private FxMesh halos,steam;
        // the lit pieces' light on the city's lamp grid (in `lit`'s order)
        private NativeCityLamps.Lamp[] lampLights;private NativeCityLampLight.Block lampBlock;
        /// <summary>Last frame: pieces drawn, draws, and the effects' quads.</summary>
        public int Drawn{get;private set;}public int Draws{get;private set;}public int FxQuads{get;private set;}
        public NativeMeridianStreetLife.Plan Plan=>plan;

        /// <summary>Opens the drawer for a plan (null without the bank or with --ca-no-city-streets: nothing is drawn).</summary>
        public static NativeMeridianStreetLifeDraw Open(NativeMeridianStreetLife.Plan plan,ContentMap map,Camera eye)
        {
            if(plan==null||map==null||eye==null||Array.IndexOf(Environment.GetCommandLineArgs(),OffArgument)>=0)return null;
            var bank=NativePackBanks.Bank(NativeMeridianStreetLife.Bank);if(bank==null)return null;
            var go=new GameObject("Meridian street life");var d=go.AddComponent<NativeMeridianStreetLifeDraw>();
            try{if(d.Prepare(bank,plan,map,eye))return d;}
            catch(Exception e){Debug.LogWarning("Meridian street life disabled: "+e.Message);}
            Destroy(go);return null;
        }
        /// <summary>Opens the drawer now and plans on a worker (the plan is pure Core): it draws from the frame the plan is ready. Null
        /// without the bank or with --ca-no-city-streets.</summary>
        public static NativeMeridianStreetLifeDraw OpenAsync(NativeMeridianStreetLife.Input input,Camera eye)
        {
            if(input?.Map==null||eye==null||Array.IndexOf(Environment.GetCommandLineArgs(),OffArgument)>=0)return null;
            var bank=NativePackBanks.Bank(NativeMeridianStreetLife.Bank);if(bank==null)return null;
            var go=new GameObject("Meridian street life");var d=go.AddComponent<NativeMeridianStreetLifeDraw>();
            d.pending=System.Threading.Tasks.Task.Run(()=>NativeMeridianStreetLife.Build(input));d.pendingMap=input.Map;d.pendingBank=bank;d.eye=eye;d.watch=System.Diagnostics.Stopwatch.StartNew();
            return d;
        }
        private System.Threading.Tasks.Task<NativeMeridianStreetLife.Plan> pending;private ContentMap pendingMap;private NativePackBank.Bank pendingBank;private System.Diagnostics.Stopwatch watch;
        /// <summary>The DEV walk's switch (its L key): the street life hidden, to compare against the bare streets.</summary>
        public static bool Hidden;
        /// <summary>Milliseconds the plan took on its worker (0 until ready).</summary>
        public float PlanMs{get;private set;}
        public void Close(){if(this!=null&&gameObject!=null)Destroy(gameObject);}

        private bool Prepare(NativePackBank.Bank bank,NativeMeridianStreetLife.Plan plan,ContentMap map,Camera eye)
        {
            this.plan=plan;this.eye=eye;
            var shader=Resources.Load<Shader>("NativeStageLit");if(shader==null||!shader.isSupported)return false;
            solid=new Material(shader){name="Meridian street life",enableInstancing=true};solid.SetFloat(GlowFlag,0);solid.SetFloat(LiftId,Lift);
            glow=new Material(shader){name="Meridian street life glow",enableInstancing=true};glow.SetFloat(GlowFlag,1);glow.SetFloat(LiftId,Lift);
            block=new MaterialPropertyBlock();
            var K=NativeMeridianStreetLife.Kinds;kinds=new Kind[K.Length];int found=0;
            for(int k=0;k<K.Length;k++)
            {
                Mesh Tier(int t)
                {
                    var prop=bank.Find("rig/streets/"+K[k].Key+"/"+t);if(prop==null||prop.Parts.Length==0)return null;
                    var mesh=NativePackBanks.Mesh(NativeMeridianStreetLife.Bank,prop.Parts[0].Mesh);if(mesh==null)return null;
                    // a mesh that left its kind's envelope (a bank built against another table) is left out: the planner placed the
                    // envelope by the city's rules, not the mesh
                    var b=mesh.bounds;var lo=K[k].Min;var hi=K[k].Max;
                    if(b.min.x<lo.x-.02f||b.min.y<lo.y-.02f||b.min.z<lo.z-.02f||b.max.x>hi.x+.02f||b.max.y>hi.y+.02f||b.max.z>hi.z+.02f){Debug.LogWarning("Meridian street life: "+K[k].Key+" leaves its envelope");return null;}
                    return mesh;
                }
                var near=Tier(NativeStageArt.Fine);var far=Tier(NativeStageArt.Low)??near;near??=far;
                if(near==null)continue;kinds[k]=new Kind{Near=near,Far=far,Casts=K[k].Casts,Reach=ReachOf(K[k].Mount,K[k].Lit),Low=ReachOf(K[k].Mount,K[k].Lit)*LowShare(K[k].Mount,K[k].Lit)};found++;
            }
            if(found==0)return false;
            int n=plan.Pieces.Count;place=new Matrix4x4[n];box=new Bounds[n];tint=new Vector4[n];lightOf=new Vector4[n];
            visible=new int[n];keyOf=new int[n];order=new int[n];counts=new int[K.Length*2+1];starts=new int[K.Length*2+1];
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            Vector4 Col(uint hex){var c=NativeArena.ColorOf((int)hex);if(linear)c=c.linear;return new Vector4(c.r,c.g,c.b,0);}
            var litList=new List<int>();var steamList=new List<int>();
            for(int i=0;i<n;i++)
            {
                var p=plan.Pieces[i];var k=K[p.Kind];
                place[i]=Matrix4x4.TRS(new Vector3(p.At.x,p.At.y,p.At.z),Quaternion.Euler(0,p.Yaw*Mathf.Rad2Deg,0),new Vector3(p.Scale.x,p.Scale.y,p.Scale.z));
                NativeMeridianStreetLife.Bounds(p,out var lo,out var hi);box[i]=new Bounds(new Vector3((lo.x+hi.x)*.5f,(lo.y+hi.y)*.5f,(lo.z+hi.z)*.5f),new Vector3(hi.x-lo.x+.05f,hi.y-lo.y+.05f,hi.z-lo.z+.05f));
                tint[i]=p.Tint!=0?Col(p.Tint):Vector4.zero;
                lightOf[i]=k.Lit?Col(k.Light):new Vector4(1,1,1,0);
                if(k.Lit&&kinds[p.Kind]!=null&&p.Life!=NativeMeridianStreetLife.Life.Dead&&p.Life!=NativeMeridianStreetLife.Life.Spent)litList.Add(i);
                if(p.Life==NativeMeridianStreetLife.Life.Steam)steamList.Add(i);
            }
            // the lights' heads and pools (the floor under each head, from the map's boxes)
            lit=litList.ToArray();steamers=steamList.ToArray();heads=new Vector3[lit.Length];poolAt=new Vector3[lit.Length];poolR=new float[lit.Length];lightColour=new Color[lit.Length];
            var all=new List<int>(map.blocks.Length);for(int i=0;i<map.blocks.Length;i++)all.Add(i);
            for(int j=0;j<lit.Length;j++)
            {
                var p=plan.Pieces[lit[j]];var k=K[p.Kind];var h=NativeMeridianStreetLife.Local(p,k.Head);heads[j]=new Vector3(h.x,h.y,h.z);
                var near=Near(map,h,6f,all);float floor=NativeCityBleedDress.FloorBelow(map.blocks,near,new V3(h.x,h.y-.02f,h.z));if(floor<-1e8f)floor=p.At.y;
                // a flood throws its pool out over the plaza, along its aim; the others under their heads
                var pc=k.Glow==NativeMeridianStreetLife.Glow.Flood?NativeMeridianStreetLife.Local(p,new V3(0,0,k.Pool*.55f)):h;
                poolAt[j]=new Vector3(pc.x,floor+.04f,pc.z);poolR[j]=k.Pool;
                var c=NativeArena.ColorOf((int)k.Light);lightColour[j]=c;
            }
            var puff=Resources.Load<Shader>("NativeFieldGlow");var halo=Resources.Load<Shader>("NativeGlow");
            if(puff!=null)
            {
                steamMat=new Material(puff){name="Meridian street steam",renderQueue=3005};steamMat.SetFloat("_Radial",2.2f);steamMat.SetFloat("_Offset",0);steamMat.SetFloat("_Cull",0);
                steam=new FxMesh("Meridian street steam",(steamers.Length+lit.Length)*8+4);
            }
            if(halo!=null){haloMat=new Material(halo){name="Meridian street halos"};halos=new FxMesh("Meridian street halos",lit.Length*3+4);}
            // every lit piece's light on the walls and floors round it (the city's lamp grid): from its head, a flood's down its aim
            lampLights=new NativeCityLamps.Lamp[lit.Length];
            for(int j=0;j<lit.Length;j++)
            {
                var c=lightColour[j].linear;
                lampLights[j]=NativeMeridianStreetLife.CityLight(K[plan.Pieces[lit[j]].Kind],new V3(heads[j].x,heads[j].y,heads[j].z),new V3(poolAt[j].x,poolAt[j].y,poolAt[j].z),c.r,c.g,c.b);
            }
            lampBlock=NativeCityLampLight.Add(map.id,lampLights);
            Debug.Log(FormattableString.Invariant($"Meridian street life: {n} pieces of {found} kinds, {lit.Length} lit, {steamers.Length} steaming"));
            return true;
        }
        private static List<int> Near(ContentMap map,V3 p,float r,List<int> all)
        {
            var o=new List<int>();foreach(int i in all){var b=map.blocks[i];if(Math.Abs(b.pos.x-p.x)<=b.size.x*.5f+r&&Math.Abs(b.pos.z-p.z)<=b.size.z*.5f+r)o.Add(i);}return o;
        }

        private void LateUpdate()
        {
            if(pending!=null)
            {
                if(!pending.IsCompleted)return;var task=pending;pending=null;
                if(task.Status!=System.Threading.Tasks.TaskStatus.RanToCompletion){Debug.LogWarning("Meridian street life disabled: "+task.Exception?.GetBaseException().Message);enabled=false;return;}
                PlanMs=(float)watch.Elapsed.TotalMilliseconds;
                try{if(!Prepare(pendingBank,task.Result,pendingMap,eye)){enabled=false;return;}}
                catch(Exception ex){Debug.LogWarning("Meridian street life disabled: "+ex.Message);enabled=false;return;}
                Debug.Log(FormattableString.Invariant($"Meridian street life planned in {PlanMs:F0} ms on a worker"));
            }
            // hidden (the DEV walk's L key): its light goes with it
            if(plan!=null&&Hidden&&lampBlock!=null)for(int j=0;j<lit.Length;j++)lampBlock.Level[j]=0;
            if(plan==null||Hidden)return;var cam=eye!=null?eye:Camera.main;if(cam==null)return;
            var e=cam.transform.position;var tier=NativeAdvancedLighting.Tier;bool low=tier==NativeGraphicsTier.Low,reduced=NativeAdvancedLighting.Reduced;float time=Time.time;
            float scale=low?.7f:1f;GeometryUtility.CalculateFrustumPlanes(cam,planes);
            var K=NativeMeridianStreetLife.Kinds;visibleCount=0;Array.Clear(counts,0,counts.Length);
            float far=175f*scale;
            // the cells in reach, then their pieces in their own reach and the frustum
            int cx0=Mathf.Clamp((int)Mathf.Floor((e.x-far-plan.MinX)/NativeMeridianStreetLife.CullCell),0,plan.Nx-1),cx1=Mathf.Clamp((int)Mathf.Floor((e.x+far-plan.MinX)/NativeMeridianStreetLife.CullCell),0,plan.Nx-1);
            int cz0=Mathf.Clamp((int)Mathf.Floor((e.z-far-plan.MinZ)/NativeMeridianStreetLife.CullCell),0,plan.Nz-1),cz1=Mathf.Clamp((int)Mathf.Floor((e.z+far-plan.MinZ)/NativeMeridianStreetLife.CullCell),0,plan.Nz-1);
            for(int cz=cz0;cz<=cz1;cz++)for(int cx=cx0;cx<=cx1;cx++)
            {
                int c=cx+cz*plan.Nx;
                for(int o=plan.CellStart[c];o<plan.CellStart[c+1];o++)
                {
                    int i=plan.Order[o];var kind=kinds[plan.Pieces[i].Kind];if(kind==null)continue;
                    float d2=box[i].SqrDistance(e),reach=low?kind.Low:kind.Reach;if(d2>reach*reach)continue;
                    if(!GeometryUtility.TestPlanesAABB(planes,box[i]))continue;
                    int t=NativeStageArt.TierAt(Mathf.Sqrt(d2),low)==NativeStageArt.Low?1:0;int key=plan.Pieces[i].Kind*2+t;
                    visible[visibleCount]=i;keyOf[visibleCount]=key;visibleCount++;counts[key]++;
                }
            }
            // counted, then placed by key: each kind's tier draws in batches of 500
            int sum=0;for(int k=0;k<counts.Length;k++){starts[k]=sum;sum+=counts[k];}
            for(int v=0;v<visibleCount;v++)order[starts[keyOf[v]]++]=visible[v];
            Drawn=Draws=0;int at=0;
            for(int key=0;key<counts.Length-1;key++)
            {
                int n=counts[key];if(n==0)continue;var kind=kinds[key>>1];var mesh=(key&1)==0?kind.Near:kind.Far;
                for(int b=0;b<n;b+=BatchSize)
                {
                    int m=Math.Min(BatchSize,n-b);
                    for(int j=0;j<m;j++){int i=order[at+b+j];batchM[j]=place[i];batchGlow[j]=GlowOf(i,time,reduced);batchTint[j]=tint[i];}
                    block.Clear();block.SetVectorArray(GlowId,batchGlow);block.SetVectorArray(TintId,batchTint);
                    for(int s=0;s<mesh.subMeshCount;s++)
                        Graphics.DrawMeshInstanced(mesh,s,s==0?solid:glow,batchM,m,block,s==0&&kind.Casts&&!low?ShadowCastingMode.On:ShadowCastingMode.Off,true,0,null,LightProbeUsage.Off);
                    Drawn+=m;Draws+=mesh.subMeshCount;
                }
                at+=n;
            }
            Light(cam,e,time,low,reduced);
        }
        /// <summary>A piece's glow this draw: a lit piece's light at its level (Core NativeMeridianStreetLife.Level), a dead one dark,
        /// anything else as banked.</summary>
        private Vector4 GlowOf(int i,float time,bool reduced)
        {
            var p=plan.Pieces[i];var k=NativeMeridianStreetLife.Kinds[p.Kind];if(!k.Lit)return Vector4.one;
            if(p.Life==NativeMeridianStreetLife.Life.Dead||p.Life==NativeMeridianStreetLife.Life.Spent)return new Vector4(.03f,.03f,.03f,1);
            float l=NativeMeridianStreetLife.Level(p.Life==NativeMeridianStreetLife.Life.None?NativeMeridianStreetLife.Life.Steady:p.Life,p.Seed,time,reduced);
            var c=lightOf[i];return new Vector4(c.x*l,c.y*l,c.z*l,1);
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The light: every lit piece on the walls and floors round it (the city's lamp grid), halos at the heads near the eye, flames
        // over the fires and flares, the grates' steam
        // ------------------------------------------------------------------------------------------------------------------
        private void Light(Camera cam,Vector3 e,float time,bool low,bool reduced)
        {
            FxQuads=0;
            // each lit piece's level this frame on the grid (NativeCityLampLight uploads after every drawer)
            if(lampBlock!=null)for(int j=0;j<lit.Length;j++){var lp=plan.Pieces[lit[j]];lampBlock.Level[j]=NativeMeridianStreetLife.Level(lp.Life==NativeMeridianStreetLife.Life.None?NativeMeridianStreetLife.Life.Steady:lp.Life,lp.Seed,time,reduced);}
            if(halos==null)return;
            var right=cam.transform.right;var up=cam.transform.up;
            halos.Begin();steam?.Begin();
            for(int j=0;j<lit.Length;j++)
            {
                var p=plan.Pieces[lit[j]];float d=Vector3.Distance(heads[j],e);if(d>FxReach*(low?.7f:1f))continue;
                float level=NativeMeridianStreetLife.Level(p.Life==NativeMeridianStreetLife.Life.None?NativeMeridianStreetLife.Life.Steady:p.Life,p.Seed,time,reduced);if(level<.02f)continue;
                var c=lightColour[j];var k=NativeMeridianStreetLife.Kinds[p.Kind];
                if(low)continue;
                float s=k.Glow==NativeMeridianStreetLife.Glow.Flood?.9f:k.Glow==NativeMeridianStreetLife.Glow.Sign?.75f:k.Glow==NativeMeridianStreetLife.Glow.Festoon?.35f:k.Glow==NativeMeridianStreetLife.Glow.Signal?.5f:.55f;
                halos.Quad(heads[j],right*s,up*s,new Color(c.r,c.g,c.b,.45f*level),0);
                if(k.Glow==NativeMeridianStreetLife.Glow.Fire||k.Glow==NativeMeridianStreetLife.Glow.Flare)Flames(j,p,time,reduced,right,up);
            }
            if(steam!=null&&!low)
            {
                foreach(int i in steamers)
                {
                    var p=plan.Pieces[i];var at=new Vector3(p.At.x,p.At.y,p.At.z);if((at-e).sqrMagnitude>SteamReach*SteamReach)continue;
                    Plume(at,p.Seed,time,reduced,right,up,new Color(.62f,.66f,.72f,1),.16f,reduced?3:6,3.4f);
                }
                // the flares' smoke: a thin red-lit wisp
                for(int j=0;j<lit.Length;j++)
                {
                    var p=plan.Pieces[lit[j]];if(NativeMeridianStreetLife.Kinds[p.Kind].Glow!=NativeMeridianStreetLife.Glow.Flare||(heads[j]-e).sqrMagnitude>SteamReach*SteamReach)continue;
                    Plume(heads[j],p.Seed,time,reduced,right,up,new Color(.55f,.28f,.26f,1),.12f,reduced?2:3,2.2f);
                }
            }
            if(halos!=null){FxQuads+=halos.Count;halos.Draw(haloMat,e);}
            if(steam!=null){FxQuads+=steam.Count;steam.Draw(steamMat,e);}
        }
        // Flames: three tongues over the head, each rising and fading on its own slow cycle (under 2.4 Hz; 0.7 Hz reduced).
        private void Flames(int j,NativeMeridianStreetLife.Piece p,float time,bool reduced,Vector3 right,Vector3 up)
        {
            bool flare=NativeMeridianStreetLife.Kinds[p.Kind].Glow==NativeMeridianStreetLife.Glow.Flare;float phase=(p.Seed&1023)/1023f;int n=reduced?2:3;
            for(int k=0;k<n;k++)
            {
                float rate=(reduced?.55f:1.6f)+.3f*k,u=(time*rate+phase+k*.37f)%1f;
                float h=(flare?.18f:.32f)*(.3f+.7f*u),w=(flare?.07f:.16f)*(1-.6f*u);
                var at=heads[j]+Vector3.up*(h+.05f)+right*((k-1)*(flare?.03f:.08f));
                var c=flare?new Color(1f,.42f,.3f):new Color(1f,.62f,.25f);halos.Quad(at,right*w,up*(w*1.8f),new Color(c.r,c.g,c.b,.7f*(1-u)),0);
            }
        }
        // A plume of soft puffs rising from a point, each on its own loop (alpha blended, faint).
        private void Plume(Vector3 at,uint seed,float time,bool reduced,Vector3 right,Vector3 up,Color c,float alpha,int puffs,float rise)
        {
            float phase=(seed&1023)/1023f,speed=reduced?.12f:.22f;
            for(int k=0;k<puffs;k++)
            {
                float u=(time*speed+phase+(float)k/puffs)%1f;float s=.35f+1.3f*u;
                var p=at+Vector3.up*(.15f+rise*u)+new Vector3(Mathf.Sin(k*2.3f+time*.3f)*.25f*u,0,Mathf.Cos(k*1.7f+time*.27f)*.25f*u);
                steam.Quad(p,right*s,up*s,new Color(c.r,c.g,c.b,alpha*Mathf.Sin(u*Mathf.PI)),-1);
            }
        }

        /// <summary>A dynamic mesh of quads rebuilt each frame (centre, two half edges, colour; uv -1..1, z the glow shader's mode).</summary>
        private sealed class FxMesh
        {
            private readonly Mesh mesh;private readonly Vector3[] v;private readonly Color[] c;private readonly Vector3[] uv;private readonly int[] t;private readonly int cap;
            public int Count{get;private set;}
            public FxMesh(string name,int quads)
            {
                cap=Math.Max(4,quads);v=new Vector3[cap*4];c=new Color[cap*4];uv=new Vector3[cap*4];t=new int[cap*6];
                for(int q=0;q<cap;q++){int a=q*4,b=q*6;t[b]=a;t[b+1]=a+2;t[b+2]=a+1;t[b+3]=a;t[b+4]=a+3;t[b+5]=a+2;}
                mesh=new Mesh{name=name,indexFormat=cap*4>65000?IndexFormat.UInt32:IndexFormat.UInt16};mesh.MarkDynamic();
            }
            public void Begin(){Count=0;}
            public void Quad(Vector3 centre,Vector3 a,Vector3 b,Color col,float mode=-1)
            {
                if(Count>=cap)return;int k=Count*4;
                v[k]=centre-a-b;v[k+1]=centre+a-b;v[k+2]=centre+a+b;v[k+3]=centre-a+b;
                float z=mode<0?0:mode;uv[k]=new Vector3(-1,-1,z);uv[k+1]=new Vector3(1,-1,z);uv[k+2]=new Vector3(1,1,z);uv[k+3]=new Vector3(-1,1,z);
                c[k]=c[k+1]=c[k+2]=c[k+3]=col;Count++;
            }
            public void Draw(Material m,Vector3 e)
            {
                if(Count==0||m==null)return;
                for(int k=Count*4;k<v.Length;k++){v[k]=Vector3.zero;c[k]=Color.clear;}
                mesh.vertices=v;mesh.colors=c;mesh.SetUVs(0,uv);if(mesh.GetIndexCount(0)==0)mesh.triangles=t;mesh.bounds=new Bounds(e,Vector3.one*500f);
                Graphics.DrawMesh(mesh,Matrix4x4.identity,m,0,null,0,null,ShadowCastingMode.Off,false);
            }
            public void Release(){if(mesh!=null)Destroy(mesh);}
        }

        private void OnDestroy()
        {
            if(solid!=null)Destroy(solid);if(glow!=null)Destroy(glow);if(haloMat!=null)Destroy(haloMat);if(steamMat!=null)Destroy(steamMat);
            halos?.Release();steam?.Release();plan=null;NativeCityLampLight.Remove(lampBlock);lampBlock=null;
        }
    }
}
