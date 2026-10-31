using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace ScienceSupport {
 // Optional fallback for syntax beyond the lightweight local WPF renderer.
 // Only the formula is sent. No lesson, image, student identity or model key.
 internal static class CloudMath {
  static string FormulaEndpoint() {
   string value=System.Environment.GetEnvironmentVariable("CLASSROOM_SERVICE_URL")??"";
   System.Uri root;
   if(!System.Uri.TryCreate(value,System.UriKind.Absolute,out root)||root.Scheme!="https"||root.AbsolutePath!="/"||root.UserInfo.Length>0||root.Query.Length>0||root.Fragment.Length>0)
    throw new System.InvalidOperationException("尚未配置自建公式服务：请设置 CLASSROOM_SERVICE_URL 为 HTTPS 根地址。原始公式仍会保留。");
   return new System.Uri(root,"/api/formula").AbsoluteUri;
  }
  static readonly HttpClient client=new HttpClient{Timeout=TimeSpan.FromSeconds(10)};
  static readonly SemaphoreSlim gate=new SemaphoreSlim(2);
  static readonly Dictionary<string,byte[]> cache=new Dictionary<string,byte[]>();
  public static bool Prefer(string s)=>System.Text.RegularExpressions.Regex.IsMatch(s,@"\\vec\{[A-Za-z]{2}")||System.Text.RegularExpressions.Regex.IsMatch(s,@"\\(?:mathbb|ce|pu|chemfig|overset|underset|overrightarrow|overleftarrow|overleftrightarrow|xrightarrow|xrightleftharpoons|prescript|dv|pdv|qty|SI|si|cancel|boxed|begin|ket|bra|braket|unicode)\b");
  static readonly Dictionary<string,Task<byte[]>> pending=new Dictionary<string,Task<byte[]>>();
  internal static Func<string,double,Task<byte[]>>? TestRenderer;
  public static Task<byte[]> Render(string formula,double size){if(TestRenderer!=null)return TestRenderer(formula,size);string key=formula+"|"+size;lock(pending){if(pending.TryGetValue(key,out var active))return active;var task=RenderCore(formula,size);pending[key]=task;_ = task.ContinueWith(_=>{lock(pending)pending.Remove(key);});return task;}}
  static async Task<byte[]> RenderCore(string formula,double size){
   if(formula.Length>6000)throw new InvalidOperationException("公式太长，请分行");
   string key=formula+"|"+size.ToString(System.Globalization.CultureInfo.InvariantCulture);lock(cache){if(cache.TryGetValue(key,out var bytes))return bytes;}
   await gate.WaitAsync().ConfigureAwait(false);try{lock(cache){if(cache.TryGetValue(key,out var bytes))return bytes;}
    string json="{\"formula\":\""+Escape(LayoutSource(formula))+"\",\"size\":"+Math.Max(24,Math.Min(72,size)).ToString(System.Globalization.CultureInfo.InvariantCulture)+"}";
    using(var response=await client.PostAsync(FormulaEndpoint(),new StringContent(json,Encoding.UTF8,"application/json")).ConfigureAwait(false)){response.EnsureSuccessStatusCode();var bytes=await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);if(bytes.Length<8||bytes.Length>2000000||bytes[0]!=137||bytes[1]!=80)throw new InvalidOperationException("公式图片无效");lock(cache){if(cache.Count>=64)cache.Clear();cache[key]=bytes;}return bytes;}
   }finally{gate.Release();}
  }
  internal static string LayoutSource(string formula)=>System.Text.RegularExpressions.Regex.Replace(formula,@"\\vec\{([A-Za-z]{2,}(?:_(?:\{[A-Za-z0-9]+\}|[A-Za-z0-9]+))?)\}",m=>@"\overrightarrow{"+m.Groups[1].Value+"}");
  static string Escape(string s){var b=new StringBuilder();foreach(char c in s){switch(c){case '\\':b.Append("\\\\");break;case '"':b.Append("\\\"");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\u"+((int)c).ToString("x4"));else b.Append(c);break;}}return b.ToString();}
 }
}
