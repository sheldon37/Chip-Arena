using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Bombadier's effects (docs/BOMBADIER.md), on the same terms as the techniques': pure functions of the replicated kit and the clock, thinned by Reduced and the low tier.
    public sealed partial class NativeTechniqueFx
    {
        private static readonly Vector3 BombFlame=new Vector3(1,.42f,.07f),BombHot=new Vector3(1,.78f,.32f),BombRed=new Vector3(1,.06f,.03f),BombPaper=new Vector3(.95f,.14f,.1f),BombSmoke=new Vector3(.1f,.09f,.09f),BombDirt=new Vector3(.27f,.23f,.2f);
        private static Vector3 BombFire(float u)=>Vector3.Lerp(new Vector3(1,.3f,.045f),new Vector3(.85f,.09f,.008f),u);
        /// <summary>Draws every Bombadier's charges, blasts, line and Finale of the frame at `time`.</summary>
        public void DrawBombs(ITechniqueFxSink s,IKitFxScene scene,float time,float q,bool low)
        {
            if(s==null||scene?.Actors==null||NativeBombs.Source==null)return;var actors=scene.Actors;
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];if(a==null)continue;var k=NativeBombs.Of(a);if(k==null)continue;long seed=a.Id*6151L;
                if(k.Bombs!=null)for(int b=0;b<k.Bombs.Length;b++)Charge(s,scene,k.Bombs[b],time,seed+k.Bombs[b].Id*31L,q,low);
                if(k.Blasts!=null)for(int b=0;b<k.Blasts.Length;b++)Boom(s,k.Blasts[b],time,seed+k.Blasts[b].Id*47L,q,low);
                if(k.Line!=null)BurningLine(s,k.Line,time,seed+7,q,low);
                if(k.Finale!=null)Finale(s,scene,k.Finale,time,seed+11,q,low);
                if(k.Scorched!=null)for(int b=0;b<k.Scorched.Length;b++)Scorch(s,scene,k.Scorched[b],time,seed+13+b*59L,q,low);
            }
        }
        private static Actor BombVictim(IKitFxScene scene,int id){if(id<=0)return null;var l=scene.Actors;for(int i=0;i<l.Count;i++)if(l[i]!=null&&l[i].Id==id)return l[i];return null;}
        /// <summary>Where a charge is drawn: its record's place, or on the chest it is stuck to.</summary>
        public static Vector3 ChargeAt(IKitFxScene scene,NativeBomb b)
        {
            if(b.Stuck>0&&scene!=null){var v=BombVictim(scene,b.Stuck);if(v!=null&&scene.Chest(v,out var c))return c+Vector3.UnitY*.1f;}return N(b.Pos);
        }
        // ---- Scorched: the chain's end -----------------------------------------------------------------------------------
        // A flash as it lands, then a body smouldering: heat in the chest, embers lifting off it, smoke off the shoulders, all guttering in the last half second as the window closes.
        // The local first-person pilot sees none of it on their own body (it would sit in their face): the HUD tells them.
        private static void Scorch(ITechniqueFxSink s,IKitFxScene scene,NativeBombScorch c,float t,long seed,float q,bool low)
        {
            if(c==null||t<c.At||t>=c.Until||scene.Viewer==c.Id)return;var v=BombVictim(scene,c.Id);if(v==null||!scene.Chest(v,out var chest))return;
            float life=Math.Max(.1f,c.Until-c.At),u=(t-c.At)/life,hit=1-Clamp01((t-c.At)/.25f),left=c.Until-t,gutter=left<.5f?.45f+.55f*(float)Math.Abs(Math.Sin(left*19)):1,heat=(1-.5f*u)*gutter;
            s.Glow(chest,BombFlame*(.5f*heat+1.5f*hit),0,.75f+.6f*hit,Core,0,seed);s.Glow(chest,BombHot*(.35f*heat+1.2f*hit),0,.32f+.3f*hit,Core,0,seed+1);
            if(hit>0)Sparks(s,chest,t-c.At,.25f,Count(16,q,low),3.4f,BombHot,seed+2,7,.014f,Vector3.UnitY*.5f);
            int n=Count(16,q,low);
            for(int i=0;i<n;i++)
            {
                float ph=(t*(.7f+.7f*R(seed,i+10))+R(seed,i+11))%1;var d=RandomDir(seed,i+12);
                var p=chest+new Vector3(d.X*.27f,-.62f+1.05f*R(seed,i+13),d.Z*.2f)+new Vector3((float)Math.Sin(t*3+i)*.07f*ph,.95f*ph,(float)Math.Cos(t*2.6f+i)*.07f*ph);
                s.Sprite(false,p,BombHot*(2*heat*(1-ph)),.03f+.02f*R(seed,i+14),Hot);
            }
            int m=Count(7,q,low);for(int i=0;i<m;i++){float ph=(t*.55f+i/(float)m)%1;s.Smoke(chest+new Vector3((R(seed,i+30)-.5f)*.42f,.2f+1.15f*ph,(R(seed,i+31)-.5f)*.42f),BombSmoke,.46f*heat*(1-ph),.16f+.42f*ph,.2f+.6f*ph,seed+i,-1000);}
        }
        // ---- the TNT in the fists ------------------------------------------------------------------------------------
        /// <summary>The lit fuse on what a fist holds: a small hot flame, spitting a few sparks that fall away. `view`: on the first-person layer.</summary>
        public void HeldFuse(ITechniqueFxSink s,bool view,Vector3 tip,float t,long seed,float q,bool low)
        {
            float fl=.8f+.2f*(float)Math.Sin(t*47+seed);s.Sprite(view,tip,BombHot*(2.4f*fl),.05f,Hot);s.Sprite(view,tip,Vector3.One*(1.3f*fl),.018f,Hot);
            int n=Count(4,q,low);for(int i=0;i<n;i++){float clock=t*(2.6f+R(seed,i+1))+R(seed,i+2),ph=clock%1;var d=RandomDir(seed+(long)clock*97,i+3);s.Sprite(view,tip+d*(.14f*ph)+Vector3.UnitY*(.1f*ph-.16f*ph*ph),BombHot*(2.6f*(1-ph)),.011f,Hot);}
        }
        // ---- the charges ----------------------------------------------------------------------------------------------
        private static void Charge(ITechniqueFxSink s,IKitFxScene scene,NativeBomb b,float t,long seed,float q,bool low)
        {
            if(t<b.At)return;var at=ChargeAt(scene,b);float left=b.BlowAt>-50?b.BlowAt-t:99,tick=left<1.2f?(float)Math.Pow(.5f+.5f*Math.Sin((1.3f-left)*(1.3f-left)*30),4):.5f+.5f*(float)Math.Sin(t*5);var v=N(b.Vel);float sp=v.Length();
            switch(b.Kind)
            {
                case NativeBombs.Pop:case NativeBombs.Bomblet:case NativeBombs.Lob:
                {
                    // The fuse's end, wherever the stick has turned it (still in the hand for its first moment: NativeBombPresentation brings it from the fist).
                    float rad=b.Kind==NativeBombs.Lob?.17f:b.Kind==NativeBombs.Pop?.09f:.06f;var fuse=at+Vector3.Transform(Vector3.UnitY*(NativeTnt.Length(b.Kind)*NativeTnt.FuseTip),NativeBombs.ChargeTurn(b,t));
                    if(t-b.At>=.1f){s.Sprite(false,fuse,BombHot*2.4f,rad*1.3f+.04f,Hot);Sparks(s,fuse,(t*8)%.3f,.3f,low?2:5,1.8f,BombHot,seed+(long)(t*8),5,.012f,Vector3.UnitY*.6f);}
                    if(sp>.5f){var d=v/sp;s.Line(false,at-d*Math.Min(.9f,sp*.05f),at,BombFlame*.7f,rad*.45f,true);if(b.Kind==NativeBombs.Lob&&!low)for(int i=0;i<4;i++){float ph=(t*2.4f+i*.25f)%1;s.Smoke(at-d*(.3f+1.6f*ph)+Vector3.UnitY*(.1f*ph),BombSmoke,.5f*(1-ph),.25f+.4f*ph,.2f+.5f*ph,seed+i,-1000);}}
                    if(left<.45f)s.Glow(at,BombRed*(1.4f*tick),0,rad*4,Core,0,seed+1);break;
                }
                case NativeBombs.Mine:
                {
                    bool armed=b.Armed(t);float beat=armed?(float)Math.Pow(.5f+.5f*Math.Sin(t*7),5):.15f;var n=N(b.Normal);if(n.LengthSquared()<.5f)n=Vector3.UnitY;var dome=at+n*.07f;
                    s.Sprite(false,dome,BombRed*(.7f+2.3f*beat),.1f+.1f*beat,Hot);s.Glow(dome,BombRed*(.5f*beat),0,.55f,Core,0,seed+2);
                    if(armed&&Math.Abs(n.Y)>.7f){float arm=Clamp01((t-b.ArmAt)/.15f),rise=(t*.9f)%1;Ring(s,at+Vector3.UnitY*.03f,NativeBombs.MineReach,.03f,BombRed*(.5f*arm),low?14:28);Ring(s,at+Vector3.UnitY*.03f,NativeBombs.MineReach*rise,.025f,BombRed*(.55f*arm*(1-rise)),low?12:22);}
                    float since=t-b.ArmAt;if(since>=0&&since<.12f)Flash(s,dome,since,.12f,BombRed,.5f,seed+3);break;
                }
                case NativeBombs.Sticky:
                {
                    float rate=left<99?Math.Max(0,1.2f-left):0;var up=N(b.Normal);if(up.LengthSquared()<.5f)up=Vector3.UnitY;
                    var fuse=at+up*(NativeTnt.Length(b.Kind)*(.42f+NativeTnt.FuseTip));Sparks(s,fuse,(t*11)%.28f,.28f,Count(9,q,low),3.2f+2.4f*rate,BombHot,seed+(long)(t*11),5,.016f,up*.9f);s.Sprite(false,fuse,BombHot*(2+1.4f*tick),.15f,Hot);s.Glow(at,BombRed*(.5f+1.4f*tick*rate),0,.5f+.5f*rate,Core,0,seed+4);break;
                }
            }
        }
        // ---- the blasts -----------------------------------------------------------------------------------------------
        private static void Boom(ITechniqueFxSink s,NativeBlast b,float t,long seed,float q,bool low)
        {
            float age=t-b.At;if(age<0||age>1.5f)return;var at=N(b.Pos);float R0=Math.Max(.4f,b.Radius);bool linked=b.Link>0;
            // The hand-on: a burning streak from the blast before, the chain drawn in the air.
            if(b.Handed&&age<.22f){var from=N(b.FromPos);float g=Clamp01(age/.08f),fade=1-Clamp01((age-.08f)/.14f);var head=Vector3.Lerp(from,at,g);s.Line(false,Vector3.Lerp(from,at,Math.Max(0,g-.6f)),head,(BombHot+Vector3.One*.25f)*(2.4f*fade),.05f+.02f*b.Link,true);s.Glow(head,BombFlame*(1.6f*fade),0,.7f,Core,0,seed+1);}
            switch(b.Kind)
            {
                case NativeBombs.Spark:{Flash(s,at+Vector3.UnitY*.12f,age,.12f,BombHot,1.1f,seed);Sparks(s,at+Vector3.UnitY*.1f,age,.32f,Count(12,q,low),4.5f,BombHot,seed+1,8,.016f,Vector3.UnitY*.8f);int p=Count(9,q,low);if(age<.9f)for(int i=0;i<p;i++){var d=RandomDir(seed,i+20);d.Y=Math.Abs(d.Y)+.6f;float a=age;var o=at+d*(2.1f*a)-Vector3.UnitY*(2.2f*a*a)+new Vector3((float)Math.Sin(a*14+i)*.06f,0,(float)Math.Cos(a*12+i)*.06f);if(o.Y<at.Y)continue;s.Sprite(false,o,BombPaper*(1.3f*(1-a/.9f)),.05f,Soft);}if(age<.8f)s.Smoke(at+Vector3.UnitY*(.2f+.5f*age),BombSmoke,.4f*(1-age/.8f),.4f+.7f*age,.2f+.6f*age,seed+3,at.Y-.3f);return;}
                case NativeBombs.Column:{Column(s,at,R0,age,seed,q,low);return;}
            }
            // The fireball: a hot core, flame rolling out and up, going to smoke.
            float life=.42f+.1f*R0,u=Clamp01(age/life);int n=Count(linked?20:14,q,low);float lift=b.Kind==NativeBombs.Mine?2.6f:.6f;
            if(age<life)for(int i=0;i<n;i++){var d=RandomDir(seed,i+30);if(b.Kind!=NativeBombs.Lob)d.Y=Math.Abs(d.Y)*.7f;var p=at+d*(R0*.62f*OutCubic(u)*(.5f+.5f*R(seed,i+31)))+Vector3.UnitY*(lift*u*R(seed,i+32));s.Fire(p,BombFire(u),(1-u*u)*(.8f+.2f*R(seed,i+33)),R0*(.42f+.5f*u)*(.8f+.4f*R(seed,i+34)),.04f+.34f*u,seed+i,at.Y-1.2f);}
            Flash(s,at+Vector3.UnitY*.2f,age,.2f,BombFlame,R0*1.5f,seed+2);
            if(age<.16f)s.Glow(at+Vector3.UnitY*.2f,Vector3.One*(3*(1-age/.16f)),0,R0*.8f,Core,age/.16f,seed+3);
            // Hard streaks and dark grit thrown out, the shock over the floor.
            Sparks(s,at+Vector3.UnitY*.15f,age,.6f,Count(linked?34:22,q,low),5+2.2f*R0,BombHot,seed+4,9,.022f,Vector3.UnitY*.45f);
            int g2=Count(10,q,low);if(age<.7f)for(int i=0;i<g2;i++){var d=RandomDir(seed,i+60);d.Y=Math.Abs(d.Y)+.3f;float sp=3+3.5f*R(seed,i+61);var v=d*sp;var h=at+v*age-Vector3.UnitY*(4.9f*age*age);if(h.Y<at.Y-.4f)continue;s.Streak(h,v-Vector3.UnitY*(9.8f*age),new Vector3(.03f,.03f,.03f),.05f,.04f,.02f,.3f,age/.7f,seed+i);}
            bool grounded=b.Kind!=NativeBombs.Lob&&b.Kind!=NativeBombs.Sticky;
            if(grounded&&age<.32f){float w=age/.32f;Ring(s,at+Vector3.UnitY*.04f,R0*1.25f*OutCubic(w),.22f*(1-w)+.05f,(linked?BombHot:BombFlame)*(1.5f*(1-w)),low?14:30);if(linked)Ring(s,at+Vector3.UnitY*.05f,R0*1.7f*OutCubic(w),.08f,Vector3.One*(1.1f*(1-w)),low?14:30);}
            if(grounded)Dust(s,at,age,.75f,Count(9,q,low),R0*1.15f,BombDirt,.55f,seed+6);
            // What is left: smoke going up, dark and slow.
            int m=Count(8,q,low);float o0=(1-Smooth((age-.5f)/1))*Smooth((age-.12f)/.2f);if(o0>0)for(int i=0;i<m;i++){var d=RandomDir(seed,i+80);d.Y=Math.Abs(d.Y);s.Smoke(at+d*(R0*.4f)+Vector3.UnitY*(.4f+1.1f*age*(.6f+.5f*R(seed,i+81))),BombSmoke,.6f*o0,R0*(.4f+.55f*age),.15f+.5f*age,seed+i,at.Y-.3f);}
            // Mines: the jet that throws you, straight up.
            if(b.Kind==NativeBombs.Mine&&age<.3f){float w=age/.3f;int tg=low?3:7;for(int i=0;i<tg;i++){var side=new Vector3((R(seed,i+90)-.5f)*.5f,0,(R(seed,i+91)-.5f)*.5f);s.Tongue(false,at+side,at+side*1.6f+Vector3.UnitY*(1.6f+3.2f*OutCubic(w))*(.7f+.5f*R(seed,i+92)),BombHot*(2*(1-w)),.16f);}}
            // A link: a star over the blast, bigger each link, so a chain reads from across the map.
            if(linked&&age<.3f){float w=age/.3f;s.Sprite(false,at+Vector3.UnitY*(R0*.5f),(BombHot+Vector3.One*.3f)*(2.6f*(1-w)),.9f+.35f*b.Link,Sparkle);}
        }
        // The Finale's column: a pillar of fire from the sky down through the circle, the floor split, a hard ring outward.
        private static void Column(ITechniqueFxSink s,Vector3 at,float R0,float age,long seed,float q,bool low)
        {
            const float High=11;float life=.75f,u=Clamp01(age/life);
            if(age<.14f){float w=age/.14f;s.Line(false,at+Vector3.UnitY*High,at,(BombHot+Vector3.One)*(3.4f*(1-w*.4f)),.4f+R0*.2f*(1-w),false);}
            Flash(s,at+Vector3.UnitY*1.6f,age,.3f,BombFlame,R0*2.6f,seed);Cracks(s,at,age,1.3f,low?8:16,R0*1.1f,BombFlame,seed+1);
            int n=Count(30,q,low);if(age<life)for(int i=0;i<n;i++){float y=High*R(seed,i+10),spread=.25f+.9f*OutCubic(u)*(1-y/High*.5f);var a=6.2831853f*R(seed,i+11)+t2(age)*(R(seed,i+12)-.5f);var p=at+new Vector3((float)Math.Cos(a)*R0*.42f*spread,y,(float)Math.Sin(a)*R0*.42f*spread);s.Fire(p,BombFire(u),(1-u*u)*(.75f+.25f*R(seed,i+13)),(1.1f+.9f*u)*(.8f+.4f*R(seed,i+14)),.06f+.36f*u,seed+i,at.Y-.3f);}
            if(age<.5f){float w=age/.5f;Ring(s,at+Vector3.UnitY*.05f,R0*1.6f*OutCubic(w),.4f*(1-w)+.08f,BombHot*(2*(1-w)),low?18:40);}
            Sparks(s,at+Vector3.UnitY*.3f,age,.9f,Count(40,q,low),10,BombHot,seed+2,10,.03f,Vector3.UnitY*.9f);Dust(s,at,age,1.1f,Count(14,q,low),R0*1.4f,BombDirt,.7f,seed+3);
            int m=Count(12,q,low);float o0=(1-Smooth((age-.5f)/1))*Smooth((age-.2f)/.25f);if(o0>0)for(int i=0;i<m;i++){float y=(2+7*R(seed,i+30))*(.5f+.6f*age);var a=6.2831853f*R(seed,i+31);s.Smoke(at+new Vector3((float)Math.Cos(a)*(.8f+.5f*age),y,(float)Math.Sin(a)*(.8f+.5f*age)),BombSmoke,.6f*o0,1.3f+.9f*age,.15f+.5f*age,seed+i,at.Y-.3f);}
        }
        private static float t2(float a)=>a*2.5f;
        // ---- the Firecracker Line -------------------------------------------------------------------------------------
        private static void BurningLine(ITechniqueFxSink s,NativeBombLine l,float t,long seed,float q,bool low)
        {
            float age=t-l.At;if(age<0)return;var e0=N(l.From);var e1=N(l.To);var d=e1-e0;float len=d.Length();if(len<.1f)return;d/=len;float run=Math.Min(len,age*l.Speed);var head=e0+d*run;var lift=Vector3.UnitY*.03f;
            // The char behind the flame, cooling; the flame itself.
            float cool=1-Clamp01((age-len/Math.Max(.1f,l.Speed))/.9f);s.Line(false,e0+lift,head+lift,BombFlame*(.5f*cool),.05f,true);
            if(run<len){s.Glow(head+Vector3.UnitY*.05f,Vector3.One*2.6f,0,.14f,Core,0,seed);s.Glow(head+Vector3.UnitY*.08f,BombHot*2,0,.36f,Core,0,seed+1);s.Glow(head+Vector3.UnitY*.05f,BombFlame,0,1.05f,Mote,0,seed+2);Sparks(s,head,(t*12)%.26f,.26f,Count(10,q,low),3.4f,BombHot,seed+(long)(t*12),6,.015f,Vector3.UnitY*.8f-d*.5f);}
            int ahead=Count(8,q,low);if(run<len)for(int i=0;i<ahead;i++){float x=run+(i+1)*(len-run)/(ahead+1);s.Sprite(false,e0+d*x+lift,BombRed*(.45f+.4f*(float)Math.Sin(t*14-i)),.08f,Hot);}
        }
        // ---- the Grand Finale -----------------------------------------------------------------------------------------
        private static void Finale(ITechniqueFxSink s,IKitFxScene scene,NativeBombFinale f,float t,long seed,float q,bool low)
        {
            float toStrike=f.Strike-t;
            if(f.Circles!=null&&t<f.Strike+.06f)for(int i=0;i<f.Circles.Length;i++)
            {
                var c=f.Circles[i];if(c==null)continue;var at=N(c.Pos)+Vector3.UnitY*.05f;float inn=Smooth((t-(f.At+NativeBombs.PlungeAt))/.3f);if(inn<=0)continue;bool locked=t>=f.Lock;float beat=locked?.5f+.5f*(float)Math.Sin(t*44):.5f+.5f*(float)Math.Sin(t*11),rad=NativeBombs.CircleRadius*(1.25f-.25f*inn);int seg=low?20:44;
                Ring(s,at,rad,.09f+.05f*beat,BombRed*((1.2f+.9f*beat)*inn),seg);Ring(s,at,rad*.5f,rad,BombRed*((locked?.34f:.14f)*inn),low?16:32);
                // The closing ring, telling you how long.
                if(toStrike>0){float span=Math.Max(.05f,f.Strike-(f.At+NativeBombs.PlungeAt)),left=Clamp01(toStrike/span);Ring(s,at,rad*left,.05f,(locked?BombHot:BombRed)*(1.5f*inn),seg);}
                int ticks=low?4:8;float spin=t*(locked?4:1.3f);for(int j=0;j<ticks;j++){float ang=spin+j*6.2831853f/ticks;var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));s.Line(false,at+d*(rad+.08f),at+d*(rad+.42f),BombRed*(1.6f*inn),.04f,true);}
                if(locked){s.Line(false,at+Vector3.UnitY*9,at,BombRed*(.7f+.6f*beat),.03f,true);}
            }
            // The plunger going home: a spark at the box.
            float since=t-f.Lock;if(since>=0&&since<.5f){var box=N(f.Box)+Vector3.UnitY*.72f;Flash(s,box,since,.2f,BombHot,.9f,seed);Sparks(s,box,since,.45f,Count(18,q,low),4,BombHot,seed+1,8,.018f,Vector3.UnitY*.6f);}
            // The fireworks over everything, after.
            float after=t-f.Strike;if(after<.25f)return;var centre=N(f.Box)+N(f.Dir)*7;int shells=low?4:7;
            for(int j=0;j<shells;j++)
            {
                float born=.25f+j*.12f,a=after-born;if(a<0||a>1)continue;long k=seed+100+j*17;var at=centre+new Vector3((R(k,1)-.5f)*12,6+3.5f*R(k,2),(R(k,3)-.5f)*10);var color=j%3==0?new Vector3(1,.3f,.12f):j%3==1?new Vector3(1,.8f,.25f):new Vector3(.9f,.25f,.5f);
                Flash(s,at,a,.18f,color,1.8f,k);int n=Count(24,q,low);for(int i=0;i<n;i++){var d=RandomDir(k,i+10);float sp=6*(.75f+.25f*R(k,i+40)),move=sp*(1-(float)Math.Exp(-a*3))/3;var p=at+d*move-Vector3.UnitY*(1.6f*a*a);s.Streak(p,d*(sp*(float)Math.Exp(-a*3))-Vector3.UnitY*(3.2f*a),color*(3.2f*(1-a)),.04f,.08f,.05f,.5f,a,k+i);}
            }
        }
    }

    /// <summary>
    /// The Bombadier's arms in first person (docs/BOMBADIER.md): a bundle of TNT low in each corner of the view, its fuse spitting, thrown from there. Pure functions of the kit and the clock on the view rig's
    /// own joints, in place of the game rig's gun hold. A throw dips below the frame, comes up and through toward the middle of the view, and drops out of it again, where the next stick comes from. (Over the shoulder would swing the arm past the lens.)
    /// </summary>
    public static class NativeBombView
    {
        private struct Arm
        {
            public float Ax,Ay,Az,Ex,Hx;public Arm(float ax,float ay,float az,float ex,float hx){Ax=ax;Ay=ay;Az=az;Ex=ex;Hx=hx;}
            public static Arm Mix(Arm a,Arm b,float u)=>new Arm(a.Ax+(b.Ax-a.Ax)*u,a.Ay+(b.Ay-a.Ay)*u,a.Az+(b.Az-a.Az)*u,a.Ex+(b.Ex-a.Ex)*u,a.Hx+(b.Hx-a.Hx)*u);
        }
        private static float Smooth(float x){x=x<0?0:x>1?1:x;return x*x*(3-2*x);}
        private static Arm Over(Arm rest,Arm top,Arm through,Arm low,NativeBombKit k,float time)
        {
            float s=time-k.CastRelease;if(s<0)return Arm.Mix(rest,top,Smooth((time-k.CastAt)/Math.Max(.02f,k.CastRelease-k.CastAt)));
            if(s<.09f)return Arm.Mix(top,through,Smooth(s/.09f));if(s<.27f)return Arm.Mix(through,low,Smooth((s-.09f)/.18f));return Arm.Mix(low,rest,Smooth((s-.27f)/.24f));
        }
        private static Arm Pose(int side,NativeBombKit k,float time,float reloadAt,float reloadUntil,float blockUntil)
        {
            float s=side;var rest=new Arm(-1.02f+(float)Math.Sin(time*1.6f+side)*.02f,s*.22f,s*.38f,-.85f,1.02f);var arm=rest;var low=new Arm(-.2f,0,s*.3f,-.5f,.2f);
            if(side<0&&time>=reloadAt&&time<reloadUntil){float u=(time-reloadAt)/Math.Max(.05f,reloadUntil-reloadAt);arm=Arm.Mix(rest,low,Smooth(u/.2f)*(1-Smooth((u-.72f)/.28f)));}
            if(time<blockUntil)arm=new Arm(-1.55f,-s*.6f,-s*.1f,-1.9f,.25f);
            if(k?.CastKind==null||time<k.CastAt||time>=k.CastUntil)return arm;bool left=side>0;
            var top=new Arm(-.5f,s*.3f,s*.38f,-1.25f,.7f);var through=new Arm(-1.75f,-s*.35f,-s*.06f,-.25f,-.4f);
            switch(k.CastKind)
            {
                case NativeBombs.PopBomb:return NativeBombs.ThrowsLeft(k.CastAt)==left?Over(rest,top,through,low,k,time):arm;
                case NativeBombs.StickyFuse:return left?arm:Over(rest,top,through,low,k,time);
                case NativeBombs.BounceMine:return left?Over(rest,low,through,low,k,time):arm;
                case NativeBombs.DeadMansSwitch:{if(!left)return arm;var up=new Arm(-1.32f,-s*.5f,s*.02f,-1.5f,.12f);float press=time>=k.CastRelease?1-Smooth((time-k.CastRelease)/.12f):0;up.Ex-=.1f*press;return Arm.Mix(rest,up,Smooth((time-k.CastAt)/.12f)*(1-Smooth((time-k.CastUntil+.14f)/.14f)));}
                case NativeBombs.ClusterLob:return Over(rest,new Arm(-1.7f,-s*.5f,-s*.05f,-1,.2f),new Arm(-1.75f,-s*.45f,-s*.1f,-.3f,-.2f),low,k,time);
                case NativeBombs.FirecrackerLine:return Arm.Mix(arm,low,Smooth((time-k.CastAt)/.1f)*(1-Smooth((time-k.CastUntil+.14f)/.14f)));
                case NativeBombs.BlastJump:return Arm.Mix(arm,new Arm(-.5f,0,s*.7f,-.3f,0),Smooth((time-k.CastAt)/.08f)*(1-Smooth((time-k.CastUntil+.14f)/.14f)));
            }
            return arm;
        }
        private static readonly int[] Torso={CapturedRig.Body,CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.LClav,CapturedRig.RClav};
        /// <summary>Both arms onto the view rig's pose.</summary>
        public static void Apply(CapturedRig.Pose p,NativeBombKit k,float time,float reloadAt,float reloadUntil,float blockUntil)
        {
            // Square to the view: the game rig's own first-person lean is for what it holds, and would carry the arms down with it.
            foreach(int b in Torso)p.Rotation[b]=Quaternion.Identity;
            for(int side=1;side>=-1;side-=2)
            {
                var a=Pose(side,k,time,reloadAt,reloadUntil,blockUntil);
                p.Rotation[side>0?CapturedRig.LArm:CapturedRig.RArm]=CapturedRig.Euler(a.Ax,a.Ay,a.Az);p.Rotation[side>0?CapturedRig.LElbow:CapturedRig.RElbow]=CapturedRig.Euler(a.Ex);p.Rotation[side>0?CapturedRig.LHand:CapturedRig.RHand]=CapturedRig.Euler(a.Hx);
            }
        }
    }

    /// <summary>The Grand Finale's private camera for the caster (docs/BOMBADIER.md): low on the box, the hands on the handle, up to see the circles, and home. Everyone else keeps theirs.</summary>
    public static class NativeBombCamera
    {
        public const float BlendIn=.14f,BlendOut=.7f;
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        public static bool Active(NativeBombFinale f,float time)=>f!=null&&f.At>-50&&time>=f.At&&time<f.Until;
        /// <summary>The shot at `time`. `reduced`: a gentle path: no shake, a wider lens, slow moves.</summary>
        public static KernelPanicCamera.Shot At(NativeBombFinale f,Vector3 feet,float time,bool reduced,bool shake)
        {
            var s=new KernelPanicCamera.Shot();float t=time-f.At;var d=new Vector3(f.Dir.x,0,f.Dir.z);d=d.LengthSquared()>1e-5f?Vector3.Normalize(d):Vector3.UnitZ;var r=new Vector3(d.Z,0,-d.X);var box=new Vector3(f.Box.x,f.Box.y,f.Box.z);float lk=f.Lock-f.At,st=f.Strike-f.At,end=f.Until-f.At;
            if(t<.6f){float u=Smooth(t/.6f);s.Position=box+d*(1.7f-.3f*u)+r*(.9f-.2f*u)+Vector3.UnitY*(.42f+.1f*u);s.Look=box+Vector3.UnitY*(.18f+.3f*u);s.Fov=46-6*u;s.Index=0;}
            else if(t<lk-.25f){float u=Smooth((t-.6f)/Math.Max(.1f,lk-.25f-.6f));s.Position=box+d*(1.1f+.3f*u)-r*(.75f+.2f*u)+Vector3.UnitY*(.9f+.3f*u);s.Look=box-d*.2f+Vector3.UnitY*(.72f+.18f*u);s.Fov=34;s.Index=1;}
            else if(t<st){float u=Smooth((t-(lk-.25f))/Math.Max(.1f,st-(lk-.25f)));s.Position=feet-d*(2.8f+2.6f*u)+r*(1.2f*u)+Vector3.UnitY*(1.7f+5.5f*u);s.Look=feet+d*(3+4*u)+Vector3.UnitY*(1-.4f*u);s.Fov=46+10*u;s.Index=2;s.Shake=shake?.02f*u:0;}
            else{float u=Smooth((t-st)/.7f);s.Position=feet-d*5.4f+r*1.2f+Vector3.UnitY*(7.2f-1.6f*u);s.Look=feet+d*7+Vector3.UnitY*(.6f+6.5f*u);s.Fov=56+4*u;s.Index=3;s.Shake=shake?.08f*(1-u):0;}
            if(reduced){s.Fov=Math.Max(s.Fov,48);s.Shake=0;}
            s.Blend=Smooth(t/BlendIn)*(1-Smooth((t-(end-BlendOut))/BlendOut));return s;
        }
    }
}
