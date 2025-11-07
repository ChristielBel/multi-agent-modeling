using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Lab5DynamicBalancing
{
    public enum BalanceMode { Simple, Smart, LFT, Swarm }

    public class Module
    {
        public string Id { get; }
        public double Load { get; }
        public List<string> Pred { get; set; } = new();
        public List<string> Succ { get; set; } = new();
        public bool Finished { get; set; }
        public bool Running { get; set; }

        public Module(string id, double load)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Load = load;
        }
    }

    public class Agent
    {
        public int Id { get; }
        public HashSet<int> Neighbors { get; set; } = new();
        public HashSet<string> AssignedModules { get; set; } = new();
        public double AvailableAt { get; set; }

        public Agent(int id) => Id = id;

        public double RemainingLoad(Dictionary<string, Module> modules)
            => AssignedModules.Where(m => !modules[m].Finished && !modules[m].Running)
                              .Sum(m => modules[m].Load);
    }

    class TimelineEntry
    {
        public double Time { get; set; }
        public int Id { get; set; }
        public Action Action { get; set; }
    }

    public static class Balancer
    {
        public static void Redistribute(BalanceMode mode,
            Dictionary<int, Agent> agents,
            Dictionary<string, Module> modules,
            double now)
        {
            switch (mode)
            {
                case BalanceMode.Smart: SmartBalance(agents, modules, now); break;
                case BalanceMode.LFT: LeastFinishTime(agents, modules, now); break;
                case BalanceMode.Swarm: SwarmBalance(agents, modules, now); break;
                default: SimpleBalance(agents, modules, now); break;
            }
        }

        private static void SimpleBalance(Dictionary<int, Agent> agents, Dictionary<string, Module> modules, double now)
        {
            var loads = agents.ToDictionary(a => a.Key, a => a.Value.RemainingLoad(modules));
            int max = loads.OrderByDescending(x => x.Value).First().Key;
            int min = loads.OrderBy(x => x.Value).First().Key;
            if (max == min || loads[max] - loads[min] < 1e-6) return;

            var candidate = agents[max].AssignedModules.FirstOrDefault(m => !modules[m].Finished && !modules[m].Running);
            if (candidate == null) return;
            LogBalance($"SIMPLE: move {candidate} from {max} → {min}", now);
            agents[max].AssignedModules.Remove(candidate);
            agents[min].AssignedModules.Add(candidate);
        }

        private static void SmartBalance(Dictionary<int, Agent> agents, Dictionary<string, Module> modules, double now)
        {
            var avg = agents.Values.Average(a => a.RemainingLoad(modules));
            var heavy = agents.Values.Where(a => a.RemainingLoad(modules) > avg * 1.2).ToList();
            var light = agents.Values.Where(a => a.RemainingLoad(modules) < avg * 0.8).ToList();

            foreach (var h in heavy)
            {
                var candidate = h.AssignedModules.FirstOrDefault(m => !modules[m].Finished && !modules[m].Running);
                if (candidate == null) continue;
                var target = light.FirstOrDefault(l => h.Neighbors.Contains(l.Id));
                if (target != null)
                {
                    LogBalance($"SMART: {candidate} from {h.Id} → {target.Id}", now);
                    h.AssignedModules.Remove(candidate);
                    target.AssignedModules.Add(candidate);
                    break;
                }
            }
        }

        private static void LeastFinishTime(Dictionary<int, Agent> agents, Dictionary<string, Module> modules, double now)
        {
            var pending = modules.Values.Where(m => !m.Finished && !m.Running).ToList();
            foreach (var mod in pending)
            {
                int bestAgent = agents.Values.OrderBy(a => a.AvailableAt + a.RemainingLoad(modules)).First().Id;
                if (!agents[bestAgent].AssignedModules.Contains(mod.Id))
                {
                    int currentAgent = agents.FirstOrDefault(a => a.Value.AssignedModules.Contains(mod.Id)).Key;
                    LogBalance($"LFT: move {mod.Id} {currentAgent} → {bestAgent}", now);
                    if (agents.ContainsKey(currentAgent))
                        agents[currentAgent].AssignedModules.Remove(mod.Id);
                    agents[bestAgent].AssignedModules.Add(mod.Id);
                }
            }
        }

        private static void SwarmBalance(Dictionary<int, Agent> agents, Dictionary<string, Module> modules, double now)
        {
            foreach (var a in agents.Values)
            {
                double load = a.RemainingLoad(modules);
                foreach (var n in a.Neighbors)
                {
                    var neighbor = agents[n];
                    double diff = load - neighbor.RemainingLoad(modules);
                    if (diff > 1.0)
                    {
                        var candidate = a.AssignedModules.FirstOrDefault(m => !modules[m].Finished && !modules[m].Running);
                        if (candidate != null)
                        {
                            LogBalance($"SWARM: {candidate} {a.Id} → {neighbor.Id}", now);
                            a.AssignedModules.Remove(candidate);
                            neighbor.AssignedModules.Add(candidate);
                            break;
                        }
                    }
                }
            }
        }

        private static void LogBalance(string msg, double t)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[t={t,6:F3}] {msg}");
            Console.ResetColor();
        }
    }

    public class FullSimulator
    {
        private readonly Dictionary<string, Module> modules;
        private readonly Dictionary<int, Agent> agents;
        private readonly Random rng;
        private readonly double failureProb = 0.05;
        private readonly BalanceMode Mode;
        private readonly SortedSet<TimelineEntry> timeline;
        private readonly Dictionary<string, Scheduled> running = new();
        private double now;
        private double lastCompletion;

        private class Scheduled
        {
            public string ModuleId = "";
            public int AgentId;
            public double Start;
            public double End;
            public bool IsFailureEvent;
        }

        public FullSimulator(Dictionary<string, Module> modules, Dictionary<int, Agent> agents, BalanceMode mode, int seed = 123)
        {
            Mode = mode;
            this.modules = modules.ToDictionary(kvp => kvp.Key, kvp => new Module(kvp.Key, kvp.Value.Load)
            {
                Pred = new List<string>(kvp.Value.Pred),
                Succ = new List<string>(kvp.Value.Succ)
            });
            this.agents = agents.ToDictionary(kvp => kvp.Key, kvp => new Agent(kvp.Key)
            {
                Neighbors = new HashSet<int>(kvp.Value.Neighbors),
                AssignedModules = new HashSet<string>(kvp.Value.AssignedModules),
                AvailableAt = kvp.Value.AvailableAt
            });

            rng = new Random(seed);
            timeline = new SortedSet<TimelineEntry>(Comparer<TimelineEntry>.Create((a, b) =>
                a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Id.CompareTo(b.Id)));
        }

        private int timelineCounter = 0;

        private void Schedule(double time, Action action)
            => timeline.Add(new TimelineEntry { Time = time, Id = timelineCounter++, Action = action });

        private int FindAgentForModule(string mid)
            => agents.FirstOrDefault(a => a.Value.AssignedModules.Contains(mid)).Key;

        private void TryStartReadyModules()
        {
            foreach (var m in modules.Values.Where(m => !m.Finished && !m.Running))
            {
                if (m.Pred.Any(p => !modules[p].Finished)) continue;
                int aid = FindAgentForModule(m.Id);
                if (aid < 0) continue;
                var agent = agents[aid];
                if (running.Values.Any(r => r.AgentId == aid && r.End > now)) continue;

                StartModuleOnAgent(m.Id, aid, Math.Max(now, agent.AvailableAt));
            }
        }

        private void StartModuleOnAgent(string mid, int aid, double startTime)
        {
            var m = modules[mid];
            var a = agents[aid];
            m.Running = true;
            double endTime = startTime + m.Load;
            bool fail = rng.NextDouble() < failureProb;

            if (fail)
            {
                double failAt = startTime + rng.NextDouble() * m.Load;
                running[mid] = new() { ModuleId = mid, AgentId = aid, Start = startTime, End = failAt, IsFailureEvent = true };
                a.AvailableAt = failAt;
                Schedule(failAt, () => OnEnd(mid));
            }
            else
            {
                running[mid] = new() { ModuleId = mid, AgentId = aid, Start = startTime, End = endTime, IsFailureEvent = false };
                a.AvailableAt = endTime;
                Schedule(endTime, () => OnEnd(mid));
            }
        }

        private void OnEnd(string mid)
        {
            if (!running.ContainsKey(mid)) return;
            var sch = running[mid];
            now = sch.End;
            var a = agents[sch.AgentId];
            var m = modules[mid];

            if (sch.IsFailureEvent)
            {
                LogFail($"FAILURE {mid} on agent {sch.AgentId}", now);
                m.Running = false;
                running.Remove(mid);
                Schedule(now + 0.1, TryStartReadyModules);
                return;
            }

            LogSuccess($"COMPLETE {mid} on agent {sch.AgentId}", now);
            m.Running = false;
            m.Finished = true;
            running.Remove(mid);
            lastCompletion = Math.Max(lastCompletion, now);

            Balancer.Redistribute(Mode, agents, modules, now);
            Schedule(now + 0.001, TryStartReadyModules);
        }

        public double Run()
        {
            now = 0;
            foreach (var m in modules.Values) { m.Finished = false; m.Running = false; }
            Schedule(0, TryStartReadyModules);

            while (timeline.Any())
            {
                var item = timeline.Min;
                timeline.Remove(item);
                now = item.Time;
                item.Action();
            }
            return lastCompletion;
        }

        private static void LogSuccess(string msg, double t)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[t={t,6:F3}] {msg}");
            Console.ResetColor();
        }

        private static void LogFail(string msg, double t)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[t={t,6:F3}] {msg}");
            Console.ResetColor();
        }
    }

    class Program
    {
        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            RunTest("Тест 1 — Simple", BalanceMode.Simple);
            RunTest("Тест 2 — Smart", BalanceMode.Smart);
            RunTest("Тест 3 — LFT", BalanceMode.LFT);
            RunTest("Тест 4 — Swarm", BalanceMode.Swarm);
            RunFailureTest();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n Все тесты завершены.\n");
            Console.ResetColor();
        }

        static void RunTest(string name, BalanceMode mode)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n==============================");
            Console.WriteLine($"{name}");
            Console.WriteLine("==============================");
            Console.ResetColor();

            var mods = MakeModules(new[] { ("A", 2.0), ("B", 3.0), ("C", 1.5), ("D", 2.5) },
                                   new[] { ("A", "B"), ("B", "C"), ("C", "D") });
            var agents = MakeAgents(3, new[] { (0, 1), (1, 2) });
            InitialAssignment(mods, agents);

            var sim = new FullSimulator(mods, agents, mode, seed: 42);
            double total = sim.Run();

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"Итоговое время выполнения: {total:F3}\n");
            Console.ResetColor();
            Thread.Sleep(300); 
        }

        static Dictionary<string, Module> MakeModules((string id, double load)[] nodes, (string a, string b)[] edges)
        {
            var dict = nodes.ToDictionary(n => n.id, n => new Module(n.id, n.load));
            foreach (var e in edges)
            {
                dict[e.a].Succ.Add(e.b);
                dict[e.b].Pred.Add(e.a);
            }
            return dict;
        }

        static Dictionary<int, Agent> MakeAgents(int n, (int a, int b)[] edges)
        {
            var agents = new Dictionary<int, Agent>();
            for (int i = 0; i < n; i++) agents[i] = new Agent(i);
            foreach (var e in edges)
            {
                agents[e.a].Neighbors.Add(e.b);
                agents[e.b].Neighbors.Add(e.a);
            }
            return agents;
        }

        static void InitialAssignment(Dictionary<string, Module> modules, Dictionary<int, Agent> agents)
        {
            var indeg = modules.ToDictionary(kv => kv.Key, kv => kv.Value.Pred.Count);
            var q = new Queue<string>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            var topo = new List<string>();
            while (q.Any())
            {
                var v = q.Dequeue();
                topo.Add(v);
                foreach (var s in modules[v].Succ)
                {
                    indeg[s]--;
                    if (indeg[s] == 0) q.Enqueue(s);
                }
            }

            var ids = agents.Keys.ToArray();
            int i = 0;
            foreach (var m in topo)
            {
                agents[ids[i % ids.Length]].AssignedModules.Add(m);
                i++;
            }
        }

        static void RunFailureTest()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n==============================");
            Console.WriteLine("Тест 5 — Сценарий с отказами");
            Console.WriteLine("==============================");
            Console.ResetColor();

            var mods = MakeModules(
                new[] { ("A", 3.0), ("B", 4.0), ("C", 2.0), ("D", 3.5), ("E", 2.5), ("F", 1.5) },
                new[] { ("A", "B"), ("A", "C"), ("B", "D"), ("C", "E"), ("D", "F"), ("E", "F") }
            );

            var agents = MakeAgents(3, new[] { (0, 1), (1, 2), (0, 2) });
            InitialAssignment(mods, agents);

            var sim = new FullSimulator(mods, agents, BalanceMode.Swarm, seed: 2025);

            var field = typeof(FullSimulator).GetField("failureProb", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(sim, 0.30);

            double total = sim.Run();

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"Итоговое время выполнения: {total:F3}\n");
            Console.ResetColor();
        }

    }
}
