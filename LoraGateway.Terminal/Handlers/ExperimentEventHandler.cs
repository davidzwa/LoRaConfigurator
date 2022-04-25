using JKang.EventBus;
using LoraGateway.Services;

namespace LoraGateway.Handlers;

public class ExperimentEventHandler : IEventHandler<RxEvent>, IEventHandler<PeriodTxEvent>
{
    private readonly ExperimentPhyService _experimentPhyService;

    public ExperimentEventHandler(
        ExperimentPhyService experimentPhyService
    )
    {
        _experimentPhyService = experimentPhyService;
    }

    public async Task HandleEventAsync(RxEvent @event)
    {
        await _experimentPhyService.ReceiveMessage(@event);
    }

    public async Task HandleEventAsync(PeriodTxEvent @event)
    {
        await _experimentPhyService.ReceivePeriodTxMessage(@event);
    }
}