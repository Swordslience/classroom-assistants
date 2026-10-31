using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ClassroomAI.Seewo;
internal static class TeachingVisualRenderer {
 static readonly Typeface Font=new("Microsoft YaHei");
 static Brush Color(string value)=>new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(value));
 static FormattedText Text(string value,double size,Brush color,double width)=>new(value,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,Font,size,color,1){MaxTextWidth=width};
 internal static BitmapSource Draw(VisualPlan plan,string subject){
  if(plan.Nodes.Count is <2 or >8)throw new InvalidOperationException("图解节点数量无效。");
  const int width=1920,height=1080;var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
   dc.DrawRectangle(Color("#F7F9F5"),null,new Rect(0,0,width,height));dc.DrawRoundedRectangle(Color("#0F5966"),null,new Rect(64,48,12,102),6,6);
   var title=Text(plan.Title,54,Color("#123943"),1740);if(title.Height>135)throw new InvalidOperationException("图解标题太长，请缩小讲解范围。");dc.DrawText(title,new Point(100,45));
   dc.DrawText(Text(plan.Purpose,27,Color("#52636B"),1740),new Point(100,130));
   int count=plan.Nodes.Count,columns=count==2?2:3,rows=(count+columns-1)/columns;double cw=columns==2?750:500,ch=rows==1?320:rows==3?218:250,gapX=(1760-columns*cw)/(columns-1),gapY=rows==3?50:115,startY=rows==1?350:225;
   var boxes=new List<Rect>();for(int i=0;i<count;i++){int row=i/columns,col=i%columns;if(plan.Kind=="process"&&row%2==1)col=columns-1-col;boxes.Add(new Rect(80+col*(cw+gapX),startY+row*(ch+gapY),cw,ch));}
   // Connectors are routed in the gutters; text is drawn after the connectors.
   for(int k=0;k<plan.Links.Count;k++){
    var link=plan.Links[k];int ai=plan.Nodes.FindIndex(x=>x.Id==link.From),bi=plan.Nodes.FindIndex(x=>x.Id==link.To);var a=boxes[ai];var b=boxes[bi];Point start,end;var path=new List<Point>();
    if(Math.Abs(a.Y-b.Y)<1&&Math.Abs(a.X-b.X)<cw+gapX+1){bool right=b.X>a.X;start=new Point(right?a.Right:a.Left,a.Y+a.Height/2);end=new Point(right?b.Left:b.Right,b.Y+b.Height/2);path.Add(start);path.Add(end);}
    else if(Math.Abs(a.Y-b.Y)<1){start=new(a.X+a.Width/2,a.Bottom);end=new(b.X+b.Width/2,b.Bottom);double y=Math.Min(a.Bottom+gapY/2,height-75);path.Add(start);path.Add(new(start.X,y));path.Add(new(end.X,y));path.Add(end);}
    else{bool down=b.Y>a.Y;start=new Point(a.X+a.Width/2,down?a.Bottom:a.Top);end=new Point(b.X+b.Width/2,down?b.Top:b.Bottom);double mid=(start.Y+end.Y)/2+(k%3-1)*13;path.Add(start);path.Add(new(start.X,mid));path.Add(new(end.X,mid));path.Add(end);}
    var pen=new Pen(Color("#627D88"),3);for(int j=1;j<path.Count;j++)dc.DrawLine(pen,path[j-1],path[j]);
    var before=path[^2];var vector=end-before;if(vector.Length>1){vector.Normalize();var perpendicular=new Vector(-vector.Y,vector.X);var triangle=new StreamGeometry();using(var g=triangle.Open()){g.BeginFigure(end,true,true);g.LineTo(end-vector*15+perpendicular*7,true,false);g.LineTo(end-vector*15-perpendicular*7,true,false);}dc.DrawGeometry(pen.Brush,null,triangle);}
    if(link.Label.Length>0){
     double available=path.Count==2?Math.Max(65,Math.Abs(end.X-start.X)-18):Math.Abs(start.X-end.X)<1?Math.Min(240,1840-start.X-20):500;
     var text=Text(link.Label,20,Color("#294D59"),available);Point middle=path.Count==2?new((start.X+end.X)/2,start.Y-text.Height/2-18):new((path[1].X+path[2].X)/2,path[1].Y);
     double labelX=Math.Abs(start.X-end.X)<1?middle.X+16:middle.X-text.Width/2;var rect=new Rect(labelX-5,middle.Y-text.Height/2-4,text.Width+10,text.Height+8);dc.DrawRoundedRectangle(Color("#F7F9F5"),null,rect,7,7);dc.DrawText(text,new Point(rect.X+5,rect.Y+4));
    }
   }
   string[] accents={"#D7EFF1","#FFF0CA","#E6E0F6","#DDEEDD","#FFE0DA","#DAE7F7"};
   for(int i=0;i<count;i++){
    var box=boxes[i];var node=plan.Nodes[i];dc.DrawRoundedRectangle(Brushes.White,new Pen(Color("#C9D5D6"),2),box,22,22);dc.DrawRoundedRectangle(Color(accents[i%accents.Length]),null,new Rect(box.X,box.Y,box.Width,13),6,6);
    dc.DrawEllipse(Color("#0F5966"),null,new Point(box.X+31,box.Y+43),18,18);dc.DrawText(Text((i+1).ToString(),22,Brushes.White,36),new Point(box.X+24,box.Y+26));
    FormattedText label=Text(node.Label,38,Color("#153D47"),box.Width-105),detail=Text(node.Detail,29,Color("#344C54"),box.Width-64);int size=38;
    while(label.Height+detail.Height+70>box.Height&&size>26){size-=2;label=Text(node.Label,size,Color("#153D47"),box.Width-105);detail=Text(node.Detail,Math.Max(24,size-7),Color("#344C54"),box.Width-64);}
    if(label.Height+detail.Height+70>box.Height)throw new VisualLayoutException("图解文字超出本地排版空间，改用云端生成。");
    dc.DrawText(label,new Point(box.X+65,box.Y+26));dc.DrawText(detail,new Point(box.X+32,box.Y+label.Height+52));
   }
   dc.DrawText(Text(string.IsNullOrWhiteSpace(subject)?"依据所选材料整理 · 请结合原文讲解":subject+" · 依据所选材料整理",23,Color("#667C83"),1750),new Point(80,1030));
  }
  var result=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();return result;
 }
 internal static string Save(VisualPlan plan,string subject,string folder){Directory.CreateDirectory(folder);string path=Path.Combine(folder,"图解_"+Guid.NewGuid().ToString("N")+".png");var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Draw(plan,subject)));using var output=File.Create(path);encoder.Save(output);return path;}
}
