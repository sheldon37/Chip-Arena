using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One Siren's look for a frame (Core/NativeSirenLook.cs): each animated piece's joint-relative transform mirrored into
    /// Unity (S·M·S, the frame the model bank is stored in), its visibility, the eye outline and the eye halos' opacity.
    /// </summary>
    public sealed class NativeSirenLookPose
    {
        public readonly Matrix4x4[] Local=new Matrix4x4[NativeSirenLook.Count];public readonly bool[] Shown=new bool[NativeSirenLook.Count];
        public int Shape;public float EyeHalo;public SirenLookState State;
        /// <summary>The eyes' and grin's breathing this frame (NativeSirenLook.Breath); 1, the source's look, unless a caller sets it.</summary>
        public float Breath=1;
        /// <summary>The viewer's distance (m) for the detail's glow lines (NativeSirenDetail.LineGlow); 0, unfaded, unless a caller sets it.</summary>
        public float GlowDistance;
        /// <summary>The Damnation ball and its halo relative to the rig root, while one shows (SetCharge).</summary>
        public bool Charging;public Matrix4x4 ChargeBall,ChargeHalo;
        private readonly double[] m=new double[NativeSirenLook.Count*16],charge=new double[32];
        // The detail's static pieces are placed once; Set mirrors the source's pieces and the detail's moving ones.
        public NativeSirenLookPose(){NativeSirenLook.Rest(m,Shown);for(int i=NativeSirenLook.Detail;i<NativeSirenLook.Count;i++)Local[i]=Mirror(m,i*16);}
        public NativeSirenLookPose Set(in SirenLookState s)
        {
            NativeSirenLook.Evaluate(s,m,Shown);Shape=NativeSirenLook.Shape(s.Anger);EyeHalo=(float)NativeSirenLook.EyeHaloAlpha(s);State=s;
            for(int i=0;i<NativeSirenLook.Source;i++)Local[i]=Mirror(m,i*16);
            for(int k=0,n=NativeSirenDetail.MovingCount;k<n;k++){int i=NativeSirenLook.Detail+NativeSirenDetail.Moving(k);Local[i]=Mirror(m,i*16);}
            Charging=false;return this;
        }
        /// <summary>The Damnation windup ball for the pose's action at `age` (none for any other action).</summary>
        public NativeSirenLookPose SetCharge(string action,double age,double time)
        {Charging=NativeSirenLook.Charge(action,age,time,charge);if(Charging){ChargeBall=Mirror(charge,0);ChargeHalo=Mirror(charge,16);}return this;}
        private static Matrix4x4 Mirror(double[] m,int o)
        {var u=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++)u[r,c]=(float)((r==0)^(c==0)?-m[o+r*4+c]:m[o+r*4+c]);return u;}
    }

    /// <summary>Resources/SirenLook.json: the face targets of each Siren clip, found without building its key string.</summary>
    public static class NativeSirenLookTracks
    {
        private static SirenLookClip idleMelee,idleRanged,grab;private static Dictionary<string,SirenLookClip[]> moves;
        // A damaged bank leaves every face at rest (logged once) rather than failing the frame that draws a Siren.
        private static void Ensure()
        {
            if(moves!=null)return;
            try{Load();}catch(Exception e){idleMelee=idleRanged=grab=null;moves=new Dictionary<string,SirenLookClip[]>();Debug.LogWarning("Siren face tracks unavailable: "+e.Message);}
        }
#if UNITY_EDITOR
        public static int Count{get{Load();int n=3;foreach(var s in moves.Values)foreach(var c in s)if(c!=null)n++;return n;}}
#endif
        private static void Load()
        {
            var set=JsonUtility.FromJson<SirenLookSet>(NativeSourceModels.Decode("SirenLook"));var found=new Dictionary<string,SirenLookClip[]>();
            if(set?.clips==null||set.fps!=60)throw new InvalidDataException("Invalid Siren look tracks");
            foreach(var c in set.clips)
            {
                if(string.IsNullOrEmpty(c.key)||!c.key.StartsWith("siren-")||c.frames<2||c.duration<=0||c.values==null||c.values.Length!=c.frames*2||Array.Exists(c.values,v=>!MathUtil.Finite(v)))throw new InvalidDataException("Invalid Siren look track "+c.key);
                if(c.key=="siren-idle-melee")idleMelee=c;else if(c.key=="siren-idle-ranged")idleRanged=c;else if(c.key=="siren-grab")grab=c;
                else{bool left=c.key.EndsWith("--1");string action=c.key.Substring(6,c.key.Length-6-(left?3:2));if(!found.TryGetValue(action,out var sides))found.Add(action,sides=new SirenLookClip[2]);sides[left?0:1]=c;}
            }
            if(idleMelee==null||idleRanged==null||grab==null)throw new InvalidDataException("Siren look tracks are incomplete");moves=found;
        }
        /// <summary>The clip NativeSourceMotion.Pilot plays for this pose (the grab, the move on its side, else the idle).</summary>
        public static SirenLookClip Clip(string action,int side,bool ranged)
        {
            Ensure();if(action==null)return ranged?idleRanged:idleMelee;if(action=="grab")return grab;
            return moves.TryGetValue(action,out var sides)?sides[side>=0?1:0]:null;
        }
    }

    /// <summary>
    /// The palm orbs (orbMaterial) and the eye and orb halos (additive radial sprites) on the Siren's own NativeSirenFx shader:
    /// one shared unit sphere and two baked sprite quads drawn per piece, so nothing is rebuilt or allocated per frame.
    /// </summary>
    public sealed partial class NativeSirenLookDraw:IDisposable
    {
        private readonly Mesh orb,eyeHalo,orbHalo,chargeHalo;private readonly Mesh[] crackHalo=new Mesh[CrackLevels];private readonly Material solid,effects;
        // The nimbus glows breathe between .8 and 1 of their opacity: one baked quad per step (no per-frame mesh or colour).
        private const int CrackLevels=6;
        public NativeSirenLookDraw()
        {
            var shader=Resources.Load<Shader>("NativeSirenFx");if(shader==null)throw new InvalidOperationException("NativeSirenFx shader is missing");
            solid=new Material(shader){name="Native Siren palm orbs",renderQueue=(int)RenderQueue.Geometry+10};solid.SetFloat("_Src",(float)BlendMode.One);solid.SetFloat("_Dst",(float)BlendMode.Zero);solid.SetFloat("_ZWrite",1);
            effects=new Material(shader){name="Native Siren halos",renderQueue=(int)RenderQueue.Transparent+1};effects.SetOverrideTag("RenderType","Transparent");
            effects.SetFloat("_Src",(float)BlendMode.One);effects.SetFloat("_Dst",(float)BlendMode.OneMinusSrcAlpha);effects.SetFloat("_ZWrite",0);
            // orbMaterial(PALETTE.ink, PALETTE.ash, 1.6): raw ShaderMaterial colours, churning in the sphere's own space.
            NativeSirenFx.Sphere(out var sp,out _,out var si,12,8);int n=sp.Length/3;var points=new Vector3[n];var colors=new Color[n];var uv=new List<Vector4>(n);var p=new List<Vector4>(n);var q=new List<Vector4>(n);
            Color core=Raw(NativeSirenFx.Ink),rim=Raw(NativeSirenFx.Ash);
            for(int i=0;i<n;i++){var o=new Vector3((float)sp[i*3],(float)sp[i*3+1],(float)sp[i*3+2]);points[i]=o;colors[i]=core;uv.Add(new Vector4(0,0,NativeFxDraw.OrbMode,0));p.Add(new Vector4(-o.x,o.y,o.z,1.6f));q.Add(new Vector4(rim.r,rim.g,rim.b,1));}
            orb=new Mesh{name="Native Siren palm orb"};orb.vertices=points;orb.normals=points;orb.colors=colors;orb.SetUVs(0,uv);orb.SetUVs(1,p);orb.SetUVs(2,q);orb.triangles=si;orb.RecalculateBounds();orb.UploadMeshData(true);
            eyeHalo=Quad("Native Siren eye halo",NativeSirenLook.EyeHaloColor,(float)NativeSirenLook.EyeHaloOpacity);orbHalo=Quad("Native Siren orb halo",NativeSirenLook.OrbHaloColor,(float)NativeSirenLook.OrbHaloOpacity);
            chargeHalo=Quad("Native Siren Damnation halo",NativeSirenLook.ChargeHaloColor,(float)NativeSirenLook.ChargeHaloOpacity);
            for(int k=0;k<CrackLevels;k++)crackHalo[k]=Quad("Native Siren nimbus glow "+k,NativeSirenDetail.GlowHex,(float)NativeSirenDetail.GlowAlpha(k/(CrackLevels-1.0)));
        }
        private static Color Raw(int hex){NativeBlastSim.Linear(hex,out double r,out double g,out double b);return new Color((float)r,(float)g,(float)b,1);}
        // haloMaterial: an authored sRGB colour at its opacity, radial alpha (1-r)^1.6, additive, both faces.
        private static Mesh Quad(string name,int hex,float opacity)
        {
            var c=NativeArena.ColorOf(hex);c.a=opacity;var mesh=new Mesh{name=name};
            mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0)};mesh.normals=new[]{Vector3.back,Vector3.back,Vector3.back,Vector3.back};mesh.colors=new[]{c,c,c,c};
            float flags=NativeFxDraw.Additive+NativeFxDraw.Authored;mesh.SetUVs(0,new List<Vector4>{new Vector4(-1,-1,NativeFxDraw.Radial,flags),new Vector4(1,-1,NativeFxDraw.Radial,flags),new Vector4(-1,1,NativeFxDraw.Radial,flags),new Vector4(1,1,NativeFxDraw.Radial,flags)});
            mesh.SetUVs(1,new List<Vector4>{Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero});mesh.SetUVs(2,new List<Vector4>{Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero});
            mesh.triangles=new[]{0,1,3,0,3,2};mesh.bounds=new Bounds(Vector3.zero,Vector3.one);mesh.UploadMeshData(true);return mesh;
        }
        /// <summary>
        /// The orbs on the hands (and the Damnation ball over the rig root) and the halos facing `facing` (none without one), on
        /// `layer` for `camera` (null: every camera that draws the layer), the orb churn on the Siren clock.
        /// </summary>
        public void Draw(NativeSirenLookPose look,Matrix4x4 root,Matrix4x4 head,Matrix4x4 left,Matrix4x4 right,Camera facing,int layer,Camera camera,float clock)
        {
            solid.SetFloat("_FxTime",clock);effects.SetFloat("_FxTime",clock);
            if(look.Charging){Graphics.DrawMesh(orb,root*look.ChargeBall,solid,layer,camera,0,null,ShadowCastingMode.Off,false);if(facing!=null)Sprite(chargeHalo,root*look.ChargeHalo,facing.transform.rotation,layer,camera);}
            for(int i=0;i<2;i++)
            {
                var hand=i==0?left:right;
                if(look.Shown[NativeSirenLook.Orb+i])Graphics.DrawMesh(orb,hand*look.Local[NativeSirenLook.Orb+i],solid,layer,camera,0,null,ShadowCastingMode.Off,false);
                if(facing==null)continue;var turn=facing.transform.rotation;
                if(look.EyeHalo>0)Sprite(eyeHalo,head*look.Local[NativeSirenLook.EyeHalo+i],turn,layer,camera,(float)NativeSirenLook.HaloSize(look.Breath));
                if(look.Shown[NativeSirenLook.OrbHalo+i])Sprite(orbHalo,hand*look.Local[NativeSirenLook.OrbHalo+i],turn,layer,camera);
            }
            // The two soft glows in the nimbus breaks (the detail's sprite pieces, on the head).
            if(facing==null)return;var crack=crackHalo[Mathf.Clamp(Mathf.RoundToInt(look.Breath*(CrackLevels-1)),0,CrackLevels-1)];
            for(int i=NativeSirenLook.Detail;i<NativeSirenLook.Count;i++)if(NativeSirenDetail.Tone(i-NativeSirenLook.Detail)==NativeSirenDetail.Sprite&&look.Shown[i])Sprite(crack,head*look.Local[i],facing.transform.rotation,layer,camera);
        }
        // A sprite: centred on the piece, its world width the piece's x scale (times `scale`), turned to the camera.
        private void Sprite(Mesh mesh,Matrix4x4 world,Quaternion turn,int layer,Camera camera,float scale=1)
        {
            Vector3 at=world.GetColumn(3);float size=((Vector3)world.GetColumn(0)).magnitude*scale;if(size<1e-4f)return;
            Graphics.DrawMesh(mesh,Matrix4x4.TRS(at,turn,new Vector3(size,size,size)),effects,layer,camera,0,null,ShadowCastingMode.Off,false);
        }
        public void Dispose(){DisposeIntroHalos();CapturedAnimation.Release(orb);CapturedAnimation.Release(eyeHalo);CapturedAnimation.Release(orbHalo);CapturedAnimation.Release(chargeHalo);foreach(var q in crackHalo)CapturedAnimation.Release(q);CapturedAnimation.Release(solid);CapturedAnimation.Release(effects);}
    }

    public sealed partial class NativeSourceModels
    {
        // A Siren piece: its joint, the shared part it draws (the flame box serves all 124 tongue steps and wisps, so they
        // instance in one draw), or one of the 41 brow cuts of its eye. Orbs and halos have no part (NativeSirenLookDraw).
        private sealed class Piece {public int Index,Bone;public Part Part;public Part[] Shapes;public bool Line;public Vector4 Tone=Vector4.one;}
        private readonly Dictionary<string,Piece[]> lookPieces=new Dictionary<string,Piece[]>();private readonly List<Mesh> pieceMeshes=new List<Mesh>();
        private static NativeSirenLookPose restLook;
        private static readonly int InkId=Shader.PropertyToID("_Ink"),InkRimId=Shader.PropertyToID("_InkRim"),InkFlowId=Shader.PropertyToID("_InkFlow");
        /// <summary>The look of a Siren nobody animates (no motion, melee, lit), drawn when a caller passes no look.</summary>
        public static NativeSirenLookPose RestLook=>restLook??(restLook=new NativeSirenLookPose().Set(NativeSirenLook.Neutral(0)));
        /// <summary>radialTexture() (the Siren's shadow pool and every halo sprite), for the portraits' halos.</summary>
        public Texture SirenHaloTexture{get;private set;}
        // tools/export_models.mjs leaves the animated pieces out of the Siren's joint batches as named sections in their own
        // frames; they leave the model's static parts here and become pieces placed per frame by a look.
        private void SplitSirenPieces(SourceModelSet data)
        {
            if(!models.TryGetValue("siren",out var all))return;
            Part Named(string name){foreach(var p in all)if(p.Name==name)return p;throw new InvalidDataException("Siren piece missing from the model bank: "+name);}
            Part eye=Named("siren-eye"),upper=Named("siren-teeth-upper"),lower=Named("siren-teeth-lower"),clawL=Named("siren-claws-l"),clawR=Named("siren-claws-r"),box=Named("siren-flame-box");
            // The Unity finish's shared unit boxes (tools/siren_design.mjs, Core/NativeSirenDetail.cs), one per tone.
            var couture=new Part[NativeSirenDetail.Sprite];couture[NativeSirenDetail.Plate]=Named("siren-couture-plate");couture[NativeSirenDetail.Deep]=Named("siren-couture-deep");couture[NativeSirenDetail.Sheen]=Named("siren-couture-sheen");
            couture[NativeSirenDetail.Ash]=Named("siren-couture-ash");couture[NativeSirenDetail.Seam]=couture[NativeSirenDetail.Filament]=Named("siren-couture-seam");couture[NativeSirenDetail.Flame]=box;
            couture[NativeSirenDetail.Seam].NoShadow=true;
            var kept=new List<Part>();foreach(var p in all){if(string.IsNullOrEmpty(p.Name))kept.Add(p);else pieceMeshes.Add(p.Mesh);if(p.Material.mainTexture!=null&&SirenHaloTexture==null)SirenHaloTexture=p.Material.mainTexture;}
            models["siren"]=kept.ToArray();
            // The eyes and teeth cast no shadow (buildSiren). The dev-siren entrance draws its fangs one by one (NativeSirenFace.cs).
            upper.NoShadow=lower.NoShadow=true;SirenFangs(data,upper);
            // buildSiren's ink strengths per piece (NativeSourceLit _InkRim, _InkFlow): the claws' CLAW, the tongues' and wisps' TONGUE.
            foreach(var claw in new[]{clawL,clawR}){claw.Material.SetFloat(InkRimId,(float)NativeSirenLook.ClawRim);claw.Material.SetFloat(InkFlowId,(float)NativeSirenLook.ClawFlow);}
            box.Material.SetFloat(InkRimId,(float)NativeSirenLook.TongueRim);box.Material.SetFloat(InkFlowId,(float)NativeSirenLook.TongueFlow);
            foreach(var p in kept)if(p.Material.GetFloat(InkId)>.5f){p.Material.SetFloat(InkRimId,(float)NativeSirenLook.BodyRim);p.Material.SetFloat(InkFlowId,(float)NativeSirenLook.BodyFlow);}
            CoutureInk();SourceSection section=null;foreach(var m in data.models)if(m.id=="siren")foreach(var s in m.sections)if(s.part=="siren-eye")section=s;
            var color=new Color(section.colors[0],section.colors[1],section.colors[2],section.colors[3]);var glow=new Vector4(section.emission[0],section.emission[1],section.emission[2],section.emission[3]);
            var shapes=new Part[2][];var xy=new double[NativeSirenLook.EyeSegments*6];
            for(int side=0;side<2;side++){shapes[side]=new Part[NativeSirenLook.Shapes];for(int k=0;k<NativeSirenLook.Shapes;k++){NativeSirenLook.EyeOutline(side==0?1:-1,k/40.0,xy);var mesh=EyeMesh(xy,color,glow,side,k);pieceMeshes.Add(mesh);shapes[side][k]=new Part{Name="siren-eye",Bone=-1,Mesh=mesh,Material=eye.Material,NoShadow=true};}}
            var list=new Piece[NativeSirenLook.Count];
            for(int i=0;i<list.Length;i++)
            {
                var p=list[i]=new Piece{Index=i,Bone=NativeSirenLook.Joint(i)};
                if(i<NativeSirenLook.EyeHalo)p.Shapes=shapes[i-NativeSirenLook.Eye];
                else if(i==NativeSirenLook.GrinUpper)p.Part=upper;else if(i==NativeSirenLook.GrinLower)p.Part=lower;
                else if(i==NativeSirenLook.Claw||i==NativeSirenLook.Claw+1)p.Part=i==NativeSirenLook.Claw?clawL:clawR;
                else if(i>=NativeSirenLook.Detail)
                {
                    // Detail pieces: the tone's box (the nimbus glows are sprites, drawn by NativeSirenLookDraw); the glow lines
                    // carry their colour as the instance tint, breathing and fading with distance (QueuePieces).
                    int tone=NativeSirenDetail.Tone(i-NativeSirenLook.Detail);if(tone==NativeSirenDetail.Sprite)continue;p.Part=couture[tone];
                    if(tone==NativeSirenDetail.Seam||tone==NativeSirenDetail.Filament){NativeSirenDetail.GlowColor(tone,out double r,out double g,out double b);p.Line=true;p.Tone=new Vector4((float)r,(float)g,(float)b,1);}
                }
                else if(i>=NativeSirenLook.Wisp)p.Part=box;
            }
            lookPieces["siren"]=list;
        }
        // The couture tones' ink strengths (NativeSourceLit _InkRim, _InkFlow) on every model that carries them: the arena
        // bank's unit boxes and the dev-siren entrance bake's named batches (tools/export_cinematics.mjs).
        private void CoutureInk()
        {
            string[] names={"siren-couture-plate","siren-couture-deep","siren-couture-sheen","siren-couture-ash"};
            foreach(var model in models.Values)foreach(var p in model)
            {
                if(p.Name==null||!p.Name.StartsWith("siren-couture-")||p.Material==null||p.Material.GetFloat(InkId)<.5f)continue;int tone=Array.IndexOf(names,p.Name);if(tone<0)continue;
                p.Material.SetFloat(InkRimId,(float)NativeSirenDetail.InkRim[tone]);p.Material.SetFloat(InkFlowId,(float)NativeSirenDetail.InkFlow[tone]);
            }
        }
        // shapeEye's fan in the bank's frame: x mirrored and each triangle's winding turned over, as the exporter stores models.
        private static Mesh EyeMesh(double[] xy,Color color,Vector4 glow,int side,int shape)
        {
            int n=xy.Length/2;var vertices=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var emission=new List<Vector4>(n);var uv=new Vector2[n];var triangles=new int[n];
            for(int i=0;i<n;i++){vertices[i]=new Vector3(-(float)xy[i*2],(float)xy[i*2+1],0);normals[i]=Vector3.forward;colors[i]=color;emission.Add(glow);}
            for(int i=0;i<n;i+=3){triangles[i]=i;triangles[i+1]=i+2;triangles[i+2]=i+1;}
            var mesh=new Mesh{name="siren / eye "+(side==0?"left":"right")+" "+shape};mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=uv;mesh.SetUVs(1,emission);mesh.triangles=triangles;mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        // Queue's Siren pieces, each on its joint under the look (the rest look without one); joints repeat, so each is read once.
        private void QueuePieces(Piece[] list,Matrix4x4 root,Func<int,Matrix4x4> bone,Func<int,bool> visible,int layer,Camera camera,Color bc,Color ac,float opacity,Func<int,float> partOpacity,Func<int,Color> partTint,Vector4? glow,NativeSirenLookPose look)
        {
            var pose=look??RestLook;int jointBone=int.MinValue;var joint=root;float line=(float)NativeSirenDetail.LineGlow(pose.Breath,pose.GlowDistance);
            foreach(var p in list)
            {
                var part=p.Shapes!=null?p.Shapes[pose.Shape]:p.Part;if(part==null||!pose.Shown[p.Index]||visible!=null&&!visible(p.Bone))continue;
                if(p.Bone!=jointBone){jointBone=p.Bone;joint=bone!=null?bone(p.Bone):root;}
                var key=(part,layer);if(!draws.TryGetValue(key,out var d)){d=new Draws{Part=part,Layer=layer,Camera=camera};draws.Add(key,d);}
                // The grin breathes with the eye halos (NativeSirenLook.GrinGlow; the pose's Breath is 1 unless the arena set it).
                var g=glow??Vector4.one;if(p.Index==NativeSirenLook.GrinUpper||p.Index==NativeSirenLook.GrinLower){float k=(float)NativeSirenLook.GrinGlow(pose.Breath);g.x*=k;g.y*=k;g.z*=k;}
                else if(p.Line){g.x*=p.Tone.x*line;g.y*=p.Tone.y*line;g.z*=p.Tone.z*line;}
                d.Matrices.Add(joint*pose.Local[p.Index]);d.Bodies.Add(new Vector4(bc.r,bc.g,bc.b,opacity*(partOpacity?.Invoke(p.Bone)??1)));d.Accents.Add(new Vector4(ac.r,ac.g,ac.b,1));d.Tints.Add(partTint?.Invoke(p.Bone)??Color.white);d.Glows.Add(g);
            }
        }
        // The portraits draw the pieces themselves (their own camera and material).
        public int PortraitPieceCount(string id)=>id!=null&&lookPieces.TryGetValue(id,out var list)?list.Length:0;
        public bool PortraitPiece(string id,int index,NativeSirenLookPose look,out Mesh mesh,out int bone,out Matrix4x4 local,out Material material)=>PortraitPiece(id,index,look,out mesh,out bone,out local,out material,out _);
        /// <summary>A Siren piece for the portraits, with the emission tint it draws with (the detail's glow lines: their colour).</summary>
        public bool PortraitPiece(string id,int index,NativeSirenLookPose look,out Mesh mesh,out int bone,out Matrix4x4 local,out Material material,out Vector4 glow)
        {
            var p=lookPieces[id][index];var pose=look??RestLook;var part=p.Shapes!=null?p.Shapes[pose.Shape]:p.Part;
            mesh=part?.Mesh;material=part?.Material;bone=p.Bone;local=pose.Local[index];glow=p.Line?p.Tone*(float)NativeSirenDetail.LineGlow(pose.Breath,0):Vector4.one;glow.w=1;return part!=null&&pose.Shown[index];
        }
        private void DisposePieces(){foreach(var mesh in pieceMeshes)CapturedAnimation.Release(mesh);pieceMeshes.Clear();lookPieces.Clear();}
    }

    // The Siren's look in the arena (third person, the PvE boss and the killcam replay), the draft and staged entrances:
    // motion.ts animateSirenRig's face smoothing and mode blend per Siren (keyed by seat), from the replicated pose only.
    public sealed partial class ArenaPresentation
    {
        private sealed class SirenLookTrack {public readonly NativeSirenLookSmoother Smoother=new NativeSirenLookSmoother();public readonly NativeSirenLookPose Pose=new NativeSirenLookPose();public CapturedRig.Pose Melee,Ranged;}
        private readonly Dictionary<int,SirenLookTrack> sirenLooks=new Dictionary<int,SirenLookTrack>();private readonly NativeSirenLookPose sirenStill=new NativeSirenLookPose();
        private NativeSirenLookDraw sirenLookDraw;private bool sirenLookFailed;private Camera sirenLookCamera;
        /// <summary>
        /// This frame's look of a Siren actor: the face targets of the clip its body plays (NativeSourceMotion.Pilot's clip, age
        /// and length), smoothed on the presentation clock; the claws, the palm orbs and (idle) the stance follow the mode swap
        /// from its authoritative time (the rendered arena's snapshot time when the replicated mode flips, not the frame that
        /// saw it); the flames lean with the forward speed; the flare peaks at the Hellfire and Damnation releases.
        /// </summary>
        private NativeSirenLookPose SirenLook(CapturedAnimation.Player rig,Actor a,float time)
        {
            if(!sirenLooks.TryGetValue(a.Id,out var t)){t=new SirenLookTrack();sirenLooks.Add(a.Id,t);}
            string action=string.IsNullOrEmpty(a.SourceAction)?null:a.SourceAction;double age=action==null?time:time-a.SourceAt;
            NativeSirenLook.Targets(NativeSirenLookTracks.Clip(action,a.SourceSide,a.SourceRanged),age,a.SourceUntil-a.SourceAt,out double open,out double squint);
            float yaw=a.Yaw*Mathf.Deg2Rad;double forward=a.Velocity.x*Mathf.Sin(yaw)+a.Velocity.z*Mathf.Cos(yaw);var s=t.Smoother;
            // WeaponLoadout is the arena this frame renders (live or killcam): its Time is the snapshot's authoritative time.
            var state=s.Step(time,s.Delta(time),open,squint,NativeSirenLook.Flare(action,age),a.SourceRanged,forward,a.Alive,WeaponLoadout!=null?WeaponLoadout.Time:time,a.SourceModeAt);
            if(action==null&&s.Swapping(time))SirenSwapBody(rig,a,t,time,state.Ranged,s.Shudder(time));
            t.Pose.Set(state).SetCharge(action,age,time);t.Pose.Breath=HaloReduced?1:(float)NativeSirenLook.Breath(time);
            // The detail's glow lines fade with the viewer's distance (NativeSirenDetail.LineGlow).
            var eye=sirenLookCamera!=null?sirenLookCamera:Camera.main;t.Pose.GlowDistance=eye!=null&&rig!=null?Vector3.Distance(eye.transform.position,rig.Root.transform.position):0;return t.Pose;
        }
        /// <summary>
        /// stance(t, ranged) through a mode swap: NativeSourceMotion.Pilot snaps the idle clip to the replicated mode, while the
        /// source blends the arms with the claws over .35 s and shudders to .5 s; the rig this actor's ActorPose just posed
        /// takes the blended idle clips instead (not while knocked down, juggled or in a special, whose layers own the frame).
        /// </summary>
        private void SirenSwapBody(CapturedAnimation.Player rig,Actor a,SirenLookTrack t,float time,double blend,double shudder)
        {
            if(rig==null||Viewer||!rigs.TryGetValue(a.Id,out var own)||!ReferenceEquals(own,rig)||NativeSpecialPose.Active(a,time)||NativeSpecialPose.Down(a,time)||NativeJugglePose.Active(a.JuggleUntil,time))return;
            var melee=t.Melee??(t.Melee=new CapturedRig.Pose());var ranged=t.Ranged??(t.Ranged=new CapturedRig.Pose());
            if(!NativeSourceMotion.Sample(NativeSirenLook.IdleMelee,time,0,melee)||!NativeSourceMotion.Sample(NativeSirenLook.IdleRanged,time,0,ranged))return;
            NativeSirenLook.SwapBody(melee,ranged,blend,shudder,time,melee);
            NativePoseWriter.Write(rig.Bones,melee,"a Siren rig");
        }
        /// <summary>A Siren nobody moves (the draft's idle): melee, mouth closed, the flames alive on `time`.</summary>
        private NativeSirenLookPose SirenStill(float time)=>sirenStill.Set(NativeSirenLook.Neutral(time));
        /// <summary>The palm orbs and halos of a Siren drawn on `rig` with `look`.</summary>
        private void SirenLookFx(CapturedAnimation.Player rig,NativeSirenLookPose look,int layer,Camera camera,float clock)
        {
            if(sirenLookFailed||rig==null||look==null)return;
            if(sirenLookDraw==null)try{sirenLookDraw=new NativeSirenLookDraw();}catch(InvalidOperationException e){sirenLookFailed=true;Debug.LogWarning("Siren orbs and halos disabled: "+e.Message);return;}
            sirenLookDraw.Draw(look,rig.Root.transform.localToWorldMatrix,rig.Bones[NativeSirenLook.Joint(NativeSirenLook.EyeHalo)].localToWorldMatrix,rig.Bones[NativeSirenLook.Joint(NativeSirenLook.Orb)].localToWorldMatrix,rig.Bones[NativeSirenLook.Joint(NativeSirenLook.Orb+1)].localToWorldMatrix,sirenLookCamera,layer,camera,clock);
        }
        private void ClearSirenLooks()=>sirenLooks.Clear();
        private void DisposeSirenLooks(){sirenLooks.Clear();sirenLookDraw?.Dispose();sirenLookDraw=null;}
    }
}
