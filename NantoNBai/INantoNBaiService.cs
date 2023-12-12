using System.IO;

namespace NantoNBai
{
    public interface INantoNBaiService
    {
        Stream Generate(string baseDirectoryPath, string target, double from, double to, Nan nan, string contentType);
    }
}
