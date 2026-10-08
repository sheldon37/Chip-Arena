using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        // Presentation adapter only: Claude can replace this layout without changing NativeUpdater.
        public NativeUpdater Updater { get;private set; }
        private void InitializeUpdater()
        {
            if(dedicated)return;
            Updater=gameObject.AddComponent<NativeUpdater>();
            Updater.Initialize(()=>menu&&!Online&&(arena==null||match.Phase==NativePhase.Match),Save);
        }
        private void UpdatePanel()
        {
            if(Updater==null)return;
            GUILayout.Space(12);GUILayout.Label("Unity updates",label);
            if(Updater.Installed!=null)GUILayout.Label("Installed build · "+NativeUpdater.Short(Updater.Installed.version),small);
            GUILayout.Label(Updater.Status,small);
            if(Updater.State==NativeUpdateState.Available)
            {
                bool previous=GUI.enabled;GUI.enabled=previous&&Updater.CanInstall;
                if(GUILayout.Button("Update and restart",button))Updater.InstallAvailableUpdate();GUI.enabled=previous;
                if(!Updater.CanInstall)GUILayout.Label("Finish the match and leave the room before updating.",small);
            }
            if(!Updater.Busy&&Updater.State!=NativeUpdateState.Unavailable&&GUILayout.Button("Check for updates",button))Updater.CheckForUpdates();
            if(Updater.CanCancel&&GUILayout.Button("Cancel download",button))Updater.Cancel();
        }
    }
}
