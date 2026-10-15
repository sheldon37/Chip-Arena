using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The hardlight string's effects (Core/CyberLordCombo.cs; v1's three steps and v2's five), each on the limb that strikes:
    /// a hardlight trail behind the striking fist (the jab, the uppercut, the palms) or foot (the roundhouse and the spinning
    /// hook kick) along its authored path (a white-hot core in a cyan ribbon with a magenta glitch edge, shedding pixel bits),
    /// a snap flare where the limb reaches full extension on the replicated hit, the spinning hook kick's crescent at the
    /// foot round the pilot, the launcher's knee flare, climbing crescent, sparks and upward pixel smear, and the Pulse Palm's
    /// punch: a flash between the palms, hex shock rings racing out, radial streaks, shards and the stomp's kicked-up blocks
    /// round the foot where it lands. The "cyber movement": the glitch-step jab leaves RGB afterimages behind its step-in and
    /// every impact holds an RGB split of the body for a beat (the palm's for its whole freeze-frame); with reduced effects
    /// the splits are small and steady (never a strobe). Each is a pure function of the view (the swing) and the
    /// presentation clock; the trails' history is the keys' own limb path, so a guest, a spectator and the killcam draw it
    /// the same. Nothing lies flat on the floor. No allocation per frame.
    /// </summary>
    public sealed partial class NativeCyberLordFx
    {
        public const int TrailSamples=10;
        /// <summary>The strikes' trail length (s of path behind the fist, at most) and the palm pulse's life (s).</summary>
        public const float TrailSeconds=.11f,PulseSeconds=.34f;
        private readonly float[] strikeScratch=new float[CyberCombo.N];
        private readonly Vector3[] trailPoints=new Vector3[TrailSamples];
        /// <summary>The pilot's strike at `time` (v2's string when its record is live, else v1's), to its end and a short tail.</summary>
        public static bool StrikeOf(CyberLordView v,float time,out CyberCombo.Swing sw)
        {
            sw=default;var k=v.Kit2;
            if(k.Live){if(!(k.ComboAt>-90)||time<k.ComboAt||time>k.ComboUntil+.2f)return false;sw=new CyberCombo.Swing(CyberCombo.V2Kind(k.ComboStep),k.ComboAt,Math.Max(k.ComboAt+.02f,Math.Min(k.ComboUntil-.02f,k.ComboHit)),k.ComboUntil);return true;}
            if(!(v.ComboAt>-90)||time<v.ComboAt||time>v.ComboUntil+.2f)return false;
            sw=new CyberCombo.Swing(CyberCombo.V1Kind(v.ComboStep),v.ComboAt,Math.Max(v.ComboAt+.02f,Math.Min(v.ComboUntil-.02f,v.ComboHit)),v.ComboUntil);return true;
        }
        /// <summary>A body-space point (the rig's group: +x the pilot's left) in the world for a pilot at `feet` facing `yaw`.</summary>
        public static Vector3 BodyToWorld(Vector3 feet,float yaw,Vector3 b)=>feet+Up*CyberLordFeed.BodyCentre+Vector3.Transform(new Vector3(-b.X,b.Y,b.Z),Yaw(yaw));
        private void Strikes(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,float q,bool low,bool me)
        {
            if(!StrikeOf(v,time,out var sw))return;var feet=scene.Feet(a);long seed=a.Id*6151L+(long)Math.Round(sw.At*240);int hand=CyberCombo.Hand[sw.Kind];
            for(int h=0;h<2;h++){bool left=h==0;if(hand!=2&&hand!=(left?0:1))continue;FistTrail(s,scene,a,v,sw,left,feet,time,seed+h*31,q,low,me);}
            // (a kick is not seen in first person: its effects are the third person's)
            if(CyberCombo.Kicks(sw.Kind)&&!me)FootTrail(s,scene,v,sw,feet,time,seed+43,q,low);
            if(sw.Kind==CyberCombo.Spin&&!me)SpinCrescent(s,v,sw,feet,time,seed+71,q,low);
            if(sw.Kind==CyberCombo.Rise&&!me)RiseCrescent(s,scene,a,v,sw,feet,time,seed+73,q,low);
            if(sw.Kind==CyberCombo.Palm)PalmPulse(s,scene,a,v,sw,feet,time,seed+79,q,low,me);
            if(!me)StrikeGhosts(a,v,sw,time,q);
        }
        /// <summary>The impact's RGB hold (s at rate 1; the palm holds through its freeze-frame) and the split's offsets (m).</summary>
        public const float HoldSeconds=.075f,SplitOffset=.045f,ReducedSplit=.012f;
        /// <summary>
        /// The string's glitch ghosts (CyberGhost.Split: the posed body offset): the glitch-step jab's RGB afterimages trailing
        /// back along its step-in from its first frame-skip snap to just after the blow, and on every impact an RGB split of the
        /// body (magenta to one side, cyan to the other) held for a beat and fading (the palm's through its freeze-frame).
        /// Reduced effects: one small steady split and one faint afterimage, no flicker. The local first-person pilot draws none.
        /// </summary>
        private void StrikeGhosts(Actor a,CyberLordView v,in CyberCombo.Swing sw,float time,float q)
        {
            bool reduced=q<.5f;var f=Forward(v.Yaw);var right=Right(f);
            if(sw.Kind==CyberCombo.Jab&&ghostCount<MaxGhosts-2)
            {
                float w=sw.Hit-sw.At,first=sw.At+w/Math.Max(1,CyberCombo.StutterSteps[CyberCombo.Jab]),end=sw.Hit+.1f;
                if(time>=first&&time<end)
                {
                    float u=Clamp01((time-first)/Math.Max(1e-3f,end-first)),g=time<sw.Hit?1:1-Smooth((time-sw.Hit)/(end-sw.Hit));float back=.07f+.1f*(1-u);
                    if(reduced)ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=-f*back*.5f,Tint=Lin(Cyan,1.2f),Opacity=.07f*g};
                    else{ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=-f*back+right*.012f,Tint=Lin(Cyan,1.5f),Opacity=.16f*g};ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=-f*back*1.9f-right*.012f,Tint=Lin(Magenta,1.5f),Opacity=.09f*g};}
                }
            }
            float age=time-sw.Hit;if(age<0||ghostCount>=MaxGhosts-2)return;
            float scale=(sw.Until-sw.Hit)/Math.Max(1e-3f,CyberCombo.Seconds(sw.Kind)-CyberCombo.Windup(sw.Kind));
            float hold=sw.Kind==CyberCombo.Palm?(sw.Until-sw.Hit)*CyberCombo.PalmFreeze+.03f:HoldSeconds*Math.Min(1,scale);if(age>=hold)return;
            float k=sw.Kind==CyberCombo.Palm?1-Smooth((age-hold*.7f)/(hold*.3f)):1-Smooth(age/hold);if(k<=.01f)return;
            float off=reduced?ReducedSplit:SplitOffset*(sw.Kind==CyberCombo.Palm?1.5f:1);
            ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=right*off,Tint=Lin(Magenta,1.4f),Opacity=(reduced?.07f:.13f)*k};
            ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=-right*off,Tint=Lin(Cyan,1.4f),Opacity=(reduced?.07f:.13f)*k};
        }
        private readonly Vector3[] footPoints=new Vector3[TrailSamples];
        // The kicking foot's hardlight trail: the keys' ankle path over the last stretch (only where the leg is on its keys), a
        // wider ribbon pair than the fists' (a white-hot core in a cyan sheath, a magenta glitch edge), pixel bits shed along it,
        // and the snap flare at the foot on the hit.
        private void FootTrail(ICyberFxSink s,ICyberFxScene scene,CyberLordView v,in CyberCombo.Swing sw,Vector3 feet,float time,long seed,float q,bool low)
        {
            float drive=CyberCombo.Drive(sw,time),snap=time>=sw.Hit&&time<sw.Hit+.1f?1-Smooth((time-sw.Hit)/.1f):0;if(drive<=.01f&&snap<=0)return;bool reduced=q<.5f;
            CyberCombo.FootAt(sw,time,strikeScratch,out float kw);if(kw<.5f)return;
            int n=low?6:TrailSamples,m=0;float span=Math.Max(.06f,Math.Min(TrailSeconds*1.4f,(sw.Hit-sw.At)*1.2f));
            for(int i=0;i<n;i++)
            {
                float t=Math.Max(sw.At,time-span*(n-1-i)/(n-1));var p=CyberCombo.FootAt(sw,t,strikeScratch,out float w);
                if(w<.5f){m=0;continue;}footPoints[m++]=BodyToWorld(feet,v.Yaw,p);
            }
            if(m<2)return;var eye=scene.Eye;float gain=drive*(reduced?.7f:1);
            for(int i=1;i<m;i++)
            {
                Vector3 p0=footPoints[i-1],p1=footPoints[i],mid=(p0+p1)*.5f,side=Vector3.Cross(p1-p0,eye-mid);if(side.LengthSquared()<1e-10f)continue;side=Vector3.Normalize(side);
                float u0=(i-1f)/(m-1),u1=(float)i/(m-1),w0=.04f+.08f*u0,w1=.04f+.08f*u1;
                s.Band(false,p0-side*w0*1.9f,p1-side*w1*1.9f,p1+side*w1*1.9f,p0+side*w0*1.9f,u0*gain,u1*gain,Lin(Cyan,1.6f));
                s.Band(false,p0-side*w0*.6f,p1-side*w1*.6f,p1+side*w1*.6f,p0+side*w0*.6f,u0*gain,u1*gain,Lin(White,2.6f));
                if(!reduced&&!low){var off=side*(.045f+.025f*H(seed+(long)(time*24),(uint)i));s.Band(false,p0+off-side*w0*.3f,p1+off-side*w1*.3f,p1+off+side*w1*.3f,p0+off+side*w0*.3f,u0*gain*.7f,u1*gain*.7f,Lin(Magenta,1.8f));}
            }
            int bits=low?2:Count(6,q,low);int step=(int)(time*24);
            for(int i=0;i<bits;i++)
            {
                long k=seed*13+step*7+i;int j=Math.Min(m-1,(int)(H(k,3)*(m-1)));var p=footPoints[j]+new Vector3(H(k,5)-.5f,H(k,7)-.5f,H(k,9)-.5f)*.14f;float z=(.02f+.022f*H(k,11))*gain;
                if(z>.004f)s.Voxel(false,WeaponShape.Cube,p,Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(H(k,13),1,H(k,17))),H(k,19)*3),new Vector3(z,z,z*.5f),Lin(i%3==0?Magenta:i%2==0?White:Cyan,2.4f));
            }
            if(snap>0){var at=footPoints[m-1];s.Sprite(false,at,Lin(White,3.4f*snap*snap),.2f+.14f*(1-snap),Hot);s.Sprite(false,at,Lin(Cyan,1.8f*snap),.16f+.36f*(1-snap),RingSprite);}
        }
        // The striking fist's hardlight trail: the keys' path over the last TrailSeconds, shifted onto the drawn fist, as a tapering
        // ribbon pair (a white-hot core in a wider cyan sheath, a thin magenta edge for the glitch), pixel bits shed along it, and the
        // snap flare at full extension. In first person a short streak on the view fist.
        private void FistTrail(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,in CyberCombo.Swing sw,bool left,Vector3 feet,float time,long seed,float q,bool low,bool me)
        {
            float drive=CyberCombo.Drive(sw,time),snap=time>=sw.Hit&&time<sw.Hit+.09f?1-Smooth((time-sw.Hit)/.09f):0;if(drive<=.01f&&snap<=0)return;bool reduced=q<.5f;
            var head=BodyToWorld(feet,v.Yaw,CyberCombo.FistAt(sw,time,left,strikeScratch));
            if(me)
            {
                if(!scene.ViewFist(left,out var vf))return;var dir=Vector3.Normalize(scene.Eye-vf);
                s.Line(true,vf+dir*(.16f*drive),vf,Lin(White,2.2f*drive),.022f,false);if(snap>0)s.Sprite(true,vf,Lin(Cyan,2.4f*snap),.07f+.05f*(1-snap),Hot);return;
            }
            Vector3 fist;var shift=scene.Fist(a,left,out fist)?fist-head:Vector3.Zero;if(shift.LengthSquared()>.09f)shift=Vector3.Normalize(shift)*.3f;
            int n=low?6:TrailSamples;float span=Math.Max(.05f,Math.Min(TrailSeconds,(sw.Hit-sw.At)*1.1f));
            for(int i=0;i<n;i++){float t=time-span*(n-1-i)/(n-1);trailPoints[i]=BodyToWorld(feet,v.Yaw,CyberCombo.FistAt(sw,Math.Max(sw.At,t),left,strikeScratch))+shift;}
            var eye=scene.Eye;float gain=drive*(reduced?.7f:1);
            for(int i=1;i<n;i++)
            {
                Vector3 p0=trailPoints[i-1],p1=trailPoints[i],mid=(p0+p1)*.5f,side=Vector3.Cross(p1-p0,eye-mid);if(side.LengthSquared()<1e-10f)continue;side=Vector3.Normalize(side);
                float u0=(i-1f)/(n-1),u1=(float)i/(n-1),w0=.03f+.06f*u0,w1=.03f+.06f*u1;
                s.Band(false,p0-side*w0*1.9f,p1-side*w1*1.9f,p1+side*w1*1.9f,p0+side*w0*1.9f,u0*gain,u1*gain,Lin(Cyan,1.6f));
                s.Band(false,p0-side*w0*.6f,p1-side*w1*.6f,p1+side*w1*.6f,p0+side*w0*.6f,u0*gain,u1*gain,Lin(White,2.6f));
                if(!reduced&&!low){var off=side*(.035f+.02f*H(seed+(long)(time*24),(uint)i));s.Band(false,p0+off-side*w0*.3f,p1+off-side*w1*.3f,p1+off+side*w1*.3f,p0+off+side*w0*.3f,u0*gain*.7f,u1*gain*.7f,Lin(Magenta,1.8f));}
            }
            // pixel bits shed off the path, drifting and falling, on 24 Hz glitch steps
            int bits=low?2:Count(5,q,low);int step=(int)(time*24);
            for(int i=0;i<bits;i++)
            {
                long k=seed*13+step*7+i;float along=H(k,3);int j=Math.Min(n-1,(int)(along*(n-1)));var p=trailPoints[j]+new Vector3(H(k,5)-.5f,H(k,7)-.5f,H(k,9)-.5f)*.12f;float z=(.018f+.02f*H(k,11))*gain;
                if(z>.004f)s.Voxel(false,WeaponShape.Cube,p,Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(H(k,13),1,H(k,17))),H(k,19)*3),new Vector3(z,z,z*.5f),Lin(i%3==0?Magenta:i%2==0?White:Cyan,2.4f));
            }
            // the snap at full extension: a hot flare and a quick ring at the fist
            if(snap>0){var at=trailPoints[n-1];s.Sprite(false,at,Lin(White,3.4f*snap*snap),.16f+.12f*(1-snap),Hot);s.Sprite(false,at,Lin(Cyan,1.8f*snap),.12f+.3f*(1-snap),RingSprite);}
        }
        // The spinning heel kick's crescent: a hardlight band sweeping round the pilot at the kicking foot's height over the arc
        // the rules strike (CyberKit2Spec.SpinArc) in the turn's direction, brightest at the hit, its leading edge on the heel.
        private void SpinCrescent(ICyberFxSink s,CyberLordView v,in CyberCombo.Swing sw,Vector3 feet,float time,long seed,float q,bool low)
        {
            float t0=sw.Hit-(sw.Hit-sw.At)*.45f,t1=sw.Hit+(sw.Until-sw.Hit)*.35f;if(time<t0||time>t1+.12f)return;
            float fade=time<=sw.Hit?Smooth((time-t0)/Math.Max(1e-3f,sw.Hit-t0)):1-Smooth((time-sw.Hit)/Math.Max(1e-3f,t1+.12f-sw.Hit));if(fade<=.01f)return;
            int segs=low?8:16;float arc=CyberKit2Spec.SpinArc,sweep=Clamp01((time-t0)/Math.Max(1e-3f,t1-t0));
            var footNow=CyberCombo.FootAt(sw,time,strikeScratch,out _);float lead=(float)Math.Atan2(footNow.X,footNow.Z),radius=Math.Max(.45f,new Vector2(footNow.X,footNow.Z).Length()),y=footNow.Y;
            float dir=CyberCombo.HipsTurn(sw,sw.Hit,strikeScratch)>=CyberCombo.HipsTurn(sw,sw.At,strikeScratch)?1:-1;
            for(int i=0;i<segs;i++)
            {
                float k0=(float)i/segs,k1=(float)(i+1)/segs;if(k1>sweep+.05f)break;
                // (tilted: the tail rides higher, so the sweep reads as a curve from a level camera too)
                float a0=lead-dir*arc*(1-k0)*.9f,a1=lead-dir*arc*(1-k1)*.9f,y0=y+.18f*(1-k0)*(1-k0),y1=y+.18f*(1-k1)*(1-k1);var p0=BodyToWorld(feet,v.Yaw,new Vector3((float)Math.Sin(a0)*radius,y0,(float)Math.Cos(a0)*radius));var p1=BodyToWorld(feet,v.Yaw,new Vector3((float)Math.Sin(a1)*radius,y1,(float)Math.Cos(a1)*radius));
                float h0=.05f+.12f*k0,h1=.05f+.12f*k1;s.Band(false,p0-Up*h0,p1-Up*h1,p1+Up*h1,p0+Up*h0,k0*fade,k1*fade,Lin(i%4==3?Magenta:Cyan,1.7f));
                if(i%3==0&&!low)s.Line(false,p0,p1,Lin(White,2.4f*fade*k1),.012f,false);
            }
        }
        // The rising uppercut's climbing crescent and the sparks flung up off it at the hit.
        private void RiseCrescent(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,in CyberCombo.Swing sw,Vector3 feet,float time,long seed,float q,bool low)
        {
            float age=time-sw.Hit;if(age<-.04f||age>.3f)return;float u=Clamp01(age/.3f),fade=1-Smooth(u);
            Vector3 fist;if(!scene.Fist(a,false,out fist))fist=BodyToWorld(feet,v.Yaw,CyberCombo.FistAt(sw,time,false,strikeScratch));
            var f=Forward(v.Yaw);int n=Count(8,q,low);
            for(int i=0;i<n;i++){float h=H(seed+i,3),sp=4+6*h;var d=Vector3.Normalize(new Vector3(H(seed+i,5)-.5f,1.6f,H(seed+i,7)-.5f)+f*.25f);if(age<0)continue;s.Streak(fist+d*(sp*age),d*sp,Lin(i%3==0?Magenta:Cyan,2.4f*fade),.02f,.14f,.03f,.9f,u,seed+i);}
            if(age>=0)s.Sprite(false,fist+Up*(.4f*age),Lin(White,2.6f*fade*fade),.2f+.25f*u,Hot);
            // the jumping knee's flare at the hit, and the climb's pixel smear: blocks left hanging along the rising fist's path
            if(age>=0&&age<.14f){var knee=BodyToWorld(feet,v.Yaw,CyberCombo.KneeAt(sw,sw.Hit,strikeScratch));float kf=1-Smooth(age/.14f);s.Sprite(false,knee,Lin(White,2.8f*kf*kf),.14f+.12f*(1-kf),Hot);s.Sprite(false,knee,Lin(Magenta,1.4f*kf),.12f+.26f*(1-kf),RingSprite);}
            int bits=low?3:Count(9,q,low);int step=(int)(time*24);
            for(int i=0;i<bits;i++)
            {
                long k=seed*17+i;float back=.03f+.1f*H(k,3),t=sw.Hit+age-back;if(t<sw.At+(sw.Hit-sw.At)*.5f)continue;var p=BodyToWorld(feet,v.Yaw,CyberCombo.FistAt(sw,t,false,strikeScratch))+new Vector3(H(k,5)-.5f,0,H(k,7)-.5f)*.12f-Up*(.6f*age*age);
                float z=.03f*fade*(.6f+.4f*H(k+step,9));if(z>.004f)s.Voxel(false,WeaponShape.Cube,p,Quaternion.Identity,new Vector3(z,z*1.8f,z*.5f),Lin(i%3==0?Magenta:i%2==0?White:Cyan,2.2f));
            }
        }
        // The Pulse Palm's punch at the replicated hit: a white flash between the palms, three hex shock rings racing out to the
        // palm's cone (CyberLordSpec / CyberKit2Spec PalmCone) and growing, radial streaks, shards blasted ahead, and blocks
        // kicked up round the stomping lead foot (upright, never a floor disc).
        private void PalmPulse(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,in CyberCombo.Swing sw,Vector3 feet,float time,long seed,float q,bool low,bool me)
        {
            float age=time-sw.Hit;if(age<0||age>=PulseSeconds)return;float u=age/PulseSeconds,fade=1-Smooth(u);var f=Forward(v.Yaw);var r=Right(f);bool reduced=q<.5f;
            Vector3 l,rr;var palms=scene.Fist(a,true,out l)&&scene.Fist(a,false,out rr)?(l+rr)*.5f+f*.08f:feet+Up*1.05f+f*.75f;
            if(me){if(scene.ViewFist(true,out var vl)&&scene.ViewFist(false,out var vr)){var c=(vl+vr)*.5f;s.Sprite(true,c,Lin(White,3f*fade*fade),.1f+.1f*u,Hot);}palms=feet+Up*1.2f+f*.55f;}
            float flash=1-Smooth(age/.07f);if(flash>0&&!me)s.Sprite(false,palms,Lin(White,(reduced?2.4f:5f)*flash),.42f+.3f*(1-flash),Hot);
            s.Glow(palms+f*(.2f+.6f*u),Lin(Cyan,.9f*fade),0,.5f+.6f*u,Core,u,seed);
            float cone=CyberKit2Spec.PalmCone;
            for(int k=0;k<3;k++)
            {
                float ku=Clamp01((age-k*.035f)/.16f);if(ku<=0)continue;float d=cone*(1-(1-ku)*(1-ku)),rad=.22f+.6f*ku,g=fade*(1-.25f*k)*(1-.5f*ku);var c=palms+f*d;int sides=6;
                for(int i=0;i<sides;i++)
                {
                    float a0=i*MeleeMath.TAU/sides+.3f*k,a1=a0+MeleeMath.TAU/sides;var p0=c+(r*(float)Math.Cos(a0)+Up*(float)Math.Sin(a0))*rad;var p1=c+(r*(float)Math.Cos(a1)+Up*(float)Math.Sin(a1))*rad;
                    s.Line(false,p0,p1,Lin(k==0?White:k==1?Cyan:Magenta,(k==0?3f:2.2f)*g),.035f-.008f*k,false);
                    if(k==0&&!low)s.Band(false,p0,p1,p1+f*(-.25f),p0+f*(-.25f),.9f*g,.9f*g,Lin(Cyan,1.2f));
                }
            }
            if(!reduced)
            {
                int n=Count(12,q,low);for(int i=0;i<n;i++){float ang=i*MeleeMath.TAU/n+H(seed+i,3)*.4f,sp=7+5*H(seed+i,5);var d=Vector3.Normalize(f*1.6f+(r*(float)Math.Cos(ang)+Up*(float)Math.Sin(ang)));s.Streak(palms+d*(sp*age),d*sp,Lin(i%4==0?Magenta:Cyan,2.4f*fade),.022f,.18f,.03f,.9f,u,seed+i);}
            }
            int shards=low?3:Count(8,q,low);
            for(int i=0;i<shards;i++){var d=Vector3.Normalize(f*2+new Vector3(H(seed+i,7)-.5f,H(seed+i,9)-.3f,H(seed+i,11)-.5f));float dist=(1.2f+1.6f*H(seed+i,13))*Smooth(u*1.4f);float z=.045f*(1-u);
                if(z>.004f)s.Voxel(false,WeaponShape.Cube,palms+d*dist-Up*(1.2f*age*age),Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1,H(seed+i,15),.4f)),age*14),new Vector3(z,z,z*.4f),Lin(i%2==0?White:Cyan,2.4f));}
            // the stomp: blocks popping up round the lead foot (the step lands on the hit)
            if(!me)
            {
                var land=CyberCombo.LandSpot(sw.Kind,CyberCombo.LeadFoot);var foot=BodyToWorld(feet,v.Yaw,new Vector3(land.X,-CyberLordFeed.BodyCentre,land.Z+.06f));int blocks=low?3:Count(7,q,low);
                for(int i=0;i<blocks;i++){float ang=H(seed+i,21)*MeleeMath.TAU,rad=.15f+.3f*H(seed+i,23)*Smooth(u*2);float up=Math.Max(0,1.8f*age-5*age*age)*(.5f+H(seed+i,25));var p=foot+(r*(float)Math.Cos(ang)+f*(float)Math.Sin(ang))*rad+Up*(.03f+up);float z=.035f*(1-u);
                    if(z>.004f)s.Voxel(false,WeaponShape.Cube,p,Quaternion.CreateFromAxisAngle(Up,ang+age*6),new Vector3(z,z*1.6f,z),Lin(i%3==0?Magenta:Cyan,1.8f));}
            }
        }
    }
}
