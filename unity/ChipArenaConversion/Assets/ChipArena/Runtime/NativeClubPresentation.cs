using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Substation 128's presentation (docs/arenas/CLUB.md), drawn only while the map is `club`: in the arena and the killcam
    /// replay (SourceWorld, on the drawn arena's field clock, so the telegraphs stand where the simulation's states put them on
    /// every client and in the killcam). Every other map is untouched: nothing is created until such a frame. Never sim data and
    /// never a collider.
    ///  - The look (light rig, night sky through the windows, fog, practical lights, block looks, the style, the advanced
    ///    lighting with its polished-floor sheen) is MapLooks.json's `club-substation`, applied like any dressed map's.
    ///  - The static kit (Core NativeClubKit) packed once into casting and shadowless NativeBlockLit batches of 500; the rig
    ///    (Core NativeClubRig: the LED floor, the screens, the cones, the moving heads, the laser posts, the mirror balls, the DJ,
    ///    the crowd) refilled each frame into its own batches, its HDR colours straight from the show.
    ///  - The backdrop (Core NativeClubScenery): SourceLit sector meshes on NativeSceneryBuilder.BackdropLayer without shadows.
    ///  - The effects (Core NativeClubFx): one NativeFxDraw batch; the volumetric beams take the moving heads, the fans, the
    ///    laser wall and the pin spots while the look's beams are on above the low tier (ArenaPresentation.AdvancedBeams).
    ///  - The show's three lights go to the arena's practical slots (NativeLighting.KitLights): 3 on High, 2 on Balanced, 1 on Low;
    ///    actor lighting is on each frame, so the pilots take the slots too (the dark club's bodies read by its lamps).
    ///  - The stage art (docs/arenas/STAGE-ART.md): with the stage bank ChipArenaStageClub, NativeStageArtDraw draws the modelled
    ///    hall, furniture and rig (Core NativeSubstationArt poses it from the rig: the cones, the moving heads, the laser posts,
    ///    the mirror balls, the Grey DJ and the crowd of Greys) instead of the kit's and the rig's boxes and the backdrop's near
    ///    sector; the lit screens (the floor's tiles, the LED wall, the cube) stay the rig's. Without the bank every box draws.
    ///  - The music's pulse (Core NativeClubPulse) from the music voice's spectrum (NativeClubMusic, NativeClubAudio.cs), or the
    ///    steady 128 bpm clock. Presentation only.
    /// </summary>
    public sealed class NativeClubDraw : MonoBehaviour
    {
        private sealed class Batch {public readonly Matrix4x4[] M=new Matrix4x4[NativeBlockLooks.Chunk];public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Rest=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk];public int Count;public bool Shadow;}
        private Mesh Block=>NativeLighting.StyleActive?(blockToon??=MeshOf(NativeToonMesh.Build())):block;private Mesh blockToon;
        private Mesh block;private Material lit,sceneryLit;private MaterialPropertyBlock props;private bool failed;
        private Batch[] kit=Array.Empty<Batch>();private readonly Batch[] rig={new Batch{Shadow=true},new Batch(),new Batch(),new Batch(),new Batch()};
        private ContentMap built;private NativeClubRig rigBuilder;private Mesh[] sectors=Array.Empty<Mesh>();
        private NativeFxDraw fxDraw;private readonly NativeClubFx fx=new NativeClubFx();private readonly NativeClubShow show=new NativeClubShow();private readonly NativeClubPulse pulse=new NativeClubPulse();
        private readonly float[] lightPos=new float[12],lightRgb=new float[9];private float lastRender=float.NaN;
        private NativeStageArtDraw art;private readonly NativeStageArtPose artPose=new NativeStageArtPose();
        /// <summary>The stage art drew last frame (the kit's boxes, the rig's modelled parts and the near backdrop did not).</summary>
        public bool ArtDrawn{get;private set;}
        private static readonly int SizeId=Shader.PropertyToID("_BlockSize"),RestId=Shader.PropertyToID("_BlockRest"),ColorId=Shader.PropertyToID("_BlockColor"),LookId=Shader.PropertyToID("_BlockLook");
        /// <summary>The frame's volumetric beam list (null: the effects' billboards) and the light-flash setting.</summary>
        public NativeBeamList Beams;public NativeKitFlash Flash=NativeKitFlash.Full;
        /// <summary>The last frame's NativeBlockLit draws and instances (kit and rig), the effect quads, whether the lights were lent.</summary>
        public int Draws{get;private set;}public int Instances{get;private set;}public int Quads=>fx.Quads.Count;public bool LightsSent{get;private set;}
        public NativeClubShow Show=>show;public NativeClubPulse Pulse=>pulse;
        private bool Ensure()
        {
            if(lit!=null)return true;if(failed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");var source=Resources.Load<Shader>("NativeSourceLit");if(shader==null||source==null)throw new InvalidOperationException("NativeBlockLit or NativeSourceLit is missing");
                lit=new Material(shader){name="Club kit",enableInstancing=true};props=new MaterialPropertyBlock();block=MeshOf(NativeBlockLooks.Build(0));
                sceneryLit=new Material(source){name="Club scenery",enableInstancing=true};sceneryLit.SetFloat("_StageLit",0);sceneryLit.SetFloat("_ToonEdgeRef",16);fxDraw=new NativeFxDraw();return true;
            }
            catch(Exception e){Debug.LogWarning("Club presentation disabled: "+e.Message);Release();failed=true;return false;}
        }
        /// <summary>The map's kit, rig, show, backdrop (once per map content; ArenaPresentation.PrewarmStage calls it behind the
        /// loading screen). Nothing is drawn.</summary>
        public bool Prepare(NativeArena arena)
        {
            if(arena==null||!Ensure())return false;var map=arena.Map;if(map==null||!NativeClubLooks.Active(map.id))return false;
            if(art==null)art=gameObject.AddComponent<NativeStageArtDraw>();art.Prepare(map,NativeSubstationArt.Slots,NativeSubstationArt.Casts);if(ReferenceEquals(map,built))return true;
            built=map;
            try
            {
                var look=NativeLighting.Look(map);var looks=NativeClubLooks.Resolve(look.kitLooks);var k=NativeClubKit.Build(map,look);Pack(k.Parts);
                rigBuilder=new NativeClubRig(looks);show.Prepare(map);pulse.Reset();
                foreach(var m in sectors)if(m!=null)CapturedAnimation.Release(m);var meshes=NativeClubScenery.Build(map);sectors=new Mesh[meshes.Length];for(int i=0;i<meshes.Length;i++)sectors[i]=SceneryMesh(meshes[i],"Club scenery "+i);
            }
            catch(Exception e){Debug.LogWarning("Club dressing disabled: "+e.Message);kit=Array.Empty<Batch>();rigBuilder=null;sectors=Array.Empty<Mesh>();}
            return true;
        }
        /// <summary>One arena frame. now: the drawn arena's field clock; quality: effects quality; low: graphics=performance.</summary>
        public void Render(NativeArena arena,float now,Camera camera,float quality,bool low,bool instancing)
        {
            Draws=Instances=0;LightsSent=false;if(!Prepare(arena))return;var map=arena.Map;var match=arena.Match;int round=NativeStructureFx.RoundIndex(match.Set,match.Round);
            const int layer=NativeSceneryBuilder.BackdropLayer;
            // the pulse: the song's spectrum when it was heard this frame, else the bar grid's clock (eased between them)
            double dt=float.IsNaN(lastRender)?0:Math.Max(0,Math.Min(.1,now-lastRender));lastRender=now;
            var flash=quality<.5f&&Flash==NativeKitFlash.Full?NativeKitFlash.Reduced:Flash;double beats=NativeClubLooks.Beats(match.Phase,match.FightAt,now,now);
            if(NativeClubMusic.Fresh)pulse.Feed(NativeClubMusic.Spectrum,NativeClubPulse.Bins,NativeClubMusic.Rate,dt,beats,flash);else pulse.Feed(null,0,0,dt,beats,flash);
            show.Build(round,match.Phase,match.FightAt,now,now,pulse,flash);
            // the art (the modelled hall and rig) in place of the kit's boxes, the rig's modelled parts and the near backdrop;
            // the lit screens stay the rig's either way
            ArtDrawn=false;if(rigBuilder!=null)rigBuilder.Build(show,now,low,quality);
            if(art!=null&&art.Ready&&rigBuilder!=null){NativeSubstationArt.Pose(rigBuilder,show,now,artPose);ArtDrawn=art.Render(arena,camera,low,artPose);}
            for(int i=0;i<sectors.Length;i++)if(!(ArtDrawn&&i==NativeClubScenery.Near))Graphics.DrawMesh(sectors[i],Matrix4x4.identity,sceneryLit,layer,null,0,null,ShadowCastingMode.Off,true);
            if(!ArtDrawn)foreach(var b in kit)Draw(b,instancing);
            if(rigBuilder!=null){Fill(rigBuilder,ArtDrawn?rigBuilder.Screens:rigBuilder.Parts.Count);foreach(var b in rig)Draw(b,instancing);}
            Effects(arena,now,camera,quality,low);
            if(camera!=null)
            {
                int n=low?1:NativeAdvancedLighting.Tier==NativeGraphicsTier.High?3:2;
                Array.Copy(show.LightPos,lightPos,12);Array.Copy(show.LightRgb,lightRgb,9);NativeLighting.KitLights(lightPos,lightRgb,Math.Min(n,show.LightCount),camera.transform.position);LightsSent=true;
            }
        }
        private void Effects(NativeArena arena,float now,Camera camera,float quality,bool low)
        {
            if(camera==null||fxDraw==null)return;var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;
            fx.Beams=Beams;fx.Build(show,now,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,Mathf.Clamp01(quality),low);
            var q=fx.Quads;fxDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var target=q.Layers[i]==0?fxDraw.Solid:fxDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=target.Count;
                for(int v=0;v<4;v++)target.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                target.Triangle(a,a+1,a+2);target.Triangle(a,a+2,a+3);
            }
            fxDraw.Draw(camera,null,now);
        }
        // The static kit into batches of 500 by shadow mode, packed once per map.
        private void Pack(NativeFactoryKit.Writer parts)
        {
            int casters=0,flat=0;for(int i=0;i<parts.Count;i++)if(parts.Items[i].Shadow)casters++;else flat++;
            int nc=(casters+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk,nf=(flat+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk;kit=new Batch[nc+nf];
            for(int i=0;i<kit.Length;i++)kit[i]=new Batch{Shadow=i<nc};int ci=0,fi=0;
            for(int i=0;i<parts.Count;i++){ref var p=ref parts.Items[i];var b=p.Shadow?kit[ci/NativeBlockLooks.Chunk]:kit[nc+fi/NativeBlockLooks.Chunk];if(p.Shadow)ci++;else fi++;Put(b,b.Count++,ref p,float.NaN,0,0);}
        }
        // The frame's rig: casters into the first batch, shadowless parts into the other four (Capacity 1800 fits 2500).
        private void Fill(NativeClubRig r,int count)
        {
            foreach(var b in rig)b.Count=0;var parts=r.Parts;
            for(int i=0;i<count;i++)
            {
                ref var p=ref parts.Items[i];Batch b=null;
                if(p.Shadow&&rig[0].Count<NativeBlockLooks.Chunk)b=rig[0];else for(int k=1;k<rig.Length;k++)if(rig[k].Count<NativeBlockLooks.Chunk){b=rig[k];break;}
                if(b==null)continue;Put(b,b.Count++,ref p,r.Rgb[i*3],r.Rgb[i*3+1],r.Rgb[i*3+2]);
            }
        }
        // One part: an HDR gain in the window value over 1 (emissive kit parts), or a linear colour from the rig (NaN: the kind's).
        private static void Put(Batch b,int i,ref FactoryPart p,float lr,float lg,float lb)
        {
            b.M[i]=Matrix4x4.TRS(new Vector3(p.X,p.Y,p.Z),new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw),Vector3.one);
            float c=NativeFactoryKit.Bevel(p.Hx,p.Hy,p.Hz);b.Size[i]=new Vector4(p.Hx,p.Hy,p.Hz,c);b.Rest[i]=new Vector4(p.Rx,p.Ry,p.Rz,0);
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;Color col;float gain=p.Window>1?p.Window:1;
            if(!float.IsNaN(lr)){col=new Color(lr,lg,lb);if(!linear)col=col.gamma;}
            else{col=NativeArena.ColorOf(p.Color);if(linear)col=col.linear;col*=gain;}
            b.Color[i]=new Vector4(col.r,col.g,col.b,Math.Min(1,p.Window));b.Look[i]=new Vector4(p.Look,1,p.Seed,p.Flags);
        }
        private void Draw(Batch b,bool instancing)
        {
            if(b.Count==0)return;var shadow=b.Shadow?ShadowCastingMode.On:ShadowCastingMode.Off;Draws++;Instances+=b.Count;
            if(instancing)
            {
                props.Clear();props.SetVectorArray(SizeId,b.Size);props.SetVectorArray(RestId,b.Rest);props.SetVectorArray(ColorId,b.Color);props.SetVectorArray(LookId,b.Look);
                try{Graphics.DrawMeshInstanced(Block,0,lit,b.M,b.Count,props,shadow,true,0,null);return;}catch(InvalidOperationException){}catch(ArgumentException){}
            }
            for(int i=0;i<b.Count;i++){props.Clear();props.SetVector(SizeId,b.Size[i]);props.SetVector(RestId,b.Rest[i]);props.SetVector(ColorId,b.Color[i]);props.SetVector(LookId,b.Look[i]);Graphics.DrawMesh(Block,b.M[i],lit,0,null,0,props,shadow,true);}
        }
        private static Mesh MeshOf(NativeBlockLooks.StageMesh s)
        {
            int n=s.Vertices;var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var a=new System.Collections.Generic.List<Vector4>(n);var b=new System.Collections.Generic.List<Vector4>(n);
            for(int i=0;i<n;i++){v[i]=new Vector3(s.Preview[i*3],s.Preview[i*3+1],s.Preview[i*3+2]);normals[i]=new Vector3(s.Normal[i*3],s.Normal[i*3+1],s.Normal[i*3+2]);colors[i]=new Color(s.Shade[i*4],s.Shade[i*4+1],s.Shade[i*4+2],s.Shade[i*4+3]);
                a.Add(new Vector4(s.Lattice[i*4],s.Lattice[i*4+1],s.Lattice[i*4+2],0));b.Add(new Vector4(s.Inset[i*4],s.Inset[i*4+1],s.Inset[i*4+2],0));}
            var mesh=new Mesh{name="Club kit block"};mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.SetUVs(0,a);mesh.SetUVs(1,b);mesh.triangles=s.Triangles;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*400);mesh.UploadMeshData(true);return mesh;
        }
        private static Mesh SceneryMesh(NativeSceneryMesh m,string name)
        {
            int n=m.Vertices;var p=new Vector3[n];var nr=new Vector3[n];var c=new Color[n];var e=new Vector4[n];
            for(int i=0;i<n;i++){p[i]=new Vector3(m.Positions[i*3],m.Positions[i*3+1],m.Positions[i*3+2]);nr[i]=new Vector3(m.Normals[i*3],m.Normals[i*3+1],m.Normals[i*3+2]);c[i]=new Color(m.Colors[i*4],m.Colors[i*4+1],m.Colors[i*4+2],m.Colors[i*4+3]);e[i]=new Vector4(m.Emission[i*4],m.Emission[i*4+1],m.Emission[i*4+2],m.Emission[i*4+3]);}
            var mesh=new Mesh{name=name,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=p;mesh.normals=nr;mesh.colors=c;mesh.SetUVs(1,e);mesh.triangles=m.Indices;
            mesh.bounds=new Bounds(new Vector3((m.MinX+m.MaxX)/2,(m.MinY+m.MaxY)/2,(m.MinZ+m.MaxZ)/2),new Vector3(m.MaxX-m.MinX,m.MaxY-m.MinY,m.MaxZ-m.MinZ));mesh.UploadMeshData(true);return mesh;
        }
        private void Release()
        {
            foreach(var m in sectors)if(m!=null)CapturedAnimation.Release(m);sectors=Array.Empty<Mesh>();
            foreach(var o in new UnityEngine.Object[]{block,blockToon,lit,sceneryLit})if(o!=null)CapturedAnimation.Release(o);block=blockToon=null;lit=sceneryLit=null;fxDraw?.Dispose();fxDraw=null;built=null;
        }
        private void OnDestroy()=>Release();
    }
    public sealed partial class ArenaPresentation
    {
        private NativeClubDraw club;private bool clubLights;
        // SourceWorld (the arena or the killcam replay arena): Substation 128's kit, rig, backdrop, effects and lent lights.
        private void ClubScenery(NativeArena arena)
        {
            if(!NativeClubLooks.Active(arena.MapId)){if(clubLights){NativeLighting.KitLights(null,null,0,Vector3.zero);clubLights=false;}return;}
            if(club==null)club=gameObject.AddComponent<NativeClubDraw>();
            // the bodies take the practical slots too (the show's coloured lights on the pilots; Tests/ZombieLightChecks)
            NativeLighting.ActorLights(true);
            club.Beams=AdvancedBeams;club.Flash=KitFlash;club.Render(arena,FieldClock(arena),WorldEye,EffectsQuality,LowGraphics,UsesInstancing);clubLights|=club.LightsSent;
        }
        // The laser wall's kill (a `maglev` death on the club): a level cut through the shins where its beams run, the body
        // lifting off its legs and thrown along the sweep (NativeBisect's shared split; gore on and a stock ending only, as the
        // saws'). Presentation only, from the replicated death record and the pure clock.
        private NativeCorpse ClubLaserBisect(NativeArena arena,NativeDeath death,CapturedAnimation.Player rig)
        {
            if(sourceModels==null||death==null||death.damageKind!="maglev"||!NativeClubLooks.Active(arena.MapId)||!NativeSmashGib.Applies(Gore,death.style,death.kept,death.model))return null;
            MapHazard laser=null;foreach(var h in arena.Map?.hazards??Array.Empty<MapHazard>())if(h!=null&&h.kind=="maglev")laser=h;if(laser==null)return null;
            int round=NativeStructureFx.RoundIndex(arena.Match.Set,arena.Match.Round);var st=NativeCyberCityHazards.Evaluate(laser,round,arena.Match.FightAt,death.at);float d=st.Direction!=0?st.Direction:death.direction.x>=0?1:-1;
            float floor=death.position.y-.865f,cut=floor+laser.center.y+.07f;var travel=new System.Numerics.Vector3(d*laser.speed,0,0);uint seed=unchecked((uint)death.seed*2654435761u^0x9e3779b9u);
            return SplitCorpse(arena,death,rig,-1,pelvis=>NativeBisect.Request("laser",new System.Numerics.Vector3(pelvis.x,cut,pelvis.z),System.Numerics.Vector3.UnitY,travel,seed,part:.45f,lift:.55f,push:.28f,topple:2.2f,hitch:.05f,blood:.9f,sparks:true),true);
        }
        // PrewarmStage: build it behind the loading screen.
        private bool PrewarmClub(NativeArena arena)
        {
            if(!NativeClubLooks.Active(arena.MapId))return false;if(club==null)club=gameObject.AddComponent<NativeClubDraw>();return club.Prepare(arena);
        }
    }
}
