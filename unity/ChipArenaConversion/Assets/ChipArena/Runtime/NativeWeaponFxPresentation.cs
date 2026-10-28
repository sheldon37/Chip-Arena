using System;
using System.Collections.Generic;
using UnityEngine;
using NVector3=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeSourceModels
    {
        private Material afterimageMaterial;private bool afterimageFailed;
        /// <summary>
        /// A model drawn as the roll afterimage (Resources/NativeWeaponGhost.shader: an additive fresnel ghost; colour and opacity
        /// in the body tint), sharing the source meshes; null when the model or the shader is missing.
        /// </summary>
        public string Afterimage(string model)
        {
            if(string.IsNullOrEmpty(model))return null;string id="afterimage-"+model;if(models.ContainsKey(id))return id;if(!models.TryGetValue(model,out var source))return null;
            if(afterimageMaterial==null)
            {
                if(afterimageFailed)return null;var shader=Resources.Load<Shader>("NativeWeaponGhost");if(shader==null||!shader.isSupported){afterimageFailed=true;return null;}
                afterimageMaterial=new Material(shader){enableInstancing=true,name="Native roll afterimage"};materials.Add(afterimageMaterial);
            }
            var parts=new Part[source.Length];for(int i=0;i<parts.Length;i++)parts[i]=new Part{Name=source[i].Name,Bone=source[i].Bone,Mesh=source[i].Mesh,Material=afterimageMaterial,NoShadow=true};
            models[id]=parts;return id;
        }
    }

    // The weapons milestone's arena hooks (Core/NativeWeaponFx.cs for the rules, NativeWeaponFxDraw.cs for the geometry): muzzle
    // flashes and their lights (WeaponDressing; Core/NativeMuzzleFlash.cs and NativeMuzzleLight.cs), slash crescents and roll afterimages (after the rigs are posed, RenderMeleeTrails), the hit flash
    // (SourceCharacter), the spell charge streaks and rings (BodyDressing), and the tracers, energy trails and impacts
    // (RenderGlow, which submits the drawer). Everything reads the replicated actors, shots, events and seat stats every peer
    // holds, on the presentation clocks the other effects use; hit timing, hitboxes, damage, cooldowns, movement, animation
    // timing and the network frame are untouched.
    public sealed partial class ArenaPresentation
    {
        // A strike's crescent: the swing by the rules (its window, arc and look), the blades it is swept from (Core/NativeSlashTrail:
        // the right hand's, and the left dagger's) and how far the blade runs behind the replicated clock (its hit-stop).
        private sealed class SlashState
        {
            public readonly NativeMeleeSwing Swing=new NativeMeleeSwing();public string Spec;public float Range,Lag;public int Color;public Vector3 At;public int Id;
            public readonly NativeSlashTrail Right=new NativeSlashTrail(),Left=new NativeSlashTrail();
        }
        private static long SlashKey(Actor a)=>(long)a.Id<<32|(uint)Mathf.RoundToInt(a.AttackAt*4096);
        private readonly NVector3[] slashInner=new NVector3[NativeSlashTrail.Capacity],slashOuter=new NVector3[NativeSlashTrail.Capacity];private readonly float[] slashShare=new float[NativeSlashTrail.Capacity];
        private sealed class Emitter
        {
            public Vector3 Origin,Last;public float LastAt,Carry;public long Emitted;public int Frame;public Color Halo;public float Radius;public long Seed;
            public readonly Vector3[] Points=new Vector3[NativeTrailFx.RibbonSamples];public readonly float[] Times=new float[NativeTrailFx.RibbonSamples];public int Count;
            public void Push(Vector3 at,float time){if(Count==Points.Length){Array.Copy(Points,1,Points,0,Count-1);Array.Copy(Times,1,Times,0,Count-1);Count--;}Points[Count]=at;Times[Count]=time;Count++;}
        }
        // Tile: a pack trail sprite's tile of the magic sheet (-1: the soft glow sprite), with its school, roll and spin; its Color
        // is then the shot's halo (NativeMagicPackPresentation.cs).
        private struct TrailSprite{public Vector3 At,Drift;public float Born,Life,Size,Roll,Spin;public Color Color;public int Tile;public NativeMagicSchool School;}
        private struct ImpactMark{public Vector3 At,Normal;public float Born;public long Seed;public Color Tint;}
        private NativeWeaponFxDraw weaponFx;private Snapshot weaponSnapshot;private int weaponFrame;
        private readonly Dictionary<long,SlashState> slashes=new Dictionary<long,SlashState>();private readonly Stack<SlashState> spareSlashes=new Stack<SlashState>();private readonly List<long> staleSlashes=new List<long>();
        private readonly Dictionary<int,(float At,bool Head)> hitFlashes=new Dictionary<int,(float,bool)>();private int hitFlashEvent;
        private readonly NativeAfterimages afterimages=new NativeAfterimages();private GhostPose[] afterimagePoses;private readonly Color[] afterimageTints=new Color[NativeAfterimages.Slots];
        private readonly VisualEcho[] afterimageEchoes=new VisualEcho[NativeAfterimages.Slots];private readonly bool[] afterimageModel=new bool[NativeAfterimages.Slots];
        private readonly string[] afterimageWeapon=new string[NativeAfterimages.Slots],afterimageBody=new string[NativeAfterimages.Slots];private readonly Dictionary<string,string> afterimageModels=new Dictionary<string,string>();
        private readonly Dictionary<long,Emitter> emitters=new Dictionary<long,Emitter>();private readonly Stack<Emitter> spareEmitters=new Stack<Emitter>();private readonly List<long> staleEmitters=new List<long>();
        private TrailSprite[] trailSprites=new TrailSprite[512];private int trailSpriteCount;
        private readonly ImpactMark[] impactMarks=new ImpactMark[32];private int impactNext,impactEvent;private NativeArena impactArena;private int impactGeneration=-1;
        private NativeArena emitterArena;private int emitterGeneration=-1;private float emitterClock=-1;
        private NativeWeaponFxDraw WeaponFxDraw=>weaponFx??=sourceModels!=null?WithSlashBrush(new NativeWeaponFxDraw()):null;
        // The slash crescents' ink brush from the bake (the visual body's prefab, VisualSlashes/Brush: its material's texture, the
        // streaks' rectangle in its local position and scale), when the owner's effect pack is baked; else the shape alone.
        private static NativeWeaponFxDraw WithSlashBrush(NativeWeaponFxDraw draw)
        {
            var prefab=Resources.Load<GameObject>(VisualBodyResource);var brush=prefab!=null?prefab.transform.Find("VisualSlashes/Brush"):null;
            var r=brush!=null?brush.GetComponent<Renderer>():null;var tex=r!=null&&r.sharedMaterial!=null?r.sharedMaterial.mainTexture:null;
            if(tex!=null)draw.SlashBrush(tex,new Vector4(brush.localPosition.x,brush.localPosition.y,brush.localScale.x,brush.localScale.y));
            return draw;
        }

        /// <summary>Called first in Render: an empty drawer queue for the frame and this frame's hit events for the flash.</summary>
        private void BeginWeaponFx(Snapshot s)
        {
            weaponFx?.Begin();muzzleLights.Begin();weaponSnapshot=s;weaponFrame++;if(s?.Events==null)return;
            int newest=0;foreach(var e in s.Events)newest=Math.Max(newest,e.Id);if(newest<hitFlashEvent){hitFlashEvent=0;hitFlashes.Clear();}
            foreach(var e in s.Events)
            {
                if(e.Id<=hitFlashEvent)continue;
                if((e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot)&&e.Target>0&&NativeEventFeedback.SourceHit(e.Kind,e.Amount)&&e.Detail!="burn"&&e.Detail!="poison")
                {if(!hitFlashes.TryGetValue(e.Target,out var h)||e.Time>=h.At)hitFlashes[e.Target]=(e.Time,e.Kind==EventKind.Headshot);}
            }
            hitFlashEvent=Math.Max(hitFlashEvent,newest);
        }
        /// <summary>The struck pilot's flash code for its body parts this frame (1: none). Third-person rigs only.</summary>
        private float HitFlashCode(int id,float time)=>hitFlashes.TryGetValue(id,out var h)?NativeHitFlash.Code(time-h.At,h.Head,WeaponEffectsQuality):1;

        // The guns' muzzle lights this frame (Core NativeMuzzleLight): every gun rig's flash offers its shooter's light, the
        // nearest the budget allows get the four slots (WeaponFx), and NativeMuzzleLights uploads them for the lit shaders.
        private readonly NativeMuzzleLightPicker muzzleLights=new NativeMuzzleLightPicker();private readonly float[] muzzleShots=new float[NativeMuzzleLight.History];
        /// <summary>
        /// One gun rig's muzzle flash (Core/NativeMuzzleFx, owner 2026-10-20) at its drawn crown (the skin's muzzle face) in the
        /// right hand's barrel frame, on the rig's camera (first person: the view-model camera, sized with the view weapon), and
        /// its light. The rounds come from the replicated Shot events of the frame and Actor.LastShot (NativeMuzzleLight.Shots):
        /// the newest flashes, the older ones of a burst keep smoking, and the shooter offers one merged light. The flash takes
        /// the seat's skin, its chip modules (vents a module covers stay shut, multishot pods flash), the Q kit the round was
        /// fired under and the model's detail tier; it dims with the rig's block cooldown, as the source flash did.
        /// </summary>
        private void MuzzleFx(CapturedAnimation.Player rig,Actor a,float time,bool first)
        {
            var draw=WeaponFxDraw;if(draw==null||!NativeWeaponDressing.Gun(a.SourceSpec))return;
            int n=NativeMuzzleLight.Shots(weaponSnapshot?.Events,a.Id,a.LastShot,time,muzzleShots);if(n==0)return;
            float shot=muzzleShots[0];double age=time-shot;if(!(age>=0&&age<NativeMuzzleFx.Span))return;
            var hand=rig.Bones[CapturedRig.RHand].localToWorldMatrix;Vector3 forward=hand.MultiplyVector(Vector3.forward);float scale=forward.magnitude;if(!(scale>1e-4f))return;
            Vector3 z=forward/scale,x=hand.MultiplyVector(Vector3.right).normalized,y=Vector3.Cross(z,x).normalized;
            int skin=WeaponSkin(a);ChipKinds chips=default;var sockets=skin==NativeWeaponSkins.StandardIndex?MuzzleChips(a,out chips):null;
            float distance=0;var eye=WorldEye;if(!first&&eye!=null&&rig.Root!=null)distance=Vector3.Distance(eye.transform.position,rig.Root.transform.position);
            var kit=NativeMuzzleFx.KitOf(a,shot,time,out float heat);
            if(!NativeMuzzleFx.Look(a.SourceSpec,skin,chips,sockets,kit,NativeWeaponSkins.Tier(LowGraphics,WeaponEffectsQuality,distance,first),heat,out var look))return;
            Vector3 crown=hand.MultiplyPoint3x4(new Vector3(0,0,look.Crown));
            NativeWeaponFxShapes.Muzzle(draw,first,crown,x,y,z,scale,look,NativeWeaponFx.Seed(shot,a.Id),age,WeaponEffectsQuality,LowGraphics,rigFactor,first?ViewCamera:null);
            for(int i=1;i<n&&i<=NativeMuzzleFx.MaxWisps;i++){double older=time-muzzleShots[i];if(older<NativeMuzzleFx.WispLife+.1)NativeWeaponFxShapes.Wisps(draw,first,crown,x,y,z,scale,look,NativeWeaponFx.Seed(muzzleShots[i],a.Id),older,WeaponEffectsQuality,LowGraphics,rigFactor);}
            if(NativeMuzzleLight.Look(a.SourceSpec,kit,out var light)){var at=crown+z*(NativeMuzzleLight.Lead*scale);muzzleLights.Offer(a.Id,new NVector3(at.x,at.y,at.z),light,NativeMuzzleLight.Level(light,muzzleShots,n,time,a.Id,KitFlash),first);}
        }

        /// <summary>
        /// The crown of a drawn gun this frame (its 'muzzle' socket), for the rounds it fires (Core/NativeShotLaunch, through the
        /// shared shot tracker). A first-person view weapon draws on the view-model camera, which shares the match camera's
        /// place but not always its field of view (the sprint kick): its crown is carried to where the match camera shows the
        /// same screen point at the same depth, so the round leaves the barrel the player sees.
        /// </summary>
        private void MuzzleRecord(CapturedAnimation.Player rig,Actor a,bool first)
        {
            if(!NativeWeaponDressing.Gun(a.SourceSpec)||a.WeaponAway)return;var hand=rig.Bones[CapturedRig.RHand].localToWorldMatrix;
            Vector3 muzzle=hand.MultiplyPoint3x4(new Vector3(0,0,NativeWeaponDressing.MuzzleZ(a.SourceSpec)));
            if(first&&ViewCamera!=null)
            {
                var world=ViewCamera.transform.parent!=null?ViewCamera.transform.parent.GetComponent<Camera>():null;
                if(world!=null&&Mathf.Abs(world.fieldOfView-ViewCamera.fieldOfView)>.01f)
                {
                    float k=Mathf.Tan(world.fieldOfView*.5f*Mathf.Deg2Rad)/Mathf.Tan(Mathf.Max(1,ViewCamera.fieldOfView)*.5f*Mathf.Deg2Rad);
                    var local=ViewCamera.transform.InverseTransformPoint(muzzle);local.x*=k;local.y*=k;muzzle=ViewCamera.transform.TransformPoint(local);
                }
            }
            shotTracker.Muzzles[a.Id]=new NVector3(muzzle.x,muzzle.y,muzzle.z);
        }

        // Slash crescents and roll afterimages, after the rigs are posed (RenderMeleeTrails). Both follow the trails setting, as
        // the swept ribbon the crescents replace does.
        private void WeaponFxFrame(float time)
        {
            var s=weaponSnapshot;if(s?.Actors==null||sourceModels==null)return;var draw=WeaponFxDraw;if(draw==null)return;
            if(!TrailsEnabled){foreach(var st in slashes.Values)spareSlashes.Push(st);slashes.Clear();afterimages.Clear();return;}
            foreach(var a in s.Actors)
            {
                if(!NativeSlashFx.CanCapture(a,time))continue;
                long key=SlashKey(a);if(slashes.ContainsKey(key))continue;
                var state=spareSlashes.Count>0?spareSlashes.Pop():new SlashState();SlashBegin(state,a);slashes.Add(key,state);
            }
            staleSlashes.Clear();float q=WeaponEffectsQuality;
            foreach(var pair in slashes)
            {
                var st=pair.Value;if(time<st.Swing.HitAt-1||time>st.Swing.HitUntil+1+st.Lag||!NativeSlashFx.Look(st.Spec,out var look)){staleSlashes.Add(pair.Key);continue;}
                if(visualPositions.TryGetValue(st.Id,out var at))st.At=at;
                bool any=SweptSlash(draw,st,look,time,q);
                if(!any&&time-st.Lag>st.Swing.HitUntil+look.Fade)staleSlashes.Add(pair.Key);
            }
            foreach(long key in staleSlashes){spareSlashes.Push(slashes[key]);slashes.Remove(key);}
            Afterimages(s,time);
        }
        // The swing by the rules (NativeMeleeRules.Begin: arc, reach) from the replicated strike: its committed yaw, attack index
        // and hit window. Without the seat's stats (a pilot missing from the loadout arena) the weapon's own arc and range.
        private void SlashBegin(SlashState st,Actor a)
        {
            var pilot=LoadoutPilot(a.Id);var w=pilot?.Weapon;var sw=st.Swing;float yaw=a.CommitYaw*Mathf.Deg2Rad;int index=a.Combo+1;
            if(w!=null&&pilot.Stats!=null){var b=NativeMeleeRules.Begin(w,pilot.Stats,a.AttackAt,index,yaw);sw.Arc=b.Arc;st.Range=b.Range;}
            else{var cw=WeaponLoadout?.Content?.Weapon(a.SourceSpec);sw.Arc=cw!=null?(float)(a.SourceSpec=="sword"&&index%3==0?.8:cw.arc):1.6f;st.Range=cw!=null?(float)cw.range:2.5f;}
            sw.Air=a.AirAttack;sw.ImpactAt=a.ImpactAt;sw.SprintAttack=a.SprintAttack;
            sw.Spec=a.SourceSpec;sw.Index=index;sw.Yaw=yaw;sw.HitAt=a.SwingAt;sw.HitUntil=a.SwingUntil;sw.At=a.AttackAt;
            st.Spec=a.SourceSpec;st.Id=a.Id;st.At=Position(a);st.Color=NativeWeaponDressing.SweepColor(pilot?.Seat?.chips);st.Lag=0;st.Right.Reset();st.Left.Reset();
        }
        /// <summary>
        /// One posed blade of a standard strike into its crescent (SampleSweep, after the rig is posed: third person, and the
        /// local pilot's hidden body in first person, as the ribbons it replaces): while the blade is in its fast arc by the
        /// swing's own clock (MeleeMotion.CutAge), its grip and stretched tip socket; both daggers. Notes the hit-stop lag.
        /// </summary>
        private void SlashSample(CapturedAnimation.Player rig,Actor a,float time,float reach)
        {
            if(!TrailsEnabled||a.WeaponAway||!NativeSlashFx.Covers(a.SourceSpec)||a.AttackAt<-90)return;var motion=rig?.Melee;if(motion==null||motion.CutAt!=a.AttackAt)return;
            long key=SlashKey(a);
            if(!slashes.TryGetValue(key,out var st)){if(!NativeSlashFx.CanCapture(a,time))return;st=spareSlashes.Count>0?spareSlashes.Pop():new SlashState();SlashBegin(st,a);slashes.Add(key,st);}
            if(!float.IsNaN(motion.CutAge))st.Lag=Mathf.Max(0,time-a.AttackAt-motion.CutAge);
            if(!NativeSlashTrail.Sweeping(motion.CutAge,motion.CutWindup,motion.CutWindow)||!NativeWeaponDressing.BladeTip(a.SourceSpec,out var socket))return;
            var tip=CapturedAnimation.Vec(CapturedRig.Mirror(socket));bool daggers=a.SourceSpec=="daggers";
            for(int hand=0;hand<(daggers?2:1);hand++)
            {
                if(hand==0&&daggers&&a.MainWeaponAway)continue;
                var m=SourceWeaponBone(rig,a,time,false,hand==0?CapturedRig.RHand:CapturedRig.LHand,reach);
                (hand==0?st.Right:st.Left).Add(CapturedAnimation.Num(m.MultiplyPoint3x4(Vector3.zero)),CapturedAnimation.Num(m.MultiplyPoint3x4(tip)),time);
            }
        }
        /// <summary>
        /// A strike's crescent this frame, swept from its blade samples: its opacity from the rules' window and fade on the
        /// blade's clock (the replicated clock less the hit-stop lag; a daggers' sprint twin plays its two sweeps), the right
        /// blade's band and, for the daggers, the left's when it sweeps a third as far or more. False when nothing shows.
        /// </summary>
        private bool SweptSlash(NativeWeaponFxDraw draw,SlashState st,in SlashLook look,float time,float quality)
        {
            float clock=time-st.Lag,alpha=0;
            if(NativeSlashFx.Frame(st.Swing,look,clock,false,out _,out _,out float a0,out _))alpha=a0;
            if(st.Swing.SprintAttack&&NativeSprintAttackRules.Twin(st.Spec)&&NativeSlashFx.Frame(st.Swing,look,clock,true,out _,out _,out float a1,out _))alpha=Mathf.Max(alpha,a1);
            if(!(alpha>.002f))return false;var centre=new NVector3(st.At.x,st.At.y,st.At.z);float yaw=st.Swing.Yaw;
            float right=st.Right.Swept(centre,yaw),left=st.Spec=="daggers"?st.Left.Swept(centre,yaw):0,lead=Mathf.Max(right,left);bool any=false;
            if(right>=lead*.34f)any|=NativeWeaponFxShapes.Swept(draw,st.Right,centre,yaw,st.Color,look,alpha*(right>=left?1:.8f),quality,slashInner,slashOuter,slashShare);
            if(left>0&&left>=lead*.34f)any|=NativeWeaponFxShapes.Swept(draw,st.Left,centre,yaw,st.Color,look,alpha*(left>right?1:.8f),quality,slashInner,slashOuter,slashShare);
            return any;
        }
        // Roll afterimages: a snapshot of each rolling third-person rig every NativeAfterimages.Spacing metres, drawn for its life
        // in the pilot's accent colour (body, and the weapon it holds).
        private void Afterimages(Snapshot s,float time)
        {
            string pilot=afterimageModels.TryGetValue("pilot",out var id)?id:(afterimageModels["pilot"]=sourceModels.Afterimage("pilot"));if(pilot==null)return;
            if(afterimagePoses==null){afterimagePoses=new GhostPose[NativeAfterimages.Slots];for(int i=0;i<afterimagePoses.Length;i++)afterimagePoses[i]=new GhostPose();}
            float q=WeaponEffectsQuality;
            foreach(var a in s.Actors)
            {
                if(!NativeAfterimages.Eligible(a,time)||FirstPerson&&a.Id==LocalId||!rigs.TryGetValue(a.Id,out var rig))continue;
                var root=rig.Root.transform.position;var at=new NVector3(root.x,root.y,root.z);
                if(!afterimages.Due(a.Id,at,a.RollAt,time,q))continue;int slot=afterimages.Take(a.Id,at,a.RollAt,time,q);var pose=afterimagePoses[slot];
                pose.Root=rig.Root.transform.localToWorldMatrix;for(int b=0;b<22&&b<rig.Bones.Length;b++)pose.Bones[b]=rig.Bones[b].localToWorldMatrix;
                string weapon=null;if(!a.WeaponAway&&NativeWeaponDressing.SocketShown(a.SourceSpec)){string w=SkinnedHeld(a);if(!afterimageModels.TryGetValue(w,out weapon))afterimageModels[w]=weapon=sourceModels.Afterimage(w);}
                afterimageWeapon[slot]=weapon;pose.Armed=weapon!=null;afterimageBody[slot]=BodyAfterimage(BodySkin(a.Id),BodyTier(root,false))??pilot;
                if(pose.Armed){float reach=WeaponReach(rig,a,time);for(int b=0;b<22&&b<rig.Bones.Length;b++)pose.Weapon[b]=SourceWeaponBone(rig,a,time,false,b,reach);}
                afterimageTints[slot]=NativeWeaponFxShapes.Fx(NativeAfterimages.Color(a.AccentColor));
                // A pilot drawn as the model leaves the model's echo.
                afterimageModel[slot]=VisualEchoSnap(rig,ref afterimageEchoes[slot]);
            }
            for(int k=0;k<NativeAfterimages.Slots;k++)
            {
                if(!afterimages.Live(k,time))continue;float opacity=(float)NativeAfterimages.Fade(time-afterimages.At[k]);if(opacity<=.002f)continue;
                var pose=afterimagePoses[k];var tint=afterimageTints[k];
                if(afterimageModel[k]){VisualEchoDraw(afterimageEchoes[k],"NativeWeaponGhost",tint,opacity);continue;}
                sourceModels.Queue(afterimageBody[k]??pilot,pose.Root,pose.Bone,null,0,null,tint,null,opacity);
                if(pose.Armed)sourceModels.Queue(afterimageWeapon[k],pose.Root,pose.WeaponBone,null,0,null,tint,null,opacity);
            }
        }
        /// <summary>The charge orb's converging streaks and shrinking rings round the socket (the orb's own frame, so they follow
        /// its growth and the view model's size), in the school colour, on the rig's camera.</summary>
        private void ChargeFx(Matrix4x4 socket,Actor a,int color,float time,bool first)
        {
            var draw=WeaponFxDraw;if(draw==null||color<0)return;float scale=socket.MultiplyVector(Vector3.right).magnitude;if(!(scale>1e-4f))return;
            NativeWeaponFxShapes.Charge(draw,first,socket.MultiplyPoint3x4(Vector3.zero),scale,NativeWeaponFxShapes.Lin(color),a.Id*7919L+(long)Mathf.Round(a.AttackAt*64),time,BlastQuality,LowGraphics);
        }

        /// <summary>
        /// The tracers, energy trails and impacts on the field clock the shots are drawn at; then the drawer is submitted to the
        /// match camera and, in first person, the view-model camera. RenderGlow calls it on both of its paths.
        /// </summary>
        private void WeaponFx(NativeArena arena,Camera camera,float now)
        {
            // Rounds that hit a surface: its debris and a bullet hole (NativeSurfaceImpactPresentation.cs).
            SurfaceImpacts(arena,now);
            var draw=WeaponFxDraw;if(draw==null)return;draw.Low=LowGraphics;
            if(!ReferenceEquals(emitterArena,arena)||emitterGeneration!=arena.Generation||now<emitterClock-.25f){ClearEmitters();emitterArena=arena;emitterGeneration=arena.Generation;}
            emitterClock=Math.Max(emitterClock,now);
            float q=BlastQuality;bool low=LowGraphics,trails=TrailsEnabled;Vector3 eye=camera!=null?camera.transform.position:Vector3.zero;
            foreach(var b in arena.Bullets){NativeShotViews.Bullet(arena,b,shotTracker,now,out var v);if(!NativeBowWire.Is(v.Spec))ShotFx(draw,v,now,q,low,trails,eye,GunKitTracer(b,v));}
            foreach(var s in arena.AbilityShots){NativeShotViews.Ability(arena,s,shotTracker,now,out var v);ShotFx(draw,v,now,q,low,trails,eye);}
            staleEmitters.Clear();foreach(var pair in emitters)if(pair.Value.Frame!=weaponFrame)staleEmitters.Add(pair.Key);
            foreach(long key in staleEmitters){spareEmitters.Push(emitters[key]);emitters.Remove(key);}
            // The world-space trail sprites outlive their shot.
            for(int i=0;i<trailSpriteCount;)
            {
                var p=trailSprites[i];float age=now-p.Born;
                if(!(age<p.Life)||age<-.5f){trailSprites[i]=trailSprites[--trailSpriteCount];continue;}
                if(p.Tile>=0&&PackLooks)PackTrailSprite(p,age);else NativeWeaponFxShapes.Sprite(draw,p.At,p.Drift,p.Color,p.Size,p.Life,age);i++;
            }
            // The pistol / machine-gun Q kit (NativeGunKitPresentation.cs) on the pose clock, before the drawer is submitted.
            GunKitFx();BowFx(arena,now);
            ImpactFx(draw,arena,now,q,low);MagicPackHits(arena,now,q,low);
            // A school bolt's model gives way to its pack head (drawn in ShotFx) from the next frame.
            if(shots!=null)shots.PackHeads=PackLooks;
            draw.Submit(camera,FirstPerson&&!ScopeActive?ViewCamera:null);
            // The muzzle lights: the nearest shooters the tier, Combat glow, Reduced effects and Alarm lights allow.
            NativeMuzzleLight.Budget(GlowQuality,WeaponEffectsQuality,LowGraphics,KitFlash,out int lights,out float gain);muzzleLights.Resolve(new NVector3(eye.x,eye.y,eye.z),lights,gain);NativeMuzzleLights.Upload(muzzleLights);
        }
        private void ShotFx(NativeWeaponFxDraw draw,in NativeShotView v,float now,float q,bool low,bool trails,Vector3 eye,NativeTracerLook look=default)
        {
            // The darkness orb draws its own core, tail and lens (NativeMagicFx); its energy trail here is skipped.
            if(MagicLooks&&NativeMagicFx.Owns(v))return;
            bool tracer=NativeTracerFx.Traced(v),energy=NativeTrailFx.Energy(v);if(!tracer&&!energy)return;
            long key=NativeShotScene.Key(v.Kind,v.Id);var at=new Vector3((float)v.X,(float)v.Y,(float)v.Z);
            if(!emitters.TryGetValue(key,out var e))
            {
                e=spareEmitters.Count>0?spareEmitters.Pop():new Emitter();e.Origin=e.Last=at;e.LastAt=now;e.Carry=0;e.Emitted=0;e.Count=0;e.Seed=key*2654435761L+17;
                e.Halo=NativeWeaponFxShapes.Lin(NativeShotRules.HaloColor(v,now));e.Radius=NativeTrailFx.Radius(v.Radius);emitters.Add(key,e);
            }
            e.Frame=weaponFrame;var velocity=new Vector3((float)v.VX,(float)v.VY,(float)v.VZ);
            // A round drawn from the muzzle (NativeShotLaunch) knows how far past the crown it is, so its streak never reaches
            // back into the barrel; any other is held to the distance since it was first seen.
            if(tracer){if(trails){float flown=v.Launched?(float)v.Flown:Vector3.Distance(at,e.Origin)+.25f;if(look.Kit)KitTracer(draw,at,velocity,flown,look);else NativeWeaponFxShapes.Tracer(draw,at,velocity,flown,v.Spec);}e.Last=at;e.LastAt=now;return;}
            // Energy: the core and glow on the model, then the trail sprites every Spacing metres flown and the ribbon. A school's
            // bolt (Core NativeMagicPackFx) draws its pack head instead of the core, and pack trail sprites.
            var school=PackLooks?NativeMagicPackFx.Bolt(v):NativeMagicSchool.None;
            if(school!=NativeMagicSchool.None)PackHead(school,at,e.Halo,e.Radius,e.Seed,now);
            else NativeWeaponFxShapes.EnergyCore(draw,at,eye,e.Halo,e.Radius,v.Spec,.93f+.07f*Mathf.Sin(now*23+(e.Seed&63)));
            if(trails)
            {
                float spacing=NativeTrailFx.Spacing(q,low);Vector3 step=at-e.Last;float distance=step.magnitude;
                if(distance>1e-4f&&distance<20)
                {
                    float travelled=-e.Carry;var color=NativeWeaponFxShapes.SpriteColor(e.Halo);
                    while(travelled+spacing<=distance)
                    {
                        travelled+=spacing;float f=travelled/distance;
                        NativeTrailFx.Sprite(e.Seed,e.Emitted,e.Radius,out float life,out float size,out var offset,out var drift);
                        int tile=-1;float roll=0,spin=0;if(school!=NativeMagicSchool.None){NativeMagicPackFx.Trail(school,e.Seed,e.Emitted,out tile,out float scale,out roll,out spin);size*=scale;}
                        e.Emitted++;
                        if(trailSpriteCount==trailSprites.Length)Array.Resize(ref trailSprites,trailSprites.Length*2);
                        trailSprites[trailSpriteCount++]=new TrailSprite{At=e.Last+step*f+NativeWeaponFxShapes.V(offset),Drift=NativeWeaponFxShapes.V(drift)+velocity*.06f,Born=Mathf.Lerp(e.LastAt,now,f),Life=life,Size=size,Color=tile>=0?e.Halo:color,Tile=tile,School=school,Roll=roll,Spin=spin};
                    }
                    e.Carry=distance-travelled;
                }
                if(e.Count==0||Vector3.Distance(e.Points[e.Count-1],at)>.02f)e.Push(at,now);else{e.Points[e.Count-1]=at;e.Times[e.Count-1]=now;}
                int first=0;while(first<e.Count-1&&now-e.Times[first]>NativeTrailFx.RibbonLife)first++;
                // A pack head covers the bolt: the ribbon's newest point steps back into it and draws as an older one, so the
                // ribbon grows out of the head instead of ending in a bright square cut across it (the model hid that end).
                if(school!=NativeMagicSchool.None&&e.Count>1&&velocity.sqrMagnitude>1e-6f)
                {
                    var head=e.Points[e.Count-1];float born=e.Times[e.Count-1];e.Points[e.Count-1]=head-velocity.normalized*(e.Radius*1.5f);e.Times[e.Count-1]=now-(float)NativeTrailFx.RibbonLife*.8f;
                    NativeWeaponFxShapes.Ribbon(draw,e.Points,e.Times,first,e.Count,now,eye,e.Halo,e.Radius);e.Points[e.Count-1]=head;e.Times[e.Count-1]=born;
                }
                else NativeWeaponFxShapes.Ribbon(draw,e.Points,e.Times,first,e.Count,now,eye,e.Halo,e.Radius);
            }
            e.Last=at;e.LastAt=now;
        }
        // Hard-surface impacts: each surface Impact event (a prop hit with a normal) throws its curved shards, cooling sparks and
        // quick glow, seeded by the replicated event id.
        private void ImpactFx(NativeWeaponFxDraw draw,NativeArena arena,float now,float q,bool low)
        {
            if(!ReferenceEquals(impactArena,arena)||impactGeneration!=arena.Generation){impactArena=arena;impactGeneration=arena.Generation;impactEvent=0;for(int i=0;i<impactMarks.Length;i++)impactMarks[i].Born=-100;}
            int newest=0;foreach(var e in arena.Events)newest=Math.Max(newest,e.Id);if(newest<impactEvent)impactEvent=0;
            foreach(var e in arena.Events)
            {
                if(e.Id<=impactEvent||e.Kind!=EventKind.Impact||string.IsNullOrEmpty(e.SurfaceId))continue;
                var normal=NativeArena.Vec(e.Normal);if(normal.sqrMagnitude<.25f||now-e.Time>=NativeImpactFx.Life)continue;
                impactMarks[impactNext]=new ImpactMark{At=NativeArena.Vec(e.Position),Normal=normal.normalized,Born=e.Time,Seed=e.Id*1000003L+7,Tint=Color.Lerp(NativeWeaponFxShapes.Lin(e.Color!=0?e.Color:0xffedb8),Color.white,.55f)};
                impactNext=(impactNext+1)%impactMarks.Length;
            }
            impactEvent=Math.Max(impactEvent,newest);
            int shards=NativeImpactFx.ShardCount(q,low),sparks=NativeImpactFx.SparkCount(q,low);
            for(int i=0;i<impactMarks.Length;i++){var m=impactMarks[i];NativeWeaponFxShapes.Impact(draw,m.At,m.Normal,m.Seed,m.Tint,now-m.Born,shards,sparks);}
        }
        private void ClearEmitters(){foreach(var e in emitters.Values)spareEmitters.Push(e);emitters.Clear();trailSpriteCount=0;emitterClock=-1;}
        private void ClearWeaponFx()
        {
            weaponFx?.Begin();weaponSnapshot=null;ClearEmitters();emitterArena=null;emitterGeneration=-1;muzzleLights.Clear();NativeMuzzleLights.Off();
            foreach(var st in slashes.Values)spareSlashes.Push(st);slashes.Clear();hitFlashes.Clear();hitFlashEvent=0;afterimages.Clear();
            impactArena=null;impactGeneration=-1;impactEvent=0;for(int i=0;i<impactMarks.Length;i++)impactMarks[i].Born=-100;ClearMagicPack();
        }
        private void DisposeWeaponFx(){ClearWeaponFx();weaponFx?.Dispose();weaponFx=null;}
    }
}
