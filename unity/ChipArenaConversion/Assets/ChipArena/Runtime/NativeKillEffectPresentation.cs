using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    // C18: the pieces of a ragdoll kill effect around the cosmetic body (Core/NativeKillEffectRules.cs): RagdollVfx swarm,
    // GibberVfx spray, woundCap stump caps, CrushDebris and voxel-burst. Built once when the corpse is made, in source axes
    // from the corpse's bind pose (Unity x mirrored), and stepped with the corpse's own fixed 1/60 s physics clock. The body's
    // forces, joints and colliders are untouched: everything here only reads the part bodies.
    internal sealed partial class NativeCorpse
    {
        internal NativeKillSwarm Swarm;internal NativeKillSpray Spray;internal NativeKillBurst Burst;internal NativeKillDebris Debris;
        /// <summary>Pieces this effect holds of the shared kill-effect budget (ragdoll.ts effect.count).</summary>
        internal int FxCount;internal bool Counted;
        internal KillBox[] RigBoxes=Array.Empty<KillBox>();internal int RigCount;internal Matrix4x4[] BoxLocal=Array.Empty<Matrix4x4>();
        internal struct KillCap{public int Part;public Matrix4x4 Local;public int Start,Count;}
        internal readonly List<KillCap> Caps=new List<KillCap>();internal Matrix4x4[] CapCells=Array.Empty<Matrix4x4>();internal Color[] CapColors=Array.Empty<Color>();
        /// <summary>The loose hat among the crush fragments' loose objects (-1 none) and where it starts: the head cosmetic
        /// socket (socketCopy's group position), Unity axes.</summary>
        internal int LooseHat=-1;internal Vector3 HatStart;
        internal readonly double[] SwarmParts=new double[NativeKillEffectRules.Parts*3];
        private int sprayPart=-1;private Vector3 sprayPoint,sprayNormal;
        private Matrix4x4 overlay=Matrix4x4.identity;private float sink,partScale=1;
        private Func<int,Matrix4x4> presentedBone;private Func<int,bool> shownBone;private string hatModel,looseModel;
        /// <summary>The hat and dropped-weapon model ids (built once, not per frame).</summary>
        internal string HatModel=>hatModel??=string.IsNullOrEmpty(Death.actor?.Hat)?null:NativeHatLooks.Model(Death.actor.Hat,Death.actor.HatColor);
        // The dropped weapon in the victim's own skin (Core/NativeWeaponSkins.cs; its seat, as every client replicates it).
        internal string LooseModel=>looseModel??=string.IsNullOrEmpty(Death.actor?.SourceSpec)?null:NativeWeaponSkins.Model("loose-"+Death.actor.SourceSpec,NativeWeaponSkins.Index(arena?.Match?.Seat(Death.actor.Id)?.weaponSkins,Death.actor.SourceSpec));
        /// <summary>The bone matrices to draw with this frame (cached delegate: no per-frame allocation).</summary>
        internal Func<int,Matrix4x4> PresentedBone=>presentedBone??=Presented;internal Func<int,bool> ShownBone=>shownBone??=Shown;

        // Unity <-> source (three.js) axes: x mirrored.
        private static double[] Source(Matrix4x4 m){var o=new double[16];for(int c=0;c<4;c++)for(int r=0;r<4;r++)o[c*4+r]=m[r,c]*((r==0)!=(c==0)?-1:1);return o;}
        private static Matrix4x4 Unity(double[] s){var m=new Matrix4x4();for(int c=0;c<4;c++)for(int r=0;r<4;r++)m[r,c]=(float)(s[c*4+r]*((r==0)!=(c==0)?-1:1));return m;}
        internal static Matrix4x4 Mirror(double px,double py,double pz,double qx,double qy,double qz,double qw,double sx,double sy,double sz)
            =>Matrix4x4.TRS(new Vector3(-(float)px,(float)py,(float)pz),new Quaternion((float)qx,-(float)qy,-(float)qz,(float)qw),new Vector3((float)sx,(float)sy,(float)sz));

        /// <summary>ragdoll.ts update(): the presented part (body pose, then the stock .35 m sink and .85 shrink, and the
        /// RagdollVfx collapse; scale is about the part's own joint, as the source scales each part group).</summary>
        internal void Present(Matrix4x4 overlay,float sink,float scale){this.overlay=overlay;this.sink=sink;partScale=scale;}
        /// <summary>Part `part`'s presented matrix over its own at death (PartMatrix times its inverse bind): what a bone riding the
        /// part rigidly is multiplied by (the visual body's corpse, NativeVisualBodyPresentation.cs VisualCorpseShown).</summary>
        internal Matrix4x4 PartFrom(int part)=>PartMatrix(part)*inverse[part];
        /// <summary>The parts' presented scale (the stock shrink, a swarm's collapse).</summary>
        internal float PartScale=>partScale;
        internal Matrix4x4 PartMatrix(int part)=>overlay*Matrix4x4.Translate(new Vector3(0,-sink,0))*Posed(part,Parts[part].transform.localToWorldMatrix)*Matrix4x4.Scale(new Vector3(partScale,partScale,partScale));
        /// <summary>The dropped weapon as its hand part is presented (ragdoll.ts keeps the held weapon's meshes in the hand
        /// part's group): the same sink, shrink and collapse, scaled about the weapon body's own origin.</summary>
        internal Matrix4x4? PresentedWeapon=>weapon==null?(Matrix4x4?)null:overlay*Matrix4x4.Translate(new Vector3(0,-sink,0))*Posed(SmoothWeapon,weapon.transform.localToWorldMatrix)*Matrix4x4.Scale(new Vector3(partScale,partScale,partScale));
        private float fxAge;
        /// <summary>The effect's age on the presentation clock (<paramref name="clockAge"/>), held so it never runs backwards
        /// or below 0.</summary>
        internal float FxAge(float clockAge){if(clockAge>fxAge)fxAge=clockAge;return fxAge;}
        private Matrix4x4 Presented(int b){int a=Anchor[b];return PartMatrix(a)*CollapseFoot(b)*inverse[a]*Bind[b];}
        private bool Shown(int b)=>!IsCrushed(Anchor[b]);
        internal bool Crushed=>CrushPart>=0&&Parts.ContainsKey(CrushPart);
        /// <summary>The slice's further stump fountains (a cleave's second and third cuts, a crush beside a cut), each on its part.</summary>
        internal NativeKillSpray[] ExtraSprays=Array.Empty<NativeKillSpray>();private int[] extraPart=Array.Empty<int>();private Vector3[] extraPoint=Array.Empty<Vector3>(),extraNormal=Array.Empty<Vector3>();
        private int[] splatsSeen=new int[1];
        /// <summary>How many of fountain `index`'s landings (0 the main one) the persistent splats have taken (Runtime/NativeBloodPresentation).</summary>
        internal int SplatsSeen(int index)=>index>=0&&index<splatsSeen.Length?splatsSeen[index]:0;
        internal void SawSplats(int index,int landed){if(index>=splatsSeen.Length)Array.Resize(ref splatsSeen,index+1);if(index>=0)splatsSeen[index]=landed;}
        internal Vector3 PresentedPosition(int part)=>PartMatrix(part).GetColumn(3);

        /// <summary>The team band colour index (NativeStatusVisuals.TeamColors) the pilot wore at death, or -1.</summary>
        internal int Band=-1;
        /// <summary>Builds the effect pieces for a corpse that received <paramref name="budget"/> (DeathEffects: 280 minus the
        /// live effects' counts, or a kept body's 96). <paramref name="hat"/>: the hat model exists. <paramref name="band"/>: the
        /// team band colour index in a teams match (team-identity.ts bands are part of the rig the source cuts and copies), or -1.
        /// <paramref name="floor"/>: the map's kill-effect floor (NativeStructureFxPresentation.KillFloor; gibs settle on upper
        /// floors), or null for the source's flat one.</summary>
        internal void SetupKill(int budget,double quality,bool counted,bool hat,int band=-1,NativeKillFloor floor=null)
        {
            Band=band>=0&&band<NativeStatusVisuals.TeamColors.Length?band:-1;
            Counted=counted;bool low=quality<.5;var wound=Smash!=null?null:Death.wound;bool crush=Crushed;int parts=Parts.Count-(crush?1+ExtraCrush.Count:0);budget=Math.Max(0,budget);int crushPart=CrushPart;
            bool rig=(Death.model??"pilot")=="pilot";var bind=new double[22][];for(int i=0;i<22;i++)bind[i]=Source(Bind[i]);
            int body=NativeKillEffectRules.ParseHex(Death.actor?.BodyColor,0xe5e7eb),accent=NativeKillEffectRules.ParseHex(Death.actor?.AccentColor,0x70e1d0);
            var scratch=new double[16];var world=new double[16];
            // The body skin the pilot wore: its cloth joins the gibs and its own colours paint the boxes (Core/NativeBodyModels.cs).
            int bodySkin=NativeBodySkins.Of(arena?.Match?.Seat(Death.actor?.Id??Death.id)?.weaponSkins);
            if(rig)
            {
                RigBoxes=new KillBox[NativeKillBoxes.MaxRigBoxes];RigCount=NativeKillBoxes.Rig(Death.actor?.SourceSpec,Death.actor?.Hat,RigBoxes);
                // The body skin's cloth joins the gibs (Core/NativeBodyModels.cs: the Sheldon Clothes' coat panels).
                RigCount=NativeBodyModels.Gibs(bodySkin,RigBoxes,RigCount);
                if(Band>=0)RigCount=NativeKillBoxes.Bands(RigBoxes,RigCount,NativeStatusVisuals.TeamColors[Band]);BoxLocal=new Matrix4x4[RigCount];
                for(int i=0;i<RigCount;i++){var b=RigBoxes[i];NativeKillEffectRules.Compose(scratch,b.Px,b.Py,b.Pz,b.Qx,b.Qy,b.Qz,b.Qw,b.Sx,b.Sy,b.Sz);BoxLocal[i]=Unity(scratch);}
            }
            Vector3 dir=Death.direction.sqrMagnitude>.01f?Death.direction.normalized:Quaternion.Euler(0,Death.yaw,0)*Vector3.back;
            if(Style=="voxel-burst")
            {
                if(!rig)return;var matrices=new double[RigCount*16];var colors=new int[RigCount];
                for(int i=0;i<RigCount;i++){NativeKillEffectRules.BoxWorld(RigBoxes[i],bind[RigBoxes[i].Bone],scratch,world);Array.Copy(world,0,matrices,i*16,16);colors[i]=NativeBodyModels.GibColor(bodySkin,RigBoxes[i],body,accent);}
                var p=Death.position;var v=Death.velocity;var d=Death.direction;
                Burst=new NativeKillBurst(matrices,colors,RigCount,Death.seed,-p.x,p.y,p.z,-v.x,v.y,v.z,-d.x,d.y,d.z,quality,budget,floor);FxCount=Burst.Count;return;
            }
            FxCount=parts;
            if(NativeKillEffectRules.Swarm(Style)){Swarm=new NativeKillSwarm(Style,Death.seed,Math.Max(0,budget-parts),quality);FxCount+=Swarm.Count;}
            if(!rig)return;
            // The fatal wound (the authority's sever / crush, or the Gibber's part) and a melee slice's cuts (Core/NativeMeleeSlice):
            // caps, sprays and fragments, as ragdoll.ts does for its one part.
            bool gib=Style=="dev-gibber";var cutParts=new List<int>();foreach(int j in Severed)if(Parts.ContainsKey(j)&&!(crush&&j==crushPart))cutParts.Add(j);
            bool throat=Slice!=null&&Slice.Throat&&Parts.ContainsKey(CapturedRig.Spine);
            if(cutParts.Count==0&&!crush&&!throat)return;bool gibSpray=gib&&cutParts.Count>0;var emitters=new List<(int part,Vector3 point,Vector3 normal,bool crush)>();
            int capBudget=Math.Max(0,budget-parts);var cells=new List<Matrix4x4>();var cellColors=new List<Color>();var buffer=new KillCell[NativeKillCaps.MaxCells];
            bool AddCap(int joint,int on,bool parentSide,bool ragged,int owner)
            {
                if(capBudget<1)return false;capBudget--;uint seed=unchecked((uint)Death.seed^(uint)(Caps.Count+1)*0x9e3779b1u);
                string meshName=NativeKillBoxes.CapMesh(joint);int section=-1;for(int i=0;i<RigCount;i++)if(RigBoxes[i].Name==meshName){section=i;break;}
                double width=(section>=0?RigBoxes[section].Sx:.16)*.92,depth=(section>=0?RigBoxes[section].Sz:.16)*.92;
                NativeKillEffectRules.Linear(section>=0?NativeBodyModels.GibColor(bodySkin,RigBoxes[section],body,accent):body,out double sr,out double sg,out double sb);
                var jm=bind[joint];double sign=joint==5||joint==2?1:-1,bx=jm[4],by=jm[5],bz=jm[6],bl=Math.Sqrt(bx*bx+by*by+bz*bz);if(bl<1e-9)bl=1;bx*=sign/bl;by*=sign/bl;bz*=sign/bl;
                var inverses=new List<double>();var inv=new double[16];
                for(int i=0;i<RigCount;i++){var b=RigBoxes[i];if(b.Root!=owner||b.Zone!=0||b.Detail||b.Hidden)continue;NativeKillEffectRules.BoxWorld(b,bind[b.Bone],scratch,world);NativeKillEffectRules.Invert(world,inv);inverses.AddRange(inv);}
                double s=NativeKillCaps.FaceAlong(jm[12],jm[13],jm[14],bx,by,bz,inverses.ToArray(),inverses.Count/16,parentSide),side=parentSide?1:-1;
                double px=jm[12]+bx*(s-side*.012),py=jm[13]+by*(s-side*.012),pz=jm[14]+bz*(s-side*.012);
                // jq * rotation about x by 0 or pi (the cap's +Y points out of the part it sits on).
                var cap=new double[16];Array.Copy(jm,cap,16);for(int r=0;r<3;r++){double l=Math.Sqrt(cap[r*4]*cap[r*4]+cap[r*4+1]*cap[r*4+1]+cap[r*4+2]*cap[r*4+2]);if(l>1e-9)for(int k=0;k<3;k++)cap[r*4+k]/=l;}
                if(sign*side<=0)for(int k=0;k<3;k++){cap[4+k]=-cap[4+k];cap[8+k]=-cap[8+k];}
                cap[12]=px;cap[13]=py;cap[14]=pz;
                int n=NativeKillCaps.Cap(width,depth,ragged,seed,sr,sg,sb,joint==2?1:0,low,buffer);int start=cells.Count;
                for(int i=0;i<n;i++){var c=buffer[i];cells.Add(Mirror(c.X,c.Y,c.Z,c.Qx,c.Qy,c.Qz,c.Qw,c.Sx,c.Sy,c.Sz));cellColors.Add(new Color((float)c.R,(float)c.G,(float)c.B,1));}
                Caps.Add(new KillCap{Part=on,Local=inverse[on]*Unity(cap),Start=start,Count=n});return true;
            }
            int Parent(int b){int p=CapturedRig.Bones[b].Parent;while(p>=0&&(!Parts.ContainsKey(p)||crush&&IsCrushed(p)))p=CapturedRig.Bones[p].Parent;return p;}
            int NearestRoot(int b){int p=CapturedRig.Bones[b].Parent;while(p>=0&&!Parts.ContainsKey(p))p=CapturedRig.Bones[p].Parent;return p;}
            void Anchor(int joint,int part)
            {
                // The joint itself on its parent part, emitting away from the parent's origin (ragdoll.ts gibber wound point).
                sprayPart=part;Vector3 jointAt=Bind[joint].GetColumn(3),origin=Bind[part].GetColumn(3);sprayPoint=inverse[part].MultiplyPoint3x4(jointAt);sprayNormal=inverse[part].MultiplyVector((jointAt-origin).normalized);
            }
            void FromCap(){var last=Caps[Caps.Count-1];sprayPart=last.Part;sprayPoint=last.Local.GetColumn(3);sprayNormal=last.Local.MultiplyVector(Vector3.up).normalized;}
            int stumpParent=-1;
            foreach(int severed in cutParts)
            {
                stumpParent=Parent(severed);if(stumpParent<0)continue;Anchor(severed,stumpParent);
                if(capBudget>=2){bool stump=AddCap(severed,stumpParent,true,false,stumpParent);int at=Caps.Count;AddCap(severed,severed,false,false,severed);if(stump&&!gib){var s=Caps[at-1];sprayPart=s.Part;sprayPoint=s.Local.GetColumn(3);sprayNormal=s.Local.MultiplyVector(Vector3.up).normalized;}}
                emitters.Add((sprayPart,sprayPoint,sprayNormal,false));
            }
            if(throat)
            {
                // A slit throat: no cut, the neck gushes forward and down off the chest part.
                Anchor(CapturedRig.Head,CapturedRig.Spine);Vector3 forward=((Vector3)Bind[CapturedRig.Head].GetColumn(2)).normalized;
                sprayNormal=inverse[CapturedRig.Spine].MultiplyVector((forward*.85f+Vector3.down*.3f).normalized).normalized;emitters.Add((sprayPart,sprayPoint,sprayNormal,false));
            }
            if(crush&&Smash!=null)
            {
                // A thrown smash (NativeSmashGib, NativeSmashPresentation.cs): each crushed part's children capped ragged where they
                // tore off, each stump capped and spraying, and all their boxes (and what their sockets carried) one burst of
                // fragments thrown along the smash's travel at its pace.
                int many=1+ExtraCrush.Count;var sources=new List<double>();var sourceColors=new List<double>();int sourceCount=0;var loose=new List<double>();int s0=NativeKillBoxes.SpecIndex(Death.actor?.SourceSpec);
                void Loose(int bone){var m=bind[bone];loose.Add(m[12]);loose.Add(m[13]);loose.Add(m[14]);}
                for(int ci=0;ci<many;ci++)
                {
                    int crushed=ci==0?crushPart:ExtraCrush[ci-1];if(!Parts.ContainsKey(crushed))continue;
                    foreach(var b in Parts.Keys)if(!IsCrushed(b)&&NearestRoot(b)==crushed)AddCap(b,b,false,true,b);
                    int stump=Parent(crushed);if(stump>=0){Anchor(crushed,stump);if(AddCap(crushed,stump,true,true,stump))FromCap();emitters.Add((sprayPart,sprayPoint,sprayNormal,true));}
                    for(int i=0;i<RigCount;i++){var b=RigBoxes[i];if(b.Root!=crushed||b.Zone!=0||b.Hidden)continue;NativeKillEffectRules.BoxWorld(b,bind[b.Bone],scratch,world);sources.AddRange(world);NativeKillEffectRules.Linear(NativeBodyModels.GibColor(bodySkin,b,body,accent),out double r,out double g,out double bb);sourceColors.Add(r);sourceColors.Add(g);sourceColors.Add(bb);sourceCount++;}
                    if(crushed==5&&hat&&LooseHat<0){var socket=NativeKillBoxes.HatSocket;HatStart=Bind[5].MultiplyPoint3x4(new Vector3(-socket[0],socket[1],socket[2]));LooseHat=loose.Count/3;loose.Add(-HatStart.x);loose.Add(HatStart.y);loose.Add(HatStart.z);}
                    if(crushed==8&&s0>=0){if(Shown(NativeKillBoxes.Offhands[s0]))Loose(9);if(NativeKillBoxes.Magic[s0])Loose(9);}
                    if(crushed==12&&s0>=0&&!NativeKillBoxes.Magic[s0]&&Shown(NativeKillBoxes.Weapons[s0]))Loose(13);
                }
                int left=Math.Max(0,budget-parts-Caps.Count),most=Math.Min(low?24:64,left*55/100);
                if(sourceCount>0||loose.Count>0)
                {
                    NativeSmashGib.Fragments(Smash,out var inherit,out float kick,out float severity);var t=Smash.Travel;var o=Death.position;
                    Debris=new NativeKillDebris(sources.ToArray(),sourceColors.ToArray(),sourceCount,loose.ToArray(),loose.Count/3,-o.x,o.y,o.z,-t.X,t.Y,t.Z,-inherit.X,inherit.Y,inherit.Z,kick,severity,unchecked((uint)Death.seed),most,Smash.Floor);
                    if(LooseHat>=Debris.Loose)LooseHat=-1;
                }
            }
            else if(crush)
            {
                int crushed=crushPart;float momentum=wound!=null&&wound.kind=="crush"&&wound.part==crushed?wound.momentum:40,severity=wound!=null&&wound.kind=="crush"&&wound.part==crushed?wound.severity:1;foreach(var b in Parts.Keys)if(b!=crushed&&NearestRoot(b)==crushed)AddCap(b,b,false,true,b);
                stumpParent=Parent(crushed);if(stumpParent>=0){Anchor(crushed,stumpParent);if(AddCap(crushed,stumpParent,true,true,stumpParent))FromCap();emitters.Add((sprayPart,sprayPoint,sprayNormal,true));}
                // CrushDebris: the crushed part's boxes and what its sockets carried (the hat; a weapon here flies as the corpse's
                // own dropped weapon, but still takes its place in the fragment budget).
                var sources=new List<double>();var sourceColors=new List<double>();int sourceCount=0;
                for(int i=0;i<RigCount;i++){var b=RigBoxes[i];if(b.Root!=crushed||b.Zone!=0||b.Hidden)continue;NativeKillEffectRules.BoxWorld(b,bind[b.Bone],scratch,world);sources.AddRange(world);NativeKillEffectRules.Linear(NativeBodyModels.GibColor(bodySkin,b,body,accent),out double r,out double g,out double bb);sourceColors.Add(r);sourceColors.Add(g);sourceColors.Add(bb);sourceCount++;}
                var loose=new List<double>();int s0=NativeKillBoxes.SpecIndex(Death.actor?.SourceSpec);
                void Loose(int bone){var m=bind[bone];loose.Add(m[12]);loose.Add(m[13]);loose.Add(m[14]);}
                if(crushed==5&&hat){var socket=NativeKillBoxes.HatSocket;HatStart=Bind[5].MultiplyPoint3x4(new Vector3(-socket[0],socket[1],socket[2]));LooseHat=0;loose.Add(-HatStart.x);loose.Add(HatStart.y);loose.Add(HatStart.z);}
                // socketCopy: a socket with no visible mesh flies nothing (Ansatsuken's hand wraps are hidden).
                if(crushed==8&&s0>=0){if(Shown(NativeKillBoxes.Offhands[s0]))Loose(9);if(NativeKillBoxes.Magic[s0])Loose(9);}
                if(crushed==12&&s0>=0&&!NativeKillBoxes.Magic[s0]&&Shown(NativeKillBoxes.Weapons[s0]))Loose(13);
                int remaining=Math.Max(0,budget-parts-Caps.Count),limit=Math.Min(low?16:40,remaining*45/100);
                if(sourceCount>0||loose.Count>0)
                {
                    // ragdoll.ts: the fragments inherit bodyPartVelocity(death.motion, wound, part, direction); Unity's death velocity
                    // is the source's motion.velocity (body velocity plus external push at death).
                    var o=bind[crushed];var v=Death.velocity;var d=Death.direction;NativeKillEffectRules.DetachedVelocity(-v.x,v.y,v.z,crushed,momentum,-d.x,d.y,d.z,out double ix,out double iy,out double iz);
                    Debris=new NativeKillDebris(sources.ToArray(),sourceColors.ToArray(),sourceCount,loose.ToArray(),loose.Count/3,o[12],o[13],o[14],-dir.x,dir.y,dir.z,ix,iy,iz,2,severity,unchecked((uint)Death.seed),limit,Death.position.y-.85);
                    if(LooseHat>=Debris.Loose)LooseHat=-1;
                }
            }
            CapCells=cells.ToArray();CapColors=cellColors.ToArray();
            int sprayBudget=gibSpray?Math.Max(0,budget-parts-2):Math.Max(0,Math.Max(0,budget-parts-Caps.Count)-(Debris?.Count??0));
            double ground=gib&&floor!=null?floor(-Death.position.x,Death.position.y,Death.position.z):double.NaN;
            // The first fountain is the source's (all of its budget when it is alone); a slice's others share half of it.
            if(emitters.Count>0){var e0=emitters[0];sprayPart=e0.part;sprayPoint=e0.point;sprayNormal=e0.normal;}else sprayPart=-1;
            int extras=Math.Max(0,emitters.Count-1),main=extras>0?sprayBudget/2:sprayBudget;
            if(sprayPart>=0)Spray=new NativeKillSpray(Death.seed,main,quality,gib?KillSprayProfile.Gibber:emitters[0].crush?KillSprayProfile.Crush:KillSprayProfile.Sever,Death.position.y-.85,double.IsNaN(ground)?0:ground);
            if(extras>0)
            {
                int each=Math.Max(0,(sprayBudget-main)/extras);ExtraSprays=new NativeKillSpray[extras];extraPart=new int[extras];extraPoint=new Vector3[extras];extraNormal=new Vector3[extras];
                for(int i=0;i<extras;i++){var e=emitters[i+1];extraPart[i]=e.part;extraPoint[i]=e.point;extraNormal[i]=e.normal;ExtraSprays[i]=new NativeKillSpray(Death.seed*31+i+1,each,quality,e.crush?KillSprayProfile.Crush:KillSprayProfile.Sever,Death.position.y-.85,double.IsNaN(ground)?0:ground);}
            }
            int extraCount=0;foreach(var x in ExtraSprays)extraCount+=x.Count;
            FxCount+=Caps.Count+(Debris?.Count??0)+(Spray?.Count??0)+extraCount;
        }
        private static bool Shown(KillBox[] list){foreach(var b in list)if(!b.Hidden)return true;return false;}
        /// <summary>GibberVfx.step at the end of each fixed physics step: emits from the moving stump (part body pose). The
        /// spray keeps its own clock summed in doubles like ragdoll.ts (elapsed += 1/60), so its rate steps fall on the same
        /// step as the source's.</summary>
        private void StepSpray()
        {
            for(int i=0;i<ExtraSprays.Length;i++)
            {
                if(!Parts.TryGetValue(extraPart[i],out var on))continue;var mx=on.transform.localToWorldMatrix;
                Vector3 px=mx.MultiplyPoint3x4(extraPoint[i]),nx=mx.MultiplyVector(extraNormal[i]);ExtraSprays[i].Step(sprayElapsed,-px.x,px.y,px.z,-nx.x,nx.y,nx.z);
            }
            if(Spray==null||sprayPart<0||!Parts.TryGetValue(sprayPart,out var body)){sprayElapsed+=1/60.0;return;}var m=body.transform.localToWorldMatrix;
            Vector3 p=m.MultiplyPoint3x4(sprayPoint),n=m.MultiplyVector(sprayNormal);Spray.Step(sprayElapsed,-p.x,p.y,p.z,-n.x,n.y,n.z);sprayElapsed+=1/60.0;
        }
        private double sprayElapsed;
    }

    public sealed partial class ArenaPresentation
    {
        // One dynamic mesh for every additive point (NativeKillPoints.shader), one for every ring (NativeBlast additive), and
        // the Singularity cores (a black sphere, NativeBlast opaque). Lists are reused; nothing here allocates per frame.
        private Mesh killPointMesh,killRingMesh,killCoreMesh;private Material killPointMaterial,killRingMaterial,killCoreMaterial;private bool killMaterialsFailed;
        private readonly List<Vector3> killPointVertices=new List<Vector3>(),killRingVertices=new List<Vector3>();private readonly List<Color> killPointColors=new List<Color>(),killRingColors=new List<Color>();
        private readonly List<Vector2> killPointCorners=new List<Vector2>();private readonly List<int> killPointIndices=new List<int>(),killRingIndices=new List<int>();private readonly List<Matrix4x4> killCores=new List<Matrix4x4>();
        private static Vector3[] torusThick,torusThin;private static int[] torusTriangles;private static readonly Bounds KillBounds=new Bounds(Vector3.zero,Vector3.one*4000);
        private int killUsed;private bool killLinear=true;

        private bool EnsureKillMaterials()
        {
            if(killPointMaterial!=null)return true;if(killMaterialsFailed)return false;
            var points=Resources.Load<Shader>("NativeKillPoints");var blast=Resources.Load<Shader>("NativeBlast");if(points==null||blast==null||!points.isSupported||!blast.isSupported){killMaterialsFailed=true;return false;}
            killPointMaterial=new Material(points){name="Kill effect points"};
            killRingMaterial=new Material(blast){name="Kill effect rings",renderQueue=(int)RenderQueue.Transparent+3};killRingMaterial.SetOverrideTag("RenderType","Transparent");
            killRingMaterial.SetFloat("_Src",(float)BlendMode.SrcAlpha);killRingMaterial.SetFloat("_Dst",(float)BlendMode.One);killRingMaterial.SetFloat("_ZWrite",0);killRingMaterial.SetFloat("_Additive",1);
            killCoreMaterial=new Material(blast){name="Kill effect core",renderQueue=(int)RenderQueue.Geometry};killCoreMaterial.SetFloat("_Src",(float)BlendMode.One);killCoreMaterial.SetFloat("_Dst",(float)BlendMode.Zero);killCoreMaterial.SetFloat("_ZWrite",1);killCoreMaterial.SetFloat("_Additive",0);
            killPointMesh=new Mesh{name="Kill effect points",indexFormat=IndexFormat.UInt32};killPointMesh.MarkDynamic();killRingMesh=new Mesh{name="Kill effect rings",indexFormat=IndexFormat.UInt32};killRingMesh.MarkDynamic();
            // ragdoll-vfx.ts SphereGeometry(1,20,14), MeshBasicMaterial 0x080011.
            var positions=new List<Vector3>();var normals=new List<Vector3>();var indices=new List<int>();NativeSourceModels.Sphere(20,14,positions,normals,indices);
            killCoreMesh=new Mesh{name="Singularity core"};killCoreMesh.SetVertices(positions);var colors=new Color[positions.Count];NativeKillEffectRules.Linear(NativeKillSwarm.CoreColor,out double cr,out double cg,out double cb);var core=KillColor(cr,cg,cb,1);for(int i=0;i<colors.Length;i++)colors[i]=core;killCoreMesh.colors=colors;killCoreMesh.SetTriangles(indices,0);killCoreMesh.RecalculateBounds();
            if(torusThick==null){torusThick=Torus(.025f,out torusTriangles);torusThin=Torus(.01f,out _);}
            return true;
        }
        // three.js TorusGeometry(1, tube, 4, 48) in Unity axes (x mirrored, winding reversed).
        private static Vector3[] Torus(float tube,out int[] triangles)
        {
            const int radial=4,tubular=48;var v=new Vector3[(radial+1)*(tubular+1)];
            for(int j=0;j<=radial;j++)for(int i=0;i<=tubular;i++){float u=i/(float)tubular*Mathf.PI*2,w=j/(float)radial*Mathf.PI*2;v[j*(tubular+1)+i]=new Vector3(-(1+tube*Mathf.Cos(w))*Mathf.Cos(u),(1+tube*Mathf.Cos(w))*Mathf.Sin(u),tube*Mathf.Sin(w));}
            var t=new List<int>();for(int j=1;j<=radial;j++)for(int i=1;i<=tubular;i++){int a=(tubular+1)*j+i-1,b=(tubular+1)*(j-1)+i-1,c=(tubular+1)*(j-1)+i,d=(tubular+1)*j+i;t.Add(a);t.Add(d);t.Add(b);t.Add(b);t.Add(d);t.Add(c);}
            triangles=t.ToArray();return v;
        }
        private Color KillColor(double r,double g,double b,float a)=>killLinear?new Color((float)r,(float)g,(float)b,a):new Color((float)NativeKillEffectRules.ToSrgb(r),(float)NativeKillEffectRules.ToSrgb(g),(float)NativeKillEffectRules.ToSrgb(b),a);
        private void BeginKillFrame()
        {
            killPointVertices.Clear();killPointColors.Clear();killPointCorners.Clear();killPointIndices.Clear();killRingVertices.Clear();killRingColors.Clear();killRingIndices.Clear();killCores.Clear();
            killLinear=QualitySettings.activeColorSpace==ColorSpace.Linear;
        }
        private void EndKillFrame()
        {
            if(killPointMesh==null)return;
            killPointMesh.Clear();if(killPointVertices.Count>0){killPointMesh.SetVertices(killPointVertices);killPointMesh.SetColors(killPointColors);killPointMesh.SetUVs(0,killPointCorners);killPointMesh.SetTriangles(killPointIndices,0,false);killPointMesh.bounds=KillBounds;Graphics.DrawMesh(killPointMesh,Matrix4x4.identity,killPointMaterial,0,null,0,null,ShadowCastingMode.Off,false);}
            killRingMesh.Clear();if(killRingVertices.Count>0){killRingMesh.SetVertices(killRingVertices);killRingMesh.SetColors(killRingColors);killRingMesh.SetTriangles(killRingIndices,0,false);killRingMesh.bounds=KillBounds;Graphics.DrawMesh(killRingMesh,Matrix4x4.identity,killRingMaterial,0,null,0,null,ShadowCastingMode.Off,false);}
            foreach(var m in killCores)Graphics.DrawMesh(killCoreMesh,m,killCoreMaterial,0,null,0,null,ShadowCastingMode.Off,false);
        }
        private void ReleaseKillEffects()
        {
            foreach(UnityEngine.Object o in new UnityEngine.Object[]{killPointMesh,killRingMesh,killCoreMesh,killPointMaterial,killRingMaterial,killCoreMaterial})if(o!=null)CapturedAnimation.Release(o);
            killPointMesh=killRingMesh=killCoreMesh=null;killPointMaterial=killRingMaterial=killCoreMaterial=null;killUsed=0;
        }
        /// <summary>RagdollVfx: the swarm's points, rings and core around the presented pelvis.</summary>
        private void KillSwarm(NativeCorpse corpse,float age)
        {
            var swarm=corpse.Swarm;if(swarm==null||!EnsureKillMaterials())return;
            for(int i=0;i<swarm.Total;i++)
            {
                if(swarm.Opacity<=.001)break;Vector3 p=new Vector3(-(float)swarm.Points[i*3],(float)swarm.Points[i*3+1],(float)swarm.Points[i*3+2]);var color=KillColor(swarm.Colors[i*3],swarm.Colors[i*3+1],swarm.Colors[i*3+2],(float)swarm.Opacity);float h=(float)swarm.Size*.5f;int start=killPointVertices.Count;
                for(int k=0;k<4;k++){killPointVertices.Add(p);killPointColors.Add(color);killPointCorners.Add(new Vector2((k==1||k==2)?h:-h,k>=2?h:-h));}
                killPointIndices.Add(start);killPointIndices.Add(start+1);killPointIndices.Add(start+2);killPointIndices.Add(start);killPointIndices.Add(start+2);killPointIndices.Add(start+3);
            }
            Vector3 center=new Vector3(-(float)swarm.Cx,(float)swarm.Cy,(float)swarm.Cz);
            for(int r=0;r<swarm.Rings;r++)
            {
                if(!swarm.RingVisible[r])continue;NativeKillEffectRules.EulerXyz(swarm.RingEuler[r*3],swarm.RingEuler[r*3+1],swarm.RingEuler[r*3+2],out double qx,out double qy,out double qz,out double qw);
                var m=NativeCorpse.Mirror(-center.x,center.y,center.z,qx,qy,qz,qw,swarm.RingScale[r],swarm.RingScale[r],swarm.RingScale[r]);
                NativeKillEffectRules.Linear(swarm.RingColor[r],out double cr,out double cg,out double cb);var color=KillColor(cr,cg,cb,(float)swarm.RingOpacity[r]);var shape=swarm.RingTube[r]>.02?torusThick:torusThin;int start=killRingVertices.Count;
                foreach(var v in shape){killRingVertices.Add(m.MultiplyPoint3x4(v));killRingColors.Add(color);}foreach(int t in torusTriangles)killRingIndices.Add(start+t);
            }
            if(swarm.HasCore&&swarm.CoreScale>.0001)
            {
                killCores.Add(Matrix4x4.TRS(center,Quaternion.identity,Vector3.one*(float)swarm.CoreScale));
            }
        }
        /// <summary>GibberVfx: drops and splat disks. <paramref name="alpha"/>: the body's fade (a kept body being revived);
        /// the opaque drops shrink with it, the splats fade.</summary>
        private void KillSpray(NativeCorpse corpse,float age,float alpha,float now)
        {
            KillSpray(corpse,corpse.Spray,0,age,alpha,now);for(int i=0;i<corpse.ExtraSprays.Length;i++)KillSpray(corpse,corpse.ExtraSprays[i],i+1,age,alpha,now);
        }
        private void KillSpray(NativeCorpse corpse,NativeKillSpray spray,int index,float age,float alpha,float now)
        {
            if(spray==null)return;KillSplats(corpse,spray,index,now);if(alpha<=.001f)return;spray.Update(age);float glow=spray.Glow?.3f:0;
            for(int d=0;d<spray.Drops;d++)
            {
                if(!spray.Shown[d])continue;spray.DropColor(d,out double r,out double g,out double b);
                sourceModels.KillDrop(NativeCorpse.Mirror(spray.Pos[d*3],spray.Pos[d*3+1],spray.Pos[d*3+2],spray.Q[d*4],spray.Q[d*4+1],spray.Q[d*4+2],spray.Q[d*4+3],spray.S[d*3]*alpha,spray.S[d*3+1]*alpha,spray.S[d*3+2]*alpha),new Color((float)r,(float)g,(float)b,1),glow);
            }
            if(spray.PoolOpacity*alpha<=.001)return;NativeKillEffectRules.Linear(NativeKillSpray.PoolColor,out double pr,out double pg,out double pb);var pool=new Color((float)pr,(float)pg,(float)pb,1);
            for(int p=0;p<spray.Pools;p++)
            {
                if(!spray.PoolVisible[p])continue;NativeKillEffectRules.EulerXyz(-Math.PI/2,0,spray.PoolRz[p],out double qx,out double qy,out double qz,out double qw);
                sourceModels.KillPool(NativeCorpse.Mirror(spray.PoolPos[p*3],spray.PoolPos[p*3+1],spray.PoolPos[p*3+2],qx,qy,qz,qw,spray.PoolScale[p*2],spray.PoolScale[p*2+1],1),pool,(float)spray.PoolOpacity*alpha);
            }
        }
        /// <summary>woundCap cross-sections riding on their part (no shadow, as woundCap), and the crushed part's fragments (which
        /// cast one, as CrushDebris) and loose hat.</summary>
        private void KillWound(NativeCorpse corpse,float age,float alpha)
        {
            if(alpha<=.001f)return;
            foreach(var cap in corpse.Caps){var on=corpse.PartMatrix(cap.Part)*cap.Local;for(int i=0;i<cap.Count;i++)sourceModels.KillCap(on*corpse.CapCells[cap.Start+i],corpse.CapColors[cap.Start+i],alpha);}
            var debris=corpse.Debris;if(debris==null)return;
            for(int i=0;i<debris.Chunks;i++)
            {
                debris.Chunk(i,age,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double s);if(s<=1e-5)continue;
                sourceModels.WoundCube(NativeCorpse.Mirror(x,y,z,qx,qy,qz,qw,s,s,s),new Color((float)debris.Colors[i*3],(float)debris.Colors[i*3+1],(float)debris.Colors[i*3+2],1),alpha);
            }
            if(corpse.LooseHat>=0&&corpse.HatModel!=null)
            {
                debris.LooseAt(corpse.LooseHat,age,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double s);
                var start=corpse.HatStart;killLooseHat=Matrix4x4.TRS(new Vector3(-(float)x,(float)y,(float)z),new Quaternion((float)qx,-(float)qy,-(float)qz,(float)qw),Vector3.one*(float)s)*Matrix4x4.Translate(-start)*corpse.Bind[5];
                if(!AuditorHalo(corpse.Death.actor?.Hat,killLooseHat,corpse.Death.at+age,alpha,HaloReduced,0,null,color:corpse.Death.actor?.HatColor))sourceModels.QueueShadowless(corpse.HatModel,Matrix4x4.identity,LooseHatBone,SheldonHood,HatBody(corpse.Death.actor?.Hat,corpse.Death.actor?.HatColor),null,alpha);
            }
        }
        private Matrix4x4 killLooseHat;private Func<int,Matrix4x4> looseHatBone;private Func<int,Matrix4x4> LooseHatBone=>looseHatBone??=b=>killLooseHat;
        /// <summary>voxel-burst: the thrown cubes (Lambert, no emission).</summary>
        private void KillBurst(NativeCorpse corpse,float age)
        {
            var burst=corpse.Burst;if(burst==null)return;
            for(int i=0;i<burst.Count;i++)
            {
                burst.Pose(i,age,out double x,out double y,out double z,out double ex,out double ey,out double ez,out double s);if(s<=1e-5)continue;NativeKillEffectRules.EulerXyz(ex,ey,ez,out double qx,out double qy,out double qz,out double qw);
                NativeKillEffectRules.Linear(burst.Colors[i],out double r,out double g,out double b);sourceModels.KillCube(NativeCorpse.Mirror(x,y,z,qx,qy,qz,qw,s,s,s),new Color((float)r,(float)g,(float)b,1));
            }
        }
        /// <summary>Soul Drift: every visible rig box as a cyan translucent plain box (the dropped weapon's boxes ride on it); a
        /// hat or weapon with round meshes is drawn from its model instead. False when the ghost shader is unavailable.</summary>
        private bool KillGhost(NativeCorpse corpse,Color albedo,float opacity,bool hat,bool weapon)
        {
            // The corpse's dropped weapon body starts at the wrist (NativeCorpse) and draws the weapon model at its own origin, so
            // the weapon's boxes (on the hand socket) are moved back by the socket offset; an off-hand weapon is not dropped.
            if(opacity<=.001f)return true;var drop=corpse.PresentedWeapon;int s=NativeKillBoxes.SpecIndex(corpse.Death.actor?.SourceSpec),hand=s>=0&&NativeKillBoxes.Magic[s]?9:13;
            var socket=hand==9?NativeKillBoxes.LeftHandSocket:NativeKillBoxes.RightHandSocket;var unsocket=Matrix4x4.Translate(new Vector3(socket[0],-socket[1],-socket[2]));
            int h=NativeKillBoxes.HatIndex(corpse.Death.actor?.Hat);bool hatModel=hat&&h>=0&&NativeKillBoxes.HatRound[h],weaponModel=weapon&&s>=0&&NativeKillBoxes.WeaponRound[s];
            if(hatModel&&!sourceModels.QueueGhost(corpse.HatModel,Matrix4x4.identity,corpse.PresentedBone,albedo,opacity))return false;
            if(weaponModel&&!sourceModels.QueueGhost(corpse.LooseModel,drop.Value,null,albedo,opacity))return false;
            for(int i=0;i<corpse.RigCount;i++)
            {
                var box=corpse.RigBoxes[i];if(box.Hidden)continue;Matrix4x4 m;
                if(box.Zone==1){if(!weapon||weaponModel||box.Bone!=hand)continue;m=drop.Value*unsocket*corpse.BoxLocal[i];}
                else if(box.Zone==2){if(!hat||hatModel)continue;m=corpse.PresentedBone(box.Bone)*corpse.BoxLocal[i];}
                else{if(!corpse.ShownBone(box.Bone))continue;m=corpse.PresentedBone(box.Bone)*corpse.BoxLocal[i];}
                if(!sourceModels.KillGhost(m,albedo,new Color(box.Er,box.Eg,box.Eb,1),opacity))return false;
            }
            return true;
        }
    }
}
