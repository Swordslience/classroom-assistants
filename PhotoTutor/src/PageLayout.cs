using System;
using System.Windows;
namespace PhotoTutor {
 internal static class PageLayout {
  internal static Int32Rect BackToOriginal(Int32Rect box,int width,int height,int clockwise,int originalWidth,int originalHeight){int x=box.X,y=box.Y,w=box.Width,h=box.Height;switch(clockwise){case 90:x=box.Y;y=height-box.X-box.Width;w=box.Height;h=box.Width;break;case 180:x=width-box.X-box.Width;y=height-box.Y-box.Height;break;case 270:x=width-box.Y-box.Height;y=box.X;w=box.Height;h=box.Width;break;}int left=Math.Max(0,(int)Math.Floor(x*originalWidth/(double)width)),top=Math.Max(0,(int)Math.Floor(y*originalHeight/(double)height));int right=Math.Min(originalWidth,(int)Math.Ceiling((x+w)*originalWidth/(double)width)),bottom=Math.Min(originalHeight,(int)Math.Ceiling((y+h)*originalHeight/(double)height));return new Int32Rect(left,top,Math.Max(0,right-left),Math.Max(0,bottom-top));}
 }
}
