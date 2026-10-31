using System;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
namespace PhotoTutor {
 // Uses the installed Windows OCR language pack; no model, service, package download or GPU.
 internal static class WindowsOcr {
  internal static int Reads;
  internal static JObject Read(BitmapSource source,CancellationToken token,string? language=null){
   Interlocked.Increment(ref Reads);
   var engine=language==null?OcrEngine.TryCreateFromUserProfileLanguages():OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language(language));
   // The Windows display/profile language is not necessarily an installed OCR language.
   if(engine==null&&language==null){var installed=OcrEngine.AvailableRecognizerLanguages;var preferred=installed.FirstOrDefault(v=>v.LanguageTag.StartsWith("zh",StringComparison.OrdinalIgnoreCase))??installed.FirstOrDefault(v=>v.LanguageTag.StartsWith("en",StringComparison.OrdinalIgnoreCase))??installed.FirstOrDefault();if(preferred!=null)engine=OcrEngine.TryCreateFromLanguage(preferred);}
   if(engine==null)return new JObject{["available"]=false,["lines"]=new JArray(),["installed"]=new JArray(OcrEngine.AvailableRecognizerLanguages.Select(v=>v.LanguageTag))};
   var converted=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);int w=converted.PixelWidth,h=converted.PixelHeight,stride=checked(w*4);var pixels=new byte[checked(stride*h)];converted.CopyPixels(pixels,stride,0);
   using(var bitmap=SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(),BitmapPixelFormat.Bgra8,w,h,BitmapAlphaMode.Ignore))using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){
    timeout.CancelAfter(TimeSpan.FromSeconds(18));var result=engine.RecognizeAsync(bitmap).AsTask(timeout.Token).GetAwaiter().GetResult();token.ThrowIfCancellationRequested();
    return new JObject{["available"]=true,["language"]=engine.RecognizerLanguage.LanguageTag,["angle"]=result.TextAngle,["lines"]=new JArray(result.Lines.Select(line=>new JObject{["text"]=line.Text,["words"]=new JArray(line.Words.Select(word=>new JObject{["text"]=word.Text,["x"]=word.BoundingRect.X,["y"]=word.BoundingRect.Y,["width"]=word.BoundingRect.Width,["height"]=word.BoundingRect.Height}))}))};
   }
  }
 }
}
