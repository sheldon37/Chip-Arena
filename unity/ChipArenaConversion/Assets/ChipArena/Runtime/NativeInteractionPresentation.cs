using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        private readonly CapturedRig.Pose victimPose=new CapturedRig.Pose();
        private sealed class RepairCell {public Matrix4x4 Target;public Vector3 From;public float Coordinate;}
        private sealed class Revival
        {
            public int Id,Seed;public float At;public bool Place;public NativeDeath Death;
            public Matrix4x4[] Body;public readonly bool[] Missing=new bool[22];public readonly float[] Coordinate=new float[22];
            public Vector3 AxisA,AxisB,FloorMin,FloorMax;public Vector3[] StandPosition;public Quaternion[] StandRotation;
            public readonly List<NativeRepairBox> Boxes=new List<NativeRepairBox>();public readonly List<RepairCell> Cells=new List<RepairCell>();
        }
        private readonly Dictionary<int,Revival> revivals=new Dictionary<int,Revival>();private readonly List<int> oldRevivals=new List<int>();
        private NativeArena reviveArena;private int reviveGeneration=-1;private float reviveClock;
        private static readonly Color RepairCyan=new Color(95/255f,232/255f,1).linear,RepairWhite=new Color(216/255f,251/255f,1).linear;
        private void ClearRevives(){revivals.Clear();oldRevivals.Clear();reviveArena=null;reviveGeneration=-1;}
        private bool IsReviving(NativePilot p,float time)=>p!=null&&p.Hp>0&&p.Seat.connected&&WeaponLoadout?.Pve!=null&&WeaponLoadout.Pve.Rows.ContainsKey(p.Seat.id)&&NativeReviveRules.Active(time,p.RevivedAt)&&!NativeSirenVictimPose.Active(p.Pose,time);
        private bool ReviveVisible(int id,float time)=>sourceModels==null||!IsReviving(LoadoutPilot(id),time)||time-LoadoutPilot(id).RevivedAt>=NativeReviveRules.Scan;
        private void PrepareInteractions(Snapshot snapshot,float time)
        {
            if(Viewer||sourceModels==null)return;
            foreach(var a in snapshot.Actors)
            {
                if(!NativeSirenVictimPose.Active(a,time)||!rigs.TryGetValue(a.Id,out var victim)||!rigs.TryGetValue(a.GrabOwner,out var owner))continue;
                var holder=Array.Find(snapshot.Actors,p=>p.Id==a.GrabOwner);if(holder==null||!holder.Alive)continue;
                NativeSirenVictimPose.Sample(victimPose,time,a.GrabAt,a.GrabCrushAt);NativePoseWriter.Write(victim.Bones,victimPose,"Siren grab victim");
                Vector3 claw=owner.Bones[CapturedRig.RHand].TransformPoint(new Vector3(0,-.36f,.16f));
                victim.Root.transform.SetPositionAndRotation(claw-Vector3.up*(.44f+.05f*(1-NativeSirenVictimPose.Lift(time-a.GrabAt))),Quaternion.Euler(0,holder.Yaw+180,0));
            }
            if(!ReferenceEquals(reviveArena,WeaponLoadout)||reviveGeneration!=(WeaponLoadout?.Generation??-1)||time<reviveClock-.01f)
            {ClearRevives();reviveArena=WeaponLoadout;reviveGeneration=WeaponLoadout?.Generation??-1;}
            reviveClock=time;oldRevivals.Clear();
            foreach(var pair in revivals){var p=LoadoutPilot(pair.Key);if(!IsReviving(p,time)||p.RevivedAt!=pair.Value.At||p.Death?.seed!=pair.Value.Death?.seed)oldRevivals.Add(pair.Key);}
            foreach(int id in oldRevivals)revivals.Remove(id);
            if(WeaponLoadout?.Pve==null)return;
            foreach(var a in snapshot.Actors)
            {
                var p=LoadoutPilot(a.Id);if(!IsReviving(p,time)||!rigs.TryGetValue(a.Id,out var rig))continue;
                if(!revivals.TryGetValue(a.Id,out var r)&&time-p.RevivedAt>=NativeReviveRules.Settle)
                {r=StartRevival(p,rig,time);revivals.Add(a.Id,r);}
                if(r!=null&&r.Place&&time-r.At>=NativeReviveRules.Scan)StandFromBody(r,rig,time-r.At);
            }
        }
        private Revival StartRevival(NativePilot p,CapturedAnimation.Player rig,float time)
        {
            var d=p.Death;var r=new Revival{Id=p.Seat.id,At=p.RevivedAt,Death=d,Seed=d?.seed??p.Seat.id};
            sourceModels.RepairBoxes(p.Seat.spec=="siren"?"siren":"pilot",r.Boxes);
            if(d?.kept==true)
            {
                r.Body=new Matrix4x4[22];float age=Math.Max(0,time-d.at);
                // A kept Siren-crush body is rebuilt from its dropped ragdoll as it lies (NativeSirenDropPresentation.cs).
                if(d.style=="siren-crush"&&SirenDropPose(WeaponLoadout,d,age,r.Body,r.Missing)){}
                // A kept vent-splat rebuilds from the body as drawn (vent-fall.ts; NativeVentDeathPresentation.cs).
                else if(Vent(d.id) is VentBody vent&&vent.Seed==d.seed)for(int b=0;b<22;b++)r.Body[b]=vent.Bones[b];
                else
                {
                    if(!corpses.TryGetValue(d.id,out var corpse)||corpse.Death.seed!=d.seed)
                    {
                        corpse?.Dispose();var temp=captured.Create(transform,-d.id);
                        try {var atDeath=d.actor?.ViewCopy()??new Actor();atDeath.Hp=Math.Max(1,atDeath.Hp);temp.ActorPose(atDeath,d.position-Vector3.up*.865f,d.at);corpse=new NativeCorpse(d,temp,WeaponLoadout);corpses[d.id]=corpse;}
                        finally {temp.Dispose();}
                    }
                    corpse.Step(age);for(int b=0;b<22;b++)r.Body[b]=corpse.Bone(b);
                    int lost=d.wound?.destroyed==true?d.wound.part:d.style=="dev-gibber"?(d.wound?.part??CapturedRig.Head):-1;
                    if(lost>=0)for(int b=0;b<22;b++)r.Missing[b]=BoneWithin(b,lost);
                }
                // Rebuild a missing subtree at its nearest remaining joint, rather than at a severed limb's landing spot.
                for(int b=0;b<22;b++)if(r.Missing[b])
                {int parent=CapturedRig.Bones[b].Parent;while(parent>=0&&r.Missing[parent])parent=CapturedRig.Bones[parent].Parent;if(parent>=0)r.Body[b]=r.Body[parent]*rig.Bones[parent].worldToLocalMatrix*rig.Bones[b].localToWorldMatrix;}
                r.Place=NativeReviveRules.InPlace(NativeArena.Vec(d.position),NativeArena.Vec(rig.Root.transform.position));
                BodyScan(r);
            }
            if(!r.Place)
            {r.AxisA=new Vector3(0,-.85f,0);r.AxisB=new Vector3(0,.95f,0);r.FloorMin=new Vector3(-.55f,-.85f,-.55f);r.FloorMax=new Vector3(.55f,-.85f,.55f);}
            BuildRepairCells(r,rig);return r;
        }
        private static float ScanCoordinate(Vector3 p,Vector3 a,Vector3 b)
        {var d=b-a;return d.sqrMagnitude>1e-6f?Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude):0;}
        private void BodyScan(Revival r)
        {
            Vector3 feet=(r.Body[CapturedRig.LKnee].GetColumn(3)+r.Body[CapturedRig.RKnee].GetColumn(3)+r.Body[CapturedRig.LHip].GetColumn(3)+r.Body[CapturedRig.RHip].GetColumn(3))*.25f;
            Vector3 top=r.Body[r.Missing[CapturedRig.Head]?CapturedRig.Spine:CapturedRig.Head].GetColumn(3),dir=(top-feet).normalized;if(dir.sqrMagnitude<.1f)dir=Vector3.up;
            float lo=float.PositiveInfinity,hi=float.NegativeInfinity;var bounds=new Bounds(feet,Vector3.zero);
            foreach(var box in r.Boxes){Vector3 p=(r.Body[box.Bone]*box.Local).GetColumn(3);float d=Vector3.Dot(p-feet,dir);lo=Math.Min(lo,d);hi=Math.Max(hi,d);bounds.Encapsulate(p);}
            if(!(hi>lo)){lo=0;hi=1;}r.AxisA=feet+dir*(lo-.08f);r.AxisB=feet+dir*(hi+.08f);
            float floor=r.Death.position.y-.85f;r.FloorMin=new Vector3(bounds.min.x-.3f,floor,bounds.min.z-.3f);r.FloorMax=new Vector3(bounds.max.x+.3f,floor,bounds.max.z+.3f);
            for(int b=0;b<22;b++)r.Coordinate[b]=ScanCoordinate(r.Body[b].GetColumn(3),r.AxisA,r.AxisB);
        }
        private void BuildRepairCells(Revival r,CapturedAnimation.Player rig)
        {
            int budget=EffectsQuality<.5f?120:360;var cells=new List<Matrix4x4>();
            foreach(var box in r.Boxes)
            {
                if(r.Place&&!r.Missing[box.Bone])continue;
                var m=(r.Place?r.Body[box.Bone]:rig.Root.transform.worldToLocalMatrix*rig.Bones[box.Bone].localToWorldMatrix)*box.Local;
                Vector3 size=m.lossyScale;int nx=Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(size.x)/.09f),1,20),ny=Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(size.y)/.09f),1,20),nz=Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(size.z)/.09f),1,20);
                for(int x=0;x<nx;x++)for(int y=0;y<ny;y++)for(int z=0;z<nz;z++)
                {if(x>0&&y>0&&z>0&&x<nx-1&&y<ny-1&&z<nz-1)continue;cells.Add(m*Matrix4x4.TRS(new Vector3((x+.5f)/nx-.5f,(y+.5f)/ny-.5f,(z+.5f)/nz-.5f),Quaternion.identity,new Vector3(.74f/nx,.74f/ny,.74f/nz)));}
            }
            int count=Math.Min(budget,cells.Count);for(int i=0;i<count;i++)
            {var m=cells[(int)(i*cells.Count/(float)count)];r.Cells.Add(new RepairCell{Target=m,Coordinate=ScanCoordinate(m.GetColumn(3),r.AxisA,r.AxisB),From=CapturedAnimation.Vec(NativeReviveRules.Scatter(unchecked((uint)r.Seed),i))});}
        }
        private static void StandFromBody(Revival r,CapturedAnimation.Player rig,float age)
        {
            if(r.StandPosition==null)
            {
                r.StandPosition=new Vector3[22];r.StandRotation=new Quaternion[22];
                for(int b=0;b<22;b++){int parent=CapturedRig.Bones[b].Parent;var local=(parent<0?rig.Root.transform.worldToLocalMatrix:r.Body[parent].inverse)*r.Body[b];r.StandPosition[b]=local.GetColumn(3);r.StandRotation[b]=local.rotation;}
            }
            float t=NativeReviveRules.Smooth(NativeReviveRules.Standing(age));
            for(int b=0;b<22;b++){var bone=rig.Bones[b];bone.localPosition=Vector3.Lerp(r.StandPosition[b],bone.localPosition,t);bone.localRotation=Quaternion.Slerp(r.StandRotation[b],bone.localRotation,t);}
        }
        private void RenderRevives(NativeArena arena)
        {
            if(sourceModels==null||arena!=reviveArena)return;float time=reviveClock;
            foreach(var r in revivals.Values)
            {
                if(!rigs.TryGetValue(r.Id,out var rig))continue;var pilot=PilotOf(arena,r.Id);if(!IsReviving(pilot,time))continue;
                float age=Math.Max(0,time-r.At),front=NativeReviveRules.Front(age),glow=NativeReviveRules.Glow(age);bool rise=age>=NativeReviveRules.Scan;
                if(r.Body!=null&&!rise)
                {
                    float alpha=r.Place?1:1-NativeReviveRules.Smooth((age-.15f)/(NativeReviveRules.Dissolve-.15f));
                    string wounded=sourceModels.WoundedPilot(r.Id,r.Death.actor?.Wounds,r.Death.actor?.BodyColor,ForceCompatibilityRendering);
                    Matrix4x4 Bone(int b)=>r.Body[b];bool Old(int b)=>!r.Missing[b]&&(!r.Place||front*1.2f<r.Coordinate[b]);bool Healed(int b)=>r.Place&&front*1.2f>=r.Coordinate[b];
                    sourceModels.QueueShadowless(wounded,Matrix4x4.identity,Bone,Old,BodyTint(pilot.Seat.bodyColor),AccentTint(pilot.Seat.accentColor),alpha);
                    if(r.Place)sourceModels.QueueShadowless("pilot",Matrix4x4.identity,Bone,Healed,BodyTint(pilot.Seat.bodyColor),AccentTint(pilot.Seat.accentColor),alpha);
                    if(!string.IsNullOrEmpty(pilot.Seat.hat)&&(!r.Missing[CapturedRig.Head]||r.Place&&front>=r.Coordinate[CapturedRig.Head]))sourceModels.QueueShadowless(HatModel(pilot.Seat.hat,pilot.Seat.hatColor),Matrix4x4.identity,Bone,null,HatBody(pilot.Seat.hat,pilot.Seat.hatColor),null,alpha);
                }
                bool third=!(FirstPerson&&r.Id==LocalId);
                if(rise&&!third)continue;
                var root=r.Place&&!rise?Matrix4x4.identity:rig.Root.transform.localToWorldMatrix;
                Vector3 a=rise?new Vector3(0,-.85f,0):r.AxisA,b=rise?new Vector3(0,.95f,0):r.AxisB;
                Vector3 min=rise?new Vector3(-.55f,-.85f,-.55f):r.FloorMin,max=rise?new Vector3(.55f,-.85f,.55f):r.FloorMax;
                RepairFrame(root,min,max,age,glow,time);
                if(!rise)RepairSlab(root,a,b,front,r.Place?.7f:.8f,r.Place?.55f:.9f,glow,time);
                foreach(var box in r.Boxes)
                {
                    var matrix=rise?rig.Bones[box.Bone].localToWorldMatrix*box.Local:r.Place?r.Body[box.Bone]*box.Local:root*box.Local;
                    float s=rise?ScanCoordinate((rig.Root.transform.worldToLocalMatrix*matrix).GetColumn(3),a,b):r.Coordinate[box.Bone];
                    float opacity=rise?.32f*(1-NativeReviveRules.Smooth((NativeReviveRules.Standing(age)*1.25f-s)/.3f)):r.Place&&front>=s?.13f+.5f*Mathf.Clamp01(1-(front-s)/.18f):0;
                    if(opacity>0)sourceModels.RepairCube(matrix*Matrix4x4.Scale(Vector3.one*1.07f),RepairCyan,opacity*glow,time);
                }
                if(!rise)foreach(var cell in r.Cells)
                {
                    float k=NativeReviveRules.Flight(age,cell.Coordinate);if(k<=0)continue;float e=1-Mathf.Pow(1-k,3),landed=Mathf.Clamp01(1-(age-NativeReviveRules.Arrival(cell.Coordinate))/.15f);
                    Vector3 at=(Vector3)cell.Target.GetColumn(3)+cell.From*(1-e);var m=root*Matrix4x4.TRS(at,cell.Target.rotation,cell.Target.lossyScale*(.3f+.7f*e));
                    sourceModels.RepairCube(m,k<1?RepairCyan:Color.Lerp(RepairCyan,RepairWhite,landed),(k<1?.5f:.32f+.4f*landed)*glow,time);
                }
            }
        }
        private void RepairFrame(Matrix4x4 root,Vector3 min,Vector3 max,float age,float glow,float time)
        {
            float x=(min.x+max.x)*.5f,z=(min.z+max.z)*.5f,w=max.x-min.x,d=max.z-min.z,y=min.y+.015f,k=glow*(.45f+.2f*Mathf.Sin(age*14));
            void Bar(Vector3 p,Vector3 s)=>sourceModels.RepairCube(root*Matrix4x4.TRS(p,Quaternion.identity,s),RepairCyan,k,time);
            Bar(new Vector3(x,y,min.z),new Vector3(w,.02f,.035f));Bar(new Vector3(x,y,max.z),new Vector3(w,.02f,.035f));Bar(new Vector3(min.x,y,z),new Vector3(.035f,.02f,d));Bar(new Vector3(max.x,y,z),new Vector3(.035f,.02f,d));
        }
        private void RepairSlab(Matrix4x4 root,Vector3 a,Vector3 b,float front,float width,float height,float glow,float time)
        {
            if(front<=0||front>=1)return;Vector3 axis=(b-a).normalized,up=Vector3.up-axis*axis.y;if(up.sqrMagnitude<.001f)up=Vector3.right-axis*axis.x;up.Normalize();Vector3 side=Vector3.Cross(up,axis);
            var basis=Matrix4x4.identity;basis.SetColumn(0,side);basis.SetColumn(1,axis);basis.SetColumn(2,up);basis.SetColumn(3,new Vector4(0,0,0,1));basis=Matrix4x4.Translate(Vector3.Lerp(a,b,front))*basis;
            void Box(float x,float z,float w,float h,float alpha)=>sourceModels.RepairCube(root*basis*Matrix4x4.TRS(new Vector3(x,0,z),Quaternion.identity,new Vector3(w,.012f,h)),RepairCyan,glow*alpha,time);
            Box(0,0,width,height,.14f);Box(0,height*.5f,width,.022f,.95f);Box(0,-height*.5f,width,.022f,.95f);Box(width*.5f,0,.022f,height,.95f);Box(-width*.5f,0,.022f,height,.95f);
        }
    }
}
