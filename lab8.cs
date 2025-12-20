using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MarsColoniesSimulation
{
    enum ColonyState { Alive, Destroyed, Won }

    enum EffectType
    {
        IncomeFromExpensePercent,
        BalancePercent,
        ExpenseFromBalancePercent,
        BalanceFlat,
        ExperienceFromCurrentPercent,
        ExperienceFromMaxPercent,
        LevelBoost,
        BalanceMultiply,
        ExpenseFromIncomePercent
    }

    enum EffectDurationType
    {
        Iterations,
        Cycles,
        UntilNextAuction,
        OneTime
    }

    class Effect
    {
        public EffectType Type;
        public EffectDurationType DurationType;
        public int Remaining;
        public double Value;
        public bool Applied;

        public Effect(EffectType type, EffectDurationType durationType, int duration, double value)
        {
            Type = type;
            DurationType = durationType;
            Remaining = duration;
            Value = value;
        }

        public bool Expired =>
            DurationType != EffectDurationType.UntilNextAuction &&
            DurationType != EffectDurationType.OneTime &&
            Remaining <= 0;
    }

    class Colony
    {
        public int Id;
        public int Level = 1;
        public const int MaxLevel = 10;

        public double Balance;
        public double BaseIncome;
        public double BaseExpense;

        public double Experience;
        public double ExperienceLimit;

        public ColonyState State = ColonyState.Alive;
        public bool HasArtifact;
        public int Lifetime;

        public List<Effect> ActiveEffects = new();

        public Colony(int id, double balance, double income, double expense, double expLimit)
        {
            Id = id;
            Balance = balance;
            BaseIncome = income;
            BaseExpense = expense;
            ExperienceLimit = expLimit;
        }

        public double GetIncome()
        {
            double income = BaseIncome;

            foreach (var e in ActiveEffects)
                if (e.Type == EffectType.IncomeFromExpensePercent)
                    income += BaseExpense * e.Value;

            return income;
        }

        public double GetExpense()
        {
            double expense = BaseExpense;

            foreach (var e in ActiveEffects)
            {
                if (e.Type == EffectType.ExpenseFromBalancePercent)
                    expense -= Balance * e.Value;

                if (e.Type == EffectType.ExpenseFromIncomePercent)
                    expense -= BaseIncome * e.Value;
            }

            return Math.Max(0, expense);
        }

        public void ApplyEffects(bool auctionHappened)
        {
            foreach (var e in ActiveEffects.ToList())
            {
                switch (e.Type)
                {
                    case EffectType.BalancePercent when !e.Applied:
                        Balance += Balance * e.Value;
                        e.Applied = true;
                        break;

                    case EffectType.BalanceFlat:
                        Balance += e.Value;
                        break;

                    case EffectType.ExperienceFromCurrentPercent:
                        Experience += Experience * e.Value;
                        break;

                    case EffectType.ExperienceFromMaxPercent when !e.Applied:
                        Experience += ExperienceLimit * e.Value;
                        e.Applied = true;
                        break;

                    case EffectType.LevelBoost when !e.Applied:
                        Level = Math.Min(MaxLevel, Level + (int)e.Value);
                        e.Applied = true;
                        break;

                    case EffectType.BalanceMultiply when !e.Applied:
                        Balance *= 2;
                        e.Applied = true;
                        break;
                }

                if (e.DurationType == EffectDurationType.Cycles ||
                    e.DurationType == EffectDurationType.Iterations)
                    e.Remaining--;

                if (e.DurationType == EffectDurationType.UntilNextAuction && auctionHappened)
                    e.Remaining = 0;

                if (e.Expired || e.DurationType == EffectDurationType.OneTime)
                    ActiveEffects.Remove(e);
            }
        }

        public void ApplyCycle()
        {
            if (State != ColonyState.Alive) return;

            double prevBalance = Balance;
            Balance += GetIncome() - GetExpense();

            Experience += Balance - prevBalance;
            Experience = Math.Max(0, Experience);

            if (Experience >= ExperienceLimit && Level < MaxLevel)
            {
                Experience = 0;
                Level++;
            }

            Lifetime++;

            if (Balance < 0)
                State = ColonyState.Destroyed;
            else if (Level >= MaxLevel)
                State = ColonyState.Won;
        }
    }

    class Artifact
    {
        public string Name;
        public double Cost;
        public Action<Colony> Apply;

        public Artifact(string name, double cost, Action<Colony> apply)
        {
            Name = name;
            Cost = cost;
            Apply = apply;
        }
    }

    class AuctionAgent
    {
        public double Bid(Colony c)
        {
            if (c.HasArtifact) return 0;
            return c.Balance * 0.25;
        }
    }

    class Simulation
    {
        public List<Colony> Colonies;
        public int T;
        public int AuctionPeriod;
        public int EventPeriod;
        Random rnd = new();

        public Simulation(List<Colony> colonies, int t, int ta, int te)
        {
            Colonies = colonies;
            T = t;
            AuctionPeriod = ta;
            EventPeriod = te;
        }

        public void Run(List<Artifact> artifacts)
        {
            var agent = new AuctionAgent();

            for (int t = 1; t <= T; t++)
            {
                bool auctionHappened = t % AuctionPeriod == 0;

                if (auctionHappened)
                {
                    foreach (var art in artifacts)
                    {
                        var bids = Colonies
                            .Where(c => c.State == ColonyState.Alive && !c.HasArtifact)
                            .ToDictionary(c => c, c => agent.Bid(c));

                        if (!bids.Any()) continue;

                        var winner = bids.OrderByDescending(b => b.Value).First().Key;

                        if (winner.Balance >= art.Cost)
                        {
                            winner.Balance -= art.Cost;
                            art.Apply(winner);
                            winner.HasArtifact = true;
                        }
                    }
                }

                if (t % EventPeriod == 0)
                {
                    foreach (var c in Colonies.Where(c => c.State == ColonyState.Alive))
                    {
                        if (rnd.NextDouble() < 0.5)
                        {
                            c.BaseIncome -= rnd.Next(5, 15);
                            c.BaseExpense += 3;
                        }
                        else
                        {
                            c.BaseIncome += 5;
                            c.BaseExpense = Math.Max(0, c.BaseExpense - 3);
                        }
                    }
                }

                foreach (var c in Colonies)
                {
                    c.ApplyEffects(auctionHappened);
                    c.ApplyCycle();
                }
            }
        }
    }

    class SimulationResult
    {
        public double ParameterValue { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public List<int> Lifetimes { get; set; }
    }

    class Program
    {
        static void Main()
        { 
            double[] initialBalances = { 50, 75, 100, 125, 150 };
            int[] auctionPeriods = { 5, 10, 15, 20 };
            int numExperiments = 100; 

            var allResults = new List<SimulationResult>();

            foreach (var B in initialBalances)
            {
                var lifetimes = new List<int>();
                int totalWins = 0;
                int totalLosses = 0;

                for (int exp = 0; exp < numExperiments; exp++)
                {
                    Random rnd = new Random(Guid.NewGuid().GetHashCode());
                    int colonyCount = 100;

                    var colonies = new List<Colony>();
                    for (int i = 0; i < colonyCount; i++)
                        colonies.Add(new Colony(
                            i,
                            B,
                            income: 10 + rnd.NextDouble() * 15,
                            expense: 8 + rnd.NextDouble() * 12,
                            expLimit: 100));

                    var artifacts = CreateArtifacts();
                    var sim = new Simulation(colonies, 300, 10, 15);
                    sim.Run(artifacts);

                    foreach (var c in colonies)
                    {
                        lifetimes.Add(c.Lifetime);
                        if (c.State == ColonyState.Won) totalWins++;
                        if (c.State == ColonyState.Destroyed) totalLosses++;
                    }
                }

                allResults.Add(new SimulationResult
                {
                    ParameterValue = B,
                    Wins = totalWins,
                    Losses = totalLosses,
                    Lifetimes = lifetimes
                });
            }

            SaveResultsToCSV(allResults, "simulation_results_balance.csv");
        }

        static List<Artifact> CreateArtifacts()
        {
            return new List<Artifact>
        {
                new("14", 20, c =>
                {
                    c.ActiveEffects.Add(new Effect(EffectType.IncomeFromExpensePercent, EffectDurationType.Iterations, 5, 0.2));
                    c.ActiveEffects.Add(new Effect(EffectType.BalancePercent, EffectDurationType.UntilNextAuction, 0, 0.15));
                    c.ActiveEffects.Add(new Effect(EffectType.ExpenseFromBalancePercent, EffectDurationType.OneTime, 0, 0.1));
                }),
                new("52", 25, c =>
                {
                    c.ActiveEffects.Add(new Effect(EffectType.BalanceFlat, EffectDurationType.Cycles, 5, 10));
                    c.ActiveEffects.Add(new Effect(EffectType.IncomeFromExpensePercent, EffectDurationType.UntilNextAuction, 0, 0.2));
                    c.ActiveEffects.Add(new Effect(EffectType.ExperienceFromCurrentPercent, EffectDurationType.Cycles, 5, 0.25));
                }),
                new("56", 30, c =>
                {
                    c.ActiveEffects.Add(new Effect(EffectType.BalanceFlat, EffectDurationType.Cycles, 7, 12));
                    c.ActiveEffects.Add(new Effect(EffectType.IncomeFromExpensePercent, EffectDurationType.UntilNextAuction, 0, 0.15));
                    c.ActiveEffects.Add(new Effect(EffectType.ExperienceFromMaxPercent, EffectDurationType.OneTime, 0, 0.3));
                }),
                new("67", 40, c =>
                {
                    c.ActiveEffects.Add(new Effect(EffectType.LevelBoost, EffectDurationType.OneTime, 0, 2));
                    c.ActiveEffects.Add(new Effect(EffectType.ExpenseFromBalancePercent, EffectDurationType.UntilNextAuction, 0, 0.2));
                    c.ActiveEffects.Add(new Effect(EffectType.BalancePercent, EffectDurationType.UntilNextAuction, 0, 0.15));
                }),
                new("69", 50, c =>
                {
                    c.ActiveEffects.Add(new Effect(EffectType.BalanceMultiply, EffectDurationType.OneTime, 0, 0));
                    c.ActiveEffects.Add(new Effect(EffectType.ExpenseFromIncomePercent, EffectDurationType.UntilNextAuction, 0, 0.25));
                    c.ActiveEffects.Add(new Effect(EffectType.ExperienceFromMaxPercent, EffectDurationType.Cycles, 5, 0.2));
                })
            };
        }

        static void SaveResultsToCSV(List<SimulationResult> results, string filename)
            {
                using var sw = new StreamWriter(filename);
                sw.WriteLine("ParameterValue,Wins,Losses,Lifetimes");
                foreach (var r in results)
                {
                    string lifetimes = string.Join(";", r.Lifetimes);
                    sw.WriteLine($"{r.ParameterValue},{r.Wins},{r.Losses},\"{lifetimes}\"");
                }
            }
        }
    }
