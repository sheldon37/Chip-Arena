using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // Hotdog Friends (Core NativeHotdogFriends, owner 2026-11-09): the body falls as the stock impact ragdoll; once it has rested its pieces
    // are gathered into loads (the parts still joined together are one), stilled for good, and carried along the scene's trips, while the door
    // and the friends are drawn in the Top Dog's toon props shader. A piece through the door is shrunk away (its part's scale), and the
    // visual body's bones ride the parts, so the model goes with them. Presentation only: the corpse is the client's own cosmetic body.
    internal sealed partial class NativeCorpse
    {
        internal sealed class FriendsLoad{public Rigidbody[] Bodies;public Vector3[] Offsets;public Quaternion[] Turns;public Vector3[] Scales;public Vector3 Rest;public float Size;public NativeHotdogFriends.Trip Trip;public int Friends;}
        internal sealed class FriendsScene{public Vector3 Door;public float Yaw,Last;public readonly List<FriendsLoad> Loads=new List<FriendsLoad>();}
        internal FriendsScene Friends;private bool friendsFailed;
        /// <summary>The death's kill effect is Hotdog Friends (its body otherwise the stock impact ragdoll); never a kept body (a PvE
        /// teammate waiting to be revived).</summary>
        internal bool HotdogFriends=>Death?.style==NativeHotdogFriends.Style&&!Death.kept;
        /// <summary>Once the body has rested: its pieces gathered into loads, stilled, the door placed beside them and each load's trip
        /// planned. False when there is nothing to carry.</summary>
        internal bool FriendsPlan()
        {
            if(Friends!=null)return true;if(friendsFailed)return false;
            var bodies=new List<Rigidbody>();void Take(NativeCorpse c){if(c==null)return;foreach(var b in c.Parts.Values)if(b!=null)bodies.Add(b);if(c.weapon!=null)bodies.Add(c.weapon);}
            Take(this);if(Twin!=null)Take(Twin);if(bodies.Count==0){friendsFailed=true;return false;}
            // The pieces still joined are carried together.
            var group=new int[bodies.Count];for(int i=0;i<group.Length;i++)group[i]=i;
            int Root(int i){while(group[i]!=i)i=group[i]=group[group[i]];return i;}
            for(int i=0;i<bodies.Count;i++)foreach(var joint in bodies[i].GetComponents<Joint>()){int j=joint.connectedBody!=null?bodies.IndexOf(joint.connectedBody):-1;if(j>=0)group[Root(i)]=Root(j);}
            var loads=new Dictionary<int,List<Rigidbody>>();for(int i=0;i<bodies.Count;i++){int r=Root(i);if(!loads.TryGetValue(r,out var l))loads[r]=l=new List<Rigidbody>();l.Add(bodies[i]);}
            var scene=new FriendsScene();var centre=Vector3.zero;var physicsScene=physics;
            foreach(var l in loads.Values)
            {
                if(scene.Loads.Count>=NativeHotdogFriends.MaxLoads)break;var rest=Vector3.zero;var bounds=new Bounds(l[0].position,Vector3.zero);
                foreach(var b in l){rest+=b.position;foreach(var c in b.GetComponents<Collider>())bounds.Encapsulate(c.bounds);}
                rest/=l.Count;var load=new FriendsLoad{Bodies=l.ToArray(),Offsets=new Vector3[l.Count],Turns=new Quaternion[l.Count],Scales=new Vector3[l.Count],Rest=rest,Size=Mathf.Max(bounds.size.x,bounds.size.z)};
                for(int i=0;i<l.Count;i++){load.Offsets[i]=l[i].transform.position-rest;load.Turns[i]=l[i].transform.rotation;load.Scales[i]=l[i].transform.localScale;}
                load.Trip.Floor=Ground(physicsScene,rest+Vector3.up*.4f,3,bodies,bounds.min.y);
                load.Friends=NativeHotdogFriends.Friends(load.Size);scene.Loads.Add(load);centre+=rest;
            }
            if(scene.Loads.Count==0){friendsFailed=true;return false;}
            centre/=scene.Loads.Count;float radius=0,floorY=0;foreach(var l in scene.Loads){var d=l.Rest-centre;d.y=0;radius=Mathf.Max(radius,d.magnitude+l.Size*.5f);floorY+=l.Trip.Floor;}floorY/=scene.Loads.Count;
            // The door: the first of the spots round the pieces with floor under it and nothing in the way.
            uint seed=(uint)Death.seed*2654435761u;bool placed=false;
            for(int k=0;k<8&&!placed;k++)
            {
                var spot=NativeHotdogFriends.DoorSpot(new NVector(centre.x,floorY,centre.z),radius,seed,k,out float yaw);var at=new Vector3(spot.X,spot.Y,spot.Z);
                float underY=Ground(physicsScene,at+Vector3.up*.6f,1.5f,bodies,float.NaN);if(float.IsNaN(underY)||Mathf.Abs(underY-floorY)>.3f)continue;
                var from=new Vector3(centre.x,floorY+.15f,centre.z);var to=new Vector3(at.x,underY+.15f,at.z);
                if(Blocked(physicsScene,from,to,bodies))continue;
                scene.Door=new Vector3(at.x,underY,at.z);scene.Yaw=yaw;placed=true;
            }
            if(!placed){var spot=NativeHotdogFriends.DoorSpot(new NVector(centre.x,floorY,centre.z),radius,seed,0,out float yaw);scene.Door=new Vector3(spot.X,floorY,spot.Z);scene.Yaw=yaw;}
            // The nearest load first; each friend through the door after its load.
            scene.Loads.Sort((a,b)=>Flat(a.Rest-scene.Door).CompareTo(Flat(b.Rest-scene.Door)));
            for(int i=0;i<scene.Loads.Count;i++)
            {
                var l=scene.Loads[i];float floorAt=l.Trip.Floor;l.Trip=NativeHotdogFriends.Plan(i,Mathf.Max(.1f,Flat(l.Rest-scene.Door)-l.Size*.4f),floorAt);
                scene.Last=Mathf.Max(scene.Last,NativeHotdogFriends.InAt(l.Trip,l.Friends-1));
            }
            // Still the body for good: the scene moves it from here, untouched by its physics or its smoothing.
            foreach(var b in bodies)b.isKinematic=true;frozen=true;rememberedMask=0;if(Twin!=null){Twin.frozen=true;Twin.rememberedMask=0;}
            Friends=scene;return true;
        }
        private static float Flat(Vector3 v){v.y=0;return v.magnitude;}
        private static readonly RaycastHit[] friendHits=new RaycastHit[16];
        // The floor under a point, past the body's own pieces (a head lying on the torso stands on the floor, not the torso); `none` if nothing.
        private static float Ground(PhysicsScene scene,Vector3 from,float reach,List<Rigidbody> bodies,float none)
        {
            int n=scene.Raycast(from,Vector3.down,friendHits,reach,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);float best=float.NegativeInfinity;
            for(int i=0;i<n;i++){var h=friendHits[i];if(h.collider==null||h.collider.attachedRigidbody!=null&&bodies.Contains(h.collider.attachedRigidbody))continue;if(h.point.y>best)best=h.point.y;}
            return float.IsNegativeInfinity(best)?none:best;
        }
        private static bool Blocked(PhysicsScene scene,Vector3 from,Vector3 to,List<Rigidbody> bodies)
        {
            var d=to-from;float l=d.magnitude;if(l<1e-4f)return false;int n=scene.Raycast(from,d/l,friendHits,l,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++){var h=friendHits[i];if(h.collider!=null&&!(h.collider.attachedRigidbody!=null&&bodies.Contains(h.collider.attachedRigidbody)))return true;}return false;
        }
        /// <summary>The loads where the scene has them at age `t`.</summary>
        internal void FriendsCarry(float t)
        {
            if(Friends==null)return;var mouth=Friends.Door;
            foreach(var l in Friends.Loads)
            {
                NativeHotdogFriends.Load(l.Trip,new NVector(l.Rest.x,l.Rest.y,l.Rest.z),new NVector(mouth.x,mouth.y,mouth.z),t,out var at,out float scale,out float tilt);
                var toDoor=mouth-l.Rest;toDoor.y=0;var side=toDoor.sqrMagnitude>1e-6f?Vector3.Cross(Vector3.up,toDoor.normalized):Vector3.right;
                var turn=Quaternion.AngleAxis(tilt*Mathf.Rad2Deg,side);float s=Mathf.Max(.001f,scale);var centre=new Vector3(at.X,at.Y,at.Z);
                for(int i=0;i<l.Bodies.Length;i++){var b=l.Bodies[i];if(b==null)continue;b.transform.SetPositionAndRotation(centre+turn*(l.Offsets[i]*s),turn*l.Turns[i]);b.transform.localScale=l.Scales[i]*s;}
            }
        }
    }

    public sealed partial class ArenaPresentation
    {
        private readonly Mesh[] friendMeshes=new Mesh[NativeHotdogFriends.WalkFrames*2];private Mesh friendFrame,friendLeaf;private int friendTier=-1;
        private void FriendMeshes()
        {
            int tier=LowGraphics?1:0;if(friendTier==tier&&friendFrame!=null)return;friendTier=tier;
            for(int f=0;f<NativeHotdogFriends.WalkFrames;f++)for(int h=0;h<2;h++)friendMeshes[f*2+h]=FbKitMesh(NativeHotdogFriends.Friend(f,h,tier),Matrix4x4.identity,"Hotdog friend",out _);
            friendFrame=FbKitMesh(NativeHotdogFriends.DoorFrame(tier),Matrix4x4.identity,"Hotdog friends door",out _);friendLeaf=FbKitMesh(NativeHotdogFriends.DoorLeaf(tier),Matrix4x4.identity,"Hotdog friends door leaf",out _);
        }
        /// <summary>PresentDeath, after the body's step: a Hotdog Friends body at effect age `t`, its loads carried and the door and its friends drawn.</summary>
        private void HotdogFriends(NativeCorpse corpse,float t)
        {
            if(corpse==null||!corpse.HotdogFriends||t<NativeHotdogFriends.Settle||!corpse.FriendsPlan())return;var scene=corpse.Friends;corpse.FriendsCarry(t);
            if(!NativeHotdogFriends.Door(t,scene.Last,out float rise,out float open))return;FriendMeshes();
            var turn=Quaternion.Euler(0,scene.Yaw,0);var at=scene.Door-Vector3.up*(NativeHotdogFriends.DoorHeight*1.15f*(1-rise));
            DogDraw(friendFrame,Matrix4x4.TRS(at,turn,Vector3.one));
            DogDraw(friendLeaf,Matrix4x4.TRS(at+turn*new Vector3(-NativeHotdogFriends.DoorWidth*.5f,0,0),turn*Quaternion.Euler(0,-NativeHotdogFriends.DoorSwing*open,0),Vector3.one));
            var mouth=new NVector(scene.Door.x,scene.Door.y,scene.Door.z);
            foreach(var l in scene.Loads)
            {
                var rest=new NVector(l.Rest.x,l.Rest.y,l.Rest.z);
                for(int j=0;j<l.Friends;j++)
                {
                    if(!NativeHotdogFriends.Friend(l.Trip,j,l.Friends,rest,mouth,l.Size,t,out var feet,out var facing,out float walked,out float hold,out float scale))continue;
                    var mesh=friendMeshes[NativeHotdogFriends.Frame(walked+j*.07f)*2+(hold>.5f?1:0)];
                    DogDraw(mesh,Matrix4x4.TRS(new Vector3(feet.X,feet.Y,feet.Z),Quaternion.LookRotation(new Vector3(facing.X,0,facing.Z),Vector3.up),Vector3.one*Mathf.Max(.001f,scale)));
                }
            }
        }
        // The door's puffs as it rises and sinks (BloodFx, with the shared effects renderer).
        private readonly List<(Vector3 at,float rose,float sank)> friendPuffs=new List<(Vector3,float,float)>();
        private void HotdogFriendsFx(float now)
        {
            if(vfx==null)return;friendPuffs.Clear();
            foreach(var c in corpses.Values){var s=c.Friends;if(s==null)continue;float t=c.FxAge(now-c.Death.at);friendPuffs.Add((s.Door,t-NativeHotdogFriends.Settle,t-(NativeHotdogFriends.Over(s.Last)-NativeHotdogFriends.DoorDown)));}
            var dust=NativeVfxDraw.Linear(0xd8cdbd);
            foreach(var p in friendPuffs)foreach(float age in new[]{p.rose,p.sank})
            {
                if(!(age>=0&&age<.5f))continue;float u=age/.5f;
                for(int k=0;k<5;k++){float a=k*1.2566f+u*.6f;var at=p.at+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(.14f+.22f*u)+Vector3.up*(.04f+.12f*u);vfx.Smoke(at,dust,.55f*(1-u)*(1-u),.12f+.2f*u,u,k*37+(int)(p.at.x*100),p.at.y-.05f);}
            }
        }
    }
}
