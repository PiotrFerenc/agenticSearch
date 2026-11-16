namespace AgenticSearch.Prompting;

/// <summary>Fluent builder składający prompt systemowy z wzorców. Kolejność wywołań = kolejność sekcji.</summary>
public sealed class PromptBuilder
{
    private readonly List<IPromptPattern> _patterns = [];

    public static PromptBuilder Create() => new();

    public PromptBuilder Add(IPromptPattern pattern)
    {
        _patterns.Add(pattern);
        return this;
    }

    /// <summary>Dodaje wzorce tylko wtedy, gdy warunek jest spełniony (np. opcjonalny agent kodu).</summary>
    public PromptBuilder When(bool condition, Func<PromptBuilder, PromptBuilder> configure) =>
        condition ? configure(this) : this;

    public PromptBuilder Persona(string role, string mission) => Add(new PersonaPattern(role, mission));
    public PromptBuilder Audience(string audience, string style) => Add(new AudiencePattern(audience, style));
    public PromptBuilder Scope(string[] inScope, string[] outOfScope) => Add(new ContextManagerPattern(inScope, outOfScope));
    public PromptBuilder Rules(params string[] rules) => Add(new RulesPattern(rules));
    public PromptBuilder ReAct(params string[] steps) => Add(new ReActPattern(steps));
    public PromptBuilder ThinkStepByStep() => Add(new ThinkStepByStepPattern());
    public PromptBuilder CognitiveVerifier(params string[] aspects) => Add(new CognitiveVerifierPattern(aspects));
    public PromptBuilder QuestionRefinement(string hint) => Add(new QuestionRefinementPattern(hint));
    public PromptBuilder FlippedInteraction(int maxQuestions, string whenToAsk) => Add(new FlippedInteractionPattern(maxQuestions, whenToAsk));
    public PromptBuilder Template(string template) => Add(new TemplatePattern(template));
    public PromptBuilder FewShot(params (string Input, string Output)[] examples) => Add(new FewShotPattern(examples));
    public PromptBuilder Reflection(params string[] checks) => Add(new ReflectionPattern(checks));
    public PromptBuilder FactCheckList(string sourceKind) => Add(new FactCheckListPattern(sourceKind));
    public PromptBuilder Fallback(string whenMissing) => Add(new FallbackPattern(whenMissing));

    public string Build() =>
        string.Join("\n\n", _patterns.Select(p => $"# {p.Name}\n{p.Render()}"));
}
