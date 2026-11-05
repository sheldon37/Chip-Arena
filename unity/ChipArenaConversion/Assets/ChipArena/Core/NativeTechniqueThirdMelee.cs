using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The third set's sword, daggers, shield and greatsword: effects, previews, sounds and poses. The sword is the worked example of the set.
    public sealed partial class NativeTechniqueFx
    {
        private static readonly string[] MeleeBuilt={NativeTechniques.Disarm,NativeTechniques.Iaido,NativeTechniques.Challenge,NativeTechniques.MirrorDouble,NativeTechniques.Envenom,NativeTechniques.ChainHook};
        private static readonly Vector3 FlickLight=new Vector3(.82f,.93f,1),DrawGold=new Vector3(1,.8f,.42f),DuelRed=new Vector3(1,.2f,.14f);
        private static bool ThirdMelee(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            switch(k.Kind)
            {
                case NativeTechniques.Disarm:Disarm(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.Iaido:Iaido(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.Challenge:Challenge(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.MirrorDouble:MirrorDouble(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.Envenom:Envenom(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.ChainHook:ChainHook(s,scene,a,k,t,seed,q,low);return true;
                default:return false;
            }
        }
        // A broken ring of `n` arcs turning about `c` in the flat, for what a mark holds over someone's head.
        private static void BrokenRing(ITechniqueFxSink s,Vector3 c,float radius,float turn,int n,Vector3 color,float width)
        {
            for(int i=0;i<n;i++)
            {
                float a0=turn+6.2831853f*i/n,a1=a0+6.2831853f/n*.62f;
                var p0=c+new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*radius;var p1=c+new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1))*radius;s.Line(false,p0,p1,color,width,false);
            }
        }

        // ---- Sword: Disarm ----------------------------------------------------------------------------------------------
        // A point of light runs up the blade; the flick is a hair-thin line that outruns the lunge. If it catches someone their weapon sparks,
        // and a cracked ring turns over their head for as long as they cannot use Q or E, so everyone can see the window.
        private static void Disarm(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var dir=Flat(N(k.Dir),Vector3.UnitZ);
            if(t<k.Release){if(scene.Blade(a,false,out var b0,out var b1)){float g=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Sprite(false,Vector3.Lerp(b0,b1,g),(FlickLight+Vector3.One)*(1.2f+1.6f*g),.12f+.1f*g,Sparkle);}return;}
            float age=t-k.Release;
            if(age<.3f)
            {
                float u=OutCubic(age/.2f),fade=1-Smooth(age/.3f);var tip=from+dir*(2.6f*u)+Up*1.18f;var tail=from+dir*(1.1f*u)+Up*1.18f;
                s.Line(false,tail,tip,FlickLight*(1.1f*fade),.06f,false);s.Line(false,tail,tip,(FlickLight+Vector3.One)*(2.4f*fade),.012f,true);s.Sprite(false,tip,(FlickLight+Vector3.One)*(2.2f*fade),.2f,Sparkle);
            }
            if(!k.Hit(t))return;
            float since=t-k.HitAt;var point=N(k.Point);
            Flash(s,point,since,.2f,FlickLight,1.1f,seed);if(since<.6f)Sparks(s,point,since,.5f,Count(20,q,low),6.5f,FlickLight+new Vector3(.4f,.3f,0),seed,8,.024f,dir*.8f);
            if(t>=k.Until)return;var who=ActorOf(scene,k.Target);
            float env=Smooth(since/.14f)*(1-Smooth((t-(k.Until-.3f))/.3f));var head=who!=null?scene.Feet(who)+Up*2.08f:point+Up*.88f;var tint=new Vector3(1,.34f,.28f);
            BrokenRing(s,head,.26f,t*2.6f,low?3:4,tint*(1.8f*env),.022f);s.Sprite(false,head,tint*(.5f*env*(.75f+.25f*(float)Math.Sin(t*9))),.3f,Hot);
            if(who!=null&&!low&&scene.Blade(who,false,out var w0,out var w1)){float run=(t*1.7f)%1;s.Sprite(false,Vector3.Lerp(w0,w1,run),tint*(1.7f*env),.1f,Sparkle);}
        }

        // ---- Sword: Iaido -----------------------------------------------------------------------------------------------
        // The blade goes away and a line of gold creeps along the floor ahead, brighter the longer you hold, with a glint gathering at the hilt: it tells everyone exactly where
        // not to stand. The draw is one bright hair along the whole line, then a beat, then the air opens along it: the wider and hotter the longer the charge.
        private static void Iaido(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var to=N(k.To);var dir=Flat(to-from,Flat(N(k.Dir),Vector3.UnitZ));var r=RightOf(dir);bool drawn=k.Recast(t);
            if(!drawn)
            {
                float hold=Clamp01((t-k.At)/1.5f),env=Smooth((t-k.At)/.15f),pulse=.8f+.2f*(float)Math.Sin(t*14);
                var feet=scene.Feet(a);var line0=feet+Up*.04f;var line1=line0+dir*(5*Smooth(hold*2.2f));
                Strip(s,line0,line1,.16f,DrawGold,.3f*env,.6f*env*pulse*(.5f+.5f*hold));s.Line(false,line0,line1,(DrawGold+Vector3.One*.2f)*((1.2f+2.2f*hold)*env),.03f,true);
                var hilt=feet+Up*.95f+r*.22f;s.Sprite(false,hilt,(DrawGold+Vector3.One)*((.8f+3.2f*hold*hold)*env*pulse),.16f+.34f*hold,Sparkle);
                if(!low&&hold>.25f){int n=Count(8,q,low);for(int i=0;i<n;i++){float ph=(t*.9f+R(seed,i+3))%1,ang=6.2831853f*R(seed,i+4);var p=hilt+new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*(.5f*(1-ph))+Up*(.3f*(R(seed,i+5)-.5f)*(1-ph));s.Glow(p,DrawGold*(1.6f*ph*hold),0,.05f,Core,0,seed+i);}}
                return;
            }
            float since=t-k.RecastAt,charge=Clamp01(k.Arm);if(since>.9f)return;var mid=Up*1.1f;
            // The hair, the instant of the draw.
            if(since<.14f){float f=1-Clamp01(since/.14f);s.Line(false,from+mid,to+mid,(DrawGold+Vector3.One)*(4.5f*f),.016f+.012f*charge,true);s.Line(false,from+mid,to+mid,DrawGold*(1.4f*f),.12f,false);}
            // The afterimages left behind along the path.
            if(since<.3f){int n=low?3:5;for(int i=0;i<n;i++){float u=(i+.5f)/n;var p=Vector3.Lerp(from,to,u)+mid;float f=1-Clamp01((since-.03f*i)/.24f);if(f>0)s.Sprite(false,p,DrawGold*(1.2f*f),.42f,Hot);}}
            // The beat, then the cut opens.
            float open=since-.09f;if(open<=0)return;float u2=OutCubic(open/.26f),fade=1-Smooth(open/.62f),half=(.1f+.22f*charge)*u2;
            for(int side=-1;side<=1;side+=2){var o=Up*(half*side);s.Band(false,from+mid+o-r*.02f,to+mid+o-r*.02f,to+mid+o+r*.02f,from+mid+o+r*.02f,1.8f*fade,1.2f*fade,DrawGold);}
            s.Band(false,from+mid-Up*half,to+mid-Up*half,to+mid+Up*half,from+mid+Up*half,.4f*fade,.25f*fade,DrawGold);
            if(k.Hit(t)&&open<.7f){int n=Count(14,q,low);for(int i=0;i<n;i++){var at=Vector3.Lerp(from,to,R(seed,i+11))+mid;Sparks(s,at,open,.55f,low?1:2,5.5f+3*charge,DrawGold+new Vector3(.3f,.2f,.1f),seed+i*17,9,.02f,r*(R(seed,i+12)-.5f)*2);}}
            Flash(s,to+mid,open,.22f,DrawGold,.9f+.7f*charge,seed);
        }

        // ---- Sword: Challenge -------------------------------------------------------------------------------------------
        // The tip points, a red spark jumps the gap, and from then on a taut line runs chest to chest and a ring burns under each of you: the rest of the fight can see the duel.
        private static void Challenge(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.Release){if(scene.Blade(a,false,out var b0,out var b1)){float g=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Sprite(false,b1,DuelRed*(1+3*g),.14f+.14f*g,Hot);}return;}
            if(t>=k.Until)return;var who=ActorOf(scene,k.Target);
            float age=t-k.Release,env=Smooth(age/.18f)*(1-Smooth((t-(k.Until-.3f))/.3f));var mine=scene.Feet(a);var theirs=who!=null?scene.Feet(who):N(k.Point);
            var c0=scene.Chest(a,out var cc0)?cc0:mine+Up*1.25f;var c1=who!=null&&scene.Chest(who,out var cc1)?cc1:theirs+Up*1.25f;
            if(age<.26f){var run=Vector3.Lerp(c0,c1,OutCubic(age/.2f));s.Sprite(false,run,(DuelRed+Vector3.One*.3f)*4f,.3f,Hot);if(age>.2f)Flash(s,c1,age-.2f,.2f,DuelRed,1.2f,seed);}
            float flicker=.75f+.25f*(float)Math.Sin(t*23+1.4f*Math.Sin(t*7));
            s.Line(false,c0,c1,DuelRed*(.55f*env*flicker),.05f,false);s.Line(false,c0,c1,(DuelRed+new Vector3(0,.4f,.4f))*(1.4f*env*flicker),.012f,true);
            float rr=.72f+.04f*(float)Math.Sin(t*5);int seg=low?14:24;Ring(s,mine+Up*.04f,rr,.07f,DuelRed,seg,.8f*env);Ring(s,theirs+Up*.04f,rr,.07f,DuelRed,seg,.8f*env);
            if(!low){int n=Count(4,q,low);for(int i=0;i<n;i++){float ph=(t*.7f+i/(float)n)%1;s.Sprite(false,Vector3.Lerp(c0,c1,ph),DuelRed*(1.6f*env),.08f,Sparkle);}}
        }

        // A burst of thin flat splinters turning over as they fly, each flashing when it faces you: glass, armour plate, slag.
        private static void MeleeShards(ITechniqueFxSink s,Vector3 at,float age,float life,int n,float speed,Vector3 color,long seed,float gravity,float length,Vector3 bias=default)
        {
            if(age<0||age>life)return;
            for(int i=0;i<n;i++)
            {
                float own=life*(.6f+.4f*R(seed,i*4+1));if(age>own)continue;float u=age/own;var v=Vector3.Normalize(RandomDir(seed,i*6+2)+bias)*(speed*(.35f+.65f*R(seed,i*4+2)));var p=at+v*(age*(1-.3f*u))-Up*(.5f*gravity*age*age);
                float turn=age*(9+14*R(seed,i*4+3))+6.2831853f*R(seed,i*4+4),len=length*(.6f+.7f*R(seed,i*4+6)),gleam=(float)Math.Pow(Math.Abs(Math.Sin(turn*.8f)),6);var axis=RandomDir(seed,i*6+5)*(float)Math.Cos(turn)+RandomDir(seed,i*6+7)*(float)Math.Sin(turn);
                s.Line(false,p-axis*len,p+axis*len,color*((.7f+2.4f*gleam)*(1-u*u)),.4f*len,true);
            }
        }

        // ---- Daggers: Mirror Double -------------------------------------------------------------------------------------
        // A hair of lilac opens into a tall pane of mirror just ahead of you, and a double runs out of it as the pane bursts into glass. The double is a body of light with its ghosts at its heels:
        // it looks straight on, never turns and never swings. Where you stand a faint shimmer gives you away until you reappear. One hit shatters the double; left alone it comes apart at the end.
        private static readonly Vector3 MirrorLilac=new Vector3(.7f,.55f,1),MirrorPale=new Vector3(.92f,.88f,1);
        /// <summary>How fast the double runs while `To` does not say (the preview's record stands still; in play `To` is live and the nearer of the two).</summary>
        public const float MirrorRun=6.8f;
        private static Vector3 MirrorAt(NativeTechnique k,float t)
        {
            var from=N(k.From);var line=N(k.To)-from;float len=line.Length();if(len<.01f)return from;return from+line/len*Math.Min(len,MirrorRun*Math.Max(0,(k.Hit(t)?k.HitAt:t)-k.Release));
        }
        // A body of light at `feet`: `run` turns its stride (a whole stride a turn), `stride` 0 stands it still.
        private static void MirrorFigure(ITechniqueFxSink s,Vector3 feet,Vector3 f,Vector3 r,float run,float stride,Vector3 color,float b,bool low)
        {
            if(b<=.01f)return;float ph=run*6.2831853f,bob=(float)Math.Abs(Math.Sin(ph))*.05f*stride;
            var hips=feet+Up*(.93f+bob)+f*(.06f*stride);var chest=hips+Up*.46f+f*(.14f*stride);var neck=chest+Up*.16f+f*(.03f*stride);var head=neck+Up*.15f+f*.02f;
            void Limb(Vector3 p0,Vector3 p1,float w){if(!low)s.Line(false,p0,p1,color*(.5f*b),w*2.6f,false);s.Line(false,p0,p1,(color+Vector3.One*.5f)*(1.1f*b),w,false);}
            for(int sd=-1;sd<=1;sd+=2)
            {
                float side=sd>0?0:3.14159f,swing=(float)Math.Sin(ph+side),th=.85f*swing*stride,kn=(.25f+1.05f*Math.Max(0,-(float)Math.Cos(ph+side)))*stride+.04f;
                var hip=hips+r*(.1f*sd);var knee=hip+(f*(float)Math.Sin(th)-Up*(float)Math.Cos(th))*.46f;var sole=knee+(f*(float)Math.Sin(th-kn)-Up*(float)Math.Cos(th-kn))*.46f;Limb(hip,knee,.085f);Limb(knee,sole,.065f);
                float arm=-.8f*swing*stride+.08f,fore=arm+.9f*stride+.15f;var sh=chest+r*(.21f*sd)+Up*.02f;var elbow=sh+(f*(float)Math.Sin(arm)-Up*(float)Math.Cos(arm))*.29f+r*(.03f*sd);var hand=elbow+(f*(float)Math.Sin(fore)-Up*(float)Math.Cos(fore))*.28f;Limb(sh,elbow,.06f);Limb(elbow,hand,.05f);
            }
            s.Band(false,chest-r*.21f+Up*.02f,chest+r*.21f+Up*.02f,hips+r*.13f,hips-r*.13f,.75f*b,.75f*b,color);Limb(neck,chest,.05f);Limb(hips,chest,.13f);
            s.Sprite(false,head,(color+Vector3.One*.4f)*(1.5f*b),.32f,Hot);
        }
        private static void MirrorDouble(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var dir=Flat(N(k.To)-from,Flat(N(k.Dir),Vector3.UnitZ));var rt=RightOf(dir);var pane=from+dir*.8f;const float Tall=2.1f;bool view=scene.Viewer==a.Id;
            if(t<k.Release)
            {
                float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Line(false,pane,pane+Up*(Tall*Smooth(u*1.5f)),MirrorPale*(2.4f*u),.03f,false);s.Sprite(false,pane+Up*(Tall*Smooth(u*1.5f)),MirrorPale*(2.4f*u),.22f,Sparkle);
                return;
            }
            float age=t-k.Release,flick=.8f+.2f*(float)Math.Sin(t*37+2*Math.Sin(t*11));
            // The pane: it swings square as the double reaches it, then bursts.
            if(age<.24f)
            {
                float open=OutCubic(age/.07f),swing=1.2f*(1-OutCubic(age/.16f)),b=1-Smooth((age-.13f)/.1f),w=.62f*open;var across=rt*(float)Math.Cos(swing)+dir*(float)Math.Sin(swing);var p0=pane-across*w;var p1=pane+across*w;var top=Up*Tall;
                s.Band(false,p0,p1,p1+top,p0+top,.55f*b,.55f*b,MirrorLilac);Vector3 fr=MirrorPale*(2.4f*b);s.Line(false,p0,p1,fr,.04f,false);s.Line(false,p0+top,p1+top,fr,.04f,false);s.Line(false,p0,p0+top,fr,.04f,false);s.Line(false,p1,p1+top,fr,.04f,false);
                float g=Clamp01(age/.16f);s.Line(false,Vector3.Lerp(p0,p1,g*.7f),Vector3.Lerp(p0,p1,g*.7f+.3f)+top,MirrorPale*(2.6f*b),.05f,true);
            }
            Flash(s,pane+Up*1.05f,age-.12f,.2f,MirrorLilac,1.5f,seed);MeleeShards(s,pane+Up*1.05f,age-.12f,.8f,Count(26,q,low),5,MirrorPale,seed+1,6,.13f,dir*.7f);
            // Where you stand, until you reappear.
            if(t<k.Arm)
            {
                float env=Smooth(age/.1f)*(1-Smooth((t-(k.Arm-.12f))/.12f));
                if(view){if(scene.ViewBlade(false,out var v0,out var v1))s.Sprite(true,v1,MirrorLilac*(1.5f*env*flick),.08f,Sparkle);}
                else
                {
                    var feet=scene.Feet(a);var fw=Flat(N(V3.Forward(a.Yaw)),Vector3.UnitZ);MirrorFigure(s,feet,fw,RightOf(fw),0,0,MirrorLilac,.3f*env*flick,low);
                    int n=Count(10,q,low);for(int i=0;i<n;i++){float ph=(t*1.3f+R(seed,i+60))%1,ang=6.2831853f*R(seed,i+61);s.Sprite(false,feet+new Vector3((float)Math.Cos(ang)*.32f,.1f+1.7f*ph,(float)Math.Sin(ang)*.32f),MirrorPale*(1.5f*env*(float)Math.Sin(3.14159f*ph)),.1f,Sparkle);}
                }
            }
            else{float back=t-k.Arm;if(back<.3f&&!view){var feet=scene.Feet(a);Flash(s,feet+Up*1.05f,back,.22f,MirrorLilac,1.1f,seed+2);Ring(s,feet+Up*.04f,1.2f*OutCubic(back/.3f),.08f,MirrorLilac*(1.1f*(1-back/.3f)),low?12:20);}}
            // The double.
            var at=MirrorAt(k,t);float run=age*2.3f;bool popped=k.Hit(t);
            if(!popped&&t<k.Until)
            {
                float show=Smooth(age/.1f)*(1-Smooth((t-(k.Until-.3f))/.3f));
                int ghosts=low?1:3;for(int j=ghosts;j>=1;j--){var o=at-dir*(.36f*j);if(Vector3.Dot(o-from,dir)>0)MirrorFigure(s,o,dir,rt,run-.09f*j,1,MirrorLilac,show*.3f/j,true);}
                MirrorFigure(s,at,dir,rt,run,1,MirrorLilac,show*flick,low);
                // Slow scan lines climbing it and flecks shed off it: it shimmers, plainly not a person.
                int lines=low?2:4;for(int i=0;i<lines;i++){float y=((t*.9f+i/(float)lines)%1)*1.8f;var c=at+Up*y+dir*(.1f*Clamp01(y));s.Line(false,c-rt*.26f,c+rt*.26f,MirrorPale*(1.8f*show),.02f,false);}
                int m=Count(10,q,low);for(int i=0;i<m;i++){float ph=(t*1.8f+R(seed,i+70))%1;var p=at-dir*(1.1f*ph)+rt*((R(seed,i+71)-.5f)*.7f)+Up*(.15f+1.6f*R(seed,i+72));s.Sprite(false,p,MirrorPale*(1.5f*(1-ph)*show),.09f,Sparkle);}
                s.Glow(at+Up*.04f,MirrorLilac*(.5f*show),0,1.3f,Core,0,seed+3);
            }
            if(popped)
            {
                float since=t-k.HitAt;var mid=at+Up*1.05f;if(since<.1f)MirrorFigure(s,at,dir,rt,run,1,MirrorPale,2.2f*(1-since/.1f),low);
                Flash(s,mid,since,.24f,MirrorLilac,1.9f,seed+4);MeleeShards(s,mid,since,.8f,Count(36,q,low),6.5f,MirrorPale,seed+5,7,.14f);if(since<.35f)Ring(s,at+Up*.04f,2.3f*OutCubic(since/.35f),.1f,MirrorLilac*(1.3f*(1-since/.35f)),low?14:24);
            }
            else if(t>=k.Until)MeleeShards(s,at+Up*1.05f,t-k.Until,.6f,Count(16,q,low),2.4f,MirrorLilac+Vector3.One*.2f,seed+6,2,.09f);
        }

        // ---- Daggers: Envenom -------------------------------------------------------------------------------------------
        // Venom runs from hilt to tip along both blades, and from then on they drip: thick green drops off each point that splash where they land, fumes curling up, a sick light on the floor.
        // Three beads circle the waist, one spent with each hit. The last hit bursts green where it struck.
        private static readonly Vector3 VenomGreen=new Vector3(.42f,1,.2f),VenomPale=new Vector3(.8f,1,.52f),VenomDark=new Vector3(.08f,.3f,.05f);
        private static void Envenom(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);bool view=scene.Viewer==a.Id;float floor=feet.Y+.03f,age=t-k.Release,env=t<k.Release?0:Smooth(age/.12f)*(1-Smooth((t-(k.Until-.25f))/.25f));
            for(int side=0;side<2;side++)
            {
                bool left=side==1,has=view?scene.ViewBlade(left,out var b0,out var b1):scene.Blade(a,left,out b0,out b1);if(!has)continue;
                if(t<k.Release)
                {
                    float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));var head=Vector3.Lerp(b0,b1,u);s.Line(view,b0,head,VenomGreen*1.7f,view?.03f:.09f,false);s.Line(view,b0,head,VenomPale*2,view?.008f:.022f,true);s.Sprite(view,head,VenomPale*2.8f,view?.06f:.2f,Hot);
                    if(!view){int n=Count(6,q,low);for(int i=0;i<n;i++){float ph=(u+i/(float)n)%1,ang=6.2831853f*R(seed,i+side*20+1);s.Sprite(false,b0+new Vector3((float)Math.Cos(ang),.3f,(float)Math.Sin(ang))*(.45f*(1-ph)),VenomGreen*(1.8f*ph),.08f,Sparkle);}}
                    continue;
                }
                if(env<=0)continue;float pulse=.8f+.2f*(float)Math.Sin(t*7+side*2.1f);
                s.Line(view,b0,b1,VenomGreen*(1.25f*env*pulse),view?.03f:.1f,false);s.Line(view,b0,b1,VenomPale*(1.7f*env),view?.008f:.022f,true);s.Sprite(view,b1,VenomGreen*(2.2f*env),view?.06f:.2f,Hot);
                for(int j=0;j<2;j++){float run=(t*.9f+j*.5f+side*.25f)%1;s.Sprite(view,Vector3.Lerp(b0,b1,run),VenomPale*(2.3f*env),view?.04f:.11f,Hot);}
                if(view)continue;
                // The drip that warns of it: off each point, down to the floor, and a little splash where it lands.
                int d=Count(5,q,low);for(int i=0;i<d;i++){float f=((t*1.25f+R(seed,i*5+side*3+1))%1)*.62f;var p=b1-Up*(5*f*f);if(p.Y<floor)continue;s.Drop(p,-Up*(10*f*.05f+.03f),VenomGreen*(1.8f*env),.95f,.05f,f/.62f,seed+i+side*9);}
                int sp=low?1:2;for(int i=0;i<sp;i++){float x=(t*1.25f+R(seed,i*7+side+40))%1;var land=new Vector3(b1.X+.16f*(R(seed,i+side*3+50)-.5f),floor,b1.Z+.16f*(R(seed,i+side*3+51)-.5f));Ring(s,land,.04f+.2f*OutCubic(x),.035f,VenomGreen*(1.5f*(1-x)*env),low?6:10);s.Sprite(false,land,VenomPale*(1.4f*(1-x)*env),.08f,Hot);}
                if(!low){int w=Count(3,q,low);for(int i=0;i<w;i++){float ph=(t*.7f+R(seed,i+side*5+60))%1;s.Smoke(Vector3.Lerp(b0,b1,R(seed,i+side*5+61))+Up*(.05f+.6f*ph),VenomDark+VenomGreen*.15f,.32f*(1-ph)*env,.24f+.34f*ph,.2f+.5f*ph,seed+i+side,floor);}}
            }
            if(env>0&&!view)
            {
                s.Glow(feet+Up*.05f,VenomGreen*(.3f*env),0,1.8f,Core,0,seed+3);
                var waist=scene.Chest(a,out var c)?c-Up*.3f:feet+Up*1;int left=Math.Max(0,Math.Min(3,(int)Math.Round(k.Arm)));
                for(int i=0;i<3;i++){float ang=t*2.4f+i*2.0943951f;var p=waist+new Vector3((float)Math.Cos(ang)*.55f,.05f*(float)Math.Sin(t*3+i),(float)Math.Sin(ang)*.55f);if(i<left){s.Sprite(false,p,VenomPale*(2.2f*env),.2f,Hot);s.Line(false,p,p+Up*.14f,VenomGreen*(1.6f*env),.05f,true);}else s.Sprite(false,p,VenomDark*(1.4f*env),.1f,Soft);}
            }
            if(k.Hit(t))
            {
                float since=t-k.HitAt;var at=N(k.To);Flash(s,at,since,.2f,VenomGreen,1.25f,seed+7);Sparks(s,at,since,.4f,Count(14,q,low),4.5f,VenomGreen,seed+8,9,.024f);
                if(since<.55f){float u=since/.55f;int n=Count(14,q,low);for(int i=0;i<n;i++){var v=RandomDir(seed,i+80)*(3.6f*(.4f+.6f*R(seed,i+81)))+Up*1.4f;var vel=v-Up*(9*since);s.Drop(at+v*since-Up*(4.5f*since*since),vel*.04f,VenomGreen*(1.9f*(1-u)),.95f,.05f,u,seed+i+30);}}
                if(since<.32f)Hoop(s,at,Flat(at-feet,Vector3.UnitZ),.15f+.75f*OutCubic(since/.32f),.07f,VenomGreen*(1.5f*(1-since/.32f)),low?10:18,1);
            }
        }

        // ---- Daggers: Chain Hook ----------------------------------------------------------------------------------------
        // The hook whirls at the hand, then flies dragging a real chain, link by link, its wave dying as it goes. It bites with a spark, the chain snaps straight and hot, a coil wraps whoever it caught and
        // they plough home along the floor. A miss clanks at the end of its reach and comes back slack, sagging.
        /// <summary>The hook's speed (the preview's record stands still: in play `Point` is live and the nearer of the two).</summary>
        public const float HookSpeed=40;
        private static readonly Vector3 HookSteel=new Vector3(.76f,.83f,.92f),HookGlint=new Vector3(1,.96f,.86f),HookTaut=new Vector3(1,.6f,.26f);
        private static Vector3 HookAt(NativeTechnique k,float t)
        {
            var from=N(k.From);var line=N(k.Point)-from;float len=line.Length(),reach;if(len<.01f)return from;
            if(k.Hit(t)){float gone=HookSpeed*(k.HitAt-k.Release),u=Clamp01((t-k.HitAt)/Math.Max(.05f,k.Until-k.HitAt));reach=gone+(Vector3.Distance(from,N(k.To))-gone)*(u*u);}
            else if(k.Arm>k.Release&&t>=k.Arm)reach=HookSpeed*(2*k.Arm-k.Release-t);else reach=HookSpeed*(t-k.Release);
            return from+line/len*Math.Max(0,Math.Min(len,reach));
        }
        // The chain, link by link: broad links and thin ones in turn, on a wave `amp` high and a sag.
        private static void HookChain(ITechniqueFxSink s,Vector3 hand,Vector3 hook,float amp,float sag,float t,Vector3 tint,float b,bool low)
        {
            var line=hook-hand;float len=line.Length();if(len<.05f)return;var d=line/len;var x=Vector3.Cross(d,Up);x=x.LengthSquared()>1e-5f?Vector3.Normalize(x):Vector3.UnitX;var y=Vector3.Normalize(Vector3.Cross(x,d));
            Vector3 P(float u){float e=(float)Math.Sin(3.14159f*u);return hand+line*u+(x*(float)Math.Sin(u*9-t*38)+y*(float)Math.Sin(u*6.5f-t*27))*(amp*e)-Up*(sag*e);}
            int n=Math.Max(4,Math.Min(low?14:30,(int)(len/.2f)));
            for(int i=0;i<n;i++){bool thin=i%2==1;var p0=P(i/(float)n);var p1=P((i+.78f)/n);s.Line(false,p0,p1,tint*((thin?1:1.5f)*b),thin?.028f:.065f,false);if(!thin&&!low)s.Line(false,p0,p1,HookGlint*(1.1f*b),.016f,true);}
        }
        // The hook: a shank and two barbed flukes.
        private static void HookClaw(ITechniqueFxSink s,Vector3 at,Vector3 d,float spin,float b)
        {
            var x=Vector3.Cross(d,Up);x=x.LengthSquared()>1e-5f?Vector3.Normalize(x):Vector3.UnitX;var y=Vector3.Normalize(Vector3.Cross(x,d));var w=x*(float)Math.Cos(spin)+y*(float)Math.Sin(spin);
            void Bar(Vector3 p,Vector3 o){s.Line(false,p,o,HookSteel*(1.1f*b),.1f,false);s.Line(false,p,o,HookGlint*(1.7f*b),.03f,false);}
            Bar(at-d*.32f,at);for(int sd=-1;sd<=1;sd+=2){var bend=at+d*.1f+w*(sd*.17f);var barb=at-d*.02f+w*(sd*.32f);Bar(at,bend);Bar(bend,barb);Bar(barb,at-d*.22f+w*(sd*.28f));}
            s.Sprite(false,at,HookGlint*(1.7f*b),.2f,Hot);
        }
        private static void ChainHook(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            bool view=scene.Viewer==a.Id;var hand=N(k.From);if(!view&&scene.Blade(a,false,out var h0,out var h1))hand=h0;
            if(t<k.Release)
            {
                float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At)),spin=t*24;var fw=Flat(N(V3.Forward(a.Yaw)),Vector3.UnitZ);var rw=RightOf(fw);Vector3 W(float ang)=>hand+(rw*(float)Math.Cos(ang)+Up*(float)Math.Sin(ang))*.46f;
                HookChain(s,hand,W(spin),0,0,t,HookSteel,u,true);HookClaw(s,W(spin),rw*(float)Math.Sin(spin)-Up*(float)Math.Cos(spin),0,u);int n=low?4:8;for(int i=0;i<n;i++)s.Line(false,W(spin-.3f*i),W(spin-.3f*(i+1)),HookGlint*(1.1f*u*(1-i/(float)n)),.03f,false);
                return;
            }
            var from=N(k.From);var feet=scene.Feet(a);var hook=HookAt(k,Math.Min(t,k.Until));var aim=N(k.Point)-from;var d=aim.LengthSquared()>1e-4f?Vector3.Normalize(aim):Flat(N(k.Dir),Vector3.UnitZ);
            float age=t-k.Release;bool caught=k.Hit(t);var who=caught?ActorOf(scene,k.Target):null;var end=hook;
            // On someone, the hook's end is on them for as long as they are near it.
            if(who!=null&&scene.Chest(who,out var ch)){float w=Clamp01(1.6f-Vector3.Distance(ch,hook));end=Vector3.Lerp(hook,ch,w);}
            // After the end the chain runs back into the hand.
            if(t>=k.Until){float back=(t-k.Until)/.18f;if(back>=1){if(caught)MeleeHookWrap(s,end,t,1-Smooth((t-k.Until)/.3f),low);return;}var home=Vector3.Lerp(end,hand,back*back);HookChain(s,hand,home,.2f,.15f,t,HookSteel,1-back,low);HookClaw(s,home,d,0,1-back);if(caught)MeleeHookWrap(s,end,t,1-Smooth((t-k.Until)/.3f),low);return;}
            if(!caught)
            {
                bool slack=k.Arm>k.Release&&t>=k.Arm;float since=slack?t-k.Arm:0;
                HookChain(s,hand,end,slack?.34f:.14f*(1-Clamp01(age/.3f))+.02f,slack?.7f*(float)Math.Sin(3.14159f*Clamp01(since/.3f)):0,t,HookSteel,1,low);HookClaw(s,end,d,slack?t*7:t*30,1);
                if(!slack){s.Line(false,end-d*1.3f,end,HookGlint*1.5f,.06f,true);s.Sprite(false,end,HookGlint*1.6f,.42f,Soft);}else{Flash(s,end,since,.16f,HookSteel,.8f,seed);Sparks(s,end,since,.3f,Count(8,q,low),3.5f,HookGlint,seed+1,9,.018f);}
                return;
            }
            float got=t-k.HitAt,heat=1-Clamp01((got-.08f)/.3f);var tint=Vector3.Lerp(HookSteel,HookTaut*1.5f,heat);
            var scar=from+d*(HookSpeed*(k.HitAt-k.Release));scar.Y=feet.Y+.04f;var over=new Vector3(end.X,feet.Y+.04f,end.Z);
            Strip(s,scar,over,.16f,HookTaut,.25f,.9f);Spray(s,over,t,.2f,Count(8,q,low),4.5f,HookTaut+Vector3.One*.3f,seed+2,9,.024f,Flat(end-hand,Vector3.UnitZ)*.8f+Up*.8f);
            HookChain(s,hand,end,got<.1f?.22f*(1-got/.1f):0,0,t,tint,1.2f,low);HookClaw(s,end,d,0,1);MeleeHookWrap(s,end,t,Smooth(got/.06f),low);
            Flash(s,end,got,.22f,HookGlint,1.35f,seed+3);Sparks(s,end,got,.4f,Count(18,q,low),5.5f,HookGlint,seed+4,9,.024f,-d*.6f);
            if(!low){int g=Count(3,q,low);for(int i=0;i<g;i++){float run=(got*4+i/(float)g)%1;s.Sprite(false,Vector3.Lerp(end,hand,run),HookGlint*2,.16f,Sparkle);}}
        }
        // Chain wound round whoever it caught.
        private static void MeleeHookWrap(ITechniqueFxSink s,Vector3 at,float t,float b,bool low)
        {
            if(b<=.01f)return;int n=low?10:20;Vector3 last=at;
            for(int i=0;i<=n;i++){float u=i/(float)n,ang=u*15.7f+t*1.4f;var p=at+new Vector3((float)Math.Cos(ang)*.4f,-.38f+.76f*u,(float)Math.Sin(ang)*.4f);if(i>0)s.Line(false,last,p,HookSteel*(1.7f*b),.035f,false);last=p;}
        }
    }
    public sealed partial class NativeTechniquePreview
    {
        private static V3 V3Of(Vector3 v)=>new V3(v.X,v.Y,v.Z);
        private static bool WriteMelee(NativeTechnique k,string id,Vector3 feet,Vector3 f,Vector3 r,Vector3 up,Vector3 hand,float t0,bool first)
        {
            switch(id)
            {
                case NativeTechniques.Disarm:k.Release=t0+.14f;k.From=V3Of(feet);k.Dir=V3Of(f);k.Point=V3Of(feet+f*2.4f+up*1.2f);if(first){k.HitAt=k.Release+.14f;k.Until=k.HitAt+2.5f;k.RecoverUntil=k.Release+.3f;}else{k.Until=k.Release+.5f;k.RecoverUntil=k.Until;}return true;
                case NativeTechniques.Iaido:k.Release=t0+.15f;k.From=V3Of(feet);k.To=V3Of(feet+f*5);k.Dir=V3Of(f);k.Arm=first?1:.35f;k.RecastAt=t0+(first?1.5f:.55f);k.Until=k.RecastAt+.3f;k.RecoverUntil=k.Until+.25f;k.HitAt=first?k.RecastAt+.05f:-100;return true;
                case NativeTechniques.Challenge:k.Release=t0+.22f;k.Point=V3Of(feet+f*5);k.Until=k.Release+(first?6:3);k.RecoverUntil=k.Release+.2f;return true;
                case NativeTechniques.MirrorDouble:{k.Release=t0+.14f;k.From=V3Of(feet);k.Dir=V3Of(f);k.Arm=k.Release+1;float run=first?1.5f:2.5f;k.To=V3Of(feet+f*(NativeTechniqueFx.MirrorRun*run));if(first){k.HitAt=k.Release+run;k.Until=k.HitAt+.3f;}else k.Until=k.Release+run;k.RecoverUntil=k.Release+.1f;return true;}
                case NativeTechniques.Envenom:k.Release=t0+.4f;k.RecoverUntil=k.Release+.1f;if(first){k.Arm=2;k.HitAt=k.Release+1.25f;k.To=V3Of(feet+f*1.25f+up*1.2f);k.Until=k.Release+3.6f;}else{k.Arm=3;k.Until=k.Release+6;}return true;
                case NativeTechniques.ChainHook:{k.Release=t0+.2f;k.From=V3Of(hand);k.To=V3Of(feet+f*2+up*1.2f);var end=feet+f*(first?9.5f:12)+up*1.2f;k.Point=V3Of(end);float fly=Vector3.Distance(hand,end)/NativeTechniqueFx.HookSpeed;if(first){k.HitAt=k.Release+fly;k.Until=k.HitAt+.35f;}else{k.Arm=k.Release+fly;k.Until=k.Arm+.3f;}k.RecoverUntil=k.Until;return true;}
                default:return false;
            }
        }
        private static float PeriodMelee(string id)
        {
            switch(id)
            {
                case NativeTechniques.Disarm:return 4.4f;
                case NativeTechniques.Iaido:return 3.4f;
                case NativeTechniques.Challenge:return 7.5f;
                case NativeTechniques.MirrorDouble:return 4.2f;
                case NativeTechniques.Envenom:return 7.4f;
                case NativeTechniques.ChainHook:return 2.6f;
                default:return 0;
            }
        }
        private static bool AimMelee(NativeTechnique k,string id,int target,Vector3 at)
        {
            switch(id)
            {
                case NativeTechniques.Disarm:if(k.HitAt>-50){k.Target=target;k.Point=V3Of(at+Vector3.UnitY*1.2f);}return true;
                case NativeTechniques.Challenge:k.Target=target;k.Point=V3Of(at);return true;
                case NativeTechniques.ChainHook:
                {
                    // Only someone ahead and in reach: it is a straight line.
                    if(k.HitAt<-50)return true;var from=new Vector3(k.From.x,k.From.y,k.From.z);var to=at+Vector3.UnitY*1.2f;var line=to-from;float len=line.Length();
                    if(len>1&&len<12&&Vector3.Dot(line,new Vector3(k.Dir.x,k.Dir.y,k.Dir.z))>0){k.Target=target;k.Point=V3Of(to);k.To=V3Of(from+line/len*1.65f);k.HitAt=k.Release+len/NativeTechniqueFx.HookSpeed;k.Until=k.HitAt+.35f;k.RecoverUntil=k.Until;}
                    return true;
                }
                default:return false;
            }
        }
    }
    public static partial class NativeTechniqueSounds
    {
        private static int MomentMelee(string kind,int m,NativeKitSound[] o,ref bool atPoint)
        {
            int n=0;
            switch(kind)
            {
                case NativeTechniques.Disarm:if(m==Release)n=S(o,n,"melee.sword.swing","sword",.75f);else if(m==Hit){atPoint=true;n=S(o,n,"melee.shield.block","parry",.8f);n=S(o,n,"impact.metal","metal",.7f);}break;
                case NativeTechniques.Iaido:if(m==Release)n=S(o,n,"gun.reload.end","metal",.3f);else if(m==Recast){n=S(o,n,"melee.greatsword.swing","greatsword",.9f);n=S(o,n,"move.roll","dash",.6f);}else if(m==Hit)n=S(o,n,"melee.greatsword.hit","greatsword",.85f);break;
                case NativeTechniques.Challenge:if(m==Release){n=S(o,n,"melee.shield.bash","shield",.4f);n=S(o,n,"blast.kinetic","dash",.35f);}break;
                case NativeTechniques.MirrorDouble:if(m==Release){n=S(o,n,"magic.blink-strike","demon-step",.55f);n=S(o,n,"blast.transient","dash",.45f);}else if(m==Arm)n=S(o,n,"move.roll","dash",.3f);else if(m==Hit){n=S(o,n,"impact.metal","metal",.45f);n=S(o,n,"blast.transient","dash",.6f);}break;
                case NativeTechniques.Envenom:if(m==Release){n=S(o,n,"impact.water","water",.5f);n=S(o,n,"gun.reload.end","metal",.3f);}else if(m==Hit){n=S(o,n,"impact.water","water",.6f);n=S(o,n,"melee.daggers.hit","daggers",.4f);}break;
                case NativeTechniques.ChainHook:if(m==Release){n=S(o,n,"melee.daggers.throw","daggers",.7f);n=S(o,n,"gun.frag.throw","dash",.5f);}else if(m==Arm){atPoint=true;n=S(o,n,"gun.reload.end","metal",.5f);}else if(m==Hit){atPoint=true;n=S(o,n,"melee.daggers.hit","daggers",.9f);n=S(o,n,"impact.metal","metal",.7f);}else if(m==End)n=S(o,n,"gun.reload.end","metal",.25f);break;
            }
            return n;
        }
    }
    public static partial class NativeVisualMotion
    {
        private static bool PoseMelee(Set set,NativeTechnique k,float time,float back,IClips clips,out Pick pick)
        {
            pick=default;Pick got=default;
            bool Part(string clip,float from,float to){return time>=from&&time<to&&clip!=null&&clips.Has(clip)&&Fit(clip,time,from,to,clips,FadeIn,out got);}
            bool hit=false;
            switch(k.Kind)
            {
                case NativeTechniques.Disarm:hit=Part(TechCounter,k.At,Math.Max(k.Release+.3f,back));break;
                case NativeTechniques.Iaido:{float draw=k.RecastAt>-50?k.RecastAt:k.Until;hit=Part(TechSurf,k.At,draw)||Part("Armed-Dash-Forward",draw,draw+.14f)||Part(TechCounter,draw+.14f,Math.Max(draw+.4f,k.RecoverUntil));}break;
                case NativeTechniques.Challenge:hit=Part(TechCoin,k.At,back);break;
                case NativeTechniques.MirrorDouble:hit=Part(TechFlickStart,k.At,k.Release)||Part(TechFlickEnd,k.Release,back);break;
                case NativeTechniques.Envenom:hit=Part(set.Block,k.At,k.Release+.1f);break;
                case NativeTechniques.ChainHook:{float turn=k.HitAt>-50?k.HitAt:k.Arm>k.Release?k.Arm:k.Until;hit=Part(TechThrowStart,k.At,k.Release)||Part(TechThrowEnd,k.Release,Math.Max(turn,k.Release+.05f))||Part(set.Block,turn,Math.Max(k.Until,turn+.1f));}break;
                default:return false;
            }
            pick=got;return hit;
        }
    }
}
