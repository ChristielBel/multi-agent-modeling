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
    set<int> targetPatents;
    set<int> currentPatents;
    set<int> missingPatents;
    int communicationRounds = 0;
    int completionStep = 0;

    Agent(int agentId) : id(agentId) {}

    void updateMissingPatents() {
        missingPatents.clear();
        for (int patentId : targetPatents) {
            if (currentPatents.find(patentId) == currentPatents.end()) {
                missingPatents.insert(patentId);
            }
        }
    }

    bool isComplete() const {
        return missingPatents.empty();
    }

    int findNeededPatent(const Agent& other) const {
        for (int patentId : missingPatents) {
            if (other.currentPatents.find(patentId) != other.currentPatents.end())
                return patentId;
        }
        return -1;
    }

    int findGiveablePatent(const Agent& other) const {
        for (int patentId : currentPatents) {
            if (other.missingPatents.find(patentId) != other.missingPatents.end()) {
                return patentId;
            }
        }

        if (!other.isComplete()) {
            for (int patentId : currentPatents) {
                if (targetPatents.find(patentId) == targetPatents.end() &&
                    other.missingPatents.find(patentId) != other.missingPatents.end()) {
                    return patentId;
                }
            }
        }

        return -1;
    }

    bool exchangeWith(Agent& other) {
        communicationRounds++;
        other.communicationRounds++;

        int needed = findNeededPatent(other);
        if (needed == -1) return false;

        int giveToOther = findGiveablePatent(other);

        currentPatents.insert(needed);
        updateMissingPatents();

        if (giveToOther != -1) {
            other.currentPatents.insert(giveToOther);
            other.updateMissingPatents();
        }

        return true;
    }
};

class Simulation {
private:
    vector<Agent> agentList;
    int agentCount;
    int patentsPerAgentTarget;
    const int MAX_SIMULATION_STEPS = 10000;
    mt19937 rng;

public:
    Simulation(int numAgents, int patentsPerAgent)
        : agentCount(numAgents), patentsPerAgentTarget(patentsPerAgent) {
        rng.seed(static_cast<unsigned>(time(nullptr)));
    }

    void initialize() {
        createAgents();
        assignTargetPatents();
        distributeInitialPatents();
    }

private:
    void createAgents() {
        for (int i = 0; i < agentCount; i++) {
            agentList.emplace_back(i);
        }
    }

    void assignTargetPatents() {
        int globalPatentId = 0;
        for (auto& agent : agentList) {
            for (int j = 0; j < patentsPerAgentTarget; j++) {
                agent.targetPatents.insert(globalPatentId++);
            }
        }
    }

    void distributeInitialPatents() {
        vector<int> allPatents;
        for (auto& agent : agentList) {
            allPatents.insert(allPatents.end(), agent.targetPatents.begin(), agent.targetPatents.end());
        }

        shuffle(allPatents.begin(), allPatents.end(), rng);

        for (int i = 0; i < allPatents.size(); i++) {
            agentList[i % agentCount].currentPatents.insert(allPatents[i]);
        }

        for (auto& agent : agentList) {
            agent.updateMissingPatents();
        }
    }

public:
    void run() {
        bool allAgentsComplete = false;
        int iteration = 0;

        while (!allAgentsComplete && iteration < MAX_SIMULATION_STEPS) {
            iteration++;
            allAgentsComplete = simulateIteration(iteration);
        }

        printResults(iteration);
    }

private:
    bool simulateIteration(int iteration) {
        bool allComplete = true;
        vector<int> activeAgents;

        for (int i = 0; i < agentCount; i++) {
            if (!agentList[i].isComplete()) activeAgents.push_back(i);
        }

        shuffle(activeAgents.begin(), activeAgents.end(), rng);

        for (int i : activeAgents) {
            if (agentList[i].isComplete()) continue;

            allComplete = false;

            uniform_int_distribution<int> dist(0, agentCount - 1);
            int j = dist(rng);
            if (j == i) continue;

            if (agentList[i].exchangeWith(agentList[j]) && agentList[i].completionStep == 0) {
                agentList[i].completionStep = iteration;
            }
        }

        return allComplete;
    }

    void printResults(int iteration) const {
        cout << "=== Результаты моделирования ===\n";
        for (const auto& agent : agentList) {
            cout << "Агент " << agent.id
                << " | Целевой набор: " << agent.targetPatents.size()
                << " | Итерации: " << agent.completionStep
                << " | Раунды коммуникаций: " << agent.communicationRounds << endl;
        }

        if (iteration >= MAX_SIMULATION_STEPS)
            cout << "\nВнимание: Достигнут лимит итераций.\n";
        else
            cout << "\nСимуляция завершена за " << iteration << " итераций.\n";
    }
};

int main() {
    setlocale(LC_ALL, "Russian");

    Simulation sim(20, 5);
    sim.initialize();
    sim.run();

    return 0;
}
