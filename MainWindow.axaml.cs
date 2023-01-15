using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Skia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Triangulate;
using SkiaSharp;
using System;
using System.Linq;
using System.Buffers;

using GeometryCollection = NetTopologySuite.Geometries.GeometryCollection;

namespace Procedural;

public partial class MainWindow : Window
{
    public const string EncodedNoise = "IgAzMwtBAAAAACEAEwBvEoM7GQANAAUAAABSuN4/CQAAexQuPwAAAAAAASEAJAADAAAAGwAFAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgL///wAAAJqZmT4bAP//BwAAuB4FPgDNzMw+";

    public MainWindow()
    {
        InitializeComponent();
    }

    private int _width = 2048;
    private int _height = 2048;

    private float Frequency => (float)FreqUI.Value;
    private int PointCount => (int)CntUI.Value;
    private int IterationCount => (int)IterUI.Value;
    private float NoiseMin => (float)MinUI.Value;
    private float NoiseMax => (float)MaxUI.Value;
    private float SeaLevel => (float)SeaUI.Value;

    private Random _rng = new Random(unchecked((int) DateTime.Now.Ticks));

    public float Lerp(float v, float a, float b)
    {
        v = Math.Clamp(v, a, b);
        return (v - a) / (b - a);
    }

    public double Rand(double min, double max)
    {
        return min + _rng.NextDouble() * (max - min);
    }

    public void OnRegenerating(object sender, RoutedEventArgs e)
    {
        RegenUI.Content = "Generating...";
        var starts = DateTime.Now;

        var ui = CanvasUI;

        var target = new WriteableBitmap(
            new PixelSize(_width, _height), 
            new Vector(96, 96),
            PixelFormat.Rgba8888);
        using (var locked = target.Lock())
        {
            var info = new SKImageInfo(
                locked.Size.Width,
                locked.Size.Height,
                locked.Format.ToSkColorType(),
                SKAlphaType.Premul);
            using (var surface = SKSurface.Create(info, locked.Address, locked.RowBytes))
            {
                var canvas = surface.Canvas;
                canvas.Clear(new SKColor(0, 0, 0));
                Generate(canvas);
            }
        }

        ui.Source = target;

        var elapsed = DateTime.Now - starts;
        RegenUI.Content = $"Generated. {elapsed.TotalMilliseconds}ms taken.";
    }

    public void Generate(SKCanvas canvas)
    {
        var magenta = new SKPaint();
        magenta.Color = new SKColor(255, 0, 0);
        var green = new SKPaint();
        green.Color = new SKColor(0, 255, 0);

        var seed = (int) DateTime.Now.Ticks;
        var noise = FastNoise.FromEncodedNodeTree(EncodedNoise);
        
        var vertices = new Coordinate[PointCount];
        for (var i = 0; i < PointCount; ++i)
            vertices[i] = new Coordinate(Rand(0, _width), Rand(0, _height));
        vertices = Relaxtion(vertices, IterationCount);

        foreach (var cell in Voronoi(vertices))
        {
            var centroid = cell.Centroid.Coordinate;

            var pts = cell.Coordinates;

            canvas.Save();
            var path = new SKPath();
            path.AddPoly(pts.Select(p => new SKPoint((float)p.X, (float)p.Y)).ToArray());
            canvas.ClipPath(path);

            var height = noise.GenSingle2D(((float) centroid.X - 1024) * Frequency, ((float) centroid.Y - 1024) * Frequency, seed);

            var paint = new SKPaint();
            
            var val = Lerp(height, NoiseMin, NoiseMax);
            if (val <= SeaLevel)
            {
                val += 0.3f;
                paint.Color = new SKColor((byte)(150 * val), (byte)(150 * val), (byte)(255 * val));
            }
            else
            {
                paint.Color = new SKColor((byte)(255 * val), (byte)(255 * val), (byte)(255 * val));
            }
            canvas.DrawPaint(paint);
            canvas.Restore();

            canvas.DrawCircle((float) centroid.X, (float) centroid.Y, 2f, magenta);
            for (var i = 0; i < pts.Length; ++i)
                canvas.DrawLine(
                    (float) pts[i].X, 
                    (float) pts[i].Y, 
                    (float) pts[(i + 1) % pts.Length].X, 
                    (float) pts[(i + 1) % pts.Length].Y, green);
        }
    }

    public GeometryCollection Voronoi(Coordinate[] pts)
    {
        var builder = new VoronoiDiagramBuilder();
        builder.ClipEnvelope = new Envelope(new Coordinate(0, 0), new Coordinate(_width, _height));
        builder.Tolerance = 0.01;
        builder.SetSites(pts);
        return builder.GetDiagram(GeometryFactory.Default);
    }

    public Coordinate[] Relaxtion(Coordinate[] pts, int iteration)
    {
        for (var i = 0; i < iteration; ++i)
            pts = Voronoi(pts).Select(c => c.Centroid.Coordinate).ToArray();
        return pts;
    }

    public void OnQuit(object sender, RoutedEventArgs e) => Close();
}