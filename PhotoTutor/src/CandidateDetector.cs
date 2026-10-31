using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
namespace PhotoTutor {
 internal sealed class OcrLine {internal string Text="";internal double X,Y,Width,Height;}
 internal sealed class RegionCandidate {internal Int32Rect Bounds;internal double Confidence;}
 internal sealed class CandidateResult {internal readonly List<RegionCandidate> Regions=new List<RegionCandidate>();internal string Message="",Language="";internal double Seconds;internal long PrivateBytes,PeakWorkingSetBytes;internal bool OcrAvailable;internal int Rotation,OcrPasses;internal double DetailHeight;internal List<OcrLine> AnalysisLines=new List<OcrLine>();}
 internal static class CandidateDetector {
  internal static Task<CandidateResult> Detect(QuestionPage page,CancellationToken token)=>Task.Run(()=>AnalysisWorker.Detect(page,token),token);
  internal static CandidateResult DetectCore(QuestionPage page,CancellationToken token){
   using var budget=CancellationTokenSource.CreateLinkedTokenSource(token);budget.CancelAfter(TimeSpan.FromSeconds(7));var clock=Stopwatch.StartNew();var result=new CandidateResult();
   try{
    token.ThrowIfCancellationRequested();var source=page.Decode();double scale=Math.Min(1,2800d/Math.Max(source.PixelWidth,source.PixelHeight));BitmapSource analysis=scale<1?new TransformedBitmap(source,new ScaleTransform(scale,scale)):source;analysis.Freeze();
    int w=analysis.PixelWidth,h=analysis.PixelHeight,rotation=0,bestW=w,bestH=h;double bestScore=double.MinValue;var bestLines=new List<OcrLine>();BitmapSource bestImage=analysis;string? preferredLanguage=null;
    // Compare semantic, margin-aligned number sequences. Random OCR digits cannot select an orientation.
    foreach(int angle in TextOrientation.Order(analysis)){
     token.ThrowIfCancellationRequested();BitmapSource image=angle==0?analysis:new TransformedBitmap(analysis,new RotateTransform(angle));image.Freeze();JObject json;
     try{result.OcrPasses++;json=WindowsOcr.Read(image,budget.Token,preferredLanguage);}catch(OperationCanceledException){if(token.IsCancellationRequested)throw;break;}catch{continue;}
     if((bool?)json["available"]!=true)break;result.OcrAvailable=true;
     var lines=ReadLines(json,image.PixelWidth);double score=QuestionSegmentation.OrientationScore(lines,image.PixelWidth,image.PixelHeight);
     if(angle==0&&((string?)json["language"]??"").StartsWith("zh")&&lines.Sum(v=>System.Text.RegularExpressions.Regex.Matches(v.Text,@"\p{IsCJKUnifiedIdeographs}").Count)<lines.Sum(v=>System.Text.RegularExpressions.Regex.Matches(v.Text,@"[A-Za-z]").Count)*.35&&lines.Sum(v=>System.Text.RegularExpressions.Regex.Matches(v.Text,@"[A-Za-z]").Count)>20&&!budget.IsCancellationRequested){try{result.OcrPasses++;var english=WindowsOcr.Read(image,budget.Token,"en-US");var alternate=ReadLines(english,image.PixelWidth);double alternateScore=QuestionSegmentation.OrientationScore(alternate,image.PixelWidth,image.PixelHeight);if((bool?)english["available"]==true&&alternateScore>score){json=english;lines=alternate;score=alternateScore;preferredLanguage="en-US";}}catch(OperationCanceledException){if(token.IsCancellationRequested)throw;}catch{}}
     if(Math.Abs((double?)json["angle"]??0)>12)score-=100;
     if(angle==0)score+=2;
     if(score>bestScore){bestScore=score;bestLines=lines;rotation=angle;bestImage=image;bestW=image.PixelWidth;bestH=image.PixelHeight;result.Language=(string?)json["language"]??"";}
     // A coherent upright numbered page does not need three more full-page OCR passes.
     if(score>=55&&FromLines(lines,bestW,bestH,bestW,bestH).Count>=2&&Math.Abs((double?)json["angle"]??0)<8)break;
    }
    var sizes=bestLines.Where(v=>QuestionSegmentation.Compact(v.Text).Length>=4).Select(v=>v.Height).OrderBy(v=>v).ToArray();
    if(result.OcrAvailable&&Math.Max(bestW,bestH)<1900&&sizes.Length>0&&sizes[sizes.Length/2]<20&&!budget.IsCancellationRequested){try{var frame=InkFrame(bestImage);var crop=new CroppedBitmap(bestImage,frame);double zoom=Math.Min(3,2800d/Math.Max(crop.PixelWidth,crop.PixelHeight));var enlarged=new TransformedBitmap(crop,new ScaleTransform(zoom,zoom));enlarged.Freeze();var refined=ReadLines(WindowsOcr.Read(enlarged,budget.Token,result.Language.Length>0?result.Language:null),enlarged.PixelWidth);foreach(var line in refined){line.X=line.X/zoom+frame.X;line.Y=line.Y/zoom+frame.Y;line.Width/=zoom;line.Height/=zoom;}if(QuestionSegmentation.OrientationScore(refined,bestW,bestH)>=QuestionSegmentation.OrientationScore(bestLines,bestW,bestH))bestLines=QuestionSegmentation.Merge(bestLines,refined,bestW);}catch(OperationCanceledException){if(token.IsCancellationRequested)throw;}catch{}}
    if(result.OcrAvailable&&FromLines(bestLines,bestW,bestH,bestW,bestH).Count<2&&!budget.IsCancellationRequested){try{var normalized=PhotographPreprocess.Normalize(bestImage);var frame=InkFrame(normalized);var crop=new CroppedBitmap(normalized,frame);double zoom=Math.Min(3,2800d/Math.Max(frame.Width,frame.Height));BitmapSource input=zoom>1?new TransformedBitmap(crop,new ScaleTransform(zoom,zoom)):crop;input.Freeze();var clean=ReadLines(WindowsOcr.Read(input,budget.Token,result.Language.Length>0?result.Language:null),input.PixelWidth);foreach(var line in clean){line.X=line.X/zoom+frame.X;line.Y=line.Y/zoom+frame.Y;line.Width/=zoom;line.Height/=zoom;}if(QuestionSegmentation.OrientationScore(clean,bestW,bestH)>QuestionSegmentation.OrientationScore(bestLines,bestW,bestH))bestLines=FromLines(bestLines,bestW,bestH,bestW,bestH).Count==0?clean:QuestionSegmentation.Merge(bestLines,clean,bestW);}catch(OperationCanceledException){if(token.IsCancellationRequested)throw;}catch{}}
    if(result.OcrAvailable&&!budget.IsCancellationRequested){
     // Supplement only established number margins, to avoid formula/watermark interference.
     foreach(var margin in QuestionSegmentation.NumberMargins(bestLines,bestW,bestH)){
      token.ThrowIfCancellationRequested();try{var crop=new CroppedBitmap(bestImage,margin);crop.Freeze();double zoom=Math.Max(1,Math.Min(3,2000d/crop.PixelHeight));BitmapSource digits=zoom>1?new TransformedBitmap(crop,new ScaleTransform(zoom,zoom)):crop;digits.Freeze();var extra=ReadLines(WindowsOcr.Read(digits,budget.Token,"en-US"),digits.PixelWidth);foreach(var line in extra){line.X=line.X/zoom+margin.X;line.Y=line.Y/zoom+margin.Y;line.Width/=zoom;line.Height/=zoom;}bestLines=QuestionSegmentation.Merge(bestLines,extra,bestW);}catch(OperationCanceledException){if(token.IsCancellationRequested)throw;break;}catch{}
     }
    }
    result.Rotation=rotation;result.AnalysisLines=bestLines;var detail=bestLines.Where(v=>QuestionSegmentation.Compact(v.Text).Length>=4&&v.Height>0).Select(v=>v.Height).OrderBy(v=>v).ToArray();result.DetailHeight=detail.Length==0?0:detail[detail.Length/2];var candidates=FromLines(bestLines,bestW,bestH,bestW,bestH);
    // Trim unused space within an established question interval; never use ink blocks to invent questions.
    var gray=new FormatConvertedBitmap(bestImage,PixelFormats.Gray8,null,0);var pixels=new byte[bestW*bestH];gray.CopyPixels(pixels,bestW,0);
    foreach(var candidate in candidates){var b=candidate.Bounds;int start=Math.Max(0,b.Y-Math.Max(24,bestH/32)),blank=-1,bestEnd=-1,bestLength=0;bool inkAfter=false;
     for(int y=start;y<b.Y;y++){int ink=0;for(int x=Math.Max(b.X+8,(int)(bestW*.02));x<Math.Min(b.X+b.Width-8,(int)(bestW*.98));x++)if(pixels[y*bestW+x]<155)ink++;if(ink<4){if(blank<0)blank=y;}else{if(blank>=0&&y-blank>=6&&y-blank>bestLength){bestLength=y-blank;bestEnd=y;inkAfter=true;}blank=-1;}}
     int topInk=0;for(int y=Math.Max(start,b.Y-2);y<=Math.Min(bestH-1,b.Y+2);y++)for(int x=Math.Max(b.X+8,(int)(bestW*.02));x<Math.Min(b.X+b.Width-8,(int)(bestW*.98));x++)if(pixels[y*bestW+x]<155)topInk++;if(topInk>=12&&inkAfter&&bestEnd>=0){int top=Math.Max(start,bestEnd-5);candidate.Bounds=new Int32Rect(b.X,top,b.Width,b.Y+b.Height-top);}
    }
    foreach(var candidate in candidates){var b=candidate.Bounds;var next=candidates.Where(c=>c!=candidate&&Math.Abs(c.Bounds.X-b.X)<bestW*.04&&c.Bounds.Y>b.Y&&c.Bounds.Y<b.Y+b.Height).OrderBy(c=>c.Bounds.Y).FirstOrDefault();if(next!=null)candidate.Bounds=new Int32Rect(b.X,b.Y,b.Width,next.Bounds.Y-b.Y);}
    foreach(var candidate in candidates){var b=candidate.Bounds;int last=-1;for(int y=b.Y;y<b.Y+b.Height;y++){int ink=0;for(int x=Math.Max(b.X+8,(int)(bestW*.02));x<Math.Min(b.X+b.Width-8,(int)(bestW*.98));x++)if(pixels[y*bestW+x]<155)ink++;if(ink>=Math.Max(4,b.Width*.003))last=y;}if(last>=b.Y)candidate.Bounds=new Int32Rect(b.X,b.Y,b.Width,Math.Min(b.Height,last-b.Y+Math.Max(10,bestH/180)));}
    if(QuestionSegmentation.ContentFrame(bestLines,bestW,bestH).Width<bestW*.85)foreach(var candidate in candidates){var b=candidate.Bounds;int left=b.X+b.Width,right=b.X;for(int y=b.Y;y<b.Y+b.Height;y++)for(int x=b.X;x<b.X+b.Width;x++)if(pixels[y*bestW+x]<155){left=Math.Min(left,x);right=Math.Max(right,x);}if(right-left>24){int x=Math.Max(b.X,left-8),r=Math.Min(b.X+b.Width,right+9);candidate.Bounds=new Int32Rect(x,b.Y,r-x,b.Height);}}
    foreach(var candidate in candidates){var original=PageLayout.BackToOriginal(candidate.Bounds,w,h,rotation,page.Width,page.Height);if(original.Width>=32&&original.Height>=32)result.Regions.Add(new RegionCandidate{Bounds=original,Confidence=candidate.Confidence});}
    result.Message=result.Regions.Count==0?"未识别到可靠的完整题目边界，请手动拖框；尽量只拍一页、对正并让文字清晰。":"已标出"+result.Regions.Count+"个待确认区域；请核对整道题的题干、选项和配图，缺少的部分可继续拖框加入。";
    if(result.DetailHeight>0&&result.DetailHeight<13)result.Message+=" 字形较小，请重点核对蓝框边界；原图已保留，可继续补框。";
    if(rotation!=0&&result.Regions.Count>0)result.Message+=" 已按文字方向分析并映射回原图；可先旋转画面再选题。";
    if(result.DetailHeight>0&&result.DetailHeight<13)result.Message+=" 可保持原生分辨率靠近局部拍摄，或分两张补拍同一道题。";
    if(!result.OcrAvailable)result.Message+=" 系统OCR不可用，可安装对应Windows文字识别语言包后重试。";
    return result;
   }finally{result.OcrPasses=WindowsOcr.Reads;result.Seconds=clock.Elapsed.TotalSeconds;result.PrivateBytes=Process.GetCurrentProcess().PrivateMemorySize64;result.PeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64;}
  }
  internal static bool IsAnchor(string text)=>QuestionSegmentation.Number(text)>0;
  // Remove only empty outer margins from the OCR analysis image, never from the retained source or a question box.
  static Int32Rect InkFrame(BitmapSource source){int w=source.PixelWidth,h=source.PixelHeight;var gray=new FormatConvertedBitmap(source,PixelFormats.Gray8,null,0);var pixels=new byte[w*h];gray.CopyPixels(pixels,w,0);int left=w,right=-1,top=h,bottom=-1;var columns=new int[w];for(int y=0;y<h;y++){int count=0;for(int x=0;x<w;x++)if(pixels[y*w+x]<155){count++;columns[x]++;}if(count>=Math.Max(3,w/250)){top=Math.Min(top,y);bottom=y;}}for(int x=0;x<w;x++)if(columns[x]>=Math.Max(3,h/250)){left=Math.Min(left,x);right=x;}if(right-left<32||bottom-top<32)return new Int32Rect(0,0,w,h);left=Math.Max(0,left-16);right=Math.Min(w,right+17);top=Math.Max(0,top-16);bottom=Math.Min(h,bottom+17);return new Int32Rect(left,top,right-left,bottom-top);}
  internal static List<OcrLine> ReadLines(JObject json,int width){
   var output=new List<OcrLine>();
   foreach(var line in json["lines"]??new JArray()){
    var words=(line["words"]??new JArray()).OrderBy(v=>(double?)v["x"]??0).ToArray();
    var parts=new List<JToken>();Action flush=()=>{if(parts.Count==0)return;double x=parts.Min(v=>(double?)v["x"]??0),y=parts.Min(v=>(double?)v["y"]??0);output.Add(new OcrLine{Text=string.Join(" ",parts.Select(v=>(string?)v["text"]??"")),X=x,Y=y,Width=parts.Max(v=>((double?)v["x"]??0)+((double?)v["width"]??0))-x,Height=parts.Max(v=>((double?)v["y"]??0)+((double?)v["height"]??0))-y});parts.Clear();};
    foreach(var word in words){if(parts.Count>0&&((double?)word["x"]??0)-(((double?)parts.Last()["x"]??0)+((double?)parts.Last()["width"]??0))>width*.16)flush();parts.Add(word);}flush();
   }
   return output.OrderBy(v=>v.Y).ThenBy(v=>v.X).ToList();
  }
  internal static List<RegionCandidate> FromLines(IEnumerable<OcrLine> source,int w,int h,int ow,int oh)=>QuestionSegmentation.Split(source,w,h,ow,oh);
 }
}

