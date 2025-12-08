using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics; 

namespace EpidemicSimulation
{
    enum AgentState { Healthy, Infected, Zombie, Recovered }

    class Agent
    {
        public Vector2 Pos;
        public Vector2 Dir; 
        public double Speed; 
        public AgentState State;
        public int TimeToChangeDirection; 
        public double ViewAngleDeg; 
        public double ViewRadius;
        public double MoveSpeedBase; 
        public int IncubationLeft = 0; 
        public Random Rand;
        public bool IsMutant = false;

        public Agent(double x, double y, Vector2 dir, double baseSpeed, double viewAngleDeg, double viewRadius, Random rand)
        {
            Pos = new Vector2((float)x, (float)y);
            Dir = Vector2.Normalize(dir);
            MoveSpeedBase = baseSpeed;
            Speed = baseSpeed;
            State = AgentState.Healthy;
            ViewAngleDeg = viewAngleDeg;
            ViewRadius = viewRadius;
            TimeToChangeDirection = 0;
            Rand = rand;
        }

        public void RandomizeDirection()
        {
            double theta = Rand.NextDouble() * Math.PI * 2.0;
            Dir = new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta));
        }

        public void SetRandomTimeToMove(int minTmove, int maxTmove)
        {
            TimeToChangeDirection = Rand.Next(minTmove, maxTmove + 1);
        }

        public void StepMovementBounds(float areaSize)
        {
            var displacement = Dir * (float)Speed;
            var next = Pos + displacement;

            if (next.X < 0 || next.X > areaSize)
            {
                Dir.X = -Dir.X;
                next.X = Clamp(next.X, 0, areaSize);
            }
            if (next.Y < 0 || next.Y > areaSize)
            {
                Dir.Y = -Dir.Y;
                next.Y = Clamp(next.Y, 0, areaSize);
            }

            Pos = next;
        }

        static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public bool CanSee(Agent other)
        {
            if (other == this) return false;
            var v = other.Pos - this.Pos;
            var dist = v.Length();
            if (dist > ViewRadius) return false;
            if (dist == 0) return true;
            var vNorm = Vector2.Normalize(v);
            double angleBetween = Math.Acos(Math.Clamp(Vector2.Dot(Dir, vNorm), -1f, 1f)) * 180.0 / Math.PI;
            return angleBetween <= (ViewAngleDeg / 2.0);
        }

        public bool InSectorAction(Agent other, double actionAngleDeg, double actionRadius)
        {
            var v = other.Pos - this.Pos;
            var dist = v.Length();
            if (dist > actionRadius) return false;
            if (dist == 0) return true;
            var vNorm = Vector2.Normalize(v);
            double angleBetween = Math.Acos(Math.Clamp(Vector2.Dot(Dir, vNorm), -1f, 1f)) * 180.0 / Math.PI;
            return angleBetween <= (actionAngleDeg / 2.0);
        }
    }

    class Simulation
    {
        public readonly int AreaSize = 100;
        public readonly double NuH = 1.0; 
        public readonly double IncubationMin = 30;
        public readonly double IncubationMax = 100;
        public readonly int TmoveMin = 20;
        public readonly int TmoveMax = 100;
        public readonly int Tlimit = 10000; 
        public readonly int ExperimentsPerPair = 1000;

        private Random globalRand = new Random();

        private double AngleMin = 90.0;
        private double AngleMax = 150.0;

        private double Rh = 8.0;

        private double ZombieToRecoveredProb = 0.01; 
        private double RecoveredToZombieOnContactProb = 0.25; 

        private double InfectedSpeedFactor = 0.90; 
        private double ZombieSpeedFactor = 0.85; 
        private double HealthyFleeFactor = 1.25;

        private double MutationProb = 0.02; 
        private double MutantSpeedFactor = 1.10; 
        private double MutantViewRadiusFactor = 1.20; 

        public Simulation() { }

        public double RunSingle(int n, int m, int seed)
        {
            var rand = new Random(seed);
            var agents = new List<Agent>(n);

            for (int i = 0; i < n; i++)
            {
                double x = rand.NextDouble() * AreaSize;
                double y = rand.NextDouble() * AreaSize;
                double theta = rand.NextDouble() * Math.PI * 2.0;
                var dir = new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta));
                double alpha = AngleMin + rand.NextDouble() * (AngleMax - AngleMin);
                var agent = new Agent(x, y, dir, NuH, alpha, Rh, rand);
                agent.SetRandomTimeToMove(TmoveMin, TmoveMax);
                agents.Add(agent);
            }

            int t = 0;

            int tinit = 50;

            while (t < Tlimit)
            {
                if (t == tinit)
                {
                    var indices = Enumerable.Range(0, n).OrderBy(_ => rand.Next()).Take(Math.Min(m, n)).ToArray();
                    foreach (var idx in indices)
                    {
                        var a = agents[idx];
                        if (a.State == AgentState.Healthy)
                        {
                            a.State = AgentState.Infected;
                            a.IncubationLeft = rand.Next((int)IncubationMin, (int)IncubationMax + 1);
                            a.Speed = a.MoveSpeedBase * InfectedSpeedFactor;
                        }
                    }
                }

                int healthyCount = agents.Count(a => a.State == AgentState.Healthy);
                if (healthyCount == 0)
                {
                    return t; 
                }

                var seenBy = new List<List<int>>(n);
                for (int i = 0; i < n; i++) seenBy.Add(new List<int>());

                for (int i = 0; i < n; i++)
                {
                    var ai = agents[i];
                    double viewAngle = ai.ViewAngleDeg;
                    double viewRadius = ai.ViewRadius;
                    if (ai.State == AgentState.Zombie)
                    {
                        viewAngle = ai.ViewAngleDeg * 0.75; 
                        viewRadius = ai.ViewRadius * 1.1; 
                    }

                    for (int j = 0; j < n; j++)
                    {
                        if (i == j) continue;
                        var aj = agents[j];
                        var v = aj.Pos - ai.Pos;
                        var dist = v.Length();
                        if (dist <= viewRadius)
                        {
                            if (dist == 0) { seenBy[i].Add(j); continue; }
                            var vNorm = Vector2.Normalize(v);
                            double angleBetween = Math.Acos(Math.Clamp(Vector2.Dot(ai.Dir, vNorm), -1f, 1f)) * 180.0 / Math.PI;
                            if (angleBetween <= (viewAngle / 2.0))
                                seenBy[i].Add(j);
                        }
                    }
                }

                for (int i = 0; i < n; i++)
                {
                    var a = agents[i];
                    if (a.State == AgentState.Zombie)
                    {
                        if (rand.NextDouble() < ZombieToRecoveredProb)
                        {
                            a.State = AgentState.Recovered;
                            a.Speed = a.MoveSpeedBase; 
                        }
                    }
                }

                for (int i = 0; i < n; i++)
                {
                    var a = agents[i];
                    if (a.State != AgentState.Zombie) continue;

                    double zombieViewAngle = a.ViewAngleDeg * 0.75; 
                    double zombieViewRadius = a.ViewRadius * 1.1;

                    double actionAngle = zombieViewAngle * 0.93; 
                    double actionRadius = zombieViewRadius * 0.93;

                    int targetIdx = -1;
                    double nearestDist = double.MaxValue;
                    for (int j = 0; j < n; j++)
                    {
                        var b = agents[j];
                        if (b == a) continue;
                        if (b.State != AgentState.Healthy && b.State != AgentState.Recovered) continue;
                        var v = b.Pos - a.Pos;
                        var dist = v.Length();
                        if (dist > actionRadius) continue;
                        if (dist == 0) { targetIdx = j; nearestDist = 0; break; }
                        var vNorm = Vector2.Normalize(v);
                        double angleBetween = Math.Acos(Math.Clamp(Vector2.Dot(a.Dir, vNorm), -1f, 1f)) * 180.0 / Math.PI;
                        if (angleBetween <= (actionAngle / 2.0))
                        {
                            if (dist < nearestDist)
                            {
                                nearestDist = dist;
                                targetIdx = j;
                            }
                        }
                    }

                    if (targetIdx >= 0)
                    {
                        var target = agents[targetIdx];
                        if (target.State == AgentState.Healthy)
                        {
                            target.State = AgentState.Infected;
                            target.IncubationLeft = rand.Next((int)IncubationMin, (int)IncubationMax + 1);
                            target.Speed = target.MoveSpeedBase * InfectedSpeedFactor;
                        }
                        else if (target.State == AgentState.Recovered)
                        {
                            if (rand.NextDouble() < RecoveredToZombieOnContactProb)
                            {
                                target.State = AgentState.Zombie;
                                target.Speed = target.MoveSpeedBase * ZombieSpeedFactor;
                                var toward = Vector2.Normalize(a.Pos - target.Pos);
                                target.Dir = toward;
                            }
                        }
                    }
                }

                for (int i = 0; i < n; i++)
                {
                    var a = agents[i];
                    if (a.State == AgentState.Infected)
                    {
                        a.IncubationLeft--;
                        if (a.IncubationLeft <= 0)
                        {
                            a.State = AgentState.Zombie;

                            if (rand.NextDouble() < MutationProb)
                            {
                                a.IsMutant = true;
                                a.Speed = a.MoveSpeedBase * ZombieSpeedFactor * MutantSpeedFactor;
                            }
                            else
                            {
                                a.Speed = a.MoveSpeedBase * ZombieSpeedFactor;
                            }
                        }
                    }
                }

                for (int i = 0; i < n; i++)
                {
                    var a = agents[i];

                    switch (a.State)
                    {
                        case AgentState.Healthy:
                            a.Speed = a.MoveSpeedBase;
                            break;
                        case AgentState.Infected:
                            a.Speed = a.MoveSpeedBase * InfectedSpeedFactor;
                            break;
                        case AgentState.Zombie:
                            a.Speed = a.MoveSpeedBase * ZombieSpeedFactor;
                            break;
                        case AgentState.Recovered:
                            a.Speed = a.MoveSpeedBase;
                            break;
                    }

                    if (a.State == AgentState.Healthy)
                    {
                        double viewAngle = a.ViewAngleDeg;
                        double viewRadius = a.ViewRadius;

                        var zombiesSeen = new List<(int idx, double angleSigned)>();
                        foreach (var j in seenBy[i])
                        {
                            var b = agents[j];
                            if (b.State != AgentState.Zombie) continue;
                            var v = b.Pos - a.Pos;
                            var dist = v.Length();
                            if (dist > viewRadius) continue;
                            if (dist == 0)
                            {
                                zombiesSeen.Add((j, 0.0));
                                continue;
                            }
                            var angleTo = Math.Atan2(v.Y, v.X);
                            var angleDir = Math.Atan2(a.Dir.Y, a.Dir.X);
                            double angleDiff = NormalizeAngleRad(angleTo - angleDir); 
                            zombiesSeen.Add((j, angleDiff));
                        }

                        if (zombiesSeen.Count > 0)
                        {
                            bool anyLeft = zombiesSeen.Any(z => z.angleSigned < 0);
                            bool anyRight = zombiesSeen.Any(z => z.angleSigned > 0);

                            if (anyLeft && anyRight)
                            {
                                a.Dir = -a.Dir;
                            }
                            else if (anyRight)
                            {
                                a.Dir = Rotate(a.Dir, Math.PI / 2.0);
                            }
                            else if (anyLeft)
                            {
                                a.Dir = Rotate(a.Dir, -Math.PI / 2.0);
                            }

                            double panicBoost = 1.0 + 0.15 * zombiesSeen.Count;
                            a.Speed = a.MoveSpeedBase * HealthyFleeFactor * panicBoost;
                        }
                        else
                        {
                            if (a.TimeToChangeDirection <= 0)
                            {
                                a.RandomizeDirection();
                                a.SetRandomTimeToMove(TmoveMin, TmoveMax);
                            }
                        }
                    }
                    else if (a.State == AgentState.Zombie)
                    {
                        double zombieViewAngle = a.IsMutant ? a.ViewAngleDeg : a.ViewAngleDeg * 0.75;
                        double zombieViewRadius = a.IsMutant ? a.ViewRadius * MutantViewRadiusFactor : a.ViewRadius * 1.1;
                        int targetIdx = -1;
                        double nearestDist = double.MaxValue;
                        foreach (var j in seenBy[i])
                        {
                            var b = agents[j];
                            if (b.State != AgentState.Healthy) continue;
                            var v = b.Pos - a.Pos;
                            var dist = v.Length();
                            if (dist <= zombieViewRadius)
                            {
                                var vNorm = Vector2.Normalize(v);
                                double angleBetween = Math.Acos(Math.Clamp(Vector2.Dot(a.Dir, vNorm), -1f, 1f)) * 180.0 / Math.PI;
                                if (angleBetween <= (zombieViewAngle / 2.0))
                                {
                                    if (dist < nearestDist)
                                    {
                                        nearestDist = dist;
                                        targetIdx = j;
                                    }
                                }
                            }
                        }

                        if (targetIdx >= 0)
                        {
                            var target = agents[targetIdx];
                            var toward = target.Pos - a.Pos;
                            if (toward.Length() > 0)
                                a.Dir = Vector2.Normalize(toward);
                        }
                        else
                        {
                            if (a.TimeToChangeDirection <= 0)
                            {
                                a.RandomizeDirection();
                                a.SetRandomTimeToMove(TmoveMin, TmoveMax);
                            }
                        }
                    }
                    else
                    {
                        if (a.TimeToChangeDirection <= 0)
                        {
                            a.RandomizeDirection();
                            a.SetRandomTimeToMove(TmoveMin, TmoveMax);
                        }
                    }

                    a.TimeToChangeDirection = Math.Max(0, a.TimeToChangeDirection - 1);
                } 

                foreach (var a in agents)
                {
                    a.StepMovementBounds(AreaSize);
                }

                t++;
            } 
           
            return Tlimit;
        }

        private static double NormalizeAngleRad(double a)
        {
            while (a <= -Math.PI) a += 2 * Math.PI;
            while (a > Math.PI) a -= 2 * Math.PI;
            return a;
        }

        private static Vector2 Rotate(Vector2 v, double angle)
        {
            double c = Math.Cos(angle), s = Math.Sin(angle);
            return new Vector2((float)(v.X * c - v.Y * s), (float)(v.X * s + v.Y * c));
        }

        public void RunBatchAndSave()
        {
            int[] nValues = new int[] { 10, 20, 50, 100 };

            var results = new List<(int n, int m, double averageTime)>();
            var csvLines = new List<string>();
            csvLines.Add("n,m,avg_time,experiments,Tlimit");

            int totalPairs = 0;
            foreach (var n in nValues)
            {
                if (n == 10) totalPairs += 3;
                else if (n == 20) totalPairs += 3;
                else if (n == 50) totalPairs += 3;
                else totalPairs += 3;
            }
            int pairIndex = 0;

            foreach (var n in nValues)
            {
                int[] mValues;
                if (n == 10) mValues = new int[] { 6, 7, 8, 9 };
                else if (n == 20) mValues = new int[] { 5, 8, 10, 15 };
                else if (n == 50) mValues = new int[] { 10, 15, 20, 35 };
                else mValues = new int[] { 15, 25, 30, 50 };

                foreach (var m in mValues)
                {
                    pairIndex++;
                    WriteColor(ConsoleColor.Cyan, $"[{pairIndex}/{totalPairs}] Running experiments for n={n}, m={m} ({ExperimentsPerPair} runs) ...");
                    double sum = 0;
                    int seedBase = globalRand.Next();

                    for (int e = 0; e < ExperimentsPerPair; e++)
                    {
                        int seed = seedBase + e * 97 + n * 13 + m * 7;
                        double t = RunSingle(n, m, seed);
                        sum += t;

                        if ((e + 1) % 100 == 0)
                            WriteColor(ConsoleColor.DarkGray, $"  progress: {e + 1}/{ExperimentsPerPair} (latest t={t})");
                    }

                    double avg = sum / ExperimentsPerPair;
                    results.Add((n, m, avg));
                    WriteColor(ConsoleColor.Green, $"-> Done n={n}, m={m} average time = {avg:F2} iterations.");
                    csvLines.Add($"{n},{m},{avg:F2},{ExperimentsPerPair},{Tlimit}");
                }
            }

            Console.WriteLine();
            WriteColor(ConsoleColor.Yellow, "Summary table (average time until no healthy agents remain):");
            WriteColor(ConsoleColor.White, " n\t m\t avg_time");
            foreach (var r in results)
            {
                WriteColor(ConsoleColor.Magenta, $" {r.n}\t {r.m}\t {r.averageTime:F2}");
            }

            var outPath = "results.csv";
            File.WriteAllLines(outPath, csvLines);
            Console.WriteLine();
            WriteColor(ConsoleColor.DarkYellow, $"Results saved to {outPath}");
        }

        static void WriteColor(ConsoleColor color, string text)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = old;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
           var sim = new Simulation();
            sim.RunBatchAndSave();
            Console.ReadLine();
            Console.ResetColor();
        }
    }
}
