using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The third set's fire, water, lightning and darkness: effects, previews, sounds and poses.
    public sealed partial class NativeTechniqueFx
    {
        private static readonly string[] MagicBuilt={NativeTechniques.FlameCloak,NativeTechniques.DragonBreath,NativeTechniques.Combust,NativeTechniques.BubbleTrap,NativeTechniques.FlashFreeze,NativeTechniques.HydroJet,NativeTechniques.Magnetize,NativeTechniques.Thunderclap,NativeTechniques.Overload,NativeTechniques.SoulSiphon,NativeTechniques.Dread,NativeTechniques.ShadowPuppet};
        /// <summary>Flash Freeze's sheet, Thunderclap's reach and Dread's cone, as the table has them (a radius, as Healing Spring's "4 m pool" is).</summary>
        public const float FreezeRadius=6,ClapReach=5,DreadReach=6;
        private const float MagicTau=6.2831853f,MagicBodyMid=1.1f,MagicBodyCentre=.865f,MagicBreathReach=7,MagicBreathTan=.268f,MagicBreathHalf=.2618f,MagicDreadHalf=.7854f,MagicJetReach=6;
        private const int MagicShade=0x0a5f7a;
        private static readonly Vector3 MagicCloakTint=new Vector3(1,.36f,.08f),MagicCloakDeep=new Vector3(1,.2f,.02f),MagicBlastGold=new Vector3(1,.62f,.2f),MagicBubbleLit=new Vector3(.62f,.94f,1),MagicBubbleDeep=new Vector3(.3f,.72f,1),MagicIce=new Vector3(.58f,.88f,1),MagicFrost=new Vector3(.86f,.97f,1),MagicIceBody=new Vector3(.62f,.84f,.96f),
            MagicMagnetRose=new Vector3(1,.3f,.44f),MagicMagnetBlue=new Vector3(.3f,.62f,1),MagicClapLight=new Vector3(.86f,.82f,1),MagicLive=new Vector3(.46f,.7f,1),MagicLiveHot=new Vector3(1,.42f,.18f),MagicSoul=new Vector3(.7f,1,.84f),MagicDrain=new Vector3(.75f,.36f,1),MagicDread=new Vector3(.54f,.29f,1),MagicPuppet=new Vector3(.63f,.44f,1);
        private static bool ThirdMagic(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            switch(k.Kind)
            {
                case NativeTechniques.FlameCloak:FlameCloak(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.DragonBreath:DragonBreath(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.Combust:Combust(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.BubbleTrap:BubbleTrap(s,scene,k,t,seed,q,low);return true;
                case NativeTechniques.FlashFreeze:FlashFreeze(s,k,t,seed,q,low);return true;
                case NativeTechniques.HydroJet:HydroJet(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.Magnetize:Magnetize(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.Thunderclap:Thunderclap(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.Overload:Overload(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.SoulSiphon:SoulSiphon(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.Dread:Dread(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.ShadowPuppet:ShadowPuppet(s,k,t,seed,q,low);return true;
                default:return false;
            }
        }
        private static Vector3 MagicFacing(Actor a)=>Flat(N(V3.Forward(a.Yaw)),Vector3.UnitZ);
        private static Vector3 MagicChest(IKitFxScene scene,Actor a)=>scene.Chest(a,out var c)?c:scene.Feet(a)+Up*1.25f;
        private static Vector3 MagicAim(V3 v,Vector3 fallback){var d=N(v);return d.LengthSquared()>1e-5f?Vector3.Normalize(d):fallback;}
        // Two directions square to `dir`, the first flat: whatever spreads about a line.
        private static void MagicFrame(Vector3 dir,out Vector3 side,out Vector3 lift){side=Vector3.Cross(dir,Up);side=side.LengthSquared()>1e-5f?Vector3.Normalize(side):Vector3.UnitX;lift=Vector3.Normalize(Vector3.Cross(side,dir));}
        // A particle of a stream poured from `start` to `stop`: its age and generation, false while the one on its beat falls outside the pour. So a stream starts at the mouth and, when it is stopped, leaves it.
        private static bool MagicBorn(float t,float start,float stop,float life,float phase,out float age,out long gen)
        {
            float x=(t-start)/life+phase;gen=(long)Math.Floor(x);float born=start+(gen-phase)*life;age=t-born;return born>=start&&born<=stop;
        }
        // A wedge on the floor: its two edges and far arc, with a soft fill.
        private static void MagicWedge(ITechniqueFxSink s,Vector3 apex,Vector3 f,float half,float reach,Vector3 color,float edge,float fill,int seg)
        {
            var r=RightOf(f);Vector3 At(float ang,float d)=>apex+(f*(float)Math.Cos(ang)+r*(float)Math.Sin(ang))*d;
            Strip(s,At(-half,.35f),At(-half,reach),.11f,color,edge,edge);Strip(s,At(half,.35f),At(half,reach),.11f,color,edge,edge);
            for(int i=0;i<seg;i++){float a0=-half+2*half*i/seg,a1=-half+2*half*(i+1)/seg;Strip(s,At(a0,reach),At(a1,reach),.11f,color,edge,edge);if(fill>0)s.Band(false,At(a0,.3f),At(a1,.3f),At(a1,reach),At(a0,reach),fill,fill,color);}
        }
        // The share `from` to `to` (0..1) of a flat ring.
        private static void MagicArc(ITechniqueFxSink s,Vector3 c,float radius,float width,float from,float to,Vector3 color,int seg,float b)
        {
            float inner=Math.Max(0,radius-width*.5f),outer=radius+width*.5f;
            for(int i=0;i<seg;i++){float u0=from+(to-from)*i/seg,u1=from+(to-from)*(i+1)/seg;float a0=MagicTau*u0,a1=MagicTau*u1;var d0=new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0));var d1=new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1));s.Band(false,c+d0*inner,c+d1*inner,c+d1*outer,c+d0*outer,b,b,color);}
        }

        // ---- Fire: Flame Cloak ------------------------------------------------------------------------------------------
        // A ring of fire climbs the body and settles into a mantle: the hem burning on the floor, a cape of flame off the shoulders with its folds rippling, tongues licking up the legs. Anyone who touches it
        // gets a lash of fire back. It gutters and strobes through its last second, so whoever is waiting it out can see when to come in.
        private static void FlameCloak(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var chest=MagicChest(scene,a);var f=MagicFacing(a);var r=RightOf(f);bool view=scene.Viewer==a.Id;float floor=feet.Y-.05f,lit=Math.Max(k.Arm,k.At+.02f);
            if(t<lit)
            {
                float u=Smooth((t-k.At)/Math.Max(.05f,lit-k.At)),h=.05f+1.5f*u;if(view)h=Math.Min(h,.8f);int n=low?6:12;
                for(int i=0;i<n;i++){float ang=MagicTau*i/n+u*2.6f,rad=.66f-.22f*u;var b=feet+new Vector3((float)Math.Cos(ang)*rad,h,(float)Math.Sin(ang)*rad);s.Tongue(false,b-Up*.3f,b+Up*(.6f-.2f*u),Flame*1.7f,.22f);s.Fire(b,MagicCloakTint,.8f,.46f,.1f+.2f*u,seed+i,floor);}
                Ring(s,feet+Up*.05f,.2f+.7f*u,.18f,Flame*(1.4f*u),low?12:22);return;
            }
            float live=t-lit,left=k.Until-t;
            if(left>0)
            {
                float flare=k.Hit(t)&&t-k.HitAt<.35f?1+.8f*(1-(t-k.HitAt)/.35f):1,env=Smooth(live/.18f)*(left<1?.28f+.72f*left*(.7f+.3f*(float)Math.Sin(t*41)):1)*flare;
                // The cape: strands from the shoulders to the floor round the back half of the body, panels between them. Not on your own screen.
                if(!view)
                {
                    int strands=low?3:5,seg=low?4:8;
                    Vector3 C(int j,float u){float ang=(j/(strands-1f)-.5f)*2.7f;var d=-f*(float)Math.Cos(ang)+r*(float)Math.Sin(ang);float ripple=(float)Math.Sin(t*7+u*5+j*1.7f)*.08f*u;return feet+Up*(1.52f-1.44f*u)+d*(.18f+.46f*u+ripple);}
                    for(int j=0;j<strands-1;j++)for(int i=0;i<seg;i++){float u0=i/(float)seg,u1=(i+1)/(float)seg;s.Band(false,C(j,u0),C(j,u1),C(j+1,u1),C(j+1,u0),(1-.5f*u0)*env*.42f,(1-.5f*u1)*env*.42f,MagicCloakDeep);}
                    for(int e=-1;e<=1;e+=2){float ph=(t*1.6f+(e+1)*.31f)%1;s.Fire(feet+Up*(1.4f+.3f*ph)+r*(e*.32f)-f*.08f,MagicCloakTint,(1-ph)*.7f*Math.Min(1,env),.3f-.1f*ph,.08f+.5f*ph,seed+e+90,floor);}
                }
                Ring(s,feet+Up*.05f,.84f+.05f*(float)Math.Sin(t*9),.13f,MagicCloakDeep*(.85f*env),low?14:26);
                // Tongues off the hem, leaning in up the legs.
                int n=Count(16,q,low);for(int i=0;i<n;i++){float ang=MagicTau*(i/(float)n+.3f*R(seed,i+1))+t*.7f,rad=.66f+.1f*R(seed,i+2),fl=.72f+.28f*(float)Math.Sin(t*(10+3*R(seed,i+3))+i*2.1f),h=(.5f+.9f*R(seed,i+4))*fl*Math.Min(1,env)*(view?.42f:1);var dd=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));s.Tongue(false,feet+dd*rad+Up*.02f,feet+dd*(rad*.62f)+Up*h,MagicCloakDeep*(1.05f*env*(view?.6f:1)),.15f);}
                // Fire climbing the body.
                int m=Count(10,q,low);for(int i=0;i<m;i++){float ph=(t*1.25f+R(seed,i+30))%1,ang=MagicTau*R(seed,i+31)+ph*1.4f,rad=.5f-.2f*ph,y=.12f+1.25f*ph;if(view&&y>.7f)continue;s.Fire(feet+new Vector3((float)Math.Cos(ang)*rad,y,(float)Math.Sin(ang)*rad),MagicCloakTint,(1-ph)*.62f*Math.Min(1,env),.42f-.2f*ph,.06f+.55f*ph,seed+i+30,floor);}
                if(!view)Spray(s,chest,t,.7f,low?1:3,2.4f,FlameCore,seed+77,-1.5f,.02f,Up*.7f);
            }
            // A melee hit on it: the lash back along the blow.
            if(k.Hit(t))
            {
                float since=t-k.HitAt;var to=N(k.To);
                if(since<.6f)
                {
                    Flash(s,to,since,.26f,Flame,1.7f,seed+5);Sparks(s,to,since,.5f,Count(26,q,low),6.5f,FlameCore,seed+6,7,.026f);
                    float lash=1-Clamp01(since/.3f);if(lash>0){int n=low?2:4;long roll=seed+(long)(t*24);for(int i=0;i<n;i++){var tip=to+RandomDir(roll,i)*.35f;s.Tongue(false,chest,Vector3.Lerp(chest,tip,OutCubic(since/.09f)),FlameCore*(1.8f*lash),.26f*lash+.05f);}}
                    int b=Count(8,q,low);float u=since/.6f;for(int i=0;i<b;i++)s.Fire(to+RandomDir(seed+8,i)*(.75f*OutCubic(u*1.6f)),MagicCloakTint,(1-u)*.9f,.5f+.4f*u,.1f+.7f*u,seed+i+40,floor);
                }
            }
            // Out: embers and a breath of smoke.
            float after=t-k.Until;if(after>=0){Sparks(s,chest,after,.6f,Count(14,q,low),2.5f,FlameCore,seed+9,2,.02f,Up*.4f);if(!low&&after<.8f){float u=after/.8f;int n=Count(6,q,low);for(int i=0;i<n;i++)s.Smoke(feet+new Vector3((R(seed,i+60)-.5f)*.7f,.5f+1.5f*u*R(seed,i+61)+.3f,(R(seed,i+62)-.5f)*.7f),new Vector3(.14f,.13f,.12f),.32f*(1-u),.5f+.5f*u,.3f+.5f*u,seed+i+60,floor);}}
        }

        // ---- Fire: Dragon Breath ----------------------------------------------------------------------------------------
        // The inhale drags sparks in along the cone and lights the throat. Then a roaring cone, each puff born at the mouth and flying out, so it grows out to its reach, sweeps with the aim, and when
        // stopped leaves the mouth and flies off. Its edges burn on the floor, which is what tells you where to stand instead.
        private static void DragonBreath(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var chest=MagicChest(scene,a);var facing=MagicFacing(a);var dir=MagicAim(k.Dir,facing);var flat=Flat(dir,facing);MagicFrame(dir,out var side,out var lift);bool view=scene.Viewer==a.Id;float floor=feet.Y-.05f,arm=ArmAt(k);
            var mouth=view?chest+dir*.75f-Up*.25f:chest+Up*.34f+flat*.26f;var apex=feet+Up*.04f;
            if(t<arm)
            {
                float u=Clamp01((t-k.At)/Math.Max(.05f,arm-k.At));MagicWedge(s,apex,flat,MagicBreathHalf,Math.Max(.6f,MagicBreathReach*Smooth(u*1.3f)),Flame,.9f*u,.08f*u,low?3:6);
                int n=Count(22,q,low);for(int i=0;i<n;i++){float ph=(u*1.5f+R(seed,i+1))%1,d=3.6f*(1-ph)*(1-ph);var way=Vector3.Normalize(dir+side*((R(seed,i+2)-.5f)*.9f)+lift*((R(seed,i+3)-.5f)*.9f));s.Glow(mouth+way*d,FlameCore*(1.9f*ph),0,.11f,Core,ph,seed+i);}
                if(!view)s.Glow(chest,Flame*(1.1f*u),0,.85f*u+.05f,Core,u,seed);s.Sprite(false,mouth,FlameCore*(2.4f*u),.12f+.24f*u,Hot);Hoop(s,mouth,dir,.6f*(1-u)+.1f,.05f,Flame,low?8:14,1.5f*u);return;
            }
            float stop=k.Recast(t)?k.RecastAt:k.Until,env=Smooth((t-arm)/.1f)*(1-Smooth((t-stop)/.25f));
            if(env>0)MagicWedge(s,apex,flat,MagicBreathHalf,MagicBreathReach,MagicCloakDeep,.9f*env,.1f*env,low?3:6);
            // The cone's body: small and yellow at the mouth, wide, red and sooty at its end.
            const float Life=.5f;int blobs=Count(84,q,low);
            for(int i=0;i<blobs;i++)
            {
                if(!MagicBorn(t,arm,stop,Life,R(seed,i+1),out float age,out long gen))continue;long g=seed+gen*131+i*7;float w=age/Life,d=MagicBreathReach*w*(1.7f-.7f*w),ang=MagicTau*R(g,1),rad=(float)Math.Sqrt(R(g,2));
                var p=mouth+dir*d+(side*(float)Math.Cos(ang)+lift*(float)Math.Sin(ang))*(d*MagicBreathTan*rad)+Up*(.25f*w*w);float near=view?Smooth((d-1)/2.5f):1;
                s.Fire(p,new Vector3(1,.56f-.38f*w,.14f-.11f*w),(1-w*w*w)*.82f*near,(.24f+.86f*w)*(.8f+.4f*R(g,3)),.03f+.8f*w,g,floor);
            }
            // Licks of flame along the cone, for its direction: hot at the mouth, deep further out.
            if(t<stop+Life)
            {
                int licks=low?4:10;long roll=seed+(long)(t*18);float gone=t>stop?MagicBreathReach*(t-stop)/Life:0,grow=Smooth((t-arm)/.14f);
                for(int i=0;i<licks;i++)
                {
                    float x=R(roll,i+30),d0=Math.Max(gone,MagicBreathReach*.7f*x),len=(1.5f+1.7f*R(roll,i+31))*grow;if(view&&d0<1.2f)continue;float spread=MagicBreathTan*(R(roll,i+32)*2-1),spread2=MagicBreathTan*.7f*(R(roll,i+33)*2-1);var way=Vector3.Normalize(dir+side*spread+lift*spread2);if(d0>=MagicBreathReach-.2f)continue;float d1=Math.Min(MagicBreathReach,d0+len);
                    s.Tongue(false,mouth+way*d0,mouth+way*d1,Vector3.Lerp(Flame,MagicCloakDeep,x)*(1.05f-.2f*x),.2f+.3f*x);
                }
            }
            // The hot jet at the mouth.
            if(t<stop)
            {
                if(!view){int n=low?2:4;long roll=seed+(long)(t*24);for(int i=0;i<n;i++){var d=Vector3.Normalize(dir+side*((R(roll,i)-.5f)*.22f)+lift*((R(roll,i+9)-.5f)*.22f));s.Tongue(false,mouth,mouth+d*((1.3f+1.1f*R(roll,i+20))*Smooth((t-arm)/.08f)),FlameCore*.9f,.2f);}}
                s.Sprite(false,mouth,FlameCore*(view?1:1.5f),view?.2f:.3f,Hot);
            }
            // Sparks thrown out past it.
            int sparks=Count(28,q,low);for(int i=0;i<sparks;i++){if(!MagicBorn(t,arm,stop,.42f,R(seed,i+200),out float age,out long gen))continue;long g=seed+gen*97+i*11;float w=age/.42f;var v=Vector3.Normalize(dir+side*((R(g,1)-.5f)*.6f)+lift*((R(g,2)-.5f)*.6f))*(15+7*R(g,3));s.Streak(mouth+v*age+Up*(.6f*age),v,FlameCore*(2*(1-w)),.026f,.08f,.018f,.8f,w,g);}
            // Smoke off the far end, and off the mouth when it stops.
            if(!low){int n=Count(10,q,low);for(int i=0;i<n;i++){if(!MagicBorn(t,arm+.2f,stop,1.1f,R(seed,i+300),out float age,out long gen))continue;long g=seed+gen*53+i*3;float w=age/1.1f,d=MagicBreathReach*(.6f+.45f*R(g,1));var p=mouth+dir*d+side*((R(g,2)-.5f)*d*MagicBreathTan*1.4f)+Up*(.3f+1.5f*w);s.Smoke(p,new Vector3(.11f,.1f,.09f),.4f*(1-w),.9f+1.2f*w,.25f+.5f*w,g,floor);}}
            float after=t-stop;if(after>=0&&after<.6f){Sparks(s,mouth,after,.5f,Count(10,q,low),3,FlameCore,seed+7,3,.02f,dir*.8f);if(!low&&!view){float u=after/.6f;s.Smoke(mouth+Up*(.5f*u),new Vector3(.14f,.12f,.11f),.4f*(1-u),.35f+.4f*u,.3f+.5f*u,seed+8,floor);}}
        }

        // ---- Fire: Combust ----------------------------------------------------------------------------------------------
        // A snap: one bright spark at the fingers. Then a flash of light to each burning enemy and each goes up where they stand, in gold: a white heart, a round ball of fire with spikes through it and
        // the blast's 2 m ring on the floor. Path holds the victims' body centres, and only from the blow on: nobody knows who is burning until it goes.
        private static void Combust(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var f=MagicFacing(a);var hand=MagicChest(scene,a)+f*.42f+RightOf(f)*.14f+Up*.12f;var path=k.Path;int count=path==null||t<k.Release?0:Math.Min(6,path.Length);float blow=ArmAt(k);
            float snap=t-k.At;Flash(s,hand,snap,.2f,MagicBlastGold,.7f,seed);Sparks(s,hand,snap,.32f,low?5:10,3,FlameCore,seed+1,6,.018f);if(snap<.14f)s.Sprite(false,hand,(FlameCore+Vector3.One)*(3.4f*(1-snap/.14f)),.34f,Sparkle);
            for(int i=0;i<count;i++)
            {
                var p=N(path[i])-Up*MagicBodyCentre;var c=p+Up*MagicBodyMid;long sd=seed+100*(i+1);float floor=p.Y-.05f,age=t-blow;if(age<0||age>.9f)continue;
                if(age<.12f){float b=1-age/.12f;s.Line(false,hand,c,MagicBlastGold*(1.8f*b),.05f*b,false);s.Sprite(false,c,Vector3.One*(4*b),1.9f,Hot);}
                Flash(s,c,age,.24f,MagicBlastGold,3.4f,sd);
                // The ball.
                int n=Count(14,q,low);float life=.62f,uf=Clamp01(age/life);if(age<life)for(int j=0;j<n;j++){var d=RandomDir(sd,j);var at=c+d*(1.75f*OutCubic(age/.3f)*(.4f+.6f*R(sd,j+40)));if(at.Y<floor+.1f)at.Y=floor+.1f;s.Fire(at,new Vector3(1,.5f-.2f*uf,.1f),(1-uf*uf)*.95f,(.75f+.85f*uf)*(.8f+.5f*R(sd,j+41)),.03f+.8f*uf,sd+j,floor);}
                // The spikes.
                if(age<.2f){float b=1-age/.2f;int m=low?4:8;for(int j=0;j<m;j++){var d=RandomDir(sd,j+70);s.Tongue(false,c+d*.3f,c+d*(2.5f*OutCubic(age/.1f)),FlameCore*(2*b),.3f*b+.04f);}}
                if(age<.42f){float u=age/.42f;Ring(s,p+Up*.05f,2*OutCubic(age/.18f),.36f*(1-u)+.06f,MagicBlastGold*(1.7f*(1-u)),low?14:26);}
                Sparks(s,c,age,.75f,Count(30,q,low),9,FlameCore,sd+3,8,.028f,Up*.4f);
                if(!low&&age>.12f){float u=(age-.12f)/.78f;int m=Count(8,q,low);for(int j=0;j<m;j++){var d=RandomDir(sd,j+90);s.Smoke(c+d*(1.1f+.5f*u)+Up*(.9f*u),new Vector3(.11f,.1f,.09f),.5f*(1-u),.8f+.8f*u,.25f+.6f*u,sd+j+90,floor);}}
            }
        }

        // ---- Water: Bubble Trap -----------------------------------------------------------------------------------------
        // A wobbling ball of water trailing drops, slow enough to read. A catch closes a bubble round the body: a bright camera-facing rim, a thin film, faint hoops turning, the two highlights of a soap bubble,
        // little bubbles rising, water running down it. It shivers before it goes; the pop is the whole shell of it thrown out as drops. A miss bursts where it ended.
        private static void BubbleTrap(ITechniqueFxSink s,IKitFxScene scene,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var point=N(k.Point);float none=from.Y-6;
            if(t<k.Release)
            {
                float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));int n=low?4:8;for(int i=0;i<n;i++){float ang=MagicTau*i/n+u*5;var o=new Vector3((float)Math.Cos(ang),(float)Math.Sin(ang*1.3f)*.5f,(float)Math.Sin(ang))*(.5f*(1-u));s.Water(from+o,MagicBubbleLit,MagicShade,.85f,.16f+.08f*u,o*.3f+Up*.02f,.15f,seed+i,none);}
                s.Sprite(false,from,MagicBubbleLit*(1.4f*u),.14f+.2f*u,RingSprite);return;
            }
            bool caught=k.Hit(t),popped=k.Recast(t);
            if(!caught&&!popped)
            {
                // Only the preview knows a landing ahead of time, and flies the orb there itself; live, Point is the orb now.
                float arrive=k.HitAt>-50?k.HitAt:k.RecastAt>-50?k.RecastAt:-100;var orb=arrive>t?Vector3.Lerp(from,point,Clamp01((t-k.Release)/Math.Max(.05f,arrive-k.Release))):point;var d=point-from;d=d.LengthSquared()>1e-5f?Vector3.Normalize(d):Vector3.UnitZ;MagicFrame(d,out var side,out var lift);
                float wob=(float)Math.Sin(t*17);s.Glow(orb,MagicBubbleDeep*.7f,0,1.1f,Core,0,seed+1);s.Water(orb,MagicBubbleLit,MagicShade,.95f,.62f+.05f*wob,d*.06f+Up*(.04f*wob),.1f,seed,none);s.Sprite(false,orb,MagicBubbleLit*1.8f,.46f-.03f*wob,RingSprite);s.Sprite(false,orb+Up*.14f-side*.1f,Vector3.One*2.2f,.09f,Hot);
                int n=Count(10,q,low);float reach=Math.Min(Vector3.Distance(orb,from),1.9f);for(int i=0;i<n;i++){float ph=(t*2.4f+R(seed,i+1))%1;var p=orb-d*(ph*reach)+(side*(R(seed,i+2)-.5f)+lift*(R(seed,i+3)-.5f))*(.42f*ph);s.Water(p,MagicBubbleLit,MagicShade,.6f*(1-ph),.26f*(1-.5f*ph),d*.1f+Up*.01f,.2f+.5f*ph,seed+i,none);}
                int m=Count(8,q,low);for(int i=0;i<m;i++){float ph=(t*1.8f+R(seed,i+20))%1;var p=orb-d*(ph*reach*.8f)-Up*(.9f*ph*ph)+side*((R(seed,i+21)-.5f)*.5f);s.Drop(p,-Up*(.1f+.2f*ph),MagicBubbleLit*1.2f,.85f*(1-ph),.04f,ph,seed+i);}
                return;
            }
            float popAt=popped?k.RecastAt:k.Until;
            var who=caught?ActorOf(scene,k.Target):null;var c=who!=null?MagicChest(scene,who)-Up*.2f:point;const float Rad=1.18f;
            if(caught&&t<popAt)
            {
                float since=t-k.HitAt,left=popAt-t,g=Smooth(since/.12f)*(1+.16f*(float)Math.Exp(-since*9)*(float)Math.Sin(since*24)),shiver=left<.35f?1+.05f*(float)Math.Sin(t*70)*(1-left/.35f):1,rad=Rad*g*shiver*(1+.025f*(float)Math.Sin(t*6));
                if(since<.3f){Flash(s,c,since,.2f,MagicBubbleLit,1.2f,seed+2);Sparks(s,c,since,.3f,Count(14,q,low),4,MagicBubbleLit,seed+3,8,.02f);}
                // The shell: a soft body with a bright edge, a crisp line on it, and water slopping in the bottom of the bowl.
                float warn=left<.35f?1:0;s.Glow(c,MagicBubbleDeep*(.85f+.3f*warn),0,rad*2.05f,RingGlow,.12f,seed+4);s.Sprite(false,c,MagicBubbleLit*(1.9f+warn),rad,RingSprite);s.Sprite(false,c,MagicBubbleLit*.9f,rad*.965f,RingSprite);
                int slop=Count(14,q,low);for(int i=0;i<slop;i++){float ang=MagicTau*i/slop+t*.5f,lat=-.55f-.55f*R(seed,i+150)+.05f*(float)Math.Sin(t*3+i);var n0=new Vector3((float)Math.Cos(ang)*(float)Math.Cos(lat),(float)Math.Sin(lat),(float)Math.Sin(ang)*(float)Math.Cos(lat));s.Water(c+n0*(rad*.93f),MagicBubbleLit,MagicShade,.62f,.5f+.2f*R(seed,i+151),Up*.02f,.12f,seed+i+150,none);}
                int seg=low?12:24;Hoop(s,c+Up*(rad*.08f*(float)Math.Sin(t*2)),Up,rad*.98f,.16f,MagicBubbleLit,seg,.42f);
                if(!low){float turn=t*.9f;Hoop(s,c,new Vector3((float)Math.Cos(turn),0,(float)Math.Sin(turn)),rad*.97f,.11f,MagicBubbleLit,seg,.3f);}
                var side2=new Vector3(.71f,0,-.71f);s.Sprite(false,c+Up*(rad*.58f)-side2*(rad*.36f),Vector3.One*2.4f,.24f,Sparkle);s.Sprite(false,c+Up*(rad*.66f)-side2*(rad*.16f),Vector3.One*1.3f,.09f,Hot);s.Sprite(false,c-Up*(rad*.5f)+side2*(rad*.48f),MagicBubbleLit*1.4f,.11f,Hot);
                int n=Count(12,q,low);for(int i=0;i<n;i++){float ph=(t*.72f+R(seed,i+30))%1,ang=MagicTau*R(seed,i+31)+ph,rr=rad*.72f*(float)Math.Sqrt(R(seed,i+32));var p=c+new Vector3((float)Math.Cos(ang)*rr,rad*.8f*(ph*2-1),(float)Math.Sin(ang)*rr);s.Sprite(false,p,MagicBubbleLit*(1.3f*(float)Math.Sin(3.14159f*ph)),.05f+.06f*R(seed,i+33),RingSprite);}
                int d=Count(10,q,low);for(int i=0;i<d;i++){float ph=(t*.55f+R(seed,i+50))%1,ang=MagicTau*R(seed,i+51),lat=1.45f-2.9f*ph;var n0=new Vector3((float)Math.Cos(ang)*(float)Math.Cos(lat),(float)Math.Sin(lat),(float)Math.Sin(ang)*(float)Math.Cos(lat));s.Drop(c+n0*rad,-Up*.1f,MagicBubbleLit*1.3f,.8f*(float)Math.Sin(3.14159f*ph),.035f,ph,seed+i);}
                return;
            }
            // The pop.
            float pop=t-popAt,bigness=caught?1:.55f;if(pop>.9f)return;
            if(pop<.2f){float u=pop/.2f;s.Sprite(false,c,MagicBubbleLit*(2.4f*(1-u)),Rad*bigness*(1+.7f*u),RingSprite);}
            Flash(s,c,pop,.16f,MagicBubbleLit,1.1f*bigness,seed+6);
            int dn=Count(46,q,low);float life=.7f,ud=Clamp01(pop/life);if(pop<life)for(int i=0;i<dn;i++){var d=RandomDir(seed,i+70);var v=d*((3.2f+3.2f*R(seed,i+71))*bigness)+Up*.8f;var p=c+d*(Rad*bigness)+v*pop-Up*(4.9f*pop*pop);s.Drop(p,v*.045f-Up*(.44f*pop),MagicBubbleLit*1.25f,.92f*(1-ud),.055f,ud,seed+i);}
            int w=Count(12,q,low);if(pop<.42f){float u=pop/.42f;for(int i=0;i<w;i++){var d=RandomDir(seed,i+130);s.Water(c+d*(Rad*bigness*(.9f+.9f*u)),MagicBubbleLit,MagicShade,.7f*(1-u),.36f*bigness+.14f*u,d*.2f,.3f+.6f*u,seed+i,none);}}
        }

        // ---- Water: Flash Freeze ----------------------------------------------------------------------------------------
        // A great snowflake etched across the floor in the instant of the stamp: six arms with their barbs running out to the rim, a pale tint and low fog between them, crystals standing on it and a crown of ice
        // all round the edge so its reach is plain, glints and a cold breath. Near its end it dulls and melts down; the last of it cracks up in flying shards.
        private static void FlashFreeze(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var c=N(k.Point);
            if(t<k.Release){float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));Ring(s,c+Up*.05f,1.5f*(1-u)+.2f,.08f,MagicFrost,low?10:18,1.5f*u);int n=low?5:10;for(int i=0;i<n;i++){float ang=MagicTau*i/n+u;var p=c+new Vector3((float)Math.Cos(ang),.04f,(float)Math.Sin(ang))*(1.8f*(1-u));s.Glow(p+Up*.1f,MagicFrost*(1.6f*u),0,.1f,Core,u,seed+i);}return;}
            float age=t-k.Release,life=k.Until-k.Release;
            float front=OutCubic(age/.28f),rim=FreezeRadius*front,melt=1-Smooth((age-(life-.8f))/.8f);
            if(age<life)
            {
                // The sheet: a pale tint in three overlapping rings so it fills evenly, and low cold fog lying on it.
                int seg=low?22:44;float tint=.2f*melt;Ring(s,c+Up*.03f,rim*.22f,rim*.44f,MagicIce*tint,seg);Ring(s,c+Up*.03f,rim*.56f,rim*.44f,MagicIce*tint,seg);Ring(s,c+Up*.03f,rim*.84f,rim*.4f,MagicIce*tint,seg);Ring(s,c+Up*.06f,rim,.34f,MagicIce*(.6f*melt),seg);
                int fog=Count(26,q,low);for(int i=0;i<fog;i++){float ang=MagicTau*R(seed,i+1)+t*.06f*(R(seed,i+4)-.5f),rad=FreezeRadius*(float)Math.Sqrt(R(seed,i+2));float g=Clamp01((rim-rad)/.9f);if(g<=0)continue;s.Glow(c+new Vector3((float)Math.Cos(ang)*rad,.14f,(float)Math.Sin(ang)*rad),MagicIceBody*.8f,.2f*g*melt,1.7f+.9f*R(seed,i+3),Mote,0,seed+i);}
                // The snowflake etched in the floor: bright as it runs, then settled. Soft strips, since a hard line this long goes across the whole screen from a low camera.
                float glow=(1+1.4f*(1-Smooth(age/.5f)))*melt,turn=R(seed,9)*1.05f;var lift=Up*.07f;
                for(int j=0;j<6;j++)
                {
                    float ang=turn+j*MagicTau/6;var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));var sd=RightOf(d);Strip(s,c+lift,c+d*(rim*.97f)+lift,.09f,MagicIce,.85f*glow,.45f*glow);
                    int barbs=low?2:4;for(int b=0;b<barbs;b++){float x=.26f+.62f*b/Math.Max(1,barbs-1),at=FreezeRadius*x;if(at>rim)break;float len=FreezeRadius*(.26f-.13f*x)*Clamp01((rim-at)/1.2f);var o=c+d*at+lift;for(int e=-1;e<=1;e+=2)Strip(s,o,o+(d*.5f+sd*(e*.866f))*len,.06f,MagicIce,.75f*glow,.3f*glow);}
                }
                // The inner hexagon, and fine cracks between the arms.
                float hex=FreezeRadius*.3f;if(rim>hex)for(int j=0;j<6;j++){float a0=turn+j*MagicTau/6,a1=turn+(j+1)*MagicTau/6;Strip(s,c+new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*hex+lift,c+new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1))*hex+lift,.06f,MagicIce,.7f*glow,.7f*glow);}
                if(!low){int n=Count(22,q,low);for(int i=0;i<n;i++){float ang=MagicTau*R(seed,i+100),rad=FreezeRadius*(.25f+.72f*(float)Math.Sqrt(R(seed,i+101)));if(rad>rim)continue;float len=.5f+.9f*R(seed,i+102),turn2=ang+1.6f*(R(seed,i+103)-.5f)+.8f;var o=c+new Vector3((float)Math.Cos(ang)*rad,.065f,(float)Math.Sin(ang)*rad);Strip(s,o,o+new Vector3((float)Math.Cos(turn2),0,(float)Math.Sin(turn2))*len,.03f,MagicIce,.6f*melt,.2f*melt);}}
                // The crown of ice round the rim, shooting up behind the front and melting at the end: what stands up reads from any camera.
                int spikes=Count(46,q,low);for(int i=0;i<spikes;i++){float ang=MagicTau*(i+.6f*R(seed,i+150))/spikes;float grow=OutCubic((age-.22f-.14f*R(seed,i+151))/.12f);if(grow<=0)continue;float h=(.38f+.95f*R(seed,i+152))*grow*melt,w=.16f+.14f*R(seed,i+155);var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));var b=c+d*(FreezeRadius*(.97f+.05f*R(seed,i+153)));var tip=b+Up*h+d*(.08f+.2f*R(seed,i+154));s.Line(false,b,tip,MagicIce*(.95f*melt),w,true);if(!low&&i%2==0)s.Line(false,b,b+(tip-b)*.74f,MagicFrost*(1.3f*melt),w*.36f,true);}
                // Smaller crystals standing on the sheet, behind the front.
                int stubs=Count(26,q,low);for(int i=0;i<stubs;i++){float ang=MagicTau*R(seed,i+170),rad=FreezeRadius*(.16f+.78f*(float)Math.Sqrt(R(seed,i+171)));float g=Clamp01((rim-rad)/.5f);if(g<=0)continue;var b=c+new Vector3((float)Math.Cos(ang)*rad,.03f,(float)Math.Sin(ang)*rad);float h=(.14f+.3f*R(seed,i+172))*g*melt;s.Line(false,b,b+Up*h+new Vector3(R(seed,i+173)-.5f,0,R(seed,i+174)-.5f)*(.3f*h),MagicIce*(.9f*melt),.08f+.07f*R(seed,i+175),true);}
                // Glints, and the cold lifting off it.
                int gl=Count(26,q,low);for(int i=0;i<gl;i++){float ang=MagicTau*R(seed,i+200),rad=FreezeRadius*(float)Math.Sqrt(R(seed,i+201));if(rad>rim)continue;float tw=(float)Math.Pow(Math.Abs(Math.Sin(t*(1.5f+R(seed,i+202))+i)),10);s.Sprite(false,c+new Vector3((float)Math.Cos(ang)*rad,.1f,(float)Math.Sin(ang)*rad),Vector3.One*(2.2f*tw*melt),.12f+.1f*R(seed,i+203),Sparkle);}
                if(!low){int n=Count(12,q,low);for(int i=0;i<n;i++){float ph=(t*.3f+R(seed,i+240))%1,ang=MagicTau*R(seed,i+241),rad=FreezeRadius*.95f*(float)Math.Sqrt(R(seed,i+242));if(rad>rim)continue;s.Glow(c+new Vector3((float)Math.Cos(ang)*rad,.2f+.9f*ph,(float)Math.Sin(ang)*rad),MagicFrost*.7f,.14f*(float)Math.Sin(3.14159f*ph)*melt,.9f+.7f*ph,Mote,ph,seed+i+240);}}
            }
            // The stamp itself: a flash, the front running out white, shards kicked up.
            Flash(s,c+Up*.3f,age,.2f,MagicIce,2.6f,seed+4);if(age<.28f)Ring(s,c+Up*.08f,rim,.4f,MagicFrost,low?22:44,2.4f*(1-age/.28f));Sparks(s,c+Up*.2f,age,.5f,Count(24,q,low),6,MagicFrost,seed+5,9,.024f,Up*.6f);
            // Its end: the sheet cracks up.
            float after=t-k.Until;if(after>=0&&after<.8f){float u=after/.8f;int n=Count(44,q,low);for(int i=0;i<n;i++){float ang=MagicTau*R(seed,i+300),rad=FreezeRadius*(float)Math.Sqrt(R(seed,i+301));var v=new Vector3((R(seed,i+302)-.5f)*1.6f,2.4f+2.4f*R(seed,i+303),(R(seed,i+304)-.5f)*1.6f);var p=c+new Vector3((float)Math.Cos(ang)*rad,.05f,(float)Math.Sin(ang)*rad)+v*after-Up*(4.9f*after*after);if(p.Y<c.Y)continue;s.Line(false,p,p+Vector3.Normalize(v)*.14f,MagicFrost*(1.5f*(1-u)),.05f,true);}}
        }

        // ---- Water: Hydro Jet -------------------------------------------------------------------------------------------
        // Water wound up round the hands, then a hard hose of it: a white core, a rope of toon water poured down it, pressure rings running along, foam at its head and drops flung off the sides, all of it
        // straight and narrow where the dragon's breath spreads. Let go, the stream leaves the hands and falls apart.
        private static void HydroJet(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var chest=MagicChest(scene,a);var facing=MagicFacing(a);var dir=MagicAim(k.Dir,facing);MagicFrame(dir,out var side,out var lift);bool view=scene.Viewer==a.Id;var o=view?chest+dir*.75f-Up*.25f:chest+dir*.5f;float none=feet.Y-8;
            if(t<k.Release)
            {
                float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));int n=low?5:10;for(int i=0;i<n;i++){float ang=MagicTau*i/n+u*7;var p=o+(side*(float)Math.Cos(ang)+lift*(float)Math.Sin(ang))*(.62f*(1-u)+.06f);s.Water(p,MagicBubbleLit,MagicShade,.85f,.14f+.1f*u,Up*.02f,.15f,seed+i,none);}
                Hoop(s,o,dir,.5f*(1-u)+.12f,.04f,MagicBubbleLit,low?8:14,1.4f*u);return;
            }
            float stop=k.Recast(t)?k.RecastAt:k.Until,since=t-k.Release,past=t-stop,run=OutCubic(since/.18f);const float Life=.3f;
            float head=run,tail=past>0?Clamp01(past/Life):0,env=1-Smooth((past-Life*.7f)/.2f);
            // Along the stream (0..1): it sags a little as it goes.
            Vector3 On(float w)=>o+dir*(MagicJetReach*w)-Up*(.5f*w*w);
            // The core, the length of what is still in the air: a deep body round a bright thread.
            if(env>0&&head>tail)
            {
                int seg=low?3:6;for(int i=0;i<seg;i++){float w0=tail+(head-tail)*i/seg,w1=tail+(head-tail)*(i+1)/seg;if(view&&w1<.2f)continue;s.Line(false,On(w0),On(w1),MagicBubbleDeep*(.75f*env),.46f+.3f*w1,false);s.Line(false,On(w0),On(w1),MagicBubbleLit*(.8f*env),.15f+.08f*w1,false);}
            }
            int blobs=Count(92,q,low);
            for(int i=0;i<blobs;i++)
            {
                if(!MagicBorn(t,k.Release,stop,Life,R(seed,i+1),out float age,out long gen))continue;long g=seed+gen*131+i*7;float w=age/Life,d=MagicJetReach*w,ang=MagicTau*R(g,1),rad=(.14f+.5f*w*w)*(float)Math.Sqrt(R(g,2));float near=view?Smooth((d-.9f)/1.5f):1;
                var p=On(w)+(side*(float)Math.Cos(ang)+lift*(float)Math.Sin(ang))*rad;s.Water(p,i%3==0?MagicBubbleLit:MagicBubbleDeep,MagicShade,.95f*(1-w*w*w)*near,.36f+.5f*w,dir*.55f,.08f+.42f*w,g,none);
            }
            // Pressure rings along it.
            int rings=low?2:5;for(int i=0;i<rings;i++){if(!MagicBorn(t,k.Release,stop,Life,(i+.5f)/rings,out float age,out _))continue;float w=age/Life;Hoop(s,On(w),dir,.28f+.42f*w,.13f,MagicBubbleLit,low?8:14,.6f*(1-w));}
            // Foam at its head, spread wide.
            if(past<Life&&run>0){int n=Count(18,q,low);var h=On(head);long roll=seed+(long)(t*16);for(int i=0;i<n;i++){var d=RandomDir(roll,i);s.Water(h+d*(.25f+.8f*R(roll,i+30)),MagicBubbleLit,MagicShade,.85f*(1-Clamp01(past/Life)),.36f+.42f*R(roll,i+31),d*.2f,.3f+.4f*R(roll,i+32),roll+i,none);}}
            // Drops flung off, falling.
            int dr=Count(40,q,low);const float DropLife=.62f;for(int i=0;i<dr;i++){if(!MagicBorn(t,k.Release,stop,DropLife,R(seed,i+60),out float age,out long gen))continue;long g=seed+gen*71+i*5;float w=age/DropLife;var v=dir*(6*R(g,2))+side*((R(g,3)-.5f)*5)+lift*((R(g,4)-.5f)*2.4f+.8f);var p=On(.15f+.85f*R(g,1))+v*age-Up*(4.9f*age*age);s.Drop(p,(v-Up*(9.8f*age))*.04f,MagicBubbleLit*1.25f,.9f*(1-w),.05f,w,g);}
            // The kick at the hands, and spray thrown back round the body.
            if(past<0)
            {
                s.Sprite(false,o,(MagicBubbleLit+Vector3.One*.3f)*(view?1:2),view?.26f:.42f,Hot);float kick=(t*7)%1;Hoop(s,o,dir,.2f+.5f*kick,.12f,MagicBubbleLit,low?8:14,(view?.3f:.7f)*(1-kick));
                if(!view){int n=Count(10,q,low);for(int i=0;i<n;i++){if(!MagicBorn(t,k.Release,stop,.34f,R(seed,i+110),out float age,out long gen))continue;long g=seed+gen*43+i;var v=-dir*(4+3*R(g,1))+side*((R(g,2)-.5f)*3.4f)+Up*((R(g,3)-.5f)*2.6f);s.Drop(o+v*age,v*.04f,MagicBubbleLit*1.2f,.85f*(1-age/.34f),.04f,age/.34f,g);}}
            }
            else if(past<.4f)Sparks(s,o,past,.4f,Count(10,q,low),3,MagicBubbleLit,seed+7,9,.02f,dir*.5f);
        }

        // ---- Lightning: Magnetize ---------------------------------------------------------------------------------------
        // Rose on one, blue on the other: a ring on the floor, a hoop at the waist and a broken crown over the head, with a magnet's field lines bowing from one to the other in a fan and light running
        // down them to the meeting place, which spins a reticle and throws chevrons along the floor toward itself. The clash is both colours at once. Path and Point hold floor points (the feet, the spot).
        private static void Magnetize(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var hand=MagicChest(scene,a)+MagicFacing(a)*.42f;var point=N(k.Point);var path=k.Path;int m=path==null?0:path.Length;var A=m>0?N(path[0]):point;var B=m>1?N(path[1]):point;bool two=m>1,hit=k.Hit(t);
            if(t<k.Release){float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Sprite(false,hand-Vector3.UnitX*.1f,MagicMagnetRose*(2.2f*u),.16f,Hot);s.Sprite(false,hand+Vector3.UnitX*.1f,MagicMagnetBlue*(2.2f*u),.16f,Hot);Bolt(s,hand-Vector3.UnitX*.1f,hand+Vector3.UnitX*.1f,MagicClapLight*1.4f,.012f,.2f,3,seed+(long)(t*24));return;}
            float age=t-k.Release;var mid=Up*MagicBodyMid;
            // Only the preview knows the clash ahead of time, and it moves nobody: there the marks run in by themselves. Live, Path is where each one is now.
            if(k.HitAt>t&&t>k.Arm){float g=Clamp01((t-k.Arm)/Math.Max(.05f,k.HitAt-k.Arm));g*=g;A=Vector3.Lerp(A,point,g);B=Vector3.Lerp(B,point,g);}
            if(age<.16f){float b=1-age/.16f;long roll=seed+(long)(t*40);Bolt(s,hand,A+mid,(MagicMagnetRose+Vector3.One*.3f)*(2*b),.024f,.6f,low?5:9,roll);if(two)Bolt(s,hand,B+mid,(MagicMagnetBlue+Vector3.One*.3f)*(2*b),.024f,.6f,low?5:9,roll+1);}
            if(!hit&&t<k.Until)
            {
                float env=Smooth(age/.12f)*(1-Smooth((t-(k.Until-.12f))/.12f));var ca=A+mid;var cb=two?B+mid:point+mid;var span=cb-ca;float L=span.Length();bool pulling=t>=k.Arm;var colorB=two?MagicMagnetBlue:MagicClapLight;
                // The field: a fan of bowed lines over the two, light running in from both ends.
                if(L>.1f)
                {
                    MagicFrame(span/L,out var side,out var lift);int lines=low?3:7,seg=low?8:16;float bow=Math.Min(3.2f,L*.34f),speed=pulling?9:5;
                    for(int j=0;j<lines;j++)
                    {
                        float th=3.14159f*j/(lines-1f);var off=(side*(float)Math.Cos(th)+lift*(float)Math.Sin(th))*(bow*(.72f+.28f*(float)Math.Sin(th)));
                        Vector3 P(float w)=>ca+span*w+off*(4*w*(1-w));
                        for(int i=0;i<seg;i++){float u0=i/(float)seg,u1=(i+1)/(float)seg,w=(u0+u1)*.5f,run=Math.Abs(w-.5f)*L*1.6f+t*speed,flow=(float)Math.Pow(Math.Abs(Math.Sin(run+j)),8);s.Line(false,P(u0),P(u1),Vector3.Lerp(MagicMagnetRose,colorB,w)*((.62f+1.1f*flow)*env),.055f,false);}
                    }
                }
                // The marks on each.
                void Mark(Vector3 at,Vector3 color,float spin){Ring(s,at+Up*.05f,.82f+.04f*(float)Math.Sin(t*14),.16f,color,low?12:22,1.15f*env);Hoop(s,at+mid,Up,.62f,.13f,color,low?10:18,.85f*env);BrokenRing(s,at+Up*2.12f,.32f,spin,3,color*(1.15f*env),.06f);s.Line(false,at+Up*.05f,at+Up*2.1f,color*(.42f*env),.06f,false);}
                Mark(A,MagicMagnetRose,t*5);if(two)Mark(B,MagicMagnetBlue,-t*5);
                // The meeting place.
                BrokenRing(s,point+Up*.06f,.78f,-t*4,4,MagicClapLight*(1.3f*env),.08f);Ring(s,point+Up*.05f,.3f,.1f,MagicClapLight,low?10:16,1.2f*env);s.Line(false,point+Up*.05f,point+Up*(1.2f+.2f*(float)Math.Sin(t*19)),MagicClapLight*(.8f*env),.06f,false);
                // Chevrons running in along the floor.
                for(int e=0;e<(two?2:1);e++)
                {
                    var from=e==0?A:B;var color=e==0?MagicMagnetRose:MagicMagnetBlue;var w=point-from;w.Y=0;float len=w.Length();if(len<.6f)continue;var along=w/len;var sd=RightOf(along);int ch=low?2:4;float b0=pulling?1.9f:.8f;
                    for(int c=0;c<ch;c++){float x=(t*(pulling?2.6f:1.1f)+c/(float)ch)%1;var p=from+along*(x*len)+Up*.07f;float fade=env*b0*(float)Math.Sin(3.14159f*x);Strip(s,p-along*.42f+sd*.38f,p,.06f,color,fade,fade);Strip(s,p-along*.42f-sd*.38f,p,.06f,color,fade,fade);}
                }
            }
            if(hit)
            {
                float since=t-k.HitAt;var c=point+mid;long roll=seed+(long)(t*20);
                Flash(s,c,since,.3f,MagicClapLight,3.2f,seed+1);Flash(s,c+Vector3.UnitX*.3f,since,.2f,MagicMagnetRose,2.2f,seed+2);Flash(s,c-Vector3.UnitX*.3f,since,.2f,MagicMagnetBlue,2.2f,seed+3);
                if(since<.42f){float b=1-since/.42f;int n=low?4:9;for(int i=0;i<n;i++){var d=RandomDir(roll,i);d.Y=Math.Abs(d.Y)*.6f+.05f;Bolt(s,c,c+Vector3.Normalize(d)*(1.1f+2.4f*R(roll,i+30)),(i%2==0?MagicMagnetRose:MagicMagnetBlue)*(2.3f*b),.028f,.5f,low?4:7,roll+i);}}
                if(since<.4f){float u=since/.4f;Ring(s,point+Up*.05f,3.2f*OutCubic(since/.26f),.44f*(1-u)+.05f,MagicClapLight,low?16:32,2.2f*(1-u));}
                Sparks(s,c,since,.7f,Count(34,q,low),9,MagicClapLight+Vector3.One*.2f,seed+4,6,.026f);
            }
            else if(t>=k.Until){float after=t-k.Until;Flash(s,A+mid,after,.2f,MagicMagnetRose,1.1f,seed+5);Sparks(s,A+mid,after,.4f,Count(12,q,low),4,MagicMagnetRose,seed+6,6,.02f);}
        }

        // ---- Lightning: Thunderclap -------------------------------------------------------------------------------------
        // The raised hands close with an arc jumping between them while the reach rings the floor and static stands up off it and runs inward: time enough to back off. The clap: a bolt out of the sky
        // into the hands, a hard white shell going out to the reach, bolts raking the floor to the ring, a wall of dust.
        private static void Thunderclap(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var chest=MagicChest(scene,a);var f=MagicFacing(a);var r=RightOf(f);var top=chest+Up*.82f+f*.14f;var c=N(k.Point);c=new Vector3(feet.X,c.Y,feet.Z);bool view=scene.Viewer==a.Id;long roll=seed+(long)(t*18);
            if(!k.Hit(t))
            {
                float span=Math.Max(.05f,ArmAt(k)-k.At),u=Clamp01((t-k.At)/span),env=1-Smooth((t-k.Until)/.1f);if(env<=0)return;var hl=top-r*(.46f*(1-u)+.04f);var hr=top+r*(.46f*(1-u)+.04f);
                if(!view){s.Sprite(false,hl,MagicClapLight*((1.4f+2*u)*env),.14f+.14f*u,Hot);s.Sprite(false,hr,MagicClapLight*((1.4f+2*u)*env),.14f+.14f*u,Hot);Bolt(s,hl,hr,(MagicClapLight+Vector3.One*.3f)*((1+1.6f*u)*env),.02f,.5f,low?4:7,roll);int n=low?1:3;for(int i=0;i<n;i++)Bolt(s,i%2==0?hl:hr,chest+new Vector3(R(roll,i+7)-.5f,R(roll,i+8)-.5f,R(roll,i+9)-.5f)*.5f,MagicClapLight*(1.2f*env),.012f,.3f,5,roll+i);}
                // The reach, brightening to the clap, and the static inside it.
                int seg=low?24:48;Ring(s,c+Up*.05f,ClapReach,.08f+.08f*u,MagicClapLight,seg,(.55f+1.5f*u*(.8f+.2f*(float)Math.Sin(t*50)))*env);
                int st=Count(12,q,low);for(int i=0;i<st;i++){float ang=MagicTau*R(roll,i+20),rad=ClapReach*(float)Math.Sqrt(R(roll,i+21));var b=c+new Vector3((float)Math.Cos(ang)*rad,.04f,(float)Math.Sin(ang)*rad);Bolt(s,b,b+Up*(.35f+.75f*R(roll,i+22)*u),MagicClapLight*(1.5f*u*env),.012f,.28f,low?3:4,roll+i+40);}
                int inn=Count(18,q,low);for(int i=0;i<inn;i++){float ph=(u*1.4f+R(seed,i+60))%1,ang=MagicTau*R(seed,i+61),d=ClapReach*(1-ph*ph);var way=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));s.Line(false,c+way*d+Up*.14f,c+way*(d+.7f)+Up*.14f,MagicClapLight*(1.3f*ph*env),.02f,true);}
                return;
            }
            float age=t-k.HitAt;var ctr=view?chest+Up*.1f:top;
            if(!view){Flash(s,top,age,.16f,MagicClapLight,1.5f,seed);if(age<.08f)s.Sprite(false,top,Vector3.One*(4*(1-age/.08f)),1.1f,Sparkle);}
            // The bolt from the sky.
            if(age<.16f)
            {
                float b=1-age/.16f;long bs=seed+(long)(age*60);var sky=ctr+Up*11+r*((R(bs,1)-.5f)*3)+f*((R(bs,2)-.5f)*3);Bolt(s,sky,ctr,(MagicClapLight+Vector3.One)*(3.4f*b),.14f*b+.02f,1.6f,low?8:16,bs);Bolt(s,sky,ctr,MagicClapLight*(.9f*b),.3f*b,1.6f,low?8:16,bs);
                int bn=low?1:3;for(int i=0;i<bn;i++){var o=Vector3.Lerp(sky,ctr,.2f+.5f*R(bs,i+5));var end=o+RandomDir(bs,i+7)*(1.6f+1.6f*R(bs,i+8));Bolt(s,o,end,MagicClapLight*(1.6f*b),.03f*b+.008f,.6f,5,bs+i+20);}
            }
            // The shell going out.
            float rad0=ClapReach*OutCubic(age/.16f),fd=1-Smooth(age/.3f),middle=view?.0f:1;if(fd>0)
            {
                s.Sprite(false,chest,MagicClapLight*(2*fd*middle),rad0,RingSprite);s.Sprite(false,chest,MagicClapLight*(.7f*fd*middle),rad0*.95f,RingSprite);s.Sprite(false,chest,MagicClapLight*(.35f*fd*middle),rad0*.9f,RingSprite);
                int seg=low?24:48;Ring(s,c+Up*.06f,rad0,.6f*fd+.08f,MagicClapLight,seg,1.7f*fd);Ring(s,c+Up*.04f,rad0*.7f,.3f*fd+.05f,MagicClapLight,seg,.7f*fd);Hoop(s,chest-Up*.3f,Up,rad0*.92f,.12f,MagicClapLight,seg,.9f*fd);
            }
            // Bolts raking the floor out to the ring.
            if(age<.42f){float b=1-age/.42f;int n=low?6:12;for(int i=0;i<n;i++){float ang=MagicTau*(i+R(roll,i+70))/n,reach=rad0*(.8f+.2f*R(roll,i+71));var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));Bolt(s,c+d*.3f+Up*.08f,c+d*reach+Up*.06f,(MagicClapLight+Vector3.One*.2f)*(2.3f*b),.03f,.9f,low?5:9,roll+i+80);}}
            Dust(s,c,age,.75f,Count(22,q,low),ClapReach,new Vector3(.5f,.5f,.56f),.5f,seed+3);Sparks(s,ctr,age,.6f,Count(28,q,low),9,MagicClapLight+Vector3.One*.2f,seed+4,4,.026f);
            // The air settling, crackling on the ring.
            if(age>.2f&&age<.9f){float b=1-(age-.2f)/.7f;int n=low?2:5;for(int i=0;i<n;i++){float ang=MagicTau*R(roll,i+100);var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));var e=c+d*(ClapReach*(.9f+.1f*R(roll,i+101)))+Up*.05f;Bolt(s,e,e+RightOf(d)*(.8f*(R(roll,i+102)-.5f)*2)+Up*(.5f*R(roll,i+103)),MagicClapLight*(1.2f*b),.012f,.3f,4,roll+i+110);}}
        }

        // ---- Lightning: Overload ----------------------------------------------------------------------------------------
        // Lightning climbs the pilot and stays: arcs jump between the joints, more of them as it goes on, and reach out to the floor, while a ring under the feet burns down like a fuse. In the last stretch it
        // all turns hot and strobes. The shock is bolts driving in from all round to the chest, sparks, and a puff of burnt smoke. Ended early, it just fizzes out.
        private static Vector3 MagicJoint(Vector3 feet,Vector3 r,int i)
        {
            switch(i){case 0:return feet-r*.14f+Up*.05f;case 1:return feet+r*.14f+Up*.05f;case 2:return feet-r*.13f+Up*.52f;case 3:return feet+r*.13f+Up*.52f;case 4:return feet+Up*.95f;case 5:return feet+Up*1.3f;case 6:return feet-r*.25f+Up*1.42f;case 7:return feet+r*.25f+Up*1.42f;default:return feet+Up*1.72f;}
        }
        private static void Overload(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var chest=MagicChest(scene,a);var r=RightOf(MagicFacing(a));bool view=scene.Viewer==a.Id;float lit=Math.Max(ArmAt(k),k.At+.02f);long roll=seed+(long)(t*18);int joints=view?5:9;
            if(t<lit)
            {
                float u=Clamp01((t-k.At)/Math.Max(.05f,lit-k.At));Ring(s,feet+Up*.05f,.3f+.9f*u,.16f,MagicLive,low?12:22,1.8f);int n=low?2:5;for(int i=0;i<n;i++){float ang=MagicTau*R(roll,i);var b=feet+new Vector3((float)Math.Cos(ang)*.8f,.04f,(float)Math.Sin(ang)*.8f);Bolt(s,b,feet+Up*(view?.8f:.3f+1.5f*u),(MagicLive+Vector3.One*.4f)*2.2f,.02f,.4f,low?4:7,roll+i);}
                if(!view)s.Glow(chest,MagicLive*(1.2f*u),0,1.1f*u+.05f,Core,u,seed);return;
            }
            float live=t-lit,left=k.Until-t;
            if(left>0)
            {
                float total=Math.Max(.1f,k.Until-lit),stress=k.RecastAt>-50?live/4:live/total,hot=Smooth((stress-.74f)/.26f),rate=6+18*hot,strobe=hot>0?.62f+.38f*(float)Math.Sin(t*rate*6.28f):1,env=Smooth(live/.12f)*strobe;var color=Vector3.Lerp(MagicLive,MagicLiveHot,hot);
                if(live<.3f){Flash(s,view?feet+Up*.3f:chest,live,.2f,MagicLive,1.2f,seed+1);Sparks(s,chest,live,.45f,Count(22,q,low),6,MagicLive+Vector3.One*.3f,seed+2,6,.024f);if(!view)Hoop(s,chest,Up,.3f+2.4f*OutCubic(live/.3f),.1f,MagicLive,low?12:22,1.6f*(1-live/.3f));}
                // Across the joints.
                int arcs=(low?3:6)+(low?0:(int)(stress*5));for(int i=0;i<arcs;i++){int j0=(int)(R(roll,i*2+1)*joints)%joints,j1=(j0+1+(int)(R(roll,i*2+2)*(joints-1)))%joints;var a0=MagicJoint(feet,r,j0);var a1=MagicJoint(feet,r,j1);Bolt(s,a0,a1,(color+Vector3.One*.22f)*(1.5f*env),.024f,.32f,low?4:6,roll+i*3);if(!low)Bolt(s,a0,a1,color*(.85f*env),.08f,.32f,6,roll+i*3);}
                // Out to the floor.
                int outs=low?1:3;for(int i=0;i<outs;i++){float ang=MagicTau*R(roll,i+40),rad=.9f+.8f*R(roll,i+41);var e=feet+new Vector3((float)Math.Cos(ang)*rad,.04f,(float)Math.Sin(ang)*rad);Bolt(s,MagicJoint(feet,r,(int)(R(roll,i+42)*joints)%joints),e,color*(1.8f*env),.016f,.45f,low?4:7,roll+i+50);}
                // The fuse under the feet: what is left of the ring is what is left of it.
                float share=Clamp01(left/total);MagicArc(s,feet+Up*.05f,.82f,.1f,0,share,color,low?14:28,1.7f*env);MagicArc(s,feet+Up*.045f,.82f,.04f,share,1,color,low?6:12,.35f);
                if(!view){s.Glow(chest,color*(.5f*env),0,1.2f,Core,0,seed+3);Spray(s,chest+Up*.2f,t,.5f,low?1:3,3,color+Vector3.One*.3f,seed+4,3,.02f,Up*.6f);}
                else if(!low)Spray(s,feet+Up*.3f,t,.5f,2,2,color+Vector3.One*.3f,seed+4,3,.02f,Up*.2f);
                return;
            }
            float after=t-k.Until;
            if(k.Hit(t))
            {
                // The shock.
                float since=t-k.HitAt;var hit=view?feet+Up*.8f:chest;Flash(s,hit,since,.22f,MagicLiveHot,2.4f,seed+5);Sparks(s,hit,since,.6f,Count(30,q,low),7,MagicLiveHot+Vector3.One*.4f,seed+6,7,.026f);
                if(since<.3f){float b=1-since/.3f;int n=low?4:9;long bs=seed+(long)(since*30);for(int i=0;i<n;i++){var d=RandomDir(bs,i);d.Y=Math.Abs(d.Y)*.8f;Bolt(s,hit+d*(1.8f+.9f*R(bs,i+30)),hit,(MagicLiveHot+Vector3.One*.6f)*(2.6f*b),.03f,.5f,low?4:7,bs+i);}}
                if(!low&&since<.8f){float u=since/.8f;int n=Count(7,q,low);for(int i=0;i<n;i++)s.Smoke(chest+new Vector3((R(seed,i+70)-.5f)*.5f,.8f*u+(R(seed,i+71)-.5f)*.5f,(R(seed,i+72)-.5f)*.5f),new Vector3(.1f,.1f,.11f),.5f*(1-u),.45f+.5f*u,.25f+.5f*u,seed+i+70,feet.Y-.05f);}
            }
            else{Sparks(s,chest,after,.5f,Count(16,q,low),3,MagicLive+Vector3.One*.3f,seed+7,7,.02f);if(after<.2f)Ring(s,feet+Up*.05f,.82f+1.2f*after,.08f,MagicLive,low?12:22,1.2f*(1-after/.2f));}
        }

        // ---- Darkness: Soul Siphon --------------------------------------------------------------------------------------
        // A tendril reaches out and takes hold. Then twisted ribbons of violet on a thread of ink, the twist itself and pale souls all running back toward the caster's hand, drawn out of the chest of whoever
        // it holds; the hand glows with what it takes. Broken or stopped, the beam whips back.
        private static void SoulSiphon(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var chest=MagicChest(scene,a);var f=MagicFacing(a);bool view=scene.Viewer==a.Id;var hand=view?chest+f*.6f-Up*.2f:chest+f*.46f+Up*.04f;var who=ActorOf(scene,k.Target);var end=who!=null?MagicChest(scene,who):N(k.To);if(who==null&&t<k.Release&&k.To.Length<.01f)end=hand+MagicAim(k.Dir,f)*5;
            var line=end-hand;float len=line.Length();if(len<.1f)return;var dir=line/len;MagicFrame(dir,out var side,out var lift);
            float stop=k.Recast(t)?k.RecastAt:k.Until,grow=t<k.Release?Smooth((t-k.At)/Math.Max(.05f,k.Release-k.At)):1,back=t>stop?OutCubic((t-stop)/.2f):0,tip=grow*(1-back),env=(1-back)*(.55f+.45f*grow);bool holding=t>=k.Release&&t<stop;
            if(t>=stop+.3f&&t>k.Release)
            {
                float after0=t-stop;Sparks(s,end,after0,.5f,Count(10,q,low),3,MagicDrain,seed+9,2,.02f);return;
            }
            if(tip>.01f)
            {
                // The ribbons.
                int rb=low?2:3,seg=low?10:22;
                for(int j=0;j<rb;j++)
                {
                    float b0=j*MagicTau/rb;
                    Vector3 P(float w){float sway=(float)Math.Sin(3.14159f*Math.Min(1,w/Math.Max(.05f,tip)));float ang=b0+w*len*2.3f+t*11;return hand+line*w+(side*(float)Math.Cos(ang)+lift*(float)Math.Sin(ang))*((.07f+.1f*sway)*(j==0?1:.8f));}
                    for(int i=0;i<seg;i++){float w0=tip*i/seg,w1=tip*(i+1)/seg;var p0=P(w0);var p1=P(w1);s.Band(false,p0-lift*.05f,p1-lift*.05f,p1+lift*.05f,p0+lift*.05f,env*.95f,env*.95f,MagicDrain);}
                }
                s.Line(false,hand,hand+line*tip,MagicDrain*(.6f*env),.05f,false);
                // The thread of ink, running home.
                int ink=Count(12,q,low);for(int i=0;i<ink;i++){float ph=(t*.85f+R(seed,i+20))%1;var p=hand+line*(tip*(1-ph));s.Glow(p,Ink,.6f*env*(float)Math.Sin(3.14159f*ph),.2f+.08f*R(seed,i+21),Core,0,seed+i);}
                // The souls.
                int n=Count(18,q,low);for(int i=0;i<n;i++){float ph=(t*1.05f+R(seed,i+40))%1,w=tip*(1-ph);var p=hand+line*w+side*((R(seed,i+41)-.5f)*.3f*(float)Math.Sin(3.14159f*ph))+lift*((R(seed,i+42)-.5f)*.3f*(float)Math.Sin(3.14159f*ph));float b=(.4f+1.9f*(float)Math.Sin(3.14159f*ph))*env;s.Sprite(false,p,MagicSoul*b,.09f+.07f*R(seed,i+43),Hot);s.Line(false,p,p+dir*.26f,MagicSoul*(.6f*b),.03f,true);}
                if(!low){int big=Count(3,q,low);for(int i=0;i<big;i++){float ph=(t*.6f+i/(float)big)%1;s.Glow(hand+line*(tip*(1-ph)),MagicSoul*(.75f*env*(float)Math.Sin(3.14159f*ph)),0,.42f,Core,ph,seed+i+60);}}
            }
            if(t<k.Release){var head=hand+line*tip;s.Sprite(false,head,MagicDrain*2.2f,.22f,Hot);s.Glow(hand,Ink,.8f*grow,.2f,Core,0,seed+1);return;}
            if(holding)
            {
                float on=Smooth((t-k.Release)/.14f);
                // Whoever it holds: the souls torn out of the chest.
                s.Glow(end,MagicDrain*(.7f*on),0,1,Core,0,seed+2);s.Sprite(false,end,MagicDrain*(1.3f*on),.5f+.04f*(float)Math.Sin(t*13),RingSprite);
                int n=low?4:9;for(int i=0;i<n;i++){float ph=(t*1.5f+R(seed,i+70))%1,ang=MagicTau*R(seed,i+71)+ph;var o=new Vector3((float)Math.Cos(ang),(R(seed,i+72)-.5f)*1.4f,(float)Math.Sin(ang))*(.85f*(1-ph));s.Line(false,end+o,end+o*.72f,MagicSoul*(1.5f*ph*on),.024f,true);}
                if(who!=null){var wf=scene.Feet(who);Ring(s,wf+Up*.05f,.72f,.07f,MagicDrain,low?12:22,1.1f*on);if(!low){int m=Count(5,q,low);for(int i=0;i<m;i++){float ph=(t*.9f+R(seed,i+80))%1;s.Smoke(wf+new Vector3((R(seed,i+81)-.5f)*.8f,.2f+1.5f*ph,(R(seed,i+82)-.5f)*.8f),Ink,.5f*(1-ph),.5f+.3f*ph,.3f,seed+i+80,wf.Y-.05f);}}}
                // The hand that takes it, and what lifts off the caster.
                s.Sprite(false,hand,MagicSoul*((view?1:2.1f)*on),.2f+.05f*(float)Math.Sin(t*17),Hot);s.Sprite(false,hand,MagicDrain*(1.4f*on),.3f,RingSprite);
                if(!view){s.Glow(chest,MagicSoul*(.42f*on),0,.75f,Core,0,seed+3);int h=low?2:6;for(int i=0;i<h;i++){float ph=(t*.8f+R(seed,i+90))%1,ang=MagicTau*R(seed,i+91)+ph*2;s.Glow(feet+new Vector3((float)Math.Cos(ang)*.42f,.2f+1.7f*ph,(float)Math.Sin(ang)*.42f),MagicSoul*(1.5f*(float)Math.Sin(3.14159f*ph)*on),0,.09f,Core,ph,seed+i+90);}}
            }
            // The snap.
            float after=t-stop;if(after>=0){Flash(s,end,after,.2f,MagicDrain,1,seed+4);Sparks(s,end,after,.5f,Count(10,q,low),3,MagicDrain,seed+9,2,.02f);}
        }

        // ---- Darkness: Dread --------------------------------------------------------------------------------------------
        // The wind-up is the warning: the cone is drawn on the floor and fills, shadows crawl in along it and up into the head, where two points of light open. The scream: walls of violet going out across
        // the cone one behind the other, ink flung with them, and a wailing face carried out in front of the caster, there and gone. The victims' marks draw on them, when the rules carry them.
        private static void Dread(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var facing=MagicFacing(a);var f=Flat(N(k.Dir),facing);var r=RightOf(f);var head=feet+Up*1.72f;bool view=scene.Viewer==a.Id;float floor=feet.Y-.05f;var apex=feet+Up*.04f;int wedge=low?5:10;
            Vector3 Across(float share)=>f*(float)Math.Cos(share*MagicDreadHalf)+r*(float)Math.Sin(share*MagicDreadHalf);
            if(!k.Hit(t))
            {
                float u=Clamp01((t-k.At)/.35f),env=1-Smooth((t-k.Until)/.1f);if(env<=0)return;
                MagicWedge(s,apex,f,MagicDreadHalf,Math.Max(.6f,DreadReach*Smooth(u*1.5f)),MagicDread,(.5f+1.3f*u)*env,.24f*u*env,wedge);
                int n=Count(20,q,low);for(int i=0;i<n;i++){float ph=(u*1.5f+R(seed,i+1))%1,d=DreadReach*(1-ph)*(.3f+.7f*R(seed,i+2));var way=Across(R(seed,i+3)*2-1);var p=apex+way*d+Up*.08f;s.Line(false,p,p+way*.55f,MagicDread*(1.6f*ph*env),.03f,true);if(!low&&i%2==0)s.Smoke(p+Up*.1f,Ink*2,.5f*ph*env,.45f,.25f,seed+i,floor);}
                if(!view)
                {
                    s.Glow(head,Ink,.85f*u*env,.42f+.3f*u,Core,0,seed);for(int e=-1;e<=1;e+=2)s.Sprite(false,head+f*.13f+r*(e*.07f),(MagicDread+Vector3.One*.4f)*(2.8f*u*env),.05f+.05f*u,Sparkle);
                    int m=low?3:6;for(int i=0;i<m;i++){float ang=MagicTau*R(seed,i+30)+t*2;var b=feet+new Vector3((float)Math.Cos(ang)*1.5f*(1-.5f*u),.05f,(float)Math.Sin(ang)*1.5f*(1-.5f*u));s.Line(false,b,head,MagicDread*(.9f*u*env),.016f,true);}
                }
                return;
            }
            float since=t-k.HitAt;if(since>1.1f)return;
            if(since<.45f)MagicWedge(s,apex,f,MagicDreadHalf,DreadReach,MagicDread,2.2f*(1-since/.45f),.3f*(1-since/.45f),wedge);
            if(!view){Flash(s,head+f*.25f,since,.22f,MagicDread,2.2f,seed+1);s.Glow(head,Ink,.9f*(1-Clamp01(since/.3f)),.7f,Core,0,seed+2);}
            // The walls of sound.
            int walls=low?3:5;for(int j=0;j<walls;j++)
            {
                float w=since*15-j*1.5f;if(w<=.4f||w>DreadReach*1.04f)continue;float life=1-w/(DreadReach*1.04f),b=(.45f+.75f*life)*(1-.12f*j),hi=.5f+1.7f*w/DreadReach;
                for(int i=0;i<wedge;i++){float s0=-1+2f*i/wedge,s1=-1+2f*(i+1)/wedge;var p0=apex+Across(s0)*w;var p1=apex+Across(s1)*w;s.Band(false,p0,p1,p1+Up*hi,p0+Up*hi,b,b,MagicDread*1.25f);s.Line(false,p0+Up*(hi*.5f),p1+Up*(hi*.5f),MagicDread*(1.5f*b),.03f,false);}
            }
            // Ink flung out with them.
            float inkLife=.8f;if(since<inkLife){int n=Count(32,q,low);for(int i=0;i<n;i++){float own=Math.Max(0,since-.02f*(i%6)),u=own/inkLife,d=DreadReach*OutCubic(own/.55f)*(.25f+.75f*R(seed,i+50));var p=apex+Across(R(seed,i+51)*2-1)*d+Up*(.15f+1.5f*R(seed,i+52)*(.4f+.6f*d/DreadReach));s.Smoke(p,Ink*2.2f,.68f*(1-u)*(1-u),.65f+1.1f*u,.15f+.65f*u,seed+i+50,floor);}}
            if(since<.5f){int n=Count(26,q,low);for(int i=0;i<n;i++){var way=Across(R(seed,i+90)*2-1);float d=DreadReach*1.05f*Clamp01(since*(2.4f+1.4f*R(seed,i+91)));if(d>=DreadReach*1.05f)continue;var p=apex+way*d+Up*(.2f+1.7f*R(seed,i+92));s.Streak(p,way*13,MagicDread*(2.2f*(1-since/.5f)),.024f,.12f,.04f,.8f,since/.5f,seed+i);}}
            // The face.
            if(!view&&since<.62f)MagicFace(s,head+f*(.55f+7.5f*OutCubic(since/.62f))-Up*.2f,r,1.1f+2.6f*since,1-Smooth((since-.1f)/.52f),low,seed+200);
        }
        // A wailing face in the plane across `r` and up: the oval of a skull, two sockets, a long open mouth.
        private static void MagicFace(ITechniqueFxSink s,Vector3 c,Vector3 r,float size,float b,bool low,long seed)
        {
            if(b<=0)return;int seg=low?10:20;var color=MagicDread*1.9f*b;
            void Oval(Vector3 o,float wide,float tall,Vector3 col,float width){Vector3 last=o+r*wide;for(int i=1;i<=seg;i++){float ang=MagicTau*i/seg;var p=o+r*(wide*(float)Math.Cos(ang))+Up*(tall*(float)Math.Sin(ang));s.Line(false,last,p,col,width,false);last=p;}}
            s.Glow(c,Ink,.3f*b,size*.7f,Core,0,seed);Oval(c,size*.5f,size*.68f,color,.04f*size);
            for(int e=-1;e<=1;e+=2){var eye=c+r*(e*size*.21f)+Up*(size*.2f);s.Glow(eye,Ink,.95f*b,size*.2f,Core,0,seed+1+e);Oval(eye,size*.11f,size*.14f,color*1.1f,.028f*size);}
            var mouth=c-Up*(size*.28f);s.Glow(mouth,Ink,.95f*b,size*.26f,Core,0,seed+4);s.Glow(mouth-Up*(size*.1f),Ink,.9f*b,size*.2f,Core,0,seed+5);Oval(mouth,size*.15f,size*.28f,color*1.1f,.03f*size);
        }

        // ---- Darkness: Shadow Puppet ------------------------------------------------------------------------------------
        // Your shadow, stood up off the floor on strings: a dark jointed figure with violet veins, ball joints and two pinpoint eyes, one arm out along your aim, hung from a crossed bar above it with its
        // own pool under its feet. As its health goes the strings go, one by one. Each shot kicks the arm. When it dies the strings whip away and it falls apart into smoke.
        private static void ShadowPuppet(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var at=N(k.Point);float floor=at.Y-.05f;var dir=MagicAim(k.Dir,Vector3.UnitZ);var f=Flat(dir,Vector3.UnitZ);var r=RightOf(f);float hp=Clamp01(k.Arm);
            if(t<k.Release)
            {
                float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));Ring(s,at+Up*.05f,.9f*u+.05f,.1f,GraveLight,low?10:20,1.3f*u);int n=Count(10,q,low);for(int i=0;i<n;i++){float ang=MagicTau*R(seed,i+1),rad=.8f*u*(float)Math.Sqrt(R(seed,i+2));s.Smoke(at+new Vector3((float)Math.Cos(ang)*rad,.2f,(float)Math.Sin(ang)*rad),Ink,.9f*u,.7f,.25f,seed+i,floor);}return;
            }
            float age=t-k.Release;
            if(t<k.Until)
            {
                float rise=Smooth(age/.42f),left=k.Until-t,flick=hp<.5f?.65f+.35f*(float)Math.Sin(t*(28-30*hp)):1,env=Smooth(age/.2f)*flick*(left<.4f?.5f+.5f*left/.4f:1),sway=(float)Math.Sin(t*1.7f)*.025f;float shot=k.Hit(t)?t-k.HitAt:9,kick=(float)Math.Exp(-shot*16)*.13f;
                Vector3 J(float x,float y,float z)=>at+r*(x+sway*y)+Up*(y*rise)+f*z;
                var headP=J(0,1.74f,0);var neck=J(0,1.5f,0);var hip=J(0,.96f,0);var shL=J(-.21f,1.44f,0);var shR=J(.21f,1.44f,0);var kneeL=J(-.13f,.52f,.04f);var kneeR=J(.13f,.52f,.04f);var footL=J(-.17f,.04f,0);var footR=J(.17f,.04f,0);
                var handR=shR+(dir*(.66f*rise)-dir*kick);var elbowR=(shR+handR)*.5f-Up*.05f;float dangle=(float)Math.Sin(t*2.3f)*.05f;var elbowL=J(-.3f,1.14f,.02f+dangle*.5f);var handL=J(-.3f,.84f,.1f+dangle);
                // The pool it stands in.
                int pool=Count(12,q,low);for(int i=0;i<pool;i++){float ang=MagicTau*R(seed,i+1),rad=.82f*(float)Math.Sqrt(R(seed,i+2));s.Smoke(at+new Vector3((float)Math.Cos(ang)*rad,.2f+.1f*R(seed,i+3),(float)Math.Sin(ang)*rad),Ink,.92f*env,.72f+.3f*R(seed,i+4),.25f,seed+i,floor);}
                Ring(s,at+Up*.05f,.92f,.1f,GraveLight,low?12:24,.9f*env);
                // The body: limbs in ink, chest and head massed over them.
                void Limb(Vector3 p0,Vector3 p1,float w,int id){s.Drop(p0,p0-p1,Ink,.98f*env,w,0,seed+id);s.Glow(p1,Ink,.95f*env,w*.75f,Core,0,seed+id+40);}
                Limb(hip,kneeL,.17f,1);Limb(kneeL,footL,.13f,2);Limb(hip,kneeR,.17f,3);Limb(kneeR,footR,.13f,4);Limb(shL,elbowL,.12f,5);Limb(elbowL,handL,.1f,6);Limb(shR,elbowR,.12f,7);Limb(elbowR,handR,.1f,8);Limb(neck,hip,.34f,9);
                s.Glow((neck+hip)*.5f+Up*.12f,Ink,.96f*env,.34f,Core,0,seed+20);s.Glow(hip,Ink,.92f*env,.26f,Core,0,seed+21);s.Glow(shL,Ink,.9f*env,.15f,Core,0,seed+22);s.Glow(shR,Ink,.9f*env,.15f,Core,0,seed+23);s.Glow(headP,Ink,.98f*env,.25f,Core,0,seed+24);s.Glow(footL,Ink,.9f*env,.13f,Core,0,seed+25);s.Glow(footR,Ink,.9f*env,.13f,Core,0,seed+26);
                // The veins and the ball joints.
                var vein=MagicPuppet*(1.5f*env);void V(Vector3 p0,Vector3 p1){s.Line(false,p0,p1,vein,.016f,false);}
                V(neck,hip);V(shL,shR);V(hip,kneeL);V(kneeL,footL);V(hip,kneeR);V(kneeR,footR);V(shL,elbowL);V(elbowL,handL);V(shR,elbowR);V(elbowR,handR);V(headP,neck);
                if(!low){var ball=MagicPuppet*(1.9f*env);foreach(var p in new[]{shL,shR,elbowL,elbowR,kneeL,kneeR,hip,handL,handR})s.Sprite(false,p,ball,.07f,RingSprite);}
                s.Sprite(false,headP,MagicPuppet*(.9f*env),.2f,RingSprite);for(int e=-1;e<=1;e+=2)s.Sprite(false,headP+f*.11f+r*(e*.06f)+Up*.02f,(MagicPuppet+Vector3.One*.4f)*(2.6f*env),.05f,Sparkle);
                // The strings and the bar; they go as its health does.
                if(rise>.5f)
                {
                    float rock=(float)Math.Sin(t*1.3f)*.09f;var bar=at+Up*3.15f+r*rock;var b0=bar-r*.42f;var b1=bar+r*.42f;var b2=bar-f*.26f;var b3=bar+f*.26f;var wood=MagicPuppet*(1.3f*env);s.Line(false,b0,b1,wood,.035f,false);s.Line(false,b2,b3,wood,.035f,false);
                    var thread=MagicPuppet*(.85f*env);int left2=(int)Math.Ceiling(hp*5);
                    if(left2>0)s.Line(false,bar,headP,thread,.009f,false);if(left2>1)s.Line(false,b1,handR,thread,.009f,false);if(left2>2)s.Line(false,b0,handL,thread,.009f,false);if(left2>3)s.Line(false,b3,kneeR,thread,.009f,false);if(left2>4)s.Line(false,b2,kneeL,thread,.009f,false);
                }
                // The wisps coming off it.
                int w=Count(8,q,low);for(int i=0;i<w;i++){float ph=(t*.8f+R(seed,i+60))%1,ang=MagicTau*R(seed,i+61);s.Glow(at+new Vector3((float)Math.Cos(ang)*.55f*(1+.3f*ph),.1f+1.9f*ph,(float)Math.Sin(ang)*.55f*(1+.3f*ph)),GraveLight*(1.2f*(1-ph)*env),0,.07f,Core,0,seed+i);}
                if(!low){int m=Count(5,q,low);for(int i=0;i<m;i++){float ph=(t*.7f+R(seed,i+70))%1;s.Smoke(at+new Vector3((R(seed,i+71)-.5f)*.45f,.4f+1.3f*ph,(R(seed,i+72)-.5f)*.45f),Ink,.35f*(1-ph)*env,.42f+.2f*ph,.3f+.4f*ph,seed+i+70,floor);}}
                // Its shot.
                if(shot<.25f)
                {
                    float b=1-shot/.25f;var to=N(k.To);Flash(s,handR,shot,.14f,MagicPuppet,.9f,seed+30);s.Line(false,handR,to,MagicPuppet*(1.7f*b),.05f*b+.01f,false);s.Line(false,handR,to,(MagicPuppet+Vector3.One)*(2.2f*b),.012f,true);Flash(s,to,shot,.18f,MagicPuppet,.8f,seed+31);Sparks(s,to,shot,.25f,Count(10,q,low),3.5f,MagicPuppet,seed+32,3,.02f);
                }
                return;
            }
            // It dies: the strings whip up, the body falls apart.
            float after=t-k.Until;if(after>.9f)return;var c=at+Up*1;Flash(s,c,after,.22f,GraveLight,1.5f,seed+40);Sparks(s,c,after,.5f,Count(16,q,low),4,MagicPuppet,seed+41,3,.022f,Up*.3f);
            if(after<.3f){float u=after/.3f;var bar=at+Up*(3.15f+2*u);for(int i=0;i<(low?3:5);i++){var o=at+Up*(1.2f+1.5f*u)+new Vector3((R(seed,i+50)-.5f)*1.2f,0,(R(seed,i+51)-.5f)*1.2f);s.Line(false,bar,o,MagicPuppet*(1.1f*(1-u)),.009f,false);}}
            if(after<.7f){float u=after/.7f;int n=Count(20,q,low);for(int i=0;i<n;i++){var d=RandomDir(seed,i+60);float y=1.8f*R(seed,i+61);s.Smoke(at+new Vector3(d.X*(.2f+.7f*u),y*(1-.5f*u)+.1f,d.Z*(.2f+.7f*u)),Ink,.8f*(1-u),.45f+.5f*u,.2f+.7f*u,seed+i+60,floor);}}
        }
    }
    public sealed partial class NativeTechniquePreview
    {
        private static bool WriteMagic(NativeTechnique k,string id,Vector3 feet,Vector3 f,Vector3 r,Vector3 up,Vector3 hand,float t0,bool first)
        {
            switch(id)
            {
                case NativeTechniques.FlameCloak:k.Release=t0+.12f;k.Arm=t0+.34f;k.Until=k.Arm+4;k.RecoverUntil=k.Arm+.3f;if(first){k.HitAt=k.Arm+1.2f;k.To=V(feet+f*.85f+up*1.25f+r*.1f);}return true;
                case NativeTechniques.DragonBreath:k.Release=t0+.25f;k.Arm=k.Release;k.Dir=V(Vector3.Normalize(f-up*.06f));k.Until=k.Arm+1.6f;if(!first){k.RecastAt=k.Arm+.8f;k.Until=k.RecastAt;}k.RecoverUntil=k.Until+.12f;return true;
                case NativeTechniques.Combust:{k.Release=t0+.2f;k.Arm=k.Release;k.Until=k.Release+.3f;k.RecoverUntil=k.Release;var body=feet+up*.865f;k.Path=first?new[]{V(body+f*6+r*3),V(body+f*5.5f-r*3.4f),V(body+f*9.5f)}:new[]{V(body+f*5)};}return true;
                case NativeTechniques.BubbleTrap:k.Release=t0+.24f;k.RecoverUntil=k.Release+.25f;if(first){k.Point=V(feet+f*7+up*1.1f);k.HitAt=k.Release+.39f;k.Until=k.HitAt+1.4f;}else{k.Point=V(feet+f*14+up*1.3f);k.RecastAt=k.Release+.78f;k.Until=k.RecastAt;}return true;
                case NativeTechniques.FlashFreeze:k.Release=t0+.18f;k.Point=V(feet);k.Until=k.Release+4;k.RecoverUntil=k.Release+.25f;return true;
                case NativeTechniques.HydroJet:k.Release=t0+.22f;k.Dir=V(Vector3.Normalize(f-up*.18f));k.Until=k.Release+1.5f;if(!first){k.RecastAt=k.Release+.7f;k.Until=k.RecastAt;}k.RecoverUntil=k.Until;return true;
                case NativeTechniques.Magnetize:k.Release=t0+.1f;k.Arm=t0+.5f;k.RecoverUntil=k.Release+.2f;if(first){k.Point=V(feet+f*6+r*4.5f);k.Path=new[]{V(feet+f*6+r*3.5f),V(feet+f*6+r*5.5f)};k.HitAt=k.Arm+.6f;k.Until=k.HitAt+.35f;}else{k.Point=V(feet+f*5);k.Path=new[]{V(feet+f*9+r*2.5f)};k.Until=k.Arm+1;}return true;
                case NativeTechniques.Thunderclap:k.Release=t0+.3f;k.Arm=k.Release;k.HitAt=k.Release;k.Point=V(feet);k.Until=k.Release+.3f;k.RecoverUntil=k.Release+.2f;return true;
                case NativeTechniques.Overload:k.Release=t0+.1f;k.Arm=k.Release;if(first){k.Until=k.Release+4;k.HitAt=k.Until;}else{k.RecastAt=k.Release+.7f;k.Until=k.RecastAt;}k.RecoverUntil=k.Release;return true;
                case NativeTechniques.SoulSiphon:k.Release=t0+.1f;k.To=V(feet+f*7+up*1.25f);k.Until=k.Release+2;if(!first){k.RecastAt=k.Release+1;k.Until=k.RecastAt;}k.RecoverUntil=k.Until;return true;
                case NativeTechniques.Dread:k.Release=t0+.05f;k.HitAt=t0+.35f;k.Point=V(feet);k.Until=k.HitAt+.6f;k.RecoverUntil=k.HitAt+.3f;return true;
                case NativeTechniques.ShadowPuppet:k.Release=t0+.28f;k.Point=V(feet+r*1.7f+f*.6f);k.Until=k.Release+4;k.RecoverUntil=k.Release+.3f;k.Arm=first?1:.4f;if(first){k.HitAt=k.Release+1.3f;k.To=V(feet+r*1.7f+f*14+up*1.25f);}return true;
                default:return false;
            }
        }
        private static float PeriodMagic(string id)
        {
            switch(id)
            {
                case NativeTechniques.FlameCloak:return 5.6f;
                case NativeTechniques.DragonBreath:return 3.4f;
                case NativeTechniques.Combust:return 2.4f;
                case NativeTechniques.BubbleTrap:return 3.6f;
                case NativeTechniques.FlashFreeze:return 5.6f;
                case NativeTechniques.HydroJet:return 3.2f;
                case NativeTechniques.Magnetize:return 3.2f;
                case NativeTechniques.Thunderclap:return 2.4f;
                case NativeTechniques.Overload:return 5.6f;
                case NativeTechniques.SoulSiphon:return 3.8f;
                case NativeTechniques.Dread:return 2.8f;
                case NativeTechniques.ShadowPuppet:return 5.8f;
                default:return 0;
            }
        }
        private static bool AimMagic(NativeTechnique k,string id,int target,Vector3 at)
        {
            switch(id)
            {
                case NativeTechniques.Combust:if(k.Path!=null&&k.Path.Length>0)k.Path[0]=V(at+Vector3.UnitY*.865f);return true;
                case NativeTechniques.BubbleTrap:if(k.HitAt>-50){k.Target=target;k.Point=V(at+Vector3.UnitY*1.1f);}return true;
                case NativeTechniques.Magnetize:k.Target=target;if(k.Path!=null&&k.Path.Length>0){k.Path[0]=V(at);if(k.Path.Length>1){var other=new Vector3(k.Path[1].x,k.Path[1].y,k.Path[1].z);k.Point=V((at+other)*.5f);}}return true;
                case NativeTechniques.SoulSiphon:k.Target=target;k.To=V(at+Vector3.UnitY*1.25f);return true;
                default:return false;
            }
        }
    }
    public static partial class NativeTechniqueSounds
    {
        private static int MomentMagic(string kind,int m,NativeKitSound[] o,ref bool atPoint)
        {
            int n=0;
            switch(kind)
            {
                case NativeTechniques.FlameCloak:if(m==Release){n=S(o,n,"blast.fire","explosion",.45f);n=S(o,n,"magic.flame-lance","fire",.55f);}else if(m==Hit){n=S(o,n,"magic.flame-lance","fire",.75f);n=S(o,n,"blast.fire","explosion",.35f);}else if(m==End)n=S(o,n,"magic.flame-lance","fire",.22f);break;
                case NativeTechniques.DragonBreath:if(m==Release){n=S(o,n,"blast.fire","explosion",.7f);n=S(o,n,"magic.flame-lance","fire",.75f);}else if(m==End)n=S(o,n,"magic.flame-lance","fire",.25f);break;
                case NativeTechniques.Combust:if(m==Release)n=S(o,n,"gun.reload.end","metal",.4f);else if(m==Arm){n=S(o,n,"blast.fire","explosion",1);n=S(o,n,"magic.flame-lance","fire",.5f);}break;
                case NativeTechniques.BubbleTrap:if(m==Release)n=S(o,n,"magic.geyser","water",.4f);else if(m==Hit){atPoint=true;n=S(o,n,"impact.water","water",.85f);n=S(o,n,"magic.geyser","water",.35f);}else if(m==End){n=S(o,n,"impact.water","water",.75f);n=S(o,n,"blast.transient","dash",.3f);}break;
                case NativeTechniques.FlashFreeze:if(m==Release){atPoint=true;n=S(o,n,"blast.quake","stone",.5f);n=S(o,n,"impact.water","water",.55f);n=S(o,n,"impact.metal","metal",.35f);}else if(m==End){atPoint=true;n=S(o,n,"impact.metal","metal",.3f);n=S(o,n,"impact.water","water",.3f);}break;
                case NativeTechniques.HydroJet:if(m==Release){n=S(o,n,"magic.geyser","water",.85f);n=S(o,n,"impact.water","water",.45f);}else if(m==End)n=S(o,n,"impact.water","water",.4f);break;
                case NativeTechniques.Magnetize:if(m==Release)n=S(o,n,"blast.static","lightning",.6f);else if(m==Arm){atPoint=true;n=S(o,n,"blast.shock","lightning",.45f);}else if(m==Hit){atPoint=true;n=S(o,n,"blast.shock","lightning",.9f);n=S(o,n,"impact.metal","metal",.75f);}break;
                case NativeTechniques.Thunderclap:if(m==Release)n=S(o,n,"blast.static","lightning",.55f);else if(m==Hit){atPoint=true;n=S(o,n,"blast.shock","lightning",1);n=S(o,n,"blast.kinetic","dash",.75f);n=S(o,n,"blast.quake","stone",.35f);}break;
                case NativeTechniques.Overload:if(m==Release){n=S(o,n,"blast.static","lightning",.7f);n=S(o,n,"blast.shock","lightning",.35f);}else if(m==Hit){n=S(o,n,"blast.shock","lightning",.9f);n=S(o,n,"blast.transient","dash",.4f);}else if(m==Recast)n=S(o,n,"blast.static","lightning",.3f);break;
                case NativeTechniques.SoulSiphon:if(m==Release)n=S(o,n,"magic.darkness.shot","darkness",.65f);else if(m==End)n=S(o,n,"magic.darkness.hit","darkness",.35f);break;
                case NativeTechniques.Dread:if(m==Release)n=S(o,n,"magic.darkness.shot","darkness",.5f);else if(m==Hit){n=S(o,n,"magic.darkness.hit","darkness",.95f);n=S(o,n,"blast.kinetic","dash",.6f);}break;
                case NativeTechniques.ShadowPuppet:if(m==Release){atPoint=true;n=S(o,n,"magic.darkness.shot","darkness",.6f);}else if(m==Hit){atPoint=true;n=S(o,n,"magic.darkness.shot","darkness",.35f);}else if(m==End){atPoint=true;n=S(o,n,"magic.darkness.hit","darkness",.7f);}break;
            }
            return n;
        }
    }
    public static partial class NativeVisualMotion
    {
        private static bool PoseMagic(Set set,NativeTechnique k,float time,float back,IClips clips,out Pick pick)
        {
            pick=default;Pick got=default;
            bool Part(string clip,float from,float to){return time>=from&&time<to&&clip!=null&&clips.Has(clip)&&Fit(clip,time,from,to,clips,FadeIn,out got);}
            // A clip held still at a share of its length, for what is kept up (a channel's raised hands).
            bool Hold(string clip,float share,float from,float to){if(time<from||time>=to||clip==null||!clips.Has(clip))return false;got=new Pick{Clip=clip,Time=clips.Length(clip)*share,Fade=FadeIn};return true;}
            string Cast0(int i)=>set.CastStart!=null&&i<set.CastStart.Length?set.CastStart[i]:null;string Cast1(int i)=>set.CastEnd!=null&&i<set.CastEnd.Length?set.CastEnd[i]:null;
            // The staff set's casts by name (Set.AbilityStarts): 0 Attack3, 1 L-Buff1, 2 L-AOE1, 3 L-Summon2, 4 L-Buff2, 5 L-Summon1, 6 Attack2, 7 L-AOE2.
            string Ab0(int i)=>set.AbilityStarts!=null&&i<set.AbilityStarts.Length?set.AbilityStarts[i]:null;string Ab1(int i)=>set.AbilityEnds!=null&&i<set.AbilityEnds.Length?set.AbilityEnds[i]:null;
            bool hit=false;
            switch(k.Kind)
            {
                case NativeTechniques.FlameCloak:{float lit=Math.Max(k.Arm,k.At+.05f);hit=Part(TechBuffStart,k.At,lit)||Part(TechBuffEnd,lit,Math.Max(lit+.3f,back));}break;
                case NativeTechniques.DragonBreath:{float stop=k.RecastAt>-50?k.RecastAt:k.Until,arm=NativeTechniqueFx.ArmAt(k);hit=Part(Cast0(2),k.At,Math.Max(arm,k.At+.05f))||Hold(Cast1(2),.1f,arm,stop)||Part(Cast1(2),stop,Math.Max(stop+.25f,back));}break;
                case NativeTechniques.Combust:hit=Part(TechTouch,k.At,Math.Max(NativeTechniqueFx.ArmAt(k)+.3f,back));break;
                case NativeTechniques.BubbleTrap:hit=Part(Cast0(0),k.At,k.Release)||Part(Cast1(0),k.Release,Math.Max(k.Release+.3f,back));break;
                case NativeTechniques.FlashFreeze:hit=Part(Ab0(2),k.At,k.Release)||Part(Ab1(2),k.Release,Math.Max(k.Release+.35f,back));break;
                case NativeTechniques.HydroJet:{float stop=k.RecastAt>-50?k.RecastAt:k.Until;if(time>=k.Release&&time<stop&&set.Fall!=null&&clips.Has(set.Fall)){got=new Pick{Clip=set.Fall,Time=time-k.Release,Fade=FadeIn};hit=true;}else hit=Part(Cast0(1),k.At,k.Release);}break;
                case NativeTechniques.Magnetize:hit=Part(Ab0(5),k.At,Math.Max(k.Arm,k.At+.05f))||Part(Ab1(5),k.Arm,k.Arm+.35f);break;
                case NativeTechniques.Thunderclap:{float clap=Math.Max(NativeTechniqueFx.ArmAt(k),k.At+.05f);hit=Part(Ab0(4),k.At,clap)||Part(Ab1(4),clap,Math.Max(clap+.3f,back));}break;
                case NativeTechniques.Overload:{float lit=Math.Max(NativeTechniqueFx.ArmAt(k),k.At+.05f);hit=Part(TechBuffStart,k.At,lit)||Part(TechBuffEnd,lit,Math.Max(lit+.3f,back));}break;
                case NativeTechniques.SoulSiphon:{float stop=k.RecastAt>-50?k.RecastAt:k.Until;hit=Part(Cast0(1),k.At,k.Release)||Hold(Cast1(1),.1f,k.Release,stop)||Part(Cast1(1),stop,stop+.25f);}break;
                case NativeTechniques.Dread:{float scream=k.HitAt>-50?k.HitAt:k.At+.35f;hit=Part(Ab0(7),k.At,scream)||Part(Ab1(7),scream,Math.Max(scream+.4f,back));}break;
                case NativeTechniques.ShadowPuppet:hit=Part(Ab0(3),k.At,k.Release)||Part(Ab1(3),k.Release,Math.Max(k.Release+.4f,back));break;
                default:return false;
            }
            pick=got;return hit;
        }
    }
}
