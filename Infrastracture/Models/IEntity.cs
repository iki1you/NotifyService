namespace Abstractions.Models
{
    public interface IHasId<TKey>
    {
        TKey Id { get; set; }
    }

    public interface IEntity : IHasId<long>
    {
    }
}
