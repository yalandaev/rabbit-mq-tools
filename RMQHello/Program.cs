using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using log4net;
using RabbitMQTools;

namespace RMQHello
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger("CommonLogger");

        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo(AppDomain.CurrentDomain.BaseDirectory + "log4net.config"));

            try
            {
                RabbitJSONConfigurator.Configure();
            }
            catch(TypeInitializationException ex)
            {
                Log.Error("Initialization error (perhaps, you need to check app.config file)");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }
}
