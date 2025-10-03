#include <iostream>
#include <vector>
#include <cmath>
#include <random>
#include <fstream>
#include <string>

struct Position {
    double x, y;
};

struct Square {
    Position center;
    double size;
    int row, col;
};

class Court {
public:
    double width, height;
    int n;
    std::vector<Square> squares;

    Court(double width, double height, int n) : width(width), height(height), n(n) {
        generateSquares();
    }

    void generateSquares() {
        double squareSizeX = width / n;
        double squareSizeY = height / n;
        for (int i = 0; i < n; i++) {
            for (int j = 0; j < n; j++) {
                squares.push_back({{(i + 0.5) * squareSizeX, (j + 0.5) * squareSizeY}, squareSizeX, i, j});
            }
        }
    }

    Square getSquare(int row, int col) const {  
        return squares[row * n + col];
    }
};


class Player {
public:
    Position pos;
    double r;
    double l;

    Player(double r, double l, Position start) : r(r), l(l), pos(start) {}

    bool canHit(const Position& ball) const {
        double dist = std::hypot(ball.x - pos.x, ball.y - pos.y);
        return dist <= r;
    }

    void moveTo(Position target) {
        double dx = target.x - pos.x;
        double dy = target.y - pos.y;
        double dist = std::hypot(dx, dy);
        if (dist <= l) {
            pos = target;
        } else {
            pos.x += dx / dist * l;
            pos.y += dy / dist * l;
        }
    }
};

class Strategy {
public:
    double errorProb = 0.05;
    int n;

    Strategy(int n) : n(n) {}

    Square chooseSquare(const Player& agent, const Player& bot, const Court& court, std::default_random_engine& rng) {
        double bestScore = -1;
        Square bestSquare = court.squares[0];

        for (auto& sq : court.squares) {
            // 1. Расстояние от болванчика до выбранного квадрата (чем больше — тем лучше для агента)
            double botDist = std::hypot(sq.center.x - bot.pos.x, sq.center.y - bot.pos.y);

            // 2. Расстояние от агента до квадрата (чем меньше — тем лучше для агента)
            double agentDist = std::hypot(sq.center.x - agent.pos.x, sq.center.y - agent.pos.y);

            // 3. Вероятность, что агент сможет добраться до мяча
            double hitProbability = std::min(1.0, agent.r / (agentDist + 0.1)); // +0.1 чтобы избежать деления на 0

            // 4. Итоговая оценка квадрата: балансируем расстояния и вероятность попадания
            double score = botDist * hitProbability;

            if (score > bestScore) {
                bestScore = score;
                bestSquare = sq;
            }
        }

        // Ошибка 5% — случайный соседний квадрат или аут
        std::uniform_real_distribution<double> errorDist(0, 1);
        if (errorDist(rng) < errorProb) {
            int dRow[] = {-1, 0, 1, 0};
            int dCol[] = {0, -1, 0, 1};
            std::uniform_int_distribution<int> dir(0, 3);
            int dirIndex = dir(rng);

            int newRow = bestSquare.row + dRow[dirIndex];
            int newCol = bestSquare.col + dCol[dirIndex];
            if (newRow >= 0 && newRow < n && newCol >= 0 && newCol < n) {
                bestSquare = court.getSquare(newRow, newCol);
            } else {
                // Попал в аут — возвращаем специальный квадрат
                return {-1, -1, 0, -1, -1};
            }
        }

        return bestSquare;
    }
};

class Match {
public:
    Player agent;
    Player bot;
    Court court;
    Strategy strategy;
    std::default_random_engine rng;

    int agentPoints = 0, botPoints = 0;
    int agentGames = 0, botGames = 0;
    int agentSets = 0, botSets = 0;

    Match(double r_agent, double l_agent, double r_bot, double l_bot, int n)
        : agent(r_agent, l_agent, {10, 0}),
          bot(r_bot, l_bot, {10, 10}),
          court(20, 10, n),
          strategy(n) {
        rng.seed(std::random_device{}());
    }

    bool simulatePoint(bool serve = false) {
        Square targetSquare;
        if (serve) {
            targetSquare = strategy.chooseSquare(agent, bot, court, rng);
            if (targetSquare.row == -1) return false; // аут при подаче
        } else {
            targetSquare = strategy.chooseSquare(agent, bot, court, rng);
            if (targetSquare.row == -1) return false;
        }

        // Болванчик пытается отбить
        std::uniform_real_distribution<double> noise(-targetSquare.size / 2, targetSquare.size / 2);
        Position ball = {targetSquare.center.x + noise(rng), targetSquare.center.y + noise(rng)};
        bot.moveTo(ball);

        if (!bot.canHit(ball)) {
            agentPoints++;
            return true;
        }

        // Болванчик отправляет мяч в случайную точку половины корта агента
        std::uniform_real_distribution<double> xDist(0, court.width / 2);
        std::uniform_real_distribution<double> yDist(0, court.height);
        Position returnBall = {xDist(rng), yDist(rng)};
        agent.moveTo(returnBall);

        if (!agent.canHit(returnBall)) {
            botPoints++;
            return false;
        }

        return simulatePoint(false);
    }

    void playGame(bool firstServe = true) {
        agentPoints = 0;
        botPoints = 0;
        while (true) {
            bool serve = (agentPoints + botPoints == 0) && firstServe;
            bool agentWon = simulatePoint(serve);

            if (agentWon) agentPoints++;
            else botPoints++;

            if (agentPoints >= 4 && agentPoints - botPoints >= 2) {
                agentGames++;
                break;
            }
            if (botPoints >= 4 && botPoints - agentPoints >= 2) {
                botGames++;
                break;
            }
        }
    }

    void playSet() {
        agentGames = 0;
        botGames = 0;
        while (true) {
            playGame();

            if (agentGames >= 6 && agentGames - botGames >= 2) {
                agentSets++;
                break;
            }
            if (botGames >= 6 && botGames - agentGames >= 2) {
                botSets++;
                break;
            }
        }
    }

    void playMatch(int bestOfSets) {
        agentSets = 0;
        botSets = 0;
        while (agentSets < bestOfSets && botSets < bestOfSets) {
            playSet();
        }
    }
};

int main() {
    const int simulations = 100;
    const int bestOfSets = 2;
    const int n = 10;
    const double r_robot = 2.0;
    const double l_robot = 3.0;

    std::ofstream results("results.csv");
    results << "r_agent,l_agent,n,agentWins,botWins\n";

    for (double r_agent = 1.0; r_agent <= 3.0; r_agent += 1.0) {
        for (double l_agent = 2.0; l_agent <= 4.0; l_agent += 1.0) {
            int agentWinCount = 0;
            int botWinCount = 0;

            for (int i = 0; i < simulations; i++) {
                Match match(r_agent, l_agent, r_robot, l_robot, n);
                match.playMatch(bestOfSets);

                if (match.agentSets > match.botSets) agentWinCount++;
                else botWinCount++;
            }

            results << r_agent << "," << l_agent << "," << n << "," << agentWinCount << "," << botWinCount << "\n";
            std::cout << "r_agent=" << r_agent << " l_agent=" << l_agent
                      << " Agent wins: " << agentWinCount << "/" << simulations << "\n";
        }
    }

    results.close();
    std::cout << "Симуляция завершена. Результаты в results.csv\n";
    return 0;
}
