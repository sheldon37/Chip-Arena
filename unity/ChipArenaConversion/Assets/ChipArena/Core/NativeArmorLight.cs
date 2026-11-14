using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Pilot armor lights (Resources/NativeArmorLight.cginc): the per-material box tables, built once from the exported
    /// pilot. Each glowing trim (accent parts and armor-* strips: emission tag 2 with emission) is an emitter whose own
    /// voxel boxes shape its profile; every other pilot part is a receiver lit by up to eight emitter boxes in its own bone
    /// frame (the bank's x-mirrored source frame), rest offsets from CapturedRig.Bones; one with no trim in reach (head, jaw,
    /// feet) is a receiver with no boxes, so the whole shell takes the same ARMOR_SHELL and arena shoulder. A neighbour
    /// bone's emitter is held at its rest offset: across a stiff joint always, across a hinge only when it reaches the
    /// shared pivot (shoulder cap, cuff, kneecap), so no pose can leave a phantom light behind.
    /// </summary>
    public static class NativeArmorLight
    {
        public const int MaxBoxes=8,Off=0,Receiver=1,Emitter=2,Held=3;
        /// <summary>Spill window (m), hinge pivot reach (m), voxel merge slack, near-field sigma (scale x smallest half extent, clamped m).</summary>
        public const float Reach=.32f,HingeGap=.07f,MergeSlack=1.6f,SigmaScale=1.8f,SigmaMin=.012f,SigmaMax=.07f;
        /// <summary>Breathing amplitude and the rest height of the pilot's root above its soles (PHASE base).</summary>
        public const float Pulse=.06f,RootHeight=.89f;
        /// <summary>
        /// Mirrors of NativeArmorLight.cginc's ARMOR_SHELL and ARMOR_STAGE_SPILL (ArmorLightChecks keeps them equal): the shell's
        /// flat self-glow scale, so a light body reads white or grey rather than accent-tinted once the spill is on it, and the
        /// diffuse spill's share on an entrance stage, whose ACES and stage lights already light the armour. SternumBoost: the
        /// sternum's light level over its baked emission (its own face and the spill it casts), so the chest's core is the
        /// focal point; the shader reads it from the levels alone.
        /// </summary>
        public const float Shell=.80f,StageSpill=.6f,SternumBoost=1.15f;
        public const string Sternum="sternum";
        /// <summary>
        /// Armor halos (NativeCombatGlow): the chest halos fade as their side turns away from the camera, and every armour halo
        /// keeps a projected diameter of 4.5-70 px, so a far pilot keeps a spark and a close-up or killcam never fogs.
        /// </summary>
        public const float FacingStart=-.10f,FacingEnd=.45f,HaloMinPixels=4.5f,HaloMaxPixels=70f;
        /// <summary>A light box. Severs: a neighbour bone's box across a joint a wound can sever (NativeBodyDamage.Part), Joint that
        /// joint (-1 for the part's own bone), skipped on corpse and regeneration draws whose joint is torn apart (<see cref="CorpseGlow"/>,
        /// <see cref="Detached"/>) so a detached limb carries no light from a trim it left behind.</summary>
        public struct Box {public Vector3 Center,Half;public float Level,Sigma;public bool Severs;public int Joint;}
        public sealed class Section
        {
            public string Part;public int Bone;public bool Emitter;public float Level;public Vector3 Min,Max;
            public readonly List<(Vector3 min,Vector3 max)> Voxels=new List<(Vector3 min,Vector3 max)>();
        }
        /// <summary>A material's table. SeverJoints: bit j for each severable joint j its Severs boxes cross (0: none).</summary>
        public sealed class Plan {public int Role;public float Level,Phase;public int SeverJoints;public Box[] Boxes=Array.Empty<Box>();}

        /// <summary>The joints a wound can sever (the parts NativeBodyDamage.Part hits): the limb, torso or head comes off there.</summary>
        public static bool Severable(int joint)=>joint==CapturedRig.Head||joint==CapturedRig.Spine||joint==CapturedRig.LArm||joint==CapturedRig.RArm||joint==CapturedRig.LElbow||joint==CapturedRig.RElbow||joint==CapturedRig.LHip||joint==CapturedRig.RHip||joint==CapturedRig.LKnee||joint==CapturedRig.RKnee;
        /// <summary>The glow vector of a corpse or regeneration part draw (NativeSourceModels.QueueShadowless) whose SeverJoints
        /// include a torn joint: w below 1 skips Severs boxes. Every other part of that body keeps glow w 1 and all its spill.</summary>
        public static readonly Vector4 CorpseGlow=new Vector4(1,1,1,.5f);
        /// <summary>
        /// A torn joint in a corpse or regeneration draw: the limb across it hidden in that draw (crushed, missing, healed in the
        /// other draw), or its origin more than DetachGap m from where its parent bone holds it (CapturedRig rest offset), plus
        /// DetachSlack m per unit the parent is shrunk below 1 (the corpse's sink and collapse scale each part about its own
        /// joint). Ragdoll joints are locked and projected within .08 m, so an ordinary death keeps its cap, kneecap and band
        /// spill; a severed or Gibber-thrown limb clears the gap within a few frames.
        /// </summary>
        public const float DetachGap=.15f,DetachSlack=.65f;
        public static bool Detached(float error,float parentScale)=>error>DetachGap+DetachSlack*Math.Max(0,1-parentScale);
        /// <summary>Bone <paramref name="joint"/>'s origin in its parent bone's frame (Unity frame, the rig's rest offset).</summary>
        public static Vector3 RestLocal(int joint)=>CapturedRig.Mirror(CapturedRig.Bones[joint].Position);
        /// <summary>Joints that barely move against their parent: spine, chest, neck, head and the clavicles.</summary>
        public static bool Stiff(int child)=>child==CapturedRig.Spine||child==CapturedRig.Chest||child==CapturedRig.Neck||child==CapturedRig.Head||child==CapturedRig.LClav||child==CapturedRig.RClav;
        /// <summary>A bone's rest origin in the rig root, Unity (bank) frame.</summary>
        public static Vector3 Rest(int bone){var p=Vector3.Zero;for(int b=bone;b>=0;b=CapturedRig.Bones[b].Parent)p+=CapturedRig.Bones[b].Position;return CapturedRig.Mirror(p);}
        public static float Sigma(Vector3 half)=>Math.Min(SigmaMax,Math.Max(SigmaMin,SigmaScale*Math.Min(half.X,Math.Min(half.Y,half.Z))));
        private static float Volume(Vector3 min,Vector3 max){var d=Vector3.Max(Vector3.Zero,max-min);return d.X*d.Y*d.Z;}

        /// <summary>
        /// An emitter's light boxes: overlapping voxel boxes merge while their AABB stays within MergeSlack of their union
        /// (shoulder caps, kneecaps, the waist band and the cuffs become one box; the sternum's bar and side bars and the
        /// back light's T stay apart).
        /// </summary>
        public static List<Box> Merge(IReadOnlyList<(Vector3 min,Vector3 max)> voxels,float level)
        {
            var list=new List<(Vector3 min,Vector3 max,float v)>();foreach(var b in voxels)list.Add((b.min,b.max,Volume(b.min,b.max)));
            for(bool merged=true;merged;)
            {
                merged=false;
                for(int i=0;i<list.Count&&!merged;i++)for(int j=i+1;j<list.Count&&!merged;j++)
                {
                    var a=list[i];var b=list[j];Vector3 lo=Vector3.Max(a.min,b.min),hi=Vector3.Min(a.max,b.max);
                    if(hi.X<lo.X-1e-4f||hi.Y<lo.Y-1e-4f||hi.Z<lo.Z-1e-4f)continue;
                    Vector3 umin=Vector3.Min(a.min,b.min),umax=Vector3.Max(a.max,b.max);float union=a.v+b.v-Volume(lo,hi);
                    if(Volume(umin,umax)>MergeSlack*union)continue;
                    list[i]=(umin,umax,union);list.RemoveAt(j);merged=true;
                }
            }
            var boxes=new List<Box>();foreach(var b in list){var half=(b.max-b.min)*.5f;boxes.Add(new Box{Center=(b.min+b.max)*.5f,Half=half,Level=level,Sigma=Sigma(half),Joint=-1});}
            return boxes;
        }
        private static float Gap(Vector3 min,Vector3 max,Vector3 p){var d=Vector3.Max(Vector3.Zero,Vector3.Max(min-p,p-max));return d.Length();}
        private static float Gap(Vector3 amin,Vector3 amax,Vector3 bmin,Vector3 bmax){var d=Vector3.Max(Vector3.Zero,Vector3.Max(amin-bmax,bmin-amax));return d.Length();}

        /// <summary>One plan per section: emitters carry their own voxel boxes, receivers their strongest MaxBoxes emitter boxes.</summary>
        public static Plan[] Build(IReadOnlyList<Section> sections)
        {
            var emitters=new List<(int bone,Box box)>();
            foreach(var s in sections)if(s.Emitter)foreach(var b in Merge(s.Voxels,Light(s)))emitters.Add((s.Bone,b));
            var plans=new Plan[sections.Count];var near=new List<(Box box,float score)>();
            for(int i=0;i<sections.Count;i++)
            {
                var s=sections[i];var plan=new Plan{Phase=Rest(s.Bone).Y+(s.Min.Y+s.Max.Y)*.5f+RootHeight};plans[i]=plan;
                if(s.Emitter)
                {
                    // Level: the baked emission level (the strip test, and the base its boxes' light levels scale the face from).
                    plan.Role=Emitter;plan.Level=s.Level;var own=new List<Box>();float light=Light(s);
                    foreach(var v in s.Voxels){if(own.Count==MaxBoxes)break;var half=(v.max-v.min)*.5f;own.Add(new Box{Center=(v.min+v.max)*.5f,Half=half,Level=light,Sigma=Sigma(half),Joint=-1});}
                    plan.Boxes=own.ToArray();continue;
                }
                near.Clear();int parent=s.Bone>=0?CapturedRig.Bones[s.Bone].Parent:-1;
                foreach(var (bone,box) in emitters)
                {
                    bool child=bone>=0&&CapturedRig.Bones[bone].Parent==s.Bone;if(bone!=s.Bone&&bone!=parent&&!child)continue;
                    bool severs=false;int hinge=-1;
                    if(bone!=s.Bone)
                    {
                        hinge=bone==parent?s.Bone:bone;severs=Severable(hinge);
                        if(!Stiff(hinge)){var pivot=Rest(hinge);var at=box.Center+Rest(bone);if(Gap(at-box.Half,at+box.Half,pivot)>HingeGap)continue;}
                    }
                    var moved=box;moved.Center=box.Center+Rest(bone)-Rest(s.Bone);moved.Severs=severs;moved.Joint=hinge;
                    float d=Gap(moved.Center-moved.Half,moved.Center+moved.Half,s.Min,s.Max);if(d>Reach)continue;
                    float area=4*(box.Half.X*box.Half.Y+box.Half.Y*box.Half.Z+box.Half.X*box.Half.Z);
                    near.Add((moved,box.Level*area/(d*d+area/(float)Math.PI)));
                }
                near.Sort((a,b)=>b.score.CompareTo(a.score));
                int n=Math.Min(MaxBoxes,near.Count);
                plan.Role=Receiver;plan.Boxes=new Box[n];for(int k=0;k<n;k++){plan.Boxes[k]=near[k].box;if(near[k].box.Severs)plan.SeverJoints|=1<<near[k].box.Joint;}
            }
            return plans;
        }
        /// <summary>
        /// The gun or glove held by a hand: a receiver of that hand's cuff (same bone frame as the weapon mesh), lit only in
        /// draws that pass <see cref="HeldGlow"/> (the arena and first-person rigs), never in intros (grip offset) or loose drops.
        /// </summary>
        public static Plan HeldPlan(IReadOnlyList<Section> pilot,int hand)
        {
            var boxes=new List<Box>();
            foreach(var s in pilot)if(s.Emitter&&s.Bone==hand&&s.Part!=null&&s.Part.EndsWith("-cuff",StringComparison.Ordinal))boxes.AddRange(Merge(s.Voxels,s.Level));
            return boxes.Count==0?new Plan():new Plan{Role=Held,Boxes=boxes.ToArray(),Phase=Rest(hand).Y+RootHeight};
        }
        /// <summary>The weapon draw's glow vector that opts a Held material in (w 2; rgb 1 keeps the emission as it is).</summary>
        public static readonly Vector4 HeldGlow=new Vector4(1,1,1,2);
        /// <summary>Weapon models drawn on the plain hand matrix (NativeWeaponPresentation.SourceWeaponBone): guns, shield, gloves.</summary>
        public static bool HandHeld(string model)=>model=="weapon-pistol"||model=="weapon-rifle"||model=="weapon-machinegun"||model=="weapon-shield"||model=="weapon-emperor"||model=="weapon-ansatsuken";
        /// <summary>The global quality from the combat glow quality: off keeps the trims' shading (.5), Low and High as named.</summary>
        public static float Quality(float glow)=>glow>=.75f?1:.5f;
        /// <summary>Breathing off with reduced effects (glow .3) and combat glow off.</summary>
        public static float PulseFor(float glow)=>glow>=.35f?Pulse:0;
        /// <summary>An emitter's light level: its baked level, the sternum's raised by <see cref="SternumBoost"/>.</summary>
        public static float Light(Section s)=>s.Part==Sternum?s.Level*SternumBoost:s.Level;
        /// <summary>A chest halo's opacity factor from the dot of its side's normal with the direction to the camera.</summary>
        public static float HaloFacing(float dotToCamera){float t=Math.Min(1,Math.Max(0,(dotToCamera-FacingStart)/(FacingEnd-FacingStart)));return t*t*(3-2*t);}
        /// <summary>
        /// An armour halo's world size (its quad's diameter) clamped to HaloMinPixels-HaloMaxPixels of projected diameter at view
        /// depth <paramref name="depth"/> (m) under a vertical field of view <paramref name="fovY"/> (degrees) and a screen
        /// <paramref name="screenH"/> pixels high.
        /// </summary>
        public static float HaloPixelClamp(float size,float depth,float fovY,float screenH)
        {
            float pixel=2*Math.Max(.05f,depth)*(float)Math.Tan(fovY*Math.PI/360)/Math.Max(1,screenH);
            return Math.Min(Math.Max(size,HaloMinPixels*pixel),HaloMaxPixels*pixel);
        }
        /// <summary>The pooled spill light's colour (linear): the pilot's ember hue with its brightest channel 1, so a pale accent lights the world in its hue, not pastel.</summary>
        public static Vector3 SpillColor(Vector3 ember){float m=Math.Max(ember.X,Math.Max(ember.Y,ember.Z));return m>1e-5f?ember/m:Vector3.One;}
        /// <summary>
        /// The pooled spill lights' hand-off (<see cref="NativeArmorSpillPicker"/>): a challenger takes a light only when it is
        /// nearer the camera than the holder by SpillMargin of the holder's distance, and by at least SpillMarginMin m; a light
        /// that changes hands fades out, then in, over SpillRamp s each way.
        /// </summary>
        public const float SpillMargin=.15f,SpillMarginMin=.5f,SpillRamp=.15f;
    }
    /// <summary>
    /// Which lit pilots hold the two pooled armor spill lights (NativeCombatGlow): the two nearest the camera, held steady. Run
    /// bob and camera motion reorder pilots at about the same distance from frame to frame; a holder keeps its light until a
    /// challenger is nearer by <see cref="NativeArmorLight.SpillMargin"/> (at least SpillMarginMin m), and a light that changes
    /// hands fades to dark and back up over SpillRamp each way instead of jumping, so a floor pool never flickers between
    /// pilots or changes hue in a frame. A holder no longer offered (dead, the first-person pilot, no rig) drops at once, as
    /// its halos do. Each frame: Begin, Offer every lit pilot, Resolve; then Index and Weight per slot. No allocation after
    /// construction.
    /// </summary>
    public sealed class NativeArmorSpillPicker
    {
        public const int Slots=2;
        private readonly int[] seat={-1,-1},target={-1,-1},index={-1,-1},bestSeat={-1,-1},bestIndex={-1,-1},offeredIndex={-1,-1};
        private readonly float[] weight=new float[Slots],bestScore=new float[Slots];private readonly bool[] offered=new bool[Slots];
        public void Clear(){for(int k=0;k<Slots;k++){seat[k]=target[k]=index[k]=-1;weight[k]=0;}Begin();}
        public void Begin(){for(int k=0;k<Slots;k++){bestSeat[k]=bestIndex[k]=offeredIndex[k]=-1;bestScore[k]=float.MaxValue;offered[k]=false;}}
        /// <summary>A lit pilot this frame: its seat id, its index in the caller's list and its distance (m) from the camera.</summary>
        public void Offer(int id,int listIndex,float distance)
        {
            bool held=false;
            for(int k=0;k<Slots;k++){if(seat[k]==id){offered[k]=true;offeredIndex[k]=listIndex;held=true;}if(target[k]==id)held=true;}
            float score=held?distance-Math.Max(NativeArmorLight.SpillMargin*distance,NativeArmorLight.SpillMarginMin):distance;
            if(score<bestScore[0]){bestScore[1]=bestScore[0];bestSeat[1]=bestSeat[0];bestIndex[1]=bestIndex[0];bestScore[0]=score;bestSeat[0]=id;bestIndex[0]=listIndex;}
            else if(score<bestScore[1]){bestScore[1]=score;bestSeat[1]=id;bestIndex[1]=listIndex;}
        }
        /// <summary>Assigns the slots after <paramref name="dt"/> seconds (0 for a repeated render of the same frame).</summary>
        public void Resolve(float dt)
        {
            float step=Math.Min(1,Math.Max(0,dt)/NativeArmorLight.SpillRamp);
            // A slot keeps a holder that is still among the two best; the rest of the best go to the free slots.
            for(int k=0;k<Slots;k++)target[k]=seat[k]>=0&&(seat[k]==bestSeat[0]||seat[k]==bestSeat[1])?seat[k]:-1;
            for(int b=0;b<Slots;b++)
            {
                int id=bestSeat[b];if(id<0||id==target[0]||id==target[1])continue;
                for(int k=0;k<Slots;k++)if(target[k]<0){target[k]=id;break;}
            }
            for(int k=0;k<Slots;k++)
            {
                if(seat[k]!=target[k])
                {
                    weight[k]=seat[k]<0||!offered[k]?0:Math.Max(0,weight[k]-step);
                    if(weight[k]<=0)seat[k]=target[k];
                }
                else if(seat[k]>=0)weight[k]=Math.Min(1,weight[k]+step);
                index[k]=seat[k]<0?-1:seat[k]==bestSeat[0]?bestIndex[0]:seat[k]==bestSeat[1]?bestIndex[1]:offered[k]?offeredIndex[k]:-1;
            }
        }
        /// <summary>The caller's list index of the pilot slot <paramref name="slot"/> lights this frame, or -1.</summary>
        public int Index(int slot)=>index[slot];
        /// <summary>The seat id holding slot <paramref name="slot"/> (-1 none); a light fading out still names its old holder.</summary>
        public int Seat(int slot)=>seat[slot];
        /// <summary>The slot's light share 0..1 (smoothstep of its fade).</summary>
        public float Weight(int slot){float w=index[slot]<0?0:weight[slot];return w*w*(3-2*w);}
    }
}
