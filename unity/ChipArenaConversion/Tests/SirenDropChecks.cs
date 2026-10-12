using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ChipArena.UnityConversion;

// The Last Laugh crush drop (Core/NativeSirenDrop, Runtime/NativeSirenDropPresentation): on the real baked
// death-siren-crush clip, the ragdoll is handed exactly the pose drawn at the release, finite and continuous with the
// frame before, with bounded motion; the gibs land on the arena floor; the timeline before the release is untouched.
// The PhysX fall itself is not run here (Unity only).
internal static class SirenDropChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string message){if(!ok)throw new Exception("Siren drop: "+message);}
        var env=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SceneMotion.json"))).RootElement;
        using var gz=new GZipStream(new MemoryStream(Convert.FromBase64String(env.GetProperty("data").GetString())),CompressionMode.Decompress);using var ms=new MemoryStream();gz.CopyTo(ms);
        JsonElement clip=default;bool found=false;
        foreach(var c in JsonDocument.Parse(Encoding.UTF8.GetString(ms.ToArray())).RootElement.GetProperty("clips").EnumerateArray())if(c.GetProperty("key").GetString()=="death-siren-crush"){clip=c.Clone();found=true;}
        Check(found,"no death-siren-crush clip");
        int frames=clip.GetProperty("frames").GetInt32(),tracks=clip.GetProperty("tracks").GetInt32();float duration=clip.GetProperty("duration").GetSingle();
        var values=new float[frames*tracks*20];int n=0;foreach(var v in clip.GetProperty("values").EnumerateArray())values[n++]=v.GetSingle();Check(n==values.Length,"clip size");
        var victim=new int[22];n=0;foreach(var v in clip.GetProperty("victim").EnumerateArray())victim[n++]=v.GetInt32();Check(n==22,"victim tracks");
        var voxel=new bool[tracks];var isVictim=new bool[tracks];for(int t=0;t<tracks;t++)voxel[t]=NativeIntroRules.TintVaries(values,frames,t);foreach(int t in victim)isVictim[t]=true;

        // Timing: the source's limp hang while held, one beat otherwise; both after the clip's .08 s carry, so the pose handed
        // over is the clip's authored hang (never its first-frame rest pose or the client-side carry blend).
        Check(NativeSirenDrop.Release(true)==.45f&&NativeSirenDrop.Release(false)==.1f&&NativeSirenDrop.Limp>NativeSirenDrop.Carry,"release times");
        Check(NativeSirenDrop.Keep==2.6f&&NativeSirenDrop.Carry==.08f&&NativeSirenDrop.Gone(3.2f)==0&&NativeSirenDrop.Gone(4.4f)==1&&Math.Abs(NativeSirenDrop.Gone(3.8f)-.5f)<1e-5f,"kills.ts keep / carry / come-apart timeline");
        // kills.ts burnRig rate.
        Check(NativeSirenDrop.BurnRate(1,1,false)==70&&NativeSirenDrop.BurnRate(1,.3,false)==30&&Math.Abs(NativeSirenDrop.BurnRate(2.6f,1,true)-21)<1e-9&&Math.Abs(NativeSirenDrop.BurnRate(3.8f,1,false)-(70*.5+120*.5))<1e-3&&NativeSirenDrop.BurnRate(4.4f,1,false)==0,"burn rate");

        var bind=new float[22*16];var lin=new float[66];var ang=new float[66];var again=new float[22*16];var lin2=new float[66];var ang2=new float[66];var rootM=new float[16];var m=new float[16];var w=new float[16];
        int cases=0;float worstStep=0,worstTurn=0;
        foreach(var (x,y,z,yaw,seed) in new[]{(0f,1.815f,1f,180f,1234),(3.5f,1.815f,-2f,90f,77),(-6f,2.29f,4f,-37f,900001),(10f,1.815f,10f,0f,5)})
        foreach(float release in new[]{NativeSirenDrop.Limp,NativeSirenDrop.Hang})
        {
            NativeSirenDrop.Root(x,y,z,yaw,rootM);NativeSirenDrop.Handoff(values,frames,duration,victim,voxel,rootM,release,seed,bind,lin,ang);
            NativeSirenDrop.Handoff(values,frames,duration,victim,voxel,rootM,release,seed,again,lin2,ang2);
            for(int i=0;i<bind.Length;i++)Check(bind[i]==again[i],"handoff not deterministic");for(int i=0;i<66;i++)Check(lin[i]==lin2[i]&&ang[i]==ang2[i],"handoff motion not deterministic");
            for(int b=0;b<22;b++)
            {
                int o=b*16;for(int k=0;k<16;k++)Check(MathUtil.Finite(bind[o+k]),"nonfinite handed-over bone");
                for(int c=0;c<3;c++){float l=(float)Math.Sqrt(bind[o+c*4]*bind[o+c*4]+bind[o+c*4+1]*bind[o+c*4+1]+bind[o+c*4+2]*bind[o+c*4+2]);Check(Math.Abs(l-1)<3e-3f,"handed-over bone scaled "+b);}
                // Exactly what is drawn at the release: root · clip(release) (the carry has ended).
                NativeSirenDrop.Sample(values,frames,victim[b],duration,release,voxel[victim[b]],m);NativeSirenDrop.Mul(rootM,0,m,0,w,0);for(int k=0;k<16;k++)Check(Math.Abs(w[k]-bind[o+k])<1e-6f,"handoff differs from the drawn pose, bone "+b);
                // Continuous with the last frame drawn before it (60 fps).
                NativeSirenDrop.Sample(values,frames,victim[b],duration,release-1/60f,voxel[victim[b]],m);NativeSirenDrop.Mul(rootM,0,m,0,w,0);
                float d=(float)Math.Sqrt((w[12]-bind[o+12])*(w[12]-bind[o+12])+(w[13]-bind[o+13])*(w[13]-bind[o+13])+(w[14]-bind[o+14])*(w[14]-bind[o+14]));
                NativeSirenDrop.Turn(w,0,bind,o,out float tx,out float ty,out float tz);float turn=(float)Math.Sqrt(tx*tx+ty*ty+tz*tz);worstStep=Math.Max(worstStep,d);worstTurn=Math.Max(worstTurn,turn);
                Check(d<.02f&&turn<.05f,"pop at the handoff, bone "+b+": "+d+" m, "+turn+" rad");
                float sp=(float)Math.Sqrt(lin[b*3]*lin[b*3]+lin[b*3+1]*lin[b*3+1]+lin[b*3+2]*lin[b*3+2]),sw=(float)Math.Sqrt(ang[b*3]*ang[b*3]+ang[b*3+1]*ang[b*3+1]+ang[b*3+2]*ang[b*3+2]);
                Check(MathUtil.Finite(sp)&&MathUtil.Finite(sw)&&sp<(float)NativeKillEffectRules.MaxBodySpeed&&sp<4&&sw<4,"handed-over motion out of range, bone "+b+": "+sp+" m/s, "+sw+" rad/s");
            }
            // The tumble tips the head back, away from the face (hard enough that the body leans back when its feet land), and
            // pushes the body back a little.
            var spin=new float[3];var push=new float[3];NativeSirenDrop.Tumble(seed,rootM,spin,push);float fx=rootM[8],fz=rootM[10];
            Check(spin[0]*-fz+spin[2]*fx>=.8f*NativeSirenDrop.Tip-1e-4f&&push[0]*fx+push[2]*fz<0&&push[1]==0,"tumble direction");
            // The knees give: only the shins and feet turn, about each knee's own hinge (x) axis in its bend direction, pivoting on the
            // knee, at the seeded rate; the rest of the handed-over motion is untouched.
            var gl=new float[66];var ga=new float[66];NativeSirenDrop.Give(seed,bind,gl,ga);
            for(int b=0;b<22;b++)
            {
                bool shin=b>=CapturedRig.LKnee&&b<=CapturedRig.LKnee+2||b>=CapturedRig.RKnee&&b<=CapturedRig.RKnee+2;
                float a=(float)Math.Sqrt(ga[b*3]*ga[b*3]+ga[b*3+1]*ga[b*3+1]+ga[b*3+2]*ga[b*3+2]),l=(float)Math.Sqrt(gl[b*3]*gl[b*3]+gl[b*3+1]*gl[b*3+1]+gl[b*3+2]*gl[b*3+2]);
                if(!shin){Check(a==0&&l==0,"knee give moved bone "+b);continue;}
                int knee=b<=CapturedRig.LKnee+2?CapturedRig.LKnee:CapturedRig.RKnee,k=knee*16;
                float along=(ga[b*3]*bind[k]+ga[b*3+1]*bind[k+1]+ga[b*3+2]*bind[k+2])/(float)Math.Sqrt(bind[k]*bind[k]+bind[k+1]*bind[k+1]+bind[k+2]*bind[k+2]);
                Check(along>=.7f*NativeSirenDrop.KneeGive-1e-4f&&along<=1.3f*NativeSirenDrop.KneeGive+1e-4f&&Math.Abs(along-a)<1e-4f,"knee give not a bend about the knee hinge, bone "+b+": "+along+" of "+a);
                if(b==knee)Check(l<1e-6f,"knee give moved the knee joint");
            }
            // Where the body ends: it is dropped from the claw (crush clip root = death position) onto the floor .95 m below the
            // clip's floor; with the cosmetic 18 m/s² gravity it lands and has a second to settle well before a kept body is held
            // (2.6 s) or the body comes apart (3.2 s).
            float lowest=float.PositiveInfinity;for(int b=0;b<22;b++)if(b!=CapturedRig.Head)lowest=Math.Min(lowest,bind[b*16+13]);float floor=y-.865f-.95f;
            Check(lowest>floor,"handed-over body below the floor");float settled=release+NativeSirenDrop.FallSeconds(lowest-floor)+1;
            Check(settled<NativeSirenDrop.Keep&&settled<NativeSirenDrop.ApartAt,"body cannot land and settle before it is kept or comes apart");
            cases++;
        }

        // Gibs: the 36 head chunks, exactly the source flight (12 m/s², floor .04, .6 slide), now landing on the arena floor.
        var launch=new float[tracks*6];var one=new float[6];int gibs=0;
        for(int t=0;t<tracks;t++)
        {
            if(!NativeSirenDrop.Gib(values,frames,t,duration,voxel[t],isVictim[t],one))continue;gibs++;Array.Copy(one,0,launch,t*6,6);
            for(int f=0;f<frames;f++)
            {
                int i=(t*frames+f)*20;if(!(values[i+16]>.001f))continue;float age=f*duration/(frames-1);
                NativeSirenDrop.GibAt(launch,t*6,age,0,out float fx,out float fy,out float fz);Check(Math.Abs(fx-values[i+12])<.02f&&Math.Abs(fy-values[i+13])<.02f&&Math.Abs(fz-values[i+14])<.02f,"gib flight differs from the clip, track "+t+" frame "+f);
                NativeSirenDrop.GibAt(launch,t*6,age,.95f,out _,out float low,out _);Check(low>=.04f-.95f-1e-5f,"gib below the arena floor");
            }
            NativeSirenDrop.GibAt(launch,t*6,4.5f,.95f,out _,out float rest,out _);Check(Math.Abs(rest-(.04f-.95f))<1e-5f,"gib did not reach the arena floor");
        }
        Check(gibs==36,"kills.ts throws 36 head gibs at full quality (found "+gibs+")");

        // Flame: before the release every baked cube shows as before; after it, only the tongues already burning play out.
        var births=NativeSirenDrop.Births(values,frames,tracks,voxel);int kept=0,cut=0,flames=0;
        foreach(float release in new[]{NativeSirenDrop.Limp,NativeSirenDrop.Hang})
        {
            int fr=NativeSirenDrop.Frame(frames,duration,release);
            for(int t=0;t<tracks;t++)
            {
                if(!voxel[t])continue;if(release==NativeSirenDrop.Limp)flames++;
                for(int f=0;f<frames;f++)
                {
                    float age=f*duration/(frames-1)+1e-4f;bool shown=NativeSirenDrop.FlameShown(births,frames,duration,t,age,release);int born=births[t*frames+f];
                    if(age<release)Check(shown,"flame hidden before the release");
                    else if(born>=0&&born<=fr){Check(shown,"burning tongue cut at the release");kept++;}
                    else if(born>fr){Check(!shown,"later flame left on the clip's scripted body");cut++;}
                }
                int at=births[t*frames+fr];Check(at<=fr,"birth after its frame");
            }
        }
        Check(flames==420&&kept>0&&cut>0,"flame pool split ("+flames+" slots, "+kept+" kept, "+cut+" cut)");

        // Authority and hooks: the grab/crush/release timing is untouched; the presentation routes the crush death through the
        // drop, the PvE repair rebuilds from it, and the drop ignores earlier non-fatal wounds.
        string Text(string p)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",p));
        Check(Text("NativeSiren.cs").Contains("m.CrushAt=Time+3.2f;m.Until=Time+3.8f"),"Last Laugh crush/release timing changed");
        var death=Text("NativeDeathPresentation.cs");Check(death.Contains("SirenDrop(arena,death,age,up)||!remains&&SourceDeathScene(death,age,up)")&&death.Contains("var wound=drop?null:death.wound;")&&death.Contains("if(drop&&b==CapturedRig.Head)continue;"),"death presentation hook");
        Check(Text("NativeInteractionPresentation.cs").Contains("SirenDropPose(WeaponLoadout,d,age,r.Body,r.Missing)"),"PvE repair hook");
        var drop=Text("NativeSirenDropPresentation.cs");Check(drop.Contains("NativeSirenDrop.Handoff(")&&drop.Contains("NativeSirenDrop.FlameShown(")&&drop.Contains("NativeSirenDrop.GibAt(")&&drop.Contains("NativeSirenDrop.Burn("),"drop presentation uses the Core rules");
        // The weapon falls out of the hand at the release (kills.ts keeps it in the victim's hand) and a kept body is never left to
        // the old clip by the corpse cap, so the repair rebuilds from the body that is drawn.
        Check(death.Contains("if(drop){weapon.linearVelocity=linear[hand];")&&drop.Contains("body.LooseModel!=null&&body.PresentedWeapon is Matrix4x4 loose"),"dropped weapon");
        Check(drop.Contains("corpses.Count>=32&&!death.kept"),"kept crush body capped away from its ragdoll");
        Check(drop.Contains("&&!NativeCorpseRules.Retain(PilotOf(arena,death.id)!=null,arena.Match.Cooperative)")&&drop.Contains("dropGone=death.kept||body.RetainUntilRoundEnd?0:"),"PvP crush remains survive body cap and come-apart ending");
        Console.WriteLine("PASS Siren crush drop: "+cases+" deaths x release (handoff = drawn pose, worst step to it "+worstStep.ToString("0.0000")+" m / "+worstTurn.ToString("0.0000")+" rad, finite, deterministic, bounded motion, backward tumble, knees give about their hinges, lands and settles before keep/come-apart), "+gibs+" gibs on the arena floor matching the clip flight, flame split at the release, dropped weapon, kept body uncapped, authority timing and hooks unchanged");
    }
}
