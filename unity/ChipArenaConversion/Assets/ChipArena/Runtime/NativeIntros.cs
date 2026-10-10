using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private bool IntroActive=>match?.Phase==NativePhase.Intro||arena?.Pve?.Holding==true&&arena.Pve.Cinematic=="intro";
        private NativePilot IntroPilot=>match.Phase==NativePhase.Intro?arena.Pilots.Find(p=>p.Seat.id==match.IntroSeat):arena.Pve.Boss;
        // The staged entrance (NativeIntroRules.Entrance): the replicated style, or the source's fallback when it has no stage.
        private string IntroStyle=>NativeIntroRules.Entrance(match.Phase==NativePhase.Intro?match.IntroStyle:"dev-siren");
        private float IntroAge=>match.Phase==NativePhase.Intro?(float)(match.Time-match.IntroStarted):arena.Pve.HoldDuration-arena.Pve.HoldLeft;
    }
    public sealed partial class ArenaPresentation
    {
        private CapturedAnimation.Player introRig;private int introActor;private Func<int,Matrix4x4> introHand;private Matrix4x4 introGrip=Matrix4x4.identity;
        // This frame's baked entrance stage, read by delegates made once (no closure per frame).
        private NativeSceneClip stageClip;private float stageAge;private string stageStyle,stageKey;private bool[] stageGlow;private readonly Dictionary<string,bool[]> stageGlows=new Dictionary<string,bool[]>();
        private Func<int,Matrix4x4> stageBone;private Func<int,bool> stageShown;private Func<int,float> stageOpacity;private Func<int,Color> stageTint;
        public void RenderIntro(NativeArena arena,NativePilot pilot,string style,float age,Camera camera)
        {
            if(pilot==null)return;style=NativeIntroRules.Entrance(style);sirenLookCamera=camera;if(introRig==null||introActor!=pilot.Seat.id){introRig?.Dispose();introRig=captured.Create(transform,-100);introActor=pilot.Seat.id;}
            sourceModels.Begin();foreach(var b in batches)b.Clear();FirstPerson=false;combatGlow?.Clear();particles.Clear();
            Actor actor=null;foreach(var a in arena.Snapshot().Actors)if(a.Id==pilot.Seat.id){actor=a;break;}actor??=pilot.Pose;NativeSourceMotion.Intro(introRig,style,age,camera);introBeats=style=="dev-siren"?arena.BossTrack?.beats:null;
            if(style!="dev-siren"){SourceCharacter(introRig,actor,arena.Time,weapon:false,halo:age);IntroWeapon(style,age);TeamBands(introRig,NativeStatusVisuals.PilotBand(arena.Match.Teams,arena.Match.Cooperative,pilot.Seat.AuthorityBoss,pilot.Seat.team,pilot.Seat.id==arena.LocalId));}if(!IntroStage(style,age,camera))throw new System.InvalidOperationException("Missing source entrance stage: "+style);
            DrawBatches(null,0,false);sourceModels.Stage=true;try{sourceModels.Flush(UsesInstancing);}finally{sourceModels.Stage=false;}
        }
        // player-intros.ts: the bare intro rig holds only a weapon intro's own weapon (WEAPON_INTROS[style].spec) or the Emperor's
        // gauntlets, with no chip dressing; every other entrance keeps its hands empty, so the L33T Throne's floating staff (a
        // stage prop, grabbed at 4 s) is the only staff. The model rides the wrist joints it was exported on (staffs: the left
        // hand, where poseWeaponIntro moves the weapon) at NativeIntroRules.IntroGrip; the baked intro clips pose both arms from
        // the armed rig (tools/export_motion.mjs), so the braced weapons keep the free hand on their support grip.
        private void IntroWeapon(string style,float age)
        {
            string model=NativeIntroRules.IntroWeapon(style);if(model==null)return;
            introGrip=Matrix4x4.Translate(CapturedAnimation.Vec(CapturedRig.Mirror(NativeIntroRules.IntroGrip(style))));
            if(introHand==null)introHand=b=>introRig.Bones[b].localToWorldMatrix*introGrip;
            sourceModels.Queue(model,introRig.Root.transform.localToWorldMatrix,introHand);
            if(NativeIntroRules.IntroMuzzleFlash(style,age,out var flash))QueuePiece(flash,introHand(CapturedRig.RHand),0,null);
        }
        // The baked entrance stage (tools/export_cinematics.mjs, the rig excluded) with its lights. ChipBlasts' glow voxels are
        // additive in the source and fade by dimming; drawn alpha-blended as baked they turned into opaque dark cubes once spent,
        // so their tracks fade by alpha instead (NativeIntroRules.GlowAlpha).
        private bool IntroStage(string style,float age,Camera camera)
        {
            if(style!=stageStyle){stageStyle=style;stageKey="stage-"+style;}string key=stageKey;var clip=NativeSceneMotion.Get(key);if(clip==null)return false;NativeLighting.Intro(clip,age,camera,introBeats);SirenCell(clip,key,age,camera);
            if(!stageGlows.TryGetValue(key,out var glow))
            {
                glow=new bool[clip.tracks];for(int t=0;t<clip.tracks;t++)glow[t]=NativeIntroRules.TintVaries(clip.values,clip.frames,t)&&sourceModels.TransparentBone(key,t);
                stageGlows.Add(key,glow);
            }
            stageClip=clip;stageAge=age;stageGlow=glow;
            if(stageBone==null)
            {
                stageBone=b=>NativeSceneMotion.Matrix(stageClip,b,stageAge);stageShown=b=>StageOpacity(b)>.001f;stageOpacity=StageOpacity;
                stageTint=b=>{var c=NativeSceneMotion.Tint(stageClip,b,stageAge);if(b>=0&&b<stageGlow.Length&&stageGlow[b])NativeIntroRules.GlowAlpha(ref c.r,ref c.g,ref c.b);if(b==sirenBulb&&introBeats!=null){c.r*=sirenGlow;c.g*=sirenGlow;c.b*=sirenGlow;}return c;};
            }
            sourceModels.Queue(key,Matrix4x4.identity,stageBone,stageShown,partOpacity:stageOpacity,partTint:stageTint);return true;
        }
        private float StageOpacity(int b)
        {
            float opacity=NativeSceneMotion.Opacity(stageClip,b,stageAge);if(b<0||b>=stageGlow.Length||!stageGlow[b])return opacity;
            var c=NativeSceneMotion.Tint(stageClip,b,stageAge);return opacity*NativeIntroRules.GlowAlpha(ref c.r,ref c.g,ref c.b);
        }
    }
    public sealed partial class NativeSourceModels
    {
        /// <summary>True when model `id` draws a transparent (alpha-blended) section on joint or track `bone`.</summary>
        internal bool TransparentBone(string id,int bone)
        {
            if(!models.TryGetValue(id,out var parts))return false;
            foreach(var part in parts)if(part.Bone==bone&&part.Material!=null&&part.Material.renderQueue>=(int)UnityEngine.Rendering.RenderQueue.Transparent)return true;return false;
        }
    }
}
