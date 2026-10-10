#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The dev-siren entrance face (Core/NativeSirenSmileFace.cs, owner request 2026-10-10 "make the smile animation on intro
    // more realistic") against tools/export_siren_smile.mjs's run of tools/siren_smile.mjs, the verified source Siren's own
    // eyes and tools/siren_design.mjs's cheeks (Tests/siren-smile-face-parity.json); then its own rules: the cue and the
    // intro clock untouched, the timeline (the stare before the cue, the seam, the corners, the reveal, the beat-synced laugh,
    // the hold), the body layer safe to bake at 30 fps, finite and bounded pieces at every age, no allocation, the rest mouth
    // equal to the arena grin (the bank's teeth parts) and the arena look untouched, nothing over the eyes or the fangs, and
    // the entrance bake without the source's eyes and grin but with its head track.
    internal static class SirenSmileFaceChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Siren smile face: "+what);count++;}
        private static void Near(double a,double b,double tolerance,string what){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static double D(JsonElement e,string name)=>e.GetProperty(name).GetDouble();
        private static JsonDocument Bank(string root,string name)
        {
            using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/"+name+".json")));
            Check(envelope.RootElement.GetProperty("schema").GetInt32()==1&&envelope.RootElement.GetProperty("commit").GetString()=="05950eaef80ff2483a9c44d898cad98be1a76bbc",name+" envelope");
            using var zip=new GZipStream(new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString())),CompressionMode.Decompress);using var reader=new StreamReader(zip);
            return JsonDocument.Parse(reader.ReadToEnd());
        }
        private static double[] Values(JsonElement e){var a=new double[e.GetArrayLength()];int i=0;foreach(var v in e.EnumerateArray())a[i++]=v.GetDouble();return a;}
        // The unit fang's (u, v) outlines (upper row; the lower row's holds -v) and a corner mapped by a fang's transform (head-local x, y).
        private static readonly double[] fang=new double[NativeSirenSmileFace.Fang.Length],fangLow=new double[NativeSirenSmileFace.Fang.Length];
        private static void FangPoint(double[] m,int o,int v,out double x,out double y,bool upper=true){var f=upper?fang:fangLow;double u=f[v*2],w=f[v*2+1];x=m[o]*u+m[o+1]*w+m[o+3];y=m[o+4]*u+m[o+5]*w+m[o+7];}

        public static void Run(string root)
        {
            count=0;NativeSirenSmileFace.FangOutline(fang);NativeSirenSmileFace.FangOutline(fangLow,false);for(int i=0;i<fang.Length;i++)Check(fangLow[i]==(i%2==0?fang[i]:-fang[i]),"the lower fang mesh is the upper's with v negated");
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/siren-smile-face-parity.json")));var f=doc.RootElement;
            var pve=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PveContent.json"))).RootElement;var song=Values(pve.GetProperty("bossMusic").GetProperty("beats"));
            // 1. The cue and the clock: beat 14 of the boss song, the intro's own table, the contract's cue times; nothing moved.
            Check(NativeSirenIntroLight.SmileBeat==14&&NativeSirenIntroLight.BlackoutBeat==13&&f.GetProperty("beats").GetProperty("smile").GetInt32()==14&&f.GetProperty("beats").GetProperty("blackout").GetInt32()==13,"cue beats");
            for(int i=0;i<NativeSirenSmileFace.IntroBeats.Length;i++)Check(NativeSirenSmileFace.IntroBeats[i]==song[i],"intro beat "+i+" is the song's");
            var cues=f.GetProperty("cues");Check(D(cues,"smile")==6.0082&&D(cues,"blackout")==5.596&&NativeSirenSmileFace.Cue(song)==6.0082&&NativeSirenSmileFace.Cue(null)==6.0082&&NativeSirenSmileFace.Blackout(song)==5.596,"smile and blackout cue times");
            Near(NativeSirenSmileFace.Name,song[15]-song[14],1e-12,"name beat offset");Near(NativeSirenSmileFace.Beat16,song[16]-song[14],1e-12,"beat 16 offset");Near(NativeSirenSmileFace.End,song[17]-song[14],1e-12,"overlay end offset");
            var off=f.GetProperty("offsets");Check(D(off,"name")==NativeSirenSmileFace.Name&&D(off,"beat16")==NativeSirenSmileFace.Beat16&&D(off,"end")==NativeSirenSmileFace.End&&D(off,"hold")==NativeSirenSmileFace.HoldEnd,"offsets");
            Near(D(cues,"name")-D(cues,"smile"),NativeSirenSmileFace.Name,1e-12,"contract name cue");
            // 2. Constants: the closed seam, the rest mouth (the arena grin), the fangs, the eyes and halos.
            var c0=f.GetProperty("closed");Check(D(c0,"w")==NativeSirenSmileFace.ClosedW&&D(c0,"uy")==NativeSirenSmileFace.ClosedUy&&D(c0,"uk")==NativeSirenSmileFace.ClosedUk&&D(c0,"ly")==NativeSirenSmileFace.ClosedLy&&D(c0,"lk")==NativeSirenSmileFace.ClosedLk&&D(c0,"reach")==NativeSirenSmileFace.ClosedReach,"closed seam");
            var r0=f.GetProperty("rest");Check(D(r0,"w")==NativeSirenSmileFace.RestW&&D(r0,"uy")==NativeSirenSmileFace.RestUy&&D(r0,"uk")==NativeSirenSmileFace.RestUk&&D(r0,"ly")==NativeSirenSmileFace.RestLy&&D(r0,"lk")==NativeSirenSmileFace.RestLk,"rest mouth");
            var ru=Values(f.GetProperty("reach").GetProperty("upper"));var rl=Values(f.GetProperty("reach").GetProperty("lower"));Check(ru.Length==13&&rl.Length==12,"reach tables");
            for(int i=0;i<13;i++)Check(ru[i]==NativeSirenSmileFace.UpperReach[i],"upper reach "+i);for(int i=0;i<12;i++)Check(rl[i]==NativeSirenSmileFace.LowerReach[i],"lower reach "+i);
            var fg=f.GetProperty("fang");Check(D(fg,"inset")==NativeSirenSmileFace.FangInset,"fang inset");int fo=0;foreach(var p in fg.GetProperty("outline").EnumerateArray()){Check(p[0].GetDouble()==NativeSirenSmileFace.Fang[fo*2]&&p[1].GetDouble()==NativeSirenSmileFace.Fang[fo*2+1],"fang outline "+fo);fo++;}
            var shape=Values(fg.GetProperty("shape"));for(int i=0;i<shape.Length;i++)Near(fang[i],shape[i],1e-8,"fang shape "+i);int ff=0;foreach(var v in fg.GetProperty("fan").EnumerateArray())Check(v.GetInt32()==NativeSirenSmileFace.FangFan[ff++],"fang fan");
            var eye=f.GetProperty("eye");Check(D(eye,"x")==NativeSirenSmileFace.EyeX&&D(eye,"y")==NativeSirenSmileFace.EyeY&&D(eye,"z")==NativeSirenSmileFace.EyeZ&&D(eye,"tilt")==NativeSirenSmileFace.EyeTilt&&D(eye,"w")==NativeSirenSmileFace.EyeW&&D(eye,"h")==NativeSirenSmileFace.EyeH&&D(eye,"lift")==NativeSirenSmileFace.EyeLift,"eye constants");
            var halo=f.GetProperty("halo");Check(D(halo,"x")==NativeSirenSmileFace.HaloX&&D(halo,"y")==NativeSirenSmileFace.HaloY&&D(halo,"z")==NativeSirenSmileFace.HaloZ&&D(halo,"size")==NativeSirenSmileFace.HaloSize&&D(f,"mouthZ")==NativeSirenSmileFace.MouthZ,"halo and mouth constants");
            // 3. Every sample against the JS mirror (fangs), the source Siren (eyes, halos) and the design's dressed cheeks.
            var face=new NativeSirenSmileFace();double worst=0;int samples=0,stares=0,smiling=0;string where="";
            void Cmp(double a,double b,string what){double d=Math.Abs(a-b);if(d>worst){worst=d;where=what;}Check(d<=1e-6&&!double.IsNaN(a),what+": "+a+" != "+b);}
            var halos=new List<double[]>();foreach(var h in f.GetProperty("halos").EnumerateArray())halos.Add(Values(h));
            foreach(var s in f.GetProperty("samples").EnumerateArray())
            {
                double age=D(s,"age");face.Evaluate(age,song);string at="age "+age.ToString("0.0000");samples++;
                Cmp(face.S,D(s,"s"),at+" s");Check(face.Grin==s.GetProperty("grin").GetBoolean(),at+" grin shown");Check(face.Shape==s.GetProperty("shape").GetInt32(),at+" eye cut "+face.Shape);
                foreach(var (v,name) in new[]{(face.Vis,"vis"),(face.Glow,"glow"),(face.WPos,"wPos"),(face.WNeg,"wNeg"),(face.Uy,"uy"),(face.Uk,"uk"),(face.Ly,"ly"),(face.Lk,"lk"),(face.Tilt,"tilt"),(face.Open,"open"),(face.Anger,"anger"),(face.Squint,"squint"),(face.HaloOpacity,"halo"),(face.Cheek,"c"),(face.Chuckle,"chuckle"),(face.Strain,"strain"),(face.Seam,"seam"),(face.Filament,"filament")})Cmp(v,D(s,name),at+" "+name);
                var reach=Values(s.GetProperty("reach"));for(int i=0;i<NativeSirenSmileFace.Teeth;i++)Cmp(face.Reach[i],reach[i],at+" reach "+i);
                var body=Values(s.GetProperty("body"));double[] mine={face.HeadX,face.HeadY,face.HeadZ,face.NeckX,face.ChestX,face.Clavicle};for(int i=0;i<6;i++)Cmp(mine[i],body[i],at+" body "+i);
                int t=0;foreach(var row in s.GetProperty("teeth").EnumerateArray()){var v=Values(row);int o=t*16;int[] map={0,1,3,4,5,7};for(int k=0;k<6;k++)Cmp(face.Tooth[o+map[k]],v[k],at+" fang "+t+" ["+map[k]+"]");
                    Check(face.Tooth[o+2]==0&&face.Tooth[o+6]==0&&face.Tooth[o+8]==0&&face.Tooth[o+9]==0&&face.Tooth[o+10]==1&&face.Tooth[o+11]==NativeSirenSmileFace.MouthZ&&face.Tooth[o+15]==1,at+" fang "+t+" stays on the mouth plane");t++;}
                Check(t==NativeSirenSmileFace.Teeth,"fangs per sample");
                int e=0;foreach(var row in s.GetProperty("eyes").EnumerateArray()){var v=Values(row);for(int k=0;k<12;k++)Cmp(face.Eye[e*16+k],v[k],at+" eye "+e+" ["+k+"]");e++;}
                for(int h=0;h<2;h++)for(int k=0;k<12;k++)Cmp(face.Halo[h*16+k],halos[h][k],at+" halo "+h+" ["+k+"]");
                int c=0;foreach(var row in s.GetProperty("cheeks").EnumerateArray()){var v=Values(row);for(int k=0;k<12;k++)Cmp(face.Cheeks[c*16+k],v[k],at+" cheek "+c+" ["+k+"]");c++;}
                Check(c==NativeSirenSmileFace.CheekPieces,"cheeks per sample");if(face.S<0)stares++;else smiling++;
            }
            Check(samples==195&&stares>=8&&smiling>=180,"samples "+samples+" ("+stares+" before the cue)");
            // 4. The timeline. Before the cue: the blank oval stare, jaw shut, cheeks down, no grin; the halos .35 lit, .6 dark.
            foreach(double age in new[]{3.0,4.4,5.5,5.597,5.8,6.0})
            {
                face.Evaluate(age,song);Check(!face.Grin&&face.Vis==0&&face.Glow==0&&face.Shape==0&&face.Anger==0&&face.Squint==0&&face.Open==0&&face.Cheek==0,"stare at "+age);
                Check(face.HeadX==0&&face.HeadY==0&&face.HeadZ==0&&face.NeckX==0&&face.ChestX==0&&face.Clavicle==0,"no body layer before the cue ("+age+")");
                Check(face.HaloOpacity==(age<5.596?.35:.6),"halo before the cue ("+age+")");Near(face.HaloAlpha,NativeEntranceLight.ToLinear(face.HaloOpacity),1e-12,"linear halo alpha");
                Check(face.Seam==(age<5.596?1:.08)&&(age<5.596?face.Filament==1:Math.Abs(face.Filament-.14)<1e-9),"couture glow before the cue ("+age+")");
            }
            double At(double s)=>NativeSirenSmileFace.Cue(song)+s;
            // The seam flickers in (.65 at 20 ms, .25 at 35 ms, full at 60 ms) at 71% width, the Siren's left corner leading.
            face.Evaluate(At(.02),song);Near(face.Vis,.65,1e-9,"seam at 20 ms");Check(face.Grin,"grin from the cue");face.Evaluate(At(.035),song);Near(face.Vis,.25,1e-9,"seam dip at 35 ms");face.Evaluate(At(.06),song);Near(face.Vis,1,1e-12,"seam full at 60 ms");
            face.Evaluate(At(.001),song);Near(face.WPos,.104,1e-9,"closed width (left)");Near(face.WNeg,.104,1e-12,"closed width (right)");Near(face.WPos/NativeSirenSmileFace.RestW,.71,.005,"the seam at 71% width");Near(face.Uk,1.3,1e-12,"closed curve");
            for(double s=.06;s<.5;s+=.02){face.Evaluate(At(s),song);Check(face.WPos>face.WNeg,"the left corner leads at "+s);}
            // Key values (the design table; open and the head pitch near .46 s carry the chuckle).
            double[,] key={{.06,.110,.104,.053,1.30,0,0,0,-.026,0},{.24,.140,.129,.075,2.332,.091,.199,.219,.025,.008},{.46,.149,.148,.080,2.599,.275,.897,.480,.141,.041},{1.26,.148,.146,.080,2.612,.128,.900,.400,.088,.081},{3.06,.153,.153,.080,2.850,.108,.900,.400,.080,.110}};
            for(int k=0;k<5;k++)
            {
                face.Evaluate(At(key[k,0]),song);string w="key "+key[k,0];double[] got={face.WPos,face.WNeg,face.Uy,face.Uk,face.Open,face.Anger,face.Squint,face.HeadX,face.HeadZ};
                for(int q=0;q<got.Length;q++)Near(got[q],key[k,q+1],q==0&&k==4?.003:.0025,w+" channel "+q);
            }
            // The reveal from the centre out; the lower row later; every fang fully bared by .8 s; the hold's creep.
            face.Evaluate(At(.3),song);for(int i=0;i<13;i++)Check(face.Reach[6]>=face.Reach[i],"upper centre leads the reveal ("+i+")");Check(face.Reach[6]>face.Reach[13+5],"the lower row follows");
            face.Evaluate(At(.8),song);for(int i=0;i<25;i++)Near(face.Reach[i],1,1e-12,"bared by .8 s ("+i+")");
            face.Evaluate(At(3.0596),song);Check(face.WPos>.152&&face.WNeg>.152&&face.Uk>2.84,"the held grin creeps wider");
            // The silent laugh lands on beats 15 and 16: the jaw, the cheeks and the head peak right after each beat; the fangs
            // and the halos flare on the name beat.
            foreach(double beat in new[]{NativeSirenSmileFace.Name,NativeSirenSmileFace.Beat16})
            {
                double best=-1,peak=0;for(double s=beat-.05;s<beat+.25;s+=.001){double ch=NativeSirenSmileFace.Laugh(s);if(ch>peak){peak=ch;best=s;}}
                Check(best>beat&&best<beat+.08&&peak>.7,"chuckle peak after beat at "+beat+": "+best+" ("+peak+")");Check(NativeSirenSmileFace.Laugh(beat-.001)<1e-9,"no laugh just before beat "+beat);
            }
            face.Evaluate(At(NativeSirenSmileFace.Name+.03),song);Check(face.Glow>1.25&&face.HaloOpacity>.76,"the fangs and halos flare on the name beat ("+face.Glow+", "+face.HaloOpacity+")");
            face.Evaluate(At(NativeSirenSmileFace.Name-.01),song);Check(face.Glow<1.12,"no flare before the name beat");
            // 5. The body layer is safe to bake at 30 fps: every channel interpolated between 30 Hz samples stays within 10 mrad
            // (about half a degree; the largest gap is the laugh's sharp attack on the beat) of its 1 kHz curve, with no 7.5 Hz
            // shudder to alias, and the angles stay small.
            double bakeErr=0,maxHead=0;var a30=new double[6];var b30=new double[6];var now=new double[6];
            for(int i=0;i<=3060;i++)
            {
                double s=i/1000.0,fr=s*30;int k0=(int)Math.Floor(fr);double bl=fr-k0;
                face.Evaluate(At(k0/30.0),song);a30[0]=face.HeadX;a30[1]=face.HeadY;a30[2]=face.HeadZ;a30[3]=face.NeckX;a30[4]=face.ChestX;a30[5]=face.Clavicle;
                face.Evaluate(At((k0+1)/30.0),song);b30[0]=face.HeadX;b30[1]=face.HeadY;b30[2]=face.HeadZ;b30[3]=face.NeckX;b30[4]=face.ChestX;b30[5]=face.Clavicle;
                face.Evaluate(At(s),song);now[0]=face.HeadX;now[1]=face.HeadY;now[2]=face.HeadZ;now[3]=face.NeckX;now[4]=face.ChestX;now[5]=face.Clavicle;
                for(int q=0;q<6;q++){bakeErr=Math.Max(bakeErr,Math.Abs(a30[q]+(b30[q]-a30[q])*bl-now[q]));maxHead=Math.Max(maxHead,Math.Abs(now[q]));}
            }
            Check(bakeErr<.01,"the body layer bakes at 30 fps within "+bakeErr+" rad");Check(maxHead<.2,"body angles stay small ("+maxHead+")");
            // 6. A sweep over the whole entrance (and past it): finite, bounded, every fang on its row's side of the mouth.
            var tooth=new double[16];double minX=1,maxX=-1,minY=1,maxY=-1;int frames=0;
            for(int i=0;i<=2400;i++)
            {
                double age=i*10.0/2400;foreach(var beats in new[]{song,null})
                {
                    face.Evaluate(age,beats);frames++;
                    foreach(var arr in new[]{face.Tooth,face.Eye,face.Halo,face.Cheeks})foreach(var v in arr)Check(!double.IsNaN(v)&&!double.IsInfinity(v),"finite pieces at "+age);
                    Check(face.Glow>=0&&face.Glow<1.5&&face.HaloOpacity>=.35&&face.HaloOpacity<.86&&face.Cheek>=0&&face.Cheek<=1.15&&face.Open>=0&&face.Open<.3&&face.Squint>=0&&face.Squint<=.5&&face.Anger>=0&&face.Anger<=.9,"channels bounded at "+age);
                    for(int e=0;e<2;e++)Check(face.Eye[e*16]>0&&face.Eye[e*16+5]>.06,"eyes keep their size at "+age);
                    if(!face.Grin)continue;
                    for(int t=0;t<NativeSirenSmileFace.Teeth;t++)
                    {
                        double det=face.FangDeterminant(t);Check(det>1e-7,"fang "+t+" is neither mirrored nor degenerate at "+age+" ("+det+")");
                        for(int v=0;v<7;v++){FangPoint(face.Tooth,t*16,v,out double x,out double y,t<NativeSirenSmileFace.UpperTeeth);minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
                    }
                }
            }
            Check(minX>-.19&&maxX<.19&&minY>-.06&&maxY<.17,"fangs stay in the mouth: x "+minX+".."+maxX+", y "+minY+".."+maxY);
            // 7. No allocation per frame.
            face.Evaluate(At(.5),song);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)face.Evaluate(At(i*.003),song);long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(allocated==0,"Evaluate allocates "+allocated+" bytes over 1000 frames");
            // 8. The rest mouth is the arena grin: the bank's siren-teeth parts, fang for fang; the arena look keeps its grin rows,
            // and the entrance's cheeks are the arena's at the same lift.
            var rest=new NativeSirenSmileFace{WPos=NativeSirenSmileFace.RestW,WNeg=NativeSirenSmileFace.RestW,Uy=NativeSirenSmileFace.RestUy,Uk=NativeSirenSmileFace.RestUk,Ly=NativeSirenSmileFace.RestLy,Lk=NativeSirenSmileFace.RestLk,Tilt=0,Open=0};
            double restErr=0;
            using(var models=Bank(root,"SourceModels"))
            {
                JsonElement siren=default,stage=default;foreach(var m in models.RootElement.GetProperty("models").EnumerateArray()){string id=m.GetProperty("id").GetString();if(id=="siren")siren=m;else if(id=="stage-dev-siren")stage=m;}
                var named=new Dictionary<string,JsonElement>();foreach(var sec in siren.GetProperty("sections").EnumerateArray()){string part=sec.GetProperty("part").GetString();if(!string.IsNullOrEmpty(part))named[part]=sec;}
                foreach(var (part,upper) in new[]{("siren-teeth-upper",true),("siren-teeth-lower",false)})
                {
                    var sec=named[part];var pos=sec.GetProperty("positions");var idx=sec.GetProperty("indices");int n=upper?13:12,q=0;
                    Check(idx.GetArrayLength()==n*15,part+" holds "+n+" fangs");
                    for(int i=0;i<n;i++)
                    {
                        rest.ToothMatrix(upper,i,1,tooth,0);Check(tooth[11]==NativeSirenSmileFace.MouthZ,"rest fang on the mouth plane");
                        for(int tr=0;tr<15;tr+=3)
                        {
                            int a=NativeSirenSmileFace.FangFan[tr],b=NativeSirenSmileFace.FangFan[tr+1],c=NativeSirenSmileFace.FangFan[tr+2];
                            FangPoint(tooth,0,a,out double ax,out double ay,upper);FangPoint(tooth,0,b,out double bx,out double by,upper);FangPoint(tooth,0,c,out double cx,out double cy,upper);
                            int[] order=(bx-ax)*(cy-ay)-(cx-ax)*(by-ay)>=0?new[]{a,b,c}:new[]{a,c,b};
                            for(int k=0;k<3;k++){FangPoint(tooth,0,order[k],out double x,out double y,upper);int v=idx[q+(k==0?0:k==1?2:1)].GetInt32();restErr=Math.Max(restErr,Math.Max(Math.Abs(-pos[v*3].GetDouble()-x),Math.Abs(pos[v*3+1].GetDouble()-y)));}
                            q+=3;
                        }
                    }
                }
                Check(restErr<2e-6,"the rest mouth is the bank's grin (off by "+restErr+")");
                // The entrance bake: the source's eyes (a 40-triangle fan) and grin rows are gone from the stage; the head track is recorded.
                int eyes=0,teeth=0;foreach(var sec in stage.GetProperty("sections").EnumerateArray())
                {
                    var col=sec.GetProperty("colors");var em=sec.GetProperty("emission");int verts=sec.GetProperty("positions").GetArrayLength()/3;bool glow=Math.Abs(col[0].GetDouble()-em[0].GetDouble())+Math.Abs(col[1].GetDouble()-em[1].GetDouble())+Math.Abs(col[2].GetDouble()-em[2].GetDouble())<1e-4;
                    if(glow&&verts==120&&col[0].GetDouble()>.99)eyes++;if(glow&&Math.Abs(col[0].GetDouble()-.896)<.01&&Math.Abs(col[1].GetDouble()-.871)<.01)teeth++;
                }
                Check(eyes==0&&teeth==0,"the entrance bake still holds the source face ("+eyes+" eyes, "+teeth+" grin rows)");
            }
            var look=new double[NativeSirenLook.Count*16];var shown=new bool[NativeSirenLook.Count];NativeSirenLook.Rest(look,shown);
            foreach(double c in new[]{0,.25,.6,1.0})
            {
                var st=NativeSirenLook.Neutral(2);NativeSirenLook.Evaluate(st,look,shown);NativeSirenDetail.Evaluate(st,look,shown,NativeSirenLook.Detail,NativeSirenLook.Claw,c);var cheeks=new double[64];NativeSirenDetail.Cheeks(c,cheeks,0);
                for(int k=0;k<4;k++)for(int q=0;q<16;q++)Near(cheeks[k*16+q],look[(NativeSirenLook.Detail+NativeSirenDetail.CheekPiece(k))*16+q],1e-12,"entrance cheek "+k+" at lift "+c+" is the arena's");
            }
            {
                var st=NativeSirenLook.Neutral(1);NativeSirenLook.Evaluate(st,look,shown);
                foreach(int piece in new[]{NativeSirenLook.GrinUpper,NativeSirenLook.GrinLower}){int o=piece*16;Check(look[o]==1&&look[o+5]==1&&look[o+10]==1&&look[o+3]==0&&look[o+7]==0&&Math.Abs(look[o+11]-.1638)<1e-15,"arena grin row "+piece+" untouched");}
                Check(NativeSirenDetail.Group(NativeSirenDetail.CheekPiece(0))=="cheek"&&NativeSirenDetail.Id(NativeSirenDetail.CheekPiece(0))=="cheek-box-l"&&NativeSirenDetail.Id(NativeSirenDetail.CheekPiece(3))=="cheek-trim-r","cheek piece order");
            }
            // 9. Nothing over the eyes or the fangs through the smile: the mask (static head pieces in front of the glow plane)
            // and the cheeks at the smile's lift, against the eyes (their cut, squint and lift) and the 25 fangs, at 30 Hz.
            var worstCover=new Dictionary<string,double>();var xy=new double[NativeSirenLook.EyeSegments*6];double eyeCover=0,fangCover=0;int checkedFrames=0;
            NativeSirenLook.Rest(look,shown);var mask=new List<SirenDetailChecks.Footprint>();
            for(int k=0;k<NativeSirenDetail.Count;k++)if(NativeSirenDetail.Bone(k)==5&&NativeSirenDetail.Group(k)!="nimbus"&&NativeSirenDetail.Group(k)!="cheek"&&!NativeSirenDetail.Moves(k)&&NativeSirenDetail.Tone(k)!=NativeSirenDetail.Sprite)mask.Add(SirenDetailChecks.Hull(NativeSirenDetail.Id(k),look,(NativeSirenLook.Detail+k)*16));
            for(int i=-3;i<=92;i++)
            {
                face.Evaluate(At(i/30.0),song);checkedFrames++;var boxes=new List<SirenDetailChecks.Footprint>(mask);for(int k=0;k<4;k++)boxes.Add(SirenDetailChecks.Hull(NativeSirenDetail.Id(NativeSirenDetail.CheekPiece(k)),face.Cheeks,k*16));
                for(int e=0;e<2;e++)
                {
                    NativeSirenLook.EyeOutline(e==0?1:-1,face.Shape/40.0,xy);
                    for(int q=0;q<NativeSirenLook.EyeSegments;q++)
                    {
                        SirenDetailChecks.Apply(face.Eye,e*16,xy[q*6],xy[q*6+1],0,out double ax,out double ay,out _);SirenDetailChecks.Apply(face.Eye,e*16,xy[q*6+2],xy[q*6+3],0,out double bx,out double by,out _);SirenDetailChecks.Apply(face.Eye,e*16,xy[q*6+4],xy[q*6+5],0,out double cx,out double cy,out _);
                        eyeCover+=SirenDetailChecks.Covered(ax,ay,bx,by,cx,cy,boxes,worstCover,"eye");
                    }
                }
                if(!face.Grin)continue;
                for(int t=0;t<NativeSirenSmileFace.Teeth;t++)for(int tr=0;tr<15;tr+=3)
                {
                    bool up=t<NativeSirenSmileFace.UpperTeeth;FangPoint(face.Tooth,t*16,NativeSirenSmileFace.FangFan[tr],out double ax,out double ay,up);FangPoint(face.Tooth,t*16,NativeSirenSmileFace.FangFan[tr+1],out double bx,out double by,up);FangPoint(face.Tooth,t*16,NativeSirenSmileFace.FangFan[tr+2],out double cx,out double cy,up);
                    fangCover+=SirenDetailChecks.Covered(ax,ay,bx,by,cx,cy,boxes,worstCover,"fang "+t);
                }
            }
            Check(eyeCover==0&&fangCover==0,"a face piece covers the smile (eyes "+eyeCover+" mm², fangs "+fangCover+" mm²): "+string.Join(", ",worstCover.Keys));
            // 10. The runtime draws it on the bake's head track; the exporter hides the source face and bakes the body layer.
            string intros=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeIntros.cs")),draw=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeSirenFace.cs")),cine=File.ReadAllText(Path.Combine(root,"tools/export_cinematics.mjs")),scene=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeSceneMotion.cs"));
            Check(intros.Contains("if(style==\"dev-siren\")SirenEntranceFace(clip,age,camera);")&&draw.Contains("NativeSceneMotion.Matrix(clip,head,age)")&&draw.Contains("sirenFace.Set(age,introBeats)")&&draw.Contains("IntroHalos(sirenFace,at,camera,0,camera)"),"the entrance draws the native face on the head track at the entrance age");
            Check(cine.Contains("SirenSmile.stagePose(stage.rig,siren,t,sirenCues)")&&cine.Contains("siren?stage.rig.head:null")&&scene.Contains("public int head=-1;"),"the exporter hides the source face, bakes the body layer and records the head track");
            using(var scenes=Bank(root,"SceneMotion"))
            {
                JsonElement clip=default;foreach(var c in scenes.RootElement.GetProperty("clips").EnumerateArray())if(c.GetProperty("key").GetString()=="stage-dev-siren")clip=c;
                int frames2=clip.GetProperty("frames").GetInt32(),tracks=clip.GetProperty("tracks").GetInt32(),head=clip.GetProperty("head").GetInt32();double duration=clip.GetProperty("duration").GetDouble();var vals=clip.GetProperty("values");
                Check(duration==9.0678&&frames2==274,"the entrance's duration and frame count unchanged");Near(duration-NativeSirenSmileFace.Cue(song),NativeSirenSmileFace.HoldEnd,1e-9,"the stage hold ends at the clip's end");
                Check(head>=1&&head<tracks,"head track "+head);
                double Opacity(double age){int fr=Math.Min(frames2-1,(int)(Math.Clamp(age/duration,0,1)*(frames2-1)));return vals[(head*frames2+fr)*20+16].GetDouble();}
                Check(Opacity(1)==0&&Opacity(2.7)==1&&Opacity(3.2)==0&&Opacity(4)==1&&Opacity(6.5)==1&&Opacity(9)==1,"the head track shows the Siren when the bake does (glimpse, reveal, smile)");
            }
            Console.WriteLine($"PASS Siren smile face: {samples} samples match the JS mirror, the source eyes and the dressed cheeks (largest gap {worst:0.0e+0} at {where}); cue at beat 14 ({NativeSirenSmileFace.Cue(song)} s) and the clock untouched; stare, seam, corners, reveal, laugh on beats 15/16 and hold as designed; body bakes at 30 fps within {bakeErr*1000:0.0} mrad; {frames} sweep frames finite and in the mouth; no allocation; rest mouth = bank grin ({restErr:0.0e+0}); {checkedFrames} frames with eyes and fangs clear; bake without the source face, head track recorded; {count} checks");
        }
    }
}
#endif
