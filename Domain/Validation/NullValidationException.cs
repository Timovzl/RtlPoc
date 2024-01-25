using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Rtl.News.RtlPoc.Domain.Validation;

/// <summary>
/// Similar to an <see cref="ArgumentNullException"/>, except in the form of a <see cref="ValidationException"/>.
/// </summary>
[Serializable]
public class NullValidationException : ValidationException
{
    public string ParameterName { get; }

    public NullValidationException(Enum errorCode, string parameterName, [CallerFilePath] string? callerFilePath = null, [CallerMemberName] string? callerMemberName = null)
        : this(errorCode, parameterName, message: CreateErrorMessage(parameterName: parameterName, callerFilePath: callerFilePath, callerMemberName: callerMemberName))
    {
    }

    public NullValidationException(Enum errorCode, string parameterName, string message)
        : base(errorCode, message)
    {
        ParameterName = parameterName ?? throw new ArgumentNullException(nameof(parameterName));
    }

    private static string CreateErrorMessage(string? parameterName, string? callerFilePath, string? callerMemberName)
    {
        if (callerMemberName == ".ctor" && callerFilePath?.EndsWith(".cs") == true)
            parameterName = $"{Path.GetFileNameWithoutExtension(callerFilePath)} {parameterName}";

        return $"The following required data was missing: {parameterName}.";
    }

    #region Serialization

    [Obsolete("Exists because base class has serializable attribute, which strict tools then expect on subclasses as well", DiagnosticId = "SYSLIB0051")]
    protected NullValidationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ParameterName = info.GetString("ParameterName") ?? throw new IOException("Failed to deserialize: ParameterName is missing.");
    }

    [Obsolete("Exists because base class has serializable attribute, which strict tools then expect on subclasses as well", DiagnosticId = "SYSLIB0051")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);

        info.AddValue("ParameterName", ParameterName);
    }

    #endregion
}
