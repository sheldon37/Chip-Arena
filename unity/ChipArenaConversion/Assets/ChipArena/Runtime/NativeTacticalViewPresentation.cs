using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The first-person tactical view on the view body (Core/NativeTacticalView.cs; docs/FPS-TACTICAL.md; owner 2026-11-21:
    // "Utilize new FPS system / animations from tactical pack"). With the local view-motion bank installed, the pistol, rifle and
    // machine gun in first person (chosen in Settings; aiming never forces it) are held by the pilot model's own forearms and
    // hands on the game's own weapon models, posed by the pack's clips: the body is set at true size with its shoulders on the
    // clip's, each arm reaches its hand by two-bone IK (the clip's elbow as the pole), the wrists' roll goes into the forearms'
    // twist bones and the clip's fingers close the hands while an action moves them. The view rig's right hand takes the model's
    // weapon frame (fitted, as VisualViewPose leaves it), so the muzzle flash, the rounds' origin and the kit pieces stay on the
    // model. Without the bank, for another weapon or class, the chip select's raised wrist, a scope or a downed pilot, the old
    // view model draws as before. Third person never reads any of it. Presentation only: aim, muzzle, shots and rules are the game's.
    // With the local pack guns installed too (ChipArenaViewGuns; owner 2026-11-21: "Use pack guns"), first person draws the pack's
    // own gun for each (the WK-11 Viper, the SRM-12, the TR15) in place of the game's model, whatever the skin: it rides the clip's
    // gun bone, its slide, bolt, magazine and trigger move by its own weapon clips, both hands take the pack's places and palms
    // and every finger the clip's, and the view rig's right hand takes the Standard gun's frame on the pack gun's muzzle (the
    // flash, the brass and the chip kit there). The tactical view's arms are cut at the shoulders, not the elbows, so they run in
    // from the screen's edges ("Seems they disappear on certain weapons in first person").
    public sealed partial class ArenaPresentation
    {
        private static NativeTacticalBank tacticalBank;private static bool tacticalTried;
        /// <summary>The installed view-motion bank (null without one, or when it fails to read).</summary>
        public static NativeTacticalBank TacticalBank{get{TacticalLoad();return tacticalBank;}}
        private static void TacticalLoad()
        {
            if(tacticalTried)return;tacticalTried=true;
            try{var asset=Resources.Load<TextAsset>(NativeTacticalBank.Resource);if(asset!=null)tacticalBank=NativeTacticalBank.Parse(asset.bytes);}
            catch(System.Exception e){tacticalBank=null;Debug.LogWarning("View-motion bank not read: "+e.Message);}
        }
        private NativeTacticalView tacticalView;private readonly NativeTacticalSolution tacticalSolve=new NativeTacticalSolution();
        private NativeTacticalCalibration tacticalCal;private VisualBody tacticalCalBody;private VisualWeapon tacticalCalHeld;private WeaponId tacticalCalGun;private bool tacticalCalCalm;
        private int tacticalFrame=-10,tacticalScopedFrame=-10,tacticalKickFrame=-1;
        private Quaternion tacticalCamera=Quaternion.identity,tacticalKick=Quaternion.identity;
        private TacticalArms tacticalArms;
        // The pack guns under the view body (a child per gun key, the one in hand shown) and the frame one drew.
        private static GameObject tacticalGunsPrefab;private static bool tacticalGunsTried;
        private GameObject tacticalGuns;private VisualBody tacticalGunsBody;private int tacticalPackFrame=-10,tacticalPackShown=-10;
        private readonly System.Collections.Generic.Dictionary<string,TacticalGunView> tacticalGunViews=new System.Collections.Generic.Dictionary<string,TacticalGunView>();
        private bool tacticalCalPack;
        /// <summary>Whether the tactical view drew the local pilot's first person this frame.</summary>
        public bool TacticalViewDrawn=>tacticalFrame==Time.frameCount;
        // The walk share of the view's gait (NativeViewSway.Motion, speed over 6.8 m/s) at which the pack's walk is whole.
        private const float TacticalWalkMotion=.6f;

        // The view body's arm, hand, finger and forearm twist bones (by the pack-style names the BoZo skeleton shares), each finger
        // bone's length axis in its own frame, each twist bone's share of the wrist's roll. Null: the body lacks one.
        private sealed class TacticalArms
        {
            public VisualBody Body;public readonly Transform[] Upper=new Transform[2],Lower=new Transform[2],Hand=new Transform[2];
            public readonly Transform[][] Fingers={new Transform[15],new Transform[15]};public readonly Vector3[][] Axis={new Vector3[15],new Vector3[15]};
            public readonly Transform[][] Twist={new Transform[2],new Transform[2]};public readonly float[][] Share={new float[2],new float[2]};
            public readonly Transform[][] UpperTwist={new Transform[2],new Transform[2]};
            public readonly Vector3[][] TwistScale={new Vector3[2],new Vector3[2]},UpperTwistScale={new Vector3[2],new Vector3[2]};
            // The places the binding never writes (it turns bones only): put back every frame before the arms are posed again.
            public readonly Vector3[] UpperAt=new Vector3[2],HandAt=new Vector3[2];
            public void Rest()
            {
                for(int i=0;i<2;i++)
                {
                    if(Upper[i]!=null)Upper[i].localPosition=UpperAt[i];if(Hand[i]!=null)Hand[i].localPosition=HandAt[i];
                    for(int k=0;k<2;k++){if(Twist[i][k]!=null)Twist[i][k].localScale=TwistScale[i][k];if(UpperTwist[i][k]!=null)UpperTwist[i][k].localScale=UpperTwistScale[i][k];}
                }
            }
        }
        private static readonly string[] TacticalDigits={"thumb","index","middle","ring","pinky"};
        private static TacticalArms TacticalArmsOf(VisualBody body)
        {
            var bones=body?.Binding?.Bones;if(bones==null)return null;
            Transform Find(string n){foreach(var t in bones)if(t!=null&&t.name==n)return t;return null;}
            var arms=new TacticalArms{Body=body};
            for(int side=0;side<2;side++)
            {
                string s=side==0?"_l":"_r";
                arms.Upper[side]=Find("upperarm"+s);arms.Lower[side]=Find("lowerarm"+s);arms.Hand[side]=Find("hand"+s);
                if(arms.Upper[side]==null||arms.Lower[side]==null||arms.Hand[side]==null)return null;
                arms.UpperAt[side]=arms.Upper[side].localPosition;arms.HandAt[side]=arms.Hand[side].localPosition;
                for(int d=0;d<5;d++)for(int k=0;k<3;k++)
                {
                    var b=Find(TacticalDigits[d]+"_0"+(k+1)+s);if(b==null)return null;arms.Fingers[side][d*3+k]=b;
                }
                for(int d=0;d<5;d++)for(int k=0;k<3;k++)
                {
                    // The direction to its child in its own frame; the last segment keeps the axis it was offset along (as the bake reads it).
                    var b=arms.Fingers[side][d*3+k];var child=k<2?arms.Fingers[side][d*3+k+1]:null;
                    var axis=child!=null&&child.parent==b?child.localPosition:b.localPosition;arms.Axis[side][d*3+k]=axis.sqrMagnitude>1e-10f?axis.normalized:Vector3.forward;
                }
                float length=Vector3.Distance(arms.Lower[side].position,arms.Hand[side].position);
                for(int i=0;i<2;i++)
                {
                    var t=Find("lowerarm_twist_0"+(i+1)+s);arms.Twist[side][i]=t;if(t!=null)arms.TwistScale[side][i]=t.localScale;
                    var u=Find("upperarm_twist_0"+(i+1)+s);arms.UpperTwist[side][i]=u;if(u!=null)arms.UpperTwistScale[side][i]=u.localScale;
                    if(t!=null&&length>1e-4f)arms.Share[side][i]=Mathf.Clamp01(Vector3.Dot(t.position-arms.Lower[side].position,(arms.Hand[side].position-arms.Lower[side].position).normalized)/length);
                }
            }
            return arms;
        }

        // The palm frame the bake reads (forward wrist to middle_01, across index_01 to pinky_01, up their cross).
        private static Quaternion TacticalPalm(TacticalArms arms,int side)
        {
            var f=arms.Fingers[side];var hand=arms.Hand[side];Transform index=f[3],middle=f[6],pinky=f[12];
            Vector3 fwd=(middle.position-hand.position).normalized,across=index.position-pinky.position;across=(across-Vector3.Dot(across,fwd)*fwd).normalized;
            return fwd.sqrMagnitude>.5f&&across.sqrMagnitude>.5f?Quaternion.LookRotation(fwd,Vector3.Cross(fwd,across)):hand.rotation;
        }
        // The wrist's roll about the forearm (radians, signed).
        private static float TacticalTwist(Transform lower,Transform hand)
        {
            Vector3 axis=Quaternion.Inverse(lower.rotation)*(hand.position-lower.position);if(axis.sqrMagnitude<1e-10f)return 0;axis.Normalize();
            var rel=Quaternion.Inverse(lower.rotation)*hand.rotation;float proj=rel.x*axis.x+rel.y*axis.y+rel.z*axis.z;
            float a=2*Mathf.Atan2(proj,rel.w);return a>Mathf.PI?a-2*Mathf.PI:a<-Mathf.PI?a+2*Mathf.PI:a;
        }
        // Two-bone IK: the elbow on the side of the pole, the hand on the target; an arm too short reaches from its shoulder first.
        private static void TacticalReach(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 pole)
        {
            Vector3 a=upper.position,b=lower.position,c=hand.position;float la=Vector3.Distance(a,b),lb=Vector3.Distance(b,c);if(la<1e-4f||lb<1e-4f)return;
            Vector3 to=target-a;float d=to.magnitude,reach=(la+lb)*.999f;
            if(d>reach){upper.position=a+to/d*(d-reach);a=upper.position;to=target-a;d=to.magnitude;}
            if(d<1e-4f)return;d=Mathf.Clamp(d,Mathf.Abs(la-lb)+1e-4f,reach);Vector3 dir=to.normalized;
            Vector3 bend=pole-a;bend-=Vector3.Dot(bend,dir)*dir;if(bend.sqrMagnitude<1e-8f){bend=b-a;bend-=Vector3.Dot(bend,dir)*dir;}if(bend.sqrMagnitude<1e-8f)bend=Vector3.down;bend.Normalize();
            float cosA=Mathf.Clamp((la*la+d*d-lb*lb)/(2*la*d),-1,1),sinA=Mathf.Sqrt(1-cosA*cosA);
            Vector3 elbow=a+dir*(la*cosA)+bend*(la*sinA);
            upper.rotation=Quaternion.FromToRotation(b-a,elbow-a)*upper.rotation;
            b=lower.position;c=hand.position;lower.rotation=Quaternion.FromToRotation(c-b,target-b)*lower.rotation;
        }

        /// <summary>The pack gun's hand anchors this frame (each hand bone's target in the gun's frame, the gun, both hand bones,
        /// the action and weapon clip): a diagnosis read by a local Editor probe (the hands on the gun through every clip). Written
        /// by the tactical view only; nothing in the game reads it.</summary>
        public static class TacticalAnchor{public static Transform Gun,HandR,HandL;public static Vector3 Right,Left;public static string State="";public static int Frame=-1;}

        // ---- The pack guns (ChipArenaViewGuns, made by the local bake: the pack's weapon prefabs on Built-in materials) ----
        private sealed class TacticalGunView
        {
            public string Key;public Transform Root;
            // Each weapon clip's bones under the gun (null where one is missing), and every bone any of them moves with its rest.
            public readonly System.Collections.Generic.Dictionary<string,Transform[]> Bones=new System.Collections.Generic.Dictionary<string,Transform[]>();
            public readonly System.Collections.Generic.List<Transform> Touched=new System.Collections.Generic.List<Transform>();
            public readonly System.Collections.Generic.List<Vector3> RestP=new System.Collections.Generic.List<Vector3>();
            public readonly System.Collections.Generic.List<Quaternion> RestR=new System.Collections.Generic.List<Quaternion>();
            public System.Numerics.Vector3[] P=new System.Numerics.Vector3[0];public System.Numerics.Quaternion[] Q=new System.Numerics.Quaternion[0];
        }
        private static GameObject TacticalGunsPrefab{get{if(!tacticalGunsTried){tacticalGunsTried=true;tacticalGunsPrefab=Resources.Load<GameObject>(NativeTacticalBank.GunsResource);}return tacticalGunsPrefab;}}
        /// <summary>Whether first person draws the pack's own gun for `g` (the guns prefab and the bank's entry installed).</summary>
        private static bool TacticalPackReady(NativeTacticalBank bank,NativeTacticalGun g)=>bank?.Gun(g?.PackGun)!=null&&TacticalGunsPrefab!=null;
        private TacticalGunView TacticalGunOf(NativeTacticalBank bank,NativeTacticalGun g)
        {
            var prefab=TacticalGunsPrefab;if(prefab==null||viewBody?.Root==null||bank.Gun(g.PackGun)==null)return null;
            if(tacticalGuns==null||tacticalGunsBody!=viewBody)
            {
                if(tacticalGuns!=null)Destroy(tacticalGuns);tacticalGunViews.Clear();
                tacticalGuns=Instantiate(prefab,viewBody.Root.transform,false);tacticalGuns.name="Tactical view guns";tacticalGunsBody=viewBody;
                foreach(var t in tacticalGuns.GetComponentsInChildren<Transform>(true))t.gameObject.layer=10;
                for(int c=0;c<tacticalGuns.transform.childCount;c++)tacticalGuns.transform.GetChild(c).gameObject.SetActive(false);
            }
            if(tacticalGunViews.TryGetValue(g.PackGun,out var v))return v;
            var root=tacticalGuns.transform.Find(g.PackGun);v=root==null?null:new TacticalGunView{Key=g.PackGun,Root=root};
            if(v!=null)foreach(var wc in bank.WeaponClips)
            {
                if(wc.Gun!=g.PackGun)continue;var bones=new Transform[wc.Bones];
                for(int i=0;i<wc.Bones;i++)
                {
                    var b=bones[i]=root.Find(wc.Paths[i]);if(b==null||v.Touched.Contains(b))continue;
                    v.Touched.Add(b);v.RestP.Add(b.localPosition);v.RestR.Add(b.localRotation);
                }
                v.Bones[wc.Key]=bones;
            }
            tacticalGunViews[g.PackGun]=v;return v;
        }
        // The gun's moving parts: back to rest, then the clip's frame at `time`.
        private static void TacticalGunPose(TacticalGunView v,NativeTacticalBank bank,string clip,float time)
        {
            for(int i=0;i<v.Touched.Count;i++)v.Touched[i].SetLocalPositionAndRotation(v.RestP[i],v.RestR[i]);
            var wc=bank.WeaponClip(clip);if(wc==null||!v.Bones.TryGetValue(wc.Key,out var bones))return;
            if(v.P.Length<wc.Bones){v.P=new System.Numerics.Vector3[wc.Bones];v.Q=new System.Numerics.Quaternion[wc.Bones];}
            wc.Sample(time,v.P,v.Q);
            for(int i=0;i<wc.Bones;i++)if(bones[i]!=null)bones[i].SetLocalPositionAndRotation(CapturedAnimation.Vec(v.P[i]),CapturedAnimation.Quat(v.Q[i]));
        }
        // Only `on` shown (none: all hidden).
        private void TacticalGunsShown(TacticalGunView on)
        {
            if(tacticalGuns==null)return;var t=tacticalGuns.transform;
            for(int c=0;c<t.childCount;c++){var child=t.GetChild(c).gameObject;bool show=on!=null&&child.transform==on.Root;if(child.activeSelf!=show)child.SetActive(show);}
        }
        /// <summary>VisualWeaponDrawn, for the view rig: the pack gun drew in the game model's place this frame (SourceCharacter's
        /// own gate: not put away, stowed or spawning); its body's model hides.</summary>
        private bool TacticalPackDrawn(VisualBody body)
        {
            if(tacticalPackFrame!=Time.frameCount)return false;
            if(body?.Shown!=null){body.Shown.Hide();body.Shown=null;}
            tacticalPackShown=Time.frameCount;return true;
        }
        /// <summary>VisualViewWeaponShown: the pack gun hides the frame SourceCharacter did not draw it.</summary>
        private void TacticalPackAfterDraw(){if(tacticalPackShown!=Time.frameCount)TacticalGunsShown(null);}

        // ---- The arms' cut: the elbows for the old view model (CreateViewBody), the shoulders for the tactical view ----
        // Shown: whether the outfit draws the renderer at all (the dressing switches off the skin parts under its garments and
        // the hologram proxy); a cut never turns on what it switched off (the brown patches on the sleeves were the proxy).
        private sealed class TacticalCut{public SkinnedMeshRenderer Skin;public Mesh Source,Elbow,Shoulder;public bool Made,Owned,Shown;}
        private readonly System.Collections.Generic.List<TacticalCut> tacticalCuts=new System.Collections.Generic.List<TacticalCut>();
        private VisualBody tacticalCutBody;private bool tacticalShoulders;
        private static readonly System.Collections.Generic.Dictionary<Mesh,Mesh> shoulderMeshes=new System.Collections.Generic.Dictionary<Mesh,Mesh>();
        /// <summary>CreateViewBody: a skinned mesh of the new view body, its source and its elbow cut (null: none).</summary>
        private void TacticalViewSkin(VisualBody body,SkinnedMeshRenderer skin,Mesh source,Mesh elbow)
        {
            if(tacticalCutBody!=body){foreach(var old in tacticalCuts)if(old.Owned&&old.Shoulder!=null)Destroy(old.Shoulder);tacticalCuts.Clear();tacticalCutBody=body;tacticalShoulders=false;}
            tacticalCuts.Add(new TacticalCut{Skin=skin,Source=source,Elbow=elbow,Shown=skin.enabled});
        }
        private void TacticalArmCut(bool shoulders)
        {
            if(viewBody==null||tacticalCutBody!=viewBody||shoulders==tacticalShoulders)return;
            System.Collections.Generic.HashSet<Transform> arm=null;
            if(shoulders)
            {
                var arms=tacticalArms;if(arms==null||arms.Body!=viewBody)return;
                // From the clavicles down: a garment's upper sleeve leans on the clavicle more than the skin under it does, and cut at the
                // upper arm alone the sleeve went while the skin stayed (bare upper arms in a long-sleeved top).
                arm=new System.Collections.Generic.HashSet<Transform>();
                for(int side=0;side<2;side++){var top=arms.Upper[side].parent!=null&&arms.Upper[side].parent.name.StartsWith("clavicle",System.StringComparison.Ordinal)?arms.Upper[side].parent:arms.Upper[side];foreach(var t in top.GetComponentsInChildren<Transform>(true))arm.Add(t);}
            }
            if(shoulders&&tacticalCuts.Exists(c=>c.Skin!=null&&!c.Made))
            {
                foreach(var c in tacticalCuts)if(c.Skin!=null&&!c.Made){c.Shoulder=ArmMesh(c.Skin,arm,c.Source,shoulderMeshes," (arms)");c.Made=true;}
                TacticalSkinUnder();
            }
            foreach(var c in tacticalCuts)
            {
                if(c.Skin==null)continue;
                var mesh=shoulders?c.Shoulder:c.Elbow;c.Skin.enabled=mesh!=null&&c.Shown;if(mesh!=null&&c.Skin.sharedMesh!=mesh)TacticalSwapMesh(c.Skin,mesh);
            }
            tacticalShoulders=shoulders;
        }
        // The body's skin under the garments (owner 2026-11-21: "Fix pilot's arms"; no skin may show through a sleeve): every skin
        // triangle whose three corners lie within TacticalSkinGap of a drawn garment vertex goes from the skin's cut, so a sleeve
        // or glove is the only surface there, and bare arms, bare hands and a short sleeve's hem keep the skin they show. Made
        // once per view body (its outfit), on the posed arms; the skin is the body's "Body" renderer.
        private const float TacticalSkinGap=.035f;
        private void TacticalSkinUnder()
        {
            var grid=new System.Collections.Generic.Dictionary<Vector3Int,System.Collections.Generic.List<Vector3>>();float cell=TacticalSkinGap;
            Vector3Int Key(Vector3 v)=>new Vector3Int(Mathf.FloorToInt(v.x/cell),Mathf.FloorToInt(v.y/cell),Mathf.FloorToInt(v.z/cell));
            var baked=new Mesh();var verts=new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector3> World(TacticalCut c)
            {
                c.Skin.BakeMesh(baked,true);baked.GetVertices(verts);var m=c.Skin.transform.localToWorldMatrix;var w=new System.Collections.Generic.List<Vector3>(verts.Count);
                foreach(var v in verts)w.Add(m.MultiplyPoint3x4(v));return w;
            }
            bool any=false;
            foreach(var c in tacticalCuts)
            {
                if(c.Skin==null||c.Shoulder==null||!c.Shown||TacticalIsSkin(c.Skin))continue;
                // The garment's drawn surface, sampled no sparser than the gap (a plate's few corners leave its middle bare).
                var w=World(c);
                void Add(Vector3 v){var k=Key(v);if(!grid.TryGetValue(k,out var l))grid[k]=l=new System.Collections.Generic.List<Vector3>();l.Add(v);any=true;}
                for(int sm=0;sm<c.Shoulder.subMeshCount;sm++)
                {
                    var tris=c.Shoulder.GetTriangles(sm);
                    for(int i=0;i+2<tris.Length;i+=3)
                    {
                        Vector3 p0=w[tris[i]],p1=w[tris[i+1]],p2=w[tris[i+2]];float edge=Mathf.Max((p1-p0).magnitude,Mathf.Max((p2-p1).magnitude,(p0-p2).magnitude));
                        int n=Mathf.Clamp(Mathf.CeilToInt(edge/(cell*.7f)),1,24);
                        for(int u=0;u<=n;u++)for(int v=0;v<=n-u;v++){float a=u/(float)n,b2=v/(float)n;Add(p0+(p1-p0)*a+(p2-p0)*b2);}
                    }
                }
            }
            if(any)foreach(var c in tacticalCuts)
            {
                if(c.Skin==null||c.Shoulder==null||!c.Shown||!TacticalIsSkin(c.Skin))continue;
                var w=World(c);var under=new bool[w.Count];float gap2=TacticalSkinGap*TacticalSkinGap;
                for(int i=0;i<w.Count;i++)
                {
                    var k=Key(w[i]);
                    for(int dx=-1;dx<=1&&!under[i];dx++)for(int dy=-1;dy<=1&&!under[i];dy++)for(int dz=-1;dz<=1&&!under[i];dz++)
                        if(grid.TryGetValue(new Vector3Int(k.x+dx,k.y+dy,k.z+dz),out var l))foreach(var g in l)if((g-w[i]).sqrMagnitude<gap2){under[i]=true;break;}
                }
                var cut=Instantiate(c.Shoulder);cut.name=c.Source.name+" (arms, skin under garments hidden)";int kept=0;
                for(int sm=0;sm<cut.subMeshCount;sm++)
                {
                    var tris=c.Shoulder.GetTriangles(sm);var list=new System.Collections.Generic.List<int>(tris.Length);
                    for(int i=0;i+2<tris.Length;i+=3)if(!(under[tris[i]]&&under[tris[i+1]]&&under[tris[i+2]])){list.Add(tris[i]);list.Add(tris[i+1]);list.Add(tris[i+2]);}
                    cut.SetTriangles(list,sm,false);kept+=list.Count;
                }
                cut.RecalculateBounds();
                if(kept>0){c.Shoulder=cut;c.Owned=true;}else{Destroy(cut);c.Shoulder=null;}
            }
            Destroy(baked);
        }
        private static bool TacticalIsSkin(SkinnedMeshRenderer r)=>r.name.StartsWith("Body",System.StringComparison.Ordinal);

        // ---- First person only: the forearms and upper arms a little slimmer (their twist bones' girth, never their length),
        // so the pilot's sleeves sit at the pack's reach as its own slimmer arms do (owner 2026-11-21: the sleeves must not fill
        // the middle of the view). The twist bones are leaves: nothing else rides them. Put back by Rest every frame.
        private const float TacticalForearmGirth=.82f,TacticalUpperGirth=.9f;
        private static void TacticalSlim(TacticalArms arms)
        {
            for(int side=0;side<2;side++)for(int i=0;i<4;i++)
            {
                var t=i<2?arms.Twist[side][i]:arms.UpperTwist[side][i-2];if(t==null)continue;
                Vector3 along=i<2?arms.Hand[side].position-arms.Lower[side].position:arms.Lower[side].position-arms.Upper[side].position;if(along.sqrMagnitude<1e-8f)continue;
                along=Quaternion.Inverse(t.rotation)*along.normalized;float g=i<2?TacticalForearmGirth:TacticalUpperGirth;
                float ax=Mathf.Abs(along.x),ay=Mathf.Abs(along.y),az=Mathf.Abs(along.z);
                var rest=i<2?arms.TwistScale[side][i]:arms.UpperTwistScale[side][i-2];
                t.localScale=Vector3.Scale(rest,ax>=ay&&ax>=az?new Vector3(1,g,g):ay>=az?new Vector3(g,1,g):new Vector3(g,g,1));
            }
        }

        // ---- The view model's fill light (owner 2026-11-21: both hands and the sleeves must read on dark stages, as the gun
        // does): one directional light from over the eye that lights the view-model layer alone, no shadows, while the tactical
        // view draws.
        private Light tacticalFill;
        private void TacticalFill(bool on)
        {
            if(!on){if(tacticalFill!=null&&tacticalFill.enabled)tacticalFill.enabled=false;return;}
            if(tacticalFill==null)
            {
                var go=new GameObject("Tactical view fill");go.transform.SetParent(transform,false);tacticalFill=go.AddComponent<Light>();
                tacticalFill.type=LightType.Directional;tacticalFill.cullingMask=1<<10;tacticalFill.shadows=LightShadows.None;tacticalFill.renderMode=LightRenderMode.ForcePixel;
                tacticalFill.color=new Color(1,.97f,.93f);tacticalFill.intensity=TacticalFillIntensity;
            }
            if(!tacticalFill.enabled)tacticalFill.enabled=true;
            if(ViewCamera!=null)tacticalFill.transform.rotation=ViewCamera.transform.rotation*Quaternion.Euler(28,-18,0);
        }
        private const float TacticalFillIntensity=.55f;

        // A cut swapped in with the skin's blend-shape weights kept (the outfit's shapes tuck the skin under its garments; a swap
        // can reset them, and the skin showed through the sleeves).
        private static float[] tacticalShapes=new float[0];
        private static void TacticalSwapMesh(SkinnedMeshRenderer skin,Mesh mesh)
        {
            var was=skin.sharedMesh;int n=was!=null?Mathf.Min(was.blendShapeCount,mesh.blendShapeCount):0;
            if(tacticalShapes.Length<n)tacticalShapes=new float[n];for(int i=0;i<n;i++)tacticalShapes[i]=skin.GetBlendShapeWeight(i);
            skin.sharedMesh=mesh;for(int i=0;i<n;i++)skin.SetBlendShapeWeight(i,tacticalShapes[i]);
        }

        /// <summary>RenderFirstPerson, once the view body is posed (VisualViewPose): the tactical view on it, or false (the old view
        /// model stands).</summary>
        private bool TacticalViewPose(Actor a,float time)
        {
            // (The old view model keeps its elbow cut: the body it places by the right hand alone raised whole upper arms into the
            // air, tried 2026-11-21 on the greatsword.)
            bool on=TacticalViewPosed(a,time);TacticalArmCut(on);TacticalFill(on);if(!on||tacticalPackFrame!=Time.frameCount)TacticalGunsShown(null);
            return on;
        }
        private bool TacticalViewPosed(Actor a,float time)
        {
            if(tacticalArms!=null&&tacticalArms.Body==viewBody)tacticalArms.Rest();
            if(ScopeActive){tacticalScopedFrame=Time.frameCount;return false;}
            var bank=TacticalBank;var g=NativeTacticalGun.For(a.Weapon);if(bank==null||g==null||!bank.Covers(g)||ViewCamera==null||viewRig==null)return false;
            if(!VisualViewDrawn(viewRig))return false;bool pack=TacticalPackReady(bank,g);if(!pack&&viewBody.Held==null)return false;
            string spec=HeldSpec(a);if(spec!="pistol"&&spec!="rifle"&&spec!="machinegun")return false;
            if(NativeBowWire.Is(a.SourceSpec)||a.SourceSpec==NativeBombs.Spec||a.SourceSpec=="siren"||chipView.Frame==Time.frameCount)return false;
            if(a.GrabOwner!=0&&time<a.GrabUntil||NativeSpecialPose.Down(a,time)||a.Swimming)return false;
            if(tacticalArms==null||tacticalArms.Body!=viewBody)tacticalArms=TacticalArmsOf(viewBody);var arms=tacticalArms;if(arms==null)return false;
            tacticalView??=new NativeTacticalView(bank);
            var root=viewBody.Root.transform;var held=viewBody.Held;
            // Calibration: the body's right hand bone in its weapon frame as its grasp holds the model (VisualViewPose's Apply, this
            // frame), taken again once the pilot is calm (not shooting, reloading, sprinting, rolling or airborne).
            bool calm=time-a.LastShot>.35f&&!NativeTacticalTiming.Reloading(time,a.ReloadAt,a.ReloadUntil)&&!a.Sprinting&&a.Grounded&&!a.Rolling(time);
            if(pack){if(!tacticalCal.Set||!tacticalCalPack||tacticalCalGun!=a.Weapon){tacticalCal=tacticalView.CalibratePack(g);tacticalCalGun=a.Weapon;tacticalCalPack=true;tacticalCalBody=viewBody;}}
            else if(!tacticalCal.Set||tacticalCalPack||tacticalCalBody!=viewBody||tacticalCalHeld!=held||tacticalCalGun!=a.Weapon||calm&&!tacticalCalCalm)
            {
                var pose=viewRig.Pose;viewBody.Binding.WeaponFrame(pose,out var rp,out var rq);
                Vector3 hp=root.InverseTransformPoint(arms.Hand[1].position);Quaternion hq=Quaternion.Inverse(root.rotation)*arms.Hand[1].rotation;
                var iq=Quaternion.Inverse(rq);
                tacticalCal=tacticalView.Calibrate(g,CapturedAnimation.Num(iq*(hp-rp)),CapturedAnimation.Num(iq*hq),CapturedAnimation.Num(TacticalSight(a,held)),held.Supported?CapturedAnimation.Num(held.Support):default);
                tacticalCalBody=viewBody;tacticalCalHeld=held;tacticalCalGun=a.Weapon;tacticalCalCalm=calm;tacticalCalPack=false;
            }
            bool fresh=tacticalFrame<Time.frameCount-1&&tacticalScopedFrame<Time.frameCount-1;
            var input=new NativeTacticalInput{Time=time,Weapon=a.Weapon,LastShot=a.LastShot,ShootReady=a.ShootReady,ReloadAt=a.ReloadAt,ReloadUntil=a.ReloadUntil,Aim=AimHeld,
                Sprinting=a.Sprinting,Grounded=a.Grounded,Blocked=a.Rolling(time)||time<a.StunUntil,Walk=Mathf.Clamp01(viewSway.Motion/TacticalWalkMotion),Sprint=viewSway.Sprint,
                Stride=viewSway.Stride/(2*Mathf.PI),SwayX=viewSway.SwayX,SwayY=viewSway.SwayY,SwayRoll=viewSway.SwayRoll,Hop=viewSway.Hop,Land=viewSway.Land,Shake=CameraShake?viewShake:0,Fresh=fresh};
            var s=tacticalSolve;if(!tacticalView.Solve(input,tacticalCal,s))return false;
            // The eye: the camera without the clips' own motion (TacticalCameraKick turned it), as the pack's arms are on the body.
            var cam=ViewCamera.transform;Quaternion kick=tacticalKickFrame==Time.frameCount?tacticalKick:Quaternion.identity;
            Vector3 eye=cam.position;Quaternion eyeRot=cam.rotation*Quaternion.Inverse(kick);
            Vector3 W(System.Numerics.Vector3 v)=>eye+eyeRot*CapturedAnimation.Vec(v);Quaternion R(System.Numerics.Quaternion q)=>eyeRot*CapturedAnimation.Quat(q);
            // What rides the left hand (its item frame, VisualViewPose's) and each wrist's roll as the grasp left it.
            var lh=viewRig.Bones[CapturedRig.LHand];Matrix4x4 leftItem=arms.Hand[0].worldToLocalMatrix*lh.localToWorldMatrix;
            float twistL=TacticalTwist(arms.Lower[0],arms.Hand[0]),twistR=TacticalTwist(arms.Lower[1],arms.Hand[1]);
            Quaternion palmInHandL=Quaternion.Inverse(arms.Hand[0].rotation)*TacticalPalm(arms,0),palmInHandR=Quaternion.Inverse(arms.Hand[1].rotation)*TacticalPalm(arms,1);
            // The body at true size, turned with the eye, its shoulders' midpoint on the clip's.
            var parent=root.parent;float ps=parent!=null?parent.lossyScale.x:1;root.localScale=Vector3.one/Mathf.Max(1e-4f,ps);root.rotation=eyeRot;
            Vector3 ours=(arms.Upper[0].position+arms.Upper[1].position)*.5f,clip=(W(s.LeftShoulder)+W(s.RightShoulder))*.5f;root.position+=clip-ours;
            // The arms: the right hand on the model's grip (its grasp's frame, carried by the clip), the left on the clip's place and palm;
            // on the pack gun both on the clip's places and palms.
            Vector3 rt=W(s.Right),lt=W(s.Left);Quaternion rr=pack?R(s.RightPalm)*Quaternion.Inverse(palmInHandR):R(s.RightTurn),lr=R(s.LeftPalm)*Quaternion.Inverse(palmInHandL);
            TacticalReach(arms.Upper[1],arms.Lower[1],arms.Hand[1],rt,W(s.RightElbow));arms.Hand[1].SetPositionAndRotation(rt,rr);
            TacticalReach(arms.Upper[0],arms.Lower[0],arms.Hand[0],lt,W(s.LeftElbow));arms.Hand[0].SetPositionAndRotation(lt,lr);
            // The wrists' new roll, shared along the forearms' twist bones as the grasp shared it.
            for(int side=0;side<2;side++)
            {
                float delta=TacticalTwist(arms.Lower[side],arms.Hand[side])-(side==0?twistL:twistR);if(Mathf.Abs(delta)<1e-4f)continue;
                Vector3 axis=(arms.Hand[side].position-arms.Lower[side].position).normalized;
                for(int i=0;i<2;i++){var t=arms.Twist[side][i];if(t!=null&&arms.Share[side][i]>0)t.rotation=Quaternion.AngleAxis(delta*arms.Share[side][i]*Mathf.Rad2Deg,axis)*t.rotation;}
                // The hand keeps its target turn (the twist bones are its siblings, not its parents).
            }
            TacticalSlim(arms);
            // The fingers: each segment turned onto the clip's direction in the palm, by the clip's weight over the grasp's.
            for(int side=0;side<2;side++)
            {
                float w=side==0?s.LeftFingerWeight:s.RightFingerWeight;if(w<=0)continue;var dirs=side==0?s.LeftFingers:s.RightFingers;
                for(int d=0;d<5;d++)for(int k=0;k<3;k++)
                {
                    var palm=TacticalPalm(arms,side);var b=arms.Fingers[side][d*3+k];
                    Vector3 now=b.rotation*arms.Axis[side][d*3+k],want=palm*CapturedAnimation.Vec(dirs[d*3+k]);
                    b.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(now,want),w)*b.rotation;
                }
            }
            // The view rig's hands: the right on the model's weapon frame at true size, fitted (the effects' frame); the left's item
            // back on the new left hand. The pack gun: drawn on the clip's gun bone, its parts on its own clip; the right hand the
            // Standard gun's frame with its muzzle on the pack gun's, at the pack gun's share of the Standard's reach.
            var rh=viewRig.Bones[CapturedRig.RHand];
            if(pack)
            {
                var v=TacticalGunOf(bank,g);var model=bank.Gun(g.PackGun);
                if(v!=null)
                {
                    v.Root.SetPositionAndRotation(W(s.Weapon),R(s.WeaponTurn));float gs=v.Root.parent!=null?v.Root.parent.lossyScale.x:1;v.Root.localScale=Vector3.one/Mathf.Max(1e-4f,gs);
                    TacticalGunPose(v,bank,s.WeaponClip,s.WeaponTime);TacticalGunsShown(v);tacticalPackFrame=Time.frameCount;
                    // Where each hand bone should be on the gun (its frame), read back before the frame renders by a local probe.
                    TacticalAnchor.Gun=v.Root;TacticalAnchor.HandR=arms.Hand[1];TacticalAnchor.HandL=arms.Hand[0];
                    TacticalAnchor.Right=v.Root.InverseTransformPoint(rt);TacticalAnchor.Left=v.Root.InverseTransformPoint(lt);
                    TacticalAnchor.State=s.Action+"/"+s.WeaponClip;TacticalAnchor.Frame=Time.frameCount;
                }
                float mz=NativeWeaponDressing.MuzzleZ(spec),k=tacticalView.PackScale(g,mz);Quaternion gr=R(s.WeaponTurn);
                rh.rotation=gr;rh.localScale=viewBody.RightScale;float ks=rh.lossyScale.x;if(ks>1e-5f)rh.localScale=viewBody.RightScale*(k/ks);
                rh.position=W(s.Weapon)+gr*(CapturedAnimation.Vec(model.Muzzle)-new Vector3(0,0,mz*k));
            }
            else{rh.SetPositionAndRotation(W(s.Weapon),R(s.WeaponTurn));rh.localScale=viewBody.RightScale;float rs=rh.lossyScale.x;if(rs>1e-5f)rh.localScale=viewBody.RightScale/rs;VisualWeaponFit(rh,held);}
            var lm=arms.Hand[0].localToWorldMatrix*leftItem;lh.SetPositionAndRotation(lm.GetColumn(3),lm.rotation);
            // The view model's lens: the pack's own framing (its first-person camera's 90 degrees, its aim's 70-80) instead of the
            // world's, eased with the aim; the sights stay on the axis whatever the lens.
            ViewCamera.fieldOfView=Mathf.Lerp(g.HipFov,g.AimFov,s.Ads);
            tacticalCamera=CapturedAnimation.Quat(s.Camera);tacticalFrame=Time.frameCount;
            return true;
        }
        // The model's rear sight in its weapon frame at true size: the bake's Sight, else the gun's ModelSight, else the Standard
        // gun's through the fit.
        private static Vector3 TacticalSight(Actor a,VisualWeapon w)
        {
            float y=a.Weapon==WeaponId.Pistol?.105f:a.Weapon==WeaponId.Rifle?.175f:.25f,z=a.Weapon==WeaponId.Pistol?-.03f:a.Weapon==WeaponId.Rifle?-.075f:-.02f;
            var rear=new Vector3(0,y,z);if(w==null)return rear;if(w.Sighted)return w.Sight;
            var own=NativeTacticalGun.For(a.Weapon)?.ModelSight??System.Numerics.Vector3.Zero;if(own!=System.Numerics.Vector3.Zero)return CapturedAnimation.Vec(own);
            return w.Scale!=1?w.Centre+w.Scale*(rear-w.Centre):rear;
        }
        /// <summary>The clips' own camera motion (a reload's look down at the magazine, the bolt's nod), first person with camera shake
        /// on, applied after the aim point like HitKick (the view is rebuilt from the look angles every frame, so it never reaches aim);
        /// `scale` is the setting's share. The arms stay on the body: TacticalViewPose places them in the camera without it.</summary>
        public void TacticalCameraKick(Transform view,float scale)
        {
            tacticalKickFrame=-1;if(view==null||!(scale>0)||!FirstPerson||tacticalFrame<Time.frameCount-2)return;
            var q=Quaternion.Slerp(Quaternion.identity,tacticalCamera,Mathf.Clamp01(scale));if(Quaternion.Angle(q,Quaternion.identity)<.01f)return;
            view.rotation*=q;tacticalKick=q;tacticalKickFrame=Time.frameCount;
        }
    }
}
