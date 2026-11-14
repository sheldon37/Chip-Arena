#if UNITY_EDITOR || ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    public static class NativeVisualChecks
    {
        private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        private static void Near(float a,float b,string message){Check(Math.Abs(a-b)<.00005f,message+": "+a+" / "+b);}
        private static Vector2 Project(Vector3 p)
        {float f=1/(float)Math.Tan(NativeViewRules.FirstFov*Math.PI/360);return new Vector2(p.X/p.Z*f/(16f/9),p.Y/p.Z*f);}
        private static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
        private static List<Vector2> Hull(List<Vector2> points)
        {
            points.Sort((a,b)=>a.X==b.X?a.Y.CompareTo(b.Y):a.X.CompareTo(b.X));var h=new List<Vector2>();
            foreach(var p in points){while(h.Count>=2&&Cross(h[h.Count-2],h[h.Count-1],p)<=0)h.RemoveAt(h.Count-1);h.Add(p);}
            int lower=h.Count;
            for(int i=points.Count-2;i>=0;i--){var p=points[i];while(h.Count>lower&&Cross(h[h.Count-2],h[h.Count-1],p)<=0)h.RemoveAt(h.Count-1);h.Add(p);}
            h.RemoveAt(h.Count-1);return h;
        }
        private static bool CoversCrosshair(List<Vector2> points)
        {
            var h=Hull(points);if(h.Count<3)return false;
            // Separating-axis intersection with Claude's |x|<.1, |y|<.15 NDC box; full convex meshes, not only the shaft.
            var axes=new List<Vector2>{Vector2.UnitX,Vector2.UnitY};
            for(int i=0;i<h.Count;i++){var d=h[(i+1)%h.Count]-h[i];axes.Add(new Vector2(-d.Y,d.X));}
            foreach(var a in axes){float lo=float.MaxValue,hi=float.MinValue;foreach(var p in h){float v=Vector2.Dot(p,a);lo=Math.Min(lo,v);hi=Math.Max(hi,v);}float extent=.1f*Math.Abs(a.X)+.15f*Math.Abs(a.Y);if(hi<=-extent||lo>=extent)return false;}
            return true;
        }
        public static List<string> Run(ConversionContent content)
        {
            var results=new List<string>();int samples=0,totalCross=0;float nearest=float.MaxValue;var channels=new float[13];var row=new float[64];
            string[] specs={"fire","water","lightning","darkness"};
            for(int element=1;element<=4;element++)foreach(string action in new[]{"idle","primary","guard","guard-transition"}.Concat(content.abilities.Where(a=>a.spec==specs[element-1]).Select(a=>a.id)))
            foreach(float rate in action=="idle"||action.StartsWith("guard")?new[]{1f}:new[]{.5f,1f,3f})
            {
                var definition=content.abilities.FirstOrDefault(a=>a.id==action);float release=definition==null?NativeAbilityRules.PrimaryWindup(element,rate):NativeAbilityRules.CastTime(definition.windup,new ChipStats(new[]{new NumberValue{name="attackRate",value=rate}}));
                var guard=new NativeGuardMotion();int visible=0,cross=0,releaseCross=0,count=0,handVisible=0;float gemX=0;
                for(int frame=0;frame<=120;frame++)
                {
                    float time=frame/60f;var pose=new CapturedRig.Pose();CapturedClips.Sets[0][0].Sample(time,row);CapturedRig.Sample(row,0,pose);
                    float cycle=(float)content.Weapon(specs[element-1]).interval/rate,attack=(float)Math.Floor(time/cycle)*cycle,shot=time>=attack+release?attack+release:attack>0?attack-cycle+release:-100;
                    var actor=new Actor{MagicVisual=element,CastRate=rate,AttackAt=action=="primary"?attack:-100,LastShot=action=="primary"?shot:-100,AbilityAt=definition!=null?0:-100,AbilityId=definition?.id,AbilityRelease=release};
                    float g=guard.Sample(time,action=="guard"||action=="guard-transition"&&time>=.2f&&time<.5f);
                    MagicPose.Apply(pose,actor,time,true,channels,g);NativeFirstPersonPose.SettleTorso(pose);
                    Vector3 wrist=CapturedRig.Mirror(pose.WorldPosition[CapturedRig.LHand])*NativeFirstPersonPose.Scale+NativeFirstPersonPose.MagicViewOffset(g);
                    Quaternion rotation=NativeFirstPersonPose.StaffRotation(CapturedRig.Mirror(pose.WorldRotation[CapturedRig.LHand]),Vector3.UnitZ,g);
                    float scale=NativeFirstPersonPose.Scale*NativeFirstPersonPose.StaffSize(g);
                    Vector3 Transform(Vector3 p)=>wrist+Vector3.Transform(CapturedRig.Mirror(p)*scale,rotation);
                    Vector2 gem=Project(Transform(new Vector3(0,0,.72f)));gemX+=gem.X;if(Math.Abs(gem.X)<=1&&Math.Abs(gem.Y)<=1)visible++;
                    Vector2 hand=Project(CapturedRig.Mirror(pose.WorldPosition[CapturedRig.RHand])*NativeFirstPersonPose.Scale+NativeFirstPersonPose.MagicViewOffset(g));if(Math.Abs(hand.X)<=1&&Math.Abs(hand.Y)<=1)handVisible++;
                    bool covered=false;
                    foreach(var part in NativeStaffGeometry.Parts[element-1])
                    {
                        var corners=new List<Vector2>();
                        foreach(float x in new[]{-1f,1f})foreach(float y in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})
                        {Vector3 p=Transform(part.Position+Vector3.Transform(part.Size*new Vector3(x,y,z)*.5f,part.Rotation));Check(p.Z>.01f,$"Staff entered near plane: {element} {action} {rate}");corners.Add(Project(p));}
                        covered|=CoversCrosshair(corners);
                    }
                    for(int i=0;i<=40;i++)nearest=Math.Min(nearest,Transform(new Vector3(0,0,-.36f+1.24f*i/40)).Length());
                    if(covered){cross++;if(Math.Abs(time-release)<=.15f)releaseCross++;}count++;samples++;
                }
                string name=$"{specs[element-1]} {action} rate {rate}";
                // Match the source's ability allowance; idle/primary/guard must be completely clear.
                Check(cross<=((definition==null)?0:count*.05f),name+" covers crosshair: "+cross+" / "+count);
                if(definition!=null)Check(releaseCross==0,name+" staff covers ability release");
                if(definition==null&&!action.Contains("transition"))Check(visible>=count*.95f,name+" crystal left screen: "+visible+" / "+count);
                if(action=="idle")Check(gemX/count<-.3f,name+" top not rolled outward");
                if(action=="primary")Check(handVisible>=count*.45f,name+" casting hand offscreen: "+handVisible);
                totalCross+=cross;
            }
            Check(nearest>=.12f,"Staff too close to camera");
            results.Add(samples+" native staff mesh projections at 75 degrees / 16:9: four schools, primary/Q/E, rates, guard transitions; "+totalCross+" crosshair intersections; nearest axis "+nearest.ToString("0.000")+" m");
            for(int n=0;n<100;n++){var q=CapturedRig.Euler(n*.01f-.5f,n*.013f-.65f,n*.015f-.75f);var e=NativeFirstPersonPose.EulerXYZ(q);Check(Math.Abs(Quaternion.Dot(q,CapturedRig.Euler(e.X,e.Y,e.Z)))>.99999f,"Torso Euler conversion");}
            var spring=new NativeGuardMotion();Near(spring.Sample(1,false),0,"Guard initial");for(int i=1;i<=12;i++)spring.Sample(1+i/60f,true);Check(spring.Weight>.8f&&spring.Weight<1,"Guard spring did not blend");Near(spring.Sample(0,false),0,"Clock reversal must reset guard");Near(spring.Sample(2,true),1,"Clock gap must reset guard");
            results.Add("First-person Euler round trips, guard blending and clock resets");
            Near(NativeGlowRules.HaloSize(.01f,1),.7f,"Minimum halo");Near(NativeGlowRules.HaloSize(2,1),4,"Maximum halo");Near(NativeGlowRules.HaloSize(.2f,.4f),1.394f,"Low halo");
            Near(NativeGlowRules.HaloAlpha(0),1,"Halo centre");Near(NativeGlowRules.HaloAlpha(.25f),.24348935f,"Halo source falloff");Near(NativeGlowRules.HaloAlpha(1),0,"Halo edge");
            Near(NativeGlowRules.ImpactFade(.425f),.2125f,"Impact half-life");Near(NativeGlowRules.ImpactFade(.85f),0,"Impact expiry");Near(NativeGlowRules.ImpactLight(.175f),.625f,"Impact light fade");Near(NativeGlowRules.ImpactLight(.35f),0,"Impact light expiry");
            Near(NativeGlowRules.TrailWidth(20,1),.025f,"Trail camera width");Near(NativeGlowRules.TrailFade(.09f),.5f,"Trail fade");Near(NativeGlowRules.ArmorPulse(1f/3,1,false),.4f,"Armor cooldown pulse");Near(NativeGlowRules.ArmorPulse(1f/3,1,true),1,"Guard armor bright");
            var trace=new NativeTrace();trace.Sample(new V3(0,0,0),0,1);trace.Sample(new V3(.02f,0,0),.01f,1);trace.Sample(new V3(.04f,0,0),.02f,1);trace.Sample(new V3(.06f,0,0),.03f,1);Check(trace.Points.Count==2,"Slow projectile never grows a trail");
            Check(!trace.Expired(.20f)&&trace.Expired(.22f),"Trail must survive removal for its natural lifetime");
            for(int n=0;n<100;n++)trace.Sample(new V3(n*.1f,0,0),1+n*.001f,2);Check(trace.Points.Count==12,"Trace point limit");trace.Sample(new V3(0,0,0),0,2);Check(trace.Points.Count==1,"Trace reset left old geometry");
            results.Add("Source glow falloff/lifetimes, light fade, armor pulse and slow-projectile trail sampling/expiry/reset");
            // A chosen accent reaches the arena glow (2026-10-09 playtest: every pick glowed cyan). Expected values: three r180
            // character-rig.ts ember() and armor-glow.ts:23 at 05950ea, linear.
            (string hex,Vector3 ember,Vector3 halo)[] accents={
                ("#70e1d0",new Vector3(.043722f,.504941f,.409573f),new Vector3(.285677f,.777364f,.726760f)),
                ("#ff0000",new Vector3(.95f,.05f,.05f),new Vector3(.905775f,.220187f,.26f)),
                ("#c8102e",new Vector3(.553624f,.029138f,.049424f),new Vector3(.593185f,.224021f,.280217f)),
                ("#3a7bff",new Vector3(.088f,.22852f,.952f),new Vector3(.197086f,.366758f,1)),
                ("#ffc0cb",new Vector3(.952f,.088f,.216054f),new Vector3(.905775f,.610252f,.701929f)),
                ("#ffffff",new Vector3(.784f,.256f,.256f),new Vector3(.905775f,.960187f,1)),
                ("#000000",Vector3.Zero,new Vector3(.165775f,.220187f,.26f))};
            Check(Vector3.Distance(NativePortraitRules.ExportEmber,accents[0].ember)<2e-5f,"exported accent trims carry ember('#70e1d0')");
            foreach(var a in accents)
            {
                // The exported trim (diffuse and emissive) times the pilot's tint is the source's ember(accent): the shader multiplies both.
                Vector3 tint=NativePortraitRules.GlowAccentTint(a.hex),glow=NativePortraitRules.ExportEmber*tint;
                Check(Vector3.Distance(glow,a.ember)<2e-5f,"accent glow "+a.hex+": "+glow+" / "+a.ember);
                Check(Vector3.Distance(NativePortraitRules.GlowAccentTint(a.hex.ToUpperInvariant()),tint)<1e-6f,"accent case "+a.hex);
                Check(Vector3.Distance(NativeGlowRules.ArmorTint(a.hex),a.halo)<2e-5f,"armor halo "+a.hex+": "+NativeGlowRules.ArmorTint(a.hex)+" / "+a.halo);
            }
            // The red pick glows red, not through a cyan filter (the old Linear(accent)/Linear('#70e1d0') ratio left it (.27,0,0)).
            var red=NativePortraitRules.ExportEmber*NativePortraitRules.GlowAccentTint("#ff0000");Check(red.X>10*red.Y&&red.X>10*red.Z&&red.X>.9f,"red accent glow "+red);
            var redHalo=NativeGlowRules.ArmorTint("#ff0000");Check(redHalo.X>2*redHalo.Y&&redHalo.X>2*redHalo.Z,"red armor halo "+redHalo);
            // JsonUtility turns an absent colour into "": the default accent, never a broken or white tint.
            foreach(var missing in new[]{null,"","#12345","70e1d0","#zzzzzz"})
            {
                Check(Vector3.Distance(NativePortraitRules.GlowAccentTint(missing),Vector3.One)<1e-6f,"missing accent tint '"+missing+"'");
                Check(Vector3.Distance(NativeGlowRules.ArmorTint(missing),accents[0].halo)<2e-5f,"missing accent halo '"+missing+"'");
            }
            results.Add("Chosen accents reach the arena glow: "+accents.Length+" picks' trims match three's ember() and their armor halos armor-glow.ts; missing colours fall back to the default");
            // Team bands (team-identity.ts): only in teams/PvE, never on the local player's own pilot (owner direction).
            Check(NativeStatusVisuals.PilotBand(true,true,false,0,true)==-1&&NativeStatusVisuals.PilotBand(true,false,false,1,true)==-1,"local pilot wears no band");
            Check(NativeStatusVisuals.PilotBand(true,true,false,0,false)==1&&NativeStatusVisuals.PilotBand(true,true,true,1,false)==4&&NativeStatusVisuals.PilotBand(true,false,false,2,false)==2,"teammates and opponents keep their bands");
            Check(NativeStatusVisuals.PilotBand(false,false,false,1,false)==-1&&NativeStatusVisuals.PilotBand(false,false,false,1,true)==-1,"no bands outside a teams match");
            results.Add("Team bands: teams/PvE only, the local pilot's own model none");
            return results;
        }
    }
}
#endif
