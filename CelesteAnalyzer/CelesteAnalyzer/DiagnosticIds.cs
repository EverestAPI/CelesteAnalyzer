namespace CelesteAnalyzer;

public static class DiagnosticIds
{
    internal const string DontUseLambdas = "CL0001";
    internal const string DontEmitInstanceMethods = "CL0002";
    internal const string CallOrigInHooks = "CL0003";
    internal const string HooksShouldBeStatic = "CL0004";
    internal const string DontUseCursorRemove = "CL0005";
    internal const string DontChainPredicatesInCursorGoto = "CL0006";
    internal const string CustomEntityWithNoValidCtor = "CL0007";
    internal const string CustomEntityNotExtendingEntity = "CL0008";
    internal const string CustomEntityGeneratorMethodMissing = "CL0009";
    internal const string CustomEntityGeneratorInvalidParams = "CL0010";
    internal const string CustomEntityGeneratorInvalid = "CL0011";
    internal const string CustomEntityNoIDs = "CL0012";
    internal const string UsingSceneInWrongPlace = "CL0013";
}