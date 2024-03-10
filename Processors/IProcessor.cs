using DoMyThingWorker.Models;

namespace DoMyThingWorker.Processors
{
    public interface IProcessor<T, TResult> where T : RequestModelBase
                                            where TResult : ResponseModelBase
    {
        public Task<TResult> ProcessAsync(T request);
    }
}
