using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class EvolvingShip : MonoBehaviour
{
    [Header("Sensores Frontais")]
    [SerializeField] private float sensorDistance = 8.5f;
    [SerializeField] private float sensorAngle = 35f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Movimentação")]
    [SerializeField] private float maxSpeed = 7.5f;
    [SerializeField] private float turnSpeed = 180f;

    private readonly float[] inputs = new float[NeuralNetwork.InputCount];
    private readonly NeuralNetwork brain = new NeuralNetwork();
    private Rigidbody body;
    private EvolutionManager manager;
    private Genome genome;
    private Transform goal;
    private float initialDistance;
    private Vector3 lastPosition;
    private bool active;

    public float Fitness { get; private set; }
    public Genome Genome => genome;
    public bool IsActive => active;
    public bool ReachedGoal { get; private set; }
    public bool Collided { get; private set; }
    public float EpisodeTime { get; private set; }
    public float RealDistance { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (obstacleMask.value == 0)
        {
            int obsLayer = LayerMask.NameToLayer("Obstacle");
            if (obsLayer >= 0)
                obstacleMask = 1 << obsLayer;
        }
    }

    public void Begin(EvolutionManager owner, Genome newGenome, Transform newGoal, Vector3 position, Quaternion rotation)
    {
        manager = owner;
        genome = newGenome;
        goal = newGoal;

        transform.SetPositionAndRotation(position, rotation);
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        initialDistance = Vector3.Distance(transform.position, goal.position);
        lastPosition = position;

        Fitness = 0f;
        ReachedGoal = false;
        Collided = false;
        EpisodeTime = 0f;
        RealDistance = 0f;
        active = true;
        gameObject.SetActive(true);
    }

    private void FixedUpdate()
    {
        if (!active) return;

        // Soma os deslocamentos frame a frame para registrar a distância real percorrida
        RealDistance += Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;
        EpisodeTime += Time.fixedDeltaTime;

        ReadSensors();
        brain.Forward(inputs, genome.Weights);

        // Ações de esterçamento e aceleração geradas pela rede neural (saídas normalizadas)
        float turn = brain.GetOutput(0);
        float throttle = Mathf.Clamp01((brain.GetOutput(1) + 1f) * 0.5f);

        Quaternion delta = Quaternion.Euler(0f, turn * turnSpeed * Time.fixedDeltaTime, 0f);
        body.MoveRotation(body.rotation * delta);
        body.linearVelocity = transform.forward * (throttle * maxSpeed);

        // Cálculo contínuo do progresso em direção ao alvo
        float currentDist = Vector3.Distance(transform.position, goal.position);
        float progress = initialDistance - currentDist;
        Fitness = progress * manager.ProgressWeight;
    }

    private void ReadSensors()
    {
        inputs[0] = ReadSensor(transform.forward);
        inputs[1] = ReadSensor(Quaternion.AngleAxis(-sensorAngle, Vector3.up) * transform.forward);
        inputs[2] = ReadSensor(Quaternion.AngleAxis(sensorAngle, Vector3.up) * transform.forward);
    }

    private float ReadSensor(Vector3 direction)
    {
        Vector3 origin = transform.position + Vector3.up * 0.25f;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, sensorDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            return hit.distance / sensorDistance;

        return 1f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (active && ((1 << collision.gameObject.layer) & obstacleMask.value) != 0)
        {
            Collided = true;
            Finish(manager.CollisionPenalty);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (active && other.transform == goal)
        {
            ReachedGoal = true;
            // Bônus por alcançar o alvo com acréscimo proporcional à rapidez
            float timeBonus = Mathf.Max(0f, (manager.EpisodeSeconds - EpisodeTime) * 2f);
            Finish(manager.GoalBonus + timeBonus);
        }
    }

    public void Timeout()
    {
        if (active)
            Finish(-manager.TimePenalty);
    }

    private void Finish(float bonusOrPenalty)
    {
        Fitness += bonusOrPenalty;
        active = false;
        body.linearVelocity = Vector3.zero;
        manager.ReportFinished(this);
    }

    // Retorna a eficiência da nave (distância de referência / distância real). Retorna 0 para episódios sem sucesso.
    public float GetEfficiency(float referenceDistance)
    {
        if (!ReachedGoal || RealDistance <= 0.001f || referenceDistance <= 0f)
            return 0f;

        return referenceDistance / RealDistance;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + Vector3.up * 0.25f;
        Gizmos.DrawRay(origin, transform.forward * sensorDistance);
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(-sensorAngle, Vector3.up) * transform.forward * sensorDistance);
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(sensorAngle, Vector3.up) * transform.forward * sensorDistance);
    }
}
