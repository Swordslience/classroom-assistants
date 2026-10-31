using System.Globalization;
using System.Windows;
using System.Windows.Media;
namespace ClassroomAI.Seewo;
internal static class Brand {
 internal static ImageSource Icon(){var group=new DrawingGroup();using(var dc=group.Open()){
  dc.DrawRoundedRectangle(Brushes.Gold,null,new Rect(0,0,64,64),14,14);
  var text=new FormattedText("龙",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),43,Brushes.Black,1);
  dc.DrawText(text,new Point((64-text.Width)/2,(64-text.Height)/2));
 }group.Freeze();var result=new DrawingImage(group);result.Freeze();return result;}
}
