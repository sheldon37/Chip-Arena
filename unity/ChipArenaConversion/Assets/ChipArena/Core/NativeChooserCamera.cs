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
        /// <summary>The drawn rig's head bone and the display wrist (the chip screen's emitter; world, last posed), when known.</summary>
        public bool HasHead,HasWrist;public Vector3 Head,Wrist;
        /// <summary>The yaw the pilot faces (the look the authority last turned it to; held still while choosing).</summary>
        public float Facing;
        /// <summary>The chip screen floats on the pilot's left (NativeChipScreen.OnLeft: the display wrist's side, or the side a
        /// two-handed hold carries it to; NativeChipScreen.Place); the camera stands in the other front quarter.</summary>
        public bool ScreenLeft;
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
        /// <summary>The front shot's angle off the pilot's facing (degrees, + its right), its distance from what it frames (m) and how
        /// clear the fallback search found its angle (0-1).</summary>
        public float Angle,Distance,Clear;
    }

    /// <summary>
    /// The Expedition chooser camera (owner 2026-10-15: "Lets swing the camera to us when choosing,"; then "...make it a holographic screen
    /// projected from the wrist. That way its easier to notice were selecting a chip"). While the local pilot is held choosing a chip, the
    /// camera eases (EaseIn, smoothstep) from wherever the gameplay camera is round to a front three-quarter shot that frames the pilot and
    /// the chip screen its wrist projects (Core/NativeChipScreen.cs): the screen floats out on the display wrist's side and the camera
    /// stands in the other front quarter, so the pilot, the beam from the wrist and the cards face it, the cards large. It holds through
    /// every queued pick and the final plant, then eases back the same way as the hold releases; at weight 0 nothing is applied, so the
    /// gameplay camera is exactly what it would be.
    ///  - The shot looks a little down across the centre of what it frames (the crown, the wrist, the far shoulder and the screen's
    ///    corners) and stands back until all of it fits the frame's margins (FitX, FitY).
    ///  - The path orbits the pilot's vertical axis: angle, horizontal distance and height interpolate from the gameplay camera's to the
    ///    front shot's, the look's yaw and pitch from the gameplay look's, each the same way round for the whole swing (unwrapped frame to
    ///    frame). First person starts its orbit in front of the face, so the camera leaves through the front and turns round; my body is
    ///    drawn once the camera is HeadClear from the eye, while it is still out of view.
    ///  - The shot follows the pilot's facing (eased at FacingRate; the look is held while choosing), its framing the pose (FrameRate).
    ///  - World collision as the shoulder rigs do it (NativeCameraRig's sphere radius and padding): the shot tries the preferred angle and
    ///    then the next clear ones (wider, the side view, the screen's side, straight on, behind either shoulder), keeps a working choice
    ///    for Dwell, and eases between them; every frame the camera is then cast from the point it orbits (the gameplay camera's own
    ///    fitting point blending to the eye) and pulled in at once, eased back out.
    /// Presentation only: it never touches the aim, AimTarget or the wire (Runtime/NativeChooserCameraPresentation.cs applies it after the
    /// aim and before the shakes). No allocation per frame.
    /// </summary>
    public sealed class NativeChooserCamera
    {
        /// <summary>The swing's ease in and out (s, smoothstep on the progress; a reversal mid-swing continues from where it is).</summary>
        public const float EaseIn=.42f,EaseOut=.42f;
        /// <summary>The front shot's field of view, its look down (degrees), and the frame's share the framed points may fill across and
        /// up; the crown over the head bone, and standing defaults over the body centre when the rig is not posed (m, by rig scale).</summary>
        public const float Fov=50,Down=7,FitX=.78f,FitY=.74f,Crown=.4f,HeadUp=.425f,WristUp=.25f;
        /// <summary>The front shot's distance from what it frames is kept within these (m, by rig scale).</summary>
        public const float MinDistance=1.2f,MaxDistance=3.6f;
        /// <summary>Eases (1/s): the shot following the pilot's facing, moving between fallback angles, and its framing following the pose.</summary>
        public const float FacingRate=9,AngleRate=5,FrameRate=4;
        /// <summary>A working fallback angle is kept this long (s) before a preferred one that cleared is taken back; and kept while at
        /// least Keep of its distance is clear.</summary>
        public const float Dwell=.4f,Keep=.85f;
        /// <summary>The world cast's sphere and padding (the shoulder rigs'), and the eased push-out after a pull-in (1/s): twice the
        /// boom's, as the swing sweeps past an obstruction far faster than a gameplay camera moves.</summary>
        public const float Radius=NativeCameraRig.Radius,Padding=NativeCameraRig.BoomPadding,BoomRelease=12;
        /// <summary>First person: my body is drawn once the camera is this far from the eye (m, by rig scale).</summary>
        public const float HeadClear=.45f;
        /// <summary>The swing goes the other way round when the middle of its way is less than this clear (and the other clearly clearer).</summary>
        public const float WayClear=.6f;
        /// <summary>A gap between frames that drops the swing (another screen owned the camera), as NativeCameraRig.Stale.</summary>
        public const float Stale=NativeCameraRig.Stale;
        /// <summary>The weight from which the HUD treats the view as the chooser shot (no crosshair, nothing over my own pilot).</summary>
        public const float HudFrom=.35f;
        /// <summary>The front shot's angles off the facing (+ toward the camera's side, away from the screen), preferred first: the
        /// three-quarter view, wider, the side view, the same on the screen's side, straight on, then behind either shoulder.</summary>
        public static readonly float[] Angles={24,48,80,-24,-48,-80,0,125,-125};

        private readonly float[] clear=new float[9];private readonly Vector3[] points=new Vector3[7];
        private float u,placedAt=-100,facing,angle,crown,allowed=float.MaxValue,choiceAt,orbitD,lookD;private Vector3 wrist;private int pilot=int.MinValue,choice=-1;private bool has,ends=true;
        /// <summary>The swing's progress (0-1) and its eased weight.</summary>
        public float Progress=>u;
        public float Weight=>NativeCameraRig.Ease(u);
        /// <summary>The fallback candidate in use (an index of Angles), -1 when not swung.</summary>
        public int Choice=>choice;
        /// <summary>The camera's side: the other front quarter from the screen (+1 the pilot's right).</summary>
        public static float SideOf(bool screenLeft)=>screenLeft?1:-1;
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
            float e=NativeCameraRig.Ease(u),s=i.Scale>0?i.Scale:1,side=SideOf(i.ScreenLeft);bool fresh=!has;has=true;
            facing=fresh?i.Facing:facing+NativeAimConvergence.DeltaAngle(facing,i.Facing)*Ease(dt,FacingRate);
            var eye=i.Body+Vector3.UnitY*(NativeViewRules.EyeHeight*s);

            // What the shot frames: my crown and wrist (eased as the pose moves), my far shoulder and the open chip screen.
            float crownW=(i.HasHead?i.Head.Y:i.Body.Y+HeadUp*s)+Crown*s;var wristW=i.HasWrist?i.Wrist:i.Body+Vector3.UnitY*(WristUp*s)+Flat(facing)*(.3f*s);
            if(fresh){crown=crownW;wrist=wristW;}else{float k=Ease(dt,FrameRate);crown+=(crownW-crown)*k;wrist=Vector3.Lerp(wrist,wristW,k);}
            var screen=NativeChipScreen.Place(i.Body,facing,i.ScreenLeft,s);var ahead=Flat(facing);var right=new Vector3(ahead.Z,0,-ahead.X);
            float hw=screen.Width*.5f,hh=screen.Height*.5f;
            points[0]=new Vector3(i.Body.X,crown,i.Body.Z);points[1]=wrist;points[2]=screen.At(-hw,hh);points[3]=screen.At(hw,hh);points[4]=screen.At(-hw,-hh);points[5]=screen.At(hw,-hh);
            points[6]=i.Body+Vector3.UnitY*(.3f*s)+right*(side*.32f*s);
            float tanV=(float)Math.Tan(Rad(Fov*.5f)),tanH=tanV*Math.Max(.2f,i.Aspect);

            float score=Choose(eye,side,s,tanV,tanH,i.Now,fresh,cast),want=side*Angles[choice];
            angle=fresh?want:angle+NativeAimConvergence.DeltaAngle(angle,want)*Ease(dt,AngleRate);
            var front=Shot(facing+angle,s,tanV,tanH,out float reach,out float yawF,out float pitchF);

            Vector3 position,pivot;float yaw,pitch,fov;
            if(e>=1-1e-6f){position=front;pivot=eye;yaw=yawF;pitch=pitchF;fov=Fov;ends=true;}
            else
            {
                // The orbit about the pilot's axis from the gameplay camera (first person: from in front of the face) to the front shot.
                var g=i.Camera-i.Body;var f=front-i.Body;float rG=(float)Math.Sqrt(g.X*g.X+g.Z*g.Z),rF=(float)Math.Sqrt(f.X*f.X+f.Z*f.Z);
                float aG=rG<.05f*s?facing:Deg(Math.Atan2(g.X,g.Z)),aF=Deg(Math.Atan2(f.X,f.Z));
                float rawOrbit=NativeAimConvergence.DeltaAngle(aG,aF),rawLook=NativeAimConvergence.DeltaAngle(i.Yaw,yawF);
                if(ends)
                {
                    // Each way round is decided at an end of the swing and then followed continuously: the orbit the short way, or
                    // from behind round the side the shot ends on; the look the short way, or when nearly opposite as the orbit turns.
                    float endSide=angle>1e-3f?1:angle<-1e-3f?-1:side;
                    orbitD=Math.Abs(rawOrbit)>170?Toward(rawOrbit,-endSide):rawOrbit;
                    // A wall on that way round (its middle mostly blocked): the other way when that is clearly clearer, so the swing does
                    // not squeeze past the pilot's head. Decided once per end; two casts.
                    if(Math.Abs(orbitD)>60)
                    {
                        float alt=orbitD>0?orbitD-360:orbitD+360,rM=(rG+rF)*.5f,yM=(i.Camera.Y+front.Y)*.5f;
                        float way=Clearance(i.Body,eye,aG+orbitD*.5f,rM,yM,cast),other=Clearance(i.Body,eye,aG+alt*.5f,rM,yM,cast);
                        if(way<WayClear&&other>way+.25f)orbitD=alt;
                    }
                    float orbitDir=orbitD>1e-3f?1:orbitD<-1e-3f?-1:-endSide;
                    lookD=Math.Abs(rawLook)>150?Toward(rawLook,orbitDir):rawLook;
                }
                else{orbitD+=NativeAimConvergence.DeltaAngle(orbitD,rawOrbit);lookD+=NativeAimConvergence.DeltaAngle(lookD,rawLook);}
                ends=false;
                float a=Rad(aG+orbitD*e),r=rG+(rF-rG)*e;
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

        // The clear fraction of a sphere cast from the eye to the orbit point at azimuth `az`, radius `r`, height `y`.
        private static float Clearance(Vector3 body,Vector3 eye,float az,float r,float y,NativeCameraRig.Cast cast)
        {
            var o=new Vector3(body.X+(float)Math.Sin(Rad(az))*r,y,body.Z+(float)Math.Cos(Rad(az))*r)-eye;float len=o.Length();if(len<=1e-4f)return 1;
            float hit=cast(eye,o/len,len,Radius);return hit>=len-1e-4f?1:Math.Max(0,hit-Padding)/len;
        }

        // The shot from `az` (absolute yaw of the camera seen from the framed points' centre): looking Down across that centre, backed off
        // until every point fits FitX across and FitY up the frame.
        private Vector3 Shot(float az,float s,float tanV,float tanH,out float distance,out float yaw,out float pitch)
        {
            Vector3 lo=points[0],hi=points[0];for(int k=1;k<points.Length;k++){lo=Vector3.Min(lo,points[k]);hi=Vector3.Max(hi,points[k]);}var centre=(lo+hi)*.5f;
            yaw=az+180;pitch=Down;NativeCameraRig.Basis(yaw,pitch,out var d,out var r);var up=Vector3.Cross(d,r);float need=MinDistance*s;
            for(int k=0;k<points.Length;k++)
            {
                var rel=points[k]-centre;float x=Math.Abs(Vector3.Dot(rel,r)),y=Math.Abs(Vector3.Dot(rel,up)),z=Vector3.Dot(rel,d);
                need=Math.Max(need,Math.Max(x/(tanH*FitX)-z,y/(tanV*FitY)-z));
            }
            distance=Math.Min(need,MaxDistance*s);return centre-d*distance;
        }

        // The fallback search: each candidate's clear fraction (a sphere cast from the eye to its camera point), the first fully clear
        // in order, else the clearest; a working choice is kept for Dwell and while Keep of it stays clear. Returns its fraction.
        private float Choose(Vector3 eye,float side,float s,float tanV,float tanH,float now,bool fresh,NativeCameraRig.Cast cast)
        {
            int best=-1,clearest=0;
            for(int k=0;k<Angles.Length;k++)
            {
                var o=Shot(facing+side*Angles[k],s,tanV,tanH,out _,out _,out _)-eye;float len=o.Length();float hit=len>1e-4f?cast(eye,o/len,len,Radius):len;
                clear[k]=hit>=len-1e-4f?1:Math.Max(0,hit-Padding)/Math.Max(1e-4f,len);
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
