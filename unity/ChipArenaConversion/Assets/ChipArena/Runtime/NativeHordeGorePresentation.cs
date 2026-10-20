using System;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NVector2=System.Numerics.Vector2;
using NMatrix=System.Numerics.Matrix4x4;

namespace ChipArena.UnityConversion
{
    // The horde's gore (owner 2026-10-19: "I want them to be more "standard" zombies, that produce gore when shot / gibbed, not
    // just black flames. High-quality design and fx please"; Core/NativeHordeGore.cs, docs/ZOMBIE-GORE.md). Presentation only:
    // every input is what host, guests and the killcam replay all hold (an add's replicated hits: time, point, direction, detail,
    // amount, attacker; its death record: id, seed, time, position, yaw, lost limbs, damage kind; PveGib's severed limbs) and the
    // field clock. No hit, collider, timing, damage, movement or network state is read back or changed.
    //  - A hit (HordeHit -> HordeGoreHit): the wound on the drawn body's nearest part (within the authority's struck part when the
    //    hit carries its horde-hit data), facing the shot, kept on the joint; blood out of it and along the shot into the blood sim
    //    (red, its splats stay on floors and walls); a puff of red mist and flesh bits; a head hit harder. Wounds draw on the
    //    nearest adds (the tier's count) and drip while fresh.
    //  - A death (HordeKindDeath -> HordeGoreDeath): the fate (a corpse, a head burst, a gib, a crush, the bloater's own burst);
    //    a gib throws the model's own limbs (HordeLaunch) and the trunk's chunks; a head burst leaves a fountaining neck stump; a
    //    blade's slice leaves stumps where it cut. The body falls on its kind's take and settles into a corpse (HordeGoreSettle)
    //    that lies over a spreading pool, then sinks and fades (HordeCorpsesDraw), under the tier's cap.
    //  - Severed and sliced parts stay where they land as long as the corpses, trailing blood as they fly (HordePartsDraw).
    // The lit voxels go through the shared effects renderer (NativeVfxDraw.Block), the mist through its vapour, the pools into the
    // zombie effects' decal layer. Allocation free in steady state (fixed arrays, cached delegates; the job arrays grow once).
    public sealed partial class ArenaPresentation
    {
        private readonly NativeHordeGoreState hordeGore=new NativeHordeGoreState();
        /// <summary>The horde gore's state (the checks and the DEV preview read it).</summary>
        public NativeHordeGoreState HordeGore=>hordeGore;
        private readonly NMatrix[] goreBones=new NMatrix[22];
        private struct GoreJob{public int Id;public CapturedAnimation.Player Rig;}
        private GoreJob[] goreJobs=new GoreJob[64];private float[] goreRank=new float[64];private int[] goreOrder=new int[64];private int goreJobCount;
        private int goreHitTarget=-1,goreHitIndex;private float goreHitTime=float.NaN;
        private static readonly int[] GoreGibJoints={CapturedRig.Head,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LHip,CapturedRig.RHip};

        private static NMatrix GoreN(in Matrix4x4 m)=>new NMatrix(m.m00,m.m10,m.m20,m.m30,m.m01,m.m11,m.m21,m.m31,m.m02,m.m12,m.m22,m.m32,m.m03,m.m13,m.m23,m.m33);
        private static Matrix4x4 GoreU(in NMatrix n)
        {
            var m=new Matrix4x4();m.m00=n.M11;m.m10=n.M12;m.m20=n.M13;m.m30=n.M14;m.m01=n.M21;m.m11=n.M22;m.m21=n.M23;m.m31=n.M24;
            m.m02=n.M31;m.m12=n.M32;m.m22=n.M33;m.m32=n.M34;m.m03=n.M41;m.m13=n.M42;m.m23=n.M43;m.m33=n.M44;return m;
        }
        private static NVector GoreV(Vector3 v)=>new NVector(v.x,v.y,v.z);
        private static Vector3 GoreU(NVector v)=>new Vector3(v.X,v.Y,v.Z);
        private NMatrix[] GoreBones(CapturedAnimation.Player rig){for(int b=0;b<22;b++)goreBones[b]=GoreN(rig.Bones[b].localToWorldMatrix);return goreBones;}

        /// <summary>HordeKindsBegin: the frame's tier and gore switch, the draw lists emptied, the gone remains dropped.</summary>
        private void HordeGoreBegin()
        {
            hordeGore.Tier=NativeHordeGore.Tier(LowGraphics,Mathf.Clamp01(EffectsQuality));hordeGore.Enabled=Gore;hordeGore.Begin();goreJobCount=0;goreHitTarget=-1;hordeGore.Expire(hordeClock);
        }
        /// <summary>A posed body whose wounds may draw this frame (its rig's joints are final by the effects pass).</summary>
        private void GoreWoundsJob(int id,CapturedAnimation.Player rig)
        {
            if(rig==null||hordeGore.Wounds(id)==0)return;
            if(goreJobCount==goreJobs.Length){Array.Resize(ref goreJobs,goreJobCount*2);Array.Resize(ref goreRank,goreJobCount*2);Array.Resize(ref goreOrder,goreJobCount*2);}
            goreJobs[goreJobCount++]=new GoreJob{Id=id,Rig=rig};
        }

        // ================================================================================================================
        // Hits
        // ================================================================================================================
        /// <summary>
        /// HordeHit: one replicated hit on a drawn add (`m` null for the blow that killed it this frame; `s` its state). `at` the add's
        /// body, `dir` the hit's push (flat; from a blast's centre for a blast). The cause and the killing blow's facts are kept on
        /// the state for the death's fate. With the authority's horde-hit data (e.HasHordeHit; docs/ZOMBIE-GORE.md) the head is
        /// e.Head, the wound goes on e.Part (NativeHordeGore.Struck, with the add's lost limbs) and the fate and spray read e.Raw
        /// (NativeHordeGore.Strength); old frames keep the Headshot kind, the nearest drawn part and the clamped e.Amount.
        /// </summary>
        private void HordeGoreHit(NativeArena arena,NativeMinion m,HordeKindState s,in ArenaEvent e,Vector3 at,Vector3 dir,bool melee,string spec,bool heavy)
        {
            var cause=NativeHordeGore.Classify(e.Detail,spec,melee,e.DamageKind);if(cause==NativeHordeGore.Cause.None)return;
            bool blast=cause==NativeHordeGore.Cause.Blast||cause==NativeHordeGore.Cause.Crush;if(dir.sqrMagnitude<.5f&&!blast)return;
            int id=e.Target;int kind=s!=null&&s.Kind>=0?s.Kind:m!=null?NativeHordeKinds.Resolve(m.Pve?.Stats?.id):NativeHordeKinds.Shambler;float maxHp=m!=null?m.MaxHp:s!=null?s.MaxHp:100;
            Vector3 point=e.Position.x==0&&e.Position.y==0&&e.Position.z==0?at+Vector3.up*.3f:NativeArena.Vec(e.Position);if(dir.sqrMagnitude<.5f)dir=(at-point).normalized;
            float blastDistance=blast?new Vector2(point.x-at.x,point.z-at.z).magnitude:99;
            if(id==goreHitTarget&&e.Time==goreHitTime)goreHitIndex++;else{goreHitTarget=id;goreHitTime=e.Time;goreHitIndex=0;}
            bool known=e.HasHordeHit;float strength=NativeHordeGore.Strength(cause,known,e.Raw,e.Amount);
            if(s!=null){s.GoreCause=cause;s.GoreAt=e.Time;s.GoreAmount=strength;s.GoreBlast=blastDistance;s.GoreHeavy=heavy;s.GoreHead=known&&e.Head;s.MaxHp=maxHp;s.GorePush=new NVector2(dir.x,dir.z);}
            if(!minionRigs.TryGetValue(id,out var rig)||rig?.Bones==null||hordeEyeKnown&&(at-hordeEye).sqrMagnitude>60*60)return;
            float floor=at.y-NativeHordeKinds.RootHeight(s!=null?s.Real:kind,m?.Pve?.Crawl==true);
            // A blade's gash runs across the body: the blow's sweep, tilted by the swing (a diagonal stroke).
            var across=Vector3.Cross(Vector3.up,dir);var sweep=melee?(across.normalized+Vector3.up*(NativeHordeGore.H(NativeHordeGore.HitSeed(id,e.Time),0,9)-.5f)*1.1f).normalized:Vector3.zero;
            // The add's lost limbs at the hit (replicated; for the blow that killed it this frame, the last ones it was drawn with).
            int lost=m?.Pve!=null?m.Pve.Lost:s!=null&&s.Lost>0?s.Lost:0;
            var h=new GoreHit{Id=id,Kind=kind,Index=goreHitIndex,Time=e.Time,Amount=e.Amount,MaxHp=maxHp,Cause=cause,Kill=m==null,Head=known?e.Head:e.Kind==EventKind.Headshot,Known=known,Raw=e.Raw,HasPart=e.HasPart,Part=e.Part,Lost=lost,Point=GoreV(point),Dir=GoreV(dir),Sweep=GoreV(sweep)};
            hordeGore.Hit(h,GoreBones(rig),floor,out bool head);if(s!=null)s.GoreHead=known?e.Head:head;
        }
        /// <summary>HordeSever: the authority severed a living add's limb (PveGib): a raw stump on the joint it hung from, a gush.</summary>
        private void HordeGoreSever(CapturedAnimation.Player rig,int id,int kind,int joint,float now,NVector2 push)
        {
            if(rig?.Bones==null)return;hordeGore.Stump(id,GoreBones(rig),joint,now,new NVector(push.X,.3f,push.Y),kind);
        }

        // ================================================================================================================
        // Deaths
        // ================================================================================================================
        /// <summary>
        /// The first sight of a death (HordeKindDeath, and the DEV preview): its fate under the gib cap, its remains (the pool, a
        /// gib's chunks), a gib's limbs thrown as the model's own parts and its body hidden, a head burst's neck stump, a blade's
        /// stumps. `slice` (the joints the blade's plan cut, as a mask) grows with what the fate takes off. Returns the fate.
        /// </summary>
        private NativeHordeGore.Fate HordeGoreDeath(int id,int kind,int look,int lost,float at,int recordSeed,NativeHordeGore.Cause cause,bool head,float amount,float maxHp,float blast,bool heavy,Vector3 push,CapturedAnimation.Player rig,float floorY,ref int slice,string model,int sliced)
        {
            var fate=sliced!=0&&kind!=NativeHordeKinds.Bloater?NativeHordeGore.Fate.Corpse:hordeGore.Fate(kind,at,cause,head,amount,maxHp,blast,heavy);
            if(NativeIronworksKinds.Local(kind)==NativeIronworksKinds.Cinderling)fate=NativeHordeGore.Fate.Corpse;// its own ember death
            uint seed=NativeHordeGore.DeathSeed(id,recordSeed);var r=hordeGore.Die(id,kind,at,seed,fate);float sc=NativeHordeKinds.Scale[kind];
            push.y=0;if(push.sqrMagnitude<1e-4f)push=Vector3.forward;push.Normalize();
            var chest=rig.Bones[CapturedRig.Chest].position;var headAt=rig.Bones[CapturedRig.Head].position+Vector3.up*.12f*sc;
            r.Look=look;r.Floor=floorY;r.Scale=sc;r.Lost=lost;r.Push=GoreV(push);r.Jaw=.4f;var v=HordeVariation(model,id);r.Variation=new System.Numerics.Vector4(v.r,v.g,v.b,v.a);
            r.ChunkAt=GoreV(fate==NativeHordeGore.Fate.HeadGib?headAt:chest);r.PoolCentre=new NVector(chest.x,floorY+.014f,chest.z);
            r.PoolMost=hordeGore.Enabled?NativeHordeGore.PoolMost(kind,fate,hordeGore.Wounds(id),sc):0;
            r.PoolAt=fate==NativeHordeGore.Fate.Burst?at+NativeHordeGait.BurstAt+.1f:NativeHordeGore.Gibs(fate)&&fate!=NativeHordeGore.Fate.HeadGib?at+.18f:at+NativeHordeGait.DownAt;
            r.ChunkDelay=fate==NativeHordeGore.Fate.Burst?NativeHordeGait.BurstAt:0;
            var bones=GoreBones(rig);var np=new NVector2(push.x,push.z);
            switch(fate)
            {
                case NativeHordeGore.Fate.Gib:case NativeHordeGore.Fate.Crush:
                {
                    // The limbs fly off whole along the blow (the train and the saws throw them hard and flat), the trunk bursts.
                    bool crush=fate==NativeHordeGore.Fate.Crush;
                    foreach(int j in GoreGibJoints){if(!HordeJointShown(lost,j))continue;HordeLaunch(rig,NativeHordeModels.Id(kind,look,NativeHordeModels.Full),kind,id,j,at,np,crush?2.6f:1.4f+.8f*NativeHordeGore.H(seed,j,81),Mathf.Max(.05f,rig.Bones[j].position.y-floorY));}
                    slice=-1&~1;// every joint but the body root: nothing of the body is drawn whole
                    hordeGore.Blast(id,GoreV(chest),GoreV(push),at,floorY,seed,crush?1:.85f);break;
                }
                case NativeHordeGore.Fate.HeadGib:
                    slice|=1<<CapturedRig.Head;hordeGore.Stump(id,bones,CapturedRig.Head,at,GoreV(push),kind);hordeGore.Blast(id,GoreV(headAt),GoreV(push),at,floorY,seed,.5f);break;
                case NativeHordeGore.Fate.Burst:
                    hordeGore.Blast(id,GoreV(chest),GoreV(push),at+NativeHordeGait.BurstAt,floorY,seed,.9f);break;
            }
            // A blade's slice: raw stumps where it cut, on the body that falls.
            if(sliced!=0)for(int j=1;j<22;j++)if((sliced&(1<<j))!=0)hordeGore.Stump(id,bones,j,at,GoreV(push),kind);
            return fate;
        }
        /// <summary>The falling body (HordeKindDeath): the pool follows its chest until it lies still; once the fall has settled
        /// (`fall` past NativeMinionFx.DeathSeconds, the take's end) the corpse keeps this pose. True while the rig still draws it;
        /// false once its remains are gone (a horde past the tier's cap sank them), so the body never pops back. Remains the cap
        /// starts sinking while they still fall keep the pose they have, and sink from there.</summary>
        private bool HordeGoreSettle(int id,float at,float fall,CapturedAnimation.Player rig,int lost,int hidden,float floorY)
        {
            var r=hordeGore.RemainsOf(id);if(r==null)return false;if(Mathf.Abs(r.At-at)>.05f)return true;if(r.Posed)return r.Frame>=hordeKindFrame;
            var chest=rig.Bones[CapturedRig.Chest].position;r.PoolCentre=new NVector(chest.x,floorY+.014f,chest.z);if(!r.Body)return true;
            if(fall<(float)NativeMinionFx.DeathSeconds&&!NativeHordeGoreState.Fading(r,hordeClock))return true;
            r.Root=GoreN(rig.Root.transform.localToWorldMatrix);for(int b=0;b<22;b++)r.Bones[b]=GoreN(rig.Bones[b].localToWorldMatrix);r.Lost=lost;r.Hidden=hidden<0?0:hidden;
            hordeGore.Pose(r,hordeKindFrame);return true;
        }
        /// <summary>A head burst's neck fountains (HordeKindDeath while it falls): pulsing drops out of the stump along the neck.</summary>
        private void HordeGoreFountain(int id,float at,float now,CapturedAnimation.Player rig,ref int fountain)
        {
            if(!Gore||rig?.Bones==null)return;int due=NativeHordeGore.FountainDue(now-at);if(fountain>=due)return;
            var neck=rig.Bones[CapturedRig.Neck];var stump=rig.Bones[CapturedRig.Head].position;var up=(stump-neck.position).normalized;
            for(;fountain<due;fountain++)
            {
                long key=((long)(id&0xffffff)<<36)^((long)(BitConverter.SingleToInt32Bits(at)&0xfffffff)<<4)^((long)(fountain+1)<<28)^(1L<<59);bool stain=stainedWounds.Add(key);
                blood.Spray("squirt",NativeHordeGore.FountainPower(fountain),NativeHordeGore.DeathSeed(id,fountain+1),key,GoreV(stump),GoreV(up),GoreV((up+Vector3.up*.6f).normalized),at+fountain*NativeHordeGore.FountainStep,BloodQuality,LowGraphics,stain,0,id,true);
            }
        }

        // ================================================================================================================
        // Corpses
        // ================================================================================================================
        private NativeHordeGoreState.Remains goreDraw;private float goreDrawSink;private Func<int,Matrix4x4> goreCorpseBone,goreCorpseJawBone;private Func<int,bool> goreCorpseVisible;
        /// <summary>HordeKindsEnd: the settled corpses, each on its kept pose, sinking and fading at its life's end; the reduced model
        /// past 24 m, none past 80 m.</summary>
        private void HordeCorpsesDraw(float now)
        {
            if(sourceModels==null)return;
            goreCorpseBone??=b=>{var m=GoreU(goreDraw.Bones[b]);m.m13-=goreDrawSink;return m;};
            goreCorpseVisible??=b=>HordeJointShown(goreDraw.Lost,b)&&(goreDraw.Hidden==0||!HordeHiddenJoint(goreDraw.Hidden,b));
            goreCorpseJawBone??=b=>{var m=goreCorpseBone(b);if(b!=CapturedRig.Head)return m;var h=NativeHordeModels.JawHinge(goreDraw.Kind);var hinge=new Vector3(h.X,h.Y,h.Z);return m*Matrix4x4.Translate(hinge)*Matrix4x4.Rotate(Quaternion.Euler(goreDraw.Jaw*Mathf.Rad2Deg,0,0))*Matrix4x4.Translate(-hinge);};
            foreach(var r in hordeGore.Corpses)
            {
                if(!r.Live||!r.Body||!r.Posed||r.Frame>=hordeKindFrame)continue;float age=now-r.At;if(age<0)continue;
                NativeHordeGore.Sink(age,r.FadeAt,out float sink,out float alpha);if(alpha<=.01f)continue;
                var chest=GoreU(NativeHordeGore.Origin(r.Bones[CapturedRig.Chest]));float d=hordeEyeKnown?Vector3.Distance(chest,hordeEye):0;if(d>80)continue;
                int tier=d<=24?NativeHordeModels.Full:NativeHordeModels.Reduced;string model=NativeHordeModels.Id(r.Kind,r.Look,tier);if(!sourceModels.Has(model))continue;
                goreDraw=r;goreDrawSink=sink;var root=GoreU(r.Root);root.m13-=sink;var body=new Color(r.Variation.X,r.Variation.Y,r.Variation.Z,alpha);var glow=HordeGlow(r.Id);
                sourceModels.Queue(model,root,goreCorpseBone,goreCorpseVisible,body:body,glow:glow);
                if(NativeHordeJaw.Hinged(r.Kind)){string jaw=NativeHordeModels.JawId(r.Kind,r.Look,tier);if(sourceModels.Has(jaw))sourceModels.Queue(jaw,root,goreCorpseJawBone,goreCorpseVisible,body:body,glow:glow);}
                if(r.Extra>=0)IronworksCorpseCrust(r.Extra,tier,root,goreCorpseBone,goreCorpseVisible,glow,alpha);
                if(r.Prop>=0){var pm=GoreU(r.PropMatrix);pm.m13-=sink;sourceModels.Queue(NativeHordeModels.PropId(r.Prop,0),pm);}
            }
        }

        /// <summary>A horde add's joint for an arrow lodged in it (NativeBowPresentation BowBone): the live or falling rig, then the
        /// settled corpse's kept pose (sinking with it); false when that part is gone (cut off, gibbed, lost) or the add is.</summary>
        private bool HordeArrowBone(int target,int bone,out NMatrix world)
        {
            world=NMatrix.Identity;var r=hordeGore.RemainsOf(target);
            if(r!=null)
            {
                if(!r.Body||(r.Hidden&(1<<bone))!=0||!HordeJointShown(r.Lost,bone))return false;
                if(r.Posed&&r.Frame<hordeKindFrame){NativeHordeGore.Sink(hordeClock-r.At,r.FadeAt,out float sink,out float alpha);if(alpha<.5f)return false;world=r.Bones[bone];world.M42-=sink;return true;}
            }
            if(!minionRigs.TryGetValue(target,out var rig)||rig?.Bones==null||!HordeKindDrawn(target))return false;world=GoreN(rig.Bones[bone].localToWorldMatrix);return true;
        }

        // ================================================================================================================
        // The effects pass (DrawHordeKindFx, between the zombie effects' Begin and Submit)
        // ================================================================================================================
        private static readonly Color GoreMistTint=NativeVfxDraw.Linear(0x5c0610)*1.1f;
        /// <summary>The frame's gore: the nearest adds' wounds and drips, the hits' mist and bits, the remains' chunks, pools and wounds,
        /// and the frame's sprays handed to the blood sim.</summary>
        private void HordeGoreFx(NativeZombieFxDraw zfx,Vector3 eye,float now)
        {
            int t=hordeGore.Tier<0?0:hordeGore.Tier>2?2:hordeGore.Tier;
            for(int n=0;n<goreJobCount;n++){goreOrder[n]=n;var rig=goreJobs[n].Rig;goreRank[n]=rig==null||rig.Bones==null?float.MaxValue:Vector3.Distance(rig.Bones[CapturedRig.Chest].position,eye);}
            Array.Sort(goreRank,goreOrder,0,goreJobCount);
            for(int r=0;r<goreJobCount&&r<NativeHordeGore.WoundAdds[t];r++)
            {
                if(goreRank[r]>NativeHordeGore.WoundRange)break;var j=goreJobs[goreOrder[r]];if(j.Rig?.Bones==null)continue;var bones=GoreBones(j.Rig);
                hordeGore.DrawWounds(j.Id,bones,now);hordeGore.Drip(j.Id,bones,now);
            }
            hordeGore.Collect(now,GoreV(eye));
            if(vfx!=null)
            {
                for(int i=0;i<hordeGore.BlockCount;i++)
                {
                    var b=hordeGore.Blocks[i];var c=new Color(b.Albedo.X,b.Albedo.Y,b.Albedo.Z,0);
                    vfx.Block(GoreU(b.At),new Quaternion(b.Turn.X,b.Turn.Y,b.Turn.Z,b.Turn.W),GoreU(b.Size),c,b.Spec,b.Gloss,c*.06f,c*.35f+new Color(.02f,.006f,.006f,0));
                }
                for(int i=0;i<hordeGore.MistCount;i++){var m=hordeGore.Mists[i];vfx.Vapor(GoreU(m.At),GoreMistTint,m.Opacity,m.Size,m.U,m.Seed%8191u,NativeVfxDraw.NoFloor,0);}
            }
            if(zfx!=null&&hordeGore.PoolCount>0){SplatLight();for(int i=0;i<hordeGore.PoolCount;i++)GorePoolDecal(zfx.Decals,hordeGore.Pools[i]);}
            for(int i=0;i<hordeGore.SprayCount;i++)
            {
                var s=hordeGore.Sprays[i];bool stain=stainedWounds.Add(s.Key);
                blood.Spray(s.Kind,s.Severity,s.Seed,s.Key,s.Origin,s.Normal,s.Along,s.Time,BloodQuality,LowGraphics,stain,s.Extra,s.Target,s.Kill);
            }
            hordeGore.Sprayed();
        }
        private readonly Vector3[] gorePoolRing=new Vector3[NativeHordeGore.PoolLobes];
        /// <summary>A pool on the floor: its lobed outline in dark, drying blood, a wetter core that catches the light while fresh.</summary>
        private void GorePoolDecal(NativeFxDraw draw,in GorePool p)
        {
            if(!(p.Alpha>.01f)||!(p.Radius>.01f))return;var c=GoreU(p.At);float dry=NativeBloodRules.Darken(p.Age),wet=NativeHordeGore.Wet(p.Age);var light=splatUp;
            Color Tone(int hex,float k,float a){var s=NativeArena.ColorOf(hex);return new Color(s.r*k*light.r,s.g*k*light.g,s.b*k*light.b,a);}
            var layer=draw.Effects;var uv=new Vector4(0,0,NativeFxDraw.Flat,NativeFxDraw.Authored);
            for(int pass=0;pass<2;pass++)
            {
                float r=p.Radius*(pass==0?1:.62f);var inner=pass==0?Tone(NativeHordeGore.BloodWet,dry*.9f,p.Alpha*.95f):Tone(NativeHordeGore.BloodWet,dry*(1.15f+.35f*wet),p.Alpha*(.35f+.4f*wet));
                var outer=pass==0?Tone(NativeHordeGore.BloodDark,dry,p.Alpha*.92f):Tone(NativeHordeGore.BloodWet,dry*1.1f,0);
                for(int k=0;k<NativeHordeGore.PoolLobes;k++){float a=k*6.2831853f/NativeHordeGore.PoolLobes,l=NativeHordeGore.PoolLobe(p.Seed^(uint)pass,k,p.Age);gorePoolRing[k]=c+new Vector3(Mathf.Cos(a)*r*l,.0006f*pass,Mathf.Sin(a)*r*l);}
                int s0=layer.Vertex(c+Vector3.up*(.0006f*pass+.0003f),Vector3.up,inner,uv,Vector4.zero,Vector4.zero);
                for(int k=0;k<NativeHordeGore.PoolLobes;k++)layer.Vertex(gorePoolRing[k],Vector3.up,outer,uv,Vector4.zero,Vector4.zero);
                for(int k=0;k<NativeHordeGore.PoolLobes;k++)layer.Triangle(s0,s0+1+k,s0+1+(k+1)%NativeHordeGore.PoolLobes);
            }
        }
        /// <summary>A new arena, generation or rewind (ClearHordeKinds): every wound, corpse, chunk and pool goes.</summary>
        private void ClearHordeGore(){hordeGore.Clear();goreJobCount=0;goreHitTarget=-1;ClearGoreGallery();}
    }
}
