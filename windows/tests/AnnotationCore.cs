// Exercise exported WPF pixels and full-state undo without taking the mouse or showing a window.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Screenline;
class AnnotationCore {
 static readonly List<object> results=new List<object>();
 static object Call(object target,string method,params object[] args){return target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Invoke(target,args);}
 static object Field(object target,string field){return target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
 static void Check(string name,bool pass){results.Add(new{name=name,pass=pass});if(!pass)throw new Exception(name);}
 static string Hash(ImageWindow window){var b=(BitmapSource)Call(window,"Render");var pixels=new byte[b.PixelWidth*b.PixelHeight*4];b.CopyPixels(pixels,b.PixelWidth*4,0);using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(pixels));}
 static bool GeometryMatches(string tool,BitmapSource before,BitmapSource after){int width=before.PixelWidth;var a=new byte[width*before.PixelHeight*4];var b=new byte[a.Length];before.CopyPixels(a,width*4,0);after.CopyPixels(b,width*4,0);Func<int,int,bool> changed=(x,y)=>{for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++){int offset=((y+dy)*width+x+dx)*4;if(Math.Abs(a[offset]-b[offset])+Math.Abs(a[offset+1]-b[offset+1])+Math.Abs(a[offset+2]-b[offset+2])>40)return true;}return false;};if(tool=="arrow"){for(int i=1;i<10;i++)if(!changed(120+44*i,100+20*i))return false;return true;}if(changed(340,200))return false;if(tool=="ellipse")return changed(340,100)&&changed(560,200)&&changed(340,300)&&changed(120,200)&&!changed(120,100);for(int i=1;i<10;i++)if(!changed(120+44*i,100)||!changed(120+44*i,300)||!changed(120,100+20*i)||!changed(560,100+20*i))return false;return true;}
 [STAThread] static int Main(string[] args) {
  Directory.CreateDirectory(args[0]);string input=Path.Combine(args[0],"notes.png");Fixtures.Create(input,0);
  byte[] sourceBytes=File.ReadAllBytes(input);var app=new Application();var store=new ClipStore(Path.Combine(args[0],"data"));var clip=store.Import(input);var editor=new ImageWindow(clip,store,false,null);int exit=0;
  try{string original=Hash(editor);var originalBitmap=(BitmapSource)Call(editor,"Render");foreach(string tool in new[]{"arrow","rectangle","ellipse"}) {
    Call(editor,"SetTool",tool);editor.GetType().GetField("shapeStart",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(editor,new Point(120,100));Call(editor,"CommitShape",new Point(560,300));string edited=Hash(editor);
    Check(tool+" 导出图像包含正确形状",edited!=original&&GeometryMatches(tool,originalBitmap,(BitmapSource)Call(editor,"Render")));Call(editor,"Undo");Check(tool+" 撤销恢复原图像素",Hash(editor)==original);Call(editor,"Redo");Check(tool+" 重做恢复相同标注",Hash(editor)==edited);Call(editor,"Undo");
  }
  Call(editor,"SetTool","text");((TextBox)Field(editor,"textEntry")).Text="关键区域";Call(editor,"PlaceText",new Point(160,160));string text=Hash(editor);Check("中文文字真实写入输出图像",text!=original);Call(editor,"Undo");Check("文字撤销恢复原图",Hash(editor)==original);Call(editor,"Redo");Check("文字重做输出一致",Hash(editor)==text);
  Check("标注保持原始分辨率",((BitmapSource)Call(editor,"Render")).PixelWidth==720&&((BitmapSource)Call(editor,"Render")).PixelHeight==400);Check("导入源文件保持不变",sourceBytes.SequenceEqual(File.ReadAllBytes(input)));
  }catch(Exception ex){results.Add(new{name="异常",pass=false,error=ex.ToString()});exit=1;}
  File.WriteAllText(Path.Combine(args[0],"annotation-tests.json"),new JavaScriptSerializer().Serialize(results));Console.WriteLine(new JavaScriptSerializer().Serialize(results));return exit;
 }
}
