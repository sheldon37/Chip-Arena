using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    // C11 weapon presentation (client/weapon-visuals.ts, client/melee-trails.ts at 05950ea): the melee reach stretch, chip
    // weapon modules, fire and poison blade enchants (also on thrown daggers and shields), the gun muzzle flash and the swept
    // melee ribbons. Presentation only: everything reads replicated actors, seat chips and the stats every client recomputes
    // from them; hit timing, hitboxes, damage, cooldowns, movement and the network frame are untouched.
    public sealed partial class ArenaPresentation
    {
        // Set before Render: the arena whose pilots carry the chips and stats (live, guest replica or killcam replay), and
        // the source's effectsQuality (1 full, .3 with Reduced effects; renderer.ts, main.ts:245).
        public NativeArena WeaponLoadout;public float WeaponEffectsQuality=1;
        private readonly WeaponPiece[] weaponPieces=new WeaponPiece[NativeWeaponDressing.MaxPieces];
        private readonly NativeMeleeSweeps meleeSweeps=new NativeMeleeSweeps();private NativeMeleeRibbons meleeRibbons;
        private NativeArena sweepArena;private int sweepGeneration=-1;
        private NativePilot LoadoutPilot(int id)
        {var arena=WeaponLoadout;if(arena==null)return null;var pilots=arena.Pilots;for(int i=0;i<pilots.Count;i++)if(pilots[i].Seat!=null&&pilots[i].Seat.id==id)return pilots[i];return null;}
        // character-animation.ts:57: every melee weapon (and the off-hand) is scaled along its +z by stats.reach, so the blade,
        // its modules, enchant and blade tip stretch together (fists only where the source skips its fighter layers). The
        // intro, draft and sandbox viewer rigs stay bare.
        private float WeaponReach(CapturedAnimation.Player rig,Actor a,float time)
        {
            if(Viewer||rig==introRig||!NativeWeaponDressing.Melee(a.SourceSpec))return 1;var stats=LoadoutPilot(a.Id)?.Stats;
            return NativeWeaponDressing.ReachScale(a.SourceSpec,stats!=null?(float)stats["reach"]:1,a.StunUntil>time||NativeJugglePose.Active(a.JuggleUntil,time)||a.Rolling(time)||NativeSpecialPose.Down(a,time));
        }
        // Before a rig is posed: the captured-melee layers measure the same stretched blade tip, blade end, pommel and support
        // grip the renderer draws (the source scales r.weapon before meleeMotion and the floor guard read its sockets).
        private void PoseReach(CapturedAnimation.Player rig,Actor a,float time)=>rig.WeaponReach=WeaponReach(rig,a,time);
        // The frame the baked 'weapon-'+spec parts are queued in (guns: the hand; staffs and leet: the staff grip; the sword
        // family: the held melee hand), with the reach stretch for melee.
        private Matrix4x4 SourceWeaponBone(CapturedAnimation.Player rig,Actor a,float time,bool first,int bone,float reach)
        {
            if(a.MagicVisual>0||a.SourceSpec=="leet")return StaffMatrix(rig,first);
            var m=a.Weapon==WeaponId.Sword&&!NativeWeaponDressing.Fist(a.SourceSpec)?MeleeHand(rig,a,time,bone==CapturedRig.LHand):rig.Bones[bone].localToWorldMatrix;
            return reach!=1?m*Matrix4x4.Scale(new Vector3(1,1,reach)):m;
        }
        // `dim`: the block-cooldown factor of the rig the piece belongs to (block-cooldown-tint.ts tints the weapon group too).
        private void QueuePiece(in WeaponPiece p,Matrix4x4 parent,int layer,Camera camera,float dim=1)
        {
            var matrix=parent*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(p.Position)),CapturedAnimation.Quat(CapturedRig.Mirror(p.Rotation)),CapturedAnimation.Vec(p.Scale));
            var color=NativeArena.ColorOf(p.Color).linear;if(dim!=1)color=new Color(color.r*dim,color.g*dim,color.b*dim,color.a);
            sourceModels.QueuePiece(p.Shape,p.Glow,matrix,color,layer,camera);
        }
        // After the weapon model is queued (SourceCharacter): the muzzle flash, the chip modules (both daggers), the blade
        // enchant (both daggers) and, in third person, this frame's blade sample. First person draws on the view weapon:
        // modules at half size and hidden past half ADS (first-person-view.ts:89), enchant and flash unscaled. The flash and the
        // modules dim with the rig's block cooldown (rigFactor); the enchant is exempt (block-cooldown-tint.ts:15).
        // Owner 2026-10-11 effects overhaul (weapons milestone): the source's single glow cone (NativeWeaponDressing.MuzzleFlash,
        // still the pinned reference for when a shot flashes) is drawn as an ETFX-style muzzle star instead (MuzzleFx,
        // NativeWeaponFxPresentation.cs), on the same socket, layer and camera.
        private void WeaponDressing(CapturedAnimation.Player rig,Actor a,float time,bool first,int layer,Camera camera,float reach)
        {
            if(sourceModels==null||Viewer||rig==introRig)return;string spec=a.SourceSpec;
            MuzzleRecord(rig,a,first);MuzzleFx(rig,a,time,first);BladeBlood(rig,a,time,first,layer,camera,reach);
            var pilot=LoadoutPilot(a.Id);
            if(!first)SampleSweep(rig,a,time,reach,pilot?.Seat.chips);
            if(pilot?.Stats==null||pilot.Weapon==null||pilot.Seat.chips==null)return;var chips=pilot.Seat.chips;var stats=pilot.Stats;bool daggers=spec=="daggers";
            // A Standard weapon carries the designed modules and enchant (NativeChipModulePresentation.cs); the Prototype Series keeps the pieces below.
            if(StandardChips(a)){StandardDressing(rig,a,time,first,layer,camera,reach,pilot);return;}
            if(!first||aimMotion.Weight<=.5f)
            {
                int count=NativeWeaponDressing.Modules(spec,pilot.Weapon.@class=="ranged",(int)pilot.Weapon.color,chips,stats,weaponPieces);
                if(count>0)for(int hand=0;hand<(daggers?2:1);hand++)
                {
                    var parent=SourceWeaponBone(rig,a,time,first,hand==0?CapturedRig.RHand:CapturedRig.LHand,reach);if(first)parent*=Matrix4x4.Scale(Vector3.one*NativeWeaponDressing.FirstPersonModuleScale);
                    for(int i=0;i<count;i++){var p=weaponPieces[i];NativeWeaponDressing.Animate(ref p,time);QueuePiece(p,first?parent:SkinPiece(a,p,parent),layer,camera,rigFactor);}
                }
            }
            // renderer.ts:83-84,129: elements only on melee-class weapons, from the burn and poison stats; stacks from the chips.
            if(!NativeWeaponDressing.Melee(spec))return;bool fire=stats["burn"]>0,poison=stats["poison"]>0;if(!fire&&!poison)return;
            chips.TryGetValue("ember",out int ember);chips.TryGetValue("poison",out int motes);int flames=NativeWeaponDressing.Enchant(spec,fire,poison,ember,motes,weaponPieces,0);
            if(flames>0)for(int hand=0;hand<(daggers?2:1);hand++)
            {
                var parent=SourceWeaponBone(rig,a,time,first,hand==0?CapturedRig.RHand:CapturedRig.LHand,reach);
                for(int i=0;i<flames;i++){var p=weaponPieces[i];NativeWeaponDressing.Animate(ref p,time);QueuePiece(p,SkinPiece(a,p,parent),layer,camera);}
            }
        }
        // A blade that drew blood carries it on its edge (Core/NativeBlood: the ledger's melee hits by this pilot, fading over
        // ten seconds; the ledger and so the blood clear with the round), both daggers, third and first person. The pieces lie on
        // the drawn blade of the skin the pilot shows (Core/NativeBladeProfile: the Standard bevels or the Prototype edge strips;
        // the Standard left dagger is built mirrored) and stretch with it under reach (SkinPiece).
        private void BladeBlood(CapturedAnimation.Player rig,Actor a,float time,bool first,int layer,Camera camera,float reach)
        {
            string spec=a.SourceSpec;if(!NativeBladeProfile.Bladed(spec)||a.WeaponAway)return;float amount=hitLedger.BladeBlood(a.Id,time);if(amount<.02f)return;
            bool standard=WeaponSkin(a)==NativeWeaponSkins.StandardIndex&&NativeWeaponModels.Designed(spec);
            for(int hand=0;hand<(spec=="daggers"?2:1);hand++)
            {
                if(hand==0&&a.MainWeaponAway&&spec=="daggers")continue;// the thrown dagger's hand is empty (NativeSourcePresentation)
                var parent=SourceWeaponBone(rig,a,time,first,hand==0?CapturedRig.RHand:CapturedRig.LHand,reach);
                int count=NativeBladeBlood.Pieces(spec,standard,amount,hand==1&&standard&&NativeWeaponModels.Get(spec).MirrorLeft,weaponPieces);
                for(int i=0;i<count;i++){var piece=weaponPieces[i];QueuePiece(piece,SkinPiece(a,piece,parent),layer,camera);}
            }
        }
        // standard-melee-effects.ts:21: a thrown dagger or shield keeps the owner's enchant (elements and stacks), flying with it.
        private void ThrownEnchant(NativeMeleeShot shot,Matrix4x4 matrix,float time)
        {
            var owner=shot.Original;if(sourceModels==null||owner?.Stats==null||owner.Seat?.chips==null)return;bool fire=owner.Stats["burn"]>0,poison=owner.Stats["poison"]>0;if(!fire&&!poison)return;
            owner.Seat.chips.TryGetValue("ember",out int ember);owner.Seat.chips.TryGetValue("poison",out int motes);if(ThrownStandardEnchant(shot,matrix,time,fire,poison,ember,motes))return;
            int count=NativeWeaponDressing.Enchant(shot.Shield?"shield":"daggers",fire,poison,ember,motes,weaponPieces,0);
            for(int i=0;i<count;i++){var p=weaponPieces[i];NativeWeaponDressing.Animate(ref p,time);QueuePiece(p,matrix,0,null);}
        }
        // melee-trails.ts:33-36,44: the swinging blade's grip and blade-tip socket in world space; even dagger strikes swing the
        // off-hand blade (Combo+1 is the attack index MeleeMotion animates).
        private void SampleSweep(CapturedAnimation.Player rig,Actor a,float time,float reach,System.Collections.Generic.IReadOnlyDictionary<string,int> chips)
        {
            // A standard melee strike draws its slash crescent instead (NativeSlashFx, owner 2026-10-11 weapons milestone), swept
            // from this same blade (Core/NativeSlashTrail).
            if(NativeSlashFx.Replaces(a,time)){SlashSample(rig,a,time,reach);return;}
            if(!meleeSweeps.Enabled||a.WeaponAway||!NativeWeaponDressing.BladeTip(a.SourceSpec,out var tip)||!NativeWeaponDressing.Sweeping(a,time))return;
            int hand=a.SourceSpec=="daggers"&&(a.Combo+1)%2==0?CapturedRig.LHand:CapturedRig.RHand;var m=SourceWeaponBone(rig,a,time,false,hand,reach);
            meleeSweeps.Add(SweepKey.Of(a),a.SourceSpec,NativeWeaponDressing.SweepColor(chips),CapturedAnimation.Num(m.MultiplyPoint3x4(Vector3.zero)),CapturedAnimation.Num(m.MultiplyPoint3x4(CapturedAnimation.Vec(CapturedRig.Mirror(tip)))),time);
        }
        // The source keeps posing the local pilot's hidden body rig in first person, so its own world sweep and hand motes still
        // show in front of the camera (renderer.ts:122-128; melee-trails.ts:34 checks the weapon, not the body; spellMotes reads
        // the body's right hand). The rig is posed (only while it sweeps or casts) but never queued.
        private void HiddenSweep(Actor a,Vector3 position,float time,bool posed=false)
        {
            if(sourceModels==null||Viewer)return;
            bool sweep=meleeSweeps.Enabled&&!a.WeaponAway&&NativeWeaponDressing.BladeTip(a.SourceSpec,out _),motes=MagicPose.School(a)>0;
            if(motes&&!NativeSpellMotes.Casting(a,time)){if(moteClocks.TryGetValue(a.Id,out var clock))clock.Reset();motes=false;}
            if(!sweep&&!motes)return;
            if(!rigs.TryGetValue(a.Id,out var rig)){rig=captured.Create(transform,a.Id);rig.Impacts=meleeImpacts;rigs.Add(a.Id,rig);}
            if(!posed){PoseReach(rig,a,time);rig.ActorPose(a,position,time);}var pilot=LoadoutPilot(a.Id);
            if(sweep)SampleSweep(rig,a,time,rig.WeaponReach,pilot?.Seat.chips);
            if(motes)HandMotes(rig,a,time,pilot?.Weapon!=null?(int)pilot.Weapon.color:-1);
        }
        // melee-trails.ts:67-102 once per frame after every blade sample: prune, release and draw the additive ribbons.
        private void RenderMeleeTrails(float time)
        {
            if(sourceModels==null)return;int generation=WeaponLoadout?.Generation??-1;
            if(!ReferenceEquals(sweepArena,WeaponLoadout)||generation!=sweepGeneration){meleeSweeps.Clear();sweepArena=WeaponLoadout;sweepGeneration=generation;}
            WeaponFxFrame(time);
            // The slash pack's rings, lines and claws on finishers and special moves (NativePackSlashPresentation.cs).
            PackSlashFx(time);
            meleeSweeps.Update(time,TrailsEnabled,WeaponEffectsQuality);
            if(meleeSweeps.Count==0)return;if(meleeRibbons==null)meleeRibbons=new NativeMeleeRibbons();meleeRibbons.Draw(meleeSweeps,time);
        }
        private void ClearWeaponPresentation(){meleeSweeps.Clear();sweepArena=null;sweepGeneration=-1;ClearWeaponFx();ClearPackSlashes();}
        private void DisposeWeaponPresentation(){ClearWeaponPresentation();meleeRibbons?.Dispose();meleeRibbons=null;WeaponLoadout=null;DisposeWeaponFx();}
    }
    // The ribbon meshes of every live sweep in one dynamic mesh: additive, unlit, double-sided, depth-tested, no depth write,
    // never culled (melee-trails.ts:87: MeshBasicMaterial opacity .7, vertexColors, AdditiveBlending, toneMapped false).
    // NativeGlow's ribbon mode at uv (0,0,1) passes the vertex alpha through, so each vertex adds colour * scalar * .7.
    internal sealed class NativeMeleeRibbons : IDisposable
    {
        private const int Vertices=NativeMeleeSweeps.MaxSweeps*NativeMeleeSweeps.MaxSamples*2;
        private readonly Mesh mesh=new Mesh{name="Native melee sweeps"};private readonly Material material;
        private readonly System.Numerics.Vector3[] points=new System.Numerics.Vector3[Vertices];private readonly float[] scalars=new float[Vertices];
        private readonly Vector3[] vertices=new Vector3[Vertices],uv=new Vector3[Vertices];private readonly Color[] colors=new Color[Vertices];
        private readonly int[] indices=new int[NativeMeleeSweeps.MaxSweeps*(NativeMeleeSweeps.MaxSamples-1)*6];
        public NativeMeleeRibbons()
        {
            var shader=Resources.Load<Shader>("NativeGlow");if(shader==null)throw new InvalidOperationException("NativeGlow shader is missing");
            material=new Material(shader){name="Native melee sweep"};mesh.MarkDynamic();for(int i=0;i<Vertices;i++)uv[i]=new Vector3(0,0,1);
        }
        public void Draw(NativeMeleeSweeps sweeps,float time)
        {
            int count=0,index=0;
            for(int s=0;s<sweeps.Count;s++)
            {
                var sweep=sweeps[s];int n=NativeMeleeSweeps.Build(sweep,time,points,scalars,count);if(n<2)continue;Color tint=NativeArena.ColorOf(sweep.Color).linear;
                for(int i=0;i<n*2;i++){var p=points[count+i];float k=scalars[count+i];vertices[count+i]=new Vector3(p.X,p.Y,p.Z);colors[count+i]=new Color(tint.r*k,tint.g*k,tint.b*k,.7f);}
                for(int i=0;i<n-1;i++){int a=count+i*2;indices[index++]=a;indices[index++]=a+1;indices[index++]=a+2;indices[index++]=a+2;indices[index++]=a+1;indices[index++]=a+3;}
                count+=n*2;
            }
            mesh.Clear();if(index==0)return;
            mesh.SetVertices(vertices,0,count);mesh.SetColors(colors,0,count);mesh.SetUVs(0,uv,0,count);mesh.SetIndices(indices,0,index,MeshTopology.Triangles,0,false);
            mesh.bounds=new Bounds(Vector3.zero,Vector3.one*10000);
            Graphics.DrawMesh(mesh,Matrix4x4.identity,material,0,null,0,null,ShadowCastingMode.Off,false);
        }
        public void Dispose(){CapturedAnimation.Release(mesh);CapturedAnimation.Release(material);}
    }
}
