#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The hats made for the model (Core/NativeModelHatMeshes; owner 2026-10-30: "Need to rework hats ... (Player head shows outside
    // of hat)", "Hats still cutting hair completely"): every hat built, its faces turned out, its tintable twin alike; each
    // pressing hat a centimetre or more round a skull of the model's measure, its crown level at the back (no sag down the back to
    // the scalp), its peak closed; the hair it presses tucked inside its crown, the hair under its band left as styled, with no step
    // where the hair goes free; a lock it would tear (a tail streaming out from under it) taken under it whole; and the runtime's
    // wiring. The skull here is an ellipsoid of the pack head's measure (Unity, 2026-10-30), not the pack's mesh: the probe on the
    // owner's PC rendered the real head and hair (DEV_LOG).
    internal static class ModelHatChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Model hats: "+what);count++;}
        // The pack head's skull in its head bone's frame: its middle and its reach (to each side, up, ahead and behind).
        private static readonly Vector3 Skull=new Vector3(0,.105f,.025f);private const float SkullX=.097f,SkullY=.107f,SkullZ=.109f;
        private static IEnumerable<Vector3> SkullPoints()
        {
            for(int i=0;i<=24;i++)for(int j=0;j<72;j++)
            {
                float el=i/24f*MathF.PI/2,az=j/72f*MathF.PI*2;
                yield return Skull+new Vector3(SkullX*MathF.Cos(el)*MathF.Sin(az),SkullY*MathF.Sin(el),SkullZ*MathF.Cos(el)*MathF.Cos(az));
            }
        }
        public static void Run(string root)
        {
            var pressing=new[]{"cap","beanie","top","wizard","cowboy","cone"};var open=new[]{"crown","antenna","halo"};
            Check(NativeModelHatMeshes.Hats.Length==9&&pressing.Concat(open).All(NativeModelHatMeshes.Has)&&!NativeModelHatMeshes.Has("none")&&!NativeModelHatMeshes.Has(null)&&!NativeModelHatMeshes.Has(NativeHatLooks.SheldonHead),"every bank hat made for the model's head, none for no hat or the Sheldon Head");
            Check(pressing.All(NativeModelHatMeshes.PressesHair)&&!open.Any(NativeModelHatMeshes.PressesHair)&&!NativeModelHatMeshes.PressesHair(NativeHatLooks.SheldonHead)&&!NativeModelHatMeshes.PressesHair(null),"the crowned hats and the cone hold the hair; the crown, the antenna and the halo stand in it");
            Check(pressing.All(h=>NativeModelHatMeshes.HairPress.Of(h)!=null)&&open.All(h=>NativeModelHatMeshes.HairPress.Of(h)==null)&&ReferenceEquals(NativeModelHatMeshes.HairPress.Of("cap"),NativeModelHatMeshes.HairPress.Of("cap")),"a press for each pressing hat, made once");
            // Built: outward faces, finite, the twin alike with its main vertices tagged.
            foreach(var hat in NativeModelHatMeshes.Hats)
            {
                var plain=NativeModelHatMeshes.Build(hat,false);var twin=NativeModelHatMeshes.Build(hat,true);
                int verts=plain.Sum(s=>s.Positions.Count),faces=0,wrong=0;bool finite=true;
                foreach(var s in plain)
                {
                    finite&=s.Positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))&&s.Bone==CapturedRig.Head;
                    for(int i=0;i<s.Indices.Count;i+=3){int a=s.Indices[i],b=s.Indices[i+1],c=s.Indices[i+2];var n=Vector3.Cross(s.Positions[b]-s.Positions[a],s.Positions[c]-s.Positions[a]);faces++;if(Vector3.Dot(n,s.Normals[a]+s.Normals[b]+s.Normals[c])<0)wrong++;}
                }
                Check(verts>500&&faces>500&&wrong==0&&finite,hat+": built ("+verts+" vertices, "+faces+" faces, "+wrong+" turned in) on the head bone");
                bool tagged=NativeHatLooks.Mains(hat).Length==0||twin.Any(s=>s.Emission.Any(e=>e.W==1));
                Check(twin.Sum(s=>s.Positions.Count)==verts&&twin.Sum(s=>s.Indices.Count)==plain.Sum(s=>s.Indices.Count)&&tagged,hat+": its tintable twin alike, its main colour tagged");
            }
            // Sized to the head: a band a centimetre or two over the skull, the crown's top 1.5 cm over it.
            Check(NativeModelHatMeshes.BandX>=SkullX+.012f&&NativeModelHatMeshes.BandX<=SkullX+.03f&&NativeModelHatMeshes.BandDepth>=SkullZ+.012f&&NativeModelHatMeshes.BandDepth<=SkullZ+.03f
                &&NativeModelHatMeshes.CrownTop>Skull.Y+SkullY&&NativeModelHatMeshes.CrownTop<Skull.Y+SkullY+.02f,"the band and the crown sized to the model's skull (not the voxel head's 36 cm)");
            foreach(var hat in pressing)
            {
                var press=NativeModelHatMeshes.HairPress.Of(hat);
                // The skull over the band inside the crown (a centimetre over the band: the band's own edge is the hat's).
                var over=SkullPoints().Where(p=>press.Over(p,.01f)).ToList();int outside=over.Count(p=>!press.Holds(p));
                Check(over.Count>(hat=="cone"?20:200)&&outside==0,hat+": the skull inside its crown ("+outside+" of "+over.Count+" points out)");
            }
            {
                // Level at the back: the top of the skull, front to back, inside the domed crowns (the band's dip down the back once
                // ran up the whole crown and sank its back onto the scalp).
                foreach(var hat in new[]{"cap","beanie","cowboy"})
                {
                    var press=NativeModelHatMeshes.HairPress.Of(hat);bool held=true;
                    for(float z=-.07f;z<=.07f;z+=.005f){var p=new Vector3(0,Skull.Y+SkullY*MathF.Sqrt(Math.Max(0,1-MathF.Pow((z-Skull.Z)/SkullZ,2)))+.006f,z);held&=press.Holds(p);}
                    Check(held,hat+": its crown over the whole top of the head, front to back, with room for the hair");
                }
                // The cowboy's peak closed: the crown's vertices nearest its top all meet (its crease runs along the midline).
                var cowboy=NativeModelHatMeshes.Build("cowboy",false)[0];var peak=cowboy.Positions.Where(p=>Math.Abs(p.X)<1e-4f&&Math.Abs(p.Z-NativeModelHatMeshes.BandZ)<1e-4f).ToList();
                Check(peak.Count>0&&peak.Max(p=>p.Y)-peak.Min(p=>p.Y)<1e-3f,"the cowboy's crown meets itself at its peak ("+peak.Count+" vertices)");
            }
            // The press: hair over the band tucked into the crown, toward the middle of the head; the hair well under it untouched.
            foreach(var hat in pressing)
            {
                var press=NativeModelHatMeshes.HairPress.Of(hat);int tucked=0,missed=0,strayed=0,moved=0,low=0;
                for(int i=0;i<=16;i++)for(int j=0;j<48;j++)for(float k=1.1f;k<=2.0f;k+=.3f)
                {
                    float el=i/16f*MathF.PI/2,az=j/48f*MathF.PI*2;var p=Skull+new Vector3(SkullX*k*MathF.Cos(el)*MathF.Sin(az),SkullY*k*MathF.Sin(el),SkullZ*k*MathF.Cos(el)*MathF.Cos(az));
                    var q=press.Press(p);
                    if(press.Over(p,.002f)&&!press.Holds(p)){tucked++;if(!press.Holds(q))missed++;var d=Vector3.Normalize(p-NativeModelHatMeshes.HairPress.Centre);if(Vector3.Distance(NativeModelHatMeshes.HairPress.Centre+d*Vector3.Distance(q,NativeModelHatMeshes.HairPress.Centre),q)>1e-4f)strayed++;}
                    if(q!=p)moved++;
                    if(press.Rise(p)<-NativeModelHatMeshes.HairPress.Flare-.001f&&press.Press(p)!=p)low++;
                    if(press.Press(q)!=q)missed++;
                }
                Check(tucked>50&&missed==0&&strayed==0,hat+": the hair over its band outside its crown tucked inside it toward the head's middle, and it stays ("+tucked+" points)");
                Check(low==0,hat+": the hair well under its band left as styled");
            }
            {
                // No step where the hair goes free: a strand far out behind (a tail) coming down past the band, a millimetre at a time.
                var press=NativeModelHatMeshes.HairPress.Of("beanie");float worst=0;Vector3? last=null;
                for(float y=.2f;y>=-.05f;y-=.001f){var p=new Vector3(0,y,-.25f);var q=press.Press(p);if(last!=null)worst=Math.Max(worst,Vector3.Distance(q,last.Value)-.001f);last=q;}
                Check(worst<.05f,"under the band the hair comes free without a step ("+(worst*100).ToString("0.0")+" cm at most a millimetre)");
                Check(press.Press(new Vector3(0,.0f,-.25f))==new Vector3(0,.0f,-.25f),"a tail hanging well under the band left where it hangs");
            }
            {
                // A piece: a short spike over the ear (pressed, kept) and a tail from the crown out behind and down past the band
                // (torn by the press: taken under the hat whole).
                var press=NativeModelHatMeshes.HairPress.Of("cap");var at=new List<Vector3>();var tris=new List<int>();
                void Ribbon(Func<float,Vector3> path,Vector3 side,int steps){int first=at.Count;for(int i=0;i<=steps;i++){var c=path(i/(float)steps);at.Add(c-side);at.Add(c+side);}for(int i=0;i<steps;i++){int a=first+i*2;tris.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}}
                Ribbon(t=>new Vector3(.1f+.04f*t,.15f+.04f*t,.02f),new Vector3(0,0,.008f),6);int spike=at.Count;
                Ribbon(t=>t<.5f?Vector3.Lerp(new Vector3(0,.215f,0),new Vector3(0,.17f,-.24f),t*2):Vector3.Lerp(new Vector3(0,.17f,-.24f),new Vector3(0,-.02f,-.28f),(t-.5f)*2),new Vector3(.01f,0,0),24);
                var pressed=new Vector3[at.Count];var hold=new float[at.Count];bool movedAny=press.PressPiece(at.ToArray(),tris.ToArray(),pressed,hold);
                bool spikeKept=Enumerable.Range(0,spike).All(i=>pressed[i]!=NativeModelHatMeshes.HairPress.Centre&&press.Holds(pressed[i]))&&Enumerable.Range(0,spike).Any(i=>pressed[i]!=at[i]);
                bool tailUnder=Enumerable.Range(spike,at.Count-spike).All(i=>pressed[i]==NativeModelHatMeshes.HairPress.Centre&&hold[i]==1);
                Check(movedAny&&spikeKept&&tailUnder,"a spike over the ear tucked in, a tail the press would tear taken under the hat whole");
                Check(Enumerable.Range(0,at.Count).All(i=>hold[i]>=0&&hold[i]<=1)&&Enumerable.Range(0,spike).All(i=>Vector3.Distance(at[i],pressed[i])<1e-6f?hold[i]==0:Vector3.Distance(at[i],pressed[i])>=NativeModelHatMeshes.HairPress.HoldMove?hold[i]==1:true),
                    "the hat holds the hair it moved a lot, not the hair it left");
                var none=new[]{new Vector3(0,.05f,.0f),new Vector3(0,.06f,.01f),new Vector3(.01f,.05f,0)};
                Check(!press.PressPiece(none,new[]{0,1,2},new Vector3[3],new float[3]),"a piece the hat does not touch left alone");
            }
            // The runtime: the model hats in the bank, worn on the model's head in its own frame (its hair pressed per hat, off the
            // main thread, a corpse's as the live body's), every model-head site drawing them.
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena","Runtime",f)).Replace("\r\n","\n");
            string bank=R("NativeHatPresentation.cs"),models=R("NativeSourceModels.cs"),body=R("NativeVisualBodyPresentation.cs"),src=R("NativeSourcePresentation.cs"),roof=R("NativeRooftopPresentation.cs"),portrait=R("NativeVisualPortraitPresentation.cs");
            Check(models.Contains("SheldonModels();ModelHats();PilotBodies();")&&bank.Contains("models[\"hat-\"+hat+ModelSuffix+(tinted?NativeHatLooks.TintSuffix:\"\")]=parts;")&&bank.Contains("private string VisualHatModel(string hat,string color)"),"the model hats and their twins in the bank");
            Check(body.Contains("if(NativeModelHatMeshes.Has(hat))return head.localToWorldMatrix;"),"a model hat on the head bone's own frame");
            Check(src.Contains("sourceModels.Queue(VisualHatModel(a.Hat,a.HatColor),rig.Root.transform.localToWorldMatrix,hb,null,layer,camera,")&&roof.Contains("sourceModels.Queue(VisualHatModel(look.Hat,hatColor),rig.Root.transform.localToWorldMatrix,hb,")
                &&portrait.Contains("sourceModels.Queue(VisualHatModel(hat,look.HatColor),")&&body.Contains("sourceModels.Queue(VisualHatModel(seat.hat,seat.hatColor),")&&body.Contains("sourceModels.Queue(VisualHatModel(a.Hat,a.HatColor),")
                &&body.Contains("sourceModels.QueueShadowless(VisualHatModel(hat,a.HatColor)??corpse.HatModel,"),"every model-head hat site draws the model hat (the arena, rooftops, portraits, the draft, the revive stand-in, corpses)");
            Check(body.Contains("string worn=hat==NativeHatLooks.SheldonHead||NativeModelHatMeshes.PressesHair(hat)?hat:null;")&&body.Contains("var want=worn!=null&&!flat?VisualPressedHair(mesh,skin,worn):mesh;")
                &&body.Contains("pressingHair[key]=System.Threading.Tasks.Task.Run(()=>")&&body.Contains("if(!press.PressPiece(at,triangles,pressed,hold))return (null,null);")
                &&body.Contains("if(NativeModelHatMeshes.PressesHair(death.actor?.Hat))"),"the hair pressed per hat off the main thread, the Sheldon hood still flat, a corpse's hair as the live body's");
            Console.WriteLine("PASS model hats ("+count+" checks)");
        }
    }
}
#endif
