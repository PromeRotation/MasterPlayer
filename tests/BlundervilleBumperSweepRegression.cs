using System.Numerics;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

internal static class BlundervilleBumperSweepRegression
{
    public static void Run(Action<string,Action> run)
    {
        run("Blunderville bumper sweep reaches the navigation snapshot without square corners", () =>
        {
            var template = typeof(BlundervilleNavigation).GetMethod("Template",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var shape = new BlundervilleBumperSweep(new(-208,17,-188),new(-204,17,-188),1.4f);
            var converted = (GeometryTemplate?)template.Invoke(null, [shape]);
            Check(converted != null, "Navigation silently omitted the moving cylinder body.");
            var resolved = converted!.Resolve(shape.Position, 0, null);
            Check(ReferenceEquals(resolved, shape) && resolved.Contains(new(-206,17,-188))
                && !resolved.Contains(new(-202.7f,17,-186.7f)), "Navigation changed the immutable analytic body.");
        });
        run("Blunderville bumper sweep contains every circular body along its animation interval",()=>
        {
            var sweep=new BlundervilleBumperSweep(new(-208,17,-188.02f),new(-204,17,-187.98f),1.4f);
            var polygon=new WorldPolygonGeometry(sweep.GetContours()[0]);
            for(var step=0;step<=20;++step)
            foreach(var z in new[]{-188.02f,-188f,-187.98f})
            for(var angle=0;angle<72;++angle)
            {
                var center=new Vector3(-208+step*.2f,17,z);
                var p=center+new Vector3(MathF.Cos(angle*MathF.Tau/72),0,MathF.Sin(angle*MathF.Tau/72))*1.4f;
                Check(sweep.Contains(p,.0001f)&&polygon.Contains(p,.0001f),"An intermediate circular contact escaped the analytic sweep or its exported contour.");
            }
            Check(!sweep.Contains(new(-202.7f,17,-186.7f)),"The nonphysical rectangle corner still blocks the diagonal passage.");
        });
        run("Blunderville bumper sweep intersects rays through its body and rounded ends",()=>
        {
            var sweep=new BlundervilleBumperSweep(new(-208,17,-188),new(-204,17,-188),1.4f);
            Check(sweep.IntersectsSegment(new(-206,17,-191),new(-206,17,-185)),"A ray through the body was missed.");
            Check(sweep.IntersectsSegment(new(-202.65f,17,-190),new(-202.65f,17,-186)),"A rounded endpoint collision was missed.");
            Check(!sweep.IntersectsSegment(new(-202.5f,17,-190),new(-202.5f,17,-186),.05f),"A ray outside the inflated end was blocked.");
            Check(sweep.IntersectsSegment(new(-202.5f,17,-190),new(-202.5f,17,-186),.11f),"Collision clearance was omitted at a rounded end.");
        });
    }
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
