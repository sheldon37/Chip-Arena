using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Belvedere Camp on screen (Core NativeCamp plans it, Core NativeCampFx lights it; the bank ChipArenaCamp, built by
    /// tools/stages/camp_bank.py, holds its meshes; docs/expedition/BELVEDERE-CAMP.md).
    /// <list type="bullet">
    /// <item><b>Live</b> (the generated Expedition hub, and its killcam): part 1 on the fence and overhead. The bank's props are combined
    /// once per run into a few world meshes (each side of the fence, the overhead strings and sails), drawn with ChipArena/StageLit
    /// (Fine within 40 m, Low beyond and on the Low tier), the fires and halos as one effects batch, the camp's share of the practical
    /// slots through NativeLighting.KitLights, and its sound through NativeAudio (Runtime NativeCampAudio.cs).</item>
    /// <item><b>The DEV preview</b> (the Developer walk only, in Editor and development builds): the whole W5 camp from the hub plan
    /// table, its structures, the survivors at their posts, glass that can be seen through, the camp's machines and an overlay of A1's
    /// clearances. It never draws in a live run.</item>
    /// </list>
    /// Without the bank (CI, cloud and public builds, --ca-no-pack-banks) nothing is drawn and the camp is as before. Presentation only:
    /// no collider, no sim data, no wire field. Allocation free per frame once a run's camp is built.
    /// </summary>
    public sealed class NativeCampDraw : IDisposable
    {
        public const float FineReach=40f,Hysteresis=4f;
        private sealed class Chunk{public Mesh Fine,Low;public Bounds Box;public bool Shadow;}
        private readonly List<Chunk> live=new List<Chunk>(),preview=new List<Chunk>();
        private Mesh warLamp,glassMesh;private Matrix4x4 warLampAt;private readonly List<(Mesh mesh,Matrix4x4 at,string instance)> posters=new List<(Mesh,Matrix4x4,string)>(),crosses=new List<(Mesh,Matrix4x4,string)>();
        private Material solid,glow,glass,overlay;private MaterialPropertyBlock lampBlock;private static readonly int GlowId=Shader.PropertyToID("_StageGlow");
        private NativeCamp.Layout layout;private NativeCampFx fx;private NativeFxDraw fxDraw;private bool failed,lit;
        private Mesh overlayMesh;
        /// <summary>The run whose camp is built (its seed and whether the preview is in it).</summary>
        public uint Seed{get;private set;}public bool HasPreview{get;private set;}public int Rescued{get;private set;}=-1;
        public bool Ready=>layout!=null&&solid!=null;
        /// <summary>Last frame: chunks at Fine and Low, triangles, effect quads, lights handed to the slots.</summary>
        public int ShownFine{get;private set;}public int ShownLow{get;private set;}public long Triangles{get;private set;}public int Quads=>fx?.Quads.Count??0;public int Lights=>fx?.LightCount??0;
        public NativeCampFx Fx=>fx;public NativeCamp.Layout Layout=>layout;
        /// <summary>The instances the war board's posters show (the run's deal, doors east to west), and those the view says are cleared.</summary>
        public static readonly HashSet<string> Cleared=new HashSet<string>(StringComparer.Ordinal);
        /// <summary>From the live view each frame (Runtime NativeExpeditionLiveUI.cs ExpeditionLiveSync; null clears).</summary>
        public static void Sync(ExpeditionView v)
        {
            if(v==null){if(Cleared.Count>0)Cleared.Clear();return;}
            foreach(var i in v.instances)if(i!=null&&i.id!=null){if(i.cleared)Cleared.Add(i.id);else Cleared.Remove(i.id);}
        }

        /// <summary>Builds a run's camp: false without the bank or the shaders (nothing draws).</summary>
        public bool Open(uint seed,int plan,string[] deal,bool withPreview,int rescued)
        {
            Release();if(failed)return false;PlanIndex=plan;
            var bank=NativePackBanks.Bank(NativeCamp.Bank);if(bank==null)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeStageLit");if(shader==null||!shader.isSupported)throw new InvalidOperationException("ChipArena/StageLit is missing or unsupported");
                solid=new Material(shader){name="Camp",enableInstancing=true};solid.SetFloat("_Glow",0);
                glow=new Material(shader){name="Camp glow",enableInstancing=true};glow.SetFloat("_Glow",1);lampBlock=new MaterialPropertyBlock();
                layout=NativeCamp.Live(seed);Seed=seed;
                // part 1: each side's props, then the overhead strings and sails (world space in the bank)
                for(int s=0;s<4;s++)
                {
                    var fine=new NativeMeridianMesh();var low=new NativeMeridianMesh();
                    foreach(var p in layout.Pieces)if(p.Side==s&&p.Kind!=NativeCamp.Kind.WarLamp&&p.Kind!=NativeCamp.Kind.WarBoard)Add(bank,p.Key,p.Place,fine,low);
                    if(s==0)Add(bank,"camp/board/war-"+Plan(deal),Find(NativeCamp.Kind.WarBoard).Place,fine,low);
                    Keep(live,fine,low,true,"Camp side "+s);
                }
                {var fine=new NativeMeridianMesh();var low=new NativeMeridianMesh();Add(bank,"camp/static/strands",Identity,fine,low);Keep(live,fine,low,false,"Camp strings");}
                {var fine=new NativeMeridianMesh();var low=new NativeMeridianMesh();Add(bank,"camp/static/sails",Identity,fine,low);Add(bank,"camp/static/cables",Identity,fine,low);Keep(live,fine,low,true,"Camp sails");}
                // the war board's desk lamp (its own draw: it clicks on), and its posters for the dealt instances with their red crosses
                var war=Find(NativeCamp.Kind.WarLamp);{var m=new NativeMeridianMesh();Add(bank,"camp/board/war-lamp",war.Place,m,null);warLamp=Upload(m,"Camp war lamp");warLampAt=Matrix4x4.identity;}
                var board=Find(NativeCamp.Kind.WarBoard);
                for(int slot=0;slot<3;slot++)
                {
                    string instance=deal!=null&&slot+1<deal.Length?deal[slot+1]:null;var family=Family(instance);if(family==null)continue;
                    var at=Mul(board.Place,PosterSlot(slot));
                    var pm=new NativeMeridianMesh();Add(bank,"camp/board/poster-"+family,at,pm,null);var mesh=Upload(pm,"Camp poster "+family);if(mesh!=null)posters.Add((mesh,Matrix4x4.identity,instance));
                    var xm=new NativeMeridianMesh();Add(bank,"camp/board/cross",at,xm,null);var cross=Upload(xm,"Camp cross "+family);if(cross!=null)crosses.Add((cross,Matrix4x4.identity,instance));
                }
                fx=new NativeCampFx(layout);fxDraw=new NativeFxDraw();
                if(withPreview)BuildPreview(bank,seed,rescued);
                HasPreview=withPreview;Rescued=rescued;
                Debug.Log("Belvedere Camp: "+layout.Pieces.Count+" pieces, "+layout.Strands.Count+" strings, "+layout.Bulbs.Count+" bulbs, "+layout.Sails.Count+" sails"+(withPreview?", the DEV preview with "+preview.Count+" groups":""));
                return true;
            }
            catch(Exception e){Debug.LogWarning("Belvedere Camp disabled: "+e.Message);Release();failed=true;return false;}
        }
        private NativeCamp.Piece Find(NativeCamp.Kind kind){foreach(var p in layout.Pieces)if(p.Kind==kind)return p;return null;}
        private static readonly float[] Identity={1,0,0,0,0,1,0,0,0,0,1,0};
        /// <summary>The run's street plan (its index in NativeCityLayout.Plans): the war board is painted with its map.</summary>
        public int PlanIndex{get;private set;}
        private string Plan(string[] deal)=>Math.Max(0,Math.Min(4,PlanIndex)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        /// <summary>An instance's poster family (NativeCityBleed's theme ids), or null.</summary>
        public static string Family(string instance)
        {
            switch(instance)
            {
                case "exp-backrooms":case "Level 0":return "level0";
                case "exp-poolrooms":case "The Poolrooms":return "poolrooms";
                case "exp-ward-zero":case "Ward Zero":return "wardzero";
                case "exp-transit":case "Central Station":return "station";
                case "exp-foundry":case "City Ironworks":return "ironworks";
            }
            return null;
        }
        /// <summary>The poster slots on the war board (board frame, rows: x across, y up, z into the fence): right of the map, top to bottom.</summary>
        public static float[] PosterSlot(int slot)=>new float[]{1,0,0,1.28f, 0,1,0,2.06f-.46f*slot, 0,0,1,0};
        private static float[] Mul(float[] a,float[] b)
        {
            var o=new float[12];
            for(int r=0;r<3;r++){for(int c=0;c<3;c++)o[r*4+c]=a[r*4]*b[c]+a[r*4+1]*b[4+c]+a[r*4+2]*b[8+c];o[r*4+3]=a[r*4]*b[3]+a[r*4+1]*b[7]+a[r*4+2]*b[11]+a[r*4+3];}
            return o;
        }
        // A bank prop's Fine ("/0") and Low ("/2"; Fine where missing) meshes into the chunks' mesh data at a place.
        private static void Add(NativePackBank.Bank bank,string key,float[] place,NativeMeridianMesh fine,NativeMeridianMesh low)
        {
            var f=bank.Find(key+"/0");var l=bank.Find(key+"/2")??f;
            if(f!=null&&fine!=null)foreach(var part in f.Parts)NativeCampMeshes.Append(bank,part,place,fine);
            if(l!=null&&low!=null)foreach(var part in l.Parts)NativeCampMeshes.Append(bank,part,place,low);
        }
        private void Keep(List<Chunk> into,NativeMeridianMesh fine,NativeMeridianMesh low,bool shadow,string name)
        {
            if(fine.Vertices==0)return;var c=new Chunk{Fine=Upload(fine,name+" fine"),Low=low!=null&&low.Vertices>0?Upload(low,name+" low"):null,Shadow=shadow};
            c.Box=c.Fine.bounds;into.Add(c);
        }
        private static Mesh Upload(NativeMeridianMesh d,string name)
        {
            int n=d.Vertices;if(n==0)return null;var v=new Vector3[n];var nr=new Vector3[n];var a=new Vector2[n];var b=new Vector2[n];var c=new Color32[n];
            for(int i=0;i<n;i++){v[i]=new Vector3(d.P[i*3],d.P[i*3+1],d.P[i*3+2]);nr[i]=new Vector3(d.N[i*3],d.N[i*3+1],d.N[i*3+2]);a[i]=new Vector2(d.Uv0[i*2],d.Uv0[i*2+1]);b[i]=new Vector2(d.Uv1[i*2],d.Uv1[i*2+1]);c[i]=new Color32(d.C[i*4],d.C[i*4+1],d.C[i*4+2],d.C[i*4+3]);}
            var m=new Mesh{name=name};if(n>65535)m.indexFormat=IndexFormat.UInt32;m.vertices=v;m.normals=nr;m.uv=a;m.uv2=b;m.colors32=c;
            m.subMeshCount=2;m.SetTriangles(d.Solid,0,false);m.SetTriangles(d.Glow,1,false);
            m.bounds=new Bounds(new Vector3((d.MinX+d.MaxX)*.5f,(d.MinY+d.MaxY)*.5f,(d.MinZ+d.MaxZ)*.5f),new Vector3(d.MaxX-d.MinX+.1f,d.MaxY-d.MinY+.1f,d.MaxZ-d.MinZ+.1f));
            m.UploadMeshData(true);return m;
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // The DEV preview (Editor and development builds; the Developer walk only)
        // ---------------------------------------------------------------------------------------------------------------------
        private void BuildPreview(NativePackBank.Bank bank,uint seed,int rescued)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var shown=NativeCamp.Preview(seed,rescued);var fine=new NativeMeridianMesh();var low=new NativeMeridianMesh();var figures=new NativeMeridianMesh();var glassData=new NativeMeridianMesh();
            foreach(var s in shown)
            {
                double r=s.Yaw*Math.PI/180;float co=(float)Math.Cos(r),si=(float)Math.Sin(r);var place=new[]{co,0,si,s.At.x,0,1,0,s.At.y,-si,0,co,s.At.z};
                if(s.Key.StartsWith("camp/figure/",StringComparison.Ordinal)){Add(bank,s.Key,place,figures,null);continue;}
                Add(bank,s.Key,place,fine,low);
                var g=bank.Find("camp/glass/"+s.Key.Substring("camp/row/".Length));if(g!=null)foreach(var part in g.Parts)NativeCampMeshes.Append(bank,part,place,glassData);
                if(fine.Vertices>40000){Keep(preview,fine,low,true,"Camp preview");fine=new NativeMeridianMesh();low=new NativeMeridianMesh();}
            }
            Keep(preview,fine,low,true,"Camp preview");Keep(preview,figures,null,true,"Camp survivors");
            if(glassData.Vertices>0)
            {
                // the panes a survivor stands behind: tinted, see-through (StageLit is opaque, art bible 4.1)
                for(int i=0;i<glassData.Vertices;i++){glassData.C[i*4]=(byte)(glassData.C[i*4]*.6f);glassData.C[i*4+1]=(byte)(glassData.C[i*4+1]*.7f);glassData.C[i*4+2]=(byte)(glassData.C[i*4+2]*.8f);glassData.C[i*4+3]=56;}
                glassData.Solid.AddRange(glassData.Glow);glassData.Glow.Clear();glassMesh=Upload(glassData,"Camp glass");
                var gs=Resources.Load<Shader>("NativeFieldGlow");if(gs!=null){glass=new Material(gs){name="Camp glass",renderQueue=3000};glass.SetFloat("_Radial",0);glass.SetFloat("_Cull",0);glass.SetColor("_Color",Color.white);}
            }
            overlayMesh=OverlayMesh(seed);
            var os=Resources.Load<Shader>("NativeFieldGlow");if(os!=null&&overlayMesh!=null){overlay=new Material(os){name="Camp clearances",renderQueue=3001};overlay.SetFloat("_Radial",0);overlay.SetFloat("_Cull",0);overlay.SetFloat("_Offset",-1);overlay.SetColor("_Color",Color.white);}
#endif
        }
        /// <summary>DEV: the preview's clearance overlay (A1's blocks, the gates' pockets and lanes, the ring's walkway, the landing field).</summary>
        public static bool Clearances;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // The overlay's boxes (Core NativeCampOverlay) as one see-through mesh, each face in its box's colour.
        private static Mesh OverlayMesh(uint seed)
        {
            var boxes=NativeCampOverlay.Boxes(seed);var v=new List<Vector3>();var c=new List<Color>();var uv=new List<Vector2>();var t=new List<int>();
            foreach(var b in boxes)
            {
                var col=new Color(((b.Hex>>16)&255)/255f,((b.Hex>>8)&255)/255f,(b.Hex&255)/255f,b.Alpha);
                Vector3 lo=new Vector3(b.Lo.x,b.Lo.y+.01f,b.Lo.z),hi=new Vector3(b.Hi.x,b.Hi.y+.01f,b.Hi.z);
                void Face(Vector3 a,Vector3 bb,Vector3 cc,Vector3 d){int i=v.Count;v.Add(a);v.Add(bb);v.Add(cc);v.Add(d);for(int k=0;k<4;k++){c.Add(col);uv.Add(Vector2.zero);}t.Add(i);t.Add(i+1);t.Add(i+2);t.Add(i);t.Add(i+2);t.Add(i+3);}
                Face(new Vector3(lo.x,hi.y,lo.z),new Vector3(hi.x,hi.y,lo.z),new Vector3(hi.x,hi.y,hi.z),new Vector3(lo.x,hi.y,hi.z));
                if(hi.y-lo.y<.05f)continue;
                Face(new Vector3(lo.x,lo.y,lo.z),new Vector3(hi.x,lo.y,lo.z),new Vector3(hi.x,hi.y,lo.z),new Vector3(lo.x,hi.y,lo.z));
                Face(new Vector3(lo.x,lo.y,hi.z),new Vector3(hi.x,lo.y,hi.z),new Vector3(hi.x,hi.y,hi.z),new Vector3(lo.x,hi.y,hi.z));
                Face(new Vector3(lo.x,lo.y,lo.z),new Vector3(lo.x,lo.y,hi.z),new Vector3(lo.x,hi.y,hi.z),new Vector3(lo.x,hi.y,lo.z));
                Face(new Vector3(hi.x,lo.y,lo.z),new Vector3(hi.x,lo.y,hi.z),new Vector3(hi.x,hi.y,hi.z),new Vector3(hi.x,hi.y,lo.z));
            }
            var m=new Mesh{name="Camp clearances"};if(v.Count>65535)m.indexFormat=IndexFormat.UInt32;m.SetVertices(v);m.SetColors(c);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateBounds();m.UploadMeshData(true);return m;
        }
#endif

        // ---------------------------------------------------------------------------------------------------------------------
        // Each frame
        // ---------------------------------------------------------------------------------------------------------------------
        /// <summary>Draws the camp for this frame's eye (`pilot`: the eye's pilot's feet, for the war board's lamp).</summary>
        public void Frame(Camera eye,float time,Vector3? pilot,bool low,bool reduced,float quality,bool showPreview,float dt)
        {
            if(!Ready||eye==null)return;var at=eye.transform.position;ShownFine=ShownLow=0;Triangles=0;
            Draw(live,at,low);if(showPreview&&HasPreview)Draw(preview,at,low);
            if(showPreview&&glassMesh!=null&&glass!=null)Graphics.DrawMesh(glassMesh,Matrix4x4.identity,glass,0,null,0,null,ShadowCastingMode.Off,false);
            if(showPreview&&Clearances&&overlayMesh!=null&&overlay!=null)Graphics.DrawMesh(overlayMesh,Matrix4x4.identity,overlay,0,null,0,null,ShadowCastingMode.Off,false);
            // the posters and the crosses on the cleared ones
            foreach(var p in posters)DrawMesh(p.mesh,ShadowCastingMode.Off);
            foreach(var c in crosses)if(c.instance!=null&&Cleared.Contains(c.instance))DrawMesh(c.mesh,ShadowCastingMode.Off);
            var t=eye.transform;Vector3 r=t.right,u=t.up;
            fx.Brazier=showPreview&&HasPreview;
            fx.Build(time,at.x,at.y,at.z,r.x,r.y,r.z,u.x,u.y,u.z,pilot.HasValue?new V3(pilot.Value.x,pilot.Value.y,pilot.Value.z):(V3?)null,quality,low,reduced,dt);
            if(warLamp!=null){float k=fx.BoardLamp?1:0;lampBlock.SetVector(GlowId,new Vector4(k,k,k,1));Graphics.DrawMesh(warLamp,warLampAt,solid,0,null,0,lampBlock,ShadowCastingMode.On,true);if(warLamp.subMeshCount>1)Graphics.DrawMesh(warLamp,warLampAt,glow,0,null,1,lampBlock,ShadowCastingMode.Off,true);}
            NativeLighting.KitLights(fx.LightPos,fx.LightRgb,fx.LightCount,at);lit=fx.LightCount>0;
            DrawFx(eye,time);
            NativeCampAudioFeed.Feed(layout,fx,Time.frameCount);
        }
        private void Draw(List<Chunk> chunks,Vector3 at,bool low)
        {
            foreach(var c in chunks)
            {
                float d=Mathf.Sqrt(c.Box.SqrDistance(at));var mesh=low||d>FineReach||c.Fine==null?(c.Low??c.Fine):c.Fine;if(mesh==null)continue;
                if(mesh==c.Fine)ShownFine++;else ShownLow++;
                DrawMesh(mesh,c.Shadow?ShadowCastingMode.On:ShadowCastingMode.Off);
            }
        }
        private void DrawMesh(Mesh mesh,ShadowCastingMode shadows)
        {
            Graphics.DrawMesh(mesh,Matrix4x4.identity,solid,0,null,0,null,shadows,true);
            if(mesh.subMeshCount>1&&mesh.GetSubMesh(1).indexCount>0)Graphics.DrawMesh(mesh,Matrix4x4.identity,glow,0,null,1,null,ShadowCastingMode.Off,true);
        }
        private void DrawFx(Camera camera,float time)
        {
            var q=fx.Quads;fxDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?fxDraw.Solid:fxDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            fxDraw.Draw(camera,null,time);
        }
        /// <summary>Nothing this frame (the map is not the camp's): the slots and the sound are let go, once (another map's hazard kit
        /// keeps its own share of the slots).</summary>
        public void Idle(){if(lit){NativeLighting.KitLights(null,null,0,Vector3.zero);lit=false;}if(NativeCampAudioFeed.Layout!=null)NativeCampAudioFeed.Feed(null,null,Time.frameCount);}

        private void Release()
        {
            foreach(var c in live){Destroy(c.Fine);Destroy(c.Low);}foreach(var c in preview){Destroy(c.Fine);Destroy(c.Low);}live.Clear();preview.Clear();
            Destroy(warLamp);Destroy(glassMesh);Destroy(overlayMesh);warLamp=glassMesh=overlayMesh=null;foreach(var p in posters)Destroy(p.mesh);foreach(var c in crosses)Destroy(c.mesh);posters.Clear();crosses.Clear();
            if(solid!=null)UnityEngine.Object.Destroy(solid);if(glow!=null)UnityEngine.Object.Destroy(glow);if(glass!=null)UnityEngine.Object.Destroy(glass);if(overlay!=null)UnityEngine.Object.Destroy(overlay);solid=glow=glass=overlay=null;
            fxDraw?.Dispose();fxDraw=null;fx=null;layout=null;HasPreview=false;Rescued=-1;
        }
        private static void Destroy(Mesh m){if(m!=null)UnityEngine.Object.Destroy(m);}
        public void Dispose(){Release();failed=false;}
    }

    /// <summary>The camp's sound this frame, from the drawer to NativeAudio (Runtime NativeCampAudio.cs): stale after a frame.</summary>
    public static class NativeCampAudioFeed
    {
        public static NativeCamp.Layout Layout;public static bool Click,Lamp;public static int Frame=-10;
        public static void Feed(NativeCamp.Layout l,NativeCampFx fx,int frame)
        {
            Layout=l;Frame=l==null?-10:frame;if(fx!=null&&fx.BoardLampClicked){Click=true;Lamp=fx.BoardLamp;}
        }
    }

    public sealed partial class ArenaPresentation
    {
        private NativeCampDraw camp;private float campLast=-1;
        /// <summary>From SourceWorld (one line): Belvedere Camp on the generated Expedition hub (live, and its killcam), and in the
        /// Developer walk with its DEV preview (Editor and development builds only). Every other map: nothing, and the camp lets go of
        /// its light slots and sound.</summary>
        private void CampScenery(NativeArena arena)
        {
            var run=GeneratedRun?.World;bool live=run?.City!=null&&arena.MapId==run.Map.id,walk=false,show=false;
            uint seed=0;int plan=0,rescued=0;string[] deal=null;
            if(live){seed=run.Blueprint.worldSeed;plan=GeneratedRun.Ticket?.cityPlan??0;deal=run.HallSlots;}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else if(NativeCampWalk.Active(arena.MapId)){walk=true;seed=NativeCampWalk.Seed;plan=NativeCampWalk.Plan;deal=NativeCampWalk.Deal;rescued=NativeCampWalk.Rescued;show=NativeCampWalk.Preview;}
#endif
            if(!live&&!walk){camp?.Idle();campLast=-1;return;}
            camp??=new NativeCampDraw();
            if(!camp.Ready||camp.Seed!=seed||camp.PlanIndex!=plan||camp.HasPreview!=walk||walk&&camp.Rescued!=rescued){if(!camp.Open(seed,plan,deal,walk,rescued))return;}
            float now=PresentationClock(arena),dt=campLast<0?0:Mathf.Clamp(now-campLast,0,.1f);campLast=now;
            Vector3? pilot=arena.ViewPilot?.Body!=null?arena.ViewPilot.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight:(Vector3?)null;
            camp.Frame(WorldEye,now,pilot,LowGraphics,EffectsQuality<.5f,EffectsQuality,show,dt);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(show)CampPreviewMachines(arena,now);
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // The camp's two machines in the preview (their real models, idling), where A1 puts them (plan R10).
        private PveMachine[] campMachines;
        private void CampPreviewMachines(NativeArena arena,float t)
        {
            if(sourceModels==null)return;
            campMachines??=NativeCampOverlay.Machines();
            foreach(var m in campMachines)
            {
                var root=Matrix4x4.TRS(NativeArena.Vec(NativeMachineRules.Root(m)),Quaternion.Euler(0,m.yaw*Mathf.Rad2Deg,0),Vector3.one);
                float level=m.kind=="med"?.8f+.2f*Mathf.Sin(t*1.3f+m.m):.75f+.25f*Mathf.Sin(t*2.1f+m.m*1.7f);var tint=new Color(level,level,level);
                sourceModels.Queue(sourceModels.MachineModel(NativeMachineModels.BodyId(m.kind),"machine-"+m.kind),root,b=>root,partTint:b=>b==1?tint:Color.white);
            }
        }
#endif
    }
}
