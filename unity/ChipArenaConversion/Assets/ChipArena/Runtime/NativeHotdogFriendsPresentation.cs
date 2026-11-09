using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // Hotdog Friends (Core NativeHotdogFriends, owner 2026-11-09): the body falls as the stock impact ragdoll; once it has rested its shown
    // pieces are gathered into loads (the parts still joined together are one; a crushed part, left to its fragments, and a weapon with no
    // model are nothing to carry), stilled for good, and carried along the scene's trips, while the door and the friends are drawn in the Top
    // Dog's toon props shader. Each friend stands under one of its load's own pieces. A piece stuffed through the door is shrunk away (its
    // part's scale), and the visual body's bones ride the parts and take that scale piece by piece (NativeVisualBodyPresentation.cs
    // VisualCorpseShown, VisualCorpseFriendsScale), so the model goes through with them. Presentation only: the corpse is the client's own
    // cosmetic body.
    internal sealed partial class NativeCorpse
    {
        internal sealed class FriendsLoad{public Rigidbody[] Bodies;public Vector3[] Offsets;public Quaternion[] Turns;public Vector3[] Scales;public Bounds[] Pieces;public Vector3 Rest;public float Size;public NativeHotdogFriends.Trip Trip;public int Friends;public Vector3[] Slots;public bool Waves;}
        internal sealed class FriendsScene{public Vector3 Door;public float Yaw,Last;public readonly List<FriendsLoad> Loads=new List<FriendsLoad>();}
        internal FriendsScene Friends;private bool friendsFailed;
        /// <summary>The death's kill effect is Hotdog Friends (its body otherwise the stock impact ragdoll); never a kept body (a PvE
        /// teammate waiting to be revived).</summary>
        internal bool HotdogFriends=>Death?.style==NativeHotdogFriends.Style&&!Death.kept;
        /// <summary>This body's pieces are being carried by a Hotdog Friends scene (its own, or its split body's first half's).</summary>
        internal bool FriendsCarried=>Friends!=null||Host?.Friends!=null;
        /// <summary>How much of its size ragdoll part `part` has left (1; less as it goes through the door).</summary>
        internal float FriendsShare(int part)=>Parts.TryGetValue(part,out var b)&&b!=null?b.transform.localScale.x:1;
        /// <summary>Once the body has rested: its pieces gathered into loads, stilled, the door placed beside them and each load's trip
        /// planned. False when there is nothing to carry.</summary>
        internal bool FriendsPlan()
        {
            if(Friends!=null)return true;if(friendsFailed)return false;
            var bodies=new List<Rigidbody>();
            void Take(NativeCorpse c)
            {
                if(c==null)return;
                // Only what is drawn: a crushed part is its fragments; a weapon without a loose model shows nothing.
                foreach(var pair in c.Parts)if(pair.Value!=null&&!c.IsCrushed(pair.Key)&&pair.Value.gameObject.activeInHierarchy)bodies.Add(pair.Value);
                if(c.weapon!=null&&c.weapon.gameObject.activeInHierarchy&&c.LooseModel!=null)bodies.Add(c.weapon);
            }
            Take(this);if(Twin!=null)Take(Twin);if(bodies.Count==0){friendsFailed=true;return false;}
            // The pieces still joined are carried together.
            var group=new int[bodies.Count];for(int i=0;i<group.Length;i++)group[i]=i;
            int Root(int i){while(group[i]!=i)i=group[i]=group[group[i]];return i;}
            for(int i=0;i<bodies.Count;i++)foreach(var joint in bodies[i].GetComponents<Joint>()){int j=joint.connectedBody!=null?bodies.IndexOf(joint.connectedBody):-1;if(j>=0)group[Root(i)]=Root(j);}
            var loads=new Dictionary<int,List<Rigidbody>>();for(int i=0;i<bodies.Count;i++){int r=Root(i);if(!loads.TryGetValue(r,out var l))loads[r]=l=new List<Rigidbody>();l.Add(bodies[i]);}
            var scene=new FriendsScene();var centre=Vector3.zero;var physicsScene=physics;
            foreach(var l in loads.Values)
            {
                if(scene.Loads.Count>=NativeHotdogFriends.MaxLoads)break;
                // The load's middle is its pieces' own (their colliders' bounds), not their joints'.
                var pieces=new Bounds[l.Count];bool any=false;var bounds=default(Bounds);
                for(int i=0;i<l.Count;i++)
                {
                    var b=l[i];var own=new Bounds(b.position,Vector3.zero);bool has=false;
                    foreach(var c in b.GetComponents<Collider>()){if(c==null||!c.enabled)continue;if(!has){own=c.bounds;has=true;}else own.Encapsulate(c.bounds);}
                    pieces[i]=own;if(!any){bounds=own;any=true;}else bounds.Encapsulate(own);
                }
                var rest=bounds.center;
                var load=new FriendsLoad{Bodies=l.ToArray(),Offsets=new Vector3[l.Count],Turns=new Quaternion[l.Count],Scales=new Vector3[l.Count],Pieces=pieces,Rest=rest,Size=Mathf.Max(bounds.size.x,bounds.size.z)};
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
            // The nearest load first; each friend under one of its load's pieces, through the door after its load.
            scene.Loads.Sort((a,b)=>Flat(a.Rest-scene.Door).CompareTo(Flat(b.Rest-scene.Door)));
            int last=-1;float latest=0;
            for(int i=0;i<scene.Loads.Count;i++)
            {
                var l=scene.Loads[i];float floorAt=l.Trip.Floor;l.Trip=NativeHotdogFriends.Plan(i,scene.Loads.Count,Mathf.Max(.1f,Flat(l.Rest-scene.Door)-l.Size*.4f),l.Friends,floorAt);
                var spots=new NVector[l.Pieces.Length];var halves=new float[l.Pieces.Length];var toDoor=scene.Door-l.Rest;
                var side=Vector3.Cross(Vector3.up,new Vector3(toDoor.x,0,toDoor.z).normalized);
                for(int k=0;k<spots.Length;k++){var c=l.Pieces[k].center-l.Rest;spots[k]=new NVector(c.x,0,c.z);var e=l.Pieces[k].extents;halves[k]=Mathf.Abs(side.x)*e.x+Mathf.Abs(side.z)*e.z;}
                var slots=NativeHotdogFriends.Slots(spots,halves,new NVector(toDoor.x,0,toDoor.z),l.Friends);l.Slots=new Vector3[slots.Length];for(int k=0;k<slots.Length;k++)l.Slots[k]=new Vector3(slots[k].X,0,slots[k].Z);
                for(int j=0;j<l.Friends;j++){float inAt=NativeHotdogFriends.InAt(l.Trip,j,slots[j],false);if(inAt>=latest){latest=inAt;last=i;}}
            }
            // The very last friend of all waves on the step before it goes in, when there is time for it inside the body's five seconds.
            if(last>=0&&NativeHotdogFriends.WaveFits(latest))scene.Loads[last].Waves=true;
            foreach(var l in scene.Loads)for(int j=0;j<l.Friends;j++)scene.Last=Mathf.Max(scene.Last,NativeHotdogFriends.InAt(l.Trip,j,Num(l.Slots[j]),l.Waves&&Waver(l,j)));
            // Still the body for good: the scene moves it from here, untouched by its physics or its smoothing.
            foreach(var b in bodies)b.isKinematic=true;frozen=true;rememberedMask=0;if(Twin!=null){Twin.frozen=true;Twin.rememberedMask=0;}
            Friends=scene;return true;
        }
        /// <summary>Friend `j` of load `l` is the one that waves: its last in, of the load that finishes last.</summary>
        internal static bool Waver(FriendsLoad l,int j)
        {
            if(!l.Waves)return false;int best=0;float at=float.NegativeInfinity;
            for(int k=0;k<l.Friends;k++){float t=NativeHotdogFriends.InAt(l.Trip,k,Num(l.Slots[k]),false);if(t>=at){at=t;best=k;}}
            return j==best;
        }
        internal static NVector Num(Vector3 v)=>new NVector(v.x,v.y,v.z);
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
        /// <summary>The loads where the scene has them at age `t` (each part's scale its share left: through the door, none).</summary>
        internal void FriendsCarry(float t)
        {
            if(Friends==null)return;var mouth=Friends.Door;
            foreach(var l in Friends.Loads)
            {
                NativeHotdogFriends.Load(l.Trip,Num(l.Rest),Num(mouth),t,out var at,out float scale,out float tilt);
                var toDoor=mouth-l.Rest;toDoor.y=0;var side=toDoor.sqrMagnitude>1e-6f?Vector3.Cross(Vector3.up,toDoor.normalized):Vector3.right;
                var turn=Quaternion.AngleAxis(tilt*Mathf.Rad2Deg,side);float s=Mathf.Max(.001f,scale);var centre=new Vector3(at.X,at.Y,at.Z);
                for(int i=0;i<l.Bodies.Length;i++){var b=l.Bodies[i];if(b==null)continue;b.transform.SetPositionAndRotation(centre+turn*(l.Offsets[i]*s),turn*l.Turns[i]);b.transform.localScale=l.Scales[i]*s;}
            }
        }
    }

    public sealed partial class ArenaPresentation
    {
        private readonly Mesh[] friendMeshes=new Mesh[NativeHotdogFriends.Frames*NativeHotdogFriends.ArmKinds];private Mesh friendFrame,friendLeaf;private int friendTier=-1;
        private void FriendMeshes()
        {
            int tier=LowGraphics?1:0;if(friendTier==tier&&friendFrame!=null)return;friendTier=tier;
            for(int f=0;f<NativeHotdogFriends.Frames;f++)for(int a=0;a<NativeHotdogFriends.ArmKinds;a++)friendMeshes[f*NativeHotdogFriends.ArmKinds+a]=FbKitMesh(NativeHotdogFriends.Friend(f,a,tier),Matrix4x4.identity,"Hotdog friend",out _);
            friendFrame=FbKitMesh(NativeHotdogFriends.DoorFrame(tier),Matrix4x4.identity,"Hotdog friends door",out _);friendLeaf=FbKitMesh(NativeHotdogFriends.DoorLeaf(tier),Matrix4x4.identity,"Hotdog friends door leaf",out _);
        }
        /// <summary>PresentDeath, after the body's step: a Hotdog Friends body at effect age `t`, its loads carried and the door and its friends drawn.</summary>
        private void HotdogFriends(NativeCorpse corpse,float t)
        {
            if(corpse==null||!corpse.HotdogFriends||t<NativeHotdogFriends.Settle||!corpse.FriendsPlan())return;var scene=corpse.Friends;corpse.FriendsCarry(t);
            if(!NativeHotdogFriends.Door(t,scene.Last,out float rise,out float open))return;FriendMeshes();
            // The frame shakes as the door slams.
            float slam=t-NativeHotdogFriends.ShutAt(scene.Last)-NativeHotdogFriends.DoorShut,shake=slam>=0&&slam<.2f?.012f*Mathf.Sin(slam*90)*(1-slam/.2f):0;
            var turn=Quaternion.Euler(0,scene.Yaw,0);var at=scene.Door-Vector3.up*(NativeHotdogFriends.DoorHeight*1.15f*(1-rise))+turn*new Vector3(shake,0,0);
            DogDraw(friendFrame,Matrix4x4.TRS(at,turn,Vector3.one));
            DogDraw(friendLeaf,Matrix4x4.TRS(at+turn*new Vector3(-NativeHotdogFriends.DoorWidth*.5f,0,0),turn*Quaternion.Euler(0,-NativeHotdogFriends.DoorSwing*open,0),Vector3.one));
            var mouth=NativeCorpse.Num(scene.Door);
            foreach(var l in scene.Loads)
            {
                var rest=NativeCorpse.Num(l.Rest);
                for(int j=0;j<l.Friends;j++)
                {
                    if(!NativeHotdogFriends.Friend(l.Trip,j,l.Friends,rest,mouth,NativeCorpse.Num(l.Slots[j]),NativeCorpse.Waver(l,j),t,out var p))continue;
                    var mesh=friendMeshes[NativeHotdogFriends.Frame(p)*NativeHotdogFriends.ArmKinds+p.Arms];
                    float k=Mathf.Max(.001f,p.Scale),sq=Mathf.Max(.2f,p.Squash),wide=k/Mathf.Sqrt(sq);
                    var facing=new Vector3(p.Facing.X,0,p.Facing.Z);if(facing.sqrMagnitude<1e-6f)facing=Vector3.forward;
                    var pose=Quaternion.LookRotation(facing,Vector3.up)*Quaternion.Euler(p.Lean*Mathf.Rad2Deg,0,p.Roll*Mathf.Rad2Deg);
                    DogDraw(mesh,Matrix4x4.TRS(new Vector3(p.Feet.X,p.Feet.Y+p.Hop,p.Feet.Z),pose,new Vector3(wide,k*sq,wide)));
                }
            }
        }
        // The scene's puffs (BloodFx, with the shared effects renderer): the door rising and sinking, each friend's skid, each dive through
        // the doorway, and the slam.
        private readonly List<(Vector3 at,float age,float size,int seed)> friendPuffs=new List<(Vector3,float,float,int)>();
        private void HotdogFriendsFx(float now)
        {
            if(vfx==null)return;friendPuffs.Clear();
            foreach(var c in corpses.Values)
            {
                var s=c.Friends;if(s==null)continue;float t=c.FxAge(now-c.Death.at);int seed=(int)(s.Door.x*100);
                float shut=NativeHotdogFriends.ShutAt(s.Last);
                friendPuffs.Add((s.Door,t-NativeHotdogFriends.Settle,1,seed));friendPuffs.Add((s.Door,t-(NativeHotdogFriends.Over(s.Last)-NativeHotdogFriends.DoorDown),1,seed+7));
                friendPuffs.Add((s.Door,t-shut-NativeHotdogFriends.DoorShut,1.3f,seed+13));
                var mouth=NativeCorpse.Num(s.Door);var turn=Quaternion.Euler(0,s.Yaw,0);
                foreach(var l in s.Loads)for(int j=0;j<l.Friends;j++)
                {
                    var slot=l.Slots[j];var stand=new Vector3(l.Rest.x+slot.x,l.Trip.Floor,l.Rest.z+slot.z);
                    friendPuffs.Add((stand,t-(l.Trip.Arrive-NativeHotdogFriends.Skid),.35f,seed+j*31+17));
                    friendPuffs.Add((s.Door+turn*Vector3.forward*.08f,t-(NativeHotdogFriends.InAt(l.Trip,j,NativeCorpse.Num(slot),NativeCorpse.Waver(l,j))-NativeHotdogFriends.Dive*.4f),.3f,seed+j*53+29));
                }
            }
            var dust=NativeVfxDraw.Linear(0xd8cdbd);
            foreach(var p in friendPuffs)
            {
                if(!(p.age>=0&&p.age<.5f))continue;float u=p.age/.5f;int n=p.size>=1?5:3;
                for(int k=0;k<n;k++){float a=k*6.2832f/n+u*.6f+p.seed*.37f;var at=p.at+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*((.14f+.22f*u)*p.size)+Vector3.up*(.04f+.12f*u)*p.size;vfx.Smoke(at,dust,.55f*(1-u)*(1-u),(.12f+.2f*u)*Mathf.Sqrt(p.size),u,k*37+p.seed,p.at.y-.05f);}
            }
        }
    }
}
