using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // Pilot rig dressing on the source models (renderer.ts:83,128-129,155 at 05950ea): the body chips (Core/NativeBodyChips.cs),
    // the right-hand spell charge and the element hand motes (Core/NativeSpellCharge.cs), and the block-cooldown dimming with the
    // Ansatsuken parry flash (Core/NativeBlockTint.cs). Arena rigs (host, guests and the killcam replay alike) and the first-person
    // rig wear them; the entrance and sandbox viewer rigs stay bare. Presentation only: everything reads the replicated actor
    // (block clocks, cast stamps, fighter action, roll and wavedash), the seat chips and the stats every client recomputes, and
    // the Block events every client already feeds the captured-melee impacts; nothing reaches the authority or the network frame.
    public sealed partial class ArenaPresentation
    {
        private readonly BodyChip[] bodyChips=new BodyChip[NativeBodyChips.MaxPieces];private readonly SpellMote[] handMotes=new SpellMote[8];
        private readonly Dictionary<int,NativeWaveDash> chipWaves=new Dictionary<int,NativeWaveDash>();private readonly Dictionary<int,NativeSpellMotes> moteClocks=new Dictionary<int,NativeSpellMotes>();
        private readonly Dictionary<int,string> impactIds=new Dictionary<int,string>();
        // This rig's block-cooldown factor and parry flash, set by RigTint before the rig's parts are queued.
        private float rigFactor=1,rigFlash;private Color rigTintColor=Color.white;private Func<int,Color> rigTint;
        private Func<int,Matrix4x4> flashSource,flashShell;
        // The flash overlay sits a hair outside the rig's own surfaces so it never depth-fights them.
        private static readonly Matrix4x4 FlashShell=Matrix4x4.Scale(Vector3.one*1.003f);
        private string ImpactId(int id){if(!impactIds.TryGetValue(id,out var s)){s=id.ToString();impactIds.Add(id,s);}return s;}
        // The Ansatsuken's parry (arcade parryAt): its Block event, which NativeChips emits from the parrying seat.
        private float ParryAt(Actor a,float time){if(a.SourceSpec!="ansatsuken")return -100;float at=meleeImpacts.BlockAt(ImpactId(a.Id),time,NativeBlockTint.FlashSeconds);return float.IsNaN(at)?-100:at;}
        /// <summary>
        /// block-cooldown-tint.ts: the per-part scene tint every rig part (body, hat, weapon) is drawn with this frame, combining a
        /// burning Last Laugh victim's char (SirenCharTint) with the block-cooldown dimming; null when neither applies. Sets
        /// rigFactor (chip modules and the muzzle flash dim with the rig) and rigFlash. Allocation-free.
        /// </summary>
        private Func<int,Color> RigTint(Actor a,float time,bool first)
        {
            rigFlash=NativeBlockTint.Flash(a.Alive,ParryAt(a,time),time);
            rigFactor=NativeBlockTint.Factor(a.Alive,NativeBlockTint.Cooldown(a,time),NativeBlockTint.Blocking(a,time),rigFlash,time);
            var siren=first?null:SirenCharTint(a.Id);if(rigFactor>=1&&siren==null)return null;
            var c=siren!=null?siren(0):Color.white;rigTintColor=new Color(c.r*rigFactor,c.g*rigFactor,c.b*rigFactor,1);return rigTint??(rigTint=_=>rigTintColor);
        }
        private void ClearRigTint(){rigFactor=1;rigFlash=0;}
        /// <summary>
        /// The parry flash: the source pulls every rig colour toward 0x9fe9ff and adds a 0x63cfff glow over .22 s; the native rig
        /// draws its own silhouette over itself in NativeBlockTint.OverlayColor at FlashLerp opacity (the Demon Step ghost shader),
        /// which gives the same colour on a part under unit light. Body, gloves and hat, on the rig's layer and camera.
        /// </summary>
        private void ParryFlash(CapturedAnimation.Player rig,Actor a,int layer,Camera camera,Func<int,Matrix4x4> bone,Func<int,bool> visible,Func<int,Matrix4x4> weaponBone,bool first)
        {
            float k=NativeBlockTint.FlashLerp(rigFlash);if(!(k>0)||sourceModels==null)return;
            if(!ghostsLoaded){ghostsLoaded=true;ghostPilot=sourceModels.Ghost("pilot");ghostGloves=sourceModels.Ghost("weapon-ansatsuken");}if(ghostPilot==null)return;
            var c=NativeBlockTint.OverlayColor(rigFlash);var color=new Color(c.X,c.Y,c.Z,1);var root=rig.Root.transform.localToWorldMatrix;
            if(flashShell==null)flashShell=b=>flashSource(b)*FlashShell;
            flashSource=bone;sourceModels.Queue(ghostPilot,root,flashShell,visible,layer,camera,color,null,k);
            if(weaponBone!=null&&ghostGloves!=null){flashSource=weaponBone;sourceModels.Queue(ghostGloves,root,flashShell,null,layer,camera,color,null,k);}
            if(!first&&!string.IsNullOrEmpty(a.Hat)&&a.Hat!="auditor-halo")
            {
                if(!ghostHats.TryGetValue(a.Hat,out var hat)){hat=sourceModels.Ghost("hat-"+a.Hat);ghostHats[a.Hat]=hat;}
                if(hat!=null){flashSource=bone;sourceModels.Queue(hat,root,flashShell,null,layer,camera,color,null,k);}
            }
            flashSource=null;
        }
        /// <summary>
        /// bodyModifiers for one rig (after its model is queued): the spell charge in the right hand, the hand motes (third-person
        /// rigs; the first-person view takes them from the hidden body, HiddenSweep), and the body chips on their joints (the
        /// first-person rig keeps only the chest's).
        /// </summary>
        private void BodyDressing(CapturedAnimation.Player rig,Actor a,float time,bool first,int layer,Camera camera)
        {
            var pilot=LoadoutPilot(a.Id);int color=pilot?.Weapon!=null?(int)pilot.Weapon.color:-1;
            bool charging=NativeSpellCharge.Charging(a,time,out float charge);var pose=NativeSpellCharge.Sample(a,time,charging,charge);
            if(pose.Visible&&color>=0)
            {
                var socket=rig.Bones[CapturedRig.RHand].localToWorldMatrix*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(NativeSpellCharge.Socket)),CapturedAnimation.Quat(CapturedRig.Mirror(pose.Rotation)),Vector3.one*pose.Scale);
                QueuePiece(new WeaponPiece{Shape=WeaponShape.Sphere,Glow=true,Color=color,Scale=new System.Numerics.Vector3(NativeSpellCharge.OrbScale),Rotation=System.Numerics.Quaternion.Identity},socket,layer,camera);
                QueuePiece(new WeaponPiece{Shape=WeaponShape.Ring,Glow=true,Color=color,Scale=NativeSpellCharge.HaloScale,Rotation=pose.Halo0},socket,layer,camera);
                QueuePiece(new WeaponPiece{Shape=WeaponShape.Ring,Glow=true,Color=color,Scale=NativeSpellCharge.HaloScale,Rotation=pose.Halo1},socket,layer,camera);
                // Converging streaks and shrinking rings round the orb (NativeChargeFx, weapons milestone).
                ChargeFx(socket,a,color,time,first);
            }
            if(!first)HandMotes(rig,a,time,color);
            if(pilot?.Stats==null||pilot.Seat?.chips==null)return;int n=NativeBodyChips.Build(pilot.Seat.chips,pilot.Stats,bodyChips);if(n==0)return;
            if(!chipWaves.TryGetValue(a.Id,out var wave)){wave=new NativeWaveDash();chipWaves.Add(a.Id,wave);}float dash=NativeBodyChips.DashTime(a,wave,time);
            for(int i=0;i<n;i++)
            {
                var c=bodyChips[i];if(first&&!NativeBodyChips.FirstPerson(c.Bone))continue;NativeBodyChips.Animate(ref c,time,dash);
                if(c.Visible)QueuePiece(c.Piece,rig.Bones[c.Bone].localToWorldMatrix,layer,camera);
            }
        }
        /// <summary>spellMotes: element sparks from the casting hand (renderer.ts:128 spellBurst), at the source effects quality.</summary>
        private void HandMotes(CapturedAnimation.Player rig,Actor a,float time,int color)
        {
            if(MagicPose.School(a)==0||color<0)return;
            if(!moteClocks.TryGetValue(a.Id,out var clock)){clock=new NativeSpellMotes();moteClocks.Add(a.Id,clock);}
            int n=clock.Step(a,time,Time.unscaledDeltaTime,BlastQuality,color,handMotes);if(n==0)return;var hand=rig.Bones[CapturedRig.RHand].localToWorldMatrix;
            for(int i=0;i<n;i++){var m=handMotes[i];SourceBurst(hand.MultiplyPoint3x4(CapturedAnimation.Vec(CapturedRig.Mirror(m.Local)))+Vector3.one*m.Jitter,m.Color,m.Count,m.Power);}
        }
        private void ClearRigDressing(){chipWaves.Clear();foreach(var clock in moteClocks.Values)clock.Reset();ClearRigTint();}
    }
}
