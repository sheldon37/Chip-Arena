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
        // camera looked through the scope.
        private bool rifleScopeKey,rifleScopeWas;
        /// <summary>The camera looks through the rifle's scope this frame: the local pilot's own aim (no spectated pilot) on held Q.</summary>
        private bool RifleScopeView(NativePilot p,bool aim)=>Spectator==null&&NativeViewRules.ScopeView(p.Seat.spec,rifleScopeKey,aim);
        // Out of the scope into a third-person camera, the chosen camera's field of view comes back with the cut (no zoomed frames
        // over the shoulder as it eases out); into the scope it still eases in, the sight closing on the target.
        private void ScopeOutFov(bool scope,bool first,float settled)
        {
            if(rifleScopeWas&&!scope&&!first)cameraView.fieldOfView=settled+sprintKick;rifleScopeWas=scope;
        }
        private Texture2D scopeMask;private GUIStyle scopeLabel;
        private void DrawScope(float width,float height)
        {
            if(scopeMask==null)
            {
                const int size=512;scopeMask=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Native rifle scope",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color32[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {float px=(x+.5f)/size-.5f,py=(y+.5f)/size-.5f,r=Mathf.Sqrt(px*px+py*py);pixels[y*size+x]=Mathf.Abs(r-.32f)<.001f?new Color32(213,236,224,153):new Color32(5,12,18,(byte)Mathf.RoundToInt(255*NativeAimPresentation.ScopeAlpha(r)));}
                scopeMask.SetPixels32(pixels);scopeMask.Apply(false,true);
            }
            Color previous=GUI.color;
            void Fill(Rect rect,Color color){GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);}
            float left=(width-height)*.5f,cx=width*.5f,cy=height*.5f,radius=height*.32f;
            GUI.color=Color.white;GUI.DrawTexture(new Rect(left,0,height,height),scopeMask);
            var shade=new Color(5/255f,12/255f,18/255f,.75f);
            if(left>0){Fill(new Rect(0,0,left,height),shade);Fill(new Rect(left+height,0,left,height),shade);}
            // Thin source-colour reticle inside the circular sight; keep the centre on the aim ray.
            var line=new Color(201/255f,229/255f,217/255f,.55f);
            Fill(new Rect(cx-radius,cy-.5f,radius*2,1),line);Fill(new Rect(cx-.5f,cy-radius,1,radius*2),line);
            Fill(new Rect(cx-1,cy-1,2,2),new Color(.91f,1,.95f));
            GUI.color=new Color(205/255f,231/255f,224/255f);if(scopeLabel==null)scopeLabel=new GUIStyle(small){alignment=TextAnchor.MiddleCenter};
            GUI.Label(new Rect(cx-160,cy+height*.23f,320,25),"3× SCOPE · RELEASE Q",scopeLabel);GUI.color=previous;
        }
    }
}
