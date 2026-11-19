using System;
using System.Collections.Generic;
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
            Reels();Machines();Blend();Map();Set();Tables();Catalog();Eject();EjectPoses();EjectComfort();EjectBore();EjectSounds();Cradle();Ride();
            Console.WriteLine("PASS Deployment presentation: "+count+" checks (reels land on the authority's result from any start, no jumps, "+
                "overshoot then settle; machine phases host and guest; mutators PENDING until Deploy, ACTIVE only after; the deploy timeline; "+
                "seamless theme weights equal to the cells at their centres; the holo map; the party pads and the shots; the 5.6 s eject and its 4 s variant: falls at 9.8 m/s² "+
                "only down into each pod's seat, the harness and seal before the release, the pods clear of each other and the tubes, each viewer's shots on its own pad, animated poses on "+
                "every pack set with nothing of the pilot rising and no part faster than .17 m a frame, its sounds once each; steady shots with camera shake off and a still wide one "+
                "with reduced effects, the ring chase without a strobe; the bores open to the collar; the cradle card's lamps, altimeter, map and its latch to the station's load; the pod ride's window at 3 s and 5 s)");
        }
        // The owner's 30 mutator cards: unique ids the roll authority accepts, unique names its card font can letter, a short
        // line each, all three polarities with the presentation's own colours, the reel carrying every card.
        private static void Catalog()
        {
            var ids=NativeDeployPresentation.MutatorIds;int n=NativeDeployPresentation.MutatorCount;
            Check(n==30&&ids.Length==n&&ids.Distinct(StringComparer.Ordinal).Count()==n&&ids.All(ExpeditionValidation.Id),"thirty unique valid mutator ids");
            var cards=Enumerable.Range(0,n).Select(NativeDeployPresentation.MutatorAt).ToArray();
            Check(cards.Select(m=>m.Name).Distinct(StringComparer.Ordinal).Count()==n,"thirty unique card names");
            foreach(var m in cards)
            {
                Check(m.Name.Length>0&&m.Name.All(NativeDeployArt.Lettered)&&NativeDeployArt.Lines(m.Name).Length<=2,m.Id+": the card font letters its name");
                Check(m.Line.Length>0&&m.Line.Length<=40&&m.Line.EndsWith(".",StringComparison.Ordinal),m.Id+": a short line");
                Check(NativeDeployPresentation.MutatorOf(m.Id).Name==m.Name&&m.Polarity!=NativeDeployPresentation.Polarity.Unknown,m.Id+": known, with a polarity");
                Check(NativeDeployArt.MutatorTile(m.Id).Length==NativeDeployArt.TilePixels*NativeDeployArt.TilePixels*4,m.Id+": its card tile renders");
            }
            foreach(NativeDeployPresentation.Polarity p in new[]{NativeDeployPresentation.Polarity.Boon,NativeDeployPresentation.Polarity.Bane,NativeDeployPresentation.Polarity.Chaos})
                Check(cards.Count(m=>m.Polarity==p)>=9,p+": at least nine cards");
            var strip=NativeDeployPresentation.Strip(ids,0);Check(strip.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(ids.OrderBy(x=>x,StringComparer.Ordinal)),"the mutator reel carries every card once");
            var a=new NativeExpeditionDeployment("station","host",7);Check(a.RollInstances("host",Pool)&&a.RollMutators("host",ids),"the authority rolls from all thirty");
            var s=a.Snapshot();Check(s.mutators.Length==3&&s.mutators.All(ids.Contains),"three of them land");
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
            Check(view==NativeDeployPresentation.Shot.Deploy,"the camera goes to the pads on Deploy");
            float last=-1,lastDrop=-1;bool travel=false,alarmed=false;
            // (re-pinned 2026-11-18, W3: the loop runs past TravelAt, now 5.6 s; the fade still ends exactly at it)
            for(float t=0;t<NativeDeployPresentation.TravelAt+.5f;t+=.01f)
            {
                var d=NativeDeployPresentation.Deploying(t);
                Check(d.Hatch>=last-1e-6&&d.Drop>=lastDrop-1e-6,"the hatches only open and the drop only goes on");last=d.Hatch;lastDrop=d.Drop;alarmed|=d.Alarm;
                Check(!d.Alarm||d.Hatch<1,"the alarm sounds before the hatches are open");
                for(int i=0;i<NativeDeployPresentation.Pads;i++){float now=NativeDeployPresentation.PadDrop(i,t),next=NativeDeployPresentation.PadDrop(i,t+.01f);Check(next>=now&&now>=0&&(now==0||d.Hatch>.99f),"pad "+i+"'s pilot drops only through an open hatch, and only down");}
                if(d.Travel&&!travel){travel=true;Check(Math.Abs(t-NativeDeployPresentation.TravelAt)<.011f&&d.Fade>.99f&&d.Drop>.99f,"travel after the drop and the fade");
                    for(int i=0;i<NativeDeployPresentation.Pads;i++)Check(NativeDeployPresentation.PadDrop(i,t)>=NativeDeployPresentation.DropGone,"pad "+i+"'s pilot is gone before travel");}
            }
            Check(alarmed&&travel&&NativeDeployPresentation.Deploying(float.NaN).Hatch==0&&NativeDeployPresentation.PadDrop(0,float.NaN)==0,"the alarm and travel happen; nothing before Deploy");
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
                // (re-pinned 2026-11-18, W3: the eject's shots are under the floor, in their pad's tube, seat or beside its pod: Eject checks them)
                var v=NativeDeployPresentation.ShotOf(shot);if(NativeDeployEject.Cuts(shot)){Check(v.Fov>30&&v.Fov<70&&Vector3.Distance(v.Eye,v.Target)>.3f,"eject shot "+shot);continue;}
                Check(Vector3.Distance(v.Eye,v.Target)>2&&v.Fov>30&&v.Fov<70&&v.Eye.Y>1,"shot "+shot);
            }
        }
        private static float Xz(Vector3 a,Vector3 b)=>(float)Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z));
        // The eject (NativeDeployEject; owner 2026-11-18 "a more realistic drop-in ... give the players an animation when they fall",
        // Q22): 5.6 s and its 4 s variant with the same beats; every pilot falls at 9.8 m/s² through an open hatch, only ever down,
        // into its pod's seat (the host's at 2.7 s), harnessed and sealed before its pod releases; the pods hang clear of each other
        // and of the neighbouring tubes; each viewer's shots follow its own pad; the panels are gone before the tube shot; the
        // rush comes before the fade and the fade ends at TravelAt.
        private static void Eject()
        {
            Check(NativeDeployPresentation.TravelAt==5.6f&&NativeDeployEject.TravelOf(NativeDeployEject.Cut.Short)==4f&&NativeDeployEject.Played==NativeDeployEject.Cut.Full,"the eject runs 5.6 s; its 4 s variant is kept");
            Check(Math.Abs(NativeDeployEject.LandAt(0)-2.7f)<.005f,"the host lands in its seat at 2.7 s ("+NativeDeployEject.LandAt(0)+")");
            foreach(var cut in new[]{NativeDeployEject.Cut.Full,NativeDeployEject.Cut.Short})
            {
                float travel=NativeDeployEject.TravelOf(cut);
                for(int pad=0;pad<NativeDeployPresentation.Pads;pad++)
                {
                    float last=0,land=NativeDeployEject.LandAt(pad),start=NativeDeployEject.DropStart(pad);
                    for(float t=0;t<travel+.5f;t+=.005f)
                    {
                        float d=NativeDeployEject.Drop(pad,t,cut);Check(d>=last-1e-5f&&d>=0,"pad "+pad+"'s pilot only ever drops ("+cut+" "+t+")");last=d;
                        if(d>0)Check(NativeDeployPresentation.Deploying(t,cut).Hatch>.99f,"pad "+pad+" drops through an open hatch");
                        if(t>start&&t<land-.005f)Check(Math.Abs(d-.5f*9.8f*(t-start)*(t-start))<1e-3f,"pad "+pad+" falls at 9.8 m/s²");
                    }
                    Check(Math.Abs(NativeDeployEject.Drop(pad,land+.0005f,cut)-NativeDeployEject.Landing(pad))<.02f,"pad "+pad+"'s pilot lands on its seat");
                    Check(NativeDeployEject.Harness(pad,land)==0&&NativeDeployEject.Harness(pad,land+NativeDeployEject.HarnessAt+NativeDeployEject.HarnessSeconds+.001f)>=1,"pad "+pad+"'s bar slams down in .15 s");
                    Check(land+NativeDeployEject.SealAt+NativeDeployEject.SealSeconds<=NativeDeployEject.ReleaseOf(pad)+1e-4f,"pad "+pad+" is seated, harnessed and sealed before its pod releases");
                    Check(land<travel-.1f&&(NativeDeployEject.Seal(pad,travel)>.99f||cut==NativeDeployEject.Cut.Short&&pad>=4),"pad "+pad+" is sealed in before travel (the 4 s variant: a live party's four pads)");
                    if(cut==NativeDeployEject.Cut.Short)Check(NativeDeployEject.PodFall(pad,travel+1,cut)==0&&NativeDeployEject.Clamps(pad,travel,cut)==0,"the 4 s eject keeps the pods docked");
                    else Check(NativeDeployEject.PodFall(pad,travel,cut)>4&&NativeDeployEject.Clamps(pad,travel,cut)>.99f,"the full eject drops every pod");
                }
                // each viewer's shots follow its own pad: the room's, my tube from my drop + TubeLead, my seat from my landing, the release
                for(int mine=0;mine<NativeDeployPresentation.Pads;mine++)
                {
                    var seen=new List<NativeDeployPresentation.Shot>();var prev=(NativeDeployPresentation.Shot)(-1);float tubeAt=-1,seatAt=-1;var pad=NativeDeployPresentation.Pad(mine);
                    for(float t=0;t<travel+.3f;t+=.005f)
                    {
                        var s=NativeDeployEject.ShotOf(mine,t,cut);if(s!=prev){seen.Add(s);prev=s;if(s==NativeDeployPresentation.Shot.Tube)tubeAt=t;if(s==NativeDeployPresentation.Shot.Seat)seatAt=t;}
                        if(!NativeDeployEject.Cuts(s))continue;var v=NativeDeployEject.ViewOf(s,mine,t,cut);Check(v.Fov>30&&v.Fov<70&&float.IsFinite(v.Eye.Y),"eject view "+s);
                        if(s==NativeDeployPresentation.Shot.Tube)
                        {
                            Check(Xz(v.Eye,pad)<NativeDeployEject.TubeRadius-.2f&&v.Target.Y<v.Eye.Y-3&&Xz(v.Target,pad)<.8f,"the tube shot looks straight down my tube (pad "+mine+")");
                            // (re-pinned 2026-11-18, W3 review: the eye starts 1.2 m over the pad, so the neighbouring pads stay out of frame:
                            // it clears the posed pilot's highest part, its helmet included, by .45 m, not a standing pilot's head)
                            float top=NativeDeployModels.PadTop-NativeDeployEject.Drop(mine,t,cut)+NativePortraitRules.GroupY+Top(mine,t)+.17f;
                            Check(v.Eye.Y>top+.45f&&v.Eye.Y>NativeDeployEject.PodRoof(mine)+1,"my pilot falls below the tube shot's eye (pad "+mine+" at "+t+": eye "+v.Eye.Y+", top "+top+")");
                            Check(v.Eye.Y<NativeDeployModels.PadTop+1.25f,"the tube shot's eye no higher than 1.2 m over the pad (pad "+mine+")");
                        }
                        if(s==NativeDeployPresentation.Shot.Seat)
                        {
                            var root=NativeDeployEject.PodRoot(mine,t,cut);Matrix4x4.Invert(root,out var inv);var e=Vector3.Transform(v.Eye,inv);
                            // (re-pinned 2026-11-18, W3 review: from just outside the doorway, 1.1 m from the pilot, a 52° lens framing the helmet,
                            // the bar and the hands, not a cropped torso from inside the 1.25 m cabin)
                            Check(e.Y>NativeDeployEject.CabinFloor+.5f&&e.Y<NativeDeployEject.PodHeight-.5f&&Math.Abs(e.X)<.2f&&e.Z>NativePodModels.Hinge.Z+.2f&&e.Z<1.6f&&v.Fov<=52,"the seat shot looks in from just outside my pod's doorway (pad "+mine+" "+e+")");
                            if(t>NativeDeployEject.LandAt(mine)+.45f)SeatFrames(v,mine,t,cut);
                        }
                        if(s==NativeDeployPresentation.Shot.Release)
                        {
                            Check(v.Eye.Y<NativeDeployEjectModels.BellyBottom&&Xz(v.Eye,pad)>NativePodModels.Radius+.8f,"the release shot is under the belly beside my pod (pad "+mine+")");
                            var podAt=Vector3.Transform(new Vector3(0,NativeDeployEject.PodHeight*.5f,0),NativeDeployEject.PodRoot(mine,t,cut));
                            if(t<NativeDeployEject.RushAt)Check(Vector3.Dot(Vector3.Normalize(podAt-v.Eye),Vector3.Normalize(v.Target-v.Eye))>Math.Cos(v.Fov*.5f*Math.PI/180),"my pod stays in the release shot until the rush (pad "+mine+" at "+t+")");
                            if(t>NativeDeployEject.ReleaseOf(mine)+NativeDeployEject.ReleaseFollow)Check(v.Target.Y<v.Eye.Y-5,"the release shot then looks down after it to the city (pad "+mine+" at "+t+")");
                        }
                    }
                    var want=cut==NativeDeployEject.Cut.Full?new[]{NativeDeployPresentation.Shot.Deploy,NativeDeployPresentation.Shot.Tube,NativeDeployPresentation.Shot.Seat,NativeDeployPresentation.Shot.Release}:new[]{NativeDeployPresentation.Shot.Deploy,NativeDeployPresentation.Shot.Tube,NativeDeployPresentation.Shot.Seat};
                    Check(seen.SequenceEqual(want),"pad "+mine+"'s shots in order ("+cut+": "+string.Join(",",seen)+")");
                    Check(Math.Abs(tubeAt-(NativeDeployEject.DropStart(mine)+NativeDeployEject.TubeLead))<.006f&&Math.Abs(seatAt-NativeDeployEject.LandAt(mine))<.006f,"pad "+mine+"'s tube and seat shots keyed by its own drop");
                }
                Check(NativeDeployEject.Hud(NativeDeployEject.DropStart(0)+NativeDeployEject.TubeLead)<.01f&&NativeDeployEject.Hud(.5f)>.99f,"the console's panels step aside before the first tube shot");
                Check(NativeDeployEject.Rush(travel-.5f,cut)>.5f&&NativeDeployEject.Fade(travel-.5f,cut)<.5f&&NativeDeployEject.Fade(travel,cut)>.999f&&NativeDeployEject.Fade(travel-.7f,cut)==0,"the rush, then the fade ends at travel ("+cut+")");
                Check(NativeDeployEject.Red(.6f)>.99f&&NativeDeployEject.Red(0)==0,"the room turns red over .6 s");
            }
            // the pods hang clear: the same depth further than two pods apart, the deep ones' roofs below the shallow ones' bases, the
            // deep ones' tubes beside the shallow pods clear of their hulls toward them (the pod model's own reach that way)
            var pod=NativePodModels.Pod();var hull=pod.Sections.SelectMany(s=>s.Positions).Where(p=>p.Y>.3f&&p.Y<NativePodModels.Height-.2f).ToArray();
            for(int i=0;i<NativeDeployPresentation.Pads;i++)for(int j=0;j<NativeDeployPresentation.Pads;j++)
            {
                if(i==j)continue;var a=NativeDeployPresentation.Pad(i);var b=NativeDeployPresentation.Pad(j);float d=Xz(a,b);
                if(NativeDeployEject.Deep(i)==NativeDeployEject.Deep(j)){if(i<j)Check(d>2*NativePodModels.Radius,"pods "+i+" and "+j+" hang apart ("+d+")");continue;}
                if(NativeDeployEject.Deep(i))continue;Check(NativeDeployEject.PodRoof(j)<NativeDeployEject.PodBase(i)-.2f,"deep pod "+j+" hangs below shallow pod "+i);
                if(d>4)continue;var to=Vector3.Transform(new Vector3(b.X-a.X,0,b.Z-a.Z),Matrix4x4.CreateRotationY(-NativeDeployEject.Yaw(i)));to=Vector3.Normalize(to);
                float reach=0;foreach(var p in hull){var q=new Vector3(p.X,0,p.Z);float along=Vector3.Dot(q,to),off=(q-to*along).Length();if(off<NativeDeployEject.TubeRadius+.08f)reach=Math.Max(reach,along);}
                Check(d-reach>NativeDeployEject.TubeRadius+.06f+.01f,"deep tube "+j+" (its outer skin) clears shallow pod "+i+"'s hull ("+(d-reach)+")");
            }
            Check(NativeDeployEject.TubeLength(0)>4&&NativeDeployEject.TubeLength(1)>NativeDeployEject.TubeLength(0)+3&&NativeDeployEject.PodRoof(0)<NativeDeployEjectModels.BellyBottom,"the tubes run from the floor to their pods under the belly");
            for(int b=0;b<3;b++){float on=0;for(float t=1;t<1.12f;t+=.004f)on=Math.Max(on,NativeDeployEject.RingChase(b,t));Check(on>.9f,"ring bank "+b+" lights once every .12 s");}
            Check(NativeDeployEject.RingChase(0,.5f)==0,"no chase before the hatches open");
            // (W3 review: the chase ran the whole frame's light up and down at 8 Hz) the three banks always sum to the same light, so the
            // pulse runs up the tube without a strobe; reduced effects: steady rings
            for(float t=NativeDeployPresentation.HatchAt;t<4;t+=.003f)
            {
                float sum=0;for(int b=0;b<3;b++){sum+=NativeDeployEject.RingChase(b,t);Check(NativeDeployEject.RingChase(b,t,true)==NativeDeployEject.ChaseMean,"steady rings with reduced effects");}
                Check(Math.Abs(sum-NativeDeployEject.ChaseSum)<1e-4f,"the ring banks sum to a steady light ("+sum+" at "+t+")");
            }
        }
        // The eject's poses (the deploy cast; NativePackJumps and NativeLobbyPoses on every body): no rigid pose anywhere in the drop.
        private static void EjectPoses()
        {
            var e=new NativeDeployEject.Pilot();var pose=new CapturedRig.Pose();var lobby=new CapturedRig.Pose();
            foreach(var spec in new[]{"sword","daggers","greatsword","shield","rifle","pistol","fire","bow","siren"})
            {
                NativeDeployEject.Sample(e,lobby,spec,NativeLobbyPoses.Weapon,0,float.NaN,3,false,out var lw,out _);
                NativeDeployEject.Sample(e,pose,spec,NativeLobbyPoses.Weapon,0,.2f,3,false,out _,out _);Check(Same(pose,lobby),spec+": the lobby pose until the crouch");
                var prev=new CapturedRig.Pose();float low=0,minMove=float.MaxValue;
                for(float t=NativeDeployEject.CrouchAt+.05f;t<NativeDeployEject.TravelOf(NativeDeployEject.Played);t+=.1f)
                {
                    NativeDeployEject.Sample(e,pose,spec,NativeLobbyPoses.Weapon,0,t,3+t,false,out var w,out _);
                    for(int i=0;i<CapturedRig.Bones.Length;i++)Check(float.IsFinite(pose.WorldPosition[i].Y)&&Math.Abs(pose.WorldRotation[i].Length()-1)<1e-3f,spec+": a finite pose");
                    var act=NativeDeployEject.ActOf(0,t,out float age);
                    if(t>NativeDeployEject.CrouchAt+.1f&&act==NativeDeployEject.Act.Fall&&age>.05f)minMove=Math.Min(minMove,Diff(pose,prev));
                    if(act==NativeDeployEject.Act.Crouch&&age>NativeDeployEject.CrouchSeconds)low=Math.Max(low,lobby.WorldPosition[CapturedRig.Pelvis].Y-pose.WorldPosition[CapturedRig.Pelvis].Y);
                    if(act==NativeDeployEject.Act.Seat&&age>.3f)Check(!w.Visible&&pose.WorldPosition[CapturedRig.Pelvis].Y<lobby.WorldPosition[CapturedRig.Pelvis].Y-.12f,spec+": seated, the weapon away ("+(lobby.WorldPosition[CapturedRig.Pelvis].Y-pose.WorldPosition[CapturedRig.Pelvis].Y)+")");
                    for(int i=0;i<CapturedRig.Bones.Length;i++){prev.Position[i]=pose.Position[i];prev.Rotation[i]=pose.Rotation[i];}prev.World();
                }
                Check(minMove>.002f,spec+": the fall is animated every tenth of a second ("+minMove+")");Check(low>.02f,spec+": the crouch gathers ("+low+")");
                // (W3 review: the fall began with a hop, the take's push-off rising a metre over the open hatch) on the falling root nothing
                // of the pilot moves up: the drawn pelvis and head only fall from the drop to the landing (deep pods too); in the seat
                // they never rise over their height at the landing (the compression dips and returns); no part moves more than .17 m in
                // any 1/30 s from the crouch to the seat (the old hop moved a metre in one)
                var at=new CapturedRig.Pose();
                foreach(int pad in new[]{0,1,2,3})
                {
                    float d0=NativeDeployEject.DropStart(pad),land=NativeDeployEject.LandAt(pad),lastP=float.MaxValue,lastH=float.MaxValue,landP=float.NaN,landH=float.NaN,fastest=0;
                    for(float t=NativeDeployEject.CrouchAt;t<land+.6f;t+=1/240f)
                    {
                        NativeDeployEject.Sample(e,pose,spec,NativeLobbyPoses.Weapon,pad,t,3,true,out _,out _);float root=-NativeDeployEject.Drop(pad,t,NativeDeployEject.Played);
                        float py=root+pose.WorldPosition[CapturedRig.Pelvis].Y,hy=root+pose.WorldPosition[CapturedRig.Head].Y;
                        if(t>=d0&&t<=land){Check(py<=lastP+1e-4f&&hy<=lastH+1e-4f,spec+" pad "+pad+": nothing rises in the fall (at "+t+": pelvis "+(py-lastP)+", head "+(hy-lastH)+")");lastP=py;lastH=hy;}
                        if(t>=land){if(float.IsNaN(landP)){landP=py;landH=hy;}Check(py<=landP+.02f&&hy<=landH+.02f,spec+" pad "+pad+": the seated pilot never rises over its landing ("+t+")");}
                        NativeDeployEject.Sample(e,at,spec,NativeLobbyPoses.Weapon,pad,t+1/30f,3+1/30f,true,out _,out _);
                        for(int b=0;b<CapturedRig.Bones.Length;b++)fastest=Math.Max(fastest,Vector3.Distance(pose.WorldPosition[b],at.WorldPosition[b]));
                    }
                    Check(fastest<=.17f,spec+" pad "+pad+": no part moves more than .17 m in a 1/30 s ("+fastest+")");
                }
            }
            // nothing allocates per frame once a pilot's buffers are made
            NativeDeployEject.Sample(e,pose,"sword",NativeLobbyPoses.Weapon,1,2f,1,false,out _,out _);long before=GC.GetAllocatedBytesForCurrentThread();
            for(int f=0;f<240;f++)NativeDeployEject.Sample(e,pose,"sword",NativeLobbyPoses.Weapon,1,f/48f,f/60f,false,out _,out _);
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"the eject's poses allocate nothing per frame");
        }
        // The posed pilot's highest part over its feet (rig frame + GroupY: metres over the pad's top before the drop), the host's sword.
        private static readonly NativeDeployEject.Pilot topPilot=new NativeDeployEject.Pilot();private static readonly CapturedRig.Pose topPose=new CapturedRig.Pose();
        private static float Top(int pad,float t){NativeDeployEject.Sample(topPilot,topPose,"sword",NativeLobbyPoses.Weapon,pad,t,3,true,out _,out _);float y=float.MinValue;foreach(var w in topPose.WorldPosition)y=Math.Max(y,w.Y);return y;}
        // Whether a point is inside a view's frame (16:9) with a margin (a share of the half extent).
        private static bool InFrame(NativeDeployPresentation.View v,Vector3 p,float margin=.04f)
        {
            var f=Vector3.Normalize(v.Target-v.Eye);var r=Vector3.Normalize(Vector3.Cross(f,Vector3.UnitY));var u=Vector3.Cross(r,f);var d=p-v.Eye;float z=Vector3.Dot(d,f);if(z<=.05f)return false;
            float th=(float)Math.Tan(v.Fov*Math.PI/360),x=Vector3.Dot(d,r)/z/(th*16/9f),y=Vector3.Dot(d,u)/z/th;return Math.Abs(x)<1-margin&&Math.Abs(y)<1-margin;
        }
        // The seat shot (W3 review: it cropped the helmet and left the bar, the hands and the cabin out): with the bar down and the hands
        // on it, the helmet's top, the visor, the crossbar and both hands in frame, the doorway's posts at its edges, on every pad.
        private static void SeatFrames(NativeDeployPresentation.View v,int mine,float t,NativeDeployEject.Cut cut)
        {
            NativeDeployEject.Sample(topPilot,topPose,"sword",NativeLobbyPoses.Weapon,mine,t,3,true,out _,out _);var pod=NativeDeployEject.PodRoot(mine,t,cut);
            Vector3 P(Vector3 w)=>Vector3.Transform(new Vector3(-w.X,NativeDeployEject.CabinFloor+NativePortraitRules.GroupY+w.Y,w.Z),pod);var wp=topPose.WorldPosition;
            var helmet=P(wp[CapturedRig.Head]+new Vector3(0,.34f,0));var chest=P(wp[CapturedRig.Chest]);var lh=P(wp[CapturedRig.LHand]);var rh=P(wp[CapturedRig.RHand]);
            var bar=Vector3.Transform(NativeDeployEjectModels.HarnessPivot+NativeDeployEjectModels.Bar,pod);
            Check(InFrame(v,helmet)&&InFrame(v,chest)&&InFrame(v,bar)&&InFrame(v,lh)&&InFrame(v,rh),"the seat shot frames the helmet, the chest, the bar and both hands (pad "+mine+" at "+t+")");
        }
        // Comfort (plan risk 11; W3 review: the eject's camera and chase ignored the settings): camera shake off gives steady shots (the
        // tube's eye held, no jolt as I land, no plunge in the rush); reduced effects give no tube shot and no cabin close-up, the room's
        // shot until my landing and then one still wide shot under the belly (Full); the stage passes both through and its rings are steady.
        private static void EjectComfort()
        {
            foreach(var cut in new[]{NativeDeployEject.Cut.Full,NativeDeployEject.Cut.Short})for(int mine=0;mine<4;mine++)
            {
                float travel=NativeDeployEject.TravelOf(cut),tube0=NativeDeployEject.DropStart(mine)+NativeDeployEject.TubeLead,land=NativeDeployEject.LandAt(mine);
                var held=NativeDeployEject.ViewOf(NativeDeployPresentation.Shot.Tube,mine,tube0,cut,false,false);Matrix4x4.Invert(NativeDeployEject.PodRoot(mine,land,cut),out var inv0);
                var seat0=Vector3.Transform(NativeDeployEject.ViewOf(NativeDeployPresentation.Shot.Seat,mine,land,cut,false,false).Eye,inv0);float relY=float.NaN;
                NativeDeployPresentation.View wide=default;bool wideSeen=false;
                for(float t=0;t<travel+.2f;t+=.01f)
                {
                    var s=NativeDeployEject.ShotOf(mine,t,cut,false);
                    if(s==NativeDeployPresentation.Shot.Tube)Check(Vector3.Distance(NativeDeployEject.ViewOf(s,mine,t,cut,false,false).Eye,held.Eye)<1e-4f,"camera shake off: the tube's eye holds (pad "+mine+")");
                    if(s==NativeDeployPresentation.Shot.Seat){Matrix4x4.Invert(NativeDeployEject.PodRoot(mine,t,cut),out var inv);Check(Vector3.Distance(Vector3.Transform(NativeDeployEject.ViewOf(s,mine,t,cut,false,false).Eye,inv),seat0)<1e-4f,"camera shake off: no jolt in the seat (pad "+mine+")");}
                    if(s==NativeDeployPresentation.Shot.Release){float y=NativeDeployEject.ViewOf(s,mine,t,cut,false,false).Eye.Y;if(float.IsNaN(relY))relY=y;Check(Math.Abs(y-relY)<1e-4f,"camera shake off: no plunge in the rush (pad "+mine+")");}
                    var r=NativeDeployEject.ShotOf(mine,t,cut,true);
                    Check(r!=NativeDeployPresentation.Shot.Tube&&r!=NativeDeployPresentation.Shot.Seat,"reduced effects: no tube shot and no cabin close-up");
                    Check(r==(cut==NativeDeployEject.Cut.Full&&t>=land?NativeDeployPresentation.Shot.Release:NativeDeployPresentation.Shot.Deploy),"reduced effects: the room's shot, then the wide one after my landing (pad "+mine+" "+cut+" "+t+")");
                    if(r==NativeDeployPresentation.Shot.Release)
                    {
                        var v=NativeDeployEject.ViewOf(r,mine,t,cut,true,true);if(!wideSeen){wide=v;wideSeen=true;}
                        Check(Vector3.Distance(v.Eye,wide.Eye)<1e-4f&&Vector3.Distance(v.Target,wide.Target)<1e-4f,"reduced effects: the wide shot is still (pad "+mine+")");
                        var podAt=Vector3.Transform(new Vector3(0,NativeDeployEject.PodHeight*.5f,0),NativeDeployEject.PodRoot(mine,land,cut));
                        Check(InFrame(v,podAt)&&v.Eye.Y<NativeDeployEjectModels.BellyBottom,"reduced effects: the wide shot holds my docked pod under the belly (pad "+mine+")");
                    }
                }
                Check(wideSeen==(cut==NativeDeployEject.Cut.Full),"reduced effects: the wide shot plays in the full eject only");
            }
            // the stage: the settings reach the shots, the rings and the rain
            var a=new NativeExpeditionDeployment("comfort","host",4242);a.RollInstances("host",Pool);a.Deploy("host",true);var dep=a.Snapshot();
            foreach(var reduced in new[]{false,true})foreach(var still in new[]{false,true})
            {
                var st=new NativeDeployStage();var input=new DeployInput{State=dep,Instances=Pool,Mutators=Mutators,Host=true,PartyReady=true,SinceInstanceRoll=9,SinceMutatorRoll=float.NaN,SinceDeploy=0,Time=1,Seats=4,Ready=new bool[8],Mine=0,Reduced=reduced,NoShake=still};
                st.Frame(input,-1);float lastLight=float.NaN,worstJump=0;bool tubeSeen=false,rain=false;
                for(float t=0;t<NativeDeployPresentation.TravelAt;t+=1/30f)
                {
                    input.SinceDeploy=t;input.Time=1+t;st.Frame(input,1/30f);
                    Check(st.Shot==NativeDeployEject.ShotOf(0,t,NativeDeployEject.Played,reduced)&&st.Shake==!still,"the stage frames the shot the settings ask for");
                    for(int i=0;i<st.Count;i++)rain|=st.Items[i].Model==NativeDeployStage.Model.Rain;
                    if(st.Shot!=NativeDeployPresentation.Shot.Tube&&!(reduced&&t>NativeDeployPresentation.HatchAt+NativeDeployPresentation.HatchSeconds+.02f&&t<NativeDeployEject.LandAt(0))){lastLight=float.NaN;continue;}
                    // the tube's light: my tube's ring tints and the shot's practicals, frame to frame
                    float light=0;for(int i=0;i<st.Count;i++){var it=st.Items[i];if(it.Model==NativeDeployStage.Model.Tube&&Vector3.Distance(new Vector3(it.World.M41,0,it.World.M43),NativeDeployPresentation.Pad(0)*new Vector3(1,0,1))<.01f)light+=it.Light.X+it.Extra0.X+it.Extra1.X;}
                    if(!reduced){tubeSeen=true;for(int i=0;i<st.Lights.Points;i++){var p=st.Lights.Point[i];light+=(float)(p.R+p.G+p.B);}}
                    if(!float.IsNaN(lastLight)&&lastLight>0)worstJump=Math.Max(worstJump,Math.Abs(light-lastLight)/lastLight);lastLight=light;
                }
                if(reduced)Check(worstJump<1e-4f,"reduced effects: my tube's rings hold steady ("+worstJump+")");
                else Check(tubeSeen&&worstJump<.12f,"the tube shot's light changes under 12% a frame: no strobe ("+worstJump+", camera shake "+(!still)+")");
                Check(rain==!reduced,"rain streams past the release only with full effects");
            }
        }
        // The bores (W3 review: a deep pod's tube shot died on a cap across its bore at the belly's docking ring): straight down each tube
        // from the floor, nothing of the eject's set crosses the bore above its pod's collar's dark disc (the cabin's dark).
        private static void EjectBore()
        {
            var a=new NativeExpeditionDeployment("bore","host",4242);a.RollInstances("host",Pool);a.Deploy("host",true);var dep=a.Snapshot();
            var st=new NativeDeployStage();var input=new DeployInput{State=dep,Instances=Pool,Mutators=Mutators,Host=true,PartyReady=true,SinceInstanceRoll=9,SinceMutatorRoll=float.NaN,SinceDeploy=2.2f,Time=3,Seats=8,Ready=new bool[8],Mine=1};
            st.Frame(input,-1);
            for(int pad=0;pad<NativeDeployPresentation.Pads;pad++)
            {
                var p=NativeDeployPresentation.Pad(pad);var from=new Vector3(p.X,NativeDeployEject.TubeTop-.05f,p.Z);float floor=NativeDeployEject.PodRoof(pad)+.19f;
                foreach(var off in new[]{Vector3.Zero,new Vector3(.25f,0,0),new Vector3(0,0,-.25f),new Vector3(-.18f,0,.18f)})
                {
                    float best=float.MaxValue;string what="";
                    for(int i=0;i<st.Count;i++)
                    {
                        var it=st.Items[i];var m=st.Models[(int)it.Model];if(m==null||it.Model==NativeDeployStage.Model.Quad||it.Model==NativeDeployStage.Model.Beam)continue;
                        foreach(var sec in m.Sections)
                        {
                            if(sec.Bone==NativeDeployModels.Screen)continue;var ps=sec.Positions;var ix=sec.Indices;
                            for(int k=0;k+2<ix.Count;k+=3)
                            {
                                var A=Vector3.Transform(ps[ix[k]],it.World);var B=Vector3.Transform(ps[ix[k+1]],it.World);var C=Vector3.Transform(ps[ix[k+2]],it.World);
                                if(Down(from+off,A,B,C,out float y)&&from.Y-y<best&&y>floor-.3f){best=from.Y-y;what=it.Model+"/"+sec.Bone+" at y "+y;}
                            }
                        }
                    }
                    Check(best==float.MaxValue||from.Y-best<=floor+.005f,"pad "+pad+"'s bore is open down to its collar's dark ("+what+", the disc at "+floor+")");
                }
            }
        }
        // A vertical ray down from o: the height where it crosses triangle abc.
        private static bool Down(Vector3 o,Vector3 a,Vector3 b,Vector3 c,out float y)
        {
            y=0;float d=(b.Z-c.Z)*(a.X-c.X)+(c.X-b.X)*(a.Z-c.Z);if(Math.Abs(d)<1e-9f)return false;
            float l1=((b.Z-c.Z)*(o.X-c.X)+(c.X-b.X)*(o.Z-c.Z))/d,l2=((c.Z-a.Z)*(o.X-c.X)+(a.X-c.X)*(o.Z-c.Z))/d,l3=1-l1-l2;if(l1<0||l2<0||l3<0)return false;
            y=l1*a.Y+l2*b.Y+l3*c.Y;return y<o.Y;
        }
        private static bool Same(CapturedRig.Pose a,CapturedRig.Pose b){for(int i=0;i<CapturedRig.Bones.Length;i++)if(Vector3.Distance(a.WorldPosition[i],b.WorldPosition[i])>1e-5f)return false;return true;}
        private static float Diff(CapturedRig.Pose a,CapturedRig.Pose b){float m=0;for(int i=0;i<CapturedRig.Bones.Length;i++)m=Math.Max(m,Vector3.Distance(a.WorldPosition[i],b.WorldPosition[i]));return m;}
        // The eject's sounds (Claude's table beside Astra's NativeDeploySounds): every moment on my pad once, none for a late joiner, the
        // release only in the full eject, deterministic finite click-free clips.
        private static void EjectSounds()
        {
            var cues=new NativeEjectSounds.Cue[NativeEjectSounds.Limit];
            foreach(var cut in new[]{NativeDeployEject.Cut.Full,NativeDeployEject.Cut.Short})for(int mine=0;mine<4;mine++)
            {
                var heard=new List<NativeEjectSounds.Kind>();float before=float.NaN;
                for(int f=0;f<=(int)(NativeDeployEject.TravelOf(cut)*60)+30;f++){float now=f/60f;int n=NativeEjectSounds.Heard(mine,cut,before,now,cues);for(int i=0;i<n;i++)heard.Add(cues[i].Kind);before=now;}
                Check(heard.Distinct().Count()==heard.Count&&heard.Count==(cut==NativeDeployEject.Cut.Full?NativeEjectSounds.Count:NativeEjectSounds.Count-1),"each eject sound once ("+cut+", pad "+mine+": "+string.Join(",",heard)+")");
                Check(NativeEjectSounds.Heard(mine,cut,float.NaN,3,cues)<=1&&NativeEjectSounds.Heard(mine,cut,1,4,cues)<=1,"a late or jumping observer hears no backlog");
            }
            for(int i=0;i<NativeEjectSounds.Count;i++)
            {
                var pcm=NativeEjectSounds.Samples(i);Check(pcm.Length==(int)Math.Ceiling(NativeEjectSounds.Duration(i)*NativeEjectSounds.SampleRate)&&pcm.All(x=>float.IsFinite(x)&&Math.Abs(x)<1)&&pcm[0]==0&&pcm[pcm.Length-1]==0,"eject sound "+i+": finite, unclipped, click-free");
                Check(pcm.Any(x=>Math.Abs(x)>.05f)&&pcm.SequenceEqual(NativeEjectSounds.Samples(i)),"eject sound "+i+": audible and deterministic");
                Check(NativeEjectSounds.Key((NativeEjectSounds.Kind)i).StartsWith("eject.",StringComparison.Ordinal),"eject sound "+i+": a presentation key");
            }
        }
        // The cradle card (NativeCradleCard): lamps READY, LOADING n% and LOST from the party loading model's states, the altimeter
        // 12,000 m to 600 m with the load, cached labels, the night map recolouring the holo map, the streaks' spots on its square.
        private static void Cradle()
        {
            var card=new NativeCradleCard();card.FromParty(new[]{"YOU","PILOT 2","PILOT 3"},3,0);
            Check(card.Count==3&&card.You[0]&&!card.You[1]&&card.Lamps.Take(3).All(l=>l==NativeCradleCard.Lamp.Loading),"the station's party, all loading, me marked");
            Check(NativeCradleCard.LampOf(PartyLoadState.Ready)==NativeCradleCard.Lamp.Ready&&NativeCradleCard.LampOf(PartyLoadState.InWorld)==NativeCradleCard.Lamp.Ready&&NativeCradleCard.LampOf(PartyLoadState.Dropped)==NativeCradleCard.Lamp.Lost&&NativeCradleCard.LampOf(PartyLoadState.Loading)==NativeCradleCard.Lamp.Loading,"lamps from the party's states");
            Check(NativeCradleCard.Label(NativeCradleCard.Lamp.Loading,.64f)=="LOADING 64%"&&NativeCradleCard.Label(NativeCradleCard.Lamp.Ready,1)=="READY"&&NativeCradleCard.Label(NativeCradleCard.Lamp.Lost,-1)=="LOST"&&NativeCradleCard.Label(NativeCradleCard.Lamp.Loading,-1)=="LOADING","READY, LOADING 64% and LOST");
            Check(ReferenceEquals(NativeCradleCard.Label(NativeCradleCard.Lamp.Loading,.5f),NativeCradleCard.Label(NativeCradleCard.Lamp.Loading,.5f)),"lamp labels are cached");
            Check(NativeCradleCard.AltitudeAt(0)==12000&&NativeCradleCard.AltitudeAt(1)==600&&NativeCradleCard.AltitudeLabel(0)=="12,000 M"&&NativeCradleCard.AltitudeLabel(1)=="600 M"&&ReferenceEquals(NativeCradleCard.AltitudeLabel(.5f),NativeCradleCard.AltitudeLabel(.5f)),"the altimeter falls 12,000 m to 600 m with the load");
            float lastAlt=float.MaxValue;for(float l=0;l<=1;l+=.01f){float al=NativeCradleCard.AltitudeAt(l);Check(al<=lastAlt,"the altimeter only falls");lastAlt=al;}
            var roster=new NativePartyLoadModel{StateOverride=m=>m.account=="b"?PartyLoadState.Loading:m.account=="c"?PartyLoadState.Dropped:(PartyLoadState?)null,ProgressOverride=m=>m.account=="b"?.64f:-1};
            var view=new ExpeditionView{viewer="a",world="hub",area="hub",members=new[]{new ExpeditionViewMember{account="a",name="Me",connected=true,area="hub",seat=1},new ExpeditionViewMember{account="b",name="Two",connected=true,area="hub",seat=2},new ExpeditionViewMember{account="c",name="Three",connected=false,area="hub",seat=3}},areaBarrier=new ExpeditionAreaBarrierView{active=true,target="hub"}};
            roster.Build(view,1,true,1,0,new ExpeditionLabels());card.FromRoster(roster,1);
            Check(card.Count==3&&card.Lamps[0]==NativeCradleCard.Lamp.Ready&&card.Lamps[1]==NativeCradleCard.Lamp.Loading&&Math.Abs(card.Progress[1]-.64f)<1e-4f&&card.Lamps[2]==NativeCradleCard.Lamp.Lost,"the card's lamps from the roster: READY, LOADING 64%, LOST ("+string.Join(",",Enumerable.Range(0,card.Count).Select(i=>card.Lamps[i]))+")");
            var a2=new NativeExpeditionDeployment("station","host",4242);a2.RollInstances("host",Pool);var s=a2.Snapshot();var night=NativeCradleCard.NightMap(s,100,76);var day=NativeDeployPresentation.Preview(s,100,76);
            float L(int c)=>.3f*((c>>16)&255)+.59f*((c>>8)&255)+.11f*(c&255);Check(night.Length==day.Length&&night.All(c=>((uint)c>>24)==255)&&night.Average(c=>L(c))<day.Average(c=>L(c))+40,"the night map is opaque and dark");
            var uv=NativeCradleCard.MapUv(0,0);Check(Math.Abs(uv.X-.5f)<1e-4f&&Math.Abs(uv.Y-.5f)<1e-4f&&NativeCradleCard.MapUv(-NativeDeployPresentation.WorldHalfX,NativeDeployPresentation.WorldHalfZ)==Vector2.Zero,"the map's square: the plaza in the middle, north-west at the top left");
            for(int k=0;k<4;k++)Check(Math.Abs(NativeCradleCard.StreakFrom(k).Length()-1)<1e-4f,"each pod's streak has a bearing");
            Check(NativeCradleCard.Lightning(3.3f,true)==0,"no lightning with reduced effects");
            // (W3 review: the card showed on every load into the hub) the card covers only the load requested from the station after this
            // client saw its eject: not a later return, reprint, reconnect or area load into the same hub, nor a late joiner's first load
            var latch=new NativeCradleLatch();Check(!latch.Covers(0)&&!latch.Covers(-1),"no load before the eject");
            latch.Requested(7,false);Check(!latch.Covers(7),"a late joiner's or a return's load keeps the loading screen");
            latch.Requested(8,true);Check(latch.Covers(8),"the load that leaves the station is the pod's");
            Check(!latch.Covers(9)&&!latch.Covers(12),"every later load into the hub keeps the loading screen");
            latch.Requested(9,false);Check(!latch.Covers(9)&&latch.Covers(8),"a later load never takes the card");
        }
        // The pod ride (NativePodRide): one braced second in my cabin at -4.1 s of a 5 s opening, an earlier shorter one in today's 3 s
        // that ends before the first pod lands; none outside its window; steady with camera shake off.
        private static void Ride()
        {
            Check(NativePodRide.Opening==3&&Math.Abs(NativePodRide.RideAt(5)+4.1f)<1e-4f&&NativePodRide.RideSeconds(5)==1f,"the ride at -4.1 s for a second of a 5 s opening");
            foreach(float opening in new[]{3f,5f})
            {
                float at=NativePodRide.RideAt(opening),len=NativePodRide.RideSeconds(opening);
                Check(at>-opening&&at+len<NativePodDrop.ImpactOf(0)-.2f,"the ride ends before the first pod lands ("+opening+" s)");
                Check(!NativePodRide.Active(at-.01f,opening,out _)&&NativePodRide.Active(at+.01f,opening,out float u)&&u<.05f&&!NativePodRide.Active(at+len+.01f,opening,out _),"the ride's window ("+opening+" s)");
                NativePodRide.Judder(at+.3f,.3f,false,out var o,out float p,out float r);Check(o==Vector3.Zero&&p==0&&r==0,"steady with camera shake off");
                NativePodRide.Judder(at+.3f,.3f,true,out o,out p,out r);Check(o.Length()>0&&o.Length()<.02f&&Math.Abs(p)<1.5f&&Math.Abs(r)<2,"a braced judder with camera shake on");
            }
            Check(NativePodRide.Eye.Y>NativePodModels.Floor+.8f&&NativePodRide.Eye.Y<NativePodModels.Height-.6f&&NativePodRide.Look.Z>0,"the seated eye looks at the door");
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
