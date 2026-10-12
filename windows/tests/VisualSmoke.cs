using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Automation;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

class VisualSmoke {
 sealed class ProbeButton:Forms.Button {public readonly List<int> Messages=new List<int>();protected override void WndProc(ref Forms.Message m){if(m.Msg==0x201||m.Msg==0x202||m.Msg==0x203)Messages.Add(m.Msg);base.WndProc(ref m);}}
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr insert,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Drawing.Point p);
 [DllImport("user32.dll")] static extern bool GetCursorPos(out Drawing.Point point);
 [DllImport("user32.dll")] static extern IntPtr GetCapture();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 static readonly JavaScriptSerializer json=new JavaScriptSerializer();
 static readonly List<object> results=new List<object>();
 static string root,evidence;static Process app;
 static void Pump(int ms){var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<ms){Forms.Application.DoEvents();Thread.Sleep(10);}}
 static void Check(string name,bool pass,object details=null){results.Add(new{name=name,pass=pass,details=details});if(!pass)throw new Exception(name);}
 static AutomationElement Window(string title){return AutomationElement.RootElement.FindFirst(TreeScope.Children,new PropertyCondition(AutomationElement.NameProperty,title));}
 static AutomationElement Find(AutomationElement parent,string id){return parent.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,id));}
 static void Invoke(AutomationElement el){if(el==null)throw new Exception("Missing control");((InvokePattern)el.GetCurrentPattern(InvokePattern.Pattern)).Invoke();Pump(230);}
 static void Click(AutomationElement el){var b=el.Current.BoundingRectangle;int x=(int)(b.Left+b.Width/2),y=(int)(b.Top+b.Height/2);SetCursorPos(x,y);Pump(160);SetCursorPos(x,y);var hit=WindowFromPoint(new Drawing.Point(x,y));uint pid;GetWindowThreadProcessId(hit,out pid);var shelf=Window("Screenline · 截图晾台");Console.WriteLine(json.Serialize(new{click_probe=new{bounds=b.ToString(),hit=hit.ToInt64(),pid=pid,app_pid=app.Id,shelf=shelf==null?0:shelf.Current.NativeWindowHandle,style=shelf==null?0:GetWindowLongPtr(new IntPtr(shelf.Current.NativeWindowHandle),-20).ToInt64()}}));mouse_event(2,0,0,0,UIntPtr.Zero);Pump(60);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(180);}
 static void DoubleClick(AutomationElement el){var b=el.Current.BoundingRectangle;SetCursorPos((int)(b.Left+b.Width/2),(int)(b.Top+b.Height/2));Pump(160);for(int i=0;i<2;i++){mouse_event(2,0,0,0,UIntPtr.Zero);Pump(35);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(65);}Pump(250);}
 static void Front(AutomationElement el){SetWindowPos(new IntPtr(el.Current.NativeWindowHandle),new IntPtr(-1),0,0,0,0,0x1|0x2|0x10);Pump(120);}
 static void Snapshot(string name){File.WriteAllText(Path.Combine(evidence,"render.request"),"");for(int i=0;i<80&&File.Exists(Path.Combine(evidence,"render.request"));i++)Pump(25);if(File.Exists(Path.Combine(evidence,"render.request")))throw new Exception("Native render did not respond");File.Copy(Path.Combine(evidence,"shelf-clean.png"),Path.Combine(evidence,name+".png"),true);}
 static Dictionary<string,object> State(){return json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(evidence,"ui-state.json")));}
 static int Count(){return Convert.ToInt32(State()["count"]);}
 static double AlphaFraction(string file){using(var b=new Drawing.Bitmap(file)){int clear=0,total=0;for(int y=0;y<b.Height;y+=4)for(int x=0;x<b.Width;x+=4){total++;if(b.GetPixel(x,y).A==0)clear++;}return (double)clear/total;}}
 static int OpaqueBelow(string file,AutomationElement shelf,AutomationElement card){var bounds=card.Current.BoundingRectangle;var window=shelf.Current.BoundingRectangle;int count=0;using(var b=new Drawing.Bitmap(file)){int x=(int)(bounds.Left-window.Left),y=(int)(bounds.Bottom-window.Top+5);for(int j=Math.Max(0,y);j<Math.Min(b.Height,y+36);j++)for(int i=Math.Max(0,x);i<Math.Min(b.Width,x+bounds.Width);i++)if(b.GetPixel(i,j).A>120)count++;}return count;}
 static Drawing.Size CopySize(AutomationElement editor){Invoke(Find(editor,"CopyEdited"));using(var picture=Forms.Clipboard.GetImage())return picture.Size;}
 static void Selection(AutomationElement surface){var b=surface.Current.BoundingRectangle;int x=(int)(b.Left+b.Width*.25),y=(int)(b.Top+b.Height*.25),dx=(int)(b.Width*.5),dy=(int)(b.Height*.5);SetCursorPos(x,y);Pump(100);mouse_event(2,0,0,0,UIntPtr.Zero);Pump(100);for(int i=1;i<=16;i++){SetCursorPos(x+dx*i/16,y+dy*i/16);Pump(18);}mouse_event(4,0,0,0,UIntPtr.Zero);Pump(180);}
 [STAThread] static int Main(string[] args) {
  root=Path.GetFullPath(args[1]);evidence=Path.Combine(root,"evidence");Directory.CreateDirectory(evidence);Directory.CreateDirectory(Path.Combine(root,"data"));Directory.CreateDirectory(Path.Combine(root,"fixtures"));
  File.WriteAllText(Path.Combine(root,"data/settings.json"),"{\"ReducedMotion\":false,\"Pinned\":true,\"MonitorClipboard\":false,\"Files\":[]}");
  var backup=new Forms.DataObject();try{var old=Forms.Clipboard.GetDataObject();if(old!=null)foreach(string format in old.GetFormats(false))try{backup.SetData(format,old.GetData(format,false));}catch(Exception){}}catch(Exception){}
  var pointer=Forms.Cursor.Position;var screen=Forms.Screen.FromPoint(pointer);
  var background=new Forms.Form{FormBorderStyle=Forms.FormBorderStyle.None,Bounds=screen.Bounds,StartPosition=Forms.FormStartPosition.Manual,BackColor=Drawing.Color.FromArgb(231,237,243),TopMost=true,ShowInTaskbar=false};
  bool through=false;var underneath=new ProbeButton{Text="Native click-through target",Location=new Drawing.Point(60,62),Size=new Drawing.Size(260,80)};underneath.Click+=delegate{through=true;};background.Controls.Add(underneath);background.Show();background.Activate();
  int exit=0;
  try {
   string portrait=Path.Combine(root,"fixtures/portrait.png"),landscape=Path.Combine(root,"fixtures/landscape.png"),notes=Path.Combine(root,"fixtures/notes.png");Fixtures.Create(portrait,3);Fixtures.Create(landscape,1);Fixtures.Create(notes,0);
   app=Process.Start(new ProcessStartInfo(args[0],"--data-dir \""+Path.Combine(root,"data")+"\" --qa-dir \""+evidence+"\" \""+portrait+"\" \""+landscape+"\" \""+notes+"\""){UseShellExecute=false});
   AutomationElement shelf=null;for(int i=0;i<100&&shelf==null;i++){Pump(80);shelf=Window("Screenline · 截图晾台");if(app.HasExited)throw new Exception("Native app exited");}
   Check("新版真实原生窗口启动",shelf!=null);Pump(2000);Check("自动展示不抢走前台焦点",GetForegroundWindow()!=new IntPtr(shelf.Current.NativeWindowHandle));
   SetCursorPos(screen.Bounds.Left+35,screen.Bounds.Top+340);Pump(180);Snapshot("transparent");double clear=AlphaFraction(Path.Combine(evidence,"transparent.png"));Check("透明留白占比超过 80%，无大底板",clear>.8,new{transparent_fraction=clear});
   var state=State();var entries=((System.Collections.IEnumerable)state["photos"]).Cast<Dictionary<string,object>>().ToArray();bool proportions=entries.Length==3;bool hasPortrait=false,hasWide=false;
   foreach(Dictionary<string,object> item in entries){using(var original=new Drawing.Bitmap((string)item["file"])){double w=Convert.ToDouble(item["width"]),h=Convert.ToDouble(item["height"]);proportions&=Math.Abs(w/h-(double)original.Width/original.Height)<.001;hasPortrait|=h>w;hasWide|=w>h;}}
   Check("横图和竖图均保持原比例",proportions&&hasPortrait&&hasWide);
   Fixtures.Composite(Path.Combine(evidence,"transparent.png"),Path.Combine(evidence,"light.png"),false);Fixtures.Composite(Path.Combine(evidence,"transparent.png"),Path.Combine(evidence,"dark.png"),true);
   SetForegroundWindow(background.Handle);SetWindowPos(background.Handle,new IntPtr(-1),0,0,0,0,0x1|0x2|0x10);Front(shelf);var target=AutomationElement.FromHandle(underneath.Handle);
   var targetBounds=target.Current.BoundingRectangle;var point=new Drawing.Point((int)(targetBounds.Left+targetBounds.Width/2),(int)(targetBounds.Top+targetBounds.Height/2));SetCursorPos(point.X,point.Y);
   bool ready=false;for(int i=0;i<30;i++){Pump(30);if(WindowFromPoint(point)==underneath.Handle){ready=true;break;}}
   if(ready)Click(target);Drawing.Point cursor;GetCursorPos(out cursor);Check("透明区域真实点击穿透到其他进程",ready&&through,new{native_target_received_hit=ready,click_received=through,messages=underneath.Messages.ToArray(),cursor=cursor.ToString(),expected=point.ToString(),mouse_captured=GetCapture()!=IntPtr.Zero,source_style=GetWindowLongPtr(new IntPtr(shelf.Current.NativeWindowHandle),-20).ToInt64()});
   var card=Find(shelf,"Clip-0");int before=OpaqueBelow(Path.Combine(evidence,"transparent.png"),shelf,card);Click(card);using(var copied=Forms.Clipboard.GetImage())Check("真实鼠标单击复制原图",copied!=null&&copied.Width==720&&copied.Height==400);
   Snapshot("copied");int after=OpaqueBelow(Path.Combine(evidence,"copied.png"),shelf,card);Check("复制反馈实际绘制在照片下方",after>before+50,new{opaque_before=before,opaque_after=after});
   // Double click from the real mouse, then exercise crop, undo, and redo through native controls.
   Pump(550);DoubleClick(card);var editor=Window("Screenline 预览");Check("真实双击打开预览",editor!=null);
   var windowPattern=(WindowPattern)editor.GetCurrentPattern(WindowPattern.Pattern);Invoke(Find(editor,"MaximizeEditor"));Check("新标题栏最大化窗口",windowPattern.Current.WindowVisualState==WindowVisualState.Maximized);Invoke(Find(editor,"MaximizeEditor"));Check("新标题栏还原窗口",windowPattern.Current.WindowVisualState==WindowVisualState.Normal);Click(Find(editor,"CloseEditor"));Check("新标题栏关闭后返回晾台",Window("Screenline 预览")==null&&Window("Screenline · 截图晾台")!=null);Front(shelf);DoubleClick(Find(shelf,"Clip-0"));editor=Window("Screenline 预览");
   Invoke(Find(editor,"Crop"));editor=Window("Screenline 标注");Selection(Find(editor,"InkSurface"));Invoke(Find(editor,"ApplyCrop"));Drawing.Size cropped=CopySize(editor);
   Check("拖选裁剪并复制正确尺寸",Math.Abs(cropped.Width-360)<=4&&Math.Abs(cropped.Height-200)<=4,new{width=cropped.Width,height=cropped.Height});
   Invoke(Find(editor,"Undo"));var restored=CopySize(editor);Check("撤销裁剪恢复原分辨率",restored.Width==720&&restored.Height==400);
   Invoke(Find(editor,"Redo"));var redone=CopySize(editor);Check("重做裁剪恢复选择区域",redone==cropped);Invoke(Find(editor,"SaveEdited"));Pump(800);Check("裁剪另存新截图并保留原文件",Count()==4&&File.Exists(notes));
   shelf=Window("Screenline · 截图晾台");Front(shelf);SetCursorPos(screen.Bounds.Left+35,screen.Bounds.Top+340);
   var recording=Stopwatch.StartNew();File.WriteAllText(Path.Combine(evidence,"recording.flag"),"");Pump(450);Invoke(Find(shelf,"Clip-0"));Pump(950);Invoke(Find(shelf,"Remove-1"));Pump(950);
   IntPtr handle=new IntPtr(shelf.Current.NativeWindowHandle);PostMessage(handle,0x0312,new IntPtr(1),IntPtr.Zero);Pump(550);PostMessage(handle,0x0312,new IntPtr(1),IntPtr.Zero);Pump(1050);
   File.Delete(Path.Combine(evidence,"recording.flag"));recording.Stop();var frameFiles=Directory.GetFiles(Path.Combine(evidence,"frames"),"frame-*.png").Where(f=>File.GetLastWriteTime(f)>DateTime.Now.AddMilliseconds(-recording.ElapsedMilliseconds-1000)).ToArray();int frames=frameFiles.Length;
   int distinct;using(var sha=System.Security.Cryptography.SHA256.Create())distinct=frameFiles.Select(f=>Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(f)))).Distinct().Count();
   Check("实际 WPF 动画生成连续变化的证据帧",frames>=24&&distinct>=12,new{frames=frames,distinct_frames=distinct});File.WriteAllText(Path.Combine(evidence,"recording.json"),json.Serialize(new{frames=frames,distinct_frames=distinct,elapsed_ms=recording.ElapsedMilliseconds,files=frameFiles.Select(f=>Path.GetFileName(f)).OrderBy(f=>f).ToArray()}));
   Check("删除后其他图片正常保留",Count()==3);Click(target);SetCursorPos(screen.Bounds.Left+35,screen.Bounds.Top+340);Pump(350);Snapshot("final-transparent");Fixtures.Composite(Path.Combine(evidence,"final-transparent.png"),Path.Combine(evidence,"final-light.png"),false);Fixtures.Composite(Path.Combine(evidence,"final-transparent.png"),Path.Combine(evidence,"final-dark.png"),true);
   PostMessage(handle,0x0010,IntPtr.Zero,IntPtr.Zero);app.WaitForExit(4000);Check("新版窗口正常退出",app.HasExited);
  } catch(Exception ex) {results.Add(new{name="异常",pass=false,error=ex.ToString()});exit=1;}
  finally {
   try{if(app!=null&&!app.HasExited){app.CloseMainWindow();if(!app.WaitForExit(1200))app.Kill();}}catch(Exception){}
   try{Forms.Clipboard.SetDataObject(backup,true);}catch(Exception){}SetCursorPos(pointer.X,pointer.Y);background.Close();
   File.WriteAllText(Path.Combine(evidence,"visual-tests.json"),json.Serialize(results));
  }
  return exit;
 }
}
