using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // Intros v2 on screen (docs/INTROS-V2.md): the per-frame context an intro reads, and its set: modelled pieces from its local set
    // bank (Resources/ChipArenaIntro<Name>.bytes, Core NativePackBank format, git-ignored) drawn with the stage art's material
    // (ChipArena/StageLit: vertex colours, the glow submesh), and the source-model primitives every build has (the dark lit void,
    // Core NativeIntroVoid). Without the bank (CI, a build without the zip, --ca-no-pack-banks) Ready is false and the intro draws its
    // fallback. Presentation only; allocation-free per frame once a bank's pieces are built.
    public sealed partial class ArenaPresentation
    {
        private IntroSetDraw introSet;private string introNameRaw,introName="Pilot";
        /// <summary>Sets a v2 intro's context for this frame (Reduced effects: the effects quality the game sets for it, .3).</summary>
        private void IntroV2Begin(NativeEntranceScript script,NativePilot pilot,float quality)
        {
            if(!(script is INativeIntroV2 v2))return;string raw=pilot?.Seat?.name;
            if(!ReferenceEquals(raw,introNameRaw)){introNameRaw=raw;introName=NativeCosmeticRules.Name(raw);}
            v2.Context=new NativeIntroContext{Reduced=WeaponEffectsQuality<.5f,Low=LowGraphics,Quality=quality,Name=introName};
        }
        /// <summary>A v2 intro's set at presentation time `t` (after its Props).</summary>
        private void IntroV2Set(NativeEntranceScript script,float t,Camera camera)
        {
            if(!(script is INativeIntroV2 v2))return;introSet??=new IntroSetDraw();
            introSet.Begin(v2.SetBank,LowGraphics,entranceProps);v2.Set(t,introSet);
        }
        // ---- the model's clip (INativeIntroBody) and the hands (INativeIntroHands) ----
        // (Model-native: the hands are the drawn model's joints, VisualJointFrame; without a model the keyed pose's anchors. No rig reads.)
        private INativeIntroBody introBody;private int introBodyActor=-1,introV2Actor=-1,introModelFrame=-1;private float introBodyClock;private CapturedAnimation.Player introModel;
        /// <summary>Before the intro body is posed: a v2 intro's model clip for this frame, and a weapon intro's weapon in the model's hands.</summary>
        private void IntroV2BodyBegin(NativeEntranceScript script,Actor actor,string style,float t)
        {
            introModel=null;introV2Actor=-1;if(!(script is INativeIntroV2)||actor==null)return;introV2Actor=actor.Id;
            var spec=NativeIntroCatalog.Find(style)?.Spec;if(spec!=null)actor.SourceSpec=spec;
            if(script is INativeIntroBody body&&!(script is NativeBladeIntro)){introBody=body;introBodyActor=actor.Id;introBodyClock=t;}
        }
        /// <summary>From VisualIntroPose, on the drawn model: notes the v2 intro's model for its hands, and plays the intro's clip on it
        /// through the arena's motion as the blade entrances play theirs.</summary>
        private void IntroV2Clip(VisualBody body,CapturedAnimation.Player rig,Actor a)
        {
            if(a==null)return;if(a.Id==introV2Actor&&introV2Actor>=0){introModel=rig;introModelFrame=Time.frameCount;}
            if(introBody==null||introBodyActor<0||a.Id!=introBodyActor||!introBody.Clip(introBodyClock,out var clip,out float time)||clip==null)return;
            NativeBladeMotion.IntroActor=a.Id;NativeBladeMotion.IntroClip=clip;NativeBladeMotion.IntroTime=time;NativeBladeMotion.IntroFade=introBody.Fade(introBodyClock);
            try{VisualMotionPose(body,rig,a,introBodyClock);}finally{NativeBladeMotion.IntroActor=-1;NativeBladeMotion.IntroFade=.12f;}
        }
        /// <summary>The hands as drawn, in the source frame: the model's wrist joints (VisualJointFrame) when the model is drawn this
        /// frame, else the keyed pose's hand anchors.</summary>
        private void IntroV2Hands(NativeEntranceScript script,float t)
        {
            if(!(script is INativeIntroHands hands))return;bool model=introModel!=null&&introModelFrame==Time.frameCount;
            NativeIntroHand Hand(int j,NativeEntranceAnchor anchor)
            {
                if(!model)return new NativeIntroHand{Model=false,At=script.Anchor(anchor,t),Turn=System.Numerics.Quaternion.Identity};
                var m=VisualJointFrame(introModel,j);var p=(Vector3)m.GetColumn(3);var q=m.rotation;
                return new NativeIntroHand{Model=true,At=new System.Numerics.Vector3(-p.x,p.y,p.z),Turn=new System.Numerics.Quaternion(q.x,-q.y,-q.z,q.w)};
            }
            var right=Hand(CapturedRig.RHand,NativeEntranceAnchor.RightHand);var left=Hand(CapturedRig.LHand,NativeEntranceAnchor.LeftHand);hands.Hands(t,right,left);
        }
        /// <summary>The v2 set sink: bank pieces through Graphics.DrawMesh (stage lit), primitives through the entrance's props.</summary>
        private sealed class IntroSetDraw:INativeIntroSet
        {
            private sealed class SetPiece{public Mesh Mesh;public Matrix4x4 Place;public int Subs;}
            private string bankName;private NativePackBank.Bank bank;private bool failed;private bool low;
            private readonly Dictionary<string,SetPiece> pieces=new Dictionary<string,SetPiece>(StringComparer.Ordinal);private readonly Dictionary<string,string> lowNames=new Dictionary<string,string>(StringComparer.Ordinal);
            // (each prop's "<prop>/low" name made once, so the Low tier allocates nothing per frame)
            private string Low(string prop){if(!lowNames.TryGetValue(prop,out var n)){n=prop+"/low";lowNames[prop]=n;}return n;}
            private Material solid,glow;private MaterialPropertyBlock block;private INativeEntranceProps props;
            private static readonly int GlowId=Shader.PropertyToID("_StageGlow"),TintId=Shader.PropertyToID("_StageTint"),GlowFlag=Shader.PropertyToID("_Glow");
            public bool Ready=>bank!=null&&solid!=null;
            public void Begin(string name,bool lowTier,INativeEntranceProps sink)
            {
                props=sink;low=lowTier;if(name==bankName)return;bankName=name;bank=null;pieces.Clear();if(string.IsNullOrEmpty(name))return;
                try
                {
                    bank=NativePackBanks.Bank(name);if(bank==null)return;
                    if(solid==null&&!failed)
                    {
                        var shader=Resources.Load<Shader>("NativeStageLit");if(shader==null||!shader.isSupported){failed=true;bank=null;return;}
                        solid=new Material(shader){name="Intro set",enableInstancing=true};solid.SetFloat(GlowFlag,0);glow=new Material(shader){name="Intro set glow",enableInstancing=true};glow.SetFloat(GlowFlag,1);block=new MaterialPropertyBlock();
                    }
                }
                catch(Exception e){Debug.LogWarning("Intro set "+name+": "+e.Message);bank=null;}
            }
            private SetPiece Find(string prop)
            {
                if(bank==null||prop==null)return null;if(pieces.TryGetValue(prop,out var p))return p;p=null;
                var src=bank.Find(prop);
                if(src!=null&&src.Parts.Length>0){var part=src.Parts[0];var mesh=NativePackBanks.Mesh(bankName,part.Mesh);if(mesh!=null)p=new SetPiece{Mesh=mesh,Place=NativePackBanks.Place(part.Place),Subs=mesh.subMeshCount};}
                pieces[prop]=p;return p;// (a missing prop is remembered as null: one lookup per name, then none)
            }
            public bool Has(string prop)=>Find(prop)!=null;
            public void Piece(string prop,System.Numerics.Vector3 at,System.Numerics.Quaternion rotation,float scale,System.Numerics.Vector3 tint,System.Numerics.Vector3 glowTint)
            {
                if(!Ready||prop==null)return;SetPiece p=null;if(low)p=Find(Low(prop));p??=Find(prop);if(p==null)return;
                var m=Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(at)),CapturedAnimation.Quat(CapturedRig.Mirror(rotation)),Vector3.one*scale)*p.Place;
                block.Clear();bool tinted=tint.X!=1||tint.Y!=1||tint.Z!=1;if(tinted)block.SetVector(TintId,new Vector4(tint.X,tint.Y,tint.Z,0));block.SetVector(GlowId,new Vector4(glowTint.X,glowTint.Y,glowTint.Z,1));
                for(int s=0;s<p.Subs;s++)Graphics.DrawMesh(p.Mesh,m,s==0?solid:glow,0,null,s,block,s==0?ShadowCastingMode.TwoSided:ShadowCastingMode.Off,true);
            }
            private readonly Matrix4x4[] batch=new Matrix4x4[NativeIntroSetLimits.Batch];private int batched,batchFrame=-1;
            public void Batch(string prop,ReadOnlySpan<System.Numerics.Vector3> at,ReadOnlySpan<System.Numerics.Quaternion> rotation,float scale,System.Numerics.Vector3 tint)
            {
                if(!Ready)return;if(batchFrame!=Time.frameCount){batchFrame=Time.frameCount;batched=0;}if(prop==null)return;SetPiece p=null;if(low)p=Find(Low(prop));p??=Find(prop);if(p==null)return;
                int n=Math.Min(Math.Min(at.Length,rotation.Length),NativeIntroSetLimits.Batch-batched);if(n<=0)return;
                for(int i=0;i<n;i++)batch[i]=Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(at[i])),CapturedAnimation.Quat(CapturedRig.Mirror(rotation[i])),Vector3.one*scale)*p.Place;
                batched+=n;block.Clear();bool tinted=tint.X!=1||tint.Y!=1||tint.Z!=1;if(tinted)block.SetVector(TintId,new Vector4(tint.X,tint.Y,tint.Z,0));
                for(int s=0;s<p.Subs;s++){if(SystemInfo.supportsInstancing)Graphics.DrawMeshInstanced(p.Mesh,s,s==0?solid:glow,batch,n,block,ShadowCastingMode.Off,false);else for(int i=0;i<n;i++)Graphics.DrawMesh(p.Mesh,batch[i],s==0?solid:glow,0,null,s,block,false,false);}
            }
            public void Primitive(WeaponShape shape,bool isGlow,System.Numerics.Vector3 at,System.Numerics.Quaternion rotation,System.Numerics.Vector3 scale,System.Numerics.Vector3 color)=>props?.Piece(shape,isGlow,at,rotation,scale,color);
        }
    }
}
