using System;
using System.Collections.Generic;
using System.Linq;

namespace CardLab6_CounterVsFinisher
{
    enum Suit { Clubs, Diamonds, Hearts, Spades }

    class Card
    {
        public Suit Suit { get; }
        public int Rank { get; }
        public Card(Suit suit, int rank) { Suit = suit; Rank = rank; }

        public override string ToString() => $"{RankToString(Rank)}{SuitToSymbol(Suit)}";
        static string RankToString(int r) => r switch { 11 => "J", 12 => "Q", 13 => "K", 14 => "A", _ => r.ToString() };
        static string SuitToSymbol(Suit s) => s switch { Suit.Clubs => "♣", Suit.Diamonds => "♦", Suit.Hearts => "♥", Suit.Spades => "♠", _ => "?" };
    }

    class Deck
    {
        private Stack<Card> cards;
        public Card TrumpCard { get; private set; }

        public Deck(Random rng)
        {
            var list = new List<Card>();
            foreach (Suit s in Enum.GetValues(typeof(Suit)))
                for (int r = 2; r <= 14; r++)
                    list.Add(new Card(s, r));

            list = list.OrderBy(_ => rng.Next()).ToList();
            TrumpCard = list.Last();
            cards = new Stack<Card>(list);
        }

        public Card? Draw() => cards.Count > 0 ? cards.Pop() : null;
        public int Count => cards.Count;
        public Card PeekTrump() => TrumpCard;
    }

    enum StrategyType { CardCounter, Finisher }

    class Player
    {
        public int Id { get; }
        public int TeamId { get; }
        public StrategyType Strategy { get; }
        public List<Card> Hand { get; } = new List<Card>();
        public int SeenTrumps { get; set; } = 0;
        public string Name { get; }

        public static readonly string[] CardCounters =
        {
            "Sherlock", "Spock", "Data", "Gandalf", "Watson", "Einstein", "Hawking", "Neo"
        };

        public static readonly string[] Finishers =
        {
            "Thor", "Conan", "Zeus", "Hulk", "Kratos", "Leonidas", "Ragnar", "Blade"
        };

        public Player(int id, int teamId, StrategyType strat, string name)
        {
            Id = id;
            TeamId = teamId;
            Strategy = strat;
            Name = name;
        }

        public void Add(Card? c) { if (c != null) Hand.Add(c); }
        public void Remove(Card? c) { if (c != null) Hand.Remove(c); }
        public bool IsEmpty => Hand.Count == 0;

        public Card? ChooseAttackCard(Card trumpCard)
        {
            if (!Hand.Any()) return null;

            var trumps = Hand.Where(c => c.Suit == trumpCard.Suit).OrderBy(c => c.Rank).ToList();
            var nonTrumps = Hand.Where(c => c.Suit != trumpCard.Suit).OrderBy(c => c.Rank).ToList();

            Card? choice;
            if (Hand.Count > 3)
                choice = nonTrumps.FirstOrDefault() ?? trumps.FirstOrDefault();
            else
                choice = nonTrumps.LastOrDefault() ?? trumps.LastOrDefault();

            return choice;
        }

        public Card? ChooseDefenseCard(Card attackCard, Suit trumpSuit)
        {
            var sameSuit = Hand.Where(c => c.Suit == attackCard.Suit && c.Rank > attackCard.Rank)
                               .OrderBy(c => c.Rank).ToList();
            var trumpCandidates = Hand.Where(c => c.Suit == trumpSuit && attackCard.Suit != trumpSuit)
                                      .OrderBy(c => c.Rank).ToList();

            Card? choice = sameSuit.FirstOrDefault() ?? trumpCandidates.FirstOrDefault();
            return choice;
        }

        public Card? ChooseTransferCard(Card attackCard)
        {
            var candidates = Hand.Where(c => c.Rank == attackCard.Rank && c.Suit != attackCard.Suit)
                                 .OrderBy(c => c.Rank).ToList();
            if (!candidates.Any() || Hand.Any(c => c.Suit == attackCard.Suit && c.Rank > attackCard.Rank))
                return null;
            return candidates.First();
        }

        public List<Card> ChooseExtraCards(List<int> tableRanks, int limit, Card trumpCard, Player partner)
        {
            var selected = new List<Card>();
            var candidates = Hand.Where(c => tableRanks.Contains(c.Rank)).ToList();
            if (!candidates.Any()) return selected;

            foreach (var c in candidates.Where(c => c.Suit != trumpCard.Suit).OrderBy(c => c.Rank))
            {
                if (selected.Count >= limit) break;
                selected.Add(c);
            }

            foreach (var c in candidates.Where(c => c.Suit == trumpCard.Suit).OrderBy(c => c.Rank))
            {
                if (selected.Count >= limit) break;
                selected.Add(c);
            }

            return selected;
        }
    }

    class Game
    {
        private Deck? deck;
        private Card? trumpCard;
        private Suit trumpSuit;
        private Player[] players = new Player[4];
        private Random rng;

        public Game(Random rng)
        {
            this.rng = rng;

            var teamANames = Player.CardCounters.OrderBy(_ => rng.Next()).Take(2).ToList();
            var teamBNames = Player.Finishers.OrderBy(_ => rng.Next()).Take(2).ToList();

            players[0] = new Player(0, 0, StrategyType.CardCounter, teamANames[0]);
            players[2] = new Player(2, 0, StrategyType.CardCounter, teamANames[1]);

            players[1] = new Player(1, 1, StrategyType.Finisher, teamBNames[0]);
            players[3] = new Player(3, 1, StrategyType.Finisher, teamBNames[1]);
        }

        public void Initialize()
        {
            deck = new Deck(rng);
            trumpCard = deck.PeekTrump();
            trumpSuit = trumpCard.Suit;

            foreach (var p in players)
            {
                p.Hand.Clear();
                p.SeenTrumps = 0;
            }

            for (int i = 0; i < 6; i++)
                for (int id = 0; id < 4; id++)
                    players[id].Add(deck.Draw());

            foreach (var p in players)
                p.SeenTrumps = p.Hand.Count(c => c.Suit == trumpSuit) + 1;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Команда A: {players[0].Name}, {players[2].Name}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Команда B: {players[1].Name}, {players[3].Name}");
            Console.ResetColor();
            Console.WriteLine($"Козырь: {trumpCard}\n");
        }

        private int PartnerOf(int id) => id switch { 0 => 2, 2 => 0, 1 => 3, _ => 1 };

        public int PlayUntilTeamWins()
        {
            Initialize();

            int leaderA = FindFirstLeader(0, 2);
            int leaderB = FindFirstLeader(1, 3);

            int attackingTeam = rng.Next(0, 2);
            int[] leaders = { leaderA, leaderB };

            int rounds = 0;
            while (true)
            {
                rounds++;
                if (players[0].IsEmpty && players[2].IsEmpty) return 0;
                if (players[1].IsEmpty && players[3].IsEmpty) return 1;

                bool defenseSuccess = PlayRound(leaders[attackingTeam], leaders[1 - attackingTeam]);
                int winningTeam = defenseSuccess ? players[leaders[1 - attackingTeam]].TeamId : attackingTeam;

                RefillHands(leaders[winningTeam]);
                attackingTeam = winningTeam;

                leaders[0] = leaders[0] == 0 ? 2 : 0;
                leaders[1] = leaders[1] == 1 ? 3 : 1;

                if (rounds > 2000)
                {
                    int sumA = players[0].Hand.Count + players[2].Hand.Count;
                    int sumB = players[1].Hand.Count + players[3].Hand.Count;
                    return sumA <= sumB ? 0 : 1;
                }
            }
        }

        private int FindFirstLeader(int p1, int p2)
        {
            var c1 = players[p1].Hand.Where(c => c.Suit == trumpSuit).OrderBy(c => c.Rank).FirstOrDefault();
            var c2 = players[p2].Hand.Where(c => c.Suit == trumpSuit).OrderBy(c => c.Rank).FirstOrDefault();
            if (c1 == null) return p2;
            if (c2 == null) return p1;
            return c1.Rank <= c2.Rank ? p1 : p2;
        }

        private void RefillHands(int start)
        {
            for (int i = 0; i < 4; i++)
            {
                int id = (start + i) % 4;
                while (players[id].Hand.Count < 6)
                {
                    var card = deck?.Draw();
                    if (card == null) break;
                    players[id].Add(card);
                }
            }
        }

        private bool PlayRound(int attacker, int defender)
        {
            var attackPile = new List<Card>();
            var defensePile = new List<Card?>();

            bool firstTurn = true;
            int defenderInitialHand = players[defender].Hand.Count;

            while (true)
            {
                var attackCard = EvaluateAttack(attacker, defender);
                if (attackCard == null || players[defender].Hand.Count == 1) break;

                players[attacker].Remove(attackCard);
                attackPile.Add(attackCard);
                defensePile.Add(null);

                if (firstTurn)
                {
                    var transfer = players[defender].ChooseTransferCard(attackCard);
                    if (transfer != null)
                    {
                        players[defender].Remove(transfer);
                        attackPile.Add(transfer);
                        defensePile.Add(null);
                        (attacker, defender) = (defender, attacker);
                        firstTurn = false;
                        continue;
                    }
                }

                int lastIdx = attackPile.Count - 1;
                var defenseCard = players[defender].ChooseDefenseCard(attackPile[lastIdx], trumpSuit);
                if (defenseCard == null)
                {
                    var allCards = attackPile.Concat(defensePile.Where(c => c != null).Select(c => c!)).ToList();
                    foreach (var c in allCards) players[defender].Add(c);
                    return false;
                }

                players[defender].Remove(defenseCard);
                defensePile[lastIdx] = defenseCard;

                var tableRanks = attackPile.Select(c => c.Rank)
                                           .Concat(defensePile.Where(c => c != null).Select(c => c!.Rank))
                                           .Distinct().ToList();
                int extraLimit = Math.Max(0, defenderInitialHand - attackPile.Count);

                if (extraLimit > 0)
                {
                    var extrasA = players[attacker].ChooseExtraCards(tableRanks, extraLimit, trumpCard!, players[PartnerOf(attacker)]);
                    foreach (var c in extrasA)
                    {
                        players[attacker].Remove(c);
                        attackPile.Add(c);
                        defensePile.Add(null);
                        extraLimit--;
                    }

                    if (extraLimit > 0)
                    {
                        var partner = PartnerOf(attacker);
                        var extrasB = players[partner].ChooseExtraCards(tableRanks, extraLimit, trumpCard!, players[attacker]);
                        foreach (var c in extrasB)
                        {
                            players[partner].Remove(c);
                            attackPile.Add(c);
                            defensePile.Add(null);
                            extraLimit--;
                        }
                    }
                }

                if (!defensePile.Any(d => d == null)) return true;
                firstTurn = false;
            }

            if (!defensePile.Any(d => d == null)) return true;

            var takeCards = attackPile.Concat(defensePile.Where(c => c != null).Select(c => c!)).ToList();
            foreach (var c in takeCards) players[defender].Add(c);
            return false;
        }

        private Card? EvaluateAttack(int attacker, int defender)
        {
            var a = players[attacker];
            var d = players[defender];

            if (!a.Hand.Any()) return null;

            if (a.Strategy == StrategyType.Finisher && d.Hand.Count <= 2)
                return a.Hand.Where(c => c.Suit != trumpSuit).OrderByDescending(c => c.Rank).FirstOrDefault()
                    ?? a.Hand.OrderByDescending(c => c.Rank).First();

            return a.ChooseAttackCard(trumpCard!);
        }
    }

    class Simulator
    {
        public static void Run(int games = 1000)
        {
            var rng = new Random();
            int teamA = 0, teamB = 0;

            for (int i = 0; i < games; i++)
            {
                var game = new Game(rng);
                int winner = game.PlayUntilTeamWins();
                if (winner == 0) teamA++; else teamB++;
            }

            Console.WriteLine($"После {games} игр:");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Команда A (CardCounter) побед: {teamA} ({teamA * 100.0 / games:F2}%)");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Команда B (Finisher) побед: {teamB} ({teamB * 100.0 / games:F2}%)");
            Console.ResetColor();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            int runs = 1000;
            if (args.Length >= 1 && int.TryParse(args[0], out int r)) runs = r;
            Simulator.Run(runs);
        }
    }
}
