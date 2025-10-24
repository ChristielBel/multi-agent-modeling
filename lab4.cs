using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

class Program
{
    static double dt = 0.001;             
    static double tstop = 50.0;           
    static double divergeThreshold = 1e6; 
    static int steadyWindowSamples = 200; 
    static double steadyEps = 1e-4;       

    static void Main()
    {
        Directory.CreateDirectory("output");

        var paramSets = new List<PlantParams>()
        {
             new PlantParams { a2=3.0, a1=2.0, a0=5.0, k=3.0 },
             new PlantParams { a2=2.5, a1=2.0, a0=5.0, k=3.0 },
             new PlantParams { a2=2.0, a1=2.0, a0=5.0, k=3.0 },
             new PlantParams { a2=1.5, a1=2.0, a0=5.0, k=3.0 },
             new PlantParams { a2=1.0, a1=2.0, a0=5.0, k=3.0 },
             new PlantParams { a2=0.5, a1=2.0, a0=5.0, k=3.0 },
        };

        double tauMin = 0.0;
        double tauMax = 100.0;
        double tauStep = 0.001;

        using var writer = new StreamWriter("output/critical_tau.csv");
        writer.WriteLine("a2;a1;a0;k;tau_crit");

        Console.WriteLine("Поиск критических значений tau...");

        foreach (var p in paramSets)
        {
            double tauCrit = FindCriticalTau(p, tauMin, tauMax, tauStep);
            writer.WriteLine($"{p.a2};{p.a1};{p.a0};{p.k};{tauCrit.ToString(CultureInfo.InvariantCulture)}");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"a2={p.a2,4:F2}, a1={p.a1,4:F2}, a0={p.a0,4:F2}, k={p.k,4:F1} => tau_crit = {tauCrit,6:F3}");
            Console.ResetColor();
        }
    }

    class PlantParams
    {
        public double a2, a1, a0, k;
    }

    class SimResult
    {
        public string status;
    }

    static double FindCriticalTau(PlantParams p, double tauMin, double tauMax, double tauStep)
    {
        double tauCrit = tauMax;
        for (double tau = tauMin; tau <= tauMax + 1e-12; tau += tauStep)
        {
            var result = Simulate(p.a2, p.a1, p.a0, p.k, tau);
            if (result.status != "stable")
            {
                tauCrit = tau;
                break;
            }
        }
        return tauCrit;
    }

    static SimResult Simulate(double a2, double a1, double a0, double k, double tau)
    {
        int N = (int)Math.Ceiling(tstop / dt) + 1;
        double[] x = new double[N];
        int delaySamples = (int)Math.Round(tau / dt);

        double c0 = a2 / (dt * dt) + a1 / dt + a0;
        double c1 = -2.0 * a2 / (dt * dt) - a1 / dt;
        double c2 = a2 / (dt * dt);

        double maxAbs = 0.0;
        string status = "stable";

        for (int i = 2; i < N; i++)
        {
            int idxDelayed = i - delaySamples;
            double xDelayed = (idxDelayed >= 0) ? x[idxDelayed] : 0.0;

            double u = k * (1.0 - xDelayed); 
            double xi = (u - c1 * x[i - 1] - c2 * x[i - 2]) / c0;
            x[i] = xi;

            double absxi = Math.Abs(xi);
            if (absxi > maxAbs) maxAbs = absxi;

            if (absxi > divergeThreshold)
            {
                status = "diverged";
                break;
            }
        }

        if (status != "diverged")
        {
            int lastN = Math.Min(steadyWindowSamples, N);
            double maxLast = double.MinValue, minLast = double.MaxValue;
            for (int i = N - lastN; i < N; i++)
            {
                double v = x[i];
                if (v > maxLast) maxLast = v;
                if (v < minLast) minLast = v;
            }
            double span = maxLast - minLast;
            status = (span < steadyEps) ? "stable" : "unstable";
        }

        return new SimResult { status = status };
    }
}
