using System;
using System.IO;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>Opt-in save adapter; not wired into current Siren/PvP menus.</summary>
    public static class NativeExpeditionPersistence
    {
        public static NativeExpeditionSaveStore Open(string run)
        {
            return new NativeExpeditionSaveStore(Path.Combine(ConversionGame.NativeUserDataPath,"Expeditions"),run,
                save=>JsonUtility.ToJson(save),json=>
                {
                    try{return JsonUtility.FromJson<ExpeditionSavepoint>(json);}
                    catch(ArgumentException){return null;}
                });
        }
    }
}
