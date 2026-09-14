namespace Xanax.Core;

public enum AdapterRouteKind
{
    NativeApi,
    NativeProtocol,
    SemanticSurface,
    CalibratedSurface,
    Unsupported
}

public sealed record AdapterCommand(
    string Host,
    AdapterRouteKind RouteKind,
    string Operation,
    IReadOnlyDictionary<string, string> Arguments,
    string RouteDescription,
    bool RequiresCalibration)
{
    public string? Endpoint { get; init; }
    public string? Payload { get; init; }
    public string? PayloadType { get; init; }
    public bool RequiresConfirmation { get; init; }
    public bool Concrete => !string.IsNullOrWhiteSpace(Endpoint) || !string.IsNullOrWhiteSpace(Payload);
}

public sealed record AdapterCommandPlan(
    MissionPlan Mission,
    IReadOnlyList<AdapterCommand> Commands,
    IReadOnlyList<string> Warnings)
{
    public MissionRealizationKind RealizationKind { get; init; } = MissionRealizationKind.Unsupported;
    public MatrixDecision? MatrixDecision { get; init; }
    public bool RequireMatrixConsensus { get; init; }
    public bool AllowApproximateRealization { get; init; }
    public bool Executable => Commands.Count > 0 && Commands.All(command => command.Concrete && !command.RequiresCalibration && !command.RequiresConfirmation && command.RouteKind is AdapterRouteKind.NativeApi or AdapterRouteKind.NativeProtocol);
}

public static class AdapterCommandCompiler
{
    public static AdapterCommandPlan Compile(MissionPlan mission, CanonicalSessionState? state = null)
    {
        var warnings = new List<string>();
        var host = mission.Target.Host;
        var hasNativeRoute = mission.Definition.NativeRoutes.TryGetValue(host, out var nativeRoute) ||
            NativeAdapterRouteCatalog.TryResolve(host, mission.Operation.CanonicalIntent, out nativeRoute!);
        var routeDescription = hasNativeRoute ? nativeRoute! : string.Join(" / ", mission.Target.Surface);
        var routeKind = hasNativeRoute ? Classify(nativeRoute!) : AdapterRouteKind.SemanticSurface;
        var requiresCalibration = routeKind is AdapterRouteKind.SemanticSurface or AdapterRouteKind.CalibratedSurface;
        if (!hasNativeRoute)
            warnings.Add($"no_native_route:{host}:{mission.Operation.CanonicalIntent}");

        var arguments = mission.Operation.Arguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        arguments["mission"] = mission.Operation.MissionId;
        arguments["canonical_intent"] = mission.Operation.CanonicalIntent;
        var commands = new List<AdapterCommand>();
        var expandedAttribute = false;

        if (mission.Operation.CanonicalIntent == "select_fixture_set" && routeKind is AdapterRouteKind.NativeApi or AdapterRouteKind.NativeProtocol)
        {
            var selection = state is not null && state.SelectionOrder.Count > 0
                ? state.SelectionOrder
                : ParseSelection(arguments);
            if (selection.Count > 0)
                commands.Add(CreateSelectionCommand(host, routeKind, selection, routeDescription));
            else
                warnings.Add("selection_route_requires_fixture_ids");
        }
        else if (mission.Operation.CanonicalIntent == "transform_ordered_fixture_selection" && routeKind is AdapterRouteKind.NativeApi or AdapterRouteKind.NativeProtocol)
        {
            if (state is null || state.SelectionOrder.Count == 0)
            {
                warnings.Add("selection_transform_requires_current_selection");
            }
            else
            {
                try
                {
                    var transformed = SelectionTransformMath.Apply(state.SelectionOrder, arguments);
                    commands.Add(CreateSelectionCommand(host, routeKind, transformed, routeDescription, "transform_ordered_fixture_selection"));
                }
                catch (ArgumentException)
                {
                    warnings.Add("selection_transform_has_unknown_transform");
                }
            }
        }
        else if (mission.Operation.CanonicalIntent == "edit_fixture_attribute" &&
                 routeKind is AdapterRouteKind.NativeApi or AdapterRouteKind.NativeProtocol &&
                 state is not null &&
                 RequiresPerFixtureProjection(arguments) &&
                 TryBuildPerFixtureAttributeCommands(host, routeKind, routeDescription, arguments, state, commands))
        {
            expandedAttribute = true;
        }
        else if (state is not null && state.SelectionOrder.Count > 0 && host.Equals("grandma3", StringComparison.OrdinalIgnoreCase) && routeKind == AdapterRouteKind.NativeProtocol)
        {
            commands.Add(CreateGrandMaSelectionCommand(host, state.SelectionOrder, routeDescription));
        }

        var command = new AdapterCommand(host, routeKind, mission.Operation.CanonicalIntent, arguments, routeDescription, requiresCalibration);
        if (!expandedAttribute && host.Equals("titan", StringComparison.OrdinalIgnoreCase) && mission.Operation.CanonicalIntent == "edit_fixture_attribute" && routeKind == AdapterRouteKind.NativeApi)
            command = command with
            {
                Endpoint = BuildTitanAttributeEndpoint(arguments),
                PayloadType = "application/x-www-form-urlencoded"
            };
        else if (!expandedAttribute && host.Equals("grandma3", StringComparison.OrdinalIgnoreCase) && mission.Operation.CanonicalIntent == "edit_fixture_attribute" && routeKind == AdapterRouteKind.NativeProtocol)
            command = command with
            {
                Endpoint = "/cmd",
                PayloadType = "osc:string",
                Payload = $"/cmd,s,{BuildGrandMaAttributeCommand(arguments)}"
            };
        else if (host.Equals("titan", StringComparison.OrdinalIgnoreCase) && mission.Operation.CanonicalIntent == "recall_reusable_attribute_values" && routeKind == AdapterRouteKind.NativeApi)
            command = BuildTitanPaletteCommand(command, arguments);
        else if (host.Equals("grandma3", StringComparison.OrdinalIgnoreCase) && mission.Operation.CanonicalIntent == "recall_reusable_attribute_values" && routeKind == AdapterRouteKind.NativeProtocol)
            command = BuildGrandMaPresetCommand(command, arguments);
        else if (host.Equals("grandma3", StringComparison.OrdinalIgnoreCase) && mission.Operation.CanonicalIntent == "store_or_edit_time_ordered_look" && routeKind == AdapterRouteKind.NativeProtocol)
            command = BuildGrandMaCueCommand(command, arguments);
        else if (mission.Operation.CanonicalIntent == "trigger_or_adjust_live_playback" && routeKind is AdapterRouteKind.NativeApi or AdapterRouteKind.NativeProtocol)
            command = BuildPlaybackCommand(command, arguments, host, routeKind);
        else if (mission.Operation.CanonicalIntent == "stop_or_release_active_output" && routeKind is AdapterRouteKind.NativeApi or AdapterRouteKind.NativeProtocol)
            command = BuildStopCommand(command, arguments, host, routeKind);
        else if (mission.Operation.CanonicalIntent is "select_fixture_set" or "transform_ordered_fixture_selection" || expandedAttribute)
        {
            // The concrete selection command was added above; this placeholder must not make
            // the plan look executable a second time.
            command = command with { RouteKind = AdapterRouteKind.Unsupported };
        }
        else if (routeKind is AdapterRouteKind.NativeApi or AdapterRouteKind.NativeProtocol)
            warnings.Add($"native_route_requires_template:{host}:{mission.Operation.CanonicalIntent}");

        if (command.RouteKind != AdapterRouteKind.Unsupported || command.Concrete)
            commands.Add(command);
        var plan = new AdapterCommandPlan(
            mission,
            commands,
            warnings);
        plan = plan with
        {
            RealizationKind = MissionRealization.Evaluate(
                mission,
                new CanonicalExecutionResult(true, state ?? new CanonicalSessionState(Array.Empty<string>(), new Dictionary<string, double>(), new Dictionary<string, double>()), Array.Empty<string>(), Array.Empty<string>()),
                commands).Kind
        };
        return plan;
    }

    private static IReadOnlyList<string> ParseSelection(IReadOnlyDictionary<string, string> arguments)
    {
        if (!arguments.TryGetValue("fixtures", out var rawFixtures))
            return Array.Empty<string>();
        return rawFixtures
            .Split(new[] { ',', ';', '|', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool RequiresPerFixtureProjection(IReadOnlyDictionary<string, string> arguments) =>
        arguments.ContainsKey("spread") || arguments.ContainsKey("distribution");

    private static bool TryBuildPerFixtureAttributeCommands(
        string host,
        AdapterRouteKind routeKind,
        string routeDescription,
        IReadOnlyDictionary<string, string> arguments,
        CanonicalSessionState state,
        ICollection<AdapterCommand> commands)
    {
        if (!arguments.TryGetValue("attribute", out var attribute) || string.IsNullOrWhiteSpace(attribute) ||
            state.SelectionOrder.Count == 0)
            return false;

        var projected = new List<(string Fixture, double Value)>();
        foreach (var fixture in state.SelectionOrder)
        {
            if (!state.FixtureAttributes.TryGetValue(fixture, out var values) || !values.TryGetValue(attribute, out var value))
                return false;
            projected.Add((fixture, value));
        }

        foreach (var (fixture, value) in projected)
        {
            commands.Add(CreateSelectionCommand(host, routeKind, new[] { fixture }, routeDescription, "select_fixture_set"));
            var perFixtureArguments = arguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            perFixtureArguments["value"] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            perFixtureArguments["mode"] = "absolute";
            var attributeCommand = new AdapterCommand(host, routeKind, "edit_fixture_attribute", perFixtureArguments, routeDescription, false);
            attributeCommand = host.Equals("titan", StringComparison.OrdinalIgnoreCase)
                ? attributeCommand with
                {
                    Endpoint = BuildTitanAttributeEndpoint(perFixtureArguments),
                    PayloadType = "application/x-www-form-urlencoded"
                }
                : attributeCommand with
                {
                    Endpoint = "/cmd",
                    PayloadType = "osc:string",
                    Payload = $"/cmd,s,{BuildGrandMaAttributeCommand(perFixtureArguments)}"
                };
            commands.Add(attributeCommand);
        }

        // A composed realization must not leave the host in a different selection.
        commands.Add(CreateSelectionCommand(host, routeKind, state.SelectionOrder, routeDescription, "select_fixture_set"));
        return true;
    }

    private static AdapterCommand CreateSelectionCommand(
        string host,
        AdapterRouteKind routeKind,
        IReadOnlyList<string> selection,
        string routeDescription,
        string operation = "select_fixture_set")
    {
        if (host.Equals("grandma3", StringComparison.OrdinalIgnoreCase))
            return CreateGrandMaSelectionCommand(host, selection, routeDescription, operation);

        if (!selection.All(fixture => int.TryParse(fixture, out _)))
        {
            return new AdapterCommand(
                host,
                AdapterRouteKind.Unsupported,
                operation,
                new Dictionary<string, string> { ["fixtures"] = string.Join(",", selection) },
                routeDescription,
                false);
        }

        var ids = string.Join(",", selection);
        return new AdapterCommand(
            host,
            routeKind,
            operation,
            new Dictionary<string, string> { ["fixtures"] = string.Join(",", selection) },
            routeDescription,
            false)
        {
            Endpoint = $"/titan/script/2/Programmer/OnOff/Selection/SetNewHandleSelection?newSelection_userNumberList={Uri.EscapeDataString(ids)}",
            PayloadType = "application/x-www-form-urlencoded"
        };
    }

    private static AdapterCommand BuildPlaybackCommand(
        AdapterCommand command,
        IReadOnlyDictionary<string, string> arguments,
        string host,
        AdapterRouteKind routeKind)
    {
        if (!arguments.TryGetValue("playback", out var playback) || string.IsNullOrWhiteSpace(playback))
            return command with { RouteKind = AdapterRouteKind.Unsupported };
        var action = arguments.TryGetValue("action", out var rawAction) ? rawAction.ToLowerInvariant() : "go";

        if (host.Equals("titan", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(playback, out _))
                return command with { RouteKind = AdapterRouteKind.Unsupported };
            var id = Uri.EscapeDataString(playback);
            if (action is "kill" or "off" or "release")
                return command with { Endpoint = $"/titan/script/Playbacks/KillPlayback?userNumber={id}", PayloadType = "application/x-www-form-urlencoded" };
            if (action is "go" or "flash")
            {
                var level = arguments.TryGetValue("level", out var rawLevel) ? rawLevel : "1";
                return command with { Endpoint = $"/titan/script/Playbacks/FirePlaybackAtLevel?userNumber={id}&level={Uri.EscapeDataString(level)}&bool=false", PayloadType = "application/x-www-form-urlencoded" };
            }
            return command with { RouteKind = AdapterRouteKind.Unsupported };
        }

        if (host.Equals("grandma3", StringComparison.OrdinalIgnoreCase) && routeKind == AdapterRouteKind.NativeProtocol)
        {
            var objectName = $"Executor {playback}";
            var syntax = action switch
            {
                "go" => $"Go {objectName}",
                "off" or "kill" or "release" => $"Off {objectName}",
                "toggle" => $"Toggle {objectName}",
                _ => string.Empty
            };
            if (string.IsNullOrWhiteSpace(syntax))
                return command with { RouteKind = AdapterRouteKind.Unsupported };
            return command with { Endpoint = "/cmd", PayloadType = "osc:string", Payload = $"/cmd,s,{syntax}" };
        }

        return command with { RouteKind = AdapterRouteKind.Unsupported };
    }

    private static AdapterCommand BuildGrandMaPresetCommand(
        AdapterCommand command,
        IReadOnlyDictionary<string, string> arguments)
    {
        if (!arguments.TryGetValue("preset", out var preset) || string.IsNullOrWhiteSpace(preset))
            return command with { RouteKind = AdapterRouteKind.Unsupported };
        var featureGroup = arguments.TryGetValue("featureGroup", out var group) && !string.IsNullOrWhiteSpace(group)
            ? $"{group}."
            : string.Empty;
        var mode = arguments.TryGetValue("presetMode", out var rawMode) && !string.IsNullOrWhiteSpace(rawMode)
            ? $" /{rawMode.TrimStart('/')}"
            : string.Empty;
        return command with
        {
            Endpoint = "/cmd",
            PayloadType = "osc:string",
            Payload = $"/cmd,s,At Preset {featureGroup}{preset}{mode}"
        };
    }

    private static AdapterCommand BuildTitanPaletteCommand(
        AdapterCommand command,
        IReadOnlyDictionary<string, string> arguments)
    {
        var handle = arguments.TryGetValue("paletteHandle", out var paletteHandle) ? paletteHandle :
            arguments.TryGetValue("palette", out var palette) ? palette : string.Empty;
        if (!int.TryParse(handle, out _))
            return command with { RouteKind = AdapterRouteKind.Unsupported };
        var useTimes = arguments.TryGetValue("usePaletteTimes", out var rawUseTimes) &&
            bool.TryParse(rawUseTimes, out var parsedUseTimes) && parsedUseTimes;
        return command with
        {
            Endpoint = $"/titan/script/2/Palette/ApplyPalette?handle_userNumber={Uri.EscapeDataString(handle)}&usePaletteTimes={useTimes.ToString().ToLowerInvariant()}",
            PayloadType = "application/x-www-form-urlencoded"
        };
    }

    private static AdapterCommand BuildGrandMaCueCommand(
        AdapterCommand command,
        IReadOnlyDictionary<string, string> arguments)
    {
        var cue = arguments.TryGetValue("cue", out var rawCue) ? rawCue :
            arguments.TryGetValue("cueNumber", out var cueNumber) ? cueNumber : string.Empty;
        if (string.IsNullOrWhiteSpace(cue))
            return command with { RouteKind = AdapterRouteKind.Unsupported };
        var sequence = arguments.TryGetValue("sequence", out var rawSequence) ? rawSequence :
            arguments.TryGetValue("sequenceNumber", out var sequenceNumber) ? sequenceNumber : string.Empty;
        var sequencePart = string.IsNullOrWhiteSpace(sequence) ? string.Empty : $"Sequence {sequence} ";
        var overwrite = arguments.TryGetValue("overwrite", out var rawOverwrite) &&
            bool.TryParse(rawOverwrite, out var parsedOverwrite) && parsedOverwrite;
        var option = overwrite ? " /Overwrite" : string.Empty;
        return command with
        {
            Endpoint = "/cmd",
            PayloadType = "osc:string",
            Payload = $"/cmd,s,Store {sequencePart}Cue {cue}{option}"
        };
    }

    private static AdapterCommand BuildStopCommand(
        AdapterCommand command,
        IReadOnlyDictionary<string, string> arguments,
        string host,
        AdapterRouteKind routeKind)
    {
        if (host.Equals("titan", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.TryGetValue("playback", out var playback) && int.TryParse(playback, out _))
                return command with { Endpoint = $"/titan/script/Playbacks/KillPlayback?userNumber={Uri.EscapeDataString(playback)}", PayloadType = "application/x-www-form-urlencoded" };
            return command with
            {
                Endpoint = "/titan/script/Playbacks/KillAllPlaybacks",
                PayloadType = "application/x-www-form-urlencoded",
                RequiresConfirmation = true
            };
        }

        if (host.Equals("grandma3", StringComparison.OrdinalIgnoreCase) && routeKind == AdapterRouteKind.NativeProtocol)
        {
            if (!arguments.TryGetValue("playback", out var playback) || string.IsNullOrWhiteSpace(playback))
                return command with { RouteKind = AdapterRouteKind.Unsupported };
            return command with { Endpoint = "/cmd", PayloadType = "osc:string", Payload = $"/cmd,s,Off Executor {playback}" };
        }

        return command with { RouteKind = AdapterRouteKind.Unsupported };
    }

    private static AdapterCommand CreateGrandMaSelectionCommand(string host, IReadOnlyList<string> selection, string routeDescription, string operation = "select_fixture_set")
    {
        var fixtureTerms = selection.Select(fixture => int.TryParse(fixture, out var number) ? number.ToString() : $"\"{fixture.Replace("\"", "\\\"")}\"");
        var command = $"Fixture {string.Join(" + Fixture ", fixtureTerms)}";
        return new AdapterCommand(host, AdapterRouteKind.NativeProtocol, operation, new Dictionary<string, string> { ["fixtures"] = string.Join(",", selection) }, routeDescription, false)
        {
            Endpoint = "/cmd",
            PayloadType = "osc:string",
            Payload = $"/cmd,s,{command}"
        };
    }

    private static string BuildGrandMaAttributeCommand(IReadOnlyDictionary<string, string> arguments)
    {
        var attribute = arguments.TryGetValue("attribute", out var rawAttribute) ? rawAttribute : throw new InvalidOperationException("Attribute command requires an attribute.");
        var value = arguments.TryGetValue("value", out var rawValue) ? rawValue : throw new InvalidOperationException("Attribute command requires a value.");
        var mode = arguments.TryGetValue("mode", out var rawMode) ? rawMode : string.Empty;
        var valueExpression = string.Equals(mode, "relative", StringComparison.OrdinalIgnoreCase) ? $"+ {value}" : value;
        return $"Attribute \"{attribute.Replace("\"", "\\\"")}\" At {valueExpression}";
    }

    private static string BuildTitanAttributeEndpoint(IReadOnlyDictionary<string, string> arguments)
    {
        var attribute = Uri.EscapeDataString(arguments.TryGetValue("attribute", out var rawAttribute) ? rawAttribute : string.Empty);
        var function = Uri.EscapeDataString(arguments.TryGetValue("functionName", out var rawFunction) ? rawFunction :
            arguments.TryGetValue("attribute", out var attributeFallback) ? attributeFallback : string.Empty);
        var value = Uri.EscapeDataString(arguments.TryGetValue("value", out var rawValue) ? rawValue : string.Empty);
        var programmer = arguments.TryGetValue("programmer", out var rawProgrammer) ? rawProgrammer : "true";
        var restore = arguments.TryGetValue("createRestorePoint", out var rawRestore) ? rawRestore : "true";
        return $"/titan/script/2/Programmer/Editor/Fixtures/SetControlValueByName?controlName={attribute}&functionName={function}&value={value}&programmer={programmer}&createRestorePoint={restore}";
    }

    private static AdapterRouteKind Classify(string route)
    {
        var lower = route.ToLowerInvariant();
        if (lower.Contains("web api")) return AdapterRouteKind.NativeApi;
        if (lower.Contains("osc") || lower.Contains("command line") || lower.Contains("lua")) return AdapterRouteKind.NativeProtocol;
        if (lower.Contains("sendinput") || lower.Contains("mouse") || lower.Contains("coordinate")) return AdapterRouteKind.CalibratedSurface;
        return AdapterRouteKind.Unsupported;
    }
}
