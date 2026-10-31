using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
#if LOCAL_OCR_ENGINE
using Int32Rect=PhotoTutor.PixelRect;
#else
using System.Windows;
#endif
namespace PhotoTutor {
 internal static class QuestionSegmentation {
  internal static string Compact(string text)=>Regex.Replace(text.Normalize(System.Text.NormalizationForm.FormKC),@"\s+","");
  internal static int Number(string text){string value=text.Normalize(System.Text.NormalizationForm.FormKC).Trim();
   var match=Regex.Match(value,@"^(?:第\s*(\d{1,3})\s*题|(?:例|题)\s*(\d{1,3})|([1-9]\d{0,2})\s*[.、．·•](?!\d)|([1-9]\d{0,2})(?:\s+(?=[A-Za-z])|\s*$)|([lI])\s*[.、．·](?=\s*[A-Za-z\p{IsCJKUnifiedIdeographs}]))");
   if(!match.Success)return 0;for(int i=1;i<match.Groups.Count;i++)if(match.Groups[i].Success)return i==5?1:int.Parse(match.Groups[i].Value);return 0;
  }
  static bool Section(OcrLine l)=>Regex.IsMatch(Compact(l.Text),@"^[一二三四五六七八九十]*[、.．]?(?:单项选择题|多项选择题|选择题|填空题|解答题|阅读理解|完形填空|判断题|计算题|综合题)");
  static bool Footer(OcrLine l,int h)=>l.Y>h*.86&&Regex.IsMatch(Compact(l.Text),@"试题第\d+页|^第\d+页$|Page\d+[Oo]f|Cambr[iI]dge|0580/|[Tt]urnover|^\d{8,}$");
  internal static Int32Rect ContentFrame(IEnumerable<OcrLine> source,int w,int h){var lines=source.Where(v=>v.Height>=4&&v.Width>=5&&v.Text.Length>0).ToArray();if(lines.Length<3)return new Int32Rect(0,0,w,h);double left=lines.Min(v=>v.X),right=lines.Max(v=>v.X+v.Width);int pad=Math.Max(10,h/40);if(left>w*.14&&right-left<w*.8&&right-left>w*.15){int x=Math.Max(0,(int)left-pad),r=Math.Min(w,(int)Math.Ceiling(right)+pad);return new Int32Rect(x,0,r-x,h);}return new Int32Rect(0,0,w,h);}
  static OcrLine[] Anchors(IEnumerable<OcrLine> source,int w,int h){var lines=source.ToArray();var frame=ContentFrame(lines,w,h);if(frame.X==0&&frame.Width==w)return AnchorsCore(lines,w,h);var shifted=lines.Select(v=>new OcrLine{Text=v.Text,X=v.X-frame.X,Y=v.Y,Width=v.Width,Height=v.Height}).ToArray();var map=shifted.Zip(lines,(local,original)=>new{local,original}).ToDictionary(v=>v.local,v=>v.original);return AnchorsCore(shifted,frame.Width,h).Select(v=>map[v]).ToArray();}
  static OcrLine[] AnchorsCore(IEnumerable<OcrLine> source,int w,int h){var lines=source.OrderBy(v=>v.Y).ToArray();double start=0;
   // Numbered exam instructions must not become question candidates.
   var instructions=lines.Where(v=>Regex.IsMatch(Compact(v.Text),@"注意事项|^Instructions$|^Questionsandanswers$",RegexOptions.IgnoreCase)).ToArray();if(instructions.Length>0){var section=lines.FirstOrDefault(v=>v.Y>instructions[0].Y&&Section(v));if(section==null)return Array.Empty<OcrLine>();start=section.Y+section.Height;}
   var possible=lines.Where(v=>v.Y>=start&&v.Y>h*.025&&v.Y<h*.94&&v.Height>=6&&v.X<w*.94&&Number(v.Text)>0).ToArray();
   var columns=new List<List<OcrLine>>();foreach(var a in possible.OrderBy(v=>v.X)){var group=columns.FirstOrDefault(c=>Math.Abs(c.Average(v=>v.X)-a.X)<Math.Max(16,w*.035));if(group==null){group=new List<OcrLine>();columns.Add(group);}group.Add(a);}
   bool Strong(OcrLine line)=>Regex.IsMatch(Compact(line.Text),@"^(?:第\d{1,3}题|(?:\d{1,3}|[lI])[.、．·])")&&Regex.IsMatch(Compact(line.Text),@"[A-Za-z\p{IsCJKUnifiedIdeographs}]{2}");bool anyStrongColumn=columns.Any(c=>c.Count(Strong)>=2);
   var accepted=new List<OcrLine>();foreach(var column in columns){double x=column.Average(v=>v.X);var ordered=column.OrderBy(v=>v.Y).ToArray();if(anyStrongColumn&&!column.Any(Strong))continue;bool coherent=ordered.Count(Strong)>=2&&ordered.Zip(ordered.Skip(1),(a,b)=>Number(b.Text)==Number(a.Text)+1&&b.Y-a.Y>Math.Max(a.Height,b.Height)*1.4).Any(v=>v);if(!coherent&&x>w*.22&&x<w*.40)continue;
    var near=lines.Where(v=>v.Y>=start&&v.X>=x-w*.035&&v.X<x+w*.25&&v.Width>w*.12).ToArray();if(near.Length==0)continue;
    if(!coherent&&x>w*.18&&x<w*.45)continue;
    if(!coherent&&x<w*.4&&lines.Any(v=>v.Y>=start&&v.Width>w*.25&&v.X<x-w*.04&&v.X<w*.18))continue;
    foreach(var a in column){bool text=Regex.IsMatch(Compact(a.Text),@"[A-Za-z\p{IsCJKUnifiedIdeographs}]{2}");bool marginOnly=Regex.IsMatch(Compact(a.Text),@"^(?:第\d{1,3}题|\d{1,3}[.·、．]|\d{1,3}$)");bool stem=text||marginOnly&&near.Any(v=>v.Y>=a.Y-a.Height&&v.Y<a.Y+h*.45&&v.X>=a.X&&v.X<a.X+w*.16)||lines.Any(v=>v!=a&&v.X>a.X+a.Width&&v.X<a.X+w*.18&&Math.Abs(v.Y-a.Y)<Math.Max(a.Height,v.Height)*.9&&Regex.IsMatch(Compact(v.Text),@"[A-Za-z\p{IsCJKUnifiedIdeographs}]{2}"));if(!stem)continue;
     if(a.Height>h*.08||a.Width<2)continue;
     if(!coherent&&x>w*.4&&lines.Any(l=>l.X<x-w*.15&&l.X+l.Width>x+w*.08&&Math.Abs(l.Y-a.Y)<h*.06))continue;
     accepted.Add(a);
    }
   }
   var unique=new List<OcrLine>();foreach(var a in accepted.OrderBy(v=>v.Y).ThenBy(v=>v.X)){if(unique.Any(prior=>Number(prior.Text)==Number(a.Text)&&Math.Abs(prior.X-a.X)<Math.Max(8,w*.018)&&a.Y-prior.Y<h*.35&&!lines.Any(l=>Section(l)&&l.Y>prior.Y&&l.Y<a.Y)))continue;unique.Add(a);}return unique.ToArray();
  }
  internal static double OrientationScore(List<OcrLine> lines,int w,int h){var anchors=Anchors(lines,w,h);double score=anchors.Length*12;for(int i=1;i<anchors.Length;i++)if(Number(anchors[i].Text)==Number(anchors[i-1].Text)+1&&Math.Abs(anchors[i].X-anchors[i-1].X)<w*.05)score+=30;
   var columns=new List<List<OcrLine>>();foreach(var a in anchors.OrderBy(v=>v.X)){var column=columns.FirstOrDefault(c=>Math.Abs(c.Average(v=>v.X)-a.X)<Math.Max(16,w*.05));if(column==null){column=new List<OcrLine>();columns.Add(column);}column.Add(a);}foreach(var column in columns){var sequence=column.OrderBy(v=>v.Y).ToArray();for(int i=1;i<sequence.Length;i++){int delta=Number(sequence[i].Text)-Number(sequence[i-1].Text);if(delta==1)score+=30;else if(delta==-1)score-=25;}}if(anchors.Length>0)score+=anchors.Sum(v=>Math.Max(0,.5-v.Y/h)*5-v.X/w*2);
   score+=lines.Sum(l=>Regex.Matches(l.Text,@"\b(?:the|is|of|and|in|to|a|write|work|find|show|number|diagram)\b",RegexOptions.IgnoreCase).Count)*2;
   score+=Math.Min(30,lines.Sum(l=>Regex.Matches(l.Text,@"[\p{IsCJKUnifiedIdeographs}]").Count)*.05);return score;
  }
  internal static List<Int32Rect> NumberMargins(List<OcrLine> lines,int w,int h){var anchors=Anchors(lines,w,h);var frame=ContentFrame(lines,w,h);int cw=frame.Width;var output=new List<Int32Rect>{new Int32Rect(frame.X,0,Math.Max(32,(int)(cw*.13)),h)};foreach(var a in anchors.Where(v=>v.X>frame.X+cw*.4)){if(output.Any(r=>Math.Abs(r.X-a.X)<cw*.08))continue;int x=Math.Max(0,(int)a.X-(int)(cw*.02)),width=Math.Min(w-x,(int)(cw*.13));output.Add(new Int32Rect(x,0,width,h));if(output.Count==2)break;}return output;}
  internal static List<OcrLine> Merge(List<OcrLine> original,List<OcrLine> extra,int w){var result=new List<OcrLine>(original);foreach(var line in extra.Where(v=>Number(v.Text)>0)){var same=original.FirstOrDefault(v=>Math.Abs(v.Y-line.Y)<Math.Max(v.Height,line.Height)&&Math.Abs(v.X-line.X)<w*.035);if(same!=null){if(Number(same.Text)==0&&Compact(same.Text).Length<12&&same.Width<w*.25){result.Add(line);}continue;}result.Add(line);}return result;}
  internal static List<RegionCandidate> Split(IEnumerable<OcrLine> source,int w,int h,int ow,int oh,bool expandedBoxes=false){var lines=source.OrderBy(v=>v.Y).ToArray();int cw=ContentFrame(lines,w,h).Width;var anchors=Anchors(lines,w,h);var result=new List<RegionCandidate>();if(anchors.Length==0||anchors.Length>40)return result;
   var columns=new List<List<OcrLine>>();foreach(var a in anchors.OrderBy(v=>v.X)){var column=columns.FirstOrDefault(c=>Math.Abs(c.Average(v=>v.X)-a.X)<cw*.14);if(column==null){column=new List<OcrLine>();columns.Add(column);}column.Add(a);}if(columns.Count>4)return result;
   foreach(var column in columns){int columnIndex=columns.IndexOf(column);double x=column.Average(v=>v.X),left=Math.Max(0,column.Min(v=>v.X)-cw*.025),right=columnIndex+1<columns.Count?columns[columnIndex+1].Min(v=>v.X)-cw*.03:w;var sequence=column.OrderBy(v=>v.Y).ToArray();
    if(columnIndex+1<columns.Count&&lines.Any(l=>l.Y>=sequence[0].Y-l.Height*.4&&l.X<right-cw*.05&&l.X+l.Width>right+cw*.04))continue;
    for(int i=0;i<sequence.Length;i++){var a=sequence[i];int number=Number(a.Text);if(i+1<sequence.Length&&Number(sequence[i+1].Text)>number+1&&Enumerable.Range(number+1,Number(sequence[i+1].Text)-number-1).Any(n=>!anchors.Any(other=>Number(other.Text)==n)))continue;
     // Full column intervals preserve formula superscripts, unrecognized figures and all options.
     double Top(OcrLine line){double top=line.Y-(expandedBoxes?Math.Max(2,line.Height*.08):Compact(line.Text).Length<16&&Compact(line.Text).Contains("分")?6:Math.Max(10,line.Height*(Regex.IsMatch(Compact(line.Text),@"^\d{1,3}[.·、．]?$")?1.0:.4)));if(expandedBoxes&&line.Height>32){var raised=lines.Where(v=>v!=line&&v.X>line.X+line.Width*.5&&v.X<right&&v.Y<line.Y&&v.Y+v.Height>line.Y+line.Height*.1&&v.Y>line.Y-Math.Max(20,line.Height)).ToArray();if(raised.Length>0)top=Math.Min(top,raised.Min(v=>v.Y)-2);}return Math.Max(0,top);}double top=Top(a),bottom=i+1<sequence.Length?Top(sequence[i+1]):h*.985;
     var nextSection=lines.FirstOrDefault(l=>l.Y>a.Y+a.Height&&l.Y<bottom&&Section(l));if(nextSection!=null)bottom=nextSection.Y-Math.Max(8,nextSection.Height*.3);
     var footer=lines.FirstOrDefault(l=>l.Y>a.Y+a.Height&&l.Y<bottom&&Footer(l,h));if(footer!=null)bottom=footer.Y-12;
     var body=lines.Where(l=>l.Y>=a.Y-a.Height*.7&&l.Y<bottom&&l.X>=left&&l.X<right&&!Footer(l,h)).ToArray();
     if(body.Length==0||bottom-top<32||body.Count(l=>Regex.IsMatch(Compact(l.Text),@"^A[.·、．]"))>1)continue;
     if(body.Any(l=>l!=a&&Number(l.Text)!=number&&l.Y>a.Y+a.Height*2&&Math.Abs(l.X-a.X)<cw*.018&&Regex.IsMatch(Compact(l.Text),@"^[lI][.·、]|^(?:\d{1,3})[.·、]")))continue;
     int lft=(int)Math.Floor(left*ow/w),tp=(int)Math.Floor(top*oh/h),rt=(int)Math.Ceiling(right*ow/w),bt=(int)Math.Ceiling(bottom*oh/h);if(rt-lft>=32&&bt-tp>=32)result.Add(new RegionCandidate{Bounds=new Int32Rect(lft,tp,rt-lft,bt-tp),Confidence=.68});
    }
   }
   return result;
  }
 }
}

