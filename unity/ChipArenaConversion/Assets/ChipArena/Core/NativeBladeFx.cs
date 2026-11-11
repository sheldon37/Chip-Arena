using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // ---- the blade classes' effects (docs/KATANA.md, docs/FANGS.md "The look"; owner 2026-11-10: "use really cool effects") ----
    // In the pilot's weapon colour. The Katana: a crescent trail swept from the drawn blade through every cut (wider and brighter on the
    // finisher), a hairline slash, a flash and sparks at each cut; the iai's draw flash, the line of light it leaves hanging in the air, the
    // hairline on each one it marked, the split (the line flaring and breaking into motes and petals, a great slash across each marked one)
    // and the click's ring at the hip; the storm's whirl of petals and wind arcs, its launch streaks; the counter's moon behind the pair. The
    // Fangs: twin trails from both drawn blades, venom splashing from each cut and dripping from the poisoned, the shadow step's smoke at both
    // ends, the veil's billow (and a thin smoke at the feet while it lasts, for the wielder and allies), the knives' flight reconstructed from
    // the throw, the bloom's green burst on each poisoned one, the takedown's dark flash. Each chip's moment too. Presentation state: only the
    // trails remember past frames; the rest is a pure function of the record and the clock.
    public sealed partial class NativeTechniqueFx
    {
        private static readonly Vector3 BladeWhite=new Vector3(1,1,1),BladeVenomDark=new Vector3(.05f,.12f,.03f),SmokeGrey=new Vector3(.16f,.17f,.19f),ShadowSmoke=new Vector3(.03f,.035f,.033f),Petal=new Vector3(1,.55f,.68f);
        private static Vector3 BladeTint(IKitFxScene scene,Actor a,Vector3 fallback){var c=WeaponPaint.Linear(scene.WeaponColor(a));float m=Math.Max(c.X,Math.Max(c.Y,c.Z));return m>1e-3f?c/m:fallback;}
        private sealed class BladeTrail
        {
            public const int Size=32;
            public readonly Vector3[] Root=new Vector3[Size],Tip=new Vector3[Size],Body=new Vector3[Size];public readonly float[] At=new float[Size],Power=new float[Size];public readonly bool[] Start=new bool[Size];
            public int Count,Head;public float Last=-100;
            /// <summary>The ring index `back` samples before the newest.</summary>
            public int Index(int back)=>((Head-back)%Size+Size)%Size;
        }
        private readonly Dictionary<long,BladeTrail> bladeTrails=new Dictionary<long,BladeTrail>();
        private float bladeTrailClock=-1;
        /// <summary>Draws every blade class's moves, hits and lingering effects of the frame at `time`.</summary>
        public void DrawBlades(ITechniqueFxSink s,IKitFxScene scene,float time,float q,bool low)
        {
            if(s==null||scene?.Actors==null)return;var actors=scene.Actors;
            if(time<bladeTrailClock-.5f)bladeTrails.Clear();bladeTrailClock=time;
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];var k=NativeBladeView.Of(a);if(k==null)continue;long seed=a.Id*7919L;bool katana=a.SourceSpec==NativeKatana.Spec;
                var tint=BladeTint(scene,a,katana?new Vector3(1,.24f,.43f):new Vector3(.49f,1,.23f));
                BladeTrails(s,scene,a,k,time,tint,katana,seed+11,q,low);
                if(katana){KatanaLine(s,a,k,time,tint,seed,q,low);KatanaStorm(s,scene,a,k,time,tint,seed+3,q,low);}
                else{FangsVeil(s,scene,a,k,time,tint,seed+5,q,low);FangsCloud(s,k,time,tint,seed+9,q,low);}
                if(k.Hits!=null)foreach(var h in k.Hits)if(h!=null){if(katana)KatanaHitFx(s,k,h,time,tint,seed+h.Id*37L,q,low);else FangsHitFx(s,scene,a,k,h,time,tint,seed+h.Id*41L,q,low);}
            }
        }
        // A move's live stretch for its trail, and the trail's least power through it: a dash's blade rides the dash, so it trails at full
        // power however still it is in the hand; a cut's trail follows the blade's own speed (TrailPower). -1: no move.
        private static float BladeLive(Actor a,NativeBladeKit k,float time,bool katana)
        {
            // The plunging slam from the air (the standard swing, Core NativeAirAttackSimulation.KitSlam): from its start to past its landing.
            if(a.AirAttack&&a.AttackAt>-90&&time>=a.AttackAt&&NativePlungeFields.Kind(a)==NativePlungeFields.DownKind&&time<(a.ImpactAt>a.AttackAt?a.ImpactAt+.16f:a.AttackAt+1.6f))return .55f;
            if(k.CastKind==null||time<k.CastAt)return -1;float rel=k.CastRelease,until=k.CastUntil;
            if(katana)switch(k.CastKind)
            {
                case NativeKatana.Cut:return time<until?0:-1;
                case NativeKatana.Rush:return time>=rel-.06f&&time<rel+NativeKatana.RushTime+.12f?1:-1;
                case NativeKatana.Iai:return time>=rel-.04f&&time<rel+NativeKatana.IaiDash+.12f?1:-1;
                case NativeKatana.Storm:return time>=rel-.05f&&time<until-NativeKatana.StormRecover*.5f?.35f:-1;
                case NativeKatana.Counter:return k.PairHit>-50&&time>=k.PairHit-.3f&&time<k.PairHit+.2f?0:-1;
            }
            else switch(k.CastKind)
            {
                case NativeFangs.Cut:return time<until?0:-1;
                case NativeFangs.Pounce:return time>=rel-.04f&&time<rel+NativeFangs.PounceTime+.1f?1:-1;
                case NativeFangs.Step:return time>=rel&&time<rel+.24f?.3f:-1;
                case NativeFangs.Fan:return time<rel+.12f?0:-1;
                case NativeFangs.Bloom:return time<rel+.16f?0:-1;
                case NativeFangs.Assassinate:return k.PairHit>-50&&time>=k.PairHit-.35f&&time<k.PairHit+.16f?0:-1;
            }
            return -1;
        }
        /// <summary>The blade's speed in the hand (m/s: its tip's, the body's own travel taken out) to the trail's power 0..1.</summary>
        private static float TrailPower(float speed,bool katana)=>katana?Clamp01((speed-3.5f)/7):Clamp01((speed-2.5f)/5);
        private static Vector3 Spline(Vector3 p0,Vector3 p1,Vector3 p2,Vector3 p3,float u){float u2=u*u,u3=u2*u;return .5f*(2*p1+(p2-p0)*u+(2*p0-5*p1+4*p2-p3)*u2+(3*p1-p0-3*p2+p3)*u3);}
        /// <summary>The weapon trails (owner 2026-11-11: "Give both weapon weapon trail vfx"): each drawn blade (both of the Fangs') sampled
        /// up to 120 times a second through its moves, its sweep laid as a smooth crescent (a spline through the samples) that narrows toward
        /// its tail and fades: a soft sheet in the weapon colour from part way up the blade, a white-hot edge along the tip's path and a
        /// faint halo past both; a glint at the tip, and the Katana's sparks or the Fangs' venom shed off the fastest of it. Its power follows
        /// the blade's speed in the hand, so a wind-up leaves little and a cut a bright arc; a dash or a slam trails at a set power.</summary>
        private void BladeTrails(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeBladeKit k,float time,Vector3 tint,bool katana,long seed,float q,bool low)
        {
            bool view=scene.Viewer==a.Id;if(!view&&NativeBladeView.Veiled(a,time))return;float live=BladeLive(a,k,time,katana);
            for(int hand=0;hand<(katana?1:2);hand++)
            {
                long key=a.Id*4L+hand;bladeTrails.TryGetValue(key,out var t);
                if(live>=0&&(view?scene.ViewBlade(hand==1,out var root,out var tip):scene.Blade(a,hand==1,out root,out tip)))
                {
                    if(t==null){t=new BladeTrail();bladeTrails[key]=t;}
                    if(time-t.Last>=1/120f)
                    {
                        // A new trail after a break (no move for a moment, or a vanish: the body somewhere else at once).
                        var body=scene.Feet(a);int h=t.Head;bool start=t.Count==0||time-t.Last>.05f||(root-t.Root[h]).LengthSquared()>6.25f;float power=live;
                        if(!start)power=Math.Max(live,TrailPower(((tip-t.Tip[h])-(body-t.Body[h])).Length()/Math.Max(1e-3f,time-t.At[h]),katana));
                        t.Head=(h+1)%BladeTrail.Size;h=t.Head;t.Root[h]=root;t.Tip[h]=tip;t.Body[h]=body;t.At[h]=time;t.Power[h]=power;t.Start[h]=start;
                        t.Count=Math.Min(BladeTrail.Size,t.Count+1);t.Last=time;
                    }
                }
                if(t==null||t.Count<2)continue;float life=katana?.34f:.28f;if(time-t.At[t.Head]>life){t.Count=0;continue;}
                bool finisher=katana?k.CastKind==NativeKatana.Counter||k.CastKind==NativeKatana.Iai||k.CastKind==NativeKatana.Cut&&k.CastStep==NativeKatana.Steps-1
                    :k.CastKind==NativeFangs.Assassinate||k.CastKind==NativeFangs.Pounce||k.CastKind==NativeFangs.Cut&&k.CastStep==NativeFangs.Steps-1;
                var hot=Vector3.Lerp(tint,BladeWhite,.6f);float bright=finisher?1.4f:1,inner=katana?.26f:.1f;int n=Math.Min(t.Count,BladeTrail.Size),sub=low?1:3;
                for(int j=0;j<n-1;j++)
                {
                    int i0=t.Index(j),i1=t.Index(j+1);float age0=time-t.At[i0],age1=time-t.At[i1];if(age0>=life)break;if(t.Start[i0])continue;
                    // The curve's neighbours: the next newer and the next older sample, where the trail goes on.
                    int ip=j>0&&!t.Start[t.Index(j-1)]?t.Index(j-1):i0,iq=j+2<n&&!t.Start[i1]?t.Index(j+2):i1;
                    Vector3 in0=default,out0=default;float a0=0;
                    for(int st=0;st<=sub;st++)
                    {
                        float u=st/(float)sub,age=age0+(age1-age0)*u,f=1-Clamp01(age/life),power=t.Power[i0]+(t.Power[i1]-t.Power[i0])*u;
                        var r=Spline(t.Root[ip],t.Root[i0],t.Root[i1],t.Root[iq],u);var p=Spline(t.Tip[ip],t.Tip[i0],t.Tip[i1],t.Tip[iq],u);
                        // Narrowing toward the tail: its inner edge climbs the blade as it fades.
                        var e=Vector3.Lerp(r,p,inner+(1-inner)*.5f*(1-f));var o=p+(p-r)*.05f;float al=power*(float)Math.Pow(f,1.2)*bright;
                        if(st>0&&(a0>.004f||al>.004f))
                        {
                            s.Band(view,in0,e,o,out0,.85f*a0,.85f*al,tint*2.2f);
                            s.Band(view,out0-(out0-in0)*.24f,o-(o-e)*.24f,o,out0,Math.Min(1,1.2f*a0),Math.Min(1,1.2f*al),hot*3.4f);
                            if(!low)s.Band(view,in0-(out0-in0)*.1f,e-(o-e)*.1f,o+(o-e)*.22f,out0+(out0-in0)*.22f,.45f*a0,.45f*al,tint*1.3f);
                        }
                        in0=e;out0=o;a0=al;
                    }
                    // Shed off the fastest of it: the Katana's sparks flying off the edge, the Fangs' venom falling.
                    if(!low&&(j&1)==0&&t.Power[i0]>.5f&&age0<.2f)
                    {
                        float g=1-age0/.2f;var along=t.Tip[i0]-t.Root[i0];along=along.LengthSquared()>1e-6f?Vector3.Normalize(along):Up;int id=(int)(t.At[i0]*997)+hand*131;
                        if(katana)s.Sprite(view,t.Tip[i0]+along*(.1f*age0/.2f)+RandomDir(seed,id)*(.12f*age0/.2f),hot*(1.6f*g*t.Power[i0]),.026f,Sparkle);
                        else s.Sprite(view,t.Tip[i0]+RandomDir(seed,id)*(.05f*age0/.2f)-Up*(3.2f*age0*age0),tint*(1.5f*g*t.Power[i0]),.028f,Soft);
                    }
                }
                if(time-t.At[t.Head]<.035f&&t.Power[t.Head]>.35f)s.Sprite(view,t.Tip[t.Head],hot*(2.4f*bright*t.Power[t.Head]),katana?.11f:.075f,Sparkle);
            }
        }
        /// <summary>The iai's line of light hanging where the dash went, from the dash's end to the split; then it flares and breaks.</summary>
        private static void KatanaLine(ITechniqueFxSink s,Actor a,NativeBladeKit k,float time,Vector3 tint,long seed,float q,bool low)
        {
            if(k.LineAt<-50||time<k.LineAt-NativeKatana.IaiDash)return;var from=NativeBlades.N(k.LineFrom)+Up*.95f;var to=NativeBlades.N(k.LineTo)+Up*.95f;float split=k.SplitAt;
            var hot=Vector3.Lerp(tint,BladeWhite,.7f);
            if(time<split)
            {
                // Drawn along the dash as it goes, then held, trembling a little brighter as the split nears.
                float laid=Clamp01((time-(k.LineAt-NativeKatana.IaiDash))/NativeKatana.IaiDash);var end=Vector3.Lerp(from,to,laid);float near=Clamp01(1-(split-time)/NativeKatana.IaiSplit);
                s.Line(false,from,end,hot*(1.6f+1.4f*near),.012f+.01f*near,false);s.Line(false,from,end,tint*(.7f+.6f*near),.045f+.03f*near,false);
                // Motes leaking off it.
                int n=Count(10,q,low);for(int i=0;i<n;i++){float u=R(seed,i);var p=Vector3.Lerp(from,end,u);float c=(float)((time*1.7f+R(seed,i+30))%1.0);s.Sprite(false,p+Up*(.25f*c)+RandomDir(seed,i)*.05f,tint*(1.4f*(1-c)*near),.03f,Sparkle);}
                return;
            }
            float age=time-split;if(age>.9f)return;float g=1-Clamp01(age/.9f);
            // The flare, the line breaking into motes that drift up, petals scattering.
            if(age<.12f){s.Line(false,from,to,BladeWhite*(5*(1-age/.12f)),.05f*(1-age/.12f)+.01f,false);s.Line(false,from,to,tint*(3*(1-age/.12f)),.16f,false);}
            int m=Count(36,q,low);for(int i=0;i<m;i++){float u=R(seed,i+60);var p=Vector3.Lerp(from,to,u);var d=RandomDir(seed,i+90);d.Y=Math.Abs(d.Y)+.3f;s.Sprite(false,p+d*(age*1.1f)+Up*(age*age*.6f),hot*(2.2f*g),.04f*(.6f+.4f*g),Sparkle);}
            int pn=Count(18,q,low);for(int i=0;i<pn;i++){float u=R(seed,i+130);var p=Vector3.Lerp(from,to,u);var d=RandomDir(seed,i+170);var at=p+d*(age*1.6f)-Up*(age*age*.9f);float spin=(time*6+i)%6.283f;s.Band(false,at,at+new Vector3((float)Math.Cos(spin)*.06f,.02f,(float)Math.Sin(spin)*.06f),at+new Vector3(.03f,.07f,.0f),at+Up*.05f,.9f*g,.9f*g,Vector3.Lerp(Petal,tint,.35f)*1.3f);}
        }
        /// <summary>The storm: a whirl of petals round the wielder and wind arcs through the cuts.</summary>
        private static void KatanaStorm(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeBladeKit k,float time,Vector3 tint,long seed,float q,bool low)
        {
            if(k.CastKind!=NativeKatana.Storm||time<k.CastAt||time>k.CastUntil+.4f)return;var c=scene.Feet(a)+Up*.95f;float grow=Clamp01((time-k.CastAt)/Math.Max(.05f,k.CastRelease-k.CastAt));float fade=time<k.CastUntil?1:1-Clamp01((time-k.CastUntil)/.4f);
            bool petals=NativeKatanaChips.Holds(k,NativeKatanaChips.ThousandPetals);int n=Count(petals?44:30,q,low);float r0=NativeKatana.StormRadius*(.3f+.7f*grow);
            for(int i=0;i<n;i++)
            {
                float ang=R(seed,i)*6.283f+time*(5+2*R(seed,i+7)),rad=r0*(.45f+.55f*R(seed,i+11)),h=(R(seed,i+19)-.5f)*1.4f;var p=c+new Vector3((float)Math.Sin(ang)*rad,h,(float)Math.Cos(ang)*rad);
                var t=new Vector3((float)Math.Cos(ang),0,-(float)Math.Sin(ang))*.07f;s.Band(false,p-t,p+t,p+t+Up*.035f,p-t+Up*.04f,fade,fade,Vector3.Lerp(Petal,tint,.45f)*1.4f);
            }
            // Wind arcs: bands round the wielder at the cuts' heights.
            if(time>=k.CastRelease&&time<k.CastUntil-NativeKatana.StormRecover*.5f)
            {
                int arcs=low?2:4;for(int j=0;j<arcs;j++)
                {
                    float a0=time*14+j*1.57f,h=-.2f+.25f*j,rad=NativeKatana.StormRadius*(.8f+.1f*j);Vector3 prev=default;
                    for(int st=0;st<=8;st++){float aa=a0+st*.16f;var p=c+new Vector3((float)Math.Sin(aa)*rad,h,(float)Math.Cos(aa)*rad);if(st>0){float f=st/8f;s.Band(false,prev,p,p+Up*.12f*f,prev+Up*.12f*f,.5f*f*fade,.5f*f*fade,tint*1.3f);}prev=p;}
                }
            }
        }
        private static void KatanaHitFx(ITechniqueFxSink s,NativeBladeKit k,NativeBladeHit h,float time,Vector3 tint,long seed,float q,bool low)
        {
            float age=time-h.At;if(age<0||age>1.4f)return;var at=NativeBlades.N(h.Pos);var dir=NativeBlades.N(h.Dir);if(dir.LengthSquared()<1e-6f)dir=Up;dir=Vector3.Normalize(dir);
            var hot=Vector3.Lerp(tint,BladeWhite,.6f);var side=Vector3.Normalize(Vector3.Cross(dir,Up).LengthSquared()>1e-6f?Vector3.Cross(dir,Up):Vector3.UnitX);
            switch(h.Kind)
            {
                case NativeKatana.HitCut:case NativeKatana.HitRush:case NativeKatana.HitStorm:case NativeKatana.HitPetal:case NativeKatana.HitWave:
                {
                    if(h.Victim==0&&h.Kind==NativeKatana.HitStorm){if(age<.2f)Ring(s,at-Up*.85f,.5f+NativeKatana.StormRadius*OutCubic(age/.2f),.05f*(1-age/.2f),tint*(1.6f*(1-age/.2f)),Count(28,q,low));break;}
                    if(age>.32f)break;float u=age/.32f;var slash=Vector3.Normalize(side+Up*(.3f+.5f*R(seed,2)));float len=(.6f+.3f*R(seed,1))*(1-.25f*u);
                    s.Line(false,at-slash*len,at+slash*len,hot*(2.6f*(1-u)),.04f*(1-u)+.008f,true);Flash(s,at,age,.1f,hot,.45f,seed);
                    Sparks(s,at,age,.3f,Count(h.Kind==NativeKatana.HitStorm||h.Kind==NativeKatana.HitPetal?5:9,q,low),5.5f,hot,seed+3,7,.02f,dir*.6f);
                    if(h.Kind==NativeKatana.HitPetal&&age<.5f)for(int i=0;i<Count(6,q,low);i++){var d=RandomDir(seed,i+40);var p=at+d*(.4f*age/.5f)-Up*(.3f*age);s.Sprite(false,p,Vector3.Lerp(Petal,tint,.4f)*(1.4f*(1-age/.5f)),.045f,Soft);}
                    break;
                }
                case NativeKatana.HitFinisher:case NativeKatana.HitCounter:
                {
                    if(age>.55f)break;float u=age/.55f;var slash=Vector3.Normalize(side*.8f+Up*.6f);float len=1.1f*(1-.2f*u);
                    s.Line(false,at-slash*len,at+slash*len,BladeWhite*(4*(1-u)),.06f*(1-u)+.01f,true);s.Line(false,at-slash*len*1.1f,at+slash*len*1.1f,tint*(2.4f*(1-u)),.2f*(1-u),false);
                    Flash(s,at,age,.16f,BladeWhite,1.1f,seed);Sparks(s,at,age,.5f,Count(18,q,low),8,hot,seed+5,8,.025f,dir*.5f);
                    // The counter: a moon rising behind the pair.
                    if(h.Kind==NativeKatana.HitCounter){float m=Clamp01(age/.12f)*(1-Clamp01((age-.25f)/.3f));s.Glow(at+Up*.6f-dir*.4f,Vector3.Lerp(tint,BladeWhite,.75f)*(2.6f*m),0,1.6f*m+.2f,RingGlow,.5f,seed+7);s.Glow(at+Up*.6f-dir*.4f,BladeWhite*(1.6f*m),0,.9f*m,Core,.2f,seed+8);}
                    break;
                }
                case NativeKatana.HitDraw:
                {
                    // The draw: a flash where it set off and a streak of light along the dash, growing with it (a streak's length is its
                    // speed times its blur).
                    if(age>=.25f)break;bool iai=k.CastKind==NativeKatana.Iai;float reach=iai?NativeKatana.IaiReach:NativeKatana.RushReach,dash=iai?NativeKatana.IaiDash:NativeKatana.RushTime;
                    float speed=reach/dash,run=reach*Clamp01(age/dash);Flash(s,at,age,.14f,hot,.8f,seed);
                    if(run>.05f)s.Streak(at+dir*run,dir*speed,hot*(2.5f*(1-age/.25f)),.05f,run,run/speed,.7f,age/.25f,seed);
                    Sparks(s,at,age,.25f,Count(10,q,low),6,hot,seed+2,5,.02f,dir*.8f);break;
                }
                case NativeKatana.HitMark:
                {
                    // A hairline of light across the marked one, held until the split.
                    if(k.SplitAt>-50&&time>=k.SplitAt+.05f||age>NativeKatana.IaiSplit+.3f)break;var slash=Vector3.Normalize(side+Up*.4f);float pulse=.6f+.4f*(float)Math.Sin(time*40);
                    s.Line(false,at-slash*.45f,at+slash*.45f,hot*(1.6f*pulse),.008f,true);break;
                }
                case NativeKatana.HitSplit:
                {
                    if(age>.7f)break;float u=age/.7f;var slash=Vector3.Normalize(side*.85f+Up*.55f);float len=1.3f;
                    s.Line(false,at-slash*len,at+slash*len,BladeWhite*(5*(1-u)),.07f*(1-u)+.01f,true);s.Line(false,at-slash*len,at+slash*len,tint*(3*(1-u)),.28f*(1-u),false);
                    Flash(s,at,age,.18f,BladeWhite,1.4f,seed);Sparks(s,at,age,.6f,Count(24,q,low),9,hot,seed+5,9,.026f,side*.8f);
                    if(h.Amount==0&&age<.4f)Ring(s,at,.2f+1.2f*OutCubic(age/.4f),.06f*(1-age/.4f),hot*(2*(1-age/.4f)),Count(24,q,low));
                    break;
                }
                case NativeKatana.HitSheathe:
                    // The click: a ring of light at the hip and a soft shockwave over the floor.
                    if(age<.4f){float u=age/.4f;Ring(s,at,.08f+.4f*OutCubic(u),.03f*(1-u),hot*(2.4f*(1-u)),Count(20,q,low));s.Glow(at,hot*(1.8f*(1-u)),0,.3f+.2f*u,RingGlow,u,seed);if(age<.3f)Ring(s,at-Up*.9f,.3f+2.4f*OutCubic(age/.3f),.05f*(1-age/.3f),tint*(1.4f*(1-age/.3f)),Count(32,q,low));}
                    break;
                case NativeKatana.HitLaunch:
                    if(age<.35f){s.Streak(at,Up*14,hot*(2.4f*(1-age/.35f)),.06f,.6f,.08f,.7f,age/.35f,seed);Sparks(s,at,age,.35f,Count(10,q,low),6,hot,seed+3,4,.02f,Up);}
                    break;
                // ---- the chips' ----
                case NativeKatana.HitBleed:
                    if(age<.5f){for(int i=0;i<Count(3,q,low);i++){var p=at+RandomDir(seed,i)*.12f-Up*(age*age*3);s.Sprite(false,p,new Vector3(.45f,.02f,.04f),.025f,Soft);}}
                    break;
                case NativeKatana.HitFocus:
                    // Still Water: a ripple at the feet and a glint up the blade.
                    if(age<.8f){float u=age/.8f;Ring(s,at-Up*1.08f,.2f+1.1f*OutCubic(u),.02f*(1-u),hot*(1.4f*(1-u)),Count(24,q,low));if(age<.25f)s.Sprite(false,at+Up*.2f,BladeWhite*(2*(1-age/.25f)),.12f,Sparkle);}
                    break;
            }
            // Crescent Wave: the wave of light flying out from the finisher (its own hits mark where it cuts).
            if(h.Kind==NativeKatana.HitWave&&h.Victim==0)
            {
                float travel=NativeKatanaChips.WaveTravel(age);if(travel>=NativeKatanaChips.WaveReach-.01f&&age>NativeKatanaChips.WaveReach/NativeKatanaChips.WaveSpeed+.15f)return;
                float fade=1-Clamp01((age-NativeKatanaChips.WaveReach/NativeKatanaChips.WaveSpeed)/.15f);var c=at+dir*travel;var w=side*(NativeKatanaChips.WaveWidth*1.4f);Vector3 prev=default;
                for(int st=0;st<=10;st++){float u=st/10f,bend=(float)Math.Sin(Math.PI*u);var p=c+w*(u*2-1)-dir*(.35f*bend)+Up*(.25f*bend);if(st>0){s.Band(false,prev,p,p-dir*.5f,prev-dir*.5f,1.4f*fade,1.4f*fade,tint*1.6f);s.Line(false,prev,p,hot*(2.4f*fade),.02f,false);}prev=p;}
            }
        }
        /// <summary>The veil: a thin smoke at the feet while it lasts (an enemy sees nothing of the body; Runtime hides it).</summary>
        private static void FangsVeil(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeBladeKit k,float time,Vector3 tint,long seed,float q,bool low)
        {
            if(time<k.VeilAt||time>=k.VeilUntil)return;var feet=scene.Feet(a);int n=Count(5,q,low);
            for(int i=0;i<n;i++){float c=(float)((time*1.3f+R(seed,i))%1.0);var p=feet+RandomDir(seed,i+(int)(time*1.3f))*(.25f+.2f*c)+Up*(.15f+.9f*c);s.Smoke(p,SmokeGrey,.22f*(1-c),.35f+.3f*c,c,seed+i,feet.Y);}
            s.Glow(feet+Up*.9f,BladeVenomDark*.6f,0,.8f,Mote,.5f,seed+3);
        }
        /// <summary>Smokescreen's cloud where the veil fell (ChipAt its start).</summary>
        private static void FangsCloud(ITechniqueFxSink s,NativeBladeKit k,float time,Vector3 tint,long seed,float q,bool low)
        {
            if(k.ChipAt<-50||time<k.ChipAt||time>k.ChipAt+NativeFangsChips.CloudFor||k.Hits==null)return;NativeBladeHit cloud=null;foreach(var h in k.Hits)if(h?.Kind==NativeFangs.HitCloud)cloud=h;
            Vector3 at;if(cloud!=null)at=NativeBlades.N(cloud.Pos);else return;float age=time-k.ChipAt,fade=Clamp01(age/.4f)*(1-Clamp01((age-NativeFangsChips.CloudFor+.8f)/.8f));
            int n=Count(14,q,low);for(int i=0;i<n;i++){float c=(float)((time*.35f+R(seed,i))%1.0);var d=RandomDir(seed,i);d.Y=Math.Abs(d.Y)*.4f;var p=at+d*(NativeFangsChips.CloudRadius*(.3f+.7f*R(seed,i+5)))+Up*(.3f+1.2f*c);s.Smoke(p,SmokeGrey,.5f*fade*(1-c*.6f),.9f+.6f*c,c,seed+i,at.Y);}
            s.Glow(at+Up*.4f,tint*(.25f*fade),0,NativeFangsChips.CloudRadius,Mote,.5f,seed+21);
        }
        private static void FangsHitFx(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeBladeKit k,NativeBladeHit h,float time,Vector3 tint,long seed,float q,bool low)
        {
            float age=time-h.At;if(age<0||age>1.5f)return;var at=NativeBlades.N(h.Pos);var dir=NativeBlades.N(h.Dir);if(dir.LengthSquared()<1e-6f)dir=Up;dir=Vector3.Normalize(dir);
            var hot=Vector3.Lerp(tint,BladeWhite,.5f);var side=Vector3.Normalize(Vector3.Cross(dir,Up).LengthSquared()>1e-6f?Vector3.Cross(dir,Up):Vector3.UnitX);
            switch(h.Kind)
            {
                case NativeFangs.HitCut:case NativeFangs.HitFinisher:case NativeFangs.HitStab:case NativeFangs.HitKnife:
                {
                    if(age>.6f)break;
                    if(age<.25f){float u=age/.25f;var slash=Vector3.Normalize(side+Up*(R(seed,2)-.5f));float len=h.Kind==NativeFangs.HitFinisher?.7f:.4f;s.Line(false,at-slash*len,at+slash*len,hot*(2.4f*(1-u)),.03f*(1-u)+.006f,true);Flash(s,at,age,.08f,hot,.3f,seed);}
                    // Venom splashing out of the cut, falling.
                    int n=Count(h.Kind==NativeFangs.HitFinisher?12:7,q,low);for(int i=0;i<n;i++){var d=Vector3.Normalize(RandomDir(seed,i)+dir*.6f+Up*.4f);float sp=1.6f+1.2f*R(seed,i+9);var p=at+d*(sp*age)-Up*(4.5f*age*age);float f=1-Clamp01(age/.6f);s.Sprite(false,p,tint*(1.5f*f),.03f+.02f*f,Soft);}
                    break;
                }
                case NativeFangs.HitPounce:
                    if(h.Amount==1&&age<.4f){float u=age/.4f;Ring(s,at+Up*.03f,.3f+NativeFangs.PounceRadius*OutCubic(u),.06f*(1-u),hot*(1.8f*(1-u)),Count(26,q,low));Dust(s,at,age,.6f,Count(10,q,low),NativeFangs.PounceRadius,new Vector3(.35f,.36f,.33f),.5f,seed+3);}
                    else if(h.Amount==0&&age<.2f)Flash(s,at,age,.1f,hot,.4f,seed);
                    break;
                case NativeFangs.HitVanish:
                {
                    // A puff of black smoke (the venom green only in its wisps and sparkle), at the vanish and at the arrival.
                    if(age>.6f)break;float u=age/.6f;int n=Count(7,q,low);
                    for(int i=0;i<n;i++){var d=RandomDir(seed,i);d.Y=Math.Abs(d.Y)*.5f;var p=at+d*(.15f+.35f*OutCubic(u))+Up*(.1f+.3f*u);s.Smoke(p,ShadowSmoke,.4f*(1-u)*(1-u),.2f+.28f*u,u,seed+i,at.Y-.9f);}
                    if(age<.2f)s.Glow(at,tint*(2*(1-age/.2f)),0,.5f+.3f*age/.2f,RingGlow,age,seed+2);
                    for(int i=0;i<Count(6,q,low);i++){var d=RandomDir(seed,i+30);s.Sprite(false,at+d*(.6f*OutCubic(u))+Up*(.4f*u),tint*(1.6f*(1-u)),.03f,Sparkle);}
                    break;
                }
                case NativeFangs.HitSmoke:
                {
                    // The veil's billow: a wide burst of smoke rising.
                    if(age>1.2f)break;float u=age/1.2f;int n=Count(18,q,low);
                    for(int i=0;i<n;i++){var d=RandomDir(seed,i);d.Y=Math.Abs(d.Y)*.6f;var p=at+d*(.3f+1.4f*OutCubic(u))+Up*(.2f+.9f*u);s.Smoke(p,SmokeGrey,.7f*(1-u),.6f+.8f*u,u,seed+i,at.Y);}
                    if(age<.25f)Flash(s,at+Up*.5f,age,.15f,tint,.6f,seed+5);
                    break;
                }
                case NativeFangs.HitThrow:
                {
                    // The fan's knives in flight, from the throw: each along its way at the throw's speed, out to its range.
                    int n=Math.Max(1,h.Amount);bool hydra=NativeFangsChips.Holds(k,NativeFangsChips.Hydra);float spread=NativeFangs.FanSpread*(hydra?1.6f:1);float travel=NativeFangs.FanSpeed*age;if(travel>NativeFangs.FanRange)break;
                    for(int i=0;i<n;i++){var d=NativeFangs.KnifeDir(dir,i,n,spread);var p=at+d*travel;s.Streak(p,d*NativeFangs.FanSpeed,hot*1.8f,.025f,.35f,.012f,.8f,travel/NativeFangs.FanRange,seed+i);s.Sprite(false,p,tint*1.6f,.05f,Sparkle);}
                    break;
                }
                case NativeFangs.HitBloom:
                {
                    // At the wielder (no victim): a green pulse out over the bloom's reach; on each poisoned one: a burst sized by its stacks.
                    if(h.Victim==0){if(age<.5f){float u=age/.5f;Ring(s,at-Up*.85f,.5f+NativeFangs.BloomRange*.6f*OutCubic(u),.08f*(1-u),tint*(1.6f*(1-u)),Count(40,q,low));s.Glow(at,tint*(1.8f*(1-u)),0,.6f+.8f*u,RingGlow,u,seed);}break;}
                    if(age>.8f)break;float g=1-Clamp01(age/.8f),size=.5f+.12f*Math.Min(10,h.Amount);
                    Flash(s,at,age,.15f,hot,.6f+.1f*h.Amount,seed);s.Glow(at,tint*(2.4f*g),0,size*(.6f+.6f*OutCubic(age/.3f)),RingGlow,age,seed+1);
                    int n=Count(10+2*Math.Min(10,h.Amount),q,low);for(int i=0;i<n;i++){var d=RandomDir(seed,i+50);var p=at+d*(size*1.4f*OutCubic(age/.5f))-Up*(1.5f*age*age);s.Sprite(false,p,tint*(1.8f*g),.04f,Soft);}
                    break;
                }
                case NativeFangs.HitTakedown:case NativeFangs.HitKill:
                {
                    if(age>.9f)break;float u=age/.9f;Flash(s,at,age,.2f,hot,1.2f,seed);s.Glow(at,BladeVenomDark*(2*(1-u)),0,1.2f+.8f*u,Mote,u,seed+1);
                    Sparks(s,at,age,.6f,Count(20,q,low),7,hot,seed+3,8,.024f,dir*.5f);
                    if(h.Kind==NativeFangs.HitKill)for(int i=0;i<Count(12,q,low);i++){var d=RandomDir(seed,i+70);var p=at+d*(.2f+.9f*OutCubic(u))+Up*(.6f*u);s.Smoke(p,BladeVenomDark*1.2f,.6f*(1-u),.4f+.5f*u,u,seed+i,at.Y-.9f);}
                    break;
                }
                case NativeFangs.HitDrip:
                    // A venom drip off the poisoned: more stacks, more drips and a stronger glow round them.
                    if(age<.6f){int n=Math.Min(4,1+h.Amount/2);for(int i=0;i<n;i++){var p=at+RandomDir(seed,i)*.15f-Up*(age*age*3.5f);s.Sprite(false,p,tint*(1.3f*(1-age/.6f)),.022f,Soft);}if(age<.4f)s.Glow(at,tint*(.15f*Math.Min(10,h.Amount)*(1-age/.4f)),0,.5f,Mote,.5f,seed+2);}
                    break;
                case NativeFangs.HitSpread:case NativeFangs.HitDrain:
                {
                    // Venom leaping to the next (Chain Bloom), or drawn back to the wielder (Bloodletting).
                    if(age>.4f)break;float u=age/.4f;var to=h.Kind==NativeFangs.HitDrain?scene.Feet(a)+Up*.9f:at;var from=h.Kind==NativeFangs.HitDrain?at:at-dir*2;
                    for(int i=0;i<Count(6,q,low);i++){float t=Clamp01(u*1.3f-i*.05f);var p=Vector3.Lerp(from,to,t)+Up*(float)Math.Sin(Math.PI*t)*.3f;s.Sprite(false,p,(h.Kind==NativeFangs.HitDrain?new Vector3(.9f,.1f,.15f):tint)*(1.6f*(1-u)),.035f,Sparkle);}
                    break;
                }
            }
        }
    }
}
