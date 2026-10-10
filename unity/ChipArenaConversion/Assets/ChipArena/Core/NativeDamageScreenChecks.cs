using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Managed checks for the damage screen rules (NativeDamageScreen), run by NativeBuildChecks and Tests/CoreTests: severity,
    // direction mapping, bloom merging and slot reuse, element ticks, low HP and heartbeat, heal relief, death fade and finite
    // outputs, the baked noise, the per-attacker threat arcs, live re-aim, the 4 Hz flash cap, the HP lag chip and a
    // steady state that allocates nothing. Visual rules only.
    public static class NativeDamageScreenChecks
    {
        public static IEnumerable<string> Run()
        {
            void Check(bool ok,string what){if(!ok)throw new Exception("Damage screen: "+what);}
            bool Near(float a,float b,float e=1e-3f)=>Math.Abs(a-b)<=e;
            V3 R=new V3(1,0,0),U=new V3(0,1,0),F=new V3(0,0,1);
            Check(NativeDamageScreen.Severity(.02f)==0&&Near(NativeDamageScreen.Severity(.40f),1)&&NativeDamageScreen.Severity(.9f)==1,"severity ends");
            Check(Near(NativeDamageScreen.Severity(.09f),.307f,.01f)&&NativeDamageScreen.Severity(.2f)>NativeDamageScreen.Severity(.09f),"severity curve");
            Check(Near(NativeDamageScreen.HitSeverity(.01f,false,false),.12f)&&Near(NativeDamageScreen.HitSeverity(.01f,true,false),.35f),"severity floors");
            Check(Near(NativeDamageScreen.HitSeverity(.2f,false,true),NativeDamageScreen.Severity(.2f)*1.2f)&&NativeDamageScreen.HitSeverity(.4f,false,true)==1,"melee runs 1.2x deeper, capped");
            yield return "damage screen severity scales with damage / max HP (floors, melee x1.2)";
            NativeDamageScreen.Direction(-1,0,.25f,out var x,out var y,out var w);Check(x<-.95f&&Math.Abs(y)<.05f&&w==1,"left");
            NativeDamageScreen.Direction(.2f,0,-1,out x,out y,out w);Check(y<-.9f&&x>0&&w==1,"behind");
            NativeDamageScreen.Direction(0,1,.1f,out x,out y,out w);Check(y>.95f&&w==1,"above");
            NativeDamageScreen.Direction(-.7f,0,-.7f,out x,out y,out w);Check(x<-.5f&&y<-.5f,"behind-left");
            NativeDamageScreen.Direction(0,0,1,out x,out y,out w);Check(w==0,"in front is not directional");
            NativeDamageScreen.Direction(0,0,0,out x,out y,out w);Check(w==0&&!float.IsNaN(x)&&!float.IsNaN(y),"zero vector");
            Check(Near(NativeDamageScreen.Bearing(new V3(0,0,5),R,F,U),0)&&Near(NativeDamageScreen.Bearing(new V3(5,0,0),R,F,U),(float)Math.PI/2)&&Math.Abs(NativeDamageScreen.Bearing(new V3(0,0,-5),R,F,U))>3.1f,"bearing: ahead up, right right, behind down");
            var yawed=V3.Forward(90);Check(Near(NativeDamageScreen.Bearing(new V3(5,0,0),new V3(0,0,-1),yawed,U),0),"bearing follows the camera heading");
            var pitched=new V3(0,-.8f,.6f);Check(Near(NativeDamageScreen.Bearing(new V3(-3,0,3),R,pitched,new V3(0,.6f,.8f)),-(float)Math.PI/4),"bearing ignores pitch");
            Check(NativeDamageScreen.Carets(new V3(5,0,0))==0&&NativeDamageScreen.Carets(new V3(5,2.5f,0))==1&&NativeDamageScreen.Carets(new V3(1,3,0))==2&&NativeDamageScreen.Carets(new V3(4,-2,0))==-1,"elevation carets at 20 and 45 degrees");
            yield return "damage screen direction: left/right as is, up for above, down for behind or below, omni ahead; ring bearing and carets";
            var s=new NativeDamageScreen();float t=10,dt=1f/60;
            int Live(){int n=0;for(int i=0;i<NativeDamageScreen.Slots;i++)if(s.A[i*4+3]>0)n++;return n;}
            int Arcs(){int n=0;for(int i=0;i<NativeDamageScreen.Arcs;i++)if(s.ArcA[i*4+1]>0)n++;return n;}
            void Run(float seconds,float hp=1,bool living=true,bool reduced=false){for(float e=0;e<seconds;e+=dt){t+=dt;s.Step(t,dt,hp,living,reduced,!reduced,R,U,F);}}
            Run(.1f);Check(!s.Active,"idle is inactive");
            s.Hit(t,.09f,false,false,1,new V3(-4,0,1),1);Run(.02f);s.Hit(t,.09f,false,false,2,new V3(-4,.3f,1.2f),1);Run(.05f);Check(Live()==1&&s.Active,"same side merges");
            s.Hit(t,.09f,false,false,3,new V3(4,0,1),1);Run(.05f);Check(Live()==2,"opposite side takes a slot");
            s.Hit(t,.09f,false,false,4,new V3(0,4,.2f),1);s.Hit(t,.09f,false,false,5,new V3(0,0,-4),1);s.Hit(t,.09f,false,false,6,new V3(3,3,-.5f),1);Run(.02f);Check(Live()==4,"four slots at most");
            float reach=0;for(int i=0;i<NativeDamageScreen.Slots;i++)reach=Math.Max(reach,s.B[i*4]);Check(reach<=(NativeDamageScreen.ReachBase+NativeDamageScreen.ReachSev)*NativeDamageScreen.Overshoot+1e-4f,"reach capped");
            Run(NativeDamageScreen.Life(1)+.1f);Check(Live()==0,"blooms expire");Run(1.5f);Check(!s.Active,"returns to idle");
            yield return "damage screen blooms merge by side, use four slots, cap their reach and expire";
            s.Clear();t=20;for(int i=0;i<8;i++){s.Tick(t,.012f,true);Run(.25f);}Check(Live()==0&&Arcs()==0&&s.Fire>.3f,"burn ticks feed the fire field, not blooms or arcs");
            Run(2.5f);Check(s.Fire==0,"fire fades");
            s.Tick(t,.01f,false);Run(.02f);Check(s.Poison>.2f&&s.PoisonTick>0&&Arcs()==0,"poison tick pulses");
            yield return "damage screen burn and poison ticks feed element fields, never blooms or arcs";
            s.Clear();t=30;Run(1,.5f);Check(s.Low==0,"half HP is not low");
            Run(2,.07f);Check(s.Low>.99f&&s.Death>.5f&&s.Pulse>=0&&s.Pulse<=1&&s.Desat>.4f,"near death");
            float lo=1,hi=0;for(int i=0;i<90;i++){Run(dt,.07f);lo=Math.Min(lo,s.Pulse);hi=Math.Max(hi,s.Pulse);}Check(hi>.9f&&lo<.1f,"heartbeat pulses");
            float clock=s.Clock;Run(.5f,.07f,true,true);Check(Near(s.Pulse,.35f)&&s.Chroma==0&&s.Clock==clock,"reduced effects still the pulse, the drift and the split");
            yield return "damage screen low HP: colour drain and a heartbeat that reduced effects stills";
            s.Hit(t,.05f,false,false,7,new V3(-4,0,0),1);Run(.05f,.45f);int arcs=Arcs();
            s.Healed(t,.38f);Run(.05f,.45f);Check(s.HealWave>0&&s.Heal>.5f&&Arcs()==arcs,"a real heal sweeps the wave and leaves the arcs");
            Run(1,.45f);s.Healed(t,.02f);Run(.05f,.47f);Check(s.HealWave==0,"regen does not sweep");Run(2,1);Check(s.Low==0,"healed HP leaves the low state");
            yield return "damage screen heal relief: wave for real heals only, arcs untouched";
            s.Clear();t=40;s.Hit(t,.3f,false,false,1,new V3(-4,0,0),1);s.Tick(t,.02f,true);Run(.05f,0,false);s.Hit(t,.3f,false,false,2,new V3(4,0,0),1);
            Run(.6f,0,false);Check(s.Fade==0&&Live()==0&&Arcs()==0&&s.Fire==0&&!s.Active,"death fades out and takes no hits");
            s.Clear();t=50;s.Hit(t,1e9f,true,false,9,new V3(float.NaN,0,1),1);s.Hit(t,.2f,false,true,3,new V3(1e-9f,0,0),float.NaN);Run(.05f,float.NaN);
            foreach(var v in s.A)Check(!float.IsNaN(v)&&!float.IsInfinity(v),"finite blooms");foreach(var v in s.ArcA)Check(!float.IsNaN(v)&&!float.IsInfinity(v),"finite arcs");
            Check(!float.IsNaN(s.Desat)&&!float.IsNaN(s.Chroma)&&!float.IsNaN(s.Low)&&!float.IsNaN(s.ShockAngle),"finite globals");
            yield return "damage screen death fade and finite outputs";
            var tex=NativeDamageScreen.NoiseTexels();int size=NativeDamageScreen.NoiseSize;Check(tex.Length==size*size*4,"noise size");
            for(int c=0;c<4;c++)
            {
                int lo2=255,hi2=0;double seam=0,inner=0;
                for(int row=0;row<size;row++){int l=tex[(row*size)*4+c],r=tex[(row*size+size-1)*4+c],m=tex[(row*size+size/2)*4+c],m1=tex[(row*size+size/2-1)*4+c];seam+=Math.Abs(l-r);inner+=Math.Abs(m-m1);}
                for(int i=c;i<tex.Length;i+=4){lo2=Math.Min(lo2,tex[i]);hi2=Math.Max(hi2,tex[i]);}
                Check(lo2<70&&hi2>185,"noise channel "+c+" range");Check(seam<=inner*1.6+size,"noise channel "+c+" tiles");
            }
            yield return "damage screen baked noise covers its range and tiles";
            // Threat arcs: one slot per attacker; the same attacker merges; a seventh replaces the weakest; no source: dashed ring.
            s.Clear();t=60;
            s.Hit(t,.1f,false,false,11,new V3(-5,0,0),1);Run(.1f);float sev0=s.ArcC[1];s.Hit(t,.1f,false,false,11,new V3(-5,0,.2f),1);Run(.02f);
            Check(Arcs()==1&&s.ArcC[1]>sev0&&s.ArcA[1]>.6f,"the same attacker merges into one stronger arc");
            for(int k=12;k<=16;k++){s.Hit(t,.05f+.02f*(k-12),false,false,k,new V3((float)Math.Sin(k),0,(float)Math.Cos(k)),1);Run(.02f);}Check(Arcs()==6,"six attackers, six arcs");
            s.Hit(t,.3f,false,false,17,new V3(0,0,-5),1);Run(.02f);bool has17=false;for(int i=0;i<NativeDamageScreen.Arcs;i++)if(s.Ring[i].Live&&s.Ring[i].Key==17)has17=true;
            Check(Arcs()==6&&has17,"a seventh attacker replaces the weakest arc");
            s.Clear();t=65;s.Hit(t,.1f,false,false,0,new V3(0,-1,0),.8f);Run(.05f);
            Check(Arcs()==1&&s.ArcB[2]==4&&Live()==1&&s.A[1]<-.9f&&Near(s.A[2],.8f,.02f),"a floor hazard blooms from below but draws the dashed ring");
            s.Hit(t,.2f,true,false,21,new V3(0,0,6),1);s.Hit(t,.25f,false,true,22,new V3(2,0,-1),1);Run(.02f);bool head=false,melee=false;
            for(int i=0;i<NativeDamageScreen.Arcs;i++){if(s.ArcA[i*4+1]>0&&s.ArcB[i*4+2]==1)head=true;if(s.ArcA[i*4+1]>0&&s.ArcB[i*4+2]>=8)melee=true;}
            Check(head&&melee&&s.MarkAge>=0&&s.ShockAge>=0,"headshot and melee arcs, the reticle mark and the shock arc");
            int ticks=0;s.Clear();t=68;for(int i=0;i<4;i++){s.Tick(t,.02f,i%2==0);Run(.1f);ticks+=Arcs();}Check(ticks==0,"ticks never make arcs");
            yield return "damage screen threat arcs: one per attacker, merged, six at most, dashed ring without a source";
            // Live re-aim: an attacker that moves 90 degrees round the pilot moves its arc within one frame; the bloom follows
            // with a .08 s time constant, round the edge.
            s.Clear();t=70;s.Hit(t,.2f,false,false,31,new V3(-6,0,0),1);Run(.05f);float before=s.ArcA[0];
            s.Aim(31,new V3(0,0,-6));Run(dt);float after=s.ArcA[0];Check(Near(before,-(float)Math.PI/2,.01f)&&Math.Abs(after)>3.1f,"the arc follows its attacker within a frame");
            float bx=s.A[0],by=s.A[1];Check(bx<-.5f&&by>-.6f,"the bloom eases toward the new side");Run(.4f);Check(s.A[1]<-.95f&&Math.Abs(s.A[0])<.2f,"the bloom settles on the attacker");
            float yawRight=(float)Math.PI/2;s.Step(t+dt,dt,1,true,false,true,V3.Forward(180),U,V3.Forward(90));t+=dt;Check(Near(s.ArcA[0],yawRight,.02f),"turning the camera re-aims the arc");
            yield return "damage screen live re-aim: arcs follow their attacker every frame, blooms slide round the edge";
            // Photosensitivity: a machine gun at 10 Hz flashes an attacker's white core and the impact flash at most at 4 Hz.
            s.Clear();t=80;int cores=0,flashes=0;float lastCore=0,lastFlash=0;
            for(int i=0;i<60;i++)
            {
                if(i%6==0)s.Hit(t,.06f,false,false,41,new V3(-3,0,2),1);t+=dt;s.Step(t,dt,1,true,false,true,R,U,F);
                float core=s.ArcB[1];if(core>lastCore+.5f)cores++;lastCore=core;if(s.Flash>lastFlash+.2f)flashes++;lastFlash=s.Flash;
            }
            Check(cores>=3&&cores<=4&&flashes>=3&&flashes<=4,"4 Hz cap: cores "+cores+", flashes "+flashes);
            s.Hit(t,.6f,false,false,42,new V3(3,0,2),1);t+=dt;s.Step(t,dt,1,true,false,true,R,U,F);Check(s.Flash>.7f,"a much stronger hit still flashes");
            yield return "damage screen flashes at most 4 times a second per attacker and globally";
            // The HP lag chip: holds the lost part .22 s, then drains .9 of max HP per second; heals and new owners snap it.
            var lag=new NativeDamageScreen.Lag();float lt=0,lv=NativeDamageScreen.LagStep(ref lag,1,lt,1);
            for(int i=0;i<3;i++){lt+=.05f;lv=NativeDamageScreen.LagStep(ref lag,1,lt,.6f);}Check(Near(lv,1),"lag holds");
            for(int i=0;i<8;i++){lt+=.05f;lv=NativeDamageScreen.LagStep(ref lag,1,lt,.6f);}Check(lv<1&&lv>.6f,"lag drains");
            lt+=.05f;Check(NativeDamageScreen.LagStep(ref lag,1,lt,.9f)==.9f&&NativeDamageScreen.LagStep(ref lag,2,lt+.05f,.3f)==.3f,"heal and new pilot snap");
            yield return "damage screen HP lag chip holds .22 s then drains";
            // Steady frames allocate nothing: 600 frames of hits, ticks, heals, re-aims and low HP.
            s.Clear();t=90;var keys=new int[NativeDamageScreen.Slots+NativeDamageScreen.Arcs];
            void Frame(int i){if(i%7==0)s.Hit(t,.05f+.01f*(i%5),i%23==0,i%31==0,50+i%8,new V3((float)Math.Sin(i*.1f),0,(float)Math.Cos(i*.1f)),1);if(i%11==0)s.Tick(t,.01f,i%2==0);if(i%97==0)s.Healed(t,.1f);
                int n=s.Keys(keys);for(int k=0;k<n;k++)s.Aim(keys[k],new V3((float)Math.Cos(i*.05f+k),0,(float)Math.Sin(i*.05f+k)));t+=dt;s.Step(t,dt,.2f,true,false,true,R,U,F);}
            for(int i=0;i<60;i++)Frame(i);
            long probe=GC.GetAllocatedBytesForCurrentThread();var junk=new byte[64];bool counts=GC.GetAllocatedBytesForCurrentThread()-probe>=64&&junk.Length==64;
            long start=GC.GetAllocatedBytesForCurrentThread();for(int i=60;i<660;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-start;
            Check(!counts||used==0,"steady frames allocated "+used+" bytes");Check(s.Active,"the allocation run kept the screen busy");
            yield return counts?"damage screen steady state: 0 bytes allocated over 600 frames":"damage screen steady state: this runtime does not count allocations (not measured)";
        }
    }
}
