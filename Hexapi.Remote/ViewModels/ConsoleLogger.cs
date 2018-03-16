using System;
using System.IO;
using System.Reactive.Subjects;
using System.Text;

namespace Hexapi.Remote.ViewModels
{
    public class SystemConsoleRedirect : TextWriter
    {
        internal ISubject<string> LogSubject { get; } = new Subject<string>();

        public override void Write(char value)
        {
            //Do something, like write to a file or something
            
        }

        public override void Write(string value)
        {
            LogSubject.OnNext(value);
        }

        public override Encoding Encoding
        {
            get
            {
                return Encoding.ASCII;
            }
        }
    }
}