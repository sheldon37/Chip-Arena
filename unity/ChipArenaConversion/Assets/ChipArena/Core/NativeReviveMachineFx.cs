using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The REVIVE-O-MATIC's effects (docs/REVIVE-MACHINE.md), on the same terms as the techniques': pure functions of the machine's clock, thinned by Reduced and the low tier.
    public sealed partial class NativeTechniqueFx
    {
        private static readonly Vector3 ReviveSteam=new Vector3(.8f,.82f,.84f),ReviveSoot=new Vector3(.09f,.085f,.085f),ReviveSpark=new Vector3(1,.62f,.2f),ReviveFurnace=new Vector3(1,.34f,.06f),ReviveDust=new Vector3(.36f,.33f,.28f),ReviveGold=new Vector3(1,.76f,.25f);
        private static readonly Vector3[] Confetti={new Vector3(1,.18f,.16f),new Vector3(1,.8f,.15f),new Vector3(.2f,.8f,.9f),new Vector3(.95f,.95f,.9f),new Vector3(.4f,1,.45f)};
        /// <summary>Everything the machine gives off this frame. `world`: its frame in the world; `hull`: where the hull is on its springs (NativeReviveMachine.HullPose); `t`: seconds from the money (below 0: idling); `pilot`: the spit's mouth and landing, in the world.</summary>
        public void DrawReviveMachine(ITechniqueFxSink s,Matrix4x4 world,Matrix4x4 hull,float t,float clock,bool hasPilot,Vector3 mouth,Vector3 land,float half,float q,bool low)
        {
            Vector3 H(Vector3 v)=>Vector3.Transform(Vector3.Transform(v,hull),world);
            var up=Vector3.UnitY;var forward=Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ,world));var right=Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX,world));
            var stack=H(NativeReviveMachineModels.CapAt+new Vector3(0,.04f,0));var vents=NativeReviveMachineModels.Vents;
            // The furnace's light in its throat, seen through the shut teeth or the open mouth.
            float heat=NativeReviveMachine.Heat(t,clock),mouthOpen=t>=NativeReviveMachine.Spit&&t<NativeReviveMachine.Shut?1:.22f;var throat=H(new Vector3(0,NativeReviveMachineModels.MouthMid,1.02f));
            s.Glow(throat,ReviveFurnace*(.14f*heat*mouthOpen),0,.9f+.22f*heat*mouthOpen,Core,0,41);
            // The stack: a lazy puff idling, belching faster as it builds, nothing while it holds its breath.
            float rate=t<0?.6f:t<NativeReviveMachine.Gulp?.6f+4.2f*Math.Min(1,t/NativeReviveMachine.Gulp):t<NativeReviveMachine.Spit?0:1.2f;
            if(rate>0)
            {
                int n=Count(t<0?6:12,q,low);
                for(int i=0;i<n;i++)
                {
                    float life=t<0?3.6f:2.2f,ph=(clock*rate/ (n*.4f)+i/(float)n)%1,age=ph;long seed=9000+i*13;
                    var p=stack+up*(1.9f*age*age+.7f*age)+right*((R(seed,1)-.5f)*.5f*age+(float)Math.Sin(clock+i)*.14f*age)+forward*(.3f*age*(R(seed,2)-.3f));
                    s.Smoke(p,Vector3.Lerp(ReviveSoot,ReviveSteam,.3f),(t<0?.2f:.42f)*(1-age)*(1-age),.14f+.75f*age,.2f+.6f*age,seed,-1000);
                }
            }
            // The vents: steam, jetting harder as the pressure climbs; a wisp from the ears idling; cut dead by the gulp.
            float level=t<0?.05f:t<NativeReviveMachine.Gulp?Math.Max(.08f,NativeReviveMachine.ShakeLevel(t)):t<NativeReviveMachine.Spit?0:Math.Max(0,1.1f-(t-NativeReviveMachine.Spit)*.5f);
            if(level>.01f)
            {
                int perVent=Count(t<0?2:7,q,low);
                for(int v=0;v<vents.Length;v++)
                {
                    bool high=v<2;if(t<0&&!high)continue;var at=H(vents[v]);var dir=high?Vector3.Normalize(right*(v==0?1:-1)*.8f+up*.7f):v<4?Vector3.Normalize(up+right*(v==2?.5f:-.5f)):v<6?Vector3.Normalize(forward*.9f+right*(v==4?.3f:-.3f)-up*.1f):Vector3.Normalize(right*(v==6?1:-1)+up*.2f);
                    for(int i=0;i<perVent;i++)
                    {
                        long seed=7300+v*97+i*11;float ph=(clock*(.9f+.8f*R(seed,3))*(1+2*level)+R(seed,4))%1;float sp=(.5f+1.6f*level);
                        var p=at+dir*(sp*ph)+up*(.5f*ph*ph)+right*((R(seed,5)-.5f)*.3f*ph);
                        s.Smoke(p,ReviveSteam,(.1f+.32f*level)*(1-ph)*(ph<.1f?ph*10:1),.05f+.4f*ph*(.4f+level),.15f+.7f*ph,seed,-1000);
                    }
                }
            }
            if(t<0)return;
            // The flywheel throws sparks off its brake as it spins up.
            float spin=NativeReviveMachine.ShakeLevel(t);
            if(t<NativeReviveMachine.Gulp&&spin>.25f)
            {
                var wheel=H(NativeReviveMachineModels.WheelAxle+new Vector3(.1f,-.72f,.1f));int n=Count(10,q,low);
                for(int i=0;i<n;i++){long seed=6100+i*17;float ph=(clock*(3.1f+1.4f*R(seed,1))+R(seed,2))%1;var d=Vector3.Normalize(forward*(.5f+R(seed,3))+up*(.4f+R(seed,4))+right*(.4f+R(seed,5)*.4f));s.Sprite(false,wheel+d*(1.1f*ph)-up*(1.1f*ph*ph),ReviveSpark*(2.4f*(1-ph)*spin),.016f+.012f*R(seed,6),Hot);}
            }
            // Coins flash as they drop into the hopper.
            for(int c=0;c<NativeReviveMachine.Coins;c++){float u=NativeReviveMachine.CoinProgress(c,t);if(u>=1&&u<1.25f){var p=H(NativeReviveMachineModels.HopperAt+new Vector3(0,.42f,0));s.Sprite(false,p+up*(.12f*(1.25f-u)),ReviveGold*(3f*(1.25f-u)),.1f,Hot);}}
            if(t<NativeReviveMachine.Spit)return;
            float a=t-NativeReviveMachine.Spit;var lip=H(new Vector3(0,NativeReviveMachineModels.MouthMid,1.46f));
            // PTOOEY: a flash, a shock ring of steam out of the mouth, a cone of it, sparks, confetti.
            if(a<.16f){float k=1-a/.16f;s.Glow(lip,ReviveFurnace*(2.6f*k),0,2.4f*(1+.5f*a/.16f),Core,0,51);s.Glow(lip,new Vector3(1,.9f,.7f)*(2.2f*k),0,1.1f,Core,0,52);}
            if(a<1.5f)
            {
                int ring=Count(26,q,low);float u=a/1.5f,e=1-(1-u)*(1-u)*(1-u);
                for(int i=0;i<ring;i++){double ang=i*Math.PI*2/ring;var d=right*(float)Math.Cos(ang)+up*(float)Math.Sin(ang);long seed=5100+i*3;var p=lip+forward*(.2f+3.2f*e)+d*(.45f+1.5f*e)+up*(.3f*e);s.Smoke(p,ReviveSteam,.5f*(1-u)*(1-u),.22f+.8f*u,.1f+.8f*u,seed,p.Y>.1f?-1000:.05f);}
                int cone=Count(24,q,low);
                for(int i=0;i<cone;i++){long seed=5400+i*7;var d=Vector3.Normalize(forward+right*((R(seed,1)-.5f)*.85f)+up*((R(seed,2)-.35f)*.7f));float sp=(1.4f+3.4f*R(seed,3))*e;var p=lip+d*sp+up*(.3f*u);s.Smoke(p,ReviveSteam,.42f*(1-u)*(1-u),.14f+.7f*u,.1f+.8f*u,seed,-1000);}
            }
            if(a<.9f)Sparks(s,lip,a,.9f,Count(46,q,low),7.5f,ReviveSpark,5800,9,.022f,forward*5.2f+up*1.4f);
            if(a<3.2f)
            {
                int n=Count(70,q,low);
                for(int i=0;i<n;i++)
                {
                    long seed=5900+i*19;float life=1.8f+1.4f*R(seed,1);if(a>=life)continue;float sp=3+8*R(seed,2),spread=R(seed,3)*6.283f,cone=.16f+.5f*R(seed,4);var d=Vector3.Normalize(forward+(right*(float)Math.Cos(spread)+up*(float)Math.Sin(spread))*cone+up*.3f);
                    // Paper: thrown out fast, then all drag and flutter.
                    float g=(1-(float)Math.Exp(-a*2.6f))/2.6f;var p=lip+d*(sp*g)+up*(-.55f*a*a*.7f)+right*(float)Math.Sin(a*7+i)*.16f*a+forward*(float)Math.Cos(a*5+i*2)*.1f*a;if(p.Y<.03f)p.Y=.03f;
                    float flip=.5f+.5f*(float)Math.Sin(a*14+i*1.7f);s.Sprite(false,p,Confetti[i%Confetti.Length]*(.55f+.75f*flip)*(1-Math.Max(0,a/life-.75f)*4),.022f+.018f*R(seed,6),Soft);
                }
            }
            // The stack's cap goes: a ball of soot up the chimney.
            if(a<1.3f){int n=Count(16,q,low);float u=a/1.3f,e=1-(1-u)*(1-u);for(int i=0;i<n;i++){long seed=4600+i*5;var d=RandomDir(seed,1);var p=stack+up*(2.1f*e)+d*(.25f+.75f*e);s.Smoke(p,ReviveSoot,.62f*(1-u)*(1-u),.2f+.8f*u,.15f+.7f*u,seed,-1000);}s.Glow(stack,ReviveFurnace*(1.4f*(1-u)*(1-u)),0,.8f,Core,0,61);}
            // It breathes out, for a while, after.
            if(t<NativeReviveMachine.Shut+.8f){int n=Count(8,q,low);for(int i=0;i<n;i++){long seed=4300+i*23;float ph=(clock*.7f+i/(float)n)%1;var p=lip+forward*(-.1f+.3f*ph)+up*(.1f+1.1f*ph*ph)+right*((R(seed,1)-.5f)*.9f);s.Smoke(p,ReviveSteam,.2f*(1-ph)*Math.Max(0,1-(t-NativeReviveMachine.Spit)/3.4f),.12f+.4f*ph,.2f+.7f*ph,seed,-1000);}}
            // The door's slam shakes dust off it.
            if(t>=NativeReviveMachine.Shut&&t<NativeReviveMachine.Shut+.5f){float z=(t-NativeReviveMachine.Shut)/.5f;Sparks(s,H(new Vector3(0,NativeReviveMachineModels.MouthMid,1.46f)),z*.5f,.5f,Count(12,q,low),2.6f,ReviveSpark,4100,8,.014f,forward*.6f);}
            if(!hasPilot)return;
            // The pilot's trail: puffs left along the flight, their sparks trailing; a ring of dust at each touch on the mat; the skid's plume; a daze of stars after.
            float flightEnd=NativeReviveMachine.Touch2;
            if(t<flightEnd+1)
            {
                int n=Count(18,q,low);float span=flightEnd-NativeReviveMachine.Spit;
                for(int i=0;i<n;i++)
                {
                    float born=NativeReviveMachine.Spit+span*i/(n-1),old=t-born;if(old<0||old>.95f)continue;if(!NativeReviveMachine.Flying(born,mouth,land,half,out var p,out _,out _))continue;p.Y+=.865f;
                    float u=old/.95f;long seed=3300+i*29;s.Smoke(p+up*(.4f*u)+RandomDir(seed,1)*(.15f*u),ReviveSteam,.44f*(1-u)*(1-u),.12f+.45f*u,.12f+.75f*u,seed,-1000);
                }
            }
            for(int b=0;b<2;b++)
            {
                float at=b==0?NativeReviveMachine.Touch:NativeReviveMachine.Touch2,o=t-at;if(o<0||o>=.9f)continue;float u=o/.9f,e=1-(1-u)*(1-u);if(!NativeReviveMachine.Flying(at+.001f,mouth,land,half,out var p,out _,out _))continue;var foot=new Vector3(p.X,land.Y+.07f,p.Z);
                int n=Count(b==0?20:12,q,low);for(int i=0;i<n;i++){double ang=i*Math.PI*2/n;long seed=2900+b*400+i;var d=right*(float)Math.Cos(ang)+forward*(float)Math.Sin(ang);var pu=foot+d*((b==0?.3f:.2f)+(b==0?1.5f:1)*e)+up*(.05f+.3f*u*R(seed,1));s.Smoke(pu,ReviveDust,.46f*(1-u)*(1-u),.12f+.45f*u,.1f+.8f*u,seed,land.Y+.04f);}
            }
            if(t>=NativeReviveMachine.Touch2&&t<NativeReviveMachine.SkidEnd+.4f&&NativeReviveMachine.Flying(Math.Min(t,NativeReviveMachine.SkidEnd-.001f),mouth,land,half,out var skid,out _,out _))
            {
                float skidding=1-Math.Max(0,(t-NativeReviveMachine.SkidEnd)/.4f);int n=Count(8,q,low);for(int i=0;i<n;i++){long seed=2600+i*31;float ph=(clock*2.6f+i/(float)n)%1;var pu=new Vector3(skid.X,land.Y+.07f,skid.Z)-forward*(.5f*ph)+right*((R(seed,1)-.5f)*.5f)+up*(.3f*ph);s.Smoke(pu,ReviveDust,.3f*skidding*(1-ph),.1f+.3f*ph,.15f+.7f*ph,seed,land.Y+.04f);}
            }
            if(t>=NativeReviveMachine.SkidEnd&&t<NativeReviveMachine.UpAt+1.1f)
            {
                float fade=Math.Min(1,(NativeReviveMachine.UpAt+1.1f-t)/.4f),rise=NativeReviveMachine.Flying(Math.Min(t,NativeReviveMachine.UpAt-.01f),mouth,land,half,out var rest,out _,out _)?1:0;if(rise==0)rest=land;
                float headY=t<NativeReviveMachine.RollEnd?land.Y+.42f:land.Y+.42f+1.46f*Math.Min(1,(t-NativeReviveMachine.RollEnd)/(NativeReviveMachine.UpAt-NativeReviveMachine.RollEnd));var head=new Vector3(rest.X,headY,rest.Z);
                for(int i=0;i<5;i++){double ang=clock*3.1+i*Math.PI*2/5;var p=head+right*(float)(Math.Cos(ang)*.26f)+forward*(float)(Math.Sin(ang)*.26f)+up*(.04f*(float)Math.Sin(clock*6+i));s.Sprite(false,p,new Vector3(1,.86f,.3f)*(2.6f*fade),.036f,Sparkle);}
            }
        }
    }
}
