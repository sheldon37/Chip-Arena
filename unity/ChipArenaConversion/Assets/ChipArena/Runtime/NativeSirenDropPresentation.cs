using System;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    internal sealed partial class NativeCorpse
    {
        /// <summary>A Siren crush drop's release age (-1 for every other body): its physics clock starts there.</summary>
        internal float DropAt=-1;
        internal bool Drop=>DropAt>=0;
        /// <summary>How far the crush clip's floor (its root stands at claw height) is above the arena floor under the death.</summary>
        internal float FloorDrop;
        /// <summary>The black flame off the dropped body (kills.ts burnRig): its emission accumulator and last clock.</summary>
        internal double BurnAcc;internal float BurnClock=float.NegativeInfinity;
        /// <summary>A body handed the crush clip's pose at <paramref name="release"/> (world matrices) and each bone's motion.</summary>
        internal static NativeCorpse SirenDrop(NativeDeath death,NativeArena arena,Matrix4x4[] bind,Vector3[] linear,Vector3[] angular,float release)
        {
            var at=new Vector3[22];var turn=new Quaternion[22];for(int i=0;i<22;i++){at[i]=bind[i].GetColumn(3);turn[i]=bind[i].rotation;}
            return new NativeCorpse(death,arena,bind,at,turn,linear,angular,release);
        }
    }

    // Last Laugh deaths (siren-crush): the crush clip until the claw lets go, then the body falls as a ragdoll from exactly
    // the pose the clip shows there (Core/NativeSirenDrop). The clip still supplies the head burst, the gibs (now landing on
    // the arena floor, not the clip's claw-height one) and the black flame already burning at the release; later flame comes
    // off the ragdoll's joints into the world flame pool. Host, guests and the killcam build the same body from the
    // replicated death (position, yaw, time, seed), the clip and the arena props; the physics is client-side presentation.
    public sealed partial class ArenaPresentation
    {
        private NativeSceneClip dropClip;private short[] dropBirths;private bool[] dropVoxel,dropGib;private float[] dropLaunch;
        // The death being drawn, read by the cached delegates (sourceModels.Queue evaluates them at once): no per-frame garbage.
        private NativeCorpse dropBody;private Matrix4x4[] dropCarried;private float dropAge,dropRelease,dropGone;private bool dropLow,dropFallen;private Vector3 dropPivot;
        private readonly float[] dropRoot=new float[16],dropM=new float[16],dropW=new float[16],dropJoints=new float[22*16];
        private Func<int,Matrix4x4> dropSceneBone,dropVictimBone;private Func<int,bool> dropSceneShown,dropVictimShown;private Func<int,float> dropSceneOpacity;private Func<int,Color> dropSceneTint;

        private bool SirenDropReady()
        {
            var clip=NativeSceneMotion.Get("death-siren-crush");if(clip?.victim==null||clip.victim.Length!=22)return false;if(clip==dropClip)return true;
            var voxel=new bool[clip.tracks];var gib=new bool[clip.tracks];var launch=new float[clip.tracks*6];var one=new float[6];var victim=new bool[clip.tracks];foreach(int t in clip.victim)victim[t]=true;
            for(int t=0;t<clip.tracks;t++){voxel[t]=NativeIntroRules.TintVaries(clip.values,clip.frames,t);if(NativeSirenDrop.Gib(clip.values,clip.frames,t,clip.duration,voxel[t],victim[t],one)){gib[t]=true;Array.Copy(one,0,launch,t*6,6);}}
            dropBirths=NativeSirenDrop.Births(clip.values,clip.frames,clip.tracks,voxel);dropVoxel=voxel;dropGib=gib;dropLaunch=launch;dropClip=clip;
            dropSceneBone??=DropScene;dropVictimBone??=DropVictim;dropSceneShown??=DropSceneShown;dropVictimShown??=DropVictimShown;
            dropSceneOpacity??=t=>NativeSceneMotion.Opacity(dropClip,t,dropAge);dropSceneTint??=t=>NativeSceneMotion.Tint(dropClip,t,dropAge);
            return true;
        }
        /// <summary>The body for this death, built once from the clip at the release (the Siren still showing the hold keeps the
        /// source's limp hang; the native hold ends at the crush, so the body drops after one limp beat). Null: no room. A PvE kept
        /// body (one per downed member) always gets one past the 32-body cap, so what is drawn and what the repair rebuilds from
        /// (StartRevival) are the same ragdoll.</summary>
        private NativeCorpse SirenDropBody(NativeArena arena,NativeDeath death)
        {
            if(corpses.TryGetValue(death.id,out var old)){if(old.Death.seed==death.seed&&old.Drop)return old;old.Dispose();corpses.Remove(death.id);}
            if(arena==null||corpses.Count>=32&&!death.kept)return null;var clip=dropClip;
            var owner=PilotOf(arena,death.owner);var hold=owner?.Pose;
            bool holding=owner!=null&&owner.Hp>0&&hold!=null&&hold.SourceSpec=="siren"&&hold.SourceAction=="grab"&&hold.SourceUntil>death.at+NativeSirenDrop.Limp&&(death.actor==null||death.actor.GrabOwner!=death.owner||Math.Abs(hold.SourceAt-death.actor.GrabAt)<.05f);
            float release=NativeSirenDrop.Release(holding);var root=new float[16];NativeSirenDrop.Root(death.position.x,death.position.y,death.position.z,death.yaw,root);
            var bind=new float[22*16];var lin=new float[66];var ang=new float[66];NativeSirenDrop.Handoff(clip.values,clip.frames,clip.duration,clip.victim,dropVoxel,root,release,death.seed,bind,lin,ang);
            var pose=new Matrix4x4[22];var linear=new Vector3[22];var angular=new Vector3[22];
            for(int b=0;b<22;b++){pose[b]=DropUnity(bind,b*16);linear[b]=new Vector3(-lin[b*3],lin[b*3+1],lin[b*3+2]);angular[b]=new Vector3(ang[b*3],-ang[b*3+1],-ang[b*3+2]);}
            var body=NativeCorpse.SirenDrop(death,arena,pose,linear,angular,release);
            if(arena.Root!=null&&arena.Root.scene.GetPhysicsScene().Raycast(death.position,Vector3.down,out var hit,8,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))body.FloorDrop=Mathf.Max(0,death.position.y-.865f-hit.point.y);
            corpses[death.id]=body;return body;
        }
        /// <summary>Sets the drawn state for <paramref name="death"/> at <paramref name="age"/> (clip age <paramref name="shown"/>).</summary>
        private void DropFrame(NativeDeath death,NativeCorpse body,float age,float shown)
        {
            dropBody=body;dropAge=shown;dropRelease=body.DropAt;dropFallen=age>=body.DropAt;dropGone=death.kept?0:NativeSirenDrop.Gone(age);dropLow=EffectsQuality<.5f;
            NativeSirenDrop.Root(death.position.x,death.position.y,death.position.z,death.yaw,dropRoot);dropCarried=null;
            if(shown<NativeSirenDrop.Carry)
            {
                // SourceDeathScene's carry: the last pose the live rig showed (the held victim), blended into the clip over .08 s.
                if(!carriedDeaths.TryGetValue(death.id,out var carried)||carried.seed!=death.seed)
                {var m=new Matrix4x4[22];if(rigs.TryGetValue(death.id,out var rig)||minionRigs.TryGetValue(death.id,out rig))for(int i=0;i<22;i++)m[i]=rig.Bones[i].localToWorldMatrix;else for(int i=0;i<22;i++)m[i]=DropClipBone(i,0);carried=(death.seed,m);carriedDeaths[death.id]=carried;}
                dropCarried=carried.bones;
            }
            if(dropFallen&&dropGone>0)dropPivot=body.Bone(CapturedRig.Pelvis).GetColumn(3);
        }
        /// <summary>PresentDeath for siren-crush; false falls back to SourceDeathScene (no clip, or no room for a body).</summary>
        private bool SirenDrop(NativeArena arena,NativeDeath death,float age,float revival)
        {
            if(death.style!="siren-crush"||!SirenDropReady())return false;var body=SirenDropBody(arena,death);if(body==null)return false;
            body.Step(age);DropFrame(death,body,age,death.kept?Math.Min(age,NativeSirenDrop.Keep):age);
            // kills.ts burnRig off the fallen body (a kept body smoulders at 30 % once held), on the presentation clock.
            float clock=KillClock(arena);
            if(dropFallen&&revival<0&&!float.IsNegativeInfinity(body.BurnClock))
            {
                float dt=Mathf.Clamp(clock-body.BurnClock,0,.05f);
                if(dt>0){for(int b=0;b<22;b++)DropSource(DropVictim(b),dropJoints,b*16);body.BurnAcc=NativeSirenDrop.Burn(WorldFlames(),dropJoints,dt,NativeSirenDrop.BurnRate(dropAge,EffectsQuality,death.kept&&age>NativeSirenDrop.Keep),body.BurnAcc);}
            }
            body.BurnClock=clock;
            CinematicRoute(death,out int layer,out Camera only);float fade=revival>=0?1-Mathf.Clamp01(revival/.8f):1;
            var root=Matrix4x4.TRS(death.position-Vector3.up*.865f,Quaternion.Euler(0,death.yaw,0),Vector3.one);
            sourceModels.Queue("death-siren-crush",root,dropSceneBone,dropSceneShown,layer:layer,camera:only,opacity:fade,partOpacity:dropSceneOpacity,partTint:dropSceneTint);
            // The body arrives charred by the hold. The crushed head (and its hat) is gone. The weapon, hidden in the hold, falls out
            // of the opening hand at the release as the body's loose weapon (NativeCorpse's dropped weapon) and shrinks with it.
            Color ink=NativeArena.ColorOf(0x0c0a10),bodyTint=Color.Lerp(BodyTint(death.actor?.BodyColor),ink,.85f),accentTint=Color.Lerp(AccentTint(death.actor?.AccentColor),ink,.85f);
            sourceModels.Queue(sourceModels.WoundedPilot(death.id,death.actor?.Wounds,death.actor?.BodyColor,ForceCompatibilityRendering),root,dropVictimBone,dropVictimShown,layer,only,body:bodyTint,accent:accentTint,opacity:fade);
            if(dropFallen&&body.LooseModel!=null&&body.PresentedWeapon is Matrix4x4 loose)sourceModels.Queue(body.LooseModel,DropShrink(loose),layer:layer,camera:only,opacity:fade);
            return true;
        }
        /// <summary>StartRevival: a kept crush body's bones as drawn at <paramref name="age"/> (the ragdoll as it lies) and the
        /// crushed head's subtree as missing.</summary>
        private bool SirenDropPose(NativeArena arena,NativeDeath death,float age,Matrix4x4[] bones,bool[] missing)
        {
            if(death.style!="siren-crush"||!SirenDropReady())return false;var body=SirenDropBody(arena,death);if(body==null)return false;
            body.Step(age);DropFrame(death,body,age,death.kept?Math.Min(age,NativeSirenDrop.Keep):age);
            for(int b=0;b<22;b++){bones[b]=DropVictim(b);missing[b]=!DropVictimShown(b);}return true;
        }
        private Matrix4x4 DropClipBone(int b,float age)
        {var clip=dropClip;int track=clip.victim[b];NativeSirenDrop.Sample(clip.values,clip.frames,track,clip.duration,age,dropVoxel[track],dropM);NativeSirenDrop.Mul(dropRoot,0,dropM,0,dropW,0);return DropUnity(dropW,0);}
        private Matrix4x4 DropVictim(int b)
        {
            if(dropFallen)
            {
                return DropShrink(dropBody.Bone(b));
            }
            var goal=DropClipBone(b,dropAge);if(dropCarried==null)return goal;
            float t=Mathf.Clamp01(dropAge/NativeSirenDrop.Carry);t=t*t*t*(t*(t*6-15)+10);if(t>=1)return goal;var from=dropCarried[b];for(int i=0;i<16;i++)goal[i]=Mathf.Lerp(from[i],goal[i],t);return goal;
        }
        /// <summary>Coming apart into black flame: the fallen body (and its weapon) shrinks about its pelvis (kills.ts scales the
        /// victim group, the weapon in its hand included).</summary>
        private Matrix4x4 DropShrink(Matrix4x4 m)
        {if(dropGone<=0)return m;float k=Mathf.Max(.001f,1-dropGone);return Matrix4x4.Translate(dropPivot)*Matrix4x4.Scale(new Vector3(k,k,k))*Matrix4x4.Translate(-dropPivot)*m;}
        private bool DropVictimShown(int b)=>NativeSceneMotion.Opacity(dropClip,dropClip.victim[b],dropAge)>.001f;
        private Matrix4x4 DropScene(int track)
        {
            var clip=dropClip;NativeSirenDrop.Sample(clip.values,clip.frames,track,clip.duration,dropAge,dropVoxel[track],dropM);
            if(dropGib[track]&&dropBody.FloorDrop>.01f){NativeSirenDrop.GibAt(dropLaunch,track*6,dropAge,dropBody.FloorDrop,out float x,out float y,out float z);dropM[12]=x;dropM[13]=y;dropM[14]=z;}
            NativeSirenDrop.Mul(dropRoot,0,dropM,0,dropW,0);return DropUnity(dropW,0);
        }
        private bool DropSceneShown(int track)
        {
            var clip=dropClip;if(!(NativeSceneMotion.Opacity(clip,track,dropAge)>.001f))return false;if(!dropVoxel[track])return true;
            // Reduced effects burn about half the cubes, as the source's 220-cube field at low quality.
            if(dropLow&&(track&1)==1)return false;return NativeSirenDrop.FlameShown(dropBirths,clip.frames,clip.duration,track,dropAge,dropRelease);
        }
        // Source (three.js, column-major) <-> Unity: x mirrored (NativeSceneMotion.Matrix).
        private static Matrix4x4 DropUnity(float[] s,int at){var m=new Matrix4x4();for(int i=0;i<16;i++){int row=i%4,column=i/4;m[row,column]=s[at+i]*((row==0)!=(column==0)?-1:1);}return m;}
        private static void DropSource(Matrix4x4 m,float[] o,int at){for(int i=0;i<16;i++){int row=i%4,column=i/4;o[at+i]=m[row,column]*((row==0)!=(column==0)?-1:1);}}
    }
}
