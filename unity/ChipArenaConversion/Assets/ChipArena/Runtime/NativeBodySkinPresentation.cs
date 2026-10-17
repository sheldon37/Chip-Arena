using UnityEngine;
namespace ChipArena.UnityConversion
{
    // Which body each pilot draw uses (Core/NativeBodySkins.cs): the pilot's own pick from its seat (the "body" entry of
    // seat.weaponSkins, replicated in the seat frames: live, guest replica and killcam replay alike), the viewer's map for
    // viewer rigs, Standard when absent; the Sheldon Intro dresses its pilot in the Sheldon Clothes. The detail tier follows
    // the weapon skins' rule (graphics tier, Reduced effects, distance from the camera; first person keeps Full). Every id is
    // a cached string and every derived model (ghost, afterimage, hologram, entrance ghost) is built once, so choosing a body
    // allocates nothing. Presentation only: no hit volume, collider, timing, rule or network field reads it.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The body skin pilot `id` wears.</summary>
        private int BodySkin(int id)=>NativeBodySkins.Of(SeatWeaponSkins(id));
        /// <summary>The detail tier of a body at `at` (first person: Full, or Reduced on the low graphics tier).</summary>
        private int BodyTier(Vector3 at,bool first)
        {
            float distance=0;var eye=WorldEye;if(!first&&eye!=null)distance=Vector3.Distance(eye.transform.position,at);
            return NativeBodySkins.Tier(LowGraphics,WeaponEffectsQuality,distance,first);
        }
        /// <summary>The close-up tier (entrances, portraits, the chip draft): Full unless Reduced effects or the low graphics tier.</summary>
        private int BodyCloseTier=>NativeBodySkins.Tier(LowGraphics,WeaponEffectsQuality,0,false);
        /// <summary>The bank model of a skin at a tier; the Prototype when a body skin's models are absent.</summary>
        private string BodyModel(int skin,int tier)
        {
            var id=NativeBodySkins.Model(skin,tier);return sourceModels!=null&&sourceModels.Has(id)?id:NativeBodySkins.PrototypeModel;
        }
        /// <summary>The body a posed rig draws (SourceCharacter): the pilot's skin at the rig's tier.</summary>
        private string RigBody(CapturedAnimation.Player rig,Actor a,bool first,out int skin)
        {
            skin=sheldonScripted?NativeBodySkins.SheldonIndex:a==null?NativeBodySkins.StandardIndex:BodySkin(a.Id);
            int tier=rig==introRig||rig?.Root==null?BodyCloseTier:BodyTier(rig.Root.transform.position,first);
            return BodyModel(skin,tier);
        }
        /// <summary>A body at a world position (corpses, death scenes, revives, vent falls): pilot `id`'s skin at that distance.</summary>
        private string BodyAt(int id,Vector3 at,out int skin){skin=BodySkin(id);return BodyModel(skin,BodyTier(at,false));}
        /// <summary>
        /// The accent tint a body draws with: the pilot's accent, or for the Sheldon Clothes the Sheldon Head's glow (the hat
        /// colour, cyan on Auto when the hood is worn; else a picked hat colour, else the accent: NativeBodySkins.SheldonGlow).
        /// </summary>
        private static Color BodyAccent(int skin,string accent,string hat,string hatColor)=>AccentTint(skin==NativeBodySkins.SheldonIndex?NativeBodySkins.SheldonGlow(hat,hatColor,accent):accent);

        // ---- derived models, built once per body and bank ----
        private NativeSourceModels bodyBank;private readonly string[] bodyGhosts=new string[NativeBodySkins.Count*NativeBodySkins.Tiers],bodyAfterimages=new string[NativeBodySkins.Count*NativeBodySkins.Tiers];
        private void BodyBank(){if(bodyBank==sourceModels)return;bodyBank=sourceModels;System.Array.Clear(bodyGhosts,0,bodyGhosts.Length);System.Array.Clear(bodyAfterimages,0,bodyAfterimages.Length);}
        /// <summary>The Demon Step / dash ghost of a body (NativeSourceModels.Ghost); null without the ghost shader.</summary>
        private string BodyGhost(int skin,int tier)
        {
            if(sourceModels==null)return null;BodyBank();int k=Mathf.Clamp(skin,0,NativeBodySkins.Count-1)*NativeBodySkins.Tiers+Mathf.Clamp(tier,0,NativeBodySkins.Tiers-1);
            if(bodyGhosts[k]==null)bodyGhosts[k]=sourceModels.Ghost(BodyModel(skin,tier))??"";return bodyGhosts[k].Length==0?null:bodyGhosts[k];
        }
        /// <summary>The roll afterimage of a body (NativeSourceModels.Afterimage); null without the afterimage shader.</summary>
        private string BodyAfterimage(int skin,int tier)
        {
            if(sourceModels==null)return null;BodyBank();int k=Mathf.Clamp(skin,0,NativeBodySkins.Count-1)*NativeBodySkins.Tiers+Mathf.Clamp(tier,0,NativeBodySkins.Tiers-1);
            if(bodyAfterimages[k]==null)bodyAfterimages[k]=sourceModels.Afterimage(BodyModel(skin,tier))??"";return bodyAfterimages[k].Length==0?null:bodyAfterimages[k];
        }
    }
}
