using ConvoLab.Domain.Common;
namespace ConvoLab.Domain.Evaluation.Entities;
public class EvaluationMetric : BaseEntity<Guid> {
    public string Name { get; private set; } = null!;
    public double Value { get; private set; }
    public string Unit { get; private set; } = null!;
    private EvaluationMetric() { }
    private EvaluationMetric(Guid id, string name, double value, string unit) : base(id) {
        Name = name; Value = value; Unit = unit;
    }
    public static EvaluationMetric Create(string name, double value, string unit) => new EvaluationMetric(Guid.NewGuid(), name, value, unit);
}
