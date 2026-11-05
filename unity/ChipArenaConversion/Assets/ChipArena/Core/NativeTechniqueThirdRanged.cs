using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The third set's pistol, machine gun, rifle and bow: effects, previews, sounds and poses.
    public sealed partial class NativeTechniqueFx
    {
        private static readonly string[] RangedBuilt={NativeTechniques.FlashRound,NativeTechniques.GunSpin,NativeTechniques.SlideShot,NativeTechniques.Bipod,NativeTechniques.AmmoCrate,NativeTechniques.FlakBurst,NativeTechniques.BreacherRound,NativeTechniques.Ghillie,NativeTechniques.BolasRound,NativeTechniques.BrambleArrow,NativeTechniques.VaultShot,NativeTechniques.TetherArrow};
        /// <summary>How far Flash Round dazzles; Flak Burst's cloud; Bramble Arrow's hedge either side of its middle, and its height; how far a Tether Arrow lets you go.</summary>
        public const float FlashReach=6,FlakReach=5,BrambleHalf=2.5f,BrambleTall=1,TetherReach=5;
        private static readonly Vector3 FlashWhite=new Vector3(1,.96f,.75f),SpinBrass=new Vector3(1,.78f,.32f),SlideDust=new Vector3(1,.74f,.46f),BipodHeat=new Vector3(1,.4f,.22f),BipodSteel=new Vector3(.6f,.68f,.82f),CrateGold=new Vector3(1,.78f,.22f),CrateSteel=new Vector3(.55f,.64f,.78f),
            FlakOrange=new Vector3(1,.52f,.18f),BreachLight=new Vector3(.45f,.85f,1),BreachWall=new Vector3(1,.42f,.14f),GhillieGreen=new Vector3(.5f,.78f,.36f),BolasCord=new Vector3(.9f,.74f,.42f),BrambleGreen=new Vector3(.3f,.85f,.2f),BrambleThorn=new Vector3(.95f,1,.5f),VaultMint=new Vector3(.58f,1,.84f),TetherBlue=new Vector3(.5f,.82f,1);
        private static bool ThirdRanged(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            switch(k.Kind)
            {
                case NativeTechniques.FlashRound:FlashRound(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.GunSpin:GunSpin(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.SlideShot:SlideShot(s,k,t,seed,q,low);return true;
                case NativeTechniques.Bipod:Bipod(s,k,t,seed,q,low);return true;
                case NativeTechniques.AmmoCrate:AmmoCrate(s,k,t,seed,q,low);return true;
                case NativeTechniques.FlakBurst:FlakBurst(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.BreacherRound:BreacherRound(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.Ghillie:Ghillie(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.BolasRound:BolasRound(s,scene,k,t,seed,q,low);return true;
                case NativeTechniques.BrambleArrow:BrambleArrow(s,k,t,seed,q,low);return true;
                case NativeTechniques.VaultShot:VaultShot(s,k,t,seed,q,low);return true;
                case NativeTechniques.TetherArrow:TetherArrow(s,scene,k,t,seed,q,low);return true;
                default:return false;
            }
        }
        private static Vector3 RangedNorm(Vector3 v,Vector3 fallback)=>v.LengthSquared()>1e-6f?Vector3.Normalize(v):fallback;
        private static Vector3 RangedSide(Vector3 d){var c=Vector3.Cross(d,Up);return c.LengthSquared()>1e-5f?Vector3.Normalize(c):Vector3.UnitX;}
        // A box of lines: its middle and its half-sizes as vectors.
        private static void RangedBox(ITechniqueFxSink s,Vector3 c,Vector3 x,Vector3 y,Vector3 z,Vector3 color,float width)
        {
            for(int i=-1;i<=1;i+=2)for(int j=-1;j<=1;j+=2)
            {
                s.Line(false,c+x*i+y*j-z,c+x*i+y*j+z,color,width,false);s.Line(false,c+y*i+z*j-x,c+y*i+z*j+x,color,width,false);s.Line(false,c+x*i+z*j-y,c+x*i+z*j+y,color,width,false);
            }
        }
        // A floor ring of dashes, turning.
        private static void RangedDashes(ITechniqueFxSink s,Vector3 c,float radius,float width,float turn,int dashes,float fill,Vector3 color,float b)
        {
            float inner=Math.Max(0,radius-width*.5f),outer=radius+width*.5f;
            for(int i=0;i<dashes;i++){float a0=turn+6.2831853f*i/dashes,a1=a0+6.2831853f/dashes*fill;var d0=new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0));var d1=new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1));s.Band(false,c+d0*inner,c+d1*inner,c+d1*outer,c+d0*outer,b,b,color);}
        }
        // An arrow in flight: a bright shaft and head, fletching, a soft wake.
        private static void RangedArrow(ITechniqueFxSink s,Vector3 head,Vector3 d,Vector3 color,float b)
        {
            var white=color+Vector3.One*.5f;var side=RangedSide(d);var nock=head-d*.9f;
            s.Line(false,nock,head,white*(2.2f*b),.03f,true);s.Line(false,head-d*2.6f,head-d*.3f,color*(.7f*b),.09f,false);s.Sprite(false,head,white*(2.2f*b),.24f,Hot);
            s.Line(false,nock-side*.09f,nock+d*.22f,white*(1.4f*b),.02f,true);s.Line(false,nock+side*.09f,nock+d*.22f,white*(1.4f*b),.02f,true);
        }
        // A leaf or a feather: a small shard, always side on to the eye.
        private static void RangedLeaf(ITechniqueFxSink s,Vector3 p,float roll,float size,Vector3 color)
        {
            var along=new Vector3((float)Math.Cos(roll),.5f*(float)Math.Sin(roll*1.7f),(float)Math.Sin(roll));s.Line(false,p-along*size,p+along*size,color,size*.75f,true);
        }

        // ---- Pistol: Flash Round ----------------------------------------------------------------------------------------
        // A strobing white pill on a bright thread: that strobe is the warning to look away. Then one white ball taller than a pilot, thin rays in every direction, a flat lens bar across it,
        // the reach it dazzles racing out over the floor and in the air, and a magnesium ember left falling and spitting. The whiteout on a victim's screen is drawn from their mark, not here.
        private static void FlashRound(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var at=N(k.Point);var d=RangedNorm(at-from,Vector3.UnitZ);var white=FlashWhite+Vector3.One*.6f;
            if(t<k.Release){float g=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Sprite(false,from,white*(.6f+2*g),.14f+.24f*g,Sparkle);return;}
            Flash(s,from,t-k.Release,.12f,FlashWhite,.9f,seed);
            if(t<k.Arm)
            {
                float u=Clamp01((t-k.Release)/Math.Max(.03f,k.Arm-k.Release)),strobe=.5f+.5f*Pulse(t,70,2);var head=Vector3.Lerp(from,at,u);var tail=Vector3.Lerp(from,at,Math.Max(0,u-.4f));
                s.Line(false,tail,head,FlashWhite*1.1f,.12f,false);s.Line(false,tail,head,white*2.8f,.035f,true);
                s.Sprite(false,head,white*(2.6f*strobe),.9f,Sparkle);s.Sprite(false,head,Vector3.One*(3.8f*strobe),.34f,Hot);s.Glow(head,FlashWhite*(1.3f*strobe),0,1.1f,Core,0,seed+1);
                return;
            }
            float age=t-k.Arm,pop=1-Clamp01(age/.36f);pop*=pop;
            if(pop>0)
            {
                s.Sprite(false,at,white*(6*pop),2+3.6f*OutCubic(age/.1f),Hot);s.Glow(at,FlashWhite*(3.4f*pop),0,3.2f+3.4f*OutCubic(age/.2f),Core,age/.36f,seed+2);s.Glow(at,white*(2.2f*pop),0,1.6f,Core,age/.36f,seed+3);
                int n=Count(18,q,low);float grow=OutCubic(age/.07f),ray=pop*(.5f+.5f*pop);
                for(int i=0;i<n;i++){var dir=RandomDir(seed,i*7+3);float len=FlashReach*(.5f+.65f*R(seed,i+60))*grow;s.Line(false,at+dir*.3f,at+dir*len,white*(2.8f*ray),.07f+.08f*R(seed,i+90),true);}
                var side=RangedSide(d);s.Line(false,at-side*(10*grow),at+side*(10*grow),white*(3.2f*ray),.14f,true);s.Line(false,at-Up*(5*grow),at+Up*(5*grow),white*(1.8f*ray),.08f,true);
            }
            float reach=Clamp01(age/.44f);
            if(reach<1)
            {
                float rr=FlashReach*OutCubic(age/.16f),b=1-reach;var floor=new Vector3(at.X,scene.Feet(a).Y+.05f,at.Z);int seg=low?24:48;
                Ring(s,floor,rr,.3f+.6f*reach,FlashWhite*(1.5f*b),seg);Ring(s,floor,rr*.84f,.09f,white*(1.2f*b),seg);Hoop(s,at,d,rr,.24f*b+.03f,white*(1.5f*b),low?20:40,1);
            }
            if(t>=k.Until)return;
            float env=1-Smooth((age-.55f)/Math.Max(.05f,k.Until-k.Arm-.55f)),burn=.72f+.28f*(float)(Math.Sin(t*53)*Math.Sin(t*31));Vector3 Ember(float x)=>at-Up*(1.1f*x*x);var e=Ember(age);
            s.Sprite(false,e,white*(3.2f*burn*env),.6f,Hot);s.Sprite(false,e,white*(2.2f*burn*env),1.1f,Sparkle);s.Glow(e,FlashWhite*(1.2f*burn*env),0,1.6f,Core,0,seed+5);
            Spray(s,e,t,.3f,Count(7,q,low),3.4f,FlashWhite,seed+6,9,.02f,default);
            if(!low){int m=Count(9,q,low);for(int j=0;j<m;j++){float born=age*(j+.5f)/m,own=age-born;s.Smoke(Ember(born)+new Vector3(.05f*(float)Math.Sin(j*2.4f),own*.35f,.05f*(float)Math.Cos(j*2.4f)),new Vector3(.8f,.78f,.72f),.4f*env*(1-Clamp01(own/.9f)),.22f+.6f*own,.15f+.4f*own,seed+j,e.Y-3);}}
        }

        // ---- Pistol: Gun Spin -------------------------------------------------------------------------------------------
        // The whirl is the point: a brass pinwheel at the hand with the gun itself going round in it (nothing else on the pistol looks like this, and you cannot shoot through it), the old magazine dropping out.
        // A snap on the last turn; then, while it fires faster, the barrel glows and hot rings keep sliding off the muzzle, two brass sparks round the gun, a thread of smoke.
        private static void GunSpin(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var f=Flat(N(V3.Forward(a.Yaw)),Vector3.UnitZ);var r=RightOf(f);var hand=feet+Up*1.2f+f*.42f+r*.22f;float self=scene.Viewer==a.Id?.4f:1;var white=SpinBrass+Vector3.One;
            if(t<k.Arm)
            {
                // The wheel's plane is turned part way to the back, so it reads from behind the pilot as well as from the side.
                float span=Math.Max(.05f,k.Arm-k.At),g=Clamp01((t-k.At)/span),env=Smooth(g/.1f)*self,turn=6.2831853f*3.5f*g*(2-g);var wheel=Vector3.Normalize(f+r*.75f);Vector3 Spoke(float ang)=>wheel*(float)Math.Cos(ang)+Up*(float)Math.Sin(ang);
                int n=low?6:12;for(int i=0;i<n;i++){float a0=turn-i*.3f,a1=a0-.3f,b0=1-i/(float)n,b1=1-(i+1)/(float)n;s.Band(false,hand+Spoke(a0)*.05f,hand+Spoke(a1)*.05f,hand+Spoke(a1)*.36f,hand+Spoke(a0)*.36f,b0*env,b1*env,SpinBrass*1.8f);}
                Hoop(s,hand,RightOf(wheel),.32f,.05f,SpinBrass*(1.7f*env),low?12:22,1);
                var d=Spoke(turn);var grip=Spoke(turn-1.5707963f);s.Line(false,hand-d*.06f,hand+d*.3f,white*(1.2f*env),.07f,true);s.Line(false,hand,hand+grip*.13f,white*(.9f*env),.06f,true);s.Sprite(false,hand+d*.3f,white*(1.3f*env),.2f,Sparkle);
                Spray(s,hand,t,.2f,Count(3,q,low),2.8f,SpinBrass,seed+4,7,.016f,default);
                float fall=t-k.At;if(fall<.42f){var mag=hand-Up*(.1f+4.9f*fall*fall);var tilt=Vector3.Normalize(Up+r*(float)Math.Sin(fall*14)+f*(float)Math.Cos(fall*11));if(mag.Y>feet.Y)s.Line(false,mag-tilt*.07f,mag+tilt*.07f,CrateSteel*(1.5f*self),.05f,false);}
                return;
            }
            float age=t-k.Arm;
            if(age<.3f){float b=1-age/.3f;Flash(s,hand,age,.2f,SpinBrass,1.1f*self,seed);Hoop(s,hand,RightOf(Vector3.Normalize(f+r*.75f)),.3f+.42f*OutCubic(age/.2f),.09f*b+.01f,white*(.8f*b*self),low?12:22,1);}
            Sparks(s,hand,age,.4f,Count(14,q,low),4.4f,SpinBrass,seed+5,9,.02f,Up*.4f);
            if(t>=k.Until)return;
            float env2=Smooth(age/.1f)*(1-Smooth((t-(k.Until-.35f))/.35f))*self;var muzzle=hand+f*.3f+Up*.03f;
            s.Line(false,hand,muzzle,Flame*(1.5f*env2),.06f,false);s.Sprite(false,muzzle,FlameCore*(1.8f*env2),.2f,Hot);s.Glow(hand,SpinBrass*(.4f*env2),0,.8f,Core,0,seed+8);
            int rings=low?2:3;for(int i=0;i<rings;i++){float ph=(t*2.4f+i/(float)rings)%1;Hoop(s,muzzle+f*(.04f+.5f*ph),f,.04f+.17f*ph,.028f*(1-ph)+.008f,SpinBrass*(2*env2*(1-ph)),low?8:14,1);}
            for(int i=0;i<2;i++){float ang=t*9+i*3.14159f;s.Sprite(false,hand+f*.12f+(r*(float)Math.Cos(ang)+Up*(float)Math.Sin(ang))*.2f,SpinBrass*(2.4f*env2),.09f,Hot);}
            if(!low){int m=Count(5,q,low);for(int i=0;i<m;i++){float ph=(t*.9f+R(seed,i+30))%1;s.Smoke(muzzle+Up*(.8f*ph)+r*(.07f*(float)Math.Sin(t*3+i)),new Vector3(.5f,.5f,.5f),.3f*env2*(1-ph),.18f+.45f*ph,.2f+.5f*ph,seed+i,feet.Y);}}
        }

        // ---- Pistol: Slide Shot -----------------------------------------------------------------------------------------
        // Chevrons run along the floor to a bar at the far end from the moment of the press: where the slide goes, and where it stops with no dodge left. Two bright heel lines burn along it, sparks and a rooster tail of
        // dust behind the heels, and the end kicks up a crescent of dirt.
        private static void SlideShot(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var to=N(k.To);var dir=Flat(to-from,Flat(N(k.Dir),Vector3.UnitZ));var r=RightOf(dir);float span=Math.Max(.05f,k.Until-k.Release),u=Clamp01((t-k.Release)/span),marks=Smooth((t-k.At)/.06f)*(1-Smooth((t-k.Until-.1f)/.75f));var fl=Up*.09f;var head=Vector3.Lerp(from,to,u);
            int chev=low?4:6;for(int i=0;i<chev;i++){float ui=(i+.6f)/chev,b=marks*(ui>u?2.6f:.5f);var p=Vector3.Lerp(from,to,ui)+fl;s.Line(false,p-r*.62f-dir*.46f,p,SlideDust*b,.15f,true);s.Line(false,p+r*.62f-dir*.46f,p,SlideDust*b,.15f,true);}
            float stop=marks*(t<k.Until?1.3f+.5f*(float)Math.Sin(t*30):.5f);var end=to+fl;s.Line(false,end-r*1.05f,end+r*1.05f,(SlideDust+Vector3.One*.3f)*(1.9f*stop),.16f,false);s.Line(false,end-r*1.05f,end-r*1.05f-dir*.5f,SlideDust*(1.8f*stop),.1f,false);s.Line(false,end+r*1.05f,end+r*1.05f-dir*.5f,SlideDust*(1.8f*stop),.1f,false);
            if(t<k.Release)return;
            var hot=new Vector3(1,.62f,.24f);float scorch=1-Smooth((t-k.Until)/.9f);
            Strip(s,from+fl,head+fl,.46f,SlideDust,.1f*scorch,.6f*scorch);s.Line(false,from-r*.16f+fl,head-r*.16f+fl,hot*(1.9f*scorch),.04f,true);s.Line(false,from+r*.16f+fl,head+r*.16f+fl,hot*(1.9f*scorch),.04f,true);
            int n=Count(20,q,low);for(int i=0;i<n;i++){float ui=(i+.5f)/n,own=t-(k.Release+ui*span);if(own<0||own>1)continue;float sd=R(seed,i+1)-.5f;var p=Vector3.Lerp(from,to,ui)+Up*(.16f+.65f*own*(.5f+R(seed,i+2)))+r*(sd*(.45f+.9f*own));s.Smoke(p,new Vector3(.46f,.4f,.32f),.7f*(1-own)*(1-own),.42f+1.25f*own,.1f+.5f*own,seed+i,from.Y-.1f);}
            if(t<k.Until)
            {
                Spray(s,head+Up*.06f,t,.22f,Count(10,q,low),6.5f,hot,seed+30,10,.024f,-dir*1.3f+Up*.55f);s.Sprite(false,head+Up*.08f,hot*2.4f,.34f,Hot);
                for(int sd=-1;sd<=1;sd+=2){var o=r*(sd*.42f)+Up*.5f;s.Line(false,head-dir*2+o,head+o,Vector3.One*.9f,.018f,true);}
            }
            float after=t-k.Until;if(after<0)return;
            Dust(s,to,after,.65f,Count(10,q,low),1.4f,new Vector3(.5f,.43f,.34f),.75f,seed+50);Sparks(s,to+Up*.1f,after,.36f,Count(12,q,low),5.2f,SlideDust,seed+60,10,.022f,dir*1.1f+Up*.35f);
        }

        // ---- Machine gun: Bipod -----------------------------------------------------------------------------------------
        // Three steel legs punch out of the mount and bite the floor with dust and cracks; a belt climbs from a box at its side, crawling, for the rounds it no longer spends; the barrel heats from steel to orange; a narrow
        // wedge lies on the floor where the gun looks and a ring round the boots: it is pinned there, and anywhere off that wedge is the way in. A pack-up folds it all back.
        private static void Bipod(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=N(k.Point);var aim=RangedNorm(N(k.Dir),Vector3.UnitZ);var f=Flat(aim,Vector3.UnitZ);var r=RightOf(f);
            float set=Smooth((t-k.At)/Math.Max(.05f,k.Release-k.At)),away=k.Recast(t)?Smooth((t-k.RecastAt)/Math.Max(.05f,k.Until-k.RecastAt)):Smooth((t-k.Until)/.25f),ext=set*(1-away),age=t-k.Release;if(ext<=.002f)return;
            var mount=feet+Up*1.2f+f*.62f+r*.14f;var steel=BipodSteel+Vector3.One*.2f;
            void Leg(float along,float side,int i)
            {
                var tip=Vector3.Lerp(mount,feet+f*along+r*side+Up*.02f,ext);var mid=Vector3.Lerp(mount,tip,.55f);
                s.Line(false,mount,tip,BipodSteel*(.7f*ext),.1f,false);s.Line(false,mount,mid,steel*(1.5f*ext),.045f,true);s.Line(false,mid,tip,steel*(1.1f*ext),.03f,true);s.Sprite(false,mid,steel*(1.8f*ext),.12f,Hot);s.Sprite(false,tip,BipodHeat*(2*ext),.14f,Hot);
                if(age>=0&&age<.9f){Cracks(s,tip,age,.9f,low?3:5,.55f,BipodHeat,seed+i*11);Sparks(s,tip+Up*.04f,age,.3f,Count(6,q,low),3.2f,BipodHeat+new Vector3(0,.3f,.2f),seed+i*13,9,.018f,Up*.5f);Dust(s,tip,age,.6f,Count(4,q,low),.6f,new Vector3(.36f,.33f,.28f),.6f,seed+i*17);}
            }
            Leg(1.3f,.5f,1);Leg(1.3f,-.22f,2);Leg(.34f,.14f,3);
            s.Sprite(false,mount,steel*(2.2f*ext),.16f,Hot);Hoop(s,mount,aim,.1f,.035f,steel*(1.7f*ext),low?8:12,1);
            if(age<0)return;
            float env=Smooth(age/.2f)*(1-away),heat=Clamp01(age/4);var muzzle=mount+aim*1.05f+Up*.04f;
            s.Line(false,mount,muzzle,Vector3.Lerp(BipodSteel,BipodHeat,heat)*((1.2f+1.4f*heat)*env),.055f,false);s.Sprite(false,muzzle,BipodHeat*((.6f+2*heat)*env),.12f+.14f*heat,Hot);
            // The stance and the wedge.
            float sl=age<.3f?1-age/.3f:0;if(sl>0)Ring(s,feet+Up*.04f,2.2f*OutCubic(age/.3f),.2f,BipodHeat*(1.2f*sl),low?16:32);
            Ring(s,feet+Up*.04f,.82f,.09f,BipodHeat,low?16:28,1.1f*env);
            var w0=feet+Up*.05f+f*1.35f;var w1=feet+Up*.05f+f*13;Strip(s,w0,w1,.62f,BipodHeat,.5f*env,.06f*env);
            for(int sd=-1;sd<=1;sd+=2)s.Line(false,w0+r*(sd*.14f),w1+r*(sd*.95f),BipodHeat*(1.3f*env),.045f,true);
            // The box and the belt.
            float boxIn=Smooth(age/.2f)*(1-away);var box=feet+f*1.02f+r*.82f+Up*.17f;RangedBox(s,box,f*(.24f*boxIn),Up*(.16f*boxIn),r*(.15f*boxIn),CrateSteel*(1.5f*env),.03f);
            var top=box+Up*.16f;int n=low?7:14;var prev=top;
            for(int i=0;i<n;i++)
            {
                float v=(i+1f)/n,b=(.75f+.25f*(float)Math.Sin(v*22-t*14))*env;var p=Vector3.Lerp(top,mount,v*boxIn)-Up*(.22f*4*v*(1-v));
                s.Line(false,prev,p,Brass*(1.5f*b),.04f,true);if(!low)s.Line(false,p-r*.055f,p+r*.055f,Brass*(1.3f*b),.02f,true);prev=p;
            }
            if(!low&&heat>.1f){int m=Count(5,q,low);for(int i=0;i<m;i++){float ph=(t*.8f+R(seed,i+30))%1;s.Smoke(muzzle+Up*(.9f*ph)+r*(.08f*(float)Math.Sin(t*2.6f+i)),new Vector3(.5f,.49f,.48f),.32f*env*heat*(1-ph),.2f+.5f*ph,.2f+.5f*ph,seed+i,feet.Y);}}
        }

        // ---- Machine gun: Ammo Crate ------------------------------------------------------------------------------------
        // A marker on the floor, then a steel crate with hazard stripes falls onto it and thumps. Its lid swings up on a glow and rows of brass; a big cartridge floats over it so anyone can see what it is from any side;
        // the ring round it is its health, gold to red and shrinking; cracked, it spits and smokes. Taking from it throws brass to whoever took it. Shot out it comes apart in boards and cook-offs; left alone it folds away.
        private static void AmmoCrate(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var c0=N(k.Point);float ang=R(seed,1)*6.2831853f,hp=Clamp01(k.Arm);var ex=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));var ez=RightOf(ex);const float Hx=.66f,Hy=.38f,Hz=.42f;
            if(t<k.Release)
            {
                float g=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));Ring(s,c0+Up*.04f,1.2f-.3f*g,.07f,CrateGold,low?16:28,1.3f*g);
                for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2){var co=c0+Up*.05f+ex*(sx*Hx)+ez*(sz*Hz);s.Line(false,co,co-ex*(sx*.2f),CrateGold*(1.8f*g),.04f,true);s.Line(false,co,co-ez*(sz*.2f),CrateGold*(1.8f*g),.04f,true);}
                return;
            }
            const float Fall=.16f;float land=k.Release+Fall,age=t-land,size=1,gone=t-k.Until;bool broken=hp<.34f;
            if(gone>=0)
            {
                var mid=c0+Up*Hy;
                if(broken)
                {
                    Flash(s,mid,gone,.28f,FlakOrange,2.2f,seed+40);Sparks(s,mid,gone,.7f,Count(26,q,low),7,Brass,seed+41,10,.026f,Up*.5f);Dust(s,c0,gone,.8f,Count(10,q,low),1.8f,new Vector3(.26f,.24f,.22f),.8f,seed+42);
                    int n=Count(14,q,low);if(gone<.85f)for(int i=0;i<n;i++){var v=RandomDir(seed,i*9+7);v.Y=Math.Abs(v.Y)+.25f;var p=mid+v*(4.2f*gone)-Up*(5.5f*gone*gone);if(p.Y<c0.Y)continue;float spin=gone*(6+9*R(seed,i+50));var o=new Vector3((float)Math.Cos(spin),(float)Math.Sin(spin),(float)Math.Sin(spin*.7f));s.Line(false,p-o*.2f,p+o*.2f,CrateSteel*(1.5f*(1-gone/.85f)),.055f,false);}
                    return;
                }
                size=1-Smooth(gone/.22f);if(size<=0)return;
            }
            float fall=age<0?1-(1+age/Fall)*(1+age/Fall):0;var c=c0+Up*(Hy*size+2.4f*fall);var x=ex*(Hx*size);var y=Up*(Hy*size);var z=ez*(Hz*size);
            var frame=Vector3.Lerp(new Vector3(1,.2f,.12f),CrateSteel,Smooth(hp/.6f))*1.6f;
            // Mass first: a soft fill on the faces under the frame, then the frame, its straps and the hazard stripes.
            s.Band(false,c+y-x-z,c+y+x-z,c+y+x+z,c+y-x+z,.5f,.5f,CrateSteel);s.Band(false,c-z-x-y,c-z+x-y,c-z+x+y,c-z-x+y,.45f,.45f,CrateSteel);s.Band(false,c+z-x-y,c+z+x-y,c+z+x+y,c+z-x+y,.45f,.45f,CrateSteel);
            RangedBox(s,c,x,y,z,frame,.05f*Math.Max(.3f,size));
            int stripes=low?3:5;for(int sd=-1;sd<=1;sd+=2)for(int i=0;i<stripes;i++){float px=((i+.5f)/stripes*2-1)*.82f;var b0=c+z*sd+x*px-y*.86f;s.Line(false,b0,b0+y*1.72f+x*.2f,CrateGold*1.9f,.06f,true);}
            if(!low)for(int sd=-1;sd<=1;sd+=2)s.Line(false,c+x*(sd*.97f)-z-y*.2f,c+x*(sd*.97f)+z-y*.2f,CrateGold*1.6f,.05f,true);
            if(age<0||gone>=0&&size<=.01f)return;
            if(age<.7f)
            {
                Dust(s,c0,age,.7f,Count(10,q,low),1.8f,new Vector3(.4f,.37f,.3f),.75f,seed+2);Sparks(s,c0+Up*.08f,age,.36f,Count(12,q,low),3.8f,CrateGold,seed+3,9,.024f,Up*.2f);
                float sl=Clamp01(age/.3f);if(sl<1)Ring(s,c0+Up*.04f,2.4f*OutCubic(sl),.2f,CrateGold*(1.4f*(1-sl)),low?16:32);
            }
            // The lid.
            float open=Smooth((age-.2f)/.28f),th=open*1.3f;var hinge=c+y-z;var lz=(ez*(float)Math.Cos(th)+Up*(float)Math.Sin(th))*(2*Hz*size);var top=c+y;
            s.Line(false,hinge-x+lz,hinge+x+lz,frame,.05f,false);s.Line(false,hinge-x,hinge-x+lz,frame,.04f,false);s.Line(false,hinge+x,hinge+x+lz,frame,.04f,false);s.Line(false,hinge-x+lz*.5f,hinge+x+lz*.5f,CrateGold*1.4f,.03f,true);
            if(open<=.02f)return;
            float env=open*size,throb=.8f+.2f*(float)Math.Sin(t*5);
            s.Sprite(false,top,CrateGold*(1.3f*env*throb),1.3f,Soft);s.Glow(top,CrateGold*(.8f*env),0,.9f,Core,0,seed+5);
            int rows=low?5:9;for(int row=-1;row<=1;row+=2)for(int i=0;i<rows;i++){float px=((i+.5f)/rows*2-1)*.8f;var b=top+x*px+z*(row*.4f)-Up*.04f;s.Line(false,b,b+Up*(.17f*env),Brass*(2*env),.06f,false);s.Sprite(false,b+Up*(.2f*env),(Brass+Vector3.One*.4f)*(1.7f*env),.05f,Hot);}
            int motes=Count(10,q,low);for(int i=0;i<motes;i++){float ph=(t*.7f+R(seed,i+10))%1;s.Glow(top+x*(R(seed,i+11)-.5f)*1.3f+z*(R(seed,i+12)-.5f)*1.3f+Up*(1.7f*ph),CrateGold*(1.7f*env*(1-ph)),0,.07f,Core,0,seed+i);}
            // The cartridge overhead, the same from every side.
            var icon=top+Up*(.72f+.07f*(float)Math.Sin(t*3));s.Line(false,icon-Up*.16f,icon+Up*.08f,Brass*(2.4f*env),.2f,false);s.Line(false,icon+Up*.08f,icon+Up*.29f,new Vector3(1,.5f,.28f)*(2.2f*env),.19f,true);s.Glow(icon,CrateGold*(.45f*env),0,.9f,Core,0,seed+6);
            // Its health: an arc on the floor that shrinks and reddens.
            var hc=Vector3.Lerp(new Vector3(1,.14f,.08f),CrateGold,Smooth(hp/.6f));int seg=low?14:28,lit=(int)Math.Ceiling(seg*hp);RangedArc(s,c0+Up*.05f,1.12f,.12f,ang,lit,seg,hc,1.7f*size);
            if(hp<.6f&&gone<0)
            {
                float d=1-hp/.6f;Spray(s,c+x*.8f+y,t,.4f,Count(4,q,low),3,FlakOrange,seed+20,9,.02f,Up*.8f);
                if(!low){int m=Count(6,q,low);for(int i=0;i<m;i++){float ph=(t*.7f+R(seed,i+31))%1;s.Smoke(c+y+x*(.7f*(R(seed,i+32)-.5f))+Up*(1.3f*ph),new Vector3(.16f,.15f,.15f),.55f*d*(1-ph),.3f+.7f*ph,.2f+.4f*ph,seed+i,c0.Y);}}
            }
            if(k.Hit(t))
            {
                float since=t-k.HitAt;var to=N(k.To);
                if(since<.6f)
                {
                    Flash(s,top,since,.18f,CrateGold,1.1f,seed+7);int n=Count(14,q,low);
                    for(int i=0;i<n;i++){float g=Clamp01((since-i*.018f)/.22f);if(g<=0||g>=1)continue;var p=Vector3.Lerp(top,to,g)+Up*(1*(float)Math.Sin(3.14159f*g));var q0=Vector3.Lerp(top,to,Math.Max(0,g-.14f))+Up*(1*(float)Math.Sin(3.14159f*Math.Max(0,g-.14f)));s.Line(false,q0,p,Brass*2.4f,.04f,true);s.Sprite(false,p,(Brass+Vector3.One*.4f)*2.2f,.14f,Hot);}
                    float land2=since-.22f;if(land2>=0){Flash(s,to,land2,.24f,CrateGold,1.3f,seed+8);float g2=Clamp01(land2/.3f);Hoop(s,to,Up,.4f+.6f*g2,.09f*(1-g2)+.01f,CrateGold*(1.8f*(1-g2)),low?12:22,1);}
                }
            }
        }
        // `lit` of `seg` pieces of a floor ring, from `turn`.
        private static void RangedArc(ITechniqueFxSink s,Vector3 c,float radius,float width,float turn,int lit,int seg,Vector3 color,float b)
        {
            float inner=radius-width*.5f,outer=radius+width*.5f;
            for(int i=0;i<lit&&i<seg;i++){float a0=turn+6.2831853f*i/seg,a1=turn+6.2831853f*(i+1)/seg;var d0=new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0));var d1=new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1));s.Band(false,c+d0*inner,c+d1*inner,c+d1*outer,c+d0*outer,b,b,color);}
        }

        // ---- Machine gun: Flak Burst ------------------------------------------------------------------------------------
        // A fat tracer, then a black-and-orange air burst with shrapnel thrown to the cloud's edge. The cloud is real soot, hung with shrapnel glitter and little orange pops, its reach marked in dashes on the floor so you can walk round it.
        // A shot shredded in it is a spark and a crack at the place it died.
        private static void FlakBurst(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var c=N(k.Point);var d=RangedNorm(c-from,Vector3.UnitZ);float floorY=scene.Feet(a).Y;
            if(t<k.Release){s.Sprite(false,from,FlakOrange*(1+2.4f*Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At))),.26f,Hot);return;}
            Flash(s,from,t-k.Release,.16f,FlakOrange,1.1f,seed);
            if(t<k.Arm)
            {
                float u=Clamp01((t-k.Release)/Math.Max(.03f,k.Arm-k.Release));var head=Vector3.Lerp(from,c,u);var tail=Vector3.Lerp(from,c,Math.Max(0,u-.3f));
                s.Line(false,tail,head,FlakOrange*1.1f,.14f,false);s.Line(false,tail,head,(FlakOrange+Vector3.One*.4f)*2.2f,.05f,true);s.Sprite(false,head,(FlakOrange+Vector3.One*.5f)*2.6f,.42f,Hot);
                return;
            }
            float age=t-k.Arm;
            Flash(s,c,age,.34f,FlakOrange,6.5f,seed+1);if(age<.1f)s.Sprite(false,c,(FlakOrange+Vector3.One)*(4*(1-age/.1f)),4,Hot);
            float shell=Clamp01(age/.32f);if(shell<1){Hoop(s,c,d,FlakReach*OutCubic(age/.2f),.34f*(1-shell)+.03f,(FlakOrange+Vector3.One*.3f)*(1.7f*(1-shell)),low?20:40,1);}
            float life=Math.Max(.05f,k.Until-k.Arm);
            if(age<.42f){int n=Count(30,q,low);for(int i=0;i<n;i++){var dir=RandomDir(seed,i*11+5);float own=Clamp01(age/(.16f+.26f*R(seed,i+70))),rr=FlakReach*(.55f+.5f*R(seed,i+71))*OutCubic(own);if(own<1)s.Line(false,c+dir*(rr*.6f),c+dir*rr,(FlakOrange+Vector3.One*.3f)*(2.4f*(1-own)),.05f,true);}}
            if(t>=k.Until)return;
            float env=Smooth(age/.12f)*(1-Smooth((age-(life-.8f))/.8f)),grow=OutCubic(age/.32f),rad=FlakReach*grow;
            int puffs=Count(38,q,low);
            for(int i=0;i<puffs;i++)
            {
                float own=(i+.5f)/puffs,yy=1-2*own,flat=(float)Math.Sqrt(Math.Max(0,1-yy*yy)),ang=2.39996f*i+t*.1f*(R(seed,i+1)-.5f),lay=.45f+.55f*R(seed,i+2);var p=c+new Vector3((float)Math.Cos(ang)*flat,yy*.62f,(float)Math.Sin(ang)*flat)*(rad*lay*.86f)+Up*(.1f*(float)Math.Sin(t*.8f+i));
                float tone=.07f+.09f*R(seed,i+3);s.Smoke(p,new Vector3(tone*1.15f,tone,tone*.9f),.72f*env,(2.1f+1.3f*R(seed,i+4))*(.6f+.4f*grow),.16f+.32f*Clamp01(age/life)+.1f*R(seed,i+5),seed+i,floorY-.1f);
            }
            // Pops inside it, each at a fresh place.
            int pops=Count(10,q,low);
            for(int i=0;i<pops;i++)
            {
                float x=t/.62f+R(seed,i+20);long gen=(long)Math.Floor(x);float own=(x-gen)*.62f;if(own>.2f)continue;var p=c+RandomDir(seed+gen*131,i)*(rad*.8f*(.3f+.7f*R(seed+gen*17,i+21)));p.Y=c.Y+(p.Y-c.Y)*.62f;
                Flash(s,p,own,.2f,FlakOrange,1.3f*env,seed+gen*7+i);if(!low)Sparks(s,p,own,.2f,4,4.4f,FlakOrange+new Vector3(0,.3f,.2f),seed+gen*5+i,5,.018f);
            }
            int bits=Count(44,q,low);
            for(int i=0;i<bits;i++)
            {
                float tw=(float)Math.Pow(.5f+.5f*Math.Sin(t*(4+9*R(seed,i+30))+R(seed,i+31)*30),8);if(tw<.03f)continue;var o=RandomDir(seed,i*13+2)*(rad*(.25f+.75f*R(seed,i+32)));float ca=(float)Math.Cos(t*.25f),sa=(float)Math.Sin(t*.25f);s.Sprite(false,c+new Vector3(o.X*ca-o.Z*sa,o.Y*.62f,o.X*sa+o.Z*ca),(FlakOrange+Vector3.One*.7f)*(3.2f*tw*env),.16f,Sparkle);
            }
            RangedDashes(s,new Vector3(c.X,floorY+.05f,c.Z),FlakReach*grow,.16f,t*.12f,low?14:26,.6f,FlakOrange,1.3f*env);
            if(k.Hit(t)){float since=t-k.HitAt;var to=N(k.To);Flash(s,to,since,.22f,FlakOrange,1.5f,seed+40);Sparks(s,to,since,.36f,Count(16,q,low),7,FlakOrange+new Vector3(0,.3f,.2f),seed+41,7,.022f);if(since<.12f)s.Sprite(false,to,Vector3.One*(3.4f*(1-since/.12f)),.8f,Sparkle);}
        }

        // ---- Rifle: Breacher Round --------------------------------------------------------------------------------------
        // For the whole charge a laser runs the round's full line, with its shadow on the floor, coils of light squeezing into the muzzle faster and faster and any wall it will go through already starting to glow. Then a white hair, a
        // heavy beam with a corkscrew round it and rings standing along it; where it went through a wall the hole stays molten, spraying inside and out.
        private static void BreacherRound(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var to=N(k.To);var d=RangedNorm(to-from,RangedNorm(N(k.Dir),Vector3.UnitZ));var side=RangedSide(d);var lift=Vector3.Normalize(Vector3.Cross(side,d));float floorY=scene.Feet(a).Y+.05f,len=Vector3.Distance(from,to);var white=BreachLight+Vector3.One*.6f;var path=k.Path;
            if(t<k.Release)
            {
                float g=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At)),env=Smooth(g/.08f),pulse=.72f+.28f*(float)Math.Sin((t-k.At)*(20+70*g));var tip=Vector3.Lerp(from,to,OutCubic(g/.1f));
                s.Line(false,from,tip,BreachLight*((.5f+.5f*g)*env*pulse),.09f+.05f*g,false);s.Line(false,from,tip,white*((1.5f+1.8f*g)*env),.016f+.014f*g,true);s.Sprite(false,tip,white*((1.6f+1.2f*g)*env*pulse),.34f,Sparkle);
                Strip(s,new Vector3(from.X,floorY,from.Z),new Vector3(tip.X,floorY,tip.Z),.09f+.07f*g,BreachLight,.9f*env,.5f*env);
                int coils=low?2:3;for(int i=0;i<coils;i++){float ph=(t*(2.2f+3.4f*g)+i/(float)coils)%1;Hoop(s,from+d*(.95f*(1-ph)),d,.06f+.36f*(1-ph),.035f,BreachLight*((.7f+1.7f*g)*ph*env),low?10:18,1);}
                s.Glow(from,BreachLight*(.5f+1.7f*g*g),0,.24f+.6f*g,Core,0,seed);s.Sprite(false,from,white*(.8f+2.4f*g),.14f+.3f*g,Hot);
                if(path!=null)for(int i=0;i<path.Length;i+=2){var w=N(path[i]);s.Sprite(false,w,BreachWall*(1.4f*g*pulse),.2f+.3f*g,Hot);Hoop(s,w,d,.14f+.12f*(1-g),.03f,BreachWall*(1.5f*g),low?8:14,1);}
                return;
            }
            float age=t-k.Release;
            Flash(s,from,age,.24f,BreachLight,2.2f,seed+1);
            if(age<.16f){float v=1-age/.16f;for(int sd=-1;sd<=1;sd+=2){s.Tongue(false,from,from+side*(sd*.85f)-d*.12f,white*(2.2f*v),.14f);}Hoop(s,from,d,.2f+.8f*(1-v),.09f*v+.01f,white*(1.6f*v),low?10:18,1);}
            if(age<.08f){float v=1-age/.08f;s.Line(false,from,to,(BreachLight+Vector3.One)*(5.5f*v),.06f,true);s.Line(false,from,to,BreachLight*(1.9f*v),.55f,false);}
            float b=1-Clamp01(age/.6f);b*=b;
            if(b>0)
            {
                s.Line(false,from,to,white*(1.9f*b),.035f+.06f*b,true);s.Line(false,from,to,BreachLight*(.6f*b),.3f,false);
                float helix=Math.Min(len,24),rad=.08f+.42f*OutCubic(age/.5f);int n=Count(52,q,low);Vector3 Screw(float x)=>from+d*x+(side*(float)Math.Cos(x*2.8f+age*7)+lift*(float)Math.Sin(x*2.8f+age*7))*rad;
                var last=Screw(0);for(int i=1;i<=n;i++){float x=helix*i/n;var p=Screw(x);s.Line(false,last,p,BreachLight*(1.5f*b*(1-.6f*i/n)),.028f,false);last=p;}
                int rings=low?3:6;for(int j=0;j<rings;j++){float x=Math.Min(len,36)*(j+1f)/(rings+1);Hoop(s,from+d*x,d,.14f+.75f*OutCubic(age/.42f),.12f*b+.02f,BreachLight*(1.3f*b),low?10:18,1);}
            }
            if(path!=null)for(int i=0;i<path.Length;i+=2)
            {
                var w0=N(path[i]);var w1=i+1<path.Length?N(path[i+1]):w0;float cool=1-Clamp01(age/1.3f),hot=cool*cool;var glow=Vector3.Lerp(BreachWall,Vector3.One*1.6f,hot);
                if(cool>0){Hoop(s,w0,d,.2f,.1f,glow*(1.8f*cool),low?10:16,1);Hoop(s,w1,d,.28f,.13f,glow*(1.8f*cool),low?10:16,1);s.Line(false,w0,w1,glow*(2.4f*hot),.16f,true);s.Sprite(false,w1,BreachWall*(1.6f*cool),.7f,Hot);}
                Sparks(s,w0,age,.42f,Count(16,q,low),6,BreachWall+new Vector3(0,.3f,.1f),seed+i*3+7,9,.026f,-d*1.1f);Sparks(s,w1,age,.65f,Count(30,q,low),10,BreachWall+new Vector3(0,.3f,.1f),seed+i*3+8,9,.032f,d*1.5f);
                int puffs=Count(9,q,low);if(age<.9f)for(int j=0;j<puffs;j++){float own=age/.9f;s.Smoke(w1+d*(.3f+2.4f*own*R(seed,j+i+50))+RandomDir(seed,j*7+i)*(.15f+.7f*own),new Vector3(.34f,.32f,.3f),.7f*(1-own),.4f+1.1f*own,.15f+.5f*own,seed+j,floorY-.2f);}
            }
            if(k.Hit(t)){float since=t-k.HitAt;if(since<.16f)s.Line(false,from,to,Vector3.One*(2.8f*(1-since/.16f)),.11f,true);}
        }

        // ---- Rifle: Ghillie ---------------------------------------------------------------------------------------------
        // Leaves spiral down and settle on the pilot while grass stands up round the boots and moss smoke hangs low; the runtime fades the body. Once hidden, only the shimmer that gives it away up close: faint wavering rings
        // climbing a body's height, and a glint or two. When it breaks, the whole cover is thrown off in a burst of leaves.
        private static void Ghillie(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var c=new Vector3(feet.X,k.Point.y,feet.Z);float span=Math.Max(.05f,k.Arm-k.At),age=t-k.At;var dark=GhillieGreen*.75f;
            if(t>=k.Until)
            {
                float after=t-k.Until;if(after>.85f)return;int n=Count(34,q,low);float f0=1-after/.85f;
                Flash(s,c+Up*1,after,.16f,GhillieGreen,1.3f,seed+50);
                for(int i=0;i<n;i++){var v=RandomDir(seed,i*5+3);v.Y=Math.Abs(v.Y)*.8f+.25f;var p=c+Up*(.3f+1.3f*R(seed,i+60))+v*(3.6f*(1-(float)Math.Exp(-after*3.4f)))-Up*(.9f*after*after);RangedLeaf(s,p,R(seed,i+61)*6.28f+after*(4+7*R(seed,i+62)),.09f+.05f*R(seed,i+63),GhillieGreen*(2*f0));}
                return;
            }
            float settled=Smooth((t-k.Arm)/.35f);
            if(settled<1)
            {
                int n=Count(34,q,low);
                for(int i=0;i<n;i++)
                {
                    float ph=Clamp01((age-R(seed,i+1)*.45f*span)/(span*.72f));if(ph<=0)continue;float rest=.22f+.14f*R(seed,i+2),rad=rest+1.5f*(1-ph),y=(.2f+1.4f*R(seed,i+3))*ph+(2.7f+.5f*R(seed,i+4))*(1-ph),ang=6.2831853f*R(seed,i+5)+(1-ph)*(1-ph)*4;
                    RangedLeaf(s,c+new Vector3((float)Math.Cos(ang)*rad,y,(float)Math.Sin(ang)*rad),ang*1.7f+t*(ph<1?5:0),.075f+.05f*R(seed,i+6),Vector3.Lerp(GhillieGreen*2,new Vector3(.55f,.42f,.16f)*1.6f,R(seed,i+7)*.6f)*(1-settled));
                }
                if(!low){int m=Count(10,q,low);for(int i=0;i<m;i++){float ang=6.2831853f*R(seed,i+20),rad=.45f+.65f*R(seed,i+21);s.Smoke(c+new Vector3((float)Math.Cos(ang)*rad,.16f+.2f*R(seed,i+22),(float)Math.Sin(ang)*rad),new Vector3(.12f,.17f,.08f),.62f*Smooth(age/.25f)*(1-settled),.55f+.4f*R(seed,i+23),.3f,seed+i,c.Y-.05f);}}
            }
            int blades=Count(24,q,low);float tall=Smooth(age/.42f),grass=Math.Max(.12f,1-.9f*settled);
            for(int i=0;i<blades;i++)
            {
                float ang=6.2831853f*R(seed,i+30),rad=.34f+.62f*R(seed,i+31),h=(.32f+.48f*R(seed,i+32))*tall;var b=c+new Vector3((float)Math.Cos(ang)*rad,.02f,(float)Math.Sin(ang)*rad);var lean=new Vector3((float)Math.Sin(t*1.7f+i)*.05f+(float)Math.Cos(ang)*.1f,0,(float)Math.Cos(t*1.3f+i)*.05f+(float)Math.Sin(ang)*.1f);
                var m1=b+Up*(h*.55f)+lean*.4f;s.Line(false,b,m1,dark*(1.3f*grass),.045f,false);s.Line(false,m1,b+Up*h+lean,GhillieGreen*(1.5f*grass),.03f,true);
            }
            if(age<span)Ring(s,c+Up*.04f,.9f*Smooth(age/.2f),.1f,GhillieGreen,low?14:24,.9f*(1-age/span));
            float glim=Smooth((t-(k.Arm-.2f))/.3f)*(1-Smooth((t-(k.Until-.08f))/.08f));
            if(glim<=0)return;
            int hoops=low?2:3,seg=low?8:14;
            for(int h=0;h<hoops;h++)
            {
                float ph=(t*.34f+h/(float)hoops)%1,y=.12f+1.7f*ph,b=.42f*glim*(float)Math.Sin(3.14159f*ph),body=.27f+.08f*(float)Math.Sin(ph*3.14159f);Vector3 last=default;
                for(int i=0;i<=seg;i++){float ang=6.2831853f*i/seg,wob=body+.045f*(float)Math.Sin(ang*3+t*5+h*2);var p=c+new Vector3((float)Math.Cos(ang)*wob,y+.03f*(float)Math.Sin(ang*2+t*4),(float)Math.Sin(ang)*wob);if(i>0)s.Line(false,last,p,new Vector3(.86f,1,.8f)*b,.012f,true);last=p;}
            }
            int glints=low?3:6;for(int i=0;i<glints;i++){float tw=(float)Math.Pow(.5f+.5f*Math.Sin(t*(2.2f+2*R(seed,i+40))+R(seed,i+41)*20),10),ang=6.2831853f*R(seed,i+42)+t*.3f;if(tw>.05f)s.Sprite(false,c+new Vector3((float)Math.Cos(ang)*.32f,.2f+1.5f*R(seed,i+43),(float)Math.Sin(ang)*.32f),new Vector3(.9f,1,.8f)*(1.1f*tw*glim),.09f,Sparkle);}
        }

        // ---- Rifle: Bolas Round -----------------------------------------------------------------------------------------
        // A real bolas: three weights on cords whirling round a hub, face on to whoever it is coming at, in a wide bright ring you have time to see and roll from, a spiral trail behind it. It lands in three loops that tighten round the legs and
        // body with cords staked to the floor; they hum until they snap, and the weights drop.
        private static void BolasRound(ITechniqueFxSink s,IKitFxScene scene,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var end=N(k.Point);var d=RangedNorm(end-from,Flat(N(k.Dir),Vector3.UnitZ));var side=RangedSide(d);var lift=Vector3.Normalize(Vector3.Cross(side,d));var weight=BolasCord+Vector3.One*.5f;
            if(t<k.Release){float g=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Sprite(false,from,BolasCord*(1+2*g),.24f,Hot);return;}
            bool hit=k.Hit(t);float arrive=hit?k.HitAt:k.Until,fly=Math.Max(.03f,arrive-k.Release);
            Flash(s,from,t-k.Release,.14f,BolasCord,1.1f,seed);
            if(t<arrive)
            {
                float u=Clamp01((t-k.Release)/fly),open=OutCubic((t-k.Release)/.1f),rad=.16f+.46f*open;var c=Vector3.Lerp(from,end,u);Vector3 At(float tt,int i,float rr){float sp=tt*20+i*2.0943951f;return Vector3.Lerp(from,end,Clamp01((tt-k.Release)/fly))+(side*(float)Math.Cos(sp)+lift*(float)Math.Sin(sp))*rr;}
                Hoop(s,c,d,rad,.1f,BolasCord*.9f,low?12:20,1);s.Sprite(false,c,weight*2,.22f,Hot);
                for(int i=0;i<3;i++){var w=At(t,i,rad);s.Line(false,c,w,weight*1.7f,.03f,true);s.Sprite(false,w,weight*2.8f,.3f,Hot);s.Glow(w,BolasCord*1.2f,0,.26f,Core,0,seed+i);}
                int m=low?6:14;for(int i=0;i<3;i++){var prev=At(t,i,rad);for(int j=1;j<=m;j++){float back=t-j*.016f;if(back<k.Release)break;var p=At(back,i,rad);s.Line(false,prev,p,BolasCord*(1.4f*(1-j/(float)(m+1))),.03f,false);prev=p;}}
                return;
            }
            if(!hit)
            {
                float after=t-k.Until;Flash(s,end,after,.2f,BolasCord,1,seed+1);Sparks(s,end,after,.4f,Count(14,q,low),4,BolasCord,seed+2,9,.022f);
                if(after<.5f){for(int i=0;i<3;i++){var o=RandomDir(seed,i+9);var p=end+o*(1.6f*after)-Up*(4.9f*after*after);s.Line(false,end-Up*(4.9f*after*after),p,BolasCord*(1.3f*(1-after/.5f)),.03f,true);s.Sprite(false,p,weight*(2*(1-after/.5f)),.16f,Hot);}}
                return;
            }
            float since=t-k.HitAt;var who=ActorOf(scene,k.Target);var feet=who!=null?scene.Feet(who):end-Up*1.2f;var chest=feet+Up*1.15f;
            Flash(s,chest,since,.22f,BolasCord,1.5f,seed+3);Sparks(s,chest,since,.4f,Count(16,q,low),5,BolasCord,seed+4,9,.022f);
            if(t>=k.Until)
            {
                float after=t-k.Until;if(after>.5f)return;float f0=1-after/.5f;
                for(int i=0;i<3;i++){float ang=i*2.0943951f+1;var o=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));var p=feet+Up*(.3f+.35f*i)+o*(.45f+1.8f*after)-Up*(3.5f*after*after);if(p.Y<feet.Y+.03f)p.Y=feet.Y+.03f;s.Sprite(false,p,weight*(2*f0),.14f,Hot);s.Line(false,p,p-o*.5f+Up*.12f,BolasCord*(1.4f*f0),.025f,true);}
                Sparks(s,feet+Up*.7f,after,.3f,Count(8,q,low),3.2f,BolasCord,seed+5,8,.018f);
                return;
            }
            float tight=OutCubic(since/.22f),env=1-Smooth((t-(k.Until-.12f))/.12f),hum=.85f+.15f*(float)Math.Sin(t*44);
            for(int i=0;i<3;i++)
            {
                float y=.3f+.38f*i,rr=(1.05f-.73f*tight)*(i==1?.92f:1)+.01f*(float)Math.Sin(t*38+i);var n=Vector3.Normalize(Up+new Vector3((float)Math.Cos(i*2.3f),0,(float)Math.Sin(i*2.3f))*.26f);var ring=feet+Up*y;
                Hoop(s,ring,n,rr,.075f,BolasCord*(1.7f*env*hum),low?12:20,1);
                float ang=(1-tight)*(1-tight)*9+i*2.0943951f;var w=ring+new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*rr;s.Sprite(false,w,weight*(2.4f*env),.2f,Hot);
            }
            int stakes=low?3:4;for(int i=0;i<stakes;i++){float ang=6.2831853f*i/stakes+.4f;var o=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));var low0=feet+Up*.3f+o*(.32f*tight+.05f);var peg=feet+o*.82f+Up*.03f;s.Line(false,low0,Vector3.Lerp(low0,peg,tight),BolasCord*(1.5f*env*hum),.03f,true);s.Sprite(false,peg,weight*(1.6f*env*tight),.1f,Hot);}
            Ring(s,feet+Up*.04f,.82f,.06f,BolasCord,low?14:24,.7f*env*tight);
        }

        // ---- Bow: Bramble Arrow -----------------------------------------------------------------------------------------
        // The arrow sheds leaves on the way down, bites, and the ground splits along the hedge's line from the middle out. Thorned stems whip up in a tangle over a low dark bank, pale thorns throbbing with each bite, and at the end they
        // brown, wilt and sink into a fall of leaves. You can see just how wide and how tall it is: go round, or jump.
        private static void BrambleArrow(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var mid=N(k.Point);var along=Flat(N(k.Dir),Vector3.UnitX);var across=RightOf(along);
            if(t<k.Release){float g=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Sprite(false,from,BrambleGreen*(.8f+2*g),.24f,Hot);int m=low?4:8;for(int i=0;i<m;i++){float ang=6.2831853f*i/m+t*5,rr=.34f*(1-g)+.08f;RangedLeaf(s,from+new Vector3((float)Math.Cos(ang)*rr,(float)Math.Sin(ang*1.4f)*rr*.5f,(float)Math.Sin(ang)*rr),ang,.05f,BrambleGreen*(1.6f*g));}return;}
            if(t<k.Arm)
            {
                float u=Clamp01((t-k.Release)/Math.Max(.03f,k.Arm-k.Release));var head=Vector3.Lerp(from,mid,u);RangedArrow(s,head,RangedNorm(mid-from,Vector3.UnitZ),BrambleGreen,1);
                int m=low?4:9;for(int i=0;i<m;i++){float ui=(i+.5f)/m,own=u-ui;if(own<0)continue;var p=Vector3.Lerp(from,mid,ui)-Up*(1.2f*own)+new Vector3((float)Math.Sin(i*2.6f)*.25f*own,0,(float)Math.Cos(i*2.6f)*.25f*own);RangedLeaf(s,p,i*1.9f+t*6,.07f,BrambleGreen*(1.8f*(1-own)));}
                return;
            }
            float age=t-k.Arm,wither=Smooth((t-(k.Until-.55f))/.55f),gone=t-k.Until;
            Flash(s,mid+Up*.1f,age,.2f,BrambleGreen,1.8f,seed);Dust(s,mid,age,.7f,Count(10,q,low),2,new Vector3(.3f,.25f,.16f),.75f,seed+1);Sparks(s,mid+Up*.1f,age,.4f,Count(14,q,low),5,BrambleThorn,seed+2,9,.022f,Up*.5f);
            // The ground rips along the line.
            if(age<.3f){float g=OutCubic(age/.2f),b=1-age/.3f;for(int sd=-1;sd<=1;sd+=2){var p=mid+along*(sd*BrambleHalf*g);Sparks(s,p+Up*.1f,age,.3f,Count(6,q,low),3.6f,BrambleGreen,seed+3+sd,9,.02f,Up);s.Line(false,mid+Up*.04f,p+Up*.04f,BrambleThorn*(2.4f*b),.09f,true);}}
            if(gone<0)
            {
                float e=Smooth(age/.2f)*(1-wither);Strip(s,mid-along*BrambleHalf+Up*.03f,mid+along*BrambleHalf+Up*.03f,.72f,BrambleGreen,.42f*e,.42f*e);
                int bank=Count(16,q,low);
                for(int i=0;i<bank;i++){float xi=(i+.5f)/bank*2-1,show=Smooth((age-Math.Abs(xi)*.18f)/.24f)*(1-wither);if(show<=0)continue;s.Smoke(mid+along*(xi*BrambleHalf)+across*((R(seed,i+150)-.5f)*.4f)+Up*(.22f+.14f*R(seed,i+151)),new Vector3(.05f,.11f,.035f),.86f*show,(.72f+.42f*R(seed,i+152))*(.5f+.5f*show),.22f,seed+i,mid.Y-.05f);}
                int n=Count(30,q,low),segs=low?3:5;float bite=Pulse(t,18.85f,4);var brown=new Vector3(.4f,.24f,.1f);
                for(int i=0;i<n;i++)
                {
                    float xi=(i+R(seed,i+200))/n*2-1,grow=OutCubic((age-Math.Abs(xi)*.2f-.04f*R(seed,i+201))/.3f)*(1-.8f*wither);if(grow<=.01f)continue;
                    var b=mid+along*(xi*BrambleHalf)+across*((R(seed,i+202)-.5f)*.75f)+Up*.02f;float h=BrambleTall*(.6f+.5f*R(seed,i+203))*(.72f+.28f*(float)Math.Cos(xi*1.3f)),lean=(R(seed,i+204)-.5f)*1.5f,bow=(R(seed,i+205)-.5f)*.7f,ph=R(seed,i+206)*6.28f,sway=.05f*(float)Math.Sin(t*2.1f+ph);var prev=b;
                    for(int j=1;j<=segs;j++)
                    {
                        float v=j/(float)segs*grow;var p=b+Up*(h*v*(1-.18f*v))+along*(lean*v*v)+across*(bow*v*(float)Math.Sin(v*3.1f)+sway*v*v+.07f*(float)Math.Sin(v*9+ph));
                        var stem=Vector3.Lerp(Vector3.Lerp(new Vector3(.16f,.42f,.1f),new Vector3(.42f,.8f,.2f),v),brown,wither);s.Line(false,prev,p,stem*(1.5f-.5f*wither),.075f*(1.1f-v)+.022f,false);
                        int sign=((i+j)&1)*2-1;var tdir=Vector3.Normalize(across*sign+along*(.5f*(((i*3+j)&1)*2-1))+Up*.3f);s.Line(false,p,p+tdir*(.17f*grow),BrambleThorn*((1.5f+1.3f*bite)*(1-wither)),.03f,true);prev=p;
                    }
                    if(i%3==0)RangedLeaf(s,prev,ph+t*.5f,.1f,Vector3.Lerp(BrambleGreen*1.8f,brown*1.4f,wither));
                }
            }
            // Leaves falling as it dies.
            float fall=t-(k.Until-.55f);if(fall>0&&fall<1.3f){int n=Count(20,q,low);for(int i=0;i<n;i++){float own=fall-.5f*R(seed,i+300);if(own<0||own>.8f)continue;var p=mid+along*((R(seed,i+301)*2-1)*BrambleHalf)+across*((R(seed,i+302)-.5f)*.8f)+Up*Math.Max(.04f,1-1.5f*own*own-.8f*R(seed,i+303))+across*(.12f*(float)Math.Sin(own*7+i));RangedLeaf(s,p,i*1.7f+own*5,.08f,new Vector3(.6f,.44f,.14f)*(1.6f*(1-own/.8f)));}}
        }

        // ---- Bow: Vault Shot --------------------------------------------------------------------------------------------
        // From the press a dotted arc and a landing ring show exactly where this ends: that is the counter. The flip draws a ribbon along the arc, shedding feathers; at its top a ring of air and one hard arrow; a puff where the boots come down.
        private static Vector3 VaultAt(Vector3 from,Vector3 to,float u)=>Vector3.Lerp(from,to,u)+Up*(1+1.9f*4*u*(1-u));
        private static void VaultShot(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var to=N(k.To);float span=Math.Max(.05f,k.Until-k.Release),u=Clamp01((t-k.Release)/span),show=Smooth((t-k.At)/.05f)*(1-Smooth((t-k.Until)/.2f));var dir=Flat(to-from,Vector3.UnitZ);var side=RightOf(dir);var white=VaultMint+Vector3.One*.5f;
            int dots=low?8:14;for(int i=0;i<dots;i++){float ui=(i+.5f)/dots;s.Sprite(false,VaultAt(from,to,ui),VaultMint*((ui>u?1.5f:.5f)*show),.11f,Hot);}
            float pinch=.9f-.25f*u;Ring(s,to+Up*.04f,pinch,.09f,VaultMint,low?16:28,1.5f*show);Ring(s,to+Up*.04f,pinch*.45f,.05f,white,low?12:20,1.1f*show);
            if(t<k.Release)return;
            // The ribbon.
            if(t<k.Until+.28f)
            {
                float fade=1-Smooth((t-k.Until)/.28f);int m=low?10:20;var prev=VaultAt(from,to,u);
                for(int i=1;i<=m;i++){float ub=u-i*.05f;if(ub<0)break;var p=VaultAt(from,to,ub);float b0=1-(i-1f)/m,b1=1-i/(float)m;s.Band(false,prev-side*(.3f*b0),p-side*(.3f*b1),p+side*(.3f*b1),prev+side*(.3f*b0),1.5f*b0*fade,1.5f*b1*fade,VaultMint);s.Line(false,prev,p,white*(1.6f*b0*fade),.02f,true);prev=p;}
                if(t<k.Until){var at=VaultAt(from,to,u);float flip=-u*6.2831853f;for(int sd=0;sd<2;sd++){float a0=flip+sd*3.14159f;Vector3 O(float x)=>at+(dir*(float)Math.Cos(x)+Up*(float)Math.Sin(x))*.95f;var last=O(a0);for(int j=1;j<=6;j++){var p=O(a0-j*.26f);s.Line(false,last,p,white*(1.7f*(1-j/7f)),.05f*(1-j/7f)+.01f,true);last=p;}}}
            }
            int fn=Count(12,q,low);for(int i=0;i<fn;i++){float ui=R(seed,i+1),own=t-(k.Release+ui*span);if(own<0||own>.85f)continue;var p=VaultAt(from,to,ui)-Up*(.7f*own)+side*(.26f*(float)Math.Sin(own*6+i))+dir*(.2f*(R(seed,i+2)-.5f));RangedLeaf(s,p,i*2.1f+own*4,.09f,white*(1.5f*(1-own/.85f)));}
            // The shot from the top of the arc.
            float age=t-k.Arm;
            if(age>=0)
            {
                var top=VaultAt(from,to,Clamp01((k.Arm-k.Release)/span));var end=N(k.Point);var d=RangedNorm(end-top,Vector3.UnitZ);Flash(s,top,age,.2f,VaultMint,1.6f,seed+2);
                float ring=Clamp01(age/.24f);if(ring<1)Hoop(s,top,d,.2f+1.1f*OutCubic(ring),.12f*(1-ring)+.015f,white*(1.5f*(1-ring)),low?12:22,1);
                float fly=k.HitAt>-50?Math.Max(.03f,k.HitAt-k.Arm):.26f,g=Clamp01(age/fly);if(age<fly+.14f){float b=1-Clamp01((age-fly)/.14f);var head=Vector3.Lerp(top,end,g);RangedArrow(s,head,d,VaultMint,b);s.Line(false,top,head,VaultMint*(1.1f*b),.025f,true);}
                if(k.Hit(t)){float since=t-k.HitAt;Flash(s,end,since,.24f,VaultMint,1.5f,seed+3);Sparks(s,end,since,.4f,Count(16,q,low),5.5f,VaultMint,seed+4,8,.024f);if(since<.12f)s.Sprite(false,end,white*(3*(1-since/.12f)),.9f,Sparkle);}
            }
            float after=t-k.Until;if(after>=0){Dust(s,to,after,.6f,Count(10,q,low),1.5f,new Vector3(.5f,.54f,.5f),.6f,seed+9);float g=Clamp01(after/.3f);if(g<1)Ring(s,to+Up*.05f,.5f+1.7f*OutCubic(g),.16f*(1-g)+.02f,VaultMint,low?16:28,1.5f*(1-g));}
        }

        // ---- Bow: Tether Arrow ------------------------------------------------------------------------------------------
        // A thin line pays out behind the arrow. On a hit a stake slams into the floor where they stood and a ring of dashes marks exactly how far they can go; a line runs stake to chest, slack near the stake and taut, hot and humming as they near the
        // ring, with light crawling back along it as it pulls. A snap whips the line apart in a burst at the victim, and the ring is thrown outward.
        private static void TetherArrow(ITechniqueFxSink s,IKitFxScene scene,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var anchor=N(k.Point);var at=anchor+Up*1.2f;var white=TetherBlue+Vector3.One*.5f;
            if(t<k.Release){float g=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Sprite(false,from,white*(.8f+1.8f*g),.2f,Hot);return;}
            bool hit=k.Hit(t);float arrive=hit?k.HitAt:k.Until;
            if(t<arrive)
            {
                float u=Clamp01((t-k.Release)/Math.Max(.03f,arrive-k.Release));var head=Vector3.Lerp(from,at,u);RangedArrow(s,head,RangedNorm(at-from,Vector3.UnitZ),TetherBlue,1);s.Line(false,from,head,TetherBlue*1.1f,.018f,true);
                return;
            }
            if(!hit){float after=t-k.Until;Flash(s,at,after,.2f,TetherBlue,.9f,seed);Sparks(s,at,after,.35f,Count(10,q,low),3.4f,TetherBlue,seed+1,9,.02f);return;}
            float since=t-k.HitAt;bool snapped=k.Recast(t);float gone=snapped?t-k.RecastAt:t-k.Until;bool over=snapped||t>=k.Until;float fade=over?1-Smooth(gone/(snapped?.26f:.3f)):1;
            var who=ActorOf(scene,k.Target);var chest=who!=null&&scene.Chest(who,out var cc)?cc:at+new Vector3(.8f*(float)Math.Sin(since*1.4f),0,.8f*(float)Math.Cos(since*1.1f));
            var d0=chest-anchor;d0.Y=0;float dist=d0.Length(),strain=Clamp01((dist-TetherReach*.4f)/(TetherReach*.6f));strain*=strain;
            var col=Vector3.Lerp(TetherBlue,new Vector3(1,.56f,.3f),strain);var stakeTop=anchor+Up*.55f;
            // The stake.
            Flash(s,anchor+Up*.3f,since,.2f,TetherBlue,1.6f,seed+2);Cracks(s,anchor,since,.9f,low?4:7,1.4f,TetherBlue,seed+3);Dust(s,anchor,since,.6f,Count(8,q,low),1.1f,new Vector3(.4f,.42f,.44f),.65f,seed+4);
            Flash(s,chest,since,.2f,TetherBlue,1.2f,seed+5);Sparks(s,chest,since,.36f,Count(12,q,low),4.6f,TetherBlue,seed+6,8,.022f);
            if(fade>0)
            {
                for(int i=0;i<3;i++){float ang=i*2.0943951f+.7f;var o=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*.13f;s.Line(false,stakeTop,anchor+o+Up*.02f,white*(1.8f*fade),.05f,true);}
                s.Sprite(false,stakeTop,white*(2.2f*fade),.2f,Hot);Hoop(s,anchor+Up*.08f,Up,.28f,.05f,TetherBlue*(1.6f*fade),low?10:16,1);
                // The ring: how far they can go.
                float open=OutCubic(since/.2f),thrown=snapped?1+.35f*OutCubic(gone/.26f):1;
                RangedDashes(s,anchor+Up*.05f,TetherReach*open*thrown,.2f,t*.2f,low?18:34,.58f,col,(1.3f+.9f*strain)*fade);Ring(s,anchor+Up*.04f,TetherReach*open*thrown,.05f,col,low?24:48,.7f*fade);
            }
            // The line.
            var line=chest-stakeTop;float span=line.Length();var dir=RangedNorm(line,Vector3.UnitZ);var side=RangedSide(dir);float sag=.42f*(1-strain),amp=.45f*(float)Math.Exp(-since*7)+.012f+.03f*strain;int n=low?10:18;float cut=snapped?OutCubic(gone/.2f)*.52f:0;
            if(over&&snapped)amp=.12f+.5f*Clamp01(gone/.2f);
            Vector3 Along(float v)=>stakeTop+line*v+Up*(-sag*4*v*(1-v))+side*(amp*(float)Math.Sin(v*12-t*34)*(float)Math.Sin(3.14159f*v))+Up*(amp*.6f*(float)Math.Cos(v*9-t*29)*(float)Math.Sin(3.14159f*v));
            if(span>.05f&&fade>0)
            {
                var last=Along(0);
                for(int i=1;i<=n;i++){float v=i/(float)n,vp=(i-1f)/n;var p=Along(v);bool cutHere=vp<.5f+cut&&v>.5f-cut&&cut>0;if(!cutHere){s.Line(false,last,p,col*(1.2f*fade),.09f,false);s.Line(false,last,p,(col+Vector3.One*.5f)*((1.6f+1.2f*strain)*fade),.026f,true);}last=p;}
                if(!over){int beads=low?2:4;for(int i=0;i<beads;i++){float ph=(t*(1+1.6f*strain)+i/(float)beads)%1;s.Sprite(false,Along(1-ph),(col+Vector3.One*.5f)*2.2f,.16f,Hot);}Hoop(s,chest,Up,.38f,.06f,col*1.8f,low?10:18,1);}
            }
            if(snapped){Flash(s,chest,gone,.26f,new Vector3(1,.62f,.42f),1.9f,seed+7);Sparks(s,chest,gone,.5f,Count(22,q,low),7,new Vector3(1,.7f,.5f),seed+8,8,.026f);if(gone<.12f)s.Sprite(false,chest,Vector3.One*(3.4f*(1-gone/.12f)),1,Sparkle);}
        }
    }
    public sealed partial class NativeTechniquePreview
    {
        private static bool WriteRanged(NativeTechnique k,string id,Vector3 feet,Vector3 f,Vector3 r,Vector3 up,Vector3 hand,float t0,bool first)
        {
            var muzzle=feet+up*1.42f+f*.85f+r*.1f;var arrow=feet+up*1.35f+f*.6f;
            switch(id)
            {
                case NativeTechniques.FlashRound:k.Release=t0+.14f;k.From=V3Of(hand);k.Point=V3Of(first?feet+f*9+up*1.9f:feet+f*10+r*2+up*1.5f);k.Arm=k.Release+(first?.16f:.2f);k.Until=k.Arm+.9f;k.RecoverUntil=k.Release+.18f;return true;
                case NativeTechniques.GunSpin:k.Release=t0+.02f;k.Arm=t0+.45f;k.Until=k.Arm+(first?4:2.2f);k.RecoverUntil=k.Arm;return true;
                case NativeTechniques.SlideShot:{k.Release=t0+.04f;var d=first?f:Vector3.Normalize(f+r*.6f);k.From=V3Of(feet);k.To=V3Of(feet+d*6);k.Dir=V3Of(d);k.Until=k.Release+.55f;k.RecoverUntil=k.Until+.2f;}return true;
                case NativeTechniques.Bipod:k.Release=t0+.5f;k.Point=V3Of(feet);k.Dir=V3Of(f);if(first)k.Until=k.Release+5;else{k.RecastAt=k.Release+2.2f;k.Until=k.RecastAt+.3f;}k.RecoverUntil=k.Until;return true;
                case NativeTechniques.AmmoCrate:k.Release=t0+.3f;k.Point=V3Of(feet+f*1.4f+r*1.1f);k.Arm=first?1:.3f;k.Until=k.Release+(first?8:3.4f);k.RecoverUntil=k.Release+.25f;if(first){k.HitAt=k.Release+2.2f;k.To=V3Of(feet+up*1.25f);}return true;
                case NativeTechniques.FlakBurst:k.Release=t0+.12f;k.From=V3Of(muzzle);k.Point=V3Of(first?feet+f*13+up*2:feet+f*9+r*6+up*1.8f);k.Arm=k.Release+.34f;k.Until=k.Arm+3;k.RecoverUntil=k.Release+.2f;if(first){k.HitAt=k.Arm+1.2f;k.To=V3Of(feet+f*9+r*1.5f+up*1.6f);}return true;
                case NativeTechniques.BreacherRound:k.Release=t0+1;k.From=V3Of(muzzle);k.Dir=V3Of(f);k.To=V3Of(muzzle+f*(first?46:80));if(first){k.Path=new[]{V3Of(muzzle+f*11),V3Of(muzzle+f*12.2f)};k.HitAt=k.Release+.03f;}k.Until=k.Release+.5f;k.RecoverUntil=k.Release+.25f;return true;
                case NativeTechniques.Ghillie:k.Release=t0+.08f;k.Point=V3Of(feet);k.Arm=t0+.6f;k.Until=k.Arm+(first?5:2.2f);k.RecoverUntil=k.Arm;return true;
                case NativeTechniques.BolasRound:k.Release=t0+.1f;k.From=V3Of(muzzle);if(first){k.Point=V3Of(feet+f*9+up*1.2f);k.HitAt=k.Release+.3f;k.Until=k.HitAt+1.2f;}else{k.Point=V3Of(feet+f*14+r*4+up*1.5f);k.Until=k.Release+.5f;}k.RecoverUntil=k.Release+.2f;return true;
                case NativeTechniques.BrambleArrow:k.Release=t0+.34f;k.From=V3Of(arrow);k.Point=V3Of(first?feet+f*8:feet+f*6+r*2);k.Dir=V3Of(first?r:Vector3.Normalize(r+f*.45f));k.Arm=k.Release+.24f;k.Until=k.Arm+(first?5:2.6f);k.RecoverUntil=k.Release+.3f;return true;
                case NativeTechniques.VaultShot:k.Release=t0+.04f;k.From=V3Of(feet);k.To=V3Of(first?feet-f*5:feet-f*4.8f+r*1.4f);k.Arm=k.Release+.2f;k.Until=k.Release+.45f;k.RecoverUntil=k.Until+.12f;k.Point=V3Of(first?feet+f*14+up*1.2f:feet+f*20+r*2+up*1);if(first)k.HitAt=k.Arm+.18f;return true;
                case NativeTechniques.TetherArrow:k.Release=t0+.3f;k.From=V3Of(arrow);k.Point=V3Of(feet+f*9);k.HitAt=k.Release+.26f;if(first)k.Until=k.HitAt+3;else{k.RecastAt=k.HitAt+1.4f;k.Until=k.RecastAt+.35f;}k.RecoverUntil=k.Release+.3f;return true;
                default:return false;
            }
        }
        private static float PeriodRanged(string id)
        {
            switch(id)
            {
                case NativeTechniques.FlashRound:return 3.2f;
                case NativeTechniques.GunSpin:return 5f;
                case NativeTechniques.SlideShot:return 2.6f;
                case NativeTechniques.Bipod:return 6.8f;
                case NativeTechniques.AmmoCrate:return 9.6f;
                case NativeTechniques.FlakBurst:return 4.8f;
                case NativeTechniques.BreacherRound:return 3.4f;
                case NativeTechniques.Ghillie:return 6.8f;
                case NativeTechniques.BolasRound:return 3.2f;
                case NativeTechniques.BrambleArrow:return 6.8f;
                case NativeTechniques.VaultShot:return 2.8f;
                case NativeTechniques.TetherArrow:return 4.8f;
                default:return 0;
            }
        }
        private static bool AimRanged(NativeTechnique k,string id,int target,Vector3 at)
        {
            switch(id)
            {
                case NativeTechniques.BolasRound:if(k.HitAt>-50){k.Target=target;k.Point=V3Of(at+Vector3.UnitY*1.2f);}return true;
                case NativeTechniques.VaultShot:if(k.HitAt>-50)k.Point=V3Of(at+Vector3.UnitY*1.2f);return true;
                case NativeTechniques.TetherArrow:k.Target=target;k.Point=V3Of(at);return true;
                default:return false;
            }
        }
    }
    public static partial class NativeTechniqueSounds
    {
        private static int MomentRanged(string kind,int m,NativeKitSound[] o,ref bool atPoint)
        {
            int n=0;
            switch(kind)
            {
                case NativeTechniques.FlashRound:if(m==Release)n=S(o,n,"gun.pistol.shot","pistol",.8f);else if(m==Arm){atPoint=true;n=S(o,n,"blast.transient","dash",.9f);n=S(o,n,"blast.shock","lightning",.55f);}break;
                case NativeTechniques.GunSpin:if(m==Release)n=S(o,n,"move.roll","dash",.35f);else if(m==Arm)n=S(o,n,"gun.reload.end","metal",.9f);else if(m==End)n=S(o,n,"gun.dryfire","metal",.3f);break;
                case NativeTechniques.SlideShot:if(m==Release)n=S(o,n,"move.roll","dash",.85f);else if(m==End)n=S(o,n,"move.land","explosion",.35f);break;
                case NativeTechniques.Bipod:if(m==Release){atPoint=true;n=S(o,n,"impact.metal","metal",.85f);n=S(o,n,"blast.quake","stone",.3f);}else if(m==Recast||m==End)n=S(o,n,"gun.reload.end","metal",.65f);break;
                case NativeTechniques.AmmoCrate:if(m==Release){atPoint=true;n=S(o,n,"impact.metal","metal",.85f);n=S(o,n,"move.land","explosion",.45f);}else if(m==Hit){atPoint=true;n=S(o,n,"gun.reload.end","metal",.9f);}else if(m==End){atPoint=true;n=S(o,n,"blast.kinetic","dash",.5f);}break;
                case NativeTechniques.FlakBurst:if(m==Release)n=S(o,n,"gun.frag.throw","dash",.8f);else if(m==Arm){atPoint=true;n=S(o,n,"blast.fire","explosion",.95f);n=S(o,n,"gun.ricochet","metal",.35f);}else if(m==Hit){atPoint=true;n=S(o,n,"gun.ricochet","metal",.75f);}break;
                case NativeTechniques.BreacherRound:if(m==Release){n=S(o,n,"gun.machinegun.shot","machinegun",1);n=S(o,n,"blast.kinetic","dash",.85f);}else if(m==Hit)n=S(o,n,"impact.metal","metal",.6f);break;
                case NativeTechniques.Ghillie:if(m==Release)n=S(o,n,"move.roll","dash",.25f);else if(m==Arm)n=S(o,n,"gun.reload.end","metal",.18f);else if(m==End)n=S(o,n,"blast.transient","dash",.4f);break;
                case NativeTechniques.BolasRound:if(m==Release){n=S(o,n,"gun.frag.throw","dash",.8f);n=S(o,n,"gun.pistol.shot","pistol",.4f);}else if(m==Hit){atPoint=true;n=S(o,n,"melee.daggers.hit","daggers",.8f);n=S(o,n,"impact.metal","metal",.4f);}else if(m==End)n=S(o,n,"melee.daggers.throw","daggers",.3f);break;
                case NativeTechniques.BrambleArrow:if(m==Release)n=S(o,n,"melee.daggers.throw","daggers",.75f);else if(m==Arm){atPoint=true;n=S(o,n,"blast.quake","stone",.55f);n=S(o,n,"melee.daggers.hit","daggers",.45f);}else if(m==End){atPoint=true;n=S(o,n,"move.roll","dash",.3f);}break;
                case NativeTechniques.VaultShot:if(m==Release)n=S(o,n,"move.jump","jump",.85f);else if(m==Arm){n=S(o,n,"melee.daggers.throw","daggers",.9f);n=S(o,n,"move.roll","dash",.4f);}else if(m==Hit){atPoint=true;n=S(o,n,"melee.daggers.hit","daggers",.8f);}else if(m==End)n=S(o,n,"move.land","explosion",.45f);break;
                case NativeTechniques.TetherArrow:if(m==Release)n=S(o,n,"melee.daggers.throw","daggers",.8f);else if(m==Hit){atPoint=true;n=S(o,n,"impact.metal","metal",.8f);n=S(o,n,"melee.daggers.hit","daggers",.6f);}else if(m==Recast)n=S(o,n,"gun.ricochet","metal",.8f);break;
            }
            return n;
        }
    }
    public static partial class NativeVisualMotion
    {
        private static bool PoseRanged(Set set,NativeTechnique k,float time,float back,IClips clips,out Pick pick)
        {
            pick=default;Pick got=default;
            bool Part(string clip,float from,float to){return time>=from&&time<to&&clip!=null&&clips.Has(clip)&&Fit(clip,time,from,to,clips,FadeIn,out got);}
            // A clip held at (or run on from) `at` of itself.
            bool Hold(string clip,float from,float to,float at){if(time<from||time>=to||clip==null||!clips.Has(clip))return false;got=new Pick{Clip=clip,Time=at,Fade=FadeIn};return true;}
            string aim=set.Aim!=null?set.Aim[1]:null,fire=set.Fire!=null?set.Fire[1]:null,draw=set.Pull!=null?set.Pull[1]:null;
            bool hit=false;
            switch(k.Kind)
            {
                case NativeTechniques.FlashRound:hit=Part(aim,k.At,back);break;
                case NativeTechniques.GunSpin:hit=Part(set.Reload,k.At,k.Arm);break;
                // The deep crouch at the start of the set's landing, held for the slide; the rest of it is the rise.
                case NativeTechniques.SlideShot:{float len=clips.Has(set.Land)?clips.Length(set.Land):1;hit=Hold(set.Land,k.At,k.RecoverUntil,len*(time<k.Until?.22f*Clamp01((time-k.At)/.08f):.22f+.78f*Clamp01((time-k.Until)/Math.Max(.05f,k.RecoverUntil-k.Until))));}break;
                case NativeTechniques.Bipod:{bool up=k.RecastAt>-50;float end=up?k.RecastAt:k.Until;hit=Part(TechDrop,k.At,k.Release)||Hold(aim,k.Release,end,time-k.Release)||(up&&Part(TechShootLine,end,k.Until));}break;
                case NativeTechniques.AmmoCrate:hit=Part(TechDrop,k.At,back);break;
                case NativeTechniques.FlakBurst:hit=Part(fire,k.At,back);break;
                case NativeTechniques.BreacherRound:hit=Hold(aim,k.At,k.Release,time-k.At)||Part(fire,k.Release,back);break;
                // Down into the stoop of the pack's pickup, and held there.
                case NativeTechniques.Ghillie:hit=clips.Has(TechDrop)&&Hold(TechDrop,k.At,k.Until,clips.Length(TechDrop)*.5f*Clamp01((time-k.At)/Math.Max(.05f,k.Arm-k.At)));break;
                case NativeTechniques.BolasRound:hit=Part(fire,k.At,back);break;
                case NativeTechniques.BrambleArrow:case NativeTechniques.TetherArrow:hit=Part(draw,k.At,k.Release)||Part(fire,k.Release,back);break;
                case NativeTechniques.VaultShot:hit=Part(set.Flip,k.At,Math.Max(k.Until,k.At+.1f))||Part(fire,k.Until,back);break;
                default:return false;
            }
            pick=got;return hit;
        }
    }
}
