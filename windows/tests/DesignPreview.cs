// Render the production WPF controls with synthetic content. No desktop capture or mouse input.
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Screenline;
class DesignPreview {
 static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Invoke(target,args);}
 static void Render(ImageWindow window,string path){var view=(FrameworkElement)window.Content;view.Measure(new Size(1000,740));view.Arrange(new Rect(0,0,1000,740));view.UpdateLayout();var output=new RenderTargetBitmap(1000,740,96,96,PixelFormats.Pbgra32);var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(window.Background,null,new Rect(0,0,1000,740));output.Render(background);output.Render(view);File.WriteAllBytes(path,ClipStore.Png(output));}
 [STAThread] static int Main(string[] args){Directory.CreateDirectory(args[0]);var app=new Application();var store=new ClipStore(Path.Combine(args[0],"data"));string source=Path.Combine(args[0],"notes.png");Fixtures.Create(source,0);var clip=store.Import(source);var editor=new ImageWindow(clip,store,false,null);Render(editor,Path.Combine(args[0],"editor-preview.png"));Call(editor,"SetTool","rectangle");editor.GetType().GetField("shapeStart",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(editor,new Point(36,145));Call(editor,"CommitShape",new Point(650,283));Call(editor,"SetTool","arrow");editor.GetType().GetField("shapeStart",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(editor,new Point(560,112));Call(editor,"CommitShape",new Point(520,154));Call(editor,"SetTool","text");((TextBox)editor.GetType().GetField("textEntry",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor)).Text="关键区域";Call(editor,"PlaceText",new Point(475,80));Render(editor,Path.Combine(args[0],"editor-annotated.png"));Console.WriteLine("Production editor controls rendered without showing a window or taking the mouse.");return 0;}
}
