#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed class NativeExpeditionLoadPreview : MonoBehaviour
    {
        private const string Flag="-expedition-load";
        private NativeExpeditionLoadFixture fixture;
        private ArenaPresentation view;
        private Camera eye;
        private SimulationMode previousMode;
        private Vector3 previousGravity;
        private float previousStep;

        internal static bool TryLaunch()
        {
            var args=Environment.GetCommandLineArgs();
            int at=Array.IndexOf(args,Flag);
            if(at<0)return false;
            if(at+1>=args.Length||!int.TryParse(args[at+1],out int pilots)||pilots<1||pilots>4)
                throw new ArgumentException(Flag+" requires a pilot count from 1 to 4.");
            var preview=new GameObject("Expedition load preview").AddComponent<NativeExpeditionLoadPreview>();
            preview.Open(pilots,args);
            return true;
        }
        private void Open(int pilots,string[] args)
        {
            previousMode=Physics.simulationMode;
            previousGravity=Physics.gravity;
            previousStep=Time.fixedDeltaTime;
            try
            {
                Physics.simulationMode=SimulationMode.Script;
                Physics.gravity=new Vector3(0,-23,0);
                Time.fixedDeltaTime=1f/60;
                if(args.Contains(ArenaPresentation.VoxelBodiesArgument)||Resources.Load<GameObject>(ArenaPresentation.VisualBodyResource)==null)
                    throw new InvalidOperationException("The load preview requires the BoZo pilot body resource and modular bodies enabled.");
                fixture=new NativeExpeditionLoadFixture(pilots);
                fixture.Runtime.SetPresented(true);
                var cameraObject=new GameObject("Load camera");cameraObject.transform.SetParent(transform,false);
                eye=cameraObject.AddComponent<Camera>();eye.tag="MainCamera";
                var tier=args.Contains("-load-low")?NativeGraphicsTier.Low:args.Contains("-load-balanced")?NativeGraphicsTier.Medium:NativeGraphicsTier.High;
                bool low=tier==NativeGraphicsTier.Low;
                QualitySettings.shadows=low?ShadowQuality.Disable:ShadowQuality.All;
                QualitySettings.antiAliasing=low?0:4;QualitySettings.pixelLightCount=low?3:9;
                NativeLighting.Create(transform);NativeLighting.Graphics(low);
                NativeAdvancedLighting.Graphics(tier,false);NativeLighting.Apply(fixture.Arena.Map,eye);
                view=gameObject.AddComponent<ArenaPresentation>();view.Initialize(false);
                view.GeneratedRun=fixture.Bundle;view.VisualEye=eye;view.WeaponLoadout=fixture.Arena;
                view.ClassicBodies=false;view.FirstPerson=false;view.SoundEnabled=false;view.LowGraphics=low;view.GraphicsTier=tier;
                Debug.Log("EXPEDITION_LOAD_PREVIEW pilots="+pilots+" zombies=88 graphics="+tier+" gpu="+SystemInfo.graphicsDeviceName);
            }
            catch{Destroy(gameObject);throw;}
        }
        private void FixedUpdate()
        {
            if(fixture==null)return;
            try
            {
                fixture.Step();
                if(fixture.Tick==NativeExpeditionLoadFixture.Warmup+NativeExpeditionLoadFixture.Samples)Debug.Log(fixture.Report());
            }
            catch{enabled=false;throw;}
        }
        private void LateUpdate()
        {
            if(fixture==null||view==null)return;
            var arena=fixture.Arena;var focus=arena.Local.Body.position;
            eye.transform.position=focus+new Vector3(1,5,-10);
            eye.transform.LookAt(focus+Vector3.up);
            view.Render(arena.Snapshot(true),arena.Time,false);view.RenderAbilities(arena);
            view.RenderStatus(arena,eye,1);view.RenderGlow(arena,eye);view.FinishSourceFrame();
        }
        private void OnDestroy()
        {
            fixture?.Dispose();Physics.gravity=previousGravity;Physics.simulationMode=previousMode;Time.fixedDeltaTime=previousStep;
        }
    }
}
#endif
