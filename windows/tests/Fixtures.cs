using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

static class Fixtures {
    static void Text(Graphics g,string text,float size,Color color,float x,float y,bool bold=false){using(var f=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))using(var b=new SolidBrush(color))g.DrawString(text,f,b,x,y);}
    public static void Create(string path,int kind) {
        int w=kind==3?400:720,h=kind==3?720:400;
        using(var b=new Bitmap(w,h))using(var g=Graphics.FromImage(b)) {
            g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            if(kind==1) {
                using(var sky=new LinearGradientBrush(new Rectangle(0,0,w,h),Color.FromArgb(177,203,212),Color.FromArgb(241,220,181),90))g.FillRectangle(sky,0,0,w,h);
                using(var sun=new SolidBrush(Color.FromArgb(247,235,206)))g.FillEllipse(sun,490,58,90,90);
                using(var far=new SolidBrush(Color.FromArgb(118,153,157)))g.FillPolygon(far,new[]{new Point(0,250),new Point(170,120),new Point(310,225),new Point(470,155),new Point(720,250),new Point(720,400),new Point(0,400)});
                using(var near=new SolidBrush(Color.FromArgb(53,101,103)))g.FillClosedCurve(near,new[]{new Point(-90,385),new Point(100,232),new Point(275,310),new Point(470,274),new Point(800,400),new Point(720,520),new Point(0,520)});
            } else if(kind==2) {
                g.Clear(Color.FromArgb(248,250,251));Text(g,"Capture overview",25,Color.FromArgb(42,56,68),32,24,true);Text(g,"LOCAL WORKSPACE  /  THIS WEEK",11,Color.FromArgb(112,125,137),34,63);
                Text(g,"128",37,Color.FromArgb(44,101,106),34,99,true);Text(g,"saved moments",13,Color.FromArgb(112,125,137),37,146);
                using(var grid=new Pen(Color.FromArgb(226,233,236),1))for(int i=0;i<4;i++)g.DrawLine(grid,36,202+i*42,685,202+i*42);
                PointF[] points={new PointF(36,324),new PointF(120,298),new PointF(202,312),new PointF(288,251),new PointF(365,263),new PointF(450,221),new PointF(538,232),new PointF(618,189),new PointF(685,178)};
                using(var fill=new SolidBrush(Color.FromArgb(28,58,126,130))){var area=new GraphicsPath();area.AddCurve(points);area.AddLine(points[points.Length-1],new PointF(685,350));area.AddLine(685,350,36,350);area.CloseFigure();g.FillPath(fill,area);}
                using(var line=new Pen(Color.FromArgb(66,132,137),3)){line.StartCap=line.EndCap=LineCap.Round;g.DrawCurve(line,points);}
            } else if(kind==3) {
                g.Clear(Color.FromArgb(246,245,240));Text(g,"09:41",15,Color.FromArgb(40,47,56),23,18,true);Text(g,"Field journal",30,Color.FromArgb(42,53,57),26,74,true);
                Text(g,"Friday, October 9",14,Color.FromArgb(122,129,124),28,120);
                using(var green=new SolidBrush(Color.FromArgb(197,216,204)))g.FillRectangle(green,28,173,344,230);
                using(var hills=new SolidBrush(Color.FromArgb(107,145,126)))g.FillClosedCurve(hills,new[]{new Point(28,383),new Point(70,275),new Point(170,334),new Point(260,251),new Point(372,373),new Point(372,403),new Point(28,403)});
                Text(g,"Keep what matters.",23,Color.FromArgb(57,76,64),28,435,true);
                using(var lines=new SolidBrush(Color.FromArgb(207,211,202)))for(int i=0;i<5;i++)g.FillRectangle(lines,28,490+i*24,i==4?210:338-i*13,8);
                using(var pill=new SolidBrush(Color.FromArgb(60,102,83)))g.FillRectangle(pill,28,641,138,37);Text(g,"Add a thought",13,Color.White,45,650);
            } else {
                g.Clear(Color.FromArgb(252,251,248));using(var bar=new SolidBrush(Color.FromArgb(235,236,233)))g.FillRectangle(bar,0,0,w,35);
                foreach(var c in new[]{Color.FromArgb(212,135,125),Color.FromArgb(221,192,119),Color.FromArgb(139,175,146)}){int i=c.R==212?0:c.R==221?1:2;using(var dot=new SolidBrush(c))g.FillEllipse(dot,15+i*17,13,9,9);}
                Text(g,"Notes",11,Color.FromArgb(119,128,131),325,8);
                Text(g,"A little room to think.",30,Color.FromArgb(48,57,64),40,65,true);Text(g,"PROJECT JOURNAL   /   08 OCTOBER",11,Color.FromArgb(129,137,140),42,109);
                using(var lines=new SolidBrush(Color.FromArgb(214,219,219)))for(int i=0;i<5;i++)g.FillRectangle(lines,42,156+i*26,new[]{580,520,556,415,475}[i],8);
                using(var wash=new SolidBrush(Color.FromArgb(231,237,231)))g.FillRectangle(wash,42,309,632,52);Text(g,"Everything you need, just within reach.",16,Color.FromArgb(90,115,99),56,323);
            }
            b.Save(path,ImageFormat.Png);
        }
    }
    public static void Composite(string transparent,string output,bool dark) {
        using(var source=new Bitmap(transparent)) {
            int w=Math.Min(source.Width,1800),h=480,x=(source.Width-w)/2;
            using(var result=new Bitmap(w,h))using(var g=Graphics.FromImage(result)) {
                using(var background=new LinearGradientBrush(new Rectangle(0,0,w,h),dark?Color.FromArgb(31,41,53):Color.FromArgb(231,237,243),dark?Color.FromArgb(53,57,65):Color.FromArgb(243,236,230),25))g.FillRectangle(background,0,0,w,h);
                g.DrawImage(source,new Rectangle(0,0,w,source.Height),new Rectangle(x,0,w,source.Height),GraphicsUnit.Pixel);result.Save(output,ImageFormat.Png);
            }
        }
    }
}
