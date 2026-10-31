using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Collections.Generic;
#if NET6_0_OR_GREATER
using System.Text.Json;
#else
using System.Web.Script.Serialization;
#endif

namespace ClassroomAiBridge {
 public static class BudgetGate {
  public static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"ClassroomAI_20261024");
  public static decimal Reserve(decimal amount,string purpose){
   Directory.CreateDirectory(Root);string file=Path.Combine(Root,"测试预算记录.tsv");
   using(var f=new FileStream(file,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
    string text;using(var reader=new StreamReader(f,Encoding.UTF8,true,1024,true))text=reader.ReadToEnd();
    decimal sum=0;foreach(string row in text.Split('\n')){var a=row.Trim().Split('\t');decimal cost;if(a.Length>=2&&Decimal.TryParse(a[1],NumberStyles.Number,CultureInfo.InvariantCulture,out cost))sum+=cost;}
    if(sum+amount>4.00m)throw new Exception("本轮测试预算已到安全上限，已停止付费调用。需核对后手动开启下一轮。");
    f.Seek(0,SeekOrigin.End);byte[] data=Encoding.UTF8.GetBytes(DateTime.Now.ToString("O")+"\t"+amount.ToString(CultureInfo.InvariantCulture)+"\t"+purpose+"\n");f.Write(data,0,data.Length);f.Flush();return sum+amount;
   }
  }
 }
 public sealed class ImageReply {public string FilePath;public double ApiSeconds;public bool Cached;public string Model;}
 public static class ImageApi {
  public const string FlashModel="doubao-seedream-5-0-flash-260915";
  static readonly HttpClient Http=new HttpClient(){Timeout=Timeout.InfiniteTimeSpan};
  public static async Task<ImageReply> Generate(string key,string prompt,CancellationToken token,string outputFolder=null){
   if(String.IsNullOrWhiteSpace(key))throw new Exception("尚未配置火山方舟API Key。");
   if(String.IsNullOrWhiteSpace(prompt)||prompt.Length>6000)throw new Exception("请填写1—6000字的画面要求。");
   Directory.CreateDirectory(BudgetGate.Root);string folder=String.IsNullOrWhiteSpace(outputFolder)?Path.Combine(BudgetGate.Root,"生成图片"):outputFolder;Directory.CreateDirectory(folder);
   string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(FlashModel+"|2048x1152|"+prompt))).Replace("-","").ToLowerInvariant();
   string path=Path.Combine(folder,hash+".png");if(File.Exists(path))return new ImageReply{FilePath=path,ApiSeconds=0,Cached=true,Model=FlashModel};
   var body=new{model=FlashModel,prompt=prompt,size="2048x1152",response_format="url",output_format="png",watermark=true,optimize_prompt_options=new{mode="standard"}};
#if NET6_0_OR_GREATER
   string json=JsonSerializer.Serialize(body);
#else
   string json=new JavaScriptSerializer().Serialize(body);
#endif
   BudgetGate.Reserve(.12m,"Flash生图，单张");var watch=Stopwatch.StartNew();string url;
   using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token))using(var req=new HttpRequestMessage(HttpMethod.Post,"https://ark.cn-beijing.volces.com/api/v3/images/generations")){
    timeout.CancelAfter(TimeSpan.FromSeconds(120));req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key.Trim());req.Content=new StringContent(json,Encoding.UTF8,"application/json");
    using(var response=await Http.SendAsync(req,timeout.Token)){
     string raw=await response.Content.ReadAsStringAsync();if(!response.IsSuccessStatusCode)throw new Exception("生图请求未成功：HTTP "+(int)response.StatusCode+"。请检查模型开通、余额和Key权限；没有自动重试。");
#if NET6_0_OR_GREATER
     using(var doc=JsonDocument.Parse(raw)){var a=doc.RootElement.GetProperty("data");if(a.GetArrayLength()!=1)throw new Exception("本轮仅允许单张输出。");url=a[0].GetProperty("url").GetString();}
#else
     var data=new JavaScriptSerializer(){MaxJsonLength=1000000}.Deserialize<Dictionary<string,object>>(raw);var a=(System.Collections.IList)data["data"];if(a.Count!=1)throw new Exception("本轮仅允许单张输出。");url=Convert.ToString(((Dictionary<string,object>)a[0])["url"]);
#endif
    }
    double seconds=watch.Elapsed.TotalSeconds;Uri download;if(!Uri.TryCreate(url,UriKind.Absolute,out download)||download.Scheme!="https")throw new Exception("图片地址无效。");
    using(var response=await Http.GetAsync(download,timeout.Token)){response.EnsureSuccessStatusCode();byte[] image=await response.Content.ReadAsByteArrayAsync();token.ThrowIfCancellationRequested();File.WriteAllBytes(path,image);}
    File.AppendAllText(Path.Combine(BudgetGate.Root,"生成记录.tsv"),DateTime.Now.ToString("O")+"\t"+FlashModel+"\t"+seconds.ToString("F3",CultureInfo.InvariantCulture)+"\t0.12\t"+path+"\n",Encoding.UTF8);
    return new ImageReply{FilePath=path,ApiSeconds=seconds,Cached=false,Model=FlashModel};
   }
  }
 }
}
