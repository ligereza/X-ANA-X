namespace Xanax.Core;

public sealed record NeuralPrediction(
    string Relation,
    double Confidence,
    bool HasEvidence,
    int SupportingCases,
    bool GeneralizationEligible,
    double NoveltyDistance,
    bool WithinLearnedDomain);

/// <summary>
/// Small online neural classifier for LEARNING. It starts with no host knowledge;
/// weights are trained only from verified mission cases and can be rebuilt from
/// the persisted evidence store.
/// </summary>
public sealed class RelationNeuralModel
{
    public const int InputSize = 20;

    private readonly int hiddenSize;
    private readonly List<string> labels = new();
    private readonly Dictionary<string, int> labelCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> labelEvidence = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<double[]> trainingFeatures = new();
    public double NoveltyThreshold { get; }
    private readonly double[][] inputHidden;
    private readonly double[] hiddenBias;
    private double[][] hiddenOutput;
    private double[] outputBias;

    public RelationNeuralModel(int hiddenSize = 12, int seed = 20260910, double noveltyThreshold = 0.75)
    {
        if (hiddenSize < 2)
            throw new ArgumentOutOfRangeException(nameof(hiddenSize));
        if (noveltyThreshold <= 0 || double.IsNaN(noveltyThreshold) || double.IsInfinity(noveltyThreshold))
            throw new ArgumentOutOfRangeException(nameof(noveltyThreshold));
        this.hiddenSize = hiddenSize;
        NoveltyThreshold = noveltyThreshold;
        inputHidden = CreateMatrix(hiddenSize, InputSize);
        hiddenBias = new double[hiddenSize];
        hiddenOutput = Array.Empty<double[]>();
        outputBias = Array.Empty<double>();
        var random = new Random(seed);
        for (var row = 0; row < inputHidden.Length; row++)
        for (var column = 0; column < inputHidden[row].Length; column++)
            inputHidden[row][column] = (random.NextDouble() - 0.5d) * 0.2d;
    }

    public IReadOnlyList<string> Labels => labels;

    public void Train(MissionCase missionCase, int epochs = 32, double learningRate = 0.04)
    {
        if (!IsVerified(missionCase))
            return;
        if (epochs < 1 || learningRate <= 0 || double.IsNaN(learningRate) || double.IsInfinity(learningRate))
            throw new ArgumentOutOfRangeException(nameof(epochs));

        var labelIndex = EnsureLabel(missionCase.Relation);
        var evidenceKey = MissionEvidenceIdentity.For(missionCase);
        if (!labelEvidence[missionCase.Relation].Add(evidenceKey))
            return;
        labelCounts[missionCase.Relation] = labelEvidence[missionCase.Relation].Count;
        var input = Features(missionCase);
        trainingFeatures.Add(input);
        for (var epoch = 0; epoch < epochs; epoch++)
        {
            var hidden = ForwardHidden(input);
            var probabilities = Softmax(hidden);
            var outputDelta = probabilities.ToArray();
            outputDelta[labelIndex] -= 1d;
            var hiddenDelta = new double[hiddenSize];
            for (var hiddenIndex = 0; hiddenIndex < hiddenSize; hiddenIndex++)
            {
                var gradient = 0d;
                for (var outputIndex = 0; outputIndex < labels.Count; outputIndex++)
                    gradient += outputDelta[outputIndex] * hiddenOutput[outputIndex][hiddenIndex];
                hiddenDelta[hiddenIndex] = hidden[hiddenIndex] > 0d ? gradient : 0d;
            }

            for (var outputIndex = 0; outputIndex < labels.Count; outputIndex++)
            {
                for (var hiddenIndex = 0; hiddenIndex < hiddenSize; hiddenIndex++)
                    hiddenOutput[outputIndex][hiddenIndex] -= learningRate * outputDelta[outputIndex] * hidden[hiddenIndex];
                outputBias[outputIndex] -= learningRate * outputDelta[outputIndex];
            }
            for (var hiddenIndex = 0; hiddenIndex < hiddenSize; hiddenIndex++)
            {
                for (var inputIndex = 0; inputIndex < InputSize; inputIndex++)
                    inputHidden[hiddenIndex][inputIndex] -= learningRate * hiddenDelta[hiddenIndex] * input[inputIndex];
                hiddenBias[hiddenIndex] -= learningRate * hiddenDelta[hiddenIndex];
            }
        }
    }

    public NeuralPrediction Predict(MissionCase missionCase)
    {
        if (labels.Count == 0)
            return new("unknown", 0d, false, 0, false, double.PositiveInfinity, false);
        var input = Features(missionCase);
        var noveltyDistance = NearestDistance(input);
        var withinDomain = noveltyDistance <= NoveltyThreshold;
        var probabilities = Softmax(ForwardHidden(input));
        var best = 0;
        for (var index = 1; index < probabilities.Length; index++)
            if (probabilities[index] > probabilities[best]) best = index;
        var supportingCases = labelCounts.TryGetValue(labels[best], out var count) ? count : 0;
        return new(labels[best], probabilities[best], true, supportingCases,
            supportingCases >= 2 && withinDomain, noveltyDistance, withinDomain);
    }

    private int EnsureLabel(string relation)
    {
        var index = labels.FindIndex(label => string.Equals(label, relation, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            return index;
        labels.Add(relation);
        labelCounts[relation] = 0;
        labelEvidence[relation] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        hiddenOutput = hiddenOutput.Append(new double[hiddenSize]).ToArray();
        outputBias = outputBias.Append(0d).ToArray();
        return labels.Count - 1;
    }

    private double[] ForwardHidden(IReadOnlyList<double> input)
    {
        var hidden = new double[hiddenSize];
        for (var row = 0; row < hiddenSize; row++)
        {
            var value = hiddenBias[row];
            for (var column = 0; column < InputSize; column++)
                value += inputHidden[row][column] * input[column];
            hidden[row] = Math.Max(0d, value);
        }
        return hidden;
    }

    private double[] Softmax(IReadOnlyList<double> hidden)
    {
        var logits = new double[labels.Count];
        var maximum = double.NegativeInfinity;
        for (var output = 0; output < labels.Count; output++)
        {
            var value = outputBias[output];
            for (var row = 0; row < hiddenSize; row++)
                value += hiddenOutput[output][row] * hidden[row];
            logits[output] = value;
            maximum = Math.Max(maximum, value);
        }
        var sum = 0d;
        for (var output = 0; output < logits.Length; output++)
            sum += logits[output] = Math.Exp(logits[output] - maximum);
        for (var output = 0; output < logits.Length; output++)
            logits[output] /= Math.Max(sum, double.Epsilon);
        return logits;
    }

    private static double[] Features(MissionCase missionCase)
    {
        var values = new double[InputSize];
        for (var index = 0; index < LiveShowSignature.DefaultAxes.Count; index++)
        {
            var axis = LiveShowSignature.DefaultAxes[index];
            if (missionCase.NumericSignature.TryGetValue(axis, out var value))
                values[index] = Math.Tanh(value);
        }
        FillHashBuckets(values, 12, 4, missionCase.CanonicalIntent);
        FillHashBuckets(values, 16, 2, missionCase.SourceHost);
        FillHashBuckets(values, 18, 2, missionCase.TargetHost);
        return values;
    }

    private static void FillHashBuckets(double[] values, int offset, int count, string text)
    {
        var hash = 2166136261u;
        foreach (var character in text)
        {
            hash ^= character;
            hash *= 16777619u;
        }
        values[offset + (int)(hash % (uint)count)] = 1d;
    }

    private static double[][] CreateMatrix(int rows, int columns) =>
        Enumerable.Range(0, rows).Select(_ => new double[columns]).ToArray();

    private double NearestDistance(IReadOnlyList<double> input)
    {
        if (trainingFeatures.Count == 0)
            return double.PositiveInfinity;
        return trainingFeatures.Min(training => Math.Sqrt(training
            .Zip(input, (left, right) => (left - right) * (left - right))
            .Sum()));
    }

    private static bool IsVerified(MissionCase missionCase) =>
        missionCase.EvidenceLevel.Equals("replayed", StringComparison.OrdinalIgnoreCase) ||
        missionCase.EvidenceLevel.Equals("human_confirmed", StringComparison.OrdinalIgnoreCase);
}
