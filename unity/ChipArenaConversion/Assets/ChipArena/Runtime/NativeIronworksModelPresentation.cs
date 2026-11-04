using System;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The City Ironworks' five on the player's model (docs/ZOMBIE-MODELS.md 4n; owner 2026-11-04: their voxel models reworked). The variants and their kit are
    // Core/NativeZombieLooks'; their gear is modelled in Blender (tools/gear/ironworks_gear.py, the local bank Resources/ChipArenaPackIronworks). Here: the props
    // drawn from that bank in the model's own hands, the crust that sheds, the char and the foil on the shader, the burn-away, the effects' sockets. They hold the
    // model on every tier of the map (the crowd's pose clock thins a far one's poses), and wait for a body rather than flash the voxel model; the voxel body remains
    // only where the bake is absent (cloud builds, --ca-voxel-zombies). Presentation only: no rule, timing, hit volume or network state is read back or changed.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The Ironworks' gear bank (read beside ZombieGearBank).</summary>
        public const string IronworksGearBank="ChipArenaPackIronworks";
        // ---- the carried props' gear (by NativeHordeModels.Prop*; the chain's links stay the instanced code model, 48 to a chain) ----
        private static readonly string[] IronworksGearProps={"stoker-rake","ladle-full","ladle-empty","rigger-hook",null,"rigger-lamp-lit"};
        private bool IronworksOnModel(int kind)=>NativeIronworksKinds.Is(kind)&&ZombieModelsOn&&NativeZombieLooks.Modelled(kind);
        /// <summary>An Ironworks add holds the full tier: the model at every distance (the far ones' poses thinned by their crowd clock).</summary>
        private void IronworksTier(HordeKindState s,int kind){if(IronworksOnModel(kind))s.Tier=NativeHordeModels.Full;}
        /// <summary>The body not made yet (the first frames of a spawn past the pool): nothing drawn, not the voxel model.</summary>
        private bool IronworksWaits(int kind)=>IronworksOnModel(kind);
        /// <summary>Queues a prop: the gear bank's where it has it and the add wears the model, else the code model.</summary>
        private void IronworksPropDraw(int prop,int tier,Matrix4x4 world,Vector4 glow,float flash)
        {
            var gear=prop>=0&&prop<IronworksGearProps.Length?IronworksGearProps[prop]:null;
            if(gear!=null&&ZombieModelsOn&&ZombieGearPropDraw(gear,world,1,flash))return;
            sourceModels.Queue(NativeHordeModels.PropId(prop,tier),world,glow:glow,hitFlash:flash);
        }
        // ---- the props in the model's hands ----
        /// <summary>After PlaceProps: its props carried from the gait's fists to the model's (false while it draws voxel).</summary>
        private bool IronworksPropsOnModel(in HordeMotionInput f,HordeKindState s,int id)
        {
            if(!ZombieModelShift(id,s.Kind,CapturedRig.LHand,out var l)||!ZombieModelShift(id,s.Kind,CapturedRig.RHand,out var r))return false;
            s.Gait.PropsOnModel(f,minionPose,l,r);return true;
        }
        // ---- the crust (Variant.Crust): the "slag-" gear on until the authority's shedAt; the fissure stays ----
        private void IronworksCrustGear(ZombieBody z,bool on)
        {
            if(z==null||z.CrustOn==on)return;z.CrustOn=on;
            foreach(var r in z.Body.All)if(r!=null&&r.name.StartsWith("Gear.slag-",StringComparison.Ordinal)&&!r.name.StartsWith("Gear.slag-fissure",StringComparison.Ordinal))r.gameObject.SetActive(on);
        }
        // ---- the char and the foil (Variant.Char, Foil): the shader's over the rest space, per renderer ----
        private static readonly int ZombieCharId=Shader.PropertyToID("_ZombieChar"),ZombieCharGlowId=Shader.PropertyToID("_ZombieCharGlow"),ZombieFoilId=Shader.PropertyToID("_ZombieFoil");
        private static void IronworksDress(NativeZombieLooks.Variant v,GameObject root,Renderer[] all)
        {
            if(v.Char<=0&&v.Foil<=0)return;bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;var c=WearColor(v.Eyes);if(linear)c=c.linear;zombieBlock??=new MaterialPropertyBlock();
            if(v.Char>0)ZombieRestWrite(root,all);float seed=NativeHordeKinds.Hash(NativeGoreRules.Fnv(v.Id),7)*9;
            foreach(var r in all)
            {
                if(r==null)continue;bool skin=VisualSkinPart(r);if(!(v.Char>0&&skin)&&!(v.Foil>0&&!skin&&!ZombieEyePart(r)))continue;
                r.GetPropertyBlock(zombieBlock);
                if(v.Char>0&&skin){zombieBlock.SetVector(ZombieCharId,new Vector4(v.Char,v.Char>=1?3.2f:1.4f,v.Char>=1?.55f:.3f,seed));zombieBlock.SetVector(ZombieCharGlowId,new Vector4(c.r,c.g,c.b,1));}
                if(v.Foil>0&&!skin)zombieBlock.SetFloat(ZombieFoilId,v.Foil);
                r.SetPropertyBlock(zombieBlock);
            }
        }
        // ---- the hands: closed on what they carry ----
        private static NativeVisualRig.Grasp IronworksGrasp(ZombieBody z,float now)
        {
            if(z.Dying)return default;
            switch(NativeIronworksKinds.Local(z.Kind))
            {
                case NativeIronworksKinds.Stoker:return new NativeVisualRig.Grasp{RightGrip=1,LeftGrip=1};
                case NativeIronworksKinds.Ladleman:return new NativeVisualRig.Grasp{RightGrip=1,LeftGrip=1};
                case NativeIronworksKinds.Rigger:return new NativeVisualRig.Grasp{RightGrip=1};
                case NativeIronworksKinds.Slagback:return new NativeVisualRig.Grasp{RightGrip=.7f,LeftGrip=.7f};
            }
            return default;
        }
        // ---- the burn-away (the Cinderling's: Core NativeMinionFx's per joint) on the model's bones ----
        private void IronworksBurn(int id,int kind,float age)
        {
            if(!zombieBodies.TryGetValue(id,out var z)||z.Kind!=kind)return;z.Hidden=-1;
            for(int j=1;j<z.Joint.Length;j++){if(z.Joint[j]==null)continue;float k=Mathf.Max(.001f,1-(float)NativeMinionFx.Vanish(j,age));z.Joint[j].localScale=z.JointScale[j]*k;}
        }
        // ---- the effects' sockets: where the gear's lights really are, on the model ----
        /// <summary>The Rigger's lens on its hat (head bone frame, Core NativeZombieLooks.RiggerLamp) lit (a prop on the head); false while it draws voxel.</summary>
        private bool IronworksLampOnModel(int id,int kind)
        {
            if(!ZombieModelSocket(id,kind,"head",NativeZombieLooks.RiggerLamp,NativeZombieLooks.RiggerLampAim,out var at,out var aim))return false;
            ZombieGearPropDraw("rigger-lamp-lit",Matrix4x4.TRS(at,Quaternion.LookRotation(aim,Vector3.up),Vector3.one),1,1);return true;
        }
        /// <summary>A rig joint's place on the model (world; its shift from the rig's), the rig's own while it draws voxel.</summary>
        private Vector3 IronworksJoint(CapturedAnimation.Player rig,int id,int kind,int joint)
        {
            var at=rig.Bones[joint].position;if(!ZombieModelShift(id,kind,joint,out var sh))return at;
            return at+rig.Root.transform.TransformVector(new Vector3(-sh.X,sh.Y,sh.Z));
        }
        /// <summary>The effects' chest and head points for an add on the model: its coal, visor, lens or lamp where the gear has them (the effects add their own
        /// offsets for the voxel sculpt's, so those are taken back off).</summary>
        private void IronworksSockets(CapturedAnimation.Player rig,int id,int kind,int z,float scale,float yaw,ref NVector chest,ref NVector head)
        {
            var fwd=new Vector3(Mathf.Sin(yaw*Mathf.Deg2Rad),0,Mathf.Cos(yaw*Mathf.Deg2Rad));
            bool Socket(string bone,NVector local,out Vector3 at)=>ZombieModelSocket(id,kind,bone,local,NVector.UnitZ,out at,out _);
            switch(z)
            {
                case NativeIronworksKinds.Cinderling:if(Socket("spine_05",NativeZombieLooks.CinderCoal,out var coal))chest=ToN(coal-fwd*.12f*scale-new Vector3(0,.02f,0));break;
                case NativeIronworksKinds.Stoker:if(Socket("head",NativeZombieLooks.StokerVisor,out var visor))head=ToN(visor-fwd*.17f*scale-new Vector3(0,.2f*scale,0));break;
                case NativeIronworksKinds.Slagback:if(Socket("spine_05",NativeZombieLooks.SlagFissure,out var fissure))chest=ToN(fissure+fwd*.24f*scale-new Vector3(0,.12f,0));break;
                case NativeIronworksKinds.Ladleman:if(Socket("head",NativeZombieLooks.LadleLens,out var lens))head=ToN(lens-fwd*.15f*scale-new Vector3(0,.13f*scale,0));break;
                case NativeIronworksKinds.Rigger:if(Socket("head",NativeZombieLooks.RiggerLamp,out var lamp))head=ToN(lamp-fwd*.17f*scale-new Vector3(0,.34f*scale,0));break;
            }
        }
    }
}
