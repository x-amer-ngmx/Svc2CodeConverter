using System;
using System.CodeDom.Compiler;
using System.IO;

namespace Svc2CodeConverter
{
    public class AbstractIndentedTextWriter : IndentedTextWriter
    {
        private bool IsVirtual { get; set; }
        public AbstractIndentedTextWriter(TextWriter writer, bool isVirtual) : base(writer)
        {
            IsVirtual = isVirtual;
        }

        public AbstractIndentedTextWriter(TextWriter writer, string tabString) : base(writer, tabString)
        {
        }

        public override void Write(string s)
        {
            if (s.IndexOf("abstract", StringComparison.Ordinal) >= 0)
            {
                base.Write(s.Replace("abstract ", IsVirtual ? "virtual " : ""));
                return;
            }
            base.Write(s);
        }
    }
}
