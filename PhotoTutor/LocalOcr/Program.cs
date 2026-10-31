using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.ML.OnnxRuntime;
using RapidOcrNet;
using SkiaSharp;
using PhotoTutor;

internal static class Program {
 static int Main(string[] args){Directory.SetCurrentDirectory(AppContext.BaseDirectory);string output="";bool probe=args.Length==3&&args[0]=="--probe";
  try{string input;int expectedWidth=0,expectedHeight=0;
   if(probe){input=args[1];output=args[2];}
   else if(args.Length==2&&args[0]=="--analysis-worker"){
    string folder=Path.GetFullPath(args[1]);Guid id;if(!string.Equals(Path.GetDirectoryName(folder),Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(folder).StartsWith("PhotoTutor-analysis-")||!Guid.TryParseExact(Path.GetFileName(folder)[20..],"N",out id)||File.ReadAllText(Path.Combine(folder,"owned.cache"))!="PhotoTutor analysis v1")return 4;
    string file=Path.Combine(folder,"request.json");if(new FileInfo(file).Length>65536)return 5;var request=JsonNode.Parse(File.ReadAllText(file))!.AsObject();output=Path.Combine(folder,"result.json");string? fault=request["fault"]?.GetValue<string>();if(fault=="hang"){Thread.Sleep(30000);return 8;}if(fault=="crash")return 77;if(fault=="invalid"){File.WriteAllText(output,"invalid");return 0;}input=request["file"]!.GetValue<string>();expectedWidth=request["width"]!.GetValue<int>();expectedHeight=request["height"]!.GetValue<int>();
   }else return 1;
   OrtEnv.Instance().DisableTelemetryEvents();var clock=Stopwatch.StartNew();using var bitmap=SKBitmap.Decode(input);if(bitmap==null||bitmap.Width<32||bitmap.Height<32||(long)bitmap.Width*bitmap.Height>24000000||(expectedWidth>0&&(bitmap.Width!=expectedWidth||bitmap.Height!=expectedHeight)))return 6;
   int threads=Math.Max(1,Math.Min(2,Environment.ProcessorCount));using var ocr=new RapidOcr();using var session=RapidOcr.GetDefaultSessionOptions(threads);ocr.InitModels(RapidOcrModelSet.PPOCRv6Tiny,session);double initialized=clock.Elapsed.TotalSeconds;
   using var gray=bitmap.Copy(SKColorType.Gray8);byte[]? pixels=gray?.Bytes;int cropX=0,cropY=0,whites=0,cropInkLeft=bitmap.Width,cropInkRight=-1,cropInkTop=bitmap.Height,cropInkBottom=-1;if(pixels!=null){for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++){byte value=pixels[y*gray!.RowBytes+x];if(value>225)whites++;if(value<225){cropInkLeft=Math.Min(cropInkLeft,x);cropInkRight=Math.Max(cropInkRight,x);cropInkTop=Math.Min(cropInkTop,y);cropInkBottom=Math.Max(cropInkBottom,y);}}}using var subset=new SKBitmap();SKBitmap analysis=bitmap;if(whites>(long)bitmap.Width*bitmap.Height*.65&&cropInkRight-cropInkLeft>32&&cropInkBottom-cropInkTop>32&&(cropInkRight-cropInkLeft<bitmap.Width*.85||cropInkBottom-cropInkTop<bitmap.Height*.85)){cropX=Math.Max(0,cropInkLeft-16);cropY=Math.Max(0,cropInkTop-16);int r=Math.Min(bitmap.Width,cropInkRight+17),b=Math.Min(bitmap.Height,cropInkBottom+17);if(bitmap.ExtractSubset(subset,new SKRectI(cropX,cropY,r,b)))analysis=subset;else{cropX=cropY=0;}}
   // Bound the detector tensor, not the captured photo. Recognition and output coordinates
   // remain in source pixels; the parent keeps the untouched full-resolution page.
   var read=ocr.Detect(analysis,RapidOcrOptions.PPOCRv6 with {ImgResize=Math.Max(analysis.Width,analysis.Height)>1536?1536:0,TextScore=.10f,ReturnWordBox=false});
   var original=read.TextBlocks.Where(b=>!string.IsNullOrWhiteSpace(b.Text)&&(b.CharScores?.Average()??0)>=.55f).Select(b=>new OcrLine{Text=b.Text,X=b.BoxPoints.Min(p=>p.X)+cropX,Y=b.BoxPoints.Min(p=>p.Y)+cropY,Width=b.BoxPoints.Max(p=>p.X)-b.BoxPoints.Min(p=>p.X),Height=b.BoxPoints.Max(p=>p.Y)-b.BoxPoints.Min(p=>p.Y)}).Where(v=>v.Width>=2&&v.Height>=2).ToList();
   int rotation=0,w=bitmap.Width,h=bitmap.Height;double best=double.MinValue;var lines=original;
   // Rectified line crops are already readable. Compare four layout directions without rerunning the three models.
   foreach(int angle in new[]{0,90,270,180}){int width=angle%180==0?bitmap.Width:bitmap.Height,height=angle%180==0?bitmap.Height:bitmap.Width;var transformed=original.Select(v=>Transform(v,bitmap.Width,bitmap.Height,angle)).ToList();double score=QuestionSegmentation.OrientationScore(transformed,width,height)+QuestionSegmentation.Split(transformed,width,height,width,height,true).Count*6;if(angle==0)score+=.2;if(score>best){best=score;rotation=angle;lines=transformed;w=width;h=height;}}
   var candidates=QuestionSegmentation.Split(lines,w,h,w,h,true);
   // A missed tiny margin numeral must not silently join two questions. An unrecognized,
   // isolated glyph aligned with the numbered margin plus a new adjacent text line is an
   // ambiguity barrier, not an invented question number. Keep manual selection available.
   if(pixels!=null)candidates.RemoveAll(candidate=>HasUnknownMarginStart(candidate.Bounds,lines,pixels,gray!.RowBytes,bitmap.Width,bitmap.Height,rotation,w,h));
   if(pixels!=null)foreach(var candidate in candidates){var b=candidate.Bounds;int last=-1;for(int y=b.Y;y<b.Y+b.Height;y++){int ink=0;for(int x=b.X+Math.Min(8,b.Width/10);x<b.X+b.Width-Math.Min(8,b.Width/10);x++){var point=BackPoint(x,y,bitmap.Width,bitmap.Height,rotation);if(pixels[point.y*gray!.RowBytes+point.x]<155)ink++;}if(ink>=Math.Max(4,b.Width*.003)&&ink<b.Width*.65)last=y;}var textBottom=lines.Where(v=>v.Y>=b.Y-2&&v.Y<b.Y+b.Height&&v.X>=b.X&&v.X<b.X+b.Width).Select(v=>(int)Math.Ceiling(v.Y+v.Height)).DefaultIfEmpty(-1).Max();last=Math.Max(last,Math.Min(b.Y+b.Height-1,textBottom));if(last>=b.Y)candidate.Bounds=new PixelRect(b.X,b.Y,b.Width,Math.Min(b.Height,last-b.Y+Math.Max(10,h/180)));}
   if(pixels!=null){if(whites>(long)bitmap.Width*bitmap.Height*.65)foreach(var candidate in candidates){var b=candidate.Bounds;int left=b.X+b.Width,right=b.X;for(int y=b.Y;y<b.Y+b.Height;y++)for(int x=b.X;x<b.X+b.Width;x++){var p=BackPoint(x,y,bitmap.Width,bitmap.Height,rotation);if(pixels[p.y*gray!.RowBytes+p.x]<155){left=Math.Min(left,x);right=Math.Max(right,x);}}if(right-left>24){int x=Math.Max(b.X,left-8),r=Math.Min(b.X+b.Width,right+9);candidate.Bounds=new PixelRect(x,b.Y,r-x,b.Height);}}}
   var boxes=new JsonArray();foreach(var candidate in candidates){var b=Back(candidate.Bounds,bitmap.Width,bitmap.Height,rotation);if(b.Width>=32&&b.Height>=32&&b.X>=0&&b.Y>=0&&b.X+b.Width<=bitmap.Width&&b.Y+b.Height<=bitmap.Height)boxes.Add((JsonNode)new JsonObject{["x"]=b.X,["y"]=b.Y,["width"]=b.Width,["height"]=b.Height,["score"]=candidate.Confidence});}
   var rows=new JsonArray();foreach(var line in lines)rows.Add((JsonNode)new JsonObject{["Text"]=line.Text,["X"]=line.X,["Y"]=line.Y,["Width"]=line.Width,["Height"]=line.Height});
   var details=lines.Where(v=>QuestionSegmentation.Compact(v.Text).Length>=4).Select(v=>v.Height).OrderBy(v=>v).ToArray();
   string message=boxes.Count>0?"已标出"+boxes.Count+"个待确认题目区域，请核对题干、选项和配图。":"本次未找到可靠的完整题目边界，原图已保留，可直接拖框补选。";if(rotation!=0&&boxes.Count>0)message+=" 已自动识别横放方向，题框已映射回原照片。";
   var result=new JsonObject{["message"]=message,["ocr"]=true,["rotation"]=rotation,["detail"]=details.Length==0?0:details[details.Length/2],["language"]="PP-OCRv6 tiny / CPU / multilingual",["passes"]=1,["threads"]=threads,["privateBytes"]=Process.GetCurrentProcess().PrivateMemorySize64,["peak"]=Process.GetCurrentProcess().PeakWorkingSet64,["lines"]=rows,["regions"]=boxes,["seconds"]=clock.Elapsed.TotalSeconds,["initialized"]=initialized,["originalWidth"]=bitmap.Width,["originalHeight"]=bitmap.Height};
   if(probe){var uncertainRows=new JsonArray();foreach(var b in read.TextBlocks.Where(b=>(b.CharScores?.Average()??0)<.55f))uncertainRows.Add((JsonNode)new JsonObject{["text"]=b.Text,["score"]=b.CharScores?.Average()??0,["x"]=b.BoxPoints.Min(p=>p.X)+cropX,["y"]=b.BoxPoints.Min(p=>p.Y)+cropY,["width"]=b.BoxPoints.Max(p=>p.X)-b.BoxPoints.Min(p=>p.X),["height"]=b.BoxPoints.Max(p=>p.Y)-b.BoxPoints.Min(p=>p.Y)});result["uncertainProbeOnly"]=uncertainRows;}
   File.WriteAllText(output,result.ToJsonString());return 0;
  }catch(Exception error){if(output.Length>0)try{File.WriteAllText(output,new JsonObject{["errorType"]=error.GetType().FullName,["hresult"]=error.HResult}.ToJsonString());}catch{}return 9;}
 }
 static OcrLine Transform(OcrLine a,int w,int h,int angle)=>angle switch{90=>new OcrLine{Text=a.Text,X=h-a.Y-a.Height,Y=a.X,Width=a.Height,Height=a.Width},180=>new OcrLine{Text=a.Text,X=w-a.X-a.Width,Y=h-a.Y-a.Height,Width=a.Width,Height=a.Height},270=>new OcrLine{Text=a.Text,X=a.Y,Y=w-a.X-a.Width,Width=a.Height,Height=a.Width},_=>new OcrLine{Text=a.Text,X=a.X,Y=a.Y,Width=a.Width,Height=a.Height}};
 static bool HasUnknownMarginStart(PixelRect b,List<OcrLine> lines,byte[] pixels,int stride,int sourceW,int sourceH,int rotation,int w,int h){
  var anchor=lines.Where(v=>QuestionSegmentation.Number(v.Text)>0&&v.Y>=b.Y-2&&v.Y<b.Y+Math.Max(60,h*.08)&&v.X>=b.X&&v.X<b.X+Math.Max(40,w*.10)).OrderBy(v=>v.Y).FirstOrDefault();
  if(anchor==null||!System.Text.RegularExpressions.Regex.IsMatch(anchor.Text.Trim(),@"^\d{1,3}$")||b.Height<h*.35)return false;
  int left=Math.Max(0,(int)anchor.X-3),right=Math.Min(w,(int)Math.Ceiling(anchor.X+anchor.Width)+3),start=-1;
  bool Ambiguous(int top,int bottom){int height=bottom-top;if(height<Math.Max(4,anchor.Height*.45)||height>anchor.Height*2.2)return false;
   if(lines.Any(v=>v.Y<bottom+2&&v.Y+v.Height>top-2&&v.X<right&&v.X+v.Width>left))return false;
   return lines.Any(v=>v.Y<bottom+anchor.Height*.6&&v.Y+v.Height>top&&v.X>=right&&v.X<right+w*.15&&System.Text.RegularExpressions.Regex.IsMatch(v.Text,@"[A-Za-z\p{IsCJKUnifiedIdeographs}]{2}"));
  }
  for(int y=Math.Max(b.Y,(int)(anchor.Y+anchor.Height*3));y<Math.Min(h,b.Y+b.Height);y++){
   int ink=0;for(int x=left;x<right;x++){var p=BackPoint(x,y,sourceW,sourceH,rotation);if(pixels[p.y*stride+p.x]<155)ink++;}
   if(ink>=1){if(start<0)start=y;}else if(start>=0){if(Ambiguous(start,y))return true;start=-1;}
  }return start>=0&&Ambiguous(start,Math.Min(h,b.Y+b.Height));
 }
 static (int x,int y) BackPoint(int x,int y,int w,int h,int angle)=>angle switch{90=>(y,h-1-x),180=>(w-1-x,h-1-y),270=>(w-1-y,x),_=>(x,y)};
 static PixelRect Back(PixelRect b,int w,int h,int angle)=>angle switch{90=>new PixelRect(b.Y,h-b.X-b.Width,b.Height,b.Width),180=>new PixelRect(w-b.X-b.Width,h-b.Y-b.Height,b.Width,b.Height),270=>new PixelRect(w-b.Y-b.Height,b.X,b.Height,b.Width),_=>b};
}

