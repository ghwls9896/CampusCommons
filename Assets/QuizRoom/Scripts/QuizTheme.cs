using System.Collections.Generic;
using UnityEngine;

namespace CampusCommons.Quiz
{
    // Local IMGUI theme; shared scene and global GUI skin are never modified.
    internal sealed class QuizTheme
    {
        internal static readonly Color Ink = new Color(.10f,.15f,.23f);
        internal static readonly Color Muted = new Color(.43f,.48f,.55f);
        internal static readonly Color Navy = new Color(.15f,.24f,.37f);
        internal static readonly Color Green = new Color(.12f,.43f,.34f);
        internal readonly GUIStyle Title, Heading, Body, Small, Caption, FileName;
        internal readonly GUIStyle Panel, Card, WhiteCard, ErrorCard, SuccessCard, Shadow;
        internal readonly GUIStyle Primary, Secondary, Quiet, Choice, SelectedChoice, CorrectChoice, WrongChoice;
        internal readonly GUIStyle Step, ActiveStep, Warning, Positive;
        internal readonly GUISkin Skin;
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly Texture2D dot;

        internal QuizTheme()
        {
            Skin=Object.Instantiate(GUI.skin); Skin.hideFlags=HideFlags.HideAndDontSave;
            Skin.verticalScrollbar=new GUIStyle {fixedWidth=6,margin=new RectOffset(10,0,0,0)};
            Skin.verticalScrollbarThumb=Box(new Color(.79f,.82f,.86f),3,0);
            Skin.verticalScrollbarUpButton=new GUIStyle {fixedHeight=0};
            Skin.verticalScrollbarDownButton=new GUIStyle {fixedHeight=0};
            var font = Resources.Load<Font>("KIT/NotoSansKR");
            Title = Label(font,24,Ink,true); Heading = Label(font,32,Ink,true);
            Body = Label(font,18,Ink); Small = Label(font,15,Muted); Caption = Label(font,13,Muted,true);
            FileName = Label(font,21,Ink,true);
            Panel = Box(Color.white,22,0); Shadow = Box(new Color(0,0,0,.11f),22,0);
            Card = Box(new Color(.96f,.97f,.98f),14,24);
            WhiteCard = Box(Color.white,14,22);
            ErrorCard = Box(new Color(1,.94f,.93f),12,18);
            SuccessCard = Box(new Color(.92f,.97f,.95f),12,20);
            Primary = Button(font,Navy,Color.white,new Color(.20f,.31f,.46f));
            Secondary = Button(font,new Color(.92f,.94f,.96f),Ink,new Color(.87f,.90f,.93f));
            Quiet = Button(font,Color.white,Muted,new Color(.96f,.97f,.98f));
            Choice = Button(font,new Color(.96f,.97f,.98f),Ink,new Color(.91f,.94f,.97f));
            SelectedChoice = Button(font,new Color(.88f,.92f,.97f),Navy,new Color(.84f,.89f,.95f));
            CorrectChoice = Button(font,new Color(.88f,.95f,.91f),Green,new Color(.88f,.95f,.91f));
            WrongChoice = Button(font,new Color(1,.91f,.90f),new Color(.66f,.21f,.18f),new Color(1,.91f,.90f));
            foreach(var s in new[]{Choice,SelectedChoice,CorrectChoice,WrongChoice}) {
                s.alignment=TextAnchor.MiddleLeft; s.padding=new RectOffset(20,20,16,16); s.fontSize=18;
            }
            Step = Label(font,14,Muted,true); ActiveStep=Label(font,14,Navy,true);
            Warning=Label(font,16,new Color(.66f,.21f,.18f)); Positive=Label(font,17,Green,true);
            dot=Rounded(Color.white,16);
        }
        static GUIStyle Label(Font font,int size,Color color,bool bold=false)
        {
            var style=new GUIStyle(GUI.skin.label) {font=font,fontSize=size,wordWrap=true,richText=false,
                fontStyle=bold?FontStyle.Bold:FontStyle.Normal,padding=new RectOffset(0,0,0,0)};
            foreach(var state in new[]{style.normal,style.hover,style.active,style.focused,style.onNormal,style.onHover,style.onActive,style.onFocused})
                state.textColor=color;
            return style;
        }
        GUIStyle Box(Color color,int radius,int padding)
        {
            return new GUIStyle {normal={background=Rounded(color,radius)},border=new RectOffset(radius,radius,radius,radius),
                padding=new RectOffset(padding,padding,padding,padding),margin=new RectOffset(0,0,0,0)};
        }
        GUIStyle Button(Font font,Color color,Color text,Color hover)
        {
            var normal=Rounded(color,10); var over=Rounded(hover,10);
            return new GUIStyle {font=font,fontSize=17,fontStyle=FontStyle.Bold,wordWrap=true,richText=false,
                alignment=TextAnchor.MiddleCenter,border=new RectOffset(10,10,10,10),
                padding=new RectOffset(22,22,14,14),margin=new RectOffset(0,0,0,0),
                normal={background=normal,textColor=text},hover={background=over,textColor=text},
                active={background=over,textColor=text},focused={background=over,textColor=text},
                onNormal={background=normal,textColor=text},onHover={background=over,textColor=text},
                onActive={background=over,textColor=text},onFocused={background=over,textColor=text}};
        }
        Texture2D Rounded(Color color,int radius)
        {
            int size=radius*2+4;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false) {hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++) {
                float dx=Mathf.Max(radius-x-.5f,x+.5f-(size-radius));
                float dy=Mathf.Max(radius-y-.5f,y+.5f-(size-radius));
                float distance=Mathf.Sqrt(Mathf.Pow(Mathf.Max(dx,0),2)+Mathf.Pow(Mathf.Max(dy,0),2));
                var c=color; c.a*=Mathf.Clamp01(radius-distance+.5f); pixels[y*size+x]=c;
            }
            texture.SetPixels(pixels);texture.Apply(false,true);textures.Add(texture);return texture;
        }
        internal void Spinner(Rect rect,float time)
        {
            var before=GUI.color;
            for(int i=0;i<12;i++) {
                float angle=(i*30-90)*Mathf.Deg2Rad;
                float alpha=.18f+.82f*Mathf.Repeat((time*1.6f-i/12f),1);
                GUI.color=new Color(Navy.r,Navy.g,Navy.b,alpha);
                GUI.DrawTexture(new Rect(rect.center.x+Mathf.Cos(angle)*19-3,rect.center.y+Mathf.Sin(angle)*19-3,6,6),dot);
            }
            GUI.color=before;
        }
        internal void Dispose() {foreach(var texture in textures) if(texture) Object.Destroy(texture);textures.Clear();if(Skin) Object.Destroy(Skin);}
    }
}
