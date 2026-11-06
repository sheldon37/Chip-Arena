using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NV=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // NOCTIEL's body (docs/EXPEDITION-BOSS-FINAL.md section 4, tools/gear/fallen_boss.py): the pilot's skeleton scaled up and posed from the pack's clips (nothing of the pilot's mesh is drawn), the bank's rigid pieces on its bones, the wings, robe, halo and ring of blades on chains
    // turned here with their own weight. Everything is drawn twice: itself, and mirrored in the water. Presentation only: no rule reads any of it.
    public sealed partial class ArenaPresentation
    {
        private const string FallenBank="ChipArenaPackBossFallen";
        private sealed class FbPart{public readonly Mesh[] Mesh=new Mesh[2];public readonly bool[] Glows=new bool[2];public int Group;public byte Phases;public float GlowScale=1;public string Name;public bool Trim;}
        private GameObject fbRoot;private HumanPoseHandler fbHandler;private HumanPose fbPose;private float[] fbMix,fbRest;private int[] fbFreeArm,FbKneelAt;private static readonly string[] FbKneelNames={"Left Upper Leg Front-Back","Left Upper Leg In-Out","Left Lower Leg Stretch","Left Foot Up-Down","Right Upper Leg Front-Back","Right Upper Leg In-Out","Right Lower Leg Stretch","Right Foot Up-Down","Spine Front-Back","Chest Front-Back","UpperChest Front-Back","Neck Nod Down-Up","Head Nod Down-Up","Left Arm Down-Up","Left Forearm Stretch"};private static readonly float[] FbKneelTo={.36f,.52f,-1f,.52f,-1.28f,.42f,-.84f,-.62f,-.2f,-.72f,-.5f,.62f,.86f,-.34f,.7f};private bool fbBuilt,fbFailed;
        private readonly Dictionary<string,Transform> fbBone=new Dictionary<string,Transform>();private readonly Dictionary<string,Matrix4x4> fbBind=new Dictionary<string,Matrix4x4>();
        private readonly Dictionary<string,int> fbGroupIndex=new Dictionary<string,int>();private Matrix4x4[] fbWorld;private int[] fbChainGroup,fbChainParent;private Transform[] fbGroupBone;
        private readonly List<FbPart> fbParts=new List<FbPart>();private readonly Dictionary<string,FbPart> fbFree=new Dictionary<string,FbPart>();
        private Material fbBody;private MaterialPropertyBlock fbBlock;
        private Transform fbHead,fbNeck,fbHandR,fbUpperR,fbLowerR,fbSpine,fbFootL,fbFootR,fbBallL,fbBallR;
        public int FallenBossDraws{get;private set;}
        // The sword in hand_r (tools/gear/fallen_boss.py to_sword): the fist's centre, the blade's way, the flat's way. Metres on the skeleton before its scale.
        private static readonly Vector3 FbGrip=new Vector3(.096f,-.018f,-.015f),FbBladeWay=new Vector3(.144f,.468f,.872f).normalized;private static Vector3 FbFlatWay=new Vector3(.349f,-.848f,.398f);
        private const float FbBladeFrom=.15f,FbBladeLength=1.5f;
        private Vector3 FbTipLocal=>FbGrip+FbBladeWay*(FbBladeFrom+FbBladeLength);private Vector3 FbHiltLocal=>FbGrip+FbBladeWay*(FbBladeFrom+.1f);

        private bool FallenBodyBuild()
        {
            if(fbBuilt)return !fbFailed;fbBuilt=true;fbFailed=true;
            var shader=Resources.Load<Shader>("NativeFallenBody");if(shader==null||!shader.isSupported||visualBodyPrefab==null||VisualMotionBank==null){Debug.LogWarning("NOCTIEL: needs the toon body bake, the motion bank and its shader; not drawn");return false;}
            var bank=NativePackBanks.Bank(FallenBank);if(bank==null){Debug.LogWarning("NOCTIEL: no "+FallenBank+" bank; not drawn");return false;}
            fbBody=new Material(shader){name="NOCTIEL",hideFlags=HideFlags.HideAndDontSave};fbBlock=new MaterialPropertyBlock();
            fbRoot=Instantiate(visualBodyPrefab);fbRoot.name="NOCTIEL";fbRoot.hideFlags=HideFlags.DontSave;fbRoot.SetActive(true);foreach(var r in fbRoot.GetComponentsInChildren<Renderer>(true))r.enabled=false;
            var skeleton=fbRoot.transform.Find("VisualSkeleton");var animator=skeleton!=null?skeleton.GetComponent<Animator>():null;if(animator==null||animator.avatar==null){Debug.LogWarning("NOCTIEL: no skeleton");return false;}
            animator.runtimeAnimatorController=null;animator.enabled=false;fbRoot.transform.localScale=Vector3.one*NativeFallenBoss.Scale;
            foreach(var t in skeleton.GetComponentsInChildren<Transform>(true))fbBone[t.name]=t;
            SkinnedMeshRenderer most=null;foreach(var r in fbRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(most==null||r.bones.Length>most.bones.Length)most=r;
            if(most==null){Debug.LogWarning("NOCTIEL: no bound mesh");return false;}
            var bones=most.bones;var binds=most.sharedMesh.bindposes;for(int i=0;i<bones.Length&&i<binds.Length;i++)if(bones[i]!=null)fbBind[bones[i].name]=binds[i];
            Transform B(string n)=>fbBone.TryGetValue(n,out var t)?t:null;fbHead=B("head");fbNeck=B("neck_01");fbHandR=B("hand_r");fbUpperR=B("upperarm_r");fbLowerR=B("lowerarm_r");fbSpine=B("spine_05");fbFootL=B("foot_l");fbFootR=B("foot_r");fbBallL=B("ball_l");fbBallR=B("ball_r");
            if(fbHead==null||fbHandR==null||fbUpperR==null||fbLowerR==null||fbSpine==null||fbFootL==null||fbFootR==null){Debug.LogWarning("NOCTIEL: bones missing");return false;}
            fbHandler=new HumanPoseHandler(animator.avatar,skeleton);int muscles=VisualMotionBank.Muscles;fbPose.muscles=new float[muscles];fbMix=new float[NativeVisualMotionClips.Head+muscles];fbRest=new float[fbMix.Length];
            var free=new List<int>();var names=HumanTrait.MuscleName;for(int i=0;i<names.Length&&i<muscles;i++){var n=names[i];if(n.StartsWith("Left Arm",StringComparison.Ordinal)||n.StartsWith("Left Forearm",StringComparison.Ordinal)||n.StartsWith("Left Hand",StringComparison.Ordinal)||n.StartsWith("Left Shoulder",StringComparison.Ordinal)||n.StartsWith("LeftHand.",StringComparison.Ordinal))free.Add(i);}fbFreeArm=free.ToArray();FbKneelAt=new int[FbKneelNames.Length];for(int i=0;i<FbKneelAt.Length;i++)FbKneelAt[i]=Array.IndexOf(names,FbKneelNames[i]);
            // Groups: every bone a part rides, then each chain in order (a chain's parent comes first).
            int Group(string name){if(fbGroupIndex.TryGetValue(name,out int g))return g;g=fbGroupIndex.Count;fbGroupIndex[name]=g;return g;}
            foreach(var part in NativeFallenBossLook.Parts){bool chain=false;foreach(var c in NativeFallenBossLook.Chains)if(c.Name==part.Group){chain=true;break;}if(!chain)Group(part.Group);}
            foreach(var c in NativeFallenBossLook.Chains){if(!fbGroupIndex.ContainsKey(c.Parent))Group(c.Parent);}
            int bonesCount=fbGroupIndex.Count;foreach(var c in NativeFallenBossLook.Chains)Group(c.Name);
            fbWorld=new Matrix4x4[fbGroupIndex.Count];fbGroupBone=new Transform[bonesCount];foreach(var kv in fbGroupIndex)if(kv.Value<bonesCount)fbGroupBone[kv.Value]=B(kv.Key);
            fbChainGroup=new int[NativeFallenBossLook.Chains.Length];fbChainParent=new int[fbChainGroup.Length];for(int i=0;i<fbChainGroup.Length;i++){fbChainGroup[i]=fbGroupIndex[NativeFallenBossLook.Chains[i].Name];fbChainParent[i]=fbGroupIndex[NativeFallenBossLook.Chains[i].Parent];}
            // The pieces.
            FbPart Part(string name)
            {
                var part=new FbPart{Name=name};
                for(int tier=0;tier<2;tier++)
                {
                    var prop=bank.Find(NativeFallenBossLook.Prefix+name+"-"+tier);if(prop==null||prop.Parts.Length==0)continue;
                    try{var built=ZombieGearBuild(bank,prop);if(built?.Mesh!=null){part.Mesh[tier]=built.Mesh;part.Glows[tier]=built.Mesh.subMeshCount>1;}}catch(Exception e){Debug.LogWarning("NOCTIEL "+name+": "+e.Message);}
                }
                return part.Mesh[0]==null&&part.Mesh[1]==null?null:part;
            }
            foreach(var lp in NativeFallenBossLook.Parts){var part=Part(lp.Name);if(part==null)continue;part.Group=fbGroupIndex[lp.Group];part.Phases=lp.Phases;fbParts.Add(part);}
            foreach(var n in new[]{"feather-one","cantor","penitent","throne-ring","throne-eye"}){var part=Part(n);if(part!=null)fbFree[n]=part;}
            var life=gameObject.GetComponent<NativeStationLifetime>();if(life==null)life=gameObject.AddComponent<NativeStationLifetime>();life.Gone+=DisposeFallenBoss;
            fbFailed=false;return true;
        }
        // ---------------- the pose ----------------
        private readonly Vector3[] fbRobeAngle=new Vector3[NativeFallenBossLook.RobeStrips],fbRobeSpeed=new Vector3[NativeFallenBossLook.RobeStrips];
        private Vector3 fbHeadLook;private readonly float[] fbWingX=new float[3],fbWingV=new float[3];private float fbWingLag,fbWingLagV,fbWingBack,fbLastYaw,fbPoseClock=-1;private Vector3 fbLastAt;private float fbBodyLean,fbBodyLeanV;
        private void FallenPose(FallenBossFrame f,float now,float dt,Vector3 origin,Vector3 eye)
        {
            var root=fbRoot.transform;var at=origin+NativeBossPoseApply.ToUnity(f.Boss);var bank=VisualMotionBank;
            Array.Clear(fbMix,0,fbMix.Length);float a=1-f.Mix,b=f.Mix;float sum=0;bool twoA=false,twoB=false;
            if(a>0&&f.ClipFrom!=null&&bank.Clips.TryGetValue(f.ClipFrom,out var ca)){NativeVisualMotionClips.Add(ca,f.TimeFrom,a,fbMix);sum+=a;twoA=f.ClipFrom.StartsWith("2Hand",StringComparison.Ordinal);}
            if(b>0&&f.ClipTo!=null&&bank.Clips.TryGetValue(f.ClipTo,out var cb)){NativeVisualMotionClips.Add(cb,f.TimeTo,b,fbMix);sum+=b;twoB=f.ClipTo.StartsWith("2Hand",StringComparison.Ordinal);}
            if(sum<=0&&bank.Clips.TryGetValue("Armed-Idle",out var ci)){NativeVisualMotionClips.Add(ci,now,1,fbMix);sum=1;}
            NativeVisualMotionClips.Finish(fbMix,sum);
            // It holds the claymore in one hand: a two-handed clip's left arm stays its own.
            float off=(twoA?a:0)+(twoB?b:0);
            if(off>0&&bank.Clips.TryGetValue("Armed-Idle",out var ir))
            {
                Array.Clear(fbRest,0,fbRest.Length);NativeVisualMotionClips.Add(ir,now*.4f,1,fbRest);NativeVisualMotionClips.Finish(fbRest,1);int head=NativeVisualMotionClips.Head;
                foreach(int m in fbFreeArm)fbMix[head+m]=Mathf.Lerp(fbMix[head+m],fbRest[head+m],off);
            }
            if(f.Kneel>0){int head=NativeVisualMotionClips.Head;for(int i=0;i<FbKneelAt.Length;i++)if(FbKneelAt[i]>=0)fbMix[head+FbKneelAt[i]]=Mathf.Lerp(fbMix[head+FbKneelAt[i]],FbKneelTo[i],f.Kneel);}
            fbPose.bodyPosition=new Vector3(fbMix[0],fbMix[1],fbMix[2]);fbPose.bodyRotation=new Quaternion(fbMix[3],fbMix[4],fbMix[5],fbMix[6]);Array.Copy(fbMix,NativeVisualMotionClips.Head,fbPose.muscles,0,fbPose.muscles.Length);
            // Leaning into what it does: its weight lags its speed.
            var velocity=dt>0&&fbPoseClock>=0?(at-fbLastAt)/dt:Vector3.zero;if(velocity.sqrMagnitude>3600)velocity=Vector3.zero;var yawRot=Quaternion.AngleAxis(f.Yaw,Vector3.up);var local=Quaternion.Inverse(yawRot)*velocity;
            float lean=Mathf.Clamp(local.z*.55f,-9,20);BossSpringStep(ref fbBodyLean,ref fbBodyLeanV,lean,dt,.09f);
            root.SetPositionAndRotation(at,yawRot*Quaternion.AngleAxis(fbBodyLean,Vector3.right));
            fbHandler.SetHumanPose(ref fbPose);
            // The soles on the water, or as high as the move has them.
            float s=NativeFallenBoss.Scale;float low=Mathf.Min(fbFootL.position.y-.11f*s,fbFootR.position.y-.11f*s);if(fbBallL!=null)low=Mathf.Min(low,fbBallL.position.y-.013f*s);if(fbBallR!=null)low=Mathf.Min(low,fbBallR.position.y-.013f*s);
            float hover=f.Phase==4&&f.Kind==NativeFallenBossPreview.KindFight?.3f+.05f*Mathf.Sin(now*1.3f):0;root.position+=Vector3.up*(origin.y+f.Air+hover-low);
            // The fist on the planted sword.
            if(f.Plant>0)FallenPlant(f,origin);else if(f.Carry>0)FallenCarry(f);
            // The head on the pilot it's after, a little behind them.
            if(f.Track>0&&f.LookAtEye<=0)
            {
                var want=origin+NativeBossPoseApply.ToUnity((f.Live!=null?f.Target:NativeFallenPilots.At(f.TrackPilot,f.P)))+Vector3.up*1.4f;fbHeadLook=fbPoseClock<0||(fbHeadLook-want).sqrMagnitude>10000?want:Vector3.Lerp(fbHeadLook,want,1-Mathf.Exp(-dt*5));
                var d=(fbHeadLook-fbHead.position).normalized;var forward=root.forward;if(Vector3.Dot(d,forward)<-.1f)d=Vector3.Slerp(forward,d,.4f);
                var turn=Quaternion.FromToRotation(fbHead.forward,d);if(fbNeck!=null)fbNeck.rotation=Quaternion.Slerp(Quaternion.identity,turn,.28f*f.Track)*fbNeck.rotation;
                turn=Quaternion.FromToRotation(fbHead.forward,d);fbHead.rotation=Quaternion.Slerp(Quaternion.identity,turn,.7f*f.Track)*fbHead.rotation;
            }
            // The head to the eye.
            if(f.LookAtEye>0)
            {
                var d=(eye-fbHead.position).normalized;var turn=Quaternion.FromToRotation(fbHead.forward,d);if(fbNeck!=null)fbNeck.rotation=Quaternion.Slerp(Quaternion.identity,turn,.3f*f.LookAtEye)*fbNeck.rotation;
                turn=Quaternion.FromToRotation(fbHead.forward,(eye-fbHead.position).normalized);fbHead.rotation=Quaternion.Slerp(Quaternion.identity,turn,.85f*f.LookAtEye)*fbHead.rotation;
            }
            FallenChains(f,now,dt,yawRot,local);
            fbLastAt=at;fbLastYaw=f.Yaw;fbPoseClock=now;
        }
        private static void BossSpringStep(ref float x,ref float v,float target,float dt,float smooth){float w=2f/Mathf.Max(.001f,smooth),k=1f/(1f+w*dt+.48f*w*w*dt*dt+.235f*w*w*w*dt*dt*dt),c=x-target,t=(v+w*c)*dt;v=(v-w*t)*k;x=target+(c+t)*k;}
        // Where the sword stands in the water beside it (its right hand's side), the fist on the grip.
        internal static Vector3 FallenPlantPoint(FallenBossFrame f,float lift)=>NativeBossPoseApply.ToUnity(NativeFallenSongPlant.At(f.Boss,f.Yaw))+Vector3.up*(NativeFallenSongPlant.GripY+lift*NativeFallenSongPlant.LiftBy);
        // The fist closes where the sword should be: upright point-down in the water (planted), or held low and forward as it stands.
        private void FallenPlant(FallenBossFrame f,Vector3 origin){var yawRot=Quaternion.AngleAxis(f.Yaw,Vector3.up);FallenSwordTo(origin+FallenPlantPoint(f,f.SwordLift),Quaternion.LookRotation(Vector3.down,yawRot*Vector3.forward),f.Plant,yawRot*new Vector3(.9f,-.5f,-.6f));}
        private void FallenCarry(FallenBossFrame f)
        {
            var yawRot=Quaternion.AngleAxis(f.Yaw,Vector3.up);var root=fbRoot.transform.position;float breath=.02f*Mathf.Sin(fbPoseClock*.8f);var grip=root+yawRot*new Vector3(.86f,1.62f+breath,.42f);
            var blade=yawRot*new Vector3(.52f,-.44f,.73f);FallenSwordTo(grip,Quaternion.LookRotation(blade.normalized,yawRot*new Vector3(.82f,.3f,-.42f)),f.Carry,yawRot*new Vector3(1f,-.4f,-.5f));
        }
        private void FallenSwordTo(Vector3 grip,Quaternion world,float w,Vector3 poleWay)
        {
            float s=NativeFallenBoss.Scale;var flat=FbFlatWay;Vector3.OrthoNormalize(ref FbBladeWayCopy,ref flat);var inHand=Quaternion.LookRotation(FbBladeWayCopy,flat);
            var hand=world*Quaternion.Inverse(inHand);
            var target=grip-hand*(FbGrip*s);var pole=fbUpperR.position+poleWay;
            var clipRot=fbHandR.rotation;var clipAt=fbHandR.position;var goal=Vector3.Lerp(clipAt,target,w);
            Vector3 sp=fbUpperR.position,el=fbLowerR.position,wr=fbHandR.position;float la=(el-sp).magnitude,lb=(wr-el).magnitude;var dv=goal-sp;float l=Mathf.Clamp(dv.magnitude,Mathf.Abs(la-lb)+.001f,la+lb-.001f);dv=dv.normalized;
            float cos=(la*la+l*l-lb*lb)/(2*la*l),sin=Mathf.Sqrt(Mathf.Max(0,1-cos*cos));var sd=pole-sp;sd=(sd-Vector3.Dot(sd,dv)*dv).normalized;var e2=sp+dv*(la*cos)+sd*(la*sin);
            fbUpperR.rotation=Quaternion.FromToRotation(el-sp,e2-sp)*fbUpperR.rotation;fbLowerR.rotation=Quaternion.FromToRotation(fbHandR.position-fbLowerR.position,goal-fbLowerR.position)*fbLowerR.rotation;
            fbHandR.rotation=Quaternion.Slerp(clipRot,hand,w);
        }
        private Vector3 FbBladeWayCopy=new Vector3(.144f,.468f,.872f).normalized;
        // Wings, robe, halo, blades: each link's matrix. Rest is spread wings, a hanging robe; each is turned from there in the skeleton's mesh axes.
        private static readonly float[] FbFoldYaw={68,-30,14},FbFoldRoll={-68,-14,-6};
        private void FallenChains(FallenBossFrame f,float now,float dt,Quaternion yawRot,Vector3 localVelocity)
        {
            for(int i=0;i<fbGroupBone.Length;i++)if(fbGroupBone[i]!=null)fbWorld[i]=fbGroupBone[i].localToWorldMatrix;
            // The wings lag a turn and blow back with its speed.
            float yawSpeed=dt>0&&fbPoseClock>=0?Mathf.DeltaAngle(fbLastYaw,f.Yaw)/dt:0;BossSpringStep(ref fbWingLag,ref fbWingLagV,Mathf.Clamp(-yawSpeed*.09f,-34,34),dt,.16f);
            float back=Mathf.Clamp(localVelocity.z*1.5f,-12,38);fbWingBack=Mathf.Lerp(fbWingBack,back,1-Mathf.Exp(-dt*7));
            for(int w=0;w<3;w++)
            {
                float goal=Mathf.Clamp01(f.WingOpen[w]+f.Flare*(.5f-.06f*w));
                if(dt<=0){fbWingX[w]=goal;fbWingV[w]=0;continue;}
                float stiff=58-w*10,damp=7.5f-w*.6f;fbWingV[w]+=(goal-fbWingX[w])*stiff*dt;fbWingV[w]*=Mathf.Exp(-damp*dt);fbWingX[w]+=fbWingV[w]*dt;
            }
            var chains=NativeFallenBossLook.Chains;float s=NativeFallenBoss.Scale;var rootScale=Vector3.one*s;Vector3 rel0;
            for(int k=0;k<chains.Length;k++)
            {
                var c=chains[k];var parent=fbWorld[fbChainParent[k]];var pivot=new Vector3(c.Pivot.X,c.Pivot.Y,c.Pivot.Z);
                if(c.Kind==NativeFallenBossLook.KindWing)
                {
                    if(c.Segment==0)
                    {
                        var at=parent*fbBind[c.Parent];var chest=at.rotation;var square=Quaternion.Slerp(yawRot,chest,.3f);
                        parent=Matrix4x4.TRS(at.MultiplyPoint3x4(Vector3.zero),square,rootScale);rel0=at.MultiplyPoint3x4(pivot);parent=Matrix4x4.TRS(rel0,square,rootScale);
                    }
                    float open=Mathf.Clamp(fbWingX[c.Index],-.08f,1.14f);float closed=1-open;int seg=c.Segment;float side=c.Side;
                    float breath=(.9f+open*2.4f)*Mathf.Sin(now*.85f+c.Index*.7f-seg*.55f);
                    float yaw=side*(FbFoldYaw[seg]*closed+(seg==0?fbWingBack*.5f:seg==1?fbWingBack*.3f:0))+fbWingLag*(seg==0?.45f:seg==1?.3f:.2f);
                    float roll=side*(FbFoldRoll[seg]*closed+breath*.8f*(seg==0?1:.7f));
                    var rel=c.Segment==0?Vector3.zero:pivot-new Vector3(chains[k-1].Pivot.X,chains[k-1].Pivot.Y,chains[k-1].Pivot.Z);
                    fbWorld[fbChainGroup[k]]=parent*Matrix4x4.TRS(rel,Quaternion.AngleAxis(yaw,Vector3.up)*Quaternion.AngleAxis(roll,Vector3.forward),Vector3.one);
                }
                else if(c.Kind==NativeFallenBossLook.KindRobe)
                {
                    // The strips hang from the waist whatever it does, swing with its speed and are pushed by its thighs.
                    var hang=c.Segment==0?fbRobeAngle[c.Index]:fbRobeAngle[c.Index]*(1+c.Segment*.35f);
                    Vector3 place;if(c.Segment==0)place=(parent*fbBind[c.Parent]).MultiplyPoint3x4(pivot);else place=parent.MultiplyPoint3x4(pivot-new Vector3(chains[k-1].Pivot.X,chains[k-1].Pivot.Y,chains[k-1].Pivot.Z));
                    var turn=yawRot*Quaternion.AngleAxis(hang.x,Vector3.right)*Quaternion.AngleAxis(hang.z,Vector3.forward);
                    fbWorld[fbChainGroup[k]]=Matrix4x4.TRS(place,turn,rootScale);
                }
                else if(c.Kind==NativeFallenBossLook.KindHalo)
                {
                    float tilt=f.Phase>=2?14:0;fbWorld[fbChainGroup[k]]=parent*fbBind[c.Parent]*Matrix4x4.TRS(pivot,Quaternion.AngleAxis(tilt,Vector3.right)*Quaternion.AngleAxis((f.Phase==3?now*24:now*2.2f)+(f.Phase>=2?9:0),Vector3.forward),Vector3.one);
                }
                else
                {
                    fbWorld[fbChainGroup[k]]=parent*fbBind[c.Parent]*Matrix4x4.TRS(pivot,Quaternion.AngleAxis(c.Index*360f/NativeFallenBossLook.RingBlades+now*(26+Mathf.Sin(now*.4f)*6),Vector3.forward),Vector3.one);
                }
            }
            // The robe's own weight.
            if(dt>0)for(int i=0;i<NativeFallenBossLook.RobeStrips;i++)
            {
                float noise=2.2f*Mathf.Sin(now*1.1f+i*1.7f)+1.4f*Mathf.Sin(now*2.3f+i*.9f);var goal=new Vector3(Mathf.Clamp(localVelocity.z*3.2f,-30,62)+noise,0,Mathf.Clamp(-localVelocity.x*2.4f,-26,26)+noise*.6f);
                var d=goal-fbRobeAngle[i];fbRobeSpeed[i]+=(d*38-fbRobeSpeed[i]*7.5f)*dt;fbRobeAngle[i]+=fbRobeSpeed[i]*dt;
            }
        }
        // ---------------- the draw ----------------
        private void FbDraw(Mesh mesh,bool glows,in Matrix4x4 m,float glow,float flash,float mirror,Vector4? cut,float scaleGlow=1)
        {
            var block=fbBlock;block.Clear();block.SetFloat(FbGlowId,0);block.SetFloat(FbFlashId,flash);block.SetFloat(FbMirrorId,mirror);if(cut.HasValue)block.SetVector(FbClipId,cut.Value);else block.SetVector(FbClipId,FbNoCut);
            Graphics.DrawMesh(mesh,m,fbBody,0,null,0,block,ShadowCastingMode.Off,false);FallenBossDraws++;
            if(glows){block.SetFloat(FbGlowId,Mathf.Max(.001f,glow*scaleGlow));Graphics.DrawMesh(mesh,m,fbBody,0,null,1,block,ShadowCastingMode.Off,false);FallenBossDraws++;}
        }
        private static readonly Vector4 FbNoCut=new Vector4(0,0,0,1);
        private static readonly int FbGlowId=Shader.PropertyToID("_FbGlow"),FbFlashId=Shader.PropertyToID("_FbFlash"),FbMirrorId=Shader.PropertyToID("_FbMirror"),FbClipId=Shader.PropertyToID("_FbClipAxis");
        private void FallenBodyDraw(FallenBossFrame f,Vector3 origin,Vector3 eye,float inner,float flash,Matrix4x4 mirror)
        {
            int bit=1<<(f.Phase-1);int tier=(eye-fbRoot.transform.position).sqrMagnitude<1600?0:1;if(LowGraphics)tier=1;
            float reach=Vector3.Dot(FbGrip,FbBladeWay)+FbBladeFrom+FbBladeLength*f.Reveal;var cut=new Vector4(FbBladeWay.x,FbBladeWay.y,FbBladeWay.z,reach);bool sealedFist=f.Plant>0&&f.Kind==NativeFallenBossPreview.KindDeath;
            foreach(var part in fbParts)
            {
                if((part.Phases&bit)==0)continue;var mesh=part.Mesh[tier]??part.Mesh[0];if(mesh==null)continue;bool glows=mesh.subMeshCount>1;var world=fbWorld[part.Group];
                Vector4? c=part.Name=="sword-veins-1"?cut:(Vector4?)null;
                float gs=part.Name=="sword-blade-light"?.72f:part.Name=="head-light"?.8f:1;FbDraw(mesh,glows,world,inner,flash,0,c,gs);FbDraw(mesh,glows,mirror*world,inner,flash,1,c,gs);
            }
        }
        /// <summary>The sword alone, standing in the water after it's gone.</summary>
        private void FallenSwordAlone(FallenBossFrame f,Vector3 origin,float inner,Matrix4x4 mirror)
        {
            int bit=1<<(f.Phase-1);var yawRot=Quaternion.AngleAxis(f.Yaw,Vector3.up);var grip=origin+FallenPlantPoint(f,0);var flat=FbFlatWay;Vector3.OrthoNormalize(ref FbBladeWayCopy,ref flat);var inHand=Quaternion.LookRotation(FbBladeWayCopy,flat);var hand=Quaternion.LookRotation(Vector3.down,yawRot*Vector3.forward)*Quaternion.Inverse(inHand);
            float s=NativeFallenBoss.Scale;var world=Matrix4x4.TRS(grip-hand*(FbGrip*s),hand,Vector3.one*s);
            foreach(var part in fbParts)
            {
                if(!part.Name.StartsWith("sword",StringComparison.Ordinal)||part.Name=="sword-flakes"||(part.Phases&bit)==0)continue;var mesh=part.Mesh[0];if(mesh==null)continue;bool glows=mesh.subMeshCount>1;
                FbDraw(mesh,glows,world,inner,0,0,null);FbDraw(mesh,glows,mirror*world,inner,0,1,null);
            }
        }
        private void FallenPropDraw(in FallenFxProp p,Vector3 origin,Matrix4x4 mirror,float glow)
        {
            if(!fbFree.TryGetValue(p.Id,out var part))return;var mesh=part.Mesh[0];if(mesh==null)return;bool glows=mesh.subMeshCount>1;var world=Matrix4x4.TRS(origin+NativeBossPoseApply.ToUnity(p.At),NativeBossPoseApply.ToUnity(p.Turn),Vector3.one*p.Scale);
            FbDraw(mesh,glows,world,p.Glow*glow,0,0,null);FbDraw(mesh,glows,mirror*world,p.Glow*glow,0,1,null);
        }
        private Vector3 FallenHandPoint(Vector3 local)=>fbHandR.TransformPoint(local);
        private void FallenBodyHide(){if(fbRoot!=null&&fbRoot.activeSelf)fbRoot.SetActive(false);}
        private void FallenBodyShow(){if(fbRoot!=null&&!fbRoot.activeSelf)fbRoot.SetActive(true);}
        private void FallenBodyDispose()
        {
            fbHandler?.Dispose();fbHandler=null;if(fbRoot!=null)Destroy(fbRoot);fbRoot=null;fbBuilt=false;fbFailed=false;fbBone.Clear();fbBind.Clear();fbGroupIndex.Clear();
            foreach(var p in fbParts)foreach(var m in p.Mesh)if(m!=null)CapturedAnimation.Release(m);fbParts.Clear();foreach(var p in fbFree.Values)foreach(var m in p.Mesh)if(m!=null)CapturedAnimation.Release(m);fbFree.Clear();if(fbBody!=null)CapturedAnimation.Release(fbBody);fbBody=null;
        }
    }
}
