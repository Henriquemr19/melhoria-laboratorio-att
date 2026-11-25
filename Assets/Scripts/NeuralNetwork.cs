using System;

// Rede neural feedforward 3-4-2 com ativação por tangente hiperbólica (tanh)
public sealed class NeuralNetwork
{
    public const int InputCount = 3;
    public const int HiddenCount = 4;
    public const int OutputCount = 2;
    public const int WeightCount = InputCount * HiddenCount + HiddenCount + HiddenCount * OutputCount + OutputCount;

    private readonly float[] hidden = new float[HiddenCount];
    private readonly float[] output = new float[OutputCount];

    public void Forward(float[] input, float[] weights)
    {
        int index = 0;

        // Camada Oculta (3 entradas -> 4 neurônios com bias)
        for (int h = 0; h < HiddenCount; h++)
        {
            float sum = 0f;
            for (int i = 0; i < InputCount; i++)
                sum += input[i] * weights[index++];
            hidden[h] = (float)Math.Tanh(sum + weights[index++]);
        }

        // Camada de Saída (4 neurônios ocultos -> 2 saídas com bias)
        for (int o = 0; o < OutputCount; o++)
        {
            float sum = 0f;
            for (int h = 0; h < HiddenCount; h++)
                sum += hidden[h] * weights[index++];
            output[o] = (float)Math.Tanh(sum + weights[index++]);
        }
    }

    public float GetOutput(int index) => output[index];
}
