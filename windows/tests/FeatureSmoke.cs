using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Automation;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

class FeatureSmoke {
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern bool GetCursorPos(out Drawing.Point point);
 [DllImport("user32.dll")] static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string className,string title);
 [StructLayout(LayoutKind.Sequential)] struct NativeRect{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
 static readonly JavaScriptSerializer json=new JavaScriptSerializer();
 static readonly List<object> results=new List<object>();
 static string root,evidence;static Process app;
 static void Pump(int ms){var time=Stopwatch.StartNew();while(time.ElapsedMilliseconds<ms){Forms.Application.DoEvents();Thread.Sleep(10);}}
 static void Check(string name,bool pass,object details=null){results.Add(new{name=name,pass=pass,details=details});if(!pass)throw new Exception(name);}
 static AutomationElement Window(string title){return AutomationElement.RootElement.FindFirst(TreeScope.Children,new PropertyCondition(AutomationElement.NameProperty,title));}
 static AutomationElement Find(AutomationElement parent,string id){return parent.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,id));}
 static void Invoke(AutomationElement el){if(el==null)throw new Exception("Missing control");((InvokePattern)el.GetCurrentPattern(InvokePattern.Pattern)).Invoke();Pump(230);}
 static void Click(AutomationElement el,double fx=.5,double fy=.5){var b=el.Current.BoundingRectangle;int x=(int)(b.Left+b.Width*fx),y=(int)(b.Top+b.Height*fy);SetCursorPos(x,y);Pump(100);SetCursorPos(x,y);mouse_event(2,0,0,0,UIntPtr.Zero);Pump(40);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(160);}
 static void DoubleClick(AutomationElement el){var b=el.Current.BoundingRectangle;SetCursorPos((int)(b.Left+b.Width/2),(int)(b.Top+b.Height/2));Pump(100);for(int i=0;i<2;i++){mouse_event(2,0,0,0,UIntPtr.Zero);Pump(35);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(65);}Pump(240);}
 static Dictionary<string,object> State(){return json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(evidence,"ui-state.json")));}
 static int Count(){return Convert.ToInt32(State()["count"]);}
 static void Hotkey(){keybd_event(0x11,0,0,UIntPtr.Zero);keybd_event(0x12,0,0,UIntPtr.Zero);keybd_event(0x53,0,0,UIntPtr.Zero);keybd_event(0x53,0,2,UIntPtr.Zero);keybd_event(0x12,0,2,UIntPtr.Zero);keybd_event(0x11,0,2,UIntPtr.Zero);Pump(250);}
 static void Drag(int x,int y,int width,int height){SetCursorPos(x,y);Pump(100);SetCursorPos(x,y);Pump(30);SetCursorPos(x,y);Drawing.Point before,after;GetCursorPos(out before);mouse_event(2,0,0,0,UIntPtr.Zero);Pump(100);GetCursorPos(out after);for(int i=1;i<=18;i++){SetCursorPos(x+width*i/18,y+height*i/18);Pump(18);}SetCursorPos(x+width,y+height);Pump(60);SetCursorPos(x+width,y+height);Drawing.Point released;GetCursorPos(out released);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(180);Console.WriteLine(json.Serialize(new{drag_input=new{requested_start=new{x=x,y=y},before_press=before.ToString(),after_press=after.ToString(),before_release=released.ToString()}}));}
 static void Draw(AutomationElement surface){var b=surface.Current.BoundingRectangle;Drag((int)(b.Left+b.Width*.2),(int)(b.Top+b.Height*.25),(int)(b.Width*.55),(int)(b.Height*.4));}
 static string CopiedHash(AutomationElement editor){uint sequence=GetClipboardSequenceNumber();Invoke(Find(editor,"CopyEdited"));for(int i=0;i<100&&GetClipboardSequenceNumber()==sequence;i++)Pump(30);if(GetClipboardSequenceNumber()==sequence)throw new Exception("CopyEdited did not update the clipboard");using(var image=(Drawing.Bitmap)Forms.Clipboard.GetImage()){var bits=image.LockBits(new Drawing.Rectangle(0,0,image.Width,image.Height),Drawing.Imaging.ImageLockMode.ReadOnly,Drawing.Imaging.PixelFormat.Format32bppArgb);try{var bytes=new byte[bits.Stride*image.Height];Marshal.Copy(bits.Scan0,bytes,0,bytes.Length);using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes));}finally{image.UnlockBits(bits);}}}
 static AutomationElement Start(string exe,string data,bool reduced,bool material,string imported) {
  Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"settings.json"),json.Serialize(new{ReducedMotion=reduced,Pinned=true,MonitorClipboard=false,SystemMaterial=material,Files=new string[0]}));
  app=Process.Start(new ProcessStartInfo(exe,"--data-dir \""+data+"\" --qa-dir \""+evidence+"\" \""+imported+"\""){UseShellExecute=false});AutomationElement shelf=null;
  for(int i=0;i<100&&shelf==null;i++){Pump(50);shelf=Window("Screenline · 截图晾台");if(app.HasExited)throw new Exception("Native app exited");}Pump(700);return shelf;
 }
 static void Stop(AutomationElement shelf){PostMessage(new IntPtr(shelf.Current.NativeWindowHandle),0x10,IntPtr.Zero,IntPtr.Zero);app.WaitForExit(3000);if(!app.HasExited)throw new Exception("App did not exit");}
 [STAThread] static int Main(string[] args) {
  root=Path.GetFullPath(args[1]);evidence=Path.Combine(root,"evidence");Directory.CreateDirectory(evidence);
  var backup=new Forms.DataObject();try{var old=Forms.Clipboard.GetDataObject();if(old!=null)foreach(string format in old.GetFormats(false))try{backup.SetData(format,old.GetData(format,false));}catch{}}catch{}
  var pointer=Forms.Cursor.Position;var screen=Forms.Screen.FromPoint(pointer);
  string notes=Path.Combine(root,"notes.png"),landscape=Path.Combine(root,"landscape.png");Fixtures.Create(notes,0);Fixtures.Create(landscape,1);
  var background=new Forms.Form{FormBorderStyle=Forms.FormBorderStyle.None,Bounds=screen.Bounds,StartPosition=Forms.FormStartPosition.Manual,TopMost=true,ShowInTaskbar=false,BackColor=Drawing.Color.FromArgb(229,235,241),AutoScaleMode=Forms.AutoScaleMode.None};
  var picture=new Forms.PictureBox{Image=Drawing.Image.FromFile(notes),Bounds=new Drawing.Rectangle(screen.WorkingArea.Width/2-360,480,720,400),SizeMode=Forms.PictureBoxSizeMode.Normal};background.Controls.Add(picture);background.Show();background.Activate();
  int exit=0;
  try {
   var shelf=Start(args[0],Path.Combine(root,"data"),false,true,landscape);Check("新功能测试窗口启动",shelf!=null);
   int initial=Count();Hotkey();var capture=Window("Screenline 区域截图");Check("真实 Ctrl+Alt+S 打开区域选框",capture!=null);
   PostMessage(new IntPtr(capture.Current.NativeWindowHandle),0x100,new IntPtr(27),IntPtr.Zero);Pump(250);Check("Esc 取消截图且不新增图片",Window("Screenline 区域截图")==null&&Count()==initial);
   File.WriteAllText(Path.Combine(evidence,"recording.flag"),"");var recording=Stopwatch.StartNew();Hotkey();Check("取消后可以再次开始截图",Window("Screenline 区域截图")!=null);
   Drawing.Point origin=picture.PointToScreen(Drawing.Point.Empty);Drag(origin.X,origin.Y,720,400);IntPtr flying=IntPtr.Zero;for(int attempt=0;attempt<60&&flying==IntPtr.Zero;attempt++){flying=FindWindow(null,"Screenline 截图飞入");if(flying==IntPtr.Zero)Pump(20);}NativeRect flightBounds;var virtualScreen=Forms.SystemInformation.VirtualScreen;if(flying==IntPtr.Zero||!GetWindowRect(flying,out flightBounds))throw new Exception("Flight window was not observed");long flightPixels=(long)(flightBounds.Right-flightBounds.Left)*(flightBounds.Bottom-flightBounds.Top);Check("截图飞入使用局部透明窗口",flightPixels<virtualScreen.Width*virtualScreen.Height*.5,new{surface_pixels=flightPixels,desktop_pixels=virtualScreen.Width*virtualScreen.Height});Pump(1050);File.Delete(Path.Combine(evidence,"recording.flag"));recording.Stop();
   var state=State();var region=(Dictionary<string,object>)state["capture_rect"];
   Check("截图选区保存物理坐标和正确分辨率",region!=null&&Convert.ToInt32(region["x"])==origin.X&&Convert.ToInt32(region["y"])==origin.Y&&Convert.ToInt32(region["width"])==720&&Convert.ToInt32(region["height"])==400,region);
   string captured=((System.Collections.IEnumerable)state["files"]).Cast<string>().First();bool same=true;using(var actual=new Drawing.Bitmap(captured))using(var expected=new Drawing.Bitmap(notes))for(int y=0;y<400;y+=23)for(int x=0;x<720;x+=31)same&=actual.GetPixel(x,y).ToArgb()==expected.GetPixel(x,y).ToArgb();
   Check("截图像素与合成选区一致且不包含选框",same);Check("截图副本和剪贴板均保留原始尺寸",Count()==initial+1&&Forms.Clipboard.ContainsImage());
   Check("从实际选区飞入并落到照片位置",Convert.ToInt32(state["flights"])==1&&Window("Screenline 截图飞入")==null);
   var frameFiles=Directory.GetFiles(Path.Combine(evidence,"frames"),"frame-*.png").Where(f=>File.GetLastWriteTime(f)>DateTime.Now.AddMilliseconds(-recording.ElapsedMilliseconds-700)).OrderBy(f=>f).ToArray();int distinct;using(var sha=SHA256.Create())distinct=frameFiles.Select(f=>Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(f)))).Distinct().Count();
   Check("飞入动效产生连续变化的实际渲染帧",distinct>=6,new{frames=frameFiles.Length,distinct_frames=distinct});File.WriteAllText(Path.Combine(evidence,"recording.json"),json.Serialize(new{elapsed_ms=recording.ElapsedMilliseconds,files=frameFiles.Select(Path.GetFileName).ToArray()}));
   shelf=Window("Screenline · 截图晾台");DoubleClick(Find(shelf,"Clip-0"));var editor=Window("Screenline 预览");Check("新截图可打开标注窗口",editor!=null);
   string original=CopiedHash(editor);
   foreach(string tool in new[]{"Arrow","Rectangle","Ellipse"}) {
    Invoke(Find(editor,tool));Draw(Find(editor,"InkSurface"));string edited=CopiedHash(editor);Check(tool+" 实际改变导出图像",edited!=original);
    Invoke(Find(editor,"Undo"));Check(tool+" 撤销恢复原图像素",CopiedHash(editor)==original);Invoke(Find(editor,"Redo"));Check(tool+" 重做恢复标注",CopiedHash(editor)==edited);Invoke(Find(editor,"Undo"));
   }
   Invoke(Find(editor,"Text"));((ValuePattern)Find(editor,"AnnotationText").GetCurrentPattern(ValuePattern.Pattern)).SetValue("关键区域");Pump(250);Click(Find(editor,"InkSurface"),.3,.4);Pump(150);string textHash=CopiedHash(editor);Check("中文文字标注实际写入图像",textHash!=original);Invoke(Find(editor,"Undo"));Check("文字标注可撤销并保留原图",CopiedHash(editor)==original);
   PostMessage(new IntPtr(editor.Current.NativeWindowHandle),0x10,IntPtr.Zero,IntPtr.Zero);Pump(350);shelf=Window("Screenline · 截图晾台");Stop(shelf);
   shelf=Start(args[0],Path.Combine(root,"data-reduced"),true,false,landscape);var materialHandle=new IntPtr(Convert.ToInt64(State()["material_handle"]));Check("关闭系统材质后局部合成窗口隐藏",!IsWindowVisible(materialHandle));
   Hotkey();Drag(origin.X,origin.Y,720,400);Pump(350);state=State();Check("减少动画模式完成截图且不播放飞入",Count()==2&&Convert.ToInt32(state["flights"])==0);Stop(Window("Screenline · 截图晾台"));Check("新增功能测试正常退出",app.HasExited);
  }catch(Exception ex){results.Add(new{name="异常",pass=false,error=ex.ToString()});exit=1;}
  finally {try{if(app!=null&&!app.HasExited)app.Kill();}catch{}try{Forms.Clipboard.SetDataObject(backup,true);}catch{}SetCursorPos(pointer.X,pointer.Y);background.Close();picture.Image.Dispose();File.WriteAllText(Path.Combine(evidence,"feature-tests.json"),json.Serialize(results));}
  return exit;
 }
}
