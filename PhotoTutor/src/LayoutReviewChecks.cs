using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace PhotoTutor {
 internal static class LayoutReviewChecks {
  internal static int Run(string folder){Settings.IsolatedTestRoot=Path.Combine(folder,"isolated-runtime");var index=JArray.Parse(File.ReadAllText(Path.Combine(folder,"本地版面索引.json")));var rows=new List<object>();foreach(var item in index){using(var workspace=new QuestionWorkspace(Settings.DataRoot)){var photo=PhotoData.FromFileForSelection((string)item["file"]!);BitmapSource source=photo.Image;var bounds=item["crop"] as JArray;if(bounds!=null){source=new CroppedBitmap(source,new Int32Rect((int)bounds[0],(int)bounds[1],(int)bounds[2],(int)bounds[3]));source.Freeze();}var page=workspace.Add(source,"本地书页检查");var result=CandidateDetector.Detect(page,CancellationToken.None).GetAwaiter().GetResult();var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()){drawing.DrawImage(source,new Rect(0,0,page.Width,page.Height));int i=0;foreach(var region in result.Regions){drawing.DrawRectangle(null,new Pen(Brushes.DeepSkyBlue,3),new Rect(region.Bounds.X,region.Bounds.Y,region.Bounds.Width,region.Bounds.Height));drawing.DrawText(new FormattedText((++i).ToString(),System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Arial"),22,Brushes.Red,1),new Point(region.Bounds.X,Math.Max(0,region.Bounds.Y-24)));}}var bitmap=new RenderTargetBitmap(page.Width,page.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));string name=(string)item["name"]!;using(var file=File.Create(Path.Combine(folder,name+"_候选叠图.png")))encoder.Save(file);rows.Add(new{name,result.OcrAvailable,count=result.Regions.Count,result.Message,result.Seconds,boxes=result.Regions.Select(r=>new{x=r.Bounds.X,y=r.Bounds.Y,width=r.Bounds.Width,height=r.Bounds.Height,heuristicScore=r.Confidence}),sourceUploaded=false,questionAccuracyMeasured=false});}}
   File.WriteAllText(Path.Combine(folder,"本地书页版面结果.json"),JsonConvert.SerializeObject(rows,Formatting.Indented));return 0;}
 }
}
