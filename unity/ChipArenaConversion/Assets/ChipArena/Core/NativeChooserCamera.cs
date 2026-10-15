using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The local view this frame for NativeChooserCamera.Frame (Unity axes: yaw clockwise from +Z, pitch positive down;
    /// unscaled seconds).</summary>
    public struct ChooserCameraInput
    {
        /// <summary>The local pilot is held choosing a chip (the tray wanted open, a pick's follow-through or plant, or the authority's
        /// public choosing flag for me).</summary>
        public bool Want;
        /// <summary>Another camera owns this frame (a cutscene, a spectated pilot): the swing drops at once.</summary>
        public bool Cut;
        /// <summary>The view pilot's seat; a change drops the swing at once.</summary>
        public int Pilot;
        public float Now,Dt;
        /// <summary>The pilot's body centre (the capsule centre, the rig root) and its rig scale (1 for a pilot).</summary>
        public Vector3 Body;public float Scale;
        /// <summary>The drawn rig's head bone and the wrist hologram's panel centre (world, last posed), when known: the shot frames
        /// from the crown to under the hologram, so a bowed look (the blades, the shield) keeps its low wrist over the tray.</summary>
        public bool HasHead,HasPanel;public Vector3 Head,Panel;
        /// <summary>The yaw the pilot faces: the client's look yaw, which the authority's hold turns the chooser to.</summary>
        public float Facing;
        /// <summary>The side the shot is taken from: +1 the pilot's front right, -1 its front left (NativeChooserCamera.SideOf its class).</summary>
        public float Side;
        public float Aspect;
        /// <summary>The gameplay camera as UpdateCamera placed it this frame: position, the point it is fitted from (the shoulder point
        /// of a shoulder rig, else the eye), look and field of view, and whether it is first person.</summary>
        public Vector3 Camera,Pivot;public float Yaw,Pitch,Fov;public bool FirstPerson;
    }

    /// <summary>The camera NativeChooserCamera places this frame; Applied false leaves the gameplay camera exactly as it is.</summary>
    public struct ChooserCameraShot
    {
        public bool Applied;
        /// <summary>The eased swing, 0 (gameplay) to 1 (the front shot).</summary>
        public float Weight;
        public Vector3 Position;public float Yaw,Pitch,Fov;
        /// <summary>My body is drawn (third person; first person once the camera is clear of the head).</summary>
        public bool Body;
        /// <summary>The front shot's angle off the pilot's facing (degrees, + its right), its horizontal distance (m) and how clear the
        /// fallback search found its angle (0-1).</summary>
        public float Angle,Distance,Clear;
    }

    /// <summary>
    /// The Expedition chooser camera (owner 2026-10-15: "Lets swing the camera to us when choosing,"). While the local pilot is held
    /// choosing a chip (Astra's hold, PR #237: movement and actions cancelled, camera yaw and pitch kept, still hittable), the camera
    /// eases (EaseIn, smoothstep) from wherever the gameplay camera is round to a front three-quarter shot from the side its wrist
    /// hologram floats on (SideOf: the panel clear of the body, not behind a bladed stance's shoulder): crown to just under the wrist hologram in the upper part of the screen (the tray covers the lower half),
    /// the pilot a little off centre away from the way it faces. It holds through every queued pick and the final plant, then eases
    /// back the same way as the hold releases; at weight 0 nothing is applied, so the gameplay camera is exactly what it would be.
    ///  - The path orbits the pilot's vertical axis: angle, horizontal distance and height interpolate from the gameplay camera's to
    ///    the front shot's, the look's yaw and pitch from the gameplay look's, each the same way round for the whole swing (unwrapped
    ///    frame to frame, so a turn mid-swing never flips it). First person starts its orbit in front of the face, so the camera leaves
    ///    through the front and turns round; my body is drawn once the camera is HeadClear from the eye, while it is still out of view.
    ///  - The front shot follows the pilot's facing (the look yaw, eased at FacingRate), so a pilot turning while held stays faced and
    ///    mouse jitter never shakes the framing; the gameplay pitch does not move it. Its heights follow the drawn head and hologram
    ///    (eased at FrameRate), its distance fits them to SpanAt of the screen.
    ///  - World collision as the shoulder rigs do it (NativeCameraRig's sphere radius and padding): the front shot tries the preferred
    ///    angle and then the next clear ones (wider, the side view, the other side, straight on, behind either shoulder), keeps a
    ///    working choice for Dwell, and eases between them; every frame the camera is then cast from the point it orbits (the gameplay
    ///    camera's own fitting point blending to the eye) and pulled in at once, eased back out, as the boom is.
    /// Presentation only: it never touches the input look, the aim, AimTarget or the wire (Runtime/NativeChooserCameraPresentation.cs
    /// applies it after the aim and before the shakes). No allocation per frame.
    /// </summary>
    public sealed class NativeChooserCamera
    {
        /// <summary>The swing's ease in and out (s, smoothstep on the progress; a reversal mid-swing continues from where it is).</summary>
        public const float EaseIn=.42f,EaseOut=.42f;
        /// <summary>The front shot's field of view, and its framing: the crown over the head bone and the frame's lower edge under the
        /// hologram's centre (m, by rig scale; standing defaults over the body centre when the rig is not posed), the span between them
        /// filling SpanAt of the screen's height with its top TopAt from the top edge, the camera CameraAt of the way down the span, and
        /// the pilot Lead of the width off centre.</summary>
        public const float Fov=50,Crown=.4f,Below=.17f,HeadUp=.425f,PanelUp=.225f,SpanAt=.33f,TopAt=.06f,CameraAt=.3f,Lead=.1f;
        /// <summary>The front shot's horizontal distance from the pilot's axis is kept within these (m, by rig scale).</summary>
        public const float MinDistance=1.6f,MaxDistance=3.4f;
        /// <summary>Eases (1/s): the shot following the pilot's facing, moving between fallback angles, and its heights following the pose.</summary>
        public const float FacingRate=9,AngleRate=5,FrameRate=4;
        /// <summary>A working fallback angle is kept this long (s) before a preferred one that cleared is taken back; and kept while at
        /// least Keep of its distance is clear.</summary>
        public const float Dwell=.4f,Keep=.85f;
        /// <summary>The world cast's sphere and padding (the shoulder rigs'), and the eased push-out after a pull-in (1/s): twice the
        /// boom's, as the swing sweeps past an obstruction far faster than a gameplay camera moves.</summary>
        public const float Radius=NativeCameraRig.Radius,Padding=NativeCameraRig.BoomPadding,BoomRelease=12;
        /// <summary>First person: my body is drawn once the camera is this far from the eye (m, by rig scale).</summary>
        public const float HeadClear=.45f;
        /// <summary>A gap between frames that drops the swing (another screen owned the camera), as NativeCameraRig.Stale.</summary>
        public const float Stale=NativeCameraRig.Stale;
        /// <summary>The weight from which the HUD treats the view as the chooser shot (no crosshair, nothing over my own pilot).</summary>
        public const float HudFrom=.35f;
        /// <summary>The front shot's angles off the facing (+ toward the Side; mirrored for the left), preferred first: the three-quarter
        /// view, wider, the side view, the same on the other side, straight on, then behind either shoulder when nothing else fits.</summary>
        public static readonly float[] Angles={35,60,90,-35,-60,-90,0,125,-125};

        private readonly float[] clear=new float[9];
        private float u,placedAt=-100,facing,angle,top,low,allowed=float.MaxValue,choiceAt,orbitD,lookD;private int pilot=int.MinValue,choice=-1;private bool has,ends=true;
        /// <summary>The swing's progress (0-1) and its eased weight.</summary>
        public float Progress=>u;
        public float Weight=>NativeCameraRig.Ease(u);
        /// <summary>The fallback candidate in use (an index of Angles), -1 when not swung.</summary>
        public int Choice=>choice;
        /// <summary>The side a class's shot is taken from: where its selecting look holds the wrist hologram (the panel's offset across the
        /// body, checked against every class's real route in ChooserCameraChecks): the left for the pistol, the shield and the gauntlets,
        /// whose display wrist is out in front of the left side; the right for the long guns, the blades, the Cyber Lord's open hands, the
        /// staffs and the bow, whose stance turns the chest (and the left wrist with it) to the right, or whose display wrist is the right.</summary>
        public static float SideOf(string spec)
        {
            switch(NativeChipSelect.Carry(spec)){case ChipSelectCarry.Pistol:case ChipSelectCarry.Shield:case ChipSelectCarry.Fist:return -1;default:return 1;}
        }
        /// <summary>Drops the swing at once (the gameplay camera exactly, from the next frame).</summary>
        public void Cut(){u=0;has=false;choice=-1;allowed=float.MaxValue;ends=true;}

        private static float Rad(float deg)=>deg*(float)Math.PI/180;
        private static float Deg(double rad)=>(float)(rad*180/Math.PI);
        private static Vector3 Flat(float yaw){float r=Rad(yaw);return new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r));}
        private static float Ease(float dt,float rate)=>1-(float)Math.Exp(-rate*dt);
        // A turn of `d` degrees (-180..180) taken the `dir` way round (+ or -).
        private static float Toward(float d,float dir)=>dir>0?(d<0?d+360:d):(d>0?d-360:d);

        /// <summary>One frame: steps the swing toward Want and returns the camera to use (Applied false at weight 0).</summary>
        public ChooserCameraShot Frame(in ChooserCameraInput i,NativeCameraRig.Cast cast)
        {
            bool stale=i.Now-placedAt>Stale||i.Now<placedAt||i.Pilot!=pilot;placedAt=i.Now;pilot=i.Pilot;
            if(stale||i.Cut)Cut();if(i.Cut)return default;
            float dt=Math.Max(0,Math.Min(i.Dt,.1f));
            u=i.Want?Math.Min(1,u+dt/EaseIn):Math.Max(0,u-dt/EaseOut);
            if(u<=0){Cut();return default;}
            float e=NativeCameraRig.Ease(u),s=i.Scale>0?i.Scale:1,side=i.Side<0?-1:1;bool fresh=!has;has=true;
            facing=fresh?i.Facing:facing+NativeAimConvergence.DeltaAngle(facing,i.Facing)*Ease(dt,FacingRate);
            var eye=i.Body+Vector3.UnitY*(NativeViewRules.EyeHeight*s);

            // The framing: crown to under the hologram, eased; the distance that fits it and the camera's height on it.
            float topW=(i.HasHead?i.Head.Y:i.Body.Y+HeadUp*s)+Crown*s,lowW=Math.Min((i.HasPanel?i.Panel.Y:i.Body.Y+PanelUp*s)-Below*s,topW-.3f*s);
            if(fresh){top=topW;low=lowW;}else{float k=Ease(dt,FrameRate);top+=(topW-top)*k;low+=(lowW-low)*k;}
            float tanV=(float)Math.Tan(Rad(Fov*.5f)),span=Math.Max(.3f*s,top-low),reach=Math.Max(MinDistance*s,Math.Min(MaxDistance*s,span/(2*tanV*SpanAt))),rise=top-span*CameraAt-eye.Y;

            float score=Choose(eye,side,reach,rise,i.Now,fresh,cast),want=side*Angles[choice];
            angle=fresh?want:angle+NativeAimConvergence.DeltaAngle(angle,want)*Ease(dt,AngleRate);

            // The front shot: on the eye's orbit, looking across the pilot's axis with the crown TopAt from the top and the pilot Lead
            // off centre (turned toward the camera's side: the pilot sits away from the way it faces).
            var front=eye+Flat(facing+angle)*reach+Vector3.UnitY*rise;
            float tanH=tanV*Math.Max(.2f,i.Aspect),sideOf=Math.Max(-1,Math.Min(1,angle/20));
            float yawF=Deg(Math.Atan2(i.Body.X-front.X,i.Body.Z-front.Z))+Deg(Math.Atan(2*Lead*tanH))*sideOf;
            float pitchF=-(Deg(Math.Atan2(top-front.Y,reach))-Deg(Math.Atan((.5f-TopAt)*2*tanV)));

            Vector3 position,pivot;float yaw,pitch,fov;
            if(e>=1-1e-6f){position=front;pivot=eye;yaw=yawF;pitch=pitchF;fov=Fov;ends=true;}
            else
            {
                // The orbit about the pilot's axis from the gameplay camera (first person: from in front of the face).
                var g=i.Camera-i.Body;float rG=(float)Math.Sqrt(g.X*g.X+g.Z*g.Z),aG=rG<.05f*s?facing:Deg(Math.Atan2(g.X,g.Z)),aF=facing+angle;
                float rawOrbit=NativeAimConvergence.DeltaAngle(aG,aF),rawLook=NativeAimConvergence.DeltaAngle(i.Yaw,yawF);
                if(ends)
                {
                    // Each way round is decided at an end of the swing and then followed continuously: the orbit the short way, or
                    // from behind round the side the shot ends on; the look the short way, or when nearly opposite as the orbit turns.
                    float endSide=angle>1e-3f?1:angle<-1e-3f?-1:side;
                    orbitD=Math.Abs(rawOrbit)>170?Toward(rawOrbit,-endSide):rawOrbit;
                    float orbitDir=orbitD>1e-3f?1:orbitD<-1e-3f?-1:-endSide;
                    lookD=Math.Abs(rawLook)>150?Toward(rawLook,orbitDir):rawLook;
                }
                else{orbitD+=NativeAimConvergence.DeltaAngle(orbitD,rawOrbit);lookD+=NativeAimConvergence.DeltaAngle(lookD,rawLook);}
                ends=false;
                float a=Rad(aG+orbitD*e),r=rG+(reach-rG)*e;
                position=new Vector3(i.Body.X+(float)Math.Sin(a)*r,i.Camera.Y+(front.Y-i.Camera.Y)*e,i.Body.Z+(float)Math.Cos(a)*r);
                yaw=i.Yaw+lookD*e;pitch=i.Pitch+(pitchF-i.Pitch)*e;fov=i.Fov+(Fov-i.Fov)*e;pivot=Vector3.Lerp(i.Pivot,eye,e);
            }

            // Collision along the path, as the boom: cast from the point the camera orbits, pulled in at once, eased back out. The ease's
            // lag fades in over the first fifth of the swing so the gameplay camera (clear from its own fitting point) is met exactly.
            var off=position-pivot;float len=off.Length();
            if(len>1e-4f)
            {
                var dir=off/len;float hit=cast(pivot,dir,len,Radius),target=hit>=len-1e-5f?float.MaxValue:Math.Max(0,hit-Padding);
                if(fresh||target<=allowed)allowed=target;
                else if(target==float.MaxValue&&allowed>=len-.005f)allowed=float.MaxValue;
                else allowed+=(Math.Min(target,len+.01f)-allowed)*Ease(dt,BoomRelease);
                float ramp=e<.2f?len*(1-e/.2f):0,dist=Math.Min(len,Math.Min(target,Math.Max(allowed,ramp)));
                position=pivot+dir*dist;
            }
            bool body=!i.FirstPerson||(position-eye).Length()>=HeadClear*s;
            return new ChooserCameraShot{Applied=true,Weight=e,Position=position,Yaw=yaw,Pitch=pitch,Fov=fov,Body=body,Angle=angle,Distance=reach,Clear=score};
        }

        // The fallback search: each candidate's clear fraction (a sphere cast from the eye to its camera point), the first fully clear
        // in order, else the clearest; a working choice is kept for Dwell and while Keep of it stays clear. Returns its fraction.
        private float Choose(Vector3 eye,float side,float reach,float rise,float now,bool fresh,NativeCameraRig.Cast cast)
        {
            int best=-1,clearest=0;
            for(int k=0;k<Angles.Length;k++)
            {
                var o=Flat(facing+side*Angles[k])*reach+Vector3.UnitY*rise;float len=o.Length(),hit=cast(eye,o/len,len,Radius);
                clear[k]=hit>=len-1e-4f?1:Math.Max(0,hit-Padding)/len;
                if(best<0&&clear[k]>=1)best=k;if(clear[k]>clear[clearest]+.02f)clearest=k;
            }
            if(best<0)best=clearest;
            if(fresh||choice<0){choice=best;choiceAt=now;return clear[choice];}
            if(best!=choice)
            {
                bool working=clear[choice]>=Keep,preferred=best<choice&&clear[best]>=1&&now-choiceAt>=Dwell,better=clear[best]>=1&&!working||clear[best]>clear[choice]+.15f;
                if(!working||preferred||better){choice=best;choiceAt=now;}
            }
            return clear[choice];
        }
    }
}
