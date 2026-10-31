using System;
using System.Drawing;
namespace PhotoTutor {
 internal static class ClassroomWindowPlacement {
  internal static Rectangle Calculate(Rectangle work,int dpi){double scale=Math.Max(1,dpi/96.0);int margin=Math.Min(60,Math.Max(16,(int)(18*scale)));int width=Math.Max(400,Math.Min(work.Width-2*margin,Math.Max((int)(1440*scale),(int)(work.Width*.86))));int height=Math.Max(300,Math.Min(work.Height-2*margin,Math.Min((int)(900*scale),(int)(work.Height*.96))));width=Math.Min(width,work.Width);height=Math.Min(height,work.Height);return new Rectangle(work.Left+(work.Width-width)/2,Math.Max(work.Top,work.Bottom-height-margin),width,height);}
 }
}
