using UnityEngine;
using UnityEngine.Events;

namespace ChipArena.UnityConversion
{
    // Put this component on the locally authored Resources/CarillonPrivateAtmosphere prefab.
    // Persistent UnityEvent bindings belong to that private prefab, including vendor adapters.
    public sealed class CarillonAtmosphereBridge : ThunderheartAtmosphere
    {
        public GameObject beamRig,weatherRig;
        [Range(.03f,.5f)]public float updateInterval=.1f;
        public UnityEvent<float> beamIntensity=new UnityEvent<float>(),windStrength=new UnityEvent<float>(),crownOpening=new UnityEvent<float>();
        private float next;
        public override bool OwnsBeams=>beamRig&&beamRig.activeInHierarchy;
        public override bool OwnsWeather=>weatherRig&&weatherRig.activeInHierarchy;
        public override void Pose(float bass,float body,float air,float crown,float quality)
        {
            bool high=quality>.01f;
            if(beamRig&&beamRig.activeSelf!=high)beamRig.SetActive(high);
            if(weatherRig&&weatherRig.activeSelf!=high)weatherRig.SetActive(high);
            if(Time.unscaledTime<next)return;
            next=Time.unscaledTime+updateInterval;
            if(OwnsBeams)beamIntensity.Invoke(.5f+.3f*bass+.2f*crown);
            if(OwnsWeather)windStrength.Invoke(.3f+.25f*body+.12f*air+.25f*crown);
            crownOpening.Invoke(crown);
        }
    }
}
