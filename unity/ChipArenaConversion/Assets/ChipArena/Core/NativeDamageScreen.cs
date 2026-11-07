using System;

namespace ChipArena.UnityConversion
{
    // The local pilot's damage screen (owner 2026-10-10: "give players a screen effect when damaged, make it a good screen
    // effect"): pure rules that turn the authoritative events aimed at the local pilot, its HP and the view camera into the
    // uniforms of one full-screen pass (Resources/NativeDamageScreen.shader, drawn by Runtime/NativeDamageScreenPresentation).
    // The source client has none of this (it plays the `hurt` cue and the .12 camera shake), so these are new rules. Visual
    // only: nothing here changes the match, and nothing allocates after construction.
    //  * Hit / Headshot: a smoky crimson "wound bloom" (four slots) that drains the frame to red glass at the edge facing the
    //    attacker. Strength, reach and life scale with the damage as a fraction of max HP; the bloom overshoots, holds, then
    //    dissolves back into the edge. Close hits on the same side merge. Each slot keeps its attacker and is re-aimed every
    //    frame (smoothed, so the smoke slides round the edge as either pilot moves).
    //  * The threat ring round the crosshair: six per-attacker arcs (segments, keyline, outward chevron, a white core that
    //    snaps in at most every .34 s), re-aimed every frame at the attacker's bearing; carets for an attacker above or
    //    below; twin rails, a hollow diamond and a reticle mark for a headshot; an expanding shock arc for a melee hit; a
    //    dashed ring (never a bearing) for hazards and hits with no source.
    //  * Each hit also kicks a trauma (brief colour drain) and an impact flash (radial chromatic split, exposure dip), one
    //    flash at most every .34 s (under 3 a second); a headshot's flash is paler at the struck edge and rings out longer,
    //    still one peak.
    //  * BurnTick / PoisonTick feed element fields (fire licks up from the bottom, poison hangs and drips from the top); they
    //    never make blooms or arcs. Heal: a cool lift, and a wave of light for a real heal.
    //  * Low HP: a churning edge pooled in the corners with a lub-dub heartbeat that quickens near death (at most 82 bpm, so
    //    lub and dub together stay under 3 swells a second, each about 130-170 ms wide), and a colour drain.
    //  * Death: no new events; everything fades over .5 s with a grey beat.
    // Reduced effects: no chromatic split, no heartbeat motion (a still pulse), no noise drift, no arc snap-in or shock motion.
    public sealed class NativeDamageScreen
    {
        public const int Slots=4,Arcs=6;
        // Severity from the damage fraction f: s = clamp01((f - .02) / .38) ^ .7 (a 9% pistol hit ~.31, 30% ~.81, 40%+ = 1),
        // at least .12 for a hit and .35 for a headshot; a melee hit runs 1.2x deeper (capped at 1).
        public const float SevFloor=.02f,SevFull=.40f,SevGamma=.7f,HitFloor=.12f,HeadFloor=.35f,MeleeBoost=1.2f,Fresh=.5f;
        // Bloom envelope (seconds): attack, hold (+ by severity), decay (+ by severity).
        public const float Attack=.07f,HoldBase=.05f,HoldSev=.15f,DecayBase=.38f,DecaySev=.72f;
        // Reach inward from the edge, in screen heights: .05 + .14 s, overshooting 1.10x at the end of the attack.
        public const float ReachBase=.05f,ReachSev=.14f,Overshoot=1.10f;
        // Lobe half-width in radians (the angle is measured in the screen square, so every aspect reads the same); x1.2
        // for an attacker behind, at most 95 degrees.
        public const float WidthBase=.9f,WidthSev=.75f,WidthBehind=1.2f,WidthMax=1.658f;
        // Same-side merge window; bloom re-aim time constant.
        public const float MergeCos=.82f,MergeWindow=.30f,AimTau=.08f;
        public const float LowStart=.38f,LowFull=.08f,DeathStart=.15f,DeathFull=.04f,LagUp=.35f,LagDown=.06f;
        // Heartbeat: 58 bpm as the low state starts, 82 near death. Lub and dub both count as swells, so 82 bpm is 2.7 a second.
        public const float BpmCalm=58,BpmPanic=82;
        public const float HealWaveTime=.6f,HealBig=.08f;
        public const float TickAdd=.22f,FireHold=.7f,PoisonHold=.8f;
        // Photosensitivity: every flash peak (the impact flash, a headshot's pale flash, an attacker's white core, the headshot
        // reticle mark) at most every .34 s, under 3 a second, however hard or fast the hits land; a hit inside the gap shows
        // through its bloom, arc and trauma only. Showing fades in over .08 s; death fades everything out over .5 s.
        public const float FlashGap=.34f,ShowIn=.08f,DeathFade=.5f;
        // Threat ring radius in screen heights: first person, and third person (larger, so the bottom arcs clear the pilot).
        public const float RingFirst=.145f,RingThird=.19f;
        // Arc envelope (s): attack, hold (+ by severity), fade (+ by severity); white core 80 ms (140 for a headshot); snap-in
        // from 14 u outside over 120 ms (u = H / 1080 px).
        public const float ArcAttack=.04f,ArcHold=.12f,ArcHoldSev=.20f,ArcFade=.90f,ArcFadeSev=.60f,ArcFlash=.08f,ArcHeadFlash=.14f,Snap=.12f,SnapDistance=14;
        // A repeat hit from the same attacker: severity decays to .85 of itself and adds .35 of the new hit.
        public const float Keep=.85f,Stack=.35f;
        // Arc shape in u: half width 11 + 17 s degrees (+4 for melee), core 3 + 4 s, outward glow 8 + 14 s.
        public const float HalfWidth=11,HalfWidthSev=17,MeleeWidth=4,CoreWidth=3,CoreWidthSev=4,Glow=8,GlowSev=14;
        // Elevation carets at 20 and 45 degrees; melee shock and headshot mark lifetimes.
        public const float ElevationOne=20,ElevationTwo=45,Shock=.22f,Mark=.26f;
        // The arc key for hits with no bearing (stage hazards, unknown sources): one shared dashed ring.
        public const int OmniKey=int.MinValue;

        // A wound bloom: its smoothed screen direction (X, Y, Weight), the world direction from the local pilot to its source
        // (From, re-aimed every frame for a keyed source) with the weight scale it carries (1 for an attacker, .8 for a floor
        // hazard, 0 for no direction), severity, start, headshot.
        public struct Slot{public float X,Y,Weight,Scale,Sev,At,Head;public V3 From;public int Key;public bool Live;}
        // A threat arc: one per attacker (Key), its last world direction, the last hit, the last white-core flash.
        public struct Arc{public float At,FlashAt,Sev;public V3 From;public int Key;public bool Live,Melee,Head,HeadFlash,Omni;}
        public readonly Slot[] Bloom=new Slot[Slots];
        public readonly Arc[] Ring=new Arc[Arcs];
        // Uniform-ready outputs (Step fills them; NativeDamageScreen.shader's order):
        //  A[4]: dirX, dirY, dir weight, amount   B[4]: reach (screen heights), cos half-width, dissolve, headshot
        //  ArcA[6]: bearing (rad, 0 up, + clockwise), strength, half width (rad), core (u)
        //  ArcB[6]: snap offset (u), white core 0..1, kind (0 hit, 1 headshot, 4 omni; +8 melee), carets -2..2
        //  ArcC[6]: glow (u), severity, 0, 0
        //  Global (flash, chroma, desaturation, exposure dip); Low (low, near death, pulse, clock); Element (fire, poison,
        //  heal, heal wave 0..1 or 0); Ticks (fire tick, poison tick, headshot flash); Track (ring track 0..1); ShockAge /
        //  MarkAge (s, -1 when none); ShockAngle; RingFade (death fade x show fade).
        public readonly float[] A=new float[Slots*4],B=new float[Slots*4],ArcA=new float[Arcs*4],ArcB=new float[Arcs*4],ArcC=new float[Arcs*4];
        public float Flash,Chroma,Desat,Dip,Low,Death,Pulse,Clock,Fire,Poison,Heal,HealWave,FireTick,PoisonTick,HeadFlash,Track,ShockAge=-1,ShockAngle,MarkAge=-1,RingFade,Fade=1,Shown;
        public bool Active;
        private float fire,poison,heal,trauma,flashAt=-10,flashAmp,fireAt=-10,poisonAt=-10,healAt=-10,shockAt=-10,markAt=-10,shown=1,phase,lastNow=-1;
        private V3 shockFrom,right=new V3(1,0,0),up=new V3(0,1,0),forward=new V3(0,0,1);private int shockKey;
        private bool alive=true,flashHead;

        public void Clear()
        {
            for(int i=0;i<Slots;i++)Bloom[i]=default;for(int i=0;i<Arcs;i++)Ring[i]=default;
            Array.Clear(A,0,A.Length);Array.Clear(B,0,B.Length);Array.Clear(ArcA,0,ArcA.Length);Array.Clear(ArcB,0,ArcB.Length);Array.Clear(ArcC,0,ArcC.Length);
            fire=poison=heal=trauma=0;flashAt=fireAt=poisonAt=healAt=shockAt=markAt=-10;flashAmp=0;flashHead=false;shown=1;phase=0;lastNow=-1;alive=true;shockKey=0;shockFrom=default;
            Flash=Chroma=Desat=Dip=Low=Death=Pulse=Clock=Fire=Poison=Heal=HealWave=FireTick=PoisonTick=HeadFlash=Track=ShockAngle=RingFade=Shown=0;ShockAge=MarkAge=-1;Fade=1;Active=false;
        }
        public static float Severity(float fraction)
        {
            if(!(fraction>0))return 0;float x=Clamp01((fraction-SevFloor)/(SevFull-SevFloor));return (float)Math.Pow(x,SevGamma);
        }
        // The severity a counted hit draws with: the floor for its kind, and the melee boost.
        public static float HitSeverity(float fraction,bool head,bool melee)
        {
            float s=Math.Max(Severity(fraction),head?HeadFloor:HitFloor);return melee?Math.Min(1,s*MeleeBoost):s;
        }
        // The screen-edge direction of a source from the local pilot. (cx, cy, cz): the pilot-to-source vector in camera axes
        // (right, up, forward), any length. Left/right read as is, up as up; behind pushes the direction down, so an attacker
        // behind-left blooms bottom-left and one straight behind at the bottom. An attacker near the aim line is weakly
        // directional: weight goes 0 -> 1 as the off-axis length runs .12 -> .40 (straight ahead is omni).
        public static void Direction(float cx,float cy,float cz,out float x,out float y,out float weight)
        {
            float len=(float)Math.Sqrt(cx*cx+cy*cy+cz*cz);if(!(len>1e-4f)){x=0;y=-1;weight=0;return;}
            cx/=len;cy/=len;cz/=len;x=cx;y=cy-Math.Max(0,-cz);float m=(float)Math.Sqrt(x*x+y*y);
            weight=Smooth(.12f,.40f,m);if(m>1e-4f){x/=m;y/=m;}else{x=0;y=-1;}
        }
        // The ring bearing of a world direction for a view (radians): 0 ahead (top of the ring), +pi/2 right, +-pi behind
        // (bottom). Measured in the ground plane of the camera's heading, so pitch and roll never swing it.
        public static float Bearing(V3 d,V3 right,V3 forward,V3 up)
        {
            float fx=forward.x,fz=forward.z,l=(float)Math.Sqrt(fx*fx+fz*fz);
            if(l<1e-3f){fx=up.x;fz=up.z;l=(float)Math.Sqrt(fx*fx+fz*fz);if(l<1e-3f){fx=0;fz=1;l=1;}}
            fx/=l;fz/=l;float ahead=d.x*fx+d.z*fz,side=d.x*fz-d.z*fx;
            if(Math.Abs(ahead)<1e-6f&&Math.Abs(side)<1e-6f)return 0;
            float r=(float)Math.Atan2(side,ahead);return right.x*fz-right.z*fx<0?-r:r;
        }
        // Elevation carets: +-1 at 20 degrees, +-2 at 45 (positive above).
        public static int Carets(V3 d)
        {
            double h=Math.Sqrt(d.x*d.x+d.z*d.z),deg=Math.Atan2(d.y,Math.Max(1e-4,h))*180/Math.PI,a=Math.Abs(deg);
            int n=a>=ElevationTwo?2:a>=ElevationOne?1:0;return deg<0?-n:n;
        }

        // A Hit or Headshot on the local pilot. fraction: damage / max HP. key: the attacker (pilot seat or minion key) whose
        // world direction `from` (local pilot to source, any length) is followed (Aim); 0: a fixed direction (a floor hazard)
        // that blooms with `weight` but draws the dashed ring; weight 0: no direction at all (omni bloom, dashed ring).
        public void Hit(float now,float fraction,bool head,bool melee,int key,V3 from,float weight)
        {
            if(!alive||!(fraction>0))return;float s=HitSeverity(fraction,head,melee);
            if(float.IsNaN(from.x)||float.IsNaN(from.y)||float.IsNaN(from.z)||float.IsInfinity(from.x)||float.IsInfinity(from.y)||float.IsInfinity(from.z)){from=default;weight=0;}
            weight=float.IsNaN(weight)?0:Clamp01(weight);if(weight<=0)key=0;
            Screen(from,weight,out float x,out float y,out float w);
            int best=-1;
            for(int i=0;i<Slots;i++)
            {
                var b=Bloom[i];if(!b.Live||now-b.At>MergeWindow+Attack)continue;
                bool omni=w<.5f&&b.Weight<.5f,same=w>=.5f&&b.Weight>=.5f&&x*b.X+y*b.Y>MergeCos;
                if(omni||same){best=i;break;}
            }
            if(best>=0)
            {
                var b=Bloom[best];float total=b.Sev+s,k=s/Math.Max(1e-4f,total);
                float nx=b.X+(x-b.X)*k,ny=b.Y+(y-b.Y)*k,nl=(float)Math.Sqrt(nx*nx+ny*ny);if(nl>1e-4f){nx/=nl;ny/=nl;}else{nx=x;ny=y;}
                // The merged slot follows the stronger of its sources.
                if(s>=b.Sev){b.Key=key;b.From=from;b.Scale=weight;}
                b.X=nx;b.Y=ny;b.Weight=b.Weight+(w-b.Weight)*k;b.Sev=1-(1-b.Sev)*(1-.8f*s);
                // A merged hit re-pulses from half way up the attack instead of restarting from nothing.
                b.At=now-Attack*.5f;b.Head=Math.Max(b.Head,head?1:0);Bloom[best]=b;
            }
            else
            {
                int slot=-1;float weakest=float.MaxValue;
                for(int i=0;i<Slots;i++){if(!Bloom[i].Live){slot=i;break;}float a=Amount(Bloom[i],now);if(a<weakest){weakest=a;slot=i;}}
                Bloom[slot]=new Slot{X=x,Y=y,Weight=w,Scale=weight,Sev=s,At=now,Head=head?1:0,From=from,Key=key,Live=true};
            }
            ArcHit(now,s,head,melee,key!=0&&weight>0?key:OmniKey,from);
            trauma=Math.Min(1,trauma+.5f*s+(head?.25f:0));
            // One flash peak per FlashGap, headshot or not: no stronger-hit bypass and no second beat.
            if(now-flashAt>=FlashGap){flashAmp=.5f+.5f*s;flashAt=now;flashHead=head;}
        }
        private void ArcHit(float now,float s,bool head,bool melee,int key,V3 from)
        {
            int k=-1;for(int n=0;n<Arcs;n++)if(Ring[n].Live&&Ring[n].Key==key){k=n;break;}
            bool same=k>=0;
            if(!same)for(int n=0;n<Arcs;n++)if(!Ring[n].Live){k=n;break;}
            if(k<0){float weakest=float.MaxValue;for(int n=0;n<Arcs;n++){float w=ArcEnvelope(now-Ring[n].At,Ring[n].Sev)*(.5f+Ring[n].Sev);if(w<weakest){weakest=w;k=n;}}}
            var a=Ring[k];
            if(same)
            {
                // Continue from the level the arc shows now (no dip to zero on every bullet), and flash the core at most at 4 Hz.
                float e0=Math.Min(1,ArcEnvelope(now-a.At,a.Sev));a.At=now-ArcAttack*(1-(float)Math.Sqrt(1-e0));
                a.Sev=Math.Min(1,Math.Max(Keep*a.Sev,s)+Stack*s);if(now-a.FlashAt>=FlashGap){a.FlashAt=now;a.HeadFlash=head;}
            }
            else a=new Arc{At=now,FlashAt=now,Sev=s,HeadFlash=head};
            a.Live=true;a.Key=key;a.Melee=melee;a.Head=head;a.Omni=key==OmniKey;if(!a.Omni)a.From=from;Ring[k]=a;
            if(melee&&!a.Omni){shockAt=now;shockKey=key;shockFrom=from;}
            if(head&&now-markAt>=FlashGap)markAt=now;
        }
        // The attacker `key` is now at `from` (local pilot to attacker): every bloom, arc and shock that follows it re-aims.
        // A gone attacker is simply not aimed again and keeps its last world direction.
        public void Aim(int key,V3 from)
        {
            if(key==0||key==OmniKey||float.IsNaN(from.x)||float.IsNaN(from.y)||float.IsNaN(from.z))return;
            for(int i=0;i<Slots;i++)if(Bloom[i].Live&&Bloom[i].Key==key)Bloom[i].From=from;
            for(int i=0;i<Arcs;i++)if(Ring[i].Live&&Ring[i].Key==key)Ring[i].From=from;
            if(shockKey==key)shockFrom=from;
        }
        // The distinct attacker keys the live slots follow, written into `into` (no allocation); returns the count.
        public int Keys(int[] into)
        {
            int n=0;
            for(int i=0;i<Slots+Arcs;i++)
            {
                int key=i<Slots?(Bloom[i].Live?Bloom[i].Key:0):(Ring[i-Slots].Live?Ring[i-Slots].Key:0);if(key==0||key==OmniKey)continue;
                bool seen=false;for(int j=0;j<n;j++)if(into[j]==key)seen=true;if(!seen&&n<into.Length)into[n++]=key;
            }
            return n;
        }
        // A BurnTick (fire) or PoisonTick on the local pilot: element fields only, never a bloom or an arc.
        public void Tick(float now,float fraction,bool burn)
        {
            if(!alive)return;float add=TickAdd+3*Math.Max(0,float.IsNaN(fraction)?0:Math.Min(1,fraction));
            if(burn){fire=Math.Min(1,fire+add);fireAt=now;}else{poison=Math.Min(1,poison+add);poisonAt=now;}
        }
        // A Heal on the local pilot (lifesteal and regen included).
        public void Healed(float now,float fraction)
        {
            if(!alive||!(fraction>0))return;fraction=Math.Min(1,fraction);
            if(fraction>=HealBig){heal=Math.Min(1,heal+.5f+2*fraction);if(now-healAt>HealWaveTime*.8f)healAt=now;}
            else heal=Math.Min(1,heal+.15f+4*fraction);
        }
        // Once per rendered frame. now: the match clock events are stamped with; dt: unscaled frame time; hp: HP / max HP;
        // split: the chromatic split is allowed (Full and not Reduced effects); (right, up, forward): the view camera's axes.
        public void Step(float now,float dt,float hp,bool living,bool reduced,bool split,V3 right,V3 up,V3 forward)
        {
            dt=float.IsNaN(dt)?0:Math.Max(0,Math.Min(.1f,dt));this.right=right;this.up=up;this.forward=forward;
            if(lastNow>=0&&now<lastNow-1e-3f){for(int i=0;i<Slots;i++)Bloom[i].Live=false;for(int i=0;i<Arcs;i++)Ring[i].Live=false;flashAt=fireAt=poisonAt=healAt=shockAt=markAt=-10;}lastNow=now;
            alive=living;Fade=living?Math.Min(1,Fade+dt/.25f):Math.Max(0,Fade-dt/DeathFade);Shown=Math.Min(1,Shown+dt/ShowIn);float vis=Fade*Shown;
            hp=Clamp01(float.IsNaN(hp)?1:hp);if(!living)hp=Math.Min(shown,hp);
            shown=hp<shown?shown+(hp-shown)*(1-(float)Math.Exp(-dt/LagDown)):shown+(hp-shown)*(1-(float)Math.Exp(-dt/LagUp));
            Low=Smooth(LowStart,LowFull,shown)*vis;Death=Smooth(DeathStart,DeathFull,shown)*vis;
            float bpm=BpmCalm+(BpmPanic-BpmCalm)*Math.Max(Low*.55f,Death);phase+=dt*bpm/60;phase-=(float)Math.Floor(phase);
            Pulse=reduced?.35f:LubDub(phase);if(!reduced){Clock+=dt;if(Clock>512)Clock-=512;}
            trauma=Math.Max(0,trauma-dt*.8f);
            float sinceFlash=now-flashAt;
            Flash=Math.Min(1,FlashCurve(sinceFlash,flashHead)*flashAmp)*vis;
            HeadFlash=flashHead?Math.Min(1,FlashCurve(sinceFlash,true))*vis:0;
            Chroma=split?Math.Min(1,Flash*(.45f+.55f*trauma)*(flashHead&&sinceFlash<.3f?1.5f:1)+.18f*Death):0;
            // Dying: a grey beat that fades out with the rest.
            Desat=Math.Min(.75f,(.35f*trauma+(living?0:.6f))*vis+.5f*Low+.15f*Death);Dip=.10f*Math.Min(1,Flash);
            // Element fields hold while ticks keep coming (a tick every .25 s settles near .5-.75) and fade within ~2 s after.
            fire=Math.Max(0,fire*(float)Math.Exp(-dt/FireHold)-dt*.05f);poison=Math.Max(0,poison*(float)Math.Exp(-dt/PoisonHold)-dt*.05f);heal=Math.Max(0,heal-dt/.9f);
            Fire=fire*vis;Poison=poison*vis;Heal=heal*vis;
            FireTick=now-fireAt>=0?(float)Math.Exp(-(now-fireAt)/.08f)*vis:0;PoisonTick=now-poisonAt>=0?(float)Math.Exp(-(now-poisonAt)/.12f)*vis:0;
            float w=(now-healAt)/HealWaveTime;HealWave=w>0&&w<1&&living&&vis>0?w:0;
            bool any=HeadFlash>.002f||Flash>.002f||trauma*vis>.002f||Low>.001f||Fire>.002f||Poison>.002f||Heal>.002f||HealWave>0||Desat>.002f;
            float aim=1-(float)Math.Exp(-dt/AimTau);
            for(int i=0;i<Slots;i++)
            {
                var b=Bloom[i];int o=i*4;
                if(b.Live&&(now-b.At>Life(b.Sev)||Fade<=0)){b.Live=false;Bloom[i]=b;}
                float amount=b.Live?Amount(b,now)*vis:0;
                if(b.Live&&b.Scale>0)
                {
                    // Re-aim toward the source (tau 80 ms): the bearing turns the short way round the edge, never through the middle.
                    Screen(b.From,b.Scale,out float tx,out float ty,out float tw);
                    float a0=(float)Math.Atan2(b.Y,b.X),a1=(float)Math.Atan2(ty,tx),da=a1-a0;
                    while(da>Math.PI)da-=(float)(2*Math.PI);while(da<-Math.PI)da+=(float)(2*Math.PI);
                    float an=a0+da*aim;b.X=(float)Math.Cos(an);b.Y=(float)Math.Sin(an);b.Weight+=(tw-b.Weight)*aim;Bloom[i]=b;
                }
                if(amount<=0){A[o+3]=0;continue;}any=true;
                float age=now-b.At,hold=HoldBase+HoldSev*b.Sev,decay=DecayBase+DecaySev*b.Sev,reach=ReachBase+ReachSev*b.Sev,k;
                if(age<Attack){k=EaseOut(Clamp01(age/Attack));reach*=.55f+(Overshoot-.55f)*k;}
                else if(age<Attack+hold){k=(age-Attack)/hold;reach*=Overshoot+(1-Overshoot)*k;}
                else{k=Clamp01((age-Attack-hold)/decay);reach*=1-.45f*k;}
                float dissolve=age<Attack+hold?0:.9f*(float)Math.Pow(Clamp01((age-Attack-hold)/decay),1.2f);
                float width=Math.Min(WidthMax,(WidthBase+WidthSev*b.Sev)*(b.Y<-.5f&&b.Weight>.5f?WidthBehind:1));
                A[o]=b.X;A[o+1]=b.Y;A[o+2]=b.Weight;A[o+3]=amount;B[o]=reach;B[o+1]=(float)Math.Cos(width);B[o+2]=dissolve;B[o+3]=b.Head;
            }
            // The threat ring: every arc re-aimed at its attacker's bearing in this frame.
            float track=0;
            for(int i=0;i<Arcs;i++)
            {
                var a=Ring[i];int o=i*4;float age=now-a.At;
                if(a.Live&&(age>ArcLife(a.Sev)||Fade<=0)){a.Live=false;Ring[i]=a;}
                float env=a.Live?ArcEnvelope(age,a.Sev):0;
                if(env<=.001f){ArcA[o+1]=0;continue;}any=true;
                float strength=env*(.62f+.38f*a.Sev)*vis;track=Math.Max(track,strength);
                ArcA[o]=a.Omni?0:Bearing(a.From,right,forward,up);ArcA[o+1]=strength;ArcA[o+2]=HalfWidthDeg(a.Sev,a.Melee)*(float)Math.PI/180;ArcA[o+3]=CoreWidth+CoreWidthSev*a.Sev;
                ArcB[o]=reduced?0:SnapOffset(now-a.FlashAt);ArcB[o+1]=FlashLevel(now-a.FlashAt,a.HeadFlash);ArcB[o+2]=(a.Omni?4:a.Head?1:0)+(a.Melee?8:0);ArcB[o+3]=a.Omni?0:Carets(a.From);
                ArcC[o]=Glow+GlowSev*a.Sev;ArcC[o+1]=a.Sev;ArcC[o+2]=0;ArcC[o+3]=0;
            }
            Track=(float)Math.Pow(track,1.5);RingFade=vis;
            float shock=now-shockAt,mark=now-markAt;
            ShockAge=shock>=0&&shock<Shock&&vis>0?shock:-1;ShockAngle=ShockAge>=0?Bearing(shockFrom,right,forward,up):0;
            MarkAge=mark>=0&&mark<Mark&&vis>0?mark:-1;
            Active=any||ShockAge>=0||MarkAge>=0;
        }
        // The screen-edge direction of a world direction for the current view, its weight scaled by `scale`.
        private void Screen(V3 from,float scale,out float x,out float y,out float w)
        {
            if(scale<=0){x=0;y=-1;w=0;return;}
            Direction(V3.Dot(from,right),V3.Dot(from,up),V3.Dot(from,forward),out x,out y,out w);w*=scale;
        }
        public static float Life(float sev)=>Attack+HoldBase+HoldSev*sev+DecayBase+DecaySev*sev;
        // Opacity of a bloom: an ease-out attack, a hold, then a (1-k)^1.6 decay; peak .55 + .45 s.
        public static float Amount(in Slot b,float now)
        {
            float age=now-b.At;if(age<0||!b.Live)return 0;float hold=HoldBase+HoldSev*b.Sev,decay=DecayBase+DecaySev*b.Sev,peak=.55f+.45f*b.Sev;
            if(age<Attack)return peak*EaseOut(age/Attack);if(age<Attack+hold)return peak;float k=(age-Attack-hold)/decay;return k>=1?0:peak*(float)Math.Pow(1-k,1.6);
        }
        public static float ArcLife(float sev)=>ArcAttack+ArcHold+ArcHoldSev*sev+ArcFade+ArcFadeSev*sev;
        // Arc envelope 0..1 at `age` after the last hit: an ease-out attack, a hold, then (1-u)^1.6.
        public static float ArcEnvelope(float age,float sev)
        {
            if(age<0)return 0;if(age<ArcAttack){float a=age/ArcAttack;return 1-(1-a)*(1-a);}
            float hold=ArcHold+ArcHoldSev*sev;if(age<ArcAttack+hold)return 1;
            float u=(age-ArcAttack-hold)/(ArcFade+ArcFadeSev*sev);return u>=1?0:(float)Math.Pow(1-u,1.6);
        }
        public static float SnapOffset(float age)=>age>=Snap||age<0?0:SnapDistance*(float)Math.Pow(1-age/Snap,3);
        public static float FlashLevel(float age,bool head)=>age<0?0:Math.Max(0,1-age/(head?ArcHeadFlash:ArcFlash));
        public static float HalfWidthDeg(float sev,bool melee)=>HalfWidth+HalfWidthSev*sev+(melee?MeleeWidth:0);
        // The impact flash: an exponential fall, .06 s (a headshot rings out over .11 s); one peak either way.
        public static float FlashCurve(float age,bool head)=>age<0?0:(float)Math.Exp(-age/(head?.11f:.06f));
        // A heartbeat that swells rather than blinks: the lub at the start of the beat and a softer dub .3 of a beat later,
        // Gaussians in the beat phase (periodic, so the wrap never steps) about 128 and 146 ms wide at half height at 82 bpm
        // (wider when calmer).
        public static float LubDub(float x)
        {
            float a=x-(float)Math.Floor(x+.5f),b=x-.3f;b-=(float)Math.Floor(b+.5f);a/=.105f;b/=.12f;
            return Math.Max((float)Math.Exp(-a*a),.55f*(float)Math.Exp(-b*b));
        }
        // Whether the event list rewound, so the screen must start over: a new arena generation, a clock that ran backwards,
        // or a listed event id lower than the last one consumed. An emptied list (the host prunes events after 1 s, and a
        // guest copies that list) is a lull, not a rewind: blooms, arcs, the low-HP state and element fields keep running.
        public static bool Rewound(int generation,int lastGeneration,float clock,float lastClock,int newest,int lastEvent)
            =>generation!=lastGeneration||clock<lastClock-1e-3f||(newest>0&&newest<lastEvent);

        // The health box's lag chip (the overhead HealthLag rule): the lost part stays for .22 s after the last loss, then
        // drains at .9 of max HP per second; a heal, a new round or a clock jump snaps it. Returns the lagged ratio.
        public struct Lag{public float Value,Last,Time,Hold;public int Owner;}
        public const float LagHold=.22f,LagDrain=.9f;
        public static float LagStep(ref Lag lag,int owner,float time,float ratio)
        {
            ratio=Clamp01(float.IsNaN(ratio)?0:ratio);float dt=time-lag.Time;lag.Time=time;
            if(owner!=lag.Owner||dt<0||dt>.35f||ratio>=lag.Value){lag.Owner=owner;lag.Value=ratio;lag.Hold=0;}
            else{lag.Hold=ratio<lag.Last?0:lag.Hold+Math.Min(dt,.05f);if(lag.Hold>LagHold)lag.Value=Math.Max(ratio,lag.Value-Math.Min(dt,.05f)*LagDrain);}
            lag.Last=ratio;return lag.Value;
        }

        // The shader's baked noise (_DmgNoise, NoiseSize^2 RGBA8, linear, repeat, bilinear, no mips): tileable value noise so the
        // pass samples four smooth fields instead of hashing lattice points per pixel. R: 3-octave fbm, base period 8 cells (the
        // broad shape, the low-HP churn and the flame tongues); G: fbm, base period 16 (bloom smoke); B: one octave, period 32
        // (grain, flame detail, headshot streaks); A: one octave, period 64 (fine headshot streaks). Deterministic integer
        // hash, so the render harness rebuilds the same texels.
        public const int NoiseSize=256;
        public static byte[] NoiseTexels()
        {
            int n=NoiseSize;var o=new byte[n*n*4];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                int i=(y*n+x)*4;
                o[i]=Byte(Fbm(x,y,8,1));o[i+1]=Byte(Fbm(x,y,16,2));o[i+2]=Byte(Value(x,y,32,3));o[i+3]=Byte(Value(x,y,64,4));
            }
            return o;
        }
        private static byte Byte(float v)=>(byte)Math.Max(0,Math.Min(255,(int)Math.Round(v*255)));
        private static float Fbm(int x,int y,int period,uint seed)=>(.5f*Value(x,y,period,seed)+.25f*Value(x,y,period*2,seed+10)+.125f*Value(x,y,period*4,seed+20))/.875f;
        // Periodic value noise with `period` cells across the texture (smoothstep between lattice values).
        public static float Value(int x,int y,int period,uint seed)
        {
            float fx=(x+.5f)*period/NoiseSize,fy=(y+.5f)*period/NoiseSize;int ix=(int)Math.Floor(fx),iy=(int)Math.Floor(fy);float u=fx-ix,v=fy-iy;
            u=u*u*(3-2*u);v=v*v*(3-2*v);int x0=ix%period,y0=iy%period,x1=(ix+1)%period,y1=(iy+1)%period;
            float a=Lattice(x0,y0,seed),b=Lattice(x1,y0,seed),c=Lattice(x0,y1,seed),d=Lattice(x1,y1,seed);
            return a+(b-a)*u+(c-a)*v+(a-b-c+d)*u*v;
        }
        public static float Lattice(int x,int y,uint seed)
        {
            unchecked{uint h=(uint)x*0x8da6b343u^(uint)y*0xd8163841u^seed*0xcb1ab31fu;h^=h>>13;h*=0x5bd1e995u;h^=h>>15;return (h&0xffffff)/16777215f;}
        }
        public static float Clamp01(float x)=>x<0?0:x>1?1:x;
        public static float EaseOut(float x){x=Clamp01(x);float u=1-x;return 1-u*u*u;}
        // smoothstep(e0, e1, x) for either edge order (e0 > e1 falls as x rises).
        public static float Smooth(float e0,float e1,float x){float t=Clamp01((x-e0)/(e1-e0));return t*t*(3-2*t);}
    }
}
