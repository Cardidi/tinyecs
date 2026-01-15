using System.Runtime.CompilerServices;

namespace TinyECS.Utils
{
    public interface ILogger
    {
        public void Debug(string msg, [CallerMemberName] object context = null);
        
        public void Info(string msg, [CallerMemberName] object context = null);
        
        public void Warn(string msg, [CallerMemberName] object context = null);
        
        public void Err(string msg, [CallerMemberName] object context = null);
        
        public void Exp(Exception err, [CallerMemberName] object context = null);
    }
    
    public static class Log
    {
        public static ILogger Logger { get; set; } = null;
        
        public static void Debug(string msg, [CallerMemberName] object context = null)
        {
            Logger?.Debug(msg, context);
        }
        
        public static void Info(string msg, [CallerMemberName] object context = null)
        {
            Logger?.Info(msg, context);
        }
        
        public static void Warn(string msg, [CallerMemberName] object context = null)
        {
            Logger?.Warn(msg, context);
        }
        
        public static void Err(string msg, [CallerMemberName] object context = null)
        {
            Logger?.Err(msg, context);
        }
        
        public static void Exp(Exception err, [CallerMemberName] object context = null)
        {
            Logger?.Exp(err, context);
        }
    }
}