using System.IO;

namespace NantoNBai
{
    public interface INantoNBaiService
    {
        Stream Generate(string target, double from, double to, string contentType);
    }
}
