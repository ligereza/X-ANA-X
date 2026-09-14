using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

namespace Xanax.Core;

public sealed record NativeTransportTarget(
    Uri? TitanHttpBaseUri = null,
    IPEndPoint? GrandMaOscEndpoint = null);

public sealed record NativeTransportResult(
    bool Sent,
    string Host,
    string Route,
    int? StatusCode,
    string? Response,
    string? Error)
{
    public static NativeTransportResult Rejected(AdapterCommand command, string error) =>
        new(false, command.Host, command.Endpoint ?? command.PayloadType ?? "unknown", null, null, error);

    public static NativeTransportResult Rejected(string host, string route, string error) =>
        new(false, host, route, null, null, error);
}

/// <summary>
/// Sends already-compiled native commands. It deliberately does not discover,
/// start or configure either console. Capability checks belong before this boundary.
/// </summary>
public sealed class NativeAdapterTransport
{
    private readonly HttpClient http;

    public NativeAdapterTransport(HttpClient? httpClient = null)
    {
        http = httpClient ?? new HttpClient();
    }

    public async Task<IReadOnlyList<NativeTransportResult>> SendPlanAsync(
        AdapterCommandPlan plan,
        NativeTransportTarget target,
        CapabilitySnapshot capability,
        bool permitPhysicalOutput = false,
        CancellationToken cancellationToken = default)
    {
        var gate = ExecutionGate.Evaluate(
            plan,
            capability,
            permitPhysicalOutput,
            plan.LearningDecision,
            plan.RequireLearningConsensus);
        if (!gate.Allowed)
        {
            var route = string.Join(";", gate.Reasons);
            return plan.Commands.Count == 0
                ? new[] { NativeTransportResult.Rejected(plan.Mission.Target.Host, "plan", route) }
                : plan.Commands.Select(command => NativeTransportResult.Rejected(command, route)).ToArray();
        }

        var results = new List<NativeTransportResult>();
        foreach (var command in plan.Commands)
        {
            var result = await SendAsync(command, target, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            if (!result.Sent)
                break;
        }
        return results;
    }

    public async Task<IReadOnlyList<NativeTransportResult>> SendTrajectoryAsync(
        AdapterCommandPlan plan,
        AdapterTrajectoryPlan trajectory,
        NativeTransportTarget target,
        CapabilitySnapshot capability,
        bool permitPhysicalOutput = false,
        CancellationToken cancellationToken = default)
    {
        var gate = ExecutionGate.Evaluate(
            plan,
            capability,
            permitPhysicalOutput,
            plan.LearningDecision,
            plan.RequireLearningConsensus);
        if (!gate.Allowed)
        {
            var route = string.Join(";", gate.Reasons);
            return plan.Commands.Count == 0
                ? new[] { NativeTransportResult.Rejected(plan.Mission.Target.Host, "trajectory", route) }
                : plan.Commands.Select(command => NativeTransportResult.Rejected(command, route)).ToArray();
        }
        if (!trajectory.Succeeded)
            return new[] { NativeTransportResult.Rejected(plan.Mission.Target.Host, "trajectory", "trajectory_is_not_executable") };
        if (plan.Commands.Count != trajectory.Commands.Count ||
            plan.Commands.Zip(trajectory.Commands, (planned, timed) => SameCommand(planned, timed.Command)).Any(matches => !matches))
            return new[] { NativeTransportResult.Rejected(plan.Mission.Target.Host, "trajectory", "trajectory_plan_mismatch") };

        var results = new List<NativeTransportResult>();
        var clock = Stopwatch.StartNew();
        foreach (var timed in trajectory.Commands)
        {
            var remainingSeconds = timed.TimeSeconds - clock.Elapsed.TotalSeconds;
            if (remainingSeconds > 0)
                await Task.Delay(TimeSpan.FromSeconds(remainingSeconds), cancellationToken).ConfigureAwait(false);
            var result = await SendAsync(timed.Command, target, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            if (!result.Sent)
                break;
        }
        return results;
    }

    private static bool SameCommand(AdapterCommand left, AdapterCommand right) =>
        string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
        left.RouteKind == right.RouteKind &&
        string.Equals(left.Operation, right.Operation, StringComparison.Ordinal) &&
        string.Equals(left.Endpoint, right.Endpoint, StringComparison.Ordinal) &&
        string.Equals(left.PayloadType, right.PayloadType, StringComparison.Ordinal) &&
        string.Equals(left.Payload, right.Payload, StringComparison.Ordinal) &&
        left.RequiresCalibration == right.RequiresCalibration &&
        left.RequiresConfirmation == right.RequiresConfirmation;

    public async Task<NativeTransportResult> SendAsync(
        AdapterCommand command,
        NativeTransportTarget target,
        CancellationToken cancellationToken = default)
    {
        if (!command.Concrete)
            return NativeTransportResult.Rejected(command, "command_has_no_concrete_route");
        if (command.RequiresCalibration)
            return NativeTransportResult.Rejected(command, "command_requires_calibration");
        if (command.RequiresConfirmation)
            return NativeTransportResult.Rejected(command, "command_requires_explicit_confirmation");

        if (command.Host.Equals("titan", StringComparison.OrdinalIgnoreCase) && command.RouteKind == AdapterRouteKind.NativeApi)
            return await SendTitanAsync(command, target.TitanHttpBaseUri, cancellationToken).ConfigureAwait(false);

        if (command.Host.Equals("grandma3", StringComparison.OrdinalIgnoreCase) && command.RouteKind == AdapterRouteKind.NativeProtocol)
            return await SendGrandMaOscAsync(command, target.GrandMaOscEndpoint, cancellationToken).ConfigureAwait(false);

        return NativeTransportResult.Rejected(command, "unsupported_native_transport");
    }

    private async Task<NativeTransportResult> SendTitanAsync(
        AdapterCommand command,
        Uri? baseUri,
        CancellationToken cancellationToken)
    {
        if (baseUri is null)
            return NativeTransportResult.Rejected(command, "titan_http_base_uri_required");
        if (string.IsNullOrWhiteSpace(command.Endpoint))
            return NativeTransportResult.Rejected(command, "titan_endpoint_required");

        var uri = new Uri(baseUri, command.Endpoint);
        try
        {
            using var response = await http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new(response.IsSuccessStatusCode, command.Host, uri.ToString(), (int)response.StatusCode, body,
                response.IsSuccessStatusCode ? null : $"http_status:{(int)response.StatusCode}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new(false, command.Host, uri.ToString(), null, null, exception.GetType().Name);
        }
    }

    private static async Task<NativeTransportResult> SendGrandMaOscAsync(
        AdapterCommand command,
        IPEndPoint? endpoint,
        CancellationToken cancellationToken)
    {
        if (endpoint is null)
            return NativeTransportResult.Rejected(command, "grandma3_osc_endpoint_required");
        if (!string.Equals(command.PayloadType, "osc:string", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(command.Payload))
            return NativeTransportResult.Rejected(command, "grandma3_osc_string_payload_required");

        if (!TryEncodeOscString(command.Payload, out var packet, out var error))
            return NativeTransportResult.Rejected(command, error!);

        try
        {
            using var udp = new UdpClient();
            await udp.SendAsync(packet, endpoint, cancellationToken).ConfigureAwait(false);
            return new(true, command.Host, $"osc://{endpoint.Address}:{endpoint.Port}{command.Endpoint}", null, null, null);
        }
        catch (Exception exception) when (exception is SocketException or TaskCanceledException)
        {
            return new(false, command.Host, $"osc://{endpoint.Address}:{endpoint.Port}{command.Endpoint}", null, null, exception.GetType().Name);
        }
    }

    private static bool TryEncodeOscString(string payload, out byte[] packet, out string? error)
    {
        packet = Array.Empty<byte>();
        error = null;
        var separator = payload.IndexOf(",s,", StringComparison.Ordinal);
        if (separator <= 0)
        {
            error = "osc_payload_must_use_address_comma_s_comma_string";
            return false;
        }

        var address = payload[..separator];
        var argument = payload[(separator + 3)..];
        if (!address.StartsWith('/'))
        {
            error = "osc_address_must_start_with_slash";
            return false;
        }

        using var stream = new MemoryStream();
        WriteOscString(stream, address);
        WriteOscString(stream, ",s");
        WriteOscString(stream, argument);
        packet = stream.ToArray();
        return true;
    }

    private static void WriteOscString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
        stream.WriteByte(0);
        while (stream.Position % 4 != 0)
            stream.WriteByte(0);
    }
}
