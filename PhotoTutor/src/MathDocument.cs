using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
namespace PhotoTutor {
 internal enum MathKind {Text,Inline,Block,Invalid}
 internal sealed class MathFragment {
  internal MathKind Kind;internal string Content,Source;
  internal MathFragment(MathKind kind,string content,string? source=null){Kind=kind;Content=content;Source=source??content;}
 }
 // Read delimiters independently of line position. Recovery is bounded to the
 // current formula, so an omitted terminator cannot turn later prose into math.
 internal static class MathDocument {
  internal static List<MathFragment> Parse(string text){
   var result=new List<MathFragment>();var plain=new StringBuilder();
   void Flush(){if(plain.Length>0){result.Add(new MathFragment(MathKind.Text,plain.ToString()));plain.Clear();}}
   for(int i=0;i<text.Length;){
    if(text[i]=='\\'&&i+1<text.Length&&text[i+1]=='$'&&!Escaped(text,i)){plain.Append('$');i+=2;continue;}
    string open="",close="";bool block=false;
    if(!Escaped(text,i)){
     if(text[i]=='$'){block=i+1<text.Length&&text[i+1]=='$';open=close=block?"$$":"$";}
     else if(At(text,i,@"\[")){open=@"\[";close=@"\]";block=true;}
     else if(At(text,i,@"\(")){open=@"\(";close=@"\)";}
    }
    if(open.Length==0){plain.Append(text[i++]);continue;}
    int start=i+open.Length,end=FindEnd(text,start,close,block,out int boundary);
    if(end>=0){Flush();string content=text.Substring(start,end-start);result.Add(new MathFragment(block?MathKind.Block:MathKind.Inline,content,text.Substring(i,end+close.Length-i)));i=end+close.Length;}
    else if(block){Flush();int stop=Math.Max(start,boundary);result.Add(new MathFragment(MathKind.Invalid,text.Substring(i,stop-i)));i=stop;}
    else {plain.Append(open);i=start;}
   }
   Flush();return result;
  }
  static int FindEnd(string text,int start,string close,bool block,out int boundary){
   int limit=Math.Min(text.Length,start+6000),depth=0;boundary=limit;
   for(int i=start;i<limit;i++){
    if(At(text,i,close)&&!Escaped(text,i)){
     // A single-dollar pair must not borrow half of a display delimiter.
     if(close!="$"||!At(text,i,"$$"))return i;
     boundary=i;return -1;
    }
    if(text[i]=='\n'){
     if(!block){boundary=i;return -1;}
     int next=i+1;while(next<limit&&(text[next]==' '||text[next]=='\t'||text[next]=='\r'))next++;
     if(depth==0&&next<limit&&(text[next]=='\n'||ProseLine(text,next))){boundary=i;return -1;}
    }
    if(block&&text[i]=='$'&&!Escaped(text,i)){boundary=i;return -1;}
    if(text[i]=='{'&&!Escaped(text,i))depth++;
    if(text[i]=='}'&&!Escaped(text,i))depth=Math.Max(0,depth-1);
   }
   return -1;
  }
  static bool ProseLine(string text,int at){
   int end=text.IndexOf('\n',at);if(end<0)end=text.Length;
   string line=text.Substring(at,end-at).Trim();
   if(line.StartsWith("#")||Regex.IsMatch(line,@"^[（(]\d+[）)]\s*[\u4e00-\u9fff]"))return true;
   // Chinese inside an explicit TeX group (e.g. \text{...}) remains math.
   return line.Length>0&&line[0]>='\u4e00'&&line[0]<='\u9fff';
  }
  static bool At(string text,int at,string value)=>at+value.Length<=text.Length&&string.CompareOrdinal(text,at,value,0,value.Length)==0;
  static bool Escaped(string text,int at){int count=0;for(int p=at-1;p>=0&&text[p]=='\\';p--)count++;return count%2==1;}
 }
}
