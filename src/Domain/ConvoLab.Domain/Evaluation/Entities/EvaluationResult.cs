using ConvoLab.Domain.Common;
namespace ConvoLab.Domain.Evaluation.Entities;
public class EvaluationResult : BaseEntity<Guid> {
    public string Aspect { get; private set; }
    public string Result { get; private set; }
    public string? Details { get; private set; }
    private EvaluationResult() { }
    private EvaluationResult(Guid id, string aspect, string result, string? details) : base(id) {
        Aspect = aspect; Result = result; Details = details;
    }
    public static EvaluationResult Create(string aspect, string result, string? details = null) => new EvaluationResult(Guid.NewGuid(), aspect, result, details);
}
