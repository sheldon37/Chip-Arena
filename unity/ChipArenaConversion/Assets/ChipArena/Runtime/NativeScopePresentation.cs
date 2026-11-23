using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        // The rifle's Q scope (owner 2026-11-18: "fix rifle Q to actually use the scope still instead of zooming in (right click
        // working as intended)"). Since 2026-11-10 every gun aims in the chosen camera (third person: a little zoom, the tight
        // crosshair, the aim pose), and the rifle's held Q had become that zoom too. Held Q now looks through the scope from the eye
        // whichever camera is chosen, as it did before: the aim row's 28 degree view (3x), its look speed (AimLook), the scope mask
        // below with the reticle and the pilot's own body hidden, all from the first-person view's existing scope (ScopeActive).
        // Right click, the controller's Aim and the other guns are unchanged. The rules see the same held Aim either way.
        // rifleScopeKey: this frame's input holds the rifle's Q for its aim (ConversionGame's input); rifleScopeWas: the last
        // camera looked through the scope, rifleScopeFrame: in which frame.
        private bool rifleScopeKey,rifleScopeWas;private int rifleScopeFrame=-10;
        /// <summary>The camera looks through the rifle's scope this frame: the local pilot's own aim (no spectated pilot) on held Q.</summary>
        private bool RifleScopeView(NativePilot p,bool aim)=>Spectator==null&&NativeViewRules.ScopeView(p.Seat.spec,rifleScopeKey,aim);
        // The hand-over between a third-person camera and the scope, run once the camera is chosen and before it is placed. Seen in
        // the Editor (2026-11-18): a shoulder camera's crosshair ray runs beside the eye's (0.62 m right, 0.25 m up), so the reticle
        // opened that far from what the crosshair was on (a foe at 8 m some 110 px aside), and for four frames the view model rose
        // half drawn before the mask.
        // - Into the scope from a third-person camera: the sight is up at once (ArenaPresentation.ScopeIn); from a shoulder camera
        //   the eye turns onto the point under its crosshair (NativeAimConvergence.Angles: the look it was already sending), so the
        //   reticle opens on it. Classic sends the camera's own look, so it keeps it. First person chosen in Settings is unchanged.
        // - Out of it, straight from the scope, into a third-person camera: that camera's field of view comes back with the cut (no
        //   zoomed frames over the shoulder as it eases out), and a shoulder camera turns so its crosshair runs through the point under
        //   the reticle (NativeCameraRig.LookAt from the rig's shoulder point, as the lock-on frames).
        private void ScopeHandOver(NativePilot p,Vector3 focus,bool scope,bool first,float settled)
        {
            bool was=rifleScopeWas,straight=rifleScopeFrame==Time.frameCount-1;rifleScopeWas=scope;if(scope)rifleScopeFrame=Time.frameCount;
            if(scope==was||!scope&&(!straight||first))return;
            float range=Mathf.Max((float)p.Weapon.range,NativeAimConvergence.FarRange);
            if(scope)
            {
                if(prefs.firstPerson)return;presentation.ScopeIn();if(!convergeAim)return;
                var point=CrosshairPoint(range,out _);NativeAimConvergence.Angles(V(focus),V(point),yaw,pitch,out float y,out float up);
                yaw=y;pitch=Mathf.Clamp(-up,-70,70);pending.Yaw=yaw;pending.Pitch=-pitch;return;
            }
            cameraView.fieldOfView=settled+sprintKick;if(!shoulderView)return;
            var at=CrosshairPoint(range,out _);NativeCameraRig.LookAt(V(focus),V(at),cameraRig.PivotSide,lockLift,out float yawOut,out float pitchOut);
            yaw+=Mathf.DeltaAngle(yaw,yawOut);pitch=Mathf.Clamp(pitchOut,-70,70);
        }
        private Texture2D scopeMask,scopeMaskWide;private GUIStyle scopeLabel;
        // How much nearer the ring looks per metre the rifle kicks back toward the eye (scale 1 + back x this).
        private const float ScopeKickNear=6;
        // The scope mask: `spread` screen heights square, the ring the same size on screen (spread 3: the kicked scope's one quad that
        // covers the screen wherever the kick moves it, so no shade strips meet in seams under its turn).
        private static Texture2D ScopeMaskTexture(int size,float spread)
        {
            var t=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Native rifle scope",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {float px=(x+.5f)/size-.5f,py=(y+.5f)/size-.5f,r=Mathf.Sqrt(px*px+py*py)*spread;pixels[y*size+x]=Mathf.Abs(r-.32f)<.001f?new Color32(213,236,224,153):new Color32(5,12,18,(byte)Mathf.RoundToInt(255*NativeAimPresentation.ScopeAlpha(r)));}
            t.SetPixels32(pixels);t.Apply(false,true);return t;
        }
        private void DrawScope(float width,float height)
        {
            if(scopeMask==null)scopeMask=ScopeMaskTexture(512,1);
            Color previous=GUI.color;
            void Fill(Rect rect,Color color){GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);}
            float left=(width-height)*.5f,cx=width*.5f,cy=height*.5f,radius=height*.32f;
            // The shot's kick (owner 2026-11-23, aimed recoil; NativeAimKick.Rifle, the pack's SRM-12 data): the ring and reticle lift and
            // turn with the rifle and near the eye, then settle back on the aim ray before the next shot is allowed. The world camera stays.
            Matrix4x4 matrix=GUI.matrix;bool kicked=presentation.ScopeKick(out float kRise,out float kYaw,out float kRoll,out float kBack);
            if(kicked)
            {
                float perDegree=cameraView!=null?height*.5f/Mathf.Tan(cameraView.fieldOfView*.5f*Mathf.Deg2Rad)*Mathf.Deg2Rad:height*.035f,near=1+kBack*ScopeKickNear;
                GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(cx+kYaw*perDegree,cy-kRise*perDegree,0),Quaternion.Euler(0,0,-kRoll),new Vector3(near,near,1))*Matrix4x4.Translate(new Vector3(-cx,-cy,0));
            }
            GUI.color=Color.white;
            // Kicked, one mask three screen heights square covers the screen (up to 21:9) wherever the ring goes (strips beside it showed
            // seams once turned).
            if(kicked){if(scopeMaskWide==null)scopeMaskWide=ScopeMaskTexture(1024,3);GUI.DrawTexture(new Rect(cx-height*1.5f,cy-height*1.5f,height*3,height*3),scopeMaskWide);}
            else
            {
                GUI.DrawTexture(new Rect(left,0,height,height),scopeMask);
                var shade=new Color(5/255f,12/255f,18/255f,.75f);
                if(left>0){Fill(new Rect(0,0,left,height),shade);Fill(new Rect(left+height,0,left,height),shade);}
            }
            // Thin source-colour reticle inside the circular sight; keep the centre on the aim ray.
            var line=new Color(201/255f,229/255f,217/255f,.55f);
            Fill(new Rect(cx-radius,cy-.5f,radius*2,1),line);Fill(new Rect(cx-.5f,cy-radius,1,radius*2),line);
            Fill(new Rect(cx-1,cy-1,2,2),new Color(.91f,1,.95f));
            GUI.matrix=matrix;
            GUI.color=new Color(205/255f,231/255f,224/255f);if(scopeLabel==null)scopeLabel=new GUIStyle(small){alignment=TextAnchor.MiddleCenter};
            GUI.Label(new Rect(cx-160,cy+height*.23f,320,25),"3× SCOPE · RELEASE Q",scopeLabel);GUI.color=previous;
        }
    }
}
