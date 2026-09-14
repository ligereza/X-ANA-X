using Xanax.Core;

LightingCoreSelfTest.Run();
Console.WriteLine("XANAX_CORE_VERIFIED");

if (args.Length > 0)
{
    var loaded = GdtfProfileLoader.Load(args[0], args.Length > 1 ? args[1] : null);
    Console.WriteLine($"GDTF_VERIFIED name={loaded.Personality.Name} mode={loaded.Personality.Mode} channels={loaded.Personality.Channels.Count} warnings={loaded.Warnings.Count}");
    foreach (var warning in loaded.Warnings)
        Console.WriteLine($"GDTF_WARNING {warning}");

    var multiplexed = loaded.Personality.Channels.FirstOrDefault(channel => channel.Functions.Count > 1);
    if (multiplexed is not null)
    {
        var function = multiplexed.Functions[0];
        var probe = loaded.Personality.Encode(new Dictionary<string, double> { [function.Attribute] = function.PhysicalFrom });
        var decoded = loaded.Personality.DecodeAttributes(probe);
        if (probe.Count == 0 || !decoded.ContainsKey(function.Attribute))
            throw new InvalidOperationException("GDTF multiplexed channel verification failed.");
        Console.WriteLine($"GDTF_MULTIPLEX_VERIFIED address={multiplexed.Address} functions={multiplexed.Functions.Count}");
    }
}

if (args.Length > 2)
{
    var catalog = MissionCatalog.Load(args[2]);
    var plan = catalog.Plan("attribute_edit", "titan", "grandma3", new Dictionary<string, string>
    {
        ["attribute"] = "Pan",
        ["value"] = "90"
    });
    if (plan.Operation.CanonicalIntent != "edit_fixture_attribute" || plan.Source.Host != "titan" || plan.Target.Host != "grandma3" || plan.TargetSteps.Count == 0)
        throw new InvalidOperationException("Canonical mission planning verification failed.");
    var initial = new CanonicalSessionState(Array.Empty<string>(), new Dictionary<string, double>(), new Dictionary<string, double>());
    var selected = CanonicalOperationEngine.Apply(initial, new CanonicalMissionOperation("selection", "select_fixture_set", new Dictionary<string, string> { ["fixtures"] = "F1,F2,F3" }));
    var edited = CanonicalOperationEngine.Apply(selected.State, plan.Operation);
    var spread = CanonicalOperationEngine.Apply(selected.State, new CanonicalMissionOperation("spread", "edit_fixture_attribute", new Dictionary<string, string>
    {
        ["attribute"] = "Pan",
        ["value"] = "0",
        ["spread"] = "1"
    }));
    var transformed = CanonicalOperationEngine.Apply(edited.State, new CanonicalMissionOperation("transform", "transform_ordered_fixture_selection", new Dictionary<string, string> { ["transform"] = "reverse" }));
    if (!selected.Applied || !edited.Applied || !spread.Applied || !transformed.Applied || transformed.State.SelectionOrder[0] != "F3" || Math.Abs(transformed.State.AttributeValues["Pan"] - 90) > 0.001 || Math.Abs(spread.State.FixtureAttributes["F1"]["Pan"] + 0.5) > 0.001 || Math.Abs(spread.State.FixtureAttributes["F3"]["Pan"] - 0.5) > 0.001)
        throw new InvalidOperationException("Canonical operation engine verification failed.");
    var reversePlan = catalog.Plan("attribute_edit", "grandma3", "titan", new Dictionary<string, string>
    {
        ["attribute"] = "Pan",
        ["value"] = "-45"
    });
    var reverseSelection = CanonicalOperationEngine.Apply(initial, new CanonicalMissionOperation("selection", "select_fixture_set", new Dictionary<string, string> { ["fixtures"] = "F1,F2" }));
    var reverseApplied = CanonicalOperationEngine.Apply(reverseSelection.State, reversePlan.Operation);
    if (!reverseApplied.Applied || Math.Abs(reverseApplied.State.AttributeValues["Pan"] + 45) > 0.001 || reversePlan.Source.Host != "grandma3" || reversePlan.Target.Host != "titan")
        throw new InvalidOperationException("Reverse canonical mission planning failed.");
    var equalTrace = CanonicalConformance.Compare(new[] { selected, edited }, new[] { selected, edited });
    var approximateTrace = CanonicalConformance.Compare(
        new[] { selected, edited },
        new[]
        {
            selected,
            new CanonicalExecutionResult(true, edited.State with
            {
                AttributeValues = new Dictionary<string, double> { ["Pan"] = 90.0005 }
            }, edited.Events, edited.Rejections)
        });
    var differentTrace = CanonicalConformance.Compare(new[] { selected, edited }, new[] { selected, reverseApplied });
    if (equalTrace.Relation != ConformanceRelation.Equal ||
        approximateTrace.Relation != ConformanceRelation.Approximate ||
        differentTrace.Relation != ConformanceRelation.Different)
        throw new InvalidOperationException("Canonical conformance comparison failed.");
    var forwardCommands = AdapterCommandCompiler.Compile(plan, transformed.State);
    var reverseCommands = AdapterCommandCompiler.Compile(reversePlan, reverseApplied.State);
    if (!forwardCommands.Executable || forwardCommands.Commands[0].RouteKind != AdapterRouteKind.NativeProtocol ||
        !reverseCommands.Executable || reverseCommands.Commands[0].RouteKind != AdapterRouteKind.NativeApi)
        throw new InvalidOperationException("Adapter command route classification failed.");
    Console.WriteLine($"MISSION_PLAN_VERIFIED id={plan.Operation.MissionId} relation={plan.Relation} missions={catalog.Definitions.Count} target_steps={plan.TargetSteps.Count}");
    Console.WriteLine($"CANONICAL_ENGINE_VERIFIED selection={string.Join(",", transformed.State.SelectionOrder)} pan={transformed.State.AttributeValues["Pan"]} reverse_pan={reverseApplied.State.AttributeValues["Pan"]}");
}
