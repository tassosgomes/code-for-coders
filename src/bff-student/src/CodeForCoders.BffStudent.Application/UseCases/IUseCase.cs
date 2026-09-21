namespace CodeForCoders.BffStudent.Application.UseCases;

public interface IUseCase<in TInput, TOutput>
{
    Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken);
}

public interface IUseCase<in TInput>
{
    Task ExecuteAsync(TInput input, CancellationToken cancellationToken);
}
