using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class EvolutionSnapshot
{
    public int version = 1;
    public int seed;
    public int generation;
    public float bestFitness;
    public float[] weights;
    public int populationSize;
    public float mutationRate;
    public float mutationStrength;
    public string savedAt;
}

public sealed class EvolutionManager : MonoBehaviour
{
    private const string SaveKey = "neuroevolution.best.v1";

    [Header("Objetos da Cena")]
    [SerializeField] private EvolvingShip shipPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform goal;
    [SerializeField] private AStarPathfinder pathfinder;
    [SerializeField] private GameObject[] scenarios;
    [SerializeField] private Vector3[] scenarioGoalPositions;

    [Header("Parâmetros do Algoritmo Genético")]
    [SerializeField, Min(4)] private int populationSize = 30;
    [SerializeField, Min(1)] private int eliteCount = 2;
    [SerializeField, Range(0f, 1f)] private float mutationRate = 0.08f;
    [SerializeField, Min(0f)] private float mutationStrength = 0.25f;
    [SerializeField, Min(1f)] private float episodeSeconds = 15f;
    [SerializeField] private int randomSeed = 78451;
    [SerializeField] private bool validationMode;

    [Header("Pesos da Função de Fitness")]
    [SerializeField] private float progressWeight = 10f;
    [SerializeField] private float goalBonus = 100f;
    [SerializeField] private float collisionPenalty = -50f;
    [SerializeField] private float timePenalty = 1f;

    private readonly List<EvolvingShip> ships = new List<EvolvingShip>();
    private readonly List<Genome> population = new List<Genome>();
    private System.Random random;
    private Genome bestGenome;
    private float elapsed;
    private int finishedCount;
    private int currentScenario;
    private float referenceDistance;
    private string lastStatus = "";

    public int Generation { get; private set; }
    public float BestFitness { get; private set; }
    public float AverageFitness { get; private set; }
    public float BestEfficiency { get; private set; }
    public float EpisodeSeconds => episodeSeconds;
    public float ProgressWeight => progressWeight;
    public float GoalBonus => goalBonus;
    public float CollisionPenalty => collisionPenalty;
    public float TimePenalty => timePenalty;

    private void Awake()
    {
        // Garante que naves não colidam entre si
        int shipLayer = LayerMask.NameToLayer("Ship");
        if (shipLayer >= 0)
        {
            Physics.IgnoreLayerCollision(shipLayer, shipLayer, true);
        }
    }

    private void Start()
    {
        if (shipPrefab == null || spawnPoint == null || goal == null)
        {
            Debug.LogError("Configuração incompleta: informe Ship Prefab, Spawn Point e Goal no Inspector.", this);
            enabled = false;
            return;
        }

        ActivateScenario(0);
        StartEvolution();
    }

    private void Update()
    {
        // Teclas de atalho para persistência do melhor genoma
        if (Input.GetKeyDown(KeyCode.S)) SaveBest();
        if (Input.GetKeyDown(KeyCode.L)) LoadBest();
        if (Input.GetKeyDown(KeyCode.D)) DeleteSave();
    }

    private void FixedUpdate()
    {
        elapsed += Time.fixedDeltaTime;
        if (elapsed >= episodeSeconds)
        {
            EvolvingShip[] activeShips = ships.Where(ship => ship.IsActive).ToArray();
            foreach (EvolvingShip ship in activeShips)
                ship.Timeout();
        }
    }

    // Inicializa a população usando a seed especificada
    public void StartEvolution()
    {
        validationMode = false;
        Generation = 0;
        BestFitness = float.MinValue;
        BestEfficiency = 0f;
        population.Clear();

        random = new System.Random(randomSeed);
        for (int i = 0; i < populationSize; i++)
            population.Add(new Genome(NeuralNetwork.WeightCount, random));

        BeginGeneration();
    }

    // Alterna entre o cenário de treino e os cenários de validação
    public void ActivateScenario(int index)
    {
        if (scenarios == null || scenarios.Length == 0) return;

        currentScenario = Mathf.Clamp(index, 0, scenarios.Length - 1);
        for (int i = 0; i < scenarios.Length; i++)
        {
            if (scenarios[i] != null)
                scenarios[i].SetActive(i == currentScenario);
        }

        if (scenarioGoalPositions != null && currentScenario < scenarioGoalPositions.Length)
            goal.position = scenarioGoalPositions[currentScenario];

        Physics.SyncTransforms();

        if (pathfinder != null)
        {
            referenceDistance = pathfinder.CalculatePath(spawnPoint.position, goal.position);
            string name = currentScenario == 0 ? "Treino" : $"Validação {currentScenario}";
            Debug.Log($"[Cenário {name}] Distância de referência A*: {referenceDistance:F2} m");
        }
    }

    public void ReportFinished(EvolvingShip ship)
    {
        finishedCount++;

        if (validationMode)
        {
            float eff = ship.GetEfficiency(referenceDistance);
            string effText = ship.ReachedGoal ? $"{eff:F3}" : "Inválida (não chegou)";
            string name = currentScenario == 0 ? "Treino" : $"Validação {currentScenario}";
            lastStatus = $"[{name}] Chegou: {ship.ReachedGoal} | Colidiu: {ship.Collided} | Tempo: {ship.EpisodeTime:F2}s | Dist. Real: {ship.RealDistance:F2}m | Dist. A*: {referenceDistance:F2}m | Efic: {effText}";
            Debug.Log(lastStatus);
            return;
        }

        if (finishedCount >= ships.Count)
        {
            Evolve();
        }
    }

    private void BeginGeneration()
    {
        elapsed = 0f;
        finishedCount = 0;

        int activeCount = validationMode ? 1 : population.Count;
        while (ships.Count < activeCount)
            ships.Add(Instantiate(shipPrefab, spawnPoint.position, spawnPoint.rotation, transform));

        for (int i = 0; i < ships.Count; i++)
        {
            if (i < activeCount)
            {
                Genome g = validationMode ? bestGenome : population[i];
                ships[i].Begin(this, g, goal, spawnPoint.position, spawnPoint.rotation);
            }
            else
            {
                ships[i].gameObject.SetActive(false);
            }
        }
    }

    private void Evolve()
    {
        float bestEffThisGen = 0f;
        foreach (EvolvingShip ship in ships)
        {
            if (!ship.gameObject.activeSelf) continue;

            ship.Genome.Fitness = ship.Fitness;
            float eff = ship.GetEfficiency(referenceDistance);
            if (eff > bestEffThisGen)
                bestEffThisGen = eff;
        }

        // Ordena a população em ordem decrescente de fitness
        population.Sort((a, b) => b.Fitness.CompareTo(a.Fitness));
        BestFitness = population[0].Fitness;
        AverageFitness = population.Average(genome => genome.Fitness);
        BestEfficiency = bestEffThisGen;
        bestGenome = population[0].Clone();

        Debug.Log($"Geração {Generation} -> Melhor Fitness: {BestFitness:F2} | Média: {AverageFitness:F2} | Melhor Eficiência: {BestEfficiency:F3}");

        // Elitismo: preserva os melhores genomas
        var nextPopulation = new List<Genome>(populationSize);
        int protectedCount = Mathf.Min(eliteCount, population.Count);
        for (int i = 0; i < protectedCount; i++)
            nextPopulation.Add(population[i].Clone());

        // Crossover e mutação para preencher a nova geração
        while (nextPopulation.Count < populationSize)
        {
            Genome parentA = TournamentPick();
            Genome parentB = TournamentPick();
            Genome child = Genome.Crossover(parentA, parentB, random);
            child.Mutate(random, mutationRate, mutationStrength);
            nextPopulation.Add(child);
        }

        population.Clear();
        population.AddRange(nextPopulation);
        Generation++;
        BeginGeneration();
    }

    private Genome TournamentPick()
    {
        const int tournamentSize = 3;
        Genome best = population[random.Next(population.Count)];
        for (int i = 1; i < tournamentSize; i++)
        {
            Genome contender = population[random.Next(population.Count)];
            if (contender.Fitness > best.Fitness)
                best = contender;
        }
        return best;
    }

    // Parte C: Grava o melhor genoma no PlayerPrefs
    public void SaveBest()
    {
        Genome toSave = bestGenome;
        if (toSave == null && population.Count > 0)
            toSave = population[0].Clone();

        if (toSave == null)
        {
            lastStatus = "Nenhum genoma disponível para salvar.";
            Debug.LogWarning(lastStatus);
            return;
        }

        float[] weightsCopy = new float[toSave.Weights.Length];
        Array.Copy(toSave.Weights, weightsCopy, toSave.Weights.Length);

        EvolutionSnapshot snapshot = new EvolutionSnapshot
        {
            version = 1,
            seed = randomSeed,
            generation = Generation,
            bestFitness = toSave.Fitness,
            weights = weightsCopy,
            populationSize = populationSize,
            mutationRate = mutationRate,
            mutationStrength = mutationStrength,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        string json = JsonUtility.ToJson(snapshot);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();

        lastStatus = $"Genoma salvo com sucesso! (Geração {snapshot.generation}, Fitness {snapshot.bestFitness:F2})";
        Debug.Log(lastStatus);
    }

    // Parte C: Carrega e executa o genoma salvo sem evoluir novamente
    public void LoadBest()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            lastStatus = "Nenhum save encontrado em PlayerPrefs.";
            Debug.LogWarning(lastStatus);
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey, "");
        EvolutionSnapshot snapshot = JsonUtility.FromJson<EvolutionSnapshot>(json);

        if (snapshot == null || snapshot.weights == null || snapshot.weights.Length != NeuralNetwork.WeightCount)
        {
            lastStatus = "Save recusado: quantidade de pesos incompatível com a rede neural.";
            Debug.LogWarning(lastStatus);
            return;
        }

        float[] weightsCopy = new float[snapshot.weights.Length];
        Array.Copy(snapshot.weights, weightsCopy, snapshot.weights.Length);

        bestGenome = new Genome(NeuralNetwork.WeightCount, new System.Random(snapshot.seed))
        {
            Weights = weightsCopy,
            Fitness = snapshot.bestFitness
        };

        Generation = snapshot.generation;
        BestFitness = snapshot.bestFitness;
        validationMode = true;
        BeginGeneration();

        lastStatus = $"Save carregado! Geração: {snapshot.generation} | Seed: {snapshot.seed} | Fitness: {snapshot.bestFitness:F2}";
        Debug.Log(lastStatus);
    }

    // Parte C: Remove a chave do save
    public void DeleteSave()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        lastStatus = "Save removido com sucesso de PlayerPrefs.";
        Debug.Log(lastStatus);
    }

    public void RunValidation()
    {
        if (bestGenome == null)
        {
            lastStatus = "Aguarde a evolução encontrar um genoma ou carregue um save.";
            Debug.LogWarning(lastStatus);
            return;
        }

        validationMode = true;
        BeginGeneration();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(12, 12, 380, 270), GUI.skin.box);

        string modeName = validationMode ? "Validação (Melhor Genoma)" : "Treino (Evolução Ativa)";
        string scnName = currentScenario == 0 ? "Treino" : $"Validação {currentScenario}";
        GUILayout.Label($"<b>Modo:</b> {modeName} | <b>Cenário:</b> {scnName}");
        GUILayout.Label($"<b>Geração:</b> {Generation} | <b>Melhor Fitness:</b> {BestFitness:F1} | <b>Ref A*:</b> {referenceDistance:F2}m");

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Salvar Melhor (S)")) SaveBest();
        if (GUILayout.Button("Carregar Melhor (L)")) LoadBest();
        if (GUILayout.Button("Apagar Save (D)")) DeleteSave();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Executar Validação")) RunValidation();
        if (GUILayout.Button("Reiniciar Treino")) StartEvolution();
        GUILayout.EndHorizontal();

        if (scenarios != null && scenarios.Length > 0)
        {
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < scenarios.Length; i++)
            {
                string btnLabel = i == 0 ? "Treino" : $"Validação {i}";
                if (GUILayout.Button(btnLabel))
                {
                    ActivateScenario(i);
                    if (validationMode)
                        BeginGeneration();
                }
            }
            GUILayout.EndHorizontal();
        }

        if (!string.IsNullOrEmpty(lastStatus))
        {
            GUILayout.Space(6);
            GUILayout.Label($"<color=yellow>{lastStatus}</color>");
        }

        GUILayout.EndArea();
    }
}
