#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The guns' muzzle flashes and their light (owner 2026-10-20: "Improve muzzle flash effects from our weapons~ I still want
    // to make use of lighting"; Core/NativeMuzzleFlash.cs, Core/NativeMuzzleLight.cs, Resources/NativeMuzzleLight.cginc):
    //  - anchors: every gun's flash leaves from its drawn crown in both skins (the Standard models' faces, the Prototype Series'
    //    block faces decoded from the bank) at every detail tier, the device vents' jets from the models' own slots, pointing
    //    out; chip modules shut the vents they cover; the hider's star on its slots;
    //  - variation: no two shots alike, each gun its own size and life, the kits larger and hotter;
    //  - the light's envelope: the attack, the 40-70 ms exponential decay, the exact end, the per-round strength;
    //  - the cap under machine-gun fire: a slot per shooter at most, the nearest within the budget, one merged flickering light
    //    per shooter (never a sum), held between rounds, gone after the burst;
    //  - determinism: host, guest (reordered, extra events, a JSON round trip), late joiner and killcam frames light the same
    //    rounds the same way;
    //  - reach: the shader's maths (NativeMuzzleLight.Shade, the cginc's own lines) on the Central Station's darkest walkable
    //    spot and an old PvP map: the floor, a wall, a zombie's skin and suit and the first-person hand, before and after;
    //  - the tiers and settings, zero allocation per frame, and the shader and runtime contract (text scans).
    // No Unity render, GPU or Windows run happens here.
    internal static class MuzzleFlashChecks
    {
        private static int count;private static readonly List<string> facts=new List<string>();
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Muzzle flash: "+what);count++;}
        private static readonly string[] Guns={"pistol","rifle","machinegun"};
        private static string F(double v,string f="F2")=>v.ToString(f,CultureInfo.InvariantCulture);
        public static void Run(string root)
        {
            count=0;facts.Clear();
            Anchors(root);Variation();Envelope();Cap();Determinism();Reach(root);Tiers();Allocation();Contract(root);
            Console.WriteLine("PASS muzzle flash ("+count+" checks: "+string.Join("; ",facts)+"; managed maths and text scans, no Unity render)");
        }

        // ---------------- anchors ----------------
        private static string Bank(string root)
        {
            using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceModels.json")));
            using var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString()));using var zip=new GZipStream(bytes,CompressionMode.Decompress);using var reader=new StreamReader(zip);return reader.ReadToEnd();
        }
        // The Prototype Series' held gun: the bank's 'weapon-' boxes on the right hand (Unity frame: x mirrored back to the source frame).
        private static List<Vector3> Prototype(string text,string spec)
        {
            string id="weapon-"+spec;int at=text.IndexOf("\"id\":\""+id+"\"",StringComparison.Ordinal);Check(at>=0,"the bank has "+id);
            int start=text.LastIndexOf('{',at),depth=0,end=start;for(int i=start;i<text.Length;i++){if(text[i]=='{')depth++;else if(text[i]=='}'&&--depth==0){end=i;break;}}
            using var doc=JsonDocument.Parse(text.Substring(start,end-start+1));var pts=new List<Vector3>();
            foreach(var sec in doc.RootElement.GetProperty("sections").EnumerateArray())
            {
                if(sec.GetProperty("bone").GetInt32()!=NativeWeaponModels.RightHand)continue;var f=sec.GetProperty("positions").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray();
                for(int i=0;i+2<f.Length;i+=3)pts.Add(new Vector3(-f[i],f[i+1],f[i+2]));
            }
            return pts;
        }
        // The distance from p to the nearest triangle of the model's sections on `bone` (the slots are decal quads: their
        // centres lie on the quads, not at a vertex).
        private static float Surface(WeaponMesh m,int bone,Vector3 p)
        {
            float best=float.MaxValue;
            foreach(var s in m.Sections){if(s.Bone!=bone)continue;for(int t=0;t+2<s.Indices.Count;t+=3)best=Math.Min(best,Triangle(p,s.Positions[s.Indices[t]],s.Positions[s.Indices[t+1]],s.Positions[s.Indices[t+2]]));}
            return best;
        }
        private static float Triangle(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            // Ericson, Real-Time Collision Detection 5.1.5: the closest point on triangle abc.
            Vector3 ab=b-a,ac=c-a,ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return Vector3.Distance(p,a);
            Vector3 bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return Vector3.Distance(p,b);
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return Vector3.Distance(p,a+ab*(d1/(d1-d3)));
            Vector3 cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return Vector3.Distance(p,c);
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return Vector3.Distance(p,a+ac*(d2/(d2-d6)));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return Vector3.Distance(p,b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6))));
            float den=1/(va+vb+vc);return Vector3.Distance(p,a+ab*(vb*den)+ac*(vc*den));
        }
        private static void Anchors(string root)
        {
            string bank=Bank(root);float worstPort=0;var old=new List<string>();
            foreach(var gun in Guns)
            {
                var d=NativeWeaponModels.Get(gun);float std=NativeMuzzleFx.Crown(gun,true),proto=NativeMuzzleFx.Crown(gun,false);
                Check(Math.Abs(std-d.Muzzle.Z-NativeMuzzleFx.CrownLead)<1e-6f,gun+" Standard crown just ahead of the model's muzzle face ("+std+" / "+d.Muzzle.Z+")");
                // Every detail tier draws the face at the crown and nothing ahead of it round the bore.
                for(int tier=0;tier<NativeWeaponModels.Tiers;tier++)
                {
                    var m=NativeWeaponModels.Build(gun,tier);var pts=m.Sections.Where(s=>s.Bone==d.Bone).SelectMany(s=>s.Positions).ToArray();
                    float face=pts.Where(p=>Math.Abs(p.X)<.012f&&Math.Abs(p.Y)<.012f).Max(p=>p.Z);
                    Check(std-face>=0&&std-face<=.006f,gun+" tier "+tier+": the flash leaves from the drawn crown (face "+face+", crown "+std+")");
                    Check(!pts.Any(p=>p.Z>std+.002f&&new Vector2(p.X,p.Y).Length()<.08f),gun+" tier "+tier+": nothing of the model ahead of the crown");
                    Check(NativeMuzzleFx.Look(gun,NativeWeaponSkins.StandardIndex,default,null,MuzzleKit.None,tier,0,out var look)&&look.Crown==std,gun+" tier "+tier+" Standard look");
                    var ports=NativeMuzzleFx.Ports(look.Device);Check(ports.Length>0,gun+" Standard device has vents");
                    for(int k=0;k<ports.Length;k++)
                    {
                        bool open=(look.PortMask&1<<k)!=0;Check(open==(tier<=ports[k].Tier),gun+" vent "+k+" open exactly where tier "+tier+" cuts it");
                        if(!open)continue;float near=Surface(m,d.Bone,ports[k].At);worstPort=Math.Max(worstPort,near);
                        Check(near<.003f,gun+" tier "+tier+" vent "+k+" opens on the model's surface ("+F(near*1000,"F1")+" mm)");
                        var radial=Vector2.Normalize(new Vector2(ports[k].At.X,ports[k].At.Y));var dir=Vector2.Normalize(new Vector2(ports[k].Dir.X,ports[k].Dir.Y));
                        Check(Vector2.Dot(radial,dir)>.99f&&ports[k].Dir.Z>0&&ports[k].At.Z<std,gun+" vent "+k+" jets outward and a little forward, behind the crown");
                    }
                }
                // The Prototype Series: its block face (decoded from the bank), a plain device.
                var pp=Prototype(bank,gun);float pface=pp.Where(p=>Math.Abs(p.X)<.06f&&Math.Abs(p.Y)<.06f).Max(p=>p.Z);// the front box's corners (boxes have no centre vertex)
                Check(Math.Abs(proto-NativeMuzzleFx.CrownLead-pface)<.001f,gun+" Prototype crown just ahead of its block face ("+proto+" / "+pface+")");
                Check(!pp.Any(p=>p.Z>proto+.002f&&new Vector2(p.X,p.Y).Length()<.08f),gun+" Prototype: nothing ahead of the crown");
                Check(NativeMuzzleFx.Look(gun,NativeWeaponSkins.PrototypeIndex,new ChipKinds{Extra=2,Vacuum=true},NativeChipModules.Sockets(gun),MuzzleKit.None,0,0,out var pl)&&pl.Device==MuzzleDevice.Plain&&pl.PortMask==0&&pl.Pods==0&&pl.Petals==4&&pl.FixedRoll,gun+" Prototype: a plain face with a four-petal cross, no vents, no pods (its modules are the old boxes)");
                old.Add(gun+" "+F((NativeWeaponDressing.MuzzleZ(gun)+.02f-d.Muzzle.Z)*100,"F1")+"/"+F((NativeWeaponDressing.MuzzleZ(gun)+.02f-pface)*100,"F1")+" cm");
            }
            facts.Add("crowns on the drawn faces of 3 guns x 2 skins x 3 tiers, vents within "+F(worstPort*1000,"F1")+" mm of their slots' surfaces (the old flash sat ahead of the face by "+string.Join(", ",old)+", Standard/Prototype)");
            // The hider's star rides its four slots.
            NativeMuzzleFx.Look("machinegun",NativeWeaponSkins.StandardIndex,default,null,MuzzleKit.None,0,0,out var mg);var slots=NativeMuzzleFx.Ports(MuzzleDevice.Hider);
            Check(mg.Petals==4&&mg.FixedRoll&&slots.Length==4,"the machine gun's four petals sit on its hider's four slots");
            for(int s=0;s<60;s++)for(int k=0;k<4;k++)
            {
                long seed=NativeWeaponFx.Seed(3+s*.085,7);Check(NativeMuzzleFx.Petal(mg,seed,k,0,out float roll,out _,out _,out _),"hider petal");
                double slot=Math.Atan2(slots[k].At.Y,slots[k].At.X),diff=Math.IEEERemainder(roll-slot,Math.PI*2);Check(Math.Abs(diff)<=mg.RollJitter*.5f+1e-4,"hider petal "+k+" on its slot ("+F(diff,"F3")+" rad)");
            }
            // Chip modules shut the vents they cover (NativeChipModules sockets: collars, the intake bell, a vane's foot).
            int Open(string spec,ChipKinds k)=>NativeMuzzleFx.OpenPorts(NativeChipModules.Sockets(spec),NativeMuzzleFx.Device(spec,true),k,0);
            Check(Open("pistol",default)==3&&Open("pistol",new ChipKinds{Ricochet=1})==1&&Open("pistol",new ChipKinds{Ricochet=2})==0&&Open("pistol",new ChipKinds{Vacuum=true})==1&&Open("pistol",new ChipKinds{Pong=true,Boomerang=true})==3,
                "pistol: a ricochet collar shuts the front top port, two shut both, the vacuum bell the front one; the pong gimbal and the vanes leave them");
            Check(Open("rifle",default)==127&&Open("rifle",new ChipKinds{Vacuum=true})==(127&~(16|32))&&Open("rifle",new ChipKinds{Boomerang=true})==(127&~(16|32))&&Open("rifle",new ChipKinds{Ricochet=3})==127,
                "rifle: the vacuum bell and the vanes' feet shut the brake's front side ports; the collars sit behind the brake");
            Check(Open("machinegun",default)==15&&Open("machinegun",new ChipKinds{Vacuum=true})==0&&Open("machinegun",new ChipKinds{Ricochet=3,Boomerang=true})==15,"machine gun: the vacuum bell shuts the hider's slots; collars and vanes leave them");
            // Multishot pods flash at their own mouths, behind the crown, in mirrored pairs.
            foreach(var gun in Guns)
            {
                var s=NativeChipModules.Sockets(gun);int n=NativeMuzzleFx.PodCount(s);Check(n==s.Pods.Length*2&&n>0,gun+" pods");
                Check(NativeMuzzleFx.Look(gun,NativeWeaponSkins.StandardIndex,new ChipKinds{Extra=9},s,MuzzleKit.None,0,0,out var lp)&&lp.Pods==n&&lp.Sockets==s,gun+" the flash takes every pod");
                for(int i=0;i<n;i++){Check(NativeMuzzleFx.PodMouth(s,i,out var a)&&a.Z<NativeMuzzleFx.Crown(gun,true)&&Math.Abs(a.Z-(s.PodZ+s.PodLength*.5f))<1e-6f,gun+" pod "+i+" mouth");if(i%2==1){NativeMuzzleFx.PodMouth(s,i-1,out var b);Check(a.X==-b.X&&a.Y==b.Y,gun+" pods mirrored");}}
            }
        }

        // ---------------- variation ----------------
        private static void Variation()
        {
            var looks=new Dictionary<string,MuzzleLook>();
            foreach(var gun in Guns)
            {
                Check(NativeMuzzleFx.Look(gun,NativeWeaponSkins.StandardIndex,default,null,MuzzleKit.None,0,0,out var l),gun+" look");looks[gun]=l;
                Check(l.Life>=.04f&&l.Life<=.09f,gun+" flash lasts 40-90 ms ("+l.Life+")");
                var seen=new HashSet<(float,float,float,float,float)>();
                for(int i=0;i<300;i++)
                {
                    long seed=NativeWeaponFx.Seed(5+i*.0853,4);NativeMuzzleFx.Petal(l,seed,0,0,out float r0,out float l0,out _,out _);NativeMuzzleFx.Plume(l,seed,0,out float pl,out float pw,out _);NativeMuzzleFx.Core(l,seed,0,out float cs,out _);
                    seen.Add((r0,l0,pl,pw,cs));
                    Check(NativeMuzzleFx.Plume(l,seed,0,out float again,out _,out _)&&again==pl,gun+" a pure function of the shot");
                }
                Check(seen.Count==300,gun+": no two of 300 shots alike ("+seen.Count+")");
                // Petals at the shot, full to two thirds, then shrinking to .35 and fading out by the end of the flash.
                long s0=NativeWeaponFx.Seed(12.5,3);
                for(int k=0;k<l.Petals;k++)
                {
                    Check(!NativeMuzzleFx.Petal(l,s0,k,-.001,out _,out _,out _,out _)&&!NativeMuzzleFx.Petal(l,s0,k,l.Life,out _,out _,out _,out _),gun+" petal only inside the flash");
                    Check(NativeMuzzleFx.Petal(l,s0,k,0,out _,out float a,out float w,out float al)&&al==1&&a>=l.Star*.75f-1e-5f&&a<=l.Star*1.25f+1e-5f&&Math.Abs(w-a*l.StarWidth)<1e-5f,gun+" petal at the shot");
                    Check(NativeMuzzleFx.Petal(l,s0,k,l.Life*.6,out _,out _,out _,out float am)&&am==1&&NativeMuzzleFx.Petal(l,s0,k,l.Life*.999,out _,out float b,out _,out float az)&&az<.01f&&b<a*.36f,gun+" petals keep full opacity to two thirds, then shrink and fade");
                }
                Check(NativeMuzzleFx.Core(l,s0,0,out float core,out var hot)&&hot.Max>3&&core<.25f&&!NativeMuzzleFx.Core(l,s0,l.Life*.6,out _,out _),gun+" the core is white-hot, small and over in .6 of the flash");
                Check(NativeMuzzleFx.Fireball(l,s0,0,out float fb,out var fire)&&fb>core&&fire.R>fire.G&&fire.G>fire.B&&!NativeMuzzleFx.Fireball(l,s0,l.Life*1.4,out _,out _),gun+" the fireball is larger, orange and gone after 1.4 flashes");
                Check(NativeMuzzleFx.Plume(l,s0,0,out float plume,out float plumeW,out _)&&plume>l.Star&&plumeW<plume,gun+" the plume runs along the barrel, longer than a petal");
            }
            var p=looks["pistol"];var r=looks["rifle"];var m=looks["machinegun"];
            Check(p.Plume<m.Plume&&m.Plume<r.Plume&&p.Fireball<m.Fireball&&m.Fireball<r.Fireball&&r.Life>p.Life&&p.Life>m.Life,"the pistol short and punchy, the machine gun fast, the rifle biggest and longest");
            Check(m.Flicker>p.Flicker&&m.Flicker>r.Flicker,"the machine gun's per-shot spread is widest, so its strobe reads as flicker");
            NativeMuzzleFx.Look("pistol",NativeWeaponSkins.StandardIndex,default,null,MuzzleKit.Fan,0,0,out var fan);NativeMuzzleFx.Look("machinegun",NativeWeaponSkins.StandardIndex,default,null,MuzzleKit.Overdrive,0,0,out var cold);NativeMuzzleFx.Look("machinegun",NativeWeaponSkins.StandardIndex,default,null,MuzzleKit.Overdrive,0,1,out var hotDrive);
            Check(fan.Plume>p.Plume&&fan.Gain>p.Gain&&cold.Plume>m.Plume&&hotDrive.Plume>cold.Plume&&hotDrive.Gain>cold.Gain,"Fan the Hammer flashes larger and hotter; Overdrive grows and whitens as the barrels heat");
            Check(NativeMuzzleFx.Fireball(hotDrive,1,0,out _,out var white)&&NativeMuzzleFx.Fireball(cold,1,0,out _,out var orange)&&white.B/white.R>orange.B/orange.R,"Overdrive's heat whitens the fireball");
            Check(!NativeMuzzleFx.Look("sword",out _)&&!NativeMuzzleFx.Look("fire",out _)&&!NativeMuzzleFx.Look("bow",out _)&&NativeMuzzleFx.Look("machinegun",out var turret)&&turret.Device==MuzzleDevice.Plain,"melee, staffs and the bow have no muzzle flash; the plain look (the hazard kit's turrets) has no vents");
            Check(NativeMuzzleFx.Span>=NativeMuzzleFx.WispLife+.03&&NativeMuzzleFx.Wisp(5,0,.1,out _,out float rise,out float size,out float cover)&&rise>0&&size>.07f&&cover>0&&cover<=.2f&&!NativeMuzzleFx.Wisp(5,0,NativeMuzzleFx.WispLife,out _,out _,out _,out _),"a light wisp rises and is gone after its life; the span covers it");
            facts.Add("flash lives pistol "+F(p.Life*1000,"F0")+" / rifle "+F(r.Life*1000,"F0")+" / machine gun "+F(m.Life*1000,"F0")+" ms, plumes "+F(p.Plume)+" / "+F(r.Plume)+" / "+F(m.Plume)+" m, 300 distinct shots each");
        }

        // ---------------- the light's envelope ----------------
        private static void Envelope()
        {
            var parts=new List<string>();
            foreach(var (gun,kit) in new[]{("pistol",MuzzleKit.None),("pistol",MuzzleKit.Fan),("rifle",MuzzleKit.None),("machinegun",MuzzleKit.None),("machinegun",MuzzleKit.Overdrive)})
            {
                Check(NativeMuzzleLight.Look(gun,kit,out var l),gun+" light");string n=gun+(kit!=MuzzleKit.None?" "+kit:"");
                Check(NativeMuzzleLight.Pulse(l,0)==0&&NativeMuzzleLight.Pulse(l,-.01)==0&&Math.Abs(NativeMuzzleLight.Pulse(l,l.Attack)-1)<1e-6&&l.Attack<=.005f,n+": a fast attack to the peak in "+F(l.Attack*1000,"F0")+" ms");
                Check(l.Decay>=.04f&&l.Decay<=.07f&&Math.Abs(NativeMuzzleLight.Pulse(l,l.Attack+l.Decay)-Math.Exp(-1))<1e-4&&Math.Abs(NativeMuzzleLight.Pulse(l,l.Attack+Math.Log(2)*l.Decay)-.5)<1e-4,n+": exponential decay, time constant "+F(l.Decay*1000,"F0")+" ms");
                double end=l.Attack+NativeMuzzleLight.Tail*l.Decay,taper=l.Attack+(NativeMuzzleLight.Tail-1)*l.Decay;
                Check(NativeMuzzleLight.Pulse(l,end)==0&&NativeMuzzleLight.Pulse(l,end+1)==0&&Math.Abs(NativeMuzzleLight.Pulse(l,taper-1e-6)-NativeMuzzleLight.Pulse(l,taper+1e-6))<1e-4,n+": gone exactly at "+F(end*1000,"F0")+" ms, no step");
                float prev=2;bool mono=true;for(double a=l.Attack;a<end;a+=.0005){float v=NativeMuzzleLight.Pulse(l,a);if(v>prev+1e-6f)mono=false;prev=v;}Check(mono,n+": never rises again after the peak");
                var shots=new float[NativeMuzzleLight.History];shots[0]=10;
                float peak=NativeMuzzleLight.Level(l,shots,1,10+l.Attack,3,NativeKitFlash.Full);Check(peak>=.88f&&peak<=1.12f&&NativeMuzzleLight.Level(l,shots,1,9.99,3,NativeKitFlash.Full)==0&&NativeMuzzleLight.Level(l,shots,1,10+end,3,NativeKitFlash.Full)==0,n+": one round lights at its own seeded strength ("+F(peak)+"), nothing before it or after its tail");
                var c=NativeMuzzleLight.Rgb(l,1,false);Check(c.X>=c.Y&&c.Y>=c.Z&&c.X>=1.5f&&l.Range>=5&&l.Range<=8,n+": a warm light, "+F(c.X)+"/"+F(c.Y)+"/"+F(c.Z)+" linear at the peak, "+F(l.Range,"F1")+" m");
                parts.Add(n+" "+F(l.Decay*1000,"F0")+" ms x"+F(l.Peak,"F1")+" "+F(l.Range,"F1")+" m");
            }
            Check(!NativeMuzzleLight.Look("bow",MuzzleKit.None,out _)&&!NativeMuzzleLight.Look("fire",MuzzleKit.None,out _),"only the guns' rounds light");
            facts.Add("light decays "+string.Join(", ",parts));
        }

        // ---------------- machine-gun fire: the cap and the merge ----------------
        private static ArenaEvent Shot(int id,float t,WeaponId w)=>new ArenaEvent{Id=0,Kind=EventKind.Shot,Source=id,Time=t,Amount=(int)w};
        private static void Cap()
        {
            // Eight machine gunners 2-16 m from the eye fire from t = 0 to 2 s at the gun's 85 ms interval, each its own phase.
            NativeMuzzleLight.Look("machinegun",MuzzleKit.None,out var mg);var events=new List<ArenaEvent>();var last=new float[9];
            for(int id=1;id<=8;id++){last[id]=-100;for(float t=id*.011f;t<=2;t+=.085f)events.Add(Shot(id,t,WeaponId.MachineGun));}
            events.Sort((a,b)=>a.Time.CompareTo(b.Time));
            var picker=new NativeMuzzleLightPicker();var buf=new float[NativeMuzzleLight.History];var eye=Vector3.Zero;
            float maxLevel=0,minSteady=9,maxSteady=0,maxSum=0,afterEnd=0;int frames=0;
            foreach(int budget in new[]{4,2,1,0})
            {
                for(int f=0;f<180;f++)
                {
                    double t=f/60.0;picker.Begin();var visible=events.Where(e=>e.Time<=t&&e.Time>t-1).ToList();
                    for(int id=1;id<=8;id++)
                    {
                        float lastShot=visible.Where(e=>e.Source==id).Select(e=>e.Time).DefaultIfEmpty(-100).Max();int n=NativeMuzzleLight.Shots(visible,id,lastShot,t,buf);float level=NativeMuzzleLight.Level(mg,buf,n,t,id,NativeKitFlash.Full);
                        if(budget==4){maxLevel=Math.Max(maxLevel,level);float sum=0;for(int i=0;i<n;i++)sum+=NativeMuzzleLight.Pulse(mg,t-buf[i]);maxSum=Math.Max(maxSum,sum+mg.Sustain);if(t>.4&&t<2){minSteady=Math.Min(minSteady,level);maxSteady=Math.Max(maxSteady,level);}if(t>2+NativeMuzzleLight.Window)afterEnd=Math.Max(afterEnd,level);}
                        picker.Offer(id,new Vector3(0,1.4f,2*id),mg,level,false);
                    }
                    picker.Resolve(eye,budget,1);frames++;
                    Check(picker.Count<=budget&&picker.Count<=NativeMuzzleLight.Slots,"at most "+budget+" lights ("+picker.Count+")");
                    if(t<=2)for(int k=0;k<picker.Count;k++)Check(picker.Ids[k]==k+1,"the nearest shooters take the slots, in order");
                    if(t>.2&&t<2)Check(picker.Count==Math.Min(budget,8),"every slot the budget gives is used while eight guns fire");
                    for(int k=picker.Count;k<NativeMuzzleLight.Slots;k++)Check(picker.Pos[k*4+1]<-5000&&picker.Rgb[k*4]==0,"unused slots sit far below, dark");
                }
                // Past the burst and the window, nothing is lit.
                picker.Begin();picker.Resolve(eye,budget,1);Check(picker.Count==0,"no offer, no light");
            }
            Check(maxLevel<=1.12f+1e-5f&&maxSum>1.6f,"a shooter's light never stacks: at most "+F(maxLevel)+" of the peak where a sum of its rounds would reach "+F(maxSum));
            Check(minSteady>=mg.Sustain*.8f-1e-4f&&maxSteady-minSteady>=.3f,"sustained fire merges into one flickering light ("+F(minSteady)+"-"+F(maxSteady)+" of the peak), never a strobe to dark");
            Check(afterEnd==0,"the light is gone half a second after the last round");
            // Fan the Hammer's four rounds .075 s apart and Overdrive's .053 s stream merge the same way.
            NativeMuzzleLight.Look("pistol",MuzzleKit.Fan,out var fan);var fanEvents=new List<ArenaEvent>();for(int i=0;i<4;i++)fanEvents.Add(Shot(1,5+i*NativeRangedRules.FanInterval,WeaponId.Pistol));
            float fanMin=9;for(double t=5+NativeRangedRules.FanInterval*1.5;t<5+NativeRangedRules.FanInterval*3;t+=.002){int n=NativeMuzzleLight.Shots(fanEvents.Where(e=>e.Time<=t).ToList(),1,-100,t,buf);fanMin=Math.Min(fanMin,NativeMuzzleLight.Level(fan,buf,n,t,1,NativeKitFlash.Full));}
            Check(fanMin>=.2f,"Fan the Hammer's volley holds its light between rounds ("+F(fanMin)+")");
            NativeMuzzleLight.Look("machinegun",MuzzleKit.Overdrive,out var od);float odInterval=NativeRangedRules.Interval(.085,1,true);var odEvents=new List<ArenaEvent>();for(float t=0;t<2;t+=odInterval)odEvents.Add(Shot(1,t,WeaponId.MachineGun));
            float odMin=9;for(double t=.5;t<1.9;t+=.003){int n=NativeMuzzleLight.Shots(odEvents.Where(e=>e.Time<=t&&e.Time>t-1).ToList(),1,-100,t,buf);odMin=Math.Min(odMin,NativeMuzzleLight.Level(od,buf,n,t,1,NativeKitFlash.Full));}
            Check(odMin>=od.Sustain*.8f-1e-4f,"Overdrive's "+F(odInterval*1000,"F0")+" ms stream holds "+F(odMin)+" of the peak");
            // The rifle and the pistol's normal fire are single pulses: no held light between rounds.
            NativeMuzzleLight.Look("pistol",MuzzleKit.None,out var pistol);var pe=new List<ArenaEvent>{Shot(1,1,WeaponId.Pistol),Shot(1,1.42f,WeaponId.Pistol)};int pn=NativeMuzzleLight.Shots(pe,1,-100,1.4,buf);
            Check(NativeMuzzleLight.Level(pistol,buf,pn,1.4,1,NativeKitFlash.Full)<.01f,"a pistol's normal rounds (.42 s apart) are separate pulses");
            facts.Add("8 machine guns over "+frames+" frames: at most the budget's 4/2/1/0 slots, the nearest; one light each at "+F(minSteady)+"-"+F(maxSteady)+" of the peak (a sum would reach "+F(maxSum)+"); Fan holds "+F(fanMin)+", Overdrive "+F(odMin));
        }

        // ---------------- determinism ----------------
        private static void Determinism()
        {
            // The host's rounds: a pistol (id 1, .42 s), a machine gun (2, .085 s, a burst), a rifle (3, 1.15 s) and Fan the Hammer, among hits and impacts.
            var all=new List<ArenaEvent>();int eid=1;
            void Add(ArenaEvent e){e.Id=eid++;all.Add(e);}
            for(float t=.3f;t<4;t+=.42f)Add(Shot(1,t,WeaponId.Pistol));for(float t=.5f;t<2.2f;t+=.085f){Add(Shot(2,t,WeaponId.MachineGun));Add(new ArenaEvent{Kind=EventKind.Hit,Source=2,Target=5,Time=t+.01f,Amount=8});}
            for(float t=.2f;t<4;t+=1.15f){Add(Shot(3,t,WeaponId.Rifle));Add(new ArenaEvent{Kind=EventKind.Impact,Source=3,Time=t+.02f});}for(int i=0;i<4;i++)Add(Shot(1,3.1f+i*.075f,WeaponId.Pistol));
            Add(new ArenaEvent{Kind=EventKind.Shot,Source=4,Time=1,Amount=0});// a staff's cast: Weapon 0, no muzzle light
            all.Sort((a,b)=>a.Time.CompareTo(b.Time));
            var looks=new[]{("pistol",1),("machinegun",2),("rifle",3)};var buf=new float[NativeMuzzleLight.History];var rnd=new Random(7);var o=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
            int samples=0,clipped=0;double worst=0;
            float LevelOf(List<ArenaEvent> ev,int id,string spec,double t,float lastShot){NativeMuzzleLight.Look(spec,MuzzleKit.None,out var l);int n=NativeMuzzleLight.Shots(ev,id,lastShot,t,buf);return NativeMuzzleLight.Level(l,buf,n,t,id,NativeKitFlash.Full);}
            float Last(List<ArenaEvent> ev,int id,double upTo)=>ev.Where(e=>e.Kind==EventKind.Shot&&e.Source==id&&e.Time<=upTo).Select(e=>e.Time).DefaultIfEmpty(-100).Max();
            for(int i=0;i<480;i++)
            {
                double t=.25+i*(3.6/480)+rnd.NextDouble()*.004;
                // Host: its buffer at its own clock (a second of events), LastShot from its pilots.
                var host=all.Where(e=>e.Time<=t&&t-e.Time<=1).ToList();
                // Guest: the frame after the render time (20 Hz, 100 ms behind), through JSON, reordered, its own buffer's window.
                double frame=Math.Ceiling(t/.05)*.05+.1;var sent=all.Where(e=>e.Time<=frame&&frame-e.Time<=1).ToList();
                var guest=JsonSerializer.Deserialize<List<ArenaEvent>>(JsonSerializer.Serialize(sent,o),o).OrderBy(_=>rnd.Next()).ToList();
                // Late joiner: its first frame at 2.9 s carries only the last second; a killcam: the recorded frame at or before t.
                var late=all.Where(e=>e.Time<=frame&&e.Time>=1.9).ToList();double rec=Math.Floor(t/.05)*.05;var cam=all.Where(e=>e.Time<=rec&&rec-e.Time<=1).ToList();
                foreach(var (spec,id) in looks)
                {
                    float h=LevelOf(host,id,spec,t,Last(host,id,t)),g=LevelOf(guest,id,spec,t,Last(guest,id,frame));samples++;
                    Check(h==g,spec+" host and guest light the same at "+F(t,"F3")+" ("+h+" / "+g+")");
                    if(t>=2.9)Check(LevelOf(late,id,spec,t,Last(late,id,frame))==h,spec+" a late joiner lights the same");
                    // The killcam knows the rounds its recorded frame holds: the same light for those rounds; a round fired after the
                    // frame shows from the next frame on (as its flash does), never differently.
                    float k=LevelOf(cam,id,spec,t,Last(cam,id,rec)),known=LevelOf(host.Where(e=>e.Time<=rec).ToList(),id,spec,t,Last(host,id,rec));
                    Check(k==known,spec+" the killcam lights the rounds its frame holds the same at "+F(t,"F3"));if(k!=h){clipped++;worst=Math.Max(worst,t-rec);}
                }
            }
            Check(NativeWeaponFx.Seed(12.3456789,2)==NativeWeaponFx.Seed((float)12.3456789,2),"seeds survive a float round trip");
            facts.Add("host = guest (JSON, reordered) = late joiner over "+samples+" samples; the killcam the same for its frame's rounds ("+clipped+" samples wait up to "+F(worst*1000,"F0")+" ms for the next recorded frame, as the flash does)");
        }

        // ---------------- reach: the shader's maths on the station and an old map ----------------
        private static double Lum(Vector3 c)=>.2126*c.X+.7152*c.Y+.0722*c.Z;
        private static double Lin(double c)=>NativeEntranceLight.ToLinear(c);
        private static Vector3 Linear(MapLookColor c)=>new Vector3((float)Lin(c.r),(float)Lin(c.g),(float)Lin(c.b));
        private static Vector3 Hex(int c)=>new Vector3((float)Lin(((c>>16)&255)/255.0),(float)Lin(((c>>8)&255)/255.0),(float)Lin((c&255)/255.0));
        private static Vector3 Applied(MapLookColor c,float i)=>new Vector3((float)NativeEntranceLight.Applied(c.r,i,true,false),(float)NativeEntranceLight.Applied(c.g,i,true,false),(float)NativeEntranceLight.Applied(c.b,i,true,false));
        private static double S8(double lum)=>Math.Round(255*NativeEntranceLight.ToGamma(Math.Clamp(lum,0,1)));
        private static (Vector3 skin,Vector3 suit) Paint()
        {
            var bodies=new[]{11,222,3333}.Select(id=>{var v=NativeHordeModels.Variation(id);return new Vector3(v.X,v.Y,v.Z);}).ToArray();Vector3 skin=Vector3.Zero,suit=Vector3.Zero;double sa=0,ca=0;
            var m=NativeHordeModels.Build(0,0,NativeHordeModels.Full);
            foreach(var s in m.Mesh.Sections)for(int t=0;t+2<s.Indices.Count;t+=3)
            {
                int a=s.Indices[t],b=s.Indices[t+1],c=s.Indices[t+2];var e=s.Emission[a];if(e.X+e.Y+e.Z>.01f||s.Data.Count!=s.Positions.Count)continue;int cls=(int)MathF.Floor(s.Data[a].X);
                if(cls!=NativeHordeModels.SurfSkin&&cls!=NativeHordeModels.SurfCloth)continue;double area=Vector3.Cross(s.Positions[b]-s.Positions[a],s.Positions[c]-s.Positions[a]).Length()*.5;var sum=Vector3.Zero;
                foreach(var body in bodies)sum+=(ZombieLightChecks.SurfAlbedo(s,a,body)+ZombieLightChecks.SurfAlbedo(s,b,body)+ZombieLightChecks.SurfAlbedo(s,c,body))/3;sum/=bodies.Length;
                if(cls==NativeHordeModels.SurfSkin){skin+=sum*(float)area;sa+=area;}else{suit+=sum*(float)area;ca+=area;}
            }
            return (skin/(float)sa,suit/(float)ca);
        }
        private static void Reach(string root)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};string R(string p)=>File.ReadAllText(Path.Combine(root,p));
            var set=JsonSerializer.Deserialize<MapLookSet>(R("Assets/Conversion/Resources/MapLooks.json"),o);NativeMapLooks.Validate(set);
            var unity=JsonSerializer.Deserialize<UnityMapContent>(R("Assets/Conversion/Resources/UnityMaps.json"),o);var map=unity.maps.Single(m=>m.id==NativeExpeditionStation.MapId);var look=set.maps.Single(m=>m.id==map.id);
            var area=NativeExpeditionLayout.Parse(R("Assets/Conversion/Resources/ExpeditionLayout.json")).Area("exp-transit");var kit=NativeExpeditionStationKit.Build(map.blocks,area);var lights=NativeExpeditionStationLights.Build(kit,look);
            var st=new MapLookState();NativeMapLooks.Applied(look,map.sky,false,st);var calm=new NativeStationState();calm.Evaluate(NativeStationLive.Calm,30);
            NativeMapLooks.Toward(look.key,out double kx,out double ky,out double kz);NativeMapLooks.Toward(look.rim,out double rx,out double ry,out double rz);
            var key=Applied(st.KeyColor,st.KeyIntensity);var rim=Applied(st.RimColor,st.RimIntensity);var kd=Vector3.Normalize(new Vector3((float)kx,(float)ky,(float)kz));var rd=Vector3.Normalize(new Vector3((float)rx,(float)ry,(float)rz));
            float shade=st.KeyShadows=="none"?1:1-st.KeyStrength;var sky=Linear(st.AmbientSky);var eq=Linear(st.AmbientEquator);var gr=Linear(st.AmbientGround);Vector3 SH(Vector3 n)=>n.Y>=0?Vector3.Lerp(eq,sky,n.Y):Vector3.Lerp(eq,gr,-n.Y);
            var floorAlb=Hex(look.blockLooks.First(b=>b.block=="expedition-transit-floor-*").color);var (skin,suit)=Paint();var glove=Hex(0x2d3640);
            // The darkest walkable spot of the station's floors (the eye 1.7 m over it, the slots picked for it, the key in shadow).
            var nodes=map.nav.nodes.Where(n=>n.pos.y<9).ToArray();Vector3 spot=default;double darkest=9;
            Vector3 Block(Vector3 alb,Vector3 p,Vector3 n)=>alb*(SH(n)+key*Math.Max(0,Vector3.Dot(n,kd))*shade+rim*Math.Max(0,Vector3.Dot(n,rd))+ZombieLightChecks.Slots(lights,p,n,false));
            for(int i=0;i<nodes.Length;i+=3){var p=new Vector3(nodes[i].pos.x,nodes[i].pos.y-.9f,nodes[i].pos.z);lights.Settle(p.X,p.Y+1.7,p.Z,calm,30);double l=Lum(Block(floorAlb,p,Vector3.UnitY));if(l<darkest){darkest=l;spot=p;}}
            lights.Settle(spot.X,spot.Y+1.7,spot.Z,calm,30);
            // The shooter at the spot facing +z: the third-person muzzle 1.35 m up, .45 m ahead; the floor 1 m ahead of the feet, a
            // wall 2 m to the side facing the shooter, a zombie 3 m ahead (its chest 1.2 m up, facing the shooter, the eye behind
            // the shooter). First person: the view muzzle .55 m ahead of the eye and .18 m down, the hand .42 m behind it.
            var fwd=Vector3.UnitZ;var muzzle=spot+new Vector3(.2f,1.35f,.45f);var floorP=spot+new Vector3(0,0,1);var wallP=spot+new Vector3(2,1.2f,.8f);var chest=spot+new Vector3(.2f,1.2f,3);var eye=spot+new Vector3(.3f,1.9f,-2.2f);
            var eyeFp=spot+new Vector3(0,1.62f,0);var viewMuzzle=eyeFp+new Vector3(.1f,-.18f,.55f);var hand=viewMuzzle+new Vector3(-.02f,-.04f,-.42f);
            Vector3 Body(Vector3 alb,Vector3 p,Vector3 nrm,float wrap,float spec,float[] pos,float[] rgb,int n)
            {var v=Vector3.Normalize(eye-p);return ZombieLightChecks.ZombieLight(alb,nrm,kd,v,key,shade,wrap,spec)+ZombieLightChecks.ZombieLight(alb,nrm,rd,v,rim,1,wrap,spec)+alb*SH(nrm)+alb*ZombieLightChecks.Slots(lights,p,nrm,true)+alb*NativeMuzzleLight.Shade(p,nrm,.35f,pos,rgb,n);}
            var picker=new NativeMuzzleLightPicker();var rows=new List<string>();
            foreach(var gun in Guns)
            {
                NativeMuzzleLight.Look(gun,MuzzleKit.None,out var l);
                (double floor,double wall,double skinL,double suitL,double handL) At(Vector3 from,bool first,float level)
                {
                    picker.Begin();if(level>0)picker.Offer(1,from+fwd*NativeMuzzleLight.Lead,l,level,first);picker.Resolve(eye,NativeMuzzleLight.Slots,1);var pos=picker.Pos;var rgb=picker.Rgb;int n=picker.Count;
                    var wn=Vector3.Normalize(new Vector3(-1,0,0));var cn=-fwd;var hn=Vector3.Normalize(new Vector3(0,.3f,1));
                    return (Lum(Block(floorAlb,floorP,Vector3.UnitY)+floorAlb*NativeMuzzleLight.Shade(floorP,Vector3.UnitY,0,pos,rgb,n)),Lum(Block(floorAlb,wallP,wn)+floorAlb*NativeMuzzleLight.Shade(wallP,wn,0,pos,rgb,n)),
                        Lum(Body(skin,chest,cn,.18f,.08f,pos,rgb,n)),Lum(Body(suit,chest,cn,.12f,0,pos,rgb,n)),Lum(glove*(SH(hn)+key*Math.Max(0,Vector3.Dot(hn,kd))*shade)+glove*NativeMuzzleLight.Shade(hand,hn,.35f,pos,rgb,n)));
                }
                var before=At(muzzle,false,0);var peak=At(muzzle,false,1);double t2=l.Attack+2*l.Decay,t5=l.Attack+5*l.Decay;var late=At(muzzle,false,NativeMuzzleLight.Pulse(l,t2));var fp=At(viewMuzzle,true,1);
                Check(S8(peak.floor)-S8(before.floor)>=40&&peak.floor>=3*before.floor&&peak.floor<.9,gun+": the floor under the shooter flares ("+S8(before.floor)+" -> "+S8(peak.floor)+"/255) without clipping");
                Check(S8(peak.wall)-S8(before.wall)>=20,gun+": a wall 2 m away takes it ("+S8(before.wall)+" -> "+S8(peak.wall)+"/255)");
                Check(S8(peak.skinL)-S8(before.skinL)>=25&&peak.suitL>2*before.suitL,gun+": a zombie 3 m ahead lights up (skin "+S8(before.skinL)+" -> "+S8(peak.skinL)+", suit "+S8(before.suitL)+" -> "+S8(peak.suitL)+"/255)");
                Check(S8(fp.handL)-S8(before.handL)>=12&&S8(fp.handL)<=200,gun+": the first-person hand takes its own flash, softened ("+S8(before.handL)+" -> "+S8(fp.handL)+"/255)");
                Check(S8(fp.floor)>S8(before.floor)+15&&fp.floor<peak.floor,gun+": the first-person flash lights the floor too, dimmer than a third-person shooter's ("+S8(fp.floor)+"/255)");
                var gone=At(muzzle,false,NativeMuzzleLight.Pulse(l,t5));
                Check(late.floor-before.floor<=.15*(peak.floor-before.floor)&&gone.floor-before.floor<=.01*(peak.floor-before.floor),gun+": brief: "+F(t2*1000,"F0")+" ms on, under 15% of the flare is left ("+S8(late.floor)+"/255), at "+F(t5*1000,"F0")+" ms under 1% ("+S8(gone.floor)+"/255)");
                rows.Add(gun+" floor "+S8(before.floor)+"->"+S8(peak.floor)+" wall "+S8(before.wall)+"->"+S8(peak.wall)+" skin "+S8(before.skinL)+"->"+S8(peak.skinL)+" hand "+S8(before.handL)+"->"+S8(fp.handL));
            }
            // An old PvP map (NativeLit blocks, no practical slots): the same light on its floor.
            var content=JsonSerializer.Deserialize<ConversionContent>(R("Assets/Conversion/Resources/GameContent.json"),o);var moonMap=content.maps.Single(m=>m.id=="moonlit");
            var moon=set.maps.Single(m=>m.id=="moonlit");var ms=new MapLookState();NativeMapLooks.Applied(moon,moonMap.sky,false,ms);NativeMapLooks.Toward(moon.key,out double mx,out double my,out double mz);
            var mkey=Applied(ms.KeyColor,ms.KeyIntensity);var md=Vector3.Normalize(new Vector3((float)mx,(float)my,(float)mz));var mfloor=Hex(moonMap.floor);
            NativeMuzzleLight.Look("pistol",MuzzleKit.None,out var pl);picker.Begin();picker.Offer(1,new Vector3(0,1.35f,.55f),pl,1,false);picker.Resolve(new Vector3(0,2,-2),4,1);
            var amb=Vector3.Lerp(Linear(ms.AmbientEquator),Linear(ms.AmbientSky),1);double mb=Lum(mfloor*(amb+mkey*Math.Max(0,md.Y))),ma=Lum(mfloor*(amb+mkey*Math.Max(0,md.Y)+NativeMuzzleLight.Shade(new Vector3(0,0,1),Vector3.UnitY,0,picker.Pos,picker.Rgb,picker.Count)));
            Check(S8(ma)-S8(mb)>=20,"an old PvP map's NativeLit floor takes the pistol's flash too ("+S8(mb)+" -> "+S8(ma)+"/255)");
            facts.Add("Central Station's darkest spot ("+F(spot.X,"F1")+", "+F(spot.Z,"F1")+"), lit /255 before -> peak: "+string.Join("; ",rows)+"; Moonlit floor "+S8(mb)+"->"+S8(ma));
        }

        // ---------------- tiers and settings ----------------
        private static void Tiers()
        {
            void B(float glow,float fx,bool low,NativeKitFlash flash,int lights,float gain){NativeMuzzleLight.Budget(glow,fx,low,flash,out int n,out float g);Check(n==lights&&Math.Abs(g-gain)<1e-5f,"budget glow "+glow+" effects "+fx+" low "+low+" "+flash+": "+n+" lights x"+g);}
            B(1,1,false,NativeKitFlash.Full,4,1);B(1,1,true,NativeKitFlash.Full,1,1);B(.4f,1,false,NativeKitFlash.Full,2,.8f);B(1,.3f,false,NativeKitFlash.Reduced,2,.7f*.75f);B(0,1,false,NativeKitFlash.Full,0,.8f);B(1,1,false,NativeKitFlash.Calm,2,.5f);B(.3f,.3f,true,NativeKitFlash.Reduced,1,.7f*.8f*.75f);
            foreach(var gun in Guns)
            {
                NativeMuzzleFx.Look(gun,NativeWeaponSkins.StandardIndex,default,null,MuzzleKit.None,0,0,out var l);int hits=0,most=0;
                for(int s=0;s<4000;s++){long seed=NativeWeaponFx.Seed(s*.0311,2);int n=NativeMuzzleFx.SparkCount(l,seed,1,false);if(n>0)hits++;most=Math.Max(most,n);Check(n<=l.Sparks&&NativeMuzzleFx.SparkCount(l,seed,.3f,false)<=1&&NativeMuzzleFx.SparkCount(l,seed,1,true)<=1,gun+" sparks follow the tiers");}
                Check(Math.Abs(hits/4000.0-l.SparkChance)<.03&&most==l.Sparks,gun+": sparks on "+F(hits/40.0,"F0")+"% of shots (chance "+l.SparkChance+"), up to "+most);
                Check(NativeMuzzleFx.WispCount(l,1,false)==l.Wisps&&NativeMuzzleFx.WispCount(l,.3f,false)==1&&NativeMuzzleFx.WispCount(l,.3f,true)==0,gun+" wisps follow the tiers");
                for(int s=0;s<200;s++)for(int k=0;k<l.Sparks;k++)for(double age=.012;age<.25;age+=.03)if(NativeMuzzleFx.Spark(NativeWeaponFx.Seed(s,1),k,age,out var local,out _,out _,out _))Check(local.Z>0&&Math.Atan2(Math.Sqrt(local.X*local.X+local.Y*local.Y),local.Z)<=.106,gun+" sparks leave forward in a 6 degree cone");
                // First person: the biggest glow at the view muzzle's depth stays under ViewShare of the view's height.
                float tan=(float)Math.Tan(35*Math.PI/180),depth=.5f;NativeMuzzleFx.Fireball(l,1,0,out float fb,out _);float shown=NativeMuzzleFx.ViewSize(fb*1.25f*NativeMuzzleFx.ViewScale*NativeMuzzleFx.ViewGlow,depth,tan)/(2*depth*tan);
                Check(shown<=NativeMuzzleFx.ViewShare+1e-5f&&NativeMuzzleFx.ViewScale<=1&&NativeMuzzleFx.ViewGain<1&&NativeMuzzleFx.ViewLead>0,gun+": the view's flash never covers more than "+F(NativeMuzzleFx.ViewShare*100,"F0")+"% of the screen's height ("+F(shown*100,"F0")+"%)");
            }
            // Alarm lights Calm: a slow glow under machine-gun fire, no flashes; Reduced: softer.
            NativeMuzzleLight.Look("machinegun",MuzzleKit.None,out var mg);var ev=new List<ArenaEvent>();for(float t=0;t<2;t+=.085f)ev.Add(Shot(1,t,WeaponId.MachineGun));var buf=new float[NativeMuzzleLight.History];
            float cMin=9,cMax=0,fMin=9,fMax=0,rMin=9,rMax=0;
            for(double t=.6;t<1.9;t+=.002){int n=NativeMuzzleLight.Shots(ev.Where(e=>e.Time<=t&&e.Time>t-1).ToList(),1,-100,t,buf);float c=NativeMuzzleLight.Level(mg,buf,n,t,1,NativeKitFlash.Calm),f=NativeMuzzleLight.Level(mg,buf,n,t,1,NativeKitFlash.Full),r=NativeMuzzleLight.Level(mg,buf,n,t,1,NativeKitFlash.Reduced);cMin=Math.Min(cMin,c);cMax=Math.Max(cMax,c);fMin=Math.Min(fMin,f);fMax=Math.Max(fMax,f);rMin=Math.Min(rMin,r);rMax=Math.Max(rMax,r);}
            Check(cMax-cMin<=.25f*(fMax-fMin)&&rMax-rMin<fMax-fMin,"Alarm lights Calm holds a slow glow ("+F(cMin)+"-"+F(cMax)+"), Reduced flickers less ("+F(rMin)+"-"+F(rMax)+") than Full ("+F(fMin)+"-"+F(fMax)+")");
            facts.Add("budgets 4/2/1/0 lights by tier and settings; Calm "+F(cMin)+"-"+F(cMax)+" vs Full "+F(fMin)+"-"+F(fMax)+" under machine-gun fire");
        }

        // ---------------- allocation ----------------
        private static void Allocation()
        {
            var events=new ArenaEvent[400];for(int i=0;i<events.Length;i++)events[i]=new ArenaEvent{Kind=i%3==0?EventKind.Hit:EventKind.Shot,Source=1+i%8,Time=i*.005f,Amount=i%3==0?5:1+i%3};
            var picker=new NativeMuzzleLightPicker();var buf=new float[NativeMuzzleLight.History];var actors=new Actor[8];for(int i=0;i<8;i++)actors[i]=new Actor{Id=i+1,Weapon=i%2==0?WeaponId.MachineGun:WeaponId.Pistol,LastShot=1.9f,OverdriveAt=1,OverdriveUntil=5,VentUntil=5.8f};
            var chips=new ChipKinds{Ricochet=1,Vacuum=true,Extra=2};float acc=0;
            void Frame(int f)
            {
                double t=1.95+f/60.0;picker.Begin();
                for(int i=0;i<8;i++)
                {
                    var a=actors[i];string spec=i%3==0?"pistol":i%3==1?"rifle":"machinegun";int n=NativeMuzzleLight.Shots(events,a.Id,a.LastShot,t,buf);if(n==0)continue;
                    var kit=NativeMuzzleFx.KitOf(a,buf[0],(float)t,out float heat);NativeMuzzleFx.Look(spec,i%2,chips,NativeChipModules.Sockets(spec),kit,i%3,heat,out var l);long seed=NativeWeaponFx.Seed(buf[0],a.Id);double age=t-buf[0];
                    NativeMuzzleFx.Core(l,seed,age,out float s,out var c);NativeMuzzleFx.Fireball(l,seed,age,out float s2,out _);NativeMuzzleFx.Plume(l,seed,age,out float p,out _,out _);acc+=s+s2+p+c.R;
                    for(int k=0;k<l.Petals;k++){NativeMuzzleFx.Petal(l,seed,k,age,out float r,out _,out _,out _);acc+=r;}
                    for(int k=0;k<NativeMuzzleFx.Ports(l.Device).Length;k++)if(NativeMuzzleFx.Jet(l,seed,k,age,out var port,out float jl,out _,out _))acc+=jl+port.At.Z;
                    for(int k=0;k<l.Pods;k++)if(NativeMuzzleFx.PodMouth(l.Sockets,k,out var m)&&NativeMuzzleFx.Pod(l,seed,k,age,out float ps,out _,out _))acc+=ps+m.Z;
                    for(int k=0;k<NativeMuzzleFx.SparkCount(l,seed,1,false);k++)if(NativeMuzzleFx.Spark(seed,k,age,out var sl,out _,out _,out _))acc+=sl.Z;
                    for(int k=0;k<NativeMuzzleFx.WispCount(l,1,false);k++)if(NativeMuzzleFx.Wisp(seed,k,age,out var wl,out _,out _,out _))acc+=wl.Z;
                    if(NativeMuzzleLight.Look(spec,kit,out var ll))picker.Offer(a.Id,new Vector3(i,1.4f,2),ll,NativeMuzzleLight.Level(ll,buf,n,t,a.Id,NativeKitFlash.Full),i==0);
                }
                picker.Resolve(Vector3.Zero,4,1);acc+=NativeMuzzleLight.Shade(new Vector3(0,0,1),Vector3.UnitY,0,picker.Pos,picker.Rgb,picker.Count).X;
            }
            for(int f=0;f<30;f++)Frame(f);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<600;f++)Frame(f);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0&&acc>0,"600 frames of 8 shooters' flashes, lights, picks and the shader's light allocate nothing ("+bytes+" bytes)");
            facts.Add("0 bytes over 600 frames x 8 shooters");
        }

        // ---------------- the shader and runtime contract ----------------
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Select(l=>{int c=l.IndexOf("//",StringComparison.Ordinal);return c<0?l:l.Substring(0,c);}));
        private static string Between(string text,string begin,string end){int a=text.IndexOf(begin,StringComparison.Ordinal),b=a<0?-1:text.IndexOf(end,a+begin.Length,StringComparison.Ordinal);Check(a>=0&&b>a,"block "+begin);return text.Substring(a,b-a);}
        private static int Count(string text,string s){int n=0,i=0;while((i=text.IndexOf(s,i,StringComparison.Ordinal))>=0){n++;i+=s.Length;}return n;}
        private static void Contract(string root)
        {
            string R(string p)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",p));
            var cg=R("Resources/NativeMuzzleLight.cginc");var block=Code(Between(cg,"MUZZLE LIGHT SHARED BEGIN","MUZZLE LIGHT SHARED END"));
            // The block is the Core's Shade, line for line.
            foreach(var line in new[]{"float4 _MuzzleLightPos[4];float4 _MuzzleLightColor[4];float _MuzzleLightCount;","float3 MuzzleLights(float3 world,float3 n,float wrap)","if(_MuzzleLightCount<0.5)return sum;",
                "float3 d=_MuzzleLightPos[k].xyz-world;float len2=max(dot(d,d),0.000001);float len=sqrt(len2);","float q=len/max(_MuzzleLightPos[k].w,0.0001);float a=saturate(1.0-q*q);float w=_MuzzleLightColor[k].w;",
                "float soft=len2/(len2+w*w);float fall=1.0/(1.0+q*q*MUZZLE_FALL);float ndl=saturate((dot(n,d/len)+wrap)/(1.0+wrap));","sum+=_MuzzleLightColor[k].rgb*(a*a*soft*fall*ndl);"})Check(block.Contains(line),"the shader's light block: "+line);
            var fallDef=Regex.Match(block,@"#define MUZZLE_FALL ([0-9.]+)\r?\n");Check(fallDef.Success&&float.Parse(fallDef.Groups[1].Value,CultureInfo.InvariantCulture)==NativeMuzzleLight.Fall,"the shader's inverse-square share is the Core's ("+NativeMuzzleLight.Fall+")");
            Check(NativeMuzzleLight.Slots==4&&Regex.Matches(block,@"\[4\]").Count==2&&block.Contains("for(int k=0;k<4;k++)"),"four slots, as the Core's");
            var ints=Regex.Matches(block.Replace("for(int k=0;k<4;k++)","").Replace("[4]",""),@"(?<![\w.\[])\d+(?![\w.])").Cast<Match>().Select(m=>m.Value).ToList();Check(ints.Count==0,"the block: float literals only, GLSL-safe ("+string.Join(", ",ints)+")");
            Check(!block.Contains("mul(")&&!block.Contains("tex")&&!Regex.IsMatch(block,@"\b(line|sample|point|near|far)\b"),"the block: no matrices, textures or reserved names");
            // The four shaders read it once each, where every map's surfaces light.
            var bl=R("Resources/NativeBlockLit.shader");var shared=Between(bl,"// SHARED BEGIN","// SHARED END");var lights=Between(shared,"float3 BlockLights(float3 world,float3 n)","// Ground haze");
            Check(Count(bl,"#include \"NativeMuzzleLight.cginc\"")==1&&shared.Contains("#include \"NativeMuzzleLight.cginc\"")&&Count(lights,"sum+=MuzzleLights(world,n,0.0);")==1&&lights.IndexOf("sum+=MuzzleLights(world,n,0.0);",StringComparison.Ordinal)<lights.IndexOf("return sum;",StringComparison.Ordinal)&&!Code(bl).Contains("ForwardAdd"),"NativeBlockLit adds the muzzle lights to its light block (no add pass)");
            var src=R("Resources/NativeSourceLit.shader");var armor=Between(src,"void surfArmor(Input i,inout SurfaceOutput o)","ENDCG");const string srcHook="if(_StageLit<.5&&!Unlit(i.color.rgb,i.emission.rgb))o.Emission+=o.Albedo*MuzzleLights(i.worldPos,normalize(i.worldNormal),.35);";
            Check(Count(src,"#include \"NativeMuzzleLight.cginc\"")==1&&Count(src,srcHook)==1&&armor.Contains(srcHook)&&armor.IndexOf("surf(i,o);if(_InkFill>.5)return;",StringComparison.Ordinal)<armor.IndexOf(srcHook,StringComparison.Ordinal)&&Regex.Matches(src,"#pragma").Count==3,"SourceLit lights pilots and the view weapon with them on every map, never the ink, a stage or an unlit part");
            var z=R("Resources/NativeZombieLit.shader");const string zHook="if(_InkFill<.5&&_StageLit<.5&&!Unlit(i.color.rgb,i.emission.rgb))o.Emission+=o.Albedo*MuzzleLights(i.worldPos,normalize(i.worldNormal),.35);";
            Check(Count(z,"#include \"NativeMuzzleLight.cginc\"")==1&&Count(z,zHook)==1&&Between(z,"void surfZombie(Input i,inout SurfaceOutput o)","ENDCG").Contains(zHook),"ZombieLit lights the horde and the station's kinds with them");
            var lit=R("Resources/NativeLit.shader");Check(Count(lit,"#include \"NativeMuzzleLight.cginc\"")==1&&lit.Contains("struct Input { float3 worldPos; float3 worldNormal; };")&&lit.Contains("o.Emission = _Emission.rgb + o.Albedo * MuzzleLights(IN.worldPos, normalize(IN.worldNormal), 0.0);"),"NativeLit (the old maps' blocks, trims and gore) takes them too");
            // The runtime: the uploader, the arena hooks, the old Unity-light pulse gone, the stages and map changes clearing it.
            var up=Code(R("Runtime/NativeMuzzleLights.cs"));
            Check(up.Contains("Shader.PropertyToID(\"_MuzzleLightPos\")")&&up.Contains("Shader.PropertyToID(\"_MuzzleLightColor\")")&&up.Contains("Shader.PropertyToID(\"_MuzzleLightCount\")")&&Count(up,"Shader.SetGlobalVectorArray(")==4&&up.Contains("new Vector4[NativeMuzzleLight.Slots]"),"NativeMuzzleLights uploads the four slots by cached id");
            var upload=Between(up,"public static void Upload(","public static void Off()");var made=Regex.Matches(upload,@"new\s+(\w+)").Cast<Match>().Select(m=>m.Groups[1].Value).Where(v=>v!="Vector4"&&v!="Color").ToList();Check(made.Count==0,"the upload allocates nothing ("+string.Join(", ",made)+")");
            var fxRaw=R("Runtime/NativeWeaponFxPresentation.cs");var fx=Code(fxRaw);var mfx=Between(fx,"private void MuzzleFx(","private void MuzzleRecord(");
            Check(mfx.Contains("NativeMuzzleLight.Shots(weaponSnapshot?.Events,a.Id,a.LastShot,time,muzzleShots)")&&mfx.Contains("NativeMuzzleFx.Look(a.SourceSpec,skin,chips,sockets,kit,NativeWeaponSkins.Tier(LowGraphics,WeaponEffectsQuality,distance,first),heat,out var look)")&&mfx.Contains("var sockets=skin==NativeWeaponSkins.StandardIndex?MuzzleChips(a,out chips):null;")&&mfx.Contains("NativeMuzzleFx.KitOf(a,shot,time,out float heat)"),"the flash reads the replicated rounds, the seat's skin and chips, the kit and the model tier");
            // ChipModuleChecks' rule: in Core only the module files, in Runtime only the module bank and presentation name them;
            // the module presentation hands the flash its kinds and sockets.
            var modules=Code(R("Runtime/NativeChipModulePresentation.cs"));
            Check(!R("Core/NativeMuzzleFlash.cs").Contains("NativeChipModules")&&!fxRaw.Contains("NativeChipModules")&&!R("Runtime/NativeWeaponFxDraw.cs").Contains("NativeChipModules")&&modules.Contains("private ChipSockets MuzzleChips(Actor a,out ChipKinds chips)")&&modules.Contains("chips=NativeChipModules.Kinds(pilot.Seat.chips,pilot.Stats);return NativeChipModules.Sockets(a.SourceSpec);"),"the flash reads the modules only through the module presentation (kinds and sockets)");
            Check(mfx.Contains("Vector3 crown=hand.MultiplyPoint3x4(new Vector3(0,0,look.Crown));")&&mfx.Contains("NativeWeaponFxShapes.Muzzle(draw,first,crown,x,y,z,scale,look,NativeWeaponFx.Seed(shot,a.Id),age,WeaponEffectsQuality,LowGraphics,rigFactor,first?ViewCamera:null);"),"the flash leaves from the drawn crown in the right hand's barrel frame, seeded by the round");
            Check(mfx.Contains("muzzleLights.Offer(a.Id,")&&mfx.Contains("NativeMuzzleLight.Level(light,muzzleShots,n,time,a.Id,KitFlash),first)"),"each shooter offers one merged light, honouring Alarm lights");
            var wfx=Between(fx,"private void WeaponFx(","private void ShotFx(");int submit=wfx.IndexOf("draw.Submit(",StringComparison.Ordinal),resolve=wfx.IndexOf("muzzleLights.Resolve(",StringComparison.Ordinal);
            Check(fx.Contains("weaponFx?.Begin();muzzleLights.Begin();")&&submit>0&&resolve>submit&&wfx.Contains("NativeMuzzleLight.Budget(GlowQuality,WeaponEffectsQuality,LowGraphics,KitFlash,out int lights,out float gain);")&&wfx.Contains("NativeMuzzleLights.Upload(muzzleLights);")&&fx.Contains("muzzleLights.Clear();NativeMuzzleLights.Off();"),"the frame begins the offers, resolves and uploads them after every rig fired, a reset clears them");
            var glow=Code(R("Runtime/NativeCombatGlow.cs"));Check(!Regex.IsMatch(glow,@"\bPulse\(")&&!glow.Contains("NativeMuzzleFx")&&glow.Contains("projectileLights=new Light[3],impactLights=new Light[2],armorLights=new Light[2];")&&Regex.Matches(glow,@"AddComponent<Light>").Count==1,"the old muzzle pulse into a free projectile light is gone; still seven pooled Unity lights, none for muzzles");
            var lighting=Code(R("Runtime/NativeLighting.cs"));Check(Regex.Matches(lighting,@"WardFieldOff\(\);EnterStage\(\);NativeMuzzleLights\.Off\(\);").Count==3&&lighting.Contains("private static void EnterStage(){if(introActive)return;introActive=true;stageFog=ReadFog();}")&&Between(lighting,"public static void Rooftop(","public static MapLookSet LookSet()").Contains("NativeMuzzleLights.Off();")&&Between(lighting,"public static void Apply(","public static void ZoneHouse(").Contains("NativeMuzzleLights.Off();"),"an entrance, the draft, the rooftop and a map change clear the muzzle lights");
            var draw=Code(R("Runtime/NativeWeaponFxDraw.cs"));var shapes=Between(draw,"public static void Muzzle(","public static bool Swept(");
            Check(shapes.Contains("NativeMuzzleFx.Ports(look.Device)")&&shapes.Contains("NativeMuzzleFx.Jet(look,seed,k,age,")&&shapes.Contains("NativeMuzzleFx.PodMouth(look.Sockets,i,out var mouth)")&&shapes.Contains("NativeMuzzleFx.ViewSize(")&&shapes.Contains("first?NativeMuzzleFx.ViewScale:1")&&shapes.Contains("first?NativeMuzzleFx.ViewGain:1")&&shapes.Contains("first?NativeMuzzleFx.ViewGlow:1"),"the geometry draws the vents, the pods and the first-person limits");
            Check(!Regex.IsMatch(shapes,@"new\s+(?!Vector2|Vector3|Color)\w+"),"the flash geometry allocates nothing");
            var kit=Code(R("Runtime/NativeHazardKitPresentation.cs"));Check(kit.Contains("NativeMuzzleFx.Look(\"machinegun\",out var look);")&&kit.Contains("NativeWeaponFxShapes.Muzzle(kitWeapon,false,m,side,up,f,1.2f,look,seed,age,q,low,1);"),"the hazard kit's turrets keep the plain flash");
            // Presentation only.
            foreach(var f in new[]{"Core/NativeMuzzleFlash.cs","Core/NativeMuzzleLight.cs","Runtime/NativeMuzzleLights.cs"})
            {
                var c=Code(R(f));Check(!c.Contains(".LastShot=")&&!c.Contains(".Hp=")&&!c.Contains("Events.Add(")&&!c.Contains("arena."),f+" writes nothing of the match");
                Check(Regex.IsMatch(R(f+".meta"),@"^guid: [0-9a-f]{32}$",RegexOptions.Multiline),f+" has its .meta");
            }
            Check(Regex.IsMatch(R("Resources/NativeMuzzleLight.cginc.meta"),@"^guid: [0-9a-f]{32}$",RegexOptions.Multiline)&&R("Resources/NativeMuzzleLight.cginc.meta").Contains("ShaderIncludeImporter:"),"the include has its .meta");
            var guids=Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories).Select(p=>Regex.Match(File.ReadAllText(p),@"^guid: ([0-9a-f]{32})",RegexOptions.Multiline).Groups[1].Value).Where(g=>g.Length==32).ToList();
            Check(guids.Count==guids.Distinct().Count(),"every .meta GUID is unique ("+guids.Count+")");
        }
    }
}
#endif
