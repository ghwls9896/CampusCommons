using UnityEngine;
namespace CampusCommons.Webcam
{
    public static class WebcamFrameEncoder
    {
        // Normalizes device rotation/mirroring and limits the long edge to 512 pixels.
        public static byte[] Encode(WebCamTexture camera)
        {
            int sw=camera.width, sh=camera.height, angle=((camera.videoRotationAngle%360)+360)%360;
            bool swap=angle==90||angle==270;
            int rw=swap?sh:sw,rh=swap?sw:sh;
            float scale=Mathf.Min(1f,512f/Mathf.Max(rw,rh));
            int w=Mathf.Max(1,Mathf.RoundToInt(rw*scale)),h=Mathf.Max(1,Mathf.RoundToInt(rh*scale));
            var src=camera.GetPixels32();var dst=new Color32[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                int u=Mathf.Min(rw-1,x*rw/w),v=Mathf.Min(rh-1,y*rh/h),sx=u,sy=v;
                if(angle==90){sx=sw-1-v;sy=u;}
                else if(angle==180){sx=sw-1-u;sy=sh-1-v;}
                else if(angle==270){sx=v;sy=sh-1-u;}
                if(camera.videoVerticallyMirrored)sy=sh-1-sy;
                dst[y*w+x]=src[sy*sw+sx];
            }
            var image=new Texture2D(w,h,TextureFormat.RGB24,false);
            try{image.SetPixels32(dst);image.Apply();return image.EncodeToJPG(85);}
            finally{Object.Destroy(image);}
        }
    }
}
