using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace PhotoTutor {
 internal static class PhotoOrientation {
  internal static int Read(BitmapFrame frame){try{var data=frame.Metadata as BitmapMetadata;return data!=null&&data.ContainsQuery("/app1/ifd/{ushort=274}")?Convert.ToInt32(data.GetQuery("/app1/ifd/{ushort=274}")):1;}catch(NotSupportedException){return 1;}catch(ArgumentException){return 1;}}
  internal static BitmapSource Canonical(BitmapSource source,int orientation){var transforms=new TransformGroup();if(orientation==2||orientation==4||orientation==5||orientation==7)transforms.Children.Add(new ScaleTransform(-1,1));int angle=orientation==3||orientation==4?180:orientation==5||orientation==8?270:orientation==6||orientation==7?90:0;if(angle!=0)transforms.Children.Add(new RotateTransform(angle));if(transforms.Children.Count==0)return source;var result=new TransformedBitmap(source,transforms);result.Freeze();return result;}
 }
}
