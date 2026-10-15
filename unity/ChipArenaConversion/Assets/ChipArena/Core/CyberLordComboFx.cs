using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The hardlight string's effects (Core/CyberLordCombo.cs; v1's three steps and v2's five): a hardlight trail behind each
    /// striking fist along its authored path (a white-hot core in a cyan ribbon with a magenta glitch edge, shedding pixel
    /// bits), a snap flare where the fist reaches full extension on the replicated hit, the spinning backfist's crescent
    /// round the pilot, the rising uppercut's climbing crescent and sparks, and the Pulse Palm's punch: a flash between the
    /// palms, hex shock rings racing out, radial streaks, shards and the stomp's kicked-up blocks. Each is a pure function of
    /// the view (the swing) and the presentation clock; the trail's history is the keys' own fist path, so a guest, a
    /// spectator and the killcam draw it the same. Nothing lies flat on the floor. No allocation per frame.
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
            if(sw.Kind==CyberCombo.Spin&&!me)SpinCrescent(s,v,sw,feet,time,seed+71,q,low);
            if(sw.Kind==CyberCombo.Rise&&!me)RiseCrescent(s,scene,a,v,sw,feet,time,seed+73,q,low);
            if(sw.Kind==CyberCombo.Palm)PalmPulse(s,scene,a,v,sw,feet,time,seed+79,q,low,me);
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
        // The spinning backfist's crescent: a hardlight band sweeping round the pilot at the fist's height over the arc the rules
        // strike (CyberKit2Spec.SpinArc), brightest at the hit, its leading edge on the fist.
        private void SpinCrescent(ICyberFxSink s,CyberLordView v,in CyberCombo.Swing sw,Vector3 feet,float time,long seed,float q,bool low)
        {
            float t0=sw.Hit-(sw.Hit-sw.At)*.45f,t1=sw.Hit+(sw.Until-sw.Hit)*.35f;if(time<t0||time>t1+.12f)return;
            float fade=time<=sw.Hit?Smooth((time-t0)/Math.Max(1e-3f,sw.Hit-t0)):1-Smooth((time-sw.Hit)/Math.Max(1e-3f,t1+.12f-sw.Hit));if(fade<=.01f)return;
            int segs=low?8:16;float arc=CyberKit2Spec.SpinArc,sweep=Clamp01((time-t0)/Math.Max(1e-3f,t1-t0));
            var fistNow=CyberCombo.FistAt(sw,time,true,strikeScratch);float lead=(float)Math.Atan2(fistNow.X,fistNow.Z),radius=Math.Max(.45f,new Vector2(fistNow.X,fistNow.Z).Length()),y=fistNow.Y;
            for(int i=0;i<segs;i++)
            {
                float k0=(float)i/segs,k1=(float)(i+1)/segs;if(k1>sweep+.05f)break;
                // (tilted: the tail rides higher, so the sweep reads as a curve from a level camera too)
                float a0=lead-arc*(1-k0)*.9f,a1=lead-arc*(1-k1)*.9f,y0=y+.3f*(1-k0)*(1-k0),y1=y+.3f*(1-k1)*(1-k1);var p0=BodyToWorld(feet,v.Yaw,new Vector3((float)Math.Sin(a0)*radius,y0,(float)Math.Cos(a0)*radius));var p1=BodyToWorld(feet,v.Yaw,new Vector3((float)Math.Sin(a1)*radius,y1,(float)Math.Cos(a1)*radius));
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
                var foot=feet+f*.3f+r*(-.16f);int blocks=low?3:Count(7,q,low);
                for(int i=0;i<blocks;i++){float ang=H(seed+i,21)*MeleeMath.TAU,rad=.15f+.3f*H(seed+i,23)*Smooth(u*2);float up=Math.Max(0,1.8f*age-5*age*age)*(.5f+H(seed+i,25));var p=foot+(r*(float)Math.Cos(ang)+f*(float)Math.Sin(ang))*rad+Up*(.03f+up);float z=.035f*(1-u);
                    if(z>.004f)s.Voxel(false,WeaponShape.Cube,p,Quaternion.CreateFromAxisAngle(Up,ang+age*6),new Vector3(z,z*1.6f,z),Lin(i%3==0?Magenta:Cyan,1.8f));}
            }
        }
    }
}
