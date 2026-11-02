using UnityEngine;
namespace ChipArena.UnityConversion
{
    // IMGUI receives genuine Layout/Repaint passes. Controller activation is an extra local hit test,
    // never an OS mouse event or a replacement Repaint (which would blank the menu during scrolling).
    public static class NativeControllerPointer
    {
        public static bool Active;
        public static readonly NativeMenuNavigation Navigation=new NativeMenuNavigation();
        public static bool Navigating;private static int sliderStep,sliderFrame=-1;
        private static Vector2 press,point,screenOrigin;
        private static bool pressed;
        private static int released=-1,consumed=-1,pressedFocus=-1,releasedFocus=-1;
        private static Rect? clip;
        public static void Update(Vector2 at,bool down,bool up)
        {
            point=at;if(down){press=at;pressed=true;pressedFocus=Navigation.Focus;}
            if(up){if(pressed){released=Time.frameCount;releasedFocus=pressedFocus;}pressed=false;}
        }
        public static void Cancel(){pressed=false;released=-1;pressedFocus=releasedFocus=-1;Active=false;clip=null;}
        public static void ResetNavigation(){Navigation.Reset();Navigating=false;Cancel();}
        public static Vector2 Navigate(float x,float y,bool confirm,float now,out float scroll)
        {
            int direction=Navigation.Pulse(x,y,now);scroll=0;
            if(direction!=0||confirm){Navigating=true;sliderStep=direction==0?0:Navigation.Move(direction);sliderFrame=Time.frameCount;Navigation.EnsureFocus();scroll=Navigation.ScrollNeeded();}
            return Navigation.Current(out var item)?new Vector2(item.CX,item.CY):point;
        }
        public static void BeginGui()
        {
            if(Event.current.type==EventType.Repaint)Navigation.BeginFrame();
            var matrix=GUI.matrix;GUI.matrix=Matrix4x4.identity;
            screenOrigin=GUIUtility.GUIToScreenPoint(Vector2.zero);GUI.matrix=matrix;
        }
        static Rect ScreenRect(Rect rect)
        {
            var a=GUIUtility.GUIToScreenPoint(rect.min);var b=GUIUtility.GUIToScreenPoint(rect.max);
            // GUIToScreenPoint includes the Editor game-view window origin; subtract that origin.
            return Rect.MinMaxRect(a.x-screenOrigin.x,a.y-screenOrigin.y,b.x-screenOrigin.x,b.y-screenOrigin.y);
        }
        public static void SetClip(Rect rect){clip=ScreenRect(rect);}
        public static void ClearClip(){clip=null;}
        private static int Register(Rect rect,bool slider)
        {
            if(!GUI.enabled||Event.current.type!=EventType.Repaint)return -1;
            var s=ScreenRect(rect);return Navigation.Add(new NativeMenuNavigation.Item{X=s.x,Y=s.y,W=s.width,H=s.height,Slider=slider,Clipped=clip.HasValue,ClipY=clip?.y??0,ClipH=clip?.height??0});
        }
        public static bool Click(Rect rect)
        {
            int index=Register(rect,false);
            if(!Active||!GUI.enabled||Event.current.type!=EventType.Repaint||released!=Time.frameCount||consumed==Time.frameCount)return false;
            var screen=ScreenRect(rect);
            if(Navigating)
            {if(index<0||index!=Navigation.Focus||index!=releasedFocus||clip.HasValue&&!clip.Value.Contains(screen.center))return false;}
            else if(clip.HasValue&&!clip.Value.Contains(point)||!screen.Contains(point)||!screen.Contains(press))return false;
            consumed=Time.frameCount;GUIUtility.keyboardControl=0;return true;
        }
        public static bool Button(Rect rect,string text)=>GUI.Button(rect,text)||Click(rect);
        public static bool Button(string text,GUIStyle style,params GUILayoutOption[] options)
        {bool hit=GUILayout.Button(text,style,options);return hit||Click(GUILayoutUtility.GetLastRect());}
        public static bool Toggle(bool value,string text,params GUILayoutOption[] options)=>Toggle(value,text,GUI.skin.toggle,options);
        public static bool Toggle(bool value,string text,GUIStyle style,params GUILayoutOption[] options)
        {bool result=GUILayout.Toggle(value,text,style,options);return Click(GUILayoutUtility.GetLastRect())?!result:result;}
        public static float Slider(float value,float min,float max)
        {
            float result=GUILayout.HorizontalSlider(value,min,max);var rect=GUILayoutUtility.GetLastRect();
            int index=Register(rect,true);
            if(Active&&Navigating&&index>=0&&index==Navigation.Focus&&sliderFrame==Time.frameCount&&sliderStep!=0)
            {result=Mathf.Clamp(result+sliderStep*(max-min)*.05f,min,max);sliderStep=0;}
            if(Active&&!Navigating&&GUI.enabled&&Event.current.type==EventType.Repaint&&(pressed||released==Time.frameCount)&&(clip==null||clip.Value.Contains(point)))
            {
                var screen=ScreenRect(rect);if(screen.Contains(press)&&screen.width>0){result=Mathf.Lerp(min,max,Mathf.Clamp01((point.x-screen.x)/screen.width));consumed=Time.frameCount;}
            }
            return result;
        }
    }
}
