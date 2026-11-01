using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

// Engine-free checks for the draft stage's wrist plant (Core/NativeDraftPlant.cs, as NativeDraftPresentation drives it after
// the tray's reach) and the install cue's timing on it (Core/NativeInstallCue.cs). Unity's rendering of the stage is not
// exercised.
internal static class DraftPlantChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Draft plant: "+what);}
    public static IEnumerable<string> Run()
    {
        // Size: the held chip at the top of its breath stays inside the pilot's palm, and reads as a real chip.
        float biggest=0;for(float t=0;t<10;t+=.01f)biggest=Math.Max(biggest,NativeDraftPlant.HeldChipScale(NativeDraftPlant.Contact+.22f,t,false)*NativeDraftPlant.PickupWidth);
        Check(biggest<NativeDraftPlant.PalmWidth&&biggest>.1f,"held chip "+biggest+" m against a "+NativeDraftPlant.PalmWidth+" m palm");
        Check(NativeDraftPlant.HeldChipScale(NativeDraftPlant.PlantEnd,0,true)<1e-4f,"the chip is not gone after the plant");
        Check(NativeDraftPlant.SoundAt>=NativeDraftPlant.CarryEnd-.02f&&NativeDraftPlant.SoundAt<=NativeDraftPlant.PlantEnd&&NativeDraftPlant.DetailsAt>=NativeDraftPlant.PulseStart+.3f&&NativeDraftPlant.ReturnEnd<3.2f,"timeline");
        yield return "Held chip "+(biggest*100).ToString("0.0")+" cm at most (palm "+(NativeDraftPlant.PalmWidth*100).ToString("0.0")+" cm, source package 17 cm), gone after the plant; cue at "+NativeDraftPlant.SoundAt+" s, details at "+NativeDraftPlant.DetailsAt+" s";

        // Motion: picks to the left and to the right at 30, 60, 144 and 240 Hz, from the tray's reach through the return.
        float fastest=0,worstPlant=0,worstRest=0,lowestNormal=1;long allocated=0;string fastestAt="";
        foreach(bool cardLeft in new[]{true,false})foreach(int hz in new[]{30,60,144,240})
        {
            var plant=new NativeDraftPlant();var pose=new CapturedRig.Pose();float dt=1f/hz;Vector3 lastL=default,lastR=default,lastChip=default;bool first=true,shown=false;
            NativeDraftRules.Pose(pose,0);pose.World();var idleL=pose.WorldPosition[CapturedRig.LHand];var idleR=pose.WorldPosition[CapturedRig.RHand];
            for(int f=0;f<=(int)(2.0*hz);f++)
            {
                float age=f*dt;long before=GC.GetAllocatedBytesForCurrentThread();
                bool left=Stage(pose,plant,cardLeft,age);if(f>2)allocated+=GC.GetAllocatedBytesForCurrentThread()-before;
                var l=pose.WorldPosition[CapturedRig.LHand];var r=pose.WorldPosition[CapturedRig.RHand];
                if(!first){float step=Math.Max((l-lastL).Length(),(r-lastR).Length());if(plant.ChipShown&&shown)step=Math.Max(step,(plant.Chip-lastChip).Length());step/=dt;if(step>fastest){fastest=step;fastestAt=hz+" Hz age "+age.ToString("0.000");}}
                first=false;lastL=l;lastR=r;lastChip=plant.Chip;shown=plant.ChipShown;
                if(age>=NativeDraftPlant.CarryEnd-.5f*dt&&age<NativeDraftPlant.CarryEnd+.5f*dt)
                {
                    int h=left?CapturedRig.LHand:CapturedRig.RHand;var palm=pose.WorldPosition[h]+Vector3.Transform(NativeDraftPlant.PalmLocal,pose.WorldRotation[h]);
                    worstPlant=Math.Max(worstPlant,(palm-plant.At).Length());lowestNormal=Math.Min(lowestNormal,plant.Normal.Z);
                    Check(plant.At.Z>.15f,"the plant point is not in front of the chest ("+plant.At+")");
                }
                if(age>NativeDraftPlant.PlantEnd+dt)Check(!plant.ChipShown,"the chip still shows after the plant");
                if(age>=NativeDraftPlant.Contact&&age<NativeDraftPlant.CarryEnd)Check(plant.ChipShown,"the chip vanished before the plant");
                if(age>=NativeDraftPlant.ReturnEnd)worstRest=Math.Max(worstRest,Math.Max((l-idleL).Length(),(r-idleR).Length()));
            }
        }
        Check(fastest<4.5f,"a wrist or the chip moves "+fastest+" m/s between frames (a pop) at "+fastestAt);Check(worstPlant<.03f,"the palm misses the wrist by "+worstPlant+" m at the plant");
        Check(lowestNormal>.3f,"the inner wrist does not face the camera (normal z "+lowestNormal+")");Check(worstRest<.002f,"the arms do not settle back to the idle ("+worstRest+" m)");Check(allocated==0,"the plant pose allocated "+allocated+" bytes");
        yield return "Wrist plant at 30/60/144/240 Hz, both sides: fastest wrist or chip "+fastest.ToString("0.00")+" m/s (no pops), palm on the wrist within "+(worstPlant*1000).ToString("0")+" mm at the plant, inner wrist facing the camera (normal z >= "+lowestNormal.ToString("0.00")+"), arms back to the idle, 0 bytes per frame";
        yield return InstallCue();
    }
    // A plant frame after a reach toward a card on the plane z=.42 (NativeDraftStage drives the full stage, DraftStageChecks).
    // Returns the reaching side.
    private static bool Stage(CapturedRig.Pose pose,NativeDraftPlant plant,bool cardLeft,float age)
    {
        NativeDraftRules.Pose(pose,0);pose.World();var target=new Vector3(cardLeft?.62f:-.62f,.42f,.42f);bool left=target.X>0;
        int hand=left?CapturedRig.LHand:CapturedRig.RHand,clav=left?CapturedRig.LClav:CapturedRig.RClav;float weight=1,reach=NativeDraftStage.Reach(age);
        var shoulder=pose.WorldPosition[clav+1];var point=shoulder+Vector3.Normalize(target-shoulder)*(.495f+.017f*reach);point.X=left?Math.Max(.36f,point.X):Math.Min(-.36f,point.X);
        var rest=pose.WorldPosition[hand];var control=shoulder+new Vector3(left?.06f:-.06f,-.12f,.34f);point=point*weight*weight+control*(2*weight*(1-weight))+rest*(1-weight)*(1-weight);
        var local=Vector3.Transform(point-pose.WorldPosition[CapturedRig.Chest],Quaternion.Inverse(pose.WorldRotation[CapturedRig.Chest]));
        if(age<NativeDraftPlant.Contact)CapturedRig.Arm(pose,left,local,NativeDraftPlant.ContactWrist,new Vector3(left?1:-1,-.8f,-.25f));
        plant.Pose(pose,left,local,age);plant.Place(pose,left,age);return left;
    }
    // The install cue lands on the plant: PvP from the pick time; the Siren follow-through holds my own event and plays it once.
    private static string InstallCue()
    {
        float at=NativeDraftPlant.SoundAt;double plant=10+(double)at;
        Check(!NativeInstallCue.DraftDue(10,10)&&!NativeInstallCue.DraftDue(10,plant-.01)&&NativeInstallCue.DraftDue(10,plant)&&NativeInstallCue.DraftDue(10,plant+.39)&&!NativeInstallCue.DraftDue(10,plant+NativeInstallCue.Fresh),"PvP cue window");
        int Plays(float[] arrive,float endAt,out float playedAt)
        {   // A follow-through from t=0 to endAt at 60 Hz; my event arrives at arrive[0] (and again at arrive[1] if given).
            var c=new NativeInstallCue();int plays=0;playedAt=-1;
            for(int f=0;f<=180;f++){float t=f/60f;string chip=t<endAt?"mishima-armor":null;if(c.Step(chip,0,t)!=null){plays++;playedAt=t;}
                foreach(float a in arrive)if(Math.Abs(t-a)<.5f/60&&!c.Hold("mishima-armor")){plays++;playedAt=t;}}
            return plays;
        }
        float end=NativeDraftPlant.ReturnEnd;
        Check(Plays(new[]{.02f},end,out float p)==1&&Math.Abs(p-at)<1.5f/60,"host pick: played "+p+" s, not at the plant");
        Check(Plays(new[]{1.2f},end,out p)==1&&Math.Abs(p-1.2f-1/60f)<1.5f/60,"late guest event: played "+p);
        Check(Plays(new[]{.02f},.3f,out p)==1&&Math.Abs(p-.3f)<1.5f/60,"ended early: played "+p);
        Check(Plays(new[]{2.5f},end,out p)==1&&Math.Abs(p-2.5f)<1.5f/60,"event after the follow-through: played "+p);
        Check(Plays(new[]{.02f,.5f},end,out _)==2,"a second event is not swallowed");
        return "Install cue: PvP at the plant ("+at.ToString("0.00")+" s after the pick, .4 s fresh); the Siren follow-through plays my own Install once at the plant, at an early end, or on a late event";
    }
}
