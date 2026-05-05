using System.Text;
using System.Threading.Channels;
using HannaDemoApp.Models;

namespace HannaDemoApp.Services.Ota;

/// <summary>
/// Photometer BLE OTA sequence aligned with native <c>HNAPMFirmwareUpdateVC</c> (HI97115 / HI97105 family).
/// </summary>
public sealed class HNAPhotometerOtaCoordinator(IPhotometerOtaBleTransport transport)
{
    private readonly IPhotometerOtaBleTransport _transport = transport;

    public async Task RunTransferAsync(
        HNABleDeviceModel device,
        IReadOnlyList<HNAPhotometerFirmwarePart> parts,
        IProgress<HNAOtaUiProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count == 0)
        {
            throw new ArgumentException("Firmware parts are required.", nameof(parts));
        }

        var options = HNAOtaOptions.Default;
        await _transport.TryRequestMtuAsync(device, options.MtuRequestValue).ConfigureAwait(false);

        var dataChunkMax = ResolveMaxSfDataBytes(device, options);
        var totalBytes = Math.Max(1, parts.Sum(p => p.FileData.Length));
        var sent = 0;
        var fileCount = parts.Count;

        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        using var _sub = _transport.SubscribeAcks(device, line => channel.Writer.TryWrite(line));

        async Task<string> NextLineAsync(TimeSpan timeout)
        {
            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            waitCts.CancelAfter(timeout);
            try
            {
                while (await channel.Reader.WaitToReadAsync(waitCts.Token).ConfigureAwait(false))
                {
                    if (channel.Reader.TryRead(out var line))
                    {
                        return line;
                    }
                }

                throw new TimeoutException(
                    $"Timed out after {timeout.TotalSeconds:0.#}s waiting for a photometer response (channel closed).");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Timed out after {timeout.TotalSeconds:0.#}s waiting for a photometer acknowledgement during OTA.");
            }
        }

        async Task<string> ExpectPrefixAsync(string prefix, TimeSpan timeout)
        {
            var line = await NextLineAsync(timeout).ConfigureAwait(false);
            var head = HNAOtaProtocol.ParseAckHead(line);
            if (!string.Equals(head, prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Expected OTA acknowledgement {prefix}, received {head} (line: {line}).");
            }

            return head;
        }

        async Task<string> ExpectAnyPrefixAsync(string[] prefixes, TimeSpan timeout)
        {
            var line = await NextLineAsync(timeout).ConfigureAwait(false);
            var head = HNAOtaProtocol.ParseAckHead(line);
            if (prefixes.Any(p => string.Equals(p, head, StringComparison.Ordinal)))
            {
                return head;
            }

            throw new InvalidOperationException(
                $"Expected OTA acknowledgement [{string.Join(", ", prefixes)}], received {head} (line: {line}).");
        }

        Task SendCmdAsync(string cmd) => _transport.SendCommandAsync(device, cmd, cancellationToken);

        async Task SendSfPayloadAsync(ReadOnlyMemory<byte> chunk)
        {
            var header = Encoding.UTF8.GetBytes(HNAOtaProtocol.Commands.SendFilePrefix);
            var payload = new byte[header.Length + chunk.Length];
            header.CopyTo(payload, 0);
            chunk.CopyTo(payload.AsMemory(header.Length));
            await _transport.SendPayloadAsync(device, payload, cancellationToken)
                .ConfigureAwait(false);
            if (options.InterChunkDelayMs > 0)
            {
                await Task.Delay(options.InterChunkDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        void Report(string title, int percent) =>
            progress?.Report(new HNAOtaUiProgress(title, Math.Clamp(percent, 0, 100)));

        var isBoot = string.Equals(device.MeterId?.Trim(), "BOOT", StringComparison.OrdinalIgnoreCase);

        var enteredBootloaderNow = false;
        if (!isBoot)
        {
            Report("Entering bootloader…\n(0 of " + fileCount + " complete)", 0);
            try
            {
                await SendCmdAsync(HNAOtaProtocol.Commands.BootloaderMode).ConfigureAwait(false);
                await ExpectPrefixAsync(HNAOtaProtocol.Acks.Sp, options.EnterBootloaderTimeout).ConfigureAwait(false);
                enteredBootloaderNow = true;
            }
            catch (TimeoutException)
            {
                // Recovery path: device may already be in bootloader after previous failed OTA.
                Report("Bootloader already active, trying fast connection…", 0);
            }
        }

        Report("Fast connection…\n(0 of " + fileCount + " complete)", 1);
        await SendCmdAsync(HNAOtaProtocol.Commands.FastConnection).ConfigureAwait(false);
        await ExpectAnyPrefixAsync([HNAOtaProtocol.Acks.Sc], options.FastConnectionTimeout).ConfigureAwait(false);

        // If we just switched into bootloader using set PICh2N6a, PIC is already blocked.
        // If meter was already in bootloader, keep the explicit block command.
        if (!enteredBootloaderNow || isBoot)
        {
            await SendCmdAsync(HNAOtaProtocol.Commands.BlockPic).ConfigureAwait(false);
            await ExpectPrefixAsync(HNAOtaProtocol.Acks.So, options.BlockPicTimeout).ConfigureAwait(false);
        }

        var fileIndex = 0;
        foreach (var part in parts)
        {
            fileIndex++;
            var title =
                "Updating firmware\n(" + (fileIndex - 1) + " of " + fileCount + " complete)";
            Report(title, (int)(sent * 100.0 / totalBytes));

            await SendCmdAsync($"{HNAOtaProtocol.Commands.SetFilePrefix}{part.FileName},{part.FileData.Length}").ConfigureAwait(false);
            await ExpectPrefixAsync(HNAOtaProtocol.Acks.Sf, options.SetFileAckTimeout).ConfigureAwait(false);

            var offset = 0;
            while (offset < part.FileData.Length)
            {
                var len = Math.Min(dataChunkMax, part.FileData.Length - offset);
                await SendSfPayloadAsync(part.FileData.AsMemory(offset, len)).ConfigureAwait(false);
                offset += len;
                sent += len;
                await ExpectPrefixAsync(HNAOtaProtocol.Acks.Sf, options.ChunkAckTimeout).ConfigureAwait(false);

                var pct = (int)(sent * 100.0 / totalBytes);
                Report(
                    "Updating firmware\n(" + (fileIndex - 1) + " of " + fileCount + " complete)",
                    pct);
            }
        }

        Report("Enabling application…", 98);
        await SendCmdAsync(HNAOtaProtocol.Commands.EnablePic).ConfigureAwait(false);
        await ExpectPrefixAsync(HNAOtaProtocol.Acks.So, options.EnablePicTimeout).ConfigureAwait(false);

        await SendCmdAsync(HNAOtaProtocol.Commands.RestartPic).ConfigureAwait(false);

        Report("Firmware update finished.", 100);
    }

    /// <summary>
    /// Max bytes per <c>SF,</c> payload (after the header), derived from ATT MTU like native <c>maximumWriteValueLength - 4</c>.
    /// </summary>
    private int ResolveMaxSfDataBytes(HNABleDeviceModel device, HNAOtaOptions options)
    {
        var mtu = _transport.GetMtuOrDefault(device, 23);
        var maxWrite = mtu - 3;
        // Native: bytesPerPacket = maxWrite - 4; data slice length = bytesPerPacket + 1 ⇒ dataMax = maxWrite - 3.
        var dataMax = maxWrite - 3;
        return Math.Clamp(dataMax, options.MinDataChunkSize, options.MaxDataChunkSize);
    }
}
