namespace PhotoTutor;
internal readonly record struct PixelRect(int X,int Y,int Width,int Height);
internal sealed class OcrLine {internal string Text="";internal double X,Y,Width,Height;}
internal sealed class RegionCandidate {internal PixelRect Bounds;internal double Confidence;}
