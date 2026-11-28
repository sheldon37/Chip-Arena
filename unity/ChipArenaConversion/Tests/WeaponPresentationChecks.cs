#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Expected values come from tools/export_weapon_fixtures.mjs, which runs the pinned original weapon-visuals.ts,
    // melee-trails.ts and attackPose, and three r180's primitives; nothing here is produced by the C# port.
    internal static class WeaponPresentationChecks
    {
        public static void Run(string root,ConversionContent data)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/weapon-presentation-parity.json")));var r=document.RootElement;string where="";
            void Near(float a,float b,string name,float tolerance=.00002f){if(!MathUtil.Finite(a)||Math.Abs(a-b)>tolerance)throw new Exception("Weapon presentation "+where+" "+name+": "+a+" != "+b);}
            void Check(bool ok,string name){if(!ok)throw new Exception("Weapon presentation "+where+" "+name);}
            Vector3 V(JsonElement e)=>new Vector3(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle());
            Quaternion Q(JsonElement e)=>new Quaternion(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle(),e[3].GetSingle());
            Dictionary<string,int> Chips(JsonElement e){var d=new Dictionary<string,int>();foreach(var p in e.EnumerateObject())d[p.Name]=p.Value.GetInt32();return d;}
            void Piece(WeaponPiece p,JsonElement e)
            {
                string shape=e.GetProperty("shape").GetString();Check(shape==(p.Shape==WeaponShape.Cube?"cube":p.Shape==WeaponShape.Sphere?"sphere":p.Shape==WeaponShape.Cone?"cone":"ring"),"shape "+shape);
                Check(e.GetProperty("glow").GetBoolean()==p.Glow&&e.GetProperty("shadow").GetBoolean()==!p.Glow,"material");Check(e.GetProperty("color").GetInt32()==p.Color,"colour");
                Near(Vector3.Distance(p.Position,V(e.GetProperty("p"))),0,"position");Near(Vector3.Distance(p.Scale,V(e.GetProperty("s"))),0,"scale");Near(1-Math.Abs(Quaternion.Dot(p.Rotation,Q(e.GetProperty("q")))),0,"rotation");
            }
            // three r180 primitives (sphere 63 vertices, torus 85, cone 12 side + 11 cap, box 24).
            int vertices=0;var positions=new List<Vector3>();var normals=new List<Vector3>();var indices=new List<int>();
            foreach(var (name,shape) in new[]{("cube",WeaponShape.Cube),("sphere",WeaponShape.Sphere),("cone",WeaponShape.Cone),("ring",WeaponShape.Ring)})
            {
                where=name;NativeWeaponDressing.Primitive(shape,positions,normals,indices);var g=r.GetProperty("geometry").GetProperty(name);var p=g.GetProperty("positions");var n=g.GetProperty("normals");var x=g.GetProperty("indices");
                Check(p.GetArrayLength()==positions.Count*3&&x.GetArrayLength()==indices.Count,"vertex or index count");
                for(int i=0;i<positions.Count;i++){Near(positions[i].X,p[i*3].GetSingle(),"x");Near(positions[i].Y,p[i*3+1].GetSingle(),"y");Near(positions[i].Z,p[i*3+2].GetSingle(),"z");Near(normals[i].X,n[i*3].GetSingle(),"nx");Near(normals[i].Y,n[i*3+1].GetSingle(),"ny");Near(normals[i].Z,n[i*3+2].GetSingle(),"nz");}
                for(int i=0;i<indices.Count;i++)Check(indices[i]==x[i].GetInt32(),"index "+i);vertices+=positions.Count;
            }
            foreach(var s in r.GetProperty("sockets").EnumerateObject())
            {
                where=s.Name;bool tip=NativeWeaponDressing.BladeTip(s.Name,out var local);var t=s.Value.GetProperty("tip");Check(tip==(t.ValueKind!=JsonValueKind.Null),"blade tip presence");if(tip)Near(Vector3.Distance(local,V(t)),0,"blade tip");
                var mz=s.Value.GetProperty("muzzle");Near(NativeWeaponDressing.MuzzleZ(s.Name),mz.ValueKind==JsonValueKind.Null?.8f:mz[2].GetSingle(),"muzzle");
            }
            var length=r.GetProperty("bladeLength");foreach(var spec in new[]{"daggers","sword","greatsword","shield"}){where=spec;Near(NativeWeaponDressing.BladeLength(spec),length.GetProperty(spec).GetSingle(),"blade length");}
            // Chip modules: the source's own stats drive the layout; the native stats must agree on every key the modules read.
            var buffer=new WeaponPiece[NativeWeaponDressing.MaxPieces];int pieces=0,cases=0;
            foreach(var c in r.GetProperty("modules").EnumerateArray())
            {
                string spec=c.GetProperty("spec").GetString();float time=c.GetProperty("time").GetSingle();var chips=Chips(c.GetProperty("chips"));where="modules "+spec+" "+string.Join(",",chips.Select(k=>k.Key+":"+k.Value))+" t="+time;
                var stats=new ChipStats(data.baseStats);var native=NativeContentRules.Stats(data,spec,chips);
                foreach(var s in c.GetProperty("stats").EnumerateObject()){stats[s.Name]=s.Value.GetDouble();Near((float)native[s.Name],(float)s.Value.GetDouble(),"native stat "+s.Name,.00001f);}
                var weapon=data.weapons.First(w=>w.id==spec);int count=NativeWeaponDressing.Modules(spec,weapon.@class=="ranged",(int)weapon.color,chips,stats,buffer);var expected=c.GetProperty("pieces");
                Check(count==expected.GetArrayLength(),"module count "+count+" != "+expected.GetArrayLength());
                for(int i=0;i<count;i++){var p=buffer[i];NativeWeaponDressing.Animate(ref p,time);where="modules "+spec+" piece "+i+" t="+time;Piece(p,expected[i]);}
                pieces+=count;cases++;
            }
            // Blade enchants: elements from the stats (renderer.ts:129), stacks from the chip counts.
            int flames=0;
            foreach(var c in r.GetProperty("enchants").EnumerateArray())
            {
                string spec=c.GetProperty("spec").GetString();float time=c.GetProperty("time").GetSingle();var chips=Chips(c.GetProperty("chips"));where="enchant "+spec+" t="+time;
                chips.TryGetValue("ember",out int ember);chips.TryGetValue("poison",out int poison);
                int count=NativeWeaponDressing.Enchant(spec,c.GetProperty("burn").GetDouble()>0,c.GetProperty("poison").GetDouble()>0,ember,poison,buffer,0);var expected=c.GetProperty("pieces");
                Check(count==expected.GetArrayLength(),"enchant count "+count+" != "+expected.GetArrayLength());
                for(int i=0;i<count;i++){var p=buffer[i];NativeWeaponDressing.Animate(ref p,time);where="enchant "+spec+" piece "+i+" t="+time;Piece(p,expected[i]);}
                flames+=count;
            }
            // The source flash pinned here stays the reference for when a shot flashes (age 0 to FlashLife); the arena no longer
            // draws its cone: owner 2026-10-11 effects overhaul (weapons milestone) replaced it with the ETFX-style muzzle star
            // (Core NativeMuzzleFx, Runtime MuzzleFx), which WeaponFxChecks pins as a Unity-owned look.
            int flashes=0;
            foreach(var f in r.GetProperty("flashes").EnumerateArray())
            {
                string spec=f.GetProperty("spec").GetString();double shot=f.GetProperty("shot").GetDouble(),time=f.GetProperty("time").GetDouble();where="flash "+spec+" shot "+shot+" at "+time;
                bool visible=NativeWeaponDressing.MuzzleFlash(spec,shot,time,out var p);Check(visible==f.GetProperty("visible").GetBoolean(),"visibility");
                if(visible){Near(Vector3.Distance(p.Position,V(f.GetProperty("p"))),0,"position");Near(Vector3.Distance(p.Scale,V(f.GetProperty("s"))),0,"scale",.0001f);Near(1-Math.Abs(Quaternion.Dot(p.Rotation,Q(f.GetProperty("q")))),0,"rotation",.0001f);Check(p.Glow&&p.Color==0xffdb90&&p.Shape==WeaponShape.Cone,"flash material");}
                flashes++;
            }
            // Sweep activation over the replicated actor fields, against MeleeTrails.sweeping(attackPose).
            int windows=0;
            foreach(var w in r.GetProperty("windows").EnumerateArray())
            {
                float rate=w.GetProperty("rate").GetSingle(),last=w.GetProperty("lastAttack").GetSingle();string special=w.GetProperty("special").ValueKind==JsonValueKind.Null?null:w.GetProperty("special").GetString();
                float Or(string key)=>w.GetProperty(key).ValueKind==JsonValueKind.Null?-100:w.GetProperty(key).GetSingle();
                var a=new Actor{Hp=100,SourceSpec=w.GetProperty("spec").GetString(),AttackAt=last,AttackUntil=last+5,SwingAt=Or("swingAt"),SwingUntil=Or("swingUntil"),VisualWindup=w.GetProperty("windup").GetSingle(),VisualActive=.16f/rate,CastRate=rate,RollAt=Or("rollAt"),SpecialKind=special,SpecialReleased=w.GetProperty("released").GetBoolean(),SpecialAt=20,SpecialUntil=1e6f};
                foreach(var s in w.GetProperty("samples").EnumerateArray()){float age=s[0].GetSingle();where="window "+a.SourceSpec+" "+w.GetProperty("mode").GetString()+" rate "+rate+" age "+age;Check(NativeWeaponDressing.Sweeping(a,last+age)==(s[1].GetInt32()==1),"sweeping");windows++;}
            }
            // Swept ribbons: the recorded blade poses replayed through the native sweeps, compared with the source meshes.
            var vertices2=new Vector3[96];var scalars=new float[96];int frames=0,ribbons=0;
            foreach(var q in r.GetProperty("sweeps").EnumerateArray())
            {
                string spec=q.GetProperty("spec").GetString();float quality=q.GetProperty("quality").GetSingle();int color=NativeWeaponDressing.SweepColor(Chips(q.GetProperty("chips")));
                var sweeps=new NativeMeleeSweeps();var names=new Dictionary<SweepKey,string>();var outputs=q.GetProperty("frames").EnumerateArray().ToDictionary(f=>f.GetProperty("k").GetInt32());int k=0;
                foreach(var frame in q.GetProperty("inputs").EnumerateArray())
                {
                    float time=(float)frame.GetProperty("time").GetDouble();
                    foreach(var p in frame.GetProperty("players").EnumerateArray())if(p.GetProperty("on").GetBoolean())
                    {var key=new SweepKey{Id=p.GetProperty("id").GetInt32(),At=p.GetProperty("lastAttack").GetSingle(),Index=p.GetProperty("index").GetInt32()};names[key]=p.GetProperty("key").GetString();sweeps.Add(key,spec,color,V(p.GetProperty("grip")),V(p.GetProperty("tip")),time);}
                    sweeps.Update(time,frame.GetProperty("enabled").GetBoolean(),quality);
                    if(outputs.TryGetValue(k,out var expected))
                    {
                        where=q.GetProperty("name").GetString()+" frame "+k;var list=expected.GetProperty("sweeps");Check(sweeps.Count==expected.GetProperty("size").GetInt32()&&sweeps.Count==list.GetArrayLength(),"live sweeps "+sweeps.Count);
                        for(int i=0;i<sweeps.Count;i++)
                        {
                            var s=sweeps[i];var e=list[i];Check(names[s.Key]==e.GetProperty("key").GetString(),"sweep order");Check(s.Color==e.GetProperty("color").GetInt32(),"ribbon colour");Check(s.Count==e.GetProperty("samples").GetInt32(),"sample count "+s.Count+" != "+e.GetProperty("samples").GetInt32());
                            int n=NativeMeleeSweeps.Build(s,time,vertices2,scalars,0);Check(n==e.GetProperty("n").GetInt32(),"drawn samples");var pos=e.GetProperty("pos");var glow=e.GetProperty("glow");
                            for(int v=0;v<n*2;v++){Near(Vector3.Distance(vertices2[v],new Vector3(pos[v*3].GetSingle(),pos[v*3+1].GetSingle(),pos[v*3+2].GetSingle())),0,"ribbon vertex "+v,.0005f);Near(scalars[v],glow[v].GetSingle(),"ribbon glow "+v,.002f);}
                            if(n>0)ribbons++;
                        }
                        frames++;
                    }
                    k++;
                }
            }
            // Named contract points (spec C11): the sword window, the peak glow, the flame count, the magazine width, the
            // mirrored reach strip and the bare fists.
            where="contract";var sword=new Actor{Hp=100,SourceSpec="sword",AttackAt=0,AttackUntil=1,SwingAt=.14f,SwingUntil=.30f,VisualWindup=.14f,VisualActive=.16f,CastRate=1,RollAt=-100};
            Check(!NativeWeaponDressing.Sweeping(sword,.1225f)&&NativeWeaponDressing.Sweeping(sword,.1240f)&&NativeWeaponDressing.Sweeping(sword,.3695f)&&!NativeWeaponDressing.Sweeping(sword,.3705f),"sword sweep window [.1232,.37]");
            sword.AttackUntil=0;Check(!NativeWeaponDressing.Sweeping(sword,.2f),"a cancelled strike stops sweeping");
            Check(NativeWeaponDressing.Enchant("sword",true,false,6,0,buffer,0)==16&&NativeWeaponDressing.Enchant("sword",true,true,0,0,buffer,0)==22,"enchant counts");
            var base0=new ChipStats(data.baseStats);var magazine=new Dictionary<string,int>{["magazine"]=8};int m=NativeWeaponDressing.Modules("pistol",true,0,magazine,base0,buffer);Check(m==1&&Math.Abs(buffer[0].Scale.X-.35f)<1e-5f,"magazine width");
            var reach=NativeContentRules.Stats(data,"sword",new Dictionary<string,int>{["reach"]=1});m=NativeWeaponDressing.Modules("sword",false,0,new Dictionary<string,int>{["reach"]=1},reach,buffer);Check(m==1&&Math.Abs(CapturedRig.Mirror(buffer[0].Position).X+.12f)<1e-6f,"reach strip at Unity x -.12");
            Check(NativeWeaponDressing.Modules("emperor",false,0,new Dictionary<string,int>{["overclock"]=3},base0,buffer)==0&&NativeWeaponDressing.Modules("ansatsuken",false,0,new Dictionary<string,int>{["vacuum"]=1},base0,buffer)==0&&NativeWeaponDressing.Enchant("emperor",true,true,1,1,buffer,0)==0,"bare fists");
            // The reach stretch (character-animation.ts:57, checked with the pinned source): every melee weapon by stats.reach;
            // the fists only while stunned, rolling or knocked down (the fighter layers reset them to 1); never the Siren or a gun.
            Check(NativeWeaponDressing.ReachScale("sword",1.2f,false)==1.2f&&NativeWeaponDressing.ReachScale("daggers",2,false)==2&&NativeWeaponDressing.ReachScale("shield",1.4f,false)==1.4f&&NativeWeaponDressing.ReachScale("greatsword",1.35f,true)==1.35f,"melee reach stretch");
            Check(NativeWeaponDressing.ReachScale("emperor",2,false)==1&&NativeWeaponDressing.ReachScale("emperor",2,true)==2&&NativeWeaponDressing.ReachScale("ansatsuken",1.6f,false)==1&&NativeWeaponDressing.ReachScale("ansatsuken",1.6f,true)==1.6f,"fist reach stretch only under skipped fighter layers");
            Check(NativeWeaponDressing.ReachScale("siren",2,true)==1&&NativeWeaponDressing.ReachScale("pistol",2,false)==1&&NativeWeaponDressing.ReachScale("sword",0,false)==1&&NativeWeaponDressing.ReachScale("sword",float.NaN,false)==1&&NativeWeaponDressing.ReachScale("sword",float.PositiveInfinity,false)==1,"no stretch for the Siren, guns or invalid reach");
            Console.WriteLine("PASS "+vertices+" three r180 primitive vertices, "+pieces+" chip-module pieces over "+cases+" loadouts, "+flames+" enchant pieces, "+flashes+" muzzle-flash frames, "+windows+" sweep-window samples and "+ribbons+" swept ribbons over "+frames+" frames against the pinned source");
        }
    }
}
#endif
