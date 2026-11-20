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
            if(!(script is INativeIntroV2 v2))return;string raw=pilot?.Seat?.name;IntroPackLoad();
            if(!ReferenceEquals(raw,introNameRaw)){introNameRaw=raw;introName=NativeCosmeticRules.Name(raw);}
            v2.Context=new NativeIntroContext{Reduced=WeaponEffectsQuality<.5f,Low=LowGraphics,Quality=quality,Name=introName};
        }
        // The intros' own motion bank (Core NativeIntroPack: pack clips as keys; the local assets' Resources, never the match's motion
        // bank), read once. Without one an intro's pack segments leave its keys as keyed.
        private static bool introPackRead;
        private static void IntroPackLoad()
        {
            if(introPackRead)return;introPackRead=true;var bytes=Resources.Load<TextAsset>(NativeIntroPack.Resource);if(bytes==null)return;
            try{NativeIntroPack.Local=NativeIntroPack.Read(bytes.bytes);}catch(System.Exception e){Debug.LogWarning("Intro motion bank: "+e.Message);}
        }
        /// <summary>A v2 intro's set at presentation time `t` (after its Props).</summary>
        private void IntroV2Set(NativeEntranceScript script,float t,Camera camera)
        {
            if(!(script is INativeIntroV2 v2))return;introSet??=new IntroSetDraw();
            introSet.Begin(v2.SetBank,LowGraphics,entranceProps);v2.Set(t,introSet);
        }
        // ---- the model's clip (INativeIntroBody) and the hands (INativeIntroHands) ----
        // (Model-native: the hands are the drawn model's joints, VisualJointFrame; without a model the keyed pose's anchors. No rig reads.)
        /// <summary>The v2 intro's body being bound this frame (VisualIntroPose) and how far its free arms relax (0: as keyed).</summary>
        private VisualBody introKeyedBody;private float introKeyedRelax;
        /// <summary>This frame's grip IK of the v2 weapon intro being bound (Core NativeIntroGrip: docs/INTROS-V2.md 9a).</summary>
        private bool introGripOn;private NativeIntroGripAt introGripAt;private string introGripSpec;
        /// <summary>This frame's share of the v2 intro's arms played from a pack clip (Core NativeIntroPack): drawn exactly as keyed.</summary>
        private float introPackArms;
        /// <summary>From VisualBodyBind, on the v2 intro's body: the drawn off fist on the drawn weapon's support point (the main hand's hold
        /// from the intro's weapon, the support bound whatever the hands' distance), when the intro has its grip on this frame.</summary>
        private void IntroV2Grip(ref NativeVisualRig.Grasp grasp,CapturedRig.Pose pose,VisualWeapon held)
        {
            grasp.Exact=introPackArms;if(!introGripOn)return;NativeIntroGrip.Grasp(introGripSpec,introGripAt,pose,ref grasp);
            // (the drawn weapon's own measured support point and stock, as the match takes them: VisualWeaponGrasp)
            if(held!=null)VisualWeaponGrasp(ref grasp,held);
        }
        private INativeIntroBody introBody;private int introBodyActor=-1,introV2Actor=-1,introModelFrame=-1;private float introBodyClock;private CapturedAnimation.Player introModel;
        /// <summary>Before the intro body is posed: a v2 intro's model clip for this frame, and a weapon intro's weapon in the model's hands.</summary>
        private void IntroV2BodyBegin(NativeEntranceScript script,Actor actor,string style,float t)
        {
            introModel=null;introV2Actor=-1;introGripOn=false;introPackArms=0;if(!(script is INativeIntroV2)||actor==null)return;introV2Actor=actor.Id;
            // (the arms a pack clip plays are the model's own: drawn exactly as keyed, Core NativeIntroPack)
            introPackArms=script is NativeProceduralEntrance packed?packed.PackWeight(t,NativeIntroPack.Arms):0;
            // (the model's free arms as keyed, unless the intro asks for the match's relax: Core NativeIntroV2Script.ArmsRelax)
            introKeyedRelax=script is NativeIntroV2Script keyed?Mathf.Clamp01(keyed.ArmsRelax(t)):0;
            var spec=NativeIntroCatalog.Find(style)?.Spec;if(spec!=null)actor.SourceSpec=spec;
            // (its grip IK, when it has one: Core NativeIntroGrip; read in VisualBodyBind through IntroV2Grip)
            if(spec!=null&&script is INativeIntroGrip grip&&grip.GripAt(t,out introGripAt)&&introGripAt.Weight>.001f){introGripOn=true;introGripSpec=spec;}
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
        /// <summary>
        /// The body as drawn, handed in before the lights, the set and the effects: the model-aware anchors (IntroV2Drawn), then the
        /// optional hands and head (INativeIntroHands, INativeIntroHead). The hands are the model's wrist joints (VisualJointFrame) when the
        /// model is drawn this frame, else the keyed pose's hand anchors; the head is the model's head as the Head anchor has it (its
        /// centre, on the head's turn), else the keyed one.
        /// </summary>
        private void IntroV2Hands(NativeEntranceScript script,float t)
        {
            IntroV2Drawn(script,t);
            var hands=script as INativeIntroHands;var head=script as INativeIntroHead;if(hands==null&&head==null)return;bool model=introModel!=null&&introModelFrame==Time.frameCount;
            NativeIntroHand Hand(int j,NativeEntranceAnchor anchor)
            {
                if(!model)return new NativeIntroHand{Model=false,At=script.Anchor(anchor,t),Turn=System.Numerics.Quaternion.Identity};
                var m=VisualJointFrame(introModel,j);var p=(Vector3)m.GetColumn(3);var q=m.rotation;
                // (the head's point: its centre on the model, where the Head anchor puts it; its turn the joint frame's, the keyed head's)
                var at=j==CapturedRig.Head&&script.IsDrawn(NativeEntranceAnchor.Head)?script.Anchor(NativeEntranceAnchor.Head,t):new System.Numerics.Vector3(-p.x,p.y,p.z);
                return new NativeIntroHand{Model=true,At=at,Turn=new System.Numerics.Quaternion(q.x,-q.y,-q.z,q.w)};
            }
            if(head!=null)head.Head(t,Hand(CapturedRig.Head,NativeEntranceAnchor.Head));if(hands==null)return;
            var right=Hand(CapturedRig.RHand,NativeEntranceAnchor.RightHand);var left=Hand(CapturedRig.LHand,NativeEntranceAnchor.LeftHand);
            // A gun's muzzle and bore on the drawn weapon: the RightMuzzle anchor (the drawn skin's crown, as the match's flash leaves it) and
            // the weapon frame's +z (IntroV2Drawn).
            if(model&&introDrawnBore.sqrMagnitude>.5f&&script.IsDrawn(NativeEntranceAnchor.RightMuzzle)&&NativeWeaponDressing.Gun(NativeIntroCatalog.Find(script.Style)?.Spec))
            {right.HasMuzzle=true;right.Muzzle=script.Anchor(NativeEntranceAnchor.RightMuzzle,t);right.Bore=new System.Numerics.Vector3(-introDrawnBore.x,introDrawnBore.y,introDrawnBore.z);}
            hands.Hands(t,right,left);
        }
        // ---- the model-aware anchors (docs/INTROS-V2.md "Anchors") ----
        // Each frame a v2 intro's model is drawn, every anchor is handed to the script where the model has it (Core NativeEntranceScript.SetDrawn),
        // so the authors' Anchor calls follow the drawn body: the pelvis, the chest (the torso's line half way up the chest), the head's centre,
        // the eyes and the visor's front on the head bone (its frame: x across, y up, z ahead, the face gear's), the shoulders, elbows, wrists,
        // palms, knees, ankles and toes (the balls of the feet); and, while its weapon is drawn, each weapon hand's grip (the weapon frame's
        // origin, as the match's sweep reads it), tip and muzzle (Core NativeIntroWeapon's sockets on that frame; a gun's muzzle the drawn skin's
        // crown, NativeMuzzleFx.Crown). Without the model (Classic, a hidden body) the script keeps its keyed anchors. Source frame (x
        // mirrored). No rig reads here: the model's bones and the weapon frames the weapon draw already made.
        private readonly System.Numerics.Vector3[] introDrawn=new System.Numerics.Vector3[(int)NativeEntranceAnchor.Count];private Vector3 introDrawnBore;
        /// <summary>The frame IntroWeapon drew a v2 intro's weapon on, and whether through the bow's own draw (its frame: bowHand).</summary>
        private int introWeaponAt=-1;private bool introWeaponBow;
        // The head's points in the head bone's frame (Core NativeRobotHelm's: the shell's centre, the eyes behind the visor, the visor's glass;
        // a face without the helmet has its front nearer).
        private static readonly Vector3 HeadCentre=new Vector3(0,.07f,.033f),HeadEyes=new Vector3(0,.065f,.10f),HeadVisor=new Vector3(0,.065f,.165f),FaceVisor=new Vector3(0,.065f,.125f);
        private VisualBody introPalmBody;private int introPalmL=-1,introPalmR=-1;
        private void IntroV2Drawn(NativeEntranceScript script,float t)
        {
            script.ClearDrawn();introDrawnBore=Vector3.zero;if(!(script is INativeIntroV2))return;ulong mask=0;
            VisualBody body=null;
            if(introModel!=null&&introModelFrame==Time.frameCount&&visualBodies.TryGetValue(introModel,out var b)&&b.Root!=null&&b.Binding!=null&&b.PosedFrame==Time.frameCount)body=b;
            if(body!=null)
            {
                var of=body.Binding.Rig.Of;var bones=body.Binding.Bones;
                Transform J(int j){int v=j>=0&&j<of.Length?of[j]:-1;return v>=0&&v<bones.Length?bones[v]:null;}
                void Put(NativeEntranceAnchor a,Vector3 p){introDrawn[(int)a]=new System.Numerics.Vector3(-p.x,p.y,p.z);mask|=1UL<<(int)a;}
                var pelvis=J(CapturedRig.Pelvis);if(pelvis!=null)Put(NativeEntranceAnchor.Pelvis,pelvis.position);
                var spine=J(CapturedRig.Spine);var neck=J(CapturedRig.Neck);if(spine!=null&&neck!=null)Put(NativeEntranceAnchor.Chest,(spine.position+neck.position)*.5f);
                var head=J(CapturedRig.Head);
                if(head!=null){Put(NativeEntranceAnchor.Head,head.TransformPoint(HeadCentre));Put(NativeEntranceAnchor.Eyes,head.TransformPoint(HeadEyes));Put(NativeEntranceAnchor.Visor,head.TransformPoint(body.Helm?HeadVisor:FaceVisor));}
                if(introPalmBody!=body)
                {
                    introPalmBody=body;var names=body.Binding.Rig.Names;introPalmL=Array.IndexOf(names,"middle_01_l");introPalmR=Array.IndexOf(names,"middle_01_r");
                }
                for(int side=0;side<2;side++)
                {
                    bool left=side==0;var hand=J(left?CapturedRig.LHand:CapturedRig.RHand);int palm=left?introPalmL:introPalmR;
                    if(hand!=null)
                    {
                        Put(left?NativeEntranceAnchor.LeftWrist:NativeEntranceAnchor.RightWrist,hand.position);
                        Put(left?NativeEntranceAnchor.LeftHand:NativeEntranceAnchor.RightHand,palm>=0&&palm<bones.Length&&bones[palm]!=null?(hand.position+bones[palm].position)*.5f:hand.position);
                    }
                    var elbow=J(left?CapturedRig.LElbow:CapturedRig.RElbow);if(elbow!=null)Put(left?NativeEntranceAnchor.LeftElbow:NativeEntranceAnchor.RightElbow,elbow.position);
                    var shoulder=J(left?CapturedRig.LArm:CapturedRig.RArm);if(shoulder!=null)Put(left?NativeEntranceAnchor.LeftShoulder:NativeEntranceAnchor.RightShoulder,shoulder.position);
                    int hip=left?CapturedRig.LHip:CapturedRig.RHip;
                    var knee=J(hip+1);if(knee!=null)Put(left?NativeEntranceAnchor.LeftKnee:NativeEntranceAnchor.RightKnee,knee.position);
                    var ankle=J(hip+2);if(ankle!=null)Put(left?NativeEntranceAnchor.LeftFoot:NativeEntranceAnchor.RightFoot,ankle.position);
                    var toe=J(hip+3);if(toe!=null)Put(left?NativeEntranceAnchor.LeftToe:NativeEntranceAnchor.RightToe,toe.position);
                }
                // The weapon, while it is drawn this frame: each hand's weapon frame as the weapon was drawn on it.
                string spec=NativeIntroCatalog.Find(script.Style)?.Spec;
                if(spec!=null&&introWeaponAt==Time.frameCount)
                {
                    bool standard=NativeWeaponSkins.Index(introSkins,spec)!=NativeWeaponSkins.PrototypeIndex;
                    for(int side=0;side<2;side++)
                    {
                        bool left=side==0;if(!NativeIntroWeapon.Tip(spec,left,out var tip))continue;NativeIntroWeapon.Muzzle(spec,left,out var muzzle);NativeIntroWeapon.Grip(spec,left,out var grip);
                        int hb=left?CapturedRig.LHand:CapturedRig.RHand;Matrix4x4 w;
                        if(introWeaponBow){if(!left)continue;w=bowHand;}
                        else if(bladeIntro!=null)w=BladeIntroFrame(hb,introGrip);
                        else if(introHand!=null)w=introHand(hb);else continue;
                        // (a gun's flash leaves the drawn skin's crown, as the match's does)
                        if(NativeWeaponDressing.Gun(spec))muzzle=new System.Numerics.Vector3(0,0,NativeMuzzleFx.Crown(spec,standard));
                        Put(left?NativeEntranceAnchor.LeftGrip:NativeEntranceAnchor.RightGrip,w.MultiplyPoint3x4(CapturedAnimation.Vec(CapturedRig.Mirror(grip))));
                        Put(left?NativeEntranceAnchor.LeftTip:NativeEntranceAnchor.RightTip,w.MultiplyPoint3x4(CapturedAnimation.Vec(CapturedRig.Mirror(tip))));
                        Put(left?NativeEntranceAnchor.LeftMuzzle:NativeEntranceAnchor.RightMuzzle,w.MultiplyPoint3x4(CapturedAnimation.Vec(CapturedRig.Mirror(muzzle))));
                        if(!left){var bore=w.MultiplyVector(Vector3.forward);if(bore.sqrMagnitude>1e-8f)introDrawnBore=bore.normalized;}
                    }
                }
            }
            if(mask!=0)script.SetDrawn(t,introDrawn,mask);
        }
        /// <summary>A v2 intro's entrance ghost (an afterimage, Whiteout's frost coat) wears its hair as the live body does: hidden under the
        /// robot's helmet, pressed under a hat (VisualHatHair). Its body is made bare of that (VisualLiveGhostDraw), so the hair stood out
        /// through the helmet as a see-through spiky crown (Whiteout's close-up, 2026-11-20).</summary>
        private void IntroV2GhostHair(long key)
        {
            if(!visualLiveGhosts.TryGetValue(key,out var g)||g.Body?.Root==null||introModel==null||introModelFrame!=Time.frameCount||!visualBodies.TryGetValue(introModel,out var live)||live.Root==null)return;
            g.Body.Helm=live.Helm;VisualHatHair(g.Body,entranceGhostActor?.Hat);
        }
        /// <summary>
        /// A v2 intro's entrance ghost is the whole dressed pilot (owner, 2026-11-20: "the 'hologram' appearance, shows the model without the
        /// cosmetics (Like it shows a blank model without armor)"): its body wears the pilot's outfit already (VisualLiveGhostDraw), and here
        /// the pieces the live body draws on top of it come too, through the entrance ghost's shader in the ghost's tint and scanlines: the
        /// robot armour's plates (VisualSuit), the helmet or half mask and the concept's mask and visor (VisualFaceGear), the hat
        /// (VisualHatFit), the chest core, the back emblem and the wrist device (VisualGear). A ghost marked Bare draws none of them (an intro
        /// that draws its own over it).
        /// </summary>
        private Matrix4x4 introGhostGear;private Func<int,Matrix4x4> introGhostGearBone;
        private void IntroV2GhostGear(long key,Color tint,Color scan,float opacity)
        {
            if(sourceModels==null||!visualLiveGhosts.TryGetValue(key,out var g)||g.Body?.Root==null)return;var gb=g.Body;var head=VisualHeadOf(gb);
            int id=introActor;bool dog=NativeHotDog.Worn(VisualOutfitOf(id));introGhostGearBone??=_=>introGhostGear;
            void Part(string model,Matrix4x4 at){if(model==null)return;var ghost=sourceModels.EntranceGhost(model);if(ghost==null)return;introGhostGear=at;sourceModels.Queue(ghost,at,introGhostGearBone,null,0,null,tint,scan,opacity);}
            // the armour's plates on the ghost's own suit bones
            int shown=SuitShown(id);var suit=shown!=0&&sourceModels.Has(NativeSourceModels.Suit)?sourceModels.EntranceGhost(NativeSourceModels.Suit):null;var plates=suit!=null?SuitRigOf(gb.Root):null;
            if(plates!=null&&(shown&=plates.Found)!=0)
            {
                for(int i=0;i<suitMatrices.Length;i++)suitMatrices[i]=plates.Bones[i]!=null?plates.Bones[i].localToWorldMatrix*plates.Bind[i]:Matrix4x4.identity;
                suitBone??=i=>suitMatrices[i];suitShown=shown;suitVisible??=b=>(suitShown>>b&1)!=0;sourceModels.Queue(suit,Matrix4x4.identity,suitBone,suitVisible,0,null,tint,scan,opacity);
            }
            // the face gear and the hat on the ghost's head
            int gear=dog?NativeWeaponSkins.FaceNone:FaceGearOf(id);
            if(head!=null&&gear!=NativeWeaponSkins.FaceNone&&sourceModels.PilotFace())
            {
                bool helm=gear==NativeWeaponSkins.FaceHelm;
                if(helm||gear==NativeWeaponSkins.FaceHalf)Part(helm?NativeSourceModels.Helm:NativeSourceModels.HalfMask,head.localToWorldMatrix);
                else{if(gear==NativeWeaponSkins.FaceBoth||gear==NativeWeaponSkins.FaceMask)Part(NativeSourceModels.GearMask,head.localToWorldMatrix);if(gear==NativeWeaponSkins.FaceBoth||gear==NativeWeaponSkins.FaceVisor)Part(NativeSourceModels.GearVisor,head.localToWorldMatrix);}
            }
            string hat=entranceGhostActor?.Hat;
            if(head!=null&&!string.IsNullOrEmpty(hat)&&hat!="none"&&hat!=NativeSheldonHead.Hat)Part(VisualHatModel(hat,entranceGhostActor.HatColor),VisualHatFit(head,hat,gear==NativeWeaponSkins.FaceHelm));
            // the pilot gear: the chest core, the back emblem, the wrist device
            if(sourceModels.PilotGear(gb.GearRadius>.01f?gb.GearRadius:.04f))
            {
                if(gb.GearCore!=null&&!dog)Part(NativeSourceModels.GearCore,gb.GearCore.localToWorldMatrix);
                if(!WearsArmour(id)&&!dog){var of=gb.Binding.Rig.Of;int cv=CapturedRig.Chest<of.Length?of[CapturedRig.Chest]:-1;var chest=cv>=0&&cv<gb.Binding.Bones.Length?gb.Binding.Bones[cv]:null;if(chest!=null)Part(NativeSourceModels.GearEmblem,chest.localToWorldMatrix*gb.Emblem);}
                if(gb.GearDevice!=null)Part(sourceModels.GearDeviceFor(gb.GearRadius),gb.GearDevice.localToWorldMatrix);
            }
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
