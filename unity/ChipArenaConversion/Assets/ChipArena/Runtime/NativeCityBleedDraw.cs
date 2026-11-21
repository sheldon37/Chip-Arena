using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The bleed's drawer (docs/MERIDIAN-BLEED.md): Core NativeCityBleedDress's plan drawn from the bleed's bank
    /// (Resources/ChipArenaCityBleed, from the Drive zip), with its light and the atmosphere the camera's share calls for.
    ///  - The pieces: every kind instanced (ChipArena/StageLit, nine layers up, over all of the street dress's layers, so paint on a floor or
    ///    a wall never fights what lies under it), each instance's tier by its distance from the eye (NativeStageArt.TierAt; the
    ///    Low tier on the low graphics tier), culled against the eye's frustum, at most 500 to a draw.
    ///  - The light: lamp heads glow in each lamp's colour; 1 in 5 dead and 1 in 10 flickering as the plan dealt them, Ward
    ///    Zero's beacons turning, Level 0's troffers buzzing on one circuit; each working lamp lights the walls and floors round it
    ///    in its colour through the city's lamp grid (NativeCityLampLight; 2026-11-21: a flat pool disc under it read as light laid
    ///    over the floor, not on it), a halo at its head; over each door a glow in its instance's colour on the cloud deck
    ///    (ChipArena/NativeGlow, additive, no fog: the rumour band's sky reads from the camp).
    ///  - The atmosphere: fog, ambient and their colours drift toward the dominant instance's by the camera's share
    ///    (eased over about a second), and are given back when the drawer closes.
    /// Without the bank (CI, a build without the Drive zip, --ca-no-pack-banks) <see cref="Open"/> returns null and nothing
    /// changes: today's greybox. Never a collider, a rule or a record. Allocation free per frame once open.
    /// </summary>
    public sealed class NativeCityBleedDraw : MonoBehaviour
    {
        /// <summary>Each theme's mood: the sky glow over its door, its fog and ambient tints, how far the fog and ambient go
        /// toward them at full share, and the ambient's gain at its threshold (the Poolrooms' one bright, calm place).</summary>
        private struct Mood{public uint Sky,Dome,Fog,Ambient;public float Fogging,Tinting,Bright;}
        // The sky glows are the art bible's (3.2: each instance's glow on its bearing; Ward Zero's red over violet), the fog and
        // ambient tints their light's.
        private static Mood MoodOf(string theme)
        {
            switch(theme)
            {
                case "level0":return new Mood{Sky=0x9A9440,Dome=0x9A9440,Fog=0x3A3620,Ambient=0x9C9460,Fogging=.55f,Tinting=.45f,Bright=1.4f};
                case "poolrooms":return new Mood{Sky=0x2E9C9A,Dome=0x2E9C9A,Fog=0x1C4A50,Ambient=0xEAF6F4,Fogging=.6f,Tinting=.5f,Bright=2.4f};
                case "wardzero":return new Mood{Sky=0x9A2430,Dome=0x5A2C8A,Fog=0x2A1430,Ambient=0xD4E6FF,Fogging=.55f,Tinting=.45f,Bright=1f};
                case "station":return new Mood{Sky=0x3A5AA0,Dome=0x3A5AA0,Fog=0x101C34,Ambient=0x7CB4FF,Fogging=.5f,Tinting=.4f,Bright=1f};
                case "ironworks":return new Mood{Sky=0xA0461A,Dome=0xA0461A,Fog=0x2A1A10,Ambient=0xFF9A30,Fogging=.6f,Tinting=.45f,Bright=1.1f};
                default:return new Mood{Sky=0x5A2C8A,Dome=0x5A2C8A,Fog=0x1A1230,Ambient=0x8A3CFF,Fogging=.4f,Tinting=.3f,Bright=1f};
            }
        }

        public const string OffArgument="--ca-no-city-bleed";
        /// <summary>The layers the bleed draws up (ChipArena/StageLit's _StageLift): over every layer the street dress uses (its
        /// paint and the stacks on its surfaces go up to 8), so a stain or a skin on the dress's surface never fights it.</summary>
        public const int Lift=9;
        private const int BatchSize=500;
        private sealed class Kind
        {
            public Mesh[] Mesh=new Mesh[NativeStageArt.Tiers];public bool Casts,Shimmer;public int Subs;
            public readonly List<int> Pieces=new List<int>();
        }
        private sealed class Batch{public readonly Matrix4x4[] M=new Matrix4x4[BatchSize];public readonly Vector4[] Glow=new Vector4[BatchSize];public int Count;}
        private NativeCityBleed bleed;private NativeCityBleedDress.Plan plan;private Camera eye;private Func<bool> low;
        private Kind[] kinds;private Matrix4x4[] place;private Bounds[] box;private Vector4[] baseGlow;private Batch batch;
        private Material solid,glow,fxMaterial;private MaterialPropertyBlock block;private Mesh fx;private Color[] fxColors;private Color[] fxBase;
        private readonly List<(int piece,int from,int count)> fxLamps=new List<(int,int,int)>();
        // the working lamps' light on the city's lamp grid, and each one's piece
        private NativeCityLampLight.Block lampBlock;private int[] lampPieces;
        private readonly Plane[] planes=new Plane[6];
        private float[] shares;private Mood[] moods;
        // the atmosphere: what the look set, what this drawer last wrote, and the eased share
        private Color fogBase,skyBase,equatorBase,groundBase,fogWrote,skyWrote;private float fogEndBase,fogEndWrote;private bool wrote;private float eased;private int easedDoor=-1;
        private static readonly int GlowId=Shader.PropertyToID("_StageGlow"),GlowFlag=Shader.PropertyToID("_Glow"),LiftId=Shader.PropertyToID("_StageLift");
        /// <summary>Last frame: pieces drawn, and draws.</summary>
        public int Drawn{get;private set;}public int Draws{get;private set;}

        /// <summary>Opens the drawer for a city's bleed and plan (null without the bank: the greybox stays as it is).
        /// low: whether the low graphics tier is on.</summary>
        public static NativeCityBleedDraw Open(NativeCityBleed bleed,NativeCityBleedDress.Plan plan,ContentMap map,Camera eye,Func<bool> low)
        {
            if(bleed==null||plan==null||map==null||eye==null||Array.IndexOf(Environment.GetCommandLineArgs(),OffArgument)>=0)return null;
            var bank=NativePackBanks.Bank(NativeCityBleedDress.Bank);if(bank==null)return null;
            var go=new GameObject("City bleed");var d=go.AddComponent<NativeCityBleedDraw>();
            try{if(d.Prepare(bank,bleed,plan,map,eye,low))return d;}
            catch(Exception e){Debug.LogWarning("City bleed disabled: "+e.Message);}
            Destroy(go);return null;
        }
        /// <summary>Closes the drawer and gives the atmosphere back.</summary>
        public void Close(){if(this!=null&&gameObject!=null)Destroy(gameObject);}

        private bool Prepare(NativePackBank.Bank bank,NativeCityBleed bleed,NativeCityBleedDress.Plan plan,ContentMap map,Camera eye,Func<bool> low)
        {
            this.bleed=bleed;this.plan=plan;this.eye=eye;this.low=low;
            var shader=Resources.Load<Shader>("NativeStageLit");if(shader==null||!shader.isSupported)return false;
            solid=new Material(shader){name="City bleed",enableInstancing=true};solid.SetFloat(GlowFlag,0);solid.SetFloat(LiftId,Lift);
            glow=new Material(shader){name="City bleed glow",enableInstancing=true};glow.SetFloat(GlowFlag,1);glow.SetFloat(LiftId,Lift);
            block=new MaterialPropertyBlock();batch=new Batch();
            var K=NativeCityBleedDress.Kinds;kinds=new Kind[K.Length];
            for(int k=0;k<K.Length;k++)
            {
                // the water's caustic threads shimmer (the flood and the puddles: their glow's gain, per instance)
                var kind=new Kind{Casts=K[k].Casts,Shimmer=K[k].Key.StartsWith("poolrooms.flood",StringComparison.Ordinal)||K[k].Key=="poolrooms.puddle"};
                for(int t=0;t<NativeStageArt.Tiers;t++)
                {
                    var prop=bank.Find("rig/bleed/"+K[k].Key+"/"+t);if(prop==null||prop.Parts.Length==0)continue;
                    var mesh=NativePackBanks.Mesh(NativeCityBleedDress.Bank,prop.Parts[0].Mesh);if(mesh==null)continue;
                    // a mesh that left its kind's envelope (a bank built against another plan) is left out: the planner placed the
                    // envelope by the city's rules, not the mesh
                    var b=mesh.bounds;var lo=K[k].Min;var hi=K[k].Max;
                    if(b.min.x<lo.x-.02f||b.min.y<lo.y-.02f||b.min.z<lo.z-.02f||b.max.x>hi.x+.02f||b.max.y>hi.y+.02f||b.max.z>hi.z+.02f){Debug.LogWarning("City bleed: "+K[k].Key+" leaves its envelope");continue;}
                    kind.Mesh[t]=mesh;kind.Subs=Math.Max(kind.Subs,mesh.subMeshCount);
                }
                if(kind.Mesh[NativeStageArt.Mid]==null)kind.Mesh[NativeStageArt.Mid]=kind.Mesh[NativeStageArt.Fine];
                kinds[k]=kind.Mesh[NativeStageArt.Fine]!=null||kind.Mesh[NativeStageArt.Low]!=null?kind:null;
            }
            int n=plan.Pieces.Count;place=new Matrix4x4[n];box=new Bounds[n];baseGlow=new Vector4[n];
            for(int i=0;i<n;i++)
            {
                var p=plan.Pieces[i];var kind=kinds[p.Kind];if(kind==null)continue;kind.Pieces.Add(i);
                place[i]=Matrix4x4.TRS(new Vector3(p.At.x,p.At.y,p.At.z),Quaternion.Euler(0,p.Yaw*Mathf.Rad2Deg,0),new Vector3(p.Scale.x,p.Scale.y,p.Scale.z));
                NativeCityBleedDress.Bounds(p,out var lo,out var hi);box[i]=new Bounds(new Vector3((lo.x+hi.x)/2,(lo.y+hi.y)/2,(lo.z+hi.z)/2),new Vector3(hi.x-lo.x+.1f,hi.y-lo.y+.1f,hi.z-lo.z+.1f));
                baseGlow[i]=p.Light!=0?Linear(p.Light):Vector4.one;
                if(p.Lamp==NativeCityBleedDress.LampState.Dead)baseGlow[i]=new Vector4(.03f,.03f,.03f,1);
            }
            Fx(map);
            shares=new float[bleed.Doors.Length];moods=new Mood[bleed.Doors.Length];
            for(int d=0;d<moods.Length;d++)moods[d]=MoodOf(NativeCityBleed.Theme(bleed.Doors[d].Instance));
            fogBase=RenderSettings.fogColor;skyBase=RenderSettings.ambientSkyColor;equatorBase=RenderSettings.ambientEquatorColor;groundBase=RenderSettings.ambientGroundColor;fogEndBase=RenderSettings.fogEndDistance;
            return true;
        }
        private static Vector4 Linear(uint hex){var c=NativeArena.ColorOf((int)hex);if(QualitySettings.activeColorSpace==ColorSpace.Linear)c=c.linear;return new Vector4(c.r,c.g,c.b,1);}
        private static Color Hex(uint hex)=>NativeArena.ColorOf((int)hex);

        // The light's effects, one mesh (ChipArena/NativeGlow: vertex colour x a gaussian on uv, additive): a pool on the floor
        // under each lamp and a cross of halos at its head; over each instance door, its glow on the cloud deck.
        private void Fx(ContentMap map)
        {
            var sh=Resources.Load<Shader>("NativeGlow");if(sh==null)return;
            fxMaterial=new Material(sh){name="City bleed light"};
            var v=new List<Vector3>();var c=new List<Color>();var uv=new List<Vector3>();var tri=new List<int>();
            var all=new int[map.blocks.Length];for(int i=0;i<all.Length;i++)all[i]=i;
            var lamps=new List<NativeCityLamps.Lamp>();var pieces=new List<int>();
            void Quad(Vector3 o,Vector3 u,Vector3 w,Color col)
            {
                int b=v.Count;v.Add(o-u-w);v.Add(o+u-w);v.Add(o+u+w);v.Add(o-u+w);
                uv.Add(new Vector3(-1,-1,0));uv.Add(new Vector3(1,-1,0));uv.Add(new Vector3(1,1,0));uv.Add(new Vector3(-1,1,0));
                for(int k=0;k<4;k++)c.Add(col);tri.Add(b);tri.Add(b+2);tri.Add(b+1);tri.Add(b);tri.Add(b+3);tri.Add(b+2);
            }
            for(int i=0;i<plan.Pieces.Count;i++)
            {
                var p=plan.Pieces[i];var K=NativeCityBleedDress.Kinds[p.Kind];if(!K.Lamp||kinds[p.Kind]==null||p.Lamp==NativeCityBleedDress.LampState.Dead)continue;
                var head=NativeCityBleedDress.Local(p,K.Head);var h=new Vector3(head.x,head.y,head.z);
                float floor=NativeCityBleedDress.FloorBelow(map.blocks,all,new V3(head.x,head.y-.05f,head.z));if(floor<-1e8f)floor=head.y-4.6f;
                var lc=baseGlow[i];var col=new Color(lc.x,lc.y,lc.z,1);int from=v.Count;
                float over=head.y-floor;
                bool ceiling=K.Key.StartsWith("level0.ceiling",StringComparison.Ordinal);
                // its light on the walls and floors round it, in its colour, reaching its floor and past it (the grid)
                float strength=ceiling?.8f:1.2f;lamps.Add(new NativeCityLamps.Lamp{At=head,Range=Mathf.Clamp(over*1.8f,5f,10f),R=lc.x*strength,G=lc.y*strength,B=lc.z*strength});pieces.Add(i);
                float s=K.Key=="poolrooms.poollamp"?.45f:ceiling?.6f:.35f;
                Quad(h,new Vector3(s,0,0),new Vector3(0,s,0),new Color(col.r,col.g,col.b,.5f));Quad(h,new Vector3(0,0,s),new Vector3(0,s,0),new Color(col.r,col.g,col.b,.5f));
                if(p.Lamp!=NativeCityBleedDress.LampState.Steady)fxLamps.Add((i,from,v.Count-from));
            }
            // the cloud deck over each instance door, lit in its colour (a deck of light 50 m up, and a dome seen from the camp)
            for(int d=0;d<bleed.Doors.Length;d++)
            {
                string theme=NativeCityBleed.Theme(bleed.Doors[d].Instance);if(theme==null||theme=="final")continue;
                var m=MoodOf(theme);var a=bleed.Doors[d].At;var at=new Vector3(a.x,a.y+55,a.z);var sky=Hex(m.Sky);var dome=Hex(m.Dome);
                Quad(at,new Vector3(95,0,0),new Vector3(0,0,95),new Color(sky.r,sky.g,sky.b,.34f));
                var toward=new Vector3(a.x,0,a.z);toward=toward.sqrMagnitude>1?toward.normalized:Vector3.forward;var across=new Vector3(toward.z,0,-toward.x);
                Quad(new Vector3(a.x,a.y+30,a.z)+toward*20,across*110,new Vector3(0,42,0),new Color(dome.r,dome.g,dome.b,.28f));
            }
            lampPieces=pieces.ToArray();lampBlock=NativeCityLampLight.Add(map.id,lamps.ToArray());
            if(v.Count==0)return;
            fx=new Mesh{name="City bleed light",indexFormat=v.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};
            fx.SetVertices(v);fx.SetColors(c);fx.SetUVs(0,uv);fx.SetTriangles(tri,0);fx.RecalculateBounds();fx.MarkDynamic();
            fxColors=c.ToArray();fxBase=c.ToArray();
        }

        private void LateUpdate()
        {
            if(plan==null||eye==null)return;
            var e=eye.transform.position;bool lowTier=low!=null&&low();float time=Time.time;
            GeometryUtility.CalculateFrustumPlanes(eye,planes);
            Drawn=Draws=0;
            for(int k=0;k<kinds.Length;k++)
            {
                var kind=kinds[k];if(kind==null)continue;var K=NativeCityBleedDress.Kinds[k];
                for(int t=0;t<NativeStageArt.Tiers;t++)
                {
                    var mesh=kind.Mesh[t]??kind.Mesh[NativeStageArt.Low]??kind.Mesh[NativeStageArt.Fine];if(mesh==null)continue;batch.Count=0;
                    foreach(int i in kind.Pieces)
                    {
                        var b=box[i];if(NativeStageArt.TierAt(Mathf.Sqrt(b.SqrDistance(e)),lowTier)!=t)continue;
                        if(!GeometryUtility.TestPlanesAABB(planes,b))continue;
                        batch.M[batch.Count]=place[i];batch.Glow[batch.Count]=GlowOf(i,time);batch.Count++;
                        if(batch.Count==BatchSize)Submit(kind,mesh,K.Casts);
                    }
                    if(batch.Count>0)Submit(kind,mesh,K.Casts);
                }
            }
            // the lamps' levels this frame on the grid (steady 1; flickering and turning as their glow)
            if(lampBlock!=null)for(int j=0;j<lampPieces.Length;j++)lampBlock.Level[j]=Intensity(lampPieces[j],time);
            if(fx!=null)
            {
                foreach(var (piece,from,count) in fxLamps)
                {
                    float k=Intensity(piece,time);for(int j=from;j<from+count;j++){var b=fxBase[j];fxColors[j]=new Color(b.r,b.g,b.b,b.a*k);}
                }
                if(fxLamps.Count>0)fx.colors=fxColors;
                Graphics.DrawMesh(fx,Matrix4x4.identity,fxMaterial,0,null,0,null,ShadowCastingMode.Off,false);
            }
            Atmosphere(new V3(e.x,e.y,e.z));
        }
        private void Submit(Kind kind,Mesh mesh,bool casts)
        {
            block.Clear();block.SetVectorArray(GlowId,batch.Glow);
            for(int s=0;s<mesh.subMeshCount;s++)
                Graphics.DrawMeshInstanced(mesh,s,s==0?solid:glow,batch.M,batch.Count,block,s==0&&casts?ShadowCastingMode.On:ShadowCastingMode.Off,true,0,null,LightProbeUsage.Off);
            Drawn+=batch.Count;Draws+=mesh.subMeshCount;batch.Count=0;
        }
        private Vector4 GlowOf(int i,float time)
        {
            var g=baseGlow[i];var p=plan.Pieces[i];
            if(kinds[p.Kind].Shimmer){float k=.45f+.55f*Mathf.Abs(Mathf.Sin(time*.9f+(p.Seed&1023)*.0061f+p.At.x*.13f));return new Vector4(g.x*k,g.y*k,g.z*k,1);}
            if(p.Lamp==NativeCityBleedDress.LampState.Flicker||p.Lamp==NativeCityBleedDress.LampState.Turning){float k=Intensity(i,time);return new Vector4(g.x*k,g.y*k,g.z*k,1);}
            return g;
        }
        // A flickering lamp stutters under 3 Hz (slower and shallower with Reduced effects); a turning beacon sweeps past the eye
        // about once a second (NativeCityBleedDress.LampIntensity, checked by BleedChecks.Flicker).
        private float Intensity(int i,float time)
        {
            var p=plan.Pieces[i];
            return NativeCityBleedDress.LampIntensity(p.Lamp,p.Seed,time,NativeAdvancedLighting.Reduced);
        }

        // The fog and ambient toward the dominant instance's by the camera's share: from the rumour band's edge, eased.
        private void Atmosphere(V3 at)
        {
            // someone else wrote the look since (a new look, an intro's end): take theirs as the base
            if(!wrote||RenderSettings.fogColor!=fogWrote||RenderSettings.ambientSkyColor!=skyWrote||Mathf.Abs(RenderSettings.fogEndDistance-fogEndWrote)>1e-3f)
            {
                fogBase=RenderSettings.fogColor;skyBase=RenderSettings.ambientSkyColor;equatorBase=RenderSettings.ambientEquatorColor;groundBase=RenderSettings.ambientGroundColor;fogEndBase=RenderSettings.fogEndDistance;
            }
            var s=bleed.At(at);float target=s.Door<0?0:Mathf.Clamp01((s.Share-NativeCityBleed.RumourAt)/(NativeCityBleed.ThresholdAt+.2f-NativeCityBleed.RumourAt));
            if(s.Door>=0&&s.Door!=easedDoor){if(eased<.02f||easedDoor<0)easedDoor=s.Door;else target=0;}
            eased=Mathf.MoveTowards(eased,target,Time.deltaTime*.8f);if(eased<.001f&&target<=0)easedDoor=-1;
            if(easedDoor<0){Write(fogBase,skyBase,equatorBase,groundBase,fogEndBase);return;}
            var m=moods[easedDoor];float k=eased;var fog=Color.Lerp(fogBase,Hex(m.Fog),k*m.Fogging);var tint=Hex(m.Ambient);
            float bright=1+(m.Bright-1)*Mathf.Clamp01((k-.6f)/.4f);
            Write(fog,Color.Lerp(skyBase,skyBase.maxColorComponent*tint*1.6f,k*m.Tinting)*bright,Color.Lerp(equatorBase,equatorBase.maxColorComponent*tint*1.6f,k*m.Tinting)*bright,
                Color.Lerp(groundBase,groundBase.maxColorComponent*tint*1.6f,k*m.Tinting)*bright,fogEndBase*(easedDoor>=0&&NativeCityBleed.Theme(bleed.Doors[easedDoor].Instance)=="ironworks"?1-.3f*k:1));
        }
        private void Write(Color fog,Color sky,Color equator,Color ground,float fogEnd)
        {
            RenderSettings.fogColor=fog;RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;RenderSettings.fogEndDistance=fogEnd;
            fogWrote=RenderSettings.fogColor;skyWrote=RenderSettings.ambientSkyColor;fogEndWrote=RenderSettings.fogEndDistance;wrote=true;
        }
        private void OnDestroy()
        {
            // give the look back, unless someone else wrote it since
            if(wrote&&RenderSettings.fogColor==fogWrote&&RenderSettings.ambientSkyColor==skyWrote){RenderSettings.fogColor=fogBase;RenderSettings.ambientSkyColor=skyBase;RenderSettings.ambientEquatorColor=equatorBase;RenderSettings.ambientGroundColor=groundBase;RenderSettings.fogEndDistance=fogEndBase;}
            if(solid!=null)Destroy(solid);if(glow!=null)Destroy(glow);if(fxMaterial!=null)Destroy(fxMaterial);if(fx!=null)Destroy(fx);
            NativeCityLampLight.Remove(lampBlock);lampBlock=null;
        }
    }
}
