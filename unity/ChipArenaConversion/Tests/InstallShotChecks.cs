using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;

// Engine-free checks for the draft stage's chip install (owner 2026-11-18: "we need to improve the animation when the player model
// "installs" the chip. Please review, improve drastically, and update."; Core/NativeInstallShot.cs, NativeDraftInstall.cs,
// NativeHandShapes.cs as NativeDraftStage and NativeDraftPresentation drive them): the timeline (the clack on the install cue's own key,
// the PvE / Deathmatch cap and the Expedition's hold unchanged, every key in order inside every turn), the pose clock's zero-sum hold,
// the tier profiles (every tier and the rainbow pick, levels rising with rank, the boss relics' and the kits'), the stage device's
// states (the lid opens with its overshoot and shuts with its bounce, the bay takes the chip from the latch, the lens from the shut),
// the camera's shot on every screen (a push to the hero framing inside the safe frame, the visor in the upper third, the bay low in the
// frame, Reduced effects and the Siren capped, back on the portrait by the settle, no jump a frame), the cartridge never bigger than the
// hand and its core inside the bay, the hit-stop holding the cartridge on the bay, the hand shapes (weight 0 is the grip's curl bit for
// bit, a pinch closes the thumb and two fingers), the sound schedule (existing bank keys; the seven bespoke keys in the bank contract
// and requested), the text's timing, the bloom's, and the runtime's order (the shot between the portrait and the pose, the contact pass
// before the draws, the lights after the stage's, the follow-throughs on the shared cap), allocation-free.
internal static class InstallShotChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Install: "+what);}
    public static IEnumerable<string> Run(string root)
    {
        yield return Timeline();
        yield return Clock();
        yield return Profiles();
        yield return Device();
        yield return Shot();
        yield return Cartridge();
        yield return Shapes();
        yield return Sounds(root);
        yield return Text();
        yield return Wiring(root);
    }
    private static string Timeline()
    {
        Check(NativeInstallShot.Seat==NativeDraftPlant.SoundAt&&NativeInstallShot.Seat==1.14f,"the clack on the install cue's key (1.14 s, NativeDraftPlant.SoundAt unchanged)");
        Check(NativeInstallShot.ShortEnd==NativeDraftPlant.ReturnEnd&&NativeExpedition.ChoicePlantSeconds==NativeDraftPlant.ReturnEnd&&NativePickFollow.Plays(1.9f,false,false,false,false)&&!NativePickFollow.Plays(1.96f,false,false,false,false),"the short follow-through's cap (1.95 s) and the Expedition's hold unchanged");
        float[] keys={0,NativeInstallShot.ReachStart,NativeInstallShot.FlyStart,NativeInstallShot.PushStart,NativeInstallShot.RiseStart,NativeInstallShot.BootStart,NativeInstallShot.Burst,NativeInstallShot.LidOpen,NativeInstallShot.Catch,NativeInstallShot.LidOpenEnd,NativeInstallShot.CarryEnd,NativeInstallShot.AlignEnd,
            NativeInstallShot.ThumbOff,NativeInstallShot.Contact,NativeInstallShot.Seat,NativeInstallShot.Release,NativeInstallShot.ReleaseEnd,NativeInstallShot.ShutStart,NativeInstallShot.ShutEnd,NativeInstallShot.SurgeStart,NativeInstallShot.ShutSettled,NativeInstallShot.PowerStart,NativeInstallShot.CoreAt,NativeInstallShot.VisorAt,NativeInstallShot.PanStart,NativeInstallShot.DetailsAt,NativeInstallShot.PowerEnd,NativeInstallShot.BloomAt,NativeInstallShot.LookStart,NativeInstallShot.ShortEnd,NativeInstallShot.PanEnd,NativeInstallShot.LookEnd,NativeInstallShot.HoldEnd};
        for(int i=1;i<keys.Length;i++)Check(keys[i]>=keys[i-1],"key "+i+" ("+keys[i]+") before key "+(i-1)+" ("+keys[i-1]+")");
        Check(NativeInstallShot.Contact<NativeInstallShot.Seat&&NativeInstallShot.Seat-NativeInstallShot.Contact<=.03f&&NativeInstallShot.Catch-NativeInstallShot.Burst>=.1f,"the latch just before the clack; the 3D flight into the hand");
        // Every PvP turn holds its whole install: the settle ends before the turn does, the readout gets at least a second.
        foreach(var id in new[]{"x","l44t-root","bomb-x","arcade-x","mishima-x",NativeDogChips.Ids[0],NativeGlaiveChips.Ids[0],NativeKatanaChips.Ids[0],NativeFangsChips.Ids[0]})
        {
            float turn=NativeInstallShot.Turn(id);Check(turn>=NativeInstallShot.TurnSeconds-1e-4f,id+": the turn "+turn);
            Check(NativeInstallShot.SettleEnd(turn)<=turn-.1f&&NativeInstallShot.SettleStart(turn)>=NativeInstallShot.HoldEnd-1e-4f&&turn-NativeInstallShot.DetailsAt>=1,id+": the settle "+NativeInstallShot.SettleStart(turn)+".."+NativeInstallShot.SettleEnd(turn)+" inside the "+turn+" s turn");
        }
        Check(Math.Abs(NativeInstallShot.SettleStart(3.2f)-2.55f)<1e-4f&&Math.Abs(NativeInstallShot.SettleEnd(3.2f)-3.05f)<1e-4f,"a 3.2 s turn settles 2.55-3.05");
        Check(NativeInstallShot.Turn(NativeKatanaChips.Ids[0])==(float)NativeKatanaChips.RevealSeconds&&NativeInstallShot.Turn(NativeFangsChips.Ids[0])==(float)NativeFangsChips.RevealSeconds,"the Katana's and the Fangs' holds are the rules'");
        return "Install timeline: the clack on the install cue's key ("+NativeInstallShot.Seat+" s), "+keys.Length+" keys in order, the short follow-through's cap ("+NativeInstallShot.ShortEnd+" s) and the Expedition's hold unchanged, every PvP turn (3.2-5.2 s) settled inside its hold";
    }
    private static string Clock()
    {
        float worstRate=0;foreach(float hold in new[]{0,.04f,.09f,.1f})
        {
            float last=-1;for(int f=0;f<=600;f++)
            {
                float a=f/200f,p=NativeInstallShot.PoseAge(a,hold,false);Check(p>=last-1e-6f,"the pose clock runs back ("+a+")");
                if(a<NativeInstallShot.Seat||a>=NativeInstallShot.Seat+hold+NativeInstallShot.CatchUp)Check(Math.Abs(p-a)<1e-5f,"the pose clock is the age outside the hold ("+a+", "+p+")");
                if(hold>0&&a>NativeInstallShot.Seat&&a<NativeInstallShot.Seat+hold)Check(Math.Abs(NativeInstallShot.PoseRate(a,hold,false)-NativeInstallShot.HoldRate)<1e-6f,"the hold runs at a tenth");
                if(f>0){float r=(p-last)*200;worstRate=Math.Max(worstRate,r);float rate=NativeInstallShot.PoseRate(a-.0025f,hold,false);Check(Math.Abs(r-rate)<.12f,"the rate is the clock's slope at "+a+" ("+r+" vs "+rate+")");}last=p;
            }
            Check(NativeInstallShot.PoseAge(NativeInstallShot.Seat+.05f,hold,true)==NativeInstallShot.Seat+.05f,"Reduced effects: no hold");
        }
        Check(worstRate<2.3f,"the catch-up runs at most "+worstRate+" times");
        return "Install pose clock: a tenth's pace through the clack's hold (40-100 ms), a catch-up of "+NativeInstallShot.CatchUp+" s at most "+worstRate.ToString("0.0")+"x, the age again after it (zero-sum); none with Reduced effects";
    }
    private static string Profiles()
    {
        float last=0;foreach(var tier in new[]{"common","uncommon","rare","epic","legendary","mythic"})
        {
            var p=NativeInstallShot.Of(tier,"x");Check(p.Level>last&&p.Grade==NativeChipCartridge.Of(tier).Rank&&p.Hold>=.04f&&p.Hold<=.09f&&p.Flash>=.16f&&p.Flash<=.34f,tier+": its level "+p.Level+" grade "+p.Grade);last=p.Level;
        }
        foreach(var tier in NativeChipCartridge.Tiers.Concat(new[]{NativeChipCartridge.Rainbow,"bogus"}))
        {
            var p=NativeInstallShot.Of(tier,"x");Check(p.Level>=.55f&&p.Level<=1.15f&&p.Grade>=1&&p.Grade<=6&&p.Hold<=.1f&&p.Sparks>=6&&p.Sparks<=16&&p.Rings>=1&&p.Rings<=3&&p.Tint>0&&p.Tint<=.75f&&p.Visor>0&&p.Visor<=1,tier+": a profile in range");
            if(NativeBossChips.IsTier(tier))Check(p.Boss&&p.Level==1.15f&&p.Hold==.1f,tier+": the boss relic's level and hold");
            if(NativeChipCartridge.Of(tier).Rank==0&&!NativeBossChips.IsTier(tier)&&tier!=NativeChipCartridge.Rainbow)Check(p.Kit&&p.Level==.95f&&p.Grade==4,tier+": a kit's level");
        }
        Check(NativeInstallShot.Of("common","ab-x").Ability&&NativeInstallShot.Of("common","wild-x").Wild&&!NativeInstallShot.Of("common","x").Wild,"ability and wild chips known by their prefix");
        Check(NativeInstallShot.Hero(1,"x",1,NativeInstallShot.Of("legendary","x"))==NativeInstallShot.Raise&&NativeInstallShot.Hero(1,"x",1,NativeInstallShot.Of("common","x"))==NativeInstallShot.ClenchNod,"legendary always raises the fist; common clenches and nods");
        int raises=0;for(int s=0;s<60;s++){int h=NativeInstallShot.Hero(s,"chip-"+s,s%3,NativeInstallShot.Of("rare","x"));Check(h==NativeInstallShot.Hero(s,"chip-"+s,s%3,NativeInstallShot.Of("rare","x")),"the hold is the same on every client");if(h==NativeInstallShot.Raise)raises++;}
        Check(raises>5&&raises<55,"rare raises by hash ("+raises+" of 60)");
        return "Install profiles: common to mythic rise in level (.55-1.1), every tier and the rainbow pick in range, the kits at .95 with epic's extras, the boss relics 1.15 with a 100 ms hold; the hero hold by hash, the same on every client";
    }
    private static string Device()
    {
        var pr=NativeInstallShot.Of("rare","x");float most=0,bounce=0;
        for(int f=0;f<=400;f++)
        {
            float p=f/100f;var d=NativeInstallShot.Device(p,3.2f,false,pr);
            if(p<NativeInstallShot.LidOpen)Check(d.Lid==0,"the lid shut before .52 ("+p+")");
            if(p>=NativeInstallShot.LidOpenEnd&&p<NativeInstallShot.ShutStart)Check(d.Lid>=1-1e-4f,"the lid open from .72 ("+p+": "+d.Lid+")");
            if(p<NativeInstallShot.ShutStart)most=Math.Max(most,d.Lid);else if(p>=NativeInstallShot.ShutEnd)bounce=Math.Max(bounce,d.Lid);
            if(p>=NativeInstallShot.ShutSettled)Check(d.Lid==0,"the lid shut from 1.30");
            Check(d.BayChip==(p>=NativeInstallShot.Contact),"the bay in the chip's colour from the latch ("+p+")");Check(d.LensChip==(p>=NativeInstallShot.ShutStart),"the lens in the chip's colour from the shut ("+p+")");
            Check(d.Lens>0&&d.Strips>=0&&d.Bay>=0,"levels in range");
        }
        Check(most>1&&most<=1+NativeInstallShot.LidOvershoot+1e-4f&&bounce>0&&bounce<=NativeInstallShot.LidOvershoot+1e-4f,"the lid's overshoot ("+(most-1)*NativePilotGear.LidOpenDegrees+" deg) and bounce ("+bounce*NativePilotGear.LidOpenDegrees+" deg)");
        long before=GC.GetAllocatedBytesForCurrentThread();float sum=0;for(int i=0;i<1000;i++){sum+=NativeInstallShot.Device(i*.003f,3.2f,i%2==0,pr).Lid+NativeInstallShot.PoseAge(i*.003f,.05f,false)+NativeInstallShot.Kick(i*.001f,30,.4f);}
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0&&!float.IsNaN(sum),"the device, the clock and the curves allocate nothing");
        return "Install device: the lid opens .52-.72 with a "+((most-1)*NativePilotGear.LidOpenDegrees).ToString("0")+" deg overshoot and shuts 1.20-1.24 with a "+(bounce*NativePilotGear.LidOpenDegrees).ToString("0")+" deg bounce, the bay takes the chip's colour from the latch and the lens from the shut; allocation-free";
    }
    // A 16:9-like portrait for a screen (a single row of four cards at the tray line) on the stage.
    private static NativeDraftStage Portrait(float aspect,float trayTop,float crown=0)
    {
        var s=new NativeDraftStage();var cards=new[]{new Vector2(.3f,trayTop+.08f),new Vector2(.45f,trayTop+.08f),new Vector2(.6f,trayTop+.08f),new Vector2(.75f,trayTop+.08f)};
        for(int f=0;f<30;f++){s.SmoothAge(-1,1/60f);s.Begin(1/60f,false);s.Frame(aspect,trayTop,trayTop+.2f,crown,false,1/60f,NativeDraftStage.ModelHeadTop);s.Step(1/60f,-1,0,false,cards,4,false,false);}
        return s;
    }
    private static string Shot()
    {
        float leastHero=9,mostHero=0,worstJump=0;int screens=0;string jumpAt="";var pr=NativeInstallShot.Of("legendary","x");
        foreach(float aspect in new[]{1.25f,4f/3,16f/10,16f/9,21f/9,32f/9})foreach(float tray in new[]{.68f,.72f,.55f})
        {
            var s=Portrait(aspect,tray);screens++;var p0=s.CameraPosition;var t0=s.CameraTarget;
            s.Shot(NativeInstallShot.PushEnd+.02f,pr,3.2f,false,false,false,true);float hero=s.HeroMagnify;leastHero=Math.Min(leastHero,hero);mostHero=Math.Max(mostHero,hero);
            Check(hero>=1.2f&&hero<=NativeDraftStage.HeroMost+1e-4f,aspect+" tray "+tray+": the hero framing "+hero+"x");
            var visor=s.Project(s.PlannedVisor);var bay=s.Project(s.PlannedBay);Check(visor.Y>=NativeDraftStage.EyesBelow&&visor.Y<.36f,aspect+": the visor in the upper third ("+visor.Y+")");Check(bay.Y>.5f&&bay.Y<=NativeDraftStage.SafeBottom+.01f&&bay.X>.3f&&bay.X<.7f,aspect+": the bay low in the frame's centre ("+bay+")");
            // Back on the portrait by the settle (the PvP turn), and the short cut ends pushed in.
            Frame(s,aspect,tray);s.Shot(NativeInstallShot.SettleEnd(3.2f)+.01f,pr,3.2f,false,false,false,true);Check((s.CameraPosition-p0).Length()<.01f&&(s.CameraTarget-t0).Length()<.01f&&Math.Abs(s.Magnify-1)<.01f,aspect+": back on the portrait by the settle");
            Frame(s,aspect,tray);s.Shot(NativeInstallShot.ShortEnd,pr,3.2f,true,false,false,true);Check(s.Magnify>hero*1.04f,aspect+": the short cut creeps in");
            Frame(s,aspect,tray);s.Shot(NativeInstallShot.PushEnd+.02f,pr,3.2f,false,false,true,true);Check(s.Magnify<=NativeDraftStage.ReducedMost+1e-3f&&s.Roll==0,aspect+": Reduced effects push at most "+NativeDraftStage.ReducedMost+"x, no kick");
            Frame(s,aspect,tray);s.Shot(NativeInstallShot.PushEnd+.02f,pr,3.2f,false,true,false,true);Check(s.Magnify<=NativeDraftStage.SirenMost*(1+NativeDraftStage.Creep)+1e-3f,aspect+": the Siren drafter at most "+NativeDraftStage.SirenMost+"x (and the legendary creep)");
            // No jump a frame (60 Hz) through the whole turn, the kick and its shake included.
            Vector3 last=default,lastStep=default;for(int f=0;f<=200;f++){float a=f/60f;Frame(s,aspect,tray);s.Shot(a,pr,3.2f,false,false,false,true);var step=s.CameraPosition-last;bool clack=a>=NativeInstallShot.Seat&&a<NativeInstallShot.Seat+NativeDraftStage.ShakeSpan+.1f||a<.1f;if(f>1&&!clack&&(step-lastStep).Length()>worstJump){worstJump=(step-lastStep).Length();jumpAt=aspect+" tray "+tray+" age "+a.ToString("0.000");}lastStep=step;last=s.CameraPosition;}
        }
        Check(worstJump<.012f,"the camera's step changes by "+worstJump+" m between frames (a jump) at "+jumpAt);
        // A tall crown keeps its room: the hero framing is lowered for it.
        var bare=Portrait(16f/9,.7f);bare.Shot(1.1f,pr,3.2f,false,false,false,true);var hat=Portrait(16f/9,.7f,.9f);hat.Shot(1.1f,pr,3.2f,false,false,false,true);Check(hat.HeroMagnify<=bare.HeroMagnify+1e-4f,"a crown never pushes further ("+hat.HeroMagnify+" vs "+bare.HeroMagnify+")");
        return "Install shot on "+screens+" screens (5:4 to 32:9, three tray heights): a dolly to the hero framing ("+leastHero.ToString("0.00")+"-"+mostHero.ToString("0.00")+"x) with the visor in the upper third and the bay low in the centre, the short cut creeping on, Reduced effects at most "+NativeDraftStage.ReducedMost+"x without the kick, the Siren at most "+NativeDraftStage.SirenMost+"x, back on the portrait by the settle, no jump (its step changes by at most "+(worstJump*1000).ToString("0.0")+" mm a frame at 60 Hz outside the lock's punch and the clack's kick and shake); a crown keeps its room";
    }
    // The portrait's camera again (Shot overwrites it each frame after Frame).
    private static void Frame(NativeDraftStage s,float aspect,float tray)=>s.Frame(aspect,tray,tray+.2f,0,false,1/60f,NativeDraftStage.ModelHeadTop);
    private static string Cartridge()
    {
        Check(NativeDraftInstall.HeldWidth<=NativeDraftPlant.PalmWidth*.5f&&NativeDraftInstall.VoxelWidth<NativeDraftPlant.PalmWidth&&NativeDraftInstall.SirenWidth<=NativeDraftPlant.PalmWidth*.8f+1e-4f&&NativeDraftInstall.HandShare<=.7f,"the cartridge is never bigger than the hand");
        // The core module seats in the bay, and through the clack's hold the cartridge stays on the bay.
        var s=new NativeDraftStage();s.Plant.Device=true;s.Plant.Profile=NativeInstallShot.Of("mythic","x");s.Plant.Tier="mythic";var cards=new[]{new Vector2(.4f,.8f),new Vector2(.6f,.8f)};var pr=s.Plant.Profile;float worst=0;
        for(int f=0;f<=(int)(4.5f*60);f++)
        {
            float t=f/60f,raw=t>=1?t-1:-1,a=s.SmoothAge(raw,1/60f),p=a<0?-1:NativeInstallShot.PoseAge(a,pr.Hold,false);s.Begin(1/60f,false,NativeDraftStage.IdleHeld(p));s.Frame(16f/9,.72f,.92f,0,false,1/60f,NativeDraftStage.ModelHeadTop);s.Shot(a,pr,3.2f,false,false,false,true);
            s.Step(1/60f*(a<0?1:NativeInstallShot.PoseRate(a,pr.Hold,false)),p,a<0?-1:0,false,cards,2,false,false);
            if(p>=NativeInstallShot.Contact&&p<NativeInstallShot.Release){s.Plant.OnBay(s.Plant.At,s.Plant.Normal,s.Plant.Forearm,0,out var flat,out _);worst=Math.Max(worst,(s.Plant.Chip-flat).Length());}
            if(p>=NativeInstallShot.Release)Check(!s.Plant.ChipShown&&s.Plant.CoreSeated,"the shell gone and the core seated from the release");
        }
        Check(worst<.002f,"the cartridge leaves the bay by "+worst+" m through the hold");
        float core=.152f*2*NativeDraftInstall.HeldWidth/NativeDraftInstall.ModelWidth;Check(core<2*NativePilotGear.BayHalf&&NativeDraftInstall.LatchDepth<=NativePilotGear.FrameHeight+.001f,"the core ("+core*100+" cm) fits the bay ("+NativePilotGear.BayHalf*200+" cm)");
        return "Install cartridge: "+NativeDraftInstall.HeldWidth*100+" cm on the model (at most "+NativeDraftInstall.HandShare+" of the glove), "+NativeDraftInstall.VoxelWidth*100+" cm on the voxel body, its "+(core*100).ToString("0.0")+" cm core seated in the "+(NativePilotGear.BayHalf*200).ToString("0.0")+" cm bay, held on the bay (within "+(worst*1000).ToString("0.0")+" mm) through the mythic's hold";
    }
    private static string Shapes()
    {
        // Weight 0 is the grip's curl bit for bit; a pinch closes the thumb and two fingers on the cartridge.
        var rig=VisualRigChecks.Synthetic(out _);var pose=new CapturedRig.Pose();pose.Reset();pose.World();
        var plain=new NativeVisualRig.State(rig);var none=new NativeVisualRig.State(rig);var pinch=new NativeVisualRig.State(rig);
        var g=new NativeVisualRig.Grasp{LeftGrip=.4f,RightGrip=.7f};NativeVisualRig.Pose(plain,pose,g,false,false);
        var g0=g;g0.LeftShape=NativeHandShapes.Pinch;g0.LeftShape.Weight=0;g0.RightShape=NativeHandShapes.Cup;g0.RightShape.Weight=0;NativeVisualRig.Pose(none,pose,g0,false,false);
        for(int v=0;v<rig.Count;v++)Check(plain.Turn[v]==none.Turn[v],"a shape at weight 0 changes bone "+rig.Names[v]);
        var g1=g;g1.LeftShape=NativeHandShapes.Pinch;NativeVisualRig.Pose(pinch,pose,g1,false,false);int index=rig.Find("index_02_l"),thumb=rig.Find("thumb_01_l");
        Check(index>=0&&thumb>=0&&rig.Digit[index]==1&&rig.Digit[thumb]==0&&rig.Digit[rig.Find("hand_l")]==-1,"the rig keeps each finger bone's digit");
        Check(Quaternion.Dot(plain.Turn[index],pinch.Turn[index])<.99999f&&rig.ThumbAcross[0].LengthSquared()>.5f,"a pinch turns the fingers and the thumb across");
        // The shapes over the install: open from the lock, the pinch at the catch, two fingers pressing, the flick, the rest; the receiving
        // hand cupped, braced, a fist; nothing before the pick; no jump a frame.
        Check(NativeHandShapes.Left(-1).Weight==0&&NativeHandShapes.Right(-1,0,out float gr,out float cl).Weight==0&&gr==0&&cl==0,"no shape before the pick");
        var at=NativeHandShapes.Left(NativeInstallShot.PinchSettled+.01f);Check(Math.Abs(at.Index-NativeHandShapes.Pinch.Index)<.01f&&Math.Abs(at.Ring-NativeHandShapes.Pinch.Ring)<.01f,"the pinch held after the catch");
        var press=NativeHandShapes.Left(NativeInstallShot.Seat);Check(press.Index<.2f&&press.Ring>.7f,"two fingers out at the clack, two tucked");
        NativeHandShapes.Right(1.4f,0,out float fist,out float clench);Check(fist>=.99f&&clench>.8f,"the receiving hand a fist after the surge");
        float worst=0;var last=NativeHandShapes.Left(0);for(int f=1;f<=360;f++){var s=NativeHandShapes.Left(f/120f);if(f/120f>=NativeInstallShot.Release&&f/120f<=NativeInstallShot.ReleaseEnd+.01f){last=s;continue;}worst=Math.Max(worst,Math.Max(Math.Abs(s.Index-last.Index),Math.Max(Math.Abs(s.Thumb-last.Thumb),Math.Abs(s.Ring-last.Ring))));last=s;}
        Check(worst<.2f,"a hand's shape jumps "+worst+" of its curl in a frame (120 Hz)");
        return "Install hands: a shape at weight 0 is the grip's curl bit for bit ("+rig.Count+" bones), the digits kept per finger bone, the taking hand open, pinching, pressing with two fingers, flicking (.04 s) and resting (else at most "+worst.ToString("0.00")+" of a curl a frame at 120 Hz), the receiving hand cupped, braced, a fist";
    }
    private static string Sounds(string root)
    {
        var keys=new HashSet<string>(NativeSoundKeys.All.Select(k=>k.Key));var toon=new HashSet<string>(NativeToonSynth.Ids);
        foreach(var c in NativeInstallShot.Cues.Concat(new[]{NativeInstallShot.Out,NativeInstallShot.Shatter}))
        {
            Check(c.Body==2?toon.Contains(c.Key):keys.Contains(c.Key),c.Key+" is a bank key (the Hot Dog's a toon synth id)");Check(c.Bespoke==null||keys.Contains(c.Bespoke),c.Key+"'s bespoke key is in the bank contract");
            Check(c.Gain>0&&c.Gain<=.6f&&c.Rate>0&&c.At>=0&&c.At<=NativeInstallShot.ShortEnd,c.Key+": a gentle cue inside the short cut ("+c.At+")");
        }
        Check(!NativeInstallShot.Cues.Any(c=>c.Key.StartsWith("intro.",StringComparison.Ordinal)),"no entrance music");
        foreach(var k in NativeInstallShot.BossSeat)Check(keys.Contains(k),k+" is a bank key");
        // The bespoke keys: in the contract (flat, high priority, short) and requested with a prompt for the sound alone.
        var path=Path.Combine(root,"../../docs/audio/sound-requests-install.json");Check(File.Exists(path),"the install's sound requests");
        using var doc=JsonDocument.Parse(File.ReadAllText(path));Check(doc.RootElement.GetProperty("schema").GetString()=="chip-arena.sound-requests/1","request schema");
        var sounds=doc.RootElement.GetProperty("sounds").EnumerateArray().ToDictionary(s=>s.GetProperty("key").GetString());var contract=NativeSoundKeys.All.ToDictionary(k=>k.Key);
        Check(sounds.Count==NativeInstallShot.Bespoke.Length,"one request per bespoke key");
        foreach(var key in NativeInstallShot.Bespoke)
        {
            Check(contract.ContainsKey(key)&&sounds.ContainsKey(key),key+" in the bank contract and requested");var d=contract[key];var s=sounds[key];
            float hi=s.GetProperty("targetSeconds").GetProperty("max").GetSingle(),gen=s.GetProperty("generateSeconds").GetSingle();
            Check(d.Space==NativeSoundSpace.Flat&&d.Priority==NativeSoundPriority.High&&d.Cap+1e-4f>=hi&&d.Cap<=.8f&&gen>=hi&&s.GetProperty("space").GetString()=="2d",key+" flat, high, inside its cap");
            var prompt=s.GetProperty("prompt").GetString();Check(prompt.Contains("isolated")&&prompt.Contains("no music")&&prompt.Length<400,key+"'s prompt asks for the sound alone");
            Check(s.GetProperty("count").GetInt32()>=1&&s.GetProperty("count").GetInt32()<=NativeSoundKeys.MaxClips,key+": takes within the bank");
        }
        return "Install sounds: "+NativeInstallShot.Cues.Length+" cues on existing bank keys (the Hot Dog's on the toon synth), no entrance music, the boss sets' seat motifs; the "+NativeInstallShot.Bespoke.Length+" bespoke ui.install keys in the bank contract (flat, high, <= .8 s) and requested (docs/audio/sound-requests-install.json)";
    }
    private static string Text()
    {
        Check(NativeInstallShot.Secured(NativeInstallShot.Seat-.01f,15)<0&&NativeInstallShot.Secured(NativeInstallShot.Seat+.01f,15)>0&&NativeInstallShot.Secured(NativeInstallShot.Seat+.21f,15)==15,"'Upgrade secured' typed from the clack, not before");
        Check(!NativeInstallShot.Installed(NativeInstallShot.Seat-.001f)&&NativeInstallShot.Installed(NativeInstallShot.Seat)&&NativeInstallShot.Stamp(NativeInstallShot.Seat)>1.1f&&NativeInstallShot.Stamp(NativeInstallShot.Seat+.2f)==1,"INSTALLED stamps at the clack");
        NativeInstallShot.Bloom(NativeInstallShot.BloomAt-.01f,-1,false,out _,out float before);NativeInstallShot.Bloom(NativeInstallShot.ShortEnd,-1,false,out float reach,out float end);NativeInstallShot.Bloom(0,0,false,out _,out float after0);NativeInstallShot.Bloom(0,.13f,false,out _,out float gone);
        Check(before==0&&Math.Abs(end-.35f)<1e-4f&&reach>.99f&&Math.Abs(after0-end)<1e-4f&&gone==0,"the bloom grows from 1.80 to .35 at the cut and fades over .12 s with no jump");
        Check(NativeInstallShot.Unfold(NativeInstallShot.DetailsAt)==0&&NativeInstallShot.Unfold(NativeInstallShot.DetailsAt+.21f)==1&&NativeInstallShot.ReadoutX(16f/9)>NativeInstallShot.PilotX(16f/9)+.25f,"the readout unfolds beside the pilot");
        // The card's flight lands its centre on the burst's hand-over and shrinks to the cartridge's width there.
        var from=new Vector2(.4f,.85f);var to=new Vector2(.55f,.5f);var b=NativeInstallShot.CardAt(from,to,NativeInstallShot.Burst);Check(b.Y<from.Y&&(b-to).Length()<(from-to).Length()*.3f&&(b-to).Length()>.005f,"the card is near, not at, the catch when it bursts ("+b+")");
        Check(Math.Abs(NativeInstallShot.CardScale(NativeInstallShot.Burst,.2f)-.2f)<1e-4f&&NativeInstallShot.CardScale(0,.2f)==1,"the card shrinks to the cartridge's width at the burst");
        return "Install text: 'Installing' until the clack, 'Upgrade secured' typed in over .2 s and INSTALLED stamped at it; the readout unfolding beside the pilot from "+NativeInstallShot.DetailsAt+" s; the short cut's bloom (.35 at the cut, .12 s fade); the card bursting near the catch at the cartridge's width";
    }
    private static string Wiring(string root)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));
        string draft=R("Runtime/NativeDraftPresentation.cs"),install=R("Runtime/NativeDraftInstallPresentation.cs"),screen=R("Runtime/NativeDraftScreen.cs"),follow=R("Runtime/NativeDraftFollow.cs"),body=R("Runtime/NativeVisualBodyPresentation.cs");
        int frame=draft.IndexOf("stage.Frame(camera.aspect",StringComparison.Ordinal),shot=draft.IndexOf("stage.Shot(a,pr,",StringComparison.Ordinal),step=draft.IndexOf("stage.Step(dt*rate,p,",StringComparison.Ordinal);
        Check(frame>0&&shot>frame&&step>shot,"RenderDraft: the portrait, the install's shot, then the pose on the pose clock");
        int pose=draft.IndexOf("VisualDraftPose(draftRig,seat,draftPose,(float)time,camera)",StringComparison.Ordinal),bay=draft.IndexOf("VisualDraftBayFrame(out diBayAt,out diBayN,out diBayF)",StringComparison.Ordinal),contact=draft.IndexOf("VisualDraftContact(stage.Left,p,",StringComparison.Ordinal),drawn=draft.IndexOf("DraftCartridgeDraw(stage,",StringComparison.Ordinal);
        Check(pose>0&&bay>pose&&contact>bay&&drawn>contact,"RenderDraft: the bind, the drawn bay, the contact pass, then the draws");
        int lights=draft.IndexOf("NativeLighting.Draft(camera,stageTint,flash);",StringComparison.Ordinal),mine=draft.IndexOf("DraftInstallLights(camera,reduced);",StringComparison.Ordinal),fx=draft.IndexOf("vfx.Begin();",StringComparison.Ordinal),submit=draft.IndexOf("vfx?.Submit(camera);",StringComparison.Ordinal);
        Check(lights>0&&mine>lights&&fx>0&&submit>fx,"the install's lights after the stage's, its effects begun and submitted on the stage");
        Check(!draft.Contains("PressOffset")&&!draft.Contains("ride=")&&!body.Contains("VisualArms.Reach,support"),"no learnt press offset, wrist ride or support bind left");
        Check(body.Contains("if(a==draftActor)DraftGrasp(ref grasp);")&&install.Contains("g.LeftShape=diLeftShape;g.RightShape=diRightShape;"),"only the draft actor wears the stage's hand shapes");
        Check(follow.Contains("PveFollowAge>NativeInstallShot.ShortEnd")&&R("Core/NativePickFollow.cs").Contains("age<=NativeInstallShot.ShortEnd"),"the PvE and Deathmatch follow-throughs end on the shared cap");
        Check(R("Runtime/NativeBladePresentation.cs").Contains("age-NativeInstallShot.DetailsAt-")&&R("Runtime/NativeGlaivePresentation.cs").Contains("age-NativeInstallShot.DetailsAt-")&&R("Runtime/NativeTopDogScreen.cs").Contains("age-NativeInstallShot.DetailsAt-"),"the class tiers' pick cues follow the readout");
        Check(screen.Contains("InstallTitle(chosen,seat,own,age,W,H)")&&screen.Contains("installed?\" · INSTALLED\":\" · INSTALLING\""),"the heading and the pill say installing until the clack");
        Check(R("Runtime/NativeAudio.cs").Contains("InstallStage();")&&install.Contains("foreach(var c in NativeInstallShot.Cues)"),"the install's cues play on the stage's clock");
        return "Install wiring: the shot between the portrait and the pose, the contact pass between the drawn bay and the draws, the lights after the stage's, the effects on the stage; the old press offset, ride and support gone; the hand shapes on the draft actor only; the follow-throughs on the shared cap, the class tiers' cues on the readout, the install's text and cues";
    }
}
