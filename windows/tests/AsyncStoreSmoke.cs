using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Screenline;

class AsyncStoreSmoke {
    static readonly List<object> results=new List<object>();
    static void Check(string name,bool pass){results.Add(new{name=name,pass=pass});if(!pass)throw new Exception(name);}
    static byte[] Pixels(BitmapSource source){var bitmap=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);return pixels;}
    static void Await(Task task){var frame=new DispatcherFrame();task.ContinueWith(delegate{Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate{frame.Continue=false;}));},TaskScheduler.FromCurrentSynchronizationContext());Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();}
    [STAThread] static int Main(string[] args){
        string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);string test=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(test);
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        int exit=0;
        try{
            string notes=Path.Combine(test,"notes.png"),landscape=Path.Combine(test,"landscape.png");Fixtures.Create(notes,0);Fixtures.Create(landscape,1);var original=File.ReadAllBytes(notes);
            var store=new ClipStore(Path.Combine(test,"data"));int uiThread=Thread.CurrentThread.ManagedThreadId;bool correctThread=true;store.Changed=delegate{correctThread&=Thread.CurrentThread.ManagedThreadId==uiThread;};
            var first=ClipStore.ReadImage(notes);var converted=new FormatConvertedBitmap(first,PixelFormats.Bgr32,null,0);converted.Freeze();
            var one=store.AddAsync(first);var duplicate=store.AddAsync(converted);var two=store.ImportAsync(landscape);var idle=store.WhenIdle;
            Check("后台导入期间可以等待队列排空",store.IsBusy&&!idle.IsCompleted);
            Await(Task.WhenAll(one,duplicate,two));Await(idle);
            Check("并发同图跨像素格式去重且保持导入顺序",store.Items.Count==2&&Object.ReferenceEquals(one.Result,duplicate.Result)&&store.Items[0]==two.Result&&Directory.GetFiles(store.Images,"*.png").Length==2);
            Check("后台任务仅在界面线程提交列表变化",correctThread&&!store.IsBusy&&store.WhenIdle.IsCompleted);
            Check("后台编码保留原始分辨率与全部像素",one.Result.Width==first.PixelWidth&&one.Result.Height==first.PixelHeight&&Pixels(ClipStore.ReadImage(one.Result.File)).SequenceEqual(Pixels(first)));
            Check("缩略图独立缩小且保持比例",one.Result.Thumbnail.IsFrozen&&Math.Max(one.Result.Thumbnail.PixelWidth,one.Result.Thumbnail.PixelHeight)<=560&&Math.Abs((double)one.Result.Thumbnail.PixelWidth/one.Result.Thumbnail.PixelHeight-(double)first.PixelWidth/first.PixelHeight)<.01);
            var restarted=new ClipStore(store.Root);Check("异步导入完成后重启恢复相同图片和哈希",restarted.Items.Count==2&&restarted.Items.Select(c=>c.Hash).SequenceEqual(store.Items.Select(c=>c.Hash)));
            Check("异步导入和去重保留源文件",original.SequenceEqual(File.ReadAllBytes(notes))&&File.Exists(landscape));
        }catch(Exception ex){results.Add(new{name="异常",pass=false,error=ex.ToString()});exit=1;}
        var json=new JavaScriptSerializer().Serialize(results);File.WriteAllText(Path.Combine(root,"async-store-tests.json"),json);Console.WriteLine(json);return exit;
    }
}
