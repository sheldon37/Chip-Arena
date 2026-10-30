using UnityEngine;
namespace ChipArena.UnityConversion
{
    // Which weapon model each draw uses (Core/NativeWeaponSkins.cs): the pilot's own pick for its weapon from its seat
    // (seat.weaponSkins, replicated in the seat frames), the Locker's preview map for viewer rigs, Standard when absent. Held
    // Standard draws pick their detail tier from the graphics tier, Reduced effects and the rig's distance from the camera.
    // Every id is a cached string: choosing a model allocates nothing.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The skin map the sandbox / preview viewer rigs show (prefs.weaponSkins; null: every weapon Standard).</summary>
        public string ViewerWeaponSkins;
        /// <summary>The pilot's skin map: the seat of the arena the pilots come from (live, guest replica or killcam replay).</summary>
        private const string RooftopCrowdSkins="face:none,suit:none";
        private string SeatWeaponSkins(int id)
        {
            // A model portrait's body wears the card's map (Runtime/NativeVisualPortraitPresentation.cs).
            if(ModelPortraitOf(id,out var portrait))return portrait.Skins;
            // The rooftop's seated pilot is yours (Runtime/NativeRooftopPresentation.cs RooftopModel); its crowd the Standard, in
            // their own faces and clothes (no default helmet and suit).
            if(id==RooftopIdTop)return rooftopSkins;if(id<RooftopIdTop&&id>RooftopIdTop-8)return RooftopCrowdSkins;
            if(Viewer)return ViewerWeaponSkins;var seats=WeaponLoadout?.Match?.Seats;if(seats==null)return null;
            for(int i=0;i<seats.Count;i++)if(seats[i]!=null&&seats[i].id==id)return seats[i].weaponSkins;return null;
        }
        private int WeaponSkin(Actor a)=>a==null?NativeWeaponSkins.StandardIndex:NativeWeaponSkins.Index(SeatWeaponSkins(a.Id),a.SourceSpec);
        /// <summary>A Prototype model id ("weapon-"/"loose-" + spec) in `a`'s skin, Full detail (ghosts, deaths, drops).</summary>
        private string Skinned(Actor a,string prototypeId)
        {
            if(sourceModels==null||prototypeId==null||a==null)return prototypeId;var id=NativeWeaponSkins.Model(prototypeId,WeaponSkin(a));return sourceModels.Has(id)?id:prototypeId;
        }
        private string SkinnedHeld(Actor a)=>a==null?null:Skinned(a,NativeWeaponSkins.PrototypeHeld(a.SourceSpec));
        /// <summary>The held weapon model of a posed rig, at the detail tier its view calls for.</summary>
        private string HeldWeaponModel(CapturedAnimation.Player rig,Actor a,bool first)
        {
            string prototype=NativeWeaponSkins.PrototypeHeld(a.SourceSpec);int skin=WeaponSkin(a);if(skin==NativeWeaponSkins.PrototypeIndex||sourceModels==null)return prototype;
            float distance=0;var eye=WorldEye;if(!first&&eye!=null&&rig?.Root!=null)distance=Vector3.Distance(eye.transform.position,rig.Root.transform.position);
            var id=NativeWeaponSkins.Model(prototype,skin,NativeWeaponSkins.Tier(LowGraphics,WeaponEffectsQuality,distance,first));
            return sourceModels.Has(id)?id:prototype;
        }
        /// <summary>A Standard held model gets the struck pilot's hit flash (the Prototype weapons never flashed).</summary>
        private static bool StandardModel(string id)=>id!=null&&id.StartsWith("weapon-std-",System.StringComparison.Ordinal);
        /// <summary>
        /// The frame a chip module, enchant flame or blade-blood piece rides on a Standard blade under the reach stretch: the
        /// weapon matrix carries the Prototype's z scale by reach, the Standard hilt keeps its length and its blade stretches
        /// from the guard (NativeWeaponModels.Stretch), so a piece ahead of the blade base follows the blade and one behind it
        /// the rigid hilt. Unchanged at reach 1, for the Prototype Series and for every unstretched model.
        /// </summary>
        private Matrix4x4 SkinPiece(Actor a,in WeaponPiece p,Matrix4x4 parent)
        {
            var d=NativeWeaponModels.Get(a?.SourceSpec);if(d==null||float.IsNaN(d.StretchBase)||WeaponSkin(a)!=NativeWeaponSkins.StandardIndex)return parent;
            float x=((Vector3)parent.GetColumn(0)).magnitude,z=((Vector3)parent.GetColumn(2)).magnitude;if(!(x>1e-6f))return parent;
            var kind=p.Position.Z>=d.StretchBase?WeaponStretch.Blade:WeaponStretch.Rigid;
            return NativeWeaponModels.Stretch(a.SourceSpec,kind,z/x,out float scale,out float offset)?parent*Matrix4x4.Translate(new Vector3(0,0,offset))*Matrix4x4.Scale(new Vector3(1,1,scale)):parent;
        }
        /// <summary>
        /// A thrown dagger or shield (Kunai Mark, Aegis Ricochet) drawn as its owner's Standard loose model; false when the
        /// owner shows the Prototype Series (the box drawing stays).
        /// </summary>
        private bool ThrownWeapon(NativeMeleeShot shot,Matrix4x4 matrix)
        {
            if(sourceModels==null||shot?.Original?.Seat==null)return false;string spec=shot.Shield?"shield":"daggers";
            if(NativeWeaponSkins.Index(shot.Original.Seat.weaponSkins,spec)==NativeWeaponSkins.PrototypeIndex)return false;
            var id=NativeWeaponSkins.StandardLoose(spec);if(id==null||!sourceModels.Has(id))return false;
            sourceModels.Queue(id,matrix,accent:AccentTint(shot.Original.Seat.accentColor));return true;
        }
    }
}
