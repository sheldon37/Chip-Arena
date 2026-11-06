using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using NV=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // NOCTIEL in the game (docs/EXPEDITION-BOSS-FINAL.md). Presentation only: the DEV preview's script draws it (DEV panel · Camera · "Final boss preview", on any map; it plays on a stage of its own far below the level), and nothing is read back. Astra's record replaces
    // the script when the rules exist.
    //  - The body: Runtime NativeFallenBossBody.cs. The stage: black water (Resources/NativeFallenWater.shader), the drifting feathers (NativeFallenMotes), the effects (Core NativeFallenBossFx through NativeFallenFx), the Slash (Core NativeFallenSlash through NativeFallenSlashImage).
    //  - The song: Core NativeFallenSong, its measure Resources/FallenBossMusic.json. What the floor reacts to is the file itself.
    // While the preview is off nothing here does anything, and the level is exactly as it is.
    public sealed partial class ArenaPresentation
    {
        private static readonly Vector3 FallenOrigin=new Vector3(0,-2000,0);
        private readonly FallenBossFrame fbFrame=new FallenBossFrame();private readonly NativeFallenBossFx fbFx=new NativeFallenBossFx();private readonly NativeFallenSong fbSong=new NativeFallenSong();private bool fbSongRead,fbStageOn;private Texture2D fbSongTex;
        private Material fbWater,fbFxMat,fbFxMirror,fbMotes;private Mesh fbPlane,fbFxMesh,fbMoteMesh;private float fbLast=-1;
        private Vector3 fbEye,fbLook;private float fbFov=45;private bool fbShotReady;
        private readonly Vector4[] fbRingV=new Vector4[NativeFallenBossFx.MaxRings],fbWaveV=new Vector4[NativeFallenBossFx.MaxWaves],fbPoolV=new Vector4[NativeFallenBossFx.MaxPools];
        private readonly List<Vector3> fbFxP=new List<Vector3>();private readonly List<Vector4> fbFxU=new List<Vector4>();private readonly List<Vector2> fbFxT=new List<Vector2>();private readonly List<int> fbFxI=new List<int>();
        private NativeFallenSlashImage fbSlash;private Material fbSlashMat;private float fbSlashLast=-1;private byte fbSlashKindLast=255;
        // What the level's look was before the stage took the eye.
        private CameraClearFlags fbHoldClear;private Color fbHoldBack,fbHoldAmbient,fbHoldSky,fbHoldEquator,fbHoldGround;private float fbHoldFar;private bool fbHoldFog;private AmbientMode fbHoldMode;private Camera fbHoldCam;
        [Serializable] private sealed class FbSongData{public string title;public float rate;public float[] level,bass,slams;}
        private static readonly int FbClockId=Shader.PropertyToID("_FbClock"),FbStageId=Shader.PropertyToID("_FbStage"),FbKeyId=Shader.PropertyToID("_FbKey"),FbRingId=Shader.PropertyToID("_FbRing"),FbRingCountId=Shader.PropertyToID("_FbRingCount"),FbWaveId=Shader.PropertyToID("_FbWave"),FbWaveCountId=Shader.PropertyToID("_FbWaveCount"),FbPoolId=Shader.PropertyToID("_FbPool"),FbPoolCountId=Shader.PropertyToID("_FbPoolCount"),FbBossId=Shader.PropertyToID("_FbBoss"),FbSongId=Shader.PropertyToID("_FbSong"),FbSongAtId=Shader.PropertyToID("_FbSongAt"),FbDimId=Shader.PropertyToID("_FbDim"),FbStrikeId=Shader.PropertyToID("_FbStrike"),FbMoteCountId=Shader.PropertyToID("_FbMoteCount"),FbMoteLightId=Shader.PropertyToID("_FbMoteLight");

        private void FallenSongRead()
        {
            if(fbSongRead)return;fbSongRead=true;var a=Resources.Load<TextAsset>("FallenBossMusic");if(a==null){Debug.LogWarning("NOCTIEL: no song measure; the floor has only its footsteps");return;}
            try
            {
                var d=JsonUtility.FromJson<FbSongData>(a.text);fbSong.Level=d.level;fbSong.Bass=d.bass;fbSong.Slams=d.slams;fbSong.Rate=d.rate;
                int n=d.level.Length;fbSongTex=new Texture2D(n,1,TextureFormat.RGBA32,false,true){name="NOCTIEL song",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
                var px=new Color32[n];for(int i=0;i<n;i++)px[i]=new Color32((byte)(Mathf.Clamp01(d.level[i])*255),(byte)(Mathf.Clamp01(d.bass[i])*255),0,255);fbSongTex.SetPixels32(px);fbSongTex.Apply(false,true);
            }
            catch(Exception e){Debug.LogWarning("NOCTIEL: song measure: "+e.Message);}
        }
        private bool FallenStageBuild()
        {
            if(fbWater!=null)return true;Material Make(string n){var sh=Resources.Load<Shader>(n);return sh!=null&&sh.isSupported?new Material(sh){name=n,hideFlags=HideFlags.HideAndDontSave}:null;}
            fbWater=Make("NativeFallenWater");fbFxMat=Make("NativeFallenFx");fbFxMirror=Make("NativeFallenFx");fbMotes=Make("NativeFallenMotes");if(fbWater==null||fbFxMat==null||fbMotes==null){Debug.LogWarning("NOCTIEL: a stage shader is missing");return false;}
            fbFxMirror.renderQueue=2965;
            fbPlane=new Mesh{name="Still Water",hideFlags=HideFlags.HideAndDontSave};const float R=260;fbPlane.vertices=new[]{new Vector3(-R,0,-R),new Vector3(-R,0,R),new Vector3(R,0,R),new Vector3(R,0,-R)};fbPlane.triangles=new[]{0,1,2,0,2,3};fbPlane.bounds=new Bounds(Vector3.zero,new Vector3(R*2,1,R*2));
            fbFxMesh=new Mesh{name="NOCTIEL effects",hideFlags=HideFlags.HideAndDontSave};fbFxMesh.indexFormat=IndexFormat.UInt32;fbFxMesh.MarkDynamic();
            const int motes=1000;var mv=new Vector3[motes*4];var mu=new List<Vector4>(motes*4);var mi=new int[motes*6];
            for(int i=0;i<motes;i++)
            {
                float seed=(i*.6180339f)%1;for(int k=0;k<4;k++)mu.Add(new Vector4(k==1||k==2?1:-1,k>=2?1:-1,seed,i));int o=i*6,v=i*4;mi[o]=v;mi[o+1]=v+1;mi[o+2]=v+2;mi[o+3]=v;mi[o+4]=v+2;mi[o+5]=v+3;
            }
            fbMoteMesh=new Mesh{name="NOCTIEL feathers",hideFlags=HideFlags.HideAndDontSave};fbMoteMesh.vertices=mv;fbMoteMesh.SetUVs(0,mu);fbMoteMesh.triangles=mi;fbMoteMesh.bounds=new Bounds(Vector3.zero,Vector3.one*100000);
            return true;
        }
        // The DEV capture can start part way in: a number in Temp/fallen-preview-skip.txt.
        private static float FallenPreviewSkip()
        {
#if UNITY_EDITOR
            try{const string f="Temp/fallen-preview-skip.txt";if(System.IO.File.Exists(f)&&float.TryParse(System.IO.File.ReadAllText(f).Trim(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float v))return v;}catch(System.IO.IOException){}
#endif
            return 0;
        }
        private Light fbFill;
        private void FallenStageEnter(Camera eye)
        {
            if(fbFill==null){var go=new GameObject("NOCTIEL's stage light"){hideFlags=HideFlags.DontSave};fbFill=go.AddComponent<Light>();fbFill.type=LightType.Directional;fbFill.color=Color.white;fbFill.intensity=1.15f;fbFill.shadows=LightShadows.None;go.transform.rotation=Quaternion.Euler(46,28,0);}
            if(!fbFill.enabled)fbFill.enabled=true;
            if(fbStageOn&&fbHoldCam==eye)return;if(fbStageOn)FallenStageLeave();fbStageOn=true;fbHoldCam=eye;fbHoldClear=eye.clearFlags;fbHoldBack=eye.backgroundColor;fbHoldFar=eye.farClipPlane;fbHoldFog=RenderSettings.fog;fbHoldMode=RenderSettings.ambientMode;fbHoldAmbient=RenderSettings.ambientLight;fbHoldSky=RenderSettings.ambientSkyColor;fbHoldEquator=RenderSettings.ambientEquatorColor;fbHoldGround=RenderSettings.ambientGroundColor;
        }
        private void FallenStageLeave()
        {
            if(fbFill!=null&&fbFill.enabled)fbFill.enabled=false;if(!fbStageOn)return;fbStageOn=false;if(fbHoldCam!=null){fbHoldCam.clearFlags=fbHoldClear;fbHoldCam.backgroundColor=fbHoldBack;fbHoldCam.farClipPlane=fbHoldFar;}RenderSettings.fog=fbHoldFog;RenderSettings.ambientMode=fbHoldMode;RenderSettings.ambientLight=fbHoldAmbient;RenderSettings.ambientSkyColor=fbHoldSky;RenderSettings.ambientEquatorColor=fbHoldEquator;RenderSettings.ambientGroundColor=fbHoldGround;fbHoldCam=null;
        }
        private void FallenBossHide()
        {
            if(fbLast<0&&!fbStageOn)return;fbLast=-1;FallenCueReset();fbShotReady=false;fbFrame.Live=null;FallenStageLeave();FallenBodyHide();FallenPilotsHide();if(fbSlash!=null&&fbSlash.enabled)fbSlash.enabled=false;fbTrailCount=0;
        }
        // ---- the frame ----
        private void FallenBossScenery(NativeArena arena)
        {
            bool live=arena?.ZoneBoss?.fallen!=null;if(!NativeFallenBossPreview.On&&!live){FallenBossHide();return;}
            var eye=WorldEye;if(arena==null||eye==null||!FallenBodyBuild()||!FallenStageBuild()){FallenBossHide();return;}
            FallenSongRead();FallenBodyShow();float now=PresentationClock(arena);
            if(double.IsNaN(NativeFallenBossPreview.Start)){NativeFallenBossPreview.Start=now-FallenPreviewSkip();FbStepsClear();}
            #if UNITY_EDITOR
            if(fbLast<0){try{const string rf="Temp/fallen-preview-rate.txt";if(System.IO.File.Exists(rf)&&double.TryParse(System.IO.File.ReadAllText(rf).Trim(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double rv)&&rv>0)NativeFallenBossPreview.Rate=rv;}catch(System.IO.IOException){}}
#endif
            double since=(now-NativeFallenBossPreview.Start-FallenPreviewSkip())*NativeFallenBossPreview.Rate+FallenPreviewSkip(),loops=Math.Floor(since/NativeFallenBossPreview.Loop);float p=(float)(since-loops*NativeFallenBossPreview.Loop);
            float dt=fbLast<0?0:Mathf.Clamp(now-fbLast,0,.1f);fbLast=now;NativeFallenBossPreview.ShownClock=NativeFallenBossPreview.Start+since;NativeFallenBossPreview.ShownP=p;
            var f=fbFrame;if(live){FallenBossViewOf(arena,f);FallenLiveMotion(arena,f,now);}else{fbLive.Reset();fbLiveNow=false;f.Live=null;f.Actions=null;f.LiveAdds=null;NativeFallenBossPreview.Eval(p,VisualMotionBank,f);}NativeFallenBossPreview.Shown.P=p;NativeFallenBossPreview.Shown.Section=f.Section;NativeFallenBossPreview.Shown.SongClip=f.SongClip;NativeFallenBossPreview.Shown.Phase=f.Phase;NativeFallenBossPreview.Shown.ChipAge=f.ChipAge;NativeFallenBossPreview.Shown.ChipPhase=f.ChipPhase;NativeFallenBossPreview.Shown.Black=f.Black;NativeFallenBossPreview.Shown.SlashAge=f.SlashAge;NativeFallenBossPreview.Shown.CardAge=f.CardAge;NativeFallenBossPreview.Shown.Kind=f.Kind;
            // The eye: on the black stage, nothing of the level reaches it.
            FallenStageEnter(eye);eye.clearFlags=CameraClearFlags.SolidColor;eye.backgroundColor=Color.black;eye.farClipPlane=460;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.62f,.62f,.66f);RenderSettings.ambientEquatorColor=new Color(.5f,.5f,.54f);RenderSettings.ambientGroundColor=new Color(.34f,.34f,.38f);
            var origin=live?new Vector3(0,.02f,0):FallenOrigin;var eyeAt=eye.transform.position;
            FallenPose(f,now,dt,origin,eyeAt);FallenShown(f);FallenCues(arena,f,now,live,p);
            // The shot, read by the camera next (BackroomsBossPreviewShot).
            if(!live||f.Kind==NativeFallenBossPreview.KindCutscene||f.Kind==NativeFallenBossPreview.KindDeath)FallenShot(f,origin,now);else fbShotReady=false;
            // The song on the floor.
            float clip=f.SongClip,level=fbSong.LevelAt(clip),bass=fbSong.BassAt(clip);
            // The trail and the steps come from the posed body.
            var t=eye.transform;var nr=t.right;var nu=t.up;fbFx.Build(f,ToNV(eyeAt-origin),ToNV(nr),ToNV(nu),fbSong,WeaponEffectsQuality<.5f);
            bool shown=f.BossShown&&f.Kind!=NativeFallenBossPreview.KindChips;
            if(shown){FallenFeet(f,now,dt,origin);FallenTrail(f,now,origin);}FbStepsInto(fbFx,now);
            if(live)FallenPilotsHide();else FallenPilots(f,now,origin);
            // The light on the plate: the shot's, driven by the song, the fight's moves.
            var keyDir=(Quaternion.AngleAxis(f.Yaw+24,Vector3.up)*new Vector3(.45f,.72f,.6f)).normalized;float key=Mathf.Max(.02f,f.Key+fbFx.KeyBoost)*(1+.1f*bass*bass);if(f.Phase==4)key*=.62f;Shader.SetGlobalVector(FbKeyId,new Vector4(keyDir.x,keyDir.y,keyDir.z,key));
            float inner=f.Inner*(1.5f+.75f*bass*bass);float flash=fbFx.White*2.4f;
            var mirror=Matrix4x4.Translate(new Vector3(0,origin.y*2,0))*Matrix4x4.Scale(new Vector3(1,-1,1));FallenBossDraws=0;
            if(shown)FallenBodyDraw(f,origin,eyeAt,inner,flash,mirror);else if(f.Kind==NativeFallenBossPreview.KindDeath)FallenSwordAlone(f,origin,2.4f,mirror);
            if(f.FeatherShown)FallenPropDraw(new FallenFxProp{Id="feather-one",At=f.Feather,Turn=System.Numerics.Quaternion.CreateFromYawPitchRoll(f.FeatherTurn*Mathf.Deg2Rad,.5f*Mathf.Sin(p*1.1f),.35f*Mathf.Sin(p*.8f)),Scale=1.6f,Glow=1.5f},origin,mirror,1);
            for(int i=0;i<fbFx.PropCount;i++)FallenPropDraw(fbFx.Props[i],origin,mirror,1);
            FallenWaterDraw(f,origin,clip,level,bass,now);
            FallenFxDraw(origin,mirror,now);
            FallenMotesDraw(origin,level,now);
            FallenSlashStep(f,eye);
        }
        private static NV ToNV(Vector3 v)=>new NV(v.x,v.y,v.z);
        private void FallenShot(FallenBossFrame f,Vector3 origin,float now)
        {
            Vector3 Mark(int m,Vector3 v)
            {
                switch(m)
                {
                    case FallenBossFrame.MarkHead:return fbHead.position+v;case FallenBossFrame.MarkHand:return fbHandR.position+v;case FallenBossFrame.MarkChest:return fbSpine.position+v;case FallenBossFrame.MarkTip:return FallenHandPoint(FbTipLocal)+v;
                    default:return origin+v;
                }
            }
            var e=Mark(f.EyeMark,NativeBossPoseApply.ToUnity(f.Eye));var l=Mark(f.LookMark,NativeBossPoseApply.ToUnity(f.Look));
            float sh=fbFx.Shake;if(sh>.002f)e+=new Vector3(Mathf.Sin(now*37.1f)+.5f*Mathf.Sin(now*71.3f+1.3f),Mathf.Sin(now*43.7f+.7f)+.5f*Mathf.Sin(now*83.9f+2.9f),Mathf.Sin(now*29.3f))*(.14f*sh);
            fbEye=e;fbLook=l;fbFov=f.Fov;fbShotReady=true;
        }
        internal bool FallenBossPreviewShot(out Vector3 eye,out Vector3 look,out float fov){eye=fbEye;look=fbLook;fov=fbFov;return (NativeFallenBossPreview.On||fbFrame.Live!=null)&&fbShotReady;}
        internal bool FallenBossPreviewCard(out BossIntroCard card,out float age)
        {
            card=null;age=-1;if((!NativeFallenBossPreview.On&&fbFrame.Live==null)||!fbShotReady||fbFrame.CardAge<0||fbFrame.CardAge>NativeBackroomsBossIntro.Hold-NativeBackroomsBossIntro.NameAt)return false;
            card=NativeFallenBoss.Card;age=NativeBackroomsBossIntro.NameAt-.05f+fbFrame.CardAge;return true;
        }
        // ---- the water and the air ----
        private void FallenWaterDraw(FallenBossFrame f,Vector3 origin,float clip,float level,float bass,float now)
        {
            var fx=fbFx;for(int i=0;i<fx.RingCount;i++){int o=i*4;fbRingV[i]=new Vector4(fx.Rings[o],fx.Rings[o+1],fx.Rings[o+2],fx.Rings[o+3]);}for(int i=0;i<fx.WaveCount;i++){int o=i*4;fbWaveV[i]=new Vector4(fx.Waves[o],fx.Waves[o+1],fx.Waves[o+2],fx.Waves[o+3]);}for(int i=0;i<fx.PoolCount;i++){int o=i*4;fbPoolV[i]=new Vector4(fx.Pools[o],fx.Pools[o+1],fx.Pools[o+2],fx.Pools[o+3]);}
            var w=fbWater;w.SetVectorArray(FbRingId,fbRingV);w.SetVectorArray(FbWaveId,fbWaveV);w.SetVectorArray(FbPoolId,fbPoolV);w.SetFloat(FbRingCountId,fx.RingCount);w.SetFloat(FbWaveCountId,fx.WaveCount);w.SetFloat(FbPoolCountId,fx.PoolCount);
            w.SetVector(FbBossId,new Vector4(f.Boss.X,f.Boss.Z,0,0));w.SetVector(FbStageId,origin);w.SetFloat(FbClockId,now%1000);w.SetFloat(FbDimId,fx.Dim);w.SetFloat(FbStrikeId,fx.Strike);if(fbSongTex!=null){w.SetTexture(FbSongId,fbSongTex);w.SetVector(FbSongAtId,new Vector4(clip,1f/NativeFallenSong.End,0,0));}
            Graphics.DrawMesh(fbPlane,Matrix4x4.Translate(origin),w,0,null,0,null,ShadowCastingMode.Off,false);FallenBossDraws++;
        }
        private void FallenMotesDraw(Vector3 origin,float level,float now)
        {
            var m=fbMotes;m.SetFloat(FbClockId,now%10000);m.SetFloat(FbMoteCountId,fbFx.Motes);m.SetFloat(FbMoteLightId,(.32f+.7f*level*level)*fbFx.Dim);m.SetVector(FbStageId,origin);Graphics.DrawMesh(fbMoteMesh,Matrix4x4.identity,m,0,null,0,null,ShadowCastingMode.Off,false);FallenBossDraws++;
        }
        private void FallenFxDraw(Vector3 origin,Matrix4x4 mirror,float now)
        {
            var q=fbFx.Quads;fbFxP.Clear();fbFxU.Clear();fbFxT.Clear();fbFxI.Clear();
            for(int i=0;i<q.Count;i++)
            {
                int o=i*12,v=fbFxP.Count;float mode=q.Modes[i*2],s=q.Modes[i*2+1],g=q.Tint[i*2],a=q.Tint[i*2+1];
                for(int k=0;k<4;k++){fbFxP.Add(new Vector3(q.Corners[o+k*3],q.Corners[o+k*3+1],q.Corners[o+k*3+2]));fbFxU.Add(new Vector4(k==1||k==2?1:0,k>=2?1:-1,mode,s));fbFxT.Add(new Vector2(g,a));}
                fbFxI.Add(v);fbFxI.Add(v+1);fbFxI.Add(v+2);fbFxI.Add(v);fbFxI.Add(v+2);fbFxI.Add(v+3);
            }
            var mesh=fbFxMesh;mesh.Clear(true);if(q.Count==0)return;mesh.SetVertices(fbFxP);mesh.SetUVs(0,fbFxU);mesh.SetUVs(1,fbFxT);mesh.SetTriangles(fbFxI,0,false);mesh.bounds=new Bounds(Vector3.zero,Vector3.one*1000);
            fbFxMat.SetFloat(FbClockId,now%1000);fbFxMirror.SetFloat(FbClockId,now%1000);var at=Matrix4x4.Translate(origin);
            Graphics.DrawMesh(mesh,at,fbFxMat,0,null,0,null,ShadowCastingMode.Off,false);Graphics.DrawMesh(mesh,mirror*at,fbFxMirror,0,null,0,null,ShadowCastingMode.Off,false);FallenBossDraws+=2;
        }
        // ---- what the posed body does to the water: each footfall a ring, the sword's point a wake, and the sword's trail ----
        private struct FbStep{public float X,Z,At,Strength;}
        private readonly FbStep[] fbSteps=new FbStep[16];private int fbStepAt;private bool fbFootDownL,fbFootDownR;private float fbWakeLast;private Vector3 fbWakeAt;
        private void FbStepsClear(){Array.Clear(fbSteps,0,fbSteps.Length);fbStepAt=0;}
        private void FbStepAdd(Vector3 at,Vector3 origin,float now,float strength){fbSteps[fbStepAt]=new FbStep{X=at.x-origin.x,Z=at.z-origin.z,At=now,Strength=strength};fbStepAt=(fbStepAt+1)%fbSteps.Length;}
        private void FbStepsInto(NativeFallenBossFx fx,float now){for(int i=0;i<fbSteps.Length;i++){var s=fbSteps[i];if(s.Strength>0&&now-s.At<2.6f)fx.Step(s.X,s.Z,now-s.At,s.Strength);}}
        private void FallenFeet(FallenBossFrame f,float now,float dt,Vector3 origin)
        {
            float s=NativeFallenBoss.Scale,floor=origin.y;bool downL=fbFootL.position.y-.11f*s<floor+.06f,downR=fbFootR.position.y-.11f*s<floor+.06f;
            if(downL&&!fbFootDownL){FbStepAdd(fbFootL.position,origin,now,.75f);FallenStepCue(fbFootL.position,now);}if(downR&&!fbFootDownR){FbStepAdd(fbFootR.position,origin,now,.75f);FallenStepCue(fbFootR.position,now);}fbFootDownL=downL;fbFootDownR=downR;
            // The point in the water lays a wake: a ring each 0.6 m it travels (a drag at a walk, a swing through the water), none while
            // it rests; a fast swing's rings are the brighter.
            var tip=FallenHandPoint(FbTipLocal);float moved=(tip-fbWakeAt).magnitude;
            if(tip.y<floor+.25f&&f.Plant<.5f&&now-fbWakeLast>.07f&&moved>=.6f){float fast=moved/Mathf.Max(.016f,now-fbWakeLast);FbStepAdd(tip,origin,now,fast>4?.9f:.5f);fbWakeLast=now;fbWakeAt=tip;}
        }
        private const int FbTrailLength=26;private const float FbTrailLife=.34f;
        private readonly Vector3[] fbTrailTip=new Vector3[FbTrailLength],fbTrailHilt=new Vector3[FbTrailLength];private readonly float[] fbTrailAt=new float[FbTrailLength];private int fbTrailHead,fbTrailCount;
        private void FallenTrail(FallenBossFrame f,float now,Vector3 origin)
        {
            var tip=FallenHandPoint(FbTipLocal)-origin;var hilt=Vector3.Lerp(tip,FallenHandPoint(FbHiltLocal)-origin,.62f);
            if(fbTrailCount>0&&fbTrailAt[fbTrailHead]>now){fbTrailCount=0;}
            fbTrailHead=(fbTrailHead+1)%FbTrailLength;fbTrailTip[fbTrailHead]=tip;fbTrailHilt[fbTrailHead]=hilt;fbTrailAt[fbTrailHead]=now;if(fbTrailCount<FbTrailLength)fbTrailCount++;
            // Only what moves fast draws. A smooth ribbon through the samples, its age in its u.
            int n=fbTrailCount;if(n<3||f.Plant>.5f)return;var q=fbFx.Quads;float grey=f.Phase==3?.2f:1;float strength=f.Phase==4?2.8f:2f;
            Vector3 Cr(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t){float t2=t*t,t3=t2*t;return .5f*(2*b+(c-a)*t+(2*a-5*b+4*c-d)*t2+(3*b-a-3*c+d)*t3);}
            int At(int k)=>(fbTrailHead-Mathf.Clamp(k,0,n-1)+FbTrailLength*2)%FbTrailLength;
            const int sub=3;for(int k=0;k<n-1;k++)
            {
                float age=now-fbTrailAt[At(k)];if(age>FbTrailLife)break;var t0=fbTrailTip[At(k-1)];var t1=fbTrailTip[At(k)];var t2=fbTrailTip[At(k+1)];var t3=fbTrailTip[At(k+2)];var h0=fbTrailHilt[At(k-1)];var h1=fbTrailHilt[At(k)];var h2=fbTrailHilt[At(k+1)];var h3=fbTrailHilt[At(k+2)];
                float span=fbTrailAt[At(k)]-fbTrailAt[At(k+1)];float speed=span>0?(t1-t2).magnitude/span:0;float k0=Mathf.Clamp01((speed-7)/18)*f.Trail;if(k0<=0)continue;
                for(int j=0;j<sub;j++)
                {
                    float u0=j/(float)sub,u1=(j+1f)/sub;var a=Cr(t0,t1,t2,t3,u0);var b=Cr(t0,t1,t2,t3,u1);var c=Cr(h0,h1,h2,h3,u1);var d=Cr(h0,h1,h2,h3,u0);
                    float age0=(age+u0*span)/FbTrailLife,age1=(age+u1*span)/FbTrailLife;FbTrailQuad(q,a,b,c,d,age0,age1,k0,strength,grey);
                }
            }
        }
        // A ribbon quad's age runs along it (the shared layout gives u 0..1 across a quad, so each carries its own mean age in alpha).
        private static void FbTrailQuad(FallenFxQuads q,Vector3 tipA,Vector3 tipB,Vector3 hiltB,Vector3 hiltA,float age0,float age1,float k,float strength,float grey)
        {
            float mean=(age0+age1)*.5f;float a=k*(1-mean)*(1-mean);if(a<=.004f)return;var n=(tipA-hiltA);
            q.Add(new NV(hiltA.x,hiltA.y,hiltA.z),new NV(hiltB.x,hiltB.y,hiltB.z),new NV(tipB.x,tipB.y,tipB.z),new NV(tipA.x,tipA.y,tipA.z),a,strength,FallenFxQuads.Trail,grey);
        }
        // ---- stand-in pilots: the model's own body, in its own colours, running on the water (and in it) ----
        private sealed class FbPilot{public GameObject Body,Reflection;public HumanPoseHandler Handler,MirrorHandler;public HumanPose Pose;public float[] Mix;}
        private readonly FbPilot[] fbPilots=new FbPilot[NativeFallenPilots.Count];
        private FbPilot FallenPilotBuild()
        {
            var a=Instantiate(visualBodyPrefab);a.name="NOCTIEL stand-in";a.hideFlags=HideFlags.DontSave;a.SetActive(true);var b=Instantiate(visualBodyPrefab);b.name="NOCTIEL stand-in (water)";b.hideFlags=HideFlags.DontSave;b.SetActive(true);
            var ska=a.transform.Find("VisualSkeleton");var skb=b.transform.Find("VisualSkeleton");var aa=ska.GetComponent<Animator>();var ab=skb.GetComponent<Animator>();aa.runtimeAnimatorController=null;aa.enabled=false;ab.runtimeAnimatorController=null;ab.enabled=false;
            int m=VisualMotionBank.Muscles;var p=new FbPilot{Body=a,Reflection=b,Handler=new HumanPoseHandler(aa.avatar,ska),MirrorHandler=new HumanPoseHandler(ab.avatar,skb),Mix=new float[NativeVisualMotionClips.Head+m]};p.Pose.muscles=new float[m];return p;
        }
        private void FallenPilots(FallenBossFrame f,float now,Vector3 origin)
        {
            bool fight=f.Kind==NativeFallenBossPreview.KindChips?false:true;var bank=VisualMotionBank;
            for(int i=0;i<fbPilots.Length;i++)
            {
                bool shown=fight&&NativeFallenPilots.Shown(i,f.P)&&f.Kind!=NativeFallenBossPreview.KindDeath;var q=fbPilots[i];
                if(!shown){if(q!=null){if(q.Body.activeSelf)q.Body.SetActive(false);if(q.Reflection.activeSelf)q.Reflection.SetActive(false);}continue;}
                if(q==null){q=FallenPilotBuild();fbPilots[i]=q;}if(!q.Body.activeSelf)q.Body.SetActive(true);if(!q.Reflection.activeSelf)q.Reflection.SetActive(true);
                Array.Clear(q.Mix,0,q.Mix.Length);string name=NativeFallenPilots.Clip(f.P);if(bank.Clips.TryGetValue(name,out var c))NativeVisualMotionClips.Add(c,now*(f.P<NativeFallenPilots.CutsceneEnd?.75f:1.1f)+i*.27f,1,q.Mix);NativeVisualMotionClips.Finish(q.Mix,1);
                q.Pose.bodyPosition=new Vector3(q.Mix[0],q.Mix[1],q.Mix[2]);q.Pose.bodyRotation=new Quaternion(q.Mix[3],q.Mix[4],q.Mix[5],q.Mix[6]);Array.Copy(q.Mix,NativeVisualMotionClips.Head,q.Pose.muscles,0,q.Pose.muscles.Length);
                var at=origin+NativeBossPoseApply.ToUnity(NativeFallenPilots.At(i,f.P));var turn=Quaternion.AngleAxis(NativeFallenPilots.Yaw(i,f.P),Vector3.up);q.Body.transform.SetPositionAndRotation(at,turn);q.Handler.SetHumanPose(ref q.Pose);
                // Its reflection: the same body turned over the water.
                var low=q.Body.transform.position;q.Reflection.transform.SetPositionAndRotation(new Vector3(low.x,origin.y*2-low.y,low.z),turn);q.Reflection.transform.localScale=new Vector3(1,-1,1);q.MirrorHandler.SetHumanPose(ref q.Pose);
            }
        }
        private void FallenPilotsHide(){foreach(var q in fbPilots)if(q!=null){if(q.Body!=null&&q.Body.activeSelf)q.Body.SetActive(false);if(q.Reflection!=null&&q.Reflection.activeSelf)q.Reflection.SetActive(false);}}
        // ---- the Slash ----
        private void FallenSlashStep(FallenBossFrame f,Camera eye)
        {
            if(f.SlashAge<0){if(fbSlash!=null&&fbSlash.enabled)fbSlash.enabled=false;fbSlashLast=-1;fbSlashKindLast=255;return;}
            if(fbSlashMat==null){var sh=Resources.Load<Shader>("NativeFallenSlash");if(sh==null||!sh.isSupported)return;fbSlashMat=new Material(sh){name="The Slash",hideFlags=HideFlags.HideAndDontSave};}
            if(fbSlash==null||fbSlash.gameObject!=eye.gameObject){fbSlash=eye.GetComponent<NativeFallenSlashImage>();if(fbSlash==null)fbSlash=eye.gameObject.AddComponent<NativeFallenSlashImage>();fbSlash.Material=fbSlashMat;}
            if(f.SlashAge<fbSlashLast-.05f||f.SlashKind!=fbSlashKindLast)fbSlash.Arm(f.SlashKind);fbSlashLast=f.SlashAge;fbSlashKindLast=f.SlashKind;
            fbSlash.State=NativeFallenSlash.Eval(f.SlashKind,f.SlashAge);fbSlash.Angle=f.SlashAngle;fbSlash.Seed=f.Section*.37f;if(!fbSlash.enabled)fbSlash.enabled=true;
        }
        private void DisposeFallenBoss()
        {
            FallenStageLeave();FallenBodyDispose();if(fbFill!=null)Destroy(fbFill.gameObject);fbFill=null;foreach(var p in new Material[]{fbWater,fbFxMat,fbFxMirror,fbMotes,fbSlashMat})if(p!=null)CapturedAnimation.Release(p);fbWater=fbFxMat=fbFxMirror=fbMotes=fbSlashMat=null;foreach(var m in new[]{fbPlane,fbFxMesh,fbMoteMesh})if(m!=null)CapturedAnimation.Release(m);fbPlane=fbFxMesh=fbMoteMesh=null;
            if(fbSongTex!=null)CapturedAnimation.Release(fbSongTex);fbSongTex=null;fbSongRead=false;for(int i=0;i<fbPilots.Length;i++){var q=fbPilots[i];if(q==null)continue;q.Handler?.Dispose();q.MirrorHandler?.Dispose();if(q.Body!=null)Destroy(q.Body);if(q.Reflection!=null)Destroy(q.Reflection);fbPilots[i]=null;}
        }
    }
    public sealed partial class ConversionGame
    {
        private void FallenBossPreviewToggle(){NativeFallenBossPreview.On=!NativeFallenBossPreview.On;NativeFallenBossPreview.Start=double.NaN;DevSay(NativeFallenBossPreview.On?"Final boss preview · on (NOCTIEL's cutscene, four phases with the Slash and chip screens, the death, on the full song; presentation only)":"Final boss preview · off",false);}
        private float FallenBossPreviewPanel(float x,float y,float w){DevToggleAct(new Rect(x,y,w,32),"preview-fallen-boss",NativeFallenBossPreview.On);y+=38;return DevNote(x,y,w,"On any map: NOCTIEL, the final boss: its cutscene, four phases with the Slash and the chip screens, every move and add, and its death, on the whole of Lux Cecidit, on the black water. Presentation only: nothing hurts or reaches the network until Astra's rules exist.",C.Muted());}
        private static readonly ExpeditionBossModel fallenBanner=new ExpeditionBossModel();private const string FallenPreviewLabel="DEV PREVIEW · NOCTIEL · NOT GAMEPLAY";
        private void FallenBossPreviewHud(float width,float height)
        {
            if(!NativeFallenBossPreview.On||Event.current.type!=EventType.Repaint)return;var f=NativeFallenBossPreview.Shown;
            if(f.Black>0&&f.ChipAge<0)U.Fill(new Rect(0,0,width,height),new Color(0,0,0,f.Black));
            if(f.ChipAge>=0){if(f.SlashAge<0)U.Fill(new Rect(0,0,width,height),Color.black);if(f.SlashAge<0||f.SlashAge<.05f)FallenChipScreen(width,height,f.ChipAge,f.ChipPhase);return;}
            if(f.Kind==NativeFallenBossPreview.KindFight)
            {
                var size=U.Measure(FallenPreviewLabel,11,true,true,.12f);var r=new Rect(width/2-size.x/2-14,92,size.x+28,24);U.Fill(r,new Color(0,0,0,.7f));H.Outline(r,new Color(1,1,1,.6f),0,H.Corners.None);U.Text(r,FallenPreviewLabel,11,Color.white,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
                float left=(NativeFallenBossPreview.Sections[f.Section].To-f.P)/(NativeFallenBossPreview.Sections[f.Section].To-NativeFallenBossPreview.Sections[f.Section].From);int phase=f.Phase-1;float from=NativeFallenBoss.PhaseFrom[phase],to=phase+1<NativeFallenBoss.Phases?NativeFallenBoss.PhaseFrom[phase+1]:0;
                var m=fallenBanner;m.Name=NativeFallenBoss.Name;m.Area=NativeFallenBoss.PhaseNames[phase];m.PlateCount=1;m.PlateNames[0]=m.Name;m.PlateDown[0]=false;m.PlateHealth[0]=to+(from-to)*left;m.PlatePercent[0]=null;m.PlateAge[0]=4;NativeExpeditionStatesHud.BossPlates(m,width,128,Time.unscaledTime,false);
            }
        }
        // The boss shows its hand: the phase's name, its three chips turning over on the beat, black and white; then the party's own draft, in colour, to answer them.
        private void FallenChipScreen(float W,float H_,float age,int phase)
        {
            float B=NativeFallenSong.Beat,total=NativeFallenBossPreview.ChipSeconds,exit=M.Progress(age,total-.5f,.4f);float a=1-exit;
            // The name.
            float e=M.EaseOut(M.Progress(age,.15f,.7f));string name=NativeFallenBoss.PhaseNames[phase-1];
            U.Text(new Rect(0,H_*.045f+10*(1-e),W,18),"PHASE "+phase+" / "+NativeFallenBoss.Phases,14,new Color(1,1,1,.65f*e*a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.42f);
            U.Text(new Rect(0,H_*.08f+16*(1-e),W,H_*.13f),name,Mathf.Clamp(W*.062f,48,104),new Color(1,1,1,e*a),TextAnchor.MiddleCenter,bold:true,tracking:.16f,glow:new Color(1,1,1,.5f*e*a));
            // A hairline where the cut was.
            float rule=M.EaseOut(M.Progress(age,.3f,.8f));U.Fill(new Rect(W/2-W*.44f*rule,H_*.22f,W*.88f*rule,1),new Color(1,1,1,.4f*a));
            float cw=W*.255f,ch=H_*.245f,gap=W*.018f,x0=W/2-(cw*3+gap*2)/2,y1=H_*.285f,y2=H_*.635f;
            U.Text(new Rect(x0,y1-24,cw*3+gap*2,18),"NOCTIEL SHOWS ITS HAND",13,new Color(1,1,1,.75f*M.Progress(age,.7f,.5f)*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);
            int first=(phase-2)*3;
            for(int i=0;i<3;i++)
            {
                var liveChips=arena?.ZoneBoss?.fallen?.chipOffer;var chip=liveChips?.Length==3?Array.Find(NativeFallenBoss.Chips,c=>c.Id==liveChips[i]):NativeFallenBoss.Chips[(first+i)%NativeFallenBoss.Chips.Length];float at=1.2f+i*2*B;float t=M.Progress(age,at,.42f);float flip=Mathf.Cos(t*Mathf.PI);bool face=t>=.5f;
                float appear=M.EaseOut(M.Progress(age,.5f+i*.09f,.5f));var r=new Rect(x0+i*(cw+gap),y1+18*(1-appear),cw,ch);U.Push(r.center,Vector2.zero,Mathf.Abs(flip),0,1);
                U.Fill(r,new Color(.015f,.015f,.015f,appear*a));H.Outline(r,new Color(1,1,1,(face?.95f:.3f)*appear*a),0,H.Corners.None);
                if(!face)U.Text(r,"?",ch*.42f,new Color(1,1,1,.25f*appear*a),TextAnchor.MiddleCenter,bold:true);
                else
                {
                    U.Fill(new Rect(r.x,r.y,r.width,4),new Color(1,1,1,a));U.Text(new Rect(r.x+18,r.y+ch*.14f,r.width-36,ch*.3f),chip.Name,ch*.2f,new Color(1,1,1,a),TextAnchor.UpperLeft,bold:true,tracking:.05f,glow:new Color(1,1,1,.3f*a));
                    U.Fill(new Rect(r.x+18,r.y+ch*.44f,r.width-36,1),new Color(1,1,1,.3f*a));U.Text(new Rect(r.x+18,r.y+ch*.5f,r.width-36,ch*.44f),chip.Line,ch*.14f,new Color(1,1,1,.85f*a),TextAnchor.UpperLeft,wrap:true);
                }
                U.Pop();
            }
            // The party's answer: the normal draft's place (colour is the party's).
            float dt=M.EaseOut(M.Progress(age,1.2f+6*B+.5f,.6f));var tone=new Color(.35f,.85f,1f);
            U.Text(new Rect(x0,y2-24,cw*3+gap*2,18),"ANSWER IT · DRAFT ONE",13,new Color(tone.r,tone.g,tone.b,.9f*dt*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);
            for(int i=0;i<3;i++){var r=new Rect(x0+i*(cw+gap),y2+20*(1-dt),cw,ch);U.Fill(r,new Color(.02f,.06f,.09f,dt*a));H.Outline(r,new Color(tone.r,tone.g,tone.b,.8f*dt*a),0,H.Corners.None);U.Text(r,"THE NORMAL CHIP DRAFT",ch*.1f,new Color(tone.r,tone.g,tone.b,.75f*dt*a),TextAnchor.MiddleCenter,bold:true,wrap:true,tracking:.1f);}
            // The time, draining to the drop.
            float bar=1-Mathf.Clamp01(age/total);U.Fill(new Rect(W*.06f,H_*.935f,W*.88f,1),new Color(1,1,1,.2f*a));U.Fill(new Rect(W/2-W*.44f*bar,H_*.935f-1,W*.88f*bar,3),new Color(1,1,1,.85f*a));
        }
    }
}
