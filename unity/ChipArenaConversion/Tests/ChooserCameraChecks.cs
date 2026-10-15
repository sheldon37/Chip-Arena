using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

/// <summary>
/// The Expedition chooser camera (owner 2026-10-15: "Lets swing the camera to us when choosing,"; Core/NativeChooserCamera.cs,
/// Runtime/NativeChooserCameraPresentation.cs): the ease in and out (timing at 30-240 Hz, smoothstep ends, bounded speed, reversals in
/// place), the exact gameplay camera before and after (nothing applied at weight 0; the runtime restores the field of view and rebuilds
/// the camera from the unchanged look), the shot staying in front of a turning pilot and calm under mouse jitter, every Settings · Camera
/// mode swinging to the same shot, the framing over the tray (standing and bowed looks, three aspects), the collision fallback (never
/// inside the world, the next clear angle, a shorter distance), first person (out through the face, my body drawn only once clear and
/// out of view), the hand-off to death, cutscenes, spectating and other screens, no allocation, and the runtime hooks (after the aim,
/// before the shakes, no input touched). Managed boxes stand in for PhysX (as CameraRigChecks); no Unity Editor or Windows play is run.
/// </summary>
internal static class ChooserCameraChecks
{
    private const float Centre=.865f;
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Chooser camera: "+what);}
    private static string Cm(float m)=>(m*100).ToString("0.0")+" cm";
    private static string Deg(float d)=>d.ToString("0.0")+" deg";
    public static void Run(string root,ConversionContent data)
    {
        var log=new List<string>();Sides(data,log);Timing(log);Exact(log);Turning(log);Jitter(log);Modes(log);Framing(log);Collision(log);FirstPerson(log);Handoff(log);Allocation(log);Hooks(root,log);
        foreach(var line in log)Console.WriteLine("PASS chooser camera: "+line);
    }

    // ---- managed stand-ins for the world (axis-aligned boxes; PhysX's SphereCast skips colliders it starts inside) ----
    private sealed class Box
    {
        public Vector3 C,H;public Box(Vector3 c,Vector3 h){C=c;H=h;}
        public float Distance(Vector3 p){var d=Vector3.Max(Vector3.Abs(p-C)-H,Vector3.Zero);return d.Length();}
        public bool Inside(Vector3 o,float r){var l=o-C;return Math.Abs(l.X)<H.X+r&&Math.Abs(l.Y)<H.Y+r&&Math.Abs(l.Z)<H.Z+r;}
        public float Enter(Vector3 o,Vector3 d,float r,float max)
        {
            var lo=o-C;var h=H+new Vector3(r);float t0=0,t1=max;
            for(int k=0;k<3;k++)
            {
                float oo=k==0?lo.X:k==1?lo.Y:lo.Z,dd=k==0?d.X:k==1?d.Y:d.Z,hh=k==0?h.X:k==1?h.Y:h.Z;
                if(Math.Abs(dd)<1e-8f){if(oo<-hh||oo>hh)return -1;continue;}
                float a=(-hh-oo)/dd,b=(hh-oo)/dd;if(a>b)(a,b)=(b,a);t0=Math.Max(t0,a);t1=Math.Min(t1,b);if(t0>t1)return -1;
            }
            return t0;
        }
    }
    private static NativeCameraRig.Cast CastOf(IReadOnlyList<Box> boxes)=>(o,d,dist,r)=>
    {
        float best=dist;for(int i=0;i<boxes.Count;i++){var b=boxes[i];if(b.Inside(o,r))continue;float t=b.Enter(o,d,r,dist);if(t>=0&&t<best)best=t;}return best;
    };
    private static readonly NativeCameraRig.Cast Open=(o,d,dist,r)=>dist;

    private sealed class Mode{public string Name,Id;public bool Left,First;}
    private static readonly Mode[] All={new Mode{Name="shoulder right",Id=NativeCameraModes.Shoulder},new Mode{Name="shoulder left",Id=NativeCameraModes.Shoulder,Left=true},
        new Mode{Name="close action",Id=NativeCameraModes.Close},new Mode{Name="classic",Id=NativeCameraModes.Classic},new Mode{Name="first person",Id=NativeCameraModes.Shoulder,First=true}};

    // A pilot held choosing, driven as LateFrame drives it: the gameplay camera placed by its mode (UpdateCamera), then the chooser shot.
    private sealed class Held
    {
        public readonly NativeChooserCamera Cam=new NativeChooserCamera();public readonly NativeCameraRig Rig=new NativeCameraRig();public readonly Mode M;public NativeCameraRig.Cast Cast=Open;
        public Vector3 Body=new Vector3(0,Centre,0);public float Pitch=12,Aspect=16f/9,Side=1,Now;public bool HasHead,HasPanel;public Vector3 Head,Panel;
        public Vector3 GamePos,GamePivot;public float GameFov,Yaw;public ChooserCameraShot Shot;
        public Held(Mode m){M=m;Rig.Select(m.Id,m.Left,0);}
        public Vector3 Eye=>Body+Vector3.UnitY*NativeViewRules.EyeHeight;
        public ChooserCameraShot Step(bool want,float yaw,float dt,bool cut=false,int pilot=1)
        {
            Now+=dt;Yaw=yaw;var eye=Eye;
            if(M.First){GamePos=GamePivot=eye;GameFov=NativeViewRules.FirstFov;}
            else if(Rig.Classic(Now))
            {
                NativeCameraRig.Basis(yaw,Pitch,out var f,out _);var off=-f*NativeViewRules.ThirdDistance+Vector3.UnitY*NativeViewRules.ThirdLift;float hit=Cast(eye,Vector3.Normalize(off),off.Length(),.12f);
                GamePos=hit<off.Length()?eye+Vector3.Normalize(off)*Math.Max(.15f,hit-NativeViewRules.CollisionPadding):eye+off;GamePivot=eye;GameFov=NativeViewRules.ThirdFov;
            }
            else{float h=Rig.Focus(eye.Y,1,dt);GamePos=Rig.Place(new Vector3(eye.X,h,eye.Z),yaw,Pitch,Now,dt,Cast);NativeCameraRig.Basis(yaw,Pitch,out var f,out _);GamePivot=GamePos+f*Rig.Boom;GameFov=Rig.Fov(Now);}
            var i=new ChooserCameraInput{Want=want,Cut=cut,Pilot=pilot,Now=Now,Dt=dt,Body=Body,Scale=1,HasHead=HasHead,Head=Head,HasPanel=HasPanel,Panel=Panel,Facing=yaw,Side=Side,Aspect=Aspect,
                Camera=GamePos,Pivot=GamePivot,Yaw=yaw,Pitch=Pitch,Fov=GameFov,FirstPerson=M.First};
            Shot=Cam.Frame(i,Cast);return Shot;
        }
        public Vector3 Pos=>Shot.Applied?Shot.Position:GamePos;
        public float LookYaw=>Shot.Applied?Shot.Yaw:Yaw;
        public float LookPitch=>Shot.Applied?Shot.Pitch:Pitch;
        // Screen fractions (x from the left, y from the top) of a world point in the camera shown; z its depth.
        public Vector3 Screen(Vector3 p)
        {
            NativeCameraRig.Basis(LookYaw,LookPitch,out var f,out var r);var u=Vector3.Cross(f,r);var d=p-Pos;float z=Vector3.Dot(d,f);
            float tanV=(float)Math.Tan((Shot.Applied?Shot.Fov:GameFov)*Math.PI/360),tanH=tanV*Aspect;
            return new Vector3(.5f+Vector3.Dot(d,r)/z/(2*tanH),.5f-Vector3.Dot(d,u)/z/(2*tanV),z);
        }
    }
    private static float Angle(float a,float b)=>Math.Abs(NativeAimConvergence.DeltaAngle(a,b));
    // The camera's bearing round the pilot's axis, relative to its facing (+ its right).
    private static float Around(Held h,float facing){var g=h.Pos-h.Body;return NativeAimConvergence.DeltaAngle(facing,(float)(Math.Atan2(g.X,g.Z)*180/Math.PI));}

    // ---- the side the shot is taken from, against every class's real route ----
    private static void Sides(ConversionContent data,List<string> log)
    {
        var specs=data.weapons.Select(w=>w.id).Where(id=>id!="siren").ToArray();Check(specs.Length>=16&&specs.Contains("cyberlord"),"the class list");
        var layer=new NativeChipSelectPose();var shown=new CapturedRig.Pose();var notes=new List<string>();
        foreach(var spec in specs)
        {
            // The selecting look at full weight over the class's own route (ChipSelectChecks' pilot), 3 s in.
            var pilot=new ChipSelectChecks.Pilot(spec);var track=new NativeChipSelectTrack();float x=0;
            for(int f=0;f<=180;f++)
            {
                float t=f/60f;float w=track.Step(t>=.5f,7,t);pilot.Base(t);for(int b=0;b<CapturedRig.Bones.Length;b++){shown.Position[b]=pilot.Pose.Position[b];shown.Rotation[b]=pilot.Pose.Rotation[b];}shown.World();
                layer.Apply(shown,new ChipSelectInput{Weight=w,Clock=t-.3f,Time=t,Reach=1,Spec=spec,Seed=NativeChipSelect.Seed(4,9),Cards=3});layer.Measure(shown,false);
                if(f==180){Check(w>=.999f,spec+": the look is up");x=-layer.PanelCentre.X;}
            }
            // The panel's offset across the body (Unity x, + the pilot's right: the rig frame is mirrored) picks the side it is seen from.
            float side=NativeChooserCamera.SideOf(spec);Check(Math.Abs(x)<.03f||Math.Sign(x)==Math.Sign(side),spec+": the shot is taken from the hologram's side ("+x.ToString("0.00")+" m, side "+side+")");
            notes.Add(spec+(side<0?" L ":" R ")+x.ToString("+0.00;-0.00"));
        }
        // The left-side shot mirrors the right one.
        {
            var r=new Held(All[0]);var l=new Held(All[0]){Side=-1};for(int f=0;f<90;f++){r.Step(true,0,1/60f);l.Step(true,0,1/60f);}
            Check(Math.Abs(l.Shot.Angle+r.Shot.Angle)<1e-3f&&Math.Abs(l.Shot.Position.X+r.Shot.Position.X)<1e-3f&&Math.Abs(l.Shot.Position.Z-r.Shot.Position.Z)<1e-3f&&Math.Abs(NativeAimConvergence.DeltaAngle(l.Shot.Yaw,-r.Shot.Yaw))<.01f,"the left shot mirrors the right");
            Check(l.Screen(new Vector3(0,1.2f,0)).X>.5f+NativeChooserCamera.Lead*.6f,"from the left the pilot sits right of centre");
        }
        log.Add("the shot is taken from the side the wrist hologram floats on, checked over every class's real route (panel offset across the body, m): "+string.Join(", ",notes)+"; the left shot mirrors the right");
    }

    // ---- the ease ----
    private static void Timing(List<string> log)
    {
        float worstStep60=0,worstTurn60=0,worstStep240=0;
        foreach(int hz in new[]{30,60,144,240})
        {
            float dt=1f/hz;var h=new Held(All[0]);for(int f=0;f<hz/2;f++){var s=h.Step(false,0,dt);Check(!s.Applied,"nothing applied before choosing");}
            float t=0,prevW=0;var prev=h.Pos;float prevYaw=h.LookYaw;bool done=false;
            for(int f=0;f<hz;f++)
            {
                var s=h.Step(true,0,dt);t+=dt;Check(s.Applied&&s.Weight>=prevW-1e-6f,hz+" Hz: the weight rises steadily");
                if(f==0)Check(s.Weight<=NativeCameraRig.Ease(dt/NativeChooserCamera.EaseIn)+1e-5f,hz+" Hz: it starts gently (smoothstep)");
                float step=(h.Pos-prev).Length(),turn=Angle(h.LookYaw,prevYaw);if(hz==60){worstStep60=Math.Max(worstStep60,step);worstTurn60=Math.Max(worstTurn60,turn);}if(hz==240)worstStep240=Math.Max(worstStep240,step);
                prev=h.Pos;prevYaw=h.LookYaw;prevW=s.Weight;
                if(!done&&s.Weight>=1){done=true;Check(Math.Abs(t-NativeChooserCamera.EaseIn)<=dt+1e-4f,hz+" Hz: in by "+NativeChooserCamera.EaseIn+" s ("+t.ToString("0.000")+")");}
            }
            Check(done&&NativeChooserCamera.EaseIn>=.35f&&NativeChooserCamera.EaseIn<=.5f&&NativeChooserCamera.EaseOut>=.35f&&NativeChooserCamera.EaseOut<=.5f,"the swing takes .35-.5 s each way");
            t=0;prevW=1;done=false;
            for(int f=0;f<hz;f++)
            {
                var s=h.Step(false,0,dt);t+=dt;float step=(h.Pos-prev).Length();if(hz==60)worstStep60=Math.Max(worstStep60,step);if(hz==240)worstStep240=Math.Max(worstStep240,step);prev=h.Pos;
                if(s.Applied){Check(s.Weight<=prevW+1e-6f,hz+" Hz: the weight falls steadily");prevW=s.Weight;}
                else if(!done){done=true;Check(Math.Abs(t-NativeChooserCamera.EaseOut)<=dt+1e-4f,hz+" Hz: out by "+NativeChooserCamera.EaseOut+" s ("+t.ToString("0.000")+")");}
            }
            Check(done,hz+" Hz: back to the gameplay camera");
        }
        Check(worstStep60<.45f&&worstTurn60<9&&worstStep240<worstStep60*.3f,"bounded speed: "+Cm(worstStep60)+" and "+Deg(worstTurn60)+" a 60 Hz frame at most, "+Cm(worstStep240)+" at 240 Hz (no jump)");
        // A reversal mid-swing (C again at once) turns back from where the camera is.
        {
            var h=new Held(All[0]);for(int f=0;f<30;f++)h.Step(false,0,1/60f);float worst=0;var prev=h.Pos;
            for(int f=0;f<12;f++){h.Step(true,0,1/60f);worst=Math.Max(worst,(h.Pos-prev).Length());prev=h.Pos;}float peak=h.Shot.Weight;
            for(int f=0;f<40;f++){h.Step(false,0,1/60f);worst=Math.Max(worst,(h.Pos-prev).Length());prev=h.Pos;}
            Check(peak>.3f&&peak<.9f&&worst<worstStep60+1e-3f&&!h.Shot.Applied,"a reversal mid-swing continues from where it is ("+Cm(worst)+" a frame) and lands on the gameplay camera");
        }
        log.Add("ease in "+NativeChooserCamera.EaseIn+" s and out "+NativeChooserCamera.EaseOut+" s (smoothstep) at 30/60/144/240 Hz, the weight monotone each way, at most "+Cm(worstStep60)+" and "+Deg(worstTurn60)+" a 60 Hz frame ("+Cm(worstStep240)+" at 240 Hz: no jumps), a reversal mid-swing turns back in place");
    }

    // ---- the gameplay camera, exactly, before and after ----
    private static void Exact(List<string> log)
    {
        float worstStart=0,worstEnd=0,worstEnd240=0,worstLook=0;
        foreach(var m in All)foreach(int hz in new[]{60,240})foreach(float yaw in new[]{0f,137f,-250f})
        {
            float dt=1f/hz;var h=new Held(m);for(int f=0;f<hz/2;f++)h.Step(false,yaw,dt);
            var s=h.Step(true,yaw,dt);float start=(s.Position-h.GamePos).Length();if(hz==60)worstStart=Math.Max(worstStart,start);
            for(int f=0;f<hz;f++)h.Step(true,yaw,dt);
            float last=0,look=0;for(int f=0;f<hz;f++){var o=h.Step(false,yaw,dt);if(!o.Applied)break;last=(o.Position-h.GamePos).Length();look=Math.Max(Angle(o.Yaw,yaw),Math.Abs(o.Pitch-h.Pitch));}
            if(hz==60){worstEnd=Math.Max(worstEnd,last);worstLook=Math.Max(worstLook,look);}else worstEnd240=Math.Max(worstEnd240,last);
            Check(!h.Shot.Applied&&h.Cam.Weight==0&&h.Cam.Choice<0,m.Name+": released, nothing applied (the gameplay camera exactly)");
            for(int f=0;f<10;f++)Check(!h.Step(false,yaw+f,dt).Applied,m.Name+": and nothing applied afterwards");
        }
        Check(worstStart<.06f&&worstEnd<.06f&&worstLook<1.5f&&worstEnd240<worstEnd*.2f+.002f,"the first and last swung frames sit next to the gameplay camera ("+Cm(worstStart)+", "+Cm(worstEnd)+", "+Deg(worstLook)+" at 60 Hz; "+Cm(worstEnd240)+" at 240 Hz)");
        log.Add("exact gameplay camera: in every mode nothing is applied before or after the swing (the runtime rebuilds the camera from the unchanged look and restores the field of view first), the first and last swung frames within "+Cm(Math.Max(worstStart,worstEnd))+" and "+Deg(worstLook)+" of it at 60 Hz ("+Cm(worstEnd240)+" at 240 Hz)");
    }

    // ---- turning while held ----
    private static void Turning(List<string> log)
    {
        float worstOff=0,settle=0,minAround=999,maxAround=-999;
        foreach(var m in All)foreach(float turn in new[]{90f,-90f,180f,-135f})
        {
            var h=new Held(m);float dt=1/60f;for(int f=0;f<60;f++)h.Step(true,0,dt);
            for(int f=0;f<=150;f++)
            {
                float t=f*dt,yaw=t<.5f?turn*t/.5f:turn;h.Step(true,yaw,dt);float a=Around(h,yaw);minAround=Math.Min(minAround,a);maxAround=Math.Max(maxAround,a);
                var chest=h.Screen(h.Body+Vector3.UnitY*.3f);Check(chest.Z>0&&chest.X>0&&chest.X<1&&chest.Y>0&&chest.Y<1,m.Name+" turning "+turn+": the pilot stays in the frame");
                Check(Math.Abs(a)<100,m.Name+" turning "+turn+": the camera stays in front ("+a.ToString("0")+")");
            }
            float end=Around(h,turn);worstOff=Math.Max(worstOff,Math.Abs(end-NativeChooserCamera.Angles[0]));
        }
        // A steady spin (mouse held over): the shot keeps up, in front.
        {var h=new Held(All[0]);float dt=1/60f;for(int f=0;f<60;f++)h.Step(true,0,dt);float worst=0;for(int f=0;f<240;f++){float yaw=f*dt*360;h.Step(true,yaw,dt);worst=Math.Max(worst,Math.Abs(Around(h,yaw)));}settle=worst;Check(worst<90,"a 360 deg/s spin keeps the camera in front ("+worst.ToString("0")+")");}
        Check(worstOff<1,"after a turn the shot settles back on its angle ("+Deg(worstOff)+" off)");
        log.Add("a pilot turning while held (90, -90, 180, -135 deg in .5 s; every mode) stays faced and in frame: the camera keeps "+minAround.ToString("0")+" to "+maxAround.ToString("0")+" deg round from its facing, settles within "+Deg(worstOff)+" of the shot's angle, and holds within "+settle.ToString("0")+" deg through a 360 deg/s spin");
    }

    // ---- mouse jitter while the tray is up ----
    private static void Jitter(List<string> log)
    {
        var h=new Held(All[0]);float dt=1/60f;for(int f=0;f<60;f++)h.Step(true,0,dt);var rng=new Random(7);var prev=h.Pos;float prevYaw=h.LookYaw,worst=0,worstTurn=0,raw=0;float last=0;
        for(int f=0;f<300;f++){float yaw=(float)(rng.NextDouble()*3-1.5);raw=Math.Max(raw,Math.Abs(yaw-last));last=yaw;h.Step(true,yaw,dt);worst=Math.Max(worst,(h.Pos-prev).Length());worstTurn=Math.Max(worstTurn,Angle(h.LookYaw,prevYaw));prev=h.Pos;prevYaw=h.LookYaw;}
        // Unsmoothed, a 3 degree flick would carry the camera round its 2.4 m orbit by about 12.5 cm in one frame.
        float unsmoothed=2*h.Shot.Distance*(float)Math.Sin(raw*Math.PI/360);
        Check(worst<unsmoothed*.2f&&worstTurn<raw*.25f,"mouse jitter (+-1.5 deg a frame) moves the shot at most "+Cm(worst)+" and "+Deg(worstTurn)+" a frame (unsmoothed "+Cm(unsmoothed)+")");
        log.Add("mouse jitter of +-1.5 deg a frame moves the shot at most "+Cm(worst)+" and "+Deg(worstTurn)+" a 60 Hz frame (the facing eases at "+NativeChooserCamera.FacingRate+"/s; the gameplay pitch never moves it)");
    }

    // ---- every camera mode ----
    private static void Modes(List<string> log)
    {
        Vector3? shot=null;float shotYaw=0,worst=0;string worstAt="";var notes=new List<string>();
        foreach(var m in All)
        {
            float mode60=0,mode240=0;
            foreach(int hz in new[]{60,240})foreach(float pitch in new[]{-40f,12f,55f})
            {
                var h=new Held(m){Pitch=pitch};float dt=1f/hz;for(int f=0;f<hz/2;f++)h.Step(false,0,dt);var prev=h.Pos;
                for(int f=0;f<hz;f++){h.Step(true,0,dt);float step=(h.Pos-prev).Length();if(hz==60)mode60=Math.Max(mode60,step);else mode240=Math.Max(mode240,step);if(step>worst&&hz==60){worst=step;worstAt=m.Name+" pitch "+pitch;}prev=h.Pos;}
                Check(h.Shot.Weight>=1,m.Name+": swung");
                if(shot==null){shot=h.Shot.Position;shotYaw=h.Shot.Yaw;}
                Check((h.Shot.Position-shot.Value).Length()<1e-3f&&Angle(h.Shot.Yaw,shotYaw)<.01f,m.Name+" at pitch "+pitch+": the same front shot as every mode (its pitch does not move it)");
            }
            // Continuous: four times the frame rate, about a quarter of the step (a jump would not shrink).
            Check(mode60<.6f&&mode240<mode60*.3f,m.Name+": a continuous swing ("+Cm(mode60)+" a 60 Hz frame, "+Cm(mode240)+" at 240 Hz)");
            notes.Add(m.Name+" "+Cm(mode60));
        }
        log.Add("shoulder right and left, close action, classic and first person (look pitch -40, 12, 55) all swing continuously to the same front shot; the largest 60 Hz step per mode: "+string.Join(", ",notes)+" (classic starts 4.4 m out), a quarter of it or less at 240 Hz");
    }

    // ---- framing over the tray ----
    private static void Framing(List<string> log)
    {
        // The tray's dark band starts at .44 of the height (NativeExpeditionChipScreen.ExpeditionTrayLayout: cards from .56, the fade 110 of 900 above).
        const float Tray=.44f;float worstPanel=0,worstCrown=1,minCrown=1;string at="";
        foreach(float aspect in new[]{4f/3,16f/9,21f/9})foreach(int pose in new[]{0,1,2})
        {
            // Standing (the rig not posed: the defaults), standing posed (pistol), bowed (the blades, the shield: wrist at the waist).
            var h=new Held(All[0]){Aspect=aspect};
            if(pose==1){h.HasHead=h.HasPanel=true;h.Head=new Vector3(0,1.29f,0);h.Panel=new Vector3(-.07f,1.09f,.33f);}
            if(pose==2){h.HasHead=h.HasPanel=true;h.Head=new Vector3(.05f,.99f,.1f);h.Panel=new Vector3(.09f,.67f,.46f);}
            float dt=1/60f;for(int f=0;f<180;f++)h.Step(true,0,dt);
            float headY=pose==0?Centre+NativeChooserCamera.HeadUp:h.Head.Y;var panelAt=pose==0?new Vector3(0,Centre+NativeChooserCamera.PanelUp,.33f):h.Panel;
            var crown=h.Screen(new Vector3(0,headY+.35f,0));var panel=h.Screen(panelAt);var axis=h.Screen(new Vector3(0,(headY+panelAt.Y)/2,0));
            Check(crown.Y>.02f&&crown.Y<.16f,"aspect "+aspect.ToString("0.00")+" pose "+pose+": the crown sits near the top ("+crown.Y.ToString("0.00")+")");
            Check(panel.Y<Tray-.03f&&panel.X>.15f&&panel.X<.85f,"aspect "+aspect.ToString("0.00")+" pose "+pose+": the hologram clear over the tray ("+panel.X.ToString("0.00")+", "+panel.Y.ToString("0.00")+")");
            Check(axis.X<.5f-NativeChooserCamera.Lead*.6f&&axis.X>.5f-NativeChooserCamera.Lead*1.6f,"aspect "+aspect.ToString("0.00")+" pose "+pose+": the pilot a little left of centre ("+axis.X.ToString("0.00")+")");
            if(panel.Y>worstPanel){worstPanel=panel.Y;at="aspect "+aspect.ToString("0.00")+" pose "+pose;}worstCrown=Math.Min(worstCrown,1-crown.Y);minCrown=Math.Min(minCrown,crown.Y);
            Check(Math.Abs(h.Shot.Angle-NativeChooserCamera.Angles[0])<.01f&&h.Shot.Fov==NativeChooserCamera.Fov,"the preferred three-quarter angle in the open");
        }
        log.Add("framing: crown at "+minCrown.ToString("0.00")+"+ of the height, the wrist hologram at most "+worstPanel.ToString("0.00")+" down ("+at+"; the tray's band starts at "+Tray+"), standing or bowed, 4:3 to 21:9, the pilot off centre by about "+NativeChooserCamera.Lead+" of the width");
    }

    // ---- collision: never inside the world, the next clear angle ----
    private static void Collision(List<string> log)
    {
        var worlds=new (string name,Box[] boxes,Func<float,bool> expect)[]
        {
            ("a wall at the right",new[]{new Box(new Vector3(1.05f,1.5f,0),new Vector3(.25f,1.5f,4))},a=>a<0),
            ("facing a wall",new[]{new Box(new Vector3(0,1.5f,1.45f),new Vector3(4,1.5f,.25f))},a=>Math.Abs(a)>=59),
            ("a corridor across",new[]{new Box(new Vector3(0,1.5f,1.05f),new Vector3(4,1.5f,.25f)),new Box(new Vector3(0,1.5f,-1.05f),new Vector3(4,1.5f,.25f))},a=>Math.Abs(Math.Abs(a)-90)<1),
            ("a doorway's corner",new[]{new Box(new Vector3(1.3f,1.5f,1.3f),new Vector3(.6f,1.5f,.6f))},a=>Math.Abs(a)<=91),
            ("a closet",new[]{new Box(new Vector3(1.15f,1.5f,0),new Vector3(.25f,1.5f,2)),new Box(new Vector3(-1.15f,1.5f,0),new Vector3(.25f,1.5f,2)),new Box(new Vector3(0,1.5f,1.15f),new Vector3(2,1.5f,.25f)),new Box(new Vector3(0,1.5f,-1.15f),new Vector3(2,1.5f,.25f))},a=>true),
            ("under a low ceiling",new[]{new Box(new Vector3(0,2.15f,0),new Vector3(6,.2f,6))},a=>true),
        };
        float nearest=99;string nearestAt="";var notes=new List<string>();
        foreach(var w in worlds)foreach(var m in All)
        {
            var h=new Held(m){Cast=CastOf(w.boxes)};float dt=1/60f;
            void Clear(string when){foreach(var b in w.boxes){float d=b.Distance(h.Pos);if(d<nearest){nearest=d;nearestAt=w.name+", "+m.Name+", "+when;}Check(d>.05f,w.name+", "+m.Name+": the camera stays out of the world "+when+" ("+Cm(d)+")");}}
            for(int f=0;f<30;f++)h.Step(false,0,dt);
            for(int f=0;f<60;f++){h.Step(true,0,dt);Clear("swinging in");}
            int changes=0,choice=h.Cam.Choice;for(int f=0;f<120;f++){h.Step(true,(float)Math.Sin(f*.3f)*2,dt);Clear("held");if(h.Cam.Choice!=choice){changes++;choice=h.Cam.Choice;}}
            Check(changes<=1,w.name+", "+m.Name+": the fallback holds its choice under small turns ("+changes+" changes)");
            Check(w.expect(h.Shot.Angle),w.name+", "+m.Name+": the fallback angle "+h.Shot.Angle.ToString("0"));
            if(m==All[0])notes.Add(w.name+" "+h.Shot.Angle.ToString("0")+" deg"+(h.Shot.Clear<1?" at "+(h.Shot.Clear*100).ToString("0")+"% distance":""));
            for(int f=0;f<40;f++){h.Step(false,0,dt);Clear("swinging back");}Check(!h.Shot.Applied,w.name+", "+m.Name+": back to the gameplay camera");
        }
        // In the open the preferred angle; a wall arriving later moves the shot (eased) and the camera is never inside it.
        {
            var boxes=new List<Box>();var h=new Held(All[0]){Cast=CastOf(boxes)};float dt=1/60f;for(int f=0;f<60;f++)h.Step(true,0,dt);Check(h.Cam.Choice==0,"the open takes the preferred angle");
            boxes.Add(new Box(new Vector3(2.2f,1.5f,1.6f),new Vector3(.3f,1.5f,.3f)));var prev=h.Pos;float worst=0;
            for(int f=0;f<90;f++){h.Step(true,0,dt);worst=Math.Max(worst,(h.Pos-prev).Length());prev=h.Pos;Check(boxes[0].Distance(h.Pos)>.05f,"a pillar appearing never puts the camera inside it");}
            Check(worst<.5f,"the pull-in is immediate and bounded ("+Cm(worst)+")");
        }
        log.Add("collision ("+worlds.Length+" spots x 5 modes, swinging in, held and back): the camera keeps at least "+Cm(nearest)+" from the world ("+nearestAt+"); fallbacks: "+string.Join("; ",notes)+"; a held choice survives small turns");
    }

    // ---- first person ----
    private static void FirstPerson(List<string> log)
    {
        var fp=All[4];float worstBehind=0,clearAt=0,hidden=0;
        foreach(float yaw in new[]{0f,90f,-160f})foreach(int hz in new[]{60,144})
        {
            var h=new Held(fp);float dt=1f/hz;for(int f=0;f<hz/4;f++)h.Step(false,yaw,dt);bool shown=false;var dir=new Vector3((float)Math.Sin(yaw*Math.PI/180),0,(float)Math.Cos(yaw*Math.PI/180));
            for(int f=0;f<hz;f++)
            {
                var s=h.Step(true,yaw,dt);var off=s.Position-h.Eye;
                if(!s.Body){Check(!shown,"first person: once drawn, my body stays drawn while swung in");worstBehind=Math.Min(worstBehind,Vector3.Dot(off,dir));Check(off.Length()<NativeChooserCamera.HeadClear+1e-4f,"hidden only within the head's clearance");}
                else if(!shown)
                {
                    shown=true;clearAt=Math.Max(clearAt,s.Weight);Check(off.Length()>=NativeChooserCamera.HeadClear-1e-4f,"my body is drawn once the camera is "+NativeChooserCamera.HeadClear+" m from the eye");
                    // It appears out of view: the head is outside the camera's field.
                    var head=h.Screen(h.Eye+Vector3.UnitY*.2f);hidden=Math.Max(hidden,head.Z);Check(head.Z<0||head.X<0||head.X>1||head.Y<0||head.Y>1,"my body appears out of view ("+head+")");
                }
            }
            Check(shown&&h.Shot.Body&&h.Shot.Weight>=1,"first person: the front shot draws my body");
            bool back=false;for(int f=0;f<hz;f++){var s=h.Step(false,yaw,dt);if(!s.Applied){back=true;break;}if(!s.Body)Check((s.Position-h.Eye).Length()<NativeChooserCamera.HeadClear+1e-4f,"swinging back, my body hides only at the head");}
            Check(back,"first person returns (nothing applied: UpdateCamera's first person, view model and all)");
        }
        Check(worstBehind>-1e-3f,"the first-person camera leaves through the front of the face ("+Cm(worstBehind)+")");
        log.Add("first person: the camera leaves through the front of the face, my body is drawn from "+NativeChooserCamera.HeadClear+" m off the eye (weight "+clearAt.ToString("0.00")+" at most) while still out of view, and first person returns when the swing ends");
    }

    // ---- death, extraction, cutscenes, spectating, other screens ----
    private static void Handoff(List<string> log)
    {
        float dt=1/60f;
        // Death mid-choose (the authority clears choosing, the tray cannot show): the swing eases out onto the death camera (the same rig).
        {var h=new Held(All[0]);for(int f=0;f<60;f++)h.Step(true,0,dt);float prevW=1;int frames=0;while(h.Step(false,0,dt).Applied){Check(h.Shot.Weight<prevW,"a death mid-choose eases out");prevW=h.Shot.Weight;frames++;}
         Check(Math.Abs(frames*dt-NativeChooserCamera.EaseOut)<=dt*1.5f,"onto the death camera in "+NativeChooserCamera.EaseOut+" s");}
        // A cutscene or a spectated pilot owns the frame: dropped at once, and a later hold starts again from the gameplay camera.
        {var h=new Held(All[0]);for(int f=0;f<60;f++)h.Step(true,0,dt);Check(!h.Step(true,0,dt,cut:true).Applied&&h.Cam.Weight==0,"a cutscene or spectating drops the swing at once");
         var s=h.Step(true,0,dt);Check(s.Applied&&s.Weight<.01f,"and a hold after it swings in again from the gameplay camera");}
        // A different view pilot (spectating a teammate, possession) drops it at once.
        {var h=new Held(All[0]);for(int f=0;f<60;f++)h.Step(true,0,dt);var s=h.Step(true,0,dt,pilot:2);Check(s.Weight<.01f,"another view pilot drops the swing");}
        // A gap (the killcam, the stage loading screen, the J screen, a draft or intro skipped LateFrame's camera): dropped at once.
        {var h=new Held(All[0]);for(int f=0;f<60;f++)h.Step(true,0,dt);h.Now+=NativeChooserCamera.Stale+.05f;var s=h.Step(false,0,dt);Check(!s.Applied,"a frame gap (another screen) drops the swing");}
        log.Add("hand-off: death or extraction mid-choose eases out over "+NativeChooserCamera.EaseOut+" s onto the death camera; a cutscene, a spectated or other view pilot, or any screen that skipped the live camera for "+NativeChooserCamera.Stale+" s drops it at once, and a later hold swings in again");
    }

    private static void Allocation(List<string> log)
    {
        var cast=CastOf(new[]{new Box(new Vector3(1.05f,1.5f,0),new Vector3(.25f,1.5f,4))});var cam=new NativeChooserCamera();float t=0;
        ChooserCameraInput I(bool want,float yaw)=>new ChooserCameraInput{Want=want,Pilot=1,Now=t,Dt=1/60f,Body=new Vector3(0,Centre,0),Scale=1,HasHead=true,Head=new Vector3(0,1.29f,0),HasPanel=true,Panel=new Vector3(0,1.09f,.33f),
            Facing=yaw,Aspect=16f/9,Camera=new Vector3(.6f,1.6f,-2.8f),Pivot=new Vector3(.6f,1.6f,0),Yaw=yaw,Pitch=12,Fov=55};
        for(int i=0;i<200;i++){t+=1/60f;cam.Frame(I(i%90<60,i*.5f),cast);}
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<3000;i++){t+=1/60f;cam.Frame(I(i%90<60,i*.5f),cast);}
        long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"no allocation per frame ("+used+" bytes)");
        log.Add("no allocation per frame (3000 frames swinging in, held and out against a wall)");
    }

    // ---- the runtime hooks ----
    private static void Hooks(string root,List<string> log)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
        string cg=R("Runtime/ConversionGame.cs"),cc=R("Runtime/NativeChooserCameraPresentation.cs"),core=R("Core/NativeChooserCamera.cs"),hud=R("Runtime/NativeCombatHud.cs"),tag=R("Runtime/NativeChipSelectHud.cs");
        // After the aim (the sent look and AimTarget are the gameplay camera's) and before every shake (one shake, on the camera shown).
        int target=cg.IndexOf("pending.AbilityTarget=NativeArena.Vec(AimPoint(",StringComparison.Ordinal),converge=cg.IndexOf("ConvergeAim(p);",StringComparison.Ordinal),swing=cg.IndexOf("ChooserCamera();",StringComparison.Ordinal),shake=cg.IndexOf("NativeShakeRules.Offset(",StringComparison.Ordinal),
            structure=cg.IndexOf("presentation.StructureShake(",StringComparison.Ordinal),kick=cg.IndexOf("presentation.HitKick(",StringComparison.Ordinal),render=cg.IndexOf("presentation.Render(frame,renderTime,false);",StringComparison.Ordinal);
        Check(target>0&&converge>target&&swing>converge&&shake>swing&&structure>swing&&kick>swing&&render>kick,"the swing follows the aim and precedes the shakes, the hit kick and the render");
        Check(Regex.Matches(cg,@"\bChooserCamera\(\);").Count==1,"placed once a frame");
        // UpdateCamera restores the gameplay field of view first, then rebuilds the camera from the unchanged look.
        int update=cg.IndexOf("private void UpdateCamera()",StringComparison.Ordinal),restore=cg.IndexOf("ChooserCameraRestore();",StringComparison.Ordinal),direct=cg.IndexOf("if(cinematic.Direct(arena,",StringComparison.Ordinal),fov=cg.IndexOf("cameraView.fieldOfView=SprintFov(",StringComparison.Ordinal);
        Check(update>0&&restore>update&&direct>restore&&fov>restore,"UpdateCamera restores the gameplay field of view first");
        Check(cc.Contains("private void ChooserCameraRestore(){if(float.IsNaN(chooserFov))return;if(cameraView!=null)cameraView.fieldOfView=chooserFov;chooserFov=float.NaN;}")&&cc.Contains("chooserFov=cameraView.fieldOfView;t.SetPositionAndRotation("),"the swung field of view is undone before the next gameplay ease");
        // Presentation only: no input, look, aim or authority touched; the client's play gate unchanged.
        Check(!cc.Contains("pending")&&!cc.Contains("Input.")&&!Regex.IsMatch(cc,@"(?<![\w.])(yaw|pitch)\s*[-+*/]?=[^=]")&&!cc.Contains("ExpeditionLiveSubmit")&&!cc.Contains("SetChoosing")&&!cc.Contains("lockPoint")&&!cc.Contains("Cursor."),"the swing reads the look and changes no input, look, lock or cursor");
        Check(!Regex.IsMatch(cg,@"play=[^;]*Chooser")&&!Regex.IsMatch(cg,@"look=[^;]*Chooser"),"no play or mouse-look gate on the swing");
        Check(cc.Contains("expeditionPick.Open||ExpeditionFollowing||ExpeditionMyPlant(Time.unscaledTime-expeditionPlantAt)||ExpeditionPickFlow.ChoosingNow(v)")&&cc.Contains("if(v==null||arena?.Local==null||!ExpeditionPickCanShow)return false;")&&cc.Contains("Want=p==arena.Local&&ExpeditionChooserHeld"),"held: the tray, its follow-through and plant, or the authority's flag, only while the tray could show, for my own pilot");
        Check(cc.Contains("bool cut=Spectator!=null||cinematic.Directing||arena.Generation!=chooserGeneration;"),"a spectated pilot, a cutscene or a new area drops it");
        Check(cc.Contains("viewModelCamera.enabled=false;presentation.AimHeld=false;if(shot.Body)presentation.FirstPerson=false;"),"first person: the view model off, my body drawn once clear");
        Check(cc.Contains("Vector3 pivot=shoulderView&&!presentation.FirstPerson?cam+t.forward*cameraRig.Boom:body+Vector3.up*NativeViewRules.EyeHeight;"),"the path starts from the shoulder rig's own fitting point");
        Check(cc.Contains("Physics.SphereCast(V(from),radius,V(direction),out var hit,distance,NativeArena.WorldMask,QueryTriggerInteraction.Ignore)"),"the shoulder rigs' world cast");
        Check(cc.Contains("Side=NativeChooserCamera.SideOf(p.Seat.spec)"),"the shot's side is the pilot's class's");
        // The HUD: no crosshair and nothing over my own pilot under the shot (other pilots' tags and bars unchanged).
        Check(cg.Contains("&&!presentation.ScopeActive&&!ChooserFramed)Crosshair();")&&hud.Contains("||ChooserFramed&&p==arena.ViewPilot||")&&tag.Contains("(FirstPersonView||ChooserFramed)&&p==arena.ViewPilot"),"no crosshair, my own health and SELECTING CHIP hidden under the shot");
        Check(!core.Contains("UnityEngine")&&!core.Contains("Random")&&!core.Contains("DateTime"),"the Core is engine-free and deterministic");
        log.Add("runtime: placed once a frame after AimTarget/AbilityTarget and the convergence and before the shakes and the hit kick; UpdateCamera restores the field of view first; no input, look, lock, cursor or authority touched and no play gate; held from the tray, its plant or the authority's flag; dropped for a spectated pilot, a cutscene or a new area; first person's view model off and my body drawn once clear; no crosshair or own overhead HUD under the shot");
    }
}
