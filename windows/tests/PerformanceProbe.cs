using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// No screen capture, clipboard, visible windows or synthetic mouse input.
class PerformanceProbe {
    static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
    static double lastBeat,maxGap;static Stopwatch clock;
    static void Beat(){double now=clock.Elapsed.TotalMilliseconds;maxGap=Math.Max(maxGap,now-lastBeat);lastBeat=now;}
    static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    static BitmapSource Source(int width,int height,int seed){
        var bytes=new byte[width*height*4];uint noise=(uint)(seed+1);
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){
            int i=(y*width+x)*4;noise=noise*1664525+1013904223;int grain=(int)(noise>>29);
            bytes[i]=(byte)((x/19+y/31+grain+seed*11)%256);bytes[i+1]=(byte)((y/13+x/71+grain+seed*17)%256);bytes[i+2]=(byte)((x/43+y/23+grain+seed*23)%256);bytes[i+3]=255;
        }
        var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,bytes,width*4);bitmap.Freeze();return bitmap;
    }
    [STAThread] static int Main(string[] args){
        try{
            var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
            var storeType=assembly.GetType("Screenline.ClipStore");var store=Activator.CreateInstance(storeType,new object[]{Path.Combine(output,"data-"+Guid.NewGuid().ToString("N"))});
            System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var sync=storeType.GetMethod("Add");var asyncAdd=storeType.GetMethod("AddAsync");
            sync.Invoke(store,new object[]{Source(64,40,0)});
            clock=Stopwatch.StartNew();lastBeat=0;
            var heartbeat=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(16)};heartbeat.Tick+=delegate{Beat();};heartbeat.Start();Pump(100);
            var rows=new List<object>();
            for(int sample=0;sample<3;sample++){
                var bitmap=Source(3840,2160,sample+1);Pump(80);maxGap=0;lastBeat=clock.Elapsed.TotalMilliseconds;
                var watch=Stopwatch.StartNew();
                if(asyncAdd==null)sync.Invoke(store,new object[]{bitmap});
                else {var task=(Task)asyncAdd.Invoke(store,new object[]{bitmap});while(!task.IsCompleted)Pump(5);task.GetAwaiter().GetResult();}
                watch.Stop();Pump(60);double gap=maxGap;
                var duplicate=Stopwatch.StartNew();
                if(asyncAdd==null)sync.Invoke(store,new object[]{bitmap});
                else {var task=(Task)asyncAdd.Invoke(store,new object[]{bitmap});while(!task.IsCompleted)Pump(5);task.GetAwaiter().GetResult();}
                duplicate.Stop();Pump(60);
                rows.Add(new{sample=sample+1,add_ms=watch.Elapsed.TotalMilliseconds,ui_max_gap_ms=gap,duplicate_ms=duplicate.Elapsed.TotalMilliseconds});
            }
            heartbeat.Stop();
            File.WriteAllText(Path.Combine(output,"image-pipeline.json"),Json.Serialize(new{version=assembly.GetName().Version.ToString(),source="Synthetic 3840x2160 BGRA gradients with fine grain",mode=asyncAdd==null?"synchronous Add on dispatcher":"production AddAsync on dispatcher",samples=rows}));
            Console.WriteLine(File.ReadAllText(Path.Combine(output,"image-pipeline.json")));return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
