using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace PhotoTutor {
 internal static class CandidateCorpusChecks {
  internal static int Run(string folder){Settings.IsolatedTestRoot=Path.Combine(folder,"isolated-runtime");var index=JArray.Parse(File.ReadAllText(Path.Combine(folder,"语料索引.json")));var rows=new List<object>();int unsafeApply=0;
   foreach(var item in index){using(var w=new QuestionWorkspace(Settings.DataRoot)){var p=w.AddFile((string)item["file"]!);long stamp=w.Revision;int revision=p.Revision;var result=CandidateDetector.Detect(p,CancellationToken.None).GetAwaiter().GetResult();int complete=0,mixed=0,leakedFigure=0;var groups=(JArray)item["groups"]!;
    double Area(JToken g)=> (double)g["width"]!*(double)g["height"]!;
    double Intersection(System.Windows.Int32Rect c,JToken g){double l=Math.Max(c.X,(double)g["x"]!),t=Math.Max(c.Y,(double)g["y"]!),r=Math.Min(c.X+c.Width,(double)g["x"]!+(double)g["width"]!),b=Math.Min(c.Y+c.Height,(double)g["y"]!+(double)g["height"]!);return Math.Max(0,r-l)*Math.Max(0,b-t);}
    foreach(var g in groups){if(result.Regions.Any(c=>Intersection(c.Bounds,g)>=Area(g)*.98))complete++;else if((bool?)g["figure"]==true)leakedFigure++;}
    foreach(var c in result.Regions)if(groups.Count(g=>Intersection(c.Bounds,g)>Area(g)*.10)>1)mixed++;
    // Discovery may add only unconfirmed regions; it must never populate a QuestionGroup or invoke AI.
    bool accepted=w.AcceptCandidates(p.Id,revision,stamp,result.Regions);if(!accepted||w.Groups.Any(g=>g.Regions.Count!=0))unsafeApply++;
    var oldStamp=w.Revision;w.NewGroup();bool staleAccepted=w.AcceptCandidates(p.Id,p.Revision,oldStamp,result.Regions);if(staleAccepted)unsafeApply++;
    rows.Add(new{name=(string)item["name"],groundTruth=(string)item["groundTruth"],requiresManual=(bool)item["requiresManual"]!,ocrAvailable=result.OcrAvailable,candidates=result.Regions.Count,groundTruthGroups=groups.Count,contentComplete=groups.Count>0?(double?)complete/groups.Count:null,mixedQuestionCandidates=mixed,missingFigureGroups=leakedFigure,needsCorrection=groups.Count>0?complete<groups.Count||mixed>0||leakedFigure>0:(bool)item["requiresManual"]!,humanCorrectionRate=(double?)null,averageHumanClicks=(double?)null,measurementLimit="Human correction and click count were not measured; coverage is from synthetic content bounds only.",seconds=result.Seconds,privateBytes=result.PrivateBytes,processLifetimePeakWorkingSetBytes=result.PeakWorkingSetBytes,message=result.Message,sourceUnchanged=File.Exists((string)item["file"]!),autoSolved=false,staleResultAccepted=staleAccepted});
   }}
   File.WriteAllText(Path.Combine(folder,"自动候选分区_实测指标.json"),JsonConvert.SerializeObject(new{unsafeApply,cases=rows,paidRequests=0,schoolHardware=false},Formatting.Indented));return unsafeApply==0?0:1;
  }
 }
}
