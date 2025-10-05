#include <iostream>
#include <vector>
#include <cmath>
#include <random>
#include <fstream>
#include <string>
#include <iomanip>

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
                squares.push_back({ {(i + 0.5) * squareSizeX, (j + 0.5) * squareSizeY}, squareSizeX, i, j });
            }
        }
    }

    const Square& getSquare(int row, int col) const {
        return squares[row * n + col];
    }

    bool isOut(const Position& p) const {
        return p.x < 0.0 || p.x > width || p.y < 0.0 || p.y > height;
    }
};


class Player {
public:
    Position pos;
    double r;
    double l;

    Player(double r, double l, Position start) : r(r), l(l), pos(start) {}

    bool canHit(const Position& ball) const {
        double dx = ball.x - pos.x;
        double dy = ball.y - pos.y;
        if (dy < 0.0) return false; 
        return std::hypot(dx, dy) <= r;
    }

    void moveTo(Position target) {
        double dx = target.x - pos.x;
        double dy = target.y - pos.y;
        double dist = std::hypot(dx, dy);
        if (dist <= l) {
            pos = target;
        }
        else {
            pos.x += dx / dist * l;
            pos.y += dy / dist * l;
        }
    }
};

class Strategy {
public:
    double errorProb = 0.05;
    int n;

    bool trapMode = false;      
    Position lastServeTarget;    
    bool hasLastTarget = false;  

    Strategy(int n) : n(n) {}

    Square chooseSquare(const Player& agent, const Player& bot, const Court& court,
        std::default_random_engine& rng, bool isServe = false) {
        if (trapMode && hasLastTarget) {
            double maxDist = -1.0;
            Square farthestSquare = court.squares[0];
            for (const auto& sq : court.squares) {
                double dist = std::hypot(sq.center.x - lastServeTarget.x, sq.center.y - lastServeTarget.y);
                if (dist > maxDist) {
                    maxDist = dist;
                    farthestSquare = sq;
                }
            }
            trapMode = false; 
            hasLastTarget = false;
            return farthestSquare;
        }

        double bestScore = -1.0;
        Square bestSquare = court.squares[0];

        for (const auto& sq : court.squares) {
            double botDist = std::hypot(sq.center.x - bot.pos.x, sq.center.y - bot.pos.y);
            double agentDist = std::hypot(sq.center.x - agent.pos.x, sq.center.y - agent.pos.y);
            double hitProbability = std::min(1.0, agent.r / (agentDist + 0.1));
            double score = botDist * hitProbability;
            if (score > bestScore) {
                bestScore = score;
                bestSquare = sq;
            }
        }

        if (!isServe) {
            std::uniform_real_distribution<double> errorDist(0.0, 1.0);
            if (errorDist(rng) < errorProb) {
                int dRow[] = { -1, 0, 1, 0 };
                int dCol[] = { 0, -1, 0, 1 };
                std::uniform_int_distribution<int> dir(0, 3);
                int dirIndex = dir(rng);
                int newRow = bestSquare.row + dRow[dirIndex];
                int newCol = bestSquare.col + dCol[dirIndex];
                if (newRow >= 0 && newRow < n && newCol >= 0 && newCol < n) {
                    bestSquare = court.getSquare(newRow, newCol);
                }
                else {
                    return Square{ {-1.0, -1.0}, 0.0, -1, -1 };
                }
            }
        }

        if (isServe) {
            double minBotDist = 1e9;
            Square nearSquare = bestSquare;
            for (const auto& sq : court.squares) {
                double botDist = std::hypot(sq.center.x - bot.pos.x, sq.center.y - bot.pos.y);
                if (botDist < minBotDist) {
                    minBotDist = botDist;
                    nearSquare = sq;
                }
            }

            lastServeTarget = nearSquare.center;
            hasLastTarget = true;
            trapMode = true; 
            return nearSquare;
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
        : agent(r_agent, l_agent, { 10.0, 0.0 }),
        bot(r_bot, l_bot, { 10.0, 10.0 }),
        court(20.0, 10.0, n),
        strategy(n) {
        rng.seed(std::random_device{}());
    }

    bool simulatePoint(bool serve = false) {
        Square targetSquare = strategy.chooseSquare(agent, bot, court, rng, serve);

        if (targetSquare.row == -1) {
            return false;
        }

        std::uniform_real_distribution<double> noise(-targetSquare.size / 2.0, targetSquare.size / 2.0);
        Position ball = { targetSquare.center.x + noise(rng), targetSquare.center.y + noise(rng) };

        if (court.isOut(ball)) {
            return false;
        }

        bot.moveTo(ball);
        if (!bot.canHit(ball)) {
            return true; 
        }

        std::uniform_real_distribution<double> xDist(0.0, court.width);
        std::uniform_real_distribution<double> yDist(0.0, court.height / 2.0);
        Position returnBall = { xDist(rng), yDist(rng) };

        if (court.isOut(returnBall)) {
            return true; 
        }

        agent.moveTo(returnBall);
        if (!agent.canHit(returnBall)) {
            return false;
        }

        return simulatePoint(false);
    }

    void playGame(bool firstServe = true) {
        agentPoints = 0;
        botPoints = 0;
        while (true) {
            bool serveNow = (agentPoints + botPoints == 0) && firstServe;
            bool agentWonPoint = simulatePoint(serveNow);

            if (agentWonPoint) agentPoints++;
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
    setlocale(LC_ALL, "Russian");
    const int simulations = 100;
    const int bestOfSets = 2;

    const int n = 10;

    const double r_robot = 2.0;
    const double l_robot = 3.0;

    std::ofstream results("results.csv");
    results << "r_agent;l_agent;n;agentWins;botWins;agentWinProbability\n";

    for (int n = 5; n <= 15; n += 5) {
        for (double r_agent = 1.0; r_agent <= 2.0; r_agent += 1.0) {
            for (double l_agent = 1.0; l_agent <= 3.0; l_agent += 1.0) {
                int agentWinCount = 0;
                int botWinCount = 0;

                for (int i = 0; i < simulations; i++) {
                    Match match(r_agent, l_agent, r_robot, l_robot, n);
                    match.playMatch(bestOfSets);

                    if (match.agentSets > match.botSets) agentWinCount++;
                    else botWinCount++;
                }

                double winProbability = static_cast<double>(agentWinCount) / simulations;
                results << std::fixed << std::setprecision(2)
                    << r_agent << ";" << l_agent << ";" << n << ";"
                    << agentWinCount << ";" << botWinCount << ";"
                    << winProbability << "\n";

                std::cout << "n=" << n << " r_agent=" << r_agent << " l_agent=" << l_agent
                    << " Agent wins: " << agentWinCount << "/" << simulations << "\n";
            }
        }
    }

    results.close();
    std::cout << "Симуляция завершена. Результаты в results.csv\n";

    return 0;
}
