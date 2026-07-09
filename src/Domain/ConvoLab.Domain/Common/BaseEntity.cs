namespace ConvoLab.Domain.Common;
public abstract class BaseEntity<TId> {
    public TId Id { get; protected set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
    protected BaseEntity(TId id) { Id = id; }
    protected BaseEntity() { }
}
