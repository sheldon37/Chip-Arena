using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Port of motion-quality.ts locomotion / groundPass and stances.ts for the captured melee weapons, the magic gait, the
    // DEV fighting styles and the gun gait (presentation only).
    // Planted feet are integrated against the root's ground travel in group space, so they stay still on the floor while
    // the hips travel over them; swing feet follow a cubic path to a speed-matched landing target. The ground pass, after
    // every action layer, turns the pelvis toward the travel (counter-rotating the chest) and re-solves the legs so the
    // ankles stay on their targets while the body drops, leans or twists. First-person view-model paths are not ported:
    // the native first-person view keeps its own poses.
    public sealed class MeleeStance
    {
        public float LeadX,LeadZ,LeadToe,RearX,RearZ,RearToe,Crouch,PelvisYaw,KneeSoft,Lean;public bool LeadLeft=true;
        private static MeleeStance S(float lx,float lz,float lt,float rx,float rz,float rt,float crouch,float yaw,float lean,float knee=0)
            =>new MeleeStance{LeadX=lx,LeadZ=lz,LeadToe=lt,RearX=rx,RearZ=rz,RearToe=rt,Crouch=crouch,PelvisYaw=yaw,Lean=lean,KneeSoft=knee};
        public static readonly MeleeStance Melee=S(.16f,.09f,.17f,-.17f,-.08f,.44f,-.03f,-.12f,.03f),
            Daggers=S(.19f,.13f,.2f,-.2f,-.12f,.5f,-.07f,-.15f,.03f,.1f),Greatsword=S(.19f,.12f,.2f,-.19f,-.11f,.5f,-.045f,-.14f,.05f),
            Neutral=S(.13f,0,0,-.13f,0,0,0,0,0),
            // DEV fighting styles: bladed to match their guards (Mishima half-bladed and compact, karate longer and lower).
            Emperor=S(.17f,.08f,-.3f,-.13f,-.23f,.62f,0,0,0),Karate=S(.17f,.13f,-.15f,-.15f,-.21f,.55f,0,0,0);
        // stances.ts MAGIC: planted stagger with the staff side back (the magic gait, MagicMotion).
        public static readonly MeleeStance Magic=S(.15f,.08f,.14f,-.16f,-.07f,.36f,-.02f,-.08f,.02f);
        // stances.ts guns (the gun gait, GunMotion): the pistol at shoulder width with a slight lean; the rifle bladed behind the
        // gun (its pelvis turn scaled by the aim weight, MeleeGait.PelvisYawScale, the chest staying on aim); the machine gun
        // wide and braced, knees soft, leaning into the weight.
        public static readonly MeleeStance Pistol=S(.15f,.04f,.12f,-.15f,-.03f,.2f,-.015f,-.05f,.035f),Rifle=S(.09f,.12f,-.3f,-.10f,-.10f,.85f,-.025f,-.55f,.04f),
            MachineGun=S(.19f,.10f,.2f,-.19f,-.04f,.4f,-.03f,-.1f,.08f,.15f);
        public static bool MagicSpec(string spec)=>spec=="fire"||spec=="water"||spec=="lightning"||spec=="darkness"||spec=="leet";
        public static MeleeStance For(string spec)=>spec=="daggers"?Daggers:spec=="greatsword"?Greatsword:spec=="sword"||spec=="shield"?Melee:spec=="emperor"?Emperor:spec=="ansatsuken"?Karate:MagicSpec(spec)?Magic:
            spec=="pistol"?Pistol:spec=="rifle"?Rifle:spec=="machinegun"?MachineGun:Neutral;
        // Idle stance ankle target (group space) for one foot.
        public void Foot(bool left,out float x,out float z,out float yaw){bool lead=left==LeadLeft;x=lead?LeadX:RearX;z=lead?LeadZ:RearZ;yaw=(left?1:-1)*(lead?LeadToe:RearToe);}
    }
    public sealed class MeleeGait
    {
        public const float FLOOR=-.795f,REACH=.485f,LAND_REACH=.47f,REWIND=.1f,TELEPORT=1.2f,ROOT_VEL_RATE=20;
        public const float CycleBase=.62f,CyclePerSpeed=.22f,CycleMax=2.1f,RunSpeed=6.8f,DashCycle=2.6f,DashSpeed=15,Stance=.42f,StanceBias=.05f,ContactMin=.18f,ContactMax=.62f,
            SettleCadence=2.3f,SettleLift=.05f,RunLean=.17f,RunCrouch=.025f,BlendSpeed=13,TurnDeadZone=.7f,TravelYawMax=.7f,LegYawOmega=10,MaxCadence=3.7f;
        public static bool TimeJumped(float prev,float time)=>time<prev-REWIND||time-prev>.35f;
        public static float CycleLength(float speed)=>speed<=RunSpeed?MeleeMath.Clamp(CycleBase+CyclePerSpeed*speed,CycleBase,CycleMax):MeleeMath.Lerp(CycleMax,DashCycle,MeleeMath.Clamp((speed-RunSpeed)/(DashSpeed-RunSpeed),0,1));
        public sealed class Foot{public float x,y,z,yaw,pitch,toe,fx,fy,fz,fyaw,uLift,lx,lz,lyaw,sx,sy,sz,syaw,spitch;public bool planted=true;
            public Foot(float x,float z,float yaw){Seat(x,z,yaw);}
            // A fresh foot planted at a stance target (every field as the constructor leaves it).
            public void Seat(float x,float z,float yaw){this.x=fx=lx=sx=x;this.z=fz=lz=sz=z;this.yaw=fyaw=lyaw=syaw=yaw;y=fy=sy=FLOOR;pitch=toe=uLift=spitch=0;planted=true;}}
        // Per-rig travel state (motion-quality Travel).
        private bool has;private string id;private float time,raw,px,pz,still,rvx,rvz,clock,forward,strafe,lean,bank,vx,vz,mvx,mvz,yaw,hipYaw,legYaw,legYawV,airAt,airVy,landAt,landDepth,dashLean,phaseOffset,phase;
        private int airLead;private bool settling,onGround,solved,fresh;private Foot[] feet;private readonly float[] armPrev={float.NaN,float.NaN},hand=new float[4];
        private readonly Quaternion[] legQ=new Quaternion[6];private readonly Vector3[] hips={new Vector3(.13f,-.32f,0),new Vector3(-.13f,-.32f,0)};
        private Vector3 solvedBodyP;private Quaternion solvedBodyQ,solvedPelvisQ;
        public bool Started=>has;
        // The share of the stance's pelvis turn the hips take (motion-quality: the rifle's is scaled by its aim weight); 1 for
        // every other spec.
        public float PelvisYawScale=1;
        // A layer that spins the upper body over these legs (MeleeKitMotion's Whirlwind, MeleeKitMotion.Stance): Widen (m)
        // sets each foot's stance that much further out (the running track .4 of it), and Shuffle (steps a second per foot)
        // keeps the feet stepping on their stance spots while the pilot stands, so planted feet keep re-planting under the
        // spin and never slide. 0, the default, leaves the gait exactly as it was.
        public float Widen,Shuffle;
        // The last Locomotion's backpedal share (0 forward or strafing, 1 running backward), read by the melee torso.
        public float Back{get;private set;}
        // Both feet re-seated on the stance (spawn, reset, teleport or a near-instant flip), reusing the rig's two feet.
        private Foot[] IdleFeet(string spec)
        {
            var st=MeleeStance.For(spec);st.Foot(true,out float lx,out float lz,out float ly);st.Foot(false,out float rx,out float rz,out float ry);
            if(feet==null)return new[]{new Foot(lx,lz,ly),new Foot(rx,rz,ry)};
            feet[0].Seat(lx,lz,ly);feet[1].Seat(rx,rz,ry);return feet;
        }
        // Per-frame scratch (no allocation in the frame loop).
        private readonly Foot[] idleFeet={new Foot(0,0,0),new Foot(0,0,0)};private readonly float[] k4=new float[4],k5=new float[5];
        private static void IdleInto(string spec,Foot[] o){var st=MeleeStance.For(spec);st.Foot(true,out o[0].x,out o[0].z,out o[0].yaw);st.Foot(false,out o[1].x,out o[1].z,out o[1].yaw);}
        private float[] K(float a,float b,float c,float d){k4[0]=a;k4[1]=b;k4[2]=c;k4[3]=d;return k4;}
        private float[] K(float a,float b,float c,float d,float e){k5[0]=a;k5[1]=b;k5[2]=c;k5[3]=d;k5[4]=e;return k5;}
        private static readonly int[] LegBones={CapturedRig.LHip,CapturedRig.LKnee,CapturedRig.LFoot,CapturedRig.RHip,CapturedRig.RKnee,CapturedRig.RFoot};
        // gaitFoot: this sample's gait target for a foot in group space (what the ground pass holds).
        public bool GaitFoot(bool left,out Vector3 target,out float yaw,out float pitch,out float toe)
        {
            target=default;yaw=pitch=toe=0;if(!has)return false;var f=feet[left?0:1];target=new Vector3(f.sx,f.sy,f.sz);yaw=f.syaw;pitch=f.spitch;toe=f.toe;return true;
        }
        /// <summary>The gait has this foot planted on the floor (read-only; the presentation checks measure slip on it).</summary>
        public bool Planted(bool left)=>has&&feet[left?0:1].planted;
        private static void SwingArm(MeleeRig r,MeleeGait s,bool left,float ph,float run,float air,float amp,float back,float push,float reach,float dt)
        {
            int arm=left?CapturedRig.LArm:CapturedRig.RArm,elbow=left?CapturedRig.LElbow:CapturedRig.RElbow,hand=left?CapturedRig.LHand:CapturedRig.RHand,i=left?0:1;float sign=left?1:-1;
            float u=MeleeMath.Frac(ph/MeleeMath.TAU+(left?0:.5f)-.06f),weight=MeleeMath.Clamp(run,0,1);
            float shoulder=MeleeMath.PoseCurve(ArmKeys,ArmShoulder,u)*amp,bend=-1.3f+(MeleeMath.PoseCurve(ArmKeys,ArmBend,u)+1.3f)-back*.3f;
            float x=MeleeMath.Lerp(left?-.10f:-.14f,shoulder,weight)-push*.55f-reach*.2f;
            r.SetE(arm,x,0,sign*(.10f+air*.18f+reach*.2f));r.SetE(elbow,MeleeMath.Lerp(-.22f,bend,weight)-push*.35f,0,0);
            // Hands trail the arm: a critically damped spring driven by the shoulder's angular velocity.
            float prev=s.armPrev[i],vel=dt>0&&!float.IsNaN(prev)?(x-prev)/dt:0;s.armPrev[i]=x;float goal=MeleeMath.Clamp(vel*.035f,-.3f,.3f),w=18,h=s.hand[i*2],hv=s.hand[i*2+1];
            hv=(hv-(h-goal)*w*w*dt)/(1+2*w*dt+w*w*dt*dt);h+=hv*dt;s.hand[i*2]=h;s.hand[i*2+1]=hv;
            r.SetE(hand,h*weight+(float)Math.Sin(ph+(left?0:MeleeMath.PI)-.65f)*.04f*weight,0,sign*.05f);
        }
        private static readonly float[] ArmKeys={0,.17f,.38f,.55f,.73f,.90f,1},ArmShoulder={.66f,.18f,-.86f,-.55f,.18f,.63f,.66f},ArmBend={-1.05f,-1.25f,-1.42f,-1.55f,-1.16f,-1.04f,-1.05f};
        private static readonly float[] RollKeys={0,.22f,.55f,1},PitchKeys={0,.3f,.72f,1},SprintKeys={0,.12f,.3f,.56f,.84f};
        private static float Landing(float t)=>t<0||t>.8f?0:(float)(Math.Exp(-8.25*t)*Math.Sin(7.27*t)/.2915);
        // Distance-driven locomotion under the action layers. `planted` (commitment, hitstun, rolls) freezes the stride.
        public void Locomotion(MeleeRig r,MeleeView p,float time,float dt,bool planted)
        {
            float frameDt=Math.Max(0,dt);dt=MeleeMath.Clamp(dt,0,.05f);float yawNow=r.GroupYaw;Vector3 gp=r.GroupPosition;
            if(has&&id!=p.Id){r.Motion=0;r.Stride=0;r.GroundWeight=1;r.Land=0;}
            if(!has||TimeJumped(this.time,time)||id!=p.Id)
            {
                has=true;this.time=time;raw=time;px=gp.X;pz=gp.Z;still=1;rvx=p.Vel.X;rvz=p.Vel.Z;clock=-1;forward=1;strafe=lean=bank=vx=vz=mvx=mvz=0;yaw=yawNow;feet=IdleFeet(p.Spec);
                hipYaw=legYaw=legYawV=0;settling=false;airAt=time;airVy=0;airLead=0;phaseOffset=0;onGround=p.Grounded&&!(p.DashTime>0);landAt=-1e9f;landDepth=0;dashLean=0;phase=MeleeMath.HashPhase(p.Id);
                armPrev[0]=armPrev[1]=float.NaN;Array.Clear(hand,0,4);hips[0]=new Vector3(.13f,-.32f,0);hips[1]=new Vector3(-.13f,-.32f,0);solved=false;id=p.Id;fresh=true;r.WasGrounded=p.Grounded;
            }
            // A small backward step holds the stateful clock (REWIND); authoritative phases below still read `time`.
            float now=Math.Max(time,this.time),dNow=now-this.time;this.time=now;raw=time;
            float dxw=gp.X-px,dzw=gp.Z-pz,moved=(float)Math.Sqrt(dxw*dxw+dzw*dzw);bool jump=moved>TELEPORT;px=gp.X;pz=gp.Z;
            float dyaw=MeleeMath.Wrap(yawNow-yaw);yaw=yawNow;
            if(Math.Abs(dyaw)>1.6f||jump)feet=IdleFeet(p.Spec);
            else if(dyaw!=0)
            {
                legYaw=MeleeMath.Clamp(legYaw-dyaw,-TurnDeadZone-.1f,TurnDeadZone+.1f);float c=(float)Math.Cos(dyaw),sn=(float)Math.Sin(dyaw);
                foreach(var f in feet){float x=f.x,z=f.z;f.x=x*c-z*sn;f.z=x*sn+z*c;x=f.fx;z=f.fz;f.fx=x*c-z*sn;f.fz=x*sn+z*c;f.yaw-=dyaw;f.fyaw-=dyaw;}
            }
            still=moved>1e-6f?0:still+dt;bool live=!jump&&still<.3f;
            if(live){float eT=Hyp(dxw-rvx*dNow,dzw-rvz*dNow),eR=Hyp(dxw-rvx*frameDt,dzw-rvz*frameDt);if(Math.Max(eT,eR)>.002f&&(eT<.5f*eR||eR<.5f*eT))clock+=((eT<eR?1:-1)-clock)*.25f;}
            float span=live&&clock>0?dNow:frameDt;
            if(!live){rvx=p.Vel.X;rvz=p.Vel.Z;}else if(span>0){float k=1-(float)Math.Exp(-Math.Min(span,.05f)*ROOT_VEL_RATE);rvx+=(dxw/span-rvx)*k;rvz+=(dzw/span-rvz)*k;}
            bool rooted=live&&!planted;float wx=rooted?rvx:p.Vel.X,wz=rooted?rvz:p.Vel.Z,cy=(float)Math.Cos(yawNow),sy=(float)Math.Sin(yawNow),lvx=wx*cy-wz*sy,lvz=wx*sy+wz*cy,speed=Hyp(lvx,lvz);
            float gdx=rooted?dxw*cy-dzw*sy:lvx*dt,gdz=rooted?dxw*sy+dzw*cy:lvz*dt;
            float blend=1-(float)Math.Exp(-dt*BlendSpeed),goalM=p.Grounded?MeleeMath.Clamp(speed/6.8f,0,1.2f):0;r.Motion+=(goalM-r.Motion)*blend;
            if(speed>.1f){forward+=(lvz/speed-forward)*blend;strafe+=(lvx/speed-strafe)*blend;}
            float dA=rooted?Math.Min(span,.05f):dt;
            if(dA>0){float ax=!planted?(lvx-vx)/dA:0,az=!planted?(lvz-vz)/dA:0,k9=1-(float)Math.Exp(-dA*9);vx=lvx;vz=lvz;lean+=(MeleeMath.Clamp(az*.008f,-.1f,.1f)-lean)*k9;bank+=(MeleeMath.Clamp(-ax*.006f,-.12f,.12f)-bank)*k9;}
            if(speed>.5f&&!planted){mvx=lvx;mvz=lvz;}else{float k=(float)Math.Exp(-dt*6);mvx*=k;mvz*=k;}
            bool dashing=p.DashTime>0;float dash=dashing?.15f:0;dashLean+=(dash-dashLean)*(1-(float)Math.Exp(-dt*(dash>dashLean?18:10)));
            bool ground=p.Grounded&&!dashing;
            if(!ground&&onGround){airAt=now;airVy=0;airLead=!feet[0].planted?0:!feet[1].planted?1:MeleeMath.Frac(r.Stride/MeleeMath.TAU)<.5f?1:0;}
            if(!p.Grounded)airVy=Math.Min(airVy,p.Vel.Y);
            bool landing=ground&&!onGround;onGround=ground;
            if(p.Grounded&&!r.WasGrounded){float airborne=now-airAt;landAt=now;landDepth=airVy<-2&&airborne>.08f?MeleeMath.Clamp(-airVy*.012f,.03f,.12f):0;airVy=0;}
            r.WasGrounded=p.Grounded;r.GroundWeight+=((ground?1:0)-r.GroundWeight)*(1-(float)Math.Exp(-dt*(ground?(r.WasGrounded&&now-landAt>.05f?9:22):16)));float air=1-r.GroundWeight;
            float dip=landDepth*Landing(now-landAt);r.Land=dip;
            float run=r.Motion,weight=Math.Min(run,1),back=MeleeMath.Ease((-forward-.3f)/.4f)*MeleeMath.Clamp(speed/1.5f,0,1),side=Math.Abs(strafe)*MeleeMath.Clamp(speed/1.5f,0,1);
            Back=back;float D=CycleLength(speed)*MeleeMath.Lerp(1,.8f,back),S=Stance*MeleeMath.Lerp(1,.7f,back),contact=MeleeMath.Clamp(S/D,ContactMin,ContactMax);
            float rate=ground&&speed>.05f?Math.Min(speed/D,MaxCadence):0;if(settling&&ground)rate=Math.Max(rate,SettleCadence);if(Shuffle>0&&ground)rate=Math.Max(rate,Shuffle);if(planted)rate=0;
            float prevStride=r.Stride;r.Stride+=MeleeMath.TAU*rate*dt;phaseOffset*=(float)Math.Exp(-dt*10);
            float sprint=MeleeMath.Ease((run-.32f)/.65f)*(1-back),moving=MeleeMath.Clamp(speed/2,0,1),heel=.1f*weight,toeOff=(.22f+.3f*sprint)*moving;
            if(landing)
            {
                int lead=airLead;float old=r.Stride;r.Stride=(float)Math.Floor(r.Stride/MeleeMath.TAU)*MeleeMath.TAU+(lead!=0?MeleeMath.PI:0)+.001f;prevStride=r.Stride;phaseOffset=MeleeMath.Wrap(phaseOffset+old-r.Stride);
                for(int i=0;i<2;i++){var f=feet[i];f.x=f.fx=f.sx;f.z=f.fz=f.sz;f.y=f.fy=Math.Max(FLOOR,f.sy);f.yaw=f.fyaw=f.syaw;f.pitch=0;f.toe=0;f.planted=i==lead||!(speed>=.25f&&.5f>=contact);f.uLift=.5f;if(f.planted)f.y=FLOOR;}
            }
            // Hips turn toward the travel direction (backward travel keeps them facing the aim).
            float hipTravel=speed>1e-3f?(float)Math.Atan2(lvx,lvz):0;if(Math.Abs(hipTravel)>1.9f)hipTravel=MeleeMath.Wrap(hipTravel-MeleeMath.PI);hipTravel=MeleeMath.Clamp(hipTravel,-TravelYawMax,TravelYawMax)*MeleeMath.Clamp((speed-.1f)/.6f,0,1)*moving;
            var idle=idleFeet;IdleInto(p.Spec,idle);if(Widen!=0){idle[0].x+=Widen;idle[1].x-=Widen;}float mean=(MeleeMath.Wrap(feet[0].yaw-idle[0].yaw)+MeleeMath.Wrap(feet[1].yaw-idle[1].yaw))/2,turn=MeleeMath.Clamp(mean,-TurnDeadZone,TurnDeadZone);
            var stance=MeleeStance.For(p.Spec);hipYaw=MeleeMath.Lerp(turn+stance.PelvisYaw*PelvisYawScale,hipTravel,moving);
            bool settle=false;
            if(ground)for(int i=0;i<2;i++)
            {
                var f=feet[i];float sx=i==1?-1:1,u=MeleeMath.Frac(r.Stride/MeleeMath.TAU+(i==1?.5f:0)),pu=MeleeMath.Frac(prevStride/MeleeMath.TAU+(i==1?.5f:0));bool wrapped=r.Stride>prevStride&&u<pu-.5f;
                // Landing target: the stance or the running track turned with the hips, plus half a stance along the velocity.
                float ch=(float)Math.Cos(hipTravel),shy=(float)Math.Sin(hipTravel),nx=sx*(MeleeMath.Lerp(.125f,.1f,sprint)+.4f*Widen),land=rate>1e-3f&&speed>1e-3f?(speed*contact/(2*rate)-StanceBias*sprint)/speed:0;
                f.lx=MeleeMath.Lerp(idle[i].x,nx*ch,moving)+lvx*land;f.lz=MeleeMath.Lerp(idle[i].z,-nx*shy,moving)+lvz*land;
                {var h=hips[i];float reach=(float)Math.Sqrt(LAND_REACH*LAND_REACH-.44f*.44f),ex=f.lx-h.X,ez=f.lz-h.Z,e=Hyp(ex,ez);if(e>reach){f.lx=h.X+ex*reach/e;f.lz=h.Z+ez*reach/e;}}
                f.lyaw=MeleeMath.Lerp(idle[i].yaw,hipTravel+sx*.05f,moving);
                float c=(float)Math.Cos(turn),sn=(float)Math.Sin(turn),ix=idle[i].x*c+idle[i].z*sn,iz=-idle[i].x*sn+idle[i].z*c;
                bool settled=f.planted&&Hyp(f.x-ix,f.z-iz)<.05f&&Math.Abs(MeleeMath.Wrap(f.yaw-idle[i].yaw))<TurnDeadZone;
                if(f.planted)
                {
                    f.x-=gdx;f.z-=gdz;
                    float hx=sx*.13f*(float)Math.Cos(legYaw),hz=-sx*.13f*(float)Math.Sin(legYaw);bool over=Hyp(f.x-hx,f.z-hz)>.4f;
                    if(!planted&&((u>=contact&&(speed>=.25f||!settled||Shuffle>0))||over)){f.planted=false;f.fx=f.x;f.fy=f.y;f.fz=f.z;f.fyaw=f.yaw;f.uLift=Math.Min(pu<contact&&u>=contact&&!wrapped?contact:u,.97f);}
                    else{float us=u<contact?u/contact:1,roll=MeleeMath.PoseCurve(RollKeys,K(-heel,0,0,toeOff),us)*MeleeMath.Clamp(speed/1.5f,0,1);f.pitch=roll;f.y=FLOOR+.09f*(float)Math.Sin(Math.Max(0,roll));f.toe=-Math.Max(0,roll)*.85f;}
                }
                if(!f.planted)
                {
                    if(planted)
                    {
                        // Commitment or hitstun: the swing foot comes down within ~60 ms, with a short catch-step along the travel.
                        float k=1-(float)Math.Exp(-dt*45),mv=Hyp(mvx,mvz),catchStep=mv>.5f?Math.Min(.12f,mv*.018f)/mv:0;
                        f.x+=(f.fx+mvx*catchStep-f.x)*k;f.z+=(f.fz+mvz*catchStep-f.z)*k;f.y+=(FLOOR-f.y)*k;f.pitch*=1-k;f.toe*=1-k;
                        if(f.y<FLOOR+.004f){f.y=FLOOR;f.planted=true;}
                    }
                    else
                    {
                        float sw=wrapped?1:MeleeMath.Clamp((u-f.uLift)/Math.Max(.03f,1-f.uLift),0,1),dur=rate>1e-3f?(1-f.uLift)/rate:.2f;
                        float k0=.3f*(1-back*.6f),k1=.7f;f.x=MeleeMath.Hermite(f.fx,f.lx,-lvx*dur*k0,-lvx*dur*k1,sw);f.z=MeleeMath.Hermite(f.fz,f.lz,-lvz*dur*k0,-lvz*dur*k1,sw);
                        // Heel fold behind the hip, then knee drive and reach (sprint); a low arc when walking or stepping in place.
                        float fold=MeleeMath.Lerp(1,.45f,side),start=Math.Max(0,f.fy-FLOOR),sprintH=sw<.84f?MeleeMath.PoseCurve(SprintKeys,K(start,start+.09f*fold,.24f*fold,.19f*fold,.07f),sw):.07f*(float)Math.Pow((1-sw)/.16f,1.3);
                        float walkH=start*(1-sw)*(1-sw)+4*sw*(1-sw)*MeleeMath.Lerp(SettleLift,.085f*MeleeMath.Lerp(1,.6f,back),moving);
                        f.y=FLOOR+Math.Max(MeleeMath.Lerp(walkH,sprintH,sprint),.012f*MeleeMath.Clamp((1-sw)*50,0,1)*MeleeMath.Clamp(speed/1.5f,0,1));f.yaw=f.fyaw+MeleeMath.Wrap(f.lyaw-f.fyaw)*MeleeMath.Ease(sw);
                        f.pitch=MeleeMath.PoseCurve(PitchKeys,K(toeOff,toeOff+.12f*sprint,.05f,MeleeMath.Lerp(-heel,.22f*weight,back)),sw)*MeleeMath.Clamp(speed/1.5f+.001f,0,1);f.toe=-Math.Max(0,toeOff)*.85f*(1-MeleeMath.Ease(sw/.35f));
                        if(sw>=1){float since=wrapped&&rate>1e-3f?u/rate:0;f.planted=true;f.x=f.lx-lvx*since;f.z=f.lz-lvz*since;f.y=FLOOR;f.yaw=f.lyaw;}
                    }
                }
                if(!f.planted||!settled&&speed<.25f)settle=true;
            }
            settling=settle;
            // Body: bob lowest at mid-stance, lateral sway over the stance foot, lean about the ankles, living idle.
            float ph2=r.Stride+phaseOffset,idleW=1-MeleeMath.Clamp(run,0,1),ph=phase;
            float shift=((float)Math.Sin(now*MeleeMath.TAU/4.3f+ph)*.6f+(float)Math.Sin(now*MeleeMath.TAU/7.1f+ph*1.7f)*.4f)*idleW,breath=(float)Math.Sin(now*MeleeMath.TAU*.25f+ph)*idleW;
            float moveW=weight,bob=-(.008f+.010f*sprint)*moveW*(float)Math.Cos(2*ph2-MeleeMath.TAU*contact),sway=(float)Math.Cos(ph2-MeleeMath.PI*contact)*.012f*(1-sprint*.4f)*moveW;
            float bodyLean=RunLean*weight*Math.Max(0,forward)*(1-back)+.05f*weight*back+lean+dashLean+dip*1.1f;
            float vy=p.Grounded?0:p.Vel.Y,airAge=now-airAt,push=air*Math.Max(MeleeMath.Ease((vy-3)/3),Math.Max(1-MeleeMath.Ease(airAge/.12f),dashLean/.15f)),descend=air*MeleeMath.Ease((-vy-2)/3),apex=Math.Max(0,air-push-descend);
            float sinceRoll=p.RollUntil>-90&&time>=p.RollUntil?time-p.RollUntil:1,postRoll=.08f*(1-MeleeMath.Ease(sinceRoll/.22f));
            r.P[CapturedRig.Body]=new Vector3(sway+shift*.018f,-.025f-RunCrouch*moveW+(stance.Crouch-stance.KneeSoft*.08f)*idleW+bob+breath*.004f-dip-postRoll,.295f*(float)Math.Sin(bodyLean));
            r.SetE(CapturedRig.Body,bodyLean,0,-strafe*weight*.08f+bank);
            float twist=weight*(1-back*.5f),gait=(float)Math.Sin(ph2),pelvisRoll=(float)Math.Cos(ph2-MeleeMath.PI*contact)*.05f*moveW+shift*.035f;
            r.SetE(CapturedRig.Pelvis,0,gait*.14f*twist,pelvisRoll);
            float spineYaw=-(float)Math.Sin(ph2-.35f)*.10f*twist,chestYaw=-(float)Math.Sin(ph2-.6f)*.12f*twist;
            r.SetE(CapturedRig.Spine,-breath*.006f+stance.Lean*.6f*idleW,spineYaw,-pelvisRoll*.6f);r.SetE(CapturedRig.Chest,breath*.02f+stance.Lean*.4f*idleW,chestYaw,-pelvisRoll*.35f-(float)Math.Cos(ph2)*.02f*moveW);
            r.SetE(CapturedRig.Neck,-r.E[CapturedRig.Body].X*.25f,0,0);
            r.SetE(CapturedRig.Head,-p.Pitch*.55f-r.E[CapturedRig.Body].X*.4f,-(gait*.14f*twist+spineYaw+chestYaw)+(float)Math.Sin(now*.6f+ph)*.014f*idleW,shift*.012f-r.E[CapturedRig.Body].Z*.5f);
            // Legs: planted/swing feet on the floor, blended toward the vy-driven air pose.
            for(int i=0;i<2;i++)
            {
                var f=feet[i];bool left=i==0,isLead=i==airLead;float sx=left?1:-1;
                Vector3 target=new Vector3(f.x,f.y,f.z);float fy=f.yaw,fpitch=f.pitch,ftoe=f.toe;Vector3 h=r.HipInGroup(left);
                if(air>.001f)
                {
                    int hip=left?CapturedRig.LHip:CapturedRig.RHip;float wsum=Math.Max(1e-6f,push+apex+descend);r.PelvisFrame(out var pq,out var pp);
                    Vector3 a=Vector3.Transform(new Vector3(-sx*.025f,(push*(isLead?-.30f:-.47f)+apex*(isLead?-.31f:-.42f)+descend*(isLead?-.44f:-.47f))/wsum,(push*(isLead?.16f:-.16f)+apex*(isLead?.23f:-.10f)+descend*(isLead?.10f:-.05f))/wsum)+r.P[hip],pq)+pp;
                    target=MeleeMath.Lerp(target,a,air);fy*=1-air;fpitch=MeleeMath.Lerp(fpitch,(push*.55f+apex*.25f-descend*.08f)/wsum,air);ftoe*=1-air;
                }
                if(p.Grounded)target.Y=Math.Max(target.Y,FLOOR);
                if(!f.planted&&air<.5f){float d=Vector3.Distance(target,h);if(d>REACH)target=h+(target-h)*(REACH/d);}
                r.SolveLeg(left,target,fy,fpitch,ftoe);hips[i]=h;f.sx=target.X;f.sy=target.Y;f.sz=target.Z;f.syaw=fy;f.spitch=fpitch;
            }
            for(int k=0;k<6;k++)legQ[k]=r.Q[LegBones[k]];solvedBodyP=r.P[CapturedRig.Body];solvedBodyQ=r.Q[CapturedRig.Body];solvedPelvisQ=r.Q[CapturedRig.Pelvis];solved=true;
            // Arms: pump opposite the legs, rise on push-off, reach on descent.
            float armAmp=MeleeMath.Lerp(1,.4f,back);
            r.SetE(CapturedRig.LClav,-breath*.009f,gait*.045f*weight,.03f+breath*.006f);r.SetE(CapturedRig.RClav,-breath*.009f,-gait*.045f*weight,-.03f-breath*.006f);
            SwingArm(r,this,true,ph2,run,air,armAmp,back,push,descend,dt);SwingArm(r,this,false,ph2,run,air,armAmp,back,push,descend,dt);
        }
        private static float Hyp(float a,float b)=>(float)Math.Sqrt(a*a+b*b);
        private readonly Vector3[] cap=new Vector3[2],capK=new Vector3[2];private readonly Quaternion[] capQ=new Quaternion[2];
        // Ground pass, after every action layer: pelvis follow, legs re-solved onto their planted (or layer-placed) ankles.
        public void GroundPass(MeleeRig r,MeleeView p,float time,float dt,bool skip)
        {
            if(!has||!solved||raw!=time)return;solved=false;dt=MeleeMath.Clamp(dt,0,.05f);
            bool layered=r.LegsLayered&&!r.LegsAuthored,changed=r.LegsAuthored||layered;for(int k=0;k<6&&!changed;k++)changed=!r.Q[LegBones[k]].Equals(legQ[k]);
            if(skip){legYaw=0;legYawV=0;fresh=false;SyncFeet(r,p,false);return;}
            // Critically damped hip follow; layers that own the legs let it relax to zero.
            float target=changed&&!layered?0:hipYaw,w=LegYawOmega;if(fresh){fresh=false;legYaw=target;legYawV=0;}else{legYawV=(legYawV-(legYaw-target)*w*w*dt)/(1+2*w*dt+w*w*dt*dt);legYaw+=legYawV*dt;}
            float L=legYaw;if(changed)for(int i=0;i<2;i++)cap[i]=r.ReadAnkle(i==0,out capQ[i],out capK[i]);
            if(Math.Abs(L)>1e-5f)
            {
                Quaternion P=r.Q[CapturedRig.Pelvis],S=r.Q[CapturedRig.Spine],C=r.Q[CapturedRig.Chest],Pi=MeleeMath.Inverse(P);
                r.SetQ(CapturedRig.Pelvis,MeleeMath.AxisAngle(MeleeMath.Y,L)*P);
                r.SetQ(CapturedRig.Spine,Pi*MeleeMath.AxisAngle(MeleeMath.Y,-.45f*L)*P*S);
                r.SetQ(CapturedRig.Chest,MeleeMath.Inverse(S)*Pi*MeleeMath.AxisAngle(MeleeMath.Y,-.55f*L)*P*S*C);
            }
            if(!p.Grounded)return;
            if(changed){for(int i=0;i<2;i++){Vector3 c=cap[i];c.Y=Math.Max(c.Y,FLOOR);r.SolveLeg(i==0,c,0,0,0,capQ[i],capK[i]);}if(!layered)SyncFeet(r,p,true);return;}
            if(Math.Abs(L)<=1e-5f&&r.P[CapturedRig.Body].Equals(solvedBodyP)&&r.Q[CapturedRig.Body].Equals(solvedBodyQ)&&r.Q[CapturedRig.Pelvis].Equals(solvedPelvisQ))return;
            for(int i=0;i<2;i++){var f=feet[i];r.SolveLeg(i==0,new Vector3(f.sx,f.sy,f.sz),f.syaw,f.spitch,f.toe);}
        }
        // The gait state follows ankles an action layer placed, so the gait resumes from them without a pop.
        private void SyncFeet(MeleeRig r,MeleeView p,bool captured)
        {
            if(!p.Grounded)return;
            for(int i=0;i<2;i++)
            {
                var f=feet[i];if(!captured)cap[i]=r.ReadAnkle(i==0,out capQ[i],out _);Vector3 c=cap[i];
                if(Vector3.Distance(c,new Vector3(f.sx,f.sy,f.sz))<.015f)continue;
                Vector3 fwd=Vector3.Transform(MeleeMath.Z,capQ[i]);
                f.x=f.fx=f.sx=c.X;f.z=f.fz=f.sz=c.Z;f.y=f.fy=f.sy=Math.Max(FLOOR,c.Y);f.yaw=f.fyaw=f.syaw=(float)Math.Atan2(fwd.X,fwd.Z);f.pitch=f.spitch=(float)Math.Asin(MeleeMath.Clamp(-fwd.Y,-1,1));f.toe=0;
                f.planted=c.Y<FLOOR+.015f;if(!f.planted)f.uLift=Math.Min(MeleeMath.Frac(r.Stride/MeleeMath.TAU+(i==1?.5f:0)),.97f);
            }
        }
    }
}
