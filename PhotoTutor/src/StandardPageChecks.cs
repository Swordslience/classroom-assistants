using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace PhotoTutor {
 internal static class StandardPageChecks {
  internal static int Run(string folder){
   Directory.CreateDirectory(folder);Settings.IsolatedTestRoot=Path.Combine(folder,"isolated-runtime");
   var index=JArray.Parse(File.ReadAllText(Path.Combine(folder,"本地版面索引.json")));var rows=new List<object>();
   foreach(var item in index){using(var workspace=new QuestionWorkspace(Settings.DataRoot)){
    var page=workspace.AddFile((string)item["file"]!);var source=page.Decode();string name=(string)item["name"]!;
    // Public benchmark pages only. This diagnostic is never used by the normal capture flow.
    if((bool?)item["exportOcr"]==true){var json=WindowsOcr.Read(source,CancellationToken.None);json["physicalRows"]=JArray.FromObject(CandidateDetector.ReadLines(json,source.PixelWidth).Select(v=>new{v.Text,v.X,v.Y,v.Width,v.Height,number=QuestionSegmentation.Number(v.Text)}));File.WriteAllText(Path.Combine(folder,name+"_ocr.json"),json.ToString());}
    var result=CandidateDetector.Detect(page,CancellationToken.None).GetAwaiter().GetResult();
    if((bool?)item["exportOcr"]==true)File.WriteAllText(Path.Combine(folder,name+"_analysis.json"),JArray.FromObject(result.AnalysisLines.Select(v=>new{v.Text,v.X,v.Y,v.Width,v.Height})).ToString());
    rows.Add(new{name,result.Message,result.OcrAvailable,result.Language,result.OcrPasses,result.Seconds,result.PrivateBytes,result.PeakWorkingSetBytes,result.DetailHeight,result.Rotation,debugNumbers=result.AnalysisLines.Where(v=>QuestionSegmentation.Number(v.Text)>0).Select(v=>new{v.Text,v.X,v.Y,v.Width,v.Height,number=QuestionSegmentation.Number(v.Text)}),boxes=result.Regions.Select(r=>new{x=r.Bounds.X,y=r.Bounds.Y,width=r.Bounds.Width,height=r.Bounds.Height,score=r.Confidence})});
   }}
   File.WriteAllText(Path.Combine(folder,"标准页检测结果.json"),JsonConvert.SerializeObject(rows,Formatting.Indented));return 0;
  }
 }
}
