using LoRa;
using LoraGateway.Handlers;
using LoraGateway.Models;
using LoraGateway.Utils;

namespace LoraGateway.Services;

public partial class SerialProcessorService
{
    async Task ReceiveBootMessage(string portName, UartResponse response)
    {
        var deviceFullId = response.BootMessage.DeviceIdentifier;
        var deviceId = deviceFullId.DeviceIdAsString();
        var firmwareVersion = response.BootMessage.GetFirmwareAsString();
        var measurementCount = response.BootMessage.MeasurementCount;
        var measurementDisabled = response.BootMessage.MeasurementsDisabled;
        var device = await _deviceStore.GetOrAddDevice(new Device
        {
            HardwareId = deviceId,
            Id = deviceFullId.Id0,
            FirmwareVersion = firmwareVersion,
            LastPortName = portName
        });

        _logger.LogInformation("[{Port} {Name}, MC:{Count}, MD:{Disabled}] heart beat {DeviceId}", portName,
            device?.NickName,
            measurementCount, measurementDisabled, deviceId);
    }

    async Task<int> ReceiveDebugMessage(string portName, UartResponse response)
    {
        var serialString = SerialUtil.ByteArrayToString(response.Payload.ToArray());
        var payload = response.Payload.ToStringUtf8();
        var code = response.DebugMessage.Code;

        if (payload!.Contains("CRC-FAIL"))
        {
            await _eventPublisher.PublishEventAsync(new StopFuotaSession{Message = "CRC failure"});
        }

        if (payload!.Contains("PROTO-FAIL"))
        {
            await _eventPublisher.PublishEventAsync(new StopFuotaSession{Message = "PROTO failure"});
        }

        if (payload!.Contains("RLNC_TERMINATE"))
        {
            await _eventPublisher.PublishEventAsync(new StopFuotaSession{Message = "End-device succeeded generation"});
        }
        if (payload!.Contains("MC") || payload!.Contains("UC"))
        {
            return 1;
        }

        string[] inclusions =
        {
            "LORATX-DONE",
            "PeriodTX",
            "PROTO-FAIL",
            "CRC-FAIL",
            "RLNC_TERMINATE",
            "INSERT_ROW",
            "RAMFUNC",
            "DevConfStop",
            "PUSH-BUTTON"
        };
        if (inclusions.Any(e => payload.Contains(e)))
        {
            _logger.LogInformation("[{Name}, Debug] {Payload} Code:{Code}", portName, payload, code);    
        }
        else {
            _logger.LogDebug("!! [{Name}, Debug] {Payload} Code:{Code} \n\t {SerialPayload}", portName, payload, code, serialString);
        }
        return 0;
    }

    void ReceiveExceptionMessage(string portName, UartResponse response)
    {
        var payload = response.Payload;
        var code = response.ExceptionMessage?.Code;

        _logger.LogError("[{Name}, Exception] {Payload} Code:{Code}", portName, payload.ToStringUtf8(), code);
    }
    
    async Task ReceiveDecodingUpdate(string portName, UartResponse response)
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

    void ReceiveDecodingMatrix(string portName, UartResponse response)
    {
        var matrix = response.Payload.ToArray();
        var sizes = response.DecodingMatrix;
        _logger.LogInformation(
            "[{Name}, DecodingMatrix] Rows {Rows} Cols {Cols}",
            portName,
            sizes.Rows,
            sizes.Cols
        );

        for (uint i=0; i < sizes.Rows; i++)
        {
            var matrixRow = SerialUtil.ArrayToStringLim(matrix, (int)(i * sizes.Cols), (int)(sizes.Cols));
            _logger.LogInformation("\t{MatrixRow}", matrixRow);
        }
    }
    
    void ReceiveDecodingResult(string portName, UartResponse response)
    {
        var decodingResult = response.DecodingResult;
        var success = decodingResult.Success;
        _logger.LogInformation(
            "[{Name}, DecodingResult] Success: {Payload} Rank: {MatrixRank} FirstNumber: {FirstNumber} LastNumber: {LastNumber}",
            portName,
            success,
            decodingResult.MatrixRank,
            decodingResult.FirstDecodedNumber,
            decodingResult.LastDecodedNumber
        );
    }

    async Task ReceiveLoRaMeasurement(string portName, UartResponse response)
    {
        if (!response.LoraMeasurement.Success)
        {
            _logger.LogInformation("[{Name}] LoRa RX error!", portName);
            return;
        }

        // if (response.LoraMeasurement.Rssi == -1)
        // {
        //     // Suppress internal message
        // }
        // else
        // {
        var snr = response.LoraMeasurement.Snr;
        var rssi = response.LoraMeasurement.Rssi;
        var sequenceNumber = response.LoraMeasurement.SequenceNumber;
        var isMeasurement = response.LoraMeasurement.IsMeasurementFragment;

        var result = await _measurementsService.AddMeasurement(sequenceNumber, snr, rssi);
        if (sequenceNumber > 60000) _measurementsService.SetLocationText("");

        InnerLoRaPacketHandler(response?.LoraMeasurement?.DownlinkPayload);

        // Debug for now
        _logger.LogInformation(
            "[{Name}] LoRa RX snr: {SNR} rssi: {RSSI} sequence-id:{Index} is-measurement:{IsMeasurement}, skipped:{Skipped}",
            portName,
            snr, rssi, sequenceNumber, isMeasurement, result);
        // }
    }
    
    private void InnerLoRaPacketHandler(LoRaMessage? message)
    {
        if (message == null) return;

        if (message.BodyCase == LoRaMessage.BodyOneofCase.ExperimentResponse)
        {
            var flashMeasureCount = message.ExperimentResponse.MeasurementCount;
            _logger.LogInformation("Flash {FlashMeasureCount}", flashMeasureCount);
        }
    }
}