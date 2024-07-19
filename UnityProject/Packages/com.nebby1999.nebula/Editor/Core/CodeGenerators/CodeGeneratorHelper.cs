using Microsoft.CSharp;
using System;

namespace Nebula.Editor
{
    public class CodeGeneratorHelper : IDisposable
    {
        private CSharpCodeProvider _codeProvider;

        public string MakeAutoGeneratedCodeHeader(string toolName, string toolVersion, string sourceFileName = null)
        {
            return
                "//------------------------------------------------------------------------------\n"
                + "// <auto-generated>\n"
                + $"//     This code was auto-generated by {toolName}\n"
                + $"//     version {toolVersion}\n"
                + (string.IsNullOrEmpty(sourceFileName) ? "" : $"//     from {sourceFileName}\n")
                + "//\n"
                + "//     Changes to this file may cause incorrect behavior and will be lost if\n"
                + "//     the code is regenerated.\n"
                + "// </auto-generated>\n"
                + "//------------------------------------------------------------------------------\n";
        }

        public void Dispose()
        {
            _codeProvider?.Dispose();
        }

        public CodeGeneratorHelper()
        {
            _codeProvider = new CSharpCodeProvider();
        }
    }
}