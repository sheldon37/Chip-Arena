#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The ROUNDS adaptation packs' bespoke visuals (owner 2026-10-19: "Go ahead and do the visuals for this thing as well: Some
    // effects may have no visuals of their own yet, such as Escort Array's orbiting satellites, Drone Nest's combat toys and the
    // fire, frost and slow-time fields."), presentation only (Core NativeRoundsLooks/Fields/Units/Procs/Fx/Meshes, Runtime
    // NativeRoundsFxPresentation, Resources NativeRoundsField/NativeRoundsWarp):
    //  - the catalogue: which pack chip made a field, a toy or a satellite (the owner's seat chips; a vanilla holder keeps the
    //    vanilla look), the procs, and the 138 card icons (pinned icons, never cpu or sparkles, distinct within each pack) and the
    //    MODS entry's own glyph;
    //  - the visible extent is the authority radius: every field's ground boundary (its outermost ring) is exactly the radius a
    //    standing pilot is inside (R for a field born at a pilot's centre), the slow-time and gravity spheres are the authority
    //    sphere, and the models stay inside; the preview's radii and lives are the authority's (source mirror);
    //  - the lifetime matches: the hazard look from the birth to the expiry exactly (also after the field leaves the replicated
    //    list, as on a guest), then only its dissipation, then nothing;
    //  - deterministic: two clients at different frame rates, and one that joins late, draw identical particles, surfaces and
    //    models at the same clock; the satellites sit exactly where the authority launches a homing shot from;
    //  - the drones' life cycle (summon, hover, attack, expiry blink, power-down without a death record, breakup with one) and
    //    the procs from their replicated events;
    //  - the replicated state they are bound to (Astra's PR #288): the swap event draws Position Exchange's and Dimensional
    //    Exchange's swap at both ends on the presentation clock (any client, a late joiner, the killcam); the drones blink and power
    //    down from the minion frame's until (a late joiner too; the summon's 6 s only without one) and a death record's owner makes
    //    an unseen drone's breakup; every protection shell ends at the pilot's shieldUntil, extended, shortened or cleared (the
    //    grant durations only without one);
    //  - the Reduced and Low tiers draw less; nothing allocates per frame;
    //  - sources: no authority, network or rules file names the new types, the presentation's mirrors of the authority still
    //    match it, the hooks are in place, the shaders keep their harness sections and their metas.
    internal static class RoundsVisualChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("ROUNDS visuals: "+what);count++;}
        private static Dictionary<string,int> Chips(params string[] ids){var d=new Dictionary<string,int>();foreach(var id in ids)d[id]=1;return d;}
        public static void Run(string root)
        {
            count=0;Catalogue(root,out string cat);Extent(out string extent);Lifetime(out string life);Determinism(out string det);Satellites(out string sat);
            Drones(out string drones);Expiry(out string expiry);Procs(out string procs);Swaps(out string swaps);Shells(out string shells);Tiers(out string tiers);Allocation(out string alloc);Source(root,out string src);
            Console.WriteLine("PASS ROUNDS visuals ("+count+" checks: "+cat+"; "+extent+"; "+life+"; "+det+"; "+sat+"; "+drones+"; "+expiry+"; "+procs+"; "+swaps+"; "+shells+"; "+tiers+"; "+alloc+"; "+src+")");
        }

        // ---- the catalogue ----
        private static void Catalogue(string root,out string summary)
        {
            var all=NativeRoundsCards.Chips().Concat(NativeRoundsPacks.Chips()).ToArray();var byId=all.ToDictionary(c=>c.id);
            Check(all.Length==138,"138 pack chips");
            // Field looks: each look's chip is a pack chip whose mechanic is the vanilla effect its fields carry.
            foreach(var l in NativeRoundsLooks.Looks)
            {
                Check(byId.ContainsKey(l.Chip),"field look chip exists: "+l.Chip);var mech=byId[l.Chip].mechanic;int look=NativeRoundsLooks.LookOf(l.Chip);
                Check(NativeRoundsLooks.FieldLook(mech,Chips(l.Chip))==look,l.Name+" resolves from its mechanic "+mech);
                Check(NativeRoundsLooks.FieldLook(mech,Chips(mech))==-1,l.Name+": a vanilla holder keeps the vanilla look");
                Check(NativeRoundsLooks.FieldLook(mech,Chips(mech,l.Chip))==-1,l.Name+": holding the vanilla chip too keeps the vanilla look");
                Check(NativeRoundsLooks.FieldLook(mech,new Dictionary<string,int>{{l.Chip,0}})==-1,l.Name+": a zero count is not held");
                Check(NativeRoundsLooks.FieldLook("wildfire",Chips(l.Chip))==-1,l.Name+": another effect is not claimed");
                Check(l.HotHex>0&&l.HotHex<0x1000000&&l.DarkHex>=0&&l.CoolHex<0x1000000,l.Name+" palette is sRGB hex");
            }
            Check(NativeRoundsLooks.FieldLook("hot-foot",null)==-1&&NativeRoundsLooks.FieldLook(null,Chips(NativeRoundsLooks.Looks[0].Chip))==-1,"no owner or no effect: vanilla");
            // Every pack field mechanic has a look (the field-making aliases).
            foreach(var c in all.Where(c=>c.mechanic=="hot-foot"||c.mechanic=="blizzard"||c.mechanic=="time-warp"||c.mechanic=="freezer-block"||c.mechanic=="repulsion-pad"||c.mechanic=="panic-button"||c.mechanic=="black-hole"||c.mechanic=="lightning-rod"))
                Check(NativeRoundsLooks.FieldLook(c.mechanic,Chips(c.id))>=0,"every pack field chip has a look: "+c.id);
            for(int i=0;i<NativeRoundsLooks.SatelliteChips.Length;i++){var id=NativeRoundsLooks.SatelliteChips[i];Check(byId[id].mechanic=="orbitals"&&NativeRoundsLooks.SatelliteLook(Chips(id))==i,"satellite look "+id);Check(NativeRoundsLooks.SatelliteLook(Chips(id,"orbitals"))==-1,"vanilla Satellite Office keeps its look");}
            Check(all.Count(c=>c.mechanic=="orbitals")==4,"four satellite chips");
            Check(byId[NativeRoundsLooks.DroneChip].mechanic=="puppet-master"&&NativeRoundsLooks.Drone(Chips(NativeRoundsLooks.DroneChip))&&!NativeRoundsLooks.Drone(Chips("puppet-master"))&&!NativeRoundsLooks.Drone(Chips(NativeRoundsLooks.DroneChip,"puppet-master")),"Drone Nest draws drones; the Toy Soldier keeps its toys");
            Check(NativeRoundsLooks.Procs(Chips(NativeRoundsLooks.Cards+"phantom"))==RoundsProc.Phase&&NativeRoundsLooks.Procs(Chips(NativeRoundsLooks.Cards+"adware","pickpocket"))==RoundsProc.None&&NativeRoundsLooks.Procs(null)==RoundsProc.None,"procs resolve; a vanilla holder keeps the vanilla look");
            foreach(var id in NativeRoundsLooks.Bespoke())Check(byId.ContainsKey(id),"bespoke chip exists: "+id);
            // Icons.
            var json=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/Icons.json"));
            var icons=new HashSet<string>(Regex.Matches(json,"\"id\":\"([a-z0-9-]+)\",\"markup\"").Select(m=>m.Groups[1].Value));Check(icons.Count>=100&&icons.Contains("orbit")&&icons.Contains("cpu"),"pinned icon set read");
            var chipMap=new HashSet<string>(Regex.Matches(json,"\"id\":\"([a-z0-9:-]+)\",\"icon\"").Select(m=>m.Groups[1].Value));
            int distinct=0;
            foreach(var g in all.GroupBy(c=>c.id.StartsWith(NativeRoundsLooks.Cards)?"cardsplus":c.id.Substring(7,c.id.IndexOf(':')-7)))
            {
                var used=new HashSet<string>();
                foreach(var c in g)
                {
                    string icon=NativeRoundsLooks.Icon(c.id);Check(icon!=null&&icons.Contains(icon),"pinned icon for "+c.id);Check(icon!="cpu"&&icon!="sparkles","not the fallback or placeholder: "+c.id);
                    Check(NativeChipIcons.Native(c.id)==icon,"the card resolves it: "+c.id);Check(!chipMap.Contains(c.id),"the source map does not shadow "+c.id);Check(used.Add(icon),"distinct within "+g.Key+": "+c.id+" "+icon);
                }
                distinct+=used.Count;
            }
            Check(NativeRoundsLooks.AllIcons.Count()==138&&NativeChipIcons.Ids.Length==5&&NativeChipIcons.Native("stride")=="footprints"&&NativeChipIcons.Native("vital")==null&&NativeChipIcons.Native("example-mobility:hop-drive")==null,"138 pack icons; the movement icons and other chips unchanged");
            var home=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeHomeMenu.cs"));
            Check(home.Contains("(\"mods\",\"MODS\",\"05\",MenuScreen.Mods)")&&home.Contains("[\"mods\"]=(\"M7 7H17V17H7Z")&&home.Contains("QuietRow(ref qy,\"catalogue\",\"CATALOGUE\""),"the MODS entry has its own glyph; the catalogue keeps its book");
            summary="13 field looks, 4 satellite looks, the drone and "+Enum.GetValues(typeof(RoundsProc)).Length+" proc kinds resolve from seat chips; 138 pinned icons ("+distinct+" pack-distinct); the MODS glyph";
        }

        // ---- extent ----
        private static NativeRoundsFx Fx(){return new NativeRoundsFx();}
        private static void One(NativeRoundsFx fx,int look,Vector3 centre,float floor,float radius,float born,float until,float clock,bool observe=true,double q=1,bool low=false,int id=7)
        {
            fx.Begin(clock,q,low);if(observe)fx.Fields.Observe(new RoundsFieldInput{Id=id,Look=look,Centre=centre,Floor=floor,Radius=radius,Born=born,Until=until},clock);fx.End();
        }
        private static void Extent(out string summary)
        {
            float worst=0,worstSphere=0;int fields=0,heights=0;
            for(int look=0;look<NativeRoundsLooks.Looks.Length;look++)
            {
                NativeRoundsPreview.FieldRule(look,out float R,out float life);var l=NativeRoundsLooks.Looks[look];
                foreach(float dy in new[]{0f,-.865f,.6f,1.6f})
                {
                    var fx=Fx();float floor=.4f;var c=new Vector3(3,floor+NativeRoundsFields.PilotCentre+dy,-2);One(fx,look,c,floor,R,0,life,life*.5f);
                    float g=NativeRoundsFields.GroundRadius(R,c.Y,floor);Check(Math.Abs(g-(float)Math.Sqrt(Math.Max(0,R*R-dy*dy)))<1e-4f,"ground radius is the standing pilot's");
                    Check(fx.Fields.ShapeCount==1,l.Name+" drawn");var s=fx.Fields.Shapes[0];Check(s.Hazard&&Math.Abs(s.Ground-g)<1e-5f,l.Name+" hazard shape");
                    // The ground surface: its outermost ring is exactly g, nothing beyond.
                    float max=0;int n=0;var V=fx.Ground.V;
                    for(int i=0;i<fx.Ground.VertexCount;i++){if(V[i].Cx!=0)continue;n++;float r=(float)Math.Sqrt((V[i].Px-c.X)*(V[i].Px-c.X)+(V[i].Pz-c.Z)*(V[i].Pz-c.Z));max=Math.Max(max,r);Check(Math.Abs(V[i].Py-(floor+NativeRoundsFields.Lift))<1e-5f,"the ground lies on its floor");Check(V[i].Cy==g&&V[i].Cz==R,"the vertices carry the drawn and authority radii");}
                    if(g>.05f){Check(n>0&&Math.Abs(max-g)<1e-4f,l.Name+" ground boundary is the hazard radius ("+max+" vs "+g+")");worst=Math.Max(worst,Math.Abs(max-g));Check(Math.Abs(s.GroundDrawn-g)<1e-5f,"shape records it");}
                    // The sphere (slow time, gravity, any field high off the floor): every vertex on the authority sphere.
                    bool sphere=l.Style==RoundsStyle.Time||l.Style==RoundsStyle.Pull||g<R*.55f;int sv=0;
                    for(int i=0;i<fx.Ground.VertexCount;i++){if(V[i].Cx!=1)continue;sv++;float d=Vector3.Distance(new Vector3(V[i].Px,V[i].Py,V[i].Pz),c);worstSphere=Math.Max(worstSphere,Math.Abs(d-R));Check(Math.Abs(d-R)<1e-3f,"sphere on the radius");Check(Math.Abs(V[i].Cy-(floor-c.Y))<1e-5f,"the sphere carries its floor");}
                    Check(sphere==(sv>0),l.Name+" draws its sphere when it should");if(sphere)Check(Math.Abs(s.DomeDrawn-R)<1e-6f,"dome radius recorded");
                    // Its models (crystals, thorns, pillars, pylons) stand inside the ground radius.
                    for(int i=0;i<fx.Lit.Vertices;i++){var p=fx.Lit.V[i].P;if(Math.Abs(p.Y-c.Y)<.2f&&l.Style==RoundsStyle.Pull)continue;float r=(float)Math.Sqrt((p.X-c.X)*(p.X-c.X)+(p.Z-c.Z)*(p.Z-c.Z));if(l.Style!=RoundsStyle.Pull)Check(r<=Math.Max(g,.3f)+.16f,l.Name+" model inside the boundary ("+r+" > "+g+")");}
                    fields++;heights++;
                }
            }
            summary=fields+" fields at 4 heights: ground boundary within "+worst.ToString("0.000000")+" m of the standing-pilot radius (R at a pilot's centre), spheres within "+worstSphere.ToString("0.0000")+" m of R, models inside";
        }

        // ---- lifetime ----
        private static void Lifetime(out string summary)
        {
            int samples=0;
            for(int look=0;look<NativeRoundsLooks.Looks.Length;look++)
            {
                NativeRoundsPreview.FieldRule(look,out float R,out float life);var st=NativeRoundsLooks.Looks[look].Style;float fade=NativeRoundsFields.Fade(st),born=2;var fx=Fx();var c=new Vector3(0,NativeRoundsFields.PilotCentre,0);
                for(float t=born-.3f;t<born+life+fade+.4f;t+=1/60f)
                {
                    // A guest's list drops the field at its expiry while it renders .1 s behind: observed until life - .1, then not.
                    bool listed=t>=born-.1f&&t<born+life-.1f;One(fx,look,c,0,R,born,born+life,t,listed);float age=t-born;
                    bool drawn=fx.Fields.ShapeCount==1;var s=drawn?fx.Fields.Shapes[0]:default;
                    if(age<-.05f)Check(!drawn,"nothing before the birth");
                    else if(age>=0&&age<=life){Check(drawn&&s.Hazard&&!s.Dissipating&&s.Strength>=.82f,NativeRoundsLooks.Looks[look].Name+" hazard look through its life (age "+age+")");Check(fx.Ground.VertexCount>0&&fx.Ground.V[0].Bz==age&&fx.Ground.V[0].Bw==life,"the surface ages on the field clock");}
                    else if(age>life+.001f&&age<life+fade-.001f)Check(drawn&&!s.Hazard&&s.Dissipating&&s.Strength<1,"only its dissipation after the expiry (age "+age+")");
                    else if(age>life+fade+.06f)Check(!drawn&&fx.Ground.VertexCount==0,"gone after its dissipation");
                    samples++;
                }
                // The shader draws the boundary only while age <= life (RfRim's alive = step(age, life) * step(0, age)).
            }
            summary=samples+" samples: every look draws its hazard from birth to expiry exactly (also once off the replicated list), then dissipates for its fade, then nothing";
        }

        // ---- a busy scene, fed identically on any client ----
        private static readonly Vector3 Owner=new Vector3(0,NativeRoundsFields.PilotCentre,0);
        private static void Feed(NativeRoundsFx fx,float t,bool late=false)
        {
            // Every field look laid out in a row; two satellite holders; three drones; procs from events.
            for(int l=0;l<NativeRoundsLooks.Looks.Length;l++){NativeRoundsPreview.FieldRule(l,out float R,out float life);float born=.3f+l*.05f;if(t<born+life)fx.Fields.Observe(new RoundsFieldInput{Id=100+l,Look=l,Centre=new Vector3(l*12,NativeRoundsFields.PilotCentre,20),Floor=0,Radius=R,Born=born,Until=born+life},t);}
            // Pilot 1's replicated protection: the first block's .6 s, the second's 3.9 cut short to 3.5 (it shatters).
            float shield=t<.5f?0:t<3.3f?1.1f:t<3.5f?3.9f:3.5f;
            fx.Pilot(1,Owner,.3f,true,0,RoundsProc.Phase|RoundsProc.Stasis|RoundsProc.Regen|RoundsProc.Swap,0,0,shield);fx.Pilot(2,new Vector3(6,NativeRoundsFields.PilotCentre,2),1.2f,true,3,RoundsProc.None,0,t<2.4f?2.4f:0);
            for(int d=0;d<3;d++)
            {
                int id=10000+d;float spawn=.4f+d*.3f;if(t<spawn)continue;var at=new Vector3(-3+d*1.5f,.9f,4);
                if(!late||t>1.5f){if(t-spawn<1.2f)fx.Units.Summoned(id,1,spawn,at);}
                if(d==2&&t>=3){fx.Units.Died(id,3,at,0,1,new Vector3(.1f,.8f,.7f));continue;}if(d==1&&t>=spawn+NativeRoundsLooks.DroneLife)continue;
                float last=t<1?-100:1+(float)Math.Floor((t-1)/1.1f)*1.1f;fx.Units.Observe(new RoundsDroneInput{Id=id,Owner=1,At=at,Velocity=new Vector3(.3f,0,0),Yaw=.4f,LastAttack=last,LastHit=t>2.2f?2.2f:-100,Health=1,Accent=new Vector3(.1f,.8f,.7f),Until=spawn+NativeRoundsLooks.DroneLife},t);
                float strike=last+NativeRoundsLooks.DroneWindup;if(t>=strike&&t<strike+.22f)fx.Units.Beam(new RoundsBeamInput{From=at+new Vector3(0,NativeRoundsLooks.DroneMuzzle,0),To=new Vector3(0,1,9),Until=strike+.22f},t);
            }
            for(int k=0;k<8;k++){float at=.5f+k*.7f;if(t<at||t>at+1.5f)continue;var e=new ArenaEvent{Id=500+k,Kind=k%4==0?EventKind.Block:k%4==1?EventKind.Freeze:k%4==2?EventKind.Heal:EventKind.Explosion,Source=1,Target=k%4==1?2:1,Time=at,Amount=1.2f,Position=new V3(1,1,1),Detail=k%4==3?"static":null,Radius=3};
                fx.Procs.Event(e,RoundsProc.Phase|RoundsProc.Stasis|RoundsProc.Regen|RoundsProc.ArcFault,true,false,1,0);}
            // The swaps: Position Exchange at 2 s, Dimensional Exchange at 4 s (pilot 1 traded places with pilot 2).
            for(int k=0;k<2;k++){float at=2+k*2;if(t<at||t>at+1.5f)continue;var e=new ArenaEvent{Id=600+k,Kind=EventKind.Teleport,Detail="swap",Source=1,Target=2,Time=at,From=new V3(6,NativeRoundsFields.PilotCentre,2),Position=new V3(Owner.X,Owner.Y,Owner.Z)};fx.Procs.Event(e,k==0?RoundsProc.Swap:RoundsProc.Shuffle,false,false,0,0);}
            fx.Shot(new Vector3(36+(t%2)*4,1,20),new Vector3(30,0,0));
        }
        private static void Step(NativeRoundsFx fx,float t,double q=1,bool low=false,bool late=false){fx.Begin(t,q,low);Feed(fx,t,late);fx.End();}
        private static bool Same(NativeRoundsFx a,NativeRoundsFx b,out string why)
        {
            why=null;
            if(a.Particles.Count!=b.Particles.Count){why="particle count "+a.Particles.Count+" vs "+b.Particles.Count;return false;}
            for(int i=0;i<a.Particles.Count;i++){var x=a.Particles.Items[i];var y=b.Particles.Items[i];if(x.Kind!=y.Kind||x.At!=y.At||x.Axis!=y.Axis||x.Rgb!=y.Rgb||x.A!=y.A||x.Size!=y.Size||x.U!=y.U||x.Seed!=y.Seed){why="particle "+i;return false;}}
            if(a.Ground.VertexCount!=b.Ground.VertexCount||a.Ground.IndexCount!=b.Ground.IndexCount){why="ground size";return false;}
            for(int i=0;i<a.Ground.VertexCount;i++)if(!a.Ground.V[i].Equals(b.Ground.V[i])){why="ground vertex "+i;return false;}
            if(a.Lit.Vertices!=b.Lit.Vertices){why="lit size";return false;}
            for(int i=0;i<a.Lit.Vertices;i++)if(!a.Lit.V[i].Equals(b.Lit.V[i])){why="lit vertex "+i;return false;}
            return true;
        }
        private static void Determinism(out string summary)
        {
            // A 144 Hz client, a 30 Hz one with jittered frames, and one that only starts drawing at 1.5 s, compared at 0.1 s steps.
            var a=Fx();var b=Fx();var c=Fx();int compared=0;double tb=0;var rng=new Random(7);
            for(int k=1;k<=60;k++)
            {
                float t=k*.1f;
                for(double s=t-.1+1/144.0;s<t-1e-6;s+=1/144.0)Step(a,(float)s);Step(a,t);
                while(tb+1/30.0<t-1e-6){tb+=1/30.0*(.6+.8*rng.NextDouble());if(tb<t)Step(b,(float)tb);}Step(b,t);
                if(t>=1.5f)Step(c,t,1,false,true);
                Check(Same(a,b,out string why),"two clients draw the same frame at "+t+" ("+why+")");
                // The late joiner never saw drone 10000's Summon event: the minion frame's until still blinks it through its last .9 s
                // and powers it down at its expiry exactly as on the others (compared to the end, the swaps and the cut shell too).
                if(t>=1.6f)Check(Same(a,c,out string why2),"a late joiner draws the same frame at "+t+" ("+why2+")");
                compared++;
            }
            summary=compared+" moments: a 144 Hz client, a jittered 30 Hz one and a late joiner draw identical particles, surfaces and models (fields, satellites, drones with their replicated expiry, procs, swaps, protection shells)";
        }

        // ---- satellites ----
        private static void Satellites(out string summary)
        {
            // The mirror of the authority's launch point (NativeChips.SpawnChipShots).
            float worst=0;var fx=Fx();var body=new Vector3(2,1.3f,-1);
            for(int k=0;k<200;k++)
            {
                float t=k*.0371f;fx.Begin(t,1,false);fx.Pilot(4,body,0,true,k%4,RoundsProc.None,0,0);fx.Pilot(5,body+Vector3.UnitX*3,0,true,-1,RoundsProc.None,0,0);fx.End();
                Check(fx.Units.SatelliteCount==2,"two satellites for the holder, none for the other");
                for(int i=0;i<2;i++){var s=fx.Units.Satellites[i];float a=t*4+(s.side>0?0:(float)Math.PI);var auth=body+new Vector3(0,.55f,0)+new Vector3((float)Math.Sin(a)*.75f,.25f*(float)Math.Sin(a*2),(float)Math.Cos(a)*.75f);worst=Math.Max(worst,Vector3.Distance(auth,s.at));Check(Vector3.Distance(auth,s.at)<1e-4f,"satellite at the authority's launch point");}
                Check(fx.Lit.Pieces>0,"satellite models drawn");
            }
            fx.Begin(0,1,false);fx.Pilot(4,body,0,false,0,RoundsProc.None,0,0);fx.End();Check(fx.Units.SatelliteCount==0,"a dead holder has none");
            // A homing shot first seen at its birth flashes the satellite it left; a plain shot draws no trail.
            var g=Fx();float born=1;g.Begin(born,1,false);g.Pilot(4,body,0,true,0,RoundsProc.None,0,0);var start=NativeRoundsLooks.SatelliteAt(body,born,-1);
            g.Units.Shot(new RoundsShotInput{Id=77,Owner=4,Look=0,Homing=true,P=start,V=new Vector3(20,0,0),Born=born},born,g.Pilots,g.PilotCount);
            g.Units.Shot(new RoundsShotInput{Id=78,Owner=4,Look=0,Homing=false,P=start,V=new Vector3(20,0,0),Born=born},born,g.Pilots,g.PilotCount);g.End();int withFlash=g.Particles.Count;
            var h=Fx();h.Begin(born,1,false);h.Pilot(4,body,0,true,0,RoundsProc.None,0,0);h.End();Check(withFlash>h.Particles.Count+4,"the homing shot's flash and head drawn");
            summary="200 frames x 4 looks: satellites within "+worst.ToString("0.000000")+" m of the authority's homing-shot launch point; a homing shot flashes its satellite; none for a dead holder";
        }

        // ---- drones ----
        private static byte Phase(NativeRoundsFx fx,int id){for(int i=0;i<fx.Units.DroneCount;i++)if(fx.Units.DroneShapes[i].Id==id)return fx.Units.DroneShapes[i].Phase;return 0;}
        private static void Drones(out string summary)
        {
            var fx=Fx();float spawn=1;var at=new Vector3(0,.9f,0);int seen=0;var phases=new HashSet<byte>();
            for(float t=.9f;t<9;t+=1/60f)
            {
                fx.Begin(t,1,false);
                if(t>=spawn&&t<spawn+.05f)fx.Units.Summoned(1,1,spawn,at);
                bool alive=t>=spawn&&t<spawn+NativeRoundsLooks.DroneLife;float last=t<2?-100:2;
                if(alive)fx.Units.Observe(new RoundsDroneInput{Id=1,Owner=1,At=at,Yaw=0,LastAttack=last,LastHit=-100,Health=1,Accent=Vector3.One},t);
                // A second drone killed at 3.
                if(t>=spawn+.2f&&t<3)fx.Units.Observe(new RoundsDroneInput{Id=2,Owner=1,At=at+Vector3.UnitX,Yaw=0,LastAttack=-100,LastHit=-100,Health=1,Accent=Vector3.One},t);
                if(t>=3)fx.Units.Died(2,3,at+Vector3.UnitX,0);
                fx.End();byte p=Phase(fx,1),q=Phase(fx,2);float age=t-spawn;phases.Add(p);
                if(t<spawn)Check(p==0,"no drone before its summon");
                else if(age<NativeRoundsUnits.SummonIn)Check(p==NativeRoundsUnits.Summoning,"summoning at "+age);
                else if(t>=2&&t<2+NativeRoundsLooks.DroneWindup+NativeRoundsLooks.DroneRecover&&age<NativeRoundsLooks.DroneLife-.9f)Check(p==NativeRoundsUnits.Attacking,"attacking through the windup and recovery at "+t);
                else if(age>NativeRoundsLooks.DroneLife-.9f&&age<NativeRoundsLooks.DroneLife)Check(p==NativeRoundsUnits.Expiring,"blinking in its last second at "+age);
                else if(age>=NativeRoundsLooks.DroneLife+.02f&&age<NativeRoundsLooks.DroneLife+NativeRoundsUnits.PowerDown-.02f)Check(p==NativeRoundsUnits.PoweringDown,"powering down when it leaves without a death record at "+age);
                else if(age>NativeRoundsLooks.DroneLife+NativeRoundsUnits.PowerDown+.6f)Check(p==0,"gone after its power-down");
                else if(age>NativeRoundsUnits.SummonIn&&age<NativeRoundsLooks.DroneLife-.9f)Check(p==NativeRoundsUnits.Hovering,"hovering at "+age);
                if(t>=3&&t<3+NativeRoundsUnits.Breakup)Check(q==NativeRoundsUnits.Destroyed&&fx.Units.Known(2),"breaking up after its death record at "+t);
                if(t>=3+NativeRoundsUnits.Breakup+.05f)Check(q==0&&fx.Units.Known(2)==(t<3+NativeRoundsUnits.DeathMemory),"remembered while its death record can be listed, then forgotten");
                seen++;
            }
            Check(phases.Contains(NativeRoundsUnits.Summoning)&&phases.Contains(NativeRoundsUnits.Hovering)&&phases.Contains(NativeRoundsUnits.Attacking)&&phases.Contains(NativeRoundsUnits.Expiring)&&phases.Contains(NativeRoundsUnits.PoweringDown),"every phase seen");
            // A drone first seen without its Summon event (a late join): no summon-in, no expiry blink.
            var late=Fx();for(float t=4;t<9;t+=.1f){late.Begin(t,1,false);late.Units.Observe(new RoundsDroneInput{Id=9,Owner=1,At=at,LastAttack=-100,LastHit=-100,Health=1,Accent=Vector3.One},t);late.End();var p=Phase(late,9);Check(p==NativeRoundsUnits.Hovering,"a late-seen drone just hovers");}
            summary=seen+" frames: summon-in, hover, attack, expiry blink, power-down without a death record, breakup with one, forgotten after; a late-seen drone just hovers";
        }

        // ---- drone expiry and death owner (the minion frame's until, the death record's owner) ----
        private static void Expiry(out string summary)
        {
            var at=new Vector3(1,.9f,2);var acc=new Vector3(.1f,.8f,.7f);int frames=0;
            // Its replicated until (7.5 s: not the 6 s from its summon at 1, so the bound value is what is drawn) blinks it from 6.6 s
            // and powers it down from 7.5 s, whether this client saw its Summon or first saw it at 4 s (a late joiner), and whether
            // its list entry ends at the expiry or a little before it (a guest's list runs ahead of its clock).
            foreach(bool summoned in new[]{true,false})foreach(float leaves in new[]{7.5f,7.3f})
            {
                var fx=Fx();float until=7.5f;
                for(float t=summoned?.9f:4;t<9.2f;t+=1/60f)
                {
                    fx.Begin(t,1,false);if(summoned&&t>=1&&t<1.05f)fx.Units.Summoned(5,1,1,at);
                    if(t<leaves&&(summoned?t>=1:true))fx.Units.Observe(new RoundsDroneInput{Id=5,Owner=1,At=at,LastAttack=-100,LastHit=-100,Health=1,Accent=acc,Until=until},t);
                    fx.End();byte p=Phase(fx,5);string who=(summoned?"summon seen":"late joiner")+", listed to "+leaves+", at "+t;
                    if(t>until-NativeRoundsUnits.BlinkLead+.02f&&t<until-.02f)Check(p==NativeRoundsUnits.Expiring,"blinks through the last .9 s before its replicated until ("+who+")");
                    else if(t>(summoned?1.5f:4)&&t<until-NativeRoundsUnits.BlinkLead-.02f)Check(p==NativeRoundsUnits.Hovering,"hovers before its last .9 s ("+who+")");
                    else if(t>until+.02f&&t<until+NativeRoundsUnits.PowerDown-.02f)Check(p==NativeRoundsUnits.PoweringDown,"powers down from its replicated until ("+who+")");
                    else if(t>until+NativeRoundsUnits.PowerDown+.6f)Check(p==0,"gone after its power-down ("+who+")");
                    frames++;
                }
            }
            // Replaced long before its expiry (it left the list at 3 s): powers down from its last sight, never blinks.
            var r=Fx();
            for(float t=.9f;t<5;t+=1/60f){r.Begin(t,1,false);if(t>=1&&t<1.05f)r.Units.Summoned(6,1,1,at);if(t>=1&&t<3)r.Units.Observe(new RoundsDroneInput{Id=6,Owner=1,At=at,LastAttack=-100,LastHit=-100,Health=1,Accent=acc,Until=7.5f},t);r.End();byte p=Phase(r,6);
                if(t>3.03f&&t<3+NativeRoundsUnits.PowerDown-.03f)Check(p==NativeRoundsUnits.PoweringDown,"a replaced drone powers down from its last sight at "+t);Check(p!=NativeRoundsUnits.Expiring,"a replaced drone never blinks");frames++;}
            // No replicated expiry (an old host: 0, or a replica's infinity): the 6 s from its Summon event, as before.
            foreach(float none in new[]{0f,float.PositiveInfinity})
            {
                var o=Fx();
                for(float t=.9f;t<8.5f;t+=1/60f){o.Begin(t,1,false);if(t>=1&&t<1.05f)o.Units.Summoned(7,1,1,at);if(t>=1&&t<7)o.Units.Observe(new RoundsDroneInput{Id=7,Owner=1,At=at,LastAttack=-100,LastHit=-100,Health=1,Accent=acc,Until=none},t);o.End();byte p=Phase(o,7);
                    if(t>6.12f&&t<6.98f)Check(p==NativeRoundsUnits.Expiring,"without an until: blinks from the summon's 6 s ("+none+")");if(t>7.03f&&t<7.38f)Check(p==NativeRoundsUnits.PoweringDown,"without an until: powers down from its last sight");frames++;}
            }
            // A death record's owner: a Drone Nest drone this client never saw alive (a late joiner, a killcam started after its
            // summon) breaks up as a drone in its owner's accent; without an owner (an old host) it stays the toy soldier's.
            int Breakup(Vector3 accent,out NativeRoundsFx fx,int owner=1){fx=Fx();fx.Begin(3.05f,1,false);fx.Units.Died(77,3,at,0,owner,accent);fx.End();return Phase(fx,77);}
            Check(Breakup(acc,out var b1)==NativeRoundsUnits.Destroyed&&b1.Units.Known(77)&&b1.Particles.Count>20,"an unseen drone's death record with its owner breaks it up as a drone");
            Breakup(new Vector3(.9f,.2f,.1f),out var b2);bool differs=false;for(int i=0;i<Math.Min(b1.Particles.Count,b2.Particles.Count);i++)if(b1.Particles.Items[i].Rgb!=b2.Particles.Items[i].Rgb){differs=true;break;}
            Check(differs&&b1.Particles.Count==b2.Particles.Count,"the breakup takes its owner's accent");
            Check(Breakup(acc,out var b3,0)==0&&!b3.Units.Known(77),"no owner (an old host): an unseen toy is not taken over");
            summary=frames+" frames: the drone blinks and powers down from its replicated until (summon seen or a late joiner, listed to its expiry or just before), a replaced one from its last sight, the summon's 6 s only without an until; an unseen drone's death record with its owner breaks up in the owner's accent";
        }

        // ---- the swap (Position Exchange, Dimensional Exchange) ----
        private static int Near(NativeRoundsFx fx,Vector3 p,float r){int n=0;for(int i=0;i<fx.Particles.Count;i++){var d=fx.Particles.Items[i].At-p;if(d.X*d.X+d.Z*d.Z<r*r&&Math.Abs(d.Y)<1.3f)n++;}return n;}
        private static void Swaps(out string summary)
        {
            var A=new Vector3(2,NativeRoundsFields.PilotCentre,1);var B=new Vector3(6.5f,NativeRoundsFields.PilotCentre,4);var mid=(A+B)*.5f;int frames=0;
            foreach(var (chip,variant) in new[]{("rounds-vecs:capture",0),("rounds-wwc:dimensional-shuffle",1)})
            {
                var procs=NativeRoundsLooks.Procs(Chips(chip));Check(procs==(variant==0?RoundsProc.Swap:RoundsProc.Shuffle),chip+" resolves its swap look");
                Check(NativeRoundsLooks.Procs(Chips(chip,"swap-shop"))==RoundsProc.None,chip+": holding the vanilla Swap Shop keeps the vanilla look");
                // The authority's swap event: From the owner's old body centre, Position its new one; the swapped pilot is the target.
                var e=new ArenaEvent{Id=70,Kind=EventKind.Teleport,Detail="swap",Source=1,Target=2,Time=1,From=new V3(A.X,A.Y,A.Z),Position=new V3(B.X,B.Y,B.Z)};
                NativeRoundsFx Frame(float t,RoundsProc pr,ArenaEvent ev,Vector3? owner=null,bool bodies=true,NativeRoundsFx fx=null)
                {fx??=Fx();fx.Begin(t,1,false);if(bodies){fx.Pilot(1,owner??B,0,true,-1,pr,0,0);fx.Pilot(2,A,0,true,-1,RoundsProc.None,0,1.5f);}if(t>=ev.Time&&t<ev.Time+1.5f)fx.Procs.Event(ev,pr,false,false,0,0);fx.End();return fx;}
                Check(Frame(.99f,procs,e).Particles.Count==0,"nothing before the swap");
                // Both ends from the swap's first moment: the departure flash exactly on each old body centre.
                var f0=Frame(1.02f,procs,e);bool flashA=false,flashB=false;for(int i=0;i<f0.Particles.Count;i++){var it=f0.Particles.Items[i];if(it.Kind!=BossVfxKind.Glow||it.Size<1)continue;flashA|=it.At==A;flashB|=it.At==B;}
                Check(flashA&&flashB,"phases out at both ends on the event's From and Position ("+chip+")");
                foreach(float age in new[]{.02f,.06f,.12f,.2f,.3f,.4f}){var f=Frame(1+age,procs,e);Check(Near(f,A,.8f)>30&&Near(f,B,.8f)>30,"draws at both ends at "+age+" s ("+chip+": "+Near(f,A,.8f)+", "+Near(f,B,.8f)+")");frames++;}
                Check(Near(Frame(1.1f,procs,e),mid,.9f)>8,"the streams and afterimages cross between the ends");
                Check(Frame(1+NativeRoundsProcs.SwapSpan+.5f,procs,e).Particles.Count==0,"over after its span and the swapped pilot's .5 s daze");
                // The arrival follows the live body (the owner may already have moved on); with no body presented, the event's end.
                var moved=B+new Vector3(0,0,3);Check(Near(Frame(1.25f,procs,e,moved),moved,.6f)>15,"the owner's arrival builds on its live body");Check(Near(Frame(1.25f,procs,e,null,false),B,.6f)>15,"no body presented: the arrival builds at the event's end");
                Check(Frame(1.1f,RoundsProc.None,e).Particles.Count==0,"no look without the chip");
                var blink=e;blink.Detail=null;Check(Frame(1.1f,procs,blink).Particles.Count==0,"a teleport without the swap detail is not a swap");
                var both=new NativeRoundsProcs();both.Event(e,procs|RoundsProc.Reclaim|RoundsProc.Transit,false,false,0,0);Check(both.Count==1&&both.Items[0].Kind==RoundsProcKind.Swap&&both.Items[0].Variant==variant,"a Reclaim or Transit holder's swap is drawn as the swap only");
                var lit=Frame(1.12f,procs,e).Lit.Pieces;Check(variant==1?lit>0:lit==0,"Dimensional Exchange's rift sheds shards; Position Exchange draws none");
                // Aged on the presentation clock from the event's time: a client stepping at 144 Hz, one that first draws mid-swap and
                // the killcam (its clock runs back to before the swap and replays it) draw the same frame at the same clock.
                var run=Fx();for(float t=.95f;t<1.3f;t+=1/144f)Frame(t,procs,e,null,true,run);Frame(1.3f,procs,e,null,true,run);
                Check(Same(run,Frame(1.3f,procs,e),out string why),"a late joiner draws the same swap ("+why+")");
                Frame(1.6f,procs,e,null,true,run);for(float t=.9f;t<1.18f;t+=1/60f)Frame(t,procs,e,null,true,run);Frame(1.18f,procs,e,null,true,run);
                Check(Same(run,Frame(1.18f,procs,e),out string why2),"the killcam replays the same swap ("+why2+")");
            }
            var p0=Frame2(0);var p1=Frame2(1);static NativeRoundsFx Frame2(int v){var fx=Fx();fx.Begin(1.15f,1,false);fx.Procs.Event(new ArenaEvent{Id=71,Kind=EventKind.Teleport,Detail="swap",Source=1,Target=2,Time=1,From=new V3(0,1,0),Position=new V3(4,1,0)},v==0?RoundsProc.Swap:RoundsProc.Shuffle,false,false,0,0);fx.End();return fx;}
            Check(!Same(p0,p1,out _),"the two swap looks differ");
            summary=frames+" swap ages x 2 looks: both ends from the event's From and Position (phase-out, streams and afterimages between, phase-in on the live bodies), none without the chip or the swap detail, aged on the presentation clock (a late joiner and the killcam draw the same), Dimensional Exchange distinct";
        }

        // ---- protection shells (the pilot's replicated shieldUntil) ----
        private static void Shells(out string summary)
        {
            int frames=0;var c=new Vector3(0,NativeRoundsFields.PilotCentre,0);
            ArenaEvent Grant(RoundsProcKind k,int id,float at)=>k==RoundsProcKind.Phase?new ArenaEvent{Id=id,Kind=EventKind.Block,Source=1,Time=at}:k==RoundsProcKind.Shield?new ArenaEvent{Id=id,Kind=EventKind.Field,Detail="panic-button",Source=1,Time=at}:new ArenaEvent{Id=id,Kind=EventKind.Teleport,Source=1,Time=at,From=new V3(0,1,-3),Position=new V3(c.X,c.Y,c.Z)};
            RoundsProc Holds(RoundsProcKind k)=>k==RoundsProcKind.Phase?RoundsProc.Phase:k==RoundsProcKind.Shield?RoundsProc.Vent:RoundsProc.Reclaim;
            // Steps a pilot granted protection of kind k at 1 s, its replicated shieldUntil `shield(t)`, from `from` at 60 Hz; per frame:
            // the shell drawn (and the end it is drawn to) and the shatter.
            void Run(string what,RoundsProcKind k,Func<float,float> shield,float end,bool cut,float from=.9f)
            {
                var fx=Fx();var e=Grant(k,90,1);
                for(float t=from;t<3.6f;t+=1/60f)
                {
                    fx.Begin(t,1,false);fx.Pilot(1,c,0,true,-1,Holds(k),0,0,shield(t));if(t>=1&&t<2.5f)fx.Procs.Event(e,Holds(k),false,false,0,0);fx.End();
                    bool shell=false,shatter=false;float drawnTo=float.NaN;for(int i=0;i<fx.Procs.ShellCount;i++){var sh=fx.Procs.Shells[i];if(sh.Seat!=1||sh.Kind!=k)continue;if(sh.Shatter)shatter=true;else{shell=true;drawnTo=sh.End;}}
                    float bound=shield(t)>0?shield(t):end;if(t>=1&&t<end-.001f)Check(shell&&Math.Abs(drawnTo-bound)<1e-4f,what+": drawn to "+bound+" at "+t+" (to "+drawnTo+")");
                    if(t>=end+.001f)Check(!shell,what+": no shell after "+end+" at "+t);
                    if(t>=end+.001f&&t<end+NativeRoundsProcs.ShatterSpan-.001f)Check(shatter==cut,what+": "+(cut?"shatters":"no shatter")+" after its end at "+t);
                    if(t>=end+NativeRoundsProcs.ShatterSpan+.001f)Check(!shatter,what+": the shatter is over at "+t);
                    frames++;
                }
            }
            foreach(var k in new[]{RoundsProcKind.Phase,RoundsProcKind.Shield,RoundsProcKind.Reclaim})
            {
                string n=k==RoundsProcKind.Phase?"Phase Reserve":k==RoundsProcKind.Shield?"Emergency Vent":"Reclamation Core";float fallback=k==RoundsProcKind.Phase?NativeRoundsProcs.PhaseFallback:k==RoundsProcKind.Shield?NativeRoundsProcs.VentFallback:NativeRoundsProcs.ReclaimFallback;
                Run(n+" at its replicated end",k,t=>t<1?0:1.45f,1.45f,false);
                Run(n+" extended past the grant",k,t=>t<1?0:3.1f,3.1f,false);
                Run(n+" shortened",k,t=>t<1?0:t<1.3f?1.9f:1.3f,1.3f,true);
                Run(n+" shortened, a late joiner",k,t=>t<1?0:t<1.3f?1.9f:1.3f,1.3f,false,1.35f);
                // Cleared to the old default after an end was seen (the authority's clear): it ends on the first cleared frame.
                float clearedAt=1+30/60f;Run(n+" cleared",k,t=>t<1?0:t<clearedAt-1e-4f?2.4f:0,(float)(.9+Math.Ceiling((clearedAt-1e-4f-.9f)*60)/60),true);
                // From an old host (no shieldUntil ever): the authority's grant duration, the only use of the fallback.
                Run(n+" from an old host",k,t=>0,1+fallback,false);
            }
            // A block that granted nothing (its pilot's shieldUntil is an older, past end): no Phase Reserve shell.
            {var fx=Fx();for(float t=.9f;t<1.6f;t+=1/60f){fx.Begin(t,1,false);fx.Pilot(1,c,0,true,-1,RoundsProc.Phase,0,0,.5f);if(t>=1)fx.Procs.Event(Grant(RoundsProcKind.Phase,91,1),RoundsProc.Phase,false,false,0,0);fx.End();Check(fx.Procs.ShellCount==0,"no shell without a protection grant at "+t);frames++;}}
            // A newer shell on the same pilot takes over: the Vent's (to 3 s) ends at the block's phase at 1.5 s, which runs to 3 s.
            {
                var fx=Fx();var vent=Grant(RoundsProcKind.Shield,92,1);var block=Grant(RoundsProcKind.Phase,93,1.5f);
                for(float t=.9f;t<3.2f;t+=1/60f)
                {
                    fx.Begin(t,1,false);fx.Pilot(1,c,0,true,-1,RoundsProc.Phase|RoundsProc.Vent,0,0,t<1?0:3);if(t>=1)fx.Procs.Event(vent,RoundsProc.Vent,false,false,0,0);if(t>=1.5f)fx.Procs.Event(block,RoundsProc.Phase,false,false,0,0);fx.End();
                    bool v=false,ph=false,shatter=false;for(int i=0;i<fx.Procs.ShellCount;i++){var sh=fx.Procs.Shells[i];shatter|=sh.Shatter;if(sh.Shatter)continue;v|=sh.Kind==RoundsProcKind.Shield;ph|=sh.Kind==RoundsProcKind.Phase;}
                    Check(!shatter,"a takeover does not shatter");if(t>=1&&t<1.5f-1e-3f)Check(v&&!ph,"the Vent's shell before the block at "+t);if(t>=1.5f+1e-3f&&t<3-1e-3f)Check(ph&&!v,"the newer phase shell takes over at "+t);if(t>3+1e-3f)Check(!v&&!ph,"both over at the protection's end");
                    frames++;
                }
            }
            summary=frames+" frames: Phase Reserve, Emergency Vent and Reclamation Core shells end at the replicated shieldUntil (exact, extended, shortened with a shatter, cleared, a late joiner), the grant durations only from an old host; no shell without a grant; a newer shell takes over";
        }

        // ---- procs ----
        private static void Procs(out string summary)
        {
            int kinds=0;
            void Expect(EventKind kind,string detail,RoundsProc procs,bool pilot,RoundsProcKind want,bool executes=false)
            {
                var p=new NativeRoundsProcs();var e=new ArenaEvent{Id=1,Kind=kind,Source=1,Target=2,Time=1,Amount=kind==EventKind.Freeze?1.2f:10,Detail=detail,Radius=3};
                p.Event(e,procs,pilot,executes,0,0);Check(p.Count==1&&p.Items[0].Kind==want,want+" from "+kind+" "+detail);
                var none=new NativeRoundsProcs();none.Event(e,RoundsProc.None,pilot,executes,0,0);Check(none.Count==0,"no look without the chip: "+want);kinds++;
            }
            Expect(EventKind.Block,null,RoundsProc.Phase,false,RoundsProcKind.Phase);Expect(EventKind.Block,null,RoundsProc.BlockHeal,false,RoundsProcKind.BlockHeal);
            Expect(EventKind.Hit,"direct",RoundsProc.Packet,true,RoundsProcKind.Packet);Expect(EventKind.Hit,"melee",RoundsProc.Grapple,true,RoundsProcKind.Grapple);Expect(EventKind.Hit,"direct",RoundsProc.Tractor,true,RoundsProcKind.Tractor);
            Expect(EventKind.Hit,"direct",RoundsProc.Execute,true,RoundsProcKind.Execute,true);Expect(EventKind.Hit,"thorns",RoundsProc.Thorns,true,RoundsProcKind.Thorns);
            Expect(EventKind.Freeze,null,RoundsProc.Stasis,true,RoundsProcKind.Stasis);Expect(EventKind.Teleport,null,RoundsProc.Reclaim,false,RoundsProcKind.Reclaim);Expect(EventKind.Teleport,null,RoundsProc.Transit,false,RoundsProcKind.Transit);
            Expect(EventKind.Teleport,"swap",RoundsProc.Swap,false,RoundsProcKind.Swap);Expect(EventKind.Teleport,"swap",RoundsProc.Shuffle,false,RoundsProcKind.Swap);
            foreach(var d in new[]{"swap","shadow-step","proxy-swap"}){var x=new NativeRoundsProcs();x.Event(new ArenaEvent{Id=1,Kind=EventKind.Teleport,Source=1,Time=1,Detail=d},RoundsProc.Reclaim|RoundsProc.Transit,false,false,0,0);Check(x.Count==0,"a "+d+" teleport is not the emergency blink or the transit");}
            Expect(EventKind.Field,"panic-button",RoundsProc.Vent,false,RoundsProcKind.Shield);Expect(EventKind.BlockPulse,null,RoundsProc.GuardPulse,false,RoundsProcKind.Pulse);
            Expect(EventKind.Explosion,"static",RoundsProc.ArcFault,false,RoundsProcKind.ArcFault);Expect(EventKind.Explosion,"shrapnel",RoundsProc.Fragment,false,RoundsProcKind.Fragment);
            Expect(EventKind.ReloadEnd,null,RoundsProc.ReloadHeal,false,RoundsProcKind.ReloadHeal);
            var q=new NativeRoundsProcs();var hit=new ArenaEvent{Id=1,Kind=EventKind.Hit,Source=1,Target=2,Time=1,Amount=10,Detail="secondary"};q.Event(hit,RoundsProc.Packet,true,false,0,0);Check(q.Count==0,"an indirect hit steals nothing");
            hit.Detail="direct";q.Event(hit,RoundsProc.Packet,false,false,0,0);Check(q.Count==0,"a hit on a minion steals nothing");
            q.Event(hit,RoundsProc.Execute,true,false,0,0);Check(q.Count==0,"no finisher above 30%");
            var blocks=new NativeRoundsProcs();for(int k=0;k<5;k++)blocks.Event(new ArenaEvent{Id=10+k,Kind=EventKind.Block,Source=1,Time=1+k*.05f},RoundsProc.Phase,false,false,0,0);Check(blocks.Count==1,"one phase per block activation");
            blocks.Event(new ArenaEvent{Id=20,Kind=EventKind.Block,Source=1,Time=2},RoundsProc.Phase,false,false,0,0);Check(blocks.Count==2,"the next block's phase");
            var regen=new NativeRoundsProcs();regen.Event(new ArenaEvent{Id=30,Kind=EventKind.Heal,Source=3,Target=3,Time=5,Amount=.07f},RoundsProc.Regen,false,false,0,0);Check(regen.Regenerating(3,5.2f)&&!regen.Regenerating(3,5.4f)&&!regen.Regenerating(4,5.1f),"regenerating for .3 s after a self heal");
            // A pulse's ring lies on its floor exactly its radius once grown.
            var fx=Fx();fx.Begin(1.3f,1,false);fx.Procs.Event(new ArenaEvent{Id=40,Kind=EventKind.BlockPulse,Source=1,Time=1,Position=new V3(2,NativeRoundsFields.PilotCentre,3),Radius=3},RoundsProc.GuardPulse,false,false,0,0);fx.End();
            float max=0;for(int i=0;i<fx.Ground.VertexCount;i++){var v=fx.Ground.V[i];max=Math.Max(max,(float)Math.Sqrt((v.Px-2)*(v.Px-2)+(v.Pz-3)*(v.Pz-3)));}
            Check(Math.Abs(max-3)<1e-4f,"the pulse ring is the pulse's 3 m");
            summary=kinds+" proc kinds from their replicated events (none without the chip); one phase per block activation; regeneration window; the pulse ring is its radius";
        }

        // ---- tiers ----
        private static void Tiers(out string summary)
        {
            int Count(double q,bool low,out int ground,out int warp,out int lit){var fx=Fx();for(float t=0;t<=2.5f;t+=1/30f)Step(fx,t,q,low);ground=fx.Ground.VertexCount;warp=fx.Warp.VertexCount;lit=fx.Lit.Vertices;return fx.Particles.Count;}
            int full=Count(1,false,out int gf,out int wf,out int lf),reduced=Count(.3,false,out int gr,out _,out int lr),low=Count(1,true,out int gl,out int wl,out int ll),both=Count(.3,true,out _,out _,out _);
            Check(full>reduced&&full>low&&both<=Math.Min(reduced,low)&&both>0,"fewer particles on Reduced and Low ("+full+", "+reduced+", "+low+", "+both+")");
            Check(gl<gf&&gr<gf,"lighter surfaces off Full");Check(wf>0&&wl==0,"no distortion on the low tier");Check(ll<lf,"fewer rotor blades on the low tier");
            summary="particles Full "+full+", Reduced "+reduced+", Low "+low+", both "+both+"; ground vertices "+gf+"/"+gr+"/"+gl+"; distortion off on Low";
        }

        // ---- allocation ----
        private static void Allocation(out string summary)
        {
            var fx=Fx();for(int f=0;f<360;f++)Step(fx,f/60f);
            for(int e=0;e<NativeRoundsPreview.Count;e++)for(int f=0;f<120;f++){float t=f/60f;fx.Begin(t,1,false);NativeRoundsPreview.Fill(fx,e,t,1,Owner,0,0);fx.End();}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int f=360;f<720;f++)Step(fx,f/60f);
            for(int e=0;e<NativeRoundsPreview.Count;e++)for(int f=120;f<240;f++){float t=f/60f;fx.Begin(t,1,false);NativeRoundsPreview.Fill(fx,e,t,1,Owner,0,0);fx.End();}
            long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"no allocation per frame ("+used+" bytes)");
            summary="0 bytes over "+(360+NativeRoundsPreview.Count*120)+" frames (the busy scene and every DEV preview entry)";
        }

        // ---- sources ----
        private static void Source(string root,out string summary)
        {
            string A(string rel)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",rel));
            string[] types={"NativeRoundsFx","NativeRoundsLooks","NativeRoundsFields","NativeRoundsUnits","NativeRoundsProcs","NativeRoundsMeshes","NativeRoundsPreview","RoundsFieldVertex","VfxLitVertex","RoundsSurface","NativeRoundsSurfaceDraw"};
            // No authority, rules or network source names the presentation (they never read it).
            var authority=new List<string>{"Runtime/NativeChips.cs","Runtime/NativeSummons.cs","Runtime/NativeDeaths.cs","Runtime/NativeProjectiles.cs","Runtime/NativeAbilities.cs","Runtime/NativeArena.cs","Runtime/NativeRoundsValidation.cs","Runtime/NativePve.cs","Core/NativeRoundsCards.cs","Core/NativeRoundsPacks.cs","Core/NativeContentRules.cs","Core/NativeMods.cs","Editor/NativeNetworkBuildChecks.cs"};
            authority.AddRange(Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"NativeNetwork*.cs").Select(f=>"Runtime/"+Path.GetFileName(f)));
            authority.AddRange(Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Core"),"NativeNetwork*.cs").Select(f=>"Core/"+Path.GetFileName(f)));
            foreach(var f in authority){if(!File.Exists(Path.Combine(root,"Assets/ChipArena",f)))continue;string s=A(f);foreach(var t in types)Check(!s.Contains(t),f+" names no presentation type ("+t+")");}
            // The presentation reads no authority internals and writes nothing back.
            foreach(var f in new[]{"Core/NativeRoundsLooks.cs","Core/NativeRoundsFields.cs","Core/NativeRoundsUnits.cs","Core/NativeRoundsProcs.cs","Core/NativeRoundsFx.cs","Core/NativeRoundsMeshes.cs","Runtime/NativeRoundsFxPresentation.cs"})
            {
                string s=A(f);foreach(var bad in new[]{"Damage(","HitMinion(","ChipFields.Add","ChipFields.Remove","Minions.Add","Minions.Remove","Bullets.Add","Emit(","NetworkFrame(","SendFrame",".Hp-=",".Hp=",".FrozenUntil=","Knockback","AddForce","Physics.Sync"})Check(!s.Contains(bad),f+" stays presentation only (no "+bad+")");
            }
            // The presenter reads the replicated protection end and summon expiry; it never writes them, the death owner or the swap.
            {string s=A("Runtime/NativeRoundsFxPresentation.cs");foreach(var bad in new[]{"ShieldUntil=","ShieldUntil+=",".Until=","shieldUntil=",".owner=","Body.position=","MinionDeaths.Add","Events.Add"})Check(!s.Contains(bad),"the presenter writes no authority state (no "+bad+")");}
            // The mirrors of the authority the presentation draws to.
            string chips=A("Runtime/NativeChips.cs"),summons=A("Runtime/NativeSummons.cs"),deaths=A("Runtime/NativeDeaths.cs"),arena=A("Runtime/NativeArena.cs"),frame=A("Runtime/NativeNetworkFrame.cs");
            // The replicated state the bindings draw from (Astra's PR #288): the swap event, the summon expiry and the protection end on
            // the frames (an unknown expiry 0 on the wire, infinity on a replica), the dead summon's owner, the grants the fallback
            // mirrors, and the chip blinks that carry no detail (so a swap is never drawn as one).
            Check(chips.Contains("Emit(EventKind.Teleport,owner,target.Seat.id,b,0,\"swap\",from:a);"),"the swap event: owner, swapped pilot, new and old body centres");
            Check(frame.Contains("until=float.IsPositiveInfinity(m.Until)?0:m.Until")&&frame.Contains("m.Until=mf.until>0?mf.until:float.PositiveInfinity;"),"the minion frame's until (0 unknown; a replica's infinity)");
            Check(frame.Contains("shieldUntil=p.ShieldUntil")&&frame.Contains("p.ShieldUntil=pf.shieldUntil;"),"the pilot frame's shieldUntil");
            Check(deaths.Contains("owner=m.Owner?.Seat.id??0"),"a dead summon's owner");
            Check(chips.Contains("p.ShieldUntil=Math.Max(p.ShieldUntil,Time+.6f)")&&NativeRoundsProcs.PhaseFallback==.6f&&chips.Contains("ChipBlink(p,-Vec(V3.Forward(p.Input.Yaw))*6);p.ShieldUntil=Time+1.2f;")&&NativeRoundsProcs.ReclaimFallback==1.2f&&chips.Contains("Heal(p,(float)p.Stats[\"hp\"]*.3f);p.ShieldUntil=Time+2;")&&NativeRoundsProcs.VentFallback==2,"the old-host fallbacks are the authority's grants");
            Check(chips.Contains("Emit(EventKind.Teleport,p,0,target,0,effect,from:origin)")&&chips.Contains("private void ChipBlink(NativePilot p,Vector3 delta,string effect=null)"),"the chip blinks' teleport carries no detail");
            Check(summons.Contains("if(m.Owner.Hp<=0||Time>=m.Until||")&&summons.Contains("Until=Time+life"),"a summon leaves at its until");
            Check(chips.Contains("float a=Time*4+(side>0?0:Mathf.PI);Vector3 center=p.Body.position+Vector3.up*.55f;b.Position=center+new Vector3(Mathf.Sin(a)*.75f,.25f*Mathf.Sin(a*2),Mathf.Cos(a)*.75f);")&&NativeRoundsLooks.SatelliteSpin==4&&NativeRoundsLooks.SatelliteLift==.55f&&NativeRoundsLooks.SatelliteRadius==.75f,"the satellites' orbit is the authority's launch point");
            Check(chips.Contains("Extra(0,.4f,-1,true);Extra(0,.4f,1,true);"),"two satellites, sides -1 and 1");
            Check(chips.Contains("if(Has(owner,\"puppet-master\")&&ProcOnce(attack+\":\"+impact+\":toy\"))Summon(owner,1,\"toy\",point,6);")&&NativeRoundsLooks.DroneLife==6,"the toy lives 6 s");
            Check(summons.Contains("float windup=toy?.45f:.42f,recover=toy?.55f:.48f;")&&summons.Contains("Vector3 origin=at+Vector3.up*(toy?.25f:0)")&&NativeRoundsLooks.DroneWindup==.45f&&NativeRoundsLooks.DroneRecover==.55f&&NativeRoundsLooks.DroneMuzzle==.25f,"the toy's strike timing and muzzle");
            Check(deaths.Contains("if(m.Hp>0||m.Kind==\"slender\")return;"),"an expired toy leaves no death record (so the drone powers down instead of breaking up)");
            Check(arena.Contains("Position=Vec(p.Body.position-Vector3.up*.865f)")&&NativeRoundsFields.PilotCentre==.865f,"a pilot's body centre is .865 m over its feet");
            Check(chips.Contains("Vector3.Distance(f.Position,p.Body.position)<f.Radius")&&chips.Contains("if(p.Hp<=0||d.magnitude>f.Radius)continue;"),"fields act within their radius of the body centre");
            foreach(var rule in new[]{"(\"black-hole\",\"black-hole\",5,3.5f,10)","(\"blizzard\",\"frost\",3.5f,3,4)","(\"time-warp\",\"time\",4,4,0)","(\"repulsion-pad\",\"repulsion\",3.5f,3,0)","(\"lightning-rod\",\"storm\",4,3,18)"})Check(chips.Contains(rule),"field rule "+rule);
            Check(chips.Contains("Field(p,\"fire\",at,3,3,15,\"hot-foot\")")&&chips.Contains("Field(p,\"time\",p.Body.position,5,3,0,\"freezer-block\")")&&chips.Contains("Field(p,\"repulsion\",p.Body.position,6,4,0,\"panic-button\")"),"dodge, block and panic field rules");
            foreach(var (l,R,life) in new[]{(0,3f,3f),(2,3.5f,3f),(4,4f,4f),(6,5f,3f),(9,3.5f,3f),(10,6f,4f),(11,5f,3.5f),(12,4f,3f)}){NativeRoundsPreview.FieldRule(l,out float r,out float lf);Check(r==R&&lf==life,"the preview's "+NativeRoundsLooks.Looks[l].Name+" is the authority's "+R+" m, "+life+" s");}
            // The hooks.
            Check(A("Runtime/ArenaPresentation.cs").Contains("RoundsFx(arena,camera,fields);")&&A("Runtime/ArenaPresentation.cs").Contains("ClearRoundsFx();")&&A("Runtime/ArenaPresentation.cs").Contains("DisposeRoundsFx();"),"drawn in RenderGlow, cleared and disposed");
            Check(A("Runtime/NativeFieldPresentation.cs").Contains("if(NativeRoundsLooks.FieldLook(f.Effect,f.Owner?.Seat.chips)>=0)continue;"),"a pack field draws no vanilla dome, core or emitters");
            Check(A("Runtime/NativeSourcePresentation.cs").Contains("if(RoundsDrone(m))continue;")&&A("Runtime/NativeSummonPresentation.cs").Contains("if(summon&&toy&&RoundsDroneDeath(arena,death))return true;")&&A("Runtime/NativeSummonPresentation.cs").Contains("&&!RoundsDroneDeath(arena,d);")&&A("Runtime/NativeDeathPresentation.cs").Contains("SourceMinionDeath(arena,death,age)"),"a drone is not drawn as the toy soldier, alive or dead (an unseen one by its death record's owner)");
            {string pr=A("Runtime/NativeRoundsFxPresentation.cs");Check(pr.Contains("p.FrozenUntil,p.ShieldUntil);")&&pr.Contains("Until=float.IsInfinity(m.Until)?0:m.Until")&&pr.Contains("var owner=RoundsDroneOwner(arena,d.owner);"),"the presenter binds shieldUntil, the summon expiry and the death owner");}
            Check(A("Runtime/NativeIcons.cs").Contains("return chips.TryGetValue(id??\"\",out var i)?i:NativeChipIcons.Native(id)??\"cpu\";")&&A("Core/NativeChipIcons.cs").Contains("return NativeRoundsLooks.Icon(id);"),"card icons: the source map, the native icons (now with the packs), then cpu");
            Check(NativeDevActions.Find("preview-rounds-fx")?.Kind==NativeDevKind.Presentation&&A("Runtime/NativeDevPanel.cs").Contains("case \"preview-rounds-fx\":RoundsPreviewToggle();break;")&&A("Runtime/NativeDevPanel.cs").Contains("y=RoundsPreviewPanel(x,y,w);"),"the DEV preview tool");
            Check(A("Runtime/NativeVfxDraw.cs").Contains("public void LitPieces(VfxLitVertex[] v,int count,int[] indices,int indexCount)"),"the shared renderer takes Core lit pieces");
            foreach(var sh in new[]{"NativeRoundsField","NativeRoundsWarp"})
            {
                string s=A("Resources/"+sh+".shader");Check(s.Contains("// SHARED BEGIN")&&s.Contains("// SHARED END")&&File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/"+sh+".shader.meta")),sh+" keeps its harness section and its meta");
                Check(A("Editor/NativeBuildChecks.cs").Contains("\""+sh+"\""),"the editor build check compiles "+sh);
            }
            Check(A("Resources/NativeRoundsField.shader").Contains("float RfRim(float d,float width,float alive){return alive*(1.0-smoothstep(0.0,width,d));}")&&A("Resources/NativeRoundsField.shader").Contains("float alive=step(age,life)*step(0.0,age);"),"the boundary line is drawn only while the field lives");
            Check(A("Resources/NativeRoundsWarp.shader").Contains("GrabPass { \"_RoundsWarpGrab\" }")&&A("Runtime/NativeRoundsFxPresentation.cs").Contains("if(!LowGraphics&&fx.Warp.VertexCount>0)"),"one named grab, none on the low tier");
            foreach(var f in new[]{"Core/NativeRoundsLooks.cs","Core/NativeRoundsMeshes.cs","Core/NativeRoundsFields.cs","Core/NativeRoundsUnits.cs","Core/NativeRoundsProcs.cs","Core/NativeRoundsFx.cs","Runtime/NativeRoundsFxPresentation.cs"})Check(File.Exists(Path.Combine(root,"Assets/ChipArena",f+".meta")),f+" has its meta");
            summary="no authority, rules or network source names the presentation; the presentation reads no authority internals and writes no replicated state; "+authority.Count+" files scanned; the orbit, toy, field, pilot, swap, expiry, protection and death-owner mirrors match the authority; hooks, shaders and metas in place";
        }
    }
}
#endif
