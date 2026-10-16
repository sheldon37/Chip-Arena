using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;

/// <summary>
/// Settings · Camera (owner 2026-10-12: "Lets give players a new camera angle, our current one makes it a little hard to aim.
/// We can try angles from a different perspective"; Core/NativeCameraRig, Runtime/NativeShoulderCamera): the five modes and
/// their saved preference; aim convergence (the crosshair point P, the sent look from the pilot's eye, the authority's gun
/// shot from its real muzzle at AimTarget and its ability aim along the look) within 2 cm for targets 2 to 60 m in every
/// mode; the lock-on framing; the collision fit (shoulder, boom, the temporary swap, no camera inside real Brickfall and
/// Foundry blocks); blend continuity between modes and shoulders; the focus smoothing on stairs and landings; the shoulder
/// sprint kick; no allocation; Classic left exactly the old camera (its code, FOV, lock look and unconverged input).
/// Managed geometry stands in for PhysX here (boxes, a box-swept sphere); no Unity Editor or Windows play is run.
/// </summary>
internal static class CameraRigChecks
{
    private const float Dt=1/60f,Eye=NativeViewRules.EyeHeight,Centre=.865f,Capsule=NativeLocomotionRules.Radius;
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Camera rig: "+what);}
    private static string Cm(float m)=>(m*100).ToString("0.00")+" cm";
    public static void Run(string root)
    {
        var log=new List<string>();Modes(log);Convergence(log);AuthorityRule(log);LockOn(log);Collision(log);Maps(root,log);Blends(log);Focus(log);Sprint(log);Allocation(log);Classic(root,log);
        foreach(var line in log)Console.WriteLine("PASS camera rig: "+line);
    }

    // ---- managed stand-ins for the world ----
    private sealed class Box
    {
        public Vector3 C,H;public Quaternion Q=Quaternion.Identity,Inv=Quaternion.Identity;
        public Box(Vector3 c,Vector3 h){C=c;H=h;}
        public Box(Vector3 c,Vector3 h,Quaternion q){C=c;H=h;Q=Quaternion.Normalize(q);Inv=Quaternion.Inverse(Q);}
        public Vector3 Local(Vector3 p)=>Vector3.Transform(p-C,Inv);
        public float Distance(Vector3 p){var l=Local(p);var d=Vector3.Max(Vector3.Abs(l)-H,Vector3.Zero);return d.Length();}
        // Entry distance of a ray against the box grown by r (a swept sphere, square at the edges: conservative), or -1.
        public float Enter(Vector3 o,Vector3 d,float r,float max)
        {
            var lo=Local(o);var ld=Vector3.Transform(d,Inv);var h=H+new Vector3(r);float t0=0,t1=max;
            for(int k=0;k<3;k++)
            {
                float oo=k==0?lo.X:k==1?lo.Y:lo.Z,dd=k==0?ld.X:k==1?ld.Y:ld.Z,hh=k==0?h.X:k==1?h.Y:h.Z;
                if(Math.Abs(dd)<1e-8f){if(oo<-hh||oo>hh)return -1;continue;}
                float a=(-hh-oo)/dd,b=(hh-oo)/dd;if(a>b)(a,b)=(b,a);t0=Math.Max(t0,a);t1=Math.Min(t1,b);if(t0>t1)return -1;
            }
            return t0;
        }
        public bool Inside(Vector3 o,float r){var l=Local(o);return Math.Abs(l.X)<H.X+r&&Math.Abs(l.Y)<H.Y+r&&Math.Abs(l.Z)<H.Z+r;}
    }
    // PhysX's SphereCast ignores colliders the sphere already overlaps at the start; this cast does the same.
    private static NativeCameraRig.Cast CastOf(IReadOnlyList<Box> boxes)=>(o,d,dist,r)=>
    {
        float best=dist;for(int i=0;i<boxes.Count;i++){var b=boxes[i];if(b.Inside(o,r))continue;float t=b.Enter(o,d,r,dist);if(t>=0&&t<best)best=t;}return best;
    };
    private static readonly NativeCameraRig.Cast Open=(o,d,dist,r)=>dist;
    private static float Ray(IReadOnlyList<Box> boxes,Vector3 o,Vector3 d,float max){float best=max;foreach(var b in boxes){if(b.Inside(o,0))continue;float t=b.Enter(o,d,0,max);if(t>=0&&t<best)best=t;}return best;}
    private static float RaySphere(Vector3 o,Vector3 d,Vector3 c,float r)
    {
        var m=o-c;float b=Vector3.Dot(m,d),q=Vector3.Dot(m,m)-r*r,disc=b*b-q;if(disc<0)return -1;float t=-b-(float)Math.Sqrt(disc);return t>=0?t:-1;
    }
    // Unity Quaternion.Euler(0, yaw, 0) applied to a vector.
    private static Vector3 Yawed(float yaw,Vector3 v){double y=yaw*Math.PI/180;float c=(float)Math.Cos(y),s=(float)Math.Sin(y);return new Vector3(v.X*c+v.Z*s,v.Y,-v.X*s+v.Z*c);}

    private sealed class Mode{public string Name,Id;public bool Left,First;}
    private static readonly Mode[] All={new Mode{Name="1 shoulder right",Id=NativeCameraModes.Shoulder},new Mode{Name="2 shoulder left",Id=NativeCameraModes.Shoulder,Left=true},
        new Mode{Name="3 close action",Id=NativeCameraModes.Close},new Mode{Name="4 classic",Id=NativeCameraModes.Classic},new Mode{Name="5 first person",Id=NativeCameraModes.Shoulder,First=true}};
    private static NativeCameraRig Rig(Mode m){var r=new NativeCameraRig();r.Select(m.Id,m.Left,0);return r;}
    // The camera as ConversionGame places it: first person at the eye; Classic by the original formula; a rig otherwise.
    private static Vector3 Camera(Mode m,NativeCameraRig rig,Vector3 focus,float yaw,float pitch,float now,NativeCameraRig.Cast cast)
    {
        if(m.First)return focus;
        if(rig.Classic(now)){NativeCameraRig.Basis(yaw,pitch,out var f,out _);var offset=-f*NativeViewRules.ThirdDistance+Vector3.UnitY*NativeViewRules.ThirdLift;float hit=cast(focus,Vector3.Normalize(offset),offset.Length(),.12f);
            return hit<offset.Length()?focus+Vector3.Normalize(offset)*Math.Max(.15f,hit-NativeViewRules.CollisionPadding):focus+offset;}
        return rig.Place(focus,yaw,pitch,now,Dt,cast);
    }

    private static void Modes(List<string> log)
    {
        Check(NativeCameraModes.Sanitize(null)==NativeCameraModes.Shoulder&&NativeCameraModes.Sanitize("")==NativeCameraModes.Shoulder&&NativeCameraModes.Sanitize("orbit")==NativeCameraModes.Shoulder
            &&NativeCameraModes.Sanitize("classic")=="classic"&&NativeCameraModes.Sanitize("close")=="close","a missing (older save) or unknown mode is the new default, the right shoulder");
        Check(NativeCameraModes.Choices.Length==5&&NativeCameraModes.Labels.Length==5&&NativeCameraModes.Choices.Distinct().Count()==5,"five Settings choices");
        foreach(var c in NativeCameraModes.Choices)
        {
            string mode=NativeCameraModes.Classic;bool left=c==NativeCameraModes.Close,first=false;NativeCameraModes.Choose(c,ref mode,ref left,ref first);
            Check(NativeCameraModes.Choice(mode,left,first)==c,"Settings choice "+c+" round trips");
        }
        {string mode=NativeCameraModes.Close;bool left=true,first=false;NativeCameraModes.Choose(NativeCameraModes.FirstPerson,ref mode,ref left,ref first);Check(first&&mode==NativeCameraModes.Close&&left,"first person keeps the third-person mode for V");
         first=!first;Check(NativeCameraModes.Choice(mode,left,first)==NativeCameraModes.Close,"V returns to the chosen third-person mode");}
        // An older save: no cameraMode (JsonUtility keeps the field's initializer) and firstPerson as it was.
        {string mode=NativeCameraModes.Sanitize(NativeCameraModes.Default);bool left=false;Check(NativeCameraModes.Choice(mode,left,false)==NativeCameraModes.ShoulderRight&&NativeCameraModes.Choice(mode,left,true)==NativeCameraModes.FirstPerson,"an older third-person save opens on the right shoulder; first person stays first person");}
        Check(NativeCameraModes.Swaps(NativeCameraModes.Shoulder,false)&&NativeCameraModes.Swaps(NativeCameraModes.Close,false)&&!NativeCameraModes.Swaps(NativeCameraModes.Classic,false)&&!NativeCameraModes.Swaps(NativeCameraModes.Shoulder,true),"X swaps the shoulder rigs only, in third person");
        var r=NativeCameraShape.Of("shoulder",false);var l=NativeCameraShape.Of("shoulder",true);var c2=NativeCameraShape.Of("close",false);var k=NativeCameraShape.Of("classic",false);
        Check(r.Side>=.55f&&r.Side<=.7f&&Math.Abs(r.Lift-.25f)<.001f&&r.Back>=2.6f&&r.Back<=3.2f&&r.Fov<NativeViewRules.ThirdFov&&l.Side==-r.Side,"shoulder: .55-.7 m right (left mirrored), .25 m up, 2.6-3.2 m back, a lower FOV");
        Check(c2.Back<r.Back&&c2.Lift<r.Lift&&c2.Side>0&&c2.Side<r.Side,"close action: tighter and lower behind the shoulder");
        Check(k.Side==0&&k.Lift==NativeViewRules.ThirdLift&&k.Back==NativeViewRules.ThirdDistance&&k.Fov==NativeViewRules.ThirdFov,"classic: the old camera's constants");
        log.Add("five modes (1 shoulder right, default; 2 shoulder left; 3 close action; 4 classic; 5 first person), Settings round trips, older saves open on the right shoulder, first person kept for V, X only on the shoulder rigs");
    }

    // ---- aim convergence ----
    private static void Convergence(List<string> log)
    {
        var body=new Vector3(0,Centre,0);var eye=body+Vector3.UnitY*Eye;
        var distances=new[]{2f,3,5,8,10,15,20,30,45,60};
        foreach(var m in All)
        {
            float worstEye=0,worstGun=0,worstAbility=0,worstBodyAbility=0,worstOld=0,worstClassicAbility=0,worstRaw=0,worstYaw=0;int cases=0,stopped=0;
            foreach(float d in distances)foreach(float side in new[]{-3f,0,2.5f})foreach(float height in new[]{-.5f,0,1.6f})foreach(bool wall in new[]{false,true})
            {
                // The target: a pilot hit volume (a .45 m sphere at its chest) or a point on a wall facing the pilot.
                var target=new Vector3(side,eye.Y+height,d);if((target-eye).Length()<2)continue;
                var rig=Rig(m);float now=0,yaw=0,pitch=0;
                // Aim the crosshair at the target: the lock-on solve for the rig's shoulder point (first person and classic
                // from their own camera; iterate for the classic boom's lift).
                for(int i=0;i<3;i++)
                {
                    var cam=Camera(m,rig,eye,yaw,pitch,now,Open);
                    if(m.First){NativeCameraRig.LookAt(eye,target,0,0,out yaw,out pitch);}
                    else if(rig.Classic(now)){NativeCameraRig.LookAt(eye,target,0,NativeViewRules.ThirdLift,out yaw,out pitch);}
                    else NativeCameraRig.LookAt(eye,target,rig.PivotSide,rig.PivotUp,out yaw,out pitch);
                    now+=Dt;
                }
                var camera=Camera(m,rig,eye,yaw,pitch,now,Open);NativeCameraRig.Basis(yaw,pitch,out var forward,out _);
                float t;Vector3 normal=-Vector3.UnitZ;
                if(wall){t=(target.Z-camera.Z)/forward.Z;}
                else{t=RaySphere(camera,forward,target,.45f);}
                Check(t>0,m.Name+": the crosshair ray meets the target at "+d+" m");
                var P=camera+forward*t;float skip=NativeAimConvergence.Skip(camera,forward,eye);Check(m.First?skip<1e-4f:t>skip,m.Name+": the crosshair point lies beyond the pilot's own depth on the ray");
                bool converges=!m.First&&!rig.Classic(now);
                float sentYaw=yaw,sentPitch=-pitch;if(converges)NativeAimConvergence.Angles(eye,P,yaw,pitch,out sentYaw,out sentPitch);
                worstYaw=Math.Max(worstYaw,Math.Abs(NativeAimConvergence.DeltaAngle(yaw,sentYaw)));
                // The authority's aim along the sent look from the eye (AuthorityAim and the Yaw/Pitch consumers).
                var look=NativeAimConvergence.Direction(sentYaw,sentPitch);float eyeMiss=NativeAimConvergence.Miss(eye,look,P);
                if(converges||m.First)worstEye=Math.Max(worstEye,eyeMiss);else worstRaw=Math.Max(worstRaw,eyeMiss);
                if(converges)worstRaw=Math.Max(worstRaw,NativeAimConvergence.Miss(eye,NativeAimConvergence.Direction(yaw,-pitch),P));
                // The primary: NativeArena.Fire starts at body + .3 up + yaw (.2, 0, .48) and flies at AimTarget (= P).
                var start=body+Vector3.UnitY*.3f+Yawed(sentYaw,new Vector3(.2f,0,.48f));worstGun=Math.Max(worstGun,NativeAimConvergence.Miss(start,P-start,P));
                // A gun or magic ability: AuthorityAim casts its point along the look from the eye against the world (a pilot is
                // not in that mask: the wall behind it) and backs off .03 m; the shot flies from Muzzle (.52 up, yaw (.18, 0,
                // .68)). AimedAbilityPoint: the reported AbilityTarget (= P) on that ray and the authority's own ray meeting the
                // pilot there stop it at the pilot instead.
                var muzzle=body+Vector3.UnitY*.52f+Yawed(sentYaw,new Vector3(.18f,0,.68f));
                Vector3 hitPoint;if(wall){float s=(target.Z-eye.Z)/look.Z;hitPoint=eye+look*s-look*.03f;}else{float s=(target.Z+6-eye.Z)/look.Z;hitPoint=eye+look*Math.Min(s,60);}
                float oldMiss=NativeAimConvergence.Miss(muzzle,hitPoint-muzzle,P);
                if(NativeAimConvergence.OnLook(eye,look,P,(hitPoint-eye).Length(),out float along))
                {
                    float bodyAt=wall?-1:RaySphere(eye,look,target,.45f);
                    if(NativeAimConvergence.Confirms(along,bodyAt,(hitPoint-eye).Length())){hitPoint=eye+look*bodyAt-look*.03f;stopped++;}
                }
                float abilityMiss=NativeAimConvergence.Miss(muzzle,hitPoint-muzzle,P);
                if(converges||m.First){if(wall)worstAbility=Math.Max(worstAbility,abilityMiss);else{worstBodyAbility=Math.Max(worstBodyAbility,abilityMiss);worstOld=Math.Max(worstOld,oldMiss);}}
                else{worstClassicAbility=Math.Max(worstClassicAbility,Math.Abs(abilityMiss-oldMiss));}
                cases++;
            }
            Check(worstGun<.02f,m.Name+": the primary from the real muzzle passes "+Cm(worstGun)+" from the crosshair point");
            if(m.Id!=NativeCameraModes.Classic||m.First)
            {
                Check(worstEye<.02f,m.Name+": the sent look from the eye passes "+Cm(worstEye)+" from the crosshair point");
                Check(worstAbility<.02f,m.Name+": an ability at a world point lands "+Cm(worstAbility)+" from it");
                Check(worstBodyAbility<.02f&&stopped==cases/2,m.Name+": an ability at a pilot passes "+Cm(worstBodyAbility)+" from the crosshair point ("+stopped+" stopped at the pilot)");
                Check(worstYaw<=NativeAimConvergence.MaxYaw,m.Name+": the look turns at most "+worstYaw.ToString("0.0")+" degrees");
                log.Add(m.Name+": "+cases+" targets 2-60 m (pilot volumes and walls, left/centre/right, low/level/high): the primary from the real muzzle passes within "+Cm(worstGun)+", the sent look from the eye within "+Cm(worstEye)+
                    ", an ability at a world point within "+Cm(worstAbility)+" and at a pilot within "+Cm(worstBodyAbility)+" (the authority's ray stops at the pilot; before, up to "+Cm(worstOld)+"); the look turns at most "+worstYaw.ToString("0.0")+" deg"+(m.First?"":"; the unconverged camera look would miss by up to "+Cm(worstRaw)));
            }
            else
            {
                Check(stopped==0&&worstClassicAbility==0,"classic: the authority never takes the reported point ("+stopped+")");
                log.Add(m.Name+": unchanged input (no convergence, as before): the primary still passes within "+Cm(worstGun)+" (it flies at AimTarget); the eye look keeps its old offset (up to "+Cm(worstRaw)+") and the authority's ability point is exactly AuthorityAim, as before");
            }
        }
        // Clamps: a wall almost at the muzzle, the sky, and hits behind the pilot's shot origin.
        {
            NativeAimConvergence.Angles(eye,eye+new Vector3(.62f,0,.4f),0,0,out float y,out float p);Check(Math.Abs(y)<=NativeAimConvergence.MaxYaw+1e-4f&&Math.Abs(p)<1e-4f,"a crosshair point beside the muzzle turns the look at most 25 degrees");
            NativeAimConvergence.Angles(eye,eye+new Vector3(.62f,80,.4f),0,10,out y,out p);Check(Math.Abs(p-(-10+NativeAimConvergence.MaxPitch))<1e-3f,"pitch clamped to 25 degrees around the look");
            NativeAimConvergence.Angles(eye,eye,370,5,out y,out p);Check(y==370&&p==-5,"a point at the eye keeps the camera's look");
            var rig=Rig(All[0]);var cam=rig.Place(eye,0,0,0,Dt,Open);var far=cam+Vector3.UnitZ*(NativeAimConvergence.FarRange+3);NativeAimConvergence.Angles(eye,far,0,0,out y,out p);
            Check(Math.Abs(y)<1&&Math.Abs(p)<.3f,"the sky: the far point keeps the look within "+Math.Abs(y).ToString("0.00")+" degrees");
            float skip=NativeAimConvergence.Skip(cam,Vector3.UnitZ,eye);Check(Math.Abs(skip-NativeCameraShape.ShoulderRight.Back)<.01f,"hits nearer than the pilot's depth ("+skip.ToString("0.00")+" m) are skipped");
            NativeAimConvergence.Angles(eye,eye+new Vector3(0,0,10),720+359,0,out y,out _);Check(Math.Abs(y-(720+360))<1e-3f,"the sent yaw stays continuous with the camera's accumulated yaw");
        }
        // Melee facing: a pilot 2-5 m under the crosshair turns the swing to face it.
        foreach(float d in new[]{2f,3,5})
        {
            var rig=Rig(All[0]);var target=new Vector3(0,Centre+.2f,d);NativeCameraRig.LookAt(eye,target,.62f,.25f,out float yaw,out float pitch);var cam=rig.Place(eye,yaw,pitch,0,Dt,Open);
            NativeCameraRig.Basis(yaw,pitch,out var f,out _);var P=cam+f*RaySphere(cam,f,target,.45f);NativeAimConvergence.Angles(eye,P,yaw,pitch,out float y,out _);
            float bearing=(float)(Math.Atan2(target.X-eye.X,target.Z-eye.Z)*180/Math.PI);Check(Math.Abs(NativeAimConvergence.DeltaAngle(y,bearing))<5,"melee faces a pilot "+d+" m under the crosshair");
        }
        log.Add("clamps: 25 degrees around the look (a wall at the muzzle, a high point), the sky keeps the look, hits behind the shot origin skipped, the yaw continuous; melee turns to a pilot under the crosshair at 2-5 m");
    }

    // ---- the authority's ability ray (NativeAbilities.AimedAbilityPoint through NativeAimConvergence.OnLook / Confirms) ----
    private static void AuthorityRule(List<string> log)
    {
        var eye=new Vector3(0,Centre+Eye,0);var look=Vector3.UnitZ;
        Check(NativeAimConvergence.OnLook(eye,look,eye+new Vector3(.1f,0,8),20,out float along)&&Math.Abs(along-8)<1e-4f,"a report on the look selects its depth");
        Check(!NativeAimConvergence.OnLook(eye,look,eye+new Vector3(.2f,0,8),20,out _),"a report .2 m off the look is refused");
        Check(!NativeAimConvergence.OnLook(eye,look,eye+new Vector3(0,0,25),20,out _)&&!NativeAimConvergence.OnLook(eye,look,eye+new Vector3(0,0,19.995f),20,out _),"a report at or past the authority's world point (a wall or the range) is refused");
        Check(!NativeAimConvergence.OnLook(eye,look,eye+new Vector3(0,0,-3),20,out _)&&!NativeAimConvergence.OnLook(eye,look,eye+new Vector3(0,0,.2f),20,out _),"a report behind or at the eye is refused");
        Check(!NativeAimConvergence.OnLook(eye,look,new Vector3(float.NaN,0,5),20,out _),"a non-finite report is refused");
        Check(NativeAimConvergence.Confirms(8,7.7f,20)&&!NativeAimConvergence.Confirms(8,-1,20)&&!NativeAimConvergence.Confirms(8,6.9f,20)&&!NativeAimConvergence.Confirms(8,21,20),"only a body the authority's own ray meets near the reported depth, before the world point, confirms it");
        // Classic: its crosshair ray runs .55 m above the eye ray (parallel), so no report qualifies at any pitch or distance.
        int classic=0,shoulder=0,checkedShoulder=0;
        foreach(float pitch in new[]{-70f,-45,-20,0,12,30,50,70})foreach(float d in new[]{1f,3,8,20,45})
        {
            NativeCameraRig.Basis(0,pitch,out var f,out _);var cam=eye-f*NativeViewRules.ThirdDistance+Vector3.UnitY*NativeViewRules.ThirdLift;var report=cam+f*(NativeViewRules.ThirdDistance+d);
            if(NativeAimConvergence.OnLook(eye,NativeAimConvergence.Direction(0,-pitch),report,60,out _))classic++;
            // A shoulder rig aimed the same way: its converged look passes the report.
            var rig=Rig(All[0]);var at=rig.Place(eye,0,pitch,0,Dt,Open);var P=at+f*((eye-at).Length()+d);
            NativeAimConvergence.Angles(eye,P,0,pitch,out float y,out float p);if(Math.Abs(NativeAimConvergence.DeltaAngle(0,y))<NativeAimConvergence.MaxYaw-.01f&&Math.Abs(p+pitch)<NativeAimConvergence.MaxPitch-.01f){checkedShoulder++;if(NativeAimConvergence.OnLook(eye,NativeAimConvergence.Direction(y,p),P,80,out _))shoulder++;}
        }
        Check(classic==0,"classic never qualifies ("+classic+" of 40 looks)");Check(shoulder==checkedShoulder&&checkedShoulder>=30,"a converged shoulder look always carries its report ("+shoulder+"/"+checkedShoulder+")");
        log.Add("authority: a gun or magic ability's own aim ray stops only at a body it meets within .6 m of a report lying within .15 m of the ray, .3 m out and before the world point (off-ray, past a wall or the range, behind the eye and NaN refused); classic never qualifies (0 of 40 looks), converged shoulder looks always do ("+shoulder+"/"+checkedShoulder+")");
    }

    // ---- lock-on framing ----
    private static void LockOn(List<string> log)
    {
        var rnd=new Random(7);float worst=0;
        foreach(var m in All.Where(x=>!x.First&&x.Id!=NativeCameraModes.Classic))
            for(int i=0;i<200;i++)
            {
                var eye=new Vector3((float)rnd.NextDouble()*10-5,1.385f,(float)rnd.NextDouble()*10-5);var target=eye+new Vector3((float)rnd.NextDouble()*16-8,(float)rnd.NextDouble()*2-1.3f,(float)rnd.NextDouble()*16-8);
                if(new Vector2(target.X-eye.X,target.Z-eye.Z).Length()<1.5f)continue;
                var rig=Rig(m);rig.Place(eye,0,0,0,Dt,Open);NativeCameraRig.LookAt(eye,target,rig.PivotSide,rig.PivotUp,out float yaw,out float pitch);
                var cam=rig.Place(eye,yaw,pitch,Dt,Dt,Open);NativeCameraRig.Basis(yaw,pitch,out var f,out _);worst=Math.Max(worst,NativeAimConvergence.Miss(cam,f,target));
            }
        Check(worst<.002f,"the lock-on look puts the crosshair ray on the target ("+Cm(worst)+")");
        log.Add("lock-on framing: the shoulder rigs aim their crosshair ray through the locked target (within "+Cm(worst)+" over 600 random locks); first person and classic keep the original eye look");
    }

    // ---- collision ----
    private static void Collision(List<string> log)
    {
        var eye=new Vector3(0,Centre+Eye,0);var floor=new Box(new Vector3(0,-.25f,0),new Vector3(30,.25f,30));
        // A wall on the right shoulder, the left open: the shoulder pulls in at once, then the rig swaps to the open side.
        {
            var right=new Box(new Vector3(.6f,2,0),new Vector3(.1f,3,8));var world=new[]{floor,right};var rig=Rig(All[0]);float now=0;var cast=CastOf(world);
            var first=rig.Place(eye,0,8,now,Dt,cast);Check(right.Distance(first)>=.12f-1e-4f&&rig.PivotSide<.62f,"a wall .5 m right pulls the shoulder in at once ("+rig.PivotSide.ToString("0.00")+" m side)");
            float minGap=9;Vector3 at=first;for(int f=0;f<90;f++){now+=Dt;at=rig.Place(eye,0,8,now,Dt,cast);minGap=Math.Min(minGap,right.Distance(at));}
            Check(rig.Swapped&&at.X<-.5f&&minGap>=.12f-1e-4f,"then the camera swaps to the open left shoulder (x "+at.X.ToString("0.00")+"), never nearer the wall than "+Cm(minGap));
            // The wall goes: the chosen shoulder returns after the hold.
            var open=CastOf(new[]{floor});for(int f=0;f<90;f++){now+=Dt;at=rig.Place(eye,0,8,now,Dt,open);}
            Check(!rig.Swapped&&at.X>.5f,"with the shoulder clear again it returns to the right ("+at.X.ToString("0.00")+")");
        }
        // A corridor: both shoulders tight, no swap; the offset flattens toward the middle.
        {
            var world=new[]{floor,new Box(new Vector3(.6f,2,0),new Vector3(.1f,3,8)),new Box(new Vector3(-.6f,2,0),new Vector3(.1f,3,8))};var cast=CastOf(world);var rig=Rig(All[0]);float now=0;Vector3 at=default;
            for(int f=0;f<60;f++){at=rig.Place(eye,0,8,now,Dt,cast);now+=Dt;}
            Check(!rig.Swapped&&Math.Abs(at.X)<.35f&&world.Skip(1).All(b=>b.Distance(at)>=.12f-1e-4f),"a 1 m corridor flattens the offset (x "+at.X.ToString("0.00")+"), no swap");
        }
        // A wall behind: pulled in on the first frame, eased out once it is gone (not in one frame), back to full within 1 s.
        {
            var back=new Box(new Vector3(0,2,-1.6f),new Vector3(4,3,.1f));var rig=Rig(All[0]);float now=0;
            var at=rig.Place(eye,0,0,now,Dt,CastOf(new[]{floor,back}));Check(back.Distance(at)>=.12f-1e-4f&&rig.Boom<1.5f,"a wall 1.5 m behind pulls the boom in at once ("+rig.Boom.ToString("0.00")+" m)");
            var open=CastOf(new[]{floor});now+=Dt;rig.Place(eye,0,0,now,Dt,open);float second=rig.Boom;float t=0;while(rig.Boom<NativeCameraShape.ShoulderRight.Back-1e-4f&&t<2){now+=Dt;t+=Dt;rig.Place(eye,0,0,now,Dt,open);}
            Check(second<NativeCameraShape.ShoulderRight.Back-.2f&&t<1,"then eases out ("+second.ToString("0.00")+" m the next frame, full after "+t.ToString("0.00")+" s)");
        }
        // A pillar edge coming up on the shoulder side as the pilot runs past it (6.8 m/s): the look-ahead pulls in early.
        float edge=0;
        foreach(float speed in new[]{3f,6.8f})
        {
            var pillar=new Box(new Vector3(1f,2,2f),new Vector3(.35f,3,.5f));var cast=CastOf(new[]{floor,pillar});var rig=Rig(All[0]);float now=0,z=0,jump=0,prev=-1,least=9;
            for(int f=0;f<70;f++){var e=eye+new Vector3(0,0,z);var at=rig.Place(e,0,8,now,Dt,cast);float s=rig.PivotSide;if(prev>=0)jump=Math.Max(jump,Math.Abs(prev-s));prev=s;least=Math.Min(least,s);Check(pillar.Distance(at)>=.12f-1e-4f,"the pillar never takes the camera");z+=speed*Dt;now+=Dt;}
            Check(least<.5f&&jump<.1f,"a pillar edge on the shoulder side pulls in to "+least.ToString("0.00")+" m, at most "+Cm(jump)+" a frame at "+speed+" m/s");edge=Math.Max(edge,jump);
        }
        log.Add("collision: a wall on the shoulder pulls in at once and then swaps to the open shoulder (back when clear), a corridor flattens the offset, a wall behind pulls the boom in at once and eases out within 1 s, running past a pillar edge moves the side at most "+Cm(edge)+" a frame; the camera sphere never meets a wall");
    }
    // Real maps: Brickfall (UnityMaps.json) and Foundry (GameContent.json) blocks. On a grid of free standing spots (the floor
    // under them found by a ray, the pilot's capsule clear) at eight yaws and three pitches, no rig places the camera centre
    // within the near clip plane (.06 m) of a block.
    private static List<Box> Blocks(JsonElement map)
    {
        var list=new List<Box>();
        foreach(var b in map.GetProperty("blocks").EnumerateArray())
        {
            var p=b.GetProperty("pos");var s=b.GetProperty("size");var r=b.GetProperty("rotation");
            list.Add(new Box(new Vector3(p.GetProperty("x").GetSingle(),p.GetProperty("y").GetSingle(),p.GetProperty("z").GetSingle()),new Vector3(s.GetProperty("x").GetSingle(),s.GetProperty("y").GetSingle(),s.GetProperty("z").GetSingle())*.5f,
                new Quaternion(r.GetProperty("x").GetSingle(),r.GetProperty("y").GetSingle(),r.GetProperty("z").GetSingle(),r.GetProperty("w").GetSingle())));
        }
        return list;
    }
    private static void Maps(string root,List<string> log)
    {
        var maps=new List<(string name,List<Box> boxes)>();
        using(var u=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"))))maps.Add(("Brickfall",Blocks(u.RootElement.GetProperty("maps").EnumerateArray().First(m=>m.GetProperty("id").GetString()=="brickfall"))));
        using(var g=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json"))))maps.Add(("Foundry",Blocks(g.RootElement.GetProperty("maps").EnumerateArray().First(m=>m.GetProperty("id").GetString()=="foundry"))));
        foreach(var (name,boxes) in maps)
        {
            int spots=0,placements=0,swaps=0,pulled=0;float nearest=9;
            for(float x=-28;x<=28;x+=2.1f)for(float z=-24;z<=24;z+=2.3f)
            {
                float down=Ray(boxes,new Vector3(x,40,z),-Vector3.UnitY,60);if(down>=60)continue;var feet=new Vector3(x,40-down,z);if(feet.Y>12)continue;
                var centre=feet+Vector3.UnitY*(Centre+.02f);bool free=true;foreach(var b in boxes)if(b.Distance(centre)<Capsule||b.Distance(centre+Vector3.UnitY*.43f)<Capsule||b.Distance(centre-Vector3.UnitY*.42f)<Capsule-.03f){free=false;break;}
                if(!free)continue;spots++;var eye=centre+Vector3.UnitY*Eye;
                // Broad phase: the blocks whose bounds come within 7 m of the eye (the longest rig reaches 4.4 m + .55).
                var near=new List<Box>();foreach(var b in boxes)if((b.C-eye).Length()<7+b.H.Length())near.Add(b);var cast=CastOf(near);
                foreach(var m in All.Where(k=>!k.First))
                    for(int yi=0;yi<8;yi++)foreach(float pitch in new[]{-35f,8,45})
                    {
                        var rig=Rig(m);float now=0;Vector3 at=default;for(int f=0;f<3;f++){at=Camera(m,rig,eye,yi*45,pitch,now,cast);now+=Dt;}
                        if(rig.Swapped)swaps++;if(!rig.Classic(now)&&rig.Boom<NativeCameraShape.Of(m.Id,m.Left).Back-.01f)pulled++;
                        foreach(var b in near){float d=b.Distance(at);if(d<nearest)nearest=d;}placements++;
                    }
            }
            Check(spots>40&&nearest>=.06f,name+": no camera within the near plane of a block (nearest "+Cm(nearest)+")");
            log.Add(name+" ("+boxes.Count+" blocks): "+spots+" free spots x 4 third-person modes x 24 looks = "+placements+" placements; the camera centre stays at least "+Cm(nearest)+" from every block (near plane 6 cm); "+pulled+" pulled in, "+swaps+" temporary swaps");
        }
    }

    // ---- blends ----
    private static void Blends(List<string> log)
    {
        var eye=new Vector3(0,Centre+Eye,0);
        (float maxStep,Vector3 end,Vector3 start,Vector3 before) Switch(string fromMode,bool fromLeft,string toMode,bool toLeft)
        {
            var rig=new NativeCameraRig();float now=0;rig.Select(fromMode,fromLeft,now);Vector3 prev=default;
            for(int f=0;f<10;f++){prev=rig.Place(eye,30,10,now,Dt,Open);now+=Dt;}
            var before=prev;rig.Select(toMode,toLeft,now);var start=rig.Place(eye,30,10,now,Dt,Open);float max=(start-before).Length();prev=start;
            for(int f=0;f<30;f++){now+=Dt;var at=rig.Place(eye,30,10,now,Dt,Open);max=Math.Max(max,(at-prev).Length());prev=at;}
            return (max,prev,start,before);
        }
        var swap=Switch("shoulder",false,"shoulder",true);var target=new NativeCameraRig();target.Select("shoulder",true,0);var want=target.Place(eye,30,10,0,Dt,Open);
        float span=2*NativeCameraShape.ShoulderRight.Side,frames=NativeCameraRig.Blend/Dt;
        Check((swap.start-swap.before).Length()<1e-4f,"a shoulder swap starts where the camera was");
        Check(swap.maxStep<=1.5f*span/frames+.005f&&swap.maxStep<.13f,"a swap moves at most "+Cm(swap.maxStep)+" a frame (smoothstep over .25 s)");
        Check((swap.end-want).Length()<1e-4f,"and ends exactly on the left shoulder");
        var toClassic=Switch("shoulder",false,"classic",false);var classicRig=new NativeCameraRig();classicRig.Select("classic",false,0);NativeCameraRig.Basis(30,10,out var f2,out _);
        var classicAt=eye-f2*NativeViewRules.ThirdDistance+Vector3.UnitY*NativeViewRules.ThirdLift;
        Check((toClassic.end-classicAt).Length()<1e-4f&&toClassic.maxStep<.2f,"shoulder to classic blends ("+Cm(toClassic.maxStep)+" a frame at most) onto the classic formula");
        var toClose=Switch("shoulder",false,"close",false);Check(toClose.maxStep<.11f,"shoulder to close action "+Cm(toClose.maxStep)+" a frame");
        // A switch back halfway through a blend continues from where the camera is.
        {
            var rig=new NativeCameraRig();float now=0;rig.Select("shoulder",false,now);rig.Place(eye,0,0,now,Dt,Open);rig.Select("shoulder",true,now);Vector3 prev=default;
            for(int f=0;f<7;f++){now+=Dt;prev=rig.Place(eye,0,0,now,Dt,Open);}rig.Select("shoulder",false,now);var again=rig.Place(eye,0,0,now,Dt,Open);
            Check((again-prev).Length()<.13f,"a reversal mid-blend continues from the current camera");
        }
        // Not settled: Classic only after the blend; selecting the same mode again does not restart it.
        {
            var rig=new NativeCameraRig();rig.Select("shoulder",false,0);rig.Select("classic",false,1);Check(!rig.Classic(1.1f)&&rig.Classic(1.25f)&&rig.Classic(5),"Classic counts once its blend is done");
            var fresh=new NativeCameraRig();fresh.Select("classic",false,3);Check(fresh.Classic(3),"a classic save starts settled (no blend on load)");
            rig.Select("classic",false,4);Check(rig.Classic(4),"re-selecting the same mode keeps it settled");
        }
        log.Add("blends: a swap starts in place, moves at most "+Cm(swap.maxStep)+" a 60 Hz frame and ends exactly on the other shoulder in .25 s; shoulder to classic lands on the classic formula; close action "+Cm(toClose.maxStep)+" a frame; reversals continue in place; Classic only once settled, a classic save starts settled");
    }

    // ---- focus smoothing ----
    private static void Focus(List<string> log)
    {
        // Stairs: .25 m steps every .12 s (a 6.8 m/s run up a flight). The eye jumps; the camera's focus steps at most a third.
        var rig=new NativeCameraRig();float y=1.385f,t=0,step=0,lag=0,prev=rig.Focus(y,1,Dt);
        for(int f=0;f<120;f++){t+=Dt;if(f%7==0&&f<84)y+=.25f;float h=rig.Focus(y,1,Dt);step=Math.Max(step,Math.Abs(h-prev));lag=Math.Max(lag,Math.Abs(y-h));prev=h;}
        Check(step<.25f*.4f&&lag<=NativeCameraRig.FocusLag+1e-5f&&Math.Abs(prev-y)<.005f,"stairs: the focus moves at most "+Cm(step)+" a frame for .25 m steps, trails at most "+Cm(lag)+", and settles");
        // A landing at 10 m/s: the focus eases into the stop with no overshoot below the eye.
        var land=new NativeCameraRig();y=3.385f;land.Focus(y,1,Dt);float overshoot=0;prev=y;
        for(int f=0;f<60;f++){y=Math.Max(1.385f,y-10*Dt);float h=land.Focus(y,1,Dt);overshoot=Math.Max(overshoot,y-h);prev=h;}
        Check(overshoot<1e-4f&&Math.Abs(prev-1.385f)<.005f,"a landing eases in from above with no overshoot");
        // A new pilot (spectating) or a teleport snaps.
        Check(rig.Focus(50,1,Dt)==50&&rig.Focus(51,2,Dt)==51,"a jump over 2 m or a new pilot snaps");
        log.Add("focus: on stairs (.25 m steps) the camera's focus moves at most "+Cm(step)+" a frame and trails at most "+Cm(lag)+"; landings ease in with no overshoot; a new pilot or a teleport snaps (first person and classic keep the exact eye)");
    }

    // ---- sprint ----
    private static void Sprint(List<string> log)
    {
        var s=new NativeCameraSprint();var a=new Actor{Id=1,Hp=100,Grounded=true,Sprinting=true,Velocity=new V3(0,0,9.18f)};float time=1;
        for(int f=0;f<12;f++){s.Step(a,time,Dt);time+=Dt;}
        Check(s.Weight>.99f&&NativeSprintRules.Fov(s.Weight,true,false)==NativeSprintRules.FovKick&&NativeSprintRules.Fov(s.Weight,false,false)==0,"the shoulder rigs take the full kick while sprinting, none with camera shake off");
        NativeSprintRules.CameraSway(s.Weight,s.Stride,true,false,out float up,out _);Check(up<=0&&up>=-NativeSprintRules.CameraBob,"the bob dips at most "+Cm(NativeSprintRules.CameraBob));
        a.Sprinting=false;for(int f=0;f<12;f++){s.Step(a,time,Dt);time+=Dt;}Check(s.Weight<.01f,"and ramps out");
        a.Sprinting=true;for(int f=0;f<12;f++){s.Step(a,time,Dt);time+=Dt;}a.Aiming=true;for(int f=0;f<10;f++){s.Step(a,time,Dt);time+=Dt;}Check(s.Weight<.01f,"an aim or shot drops it like the first-person view");
        log.Add("sprint: the shoulder rigs ramp the first-person FOV kick ("+NativeSprintRules.FovKick+" degrees, camera shake only) and bob from the replicated sprint, out again on stopping or aiming");
    }

    private static void Allocation(List<string> log)
    {
        var rig=new NativeCameraRig();rig.Select("shoulder",false,0);var eye=new Vector3(0,1.385f,0);var cast=CastOf(new[]{new Box(new Vector3(.9f,2,0),new Vector3(.1f,3,8))});var sprint=new NativeCameraSprint();var a=new Actor{Id=1,Hp=100,Grounded=true,Sprinting=true,Velocity=new V3(0,0,9)};
        for(int i=0;i<200;i++){rig.Place(eye,i,8,i*Dt,Dt,cast);rig.Focus(1.385f+(i%7)*.1f,1,Dt);sprint.Step(a,i*Dt,Dt);}
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<2000;i++){rig.Place(eye,i*.3f,8,4+i*Dt,Dt,cast);rig.Focus(1.385f+(i%7)*.1f,1,Dt);sprint.Step(a,4+i*Dt,Dt);NativeAimConvergence.Angles(eye,eye+new Vector3(1,0,9),i,3,out _,out _);NativeCameraRig.LookAt(eye,eye+new Vector3(2,0,6),.62f,.25f,out _,out _);}
        long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"no allocation per frame ("+used+" bytes)");
        log.Add("no allocation per frame (placement, focus, sprint, convergence, lock-on solve)");
    }

    // ---- Classic and the hooks ----
    private static void Classic(string root,List<string> log)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
        string cg=R("Runtime/ConversionGame.cs"),sc=R("Runtime/NativeShoulderCamera.cs"),lk=R("Runtime/NativeLockOn.cs"),st=R("Runtime/NativeSettings.cs"),sp=R("Runtime/NativeSprintFxPresentation.cs");
        // The original third-person camera math, with its query scoped to the displayed scene, is the settled-Classic branch.
        string original="            else if(!first)\n            {\n                Vector3 offset=rotation*Vector3.back*NativeViewRules.ThirdDistance+Vector3.up*NativeViewRules.ThirdLift;\n                location=focus+offset;\n"
            +"                if(arena.PhysicsWorld.SphereCast(focus,.12f,offset.normalized,out var hit,offset.magnitude,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))location=focus+offset.normalized*Mathf.Max(.15f,hit.distance-NativeViewRules.CollisionPadding);\n            }";
        Check(cg.Contains("bool classic=CameraClassic(first);")&&cg.Contains("if(!first&&!classic)location=ShoulderCamera(p,focus);\n"+original),"settled Classic retains original camera math in its own scene");
        Check(cg.Contains("first?NativeViewRules.FirstFov:classic?NativeViewRules.ThirdFov:cameraRig.Fov(Time.unscaledTime)")&&NativeCameraShape.ClassicShape.Fov==NativeViewRules.ThirdFov,"Classic keeps the 60 degree FOV");
        Check(sc.Contains("convergeAim=shoulderView&&Spectator==null&&!menu;")&&sc.Contains("if(!convergeAim||cinematic.Directing)return;")&&sc.Contains("aimSkip=convergeAim?")&&cg.Contains("hit.distance<aimSkip)continue;"),"Classic and first person send the camera look and the old AimTarget (no convergence, no skip)");
        Check(lk.Contains("LockLook(eye,target.point,out float wantYaw,out float wantPitch)")&&sc.Contains("if(!shoulderView||presentation.FirstPerson){wantYaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;wantPitch=Mathf.Clamp(-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg+3.438f,-63,63);return;}"),"Classic and first person keep the original lock-on look");
        Check(sp.Contains("!first&&!aim?ShoulderSprintFov():0")&&sc.Contains("ShoulderSprintFov()=>shoulderView&&")&&sc.Contains("if(!shoulderView||presentation.FirstPerson)return;"),"Classic keeps no third-person sprint kick, as before");
        // The aim convergence sits after the camera-ray targets and before the shakes.
        int target=cg.IndexOf("pending.AbilityTarget=NativeArena.Vec(AimPoint("),converge=cg.IndexOf("ConvergeAim(p);"),shake=cg.IndexOf("NativeShakeRules.Offset(");
        Check(target>0&&converge>target&&shake>converge,"the convergence follows AimTarget/AbilityTarget, before any shake moves the camera");
        // The authority's one addition: gun and magic Q/E stop their own aim ray at a confirmed body (bots excluded); the
        // primary, hit validation, prediction and the wire are untouched; AuthorityAim and its wall check remain.
        string ab=R("Runtime/NativeAbilities.cs"),pv=R("Runtime/NativeParityValidation.cs");
        Check(ab.Contains("Vector3 point=AimedAbilityPoint(p,(float)d.range);")&&ab.Contains("Vector3 point=AuthorityAim(p,range);if(p.Seat.bot||p.Input.AbilityTarget.Length<.01f)return point;")
            &&ab.Contains("return NativeAimConvergence.Confirms(along,body,world)?at-look*.03f:point;")&&ab.Contains("if(q==p||!(q!=null&&q.Hp>0||m!=null&&m.Hp>0)"),"the authority ends a gun or magic ability's own ray at a confirmed live body, never the caster; bots keep AuthorityAim");
        Check(pv.Contains("private Vector3 AuthorityAim(NativePilot p,float range)")&&pv.Contains("\"reported target cannot bypass a wall\""),"AuthorityAim and its wall check are unchanged");
        foreach(var f in new[]{"Runtime/NativeArena.cs","Runtime/NativeProjectiles.cs","Runtime/NativeParityValidation.cs","Runtime/NativeMelee.cs","Runtime/NativeMeleeAbilities.cs","Runtime/NativeNetworkFrame.cs","Runtime/NativeReplicaMotion.cs","Core/WireCodec.cs","Core/ArenaModel.cs","Core/NativeMeleePrediction.cs"})
            Check(!R(f).Contains("NativeCameraRig")&&!R(f).Contains("NativeAimConvergence"),f+" (primaries, melee, prediction and wire) does not use the camera");
        // Preferences and keys.
        Check(cg.Contains("public string cameraMode=NativeCameraModes.Default;public bool shoulderLeft;")&&cg.Contains("prefs.cameraMode=NativeCameraModes.Sanitize(prefs.cameraMode);"),"the saved camera has a default (older saves: the right shoulder) and is sanitized on load");
        Check(st.Contains("Field(\"Camera\");NativeCameraModes.Choose(ChoiceGrid(NativeCameraModes.Choices,"),"Settings · Camera offers the five modes");
        Check(sc.Contains("Input.GetKeyDown(KeyCode.X)")&&cg.Contains("CameraKeys(typing);"),"X swaps the shoulder");
        var keyed=Directory.GetFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories).Where(f=>File.ReadAllText(f).Contains("KeyCode.X")).Select(Path.GetFileName).ToArray();
        Check(keyed.Length==1&&keyed[0]=="NativeShoulderCamera.cs","X is bound nowhere else ("+string.Join(",",keyed)+")");
        log.Add("Classic is the old camera: its original placement code verbatim (settled), 60 degree FOV, unconverged look and AimTarget, the original lock-on look and no third-person sprint kick; the authority's one addition (gun/magic Q/E aim ray stops at a confirmed body) wired, AuthorityAim kept, primaries, prediction and wire code untouched; the saved camera defaults to the right shoulder; Settings · Camera; X bound only here");
    }
}
