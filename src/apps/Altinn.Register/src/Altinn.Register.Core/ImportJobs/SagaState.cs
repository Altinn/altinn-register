namespace Altinn.Register.Core.ImportJobs;

/// <summary>
/// Represents the state of a saga, including its unique identifier and associated data.
/// </summary>
/// <typeparam name="T">The type of data associated with the saga state. Must implement <see cref="ISagaStateData{T}"/>.</typeparam>
public sealed class SagaState<T>
    where T : class, ISagaStateData<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SagaState{T}"/> class.
    /// </summary>
    /// <param name="sagaId">The saga id.</param>
    /// <param name="status">The saga status.</param>
    /// <param name="data">The saga data.</param>
    public SagaState(Guid sagaId, SagaStatus status, T? data)
    {
        SagaId = sagaId;
        Status = status;
        Data = data;
    }

    /// <summary>
    /// Gets or sets the unique identifier for the saga instance.
    /// </summary>
    public Guid SagaId { get; }

    /// <summary>
    /// Gets or sets the current status of the saga.
    /// </summary>
    public SagaStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the data associated with the current instance.
    /// </summary>
    public T? Data { get; set; }
}
