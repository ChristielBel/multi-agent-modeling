#include <iostream>
#include <vector>
#include <set>
#include <algorithm>
#include <random>
#include <ctime>
#include <queue>

using namespace std;

class Agent {
public:
    int id;
    set<int> targetSetOfPatents;
    set<int> existingSetOfPatents;
    set<int> neededPatents;
    int rounds = 0;
    int iterations = 0;

    Agent(int agentId) : id(agentId) {}

    void updateNeededPatents() {
        neededPatents.clear();
        for (int t : targetSetOfPatents) {
            if (existingSetOfPatents.find(t) == existingSetOfPatents.end()) {
                neededPatents.insert(t);
            }
        }
    }

    bool isComplete() const {
        return neededPatents.empty();
    }

    int findNeededPatent(const Agent& other) const {
        for (int t : neededPatents) {
            if (other.existingSetOfPatents.find(t) != other.existingSetOfPatents.end())
                return t;
        }
        return -1;
    }

    int findGiveablePatent(const Agent& other) const {
        if (other.isComplete()) return -1;
        for (int t : existingSetOfPatents) {
            if (targetSetOfPatents.find(t) == targetSetOfPatents.end() &&
                other.neededPatents.find(t) != other.neededPatents.end()) {
                return t;
            }
        }
        return -1;
    }

    bool exchangeWith(Agent& other) {
        rounds++;
        other.rounds++;

        int needed = findNeededPatent(other);
        if (needed == -1) return false;

        int giveToOther = findGiveablePatent(other);

        existingSetOfPatents.insert(needed);
        updateNeededPatents();

        if (giveToOther != -1) {
            other.existingSetOfPatents.insert(giveToOther);
            other.updateNeededPatents();
        }

        return true;
    }
};

class Simulation {
private:
    vector<Agent> agents;
    int n;
    int patentsPerTarget;
    const int MAX_ITERATIONS = 10000;
    mt19937 rng;

public:
    Simulation(int numAgents, int patentsPerAgent)
        : n(numAgents), patentsPerTarget(patentsPerAgent) {
        rng.seed(static_cast<unsigned>(time(nullptr)));
    }

    void initialize() {
        createAgents();
        assignTargets();
        assignInitialPatents();
    }

private:
    void createAgents() {
        for (int i = 0; i < n; i++) {
            agents.emplace_back(i);
        }
    }

    void assignTargets() {
        int globalPatentId = 0;
        for (auto& agent : agents) {
            for (int j = 0; j < patentsPerTarget; j++) {
                agent.targetSetOfPatents.insert(globalPatentId++);
            }
        }
    }

    void assignInitialPatents() {
        vector<int> allPatents;
        for (auto& a : agents) {
            allPatents.insert(allPatents.end(), a.targetSetOfPatents.begin(), a.targetSetOfPatents.end());
        }

        shuffle(allPatents.begin(), allPatents.end(), rng);

        for (int i = 0; i < allPatents.size(); i++) {
            agents[i % n].existingSetOfPatents.insert(allPatents[i]);
        }

        for (auto& a : agents) {
            a.updateNeededPatents();
        }
    }

public:
    void run() {
        bool finished = false;
        int iteration = 0;

        while (!finished && iteration < MAX_ITERATIONS) {
            iteration++;
            finished = simulateStep(iteration);
        }

        printResults(iteration);
    }

private:
    bool simulateStep(int iteration) {
        bool finished = true;
        vector<int> activeAgents;

        for (int i = 0; i < n; i++) {
            if (!agents[i].isComplete()) activeAgents.push_back(i);
        }

        shuffle(activeAgents.begin(), activeAgents.end(), rng);

        for (int i : activeAgents) {
            if (agents[i].isComplete()) continue;

            finished = false;

            uniform_int_distribution<int> dist(0, n - 1);
            int j = dist(rng);
            if (j == i) continue;

            if (agents[i].exchangeWith(agents[j]) && agents[i].iterations == 0) {
                agents[i].iterations = iteration;
            }
        }

        return finished;
    }

    void printResults(int iterations) const {
        cout << "=== Результаты моделирования ===\n";
        for (const auto& a : agents) {
            cout << "Агент " << a.id
                << " | Целевой набор: " << a.targetSetOfPatents.size()
                << " | Итерации: " << a.iterations
                << " | Раунды коммуникаций: " << a.rounds << endl;
        }

        if (iterations >= MAX_ITERATIONS)
            cout << "\nВнимание: Достигнут лимит итераций.\n";
        else
            cout << "\nСимуляция завершена за " << iterations << " итераций.\n";
    }
};

int main() {
    setlocale(LC_ALL, "Russian");

    Simulation sim(4, 3);
    sim.initialize();
    sim.run();

    return 0;
}
