using System;
using System.Collections.Generic;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The local pilot's damage screen (rules: Core/NativeDamageScreen.cs; shader: Resources/NativeDamageScreen.shader).
    // Once per frame, after the live view is placed (ConversionGame.LateUpdate -> DamageScreen), the authoritative events
    // aimed at the local pilot feed the rules, every bloom and arc is re-aimed at its attacker, and NativeDamageScreenPass
    // runs the one full-screen pass as the last screen camera's image effect, before the IMGUI HUD (so the HUD, crosshair,
    // hit marker and numbers stay crisp on top). Visual only: it reads events, HP, pilot and minion positions and the camera.
    // Shown only in the live local view of the fight (and the result while still alive); hidden and cleared in menus and
    // the pause menu, preview arenas, every killcam replay (the viewer's own too), intros, the draft and chip screens, the
    // Siren reveal and the PvE / Last Laugh cutscenes. While the local pilot is dead it takes no new events and fades out
    // (spectating shows nothing). Settings · Damage screen effect: Off / Subtle (x.6, no chromatic split; the threat ring
    // stays at full strength) / Full; Reduced effects stills the heartbeat, the noise drift, the snap-in and the shock motion.
    public sealed partial class ConversionGame
    {
        private readonly NativeDamageScreen damageScreen=new NativeDamageScreen();
        private readonly int[] damageKeys=new int[NativeDamageScreen.Slots+NativeDamageScreen.Arcs];
        private int damageEvent,damageGeneration=-1;private float damageTime=-1;private NativeDamageScreen.Lag damageLag;
        private void DamageScreen()
        {
            var local=arena?.Local;
            bool show=local!=null&&local.Body!=null&&cameraView!=null&&!menu&&previewArena==null&&!ReplayActive&&!IntroActive&&!PvePickScreen&&!SirenRevealActive&&!cinematic.Directing
                &&arena.Pve?.Holding!=true&&(match.Phase==NativePhase.Fight||match.Phase==NativePhase.Result)&&prefs.damageScreen!="Off";
            if(!show){if(damageGeneration>=0){damageScreen.Clear();damageGeneration=-1;}NativeDamageScreenPass.Frame(null,false);return;}
            // Envelopes run on the unscaled frame clock (smooth on guests, whose match clock moves in network steps); the match
            // clock only decides which events are fresh and catches rewinds. An empty list (events are pruned after 1 s, and
            // guests copy that list) is a lull: everything keeps running, as the camera shake treats it.
            float clock=arena.Time,now=Time.unscaledTime;var events=arena.Events;int newest=0;
            for(int i=0;i<events.Count;i++)newest=Math.Max(newest,events[i].Id);
            if(NativeDamageScreen.Rewound(arena.Generation,damageGeneration,clock,damageTime,newest,damageEvent))
            {
                // A view that just opened starts after the events already in the list: it never replays old hits.
                damageScreen.Clear();damageGeneration=arena.Generation;damageEvent=newest;
            }
            damageTime=clock;
            int me=local.Seat.id;float max=Mathf.Max(1,(float)local.Stats["hp"]);bool living=local.Hp>0;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=damageEvent)continue;
                if(e.Target!=me||clock-e.Time>NativeDamageScreen.Fresh||!(e.Amount>0)||float.IsInfinity(e.Amount))continue;
                float fraction=e.Amount/max;
                switch(e.Kind)
                {
                    case EventKind.Hit:case EventKind.Headshot:
                        if(!NativeEventFeedback.SourceHit(e.Kind,e.Amount))break;
                        DamageSource(e,local,out int key,out var from,out float weight,out bool melee);
                        damageScreen.Hit(now,fraction,e.Kind==EventKind.Headshot,melee,key,NativeArena.Vec(from),weight);break;
                    case EventKind.BurnTick:case EventKind.PoisonTick:
                        if(NativeEventFeedback.SourceHit(e.Kind,e.Amount))damageScreen.Tick(now,fraction,e.Kind==EventKind.BurnTick);break;
                    case EventKind.Heal:damageScreen.Healed(now,fraction);break;
                }
            }
            damageEvent=Math.Max(damageEvent,newest);
            // Live re-aim: every attacker a bloom or arc follows, from where the local pilot stands now.
            Vector3 at=local.Body.transform.position;int keys=damageScreen.Keys(damageKeys);
            for(int k=0;k<keys;k++)if(DamageAttacker(damageKeys[k],out var to))damageScreen.Aim(damageKeys[k],NativeArena.Vec(to-at));
            var view=cameraView.transform;bool subtle=prefs.damageScreen=="Subtle";
            damageScreen.Step(now,Time.unscaledDeltaTime,local.Hp/max,living,prefs.reducedEffects,!prefs.reducedEffects&&!subtle,NativeArena.Vec(view.right),NativeArena.Vec(view.up),NativeArena.Vec(view.forward));
            NativeDamageScreenPass.Frame(damageScreen,damageScreen.Active,(float)Screen.width/Mathf.Max(1,Screen.height),Screen.height,presentation.FirstPerson,subtle?.6f:1,prefs.reducedEffects);
        }
        // Where a hit came from (the event names the attacker's seat, never a position): the attacking pilot's body; a
        // summoner's non-direct hit, from its nearest live minion within 3.5 m; a direct hit with no source (a PvE add strikes
        // in melee), from the nearest live add within 3.5 m; a lava or spike floor, from below (.8, no bearing); anything
        // else (self damage, vents, a gone source) has no direction. key: the attacker the slots follow (minions offset by
        // 1 << 24). If Hit/Headshot events ever carry the attacker or blast origin in ArenaEvent.From, it can stand in for
        // the minion search here.
        private void DamageSource(in ArenaEvent e,NativePilot local,out int key,out Vector3 from,out float weight,out bool melee)
        {
            key=0;from=Vector3.zero;weight=0;melee=false;Vector3 at=local.Body.transform.position;bool direct=e.Detail=="direct";
            if(e.Source>0)
            {
                NativePilot source=null;var pilots=arena.Pilots;for(int i=0;i<pilots.Count;i++)if(pilots[i].Seat.id==e.Source){source=pilots[i];break;}
                if(source!=null&&source!=local&&source.Body!=null)
                {
                    Vector3 pos=source.Body.transform.position;key=e.Source;
                    if(!direct){var minion=NearestMinion(at,source);if(minion!=null){pos=minion.Body.transform.position;key=MinionKey(minion.Id);}}
                    melee=direct&&source.Weapon?.@class=="melee";from=pos-at;weight=1;return;
                }
                return;
            }
            if(direct){var add=NearestMinion(at,null);if(add!=null){key=MinionKey(add.Id);from=add.Body.transform.position-at;weight=1;melee=true;}return;}
            if(e.Detail=="stage-lava"||e.Detail=="stage-spikes"){from=Vector3.down;weight=.8f;}
        }
        private static int MinionKey(int id)=>(1<<24)|(id&((1<<24)-1));
        // The nearest live minion within 3.5 m: `owner`'s, or with no owner a PvE add.
        private NativeMinion NearestMinion(Vector3 at,NativePilot owner)
        {
            NativeMinion best=null;float range=3.5f*3.5f;var minions=arena.Minions;
            for(int i=0;i<minions.Count;i++)
            {
                var m=minions[i];if(m==null||m.Hp<=0||m.Body==null||(owner!=null?m.Owner!=owner:m.Pve==null))continue;
                float d=(m.Body.transform.position-at).sqrMagnitude;if(d<range){range=d;best=m;}
            }
            return best;
        }
        // Where an attacker the damage screen follows is now; a dead or gone one is not found and keeps its last direction.
        private bool DamageAttacker(int key,out Vector3 position)
        {
            position=Vector3.zero;
            if((key&(1<<24))!=0)
            {
                int id=key&((1<<24)-1);var minions=arena.Minions;
                for(int i=0;i<minions.Count;i++){var m=minions[i];if(m!=null&&m.Id==id&&m.Hp>0&&m.Body!=null){position=m.Body.transform.position;return true;}}
                return false;
            }
            var pilots=arena.Pilots;
            for(int i=0;i<pilots.Count;i++){var p=pilots[i];if(p.Seat.id==key&&p.Hp>0&&p.Body!=null){position=p.Body.transform.position;return true;}}
            return false;
        }
        // HealthBox: the HP bar's damage trail (the overhead HealthLag rule, NativeDamageScreen.LagStep): the lost part stays
        // shown for .22 s after the last loss, then drains at .9 of max HP per second; the HUD draws it as the segmented bar's
        // ghost. Off with the damage screen (the trail then equals the HP).
        private float DamageTrail(NativePilot p,float hp)
        {
            if(arena==null||p==null||prefs.damageScreen=="Off")return hp;
            float lag=NativeDamageScreen.LagStep(ref damageLag,p.Seat.id,arena.Time,hp);
            return lag>hp+.002f?lag:hp;
        }
    }

    // The damage screen's full-screen pass. While the rules are active, NativeDamageScreenImage is enabled on the last
    // camera that draws to the screen (the highest enabled depth, as NativeUiBlur picks it: the first-person view-model
    // camera while it is on, else the match camera); its OnRenderImage draws that camera's finished image through
    // NativeDamageScreen.shader with Graphics.Blit, Unity's single-source image effect path, which orients the source
    // itself on every graphics API. (The first version copied BuiltinRenderTextureType.CameraTarget into a temporary from a
    // command buffer and blitted it back: on D3D a copy of the back buffer or anti-aliased target lands top-down without a
    // negative texel height, a known Unity behaviour it leaves as is, so the write-back turned the whole frame upside down
    // while the effect was up: one candidate for the owner's Windows D3D11 report "camera seems to flip when taking damage";
    // the other, a knockdown or stagger switching the view mode in ConversionGame.UpdateCamera, is not touched here.) The
    // view-model camera clears depth only: Unity renders a camera with an enabled image effect into an intermediate target
    // that starts from what the cameras before it drew, the same composite these cameras already rely on when they render
    // through their HDR intermediates (the view model over the world), so the source is the whole stacked frame (the shader
    // clamps it to 0..1, as the old 8-bit copy did). Order: the globals are written here, in LateUpdate, before any camera
    // renders; the effect runs at the end of that camera's render, before its AfterEverything command buffers
    // (NativeUiBlur's copy: panel backdrops show the tinted frame) and before OnGUI (the HUD, crosshair, hit marker and
    // numbers stay crisp on top). The component is added on demand and disabled whenever the effect is inactive or hidden
    // (Unity then skips it: no camera is forced into an intermediate target and an idle frame costs nothing); it moves when
    // the last camera changes. The 256x256 noise texture is built once with the material. Globals are written from
    // preallocated arrays: steady frames allocate nothing. Without the shader nothing is drawn. Release destroys the
    // components; Release(true) also drops the material and the noise (ConversionGame.OnDestroy).
    public static class NativeDamageScreenPass
    {
        private static NativeDamageScreenImage image;private static Camera attached;private static Material material;private static Texture2D noise;private static bool missing;
        private static readonly List<NativeDamageScreenImage> added=new List<NativeDamageScreenImage>();
        private static Camera[] cameras=new Camera[8];
        private static readonly Vector4[] a=new Vector4[NativeDamageScreen.Slots],b=new Vector4[NativeDamageScreen.Slots],arcA=new Vector4[NativeDamageScreen.Arcs],arcB=new Vector4[NativeDamageScreen.Arcs],arcC=new Vector4[NativeDamageScreen.Arcs];
        private static readonly int idA=Shader.PropertyToID("_DmgA"),idB=Shader.PropertyToID("_DmgB"),idG=Shader.PropertyToID("_DmgG"),idL=Shader.PropertyToID("_DmgL"),idE=Shader.PropertyToID("_DmgE"),
            idK=Shader.PropertyToID("_DmgK"),idView=Shader.PropertyToID("_DmgView"),idArcA=Shader.PropertyToID("_DmgArcA"),idArcB=Shader.PropertyToID("_DmgArcB"),idArcC=Shader.PropertyToID("_DmgArcC"),
            idRing=Shader.PropertyToID("_DmgRing"),idFx=Shader.PropertyToID("_DmgFx"),idNoise=Shader.PropertyToID("_DmgNoise");
        // aspect: width / height; height: screen pixels; firstPerson: the guard and the ring follow the view (third person: a
        // guard .06 lower and 1.18x taller, the ring .19 H instead of .145 H); intensity: Subtle .6.
        public static void Frame(NativeDamageScreen s,bool active,float aspect=1,float height=1080,bool firstPerson=true,float intensity=1,bool reduced=false)
        {
            if(!active||s==null){Detach();return;}
            if(material==null)
            {
                if(missing)return;var shader=Resources.Load<Shader>("NativeDamageScreen");if(shader==null||!shader.isSupported){missing=true;return;}
                material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                // The baked noise (NativeDamageScreen.NoiseTexels): linear data, tiling, bilinear, no mips.
                noise=new Texture2D(NativeDamageScreen.NoiseSize,NativeDamageScreen.NoiseSize,TextureFormat.RGBA32,false,true){name="Native damage screen noise",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
                noise.LoadRawTextureData(NativeDamageScreen.NoiseTexels());noise.Apply(false,true);material.SetTexture(idNoise,noise);
            }
            var camera=LastScreenCamera();if(camera==null){Detach();return;}
            if(attached!=camera||image==null){Detach();image=Image(camera);attached=camera;}
            if(!image.enabled)image.enabled=true;
            for(int i=0;i<NativeDamageScreen.Slots;i++){int o=i*4;a[i]=new Vector4(s.A[o],s.A[o+1],s.A[o+2],s.A[o+3]);b[i]=new Vector4(s.B[o],s.B[o+1],s.B[o+2],s.B[o+3]);}
            for(int i=0;i<NativeDamageScreen.Arcs;i++){int o=i*4;arcA[i]=new Vector4(s.ArcA[o],s.ArcA[o+1],s.ArcA[o+2],s.ArcA[o+3]);arcB[i]=new Vector4(s.ArcB[o],s.ArcB[o+1],s.ArcB[o+2],s.ArcB[o+3]);arcC[i]=new Vector4(s.ArcC[o],s.ArcC[o+1],s.ArcC[o+2],s.ArcC[o+3]);}
            Shader.SetGlobalVectorArray(idA,a);Shader.SetGlobalVectorArray(idB,b);Shader.SetGlobalVectorArray(idArcA,arcA);Shader.SetGlobalVectorArray(idArcB,arcB);Shader.SetGlobalVectorArray(idArcC,arcC);
            Shader.SetGlobalVector(idG,new Vector4(s.Flash,s.Chroma,s.Desat,s.Dip));Shader.SetGlobalVector(idL,new Vector4(s.Low,s.Death,s.Pulse,s.Clock));
            Shader.SetGlobalVector(idE,new Vector4(s.Fire,s.Poison,s.Heal,s.HealWave));Shader.SetGlobalVector(idK,new Vector4(s.FireTick,s.PoisonTick,s.HeadFlash,reduced?1:0));
            Shader.SetGlobalVector(idView,new Vector4(aspect,firstPerson?0:-.06f,firstPerson?1:1.18f,intensity));
            Shader.SetGlobalVector(idRing,new Vector4(firstPerson?NativeDamageScreen.RingFirst:NativeDamageScreen.RingThird,Mathf.Max(1,height),s.Track,firstPerson?0:1));
            Shader.SetGlobalVector(idFx,new Vector4(s.ShockAge,s.ShockAngle,s.MarkAge,s.RingFade));
        }
        // The camera Unity renders last into the back buffer (as NativeUiBlur picks it): the highest enabled depth.
        private static Camera LastScreenCamera()
        {
            if(cameras.Length<Camera.allCamerasCount)cameras=new Camera[Mathf.NextPowerOfTwo(Camera.allCamerasCount)];
            int n=Camera.GetAllCameras(cameras);Camera best=null;
            for(int i=0;i<n;i++){var c=cameras[i];cameras[i]=null;if(c==null||c.targetTexture!=null||!c.isActiveAndEnabled)continue;if(best==null||c.depth>=best.depth)best=c;}
            return best;
        }
        // The camera's image effect: the one already on it, or a new one (remembered for Release; destroyed cameras' entries
        // are pruned first). AddComponent leaves it enabled.
        private static NativeDamageScreenImage Image(Camera camera)
        {
            var c=camera.GetComponent<NativeDamageScreenImage>();
            if(c==null){for(int i=added.Count-1;i>=0;i--)if(added[i]==null)added.RemoveAt(i);c=camera.gameObject.AddComponent<NativeDamageScreenImage>();added.Add(c);}
            c.Material=material;return c;
        }
        private static void Drop(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
        private static void Detach(){if(image!=null)image.enabled=false;image=null;attached=null;}
        public static void Release(bool all)
        {
            Detach();for(int i=0;i<added.Count;i++)Drop(added[i]);added.Clear();
            if(all){Drop(material);Drop(noise);material=null;noise=null;missing=false;}
        }
    }
}
