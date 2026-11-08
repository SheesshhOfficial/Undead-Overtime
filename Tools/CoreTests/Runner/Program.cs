using System;
using System.Runtime.InteropServices;
using NUnitLite;

internal static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("Undead Overtime cloud core verification only. Not Unity/physics/rendering/LAN acceptance.");
        Console.WriteLine(RuntimeInformation.FrameworkDescription + " | " + RuntimeInformation.OSDescription);
        return new AutoRun(typeof(Program).Assembly).Execute(args);
    }
}
