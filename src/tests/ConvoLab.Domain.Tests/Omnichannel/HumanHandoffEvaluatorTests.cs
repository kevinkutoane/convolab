using ConvoLab.Domain.Omnichannel;

namespace ConvoLab.Domain.Tests.Omnichannel;

public class HumanHandoffEvaluatorTests
{
    private readonly KeywordHumanHandoffEvaluator _evaluator = new();

    [Theory]
    [InlineData("I want to speak to a human please")]
    [InlineData("Can you connect me to an agent")]
    [InlineData("I need a representative right now")]
    [InlineData("Transfer to an operator")]
    public void Escalates_On_Explicit_Agent_Requests(string text)
    {
        var result = _evaluator.Evaluate(text);
        Assert.True(result.ShouldEscalate);
        Assert.Equal("CustomerService", result.TargetDepartment);
    }

    [Theory]
    [InlineData("This is ridiculous, stop repeating")]
    [InlineData("You are a useless bot, give me someone real")]
    public void Escalates_On_Severe_Frustration(string text)
    {
        var result = _evaluator.Evaluate(text);
        Assert.True(result.ShouldEscalate);
        Assert.Equal("Escalations", result.TargetDepartment);
    }

    [Theory]
    [InlineData("I will contact my lawyer and the ombudsman")]
    [InlineData("I am filing a POPIA complaint today")]
    public void Escalates_On_Regulatory_And_Legal_Threats(string text)
    {
        var result = _evaluator.Evaluate(text);
        Assert.True(result.ShouldEscalate);
        Assert.Equal("Compliance", result.TargetDepartment);
    }

    [Theory]
    [InlineData("My car was just stolen vehicle emergency")]
    [InlineData("I need to report fraud unauthorized transaction")]
    public void Escalates_On_Urgent_Fraud_Or_Security(string text)
    {
        var result = _evaluator.Evaluate(text);
        Assert.True(result.ShouldEscalate);
        Assert.Equal("Fraud", result.TargetDepartment);
    }

    [Fact]
    public void Escalates_When_Consecutive_Unresolved_Turns_Threshold_Reached()
    {
        var result = _evaluator.Evaluate("I still don't get it", consecutiveFrustrations: 3);
        Assert.True(result.ShouldEscalate);
        Assert.Contains("threshold", result.Reason);
    }

    [Theory]
    [InlineData("Hello, can you explain what comprehensive vehicle insurance covers?")]
    [InlineData("How long does an insurance claim approval normally take?")]
    public void Does_Not_Escalate_Standard_Inquiries(string text)
    {
        var result = _evaluator.Evaluate(text);
        Assert.False(result.ShouldEscalate);
        Assert.Null(result.TargetDepartment);
    }
}
