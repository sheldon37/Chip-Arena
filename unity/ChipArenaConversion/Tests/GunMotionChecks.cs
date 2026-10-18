using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

/// <summary>
/// The third-person gun gait (Core/GunMotion) and the Siren's drift layer (Core/NativeSirenDrift): routes, stances, the
/// unchanged first-person hold, torso and hip motion with the arm counter-swing at run speed, the gun aimed and in the hand,
/// stride phase against speed with planted feet, continuity at 30-240 Hz and no allocation. Presentation only.
/// </summary>
internal static class GunMotionChecks
{
    private const float D=(float)Math.PI/180;
    private static readonly WeaponId[] Guns={WeaponId.Pistol,WeaponId.Rifle,WeaponId.MachineGun};
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Gun motion: "+what);}
    private static Actor Gun(WeaponId w,int id=31)=>new Actor{Id=id,SourceSpec=GunMotion.Spec(w),Weapon=w,Grounded=true,Hp=100,CastRate=1,RollDirection=new V3(0,0,1),Ammo=GunRules.Get(w).Magazine};
    private static float Tip(WeaponId w)=>w==WeaponId.Rifle?1.21f:w==WeaponId.MachineGun?.9f:.47f;
    public static void Run()
    {
        var log=new List<string>();
        Routes();Stances();LegacyHold(log);
        foreach(var w in Guns){Travel(w,log);Aimed(w,log);Stride(w,log);}
        Continuity(log);Allocation(log);Siren(log);
        foreach(var line in log)Console.WriteLine("PASS gun motion: "+line);
    }
    // ---- a pilot driven as CapturedAnimation.Player.GunGaitPose drives it ----
    private sealed class Driver
    {
        public readonly GunMotion Motion=new GunMotion();public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly NativeJuggleTrack Juggle=new NativeJuggleTrack();
        public Vector3 Root;public bool React;public float Yaw;
        public string Step(Actor a,float t,float dt)
        {
            Root+=new Vector3(a.Velocity.x,0,a.Velocity.z)*dt;
            bool juggled=Juggle.Observe(a.JuggleUntil,t)&&NativeJugglePose.Active(a.JuggleUntil,t),roll=!juggled&&a.Rolling(t);
            Yaw=roll?(float)Math.Atan2(a.RollDirection.x,a.RollDirection.z)/D:a.Yaw;
            Motion.Rig.GroupPosition=new Vector3(-Root.X,.865f,Root.Z);Motion.Rig.GroupYaw=-Yaw*D;
            string state=Motion.Pose(a,t,Pose,juggled,Juggle,out React);Motion.Finish(Pose,t);return state;
        }
        public Quaternion Group=>MeleeMath.AxisAngle(MeleeMath.Y,Motion.Rig.GroupYaw);
        // Source-frame world position and orientation of a joint.
        public Vector3 World(int bone)=>Motion.Rig.GroupPosition+Vector3.Transform(Motion.Rig.ChainPoint(bone,-1),Group);
        public Quaternion WorldQ(int bone)=>Group*Motion.Rig.ChainQuat(bone,-1);
        public Vector3 Local(int bone)=>Motion.Rig.ChainPoint(bone,-1);
        public Vector3 Muzzle(WeaponId w)=>World(CapturedRig.RHand)+Vector3.Transform(new Vector3(0,0,Tip(w)),WorldQ(CapturedRig.RHand));
        // The support grip the left hand holds (RangedPose: the reached wrist plus Support along the gun).
        public float SupportGap(WeaponId w)=>Vector3.Distance(World(CapturedRig.LHand),World(CapturedRig.RHand)+Vector3.Transform(RangedPose.Support(w),WorldQ(CapturedRig.RHand)));
    }
    private static float Corr(List<float> a,List<float> b)
    {
        float ma=0,mb=0;for(int i=0;i<a.Count;i++){ma+=a[i];mb+=b[i];}ma/=a.Count;mb/=b.Count;
        float s=0,sa=0,sb=0;for(int i=0;i<a.Count;i++){float x=a[i]-ma,y=b[i]-mb;s+=x*y;sa+=x*x;sb+=y*y;}
        return s/(float)Math.Sqrt(Math.Max(1e-12f,sa*sb));
    }
    private static float Range(List<float> v){float lo=float.MaxValue,hi=float.MinValue;foreach(float x in v){lo=Math.Min(lo,x);hi=Math.Max(hi,x);}return hi-lo;}
    private static void Routes()
    {
        foreach(var w in Guns)foreach(bool bot in new[]{false,true})
        {
            var a=Gun(w);a.Bot=bot;
            Check(NativePoseRoute.For(a,false)==PoseRoute.Gun,w+(bot?" bot":"")+": third person walks on the gun gait");
            Check(NativePoseRoute.For(a,true)==PoseRoute.Generic,w+": first person keeps the view-model path");
            Check(!NativePoseRoute.SwordLayers(a),w+": no sword layers");
        }
        Check(NativePoseRoute.For(new Actor{Weapon=WeaponId.Rifle},false)==PoseRoute.Gun,"a prototype gunner without a source spec walks on the gun gait");
        Check(NativePoseRoute.For(new Actor{SourceSpec="siren",Weapon=WeaponId.Sword},false)==PoseRoute.Generic,"the Siren keeps her baked motion");
        Check(NativePoseRoute.For(new Actor{SourceSpec="sword",Weapon=WeaponId.Sword},false)==PoseRoute.CapturedMelee,"sword keeps captured melee");
        Check(NativePoseRoute.For(new Actor{SourceSpec="fire",MagicVisual=1,Weapon=WeaponId.Sword},false)==PoseRoute.Magic,"fire keeps the magic gait");
        Check(NativePoseRoute.For(new Actor{SourceSpec="leet",Weapon=WeaponId.Sword},false)==PoseRoute.Magic,"L33t H4x keeps the magic gait");
        Check(NativePoseRoute.For(new Actor{SourceSpec="ansatsuken",Weapon=WeaponId.Sword},false)==PoseRoute.Fighter,"Ansatsuken keeps its style");
    }
    // stances.ts pistol / rifle / machinegun.
    private static void Stances()
    {
        void Is(MeleeStance s,float lx,float lz,float lt,float rx,float rz,float rt,float crouch,float yaw,float lean,float knee,string name)
        {
            float[] got={s.LeadX,s.LeadZ,s.LeadToe,s.RearX,s.RearZ,s.RearToe,s.Crouch,s.PelvisYaw,s.Lean,s.KneeSoft},want={lx,lz,lt,rx,rz,rt,crouch,yaw,lean,knee};
            for(int i=0;i<got.Length;i++)Check(Math.Abs(got[i]-want[i])<1e-6f&&s.LeadLeft,name+" stance field "+i);
        }
        Is(MeleeStance.For("pistol"),.15f,.04f,.12f,-.15f,-.03f,.2f,-.015f,-.05f,.035f,0,"pistol");
        Is(MeleeStance.For("rifle"),.09f,.12f,-.3f,-.10f,-.10f,.85f,-.025f,-.55f,.04f,0,"rifle");
        Is(MeleeStance.For("machinegun"),.19f,.10f,.2f,-.19f,-.04f,.4f,-.03f,-.1f,.08f,.15f,"machinegun");
        Check(MeleeStance.For("sword")==MeleeStance.Melee&&MeleeStance.For("leet")==MeleeStance.Magic&&MeleeStance.For("siren")==MeleeStance.Neutral,"the other stances are unchanged");
        Check(new MeleeGait().PelvisYawScale==1,"the gait takes the whole stance pelvis turn by default");
    }
    // The first-person view model, the gun viewer and the checks still get the hold exactly as before the refactor.
    private static void Legacy(CapturedRig.Pose p,Actor a,float time,float aimWeight)
    {
        bool longGun=a.Weapon!=WeaponId.Pistol,mg=a.Weapon==WeaponId.MachineGun;const float Rad=(float)Math.PI/180;
        float kick=RangedPose.Recoil(a.Weapon,time-a.LastShot),e,u=RangedPose.ReloadPhase(a,time,out e),tuck=RangedPose.Curve(new[]{0,.12f,.8f,.95f,1},new[]{0,1f,1,0,0},u)*e;
        float guard=time<a.BlockUntil?CapturedClip.Ease((time-a.BlockUntil+Rules.BlockDuration)/.07f):0;
        float hold=aimWeight+(1-aimWeight)*guard,blade=(longGun?.75f:.04f)*hold*(1-guard),pitch=a.Pitch*Rad;
        p.Rotation[CapturedRig.Pelvis]=Quaternion.Identity;
        p.Rotation[CapturedRig.Spine]=CapturedRig.Euler(-pitch*.22f*hold+guard*.12f,-blade*.45f);
        p.Rotation[CapturedRig.Chest]=CapturedRig.Euler(-pitch*.18f*hold-kick*.05f*hold+tuck*.06f,-blade*.55f+tuck*.2f);
        p.Rotation[CapturedRig.Neck]=CapturedRig.Euler(-pitch*.10f*hold+(longGun?.15f:0),0,longGun?.12f:0);
        p.Rotation[CapturedRig.Head]=CapturedRig.Euler(pitch*.5f*hold+(longGun?.12f:0)+kick*.05f+tuck*.25f,blade,longGun?.06f:-.08f);
        p.Rotation[CapturedRig.LClav]=CapturedRig.Euler(0,-(longGun?.6f:.15f)*hold);
        p.Rotation[CapturedRig.RClav]=CapturedRig.Euler(0,(longGun?.9f:0)*hold-kick*.08f*hold);
        p.World();
        Quaternion aim=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest])*CapturedRig.Rot(CapturedRig.Y,-(a.AimYaw-a.Yaw)*Rad)*CapturedRig.Rot(CapturedRig.X,-pitch-kick*.13f);
        Vector3 target=longGun?new Vector3(-.11f,.245f,.155f)-Vector3.Transform(new Vector3(0,-.04f,-.30f),aim):new Vector3(-.035f,.215f,0)+Vector3.Transform(new Vector3(0,.075f,.37f),aim);
        target-=Vector3.Transform(Vector3.UnitZ,aim)*kick*(longGun?.045f:.035f);
        if(hold<1){aim=Quaternion.Slerp(CapturedRig.Euler(longGun?.3f:.62f,longGun?.38f:.18f,longGun?-.35f:-.12f),aim,hold);target=Vector3.Lerp(new Vector3(-.24f,-.02f,.25f),target,hold);}
        if(guard>0){aim=Quaternion.Slerp(aim,CapturedRig.Euler(longGun?-1.05f:-1.25f,longGun?.55f:.1f,longGun?.75f:0),guard);target=Vector3.Lerp(target,new Vector3(-.1f,.18f,.28f),guard);}
        aim*=CapturedRig.Euler(-.25f*tuck,longGun?.1f*tuck:-.25f*tuck,.5f*tuck);
        target+=new Vector3(longGun?.05f:.09f,longGun?-.02f:-.04f,longGun?-.06f:-.12f)*tuck;
        CapturedRig.Arm(p,false,target,aim,new Vector3(-1,-.7f,-.2f));p.World();
        Quaternion chestInv=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]);
        Vector3 reached=Vector3.Transform(p.WorldPosition[CapturedRig.RHand]-p.WorldPosition[CapturedRig.Chest],chestInv);
        Vector3 start=reached+Vector3.Transform(RangedPose.Support(a.Weapon),aim),left=start;
        if(e>0)
        {
            float[] handTimes={0,.1f,.2f,.3f,.42f,.5f,.6f,.68f,.71f,.74f,.83f,.89f,.97f,1};
            Vector3 well=a.Weapon==WeaponId.Pistol?new Vector3(0,-.30f,-.07f):new Vector3(mg?.02f:0,-.27f,mg?.2f:.16f);
            Vector3 c0=a.Weapon==WeaponId.Pistol?new Vector3(.06f,.02f,-.04f):mg?new Vector3(0,.24f,.02f):new Vector3(-.12f,.06f,-.02f);
            Vector3 c1=a.Weapon==WeaponId.Pistol?new Vector3(.06f,.02f,-.1f):mg?new Vector3(0,.16f,.02f):new Vector3(-.12f,.06f,-.09f);
            Vector3 W(Vector3 v)=>reached+Vector3.Transform(v,aim);
            Vector3 wp=W(well),strip=W(well+new Vector3(0,-.13f,-.02f)),pouch=new Vector3(.15f,-.33f,.07f);
            Vector3[] pts={start,start,wp,strip,pouch,pouch,strip,wp,W(well+new Vector3(0,-.05f,0)),wp,W(c0),W(c1),start,start};
            int k=1;while(k<handTimes.Length-1&&u>handTimes[k])k++;
            left=Vector3.Lerp(start,Vector3.Lerp(pts[k-1],pts[k],CapturedClip.Ease((u-handTimes[k-1])/(handTimes[k]-handTimes[k-1]))),RangedPose.Curve(new[]{0,.08f,.14f,.94f,1},new[]{0,0,1f,1,0},u)*e);
        }
        CapturedRig.Arm(p,true,left,aim,new Vector3(1,-.8f,-.25f));
        if(time<a.StunUntil){p.Rotation[CapturedRig.Spine]*=CapturedRig.Euler(-.12f);p.Rotation[CapturedRig.Head]*=CapturedRig.Euler(.16f);}
        p.World();
    }
    private static void LegacyHold(List<string> log)
    {
        var p=new CapturedRig.Pose();var q=new CapturedRig.Pose();var row=new float[64];int n=0;float worst=0;
        foreach(var w in Guns)foreach(float aim in new[]{0f,.35f,1f})foreach(float pitch in new[]{-40f,0f,35f})for(int i=0;i<24;i++)
        {
            float time=10+i*.05f;var a=Gun(w);a.Pitch=pitch;a.AimYaw=i%3==0?12:0;a.LastShot=i%4==1?time-.05f:-100;
            if(i%5==2){a.ReloadAt=time-.4f;a.ReloadUntil=a.ReloadAt+GunRules.Get(w).Reload;}
            if(i%6==3){a.BlockAt=time-.03f;a.BlockUntil=a.BlockAt+Rules.BlockDuration;}
            if(i%7==4)a.StunUntil=time+.2f;
            CapturedClips.Sets[0][0].Sample(time,row);CapturedRig.Sample(row,0,p);CapturedRig.Sample(row,0,q);
            RangedPose.Apply(p,a,time,aim);Legacy(q,a,time,aim);n++;
            for(int b=0;b<CapturedRig.Bones.Length;b++){Quaternion x=p.Rotation[b],y=q.Rotation[b];worst=Math.Max(worst,Math.Min((x-y).Length(),(x+y).Length()));Check(Vector3.Distance(p.Position[b],q.Position[b])<1e-6f,"hold position "+b);}
        }
        Check(worst<1e-5f,"the hold differs from the committed one by "+worst+" (quaternion distance)");
        log.Add("first-person hold unchanged over "+n+" aims, shots, reloads, guards and stuns (quaternion distance "+worst.ToString("0.0e0")+")");
    }
    // Running forward lowers the gun: the hips and shoulders counter-twist, the body bobs and leans, and the arms swing
    // against the legs (the pistol both arms, the long guns ride the torso in both hands).
    private static void Travel(WeaponId w,List<string> log)
    {
        var d=new Driver();var a=Gun(w);a.Velocity=new V3(0,0,6.8f);float t=20,dt=1/60f;string state=null;
        var pelvis=new List<float>();var chest=new List<float>();var bob=new List<float>();var lhand=new List<float>();var lfoot=new List<float>();var rhand=new List<float>();var rfoot=new List<float>();var gunYaw=new List<float>();
        float lean=0,lowTip=float.MaxValue,gap=0,carryOff=0;int frames=0;
        for(int f=0;f<150;f++,t+=dt)
        {
            state=d.Step(a,t,dt);if(f<60)continue;var r=d.Motion.Rig;frames++;
            Vector3 pf=Vector3.Transform(MeleeMath.Z,r.ChainQuat(CapturedRig.Pelvis,-1)),cf=Vector3.Transform(MeleeMath.Z,r.ChainQuat(CapturedRig.Chest,-1));
            pelvis.Add((float)Math.Atan2(pf.X,pf.Z));chest.Add((float)Math.Atan2(cf.X,cf.Z));bob.Add(r.P[CapturedRig.Body].Y);lean+=r.E[CapturedRig.Body].X;
            lhand.Add(d.Local(CapturedRig.LHand).Z);lfoot.Add(d.Local(CapturedRig.LFoot).Z);rhand.Add(d.Local(CapturedRig.RHand).Z);rfoot.Add(d.Local(CapturedRig.RFoot).Z);
            Vector3 barrel=Vector3.Transform(MeleeMath.Z,r.ChainQuat(CapturedRig.RHand,-1));gunYaw.Add((float)Math.Atan2(barrel.X,barrel.Z));
            lowTip=Math.Min(lowTip,d.Muzzle(w).Y);if(w!=WeaponId.Pistol)gap=Math.Max(gap,d.SupportGap(w));
            if(w==WeaponId.Pistol){var held=MeleeMath.Inverse(r.ChainQuat(CapturedRig.Chest,-1))*r.ChainQuat(CapturedRig.RHand,-1);carryOff=Math.Max(carryOff,MeleeMath.AngleTo(held,RangedPose.CarryOrientation(false,0,0)));}
            for(int b=0;b<MeleeRig.N;b++)Check(MathUtil.Finite(r.Q[b].W)&&Math.Abs(r.Q[b].Length()-1)<1e-3f&&MathUtil.Finite(r.P[b].X),w+" finite joint "+b);
        }
        lean/=frames;float twist=Corr(pelvis,chest);
        Check(state==GunMotion.Carry,w+" running forward shows "+state);
        Check(d.Motion.AimWeight<.02f,w+" lowers the gun to the carry ("+d.Motion.AimWeight+")");
        Check(Range(pelvis)>.12f,w+" hips swing with the stride ("+Range(pelvis)+" rad)");
        Check(twist<-.3f,w+" shoulders counter-twist against the hips (correlation "+twist+")");
        Check(Range(bob)>.012f,w+" body bobs ("+Range(bob)+" m)");
        Check(lean>.12f,w+" leans into the run ("+lean+" rad)");
        Check(lowTip>.2f,w+" muzzle stays clear of the floor ("+lowTip+" m)");
        float leftSwing=Corr(lhand,lfoot),rightSwing=Corr(rhand,rfoot);
        if(w==WeaponId.Pistol)
        {
            Check(Range(lhand)>.25f&&leftSwing<-.4f,"pistol free arm counter-swings ("+Range(lhand)+" m, correlation "+leftSwing+")");
            Check(Range(rhand)>.12f&&rightSwing<-.3f,"pistol gun arm pumps ("+Range(rhand)+" m, correlation "+rightSwing+")");
            Check(carryOff<.45f,"pistol held at the low-ready carry in the hand (within "+carryOff+" rad)");
            log.Add("pistol run: hips "+Range(pelvis).ToString("0.00")+" rad, twist corr "+twist.ToString("0.00")+", bob "+(Range(bob)*100).ToString("0.0")+" cm, lean "+lean.ToString("0.00")+", arm swing "+Range(lhand).ToString("0.00")+"/"+Range(rhand).ToString("0.00")+" m (corr "+leftSwing.ToString("0.00")+"/"+rightSwing.ToString("0.00")+")");
        }
        else
        {
            // The native carry (RangedPose with the gun lowered) holds the support hand where it reaches: the same gap here.
            var rest=new CapturedRig.Pose();var row=new float[64];CapturedClips.Sets[0][0].Sample(0,row);CapturedRig.Sample(row,0,rest);var still=Gun(w);RangedPose.Apply(rest,still,0,0);
            float carryGap=Vector3.Distance(rest.WorldPosition[CapturedRig.LHand],rest.WorldPosition[CapturedRig.RHand]+Vector3.Transform(RangedPose.Support(w),rest.WorldRotation[CapturedRig.RHand]));
            Check(gap<carryGap+.015f,w+" support hand stays with the carried gun ("+gap+" m, native carry "+carryGap+" m)");
            Check(Range(gunYaw)>.04f,w+" carried gun rides the torso's twist ("+Range(gunYaw)+" rad)");
            log.Add(w+" run: hips "+Range(pelvis).ToString("0.00")+" rad, twist corr "+twist.ToString("0.00")+", bob "+(Range(bob)*100).ToString("0.0")+" cm, lean "+lean.ToString("0.00")+", support hand within "+(gap*100).ToString("0.0")+" cm of the grip (native carry "+(carryGap*100).ToString("0.0")+" cm), gun sway "+Range(gunYaw).ToString("0.00")+" rad, muzzle >= "+lowTip.ToString("0.00")+" m");
        }
    }
    // Strafing, backpedalling, standing, aiming and firing on the run keep the gun on the sight exactly as the native hold
    // aims it (GunRules.Direction), the support hand on the grip, while the hips and legs move under it.
    private static void Aimed(WeaponId w,List<string> log)
    {
        float worst=0,gapMax=0,hips=0;
        foreach(var (name,vel,aiming,pitch,aimYaw) in new[]{("strafe",new V3(-6.8f,0,0),false,0f,0f),("backpedal",new V3(0,0,-6.8f),false,25f,0f),("stand",new V3(0,0,0),false,-20f,8f),("ADS run",new V3(0,0,6.8f),true,10f,0f),("diagonal",new V3(-4.8f,0,-4.8f),false,0f,0f)})
        {
            var d=new Driver();var a=Gun(w);a.Velocity=vel;a.Aiming=aiming;a.Pitch=pitch;a.AimYaw=aimYaw;a.Yaw=0;float t=30,dt=1/60f;var pel=new List<float>();
            for(int f=0;f<120;f++,t+=dt)
            {
                d.Step(a,t,dt);if(f<50)continue;
                Vector3 barrel=CapturedRig.Mirror(Vector3.Transform(Vector3.UnitZ,d.WorldQ(CapturedRig.RHand)));V3 want=GunRules.Direction(a.AimYaw,a.Pitch);
                float off=(float)Math.Acos(MeleeMath.Clamp(Vector3.Dot(Vector3.Normalize(barrel),new Vector3(want.x,want.y,want.z)),-1,1));worst=Math.Max(worst,off);
                gapMax=Math.Max(gapMax,d.SupportGap(w));Vector3 pf=Vector3.Transform(MeleeMath.Z,d.Motion.Rig.ChainQuat(CapturedRig.Pelvis,-1));pel.Add((float)Math.Atan2(pf.X,pf.Z));
            }
            Check(d.Motion.AimWeight>.98f,w+" "+name+" keeps the gun up ("+d.Motion.AimWeight+")");
            if(vel.x!=0||vel.z!=0)hips=Math.Max(hips,Range(pel));
        }
        Check(worst<1.5f*D,w+" barrel leaves the sight by "+(worst/D)+" deg");
        Check(gapMax<.03f,w+" support hand leaves the grip by "+gapMax+" m");
        Check(hips>.05f,w+" hips still swing under the aimed torso ("+hips+" rad)");
        // A shot while running raises the lowered gun quickly; .33 s after it the barrel is back on the sight.
        {
            var d=new Driver();var a=Gun(w);a.Velocity=new V3(0,0,6.8f);float t=40,dt=1/60f;
            for(int f=0;f<90;f++,t+=dt)d.Step(a,t,dt);
            Check(d.Motion.AimWeight<.02f,w+" carries the gun before the shot");a.LastShot=t;float raised=-1;
            for(int f=0;f<20;f++,t+=dt){d.Step(a,t,dt);if(raised<0&&d.Motion.AimWeight>.9f)raised=t-a.LastShot;}
            Vector3 barrel=CapturedRig.Mirror(Vector3.Transform(Vector3.UnitZ,d.WorldQ(CapturedRig.RHand)));V3 want=GunRules.Direction(0,0);
            float off=(float)Math.Acos(MeleeMath.Clamp(Vector3.Dot(Vector3.Normalize(barrel),new Vector3(want.x,want.y,want.z)),-1,1));
            Check(raised>0&&raised<.2f,w+" raises the gun for a shot on the run in "+raised+" s");Check(off<2.5f*D,w+" fires on the sight while running ("+(off/D)+" deg)");
            log.Add(w+" aimed: barrel within "+(worst/D).ToString("0.00")+" deg of the sight strafing/backpedalling/standing/ADS, grip gap <= "+(gapMax*100).ToString("0.0")+" cm, hips "+hips.ToString("0.00")+" rad, shot on the run raises in "+raised.ToString("0.00")+" s");
        }
    }
    // The stride advances at the gait's cadence for the replicated speed (motion-quality cycleLength) and a planted foot
    // stays put on the floor while the body travels over it.
    private static void Stride(WeaponId w,List<string> log)
    {
        string line=w+" stride";
        foreach(float speed in new[]{2.8f,6.8f,9.18f})
        {
            var d=new Driver();var a=Gun(w);a.Velocity=new V3(0,0,speed);a.Sprinting=speed>7;float t=50,dt=1/60f,s0=0,slide=0,count=0;
            var planted=new Vector3[2];var held=new bool[2];
            for(int f=0;f<240;f++,t+=dt)
            {
                d.Step(a,t,dt);if(f==90)s0=d.Motion.Rig.Stride;if(f<90)continue;
                for(int i=0;i<2;i++)
                {
                    int foot=i==0?CapturedRig.LFoot:CapturedRig.RFoot;Vector3 at=d.World(foot);bool down=d.Local(foot).Y<MeleeGait.FLOOR+.002f;
                    if(down&&held[i]){slide=Math.Max(slide,Vector3.Distance(new Vector3(at.X,0,at.Z),new Vector3(planted[i].X,0,planted[i].Z)));}
                    else if(down){planted[i]=at;count++;}
                    held[i]=down;
                }
            }
            float cycles=(d.Motion.Rig.Stride-s0)/MeleeMath.TAU,want=Math.Min(speed/MeleeGait.CycleLength(speed),MeleeGait.MaxCadence)*150*dt;
            Check(Math.Abs(cycles-want)<.04f*want,w+" at "+speed+" m/s strides "+cycles+" cycles, expected "+want);
            Check(count>=4,w+" at "+speed+" m/s plants its feet ("+count+")");
            Check(slide<.02f,w+" at "+speed+" m/s planted foot slides "+slide+" m");
            line+=" "+speed+" m/s "+cycles.ToString("0.00")+"/"+want.ToString("0.00")+" cycles slide "+(slide*1000).ToString("0")+" mm;";
        }
        log.Add(line);
    }
    // One bout over every state at a fixed rate: starting, walking, running, sprinting, stopping, firing on the run, a
    // strafe, a reload, a guard, a roll, a juggle and a stun with its reaction. Events land on the 1/30 s grid.
    private static void Bout(Driver d,Actor a,float t0,float hz,Action<float> sample)
    {
        float dt=1/hz;int n=(int)Math.Round(7.5f*hz);
        for(int f=0;f<=n;f++)
        {
            float u=f/hz,t=t0+u;
            // The simulation's velocity, which reaches each target at Rules.Acceleration (35 m/s2).
            V3 goal=u<.5f?new V3(0,0,0):u<1.2f?new V3(0,0,2.8f):u<2f?new V3(0,0,6.8f):u<2.6f?new V3(0,0,9.18f):u<3f?new V3(0,0,0):u<3.8f?new V3(0,0,6.8f):u<4.6f?new V3(-6.8f,0,0):new V3(0,0,0);
            V3 change=goal-a.Velocity;float size=change.Length,most=Rules.Acceleration*dt;a.Velocity=size<=most?goal:a.Velocity+change*(most/size);
            a.Sprinting=u>=2f&&u<2.6f;
            if(Math.Abs(u-3.3f)<dt*.5f)a.LastShot=t;
            if(Math.Abs(u-4.8f)<dt*.5f){a.ReloadAt=t;a.ReloadUntil=t+GunRules.Get(a.Weapon).Reload;}
            if(Math.Abs(u-5.6f)<dt*.5f){a.BlockAt=t;a.BlockUntil=t+Rules.BlockDuration;}
            if(Math.Abs(u-6f)<dt*.5f)a.RollAt=t;
            if(Math.Abs(u-6.6f)<dt*.5f)a.JuggleUntil=t+.5f;
            if(Math.Abs(u-7.2f)<dt*.5f){a.StunUntil=t+.25f;a.ReactionAt=t;a.ReactionUntil=t+.25f;a.ReactionStrength=1;a.ReactionDir=new V3(0,0,1);}
            d.Step(a,t,f==0?dt:dt);sample(u);
        }
    }
    private static readonly int[] Upper={CapturedRig.Body,CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.LArm,
        CapturedRig.LElbow,CapturedRig.LHand,CapturedRig.RClav,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand};
    // At 30, 60, 120 and 240 Hz: no snap (a frame's step much longer than both of its neighbours; the allowance grows with
    // the frame time as smooth motion's own step changes do: 1.5 cm at 240 Hz, about 8 cm at 30 Hz; the authored fast
    // moves, the shot's 30 ms recoil rise, the roll's 2 pi spin and the juggle, are checked at 240 Hz only), no point faster than
    // 14 m/s outside the roll's spin and the juggle, and while the gun is up (firing on the run, the aimed strafe, the
    // standing reload) the chest, head and muzzle where the 240 Hz pilot has them (the gait's stride phase itself is
    // stateful and may settle differently per rate, as in the source).
    // No snaps at 30, 60, 120 or 240 Hz. At 240 Hz no frame's step may be much longer than both of its neighbours (1.5 cm;
    // the shot's recoil onset is exempt: RangedPose.Recoil, unchanged, kicks the gun within 30 ms by design);
    // at the lower rates every frame's step must be explained by the motion itself: no longer than the fastest the 240 Hz
    // pilot moved that point within .15 s of the frame (a threshold such as the run lowering or raising the gun may cross
    // a few frames apart at different rates; x1.25 + 1 cm), so a reset, a dropped blend or a
    // state switch shows while the gait's own fast moves (the brace raise, the recoil, the roll) do not. No body point moves
    // faster than 14 m/s outside those authored fast moves (Authored; the muzzle, up to 1.2 m out, swings faster). While the gun is up (firing on the run, the aimed strafe,
    // the standing reload) the chest, head and muzzle stay where the 240 Hz pilot has them (the gait's stride phase itself
    // is stateful and may settle differently per rate, as in the source).
    // The bout's authored fast moves: the recoil onset (30 ms), the guard's 70 ms swing to the barrier, the roll's spin and the juggle.
    private static bool Authored(float u)=>u>=3.3f&&u<3.42f||u>=5.6f&&u<5.72f||u>=6f&&u<7.2f;
    private static void Continuity(List<string> log)
    {
        int[] aimed={CapturedRig.Chest,CapturedRig.Head,-1};
        foreach(var w in Guns)
        {
            int n=Upper.Length+1;var reference=new Dictionary<int,Vector3[]>();var speed240=new List<float[]>();float spread=0,spreadAt=-1;string report="";
            foreach(float hz in new[]{240f,120f,60f,30f})
            {
                var d=new Driver();var a=Gun(w);var prev=new Vector3[n];var step=new float[n];var before=new float[n];
                float spike=0,spikeAt=-1,fastest=0,fastestAt=-1,worst=0,worstAt=-1;int frame=0,worstPoint=-1,fastestPoint=-1;
                Bout(d,a,60,hz,u=>
                {
                    var r=d.Motion.Rig;var points=new Vector3[n];var speeds=new float[n];
                    for(int k=0;k<n;k++)points[k]=k<Upper.Length?r.ChainPoint(Upper[k],-1):r.ChainPoint(CapturedRig.RHand,-1)+Vector3.Transform(new Vector3(0,0,Tip(w)),r.ChainQuat(CapturedRig.RHand,-1));
                    for(int k=0;k<n;k++)
                    {
                        float e=frame>0?Vector3.Distance(points[k],prev[k]):0;speeds[k]=e*hz;if(!Authored(u)&&k<Upper.Length&&e*hz>fastest){fastest=e*hz;fastestAt=u;fastestPoint=k;}
                        if(hz==240&&frame>2&&(u<3.3f||u>=3.42f)){float jump=step[k]-Math.Max(before[k],e);if(jump>spike){spike=jump;spikeAt=u;}}
                        if(hz<240&&frame>0)
                        {
                            int hi=Math.Min(speed240.Count-1,(int)Math.Round((u+.15f)*240)),lo=Math.Max(1,(int)Math.Round((u-1/hz-.15f)*240));float most=0;
                            for(int g=lo;g<=hi;g++)most=Math.Max(most,speed240[g][k]);
                            float ratio=e/(most/hz*1.25f+.01f);if(ratio>worst){worst=ratio;worstAt=u;worstPoint=k;}
                        }
                        before[k]=step[k];step[k]=e;prev[k]=points[k];
                    }
                    if(hz==240)speed240.Add(speeds);
                    int key=(int)Math.Round(u*30);bool up=u>=3.55f&&u<3.66f||u>=4.3f&&u<4.6f||u>=5f&&u<5.5f;
                    if(Math.Abs(u*30-key)<1e-3f&&up)
                    {
                        if(hz==240)reference[key]=points;
                        else if(reference.TryGetValue(key,out var want))foreach(int b in aimed){int k=b<0?n-1:Array.IndexOf(Upper,b);float gap=Vector3.Distance(points[k],want[k]);if(gap>spread){spread=gap;spreadAt=u;}}
                    }
                    frame++;
                });
                if(hz==240){Check(spike<.015f,w+" snaps "+(spike*100).ToString("0.0")+" cm at "+spikeAt.ToString("0.00")+" s (240 Hz)");report+=" 240 Hz largest snap "+(spike*1000).ToString("0.0")+" mm;";}
                else{Check(worst<1,w+" at "+hz+" Hz jumps point "+worstPoint+" at "+worstAt.ToString("0.00")+" s ("+worst.ToString("0.00")+" of the motion's own step)");report+=" "+hz+" Hz "+worst.ToString("0.00")+";";}
                Check(fastest<14,w+" at "+hz+" Hz moves point "+fastestPoint+" at "+fastest+" m/s at "+fastestAt.ToString("0.00")+" s");
            }
            Check(spread<.06f,w+" aimed chest/head/muzzle at 30-120 Hz differ from 240 Hz by "+(spread*100).ToString("0.0")+" cm at "+spreadAt.ToString("0.00")+" s");
            log.Add(w+" continuity:"+report+" (lower rates: largest step over the motion's own); aimed chest, head and muzzle within "+(spread*100).ToString("0.0")+" cm of the 240 Hz pose at 30-120 Hz");
        }
    }
    private static void Allocation(List<string> log)
    {
        foreach(var w in Guns)
        {
            // The exact sequence runs once on a throwaway driver first (a fresh bout, then a new pilot after a 7.5 s gap), as the
            // gestures below skip their first: a one-time runtime cost on a path's first run is not per-frame allocation.
            Action<float> none=_=>{};var warm=new Driver();Bout(warm,Gun(w),80,60,none);Bout(warm,Gun(w),95,60,none);
            var d=new Driver();var a=Gun(w);Bout(d,a,80,60,none);var b=Gun(w);
            long mark=0;int from=-1,frame=0;Action<float> probe=u=>{long now=GC.GetAllocatedBytesForCurrentThread();if(from<0&&now>mark)from=frame;mark=now;frame++;};
            long before=GC.GetAllocatedBytesForCurrentThread();mark=before;Bout(d,b,95,60,probe);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,w+" posing allocates "+bytes+" bytes over a bout from frame "+from+" ("+(from/60f).ToString("0.00")+" s)");
            // The support-hand gestures (frag throw, Orbital Mark tap) and the rocket's recoil ride the gait without allocating.
            string[] abilities={"frag-grenade","frag-grenade","orbital-mark","rocket"};
            for(int i=0;i<abilities.Length;i++)
            {
                float t=118+i*2;b.AbilityId=abilities[i];b.AbilityAt=t;b.AbilityRelease=t+.3f;b.Velocity=new V3(0,0,6.8f);
                long start=GC.GetAllocatedBytesForCurrentThread();int first=-1;
                for(int f=0;f<120;f++){long s0=GC.GetAllocatedBytesForCurrentThread();d.Step(b,t+f/60f,1/60f);if(first<0&&GC.GetAllocatedBytesForCurrentThread()>s0)first=f;}
                if(i>0)Check(GC.GetAllocatedBytesForCurrentThread()-start==0,w+" "+abilities[i]+" allocates "+(GC.GetAllocatedBytesForCurrentThread()-start)+" bytes from frame "+first);
            }
        }
        log.Add("no allocation over a 7.5 s bout of every state, each gun, nor through a frag throw, an Orbital Mark tap or a rocket");
    }
    // NativeSirenDrift over a still stance: the lean into the drift, trailing limbs, the air tuck, the aim pitch, the flinch,
    // the guard and the move's own arms, without allocation.
    private static void Siren(List<string> log)
    {
        var drift=new NativeSirenDrift();var p=new CapturedRig.Pose();var a=new Actor{Id=40,SourceSpec="siren",Weapon=WeaponId.Sword,Grounded=true,Hp=100};float t=0,dt=1/60f;
        void Run(int frames){for(int f=0;f<frames;f++,t+=dt){p.Reset();drift.Apply(p,a,t);}}
        Vector3 E(int bone)=>MeleeMath.ToEuler(p.Rotation[bone]);
        Run(30);Check(E(CapturedRig.Body).Length()<1e-5f&&E(CapturedRig.LHip).Length()<1e-5f,"a still Siren keeps her stance");
        a.Velocity=new V3(0,0,6.8f);Run(120);
        Check(Math.Abs(E(CapturedRig.Body).X-.17f)<.005f&&Math.Abs(E(CapturedRig.LArm).X-.5f)<.01f&&Math.Abs(E(CapturedRig.LHip).X-.70f)<.01f&&Math.Abs(E(CapturedRig.RKnee).X-.25f)<.01f,"the Siren leans into a forward drift, arms and legs trailing");
        a.Velocity=new V3(6.8f,0,0);Run(120);Check(Math.Abs(E(CapturedRig.Body).Z+.18f)<.01f&&Math.Abs(E(CapturedRig.Body).X)<.01f,"a drift to her right banks her (body z -.18)");
        a.Velocity=new V3(0,0,0);a.Grounded=false;Run(120);Check(Math.Abs(E(CapturedRig.LKnee).X-.35f)<.01f&&Math.Abs(p.Position[CapturedRig.Body].Y-(CapturedRig.Bones[0].Position.Y-.06f))<1e-5f,"airborne knees tuck and the hover drops");
        a.Grounded=true;a.SourceRanged=true;a.Pitch=20;Run(60);Check(Math.Abs(E(CapturedRig.Chest).X+20*D*.28f)<.01f&&Math.Abs(E(CapturedRig.Head).X+20*D*.22f)<.01f,"ranged aim pitch follows into the chest and head");
        a.Pitch=0;a.Velocity=new V3(0,0,6.8f);Run(120);a.SourceAction="swipe";a.SourceAt=t;a.SourceUntil=t+.4f;Run(10);
        Check(Math.Abs(E(CapturedRig.LArm).X)<.02f&&E(CapturedRig.Body).X>.15f,"a move keeps its own arms while the body still leans");
        Run(30);a.SourceAction=null;a.BlockAt=t;a.BlockUntil=t+.3f;Run(5);Check(Math.Abs(E(CapturedRig.RArm).X+1.35f)<1e-3f&&Math.Abs(E(CapturedRig.LElbow).X+1.45f)<1e-3f,"the guard crosses her claws");
        a.BlockUntil=0;a.ReactionAt=t;Run(10);Check(E(CapturedRig.Chest).X<-.15f,"a hit snaps her back");
        long before=GC.GetAllocatedBytesForCurrentThread();Run(240);Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"the Siren drift allocates");
        log.Add("Siren: leans .17 rad into a full drift, banks -.18 strafing, tucks airborne, follows the aim, flinches and guards without allocation");
    }
}
