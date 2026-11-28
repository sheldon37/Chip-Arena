using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Intros v2 in the Locker (owner 2026-11-20: archive the old intros "so theyre no longer visible in the customization screen"): the
    // gated list loses every archived id (Core NativeIntroCatalog.Archived: all but the Top Dog's and the Bombadier's) and gains the
    // registered v2 intros this access may pick (DEV ones only with live DEV access). The weapon intros show their weapon's icon.
    public sealed partial class ConversionGame
    {
        private void LockerIntrosV2(List<string> intros,NativeAccess access)
        {
            for(int i=intros.Count-1;i>=0;i--)if(NativeIntroCatalog.Archived(intros[i]))intros.RemoveAt(i);
            NativeIntroCatalog.Listed(intros,access,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            foreach(var id in intros){var spec=NativeIntroCatalog.Find(id)?.Spec;if(spec!=null&&!lockerIntroWeapon.ContainsKey(id)){var w=LockerWeaponById(spec);if(w!=null)lockerIntroWeapon[id]=w;}}
        }
    }
}
