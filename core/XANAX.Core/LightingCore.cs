namespace Xanax.Core;

public sealed record DmxChannel(
    string Attribute,
    int Address,
    int ResolutionBits,
    double PhysicalFrom,
    double PhysicalTo,
    bool Virtual = false,
    string? ModeMaster = null)
{
    public IReadOnlyList<DmxFunction> Functions { get; init; } = Array.Empty<DmxFunction>();
    public int DmxBreak { get; init; } = 1;
}

public sealed record DmxValue(int Address, int ResolutionBits, long RawValue, double PhysicalValue)
{
    public int DmxBreak { get; init; } = 1;
}

public sealed class FixturePersonality
{
    public FixturePersonality(string name, string mode, IEnumerable<DmxChannel> channels)
    {
        Name = name;
        Mode = mode;
        Channels = channels.ToArray();
        if (Channels.Count == 0)
            throw new ArgumentException("A personality must contain at least one channel.", nameof(channels));
    }

    public string Name { get; }
    public string Mode { get; }
    public IReadOnlyList<DmxChannel> Channels { get; }

    public IReadOnlyList<DmxValue> Encode(IReadOnlyDictionary<string, double> physicalValues)
    {
        var encoded = new List<DmxValue>();
        foreach (var channel in Channels)
        {
            if (channel.Virtual)
                continue;

            if (physicalValues.TryGetValue(channel.Attribute, out var physical))
            {
                encoded.Add(Encode(channel, physical, channel.PhysicalFrom, channel.PhysicalTo, 0, MaximumRaw(channel.ResolutionBits)));
                continue;
            }

            var function = channel.Functions.FirstOrDefault(f => physicalValues.ContainsKey(f.Attribute));
            if (function is not null)
            {
                physical = physicalValues[function.Attribute];
                encoded.Add(Encode(channel, physical, function.PhysicalFrom, function.PhysicalTo, function.DmxFrom, function.DmxTo));
            }
        }
        return encoded;
    }

    public MultiBreakDmxFrame EncodeMultiBreak(IReadOnlyDictionary<string, double> physicalValues) =>
        MultiBreakDmxFrameEncoder.Encode(Encode(physicalValues));

    public double Decode(DmxValue value)
    {
        var channel = FindChannel(value) ?? throw new KeyNotFoundException(
            $"No channel matches address {value.Address}, resolution {value.ResolutionBits}, break {value.DmxBreak}.");
        var function = channel.Functions.FirstOrDefault(f => value.RawValue >= f.DmxFrom && value.RawValue <= f.DmxTo);
        return function is null
            ? DecodePhysical(value.RawValue, 0, MaximumRaw(channel.ResolutionBits), channel.PhysicalFrom, channel.PhysicalTo)
            : DecodePhysical(value.RawValue, function.DmxFrom, function.DmxTo, function.PhysicalFrom, function.PhysicalTo);
    }

    public IReadOnlyDictionary<string, double> DecodeAttributes(IEnumerable<DmxValue> values)
    {
        var decoded = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var channel = FindChannel(value);
            if (channel is null || channel.Virtual)
                continue;

            var function = channel.Functions.FirstOrDefault(f => value.RawValue >= f.DmxFrom && value.RawValue <= f.DmxTo);
            if (function is null)
                decoded[channel.Attribute] = Decode(value);
            else
                decoded[function.Attribute] = DecodePhysical(value.RawValue, function.DmxFrom, function.DmxTo, function.PhysicalFrom, function.PhysicalTo);
        }
        return decoded;
    }

    private DmxChannel? FindChannel(DmxValue value)
    {
        var candidates = Channels
            .Where(channel => channel.Address == value.Address && channel.ResolutionBits == value.ResolutionBits)
            .ToArray();
        return candidates.FirstOrDefault(channel => channel.DmxBreak == value.DmxBreak) ??
            (candidates.Length == 1 ? candidates[0] : null);
    }

    private static double Normalize(double value, double from, double to)
    {
        if (Math.Abs(to - from) < double.Epsilon)
            return 0d;
        return Math.Clamp((value - from) / (to - from), 0d, 1d);
    }

    private static long MaximumRaw(int resolutionBits)
    {
        if (resolutionBits is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(resolutionBits), "XANAX supports DMX resolutions from 1 to 31 bits.");
        return (1L << resolutionBits) - 1L;
    }

    private static DmxValue Encode(
        DmxChannel channel,
        double physical,
        double physicalFrom,
        double physicalTo,
        double rawFrom,
        double rawTo)
    {
        var normalized = Normalize(physical, physicalFrom, physicalTo);
        var raw = (long)Math.Round(rawFrom + normalized * (rawTo - rawFrom), MidpointRounding.AwayFromZero);
        return new DmxValue(channel.Address, channel.ResolutionBits, raw, physical)
        {
            DmxBreak = channel.DmxBreak
        };
    }

    private static double DecodePhysical(long raw, double rawFrom, double rawTo, double physicalFrom, double physicalTo)
    {
        if (Math.Abs(rawTo - rawFrom) < double.Epsilon)
            return physicalFrom;
        var normalized = Math.Clamp((raw - rawFrom) / (rawTo - rawFrom), 0d, 1d);
        return physicalFrom + normalized * (physicalTo - physicalFrom);
    }
}

public sealed record ControlParameter(string Id, double Minimum, double Maximum)
{
    public double Clamp(double value) => Math.Clamp(value, Minimum, Maximum);
}

public sealed class BehaviorModel
{
    private readonly Func<IReadOnlyList<double>, double[]> evaluator;

    public BehaviorModel(
        string name,
        IEnumerable<ControlParameter> parameters,
        IEnumerable<string> outputs,
        Func<IReadOnlyList<double>, double[]> evaluator)
    {
        Name = name;
        Parameters = parameters.ToArray();
        Outputs = outputs.ToArray();
        this.evaluator = evaluator;
        if (Parameters.Count == 0 || Outputs.Count == 0)
            throw new ArgumentException("A behavior model needs parameters and outputs.");
    }

    public string Name { get; }
    public IReadOnlyList<ControlParameter> Parameters { get; }
    public IReadOnlyList<string> Outputs { get; }

    public double[] Evaluate(IReadOnlyList<double> parameters)
    {
        if (parameters.Count != Parameters.Count)
            throw new ArgumentException("Parameter count does not match the behavior model.");
        return evaluator(parameters);
    }
}

public sealed record JacobianBridgeResult(
    bool Solved,
    double[] Delta,
    double[] PredictedOutputDelta,
    double ResidualNorm,
    string Reason)
{
    public int Iterations { get; init; }
}

public sealed record MissionBridgeResult(
    bool Solved,
    double[] CanonicalOutputDelta,
    JacobianBridgeResult TargetResolution,
    string Reason);

/// <summary>
/// Defines the common behavioral space. Source and target may expose different
/// control/output names and different output counts; only this projection must
/// have the same canonical dimension on both sides.
/// </summary>
public sealed record CanonicalOutputMapping(
    IReadOnlyList<string> CanonicalNames,
    Func<IReadOnlyList<double>, double[]> SourceProjection,
    Func<IReadOnlyList<double>, double[]> TargetProjection)
{
    public void Validate()
    {
        if (CanonicalNames.Count == 0 ||
            CanonicalNames.Count != CanonicalNames.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new ArgumentException("Canonical output names must be non-empty and unique.");
    }
}

public static class MissionBridge
{
    public static MissionBridgeResult Translate(
        BehaviorModel source,
        IReadOnlyList<double> sourceParameters,
        IReadOnlyList<double> sourceDelta,
        BehaviorModel target,
        IReadOnlyList<double> targetParameters)
    {
        if (source.Outputs.Count != target.Outputs.Count ||
            source.Outputs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != source.Outputs.Count ||
            target.Outputs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != target.Outputs.Count ||
            !source.Outputs.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(target.Outputs))
            throw new ArgumentException("Source and target must expose the same canonical output names.");
        if (sourceDelta.Count != source.Parameters.Count)
            throw new ArgumentException("Source delta does not match the source model.");

        var current = source.Evaluate(sourceParameters);
        var nextParameters = source.Parameters
            .Select((parameter, index) => parameter.Clamp(sourceParameters[index] + sourceDelta[index]))
            .ToArray();
        var next = source.Evaluate(nextParameters);
        var canonicalDelta = next.Zip(current, (after, before) => after - before).ToArray();
        var sourceDeltaByOutput = source.Outputs
            .Select((output, index) => (output, delta: canonicalDelta[index]))
            .ToDictionary(item => item.output, item => item.delta, StringComparer.OrdinalIgnoreCase);
        var targetDelta = target.Outputs
            .Select(output => sourceDeltaByOutput[output])
            .ToArray();
        var targetResolution = JacobianBridge.Solve(target, targetParameters, targetDelta);
        return new(
            targetResolution.Solved,
            canonicalDelta,
            targetResolution,
            targetResolution.Solved ? "source_to_target_mission_translation" : targetResolution.Reason);
    }

    public static MissionBridgeResult Translate(
        BehaviorModel source,
        IReadOnlyList<double> sourceParameters,
        IReadOnlyList<double> sourceDelta,
        BehaviorModel target,
        IReadOnlyList<double> targetParameters,
        CanonicalOutputMapping mapping,
        double step = 1e-5,
        double damping = 1e-6,
        int maxIterations = 32,
        double tolerance = 1e-6)
    {
        mapping.Validate();
        if (sourceDelta.Count != source.Parameters.Count)
            throw new ArgumentException("Source delta does not match the source model.");

        var sourceCurrent = mapping.SourceProjection(source.Evaluate(sourceParameters));
        var sourceNextParameters = source.Parameters
            .Select((parameter, index) => parameter.Clamp(sourceParameters[index] + sourceDelta[index]))
            .ToArray();
        var sourceNext = mapping.SourceProjection(source.Evaluate(sourceNextParameters));
        ValidateProjection(sourceCurrent, mapping.CanonicalNames, "source");
        ValidateProjection(sourceNext, mapping.CanonicalNames, "source");
        var canonicalDelta = sourceNext.Zip(sourceCurrent, (after, before) => after - before).ToArray();

        var projectedTarget = new BehaviorModel(
            $"{target.Name}->canonical",
            target.Parameters,
            mapping.CanonicalNames,
            parameters =>
            {
                var projected = mapping.TargetProjection(target.Evaluate(parameters));
                ValidateProjection(projected, mapping.CanonicalNames, "target");
                return projected;
            });
        var targetResolution = JacobianBridge.Solve(
            projectedTarget,
            targetParameters,
            canonicalDelta,
            step,
            damping,
            maxIterations,
            tolerance);
        return new(
            targetResolution.Solved,
            canonicalDelta,
            targetResolution,
            targetResolution.Solved ? "projected_source_to_target_mission_translation" : targetResolution.Reason);
    }

    private static void ValidateProjection(
        IReadOnlyList<double> projection,
        IReadOnlyList<string> canonicalNames,
        string side)
    {
        if (projection.Count != canonicalNames.Count || projection.Any(double.IsNaN) || projection.Any(double.IsInfinity))
            throw new ArgumentException($"The {side} canonical projection has an invalid dimension or value.");
    }
}

public static class JacobianBridge
{
    public static JacobianBridgeResult Solve(
        BehaviorModel target,
        IReadOnlyList<double> targetParameters,
        IReadOnlyList<double> desiredOutputDelta,
        double step = 1e-5,
        double damping = 1e-6,
        int maxIterations = 32,
        double tolerance = 1e-6)
    {
        if (targetParameters.Count != target.Parameters.Count)
            throw new ArgumentException("Target parameter count does not match the model.");
        if (desiredOutputDelta.Count != target.Outputs.Count)
            throw new ArgumentException("Output count does not match the model.");

        var initial = targetParameters.ToArray();
        var working = initial.ToArray();
        var initialOutput = target.Evaluate(initial);
        var desiredOutput = initialOutput.Zip(desiredOutputDelta, (current, delta) => current + delta).ToArray();
        var currentOutput = initialOutput;
        var residualVector = Difference(desiredOutput, currentOutput);
        var residual = Norm(residualVector);
        if (residual <= tolerance)
            return new(true, new double[target.Parameters.Count], new double[target.Outputs.Count], residual, "already_at_target") { Iterations = 0 };

        var lambda = Math.Max(Math.Abs(damping), 1e-9);
        for (var iteration = 1; iteration <= maxIterations; iteration++)
        {
            var jacobian = NumericalJacobian(target, working, step);
            var normal = MultiplyTranspose(jacobian, jacobian);
            var rhs = MultiplyTransposeVector(jacobian, residualVector);
            for (var i = 0; i < normal.Length; i++)
                normal[i][i] += lambda * lambda;

            if (!TrySolve(normal, rhs, out var candidateDelta))
                return Result(false, initial, working, initialOutput, currentOutput, residual, "singular_target_model", iteration);

            var candidate = working.ToArray();
            for (var i = 0; i < candidate.Length; i++)
                candidate[i] = target.Parameters[i].Clamp(working[i] + candidateDelta[i]);
            if (candidate.SequenceEqual(working))
                return Result(false, initial, working, initialOutput, currentOutput, residual, "target_bounds_block_progress", iteration);

            var candidateOutput = target.Evaluate(candidate);
            var candidateResidualVector = Difference(desiredOutput, candidateOutput);
            var candidateResidual = Norm(candidateResidualVector);
            if (candidateResidual < residual)
            {
                working = candidate;
                currentOutput = candidateOutput;
                residualVector = candidateResidualVector;
                residual = candidateResidual;
                lambda = Math.Max(lambda / 2d, 1e-9);
                if (residual <= tolerance)
                    return Result(true, initial, working, initialOutput, currentOutput, residual, "solved_by_iterative_damped_least_squares", iteration);
            }
            else
            {
                lambda *= 10d;
            }
        }

        return Result(false, initial, working, initialOutput, currentOutput, residual, "tolerance_not_reached", maxIterations);
    }

    private static double[][] NumericalJacobian(BehaviorModel model, IReadOnlyList<double> parameters, double step)
    {
        var baseline = model.Evaluate(parameters);
        var result = new double[model.Outputs.Count][];
        for (var row = 0; row < result.Length; row++)
            result[row] = new double[model.Parameters.Count];

        for (var column = 0; column < model.Parameters.Count; column++)
        {
            var delta = Math.Min(step, Math.Max(step, (model.Parameters[column].Maximum - model.Parameters[column].Minimum) * 1e-5));
            var plus = parameters.ToArray();
            var minus = parameters.ToArray();
            plus[column] = model.Parameters[column].Clamp(parameters[column] + delta);
            minus[column] = model.Parameters[column].Clamp(parameters[column] - delta);
            var plusStep = plus[column] - parameters[column];
            var minusStep = parameters[column] - minus[column];
            if (plusStep > double.Epsilon && minusStep > double.Epsilon)
            {
                var plusValue = model.Evaluate(plus);
                var minusValue = model.Evaluate(minus);
                for (var row = 0; row < baseline.Length; row++)
                    result[row][column] = (plusValue[row] - minusValue[row]) / (plusStep + minusStep);
            }
            else if (plusStep > double.Epsilon)
            {
                var value = model.Evaluate(plus);
                for (var row = 0; row < baseline.Length; row++)
                    result[row][column] = (value[row] - baseline[row]) / plusStep;
            }
            else if (minusStep > double.Epsilon)
            {
                var value = model.Evaluate(minus);
                for (var row = 0; row < baseline.Length; row++)
                    result[row][column] = (baseline[row] - value[row]) / minusStep;
            }
        }
        return result;
    }

    private static JacobianBridgeResult Result(
        bool solved,
        IReadOnlyList<double> initial,
        IReadOnlyList<double> working,
        IReadOnlyList<double> initialOutput,
        IReadOnlyList<double> currentOutput,
        double residual,
        string reason,
        int iterations)
    {
        var delta = working.Select((value, index) => value - initial[index]).ToArray();
        var predicted = currentOutput.Select((value, index) => value - initialOutput[index]).ToArray();
        return new(solved, delta, predicted, residual, reason) { Iterations = iterations };
    }

    private static double[] Difference(IReadOnlyList<double> left, IReadOnlyList<double> right) =>
        left.Select((value, index) => value - right[index]).ToArray();

    private static double Norm(IReadOnlyList<double> values) =>
        Math.Sqrt(values.Sum(value => value * value));

    private static double[][] MultiplyTranspose(double[][] matrix, double[][] other)
    {
        var rows = matrix.Length;
        var columns = matrix[0].Length;
        var result = new double[columns][];
        for (var i = 0; i < columns; i++)
        {
            result[i] = new double[columns];
            for (var j = 0; j < columns; j++)
                for (var k = 0; k < rows; k++)
                    result[i][j] += matrix[k][i] * other[k][j];
        }
        return result;
    }

    private static double[] MultiplyTransposeVector(double[][] matrix, IReadOnlyList<double> vector)
    {
        var columns = matrix[0].Length;
        var result = new double[columns];
        for (var column = 0; column < columns; column++)
            for (var row = 0; row < matrix.Length; row++)
                result[column] += matrix[row][column] * vector[row];
        return result;
    }

    private static double[] Multiply(double[][] matrix, IReadOnlyList<double> vector)
    {
        var result = new double[matrix.Length];
        for (var row = 0; row < matrix.Length; row++)
            for (var column = 0; column < vector.Count; column++)
                result[row] += matrix[row][column] * vector[column];
        return result;
    }

    private static bool TrySolve(double[][] matrix, double[] vector, out double[] solution)
    {
        var n = vector.Length;
        var augmented = new double[n][];
        for (var row = 0; row < n; row++)
        {
            augmented[row] = new double[n + 1];
            Array.Copy(matrix[row], augmented[row], n);
            augmented[row][n] = vector[row];
        }

        for (var pivot = 0; pivot < n; pivot++)
        {
            var best = pivot;
            for (var row = pivot + 1; row < n; row++)
                if (Math.Abs(augmented[row][pivot]) > Math.Abs(augmented[best][pivot])) best = row;
            if (Math.Abs(augmented[best][pivot]) < 1e-12)
            {
                solution = Array.Empty<double>();
                return false;
            }
            (augmented[pivot], augmented[best]) = (augmented[best], augmented[pivot]);
            var divisor = augmented[pivot][pivot];
            for (var column = pivot; column <= n; column++) augmented[pivot][column] /= divisor;
            for (var row = 0; row < n; row++)
            {
                if (row == pivot) continue;
                var factor = augmented[row][pivot];
                for (var column = pivot; column <= n; column++) augmented[row][column] -= factor * augmented[pivot][column];
            }
        }

        solution = new double[n];
        for (var row = 0; row < n; row++) solution[row] = augmented[row][n];
        return true;
    }
}

public static class LightingCoreSelfTest
{
    public static void Run()
    {
        var target = new BehaviorModel(
            "min_max_target",
            new[] { new ControlParameter("minimum", 0, 1), new ControlParameter("maximum", 0, 1) },
            new[] { "center", "amplitude" },
            q => new[] { (q[0] + q[1]) / 2d, (q[1] - q[0]) / 2d });

        var bridge = JacobianBridge.Solve(target, new[] { 0.25, 0.75 }, new[] { 0d, 0.10d });
        if (!bridge.Solved || Math.Abs(bridge.Delta[0] + 0.10d) > 1e-3 || Math.Abs(bridge.Delta[1] - 0.10d) > 1e-3)
            throw new InvalidOperationException("Jacobian bridge failed to translate center/amplitude into min/max.");

        var nonlinear = new BehaviorModel(
            "nonlinear_target",
            new[] { new ControlParameter("position", 0, 1) },
            new[] { "energy" },
            q => new[] { q[0] * q[0] });
        var nonlinearBridge = JacobianBridge.Solve(nonlinear, new[] { 0.2 }, new[] { 0.44 }, maxIterations: 48, tolerance: 1e-5);
        var expectedNonlinearDelta = Math.Sqrt(0.2 * 0.2 + 0.44) - 0.2;
        if (!nonlinearBridge.Solved || nonlinearBridge.Iterations < 2 || Math.Abs(nonlinearBridge.Delta[0] - expectedNonlinearDelta) > 1e-3)
            throw new InvalidOperationException("Iterative nonlinear Jacobian bridge failed.");

        var impossibleBridge = JacobianBridge.Solve(nonlinear, new[] { 0.2 }, new[] { 2d }, maxIterations: 16, tolerance: 1e-5);
        if (impossibleBridge.Solved || impossibleBridge.ResidualNorm < 1d)
            throw new InvalidOperationException("Jacobian bridge incorrectly accepted an unreachable target.");

        var source = new BehaviorModel(
            "center_amplitude_source",
            new[] { new ControlParameter("center", 0, 1), new ControlParameter("amplitude", 0, 1) },
            new[] { "center", "amplitude" },
            q => new[] { q[0], q[1] });
        var reverse = MissionBridge.Translate(
            source,
            new[] { 0.5, 0.25 },
            new[] { 0d, 0.10d },
            target,
            new[] { 0.25, 0.75 });
        if (!reverse.Solved || Math.Abs(reverse.TargetResolution.Delta[0] + 0.10d) > 1e-3 || Math.Abs(reverse.TargetResolution.Delta[1] - 0.10d) > 1e-3)
            throw new InvalidOperationException($"Bidirectional mission bridge failed: solved={reverse.Solved}, delta=[{string.Join(",", reverse.TargetResolution.Delta)}], canonical=[{string.Join(",", reverse.CanonicalOutputDelta)}].");

        var personality = new FixturePersonality(
            "XANAX_TEST_FIXTURE",
            "16bit_pan_rgb",
            new[]
            {
                new DmxChannel("Pan", 1, 16, -270, 270),
                new DmxChannel("Red", 3, 8, 0, 1),
                new DmxChannel("Green", 4, 8, 0, 1),
                new DmxChannel("VirtualDimmer", 0, 0, 0, 1, true)
            });
        var values = personality.Encode(new Dictionary<string, double> { ["Pan"] = 0, ["Red"] = 0.5, ["Green"] = 0.25 });
        if (values.Count != 3 || values.Single(v => v.Address == 3).RawValue != 128)
            throw new InvalidOperationException("DMX personality encoding failed.");

        using var xml = new StringReader("""
<FixtureType Name="LOADER_TEST_FIXTURE">
  <DMXModes>
    <DMXMode Name="Mode 1">
      <DMXChannels>
        <DMXChannel DMXBreak="1" Offset="1 2">
          <LogicalChannel Attribute="Pan">
            <ChannelFunction Name="Pan" DMXFrom="0/1" DMXTo="65535/1" PhysicalFrom="-270" PhysicalTo="270" />
          </LogicalChannel>
        </DMXChannel>
        <DMXChannel DMXBreak="1" Offset="3">
          <LogicalChannel Attribute="Dimmer">
            <ChannelFunction Name="Dimmer" DMXFrom="0/1" DMXTo="255/1" PhysicalFrom="0" PhysicalTo="1" />
          </LogicalChannel>
        </DMXChannel>
      </DMXChannels>
    </DMXMode>
  </DMXModes>
</FixtureType>
""");
        var loaded = GdtfProfileLoader.LoadDescriptionXml(xml);
        if (loaded.Personality.Channels.Count != 2 ||
            loaded.Personality.Channels[0].ResolutionBits != 16 ||
            loaded.Personality.Channels[0].Functions.Count != 1 ||
            loaded.Personality.Channels[1].Attribute != "Dimmer")
            throw new InvalidOperationException("GDTF personality loading failed.");

        var sourceDmx = new FixturePersonality(
            "SOURCE",
            "source_mode",
            new[]
            {
                new DmxChannel("Pan", 1, 16, -270, 270),
                new DmxChannel("Dimmer", 3, 8, 0, 1)
            });
        var targetDmx = new FixturePersonality(
            "TARGET",
            "target_mode",
            new[]
            {
                new DmxChannel("Pan", 7, 8, -270, 270),
                new DmxChannel("Dimmer", 10, 16, 0, 1)
            });
        var sourcePhysical = new Dictionary<string, double> { ["Pan"] = 90, ["Dimmer"] = 0.5 };
        var sourceEncoded = sourceDmx.Encode(sourcePhysical);
        var canonical = PersonalityBridge.Decode(sourceDmx, sourceEncoded);
        var translated = PersonalityBridge.Translate(targetDmx, canonical);
        if (translated.UnsupportedAttributes.Count != 0 ||
            Math.Abs(translated.State["Pan"] - 90) > 0.01 ||
            translated.Frame[7] != 170 ||
            translated.Frame[10] != 128)
            throw new InvalidOperationException("Bidirectional DMX personality bridge failed.");

        var patchedState = new CanonicalSessionState(
            new[] { "F1", "F2" },
            new Dictionary<string, double>(),
            new Dictionary<string, double>())
        {
            FixtureAttributes = new Dictionary<string, IReadOnlyDictionary<string, double>>
            {
                ["F1"] = new Dictionary<string, double> { ["Pan"] = 90, ["Dimmer"] = 0.5 },
                ["F2"] = new Dictionary<string, double> { ["Pan"] = -90, ["Dimmer"] = 0.25 }
            }
        };
        var compiledPatch = PatchCompiler.Compile(
            new[]
            {
                new PatchedFixture("F1", sourceDmx, 2, 100),
                new PatchedFixture("F2", sourceDmx, 2, 110)
            },
            patchedState);
        if (!compiledPatch.Succeeded || compiledPatch.Frame[2, 100] != 170 || compiledPatch.Frame[2, 110] != 85 || compiledPatch.Frame.Slots.Count != 6)
            throw new InvalidOperationException("Multi-fixture DMX patch compilation failed.");

        var translatedPatch = PersonalityPatchBridge.Translate(
            new[]
            {
                new PersonalityPatchMapping("F1", sourceDmx, 1, 100, targetDmx, 3, 200)
            },
            patchedState);
        if (!translatedPatch.Succeeded || translatedPatch.CanonicalStates.Count != 1 ||
            translatedPatch.Frame[3, 206] != 170 || translatedPatch.Frame[3, 209] != 128 || translatedPatch.Frame[3, 210] != 0)
            throw new InvalidOperationException("Multi-fixture personality patch bridge failed.");

        var artNetOutput = CanonicalDmxOutputPlanner.CompileArtNet(
            new[] { new PersonalityPatchMapping("F1", sourceDmx, 1, 100, targetDmx, 3, 200) },
            patchedState,
            new[] { 3 },
            sequence: 11);
        if (!artNetOutput.Succeeded || artNetOutput.ArtNet?.Packets.Count != 1 ||
            artNetOutput.ArtNet.Packets[0].Bytes[18 + 205] != 170)
            throw new InvalidOperationException("Canonical Art-Net output planning failed.");

        var sacnOutput = CanonicalDmxOutputPlanner.CompileSacn(
            new[] { new PersonalityPatchMapping("F1", sourceDmx, 1, 100, targetDmx, 3, 200) },
            patchedState,
            new[] { 3 },
            Guid.Empty,
            "XANAX",
            sequence: 12);
        if (!sacnOutput.Succeeded || sacnOutput.Sacn.Count != 1 || sacnOutput.Sacn[0].Bytes[113] != 0 ||
            sacnOutput.Sacn[0].Bytes[114] != 3 || sacnOutput.Sacn[0].Bytes[125 + 205] != 170)
            throw new InvalidOperationException("Canonical sACN output planning failed.");

        var multiBreakPersonality = new FixturePersonality(
            "MULTI_BREAK",
            "mode",
            new[]
            {
                new DmxChannel("Dimmer", 1, 8, 0, 1) { DmxBreak = 1 },
                new DmxChannel("Pan", 1, 8, -270, 270) { DmxBreak = 2 }
            });
        var multiBreakState = new CanonicalSessionState(
            new[] { "F1" },
            new Dictionary<string, double>(),
            new Dictionary<string, double>())
        {
            FixtureAttributes = new Dictionary<string, IReadOnlyDictionary<string, double>>
            {
                ["F1"] = new Dictionary<string, double> { ["Dimmer"] = 0.5, ["Pan"] = 90 }
            }
        };
        var multiBreakPatch = MultiBreakPatchCompiler.Compile(
            new[] { new PatchedFixture("F1", multiBreakPersonality, 3, 100) },
            multiBreakState,
            new Dictionary<int, int> { [2] = 4 });
        if (!multiBreakPatch.Succeeded || multiBreakPatch.Frame[3, 100] != 128 || multiBreakPatch.Frame[4, 100] != 170)
            throw new InvalidOperationException("Multi-break DMX patch compilation failed.");

        var multiBreakBridge = MultiBreakPersonalityPatchBridge.Translate(
            new[]
            {
                new MultiBreakPersonalityPatchMapping(
                    "F1",
                    multiBreakPersonality,
                    3,
                    100,
                    multiBreakPersonality,
                    3,
                    100,
                    new Dictionary<int, int> { [2] = 4 })
            },
            multiBreakState);
        if (!multiBreakBridge.Succeeded || multiBreakBridge.Frame[3, 100] != 128 || multiBreakBridge.Frame[4, 100] != 170 ||
            multiBreakBridge.CanonicalStates["F1"]["Dimmer"] != 0.5 || multiBreakBridge.CanonicalStates["F1"]["Pan"] != 90)
            throw new InvalidOperationException("Multi-break personality bridge failed to preserve break identity.");
        var multiBreakArtNet = CanonicalDmxOutputPlanner.CompileMultiBreakArtNet(
            new[]
            {
                new MultiBreakPersonalityPatchMapping(
                    "F1",
                    multiBreakPersonality,
                    3,
                    100,
                    multiBreakPersonality,
                    3,
                    100,
                    new Dictionary<int, int> { [2] = 4 })
            },
            multiBreakState,
            new[] { 3, 4 },
            sequence: 13);
        if (!multiBreakArtNet.Succeeded || multiBreakArtNet.ArtNet?.Packets.Count != 2 ||
            multiBreakArtNet.ArtNet.Packets[0].Bytes[18 + 99] != 128 ||
            multiBreakArtNet.ArtNet.Packets[1].Bytes[18 + 99] != 170)
            throw new InvalidOperationException("Multi-break canonical Art-Net planning failed.");

        var titanRoute = new MissionRoute("titan", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, string>());
        var grandmaRoute = new MissionRoute("grandma3", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, string>());
        var presetMission = new MissionPlan(
            new CanonicalMissionOperation("preset_test", "recall_reusable_attribute_values", new Dictionary<string, string>
            {
                ["preset"] = "3",
                ["featureGroup"] = "1",
                ["presetMode"] = "Universal"
            }),
            new MissionDefinition("preset_test", "recall_reusable_attribute_values", "partial", titanRoute, grandmaRoute,
                new Dictionary<string, string> { ["grandma3"] = "OSC command line" }, Array.Empty<string>(), Array.Empty<string>()),
            MissionRelation.Lossy,
            titanRoute,
            grandmaRoute,
            Array.Empty<string>(),
            new[] { "OSC command line" },
            Array.Empty<string>());
        var presetCommand = AdapterCommandCompiler.Compile(presetMission);
        if (!presetCommand.Executable || presetCommand.Commands.Count != 1 || presetCommand.Commands[0].Payload != "/cmd,s,At Preset 1.3 /Universal")
            throw new InvalidOperationException("grandMA3 preset command compilation failed.");

        var cueMission = presetMission with
        {
            Operation = new CanonicalMissionOperation("cue_test", "store_or_edit_time_ordered_look", new Dictionary<string, string>
            {
                ["sequence"] = "8",
                ["cue"] = "20",
                ["overwrite"] = "true"
            }),
            Definition = presetMission.Definition with
            {
                Id = "cue_test",
                CanonicalIntent = "store_or_edit_time_ordered_look"
            }
        };
        var cueCommand = AdapterCommandCompiler.Compile(cueMission);
        if (!cueCommand.Executable || cueCommand.Commands[0].Payload != "/cmd,s,Store Sequence 8 Cue 20 /Overwrite")
            throw new InvalidOperationException("grandMA3 cue command compilation failed.");

        var titanPaletteMission = presetMission with
        {
            Operation = new CanonicalMissionOperation("palette_test", "recall_reusable_attribute_values", new Dictionary<string, string>
            {
                ["paletteHandle"] = "6",
                ["usePaletteTimes"] = "true"
            }),
            Source = grandmaRoute,
            Target = titanRoute,
            Definition = presetMission.Definition with
            {
                Id = "palette_test",
                NativeRoutes = new Dictionary<string, string> { ["titan"] = "Titan Web API Palette.ApplyPalette" }
            }
        };
        var titanPaletteCommand = AdapterCommandCompiler.Compile(titanPaletteMission);
        if (!titanPaletteCommand.Executable || titanPaletteCommand.Commands[0].Endpoint != "/titan/script/2/Palette/ApplyPalette?handle_userNumber=6&usePaletteTimes=true")
            throw new InvalidOperationException("Titan palette command compilation failed.");

        var orderedSelectionState = new CanonicalSessionState(
            new[] { "1", "2", "3", "4", "5" },
            new Dictionary<string, double>(),
            new Dictionary<string, double>());
        var wings = CanonicalOperationEngine.Apply(
            orderedSelectionState,
            new CanonicalMissionOperation("wings", "transform_ordered_fixture_selection", new Dictionary<string, string>
            {
                ["transform"] = "wings"
            }));
        if (!wings.Applied || !wings.State.SelectionOrder.SequenceEqual(new[] { "1", "5", "2", "4", "3" }))
            throw new InvalidOperationException("Canonical wings permutation failed.");

        var reverseMission = presetMission with
        {
            Operation = new CanonicalMissionOperation("selection_transform_test", "transform_ordered_fixture_selection", new Dictionary<string, string>
            {
                ["transform"] = "reverse"
            }),
            Definition = presetMission.Definition with
            {
                Id = "selection_transform_test",
                CanonicalIntent = "transform_ordered_fixture_selection"
            }
        };
        var reverseCommand = AdapterCommandCompiler.Compile(reverseMission, orderedSelectionState);
        if (!reverseCommand.Executable || reverseCommand.Commands.Count != 1 ||
            reverseCommand.Commands[0].Payload != "/cmd,s,Fixture 5 + Fixture 4 + Fixture 3 + Fixture 2 + Fixture 1")
            throw new InvalidOperationException("Canonical selection transform did not compile to grandMA3 selection order.");

        var composedSelectionMission = reverseMission with
        {
            Relation = MissionRelation.Composed,
            Definition = reverseMission.Definition with
            {
                Id = "selection_transform_composed_test",
                NativeRoutes = new Dictionary<string, string>()
            }
        };
        var transformedSelection = CanonicalOperationEngine.Apply(
            orderedSelectionState,
            composedSelectionMission.Operation);
        if (!transformedSelection.Applied ||
            !CanonicalMissionComposer.TryComposeSelectionTransform(
                composedSelectionMission,
                transformedSelection.State,
                out var composedSelectionPlan))
            throw new InvalidOperationException("Canonical selection transform composition failed.");
        var composedSelectionCommand = AdapterCommandCompiler.Compile(composedSelectionPlan, transformedSelection.State);
        if (!composedSelectionCommand.Executable || composedSelectionCommand.Commands.Count != 1 ||
            composedSelectionCommand.Commands[0].Payload != "/cmd,s,Fixture 5 + Fixture 4 + Fixture 3 + Fixture 2 + Fixture 1")
            throw new InvalidOperationException("Canonical absent-function selection composition failed.");

        var model = PersonalityModelFactory.Create(targetDmx);
        var modelOutput = model.Evaluate(new[] { 90d, 0.5d });
        if (modelOutput.Length != 2 || Math.Abs(modelOutput[0] - 90) > 0.001)
            throw new InvalidOperationException("Personality behavior model failed.");

        var colorTarget = new FixturePersonality(
            "CMY_TARGET",
            "cmy_mode",
            new[]
            {
                new DmxChannel("Cyan", 1, 8, 0, 1),
                new DmxChannel("Magenta", 2, 8, 0, 1),
                new DmxChannel("Yellow", 3, 8, 0, 1)
            });
        var rgbState = new CanonicalLightingState(new Dictionary<string, double>
        {
            ["Red"] = 0.2,
            ["Green"] = 0.4,
            ["Blue"] = 0.6
        });
        var colorTranslation = PersonalityBridge.Translate(
            colorTarget,
            rgbState,
            new[]
            {
                new DerivedCapabilityRule("Cyan", new Dictionary<string, double> { ["Red"] = -1 }, 1),
                new DerivedCapabilityRule("Magenta", new Dictionary<string, double> { ["Green"] = -1 }, 1),
                new DerivedCapabilityRule("Yellow", new Dictionary<string, double> { ["Blue"] = -1 }, 1)
            });
        if (colorTranslation.UnsupportedAttributes.Count != 0 ||
            colorTranslation.DerivedAttributes.Count != 3 ||
            colorTranslation.Frame[1] != 204 ||
            colorTranslation.Frame[2] != 153 ||
            colorTranslation.Frame[3] != 102)
            throw new InvalidOperationException("Derived capability translation failed.");
        var automaticColorTranslation = PersonalityBridge.TranslateAuto(colorTarget, rgbState);
        if (automaticColorTranslation.UnsupportedAttributes.Count != 0 || automaticColorTranslation.DerivedAttributes.Count != 3 ||
            automaticColorTranslation.Frame[1] != 204 || automaticColorTranslation.Frame[2] != 153 || automaticColorTranslation.Frame[3] != 102)
            throw new InvalidOperationException("Automatic standard capability translation failed.");

        var vocabularyTarget = new FixturePersonality(
            "VOCABULARY_TARGET",
            "intensity_mode",
            new[] { new DmxChannel("Intensity", 1, 8, 0, 1) });
        var vocabularyTranslation = PersonalityBridge.Translate(
            vocabularyTarget,
            new CanonicalLightingState(new Dictionary<string, double> { ["Dimmer"] = 0.5 }));
        if (vocabularyTranslation.UnsupportedAttributes.Count != 0 || vocabularyTranslation.Frame[1] != 128 ||
            vocabularyTranslation.Resolutions.Single(resolution => resolution.Attribute == "Intensity").Kind != "renamed")
            throw new InvalidOperationException("Canonical attribute vocabulary alias resolution failed.");

        var composedTarget = new FixturePersonality(
            "COMPOSED_TARGET",
            "composed_mode",
            new[] { new DmxChannel("Output", 1, 8, 0, 10) });
        var composedTranslation = PersonalityBridge.Translate(
            composedTarget,
            new CanonicalLightingState(new Dictionary<string, double>
            {
                ["Input"] = 2,
                ["Unrealized"] = 7
            }),
            new[]
            {
                new DerivedCapabilityRule("Intermediate", new Dictionary<string, double> { ["Input"] = 2 }),
                new DerivedCapabilityRule("Output", new Dictionary<string, double> { ["Intermediate"] = 1 }, 1)
            });
        if (composedTranslation.UnsupportedAttributes.Count != 1 || composedTranslation.UnsupportedAttributes[0] != "Unrealized" ||
            composedTranslation.DerivedAttributes.Count != 1 || composedTranslation.Frame[1] != 128 ||
            composedTranslation.Resolutions.Single(resolution => resolution.Attribute == "Output").Kind != "derived")
            throw new InvalidOperationException("Composed capability projection failed to resolve a multi-step function.");

        var wingsOffset = DistributionMath.Offset(new DistributionProfile("wings"), 0, 3, 1d);
        var centerOffset = DistributionMath.Offset(new DistributionProfile("center"), 1, 3, 1d);
        var sineOffset = DistributionMath.Offset(new DistributionProfile("sine"), 1, 5, 1d);
        if (Math.Abs(wingsOffset - 0.5d) > 1e-9 || Math.Abs(centerOffset + 0.5d) > 1e-9 ||
            Math.Abs(sineOffset - 0.5d) > 1e-9)
            throw new InvalidOperationException("Canonical distribution algebra failed.");

        var distributed = CanonicalOperationEngine.Apply(
            new CanonicalSessionState(
                new[] { "F1", "F2", "F3" },
                new Dictionary<string, double>(),
                new Dictionary<string, double>()),
            new CanonicalMissionOperation("distributed_attribute", "edit_fixture_attribute", new Dictionary<string, string>
            {
                ["attribute"] = "Dimmer",
                ["value"] = "0.5",
                ["spread"] = "1",
                ["distribution"] = "wings"
            }));
        if (!distributed.Applied ||
            Math.Abs(distributed.State.FixtureAttributes["F1"]["Dimmer"] - 1d) > 1e-9 ||
            Math.Abs(distributed.State.FixtureAttributes["F2"]["Dimmer"]) > 1e-9 ||
            Math.Abs(distributed.State.FixtureAttributes["F3"]["Dimmer"] - 1d) > 1e-9)
            throw new InvalidOperationException("Canonical fixture distribution application failed.");

        var distributedMission = presetMission with
        {
            Operation = new CanonicalMissionOperation("distributed_attribute_test", "edit_fixture_attribute", new Dictionary<string, string>
            {
                ["attribute"] = "Dimmer",
                ["value"] = "0.5",
                ["spread"] = "1",
                ["distribution"] = "wings"
            }),
            Definition = presetMission.Definition with
            {
                Id = "distributed_attribute_test",
                CanonicalIntent = "edit_fixture_attribute"
            }
        };
        var distributedCommands = AdapterCommandCompiler.Compile(distributedMission, distributed.State);
        if (!distributedCommands.Executable || distributedCommands.Commands.Count != 7 ||
            !distributedCommands.Commands.Any(command => command.Payload?.Contains("At 1", StringComparison.Ordinal) == true) ||
            !distributedCommands.Commands.Any(command => command.Payload?.Contains("At 0", StringComparison.Ordinal) == true))
            throw new InvalidOperationException("Distributed canonical attribute did not compile into reversible per-fixture native commands.");

        var missionInitial = new CanonicalSessionState(
            Array.Empty<string>(),
            new Dictionary<string, double>(),
            new Dictionary<string, double>());
        var traceA = new List<CanonicalExecutionResult>();
        var traceASelection = CanonicalOperationEngine.Apply(
            missionInitial,
            new CanonicalMissionOperation("trace_select", "select_fixture_set", new Dictionary<string, string> { ["fixtures"] = "F1,F2" }));
        traceA.Add(traceASelection);
        traceA.Add(CanonicalOperationEngine.Apply(
            traceASelection.State,
            new CanonicalMissionOperation("trace_edit", "edit_fixture_attribute", new Dictionary<string, string>
            {
                ["attribute"] = "Dimmer", ["value"] = "0.5", ["spread"] = "1", ["distribution"] = "wings"
            })));
        var traceB = new List<CanonicalExecutionResult>();
        var traceBSelection = CanonicalOperationEngine.Apply(
            missionInitial,
            new CanonicalMissionOperation("other_select", "select_fixture_set", new Dictionary<string, string> { ["fixtures"] = "F1,F2" }));
        traceB.Add(traceBSelection);
        var traceBInternal = CanonicalOperationEngine.Apply(
            traceBSelection.State,
            new CanonicalMissionOperation("other_internal_step", "select_fixture_set", new Dictionary<string, string> { ["fixtures"] = "F1,F2" }));
        traceB.Add(traceBInternal);
        traceB.Add(CanonicalOperationEngine.Apply(
            traceBInternal.State,
            new CanonicalMissionOperation("other_edit", "edit_fixture_attribute", new Dictionary<string, string>
            {
                ["attribute"] = "Dimmer", ["value"] = "0.5", ["spread"] = "1", ["distribution"] = "wings"
            })));
        var missionComparison = MissionTraceComparator.Compare(traceA, traceB);
        if (missionComparison.Relation != ConformanceRelation.Equal ||
            !missionComparison.Reason.Contains("decomposition", StringComparison.Ordinal))
            throw new InvalidOperationException("Mission trace alignment failed to accept an internal decomposition.");

        var reorderedTarget = new BehaviorModel(
            "reordered_target",
            new[] { new ControlParameter("minimum", 0, 1), new ControlParameter("maximum", 0, 1) },
            new[] { "amplitude", "center" },
            q => new[] { (q[1] - q[0]) / 2d, (q[0] + q[1]) / 2d });
        var reordered = MissionBridge.Translate(
            source,
            new[] { 0.5, 0.25 },
            new[] { 0d, 0.10d },
            reorderedTarget,
            new[] { 0.25, 0.75 });
        if (!reordered.Solved || Math.Abs(reordered.TargetResolution.Delta[0] + 0.10d) > 1e-3 || Math.Abs(reordered.TargetResolution.Delta[1] - 0.10d) > 1e-3)
            throw new InvalidOperationException("Canonical output-name mapping failed when target order changed.");

        var differentlyShapedTarget = new BehaviorModel(
            "differently_shaped_target",
            new[] { new ControlParameter("minimum", 0, 1), new ControlParameter("maximum", 0, 1) },
            new[] { "min", "max" },
            q => q.ToArray());
        var projected = MissionBridge.Translate(
            source,
            new[] { 0.5, 0.25 },
            new[] { 0d, 0.10d },
            differentlyShapedTarget,
            new[] { 0.25, 0.75 },
            new CanonicalOutputMapping(
                new[] { "center", "amplitude" },
                values => new[] { values[0], values[1] },
                values => new[] { (values[0] + values[1]) / 2d, (values[1] - values[0]) / 2d }));
        if (!projected.Solved || Math.Abs(projected.TargetResolution.Delta[0] + 0.10d) > 1e-3 || Math.Abs(projected.TargetResolution.Delta[1] - 0.10d) > 1e-3)
            throw new InvalidOperationException("Projected canonical behavior bridge failed for different output shapes.");

        var extensions = new CanonicalOperationRegistry();
        extensions.Register(new CanonicalOperationDefinition(
            "set_environment",
            "Switch the canonical editing environment without depending on a host UI.",
            (state, arguments) =>
            {
                if (!arguments.TryGetValue("environment", out var environment) || string.IsNullOrWhiteSpace(environment))
                    return new(false, state, Array.Empty<string>(), new[] { "environment_required" });
                return new(true, state with { Environment = environment }, new[] { $"environment:{environment}" }, Array.Empty<string>());
            }));
        var extensionResult = CanonicalOperationEngine.Apply(
            new CanonicalSessionState(Array.Empty<string>(), new Dictionary<string, double>(), new Dictionary<string, double>()),
            new CanonicalMissionOperation("environment", "set_environment", new Dictionary<string, string> { ["environment"] = "preview" }),
            extensions);
        if (!extensionResult.Applied || extensionResult.State.Environment != "preview")
            throw new InvalidOperationException("Canonical operation registry extension failed.");

        var reusableResult = CanonicalOperationEngine.Apply(
            missionInitial,
            new CanonicalMissionOperation("reusable", "recall_reusable_attribute_values", new Dictionary<string, string>
            {
                ["preset"] = "3", ["featureGroup"] = "1", ["presetMode"] = "Universal"
            }));
        var cueResult = CanonicalOperationEngine.Apply(
            missionInitial,
            new CanonicalMissionOperation("cue", "store_or_edit_time_ordered_look", new Dictionary<string, string>
            {
                ["sequence"] = "8", ["cue"] = "20"
            }));
        var previewResult = CanonicalOperationEngine.Apply(
            missionInitial,
            new CanonicalMissionOperation("preview", "edit_without_or_with_live_output", new Dictionary<string, string>
            {
                ["environment"] = "preview"
            }));
        var releaseResult = CanonicalOperationEngine.Apply(
            new CanonicalSessionState(
                Array.Empty<string>(),
                new Dictionary<string, double>(),
                new Dictionary<string, double> { ["P1"] = 1d, ["P2"] = 0.5d }),
            new CanonicalMissionOperation("release", "stop_or_release_active_output", new Dictionary<string, string>
            {
                ["playback"] = "P1"
            }));
        if (!reusableResult.Applied || !reusableResult.State.ReusableReferences.ContainsKey("1:3") ||
            !cueResult.Applied || !cueResult.State.TimeOrderedLooks.ContainsKey("8:20") ||
            !previewResult.Applied || previewResult.State.Environment != "preview" ||
            !releaseResult.Applied || releaseResult.State.PlaybackLevels.ContainsKey("P1") || !releaseResult.State.PlaybackLevels.ContainsKey("P2"))
            throw new InvalidOperationException("Canonical reusable, cue, environment or targeted release transition failed.");

        var hybrid = new CanonicalHybridState(
            new CanonicalSessionState(Array.Empty<string>(), new Dictionary<string, double>(), new Dictionary<string, double>()),
            new Dictionary<string, CanonicalFade>());
        var fadeStart = CanonicalHybridRuntime.Apply(
            hybrid,
            new CanonicalMissionOperation("playback", "trigger_or_adjust_live_playback", new Dictionary<string, string>
            {
                ["playback"] = "P1",
                ["action"] = "go",
                ["level"] = "1",
                ["fade"] = "2"
            }));
        var fadeMid = CanonicalHybridRuntime.Advance(fadeStart.State, 1);
        var fadeEnd = CanonicalHybridRuntime.Advance(fadeMid.State, 1);
        if (!fadeStart.Applied || !fadeMid.Applied || !fadeEnd.Applied ||
            Math.Abs(fadeMid.State.Session.PlaybackLevels["P1"] - 0.5d) > 1e-6 ||
            Math.Abs(fadeEnd.State.Session.PlaybackLevels["P1"] - 1d) > 1e-6 ||
            fadeEnd.State.Fades.Count != 0)
            throw new InvalidOperationException("Canonical hybrid fade runtime failed.");

        var modulationHybrid = new CanonicalHybridState(
            new CanonicalSessionState(
                new[] { "F1", "F2" },
                new Dictionary<string, double>(),
                new Dictionary<string, double>())
            {
                FixtureAttributes = new Dictionary<string, IReadOnlyDictionary<string, double>>
                {
                    ["F1"] = new Dictionary<string, double>(),
                    ["F2"] = new Dictionary<string, double>()
                }
            },
            new Dictionary<string, CanonicalFade>());
        var modulationStart = CanonicalHybridRuntime.Apply(
            modulationHybrid,
            new CanonicalMissionOperation("modulation", "start_attribute_modulation", new Dictionary<string, string>
            {
                ["attribute"] = "Dimmer",
                ["base"] = "0.5",
                ["amplitude"] = "0.5",
                ["frequency"] = "1",
                ["waveform"] = "sine",
                ["key"] = "pulse"
            }));
        var modulationPeak = CanonicalHybridRuntime.Advance(modulationStart.State, 0.25);
        var modulationStop = CanonicalHybridRuntime.Apply(
            modulationPeak.State,
            new CanonicalMissionOperation("modulation_stop", "stop_attribute_modulation", new Dictionary<string, string>
            {
                ["key"] = "pulse"
            }));
        if (!modulationStart.Applied || modulationStart.State.ActiveModulations.Count != 1 ||
            Math.Abs(modulationPeak.State.Session.FixtureAttributes["F1"]["Dimmer"] - 1d) > 1e-6 ||
            !modulationStop.Applied || modulationStop.State.ActiveModulations.Count != 0)
            throw new InvalidOperationException("Canonical frequency/phase modulation runtime failed.");

        var outputHybrid = new CanonicalHybridState(
            new CanonicalSessionState(
                new[] { "F1" },
                new Dictionary<string, double>(),
                new Dictionary<string, double>())
            {
                FixtureAttributes = new Dictionary<string, IReadOnlyDictionary<string, double>>
                {
                    ["F1"] = new Dictionary<string, double> { ["Pan"] = 0, ["Dimmer"] = 0.5 }
                }
            },
            new Dictionary<string, CanonicalFade>());
        outputHybrid = CanonicalHybridRuntime.Apply(
            outputHybrid,
            new CanonicalMissionOperation("output_modulation", "start_attribute_modulation", new Dictionary<string, string>
            {
                ["attribute"] = "Dimmer", ["base"] = "0.5", ["amplitude"] = "0.5", ["frequency"] = "1", ["waveform"] = "sine", ["key"] = "output_pulse"
            })).State;
        var outputTrajectory = CanonicalDmxTrajectoryPlanner.Sample(
            outputHybrid,
            new[] { new PersonalityPatchMapping("F1", sourceDmx, 1, 100, targetDmx, 3, 200) },
            new[] { 0d, 0.25d, 0.5d });
        var outputValues = outputTrajectory.Select(sample => (double)sample.Frame[3, 209]).ToArray();
        var outputSignature = LiveShowTrajectoryAnalyzer.Analyze(
            CanonicalDmxTrajectoryPlanner.ToUniverseTrajectory(outputTrajectory, 3)).Signature;
        if (outputTrajectory.Count != 3 || outputTrajectory.Any(sample => sample.Errors.Count != 0) ||
            outputValues[1] <= outputValues[0] || outputValues[2] >= outputValues[1] ||
            !outputSignature.Axes.Contains("rate_frequency", StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Canonical modulation-to-DMX trajectory planning failed.");

        var timedCommands = CanonicalAdapterTrajectoryPlanner.Compile(
            distributedMission,
            outputHybrid,
            new[] { 0d, 0.25d });
        if (!timedCommands.Succeeded || timedCommands.Commands.Count != 6 ||
            timedCommands.Commands[0].TimeSeconds != 0d || timedCommands.Commands[^1].TimeSeconds != 0.25d)
            throw new InvalidOperationException("Canonical trajectory-to-native command planning failed.");

        var modulationMission = distributedMission with
        {
            Operation = new CanonicalMissionOperation("composed_modulation", "start_attribute_modulation", new Dictionary<string, string>
            {
                ["attribute"] = "Dimmer", ["base"] = "0.5", ["amplitude"] = "0.5", ["frequency"] = "1", ["waveform"] = "sine", ["key"] = "composed_pulse"
            })
        };
        var composedCommands = CanonicalMissionComposer.CompileModulation(
            modulationMission,
            outputHybrid,
            new[] { 0d, 0.25d });
        if (!composedCommands.Succeeded || composedCommands.Commands.Count != 6)
            throw new InvalidOperationException("Canonical absent-function composition failed.");

        var outputFrame = new PatchedDmxFrame(new Dictionary<DmxAddress, byte>
        {
            [new DmxAddress(1, 1)] = 127,
            [new DmxAddress(1, 512)] = 85
        });
        var artNet = ArtNetEncoder.Encode(outputFrame, 1, sequence: 7);
        if (artNet.Bytes.Count != 530 || artNet.Bytes[0] != (byte)'A' || artNet.Bytes[8] != 0 || artNet.Bytes[9] != 0x50 ||
            artNet.Bytes[12] != 7 || artNet.Bytes[14] != 0 || artNet.Bytes[15] != 0 || artNet.Bytes[16] != 2 || artNet.Bytes[17] != 0 ||
            artNet.Bytes[18] != 127 || artNet.Bytes[529] != 85)
            throw new InvalidOperationException("Art-Net DMX encoder failed.");
        var sacn = SacnEncoder.Encode(outputFrame, 1, Guid.Empty, "XANAX", sequence: 9);
        if (sacn.Bytes.Count != SacnEncoder.PacketLength || sacn.Bytes[0] != 0 || sacn.Bytes[1] != 0x10 ||
            sacn.Bytes[16] != 0x72 || sacn.Bytes[17] != 0x6E || sacn.Bytes[38] != 0x72 || sacn.Bytes[39] != 0x58 ||
            sacn.Bytes[108] != 100 || sacn.Bytes[111] != 9 || sacn.Bytes[113] != 0 || sacn.Bytes[114] != 1 ||
            sacn.Bytes[115] != 0x72 || sacn.Bytes[116] != 0x0B || sacn.Bytes[125] != 0 || sacn.Bytes[126] != 127 || sacn.Bytes[637] != 85)
            throw new InvalidOperationException("sACN E1.31 encoder failed.");

        var sourceTrajectory = new[]
        {
            new TrajectorySample(0, new[] { 0d, 0d }),
            new TrajectorySample(1, new[] { 1d, 0.5d })
        };
        var equalTrajectory = TrajectoryComparator.Compare(sourceTrajectory, sourceTrajectory, 1e-6);
        var differentTrajectory = TrajectoryComparator.Compare(
            sourceTrajectory,
            new[]
            {
                new TrajectorySample(0, new[] { 0d, 0d }),
                new TrajectorySample(1, new[] { 0d, 0d })
            },
            1e-6);
        if (equalTrajectory.Relation != ConformanceRelation.Equal || differentTrajectory.Relation != ConformanceRelation.Different)
            throw new InvalidOperationException("Trajectory conformance comparison failed.");

        var rhythmAnalysis = LiveShowTrajectoryAnalyzer.Analyze(new[]
        {
            new TrajectorySample(0, new[] { 0d, 0d }),
            new TrajectorySample(1, new[] { 1d, 1d }),
            new TrajectorySample(2, new[] { 0d, 0d }),
            new TrajectorySample(3, new[] { 1d, 1d }),
            new TrajectorySample(4, new[] { 0d, 0d })
        });
        var rhythmAxis = Array.IndexOf(rhythmAnalysis.Signature.Axes.ToArray(), "rhythm");
        var energyAxis = Array.IndexOf(rhythmAnalysis.Signature.Axes.ToArray(), "amplitude_energy");
        if (rhythmAnalysis.PeakCount != 2 ||
            !rhythmAnalysis.Signature.Axes.Contains("rhythm", StringComparer.OrdinalIgnoreCase) ||
            rhythmAxis < 0 || energyAxis < 0 ||
            rhythmAnalysis.Signature.Values[rhythmAxis] <= 0 ||
            rhythmAnalysis.Signature.Values[energyAxis] <= 0)
            throw new InvalidOperationException("Live-show trajectory signature extraction failed.");

        var trajectoryCase = MissionCaseSignature.AttachTrajectory(
            MissionCaseFactory.FromDocumentedPlan(presetMission),
            rhythmAnalysis.Signature);
        if (trajectoryCase.NumericSignature.Count != rhythmAnalysis.Signature.Axes.Count ||
            !trajectoryCase.Signature.ContainsKey("trajectory:rhythm") ||
            trajectoryCase.ModelVersion != "learning-rel-v2")
            throw new InvalidOperationException("LEARNING trajectory signature persistence failed.");

        var matrix = new LearningSession();
        var learnedCase = trajectoryCase with
        {
            Id = "learned:trajectory:1",
            Relation = "common",
            EvidenceLevel = "replayed",
            Evidence = trajectoryCase.Evidence.Append("observation:trajectory:1").ToArray()
        };
        var learnedCaseTwo = learnedCase with
        {
            Id = "learned:trajectory:2",
            Evidence = trajectoryCase.Evidence.Append("observation:trajectory:2").ToArray()
        };
        var learned = matrix.LearnVerifiedCase(learnedCase);
        matrix.LearnVerifiedCase(learnedCaseTwo);
        var duplicateLearning = matrix.LearnVerifiedCase(learnedCase);
        var prediction = matrix.Predict(learnedCase with { Id = "query:trajectory:1" });
        var neuralPrediction = matrix.PredictNeural(learnedCase with { Id = "query:trajectory:1" });
        var matrixDecision = matrix.Decide(learnedCase with { Id = "query:trajectory:1" });
        var novelNeuralPrediction = matrix.PredictNeural(learnedCase with
        {
            Id = "query:novel:1",
            NumericSignature = LiveShowSignature.DefaultAxes.ToDictionary(axis => axis, _ => 100d, StringComparer.OrdinalIgnoreCase)
        });
        if (!learned.Learned || matrix.Cases.Count != 2 || prediction.Relation != "common" || !prediction.ExecutionEligible ||
            duplicateLearning.Learned || duplicateLearning.Reason != "mission_case_already_known" ||
            prediction.SupportingCases < 2 || !neuralPrediction.HasEvidence || neuralPrediction.Relation != "common" ||
            neuralPrediction.Confidence < 0.5 || !neuralPrediction.GeneralizationEligible || neuralPrediction.SupportingCases < 2 ||
            !neuralPrediction.WithinLearnedDomain || !matrixDecision.Consensus || !matrixDecision.ExecutionEligible ||
            novelNeuralPrediction.GeneralizationEligible || novelNeuralPrediction.WithinLearnedDomain)
            throw new InvalidOperationException("LEARNING evidence-only learning session failed.");

        var capability = new CapabilitySnapshot("grandma3", "2.4", true, true, true, true, true, true, true);
        var matrixDenied = ExecutionGate.Evaluate(presetCommand, capability, true, null, true);
        var matrixAllowed = ExecutionGate.Evaluate(
            presetCommand with { AllowApproximateRealization = true },
            capability,
            true,
            matrixDecision,
            true);
        if (matrixDenied.Allowed || !matrixAllowed.Allowed)
            throw new InvalidOperationException("LEARNING consensus execution gate failed.");
    }
}
