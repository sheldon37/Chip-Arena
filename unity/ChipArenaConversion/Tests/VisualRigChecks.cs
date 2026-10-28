#if ARENA_TESTS
using System;
using System.IO;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The visual rig adapter (Core/NativeVisualRig.cs, Runtime/NativeVisualRigBinding.cs; docs/BOZO-INTEGRATION.md step 3).
    // Driven by every captured clip on a synthetic Unreal-mannequin-named skeleton built here (A-pose arms 50 degrees down,
    // bone frames turned off identity, long legs): no pack data. Checked: the bind straightens the limbs to the game's rest
    // and keeps the spine, head and feet as authored; limbs follow the game's bone directions exactly, except that a hand
    // inside the game's shoulder line reaches its chest-relative place (sideways and, below the head, in height, keeping the
    // copied arm's extension) by swinging the whole arm at the shoulder, so the elbow keeps the game's bend; split chains end on
    // the game joint's turn, hands carry the game wrist turn; the root height scales by the leg ratio; planted feet reach the
    // game ankle's ground point and bend toward the game knee; the support hand keeps the game's wrist offset; twist bones
    // take their share of the arm's final roll; the grip is the game socket on the visual wrist (P1); the Standard weapons
    // ride the closed fists (the arm keeps the animation's wrist; long guns raised to aim shoulder their stock) with the
    // straightest wrist the handle allows; every arm keeps a hinged elbow; hands move smoothly through clip transitions, the
    // gun gait's idle, aim, fire and run, and attacks into recovery; the floor fit; the locals rebuild
    // the pose; deterministic; nothing allocated per frame; and the binding names no pack types. The real pack skeleton's
    // drift is measured in Unity on the owner's PC (the local inventory tool), not here.
    internal static class VisualRigChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Visual rig: "+what);count++;}
        private static bool Near(Vector3 a,Vector3 b,float e)=>Vector3.Distance(a,b)<=e;
        private static bool Near(Quaternion a,Quaternion b,float e)=>Math.Abs(Math.Abs(Quaternion.Dot(Quaternion.Normalize(a),Quaternion.Normalize(b)))-1)<=e;
        private static float Angle(Vector3 a,Vector3 b)=>(float)Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(a),Vector3.Normalize(b)),-1f,1f));

        // ---- the synthetic skeleton: world rest positions (Pose body space, floor at -.865) and bone frames off identity ----
        private static readonly string[] Names={"root","pelvis","spine_01","spine_02","spine_03","spine_04","spine_05","neck_01","neck_02","head",
            "clavicle_l","upperarm_l","shoulder_twist_l","upperarm_twist_01_l","lowerarm_l","lowerarm_twist_01_l","hand_l","index_01_l","index_02_l","index_03_l","thumb_01_l","thumb_02_l","thumb_03_l",
            "clavicle_r","upperarm_r","upperarm_twist_01_r","lowerarm_r","lowerarm_twist_01_r","hand_r","index_01_r","index_02_r","index_03_r","thumb_01_r","thumb_02_r","thumb_03_r",
            "thigh_l","thigh_twist_01_l","calf_l","calf_twist_01_l","foot_l","ball_l","thigh_r","calf_r","foot_r","ball_r","ik_hand_gun"};
        private static readonly string[] Parents={"","root","pelvis","spine_01","spine_02","spine_03","spine_04","spine_05","neck_01","neck_02",
            "spine_05","clavicle_l","upperarm_l","upperarm_l","upperarm_l","lowerarm_l","lowerarm_l","hand_l","index_01_l","index_02_l","hand_l","thumb_01_l","thumb_02_l",
            "spine_05","clavicle_r","upperarm_r","upperarm_r","lowerarm_r","lowerarm_r","hand_r","index_01_r","index_02_r","hand_r","thumb_01_r","thumb_02_r",
            "pelvis","thigh_l","thigh_l","calf_l","calf_l","foot_l","pelvis","thigh_r","calf_r","foot_r","root"};
        public static NativeVisualRig.Rig Synthetic(out Vector3[] world)
        {
            const float floor=-.865f,down=50*(float)Math.PI/180;
            float c=(float)Math.Cos(down),s=(float)Math.Sin(down);
            Vector3 P(float x,float y,float z=0)=>new Vector3(x,y+floor,z);
            int n=Names.Length; var at=new Vector3[n]; world=at; var parent=new int[n];
            for(int i=0;i<n;i++) parent[i]=Array.IndexOf(Names,Parents[i]);
            void At(string name,Vector3 p) { at[Array.IndexOf(Names,name)]=p; }
            At("root",P(0,0)); At("pelvis",P(0,.98f,.02f)); At("spine_01",P(0,1.01f,.02f)); At("spine_02",P(0,1.07f,.035f));
            At("spine_03",P(0,1.14f,.04f)); At("spine_04",P(0,1.22f,.035f)); At("spine_05",P(0,1.39f,0)); At("neck_01",P(0,1.49f,-.015f));
            At("neck_02",P(0,1.53f,-.01f)); At("head",P(0,1.57f,0)); At("ik_hand_gun",P(-.4f,1.05f,.05f));
            foreach(bool left in new[]{true,false})
            {
                float x=left?1:-1; string side=left?"_l":"_r";
                Vector3 shoulder=P(x*.14f,1.43f,-.03f),arm=new Vector3(x*c,-s,0);
                At("clavicle"+side,P(x*.01f,1.45f,-.02f)); At("upperarm"+side,shoulder); At("lowerarm"+side,shoulder+arm*.26f);
                At("hand"+side,shoulder+arm*.49f); At("upperarm_twist_01"+side,shoulder+arm*.09f); At("lowerarm_twist_01"+side,shoulder+arm*(.26f+.14f));
                At("thigh"+side,P(x*.09f,.96f,.035f)); At("calf"+side,P(x*.10f,.535f,.03f)); At("foot"+side,P(x*.115f,.11f,0)); At("ball"+side,P(x*.115f,.014f,.115f));
                if(left) { At("shoulder_twist_l",shoulder); At("thigh_twist_01_l",P(.095f,.75f,.033f)); At("calf_twist_01_l",P(.105f,.32f,.015f)); }
                // Fingers curl toward the palm (+z here) a few degrees at rest, as exported hands do.
                Vector3 hand=shoulder+arm*.49f,palm=new Vector3(0,0,1),i0=hand+arm*.09f,i1=i0+Vector3.Normalize(arm+palm*.09f)*.035f;
                At("index_01"+side,i0); At("index_02"+side,i1); At("index_03"+side,i1+Vector3.Normalize(arm+palm*.18f)*.025f);
                Vector3 t0=hand+arm*.03f+palm*.02f,td=Vector3.Normalize(arm+palm*.5f),t1=t0+td*.03f;
                At("thumb_01"+side,t0); At("thumb_02"+side,t1); At("thumb_03"+side,t1+Vector3.Normalize(td+palm*.12f)*.025f);
            }
            // Bone frames: +x down each bone toward its first child (as exported humanoids do), rolled by a per-bone amount.
            var rot=new Quaternion[n];
            for(int i=0;i<n;i++)
            {
                int child=Array.IndexOf(Parents,Names[i]); Vector3 dir=child<0?new Vector3(0,1,0):world[child]-world[i];
                if(dir.LengthSquared()<1e-8f) dir=new Vector3(0,1,0);
                rot[i]=CapturedRig.Rot(Vector3.Normalize(dir),.37f*i)*CapturedRig.FromTo(CapturedRig.X,Vector3.Normalize(dir));
            }
            var lp=new Vector3[n]; var lr=new Quaternion[n];
            for(int i=0;i<n;i++)
            {
                int p=parent[i];
                lr[i]=p<0?rot[i]:Quaternion.Inverse(rot[p])*rot[i];
                lp[i]=p<0?world[i]:Vector3.Transform(world[i]-world[p],Quaternion.Inverse(rot[p]));
            }
            return new NativeVisualRig.Rig(NativeVisualRig.Mannequin(Names,parent,lp,lr));
        }

        public static void Run(string root)
        {
            var rig=Synthetic(out var restWorld);
            Bind(rig,restWorld);Console.WriteLine("PASS visual rig: the bind maps 21 joints (spine 2, chest 3, neck 2 bones), straightens arms and legs to the game's rest, keeps spine, head and feet as authored; leg ratio "+rig.LegRatio.ToString("0.000")+" ("+count+" checks)");
            int poses=Clips(rig,out float support,out float leg,out float grip);
            Console.WriteLine("PASS visual rig: "+poses+" captured poses: limb directions equal the game's (hands inside the shoulder line reach their chest-relative place), chains end on the game turn, wrists keep the roll and ease the bend (max "+(maxWrist*57.3f).ToString("0")+" degrees), the root scales by the leg ratio, twist shares, locals rebuild the pose; planted feet reach the ground point (max leg reach "+leg.ToString("0.00")+"), the support hand keeps the wrist offset (max reach "+support.ToString("0.00")+"), grip "+grip.ToString("0.000")+" m from the game's at most ("+count+" checks)");
            Console.WriteLine("INFO visual rig (synthetic skeleton): hands inside the game's shoulder line "+last.Inside+" ("+last.InsideFull+" fully, "+last.Crossed+" past the middle, at most "+last.Across.ToString("0.00")+" of the game's crossing); support shoulder reached forward "+last.Shrugged+" times, at most "+(last.Shoulder*57.3f).ToString("0")+" degrees; weapon hand gave way "+last.GaveWay+" times, at most "+last.GiveWay.ToString("0.000")+" m; seated fists "+last.Seated+", wrist bend at most "+(last.Bend*57.3f).ToString("0")+" degrees, off the straightest at most "+(last.Straight*57.3f).ToString("0.0")+"; planted feet "+last.Planted+", "+last.LegShort+" at full reach; two-handed support grips (guns, greatsword, hands together) "+last.Supported+", "+last.SupportShort+" at full reach; grip offset mean "+(last.GripSum/Math.Max(1,last.Gripped)).ToString("0.000")+" m; short grips by take: "+string.Join(", ",System.Linq.Enumerable.Select(last.Short,kv=>kv.Key+" "+kv.Value)));
            Console.WriteLine("INFO visual rig (synthetic skeleton): weapons riding the fist at the animation's wrist "+last.Rode+", braced shields "+last.Braced+", shouldered stocks "+last.Butted+", elbows swung off the animation's side over 10 degrees "+last.Swung+" (at most "+(last.Swing*57.3f).ToString("0")+")");
            Console.WriteLine("INFO visual rig (synthetic skeleton): fists moved out of the head or torso "+last.Cleared+" (at most "+last.ClearMost.ToString("0.000")+" m, left inside at most "+last.ClearLeft.ToString("0.000")+" m); with their weapons "+last.SeatCleared+" (at most "+last.SeatClearMost.ToString("0.000")+" m, a seated fist or weapon left inside at most "+last.SeatClearLeft.ToString("0.000")+" m)");
            Transitions(rig,out int frames,out float turnOver,out float moveOver);Console.WriteLine("PASS visual rig: "+frames+" transition frames (idle to each cut and block and back, guns through idle, aim, fire, run): hands and elbows move smoothly ("+count+" checks)");
            Floor(rig);Console.WriteLine("PASS visual rig: the floor fit keeps every visual bone's contact over the floor through the roll ("+count+" checks)");
            Allocation(rig);Console.WriteLine("PASS visual rig: posing, both IKs and the grip allocate nothing per frame and repeat bit for bit ("+count+" checks)");
            Binding(root);Console.WriteLine("PASS visual rig: the Unity binding mirrors at the boundary, writes local turns and the pelvis only, and names no pack types ("+count+" checks)");
            ViewAndDeath(root);Console.WriteLine("PASS visual rig: first person wears the visual body's forearms and hands (placed on the view rig's weapon hand, the device on the wrist), and a pilot's whole-body death scene and ragdoll wear the visual body ("+count+" checks)");
            AccentGlow(root);Console.WriteLine("PASS visual rig: on the visual body the clothing's accent trims glow (pulsing with the block cooldown as the armour halos did) and the voxel joint halos are skipped, the armour spill light coming from the chest core ("+count+" checks)");
            ModelSetting(root);Console.WriteLine("PASS visual rig: Settings · Player model (New, saved) switches every pilot to the classic voxel body and back live, shown only where the visual body is installed ("+count+" checks)");
            BodyFx(root);Console.WriteLine("PASS visual rig: the visual body's hit flash is SourceLit's and its per-pixel spawn-in front, heat and hologram slice are the voxel parts' (Core NativeSpawnIn), wired from SourceCharacter; the pilot gear faces out, clears its chip and follows health and chip select ("+count+" checks)");
            Weapons(root);Console.WriteLine("PASS visual rig: a visual body draws the bake's life-size weapon in the Standard skin's place, its right hand fitted so the weapon's dressing lands on it, the grasp on its handguard and stock, all put back before the next pose ("+count+" checks)");
            Impacts(root);Console.WriteLine("PASS visual rig: a gun's plain round that hits a prop plays the bake's stone, metal or wood impact out of the surface and leaves a bullet hole that fades; specials, melee and magic draw their own; the low tier draws holes only ("+count+" checks)");
            SlashBrush(root);Console.WriteLine("PASS visual rig: the slash crescents take the baked ink brush's streaks (outside the shader's shared block, crescents only, off without a bake) ("+count+" checks)");
            Dissolve(root);Console.WriteLine("PASS visual rig: a dead pilot's visual body dissolves into the bake's noise as its corpse or death scene fades, the front in the pilot's accent (at half opacity without the noise) ("+count+" checks)");
            PackSlashes(root);Console.WriteLine("PASS visual rig: the slash pack's line slashes, claw marks and rings play on the sword's, daggers' and greatsword's finishers and on the sword launch, Wolf Spin, Rising Dragon, Whirlwind, the steps and charge and the dagger throw, once each at its hit or release, about the pilot, off on the low tier and in my own first person ("+count+" checks)");
            Motion(root);Console.WriteLine("PASS visual rig: every melee weapon's (the shield mirrored), gun's, bow's, caster's (the staff mirrored) and fighter's model plays the animation pack's own clips, every melee weapon's kit moves too (a gun shouldered over its legs, on the look unless sprinting, its reload fitted; a caster's cast over its legs, its start fitted to the release), retimed so each attack's Hit lands mid hit window, the roll fills the roll, the air states and landing follow the pilot, specials and kit moves keep the game's pose; the weapon rides the drawn hand and no foot goes under the floor ("+count+" checks)");
            Stride(root);Console.WriteLine("PASS visual rig: the stride mixes the pack's runs, walks and sprint by the body's travel (direction, gait, sprint forward only), weights summing to one, at the rate that makes the mix travel at the body's speed, without allocating; legs and hips taken after the game pose, off in the air, rolling, attacking, stunned, juggled, in a special or a reaction ("+count+" checks)");
            Echoes(root);Console.WriteLine("PASS visual rig: a pilot drawn as the model leaves the model's roll afterimages, dash ghosts and Demon Step ghosts (its drawn renderers baked), tinted and fading as the voxel ghosts, a little toward the eye; its Shadow Step decoy, entrance ghosts and Cyber Lord copies are live ghosts of the model ("+count+" checks)");
            SheldonWear(root);Console.WriteLine("PASS visual rig: Sheldon's clothes on the model (DEV): the robe, trousers, boots and bare feet as wardrobe pieces in the Sheldon palette with glowing piping, the skirt and the hood as cloth; the Sheldon Head a cloth hood fitted round the head with the Sheldon face in its opening, in play, the draft, the portraits, the rooftop and the intro ("+count+" checks)");
            Bust(root);Console.WriteLine("PASS visual rig: the sky death broadcast's portrait is the victim's model cut to its bust, a hologram in the broadcast's colour scaled about its neck to fill the frame, hidden when not drawn; Classic keeps the voxel portrait and its edges ("+count+" checks)");
        }

        // Echoes of the visual body (owner 2026-10-24: "The dodge effect still shows the voxel model"): the roll afterimages, dash
        // ghosts and Demon Step ghosts of a pilot drawn as the model snapshot the model (NativeVisualBodyPresentation.cs).
        private static void Echoes(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string body=R("Runtime/NativeVisualBodyPresentation.cs"),roll=R("Runtime/NativeWeaponFxPresentation.cs"),dash=R("Runtime/NativeMeleeKitPresentation.cs"),demon=R("Runtime/NativeDevFighterEffects.cs");
            Check(body.Contains("skin.BakeMesh(echo.Baked[k],true);")&&body.Contains("if(r==null||!r.enabled||!r.gameObject.activeInHierarchy)continue;"),"an echo bakes the model's drawn renderers");
            Check(roll.Contains("afterimageModel[slot]=VisualEchoSnap(rig,ref afterimageEchoes[slot]);")&&roll.Contains("if(afterimageModel[k]){VisualEchoDraw(afterimageEchoes[k],\"NativeWeaponGhost\",tint,opacity);continue;}"),"the roll afterimages");
            Check(dash.Contains("g.Model=VisualEchoSnap(rig,ref g.Echo);")&&dash.Contains("if(g.Model){VisualEchoDraw(g.Echo,\"NativeWeaponGhost\",g.Tint,opacity);continue;}"),"the dash ghosts");
            Check(demon.Contains("demonModel[slot]=VisualEchoSnap(rig,ref demonEchoes[slot]);")&&demon.Contains("if(demonModel[s]){VisualEchoDraw(demonEchoes[s],\"NativeDevGhost\",tint,opacity);continue;}"),"the Demon Step ghosts");
            // Live ghosts (posed every frame): the Shadow Step decoy, the entrance's ghosts, the Cyber Lord's glitch copies.
            string intros=R("Runtime/NativeIntros.cs"),cyber=R("Runtime/NativeCyberLordPresentation.cs");
            Check(dash.Contains("VisualLiveGhostDraw(1L<<40|(uint)a.Id,d.Rig,a.Id,d.Rig.Pose,Vector3.zero,\"NativeWeaponGhost\",tint,opacity)")&&intros.Contains("asModel=VisualLiveGhostDraw(2L<<40|(uint)i,holder,introActor,entranceGhostPose,")&&intros.Contains("if(!asModel)sourceModels.Queue(model,rootM,entranceGhostBone,"),"the Shadow Step decoy and the entrance's ghosts as live ghosts of the model");
            Check(cyber.Contains("VisualLiveGhostDraw(3L<<40|")&&cyber.Contains("VisualLiveGhostDraw(4L<<40|")&&cyber.Contains("VisualLiveGhostDraw(5L<<40|")&&R("Runtime/NativeCyberKit2MeleePresentation.cs").Contains("VisualLiveGhostDraw(6L<<40|"),"the Cyber Lord's split, clone, idle glitch and fork bomb copies");
            Check(body.Contains("if(Viewer||sourceModels==null||ClassicBodies||!VisualBodiesOn||holder==null||pose==null)return false;")&&body.Contains("if(pair.Value.Frame<Time.frameCount-1&&b.Root.activeSelf)b.Root.SetActive(false);")&&body.Contains("VisualLiveGhostsShown();"),"a live ghost: none under Classic, hidden once a frame passes without it");
            foreach(var shader in new[]{"NativeWeaponGhost","NativeDevGhost"}){var text=R("Resources/"+shader+".shader");Check(text.Contains("_EchoOffset(\"Depth offset\",Float)=0")&&text.Contains("Offset [_EchoOffset],[_EchoOffset]"),shader+": the echoes' depth offset, 0 for the voxel ghosts");}
        }

        // The sky death broadcast on the model (owner 2026-10-24: "Model everywhere"; NativeDeathBroadcastPresentation.cs).
        // Sheldon's clothes on the model (owner 2026-10-27: "remake Sheldon clothes into functional clothing items (Hood/face,
        // robe, pants, shoes. Shoeless option too - these are all dev items)"; "utilize magica cloth for any kind of fabric
        // movement"; "a very clean design that fits the model perfectly"; "the same face on the sheldon face").
        private static void SheldonWear(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            // The pieces: DEV only, each slot's last; the gate drops them without live DEV access.
            int upper=NativeLook.SlotIndex(NativeLook.Upper),lower=NativeLook.SlotIndex(NativeLook.Lower),feet=NativeLook.SlotIndex(NativeLook.Feet);
            Check(NativeLook.Ids[upper][NativeLook.Ids[upper].Length-1]==NativeSheldonWear.Robe&&NativeLook.Ids[lower][NativeLook.Ids[lower].Length-1]==NativeSheldonWear.Pants
                &&NativeLook.Ids[feet][NativeLook.Ids[feet].Length-2]==NativeSheldonWear.Boots&&NativeLook.Ids[feet][NativeLook.Ids[feet].Length-1]==NativeSheldonWear.Barefoot,"the robe, trousers, boots and bare feet are the last pieces of their slots");
            bool devLast=true;for(int s=0;s<NativeLook.Slots.Length;s++){bool seen=false;foreach(var id in NativeLook.Ids[s]){if(NativeLook.DevOnly(id))seen=true;else if(seen)devLast=false;}}
            Check(devLast&&!NativeLook.DevOnly(NativeLook.Ids[feet][0])&&NativeLook.DevOnly(NativeSheldonWear.Barefoot)&&NativeSheldonWear.IsSheldon(NativeSheldonWear.Boots)&&!NativeSheldonWear.IsSheldon(NativeSheldonWear.Barefoot),"every slot's DEV pieces come after its public ones (the Locker lists a prefix)");
            string worn="upper:sheldon-robe,lower:sheldon-pants,hands:gloves,feet:barefoot";
            Check(NativeLook.Gate(worn,true)==worn&&NativeLook.Gate(worn,false)=="hands:gloves"&&NativeLook.Gate("feet:sneakers",false)=="feet:sneakers"&&NativeLook.Gate("",false)=="","the DEV gate keeps the pieces with live DEV access and drops only them otherwise");
            string outfit=R("Runtime/NativeLockerOutfitUI.cs"),game=R("Runtime/ConversionGame.cs"),portraitUi=R("Runtime/NativePortraitUI.cs");
            Check(outfit.Contains("private int LkOutfitCount(int slot){var ids=NativeLook.Ids[slot];int n=ids.Length;if(!lockerBodySheldon)while(n>1&&NativeLook.DevOnly(ids[n-1]))n--;return n;}")
                &&outfit.Contains("prefs.look=NativeLook.Gate(NativeLook.With(prefs.look,slot,ids[k]),lockerBodySheldon);")&&outfit.Contains("bool dev=NativeLook.DevOnly(ids[worn]);")&&outfit.Contains("\"DEV\",9,U.Hex(0xffd479,e)")
                &&outfit.Contains("lookShown=NativeLook.Gate(look,dev==1);")&&outfit.Contains("NativeBodySkins.LiveDeveloper(account?.Access??NativeAccess.Guest,")&&game.Contains("presentation.LocalLook=LookShown();")&&portraitUi.Contains("own?LookShown():PortraitOutfit(key)"),
                "the Locker lists the DEV pieces (marked) only with live DEV access, and the pilot wears its look through the gate");
            // The palette and the piped colour maps; the glow is the Sheldon glow; bare feet set down by the bake's lift.
            string body=R("Runtime/NativeVisualBodyPresentation.cs"),motion=R("Runtime/NativeVisualMotionPresentation.cs");
            Check(body.Contains("foreach(var p in d.pieces)if(p?.renderers!=null&&NativeSheldonWear.IsSheldon(p.id)&&NativeLook.Of(look,p.slot)==p.id)foreach(var n in p.renderers)if(lookNamed.TryGetValue(n,out var r)){VisualSheldonPalette(r);sheldon?.Add(r);}")
                &&body.Contains("map=Resources.Load<Texture2D>(NativeSheldonWear.MapFolder+r.name)")&&body.Contains("if(tex!=null&&!body.Block.HasTexture(TextureId))body.Block.SetTexture(TextureId,tex);")
                &&body.Contains("if(body.Sheldon.Length>0)VisualSheldonGlow(body,NativeBodySkins.SheldonGlow(a.Hat,a.HatColor,a.AccentColor));")&&body.Contains("if(r==null||Array.IndexOf(body.Sheldon,r)>=0)continue;")
                &&body.Contains("block.SetColor(id,sheldon.Contains(r)?glow:accent);"),"Sheldon's pieces in the Sheldon palette on their piped maps, their piping in the Sheldon glow (the corpse's too), the rest in the accent");
            Check(body.Contains("float lift=VisualLiftOf(look);if(lift!=0)skeleton.localPosition+=new Vector3(0,lift,0);")&&body.IndexOf("skeleton.localPosition+=new Vector3(0,lift,0);",StringComparison.Ordinal)<body.IndexOf("Binding=new NativeVisualRigBinding(root.transform,skeleton)",StringComparison.Ordinal)
                &&body.Contains("return Mathf.Clamp(worn-rest,-.05f,.05f);")&&motion.Contains("r.position.y-visualBodyPrefab.transform.position.y+body.Lift"),"a feet piece's lift sets the skeleton (bare feet on the floor) before the binding reads its rest, and the feet's clearances with it");
            // The hood: its opening, its rim ahead of the face's void, the void inside its walls, the head and hair inside it.
            Check(Math.Abs(NativeSheldonHood.OpeningHalfWidth(NativeSheldonHood.OpenWidest)-NativeSheldonHood.OpenHalf)<1e-5f&&NativeSheldonHood.OpeningHalfWidth(NativeSheldonHood.OpenTop)==0&&NativeSheldonHood.OpeningHalfWidth(NativeSheldonHood.OpenBottom)==0
                &&Math.Abs(NativeSheldonHood.OpeningY(1)-NativeSheldonHood.OpenTop)<1e-5f&&Math.Abs(NativeSheldonHood.OpeningY(-1)-NativeSheldonHood.OpenBottom)<1e-5f,"the opening: an arch from the chin to over the brow");
            float rimMiss=0;for(int i=1;i<40;i++){float y=NativeSheldonHood.OpeningY(-1+2f*i/40);var q=NativeSheldonHood.Section(y,NativeSheldonHood.RimAngle(y));rimMiss=Math.Max(rimMiss,Math.Max(Math.Abs(q.X-NativeSheldonHood.OpeningHalfWidth(y)),Math.Abs(q.Z-NativeSheldonHood.RimZ(y))));}
            Check(rimMiss<.001f&&NativeSheldonHood.RimSide>NativeSheldonHood.VoidZ+.015f,"the hood's sections meet the rim (within "+rimMiss.ToString("0.0000")+" m), the rim standing ahead of the void");
            // Every point of the void (on the model) lies inside the hood's walls.
            var sc0=NativeModelHats.SheldonScale;var off0=NativeModelHats.SheldonOffset;float wall=1;
            foreach(var q in NativeSheldonHood.Void().Positions){float x=Math.Abs(q.X*sc0.X),y=q.Y*sc0.Y+off0.Y,lo=0,hi=(float)Math.PI/2;for(int k=0;k<40;k++){float m=(lo+hi)/2;if(NativeSheldonHood.Section(y,m).X<x)lo=m;else hi=m;}wall=Math.Min(wall,NativeSheldonHood.Section(y,lo).Z-NativeSheldonHood.VoidZ);}
            Check(wall>.002f,"the void lies inside the hood's walls (the wall at least "+wall.ToString("0.000")+" m ahead of it)");
            // Measured on the bake (every hair style laid flat by its hood shape): the hair 12.8 cm to each side, to .226 over the
            // head bone, 13.4 cm behind; the nose's tip at z .151; the face's fit puts the eyes and grin inside the opening.
            Check(NativeSheldonHood.Section(.10f,(float)Math.PI/2).X>.128f+.015f&&NativeSheldonHood.Section(.10f,(float)Math.PI).Z<-.134f-.015f&&NativeSheldonHood.Top>.226f+.02f&&NativeSheldonHood.VoidZ>.151f+.005f,"the hood clears the hair laid flat (1.5 cm and more) and the void the nose");
            var sc=NativeModelHats.SheldonScale;var off=NativeModelHats.SheldonOffset;float eyeY=NativeSheldonHead.EyeY*sc.Y+off.Y,eyeOut=(NativeSheldonHead.EyeX+NativeSheldonHead.EyeR)*sc.X,grinY=NativeSheldonHead.LipTop(NativeSheldonHead.GrinW)*sc.Y+off.Y,grinOut=NativeSheldonHead.GrinW*sc.X;
            // The grin's corners at rest and laughing its widest (NativeSheldonHead.Evaluate: GrinWide up to 1.17).
            Check(eyeOut<NativeSheldonHood.OpeningHalfWidth(eyeY)-.01f&&grinOut<NativeSheldonHood.OpeningHalfWidth(grinY)-.01f&&grinOut*1.17f<NativeSheldonHood.OpeningHalfWidth(grinY)-.002f&&NativeSheldonHead.FaceZ*sc.Z+off.Z>NativeSheldonHood.VoidZ+.005f,"the eyes and the laughing grin inside the opening, in front of the void");
            // The mesh: deterministic, in budget, weights whole, the head part on the head; the maps: piping and cloth.
            var a=NativeSheldonHood.Build();var b=NativeSheldonHood.Build();
            Check(System.Linq.Enumerable.SequenceEqual(a.Positions,b.Positions)&&System.Linq.Enumerable.SequenceEqual(a.Outer,b.Outer)&&System.Linq.Enumerable.SequenceEqual(a.Weights,b.Weights),"the hood builds the same every time");
            int tris=(a.Outer.Length+a.Inner.Length)/3;Check(tris<9000&&a.Positions.Length<6000&&a.Inner.Length>0,"the hood is within budget ("+a.Positions.Length+" vertices, "+tris+" triangles with its lining)");
            bool whole=true,headPart=true;for(int i=0;i<a.Positions.Length;i++){float w=a.Weights[i*4]+a.Weights[i*4+1]+a.Weights[i*4+2]+a.Weights[i*4+3];whole&=Math.Abs(w-1)<1e-4f;if(a.Positions[i].Y>=NativeSheldonHood.OpenBottom&&a.Positions[i].Z>0||a.Positions[i].Y>NativeSheldonHood.OpenBottom+NativeSheldonHood.NapeRise)headPart&=a.Bones[i*4]==NativeSheldonHood.HeadBone&&a.Weights[i*4]>.999f;}
            Check(whole&&headPart&&NativeSheldonHood.Bones[NativeSheldonHood.HeadBone]=="head"&&NativeSheldonHood.Bones[NativeSheldonHood.ClavicleLeft]=="clavicle_l","every vertex's weights are whole; the hood's front to the chin (the face's frame) and its crown ride the head alone");
            int size=NativeSheldonHood.MapSize;var colour=new byte[size*size*4];var paint=new byte[size*size*4];NativeSheldonHood.Maps(colour,paint);
            byte At(byte[] m,float v,int c)=>m[((int)(v*size)*size)*4+c];
            float fixedRow=NativeSheldonHood.SurfaceV*(NativeSheldonHood.DomeRows+NativeSheldonHood.OpenRows)/(NativeSheldonHood.RowCount-1),cowlRow=NativeSheldonHood.SurfaceV*(NativeSheldonHood.DomeRows+NativeSheldonHood.OpenRows+1)/(NativeSheldonHood.RowCount-1);
            Check(At(paint,0,0)==255&&At(paint,fixedRow,0)==255&&At(paint,cowlRow,1)==255&&At(paint,NativeSheldonHood.SurfaceV,1)==255&&At(paint,.96f,0)==255,"the cloth holds the hood to the chin and its rim fixed and lets the cowl move");
            Check(At(colour,.3f,0)==255&&At(colour,NativeSheldonHood.HemBandV,2)==255&&At(colour,NativeSheldonHood.SurfaceV,2)==255&&At(colour,.96f,2)==255&&At(colour,NativeSheldonHood.HemBandV-.02f,0)==255,"the hood's cloth, its hem band and its rim's tube in the piping");
            // On the bodies: made with the body for a pilot in the Sheldon Head (rebuilt when that changes), the hair hidden under
            // it, its paint map handed to the cloth before the cloth attaches; the face drawn over it; menus and the intro too.
            string hoodp=R("Runtime/NativeSheldonHoodPresentation.cs"),src=R("Runtime/NativeSourcePresentation.cs"),hats=R("Runtime/NativeHatPresentation.cs"),portraits=R("Runtime/NativeVisualPortraitPresentation.cs"),roof=R("Runtime/NativeRooftopPresentation.cs"),cloth=R("Runtime/NativeVisualCloth.cs");
            Check(body.Contains("var hoodRenderer=hood?VisualHoodMake(root,skeleton):null;if(hoodRenderer!=null)sheldon.Add(hoodRenderer);")&&body.IndexOf("VisualHoodMake(root,skeleton)",StringComparison.Ordinal)<body.IndexOf("body.ClothSim=NativeVisualCloth.Attach(root,skeleton);",StringComparison.Ordinal)
                &&hoodp.Contains("NativeVisualCloth.PaintMaps[NativeSheldonHood.Renderer]=hoodPaint;")&&cloth.Contains("public static readonly System.Collections.Generic.Dictionary<string,Texture2D> PaintMaps=")
                &&body.Contains("r.name.StartsWith(\"HairFront.\",StringComparison.Ordinal)||r.name.StartsWith(\"HairBack.\",StringComparison.Ordinal))r.gameObject.SetActive(false);")
                &&hoodp.Contains("private static bool VisualHooded(Actor a)=>a!=null&&a.Hat==NativeSheldonHead.Hat&&a.SourceSpec!=\"siren\";"),"the hood is made with the body for the Sheldon Head, the hair hidden under it, its paint map ready before the cloth attaches");
            Check(body.Contains("var body=VisualBodyOf(rig,a.Id,VisualHooded(a));if(body==null)return;")&&body.Contains("var body=VisualBodyOf(rig,seat.id,VisualHooded(a));")&&body.Contains("if(make){body=VisualBodyOf(rig,a.Id,VisualHooded(a));")
                &&roof.Contains("var body=VisualBodyOf(rig,id,VisualHooded(a));")&&portraits.Contains("s.Model=CreateVisualBody(s.Player,id,hat==NativeSheldonHead.Hat);")&&portraits.Contains("s.Model.Hood!=(hat==NativeSheldonHead.Hat)"),"the live, intro, draft, revive, rooftop and portrait bodies wear the hood with the Sheldon Head");
            Check(hoodp.Contains("sourceModels.Queue(NativeSourceModels.SheldonHoodVoid,rig.Root.transform.localToWorldMatrix,bone,null,layer,camera,body:glow,partTint:partTint,hitFlash:flash);")&&hoodp.Contains("sourceModels.QueueSheldonPieces(bone(CapturedRig.Head),face,glow,layer,camera,1,flash,")
                &&hats.Contains("models[SheldonHoodVoid]=new[]{SheldonPart(NativeSheldonHood.Void(),lit,clear,false)};")&&src.Contains("&&!SheldonHoodFace(rig,a,VisualHatBone(rig,a.Hat),")
                &&portraits.Contains("else if(hat==NativeSheldonHead.Hat)SheldonHoodStill(body,hat,")&&roof.Contains("else SheldonHoodStill(body,look.Hat,hatColor,"),"the Sheldon face (its void, eyes, grin and ring) over the hood in play, the intro, the portraits and the rooftop");
            var v=NativeSheldonHood.Void();bool flat=true;foreach(var q in v.Positions)flat&=Math.Abs(q.Z*sc.Z+off.Z-NativeSheldonHood.VoidZ)<1e-4f;
            Check(v.Indices.Count>0&&flat&&v.Bone==CapturedRig.Head,"the void is one flat plate at its depth on the head");
        }

        private static void Bust(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string body=R("Runtime/NativeVisualBodyPresentation.cs"),cast=R("Runtime/NativeDeathBroadcastPresentation.cs");
            Check(cast.Contains("stage.Bust=VisualBust(rig,item.Victim,ref stage.Owned);")&&cast.Contains("bool model=stage.Bust?.Root!=null&&!ClassicBodies;"),"a broadcast makes the victim's bust, used unless Classic");
            Check(cast.Contains("if(!model){Edges(NativeBroadcastEdges.Body);")&&cast.Contains("if(stage.Bust?.Root!=null&&stage.Frame!=Time.frameCount&&stage.Bust.Root.activeSelf)stage.Bust.Root.SetActive(false);"),"the voxel edges only on the voxel portrait; a bust not drawn hides");
            Check(body.Contains("piece[v]=BustBone(joint[v])?0:-1;")&&body.Contains("r.sharedMaterials=mats;")&&body.Contains("if(Viewer||sourceModels==null||ClassicBodies||!VisualBodiesOn)return null;"),"the bust: cut to the portrait's bones, in the hologram material, none under Classic");
            Check(Array.TrueForAll(new[]{CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.RClav},NativeBroadcastFx.PortraitBone)&&!NativeBroadcastFx.PortraitBone(CapturedRig.Pelvis),"the portrait's bones");
        }

        // Surface impacts from the owner's effect pack (owner 2026-10-24: "Implement whatever we have on asset packs that would work
        // good on this project, make them fit"): Core NativeSurfaceImpacts and its wiring (Runtime NativeSurfaceImpactPresentation.cs).
        private static void Impacts(string root)
        {
            Check(NativeSurfaceImpacts.Kind("metal")==NativeSurfaceImpacts.Metal&&NativeSurfaceImpacts.Kind("wood")==NativeSurfaceImpacts.Wood&&NativeSurfaceImpacts.Kind("brick")==NativeSurfaceImpacts.Stone&&NativeSurfaceImpacts.Kind(null)==NativeSurfaceImpacts.Stone,"metal and wood by name, every other surface stone (as the impact sound)");
            Check(NativeSurfaceImpacts.Shows("pistol",null)&&NativeSurfaceImpacts.Shows("rifle","")&&NativeSurfaceImpacts.Shows("machinegun",null),"the guns' plain rounds show");
            Check(!NativeSurfaceImpacts.Shows("rifle","bit-bolt")&&!NativeSurfaceImpacts.Shows("sword",null)&&!NativeSurfaceImpacts.Shows("fire",null)&&!NativeSurfaceImpacts.Shows(null,null),"special rounds, melee, magic and unknown sources draw their own");
            Check(NativeSurfaceImpacts.DecalAlpha(0)==1&&NativeSurfaceImpacts.DecalAlpha(NativeSurfaceImpacts.DecalLife-NativeSurfaceImpacts.DecalFade)==1&&NativeSurfaceImpacts.DecalAlpha(NativeSurfaceImpacts.DecalLife)==0&&NativeSurfaceImpacts.DecalAlpha(-1)==0,"a hole is solid, then fades out by its life");
            float last=1;for(float t=0;t<NativeSurfaceImpacts.DecalLife+1;t+=.05f){float a=NativeSurfaceImpacts.DecalAlpha(t);Check(a<=last+1e-6f,"a hole only fades");last=a;}
            Check(NativeSurfaceImpacts.Names.Length==NativeSurfaceImpacts.Kinds,"a bake entry per kind");
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string impacts=R("Runtime/NativeSurfaceImpactPresentation.cs"),fx=R("Runtime/NativeWeaponFxPresentation.cs");
            Check(fx.Contains("            SurfaceImpacts(arena,now);\n            var draw=WeaponFxDraw;if(draw==null)return;"),"the weapon effects pass plays the impacts each frame");
            Check(impacts.Contains("if(e.Id<=surfaceSeen)continue;surfaceSeen=e.Id;")&&impacts.Contains("if(e.Kind!=EventKind.Impact||string.IsNullOrEmpty(e.SurfaceId)||now-e.Time>NativeSurfaceImpacts.Stale)continue;"),"each Impact event with a surface plays once, never late");
            Check(impacts.Contains("if(!LowGraphics)SurfaceEffect(")&&impacts.Contains("SurfaceHole(kind,"),"the low graphics tier draws the holes only");
            Check(impacts.Contains("foreach(var e in arena.Events)if(e.Id>surfaceSeen)surfaceSeen=e.Id;"),"a new arena's earlier events never play");
            foreach(string pack in new[]{"Kripto","FPS_","Bozo","BoZo"})Check(!impacts.Contains(pack),"no pack types or names in the impacts' code: "+pack);
        }

        // The slash crescents' ink brush from the owner's effect pack (owner 2026-10-24: "keep going with the next packs, starting
        // with the sword slash arcs"): the crescent shader reads it outside the shared block, only for crescents, only when on.
        private static void SlashBrush(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string fx=R("Resources/NativeWeaponFx.shader"),draw=R("Runtime/NativeWeaponFxDraw.cs"),pres=R("Runtime/NativeWeaponFxPresentation.cs");
            int shared=fx.IndexOf("WEAPON FX SHARED END",StringComparison.Ordinal),brushed=fx.IndexOf("float2 SlashBrushed(",StringComparison.Ordinal);
            Check(shared>0&&brushed>shared&&fx.Contains("_SlashBrushOn(\"Slash brush on\",Float)=0"),"the brush is read outside the shared block, off by default");
            Check(fx.Contains("if(mode>0.5&&mode<1.5&&_SlashBrushOn>0.5)shape=SlashBrushed(shape,uv,w);\n                float4 c=FxShade(i.color,shape);"),"only crescents take the brush, after their shape");
            Check(draw.Contains("material.SetFloat(BrushOnId,brush!=null?1:0);"),"the drawer turns the brush on only with a texture");
            Check(pres.Contains("WeaponFxDraw=>weaponFx??=sourceModels!=null?WithSlashBrush(new NativeWeaponFxDraw()):null;")&&pres.Contains("prefab.transform.Find(\"VisualSlashes/Brush\")"),"the match's drawer takes the bake's brush when there is one");
            foreach(string pack in new[]{"Ordos","Scratches","Kripto"})Check(!pres.Contains(pack)&&!draw.Contains(pack)&&!fx.Contains(pack),"no pack names in the brush's code: "+pack);
        }

        // The dead pilot's dissolve (the owner's dissolve pack's noise through the bake): the visual body shader cuts by it as the
        // corpse fades, its front glowing the pilot's accent; without the noise a dead body goes at half opacity as before.
        private static void Dissolve(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string fx=R("Resources/NativeVisualBodyFx.cginc"),toon=R("Resources/NativeVisualBodyToon.shader"),body=R("Runtime/NativeVisualBodyPresentation.cs");
            Check(fx.Contains("if(_Dissolve<=0.0)return 0.0;")&&fx.Contains("clip(n-cut);"),"the dissolve reads nothing and cuts nothing at 0");
            Check(toon.Contains("float front=DissolveFront(i.uv_Texture2D);o.Emission+=_DissolveEdge.rgb*front;")&&toon.Contains("clip(map.a-_Cutoff);\n   DissolveFront(i.uv);"),"the body and its outline dissolve together, the front glowing");
            Check(body.Contains("if(noiseTex!=null){Shader.SetGlobalTexture(DissolveNoiseId,noiseTex);visualDissolve=true;}"),"the noise comes from the bake, once");
            Check(body.Contains("if(alpha<(visualDissolve?.01f:.5f)){if(c.Root.activeSelf)c.Root.SetActive(false);return true;}")&&body.Contains("if(visualDissolve)VisualDissolve(c.All,ref c.Dissolve,1-alpha,c.Edge);"),"a corpse dissolves as it fades (at half without the noise)");
            Check(body.Contains("if(alpha<(visualDissolve?.01f:.5f)||!visible(CapturedRig.Pelvis))")&&body.Contains("if(visualDissolve)VisualDissolve(s.All,ref s.Dissolve,1-alpha,s.Edge);"),"a death scene's body dissolves as it fades");
            Check(body.Contains("amount=Mathf.Round(Mathf.Clamp01(amount)*64)/64;if(amount==last)return;"),"the dissolve is written only when it moves");
        }

        // The slash pack on the special moves and finishers (owner 2026-10-24: "bring the slash pack's ring slashes, claw marks and
        // line slashes onto the special moves and finishers"): Core NativePackSlashes and its wiring (Runtime
        // NativePackSlashPresentation.cs, Resources/NativePackSlash.shader).
        private static void PackSlashes(string root)
        {
            bool Fin(string spec,int index,out NativePackSlashes.Look look)=>NativePackSlashes.Finisher(spec,index,NativeMeleeRules.Reverse(new NativeMeleeSwing{Spec=spec,Index=index}),out look);
            for(int i=1;i<=12;i++)
            {
                Check(Fin("sword",i,out var s)==(i%3==0)&&(i%3!=0||s.Kind==NativePackSlashes.Lines),"the sword's third cut (the thrust) is its finisher: line slashes");
                Check(Fin("daggers",i,out var d)==(i%4==0)&&(i%4!=0||d.Kind==NativePackSlashes.Claws),"the daggers' fourth cut is theirs: claw marks");
                Check(Fin("greatsword",i,out var g)==(i%2==0)&&(i%2!=0||g.Kind==(NativeMeleeRules.Reverse(new NativeMeleeSwing{Spec="greatsword",Index=i})?NativePackSlashes.RingReverse:NativePackSlashes.Ring)),"the greatsword's second cut is its: the ring turning its way");
                Check(!Fin("shield",i,out _)&&!Fin("rifle",i,out _)&&!Fin(null,i,out _),"no other weapon has one");
            }
            Check(!Fin("sword",0,out _)&&!Fin("daggers",0,out _)&&!Fin("greatsword",0,out _),"no strike index, no finisher");
            foreach(var k in new[]{"whirlwind","wolf-spin"})Check(NativePackSlashes.Ability(k,out var l)&&l.Kind==NativePackSlashes.Ring&&l.Lay==NativePackSlashes.Lay.Flat,"a spin lays the ring flat round the pilot: "+k);
            foreach(var k in new[]{"rising-dragon","sword-launch"})Check(NativePackSlashes.Ability(k,out var l)&&l.Kind==NativePackSlashes.Ring&&l.Lay==NativePackSlashes.Lay.Upright,"a launch stands the ring up: "+k);
            foreach(var k in new[]{"flash-step","shadow-step","bulwark-charge"})Check(NativePackSlashes.Ability(k,out var l)&&l.Kind==NativePackSlashes.Lines&&l.Lay==NativePackSlashes.Lay.Ahead,"a step or charge throws line slashes ahead: "+k);
            Check(NativePackSlashes.Ability("dagger-throw",out var thrown)&&thrown.Kind==NativePackSlashes.Claws,"the dagger throw rakes claw marks ahead");
            foreach(var k in new[]{"skyfall","kunai-mark","kunai-recast","aegis-ricochet","shield-throw","earthsplitter","",null})Check(!NativePackSlashes.Ability(k,out _),"other moves draw their own: "+k);
            foreach(var k in new[]{"whirlwind","rising-dragon","flash-step","dagger-throw"}){NativePackSlashes.Ability(k,out var l);Check(l.Scale>.2f&&l.Scale<1&&Math.Abs(l.Height)<1&&l.Kind>=0&&l.Kind<NativePackSlashes.Kinds,"a look sized and placed about the pilot: "+k);}
            Check(!NativePackSlashes.On&&R("Runtime/NativePackSlashPresentation.cs").Contains("if(!NativePackSlashes.On||s?.Actors==null||LowGraphics||!PackSlashBank())return;"),"the slash pack's effects are off on every move (owner 2026-10-26: \"Nothing fits it currently\"); the tables stay");
            Check(NativePackSlashes.Names.Length==NativePackSlashes.Kinds&&NativePackSlashes.PerKind>0&&NativePackSlashes.EffectLife>2,"a bake entry per kind, pooled, playing out the pack's two-second strokes");
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string pres=R("Runtime/NativePackSlashPresentation.cs"),weapon=R("Runtime/NativeWeaponPresentation.cs"),shader=R("Resources/NativePackSlash.shader"),rules=R("Core/NativePackSlashes.cs");
            Check(weapon.Contains("WeaponFxFrame(time);\n            // The slash pack's rings, lines and claws on finishers and special moves (NativePackSlashPresentation.cs).\n            PackSlashFx(time);")&&weapon.Contains("ClearWeaponFx();ClearPackSlashes();"),"played each frame after the weapon effects, cleared with them");
            Check(pres.Contains("s?.Actors==null||LowGraphics||!PackSlashBank())return;")&&pres.Contains("if(!a.Alive||FirstPerson&&a.Id==LocalId)continue;"),"off on the low tier, without a bake, and for my own first-person pilot");
            Check(pres.Contains("if(a.SwingAt>=a.AttackAt&&time>=a.SwingAt){packSlashFinisher.Remove(a.Id);Cue(a.Id,a.SwingAt,finisher,a);}")&&pres.Contains("else if(time>a.AttackAt+1)packSlashFinisher.Remove(a.Id);"),"a finisher plays at its strike's hit window, or not at all");
            Check(pres.Contains("packSlashRelease[a.Id]=release;")&&pres.Contains("if(time-release<NativePackSlashes.Stale&&NativePackSlashes.Ability(kind,out var look))"),"each release plays once, never late");
            Check(pres.Contains("if(use==null&&list.Count<NativePackSlashes.PerKind)")&&pres.Contains("foreach(var ps in use.Systems){ps.Clear(true);ps.Play(true);}"),"pooled per kind, replayed from the systems not under another");
            Check(shader.Contains("Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off")&&shader.Contains("#pragma multi_compile_fog"),"the strokes blend over the scene without writing depth");
            foreach(string pack in new[]{"Ordos","OrdosFX","Magic Slashes","Eff1","Eff4","Eff6","Slash/Blend","DistTex","DistStrength"})Check(!pres.Contains(pack)&&!shader.Contains(pack)&&!rules.Contains(pack),"no pack names in the slashes' code: "+pack);
        }

        // The model's own animation (owner 2026-10-24: "Pack animations whole, and adjust hit timing as needed", "Retime clips to
        // rules", greatsword first): Core NativeVisualMotion's choices and timing, and its wiring (Runtime
        // NativeVisualMotionPresentation.cs).
        private sealed class MotionBook : NativeVisualMotion.IClips { public bool Has(string c)=>c!=null&&c.StartsWith("2Hand-Sword-",StringComparison.Ordinal);public float Length(string c)=>1.2f;public float Hit(string c)=>.42f; }
        private sealed class GunBook : NativeVisualMotion.IClips { public bool Has(string c)=>c!=null;public float Length(string c)=>1.2f;public float Hit(string c)=>.42f; }
        private static void Motion(string root)
        {
            var set=NativeVisualMotion.For("greatsword");var book=new MotionBook();var book2=new GunBook();
            var sword=NativeVisualMotion.For("sword");
            var daggers=NativeVisualMotion.For("daggers");
            var shield=NativeVisualMotion.For("shield");
            var rifle=NativeVisualMotion.For("rifle");var pistol=NativeVisualMotion.For("pistol");
            var staff=NativeVisualMotion.For("fire");var bow=NativeVisualMotion.For("bow");var fists=NativeVisualMotion.For("emperor");
            Check(set!=null&&sword!=null&&daggers!=null&&shield!=null&&rifle!=null&&pistol!=null&&NativeVisualMotion.For("machinegun")==rifle&&sword!=set&&staff!=null&&NativeVisualMotion.For("water")==staff&&NativeVisualMotion.For("lightning")==staff&&NativeVisualMotion.For("darkness")==staff&&NativeVisualMotion.For("leet")==staff&&bow!=null&&fists!=null&&NativeVisualMotion.For("ansatsuken")==fists&&NativeVisualMotion.For(null)==null,"the melee weapons, the guns and the casters play the pack's sets (the machine gun the rifle's); the others keep the game's pose until theirs move over");
            // A gun's upper layer: low by default, raised aiming or just after a shot, fitted to the reload; pitched by the aim.
            var gun=new Actor{Grounded=true,Weapon=WeaponId.Rifle};NativeVisualMotion.Upper up;
            Check(NativeVisualMotion.Layer(rifle,gun,50,book2,out up)&&!up.Raised,"a rifle carried low by default");
            gun.Aiming=true;Check(NativeVisualMotion.Layer(rifle,gun,50,book2,out up)&&up.Raised&&up.A==rifle.Aim[1]&&up.Mix==0,"aimed level: the level aim pose");
            gun.Pitch=rifle.AimPitch*.5f;NativeVisualMotion.Layer(rifle,gun,50,book2,out up);Check(up.B==rifle.Aim[2]&&Math.Abs(up.Mix-.5f)<1e-5f,"aimed up: toward the up pose by the pitch");
            gun.Pitch=-rifle.AimPitch*2;NativeVisualMotion.Layer(rifle,gun,50,book2,out up);Check(up.B==rifle.Aim[0]&&Math.Abs(up.Mix-1)<1e-5f,"aimed down: the down pose, held at its row");
            gun.Aiming=false;gun.Pitch=0;gun.LastShot=50;NativeVisualMotion.Layer(rifle,gun,50.1f,book2,out up);Check(up.Raised&&up.A==rifle.Fire[1]&&Math.Abs(up.TimeA-.1f)<1e-5f,"just after a shot: raised, the recoil from the shot");
            NativeVisualMotion.Layer(rifle,gun,50+NativeVisualMotion.ShotHold(gun)+.01f,book2,out up);Check(!up.Raised,"lowered again once the shot is past");
            NativeVisualMotion.Layer(rifle,gun,55,book2,out up,true);Check(up.Raised&&up.A==rifle.Aim[1],"arms free (idle or moving): the gun follows the look");
            gun.Sprinting=true;NativeVisualMotion.Layer(rifle,gun,55,book2,out up,true);Check(!up.Raised,"sprinting: carried low");gun.Sprinting=false;
            gun.ReloadAt=60;gun.ReloadUntil=62;NativeVisualMotion.Layer(rifle,gun,61,book2,out up);Check(up.Raised&&up.A==rifle.Reload&&Math.Abs(up.TimeA-.6f)<1e-4f,"the reload fitted to the reload");
            Check(!NativeVisualMotion.Layer(set,new Actor{Grounded=true},50,book,out _),"melee has no upper layer");
            // A caster's cast on the upper layer: its start fitted to the release, its end after it at its pace, then lowered; a
            // Q/E ability the area cast's likewise. The cast is no melee strike (the legs keep their motion).
            Check(staff.Gun==false&&rifle.Gun&&staff.LeftShaft&&staff.Idle.EndsWith(NativeVisualMotion.Mirror)&&staff.Reactions[2]=="2Hand-Staff-GetHit-R1"+NativeVisualMotion.Mirror,"the staff's set plays mirrored (the game's staff is in the left fist), its reactions' sides swapped");
            var mage=new Actor{Grounded=true,SourceSpec="fire",MagicVisual=1,CastRate=1,AttackAt=70,AttackUntil=70.6f};float release=70+MagicPose.Windup(MagicPose.School(mage),1);
            NativeVisualMotion.Layer(staff,mage,70+(release-70)*.5f,book2,out up);Check(up.Raised&&Array.IndexOf(staff.CastStart,up.A)>=0&&Math.Abs(up.TimeA-.6f)<1e-4f,"a cast: its start fitted to the release");
            NativeVisualMotion.Layer(staff,mage,release+.3f,book2,out up);Check(up.Raised&&Array.IndexOf(staff.CastEnd,up.A)>=0&&Math.Abs(up.TimeA-.3f)<1e-4f,"after the release: the cast's end at its own pace");
            Check(NativeVisualMotion.Layer(staff,mage,release+1.3f,book2,out up)&&!up.Raised,"the cast done: the layer lowers");
            Check(NativeVisualMotion.Choose(staff,mage,70.2f,new NativeVisualMotion.Memory(),book2,false,out var castPick)&&castPick.Clip==staff.Idle,"a cast is no melee strike: the body idles under it");
            mage.AbilityId="flame-lance";mage.AbilityAt=80;mage.AbilityRelease=80.5f;NativeVisualMotion.Layer(staff,mage,80.25f,book2,out up);Check(up.Raised&&up.A==staff.AbilityStarts[0]&&Math.Abs(up.TimeA-.6f)<1e-4f,"an ability: its own cast's start fitted to its release");
            mage.AbilityId="unknown-cast";NativeVisualMotion.Layer(staff,mage,80.25f,book2,out up);Check(up.A==staff.AbilityStart,"an ability without its own cast: the area cast");
            Check(staff.AbilityIds.Length==8&&staff.AbilityStarts.Length==8&&staff.AbilityEnds.Length==8&&Array.TrueForAll(staff.AbilityStarts,c=>c.EndsWith("_start"+NativeVisualMotion.Mirror)),"the eight school abilities each have a mirrored cast");
            // A gun's Q/E: the pistol's grenade throw (wind-up, then the throw), the machine gun's flourish, the rocket held on the aim.
            var thrower=new Actor{Grounded=true,Weapon=WeaponId.Pistol,AbilityId="frag-grenade",AbilityAt=120,AbilityRelease=120.2f};
            Check(NativeVisualMotion.Layer(pistol,thrower,120.1f,book2,out up)&&up.Raised&&up.A==pistol.AbilityStarts[0]&&NativeVisualMotion.Layer(pistol,thrower,120.4f,book2,out up)&&up.A==pistol.AbilityEnds[0],"the grenade: the wind-up, then the throw");
            var gunner=new Actor{Grounded=true,Weapon=WeaponId.MachineGun,AbilityId="overdrive",AbilityAt=130};Check(NativeVisualMotion.Layer(rifle,gunner,130.5f,book2,out up)&&up.Raised&&up.A==rifle.AbilityStarts[0]&&Math.Abs(up.TimeA-.5f)<1e-4f,"overdrive: the flourish at its pace");
            gunner.AbilityId="rocket";gunner.AbilityRelease=130.12f;gunner.Sprinting=true;Check(NativeVisualMotion.Layer(rifle,gunner,130.3f,book2,out up,true)&&up.Raised&&up.A==rifle.Aim[1],"the rocket: held on the aim, even sprinting");
            Check(!NativeVisualMotion.Layer(rifle,new Actor{Grounded=true,Sprinting=true},50,book2,out up,true)||!up.Raised,"a sprinting gun stays low with its arms free");
            // A bow: carried at rest, drawn (its drawn poses by the pitch) from the draw to the loose, its release after; not a gun.
            var archer=new Actor{Grounded=true,SourceSpec="bow"};
            Check(!bow.Gun&&NativeVisualMotion.Layer(bow,archer,50,book2,out up,true)&&!up.Raised,"a bow carried at rest, its arms free");
            archer.BowDrawAt=50;archer.Pitch=bow.AimPitch*.5f;NativeVisualMotion.Layer(bow,archer,50.4f,book2,out up);Check(up.Raised&&up.A==bow.Pull[1]&&up.B==bow.Pull[2]&&Math.Abs(up.Mix-.5f)<1e-5f,"drawing: the drawn poses by the pitch");
            archer.BowReleaseAt=51;archer.Pitch=0;NativeVisualMotion.Layer(bow,archer,51.1f,book2,out up);Check(up.Raised&&up.A==bow.Fire[1]&&Math.Abs(up.TimeA-.1f)<1e-5f,"loosed: the release from the loose");
            NativeVisualMotion.Layer(bow,archer,51+NativeVisualMotion.BowHold+.01f,book2,out up);Check(!up.Raised,"lowered again after the loose");
            // The greatsword's kit moves: Earthsplitter's slam with its Hit on the release, the Whirlwind turned by the game's spin;
            // a kit move the set does not map keeps the game's pose.
            var slam=new Actor{Grounded=true,SourceSpec="greatsword",MeleeKit=new NativeMeleeKit{Kind="earthsplitter",Phase="windup",At=100,Release=100.45f,Until=100.75f}};
            Check(NativeVisualMotion.Choose(set,slam,100.45f,new NativeVisualMotion.Memory(),book2,false,out var kitPick)&&kitPick.Clip==set.KitClips[0]&&Math.Abs(kitPick.Time-.42f)<1e-4f,"Earthsplitter: the slam's Hit on the release");
            slam.SpecialKind="wolf-spin";slam.SpecialAt=100;slam.SpecialUntil=101;Check(NativeVisualMotion.Choose(set,slam,100.9f,new NativeVisualMotion.Memory(),book2,false,out kitPick)&&kitPick.Clip==set.Idle,"a mapped kit move's tail goes on to the idle");
            var spin=new Actor{Grounded=true,SourceSpec="greatsword",MeleeKit=new NativeMeleeKit{Kind="whirlwind",Phase="active",At=110,Release=110.3f,Until=113.3f}};
            // The Whirlwind (owner 2026-10-26: "need to fix whirlwind on greatsword, the player spin is not good"): the stance's
            // legs under the level sweep's upper body, the body turned so the drawn blade rides the game's tip, the feet marking
            // time once per turn each.
            Check(NativeVisualMotion.Choose(set,spin,111,new NativeVisualMotion.Memory(),book2,false,out kitPick)&&kitPick.Clip==set.Idle&&kitPick.Spin&&kitPick.Align==1&&kitPick.Sit==1
                &&kitPick.Turn!=0&&Math.Abs(kitPick.Tip-kitPick.Turn-MeleeKitMotion.WhirlTip)<1e-4f,"the Whirlwind: the stance's legs, the body turned so its blade rides the game's tip");
            Check(set.KitClips[1]=="2Hand-Sword-Attack2"&&NativeVisualMotion.Layer(set,spin,111,book2,out up,true)&&up.Raised&&up.A==set.KitClips[1]&&Math.Abs(up.TimeA-NativeVisualMotion.WhirlHold*1.2f)<1e-4f,
                "the Whirlwind's upper body: the level sweep, held through the spin");
            {
                float maxL=0,maxR=0;bool both=false,still=true;
                for(float t=110.3f;t<113.3f;t+=1/240f){NativeVisualMotion.Choose(set,spin,t,new NativeVisualMotion.Memory(),book2,false,out var p);maxL=Math.Max(maxL,p.LiftL);maxR=Math.Max(maxR,p.LiftR);both|=p.LiftL>.02f&&p.LiftR>.02f;}
                var wind=new Actor{Grounded=true,SourceSpec="greatsword",MeleeKit=new NativeMeleeKit{Kind="whirlwind",Phase="windup",At=110,Release=110.3f,Until=113.3f}};
                NativeVisualMotion.Choose(set,wind,110.001f,new NativeVisualMotion.Memory(),book2,false,out var start);NativeVisualMotion.Choose(set,wind,110.299f,new NativeVisualMotion.Memory(),book2,false,out var released);
                NativeVisualMotion.Layer(set,wind,110.15f,book2,out var halfway,true);still=start.LiftL==0&&start.LiftR==0&&start.Align<1e-4f&&Math.Abs(start.Turn)<1e-3f;
                Check(maxL>.95f&&maxR>.95f&&!both,"each foot steps once per turn, half a turn apart, never both up ("+maxL.ToString("0.00")+", "+maxR.ToString("0.00")+")");
                Check(still&&released.Align>.99f&&Math.Abs(halfway.TimeA-NativeVisualMotion.WhirlHold*1.2f*.5f)<1e-3f,"the wind-up: from the guard as it stands, onto the blade by the release, the sweep into its hold");
                var stop=new Actor{Grounded=true,SourceSpec="greatsword",MeleeKit=new NativeMeleeKit{Kind="whirlwind",Phase="recovery",At=110,Release=110.3f,Until=113.3f,RecoverAt=113.3f,RecoverUntil=113.8f}};
                NativeVisualMotion.Choose(set,stop,113.799f,new NativeVisualMotion.Memory(),book2,false,out var settled);NativeVisualMotion.Layer(set,stop,113.799f,book2,out var risen,true);
                Check(settled.Align<.01f&&settled.LiftL==0&&settled.LiftR==0&&risen.TimeA>1.19f,"the stop: back off the blade and still, the sweep risen to its end");
            }
            // A sprint (dash) attack keeps its leaping run's flight (owner 2026-10-26: "it looks like they need to be in the air
            // when dash attacking?"); the greatsword's rides the game's hop; a plain swing stays on the floor.
            {
                var run=new Actor{Grounded=true,SourceSpec="sword",SprintAttack=true,AttackAt=200,AttackUntil=200.8f,SwingAt=200.16f,SwingUntil=200.34f,VisualWindup=.16f};
                Check(NativeVisualMotion.Choose(sword,run,200.2f,new NativeVisualMotion.Memory(),book2,false,out var dashPick)&&dashPick.Clip==sword.SprintAttack&&dashPick.Flight,"a sword dash attack keeps its clip's flight");
                run.SourceSpec="greatsword";run.Grounded=false;Check(NativeVisualMotion.Choose(set,run,200.1f,new NativeVisualMotion.Memory(),book2,false,out dashPick)&&dashPick.Clip==set.SprintAttack&&!dashPick.Flight,"the greatsword's dash attack rides the game's hop");
                run.Grounded=true;Check(NativeVisualMotion.Choose(set,run,200.3f,new NativeVisualMotion.Memory(),book2,false,out dashPick)&&dashPick.Clip==set.Land&&Math.Abs(dashPick.Time-(200.3f-run.SwingAt))<1e-4f
                    &&NativeVisualMotion.Layer(set,run,200.3f,book2,out up,true)&&up.A==set.SprintAttack&&up.Raised&&up.Snap,"landed, the greatsword's cleave plants on the landing's legs, its follow-through laid on at once");
                run.SwingAt=run.SwingUntil=-100;Check(NativeVisualMotion.Choose(set,run,200.5f,new NativeVisualMotion.Memory(),book2,false,out dashPick)&&dashPick.Clip==set.Land&&Math.Abs(dashPick.Time-(200.5f-200.16f))<1e-4f,"still planted once the hit window has closed (the touchdown from the wind-up)");
                run.SwingAt=200.16f;run.SwingUntil=200.34f;
                Check(NativeVisualMotion.Choose(set,run,200.1f,new NativeVisualMotion.Memory(),book2,false,out dashPick)&&dashPick.Clip==set.SprintAttack&&!NativeVisualMotion.Layer(set,run,200.1f,book2,out up,true),"before the touchdown the take is the whole body");
                run.SourceSpec="sword";run.SprintAttack=false;Check(NativeVisualMotion.Choose(sword,run,200.2f,new NativeVisualMotion.Memory(),book2,false,out dashPick)&&dashPick.Clip!=sword.SprintAttack&&!dashPick.Flight,"a plain swing stays on the floor");
                Check(NativeVisualMotion.Flies("daggers")&&NativeVisualMotion.Flies("shield")&&!NativeVisualMotion.Flies("greatsword"),"which dash attacks fly");
            }
            // The sword's, daggers' and shield's: the dash's lunge on the release, a recast its own clip, the charge at its pace.
            var dash=new Actor{Grounded=true,SourceSpec="sword",MeleeKit=new NativeMeleeKit{Kind="flash-step",Phase="windup",At=120,Release=120.15f,Until=120.6f},SpecialKind="flash-step",SpecialAt=120,SpecialUntil=120.6f};
            Check(NativeVisualMotion.Choose(sword,dash,120.15f,new NativeVisualMotion.Memory(),book2,false,out kitPick)&&kitPick.Clip==sword.KitClips[0]&&Math.Abs(kitPick.Time-.2f*1.2f)<1e-4f,"Flash Step: the dash's lunge on the release");
            var sky=new Actor{Grounded=false,SourceSpec="sword",MeleeKit=new NativeMeleeKit{Kind="rising-dragon",Phase="recast",At=130,Release=130.2f,Until=130.65f},SpecialKind="skyfall",SpecialAt=130,SpecialUntil=130.65f};
            Check(NativeVisualMotion.Choose(sword,sky,130.1f,new NativeVisualMotion.Memory(),book2,false,out kitPick)&&kitPick.Clip==sword.KitRecast[1],"Skyfall, the Rising Dragon's recast: its own clip");
            var charge=new Actor{Grounded=true,SourceSpec="shield",MeleeKit=new NativeMeleeKit{Kind="bulwark-charge",Phase="active",At=140,Release=140.15f,Until=140.8f}};
            Check(NativeVisualMotion.Choose(shield,charge,140.5f,new NativeVisualMotion.Memory(),book2,false,out kitPick)&&kitPick.Clip==shield.KitClips[0]&&Math.Abs(kitPick.Time-.5f)<1e-4f,"the Bulwark Charge: the charge at its own pace");
            var toss=new Actor{Grounded=true,SourceSpec="daggers",MeleeKit=new NativeMeleeKit{Kind="kunai-mark",Phase="windup",At=145,Release=145.2f,Until=145.6f},SpecialKind="dagger-throw",SpecialAt=145,SpecialUntil=145.6f};
            Check(NativeVisualMotion.Choose(daggers,toss,145.1f,new NativeVisualMotion.Memory(),book2,false,out kitPick)&&kitPick.Clip==daggers.KitClips[1]&&Math.Abs(kitPick.Time-.6f)<1e-4f,"the Kunai Mark: the throw's wind-back fitted to the release");
            Check(NativeVisualMotion.Choose(daggers,toss,145.4f,new NativeVisualMotion.Memory(),book2,false,out kitPick)&&kitPick.Clip==daggers.KitThen[1]&&Math.Abs(kitPick.Time-.6f)<1e-4f,"then the throw, fitted to the cast's end");
            var gone=new Actor{Grounded=true,SourceSpec="sword",MeleeKit=new NativeMeleeKit{Kind="earthsplitter",Phase="active",At=150,Release=150.45f,Until=150.75f},SpecialKind="wolf-spin",SpecialAt=150,SpecialUntil=150.75f};
            Check(!NativeVisualMotion.Choose(sword,gone,150.5f,new NativeVisualMotion.Memory(),book2,false,out _),"a kit move the set does not map keeps the game's pose");
            var bankClip=new NativeVisualMotionClips.Clip{Frames=2,Stride=NativeVisualMotionClips.Head,Rate=1,Data=new float[]{0,0,0,0,0,0,1,0,0,0,0,(float)Math.Sin(Math.PI/4),0,(float)Math.Cos(Math.PI/4)}};
            Check(Math.Abs(NativeVisualMotionClips.Yaw(bankClip,0))<1e-5f&&Math.Abs(NativeVisualMotionClips.Yaw(bankClip,1)-Math.PI/2)<1e-4f,"a clip's own turn read from its body rotation");
            // A fighter's jab: the clip's Hit on the rules' share of the strike; a chain's last the heavy right; its other moves
            // (a charge, a wave) keep the game's pose.
            var boxer=new Actor{Grounded=true,SourceSpec="emperor",SourceAction="jab",SourceAt=90,SourceUntil=90.2f,SourceTotal=2,SourceIndex=0};
            Check(NativeVisualMotion.Choose(fists,boxer,90+.2f*NativeVisualMotion.StrikeHit,new NativeVisualMotion.Memory(),book2,false,out var jab)&&jab.Clip==fists.Attacks[0]&&Math.Abs(jab.Time-.42f)<1e-4f,"a jab: its Hit on the rules' hit");
            boxer.SourceIndex=1;Check(NativeVisualMotion.Choose(fists,boxer,90.05f,new NativeVisualMotion.Memory(),book2,false,out jab)&&jab.Clip==fists.Attacks[fists.Attacks.Length-1],"a chain's last: the heavy right");
            // Its own moves: the charge gathers then holds, the hadoken pushes, the tatsu alternates its kicks, the electric punch's Hit on its quarter.
            boxer.SourceAction="charge";boxer.SourceUntil=93;Check(NativeVisualMotion.Choose(fists,boxer,90.1f,new NativeVisualMotion.Memory(),book2,false,out jab)&&jab.Clip==fists.ChargeStart,"the shinku charge gathers");
            Check(NativeVisualMotion.Choose(fists,boxer,91,new NativeVisualMotion.Memory(),book2,false,out jab)&&jab.Clip==fists.ChargeLoop,"then holds");
            boxer.SourceAction="hadoken";boxer.SourceAt=94;boxer.SourceUntil=94.48f;Check(NativeVisualMotion.Choose(fists,boxer,94.24f,new NativeVisualMotion.Memory(),book2,false,out jab)&&jab.Clip==fists.Hadoken&&Math.Abs(jab.Time-.6f)<1e-4f,"the hadoken's push across the move");
            boxer.SourceAction="tatsu";boxer.SourceAt=100;boxer.SourceTotal=4;boxer.SourceUntil=100+.12f+3*.18f+.22f;
            Check(NativeVisualMotion.Choose(fists,boxer,100.05f,new NativeVisualMotion.Memory(),book2,false,out jab)&&jab.Clip==fists.Kicks[0]&&NativeVisualMotion.Choose(fists,boxer,100.12f+.27f,new NativeVisualMotion.Memory(),book2,false,out var kick2)&&kick2.Clip==fists.Kicks[0]&&NativeVisualMotion.Choose(fists,boxer,100.12f+.09f,new NativeVisualMotion.Memory(),book2,false,out var kick1)&&kick1.Clip==fists.Kicks[1],"the tatsu's kicks alternate");
            boxer.SourceAction="electric";boxer.SourceAt=110;boxer.SourceUntil=110.4f;Check(NativeVisualMotion.Choose(fists,boxer,110.1f,new NativeVisualMotion.Memory(),book2,false,out jab)&&jab.Clip==fists.Electric&&Math.Abs(jab.Time-.42f)<1e-4f,"the electric punch's Hit on its quarter");
            boxer.SourceAction="mystery";boxer.SourceUntil=115;Check(!NativeVisualMotion.Choose(fists,boxer,112,new NativeVisualMotion.Memory(),book2,false,out _)&&NativeVisualMotion.Declined=="fighter","a fighter move without a clip keeps the game's pose");
            var stepper=new Actor{Grounded=true,SourceSpec="ansatsuken",RollAt=95};Check(NativeVisualMotion.Choose(fists,stepper,95.1f,new NativeVisualMotion.Memory(),book2,false,out var step)&&step.Clip==fists.Step,"the Ansatsuken's roll, its Demon Step, plays the pack's dash");
            stepper.SourceSpec="emperor";Check(NativeVisualMotion.Choose(fists,stepper,95.1f,new NativeVisualMotion.Memory(),book2,false,out var empRoll)&&empRoll.Clip==fists.Roll,"the Emperor rolls the pack's roll");
            Check(shield.Idle.EndsWith(NativeVisualMotion.Mirror)&&Array.TrueForAll(shield.Attacks,c=>c.EndsWith(NativeVisualMotion.Mirror))&&shield.Locomotion[2]=="Armed-Shield-Run-Left"+NativeVisualMotion.Mirror,"the shield's set plays mirrored (its shield in the right hand): a mirrored run to the left runs right");
            foreach(var s in new[]{set,sword,daggers,shield,rifle,pistol,staff,bow,fists})Check(s.Locomotion.Length==NativeVisualStride.Clips&&s.Attacks.Length>=1&&s.Reactions.Length==4&&Array.TrueForAll(s.Locomotion,c=>Array.IndexOf(s.All,c)>=0)&&Array.TrueForAll(s.Attacks,c=>Array.IndexOf(s.All,c)>=0),"a set lists every clip it plays: "+s.Spec);
            Check(set.Locomotion.Length==NativeVisualStride.Clips&&Array.IndexOf(set.All,set.Idle)>=0&&Array.TrueForAll(set.Attacks,c=>Array.IndexOf(set.All,c)>=0),"the set lists its locomotion in the stride's order and every clip it plays");
            NativeVisualMotion.Pick P(Actor a,float t,bool moving=false,NativeVisualMotion.Memory m=null){Check(NativeVisualMotion.Choose(set,a,t,m??new NativeVisualMotion.Memory(),book,moving,out var p),"covered at "+t);return p;}
            Actor Ground()=>new Actor{Grounded=true};
            var idle=P(Ground(),50);Check(idle.Clip==set.Idle&&!idle.Locomotion,"standing: the idle");
            Check(P(Ground(),50,true).Locomotion,"moving on the ground: the locomotion blend");
            var cut=Ground();cut.AttackAt=10;cut.AttackUntil=11;cut.SwingAt=10.3f;cut.SwingUntil=10.5f;cut.Combo=0;
            Check(P(cut,10).Clip==set.Attacks[0]&&Math.Abs(P(cut,10).Time)<1e-4f,"a strike starts its clip at its start");
            Check(Math.Abs(P(cut,10.4f).Time-.42f)<1e-4f,"its Hit lands mid hit window");
            Check(Math.Abs(P(cut,10.999f).Time-1.2f)<.01f&&P(cut,11).Clip==set.Idle,"its recovery ends with the strike");
            float last=-1;for(float t=10;t<11;t+=.01f){float c=P(cut,t).Time;Check(c>=last-1e-5f,"a strike's clip only moves forward");last=c;}
            cut.Combo=1;Check(P(cut,10.2f).Clip==set.Attacks[1],"the combo's next cut its next clip");cut.Combo=set.Attacks.Length;Check(P(cut,10.2f).Clip==set.Attacks[0],"the combo's clips cycle");
            cut.SprintAttack=true;Check(P(cut,10.2f).Clip==set.SprintAttack,"a sprint attack its own clip");
            var roll=Ground();roll.RollAt=5;Check(P(roll,5+roll.VisualRollDuration*.5f).Clip==set.Roll&&Math.Abs(P(roll,5+roll.VisualRollDuration*.5f).Time-roll.VisualRollDuration*.5f*NativeVisualMotion.MaxRollRate)<1e-3f,"a roll plays through the roll at most MaxRollRate times its pace");
            var plunge=new Actor{Grounded=false,AirAttack=true,AttackAt=20,AttackUntil=21,ImpactAt=20.5f};Check(P(plunge,20.5f).Clip==set.AirAttack&&Math.Abs(P(plunge,20.5f).Time-.42f)<1e-4f,"the plunge's Hit on its impact");
            var air=new Actor{Grounded=false,Velocity=new V3(0,3,0)};Check(P(air,30).Clip==set.Jump,"rising: the jump");
            air.Velocity=new V3(0,-3,0);Check(P(air,30).Clip==set.Fall,"falling: the fall");
            air.AirJumps=1;air.AirJumpAt=30;Check(P(air,30.1f).Clip==set.Flip,"an air jump: the flip");
            var mem=new NativeVisualMotion.Memory();var land=new Actor{Grounded=false};P(land,40,false,mem);land.Grounded=true;
            Check(P(land,40.05f,false,mem).Clip==set.Land&&P(land,40.05f+NativeVisualMotion.LandFor+.01f,false,mem).Clip==set.Idle,"a landing, then the idle");
            var hit=Ground();hit.ReactionAt=60;hit.ReactionUntil=60.5f;hit.ReactionYaw=0;Check(P(hit,60.2f).Clip==set.Reactions[0],"struck from the front");
            hit.ReactionYaw=(float)Math.PI;Check(P(hit,60.2f).Clip==set.Reactions[1],"struck from behind");
            Check(NativeVisualMotion.Side(1.2f)==2&&NativeVisualMotion.Side(-1.2f)==3&&NativeVisualMotion.Side(.3f)==0&&NativeVisualMotion.Side(3f)==1,"reaction sides by the yaw");
            var block=Ground();block.BlockAt=70;block.BlockUntil=71;Check(P(block,70.5f).Clip==set.Block,"blocking: the block");
            var special=Ground();special.SpecialKind="wolf-spin";special.SpecialAt=80;special.SpecialUntil=83;
            Check(!NativeVisualMotion.Choose(set,special,81,new NativeVisualMotion.Memory(),book,false,out _),"a special keeps the game's pose");
            var kit=Ground();kit.MeleeKit=new NativeMeleeKit{Kind="flash-step",At=90,Until=92};Check(!NativeVisualMotion.Choose(set,kit,91,new NativeVisualMotion.Memory(),book,false,out _),"a kit move the set does not map keeps the game's pose");
            // Knocked down: the pack's fall airborne (held short of the impact), its landing and lying, then the backward roll.
            var down=Ground();down.DownAt=95;down.DownUntil=97;var downBook=new GunBook();
            Check(NativeVisualMotion.Choose(set,down,95.3f,new NativeVisualMotion.Memory(),downBook,false,out var downPick)&&downPick.Clip==set.Knockdown&&Math.Abs(downPick.Time-.3f)<1e-4f,"knocked down, airborne: the fall's flight");
            Check(NativeVisualMotion.Choose(set,down,96.5f,new NativeVisualMotion.Memory(),downBook,false,out downPick)&&downPick.Clip==set.Knockdown&&Math.Abs(downPick.Time-1.2f*NativeVisualMotion.KnockImpact*NativeVisualMotion.KnockFlight)<1e-4f,"a long flight holds short of the impact");
            down.DownLanded=95.6f;down.DownPickup=95.82f;down.DownRoll=96f;down.DownRollEnd=96.4f;down.DownUntil=96.6f;
            Check(NativeVisualMotion.Choose(set,down,95.7f,new NativeVisualMotion.Memory(),downBook,false,out downPick)&&downPick.Clip==set.Knockdown&&Math.Abs(downPick.Time-(1.2f*NativeVisualMotion.KnockImpact+.1f))<1e-4f,"landed: the impact and lying");
            Check(NativeVisualMotion.Choose(set,down,96.3f,new NativeVisualMotion.Memory(),downBook,false,out downPick)&&downPick.Clip==set.RollBack&&downPick.Time>1.2f*NativeVisualMotion.RollFromBack,"the get-up: the backward roll from on its back");
            var juggled=Ground();juggled.JuggleUntil=99;Check(NativeVisualMotion.Choose(set,juggled,98.5f,new NativeVisualMotion.Memory(),downBook,false,out downPick)&&downPick.Clip==set.Knockdown,"juggled: the fall's flight");
            Check(!NativeVisualMotion.Choose(sword,down,96,new NativeVisualMotion.Memory(),book,false,out _)&&NativeVisualMotion.Declined=="down","without the clips a knockdown keeps the game's pose");
            Check(Math.Abs(NativeVisualMotion.Retime(1,1,2,3,.4f,1.2f))<1e-6f&&Math.Abs(NativeVisualMotion.Retime(2,1,2,3,.4f,1.2f)-.4f)<1e-6f&&Math.Abs(NativeVisualMotion.Retime(9,1,2,3,.4f,1.2f)-1.2f)<1e-6f,"retiming: start, Hit and end held");
            // Motion polish (owner 2026-10-27: "Make the movements look realistic and not glitchy. For everything, movements,
            // attacks, for all weapons"), measured frame by frame in play on every weapon.
            {
                // The Hit: on the window's middle while it is replicated and after it resets (the clip jumped 0.08-0.11 s on).
                var swing=new Actor{Grounded=true,SourceSpec="sword",AttackAt=10,AttackUntil=10.62f,SwingAt=10.14f,SwingUntil=10.3f,VisualWindup=.14f,VisualActive=.16f};
                float open=NativeVisualMotion.HitAt(swing);NativeVisualMotion.Choose(sword,swing,10.35f,new NativeVisualMotion.Memory(),book2,false,out var during);
                swing.SwingAt=swing.SwingUntil=-100;NativeVisualMotion.Choose(sword,swing,10.35f,new NativeVisualMotion.Memory(),book2,false,out var after);
                Check(Math.Abs(open-10.22f)<1e-5f&&Math.Abs(NativeVisualMotion.HitAt(swing)-open)<1e-5f&&Math.Abs(during.Time-after.Time)<1e-5f,"a swing's clip runs on as its hit window resets");
                // A wind-up too short for its clip starts further in, at most MaxStrikeRate times its pace; its Hit still mid window.
                var quick=new Actor{Grounded=true,SourceSpec="daggers",AttackAt=20,AttackUntil=20.4f,SwingAt=20.1f,SwingUntil=20.2f};
                NativeVisualMotion.Choose(daggers,quick,20,new NativeVisualMotion.Memory(),book2,false,out var q0);NativeVisualMotion.Choose(daggers,quick,20.075f,new NativeVisualMotion.Memory(),book2,false,out var q1);
                NativeVisualMotion.Choose(daggers,quick,20.15f,new NativeVisualMotion.Memory(),book2,false,out var q2);NativeVisualMotion.Choose(daggers,quick,20.25f,new NativeVisualMotion.Memory(),book2,false,out var q3);
                Check(Math.Abs(q0.Time-(.42f-.15f*NativeVisualMotion.MaxStrikeRate))<1e-4f&&Math.Abs((q1.Time-q0.Time)/.075f-NativeVisualMotion.MaxStrikeRate)<1e-3f&&Math.Abs(q2.Time-.42f)<1e-4f
                    &&Math.Abs((q3.Time-q2.Time)/.1f-NativeVisualMotion.MaxStrikeRate)<1e-3f,"a quick strike: into its wind-up late, its Hit mid window, never faster than MaxStrikeRate");
                Check(Math.Abs(NativeVisualMotion.Retime(5,1,2,3,.4f,1.2f,10)-1.2f)<1e-6f&&Math.Abs(NativeVisualMotion.Retime(2.5f,1,2,3,.4f,1.2f,.5f)-.65f)<1e-5f&&Math.Abs(NativeVisualMotion.Retime(1,1,2,3,.4f,1.2f,.2f)-.2f)<1e-5f,"the capped retime: a slow fit unchanged, a quick one limited each side");
                // The jump from its launch (its crouch is the game's own push); a landing at about its own pace.
                var jm=new NativeVisualMotion.Memory();var hop=new Actor{Grounded=true};P(hop,29,false,jm);hop.Grounded=false;hop.Velocity=new V3(0,4,0);var rise=P(hop,30,false,jm);
                Check(rise.Clip==set.Jump&&Math.Abs(rise.Time-NativeVisualMotion.JumpLaunch)<1e-5f&&rise.Fade==NativeVisualMotion.JumpFade,"a jump plays from its launch");
                var lm=new NativeVisualMotion.Memory();var down2=new Actor{Grounded=false};NativeVisualMotion.Choose(sword,down2,40,lm,book2,false,out _);down2.Grounded=true;NativeVisualMotion.Choose(sword,down2,40.05f,lm,book2,false,out _);
                Check(NativeVisualMotion.Choose(sword,down2,40.15f,lm,book2,false,out var landed)&&landed.Clip==sword.Land&&Math.Abs(landed.Time-.1f*Math.Max(NativeVisualMotion.LandRate,1.2f/NativeVisualMotion.LandFor))<1e-4f
                    &&NativeVisualMotion.Choose(sword,down2,40.06f+NativeVisualMotion.LandFor,lm,book2,false,out var stood)&&stood.Clip==sword.Idle,"a landing at its own pace or a little quicker, done within LandFor");
                // A hit reaction at about its own pace (it twitched at two to three times); a block held on keeps its clip going.
                var struck=Ground();struck.ReactionAt=60;struck.ReactionUntil=60.25f;
                Check(Math.Abs(P(struck,60.2f).Time-.2f*NativeVisualMotion.MaxStrikeRate)<1e-4f,"a quick reaction plays at most MaxStrikeRate times its pace");
                var bm=new NativeVisualMotion.Memory();var held=Ground();held.BlockAt=70;held.BlockUntil=70.3f;var b1=P(held,70.2f,false,bm);held.BlockAt=70.28f;held.BlockUntil=70.58f;var b2=P(held,70.4f,false,bm);
                held.BlockAt=75;held.BlockUntil=75.3f;var b3=P(held,75.1f,false,bm);
                Check(b1.Clip==set.Block&&Math.Abs(b1.Time-.2f)<1e-4f&&Math.Abs(b2.Time-.4f)<1e-4f&&Math.Abs(b3.Time-.1f)<1e-4f,"a block raised again while held goes on; a new block starts afresh");
                // The stride's phase: from the knees' bend, so the mixed runs and walks step together.
                NativeVisualMotionClips.Clip Gait(float shift){int n=24,w=NativeVisualMotionClips.Head+2;var c=new NativeVisualMotionClips.Clip{Frames=n+1,Stride=w,Rate=30,Loop=true,Length=n/30f,Data=new float[(n+1)*w]};
                    for(int f=0;f<=n;f++){double a=2*Math.PI*(f/(double)n-shift);c.Data[f*w+6]=1;c.Data[f*w+NativeVisualMotionClips.Head]=(float)Math.Cos(a);c.Data[f*w+NativeVisualMotionClips.Head+1]=-(float)Math.Cos(a);}return c;}
                float g0=NativeVisualMotionClips.StridePhase(Gait(0),0,1),g3=NativeVisualMotionClips.StridePhase(Gait(.3f),0,1);
                Check(Math.Abs(g0-.5f)<1e-3f&&Math.Abs(g3-.8f)<1e-3f&&NativeVisualMotionClips.StridePhase(Gait(0),-1,1)==0,"a stride's phase from its knees, and its shift");
                // Between frames: a smooth curve through them, never past them.
                var ramp=new NativeVisualMotionClips.Clip{Frames=4,Stride=NativeVisualMotionClips.Head+1,Rate=1,Length=3,Data=new float[4*(NativeVisualMotionClips.Head+1)]};
                for(int f=0;f<4;f++){ramp.Data[f*(NativeVisualMotionClips.Head+1)+6]=1;ramp.Data[f*(NativeVisualMotionClips.Head+1)+NativeVisualMotionClips.Head]=f<2?0:1;}
                var smix=new float[NativeVisualMotionClips.Head+1];float lastV=-1;bool inside=true,rising=true;
                for(float s=0;s<=3;s+=.05f){Array.Clear(smix,0,smix.Length);NativeVisualMotionClips.Add(ramp,s,1,smix);NativeVisualMotionClips.Finish(smix,1);float v=smix[NativeVisualMotionClips.Head];inside&=v>=-1e-5f&&v<=1+1e-5f;rising&=v>=lastV-1e-5f;lastV=v;}
                Array.Clear(smix,0,smix.Length);NativeVisualMotionClips.Add(ramp,1.5f,1,smix);NativeVisualMotionClips.Finish(smix,1);
                Check(inside&&rising&&Math.Abs(smix[NativeVisualMotionClips.Head]-.5f)<1e-5f,"a clip's curve between frames neither overshoots nor turns back");
                string mo=File.ReadAllText(Path.Combine(root,"Assets","ChipArena","Runtime","NativeVisualMotionPresentation.cs")).Replace("\r\n","\n");
                Check(mo.Contains("if(m.Weights[ti]>.02f&&(d<-.02f||d>.05f+dt*2.5f))jumped=true;")&&mo.Contains("if(upper&&m.UpA!=null&&(up.A!=m.UpA||!(Mathf.Abs(up.TimeA-m.UpTimeA-dt)<.05f+dt*2.5f)))jumped=true;")
                    &&mo.Contains("VisualMotionInertia(m,jumped,dt,upper);")&&mo.IndexOf("VisualMotionInertia(m,jumped,dt,upper);",StringComparison.Ordinal)<mo.IndexOf("VisualMotionApply(m,m.Mix);",StringComparison.Ordinal),
                    "a pose that would jump is carried over (its difference sprung away) before it goes on the body");
                Check(mo.Contains("NativeVisualMotionClips.Add(m.Clips[i],m.Times[i],Ease(m.Weights[i]),m.Mix);")&&mo.Contains("if(i!=target&&m.Weights[i]>0){var c=m.Clips[i];m.Times[i]=c.Loop?m.Times[i]+dt:Mathf.Min(m.Times[i]+dt,c.Length);}")
                    &&mo.Contains("Mathf.Repeat(m.Phase+m.LocoOffset[k],1)*m.LocoLength[k]"),"crossfades eased, a fading clip going on at its pace, each stride on its offset");
                Check(mo.Contains("if(m.GuardSeen&&pin!=m.GuardPin){m.GuardCarry+=m.GuardApplied-goal;m.GuardCarryVel=0;}")&&mo.Contains("float applied=m.GuardApplied=Mathf.Min(goal+m.GuardCarry,deepest);"),"the guard's switch carried off, never under the floor");
                int feet=mo.IndexOf("VisualMotionFeet(m,body.Root.transform,plant,pick.Locomotion,dt,wgt);",StringComparison.Ordinal),guarded=mo.IndexOf("m.BodyHips.position-=origin.up*(applied*wgt);",StringComparison.Ordinal),carried=mo.IndexOf("if(m.Trail>0)VisualSwordTrail(",StringComparison.Ordinal);
                Check(feet>guarded&&carried>feet,"the feet plant after the guard and before the sword's carry");
                // The hats on the model (owner 2026-10-27: "the hats still don't sit naturally and are too big"): scaled from the voxel head,
                // a brimmed hat's band about 5 cm over the brows, a halo over the crown, the hair pressed under a worn hat.
                NativeModelHats.Fit("cap",out float hatScale,out var capAt);NativeModelHats.Fit("halo",out float haloScale,out var haloAt);
                float capBand=capAt.Y+hatScale*.328f,haloRing=haloAt.Y+haloScale*.522f;
                Check(hatScale>.5f&&hatScale<.7f&&capBand>.13f&&capBand<.17f&&haloRing>.30f&&haloScale<hatScale&&Math.Abs(capAt.X)<1e-6f,"a hat scaled to the model's head, a cap's band above the brows ("+capBand.ToString("0.000")+"), a halo over the crown");
                Check(NativeModelHats.Presses("cap")&&NativeModelHats.Presses("top")&&!NativeModelHats.Presses("halo")&&!NativeModelHats.Presses(NativeAuditorHalo.Hat)&&!NativeModelHats.Presses("none")&&!NativeModelHats.Presses(null),"the hair pressed under a worn hat, not under a halo");
                // The Sheldon Head as a hood over the model (its intro on the model too), the hair laid flat under it.
                string intros=File.ReadAllText(Path.Combine(root,"Assets","ChipArena","Runtime","NativeIntros.cs")).Replace("\r\n","\n"),srcp=File.ReadAllText(Path.Combine(root,"Assets","ChipArena","Runtime","NativeSourcePresentation.cs")).Replace("\r\n","\n");
                Check(NativeModelHats.Presses(NativeHatLooks.SheldonHead)&&!intros.Contains("if(sheldon==null)VisualIntroPose(")&&intros.Contains("VisualIntroPose(introRig,actor,arena.Time,entrancePose,entranceReveal);")
                    &&srcp.Contains("if(headShown&&visual&&!first&&a.Hat==NativeSheldonHead.Hat&&a.SourceSpec!=\"siren\"&&!SheldonHoodFace(rig,a,VisualHatBone(rig,a.Hat),float.IsNaN(halo)?time:halo,layer,camera,dressed?tint:null,flash))SheldonHead(a,rig.Root.transform.localToWorldMatrix,VisualHatBone(rig,a.Hat),")
                    &&NativeModelHats.SheldonScale.Z>NativeModelHats.SheldonScale.X,"the Sheldon Head a hood over the model's head, its intro on the model");
                // The model's gibs show flesh and bones where they lie (owner 2026-10-27: "Can you show bones + flesh on gibs too?
                // correct on anamonics too").
                Check(NativeModelGore.Kind("upperarm_twist_02_l")=="upperarm"&&NativeModelGore.Kind("spine_03")=="spine"&&NativeModelGore.Kind("calf_twist_01_l1")=="calf"&&NativeModelGore.Kind("head")=="head"
                    &&NativeModelGore.Side("thigh_l")==-1&&NativeModelGore.Side("calf_twist_01_r1")==1&&NativeModelGore.Side("neck_01")==0,"a bone's kind and side from its name");
                var forearm=NativeModelGore.At("lowerarm_l","upperarm_l");var shin=NativeModelGore.At("calf_r","thigh_r");
                Check(NativeModelGore.At("upperarm_l","clavicle_l").Length==1&&forearm.Length==2&&forearm[0].Outward>0&&forearm[1].Outward<0
                    &&shin.Length==2&&shin[0].Ahead>shin[1].Ahead&&shin[0].Radius>shin[1].Radius&&shin[1].Outward>0,"the forearm's radius outside its ulna, the shin's tibia ahead of and thicker than its fibula");
                Check(NativeModelGore.At("spine_02","pelvis")[0].Ahead<0&&NativeModelGore.At("neck_01","spine_05")[0].Ahead<0&&NativeModelGore.At("neck_01","spine_05")[1].Ahead>0
                    &&NativeModelGore.At("pelvis","thigh_l")[0].Ahead==0&&NativeModelGore.At("pelvis","spine_01")[0].Ahead<0&&NativeModelGore.At("thigh_l","pelvis")[0].Radius>NativeModelGore.At("upperarm_l","clavicle_l")[0].Radius
                    &&NativeModelGore.At("Hair_Front2","head").Length==0&&NativeModelGore.At("ik_hand_l","hand_l").Length==0,"the spine behind the middle and the windpipe ahead; the hip bone at a hip cut; the femur the thickest; none for hair or IK bones");
                string bodyp=File.ReadAllText(Path.Combine(root,"Assets","ChipArena","Runtime","NativeVisualBodyPresentation.cs")).Replace("\r\n","\n");
                Check(bodyp.Contains("private void VisualCorpseSever(VisualCorpse c,int[] pieceOfBone){VisualSever(c.All,c.Bones,pieceOfBone,ref c.Owned,true);VisualCorpseBones(c,pieceOfBone);}")
                    &&bodyp.Contains("private static readonly Color CutInside=new Color((NativeModelGore.Flesh>>16&255)/255f")&&bodyp.Contains("c.All.CopyTo(all,0);added.CopyTo(all,c.All.Length);c.All=all;"),"a cut corpse's caps in flesh, its bones on each side, dissolving with it");
                // The tech mask refitted: clear of the nose bridge between its top rows.
                Check(NativePilotGear.MaskSurface(0,.036f)>.153f&&NativePilotGear.MaskSurface(0,.039f)>.147f,"the mask clears the nose bridge");
                Check(mo.Contains("float twist=NativeSwordTrail.Twist(CapturedAnimation.Num(goal*Quaternion.Inverse(hand)),CapturedAnimation.Num(forearm.normalized))*Mathf.Rad2Deg;"),"the carry moves only its own turn of the fist to the arm");
                Check(File.ReadAllText(Path.Combine(root,"Assets","ChipArena","Runtime","NativeVisualStridePresentation.cs")).Replace("\r\n","\n").Contains("var m=body.Motion;if(m!=null&&m.Weight>=.5f&&m.LocoShare>=.5f){contacts=(m.FootLocked[0]?1:0)|(m.FootLocked[1]?2:0);return true;}"),
                    "footsteps sound as the model's drawn feet plant while its own clips carry the legs");
            }
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string motion=R("Runtime/NativeVisualMotionPresentation.cs"),body=R("Runtime/NativeVisualBodyPresentation.cs");
            int stride=body.IndexOf("VisualStridePose(body,rig,a,time,roll,juggle);",StringComparison.Ordinal),pose=body.IndexOf("VisualMotionPose(body,rig,a,time);",StringComparison.Ordinal),fit=body.IndexOf("VisualWeaponFit(right,held);",StringComparison.Ordinal);
            Check(stride>0&&pose>stride&&fit>pose,"the clips go on after the game pose and the stride, before the weapon fit");
            Check(motion.Contains("weaponHand.SetPositionAndRotation(m.RightHand.position+m.RightHand.rotation*m.HoldPosition,m.RightHand.rotation*m.HoldRotation);"),"the weapon rides the drawn hand at any weight");
            Check(motion.Contains("bool pin=!(covered&&(pick.Locomotion||pick.Flight));float goal=pin?deepest:Mathf.Min(deepest,0);")&&motion.Contains("m.BodyHips.position-=origin.up*(applied*wgt);"),"the body's lowest point stands on the pilot's floor point (running or a dash attack's leaping run, only kept out of it)");
            Check(motion.Contains("foreach(var r in proxy.GetComponentsInChildren<Renderer>(true))Destroy(r);")&&motion.Contains("m.Handler.SetHumanPose(ref m.Pose);"),"the motion proxy draws nothing and takes the mixed pose through its avatar");
            Check(motion.Contains("var bytes=Resources.Load<TextAsset>(VisualMotionResource);")&&motion.Contains("const string VisualMotionResource=\"ChipArenaVisualMotion\";"),"the clips come from the bake's motion bank, read once");
            // The bank: written and read back, a frame sampled and two clips mixed.
            int muscles=3,width=NativeVisualMotionClips.Head+muscles;
            NativeVisualMotionClips.Clip Make(string name,float y,float m0,bool loop){var c=new NativeVisualMotionClips.Clip{Name=name,Length=1,Hit=.4f,Rate=2,SpeedX=.5f,SpeedZ=3,Loop=loop,Frames=3,Stride=width,Data=new float[3*width]};
                for(int f=0;f<3;f++){int o=f*width;c.Data[o+1]=y+f*.1f;c.Data[o+6]=1;c.Data[o+7]=m0+f;}return c;}
            var bank=NativeVisualMotionClips.Read(NativeVisualMotionClips.Write(muscles,new[]{Make("a",1,0,false),Make("b",2,10,true)}));
            Check(bank.Muscles==muscles&&bank.Has("a")&&bank.Has("b")&&!bank.Has("c")&&bank.Length("a")==1&&bank.Hit("a")==.4f&&bank.Clips["b"].Loop&&bank.Clips["a"].SpeedZ==3,"the bank reads back what it wrote");
            var mix=new float[width];NativeVisualMotionClips.Add(bank.Clips["a"],.25f,1,mix);NativeVisualMotionClips.Finish(mix,1);
            Check(Math.Abs(mix[1]-1.05f)<1e-5f&&Math.Abs(mix[7]-.5f)<1e-5f&&Math.Abs(mix[6]-1)<1e-5f,"a clip samples between its frames");
            Array.Clear(mix,0,width);NativeVisualMotionClips.Add(bank.Clips["a"],9,1,mix);NativeVisualMotionClips.Finish(mix,1);Check(Math.Abs(mix[7]-2)<1e-5f,"a clip that does not loop holds its last frame");
            Array.Clear(mix,0,width);NativeVisualMotionClips.Add(bank.Clips["a"],0,1,mix);NativeVisualMotionClips.Add(bank.Clips["b"],0,3,mix);NativeVisualMotionClips.Finish(mix,4);
            Check(Math.Abs(mix[1]-1.75f)<1e-5f&&Math.Abs(mix[7]-7.5f)<1e-5f,"clips mix by weight");
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++){NativeVisualMotionClips.Add(bank.Clips["b"],i*.01f,.5f,mix);NativeVisualMotionClips.Finish(mix,.5f);}
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"mixing allocates nothing");
            bool bad=false;try{NativeVisualMotionClips.Read(new byte[]{1,2,3,4,5,6,7,8});}catch(InvalidDataException){bad=true;}Check(bad,"a bank that is not one is refused");
            Check(motion.Contains("NativeVisualMotion.For(a.SourceSpec)!=null&&!StrideOff?VisualMotionFor(body,a.SourceSpec):null"),"--ca-no-stride keeps the game's pose");
            Check(body.Contains("if(body.Root==null){VisualStrideRelease(body);VisualMotionRelease(body);"),"each motion graph goes with its body");
            // The sword's run (owner 2026-10-27: "Fix run for sword as well, it looks not very styled lol"): the arm swept back,
            // the blade trailing out and back, the chest leaning in, running ahead only.
            {
                Check(NativeSwordTrail.Share("sword",0,6.8f)==1&&NativeSwordTrail.Share("sword",0,9.2f)==1&&NativeSwordTrail.Share("sword",0,2)==0&&NativeSwordTrail.Share("sword",0,-6.8f)==0&&NativeSwordTrail.Share("sword",6.8f,0)==0&&NativeSwordTrail.Share("sword",0,0)==0
                    &&NativeSwordTrail.Share("greatsword",0,6.8f)==0&&NativeSwordTrail.Share("daggers",0,6.8f)==0&&NativeSwordTrail.Share(null,0,6.8f)==0,"the trailing carry: the sword only, from the run (sprint too), heading ahead; none walking, strafing or back-pedalling");
                float half=NativeSwordTrail.Share("sword",4.8f,4.8f),walkRun=NativeSwordTrail.Share("sword",0,3.3f);
                Check(half>0&&half<1&&walkRun>0&&walkRun<1,"it eases in between the walk and the run, and toward a strafe");
                var trailArm=NativeSwordTrail.Arm(0);
                Check(Math.Abs(trailArm.Length()-1)<1e-5f&&Math.Abs(NativeSwordTrail.Pitch(trailArm)-NativeSwordTrail.ArmBack)<.01f&&trailArm.X>0&&trailArm.Z<0&&trailArm.Y<-.7f,"the arm reaches down, back and out from the shoulder");
                Check(Math.Abs(NativeSwordTrail.Pitch(NativeSwordTrail.Arm(99))-(NativeSwordTrail.ArmBack+NativeSwordTrail.SwingLimit))<.01f&&Math.Abs(NativeSwordTrail.Pitch(NativeSwordTrail.Arm(-99))-(NativeSwordTrail.ArmBack-NativeSwordTrail.SwingLimit))<.01f,"the clip's swing kept within its limit");
                Check(NativeSwordTrail.Blade.X>.5f&&NativeSwordTrail.Blade.Z<-.5f&&NativeSwordTrail.Blade.Y<0&&NativeSwordTrail.Blade.Y>-.4f,"the blade trails out and back, a little low");
                Check(NativeSwordTrail.LeanAt(6.8f)==NativeSwordTrail.Lean&&Math.Abs(NativeSwordTrail.LeanAt(9.2f)-(NativeSwordTrail.Lean+NativeSwordTrail.SprintLean))<1e-5f,"the chest leans in, more at a sprint");
                // A fist whose knuckles run along its +x and whose blade leaves it 80 degrees from them.
                var knuckleLocal=Vector3.UnitX;var bladeLocal=Vector3.Normalize(new Vector3((float)Math.Sin(.1745),(float)Math.Cos(.1745),0));
                float Deg(Vector3 a,Vector3 b)=>NativeSwordTrail.Angle(Vector3.Normalize(a),Vector3.Normalize(b))*180/(float)Math.PI;
                foreach(var forearm in new[]{trailArm,Vector3.Normalize(new Vector3(0,-.7f,.7f)),-Vector3.UnitY,Vector3.Normalize(new Vector3(.6f,-.3f,-.7f))})
                {
                    var q=NativeSwordTrail.Hand(bladeLocal,knuckleLocal,forearm,NativeSwordTrail.Blade,NativeSwordTrail.MaxBend);
                    var b=Vector3.Transform(bladeLocal,q);var k=Vector3.Transform(knuckleLocal,q);
                    Check(Math.Abs(q.Length()-1)<1e-4f&&Math.Abs(Deg(b,k)-80)<.1f&&Deg(k,forearm)<=NativeSwordTrail.MaxBend+.1f,"the fist keeps its grip and bends the wrist at most MaxBend");
                    float needed=Math.Abs(Deg(forearm,NativeSwordTrail.Blade)-80);
                    Check(Deg(b,NativeSwordTrail.Blade)<=Math.Max(0,needed-NativeSwordTrail.MaxBend)+.2f,"the blade lies on the trail, or as near as the wrist's bend allows");
                }
                Check(Deg(Vector3.Transform(bladeLocal,NativeSwordTrail.Hand(bladeLocal,knuckleLocal,trailArm,NativeSwordTrail.Blade,NativeSwordTrail.MaxBend)),NativeSwordTrail.Blade)<10,"on the trailing arm the blade comes within 10 degrees of the trail");
                var pure=Quaternion.CreateFromAxisAngle(Vector3.UnitY,-2.5f);var swung=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.7f)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,.3f);
                Check(Math.Abs(NativeSwordTrail.Twist(pure,Vector3.UnitY)+2.5f)<1e-4f&&Math.Abs(NativeSwordTrail.Twist(swung,Vector3.UnitY)-.7f)<1e-4f&&Math.Abs(NativeSwordTrail.Twist(Quaternion.Identity,Vector3.UnitY))<1e-6f,"a turn's twist about an axis, its swing aside");
                // On a body: the shoulder 1.42 m up, the arm 0.6 m, a 0.95 m blade: the fist by the hip, the tip clear of the floor and out past the leg.
                var shoulder=new Vector3(.19f,1.42f,0);var fist=shoulder+trailArm*.6f*NativeSwordTrail.Reach;var tip=fist+NativeSwordTrail.Blade*.95f;
                Check(fist.Y>.8f&&fist.Z<-.2f&&tip.Y>.5f&&tip.X>.7f&&tip.Z<-.8f,"the fist rides behind the hip and the tip stays high and wide of the legs");
                Check(motion.Contains("m.Trail=Mathf.MoveTowards(m.Trail,covered&&pick.Locomotion&&m.HasHold?NativeSwordTrail.Share(m.Set.Spec,local.x,local.z):0,dt/NativeSwordTrail.Ease);")
                    &&motion.Contains("if(m.Trail>0)VisualSwordTrail(m,body.Root.transform,m.Trail*m.LocoShare*wgt,speed,dt);else m.TrailMean=m.TrailTwist=float.NaN;"),"the carry rides the locomotion blend's share and eases away with it");
                int carry=motion.IndexOf("if(m.Trail>0)VisualSwordTrail(",StringComparison.Ordinal),guard=motion.IndexOf("m.BodyHips.position-=origin.up*(applied*wgt);",StringComparison.Ordinal),held=motion.IndexOf("weaponHand.SetPositionAndRotation(m.RightHand.position",StringComparison.Ordinal);
                Check(carry>guard&&held>carry,"the carry goes on after the floor guard and before the weapon rides the hand (the blade follows the fist)");
                Check(motion.Contains("m.RightArm.rotation=Quaternion.AngleAxis(twist*NativeSwordTrail.ShoulderTwist,(fist-shoulder).normalized)*m.RightArm.rotation;")&&motion.Contains("m.RightHand.rotation=goal;"),"the fist's twist goes to the shoulder and the forearm, not the wrist");
                foreach(string pack in new[]{"Bozo","BoZo","ExplosiveLLC","RPG-Character"})Check(!R("Core/NativeSwordTrail.cs").Contains(pack),"no pack names in the sword's carry: "+pack);
            }
            foreach(string pack in new[]{"Bozo","BoZo","Magica","OutfitSystem","BMAC","BSMC","ExplosiveLLC","RPG-Character"})Check(!motion.Contains(pack)&&!R("Core/NativeVisualMotion.cs").Contains(pack),"no pack types or names in the motion's code: "+pack);
        }

        // The visual body's stride (owner 2026-10-23: "I thought we have a running animation pack already? Cant we utilize that?"):
        // Core NativeVisualStride's weights and rate against the clips' measured travel on the body (2026-10-24, the body's avatar),
        // and its wiring (Runtime NativeVisualStridePresentation.cs).
        private static void Stride(string root)
        {
            int n=NativeVisualStride.Clips;var sx=new float[n];var sz=new float[n];var w=new float[n];
            void Clip(int i,float x,float z){sx[i]=x;sz[i]=z;}
            Clip(0,0,4.26f);Clip(1,2.58f,2.58f);Clip(2,3.72f,0);Clip(3,2.55f,-2.54f);Clip(4,0,-3.67f);Clip(5,-2.65f,-2.59f);Clip(6,-3.73f,0);Clip(7,-2.55f,2.57f);
            Clip(8,0,2.15f);Clip(9,1.48f,1.48f);Clip(10,1.97f,0);Clip(11,1.48f,-1.48f);Clip(12,0,-2.10f);Clip(13,-1.48f,-1.48f);Clip(14,-2.04f,0);Clip(15,-1.48f,1.48f);Clip(16,0,6.87f);
            float Sum(){float s=0;foreach(var x in w)s+=x;return s;}
            float Travel(float vx,float vz,float rate){float bx=0,bz=0;for(int i=0;i<n;i++){bx+=w[i]*sx[i];bz+=w[i]*sz[i];}float speed=(float)Math.Sqrt(vx*vx+vz*vz);return rate*(bx*vx+bz*vz)/speed;}
            foreach(var (vx,vz) in new[]{(0f,6.8f),(0f,9.18f),(6.8f,0f),(-3f,-3f),(0f,2f),(0f,3.3f),(1.2f,-5f),(-6.8f,.5f),(.3f,-1.1f),(4f,8f)})
            {
                float rate=NativeVisualStride.Weights(vx,vz,sx,sz,w),speed=(float)Math.Sqrt(vx*vx+vz*vz);
                Check(Math.Abs(Sum()-1)<1e-4f,"stride weights sum to one at ("+vx+", "+vz+")");
                foreach(var x in w)Check(x>=0&&x<=1,"stride weights within 0..1");
                if(rate>NativeVisualStride.RateMin+1e-3f&&rate<NativeVisualStride.RateMax-1e-3f)Check(Math.Abs(Travel(vx,vz,rate)-speed)<.01f*speed,"the mix travels at the body's speed at ("+vx+", "+vz+")");
            }
            float r=NativeVisualStride.Weights(0,6.8f,sx,sz,w);Check(w[0]>.999f&&Math.Abs(r-6.8f/4.26f)<.01f,"the game's run forward plays the forward run at 1.6 times");
            r=NativeVisualStride.Weights(0,9.18f,sx,sz,w);Check(w[NativeVisualStride.Sprint]>.999f&&Math.Abs(r-9.18f/6.87f)<.01f,"the sprint forward plays the sprint clip");
            NativeVisualStride.Weights(9.18f,0,sx,sz,w);Check(w[NativeVisualStride.Sprint]==0&&w[2]>.999f,"sideways at sprint speed runs to the right, no sprint");
            NativeVisualStride.Weights(-3,-3,sx,sz,w);Check(w[5]>.999f,"back-left travel runs back-left");
            NativeVisualStride.Weights(0,2,sx,sz,w);Check(w[NativeVisualStride.Walks]>.999f,"a slow forward walk walks forward");
            NativeVisualStride.Weights(0,3.3f,sx,sz,w);Check(w[0]>.3f&&w[NativeVisualStride.Walks]>.3f,"between the gaits the walk and run mix");
            NativeVisualStride.Weights(3,3,sx,sz,w);Check(w[1]>.999f,"forward-right travel runs forward-right");
            Check(NativeVisualStride.LegWeight(0)==0&&NativeVisualStride.LegWeight(NativeVisualStride.Moving)==0&&NativeVisualStride.LegWeight(NativeVisualStride.Moving+NativeVisualStride.Ramp)==1,"the legs take the clips once moving");
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)NativeVisualStride.Weights(i*.01f,6-i*.005f,sx,sz,w);
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"the stride weights allocate nothing");
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string stride=R("Runtime/NativeVisualStridePresentation.cs"),body=R("Runtime/NativeVisualBodyPresentation.cs");
            int hands=body.IndexOf("var held=VisualBodyBind(body,rig,a,time,pose,a.Grounded&&!roll&&!juggle,roll||juggle);",StringComparison.Ordinal),pose=body.IndexOf("VisualStridePose(body,rig,a,time,roll,juggle);",StringComparison.Ordinal),fit=body.IndexOf("VisualWeaponFit(right,held);",pose,StringComparison.Ordinal);
            Check(hands>0&&pose>hands&&fit>pose,"the stride takes the legs after the game pose and the hands on the weapon frames, before the weapon fit");
            Check(stride.Contains("bool free=a.Grounded&&!roll&&!juggle&&!a.Attacking(time)&&time>=a.StunUntil&&!(time>=a.SpecialAt&&time<a.SpecialUntil)&&!(time>=a.ReactionAt&&time<a.ReactionUntil);"),"the stride is off in the air, rolling, attacking, stunned, juggled, in a special or a reaction");
            Check(stride.Contains("rig.Bones[CapturedRig.LHand].position+=shift;rig.Bones[CapturedRig.RHand].position+=shift;"),"the weapon frames move with the hips");
            Check(stride.Contains("foreach(var r in proxy.GetComponentsInChildren<Renderer>(true))Destroy(r);")&&stride.Contains("s.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);"),"the proxy draws nothing and its graph is evaluated only by the pose");
            Check(body.Contains("if(body.Root==null){VisualStrideRelease(body);")&&body.Contains("{VisualStrideRelease(body);VisualMotionRelease(body);if(body.Root!=null)Destroy(body.Root);}"),"each stride graph goes with its body");
            Check(body.Contains("if(id!=ViewBodyId)body.Stride=VisualStrideOf(root.transform,skeleton);"),"first person has no stride");
            Check(stride.Contains("if(!s.Locked[f]&&h<StrideDown){s.Locked[f]=true;s.Lock[f]=foot.position;s.LockWeight[f]=1;}")&&stride.Contains("var turn=foot.rotation;StrideReachTo(hip,knee,foot,Vector3.Lerp(foot.position,s.Lock[f],w));foot.rotation=turn;"),"a planted foot stays where it landed, its leg reaching it, the foot keeping the clip's turn");
            Check(R("Runtime/NativeFootfallPresentation.cs").Contains("if(contacts>=0&&VisualStrideContacts(a.Id,out int drawn))contacts=drawn;")&&stride.Contains("contacts=(s.Locked[0]?1:0)|(s.Locked[1]?2:0);return true;"),"footsteps sound when the drawn feet plant while the stride carries the legs");
            foreach(string pack in new[]{"Bozo","BoZo","Magica","OutfitSystem","BMAC","BSMC"})Check(!stride.Contains(pack),"no pack types or names in the stride's code: "+pack);
        }

        // The visual body's own weapons (owner 2026-10-23: "the guns seem "comically" big"; Runtime NativeVisualWeaponPresentation.cs).
        private static void Weapons(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string weapons=R("Runtime/NativeVisualWeaponPresentation.cs"),body=R("Runtime/NativeVisualBodyPresentation.cs"),source=R("Runtime/NativeSourcePresentation.cs");
            Check(weapons.Contains("WeaponSkin(a)!=NativeWeaponSkins.StandardIndex")&&weapons.Contains("a.WeaponAway"),"only a weapon in hand on the Standard skin draws the visual body's model (the Prototype Series keeps its own)");
            int bind=body.IndexOf("private VisualWeapon VisualBodyBind(",StringComparison.Ordinal),pick=body.IndexOf("var held=body.Held=VisualWeaponFor(body,a);",bind,StringComparison.Ordinal),apply=body.IndexOf("body.Binding.Apply(pose,grasp,",bind,StringComparison.Ordinal),hands=body.IndexOf("bone.SetPositionAndRotation(origin.TransformPoint(side==0?lp:rp)",bind,StringComparison.Ordinal);
            int call=body.IndexOf("var held=VisualBodyBind(body,rig,a,time,pose,",StringComparison.Ordinal),fit=call<0?-1:body.IndexOf("var right=rig.Bones[CapturedRig.RHand];body.RightScale=right.localScale;VisualWeaponFit(right,held);",call,StringComparison.Ordinal);
            Check(bind>0&&pick>bind&&apply>pick&&body.IndexOf("VisualWeaponGrasp(ref grasp,held);",pick,StringComparison.Ordinal)<apply&&hands>apply&&call>0&&fit>call,"the weapon is picked and its grasp set before the visual body poses; the right hand takes the fitted frame after the hands are placed");
            Check(body.Contains("rig.Bones[CapturedRig.RHand].localScale=body.RightScale;body.Moved=false;"),"the hand's scale is put back with its place before the rig poses again");
            Check(weapons.Contains("hand.position+=hand.TransformVector((1-w.Scale)*w.Centre);hand.localScale*=w.Scale;")&&weapons.Contains("frame=frame*Matrix4x4.Translate(w.Centre)*Matrix4x4.Scale(Vector3.one/w.Scale)*Matrix4x4.Translate(-w.Centre);"),"the fit scales the hand's frame about its centre, and the model draws in the frame without it");
            Check(weapons.Contains("g.SupportHold.Center=new System.Numerics.Vector3(-w.Support.x,w.Support.y,w.Support.z);"),"the support hold enters the source frame mirrored");
            Check(source.Contains("if(armed){if(!(visual&&VisualWeaponDrawn(rig,WeaponBone(CapturedRig.RHand),a.SourceSpec==\"daggers\"?WeaponBone(CapturedRig.LHand):(Matrix4x4?)null))){string held=HeldWeaponModel(rig,a,first);")&&source.Contains("glow:HeldGlow);}WeaponDressing(rig,a,time,first,layer,camera,reach);}"),"SourceCharacter draws the model in the game model's place, at the frame it would draw at; the dressing draws either way");
            Check(body.Contains("body.Weapons=VisualWeaponsOf(root.transform);")&&weapons.Contains("model.gameObject.SetActive(false);")&&body.Contains("VisualWeaponShown(body);body.Seen=false;"),"the models start hidden and hide each frame they are not drawn");
            foreach(string pack in new[]{"Bozo","BoZo","Magica","OutfitSystem","BMAC","BSMC","Polygon","RB_","SM_"})
                Check(!weapons.Contains(pack),"no pack types or names in the visual weapons' code: "+pack);
        }

        private static void Bind(NativeVisualRig.Rig rig,Vector3[] restWorld)
        {
            for(int j=1;j<CapturedRig.Bones.Length;j++) Check(rig.Of[j]>=0,"joint "+CapturedRig.Bones[j].Name+" has a visual bone");
            Check(rig.Of[CapturedRig.Body]<0,"the motion-root has no visual bone (the pelvis carries it)");
            Check(rig.Names[rig.Of[CapturedRig.Spine]]=="spine_02"&&rig.Names[rig.Of[CapturedRig.Chest]]=="spine_05"&&rig.Names[rig.Of[CapturedRig.Neck]]=="neck_02","split chains end on their last bone");
            Check(Math.Abs(NativeVisualRig.GameHeadRise-.24f)<1e-5f&&Math.Abs(rig.HeadRise-(restWorld[rig.Find("head")].Y-restWorld[rig.Find("upperarm_r")].Y))<1e-4f,"head rise: the game's head joint .24 over its shoulders, the visual head bone's over its shoulders");
            float leg=Vector3.Distance(restWorld[rig.Find("thigh_r")],restWorld[rig.Find("calf_r")])+Vector3.Distance(restWorld[rig.Find("calf_r")],restWorld[rig.Find("foot_r")]);
            Check(Math.Abs(rig.LegRatio-leg/.5f)<1e-4f,"the leg ratio is the visual hip-to-ankle (straightened) over the game's .50");
            var state=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();pose.World();
            NativeVisualRig.Pose(state,pose,0,false,false);
            foreach(string limb in new[]{"upperarm_l","lowerarm_l","upperarm_r","lowerarm_r","thigh_l","calf_l","thigh_r","calf_r"})
            {
                int v=rig.Find(limb),child=-1;
                for(int k=v+1;k<rig.Count;k++) if(rig.Parent[k]==v&&rig.Joint[k]>=0&&rig.Joint[k]!=rig.Joint[v]) { child=k; break; }
                bool arm=limb.Contains("arm");float outward=limb.EndsWith("_l")?1:-1;
                Vector3 rest=arm?new Vector3(outward*(float)Math.Sin(NativeVisualRig.ArmSpread),-(float)Math.Cos(NativeVisualRig.ArmSpread),0):-CapturedRig.Y;
                Check(Angle(state.Position[child]-state.Position[v],rest)<1e-4f,limb+(arm?" hangs 15 degrees out from the body":" points straight down")+" at the game's rest");
                Check(Math.Abs(Vector3.Distance(state.Position[child],state.Position[v])-Vector3.Distance(restWorld[child],restWorld[v]))<1e-5f,limb+" keeps its length");
            }
            foreach(string kept in new[]{"pelvis","spine_01","spine_03","spine_05","neck_02","head","clavicle_l","foot_l","foot_r","ball_l"})
            {
                int v=rig.Find(kept); Quaternion rest=Quaternion.Identity;
                for(int k=v;k>=0;k=rig.Parent[k]) rest=rig.RestRotation[k]*rest;
                Check(Near(state.World[v],rest,1e-5f),kept+" keeps its authored world turn at rest");
            }
            Check(Math.Abs(state.Position[rig.Find("hand_l")].X)>.26f&&Math.Abs(state.Position[rig.Find("hand_r")].X)>.26f,"hanging hands clear the hips (shoulders .14 out)");
            Check(Near(state.Position[rig.Find("pelvis")],restWorld[rig.Find("pelvis")],1e-5f)&&Near(state.Position[rig.Find("head")],restWorld[rig.Find("head")],1e-5f),"the trunk and head stay where authored at rest");
            int t=rig.Find("upperarm_twist_01_l"),st=rig.Find("shoulder_twist_l"),lt=rig.Find("lowerarm_twist_01_l"),ct=rig.Find("calf_twist_01_l"),tt=rig.Find("thigh_twist_01_l");
            Check(rig.TwistJoint[t]==CapturedRig.LArm&&Math.Abs(rig.TwistShare[t]+(1-.09f/.26f))<1e-3f&&Math.Abs(rig.TwistShare[st]+1)<1e-4f,"upper-arm helpers counter the arm's roll, fully at the shoulder");
            Check(rig.TwistJoint[lt]==CapturedRig.LHand&&Math.Abs(rig.TwistShare[lt]-.14f/.23f)<1e-3f,"forearm helpers take the hand's roll by their place along the forearm");
            Check(rig.TwistJoint[ct]==CapturedRig.LFoot&&rig.TwistShare[ct]>0&&rig.TwistJoint[tt]==CapturedRig.LHip&&rig.TwistShare[tt]<0,"calf helpers take the foot's roll, thigh helpers counter the hip's");
            Check(rig.Joint[rig.Find("index_01_l")]<0&&rig.TwistJoint[rig.Find("index_01_l")]<0&&rig.Joint[rig.Find("ik_hand_gun")]<0,"fingers and helpers ride their parents");
            // The eased wrist: rest and small bends unchanged, a pure roll kept exactly, deep bends eased past the knee.
            Check(Near(NativeVisualRig.Wrist(Quaternion.Identity),Quaternion.Identity,1e-7f),"the wrist at rest is unchanged");
            var roll=CapturedRig.Rot(CapturedRig.Y,1.3f);Check(Near(NativeVisualRig.Wrist(roll),roll,1e-6f),"a pure wrist roll is kept");
            var small=CapturedRig.Rot(CapturedRig.X,.3f)*roll;Check(Near(NativeVisualRig.Wrist(small),small,1e-6f),"a bend under the knee is kept");
            var deep=CapturedRig.Rot(CapturedRig.X,107*(float)Math.PI/180);float eased=Angle(-CapturedRig.Y,Vector3.Transform(-CapturedRig.Y,NativeVisualRig.Wrist(deep)));
            Check(Math.Abs(eased-(25+.25f*82)*(float)Math.PI/180)<1e-3f,"a 107 degree rifle-hold bend eases to 45.5 degrees");
            Fingers(rig);
            bool threw=false;try{var bad=NativeVisualRig.Mannequin(new[]{"pelvis","root"},new[]{1,-1},new Vector3[2],new[]{Quaternion.Identity,Quaternion.Identity});new NativeVisualRig.Rig(bad);}catch(ArgumentException){threw=true;}
            Check(threw,"a bone before its parent is refused");
        }

        // Finger grips: each chain curls the way its rest curls, by the relaxed or closed angles, joint by joint.
        private static void Fingers(NativeVisualRig.Rig rig)
        {
            foreach(string side in new[]{"_l","_r"})
            {
                int hand=rig.Find("hand"+side),i0=rig.Find("index_01"+side),i1=rig.Find("index_02"+side),i2=rig.Find("index_03"+side),t0=rig.Find("thumb_01"+side),t1=rig.Find("thumb_02"+side),t2=rig.Find("thumb_03"+side);
                Check(rig.Finger[i0]==1&&rig.Finger[i1]==2&&rig.Finger[i2]==3&&rig.Finger[t0]==11&&rig.Finger[t2]==13&&rig.Side[i0]==(side=="_l"?0:1),"finger roles"+side);
                Check(Math.Abs(Vector3.Dot(rig.FingerAxis[i0],rig.FingerAxis[i2])-1)<1e-5f&&rig.FingerAxis[i0].LengthSquared()>.99f,"one curl axis per finger chain"+side);
                var state=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();pose.World();
                float RestAngle(int a,int b,int c)=>Angle(rig.AlignedPosition[b]-rig.AlignedPosition[a],rig.AlignedPosition[c]-rig.AlignedPosition[b]);
                float Bent(int a,int b,int c)=>Angle(state.Position[b]-state.Position[a],state.Position[c]-state.Position[b]);
                float rest0=RestAngle(hand,i0,i1),rest1=RestAngle(i0,i1,i2),restT=RestAngle(t0,t1,t2);
                float[] grips={0,1,.5f};
                float tipOpen=0;
                foreach(float g in grips)
                {
                    NativeVisualRig.Pose(state,pose,0,false,false,side=="_r"?g:0,side=="_l"?g:0);
                    Check(Math.Abs(Bent(hand,i0,i1)-(rest0+NativeVisualRig.Curl(1,g)))<2e-3f,"index base curls by "+NativeVisualRig.Curl(1,g)+" past its rest"+side);
                    Check(Math.Abs(Bent(i0,i1,i2)-(rest1+NativeVisualRig.Curl(2,g)))<2e-3f,"index middle joint curls"+side);
                    Check(Math.Abs(Bent(t0,t1,t2)-(restT+NativeVisualRig.Curl(12,g)))<2e-3f,"thumb middle joint curls"+side);
                    float tip=Vector3.Distance(state.Position[i2],state.Position[hand]);
                    if(g==0) tipOpen=tip; else if(g==1) Check(tip<tipOpen-.005f,"a closed hand draws the fingertip in"+side);
                }
            }
            Check(NativeVisualRig.Curl(1,0)==NativeVisualRig.Relaxed[0]&&NativeVisualRig.Curl(13,1)==NativeVisualRig.Closed[5]&&NativeVisualRig.Curl(0,1)==0,"the curl table");
            // The closed fist: a handle runs through it across the palm (+z here), a few centimetres out along the hand, toward the thumb.
            foreach(string side in new[]{"_l","_r"})
            {
                int s=side=="_l"?0:1,hand=rig.Find("hand"+side);Vector3 arm=Vector3.Normalize(rig.AlignedPosition[rig.Find("index_01"+side)]-rig.AlignedPosition[hand]);
                Check(rig.HasFist[s]&&Math.Abs(Vector3.Dot(rig.FistAxis[s],rig.FistAhead[s]))<1e-5f&&Math.Abs(rig.FistAxis[s].Length()-1)<1e-5f,"the fist's axis and forward are square"+side);
                Check(Vector3.Dot(rig.FistCenter[s],new Vector3(0,0,1))>.005f&&Vector3.Dot(rig.FistCenter[s],arm)>.03f&&rig.FistCenter[s].Length()<.15f,"the fist's centre lies on the palm side, out along the hand"+side);
                Check(Vector3.Dot(rig.FistAxis[s],rig.AlignedPosition[rig.Find("thumb_01"+side)]-rig.AlignedPosition[rig.Find("index_01"+side)])>0,"the fist's axis points to the thumb's side"+side);
                Check(Angle(rig.FistAhead[s],arm)<35f/57.3f,"the fist's forward runs along the hand"+side);
            }
        }

        private sealed class Stats { public int Poses, Planted, LegShort, Supported, SupportShort, Gripped, Inside, InsideFull, Crossed, GaveWay, Shrugged, Seated, Rode, Butted, Swung, Braced, Cleared, SeatCleared; public float Support, Leg, Grip, GripSum, Across, GiveWay, Shoulder, Bend, Straight, Swing, ClearMost, ClearLeft, SeatClearMost, SeatClearLeft; public readonly System.Collections.Generic.Dictionary<string,int> Short=new System.Collections.Generic.Dictionary<string,int>(); }
        private static Stats last;
        private static float maxWrist;
        private static readonly int[][] Limbs={new[]{CapturedRig.LArm,CapturedRig.LElbow},new[]{CapturedRig.LElbow,CapturedRig.LHand},new[]{CapturedRig.RArm,CapturedRig.RElbow},new[]{CapturedRig.RElbow,CapturedRig.RHand},
            new[]{CapturedRig.LHip,CapturedRig.LKnee},new[]{CapturedRig.LKnee,CapturedRig.LFoot},new[]{CapturedRig.RHip,CapturedRig.RKnee},new[]{CapturedRig.RKnee,CapturedRig.RFoot}};

        // Every captured melee clip, and the gun gait (pistol, rifle, machine gun: idle, aimed, run, sprint, roll) as
        // CapturedAnimation drives it.
        private static int Clips(NativeVisualRig.Rig rig,out float maxSupport,out float maxLeg,out float maxGrip)
        {
            var state=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();var f=new float[CapturedClip.Channels];var stats=new Stats();
            for(int set=0;set<CapturedClips.Sets.Length;set++) foreach(var clip in CapturedClips.Sets[set])
                for(int k=0;k<8;k++)
                {
                    clip.Sample(clip.Duration*k/8,f);CapturedRig.Sample(f,set,pose);
                    Verify(rig,state,pose,clip.Name,true,set==2,stats,MeleeGrasp(set));
                }
            int melee=stats.Poses;
            foreach(var w in new[]{WeaponId.Pistol,WeaponId.Rifle,WeaponId.MachineGun})
                foreach(string scene in new[]{"idle","aim","run","sprint","roll"})
                {
                    var a=new Actor{Id=31,SourceSpec=GunMotion.Spec(w),Weapon=w,Grounded=true,Hp=100,CastRate=1,RollDirection=new V3(0,0,1),Ammo=GunRules.Get(w).Magazine};
                    if(scene=="aim") a.Aiming=true;
                    if(scene=="run"||scene=="sprint"||scene=="roll") a.Velocity=new V3(0,0,scene=="sprint"?8.5f:6.8f);
                    a.Sprinting=scene=="sprint";
                    var motion=new GunMotion();var juggle=new NativeJuggleTrack();Vector3 at=Vector3.Zero;float t0=20,dt=1/60f;
                    for(int i=0;i<150;i++)
                    {
                        float t=t0+i*dt; if(scene=="roll"&&i==30) a.RollAt=t;
                        at+=new Vector3(a.Velocity.x,0,a.Velocity.z)*dt;
                        motion.Rig.GroupPosition=new Vector3(-at.X,.865f,at.Z);motion.Rig.GroupYaw=0;
                        motion.Pose(a,t,pose,false,juggle,out _);motion.Finish(pose,t);pose.World();
                        if(i%6==0) Verify(rig,state,pose,w+" "+scene,!a.Rolling(t),true,stats,GunGrasp(w,pose));
                        if(scene=="aim"&&i>60) Check(NativeVisualRig.HandsTogether(pose)==1,w+" aimed: the game's hands hold together, so the support grip is on");
                    }
                }
            Check(melee>200&&stats.Poses-melee>300,"enough captured and gun poses ("+melee+" + "+(stats.Poses-melee)+")");
            maxSupport=stats.Support;maxLeg=stats.Leg;maxGrip=stats.Grip;last=stats;
            return stats.Poses;
        }

        // Each melee set's hands as the game draws them: sword in the right; daggers in both, reverse grip; the greatsword
        // two-handed; the shield (drawn on the right hand, its clips mirrored) by its grip bar.
        private static NativeVisualRig.Grasp GunGrasp(WeaponId w,CapturedRig.Pose pose)=>new NativeVisualRig.Grasp{RightGrip=1,Support=1,Stock=NativeVisualRig.Stock(w),Right=NativeVisualRig.PistolGrip(w),SupportHold=NativeVisualRig.GunSupport(w,pose)};
        private static NativeVisualRig.Grasp MeleeGrasp(int set)
        {
            var held=Quaternion.Identity;string spec=set==0?"sword":set==1?"daggers":set==2?"greatsword":"shield";
            var g=new NativeVisualRig.Grasp{RightGrip=1,Right=NativeVisualRig.Melee(spec,held)};
            if(set==1){g.LeftGrip=1;g.Left=NativeVisualRig.Melee(spec,held);}
            if(set==2){g.Support=1;g.SupportHold=NativeVisualRig.GreatswordSupport(held);}
            return g;
        }

        private static void Verify(NativeVisualRig.Rig rig,NativeVisualRig.State state,CapturedRig.Pose pose,string name,bool plant,bool twoHanded,Stats stats,NativeVisualRig.Grasp grasp)
        {
            int pelvis=rig.Of[CapturedRig.Pelvis];int[][] limbs=Limbs;stats.Poses++;
            {
                {
                    state.Forget(); // each captured pose is checked on its own (no last frame)
                    NativeVisualRig.Pose(state,pose,0,false,false);
                    Hinges(rig,state,name);
                    Vector3[] copied={state.Position[rig.Of[CapturedRig.LHand]],state.Position[rig.Of[CapturedRig.RHand]]};
                    bool[] fkCleared={state.LeftClear>0,state.RightClear>0};
                    Clearance(rig,state,name,stats);
                    Vector3 fkLeftElbow=state.Position[rig.Of[CapturedRig.LElbow]],fkLeftShoulder=state.Position[rig.Of[CapturedRig.LArm]];
                    Vector3[] fkKnee={state.Position[rig.Of[CapturedRig.LKnee]],state.Position[rig.Of[CapturedRig.RKnee]]};
                    Inside(rig,state,pose,name,stats);
                    foreach(var l in limbs)
                    {
                        if(l[0]>=CapturedRig.LArm&&l[0]<=CapturedRig.LHand&&(state.LeftInside>0||state.LeftClear>0)||l[0]>=CapturedRig.RArm&&l[0]<=CapturedRig.RHand&&(state.RightInside>0||state.RightClear>0)) continue; // checked by Inside, Clearance
                        Vector3 game=pose.WorldPosition[l[1]]-pose.WorldPosition[l[0]],vis=state.Position[rig.Of[l[1]]]-state.Position[rig.Of[l[0]]];
                        int clav=l[0]<=CapturedRig.LHand?CapturedRig.LClav:CapturedRig.RClav;
                        if(l[0]>=CapturedRig.LArm&&l[0]<=CapturedRig.RHand) // arms: the game's direction, turned by the hanging spread at the shoulder
                            game=Vector3.Transform(game,pose.WorldRotation[clav]*NativeVisualRig.Spread(pose,clav==CapturedRig.LClav)*Quaternion.Inverse(pose.WorldRotation[clav]));
                        Check(Angle(game,vis)<2e-3f,name+" "+CapturedRig.Bones[l[0]].Name+" follows the game's bone direction");
                    }
                    foreach(int j in new[]{CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LFoot,CapturedRig.RFoot})
                        Check(Near(state.Turn[rig.Of[j]],pose.WorldRotation[j],2e-4f),name+" "+CapturedRig.Bones[j].Name+" carries the game joint's turn");
                    foreach(int h in new[]{CapturedRig.LHand,CapturedRig.RHand})
                    {
                        if((h==CapturedRig.LHand?state.LeftClear:state.RightClear)>0) continue; // its arm reached out of the body (Clearance)
                        // The visual wrist: the game's roll in full, the bend eased past the knee (NativeVisualRig.Wrist).
                        Quaternion want=state.Turn[rig.Of[h-1]]*NativeVisualRig.Wrist(pose.Rotation[h]); // on the visual forearm (which carries the spread)
                        Check(Near(state.Turn[rig.Of[h]],want,2e-4f),name+" "+CapturedRig.Bones[h].Name+" takes the eased wrist");
                        float gameBend=Angle(Vector3.Transform(-CapturedRig.Y,pose.WorldRotation[h-1]),Vector3.Transform(-CapturedRig.Y,pose.WorldRotation[h]));
                        float visBend=Angle(Vector3.Transform(-CapturedRig.Y,state.Turn[rig.Of[h-1]]),Vector3.Transform(-CapturedRig.Y,state.Turn[rig.Of[h]]));
                        Check(Math.Abs(visBend-NativeVisualRig.WristBend(gameBend))<1e-2f,name+" "+CapturedRig.Bones[h].Name+" bends "+(visBend*57.3f).ToString("0")+" for the game's "+(gameBend*57.3f).ToString("0"));
                        Check(Math.Abs(NativeVisualRig.Twist(Quaternion.Inverse(state.Turn[rig.Of[h-1]])*state.Turn[rig.Of[h]])-NativeVisualRig.Twist(pose.Rotation[h]))<2e-3f,name+" "+CapturedRig.Bones[h].Name+" keeps the game wrist's roll");
                        maxWrist=Math.Max(maxWrist,visBend);
                    }
                    Check(Near(state.Position[pelvis]-rig.PelvisRest,(pose.WorldPosition[CapturedRig.Pelvis]-rig.GamePelvisRest)*rig.LegRatio,1e-4f),name+" the root moves by the leg ratio");
                    TwistShares(rig,state,pose,name);
                    for(int v=0;v<rig.Count;v++) if(rig.TwistJoint[v]>=0&&rig.TwistJoint[v]>=CapturedRig.LHip)
                    {
                        Quaternion rel=Quaternion.Inverse(state.Turn[rig.Parent[v]])*state.Turn[v];
                        Check(Near(rel,CapturedRig.Rot(CapturedRig.Y,rig.TwistShare[v]*NativeVisualRig.Twist(pose.Rotation[rig.TwistJoint[v]])),2e-4f),name+" "+rig.Names[v]+" takes its share of the roll");
                    }
                    // Planted feet and the support grip.
                    state.Forget();
                    Vector3 rightBefore=state.Position[rig.Of[CapturedRig.RHand]],leftShoulder=state.Position[rig.Of[CapturedRig.LArm]];
                    Vector3 gripBefore=rightBefore+pose.WorldPosition[CapturedRig.LHand]-pose.WorldPosition[CapturedRig.RHand];
                    NativeVisualRig.Pose(state,pose,1,plant,false,1,0);
                    {
                        float most=(rig.ArmUpper+rig.ArmLower)*NativeVisualRig.SupportReach,far=Vector3.Distance(gripBefore,leftShoulder),moved=Vector3.Distance(state.Position[rig.Of[CapturedRig.RHand]],rightBefore);
                        bool over=far>most;
                        Check(state.ShoulderTurn>=0&&state.ShoulderTurn<=NativeVisualRig.ShoulderReach*state.SupportWeight+1e-5f,name+" the support shoulder reaches forward at most "+(NativeVisualRig.ShoulderReach*57.3f).ToString("0")+" degrees");
                        Check(state.GiveWay>=0&&state.GiveWay<=(over?(far-most)*state.SupportWeight:0)+1e-5f,name+" the weapon hand gives way at most by the support grip's excess reach");
                        if(state.GiveWay>0) Check(Math.Abs(state.ShoulderTurn-NativeVisualRig.ShoulderReach*state.SupportWeight)<1e-4f,name+" the weapon hand gives way only once the shoulder has reached as far as it may");
                        if(over){ if(state.ShoulderTurn>0){stats.Shrugged++;stats.Shoulder=Math.Max(stats.Shoulder,state.ShoulderTurn);} if(state.GiveWay>0){stats.GaveWay++;stats.GiveWay=Math.Max(stats.GiveWay,state.GiveWay);}
                            if(state.SupportWeight>=.9999f) Check(state.LeftArmReach<=NativeVisualRig.SupportReach+1e-3f,name+" with the shoulder's reach and any give-way, the support hand reaches its grip");}
                        else Check(state.ShoulderTurn==0&&moved<1e-4f,name+" the shoulder and the weapon hand stay where the copied arm put them");
                    }
                    Check(state.RightGrip==1&&state.LeftGrip>=state.SupportWeight,name+" the weapon hand closes, and the left closes while it supports");
                    foreach(bool left in new[]{true,false})
                    {
                        int hip=left?CapturedRig.LHip:CapturedRig.RHip; float w=left?state.LeftFootWeight:state.RightFootWeight;
                        float above=pose.WorldPosition[hip+2].Y-NativeVisualRig.AnkleRest(hip);
                        Check(!plant?w==0:above>NativeVisualRig.LiftedHeight?w==0:above<=NativeVisualRig.PlantedHeight?w==1:w>0&&w<1,name+" foot weight follows the game ankle's height");
                        if(w==1)
                        {
                            float reach=left?state.LeftLegReach:state.RightLegReach; stats.Leg=Math.Max(stats.Leg,reach); stats.Planted++; if(reach>=.999f) stats.LegShort++;
                            Vector3 g=pose.WorldPosition[hip+2],want=new Vector3(g.X,rig.AlignedPosition[rig.Of[hip+2]].Y+above,g.Z);
                            if(reach<.999f) Check(Near(state.Position[rig.Of[hip+2]],want,1e-3f),name+" a planted foot reaches the game ankle's ground point");
                            Vector3 vh=state.Position[rig.Of[hip]],vk=state.Position[rig.Of[hip+1]],va=state.Position[rig.Of[hip+2]];
                            Vector3 gh=pose.WorldPosition[hip],gk=pose.WorldPosition[hip+1],ga=pose.WorldPosition[hip+2];
                            Vector3 gb=gk-(gh+ga)*.5f,vb=vk-(vh+va)*.5f;
                            Vector3 fb=fkKnee[left?0:1]-(vh+va)*.5f,line=Vector3.Normalize(va-vh);fb-=line*Vector3.Dot(fb,line);
                            if(fb.Length()>.02f) Check(Vector3.Dot(fb,vb)>0,name+" the knee bends to its unadjusted side");
                        }
                    }
                    if(twoHanded&&state.SupportWeight>0){stats.Support=Math.Max(stats.Support,state.LeftArmReach); stats.Supported++;
                    {Vector3 ve=state.Position[rig.Of[CapturedRig.LElbow]],vs=state.Position[rig.Of[CapturedRig.LArm]],vt=state.Position[rig.Of[CapturedRig.LHand]],ln=Vector3.Normalize(vt-vs);
                     Vector3 fe=fkLeftElbow-(vs+vt)*.5f;fe-=ln*Vector3.Dot(fe,ln);Vector3 ie=ve-(vs+vt)*.5f;
                     if(fe.Length()>.02f) Check(Vector3.Dot(fe,ie)>0,name+" the support elbow bends to its unadjusted side");} if(state.LeftArmReach>=.999f){stats.SupportShort++;string key=name.StartsWith("Pistol")||name.StartsWith("Rifle")||name.StartsWith("MachineGun")?name:"greatsword";stats.Short[key]=(stats.Short.TryGetValue(key,out int c)?c:0)+1;}}
                    Vector3 hand=state.Position[rig.Of[CapturedRig.LHand]],right=state.Position[rig.Of[CapturedRig.RHand]];
                    if(state.SupportWeight>=.9999f&&state.LeftArmReach<.999f) Check(Near(hand,right+pose.WorldPosition[CapturedRig.LHand]-pose.WorldPosition[CapturedRig.RHand],1e-3f),name+" the support hand keeps the game's wrist offset");
                    Check(Near(state.World[rig.Of[CapturedRig.LHand]],state.Turn[rig.Of[CapturedRig.LHand]]*rig.AlignedWorld[rig.Of[CapturedRig.LHand]],2e-4f),name+" the support hand keeps the game wrist turn");
                    NativeVisualRig.Grip(state,pose,out var gp,out var gq);
                    var socket=NativeKillBoxes.RightHandSocket;Vector3 offset=Vector3.Transform(new Vector3(socket[0],socket[1],socket[2]),pose.WorldRotation[CapturedRig.RHand]);
                    Check(Near(gp,right+offset,1e-5f)&&Near(gq,pose.WorldRotation[CapturedRig.RHand],1e-6f),name+" the grip is the game socket on the visual wrist");
                    float gd=Vector3.Distance(gp,pose.WorldPosition[CapturedRig.RHand]+offset); stats.Grip=Math.Max(stats.Grip,gd); stats.GripSum+=gd; stats.Gripped++;
                    // The locals rebuild the world pose.
                    Quaternion[] w2=new Quaternion[rig.Count];Vector3[] p2=new Vector3[rig.Count];
                    for(int v=0;v<rig.Count;v++){int p=rig.Parent[v];w2[v]=p<0?state.Local[v]:w2[p]*state.Local[v];p2[v]=p<0?state.LocalPosition[v]:p2[p]+Vector3.Transform(state.LocalPosition[v],w2[p]);}
                    for(int v=0;v<rig.Count;v++) Check(Near(w2[v],state.World[v],2e-4f)&&Near(p2[v],state.Position[v],2e-4f),name+" "+rig.Names[v]+" locals rebuild the pose");
                    // Seated fists on the weapon's handles (solved afresh: no last frame across unrelated poses).
                    state.Forget();
                    NativeVisualRig.Pose(state,pose,grasp,plant,false);
                    Seats(rig,state,pose,grasp,name,stats,copied,fkCleared);
                    Hinges(rig,state,name);
                    TwistShares(rig,state,pose,name);
                    for(int v=0;v<rig.Count;v++){int p=rig.Parent[v];w2[v]=p<0?state.Local[v]:w2[p]*state.Local[v];p2[v]=p<0?state.LocalPosition[v]:p2[p]+Vector3.Transform(state.LocalPosition[v],w2[p]);}
                    for(int v=0;v<rig.Count;v++) Check(Near(w2[v],state.World[v],2e-4f)&&Near(p2[v],state.Position[v],2e-4f),name+" "+rig.Names[v]+" locals rebuild the seated pose");
                }
            }

        }

        // Fists keep out of the visual head and torso (NativeVisualRig.Clear): each fist's centre stands FistHeadClear outside
        // the head ellipsoid and FistClear outside the torso cylinder as the rig sets them (within 1 cm, where its arm is not at
        // full stretch).
        private static void Clearance(NativeVisualRig.Rig rig,NativeVisualRig.State state,string name,Stats stats)
        {
            foreach(bool left in new[]{true,false})
            {
                int side=left?0:1,vh=rig.Of[left?CapturedRig.LHand:CapturedRig.RHand],va=rig.Of[left?CapturedRig.LArm:CapturedRig.RArm];
                Vector3 fist=state.Position[vh]+Vector3.Transform(rig.FistCenter[side],Eff(rig,state,vh));
                float deep=Depth(rig,state,fist,NativeVisualRig.FistHeadClear,NativeVisualRig.FistClear),moved=left?state.LeftClear:state.RightClear;
                float reach=Vector3.Distance(state.Position[vh],state.Position[va])/(rig.ArmUpper+rig.ArmLower);
                if(moved>0){stats.Cleared++;stats.ClearMost=Math.Max(stats.ClearMost,moved);stats.ClearLeft=Math.Max(stats.ClearLeft,deep);}
                if(reach<.999f) Check(deep<.01f,name+(left?" left":" right")+" fist keeps out of the head and torso (inside by "+deep.ToString("0.000")+" m)");
            }
        }
        // The seated fists and their weapons' lines (Hold: Tail to Tip, CrossA to CrossB) after the whole grasp: how far any is
        // still inside (a riding fist turns on its handle after the clearance, a support fist follows the weapon, a shouldered
        // stock rests on the chest), reported, not required.
        private static void SeatClearance(NativeVisualRig.Rig rig,NativeVisualRig.State state,CapturedRig.Pose pose,in NativeVisualRig.Grasp g,string name,Stats stats)
        {
            float moved=Math.Max(state.LeftClear,state.RightClear),deep=0;
            for(int side=0;side<2;side++)
            {
                bool left=side==0; int vh=rig.Of[left?CapturedRig.LHand:CapturedRig.RHand];
                deep=Math.Max(deep,Depth(rig,state,state.Position[vh]+Vector3.Transform(rig.FistCenter[side],Eff(rig,state,vh)),NativeVisualRig.FistHeadClear,NativeVisualRig.FistClear));
                var hold=left?g.Left:g.Right; bool seated=left?state.LeftSeated:state.RightSeated;
                if(!seated||!hold.Spans) continue;
                Vector3 anchor=left?state.LeftAnchor:state.RightAnchor; Quaternion w=pose.WorldRotation[left?CapturedRig.LHand:CapturedRig.RHand];
                for(int i=0;i<=20;i++)
                {
                    deep=Math.Max(deep,Depth(rig,state,anchor+Vector3.Transform(Vector3.Lerp(hold.Tail,hold.Tip,i/20f),w),hold.Half,hold.Half));
                    if(hold.Crosses) deep=Math.Max(deep,Depth(rig,state,anchor+Vector3.Transform(Vector3.Lerp(hold.CrossA,hold.CrossB,i/20f),w),hold.CrossHalf,hold.CrossHalf));
                }
            }
            if(moved>0){stats.SeatCleared++;stats.SeatClearMost=Math.Max(stats.SeatClearMost,moved);}
            stats.SeatClearLeft=Math.Max(stats.SeatClearLeft,deep);
        }
        // How deep a point is inside the head ellipsoid grown by `margin` or the torso cylinder (pelvis to shoulders) grown by
        // `torso` (negative: the head only), metres along its scaled radius.
        private static float Depth(NativeVisualRig.Rig rig,NativeVisualRig.State state,Vector3 p,float margin,float torso)
        {
            float deep=0;int head=rig.Of[CapturedRig.Head],pelvis=rig.Of[CapturedRig.Pelvis],neck=rig.Of[CapturedRig.Neck],chest=rig.Of[CapturedRig.Chest];
            Quaternion hq=Eff(rig,state,head);Vector3 l=Vector3.Transform(p-state.Position[head]-Vector3.Transform(rig.HeadCenter,hq),Quaternion.Inverse(hq));
            float k=(l/(rig.HeadRadii+new Vector3(margin))).Length();if(k<1) deep=Math.Max(deep,l.Length()*(1/Math.Max(k,1e-4f)-1));
            Vector3 low=state.Position[pelvis],line=state.Position[neck]-low,mid=(state.Position[rig.Of[CapturedRig.LArm]]+state.Position[rig.Of[CapturedRig.RArm]])*.5f;
            float t=Vector3.Dot(p-low,line)/line.LengthSquared(),top=Vector3.Dot(mid-low,line)/line.LengthSquared();
            if(torso>=0&&t>=0&&t<=top)
            {
                Quaternion cq=Eff(rig,state,chest);Vector3 d=p-low-line*t;float rx=Vector3.Dot(d,Vector3.Transform(CapturedRig.X,cq)),rz=Vector3.Dot(d,Vector3.Transform(CapturedRig.Z,cq));
                float ux=rx/(rig.TorsoHalfWidth+torso),uz=rz/(rig.TorsoHalfDepth+torso),kt=(float)Math.Sqrt(ux*ux+uz*uz);
                if(kt<1) deep=Math.Max(deep,(float)Math.Sqrt(rx*rx+rz*rz)*(1/Math.Max(kt,1e-4f)-1));
            }
            return deep;
        }

        // Both arms are hinged: the upper arm points down its bone with its fold side (+z, game-aligned) toward the forearm, and
        // the forearm points down its own bone sharing the upper arm's hinge, so no elbow crease turns inside out.
        private static void Hinges(NativeVisualRig.Rig rig,NativeVisualRig.State state,string name)
        {
            foreach(bool left in new[]{true,false})
            {
                int ga=left?CapturedRig.LArm:CapturedRig.RArm,va=rig.Of[ga],ve=rig.Of[ga+1],vh=rig.Of[ga+2];
                Quaternion upper=Eff(rig,state,va),fore=Eff(rig,state,ve);
                Vector3 u=state.Position[ve]-state.Position[va],f=state.Position[vh]-state.Position[ve],fold=f-Vector3.Normalize(u)*Vector3.Dot(f,Vector3.Normalize(u));
                string arm=name+(left?" left":" right");
                Check(Angle(Vector3.Transform(-CapturedRig.Y,upper),u)<.3f/57.3f&&Angle(Vector3.Transform(-CapturedRig.Y,fore),f)<.3f/57.3f,arm+" arm bones lie along the arm");
                Check(Angle(Vector3.Transform(CapturedRig.X,upper),Vector3.Transform(CapturedRig.X,fore))<.5f/57.3f,arm+" elbow is a hinge (both bones share its axis)");
                if(fold.Length()>.03f) Check(Angle(Vector3.Transform(CapturedRig.Z,upper),fold)<1f/57.3f,arm+" elbow crease faces the fold");
            }
        }

        // Hands through transitions: frame by frame at 60 Hz, each visual hand turns no more than three times the game's own
        // hand plus 20 degrees and moves no more than three times its move plus 5 cm (6 cm for an elbow), so nothing snaps
        // (the snaps this caught flipped a hand 37-177 degrees in a frame): every melee set from idle crossfading into each cut, through
        // it and back to idle, and into a block; every gun through idle, aim, firing, a run and back to aim.
        private static void Transitions(NativeVisualRig.Rig rig,out int frames,out float worstTurn,out float worstMove)
        {
            var state=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();var fa=new float[CapturedClip.Channels];var fb=new float[CapturedClip.Channels];
            var lastVis=new Quaternion[2];var lastGame=new Quaternion[2];var lastVisAt=new Vector3[4];var lastGameAt=new Vector3[4];bool first=true;
            int steps=0;float turnWorst=0,moveWorst=0,headWorst=0;
            void Step(string name,NativeVisualRig.Grasp g,bool plant)
            {
                NativeVisualRig.Pose(state,pose,g,plant,false);steps++;
                for(int side=0;side<2;side++)
                {
                    int gh=side==0?CapturedRig.LHand:CapturedRig.RHand,vh=rig.Of[gh],ve=rig.Of[gh-1];
                    Quaternion vis=state.World[vh],game=pose.WorldRotation[gh];
                    if(!first)
                    {
                        float dv=2*(float)Math.Acos(Math.Min(1,Math.Abs(Quaternion.Dot(vis,lastVis[side])))),dg=2*(float)Math.Acos(Math.Min(1,Math.Abs(Quaternion.Dot(game,lastGame[side]))));
                        float mv=Vector3.Distance(state.Position[vh],lastVisAt[side]),mg=Vector3.Distance(pose.WorldPosition[gh],lastGameAt[side]);
                        float me=Vector3.Distance(state.Position[ve],lastVisAt[2+side]),mge=Vector3.Distance(pose.WorldPosition[gh-1],lastGameAt[2+side]);
                        turnWorst=Math.Max(turnWorst,dv-3*dg);moveWorst=Math.Max(moveWorst,Math.Max(mv-3*mg,me-3*mge));
                        Check(dv<=3*dg+20f/57.3f,name+(side==0?" left":" right")+" hand turns smoothly ("+(dv*57.3f).ToString("0")+" against the game's "+(dg*57.3f).ToString("0")+" degrees this frame)");
                        Check(mv<=3*mg+.05f&&me<=3*mge+.06f,name+(side==0?" left":" right")+" hand and elbow move smoothly (hand "+mv.ToString("0.000")+" vs "+mg.ToString("0.000")+", elbow "+me.ToString("0.000")+" vs "+mge.ToString("0.000")+" m)");
                    }
                    // No fist centre inside the head, however fast the hands sweep past it (a greatsword's raise once crossed the face).
                    float inHead=Depth(rig,state,state.Position[vh]+Vector3.Transform(rig.FistCenter[side],Eff(rig,state,vh)),0,-1);
                    headWorst=Math.Max(headWorst,inHead);
                    Check(inHead==0,name+(side==0?" left":" right")+" fist stays out of the head (inside by "+inHead.ToString("0.000")+" m)");
                    lastVis[side]=vis;lastGame[side]=game;lastVisAt[side]=state.Position[vh];lastGameAt[side]=pose.WorldPosition[gh];lastVisAt[2+side]=state.Position[ve];lastGameAt[2+side]=pose.WorldPosition[gh-1];
                }
                first=false;
            }
            const float dt=1/60f;
            for(int set=0;set<CapturedClips.Sets.Length;set++)
            {
                var clips=CapturedClips.Sets[set];CapturedClip Find(string n){foreach(var c in clips)if(c.StateName==n)return c;return null;}
                var idle=Find("idle");if(idle==null)continue;var g=MeleeGrasp(set);
                foreach(string cut in new[]{"cutA","cutB","block"})
                {
                    var attack=Find(cut);if(attack==null)continue;first=true;state.Forget();
                    // idle .3 s, a .15 s crossfade into the attack, the attack through, a .2 s crossfade back, idle .3 s
                    float fadeIn=.15f,fadeOut=.2f,total=.3f+attack.Duration+.3f;
                    for(float t=0;t<=total;t+=dt)
                    {
                        float ta=t-.3f;
                        idle.Sample(t%Math.Max(idle.Duration,1e-3f),fa);
                        attack.Sample(CapturedClip.Clamp(ta,0,attack.Duration),fb);
                        float w=ta<0?0:ta<fadeIn?ta/fadeIn:ta<attack.Duration?1:1-Math.Min(1,(ta-attack.Duration)/fadeOut);
                        for(int c=0;c<fa.Length;c++) fa[c]+=(fb[c]-fa[c])*w;
                        CapturedRig.Sample(fa,set,pose);
                        Step(CapturedClips.Sets[set][0].Name.Split('/')[0]+" idle-"+cut+" "+t.ToString("0.00"),g,true);
                    }
                }
            }
            foreach(var w in new[]{WeaponId.Pistol,WeaponId.Rifle,WeaponId.MachineGun})
            {
                var a=new Actor{Id=31,SourceSpec=GunMotion.Spec(w),Weapon=w,Grounded=true,Hp=100,CastRate=1,RollDirection=new V3(0,0,1),Ammo=GunRules.Get(w).Magazine};
                var motion=new GunMotion();var juggle=new NativeJuggleTrack();Vector3 at=Vector3.Zero;first=true;state.Forget();
                for(int i=0;i<6*60;i++)
                {
                    float t=20+i*dt,phase=i*dt;
                    a.Aiming=phase>=1&&phase<3||phase>=4.5f;
                    if(phase>=2&&phase<3&&(i%15)==0) a.LastShot=t;
                    a.Velocity=phase>=3&&phase<4.5f?new V3(0,0,6.8f):new V3(0,0,0);
                    at+=new Vector3(a.Velocity.x,0,a.Velocity.z)*dt;
                    motion.Rig.GroupPosition=new Vector3(-at.X,.865f,at.Z);motion.Rig.GroupYaw=0;
                    motion.Pose(a,t,pose,false,juggle,out _);motion.Finish(pose,t);pose.World();
                    Step(w+" "+(phase<1?"idle":phase<2?"aim":phase<3?"fire":phase<4.5f?"run":"aim")+" "+phase.ToString("0.00"),GunGrasp(w,pose),true);
                }
            }
            frames=steps;worstTurn=turnWorst;worstMove=moveWorst;
        }

        // A bone's turn from its aligned rest as it stands.
        private static Quaternion Eff(NativeVisualRig.Rig rig,NativeVisualRig.State state,int v)=>Quaternion.Normalize(state.World[v]*Quaternion.Inverse(rig.AlignedWorld[v]));
        // The arm twist bones follow the final pose: forearm bones their share of the hand's roll on the forearm, upper-arm
        // bones their (counter) share of the upper arm's roll on the shoulder (the clavicle with the hanging spread).
        private static void TwistShares(NativeVisualRig.Rig rig,NativeVisualRig.State state,CapturedRig.Pose pose,string name)
        {
            for(int v=0;v<rig.Count;v++)
            {
                int j=rig.TwistJoint[v]; if(j<0||j>=CapturedRig.LHip) continue;
                Quaternion parent=Eff(rig,state,rig.Parent[v]),roll;
                if(j==CapturedRig.LHand||j==CapturedRig.RHand) roll=Quaternion.Inverse(parent)*Eff(rig,state,rig.Of[j]);
                else roll=Quaternion.Inverse(Eff(rig,state,rig.Of[j-1])*NativeVisualRig.Spread(pose,j==CapturedRig.LArm))*parent;
                Check(Near(Quaternion.Inverse(parent)*Eff(rig,state,v),CapturedRig.Rot(CapturedRig.Y,rig.TwistShare[v]*NativeVisualRig.Twist(roll)),2e-4f),name+" "+rig.Names[v]+" takes its share of the arm's final roll");
            }
        }

        // A seated fist: its centre on the handle's grip point (the weapon drawn at its anchor, the game wrist's turn), its axis
        // along the handle, the hand pointing as nearly along the forearm as the handle leaves (the straightest wrist, where the
        // handle stands 30 degrees or more off the forearm's line; nearer, the game wrist's direction decides), and its
        // fingers carried with it. The support fist closes on the right weapon's support hold.
        private static void Seats(NativeVisualRig.Rig rig,NativeVisualRig.State state,CapturedRig.Pose pose,in NativeVisualRig.Grasp g,string name,Stats stats,Vector3[] copied,bool[] fkCleared)
        {
            // The weapon rides the hand: a held weapon's arm keeps the wrist the animation gave it, unless the gun is shouldered,
            // gave way to the support grip or the weapon had to move out of the body (Clear); a shouldered stock butt sits at the
            // shoulder pocket. A braced weapon stays on the fist the animation gives (its frame there), the fist reaching its handle.
            SeatClearance(rig,state,pose,g,name,stats);
            for(int side=0;side<2;side++)
            {
                bool left=side==0,seated=left?state.LeftSeated:state.RightSeated; if(!seated) continue;
                if((left?g.Left:g.Right).Braced)
                {
                    stats.Braced++;
                    if(!fkCleared[side]&&(left?state.LeftClear:state.RightClear)==0) Check(Math.Abs(Vector3.Distance(left?state.LeftAnchor:state.RightAnchor,copied[side])-rig.FistCenter[side].Length())<1e-3f,name+(left?" left":" right")+" braced weapon stays on the fist the animation gives");
                }
                else if((left?state.SupportWeight==0:state.Shouldered==0&&state.GiveWay==0)&&!fkCleared[side]&&(left?state.LeftClear:state.RightClear)==0)
                {
                    stats.Rode++; Check(Near(state.Position[rig.Of[left?CapturedRig.LHand:CapturedRig.RHand]],copied[side],1e-4f),name+(left?" left":" right")+" weapon rides the hand where the animation put the wrist");
                }
                stats.Swing=Math.Max(stats.Swing,Math.Abs(state.ElbowSwing[side])); if(Math.Abs(state.ElbowSwing[side])>10f/57.3f) stats.Swung++;
            }
            if(state.Shouldered>=.9999f&&state.GiveWay==0)
            {
                stats.Butted++;
                Quaternion w=pose.WorldRotation[CapturedRig.RHand],chest=pose.WorldRotation[CapturedRig.Chest];
                Vector3 butt=state.RightAnchor+Vector3.Transform(new Vector3(0,0,-g.Stock),w);
                Vector3 pocket=state.Position[rig.Of[CapturedRig.RArm]]-Vector3.Transform(-CapturedRig.X,chest)*NativeVisualRig.PocketIn-Vector3.Transform(CapturedRig.Y,chest)*NativeVisualRig.PocketDown+Vector3.Transform(CapturedRig.Z,chest)*(rig.ShoulderFront-NativeVisualRig.PocketPress);
                Check(Near(butt,pocket,1e-4f),name+" the shouldered stock butt sits at the shoulder pocket");
            }
            NativeVisualRig.WeaponFrame(state,pose,out var wp,out var wq);
            Check(Near(wp,state.RightAnchor,1e-6f)&&Near(wq,pose.WorldRotation[CapturedRig.RHand],1e-6f),name+" the weapon frame is the anchor with the game wrist turn");
            Check(state.RightSeated==g.Right.Seated,name+" the right fist seats when its weapon has a handle");
            for(int side=0;side<2;side++)
            {
                bool left=side==0;int ga=left?CapturedRig.LArm:CapturedRig.RArm,va=rig.Of[ga],ve=rig.Of[ga+1],vh=rig.Of[ga+2];
                Vector3 anchor;Quaternion w;NativeVisualRig.Hold hold;bool support=false;
                if(left&&state.SupportWeight>=.9999f&&g.SupportHold.Seated){anchor=state.RightAnchor;w=pose.WorldRotation[CapturedRig.RHand];hold=g.SupportHold;support=true;}
                else if(left?state.LeftSeated:state.RightSeated){anchor=left?state.LeftAnchor:state.RightAnchor;w=pose.WorldRotation[ga+2];hold=left?g.Left:g.Right;}
                else continue;
                stats.Seated++;
                Vector3 axis=Vector3.Transform(hold.FistAxis,w),fistAxis=Vector3.Transform(rig.FistAxis[side],state.Turn[vh]),handle=Vector3.Transform(hold.Axis,w);
                Vector3 fist=state.Position[vh]+Vector3.Transform(rig.FistCenter[side],state.Turn[vh]),grip=anchor+Vector3.Transform(hold.Center,w)-(support?handle*state.SupportSlide:Vector3.Zero);
                if(support) Check(state.SupportSlide>=0&&state.SupportSlide<=hold.Slide+1e-6f,name+" the support fist slides back along its handle at most its Slide");
                float wrist=Vector3.Distance(state.Position[vh],state.Position[va]),shortest=Math.Abs(rig.ArmUpper-rig.ArmLower)+.01f;
                if(wrist>shortest&&(!support||state.LeftArmReach<.999f)) Check(Vector3.Distance(fist,grip)<2e-3f,name+(left?" left":" right")+(support?" support":"")+" fist closes on its handle's grip point (off by "+Vector3.Distance(fist,grip).ToString("0.0000")+")");
                float dot=Vector3.Dot(axis,fistAxis);
                Check(dot>.9999f,name+(left?" left":" right")+" fist's axis lies along the handle (or as the hold lays it across), its index side the way the hold names");
                Vector3 forearm=Vector3.Normalize(state.Position[vh]-state.Position[ve]),ahead=Vector3.Transform(rig.FistAhead[side],state.Turn[vh]);
                Vector3 straight=forearm-fistAxis*Vector3.Dot(forearm,fistAxis);
                bool riding=support||hold.Braced?false:left||state.Shouldered==0&&state.GiveWay==0;
                if(riding)
                {
                    // A riding fist takes the straightest wrist the handle allows.
                    if(straight.Length()>.5f) stats.Straight=Math.Max(stats.Straight,Angle(ahead,straight));
                    if(straight.Length()>.5f) Check(Angle(ahead,straight)<3f/57.3f,name+(left?" left":" right")+" wrist is as straight as the handle allows (off by "+(Angle(ahead,straight)*57.3f).ToString("0.0")+")");
                }
                else if((support||state.Shouldered>=.9999f)&&hold.Ahead.LengthSquared()>0)
                {
                    // A reaching fist points where its weapon sets it (Hold.Ahead), rolled toward the straightest by up to its Spread.
                    Vector3 want=Vector3.Transform(hold.Ahead,w); want-=axis*Vector3.Dot(want,axis);
                    Check(Angle(ahead,want)<hold.Spread+.5f/57.3f,name+(left?" left":" right")+" reaching fist points as its weapon sets it, within its spread");
                }
                stats.Bend=Math.Max(stats.Bend,Angle(ahead,forearm));
                for(int v=vh;v<rig.Count;v++) if(v==vh||(rig.Finger[v]!=0&&rig.Side[v]==side)) Check(Near(state.World[v],state.Turn[v]*rig.AlignedWorld[v],2e-4f),name+" "+rig.Names[v]+" turns with its seated hand");
            }
        }

        // A hand inside the game's shoulder line: the copied arm (the game's directions, turned by the hanging spread, from the
        // visual shoulder) shifted sideways to the game hand's place from the chest's middle, scaled by the shoulder widths;
        // lowered by the head drop; then moved in depth until the arm is as straight as the copied one.
        private static void Inside(NativeVisualRig.Rig rig,NativeVisualRig.State state,CapturedRig.Pose pose,string name,Stats stats)
        {
            foreach(bool left in new[]{true,false})
            {
                int ga=left?CapturedRig.LArm:CapturedRig.RArm,clav=left?CapturedRig.LClav:CapturedRig.RClav;
                Quaternion spread=pose.WorldRotation[clav]*NativeVisualRig.Spread(pose,left)*Quaternion.Inverse(pose.WorldRotation[clav]);
                Vector3 shoulder=state.Position[rig.Of[ga]],hand=state.Position[rig.Of[ga+2]];
                Vector3 elbowCopy=shoulder+Vector3.Normalize(Vector3.Transform(pose.WorldPosition[ga+1]-pose.WorldPosition[ga],spread))*rig.ArmUpper;
                Vector3 handCopy=elbowCopy+Vector3.Normalize(Vector3.Transform(pose.WorldPosition[ga+2]-pose.WorldPosition[ga+1],spread))*rig.ArmLower;
                Vector3 side=Vector3.Transform(left?CapturedRig.X:-CapturedRig.X,pose.WorldRotation[CapturedRig.Chest]);
                Vector3 gm=(pose.WorldPosition[CapturedRig.LClav]+pose.WorldPosition[CapturedRig.RClav])*.5f,vm=(state.Position[rig.Of[CapturedRig.LClav]]+state.Position[rig.Of[CapturedRig.RClav]])*.5f;
                float gs=Vector3.Dot(pose.WorldPosition[ga]-gm,side),gh=Vector3.Dot(pose.WorldPosition[ga+2]-gm,side),vs=Vector3.Dot(shoulder-vm,side);
                float w=gs<.05f?0:CapturedClip.Ease((gs-gh)/NativeVisualRig.InsideBand),got=left?state.LeftInside:state.RightInside;
                Check(Math.Abs(got-w)<1e-6f,name+(left?" left":" right")+" hand's inside weight follows the game hand's place from the shoulder line");
                if(w==0) continue;
                stats.Inside++;
                bool cleared=(left?state.LeftClear:state.RightClear)>0; // then the arm reached on out of the body (Clearance)
                Vector3 copy=handCopy-shoulder,up=Vector3.Transform(CapturedRig.Y,pose.WorldRotation[CapturedRig.Chest]);
                float gameRise=Vector3.Dot(pose.WorldPosition[ga+2]-pose.WorldPosition[ga],up),G=NativeVisualRig.GameHeadRise;
                float drop=Math.Clamp(Math.Min(gameRise,2*G-gameRise),0,G)*(1-rig.HeadRise/G)*(rig.ArmUpper+rig.ArmLower)/NativeVisualRig.GameArm;
                Check(Math.Abs(NativeVisualRig.HeadDrop(rig,gameRise)-drop)<1e-6f,name+" the head drop peaks at the game head's height and is gone by twice it");
                Vector3 moved=copy+side*((gh*vs/gs-Vector3.Dot(handCopy-vm,side))*w)-up*(drop*w);
                Vector3 want=shoulder+Vector3.Normalize(moved)*copy.Length();
                Check(Math.Abs(Vector3.Distance(want,shoulder)-copy.Length())<1e-4f,name+" the reach target keeps the copied arm's extension");
                if(cleared) continue;
                if(Vector3.Distance(want,shoulder)<(rig.ArmUpper+rig.ArmLower)*.999f) Check(Near(hand,want,1e-3f),name+(left?" left":" right")+" hand inside the shoulder line reaches its chest-relative place");
                Quaternion swing=CapturedRig.FromTo(Vector3.Normalize(copy),Vector3.Normalize(want-shoulder));
                Check(Near(state.Position[rig.Of[ga+1]],shoulder+Vector3.Transform(elbowCopy-shoulder,swing),1e-3f),name+(left?" left":" right")+" arm swings at the shoulder by the shortest turn to its hand's place");
                Check(Math.Abs(Angle(state.Position[rig.Of[ga+1]]-shoulder,hand-state.Position[rig.Of[ga+1]])-Angle(elbowCopy-shoulder,handCopy-elbowCopy))<2e-3f,name+(left?" left":" right")+" elbow keeps the game's bend");
                if(w<1) continue;
                stats.InsideFull++;
                float past=-Vector3.Dot(hand-vm,side),gamePast=-gh; // past the middle toward the other side
                if(gamePast>0){stats.Crossed++;stats.Across=Math.Max(stats.Across,past/gamePast);}

            }
        }

        private static void Floor(NativeVisualRig.Rig rig)
        {
            var state=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();
            for(int k=0;k<=40;k++)
            {
                pose.Reset();CapturedRig.Roll(pose,k/40f);
                NativeVisualRig.Pose(state,pose,0,false,true);
                float low=float.MaxValue;for(int v=0;v<rig.Count;v++) low=Math.Min(low,state.Position[v].Y-rig.Contact[v]);
                Check(low>=NativeVisualRig.Floor-1e-4f,"roll "+k+": nothing below the floor");
            }
            pose.Reset();pose.World();NativeVisualRig.Pose(state,pose,0,true,true);
            float rest=float.MaxValue;for(int v=0;v<rig.Count;v++) rest=Math.Min(rest,state.Position[v].Y-rig.Contact[v]);
            Check(Math.Abs(rest-NativeVisualRig.Floor)<1e-4f,"standing, the soles touch the floor exactly");
        }

        private static void Allocation(NativeVisualRig.Rig rig)
        {
            var state=new NativeVisualRig.State(rig);var other=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();var f=new float[CapturedClip.Channels];
            var clip=CapturedClips.Sets[0][0];clip.Sample(.3f,f);CapturedRig.Sample(f,0,pose);
            for(int i=0;i<50;i++){NativeVisualRig.Pose(state,pose,1,true,true);NativeVisualRig.Grip(state,pose,out _,out _);}
            var clock=System.Diagnostics.Stopwatch.StartNew();
            long before=GC.GetAllocatedBytesForCurrentThread();
            var grasp=MeleeGrasp(2);
            for(int i=0;i<1000;i++){if(i%2==0)NativeVisualRig.Pose(state,pose,i%4,i%3!=0,i%5==0,i%4*.25f,i%3*.5f);else{grasp.Support=i%3*.5f;NativeVisualRig.Pose(state,pose,grasp,i%3!=0,i%5==0);}NativeVisualRig.Grip(state,pose,out _,out _);}
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"posing allocates nothing per frame");
            Console.WriteLine("INFO visual rig: "+(clock.Elapsed.TotalMilliseconds*1000/1000).ToString("0")+" microseconds per pose on this machine (half with the greatsword's seated grasp)");
            NativeVisualRig.Pose(state,pose,1,true,false);NativeVisualRig.Pose(other,pose,1,true,false);
            for(int v=0;v<rig.Count;v++) Check(state.Local[v].Equals(other.Local[v])&&state.LocalPosition[v].Equals(other.LocalPosition[v]),"the same pose gives the same bones bit for bit");
        }

        private static void Binding(string root)
        {
            string code=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeVisualRigBinding.cs"));
            string core=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeVisualRig.cs"));
            Check(code.Contains("CapturedRig.Mirror(new N.Vector3(")&&code.Contains("CapturedRig.Mirror(new N.Quaternion(")&&code.Contains("CapturedRig.Mirror(State.Local[v])"),"the binding mirrors into and out of the source frame");
            Check(code.Contains("Bones[v].localRotation=")&&code.Contains("if(v==pelvis)")&&!code.Contains(".position=")&&!code.Contains("localScale"),"the binding writes local turns and the pelvis position only");
            foreach(string pack in new[]{"Bozo","BoZo","Magica","OutfitSystem","BMAC","BSMC"})
                Check(!code.Contains(pack)&&!core.Contains(pack),"no pack types or names in the adapter: "+pack);
        }

        // First person and death on the visual body (owner 2026-10-23: "First person still shows voxel", "Death turns model into voxel").
        private static void ViewAndDeath(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string view=R("Runtime/NativeViewPresentation.cs"),body=R("Runtime/NativeVisualBodyPresentation.cs"),death=R("Runtime/NativeDeathPresentation.cs"),scene=R("Runtime/NativeSceneMotion.cs"),kill=R("Runtime/NativeKillEffectPresentation.cs");
            int restore=view.IndexOf("VisualViewRestore();",StringComparison.Ordinal),pose=view.IndexOf("viewRig.ActorPose(shown,Vector3.zero,time,true);",StringComparison.Ordinal),place=view.IndexOf("VisualViewPose(viewRig,shown,time,ScopeActive);",StringComparison.Ordinal),draw=view.IndexOf("if(!ScopeActive&&!SourceCharacter(viewRig,shown,time,true))",StringComparison.Ordinal),chip=view.IndexOf("ChipSelectView(viewRig,source,time);",StringComparison.Ordinal);
            Check(restore>0&&pose>restore&&chip>pose&&place>chip&&draw>place,"first person: the left hand restored before the view rig poses; the view body posed after the aim, sway and chip-select layer, before the draw");
            Check(body.Contains("foreach(int j in new[]{CapturedRig.LElbow,CapturedRig.RElbow})")&&body.Contains("keep[v]=On(w.boneIndex0,w.weight0)+On(w.boneIndex1,w.weight1)+On(w.boneIndex2,w.weight2)+On(w.boneIndex3,w.weight3)>=.5f;")&&body.Contains("if(armMeshes.TryGetValue(mesh,out var made))return made;"),"the view body keeps the triangles weighted half or more below the elbows, cut once per mesh");
            Check(body.Contains("t.gameObject.layer=10;")&&body.Contains("r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;")&&body.Contains("if(cut==null){r.enabled=false;continue;}"),"on the view model's layer, without shadows, the rest of the body off");
            Check(body.Contains("var turn=hand.rotation*Quaternion.Inverse(rq);")&&body.Contains("root.SetPositionAndRotation(hand.position-turn*(rp*s),turn);"),"its right weapon frame lands on the view rig's weapon hand");
            Check(body.Contains("bool on=!Viewer&&sourceModels!=null&&!ClassicBodies&&!scoped&&view!=null&&a.SourceSpec!=\"siren\"&&VisualBodiesOn;"),"Classic, a scope, the Siren and a missing bake keep the voxel arms");
            Check(body.Contains("if(viewBody!=null&&viewBody.Root!=null&&viewBody.Root.activeSelf&&viewBody.PosedFrame<Time.frameCount-1)viewBody.Root.SetActive(false);"),"the view body hides once first person stops");
            Check(body.Contains("int layer=first?10:gearLayer;var camera=first?ViewCamera:gearCamera;")&&body.Contains("if(body.GearCore!=null&&!first)"),"first person draws the device on the view model, not the core (a portrait's gear on its layer and camera)");
            int made=death.IndexOf("if(pilot)VisualCorpseMake(corpse,death,rig);",StringComparison.Ordinal),dispose=death.IndexOf("if(temporary)rig.Dispose();",StringComparison.Ordinal);
            Check(made>0&&dispose>made,"the corpse's visual body is posed from the rig before a temporary rig goes");
            Check(death.Contains("dressed=VisualCorpseShown(corpse,alpha);")&&death.Contains("if(!dressed)sourceModels.QueueShadowless(model,")&&death.Contains("if(!dressed)\n                    {\n                    if(hat&&!AuditorHalo(")&&death.Contains("corpse.Band>=0&&alpha>.5f&&!dressed&&"),"a dressed corpse draws no voxel body, hat or bands");
            int end=death.IndexOf("EndKillFrame();\n            VisualCorpsesShown();",StringComparison.Ordinal);Check(end>0&&death.Contains("ClearVisualCorpses();"),"undrawn corpse bodies hide each frame and go with their corpses");
            Check(kill.Contains("internal Matrix4x4 PartFrom(int part)=>PartMatrix(part)*inverse[part];")&&body.Contains("var m=undo*corpse.PartFrom(c.Part[v])*c.Bind[v];"),"each bone rides its ragdoll part rigidly (the part's motion, sink, overlay and shrink)");
            int scale=body.IndexOf("c.Bones[0].localScale=Vector3.one*Mathf.Max(.001f,corpse.PartScale);",StringComparison.Ordinal),loop=body.IndexOf("var m=undo*corpse.PartFrom(c.Part[v])*c.Bind[v];",StringComparison.Ordinal);
            Check(scale>0&&loop>scale,"the skeleton takes the parts' scale before the bones are placed");
            {
                string motion=R("Runtime/NativeVisualMotionPresentation.cs");
                Check(motion.Contains("if(covered&&pick.Spin)VisualSpinLegs(m,pick);")&&motion.Contains("turn+=Mathf.DeltaAngle(turn,onTip)*pick.Align*Mathf.Clamp01((flat-.3f)/.4f);")
                    &&motion.Contains("m.BodyHips.RotateAround(m.BodyHips.position,body.Root.transform.up,turn*wgt);")
                    &&motion.Contains("bool pin=!(covered&&(pick.Locomotion||pick.Flight));float goal=pin?deepest:Mathf.Min(deepest,0);")&&motion.Contains("m.BodyHips.position-=origin.up*(applied*wgt);")
                    &&motion.Contains("m.Upper=layered&&up.Raised&&up.Snap?1:Mathf.MoveTowards(m.Upper,layered&&up.Raised?1:0,dt/UpperEase);"),"the Whirlwind's legs as muscle offsets before the pose; the body turned onto the blade's bearing");
            }
            // Every corpse keeps the model's lengths (owner 2026-10-26: "ragdoll effect is currently stretching the player
            // model"; 2026-10-27: "Bodies aren't gibbing, they seem to still stretch"), a split body's halves too: each bone turns
            // with its part and keeps its rest offset; the root and the first bone of a part with no joint to its parent part take
            // their part's place.
            Check(!body.Contains("bool whole=")&&body.Contains("if(c.Placed[v])b.SetPositionAndRotation(m.GetColumn(3),m.rotation);else{b.localPosition=c.Rest[v];b.rotation=m.rotation;}")
                &&body.Contains("c.Parent[v]=u<v?u:-1;")&&body.Contains("int p=c.Parent[v];c.Placed[v]=p<0||c.Part[v]!=c.Part[p]&&(corpse.Severed.Contains(c.Part[v])||!corpse.Jointed(c.Part[v]));")
                &&R("Runtime/NativeDeathPresentation.cs").Contains("internal bool Jointed(int part)=>Parts.TryGetValue(part,out var body)&&body.GetComponent<ConfigurableJoint>()!=null;"),"a ragdoll never stretches the model: rotations with the rest lengths, places only at the root and where the ragdoll has no joint");
            // The menu portraits on the model (owner 2026-10-26: "Player model still shows voxel in customization?").
            string portrait=R("Runtime/NativeVisualPortraitPresentation.cs"),portraitUi=R("Runtime/NativePortraitUI.cs");
            Check(portraitUi.Contains("if(presentation.ModelPortraitFor(look)){presentation.ModelPortraitReduced=prefs.reducedEffects;return presentation.ModelPortrait(key,look,own?LookShown():PortraitOutfit(key),PortraitSkins(look,own),stage.width,stage.height,U.S,out top);}")
                &&portrait.Contains("public bool ModelPortraitFor(in PortraitLook look)=>!modelPortraitFailed&&!ClassicBodies&&VisualBodiesOn&&look.Spec!=\"siren\"")&&R("Runtime/NativeLockerUI.cs").Contains("ring,28,false,true);"),"every pilot portrait draws on the model while it draws (this client's cards in its outfit and skins); the Siren and Classic keep the voxel portraits");
            Check(portrait.Contains("NativeLobbyPoses.Sample(s.Rig,spec,pose,time,hat,ModelPortraitReduced,s.Key,out var weapon,out _,out _);")&&portrait.Contains("var frame=NativePortraitRules.Frame(pose,s.Aspect,s.Top);")&&portrait.Contains("visualBodies[s.Player]=body;")&&portrait.Contains("finally{visualBodies.Remove(s.Player);gearLayer=0;gearCamera=null;}")
                &&portrait.Contains("view.RenderWithShader(modelPortraitMask,\"RenderType\");")&&portrait.Contains("RenderSettings.fog=fog;RenderSettings.sun=sun;RenderSettings.ambientProbe=ambient;"),"a model portrait: the voxel portrait's pose and framing, bound only while it draws, its coverage written, the scene's light restored");
            Check(body.Contains("if(ModelPortraitOf(id,out var portrait))return portrait.Outfit;")&&R("Runtime/NativeWeaponSkinPresentation.cs").Contains("if(ModelPortraitOf(id,out var portrait))return portrait.Skins;"),"a portrait body wears its card's outfit and skin map");
            foreach(string pack in new[]{"Bozo","BoZo","Magica","BMAC","BSMC"})Check(!portrait.Contains(pack),"no pack names in the portrait code: "+pack);
            // The rooftop's pilots on the model (owner 2026-10-27: "Yes to all of the open items"), Classic kept.
            {
                string roof=R("Runtime/NativeRooftopPresentation.cs"),game=R("Runtime/ConversionGame.cs"),skins=R("Runtime/NativeWeaponSkinPresentation.cs");
                Check(roof.Contains("if(siren||!RooftopModel(cast,i,root,group,time,camera,reduced,hatColor))")&&roof.Contains("if(Viewer||ClassicBodies||!VisualBodiesOn)return false;"),"each rooftop pilot draws as the model while it draws; Classic, the Siren and an install without the bake keep the voxel pilots");
                Check(roof.Contains("if(!NativePoseWriter.Write(rig.Bones,s.Pose,\"a rooftop pilot\"))return false;")&&roof.Contains("var body=VisualBodyOf(rig,id,VisualHooded(a));if(body==null)return false;")&&roof.Contains("var held=VisualBodyBind(body,rig,a,time,s.Pose,false,false);")&&roof.Contains("body.Moved=true;body.Seen=true;"),"its own rig takes the voxel pilot's game pose and the body binds to it, shown this frame");
                int seat=roof.IndexOf("if(i==0)\n            {\n                int hips=",StringComparison.Ordinal),bind=roof.IndexOf("var held=VisualBodyBind(body,rig,a,time,s.Pose,false,false);",StringComparison.Ordinal);
                Check(seat>bind&&bind>0,"the seated pilot's hips go onto the chair pose's pelvis after the bind, as the portraits seat theirs");
                int flush=roof.IndexOf("sourceModels.Flush(UsesInstancing);\n            // The visual bodies posed this frame show",StringComparison.Ordinal),shown=roof.IndexOf("VisualBodiesShown();",StringComparison.Ordinal);
                Check(flush>0&&shown>flush&&game.Contains("backdropMap=null;if(presentation!=null)presentation.RooftopHide();}"),"the bodies show while the rooftop draws and hide when the home screen leaves it");
                Check(body.Contains("if(id==ViewBodyId||id==RooftopIdTop)return LocalLook")&&skins.Contains("if(id==RooftopIdTop)return rooftopSkins;")&&game.Contains("Skins=RooftopSkins(bodySkin),Accent=prefs.accentColor"),"the seated pilot wears your outfit and skins; the crowd the default outfit in the Standard skin");
                Check(roof.Contains("cast.OwnWeapon[i]=s.Weapon.Visible&&VisualWeaponDrawn(rig,")&&roof.Contains("ModelHeldShown[i]=b=>s.Weapon.Visible&&b==s.Weapon.Bone&&!OwnWeapon[k]||s.Offhand.Visible&&b==s.Offhand.Bone;"),"the crowd's weapons ride the model's wrists (the bake's own where it has one)");
            }
            Check(body.Contains("if(rig==null||!VisualDeathFor(death))return;")&&body.Contains("if(!visualCorpses.TryGetValue(corpse,out var c)||c.Root==null)return false;"),"a smashed corpse and a swarm wear the model too");
            Check(body.Contains("var overlay=corpse.Smash!=null?corpse.Overlay:Matrix4x4.identity;")&&body.Contains("root.localScale=scale;root.position=(Vector3)overlay.GetColumn(3)+Vector3.Scale(scale,at);"),"a pinned smash flattens the model by its overlay on the root");
            Check(death.Contains("modelLook=VisualCorpseLook(corpse,soul?1:2,")&&death.Contains("if(!soul&&!modelLook&&alpha>.001f)")&&body.Contains("foreach(int id in BodyColours)lookBlock.SetColor(id,colour);"),"Soul Drift and Frostbreak on the model stand in for the voxel looks");
            Check(body.Contains("{VisualSever(c.All,c.Bones,pieceOfBone,ref c.Owned,true);")&&body.Contains("if(caps){var fill=SeverCaps(mesh,of,keptTris);")&&body.Contains("if(keptEdge[k]==1&&everyEdge[k]>=2)")&&body.Contains("cap.SetFloat(\"_Cull\",0);"),"a cut corpse's holes are capped in flesh (the mesh's own open edges are not cuts), both sides, keeping the dissolve");
            Check(R("Runtime/NativeInteractionPresentation.cs").Contains("if((d.model??\"pilot\")==\"pilot\")VisualCorpseMake(corpse,d,temp);"),"a corpse a revive makes first wears its visual body too");
            // The model's dressing: hats on its head, chips and spell charges, hit blood from its hit point, its own parry flash.
            string src=R("Runtime/NativeSourcePresentation.cs");
            Check(src.Contains("if(!first)LivingWounds(rig,a,time);")&&src.Contains("if(dressed&&SpawnShows(CapturedRig.Chest))BodyDressing(rig,a,time,first,layer,camera);")&&src.Contains("{var hb=VisualHatBone(rig,a.Hat);")&&src.Contains("if(visual&&!first)VisualHatHair(rig,headShown&&a.SourceSpec!=\"siren\"?a.Hat:null);")&&src.Contains("VisualParryFlash(rig,a,"),"a model pilot draws its hat, chips, spell charge, hit blood and parry flash");
            Check(R("Runtime/NativeWoundPresentation.cs").Contains("var bone=VisualJointFrame(rig,a.bone);")&&R("Runtime/NativeBodyChipPresentation.cs").Contains("VisualJointFrame(rig,c.Bone)")&&body.Contains("if(body.JointFrom==null)VisualJointsOf(body,rig);"),"wounds and chips ride the model's joints");
            string holo=R("Runtime/NativeReviveHoloPresentation.cs"),inter=R("Runtime/NativeInteractionPresentation.cs");
            Check(holo.Contains("bool model=VisualReviveBody(1L<<62|(uint)r.Seed,rig,r.Death,h.Bone,h.Grow,alpha);")&&holo.Contains("if(holo&&r.Place&&!model)")&&holo.Contains("if(rise&&holo&&third&&!VisualBodyPosed(rig))"),"a revive's kept body is the model (its voxel hologram passes off), lifted, its missing parts grown back");
            Check(inter.Contains("{StandFromBody(r,rig,time-r.At);VisualStandPose(rig,a,time);}")&&inter.Contains("VisualRevivesShown();")&&body.Contains("if(grow!=null)for(int v=0;v<s.Bones.Length;v++){if(!s.Top[v])continue;"),"the revived pilot's model stands up from its kept body");
            Check(body.Contains("if(corpse.IsCrushed(j))return -1;")&&body.Contains("if(corpse.Severed.Contains(j)||corpse.Parts.ContainsKey(j)&&!corpse.Jointed(j))return j==CapturedRig.Pelvis?0:j+1;")&&body.Contains("piece[v]=VisualCorpsePiece(corpse,whole.Part[v]);")&&body.Contains("VisualCorpseSever(whole,piece);"),
                "a cut or crushed corpse wears the model severed at its cut joints, the parts a crushed part held flying as their own pieces (the crushed part's left to its fragments)");
            Check(body.Contains("piece[v]=c.Bones[v]!=null&&fx.Request.HalfOf(joint[v],CutNum(c.Bones[v].position))==h?VisualCorpsePiece(half,c.Part[v]):-1;")&&death.Contains("recolored=corpse.Twin!=null&&VisualCorpseShown(corpse,alpha)&VisualCorpseShown(corpse.Twin,alpha)||BisectDraw("),"each half of a split body (a blade's killing blow, a saw) wears the model's parts on its side of the cut, its loose pieces their own meshes");
            Check(scene.Contains("bool dressed=layer==0&&only==null&&VisualSceneShown(death,Bone,Visible,alpha);")&&scene.Contains("if(!dressed)sourceModels.Queue((death.model??\"pilot\")==\"pilot\"")&&scene.Contains("if(!dressed&&!string.IsNullOrEmpty(death.actor?.Hat)"),"a death scene's victim plays in the visual body (not on a cinematic's own layer)");
            Check(body.Contains("for(int j=0;j<22;j++)s.Now[j]=bone(j)*s.Inverse[j];")&&body.Contains("for(int j=0;j<22;j++)s.Inverse[j]=rig.Bones[j].worldToLocalMatrix;"),"each scene bone follows its joint's motion since death");
            // The draft's pilot on the model: the stage's pose bound to it (its hips on the pose's pelvis), the voxel body and hat
            // only off the model, the palm, the chip and the plant moved from the stage's wrists onto the model's.
            string draft=R("Runtime/NativeDraftPresentation.cs");
            Check(draft.Contains("bool asModel=!siren&&VisualDraftPose(draftRig,seat,draftPose,(float)time,camera);VisualBodiesShown();")&&draft.Contains("if(!asModel){sourceModels.Queue(siren?\"siren\":BodyModel(bodySkin,BodyCloseTier)")&&body.Contains("pose.World();var held=VisualBodyBind(body,rig,a,time,pose,false,false);")&&body.Contains("var shift=rig.Bones[CapturedRig.Pelvis].position-body.Binding.Bones[hips].position;"),"the draft's pilot wears the model, its hips on the stage pose's pelvis (the Siren and Classic keep the voxel body)");
            Check(draft.Contains("Matrix4x4 holdTo=asModel?draftRig.Bones[hand].localToWorldMatrix*holdFrom.inverse:Matrix4x4.identity,plantTo=asModel?draftRig.Bones[other].localToWorldMatrix*plantFrom.inverse:Matrix4x4.identity;")&&draft.Contains("var palm=holdTo.MultiplyPoint3x4(Stage(stage.Palm));")&&draft.Contains("at=Vector3.Lerp(holdTo.MultiplyPoint3x4(at),plantTo.MultiplyPoint3x4(at),ride);")&&draft.Contains("var at=siren?palm:plantTo.MultiplyPoint3x4(Stage(plant.At));")&&draft.Contains("catchAt=camera.WorldToScreenPoint(holdTo.MultiplyPoint3x4(Stage(stage.CatchPalm)));"),"the draft's catch, chip and plant ride the model's wrists");
            // The Cyber Lord's hardlight pieces (aura or gauntlets) ride the model's joints; the hands' joint frames are taken after
            // the rig's hands go onto the model's wrists, so the gauntlets close round its hands.
            Check(R("Runtime/NativeSourcePresentation.cs").Contains("if(SpawnShows(CapturedRig.RHand))CyberLordBody(rig,a,time,first,layer,camera);")&&R("Runtime/NativeCyberLordPresentation.cs").Contains("QueueCyberPiece(cyberPieces[i],first?rig.Bones[cyberPieces[i].Bone].localToWorldMatrix:VisualJointFrame(rig,cyberPieces[i].Bone),"),"the Cyber Lord's pieces draw on the model's joints");
            int handsOn=body.IndexOf("bone.SetPositionAndRotation(origin.TransformPoint(side==0?lp:rp),origin.rotation*(side==0?lq:rq));",StringComparison.Ordinal),joints=body.IndexOf("if(body.JointFrom==null)VisualJointsOf(body,rig);",StringComparison.Ordinal);
            Check(handsOn>0&&joints>handsOn,"the joint frames are taken after the hands go onto the model's wrists");
        }
        // The accent glow (owner 2026-10-23: "lights still show where old player model limbs are. I actually want to make lights
        // accentuate the blue colors on the clothes for new model"): the shader's emission over the accent channels, written per
        // clothing renderer with the armour pulse; NativeCombatGlow skips its joint halos (the game rig's joints) on a visual body and
        // spills from in front of the chest core.
        private static void AccentGlow(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string toon=R("Resources/NativeVisualBodyToon.shader"),body=R("Runtime/NativeVisualBodyPresentation.cs"),glow=R("Runtime/NativeCombatGlow.cs"),arena=R("Runtime/ArenaPresentation.cs");
            Check(toon.Contains("_AccentGlow(\"")&&toon.Contains("float3 VisualAccentPart(float3 w,float3 layer){return lerp(lerp(_Color_3.rgb*w.z*layer.x,_Color_6.rgb*w.z,layer.y),_Color_9.rgb*w.z,layer.z);}"),"the shader's accent share mirrors VisualColours' third colours");
            Check(toon.Contains("if(_UseCustomTexture>.5)o.Emission+=SpawnTinted(VisualAccentPart(map.rgb,i.color.rgb),i.worldPos.y)*_AccentGlow;"),"the accent glows as emission, tinted by the spawn-in");
            Check(body.Contains("NativeGlowRules.ArmorPulse(time,p.BlockReady-time,time<p.BlockEnd)")&&body.Contains("foreach(var r in body.Cloth){if(r==null)continue;r.GetPropertyBlock(body.Block);body.Block.SetFloat(AccentGlowId,glow);r.SetPropertyBlock(body.Block);}"),"the glow pulses with the block cooldown, on the clothing renderers only");
            Check(body.Contains("if(glow==body.Glow)return;"),"the glow is written only when it changes");
            int look=body.IndexOf("private void VisualBodyLook(",StringComparison.Ordinal),call=body.IndexOf("VisualAccentGlow(body,a.Id,time);",look,StringComparison.Ordinal),early=body.IndexOf("if(!on&&!body.SpawnOn&&flash==body.Flash)return;",look,StringComparison.Ordinal);
            Check(look>0&&call>look&&early>call,"VisualBodyLook updates the glow before its early return");
            Check(glow.Contains("Vector3 core=default;bool dressed=Visual!=null&&Visual(pilot.Seat.id,out core);")&&glow.Contains("if(!dressed)")&&glow.Contains("if(dressed)spill.Offer(pilot.Seat.id,i,(core-eye).magnitude);else spill.Offer(pilot.Seat.id,i,(rig.Bones[CapturedRig.Chest].position-eye).magnitude);"),"a visual body's joint halos are skipped, its spill picked from the core");
            Check(glow.Contains("var from=Visual!=null&&Visual(pilot.Seat.id,out var core)?core:rig.Bones[CapturedRig.Chest].TransformPoint(new Vector3(0,-.05f,.16f));"),"the spill light from in front of the core");
            Check(arena.Contains("combatGlow.Visual=visualGlow??=VisualGlowAt;")&&body.Contains("at=body.GearCore!=null?body.GearCore.position+body.GearCore.forward*.14f:"),"the glow asks the presentation where each visual body's core is");
        }
        // Settings · Player model (owner 2026-10-23, a fallback while the new body is tested): a saved preference, New by default; the
        // game sets the presentation's ClassicBodies from it every frame before the arena renders, and a classic frame poses no visual
        // body, so it hides and the voxel body draws (VisualBodyDrawn reads only this frame's pose). Shown only where it is installed.
        private static void ModelSetting(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string game=R("Runtime/ConversionGame.cs"),settings=R("Runtime/NativeSettings.cs"),body=R("Runtime/NativeVisualBodyPresentation.cs"),source=R("Runtime/NativeSourcePresentation.cs");
            Check(game.Contains("alarmLights=\"Full\",playerModel=\"New\";"),"the preference is saved with the others, New by default");
            int set=game.IndexOf("presentation.ClassicBodies=prefs.playerModel==\"Classic\";",StringComparison.Ordinal),render=game.IndexOf("presentation.Render(frame,renderTime,false);",StringComparison.Ordinal);
            Check(set>0&&render>set,"the game sets ClassicBodies from the preference every frame, before the arena renders");
            Check(settings.Contains("if(ArenaPresentation.VisualBodyInstalled){Field(\"Player model\");prefs.playerModel=ChoiceGrid(PlayerModels,prefs.playerModel==\"Classic\"?\"Classic\":\"New\",PlayerModelLabels);"),"Settings shows the choice only where the visual body is installed, anything but Classic reading as New");
            Check(settings.Contains("PlayerModels={\"New\",\"Classic\"}"),"the two choices");
            Check(body.Contains("public bool ClassicBodies;")&&body.Contains("public static bool VisualBodyInstalled=>VisualBodiesOn;"),"the presentation's switch and the installed test");
            int pose=body.IndexOf("private void VisualBodyPose(",StringComparison.Ordinal),skip=body.IndexOf("||ClassicBodies||",pose,StringComparison.Ordinal),create=body.IndexOf("VisualBodyOf(rig,a.Id,",pose,StringComparison.Ordinal);
            Check(pose>0&&skip>pose&&create>skip,"a classic frame poses (and creates) no visual body");
            Check(body.Contains("if(!visualBodies.TryGetValue(rig,out var body)||body.Root==null||body.PosedFrame!=Time.frameCount)return false;")&&body.Contains("if(body.Root.activeSelf!=body.Seen)body.Root.SetActive(body.Seen);"),"an unposed body is not drawn this frame and hides (the voxel body draws)");
            Check(source.Contains("bool visual=(dressed||rig==introRig)&&(first?VisualViewDrawn(rig):VisualBodyDrawn(rig));"),"the pilot draws voxel when its visual body is not drawn");
            int restore=body.IndexOf("private void VisualBodyRestore(",StringComparison.Ordinal);
            Check(restore>0&&!Between2(body,"private void VisualBodyRestore(","private void VisualBodyPose(").Contains("ClassicBodies"),"switching to Classic still puts the rig's hands back (the restore does not read the switch)");
            static string Between2(string t,string a,string b){int i=t.IndexOf(a,StringComparison.Ordinal),j=i<0?-1:t.IndexOf(b,i,StringComparison.Ordinal);return j<0?"":t.Substring(i,j-i);}
        }
        // The visual body's hit flash and match-start spawn-in (Resources/NativeVisualBodyFx.cginc in its toon and hologram;
        // Runtime NativeVisualBodyPresentation VisualBodyLook and NativeSpawnInPresentation SpawnVisualLook).
        private static void BodyFx(string root)
        {
            string Read(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r\n","\n");
            string fx=Read("Resources/NativeVisualBodyFx.cginc"),toon=Read("Resources/NativeVisualBodyToon.shader"),ghost=Read("Resources/NativeVisualBodyGhost.shader"),lit=Read("Resources/NativeSourceLit.shader");
            string body=Read("Runtime/NativeVisualBodyPresentation.cs"),source=Read("Runtime/NativeSourcePresentation.cs"),spawn=Read("Runtime/NativeSpawnInPresentation.cs");
            static string Between(string t,string a,string b){int i=t.IndexOf(a,StringComparison.Ordinal),j=i<0?-1:t.IndexOf(b,i,StringComparison.Ordinal);return j<0?null:t.Substring(i,j-i);}
            static string Bare(string t){var s=new System.Text.StringBuilder();if(t!=null)foreach(char c in t)if(!char.IsWhiteSpace(c))s.Append(c);return s.ToString();}
            // The flash: SourceLit's shared block (indentation aside), after the light, not in the add passes, on the line too.
            var flash=Between(fx,"// ---- HIT FLASH SHARED BEGIN","// ---- HIT FLASH SHARED END");
            Check(flash!=null&&Bare(flash)==Bare(Between(lit,"// ---- HIT FLASH SHARED BEGIN","// ---- HIT FLASH SHARED END")),"the hit flash colour and blend are SourceLit's");
            Check(toon.Contains("finalcolor:VisualFinal")&&Bare(Between(toon,"void VisualFinal(","ENDCG")).Contains("#ifndefUNITY_PASS_FORWARDADDcolor.rgb=VisualHitFlash(color.rgb,"),"the body flashes after the light, not in the add passes");
            Check(toon.Contains("VisualHitFlash(albedo*light,1)"),"the outline is the darker version of the flash");
            Check(Bare(fx).Contains("if(_HitFlash<1.001)returnc;floathead=_HitFlash>2.5?1.0:0.0;floatk=saturate(_HitFlash-(head>0.5?3.0:1.0));"),"the flash code reads as SourceLit's accent w (1 none, 1 + k, 3 + k a headshot)");
            // The spawn-in: the body and its outline clip in the front's dither and take its tint; the hologram is the slice's.
            int includes=0;for(int i=toon.IndexOf("#include \"NativeVisualBodyFx.cginc\"",StringComparison.Ordinal);i>=0;i=toon.IndexOf("#include \"NativeVisualBodyFx.cginc\"",i+1,StringComparison.Ordinal))includes++;
            Check(includes==2&&ghost.Contains("#include \"NativeVisualBodyFx.cginc\""),"the body, its outline and the hologram share the include");
            Check(toon.Contains("SpawnClip(i.worldPos);")&&toon.Contains("o.Albedo=SpawnTinted(")&&toon.Contains("SpawnClip(i.world);")&&toon.Contains("float3 albedo=SpawnTinted("),"the body and its outline form in the front and take its tint (the cast shadow runs the body's surf)");
            Check(toon.Contains("addshadow")&&toon.Contains("_SpawnFront(\"Spawn-in: solid front (world y), band, heat span, slice lead\",Vector)=(100000,")&&toon.Contains("_HitFlash(\"Hit flash code (1 none; the arena sets it per renderer)\",Float)=1"),"unset, the body is formed and unflashed");
            Check(ghost.Contains("Blend SrcAlpha OneMinusSrcAlpha")&&ghost.Contains("ZWrite Off")&&!ghost.Contains("ShadowCaster")&&ghost.Contains("SpawnSlice(i.world.y)*_SpawnHolo.w")&&ghost.Contains("_SpawnGlitch.x*0.55")&&ghost.Contains("UNITY_MATRIX_V[0].xyz*(_GhostSide*_SpawnGlitch.y)"),"the hologram: the slice, the flash silhouette and the split copies, see-through, no shadow");
            // The per-pixel functions as written, and their mapping (x the solid front, w the slice lead) equals Core's per part.
            Check(Bare(fx).Contains("floatSpawnSolid(floaty){floatt=saturate((_SpawnFront.x-y)/_SpawnFront.y);returnt*t*(3.0-2.0*t);}")&&Bare(fx).Contains("floatSpawnHeat(floaty){return1.0-saturate((_SpawnFront.x-_SpawnFront.y-y)/_SpawnFront.z);}")&&Bare(fx).Contains("floatSpawnSlice(floaty){returnsaturate((_SpawnFront.x+_SpawnFront.w-y)/_SpawnFront.y)*(1.0-SpawnSolid(y));}"),"the shader's solid, heat and slice");
            static float Sat(float v)=>v<0?0:v>1?1:v;
            float x0=NativeSpawnIn.SolidLag,band=NativeSpawnIn.Band,span=NativeSpawnIn.HeatSpan,lead=NativeSpawnIn.SolidLag+NativeSpawnIn.GhostLead;float worst=0;
            for(float front=-.6f;front<=2.5f;front+=.037f)for(float h=-.2f;h<=2.2f;h+=.029f)
            {
                float x=front-x0,t=Sat((x-h)/band),solid=t*t*(3-2*t),heat=1-Sat((x-band-h)/span),slice=Sat((x+lead-h)/band)*(1-solid);
                worst=Math.Max(worst,Math.Max(Math.Abs(solid-NativeSpawnIn.Solid(front,h)),Math.Max(Math.Abs(heat-NativeSpawnIn.Heat(front,h)),Math.Abs(slice-NativeSpawnIn.Ghost(front,h)))));
            }
            Check(worst<1e-5f,"per pixel the voxel parts' solid, heat and slice at the same height (worst "+worst+")");
            Check(Bare(fx).Contains("floatk=saturate(_SpawnTint.w*(0.35+0.65*SpawnHeat(y)))*0.8;")&&Bare(fx).Contains("returnc*lerp(t,float3(2.2,2.3,2.5),_SpawnGlitch.x*0.7);")&&Bare(spawn).Contains("floatheat=v.Heat[b>=0&&b<v.Heat.Length?b:0],k=Mathf.Clamp01(v.Frame.Tint*(.35f+.65f*heat))*.8f,flash=v.Frame.Flash*.7f;")&&spawn.Contains("t=Color.Lerp(t,new Color(2.2f,2.3f,2.5f,1),flash);"),"the hardlight tint and flash are SpawnTintOf's");
            // The runtime: one hook in SourceCharacter; Core's values written per renderer; the hologram never on the low tier.
            var character=Between(source,"private bool SourceCharacter(","private void SourceWorld(");
            Check(character!=null&&character.Contains("float flash=dressed&&!first?HitFlashCode(a.Id,time):1;\n")&&character.IndexOf("if(visual)VisualBodyLook(rig,a,time,spawn,flash,reveal,tint);",StringComparison.Ordinal)>character.IndexOf("float flash=",StringComparison.Ordinal),"SourceCharacter hands a visual body its flash and spawn-in, and the reveal and tint for its gear");
            var look=Between(spawn,"private bool SpawnVisualLook(","/// <summary>SourceCharacter (arena rigs)");
            Check(look!=null&&look.Contains("NativeSpawnIn.SolidLag,NativeSpawnIn.Band,NativeSpawnIn.HeatSpan,NativeSpawnIn.SolidLag+NativeSpawnIn.GhostLead")&&look.Contains("v.Built?1e5f:feet+f.Front-NativeSpawnIn.SolidLag")&&look.Contains("bool holo=!LowGraphics;")&&look.Contains("NativeSpawnIn.GhostOpacity")&&look.Contains("WeaponEffectsQuality>=.5f?f.Split:0")&&look.Contains("NativeSpawnIn.FeetOffset"),"the front over the presented feet, Core's constants, the hologram off on the low tier and the split off in Reduced");
            var write=Between(body,"private void VisualBodyLook(","// The pilot gear on the visual body");
            Check(write!=null&&write.Contains("if(!on&&!body.SpawnOn&&flash==body.Flash)return;")&&write.Contains("if(!on)look=VisualSpawnLook.Off;")&&write.Contains("body.Block.SetFloat(HitFlashId,flash);"),"the values are written while a spawn-in runs or the flash changes, and reset after");
            int news=0;for(int i=write?.IndexOf("new ",StringComparison.Ordinal)??-1;i>=0;i=write.IndexOf("new ",i+1,StringComparison.Ordinal))news++;
            Check(news==1&&write.Contains("var look=new VisualSpawnLook();"),"VisualBodyLook builds only a value per frame (the hologram's slots are made once)");
            Gear(body,Read("Runtime/NativeChipSelectPresentation.cs"),Read("Runtime/NativePilotGearModels.cs"),Read("Runtime/NativeVisualCloth.cs"),Read("Runtime/ConversionGame.cs"));
            Check(!body.Contains("NativeSpawnIn")&&!body.Contains("SpawnInFrame"),"the visual body reads the spawn-in only through SpawnVisualLook (the spawn-in's presentation files)");
            foreach(string pack in new[]{"Bozo","BoZo","Magica","OutfitSystem","BMAC","BSMC"})
                Check(!body.Contains(pack)&&!fx.Contains(pack)&&!toon.Contains(pack)&&!ghost.Contains(pack),"no pack types or names in the visual body's code: "+pack);
        }

        // The pilot gear (Core NativePilotGear; Runtime NativePilotGearModels and VisualGear): its sections face outward where
        // they are seen, the lid clears the seated chip, and its states follow the pilot's health and chip select as designed.
        private static void Gear(string body,string chip,string models,string cloth,string game)
        {
            // Every triangle of a section: its centroid and face normal (source frame, as built).
            static System.Collections.Generic.List<(Vector3 c,Vector3 n)> Faces(WeaponMesh mesh)
            {
                var list=new System.Collections.Generic.List<(Vector3,Vector3)>();
                foreach(var s in mesh.Sections)for(int i=0;i+2<s.Indices.Count;i+=3)
                {
                    Vector3 a=s.Positions[s.Indices[i]],b=s.Positions[s.Indices[i+1]],d=s.Positions[s.Indices[i+2]];var n=Vector3.Cross(b-a,d-a);
                    if(n.LengthSquared()>1e-18f)list.Add(((a+b+d)/3,Vector3.Normalize(n)));
                }
                return list;
            }
            // The core: its front faces toward +z (off the chest), its back cap (sunk into the chest) toward -z, nothing else back.
            var core=Faces(NativePilotGear.Core());int coreBack=0;
            foreach(var (c,n) in core){if(c.Z<-.0095f){Check(n.Z<-.9f,"the core's back cap faces into the chest");coreBack++;}else Check(n.Z>-.05f,"the core's rim and gem face off the chest");}
            Check(core.Count>100&&coreBack>0,"the core is built ("+core.Count+" faces)");
            float width=0;foreach(var v in NativePilotGear.Shield(1.05f,0))width=Math.Max(width,Math.Abs(v.X)*2);Check(width>.07f&&width<.1f,"the core is 7 to 10 cm across ("+width+")");
            // The device on a 4.7 cm sleeve: the band's outer face faces away from the forearm, its housing's top away from the back
            // of the forearm, the bay's walls into the bay; the lid's lens and the chip's crystal outward; the lid clears the chip.
            const float r=.047f;var device=Faces(NativePilotGear.Device(r));int band=0;
            foreach(var (c,n) in device)
            {
                float radial=(float)Math.Sqrt(c.X*c.X+c.Z*c.Z);
                if(Math.Abs(c.Y)<.019f&&radial>r+.0062f&&radial<r+.0075f&&c.X>-.005f){var out_=new Vector3(c.X,0,c.Z)/radial;Check(Vector3.Dot(n,out_)>.9f,"the band's face faces off the forearm");band++;}
                if(c.X<-(r+NativePilotGear.HousingTop-.0002f)&&c.X>-(r+NativePilotGear.HousingTop+.0002f)&&Math.Abs(c.Z)<.02f&&c.Y>-.02f&&c.Y<.024f)Check(Math.Abs(n.X)>.9f,"the housing's top lies flat (facing off the back of the forearm; the frame bars' undersides on it face in, hidden)");
            }
            Check(band>=12,"the band's outer face is built ("+band+")");
            var bayMid=new Vector3(-(r+NativePilotGear.HousingTop),(NativePilotGear.BayNear+NativePilotGear.BayFar)*.5f,0);
            foreach(var (c,n) in Faces(NativePilotGear.Bay(r)))if(Math.Abs(n.X)<.5f)Check(Vector3.Dot(n,new Vector3(0,bayMid.Y-c.Y,bayMid.Z-c.Z))>0,"the bay's walls face into the bay");else Check(n.X<-.9f,"the bay's floor faces up out of the housing");
            foreach(var (c,n) in Faces(NativePilotGear.Lid()))if(c.X<-NativePilotGear.LidThick-.0003f)Check(n.X<-.3f,"the lid's lens faces out");
            var chipMesh=NativePilotGear.Chip();float chipTop=0;foreach(var s in chipMesh.Sections)foreach(var v in s.Positions)chipTop=Math.Min(chipTop,v.X);
            foreach(var (c,n) in Faces(chipMesh))if(c.X<-NativePilotGear.ChipThick-.0001f)Check(n.X<-.3f,"the chip's crystal faces out");
            float seatX=NativePilotGear.Seat(r).X,hingeX=NativePilotGear.Hinge(r).X;
            Check(seatX+chipTop-.0003f>hingeX,"the shut lid clears the seated chip ("+(seatX+chipTop)+" over "+hingeX+")");
            // States: the core pulses gently, flares as the body appears and settles, flickers only at low health.
            float lo=9,hi=0;for(float t=0;t<6;t+=.01f){float g=NativePilotGear.CoreGlow(t,1,-1,7);lo=Math.Min(lo,g);hi=Math.Max(hi,g);}
            Check(lo>=1-NativePilotGear.PulseDepth-1e-4f&&hi<=1+1e-4f&&hi-lo>.1f,"the healthy core pulses within its depth");
            Check(NativePilotGear.CoreGlow(1,1,0,7)<.01f&&NativePilotGear.CoreGlow(1,1,NativePilotGear.IgniteRise,7)>1.5f&&Math.Abs(NativePilotGear.CoreGlow(1,1,3,7)/NativePilotGear.CoreGlow(1,1,-1,7)-1)<.01f,"the core flares as the body appears and settles");
            int dips=0;for(float t=0;t<10;t+=NativePilotGear.FlickerSlot){if(NativePilotGear.CoreGlow(t,1,-1,3)<1-NativePilotGear.PulseDepth-.01f)dips=-1000;if(NativePilotGear.CoreGlow(t,.05f,-1,3)<.6f)dips++;}
            Check(dips>10,"the core flickers at low health only ("+dips+")");
            Check(NativePilotGear.CoreGlow(2.5f,.1f,-1,9)==NativePilotGear.CoreGlow(2.5f,.1f,-1,9),"the core's glow is a pure function");
            // The device: shut at rest, open with the look; the chip from its carry's end while the lid is open; the bay in the chip's
            // colour while accepted; the lens keeping it after the lid has shut, back to rest after ActiveSeconds.
            var rest=NativePilotGear.Device(0,-1,float.NaN,1);Check(rest.Lid==0&&!rest.Chip&&rest.Bay==0&&!rest.LensChip,"the device rests shut");
            Check(NativePilotGear.Device(1,-1,float.NaN,1).Lid>.99f,"the lid opens with the look");
            Check(!NativePilotGear.Device(1,NativeDraftPlant.CarryEnd-.05f,float.NaN,1).Chip&&NativePilotGear.Device(1,NativeDraftPlant.CarryEnd+.05f,.05f,1).Chip,"the chip shows from the carry's end");
            var accepted=NativePilotGear.Device(1,(NativeDraftPlant.PulseStart+NativeDraftPlant.PulseEnd)*.5f,.3f,1);Check(accepted.BayChip&&accepted.Bay>1.2f&&accepted.LensChip,"the bay and lens take the chip's colour as it is accepted");
            float after=NativeDraftPlant.PulseEnd-NativeDraftPlant.CarryEnd;
            Check(NativePilotGear.Device(0,-1,after+.5f,1).LensChip&&!NativePilotGear.Device(0,-1,after+NativePilotGear.ActiveSeconds+.01f,1).LensChip,"the lens keeps the chip's colour for ActiveSeconds after the lid has shut");
            // The face gear (the concept's mask and visor, in the head bone's frame): mirror-symmetric shells on the face, the front
            // facing out; the mask over the nose tip, the visor over the eyes' band; the Prototype body skin wears it.
            foreach(var (mesh,name) in new[]{(NativePilotGear.Mask(),"mask"),(NativePilotGear.Visor(),"visor")})
            {
                var faces=Faces(mesh);float minX=9,maxX=-9,minY=9,maxY=-9,minZ=9;var front=faces[0];
                foreach(var sec in mesh.Sections)foreach(var v in sec.Positions){minX=Math.Min(minX,v.X);maxX=Math.Max(maxX,v.X);minY=Math.Min(minY,v.Y);maxY=Math.Max(maxY,v.Y);minZ=Math.Min(minZ,v.Z);}
                foreach(var f in faces)if(f.c.Z>front.c.Z)front=f;
                Check(faces.Count>100&&Math.Abs(minX+maxX)<1e-4f&&maxX<.11f&&minY>-.06f&&maxY<.1f&&minZ>.02f&&front.n.Z>.5f,"the "+name+" is built, symmetric, on the face and facing out ("+faces.Count+" faces, x to "+maxX+", y "+minY+".."+maxY+")");
                if(name=="visor")Check(minY<=.044f&&maxY>=.08f,"the visor covers the eyes' band (y .044 to .080)");
            }
            Check(NativePilotGear.MaskSurface(0,.03f)>.154f&&NativePilotGear.MaskSurface(0,-.03f)>.125f,"the mask clears the nose tip and the chin");
            Check(body.Contains("private static bool WearsFaceGear(int bodySkin)=>bodySkin==NativeBodySkins.PrototypeIndex;")&&body.Contains("if(head==null||!WearsFaceGear(BodySkin(id))||!sourceModels.PilotFace())return;"),"the Prototype body skin wears the face gear; the Standard keeps the authored face");
            // The back emblem: a ring, two X eyes and a smile facing out of the back, inside its radius, on the chest bone.
            var emblem=Faces(NativePilotGear.Emblem());float reach=0;bool outward=true;foreach(var (c,n) in emblem){reach=Math.Max(reach,new Vector2(c.X,c.Y).Length());outward&=n.Z>.99f;}
            Check(emblem.Count>=2*(28+4+10)&&outward&&reach<=NativePilotGear.EmblemRadius,"the back emblem's strokes face out of the back, inside its radius ("+emblem.Count+" faces)");
            Check(models.Contains("GearModel(GearEmblem,NativePilotGear.Emblem(),CapturedRig.Chest);")&&body.Contains("gearMatrix=chest.localToWorldMatrix*body.Emblem;"),"every model pilot wears the back emblem on its chest bone (its frame the worn top's)");
            // The wardrobe (owner 2026-10-26): the outfit map normalizes as the look contract has it; every model instance is
            // dressed when made (the live and first-person bodies, corpses and what is made from them), worn renderers only from
            // then on; a changed outfit makes the body again; the gear's anchors and the device's sleeve follow the top.
            Check(NativeLook.Slots.Length==NativeLook.Ids.Length&&NativeLook.Ids.Length==NativeLook.Labels.Length&&NativeLook.Normalize("")==""&&NativeLook.Normalize("upper:hard-jacket,feet:work-boots,hair:wild")=="hair:wild,upper:hard-jacket,feet:work-boots"
                &&NativeLook.Normalize("upper:open-hoodie,lower:gold,cape:red")==""&&NativeLook.With("",NativeLook.Hands,"bare")=="hands:bare"&&NativeLook.Index("feet:sneakers",NativeLook.Feet)==2&&NativeLook.Of("",NativeLook.Upper)=="open-hoodie"
                &&System.Linq.Enumerable.All(NativeLook.Ids,ids=>System.Linq.Enumerable.All(ids,id=>System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-z0-9-]{1,24}$"))),"the outfit map: canonical slot order, defaults omitted, unknown slots and ids dropped, contract ids");
            int longest=0;foreach(var ids in NativeLook.Ids){int m=0;foreach(var id in ids)m=Math.Max(m,id.Length);longest+=m;}
            Check(longest+NativeLook.Slots.Length*8<NativeLook.MaxLength,"the longest outfit map fits the contract's limit");
            Check(body.Contains("var look=VisualLookOf(id);var sheldon=new List<Renderer>();VisualDress(root,look,sheldon);")&&body.Contains("body.All=VisualWorn(root);")&&body.Contains("root.name=\"Visual corpse \"+death.id;var look=VisualLookOf(death.id);var sheldon=new List<Renderer>();VisualDress(root,look,sheldon);")
                &&!body.Contains("All=root.GetComponentsInChildren<Renderer>(true)")&&body.Contains("foreach(var r in VisualWorn(root))"),"every model instance is dressed when made and keeps only its worn renderers");
            Check(body.Contains("(body.Hood!=hood||!string.Equals(body.Look,VisualLookOf(id),StringComparison.Ordinal))){VisualStrideRelease(body);VisualMotionRelease(body);body.Root.SetActive(false);Destroy(body.Root);body.Root=null;}")&&body.Contains("if(viewBody?.Root!=null&&!string.Equals(viewBody.Look,LocalLook??\"\",StringComparison.Ordinal))"),"a changed outfit makes the body (and the first-person body) again");
            Check(body.Contains("foreach(var p in d.pieces)if(p?.renderers!=null)foreach(var n in p.renderers)if(lookNamed.TryGetValue(n,out var r))r.gameObject.SetActive(false);")&&body.Contains("r.gameObject.SetActive(!lookCovers.Contains(b.part));")&&body.Contains("skin.SetBlendShapeWeight(t.shape,lookTags.Contains(t.tag)?100:0);"),"dressing: every piece off then the worn on, covered body parts off, tag shapes from the worn pieces' tags");
            Check(body.Contains("VisualAnchorsFor(body);")&&body.Contains("body.GearRadius=best.radius;")&&body.Contains("body.Emblem=EmblemFrameAt(best.emblem[0],best.emblem[1]);")&&body.Contains("sourceModels.Queue(sourceModels.GearDeviceFor(body.GearRadius),m,gearBone,")&&models.Contains("gearDevices[mm]=device;gearBays[mm]=bay;"),"the gear follows the top: core, device and sleeve, emblem; one device model per sleeve radius");
            Check(body.Contains("var seat=WeaponLoadout?.Match?.Seat(id);if(seat!=null)return seat.look??\"\";")&&body.Contains("if(id==ViewBodyId||id==RooftopIdTop)return LocalLook??\"\";")&&game.Contains("presentation.LocalLook=LookShown();"),"pilots wear the displayed arena seat's outfit; previews and the rooftop keep the local DEV-gated outfit");
            // The cloth hook: a local simulation registers; never on the first-person body; reset on a gap; held on the low tier and far away.
            Check(cloth.Contains("public static Func<GameObject,Transform,object> Attach;")&&body.Contains("if(id!=ViewBodyId&&NativeVisualCloth.Attach!=null)")&&body.Contains("body.Binding.State.Forget();if(body.ClothSim!=null)NativeVisualCloth.Reset?.Invoke(body.ClothSim);")
                &&body.Contains("bool sim=body.Seen&&!LowGraphics&&(VisualEye==null||(VisualEye.transform.position-body.At).sqrMagnitude<NativeVisualCloth.Far*NativeVisualCloth.Far);"),"cloth: registered locally, off in first person, reset on a gap, held on the low tier and past Far");
            foreach(string pack in new[]{"Bozo","BoZo","Magica","BMAC","BSMC"})Check(!cloth.Contains(pack),"no pack names in the cloth hook: "+pack);
            Check(models.Contains("GearModel(GearMask,NativePilotGear.Mask(),CapturedRig.Head);GearModel(GearVisor,NativePilotGear.Visor(),CapturedRig.Head);")&&body.Contains("if(!first)VisualFaceGear(VisualHeadOf(body),a.Id,a.AccentColor,layer,camera,reveal,tint,flash);")&&body.Contains("VisualFaceGear(VisualHeadOf(body),seat.id,seat.accentColor,0,null,null,null,0);")&&body.Contains("if(c.Look==0&&corpse.Twin==null&&corpse.Host==null&&!corpse.IsCrushed(CapturedRig.Head)&&corpse.Death!=null)"),"the face gear rides the model's head bone: the live body, the draft's pilot and a whole corpse");
            Check(body.Contains("int gear=NativeWeaponSkins.Index(SeatWeaponSkins(id),NativeWeaponSkins.FaceEntry);")&&body.Contains("if(gear!=NativeWeaponSkins.FaceVisor)sourceModels.Queue(NativeSourceModels.GearMask,")&&body.Contains("if(gear!=NativeWeaponSkins.FaceMask)sourceModels.Queue(NativeSourceModels.GearVisor,"),"the face gear pick draws both, the visor alone or the mask alone");
            // The runtime: the anchors by name, the device's look read from the chip-select slot, the models on one SourceLit material,
            // and VisualGear building only values.
            Check(body.Contains("t.name==\"VisualGearCore\"")&&body.Contains("t.name==\"VisualGearDevice\"")&&body.Contains("t.name==\"VisualGearDeviceRim\""),"the gear's anchors by the bake's names");
            Check(chip.Contains("private void ChipDeviceLook(int id,out float look,out float plant,out int rgb,out int key)")&&chip.Contains("slot.Frame!=Time.frameCount)return;"),"the device reads the chip-select slot posed this frame");
            Check(models.Contains("Resources.Load<Shader>(\"NativeSourceLit\")")&&models.Contains("enableInstancing=true")&&models.Contains("WeaponMeshOf(s,id)"),"the gear's models on one instanced SourceLit material, mirrored as the weapons are");
            int gearAt=body.IndexOf("private void VisualGear(",StringComparison.Ordinal),gearEnd=body.IndexOf("private Func<int,Matrix4x4> gearBone;",StringComparison.Ordinal);var gear=gearAt>=0&&gearEnd>gearAt?body.Substring(gearAt,gearEnd-gearAt):"";
            foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(gear,@"\bnew\s+([A-Z]\w*)"))Check(m.Groups[1].Value=="Vector4"||m.Groups[1].Value=="Vector3"||m.Groups[1].Value=="Color","VisualGear builds only values ("+m.Value+")");
            Check(gear.Length>0&&!gear.Contains("=>")&&!gear.Contains(".Where(")&&!gear.Contains(".Select("),"VisualGear: no lambdas or LINQ per frame");
        }
    }
}
#endif
