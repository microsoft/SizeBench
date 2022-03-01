using System.Text;

namespace SizeBench.ErrorReporting.ErrorInfoProviders;

public interface IErrorInfoProvider
{
    void AddErrorInfo(StringBuilder body);
}
