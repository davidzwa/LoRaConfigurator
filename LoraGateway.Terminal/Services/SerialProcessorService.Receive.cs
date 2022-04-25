using LoRa;
using LoraGateway.Handlers;
using LoraGateway.Models;
using LoraGateway.Utils;

namespace LoraGateway.Services;

public partial class SerialProcessorService
{
    protected bool LoraRxMessageSuppressed;

    public bool SetLoraRxMessagesSuppression(bool val)
    {
        return LoraRxMessageSuppressed = val;
    }

    private async Task ReceiveBootMessage(string portName, UartResponse response)
    {
        var deviceFullId = response.BootMessage.DeviceIdentifier;
        var deviceId = deviceFullId.DeviceIdAsString();
        var firmwareVersion = response.BootMessage.GetFirmwareAsString();
        var measurementCount = response.BootMessage.MeasurementCount;
        var measurementDisabled = response.BootMessage.MeasurementsDisabled;
        var flashState = response.BootMessage.RlncFlashState;
        var sessionState = response.BootMessage.RlncSessionState;
        var device = await _deviceStore.GetOrAddDevice(new Device
        {
            HardwareId = deviceId,
            Id = deviceFullId.Id0,
            FirmwareVersion = firmwareVersion,
            LastPortName = portName
        });

        _logger.LogInformation(
            "[{Port} {Name}, MC:{Count}, MD:{Disabled}, RLNC:{RlncSessionState}-{FlashState}] heart beat {DeviceId}",
            portName,
            device?.NickName,
            measurementCount,
            measurementDisabled,
            sessionState,
            flashState,
            deviceId);
    }

    private async Task<int> ReceiveDebugMessage(string portName, UartResponse response)
    {
        var serialString = SerialUtil.ByteArrayToString(response.Payload.ToArray());
        var payload = response.Payload.ToStringUtf8();
        var code = response.DebugMessage.Code;

        if (payload!.Contains("CRC_FAIL"))
            await _eventPublisher.PublishEventAsync(new StopFuotaSession { Message = "CRC failure" });

        if (payload.Contains("PROTO_FAIL"))
            await _eventPublisher.PublishEventAsync(new StopFuotaSession { Message = "PROTO failure" });

        if (payload.Contains("RLNC_TERMINATE"))
            await _eventPublisher.PublishEventAsync(new StopFuotaSession
                { Message = "End-device succeeded generation", SuccessfulTermination = true });

        if (payload.Contains("PeriodTX"))
        {
            await _eventPublisher.PublishEventAsync(new PeriodTxEvent
            {
                PortName = portName
            });
            return 0;
        }

        string[] inclusions =
        {
            // "PeriodTX",
            "PROTO_FAIL",
            "PROTO_FAIL_TX",
            "PROTO_LORA_FAIL",
            "CRC_FAIL",
            "RLNC_TERMINATE",
            "LORATX_TIMEOUT",
            "RAMFUNC",
            "FLASH",
            // "UC",
            // "MC",
            // "LORARX_DONE",
            // "LORATX_DONE",
            // "RLNC_NVM",
            "RLNC_PARSED_SEQ",
            // "RLNC_RNG_DROP",
            // "RLNC_RNG_ACPT",
            "RLNC_LAG_GEN",
            // "RLNC_LAG_FRAG",
            // "RLNC_PER_SEED",
            "RLNC_ERR",
            // "RNG",
            "DevConf", // TODO change to device ACK
            "DevConfStop",
            "PUSH-BUTTON"
        };
        if (inclusions.Any(e => payload.Contains(e)))
            _logger.LogInformation("[{Name}, Debug] {Payload} Code:{Code} (Hex {Hex})", portName, payload, code,
                Convert.ToString(code, 16));
        else
            _logger.LogDebug("!! [{Name}, Debug] {Payload} Code:{Code} \n\t {SerialPayload}", portName, payload, code,
                serialString);

        return 0;
    }

    private void ReceiveExceptionMessage(string portName, UartResponse response)
    {
        var payload = response.Payload;
        var code = response.ExceptionMessage?.Code;

        _logger.LogError("[{Name}, Exception] {Payload} Code:{Code}", portName, payload.ToStringUtf8(), code);
    }

    private async Task ReceiveDecodingUpdate(string portName, UartResponse response)
    {
        var result = response.DecodingUpdate;

        // Let the event handler, fuota manager and hosted service fix the rest
        await _eventPublisher.PublishEventAsync(new DecodingUpdateEvent
        {
            Source = portName,
            DecodingUpdate = result,
            Payload = response.Payload
        });
    }

    private void ReceiveDecodingMatrix(string portName, UartResponse response)
    {
        var payloadArray = response.Payload.ToArray();
        var matrix = response.DecodingMatrix;
        _logger.LogInformation(
            "[{Name}, DecodingMatrix] Rows {Rows} Cols {Cols}",
            portName,
            matrix.Rows,
            matrix.Cols
        );

        for (uint i = 0; i < matrix.Rows; i++)
        {
            var matrixRow = SerialUtil.ArrayToStringLim(payloadArray, (int)(i * matrix.Cols), (int)matrix.Cols);
            _logger.LogInformation("\t{MatrixRow}", matrixRow);
        }
    }

    private void ReceiveDecodingResult(string portName, UartResponse response)
    {
        var decodingResult = response.DecodingResult;
        var success = decodingResult.Success;
        var missedGenFragments = decodingResult.MissedGenFragments;
        var receivedGenFragments = decodingResult.ReceivedFragments;

        _eventPublisher.PublishEventAsync(new DecodingResultEvent
        {
            DecodingResult = decodingResult
        });

        var total = receivedGenFragments + missedGenFragments;
        var perReal = (float)missedGenFragments / total;

        _logger.LogInformation(
            "[{Name}, DecodingResult] Success: {Payload} GenIndex {GenIndex} Rank: {MatrixRank} PER {Rx}/{Total}={Per:F2} FirstNumber: {FirstNumber} LastNumber: {LastNumber}",
            portName,
            success,
            decodingResult.CurrentGenerationIndex,
            decodingResult.MatrixRank,
            decodingResult.MissedGenFragments,
            total,
            perReal,
            decodingResult.FirstDecodedNumber,
            decodingResult.LastDecodedNumber
        );
    }

    private async Task ReceiveLoRaMeasurement(string portName, UartResponse response)
    {
        var bodyCase = response?.LoraMeasurement?.DownlinkPayload?.BodyCase;
        if (bodyCase == null)
        {
            _logger.LogInformation("[{Name}] LoRa RX unknown", portName);
            return;
        }

        if (bodyCase is LoRaMessage.BodyOneofCase.ExperimentResponse
            or LoRaMessage.BodyOneofCase.RlncRemoteFlashResponse)
        {
            await InnerLoRaPacketHandler(portName, response.LoraMeasurement?.DownlinkPayload);
        }
        else if (bodyCase is LoRaMessage.BodyOneofCase.None)
        {
            // Measurement was a dummy - send to RX handler
            await _eventPublisher.PublishEventAsync(new RxEvent
            {
                Message = response,
                PortName = portName
            });
            
            // Suppress
            return;
        }

        // Suppress anything else   
        if (!response.LoraMeasurement.Success)
        {
            _logger.LogWarning("[{Name}] LoRa RX error!", portName);
            return;
        }

        var snr = response.LoraMeasurement.Snr;
        var rssi = response.LoraMeasurement.Rssi;
        var sequenceNumber = response.LoraMeasurement.SequenceNumber;
        var isMeasurement = response.LoraMeasurement.IsMeasurementFragment;
        if (LoraRxMessageSuppressed)
        {
            _logger.LogDebug(
                "[{Name}] LoRa RX snr: {SNR} rssi: {RSSI} sequence-id:{Index} is-measurement:{IsMeasurement}",
                portName,
                snr, rssi, sequenceNumber, isMeasurement);
            return;
        }

        var result = await _measurementsService.AddMeasurement(sequenceNumber, snr, rssi);
        if (sequenceNumber > 60000) _measurementsService.SetLocationText("");
        _logger.LogInformation(
            "[{Name}] LoRa RX snr: {SNR} rssi: {RSSI} sequence-id:{Index} is-measurement:{IsMeasurement}, skipped:{Skipped}",
            portName,
            snr, rssi, sequenceNumber, isMeasurement, result);
    }

    private async Task InnerLoRaPacketHandler(string portName, LoRaMessage? message)
    {
        if (message == null) return;

        var messageType = message.BodyCase;
        if (messageType == LoRaMessage.BodyOneofCase.ExperimentResponse)
        {
            var flashMeasureCount = message.ExperimentResponse.MeasurementCount;
            _logger.LogInformation("[{PortName}] Flash {FlashMeasureCount}", portName, flashMeasureCount);
        }
        else if (messageType == LoRaMessage.BodyOneofCase.RlncRemoteFlashResponse)
        {
            var body = message.RlncRemoteFlashResponse;
            var txPower = body.CurrentTxPower;
            var bandwidth = body.CurrentTxBandwidth;
            var spreadingFactor = body.CurrentTxDataRate;
            var delay = body.CurrentTimerDelay;
            var flashState = body.RlncFlashState;
            var sessionState = body.RlncSessionState;
            var mc = body.CurrentSetIsMulticast;
            var deviceId0 = body.CurrentDeviceId0;
            _logger.LogInformation(
                "[{PortName}] RLNC Response\n TxPower:{TXPower} BW:{BW} SF{SF}\n Period:{Delay} Session:{SessionState} Flash:{FlashState} MC:{MC} DeviceId0:{DeviceId0}",
                portName, txPower, bandwidth, spreadingFactor, delay, sessionState, flashState, mc, deviceId0);

            await _eventPublisher.PublishEventAsync(new RlncRemoteFlashResponseEvent
            {
                Source = portName,
                FlashResponse = body
            });
        }
    }
}