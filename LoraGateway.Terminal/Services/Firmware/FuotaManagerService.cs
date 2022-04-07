using System.ComponentModel.DataAnnotations;
using Google.Protobuf;
using JKang.EventBus;
using LoRa;
using LoraGateway.Handlers;
using LoraGateway.Models;
using LoraGateway.Services.Contracts;
using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.Packets;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Utils;

namespace LoraGateway.Services;

public class FuotaManagerService : JsonDataStore<FuotaConfig>
{
    private readonly BlobFragmentationService _blobFragmentationService;

    private readonly CancellationTokenSource _cancellation = new();
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<FuotaManagerService> _logger;
    private readonly RlncEncodingService _rlncEncodingService;
    private FuotaSession? _currentFuotaSession;
    private List<UnencodedPacket> _firmwarePackets = new();

    // Whether we attempted to start a remote session
    public bool IsRemoteSessionStarted = false;

    public FuotaManagerService(
        ILogger<FuotaManagerService> logger,
        IEventPublisher eventPublisher,
        BlobFragmentationService blobFragmentationService,
        RlncEncodingService rlncEncodingService
    )
    {
        _logger = logger;
        _eventPublisher = eventPublisher;
        _blobFragmentationService = blobFragmentationService;
        _rlncEncodingService = rlncEncodingService;
    }

    public bool ShouldWait()
    {
        var session = GetCurrentSession();
        var currentGen = session.CurrentGenerationIndex;
        var genSize = session.Config.GenerationSize + session.Config.GenerationSizeRedundancy;
        var expectedAcksCount = genSize * ((int)currentGen) + session.CurrentFragmentIndex;
        return _currentFuotaSession!.Acks.Count < expectedAcksCount;
    }

    public void SetGeneratorType(RlncEncodingService.RandomGeneratorType type)
    {
        _rlncEncodingService.GeneratorType = type;
    }

    public void SetPrngSeed(UInt32 seed)
    {
        Store.PRngSeedState = seed;
        WriteStore();
    }
    
    public void SetPacketErrorRate(float per)
    {
        Store.ApproxPacketErrorRate = per;
        WriteStore();
    }

    public void SetPacketErrorSeed(UInt16 seed)
    {
        Store.PacketErrorSeed = seed;
        WriteStore();
    }

    public LoRaMessage RemoteSessionStopCommand()
    {
        var loraMessage = new LoRaMessage();
        loraMessage.RlncRemoteFlashStopCommand = new();
        IsRemoteSessionStarted = false;
        return loraMessage;
    }
    
    public LoRaMessage RemoteSessionStartCommand()
    {
        var config = GetStore();
        var loraMessage = new LoRaMessage();
        loraMessage.RlncRemoteFlashStartCommand = new()
        {
            DeviceId0 = config.RemoteDeviceId0,
            SetIsMulticast = config.RemoteIsMulticast,
            TimerDelay = config.RemoteUpdateIntervalMs,
            DebugFragmentUart = config.DebugFragmentUart,
            DebugMatrixUart = config.DebugMatrixUart,
            TransmitConfiguration = new TransmitConfiguration()
            {
                TxBandwidth = config.TxBandwidth,
                TxPower = config.TxPower,
                TxDataRate = config.TxDataRate
            },
            ReceptionRateConfig = new ()
            {
                PacketErrorRate = config.ApproxPacketErrorRate,
                OverrideSeed = config.OverridePacketErrorSeed,
                DropUpdateCommands = config.DropUpdateCommands,
                Seed = config.PacketErrorSeed
            }
        };
        
        IsRemoteSessionStarted = true;

        return loraMessage;
    }

    public bool IsAwaitAckEnabled()
    {
        return GetCurrentSession().Config.UartFakeAwaitAck;
    }

    public async Task ReloadStore()
    {
        await StopFuotaSession(true);
        await LoadStore();
    }

    public async Task HandleRlncConsoleCommand()
    {
        if (Store == null) await LoadStore();

        if (!IsFuotaSessionEnabled())
            await StartFuotaSession(true);
        else
            await StopFuotaSession(true);
    }

    public override string GetJsonFileName()
    {
        return "fuota_config.json";
    }

    public bool IsFuotaSessionEnabled()
    {
        return _currentFuotaSession != null;
    }

    public bool IsLastGeneration()
    {
        if (_currentFuotaSession == null) return true;

        var currentGen = _currentFuotaSession.CurrentGenerationIndex + 1;
        var maxGen = _currentFuotaSession.GenerationCount;

        return currentGen >= maxGen;
    }

    public bool IsCurrentGenerationComplete()
    {
        if (_currentFuotaSession == null) return true;

        var config = _currentFuotaSession.Config;
        var redundancy = config.GenerationSizeRedundancy;

        var maxGenerationFragments = config.GenerationSize + redundancy;
        int remainingFragments = (int)_currentFuotaSession.TotalFragmentCount -
                                 (int)config.GenerationSize * (int)_currentFuotaSession.CurrentGenerationIndex;
        var minimumFragmentsRemaining = Math.Min(maxGenerationFragments, remainingFragments + redundancy);

        var fragmentIndex = _currentFuotaSession.CurrentFragmentIndex;
        return fragmentIndex >= minimumFragmentsRemaining;
    }

    public bool IsFuotaSessionDone()
    {
        return IsCurrentGenerationComplete() && IsLastGeneration();
    }

    public FuotaSession GetCurrentSession()
    {
        if (_currentFuotaSession == null) throw new ValidationException("Cant provide fuota session as its unset");
        return _currentFuotaSession;
    }

    public async Task StartFuotaSession(bool publishEvent)
    {
        _logger.LogDebug("Starting FUOTA session");

        await PrepareFuotaSession();

        var result = _cancellation.TryReset();
        if (!result) _logger.LogDebug("Resetting of FUOTA cancellation source failed. Continuing anyway");

        if (publishEvent)
            await _eventPublisher.PublishEventAsync(new InitFuotaSession { Message = "Initiating" });
    }

    public async Task StopFuotaSession(bool publishEvent)
    {
        _logger.LogDebug("Stopping FUOTA session");
        _cancellation.Cancel();

        // Termination imminent - clear the session and terminate the hosted service
        if (publishEvent)
            await _eventPublisher.PublishEventAsync(new StopFuotaSession { Message = "Stopping" });

        ClearFuotaSession();
    }

    private async Task PrepareFuotaSession()
    {
        if (Store == null)
            throw new ValidationException(
                "Fuota config store was not loaded, yet PrepareFuotaSession was called - call LoadStore first");

        if (_firmwarePackets.Count != 0)
            throw new ValidationException(
                "A new fuota session was started, while a previous one was already requested");

        if (Store.UartFakeLoRaRxMode) _logger.LogWarning("UartFakeLoRaRxMode enabled");

        if (Store.FakeFirmware)
        {
            var frameSize = (int)Store.FakeFragmentSize;
            var firmwareSize = Store.FakeFragmentCount * frameSize;
            _firmwarePackets = await _blobFragmentationService.GenerateFakeFirmwareAsync(firmwareSize, frameSize);
            // foreach (var packetIndex in Enumerable.Range(0, _firmwarePackets.Count))
            // {
                // var packet = _firmwarePackets[packetIndex];
                // var packetSerialized = SerialUtil.ByteArrayToString(packet.Payload
                //     .Select(b => b.GetValue())
                //     .ToArray());
                // _logger.LogInformation("OriginalPacket {Packet}", packetSerialized);
            // }
        }
        else
        {
            throw new NotImplementedException(
                "Non-fake firmware generation is not implemented - please turn on the FakeFirmware setting");
        }

        // Prepare 
        _rlncEncodingService.ConfigureEncoding(new EncodingConfiguration
        {
            PRngSeedState = Store.PRngSeedState,
            CurrentGeneration = 0,
            FieldDegree = Store.FieldDegree,
            GenerationSize = Store.GenerationSize
        });

        _logger.LogWarning("Using new PRNG seed/state {Seed}", _rlncEncodingService.GetGeneratorState());
        var genCountResult =
            (uint)_rlncEncodingService.PreprocessGenerations(_firmwarePackets, Store.GenerationSize);

        _currentFuotaSession = new FuotaSession(Store, genCountResult)
        {
            TotalFragmentCount = (uint)_firmwarePackets.Count
        };
    }

    public void LogSessionProgress()
    {
        if (!IsFuotaSessionEnabled())
        {
            _logger.LogInformation("Fuota session stopped - no progress to log");
            return;
        }

        if (IsCurrentGenerationComplete())
        {
            _logger.LogInformation("Generation {GenIndex} complete - switching to next generation",
                _currentFuotaSession!.CurrentGenerationIndex + 1);
            return;
        }

        var session = GetCurrentSession();
        var currentGen = session.CurrentGenerationIndex + 1;
        var maxGen = session.GenerationCount;
        var genTotal = session.Config.GenerationSize + session.Config.GenerationSizeRedundancy;
        var fragment = genTotal * (currentGen - 1) + session.CurrentFragmentIndex + 1;
        var fragmentMax = session.Config.GenerationSize + maxGen * session.Config.GenerationSizeRedundancy;
        var progress = Math.Round(100.0 * fragment / fragmentMax, 0);

        _logger.LogInformation("Progress {Progress}% Gen {Gen}/{MaxGen} Fragment {Frag}/{MaxFrag}",
            progress, currentGen, maxGen, fragment, fragmentMax);
    }

    public void MoveNextRlncGeneration()
    {
        if (_currentFuotaSession == null) throw new ValidationException("Cant fetch RLNC payload when session is null");

        // Increment generation, reset fragment index
        _currentFuotaSession.IncrementGenerationIndex();

        // Increase the encoder generation as well
        _rlncEncodingService.MoveNextGeneration();
    }

    public FragmentWithSeed FetchNextRlncPayloadWithGenerator(bool logPacket = true)
    {
        if (_currentFuotaSession == null) throw new ValidationException("Cant fetch RLNC payload when session is null");

        if (IsCurrentGenerationComplete())
        {
            if (!_rlncEncodingService.HasNextGeneration())
                throw new ValidationException("Cant fetch RLNC payload when no generation is left");

            throw new ValidationException("Generation packets have run out");
        }

        var currentPrngSeedState = _rlncEncodingService.GetGeneratorState();
        var encodedPacket = _rlncEncodingService.PrecodeNumberOfPackets(1).First();
        var fragmentBytes = encodedPacket.Payload.Select(p => p.GetValue());
        // var encodingVector = encodedPacket.EncodingVector.Select(p => p.GetValue()).ToArray();

        // if (logPacket)
        //     _logger.LogInformation("Vector {Vector}| Packet {Message}| Gen {Generator} -> {CurrentGenerator}",
        //         SerialUtil.ByteArrayToString(encodingVector),
        //         SerialUtil.ByteArrayToString(fragmentBytes.ToArray()),
        //         currentLfsrState,
        //         _rlncEncodingService.GetGeneratorState()
        //     );

        // Next fragment index to be sent is 1 higher
        _currentFuotaSession.IncrementFragmentIndex();

        return new()
        {
            GenerationIndex = (byte)_currentFuotaSession.CurrentGenerationIndex,
            Fragment = fragmentBytes.ToArray(),
            PrngSeedState = currentPrngSeedState,
            OriginalPacket = encodedPacket
        };
    }

    public void SaveFuotaDebuggingProgress(string source, DecodingUpdate update, ByteString payload)
    {
        // TODO test the min input
        var encodingLength = (int)Math.Min(Store.GenerationSize,
            Store.FakeFragmentCount -
            update.CurrentGenerationIndex * Store.GenerationSize); // encodedPacket.EncodingVector.Count;
        var rank = update.RankProgress;

        var arrayPayload = payload.ToArray();
        if (arrayPayload.Length > 0)
        {
            var encodingVector = new ArraySegment<byte>(arrayPayload, 0, encodingLength);
            var payloadVector =
                new ArraySegment<byte>(arrayPayload, encodingLength, arrayPayload.Length - encodingLength);

            _logger.LogInformation(
                "Vector {Vector}| Packet {Packet} (OUTPUT)",
                SerialUtil.ByteArrayToString(encodingVector.ToArray()),
                SerialUtil.ByteArrayToString(payloadVector.ToArray())
            );
        }

        _logger.LogDebug(
            "[{Name}, DecodingType] Rank: {Rank} GenIndex: {MatrixRank} FragRx: {ReceivedFragments} FragMiss: {MissedFragments} FirstRowCrc: {FirstRowCrc} LastAppendedRowCrc({LastRowIndex}): {LastRowCrc} LFSR {Lfsr1} -> {Lfsr2} IsRunning: {IsRunning}",
            source,
            rank,
            update.CurrentGenerationIndex,
            update.ReceivedFragments,
            update.MissedGenFragments,
            update.FirstRowCrc8,
            update.LastRowIndex,
            update.LastRowCrc8,
            update.UsedPrngSeedState,
            update.CurrentPrngState,
            update.IsRunning
        );

        _currentFuotaSession?.Acks.Add(update);
    }

    public void ClearFuotaSession()
    {
        // if (!IsFuotaSessionEnabled()) throw new ValidationException("Cant stop FUOTA session when none is enabled");

        _firmwarePackets = new List<UnencodedPacket>();
        _currentFuotaSession = null;
        _rlncEncodingService.ResetEncoding();
    }

    public override FuotaConfig GetDefaultJson()
    {
        var jsonStore = new FuotaConfig();

        // Adjust anything deviating from default here

        return jsonStore;
    }
}