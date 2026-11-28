#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    // The Civil Defence caches' presentation (R9, W6; the board 3 opening with the owner's three decisions, 2026-11-19): the tier
    // adapter (A2's field, missing reads standard), the chip rarities on the ladder, the words (no raw id, no allocation), R9's
    // beats, the ladder, nothing on the floor, no ring, small blooms gone by 0.45 s, lights at 3 Hz or slower, the FX quad
    // budget, Reduced effects showing only the lid, determinism, the cache.* sounds, the CD-647 locker inside A2's block, the
    // open moment from the replicated view (the burst public, the buyer private, a late joiner's first-seen clock), the
    // hologram's flare and the DEV preview.
    internal static class CacheOpeningChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Cache openings: "+what);count++;}
        private static readonly NativeCacheTier[] Tiers=NativeCacheTiers.All;

        public static void Run(string root)
        {
            count=0;
            Adapter(root);Words();Beats();Ladder();FloorAndRings();Blooms();Pulses();Budget();OnItsLine();Reduced();Determinism();Sounds();Locker();MapBinding();Clock();Hologram();Preview();NoAllocation();Sources(root);
            Console.WriteLine("PASS Cache openings ("+count+" checks; the locker's triangles a tier "+string.Join(", ",Tiers.Select(t=>NativeCacheLockerModels.Triangles(t)))+"; FX quads at peak "+string.Join(", ",Tiers.Select(t=>Peak(O(t))))+"; particles "+string.Join(", ",Tiers.Select(t=>O(t).ParticleCount))+")");
        }

        // 1. The tier through one adapter: A2's ExpeditionChestOffer.tier, missing reads standard; the chip rarities' mapping.
        private static void Adapter(string root)
        {
            Check(NativeCacheTiers.Of(null)==NativeCacheTier.Standard,"no offer reads standard");
            foreach(var (id,t) in new[]{("standard",NativeCacheTier.Standard),("rare",NativeCacheTier.Rare),("epic",NativeCacheTier.Epic),("legendary",NativeCacheTier.Legendary),("LEGENDARY",NativeCacheTier.Legendary),("",NativeCacheTier.Standard),(null,NativeCacheTier.Standard),("mythic",NativeCacheTier.Standard)})
                Check(NativeCacheTiers.Parse(id)==t,"the contract's tier id "+(id??"null")+" reads "+t);
            foreach(var t in Tiers)Check(NativeCacheTiers.Parse(NativeCacheTiers.Id(t))==t&&NativeCacheTiers.OfTone(NativeCacheTiers.Tone(t))==t&&NativeCacheTiers.IsCacheTone(NativeCacheTiers.Tone(t)),"tier "+t+" round-trips its id and tone");
            // Until A2 adds the field every offer reads standard; once it lands the adapter must read it (its one-line change).
            var field=typeof(ExpeditionChestOffer).GetField("tier");var offer=new ExpeditionChestOffer{id="c",generation="g",price=100};
            if(field==null)Check(NativeCacheTiers.Of(offer)==NativeCacheTier.Standard,"no tier field yet (A2): every cache reads standard");
            else{field.SetValue(offer,"epic");Check(NativeCacheTiers.Of(offer)==NativeCacheTier.Epic,"A2's tier field is read by the adapter (NativeCacheTiers.TierField: =>o?.tier)");}
            string src=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeCacheTiers.cs"));
            Check(Regex.Matches(src,@"private static string TierField\(ExpeditionChestOffer o\)=>").Count==1,"one adapter line reads the offer's tier");
            // Chip-machine purchases: the chip's rarity on the four tiers (owner, 2026-11-19).
            foreach(var (r,t) in new[]{("common",NativeCacheTier.Standard),("uncommon",NativeCacheTier.Standard),("rare",NativeCacheTier.Rare),("epic",NativeCacheTier.Epic),("legendary",NativeCacheTier.Legendary),
                ("mythic",NativeCacheTier.Legendary),("rainbow",NativeCacheTier.Legendary),("siren",NativeCacheTier.Standard),("l44t",NativeCacheTier.Standard),(null,NativeCacheTier.Standard)})
                Check(NativeCacheTiers.OfRarity(r)==t,"chip rarity "+(r??"null")+" opens "+t);
            foreach(var t in Tiers)Check(Array.IndexOf(NativeMachineModels.PickupTiers,NativeCacheTiers.Cartridge(t))>=0,"tier "+t+"'s cartridges are a built chip model");
            Check(NativeCacheTiers.Hex(NativeCacheTier.Standard)==NativeMachineModels.RarityColors[0]&&NativeCacheTiers.Hex(NativeCacheTier.Rare)==NativeMachineModels.RarityColors[2]&&NativeCacheTiers.Hex(NativeCacheTier.Epic)==NativeMachineModels.RarityColors[3]&&NativeCacheTiers.Hex(NativeCacheTier.Legendary)==NativeMachineModels.RarityColors[4],"the tier colours are the rarity colours");
        }

        // 2. R9's words: RARE CACHE · $175 · 3 CHIPS · KEEP 1 · F · OPEN CACHE; $75 SHORT; RESTOCKS IN 0:42.
        private static void Words()
        {
            var l=new ExpeditionLabels();
            Check(NativeCacheTiers.Title(NativeCacheTier.Rare)=="RARE CACHE"&&NativeCacheTiers.Title(NativeCacheTier.Standard)=="STANDARD CACHE"&&NativeCacheTiers.Title(NativeCacheTier.Legendary)=="LEGENDARY CACHE","the titles");
            Check(NativeCacheText.Detail(l,175)=="$175 · 3 CHIPS · KEEP 1"&&NativeCacheText.Prompt=="F · OPEN CACHE"&&NativeCacheText.Key=="F"&&NativeCacheText.Open=="OPEN CACHE","the offer and the prompt");
            Check(NativeCacheText.Short(l,75)=="$75 SHORT","the shortfall");
            Check(NativeCacheText.Restocking(l,42)=="RESTOCKS IN 0:42"&&NativeCacheText.Restocking(l,65)=="RESTOCKS IN 1:05"&&NativeCacheText.Restocking(l,0)=="RESTOCKS IN 0:00"&&NativeCacheText.Restocking(l,-3)=="RESTOCKS IN 0:00","the countdown");
            Check(ReferenceEquals(NativeCacheText.Restocking(l,42),NativeCacheText.Restocking(l,42))&&ReferenceEquals(NativeCacheText.Detail(l,175),NativeCacheText.Detail(l,175)),"the words are cached (one string per value)");
        }

        private static NativeCacheOpening O(NativeCacheTier t,bool reduced=false,uint seed=647)=>NativeCacheOpenFx.Opening(t,seed,reduced);
        private static IEnumerable<float> Times(float from,float to,float step=1/120f){for(float t=from;t<=to;t+=step)yield return t;}

        // 3. R9's beats: the latches at 0.1 s, the lid at 0.25 s, the cartridges fanned 0.45-0.85 s and docked by 2.0 s (the buyer
        // only), the shut at 2.2 s with the lamp faces red; the lid inside its 105 degree stop.
        private static void Beats()
        {
            var f=new NativeCacheFrame();
            foreach(var t in Tiers)foreach(bool reduced in new[]{false,true})
            {
                var o=O(t,reduced);string n=t+(reduced?" (reduced)":"");
                Check(o.Lid(.249f)==0&&o.Lid(.6f)>90&&o.Lid(2.0f)>90&&o.Lid(NativeCacheOpenFx.End)==0,n+": the lid opens at 0.25 s and is home after the shut");
                Check(Times(0,3).All(x=>o.Lid(x)>=0&&o.Lid(x)<=NativeCacheOpenFx.LidMax+1e-3f),n+": the lid never passes its 105 degree stop");
                Check(o.Latch(.09f)==0&&o.Latch(.2f)< -30,n+": the latches pop at 0.1 s");
                o.Frame(f,.44f,true,true,1.3f,1.25f,-.65f);Check(f.CartridgeCount==0,n+": no cartridge before 0.45 s");
                o.Frame(f,.9f,true,true,1.3f,1.25f,-.65f);
                if(reduced)Check(f.CartridgeCount==0,n+": only the lid with Reduced effects (no cartridges)");
                else Check(f.CartridgeCount==3&&f.Cartridges.Take(3).All(c=>Math.Abs(c.Y-NativeCacheOpenFx.ChestHeight)<.05f&&c.Alpha>.9f)&&f.Cartridges[0].X<f.Cartridges[1].X&&f.Cartridges[1].X<f.Cartridges[2].X,n+": the buyer's three cartridges fanned at chest height");
                o.Frame(f,.9f,false,false,0,0,0);Check(f.CartridgeCount==0,n+": a watcher never sees the cartridges");
                if(!reduced)
                {
                    o.Frame(f,1.99f,true,true,1.3f,1.25f,-.65f);Check(f.CartridgeCount==3&&f.Cartridges.Take(3).All(c=>Vector3.Distance(new Vector3(c.X,c.Y,c.Z),new Vector3(1.3f,1.25f,-.65f))<.2f),n+": the cartridges dock on the buyer's pilot by 2.0 s");
                    o.Frame(f,2.05f,true,true,1.3f,1.25f,-.65f);Check(f.CartridgeCount==0,n+": docked: gone");
                }
                o.Frame(f,2.1f,false,false,0,0,0);Check(Enumerable.Range(0,f.Lamps).All(i=>f.LampHex[i]!=NativeCacheTiers.Red),n+": the lamps are not red before the shut");
                o.Frame(f,2.4f,false,false,0,0,0);Check(f.Lamps==NativeCacheTiers.Lamps(t)&&Enumerable.Range(0,f.Lamps).All(i=>f.LampHex[i]==NativeCacheTiers.Red&&f.LampGain[i]>1)&&f.StripHex==NativeCacheTiers.Red,n+": after the shut the lamp faces and the strip are red");
                Check(f.Spill==0&&f.Flash==0&&f.Pillar==0&&f.LampLight==0,n+": after the shut no light lands on the pavement (the red is on the faces only)");
            }
        }

        // 4. The ladder: every step up is longer, taller, busier; STANDARD has no column (owner: "a glow with particles").
        private static float Tallest(NativeCacheOpening o,out float longest)
        {
            var f=new NativeCacheFrame();float top=0;longest=0;
            foreach(var t in Times(0,2.6f,1/60f)){o.Frame(f,t,false,false,0,0,0);for(int i=0;i<f.StreakCount;i++){var s=f.Streaks[i];top=Math.Max(top,Math.Max(s.Y,s.Y-s.Ay));longest=Math.Max(longest,(float)Math.Sqrt(s.Ax*s.Ax+s.Ay*s.Ay+s.Az*s.Az));}for(int i=0;i<f.GlowCount;i++)top=Math.Max(top,f.Glows[i].Y);}
            return top;
        }
        private static void Ladder()
        {
            for(int i=1;i<4;i++)Check(NativeCacheOpenFx.Length[i]>NativeCacheOpenFx.Length[i-1]&&NativeCacheOpenFx.Height[i]>NativeCacheOpenFx.Height[i-1]&&NativeCacheTiers.Lamps(Tiers[i])==i+1,"tier "+Tiers[i]+" is longer and taller than "+Tiers[i-1]);
            float std=Tallest(O(NativeCacheTier.Standard),out float stdLong);
            Check(std<2.5f&&stdLong<1,"STANDARD: a glow with particles in its colour and no column (its highest light "+std.ToString("0.00")+" m, its longest streak "+stdLong.ToString("0.00")+" m)");
            float rare=Tallest(O(NativeCacheTier.Rare),out _),epic=Tallest(O(NativeCacheTier.Epic),out _),leg=Tallest(O(NativeCacheTier.Legendary),out _);
            Check(rare>=3.9f&&rare<5,"RARE's column reaches 4 m ("+rare+")");Check(epic>=9.4f&&epic<11,"EPIC's column clears a row's 7 m eaves ("+epic+")");Check(leg>=39,"LEGENDARY's pillar stands 40 m over the roofs ("+leg+")");
            int last=0;foreach(var t in Tiers){var o=O(t);int n=o.ParticleCount+o.BladeCount;Check(n>last,t+" is busier than the tier below ("+n+" > "+last+")");last=n;}
        }

        // 5. Nothing on the floor, no ring: every spark rises and dies 0.3 m or more over the floor, every light blade rises from
        // its base, every puff stays off the floor; no glow, streak or puff flat on the ground in any frame (for sale too).
        private static void FloorAndRings()
        {
            var f=new NativeCacheFrame();
            foreach(var t in Tiers)
            {
                var o=O(t);float lowLight=9,lowPuff=9;
                foreach(var x in Times(0,2.6f)){o.Frame(f,x,true,true,1.3f,1.25f,-.65f);Lowest(f,ref lowLight,ref lowPuff);}
                foreach(var x in Times(0,4.8f,1/30f)){NativeCacheOpenFx.ForSale(f,t,x,false,false);Lowest(f,ref lowLight,ref lowPuff);NativeCacheOpenFx.ForSale(f,t,x,true,true);Lowest(f,ref lowLight,ref lowPuff);}
                NativeCacheOpenFx.Shut(f,t);Lowest(f,ref lowLight,ref lowPuff);
                Check(lowLight>=.1f,t+": no glow or streak lies on the floor (lowest "+lowLight.ToString("0.00")+" m: "+lowWhat+")");
                float glint=o.LowestGlint();Check(glint>=NativeCacheOpenFx.SparkFloor,t+": every glint, ember and mote rises and dies "+NativeCacheOpenFx.SparkFloor+" m or more over the floor (lowest "+glint.ToString("0.00")+" m)");
                Check(lowPuff>=.05f,t+": the vapour and dust stay off the floor (lowest "+lowPuff.ToString("0.00")+" m)");
            }
        }
        private static string lowWhat="";
        private static void Lowest(NativeCacheFrame f,ref float light,ref float puff)
        {
            for(int i=0;i<f.GlowCount;i++){float y=f.Glows[i].Y;if(y<light){light=y;lowWhat="glow "+f.Glows[i].Hex.ToString("x6")+" size "+f.Glows[i].Size;}}
            for(int i=0;i<f.StreakCount;i++){var s=f.Streaks[i];float y=Math.Min(s.Y,s.Y-s.Ay);if(y<light){light=y;lowWhat="streak w "+s.Width+" head "+s.Y+" axis "+s.Ay;}}
            for(int i=0;i<f.PuffCount;i++)puff=Math.Min(puff,f.Puffs[i].Y);
        }

        // 6. The blooms: 0.45 m across at most, at the tray's rim (centre 0.5 m up at most), gone by 0.45 s (the lid's lip reads).
        private static void Blooms()
        {
            var f=new NativeCacheFrame();
            foreach(var t in Tiers)
            {
                var o=O(t);int seen=0;bool ok=true;float lateBloom=0;
                foreach(var x in Times(0,2.6f)){o.Frame(f,x,false,false,0,0,0);for(int i=0;i<f.GlowCount;i++){var g=f.Glows[i];if(!g.Bloom)continue;seen++;ok&=g.Size<=NativeCacheOpenFx.GlowMax+1e-4f&&g.Y<=NativeCacheOpenFx.GlowTop+1e-4f;if(x>=NativeCacheOpenFx.GlowEnd)lateBloom=x;}}
                Check(seen>0&&ok,t+": every bloom is 0.45 m or less, at the tray's rim");Check(lateBloom==0,t+": every bloom is gone by 0.45 s");
                foreach(var x in Times(0,2.6f)){o.Frame(f,x,false,false,0,0,0);for(int i=0;i<f.GlowCount;i++)Check(f.Glows[i].Size<=NativeCacheOpenFx.GlowMax+1e-4f,t+": no glow sprite wider than 0.45 m");}
            }
        }

        // 7. Photosensitivity: every light (spill, flash, pillar, seams, the lamps' own light) pulses 3 times a second or slower;
        // for sale, the lamps blink the tier's count each 2.4 s, 0.35 s apart.
        private static int Busiest(Func<float,float> curve)
        {
            const float step=1/240f;var pulses=new List<float>();float max=0;for(float t=0;t<3;t+=step)max=Math.Max(max,curve(t));if(max<=0)return 0;
            float prom=.2f*max,lo=curve(0),hi=lo,hiAt=0;bool rising=true;
            for(float t=step;t<3;t+=step)
            {
                float v=curve(t);
                if(rising){if(v>hi){hi=v;hiAt=t;}else if(hi-v>=prom&&hi-lo>=prom){pulses.Add(hiAt);rising=false;lo=v;}}
                else{if(v<lo)lo=v;else if(v-lo>=prom){rising=true;hi=v;hiAt=t;}}
            }
            if(rising&&hi-lo>=prom)pulses.Add(hiAt);
            int worst=0;foreach(var p in pulses)worst=Math.Max(worst,pulses.Count(q=>q>=p&&q<p+1));return worst;
        }
        private static void Pulses()
        {
            var f=new NativeCacheFrame();
            foreach(var t in Tiers)
            {
                var o=O(t);
                float Light(float x,int which){o.Frame(f,x,false,false,0,0,0);return which==0?f.Spill:which==1?f.Flash:which==2?f.Pillar:which==3?f.SeamLight:f.LampLight;}
                for(int w=0;w<5;w++){int n=Busiest(x=>Light(x,w));Check(n<=NativeCacheOpenFx.PulseHzMax,t+": light "+w+" pulses "+n+" times in its busiest second (3 at most)");}
                int lit=0,rises=0;bool was=false;for(float x=0;x<NativeCacheOpenFx.SaleCycle;x+=1/480f){bool on=NativeCacheOpenFx.SaleLit(t,x);if(on&&!was)rises++;if(on)lit++;was=on;}
                Check(rises==NativeCacheTiers.Lamps(t),t+": for sale, the lamps blink "+NativeCacheTiers.Lamps(t)+" times each 2.4 s ("+rises+")");
                Check(1/NativeCacheOpenFx.SaleStep<=NativeCacheOpenFx.PulseHzMax,"the for-sale blinks come no faster than 3 a second");
            }
        }

        // 8. The FX quad budget (bible 10.1: the frame's 2,800): each tier's peak, the ladder, four LEGENDARY at once well inside.
        private static int Peak(NativeCacheOpening o)
        {var f=new NativeCacheFrame();int peak=0;foreach(var x in Times(0,2.6f)){o.Frame(f,x,true,true,1.3f,1.25f,-.65f);peak=Math.Max(peak,f.Quads);}return peak;}
        private static void Budget()
        {
            int[] cap={60,110,150,320};int last=0;
            foreach(var t in Tiers)
            {
                int p=Peak(O(t));Check(p<=cap[NativeCacheTiers.Index(t)]&&p<=420,t+": "+p+" FX quads at peak (cap "+cap[NativeCacheTiers.Index(t)]+")");
                Check(p>last,t+": busier than the tier below ("+p+" > "+last+" quads)");last=p;
                Check(p<NativeCacheFrame.MaxGlows&&p<NativeCacheFrame.MaxGlows+NativeCacheFrame.MaxStreaks,t+": the frame's arrays hold its peak");
            }
            Check(4*Peak(O(NativeCacheTier.Legendary))<2800/2,"four LEGENDARY openings at once use under half the frame's 2,800 FX quads");
            var f=new NativeCacheFrame();foreach(var t in Tiers){int m=0;foreach(var x in Times(0,4.8f,1/30f)){NativeCacheOpenFx.ForSale(f,t,x,false,false);m=Math.Max(m,f.Quads);}Check(m<=30,t+": for sale, "+m+" quads (the motes and the lamps)");}
        }

        // 8b. The column stands on its locker from every view (owner, 2026-11-19: the LEGENDARY beam "starts to move away from
        // the crate"). The shared streak lies in the camera's plane through its middle (NativeVfx.shader VfxStreakOffset), so the
        // presentation lays each long streak onto its head's camera plane (NativeCacheOpenFx.OntoHeadPlane). Emulated here at
        // 1080p: the quad's ends land on the true foot and top (or the clip point in front of the camera, its taper scaled) and
        // the line between them is the true line, from a watcher, the buyer, a far watcher and a camera below looking up.
        // Uncorrected, the watcher's 40 m pillar's foot missed its locker by tens of pixels (the emulation must still see it).
        private static void OnItsLine()
        {
            const float Px=935.3f,Near=.2f;
            var views=new (string name,Vector3 eye,Vector3 at)[]{("a watcher 10 m off",new Vector3(-3,2.6f,-9.5f),new Vector3(.6f,1,0)),("the buyer within 3 m",new Vector3(.4f,2.4f,-4.2f),new Vector3(0,1.2f,0)),
                ("a watcher 25 m off",new Vector3(-8,3,-24),new Vector3(0,4,0)),("a camera below, looking up",new Vector3(2,-6,-12),new Vector3(0,6,0))};
            float before=0,after=0;int clipped=0,longOnes=0;
            foreach(var tier in new[]{NativeCacheTier.Rare,NativeCacheTier.Epic,NativeCacheTier.Legendary})
            {
                var f=new NativeCacheFrame();O(tier).Frame(f,1.0f,false,false,0,0,0);
                foreach(var (name,eye,at) in views)
                {
                    var fw=Vector3.Normalize(at-eye);var right=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,fw));var up=Vector3.Cross(fw,right);
                    float Z(Vector3 p)=>Vector3.Dot(p-eye,fw);
                    Vector2 P(Vector3 p){var d=p-eye;float z=Vector3.Dot(d,fw);return new Vector2(Vector3.Dot(d,right)/z*Px,Vector3.Dot(d,up)/z*Px);}
                    // the shader's quad: its ends are the centre +- the axis in the camera's plane
                    (Vector3 head,Vector3 tail) Quad(Vector3 h,Vector3 a){var c=h-a*.5f;var p=right*Vector3.Dot(a,right)+up*Vector3.Dot(a,up);return (c+p*.5f,c-p*.5f);}
                    for(int k=0;k<f.StreakCount;k++)
                    {
                        var g=f.Streaks[k];NativeCacheOpenFx.ToWorld(0,g.X,g.Z,out float hx,out float hz);NativeCacheOpenFx.ToWorld(0,g.Ax,g.Az,out float ax,out float az);
                        var head=new Vector3(hx,g.Y,hz);var axis=new Vector3(ax,g.Ay,az);var tail=head-axis;if(axis.Length()<=NativeCacheOpenFx.ExactStreak)continue;longOnes++;
                        bool column=Math.Abs(g.Ax)<1e-4f&&Math.Abs(g.Az)<1e-4f&&axis.Length()>5;
                        if(column&&name=="a watcher 10 m off"&&Z(head-axis*.5f)>Near&&Z(tail)>Near){var q=Quad(head,axis);before=Math.Max(before,(P(q.head)-P(head)).Length());}
                        float bx=axis.X,by=axis.Y,bz=axis.Z,taper=g.Taper;
                        Check(NativeCacheOpenFx.OntoHeadPlane(eye.X,eye.Y,eye.Z,fw.X,fw.Y,fw.Z,Near,head.X,head.Y,head.Z,ref bx,ref by,ref bz,ref taper),tier+", "+name+": the streak's head is in front of the camera");
                        float zh=Z(head),zt=Z(tail),keep=zt<Near?(zh-Near)/(zh-zt):1;var end=head-axis*keep;if(keep<1)clipped++;
                        var r=Quad(head,new Vector3(bx,by,bz));var mid=head-axis*(keep*.37f);
                        var line=P(r.tail)-P(r.head);var off=P(mid)-P(r.head);float across=Math.Abs(line.X*off.Y-line.Y*off.X)/Math.Max(line.Length(),1e-4f);
                        float miss=Math.Max(Math.Max((P(r.head)-P(head)).Length(),(P(r.tail)-P(end)).Length()),across);after=Math.Max(after,miss);
                        Check(miss<.25f,tier+", "+name+": a "+axis.Length().ToString("0.0")+" m streak lands on its true line ("+miss.ToString("0.000")+" px off)");
                        Check(Math.Abs(taper-g.Taper*keep)<1e-4f,tier+", "+name+": a clipped streak's taper is scaled to the share kept");
                    }
                }
            }
            Check(clipped>0,"the buyer's view clips the pillar in front of the camera (the clip is exercised)");
            Check(before>10,"uncorrected, the watcher's pillar's foot misses its locker ("+before.ToString("0.0")+" px): the emulation sees the reported slide");
            Console.WriteLine("PASS Cache columns on their line: "+longOnes+" long streaks in four views, at most "+after.ToString("0.000")+" px off (uncorrected, the 40 m pillar's foot missed its locker by "+before.ToString("0.0")+" px), "+clipped+" clipped in front of the camera");
        }

        // 9. Reduced effects (owner, 2026-11-19: "only the lid opening"): the lid swings, the lamps hold their colour; no column,
        // particle, blade, bloom, flash, light or cartridge, and nothing moves for sale.
        private static void Reduced()
        {
            var f=new NativeCacheFrame();
            foreach(var t in Tiers)
            {
                var o=O(t,true);Check(o.ParticleCount==0&&o.BladeCount==0,t+": Reduced effects builds no particle or blade");bool still=true;
                foreach(var x in Times(0,2.6f))
                {
                    o.Frame(f,x,true,true,1.3f,1.25f,-.65f);
                    still&=f.StreakCount==0&&f.PuffCount==0&&f.CartridgeCount==0&&f.Spill==0&&f.Flash==0&&f.Pillar==0&&f.SeamLight==0&&f.GlowCount<=f.Lamps&&Enumerable.Range(0,f.GlowCount).All(i=>!f.Glows[i].Bloom);
                    still&=Enumerable.Range(0,f.Lamps).All(i=>f.LampHex[i]==o.Hex&&Math.Abs(f.LampGain[i]-1)<1e-4f||x>=NativeCacheOpenFx.ShutAt);
                }
                Check(still,t+": Reduced effects shows only the lid (no column, particles, blades, blooms, lights or cartridges; the lamps steady)");
                Check(o.Lid(.25f+.32f)>NativeCacheOpenFx.LidMax-1&&o.Lid(.25f+.16f)<o.Lid(.25f+.32f),t+": with Reduced effects the lid swings open in 0.32 s");
                var a=new NativeCacheFrame();var b=new NativeCacheFrame();NativeCacheOpenFx.ForSale(a,t,.1f,true,false);NativeCacheOpenFx.ForSale(b,t,1.7f,true,false);
                Check(Same(a,b),t+": for sale with Reduced effects nothing moves or blinks");
            }
        }
        private static bool Same(NativeCacheFrame a,NativeCacheFrame b)
        {
            if(a.GlowCount!=b.GlowCount||a.StreakCount!=b.StreakCount||a.PuffCount!=b.PuffCount||a.Lamps!=b.Lamps)return false;
            for(int i=0;i<a.GlowCount;i++)if(!a.Glows[i].Equals(b.Glows[i]))return false;for(int i=0;i<a.StreakCount;i++)if(!a.Streaks[i].Equals(b.Streaks[i]))return false;
            for(int i=0;i<a.Lamps;i++)if(a.LampGain[i]!=b.LampGain[i]||a.LampHex[i]!=b.LampHex[i])return false;return true;
        }

        // 10. The same frames on every peer: a pure function of the time, the tier and the seed.
        private static void Determinism()
        {
            foreach(var t in Tiers)
            {
                var a=new NativeCacheOpening(t,99,false);var b=new NativeCacheOpening(t,99,false);var c=new NativeCacheOpening(t,100,false);var fa=new NativeCacheFrame();var fb=new NativeCacheFrame();var fc=new NativeCacheFrame();bool same=true,differs=false;
                foreach(var x in Times(0,2.6f,1/30f)){a.Frame(fa,x,true,true,1,1,1);b.Frame(fb,x,true,true,1,1,1);c.Frame(fc,x,true,true,1,1,1);same&=Same(fa,fb);differs|=!Same(fa,fc);}
                Check(same,t+": one seed draws the same frames");Check(differs,t+": another seed scatters its particles otherwise");
            }
            Check(NativeCacheOpenFx.Seed("exp-city:cache:0021","0:a:1")==NativeCacheOpenFx.Seed("exp-city:cache:0021","0:a:1")&&NativeCacheOpenFx.Seed("a","b")!=NativeCacheOpenFx.Seed("a","c"),"the seed is the cache's id and the generation it opened");
        }

        // 11. Sounds: cache.* keys only, none named for a hazard, R9's beats; the restock's click.
        private static void Sounds()
        {
            var hazard=new[]{"shock","boom","blast","explo","arc","zap","bolt","hit","alarm","boss","turret","siren","danger","impact","slam"};
            foreach(var t in Tiers)foreach(bool reduced in new[]{false,true})
            {
                var cues=NativeCacheOpenFx.Cues(t,reduced);
                Check(cues.All(c=>c.Key.StartsWith("cache.",StringComparison.Ordinal)&&!hazard.Any(h=>c.Key.Contains(h))&&Array.IndexOf(NativeCacheOpenFx.Keys,c.Key)>=0),t+": cache.* sounds only, none named for a hazard");
                Check(cues.Any(c=>c.At==NativeCacheOpenFx.LatchAt&&c.Key=="cache.latch")&&cues.Any(c=>c.At==NativeCacheOpenFx.LidAt)&&cues.Any(c=>c.At==NativeCacheOpenFx.ShutAt&&c.Key=="cache.lid.shut"),t+": the latch, the lid and the shut on R9's beats");
                Check(cues.All(c=>!string.IsNullOrEmpty(c.Fallback)&&c.Gain>0&&c.Gain<=1),t+": each plays an existing sound until its recording lands");
            }
            Check(NativeCacheOpenFx.Restock.Key=="cache.restock"&&NativeCacheOpenFx.Keys.Distinct().Count()==NativeCacheOpenFx.Keys.Length,"the restock's click; the keys listed once");
        }

        // 12. The CD-647 locker: about 2.5k triangles, inside A2's 0.92 x 0.62 x 0.58 m block and the 0.63 m chest floor limit
        // when shut, its lamps one per grade, its lights on the tinted bones, four grades that differ in their outline.
        private static void Locker()
        {
            var lids=new List<Vector3>();
            foreach(var t in Tiers)
            {
                var body=NativeCacheLockerModels.BuildBody(t);var lid=NativeCacheLockerModels.BuildLid(t);var latch=NativeCacheLockerModels.BuildLatch(t);int tris=NativeCacheLockerModels.Triangles(t);
                Check(tris>=1200&&tris<=2700,t+": "+tris+" triangles (about 2.5k; the four: "+string.Join(", ",Tiers.Select(x=>NativeCacheLockerModels.Triangles(x)))+")");
                var all=new List<Vector3>();foreach(var m in new[]{body,lid})foreach(var s in m.Sections)all.AddRange(s.Positions);
                foreach(var s in latch.Sections)foreach(var p in s.Positions){all.Add(p+new Vector3(-.33f,0,0));all.Add(p+new Vector3(.33f,0,0));}
                var b=NativeCacheLockerModels.Block;float minY=all.Min(p=>p.Y),maxY=all.Max(p=>p.Y),maxX=all.Max(p=>Math.Abs(p.X)),maxZ=all.Max(p=>Math.Abs(p.Z));
                Check(minY>=-1e-4f&&maxY<=b.Y+1e-4f&&maxY<=NativeCacheLockerModels.FloorLimit&&maxX<=b.X/2+1e-4f&&maxZ<=b.Z/2+1e-4f,t+": the shut locker stands inside A2's block ("+(2*maxX).ToString("0.000")+" x "+maxY.ToString("0.000")+" x "+(2*maxZ).ToString("0.000")+" m; farthest "+all.OrderByDescending(p=>Math.Max(Math.Abs(p.Z)/b.Z,Math.Max(Math.Abs(p.X)/b.X,p.Y/b.Y/2))).First()+")");
                Check(body.Sections.Any(s=>s.Bone==NativeCacheLockerModels.LampsA&&s.Triangles>0)&&(NativeCacheTiers.Lamps(t)<2||body.Sections.Any(s=>s.Bone==NativeCacheLockerModels.LampsB&&s.Triangles>0)),t+": its lock lamps on the tinted lamp bones");
                Check(body.Sections.Any(s=>s.Bone==NativeCacheLockerModels.Glass&&s.Triangles>0&&s.Colors.All(c=>Math.Abs(c.W-NativeCacheLockerModels.GlowAlpha)<1e-4f)),t+": the tray's glow is see-through");
                Check(lid.Sections.Any(s=>s.Bone==NativeCacheLockerModels.Lights&&s.Triangles>0),t+": the lid's LED strip on the tinted-light bone");
                Check(t!=NativeCacheTier.Epic||body.Sections.Any(s=>s.Bone==NativeCacheLockerModels.Lights&&s.Triangles>0),"EPIC's seams light up");
                lid.Bounds(NativeCacheLockerModels.Body,out _,out var top);lids.Add(top);
                Check(NativeCacheLockerModels.Hinge.Z>0&&NativeCacheLockerModels.Hinge.Y==NativeCacheOpenFx.Body1,"the hinge at the back");
            }
            Check(lids[1].Y>lids[0].Y+.02f&&lids[2].Y>lids[0].Y+.02f&&lids[3].Y>lids[0].Y+.01f,"each grade above STANDARD adds to the lid's outline (RARE's bar, EPIC's spine, LEGENDARY's ribs)");
            Check(NativeCacheLockerModels.All().Select(m=>m.id).Distinct().Count()==12&&NativeCacheLockerModels.All().All(m=>m.id.StartsWith(NativeCacheLockerModels.Prefix,StringComparison.Ordinal)),"twelve models, one id each");
        }

        // 13. The open moment from the replicated view only: the burst public, the buyer private (the Leaks audit), a late joiner's
        // first-seen clock (opened: shut and red, no burst), the restock's click, nothing stale after leaving the area.
        private static NativeExpedition Party()
        {
            var run=NativeExpedition.Create("cache-run","city","p0","sword",91,new[]{new ExpeditionInstance{id="ward-zero",target=3}});Check(run.Join("p1","sword"),"join");return run;
        }
        private static void MapBinding()
        {
            var index=new NativeCacheBlockIndex();
            var anchor=new ExpeditionWorldAnchor{id="exp-city:cache:1",x=2,y=1,z=4};
            var map=new ContentMap{id="exp-city",blocks=Array.Empty<ContentBlock>()};
            index.Bind(map.blocks);Check(index.At(anchor)==null,"unsupported anchor uses the hologram");
            var locker=NativeExpeditionGeneratedWorld.CacheBlock(anchor);
            var reroll=map.WithCacheBlocks(new[]{locker});
            index.Bind(reroll.blocks);Check(index.At(anchor)==locker,"same map and cache IDs resolve the new run's locker");
            anchor.x+=5;Check(index.At(anchor)==null,"previous location cannot hide a block for a different placement");
            index.Bind(map.blocks);Check(index.At(anchor)==null,"switching back drops the previous run's block");
            foreach(var plan in NativeCityLayout.Plans)
            {
                var city=new NativeLayeredCityWalk(plan,17);
                index.Bind(city.Map.blocks);
                foreach(var cache in city.Caches)
                    Check(index.At(cache)!=null,plan.Name+" resolves generated locker "+cache.id);
            }
        }
        private static void Clock()
        {
            var run=Party();Check(run.Credit("p0","cash",300)&&run.Credit("p1","cash",50),"credit");
            Check(run.SetWorld(new[]{new ExpeditionWorldAnchor{id="c1",area="city",kind="chest",x=2,yaw=90},new ExpeditionWorldAnchor{id="c2",area="city",kind="chest",x=-2},new ExpeditionWorldAnchor{id="c3",area="ward-zero",kind="chest"}},
                new[]{new ExpeditionChestOffer{id="c1",generation="g1",price=100},new ExpeditionChestOffer{id="c2",generation="h1",price=100},new ExpeditionChestOffer{id="c3",generation="w1",price=100}}),"world");
            var mine=new NativeCacheClock();var theirs=new NativeCacheClock();double now=10;
            mine.Observe(NativeExpeditionView.Build(run,"p0"),now);theirs.Observe(NativeExpeditionView.Build(run,"p1"),now);
            Check(mine.Items.Count==2&&mine.Items.All(s=>s.State==NativeCacheState.ForSale&&!s.Short&&s.Tier==NativeCacheTier.Standard),"two caches here for sale (the third in another area draws nothing)");
            Check(theirs.Items.All(s=>s.Short),"for a pilot short of the price it goes dim");
            var c1=mine.Items.First(s=>s.Id=="c1");Check(c1.X==2&&c1.Yaw==90&&!c1.Locker,"at its anchor, turned by its yaw; no A2 block yet: no locker");
            // p0 buys c1; the scene restocks it at once with a cooldown (the director's Step): the open moment
            Check(run.Purchase("p0","g1",100),"p0 buys");Check(run.RestockChest("c1","g2",100,run.Snapshot().elapsed+42),"restock with its cooldown");now+=.05;
            var v0=NativeExpeditionView.Build(run,"p0");var v1=NativeExpeditionView.Build(run,"p1");
            Check(NativeExpeditionView.Leaks(v0,run.Snapshot()).Count==0&&NativeExpeditionView.Leaks(v1,run.Snapshot()).Count==0,"the views it reads are leak-free");
            mine.Observe(v0,now);theirs.Observe(v1,now);
            var m1=mine.Items.First(s=>s.Id=="c1");var t1=theirs.Items.First(s=>s.Id=="c1");
            Check(m1.State==NativeCacheState.Opening&&m1.Since==0&&m1.Buyer&&m1.Seconds==42,"the buyer sees its opening begin, as the buyer");
            Check(t1.State==NativeCacheState.Opening&&t1.Since==0&&!t1.Buyer,"a watcher sees the same public burst, never as the buyer");
            Check(m1.Seed==t1.Seed&&m1.Seed==NativeCacheOpenFx.Seed("c1","g1"),"every peer seeds it alike (the cache and the generation that opened)");
            Check(mine.Items.First(s=>s.Id=="c2").State==NativeCacheState.ForSale,"the other cache stays for sale");
            // a late joiner (or an area just entered) first sees it already opened: shut and red, no burst
            var late=new NativeCacheClock();late.Observe(NativeExpeditionView.Build(run,"p1"),now+1);var l1=late.Items.First(s=>s.Id=="c1");
            Check(l1.State==NativeCacheState.Restocking&&l1.Since>=NativeCacheOpenFx.End&&!l1.Buyer,"a late joiner sees it opened from its first-seen clock (shut and red, no burst)");
            now+=1.5;mine.Observe(NativeExpeditionView.Build(run,"p0"),now);Check(Math.Abs(mine.Items.First(s=>s.Id=="c1").Since-1.5f)<1e-3f,"the opening runs on the presentation clock");
            // the cooldown passes: a click, for sale again
            for(int i=0;i<170;i++)run.Tick(.25);now+=43;mine.Observe(NativeExpeditionView.Build(run,"p0"),now);
            Check(mine.Items.First(s=>s.Id=="c1").State==NativeCacheState.ForSale&&mine.Clicks.Count==1&&mine.Clicks[0].Id=="c1","at restock, a click, and it is for sale again");
            now+=.1;mine.Observe(NativeExpeditionView.Build(run,"p0"),now);Check(mine.Clicks.Count==0,"one click");
            // leaving the area forgets it: coming back never replays a stale opening
            Check(run.Enter("ward-zero"),"enter");mine.Observe(NativeExpeditionView.Build(run,"p0"),now+1);Check(mine.Items.Count==1&&mine.Items[0].Id=="c3","in another area: its own cache");
            // a solid block predicate (A2) makes it a locker
            mine.Solid=a=>a.id=="c3";mine.Observe(NativeExpeditionView.Build(run,"p0"),now+2);Check(mine.Items[0].Locker,"where A2's block stands, the steel locker");
            mine.Observe(null,now);Check(mine.Items.Count==0,"no view, nothing");
        }

        // 14. Today's hologram plays the same opening as a flare: its lid lifts, it flares to full light; nothing with no opening.
        private static void Hologram()
        {
            var clock=new NativeCacheClock();clock.Items.Add(new NativeCacheShow{Id="c",State=NativeCacheState.Opening,Since=.5f,Tier=NativeCacheTier.Standard,Seed=1});
            var pieces=new List<ExpeditionWorldPiece>{new ExpeditionWorldPiece{kind=ExpeditionPieceKind.Chest,look=ExpeditionPieceLook.Chest,anchor="c",tone=ExpeditionTone.Muted,settled=true}};
            clock.Stamp(pieces,false);Check(pieces[0].lid>60&&pieces[0].flare>.5f,"the hologram's lid flares open");
            pieces[0]=new ExpeditionWorldPiece{kind=ExpeditionPieceKind.Chest,look=ExpeditionPieceLook.Chest,anchor="c"};clock.Stamp(pieces,true);Check(pieces[0].lid>60&&pieces[0].flare==0,"with Reduced effects only its lid");
            var a=new Points();var b=new Points();var shut=pieces[0];shut.lid=0;shut.flare=0;NativeExpeditionWorld.Emit(a,shut,0,false);NativeExpeditionWorld.Emit(b,pieces[0],0,false);
            Check(a.Valid&&b.Valid&&b.Top>a.Top+.3f,"the lid stands up over the crate as it opens");
            var locker=pieces[0];locker.locker=true;var c=new Points();NativeExpeditionWorld.Emit(c,locker,0,false);Check(c.Count==0,"no hologram crate where the steel locker stands");
        }
        private sealed class Points:IExpeditionWorldSink
        {
            public int Count;public float Top=-9;public bool Valid=true;
            public int Vertex(float x,float y,float z,float u,float v,float flat,ExpeditionTone tone,float level,float alpha){if(float.IsNaN(x+y+z)||level>1.01f||alpha>1||alpha<0)Valid=false;Top=Math.Max(Top,y);return Count++;}
            public void Triangle(int a,int b,int c){}
        }

        // 15. The DEV preview: the four lockers (all four tiers at any moment, each locker every tier over four cycles), the
        // hologram beside them; for sale with the click and the livery sweep, then the openings; buyer and watcher by turns.
        private static void Preview()
        {
            var items=new List<NativeCacheShow>();var clicks=new List<NativeCacheShow>();NativeCachePreview.Place(0,0,0,0,100);double s0=NativeCachePreview.Start;
            Check(s0==108&&s0%NativeCachePreview.Period==0,"the first cycle begins at the next whole period of the clock ("+s0+")");
            NativeCachePreview.Fill(items,clicks,100.5,100.4);
            Check(items.Count==5&&items.Take(4).All(s=>s.Locker)&&!items[4].Locker&&items.Take(4).Select(s=>s.Tier).Distinct().Count()==4&&items.All(s=>s.State==NativeCacheState.ForSale)&&clicks.Count==0,"four lockers of four tiers and the hologram, for sale until the first cycle");
            var seen=new HashSet<(int,NativeCacheTier)>();bool buyer=false,watcher=false;
            for(int c=0;c<4;c++){items.Clear();clicks.Clear();NativeCachePreview.Fill(items,clicks,s0+c*NativeCachePreview.Period+4,s0+c*NativeCachePreview.Period+3.9);for(int i=0;i<4;i++){seen.Add((i,items[i].Tier));Check(items[i].State==NativeCacheState.Opening,"opening");if(items[i].Buyer)buyer=true;else watcher=true;}}
            Check(seen.Count==16&&buyer&&watcher,"over four cycles each locker shows every tier, as buyer and watcher");
            items.Clear();clicks.Clear();NativeCachePreview.Fill(items,clicks,s0+NativeCachePreview.Period+.1,s0+NativeCachePreview.Period-.1);
            Check(clicks.Count==5&&items.Take(4).All(s=>!float.IsNaN(s.Sweep)&&s.SweepFrom!=s.Tier),"each cycle restocks with a click and sweeps each locker's livery to its next tier");
            items.Clear();NativeCachePreview.Fill(items,clicks,s0+2*NativeCachePreview.Period+NativeCachePreview.SaleFor+.4f,s0+2*NativeCachePreview.Period+NativeCachePreview.SaleFor+.3f);
            Check(items[0].Tier==(NativeCacheTier)(((int)(s0/NativeCachePreview.Period)+2)%4)&&items.Take(4).All(s=>s.Buyer==(((int)(s0/NativeCachePreview.Period)+2)%2==1)),"which tier opens when is known from the clock alone (captures)");
            NativeCachePreview.Start=double.NaN;
        }

        // 16. No allocation per frame: the clock, the frames and the J panel's words on a warmed set.
        private static void NoAllocation()
        {
            var run=Party();Check(run.Credit("p0","cash",50),"credit");
            Check(run.SetWorld(new[]{new ExpeditionWorldAnchor{id="c1",area="city",kind="chest",x=2},new ExpeditionWorldAnchor{id="c2",area="city",kind="chest",x=-2}},
                new[]{new ExpeditionChestOffer{id="c1",generation="g1",price=100},new ExpeditionChestOffer{id="c2",generation="h1",price=60,cooldown=30}}),"world");
            var v=NativeExpeditionView.Build(run,"p0");var clock=new NativeCacheClock();var f=new NativeCacheFrame();var rows=new List<NativeExpeditionPresentation.CacheRow>();var labels=new ExpeditionLabels();
            var openings=Tiers.Select(t=>O(t)).ToArray();var reduced=Tiers.Select(t=>O(t,true)).ToArray();
            void Pass(double now)
            {
                clock.Observe(v,now);NativeExpeditionPresentation.Caches(v,rows);
                foreach(var o in openings)o.Frame(f,(float)(now%2.6),true,true,1,1.2f,-.6f);foreach(var o in reduced)o.Frame(f,(float)(now%2.6),true,true,1,1.2f,-.6f);
                foreach(var t in Tiers)NativeCacheOpenFx.ForSale(f,t,(float)now,false,true);NativeCacheText.Restocking(labels,30);NativeCacheText.Detail(labels,100);
            }
            for(int i=0;i<3;i++)Pass(1+i*.016);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<120;i++)Pass(1.05+i*.016);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"the caches allocate nothing per frame ("+bytes+" bytes over 120 frames)");
        }

        // 17. Source scans: presentation only (no authority mutator, no randomness), the palette through the tones, the hook lines.
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            Check(R("Core/NativeExpeditionWorld.cs").Contains("if(p.actionable&&!p.locker)Column(sink,"),"the beacon's tube stands down where the steel locker stands (its own lamps and motes show it for sale; the hologram keeps the tube)");
            foreach(var f in new[]{"Core/NativeCacheTiers.cs","Core/NativeCacheOpenFx.cs","Core/NativeCacheClock.cs","Core/NativeCacheLockerModels.cs","Runtime/NativeCachePresentation.cs","Runtime/NativeCacheLive.cs"})
            {
                string src=R(f);
                Check(!Regex.IsMatch(src,@"\b(run|Run|session|Session|authority)\s*\.\s*(Purchase|RestockChest|SetWorld|Credit|Claim|Select|Spend)\("),f+" calls no authority mutator");
                Check(!src.Contains("System.Random")&&!src.Contains("UnityEngine.Random")&&!src.Contains("NativePveRandom"),f+" draws no randomness (seeded from the cache and generation)");
                Check(!Regex.IsMatch(src,@"\bExpeditionChestOffer\s*\{[^}]*\btier\b"),f+" adds no tier field (A2's contract)");
            }
            string live=R("Runtime/NativeCacheLive.cs"),draw=R("Runtime/NativeCachePresentation.cs");
            Check(!Regex.IsMatch(live+draw,@"0x[0-9a-fA-F]{6}"),"the runtime restates no colour (the tiers' colours are Core's)");
            Check(!draw.Contains("SlamWave")&&!draw.Contains("AddShake")&&!draw.Contains("Ring)")&&!draw.Contains("FlashMode"),"no hazard language: no slam wave, shake, ring or flash flipbook");
            Check(R("Runtime/NativeExpeditionWorldPresentation.cs").Contains("CacheWorldFrame()"),"the live caches ride the world pieces' frame");
            Check(R("Runtime/ArenaPresentation.cs").Contains("CacheFx(arena,camera);")&&R("Runtime/NativeSourcePresentation.cs").Contains("CacheLockers(arena);"),"the caches draw in the effects pass and the source models' pass");
            Check(R("Runtime/NativeMachineModels.cs").Contains("foreach(var (id,mesh) in NativeCacheLockerModels.All())Add(id,mesh);"),"the lockers are in the source-model bank");
            Check(R("Core/NativeDevTools.cs").Contains("new NativeDevAction(\"preview-caches\",Ca,V,")&&R("Runtime/NativeDevPanel.cs").Contains("case \"preview-caches\":CachePreviewToggle();"),"the DEV preview is a presentation-only DEV panel preview");
        }
    }
}
#endif
