using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The third set's sword, daggers, shield and greatsword: effects, previews, sounds and poses. The sword is the worked example of the set.
    public sealed partial class NativeTechniqueFx
    {
        private static readonly string[] MeleeBuilt={NativeTechniques.Disarm,NativeTechniques.Iaido,NativeTechniques.Challenge,NativeTechniques.MirrorDouble,NativeTechniques.Envenom,NativeTechniques.ChainHook,NativeTechniques.WarCry,NativeTechniques.GroundSlam,NativeTechniques.Lodestone,NativeTechniques.Sunder,NativeTechniques.ReapingSweep,NativeTechniques.BladeDrag};
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
                case NativeTechniques.WarCry:WarCry(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.GroundSlam:GroundSlam(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.Lodestone:Lodestone(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.Sunder:Sunder(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.ReapingSweep:ReapingSweep(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.BladeDrag:BladeDrag(s,scene,a,k,t,seed,q,low);return true;
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
        private static readonly Vector3 MirrorLilac=new Vector3(.6f,.4f,1),MirrorPale=new Vector3(.92f,.88f,1);
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
            void Limb(Vector3 p0,Vector3 p1,float w){if(!low)s.Line(false,p0,p1,color*(.32f*b),w*2.4f,false);s.Line(false,p0,p1,color*(.85f*b),w,false);}
            for(int sd=-1;sd<=1;sd+=2)
            {
                float side=sd>0?0:3.14159f,swing=(float)Math.Sin(ph+side),th=.85f*swing*stride,kn=(.25f+1.05f*Math.Max(0,-(float)Math.Cos(ph+side)))*stride+.04f;
                var hip=hips+r*(.1f*sd);var knee=hip+(f*(float)Math.Sin(th)-Up*(float)Math.Cos(th))*.46f;var sole=knee+(f*(float)Math.Sin(th-kn)-Up*(float)Math.Cos(th-kn))*.46f;Limb(hip,knee,.085f);Limb(knee,sole,.065f);
                float arm=-.8f*swing*stride+.08f,fore=arm+.9f*stride+.15f;var sh=chest+r*(.21f*sd)+Up*.02f;var elbow=sh+(f*(float)Math.Sin(arm)-Up*(float)Math.Cos(arm))*.29f+r*(.03f*sd);var hand=elbow+(f*(float)Math.Sin(fore)-Up*(float)Math.Cos(fore))*.28f;Limb(sh,elbow,.06f);Limb(elbow,hand,.05f);
            }
            s.Band(false,chest-r*.21f+Up*.02f,chest+r*.21f+Up*.02f,hips+r*.13f,hips-r*.13f,.55f*b,.55f*b,color);Limb(neck,chest,.05f);Limb(hips,chest,.13f);
            s.Sprite(false,head,color*(1.05f*b),.3f,Hot);
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
                    // A column of heat haze the size of a body, not a body: it fits any pose.
                    var feet=scene.Feet(a);int hairs=low?4:8;
                    for(int i=0;i<hairs;i++)
                    {
                        float ang=i*6.2831853f/hairs+t*.6f;var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));Vector3 last=feet+d*.3f+Up*.1f;
                        for(int j=1;j<=4;j++){float y=j/4f,w=.3f+.06f*(float)Math.Sin(t*19+i*2.1f+j*1.7f);var p=feet+d*(w*(1-.25f*y))+Up*(.1f+1.65f*y);s.Line(false,last,p,MirrorLilac*(.55f*env*flick),.018f,false);last=p;}
                    }
                    int n=Count(10,q,low);for(int i=0;i<n;i++){float ph=(t*1.3f+R(seed,i+60))%1,ang=6.2831853f*R(seed,i+61);s.Sprite(false,feet+new Vector3((float)Math.Cos(ang)*.34f,.1f+1.7f*ph,(float)Math.Sin(ang)*.34f),MirrorPale*(1.3f*env*(float)Math.Sin(3.14159f*ph)),.1f,Sparkle);}
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
                int lines=low?2:4;for(int i=0;i<lines;i++){float y=((t*.9f+i/(float)lines)%1)*1.8f;var c=at+Up*y+dir*(.1f*Clamp01(y));s.Line(false,c-rt*.26f,c+rt*.26f,MirrorPale*(1.1f*show),.018f,false);}
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

        // A fan on the floor, from `c` along `f`: its soft fill and its bright edge.
        private static void MeleeSector(ITechniqueFxSink s,Vector3 c,Vector3 f,Vector3 r,float radius,float half,Vector3 color,float fill,float edge,int n)
        {
            if(radius<.05f)return;Vector3 At(float ang,float rad)=>c+(f*(float)Math.Cos(ang)+r*(float)Math.Sin(ang))*rad;
            for(int i=0;i<n;i++)
            {
                float a0=-half+2*half*i/n,a1=-half+2*half*(i+1)/n;if(fill>0)s.Band(false,At(a0,radius*.06f),At(a1,radius*.06f),At(a1,radius),At(a0,radius),fill,fill,color);
                if(edge>0)s.Line(false,At(a0,radius),At(a1,radius),color*edge,.05f,false);
            }
            if(edge>0){s.Line(false,At(-half,radius*.06f),At(-half,radius),color*edge,.035f,false);s.Line(false,At(half,radius*.06f),At(half,radius),color*edge,.035f,false);}
        }

        // ---- Shield: War Cry --------------------------------------------------------------------------------------------
        // Before the bang its reach burns on the floor and the air draws in toward you, so anyone close can still get out. Then the shield rings: three shaking curtains of sound run out to the line and break on it.
        // While it lasts a ring of bars dances round your feet like a level meter, a beat goes out each second, and a gold crown turns over your head: the one to shoot.
        public const float CryReach=8;
        private static readonly Vector3 CryGold=new Vector3(1,.74f,.26f),CryHot=new Vector3(1,.9f,.6f);
        private static void WarCry(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var c=N(k.Point);var fl=c+Up*.04f;var feet=scene.Feet(a);var chest=scene.Chest(a,out var c0)?c0:feet+Up*1.25f;bool view=scene.Viewer==a.Id;int seg=low?28:56;
            if(t<k.Release)
            {
                float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));Ring(s,fl,CryReach,.08f,CryGold*(1.7f*u),seg);if(!view)s.Sprite(false,chest,CryHot*(1+3*u),.2f+.45f*u,Hot);
                int n=Count(18,q,low);for(int i=0;i<n;i++){float ang=6.2831853f*R(seed,i+1),ph=(u*1.3f+R(seed,i+2))%1;var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));var p=c+d*(CryReach*(1-ph))+Up*(.1f+1.1f*R(seed,i+3));s.Line(false,p,p+d*1.3f,CryGold*(1.4f*u*ph),.03f,false);}
                return;
            }
            float age=t-k.Release;
            if(!view)Flash(s,chest,age,.24f,CryGold,2.6f,seed);
            // The three curtains.
            for(int w=0;w<3;w++)
            {
                float own=age-.09f*w;if(own<0||own>.55f)continue;float g=own/.55f,rad=CryReach*OutCubic(g*1.1f),fade=(1-g*g)*(w==0?1:.7f),h=(1.5f-.3f*w)*(.5f+.5f*(1-g));
                for(int i=0;i<seg;i++)
                {
                    float a0=6.2831853f*i/seg,a1=6.2831853f*(i+1)/seg;float R0(float ang)=>rad*(1+.03f*(float)Math.Sin(ang*14+t*40));var p0=fl+new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*R0(a0);var p1=fl+new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1))*R0(a1);
                    s.Band(false,p0,p1,p1+Up*h,p0+Up*h,.85f*fade,.85f*fade,CryGold*1.5f);
                }
                Ring(s,fl,rad,.14f,CryHot*(1.5f*fade),seg);
            }
            // The line it broke on.
            float line=1-Smooth((age-.4f)/.6f);if(line>0)Ring(s,fl,CryReach,.1f,CryGold*(1.4f*line),seg);Dust(s,c,age,.6f,Count(12,q,low),2.6f,new Vector3(.36f,.32f,.24f),.5f,seed+1);Sparks(s,chest,age,.4f,Count(16,q,low),6,CryHot,seed+2,8,.024f);
            // The rally.
            if(t>=k.Until)return;float env=Smooth(age/.2f)*(1-Smooth((t-(k.Until-.3f))/.3f)),vb=view?.4f:1;
            int bars=low?10:20;for(int i=0;i<bars;i++)
            {
                float ang=i*6.2831853f/bars,h=.12f+.6f*(float)Math.Abs(Math.Sin(t*6+i*2.4f+1.3f*Math.Sin(t*2.1f+i)))+1.3f*(1-Clamp01(age/.35f));var p=feet+new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*1.02f+Up*.03f;
                s.Line(false,p,p+Up*h,CryGold*(1.6f*env*vb),.06f,false);if(!low)s.Sprite(false,p+Up*h,CryHot*(1.5f*env*vb),.08f,Hot);
            }
            float beat=age%1;Ring(s,feet+Up*.05f,.95f+2.7f*OutCubic(beat),.07f,CryGold*(1.1f*(1-beat)*env*vb),low?16:32);
            if(!view)
            {
                var top=feet+Up*2.18f;int points=low?5:7;Vector3 last=default;
                for(int i=0;i<=points;i++){float ang=t*1.1f+i*6.2831853f/points;var p=top+new Vector3((float)Math.Cos(ang)*.25f,0,(float)Math.Sin(ang)*.25f);if(i>0){s.Line(false,last,p,CryGold*(1.8f*env),.035f,false);s.Line(false,p,p+Up*.19f,CryHot*(2.1f*env),.05f,true);}last=p;}
                s.Sprite(false,top+Up*.06f,CryGold*(.7f*env),.55f,Soft);s.Glow(feet+Up*1,CryGold*(.16f*env),0,2.4f,Core,0,seed+5);
            }
        }

        // ---- Shield: Ground Slam ----------------------------------------------------------------------------------------
        // The floor kicks at the take-off. All through the leap its circle burns where it will land: a bright rim, chevrons pointing in, a ring closing to the moment and a thread straight down from the falling pilot.
        // The landing breaks the floor to the rim: a white shock, spears of light going up off it, slabs of floor thrown in the air.
        public const float SlamReach=4;
        private static readonly Vector3 SlamBlue=new Vector3(.45f,.75f,1),SlamPale=new Vector3(.82f,.93f,1),SlamRock=new Vector3(.17f,.17f,.2f);
        private static Vector3 SlamAt(NativeTechnique k,float t){float u=Clamp01((t-k.Release)/Math.Max(.05f,k.Arm-k.Release));return Vector3.Lerp(N(k.From),N(k.Point),u)+Up*(12*u*(1-u));}
        private static void GroundSlam(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var p=N(k.Point);var from=N(k.From);var fl=p+Up*.04f;float land=k.HitAt>-50?k.HitAt:k.Arm;int seg=low?28:56;
            if(t<k.Release){float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Glow(from+Up*.05f,SlamBlue*(1.2f*u),0,1.6f*u,RingGlow,u,seed);return;}
            float air=t-k.Release;
            if(air<.4f){if(air<.3f)Ring(s,from+Up*.05f,1.5f*OutCubic(air/.3f),.18f,SlamBlue*(1.5f*(1-air/.3f)),low?14:24);Dust(s,from,air,.4f,Count(10,q,low),1.6f,SlamRock*2,.55f,seed+1);}
            if(t<land)
            {
                float u=Clamp01(air/Math.Max(.05f,land-k.Release)),env=Smooth(u*5);
                Ring(s,fl,SlamReach,.13f,SlamBlue*(1.9f*env),seg);Ring(s,fl,SlamReach*.5f,SlamReach,SlamBlue*((.06f+.18f*u)*env),low?16:32);Ring(s,fl,Math.Max(.05f,SlamReach*(1-u*u)),.1f,SlamPale*(2.2f*env),seg);
                int chev=low?6:12;for(int i=0;i<chev;i++){float ang=i*6.2831853f/chev,x=(t*2.2f+i*.5f)%1;var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));var side=RightOf(d);var at=fl+d*(SlamReach*(.94f-.3f*x));var tip=at-d*.3f;float b=1.7f*env*(1-x);s.Line(false,at+side*.28f,tip,SlamPale*b,.05f,true);s.Line(false,at-side*.28f,tip,SlamPale*b,.05f,true);}
                var head=SlamAt(k,t)+Up*.9f;s.Line(false,head,fl,SlamBlue*(.35f+1.1f*u*u),.05f,false);s.Sprite(false,fl,SlamPale*(1+2.4f*u*u),.3f+.5f*u,Hot);
                int n=low?6:12;var prev=head;for(int i=1;i<=n;i++){float tt=t-i*.03f;if(tt<k.Release)break;var o=SlamAt(k,tt)+Up*.9f;float b=1-i/(float)n;s.Line(false,o,prev,SlamBlue*(1.5f*b),.2f*b+.02f,false);prev=o;}
                if(u>.5f&&!low){float w=(u-.5f)*2;int m=Count(10,q,low);for(int i=0;i<m;i++){float ph=(t*3+R(seed,i+30))%1,ang=6.2831853f*R(seed,i+31);var o=head+new Vector3((float)Math.Cos(ang)*.65f,.8f-1.7f*ph,(float)Math.Sin(ang)*.65f);s.Line(false,o,o+Up*.8f,SlamPale*(1.5f*w*(float)Math.Sin(3.14159f*ph)),.025f,false);}}
                return;
            }
            float since=t-land;
            Flash(s,p+Up*.3f,since,.3f,SlamBlue,3.8f,seed+2);Cracks(s,p,since,1.2f,low?9:17,SlamReach*.95f,SlamBlue+new Vector3(.3f,.2f,.1f),seed+3);
            if(since<.3f){float g=since/.3f;Ring(s,fl,SlamReach*OutCubic(g),.8f*(1-g)+.12f,SlamPale*(2.4f*(1-g)),seg);}
            if(since<.5f){float g=since/.5f;Ring(s,fl,SlamReach,.24f*(1-g)+.05f,SlamBlue*(2.4f*(1-g)),seg);}
            if(since<.45f){int n=low?12:24;float g=since/.45f;for(int i=0;i<n;i++){float ang=i*6.2831853f/n+.3f*R(seed,i+40);var b=fl+new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*(SlamReach*(.35f+.65f*R(seed,i+41)));float h=(1.3f+1.5f*R(seed,i+42))*OutCubic(g*1.6f);s.Line(false,b+Up*(h*g),b+Up*h,SlamPale*(2.2f*(1-g)),.06f,true);}}
            if(since<.9f){int n=Count(20,q,low);for(int i=0;i<n;i++){float rr=SlamReach*(float)Math.Sqrt(R(seed,i+50)),ang=6.2831853f*R(seed,i+51),tt=since-rr/26f;if(tt<0)continue;float y=(5+3.5f*R(seed,i+52))*tt-7*tt*tt;if(y<0)continue;var at=p+new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*(rr+1.2f*tt)+Up*(y+.05f);float size=.14f+.2f*R(seed,i+53);s.Glow(at,SlamRock,.92f,size,Core,0,seed+i);s.Sprite(false,at+Up*.03f,SlamBlue*.7f,size*.6f,Hot);}}
            Sparks(s,p+Up*.15f,since,.6f,Count(30,q,low),8.5f,SlamPale,seed+4,9,.03f,Up*.9f);Dust(s,p,since,1,Count(16,q,low),SlamReach*1.05f,new Vector3(.33f,.33f,.36f),.75f,seed+5);
        }

        // ---- Shield: Lodestone ------------------------------------------------------------------------------------------
        // A fan burns on the floor ahead and only ahead, showing where it reaches and that behind it is safe. Lines of force stream in along the fan to the shield and hoops close on it. A shot it takes bends in amber along a line of force and sticks,
        // standing out of the rim, hot against the blue. The release throws them all out in a spread with a blast off the shield's face.
        public const float LodeReach=7,LodeThrow=6;
        private static readonly Vector3 LodeBlue=new Vector3(.55f,.8f,1),LodePale=new Vector3(.85f,.95f,1),LodeShot=new Vector3(1,.6f,.22f);
        // Shots held: `Arm` in play; the preview's stands still, so it counts up to it.
        private static int LodeHeld(NativeTechnique k,float t)=>Math.Max(0,Math.Min(8,Math.Min((int)Math.Round(k.Arm),(int)Math.Ceiling((t-k.Release)*3))));
        private static void Lodestone(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var chest=scene.Chest(a,out var c0)?c0:feet+Up*1.25f;var f=Flat(N(k.Dir),Vector3.UnitZ);var r=RightOf(f);var face=chest+f*.62f;bool view=scene.Viewer==a.Id,released=k.Recast(t);float vb=view?.45f:1,stop=released?k.RecastAt:k.Until,env=Smooth((t-k.At)/Math.Max(.05f,k.Release-k.At))*(1-Smooth((t-stop)/.08f));const float Half=1.05f;
            if(env>0)
            {
                MeleeSector(s,feet+Up*.04f,f,r,LodeReach,Half,LodeBlue,.08f*env,.8f*env,low?12:24);
                int lines=low?5:10;
                for(int i=0;i<lines;i++)
                {
                    float x=i/(lines-1f)*2-1,ang=x*Half;var p0=feet+(f*(float)Math.Cos(ang)+r*(float)Math.Sin(ang))*LodeReach+Up*(.2f+1.5f*R(seed,i+1));var p1=chest+f*(LodeReach*.42f)+r*(x*.9f);Vector3 B(float u){float v=1-u;return p0*(v*v)+p1*(2*u*v)+face*(u*u);}
                    int dashes=low?2:3;for(int j=0;j<dashes;j++){float u=(t*.85f+j/(float)dashes+R(seed,i+20))%1;float b=env*(.35f+1.1f*u*u)*vb;s.Line(false,B(u),B(Math.Min(1,u+.11f)),LodeBlue*(1.3f*b),.045f,false);}
                }
                int hoops=low?2:3;for(int j=0;j<hoops;j++){float ph=(t*1.5f+j/(float)hoops)%1;Hoop(s,face+f*(1.5f*(1-ph)),f,.62f+.7f*(1-ph),.05f,LodeBlue*(1.3f*ph*env*vb),low?12:22,1);}
                Hoop(s,face,f,.62f,.06f,LodePale*(1.7f*env*(.8f+.2f*(float)Math.Sin(t*11))*vb),low?14:26,1);s.Glow(face,LodeBlue*(.7f*env*vb),0,1.5f,Core,0,seed+2);
                int held=LodeHeld(k,t);for(int i=0;i<held;i++){float ang=i*.7854f+t*.35f;var rim=face+(r*(float)Math.Cos(ang)+Up*(float)Math.Sin(ang))*(.52f+.02f*(float)Math.Sin(t*60+i));var tip=rim+f*(.24f+.04f*(float)Math.Sin(t*9+i));s.Line(false,rim,tip,LodeShot*(2*env),.07f,false);s.Sprite(false,tip,(LodeShot+Vector3.One*.3f)*(2.3f*env),.22f,Hot);}
            }
            // The catch: the shot bends along a line of force onto the shield.
            if(k.Hit(t)&&(!released||k.HitAt<k.RecastAt))
            {
                float since=t-k.HitAt;var from=N(k.To);
                if(since<.3f){var bend=chest+f*(Math.Max(1,Vector3.Dot(from-chest,f))*.5f)+r*(Vector3.Dot(from-chest,r)*.2f);Vector3 B(float u){float v=1-u;return from*(v*v)+bend*(2*u*v)+face*(u*u);}float g=Clamp01(since/.16f),fade=1-Clamp01((since-.16f)/.14f);int n=low?5:10;for(int i=0;i<n;i++){float u0=g*i/n,u1=g*(i+1)/n;s.Line(false,B(u0),B(u1),LodeShot*(2.2f*fade*(.4f+.6f*(i+1f)/n)),.05f,false);}s.Sprite(false,B(g),(LodeShot+Vector3.One*.4f)*(2.6f*fade),.26f,Hot);}
                Flash(s,from,since,.14f,LodeShot,.6f,seed+3);Flash(s,face,since-.16f,.18f,LodePale,1,seed+4);Sparks(s,face,since-.16f,.3f,Count(10,q,low),4,LodeShot,seed+5,8,.02f,f*.6f);
            }
            if(released)
            {
                float since=t-k.RecastAt;int shots=LodeHeld(k,k.RecastAt);Flash(s,face,since,.26f,LodePale,shots>0?2.4f:1.1f,seed+6);
                if(since<.35f){float g=since/.35f;Hoop(s,face+f*(.3f+1.7f*OutCubic(g)),f,.6f+1.8f*OutCubic(g),.14f*(1-g)+.02f,LodePale*(2.2f*(1-g)),low?14:26,1);if(shots>0)MeleeSector(s,feet+Up*.05f,f,r,LodeThrow*OutCubic(g*1.6f),.5f,LodeShot,.18f*(1-g),1.3f*(1-g),low?6:12);}
                for(int i=0;i<shots;i++)
                {
                    float x=shots>1?i/(shots-1f)*2-1:0,ang=x*.45f+.08f*(R(seed,i+90)-.5f),start=.012f*i,g=Clamp01((since-start)/.2f),fade=1-Clamp01((since-start-.2f)/.1f);if(since<start||fade<=0)continue;var d=Vector3.Normalize(f*(float)Math.Cos(ang)+r*(float)Math.Sin(ang)+Up*(.18f*(R(seed,i+91)-.5f)));
                    s.Line(false,face+d*(LodeThrow*OutCubic(Math.Max(0,g-.35f))),face+d*(LodeThrow*OutCubic(g)),(LodeShot+Vector3.One*.25f)*(2.8f*fade),.07f,true);s.Sprite(false,face+d*(LodeThrow*OutCubic(g)),LodeShot*(2.3f*fade),.26f,Hot);
                }
                Sparks(s,face,since,.4f,Count(16,q,low),6,LodePale,seed+7,8,.022f,f);
            }
        }

        // Fissures fanned out from `p` along `f`, white-hot and cooling, each with a curtain of light standing out of it for its first moments: the floor sundered, not just scratched.
        private static void MeleeFissures(ITechniqueFxSink s,Vector3 p,Vector3 f,Vector3 r,float half,float reach,float age,float life,int n,Vector3 hot,Vector3 white,float curtain,long seed)
        {
            if(age<0||age>life)return;float grow=OutCubic(age/.14f),fade=1-Smooth(age/life),heat=1-Clamp01(age/.5f),rise=curtain*Math.Max(0,1-age/.55f)*grow;var col=Vector3.Lerp(hot,white,heat);
            for(int j=0;j<n;j++)
            {
                float ang=((j+.5f)/n*2-1)*half+.12f*(R(seed,j*9)-.5f),len=reach*(.7f+.3f*R(seed,j*9+1))*grow;var d=f*(float)Math.Cos(ang)+r*(float)Math.Sin(ang);var side=RightOf(d);var prev=p+Up*.03f;
                for(int i=1;i<=5;i++)
                {
                    float u=i/5f;var at=p+d*(len*u)+side*((R(seed,j*9+i+2)-.5f)*.55f*u)+Up*.03f;s.Line(false,prev,at,col*(2.1f*fade),.085f*(1-.6f*u),false);
                    if(rise>.01f){float h0=rise*(1-.5f*(i-1)/5f),h1=rise*(1-.5f*u);s.Band(false,prev,at,at+Up*h1,prev+Up*h0,.8f*fade,.8f*fade,hot*1.4f);}
                    prev=at;
                }
            }
        }

        // ---- Greatsword: Sunder -----------------------------------------------------------------------------------------
        // Slow and plain: the blade heats as it climbs, the wedge it will break lights on the floor and a mark burns where it will land. Then a broad crescent comes over and down, and the floor splits in a fan of white-hot fissures with light standing out of them.
        // If it struck someone a star of plate breaks out at chest height and shards of armour fly. The victims' own cracks of light come with their marks.
        public const float SunderReach=3;
        private static readonly Vector3 SunderHot=new Vector3(1,.46f,.16f),SunderWhite=new Vector3(1,.85f,.6f);
        private const float SunderHalf=.7f;
        private static void Sunder(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var p=N(k.Point);var f=Flat(N(k.Dir),Vector3.UnitZ);var r=RightOf(f);var feet=scene.Feet(a);bool view=scene.Viewer==a.Id;
            if(t<k.Release)
            {
                float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));bool has=view?scene.ViewBlade(false,out var b0,out var b1):scene.Blade(a,false,out b0,out b1);
                if(has){s.Line(view,b0,b1,SunderHot*(.5f+1.6f*u),view?.03f:.11f,false);s.Line(view,b0,b1,SunderWhite*(1.7f*u),view?.008f:.022f,true);s.Sprite(view,b1,SunderWhite*(1+3*u*u),view?.06f:.2f+.45f*u,Sparkle);}
                var fl=feet+Up*.04f;var mark=new Vector3(p.X,fl.Y+.01f,p.Z);MeleeSector(s,fl,f,r,SunderReach,SunderHalf,SunderHot,.1f+.3f*u*u,.45f+1.2f*u,low?8:16);s.Line(false,fl,mark,SunderWhite*(.4f+1.5f*u),.05f,false);Ring(s,mark,.12f+.5f*(1-u),.06f,SunderWhite*(1.6f*u),low?8:14);s.Sprite(false,mark,SunderWhite*(.5f+2.2f*u),.25f+.3f*u,Hot);
                if(!low&&has){int n=Count(8,q,low);for(int i=0;i<n;i++){float ph=(t*1.6f+R(seed,i+1))%1;s.Glow(Vector3.Lerp(b0,b1,R(seed,i+2))+new Vector3((R(seed,i+3)-.5f)*.3f,.6f*ph,(R(seed,i+4)-.5f)*.3f),SunderHot*(1.6f*(1-ph)*u),0,.06f,Core,0,seed+i);}}
                return;
            }
            float age=t-k.Release;
            // The crescent, overhead to floor, broad enough to read from behind.
            if(age<.2f){var pivot=feet+Up*1+f*.35f;float fade=1-Clamp01(age/.2f);int n=low?7:14;Vector3 P(float th)=>pivot+(Up*(float)Math.Cos(th)+f*(float)Math.Sin(th))*2.15f;var w=r*.32f;for(int i=0;i<n;i++){float x0=i/(float)n,x1=(i+1f)/n,th0=-.35f+2.2f*x0,th1=-.35f+2.2f*x1;s.Band(false,P(th0)-w,P(th1)-w,P(th1)+w,P(th0)+w,(.2f+.8f*x0)*fade*1.2f,(.2f+.8f*x1)*fade*1.2f,SunderWhite*1.5f);}}
            Flash(s,p+Up*.2f,age,.26f,SunderHot,3.2f,seed);MeleeFissures(s,p,f,r,SunderHalf,SunderReach,age,1.1f,low?3:5,SunderHot,SunderWhite,.75f,seed+1);Cracks(s,p,age,.7f,low?4:7,.9f,SunderWhite,seed+2);
            Dust(s,p,age,.7f,Count(12,q,low),2.4f,new Vector3(.3f,.26f,.22f),.7f,seed+3);Sparks(s,p+Up*.1f,age,.55f,Count(26,q,low),8,SunderWhite,seed+4,10,.03f,f*.8f+Up);
            if(age<.3f)Ring(s,p+Up*.05f,2.4f*OutCubic(age/.3f),.16f*(1-age/.3f)+.03f,SunderHot*(1.7f*(1-age/.3f)),low?14:24);
            if(k.Hit(t))
            {
                float since=t-k.HitAt;var c=p+Up*1;MeleeShards(s,c,since,.75f,Count(20,q,low),7,SunderHot+Vector3.One*.25f,seed+5,9,.17f,f*.7f+Up*.4f);Flash(s,c,since,.2f,SunderWhite,1.6f,seed+6);
                if(since<.22f){float g=since/.22f;s.Sprite(false,c,SunderWhite*(3.4f*(1-g)),1.7f,Sparkle);int n=low?4:7;for(int i=0;i<n;i++){float ang=i*6.2831853f/n+.4f;var d=r*(float)Math.Cos(ang)+Up*(float)Math.Sin(ang);s.Line(false,c+d*(.25f+.9f*g),c+d*(.7f+1.4f*g),SunderWhite*(2.6f*(1-g)),.09f,true);}}
            }
        }

        // ---- Greatsword: Reaping Sweep ----------------------------------------------------------------------------------
        // The blade drops to shin height, a hair lies along the floor where it starts and the arc it will cover is drawn round you. Then a low flat scythe of silver swings 300 degrees with a short fence of light on its leading edge, plainly under the knee:
        // jump it. What it hooks is dragged in: claws of light curling from the rim to a ring close by, and dust thrown up on that ring.
        public const float ReapReach=4,ReapInto=1.6f,ReapArc=5.236f;
        private static readonly Vector3 ReapSilver=new Vector3(.74f,.82f,.95f),ReapPale=new Vector3(.93f,.96f,1);
        private static void ReapingSweep(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var c=N(k.Point);var fl=c+Up*.04f;var f=Flat(N(k.Dir),Vector3.UnitZ);float a0=(float)Math.Atan2(f.Z,f.X);const float Shin=.38f,Inner=.7f,Sweep=.24f;int seg=low?24:48;
            Vector3 D(float ang)=>new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));
            if(t<k.Release)
            {
                float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));int n=(int)(seg*u);for(int i=0;i<n;i++){float x0=a0+ReapArc*i/seg,x1=a0+ReapArc*(i+1)/seg;s.Line(false,fl+D(x0)*ReapReach,fl+D(x1)*ReapReach,ReapSilver*(1.5f*u),.05f,false);}
                s.Line(false,c+D(a0)*Inner+Up*Shin,c+D(a0)*ReapReach+Up*Shin,ReapPale*(2.2f*u),.035f,true);s.Line(false,fl+D(a0)*Inner,fl+D(a0)*ReapReach,ReapSilver*(1.2f*u),.05f,false);s.Sprite(false,c+D(a0)*ReapReach+Up*Shin,ReapPale*(2.6f*u),.2f+.2f*u,Sparkle);
                return;
            }
            float age=t-k.Release,g=Clamp01(age/Sweep),lead=a0+ReapArc*(1-(1-g)*(1-g)),fade=1-Clamp01((age-Sweep)/.24f);
            if(fade>0)
            {
                // The scythe's flat wake, brightest behind its edge.
                int n=low?18:36;const float Trail=2.8f;
                for(int i=0;i<n;i++)
                {
                    float b0=i/(float)n,b1=(i+1f)/n,x0=lead-Trail*b0,x1=lead-Trail*b1;if(x0<=a0)break;if(x1<a0)x1=a0;float e0=(1-b0)*(1-b0)*fade,e1=(1-b1)*(1-b1)*fade;Vector3 Y(Vector3 v)=>v+Up*Shin;
                    s.Band(false,Y(c+D(x0)*Inner),Y(c+D(x1)*Inner),Y(c+D(x1)*ReapReach),Y(c+D(x0)*ReapReach),e0*1.3f,e1*1.3f,ReapSilver*1.3f);s.Line(false,c+D(x0)*(ReapReach-.04f)+Up*.03f,c+D(x1)*(ReapReach-.04f)+Up*.03f,ReapPale*(1.8f*Math.Max(e0,.25f*fade)),.05f,false);
                }
                if(g<1){var e=D(lead);var i0=c+e*Inner;var o0=c+e*ReapReach;s.Band(false,i0+Up*.05f,o0+Up*.05f,o0+Up*.74f,i0+Up*.74f,1.2f,1.2f,ReapPale);s.Line(false,i0+Up*Shin,o0+Up*Shin,ReapPale*2.8f,.07f,true);Sparks(s,o0+Up*.06f,(t*10)%.3f,.3f,low?3:7,5.5f,ReapPale,seed+(long)(t*10)*31,9,.024f,RightOf(e)*-1.1f+Up*.5f);s.Sprite(false,o0+Up*Shin,ReapPale*3,.34f,Sparkle);}
            }
            float scar=1-Smooth((age-.2f)/.7f);if(scar>0&&!low){int n=seg;for(int i=0;i<n;i++){float x0=a0+ReapArc*i/n,x1=a0+ReapArc*(i+1)/n;if(x0>lead)break;s.Line(false,fl+D(x0)*(ReapReach*.97f),fl+D(x1)*(ReapReach*.97f),ReapSilver*(.7f*scar),.03f,false);}}
            Dust(s,c,age,.6f,Count(12,q,low),ReapReach,new Vector3(.34f,.34f,.36f),.45f,seed+1);
            if(!k.Hit(t))return;float since=t-k.HitAt;
            // Claws of light from the rim to the ring they are dragged to.
            if(since<.5f)
            {
                float u=since/.5f,go=OutCubic(u),b=1-u*u;int n=Count(14,q,low),parts=low?3:6;
                for(int i=0;i<n;i++)
                {
                    float start=a0+ReapArc*R(seed,i+10);float Rad(float x)=>ReapReach-(ReapReach-ReapInto)*x;float Ang(float x)=>start+.8f*x;
                    Vector3 last=fl+D(Ang(go))*Rad(go);for(int j=1;j<=parts;j++){float x=Math.Max(0,go-.45f*j/parts);var o=fl+D(Ang(x))*Rad(x);s.Line(false,o,last,ReapPale*(2*b*(1-(j-1f)/parts)),.06f*(1-.6f*(j-1f)/parts),true);last=o;}
                }
            }
            if(since<.7f){float u=since/.7f;Ring(s,fl,ReapInto,.14f*(1-u)+.03f,ReapPale*(1.9f*(1-u)*Smooth(since/.18f)),seg);Dust(s,c,since-.15f,.55f,Count(10,q,low),ReapInto,new Vector3(.36f,.36f,.38f),.6f,seed+2);}
        }

        // ---- Greatsword: Blade Drag -------------------------------------------------------------------------------------
        // Loud and bright: the point ploughs the floor, throwing a rooster tail of sparks and leaving a gouge that runs white-hot, orange and dull red as it cools. A wedge burns ahead of you, stronger the longer you drag: that is where it will rip, and you turn badly.
        // The rise is a broad crescent torn upward out of the floor, spikes of slag bursting along the fan, slabs of floor and a fountain of sparks thrown high: bigger the longer the charge.
        public const float DragCone=2.6f;
        private static readonly Vector3 DragWhite=new Vector3(1,.88f,.62f),DragHot=new Vector3(1,.52f,.16f),DragCool=new Vector3(.75f,.13f,.03f);
        private const float DragHalf=.62f;
        // Where the point is: along the path where it is already laid (the preview's), else behind the pilot, since in play the path ends about where the pilot is.
        private static Vector3 DragHead(NativeTechnique k,float t,Vector3 feet,Vector3 f)
        {
            var p=k.Path;if(p==null||p.Length==0)return feet-f*.9f;float x=Math.Max(0,(t-k.At)/NativeTechniques.PathStep);int i=(int)x;if(i+1<p.Length)return Vector3.Lerp(N(p[i]),N(p[i+1]),x-i);
            var last=N(p[p.Length-1]);return Vector3.Distance(last,feet)<2.5f?feet-f*.9f:last;
        }
        private static void BladeDrag(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var f=Flat(N(k.Dir),Vector3.UnitZ);var r=RightOf(f);bool view=scene.Viewer==a.Id,risen=k.Recast(t);float stop=risen?k.RecastAt:k.Until,charge=risen?Clamp01(k.Arm):Clamp01((t-k.At)/2);
            var head=DragHead(k,Math.Min(t,stop),feet,f);var path=k.Path;
            // The gouge, cooling behind the point.
            if(path!=null)
            {
                int step=low||q<.5f?2:1;Vector3 prev=default;bool laid=false;
                for(int i=0;i<path.Length;i+=step)
                {
                    float born=k.At+i*NativeTechniques.PathStep;if(born>t||born>stop)break;float cool=(t-born)/1.5f;var pt=N(path[i])+Up*.03f+r*(.07f*(R(seed,i+1)-.5f));
                    if(laid&&cool<1){float e=1-Smooth(cool),heat=Clamp01(cool*3);var col=heat<.5f?Vector3.Lerp(DragWhite,DragHot,heat*2):Vector3.Lerp(DragHot,DragCool,heat*2-1);Strip(s,prev,pt,.06f,col*2.1f,e,e);Strip(s,prev,pt,.24f,col,.4f*e,.4f*e);}
                    if(!low&&cool<1&&i%3==0){float ph=(t*1.2f+R(seed,i+2))%1;s.Smoke(pt+Up*(.08f+.6f*ph),new Vector3(.16f,.14f,.13f),.28f*(1-ph)*(1-cool),.35f+.4f*ph,.2f+.5f*ph,seed+i,pt.Y-.05f);}
                    prev=pt;laid=true;
                }
                if(laid&&!risen)Strip(s,prev,head+Up*.03f,.06f,DragWhite*2.2f,1,1);
            }
            if(!risen&&t<k.Until)
            {
                float env=Smooth((t-k.At)/.1f);
                Spray(s,head+Up*.05f,t,.22f,Count(13,q,low),7.5f,DragWhite,seed+1,9,.032f,-f*1.5f+Up*1.2f);if(!low){Spray(s,head+Up*.05f,t,.26f,Count(6,q,low),5,DragHot,seed+2,9,.024f,r*1.2f+Up*.6f);Spray(s,head+Up*.05f,t,.26f,Count(6,q,low),5,DragHot,seed+3,9,.024f,-r*1.2f+Up*.6f);}
                s.Glow(head+Up*.06f,DragHot*(1.8f*env),0,1+.7f*charge,Core,0,seed+4);s.Sprite(false,head+Up*.05f,DragWhite*(2.8f*env),.3f,Hot);
                bool has=view?scene.ViewBlade(false,out var b0,out var b1):scene.Blade(a,false,out b0,out b1);if(has){s.Line(view,b0,b1,DragHot*((.5f+1.3f*charge)*env),view?.03f:.1f,false);s.Sprite(view,b1,DragWhite*((1+1.5f*charge)*env),view?.06f:.22f,Hot);}
                MeleeSector(s,head+f*.9f+Up*.04f,f,r,DragCone,DragHalf,DragHot,(.05f+.22f*charge)*env,(.3f+1.1f*charge)*env,low?6:12);
                return;
            }
            if(!risen)return;
            float since=t-k.RecastAt,size=.6f+.4f*charge;var at=DragHead(k,k.RecastAt,feet,f)+f*.9f;var fl=at+Up*.04f;
            // The crescent, torn up out of the floor ahead.
            if(since<.3f)
            {
                int n=low?7:14;float g=OutCubic(since/.14f),fade=1-Smooth(since/.3f);var w=r*(.3f+.5f*charge);Vector3 P(float th)=>at+Up*1.05f+(f*(float)Math.Cos(th)+Up*(float)Math.Sin(th))*(DragCone*(.75f+.25f*size));
                for(int i=0;i<n;i++){float x0=g*i/n,x1=g*(i+1)/n,th0=-.5f+2.1f*x0,th1=-.5f+2.1f*x1;s.Band(false,P(th0)-w,P(th1)-w,P(th1)+w,P(th0)+w,(.2f+.8f*x0/Math.Max(.05f,g))*fade*1.3f,(.2f+.8f*x1/Math.Max(.05f,g))*fade*1.3f,DragWhite*1.6f);}
            }
            Flash(s,at+Up*.4f,since,.3f,DragHot,2.4f+1.6f*charge,seed+5);MeleeFissures(s,at,f,r,DragHalf,DragCone*(.7f+.3f*size),since,1,low?3:5,DragHot,DragWhite,.45f,seed+6);
            // Spikes of slag bursting along the fan, near ones first.
            int spikes=Count(14,q,low);for(int i=0;i<spikes;i++){float ang=(R(seed,i+20)*2-1)*DragHalf,rad=DragCone*(.25f+.75f*(float)Math.Sqrt(R(seed,i+21))),tt=since-rad/16;if(tt<0)continue;float h=(.8f+1.5f*R(seed,i+22))*size*OutCubic(tt/.1f)*(1-Smooth((tt-.12f)/.35f));if(h<=.01f)continue;var b=fl+(f*(float)Math.Cos(ang)+r*(float)Math.Sin(ang))*rad;s.Line(false,b,b+Up*h,DragWhite*2.2f,.12f,true);s.Sprite(false,b+Up*.06f,DragHot*1.5f,.26f,Hot);}
            // Slabs of floor.
            if(since<.9f){int n=Count(12,q,low);for(int i=0;i<n;i++){float ang=(R(seed,i+30)*2-1)*DragHalf,rad=DragCone*R(seed,i+31),tt=since-rad/20;if(tt<0)continue;float y=(4.5f+3*size*R(seed,i+32))*tt-7*tt*tt;if(y<0)continue;var o=fl+(f*(float)Math.Cos(ang)+r*(float)Math.Sin(ang))*(rad+1.4f*tt)+Up*y;float sz=.12f+.18f*R(seed,i+33);s.Glow(o,new Vector3(.14f,.11f,.1f),.92f,sz,Core,0,seed+i);s.Sprite(false,o,DragHot*.9f,sz*.55f,Hot);}}
            Sparks(s,at+Up*.1f,since,.7f,Count(30,q,low),8+3*charge,DragWhite,seed+7,9,.03f,Up*1.5f+f*.6f);Dust(s,at+f*.9f,since,.8f,Count(12,q,low),2.2f,new Vector3(.3f,.26f,.22f),.7f,seed+8);
            if(k.Hit(t)){float got=t-k.HitAt;var c=at+f*1.3f+Up*1.1f;Flash(s,c,got,.22f,DragWhite,1.7f,seed+9);MeleeShards(s,c,got,.6f,Count(12,q,low),6.5f,DragHot+Vector3.One*.2f,seed+10,8,.14f,Up);}
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
                case NativeTechniques.WarCry:k.Release=t0+.3f;k.Point=V3Of(feet);k.Until=k.Release+4;k.RecoverUntil=k.Release+.25f;return true;
                case NativeTechniques.GroundSlam:k.Release=t0+.14f;k.From=V3Of(feet);k.Point=V3Of(feet+f*(first?5:2.6f));k.Arm=k.Release+.5f;k.HitAt=k.Arm;k.Until=k.Arm+.25f;k.RecoverUntil=k.Until+.15f;return true;
                case NativeTechniques.Lodestone:k.Release=t0+.2f;k.Dir=V3Of(f);k.Arm=first?6:2;k.HitAt=k.Release+(first?.95f:1.3f);k.To=V3Of(first?feet+f*5.5f+r*2+up*1.4f:feet+f*4.5f-r*2.6f+up*1);k.RecastAt=k.Release+(first?1.8f:2.5f);k.Until=k.RecastAt+.3f;k.RecoverUntil=k.Until;return true;
                case NativeTechniques.Sunder:k.Release=t0+.55f;k.Point=V3Of(feet+f*2.2f);k.Dir=V3Of(f);k.Until=k.Release+.3f;k.RecoverUntil=k.Until+.3f;if(first)k.HitAt=k.Release;return true;
                case NativeTechniques.ReapingSweep:k.Release=t0+.25f;k.Point=V3Of(feet);k.Dir=V3Of(f);k.Until=k.Release+.3f;k.RecoverUntil=k.Until+.25f;if(first)k.HitAt=k.Release+.12f;return true;
                case NativeTechniques.BladeDrag:
                {
                    float drag=first?1.5f:2,step=NativeTechniques.PathStep;k.Release=t0+.05f;var path=new V3[(int)Math.Ceiling(drag/step)+1];Vector3 At(float x)=>feet+f*(.2f+4.5f*x)+r*((float)Math.Sin(x*1.6f)*.9f);
                    for(int i=0;i<path.Length;i++)path[i]=V3Of(At(i*step));k.Path=path;k.Dir=V3Of(Vector3.Normalize(At(drag)-At(drag-.1f)));k.Arm=drag/2;k.RecastAt=t0+drag;k.Until=k.RecastAt+.3f;k.RecoverUntil=k.Until+.25f;if(first)k.HitAt=k.RecastAt+.06f;return true;
                }
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
                case NativeTechniques.WarCry:return 5.4f;
                case NativeTechniques.GroundSlam:return 3.2f;
                case NativeTechniques.Lodestone:return 4.4f;
                case NativeTechniques.Sunder:return 2.8f;
                case NativeTechniques.ReapingSweep:return 2.4f;
                case NativeTechniques.BladeDrag:return 4.2f;
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
                case NativeTechniques.WarCry:if(m==Release){n=S(o,n,"melee.shield.bash","shield",1);n=S(o,n,"blast.kinetic","dash",.7f);n=S(o,n,"blast.quake","stone",.3f);}else if(m==End)n=S(o,n,"melee.shield.block","parry",.25f);break;
                case NativeTechniques.GroundSlam:if(m==Release){n=S(o,n,"move.jump","jump",.8f);n=S(o,n,"melee.shield.bash","shield",.3f);}else if(m==Hit){atPoint=true;n=S(o,n,"blast.quake","stone",.9f);n=S(o,n,"melee.shield.bash","shield",.9f);n=S(o,n,"move.land","explosion",.55f);}break;
                case NativeTechniques.Lodestone:if(m==Release){n=S(o,n,"blast.static","lightning",.35f);n=S(o,n,"melee.shield.block","parry",.4f);}else if(m==Hit)n=S(o,n,"impact.metal","metal",.55f);else if(m==Recast){n=S(o,n,"blast.kinetic","dash",.85f);n=S(o,n,"gun.ricochet","metal",.8f);}break;
                case NativeTechniques.Sunder:if(m==Release){atPoint=true;n=S(o,n,"melee.greatsword.swing","greatsword",.9f);n=S(o,n,"blast.quake","stone",.65f);}else if(m==Hit){atPoint=true;n=S(o,n,"melee.greatsword.hit","greatsword",1);n=S(o,n,"impact.metal","metal",.6f);}break;
                case NativeTechniques.ReapingSweep:if(m==Release){n=S(o,n,"melee.greatsword.swing","greatsword",.85f);n=S(o,n,"move.roll","dash",.5f);}else if(m==Hit)n=S(o,n,"melee.greatsword.hit","greatsword",.8f);break;
                case NativeTechniques.BladeDrag:if(m==Release){n=S(o,n,"impact.metal","metal",.6f);n=S(o,n,"move.roll","dash",.5f);}else if(m==Recast){n=S(o,n,"melee.greatsword.swing","greatsword",1);n=S(o,n,"blast.kinetic","dash",.6f);n=S(o,n,"blast.quake","stone",.4f);}else if(m==Hit)n=S(o,n,"melee.greatsword.hit","greatsword",.9f);break;
            }
            return n;
        }
    }
    public static partial class NativeVisualMotion
    {
        private const string MeleeOverhead="2Hand-Sword-Attack11",MeleeSweep="2Hand-Sword-Attack2";
        private static bool PoseMelee(Set set,NativeTechnique k,float time,float back,IClips clips,out Pick pick)
        {
            pick=default;Pick got=default;
            bool Part(string clip,float from,float to){return time>=from&&time<to&&clip!=null&&clips.Has(clip)&&Fit(clip,time,from,to,clips,FadeIn,out got);}
            // A clip with its own Hit on `at`, each side stretched to fit.
            bool Struck(string clip,float from,float at,float to){if(time<from||time>=to||clip==null||!clips.Has(clip))return false;float len=clips.Length(clip),h=clips.Hit(clip);got=new Pick{Clip=clip,Time=Retime(time,from,at,to,h>0&&h<len?h:len*.5f,len),Fade=FadeIn};return true;}
            bool hit=false;
            switch(k.Kind)
            {
                case NativeTechniques.Disarm:hit=Part(TechCounter,k.At,Math.Max(k.Release+.3f,back));break;
                case NativeTechniques.Iaido:{float draw=k.RecastAt>-50?k.RecastAt:k.Until;hit=Part(TechSurf,k.At,draw)||Part("Armed-Dash-Forward",draw,draw+.14f)||Part(TechCounter,draw+.14f,Math.Max(draw+.4f,k.RecoverUntil));}break;
                case NativeTechniques.Challenge:hit=Part(TechCoin,k.At,back);break;
                case NativeTechniques.MirrorDouble:hit=Part(TechFlickStart,k.At,k.Release)||Part(TechFlickEnd,k.Release,back);break;
                case NativeTechniques.Envenom:hit=Part(set.Block,k.At,k.Release+.1f);break;
                case NativeTechniques.ChainHook:{float turn=k.HitAt>-50?k.HitAt:k.Arm>k.Release?k.Arm:k.Until;hit=Part(TechThrowStart,k.At,k.Release)||Part(TechThrowEnd,k.Release,Math.Max(turn,k.Release+.05f))||Part(set.Block,turn,Math.Max(k.Until,turn+.1f));}break;
                case NativeTechniques.WarCry:hit=Part(set.Block,k.At,k.Release)||Part(set.BlockHit,k.Release,back+.2f);break;
                case NativeTechniques.GroundSlam:{float land=k.HitAt>-50?k.HitAt:k.Arm,top=(k.Release+land)*.5f;hit=Part(TechSurf,k.At,k.Release)||Part(set.Jump,k.Release,top)||Part(set.AirAttack,top,land)||Part(set.Land,land,Math.Max(back,land+.2f));}break;
                case NativeTechniques.Lodestone:
                    if(k.Recast(time))hit=set.Attacks!=null&&Part(set.Attacks[0],k.RecastAt,Math.Max(back,k.RecastAt+.3f));
                    else if(time<k.Until&&set.Block!=null&&clips.Has(set.Block)){got=new Pick{Clip=set.Block,Time=Math.Min(time-k.At,clips.Length(set.Block)*.4f),Fade=FadeIn};hit=true;}
                    break;
                // The big overhead, its own Hit on the landing.
                case NativeTechniques.Sunder:hit=Struck(MeleeOverhead,k.At,k.Release,Math.Max(back,k.Release+.3f));break;
                case NativeTechniques.ReapingSweep:hit=Part(TechVowHold,k.At,k.Release)||Struck(MeleeSweep,k.Release-.02f,k.Release+.1f,Math.Max(back,k.Release+.3f));break;
                // The drag is the run itself; the rise tears up out of it.
                case NativeTechniques.BladeDrag:if(k.Recast(time))hit=Struck(set.SprintAttack,k.RecastAt-.02f,k.RecastAt+.1f,Math.Max(back,k.RecastAt+.3f));break;
                default:return false;
            }
            pick=got;return hit;
        }
    }
}
