using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
using Plan=ChipArena.UnityConversion.NativeWeaponFlourishPlan;
namespace ChipArena.UnityConversion
{
    // Each weapon's selection flourish and lock-in finisher (NativeWeaponFlourish.cs has the frame, the kit and the card's
    // part, Core's NativeWeaponFlourishPlan the lengths and sounds). Times are seconds into the flourish; sizes are in the
    // card's icon size `fs`, so a flourish scales with the grid. `fc` is the card's icon, `fr` the card, `fdir` the side of
    // the screen with more room. Every particle is a closed-form function of `t`.
    public static partial class NativeWeaponCard
    {
        private static void DrawMove(Plan.Move m,float t)
        {
            switch(m)
            {
                case Plan.Move.Pistol:Pistol(t);break;case Plan.Move.Rifle:Rifle(t);break;case Plan.Move.MachineGun:MachineGun(t);break;
                case Plan.Move.Fire:Fire(t);break;case Plan.Move.Water:Water(t);break;case Plan.Move.Lightning:Lightning(t);break;
                case Plan.Move.Darkness:Darkness(t);break;case Plan.Move.Sword:Sword(t);break;case Plan.Move.Daggers:Daggers(t);break;
                case Plan.Move.Shield:Shield(t);break;case Plan.Move.Greatsword:Greatsword(t);break;case Plan.Move.Bow:Bow(t);break;
                case Plan.Move.Katana:Katana(t);break;case Plan.Move.Glaive:Glaive(t);break;case Plan.Move.Fangs:Fangs(t);break;
                case Plan.Move.DevGreatsword:DevGreatsword(t);break;case Plan.Move.DevKatana:DevKatana(t);break;case Plan.Move.Bombadier:Bombadier(t);break;
                case Plan.Move.TopDog:TopDog(t);break;case Plan.Move.Siren:Siren(t);break;case Plan.Move.Emperor:Emperor(t);break;
                case Plan.Move.Ansatsuken:Ansatsuken(t);break;case Plan.Move.Leet:Leet(t);break;case Plan.Move.CyberLord:CyberLord(t);break;
                default:Generic(t);break;
            }
        }

        // ---- shared beats ------------------------------------------------------------------------------------------------
        // The icon leaving its slot: a ring and a flash on the card where it was.
        private static void Launch(float t,float at=0)
        {
            float u=Seg(t,at,at+.32f);if(u<=0||u>=1)return;Ring(fc,fs*(.6f+1.6f*M.OutCubic(u)),U.A(fh,.9f*(1-u)));Dot(fc,fs*2.8f,U.A(fa,.45f*(1-u)));Star(fc,fs*.9f*(1-u),15,8,U.A(fh,1-u));
        }
        // The weapon snapping back into its slot: a flash, a ring, a few sparks.
        private static void Land(float t,float at)
        {
            float u=t-at;if(u<0||u>.45f)return;float k=1-u/.45f;Star(fc,fs*.95f*k,20,8,U.A(fh,k));Ring(fc,fs*(.6f+1.5f*(1-k)),U.A(fa,.8f*k));Dot(fc,fs*2.4f,U.A(fa,.35f*k));Sparks(fc,u,14,77,-90,300,560,.45f,fh,500,2.4f);
        }
        // From the slot to `to`, `spins` turns on the way (out), or back (in), on an arc that rises.
        private static Vector2 Fly(Vector2 a,Vector2 b,float u,float rise)=>Vector2.Lerp(a,b,u)+new Vector2(0,-rise*Mathf.Sin(u*Mathf.PI));
        // A target point toward `p`: the nearest other card's face near it (a little off centre) when one is within reach.
        private static Vector2 Snap(Vector2 p,int salt)
        {
            p=Clamp(p,40);float best=float.MaxValue;var at=p;
            if(seenFrame==Time.frameCount)for(int i=0;i<seenCount;i++)
            {
                var c=seen[i];if(c.Overlaps(fr))continue;float d=(c.center-p).sqrMagnitude;
                if(d<best){best=d;at=c.center+new Vector2(Sgn(salt,i)*c.width*.2f,Sgn(salt+1,i)*c.height*.16f-c.height*.06f);}
            }
            return best<300*300?at:p;
        }
        // Where the line a-b enters and leaves rect r (false when it misses).
        private static bool Through(Vector2 a,Vector2 b,Rect r,out Vector2 p,out Vector2 q)
        {
            float t0=0,t1=1;var d=b-a;p=q=a;
            if(!Clip(-d.x,a.x-r.x,ref t0,ref t1)||!Clip(d.x,r.xMax-a.x,ref t0,ref t1)||!Clip(-d.y,a.y-r.y,ref t0,ref t1)||!Clip(d.y,r.yMax-a.y,ref t0,ref t1))return false;
            p=a+d*t0;q=a+d*t1;return true;
        }
        private static bool Clip(float den,float num,ref float t0,ref float t1)
        {
            if(Mathf.Abs(den)<1e-5f)return num>=0;float r=num/den;
            if(den<0){if(r>t1)return false;if(r>t0)t0=r;}else{if(r<t0)return false;if(r<t1)t1=r;}return true;
        }
        // A straight blade cut a->b over [t0, t1]: its trail (bent `bend` of its length to one side), the blade riding the
        // head, sparks where it crosses the card's edge, and the mark it leaves on the card.
        private static void Slash(Vector2 a,Vector2 b,float bend,float t0,float t1,float t,float w,float fwd,bool blade,int salt)
        {
            var d=b-a;float len=d.magnitude;if(len<1)return;var n=new Vector2(-d.y,d.x)/len;var mid=(a+b)*.5f;
            // The arc through a and b bowing `bend` of the chord: its centre on the normal through the midpoint.
            float sag=Mathf.Max(.02f,Mathf.Abs(bend))*len,rad=(len*len*.25f+sag*sag)/(2*sag);var o=mid-n*Mathf.Sign(bend==0?1:bend)*(rad-sag);
            float a0=Deg(a-o),a1=Deg(b-o);if(a1-a0>180)a1-=360;if(a0-a1>180)a1+=360;
            float u=Seg(t,t0,t1),fade=1-Seg(t,t1,t1+.28f);
            if(t>=t0)Sweep(o,rad,a0,a1,Mathf.Max(0,u-.55f)*fade+(1-fade),M.OutCubic(u),w,fade);
            if(blade&&t>=t0-.04f&&t<=t1+.03f){float uu=M.OutCubic(u);var p=o+Dir(Mathf.Lerp(a0,a1,uu))*rad;float tan=Mathf.Lerp(a0,a1,uu)+(a1>a0?90:-90);Arm(p,1.25f,tan-fwd,1,.9f,Dir(tan)*1500);}
            if(Through(a,b,fr,out var p0,out var q0))
            {
                float s=t-Mathf.Lerp(t0,t1,.35f);Sparks(p0,s,11,salt,Deg(a-b)+180,110,640,.45f,fh,600);Sparks(q0,t-Mathf.Lerp(t0,t1,.75f),11,salt+3,Deg(b-a),110,640,.45f,fh,600);
                float mark=Seg(t,t1-.04f,t1)*(1-Seg(t,t1+.25f,t1+.7f));Cut(p0,q0,3.2f,U.A(fh,.9f*mark));
            }
        }
        // A light pillar rising from `p` (the finisher's beam).
        private static void Pillar(Vector2 p,float w,float a)
        {
            if(a<=.003f)return;U.Fade(new Rect(p.x-w*.5f,p.y-U.H,w,U.H),U.A(fa,.32f*a),false);U.Fade(new Rect(p.x-w*.12f,p.y-U.H*.8f,w*.24f,U.H*.8f),new Color(1,1,1,.4f*a),false);
        }

        // ---- Pistol: out of the icon in a spin, three shots round the screen (the last behind it), a gunslinger's twirl home.
        private static readonly Vector2[] aims=new Vector2[3];
        private static void PistolPose(float t,Vector2 hold,out Vector2 p,out float scale,out float deg,out float sx)
        {
            const float fwd=0;float d0,d1,d2,s0,s1,s2;Aim(hold,aims[0],fwd,out d0,out s0);Aim(hold,aims[1],fwd,out d1,out s1);Aim(hold,aims[2],fwd,out d2,out s2);
            float k1=InOut(Seg(t,.52f,.64f)),k2=InOut(Seg(t,.8f,.95f));deg=Mathf.LerpAngle(Mathf.LerpAngle(d0,d1,k1),d2,k2);sx=Mathf.Lerp(Mathf.Lerp(s0,s1,k1),s2,k2);
            p=hold+new Vector2(0,Mathf.Sin(t*7)*2);scale=1.3f;
            if(t<.28f){float u=M.OutCubic(t/.28f);p=Fly(fc,hold,u,fs*.7f);scale=1+.3f*u;deg+=-fdir*540*(1-u);}
            for(int i=0;i<3;i++){float s=t-(.4f+.3f*i);if(s>0){float r=Mathf.Exp(-s*13);var aim=(aims[i]-hold).normalized;p-=aim*fs*.4f*r;deg+=-Mathf.Sign(sx)*30*r;}}
            if(t>1.22f){float u=InOut(Seg(t,1.22f,1.62f));p=Fly(hold,fc,u,fs*.9f);scale=Mathf.Lerp(1.3f,1,u);deg+=-Mathf.Sign(sx)*720*u;}
        }
        private static void Pistol(float t)
        {
            var hold=Clamp(fc+new Vector2(fdir*fs*1.5f,-fs*1.65f),fs*1.5f);
            aims[0]=Snap(hold+new Vector2(fdir*U.W*.42f,-U.H*.14f),3);aims[1]=Snap(hold+new Vector2(fdir*U.W*.3f,U.H*.3f),5);aims[2]=Snap(hold+new Vector2(-fdir*U.W*.26f,-U.H*.08f),7);
            Launch(t);
            // The shots: flash, tracer, the hole, sparks and a casing.
            for(int i=0;i<3;i++)
            {
                float st=.4f+.3f*i,s=t-st;if(s<0)continue;PistolPose(st,hold,out var p,out float sc,out float dg,out float sx);
                var mz=On(p,sc,dg,sx,.47f,-.17f);var tgt=aims[i];var dir=(tgt-mz).normalized;float dist=(tgt-mz).magnitude;
                if(s<.09f){float k=1-s/.09f;Star(mz,fs*.95f*k,Deg(dir),6,U.A(fh,1));Dot(mz,fs*3.2f,U.A(fa,.4f*k));Flash(.05f*k,fh);}
                float head=Seg(s,0,.06f);if(s<.26f){var hp=Vector2.Lerp(mz,tgt,head);var tp=Vector2.Lerp(mz,tgt,Mathf.Max(0,head-Mathf.Min(1,220/dist)));Beam(tp,hp,3.6f,U.A(fh,1-Seg(s,.06f,.26f)));Streak(mz,hp,1.8f,U.A(fa,.45f*(1-Seg(s,0,.3f))));}
                float hit=s-.06f;Hole(tgt,hit,fs*.13f,30+i);Sparks(tgt,hit,16,40+i,Deg(-dir),130,720,.5f,fh);if(hit>0&&hit<.3f){Ring(tgt,fs*(.3f+1.5f*hit/.3f),U.A(fa,.8f*(1-hit/.3f)));Star(tgt,fs*.7f*(1-hit/.3f),Deg(dir),6,U.A(fh,1-hit/.3f));}
                Casing(On(p,sc,dg,sx,.05f,-.22f),s,i,-Mathf.Sign(sx),fr.yMax+6);Smoke(mz,s-.03f,3,45+i,fs*.35f,.6f,.22f,new Vector2(0,-30));
            }
            if(t<1.66f){PistolPose(t,hold,out var p,out float sc,out float dg,out float sx);
                for(int g=3;g>=1;g--){PistolPose(t-g*.022f,hold,out var gp,out float gs,out float gd,out float gx);Arm(gp,gs,gd,gx,.16f*(4-g)*(t>1.22f||t<.28f?1:0),default,0);}
                PistolPose(t-.016f,hold,out var pp,out _,out _,out _);Arm(p,sc,dg,sx,1-Seg(t,1.58f,1.65f),(p-pp)/.016f);}
            Land(t,1.62f);
        }

        // ---- Rifle: out, scopes in on the farthest card (the screen dims round the scope), one shot across the grid, the bolt.
        private static Vector2 rifleAim;
        private static void RiflePose(float t,Vector2 hold,out Vector2 p,out float scale,out float deg,out float sx)
        {
            const float fwd=-49;Aim(hold,rifleAim,fwd,out deg,out sx);p=hold+new Vector2(0,Mathf.Sin(t*5)*1.5f*(1-Seg(t,.6f,.9f)));scale=1.4f;
            if(t<.3f){float u=M.OutCubic(t/.3f);p=Fly(fc,hold,u,fs*.6f);scale=1+.4f*u;deg=Mathf.LerpAngle(0,deg,u);sx=Mathf.Lerp(1,sx,u);}
            float s=t-.92f;if(s>0){float r=Mathf.Exp(-s*8);p-=(rifleAim-hold).normalized*fs*.6f*r;deg+=-Mathf.Sign(sx)*34*r;}
            float b=Bell(t,1.26f,1.42f);deg+=Mathf.Sign(sx)*8*b;
            if(t>1.55f){float u=InOut(Seg(t,1.55f,1.82f));p=Fly(hold,fc,u,fs*.7f);scale=Mathf.Lerp(1.4f,1,u);deg=Mathf.LerpAngle(deg,0,u);sx=Mathf.Lerp(sx,1,u);}
        }
        private static void Rifle(float t)
        {
            var hold=Clamp(fc+new Vector2(fdir*fs*.6f,-fs*1.75f),fs*1.5f);rifleAim=Target(0,hold,true,out var tgt,out var card)?tgt:Clamp(hold+new Vector2(fdir*U.W*.55f,-U.H*.05f),60);
            Launch(t);
            // The scope: it swings onto the target and tightens; the screen darkens round it.
            float sc=Seg(t,.3f,.46f)*(1-Seg(t,1.02f,1.3f));
            if(sc>0)
            {
                float u=M.OutCubic(Seg(t,.3f,.78f));var sway=new Vector2(Mathf.Sin(t*9),Mathf.Cos(t*7))*fs*.18f*(1-u);var s=Vector2.Lerp(hold+(rifleAim-hold)*.35f,rifleAim,u)+sway;float rad=Mathf.Lerp(fs*3.4f,fs*1.9f,u);
                var hole=Square(s,rad*2);var dark=new Color(.01f,.02f,.05f,.66f*sc);
                U.Fill(new Rect(0,0,U.W,Mathf.Max(0,hole.y)),dark);U.Fill(new Rect(0,hole.yMax,U.W,Mathf.Max(0,U.H-hole.yMax)),dark);
                U.Fill(new Rect(0,hole.y,Mathf.Max(0,hole.x),hole.height),dark);U.Fill(new Rect(hole.xMax,hole.y,Mathf.Max(0,U.W-hole.xMax),hole.height),dark);
                U.RadialStops(hole,dark,HoleT,HoleTA);Dot(s,rad*2,U.A(fa,.06f*sc));
                var ink=U.A(fh,.85f*sc);Thin(s,rad*.97f,ink);Thin(s,rad*.62f,U.A(fa,.35f*sc));
                float gap=rad*.12f;Line(new Vector2(s.x-rad,s.y),new Vector2(s.x-gap,s.y),1.6f,ink);Line(new Vector2(s.x+gap,s.y),new Vector2(s.x+rad,s.y),1.6f,ink);Thin(s,rad*1.03f,U.A(fa,.5f*sc));
                Line(new Vector2(s.x,s.y-rad),new Vector2(s.x,s.y-gap),1.6f,ink);Line(new Vector2(s.x,s.y+gap),new Vector2(s.x,s.y+rad),1.6f,ink);
                for(int i=1;i<=4;i++){float o=rad*.18f*i;Line(new Vector2(s.x+o,s.y-3),new Vector2(s.x+o,s.y+3),1,ink);Line(new Vector2(s.x-o,s.y-3),new Vector2(s.x-o,s.y+3),1,ink);Line(new Vector2(s.x-3,s.y+o),new Vector2(s.x+3,s.y+o),1,ink);}
                Dot(s,8,new Color(1,.25f,.3f,.95f*sc));Dot(s,26,new Color(1,.25f,.3f,.35f*sc));
                float lockIn=M.OutCubic(Seg(t,.62f,.86f));Reticle(s,Mathf.Lerp(rad*1.6f,rad*1.12f,lockIn),t*120,U.A(fh,.7f*sc*Seg(t,.55f,.7f)),4);
                if(t>.8f&&t<.92f&&Mathf.Repeat(t*14,1)<.6f)U.Text(new Rect(s.x+rad*.2f,s.y+rad*.18f,140,16),"TARGET LOCKED",11,new Color(1,.4f,.45f,.9f*sc),TextAnchor.UpperLeft,bold:true,mono:true,tracking:.25f);
            }
            // The shot: a hitscan beam that leaves a smoke trail, the impact cracking the target.
            float sh=t-.92f;RiflePose(.92f,hold,out var p0,out float s0,out float d0,out float x0);var mz=On(p0,s0,d0,x0,.42f,-.45f);var dir=(rifleAim-mz).normalized;
            if(sh>=0)
            {
                if(sh<.1f){Star(mz,fs*1.5f*(1-sh/.1f),Deg(dir),8,U.A(fh,1));Dot(mz,fs*4,U.A(fa,.45f*(1-sh/.1f)));Flash(.2f*(1-sh/.1f),fh);}
                if(sh<.35f)Beam(mz,rifleAim,6f*(1-sh/.35f)+1,U.A(fh,1-sh/.35f),1.8f);
                for(int i=0;i<12;i++){float k=(i+.5f)/12,life=.9f;float u=sh/life;if(u>1)break;var q=Vector2.Lerp(mz,rifleAim,k)+new Vector2(0,-fs*.25f*u)+new Vector2(-dir.y,dir.x)*Sgn(60,i)*fs*.12f*u;Dot(q,fs*(.35f+.9f*u),new Color(.75f,.8f,.9f,.2f*(1-u)*(1-u)));}
                Sparks(rifleAim,sh,24,61,Deg(-dir),160,900,.6f,fh);Hole(rifleAim,sh,fs*.16f,63);if(sh<.4f){Ring(rifleAim,fs*(.4f+2.2f*sh/.4f),U.A(fa,.85f*(1-sh/.4f)));Star(rifleAim,fs*1.1f*(1-sh/.4f),Deg(dir),8,U.A(fh,1-sh/.4f));}
                float cr=Seg(sh,0,.08f)*(1-Seg(sh,.7f,1));for(int i=0;i<7;i++){float a=i*51+Rnd(64,i)*30;var e=rifleAim+Dir(a)*fs*(.55f+.8f*Rnd(65,i))*cr;Line(rifleAim,e,1.8f,new Color(1,1,1,.65f*cr));Line(e,e+Dir(a+40)*fs*.3f*cr,1.3f,new Color(1,1,1,.45f*cr));}
            }
            Casing(On(p0,s0,d0,x0,-.05f,-.12f),t-1.32f,9,-Mathf.Sign(x0),fr.yMax+6);
            if(t<1.86f){RiflePose(t,hold,out var p,out float s,out float d,out float x);RiflePose(t-.016f,hold,out var pp,out _,out _,out _);Arm(p,s,d,x,1-Seg(t,1.78f,1.85f),(p-pp)/.016f);
                if(t>1.26f&&t<1.42f){var bolt=On(p,s,d,x,-.08f,-.2f);Line(bolt,bolt+Rot(new Vector2(0,-fs*.18f*Bell(t,1.26f,1.42f)),d),2.2f,U.A(fh,.9f));}}
            Land(t,1.82f);
        }

        // ---- Machine gun: a sweeping spray of tracers, a fountain of brass bouncing on the card, a hot smoking barrel.
        private const int Rounds=17;
        private static void GunPose(float t,Vector2 hold,out Vector2 p,out float scale,out float deg,out float sx)
        {
            float base0=fdir>0?0:180,sweep=-30+52*InOut(Seg(t,.3f,1.25f));float th=base0+fdir*sweep;sx=fdir;deg=sx>0?th:th-180;
            p=hold;scale=1.3f;
            if(t>.3f&&t<1.27f){int k=Mathf.Min(Rounds-1,(int)((t-.3f)*18));float s=t-(.3f+k/18f),r=Mathf.Exp(-s*30);p-=Dir(th)*fs*.2f*r;p+=new Vector2(Sgn(80,k),Sgn(81,k))*fs*.05f;deg+=Sgn(82,k)*3-sx*8*r;}
            if(t<.26f){float u=M.OutCubic(t/.26f);p=Fly(fc,hold,u,fs*.45f);scale=1+.3f*u;deg=Mathf.LerpAngle(0,deg,u)+-fdir*360*(1-u);}
            if(t>1.5f){float u=InOut(Seg(t,1.5f,1.74f));p=Fly(hold,fc,u,fs*.5f);scale=Mathf.Lerp(1.3f,1,u);deg=Mathf.LerpAngle(deg,0,u);sx=Mathf.Lerp(sx,1,u);}
        }
        private static void MachineGun(float t)
        {
            var hold=Clamp(fc+new Vector2(fdir*fs*1.15f,-fs*1.6f),fs*1.5f);Launch(t);
            for(int k=0;k<Rounds;k++)
            {
                float st=.3f+k/18f,s=t-st;if(s<0)break;GunPose(st,hold,out var p,out float sc,out float dg,out float sx);
                var mz=On(p,sc,dg,sx,.49f,-.17f);float th=(fdir>0?0:180)+fdir*(-30+52*InOut(Seg(st,.3f,1.25f)))+Sgn(83,k)*2.5f;var dir=Dir(th);
                if(s<.05f){Star(mz,fs*(.6f+.25f*Rnd(84,k))*(1-s/.05f),th,k%2==0?6:4,U.A(fh,1));Dot(mz,fs*2.4f,U.A(fa,.35f*(1-s/.05f)));Flash(.03f*(1-s/.05f),fh);}
                if(s<.55f){float head=s*3200,len=Mathf.Min(head,240);var hp=mz+dir*head;if(hp.x>-260&&hp.x<U.W+260&&hp.y>-260&&hp.y<U.H+260)Beam(hp-dir*len,hp,2.8f,U.A(fh,.95f-s));}
                Casing(On(p,sc,dg,sx,.08f,-.2f),s,k,-fdir,fr.yMax+4);
            }
            if(t<1.78f){GunPose(t,hold,out var p,out float sc,out float dg,out float sx);GunPose(t-.016f,hold,out var pp,out _,out _,out _);
                float heat=Seg(t,.3f,1.25f)*(1-Seg(t,1.25f,1.7f));var mz=On(p,sc,dg,sx,.49f,-.17f);
                Arm(p,sc,dg,sx,1-Seg(t,1.7f,1.77f),(p-pp)/.016f);Dot(mz,fs*.9f*heat,new Color(1,.45f,.15f,.75f*heat));Dot(On(p,sc,dg,sx,.38f,-.17f),fs*.55f*heat,new Color(1,.3f,.1f,.55f*heat));
                Smoke(mz,t-1.25f,8,85,fs*.55f,.9f,.34f,new Vector2(fdir*12,-40));}
            Land(t,1.74f);
        }

        // ---- Fire: the card catches, a fireball gathers over it and arcs across the grid, a burst of embers where it lands.
        private static void Flames(Rect band,float t,float power,int n,Color hot,Color tip,float height,int salt)
        {
            if(power<=.01f)return;
            for(int i=0;i<n;i++)
            {
                float per=.38f+.22f*Rnd(salt,i),u=Mathf.Repeat(t/per+Rnd(salt+1,i),1),x=band.x+band.width*(i+.5f)/n+Sgn(salt+2,i)*band.width*.04f;
                var p=new Vector2(x+Mathf.Sin(t*11+i)*3*u,band.yMax-height*u*(.7f+.5f*Rnd(salt+3,i)));float s=fs*.44f*(1-u)*(.7f+.6f*Rnd(salt+4,i))*power;
                Blob(p,s*.9f,s*1.6f,0,U.A(Color.Lerp(hot,tip,u),(1-u)*power*.9f));
            }
        }
        private static void Fire(float t)
        {
            var hot=new Color(1,.92f,.55f);var tip=new Color(1,.28f,.08f);
            float burn=Seg(t,0,.15f)*(1-Seg(t,.55f,.85f))+.35f*Seg(t,.55f,.85f)*(1-Seg(t,1.3f,1.7f));
            Dot(fc,fs*3.4f,new Color(1,.45f,.15f,.35f*burn));Flames(new Rect(fr.x+fr.width*.05f,fr.y,fr.width*.9f,fc.y+fs*.45f-fr.y),t,burn,16,hot,tip,fs*1.8f,10);
            var orb=fc+new Vector2(0,-fs*1.6f);Vector2 tgt=Snap(fc+new Vector2(fdir*U.W*.36f,-U.H*.04f),11);
            // Gathering: embers spiral in, the ball grows.
            float g=Seg(t,.3f,.72f);if(t>.3f&&t<.76f)for(int i=0;i<12;i++){float u=Mathf.Repeat(g*2+i/12f,1),a=i*30+u*240;var q=orb+Dir(a)*fs*1.7f*(1-u);Dot(q,fs*.16f,new Color(1,.6f,.2f,.85f*u));}
            float fly=Seg(t,.75f,1.02f);var at=t<.75f?orb:Fly(orb,tgt,InOut(fly),fs*2.2f);float r=fs*.66f*M.OutCubic(g)*(t<1.02f?1:0);
            if(t>.75f&&t<1.02f)for(int i=1;i<=10;i++){float k=Mathf.Max(0,fly-i*.035f);var q=Fly(orb,tgt,InOut(k),fs*2.2f);float a=(1-i/11f);Dot(q,r*2.2f*a,new Color(1,.4f+.4f*a,.1f,.35f*a));}
            if(r>0){Dot(at,r*4,new Color(1,.4f,.1f,.35f));Dot(at,r*2.2f,new Color(1,.55f,.15f,.85f));Dot(at,r*1.3f,new Color(1,.9f,.5f,1));Dot(at,r*.6f,Color.white);
                for(int i=0;i<5;i++){float a=t*400+i*72;Blob(at+Dir(a)*r*.7f,r*.5f,r*.9f,a+90,new Color(1,.7f,.3f,.6f));}}
            // The blast.
            float b=t-1.02f;if(b>=0)
            {
                if(b<.14f)Flash(.17f*(1-b/.14f),new Color(1,.6f,.2f));if(b<.4f){Dot(tgt,fs*(1.6f+4.6f*b/.4f),new Color(1,.55f,.15f,.65f*(1-b/.4f)));Dot(tgt,fs*1.9f*(1-b/.4f),Color.white);}
                Ring(tgt,fs*(.5f+3.3f*M.OutCubic(Seg(b,0,.5f))),new Color(1,.55f,.2f,.85f*(1-Seg(b,0,.5f))));Motes(tgt,b,34,12,-90,360,640,.85f,fs*.1f,new Color(1,.6f,.2f),500);
                Sparks(tgt,b,18,13,-90,300,760,.55f,hot);Smoke(tgt+new Vector2(0,-fs*.2f),b-.1f,8,14,fs*.9f,.9f,.34f,new Vector2(0,-40));
            }
            Motes(fc+new Vector2(0,-fs*.3f),t-.6f,14,15,-90,60,110,1,fs*.08f,new Color(1,.55f,.2f),-60,1);
        }

        // ---- Water: a globe wells out of the icon, wobbles, bursts in a crown of droplets that splash round the card.
        private static void Water(float t)
        {
            var light=new Color(.75f,.97f,1);float rise=M.OutCubic(Seg(t,0,.4f)),burst=t-.5f;var g=fc+new Vector2(0,-fs*1.3f*rise);
            if(burst<0)
            {
                float r=fs*.58f*M.OutCubic(Seg(t,0,.32f))*(1+.2f*Bell(t,.42f,.5f)),w=.12f*Mathf.Sin(t*30)*Mathf.Exp(-t*2.5f);
                if(t<.32f)Line(fc,g,fs*.22f*(1-Seg(t,.1f,.3f)),U.A(fa,.6f));
                Blob(g,r*2.9f*(1+w),r*2.9f*(1-w),0,U.A(fa,.28f));Blob(g,r*2*(1+w),r*2*(1-w),0,U.A(fa,.75f));Oval(g,r*(1+w),(1-w)/(1+w),0,U.A(light,.9f),true);
                Dot(g+new Vector2(-r*.35f,-r*.38f),r*.55f,new Color(1,1,1,.85f));for(int i=0;i<3;i++){float a=t*260+i*120;Dot(g+Dir(a)*r*.45f,r*.25f,U.A(light,.6f));}
            }
            else
            {
                if(burst<.3f){Ring(g,fs*(.5f+2.3f*burst/.3f),U.A(light,.85f*(1-burst/.3f)));Dot(g,fs*2.4f*(1-burst/.3f),U.A(fa,.45f));Flash(.05f*(1-burst/.3f),light);}
                float floor=fr.yMax+4;const float grav=1300;
                for(int i=0;i<26;i++)
                {
                    var v=Dir(-90+Sgn(20,i)*85)*(320+480*Rnd(21,i));float y0=g.y,disc=v.y*v.y+2*grav*(floor-y0),tl=(-v.y+Mathf.Sqrt(Mathf.Max(0,disc)))/grav;
                    var p=g+v*burst+new Vector2(0,.5f*grav*burst*burst);float s=fs*(.09f+.08f*Rnd(22,i));
                    if(burst<tl){var vel=v+new Vector2(0,grav*burst);Blob(p,s*(1.6f+vel.magnitude/500),s*1.5f,Deg(vel),U.A(fa,.85f));Dot(p+new Vector2(-s*.2f,-s*.2f),s*.6f,new Color(1,1,1,.8f));}
                    else{float u=(burst-tl)/.4f;var at=g+v*tl+new Vector2(0,.5f*grav*tl*tl);if(u<1){Oval(at,s*(1.5f+4*u),.32f,0,U.A(light,.8f*(1-u)),true);Motes(at,burst-tl,3,23+i,-90,90,150,.35f,s*.4f,light,900,1);}}
                }
            }
            for(int i=0;i<2;i++){float u=Seg(t,1+i*.18f,1.55f+i*.18f);if(u>0&&u<1)Oval(fc+new Vector2(0,fs*.25f),fs*(.3f+1.5f*u),.36f,0,U.A(light,.7f*(1-u)),true);}
        }

        // ---- Lightning: the card charges, then a bolt forks from card to card down the grid, each struck card flashing.
        private static readonly Vector2[] chain=new Vector2[5];private static readonly Rect[] chainCards=new Rect[5];private static int chainCount;
        private static void Chain()
        {
            chain[0]=fc;chainCount=1;
            for(int k=1;k<5;k++)
            {
                float best=float.MaxValue;int pick=-1;var from=chain[k-1];
                if(seenFrame==Time.frameCount)for(int i=0;i<seenCount;i++)
                {
                    var c=seen[i];if(c.Overlaps(fr))continue;bool used=false;for(int j=1;j<k;j++)if(chainCards[j]==c)used=true;if(used)continue;
                    float d=(c.center-from).magnitude;d=Mathf.Abs(d-fs*3.2f)*(1+.8f*Rnd(50+k,i));if(d<best){best=d;pick=i;}
                }
                if(pick>=0){chainCards[k]=seen[pick];chain[k]=seen[pick].center+new Vector2(0,-seen[pick].height*.07f);}
                else{chainCards[k]=default;chain[k]=Clamp(from+Dir(-60+Sgn(51,k)*70)*fs*3,50);}
                chainCount++;
            }
        }
        private static void Lightning(float t)
        {
            var bolt=U.Mix(fa,Color.white,.25f);int flick=(int)(t*30);Chain();
            float charge=Seg(t,0,.12f)*(1-Seg(t,.3f,.45f))+.4f*Seg(t,.9f,1)*(1-Seg(t,1.3f,1.6f));
            for(int i=0;i<4;i++)if(charge>.02f){Vector2 a=Edge(fr,Rnd(flick+i,1)),b=Edge(fr,Rnd(flick+i,2)*.25f+Rnd(flick+i,1));Bolt(a,b,flick*7+i,fs*.16f,2.2f,U.A(bolt,charge*.95f),5);}
            if(t>.25f&&t<.33f)Flash(.18f*Bell(t,.25f,.33f),bolt);
            for(int k=0;k+1<chainCount;k++)
            {
                float tk=.25f+k*.16f,s=t-tk;if(s<0||s>.34f)continue;float a=1-Seg(s,.08f,.34f);
                Bolt(chain[k],chain[k+1],flick*13+k,fs*.45f,5.5f,U.A(bolt,a),12);Bolt(chain[k],Vector2.Lerp(chain[k],chain[k+1],.6f)+new Vector2(Sgn(52,k+flick),Sgn(53,k+flick))*fs*.8f,flick*17+k,fs*.25f,2.4f,U.A(bolt,.7f*a),6);
                if(chainCards[k+1].width>0)CardFlash(chainCards[k+1],.9f*Bell(s,0,.25f),bolt);Star(chain[k+1],fs*1.1f*a,flick*20,8,U.A(bolt,a));Dot(chain[k+1],fs*3*a,U.A(bolt,.3f*a));Sparks(chain[k+1],s,14,54+k,-90,360,620,.45f,bolt);
            }
            Star(fc,fs*1.2f*Bell(t,.22f,.4f),t*90,8,U.A(bolt,1));Dot(fc,fs*3*Bell(t,.2f,.45f),U.A(bolt,.35f));
        }
        // A point on the rect's edge, `u` 0..1 round it.
        private static Vector2 Edge(Rect r,float u)
        {
            u=Mathf.Repeat(u,1)*2*(r.width+r.height);if(u<r.width)return new Vector2(r.x+u,r.y);u-=r.width;if(u<r.height)return new Vector2(r.xMax,r.y+u);u-=r.height;
            if(u<r.width)return new Vector2(r.xMax-u,r.yMax);u-=r.width;return new Vector2(r.x,r.yMax-u);
        }

        // ---- Darkness: a singularity opens on the icon, its disc turning, motes spiral in, it collapses and throws a dark ring.
        private static void Darkness(float t)
        {
            var o=fc+new Vector2(0,-fs*.05f);var vio=U.Mix(fa,Color.white,.15f);float grow=InOut(Seg(t,.08f,.6f))*(1-InCubic(Seg(t,.98f,1.12f)));float r=fs*(.16f+.62f*grow);
            float live=Seg(t,.05f,.15f)*(t<1.12f?1:0);
            if(live>0)
            {
                Soft(o,r*4.2f,U.A(fa,.5f*live));
                // The disc, behind then in front of the core: an ellipse of light, sparks riding it.
                for(int pass=0;pass<2;pass++)
                {
                    if(pass==1){Dot(o,r*2.2f,new Color(.01f,0,.03f,.97f*live));Thin(o,r*1.08f,U.A(vio,.9f*live));Thin(o,r*1.25f,new Color(1,1,1,.35f*live));}
                    Oval(o,r*1.9f,.26f,-16,U.A(vio,.75f*live),pass==1);
                    for(int i=0;i<12;i++){float a=t*300+i*30,sn=Mathf.Sin(a*Mathf.Deg2Rad);if(pass==0?sn>0:sn<=0)continue;var q=o+Rot(new Vector2(Mathf.Cos(a*Mathf.Deg2Rad)*r*1.9f,sn*r*1.9f*.26f),-16);Dot(q,fs*.12f,U.A(Color.white,.85f*live));}
                }
                // Motes drawn in on a spiral.
                for(int i=0;i<44;i++)
                {
                    float ts=.1f+i*.018f,u=Seg(t,ts,ts+.55f);if(u<=0||u>=1)continue;float r0=fs*(1.9f+2.6f*Rnd(70,i)),a=Rnd(71,i)*360+500*u*u,rr=r0*(1-u*u)+r*1.05f*u*u;
                    var q=o+Dir(a)*rr;var tan=Dir(a+90)*fs*(.18f+.35f*u);Streak(q-tan,q,2.4f,U.A(Color.Lerp(fa,Color.white,u),.9f*Mathf.Sin(u*Mathf.PI)));
                }
            }
            float b=t-1.12f;if(b>=0)
            {
                if(b<.1f)Flash(.32f*(1-b/.1f),new Color(0,0,.02f));if(b>.04f&&b<.18f)Flash(.12f*Bell(b,.04f,.18f),vio);
                float u=M.OutCubic(Seg(b,0,.5f));Ring(o,fs*(.5f+4.4f*u),U.A(new Color(.3f,.06f,.5f),.9f*(1-u)));Ring(o,fs*(.4f+3.6f*u),new Color(0,0,.02f,.5f*(1-u)));Thin(o,fs*(.6f+4.8f*u),new Color(1,1,1,.7f*(1-u)));
                Sparks(o,b,20,72,-90,360,720,.6f,vio,300);Motes(o,b,22,73,-90,360,320,.7f,fs*.08f,vio,-40,1.5f);
            }
        }

        // ---- One-Handed Sword: out spinning, a three-cut combo across the card (cross cut, backhand, overhead), sheathed home.
        private static void Sword(float t)
        {
            const float fwd=-45;var hold=fc+new Vector2(0,-fs*1.5f);Launch(t);
            Vector2 a1=new Vector2(fr.x-fs*1.1f,fr.y-fr.height*.05f),b1=new Vector2(fr.xMax+fs*1.1f,fr.yMax-fr.height*.12f),a2=new Vector2(fr.xMax+fs*1.1f,fr.y+fr.height*.82f),b2=new Vector2(fr.x-fs*1.1f,fr.y+fr.height*.22f);
            Vector2 a3=new Vector2(fr.center.x+fs*.3f,fr.y-fs*1.4f),b3=new Vector2(fr.center.x-fs*.2f,fr.yMax+fs*.8f);
            Slash(a1,b1,.16f,.36f,.5f,t,fs*.5f,fwd,true,90);Slash(a2,b2,-.18f,.6f,.74f,t,fs*.46f,fwd,true,94);Slash(a3,b3,.12f,.86f,1,t,fs*.56f,fwd,true,98);
            for(int i=0;i<3;i++){float ct=i==0?.43f:i==1?.67f:.93f;if(t>ct&&t<ct+.08f)Flash(.06f*(1-(t-ct)/.08f),fh);}
            // Between cuts and home: the blade carried round with afterimages.
            if(t<.36f){float u=M.OutCubic(Seg(t,0,.3f));var p=Fly(fc,hold,u,fs*.4f);float d=-360*(1-u)*fdir;float to=InOut(Seg(t,.3f,.36f));p=Vector2.Lerp(p,a1,to);Arm(p,1+.25f*u,d+Mathf.Lerp(0,Deg(b1-a1)+90-fwd-90,to),1,1,default);}
            else if(t>.5f&&t<.6f){float u=InOut(Seg(t,.5f,.6f));Arm(Vector2.Lerp(b1,a2,u)+new Vector2(0,-fs*.5f*Mathf.Sin(u*Mathf.PI)),1.25f,Mathf.Lerp(Deg(b1-a1),Deg(b2-a2),u)-fwd,1,.55f,(a2-b1)*10);}
            else if(t>.74f&&t<.86f){float u=InOut(Seg(t,.74f,.86f));Arm(Vector2.Lerp(b2,a3,u)+new Vector2(fs*.6f*Mathf.Sin(u*Mathf.PI),0),1.25f,Mathf.Lerp(Deg(b2-a2),Deg(b3-a3),u)-fwd,1,.55f,(a3-b2)*10);}
            else if(t>1f&&t<1.66f)
            {
                float u=InOut(Seg(t,1,1.3f)),v=InOut(Seg(t,1.3f,1.62f));var p=Vector2.Lerp(b3,hold,u);p=Vector2.Lerp(p,fc,v);float d=Mathf.Lerp(Deg(b3-a3)-fwd,-fwd-45+360*fdir,u);d=Mathf.Lerp(d,0,v);
                Arm(p,Mathf.Lerp(1.25f,1,v),d,1,1-Seg(t,1.6f,1.66f),default);if(u>0&&u<1)Sweep(p,fs*.85f,Deg(b3-a3),Deg(b3-a3)+360*fdir,Mathf.Max(0,u-.3f),u,fs*.2f,.7f);
            }
            Land(t,1.62f);
        }

        // ---- Daggers: the crossed pair twirls out like a pinwheel round the card, splits, both thunk into the card's top
        // corners, quiver, and are pulled home.
        private static void Dagger(Vector2 hilt,float deg,float len,float a)
        {
            if(a<=.003f)return;var d=Dir(deg);var n=new Vector2(-d.y,d.x);var tip=hilt+d*len;
            GlowLine(hilt,tip,len*.12f,U.A(fa,.35f*a),len*.12f);Line(hilt-n*len*.15f,hilt+n*len*.15f,len*.06f,U.A(fh,a));Line(hilt,hilt-d*len*.3f,len*.07f,U.A(U.Mix(fa,Color.black,.35f),a));
            Dot(hilt-d*len*.33f,len*.12f,U.A(fh,a));Line(hilt,hilt+d*len*.8f,len*.1f,U.A(fh,a));Streak(hilt+d*len*.55f,tip,len*.09f,new Color(1,1,1,a));Line(hilt,hilt+d*len*.75f,len*.03f,new Color(1,1,1,.8f*a));
        }
        private static void Daggers(float t)
        {
            Launch(t);float L=fs*1.25f;
            // The twirl: a figure of eight round the card with afterimages.
            if(t<1f)
            {
                for(int g=5;g>=0;g--)
                {
                    float tg=t-g*.025f;if(tg<0)continue;float u=Seg(tg,.05f,.98f),a=u*Mathf.PI*2;var path=fr.center+new Vector2(Mathf.Sin(a)*fr.width*1.05f,-Mathf.Sin(a*2)*fr.height*.42f-fs*.45f);
                    var p=Vector2.Lerp(fc,path,M.OutCubic(Seg(tg,0,.18f)));float spin=tg*1300*fdir;Arm(p,1.2f,spin,1,g==0?1:.14f,default,g==0?1:0);
                    if(g==0){Sweep(p,fs*.95f,spin-150,spin+20,.2f,1,fs*.16f,.65f,12);Sweep(p,fs*.95f,spin+30,spin+200,.2f,1,fs*.16f,.65f,12);}
                }
            }
            // The throw: two blades into the corners, quivering, then home.
            Vector2 c0=new Vector2(fr.x+fr.width*.16f,fr.y+fr.height*.05f),c1=new Vector2(fr.xMax-fr.width*.16f,fr.y+fr.height*.05f);var from=fr.center+new Vector2(0,-fs*.35f);
            for(int i=0;i<2;i++)
            {
                var corner=i==0?c0:c1;float deg=i==0?58:122;if(t<.96f||t>1.48f)continue;
                float fly=Seg(t,.96f,1.12f),back=InOut(Seg(t,1.3f,1.46f));var start=from+Dir(deg+180)*L*.6f;
                var hilt=Vector2.Lerp(Vector2.Lerp(start,corner-Dir(deg)*L*.62f,InCubic(fly)),fc,back);float q=t>1.12f?Mathf.Sin((t-1.12f)*55)*7*Mathf.Exp(-(t-1.12f)*7):0;
                float spin=fly<1?(1-fly)*720*(i==0?-1:1):0;Dagger(hilt,Mathf.Lerp(deg+spin+q,-90,back),L*(1-.4f*back),1-Seg(t,1.42f,1.48f));
                float hit=t-1.12f;if(hit>=0){Sparks(corner,hit,12,100+i,deg+180,140,520,.4f,fh);Motes(corner,hit,9,104+i,deg+180,120,280,.45f,fs*.06f,new Color(.9f,.85f,.7f),800);if(hit<.25f){Ring(corner,fs*(.2f+.9f*hit/.25f),U.A(fh,.85f*(1-hit/.25f)));Star(corner,fs*.6f*(1-hit/.25f),deg,6,U.A(fh,1-hit/.25f));}}
            }
            if(t>.94f&&t<1.02f){Star(from,fs*1f*Bell(t,.94f,1.02f),30,8,U.A(fh,1));Dot(from,fs*2.4f*Bell(t,.94f,1.02f),U.A(fa,.4f));}
            Land(t,1.46f);
        }

        // ---- Shield: wound back, then a charge and a bash on a wall of hardlight hexes, a rebound and a planted block.
        private static void ShieldPose(float t,out Vector2 p,out float scale,out float deg)
        {
            var back=fc+new Vector2(-fdir*fs*.6f,-fs*.9f);var hit=fc+new Vector2(fdir*fs*2.5f,-fs*.55f);var rest=fc+new Vector2(fdir*fs*.8f,-fs*1.35f);
            if(t<.25f){float u=M.OutCubic(t/.25f);p=Vector2.Lerp(fc,back,u);scale=1+.45f*u;deg=-fdir*14*u;}
            else if(t<.45f){float u=InCubic(Seg(t,.25f,.45f));p=Vector2.Lerp(back,hit,u);scale=1.45f;deg=-fdir*14*(1-u);}
            else if(t<1.1f){float u=Spring(Seg(t,.45f,.95f)*1.6f,6);p=Vector2.LerpUnclamped(hit,rest,u)+new Vector2(0,fs*.12f*Bell(t,.82f,1.02f));scale=1.45f-.12f*Bell(t,.45f,.55f);deg=fdir*6*Bell(t,.45f,.7f);}
            else{float u=InOut(Seg(t,1.1f,1.52f));p=Fly(rest,fc,u,fs*.4f);scale=Mathf.Lerp(1.45f,1,u);deg=0;}
        }
        private static void Shield(float t)
        {
            Launch(t);var hit=fc+new Vector2(fdir*fs*2.5f,-fs*.55f);var wall=hit+new Vector2(fdir*fs*1.05f,0);
            if(t>.27f&&t<.47f)for(int i=0;i<7;i++){float y=hit.y+Sgn(110,i)*fs*.6f,u=Seg(t,.27f,.47f);var a=new Vector2(Mathf.Lerp(fc.x-fdir*fs*.4f,hit.x,u)-fdir*fs*(.6f+.8f*Rnd(111,i)),y);Streak(a,a+new Vector2(fdir*fs*(.8f+.9f*Rnd(112,i)),0),2.2f,U.A(fh,.8f*u));}
            float b=t-.45f;if(b>=0)
            {
                float w=Bell(b,0,.45f);for(int i=-2;i<=2;i++)for(int j=0;j<2;j++){var hp=wall+new Vector2(fdir*j*fs*.52f,(i+(j==1?.5f:0))*fs*.6f);float k=w*Mathf.Clamp01(1.4f-Mathf.Abs(i)*.3f-j*.4f);Hex(hp,fs*.34f,30,2.6f,U.A(fh,.95f*k));Dot(hp,fs*.6f,U.A(fa,.26f*k));}
                if(b<.12f){Star(wall,fs*1.5f*(1-b/.12f),90,8,U.A(fh,1));Dot(wall,fs*4*(1-b/.12f),U.A(fa,.4f));Flash(.12f*(1-b/.12f),fh);}if(b<.45f)Ring(wall,fs*(.4f+3f*M.OutCubic(b/.45f)),U.A(fa,.9f*(1-b/.45f)));
                Sparks(wall,b,24,113,fdir>0?180:0,150,820,.55f,fh);
            }
            float dome=Bell(t,.88f,1.35f);if(dome>0){var dc=fc+new Vector2(0,fs*.2f);Oval(dc,fr.width*.95f,.5f,0,U.A(fa,.8f*dome));Soft(dc,fr.width*.9f,U.A(fa,.2f*dome));for(int i=0;i<7;i++){float a=-170+i*26.6f;var hp=dc+new Vector2(Mathf.Cos(a*Mathf.Deg2Rad)*fr.width*.9f,Mathf.Sin(a*Mathf.Deg2Rad)*fr.width*.58f);Hex(hp,fs*.26f,30,2.2f,U.A(fh,.85f*dome*Seg(t,.88f+i*.025f,.98f+i*.025f)));}}
            if(t<1.56f){ShieldPose(t,out var p,out float s,out float d);ShieldPose(t-.016f,out var pp,out _,out _);Arm(p,s,d,1,1-Seg(t,1.5f,1.56f),(p-pp)/.016f);}
            Land(t,1.52f);
        }

        // ---- Greatsword: hauled up out of the card, turned point down, a beat, then slammed into the card: an impact
        // frame, the card cracks and squashes, chunks and dust fly; it stands in the card, is wrenched free and goes home.
        private static void Cracks(Vector2 at,float grow,float a,int salt,int arms,float reach)
        {
            if(a<=.003f||grow<=0)return;
            for(int i=0;i<arms;i++)
            {
                float deg=i*360f/arms+Sgn(salt,i)*18;var p=at;float seg=reach*(.18f+.14f*Rnd(salt+1,i))*grow;
                for(int s=0;s<4;s++){deg+=Sgn(salt+2,i*7+s)*28;var q=p+Dir(deg)*seg;Line(p,q,3.2f-.6f*s,new Color(0,0,0,.75f*a));Line(p+new Vector2(1.2f,1.2f),q+new Vector2(1.2f,1.2f),1.2f,U.A(fh,.7f*a));
                    if(s==1&&Rnd(salt+3,i)>.5f){var br=q+Dir(deg+60*Sgn(salt+4,i))*seg*.7f;Line(q,br,1.2f,new Color(0,0,0,.6f*a));}p=q;seg*=.85f;}
            }
        }
        private static void Greatsword(float t)
        {
            float top=fr.y-fs*1.7f,tipY=fr.y+fr.height*.44f,sc=1.55f,half=fs*Big(sc)*.47f,hit=t-.66f;var x=fr.center.x;
            float sink=hit>0?9*Mathf.Exp(-hit*5):0;
            Launch(t);
            // The sword: up (point up), turned over (a flip in depth), held, dropped; stuck; wrenched out; flipped back, home.
            Vector2 p;float s,sy=1;
            if(t<.38f){float u=M.OutCubic(Seg(t,0,.38f));p=new Vector2(x,Mathf.Lerp(fc.y,top,u));s=Mathf.Lerp(1,sc,u);}
            else if(t<.56f){float u=InOut(Seg(t,.38f,.52f));p=new Vector2(x,top+Mathf.Sin(t*9)*1.5f);s=sc;sy=Mathf.Cos(u*Mathf.PI);}
            else if(t<.66f){float u=InCubic(Seg(t,.56f,.66f));p=new Vector2(x,Mathf.Lerp(top,tipY-half,u));s=sc;sy=-1;}
            else if(t<1.32f){p=new Vector2(x+(hit<.4f?Mathf.Sin(hit*70)*2.5f*(1-hit/.4f):0),tipY-half+sink);s=sc;sy=-1;}
            else if(t<1.5f){float u=M.OutCubic(Seg(t,1.32f,1.5f));p=new Vector2(x,Mathf.Lerp(tipY-half,top,u));s=sc;sy=-1;}
            else{float u=InOut(Seg(t,1.5f,1.8f));p=new Vector2(x,Mathf.Lerp(top,fc.y,u));s=Mathf.Lerp(sc,1,u);sy=Mathf.Cos(Mathf.Min(1,u*1.6f)*Mathf.PI)*-1;}
            float a=1-Seg(t,1.76f,1.82f);
            if(t>.48f&&t<.56f){var g=p+new Vector2(0,Mathf.Lerp(-half,half,Seg(t,.48f,.56f)));Star(g,fs*.7f,45,4,U.A(fh,1));}
            if(t>.56f&&t<.66f)for(int i=0;i<8;i++){float lx=x+(i-3.5f)*fs*.26f;Streak(new Vector2(lx,p.y-half*2.2f),new Vector2(lx,p.y-half*.3f),2.2f,U.A(fh,.75f));}
            bool stuck=t>=.66f&&t<1.5f;
            // Stuck: the point inside the card hidden. The blade is mirrored (point down) and IMGUI maps a clip through the
            // matrix drawn under, so the band is given in the mirrored frame: local y from 2p.y - cut shows on screen above `cut`.
            if(stuck){float cut=tipY+sink-fs*.06f;U.Push(p,Vector2.zero,1,0,-1);U.BeginBand(new Rect(-U.W,2*p.y-cut,U.W*3,U.H*2));Arm(p,s,0,1,a,default);U.EndBand();U.Pop();}
            else{var pv=new Vector2(0,t>.56f&&t<.66f?2200:0);U.Push(p,Vector2.zero,1,0,Mathf.Sign(sy)*Mathf.Max(.04f,Mathf.Abs(sy)));Arm(p,s,0,1,a,pv);U.Pop();}
            if(stuck&&hit<.18f)Dot(new Vector2(x,tipY+sink),fs*(2.6f-hit*10),U.A(fh,.65f));
            // The impact.
            if(hit>=0)
            {
                var at=new Vector2(x,tipY+sink);if(hit<.07f)Flash(.34f*(1-hit/.07f),Color.white);if(hit<.14f){Star(at,fs*1.8f*(1-hit/.14f),0,10,U.A(fh,1));Dot(at,fs*4.5f*(1-hit/.14f),U.A(fa,.45f));}
                Cracks(at,M.OutCubic(Seg(hit,0,.12f)),1-Seg(t,1.3f,1.62f),120,10,fr.width*1.05f);
                for(int i=0;i<22;i++){float life=.8f;if(hit>life)break;var v=Dir(-90+Sgn(121,i)*85)*(360+560*Rnd(122,i));var q=at+v*hit+new Vector2(0,.5f*1700*hit*hit);float sz=fs*(.09f+.1f*Rnd(123,i));
                    Box(q,sz,sz*.7f,hit*(400+600*Rnd(124,i)),new Color(.2f,.22f,.3f,1-hit/life));Box(q+new Vector2(-sz*.2f,-sz*.2f),sz*.5f,sz*.2f,hit*(400+600*Rnd(124,i)),U.A(fh,.6f*(1-hit/life)));}
                float du=M.OutCubic(Seg(hit,0,.7f));Oval(new Vector2(x,fr.yMax+2),fr.width*(.6f+1.3f*du),.22f,0,new Color(.8f,.82f,.9f,.55f*(1-du)));Soft(new Vector2(x,fr.yMax),fr.width*(.5f+1.2f*du),new Color(.6f,.65f,.75f,.3f*(1-du)));Smoke(new Vector2(x,fr.yMax-fs*.1f),hit,6,126,fs*.7f,.9f,.25f,new Vector2(0,-20));
                if(hit<.4f)Ring(at,fs*(.4f+2.8f*hit/.4f),U.A(fa,.85f*(1-hit/.4f)));Sparks(at,hit,20,125,-90,200,820,.55f,fh);
                float heal=Seg(t,1.3f,1.62f);if(heal>0&&heal<1){float y=Mathf.Lerp(fr.y,fr.yMax,heal);Beam(new Vector2(fr.x+4,y),new Vector2(fr.xMax-4,y),2.2f,U.A(fh,.9f*Bell(heal,0,1)));}
            }
            Land(t,1.8f);
        }

        // ---- Bow: raised and turned on the hero card, drawn (the string bends back, light gathers at the head), loosed: the
        // arrow arcs across and thunks into the hero card's frame, quivering, then breaks into light.
        private static void Arrow(Vector2 nock,float deg,float len,float a,bool lit)
        {
            if(a<=.003f)return;var d=Dir(deg);var n=new Vector2(-d.y,d.x);var head=nock+d*len;
            if(lit)GlowLine(nock,head,3,U.A(fa,.5f*a),7);Line(nock,head,2.6f,U.A(fh,a));
            Line(head,head-d*len*.16f+n*len*.08f,2.6f,new Color(1,1,1,a));Line(head,head-d*len*.16f-n*len*.08f,2.6f,new Color(1,1,1,a));
            for(int i=0;i<2;i++){var f=nock+d*len*(.04f+.07f*i);Line(f,f-d*len*.1f+n*len*.08f,2,U.A(fa,a));Line(f,f-d*len*.1f-n*len*.08f,2,U.A(fa,a));}
        }
        private static Vector2 bowAim;private static float bowFlight;
        private static void BowPose(float t,Vector2 hold,out Vector2 p,out float scale,out float deg,out float sx)
        {
            const float fwd=-44;Aim(hold,bowAim,fwd,out deg,out sx);p=hold;scale=1.3f;
            if(t<.3f){float u=M.OutCubic(t/.3f);p=Fly(fc,hold,u,fs*.5f);scale=1+.3f*u;deg=Mathf.LerpAngle(0,deg,u);sx=Mathf.Lerp(1,sx,u);}
            p+=Shake(t,1.2f*Seg(t,.45f,.72f)*(t<.75f?1:0));float rel=t-.75f;if(rel>0)deg+=-Mathf.Sign(sx)*10*Mathf.Exp(-rel*9)*Mathf.Cos(rel*30);
            if(t>1.02f){float u=InOut(Seg(t,1.02f,1.36f));p=Fly(hold,fc,u,fs*.4f);scale=Mathf.Lerp(1.3f,1,u);deg=Mathf.LerpAngle(deg,0,u);sx=Mathf.Lerp(sx,1,u);}
        }
        private static void Bow(float t)
        {
            var hold=Clamp(fc+new Vector2(fdir*fs*.35f,-fs*1.6f),fs*1.5f);
            if(HeroCard(out var hr)&&hr.x>fc.x){bowAim=new Vector2(hr.x+1,Mathf.Clamp(hold.y-fs*.4f,hr.y+fs*.6f,hr.yMax-fs*.6f));}
            else if(Target(0,hold,true,out var tg,out var card))bowAim=new Vector2(card.center.x<hold.x?card.xMax-1:card.x+1,card.center.y);else bowAim=Clamp(hold+new Vector2(fdir*U.W*.5f,0),20);
            Launch(t);BowPose(t,hold,out var p,out float s,out float d,out float sx);
            float pull=InOut(Seg(t,.3f,.68f))*(t<.75f?1:0),rel=t-.75f;var aim=Dir(d+(sx>0?-44:-136));var nockRest=On(p,s,d,sx,-.16f,.01f);var nock=nockRest-aim*fs*.6f*pull;
            if(rel>0&&rel<.3f)nock=nockRest+aim*fs*.08f*Mathf.Sin(rel*60)*Mathf.Exp(-rel*12);
            if(t<1.4f)
            {
                BowPose(t-.016f,hold,out var pp,out _,out _,out _);Arm(p,s,d,sx,1-Seg(t,1.3f,1.37f)-.25f*Bell(t,.3f,.8f),(p-pp)/.016f);var s1=On(p,s,d,sx,-.42f,-.38f);var s2=On(p,s,d,sx,.1f,.4f);float sa=1-Seg(t,1.3f,1.4f);Line(s1,nock,1.8f,new Color(1,1,1,.9f*sa));Line(nock,s2,1.8f,new Color(1,1,1,.9f*sa));
                if(t>.25f&&t<.75f){Arrow(nock,Deg(aim),fs*1.05f*Big(s),1,true);float glow=Seg(t,.35f,.72f);var head=nock+aim*fs*1.05f*Big(s);Dot(head,fs*1.3f*glow,U.A(fa,.45f*glow));Dot(head,fs*.35f*glow,new Color(1,1,1,.85f*glow));}
            }
            // Flight and the thunk.
            var a0=nockRest;float dist=(bowAim-a0).magnitude;bowFlight=Mathf.Clamp(dist/1700,.14f,.3f);float fl=Seg(t,.75f,.75f+bowFlight),arc=dist*.07f;
            if(rel>0&&fl<1)
            {
                Vector2 At(float u)=>Vector2.Lerp(a0,bowAim,u)+new Vector2(0,-arc*4*u*(1-u));var q=At(fl);var q0=At(Mathf.Max(0,fl-.06f));var dd=q-q0;
                Arrow(q-dd.normalized*fs*1.6f,Deg(dd),fs*1.6f,1,true);Streak(At(Mathf.Max(0,fl-.4f)),q-dd.normalized*fs*1.5f,3,U.A(fh,.7f));
                if(rel<.22f){Oval(a0+aim*fs*.9f,fs*(.4f+1.4f*rel/.22f),.35f,Deg(aim)+90,U.A(fh,.8f*(1-rel/.22f)),true);Oval(a0+aim*fs*.4f,fs*(.3f+.9f*rel/.22f),.35f,Deg(aim)+90,U.A(fa,.6f*(1-rel/.22f)),true);}
            }
            float hit=t-.75f-bowFlight;if(hit>=0&&t<1.75f)
            {
                var inc=(bowAim-(Vector2.Lerp(a0,bowAim,.94f)+new Vector2(0,-arc*4*.94f*.06f))).normalized;float q=Mathf.Sin(hit*48)*8*Mathf.Exp(-hit*6),fade=1-Seg(t,1.55f,1.72f);
                Arrow(bowAim-Dir(Deg(inc)+q)*fs*1.38f,Deg(inc)+q,fs*1.6f,fade,true);
                Sparks(bowAim,hit,14,130,Deg(-inc),120,560,.45f,fh);Motes(bowAim,hit,10,131,Deg(-inc),140,320,.5f,fs*.06f,new Color(.95f,.85f,.6f),900);
                if(hit<.35f){Ring(bowAim,fs*(.3f+1.5f*hit/.35f),U.A(fa,.85f*(1-hit/.35f)));Star(bowAim,fs*.9f*(1-hit/.35f),90,6,U.A(fh,1-hit/.35f));Beam(bowAim+new Vector2(0,-fs*1.4f),bowAim+new Vector2(0,fs*1.4f),2.4f,U.A(fh,.9f*(1-hit/.35f)));}
                if(t>1.55f)Motes(bowAim-inc*fs*.7f,t-1.55f,16,132,-90,360,160,.25f,fs*.06f,fh,0,1);
            }
            Land(t,1.36f);
        }

        // ---- Katana: drawn across the card, stillness and falling petals, then one iai cut: the card parts along it in a
        // frozen beat, closes, and the blade slides home with a click; petals burst.
        private static void Katana(float t)
        {
            const float fwd=-44;float y=fr.y+fr.height*.47f;var held=new Vector2(fr.center.x,fc.y-fs*1.5f);Launch(t);
            // Petals drifting through the stillness (they slow almost to a stop in the cut's frozen beat).
            float slow=t<.6f?t:t<.9f?.6f+(t-.6f)*.12f:.636f+(t-.9f);
            for(int i=0;i<12;i++){float u=Mathf.Repeat(slow*.45f+Rnd(140,i),1),a=Bell(t,.15f,1.4f);var q=new Vector2(fr.x-fs*.8f+(fr.width+fs*1.6f)*Rnd(141,i)+Mathf.Sin(slow*3+i)*fs*.3f,fr.y-fs*.9f+(fr.height+fs*1.6f)*u);Petal(q,fs*.24f,slow*120+i*40,Mathf.Cos(slow*5+i),a*.85f);}
            if(t<.56f){float u=M.OutCubic(Seg(t,0,.3f));var p=Fly(fc,held,u,fs*.3f);Arm(p,1+.4f*u,Mathf.LerpAngle(0,-fwd,u),1,1,default);if(t>.36f&&t<.52f){var g=p+Dir(0)*Mathf.Lerp(-fs*.5f,fs*.6f,Seg(t,.36f,.52f));Star(g,fs*.55f,0,4,U.A(fh,.95f));}}
            // The cut: an after-image racing across, then the line itself.
            if(t>.54f&&t<.62f){float u=Seg(t,.54f,.6f);for(int g=0;g<5;g++){float k=Mathf.Max(0,u-g*.06f);Arm(new Vector2(Mathf.Lerp(fr.x-fs,fr.xMax+fs*1.2f,k),y),1.4f,-fwd,1,g==0?.8f:.15f,new Vector2(3000,0),g==0?1:0);}}
            float c=t-.58f;if(c>=0&&c<.4f){float a=1-Seg(c,.1f,.4f);var l=new Vector2(fr.x-fs*2.8f,y);var r=new Vector2(fr.xMax+fs*2.8f,y);Cut(l,r,5+5*(1-a),U.A(fh,a));if(c<.06f)Flash(.22f*(1-c/.06f),fh);
                Sparks(new Vector2(fr.x,y),c,12,142,180,60,700,.5f,fh,500);Sparks(new Vector2(fr.xMax,y),c,12,143,0,60,700,.5f,fh,500);}
            float seam=Seg(t,.58f,.62f)*(1-Seg(t,1.1f,1.18f));if(seam>0){Line(new Vector2(fr.x-4,y),new Vector2(fr.xMax+4,y),2.4f,U.A(fh,.95f*seam));GlowLine(new Vector2(fr.x,y),new Vector2(fr.xMax,y),3,U.A(fa,.6f*seam),10);}
            if(t>1.15f&&t<1.22f)Beam(new Vector2(fr.x,y),new Vector2(fr.xMax,y),2.6f,U.A(fh,Bell(t,1.15f,1.22f)));
            // Home: in from the right, turning back to its icon angle, the click at the guard.
            if(t>1.08f&&t<1.36f){float u=InOut(Seg(t,1.08f,1.32f));var from=new Vector2(fr.xMax+fs*.9f,fc.y);Arm(Vector2.Lerp(from,fc,u),Mathf.Lerp(1.4f,1,u),Mathf.Lerp(-fwd,0,u),1,1-Seg(t,1.3f,1.34f),new Vector2(-900,0));}
            float k2=t-1.32f;if(k2>=0&&k2<.3f){Star(fc+new Vector2(-fs*.22f,fs*.22f),fs*.8f*(1-k2/.3f),45,4,new Color(1,1,1,1-k2/.3f));Thin(fc,fs*(.5f+1.4f*k2/.3f),U.A(fh,.9f*(1-k2/.3f)));}
            float b=t-1.33f;if(b>=0)for(int i=0;i<36;i++){var v=Dir(-90+Sgn(144,i)*115)*(200+360*Rnd(145,i));const float drag=2.4f;float e=Mathf.Exp(-drag*b);var q=fc+v*((1-e)/drag)+new Vector2(Mathf.Sin(b*4+i)*fs*.25f,60*b*b);Petal(q,fs*(.2f+.1f*Rnd(146,i)),b*200+i*30,Mathf.Cos(b*7+i),1-Seg(b,.3f,.52f));}
        }

        // ---- Glaive: the ring blade spins up and is thrown on a wide boomerang loop round the grid, a ribbon of light
        // behind it, and is caught back in the card.
        private static Vector2 g1,g2;
        private static Vector2 GlaiveAt(float t){float u=InOut(Seg(t,.2f,1.24f));return Bez(fc,g1,g2,fc,u);}
        private static void Glaive(float t)
        {
            g1=Clamp(fc+new Vector2(fdir*U.W*.4f,-U.H*.42f),70);g2=Clamp(fc+new Vector2(fdir*U.W*.55f,U.H*.36f),70);
            float spin=t*1500*fdir;var p=t<.2f?fc:GlaiveAt(t);float s=t<.2f?1+.15f*Seg(t,.1f,.2f):1.2f;
            if(t>.2f&&t<1.26f)
            {
                var prev=GlaiveAt(t);for(int i=1;i<=16;i++){var q=GlaiveAt(t-i*.016f);float k=1-i/17f;GlowLine(prev,q,fs*.26f*k,U.A(fa,.38f*k),fs*.3f);Line(prev,q,fs*.17f*k,U.A(fa,.7f*k));Line(prev,q,fs*.05f*k+.8f,new Color(1,1,1,.85f*k));prev=q;}
                int burst=(int)((t-.2f)/.09f);for(int j=Mathf.Max(0,burst-4);j<=burst;j++){float bt=.2f+j*.09f;Sparks(GlaiveAt(bt),t-bt,6,150+j,Rnd(151,j)*360,140,480,.35f,fh,300);}
            }
            if(t<1.3f)
            {
                float tilt=t<.2f?1:.55f+.45f*Mathf.Abs(Mathf.Cos(Seg(t,.2f,1.24f)*Mathf.PI));float settle=t>1.24f?1-Seg(t,1.24f,1.3f):1;
                var vel=(GlaiveAt(t)-GlaiveAt(t-.016f))/.016f;U.Push(p,Vector2.zero,1,0,tilt);Arm(p,s,spin*settle,1,settle,vel);U.Pop();
                Thin(p,fs*.62f*Big(s),U.A(fh,.55f*Seg(t,.05f,.2f)*(t<1.24f?1:0)));
            }
            Land(t,1.24f);
        }

        // ---- Fangs: two venom blades rake an X across the card; the cuts glow and drip venom down the card, bubbling.
        private static void Fang(Vector2 p,float deg,float size,float a)
        {
            if(a<=.003f)return;var o=p+Dir(deg+90)*size*.9f;Sweep(o,size*.9f,deg-90-60,deg-90+20,0,1,size*.22f,a,10);
        }
        private static void Fangs(float t)
        {
            var tox=U.Mix(fa,Color.white,.2f);Vector2 a1=new Vector2(fr.x-fs*.9f,fr.y-fr.height*.02f),b1=new Vector2(fr.xMax+fs*.9f,fr.yMax+fr.height*.02f),a2=new Vector2(fr.xMax+fs*.9f,fr.y-fr.height*.02f),b2=new Vector2(fr.x-fs*.9f,fr.yMax+fr.height*.02f);
            if(t<.3f){float u=M.OutCubic(Seg(t,0,.22f));Fang(Vector2.Lerp(fc,a1,u),Deg(b1-a1)-30,fs*.85f,u);Fang(Vector2.Lerp(fc,a2,u),Deg(b2-a2)+30,fs*.85f,u);}
            Slash(a1,b1,.12f,.28f,.36f,t,fs*.42f,0,false,160);Slash(a2,b2,-.12f,.38f,.46f,t,fs*.42f,0,false,163);for(int i=0;i<2;i++){float ct=i==0?.33f:.43f;if(t>ct&&t<ct+.07f)Flash(.06f*(1-(t-ct)/.07f),fh);}
            if(t>.26f&&t<.4f){float u=Seg(t,.28f,.36f);Fang(Vector2.Lerp(a1,b1,u),Deg(b1-a1),fs*.9f,1-Seg(t,.36f,.4f));}
            if(t>.36f&&t<.5f){float u=Seg(t,.38f,.46f);Fang(Vector2.Lerp(a2,b2,u),Deg(b2-a2),fs*.9f,1-Seg(t,.46f,.5f));}
            // The X marks: glowing, then dripping.
            float glow=Seg(t,.36f,.46f)*(1-Seg(t,1.3f,1.7f));
            if(glow>0&&Through(a1,b1,fr,out var p1,out var q1)&&Through(a2,b2,fr,out var p2,out var q2))
            {
                float pulse=.75f+.25f*Mathf.Sin(t*14);GlowLine(p1,q1,6,U.A(fa,.5f*glow),12);GlowLine(p2,q2,6,U.A(fa,.5f*glow),12);Line(p1,q1,3.6f,U.A(tox,glow*pulse));Line(p2,q2,3.6f,U.A(tox,glow*pulse));Line(p1,q1,1.2f,new Color(1,1,1,.7f*glow));Line(p2,q2,1.2f,new Color(1,1,1,.7f*glow));
                for(int i=0;i<12;i++)
                {
                    float k=.12f+.76f*Rnd(170,i);var o=i%2==0?Vector2.Lerp(p1,q1,k):Vector2.Lerp(p2,q2,k);float td=.5f+.45f*Rnd(171,i),d=t-td;if(d<-.15f)continue;
                    if(d<0){Dot(o,fs*.17f*(1+d/.15f),U.A(tox,glow));continue;}
                    float y=o.y+.5f*900*d*d,len=Mathf.Min(y-o.y,fs*.6f);var tipP=new Vector2(o.x,y);float onCard=y<fr.yMax?1:0;
                    if(y<fr.yMax+fs*.2f){Line(o,new Vector2(o.x,Mathf.Min(y,fr.yMax)),2.4f,U.A(tox,.65f*glow));Blob(tipP,fs*.16f,fs*.25f,90,U.A(tox,glow));Dot(tipP+new Vector2(-1,-1),fs*.07f,new Color(1,1,1,.75f*glow));}
                    else{float hitT=Mathf.Sqrt(2*(fr.yMax+fs*.2f-o.y)/900),u=(d-hitT)/.35f;if(u<1){Oval(new Vector2(o.x,fr.yMax+fs*.2f),fs*(.15f+.45f*u),.35f,0,U.A(tox,.85f*(1-u)),true);Motes(new Vector2(o.x,fr.yMax+fs*.2f),d-hitT,3,175+i,-90,100,120,.3f,fs*.04f,tox,700,1);}}
                    if(onCard>0&&len>0)Line(o,new Vector2(o.x,o.y+len*.4f),2.6f,U.A(tox,.25f*glow));
                }
                for(int i=0;i<6;i++){float u=Mathf.Repeat(t*1.4f+Rnd(172,i),1);var q=fr.center+new Vector2(Sgn(173,i)*fr.width*.3f,fr.height*.2f-u*fr.height*.5f);Thin(q,fs*(.06f+.08f*u),U.A(tox,.85f*glow*(1-u)));}
                Soft(fr.center,fr.width*(.6f+.4f*Seg(t,.4f,1.3f)),U.A(fa,.24f*glow));Smoke(fr.center,t-.5f,5,176,fs*.7f,1,.12f,new Vector2(0,-30));
            }
        }

        // ---- DEV Greatsword: raised and turned point down in a glitch, plunged under the card: Earthsplitter's fissure
        // races out across the whole screen, the floor erupting; it is wrenched out and glitches home.
        private static void DevGreatsword(float t)
        {
            float floor=fr.yMax+6,sc=1.5f,half=fs*Big(sc)*.47f,hit=t-.5f;var x=fr.center.x;var dev=C.Violet();var top=new Vector2(x,fr.y-fs*1.7f);Launch(t);
            Vector2 p;float d=90;
            if(t<.32f){float u=M.OutCubic(Seg(t,0,.32f));p=Vector2.Lerp(fc,top,u);d=Mathf.Lerp(0,90,InOut(Seg(t,.1f,.32f)));}
            else if(t<.5f){float u=InCubic(Seg(t,.36f,.5f));p=new Vector2(x,Mathf.Lerp(top.y,floor-half,u));}
            else if(t<1.32f)p=new Vector2(x,floor-half);
            else if(t<1.5f)p=new Vector2(x,Mathf.Lerp(floor-half,top.y,M.OutCubic(Seg(t,1.32f,1.48f))));
            else p=fc;
            float on=t<1.5f?1:0;int fl=(int)(t*24);var jit=new Vector2(Sgn(180,fl),Sgn(181,fl))*fs*.06f*(t<.32f||t>1.36f?1:.3f);
            Arm(p+jit,sc,d,1,on*(t>1.36f&&Rnd(182,fl)<.35f?.3f:1),t>.36f&&t<.5f?new Vector2(0,2400):new Vector2(900,0));
            if(on>0&&(t<.36f||t>1.3f)){Glyph(p-jit*2+new Vector2(4,0),sc,d,1,C.Magenta(.35f));Glyph(p+jit*2-new Vector2(4,0),sc,d,1,C.Cyan(.35f));}
            for(int i=0;i<4;i++){float a=t*200+i*90;Hex(p+Dir(a)*fs*1.6f,fs*.18f,a,1.8f,U.A(dev,.8f*on*Seg(t,.05f,.2f)));}
            if(hit>=0)
            {
                var at=new Vector2(x,floor);if(hit<.07f)Flash(.32f*(1-hit/.07f),Color.white);if(hit<.14f){Star(at,fs*1.9f*(1-hit/.14f),0,10,U.A(fh,1));Dot(at,fs*4.5f*(1-hit/.14f),U.A(fa,.45f));}
                float fade=1-Seg(t,1.25f,1.6f);if(t>1.25f&&Rnd(183,fl)<.3f)fade*=.3f;float reach=U.W*1.5f*hit;
                for(int side=-1;side<=1;side+=2)
                {
                    var prev=at;for(int i=1;i<40;i++)
                    {
                        float dx=i*30;if(dx>reach+30||x+side*dx<-40||x+side*dx>U.W+40)break;var q=new Vector2(x+side*Mathf.Min(dx,reach),floor+Sgn(184+side,i)*5);
                        U.Fade(new Rect(Mathf.Min(prev.x,q.x),floor-fs*1.4f,Mathf.Abs(q.x-prev.x)+1,fs*1.4f),U.A(fa,.3f*fade),false);GlowLine(prev,q,5,U.A(fa,.6f*fade),9);Line(prev,q,3.6f,U.A(fh,fade));Line(prev,q,1.2f,new Color(1,1,1,fade));
                        float tv=hit-dx/(U.W*1.5f);if(i%2==0&&tv>0&&tv<.6f){for(int j=0;j<2;j++){var v=new Vector2(Sgn(185+j,i)*80,-(260+260*Rnd(186+j,i)));var c=q+v*tv+new Vector2(0,.5f*1500*tv*tv);float sz=fs*(.08f+.08f*Rnd(187,i));Box(c,sz,sz*.7f,tv*500,new Color(.22f,.24f,.32f,1-tv/.6f));}
                            Streak(q,q+new Vector2(0,-fs*(.8f+.9f*Rnd(188,i))*Bell(tv,0,.4f)),2.4f,U.A(fh,.85f*Bell(tv,0,.4f)));if(Rnd(189,i)>.5f)Box(q+new Vector2(0,-fs*1.6f*tv),fs*.12f,fs*.12f,0,U.A(dev,.85f*(1-tv/.6f)));}
                        prev=q;
                    }
                }
                if(hit<.4f)Ring(at,fs*(.4f+3f*hit/.4f),U.A(fa,.85f*(1-hit/.4f)));Sparks(at,hit,20,190,-90,160,820,.55f,fh);
            }
            Land(t,1.62f);
        }

        // ---- DEV Katana: the screen dims under a rising moon; a flurry of cuts through the card, a final cross, a storm
        // of petals spiralling out; the blade sheathes.
        private static void DevKatana(float t)
        {
            var dev=C.Violet();float dim=Seg(t,0,.3f)*(1-Seg(t,1.5f,1.9f));Flash(.32f*dim,new Color(.01f,.01f,.04f));
            var moon=Clamp(fc+new Vector2(fdir*fs*1.5f,-fs*2f),fs*1.5f);float mr=fs*1.1f*M.OutCubic(Seg(t,.05f,.4f));
            if(mr>1){Dot(moon,mr*4.5f,new Color(1,.86f,.9f,.12f*dim));Dot(moon,mr*2,new Color(1,.95f,.97f,.95f*dim));Dot(moon+new Vector2(-fdir*mr*.42f,-mr*.2f),mr*1.75f,new Color(.04f,.05f,.1f,.98f*dim));}
            Launch(t);
            for(int k=0;k<6;k++)
            {
                float tk=.32f+k*.09f,s=t-tk;if(s<0||s>.26f)continue;float deg=Rnd(200,k)*180,a=1-Seg(s,.05f,.26f);var c=fr.center+new Vector2(Sgn(201,k),Sgn(202,k))*fs*.3f;
                var d=Dir(deg)*fs*2.6f*M.OutCubic(Seg(s,0,.05f));Cut(c-d,c+d,4,U.A(fh,a));if(s<.05f){CardFlash(fr,.6f*(1-s/.05f),fh);Flash(.05f*(1-s/.05f),fh);}Sparks(c+d,s,8,203+k,deg,60,560,.35f,fh,400);
            }
            float x=t-.9f;if(x>=0&&x<.45f){float a=1-Seg(x,.12f,.45f);var c=fr.center;float r=fs*2.6f;Cut(c+new Vector2(-r,-r*1.1f),c+new Vector2(r,r*1.1f),6,U.A(fh,a));Cut(c+new Vector2(r,-r*1.1f),c+new Vector2(-r,r*1.1f),6,U.A(fh,a));if(x<.07f)Flash(.24f*(1-x/.07f),fh);}
            float b=t-1;if(b>=0)
            {
                for(int i=0;i<44;i++){float u=M.OutCubic(Seg(b,0,.75f)),a0=Rnd(210,i)*360+u*420*fdir,rr=fs*(.3f+(3f+1.6f*Rnd(211,i))*u);var q=fr.center+Dir(a0)*rr+new Vector2(0,90*b*b);Petal(q,fs*(.18f+.1f*Rnd(212,i)),a0+b*300,Mathf.Cos(b*8+i),1-Seg(b,.55f,.9f));}
                for(int i=0;i<6;i++){float u=M.OutCubic(Seg(b,0,.6f)),a0=i*60+u*200;Hex(fr.center+Dir(a0)*fs*(.4f+2.4f*u),fs*.15f,a0,1.8f,U.A(dev,.85f*(1-Seg(b,.4f,.7f))));}
            }
            if(t>1.36f&&t<1.58f){float u=InOut(Seg(t,1.36f,1.54f));Arm(Vector2.Lerp(fc+new Vector2(-fdir*fs*1.2f,-fs*.4f),fc,u),Mathf.Lerp(1.3f,1,u),Mathf.Lerp(-90*fdir,0,u),1,1-Seg(t,1.52f,1.56f),default);}
            float k2=t-1.55f;if(k2>=0&&k2<.3f){Star(fc,fs*.8f*(1-k2/.3f),45,4,new Color(1,1,1,1-k2/.3f));Thin(fc,fs*(.5f+1.4f*k2/.3f),U.A(fh,.9f*(1-k2/.3f)));}
        }

        // ---- Bombadier: the TNT hops out, its fuse lit and fizzing, it swells and flashes with each tick, then BOOM:
        // a flash, rings, fire, smoke and a storm of confetti over the grid.
        private static void Bombadier(float t)
        {
            var b=fc+new Vector2(0,-fs*1.5f);float boom=t-1;Launch(t);
            float beat=0;float[] ticks=bombTicks;for(int i=0;i<ticks.Length;i++){float s=t-ticks[i];if(s>=0&&s<.12f)beat=Mathf.Max(beat,1-s/.12f);}
            if(boom<0)
            {
                float u=M.OutCubic(Seg(t,0,.24f)),squash=Mathf.Exp(-t*6)*Mathf.Sin(t*22);var p=Fly(fc,b,u,fs*.3f)+Shake(t,fs*.04f*Seg(t,.5f,1));float s=(1+.3f*u)*(1+.1f*beat+.14f*Seg(t,.7f,1));
                U.Push(p,Vector2.zero,1+.12f*squash,0,1-.12f*squash);Arm(p,s,Mathf.Sin(t*18)*6*Seg(t,.5f,1),1,1,default,1+beat);U.Pop();if(beat>0)Glyph(p,s,0,1,new Color(1,.25f,.2f,.5f*beat));
                // The fuse: from the cap outward, burning down to it.
                var cap=On(p,s,0,1,.33f,-.33f);var end=cap+new Vector2(fs*.5f,-fs*.45f);float burn=Seg(t,.3f,1);
                Vector2 F(float k)=>Vector2.Lerp(cap,end,k)+new Vector2(-fs*.06f,fs*.06f)*Mathf.Sin(k*Mathf.PI*2);
                var prev=cap;for(int i=1;i<=8;i++){float k=i/8f*(1-burn);var q=F(k);Line(prev,q,3.2f,new Color(.75f,.62f,.45f,1));prev=q;}
                if(t>.28f){var sp=F(1-burn);float fl=.8f+.4f*Rnd(220,(int)(t*30));Star(sp,fs*.5f*fl,t*500,6,new Color(1,.85f,.4f,1));Dot(sp,fs*1.1f,new Color(1,.6f,.2f,.45f));
                    for(int j=0;j<14;j++){float bt=.3f+j*.05f;if(t<bt)break;Sparks(F(1-Seg(bt,.3f,1)),t-bt,4,221+j,-90,240,340,.3f,new Color(1,.85f,.4f),500,1.8f);}
                    Smoke(sp,Mathf.Repeat(t,.45f),3,222,fs*.28f,.45f,.22f,new Vector2(0,-40));}
            }
            else
            {
                if(boom<.14f)Flash(.4f*(1-boom/.14f),new Color(1,.9f,.6f));float u=M.OutCubic(Seg(boom,0,.5f));
                if(boom<.32f){Dot(b,fs*(2.2f+7*boom/.32f),new Color(1,.6f,.2f,.6f*(1-boom/.32f)));Dot(b,fs*3.2f*(1-boom/.32f),Color.white);}
                Ring(b,fs*(.6f+4.6f*u),new Color(1,.78f,.35f,.9f*(1-u)));Thin(b,fs*(.7f+5.8f*u),new Color(1,1,1,.75f*(1-u)));
                for(int i=0;i<8;i++){float a=i*45+Rnd(223,i)*30;var q=b+Dir(a)*fs*1.9f*u;Dot(q,fs*(.9f+1.2f*u)*(1-.5f*u),new Color(1,.45f+.3f*Rnd(224,i),.15f,.5f*(1-u)));}
                Sparks(b,boom,30,225,-90,360,900,.65f,new Color(1,.85f,.45f));Smoke(b,boom-.08f,10,226,fs*1.3f,1,.38f,new Vector2(0,-30));Confetti(b,boom,90,227,980,.9f,fs*.15f);
            }
        }
        private static readonly float[] bombTicks={.5f,.7f,.84f,.94f};

        // ---- Top Dog: BOING out of the card, hop-hop-backflip along the cards, back up with the Big Mallet: BONK! The card
        // squashes flat and springs back, dizzy stars circle, a squiggle of mustard.
        private static readonly Vector2[] hops=new Vector2[4];
        private static void DogPose(float t,out Vector2 p,out float sx,out float sy,out float deg)
        {
            float ground=fr.y-fs*.5f;hops[0]=new Vector2(fc.x,ground);hops[1]=Clamp(new Vector2(fc.x+fdir*fs*2.6f,ground),fs);hops[2]=Clamp(new Vector2(fc.x+fdir*fs*4.9f,ground),fs);hops[3]=new Vector2(fc.x,fr.y-fs*2.1f);
            sx=sy=1;deg=0;
            if(t<.3f){float u=M.OutCubic(Seg(t,0,.3f));p=Vector2.Lerp(fc,hops[0],u);float st=Bell(t,0,.3f);sy=1+.55f*st;sx=1-.3f*st;return;}
            int h=t<.56f?0:t<.84f?1:2;float h0=h==0?.3f:h==1?.56f:.84f,h1=h==0?.56f:h==1?.84f:1.12f,uu=Seg(t,h0,h1);
            var a=hops[h];var bb=hops[h+1];p=Vector2.Lerp(a,bb,h==2?InOut(uu):uu)+new Vector2(0,-fs*(h==2?1.2f:1.6f)*4*uu*(1-uu));
            float land=t-h1;if(h<2){float stretch=Bell(uu,0,.3f)*.25f;sy=1+stretch;sx=1-stretch*.6f;}
            if(h==1)deg=-fdir*360*InOut(uu);
            for(int i=0;i<2;i++){float lt=t-(i==0?.56f:.84f);if(lt>=0&&lt<.16f){float k=Mathf.Sin(lt/.16f*Mathf.PI);sy=1-.38f*k;sx=1+.3f*k;}}
            if(t>1.12f){float u=Seg(t,1.12f,1.36f);p=Vector2.Lerp(hops[3],fc,InCubic(u));sx=1-.2f*u;sy=1+.3f*u;}
        }
        private static void Mallet(Vector2 grip,float deg,float a)
        {
            if(a<=.003f)return;var d=Dir(deg);var head=grip+d*fs*1.7f;
            Line(grip,head,fs*.12f,new Color(.55f,.35f,.18f,a));Line(grip,head,fs*.04f,new Color(.8f,.6f,.35f,a));
            U.Push(head,Vector2.zero,1,deg+90);var hr=new Rect(head.x-fs*.58f,head.y-fs*.36f,fs*1.16f,fs*.72f);U.Round(hr,new Color(.88f,.16f,.16f,a),8,new Color(.3f,.05f,.05f,a),2);
            U.Fill(new Rect(hr.x+fs*.1f,hr.y,fs*.14f,hr.height),new Color(1,1,1,.85f*a));U.Fill(new Rect(hr.xMax-fs*.24f,hr.y,fs*.14f,hr.height),new Color(1,1,1,.85f*a));U.Fill(new Rect(hr.x+fs*.1f,hr.y+2,hr.width-fs*.2f,fs*.08f),new Color(1,1,1,.3f*a));U.Pop();
        }
        private static void TopDog(float t)
        {
            var gold=new Color(1,.86f,.2f);
            if(t<.36f){float st=Bell(t,.02f,.36f);var bottom=fc+new Vector2(0,fs*.2f);var dog=Vector2.Lerp(fc,new Vector2(fc.x,fr.y-fs*.25f),M.OutCubic(Seg(t,0,.3f)));var prev=bottom;
                for(int i=1;i<=8;i++){var q=Vector2.Lerp(bottom,dog+new Vector2(0,fs*.3f),i/8f)+new Vector2((i%2==0?-1:1)*fs*.16f*st,0);Line(prev,q,2.4f,new Color(.85f,.85f,.9f,st));prev=q;}}
            for(int i=0;i<2;i++){float lt=t-(i==0?.56f:.84f);if(lt>=0&&lt<.45f){var at=hops[i+1]+new Vector2(0,fs*.7f);Soft(at,fs*(.4f+.9f*lt/.45f),new Color(.9f,.9f,.95f,.4f*(1-lt/.45f)));Oval(at,fs*(.3f+1.2f*lt/.45f),.3f,0,U.A(gold,.8f*(1-lt/.45f)),true);}}
            if(t<1.37f){DogPose(t,out var p,out float sx,out float sy,out float deg);DogPose(t-.016f,out var pp,out _,out _,out _);U.Push(p,Vector2.zero,1,deg,1);U.Push(p,Vector2.zero,sx,0,sy);Arm(p,Mathf.Lerp(1,1.3f,Seg(t,0,.2f)*(1-Seg(t,1.2f,1.36f))),0,1,1,(p-pp)/.016f);U.Pop();U.Pop();}
            // The mallet: raised from the dog at the top of its leap, swung down onto the card.
            if(t>1.06f&&t<1.62f){float sw=InCubic(Seg(t,1.14f,1.35f)),back=t>1.35f?Seg(t,1.35f,1.6f):0;var grip=Vector2.Lerp(hops[3]+new Vector2(-fdir*fs*.3f,0),new Vector2(fc.x-fdir*fs*1.5f,fr.y-fs*.7f),sw);
                Mallet(grip,Mathf.Lerp(-90-fdir*60,fdir>0?-8:188,sw)-(back>0?fdir*20*back:0),Seg(t,1.06f,1.12f)*(1-Seg(t,1.5f,1.62f)));}
            float bonk=t-1.35f;if(bonk>=0)
            {
                var wp=fc+new Vector2(fdir*fs*.75f,-fs*1.9f);float pop=Spring(bonk*1.4f,9),a=1-Seg(bonk,.45f,.6f);if(bonk<.06f)Flash(.12f*(1-bonk/.06f),Color.white);
                Star(fc+new Vector2(0,-fs*.8f),fs*2.2f*Mathf.Min(1,bonk*8)*a,15,12,U.A(gold,.95f*a));Dot(fc+new Vector2(0,-fs*.8f),fs*2f*a,new Color(1,1,1,.55f*a));
                Word(wp,"BONK!",Mathf.Clamp(fs*.55f,22,44),pop,-fdir*12,gold,a);
                for(int i=0;i<4;i++){float an=bonk*360+i*90;var q=new Vector2(fc.x,fr.y-fs*.15f)+new Vector2(Mathf.Cos(an*Mathf.Deg2Rad)*fs*1f,Mathf.Sin(an*Mathf.Deg2Rad)*fs*.3f);Star(q,fs*.3f,an,4,U.A(gold,(1-Seg(bonk,.4f,.6f))*Seg(bonk,.05f,.12f)));}
                float m=Seg(bonk,.08f,.24f),ma=1-Seg(bonk,.4f,.6f);var prev=new Vector2(fr.x+fr.width*.12f,fr.y+fr.height*.72f);
                for(int i=1;i<=20;i++){float k=i/20f;if(k>m)break;var q=new Vector2(fr.x+fr.width*(.12f+.76f*k),fr.y+fr.height*.72f+Mathf.Sin(k*Mathf.PI*5)*fs*.12f);Line(prev,q,fs*.13f,new Color(1,.82f,.1f,ma));Line(prev,q,fs*.04f,new Color(1,1,.7f,ma));prev=q;}
            }
        }

        // ---- Siren: the card darkens and its eyes burn, three claws rake it, hellfire climbs it in violet and white.
        private static void Siren(float t)
        {
            var hell=U.Mix(fa,C.Magenta(),.4f);float dark=Bell(t,0,1.7f);U.Round(fr,new Color(0,0,0,.35f*dark),R);
            float eyes=Seg(t,.05f,.25f)*(1-Seg(t,1.4f,1.75f));for(int i=-1;i<=1;i+=2){var e=fc+new Vector2(i*fs*.2f,-fs*.14f);Dot(e,fs*.9f,U.A(hell,.5f*eyes));Dot(e,fs*.22f,new Color(1,.9f,1,eyes));Ray(e,i>0?-10:190,fs*.9f*eyes,3,U.A(hell,eyes));Ray(e,i>0?-30:210,fs*.5f*eyes,2,U.A(hell,.7f*eyes));}
            var dir=Dir(125);var n=new Vector2(-dir.y,dir.x);
            for(int i=-1;i<=1;i++)
            {
                var a=fr.center+n*i*fs*.36f-dir*fr.height*.95f;var b=fr.center+n*i*fs*.36f+dir*fr.height*.95f;float t0=.34f+(i+1)*.03f;
                Slash(a,b,.04f,t0,t0+.1f,t,fs*.26f,0,false,240+i*3);
                float mark=Seg(t,t0+.08f,t0+.12f)*(1-Seg(t,1.2f,1.5f));if(mark>0&&Through(a,b,fr,out var p,out var q)){Line(p,q,6,new Color(0,0,0,.7f*mark));Line(p+n*3.4f,q+n*3.4f,1.6f,U.A(hell,.95f*mark));Line(p-n*3.4f,q-n*3.4f,1.6f,U.A(hell,.95f*mark));}
            }
            float fire=Bell(t,.55f,1.55f);Flames(new Rect(fr.x-fs*.3f,fr.y,fr.width+fs*.6f,fr.height*.98f),t,fire,18,new Color(1,.9f,1),hell,fs*2.2f,250);Soft(fr.center,fr.width*.9f,U.A(hell,.2f*fire));
            Oval(new Vector2(fr.center.x,fr.yMax),fr.width*(.6f+.2f*Seg(t,.55f,1.3f)),.25f,0,U.A(hell,.8f*fire));Motes(new Vector2(fr.center.x,fr.yMax-fs*.3f),t-.6f,26,251,-90,120,260,1,fs*.08f,hell,-120,1.2f);
        }

        // ---- Emperor Style: the crown rises and lightning strikes it, a left and a right electric punch rock the card, an
        // electric wind hook spirals up round it, the crown comes home.
        private static void Punch(Vector2 at,float side,float s,float t)
        {
            var arc=new Color(.75f,.9f,1);
            if(s<0&&s>-.08f)for(int i=0;i<7;i++){float y=at.y+Sgn(260,i)*fs*.5f,u=1+s/.08f;var a=new Vector2(at.x-side*fs*(2.2f+.9f*Rnd(261,i)),y);Streak(Vector2.Lerp(a,at,u*.4f),Vector2.Lerp(a,at,u),2.4f,U.A(fh,.85f*u));}
            if(s<0||s>.4f)return;float k=1-s/.4f;Star(at,fs*1.4f*k,side>0?180:0,10,U.A(fh,k));Dot(at,fs*3.4f*k,U.A(fa,.4f*k));Ring(at,fs*(.4f+2f*(1-k)),U.A(fa,.9f*k));if(s<.05f)Flash(.07f*(1-s/.05f),fh);
            for(int i=0;i<4;i++)Bolt(at,at+Dir((side>0?180:0)+Sgn(262,i)*70)*fs*(.9f+.6f*Rnd(263,i)),(int)(s*30)*5+i,fs*.16f,2,U.A(arc,k),5);Sparks(at,s,14,264,side>0?180:0,140,680,.45f,arc);
        }
        private static void Emperor(float t)
        {
            var arc=new Color(.75f,.9f,1);var crown=fc+new Vector2(0,-fs*1.4f);Launch(t);int fl=(int)(t*30);
            Vector2 p;float s;
            if(t<.25f){float u=M.OutCubic(t/.25f);p=Vector2.Lerp(fc,crown,u);s=1+.2f*u;}else if(t<1.32f){p=crown+new Vector2(0,Mathf.Sin(t*6)*2);s=1.2f;}else{float u=InOut(Seg(t,1.32f,1.64f));p=Vector2.Lerp(crown,fc,u);s=Mathf.Lerp(1.2f,1,u);}
            float strike=t-.25f;if(strike>=0&&strike<.18f){Bolt(new Vector2(p.x+Sgn(265,fl)*fs*.3f,-10),p+new Vector2(0,-fs*.6f),fl*3,fs*.6f,5.5f,U.A(arc,1-strike/.18f),12);Flash(.18f*(1-strike/.18f),arc);Dot(p,fs*3*(1-strike/.18f),U.A(arc,.4f));}
            float charge=Seg(t,.25f,.3f)*(1-Seg(t,.6f,.9f));for(int i=0;i<3;i++)if(charge>0){var a=p+Dir(Rnd(fl+i,4)*360)*fs*.85f;Bolt(a,p+Dir(Rnd(fl+i,5)*360)*fs*.9f,fl*9+i,fs*.16f,2,U.A(arc,charge),4);}
            if(t<1.66f)Arm(p,s,0,1,1-Seg(t,1.6f,1.66f),default,1+charge);
            Punch(new Vector2(fr.x-2,fr.center.y),-1,t-.55f,t);Punch(new Vector2(fr.xMax+2,fr.center.y),1,t-.75f,t);
            // The wind hook: a lightning helix climbing the card.
            float w=Seg(t,.95f,1.32f);if(w>0&&w<1)
            {
                Vector2 H(float u)=>new Vector2(fr.center.x+Mathf.Cos(u*Mathf.PI*4)*fr.width*.8f*(1-.25f*u),Mathf.Lerp(fr.yMax+fs*.3f,fr.y-fs*1.3f,u)+Mathf.Sin(u*Mathf.PI*4)*fs*.24f);
                float head=M.OutCubic(w),tail=Mathf.Max(0,head-.4f);var prev=H(tail);for(int i=1;i<=14;i++){var q=H(Mathf.Lerp(tail,head,i/14f));Bolt(prev,q,fl*11+i,fs*.08f,3.2f,U.A(arc,(i/14f)*(1-Seg(w,.8f,1))),2);prev=q;}
                Motes(new Vector2(fr.center.x,fr.yMax),t-.95f,18,266,-90,70,460,.5f,fs*.07f,arc,-200,1);
            }
            Land(t,1.64f);
        }

        // ---- Ansatsuken: the fist draws back and gathers a ball of blue energy, thrusts it across the screen in a rippling
        // wave to burst far off, then rises in a spinning dragon uppercut and drops home.
        private static void Ansatsuken(float t)
        {
            var ki=new Color(.6f,.9f,1);var back=Clamp(fc+new Vector2(-fdir*fs*.6f,-fs*.9f),fs*1.4f);var ball0=back+new Vector2(fdir*fs*1.3f,0);var tgt=Snap(new Vector2(fc.x+fdir*U.W*.5f,ball0.y),270);
            float speed=1800,ta=.5f+Mathf.Clamp(Mathf.Abs(tgt.x-ball0.x)/speed,.15f,.4f);Launch(t);
            // The fist: back, the thrust, the rising uppercut, down home.
            Vector2 p;float s=1.2f,d=0;var thrust=back+new Vector2(fdir*fs*.8f,0);
            if(t<.3f){float u=M.OutCubic(t/.3f);p=Vector2.Lerp(fc,back,u);s=1+.2f*u;}
            else if(t<.5f)p=back+Shake(t,1.5f);
            else if(t<.9f)p=Vector2.Lerp(back,thrust,M.OutCubic(Seg(t,.5f,.56f)));
            else if(t<1.35f){float u=M.OutCubic(Seg(t,.9f,1.3f));p=thrust+new Vector2(fdir*fs*.3f*u,-fs*2f*u);d=-fdir*360*u;}
            else{float u=InCubic(Seg(t,1.35f,1.66f));p=Vector2.Lerp(thrust+new Vector2(fdir*fs*.3f,-fs*2f),fc,u);s=Mathf.Lerp(1.2f,1,u);}
            if(t>.9f&&t<1.35f)for(int i=1;i<=8;i++){float tt=t-i*.03f;if(tt<.9f)break;float u=M.OutCubic(Seg(tt,.9f,1.3f));var q=thrust+new Vector2(fdir*fs*.3f*u,-fs*2f*u)+new Vector2(0,fs*.45f);float k=1-i/9f;Blob(q,fs*.8f*k,fs*1.2f*k,0,U.A(ki,.55f*k));Dot(q,fs*.3f*k,new Color(1,1,1,.65f*k));}
            if(t<1.68f){Arm(p,s,d,fdir,1-Seg(t,1.62f,1.68f),t>.5f&&t<.56f?new Vector2(fdir*1800,0):default);}
            // The ball.
            if(t>.05f&&t<.5f)for(int i=0;i<12;i++){float u=Mathf.Repeat(Seg(t,.05f,.5f)*2+i/12f,1);Dot(ball0+Dir(i*30+t*90)*fs*1.9f*(1-u),fs*.14f,U.A(ki,.9f*u));}
            float r=fs*(.25f+.34f*Seg(t,.2f,.5f))*(1+.1f*Mathf.Sin(t*40));Vector2 bp=t<.5f?ball0:new Vector2(Mathf.Lerp(ball0.x,tgt.x,Seg(t,.5f,ta)),ball0.y+Mathf.Sin(t*30)*2);
            if(t>.2f&&t<ta)
            {
                if(t>.5f){r*=1.25f;for(int i=1;i<=6;i++){float tt=t-i*.035f;if(tt<.5f)break;var q=new Vector2(Mathf.Lerp(ball0.x,tgt.x,Seg(tt,.5f,ta)),ball0.y);Oval(q,r*(1+.2f*i),.45f,90,U.A(ki,.5f*(1-i/7f)),true);}}
                Dot(bp,r*4,U.A(ki,.3f));Dot(bp,r*2.2f,U.A(fa,.85f));Dot(bp,r*1.2f,new Color(.85f,.97f,1,1));Oval(bp,r*1.3f,.4f,t*400,U.A(Color.white,.7f),true);Oval(bp,r*1.5f,.35f,-t*300+60,U.A(ki,.6f),true);
                for(int i=0;i<2;i++)Bolt(bp,bp+Dir(Rnd((int)(t*30),i)*360)*r*1.8f,(int)(t*30)*3+i,r*.4f,1,U.A(Color.white,.8f),3);
            }
            float hit=t-ta;if(hit>=0){if(hit<.45f){Ring(tgt,fs*(.5f+3f*M.OutCubic(hit/.45f)),U.A(ki,.9f*(1-hit/.45f)));Star(tgt,fs*1.7f*(1-hit/.45f),0,10,U.A(fh,1-hit/.45f));Dot(tgt,fs*4*(1-hit/.45f),U.A(ki,.4f));}Motes(tgt,hit,26,271,-90,360,620,.65f,fs*.08f,ki,200);if(hit<.08f)Flash(.12f*(1-hit/.08f),ki);}
            Land(t,1.66f);
        }

        // ---- L33T H4x: the card is hacked (its slices tear sideways, the icon splits into colour), binary pours round it,
        // three bit bolts launch, pause on their marks and strike; ACCESS GRANTED types out.
        private static readonly string[] bits={"0","1"};
        private static void Leet(float t)
        {
            var g=U.Mix(fa,Color.white,.15f);int fl=(int)(t*24);float glitch=Bell(t,0,.42f)+.8f*Bell(t,1.38f,1.62f);
            if(glitch>.02f){var o=new Vector2(Sgn(280,fl),0)*fs*.08f*glitch;Glyph(fc+o,1,0,1,C.Magenta(.5f*glitch));Glyph(fc-o,1,0,1,C.Cyan(.5f*glitch));
                for(int i=0;i<3;i++){float y=fr.y+fr.height*Rnd(281+i,fl);U.Fill(new Rect(fr.x-fs*.2f,y,fr.width+fs*.4f,1.5f),U.A(g,.7f*glitch));}}
            // The rain.
            float rain=Bell(t,.12f,1.4f);if(rain>.01f)for(int c=0;c<14;c++)
            {
                float x=fr.x-fs*.7f+(fr.width+fs*1.4f)*(c+.5f)/14,sp=240+180*Rnd(282,c),head=fr.y-fs*.8f+Mathf.Repeat((t-.12f)*sp+Rnd(283,c)*fs*3,fr.height+fs*2.2f);
                for(int k=0;k<5;k++){float y=head-k*16;if(y<fr.y-fs*.9f)break;float a=rain*(k==0?1:.75f-.15f*k);U.Text(new Rect(x-9,y-9,18,18),bits[Rnd(284+c,k+fl)>.5f?1:0],14,k==0?new Color(.85f,1,.9f,a):U.A(g,a),TextAnchor.MiddleCenter,bold:k==0,mono:true);}
            }
            // The bolts: out, a pause on their marks (the lock boxes), then the strike.
            for(int k=0;k<3;k++)
            {
                var hover=fc+Dir(-90+(k-1)*55)*fs*1.9f;Vector2 tgt=Target(k,hover,false,out var tp,out _)?tp:Clamp(hover+Dir(-90+(k-1)*55)*fs*3,40);
                float o=M.OutCubic(Seg(t,.5f,.62f)),dash=InCubic(Seg(t,.78f,.86f));if(t<.5f||t>1.3f)continue;var p=Vector2.Lerp(Vector2.Lerp(fc,hover,o),tgt,dash);
                if(t<.88f){float bz=fs*.17f;Dot(p,bz*5,U.A(fa,.45f));U.Fill(Square(p,bz),new Color(.85f,1,.9f,1));if(t>.62f&&t<.78f&&Mathf.Repeat(t*16,1)<.6f){float bs=fs*.45f;Hex(p,bs,0,1.8f,U.A(g,.95f));}
                    if(dash>0)Beam(Vector2.Lerp(hover,tgt,Mathf.Max(0,dash-.5f)),p,3,U.A(g,.9f));}
                float hit=t-.86f;if(hit>=0&&hit<.45f){for(int i=0;i<14;i++){var v=Dir(Rnd(285+k,i)*360)*(150+320*Rnd(286+k,i));var q=tgt+v*hit;float a=1-hit/.45f;U.Fill(Square(q,fs*.09f*(1+Rnd(287,i))),U.A(i%3==0?Color.white:g,a));}if(hit<.25f)Ring(tgt,fs*(.3f+1.5f*hit/.25f),U.A(g,.85f*(1-hit/.25f)));}
            }
            // ACCESS GRANTED.
            float typed=Seg(t,1,1.3f),ta=Seg(t,1,1.04f)*(1-Seg(t,1.5f,1.72f));if(ta>0)
            {
                const string text="ACCESS GRANTED";float w=U.Measure(text,15,true,true,.14f).x;var tr=new Rect(fc.x-w*.5f,fr.y-fs*.8f,w,22);
                U.Round(new Rect(tr.x-10,tr.y-5,w+20,32),new Color(.01f,.05f,.02f,.8f*ta),4,U.A(g,.6f*ta));
                U.BeginBand(new Rect(tr.x,tr.y-2,w*typed,26));U.Text(tr,text,15,U.A(g,ta),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f,glow:U.A(g,.4f*ta));U.EndBand();
                if(Mathf.Repeat(t*4,1)<.6f)U.Fill(new Rect(tr.x+w*typed+2,tr.y+2,9,18),U.A(g,ta));
            }
        }

        // ---- Cyber Lord: hardlight hexes ripple over the card, the fist data-dashes out and back in afterimages, a firewall
        // of hexes rises up the card and bursts into packets; the fist comes home.
        private static void CyberLord(float t)
        {
            var hard=U.Mix(fa,Color.white,.2f);Launch(t,.18f);
            // The honeycomb over the card.
            float comb=Seg(t,0,.06f)*(1-Seg(t,.3f,.5f));float hx=fs*.3f;
            for(int r=0;r<5&&comb>0;r++)for(int c=0;c<4;c++){var hp=new Vector2(fr.x+fr.width*(c+.5f+(r%2)*.5f)/4.4f,fr.y+fr.height*(r+.5f)/5);float k=comb*Bell(t-r*.03f,0,.4f);Hex(hp,hx,30,1.8f,U.A(hard,.8f*k));Dot(hp,hx*1.4f,U.A(fa,.12f*k));}
            Vector2 d1=Clamp(fc+new Vector2(fdir*fs*3f,-fs*.35f),fs*1.4f),d2=Clamp(fc+new Vector2(-fdir*fs*2.3f,-fs*.8f),fs*1.4f),d3=fc+new Vector2(0,-fs*1.7f);
            Vector2 Pose(float tt){if(tt<.2f)return fc;if(tt<.32f)return Vector2.Lerp(fc,d1,InCubic(Seg(tt,.2f,.32f)));if(tt<.45f)return d1;if(tt<.57f)return Vector2.Lerp(d1,d2,InCubic(Seg(tt,.45f,.57f)));if(tt<.64f)return d2;if(tt<.72f)return Vector2.Lerp(d2,d3,InOut(Seg(tt,.64f,.72f)));if(tt<1.35f)return d3+new Vector2(0,Mathf.Sin(tt*6)*2);return Vector2.Lerp(d3,fc,InOut(Seg(tt,1.35f,1.48f)));}
            if(t>.18f&&t<1.5f)
            {
                for(int g=6;g>=1;g--){float tg=t-g*.018f;Arm(Pose(tg),1.25f,0,1,.12f*(7-g)*(Vector2.Distance(Pose(tg),Pose(t))>2?1:0),default,0);}
                var p=Pose(t);float sc=t<.24f?Mathf.Lerp(1,1.25f,Seg(t,.18f,.24f)):t>1.35f?Mathf.Lerp(1.25f,1,Seg(t,1.35f,1.48f)):1.25f;Arm(p,sc,0,1,1-Seg(t,1.44f,1.5f),(p-Pose(t-.016f))/.016f);
                for(int k=0;k<3;k++){float s=t-(k==0?.32f:k==1?.57f:.72f);var at=k==0?d1:k==1?d2:d3;if(s>=0&&s<.3f){Ring(at,fs*(.4f+1.3f*s/.3f),U.A(hard,.85f*(1-s/.3f)));Star(at,fs*.8f*(1-s/.3f),0,6,U.A(hard,1-s/.3f));for(int i=0;i<4;i++)Hex(at+Dir(i*90+s*300)*fs*.9f*(.5f+s/.3f),fs*.14f,s*200,1.8f,U.A(hard,1-s/.3f));}}
                if(t>.2f&&t<.72f){var a=t<.45f?fc:t<.64f?d1:d2;var b=Pose(t);Streak(a,b,4,U.A(hard,.8f));}
            }
            // The firewall and its packets.
            float wall=Seg(t,.7f,.98f),burst=t-1.05f;int rows=6,cols=5;float cw=(fr.width+fs*1.2f)/cols;
            for(int r=0;r<rows;r++)for(int c=0;c<cols;c++)
            {
                var hp=new Vector2(fr.x-fs*.6f+cw*(c+.5f)+(r%2==1?cw*.25f:0),fr.yMax-r*cw*.86f-cw*.4f);
                float on=Seg(wall,r/(float)rows,(r+1f)/rows);if(on<=0)continue;
                if(burst<0){float pop=Spring(on*1.2f,8);Hex(hp,cw*.48f*Mathf.Min(1.1f,pop),30,2.4f,U.A(hard,.95f*on));Dot(hp,cw*.8f,U.A(fa,.22f*on));}
                else{var v=(hp-fr.center).normalized*(480+380*Rnd(290,r*cols+c));var q=hp+v*burst;float a=1-Seg(burst,0,.5f);if(a<=0)continue;U.Fill(Square(q,cw*.22f),U.A(hard,a));Streak(q-v*.08f,q,3,U.A(fa,.85f*a));}
            }
            if(wall>0&&burst<0){float y=Mathf.Lerp(fr.yMax,fr.yMax-rows*cw*.86f,wall);Beam(new Vector2(fr.x-fs*.7f,y),new Vector2(fr.xMax+fs*.7f,y),2.4f,U.A(hard,.95f));}
            if(burst>=0&&burst<.12f){CardFlash(fr,1-burst/.12f,hard);Flash(.08f*(1-burst/.12f),hard);}
            Land(t,1.48f);
        }

        // ---- Generic (a weapon added later, until it has its own): the icon flares and rings out a burst of light.
        private static void Generic(float t)
        {
            float a=Bell(t,0,Plan.Length(Plan.Move.Generic));Dot(fc,fs*3.4f,U.A(fa,.35f*a));Ring(fc,fs*(.6f+2.4f*Seg(t,0,.6f)),U.A(fh,.85f*(1-Seg(t,0,.6f))));
            Motes(fc,t,18,300,-90,360,260,1,fs*.06f,fh,-60,1.5f);Star(fc,fs*.8f*Bell(t,0,.3f),t*60,8,U.A(fh,1));
        }

        // ---- the lock-in finisher (on the locked card, ~1 s): a light pillar, a flash and two rings on the icon, and the
        // weapon's own signature in small.
        private static void Finisher(Plan.Move m,float t)
        {
            float u=Seg(t,0,.5f),fade=1-Seg(t,.55f,Plan.Finish);
            Pillar(fc,fs*.9f*(1+.3f*u),(1-Seg(t,.05f,.7f))*Seg(t,0,.04f));Dot(fc,fs*2.2f*(1-u),new Color(1,1,1,.55f*(1-u)));
            Ring(fc,fs*(.5f+1.4f*M.OutCubic(u)),U.A(fa,.85f*(1-u)));Thin(fc,fs*(.6f+1.9f*M.OutCubic(Seg(t,.04f,.45f))),new Color(1,1,1,.7f*(1-Seg(t,.04f,.45f))));
            Rect r=fr;var c=fc;float L=fs;
            switch(m)
            {
                case Plan.Move.Pistol:case Plan.Move.Rifle:case Plan.Move.MachineGun:
                    for(int i=0;i<6;i++)Casing(c+new Vector2(Sgn(400,i)*L*.3f,0),t,i,i%2==0?1:-1,r.yMax-L*.2f);for(int i=0;i<3;i++){float d=-90+(i-1)*40;Beam(c+Dir(d)*L*(.6f+1.6f*u),c+Dir(d)*L*(.2f+1.6f*u),1.4f,U.A(fh,1-u));}break;
                case Plan.Move.Fire:Motes(c,t,26,401,-90,300,420,.9f,L*.05f,new Color(1,.6f,.2f),-80,1.6f);Oval(new Vector2(c.x,c.y+L*.6f),L*(.5f+1.2f*u),.3f,0,new Color(1,.5f,.15f,.8f*(1-u)));break;
                case Plan.Move.Water:Motes(c,t,18,402,-90,150,460,.7f,L*.06f,U.Mix(fa,Color.white,.4f),1200,1);for(int i=0;i<2;i++){float k=Seg(t,i*.12f,.5f+i*.12f);if(k>0&&k<1)Oval(new Vector2(c.x,c.y+L*.5f),L*(.4f+1.3f*k),.32f,0,U.A(fh,.7f*(1-k)),true);}break;
                case Plan.Move.Lightning:for(int i=0;i<5;i++)if(t<.4f)Bolt(c,Edge(r,i/5f+.05f),(int)(t*30)*5+i,L*.2f,1.8f,U.A(fh,1-t/.4f),7);break;
                case Plan.Move.Darkness:{float k=Seg(t,0,.25f);Dot(c,L*1.3f*Bell(t,0,.3f),new Color(0,0,.02f,.9f));Ring(c,L*(2.4f-1.8f*k),U.A(fa,.6f*Bell(t,0,.3f)));Sparks(c,t-.25f,14,403,-90,360,520,.5f,fh,200);break;}
                case Plan.Move.Sword:case Plan.Move.Greatsword:Slash(new Vector2(r.x-L*.3f,r.y+r.height*.2f),new Vector2(r.xMax+L*.3f,c.y+L*.6f),.14f,0,.12f,t,L*.22f,0,false,404);
                    if(m==Plan.Move.Greatsword){Cracks(new Vector2(c.x,c.y+L*.55f),M.OutCubic(Seg(t,.02f,.14f)),fade,405,7,r.width*.5f);}break;
                case Plan.Move.Daggers:case Plan.Move.Fangs:Slash(new Vector2(r.x,r.y+r.height*.18f),new Vector2(r.xMax,c.y+L*.7f),.1f,0,.1f,t,L*.18f,0,false,406);Slash(new Vector2(r.xMax,r.y+r.height*.18f),new Vector2(r.x,c.y+L*.7f),-.1f,.08f,.18f,t,L*.18f,0,false,407);break;
                case Plan.Move.Shield:{float d=Bell(t,0,.7f);Oval(c,L*1.3f,.55f,0,U.A(fa,.7f*d));for(int i=0;i<7;i++){float a=-170+i*26.6f;Hex(c+new Vector2(Mathf.Cos(a*Mathf.Deg2Rad)*L*1.25f,Mathf.Sin(a*Mathf.Deg2Rad)*L*.7f),L*.13f,30,1.4f,U.A(fh,.85f*d*Seg(t,i*.03f,.1f+i*.03f)));}break;}
                case Plan.Move.Bow:{float k=Seg(t,0,.25f);Arrow(c+new Vector2(0,-L*(.6f+U.H*.9f/L*InCubic(k))),-90,L*.9f,1-Seg(t,.2f,.3f),true);Motes(c,t,10,408,-90,60,300,.5f,L*.04f,fh,0,1);break;}
                case Plan.Move.Katana:case Plan.Move.DevKatana:
                    Cut(new Vector2(r.x-L*.4f,c.y),new Vector2(r.xMax+L*.4f,c.y),3,U.A(fh,1-Seg(t,.08f,.4f)));for(int i=0;i<16;i++){var v=Dir(-90+Sgn(409,i)*110)*(140+220*Rnd(410,i));const float drag=2.4f;float e=Mathf.Exp(-drag*t);Petal(c+v*((1-e)/drag)+new Vector2(0,60*t*t),L*.11f,t*200+i*30,Mathf.Cos(t*7+i),fade);}break;
                case Plan.Move.Glaive:{float k=Bell(t,0,.7f);U.Push(c,Vector2.zero,1,0,.4f);Thin(c,L*(.6f+1.4f*Mathf.Sin(Seg(t,0,.7f)*Mathf.PI)),U.A(fh,.9f*k));U.Pop();Sparks(c,t,10,411,-90,360,420,.4f,fh,200);break;}
                case Plan.Move.DevGreatsword:Cracks(new Vector2(c.x,r.yMax-L*.3f),M.OutCubic(Seg(t,0,.12f)),fade,412,8,r.width*.6f);Beam(new Vector2(r.x-L*.4f,r.yMax-L*.3f),new Vector2(r.xMax+L*.4f,r.yMax-L*.3f),2,U.A(fh,1-Seg(t,.1f,.5f)));break;
                case Plan.Move.Bombadier:Confetti(c,t,36,413,560,Plan.Finish,L*.08f);Smoke(c,t,5,414,L*.5f,.8f,.25f,new Vector2(0,-30));break;
                case Plan.Move.TopDog:Star(c,L*1.2f*Bell(t,0,.35f),15,12,new Color(1,.86f,.2f,.9f));for(int i=0;i<4;i++){float an=t*360+i*90;Star(new Vector2(c.x,c.y-L*.7f)+new Vector2(Mathf.Cos(an*Mathf.Deg2Rad)*L*.7f,Mathf.Sin(an*Mathf.Deg2Rad)*L*.2f),L*.14f,an,4,new Color(1,.86f,.2f,fade));}break;
                case Plan.Move.Siren:for(int i=-1;i<=1;i++){var n=new Vector2(-Dir(125).y,Dir(125).x);Slash(c+n*i*L*.25f-Dir(125)*L,c+n*i*L*.25f+Dir(125)*L,.04f,.02f*(i+1),.1f+.02f*(i+1),t,L*.12f,0,false,415+i);}Flames(new Rect(r.x,r.y,r.width,r.height*.9f),t,Bell(t,0,.9f)*.6f,10,new Color(1,.9f,1),U.Mix(fa,C.Magenta(),.4f),L*1.2f,416);break;
                case Plan.Move.Emperor:if(t<.2f){Bolt(new Vector2(c.x,-10),c,(int)(t*30)*3,L*.4f,3,U.A(new Color(.75f,.9f,1),1-t/.2f),12);}Sparks(c,t,12,417,-90,360,480,.45f,new Color(.75f,.9f,1));break;
                case Plan.Move.Ansatsuken:Ring(c,L*(.4f+2.2f*M.OutCubic(u)),new Color(.6f,.9f,1,.8f*(1-u)));Motes(c,t,18,418,-90,360,420,.6f,L*.05f,new Color(.6f,.9f,1),0,2);break;
                case Plan.Move.Leet:for(int i=0;i<18;i++){var v=Dir(Rnd(419,i)*360)*(160+240*Rnd(420,i));const float drag=2;float e=Mathf.Exp(-drag*t);var q=c+v*((1-e)/drag);U.Text(new Rect(q.x-8,q.y-8,16,16),bits[i%2],11,U.A(fh,fade),TextAnchor.MiddleCenter,bold:true,mono:true);}break;
                case Plan.Move.CyberLord:for(int i=0;i<12;i++){var v=Dir(i*30)*(260+120*Rnd(421,i));var q=c+v*t;float a=1-Seg(t,.1f,.6f);Hex(q,L*.1f,i*30,1.2f,U.A(fh,a));Streak(q-v*.05f,q,1.6f,U.A(fa,a));}break;
                default:Motes(c,t,18,422,-90,360,300,.8f,L*.05f,fh,-60,1.5f);break;
            }
        }
    }
}
