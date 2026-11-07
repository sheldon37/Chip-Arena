using UnityEngine;
namespace ChipArena.UnityConversion
{
    // Chip modules on a Standard weapon (Core/NativeChipModules*.cs). When the pilot's skin for its weapon is Standard, the
    // designed attachments replace the source's primitive pieces; the Prototype Series keeps NativeWeaponPresentation's route
    // exactly. The same rules decide when and where they show: the arena (live, guest replica, killcam replay) and first person,
    // the view weapon hiding them past half ADS; the modules dim with the rig's block cooldown, the enchant does not. In first
    // person the attachments stay where they sit on the model (the Prototype's shrink to half size about the hand); their mounts
    // keep them out of the sight line (ChipModuleChecks). The bow draws through BowDressing (StandardBowDressing below): its
    // pieces ride the riser, the flexing limbs and the nocked arrow. Each draw uses the weapon's detail tier and a cached bank
    // id: no allocation per frame.
    public sealed partial class ArenaPresentation
    {
        private readonly ChipModule[] chipModules=new ChipModule[NativeChipModules.MaxModules];
        /// <summary>True when `a` shows the Standard modules (WeaponDressing then skips the Prototype pieces).</summary>
        private bool StandardChips(Actor a)=>a!=null&&sourceModels!=null&&NativeChipModules.Designed(a.SourceSpec)&&WeaponSkin(a)==NativeWeaponSkins.StandardIndex;
        /// <summary>The seat's chip kinds and the gun's module sockets for `a`'s Standard muzzle flash (Core/NativeMuzzleFlash.cs:
        /// the vents a module covers, the multishot pods); null and no kinds without a loadout.</summary>
        private ChipSockets MuzzleChips(Actor a,out ChipKinds chips)
        {
            var pilot=LoadoutPilot(a.Id);chips=default;if(pilot?.Stats==null||pilot.Seat?.chips==null)return null;
            chips=NativeChipModules.Kinds(pilot.Seat.chips,pilot.Stats);return NativeChipModules.Sockets(a.SourceSpec);
        }
        // The held weapon's detail tier (HeldWeaponModel's rule), so the modules thin out with it.
        private int ChipTier(CapturedAnimation.Player rig,bool first)
        {
            float distance=0;var eye=WorldEye;if(!first&&eye!=null&&rig?.Root!=null)distance=Vector3.Distance(eye.transform.position,rig.Root.transform.position);
            return NativeWeaponSkins.Tier(LowGraphics,WeaponEffectsQuality,distance,first);
        }
        private void StandardDressing(CapturedAnimation.Player rig,Actor a,float time,bool first,int layer,Camera camera,float reach,NativePilot pilot)
        {
            string spec=a.SourceSpec;var chips=pilot.Seat.chips;var stats=pilot.Stats;bool daggers=spec=="daggers";int tier=ChipTier(rig,first);
            if(!first||aimMotion.Weight<=.5f)
            {
                int count=NativeChipModules.Modules(spec,pilot.Weapon.@class=="ranged",(int)pilot.Weapon.color,chips,stats,chipModules);
                if(count>0)for(int hand=0;hand<(daggers?2:1);hand++){var parent=SourceWeaponBone(rig,a,time,first,hand==0?CapturedRig.RHand:CapturedRig.LHand,reach);for(int i=0;i<count;i++)QueueChip(spec,chipModules[i],parent,hand==1,tier,time,first,layer,camera,rigFactor);}
            }
            // renderer.ts:83-84,129: elements only on melee-class weapons, from the burn and poison stats; stacks from the chips.
            if(!NativeWeaponDressing.Melee(spec))return;bool fire=stats["burn"]>0,poison=stats["poison"]>0;if(!fire&&!poison)return;
            chips.TryGetValue("ember",out int ember);chips.TryGetValue("poison",out int motes);int flames=NativeChipModules.Enchant(spec,fire,poison,ember,motes,chipModules,0);
            for(int hand=0;hand<(daggers?2:1);hand++){var parent=SourceWeaponBone(rig,a,time,first,hand==0?CapturedRig.RHand:CapturedRig.LHand,reach);for(int i=0;i<flames;i++)QueueChip(spec,chipModules[i],parent,hand==1,tier,time,first,layer,camera,1);}
        }
        // One attachment: animated, mirrored onto the left dagger, framed by the reach stretch, tinted by its Chip colour.
        private void QueueChip(string spec,ChipModule p,Matrix4x4 parent,bool left,int tier,float time,bool first,int layer,Camera camera,float dim)
        {
            NativeChipModules.Animate(ref p,time);
            left&=NativeWeaponModels.Get(spec)?.MirrorLeft??false;if(left){p.Position.X=-p.Position.X;p.Rotation=NativeChipModules.Mirrored(p.Rotation);}
            var id=NativeChipModules.ModelId(spec,p.Part,p.Variant,tier,left&&NativeChipModules.HasLeft(spec,p.Part));if(id==null)return;
            float x=((Vector3)parent.GetColumn(0)).magnitude,z=((Vector3)parent.GetColumn(2)).magnitude;
            if(x>1e-6f&&NativeChipModules.Frame(spec,p,z/x,out float scale,out float offset))parent=parent*Matrix4x4.Translate(new Vector3(0,0,offset))*Matrix4x4.Scale(new Vector3(1,1,scale));
            var matrix=parent*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(p.Position)),CapturedAnimation.Quat(CapturedRig.Mirror(p.Rotation)),CapturedAnimation.Vec(p.Scale));
            var color=NativeArena.ColorOf(p.Color).linear;if(dim!=1)color=new Color(color.r*dim,color.g*dim,color.b*dim,color.a);
            sourceModels.Queue(id,matrix,null,null,layer,camera,body:color);
        }
        /// <summary>
        /// The Standard bow (called from BowDressing with the frame BowDraw just posed; false: the Prototype route): the designed
        /// modules on the bow hand, each on its live part (the riser; a limb, turned with its flex; the nocked arrow, moving with
        /// the pull, hidden while none is on the string), and the burn / poison enchant on the nocked arrow, as the Prototype's.
        /// The bow keeps its pieces in first person at their own size and place; no aim hides them (the bow has no ADS).
        /// </summary>
        private bool StandardBowDressing(Actor a,NativePilot pilot,in NativeBowPose.Frame f,in NativeBow.View v,float time,bool first,int layer,Camera camera)
        {
            if(!StandardChips(a))return false;string spec=NativeBowWire.Spec;var chips=pilot.Seat.chips;var stats=pilot.Stats;
            float distance=0;var eye=WorldEye;if(!first&&eye!=null)distance=Vector3.Distance(eye.transform.position,bowHand.GetColumn(3));int tier=NativeWeaponSkins.Tier(LowGraphics,WeaponEffectsQuality,distance,first);
            int count=NativeChipModules.Modules(spec,true,(int)pilot.Weapon.color,chips,stats,chipModules);
            for(int i=0;i<count;i++)QueueBowChip(chipModules[i],tier,time,f.Pull,v.Nocked,layer,camera,rigFactor);
            bool fire=stats["burn"]>0,poison=stats["poison"]>0;if(!fire&&!poison||!v.Nocked)return true;
            chips.TryGetValue("ember",out int ember);chips.TryGetValue("poison",out int motes);int flames=NativeChipModules.Enchant(spec,fire,poison,ember,motes,chipModules,0);
            for(int i=0;i<flames;i++)QueueBowChip(chipModules[i],tier,time,f.Pull,true,layer,camera,1);
            return true;
        }
        private void QueueBowChip(ChipModule p,int tier,float time,float pull,bool nocked,int layer,Camera camera,float dim)
        {
            NativeChipModules.Animate(ref p,time);if(!NativeChipModules.Ride(ref p,pull,nocked))return;
            var id=NativeChipModules.ModelId(NativeBowWire.Spec,p.Part,p.Variant,tier);if(id==null)return;
            var matrix=bowHand*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(p.Position)),CapturedAnimation.Quat(CapturedRig.Mirror(p.Rotation)),CapturedAnimation.Vec(p.Scale));
            var color=NativeArena.ColorOf(p.Color).linear;if(dim!=1)color=new Color(color.r*dim,color.g*dim,color.b*dim,color.a);
            sourceModels.Queue(id,matrix,null,null,layer,camera,body:color);
        }
        /// <summary>A thrown dagger or shield keeps its owner's Standard enchant on the Standard loose model (false: the Prototype route).</summary>
        private bool ThrownStandardEnchant(NativeMeleeShot shot,Matrix4x4 matrix,float time,bool fire,bool poison,int ember,int motes)
        {
            string spec=shot.Shield?"shield":"daggers";if(sourceModels==null||NativeWeaponSkins.Index(shot.Original.Seat.weaponSkins,spec)!=NativeWeaponSkins.StandardIndex||!NativeChipModules.Designed(spec))return false;
            int count=NativeChipModules.Enchant(spec,fire,poison,ember,motes,chipModules,0);for(int i=0;i<count;i++)QueueChip(spec,chipModules[i],matrix,false,0,time,false,0,null,1);return true;
        }
    }
}
