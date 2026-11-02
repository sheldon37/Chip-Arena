using System;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Deployment Station's presentation rules (Core NativeDeployPresentation) against Astra's authority
    // (NativeExpeditionDeployment): reels land on the result, phases follow the replicated state, mutators are never active
    // before Deploy, the theme blend has no seams, the map and the set are sane.
    internal static class DeployPresentationChecks
    {
        private static int count;
        private static void Check(bool condition,string why)
        {
            if(!condition)throw new Exception("Deployment presentation: "+why);
            count++;
        }
        private static readonly ExpeditionInstanceTheme[] Pool=
        {
            new ExpeditionInstanceTheme{id="exp-ward-zero",theme="quarantine"},
            new ExpeditionInstanceTheme{id="exp-foundry",theme="industrial"},
            new ExpeditionInstanceTheme{id="exp-transit",theme="rail"},
        };
        private static readonly string[] Mutators={"surge","storm","darkness","brittle","windfall","second-wind"};
        public static void Run()
        {
            count=0;
            Reels();Machines();Blend();Map();Set();Tables();
            Console.WriteLine("PASS Deployment presentation: "+count+" checks (reels land on the authority's result from any start, no jumps, "+
                "overshoot then settle; machine phases host and guest; mutators PENDING until Deploy, ACTIVE only after; the deploy timeline; "+
                "seamless theme weights equal to the cells at their centres; the holo map; the party pads and the shots)");
        }
        private static void Reels()
        {
            for(int n=1;n<=12;n++)for(int reel=0;reel<NativeDeployPresentation.Reels;reel++)for(int from=0;from<n;from++)for(int land=0;land<n;land++)
            {
                var end=NativeDeployPresentation.Reel(reel,NativeDeployPresentation.RollDuration+1,from,land,n);
                Check(end.Landed&&Math.Abs(end.Offset-land)<1e-4,"lands on the result "+n+" "+reel+" "+from+" "+land);
                float prev=from,maxStep=0,peak=0;bool backward=false;float dt=1/240f;
                float Wrap(float d){float h=n/2f;d=((d%n)+n)%n;return d>h?d-n:d;}
                for(float t=0;t<NativeDeployPresentation.RollDuration+.05f;t+=dt)
                {
                    var p=NativeDeployPresentation.Reel(reel,t,from,land,n);
                    float step=n>1?Wrap(p.Offset-prev):0;maxStep=Math.Max(maxStep,Math.Abs(step));peak=Math.Max(peak,p.Speed);
                    if(t<NativeDeployPresentation.StopTime(reel)&&step<-1e-4f)backward=true;
                    if(p.Landed){float m=((p.Offset%n)+n)%n;Check(Math.Abs(m-land)<1e-3,"landed symbol");}
                    prev=p.Offset;
                }
                Check(!backward,"spins forward until its stop");
                Check(maxStep<30*dt+1e-3,"no jump: at most 30 symbols a second ("+maxStep/dt+")");
                Check(peak>=NativeDeployPresentation.MinCruise*.9f&&peak<=30,"cruise speed "+peak);
                var top=NativeDeployPresentation.Reel(reel,NativeDeployPresentation.StopTime(reel),from,land,n);
                Check(Math.Abs(top.Offset-(float)Math.Round(top.Offset-NativeDeployPresentation.Overshoot)-NativeDeployPresentation.Overshoot)<1e-3,"overshoots by .22 of a symbol");
            }
            Check(NativeDeployPresentation.Reel(1,float.NaN,0,3,5).Landed&&NativeDeployPresentation.Reel(1,-1,0,3,5).Offset==3,"a late joiner sees the result landed");
            Check(NativeDeployPresentation.StopTime(0)<NativeDeployPresentation.StopTime(1)&&NativeDeployPresentation.StopTime(1)<NativeDeployPresentation.StopTime(2),"reels stop left to right");
            var ids=Pool.Select(x=>x.id).ToArray();
            var strips=Enumerable.Range(0,3).Select(r=>NativeDeployPresentation.Strip(ids,r)).ToArray();
            Check(strips.All(s=>s.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(ids.OrderBy(x=>x,StringComparer.Ordinal))),"each strip carries every entry once");
            Check(strips.Select(s=>s[0]).Distinct().Count()==3,"the reels start on different symbols");
            var strip=NativeDeployPresentation.Strip(Mutators,0);int known=NativeDeployPresentation.Slot(ref strip,"storm");
            Check(strip[known]=="storm"&&strip.Length==Mutators.Length,"a known id lands on its own slot");
            int added=NativeDeployPresentation.Slot(ref strip,"meteor-shower");
            Check(strip.Length==Mutators.Length+1&&strip[added]=="meteor-shower","an unknown result is appended so it still lands");
            Check(NativeDeployPresentation.Lever(float.NaN)==0&&NativeDeployPresentation.Lever(.2f)>.99f&&NativeDeployPresentation.Lever(1)<.01f,"the lever pulls and returns");
        }
        private static void Machines()
        {
            const float Spin=.5f,Done=99;float before=float.NaN;
            var a=new NativeExpeditionDeployment("station","host",777);
            var s=a.Snapshot();
            var im=NativeDeployPresentation.InstanceMachine(s,before,before,true);var mm=NativeDeployPresentation.MutatorMachine(s,before,before,true);
            Check(im.Phase==NativeDeployPresentation.Phase.Dormant&&im.Usable,"the host's instance machine waits for a pull");
            Check(!NativeDeployPresentation.InstanceMachine(s,before,before,false).Usable,"a guest cannot pull");
            Check(mm.Phase==NativeDeployPresentation.Phase.Unavailable&&!mm.Usable,"mutators wait for a destination roll");
            Check(NativeDeployPresentation.DeployConsole(s,true,true)==NativeDeployPresentation.Launch.NeedDestination,"no deploy before a destination roll");
            Check(a.RollInstances("host",Pool),"host rolls");s=a.Snapshot();
            Check(NativeDeployPresentation.InstanceMachine(s,Spin,before,true).Phase==NativeDeployPresentation.Phase.Spinning,"spins after the roll is seen");
            Check(NativeDeployPresentation.InstanceMachine(s,Done,before,true).Phase==NativeDeployPresentation.Phase.Landed,"lands");
            Check(NativeDeployPresentation.InstanceMachine(s,before,before,false).Phase==NativeDeployPresentation.Phase.Landed,"a late guest sees it landed");
            mm=NativeDeployPresentation.MutatorMachine(s,before,before,true);
            Check(mm.Phase==NativeDeployPresentation.Phase.Optional&&mm.Usable&&mm.Banner.Contains("OPTIONAL"),"mutators read optional");
            Check(NativeDeployPresentation.DeployConsole(s,false,true)==NativeDeployPresentation.Launch.WaitingParty,"deploy waits for the party");
            Check(NativeDeployPresentation.DeployConsole(s,true,true)==NativeDeployPresentation.Launch.Ready&&NativeDeployPresentation.DeployConsole(s,true,false)==NativeDeployPresentation.Launch.HostOnly,"only the host deploys");
            Check(a.RollMutators("host",Mutators),"host rolls mutators");s=a.Snapshot();
            Check(NativeDeployPresentation.MutatorMachine(s,Spin,before,true).Phase==NativeDeployPresentation.Phase.Spinning,"mutators spin");
            var pending=NativeDeployPresentation.MutatorMachine(s,Done,before,true);
            Check(pending.Phase==NativeDeployPresentation.Phase.Pending&&pending.Banner.Contains("PENDING"),"rolled mutators read pending");
            foreach(float t in new[]{float.NaN,0,.5f,1,2,3,5,99})Check(NativeDeployPresentation.MutatorMachine(s,t,before,true).Phase!=NativeDeployPresentation.Phase.Active&&
                NativeDeployPresentation.MutatorMachine(s,t,before,false).Phase!=NativeDeployPresentation.Phase.Active,"never active before Deploy ("+t+")");
            Check(a.SkipMutators("host"),"host skips");s=a.Snapshot();
            Check(NativeDeployPresentation.MutatorMachine(s,Done,before,true).Phase==NativeDeployPresentation.Phase.Skipped,"a skip reads skipped");
            Check(a.RollMutators("host",Mutators)&&a.Deploy("host",true),"host rolls and deploys");s=a.Snapshot();
            var locked=NativeDeployPresentation.InstanceMachine(s,Done,0,true);
            Check(locked.Phase==NativeDeployPresentation.Phase.Locked&&!locked.Usable,"deploy locks the instance machine");
            Check(NativeDeployPresentation.InstanceMachine(s,Done,2,true).Power<.25f&&locked.Power>.99f,"it darkens over the deploy");
            Check(NativeDeployPresentation.MutatorMachine(s,Done,2,true).Phase==NativeDeployPresentation.Phase.Active,"mutators become active at Deploy");
            Check(NativeDeployPresentation.DeployConsole(s,true,true)==NativeDeployPresentation.Launch.Launching,"the console launches");
            var b=new NativeExpeditionDeployment("station","host",778);b.RollInstances("host",Pool);b.Deploy("host",true);
            Check(NativeDeployPresentation.MutatorMachine(b.Snapshot(),Done,2,true).Phase==NativeDeployPresentation.Phase.Locked,"no mutators: simply locked");
            var view=NativeDeployPresentation.Framing(NativeDeployPresentation.InstanceMachine(s,Done,0,true),NativeDeployPresentation.MutatorMachine(s,Done,0,true),s);
            Check(view==NativeDeployPresentation.Shot.Deploy,"the camera goes to the gate on Deploy");
            float last=-1;bool travel=false;
            for(float t=0;t<5;t+=.01f)
            {
                var d=NativeDeployPresentation.Deploying(t);
                Check(d.Gate>=last-1e-6,"the gate only opens");last=d.Gate;
                if(d.Travel&&!travel){travel=true;Check(Math.Abs(t-NativeDeployPresentation.TravelAt)<.011f&&d.Fade>.99f&&d.Lift>.99f,"travel after the lift and the fade");}
            }
            Check(travel&&NativeDeployPresentation.Deploying(float.NaN).Gate==0,"travel happens; nothing before Deploy");
        }
        private static void Blend()
        {
            var a=new NativeExpeditionDeployment("station","host",4242);a.RollInstances("host",Pool);var s=a.Snapshot();
            foreach(var c in s.cells)
            {
                var w=NativeDeployPresentation.Weights(s,c.x*NativeDeployPresentation.CellMetres,c.z*NativeDeployPresentation.CellMetres);
                for(int i=0;i<4;i++)Check(Math.Abs(w[i]-c.influences[i]/100f)<1e-4,"centre weight equals the cell "+c.x+","+c.z);
            }
            float worst=0;
            for(float x=-NativeDeployPresentation.WorldHalfX;x<NativeDeployPresentation.WorldHalfX;x+=.5f)for(float z=-NativeDeployPresentation.WorldHalfZ;z<NativeDeployPresentation.WorldHalfZ;z+=.5f)
            {
                var w=NativeDeployPresentation.Weights(s,x,z);var e=NativeDeployPresentation.Weights(s,x+.25f,z);var n=NativeDeployPresentation.Weights(s,x,z+.25f);
                Check(Math.Abs(w.Sum()-1)<1e-4&&w.All(v=>v>=0),"weights sum to 1");
                for(int i=0;i<4;i++)worst=Math.Max(worst,Math.Max(Math.Abs(w[i]-e[i]),Math.Abs(w[i]-n[i])));
            }
            Check(worst<.08f,"no seam: at most "+worst+" of change over 25 cm");
            var plaza=NativeDeployPresentation.Weights(s,0,0);Check(plaza[0]>.999f,"the plaza is neutral");
            for(int d=0;d<3;d++){var dd=s.districts[d];var w=NativeDeployPresentation.Weights(s,dd.x*10,dd.z*10);Check(w[d+1]>.6f,"the district owns its entrance "+d);}
            Check(NativeDeployPresentation.Weights(null,3,4)[0]==1,"no state: neutral");
        }
        private static void Map()
        {
            var a=new NativeExpeditionDeployment("station","host",99);var empty=NativeDeployPresentation.Preview(a.Snapshot(),125,95);
            Check(empty.Length==125*95&&empty.All(p=>((uint)p>>24)==255),"opaque raster before a roll");
            a.RollInstances("host",Pool);var s=a.Snapshot();
            var img=NativeDeployPresentation.Preview(s,250,190);var again=NativeDeployPresentation.Preview(s,250,190);
            Check(img.SequenceEqual(again)&&!img.SequenceEqual(NativeDeployPresentation.Preview(new NativeExpeditionDeployment("station","host",100).Snapshot(),250,190)),"deterministic");
            var pal=NativeDeployPresentation.Palette(s);
            int Px(float wx,float wz){int i=(int)((wx+NativeDeployPresentation.WorldHalfX)/(2*NativeDeployPresentation.WorldHalfX)*250),j=(int)((NativeDeployPresentation.WorldHalfZ-wz)/(2*NativeDeployPresentation.WorldHalfZ)*190);return img[j*250+i]&0xffffff;}
            for(int d=0;d<3;d++){var dd=s.districts[d];Check(Px(dd.x*10,dd.z*10)==pal[d+1],"entrance "+d+" drawn in its colour");}
            var boss=NativeDeployPresentation.BossAnchor;Check(((Px(boss.X,boss.Y-1)>>16)&255)>200,"the boss gate is red");
            Check(pal.Skip(1).Distinct().Count()==3,"three distinct district colours");
        }
        private static void Set()
        {
            var pads=Enumerable.Range(0,NativeDeployPresentation.Pads).Select(NativeDeployPresentation.Pad).ToArray();
            for(int i=0;i<pads.Length;i++)for(int j=i+1;j<pads.Length;j++)Check(Vector3.Distance(pads[i],pads[j])>1.1f,"pads apart "+i+" "+j);
            foreach(var p in pads)
            {
                Check(p.Z<NativeDeployPresentation.Table.Z&&Vector3.Distance(p,NativeDeployPresentation.Table)>NativeDeployPresentation.TableRadius+.6f,"pad in front of the table");
                Check(Vector3.Distance(p,NativeDeployPresentation.InstanceMachineAt)>2&&Vector3.Distance(p,NativeDeployPresentation.MutatorMachineAt)>2,"pad clear of the machines");
            }
            Check(Math.Abs(pads[0].X)<1e-4,"the host stands in the middle");
            foreach(NativeDeployPresentation.Shot shot in Enum.GetValues(typeof(NativeDeployPresentation.Shot)))
            {
                var v=NativeDeployPresentation.ShotOf(shot);Check(Vector3.Distance(v.Eye,v.Target)>2&&v.Fov>30&&v.Fov<70&&v.Eye.Y>1,"shot "+shot);
            }
        }
        private static void Tables()
        {
            foreach(var p in Pool)
            {
                var d=NativeDeployPresentation.DestinationOf(p.id,p.theme);
                Check(d.Name.Length>0&&d.Color!=NativeDeployPresentation.UnknownColor&&d.Emblem!="unknown","known destination "+p.id);
            }
            var cat=NativeDeployPresentation.DestinationOf("exp-catacombs","catacombs");Check(cat.Name=="CATACOMBS"&&cat.Emblem=="crypt","a new instance falls back to its theme");
            Check(NativeDeployPresentation.DestinationOf("exp-x",null).Color==NativeDeployPresentation.UnknownColor,"unknown destination neutral");
            foreach(var m in Mutators)Check(NativeDeployPresentation.MutatorOf(m).Polarity!=NativeDeployPresentation.Polarity.Unknown,"known mutator "+m);
            Check(NativeDeployPresentation.MutatorOf("x").Polarity==NativeDeployPresentation.Polarity.Unknown&&NativeDeployPresentation.MutatorOf("x").Name=="UNKNOWN","unknown mutator");
            Check(new[]{NativeDeployPresentation.Polarity.Boon,NativeDeployPresentation.Polarity.Bane,NativeDeployPresentation.Polarity.Chaos,NativeDeployPresentation.Polarity.Unknown}
                .Select(NativeDeployPresentation.PolarityColor).Distinct().Count()==4,"four polarity colours");
        }
    }
}
