#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;
using Unity.Profiling;

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
        private int surveyEye, surveyFrame, surveyTier;
        private readonly double[] frameTimes = new double[36];
        private long surveyPeak;
        private double previousFrame;
        private ProfilerRecorder triangles;

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
                NativeCityLayout.Plan candidate = null;
                int selection = Array.IndexOf(args, "-meridian-candidate");
                if (selection >= 0)
                {
                    if (selection+1 >= args.Length || !int.TryParse(args[selection+1], out int index)
                        || index < 0 || index >= NativeCityLayout.Plans.Count)
                        throw new ArgumentException("Invalid Meridian candidate index");
                    candidate = NativeCityLayout.Plans[index];
                    if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                        throw new InvalidOperationException("Meridian render sampling requires a graphics device");
                    triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                    Application.runInBackground = true;
                    Application.targetFrameRate = -1;
                    QualitySettings.vSyncCount = 0;
                }
                fixture=new NativeExpeditionLoadFixture(pilots, candidate:candidate);
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
                view.GeneratedRun=candidate == null ? fixture.Bundle : null;view.VisualEye=eye;view.WeaponLoadout=fixture.Arena;
                view.ClassicBodies=false;view.FirstPerson=false;view.SoundEnabled=false;view.LowGraphics=low;view.GraphicsTier=tier;
                if (candidate != null) eye.farClipPlane = 800;
                Debug.Log("EXPEDITION_LOAD_PREVIEW pilots="+pilots+" zombies=88 graphics="+tier+" gpu="+SystemInfo.graphicsDeviceName);
            }
            catch
            {
                if (args.Contains("-meridian-candidate")) Application.Quit(2);
                Destroy(gameObject);
                throw;
            }
        }
        private void FixedUpdate()
        {
            if(fixture==null)return;
            try
            {
                fixture.Step();
                if(fixture.Tick==NativeExpeditionLoadFixture.Warmup+NativeExpeditionLoadFixture.Samples)Debug.Log(fixture.Report());
            }
            catch{enabled=false;if(fixture.Survey!=null)Application.Quit(2);throw;}
        }
        private void LateUpdate()
        {
            if(fixture==null||view==null)return;
            var arena=fixture.Arena;var focus=arena.Local.Body.position;
            eye.transform.position=focus+new Vector3(1,5,-10);
            eye.transform.LookAt(focus+Vector3.up);
            if (fixture.Survey != null)
            {
                if (surveyEye >= 1 && surveyEye <= 4)
                {
                    var route = fixture.Survey.Routes[surveyEye-1];
                    route.Sample(route.Length-18,out var foot,out _,out _);
                    eye.transform.position = NativeArena.Vec(foot)+Vector3.up*2;
                    eye.transform.LookAt(NativeArena.Vec(route.Destination)+Vector3.up*3);
                }
                else if (surveyEye == 5)
                {
                    eye.transform.position = new Vector3(0,42,-18);
                    eye.transform.LookAt(new Vector3(0,0,40));
                }
            }
            view.Render(arena.Snapshot(true),arena.Time,false);view.RenderAbilities(arena);
            view.RenderStatus(arena,eye,1);view.RenderGlow(arena,eye);view.FinishSourceFrame();
            if (fixture.Survey != null) SampleSurvey();
        }

        private void SampleSurvey()
        {
            // Let previous-camera submissions drain, then read the render thread's
            // triangle counter and elapsed wall time for 36 consecutive frame intervals.
            const int warmup = 24;
            double now = Time.realtimeSinceStartupAsDouble;
            if (surveyFrame >= warmup)
            {
                frameTimes[surveyFrame-warmup] = (now-previousFrame)*1000;
                if (triangles.Valid) surveyPeak=Math.Max(surveyPeak,triangles.LastValue);
            }
            previousFrame=now;
            if (++surveyFrame < warmup+frameTimes.Length) return;
            if (surveyPeak <= 0)
            {
                Debug.LogError("Meridian player has no rendered triangle counter");
                enabled=false;
                Application.Quit(2);
                return;
            }
            Array.Sort(frameTimes);
            Debug.Log(FormattableString.Invariant($"MERIDIAN_VIEW layout={fixture.Survey.Map.name} tier={(surveyTier==0?"High":"Low")} eye={surveyEye} peakTriangles={surveyPeak} meanMs={frameTimes.Average():F2} p95Ms={frameTimes[(int)Math.Ceiling(frameTimes.Length*.95)-1]:F2} gpu={SystemInfo.graphicsDeviceName} resolution={Screen.width}x{Screen.height}"));
            surveyFrame=0;
            surveyPeak=0;
            if (++surveyEye < 6) return;
            if (surveyTier==1)
            {
                enabled=false;
                Application.Quit(0);
                return;
            }
            surveyTier=1;
            surveyEye=0;
            QualitySettings.shadows=ShadowQuality.Disable;
            QualitySettings.antiAliasing=0;
            QualitySettings.pixelLightCount=3;
            NativeLighting.Graphics(true);
            NativeAdvancedLighting.Graphics(NativeGraphicsTier.Low,false);
            NativeLighting.Apply(fixture.Arena.Map,eye);
            view.LowGraphics=true;
            view.GraphicsTier=NativeGraphicsTier.Low;
        }
        private void OnDestroy()
        {
            if (triangles.Valid) triangles.Dispose();
            fixture?.Dispose();Physics.gravity=previousGravity;Physics.simulationMode=previousMode;Time.fixedDeltaTime=previousStep;
        }
    }
}
#endif
