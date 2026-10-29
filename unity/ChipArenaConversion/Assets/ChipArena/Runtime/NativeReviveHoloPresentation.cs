using System;
using System.Collections.Generic;
using UnityEngine;
using NVector3=System.Numerics.Vector3;
using NMatrix=System.Numerics.Matrix4x4;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The holographic revive (owner 2026-10-13: "Improve revive effect as well (If user was gibbed / wounded, repair with cyber
    /// holographic lines and player levitates up for a moment, returns to normal after)"), drawn over the C17 revive
    /// (NativeInteractionPresentation.cs: the scan of the kept body, its repair voxels and the stand from its pose, unchanged in
    /// timing). Rules and curves: Core NativeReviveHolo. Presentation only, from replicated data only (RevivedAt, the death
    /// record's wound list, destroyed part and style, and the revive anchor the authority body carries: NativeReviveAnchor.cs),
    /// so the host, guests and the killcam match:
    /// <list type="bullet">
    /// <item>The kept body floats up off its anchor's floor as the scan starts; the scan front sweeps it as a hologram band; a
    /// wounded joint's clean skin solidifies over its craters (dithered in over the wounded mesh) while hardlight voxels fill each
    /// crater cell (the cells the wounds took: NativeSourceModels.WoundHoles); a missing part re-forms as a wireframe, a
    /// hologram, then its skin, its gib dissolving where it lay into voxels that fly back; a split body's halves float back
    /// together first. The blood and ink round it wash away.</item>
    /// <item>The live rig stands up from that pose still floating, a second band passing down it, and sets down exactly on the
    /// authority body by the window's end (the rig's root is lifted for the draw only; the body and collider never move).</item>
    /// <item>A floor ring, a disc under the floating body, the column with rising data streaks and a landing pulse, fading after
    /// it lands. First person: a gentle view rise (none with reduced effects or camera shake off) in a scanline vignette.</item>
    /// <item>An unwounded, whole body gets the lift, the ring and column and the band only.</item>
    /// </list>
    /// Built once when the revive starts (models, holes, delegates); the frames after allocate nothing.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        private sealed class ReviveHolo
        {
            public string Healed,Wounded,Hat;public Color Tint,Accent;public Color? HatTint;public bool Light,Crushed;
            public readonly List<WoundHole> Holes=new List<WoundHole>();public float[] HoleAt=Array.Empty<float>();public readonly NMatrix[] Draw=new NMatrix[22];
            /// <summary>A split body (NativeBisectPresentation): its half models (body 0, 1, hat 0, 1) and each half's pose when the revive began.</summary>
            public string[] Halves;public readonly Matrix4x4[][] HalfFrom={new Matrix4x4[22],new Matrix4x4[22]};
            public float Age,Lift;public Matrix4x4 Up=Matrix4x4.identity;public CapturedAnimation.Player Rig;public bool Washed;
            public Func<int,Matrix4x4> Bone,GibBone,RigBone,Half0,Half1;public Func<int,bool> Old,Missing;public Func<int,float> Heal,Wire,Fill,Band,Gib,HalfAlpha,Grow;
        }
        private readonly ReviveGlow[] reviveGlows=new ReviveGlow[NativeReviveHolo.CraterBudget(NativeReviveHolo.Full)+NativeReviveHolo.MaxAura+4];
        private int ReviveTier=>LowGraphics?NativeReviveHolo.Low:EffectsQuality<.5f?NativeReviveHolo.Reduced:NativeReviveHolo.Full;
        private static NMatrix NumM(Matrix4x4 m)=>new NMatrix(m.m00,m.m10,m.m20,m.m30,m.m01,m.m11,m.m21,m.m31,m.m02,m.m12,m.m22,m.m32,m.m03,m.m13,m.m23,m.m33);
        private static Matrix4x4 UnityM(in NMatrix n)=>new Matrix4x4(new Vector4(n.M11,n.M12,n.M13,n.M14),new Vector4(n.M21,n.M22,n.M23,n.M24),new Vector4(n.M31,n.M32,n.M33,n.M34),new Vector4(n.M41,n.M42,n.M43,n.M44));
        private static readonly Color HoloCyan=new Color(NativeReviveHolo.Cyan.X,NativeReviveHolo.Cyan.Y,NativeReviveHolo.Cyan.Z,1);

        /// <summary>The hologram's look and pieces, decided once as the revive starts: the healed body (the pilot's skin at the
        /// body's distance), the wounded one it repairs, the hat, the crater cells and the cached per-joint curves.</summary>
        private void BeginHolo(Revival r,CapturedAnimation.Player rig,string bodyColor,string accentColor,string hat,string hatColor,int skin=-1)
        {
            var h=new ReviveHolo();r.Holo=h;var d=r.Death;
            Vector3 at=r.Body!=null?(Vector3)r.Body[CapturedRig.Pelvis].GetColumn(3):rig.Root.transform.position;
            string healed=skin>=0?BodyModel(skin,BodyTier(at,false)):BodyAt(r.Id,at,out skin);
            h.Healed=healed;h.Tint=BodyTint(bodyColor);h.Accent=BodyAccent(skin,accentColor,hat,hatColor);
            h.Wounded=r.Body!=null?sourceModels.WoundedPilot(r.Id,d?.actor?.Wounds,d?.actor?.BodyColor,ForceCompatibilityRendering,healed):healed;
            h.Hat=string.IsNullOrEmpty(hat)||hat=="none"?null:HatModel(hat,hatColor);h.HatTint=HatBody(hat,hatColor);
            h.Crushed=d?.style=="siren-crush"||d?.wound?.destroyed==true&&d.wound.kind=="crush";
            if(r.Body!=null&&r.Place)
            {
                for(int b=0;b<22;b++)h.Draw[b]=NumM(r.Body[b]);
                if(h.Wounded!=healed){sourceModels.WoundHoles(h.Wounded,h.Holes);h.HoleAt=new float[h.Holes.Count];for(int i=0;i<h.Holes.Count;i++){var hole=h.Holes[i];h.HoleAt[i]=hole.Bone>=0&&hole.Bone<22?ScanCoordinate(r.Body[hole.Bone].MultiplyPoint3x4(CutVec(hole.Center)),r.AxisA,r.AxisB):0;}}
            }
            if(r.SplitHalves!=null&&r.SplitFrom!=null){h.Halves=r.SplitHalves;for(int k=0;k<2;k++)for(int b=0;b<22;b++)h.HalfFrom[k][b]=r.SplitFrom[k][b];}
            bool missing=false;for(int b=0;b<22;b++)missing|=r.Missing[b];h.Light=!missing&&h.Wounded==healed&&h.Halves==null;
            // The per-joint curves, read by the batches every frame (cached here: no delegate is made per frame).
            h.Bone=b=>h.Up*r.Body[b];h.GibBone=b=>r.Gib!=null?r.Gib[b]:r.Body[b];h.RigBone=b=>h.Rig.Bones[b].localToWorldMatrix;
            h.Old=b=>!r.Missing[b]&&(h.Light||!r.Place||NativeReviveHolo.Heal(h.Age,r.Coordinate[b])<1);h.Missing=b=>r.Missing[b];
            h.Heal=b=>r.Missing[b]?NativeReviveHolo.Solid(h.Age,r.Coordinate[b]):NativeReviveHolo.Heal(h.Age,r.Coordinate[b]);
            h.Wire=b=>r.Missing[b]?NativeReviveHolo.Wire(h.Age,r.Coordinate[b]):0;h.Fill=b=>r.Missing[b]?NativeReviveHolo.Fill(h.Age,r.Coordinate[b]):0;
            h.Band=b=>!r.Missing[b]?1:NativeReviveHolo.Clamp01(NativeReviveHolo.Fill(h.Age,r.Coordinate[b])+NativeReviveHolo.Solid(h.Age,r.Coordinate[b]));
            h.Gib=b=>1-NativeReviveHolo.Smooth((h.Age-.05f)/.35f);
            h.Half0=b=>HalfBone(r,0,b);h.Half1=b=>HalfBone(r,1,b);h.HalfAlpha=b=>1-NativeReviveHolo.Smooth((h.Age-HalfJoined)/.14f);
            // The model's missing parts grow back from the top of each lost subtree as their repair solidifies.
            h.Grow=b=>r.Missing[b]&&(CapturedRig.Bones[b].Parent<0||!r.Missing[CapturedRig.Bones[b].Parent])?(r.Place?NativeReviveHolo.Solid(h.Age,r.Coordinate[b]):0):1;
        }
        /// <summary>A split body's halves float back together by this age (s), then its whole skin dithers in over them.</summary>
        private const float HalfJoined=.4f;
        private static Matrix4x4 HalfBone(Revival r,int half,int b)
        {
            var h=r.Holo;float k=NativeReviveHolo.Smooth((h.Age-.04f)/(HalfJoined-.04f));var from=h.HalfFrom[half][b];var to=r.Body[b];
            var m=Matrix4x4.TRS(Vector3.Lerp(from.GetColumn(3),to.GetColumn(3),k),Quaternion.Slerp(from.rotation,to.rotation,k),Vector3.one);return h.Up*m;
        }
        /// <summary>A split corpse (the first half; its Twin the second) as the revive's body: the halves as they lie, and the
        /// whole body in its death pose (where they float back to). False when it is not split or its halves are not built.</summary>
        private bool HalvesBody(Revival r,NativeCorpse corpse)
        {
            if(corpse?.Twin==null||!(corpse.Bisect is BisectFx fx))return false;if(!fx.Settled)Settle(corpse);if(fx.Models.Count==0)return false;
            string[] names=null;foreach(var any in fx.Models.Values){names=any;break;}if(names==null)return false;
            r.Body=new Matrix4x4[22];for(int b=0;b<22;b++)r.Body[b]=corpse.Bind[b];
            r.SplitHalves=names;r.SplitFrom=new Matrix4x4[2][];for(int h=0;h<2;h++){var half=h==0?corpse:corpse.Twin;r.SplitFrom[h]=new Matrix4x4[22];for(int b=0;b<22;b++)r.SplitFrom[h][b]=half.Bone(b);}
            return true;
        }

        /// <summary>One revive's frame: the body on the floor being repaired (or dissolving, when the pilot came back elsewhere),
        /// then the live rig standing and setting down, and the hardlight round it. `third`: not the pilot's own first-person view.</summary>
        private void RenderRevival(NativeArena arena,Revival r,CapturedAnimation.Player rig,float time,bool third)
        {
            var h=r.Holo;if(h==null)return;float raw=time-r.At,age=Math.Max(0,raw),front=NativeReviveRules.Front(age),glow=NativeReviveRules.Glow(age);bool rise=age>=NativeReviveRules.Scan;
            float lift=NativeReviveHolo.Lift(age);h.Age=age;h.Lift=lift;h.Up=Matrix4x4.Translate(new Vector3(0,lift,0));h.Rig=rig;
            int tier=ReviveTier;bool holo=sourceModels.HoloReady;sourceModels.HoloClock(time);
            Vector3 axis=(r.AxisB-r.AxisA).sqrMagnitude>1e-6f?(r.AxisB-r.AxisA).normalized:Vector3.up;
            if(r.Body!=null&&!rise)
            {
                float alpha=r.Place?1:1-NativeReviveRules.Smooth((age-.15f)/(NativeReviveRules.Dissolve-.15f));
                // The pilot's visual body when it wears one (NativeVisualBodyPresentation.cs VisualReviveBody): it lies, lifts and
                // grows its missing parts back in place of the voxel body, its halves and its hat.
                bool model=VisualReviveBody(1L<<62|(uint)r.Seed,rig,r.Death,h.Bone,h.Grow,alpha);
                if(model){}
                else if(h.Halves!=null&&r.Place)
                {
                    // The halves float back together, then the whole body (healed) dithers in over the seam.
                    if(age<HalfJoined+.14f)for(int k=0;k<2;k++){sourceModels.QueueShadowless(h.Halves[k],Matrix4x4.identity,k==0?h.Half0:h.Half1,null,h.Tint,h.Accent,alpha,h.HalfAlpha);if(h.Halves[2+k]!=null)sourceModels.QueueShadowless(h.Halves[2+k],Matrix4x4.identity,k==0?h.Half0:h.Half1,null,h.HatTint,null,alpha,h.HalfAlpha);}
                    float whole=NativeReviveHolo.Smooth((age-HalfJoined+.1f)/.16f);
                    if(whole>.002f){sourceModels.QueueShadowless(h.Healed,Matrix4x4.identity,h.Bone,null,h.Tint,h.Accent,alpha*whole);if(h.Hat!=null)sourceModels.QueueShadowless(h.Hat,Matrix4x4.identity,h.Bone,null,h.HatTint,null,alpha*whole);}
                }
                else
                {
                    sourceModels.QueueShadowless(h.Wounded,Matrix4x4.identity,h.Bone,h.Old,h.Tint,h.Accent,alpha);
                    if(r.Place&&!h.Light)sourceModels.QueueShadowless(h.Healed,Matrix4x4.identity,h.Bone,null,h.Tint,h.Accent,alpha,h.Heal);
                    // A gib dissolves where it lay while its voxels fly back (NativeInteractionPresentation BuildRepairCells).
                    if(r.Place&&r.Gib!=null&&!h.Crushed)sourceModels.QueueShadowless(h.Wounded,Matrix4x4.identity,h.GibBone,h.Missing,h.Tint,h.Accent,alpha,h.Gib);
                    if(h.Hat!=null)
                    {
                        float hatAlpha=!r.Missing[CapturedRig.Head]?1:r.Place?NativeReviveHolo.Solid(age,r.Coordinate[CapturedRig.Head]):0;
                        if(hatAlpha>.002f)sourceModels.QueueShadowless(h.Hat,Matrix4x4.identity,h.Bone,null,h.HatTint,null,alpha*hatAlpha);
                    }
                }
                if(holo&&r.Place&&!model)
                {
                    // The scan band (the healed body's hologram, lit where the front is), the missing parts' wireframes and holograms.
                    float s=Mathf.Clamp(NativeReviveHolo.Band(age),-.1f,1.15f);Vector3 at=Vector3.LerpUnclamped(r.AxisA,r.AxisB,s)+Vector3.up*lift;var sweep=new Vector4(axis.x,axis.y,axis.z,Vector3.Dot(at,axis));
                    sourceModels.QueueHolo(h.Healed,h.Bone,h.Band,new Color(HoloCyan.r,HoloCyan.g,HoloCyan.b,.6f*glow),sweep,new Vector4(.1f,h.Light?.03f:.05f,h.Light?.25f:.35f,36));
                    if(!h.Light)
                    {
                        if(tier!=NativeReviveHolo.Low)sourceModels.QueueHolo(h.Healed,h.Bone,h.Wire,new Color(HoloCyan.r,HoloCyan.g,HoloCyan.b,.9f*glow),sweep,new Vector4(0,1,0,0),true);
                        sourceModels.QueueHolo(h.Healed,h.Bone,h.Fill,new Color(HoloCyan.r,HoloCyan.g,HoloCyan.b,.45f*glow),sweep,new Vector4(0,.4f,.7f,44));
                        int n=NativeReviveHolo.Crater(h.Holes,h.HoleAt,h.Draw,lift,raw,tier,reviveGlows,0);DrawReviveGlows(n,time);
                    }
                }
            }
            if(rise&&holo&&third&&!VisualBodyPosed(rig))
            {
                // The second pass, down the standing body as it sets down (the rig's root already carries the lift).
                float k=NativeReviveHolo.Smooth(NativeReviveRules.Standing(age));float top=r.FloorY+lift+1.85f*(1-k)+.05f;
                sourceModels.QueueHolo(h.Healed,h.RigBone,null,new Color(HoloCyan.r,HoloCyan.g,HoloCyan.b,.5f*glow),new Vector4(0,1,0,top),new Vector4(.07f,0,.2f,40));
            }
            if(!(rise&&!third))
            {
                var root=r.Place&&!rise?h.Up:rig.Root.transform.localToWorldMatrix;
                Vector3 a=rise?new Vector3(0,-.85f,0):r.AxisA,b=rise?new Vector3(0,.95f,0):r.AxisB;
                if(!rise)RepairSlab(root,a,b,front,r.Place?.7f:.8f,r.Place?.55f:.9f,glow,time);
                if(!holo)foreach(var box in r.Boxes)
                {
                    // Without the hardlight shader: the C17 voxel boxes over each joint, as before.
                    var matrix=rise?rig.Bones[box.Bone].localToWorldMatrix*box.Local:r.Place?h.Up*r.Body[box.Bone]*box.Local:root*box.Local;
                    float s=rise?ScanCoordinate((rig.Root.transform.worldToLocalMatrix*matrix).GetColumn(3),a,b):r.Coordinate[box.Bone];
                    float opacity=rise?.32f*(1-NativeReviveRules.Smooth((NativeReviveRules.Standing(age)*1.25f-s)/.3f)):r.Place&&front>=s?.13f+.5f*Mathf.Clamp01(1-(front-s)/.18f):0;
                    if(opacity>0)sourceModels.RepairCube(matrix*Matrix4x4.Scale(Vector3.one*1.07f),RepairCyan,opacity*glow,time);
                }
                if(!rise&&raw>=0)foreach(var cell in r.Cells)
                {
                    float k=NativeReviveRules.Flight(age,cell.Coordinate);if(k<=0)continue;float e=1-Mathf.Pow(1-k,3),landed=Mathf.Clamp01(1-(age-NativeReviveRules.Arrival(cell.Coordinate))/.15f);
                    // A gib's voxels leave from the floor where it lay and land on the floating body.
                    Vector3 at=(Vector3)cell.Target.GetColumn(3)+cell.From*(1-e);var m=(r.Place?Matrix4x4.Translate(Vector3.up*lift*e):root)*Matrix4x4.TRS(at,cell.Target.rotation,cell.Target.lossyScale*(.3f+.7f*e));
                    sourceModels.RepairCube(m,k<1?RepairCyan:Color.Lerp(RepairCyan,RepairWhite,landed),(k<1?.5f:.32f+.4f*landed)*glow,time);
                }
            }
            // The ring and column: round the body on the floor, closing round the pilot as it stands; on the anchor's floor.
            Vector3 floor;float radius;ReviveFloor(r,rig,age,out floor,out radius);
            if(holo)DrawReviveGlows(NativeReviveHolo.Halo(raw,unchecked((uint)r.Seed),CutNum(floor),radius,lift,tier,!third,reviveGlows,0),time);
            else if(!rise||third)RepairFrame(Matrix4x4.identity,floor-new Vector3(radius,0,radius),floor+new Vector3(radius,0,radius),age,glow,time);
            if(!h.Washed&&raw>=NativeReviveHolo.WashAt){h.Washed=true;blood.Splats.FadeNear(CutNum(floor),Math.Max(NativeReviveHolo.WashRadius,radius+.3f),FieldClock(arena),NativeReviveHolo.WashFor);}
        }
        /// <summary>The floor point and column radius: the body's footprint on its anchor's floor while it lies, then the rig.</summary>
        private static void ReviveFloor(Revival r,CapturedAnimation.Player rig,float age,out Vector3 floor,out float radius)
        {
            var root=rig.Root.transform.position;var stand=new Vector3(root.x,r.FloorY,root.z);
            if(!r.Place||r.Body==null){floor=stand;radius=NativeReviveHolo.StandRadius;return;}
            var lying=new Vector3((r.FloorMin.x+r.FloorMax.x)*.5f,r.FloorY,(r.FloorMin.z+r.FloorMax.z)*.5f);float wide=Mathf.Max(NativeReviveHolo.StandRadius,Mathf.Max(r.FloorMax.x-r.FloorMin.x,r.FloorMax.z-r.FloorMin.z)*.5f);
            float k=NativeReviveHolo.Smooth(NativeReviveRules.Standing(age));floor=Vector3.Lerp(lying,stand,k);radius=Mathf.Lerp(wide,NativeReviveHolo.StandRadius,k);
        }
        private void DrawReviveGlows(int n,float time)
        {
            for(int i=0;i<n;i++)
            {
                var g=reviveGlows[i];var m=UnityM(g.Matrix);var c=new Color(g.Color.X,g.Color.Y,g.Color.Z,g.Color.W);
                if(g.Kind==NativeReviveHolo.Cube)sourceModels.RepairCube(m,new Color(c.r,c.g,c.b),c.a,time);
                else if(g.Kind==NativeReviveHolo.Ring)sourceModels.ReviveGlow(g.Kind,m,c,new Vector4(0,1,0,m.m13),new Vector4(0,1,0,0));
                else sourceModels.ReviveGlow(g.Kind,m,c,new Vector4(0,1,0,m.m13+NativeReviveHolo.ColumnHeight*.38f),new Vector4(-1.1f,.1f,1,16));
            }
        }
        /// <summary>The ring and column fading after the body has landed (the revive itself is over: stateless, from RevivedAt).</summary>
        private void ReviveLinger(NativeArena arena,float time)
        {
            if(WeaponLoadout?.Pve==null)return;
            foreach(var p in WeaponLoadout.Pilots)
            {
                if(p==null||p.Hp<=0||!p.Seat.connected||!WeaponLoadout.Pve.Rows.ContainsKey(p.Seat.id))continue;float age=time-p.RevivedAt;
                if(!(age>=NativeReviveHolo.End&&age<NativeReviveHolo.Total)||!rigs.TryGetValue(p.Seat.id,out var rig))continue;
                var root=rig.Root.transform.position;var floor=new Vector3(root.x,root.y-NativeLocomotionRules.HalfHeight-.015f,root.z);
                DrawReviveGlows(NativeReviveHolo.Halo(age,unchecked((uint)(p.Death?.seed??p.Seat.id)),CutNum(floor),NativeReviveHolo.StandRadius,0,ReviveTier,FirstPerson&&p.Seat.id==LocalId,reviveGlows,0),time);
            }
        }
        /// <summary>The live rig's lift for the draw (its root only; the authority body and collider never move). Not the
        /// first-person viewer's own rig: their view rises instead (<see cref="ReviveView"/>).</summary>
        private void ReviveLift(Revival r,CapturedAnimation.Player rig,float time,int id)
        {
            if(FirstPerson&&id==LocalId)return;float lift=NativeReviveHolo.Lift(time-r.At);if(lift>0)rig.Root.transform.position+=Vector3.up*lift;
        }

        // ---- first person: the view's gentle rise and the scanline vignette ----
        private Mesh reviveQuad;private Material reviveScreen;private bool reviveScreenFailed;private static readonly int StrengthId=Shader.PropertyToID("_Strength"),ScreenTimeId=Shader.PropertyToID("_ReviveTime");
        /// <summary>
        /// The local pilot's first-person view while it is revived (ConversionGame, after the camera is placed and aimed: the
        /// aim never moves): it rises by NativeReviveHolo.ViewLift (none when `still`: reduced effects or camera shake off) and
        /// a scanline vignette frames it. The authority eye and body never move.
        /// </summary>
        public void ReviveView(Camera camera,int id,float time,bool still)
        {
            if(camera==null||!FirstPerson||WeaponLoadout?.Pve==null)return;var p=LoadoutPilot(id);
            if(p==null||p.Hp<=0||!WeaponLoadout.Pve.Rows.ContainsKey(id))return;float age=time-p.RevivedAt;if(!(age>=0&&age<NativeReviveHolo.Total))return;
            float lift=NativeReviveHolo.ViewLift(age,still);if(lift>0)camera.transform.position+=Vector3.up*lift;
            float strength=NativeReviveHolo.Vignette(age)*(still?.7f:1);if(strength<=.002f)return;
            if(reviveScreen==null&&!reviveScreenFailed)
            {
                var shader=Resources.Load<Shader>("NativeReviveScreen");if(shader==null){reviveScreenFailed=true;return;}
                reviveScreen=new Material(shader){name="Revive vignette"};
                reviveQuad=new Mesh{name="Revive vignette"};reviveQuad.SetVertices(new List<Vector3>{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)});
                reviveQuad.SetUVs(0,new List<Vector2>{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)});reviveQuad.SetTriangles(new[]{0,2,1,0,3,2},0);reviveQuad.RecalculateBounds();reviveQuad.bounds=new Bounds(Vector3.zero,Vector3.one*1000);
            }
            if(reviveScreen==null)return;
            float d=Mathf.Max(camera.nearClipPlane*1.6f,.03f),tall=2*d*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad)*1.04f;
            reviveScreen.SetFloat(StrengthId,strength);reviveScreen.SetFloat(ScreenTimeId,time);
            Graphics.DrawMesh(reviveQuad,camera.transform.localToWorldMatrix*Matrix4x4.TRS(new Vector3(0,0,d),Quaternion.identity,new Vector3(tall*camera.aspect,tall,1)),reviveScreen,0,camera,0,null,UnityEngine.Rendering.ShadowCastingMode.Off,false);
        }
        private void ReleaseReviveScreen(){if(reviveQuad!=null)CapturedAnimation.Release(reviveQuad);if(reviveScreen!=null)CapturedAnimation.Release(reviveScreen);reviveQuad=null;reviveScreen=null;}

        // ---- the Dev Sandbox preview: a stand-in revived on a loop before you ----
        /// <summary>Dev Sandbox "Preview Revive": every <see cref="ReviveGalleryLoop"/> s a stand-in of you, 3.2 m ahead, lies
        /// headshot-wounded, heavily wounded, gibbed (its head off) or split in two, and is revived; the bodies cycle the Standard,
        /// Sheldon Clothes and Prototype skins in your hat. Nothing reaches the match.</summary>
        public bool ReviveGallery;public const float ReviveGalleryLoop=3.4f,ReviveGalleryHold=.6f;
        private Revival galleryRevival;private CapturedAnimation.Player galleryReviveRig;private NativeCorpse galleryReviveBody;private Actor galleryReviveActor;private Vector3 galleryReviveAt;private int galleryReviveRun;
        private const int GalleryReviveId=990800;
        private static readonly string[] GalleryReviveKinds={"headshot","heavily wounded","gibbed","split"};
        /// <summary>The preview's current stand-in (for the overlay): its kind and skin.</summary>
        public string ReviveGalleryLabel{get;private set;}
        private void ClearReviveGallery(){galleryReviveBody?.Dispose();galleryReviveBody=null;galleryReviveRig?.Dispose();galleryReviveRig=null;galleryRevival=null;galleryReviveActor=null;}
        private void RevivePreview(NativeArena arena)
        {
            if(!ReviveGallery||!arena.Sandbox){if(galleryReviveRig!=null)ClearReviveGallery();return;}
            var me=arena.Local;if(me?.Body==null||sourceModels==null)return;float now=arena.Time;
            if(galleryRevival==null||now-galleryRevival.At>=ReviveGalleryLoop-ReviveGalleryHold||now<galleryRevival.At-ReviveGalleryHold-.1f)
            {
                ClearReviveGallery();int run=galleryReviveRun++;int kind=run%4,skin=new[]{NativeBodySkins.StandardIndex,NativeBodySkins.SheldonIndex,NativeBodySkins.PrototypeIndex}[(run/4)%3];
                ReviveGalleryLabel=GalleryReviveKinds[kind]+" · "+NativeBodySkins.Labels[skin];
                float yaw=me.Yaw;var facing=Quaternion.Euler(0,yaw,0)*Vector3.forward;var at=me.Body.position+facing*3.2f;galleryReviveAt=at;
                var actor=me.Pose.ViewCopy();actor.Hp=Math.Max(1,actor.Hp);actor.Yaw=yaw+180;actor.Wounds=GalleryWounds(kind);
                var standing=actor.ViewCopy();standing.Wounds=Array.Empty<NativeWound>();galleryReviveActor=standing;
                var death=new NativeDeath{id=GalleryReviveId,seed=0x2e71+run,at=now-1.6f,position=at,yaw=yaw+180,style=kind==2?"dev-gibber":"impact-ragdoll",model="pilot",damageKind=kind==3?"hazard":"bullet",actor=actor,
                    velocity=Quaternion.Euler(0,yaw,0)*new Vector3(0,0,1.2f),wound=kind==2?new NativeWound{id=9,part=CapturedRig.Head,at=now-1.6f,radius=.18f,severity=1,kind="sever",destroyed=true}:null};
                var rig=captured.Create(transform,GalleryReviveId);rig.ActorPose(actor,at-Vector3.up*NativeMeleeSlice.RootHeight,death.at);
                NativeCorpse corpse;
                if(kind==3)
                {
                    var across=Quaternion.Euler(0,yaw,0)*Vector3.right;
                    corpse=SplitCorpse(arena,death,rig,skin,pelvis=>{var q=NativeBisect.Request("preview-revive-split",CutNum(pelvis),CutNum(across),CutNum(-facing*6),unchecked((uint)death.seed*2654435761u),part:1.55f,lift:1.1f,push:.16f,topple:2.6f,hitch:.17f,blood:1.6f,sparks:true);return q;});
                }
                else corpse=new NativeCorpse(death,rig,arena);
                if(corpse==null){rig.Dispose();return;}corpse.Step(1.6f);galleryReviveBody=corpse;galleryReviveRig=rig;
                var r=new Revival{Id=GalleryReviveId,At=now+ReviveGalleryHold,FloorY=at.y-NativeLocomotionRules.HalfHeight,Death=death,Seed=death.seed,Place=true};
                sourceModels.RepairBoxes("pilot",r.Boxes);
                if(!(kind==3&&HalvesBody(r,corpse))){r.Body=new Matrix4x4[22];for(int b=0;b<22;b++)r.Body[b]=corpse.Bone(b);}
                rig.ActorPose(standing,at-Vector3.up*NativeMeleeSlice.RootHeight,now);
                if(kind==2){for(int b=0;b<22;b++)r.Missing[b]=BoneWithin(b,CapturedRig.Head);GibsOf(r,rig);}
                BodyScan(r);BuildRepairCells(r,rig);BeginHolo(r,rig,me.Seat.bodyColor,me.Seat.accentColor,me.Seat.hat,me.Seat.hatColor,skin);
                galleryRevival=r;
            }
            var g=galleryRevival;float age=now-g.At;
            galleryReviveRig.ActorPose(galleryReviveActor,galleryReviveAt-Vector3.up*NativeMeleeSlice.RootHeight,now);
            if(age>=NativeReviveRules.Scan){StandFromBody(g,galleryReviveRig,age);float lift=NativeReviveHolo.Lift(age);if(lift>0)galleryReviveRig.Root.transform.position+=Vector3.up*lift;}
            RenderRevival(arena,g,galleryReviveRig,now,true);
            // The stand-in itself once it stands (a live pilot's rig is drawn with the arena's actors).
            if(age>=NativeReviveRules.Scan&&VisualStandPose(galleryReviveRig,galleryReviveActor,now,true))VisualStandHat(galleryReviveRig,galleryReviveActor,now);
            else if(age>=NativeReviveRules.Scan){var h=g.Holo;h.Rig=galleryReviveRig;sourceModels.Queue(h.Healed,Matrix4x4.identity,h.RigBone,null,body:h.Tint,accent:h.Accent);if(h.Hat!=null)sourceModels.Queue(h.Hat,Matrix4x4.identity,h.RigBone,null,body:h.HatTint);}
            if(age>=NativeReviveHolo.End&&age<NativeReviveHolo.Total)
            {var floor=new Vector3(galleryReviveAt.x,g.FloorY,galleryReviveAt.z);DrawReviveGlows(NativeReviveHolo.Halo(age,unchecked((uint)g.Seed),CutNum(floor),NativeReviveHolo.StandRadius,0,ReviveTier,false,reviveGlows,0),now);}
        }
        /// <summary>The preview's wound lists (NativeBodyDamage, as the authority resolves them; source-frame points).</summary>
        private static NativeWound[] GalleryWounds(int kind)
        {
            int id=1;float at=1;NativeWound W(NativeBodyImpact impact,int part,float damage,bool head,float px,float py,float pz,float dx,float dy,float dz)
            {var w=NativeBodyDamage.Resolve(impact,part,damage,false,head);if(w==null)return null;w.id=id++;w.at=at;at+=.25f;w.point=new V3(px,py,pz);w.direction=new V3(dx,dy,dz).Normalized;return w;}
            var pistol=NativeBodyDamage.Projectile("pistol",120,.045f);var rifle=NativeBodyDamage.Projectile("rifle",210,.045f);var mg=NativeBodyDamage.Projectile("machinegun",160,.045f);var sword=NativeBodyDamage.Melee("sword",1);var fire=NativeBodyDamage.Projectile("fire",40,.3f);
            NativeWound[] list=kind==0?new[]{W(pistol,5,23,true,.02f,.66f,.2f,0,-.05f,-1)}
                :kind==1?new[]{W(pistol,5,23,true,.02f,.64f,.2f,0,0,-1),W(rifle,2,58,false,.1f,.15f,.3f,0,0,-1),W(mg,2,8,false,-.12f,.05f,.3f,0,0,-1),W(sword,7,30,false,.36f,.1f,.2f,-1,0,-.4f),W(pistol,14,23,false,.13f,-.42f,.25f,0,0,-1),W(mg,18,8,false,-.13f,-.4f,.25f,0,0,-1),W(fire,2,22,false,0,.2f,-.3f,0,0,1),W(rifle,11,58,false,-.36f,.1f,.2f,0,0,-1)}
                :kind==2?new[]{W(rifle,2,58,false,.13f,.12f,.3f,0,-.1f,-1)}:new[]{W(pistol,2,23,false,0,.2f,.3f,0,0,-1)};
            return Array.FindAll(list,w=>w!=null);
        }
    }
}
