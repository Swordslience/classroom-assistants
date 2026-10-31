using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoTutor {
 internal sealed class PhotoData {
  readonly BitmapSource? image;readonly byte[]? jpeg;
  internal string? OriginalFile {get;private set;}internal int OriginalWidth,OriginalHeight;
  public BitmapSource Image {get{if(image!=null)return image;if(OriginalFile==null)throw new InvalidOperationException("原图已清空。");return DecodeOriginal(OriginalFile);}}
  public byte[] Jpeg=>jpeg??throw new InvalidOperationException("请先确认选区，再使用压缩题图。");
  public string Label {get;}
  public string DataUri=>"data:image/png;base64,"+Convert.ToBase64String(Jpeg);
  PhotoData(BitmapSource? image,byte[]? jpeg,string label){this.image=image;this.jpeg=jpeg;Label=label;}
  internal bool IsSelectionSource=>jpeg==null;
  static BitmapSource DecodeOriginal(string path){using(var stream=File.OpenRead(path)){var b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;b.StreamSource=stream;b.EndInit();b.Freeze();return b;}}
  internal static PhotoData SelectionSource(BitmapSource image,string label){if((long)image.PixelWidth*image.PixelHeight>24000000)throw new InvalidOperationException("原图超过2400万像素，请缩小拍摄范围。");return new PhotoData(image,null,label);}
  internal static PhotoData FromFileForSelection(string file){if(new FileInfo(file).Length>40L*1024*1024)throw new InvalidOperationException("原图超过40MB，请缩小拍摄范围。");int orientation;using(var stream=File.OpenRead(file)){var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None).Frames[0];if((long)frame.PixelWidth*frame.PixelHeight>24000000||frame.PixelWidth<32||frame.PixelHeight<32)throw new InvalidOperationException("原图尺寸不合适；不先缩小整页再截小字，请先裁掉无关边缘。");orientation=PhotoOrientation.Read(frame);}return SelectionSource(PhotoOrientation.Canonical(DecodeOriginal(file),orientation),"本机原始照片");}
  internal PhotoData CacheOriginal(){var source=Image;string folder=Path.Combine(Settings.DataRoot,"regions_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"owned.cache"),"QuestionWorkspace v1");string file=Path.Combine(folder,Guid.NewGuid().ToString("N")+".png");var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using(var stream=File.Create(file))encoder.Save(stream);return new PhotoData(null,null,Label){OriginalFile=file,OriginalWidth=source.PixelWidth,OriginalHeight=source.PixelHeight};}
  internal void ReleaseOriginal(){if(OriginalFile==null)return;string file=OriginalFile;OriginalFile=null;try{File.Delete(file);string folder=Path.GetDirectoryName(file)!;File.Delete(Path.Combine(folder,"owned.cache"));Directory.Delete(folder,false);}catch(IOException){}catch(UnauthorizedAccessException){}}
  public static PhotoData FromFile(string path,bool high) {
   var info=new FileInfo(path);if(info.Length>40*1024*1024)throw new InvalidOperationException("原图超过40MB，请使用拍照或框选题目区域。");
   using(var stream=File.OpenRead(path)) {
    var header=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None).Frames[0];int w=header.PixelWidth,h=header.PixelHeight;
    if(w<32||h<32||(long)w*h>40000000)throw new InvalidOperationException("图片尺寸不合适，请只保留题目区域。");
     stream.Position=0;var bmp=new BitmapImage();bmp.BeginInit();bmp.CacheOption=BitmapCacheOption.OnLoad;bmp.StreamSource=stream;bmp.EndInit();bmp.Freeze();
    return Create(bmp,"本机照片",high);
   }
  }
  public static PhotoData Create(BitmapSource source,string label,bool high) {
   if(source.PixelWidth<32||source.PixelHeight<32)throw new InvalidOperationException("取图区域太小，请重新框选完整题目。");
   if((long)source.PixelWidth*source.PixelHeight>24000000)throw new InvalidOperationException("题图超出安全内存范围，请只选需要讲解的区域。");
   BitmapSource image=Flatten(source);byte[] bytes=Encode(image);
   if(bytes.Length>12*1024*1024)throw new InvalidOperationException("无损题图超过12MB，请减少无关区域；原始照片仍保留。");
   return new PhotoData(image,bytes,label);
  }
  public PhotoData Rotate(bool high)=>Create(new TransformedBitmap(Image,new RotateTransform(90)),Label,high);
  internal static BitmapSource Flatten(BitmapSource source){var formatted=new FormatConvertedBitmap(source,PixelFormats.Bgr24,null,0);int stride=checked(formatted.PixelWidth*3);var pixels=new byte[checked(stride*formatted.PixelHeight)];formatted.CopyPixels(pixels,stride,0);var result=BitmapSource.Create(formatted.PixelWidth,formatted.PixelHeight,96,96,PixelFormats.Bgr24,null,pixels,stride);result.Freeze();return result;}
  internal static PhotoData CreateComposed(BitmapSource source,string label,bool high){if((long)source.PixelWidth*source.PixelHeight>10000000)throw new InvalidOperationException("组合图像素过多，请减少无关区域；原图保留。");var image=Flatten(source);var bytes=Encode(image);if(bytes.Length>12*1024*1024)throw new InvalidOperationException("无损组合图片超过12MB，请减少无关区域。");return new PhotoData(image,bytes,label);}
  public PhotoData Crop(System.Windows.Int32Rect area,bool high) {
   var original=Image;if(area.Width<32||area.Height<32||area.X<0||area.Y<0||area.X+area.Width>original.PixelWidth||area.Y+area.Height>original.PixelHeight)throw new InvalidOperationException("选区太小或超出图片，请重新框选完整题目。");
   return Create(new CroppedBitmap(original,area),Label+" · 已框选",high);
  }
  static byte[] Encode(BitmapSource source) {
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));
   using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}
  }
 }
}
