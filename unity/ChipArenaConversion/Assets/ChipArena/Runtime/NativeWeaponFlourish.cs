using System;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
using Plan=ChipArena.UnityConversion.NativeWeaponFlourishPlan;
namespace ChipArena.UnityConversion
{
    // The Blind Pick weapon flourishes (owner 2026-11-18: "Give each weapon choice a selection animation for the player ...
    // the "Oh that looks cool!" impression ... if they select pistol ... a pistol will eject from the icon and shoot around
    // etc. Be creative!"). Every roster weapon has its own move (NativeWeaponFlourish.Moves.cs; timing and sounds in Core's
    // NativeWeaponFlourishPlan): choosing a card plays its weapon's 1-2 s flourish, choosing again or another card restarts it
    // on that card, and the locked-in card plays a short finisher.
    //  - Draw and Hero queue the flourish of the card the pilot chose (Look.Selected, from Look.SelectAge) and of the locked
    //    card (Look.Locked, from LockAge, or from SelectAge when the locked card first shows later). Flourishes(), called once
    //    per frame after every card, draws them over the whole screen, so they fly outside the card. Against bots the locked-in
    //    screen never shows (lock-in goes straight to the reveal), so the reveal plays the finisher on the pilot's own card
    //    (Look.Finish) unless the locked-in screen just did; every other pilot's card there is Quiet. A screen that never calls
    //    Flourishes() shows none: the card's own part (its shake, the icon leaving its slot, the Katana's cut) plays only while
    //    Flourishes() drew that same flourish in the last frames.
    //  - Drawn in the card kit's hardlight style from the weapon's own icon art (the same raster the card drew, so nothing
    //    rasterises mid-flourish) with light, trails, sparks and particles: every particle is a closed-form function of the
    //    flourish's age, so nothing is allocated or kept per frame and a restart is clean by construction.
    //  - Reduced effects plays a gentle pulse instead (no motion, shake, flashes or particles).
    //  - Sounds: a few short quiet cues per move through NativeAudio's screen cue path (Sound).
    // Presentation only: each pilot's own screen, never sent; nothing here reads or writes rules, timers or the network, and the
    // pick, the countdown and the reveal never wait on it.
    public static partial class NativeWeaponCard
    {
        /// <summary>A flourish's sound is due (NativeAudio plays it: the bank key, else the fallback clip, or the toon sound).</summary>
        public static event Action<Plan.Cue> Sound;

        private struct Act {public ContentWeapon W;public Plan.Move Move;public Rect Card;public Vector2 Icon;public float Size,Age,Start,Drawn;public bool Hero,Calm,Locked;public int Frame;public Matrix4x4 Kit;}
        private static Act select=new Act{Frame=-10},finish=new Act{Frame=-10},liveSelect=new Act{Frame=-10},liveFinish=new Act{Frame=-10};
        private static readonly Plan.CueClock selectClock=new Plan.CueClock(),finishClock=new Plan.CueClock();
        // This frame's cards (targets for the shots, the chain lightning, the glaive's loop) and the hero card (the arrow's).
        private static readonly Rect[] seen=new Rect[48];private static int seenCount,seenFrame=-10,heroFrame=-10;private static Rect heroSeen;
        // The hero card's weapon and icon size this frame: when it shows the flourish's weapon, the flourish draws from its larger
        // raster (already made by the hero), so a weapon drawn twice its card size stays sharp.
        private static ContentWeapon heroWeapon;private static float heroIcon;
        // Every hero card of the frame (the dealt cards): the targets of a flourish played on a hero card.
        private static readonly Rect[] heroes=new Rect[16];private static int heroCount;

        // The card's own part of a flourish: drawn moved (shake, recoil, squash), its icon out of its slot (Hide), flashed,
        // or cut into bands (the Katana's two halves, L33T's glitch slices).
        private struct CardFx
        {
            public Vector2 Shift;public float Sx,Sy,Hide,Flash;public int Bands;public float Split,Cut,Glitch;
            public bool Moves=>Shift.sqrMagnitude>.0001f||Mathf.Abs(Sx-1)>.0005f||Mathf.Abs(Sy-1)>.0005f;
        }
        private static readonly CardFx Still=new CardFx{Sx=1,Sy=1};

        // Records the card and queues its flourish; the card's own part while that flourish is live.
        private static CardFx Track(Rect r,ContentWeapon w,in Look k,bool hero,Vector2 icon,float size)
        {
            int f=Time.frameCount;if(seenFrame!=f){seenFrame=f;seenCount=0;}
            if(hero){if(heroFrame!=f)heroCount=0;heroSeen=r;heroFrame=f;heroWeapon=w;heroIcon=size;if(heroCount<heroes.Length)heroes[heroCount++]=r;}else if(seenCount<seen.Length)seen[seenCount++]=r;
            if(k.Quiet)return Still;var move=Plan.Of(w.id);bool locked=k.Locked>0;
            if(locked||k.Finish)
            {
                // A locked card's finisher runs from the lock, or from the card's first showing when the locked-in screen comes
                // later (after the starting chip). The reveal's own card plays it unless the locked-in screen just did.
                float age=!locked?k.LockAge:k.LockAge>=0&&k.SelectAge>=0?Mathf.Min(k.LockAge,k.SelectAge):k.LockAge>=0?k.LockAge:k.SelectAge;
                if(age<0||age>Plan.Finish||finish.Frame==f)return Still;
                if(!locked&&liveFinish.W==w&&liveFinish.Locked&&liveFinish.Age>Plan.Finish*.5f&&Time.unscaledTime-liveFinish.Drawn<60)return Still;
                finish=new Act{W=w,Move=move,Card=r,Icon=icon,Size=size,Age=age,Start=k.Time-age,Hero=hero,Calm=k.Calm,Locked=locked,Frame=f,Kit=U.Current};
                return Live(finish,liveFinish)?FinishFx(finish):Still;
            }
            if(k.Selected<.5f||k.SelectAge<0||k.SelectAge>Plan.Length(move,k.Calm)||select.Frame==f)return Still;
            select=new Act{W=w,Move=move,Card=r,Icon=icon,Size=size,Age=k.SelectAge,Start=k.Time-k.SelectAge,Hero=hero,Calm=k.Calm,Frame=f,Kit=U.Current};
            return Live(select,liveSelect)?SelectFx(select):Still;
        }
        // (A start within .3 s is the same flourish: the reveal's or a restarted one is seconds off.)
        private static bool Live(in Act a,in Act live)=>live.Frame>=a.Frame-2&&live.W==a.W&&live.Hero==a.Hero&&Mathf.Abs(live.Start-a.Start)<.3f;

        /// <summary>Draws this frame's queued flourishes over everything drawn so far (call once per frame, after the cards) and
        /// cues their sounds. Repaint only; nothing is drawn when no card queued one this frame.</summary>
        public static void Flourishes()
        {
            if(Event.current==null||Event.current.type!=EventType.Repaint)return;int f=Time.frameCount;float dt=Time.unscaledDeltaTime;
            if(select.Frame==f)
            {
                select.Drawn=Time.unscaledTime;liveSelect=select;Begin(select);U.PushMatrix(select.Kit);if(select.Calm)Gentle(select.Age,false);else DrawMove(select.Move,select.Age);U.Pop();
                Cues(selectClock,select,Plan.Cues(select.Move),dt);
            }
            if(finish.Frame==f)
            {
                finish.Drawn=Time.unscaledTime;liveFinish=finish;Begin(finish);U.PushMatrix(finish.Kit);if(finish.Calm)Gentle(finish.Age,true);else Finisher(finish.Move,finish.Age);U.Pop();
                Cues(finishClock,finish,Plan.FinishCues(finish.Move),dt);
            }
        }
        private static void Cues(Plan.CueClock clock,in Act a,Plan.Cue[] cues,float dt)
        {
            clock.Step(a.Move,a.Start,a.Age,dt);if(Sound==null)return;
            for(int i=0;i<cues.Length;i++)if(clock.Due(cues[i])){Sound(cues[i]);if(a.Calm)break;}
        }

        // ---- the flourish's frame ----------------------------------------------------------------------------------------
        // The card's icon centre and size, the card, the weapon's colours and icon, a seed, and the screen side with more room.
        private static Vector2 fc;private static float fs,fslot,fspr,fdir;private static Rect fr;private static Color fa,fh;private static string fi;private static int fseed;private static bool fhero;
        private static void Begin(in Act a)
        {
            fc=a.Icon;fslot=Mathf.Max(8,a.Size);
            // A hero card's icon is far larger than a grid card's: its flourish keeps a grid card's measure (fs) and starts and ends
            // at the hero's icon (fslot). A grid card's flourish draws from the hero's raster when the hero shows the same weapon.
            fs=a.Hero?Mathf.Clamp(fslot*.6f,72,100):fslot;
            fspr=a.Hero?fslot:heroFrame==Time.frameCount&&heroWeapon==a.W&&heroIcon>fslot?heroIcon:fslot;fr=a.Card;fa=Accent(a.W);fh=U.Mix(fa,Color.white,.55f);fi=NativeIcons.Spec(a.W.id);fhero=a.Hero;
            unchecked{int h=17;foreach(char ch in a.W.id)h=h*31+ch;fseed=h;}
            fdir=fc.x<U.W*.5f?1:-1;
        }
        private static Rect Whole=>new Rect(0,0,U.W,U.H);

        // ---- maths -------------------------------------------------------------------------------------------------------
        private static float Seg(float t,float a,float b)=>M.Clamp01((t-a)/(b-a));
        private static float Bell(float t,float a,float b)=>t<=a||t>=b?0:Mathf.Sin((t-a)/(b-a)*Mathf.PI);
        private static float InCubic(float t){t=M.Clamp01(t);return t*t*t;}
        private static float InOut(float t){t=M.Clamp01(t);return t*t*(3-2*t);}
        // Overshoots and settles (a spring's step).
        private static float Spring(float t,float k=7){t=Mathf.Max(0,t);return 1-Mathf.Exp(-k*t)*Mathf.Cos(t*k*1.4f);}
        // A deterministic 0..1 per (salt, i) and this weapon.
        private static float Rnd(int salt,int i){unchecked{uint x=(uint)(salt*374761393+i*668265263+fseed*1274126177);x=(x^(x>>13))*1274126177u;x^=x>>16;return (x&0xffffff)/16777216f;}}
        private static float Sgn(int salt,int i)=>Rnd(salt,i)*2-1;
        private static Vector2 Dir(float deg){float a=deg*Mathf.Deg2Rad;return new Vector2(Mathf.Cos(a),Mathf.Sin(a));}
        private static float Deg(Vector2 d)=>Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg;
        private static Vector2 Rot(Vector2 v,float deg){float a=deg*Mathf.Deg2Rad,c=Mathf.Cos(a),s=Mathf.Sin(a);return new Vector2(v.x*c-v.y*s,v.x*s+v.y*c);}
        private static Vector2 Bez(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float u){float v=1-u;return v*v*v*a+3*v*v*u*b+3*v*u*u*c+u*u*u*d;}
        private static Vector2 Shake(float t,float amp)=>new Vector2(Mathf.Sin(t*97)+Mathf.Sin(t*151+1.3f),Mathf.Sin(t*83+.7f)+Mathf.Sin(t*137+2.1f))*.5f*amp;
        private static Vector2 Clamp(Vector2 p,float m=30)=>new Vector2(Mathf.Clamp(p.x,m,U.W-m),Mathf.Clamp(p.y,m,U.H-m));
        // A point in the icon's own frame (fractions of its size, y down) on the weapon drawn at p, scale, deg, sx.
        private static Vector2 On(Vector2 p,float scale,float deg,float sx,float x,float y)=>p+Rot(new Vector2(x*sx,y)*Size(scale),deg);
        // The weapon's drawn size at `scale`: the card's icon in its slot (1), Big of the measure when out (a hero card's blends).
        private static float Size(float s)=>fslot==fs?fs*Big(s):s<=1?fslot*s:Mathf.Lerp(fslot,fs*Big(s),Seg(s,1,1.2f));
        // Out of its slot a weapon is drawn larger than its card icon: 1 stays 1 (the slot), 1.2 and more are 1.6 times.
        private static float Big(float s)=>s<=1?s:s*(1+.6f*Seg(s,1,1.2f));
        // The turn and mirror that point a weapon whose icon faces `fwd` (degrees) from `from` at `to`, kept upright.
        private static void Aim(Vector2 from,Vector2 to,float fwd,out float deg,out float sx)
        {
            float th=Deg(to-from);if(to.x>=from.x){sx=1;deg=th-fwd;}else{sx=-1;deg=th-180+fwd;}
            deg=Mathf.Repeat(deg+180,360)-180;
        }

        // Targets: the other cards' centres (this frame's), nearest-first from `from` skipping the flourish's own card and
        // `used`; false when there is none.
        private static bool Target(int nth,Vector2 from,bool far,out Vector2 at,out Rect card)
        {
            at=default;card=default;int n=Others();if(n==0)return false;
            // A selection sort over at most 48 cards, by distance, skipping the own card: the nth from the near or far end.
            float last=far?float.MaxValue:-1;int pick=-1;
            for(int round=0;round<=nth;round++)
            {
                pick=-1;float best=far?-1:float.MaxValue;
                for(int i=0;i<n;i++)
                {
                    var c=Other(i);if(c.Overlaps(fr))continue;float d=(c.center-from).sqrMagnitude+i*.001f;
                    if(far?(d<last&&d>best):(d>last&&d<best)){best=d;pick=i;}
                }
                if(pick<0)return false;last=best;
            }
            card=Other(pick);at=card.center;return true;
        }
        // The other cards of this frame a flourish may aim at: the grid's, and for a hero card's flourish the other hero cards.
        private static int Others(){int f=Time.frameCount;return (seenFrame==f?seenCount:0)+(fhero&&heroFrame==f?heroCount:0);}
        private static Rect Other(int i){int g=seenFrame==Time.frameCount?seenCount:0;return i<g?seen[i]:heroes[i-g];}
        private static bool HeroCard(out Rect r){r=heroSeen;return heroFrame==Time.frameCount&&!fhero;}

        // ---- drawing kit -------------------------------------------------------------------------------------------------
        private static readonly float[] RingT={0,.8f,.91f,.97f,1},RingTA={0,0,1,.4f,0},ThinT={0,.9f,.955f,.985f,1},ThinTA={0,0,1,.3f,0},
            HoleT={0,.9f,.94f,1},HoleTA={0,0,1,1},SoftT={0,.5f,1},SoftTA={1,.45f,0};
        private static void Line(Vector2 a,Vector2 b,float w,Color c)
        {var d=b-a;float len=d.magnitude;if(len<.05f||c.a<=.003f)return;U.Push(a,Vector2.zero,1,Deg(d));U.Fill(new Rect(a.x,a.y-w*.5f,len,w),c);U.Pop();}
        private static void GlowLine(Vector2 a,Vector2 b,float w,Color c,float spread)
        {var d=b-a;float len=d.magnitude;if(len<.05f||c.a<=.003f)return;U.Push(a,Vector2.zero,1,Deg(d));U.Glow(new Rect(a.x,a.y-w*.5f,len,w),c,spread);U.Pop();}
        // Clear at the tail `a`, `c` at the head `b`.
        private static void Streak(Vector2 a,Vector2 b,float w,Color c)
        {var d=b-a;float len=d.magnitude;if(len<.05f||c.a<=.003f)return;U.Push(a,Vector2.zero,1,Deg(d));U.HGradient(new Rect(a.x,a.y-w*.5f,len,w),U.A(c,0),c);U.Pop();}
        // A lit line: its glow, its colour and a white core.
        private static void Beam(Vector2 a,Vector2 b,float w,Color c,float glow=1)
        {if(c.a<=.003f)return;GlowLine(a,b,w*2.2f,U.A(c,.42f*glow*c.a),w*4);Line(a,b,w,c);Line(a,b,Mathf.Max(1,w*.45f),new Color(1,1,1,c.a));}
        // A cut: a lit line tapered at both ends.
        private static void Cut(Vector2 a,Vector2 b,float w,Color c)
        {
            if(c.a<=.003f)return;var m=(a+b)*.5f;GlowLine(a,b,w*1.8f,U.A(c,.42f*c.a),w*5);
            Streak(a,m,w,c);Streak(b,m,w,c);Streak(a,m,w*.4f,new Color(1,1,1,c.a));Streak(b,m,w*.4f,new Color(1,1,1,c.a));
        }
        private static void Dot(Vector2 p,float d,Color c)=>U.Radial(Square(p,d),c);
        private static void Ring(Vector2 p,float rad,Color c){if(rad>.5f)U.RadialStops(Square(p,rad*2),c,RingT,RingTA);}
        private static void Thin(Vector2 p,float rad,Color c){if(rad>.5f)U.RadialStops(Square(p,rad*2),c,ThinT,ThinTA);}
        // A ring seen at an angle: squashed to `squash` of its height and turned `deg`.
        private static void Oval(Vector2 p,float rad,float squash,float deg,Color c,bool thin=false){U.Push(p,Vector2.zero,1,deg,Mathf.Max(.03f,squash));if(thin)Thin(p,rad,c);else Ring(p,rad,c);U.Pop();}
        private static void Soft(Vector2 p,float rad,Color c){if(rad>.5f)U.RadialStops(Square(p,rad*2),c,SoftT,SoftTA);}
        // `rays` light spikes about `deg` (alternately long and short) over a bright core: a muzzle flash or a star.
        private static void Star(Vector2 p,float size,float deg,int rays,Color c)
        {
            if(c.a<=.003f)return;Dot(p,size*1.5f,U.A(c,.5f*c.a));
            for(int i=0;i<rays;i++){float len=size*(i%2==0?1:.5f)*(.8f+.4f*Rnd(91,i));Ray(p,deg+i*360f/rays,len,Mathf.Max(2,size*.14f),c);}
            Dot(p,size*.55f,new Color(1,1,1,c.a));
        }
        private static void Ray(Vector2 p,float deg,float len,float w,Color c){if(len<.5f||c.a<=.003f)return;U.Push(p,Vector2.zero,1,deg);U.HGradient(new Rect(p.x,p.y-w*.5f,len,w),c,U.A(c,0));U.Pop();}
        // A filled rotated box (debris, a casing, confetti), `squash` its turn about its length.
        private static void Box(Vector2 p,float w,float h,float deg,Color c,float squash=1)
        {if(c.a<=.003f)return;U.Push(p,Vector2.zero,1,deg,Mathf.Sign(squash)*Mathf.Max(.08f,Mathf.Abs(squash)));U.Fill(new Rect(p.x-w*.5f,p.y-h*.5f,w,h),c);U.Pop();}
        // A soft oval (a petal, a flame tongue, a droplet).
        private static void Blob(Vector2 p,float w,float h,float deg,Color c){if(c.a<=.003f||w<.3f||h<.3f)return;U.Push(p,Vector2.zero,w,deg,h);U.Radial(new Rect(p.x-.5f,p.y-.5f,1,1),c);U.Pop();}
        private static void Hex(Vector2 p,float rad,float deg,float w,Color c)
        {
            if(c.a<=.003f)return;var prev=p+Dir(deg)*rad;
            for(int i=1;i<=6;i++){var q=p+Dir(deg+i*60)*rad;Line(prev,q,w,c);prev=q;}
        }
        // A jagged bolt from a to b, its kinks re-drawn every 1/30 s (`salt` changes).
        private static void Bolt(Vector2 a,Vector2 b,int salt,float jag,float w,Color c,int seg=9)
        {
            var d=b-a;float len=d.magnitude;if(len<1||c.a<=.003f)return;var n=new Vector2(-d.y,d.x)/len;var prev=a;
            for(int i=1;i<=seg;i++)
            {
                float u=i/(float)seg;var p=a+d*u+(i<seg?n*(Sgn(salt,i)*jag*(.4f+.6f*Mathf.Sin(u*Mathf.PI))):Vector2.zero);
                GlowLine(prev,p,w*2.4f,U.A(c,.4f*c.a),w*4.5f);Line(prev,p,w,c);Line(prev,p,Mathf.Max(1,w*.45f),new Color(1,1,1,c.a));prev=p;
            }
        }
        // A blade's sweep along the arc of radius `rad` about `o`, angles a0 to a1: drawn from p0 (tail) to p1 (head) of the
        // way, widest and brightest at the head.
        private static void Sweep(Vector2 o,float rad,float a0,float a1,float p0,float p1,float w,float alpha,int n=16)
        {
            if(p1<=p0+.001f||alpha<=.003f)return;var prev=o+Dir(Mathf.Lerp(a0,a1,p0))*rad;
            for(int i=1;i<=n;i++)
            {
                float k=i/(float)n,u=Mathf.Lerp(p0,p1,k);var p=o+Dir(Mathf.Lerp(a0,a1,u))*rad;float ww=w*(.12f+.88f*k);
                GlowLine(prev,p,ww,U.A(fa,.36f*alpha*k),ww*2.2f);Line(prev,p,ww,U.A(fa,.8f*alpha*k));Line(prev,p,Mathf.Max(1,ww*.38f),new Color(1,1,1,.95f*alpha*k));prev=p;
            }
        }
        // `n` sparks from `o`, `t` s after they burst: hot streaks thrown about `deg` within `spread`, slowed and falling.
        private static void Sparks(Vector2 o,float t,int n,int salt,float deg,float spread,float speed,float life,Color c,float grav=700,float width=2.6f)
        {
            if(t<0||t>life)return;
            for(int i=0;i<n;i++)
            {
                float li=life*(.55f+.45f*Rnd(salt,i));if(t>li)continue;float u=t/li;
                var v=Dir(deg+Sgn(salt+1,i)*spread*.5f)*speed*(.4f+.6f*Rnd(salt+2,i));const float drag=3.4f;float e=Mathf.Exp(-drag*t);
                var p=o+v*((1-e)/drag)+new Vector2(0,.5f*grav*t*t);var vel=v*e+new Vector2(0,grav*t);float sp=vel.magnitude;if(sp<1)continue;
                float len=Mathf.Clamp(sp*.05f,3,30);Streak(p-vel/sp*len,p,width*(1-.5f*u),U.A(U.Mix(c,Color.white,.4f*(1-u)),(1-u)*(1-u)*c.a));
            }
        }
        // `n` motes (soft dots) thrown from `o`: embers, droplets, dust; `rise` lifts them (negative gravity).
        private static void Motes(Vector2 o,float t,int n,int salt,float deg,float spread,float speed,float life,float size,Color c,float grav=0,float drag=2.5f)
        {
            if(t<0||t>life)return;
            for(int i=0;i<n;i++)
            {
                float li=life*(.6f+.4f*Rnd(salt,i)),ti=t-.06f*Rnd(salt+5,i)*life;if(ti<0||ti>li)continue;float u=ti/li;
                var v=Dir(deg+Sgn(salt+1,i)*spread*.5f)*speed*(.35f+.65f*Rnd(salt+2,i));float e=Mathf.Exp(-drag*ti);
                var p=o+v*((1-e)/drag)+new Vector2(Mathf.Sin(ti*6+i)*3,.5f*grav*ti*ti);float s=size*(.6f+.6f*Rnd(salt+3,i))*(1-.5f*u);
                Dot(p,s*2.4f,U.A(c,.28f*(1-u)*c.a));Dot(p,s,U.A(U.Mix(c,Color.white,.5f),(1-u)*c.a));
            }
        }
        // Smoke: soft grey puffs from `o`, growing, rising and thinning.
        private static void Smoke(Vector2 o,float t,int n,int salt,float size,float life,float alpha,Vector2 drift)
        {
            if(t<0||t>life)return;
            for(int i=0;i<n;i++)
            {
                float ti=t-i*life*.08f;if(ti<0)continue;float u=ti/life;if(u>1)continue;
                var p=o+drift*ti+new Vector2(Sgn(salt,i)*size*.5f*u,-size*.8f*u);float s=size*(.4f+1.3f*u)*(.7f+.5f*Rnd(salt+1,i));
                Dot(p,s,new Color(.62f,.66f,.74f,alpha*(1-u)*(1-u)*Mathf.Clamp01(ti*12)));
            }
        }
        // A brass casing ejected from `o` at `t` s: up and back, tumbling, bouncing once on `floor`.
        private static void Casing(Vector2 o,float t,int i,float back,float floor,float alpha=1)
        {
            if(t<0||t>1.1f)return;float vx=back*(90+70*Rnd(41,i)),vy=-(240+120*Rnd(42,i)),g=1500;
            float x=o.x+vx*t,y=o.y+vy*t+.5f*g*t*t,deg=t*(900+500*Rnd(43,i))*Mathf.Sign(back);
            // The bounce: from the floor at its first contact, a third of the speed.
            float tc=(-vy+Mathf.Sqrt(Mathf.Max(0,vy*vy+2*g*(floor-o.y))))/g;
            if(t>tc){float u=t-tc,v2=-(vy+g*tc)*.32f;x=o.x+vx*tc+vx*.5f*u;y=Mathf.Min(floor,floor+v2*u+.5f*g*u*u);deg=tc*(900+500*Rnd(43,i))*Mathf.Sign(back)+u*400*Mathf.Sign(back);}
            float a=alpha*(1-Seg(t,.75f,1.1f));var p=new Vector2(x,y);float L=Mathf.Max(6,fs*.13f);
            Dot(p,L*2.2f,new Color(1,.8f,.35f,.22f*a));Box(p,L,L*.45f,deg,new Color(.95f,.74f,.32f,a));Box(p+Rot(new Vector2(0,-L*.12f),deg),L*.8f,L*.12f,deg,new Color(1,.95f,.75f,.8f*a));
        }
        // A bullet hole where a shot landed: a dark pit with a hot rim, fading.
        private static void Hole(Vector2 p,float t,float size,int salt)
        {
            if(t<0||t>1.2f)return;float a=1-Seg(t,.6f,1.2f),hot=1-Seg(t,0,.35f);
            Dot(p,size*3,U.A(fa,.35f*hot*a));Dot(p,size*1.2f,new Color(0,0,0,.65f*a));Thin(p,size*.75f,U.A(U.Mix(fa,Color.white,.6f),(.4f+.6f*hot)*a));
            for(int i=0;i<5;i++){float d=Rnd(salt,i)*360;Line(p+Dir(d)*size*.5f,p+Dir(d)*size*(1.2f+Rnd(salt+1,i)),1,new Color(1,1,1,.35f*a));}
        }
        // A spray of confetti from `o`: paper squares in five colours, fluttering down.
        private static void Confetti(Vector2 o,float t,int n,int salt,float speed,float life,float size,float alpha=1)
        {
            if(t<0||t>life)return;
            for(int i=0;i<n;i++)
            {
                float u=t/life;var v=Dir(-90+Sgn(salt,i)*170)*speed*(.3f+.7f*Rnd(salt+1,i));const float drag=2.6f;float e=Mathf.Exp(-drag*t);
                var p=o+v*((1-e)/drag)+new Vector2(Mathf.Sin(t*5+i*1.7f)*size*1.5f,90*t*t+60*t);
                Color c=Confetto(i);float spin=t*(300+400*Rnd(salt+2,i))+i*40;
                Box(p,size,size*.6f,spin,U.A(c,alpha*(1-Seg(u,.7f,1))),Mathf.Cos(t*(9+6*Rnd(salt+3,i))+i));
            }
        }
        private static Color Confetto(int i){switch(i%5){case 0:return U.Hex(0xff4d6d);case 1:return U.Hex(0xffd23f);case 2:return U.Hex(0x3ee6ff);case 3:return U.Hex(0x7dff6a);default:return U.Hex(0xc77dff);}}
        // A cherry petal: a soft pink oval with a pale centre, its flutter a turn about its length.
        private static void Petal(Vector2 p,float size,float deg,float flip,float a)
        {
            if(a<=.003f)return;float f=Mathf.Max(.15f,Mathf.Abs(flip));
            Blob(p,size,size*.62f*f,deg,new Color(1,.55f,.72f,.8f*a));Blob(p,size*.5f,size*.3f*f,deg,new Color(1,.88f,.93f,.9f*a));
        }
        // The weapon: a hardlight copy of its icon (the card's raster, scaled on the GPU), a soft glow in its colour, the
        // chromatic split along fast motion and a drop shadow. `sx` mirrors and turns it (|sx| < 1 reads as a turn in depth).
        private static void Arm(Vector2 p,float scale,float deg,float sx,float a,Vector2 vel=default,float glow=1)
        {
            if(a<=.003f||scale<=.01f)return;float sz=Size(scale);
            Dot(p,sz*1.9f,U.A(fa,.26f*glow*a));
            float sp=vel.magnitude;if(sp>350){var d=vel/sp*Mathf.Min(5,sp/500);Glyph(p-d,scale,deg,sx,C.Magenta(.32f*a));Glyph(p+d,scale,deg,sx,C.Cyan(.32f*a));}
            Glyph(p+new Vector2(0,sz*.04f+2),scale,deg,sx,new Color(0,0,0,.42f*a));Glyph(p,scale,deg,sx,U.A(U.Mix(fa,Color.white,.5f),a));
        }
        // The weapon's afterimages along its path: `n` copies at `dt` s apart behind its pose now (`pose(t)`).
        private static void Glyph(Vector2 p,float scale,float deg,float sx,Color c)
        {
            if(c.a<=.003f)return;if(Mathf.Abs(sx)<.04f)sx=sx<0?-.04f:.04f;
            float k=Size(scale)/fspr;U.Push(p,Vector2.zero,k*sx,deg,k);NativeIcons.Draw(Square(p,fspr),fi,c);U.Pop();
        }
        private static void Flash(float a,Color c){if(a>.003f)U.Fill(Whole,U.A(c,a));}
        private static void CardFlash(Rect r,float a,Color c){if(a>.003f){U.Glow(r,U.A(c,.5f*a),16);U.Round(r,U.A(c,.35f*a),R);}}
        // A comic word: big, bold, outlined, popped in at `pop` and turned `deg`.
        private static void Word(Vector2 p,string word,float size,float pop,float deg,Color c,float a)
        {
            if(a<=.003f)return;float s=Mathf.Max(.05f,pop);U.Push(p,Vector2.zero,s,deg);var r=new Rect(p.x-size*3,p.y-size*.8f,size*6,size*1.6f);
            for(int i=0;i<8;i++){var o=Dir(i*45)*Mathf.Max(1.5f,size*.07f);U.Text(new Rect(r.x+o.x,r.y+o.y,r.width,r.height),word,size,new Color(.08f,.04f,.06f,a),TextAnchor.MiddleCenter,bold:true);}
            U.Text(r,word,size,U.A(c,a),TextAnchor.MiddleCenter,bold:true,glow:U.A(c,.4f*a));U.Pop();
        }

        // ---- the card's part ----------------------------------------------------------------------------------------------
        private static CardFx SelectFx(in Act a)
        {
            var fx=Still;float t=a.Age;if(a.Calm){fx.Flash=.25f*Bell(t,0,.5f);return fx;}
            switch(a.Move)
            {
                case Plan.Move.Pistol:fx.Shift=new Vector2(0,6*Bell(t,0,.25f));fx.Hide=Seg(t,0,.08f)*(1-Seg(t,1.56f,1.64f));fx.Flash=.5f*(1-Seg(t,1.6f,1.8f))*Seg(t,1.6f,1.61f);break;
                case Plan.Move.Rifle:fx.Hide=Seg(t,0,.1f)*(1-Seg(t,1.75f,1.85f));fx.Shift=new Vector2(0,5*Bell(t,0,.3f));break;
                case Plan.Move.MachineGun:fx.Hide=Seg(t,0,.08f)*(1-Seg(t,1.68f,1.76f));fx.Shift=Shake(t,t>.3f&&t<1.25f?2.2f:0)+new Vector2(0,6*Bell(t,0,.25f));break;
                case Plan.Move.Fire:fx.Flash=.22f*Bell(t,0,.7f);fx.Shift=Shake(t,1.2f*Bell(t,.05f,.7f));break;
                case Plan.Move.Water:{float w=t>.5f?Mathf.Exp(-(t-.5f)*4)*Mathf.Sin((t-.5f)*26):0;fx.Sx=1+.045f*w;fx.Sy=1-.045f*w;fx.Hide=.6f*Bell(t,0,.55f);break;}
                case Plan.Move.Lightning:fx.Shift=Shake(t,2.2f*Bell(t,0,.3f));fx.Flash=.35f*Bell(t,.24f,.34f);break;
                case Plan.Move.Darkness:{float pull=InOut(Seg(t,.2f,1.05f))*(1-Seg(t,1.12f,1.2f));fx.Sx=fx.Sy=1-.06f*pull+.05f*Bell(t,1.12f,1.4f);fx.Hide=Seg(t,.1f,.3f)*(1-Seg(t,1.2f,1.45f));break;}
                case Plan.Move.Sword:fx.Hide=Seg(t,0,.08f)*(1-Seg(t,1.6f,1.66f));fx.Shift=Dir(35)*4*Bell(t,.4f,.52f)+Dir(200)*4*Bell(t,.64f,.76f)+Dir(90)*5*Bell(t,.9f,1.02f);break;
                case Plan.Move.Daggers:fx.Hide=Seg(t,0,.1f)*(1-Seg(t,1.4f,1.48f));fx.Shift=new Vector2(0,-5*Bell(t,1.1f,1.25f))+Shake(t,1.8f*Bell(t,1.1f,1.3f));break;
                case Plan.Move.Shield:fx.Hide=Seg(t,0,.1f)*(1-Seg(t,1.5f,1.58f));fx.Shift=new Vector2(-fdir*7*Bell(t,.45f,.62f),0)+Shake(t,3*Bell(t,.45f,.7f));break;
                case Plan.Move.Greatsword:
                {
                    float hit=t-.66f,sq=hit>0?Mathf.Exp(-hit*6)*Mathf.Cos(hit*24):0;fx.Hide=Seg(t,0,.12f)*(1-Seg(t,1.76f,1.84f));
                    fx.Sx=1+.05f*sq;fx.Sy=1-.08f*sq;fx.Shift=new Vector2(0,hit>0?9*Mathf.Exp(-hit*5):0)+Shake(t,hit>0&&hit<.35f?3.5f*(1-hit/.35f):0);fx.Flash=hit>0?.45f*Mathf.Exp(-hit*12):0;break;
                }
                case Plan.Move.Bow:fx.Hide=Seg(t,0,.1f)*(1-Seg(t,1.32f,1.4f));break;
                case Plan.Move.Katana:
                {
                    fx.Hide=Seg(t,0,.1f)*(1-Seg(t,1.28f,1.33f));float open=Seg(t,.58f,.66f)*(1-InOut(Seg(t,.92f,1.16f)));
                    if(open>0){fx.Bands=2;fx.Split=9*M.OutCubic(open);fx.Cut=.47f;}fx.Shift=Shake(t,2.5f*Bell(t,.58f,.7f))+new Vector2(0,3*Bell(t,1.3f,1.42f));fx.Flash=.5f*Bell(t,.57f,.66f);break;
                }
                case Plan.Move.Glaive:fx.Hide=Seg(t,.12f,.2f)*(1-Seg(t,1.24f,1.3f));fx.Shift=new Vector2(0,4*Bell(t,1.24f,1.4f));break;
                case Plan.Move.Fangs:fx.Shift=Dir(45)*3*Bell(t,.28f,.38f)+Dir(135)*3*Bell(t,.38f,.48f);fx.Flash=.15f*Bell(t,.3f,.6f);break;
                case Plan.Move.DevGreatsword:{float hit=t-.5f;fx.Hide=Seg(t,0,.12f)*(1-Seg(t,1.8f,1.88f));fx.Shift=Shake(t,hit>0&&hit<.6f?4f*(1-hit/.6f):0);fx.Flash=hit>0?.35f*Mathf.Exp(-hit*10):0;if(t>1.42f&&t<1.62f){fx.Bands=6;fx.Glitch=Bell(t,1.42f,1.62f);}break;}
                case Plan.Move.DevKatana:
                {
                    fx.Hide=Seg(t,0,.1f)*(1-Seg(t,1.5f,1.56f));float k=Mathf.Repeat((t-.32f)/.14f,1);bool flurry=t>.32f&&t<.86f;
                    fx.Shift=flurry?Dir(Rnd(7,(int)((t-.32f)/.14f))*360)*4*(1-k):Shake(t,3*Bell(t,.9f,1.05f));if(t>.9f&&t<1.1f){fx.Bands=2;fx.Split=6*Bell(t,.9f,1.1f);fx.Cut=.5f;}break;
                }
                case Plan.Move.Bombadier:{float boom=t-1;fx.Hide=Seg(t,0,.08f)*(1-Seg(t,1.4f,1.6f));fx.Shift=Shake(t,boom>0?7*Mathf.Exp(-boom*5):Seg(t,.3f,1)*1.6f);fx.Flash=boom>0?.6f*Mathf.Exp(-boom*9):0;break;}
                case Plan.Move.TopDog:
                {
                    float b=t-1.35f;fx.Hide=Seg(t,0,.06f)*(1-Seg(t,1.33f,1.36f));float sq=b>0?Mathf.Exp(-b*5)*Mathf.Cos(b*20):0;
                    fx.Sx=1+.14f*sq;fx.Sy=1-.26f*sq;fx.Shift=new Vector2(0,b>0?14*Mathf.Exp(-b*6):0);if(t<.3f){float s=Bell(t,0,.3f);fx.Sy=1-.08f*s;fx.Sx=1+.05f*s;}break;
                }
                case Plan.Move.Siren:fx.Shift=Dir(-140)*4*Bell(t,.34f,.5f)+Shake(t,1.6f*Bell(t,.6f,1.3f));fx.Flash=.12f*Bell(t,0,.4f);break;
                case Plan.Move.Emperor:fx.Hide=Seg(t,0,.08f)*(1-Seg(t,1.62f,1.7f));fx.Shift=new Vector2(8*Bell(t,.55f,.68f)-8*Bell(t,.75f,.88f),-6*Bell(t,.95f,1.3f))+Shake(t,2*Bell(t,.25f,.4f));break;
                case Plan.Move.Ansatsuken:fx.Hide=Seg(t,0,.08f)*(1-Seg(t,1.62f,1.7f));fx.Shift=new Vector2(0,5*Bell(t,1.6f,1.75f));break;
                case Plan.Move.Leet:{float g=Bell(t,0,.42f)+.8f*Bell(t,1.38f,1.62f);if(g>.01f){fx.Bands=7;fx.Glitch=g;}break;}
                case Plan.Move.CyberLord:fx.Hide=Seg(t,.18f,.24f)*(1-Seg(t,1.4f,1.5f));fx.Flash=.2f*Bell(t,0,.3f);fx.Shift=new Vector2(0,-5*Bell(t,.7f,1.05f));break;
                default:fx.Flash=.3f*Bell(t,0,.45f);break;
            }
            return fx;
        }
        private static CardFx FinishFx(in Act a)
        {
            var fx=Still;float t=a.Age;fx.Flash=(a.Calm?.25f:.55f)*Mathf.Exp(-t*7)*Seg(t,0,.02f);if(a.Calm)return fx;
            float pop=Mathf.Exp(-t*6)*Mathf.Cos(t*18);fx.Sx=fx.Sy=1+.03f*pop;
            if(a.Move==Plan.Move.TopDog){fx.Sy=1-.08f*pop;fx.Sx=1+.05f*pop;}
            if(a.Move==Plan.Move.Greatsword||a.Move==Plan.Move.DevGreatsword||a.Move==Plan.Move.Bombadier)fx.Shift=Shake(t,4*Mathf.Exp(-t*8));
            return fx;
        }
        // Band `b` of a card drawn in bands: its clip and its offset.
        private static void Band(Rect r,in CardFx fx,int b,out Rect clip,out Vector2 off)
        {
            if(fx.Split>0)
            {
                float y=r.y+r.height*fx.Cut;
                if(b==0){clip=new Rect(r.x-40,r.y-40,r.width+80,y-r.y+40);off=new Vector2(-fx.Split,-fx.Split*.3f);}
                else{clip=new Rect(r.x-40,y,r.width+80,r.yMax-y+40);off=new Vector2(fx.Split,fx.Split*.3f);}
                return;
            }
            float h=(r.height+60)/fx.Bands,y0=r.y-30+b*h;clip=new Rect(r.x-40,y0,r.width+80,h+.5f);
            int step=(int)(Time.unscaledTime*24);off=new Vector2(Sgn(b*7+step,3)*14*fx.Glitch*(Rnd(b+step,5)>.45f?1:.15f),0);
        }

        // ---- gentle (Reduced effects) and the lock-in finisher -----------------------------------------------------------
        // A soft ring and a slow glow from the icon; nothing moves.
        private static void Gentle(float t,bool locked)
        {
            float a=Bell(t,0,Plan.Gentle);Dot(fc,fslot*2.4f,U.A(fa,.22f*a));Thin(fc,fslot*(.7f+.5f*Seg(t,0,Plan.Gentle)),U.A(fh,.6f*a));
            if(locked)Thin(fc,fslot*(.9f+.4f*Seg(t,0,Plan.Gentle)),U.A(fa,.4f*a));
        }
    }
}
