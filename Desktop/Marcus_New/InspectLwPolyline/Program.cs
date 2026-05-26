using System;
using ACadSharp.Entities;

public class Program
{
    public static void Main()
    {
        var type = typeof(LwPolyline);
        Console.WriteLine(type.FullName);
        Console.WriteLine("Vertices property: " + type.GetProperty("Vertices")?.PropertyType.FullName);
        Console.WriteLine("IsClosed property: " + type.GetProperty("IsClosed")?.PropertyType.FullName);

        foreach (var prop in type.GetProperties())
        {
            if (prop.Name.Contains("Vert") || prop.Name.Contains("Bulge"))
            {
                Console.WriteLine(prop.Name + ": " + prop.PropertyType.FullName);
            }
        }

        foreach (var nested in type.GetNestedTypes())
        {
            if (nested.Name.Contains("Vertex"))
            {
                Console.WriteLine("Nested type: " + nested.FullName);
                foreach (var prop in nested.GetProperties())
                {
                    if (prop.Name.Contains("Bulge") || prop.Name.Contains("Location"))
                    {
                        Console.WriteLine("  " + prop.Name + ": " + prop.PropertyType.FullName);
                    }
                }
            }
        }
    }
}
