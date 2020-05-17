using System;

namespace Papyrus.Automation
{
    public abstract class Manager
    {
        protected static string _tag = "[Papyrus] ";
        protected static string _indent = "\t-> ";
        public bool Processing { get; protected set; } = false;

        protected static void Log(string text)
        {
            #if !IS_LIB
            Console.WriteLine(text);
            #endif
        }
    }
}