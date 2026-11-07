using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The charge warning heard by the hunted pilot (owner 2026-10-14: "They literally can charge from across the map to the player
    // breaking the walls if they are hunting them and 'know' where they are"). Presentation only, from the presentation's
    // HulkWarning (Core/NativeHulkCharge: the pilot the charge hunts, nobody else), handed over by ConversionGame each live frame.
    // Existing bank keys only (silent without the owner's LocalAssets bank, as every horde cue): heard through walls, placed toward
    // the Hulk but close to the listener so the pan gives its direction and the urgency its level:
    //  - the wind-up's bellow (the lock's tell, heard before it runs);
    //  - the run's start: a rumble; a second, deeper one once it is close (urgency .75);
    //  - its footfalls by the distance it covers on its heavy stride, louder as it closes (the heavy footfall key on a hard floor,
    //    the surface's own step on metal, wood, gravel or snow: the same surface memory as its drawn footfalls).
    // The Hulk's own footfalls, roar and breaks still play at it from HordeCues for everyone near.
    public sealed partial class NativeAudio
    {
        /// <summary>The presentation's charge warning for the local pilot (ConversionGame sets it before Render each live frame).</summary>
        public NativeHulkCharge.Warning HulkWarning;
        private int hulkWarnId;private Vector3 hulkWarnLast;private float hulkWarnStep;
        private void HulkChargeCues(NativeArena arena)
        {
            var w=HulkWarning;if(arena==null||!w.Active||w.Frame<Time.frameCount-2||w.Fade<=0){hulkWarnId=0;return;}
            float t=arena.Time,start=t-w.Age,gain=NativeHulkCharge.RumbleGain(w.Urgency);var hulk=new Vector3(w.Hulk.X,w.Hulk.Y,w.Hulk.Z);
            var near=listener+new Vector3(w.Toward.X,0,w.Toward.Z)*Mathf.Min(w.Distance,3.5f);
            if(w.Id!=hulkWarnId){hulkWarnId=w.Id;hulkWarnStep=0;hulkWarnLast=hulk;}
            // (The cue keys are built only inside a cue's window: no string per frame.)
            if(!w.Running&&w.Age<.15f&&Once("hulk-charge-w"+w.Id,Mathf.Round(start*10)/10))BankPlay("structure.groan",near,.25f+.7f*gain,0,null,false,null,.6f);
            if(w.Running&&w.Age-NativeHulkMoves.ChargeWind<.15f&&Once("hulk-charge-r"+w.Id,Mathf.Round(start*10)/10))BankPlay("structure.rumble",near,.5f+.45f*gain,0,null,false,null,.75f);
            if(w.Running&&w.Urgency>=.75f&&Once("hulk-charge-n"+w.Id,Mathf.Round(start*10)/10))BankPlay("blast.quake",near,.45f+.4f*gain,0,null,false,null,.85f);
            if(w.Running&&w.Fade>=1)
            {
                var d=hulk-hulkWarnLast;d.y=0;float moved=d.magnitude;
                if(moved<2.5f)
                {
                    hulkWarnStep+=moved;float sc=NativeHordeKinds.Scale[NativeHordeKinds.Hulk],v=Mathf.Max(.5f,moved/Mathf.Max(1e-3f,Time.unscaledDeltaTime));
                    float stride=NativeHordeGait.Stride(NativeHordeKinds.Hulk,v/sc)*.5f*sc;string step=NativeHordeKinds.StepCue(NativeHordeKinds.Hulk);
                    // (the heavy footfall on a hard floor, else the surface's own step under the Hulk, through its surface memory;
                    // without clips today's stone thud, else its synthesized take: NativeFootstepAudio.cs ZombieStep)
                    if(hulkWarnStep>=stride){hulkWarnStep-=stride;ZombieStep(arena,w.Id,step,NativeHordeKinds.Hulk,hulk.x,hulk.y,hulk.z,sc,near,.3f+.6f*gain,.7f);}
                }
            }
            hulkWarnLast=hulk;
        }
    }
}
