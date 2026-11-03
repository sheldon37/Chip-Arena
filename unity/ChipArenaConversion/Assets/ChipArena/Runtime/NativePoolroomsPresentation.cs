using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// DEV: the Poolrooms stand-in layout (Core NativePoolrooms.Preview) drawn at <see cref="Origin"/> until Astra's generator and
    /// the level's map land (docs/BACKROOMS.md "The Poolrooms"). Off by default; `--ca-poolrooms-preview` or the local capture tool
    /// turns it on.
    /// </summary>
    public static class NativePoolroomsPreview
    {
        public const string Argument="--ca-poolrooms-preview";
        public static bool On;public static int Seed=7,Width=30,Depth=30;public static Vector3 Origin=new Vector3(-1000,0,1000);
        /// <summary>Where the blocks are drawn round (null: the world camera), for a fixed capture camera.</summary>
        public static Vector3? Eye;
        /// <summary>DEV: pokes at the water (world x, z, push m, radius m), taken on the next frame; a stand-in wading body walking
        /// from From to To and back at Speed m/s (its feet a metre under the surface) while Wading.</summary>
        public static readonly List<Vector4> Pokes=new List<Vector4>();public static bool Wading;public static Vector3 From,To;public static float Speed=2.5f;
        private static int args=-1;
        /// <summary>On, by the flag or the command line.</summary>
        public static bool Active{get{if(args<0){args=0;try{foreach(var a in Environment.GetCommandLineArgs())if(a==Argument)args=1;}catch(Exception){}}return On||args==1;}}
    }

    // Draws a Poolrooms layout (the owner's Backrooms add-on's look): the layout built in blocks of cells (Core PoolroomsMesh: tiled
    // decks, turquoise pools and steps, tiled walls and arches, plaster ceilings, round pillars and lights), one static mesh per
    // surface per block, the solid ones through NativeBlockLit's pack path with the add-on's textures in the Backrooms bank
    // (ChipArenaPackBackrooms, docs/PACK-BANK.md), the water through NativePoolWater; the blocks near the eye only. The nearest
    // lights light the scene through the practical slots. Without the bank's textures nothing is drawn (the preview is DEV only).
    public sealed partial class ArenaPresentation
    {
        public const int PoolroomsBlockCells=8;public const float PoolroomsReach=46,PoolroomsLampRange=9f;
        public static readonly Vector3 PoolroomsLampColor=new Vector3(.36f,.34f,.3f);
        private static readonly string[] PoolroomsTextures={"backrooms-pool-floor","backrooms-pool-wall","backrooms-pool-basin","backrooms-pool-plaster",null,null};
        private const string PoolroomsWaterNormal="backrooms-pool-water";
        // Each solid surface's tint (multipliers over its texture: the add-on's baked colours as they are) and glow.
        private static readonly Color[] PoolroomsTint={Color.white,Color.white,Color.white,Color.white,Color.white,new Color(1f,.97f,.92f)};
        private static readonly float[] PoolroomsGlow={0,0,0,.12f,0,2.6f};
        private sealed class PoolroomsBlock{public Vector3 Centre;public readonly Mesh[] Meshes=new Mesh[(int)PoolroomsMesh.Surface.Count];}
        private readonly List<PoolroomsBlock> poolroomsBlocks=new List<PoolroomsBlock>();private PoolroomsLayout poolroomsLayout;private Vector3 poolroomsOrigin;
        private Material[] poolroomsMats;private MaterialPropertyBlock poolroomsProps;
        private readonly float[] poolroomsPos=new float[NativeMapLooks.MaxLights*4],poolroomsRgb=new float[NativeMapLooks.MaxLights*3];
        /// <summary>The Poolrooms blocks drawn last frame (budget lines).</summary>
        public int PoolroomsBlocksDrawn{get;private set;}

        private void PoolroomsWorld(NativeArena arena)
        {
            PoolroomsBlocksDrawn=0;if(!NativePoolroomsPreview.Active)return;var eye=WorldEye;if(eye==null)return;
            if(NativePackBanks.Texture(PoolroomsTextures[0])==null)return;
            var p=NativePoolroomsPreview.Origin;
            if(poolroomsLayout==null||poolroomsLayout.Seed!=NativePoolroomsPreview.Seed||poolroomsLayout.Width!=NativePoolroomsPreview.Width||poolroomsLayout.Depth!=NativePoolroomsPreview.Depth||poolroomsOrigin!=p)
                PoolroomsBuild(NativePoolrooms.Preview(NativePoolroomsPreview.Seed,NativePoolroomsPreview.Width,NativePoolroomsPreview.Depth),p);
            if(!PoolroomsMaterials())return;
            var e=NativePoolroomsPreview.Eye??eye.transform.position;float r2=PoolroomsReach*PoolroomsReach;
            foreach(var b in poolroomsBlocks)
            {
                var dc=b.Centre-e;dc.y=0;if(dc.sqrMagnitude>r2)continue;PoolroomsBlocksDrawn++;
                for(int s=0;s<b.Meshes.Length;s++)
                {
                    if(b.Meshes[s]==null)continue;bool solid=s!=(int)PoolroomsMesh.Surface.Water&&s!=(int)PoolroomsMesh.Surface.Light;
                    Graphics.DrawMesh(b.Meshes[s],Matrix4x4.identity,poolroomsMats[s],0,null,0,poolroomsProps,solid?ShadowCastingMode.TwoSided:ShadowCastingMode.Off,solid);
                }
            }
            var origin=new NVector(p.x,p.y,p.z);var at=new NVector(e.x,e.y,e.z);
            PoolroomsWaves(arena,origin,at);
            NativePoolrooms.NearLamps(poolroomsLayout,origin,at,NativeMapLooks.MaxLights,poolroomsPos,poolroomsRgb,new NVector(PoolroomsLampColor.x,PoolroomsLampColor.y,PoolroomsLampColor.z),PoolroomsLampRange);
            float w=poolroomsLayout.Width*PoolroomsLayout.Cell,d=poolroomsLayout.Depth*PoolroomsLayout.Cell;
            if(e.x>p.x&&e.x<p.x+w&&e.z>p.z&&e.z<p.z+d&&e.y<p.y+PoolroomsLayout.Height+PoolroomsLayout.Rise*2)
            {
                NativeLighting.PickedLights(poolroomsPos,poolroomsRgb);
                // DEV: inside the preview no sun reaches; the Poolrooms' even, cool-white wash off the tile and a pale haze down the
                // halls (the level's own map look replaces this once its map exists). The toon style is off there, as in Level 0.
                RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=PoolroomsAmbient;RenderSettings.ambientEquatorColor=PoolroomsAmbient*.95f;RenderSettings.ambientGroundColor=PoolroomsAmbient*1.25f;
                if(RenderSettings.sun!=null){RenderSettings.sun.intensity=0;RenderSettings.sun.shadows=LightShadows.None;}
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=PoolroomsHaze;RenderSettings.fogStartDistance=12;RenderSettings.fogEndDistance=130;
                NativePackStage.Glow(1);if(NativeLighting.StyleActive)NativeLighting.StyleOff();
            }
        }
        // ---- the water's waves (Core PoolWaves, NativePoolWaves; docs/BACKROOMS.md "The water's waves") ----
        private readonly PoolWaves poolWaves=new PoolWaves();private Texture2D poolWaveTex;private int poolWaveEvent;private float poolWaveClock=-1;
        private readonly Dictionary<int,Vector3> poolWaveFeet=new Dictionary<int,Vector3>();private readonly HashSet<int> poolWaveSeen=new HashSet<int>();private readonly List<int> poolWaveGone=new List<int>();
        private static readonly int PoolWavesId=Shader.PropertyToID("_PoolWaves"),PoolWaveRectId=Shader.PropertyToID("_PoolWaveRect");
        private readonly PoolSpray poolSpray=new PoolSpray();private Mesh poolSprayMesh;private Material poolSprayMat;
        private readonly List<Vector3> sprayVerts=new List<Vector3>(),sprayNormals=new List<Vector3>();private readonly List<int> sprayTris=new List<int>();
        /// <summary>The spray's tint (linear multipliers over white) and glow.</summary>
        private static readonly Color PoolSprayTint=new Color(.84f,.95f,.94f);private const float PoolSprayGlow=.4f;
        // The field round the eye: what moves through or drops into the pools this frame (the pilots, the zombies, the shots and
        // blasts since the last frame, the DEV pokes and wader), its steps, and the heights to the water's material.
        private void PoolroomsWaves(NativeArena arena,NVector origin,NVector eye)
        {
            float now=Time.time,dt=poolWaveClock<0?0:Mathf.Min(.1f,now-poolWaveClock);poolWaveClock=now;
            poolWaves.Follow(poolroomsLayout,origin,eye);
            if(arena!=null&&dt>0)
            {
                poolWaveSeen.Clear();
                foreach(var pilot in arena.Pilots)if(pilot.Pose.Alive)PoolWaveBody(pilot.Seat.id,new NVector(pilot.Pose.Position.x,pilot.Pose.Position.y,pilot.Pose.Position.z),dt);
                foreach(var m in arena.Minions)if(m.Body!=null&&m.Hp>0){var q=m.Body.position;PoolWaveBody(1000000+m.Id,new NVector(q.x,q.y,q.z),dt);}
                poolWaveGone.Clear();foreach(int id in poolWaveFeet.Keys)if(!poolWaveSeen.Contains(id))poolWaveGone.Add(id);foreach(int id in poolWaveGone)poolWaveFeet.Remove(id);
                foreach(var e in arena.Events)
                {
                    if(e.Id<=poolWaveEvent)continue;poolWaveEvent=Math.Max(poolWaveEvent,e.Id);var at=new NVector(e.Position.x,e.Position.y,e.Position.z);
                    float k=e.Kind==EventKind.Explosion?NativePoolWaves.Blast(poolWaves,at,e.Radius):e.Kind==EventKind.Impact||e.Kind==EventKind.Ricochet?NativePoolWaves.Shot(poolWaves,at):0;
                    if(k>0)PoolSplash(at,k);
                }
            }
            if(NativePoolroomsPreview.Wading&&dt>0)
            {
                // DEV: the stand-in wader, back and forth between From and To.
                float len=Vector3.Distance(NativePoolroomsPreview.From,NativePoolroomsPreview.To);
                if(len>.01f)
                {
                    float u=Mathf.PingPong(now*NativePoolroomsPreview.Speed,len)/len;var f=Vector3.Lerp(NativePoolroomsPreview.From,NativePoolroomsPreview.To,u);var dir=(NativePoolroomsPreview.To-NativePoolroomsPreview.From)/len;
                    float sign=Mathf.Repeat(now*NativePoolroomsPreview.Speed,2*len)<len?1:-1;var v=dir*NativePoolroomsPreview.Speed*sign;
                    NativePoolWaves.Wake(poolWaves,new NVector(f.x,origin.Y+PoolroomsLayout.WaterY-1,f.z),new NVector(v.x,0,v.z),dt);
                }
            }
            foreach(var k in NativePoolroomsPreview.Pokes){if(poolWaves.Disturb(k.x,k.y,k.w,k.z)&&k.z<-.05f)PoolSplash(new NVector(k.x,origin.Y,k.y),Mathf.Clamp01(-k.z/NativePoolWaves.EntryPush));}
            NativePoolroomsPreview.Pokes.Clear();
            poolWaves.Step(dt);PoolSprayDraw(dt);
            if(poolWaveTex==null)poolWaveTex=new Texture2D(PoolWaves.Size,PoolWaves.Size,TextureFormat.RFloat,false,true){name="Poolrooms waves",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            poolWaveTex.SetPixelData(poolWaves.Height,0);poolWaveTex.Apply(false,false);
            var water=poolroomsMats[(int)PoolroomsMesh.Surface.Water];water.SetTexture(PoolWavesId,poolWaveTex);
            water.SetVector(PoolWaveRectId,new Vector4(origin.X+poolWaves.CornerX*PoolWaves.Texel,origin.Z+poolWaves.CornerZ*PoolWaves.Texel,PoolWaves.Size*PoolWaves.Texel,PoolWaves.Texel));
        }
        // One body: its wake, and a splash when its feet drop through the surface (its vertical speed from its last frame's feet).
        private void PoolWaveBody(int id,NVector feet,float dt)
        {
            poolWaveSeen.Add(id);
            if(poolWaveFeet.TryGetValue(id,out var was))
            {
                var w=new NVector(was.x,was.y,was.z);var v=(feet-w)/dt;
                if(NVector.DistanceSquared(feet,w)<4){NativePoolWaves.Wake(poolWaves,feet,v,dt);float k=NativePoolWaves.Entry(poolWaves,w,feet,v.Y);if(k>0)PoolSplash(feet,k);}
            }
            poolWaveFeet[id]=new Vector3(feet.X,feet.Y,feet.Z);
        }
        // The spray thrown up by a splash of strength k (0..1) at (x, z) of `at`, on the surface.
        private void PoolSplash(NVector at,float k)=>poolSpray.Splash(new NVector(at.X,poolWaves.Origin.Y+PoolroomsLayout.WaterY,at.Z),k);
        // The drops: stepped (those landing ripple the waves) and drawn as one mesh of small diamonds stretched along their flight,
        // lit like the room, a little bright (seen against the tile by every camera).
        private void PoolSprayDraw(float dt)
        {
            poolSpray.Step(dt,poolWaves,poolWaves.Origin.Y+PoolroomsLayout.WaterY);if(poolSpray.Count==0||poolSprayMat==null)return;
            sprayVerts.Clear();sprayNormals.Clear();sprayTris.Clear();
            for(int i=0;i<poolSpray.Count;i++)
            {
                var d=poolSpray.Live[i];var at=new Vector3(d.At.X,d.At.Y,d.At.Z);var v=new Vector3(d.Velocity.X,d.Velocity.Y,d.Velocity.Z);float speed=v.magnitude;
                var axis=speed>.01f?v/speed:Vector3.up;var u=Vector3.Cross(axis,Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right).normalized;var w=Vector3.Cross(axis,u);
                float half=d.Size*.6f+speed*.012f,r=d.Size*.5f;int b=sprayVerts.Count;
                sprayVerts.Add(at+axis*half);sprayNormals.Add(axis);sprayVerts.Add(at-axis*half);sprayNormals.Add(-axis);
                sprayVerts.Add(at+u*r);sprayNormals.Add(u);sprayVerts.Add(at+w*r);sprayNormals.Add(w);sprayVerts.Add(at-u*r);sprayNormals.Add(-u);sprayVerts.Add(at-w*r);sprayNormals.Add(-w);
                for(int k=0;k<4;k++){int a=b+2+k,c=b+2+(k+1)%4;sprayTris.Add(b);sprayTris.Add(a);sprayTris.Add(c);sprayTris.Add(b+1);sprayTris.Add(c);sprayTris.Add(a);}
            }
            if(poolSprayMesh==null){poolSprayMesh=new Mesh{name="Poolrooms spray"};poolSprayMesh.MarkDynamic();}
            poolSprayMesh.Clear();poolSprayMesh.SetVertices(sprayVerts);poolSprayMesh.SetNormals(sprayNormals);poolSprayMesh.SetTriangles(sprayTris,0,true);
            Graphics.DrawMesh(poolSprayMesh,Matrix4x4.identity,poolSprayMat,0,null,0,poolroomsProps,ShadowCastingMode.Off,false);
        }
        private static readonly Color PoolroomsAmbient=new Color(.6f,.6f,.57f),PoolroomsHaze=new Color(.72f,.74f,.72f);
        // Builds every block of the layout once (static meshes).
        private void PoolroomsBuild(PoolroomsLayout l,Vector3 origin)
        {
            PoolroomsRelease();poolroomsLayout=l;poolroomsOrigin=origin;var builder=new PoolroomsMesh();var o=new NVector(origin.x,origin.y,origin.z);int n=PoolroomsBlockCells;float c=PoolroomsLayout.Cell;
            for(int z0=0;z0<l.Depth;z0+=n)for(int x0=0;x0<l.Width;x0+=n)
            {
                builder.Build(l,o,x0,z0,x0+n,z0+n);var b=new PoolroomsBlock{Centre=origin+new Vector3((x0+n*.5f)*c,0,(z0+n*.5f)*c)};
                for(int s=0;s<b.Meshes.Length;s++)b.Meshes[s]=PoolroomsMeshOf(builder.Parts[s],"Poolrooms "+(PoolroomsMesh.Surface)s+" "+x0+","+z0);
                poolroomsBlocks.Add(b);
            }
        }
        private static Mesh PoolroomsMeshOf(PoolroomsMesh.Part p,string name)
        {
            if(p.Indices.Count==0)return null;int n=p.Positions.Count;var v=new Vector3[n];var nr=new Vector3[n];var uv=new Vector2[n];
            for(int i=0;i<n;i++){var q=p.Positions[i];v[i]=new Vector3(q.X,q.Y,q.Z);var m=p.Normals[i];nr[i]=new Vector3(m.X,m.Y,m.Z);uv[i]=new Vector2(p.Uv[i].X,p.Uv[i].Y);}
            var mesh=new Mesh{name=name};if(n>65535)mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=v;mesh.normals=nr;mesh.uv=uv;mesh.SetTriangles(p.Indices,0,true);mesh.UploadMeshData(true);return mesh;
        }
        private void PoolroomsRelease(){foreach(var b in poolroomsBlocks)foreach(var m in b.Meshes)if(m!=null)CapturedAnimation.Release(m);poolroomsBlocks.Clear();}
        // One NativeBlockLit pack material per solid surface (its texture, tint and glow), NativePoolWater for the water, and the
        // draws' constant block values.
        private bool PoolroomsMaterials()
        {
            if(poolroomsMats!=null)return true;var shader=Resources.Load<Shader>("NativeBlockLit");var water=Resources.Load<Shader>("NativePoolWater");if(shader==null||water==null)return false;
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;poolroomsMats=new Material[(int)PoolroomsMesh.Surface.Count];
            for(int s=0;s<poolroomsMats.Length;s++)
            {
                if(s==(int)PoolroomsMesh.Surface.Water)
                {
                    var wm=new Material(water){name="Poolrooms water",renderQueue=3000};var ripples=NativePackBanks.Texture(PoolroomsWaterNormal);
                    if(ripples!=null){ripples.wrapMode=TextureWrapMode.Repeat;wm.SetTexture("_PoolNormal",ripples);}
                    wm.SetFloat("_PoolRepeat",PoolroomsMesh.WaterRepeat);poolroomsMats[s]=wm;continue;
                }
                var m=new Material(shader){name="Poolrooms "+(PoolroomsMesh.Surface)s};m.EnableKeyword(NativePackStage.Keyword);
                var tex=PoolroomsTextures[s]!=null?NativePackBanks.Texture(PoolroomsTextures[s]):null;if(tex!=null)tex.wrapMode=TextureWrapMode.Repeat;
                m.SetTexture(BrTexId,tex!=null?(Texture)tex:Texture2D.whiteTexture);m.SetColor(BrColorId,linear?PoolroomsTint[s].linear:PoolroomsTint[s]);m.SetFloat(BrEmitId,PoolroomsGlow[s]);m.SetFloat(BrCutoffId,-1);
                poolroomsMats[s]=m;
            }
            poolSprayMat=new Material(shader){name="Poolrooms spray"};poolSprayMat.EnableKeyword(NativePackStage.Keyword);poolSprayMat.SetTexture(BrTexId,Texture2D.whiteTexture);
            poolSprayMat.SetColor(BrColorId,linear?PoolSprayTint.linear:PoolSprayTint);poolSprayMat.SetFloat(BrEmitId,PoolSprayGlow);poolSprayMat.SetFloat(BrCutoffId,-1);
            poolroomsProps=new MaterialPropertyBlock();poolroomsProps.SetVector(BrRestId,Vector4.zero);poolroomsProps.SetVector(BrLookId,new Vector4(0,1,0,0));poolroomsProps.SetVector(BrBlockColorId,new Vector4(1,1,1,1));
            return true;
        }
    }
}
