using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The selectable match cameras (owner 2026-10-12: "Lets give players a new camera angle, our current one makes it a
    /// little hard to aim. We can try angles from a different perspective"). Settings · Camera picks one of five; V still
    /// switches first person on and off (prefs.firstPerson), X swaps the shoulder of the two shoulder rigs.
    ///   1 Shoulder (right): over the right shoulder, the pilot left of centre, the new default;
    ///   2 Shoulder (left): its mirror;
    ///   3 Close action: tighter and lower behind the shoulder, for melee;
    ///   4 Classic: the camera before this change, unchanged (centred 4.4 m behind and .55 m above the eye);
    ///   5 First person, as before.
    /// Presentation and client input only: the third-person mode is saved as prefs.cameraMode and prefs.shoulderLeft.
    /// </summary>
    public static class NativeCameraModes
    {
        public const string Shoulder="shoulder",Close="close",Classic="classic",Default=Shoulder;
        // Settings choices (the first-person choice is prefs.firstPerson, which V toggles).
        public const string ShoulderRight="shoulder-right",ShoulderLeft="shoulder-left",FirstPerson="first";
        public static readonly string[] Choices={ShoulderRight,ShoulderLeft,Close,Classic,FirstPerson};
        public static readonly string[] Labels={"Shoulder (right)","Shoulder (left)","Close action","Classic","First person"};
        /// <summary>The shoulder swap key (unbound before; V is the first-person toggle and a Siren mode key).</summary>
        public const string SwapKey="X";
        /// <summary>A saved mode, or the default for a missing (older save) or unknown value.</summary>
        public static string Sanitize(string mode)=>mode==Shoulder||mode==Close||mode==Classic?mode:Default;
        public static string Choice(string mode,bool left,bool first)
        {
            if(first)return FirstPerson;mode=Sanitize(mode);return mode==Classic?Classic:mode==Close?Close:left?ShoulderLeft:ShoulderRight;
        }
        /// <summary>Applies a Settings choice; Close action keeps the shoulder side, First person keeps the third-person mode for V.</summary>
        public static void Choose(string choice,ref string mode,ref bool left,ref bool first)
        {
            switch(choice)
            {
                case ShoulderRight:mode=Shoulder;left=false;first=false;break;
                case ShoulderLeft:mode=Shoulder;left=true;first=false;break;
                case Close:mode=Close;first=false;break;
                case Classic:mode=Classic;first=false;break;
                case FirstPerson:first=true;break;
            }
        }
        /// <summary>Whether the swap key applies: a shoulder rig in third person.</summary>
        public static bool Swaps(string mode,bool first)=>!first&&Sanitize(mode)!=Classic;
        public static string Name(string mode,bool left)
        {
            mode=Sanitize(mode);return mode==Classic?"Classic camera":mode==Close?(left?"Close action, left":"Close action, right"):left?"Left shoulder":"Right shoulder";
        }
    }

    /// <summary>A third-person rig shape: the shoulder offset from the eye focus (Side right, signed; Lift up, world), the boom
    /// behind that point along the look (Back) and the field of view. Camera = focus + right*Side + up*Lift - forward*Back, so
    /// the crosshair ray always runs through the shoulder point (classic: Side 0, Lift .55, Back 4.4, the old camera).</summary>
    public struct NativeCameraShape
    {
        public float Side,Lift,Back,Fov;
        public NativeCameraShape(float side,float lift,float back,float fov){Side=side;Lift=lift;Back=back;Fov=fov;}
        public static readonly NativeCameraShape ShoulderRight=new NativeCameraShape(.62f,.25f,2.9f,55),
            CloseRight=new NativeCameraShape(.45f,.05f,2f,62),
            ClassicShape=new NativeCameraShape(0,NativeViewRules.ThirdLift,NativeViewRules.ThirdDistance,NativeViewRules.ThirdFov);
        public static NativeCameraShape Of(string mode,bool left)
        {
            mode=NativeCameraModes.Sanitize(mode);var s=mode==NativeCameraModes.Classic?ClassicShape:mode==NativeCameraModes.Close?CloseRight:ShoulderRight;
            if(left)s.Side=-s.Side;return s;
        }
        public static NativeCameraShape Lerp(NativeCameraShape a,NativeCameraShape b,float t)
            =>new NativeCameraShape(a.Side+(b.Side-a.Side)*t,a.Lift+(b.Lift-a.Lift)*t,a.Back+(b.Back-a.Back)*t,a.Fov+(b.Fov-a.Fov)*t);
    }

    /// <summary>
    /// The shoulder rigs' placement over time: the .25 s eased blend between modes and shoulders, the collision fit (the
    /// shoulder offset cast from the eye toward the shoulder point, then the boom cast back from it; pulled in at once,
    /// eased out), the temporary swap to the open shoulder in a tight spot, and the eye focus's vertical smoothing (stairs,
    /// landings). The Classic mode, once settled, is placed by ConversionGame's original code (Classic is true), so it stays
    /// exactly the old camera. Unity axes (left-handed, yaw clockwise from +Z, pitch positive down), unscaled seconds.
    /// </summary>
    public sealed class NativeCameraRig
    {
        /// <summary>A sphere cast: the distance the sphere travels before it touches the world, or `distance` when clear.</summary>
        public delegate float Cast(Vector3 from,Vector3 direction,float distance,float radius);
        public const float Blend=.25f,Radius=.12f,SoftRadius=.45f,SidePadding=.08f,BoomPadding=NativeViewRules.CollisionPadding,MinBoom=0;
        /// <summary>Eased push-out after a pull-in (1/s), and the side's ease toward the soft (look-ahead) fit.</summary>
        public const float BoomRelease=6,SideEase=14;
        /// <summary>The temporary swap: the fitted shoulder offset under SwapBelow (m; the pilot then nearly covers the crosshair)
        /// with the other shoulder open beyond SwapOpen of its offset; back once its own side fits SwapBack, after SwapHold s.</summary>
        public const float SwapBelow=.35f,SwapOpen=.95f,SwapBack=.85f,SwapHold=.6f;
        /// <summary>Vertical focus smoothing: approach rate (1/s) and the most the camera's focus trails the eye (m).</summary>
        public const float FocusRate=16,FocusLag=.22f,FocusReset=2;
        /// <summary>A gap between placements that restarts the collision state instead of easing out of a stale one (s).</summary>
        public const float Stale=.2f;

        private string mode=NativeCameraModes.Default;private bool left,has,swapped;
        private NativeCameraShape from,to;private float blendAt=-100,swapAt=-100,placedAt=-100;
        private float sideFit=1,boomAllowed=float.MaxValue;
        private bool focusHas;private int focusId;private float focusY;

        public string Mode=>mode;public bool Left=>left;public bool Swapped=>swapped;
        /// <summary>The shoulder offset used by the last placement (signed side and lift after the fit, from the smoothed focus),
        /// which the lock-on framing solves against (ConversionGame adds the focus's lag behind the eye).</summary>
        public float PivotSide {get;private set;}
        public float PivotUp {get;private set;}
        public float Boom {get;private set;}
        public Vector3 Position {get;private set;}

        /// <summary>The selected mode and shoulder. The first call takes them at once; a change later blends over .25 s.</summary>
        public void Select(string mode,bool left,float now)
        {
            mode=NativeCameraModes.Sanitize(mode);
            if(!has){has=true;this.mode=mode;this.left=left;swapped=false;from=to=Target();blendAt=-100;return;}
            if(mode==this.mode&&left==this.left)return;
            // A chosen shoulder (or Classic) ends a temporary swap.
            if(mode==NativeCameraModes.Classic||left!=this.left)swapped=false;
            var current=Current(now);this.mode=mode;this.left=left;Retarget(current,now);
        }
        private NativeCameraShape Target()=>NativeCameraShape.Of(mode,left^swapped);
        private void Retarget(NativeCameraShape current,float now){from=current;to=Target();blendAt=now;}
        public static float Ease(float u){u=u<0?0:u>1?1:u;return u*u*(3-2*u);}
        public NativeCameraShape Current(float now)
        {
            float u=(now-blendAt)/Blend;if(u>=1)return to;if(u<=0)return from;return NativeCameraShape.Lerp(from,to,Ease(u));
        }
        public bool Settled(float now)=>now-blendAt>=Blend;
        /// <summary>Settled in Classic: ConversionGame then runs the original camera code and sends the look unchanged.</summary>
        public bool Classic(float now)=>has&&mode==NativeCameraModes.Classic&&!swapped&&Settled(now);
        public float Fov(float now)=>Current(now).Fov;

        /// <summary>The eye focus height the rig places from: follows the eye, eased (rate 16/s) and never more than .22 m
        /// behind it, so stair steps and landings do not jolt the view; a new pilot or a jump over 2 m snaps.</summary>
        public float Focus(float eyeY,int pilot,float dt)
        {
            if(!focusHas||pilot!=focusId||Math.Abs(eyeY-focusY)>FocusReset){focusHas=true;focusId=pilot;focusY=eyeY;return eyeY;}
            focusY+=(eyeY-focusY)*(1-(float)Math.Exp(-FocusRate*Math.Max(0,dt)));
            focusY=Math.Max(eyeY-FocusLag,Math.Min(eyeY+FocusLag,focusY));return focusY;
        }

        /// <summary>Unity's Quaternion.Euler(pitch, yaw, 0) forward and right.</summary>
        public static void Basis(float yaw,float pitch,out Vector3 forward,out Vector3 right)
        {
            double y=yaw*Math.PI/180,p=pitch*Math.PI/180;
            forward=new Vector3((float)(Math.Sin(y)*Math.Cos(p)),(float)-Math.Sin(p),(float)(Math.Cos(y)*Math.Cos(p)));
            right=new Vector3((float)Math.Cos(y),0,(float)-Math.Sin(y));
        }
        private static float Fit(float hit,float length,float padding)=>hit>=length-1e-5f?1:Math.Max(0,Math.Min(1,(hit-padding)/length));

        /// <summary>
        /// The camera position for a look (yaw, pitch) around the focus (the eye, its height from Focus). The shoulder offset is
        /// cast from the focus (radius .12, padding .08, plus a .45 sphere looking ahead for edges; the side eases toward the
        /// look-ahead fit, so a pillar or wall coming up on the shoulder side pulls the camera in smoothly, and never past the
        /// narrow cast). Pressed under .35 m with the other shoulder open, the rig swaps there for the moment (blended) and
        /// swaps back once its own side clears. The boom is cast back from
        /// the fitted shoulder point (padding .2; right up to the shoulder point, which the first cast cleared, when the wall is
        /// nearer) and pulled in at once, then eased out at 6/s.
        /// </summary>
        public Vector3 Place(Vector3 focus,float yaw,float pitch,float now,float dt,Cast cast)
        {
            if(!has)Select(mode,left,now);
            bool stale=now-placedAt>Stale||now<placedAt;placedAt=now;dt=Math.Max(0,Math.Min(dt,.1f));
            Basis(yaw,pitch,out var forward,out var right);
            var s=Current(now);
            float side=OffsetFit(focus,right,s.Side,s.Lift,cast,out float soft);
            // A tight spot: this shoulder mostly blocked while the other is open swaps for the moment (never in Classic).
            if(mode!=NativeCameraModes.Classic&&Settled(now)&&now-swapAt>=SwapHold)
            {
                if(!swapped&&Math.Abs(s.Side)>.01f&&Math.Abs(s.Side)*side<SwapBelow&&OffsetFit(focus,right,-s.Side,s.Lift,cast,out _)>=SwapOpen){swapped=true;swapAt=now;Retarget(s,now);}
                else if(swapped&&OffsetFit(focus,right,-s.Side,s.Lift,cast,out _)>=SwapBack){swapped=false;swapAt=now;Retarget(s,now);}
            }
            if(stale)sideFit=soft;else sideFit+=(soft-sideFit)*(1-(float)Math.Exp(-SideEase*dt));
            sideFit=Math.Min(sideFit,side);
            Vector3 pivot=focus+(right*s.Side+Vector3.UnitY*s.Lift)*sideFit;
            float boomHit=s.Back>1e-4f?cast(pivot,-forward,s.Back,Radius):s.Back;
            float allowed=boomHit>=s.Back-1e-5f?float.MaxValue:Math.Max(MinBoom,boomHit-BoomPadding);
            if(stale||allowed<=boomAllowed)boomAllowed=allowed;
            else if(allowed==float.MaxValue&&boomAllowed>=s.Back-.005f)boomAllowed=float.MaxValue;
            else boomAllowed+=(Math.Min(allowed,s.Back+.01f)-boomAllowed)*(1-(float)Math.Exp(-BoomRelease*dt));
            Boom=Math.Min(s.Back,boomAllowed);
            PivotSide=s.Side*sideFit;PivotUp=s.Lift*sideFit;Position=pivot-forward*Boom;return Position;
        }
        // Fit (0..1) of the shoulder offset (side, lift) from the focus; `soft` the look-ahead fit (at most the hard one).
        private static float OffsetFit(Vector3 focus,Vector3 right,float side,float lift,Cast cast,out float soft)
        {
            var offset=right*side+Vector3.UnitY*lift;float length=offset.Length();soft=1;if(length<1e-4f)return 1;
            var direction=offset/length;float hard=Fit(cast(focus,direction,length,Radius),length,SidePadding);
            // The wider sphere meets an edge coming into the path earlier; its hit is read as the narrow sphere's would be
            // (+ the radius difference), so against a flat wall both agree and only an approaching edge pulls in ahead.
            float wide=cast(focus,direction,length,SoftRadius);
            soft=Math.Min(hard,wide>=length-1e-5f?1:Math.Max(0,Math.Min(1,(wide+SoftRadius-Radius-SidePadding)/length)));return hard;
        }

        /// <summary>
        /// The look (Unity yaw, pitch down positive) whose crosshair ray, from a rig whose shoulder point sits `side` right and
        /// `up` above the focus, runs through `target` (the lock-on framing). The ray always passes the shoulder point, so the
        /// yaw is the bearing to the target less asin(side / horizontal distance) and the pitch is taken from the shoulder point.
        /// Within the side offset (or closer than .3 m) it falls back to the bearing from the focus.
        /// </summary>
        public static void LookAt(Vector3 focus,Vector3 target,float side,float up,out float yaw,out float pitch)
        {
            float dx=target.X-focus.X,dz=target.Z-focus.Z,r=(float)Math.Sqrt(dx*dx+dz*dz);double bearing=Math.Atan2(dx,dz);
            if(r<Math.Abs(side)+.3f){yaw=(float)(bearing*180/Math.PI);pitch=(float)(-Math.Atan2(target.Y-focus.Y,Math.Max(.01f,r))*180/Math.PI);return;}
            yaw=(float)((bearing-Math.Asin(side/r))*180/Math.PI);float d=(float)Math.Sqrt(r*r-side*side);
            pitch=(float)(-Math.Atan2(target.Y-(focus.Y+up),d)*180/Math.PI);
        }
    }

    /// <summary>
    /// Aim convergence for the shoulder cameras. With the camera beside the pilot, the crosshair ray (from the camera centre)
    /// and the pilot's own aim no longer coincide. The client finds the crosshair point P (the camera ray against the world and
    /// the pilots' and minions' hit volumes, the view pilot excluded, hits nearer than the pilot's own depth along the ray
    /// skipped because they are behind its shot origin), keeps AimTarget/AbilityTarget as that camera-ray point (the authority
    /// already fires primaries from the real muzzle at AimTarget), and sends Yaw/Pitch pointing the pilot's eye origin
    /// (body + EyeHeight, the authority's AuthorityAim origin for gun and magic abilities) at P, within 25 degrees of the
    /// camera's look. Melee only turns toward a body under the crosshair; otherwise it keeps the far point (the camera's look).
    /// The authority is unchanged; guests predict with the same frame.
    /// </summary>
    public static class NativeAimConvergence
    {
        public const float MaxYaw=25,MaxPitch=25,FarRange=60;
        /// <summary>Hits nearer than this along the camera ray lie behind the pilot's shot origin (its depth on the ray).</summary>
        public static float Skip(Vector3 camera,Vector3 forward,Vector3 origin)=>Math.Max(0,Vector3.Dot(origin-camera,forward));
        public static float DeltaAngle(float from,float to){float d=(to-from)%360;if(d>180)d-=360;else if(d<-180)d+=360;return d;}
        /// <summary>Yaw (Unity degrees, continuous with the camera yaw) and input pitch (degrees, up positive, as InputFrame.Pitch)
        /// pointing `origin` at `point`, clamped to MaxYaw/MaxPitch around the camera's look (cameraPitch: Unity, down positive).</summary>
        public static void Angles(Vector3 origin,Vector3 point,float cameraYaw,float cameraPitch,out float yaw,out float inputPitch)
        {
            yaw=cameraYaw;inputPitch=-cameraPitch;var d=point-origin;float flat=(float)Math.Sqrt(d.X*d.X+d.Z*d.Z);
            if(flat<.05f&&Math.Abs(d.Y)<.05f)return;
            float want=(float)(Math.Atan2(d.X,d.Z)*180/Math.PI),up=(float)(Math.Atan2(d.Y,flat)*180/Math.PI);
            yaw=cameraYaw+Math.Max(-MaxYaw,Math.Min(MaxYaw,DeltaAngle(cameraYaw,want)));
            inputPitch=Math.Max(-89,Math.Min(89,Math.Max(-cameraPitch-MaxPitch,Math.Min(-cameraPitch+MaxPitch,up))));
        }
        /// <summary>GunRules.Direction as a vector: the authority's aim direction for an input yaw and pitch.</summary>
        public static Vector3 Direction(float yaw,float inputPitch){var v=GunRules.Direction(yaw,inputPitch);return new Vector3(v.x,v.y,v.z);}
        /// <summary>Distance from `point` to the ray from `origin` along `direction` (for the convergence checks).</summary>
        public static float Miss(Vector3 origin,Vector3 direction,Vector3 point)
        {
            direction=Vector3.Normalize(direction);var d=point-origin;float along=Math.Max(0,Vector3.Dot(d,direction));return (d-direction*along).Length();
        }
    }

    /// <summary>The third-person sprint weight and stride for the shoulder rigs' FOV kick and bob (NativeSprintRules.Fov and
    /// CameraSway, the first-person values): the replicated sprint flag through NativeSprintRules.Shows, the .15 s ramp, and a
    /// stride advancing half a turn per footfall (StepLength of travel).</summary>
    public sealed class NativeCameraSprint
    {
        private float ramp;private int pilot=-1;
        public float Stride {get;private set;}
        public float Weight=>NativeSprintRules.Shape(ramp);
        public void Step(Actor a,float time,float dt)
        {
            if(a==null){ramp=0;return;}if(a.Id!=pilot){pilot=a.Id;ramp=0;Stride=0;}
            dt=Math.Max(0,Math.Min(dt,.1f));ramp=NativeSprintRules.Step(ramp,NativeSprintRules.Shows(a,time),dt);
            float speed=(float)Math.Sqrt(a.Velocity.x*a.Velocity.x+a.Velocity.z*a.Velocity.z);Stride=(Stride+(float)Math.PI*speed*dt/NativeSprintRules.StepLength)%(float)(Math.PI*2);
        }
    }
}
