using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace PhotoTutor {
 internal static class SegmentationRegressionChecks {
  internal static int Run(string folder){Directory.CreateDirectory(folder);var checks=new List<object>();int failed=0;void Check(string name,Action action){try{action();checks.Add(new{name,passed=true});}catch(Exception error){failed++;checks.Add(new{name,passed=false,error=error.Message});}}
   OcrLine Line(string text,double x,double y,double width=250,double height=24)=>new OcrLine{Text=text,X=x,Y=y,Width=width,Height=height};
   Check("two exercise columns shifted by a partial left page remain valid columns",()=>{var lines=new[]{Line("上一页的课内知识摘录",30,80,180),Line("这是无题号的前页内容",35,150,180),Line("1.已知一条直线，求对应关系",360,120),Line("2.已知一个图形，判断性质",360,420),Line("3.已知一个点，求满足条件的圆",700,120),Line("4.请根据图像说明结论",700,420)};var found=QuestionSegmentation.Split(lines,1100,900,1100,900,true);if(found.Count!=4||found.Count(v=>v.Bounds.X>300&&v.Bounds.X<400)!=2)throw new Exception("offset exercise column was filtered");});
   Check("fraction digits inside full-width questions cannot invent a second column",()=>{var lines=new[]{Line("7.已知一个三角函数，求解",40,120,1600,55),Line("8.已知双曲线，求离心率",40,500,1500,55),Line("9.已知一个复数，求模",40,1000,1600,55),Line("4",800,210,40,45),Line("4",810,560,40,45),Line("求得的式子如下",830,280,500,40)};var found=QuestionSegmentation.Split(lines,1800,1600,1800,1600,true);if(found.Count!=3||found.Any(v=>v.Bounds.X>100))throw new Exception("equation digits became question columns");});
   Check("duplicate recognized parent numbers within a question do not split its subparts",()=>{var lines=new[]{Line("6.已知一个圆，求圆心",50,100),Line("7.已知两条直线，求交点",50,250),Line("8.已知坐标，请判断条件",50,400),Line("9.矩形的对角线相交于一点",50,550),Line("9.点在边所在的直线上",54,620),Line("请据此求出边的长度",55,700)};var found=QuestionSegmentation.Split(lines,800,900,800,900,true);if(found.Count!=4||found.Last().Bounds.Y>560||found.Last().Bounds.Y+found.Last().Bounds.Height<720)throw new Exception("a repeated number cut off a subpart");});
   Check("an unrecognized intermediate question is never merged across a section heading",()=>{var lines=new[]{Line("6.已知一个圆，求圆心",50,100),Line("7.已知两条直线，求交点",50,250),Line("8的题号被遮挡，正文仍在",50,400),Line("二、填空题",50,500),Line("9.已知坐标，请判断条件",50,600)};var found=QuestionSegmentation.Split(lines,800,900,800,900,true);if(found.Any(v=>v.Bounds.Y<270&&v.Bounds.Y+v.Bounds.Height>400))throw new Exception("unknown question was swallowed into question7");});
   File.WriteAllText(Path.Combine(folder,"横放与题目完整性规则回归.json"),JsonConvert.SerializeObject(new{failed,checks,cloudModelCalls=0},Formatting.Indented));return failed==0?0:1;
  }
 }
}
