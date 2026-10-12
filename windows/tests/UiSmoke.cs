using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Automation;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

class UiSmoke {
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr insert,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")] static extern void keybd_event(byte vk,byte scan,uint flags,UIntPtr extra);
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Drawing.Point p);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [StructLayout(LayoutKind.Sequential)] struct RECT {public int L,T,R,B;}
 static string root;static List<object> results=new List<object>();static Process app;
 static void Check(string name,bool ok){results.Add(new{name=name,pass=ok});if(!ok)throw new Exception(name);}
 static void Pump(int ms){var t=Stopwatch.StartNew();while(t.ElapsedMilliseconds<ms){Forms.Application.DoEvents();Thread.Sleep(25);}}
 static void Toggle(){keybd_event(0x11,0,0,UIntPtr.Zero);keybd_event(0x12,0,0,UIntPtr.Zero);keybd_event(0x54,0,0,UIntPtr.Zero);Pump(45);keybd_event(0x54,0,2,UIntPtr.Zero);keybd_event(0x12,0,2,UIntPtr.Zero);keybd_event(0x11,0,2,UIntPtr.Zero);Pump(400);}
 static AutomationElement Find(AutomationElement parent,string id){return parent.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,id));}
 static void Invoke(AutomationElement el){if(el==null)throw new Exception("Missing control");((InvokePattern)el.GetCurrentPattern(InvokePattern.Pattern)).Invoke();Pump(250);}
 static AutomationElement Window(string title){return AutomationElement.RootElement.FindFirst(TreeScope.Children,new PropertyCondition(AutomationElement.NameProperty,title));}
 static int Count(){var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"evidence/ui-state.json")));return Convert.ToInt32(data["count"]);}
 static void Picture(string path,int n){Fixtures.Create(path,n);}
 // Use only the app's own render output. Never include the user's desktop in QA artifacts.
 static void Screenshot(AutomationElement win,string name){string own=Path.Combine(root,"evidence/"+(name=="annotation"?"annotation":"shelf-clean")+".png");if(File.Exists(own)&&name!="annotation")File.Copy(own,Path.Combine(root,"evidence/"+name+".png"),true);}
 [STAThread] static int Main(string[] args){
  root=Path.GetFullPath(args[1]);Directory.CreateDirectory(Path.Combine(root,"fixtures"));Directory.CreateDirectory(Path.Combine(root,"evidence"));Directory.CreateDirectory(Path.Combine(root,"data"));
  Directory.CreateDirectory(Path.Combine(root,"watch"));
  File.WriteAllText(Path.Combine(root,"data/settings.json"),new JavaScriptSerializer().Serialize(new{ReducedMotion=true,Pinned=true,MonitorClipboard=true,WatchFolder=Path.Combine(root,"watch"),Files=new string[0]}));
  var clipboardBackup=new Forms.DataObject();try{var old=Forms.Clipboard.GetDataObject();if(old!=null)foreach(var f in old.GetFormats(false)){try{clipboardBackup.SetData(f,old.GetData(f,false));}catch(Exception){}}}catch(Exception){}
  var pointer=Forms.Cursor.Position;
  bool moveDrop=false;string receivedFile=null;var dropLabel=new Forms.Label{Text="SYNTHETIC DROP TARGET",BackColor=Drawing.Color.FromArgb(77,93,112),ForeColor=Drawing.Color.White,Location=new Drawing.Point(70,400),Size=new Drawing.Size(400,200),AllowDrop=true};
  dropLabel.DragEnter+=delegate(object s,Forms.DragEventArgs e){e.Effect=e.Data.GetDataPresent(Forms.DataFormats.FileDrop)?(moveDrop?Forms.DragDropEffects.Move:Forms.DragDropEffects.Copy):Forms.DragDropEffects.None;};
  dropLabel.DragOver+=delegate(object s,Forms.DragEventArgs e){e.Effect=moveDrop?Forms.DragDropEffects.Move:Forms.DragDropEffects.Copy;};
  dropLabel.DragDrop+=delegate(object s,Forms.DragEventArgs e){var paths=(string[])e.Data.GetData(Forms.DataFormats.FileDrop);receivedFile=Path.Combine(root,"fixtures/dropped-"+(moveDrop?"move":"copy")+"-"+Guid.NewGuid().ToString("N")+".png");if(moveDrop)File.Move(paths[0],receivedFile);else File.Copy(paths[0],receivedFile,true);e.Effect=moveDrop?Forms.DragDropEffects.Move:Forms.DragDropEffects.Copy;};
  var backdrop=new Forms.Form{Text="Screenline QA fixture",FormBorderStyle=Forms.FormBorderStyle.None,StartPosition=Forms.FormStartPosition.Manual,Bounds=Forms.Screen.FromPoint(pointer).Bounds,BackColor=Drawing.Color.FromArgb(42,54,69),ShowInTaskbar=false,TopMost=true};
  backdrop.Controls.Add(dropLabel);
  backdrop.Show();Pump(300);
  int exit=0;
  try{
   var paths=new List<string>();for(int i=0;i<3;i++){string p=Path.Combine(root,"fixtures/fixture-"+i+".png");Picture(p,i);paths.Add(p);}
   app=Process.Start(new ProcessStartInfo(args[0],"--data-dir \""+Path.Combine(root,"data")+"\" --qa-dir \""+Path.Combine(root,"evidence")+"\" \""+String.Join("\" \"",paths)+"\""){UseShellExecute=false});
   AutomationElement shelf=null;for(int i=0;i<80&&shelf==null;i++){Pump(100);shelf=Window("Screenline · 截图晾台");if(app.HasExited)throw new Exception("App exited at startup");}
   Check("真实 WPF 窗口启动",shelf!=null);Pump(500);Check("导入三张合成截图",Count()==3);Screenshot(shelf,"shelf");
   Invoke(Find(shelf,"Clip-0"));Check("点击复制传递原始图片尺寸",Forms.Clipboard.ContainsImage()&&Forms.Clipboard.GetImage().Width==720);Check("应用自身复制不会重复收纳",Count()==3);
   Invoke(Find(shelf,"Remove-0"));Check("移除更新可见列表",Count()==2);Check("移除保留导入原文件",File.Exists(paths[0])&&File.Exists(paths[1])&&File.Exists(paths[2]));
   using(var bmp=new Drawing.Bitmap(paths[2]))Forms.Clipboard.SetImage(bmp);Pump(650);Check("外部剪贴板同图跨像素格式去重",Count()==3);
   using(var bmp=new Drawing.Bitmap(paths[2]))Forms.Clipboard.SetImage(bmp);Pump(650);Check("重复系统剪贴板消息不重复收纳",Count()==3);
   IntPtr hwnd=new IntPtr(shelf.Current.NativeWindowHandle);PostMessage(hwnd,0x0312,new IntPtr(1),IntPtr.Zero);Pump(300);Check("收起窗口",!IsVisible(hwnd));PostMessage(hwnd,0x0312,new IntPtr(1),IntPtr.Zero);Pump(350);Check("Win32 热键消息展开",IsVisible(hwnd));
   Toggle();Check("实际全局 Ctrl+Alt+T 收起",!IsVisible(hwnd));Toggle();Check("实际全局 Ctrl+Alt+T 展开",IsVisible(hwnd));
   shelf=Window("Screenline · 截图晾台");
   var card=Find(shelf,"Clip-0");var r=card.Current.BoundingRectangle;var pressPoint=new Drawing.Point((int)(r.Left+r.Width/2),(int)(r.Top+r.Height/2));SetCursorPos(pressPoint.X,pressPoint.Y);Pump(160);SetCursorPos(pressPoint.X,pressPoint.Y);var pressHit=WindowFromPoint(pressPoint);uint pressPid;GetWindowThreadProcessId(pressHit,out pressPid);Console.WriteLine(new JavaScriptSerializer().Serialize(new{press_probe=new{bounds=r.ToString(),hit=pressHit.ToInt64(),pid=pressPid,app_pid=app.Id,shelf=shelf.Current.NativeWindowHandle,style=GetWindowLongPtr(new IntPtr(shelf.Current.NativeWindowHandle),-20).ToInt64()}}));mouse_event(2,0,0,0,UIntPtr.Zero);Pump(600);mouse_event(4,0,0,0,UIntPtr.Zero);Pump(350);
   var editor=Window("Screenline 标注");Check("真实鼠标长按打开标注",editor!=null);Screenshot(editor,"annotation");
   var er=editor.Current.BoundingRectangle;SetCursorPos((int)(er.Left+er.Width*.3),(int)(er.Top+er.Height*.45));mouse_event(2,0,0,0,UIntPtr.Zero);
   for(int j=0;j<12;j++){SetCursorPos((int)(er.Left+er.Width*.3+j*10),(int)(er.Top+er.Height*.45+j*5));Pump(25);}mouse_event(4,0,0,0,UIntPtr.Zero);Pump(150);
   Invoke(Find(editor,"SaveEdited"));Pump(350);Check("标注保存为新的截图",Count()==4);Check("标注不覆盖原始文件",File.Exists(paths[2]));
   shelf=Window("Screenline · 截图晾台");SetWindowPos(new IntPtr(shelf.Current.NativeWindowHandle),new IntPtr(-1),0,0,0,0,0x1|0x2|0x10);Pump(200);Screenshot(shelf,"shelf-annotated");
   card=Find(shelf,"Clip-0");Drag(card,dropLabel);Check("真实跨进程 OLE 拖拽复制文件",receivedFile!=null&&File.Exists(receivedFile));Check("拖拽复制后图片留在晾台",Count()==4);
   moveDrop=true;receivedFile=null;Drag(Find(shelf,"Clip-0"),dropLabel);Pump(900);Check("真实跨进程 OLE 拖拽移动文件",receivedFile!=null&&File.Exists(receivedFile));Check("已移动的缓存从晾台移除",Count()==3);
   string watched=Path.Combine(root,"watch/watched-"+Guid.NewGuid().ToString("N")+".png");using(var bm=new Drawing.Bitmap(paths[0])){bm.SetPixel(1,1,Drawing.Color.Red);bm.Save(watched,Drawing.Imaging.ImageFormat.Png);}Pump(1500);Check("真实 FileSystemWatcher 收纳新增图片",Count()==4);Check("目录监听保留原始文件",File.Exists(watched));
   PostMessage(new IntPtr(shelf.Current.NativeWindowHandle),0x0010,IntPtr.Zero,IntPtr.Zero);app.WaitForExit(4000);Check("退出保存状态",app.HasExited);
   app=Process.Start(new ProcessStartInfo(args[0],"--data-dir \""+Path.Combine(root,"data")+"\" --qa-dir \""+Path.Combine(root,"evidence")+"\""){UseShellExecute=false});Pump(1500);Check("重启恢复全部图片",Count()==4);
   var again=Window("Screenline · 截图晾台");PostMessage(new IntPtr(again.Current.NativeWindowHandle),0x0010,IntPtr.Zero,IntPtr.Zero);app.WaitForExit(4000);
   string edgeData=Path.Combine(root,"edge-data");Directory.CreateDirectory(edgeData);File.WriteAllText(Path.Combine(edgeData,"settings.json"),"{\"ReducedMotion\":true,\"Pinned\":false,\"MonitorClipboard\":false,\"Files\":[]}");
   backdrop.Bounds=new Drawing.Rectangle(50,50,900,700);backdrop.Activate();app=Process.Start(new ProcessStartInfo(args[0],"--data-dir \""+edgeData+"\""){UseShellExecute=false});Pump(900);
   var edgeWin=Window("Screenline · 截图晾台");IntPtr edgeHandle=new IntPtr(edgeWin.Current.NativeWindowHandle);PostMessage(edgeHandle,0x0312,new IntPtr(1),IntPtr.Zero);Pump(250);backdrop.Activate();var screen=Forms.Screen.FromPoint(pointer);SetCursorPos(screen.Bounds.Left+screen.Bounds.Width/2,screen.Bounds.Top);Pump(700);Check("正常运行屏幕顶边自动展开",IsVisible(edgeHandle));
   SetCursorPos(screen.Bounds.Left+screen.Bounds.Width/2,screen.Bounds.Bottom-70);Pump(1600);Check("鼠标移开后自动收起",!IsVisible(edgeHandle));
   backdrop.Bounds=screen.Bounds;backdrop.Activate();SetCursorPos(screen.Bounds.Left+screen.Bounds.Width/2,screen.Bounds.Top);Pump(700);Check("全屏前台窗口抑制顶部展开",!IsVisible(edgeHandle));
   PostMessage(edgeHandle,0x0010,IntPtr.Zero,IntPtr.Zero);app.WaitForExit(4000);
  }catch(Exception ex){results.Add(new{name="异常",pass=false,error=ex.ToString()});exit=1;}
  finally{try{if(app!=null&&!app.HasExited){app.CloseMainWindow();if(!app.WaitForExit(1000))app.Kill();}}catch(Exception){}
   try{Forms.Clipboard.SetDataObject(clipboardBackup,true);}catch(Exception){}SetCursorPos(pointer.X,pointer.Y);backdrop.Close();
   File.WriteAllText(Path.Combine(root,"evidence/ui-tests.json"),new JavaScriptSerializer().Serialize(results));}
  return exit;
 }
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
 static void Drag(AutomationElement card,Forms.Control target){var shelf=Window("Screenline · 截图晾台");SetWindowPos(new IntPtr(shelf.Current.NativeWindowHandle),new IntPtr(-1),0,0,0,0,0x1|0x2|0x10);Pump(180);var r=card.Current.BoundingRectangle;int x=(int)(r.Left+r.Width/2),y=(int)(r.Top+r.Height/2);var tr=AutomationElement.FromHandle(target.Handle).Current.BoundingRectangle;results.Add(new{name="拖拽坐标",pass=true,source=r.ToString(),target=tr.ToString()});var end=new Drawing.Point((int)(tr.Left+tr.Width/2),(int)(tr.Top+tr.Height/2));SetCursorPos(x,y);Pump(160);SetCursorPos(x,y);mouse_event(2,0,0,0,UIntPtr.Zero);Pump(100);for(int i=1;i<=25;i++){SetCursorPos(x+(end.X-x)*i/25,y+(end.Y-y)*i/25);Pump(35);}mouse_event(4,0,0,0,UIntPtr.Zero);Pump(600);}
 static bool IsVisible(IntPtr hwnd){return IsWindowVisible(hwnd);}
}
