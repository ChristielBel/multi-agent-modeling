import kotlin.random.Random

data class Client(
    val id: Int,
    val arrivalTime: Double,
    val complexity: Int
)

class Agent(val id: Int) {
    private var busyUntil: Double = 0.0
    private val queue: MutableList<Client> = mutableListOf()
    var totalTime: Int = 0
        private set
    var servedClients: Int = 0
        private set

    fun currentLoad(currentTime: Double): Double {
        val load = (busyUntil - currentTime).coerceAtLeast(0.0)
        val queueLoad = queue.sumOf { it.complexity }
        return load + queueLoad
    }

    fun addClient(client: Client) {
        val startTime = maxOf(busyUntil, client.arrivalTime)
        val finishTime = startTime + client.complexity
        busyUntil = finishTime
        queue.add(client)
        totalTime += client.complexity
        servedClients++
    }
}

fun modeling (
    n : Int = 3,
    m : Int = 10,
    a : Double = 1.0,
    b : Double = 5.0
): List<Triple<Int, Int, Int>> {
    val random = Random.Default
    val agents = List(n) { Agent(it) }

    var time = 0.0
    var clientsServed = 0
    var clientId = 1

    while (clientsServed < m) {
        val interval = random.nextDouble(a, b)
        time += interval

        val complexity = random.nextInt(1, 11)
        val client = Client(clientId, time, complexity)

        val chosen = agents.minWith(compareBy({ it.currentLoad(time) }, { it.id }))
        chosen.addClient(client)

        clientsServed++
        clientId++
    }

    val report = agents.map { Triple(it.id, it.servedClients, it.totalTime) }
    return report.sortedWith(compareBy({ -it.second }, { it.third }))
}

fun main() {
    val report = modeling(n = 3, m = 15, a = 1.0, b = 3.0)
    println("Отчет об агентах:")
    report.forEach {
        println("Агент ${it.first}: обслужил ${it.second} клиентов, суммарное время ${it.third}")
    }
}
