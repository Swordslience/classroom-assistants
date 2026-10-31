using System;
using System.Text.RegularExpressions;

namespace ScienceSupport {
 internal sealed class PracticeQuestion {
  public string Title {get;set;}="相似练习";
  public string Problem {get;set;}="";
  public string Hint {get;set;}="";
  public string Answer {get;set;}="";
  public string Solution {get;set;}="";
  public void Validate(){if(string.IsNullOrWhiteSpace(Problem)||string.IsNullOrWhiteSpace(Answer)||string.IsNullOrWhiteSpace(Solution))throw new InvalidOperationException("生成的练习不完整，请重试；未展示不完整答案。");if(Problem.Length>12000||Solution.Length>30000)throw new InvalidOperationException("练习内容异常，未展示，请重试。");}
  public string Display(int reveal){string body=reveal==1?"## 提示\n"+Hint+"\n\n## 题目\n"+Problem:reveal==2?"## 答案\n"+Answer+"\n\n## 题目\n"+Problem:reveal>=3?"## 解析\n"+Solution+"\n\n## 答案\n"+Answer+"\n\n## 题目\n"+Problem:Problem;return "# "+Title+"\n\n"+body;}
  public static string SystemPrompt=>"你是高中跨学科举一反三命题教师。根据提供的真实题目及图像，仅生成一道同知识点、条件完备且可解的相似题，先自己完整求解核对再输出。难度相近，不把原题换名字当新题。缺少原题条件时明确让用户补充，不猜数值。不得出现没有配图的‘如图’；新题必须自足，优先无需新图即可描述的变式，复杂有机结构用无歧义结构简式，并写清双键、支链、电荷及反应条件。数学用标准LaTeX；化学优先Unicode下标、电荷和箭头，不用\\ce、chemfig、mhchem、HTML。只返回JSON对象，字段Title,Problem,Hint,Answer,Solution，全部为字符串，JSON内反斜杠必须转义。Problem仅含题干，不含答案、解析或暗示最终数值；Hint只给方向不揭示答案。Answer给明确结果，Solution完整推导。不要用代码块或联网补充题干外事实。";
  public static string JsonBody(string raw){raw=raw.Trim();int a=raw.IndexOf('{'),b=raw.LastIndexOf('}');if(a<0||b<=a)throw new InvalidOperationException("模型没有返回有效练习，未展示，请重试。");return raw.Substring(a,b-a+1);}
 }
 internal static class ScientificNotation {
  public static string Normalize(string value){value=value.Replace(@"\dfrac",@"\frac").Replace(@"\tfrac",@"\frac").Replace(@"\qquad",@"\;").Replace(@"\ ",@"\;").Replace(@"\quad",@"\;").Replace(@"\displaystyle","").Replace(@"\textstyle","");
   // WpfMath lacks the TeX fixed-size delimiter commands. Keep the actual
   // delimiter (including escaped braces and bars); remove only its size hint.
   value=Regex.Replace(value,@"\\(?:big|Big|bigg|Bigg)[lrm]?\b","");
   return Regex.Replace(value,@"\\sqrt\s*([A-Za-z0-9])(?![A-Za-z])",m=>@"\sqrt{"+m.Groups[1].Value+"}");}
  public static bool TryChemistry(string expression,out string plain){
   plain=expression;
   if(!Regex.IsMatch(expression,@"\\(?:ce|overset|underset|xrightarrow|xrightleftharpoons)\b"))return false;
   // Small supported subset only. Unknown chemistry commands retain visible source.
   plain=Regex.Replace(plain,@"\\(?:over|under)set\{([^{}]*)\}\{(\\(?:rightleftharpoons|leftrightharpoons|rightarrow|leftarrow))\}",m=>m.Groups[2].Value+"（"+m.Groups[1].Value+"）");
   plain=Regex.Replace(plain,@"\\xrightarrow\{([^{}]*)\}","→（$1）");
   plain=Regex.Replace(plain,@"\\(?:ce|mathrm|text)\{([^{}]*)\}","$1");
   plain=plain.Replace(@"\rightleftharpoons","⇌").Replace(@"\leftrightharpoons","⇋").Replace(@"\rightarrow","→").Replace(@"\leftarrow","←").Replace(@"\Delta","Δ").Replace(@"\cdot","·");
   plain=plain.Replace("<=>","⇌").Replace("->","→");
   plain=Regex.Replace(plain,@"\^\{?([0-9+−-]+)\}?",m=>Super(m.Groups[1].Value));
   plain=Regex.Replace(plain,@"_\{?(\d+)\}?",m=>Sub(m.Groups[1].Value));
   // Bare chemical digits become atom counts; leading coefficients remain baseline.
   plain=Regex.Replace(plain,@"(?<=[A-Za-z)\]])\d+",m=>Sub(m.Value));
   if(plain.Contains("\\")){plain="公式待核对："+expression;return true;}
   return true;
  }
  static string Sub(string s){return Map(s,"0123456789+-−","₀₁₂₃₄₅₆₇₈₉₊₋₋");}
  static string Super(string s){return Map(s,"0123456789+-−","⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻⁻");}
  static string Map(string s,string from,string to){var chars=s.ToCharArray();for(int i=0;i<chars.Length;i++){int p=from.IndexOf(chars[i]);if(p>=0)chars[i]=to[p];}return new string(chars);}
 }
}
