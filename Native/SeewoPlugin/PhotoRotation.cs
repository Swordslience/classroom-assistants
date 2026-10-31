using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClassroomAI.Seewo {
 public static class PhotoRotation {
  public static int Normalize(int quarterTurns) { return ((quarterTurns % 4) + 4) % 4; }
  // Always transform the original decoded bitmap, avoiding cumulative rotation
  // and re-encoding loss. Rotating the view does not call or restart the model.
  public static BitmapSource Turn(BitmapSource original,int quarterTurns) {
   if(original==null)throw new ArgumentNullException("original");
   int turns=Normalize(quarterTurns);if(turns==0)return original;
   var result=new TransformedBitmap(original,new RotateTransform(turns*90));result.Freeze();return result;
  }
 }
}
