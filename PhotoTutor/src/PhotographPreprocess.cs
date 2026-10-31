using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoTutor {
 internal static class PhotographPreprocess {
  // Local illumination normalization for photographed paper. Analysis-only; source pixels are never replaced.
  internal static BitmapSource Normalize(BitmapSource source){int w=source.PixelWidth,h=source.PixelHeight,stride=w+1;var gray=new FormatConvertedBitmap(source,PixelFormats.Gray8,null,0);var pixels=new byte[w*h];gray.CopyPixels(pixels,w,0);var integral=new long[checked((w+1)*(h+1))];for(int y=0;y<h;y++){long row=0;for(int x=0;x<w;x++){row+=pixels[y*w+x];integral[(y+1)*stride+x+1]=integral[y*stride+x+1]+row;}}
   int radius=Math.Max(9,Math.Min(27,Math.Min(w,h)/35));var output=new byte[w*h];for(int y=0;y<h;y++){int top=Math.Max(0,y-radius),bottom=Math.Min(h,y+radius+1);for(int x=0;x<w;x++){int left=Math.Max(0,x-radius),right=Math.Min(w,x+radius+1);double mean=(integral[bottom*stride+right]-integral[top*stride+right]-integral[bottom*stride+left]+integral[top*stride+left])/(double)((right-left)*(bottom-top));double difference=mean-pixels[y*w+x];output[y*w+x]=(byte)Math.Max(0,Math.Min(255,242-difference*5.5));}}
   var result=BitmapSource.Create(w,h,96,96,PixelFormats.Gray8,null,output,w);result.Freeze();return result;
  }
 }
}
