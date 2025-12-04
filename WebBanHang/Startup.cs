using Microsoft.Owin;
using Owin;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

[assembly: OwinStartupAttribute(typeof(WebBanLinhKienDienTu.Startup))]

namespace WebBanLinhKienDienTu
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
            
            // Thêm global exception handler cho SignalR
            GlobalHost.HubPipeline.AddModule(new ErrorHandlingPipelineModule());
            
            // Cấu hình SignalR
            app.MapSignalR();
        }
    }
    
    // Global exception handler cho SignalR Hub
    public class ErrorHandlingPipelineModule : HubPipelineModule
    {
        protected override void OnIncomingError(ExceptionContext exceptionContext, IHubIncomingInvokerContext invokerContext)
        {
            System.Diagnostics.Debug.WriteLine("========================================");
            System.Diagnostics.Debug.WriteLine("SIGNALR GLOBAL EXCEPTION HANDLER");
            System.Diagnostics.Debug.WriteLine("Hub: " + (invokerContext?.Hub?.GetType()?.Name ?? "null"));
            System.Diagnostics.Debug.WriteLine("Method: " + (invokerContext?.MethodDescriptor?.Name ?? "null"));
            System.Diagnostics.Debug.WriteLine("Exception Type: " + exceptionContext.Error.GetType().FullName);
            System.Diagnostics.Debug.WriteLine("Exception Message: " + exceptionContext.Error.Message);
            System.Diagnostics.Debug.WriteLine("Stack Trace: " + exceptionContext.Error.StackTrace);
            
            if (exceptionContext.Error.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine("Inner Exception: " + exceptionContext.Error.InnerException.Message);
                System.Diagnostics.Debug.WriteLine("Inner Stack Trace: " + exceptionContext.Error.InnerException.StackTrace);
            }
            
            System.Diagnostics.Debug.WriteLine("========================================");
            
            base.OnIncomingError(exceptionContext, invokerContext);
        }
    }
}