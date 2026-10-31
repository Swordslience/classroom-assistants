using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoTutor {
 internal static class TextOrientation {
  // A small projection vote only prioritizes OCR orientations. It never creates question rectangles.
  internal static int[] Order(BitmapSource source){double scale=Math.Min(1,720d/Math.Max(source.PixelWidth,source.PixelHeight));BitmapSource small=scale<1?new TransformedBitmap(source,new ScaleTransform(scale,scale)):source;int w=small.PixelWidth,h=small.PixelHeight;var gray=new FormatConvertedBitmap(small,PixelFormats.Gray8,null,0);var pixels=new byte[w*h];gray.CopyPixels(pixels,w,0);double horizontal=0,vertical=0;
   for(int gy=0;gy<3;gy++)for(int gx=0;gx<3;gx++){int x0=gx*w/3+4,x1=(gx+1)*w/3-4,y0=gy*h/3+4,y1=(gy+1)*h/3-4;if(x1-x0<24||y1-y0<24)continue;int pw=x1-x0,ph=y1-y0;var rows=new double[ph];var columns=new double[pw];int count=0;for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)if(pixels[y*w+x]<155){rows[y-y0]++;columns[x-x0]++;count++;}double density=count/(double)(pw*ph);if(density<.012||density>.4)continue;
    double Energy(double[] values,int denominator){double sum=0,sumSquares=0;int n=values.Length-4;for(int i=2;i<values.Length-2;i++){double value=(values[i-2]+values[i-1]+values[i]+values[i+1]+values[i+2])/(5*denominator);sum+=value;sumSquares+=value*value;}return Math.Max(0,sumSquares/n-(sum/n)*(sum/n));}
    double r=Energy(rows,pw),c=Energy(columns,ph);if(r>c*1.25)horizontal+=Math.Log(1+r/Math.Max(c,1e-7));if(c>r*1.25)vertical+=Math.Log(1+c/Math.Max(r,1e-7));
   }
   return vertical>horizontal*1.2&&vertical>1?new[]{90,270,0,180}:new[]{0,180,90,270};
  }
 }
}
